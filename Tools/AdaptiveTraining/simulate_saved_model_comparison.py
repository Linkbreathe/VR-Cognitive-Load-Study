"""Isolated engineering A/B run with recorded EEG and seeded synthetic task/gaze inputs."""
import argparse
from collections import Counter
from datetime import datetime, timezone
import json
import html
from pathlib import Path
import shutil

import numpy as np

from comparison_report import report
from service import StudyService
from test_personalized import packet, trials
from test_session_store import read, write, digest


def write_report(output, provenance, outputs, comparison):
    """Create a self-contained report without exposing raw EEG through the report server."""
    report_folder = output / "report"
    report_folder.mkdir(exist_ok=True)
    rows = []
    cards = []
    reasons = {
        "current_condition_agreement_missing": "Behavior and model do not both support opening in the current condition.",
        "model_window_not_current_condition": "The model window spans a condition change.",
        "playback_score_rise_with_stable_behavior_and_task_gaze": "Score rose during playback while behavior stayed stable and gaze supported task viewing.",
        "insufficient_remaining_probe_checkpoints": "Too few trials remain to verify an opening.",
    }
    for stage, data in outputs.items():
        arm = stage[-1].upper()
        records = [c for c in comparison["checkpoints"] if c["stage"] == stage]
        stats = comparison["summary"][stage]
        total = data["trials"]
        points = [(32+(c["trial"]/total)*616,172-140*c["base"]["score"]) for c in records if c["base"].get("score") is not None]
        path = " ".join(f"{x:.1f},{y:.1f}" for x,y in points)
        lines = "".join(f'<line x1="32" x2="648" y1="{172-140*v}" y2="{172-140*v}" stroke="#a7b0b3" stroke-dasharray="5 5"/><text x="652" y="{176-140*v}" font-size="11">{v:.2f}</text>'
                        for v in provenance["thresholds"].values())
        markers = "".join(f'<circle cx="{32+c["trial"]/total*616}" cy="{172-140*c["base"]["score"]}" r="4" fill="#b53434"/>'
                          for c in records if c["base"].get("score") is not None and c["base"]["action"] in ("ON","OFF") and c["delivered"]=="HOLD")
        timeline = [t for t in comparison["trials"] if t["stage"] == stage and t["scored"]]
        eye_total = sum(t["preResponseSeconds"] for t in timeline)
        eye_valid = sum(t["validEyeSeconds"] for t in timeline)
        eye_task = sum(b-a for t in timeline for a,b,target in t["preResponse"] if target=="task")
        coverage = eye_valid/eye_total if eye_total else 0
        task_share = eye_task/eye_valid if eye_valid else 0
        on, off = data["deliveredActions"].get("ON",0), data["deliveredActions"].get("OFF",0)
        cards.append(f'''<section class="card"><h2>Round {arm} <small>{"Base controller" if arm=="A" else "Evidence review enabled"}</small></h2>
<div class="metrics"><div><strong>{on} / {off}</strong>ON / OFF executed</div><div><strong>{data["blocked"]}</strong>Proposals blocked</div><div><strong>{stats["videoSeconds"]:.1f}s</strong>Video exposure</div></div>
<p>Generated responses · {data["correct"]}/{data["scoredTrials"]} correct ({data["correct"]/data["scoredTrials"]:.1%}), {data["omissions"]} omissions.<br>
Generated gaze before response · {coverage:.1%} valid coverage, {task_share:.1%} on task among valid samples.</p>
<svg viewBox="0 0 700 205" role="img" aria-label="Round {arm} model score by checkpoint"><text x="10" y="18" font-size="12">RF score · thresholds dashed · blocked proposals red</text>
{lines}<polyline points="{path}" fill="none" stroke="#256c80" stroke-width="2.5"/>{markers}<text x="32" y="198" font-size="12">Trial 1</text><text x="592" y="198" font-size="12">Trial {total}</text></svg></section>''')
        for c in records:
            base, review = c["base"], c["review"]
            rejected = base["action"] in ("ON","OFF") and c["delivered"]=="HOLD"
            reason = review.get("reason",base["reason"])
            value = f'{base["score"]:.3f}' if base.get("score") is not None else "—"
            behavior = base.get("behavior",{})
            ba = f'{behavior["ba"]:.2f}' if behavior.get("ba") is not None else "—"
            explanation = reasons.get(reason,reason.replace("_"," "))
            if arm == "A":
                explanation = "Observed review only: " + explanation
            detail = html.escape(json.dumps(dict(base=base,review=review,receipt=c["receipt"]),indent=2))
            rows.append(f'<tr data-arm="{arm}" data-blocked="{str(rejected).lower()}"><td>{arm}</td><td>{c["trial"]}</td><td>{value}</td><td>{ba}</td><td>{base["action"]}</td><td class="{"blocked" if rejected else ""}">{c["delivered"]}</td><td><details><summary>{html.escape(explanation)}</summary><pre>{detail}</pre></details></td></tr>')
    b_reasons = "".join(f"<li>{count} · {html.escape(reasons.get(reason,reason))}</li>" for reason,count in outputs["layer_b"]["blockReasons"].items())
    highlighted = next((c for c in comparison["checkpoints"] if c["stage"]=="layer_b" and
                        c["review"].get("reason")=="playback_score_rise_with_stable_behavior_and_task_gaze"), None)
    example = ""
    source_note = f'<p>EEG source: {html.escape(Path(provenance["recording"]).name)}</p>'
    source_note += f'<p>{html.escape(provenance.get("recordingDetails", {}).get("unitAssumption", ""))}</p>'
    if provenance.get("reuseInputs"):
        previous = read(Path(provenance["reuseInputs"]) / "summary.json")
        source_note += '<p>Prior simulated responses and gaze targets/validity reused up to the new trial budget; times shifted to the new stage start and playback visibility recomputed. Prior and current trial budgets may differ, so raw counts are not normalized comparisons. Both arms restart the EEG recording at their saved offsets.</p><table><tr><th>Round</th><th>Previous ON / OFF</th><th>Current ON / OFF</th><th>Previous / current blocked</th></tr>'
        for stage, current in outputs.items():
            old = previous["arms"][stage]
            a, b = old["deliveredActions"], current["deliveredActions"]
            source_note += f'<tr><td>{stage[-1].upper()}</td><td>{a.get("ON",0)} / {a.get("OFF",0)}</td><td>{b.get("ON",0)} / {b.get("OFF",0)}</td><td>{old["blocked"]} / {current["blocked"]}</td></tr>'
        source_note += '</table>'
    if highlighted:
        review = highlighted["review"]
        example = (f'<p>At trial {highlighted["trial"]}, RF = {highlighted["base"]["score"]:.3f} proposed OFF. '
                   f'BA = {review["applicableBehavior"]["ba"]:.2f} and '
                   f'{review["playbackGaze"]["task_gaze_ratio"]:.1%} task gaze during the reviewed playback interval '
                   f'supported keeping the video on. The prior non-overlapping RF score was {review["modelRise"]["priorScore"]:.3f}.</p>')
    text = f'''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Wei_004 · Simulated A/B comparison</title>
<style>body{{margin:0;background:#f3f5f4;color:#18313b;font:16px/1.55 system-ui,sans-serif}}main{{max-width:1240px;margin:40px auto;padding:0 24px}}h1{{font-size:32px;margin-bottom:8px}}h2{{font-size:23px}}small{{display:block;font-size:14px;font-weight:400;color:#546972}}.note{{padding:18px 22px;background:#e7edf0;border-radius:12px}}.grid{{display:grid;grid-template-columns:1fr 1fr;gap:20px;margin:24px 0}}.card{{background:white;padding:24px;border:1px solid #dce3e1;border-radius:16px}}.metrics{{display:flex;gap:22px;font-size:13px}}strong{{display:block;font-size:29px}}svg{{width:100%;margin-top:10px}}table{{width:100%;border-collapse:collapse;background:white;font-size:14px}}td,th{{text-align:left;vertical-align:top;padding:11px;border-bottom:1px solid #dce3e1}}th{{background:#e7edf0}}td.blocked{{color:#a42c30;font-weight:700}}pre{{max-width:620px;max-height:340px;overflow:auto;font-size:12px;background:#f3f5f4;padding:12px}}select,label{{font:inherit;margin:8px 16px 16px 0}}summary{{cursor:pointer}}@media(max-width:800px){{.grid{{grid-template-columns:1fr}}}}</style>
<main><h1>Wei_004 · Simulated A/B comparison</h1><p>{html.escape(provenance["model"])} · ON ≤ {provenance["thresholds"]["on"]} · OFF ≥ {provenance["thresholds"]["off"]}</p>
<p class="note">Recorded Mindtooth EEG scored by the saved RF. Responses, gaze, playback and execution receipts are simulated. Each round has {outputs['layer_a']['trials']} trials and {len(outputs['layer_a']['decisions'])} decision checkpoints, including early checks with incomplete evidence. Different seeded inputs in A and B mean these outcomes do not establish an advantage of B.</p>
{source_note}
<div class="grid">{"".join(cards)}</div><section class="card"><h2>Why B blocked {outputs["layer_b"]["blocked"]} proposals</h2><ul>{b_reasons}</ul>
{example}</section>
<h2>Decision evidence</h2><label>Round <select id="arm"><option value="">Both</option><option>A</option><option>B</option></select></label><label><input type="checkbox" id="blocked"> Blocked proposals only</label>
<table><thead><tr><th>Round</th><th>Trial</th><th>RF</th><th>BA</th><th>Proposed</th><th>Delivered</th><th>Reason and evidence</th></tr></thead><tbody>{"".join(rows)}</tbody></table>
<p>Seeds A {outputs["layer_a"]["seed"]}, B {outputs["layer_b"]["seed"]}. Artifact screening off. First two trials in each round are unscored. All executed changes have simulated acknowledgements.</p></main>
<script>function filter(){{const a=document.getElementById('arm').value,b=document.getElementById('blocked').checked;for(const r of document.querySelectorAll('tbody tr'))r.hidden=!!((a&&r.dataset.arm!==a)||(b&&r.dataset.blocked!=='true'));}}document.getElementById('arm').onchange=filter;document.getElementById('blocked').onchange=filter;</script></html>'''
    (report_folder / "index.html").write_text(text,encoding="utf-8")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", type=Path, required=True)
    parser.add_argument("--recording", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--seed", type=int, default=20261107)
    parser.add_argument("--reuse-inputs", type=Path, help="Prior simulation folder supplying exact responses and gaze")
    args = parser.parse_args()
    args.output = args.output.resolve()
    args.recording = args.recording.resolve()
    if args.reuse_inputs:
        args.reuse_inputs = args.reuse_inputs.resolve()
    args.output.mkdir(parents=True, exist_ok=False)
    meta = read(args.model / "manifest.json")
    participant = meta["participantCode"]
    copy = args.output / "data" / participant / "models" / meta["modelId"]
    shutil.copytree(args.model, copy)
    times, samples = [], []
    recording_notes = {}
    if args.recording.suffix.lower() == ".csv":
        with args.recording.open(encoding="utf-8-sig") as stream:
            line = next(stream)
            metadata = {}
            while line.startswith("#"):
                key, value = line[1:].strip().split("=", 1)
                metadata[key] = value
                line = next(stream)
            labels = line.strip().split(";")[1:]
            values = np.loadtxt(stream, delimiter=";")
        source = dict(meta["signalContract"][0])
        if labels != source["channel_labels"] or float(metadata["samplingRate"]) != source["sample_rate"]:
            raise ValueError("CSV montage or sample rate differs from the model contract")
        times, samples = values[:, 0] / 1000, values[:, 1:]
        gaps = np.diff(times)
        if samples.shape[1] != 8 or not np.isfinite(values).all() or np.any(gaps <= 0) or max(gaps) > 2.5/250:
            raise ValueError("CSV has nonfinite values, invalid timestamps or incomplete sampling")
        source.update(name="archived-mindtooth-csv", source_id=args.recording.stem)
        recording_notes = dict(rows=len(values), durationSeconds=float(times[-1]-times[0]),
            sampleRate=250, channelLabels=labels, metadata=metadata,
            unitAssumption="CSV does not declare units; model uV and scale 1 assumed, not independently verified.")
    else:
        with args.recording.open() as stream:
            for line in stream:
                row = json.loads(line)
                if row["stream"]["kind"] == "EEG":
                    times.extend(row["corrected_monotonic"])
                    samples.extend(row["samples"])
                    source = row["stream"]
    times = np.asarray(times)
    samples = np.asarray(samples)
    source = dict(source, data_origin="recorded_csv_replay", synthetic=True)
    reused_rows, reused_gaze, prior = {}, {}, None
    if args.reuse_inputs:
        prior = read(args.reuse_inputs / "summary.json")
        if prior["provenance"]["forestHash"] != digest(args.model / "workload_rf.joblib"):
            raise ValueError("Reused simulation used a different forest")
        for row in read(args.reuse_inputs / "simulated-trials.json"):
            reused_rows[(row["trainingStage"], row["phaseTrial"])] = row
        files = list(args.reuse_inputs.glob("data/*/sessions/*/service/telemetry.jsonl"))
        if len(files) != 1:
            raise ValueError("Expected exactly one prior telemetry file")
        for line in files[0].read_text().splitlines():
            saved = json.loads(line)["packet"]
            if saved.get("guardGaze"):
                reused_gaze[(saved["stage"], saved["completedTrials"])] = saved["guardGaze"]
        if len(reused_rows) != sum(a["trials"] for a in prior["arms"].values()) or len(reused_gaze) != len(reused_rows):
            raise ValueError("Reused inputs must cover both complete saved rounds")
    service = StudyService(args.output / "service", personalized_policy=meta["policy"],
                           test_state_dir=args.output / "registry")
    service.test_store.streams = [source]
    assignment = service.test_store.prepare(dict(participantCode=participant,
        storageFolder=str(args.output / "data"), modelId=meta["modelId"],
        sessionMode="comparison_candidate", order="AB"))
    claim = service.test_store.claim(dict(clientId="seeded-simulator", controllerUrl="http://127.0.0.1:8786"))
    config = claim["config"]
    total = config["trainingTrials"]
    stride = config["rollingStrideTrials"]
    continuous = config["evaluationPolicy"] == "behavior5-continuous-stride2-v3"
    stage_duration = total * 4.5
    sid = claim["sessionId"]
    p = dict(packet(), sessionId=sid, participantCode=participant, sessionType="engineering",
             config=config, testClientId="seeded-simulator", now=0, trials=[], events=[],
             completedTrials=0, lastTrial=0, environmentRevision="synthetic-fixed-room")
    provenance = dict(createdUtc=datetime.now(timezone.utc).isoformat(), seed=args.seed,
        participant=participant, model=meta["modelId"], modelHash=meta["sha256"],
        forestHash=digest(args.model / "workload_rf.joblib"),
        thresholds=assignment["scoreThresholds"], evidence="recorded EEG; synthetic behavior, gaze, media and execution",
        recording=str(args.recording), recordingHash=digest(args.recording),
        epochs="10-second workload windows, artifact screening disabled",
        comparisonLimit="Different randomized arm inputs confound comparisons of performance. This exercises software behavior.")
    provenance.update(recordingDetails=recording_notes, reuseInputs=str(args.reuse_inputs) if args.reuse_inputs else None,
        evaluationPolicy=config["evaluationPolicy"], trialsPerArm=total, decisionStride=stride,
        replayMethod="Original timestamps interpolated to 250 Hz; each arm restarts the recording at its saved offset. Wrap only if recording too short.",
        gazeMethod="Saved targets and validity retained; level and movie visibility recomputed for this run." if prior else "Seeded synthetic gaze")
    if prior and provenance["thresholds"] != prior["provenance"]["thresholds"]:
        raise ValueError("Thresholds differ from the prior simulation")
    outputs = {}
    task_rows = []
    for arm, stage in enumerate(("layer_a", "layer_b")):
        rng = np.random.default_rng(args.seed + arm)
        offset = arm * (stage_duration + 180)
        level = 0
        pending = []
        completed = []
        media = None
        media_intervals = []
        slot_times = [offset + block*108 + t for block in range(6) for t in (4,31,58,85)]
        slot_index = 0
        eeg_offset = float(rng.uniform(0, 60))
        regimes = rng.choice(["focused", "variable", "distracted"], 8, p=[.55,.30,.15])
        if prior:
            eeg_offset = prior["arms"][stage]["eegReplayOffset"]
            regimes = np.asarray(prior["arms"][stage]["regimes"])
        decisions = []
        p.update(stage=stage, now=offset, level=0, trials=[], completedTrials=0, lastTrial=0,
                 closedBehavior=None, events=[dict(type="protocol_stage", actual=offset, detail=stage)])
        service.receive(p)
        for i, raw in enumerate(trials(total, stage)):
            n = i + 1
            start, close, boundary = offset+i*4.5, offset+i*4.5+4, offset+(i+1)*4.5
            events = pending
            pending = []
            if media and media["end"] <= close:
                events.append(dict(type="adaptive_media_ended", actual=media["end"], detail=media["detail"]))
                media = None
            if continuous and level == 1 and media is None:
                detail = json.dumps(dict(stage=stage,modality="audiovisual",level=1,block=i//24,onset=start,mediaId=f"continuous-{arm}-{n}"))
                media = dict(start=start,end=offset+stage_duration,detail=detail)
                media_intervals.append(media)
                events.append(dict(type="adaptive_media_started",actual=start,detail=detail+";first_decoded_frame;audio_playing=true"))
            while not continuous and slot_index < len(slot_times) and slot_times[slot_index] <= close:
                when = slot_times[slot_index]
                slot_index += 1
                if level == 1:
                    detail = json.dumps(dict(stage=stage,modality="audiovisual",level=1,
                                             block=i//24,onset=when,mediaId=f"sim-{arm}-{slot_index}"))
                    media = dict(start=when,end=when+20,detail=detail)
                    media_intervals.append(media)
                    events.append(dict(type="adaptive_media_started",actual=when,
                                       detail=detail+";first_decoded_frame;audio_playing=true"))
            regime = regimes[i//20]
            accuracy, omission, task_share, validity = {
                "focused": (.96,.01,.92,.98), "variable": (.82,.04,.70,.92),
                "distracted": (.62,.12,.42,.72)}[regime]
            responded = bool(rng.random() >= omission)
            correct = responded and bool(rng.random() < accuracy)
            rt = float(rng.uniform(.6,2.7)) if responded else 0.
            row = dict(raw,plannedOnset=start,actualOnset=start,trialEnd=boundary,
                       closeTime=close,offsetTime=close,level=level,responded=responded,correct=correct,
                       reactionTime=rt,responseTime=start+rt if responded else -1,simulationRegime=str(regime))
            gaze = []
            for k in range(40):
                a, b = start+k*.1, start+(k+1)*.1
                playing = any(m["start"] <= a < m["end"] for m in media_intervals)
                target = "task" if rng.random() < task_share else "movie" if playing and rng.random()<.8 else "other"
                gaze.append(dict(id=arm*10000+i*40+k,start=a,end=b,level=level,
                    valid=bool(rng.random()<validity),target=target,taskVisible=True,movieVisible=playing))
            if prior:
                row = dict(reused_rows[(stage, i)], level=level)
                shift = start - row["actualOnset"]
                for field in ("plannedOnset", "actualOnset", "trialEnd", "closeTime", "offsetTime"):
                    row[field] += shift
                if row["responded"]: row["responseTime"] += shift
                gaze = [dict(g, start=g["start"]+shift, end=g["end"]+shift, level=level,
                    movieVisible=any(m["start"] <= g["start"]+shift < m["end"] for m in media_intervals)) for g in reused_gaze[(stage, n)]]
            # Map original recorded EEG onto the simulation clock, cyclically for the longer run.
            t = start + np.arange(1125)/250
            origin = times[0] + (t-offset+eeg_offset) % (times[-1]-times[0]-.01)
            x = np.column_stack([np.interp(origin,times,samples[:,ch]) for ch in range(8)])
            service.record_signal(source, origin.tolist(), t.tolist(), x.tolist(),0,"archived-mindtooth")
            p.update(now=close,level=level,trials=completed[-1:],events=events,guardGaze=gaze,
                closedBehavior=dict(row,trialEnd=0),completedTrials=n,lastTrial=n,
                requestId=f"{stage}-{n}",evaluationId=n//stride,decisionDeadline=boundary)
            check = n%stride==0 and n<total
            reply = service.receive(p, decide=check)
            if service.sessions[sid]["guard"].failure:
                raise RuntimeError(service.sessions[sid]["guard"].failure)
            if check:
                decision = reply["decision"]
                base = service.sessions[sid]["personal"].decisions[p["requestId"]]
                decisions.append(dict(trial=n,base=base,delivered=decision))
                if decision["action"] in ("ON","OFF"):
                    previous = level
                    level = int(decision["action"]=="ON")
                    if previous == level:
                        raise AssertionError("Repeated state transition")
                    if not level and media:
                        media["end"] = min(media["end"],boundary)
                        pending.append(dict(type="adaptive_media_ended",actual=boundary,detail=media["detail"]))
                        media = None
                    pending.extend([
                        dict(type="personal_execution",actual=boundary,
                             detail=f"request={p['requestId']};level={level};previous_level={previous};simulation=true"),
                        dict(type="adaptive_level_changed",actual=boundary,detail=f"level={level}")])
            completed.append(row)
            task_rows.append(row)
        if media:
            pending.append(dict(type="adaptive_media_ended",actual=offset+stage_duration,detail=media["detail"]))
        p.update(now=offset+stage_duration,trials=completed[-1:],events=pending,guardGaze=[],closedBehavior=None,level=level)
        service.receive(p)
        scored = [r for r in completed if r["scored"]]
        base_actions = Counter(d["base"]["action"] for d in decisions)
        delivered = Counter(d["delivered"]["action"] for d in decisions)
        blocked = [d for d in decisions if d["base"]["action"] in ("ON","OFF") and d["delivered"]["action"]=="HOLD"]
        scores = [d["base"]["score"] for d in decisions if d["base"].get("score") is not None]
        outputs[stage] = dict(seed=args.seed+arm,regimes=regimes.tolist(),eegReplayOffset=eeg_offset,
            trials=len(completed),scoredTrials=len(scored),correct=sum(r["correct"] for r in scored),
            omissions=sum(not r["responded"] for r in scored),baseActions=dict(base_actions),
            deliveredActions=dict(delivered),blocked=len(blocked),
            blockReasons=dict(Counter(d["delivered"]["guardReason"] for d in blocked)),
            modelTriggers=sum(d["base"].get("modelTrigger",False) for d in decisions),
            scores=dict(min=min(scores),max=max(scores),mean=float(np.mean(scores))),decisions=decisions)
    folder = service.sessions[sid]["path"]
    service.test_store.complete(sid,"complete")
    comparison = report(folder)
    write(args.output/"provenance.json",provenance)
    write(args.output/"summary.json",dict(provenance=provenance,arms=outputs))
    write(args.output/"comparison.json",comparison)
    write(args.output/"simulated-trials.json",task_rows)
    write_report(args.output,provenance,outputs,comparison)
    print(json.dumps({stage:{k:v for k,v in value.items() if k!="decisions"} for stage,value in outputs.items()},indent=2))
    print("OUTPUT",args.output)


if __name__ == "__main__":
    main()
