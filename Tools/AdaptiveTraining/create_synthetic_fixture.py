"""Explicit engineering-only media for playback checks. Never a film/study stimulus."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess


def create():
    root=Path(__file__).resolve().parents[2]
    target=Path((root/"Evidence/Stage2/OverrideBackup/target.txt").read_text(encoding="utf-8-sig")).parent
    directory=root/"Evidence/Stage2/SyntheticMedia";directory.mkdir(parents=True,exist_ok=True)
    video=directory/"synthetic-test-pattern.mp4";audio=directory/"synthetic-test-tone.wav"
    ffmpeg=shutil.which("ffmpeg")
    subprocess.run([ffmpeg,"-nostdin","-v","error","-y","-f","lavfi","-i","testsrc2=size=1280x720:rate=30",
        "-f","lavfi","-i","sine=frequency=330:sample_rate=48000:duration=11","-t","11","-af","volume=0.05",
        "-c:v","libx264","-preset","ultrafast","-pix_fmt","yuv420p","-colorspace","bt709","-color_primaries","bt709","-color_trc","bt709","-c:a","aac","-ac","1",str(video)],check=True)
    subprocess.run([ffmpeg,"-nostdin","-v","error","-y","-i",str(video),"-vn","-c:a","pcm_s16le",str(audio)],check=True)
    assets=target/"AdaptiveMedia";assets.mkdir(exist_ok=True)
    catalog=json.loads((root/"Assets/Resources/adaptive-media-manifest.json").read_text(encoding="utf-8"))
    for i in catalog["items"]:
        for kind,path,extension in (("audio",audio,"wav"),("video",video,"mp4")):
            name=f"stage2-synthetic-{i['id']}.{extension}";shutil.copy2(path,assets/name)
            i[kind+"File"]=name;i[kind+"Sha256"]=hashlib.sha256(path.read_bytes()).hexdigest()
        i.update(work="SYNTHETIC TEST PATTERN",source="generated lavfi test pattern and quiet sine; not semantic content",rightsRecord="generated engineering fixture",
                 synthetic=True,ready=True,volumeReviewed=True,notes="SOFTWARE TEST ONLY. Excluded from participant media readiness.")
    (target/"stage2-synthetic-media.json").write_text(json.dumps(catalog,indent=2),encoding="utf-8")
    print(target)


if __name__=="__main__":create()
