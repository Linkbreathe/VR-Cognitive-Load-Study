"""Render the completed read-only EEG comparison as a standalone local report."""
import html
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Evidence/P002RawEEGComparison-20261110'
r=json.loads((OUT/'analysis.json').read_text())
f=json.loads((OUT/'filter-sensitivity.json').read_text())


def table(headers,rows):
    return '<table><thead><tr>'+''.join('<th>'+html.escape(str(h))+'</th>' for h in headers)+'</tr></thead><tbody>'+''.join('<tr>'+''.join('<td>'+html.escape(str(x))+'</td>' for x in row)+'</tr>' for row in rows)+'</tbody></table>'


contrasts=table(['Fixed-band feature','Recording 1 Calculate/Rest','Recording 2 Calculate/Rest'],
                [[name,f'{r["features"][0]["mathRestRatio"][i]:.2f}x',f'{r["features"][1]["mathRestRatio"][i]:.2f}x']
                 for i,name in enumerate(['AFz theta','AF3 theta','AF4 theta','AF7 theta','AF8 theta','Pz alpha','P3 alpha','P4 alpha'])])
variants=table(['Variant','Train 1 / test 2 AUC','Train 2 / test 1 AUC'],
               [[name]+[f'{next(e for e in r["exploratoryVariants"] if e["variant"]==key and e["sourceSession"]==s["session"])["crossSessionAuc"]:.3f}' for s in r['sessions']]
                for key,name in [('absolute','Absolute band power'),('relative_2_15','Relative to same-channel 2–15 Hz power'),('without_p4','Exclude P4 feature (diagnostic only)'),('window_average','Train on five-second mean features')]])
filter_table=table(['Filter variant','Train 1 / test 2','Train 2 / test 1','Windows per recording'],
                   [[key]+[f'{next(e for e in f["evaluation"] if e["method"]==key and e["source"]==i)["crossSessionAuc"]:.3f}' for i in (1,2)]+
                    [str(next(e for e in f['evaluation'] if e['method']==key and e['source']==1)['sourceWindows'])+'/'+str(next(e for e in f['evaluation'] if e['method']==key and e['source']==1)['targetWindows'])]
                    for key in ('reconstructed','notch50','reconstructed_context_subset','context_padding')])
