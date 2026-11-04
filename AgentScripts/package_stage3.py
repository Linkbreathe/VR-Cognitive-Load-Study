"""Package the Stage 3 candidate without environments, raw participant data or media."""
from pathlib import Path
import hashlib
import json
import zipfile


ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / "Builds/Stage3"


def main():
    selected = []
    selected += list((ROOT / "Tools/AdaptiveTraining").glob("*.py"))
    selected += list((ROOT / "Tools/AdaptiveTraining").glob("requirements*.txt"))
    selected += [ROOT / "Tools/AdaptiveTraining/personalized-policy.json", ROOT / "Tools/AdaptiveTraining/lsl-config.example.json"]
    selected += [p for p in (ROOT / "Tools/EvidenceGuard").rglob("*") if p.is_file() and
                 not any(part in ("__pycache__", "build", "dist") or part.endswith(".egg-info") for part in p.parts)]
    selected += [ROOT / "Docs" / name for name in ("EvidenceGuardGuide.md", "EvidenceGuardReview.md", "EvidenceGuardRelease.md",
                 "ProjectLog.md", "AcceptanceRecord.md", "ExperimentGuide.md")]
    selected += [p for p in (ROOT / "Evidence/Stage3").rglob("*") if p.is_file() and p.suffix in (".json", ".jsonl", ".txt", ".tsv") and p.name != "package-result.json"]
    selected += [ROOT / "Assets/TaskPlatform/Runtime" / name for name in ("EvidenceGuardRuntime.cs", "EvidenceGuardRuntime.cs.meta",
                 "AdaptiveRuntime.cs", "AdaptiveMedia.cs", "AdaptiveTrainingModel.cs", "PersonalizedProtocol.cs", "SessionRecorder.cs", "TaskPlatformController.cs")]
    selected += [DEST / "EvidenceVR-Stage3-dev1.apk", DEST / "evidence_guard-0.1.0-py3-none-any.whl"]
    selected += list(DEST.glob("adaptive-training-config-*.json"))
    selected += [ROOT / "AgentScripts" / name for name in ("EvidenceGuardBuild.cs", "EvidenceGuardValidation.cs", "package_stage3.py")]
    manifest = {"revision": "evidence-guard-v3-minimal-1", "application": "3.0.0-stage3-dev1", "android_code": 25,
                "date": "2026-11-03", "device_verified": False, "participant_ready": False,
                "media_included": False, "default_guard_mode": "off", "files": {}}
    archive = DEST / "EvidenceGuard-Stage3-dev1-delivery.zip"
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as z:
        for path in sorted(set(selected)):
            data = path.read_bytes(); relative = path.relative_to(ROOT).as_posix()
            z.writestr(relative, data)
            manifest["files"][relative] = {"bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}
        z.writestr("release-manifest.json", json.dumps(manifest, indent=2))
    (DEST / "release-manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps({"archive": str(archive), "files": len(selected), "bytes": archive.stat().st_size,
                      "sha256": hashlib.sha256(archive.read_bytes()).hexdigest()}, indent=2))


if __name__ == "__main__":
    main()
