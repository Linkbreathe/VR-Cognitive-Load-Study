# Explicit decision flow wording

2026-11-08 · Stage 2/3 · display `console-decision-flow-wording-v1` · backend unchanged

## Changes

The shared monitor/report renderer replaces Base / Patent review / Delivered badges with numbered cards: Base controller proposal, Patent evidence review, and Final command to Unity. Each card explains its role and spells out ON, OFF or HOLD. HOLD retains the recorded state, with unknown state labeled explicitly. Review uses the proposal level; final command uses the matching receipt level when available, otherwise the proposal level. Round A labels review as observation only. A separate sentence explains a held opening/closure and that task progression continues. Receipt acknowledgment remains separate from decoded playback. The original recorded reason remains above the cards.

The three cards use a responsive grid, stacking below a 560 px container width. Existing checkpoint navigation, evidence criteria, raw records and gaze display are retained. The display revision file updates new-session metadata through the existing backend mechanism. No controller, RF, threshold, timing, media, acquisition or historical data changes were made.

## Validation

- `node --check Tools/AdaptiveTraining/console/decision-evidence.js` passed.
- `node Tools/AdaptiveTraining/test_decision_clarity.cjs` passed its existing contracts for separate evidence windows, null/zero values, provenance, receipts and immutable records.
- The running service served the updated static assets without restart. Browser inspection of archived session `20261107T203024_5e0116214361` confirmed A's observation-only review and B trial 10's ON / HOLD / HOLD flow. B10 shows Keep video OFF and the recorded current-condition agreement reason, including that eyes were not evaluated on that branch.
- No browser console errors were captured. [Verified desktop screenshot](../Evidence/DecisionFlowWording-20261108/decision-flow.png).
- This was read-only inspection of existing engineering data; no new participant assignment, Unity execution or device validation was performed. Narrow-screen rendering was not separately exercised in this revision. Physical Quest Pro acceptance remains pending.