body=f'''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>P002 raw EEG and cross-session stability</title><style>body{{font:16px/1.6 system-ui;max-width:1120px;margin:40px auto;padding:0 24px;color:#182336;background:#fafbfd}}h1,h2{{line-height:1.25}}h2{{margin-top:38px}}table{{border-collapse:collapse;width:100%;background:white}}td,th{{padding:9px 12px;border-bottom:1px solid #d9e0e8;text-align:left}}th{{background:#eaf0f7}}img{{width:100%;background:white}}.lead{{padding:20px;background:#eaf0f7;border-left:5px solid #2864b8}}small{{color:#566274}}a{{color:#185ca6}}</style>
<h1>P002: why the two calibrations do not transfer</h1><p>2026-11-10 · exploratory engineering analysis · original records and production controller unchanged</p>
<p><b>Recording 1:</b> 20261110T152940_293bba07749d<br><b>Recording 2:</b> 20261110T155340_7eaf2c6e62ae</p>
<div class="lead"><b>The measured condition-to-feature relationship changed direction.</b> The first model learned patterns that are weak or reversed in the second recording. Strong, spatially changing 50 Hz contamination and a different task history are documented contributors to instability, but these two recordings cannot establish a single physical or physiological cause. No tested simple correction restored reliable transfer.</div>
<h2>1. What was controlled and what was reconstructed</h2>
<p>The source stream UID, channel order, 250 Hz sample rate, declared uV units, scaling factor 1.0 and acquisition configuration are identical. Unity task configuration differs only in seed and session ID. This does not independently validate the declared numeric units or electrode contact. Both original raw-file SHA-256 values are preserved in <a href="analysis.json">analysis.json</a>.</p>
<p>The production selected-window spectra were reconstructed from raw timestamps, samples and original packet offsets with maximum absolute difference <b>0.0</b> in both recordings. The comparison therefore reproduces the actual feature path. Raw one-second diagnostic plots use the median recorded clock offset for each final condition, rather than claiming sub-millisecond event alignment.</p>
<p>Recording 1 has a 6.244-second gap during Calculate; Recording 2's final Calculate has no gap exceeding 10 ms. Both retain enough training windows (36/34 and 36/33). The second failure is not explained by a larger raw-data dropout. Occasional clock-uncertainty failures still account for some omitted candidates.</p>
<h2>2. Condition contrasts reverse</h2>
<p>All comparisons here use fixed theta [4,8) Hz / alpha [8,12) Hz, so band choice cannot explain these contrasts. Values below are Calculate/Rest ratios of median five-second mean epoch powers. A value below one indicates lower power in Calculate; a value above one indicates higher power.</p>{contrasts}
<img src="feature-contrasts.png" alt="Condition contrasts reverse between recordings">
<p>The five frontal theta features decrease in Recording 1 but increase in Recording 2. Posterior alpha increases in Recording 1 but is unchanged or reduced in Recording 2. The saved first model assigns about 56% of its impurity-based feature importance to the three posterior features; this importance is descriptive, not causal evidence. Recording 2 also varies internally: AFz Calculate/Rest ratios across the three matched minute portions are 5.12, 0.48 and 1.23.</p>
<img src="feature-timeline.png" alt="Per-window feature drift"><small>The timeline uses retained-window slots, not exact wall-clock timestamps; interrupted attempts and between-stage time are not plotted. Missing slots appear as gaps.</small>
<h2>3. Raw EEG contains large, changing 50 Hz peaks</h2>
<p>In Rest, median 49–51 Hz power rises by <b>47.4x at P3</b> and <b>6.05x at P4</b> from Recording 1 to Recording 2, while frontal 50 Hz power falls. This spatial redistribution is not a uniform gain change. P4 raw one-second standard deviation rises from about 818 to 1994 in the configured uV scale; P3 rises from 189 to 1289. Values describe recorded numeric scale, not a calibrated clinical measurement.</p>
<img src="raw-spectra.png" alt="Raw EEG spectra show strong 50 Hz peaks"><p>The narrow 50 Hz peak is consistent with mains-frequency contamination. The recordings lack impedance/contact measurements, amplifier reference diagnostics and direct cable/environment observations, so an electrode, reference, cable or electrical source cannot be singled out from these files.</p>
<h2>4. Filtering has a measurable edge effect, but it is not the complete explanation</h2>
<p>The production code filters each five-second interval separately with forward/backward 2–30 Hz filtering. A synthetic pure 50 Hz sine of amplitude 2800, containing no actual theta or alpha component, creates last-epoch theta power about 590.8 and alpha power about 3.32 after this windowed procedure. A 50 Hz notch before bandpass reduces those to about 63.1 and 0.351. This demonstrates a preprocessing artifact under a controlled input; it does not establish the fraction of real EEG power attributable to that artifact.</p>
{filter_table}
<p>Notch results retain the same windows as production. Context padding uses two real seconds before and after each window; windows crossing a fold/stage boundary or missing context are excluded. Compare that variant only with <code>reconstructed_context_subset</code>, which uses exactly the same windows. Context does not cross validation folds. The padded experiment is offline and uses future context; online filtering would need an explicit latency/state contract. Neither notch nor context padding consistently restores transfer.</p>
<h2>5. Task history also changed</h2>
<p>Recording 1: 18 Calculate questions, 6 correct, 4 omitted, median response time 1.48 seconds among responses. Recording 2's final attempt: 18 questions, 9 correct, 4 omitted, median response time 3.82 seconds. Both contain two responses faster than 300 ms. These are operation/behavior differences, not proof of a particular mental state.</p>
<p>Recording 2 first attempted Calculate for about 47 seconds, then an operator pause restarted the stage. That interrupted attempt had four omissions and one interrupted question. The final training data use the repeated full attempt; the aborted partial attempt is not counted twice. The researcher described the event as another interruption; no headset/electrode adjustment has been established. Fixed Rest-then-Calculate order and the additional task exposure make time/history inseparable from the intended class contrast in this pair.</p>
<h2>6. Candidate stability improvements tested offline</h2>{variants}
<p>All variants use the same fixed bands, seed, RF size/depth and temporal partitions. The relative-power denominator is this implementation's stored 2–15 Hz spectrum; it is an exploratory normalization, not full broadband normalization. Relative power improves Recording 2's internal mean AUC to 0.725, yet transfer remains 0.515 / 0.479. Selecting it solely for the internal score would not solve the observed problem.</p>
<p>Target-rest alignment uses only the first 60 seconds of target Rest, excludes those windows from evaluation and uses no target Calculate labels. On identical remaining target windows, forward transfer changes from 0.420 to 0.426; reverse changes from 0.507 to 0.500. A simple baseline gain correction does not recover the reversed conditional relationship.</p>
<h2>7. Improvement plan, ordered by evidence</h2>
<ol><li><b>Stabilize acquisition before retuning the classifier.</b> Inspect P3/P4 contact, reference/ground, headset pressure and cable routing; record those checks and inspect the full raw spectrum, not only the 2–30 Hz view. Keep the numeric-unit verification explicit. This recommendation does not re-enable artifact rejection or silently remove training windows.</li>
<li><b>Make preprocessing consistent in calibration and deployment.</b> Develop a versioned continuous/stateful filter, or a carefully specified contextual filter, with a 50 Hz suppression option verified on synthetic and recorded signals. Quantify boundaries, delay and reset handling. Do not deploy the diagnostic future-context implementation as an online fix.</li>
<li><b>Improve reproducibility of the two calibration conditions.</b> Confirm Calculate instructions and response handling before acquisition; standardize posture, speech and environment, and log interruptions and mount changes. For a revised protocol, collect repeated alternating Rest/Calculate blocks to separate temporal drift from condition. That protocol change is proposed only.</li>
<li><b>Evaluate across whole acquisitions.</b> Collect several repeat recordings and reserve an entire later recording for testing. Compare predeclared absolute/relative/baseline-normalized features with the same partitions. Fit feature choices and normalization using training data or an explicitly separate pre-task baseline. Do not pick a band, polarity or seed to maximize the test recording.</li></ol>
<p><b>Recommended immediate decision:</b> keep the existing engineering model identifiable, but do not treat its 0.760 internal AUC as evidence of stable cross-session workload measurement. The next meaningful success criterion is repeat-acquisition transfer, after signal acquisition and preprocessing are made reproducible, rather than lowering the current admission threshold.</p>
<h2>Evidence, limitations and sources</h2>
<p>Scripts: <code>AgentScripts/analyze_p002_raw_eeg.py</code>, <code>AgentScripts/diagnose_p002_filter_edges.py</code>. Machine-readable outputs: <a href="analysis.json">raw/features/transfer</a>, <a href="filter-sensitivity.json">filter sensitivity</a>. No production service, model, threshold, session record or selection policy was changed by this analysis. Synthetic probes live only in this engineering evidence folder.</p>
<p>Two fixed-order recordings from one person cannot identify an electrode fault or causal cognitive effect, nor establish generalization. All alternative-feature results are post-hoc exploratory checks on already-inspected data, with no independent prospective validation. Reported AUCs are point estimates, not confidence-qualified population effects.</p>
<p><a href="https://www.frontiersin.org/journals/artificial-intelligence/articles/10.3389/frai.2022.992732/full">Albuquerque et al. (2022)</a> examines distribution shifts and normalization in cross-subject workload prediction; it motivates measuring shifts, but does not validate this two-session approach. <a href="https://www.nature.com/articles/s41593-020-00744-x">Donoghue et al. (2020)</a> shows why band-power differences can mix periodic and aperiodic changes; the current analysis does not equate all recorded band-power changes with neural oscillations.</p>
</html>'''
(OUT/'report.html').write_text(body,encoding='utf-8')
print(OUT/'report.html')
