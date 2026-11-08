/* Display contracts only; these fixtures do not run or alter the controller. */
const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict'),path=require('node:path');
const context={window:{},setInterval:()=>{}};
vm.runInNewContext(fs.readFileSync(path.join(__dirname,'console/decision-evidence.js'),'utf8'),context);
const describe=context.window.EvidenceUI.describe;
const decision={stage:'layer_b',base:{score:.60358,behavior:{ba:.25,count:5},lowThreshold:.56,highThreshold:.6,level:1},
 review:{score:null,applicableBehavior:{ba:1/6,count:4},thresholds:{behaviorTrials:5},reason:'model_window_not_current_condition',
 gazeInput:{simulated:true,environment:'desktop_fixed_gaze',validRatio:.75,taskShare:.7}},delivered:'HOLD',receipt:{level:1}};
const saved=JSON.stringify(decision),view=describe(decision);
assert.match(view.baseText,/RF 0.604.*BA 25%.*5 trials/);
assert.match(view.currentText,/4 \/ 5 eligible trials.*BA 16.7%.*Unavailable in current condition/);
assert.match(view.gateText,/condition change.*Eye checks were not evaluated/);
assert.equal(view.command,'Keep video ON');
assert.equal(view.acknowledgment,'Command acknowledged');
assert.equal(view.desktop,true);assert.equal(view.simulated,true);
assert.equal(JSON.stringify(decision),saved,'Rendering must preserve the original record');
const nullBA=structuredClone(decision);nullBA.review.applicableBehavior.ba=null;
assert.match(describe(nullBA).currentText,/BA Unavailable/);
assert.doesNotMatch(describe(nullBA).currentText,/BA 25%/);
const zero=structuredClone(decision);zero.base.score=0;zero.review.score=0;zero.review.applicableBehavior.ba=0;
assert.match(describe(zero).baseText,/RF 0 /);assert.match(describe(zero).currentText,/BA 0%.*RF 0$/);
const legacy=structuredClone(decision);delete legacy.review.applicableBehavior;delete legacy.review.score;
assert.match(describe(legacy).currentText,/Inputs not recorded/);
const tracked=structuredClone(decision);tracked.review.gazeInput={simulated:false};tracked.review.gaze={gaze_valid_ratio:.8,task_gaze_ratio:.7};
assert.equal(describe(tracked).simulated,false);assert.equal(describe(tracked).hasEyes,true);
assert.equal(describe(tracked,{taskInputSource:'simulated_answers_gaze_playback_receipts'}).simulated,true);
const missing=structuredClone(decision);delete missing.receipt;assert.equal(describe(missing).acknowledgment,'No matching command receipt');
const acknowledgedOff=structuredClone(decision);acknowledgedOff.receipt.level=0;
assert.equal(describe(acknowledgedOff).command,'Keep video OFF');
console.log('Decision clarity contracts passed: separate windows, null/zero values, provenance, receipts and immutable inputs.');
