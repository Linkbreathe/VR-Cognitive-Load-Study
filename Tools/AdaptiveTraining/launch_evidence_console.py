"""Single console entry: reuse/start the bridge and open tests; never create a test assignment."""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import socket
import signal
import subprocess
import sys
import time
from urllib.request import urlopen
import webbrowser

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent


def health(url):
    try:
        with urlopen(url + "console/health", timeout=2) as response:
            return json.load(response)
    except Exception:
        return None


def compatible(info, lsl=None, policy=None, host=None):
    if not info or info.get("application") != "Evidence Console":
        return False
    from evidence_console import REVISION, config_fingerprint
    from personalized_signals import REVISION as FEATURE_REVISION
    if info.get("featureRevision") != FEATURE_REVISION:
        return False
    if info.get("revision") != REVISION or Path(info.get("project", "")).resolve() != ROOT:
        return False
    if lsl and not info.get("lslSelection"):
        return False
    if lsl and info.get("lslHash") != config_fingerprint(lsl, lsl=True):
        return False
    if policy and info.get("policy") != str(policy.resolve()):
        return False
    if policy and info.get("policyHash") != config_fingerprint(policy):
        return False
    if host and info.get("host") != host:
        return False
    return True


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--host", default="127.0.0.1", help="Use 0.0.0.0 only for an intended LAN Quest connection")
    parser.add_argument("--lsl-config", type=Path)
    parser.add_argument("--personalized-policy", type=Path)
    parser.add_argument("--no-browser", action="store_true")
    parser.add_argument("--page", choices=("console", "tests"), default="tests")
    parser.add_argument("--stop", action="store_true", help="Stop this project's bridge after Unity has stopped sending telemetry")
    args = parser.parse_args()
    if not 1024 <= args.port <= 65535:
        raise ValueError("Port must be between 1024 and 65535")
    url = f"http://127.0.0.1:{args.port}/"
    if args.stop:
        info = health(url)
        if not info or info.get("application") != "Evidence Console" or Path(info.get("project", "")).resolve() != ROOT or not isinstance(info.get("pid"), int):
            raise RuntimeError("No matching Evidence Console process found. No process was stopped.")
        with urlopen(url + "console/snapshot", timeout=3) as response:
            current = json.load(response)
        if current.get("task") and current.get("telemetryAge", 0) <= 5:
            raise RuntimeError("Unity is still sending telemetry. End the session, exit Play Mode or the Quest app, wait five seconds, then stop the bridge.")
        os.kill(info["pid"], signal.SIGTERM)
        print("Evidence Console service stopped. Recorded files are retained. Start a new Unity session after restarting.")
        return
    from personalized_model import resolve_policy_path
    args.personalized_policy = resolve_policy_path(args.personalized_policy, ROOT)
    selection = args.lsl_config
    saved = ROOT / "UserSettings/lsl-source-selection.json"
    if selection is None and saved.exists():
        selection = saved
    if selection:
        from lsl_sources import validate_config
        validated = validate_config(json.loads(selection.read_text(encoding="utf-8-sig")))
        if any(s.get("synthetic") or s.get("data_origin") in ("synthetic", "recorded_xdf_replay") for s in validated["streams"]):
            if args.page != "tests":
                raise ValueError("The saved LSL selection contains replay/synthetic inputs. Select real outlets for this launcher; use the explicit engineering Test sessions entry for simulations.")
            print("Engineering Test sessions: the saved source is replay/synthetic. Its provenance and quality gates remain unchanged; this is not live physiology.")
    if args.personalized_policy:
        from personalized_model import policy_config
        policy_config(json.loads(args.personalized_policy.read_text(encoding="utf-8-sig")))
    info = health(url)
    if info:
        if not compatible(info, selection, args.personalized_policy, args.host):
            raise RuntimeError("This port belongs to a different console, revision or configuration. End the session and stop Unity telemetry, then use StartEvidenceConsole.cmd --stop before relaunching. No process was stopped.")
        print("Using the running Evidence Console: " + url)
    else:
        with socket.socket() as probe:
            if probe.connect_ex(("127.0.0.1", args.port)) == 0:
                raise RuntimeError("Port is already occupied by another service. Stop the old bridge after ending its session, or choose --port. No process was stopped.")
        import numpy, scipy, sklearn, pylsl, evidence_guard  # Check before detaching.
        output = ROOT / "Evidence/EvidenceConsole" / datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
        output.mkdir(parents=True)
        command = [sys.executable, str(HERE / "service.py"), "--output", str(output / "service"),
                   "--host", args.host, "--port", str(args.port)]
        if selection:
            command += ["--lsl-config", str(selection.resolve())]
        if args.personalized_policy:
            command += ["--personalized-policy", str(args.personalized_policy.resolve())]
        with (output / "service.log").open("w", encoding="utf-8") as log:
            process = subprocess.Popen(command, cwd=ROOT, stdin=subprocess.DEVNULL, stdout=log, stderr=subprocess.STDOUT,
                                       creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        deadline = time.monotonic() + 30
        while time.monotonic() < deadline:
            info = health(url)
            if compatible(info, selection, args.personalized_policy, args.host):
                break
            if process.poll() is not None:
                raise RuntimeError("Service exited; inspect " + str(output / "service.log"))
            time.sleep(.25)
        else:
            raise RuntimeError("Service readiness timed out; inspect " + str(output / "service.log"))
        (output / "launcher.json").write_text(json.dumps({"pid": process.pid, "url": url, "command": command}, indent=2), encoding="utf-8")
        print("Evidence Console ready: " + url)
        print("Service log: " + str(output / "service.log"))
    if not selection:
        print("No saved LSL selection. The console will show waiting states until sources and Unity are configured.")
    print("Start the configured Unity session. Closing the browser does not stop acquisition.")
    if not args.no_browser:
        webbrowser.open(url + ("tests" if args.page == "tests" else ""))


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print("Evidence Console could not start: " + str(error), file=sys.stderr)
        sys.exit(1)
