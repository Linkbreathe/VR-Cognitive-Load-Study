"""Audit prepared pairs without changing media or participant readiness flags."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import subprocess
import wave

import numpy as np


def audit(folder):
    folder=Path(folder)
    manifest=json.loads((folder/"adaptive-media-manifest.json").read_text(encoding="utf-8"))
    rows=[]
    for item in manifest["items"]:
        video=folder/"AdaptiveMedia"/item["videoFile"]
        audio=folder/"AdaptiveMedia"/item["audioFile"]
        for path,key in [(video,"videoSha256"),(audio,"audioSha256")]:
            assert hashlib.sha256(path.read_bytes()).hexdigest()==item[key],f"Hash mismatch: {path}"
        info=json.loads(subprocess.check_output(["ffprobe","-v","error","-show_streams","-show_format","-of","json",str(video)]))
        vs=next(s for s in info["streams"] if s["codec_type"]=="video")
        assert vs["codec_name"]=="h264" and (vs["width"],vs["height"])==(1280,720)
        assert vs["r_frame_rate"]=="30/1" and vs["pix_fmt"]=="yuv420p"
        duration=float(info["format"]["duration"]);assert 10<=duration<=12.1
        pcm=subprocess.check_output(["ffmpeg","-v","error","-i",str(video),"-vn","-f","s16le","-acodec","pcm_s16le","-"])
        with wave.open(str(audio)) as wav:
            assert wav.getnchannels()==1 and wav.getframerate()==48000 and wav.getsampwidth()==2
            wave_pcm=wav.readframes(wav.getnframes())
        assert wave_pcm==pcm,"Audio-only content must exactly match the decoded AV soundtrack"
        peak=float(np.max(np.abs(np.frombuffer(pcm,dtype='<i2').astype(float)))/32768)
        assert 0<peak<.99,"Silent or clipped soundtrack"
        measured=subprocess.run(["ffmpeg","-v","info","-i",str(audio),"-af","loudnorm=I=-23:TP=-3:LRA=7:print_format=json","-f","null","-"],text=True,capture_output=True,check=True).stderr
        loudness=json.loads(measured[measured.rfind("{"):measured.rfind("}")+1])
        matched=float(loudness["input_i"])+20*math.log10(item["gain"])
        assert abs(matched+24)<.15,"Runtime pair gain must match -24 LUFS within 0.15 LU"
        rows.append(dict(id=item["id"],duration=duration,paired_pcm_identical=True,peak_dbfs=round(20*np.log10(peak),2),
            integrated_lufs=float(loudness["input_i"]),true_peak_dbtp=float(loudness["input_tp"]),runtime_gain=item["gain"],matched_lufs=matched,hashes_match=True))
    assert len(rows)==10
    return dict(status="pass",pairs=rows,scope="Software codec/hash/pair consistency only; human semantic and headset listening review pending")


if __name__=="__main__":
    p=argparse.ArgumentParser();p.add_argument("folder");p.add_argument("--output",type=Path);a=p.parse_args()
    result=json.dumps(audit(a.folder),indent=2)
    if a.output:a.output.write_text(result,encoding="utf-8")
    print(result)
