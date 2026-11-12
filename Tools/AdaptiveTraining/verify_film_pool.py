"""Decode all generated media and audit duration, resolution, provenance and source overlap."""
import argparse
from concurrent.futures import ThreadPoolExecutor
import json
from pathlib import Path
import re

from prepare_film_pool import digest, probe, run, write


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--folder',type=Path,required=True)
    parser.add_argument('--evidence',type=Path,required=True)
    parser.add_argument('--clips-only',action='store_true')
    a=parser.parse_args();a.evidence.mkdir(parents=True,exist_ok=True)
    catalog=json.loads((a.folder/'layer-ab-144-media.json').read_text())
    items=[x for x in catalog['items'] if x['split']=='continuous20']
    assert len(items)==90 and len({x['id'] for x in items})==90
    for i,x in enumerate(items):
        for y in items[i+1:]:
            if x['sourceFile']==y['sourceFile']:
                assert abs(x['startSeconds']-y['startSeconds'])>=20
    entries=[dict(id=x['id'],file=x['videoFile'],sha256=x['videoSha256'],duration=20) for x in items]
    if not a.clips_only:
        bank=json.loads((a.folder/'continuous-comparison-media.json').read_text())
        playlists=[x for x in bank['playlists'] if x['id'] in bank['randomizedPlaylistIds']]
        assert len(playlists)==6
        segments=[s for p in playlists for s in p['segments']]
        assert {s['mediaId'] for s in segments}=={x['id'] for x in items}
        assert len(segments)==len({s['sourceVideoHash'] for s in segments})==90
        for p in playlists:
            assert len(p['segments'])==15 and len({s['work'] for s in p['segments']})==9
            for i,s in enumerate(p['segments']):assert s['start']==i*20 and s['duration']==20
        entries.extend(playlists)

    def check(entry):
        path=a.folder/'AdaptiveMedia'/entry['file'];sha=digest(path)
        assert sha==entry['sha256'],path
        cached=a.evidence/(entry['id']+'-quality.json')
        if cached.exists():
            value=json.loads(cached.read_text())
            if value['sha256']==sha:return value
        info=probe(path);v=next(s for s in info['streams'] if s['codec_type']=='video');audio=next(s for s in info['streams'] if s['codec_type']=='audio')
        assert v['width']==1280 and v['height']==720 and v['pix_fmt']=='yuv420p'
        assert v['codec_name']=='h264' and int(v['nb_frames'])==entry['duration']*30 and v['r_frame_rate']=='30/1'
        assert abs(float(info['format']['duration'])-entry['duration'])<.08
        assert abs(float(v['start_time']))<.005 and abs(float(audio['start_time']))<.005
        assert int(audio['sample_rate'])==48000 and audio['channels']==2
        result=run(['ffmpeg','-nostdin','-hide_banner','-nostats','-v','info','-xerror','-threads','2','-i',str(path),
                    '-vf','blackdetect=d=0.35:pix_th=0.08:pic_th=0.99',
                    '-af','ebur128=peak=true:framelog=verbose','-f','null','-'])
        (a.evidence/(entry['id']+'-decode.log')).write_text(result.stderr,encoding='utf-8')
        integrated=float(re.findall(r'I:\s+(-?[\d.]+) LUFS',result.stderr)[-1])
        peak=float(re.findall(r'Peak:\s+(-?[\d.]+) dBFS',result.stderr)[-1])
        black=re.findall(r'black_start:([\d.]+) black_end:([\d.]+) black_duration:([\d.]+)',result.stderr)
        assert peak<=-0.5 and -32<integrated<-17,(path,integrated,peak)
        value=dict(id=entry['id'],sha256=sha,width=v['width'],height=v['height'],frames=int(v['nb_frames']),
                   duration=float(info['format']['duration']),fullDecodePassed=True,integratedLufs=integrated,truePeakDbfs=peak,
                   blackIntervals=black,scope='Software decode and audio measurements; not a worn headset loudness acceptance.')
        write(cached,value);print('Verified '+entry['id'],flush=True);return value
    with ThreadPoolExecutor(max_workers=3) as pool:results=list(pool.map(check,entries))
    for x in items:
        assert digest(a.folder/'AdaptiveMedia'/x['audioFile'])==x['audioSha256']
    report=dict(revision=catalog['version'],clips=len(items),playlists=0 if a.clips_only else 6,
                nonoverlappingSourceWindows=True,disjointPlaylists=not a.clips_only,
                lufsRange=[min(x['integratedLufs'] for x in results),max(x['integratedLufs'] for x in results)],
                maximumTruePeak=max(x['truePeakDbfs'] for x in results),blackIntervals=[(x['id'],x['blackIntervals']) for x in results if x['blackIntervals']],files=results)
    write(a.evidence/('clips-verification.json' if a.clips_only else 'verification.json'),report)
    print('Verified '+str(len(results))+' complete videos; black interval flags: '+str(report['blackIntervals']),flush=True)


if __name__=='__main__':main()
