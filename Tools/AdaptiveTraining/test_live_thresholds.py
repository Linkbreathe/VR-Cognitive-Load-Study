"""Synthetic checks for researcher edits, decision snapshots and local HTTP access."""
import copy
import json
from pathlib import Path
from tempfile import TemporaryDirectory
import threading
import unittest
from unittest.mock import patch
from urllib.request import Request, urlopen
from urllib.error import HTTPError

from personalized_runtime import PersonalSession
from service import StudyService, StudyHTTPServer, make_handler
from test_compact_comparison import fixture
from test_layer_ab import ab_packet
from test_session_store import TestSessionStore, REVISION, read
import workload_rf


def record(store, folder, sid='THRESHOLD_FIXTURE'):
    r = dict(sessionId=sid, state='running', folder=str(folder), config={},
             scoreThresholds=dict(on=.49, off=.57), sourceModel=dict(featureRevision=workload_rf.REVISION))
    store.records[sid] = r
    return r


class LiveThresholdTests(unittest.TestCase):
    def test_store_history_validation_and_completed_sessions(self):
        with TemporaryDirectory() as tmp:
            root=Path(tmp);store=TestSessionStore(root/'state');r=record(store,root)
            self.assertTrue(store.status()['thresholdEditing'])
            req=dict(sessionId=r['sessionId'], expectedRevision=0, scoreThresholds=dict(on=.55,off=.65))
            updated=store.update_thresholds(req,dict(stage='layer_b',completedTrials=20))
            self.assertEqual(updated['initialScoreThresholds'],dict(on=.49,off=.57))
            self.assertEqual(updated['thresholdHistory'][0]['context']['stage'],'layer_b')
            self.assertEqual(read(root/'test-session.json'),updated)
            self.assertEqual(read(store.index_path)[r['sessionId']],updated)
            with self.assertRaisesRegex(ValueError,'another window'):store.update_thresholds(req)
            with self.assertRaisesRegex(ValueError,'ON lower'):store.update_thresholds(dict(req,expectedRevision=1,scoreThresholds=dict(on=.8,off=.4)))
            self.assertEqual(store.records[r['sessionId']]['thresholdRevision'],1)
            store.complete(r['sessionId'],'complete')
            with self.assertRaisesRegex(ValueError,'active assignment'):store.update_thresholds(dict(req,expectedRevision=1))

    def test_new_decisions_change_both_rounds_retries_keep_original(self):
        for stage in ('layer_a','layer_b'):
            with self.subTest(stage=stage), TemporaryDirectory() as tmp:
                root=Path(tmp);sample,p,_,_=fixture(score=.60)
                packet=ab_packet(stage)
                packet['config'].update(sample.config,testSessionMode='full')
                arm=PersonalSession(packet,root/'service',None)
                store=TestSessionStore(root/'state');record(store,root,arm.sid)
                arm.test_store=store;arm.workload_control=True;arm.candidate_control=True
                arm.report=sample.report;arm.predictions=sample.predictions;arm.barrier=0
                for value in arm.predictions:value['stage']=stage
                arm.trials={(stage,i):dict(v,trainingStage=stage) for (_,i),v in sample.trials.items()}
                p=dict(packet,**{k:v for k,v in p.items() if k!='stage'},stage=stage,events=[],trials=[],
                       lastTrial=19,evaluationId=1,decisionDeadline=90)
                p['closedBehavior']=dict(p['closedBehavior'],trainingStage=stage)
                with patch.object(arm,'process_windows'):
                    before=copy.deepcopy(arm.receive(p,True)['decision'])
                    self.assertEqual(before['action'],'OFF')
                    store.update_thresholds(dict(sessionId=arm.sid,expectedRevision=0,scoreThresholds=dict(on=.55,off=.65)))
                    self.assertEqual(arm.receive(p,True)['decision'],before)
                    after=arm.receive(dict(p,requestId='next-fixture',evaluationId=2),True)['decision']
                self.assertEqual((after['action'],after['highThreshold'],after['thresholdRevision']),('HOLD',.65,1))
                self.assertEqual((before['highThreshold'],before['thresholdRevision']),(.57,0))
                saved=[json.loads(line)['decision'] for line in (root/'service/personal_decisions.jsonl').read_text().splitlines()]
                self.assertEqual([x['highThreshold'] for x in saved],[.57,.65])
                from comparison_report import report
                exported=report(root/'service')
                self.assertEqual(exported['initialScoreThresholds'],dict(on=.49,off=.57))
                self.assertEqual(exported['thresholdHistory'][0]['current'],dict(on=.55,off=.65))

    def test_http_requires_local_console_and_returns_revision(self):
        with TemporaryDirectory() as tmp:
            root=Path(tmp);service=StudyService(root/'service');r=record(service.test_store,root)
            server=StudyHTTPServer(('127.0.0.1',0),make_handler(service))
            worker=threading.Thread(target=server.serve_forever,daemon=True);worker.start()
            url=f'http://127.0.0.1:{server.server_port}/tests/thresholds'
            data=json.dumps(dict(sessionId=r['sessionId'],expectedRevision=0,scoreThresholds=dict(on=.55,off=.65))).encode()
            try:
                with self.assertRaises(HTTPError) as failure:urlopen(Request(url,data=data))
                self.assertEqual(failure.exception.code,403)
                with urlopen(Request(url,data=data,headers={'X-Test-Console':REVISION})) as response:
                    self.assertEqual(json.load(response)['thresholdRevision'],1)
            finally:server.shutdown();server.server_close();worker.join()


if __name__=='__main__':unittest.main()
