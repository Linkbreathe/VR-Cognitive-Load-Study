"""Synthetic contracts for time alignment and honest spatial evidence."""
import json
from pathlib import Path
import tempfile
import unittest

from decision_gaze import density, read_projection, attach_gaze

LAYOUT = dict(available=True, minX=-1, maxX=1, minY=-1, maxY=1)


def point(t, **kwargs):
    return dict(dict(id=int(t*100), time=t, availableAt=t, stage='layer_b', frame=1,
                     valid=True, projected=True, x=0., y=0., layout=LAYOUT), **kwargs)


class DecisionGazeTests(unittest.TestCase):
    def test_all_valid_points_outside_original_view_are_plotted_without_changing_source(self):
        rows=[point(0,x=1.5,y=-3),point(.05,x=1.6,y=-3.2),point(.1)]
        original=json.dumps(rows,sort_keys=True)
        d=density(rows,[[0,.1]],'layer_b')
        self.assertEqual(d['status'],'available')
        f=d['frames'][0]
        self.assertTrue(f['expanded'])
        self.assertAlmostEqual(f['mappedSeconds'],.1)
        self.assertAlmostEqual(f['outsideSeconds'],.1)
        self.assertLess(f['layout']['minY'],-3.2)
        self.assertEqual(f['referenceLayout'],LAYOUT)
        self.assertAlmostEqual(sum(b[2] for b in f['bins']),.1)
        self.assertEqual(json.dumps(rows,sort_keys=True),original)

    def test_late_or_invalid_outliers_do_not_expand_decision_view(self):
        rows=[point(0),point(.05,x=20,availableAt=5),point(.1,y=-20,valid=False),point(.15)]
        f=density(rows,[[0,.15]],'layer_b',cutoff=1)['frames'][0]
        self.assertFalse(f['expanded'])
        self.assertEqual(f['layout'],LAYOUT)

    def test_duration_clipping_gaps_and_late_arrival(self):
        rows=[point(0), point(.05), point(1, availableAt=4), point(1.1)]
        d=density(rows, [[.025,1.1]], 'layer_b', cutoff=2)
        self.assertAlmostEqual(d['frames'][0]['mappedSeconds'], .125)
        self.assertAlmostEqual(d['eligibleSeconds'], 1.075)
        self.assertEqual(density(rows, [[0,2]], 'layer_a')['frames'], [])

    def test_frames_outside_invalid_and_simulated(self):
        rows=[point(0),point(.05,frame=2,x=2),point(.1,valid=False),point(.15,projected=False),point(.2)]
        d=density(rows, [[0,.2]], 'layer_b')
        self.assertEqual(len(d['frames']),2)
        self.assertAlmostEqual(d['frames'][0]['mappedSeconds'], .05)
        self.assertAlmostEqual(d['frames'][1]['outsideSeconds'], .05)
        self.assertEqual(density(rows,[[0,.2]],'layer_b',simulated=True)['frames'],[])

    def test_dedup_partial_tail_and_first_arrival(self):
        with tempfile.TemporaryDirectory() as tmp:
            path=Path(tmp)/'telemetry.jsonl'
            def packet(now):
                return json.dumps(dict(packet=dict(stage='layer_b', now=now,gazeOverview=dict(
                    source='tracked_eyes',revision='gaze-console-front-plane-v1',frame=1,layout=LAYOUT,
                    samples=[point(0), point(.05)]))))
            path.write_text(packet(1)+'\n'+packet(2),encoding='utf-8')
            self.assertEqual(len(read_projection(path)),2)
            with path.open('a') as f:f.write('\n')
            rows=read_projection(path)
            self.assertEqual(len(rows),2)
            self.assertEqual(rows[0]['availableAt'],1)

    def test_recorded_intervals_and_follow_up_are_separate(self):
        def trial(n):
            return dict(id=str(n),stage='layer_b',trial=n,start=n*5.,deadline=n*5.+4,scored=True,
                        preResponseSeconds=2,preResponse=[[n*5.,n*5.+2,'task']],afterResponse=[[n*5.+2,n*5.+4,'movie']])
        checkpoints=[dict(stage='layer_b',request='r1',trial=1,time=9.,review=dict(windowTrials=[0],gazeIntervals=[[5.5,6.5]])),
                     dict(stage='layer_b',request='r2',trial=3,time=19.,review=dict(windowTrials=[1,2]))]
        with tempfile.TemporaryDirectory() as tmp:
            attach_gaze(Path(tmp),checkpoints,[trial(i) for i in range(1,5)])
        evidence=checkpoints[0]['gazeEvidence']
        self.assertEqual(evidence['before']['intervals'],[[5.5,6.5]])
        self.assertEqual(evidence['before']['regionSeconds']['task'],1.)
        self.assertEqual(evidence['after']['trials'],['2','3'])
        self.assertEqual(evidence['after']['nextRequest'],'r2')


if __name__ == '__main__':
    unittest.main()
