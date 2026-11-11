"""Read-only raw EEG and transfer diagnostics; exploratory, no production changes."""
import hashlib
import html
import json
from pathlib import Path
import sys
import collections
import numpy as np
from scipy import signal
from sklearn.ensemble import RandomForestClassifier
from sklearn.metrics import roc_auc_score
import joblib
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT/"Tools/AdaptiveTraining"))
import workload_rf as w
from personalized_signals import sampled_window

OUT = ROOT/"Evidence/P002RawEEGComparison-20261110"
SIDS = ["20261110T152940_293bba07749d", "20261110T155340_7eaf2c6e62ae"]
SPECTRA = [ROOT/"Evidence/AlphaPeakRepair-20261110/after/training-spectra.npz",
           ROOT/"Evidence/CalibrationComparison-20261110/latest-replay/training-spectra.npz"]
LABELS = w.LABELS


def jsonlines(path):
    return [json.loads(line) for line in path.read_text(encoding="utf-8-sig").splitlines()]


def auc(y, score):
    return float(roc_auc_score(y,score))


def extract(z, variant="absolute"):
    windows=[]; targets=[]; slots=[]
    for y,stage in enumerate(w.STAGES):
        for slot,v in enumerate(z[stage]):
            if not np.isfinite(v).all(): continue
            x=w.features(v,10)
            if variant=="relative_2_15": x=x/np.maximum(v[:,2:16,:].sum(axis=1),1e-12)
            if variant=="without_p4": x=x[:,:7]
            if variant=="window_average": x=x.mean(axis=0,keepdims=True)
            windows.append(x);targets.append(y);slots.append(slot)
    return windows,np.array(targets),np.array(slots)


