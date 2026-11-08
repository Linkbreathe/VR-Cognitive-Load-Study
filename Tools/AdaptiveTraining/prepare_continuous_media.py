"""Join verified source clips into two deterministic 300-second local playlists."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--source', type=Path, required=True, help='Folder containing layer-ab-144-media.json and AdaptiveMedia')
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--ffmpeg', required=True)
    p.add_argument('--deploy', type=Path)
    p.add_argument('--previous-bank', type=Path, help='Preserve existing slots; replace removed clips with unused catalog clips')
    args = p.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    catalog = json.loads((args.source/'layer-ab-144-media.json').read_text())
    items = sorted((x for x in catalog['items'] if x['split']=='training20'), key=lambda x:x['id'])
    bank = dict(revision='continuous-clips-resume-v1', catalogVersion=catalog.get('version'), playlists=[])
    selected_items = items[:30]
    if args.previous_bank:
        previous = json.loads(args.previous_bank.read_text())
        if len(previous['playlists']) != 2 or any(len(x['segments']) != 15 for x in previous['playlists']):
            raise ValueError('Previous bank must have two fifteen-clip playlists')
        by_id = {x['id']: x for x in items}
        slots = [s for playlist in previous['playlists'] for s in playlist['segments']]
        retained = {s['mediaId'] for s in slots if s['mediaId'] in by_id}
        replacements = iter(x for x in items if x['id'] not in retained)
        selected_items = []
        for slot in slots:
            item = by_id.get(slot['mediaId'])
            if item is not None and item['videoSha256'] != slot['sourceVideoHash']:
                raise ValueError('Retained source content changed: ' + item['id'])
            if item is None:
                item = next(replacements, None)
                if item is None: raise ValueError('Not enough unused replacement clips')
            selected_items.append(item)
    for index in range(2):
        selected = selected_items[index*15:(index+1)*15]
        if len(selected)!=15: raise ValueError('Thirty source clips are required')
        paths, segments = [], []
        for i, item in enumerate(selected):
            source = args.source/'AdaptiveMedia'/Path(item['videoFile']).name
            if digest(source)!=item['videoSha256'] or item['duration']!=20 or item['gain']!=1:
                raise ValueError('Changed source or unsupported clip duration/gain')
            paths.append(source.resolve())
            segments.append(dict(mediaId=item['id'],start=i*20,duration=20,sourceVideoHash=item['videoSha256']))
        stem = f'continuous-comparison-{index}'
        concat = args.output/(stem+'.ffconcat')
        concat.write_text('ffconcat version 1.0\n'+''.join("file '"+str(x).replace('\\','/').replace("'", "'\\''")+"'\nduration 20\n" for x in paths))
        output = args.output/(stem+'.mp4')
        # Encode one continuous timeline, including continuous audio timestamps at every cut.
        subprocess.run([args.ffmpeg,'-nostdin','-v','error','-y','-f','concat','-safe','0','-i',str(concat),
                        '-t','300','-vf','fps=30,scale=1280:720,setsar=1','-af','aresample=48000:async=1:first_pts=0',
                        '-c:v','libx264','-preset','veryfast','-crf','21','-pix_fmt','yuv420p','-threads','4',
                        '-c:a','aac','-b:a','160k','-ac','2','-movflags','+faststart',str(output)],check=True)
        bank['playlists'].append(dict(id=stem,file=output.name,sha256=digest(output),duration=300,segments=segments))
        print('Encoded',output,flush=True)
    name='continuous-comparison-media.json'
    (args.output/name).write_text(json.dumps(bank,indent=2))
    if args.deploy:
        (args.deploy/'AdaptiveMedia').mkdir(exist_ok=True)
        for entry in bank['playlists']: shutil.copy2(args.output/entry['file'],args.deploy/'AdaptiveMedia'/entry['file'])
        shutil.copy2(args.output/name,args.deploy/name)
    print('Prepared continuous bank',flush=True)


if __name__=='__main__': main()
