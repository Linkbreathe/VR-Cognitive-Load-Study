/* Display contracts only; these fixtures do not run or alter the controller. */
const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict'),path=require('node:path');
const context={window:{},setInterval:()=>{}};
vm.runInNewContext(fs.readFileSync(path.join(__dirname,'console/decision-evidence.js'),'utf8'),context);
const {describe,label,explain}=context.window.EvidenceUI;
const decision={stage:'layer_b',base:{score:.60358,behavior:{ba:.25,count:5},lowThreshold:.56,highThreshold:.6,level:1},
 review:{score:null,applicableBehavior:{ba:1/6,count:4},thresholds:{behaviorTrials:5},reason:'model_window_not_current_condition',
 gazeInput:{simulated:true,environment:'desktop_fixed_gaze',validRatio:.75,taskShare:.7}},delivered:'HOLD',receipt:{level:1}};
const saved=JSON.stringify(decision),view=describe(decision);
assert.match(view.baseText,/Workload 0.604.*BA 25%.*5 trials/);
assert.match(view.currentText,/4 \/ 5 scored trials.*BA 16.7%.*not available for the current video state/);
assert.match(view.gateText,/last video change.*Gaze checks were not reached/);
assert.equal(view.command,'Video stays ON');
assert.equal(view.acknowledgment,'Unity confirmed receipt');
assert.equal(view.desktop,true);assert.equal(view.simulated,true);
assert.equal(JSON.stringify(decision),saved,'Rendering must preserve the original record');
const nullBA=structuredClone(decision);nullBA.review.applicableBehavior.ba=null;
assert.match(describe(nullBA).currentText,/BA Unavailable/);
assert.doesNotMatch(describe(nullBA).currentText,/BA 25%/);
const zero=structuredClone(decision);zero.base.score=0;zero.review.score=0;zero.review.applicableBehavior.ba=0;
assert.match(describe(zero).baseText,/Workload 0 /);assert.match(describe(zero).currentText,/BA 0%.*Workload 0$/);
const legacy=structuredClone(decision);delete legacy.review.applicableBehavior;delete legacy.review.score;
assert.match(describe(legacy).currentText,/Inputs not recorded/);
const tracked=structuredClone(decision);tracked.review.gazeInput={simulated:false};tracked.review.gaze={gaze_valid_ratio:.8,task_gaze_ratio:.7};
assert.equal(describe(tracked).simulated,false);assert.equal(describe(tracked).hasEyes,true);
assert.equal(describe(tracked,{taskInputSource:'simulated_answers_gaze_playback_receipts'}).simulated,true);
const missing=structuredClone(decision);delete missing.receipt;assert.equal(describe(missing).acknowledgment,'No Unity receipt matched');
const acknowledgedOff=structuredClone(decision);acknowledgedOff.receipt.level=0;
assert.equal(describe(acknowledgedOff).command,'Video stays OFF');

/* Recorded B trial 10 shape: behavior proposes ON, the current-state workload score blocks it. */
const check=(name,value,operator,threshold,status,denominator)=>({name,value,operator,threshold,status,denominator});
const blocked={stage:'layer_b',trial:10,delivered:'HOLD',receipt:{level:0},
 base:{action:'ON',level:0,score:.634,behaviorTrigger:true,modelTrigger:false,behavior:{ba:.875,count:5,omissions:0},reason:'any_threshold_met',lowThreshold:.56,highThreshold:.6},
 review:{recommended:'HOLD',reason:'current_condition_agreement_missing',score:.634,applicableBehavior:{ba:.875,count:5},thresholds:{behaviorTrials:5,onScore:.56,offScore:.6},
  gazeInput:{simulated:true,environment:'desktop_fixed_gaze'},checks:[
  check('Base balanced accuracy',.875,'>=',.8,'met',5),check('Match trials',1,'>=',1,'met',5),check('Non-match trials',4,'>=',1,'met',5),
  check('Base model score',.634,'<=',.56,'outside threshold'),
  check('Current-condition eligible trials',5,'>=',5,'met'),check('Current-condition balanced accuracy',.875,'>=',.8,'met',5),
  check('Current-condition omissions',0,'<',2,'met',5),check('Applicable model score',.634,'<=',.56,'outside threshold')]}};