def fit_predict(train,y,test):
    rf=RandomForestClassifier(n_estimators=100,max_depth=4,class_weight="balanced",random_state=132687081,n_jobs=1)
    rf.fit(np.concatenate(train),np.repeat(y,[len(v) for v in train]))
    return [float(rf.predict_proba(v)[:,1].mean()) for v in test]


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    allmeta=[]; psds={}; rawstats={}; zlist=[]; drift=[]
    for n,(sid,specpath) in enumerate(zip(SIDS,SPECTRA)):
        folder=Path("D:/ExperimentData/PatentDemo/P002/sessions")/sid
        rawpath=folder/"service/raw_signals.jsonl"; rawbytes=rawpath.read_bytes()
        raw=[json.loads(s) for s in rawbytes.splitlines()]
        t=np.concatenate([r["corrected_monotonic"] for r in raw]); x=np.concatenate([r["samples"] for r in raw])
        packets=[r["packet"] for r in jsonlines(folder/"service/telemetry.jsonl")]
        events=jsonlines(folder/"unity/events.jsonl")
        with np.load(specpath) as z: data={stage:z[stage].copy() for stage in w.STAGES}
        zlist.append(data)
        onsets={e["detail"]:e["actual"] for e in events if e["type"]=="protocol_stage" and e["detail"] in w.STAGES}
        attempts=collections.defaultdict(list)
        for e in events:
            if e["type"]=="calibration_math_result":
                detail=dict(v.split("=",1) for v in e["detail"].split(";"));attempts[detail["attempt"]].append(detail)
        behavior={}
        for attempt,rows in attempts.items():
            rt=[float(r["rt_seconds"]) for r in rows if r.get("rt_seconds")]
            behavior[attempt]=dict(questions=len(rows),correct=sum(r["correct"]=="true" for r in rows),
                                  omitted=sum(r["response"]=="omitted" for r in rows),
                                  interrupted=sum(r["response"]=="interrupted" for r in rows),
                                  medianResponseSeconds=float(np.median(rt)) if rt else None,
                                  responsesUnder300ms=sum(v<.3 for v in rt))
        meta=dict(session=sid,stream=raw[0]["stream"],onsets=onsets,behavior=behavior,
                  interruptions=[e for e in events if e["type"] in ("pause","resume","personal_mount_interrupted")],
                  rawSha256=hashlib.sha256(rawbytes).hexdigest(),rawSamples=len(t),stages={})
        for stage,onset in onsets.items():
            relevant=[p for p in packets if p["stage"]==stage and onset<=p["now"]<onset+180]
            offset=float(np.median([p["clockOffset"] for p in relevant]))
            selected=(t>=onset+offset)&(t<onset+offset+180);ts=t[selected]; xs=x[selected]
            powers=[]; metrics=[]; invalid=0
            for second in range(180):
                v,reason=sampled_window(t-offset,x,onset+second,onset+second+1,250)
                if v is None: invalid+=1;continue
                v=v[:250]
                _,p=signal.periodogram(v,fs=250,window="hann",axis=0)
                powers.append(p)
                metrics.append(dict(second=second,rawStd=np.std(v,axis=0),
                                    rawMedian=np.median(v,axis=0),p2p=np.ptp(v,axis=0),
                                    jump1000=(np.abs(np.diff(v,axis=0))>1000).any(axis=0),
                                    flat=(np.std(v,axis=0)<.01)))
            p=np.array(powers);psds[n,stage]=np.median(p,axis=0)
            stage_stats=[]
            for ch,label in enumerate(LABELS):
                line=p[:,49:52,ch].sum(axis=1);broad=p[:,2:31,ch].sum(axis=1)
                stat=dict(channel=label,medianRawStd=float(np.median([m["rawStd"][ch] for m in metrics])),
                          medianRawOffset=float(np.median([m["rawMedian"][ch] for m in metrics])),
                          medianPeakToPeak=float(np.median([m["p2p"][ch] for m in metrics])),
                          jumpEpochFraction=float(np.mean([m["jump1000"][ch] for m in metrics])),
                          flatEpochFraction=float(np.mean([m["flat"][ch] for m in metrics])),
                          median49to51Power=float(np.median(line)),median2to30Power=float(np.median(broad)),
                          medianLineToBroadRatio=float(np.median(line/np.maximum(broad,1e-12))),
                          medianBeta13to30Power=float(np.median(p[:,13:31,ch].sum(axis=1))))
                stage_stats.append(stat)
            rawstats[n,stage]=stage_stats
            meta["stages"][stage]=dict(samples=len(ts),usableOneSecondEpochs=len(metrics),invalidOneSecondEpochs=invalid,
                                      maxSampleGapSeconds=float(np.diff(ts).max()),
                                      gapsOver10ms=int(sum(np.diff(ts)>.01)),
                                      clockUncertaintyMedian=float(np.median([p["clockUncertainty"] for p in relevant])),
                                      clockUncertaintyMax=float(max(p["clockUncertainty"] for p in relevant)),
                                      channelMetrics=stage_stats)
        meta["rawUnchanged"]=hashlib.sha256(rawpath.read_bytes()).hexdigest()==meta["rawSha256"]
        allmeta.append(meta)
        bystage={}
        for stage in w.STAGES:
            a=[];folds=[]
            for i,v in enumerate(data[stage]):
                if np.isfinite(v).all(): a.append(w.features(v,10).mean(axis=0));folds.append(i//12)
            a=np.array(a);folds=np.array(folds)
            bystage[stage]=dict(median=np.median(a,axis=0).tolist(),
                               foldMedians=[np.median(a[folds==f],axis=0).tolist() for f in range(3)])
        rest=np.array(bystage["load_rest"]["median"]);math=np.array(bystage["load_math"]["median"])
        drift.append(dict(session=sid,stageFeatures=bystage,mathRestRatio=(math/rest).tolist()))
    evaluation=[]
    for variant in ("absolute","relative_2_15","without_p4","window_average"):
        datasets=[extract(z,variant) for z in zlist]
        for source in (0,1):
            windows,y,slots=datasets[source];fold_auc=[]
            for fold in range(3):
                train=[v for v,s in zip(windows,slots) if s//12!=fold]
                test=[v for v,s in zip(windows,slots) if s//12==fold]
                score=fit_predict(train,y[slots//12!=fold],test)
                fold_auc.append(auc(y[slots//12==fold],score))
            other,other_y,other_slots=datasets[1-source]
            transfer=fit_predict(windows,y,other)
            evaluation.append(dict(variant=variant,sourceSession=SIDS[source],targetSession=SIDS[1-source],
                                   withinFoldAuc=fold_auc,withinMeanAuc=float(np.mean(fold_auc)),
                                   crossSessionAuc=auc(other_y,transfer)))
    # Pre-task target-rest alignment: consumes first 60 s of target rest, excluded from evaluation.
    alignment=[]
    datasets=[extract(z) for z in zlist]
    for source in (0,1):
        train,y,_=datasets[source];target,ty,slots=datasets[1-source]
        source_base=np.median(np.concatenate([v for v,label in zip(train,y) if label==0]),axis=0)
        target_base=np.median(np.concatenate([v for v,label,s in zip(target,ty,slots) if label==0 and s<12]),axis=0)
        mask=~((ty==0)&(slots<12));test=[v for v,keep in zip(target,mask) if keep]
        raw=fit_predict(train,y,test)
        adjusted=fit_predict([v/source_base for v in train],y,[v/target_base for v in test])
        alignment.append(dict(sourceSession=SIDS[source],targetSession=SIDS[1-source],
                              targetRestSecondsConsumed=60,targetEvaluatedWindows=int(sum(mask)),
                              rawSameSubsetAuc=auc(ty[mask],raw),restAlignedAuc=auc(ty[mask],adjusted)))
    saved=joblib.load(Path("D:/ExperimentData/PatentDemo/P002/models")/(SIDS[0]+"-bands-v1")/"workload_rf.joblib")
    importance=dict(zip(w.FEATURES,map(float,saved["forest"].feature_importances_)))
    report=dict(purpose="Exploratory same-person two-recording raw EEG comparison; no production changes",
                sessions=allmeta,features=drift,modelFeatureImportance=importance,
                exploratoryVariants=evaluation,preTaskRestAlignment=alignment,
                uncertainty="No impedance/contact or independent numeric-unit evidence; no causal attribution from two fixed-order recordings.",
                userInterruptionDescription="Other interruption; no more specific cause established.")
    (OUT/"analysis.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
    # Diagnostic plots use the recorded numeric scale; no independent unit verification is implied.
    fig,axs=plt.subplots(2,2,figsize=(12,7),layout="constrained")
    for n in (0,1):
        for k,stage in enumerate(w.STAGES):
            ax=axs[n,k]
            for ch,label in enumerate(LABELS):ax.semilogy(np.arange(126),psds[n,stage][:,ch],label=label,lw=1)
            ax.set(xlim=(2,65),ylim=(.001,1e7),title=f"Recording {n+1} / {stage}",xlabel="Hz",ylabel="Median one-second PSD (configured units)");ax.axvline(50,color="black",ls=":",alpha=.4)
    axs[0,1].legend(ncol=4,fontsize=8);fig.savefig(OUT/"raw-spectra.png",dpi=150);plt.close(fig)
    fig,ax=plt.subplots(figsize=(10,4),layout="constrained")
    xx=np.arange(8)
    for n in (0,1):ax.bar(xx+(n-.5)*.35,np.log2(drift[n]["mathRestRatio"]),.35,label=f"Recording {n+1}")
    ax.axhline(0,color="black",lw=.8);ax.set(xticks=xx,xticklabels=LABELS,ylabel="log2(Calculate / Rest median power)",title="Fixed-band model features: condition contrasts");ax.legend();fig.savefig(OUT/"feature-contrasts.png",dpi=150);plt.close(fig)
    fig,axs=plt.subplots(2,1,figsize=(11,6),layout="constrained")
    for n in (0,1):
        for ch in (0,5,6,7):
            yy=[]
            for stage in w.STAGES:
                yy.extend([float(np.log10(w.features(v,10).mean(axis=0)[ch]+1e-12)) if np.isfinite(v).all() else np.nan for v in zlist[n][stage]])
            axs[n].plot(np.arange(72)*5,yy,label=w.FEATURES[ch])
        axs[n].axvline(180,color="black",ls="--");axs[n].set(title=f"Recording {n+1}: Rest then final Calculate attempt",ylabel="log10 band power",ylim=(0,3.4),xlabel="Window slot x 5 (s; not a wall-clock timeline)")
    axs[0].legend(ncol=4,fontsize=8);fig.savefig(OUT/"feature-timeline.png",dpi=150);plt.close(fig)
    concise=dict(behavior=[m["behavior"] for m in allmeta],rawStageMetrics=[{s:{k:v for k,v in m["stages"][s].items() if k!='channelMetrics'} for s in w.STAGES} for m in allmeta],
                 featureRatios=[r["mathRestRatio"] for r in drift],importance=importance,evaluation=evaluation,alignment=alignment,
                 p4=[dict(recording=n+1,stage=s,**rawstats[n,s][7]) for n in (0,1) for s in w.STAGES])
    print(json.dumps(concise,indent=2))


if __name__=="__main__":main()
