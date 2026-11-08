"""Discover LSL outlets and validate explicit, restart-only acquisition selections."""
from __future__ import annotations

import argparse
import json
import math
from pathlib import Path
import xml.etree.ElementTree as ET


def stream_identity(info):
    xml = ET.fromstring(info.as_xml())
    recorded = xml.findtext("./desc/xdf_replay/recorded_data", "false").lower() == "true"
    origin = xml.findtext("./desc/data_origin", "unknown")
    if recorded:
        origin = "recorded_xdf_replay"
    return dict(data_origin=origin, name=info.name(), source_id=info.source_id(), uid=info.uid(),
                stream_type=info.type(), sample_rate=info.nominal_srate(),
                channel_count=info.channel_count(), channel_format=info.channel_format(), recorded=origin in ("recorded_csv_replay", "recorded_xdf_replay"),
                channel_labels=[c.findtext("label", "") for c in xml.findall("./desc/channels/channel")])


def discover():
    import pylsl
    rows = []
    for info in pylsl.resolve_streams(wait_time=2):
        inlet = None
        try:
            inlet = pylsl.StreamInlet(info)
            rows.append(stream_identity(inlet.info(timeout=2)))
        except Exception as error:
            row = stream_identity(info)
            row["discovery_error"] = str(error)
            rows.append(row)
        finally:
            if inlet is not None:
                inlet.close_stream()
    return {"streams": sorted(rows, key=lambda r: (r["name"], r["source_id"], r["uid"]))}


def matching_streams(infos, selection):
    """Pin a selected outlet instance; never follow a shared source_id to another stream."""
    uid = selection.get("uid")
    matches = [info for info in infos if (info.uid() == uid if uid else info.source_id() == selection["source_id"])]
    for key, getter in (("name", "name"), ("stream_type", "type"), ("source_id", "source_id")):
        expected = selection.get(key)
        if expected:
            matches = [info for info in matches if getattr(info, getter)() == expected]
    return matches


def observation_values(config, samples):
    labels = config.get("channel_labels", [])
    return [{"channel":c, "label":labels[c] if c < len(labels) else str(c),
             "value":None if isinstance(value, float) and not math.isfinite(value) else value}
            for c, value in zip(config["channels"], samples[-1])]


def validate_config(config):
    streams = config.get("streams")
    if not isinstance(streams, list) or len(streams) != 1:
        raise ValueError("Select one LSL source")
    kinds = set()
    for stream in streams:
        stream.setdefault("source_id", "")
        kind = stream.get("kind")
        if kind not in ("EEG", "OBSERVE") or kind in kinds:
            raise ValueError("Configure at most one input for each processing role")
        kinds.add(kind)
        if not stream.get("uid") and (not isinstance(stream.get("source_id"), str) or not stream["source_id"].strip()):
            raise ValueError("An outlet UID or nonempty source_id is required")
        for key in ("sample_rate", "scale_to_units", "amplitude_limit", "window_seconds"):
            value = stream.get(key)
            allow_zero = key == "sample_rate" and kind == "OBSERVE"
            if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value) or (value < 0 if allow_zero else value <= 0):
                raise ValueError(f"{kind}: {key} must be finite and positive")
        channels = stream.get("channels")
        if not isinstance(channels, list) or not channels or any(type(c) is not int or c < 0 for c in channels) or len(channels) != len(set(channels)):
            raise ValueError(f"{kind}: use distinct, zero-based channel indices")
        count = stream.get("channel_count", 0)
        if count and max(channels) >= count:
            raise ValueError(f"{kind}: selected channel exceeds the discovered channel count")
        if stream.get("data_origin", "unknown") not in ("unknown", "live_sensor", "synthetic", "recorded_csv_replay", "recorded_xdf_replay"):
            raise ValueError("Unsupported source provenance")
        expected_labels = ["AFz", "AF3", "AF4", "AF7", "AF8", "Pz", "P3", "P4"]
        if kind == "EEG" and (stream.get("stream_type", "").upper() != "EEG"
                or stream["sample_rate"] != 250 or count != 8
                or stream.get("channel_labels") != expected_labels or channels != list(range(8))
                or stream.get("differential")):
            raise ValueError("Select the 250 Hz eight-channel Mindtooth EEG profile")
        if kind == "EEG" and not 10 <= stream["window_seconds"] <= 15:
            raise ValueError("Keep EEG windows at 10-15 seconds")
        if kind != "OBSERVE" and stream.get("channel_format") == 3:
            raise ValueError("String streams support receive-only mode")
        if kind == "OBSERVE" and (stream["scale_to_units"] != 1 or stream.get("differential")):
            raise ValueError("Receive-only mode preserves values without scaling or derivation")
    return config


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--discover", action="store_true")
    parser.add_argument("--validate", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = discover() if args.discover else validate_config(json.loads(args.validate.read_text(encoding="utf-8-sig")))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
