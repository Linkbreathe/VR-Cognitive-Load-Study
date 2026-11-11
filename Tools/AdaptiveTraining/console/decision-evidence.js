/* Display-only rendering of recorded decisions. No control rules are evaluated here. */
(() => {
'use strict';
const human=v=>v==null||v===''?'—':String(v).replaceAll('_',' ').replaceAll('-',' ').replace(/^./,c=>c.toUpperCase());
/* One vocabulary for every Console page. Recorded codes stay unchanged; only their display text is mapped. */
const terms={
 round:{layer_a:'Round A',layer_b:'Round B'},
 roundRole:{layer_a:'Review observed only',layer_b:'Review enforced'},
 scope:{'editor-test':'Unity Editor test',engineering:'Engineering session',participant:'Participant session',synthetic:'Synthetic test','synthetic-test':'Synthetic test',replay:'Replay'},
 signal:{live_sensor:'Live EEG',replay:'Recorded EEG replay',csv_replay:'CSV EEG replay',synthetic:'Synthetic EEG'},
 mode:{practice:'Practice',practice_eeg:'Practice with EEG',calibration:'Workload calibration',calibration_rehearsal:'Calibration rehearsal',control_calibration:'Control calibration',comparison_candidate:'Adaptive comparison',comparison:'Adaptive comparison',comparison_rehearsal:'Comparison rehearsal',bioadaptive:'Saved-model test',vendor_workload:'Mindtooth workload test',full:'Legacy protocol'},
 kind:{participant:'Participant session',engineering:'Engineering session','editor-test':'Unity Editor test',synthetic:'Synthetic test'},
 verdict:{NO_CHANGE:'No change proposed',DEFER:'Held by review',ALLOW:'Allowed',DENY:'Held by review',HOLD:'Held'},
 basis:{probe_or_controller:'Behavior or workload model',mindtooth_vendor_workload:'Mindtooth workload',behavioral_fallback:'Behavioral fallback',random_schedule:'Random schedule'},
 training:{load_reference_ready:'Workload reference ready',ok:'Completed'},
 environment:{desktop_fixed_gaze:'Desktop · simulated gaze',quest_pro:'Quest Pro · tracked eyes'}
};
const label=(group,code)=>terms[group]?.[code]??human(code);
const reasons={
 current_condition_agreement_missing:'Behavior and the workload score did not both support opening for the current video state.',
 model_window_not_current_condition:'The workload score window spans the last video change, so it cannot support this change yet.',
 insufficient_remaining_probe_checkpoints:'Too few trials remain in this round to test a new opening.',
 playback_score_rise_with_stable_behavior_and_task_gaze:'The score rose as the video started, while behavior stayed stable and gaze stayed on the task before responses.',
 playback_eye_tracking_insufficient:'Too little valid eye tracking before responses to defer the closure.',
 playback_task_view_insufficient:'Too little valid eye time was on the task to defer the closure.',
 insufficient_pre_response_playback:'Less than one second of decoded video overlapped the trials before responses.',
 eye_tracking_insufficient:'Too little valid eye tracking to allow opening.',
 task_view_insufficient:'Too little eye time was on the task to allow opening.',
 current_condition_supported:'Behavior, workload score and gaze all support opening for the current video state.',
 conditional_reopening_probe:'Current evidence supports reopening under this session\'s recorded opening-verification policy.',
 support_probe_on:'Behavior, workload score and gaze support opening, but the last turn-off followed difficulty during playback. This opening is a logged 4-trial probe.',
 support_probe_passed:'Probe passed: 4 trials with the video on, fewer than 2 mistakes or omissions and no turn-off. Authorization restored via logged probe; the video stays on.',
 support_probe_failed_behavior:'Probe failed: 2 or more mistakes or omissions with the video on. The video is turned off; the next opening is again a probe and needs 2 more scored trials with the video off (at most 7).',
 support_probe_failed_model:'Probe failed: the workload score for the current video state supports turning the video off. The next opening is again a probe and needs 2 more scored trials with the video off (at most 7).',
 support_probe_inconclusive_playback:'Probe inconclusive: playback failed or was interrupted, so no comparable evidence was collected. The video is turned off and the next opening is again a probe; no cooldown is added.',
 support_probe_inconclusive_no_decoded_playback:'Probe inconclusive: no decoded video played during the 4 probe trials. The video is turned off and the next opening is again a probe; no cooldown is added.',
 support_cooldown_trials_missing:'After a failed probe, the next probe needs more scored trials with the video off.',
 support_probe_window_closed:'Only a probe opening is authorized, and too few checks remain to complete one (the last probe opening is at trial 54).',
 behavior_reduction:'Behavior supports turning the video off.',
 model_reduction:'The workload score supports turning the video off; the conditions to defer were not met.',
 base_hold:'No change was proposed at this check.',
 probe_passed:'The recorded opening verification passed. See the final command for the video state.',
 probe_failed:'Opening verification failed. This session\'s policy turns the video off and locks further openings for the round.',
 probe_failed_retry_permitted:'The test failed: 2 or more mistakes or omissions after opening. The video is turned off; one more test is allowed this round.',
 failed_probe_phase_lock:'This session\'s opening-verification policy has locked further openings for the round.',
 probe_inconclusive_retry_permitted:'The test could not be completed (playback failed or trials were missing). The video is turned off; a later opening may be considered.',
 probe_checkpoint_missed_retry_permitted:'The verification check was missed. The video is turned off; a later opening may be considered.',
 behavior_evidence_predates_switch:'The behavior window includes trials from before the last video change.',
 thresholds_not_met:'Neither behavior nor the workload score reached a threshold.',
 any_threshold_met:'Behavior or the workload score reached a threshold.',
 insufficient_trials:'Not enough scored trials yet.'
};
const explain=r=>reasons[r]||(r?human(r)+'.':'No reason recorded.');
const checkNames={
 'Base balanced accuracy':'Balanced accuracy','Base omissions':'Omissions','Match trials':'Match trials','Non-match trials':'Non-match trials','Base model score':'Workload score',
 'Current-condition eligible trials':'Scored trials since the last video change','Current-condition balanced accuracy':'Balanced accuracy since the last video change',
 'Current-condition omissions':'Omissions since the last video change','Applicable model score':'Workload score for the current video state',
 'Playback in verification (seconds)':'Video playback during verification','Prior nonoverlapping score':'Previous workload score','Current score':'Current workload score',
 'Behavior permits OFF review (BA)':'Balanced accuracy allows deferral','Behavior permits OFF review (omissions)':'Omissions allow deferral',
 'Pre-response movie overlap (seconds)':'Video overlap before responses','Valid eye coverage':'Valid eye coverage','Task share of valid eye time':'Task gaze share',
 'Test trials completed':'Test trials completed (video on)','Test mistakes and omissions':'Mistakes and omissions in the test',
 'Probe trials completed (video on)':'Probe trials completed (video on)','Probe mistakes and omissions':'Mistakes and omissions in the probe','Decoded playback in probe (seconds)':'Decoded video during the probe'
};
const checkName=n=>checkNames[n]||n;
const baseCheck=n=>/^(Base |Match trials|Non-match trials)/.test(n);
const add=(p,tag,text,cls)=>{const e=document.createElement(tag);if(text!=null)e.textContent=text;if(cls)e.className=cls;p.append(e);return e;};
const fmt=v=>typeof v==='number'&&Number.isFinite(v)?String(Math.round(v*1000)/1000):v??'—';
const fixed2=v=>typeof v==='number'&&Number.isFinite(v)?v.toFixed(2):'—';
const pct=v=>typeof v==='number'&&Number.isFinite(v)?`${Math.round(v*1000)/10}%`:'Unavailable';
const ops={'>=':'≥','<=':'≤','<':'<','>':'>'};
const isPercent=n=>/accuracy|eye coverage|share of valid|Task share/i.test(n);
const isSeconds=n=>/seconds/i.test(n);
function checkValue(row,v){if(v==null)return '—';if(isPercent(row.name))return fmt(v*100)+'%';if(isSeconds(row.name))return fmt(v)+' s';return fmt(v);}
const checkRule=row=>row.operator?`${ops[row.operator]||row.operator} ${checkValue(row,row.threshold)}`:'—';
const videoState=level=>level===1?'ON':level===0?'OFF':'unchanged';
// Fit real projected coordinates without moving points or changing target membership.
function fitGazeLayout(layout,points){
 const fitted={...layout};
 for(const p of points)if(p?.valid===true&&p.projected===true&&Number.isFinite(p.x)&&Number.isFinite(p.y)){
  fitted.minX=Math.min(fitted.minX,p.x-.1);fitted.maxX=Math.max(fitted.maxX,p.x+.1);
  fitted.minY=Math.min(fitted.minY,p.y-.1);fitted.maxY=Math.max(fitted.maxY,p.y+.1);
 }
 return fitted;
}

function describe(c,context={}){
 const b=c.base||{},r=c.review||{},limits=r.thresholds||{},behavior=r.applicableBehavior;
 const source=r.gazeInput||context.gazeInput||b.gazeInput;
 const simulated=!!source?.simulated||context.taskInputSource==='simulated_answers_gaze_playback_receipts'||c.gazeEvidence?.before?.source==='simulated';
 const desktop=source?.environment==='desktop_fixed_gaze'||source?.source==='simulated_fixed_gaze';
 const score=Object.hasOwn(r,'score')?(r.score==null?'not available for the current video state':fmt(r.score)):'Not recorded';
 const baseText=`Proposal inputs · Workload ${fmt(b.score)} · BA ${pct(b.behavior?.ba)} · ${fmt(b.behavior?.count)} trials`;
 const currentText=behavior?`Since the last video change · ${fmt(behavior.count)} / ${fmt(limits.behaviorTrials)} scored trials · BA ${pct(behavior.ba)} · Workload ${score}`:`Since the last video change · ${score==='Not recorded'?'Inputs not recorded':'Workload '+score}`;
 const checks=(Array.isArray(r.checks)?r.checks:[]).filter(x=>x&&typeof x.name==='string'&&typeof x.status==='string').map(x=>({...x,label:checkName(x.name),
  /* Display only: an unfinished probe is in progress, not an unmet requirement. The recorded status is unchanged. */
  ...(x.name==='Probe trials completed (video on)'&&x.status!=='met'&&!r.support?.outcome?{status:'in progress'}:{})}));
 /* Probe-style A/B reviews record a recommendation, applicable behavior or named checks; Stage 3 Guard records do not. */
 const probe=typeof r.recommended==='string'||checks.length>0||Object.hasOwn(r,'applicableBehavior');
 const reason=r.reason||b.reason,hasEyes=!!(r.gaze||r.playbackGaze||c.viewChecks?.length);
 const eyeNote=probe&&!hasEyes?' Gaze checks were not reached: an earlier requirement stopped the review. Eye tracking can still be active.':'';
 const gateText=explain(reason)+eyeNote;
 const level=c.receipt?.level,levelKnown=level===0||level===1,videoLevel=levelKnown?level:b.level;
 const command=c.delivered==='HOLD'?'Video stays '+videoState(videoLevel):'Turn video '+(c.delivered||'unknown');
 const acknowledgment=c.receipt?'Unity confirmed receipt':'No Unity receipt matched';
 const isA=c.stage==='layer_a',enforced=c.stage==='layer_b'||c.reviewMode==='enforce',base=b.action||'HOLD',del=c.delivered;
 /* Without a recorded recommendation, an enforced review's outcome is the delivered command; otherwise it stays unknown. */
 const rec=typeof r.recommended==='string'?r.recommended:enforced&&!isA?del:undefined;

 /* Checks are split by their recorded names: proposal routes first, then review checks in recorded order. */
 const behaviorRows=checks.filter(x=>baseCheck(x.name)&&x.name!=='Base model score'),modelRows=checks.filter(x=>x.name==='Base model score');
 const reviewRows=checks.filter(x=>!baseCheck(x.name));
 const firstUnmet=String(reason).startsWith('support_probe_')?undefined:reviewRows.find(x=>x.status!=='met'&&x.name!=='Probe trials completed (video on)');
 const firstUnmetText=firstUnmet?`${firstUnmet.label} ${checkValue(firstUnmet,firstUnmet.value)} — needs ${checkRule(firstUnmet)}.`:'';
 const reviewed=reviewRows.length>0||base!=='HOLD'&&(probe||rec!==undefined);
 const purpose=String(reason).startsWith('probe_')?'Verification needs every check':r.support?.probeEvaluation?'Probe check · any turn-off fails the probe':base==='ON'?'Opening needs every check':base==='OFF'&&reviewRows.length?'Deferring the closure needs every check':'Recorded review checks';
 const routes={behavior:{triggered:b.behaviorTrigger,rows:behaviorRows,either:behaviorRows.some(x=>x.name==='Base omissions')},model:{triggered:b.modelTrigger,rows:modelRows}};

 let headline,tone;
 if(base==='HOLD'&&del==='HOLD'){headline='No change · video stays '+videoState(videoLevel);tone='hold';}
 else if(['ON','OFF'].includes(del)&&base===del){headline=enforced&&!isA?`Video ${del} allowed by review`:`Video ${del} executed`;tone=isA&&rec==='HOLD'?'hypothetical':'executed';}
 else if(del==='HOLD'&&['ON','OFF'].includes(base)){headline=base==='ON'?'Video ON blocked by review':'Video OFF deferred by review';tone='blocked';}
 else if(['ON','OFF'].includes(del)){headline=`Video ${del} after review`;tone='executed';}
 else{headline='Command '+(del||'not recorded');tone='hold';}
 const lead=tone==='hypothetical'?'Round B rules would have held this change. Round A executes the proposal and only observes the review.'+(firstUnmetText?' First unmet check: '+firstUnmetText:'')
  :tone==='blocked'?(firstUnmetText||explain(reason))
  :tone==='hold'?explain(b.reason||reason):explain(reason);

 const triggered=[b.behaviorTrigger&&'behavior',b.modelTrigger&&'workload model'].filter(Boolean);
 const proposalWhy=base==='HOLD'?explain(b.reason):triggered.length?`Triggered by ${triggered.join(' and ')}: BA ${pct(b.behavior?.ba)}, ${fmt(b.behavior?.omissions??0)} omissions, workload ${fmt(b.score)}.`:explain(b.reason);
 const actionWord=a=>a==='ON'?'Turn video ON':a==='OFF'?'Turn video OFF':'No change';
 let review;
 if(!reviewed)review=base==='HOLD'?{pill:null,text:'Not reached',why:'No change was proposed.',tone:'muted'}:{pill:null,text:'Not recorded',why:'This record has no review outcome'+(c.reviewMode?` (review mode ${c.reviewMode})`:'')+'.',tone:'muted'};
 else if(isA)review={pill:rec||'—',text:rec==='HOLD'&&base!=='HOLD'?'Would hold · not applied':'Would allow · not applied',why:'Round A only observes the review.'+eyeNote,tone:rec==='HOLD'&&base!=='HOLD'?'hypothetical':'muted'};
 else if(rec===base)review={pill:rec,text:'Allowed',why:(firstUnmetText&&base==='OFF'?'Deferral not met: '+firstUnmetText:explain(reason))+eyeNote,tone:'ok'};
 else review={pill:rec||'—',text:rec==='HOLD'?(base==='ON'?'Blocked':'Deferred'):'Changed to '+rec,why:(firstUnmetText||explain(reason))+eyeNote,tone:'blocked'};
 const steps=[
  {key:'proposal',label:'Proposal',pill:base,text:actionWord(base),why:proposalWhy,tone:base==='HOLD'?'muted':'ok'},
  {key:'review',label:isA?'Review · observed':'Review',...review},
  {key:'command',label:'Command',pill:del||'—',text:command,why:acknowledgment+'.',tone:'muted'}
 ];
 return {baseText,currentText,gateText,hasEyes,probe,source,simulated,desktop,command,acknowledgment,headline,tone,lead,steps,routes,reviewRows,firstUnmet,firstUnmetText,purpose,reviewed,
  thresholds:`Thresholds · ON ≤ ${fixed2(limits.onScore??b.lowThreshold)} · OFF ≥ ${fixed2(limits.offScore??b.highThreshold)}`};
}

function numericFacts(parent,text,cls){
 const p=add(parent,'p',null,cls);
 for(const part of text.split(/(\d+(?:\.\d+)?%?)/g)){
  if(/^\d/.test(part))add(p,'strong',part,'de-number');else p.append(document.createTextNode(part));
 }
 return p;
}
const views=new WeakMap(), liveBoxes=new Map(), reports=new Map();
const svgNS='http://www.w3.org/2000/svg';
function svg(parent,tag,attrs={}){const e=document.createElementNS(svgNS,tag);for(const [k,v]of Object.entries(attrs))e.setAttribute(k,String(v));parent.append(e);return e;}
function spatialPanel(box,c,context,state){
 const info=describe(c,context),panel=add(box,'section',null,'de-gaze'),header=add(panel,'div',null,'de-heading');
 add(header,'h3','Gaze evidence');const controls=add(header,'div',null,'de-toggle');controls.setAttribute('aria-label','Gaze evidence period');
 for(const [value,text]of [['before','At decision'],['after','After decision']]){const button=add(controls,'button',text);button.type='button';button.setAttribute('aria-pressed',state.period===value);button.onclick=()=>{state.period=value;state.frame=null;render(box,c,context);};}
 const e=c.gazeEvidence?.[state.period],rows=(context.trials||[]).filter(t=>e?.trials?.includes(t.id));
 const numbers=rows.map(t=>t.trial),range=numbers.length?`Trials ${Math.min(...numbers)}–${Math.max(...numbers)}`:'No matching trials';
 add(panel,'p',`${range} · ${e?fmt(e.eligibleSeconds)+' s in selected intervals':'Interval unavailable'} · ${state.period==='after'?'Later observations':info.hasEyes?'Used by the review':'Context only'}`,'de-muted');
 if(state.period==='after')add(panel,'p','Follow-up only. The decision inputs above are unchanged.','de-period-note');
 const names={task:'Task',movie:'Movie',other:'Other',missing:'Missing / invalid'},colors={task:'#3f6f8f',movie:'#a07a3f',other:'#9aa1ab',missing:'#c98a7f'};
 if(info.simulated){
  const empty=add(panel,'div',null,'de-empty');add(empty,'strong',info.desktop?'Real gaze is not collected in Desktop mode':'Simulated gaze · no measured eye positions');
  add(empty,'p',info.desktop?`Configured values · ${pct(info.source?.validRatio)} valid coverage · ${pct(info.source?.taskShare)} of valid time on the task`:'See the recorded simulation provenance for its gaze assumptions.','de-muted');
  if(state.period==='after')add(empty,'p','No measured spatial follow-up is available.','de-muted');
 }else if(!e){
  const empty=add(panel,'div',null,'de-empty');add(empty,'strong','No recorded gaze interval for this decision');
  add(empty,'p',context.spatialEvidenceRevision?'This decision has no gaze audit in the report.':'Gaze intervals appear once the session report is available.','de-muted');
 }else{
  const grid=add(panel,'div',null,'de-eye-grid'),left=add(grid,'div'),right=add(grid,'div');
  const frames=e?.frames||[];let frame=frames.find(f=>f.frame===state.frame)||frames.slice().sort((a,b)=>b.mappedSeconds-a.mappedSeconds)[0];
  if(frames.length>1){const wrap=add(left,'label','Projection frame'),select=add(wrap,'select');for(const f of frames)select.add(new Option(`Frame ${f.frame} · ${fmt(f.mappedSeconds)} s mapped`,String(f.frame)));select.value=String(frame.frame);select.onchange=()=>{state.frame=Number(select.value);render(box,c,context);};}
  const plot=svg(left,'svg',{viewBox:'0 0 600 360',role:'img','aria-label':frame?.bins.length?'Recorded tracked-eye dwell density on the front reference plane':'No recorded spatial gaze data',class:'de-heatmap'});
  svg(plot,'rect',{width:600,height:360,fill:'#f7f6f2'});
  if(frame){const l=frame.layout,xx=x=>600*(x-l.minX)/(l.maxX-l.minX),yy=y=>360*(l.maxY-y)/(l.maxY-l.minY);
   for(const [key,text]of [['movie','Movie'],['task','Task area']]){const r=l[key];if(!r?.available)continue;svg(plot,'rect',{x:xx(r.x),y:yy(r.y+r.height),width:600*r.width/(l.maxX-l.minX),height:360*r.height/(l.maxY-l.minY),fill:'#fff',stroke:'#cdcac2','stroke-width':1.5});const t=svg(plot,'text',{x:xx(r.x)+6,y:yy(r.y+r.height)+17,fill:'#5d6572','font-size':13});t.textContent=text;}
   const max=Math.max(...frame.bins.map(bin=>bin[2]),.001);
   for(const [x,y,seconds]of frame.bins)svg(plot,'rect',{x:x*12.5,y:y*11.25,width:12.5,height:11.25,rx:4,fill:'#a5612e',opacity:.12+.78*seconds/max});
   add(left,'p',`Frame ${frame.frame} · ${fmt(frame.mappedSeconds)} s mapped · ${fmt(frame.outsideSeconds)} s outside the original view${frame.expanded?' · Plot expanded to show recorded positions':''}`,'de-muted');
   add(left,'p',`0–${fmt(max)} s per cell · Local color scale; compare durations, not darkness across views.`,'de-muted');
  }else{const t=svg(plot,'text',{x:300,y:180,'text-anchor':'middle',fill:'#697281','font-size':18});t.textContent=!e?'Coordinates not recorded':!e.eligibleSeconds?'No eligible interval':'Coordinates unavailable';}
  add(left,'p','Front-plane projection · outlines show location, not playback state.','de-muted');
  add(right,'h4','Recorded time by region');const total=e?.eligibleSeconds||0,regions=e?.regionSeconds;
  if(total&&regions){const bar=add(right,'div',null,'de-region-bar');bar.setAttribute('role','img');bar.setAttribute('aria-label',Object.entries(regions).map(([k,v])=>`${names[k]} ${pct(v/total)}`).join(', '));
   const list=add(right,'div',null,'de-region-list');for(const [k,name]of Object.entries(names)){const segment=add(bar,'span');segment.style.width=100*regions[k]/total+'%';segment.style.background=colors[k];const key=add(list,'span',name);key.style.setProperty('--swatch',colors[k]);add(list,'strong',pct(regions[k]/total));}
   const valid=total-regions.missing;add(right,'p',`${pct(valid/total)} valid / eligible time · ${valid?pct(regions.task/valid):'Unavailable'} of valid time on the task`,'de-muted');
  }else add(right,'p',e?'No eligible recorded interval.':'Gaze audit not available in this report.','de-muted');
  const actual=c.review?.playbackGaze||c.review?.gaze;
  if(state.period==='before'&&actual)add(right,'p',`Used by the review: ${pct(actual.gaze_valid_ratio)} valid · ${pct(actual.task_gaze_ratio)} of valid time on the task.`,'de-period-note');
  add(right,'p','Missing / invalid includes gaps in the audit. Spatial coverage can be lower because coordinates are missing, late or in another frame.','de-muted');
 }
 if(state.period==='after'){
  add(panel,'p',rows.length?`${rows.filter(t=>t.scored).length} scored follow-up trials · ${rows.filter(t=>t.scored&&!t.correct).length} incorrect or omitted. Later observations do not establish that the earlier decision was correct.`:'No completed follow-up trials recorded yet.','de-follow');
  const next=context.checkpoints?.find(x=>x.request===e?.nextRequest);if(next)add(panel,'p',`Next recorded decision · Trial ${next.trial} · ${next.delivered} · ${explain(next.review?.reason)}`,'de-muted');
 }
 const timeline=add(box,'details',null,'de-timeline');timeline.open=state.timeline;add(timeline,'summary','Trials in this decision window');timeline.ontoggle=()=>state.timeline=timeline.open;
 add(timeline,'p',e?.basis||'Interval basis not recorded.','de-muted');
 if(!rows.length)add(timeline,'p','No matching trials recorded.','de-muted');
 else{const wrap=add(timeline,'div',null,'table-scroll'),table=add(wrap,'table',null,'evidence-table'),tr=add(table,'tr');for(const h of ['Trial','Response','Decoded video','Gaze before response'])add(tr,'th',h);
  for(const t of rows){const row=add(table,'tr');add(row,'td',t.trial);add(row,'td',!t.scored?'Unscored':t.response==null?'Omitted':t.correct?'Correct':'Incorrect');add(row,'td',t.media?.some(m=>m.visual)?'Overlaps trial':'No decoded overlap');const cell=add(row,'td');if(info.simulated){cell.textContent='Not measured';continue;}const line=add(cell,'div',null,'de-region-bar');for(const [a,b,target]of t.preResponse){const seg=add(line,'span');seg.style.width=100*(b-a)/Math.max(.001,t.preResponseSeconds)+'%';seg.style.background=colors[target]||colors.other;seg.title=`${names[target]||target}: ${fmt(b-a)} s`;}}}
}
setInterval(()=>{for(const [box,v]of liveBoxes){if(!box.isConnected){liveBoxes.delete(box);continue;}render(box,v.c,v.context);}},5000);

const icon={met:'✓','outside threshold':'✗',missing:'?','not reached':'–','in progress':'…'};
function checkList(parent,rows,first){
 const list=add(parent,'ul',null,'de-checks');
 for(const row of rows){
  const item=add(list,'li',null,'de-check '+String(row.status).replaceAll(' ','-')+(row===first?' first':''));
  add(item,'span',icon[row.status]||'·','de-check-icon').setAttribute('aria-label',row.status);
  const text=add(item,'span',null,'de-check-name');text.textContent=row.label;
  if(row===first)add(text,'span','First unmet','de-first');
  if(row.note)add(text,'small',row.note);
  add(item,'span',row.status==='not reached'?'not reached':`${checkValue(row,row.value)}  ${checkRule(row)}`,'de-check-value');
 }
 return list;
}
function routeView(parent,info,c){
 const b=c.base||{},isA=c.stage==='layer_a';
 const proposal=add(parent,'section',null,'de-route-group');
 const head=add(proposal,'header');add(head,'h4','1 · Proposal');add(head,'span','Either route can propose a change','de-rule');add(head,'span','Result: '+(b.action==='HOLD'?'no change':'propose '+b.action),'de-result');
 const grid=add(proposal,'div',null,'de-routes');
 for(const [key,name] of [['behavior','Behavior route'],['model','Workload model route']]){
  const route=info.routes[key],card=add(grid,'div',null,'de-route'+(route.triggered===true?' decisive':route.triggered===false?' idle':''));
  const rh=add(card,'div',null,'de-route-head');add(rh,'strong',name);
  add(rh,'span',route.triggered===true?'✓ Triggered':route.triggered===false?'Not triggered':'Not recorded',route.triggered===true?'ok':'muted');
  if(route.rows.length)checkList(card,route.rows);else add(card,'p','No recorded check.','de-muted');
  if(key==='behavior'&&route.either)add(card,'p','Either the accuracy or the omission condition is enough to propose OFF.','de-muted');
 }
 const review=add(parent,'section',null,'de-route-group');
 const rhead=add(review,'header');add(rhead,'h4',isA?'2 · Review (Round B rules, observed only)':'2 · Review');
 if(!info.reviewed){add(rhead,'span','Not reached','de-rule muted');add(review,'p','No change was proposed, so the review did not run.','de-muted');return;}
 if(info.probe)add(rhead,'span',info.purpose,'de-rule');
 const step=info.steps[1];add(rhead,'span','Result: '+step.text.toLowerCase(),'de-result '+(step.tone==='blocked'?'bad':''));
 const rows=info.reviewRows.slice();
 if(info.probe&&!info.hasEyes&&rows.length)rows.push({name:'gaze',label:'Valid eye coverage and task gaze share',status:'not reached',value:null});
 if(rows.length)checkList(review,rows,info.firstUnmet);else add(review,'p','No review checks were recorded for this decision.','de-muted');
}


/* Evidence relations: recorded attribution claims and Guard forward/reverse checks. Display only; never used to decide. */
const stateWord={SUPPORTED:'Supported',UNRESOLVED:'Unresolved',DISPUTED:'Disputed'};
const annotationWord={during:'Started during the video',before:'Started before the video',mixed:'Unclear · clues disagree',insufficient:'Not enough evidence',none:'No sign',unresolved:'Not known yet'};
const annotationPhrase={during:'started while the video was playing',before:'started before the video',mixed:'is unclear because the two clues disagree',insufficient:'shows no clear sign'};
function clipLabel(clip,context){
 const id=clip?.id||clip, span=Object.values(context.evidenceRelations?.clips||{}).flat().find(x=>x.id===id);
 const m=/comparison-(\d+)-exposure-(\d+)/.exec(span?.clip||clip?.clip||'');
 const name=m?`Round ${m[1]==='0'?'A':'B'} · clip ${m[2]}`:'Video clip';
 return span?`${name} · ${fmt(Math.round(span.seconds*10)/10)} s · trials ${span.startTrial}–${span.endTrial}`:name;
}
function stateTag(parent,state){add(parent,'span',stateWord[state]||state||'Pending','de-state '+String(state||'pending').toLowerCase());}
const mistakesWord={during:'Mostly while the video played',before:'All before the video',mixed:'Some during, some before',none:'No mistakes'};
const scoreWord={during:'Rose while the video played',before:'Already high before the video',none:'Did not reach the line',insufficient:'No score available'};
function annotationLine(list,label,value,words,note){const li=add(list,'li',null,'de-check '+(value==='during'?'outside-threshold':value==='before'?'met':'not-reached'));add(li,'span','·','de-check-icon');const n=add(li,'span',label,'de-check-name');if(note)add(n,'small',note);add(li,'span',words[value]||value,'de-check-value');}
/* The two clues behind a turn-off note, with the two score numbers that decide the second one. */
function noteClues(box,note,when){
 const list=add(box,'ul',null,'de-checks');
 annotationLine(list,(when?when+' · ':'')+'Clue 1 · Mistakes: were they made while the video played?',note.behavior,mistakesWord);
 const e=note.scoreEvidence||{},num=v=>typeof v==='number'?fixed2(v):'—';
 annotationLine(list,(when?when+' · ':'')+'Clue 2 · Workload score: did it rise while the video played?',note.score,scoreWord,
  e.line!=null?`Before the video ${num(e.before)} → highest while playing ${num(e.peak)} · line ${num(e.line)}`:null);
}
function linkedOffText(link){
 if(link.rule==='selective_support_probe')return link.supportInvalidated?`Difficulty during playback suspended direct opening: evidence with the video off cannot by itself re-authorize it. The next opening is a logged 4-trial probe after ${link.requiredOffTrials||5} scored trials with the video off.`:'This turn-off did not suspend direct opening. Opening depends on current-condition behavior, workload score and gaze.';
 if(link.rule==='annotation_only')return 'This note is historical context. Opening depends on current-condition behavior, workload score and gaze; the note adds no opening test or lock.';
 if(link.verifiedReopening)return link.basis==='playback_difficulty'?'The recorded policy requires opening verification after difficulty during playback.':'The recorded policy requires opening verification after the earlier verification was interrupted.';
 return 'The recorded note adds no separate reopening requirement. The session\'s opening rules still apply.';
}
function relationsView(box,c,context){
 const e=c.evidenceRelations, r=c.review||{};
 if(!e&&!r.offAnnotation&&!r.linkedOff&&!r.support)return;
 const panel=add(box,'details',null,'de-relations');panel.open=true;add(panel,'summary','Evidence relations');
 const head=add(panel,'div',null,'de-rel-head');add(head,'span','Association, not cause','de-rule');add(head,'span',r.support?'Relations are recorded only; the turn-off note sets whether the next opening is a probe':'Recorded alongside the decision; not used to make it','de-muted');
 /* Plain-language reading of everything below, so the blocks can be read as supporting detail. */
 const said=[];
 if(e?.claim){const k=e.claim;said.push(`The score ${k.direction==='up'?'rose into the struggling range':'fell into the stable range'} (${fmt(k.previousScore)} → ${fmt(k.score)}).`);
  said.push(k.claimedEvent?`It was traced back to ${clipLabel(k.claimedEvent,context)}, and the timing and exposure ${k.relationState==='SUPPORTED'?'line up':'do not fully line up'}.`:'No video played just before it, so the video is not a candidate source.');
  if(k.featureChange?.available)said.push(k.featureChange.consistent?'The EEG features moved the same way, which supports the score change.':'The EEG features did not move clearly in the same direction, so the score change has weak support.');}
 else if(e)said.push('The score did not cross a cutoff here, so no reverse check ran.');
 if(e){const n=(e.relations||[]).length,sup=(e.relations||[]).filter(x=>x.state==='SUPPORTED').length;said.push(n?`${n===1?'One video clip':n+' video clips'} played before this score window${sup?` (${sup} with timing and exposure confirmed)`:''}.`:'No video clip played before this score window.');}
 const v=r.verification;if(v)said.push(v.outcome==='passed'?`The recorded ${v.needed}-trial test after the last opening passed (${v.errors} mistake${v.errors===1?'':'s'} or omissions).`:v.errors>=v.failAt?`The recorded ${v.needed}-trial test after the last opening failed (${v.errors} mistakes or omissions).`:`The recorded ${v.needed}-trial test after the last opening is running: ${v.completed} of ${v.needed} trials, ${v.errors} mistake${v.errors===1?'':'s'} so far.`);
 if(r.offAnnotation)said.push(`Turning the video off here: the difficulty ${annotationPhrase[r.offAnnotation.result]||'is unclear'}.`);
 if(r.linkedOff)said.push(`Last time the video was turned off (trial ${r.linkedOff.trial}), the difficulty ${annotationPhrase[r.linkedOff.annotation.result]||'was not known'}. ${linkedOffText(r.linkedOff)}`);
 const sp=r.support;if(sp)said.push(supportText(sp));
 if(said.length){const box=add(panel,'div',null,'de-rel-summary');add(box,'strong','In short');add(box,'p',said.join(' '));add(box,'p',sp?'Only the turn-off note and probe outcome change opening authorization; the other relations explain the timing around it.':'None of this changed the decision; it explains the timing around it.','de-muted');}
 if(e){
  const b=e.bands;
  add(panel,'p',`Score window used by this decision: workload ${fmt(e.score)}`+(b?` · reverse bands follow the session cutoffs (stable ≤ ${fixed2(b.stable_max)}, struggling ≥ ${fixed2(b.struggling_min)}, minimum change ${fixed2(b.min_change)})`:''),'de-muted');
  const rev=add(panel,'div',null,'de-rel');const top=add(rev,'div',null,'de-rel-top');const dir=add(top,'span',null,'de-dir');add(dir,'b','REVERSE','rv');add(dir,'span','From this score back to its sources');
  if(e.claim){
   const k=e.claim,f=k.featureChange||{};stateTag(top,k.claimedEvent?k.relationState:'NONE');
   const list=add(rev,'ul',null,'de-checks');
   const row=(icon,text,value,cls)=>{const li=add(list,'li',null,'de-check '+(cls||''));add(li,'span',icon,'de-check-icon');add(li,'span',text,'de-check-name');add(li,'span',value,'de-check-value');};
   row('↕',`Score ${fmt(k.previousScore)} → ${fmt(k.score)} crossed ${k.direction==='up'?'into struggling':'into stable'}`,k.direction==='up'?'up':'down','met');
   row(k.claimedEvent?'✓':'–',k.claimedEvent?'Candidate clip: '+clipLabel(k.claimedEvent,context):'No clip played in the 10 s before or during this window',k.claimedEvent?(stateWord[k.relationState]||'pending'):'not video',k.claimedEvent?'met':'not-reached');
   row(f.available?(f.consistent?'✓':'✗'):'–','EEG feature change in the model’s workload direction',f.available?`${f.index>=0?'+':''}${fmt(Math.round(f.index*100)/100)} SD · needs ${k.direction==='up'?'≥ +':'≤ −'}${fmt(f.threshold)}`:'not available',f.available?(f.consistent?'met':'outside-threshold'):'not-reached');
   add(rev,'p',`Change happened between ${fmt(Math.round(k.onsetEarliest*10)/10)} s and ${fmt(Math.round(k.onsetLatest*10)/10)} s (Unity time). A dispute is recorded only if the change finished before the clip could act.`,'de-rel-note');
  }else{
   add(top,'span','Not triggered','de-state idle');
   const reason=e.previousScore==null?'No earlier comparable window in this round.':
     b?`Previous window ${fmt(e.previousScore)} → this window ${fmt(e.score)}. A reverse check starts only on a crossing between ≤ ${fixed2(b.stable_max)} and ≥ ${fixed2(b.struggling_min)} of at least ${fixed2(b.min_change)}.`:'This session records no reverse bands.';
   add(rev,'p',reason,'de-rel-note');
  }
  for(const x of e.relations||[]){
   const item=add(panel,'div',null,'de-rel');const t=add(item,'div',null,'de-rel-top');const d=add(t,'span',null,'de-dir');
   for(const way of x.directions)add(d,'b',way.toUpperCase(),way==='reverse'?'rv':'');add(d,'span','From '+clipLabel(x.clip,context));stateTag(t,x.state);
   const list=add(item,'ul',null,'de-checks');const row=(ok,text,value)=>{const li=add(list,'li',null,'de-check '+(ok===true?'met':ok===false?'outside-threshold':'not-reached'));add(li,'span',ok===true?'✓':ok===false?'✗':'–','de-check-icon');add(li,'span',text,'de-check-name');add(li,'span',value,'de-check-value');};
   row(x.actualSeconds>0,'Clip actually played (decoded)',fmt(Math.round(x.actualSeconds*10)/10)+' s');
   row(x.timeCorrespondence,'Score window falls within the clip plus 0–10 s',x.timeCorrespondence?'yes':'no');
   row(x.audioConfirmed||x.visualExposure,'Exposure confirmed',[x.visualExposure&&'visual',x.audioConfirmed&&'audio (software playback state)'].filter(Boolean).join(' · ')||'not confirmed');
   if(x.state==='UNRESOLVED')add(item,'p','Unresolved usually means the window was recorded in a different video state from the clip, or exposure was not confirmed.','de-rel-note');
   const alt=add(item,'div',null,'de-alt');add(alt,'span','Other explanations kept open:');for(const a of x.alternatives||[])add(alt,'span',human(a),'de-alt-chip');
  }
  if(!(e.relations||[]).length)add(panel,'p','No video clip is related to this score window.','de-muted');
 }else if(context.evidenceRelations)add(panel,'p','This decision has no recorded score window.','de-muted');
 const ann=r.offAnnotation;
 if(ann){
  const box2=add(panel,'div',null,'de-rel');const t=add(box2,'div',null,'de-rel-top');const d=add(t,'span',null,'de-dir');add(d,'b','TURN-OFF NOTE');add(d,'span','Did the difficulty start while the video was playing?');add(t,'span',annotationWord[ann.result]||ann.result,'de-state ann-'+ann.result);
  noteClues(box2,ann);
  add(box2,'p',r.support?'It never delays turning the video off. In Round B, difficulty during playback (or mixed clues) makes the next opening a logged 4-trial probe.':'Written down only; it never delays turning the video off. Retained as historical context for later opening reviews.','de-rel-note');
 }
 const link=r.linkedOff;
 if(link){
  const box3=add(panel,'div',null,'de-rel');const t=add(box3,'div',null,'de-rel-top');const d=add(t,'span',null,'de-dir');add(d,'b','BEFORE TURNING ON');add(d,'span',`Last time the video was turned off (trial ${link.trial})`);add(t,'span',annotationWord[link.annotation.result]||link.annotation.result,'de-state ann-'+link.annotation.result);
  noteClues(box3,link.annotation,`At that turn-off (trial ${link.trial})`);
  add(box3,'p',linkedOffText(link),'de-rel-note strong');
 }
 if(sp){
  const box4=add(panel,'div',null,'de-rel');const t=add(box4,'div',null,'de-rel-top');const d=add(t,'span',null,'de-dir');add(d,'b','ON AUTHORIZATION');add(d,'span','Selective support and logged probe (Round B)');
  const shown=sp.afterReview||sp,badge=sp.outcome==='Probe_Failed'?['Probe failed','ann-during']:sp.outcome==='Probe_Inconclusive'?['Probe inconclusive','ann-mixed']:[supportWord[shown.state]||shown.state,shown.invalidated?'ann-during':'ann-before'];
  add(t,'span',badge[0],'de-state '+badge[1]);
  const list=add(box4,'ul',null,'de-checks de-support');const row=(text,value)=>{const li=add(list,'li',null,'de-check');add(li,'span','·','de-check-icon');add(li,'span',text,'de-check-name');add(li,'span',value,'de-check-value');};
  row('Direct opening from video-off evidence',sp.invalidated?'suspended':'allowed');
  row('Scored video-off trials required before opening',String(sp.requiredOffTrials));
  row('Failed probes this round',String(sp.probeFailures));
  if(sp.probeInconclusive)row('Inconclusive probes this round (no cooldown)',String(sp.probeInconclusive));
  if(sp.authorization)row('Authorized opening',supportWord[sp.authorization]||sp.authorization);
  const pe=sp.probeEvaluation;if(pe)row('Probe so far',`${pe.completed} of 4 trials · ${pe.errors} mistake${pe.errors===1?'':'s'} or omissions · ${fmt(Math.round(pe.playbackSeconds*10)/10)} s decoded video`);
  if(sp.outcome)row('Probe outcome',sp.outcome==='Probe_Passed'?'Passed · '+(sp.audit||'authorization restored'):(sp.outcome==='Probe_Inconclusive'?'Inconclusive · no cooldown · ':'Failed · ')+'next probe needs '+(sp.nextRequiredOffTrials??'more')+' video-off trials');
  add(box4,'p','A failed probe adds a 2-trial cooldown (at most 7); an inconclusive probe adds none. Neither locks openings for the round.','de-rel-note');
 }
}
const supportWord={OFF:'Video off',Normal_ON:'Normal ON',Probe_ON:'Probe ON',Restored_ON:'Restored ON'};
function supportText(sp){
 if(sp.outcome==='Probe_Passed')return 'The probe passed and opening authorization was restored via the logged probe.';
 if(sp.outcome==='Probe_Inconclusive')return `The probe was inconclusive (playback failed, was interrupted or never decoded), so no comparable evidence was collected. The next opening is again a probe and needs ${sp.nextRequiredOffTrials} scored trials with the video off; no cooldown was added.`;
 if(sp.outcome==='Probe_Failed')return `The probe failed. The next opening is again a probe and needs ${sp.nextRequiredOffTrials} scored trials with the video off.`;
 if(sp.probeEvaluation)return `A logged probe is running: ${sp.probeEvaluation.completed} of 4 trials with the video on, ${sp.probeEvaluation.errors} mistake${sp.probeEvaluation.errors===1?'':'s'} or omissions.`;
 if(sp.authorization==='Probe_ON')return 'This opening is a logged probe because direct opening was suspended after difficulty during playback.';
 return sp.invalidated?'Direct opening is suspended; the next opening must be a logged probe.':'Direct opening is allowed.';
}
function render(box,c,context={}){
 let state=views.get(box);if(!state||state.request!==c?.request){state={request:c?.request,period:'before',frame:null,timeline:false,criteria:true};views.set(box,state);}
 if(context.session&&!context.spatialEvidenceRevision){
  liveBoxes.set(box,{c,context});const cached=reports.get(context.session);
  if(!cached||(!cached.loading&&Date.now()-cached.at>4500)){
   const entry={...cached,loading:true,at:Date.now()};reports.set(context.session,entry);
   fetch('/tests/comparison-report?'+new URLSearchParams({session:context.session})).then(async r=>{if(!r.ok)throw Error('Unavailable');return r.json();}).then(report=>{entry.report=report;}).catch(()=>{}).finally(()=>{entry.loading=false;entry.at=Date.now();if(liveBoxes.get(box)?.c?.request===c?.request)render(box,c,context);});
  }
  const report=cached?.report,match=report?.checkpoints?.find(x=>x.request===c?.request);
  if(match){c=match;context=report;}
 }
 box.classList.add('de-workspace');
 box.replaceChildren();if(!c){add(box,'p','No decisions yet. A decision appears after Unity submits its first check.','empty-state');return;}
 const b=c.base||{},r=c.review||{},delivered=c.delivered||'Unknown',reason=r.reason||b.reason;
 const info=describe(c,context);
 const lead=add(box,'div',null,'decision-lead tone-'+info.tone);
 add(lead,'p',`${label('round',c.stage)} · Trial ${c.trial??'—'}`+(context.position?` · Check ${context.position.index} of ${context.position.total}`:''),'eyebrow');
 add(lead,'h3',info.headline);
 add(lead,'p',info.lead,'de-lead-text');
 const steps=add(box,'ol',null,'de-steps');
 for(const step of info.steps){
  const li=add(steps,'li',null,'de-step tone-'+step.tone);add(li,'span',step.label,'de-step-label');
  const body=add(li,'div');const line=add(body,'div',null,'de-step-what');
  if(step.pill)add(line,'span',step.pill,'de-pill pill-'+String(step.pill).toLowerCase());
  add(line,'strong',step.text);add(body,'p',step.why,'de-step-why');
 }
 const facts=add(box,'div',null,'de-facts');numericFacts(facts,info.baseText);numericFacts(facts,info.currentText);
 if(r.thresholds?.behaviorTrials)add(facts,'p',`Current-state review window: ${r.thresholds.behaviorTrials} scored trials. Gaze checks follow the earlier behavior/model requirements; eye acquisition does not wait for this window.`,'de-muted');
 const windowTrials=r.verificationWindowTrials||r.windowTrials;
 numericFacts(facts,(windowTrials?.length?`Window · trials ${Math.min(...windowTrials)+1}–${Math.max(...windowTrials)+1} · `:'')+info.thresholds,'de-muted');
 spatialPanel(box,c,context,state);
 const criteria=add(box,'details',null,'de-criteria');criteria.open=state.criteria;add(criteria,'summary','How this decision was reached');criteria.ontoggle=()=>state.criteria=criteria.open;
 routeView(criteria,info,c);
 add(criteria,'p','✓ met · ✗ not met · ? value missing · – not reached · … in progress. Display only: values and rule versions come from the recorded decision.','check-legend');
 relationsView(box,c,context);
 const provenance=add(box,'details',null,'de-provenance');add(provenance,'summary','Execution & input source');
 const source=r.gazeInput||context.gazeInput||b.gazeInput;
 add(provenance,'p',context.taskInputSource==='simulated_answers_gaze_playback_receipts'?'Eye input: seeded simulated gaze; this is a software replay.':source?.simulated?'Eye input: simulated gaze · fixed 75% valid coverage and 70% of valid time on the task.':source?'Eye input: Quest Pro · see recorded coverage for acquisition evidence.':'Eye input: see session provenance; this record predates the explicit source field.','decision-context');
 add(provenance,'p','A Unity receipt confirms the command, not decoded playback. Check the trial records for actual playback overlap.','decision-context');
 add(provenance,'p','Unity execution: '+human(c.execution||'unknown')+'. '+(delivered==='HOLD'?'The video stays '+videoState(b.level)+'.':''),'decision-context');
 if(delivered==='HOLD'&&c.trial!=null){const stride=context.decisionStride;const next=stride?c.trial+stride:null;add(provenance,'p',reason==='failed_probe_phase_lock'?'Openings remain locked for this round.':next&&context.trialBudget&&next<context.trialBudget?`Next scheduled check: trial ${next}. A later check may still hold.`:'Any further change is considered at the next scheduled check.','decision-context');}
 const technical=add(provenance,'details');add(technical,'summary','Technical details · raw record');add(technical,'pre',JSON.stringify(c,null,2));
}
window.EvidenceUI={render,explain,describe,label,human,terms,checkName,linkedOffText,supportText,fitGazeLayout};
})();
