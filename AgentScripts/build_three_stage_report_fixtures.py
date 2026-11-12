"""Create isolated report fixtures with the real service and report builder."""
import json
from pathlib import Path
import sys
import tempfile
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools/AdaptiveTraining'))
from service import StudyService
from test_personalized import packet, trials
from comparison_report import report
from fixed_exposure_thresholds import summarize

out=ROOT/'Evidence/Comparison34-20261112/browser'
with tempfile.TemporaryDirectory() as tmp:
    service=StudyService(Path(tmp)/'service');store=service.test_store
    for stage in ('fixed_exposure','layer_a','layer_b'):
        record=store.prepare(dict(participantCode='SYNTHETIC_REPORT_FIXTURE',storageFolder=str(Path(tmp)/'data'),sessionMode='comparison_rehearsal',comparisonStage=stage))
        claim=store.claim(dict(clientId='synthetic',controllerUrl='http://127.0.0.1:8773'))
        p=packet();p.update(sessionId=record['sessionId'],participantCode='SYNTHETIC_REPORT_FIXTURE',sessionType='editor-test',config=claim['config'],stage='preparation',testClientId='synthetic',now=0)
        service.receive(p);rows=trials(34,stage)
        for r in rows:r.update(level=int(r['phaseTrial']>=18),closeTime=r['plannedOnset']+4,offsetTime=r['plannedOnset']+4,responseTime=r['plannedOnset']+1,correct=r['phaseTrial']<18)
        p.update(stage=stage,trials=rows,completedTrials=34,now=154,level=1);service.receive(p)
        result=report(Path(record['folder'])/'service');result.update(evidenceScope='synthetic',syntheticSignal=True)
        (out/(stage+'-report.json')).write_text(json.dumps(result,indent=2),encoding='utf-8')
        if stage=='fixed_exposure':
            predictions=[dict(stage=stage,trial=i,valid=True,origin='live_sensor',score=.3 if i<18 else .7,movie_fraction=0 if i<18 else 1) for i in (6,11,16,22,27,32)]
            for pred in predictions:
                pred.update(start=(pred['trial']+1)*4.5-5,end=(pred['trial']+1)*4.5)
            result['eegWindows']=[dict(p,trial=p['trial']+1) for p in predictions]
            # In-memory UI fixture only; these are not sensor observations or saved model predictions.
            result['fixedExposureThresholds']=summarize(record['config'],rows,predictions,[])
            (out/'fixed-candidate-report.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
        store.complete(record['sessionId'],'complete')
print('Four synthetic report fixtures generated; no production sessions used.')
