"""Read-only, time-weighted gaze projections for decision evidence.

Controller values remain authoritative. This reconstructs spatial context from
tracked-eye telemetry; it never supplies authorization or infers missing eyes.
"""
from collections import OrderedDict
from bisect import bisect_left
import json
import math
from threading import RLock

REVISION = "decision-gaze-density-v2-expanded-view"
_cache = OrderedDict()
_lock = RLock()


def finite(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def read_projection(path):
    """Incremental JSONL read; deduplicate overlapping Unity rolling packets."""
    with _lock:
        if not path.exists():
            return []
        stat = path.stat()
        key = str(path.resolve())
        item = _cache.get(key)
        if not item or stat.st_size < item['offset'] or (stat.st_size == item['offset'] and stat.st_mtime_ns != item['mtime']):
            item = dict(offset=0, mtime=0, points={})
        with path.open('rb') as stream:
            stream.seek(item['offset'])
            while True:
                line = stream.readline()
                if not line or not line.endswith(b'\n'):
                    break
                item['offset'] = stream.tell()
                try:
                    packet = json.loads(line).get('packet', {})
                except (ValueError, UnicodeError):
                    continue
                view = packet.get('gazeOverview') or {}
                if view.get('source') != 'tracked_eyes' or view.get('revision') != 'gaze-console-front-plane-v1':
                    continue
                layout = view.get('layout') or {}
                if not layout.get('available') or not all(finite(layout.get(k)) for k in ('minX', 'maxX', 'minY', 'maxY')):
                    continue
                if layout['maxX'] <= layout['minX'] or layout['maxY'] <= layout['minY']:
                    continue
                for point in view.get('samples', []):
                    if not finite(point.get('time')) or point.get('id') is None:
                        continue
                    identity = (packet.get('stage'), view.get('frame'), point['id'])
                    # The first arrival is retained, including its layout and availability time.
                    item['points'].setdefault(identity, dict(point, stage=packet.get('stage'), frame=view.get('frame'),
                        layout=layout, availableAt=packet.get('now', view.get('now'))))
        item['mtime'] = stat.st_mtime_ns
        _cache[key] = item
        _cache.move_to_end(key)
        while len(_cache) > 4:
            _cache.popitem(last=False)
        return sorted(item['points'].values(), key=lambda p: p['time'])


def merge_intervals(intervals):
    result = []
    for a, b in sorted((a, b) for a, b in intervals if finite(a) and finite(b) and b > a):
        if result and a <= result[-1][1]:
            result[-1][1] = max(b, result[-1][1])
        else:
            result.append([a, b])
    return result


def density(points, intervals, stage, cutoff=None, simulated=False):
    intervals = merge_intervals(intervals)
    total = sum(b-a for a, b in intervals)
    result = dict(revision=REVISION, eligibleSeconds=total, frames=[], source='tracked_eyes',
                  status='no_recorded_positions', intervals=intervals,
                  method='Elapsed-time bins; each sample holds for at most 100 ms; gaps are not filled.')
    if simulated:
        result.update(status='simulated_aggregates_no_positions', source='simulated')
        return result
    if not intervals:
        return result
    points = [p for p in points if p['stage'] == stage]
    times = [p['time'] for p in points]
    frames = {}
    for a, b in intervals:
        for i in range(bisect_left(times, a-.1), bisect_left(times, b)):
            p = points[i]
            end = min(p['time']+.1, points[i+1]['time'] if i+1 < len(points) else p['time'], b)
            duration = end-max(a, p['time'])
            if duration <= 0 or not p.get('valid') or not p.get('projected'):
                continue
            if cutoff is not None and (not finite(p.get('availableAt')) or p['availableAt'] > cutoff):
                continue
            if not finite(p.get('x')) or not finite(p.get('y')):
                continue
            frame = frames.setdefault(p['frame'], dict(frame=p['frame'], layout=dict(p['layout']), referenceLayout=p['layout'], positions=[], bins={},
                mappedSeconds=0., outsideSeconds=0., sampleCount=0, firstTime=p['time'], lastTime=p['time']))
            frame['lastTime'] = p['time']
            frame['sampleCount'] += 1
            layout = frame['referenceLayout']
            x = (p['x']-layout['minX'])/(layout['maxX']-layout['minX'])
            y = (layout['maxY']-p['y'])/(layout['maxY']-layout['minY'])
            if not (0 <= x <= 1 and 0 <= y <= 1):
                frame['outsideSeconds'] += duration
                view=frame['layout']
                view['minX']=min(view['minX'],p['x']-.1);view['maxX']=max(view['maxX'],p['x']+.1)
                view['minY']=min(view['minY'],p['y']-.1);view['maxY']=max(view['maxY'],p['y']+.1)
            frame['positions'].append((p['x'],p['y'],duration))
            frame['mappedSeconds'] += duration
    for frame in frames.values():
        layout=frame['layout']
        for px,py,duration in frame.pop('positions'):
            x=(px-layout['minX'])/(layout['maxX']-layout['minX'])
            y=(layout['maxY']-py)/(layout['maxY']-layout['minY'])
            cell=(min(47,max(0,int(x*48))),min(31,max(0,int(y*32))))
            frame['bins'][cell]=frame['bins'].get(cell,0.)+duration
        frame['expanded']=frame['outsideSeconds']>0
        frame['bins'] = [[x, y, seconds] for (x, y), seconds in sorted(frame['bins'].items())]
    result['frames'] = list(frames.values())
    if any(f['bins'] for f in result['frames']):
        result['status'] = 'available'
    return result


def attach_gaze(folder, checkpoints, timeline, simulated=False):
    points = [] if simulated else read_projection(folder/'telemetry.jsonl')
    for c in checkpoints:
        review = c['review']
        ids = review.get('verificationWindowTrials', review.get('windowTrials', []))
        before = [t for t in timeline if t['stage'] == c['stage'] and t['trial']-1 in ids and t['deadline'] <= c['time']]
        following = sorted((x for x in checkpoints if x['stage'] == c['stage'] and x['time'] > c['time']), key=lambda x:x['time'])
        end_trial = following[0]['trial'] if following else c['trial']+2
        after = [t for t in timeline if t['stage'] == c['stage'] and c['trial'] < t['trial'] <= end_trial and t['start'] >= c['time']]
        for period, rows in [('before', before), ('after', after)]:
            intervals = [[t['start'], t['start']+t['preResponseSeconds']] for t in rows if t['scored']]
            basis = 'Pre-response intervals · contextual observation'
            if period == 'before' and 'gazeIntervals' in review:
                intervals = review['gazeIntervals']
                basis = 'Recorded playback / pre-response review intervals'
            elif period == 'before' and 'gaze' in review:
                intervals = [[t['start'], t['deadline']] for t in rows]
                basis = 'Recorded opening review · complete trial intervals'
            spatial = density(points, intervals, c['stage'], c['time'] if period == 'before' else None, simulated)
            durations = dict(task=0., movie=0., other=0., missing=0.)
            for t in rows:
                for x, y, target in t['preResponse']+t['afterResponse']:
                    for a, b in spatial['intervals']:
                        durations[target if target in durations else 'other'] += max(0., min(y,b)-max(x,a))
            durations['missing'] += max(0., spatial['eligibleSeconds']-sum(durations.values()))
            spatial['regionSeconds'] = durations
            spatial.update(basis=basis, trials=[t['id'] for t in rows], nextRequest=following[0]['request'] if following and period=='after' else None)
            c.setdefault('gazeEvidence', {})[period] = spatial
