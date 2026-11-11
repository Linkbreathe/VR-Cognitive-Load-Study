"""Offline sensitivity comparison; no model publication or production policy changes."""
import json
from pathlib import Path
import sys
from unittest.mock import patch
import joblib
import numpy as np
from sklearn.metrics import roc_auc_score

sys.path.insert(0, str(Path(__file__).resolve().parents[1]/"Tools"/"AdaptiveTraining"))
import workload_rf as w


def main():
    root = Path(__file__).resolve().parents[1]
    sources = {
        "152940": root/"Evidence/AlphaPeakRepair-20261110/after/training-spectra.npz",
        "155340": root/"Evidence/CalibrationComparison-20261110/latest-replay/training-spectra.npz",
    }
    rows = {}
    for name, path in sources.items():
        with np.load(path) as spectra:
            rows[name] = {s:[v if np.isfinite(v).all() else None for v in spectra[s]] for s in w.STAGES}
    results = []
    choose = w.select_bands
    for name, data in rows.items():
        for center in (8,10):
            def fixed(rest):
                profile = choose(rest)
                return dict(profile, mode="offline_fixed_band_sensitivity", detectedIafHz=None,
                            bandCenterHz=center, thetaHz=[center-6,center-2], alphaHz=[center-2,center+2])
            for seed in (132687081,116689512):
                with patch.object(w, "select_bands", side_effect=fixed):
                    _, reason, report = w.fit(data, seed)
                results.append(dict(recording=name, bandCenterHz=center, seed=seed,
                                    meanHeldAuc=report.get("meanHeldAuc"), reason=reason,
                                    folds=[f["auc"] for f in report["folds"]]))
    saved = Path("D:/ExperimentData/PatentDemo/P002/models/20261110T152940_293bba07749d-bands-v1/workload_rf.joblib")
    model = joblib.load(saved)
    scores = [[w.score(model,v) for v in rows["155340"][s] if v is not None] for s in w.STAGES]
    cross = dict(model=str(saved), targetRecording="155340", refitted=False,
                 windows=list(map(len,scores)),
                 pooledAuc=float(roc_auc_score(np.repeat([0,1],list(map(len,scores))),np.concatenate(scores))),
                 restMedian=float(np.median(scores[0])), mathMedian=float(np.median(scores[1])))
    output = dict(purpose="Post-hoc engineering diagnosis, not model selection or independent protocol validation",
                  productionChanges=False, sensitivity=results, frozenModelTransfer=cross)
    (root/"Evidence/CalibrationComparison-20261110/comparison.json").write_text(json.dumps(output,indent=2),encoding="utf-8")
    print(json.dumps(output,indent=2))


if __name__ == "__main__":
    main()
