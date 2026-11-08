# Physiological model sources

Stage 2/3 engineering revision `workload-threshold-engineering-v1`, 2026-11-05.

## Connect the vendor model

Start a calibrated participant's monitoring session in the Mindtooth Neurometrics App with AI-Based Workload enabled. Enable Stress to view it alongside Workload. Supply PARTICIPANT_ID, ORGANIZATION_EMAIL and ACTIVATION_TOKEN as environment variables to the study computer's bridge process. Use the vendor participant identifier and organization activation email. Restart the bridge between tests after changing the environment. Keep the token out of project files and logs.

Open Evidence Console → Physiology → Model source → Mindtooth Neurometrics → Connect WebSocket. The study computer subscribes directly to the licensed endpoint documented in the Mindtooth Neurometrics App v3.3 manual, section 10.3. The tab shows the current vendor participant, session, workload, vendor workload threshold, stress and vigilance when available. Disconnect closes the subscription. The monitor selection only changes the view.

## Select the source for an engineering session

In Test sessions, choose Bioadaptive test — load a saved model to use a personal RF. Choose Bioadaptive test — Mindtooth Workload and vendor threshold to use the vendor model. Check the displayed vendor participant and session against the local participant before starting. The assignment pins both identifiers and records them in test-session.json. Vendor mode skips local model training and requires fresh vendor input during preparation. The study PC clock must agree with the vendor server for the freshness check. The direct EEG stream and existing preparation checks are still required.

At each eligible five-trial boundary, the vendor mode checks the current processed Workload against workload_th from the same message. Below threshold plus stable behavior at L0 permits ON. Above threshold plus struggling behavior at L1 permits OFF. Equality or disagreement holds. Behavior uses the existing 20-trial criteria. A change also requires an intact mount, a qualified recent live EEG window after the latest condition or interruption boundary, acceptable clock uncertainty, dwell, and sufficient actual movie exposure at L1. Missing vendor thresholds, timestamps older than three seconds, future timestamps, disconnection or a different vendor identity prevent vendor control. Stress and vigilance provide observation only. Current LSL amplitude scaling still needs verification before numerical EEG quality qualification.

The base decision records vendor metrics, their computation timestamp, source identity, threshold policy, action and reason in personal_decisions.jsonl. Engineering eligibility is distinct from participant physiological admission. The operator cannot switch the assigned source mid-session.

## Verification

An isolated WebSocket fixture exercised subscription, participant filtering, preserved zero values and stale display suppression. Engineering fixtures exercised identity binding, ON, OFF, equality, stale evidence, missing identity and session mismatch. Unity 6000.3.27f2 compiled the vendor engineering acceptance branch. The production site responded; licensed WebSocket authentication, actual vendor payloads, live cross-clock agreement and Quest Pro execution await physical testing.
