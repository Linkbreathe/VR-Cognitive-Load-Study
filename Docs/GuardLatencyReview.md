# Guard latency benchmark

Date: 2026-11-05. Engineering measurement only; no controller or Guard policy changes.

The user requested measured Guard review time before redesigning sliding-window decisions. `Tools/AdaptiveTraining/benchmark_guard_latency.py` times the production `EvidenceGuard.review` and `GuardAdapter.receive` separately using `perf_counter_ns`. Both include their real local JSONL audit writes. Writes use normal OS buffering, not fsync durability. The adapter measurement includes its final host-decision log, which the internal `adapter_latency_ms` field does not fully include.

Fixtures cover observe/enforce, valid ON, missing-eye DEFER, OFF, and valid ON with 95 trials of historical evidence. Normal fixtures contain 25 trials and 1,000 synthetic gaze intervals; late fixtures contain 95 trials and 3,800 gaze intervals. Each case has 20 first-ingestion measurements and 300 pre-ingested measurements after 20 warmups. Requests have unique IDs, and exactly one actual review per timed call is asserted, excluding cached retry shortcuts. Expected verdicts and actions are checked on every call. Evidence preparation and adapter construction are outside the timer.

The inputs are synthetic behavior-based proposals with no trained model object. Acquisition delay, feature extraction, primary RF prediction, the adapter's optional diagnostic counterfactual RF inference, HTTP, Unity and physical display latency are excluded. The benchmark does not establish performance for all signal/media relation histories, a real RF-backed integration, simultaneous load, a Quest network or hard realtime deadlines. First-ingestion tests deliberately deliver accumulated evidence all at once; regular telemetry normally ingests it earlier. Tests use temporary isolated session directories and do not touch live assignments or participant data.

Full results and machine/source hashes: `Evidence/GuardLatency-20261105/summary.json`; individual samples: `samples.csv`. Run with the project Python environment:

## Measured results

All 2,560 measured calls passed verdict/action and non-cache assertions (2,400 warm calls plus 160 first-ingestion calls; 160 additional warmups discarded). Enforce results in milliseconds:

| Scenario | Core median | Core P95 | Core max | Adapter median | Adapter P95 | Adapter max |
|---|---:|---:|---:|---:|---:|---:|
| Normal ON | 8.270 | 9.857 | 159.532 | 11.127 | 13.285 | 162.833 |
| Missing eyes | 0.828 | 1.050 | 1.894 | 1.971 | 2.357 | 10.861 |
| OFF | 0.395 | 0.568 | 1.426 | 2.311 | 2.853 | 4.680 |
| ON with 95-trial history | 8.404 | 9.804 | 188.076 | 15.694 | 18.178 | 197.682 |

Observe ON core medians were 8.282/8.620 ms (normal/late); adapter medians 11.159/16.538 ms. Across all warm cases, maximum core time was 188.076 ms and maximum adapter time 211.772 ms. These scheduling/I/O-inclusive outliers were retained; causes were not profiled. No warm adapter call exceeded 500 ms, which is an observation, not a deadline guarantee.

First ingestion is substantially more expensive: enforce normal ON adapter median 423.0 ms, late ON median 1563.8 ms. The maximum first-ingestion duration across both modes was 2058.4 ms. Do not wait until a decision deadline to ingest an entire historical gaze packet; independently measure incremental telemetry and real RF/media workloads before changing execution deadlines.

## Reproduction

```powershell
.\Tools\AdaptiveTraining\.venv\Scripts\python.exe Tools/AdaptiveTraining/benchmark_guard_latency.py --output Evidence/GuardLatency-20261105
```

Interpretation: measure the whole decision pipeline separately before promising a 500 ms next-trial deadline. A fast Guard cannot make physiological evidence arrive earlier. No changes to experiment flow, signal gates, adaptation thresholds or APKs are included.
