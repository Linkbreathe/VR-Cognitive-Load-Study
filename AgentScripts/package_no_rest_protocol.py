"""Package the dev11 engineering deployment without raw recordings or local environments."""
from pathlib import Path
import hashlib
import json
import zipfile

root = Path(__file__).resolve().parents[1]
target = root / "Builds/Stage2PersonalizedDev11.zip"
config = json.loads((root / "Assets/TaskPlatform/Config/adaptive-training-config.json").read_text())
assert config["version"] == "2.0.0-stage2-dev11" and config["protocol"] == "personalized-rf-v2"
config.update(enabled=True, sessionType="engineering", participantCode="engineering", mode="ACTIVE")
with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as archive:
    def add(path):
        archive.write(path, path.relative_to(root).as_posix())
    add(root / "Builds/Quest/EvidenceVR-Stage2-dev11.apk")
    for path in (root / "Tools/AdaptiveTraining").iterdir():
        if path.is_file() and path.suffix in (".py", ".json", ".txt"):
            add(path)
    for path in (root / "Docs").glob("*.md"):
        add(path)
    for path in (root / "Builds/Stage2MediaRelease").rglob("*"):
        if path.is_file() and path.name not in ("adaptive-training-config.json", "README.txt"):
            add(path)
    archive.writestr("Builds/Stage2MediaRelease/adaptive-training-config.json", json.dumps(config, indent=2))
    archive.writestr("Builds/Stage2MediaRelease/README.txt",
        "Dev11 engineering deployment. Copy the configuration, manifest and AdaptiveMedia directory to the app's persistent data folder. "
        "Replace controllerUrl with the study computer address for a standalone headset. This is not a participant allocation. "
        "All media gains/review flags are unchanged. See Docs/NoRestProtocolRelease.md and Docs/ExperimentGuide.md.\n")
    for name in ("PersonalizedProtocolValidation.cs", "NoRestProtocolValidation.cs", "package_no_rest_protocol.py"):
        add(root / "AgentScripts" / name)
    evidence = root / "Evidence/NoRestProtocol"
    for name in ("android-build.json", "apk-signature.txt", "apk-badging.txt", "python-tests.txt",
                 "unity-protocol-validation.txt", "unity-live-validation.txt", "python-no-rest-check.json",
                 "live-preview-console.json", "preparation.png", "calibration.png", "final-verification.json"):
        add(evidence / name)
with zipfile.ZipFile(target) as archive:
    assert archive.testzip() is None
    names = archive.namelist()
    assert len(names) == len(set(names))
    assert not any(name.endswith((".xdf", ".joblib")) or "raw_signals" in name or "/.venv/" in name for name in names)
report = {"path": str(target), "bytes": target.stat().st_size, "entries": len(names),
          "sha256": hashlib.sha256(target.read_bytes()).hexdigest(), "crc_verified": True,
          "protocol": config["protocol"], "version": config["version"], "participant_ready": False, "resting_baseline_seconds": 0}
(root / "Evidence/NoRestProtocol/release-package.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
