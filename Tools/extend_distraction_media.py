"""Prepare ten-second video and licensed spatial sound assets; retain source provenance."""
import concurrent.futures
import hashlib
import json
from pathlib import Path
from bs4 import BeautifulSoup
from prepare_distraction_media import ROOT, SOURCES, STREETS, get, probe, convert

def environment(item):
    category, ident, family = item
    page=f'https://mixkit.co/free-sound-effects/{category}/'
    soup=BeautifulSoup((SOURCES/f'mixkit-{category}.html').read_text(encoding='utf8'),'html.parser')
    node=soup.select_one(f'[data-audio-player-item-id-value="{ident}"]')
    url=node['data-audio-player-preview-url-value']
    source=get(url,SOURCES/f'{family}-source-{ident}.mp3')
    target=ROOT/f'Assets/TaskPlatform/Media/Environment/{family}-{ident}.wav'
    target.parent.mkdir(parents=True,exist_ok=True)
    duration=min(8,float(probe(source)['format']['duration']))
    filters=f'atrim=duration={duration},highpass=f=60,loudnorm=I=-23:TP=-3:LRA=7,afade=t=in:d=0.03,afade=t=out:st={max(0,duration-.1)}:d=0.1'
    convert(['-i',str(source),'-af',filters,'-ac','1','-ar','48000','-c:a','pcm_s16le',str(target)])
    return dict(id=target.stem,kind='environment',family=family,title=node.parent.get_text(' ',strip=True).split('Download')[0],sourcePage=page,downloadUrl=url,license='Mixkit Sound Effects Free License',file=target.relative_to(ROOT).as_posix(),duration=float(probe(target)['format']['duration']),sha256=hashlib.sha256(target.read_bytes()).hexdigest())

def street(record):
    ident=record['id'].split('-')[1];source=SOURCES/f'street-source-{ident}.mp4';target=ROOT/record['file']
    # Sources shorter than ten seconds loop locally; a short tail prevents decoder EOF from shortening exposure.
    convert(['-stream_loop','-1','-i',str(source),'-t','10.25','-an','-vf','scale=1280:720:force_original_aspect_ratio=increase,crop=1280:720,fps=24','-c:v','libx264','-preset','fast','-crf','24','-profile:v','baseline','-pix_fmt','yuv420p','-movflags','+faststart',str(target)])
    record.update(duration=float(probe(target)['format']['duration']),exposureSeconds=10,sourceDuration=float(probe(source)['format']['duration']),preparation='Loop source only if needed; encode 10.25 s including a 0.25 s decoder tail; display exactly 10 s after first frame',sha256=hashlib.sha256(target.read_bytes()).hexdigest())
    return record

if __name__=='__main__':
    path=ROOT/'Assets/TaskPlatform/Media/distraction-media-manifest.json'
    manifest=json.loads(path.read_text(encoding='utf-8-sig'))
    print(list(manifest),flush=True)
    records=manifest['assets']
    inputs=[('car',x,'traffic') for x in ['1538','1554','1536','1553']]+[('footsteps',x,'footsteps') for x in ['542','543','535','538']]
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        streets=list(pool.map(street,[x for x in records if x['kind']=='street']))
        sounds=list(pool.map(environment,inputs))
    manifest['assets']=[x for x in records if x['kind'] not in ['street','environment','news','softvoice']]+streets+sounds
    manifest['revision']='local-media-v1.7-2026-11-01'
    manifest['version']='local-media-v1.7-2026-11-01'
    path.write_text(json.dumps(manifest,indent=2),encoding='utf8')
    print('Prepared eight 10.25-second videos and eight spatial sounds.',flush=True)
