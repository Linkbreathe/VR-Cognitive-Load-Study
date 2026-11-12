"""Build reviewed, nonoverlapping film excerpts and six disjoint continuous playlists."""
import argparse
from collections import Counter, defaultdict
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
from pathlib import Path
import random
import re
import subprocess


def digest(path):
    h = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def run(args):
    return subprocess.run(args, check=True, capture_output=True, text=True, encoding='utf-8', errors='replace')


def probe(path):
    return json.loads(run(['ffprobe', '-v', 'error', '-show_streams', '-show_format', '-of', 'json', str(path)]).stdout)


def write(path, value):
    Path(path).write_text(json.dumps(value, indent=2, ensure_ascii=True), encoding='utf-8')


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--recipe', type=Path, required=True)
    p.add_argument('--sources', type=Path, required=True)
    p.add_argument('--base-catalog', type=Path, required=True)
    p.add_argument('--legacy-bank', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--resume', action='store_true', help='Reuse outputs only when their source recipe and hashes still match')
    a = p.parse_args()
    recipe = json.loads(a.recipe.read_text(encoding='utf-8-sig'))
    media = a.output / 'AdaptiveMedia'
    media.mkdir(parents=True, exist_ok=True)
    prior_catalog=a.output/'layer-ab-144-media.json'
    prior_items={x['id']:x for x in json.loads(prior_catalog.read_text())['items']} if a.resume and prior_catalog.exists() else {}
    prior_bank=a.output/'continuous-comparison-media.json'
    prior_playlists={x['id']:x for x in json.loads(prior_bank.read_text())['playlists']} if a.resume and prior_bank.exists() else {}
    checked, jobs = [], []
    for source in recipe['sources']:
        path = a.sources / (source['id'] + '.mp4')
        meta = json.loads(path.with_suffix('.info.json').read_text(encoding='utf-8'))
        info = probe(path)
        video = next(s for s in info['streams'] if s['codec_type'] == 'video')
        if video['height'] < 720 or video['width'] < 1280 or not any(s['codec_type'] == 'audio' for s in info['streams']):
            raise ValueError('Source fails native HD/audio gate: ' + source['id'])
        if any(word in meta['title'].lower() for word in ('trailer', 'teaser', 'tv spot', 'featurette')):
            raise ValueError('Promotional format excluded: ' + source['id'])
        starts = source['starts']
        ordered=sorted(starts)
        if any(y-x < 20 for x,y in zip(ordered,ordered[1:])) or ordered[0] < 0 or ordered[-1]+20 > float(info['format']['duration'])-5:
            raise ValueError('Overlapping or out-of-range excerpts: ' + source['id'])
        row = dict(source, title=meta['title'], channel=meta['channel'], channelId=meta['channel_id'],
                   sourceSha256=digest(path), width=video['width'], height=video['height'],
                   sourceDuration=float(info['format']['duration']))
        checked.append(row)
        for start in starts:
            jobs.append((row, start, len(jobs)+1))
    if len(jobs) != 90:
        raise ValueError('This revision requires ninety unique twenty-second excerpts.')
    write(a.output/'source-provenance.json', checked)

    def encode(job):
        source, start, index = job
        stem = f'film20-v1-{index:03}'
        video, audio = media/(stem+'.mp4'), media/(stem+'.wav')
        prior=prior_items.get(stem)
        reuse=prior and prior['sourceSha256']==source['sourceSha256'] and prior['startSeconds']==start and video.exists() and audio.exists() and digest(video)==prior['videoSha256'] and digest(audio)==prior['audioSha256']
        if reuse and prior.get('loudnessRevision')=='measured-ebu-trim-v1':
            return prior
        args = ['ffmpeg','-nostdin','-v','info','-y','-ss',str(start),'-i',str(a.sources/(source['id']+'.mp4')),
                '-t','20','-map','0:v:0','-map','0:a:0',
                '-vf','scale=1280:720:force_original_aspect_ratio=decrease:force_divisible_by=2,pad=1280:720:(ow-iw)/2:(oh-ih)/2,setsar=1,fps=30:start_time=0',
                '-c:v','libx264','-profile:v','baseline','-preset','fast','-crf','20','-threads','2','-pix_fmt','yuv420p',
                '-af','loudnorm=I=-23:TP=-3:LRA=7:print_format=json,aresample=48000,afade=t=in:d=0.005,afade=t=out:st=19.995:d=0.005',
                '-c:a','aac','-b:a','160k','-ar','48000','-ac','2','-movflags','+faststart',str(video)]
        if not reuse:
            log = run(args).stderr
            (a.output/(stem+'-encode.log')).write_text(log,encoding='utf-8')
        measurement=run(['ffmpeg','-nostdin','-hide_banner','-nostats','-v','info','-i',str(video),'-vn',
                         '-af','ebur128=peak=true:framelog=verbose','-f','null','-']).stderr
        integrated=float(re.findall(r'I:\s+(-?[\d.]+) LUFS',measurement)[-1])
        peak=float(re.findall(r'Peak:\s+(-?[\d.]+) dBFS',measurement)[-1])
        adjustment=min(-23-integrated,-3.2-peak)
        temporary=media/(stem+'-audio-adjusted.mp4')
        run(['ffmpeg','-nostdin','-v','error','-y','-i',str(video),'-map','0:v:0','-map','0:a:0','-c:v','copy',
             '-af','volume='+str(adjustment)+'dB','-c:a','aac','-b:a','160k','-ar','48000','-ac','2','-movflags','+faststart',str(temporary)])
        temporary.replace(video)
        run(['ffmpeg','-nostdin','-v','error','-y','-i',str(video),'-vn','-c:a','pcm_s16le',str(audio)])
        info=probe(video)
        v=next(s for s in info['streams'] if s['codec_type']=='video')
        if abs(float(info['format']['duration'])-20)>.08 or v['width']!=1280 or v['height']!=720 or int(v['nb_frames'])!=600:
            raise ValueError('Encode contract failed: '+stem)
        print('Encoded '+stem,flush=True)
        return dict(id=stem,work=source['work'],source=source['url'],sourceFile=source['id']+'.mp4',
                    sourceTitle=source['title'],sourceChannel=source['channel'],sourceSha256=source['sourceSha256'],
                    startSeconds=start,duration=20,category='film scene',split='continuous20',ready=True,
                    volumeReviewed=False,synthetic=False,gain=1,
                    loudnessRevision='measured-ebu-trim-v1',measuredBeforeTrimLufs=integrated,finalAudioTrimDb=round(adjustment,3),
                    rightsRecord='Public studio or movie-clip channel upload; locally prepared at researcher request. No redistribution grant asserted.',
                    notes='Native HD source; no trailer, external advertisement, added music or added narration. Original soundtrack and source channel marks retained. Headset review pending.',
                    videoFile=video.name,audioFile=audio.name,videoSha256=digest(video),audioSha256=digest(audio))

    with ThreadPoolExecutor(max_workers=3) as pool:
        items=list(pool.map(encode,jobs))
    catalog=json.loads(a.base_catalog.read_text(encoding='utf-8-sig'))
    if any(i['id'].startswith('film20-v1-') for i in catalog['items']):
        raise ValueError('Use the original base catalog, not an already expanded catalog.')
    catalog['items'].extend(items)
    catalog['version']='film-scenes-hd-v1-20261112'
    catalog['trainingClipCount']=sum(i['split']=='training20' for i in catalog['items'])
    catalog['continuousClipCount']=sum(i['split']=='continuous20' for i in catalog['items'])
    write(a.output/'layer-ab-144-media.json',catalog)
    pools=defaultdict(list)
    for item in items:pools[item['work']].append(item)
    rng=random.Random(recipe['assemblySeed'])
    works=sorted(pools)
    rng.shuffle(works)
    for pool in pools.values():rng.shuffle(pool)
    sequence=[pools[work][i] for i in range(10) for work in works]
    bank=json.loads(a.legacy_bank.read_text(encoding='utf-8-sig'))
    if len(bank['playlists'])!=2:raise ValueError('Two original legacy playlists required.')
    bank.update(catalogVersion=catalog['version'],selectionRevision='participant-film-pool-v1',
                assemblySeed=recipe['assemblySeed'],randomizedPlaylistIds=[])
    for i in range(6):
        selected=sequence[i*15:(i+1)*15]
        counts=Counter(x['work'] for x in selected)
        if len(counts)!=9 or max(counts.values())>2:raise ValueError('Unbalanced film mixture')
        stem=f'film-mix-v1-{i+1:02}'
        concat=a.output/(stem+'.ffconcat')
        concat.write_text('ffconcat version 1.0\n'+''.join("file '"+str((media/x['videoFile']).resolve()).replace('\\','/').replace("'","'\\''")+"'\nduration 20\n" for x in selected),encoding='utf-8')
        output=media/(stem+'.mp4')
        segments=[dict(mediaId=x['id'],start=j*20,duration=20,sourceVideoHash=x['videoSha256'],
                       work=x['work'],source=x['source'],sourceStartSeconds=x['startSeconds']) for j,x in enumerate(selected)]
        prior=prior_playlists.get(stem)
        if not (prior and prior['segments']==segments and output.exists() and digest(output)==prior['sha256']):
            run(['ffmpeg','-nostdin','-v','error','-y','-f','concat','-safe','0','-i',str(concat),'-t','300',
             '-af','aresample=48000:async=1:first_pts=0','-c:v','copy',
             '-c:a','aac','-b:a','160k','-ar','48000','-ac','2','-movflags','+faststart',str(output)])
        info=probe(output)
        if abs(float(next(s for s in info['streams'] if s['codec_type']=='video')['start_time']))>.001:
            # AAC priming in the concat demuxer must not shift the video timeline by 21 ms.
            aligned=a.output/(stem+'-aligned.mp4')
            run(['ffmpeg','-nostdin','-v','error','-y','-i',str(output),'-map','0','-c','copy',
                 '-bsf:v','setts=pts=PTS-STARTPTS:dts=DTS-STARTDTS','-movflags','+faststart',str(aligned)])
            aligned.replace(output);info=probe(output)
        if abs(float(info['format']['duration'])-300)>.08 or int(next(s for s in info['streams'] if s['codec_type']=='video')['nb_frames'])!=9000:
            raise ValueError('Continuous duration/frame count failed')
        bank['playlists'].append(dict(id=stem,file=output.name,sha256=digest(output),duration=300,segments=segments))
        bank['randomizedPlaylistIds'].append(stem)
        print('Assembled '+stem,flush=True)
    write(a.output/'continuous-comparison-media.json',bank)
    print('Complete: 90 unique excerpts, six disjoint five-minute playlists.',flush=True)


if __name__=='__main__':main()
