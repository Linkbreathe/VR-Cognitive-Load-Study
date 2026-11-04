"""Export software event/response times on the service's physiological sample clock."""
import argparse
import csv
import json
from pathlib import Path

from layer_ab import enabled, condition
from study_timing import map_event


def jsonl(path):
    if path.exists():
        with path.open(encoding="utf-8-sig") as f:
            for line in f:
                if line.strip():yield json.loads(line)


def align(t, clocks):
    if t is None or t < 0:return {"alignment_status":"not_recorded"}
    if not clocks:return {"alignment_status":"no_clock_samples"}
    s=min(clocks,key=lambda c:abs((c['unitySent']+c['unityReceived'])/2-t))
    row=map_event(t,s,s['clockId'],s['unityReceived'])
    if row.get('alignment_status')=='estimated' and s.get('lslAvailable'):
        row['event_lsl_local_s']=row['event_perf_s']+s['lslClock']-s['lslSamplePerf']
        row['lsl_read_uncertainty_s']=s.get('lslReadUncertainty')
    return row


def write_csv(path, rows):
    keys=list(dict.fromkeys(k for row in rows for k in row))
    with path.open('x',newline='',encoding='utf-8') as f:
        writer=csv.DictWriter(f,fieldnames=keys);writer.writeheader();writer.writerows(rows)


def export(unity, service, output):
    unity,service,output=map(Path,(unity,service,output))
    config=json.loads((unity/'adaptive-training-config.json').read_text())
    if not enabled(config):raise ValueError('Expected a layer-ab-v1 recording')
    if unity.name!=service.name:raise ValueError('Unity and service session directory IDs must match')
    clocks=list(jsonl(unity/'clock_sync_ab.jsonl'))
    if not clocks:clocks=list(jsonl(service/'clock_sync_ab.jsonl'))
    ids={c['clockId'] for c in clocks}
    if len(ids)>1:raise ValueError('Service clock changed; split/review this recording before alignment')
    events=[]
    for e in jsonl(unity/'events.jsonl'):
        events.append({k:v for k,v in e.items() if k!='detail'}|{'detail_json':e.get('detail','')}|align(e['actual'],clocks))
    trials=[]
    with (unity/'trials.csv').open(newline='',encoding='utf-8-sig') as f:
        for t in csv.DictReader(f):
            row=dict(t)
            for label,column in [('onset','actual_onset_s'),('response','response_time_s'),('input','input_event_time_s')]:
                row.update({label+'_'+k:v for k,v in align(float(t.get(column,-1)),clocks).items()})
            trials.append(row)
    acknowledgements=[]
    for e in events:
        if e.get('type') != 'external_questionnaire_acknowledged':continue
        occasion=e.get('detail_json','')
        ordinal={'training_1':1,'training_2':2}.get(occasion)
        acknowledgements.append({'session_id':unity.name,'participant_code':config.get('participantCode',''),
            'occasion':occasion,'ordinal':ordinal,'condition':condition(config,ordinal) if ordinal else '',
            'operator_acknowledged_unity_s':e['actual'],'submission_verified':False,
            'event_perf_s':e.get('event_perf_s'),'alignment_status':e.get('alignment_status'),
            'questionnaire_revision':config.get('questionnaireRevision','')})
    output.mkdir(parents=True,exist_ok=False)
    write_csv(output/'events_aligned.csv',events);write_csv(output/'trials_aligned.csv',trials)
    write_csv(output/'questionnaire_acknowledgements.csv',acknowledgements)
    report={'session_id':unity.name,'clock_ids':sorted(ids),'clock_samples':len(clocks),'events':len(events),'trials':len(trials),
        'unqualified_event_times':sum(r.get('alignment_status')!='estimated' for r in events),
        'questionnaire_acknowledgements':len(acknowledgements),'questionnaire_responses':'External Google Forms export; join participant code + session ID + training occasion. Operator acknowledgements are not verified submission receipts.','reference':'event_perf_s matches raw_signals.jsonl corrected_monotonic',
        'lsl_reference':'event_lsl_local_s is the service-host LSL clock, not arbitrary XDF importer timestamps',
        'limitations':'Software onset and input action timestamps only. <=50 ms network estimate and <=5 s clock distance are engineering flags, not measured device accuracy. Preserve raw_signals.jsonl, raw LSL timestamps, correction, clock offset and XDF clock history. No causal/physiological qualification.'}
    (output/'timing_report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    return report


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--unity-session',required=True);p.add_argument('--service-session',required=True);p.add_argument('--output',required=True)
    a=p.parse_args();print(json.dumps(export(a.unity_session,a.service_session,a.output),indent=2))
