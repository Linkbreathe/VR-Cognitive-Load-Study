/* Display contracts only; these fixtures do not run or alter the controller. */
const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict'),path=require('node:path');
const context={window:{},setInterval:()=>{}};
vm.runInNewContext(fs.readFileSync(path.join(__dirname,'console/decision-evidence.js'),'utf8'),context);
const {describe,label,explain}=context.window.EvidenceUI;
{
 const original={minX:-1,maxX:1,minY:-1,maxY:1,task:{x:0,y:0,width:.2,height:.2}};
 const snapshot=JSON.stringify(original);
 const expanded=context.window.EvidenceUI.fitGazeLayout(original,[{valid:true,projected:true,x:1.5,y:-3},{valid:false,projected:true,x:100,y:100}]);
 assert.ok(expanded.minY<-3&&expanded.maxX>1.5);
 assert.equal(expanded.maxY,1);assert.equal(JSON.stringify(original),snapshot);
 assert.equal(expanded.task,original.task,'Fitting the plot cannot change task bounds');
}
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
// New ON thresholds are taken from each recorded check; historical 80% rows stay intact.
const relaxed=structuredClone(blocked);
relaxed.base.behavior.ba=2/3;relaxed.review.applicableBehavior.ba=2/3;
for(const row of relaxed.review.checks){if(row.name==='Base balanced accuracy'||row.name==='Current-condition balanced accuracy'){row.value=2/3;row.threshold=.65;}}
const relaxedView=describe(relaxed);
assert.equal(relaxedView.routes.behavior.rows.find(r=>r.name==='Base balanced accuracy').threshold,.65);
assert.equal(relaxedView.reviewRows.find(r=>r.name==='Current-condition balanced accuracy').threshold,.65);
assert.equal(blocked.review.checks[0].threshold,.8);
const setupHtml=fs.readFileSync(path.join(__dirname,'console/tests.html'),'utf8');
assert.match(setupHtml,/Behavior proposes ON at balanced accuracy ≥ 65%/);
assert.match(setupHtml,/OFF at ≤ 50% or 2 omissions/);
assert.match(setupHtml,/defers a workload-only OFF when balanced accuracy is ≥ 80%/);
const offHalf=structuredClone(blocked);
Object.assign(offHalf.base,{action:'OFF',level:1,behaviorTrigger:true,modelTrigger:false});
offHalf.review={recommended:'OFF',reason:'behavior_reduction',checks:[check('Base balanced accuracy',.5,'<=',.5,'met',5)]};
offHalf.delivered='OFF';
assert.equal(describe(offHalf).routes.behavior.rows[0].threshold,.5);
const historicalOff=structuredClone(offHalf);historicalOff.review.checks[0].threshold=.6;
assert.equal(describe(historicalOff).routes.behavior.rows[0].threshold,.6);
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

/* New history-only notes do not inherit archived six/ten-trial requirements. */
const {linkedOffText}=context.window.EvidenceUI;
const note={rule:'annotation_only',annotation:{result:'during'},trial:12};
const noteSaved=JSON.stringify(note);
assert.match(linkedOffText(note),/historical context.*current-condition behavior.*adds no opening test or lock/);
assert.doesNotMatch(linkedOffText(note),/6 trials|ten trials|requires opening verification|can be turned on as usual/);
assert.equal(JSON.stringify(note),noteSaved);
assert.match(linkedOffText({verifiedReopening:true,basis:'playback_difficulty'}),/recorded policy requires opening verification/);
assert.doesNotMatch(linkedOffText({verifiedReopening:true,basis:'playback_difficulty'}),/6 trials|ten trials/);
assert.doesNotMatch(explain('probe_failed'),/second|2 mistakes|6 trials/,'Legacy ten-trial failures must not be described as six-trial failures');
console.log('Opening-review display contracts passed: history-only notes and archived verification stay distinct.');

