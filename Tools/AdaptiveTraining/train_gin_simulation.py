"""Fit an unvalidated simulation model using only current quality-qualified Gin features.

Replay signals and generated responses are paired for engineering tests, never admission.
"""
import argparse
import copy
from datetime import datetime, timezone
import json
from pathlib import Path
from types import SimpleNamespace
import xml.etree.ElementTree as ET

import joblib
import numpy as np

from personalized_model import VERSION, forest, label_fit, label_rows, policy_config, train_personal
from personalized_signals import FEATURES, REVISION as FEATURE_REVISION, sampled_window, eeg_features, ecg_features
from test_session_store import TestSessionStore, read, write, digest


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument("--summary",type=Path,default=Path("Evidence/GinModelTest/trained-model.json"))
    p.add_argument("--capture",type=Path,required=True)
    p.add_argument("--responses",type=Path,required=True)
    p.add_argument("--storage",type=Path,default=Path("D:/ExperimentData/PatentDemo"))
    p.add_argument("--state",type=Path,default=Path("Evidence/GinModelTest/offline-registry"))
    args=p.parse_args()
    capture=read(args.capture/"capture.json")
    if digest(args.capture/"raw-replay.npz")!=capture["sha256"]:raise ValueError("Capture hash mismatch")
    xml=ET.fromstring((args.capture/"source.xml").read_text(encoding="utf-8"))
    source=copy.deepcopy(capture["selection"])
    if source["name"]!="Gin_mock_p007_test" or xml.findtext("./desc/xdf_replay/recorded_data")!="true":
        raise ValueError("Expected selected Gin replay")
    units=[c.findtext("unit","").strip() for c in xml.findall("./desc/channels/channel")]
    if len(units)!=9 or any(u!="uV" for u in units[1:]):raise ValueError("Expected verified uV payload layout")
    labels=[c.findtext("label","") for c in xml.findall("./desc/channels/channel")]
    streams=[]
    for kind,channels in (("EEG",[1,2,3,4]),("ECG",[7,8])):
        streams.append(dict(source,kind=kind,channels=channels,differential=kind=="ECG",
            scale_to_units=1,amplitude_limit=300 if kind=="EEG" else 5000,
            window_seconds=12 if kind=="EEG" else 30,hrv_window_seconds=60,
            channel_labels=labels,units="uV",synthetic=True,data_origin="recorded_xdf_replay"))
    policy=policy_config(dict(theta_channels=[0,1,2,3],alpha_channels=[0,1,2,3],eeg_units_confirmed=True,
        electrode_description="Gin recorded Hyperscanner stream: XDF columns 1-4 uV; ECG column 7 minus 8, retained project montage; anatomical sites unconfirmed; simulation only; v3 quality-qualified feature extraction"))
    store=TestSessionStore(args.state,policy);store.streams=streams
    r=store.prepare(dict(participantCode="SIM_GIN_TEST",storageFolder=str(args.storage.resolve()),sessionMode="calibration",calibrationBlocks=8))
    sid=r["sessionId"];folder=Path(r["folder"])/"service";folder.mkdir()
    data=np.load(args.capture/"raw-replay.npz");t=data["timestamps"];x=data["samples"].astype(float)
    rate=source["sample_rate"];origin=float(t[0]);windows=read(args.responses/"behavior-windows.json")
    write(folder/"simulated-trials.json",read(args.responses/"simulated-trials.json"))
    rows=[]
    for w in windows:
        start=w["behavior"]["start"];end=w["behavior"]["end"]
        sample,reason=sampled_window(t-origin,x,start,end,rate)
        row=dict(w,start=start,end=end,stage="calibration",origin="recorded_replay_with_synthetic_behavior",
            valid=False,features=[],reason=reason,featureRevision=FEATURE_REVISION)
        if sample is not None:
            eeg=sample[:,1:5];ecg=sample[:,7]-sample[:,8]
            ev,eq=eeg_features(eeg,rate,policy["theta_channels"],policy["alpha_channels"])
            cv,cq=ecg_features(ecg,rate)
            row["quality"]={"eeg":eq,"ecg":cq}
            # Exposure is generated protocol context, not observed original movie playback.
            exposure=.5 if w["level"] else 0.
            row["simulatedMovieFraction"]=exposure
            row["valid"]=eq["valid"] and cq["valid"]
            row["reason"]="ok" if row["valid"] else "signal_quality"
            if row["valid"]:row["features"]=[ev[FEATURES[0]],ev[FEATURES[1]],cv[FEATURES[2]],exposure]
        rows.append(row)
    write(folder/"training_rows_1.json",rows)
    attempt=folder/"model_attempt_1"
    model,report=train_personal(rows,"SIM_GIN_TEST",sid,"synthetic",20261103,policy,attempt)
    if model is None:raise ValueError("No quality-qualified test estimator; no diagnostic fallback")
    if report["admitted"]:raise AssertionError("Synthetic pairing must never be admitted")
    report.update(trainingMethod="Quality-qualified v3 features; standard personal training pipeline",
        sourceCapture=capture["sha256"],responseFile=str((args.responses/"simulated-trials.json").resolve()),
        responseSha256=digest(args.responses/"simulated-trials.json"),
        sourceFile=xml.findtext("./desc/xdf_replay/source_file"),syntheticExposure=True,
        timestampAlignment="Source time minus first captured timestamp, paired with generated task clock; not observed behavior",
        heldOutValidityEstablished=False)
    report["reasons"].append("synthetic_behavior_not_observed_responses")
    artifact=attempt/"unvalidated_candidate.joblib"
    report.pop("model_sha256",None)
    joblib.dump(dict(model=model,report=report,participant="SIM_GIN_TEST",session=sid,
        features=FEATURES,thresholds=policy["thresholds"]),artifact)
    report["model_sha256"]=digest(artifact)
    write(attempt/"model_report.json",report)
    usable=[w for w in rows if w["valid"]]
    y=label_rows(usable,label_fit(usable,policy),policy)
    chosen=[w for w,label in zip(usable,y) if label>=0];y=y[y>=0]
    matrix=np.asarray([w["features"] for w in chosen])
    store.publish(SimpleNamespace(sid=sid,participant="SIM_GIN_TEST",folder=folder,policy=policy),1)
    store.complete(sid,"simulation_training_complete")
    model_id=sid+"-1"
    saved,manifest=store.load_model(args.storage,"SIM_GIN_TEST",model_id,True)
    np.testing.assert_array_equal(model.predict_proba(matrix),saved["model"].predict_proba(matrix))
    write(folder/"quality-qualified-predictions.json",[dict(block=w["block"],trial=w["trial"],
        syntheticLabel=int(label),classifierScore=float(score),strictQualityEligible=bool(w["valid"]))
        for w,label,score in zip(chosen,y,saved["model"].predict_proba(matrix)[:,1])])
    write(folder/"lsl-gin-mapping.json",{"streams":streams})
    write(folder/"gin-policy.json",policy)
    summary=dict(participantCode="SIM_GIN_TEST",modelId=model_id,sourceSession=sid,storageFolder=str(args.storage.resolve()),
        folder=str(folder.resolve()),modelPath=str((args.storage/"SIM_GIN_TEST/models"/model_id/"model.joblib").resolve()),
        mapping=str((folder/"lsl-gin-mapping.json").resolve()),policy=str((folder/"gin-policy.json").resolve()),
        reloadPredictionsIdentical=True,trainedUtc=datetime.now(timezone.utc).isoformat(),
        strictEligibleWindows=report["eligible_windows"],trainingWindows=len(chosen),classCounts=report["class_counts"],
        featureRevision=FEATURE_REVISION,
        admitted=False,captureFolder=str(args.capture.resolve()))
    write(args.summary,summary)
    write(folder/"simulation-training.json",summary)
    print(json.dumps(summary,indent=2))


if __name__=="__main__":main()
