"""Start the isolated A/B service and external Google Forms; never start a participant run."""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import socket
import subprocess
import sys
import time
from urllib.request import urlopen
import webbrowser

from lsl_sources import validate_config
from personalized_model import resolve_policy_path


def main():
    root=Path(__file__).resolve().parents[2]
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--port',type=int,default=8766);p.add_argument('--host',default='127.0.0.1')
    p.add_argument('--output',type=Path,default=Path('D:/ExperimentData/LayerAB'))
    p.add_argument('--lsl-config',type=Path,default=None)
    p.add_argument('--personalized-policy',type=Path);p.add_argument('--engineering',action='store_true')
    links=json.loads((root/'Tools/LayerAB/questionnaire-links.json').read_text(encoding='utf-8'))
    a=p.parse_args();policy=resolve_policy_path(a.personalized_policy,root)
    if a.lsl_config is None:
        saved = root / "UserSettings/lsl-source-selection.json"
        a.lsl_config = saved if saved.is_file() else Path(__file__).with_name("lsl-config.example.json")
    selected=validate_config(json.loads(a.lsl_config.read_text(encoding='utf-8-sig')))
    if not {s['kind'] for s in selected['streams']} >= {'EEG'}:
        raise ValueError('Select the Mindtooth EEG source before starting the study service')
    if not a.engineering and any(s.get('synthetic') or s.get('data_origin')!='live_sensor' for s in selected['streams'] if s['kind'] in ('EEG',)):
        raise ValueError('Live study requires live sources. Use --engineering for a separately labeled rehearsal.')
    if not 1024<=a.port<=65535:raise ValueError('Invalid port')
    with socket.socket() as check:
        if check.connect_ex(('127.0.0.1',a.port))==0:
            raise RuntimeError('Selected port is already in use. Keep the current recording intact; stop that service explicitly or choose another port.')
    a.output.mkdir(parents=True,exist_ok=True)
    log=a.output/('service-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')+'.log')
    cmd=[sys.executable,str(root/'Tools/AdaptiveTraining/service.py'),'--host',a.host,'--port',str(a.port),
        '--output',str(a.output.resolve()),'--lsl-config',str(a.lsl_config.resolve()),'--lsl-markers',
        '--evidence-guard-config',str(root/'Tools/EvidenceGuard/config.example.json')]
    if policy:cmd+=['--personalized-policy',str(policy)]
    with log.open('x',encoding='utf-8') as f:
        child=subprocess.Popen(cmd,cwd=root,stdout=f,stderr=subprocess.STDOUT,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
    url=f'http://127.0.0.1:{a.port}'
    for _ in range(40):
        if child.poll() is not None:raise RuntimeError('Service stopped; inspect '+str(log))
        try:
            with urlopen(url+'/clock',timeout=.5) as response:status=json.load(response)
            if status['markerStatus']!='available':raise RuntimeError('LSL marker outlet unavailable: '+status['markerStatus'])
            webbrowser.open(links['training']['responder'])
            print('Post-condition form:',links['training']['responder'],'Final preference:',links['preference']['responder'])
            print('Service PID:',child.pid,'Log:',log,'Markers:',status['markerSourceId']);return
        except (OSError,ValueError):time.sleep(.25)
    raise RuntimeError('Service readiness timed out; inspect '+str(log))


if __name__=='__main__':main()