/* Round B selective support: suspended direct opening, logged probe and cooldown without a lock. */
const {supportText,checkName}=context.window.EvidenceUI;
const suspended={rule:'selective_support_probe',annotation:{result:'during'},trial:16,supportInvalidated:true,requiredOffTrials:7};
assert.match(linkedOffText(suspended),/suspended direct opening.*logged 4-trial probe after 7 scored trials/);
assert.match(linkedOffText({...suspended,supportInvalidated:false}),/did not suspend direct opening/);
assert.match(explain('support_probe_on'),/logged 4-trial probe/);
assert.match(explain('support_probe_passed'),/Authorization restored via logged probe/);
for(const r of ['support_probe_failed_behavior','support_probe_failed_model'])
 assert.match(explain(r),/next opening is again a probe.*at most 7/,r);
for(const r of ['support_probe_inconclusive_playback','support_probe_inconclusive_no_decoded_playback']){
 assert.match(explain(r),/inconclusive.*next opening is again a probe; no cooldown is added/,r);
 assert.doesNotMatch(explain(r),/2 more|at most 7/,r);}
assert.match(supportText({outcome:'Probe_Inconclusive',nextRequiredOffTrials:5}),/inconclusive.*needs 5 scored trials.*no cooldown was added/);
assert.match(explain('support_probe_window_closed'),/trial 54/);
assert.match(supportText({outcome:'Probe_Failed',nextRequiredOffTrials:7}),/again a probe and needs 7/);
assert.match(supportText({outcome:'Probe_Passed'}),/restored via the logged probe/);
assert.doesNotMatch(supportText({outcome:'Probe_Failed',nextRequiredOffTrials:7}),/lock|rest of the round/);
assert.equal(checkName('Probe mistakes and omissions'),'Mistakes and omissions in the probe');
console.log('Selective-support display contracts passed: suspended direct opening, logged probe, restored authorization and cooldown without a lock.');

/* Probe outcomes are explained by their recorded reason; progress below four trials is not an unmet requirement. */
{
 const probeRow={name:'Probe trials completed (video on)',status:'outside threshold',value:0,operator:'>=',threshold:4};
 const base={action:'HOLD',level:1,behavior:{}};
 const ended=describe({stage:'layer_b',base,delivered:'OFF',review:{recommended:'OFF',reason:'support_probe_inconclusive_playback',checks:[probeRow],support:{outcome:'Probe_Inconclusive'}}});
 assert.equal(ended.firstUnmet,undefined);
 assert.match(ended.steps[1].why,/inconclusive.*no cooldown is added/);
 const running=describe({stage:'layer_b',base:{...base,action:'OFF'},delivered:'HOLD',review:{recommended:'HOLD',reason:'model_window_not_current_condition',checks:[probeRow],support:{probeEvaluation:{completed:2,errors:0}}}});
 assert.equal(running.firstUnmet,undefined);
 assert.match(running.steps[1].why,/score window spans the last video change/);
 console.log('Probe display contracts passed: outcome reasons lead and probe progress is not shown as an unmet requirement.');
}
{
 const row={name:'Probe trials completed (video on)',status:'outside threshold',value:2,operator:'>=',threshold:4};
 const running=describe({stage:'layer_b',base:{action:'HOLD',level:1,behavior:{}},delivered:'HOLD',review:{recommended:'HOLD',reason:'base_hold',checks:[row],support:{probeEvaluation:{completed:2,errors:0}}}});
 assert.equal(running.reviewRows[0].status,'in progress');
 assert.equal(row.status,'outside threshold','Recorded check status is not mutated');
 const ended=describe({stage:'layer_b',base:{action:'HOLD',level:1,behavior:{}},delivered:'OFF',review:{recommended:'OFF',reason:'support_probe_inconclusive_playback',checks:[{...row,value:0}],support:{outcome:'Probe_Inconclusive'}}});
 assert.equal(ended.reviewRows[0].status,'outside threshold');
 console.log('Probe progress display contract passed: an unfinished probe is shown in progress without changing the record.');
}
