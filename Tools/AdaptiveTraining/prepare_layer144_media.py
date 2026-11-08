"""Build a reproducible local bank of 100 dialogue-bearing AV stimuli."""

import hashlib
import json
from pathlib import Path
import subprocess

from faster_whisper import WhisperModel

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "MediaSources/Stage2"
OUT = ROOT / "Builds/LayerAB144"
WORKS = {
    "Harry Potter": ["49o5QSVRtfg", "hMIN4wmHuiA", "phdwdrPs3Bo"],
    "The Lord of the Rings": ["Bypor4e04rg"],
    "Titanic": ["oklPl95DC8c", "JYdCltjvrxg", "titanic-paramount"],
    "Spirited Away": ["fDUFP7EeXLE"],
    "Kiki's Delivery Service": ["t6-fT0hjTvc"],
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    media = OUT / "AdaptiveMedia"
    media.mkdir(parents=True, exist_ok=True)
    model = WhisperModel(
        str(next((SRC / "asr-model").glob("*/snapshots/*"))),
        device="cpu",
        compute_type="int8",
        cpu_threads=4,
    )
    pools = {}
    for work, files in WORKS.items():
        candidates = []
        for name in files:
            path = SRC / (name + ".mp4")
            meta = json.loads(path.with_suffix(".info.json").read_text(encoding="utf8"))
            cache = OUT / (name + "-speech.json")
            if not cache.exists():
                segments, _ = model.transcribe(
                    str(path), beam_size=1, vad_filter=True, word_timestamps=True
                )
                spans = [
                    (w.start, w.end)
                    for s in segments
                    for w in s.words
                    if w.probability >= 0.4 and any(c.isalpha() for c in w.word)
                ]
                cache.write_text(json.dumps(spans))
            spans = json.loads(cache.read_text())
            for start in range(0, int(meta["duration"]) - 20, 5):
                seconds = sum(
                    max(0, min(b, start + 20) - max(a, start)) for a, b in spans
                )
                if seconds >= 3:
                    candidates.append((seconds, name, start, meta["webpage_url"]))
        pools[work] = sorted(candidates, reverse=True)
    # Round-robin across works; prefer speech-rich spans with little overlap.
    chosen = []
    for separation in (20, 15, 10, 5):
        while len(chosen) < 100:
            count = len(chosen)
            for work, pool in pools.items():
                for seconds, name, start, url in pool:
                    if all(
                        name != r[2] or abs(start - r[3]) >= separation for r in chosen
                    ):
                        chosen.append((work, seconds, name, start, url))
                        break
                if len(chosen) == 100:
                    break
            if count == len(chosen):
                break
    if len(chosen) != 100 or set(r[0] for r in chosen) != set(WORKS):
        raise ValueError(
            "Not enough dialogue-bearing cuts from all configured works: " + str(len(chosen))
        )
    manifest = json.loads(
        (ROOT / "Assets/Resources/adaptive-media-manifest.json").read_text()
    )
    manifest["version"] = "layer-ab-144-dialogue-v2-no-heidi"
    manifest["trainingClipCount"] = len(chosen)
    for i, (work, seconds, name, start, url) in enumerate(chosen, 1):
        stem = f"ab20-{i:03}"
        vid = media / (stem + ".mp4")
        wav = media / (stem + ".wav")
        subprocess.run(
            [
                "ffmpeg",
                "-nostdin",
                "-loglevel",
                "error",
                "-y",
                "-ss",
                str(start),
                "-i",
                str(SRC / (name + ".mp4")),
                "-t",
                "20",
                "-map",
                "0:v:0",
                "-map",
                "0:a:0",
                "-vf",
                "scale=640:360:force_original_aspect_ratio=decrease,pad=640:360:(ow-iw)/2:(oh-ih)/2,setsar=1",
                "-c:v",
                "libx264",
                "-threads",
                "3",
                "-preset",
                "veryfast",
                "-crf",
                "23",
                "-profile:v",
                "baseline",
                "-pix_fmt",
                "yuv420p",
                "-r",
                "30",
                "-af",
                "loudnorm=I=-23:TP=-3:LRA=7",
                "-ac",
                "1",
                "-ar",
                "48000",
                "-c:a",
                "aac",
                "-movflags",
                "+faststart",
                str(vid),
            ],
            check=True,
        )
        subprocess.run(
            [
                "ffmpeg",
                "-nostdin",
                "-loglevel",
                "error",
                "-y",
                "-i",
                str(vid),
                "-vn",
                "-c:a",
                "pcm_s16le",
                str(wav),
            ],
            check=True,
        )
        duration = float(
            subprocess.check_output(
                [
                    "ffprobe",
                    "-v",
                    "error",
                    "-show_entries",
                    "format=duration",
                    "-of",
                    "csv=p=0",
                    str(vid),
                ]
            )
        )
        if abs(duration - 20) > 0.1:
            raise ValueError((stem, duration))
        manifest["items"].append(
            dict(
                id=stem,
                work=work,
                source=url,
                sourceFile=name + ".mp4",
                startSeconds=start,
                duration=20,
                speechSeconds=round(seconds, 2),
                category="dialogue",
                split="training20",
                ready=True,
                volumeReviewed=False,
                synthetic=False,
                gain=1,
                rightsRecord="Public promotional source, local pilot preparation.",
                videoFile=vid.name,
                audioFile=wav.name,
                videoSha256=sha(vid),
                audioSha256=sha(wav),
            )
        )
    dest = OUT / "layer-ab-144-media.json"
    dest.write_text(json.dumps(manifest, indent=2))
    print(dest, len(chosen))


if __name__ == "__main__":
    main()
