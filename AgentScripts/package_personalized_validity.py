"""Package the dev10 engineering deployment without raw recordings or local environments."""
from pathlib import Path
import hashlib
import json
import zipfile

root = Path(__file__).resolve().parents[1]
target = root / "Builds/Stage2PersonalizedDev10.zip"
config = json.loads((root / "Assets/TaskPlatform/Config/adaptive-training-config.json").read_text())
assert config["version"] == "2.0.0-stage2-dev10" and config["protocol"] == "personalized-rf-v2"
config.update(enabled=True, sessionType="engineering", participantCode="engineering", mode="ACTIVE")
with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as archive:
    def add(path):
        archive.write(path, path.relative_to(root).as_posix())
    add(root / "Builds/Quest/EvidenceVR-Stage2-dev10.apk")
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
        "Dev10 engineering deployment. Copy the configuration, manifest and AdaptiveMedia directory to the app's persistent data folder. "
        "Replace controllerUrl with the study computer address for a standalone headset. This is not a participant allocation. "
        "All media gains/review flags are unchanged. See Docs/PersonalizedValidityRelease.md and Docs/ExperimentGuide.md.\n")
    for name in ("PersonalizedProtocolValidation.cs", "PersonalizedValidityValidation.cs", "validate_personalized_validity.py", "package_personalized_validity.py"):
        add(root / "AgentScripts" / name)
    evidence = root / "Evidence/PersonalizedValidity"
    for name in ("android-build.json", "apk-signature.txt", "apk-badging.txt", "python-tests.txt",
                 "production-forest-test.txt", "unity-protocol-validation.txt", "unity-validity-validation.txt",
                 "p007-eeg-audit.json", "xdf-header.json", "live-preview-console.json",
                 "calibration-neutral.png", "oral-questions.png", "final-verification.json"):
        add(evidence / name)
    add(evidence / "ProductionForestSynthetic/model_report.json")
with zipfile.ZipFile(target) as archive:
    assert archive.testzip() is None
    names = archive.namelist()
    assert len(names) == len(set(names))
    assert not any(name.endswith((".xdf", ".joblib")) or "raw_signals" in name or "/.venv/" in name for name in names)
report = {"path": str(target), "bytes": target.stat().st_size, "entries": len(names),
          "sha256": hashlib.sha256(target.read_bytes()).hexdigest(), "crc_verified": True,
          "protocol": config["protocol"], "version": config["version"], "participant_ready": False}
(root / "Evidence/PersonalizedValidity/release-package.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
