"""Create paired, identically mixed local clips from an explicit source manifest.

Downloads are separate from this reproducible preparation step. Every cut retains source provenance.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess


def run(args):
    return subprocess.run(args,check=True,text=True,capture_output=True).stdout


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def prepare(source_manifest, output, ffmpeg="ffmpeg", ffprobe="ffprobe", append_to=None):
    catalog=json.loads(Path(source_manifest).read_text(encoding="utf-8-sig"))
    if catalog.get("videoProfile","default") not in ("default","baseline"):
        raise ValueError("Unknown video profile")
    items=catalog["items"]
    previous=json.loads(Path(append_to).read_text(encoding="utf-8"))["items"] if append_to else []
    combined=previous+items
    if not items or len({i["id"] for i in combined})!=len(combined):
        raise ValueError("Expected unique pair IDs across existing and new content")
    training=sum(i["split"]=="train" for i in combined)
    if training<6 or training%2 or sorted(i["split"] for i in combined)!=sorted(["train"]*training+["test_a"]*2+["test_b"]*2):
        raise ValueError("Expected an even training pool of at least six pairs and two pairs per transfer set")
    output=Path(output);output.mkdir(parents=True,exist_ok=True)
    completed=list(previous)
    for item in previous:
        for kind in ("audio","video"):
            path=output/Path(item[kind+"File"]).name
            if not path.is_file() or sha(path)!=item[kind+"Sha256"]:
                raise ValueError(f"Existing release pair is missing or changed: {path}")
    for item in items:
        if not item["id"].replace("-","").replace("_","").isalnum():
            raise ValueError("Media IDs must be safe filenames")
        duration=float(item["duration"])
        source_duration=float(item.get("sourceDuration",duration))
        if not 10<=duration<=12 or not item["source"] or not item["rightsRecord"]:
            raise ValueError("Each source needs a 10-12 second cut and provenance record")
        if not 0<source_duration<=duration or duration-source_duration>2:
            raise ValueError("A complete dialogue cut may use at most two seconds of documented end padding")
        source=Path(item["sourceFile"]).resolve(strict=True)
        video=output/(item["id"]+".mp4");audio=output/(item["id"]+".wav")
        if video.exists() or audio.exists():
            raise FileExistsError(f"Refusing to overwrite existing pair {item['id']}")
        # One mono mix is encoded in the video; the audio-only version is decoded from that exact mix.
        pad=duration-source_duration
        scan=subprocess.run([ffmpeg,"-nostdin","-v","info","-ss",str(item["startSeconds"]),"-t",str(source_duration),"-i",str(source),
            "-vn","-af","aformat=channel_layouts=mono,loudnorm=I=-23:TP=-3:LRA=7:print_format=json","-f","null","-"],
            check=True,text=True,capture_output=True).stderr
        measured=json.loads(scan[scan.rfind("{"):scan.rfind("}")+1])
        normalization=("aformat=channel_layouts=mono,loudnorm=I=-23:TP=-3:LRA=7:linear=true:"
            f"measured_I={measured['input_i']}:measured_LRA={measured['input_lra']}:measured_TP={measured['input_tp']}:"
            f"measured_thresh={measured['input_thresh']}:offset={measured['target_offset']},"
            "aresample=48000,alimiter=limit=0.707946:level=false:latency=true")
        run([ffmpeg,"-nostdin","-v","error","-ss",str(item["startSeconds"]),"-t",str(source_duration),"-i",str(source),"-t",str(duration),
            "-map","0:v:0","-map","0:a:0","-vf",f"scale=1280:720:force_original_aspect_ratio=decrease,pad=1280:720:(ow-iw)/2:(oh-ih)/2,setsar=1,tpad=stop_mode=clone:stop_duration={pad}",
            "-af",f"{normalization},apad=pad_dur={pad}","-ac","1","-ar","48000","-c:v","libx264","-pix_fmt","yuv420p",
            *( ["-profile:v","baseline","-bf","0"] if catalog.get("videoProfile")=="baseline" else [] ),
            "-r","30","-colorspace","bt709","-color_primaries","bt709","-color_trc","bt709","-c:a","aac","-b:a","192k","-movflags","+faststart",str(video)])
        run([ffmpeg,"-nostdin","-v","error","-i",str(video),"-vn","-acodec","pcm_s16le",str(audio)])
        info=json.loads(run([ffprobe,"-v","error","-show_format","-show_streams","-of","json",str(video)]))
        actual=float(info["format"]["duration"])
        if not 9.99<=actual<=12.1:
            raise ValueError(f"Encoded duration invalid: {actual}")
        output_scan=subprocess.run([ffmpeg,"-nostdin","-v","info","-i",str(audio),"-af",
            "loudnorm=I=-24:TP=-3:LRA=7:print_format=json","-f","null","-"],check=True,text=True,capture_output=True).stderr
        output_level=float(json.loads(output_scan[output_scan.rfind("{"):output_scan.rfind("}")+1])["input_i"])
        # Short-clip gating and codec overshoot can differ from the processing target.
        # Apply only attenuation, identically in both runtime modalities, to match the encoded clips.
        matched_gain=min(1.,10**((-24-output_level)/20))
        row={k:item.get(k,"") for k in ("id","work","source","category","emotion","rightsRecord","split","notes")}
        row.update(audioFile=audio.name,videoFile=video.name,audioSha256=sha(audio),videoSha256=sha(video),
            duration=min(12,actual),gain=matched_gain,encodedLufs=output_level,runtimeTargetLufs=-24,ready=True,volumeReviewed=bool(item.get("volumeReviewed",False)),synthetic=False,
            startSeconds=item["startSeconds"],sourceDuration=source_duration,endPaddingSeconds=pad,sourceSha256=sha(source),
            normalization="two-pass mono -23 LUFS target, -3 dBTP target, pre-encode -3 dBFS limiter; AAC output measured separately")
        completed.append(row)
        print(f"Prepared {item['id']}: {actual:.2f}s, paired PCM, gain {matched_gain:.4f}",flush=True)
    target=output.parent/"adaptive-media-manifest.json"
    target.write_text(json.dumps({"version":catalog.get("outputVersion","stage2-paired-media-v1-matched"),"items":completed},indent=2),encoding="utf-8")
    return target


if __name__=="__main__":
    p=argparse.ArgumentParser();p.add_argument("manifest");p.add_argument("output");p.add_argument("--ffmpeg",default=shutil.which("ffmpeg") or "ffmpeg");p.add_argument("--ffprobe",default=shutil.which("ffprobe") or "ffprobe")
    p.add_argument("--append-to",help="Existing manifest; existing pairs in output are hash-verified and retained")
    a=p.parse_args();print(prepare(a.manifest,a.output,a.ffmpeg,a.ffprobe,a.append_to))
