"""Synthetic A/B protocol, questionnaire, clock transfer and HTTP regression checks."""
import copy
import json
from pathlib import Path
import tempfile
import threading
import unittest
import urllib.request

import layer_ab
from study_timing import map_event, StudyTiming
from test_personalized import packet, trials
from test_personalized_control import decision_fixture, control_rows
from test_evidence_guard import gaze, complete_predictions
from evidence_guard_adapter import GuardAdapter
from service import StudyService, StudyHTTPServer, make_handler
from export_ab_timing import align, export


def ab_packet(stage="layer_a"):
    p=packet();p['config'].update(studyDesign=layer_ab.REVISION,questionnaireRevision=layer_ab.QUESTIONNAIRE,
        evidenceGuardMode='observe',abInterConditionRestSeconds=180)
    p['stage']=stage
    return p


class LayerABTests(unittest.TestCase):
    def test_balanced_order_and_fixed_mode(self):
        orders=[]
        for group in range(4):
            c=ab_packet()['config'];c['counterbalanceGroup']=group;layer_ab.validate(c)
            orders.append([layer_ab.condition(c,1),layer_ab.condition(c,2)])
            self.assertEqual(layer_ab.guard_mode(c,'layer_a'),'observe')
            self.assertEqual(layer_ab.guard_mode(c,'layer_b'),'enforce')
        self.assertEqual(orders.count(['A','B']),2);self.assertEqual(orders.count(['B','A']),2)
        c['allowCandidateModel']=True
        with self.assertRaises(ValueError):layer_ab.validate(c)

    def test_base_identical_b_only_defers_missing_evidence(self):
        for stage, eyes in [('layer_a',False),('layer_b',False),('layer_b',True)]:
            with self.subTest(stage=stage,eyes=eyes), tempfile.TemporaryDirectory() as d:
                s,p=decision_fixture(Path(d),control_rows(),admitted=False)
                complete_predictions(s)
                p['config'].update(ab_packet()['config']);p['stage']=stage
                for row in s.trials.values():row['trainingStage']=stage
                p['trials']=list(s.trials.values());s.trials={(stage,t['phaseTrial']):t for t in p['trials']}
                if eyes:p['guardGaze']=gaze(p['trials'])
                base=s.decide(p);self.assertEqual(base['action'],'ON')
                frozen=copy.deepcopy(base)
                guard=GuardAdapter(p,Path(d));result=guard.receive(p,s,base)
                self.assertEqual(base,frozen)
                self.assertEqual(result['action'],'HOLD' if stage=='layer_b' and not eyes else 'ON',guard.failure)
                self.assertEqual(result['guardMode'],layer_ab.guard_mode(p['config'],stage))
                off=dict(base,action='OFF',requestId='reduction')
                self.assertEqual(guard.receive(p,s,off)['action'],'OFF')

    def test_condition_switch_keeps_trial_identity_and_resets_predictions(self):
        with tempfile.TemporaryDirectory() as d:
            service=StudyService(d,test_state_dir=Path(d)/'state');p=ab_packet();p['trials']=trials(24,'layer_a')
            service.receive(p);s=service.sessions[p['sessionId']]['personal']
            s.predictions.append({'fake':True});p=copy.deepcopy(p);p['now']+=500;p['stage']='layer_b'
            p['trials']+=trials(24,'layer_b');p['events']=[{'type':'protocol_stage','actual':p['now'],'detail':'layer_b','frame':1,'block':4,'trial':-1}]
            service.receive(p)
            self.assertEqual(len(s.trials),48);self.assertTrue(all('fake' not in r and r['stage']=='layer_b' for r in s.predictions))
            self.assertEqual(service.sessions[p['sessionId']]['guard'].mode,'enforce')

    def test_clock_four_stamp_removes_server_processing(self):
        sample={'id':'1','clockId':'clock','unitySent':10,'unityReceived':10.12,'serverReceived':110.01,'serverSent':110.11}
        row=map_event(10.05,sample,'clock',10.13)
        self.assertAlmostEqual(row['event_perf_s'],110.05);self.assertAlmostEqual(row['network_uncertainty_s'],.01)
        self.assertEqual(row['alignment_status'],'estimated')
        self.assertEqual(map_event(10,sample,'other',10.13)['alignment_status'],'missing_or_foreign_clock')
        self.assertEqual(map_event(10,sample,'clock',20)['alignment_status'],'stale_or_invalid_clock')
        self.assertEqual(align(-1,[sample])['alignment_status'],'not_recorded')
        self.assertEqual(align(50,[sample])['alignment_status'],'uncertain')

    def test_markers_keep_event_time_and_record_failures(self):
        with tempfile.TemporaryDirectory() as d:
            timing=StudyTiming();timing.clock_id='clock';p=ab_packet();p['now']=10.13
            p['abClock']={'id':'1','clockId':'clock','unitySent':10,'unityReceived':10.12,'serverReceived':110.01,'serverSent':110.11}
            calls=[]
            class Lsl:
                @staticmethod
                def local_clock():return 1000
            class Outlet:
                def push_sample(self,values,timestamp):calls.append((values,timestamp))
            timing.lsl=Lsl();timing.outlet=Outlet();timing.marker_error=''
            timing.record({'path':Path(d),'kind':'synthetic'},p,{'type':'input_edge','actual':10.05})
            row=json.loads((Path(d)/'aligned_events.jsonl').read_text())
            self.assertTrue(row['marker_sent']);self.assertAlmostEqual(row['event_perf_s'],110.05)
            self.assertAlmostEqual(calls[0][1],110.05+row['lsl_minus_perf_s'])
            self.assertNotIn('marker_sent',json.loads(calls[0][0][0]))

    def test_local_questionnaire_routes_removed_clock_preserved(self):
        with tempfile.TemporaryDirectory() as d:
            service=StudyService(d,test_state_dir=Path(d)/'state');p=ab_packet('questionnaire_1')
            reply=service.receive(p)
            self.assertNotIn('abQuestionnaireSaved',reply)
            self.assertNotIn('abQuestionnaireStage',reply)
            server=StudyHTTPServer(('127.0.0.1',0),make_handler(service))
            thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start()
            url=f'http://127.0.0.1:{server.server_port}'
            try:
                with urllib.request.urlopen(url+'/clock') as r:clock=json.load(r)
                self.assertGreaterEqual(clock['serverSent'],clock['serverReceived'])
                for path in ['/ab-study','/ab-study/status']:
                    with self.assertRaises(urllib.error.HTTPError) as error:urllib.request.urlopen(url+path)
                    self.assertEqual(error.exception.code,404)
                req=urllib.request.Request(url+'/ab-study/questionnaire',b'{}',{'Content-Type':'application/json'})
                with self.assertRaises(urllib.error.HTTPError) as error:urllib.request.urlopen(req)
                self.assertEqual(error.exception.code,404)
                self.assertEqual(list(Path(d).rglob('questionnaire_*.json')),[])
            finally:server.shutdown();server.server_close();thread.join()

    def test_allocation_uses_published_google_forms(self):
        import subprocess
        import sys
        root=Path(__file__).resolve().parents[2]
        links=json.loads((root/'Tools/LayerAB/questionnaire-links.json').read_text())
        with tempfile.TemporaryDirectory() as d:
            subprocess.run([sys.executable,str(root/'Tools/AdaptiveTraining/allocate_personalized.py'),
                '--layer-ab','--engineering','--participant','SYNTHETIC_FORMS','--index','0',
                '--allocation-seed','20261105','--controller-url','http://127.0.0.1:8766','--output',d],check=True,capture_output=True)
            config=json.loads((Path(d)/'SYNTHETIC_FORMS-adaptive-training-config.json').read_text())
            layer_ab.validate(config)
            self.assertEqual(config['trainingFormUrl'],links['training']['responder'])
            self.assertEqual(config['preferenceFormUrl'],links['preference']['responder'])
            self.assertEqual(config['calibrationFormUrl'],'')
            self.assertEqual(config['questionnaireRevision'],links['revision'])

    def test_export_preserves_original_rt_and_missing_response(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);u=root/'unity'/'sid';s=root/'service'/'sid';u.mkdir(parents=True);s.mkdir(parents=True)
            (u/'adaptive-training-config.json').write_text(json.dumps(ab_packet()['config']))
            clock={'id':'1','clockId':'c','unitySent':10,'unityReceived':10.02,'serverReceived':110.01,'serverSent':110.01,'lslAvailable':True,'lslClock':210.01,'lslSamplePerf':110.01}
            (u/'clock_sync_ab.jsonl').write_text(json.dumps(clock)+'\n')
            (u/'events.jsonl').write_text('\n'.join(json.dumps(e) for e in [
                {'type':'button','actual':10.5,'detail':'match:accepted'},
                {'type':'external_questionnaire_acknowledged','actual':10.6,'detail':'training_1'}])+'\n')
            (u/'trials.csv').write_text('actual_onset_s,response_time_s,input_event_time_s,rt_s\n10.1,10.5,10.48,0.4\n11,-1,-1,-1\n')
            report=export(u,s,root/'out');self.assertEqual(report['unqualified_event_times'],0)
            text=(root/'out'/'trials_aligned.csv').read_text();self.assertIn('0.4',text);self.assertIn('not_recorded',text)
            self.assertEqual((u/'trials.csv').read_text().count('\n'),3)
            self.assertEqual(report['questionnaire_acknowledgements'],1)
            self.assertFalse((root/'out'/'questionnaires.csv').exists())
            import csv
            with (root/'out'/'questionnaire_acknowledgements.csv').open() as f:ack=list(csv.DictReader(f))[0]
            self.assertEqual(ack['submission_verified'],'False');self.assertEqual(ack['condition'],'A')
            self.assertEqual(ack['occasion'],'training_1')


if __name__=='__main__':unittest.main()
