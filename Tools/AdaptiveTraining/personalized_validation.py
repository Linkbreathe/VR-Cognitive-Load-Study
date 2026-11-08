"""Block-held-out scores and a dependence-preserving, provisional circular-shift null."""
from __future__ import annotations

import itertools
import math

import numpy as np


COLUMNS = {"combined": [0, 1, 2], "physiology_only": [0, 1],
           "exposure_only": [2], "condition_only": [3], "time_only": [4, 5]}


def block_validation(rows, p, seed, forest, metrics, comparators=True):
    groups = sorted({r["block"] for r in rows})
    columns = COLUMNS if comparators else {"combined": COLUMNS["combined"]}
    folds, oof = [], []
    for block in groups:
        train = [r for r in rows if r["block"] != block and r["valid"] and r["label"] >= 0]
        test = [r for r in rows if r["block"] == block and r["valid"] and r["label"] >= 0]
        held = {b for r in rows if r["block"] == block for b in r.get("source_blocks", [block])}
        train = [r for r in train if held.isdisjoint(r.get("source_blocks",[r["block"]])) and held.isdisjoint(r.get("permuted_label_blocks", []))]
        fold = {"block": block, "held_source_blocks": sorted(held), "train_blocks": sorted({r["block"] for r in train}),
                "train_n": len(train), "test_n": len(test), "status": "unusable"}
        folds.append(fold)
        if len({r["label"] for r in train}) != 2 or not test:
            continue
        def inputs(selected):
            return np.array([r["features"] + [r["level"], r["trial"] // 12, r["trial"] % 12] for r in selected])
        xt, xv = inputs(train), inputs(test)
        predictions = {}
        for name, cols in columns.items():
            estimator = forest(p, seed + block)
            estimator.fit(xt[:, cols], [r["label"] for r in train])
            predictions[name] = estimator.predict_proba(xv[:, cols])[:, 1].tolist()
        fold["status"] = "evaluated"
        for i, row in enumerate(test):
            oof.append({"block": block, "trial": row["trial"], "level": row["level"], "label": row["label"], "mixed": len(set(row.get("levels", [row["level"]])))>1,
                        **{name: values[i] for name, values in predictions.items()}})
    within = {str(level): metrics([r["label"] for r in oof if r["level"] == level],
        [r["combined"] for r in oof if r["level"] == level]) for level in (0, 1)}
    complete = bool(folds) and all(f["status"] == "evaluated" for f in folds)
    aucs = [within[str(level)]["roc_auc"] for level in (0, 1)]
    statistic = float(np.mean(aucs)) if complete and all(a is not None for a in aucs) else None
    return {"folds": folds, "oof_predictions": oof, "within_condition": within,
            "metrics": {name: metrics([r["label"] for r in oof], [r[name] for r in oof]) for name in columns},
            "permutation_statistic": statistic, "complete": complete}


def circular_permutation_test(rows, observed, p, seed, forest, metrics):
    """Shift each entire label trajectory, including its buffer labels, within its own block.

    The quality mask, features, condition, block and trial positions stay fixed. Every null
    draw refits all held-out-block models. Circular stationarity is an assumption requiring
    pilot qualification; this does not make overlapping windows independent.
    """
    groups = {b: sorted([r for r in rows if r["block"] == b], key=lambda r: r["trial"])
              for b in sorted({r["block"] for r in rows})}
    sizes = [len(v) for v in groups.values()]
    report = {"method": p["permutation_method"], "statistic": "mean_L0_L1_held_out_AUC",
        "observed": observed, "alpha": p["permutation_alpha"], "seed": seed,
        "requested": p["permutation_count"], "null_scores": [], "shifts": [], "unusable_draws": 0,
        "p_value": None, "passed": False, "status": "insufficient_validation",
        "refit_each_draw": True, "individual_window_shuffle": False,
        "assumption": "Within each block the label trajectory is exchangeable under circular shifts.",
        "exchangeability_qualified": p["permutation_exchangeability_qualified"]}
    if observed is None or not sizes or any(n < 2 for n in sizes):
        return report
    if all(len({r["label"] for r in block_rows}) == 1 for block_rows in groups.values()):
        report.update(status="invariant_label_trajectories", p_value=1.)
        return report
    space = math.prod(sizes)
    report["index_transformation_space"] = space
    target = min(p["permutation_count"], space - 1)
    if target < 199:
        report["status"] = "insufficient_transformations"
        return report
    # Sample without replacement from all non-identity shifts, including zero for individual blocks.
    rng = np.random.default_rng(seed)
    if space <= target + 1:
        draws = [x for x in itertools.product(*(range(n) for n in sizes)) if any(x)]
    else:
        unique, draws = set(), []
        while len(draws) < target:
            shift = tuple(int(rng.integers(n)) for n in sizes)
            if any(shift) and shift not in unique:
                unique.add(shift); draws.append(shift)
    for shifts in draws:
        permuted = []
        for block_rows, shift in zip(groups.values(), shifts):
            labels = np.roll([r["label"] for r in block_rows], shift)
            permuted.extend(dict(r, label=int(label), permuted_label_blocks=block_rows[(i-shift)%len(block_rows)].get("source_blocks",[r["block"]]))
                            for i,(r,label) in enumerate(zip(block_rows, labels)))
        validation = block_validation(permuted, p, seed, forest, metrics, comparators=False)
        score = validation["permutation_statistic"]
        if score is None:
            # Never discard unfavorable/incomplete draws to manufacture a small tail probability.
            score = 1.; report["unusable_draws"] += 1
        report["null_scores"].append(score)
        report["shifts"].append(list(shifts))
    exceed = sum(score >= observed - 1e-12 for score in report["null_scores"])
    report.update(p_value=(exceed + 1) / (len(draws) + 1), completed=len(draws),
                  status="completed", resolution=1 / (len(draws) + 1), block_order=list(groups))
    report["passed"] = report["p_value"] <= p["permutation_alpha"]
    return report