const blockedSaved=JSON.stringify(blocked),b=describe(blocked);
assert.equal(b.headline,'Video ON blocked by review');assert.equal(b.tone,'blocked');
assert.deepEqual([...b.routes.behavior.rows.map(r=>r.name)],['Base balanced accuracy','Match trials','Non-match trials'],'Behavior route keeps only recorded base behavior checks');
assert.deepEqual([...b.routes.model.rows.map(r=>r.name)],['Base model score']);
assert.equal(b.routes.behavior.triggered,true);assert.equal(b.routes.model.triggered,false,'Route status comes from recorded trigger flags');
assert.equal(b.reviewRows.length,4,'Review rows exclude proposal checks');
assert.equal(b.firstUnmet.name,'Applicable model score');
assert.match(b.firstUnmetText,/Workload score for the current video state 0.634 — needs ≤ 0.56/);
assert.equal(b.purpose,'Opening needs every check');
assert.equal(b.steps[1].text,'Blocked');assert.match(b.steps[1].why,/Gaze checks were not reached/);
assert.equal(JSON.stringify(blocked),blockedSaved,'Rendering must preserve the original record');

/* Round A executes its proposal; B rules are reported as hypothetical, never as applied. */
const observed=structuredClone(blocked);observed.stage='layer_a';observed.delivered='ON';
const a=describe(observed);
assert.equal(a.headline,'Video ON executed');assert.equal(a.tone,'hypothetical');
assert.match(a.lead,/Round B rules would have held this change/);assert.equal(a.steps[1].text,'Would hold · not applied');

/* An unmet deferral check means the closure proceeds; it must not be described as a block. */
const closing=structuredClone(blocked);Object.assign(closing.base,{action:'OFF',level:1,behaviorTrigger:false,modelTrigger:true});
Object.assign(closing.review,{recommended:'OFF',reason:'model_reduction',checks:[check('Prior nonoverlapping score',.5,'<',.6,'met'),check('Current score',.7,'>=',.6,'met'),
 check('Pre-response movie overlap (seconds)',.4,'>=',1,'outside threshold')]});closing.delivered='OFF';
const off=describe(closing);
assert.equal(off.headline,'Video OFF allowed by review');assert.equal(off.purpose,'Deferring the closure needs every check');
assert.equal(off.steps[1].text,'Allowed');assert.match(off.steps[1].why,/^Deferral not met: Video overlap before responses 0.4 s — needs ≥ 1 s/);

const hold=structuredClone(blocked);Object.assign(hold.base,{action:'HOLD',behaviorTrigger:false,modelTrigger:false,reason:'thresholds_not_met'});
hold.review={checks:blocked.review.checks.slice(0,4)};
const h=describe(hold);
assert.equal(h.headline,'No change · video stays OFF');assert.equal(h.reviewed,false);assert.equal(h.steps[1].text,'Not reached');

/* Stage 3 Guard records: nested checks and no recommendation must not produce invented rows or eye claims. */
const guard={stage:'bioadaptive',trial:25,delivered:'ON',reviewMode:'enforce',viewChecks:[{judgment:'j1'}],
 base:{action:'ON',reason:'personal_model_not_validated',behavior:{ba:1,count:20}},review:{reason:'independent_valid_support',checks:[[{judgment:'j1',valid:true}]]}};
const g=describe(guard);
assert.equal(g.probe,false);assert.equal(g.reviewRows.length,0);assert.equal(g.firstUnmet,undefined);
assert.equal(g.headline,'Video ON allowed by review');assert.equal(g.steps[1].text,'Allowed');
assert.doesNotMatch(g.steps[1].why,/Gaze checks were not reached|undefined/);assert.doesNotMatch(g.gateText,/Gaze checks/);
const observedGuard=structuredClone(guard);observedGuard.reviewMode='observe';
const og=describe(observedGuard);
assert.equal(og.headline,'Video ON executed');assert.equal(og.steps[1].text,'Not recorded');
assert.equal(label('round','layer_b'),'Round B');assert.equal(label('training','load_reference_ready'),'Workload reference ready');
assert.equal(label('scope','editor-test'),'Unity Editor test');assert.equal(label('training','new_status_code'),'New status code','Unknown codes stay readable');
assert.equal(explain('unrecorded_reason'),'Unrecorded reason.');
console.log('Decision clarity contracts passed: separate windows, null/zero values, provenance, receipts, routes, first unmet check, hypothetical A review and immutable inputs.');
