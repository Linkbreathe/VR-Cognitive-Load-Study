"""Download licensed local media and prepare small, silent Quest video clips."""
import concurrent.futures
import hashlib
import json
import re
import subprocess
from pathlib import Path
import requests
from bs4 import BeautifulSoup

ROOT = Path(__file__).resolve().parents[1]
SOURCES = ROOT / 'MediaSources'
STREETS = ROOT / 'Assets/StreamingAssets/Distractions/Streets'
DOORS = ROOT / 'Assets/TaskPlatform/Media/Doors'
VIDEOS = [
    ('4348', 'a-calm-street-in-paris'),
    ('4401', 'crowds-of-people-cross-a-street-junction'),
    ('4231', 'pedestrian-walk-in-tokyo'),
    ('4451', 'quiet-tokyo-street-at-night'),
    ('3429', 'city-street-with-trolleybus-at-night'),
    ('3428', 'street-with-people-walking-at-dusk'),
    ('4600', 'narrow-and-old-alley-in-venice'),
    ('4332', 'times-square-during-a-rainy-night'),
]
DOOR_IDS = ['195', '189', '194', '196', '192', '1908', '182', '203']

def get(url, path):
    if not path.exists():
        response = requests.get(url, timeout=90)
        response.raise_for_status()
        path.write_bytes(response.content)
    return path

def probe(path):
    return json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_format', '-show_streams', '-of', 'json', str(path)]))

def convert(args):
    subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', *args], check=True)

def video(item):
    ident, slug = item
    page = f'https://mixkit.co/free-stock-video/{slug}-{ident}/'
    html = get(page, SOURCES / f'video-{ident}.html').read_text(encoding='utf8')
    soup = BeautifulSoup(html, 'html.parser')
    entries = [json.loads(x.string) for x in soup.select('script[type="application/ld+json"]') if x.string]
    def objects(value):
        if isinstance(value, dict):
            yield value
            for child in value.values(): yield from objects(child)
        elif isinstance(value, list):
            for child in value: yield from objects(child)
    info = next(x for entry in entries for x in objects(entry) if x.get('@type') == 'VideoObject')
    if 'videoFree' not in info.get('license', ''):
        raise ValueError(f'Non-free license: {page}')
    url = info['contentUrl']
    source = get(url, SOURCES / f'street-source-{ident}.mp4')
    target = STREETS / f'street-{ident}.mp4'
    convert(['-i', str(source), '-t', '8', '-an', '-vf', 'scale=1280:720:force_original_aspect_ratio=increase,crop=1280:720,fps=24', '-c:v', 'libx264', '-preset', 'fast', '-crf', '24', '-profile:v', 'baseline', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(target)])
    return {'id': f'street-{ident}', 'kind': 'street', 'title': info.get('name', slug), 'sourcePage': page, 'downloadUrl': url, 'license': 'Mixkit Stock Video Free License', 'file': str(target.relative_to(ROOT)).replace('\\', '/'), 'duration': float(probe(target)['format']['duration']), 'sha256': hashlib.sha256(target.read_bytes()).hexdigest()}

def door(ident):
    soup = BeautifulSoup((SOURCES / 'mixkit-doors.html').read_text(encoding='utf8'), 'html.parser')
    node = soup.select_one(f'[data-audio-player-item-id-value="{ident}"]')
    url = node['data-audio-player-preview-url-value']
    source = get(url, SOURCES / f'door-source-{ident}.mp3')
    duration = float(probe(source)['format']['duration'])
    tempo = max(1, duration / 2)
    target = DOORS / f'door-{ident}.wav'
    filters = f'atempo={tempo},apad=whole_dur=1,atrim=duration=2,highpass=f=70,loudnorm=I=-23:TP=-3:LRA=7,afade=t=in:d=0.005'
    convert(['-i', str(source), '-af', filters, '-ac', '1', '-ar', '48000', '-c:a', 'pcm_s16le', str(target)])
    return {'id': f'door-{ident}', 'kind': 'door', 'title': node.parent.get_text(' ', strip=True).split('Download')[0], 'sourcePage': 'https://mixkit.co/free-sound-effects/door/', 'downloadUrl': url, 'license': 'Mixkit Sound Effects Free License', 'file': str(target.relative_to(ROOT)).replace('\\', '/'), 'duration': float(probe(target)['format']['duration']), 'sourceDuration': duration, 'tempo': tempo, 'sha256': hashlib.sha256(target.read_bytes()).hexdigest()}

if __name__ == '__main__':
    for folder in [SOURCES, STREETS, DOORS]: folder.mkdir(parents=True, exist_ok=True)
    get('https://mixkit.co/free-sound-effects/door/', SOURCES / 'mixkit-doors.html')
    for name in ['videoFree', 'sfxFree']:
        get(f'https://mixkit.co/license/modal/{name}/', SOURCES / f'license-{name}.html')
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        futures = [pool.submit(video, item) for item in VIDEOS] + [pool.submit(door, ident) for ident in DOOR_IDS]
        records = []
        for future in concurrent.futures.as_completed(futures):
            record = future.result(); records.append(record)
            print(record['id'], record['duration'], flush=True)
    records.sort(key=lambda x: (x['kind'], x['id']))
    (SOURCES / 'media-manifest.json').write_text(json.dumps(records, indent=2), encoding='utf8')
