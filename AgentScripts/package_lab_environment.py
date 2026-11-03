"""Package the dev12 warm study with the unchanged dev11 protocol service without raw recordings or local environments."""
from pathlib import Path
import hashlib
import json
import zipfile

root = Path(__file__).resolve().parents[1]
target = root / "Builds/Stage2PersonalizedDev12.zip"
config = json.loads((root / "Assets/TaskPlatform/Config/adaptive-training-config.json").read_text())
assert config["version"] == "2.0.0-stage2-dev11" and config["protocol"] == "personalized-rf-v2"
config.update(enabled=True, sessionType="engineering", participantCode="engineering", mode="ACTIVE")
with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as archive:
    def add(path):
        archive.write(path, path.relative_to(root).as_posix())
    add(root / "Builds/Quest/EvidenceVR-Stage2-dev12.apk")
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
        "Dev12 warm-study application with unchanged dev11 configuration/service. Engineering deployment. Copy the configuration, manifest and AdaptiveMedia directory to the app's persistent data folder. "
        "Replace controllerUrl with the study computer address for a standalone headset. This is not a participant allocation. "
        "All media gains/review flags are unchanged. See Docs/LabEnvironmentRelease.md and Docs/ExperimentGuide.md.\n")
    for name in ("LabEnvironmentValidation.cs", "LabEnvironmentPreview.cs", "package_lab_environment.py"):
        add(root / "AgentScripts" / name)
    add(root / "Assets/TaskPlatform/Environment/ThirdParty/AssetCredits.md")
    add(root / "Assets/TaskPlatform/Environment/ThirdParty/Kenney/License.txt")
    evidence = root / "Evidence/LabEnvironment"
    for name in ("android-build.json", "apk-signature.txt", "apk-badging.txt", "protected-source-check.json",
                 "unity-scene-validation.json", "unity-live-validation.json", "unity-protocol-validation.txt",
                 "live-preview-console.json", "asset-downloads.json", "lighting-bake.json", "final-verification.json", "engineering-session-metadata.json", "build-asset-audit.json",
                 "study-final-front.png", "study-final-workspace.png", "study-final-storage.png", "study-live-task.png"):
        add(evidence / name)
with zipfile.ZipFile(target) as archive:
    assert archive.testzip() is None
    names = archive.namelist()
    assert len(names) == len(set(names))
    assert not any(name.endswith((".xdf", ".joblib")) or "raw_signals" in name or "/.venv/" in name for name in names)
report = {"path": str(target), "bytes": target.stat().st_size, "entries": len(names),
          "sha256": hashlib.sha256(target.read_bytes()).hexdigest(), "crc_verified": True,
          "protocol": config["protocol"], "configuration_version": config["version"], "application_version": "2.0.0-stage2-dev12", "environment_revision": "warm-study-v1", "participant_ready": False, "resting_baseline_seconds": 0}
(root / "Evidence/LabEnvironment/release-package.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
