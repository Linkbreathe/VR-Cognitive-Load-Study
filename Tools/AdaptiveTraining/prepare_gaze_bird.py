"""Decode the supplied GIF without repainting it; preserve timing and alpha."""
from pathlib import Path
import argparse
import hashlib
import json
from PIL import Image


def prepare(source, output):
    source, output = Path(source), Path(output)
    output.mkdir(parents=True, exist_ok=True)
    image = Image.open(source)
    durations = []
    for index in range(image.n_frames):
        image.seek(index)
        durations.append(image.info.get("duration", 100) / 1000)
        image.convert("RGBA").save(output / f"frame-{index:02d}.png")
    manifest = dict(revision="gaze-bird-frames-v1", sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                    width=image.width, height=image.height, durations=durations, loop=True)
    (output / "animation.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps(manifest))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("source")
    parser.add_argument("output")
    args = parser.parse_args()
    prepare(args.source, args.output)
