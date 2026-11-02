"""Descriptive pre-calibration and transfer summaries; no efficacy or significance claims."""
import argparse
import csv
import json
from pathlib import Path
from statistics import mean, median


def summarize(folder):
    with (folder/"training_blocks.csv").open(encoding="utf-8-sig",newline="") as f:rows=list(csv.DictReader(f))
    valid=[r for r in rows if r["valid"].lower() in ("true","1")]
    grouped={}
    for stage in ("baseline","calibration","pretest","training","posttest"):
        for level in range(3):
            group=[r for r in valid if r["stage"]==stage and int(r["level"])==level]
            if group:grouped[f"{stage}_level_{level}"]={"blocks":len(group),"balanced_accuracy":mean(float(r["balanced_accuracy"]) for r in group),
                "median_correct_rt_s":mean(float(r["median_correct_rt_s"]) for r in group),"omissions":sum(int(r["omissions"]) for r in group)}
    baseline=grouped.get("baseline_level_0")
    changes={}
    if baseline:
        for level in (1,2):
            pre=grouped.get(f"pretest_level_{level}");post=grouped.get(f"posttest_level_{level}")
            if pre and post:changes[str(level)]={"pre_accuracy_cost":baseline["balanced_accuracy"]-pre["balanced_accuracy"],
                "post_accuracy_cost":baseline["balanced_accuracy"]-post["balanced_accuracy"],
                "accuracy_cost_reduction":post["balanced_accuracy"]-pre["balanced_accuracy"],
                "rt_change_s":post["median_correct_rt_s"]-pre["median_correct_rt_s"],"omission_change":post["omissions"]-pre["omissions"]}
    per_level={}
    trial_path=folder/"trials.csv"
    if trial_path.exists():
        with trial_path.open(encoding="utf-8-sig",newline="") as f:trials=list(csv.DictReader(f))
        good_blocks={r["block"] for r in valid}
        for level in range(3):
            selected=[t for t in trials if t.get("training_stage")=="training" and t.get("level")==str(level)
                      and t["block"] in good_blocks and t["scored"]=="1" and all(t.get(k,"0")=="0" for k in ("transition","interrupted","aborted"))]
            m=[t for t in selected if t["expected_match"]=="1"];n=[t for t in selected if t["expected_match"]=="0"]
            rt=[float(t["rt_s"]) for t in selected if t["correct"]=="1" and t["responded"]=="1"]
            if selected:per_level[str(level)]={"trials":len(selected),"matches":len(m),"nonmatches":len(n),
                "balanced_accuracy":.5*(sum(t["correct"]=="1" for t in m)/len(m)+sum(t["correct"]=="1" for t in n)/len(n)) if m and n else None,
                "median_correct_rt_s":median(rt) if rt else None,"omissions":sum(t["responded"]=="0" for t in selected)}
    result={"session":folder.name,"valid_blocks":len(valid),"excluded_blocks":len(rows)-len(valid),"conditions":grouped,
        "mixed_level_statistics_blocks":sum(int(r["level"])==-1 for r in rows),"training_trial_metrics_by_actual_level":per_level,
        "transfer_descriptive":changes,"interpretation":"Engineering/exploratory summary only. Practice, fatigue, clip differences and order are not controlled causal effects."}
    (folder/"stage2-summary.json").write_text(json.dumps(result,indent=2),encoding="utf-8")
    return result


if __name__=="__main__":
    p=argparse.ArgumentParser();p.add_argument("session",type=Path);a=p.parse_args();print(json.dumps(summarize(a.session),indent=2))
