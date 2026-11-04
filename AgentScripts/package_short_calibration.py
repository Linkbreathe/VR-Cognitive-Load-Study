"""Package the dev2 short-calibration candidate with its matching host and evidence."""
from pathlib import Path
import hashlib
import json
import shutil
import zipfile

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / "Builds/Stage3Dev2"


def main():
    shutil.copy2(ROOT / "Builds/Stage3/evidence_guard-0.1.0-py3-none-any.whl", DEST)
    selected = list((ROOT / "Tools/AdaptiveTraining").glob("*.py"))
    selected += list((ROOT / "Tools/AdaptiveTraining").glob("requirements*.txt"))
    selected += [ROOT / "Tools/AdaptiveTraining" / name for name in ("personalized-policy.json", "lsl-config.example.json")]
    selected += [p for p in (ROOT / "Tools/EvidenceGuard").rglob("*") if p.is_file() and
                 not any(part in ("__pycache__", "build", "dist") or part.endswith(".egg-info") for part in p.parts)]
    selected += [ROOT / "Docs" / name for name in ("ShortCalibrationProgressReview.md", "EvidenceGuardGuide.md",
                 "EvidenceGuardReview.md", "EvidenceGuardRelease.md", "ProjectLog.md", "AcceptanceRecord.md",
                 "ExperimentGuide.md", "PersonalizedControlRulesReview.md", "PersonalizedValidityReview.md")]
    selected += list((ROOT / "Assets/TaskPlatform/Runtime").glob("*.cs"))
    selected += list((ROOT / "Assets/TaskPlatform/Runtime").glob("*.cs.meta"))
    selected += [ROOT / "Assets/TaskPlatform/Config/adaptive-training-config.json"]
    selected += [p for p in (ROOT / "Evidence/Stage3").rglob("*") if p.is_file() and
                 p.suffix in (".json", ".jsonl", ".txt", ".tsv") and p.name != "package-result.json"]
    selected += [ROOT / "Evidence/ShortCalibrationProgress" / name for name in (
        "python-regression.txt", "python-short-tests.txt", "unity-validation.txt", "full-protocol-command.tsv",
        "protected-before.json", "protected-after.json", "build-report.json", "build-summary.json",
        "apk-badging.txt", "apk-signature.txt", "artifact-identity.json", "final-editor-status.json",
        "final-progress-validation.json", "final-console-status.json")]
    selected += list((ROOT / "Assets/Evidence/ShortCalibrationProgress").glob("*.png"))
    selected += [DEST / "EvidenceVR-Stage3-dev2.apk", DEST / "evidence_guard-0.1.0-py3-none-any.whl"]
    selected += list(DEST.glob("adaptive-training-config-*.json"))
    selected += [ROOT / "AgentScripts" / name for name in ("ShortCalibrationProgressBuild.cs",
                 "ShortCalibrationProgressValidation.cs", "PersonalizedProtocolValidation.cs",
                 "EvidenceGuardValidation.cs", "package_short_calibration.py")]
    manifest = {"revision": "calibration-block-budget-v2-short-default", "progress": "trial-progress-v1-completed",
                "application": "3.0.0-stage3-dev2", "android_code": 26, "date": "2026-11-03",
                "initial_calibration_seconds": 216, "supplemental_seconds": 108,
                "device_verified": False, "participant_ready": False,
                "media_included": False, "default_guard_mode": "off", "files": {}}
    archive = DEST / "EvidenceVR-Stage3-dev2-delivery.zip"
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as z:
        for path in sorted(set(selected)):
            data = path.read_bytes()
            relative = path.relative_to(ROOT).as_posix()
            z.writestr(relative, data)
            manifest["files"][relative] = {"bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}
        z.writestr("release-manifest.json", json.dumps(manifest, indent=2))
    (DEST / "release-manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None, "Archive CRC verification failed"
        stored = json.loads(z.read("release-manifest.json"))
        for relative, identity in stored["files"].items():
            assert hashlib.sha256(z.read(relative)).hexdigest() == identity["sha256"], relative
    result = {"archive": str(archive), "files": len(manifest["files"]), "bytes": archive.stat().st_size,
              "sha256": hashlib.sha256(archive.read_bytes()).hexdigest(), "crc_and_manifest_verified": True}
    (ROOT / "Evidence/ShortCalibrationProgress/package-result.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
