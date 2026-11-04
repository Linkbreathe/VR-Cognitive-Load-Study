"""Package the engineering build and study-computer sources, without personal data or environments."""
from pathlib import Path
import hashlib
import json
import zipfile

root = Path(__file__).resolve().parents[1]
dest = root / "Builds/Stage3Dev6"
sources = list((root / "Tools/AdaptiveTraining").glob("*.py"))
sources += list((root / "Tools/AdaptiveTraining").glob("requirements*.txt"))
sources += [root / "Tools/AdaptiveTraining" / name for name in ("personalized-policy.json", "lsl-config.example.json")]
sources += [p for p in (root / "Tools/AdaptiveTraining/console").iterdir() if p.is_file()]
sources += [p for p in (root / "Tools/EvidenceGuard").rglob("*") if p.is_file() and
    not any(part in ("__pycache__", "build", "dist") or part.endswith(".egg-info") for part in p.parts)]
sources += [root / name for name in ("StartEvidenceConsole.cmd",
    "Assets/TaskPlatform/Config/adaptive-training-config.json", "Docs/ModelReuseTestGuide.md", "Docs/AcceptanceRecord.md",
    "Docs/ExperimentGuide.md", "Docs/ProjectLog.md", "Docs/EvidenceConsoleGuide.md")]
sources += [dest / "EvidenceVR-Stage3-dev6.apk", dest / "adaptive-training-config.json"]
sources += [dest / "README.txt"]
manifest = {"application": "3.0.0-stage3-dev6", "androidCode": 30, "revision": "engineering-model-reuse-v1",
            "includesEnvironment": False, "includesMedia": False, "includesParticipantData": False,
            "physicalQuestProAcceptance": False, "files": []}
for path in sources:
    manifest["files"].append({"path": path.relative_to(root).as_posix(), "bytes": path.stat().st_size,
        "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
(dest / "package-manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
with zipfile.ZipFile(dest / "EvidenceVR-ModelReuse-dev6.zip", "w", zipfile.ZIP_DEFLATED) as archive:
    for path in sources: archive.write(path, path.relative_to(root))
    archive.write(dest / "package-manifest.json", "Builds/Stage3Dev6/package-manifest.json")
print(json.dumps({"zip": str(dest / "EvidenceVR-ModelReuse-dev6.zip"), "files": len(sources)}, indent=2))
