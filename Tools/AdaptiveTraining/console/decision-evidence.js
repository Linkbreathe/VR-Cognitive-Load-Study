/* Display-only rendering of recorded decisions. No control rules are evaluated here. */
(() => {
'use strict';
const reasons={
 current_condition_agreement_missing:'Behavior and the applicable model score do not both support opening in the current condition.',
 model_window_not_current_condition:'The score window crosses a condition change. It cannot support this change yet.',
 insufficient_remaining_probe_checkpoints:'Too few scheduled trials remain to verify a new video opening.',
 playback_score_rise_with_stable_behavior_and_task_gaze:'The score rose during video onset, while behavior stayed stable and pre-response task gaze met the criteria.',
 playback_eye_tracking_insufficient:'There is not enough valid pre-response eye tracking to defer closure.',
 playback_task_view_insufficient:'Too little valid eye time was directed at the task to defer closure.',
 insufficient_pre_response_playback:'Less than one second of decoded video overlapped the eligible pre-response intervals.',
 eye_tracking_insufficient:'Valid eye tracking is insufficient to authorize opening.',
 task_view_insufficient:'Task-directed eye time does not meet the opening criterion.',
 current_condition_supported:'Behavior, model score and viewing evidence support opening in the current condition.',
 conditional_reopening_probe:'Current evidence supports reopening. The following ten trials will verify the change.',
 behavior_reduction:'Current behavioral evidence supports removing the video.',
 model_reduction:'The applicable model score supports removing the video; deferral conditions were not met.',
 base_hold:'The base controller proposed no change at this checkpoint.',
 probe_passed:'The ten-trial verification passed. Keep the current video state.',
 probe_failed:'The ten-trial verification failed. Close the video and apply the recorded phase lock.',
 failed_probe_phase_lock:'An earlier failed verification prevents further openings in this phase.',
 probe_inconclusive_retry_permitted:'The verification is inconclusive. Close the video; a later opening may be reconsidered.',
 probe_checkpoint_missed_retry_permitted:'The scheduled verification checkpoint was missed. Close the video; a later opening may be reconsidered.',
 behavior_evidence_predates_switch:'The behavioral trigger includes evidence from before the last condition change.'
};
const explain=r=>reasons[r]||String(r||'No reason recorded').replaceAll('_',' ');
const add=(p,tag,text,cls)=>{const e=document.createElement(tag);if(text!=null)e.textContent=text;if(cls)e.className=cls;p.append(e);return e;};
const fmt=v=>typeof v==='number'&&Number.isFinite(v)?String(Math.round(v*1000)/1000):v??'—';

const pct=v=>typeof v==='number'&&Number.isFinite(v)?`${Math.round(v*1000)/10}%`:'Unavailable';
const actionText=(action,level)=>action==='HOLD'?'HOLD — Keep video '+(level===1?'ON':level===0?'OFF':'unchanged (state unknown)'):action==='ON'?'ON — Turn video ON':action==='OFF'?'OFF — Turn video OFF':'Not recorded';
function describe(c,context={}){
 const b=c.base||{},r=c.review||{},limits=r.thresholds||{},behavior=r.applicableBehavior;
 const source=r.gazeInput||context.gazeInput||b.gazeInput;
 const simulated=!!source?.simulated||context.taskInputSource==='simulated_answers_gaze_playback_receipts'||c.gazeEvidence?.before?.source==='simulated';
 const desktop=source?.environment==='desktop_fixed_gaze'||source?.source==='simulated_fixed_gaze';
 const score=Object.hasOwn(r,'score')?(r.score==null?'Unavailable in current condition':fmt(r.score)):'Not recorded';
 const baseText=`Base proposal inputs · RF ${fmt(b.score)} · BA ${pct(b.behavior?.ba)} · ${fmt(b.behavior?.count)} trials`;
 const currentText=behavior?`Current-condition review · ${fmt(behavior.count)} / ${fmt(limits.behaviorTrials)} eligible trials · BA ${pct(behavior.ba)} · RF ${score}`:`Current-condition review · ${score==='Not recorded'?'Inputs not recorded':'RF '+score}`;
 const reason=r.reason||b.reason,hasEyes=!!(r.gaze||r.playbackGaze);
 const gateText=explain(reason)+(hasEyes?'':' Eye checks were not evaluated on this branch.');
 const level=c.receipt?.level,levelKnown=level===0||level===1;
 const command=c.delivered==='HOLD'?'Keep video '+(levelKnown?(level?'ON':'OFF'):b.level===1?'ON':b.level===0?'OFF':'unchanged'):'Request video '+(c.delivered||'unknown');
 const acknowledgment=c.receipt?'Command acknowledged':'No matching command receipt';
 const flow=[
  {label:'1. Base controller proposal',action:actionText(b.action,b.level),detail:'Initial suggestion from behavior or model evidence.'},
  {label:c.stage==='layer_a'?'2. Evidence review (observation only)':'2. Patent evidence review',action:actionText(r.recommended,b.level),detail:c.stage==='layer_a'?'Recorded for comparison; does not change the final command.':'Additional evidence checks; the recorded reason is shown above.'},
  {label:'3. Final command to Unity',action:actionText(c.delivered,levelKnown?level:b.level),detail:'Command sent by the backend; this does not confirm video playback.'}
 ];
 const holdExplanation=c.delivered==='HOLD'?(b.action==='ON'?'The proposed video opening was not sent. ':b.action==='OFF'?'The proposed video closure was not sent. ':'')+'HOLD keeps the video condition unchanged; the task continues.':'';
 return {baseText,currentText,gateText,hasEyes,source,simulated,desktop,command,acknowledgment,
 flow,holdExplanation,
 thresholds:`Recorded thresholds · ON ≤ ${fmt(limits.onScore??b.lowThreshold)} · OFF ≥ ${fmt(limits.offScore??b.highThreshold)}`};
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
 for(const [value,label]of [['before','At decision'],['after','After decision']]){const button=add(controls,'button',label);button.type='button';button.setAttribute('aria-pressed',state.period===value);button.onclick=()=>{state.period=value;state.frame=null;render(box,c,context);};}
 const e=c.gazeEvidence?.[state.period],rows=(context.trials||[]).filter(t=>e?.trials?.includes(t.id));
 const numbers=rows.map(t=>t.trial),range=numbers.length?`Trials ${Math.min(...numbers)}–${Math.max(...numbers)}`:'No matching trials';
 add(panel,'p',`${range} · ${e?fmt(e.eligibleSeconds)+' eligible seconds':'Interval unavailable'} · ${state.period==='after'?'Later observations':info.hasEyes?'Used by recorded review':'Context only'}`,'de-muted');
 if(state.period==='after')add(panel,'p','Follow-up only. RF and BA above remain the original decision inputs.','de-period-note');
 const names={task:'Task',movie:'Movie',other:'Other',missing:'Missing / invalid'},colors={task:'#477d60',movie:'#7b94b6',other:'#bea97b',missing:'#bd8278'};
 if(info.simulated){
  const empty=add(panel,'div',null,'de-empty');add(empty,'strong',info.desktop?'Real gaze not collected in Desktop mode':'Simulated gaze · no measured eye positions');
  add(empty,'p',info.desktop?`Configured assumptions · ${pct(info.source?.validRatio)} valid coverage · ${pct(info.source?.taskShare)} task / valid time`:'See the recorded simulation provenance for its gaze assumptions.','de-muted');
  add(empty,'p',state.period==='before'?(info.hasEyes?'The recorded review used simulated gaze values.':'Eye checks were not evaluated on this branch.'):'No measured spatial follow-up is available.','de-muted');
 }else{
  const grid=add(panel,'div',null,'de-eye-grid'),left=add(grid,'div'),right=add(grid,'div');
  const frames=e?.frames||[];let frame=frames.find(f=>f.frame===state.frame)||frames.slice().sort((a,b)=>b.mappedSeconds-a.mappedSeconds)[0];
  if(frames.length>1){const label=add(left,'label','Projection frame'),select=add(label,'select');for(const f of frames)select.add(new Option(`Frame ${f.frame} · ${fmt(f.mappedSeconds)} s mapped`,String(f.frame)));select.value=String(frame.frame);select.onchange=()=>{state.frame=Number(select.value);render(box,c,context);};}
  const plot=svg(left,'svg',{viewBox:'0 0 600 360',role:'img','aria-label':frame?.bins.length?'Recorded tracked-eye dwell density on the front reference plane':'No recorded spatial gaze data',class:'de-heatmap'});
  svg(plot,'rect',{width:600,height:360,fill:'#f6f7f2'});
  if(frame){const l=frame.layout,xx=x=>600*(x-l.minX)/(l.maxX-l.minX),yy=y=>360*(l.maxY-y)/(l.maxY-l.minY);
   for(const [key,label]of [['movie','Movie'],['task','Task area']]){const r=l[key];if(!r?.available)continue;svg(plot,'rect',{x:xx(r.x),y:yy(r.y+r.height),width:600*r.width/(l.maxX-l.minX),height:360*r.height/(l.maxY-l.minY),fill:'#fff',stroke:'#bdcbbf','stroke-width':1.5});const t=svg(plot,'text',{x:xx(r.x)+6,y:yy(r.y+r.height)+17,fill:'#4c6254','font-size':13});t.textContent=label;}
   const max=Math.max(...frame.bins.map(bin=>bin[2]),.001);
   for(const [x,y,seconds]of frame.bins)svg(plot,'rect',{x:x*12.5,y:y*11.25,width:12.5,height:11.25,rx:4,fill:'#bd682c',opacity:.12+.78*seconds/max});
   add(left,'p',`Frame ${frame.frame} · ${fmt(frame.mappedSeconds)} s mapped · ${fmt(frame.outsideSeconds)} s outside view`,'de-muted');
   add(left,'p',`0–${fmt(max)} s per cell · Local color scale; compare durations, not darkness across views.`,'de-muted');
  }else{const t=svg(plot,'text',{x:300,y:180,'text-anchor':'middle',fill:'#65736a','font-size':18});t.textContent=!e?'Coordinates not recorded':!e.eligibleSeconds?'No eligible interval':'Coordinates unavailable';}
  add(left,'p','Front-plane projection · outlines show location, not playback state.','de-muted');
  add(right,'h4','Recorded time by region');const total=e?.eligibleSeconds||0,regions=e?.regionSeconds;
  if(total&&regions){const bar=add(right,'div',null,'de-region-bar');bar.setAttribute('role','img');bar.setAttribute('aria-label',Object.entries(regions).map(([k,v])=>`${names[k]} ${pct(v/total)}`).join(', '));
   const list=add(right,'div',null,'de-region-list');for(const [k,name]of Object.entries(names)){const segment=add(bar,'span');segment.style.width=100*regions[k]/total+'%';segment.style.background=colors[k];add(list,'span',name);add(list,'strong',pct(regions[k]/total));}
   const valid=total-regions.missing;add(right,'p',`${pct(valid/total)} valid / eligible time · ${valid?pct(regions.task/valid):'Unavailable'} task / valid time`,'de-muted');
  }else add(right,'p',e?'No eligible recorded interval.':'Gaze audit not available in this report.','de-muted');
  const actual=c.review?.playbackGaze||c.review?.gaze;
  if(state.period==='before')add(right,'p',actual?`Used by review: ${pct(actual.gaze_valid_ratio)} valid · ${pct(actual.task_gaze_ratio)} task / valid.`:'Eye checks were not evaluated on this branch.','de-period-note');
  add(right,'p','Missing / invalid includes gaps in the audit. Spatial coverage can be lower because coordinates are missing, late or in another frame.','de-muted');
 }
 if(state.period==='after'){
  add(panel,'p',rows.length?`${rows.filter(t=>t.scored).length} scored follow-up trials · ${rows.filter(t=>t.scored&&!t.correct).length} incorrect or omitted. Later observations do not establish that the earlier decision was correct.`:'No completed follow-up trials recorded yet.','de-follow');
  const next=context.checkpoints?.find(x=>x.request===e?.nextRequest);if(next)add(panel,'p',`Next recorded decision · Trial ${next.trial} · ${next.delivered} · ${explain(next.review?.reason)}`,'de-muted');
 }
 const timeline=add(box,'details',null,'de-timeline');timeline.open=state.timeline;add(timeline,'summary','Time-aligned evidence');timeline.ontoggle=()=>state.timeline=timeline.open;
 add(timeline,'p',e?.basis||'Interval basis not recorded.','de-muted');
 if(!rows.length)add(timeline,'p','No matching trials recorded.','de-muted');
 else{const wrap=add(timeline,'div',null,'table-scroll'),table=add(wrap,'table',null,'evidence-table'),tr=add(table,'tr');for(const h of ['Trial','Response','Decoded video','Pre-response gaze'])add(tr,'th',h);
  for(const t of rows){const row=add(table,'tr');add(row,'td',t.trial);add(row,'td',!t.scored?'Unscored':t.response==null?'Omitted':t.correct?'Correct':'Incorrect');add(row,'td',t.media?.some(m=>m.visual)?'Overlaps trial':'No decoded overlap');const cell=add(row,'td');if(info.simulated){cell.textContent='Not measured';continue;}const line=add(cell,'div',null,'de-region-bar');for(const [a,b,target]of t.preResponse){const seg=add(line,'span');seg.style.width=100*(b-a)/Math.max(.001,t.preResponseSeconds)+'%';seg.style.background=colors[target]||colors.other;seg.title=`${names[target]||target}: ${fmt(b-a)} s`;}}}
}
setInterval(()=>{for(const [box,v]of liveBoxes){if(!box.isConnected){liveBoxes.delete(box);continue;}render(box,v.c,v.context);}},5000);

function render(box,c,context={}){
 let state=views.get(box);if(!state||state.request!==c?.request){state={request:c?.request,period:'before',frame:null,timeline:false,criteria:false};views.set(box,state);}
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
 box.replaceChildren();if(!c){add(box,'p','No decision evidence yet. A checkpoint appears after Unity submits a decision request.','empty-state');return;}
 const b=c.base||{},r=c.review||{},a=c.stage==='layer_a'?'A':'B',isA=a==='A',delivered=c.delivered||'Unknown',reason=r.reason||b.reason;
 const info=describe(c,context);
 const lead=add(box,'div',null,'decision-lead');
 add(lead,'h3',`${a} · Trial ${c.trial??'—'} · ${info.command}`);
 add(lead,'p',(isA?'Observed review: ':'Recorded reason: ')+info.gateText);
 if(isA)add(lead,'p','A executes the base proposal. The review is observed only.','decision-context');
 const flow=add(box,'div',null,'decision-flow');
 for(const step of info.flow){const card=add(flow,'div',null,'decision-step');add(card,'div',step.label,'decision-step-label');add(card,'strong',step.action);add(card,'p',step.detail);}
 if(info.holdExplanation)add(box,'p',info.holdExplanation,'de-period-note');
 add(box,'p','Unity receipt: '+info.acknowledgment+'. Playback is recorded separately in the trial timeline.','decision-context');
 const facts=add(box,'div',null,'de-facts');numericFacts(facts,info.baseText);numericFacts(facts,info.currentText);numericFacts(facts,info.thresholds,'de-muted');
 const provenance=add(box,'details',null,'de-provenance');add(provenance,'summary','Execution & input source');
 const source=r.gazeInput||context.gazeInput||b.gazeInput;
 add(provenance,'p',context.taskInputSource==='simulated_answers_gaze_playback_receipts'?'Eye input: Seeded simulated gaze; this is a software replay.':source?.simulated?'Eye input: Simulated gaze · fixed 75% valid coverage / 70% task share of valid time.':source?'Eye configuration: Quest Pro · see recorded coverage for acquisition evidence.':'Eye source: see session provenance; this record predates the explicit source field.','decision-context');
 add(provenance,'p','A command receipt does not confirm decoded playback. Inspect the trial timeline for recorded playback overlap.','decision-context');
 add(provenance,'p','Unity execution: '+(c.execution||'Unknown')+'. '+(delivered==='HOLD'?'Command keeps video '+(b.level===1?'on':b.level===0?'off':'unchanged')+'.':''),'decision-context');
 if(delivered==='HOLD'&&c.trial!=null){const stride=context.decisionStride;const next=stride?c.trial+stride:null;add(provenance,'p',reason==='failed_probe_phase_lock'?'Openings remain locked for this phase.':next&&context.trialBudget&&next<context.trialBudget?`Next scheduled check: trial ${next}. A later check may still hold.`:'Any further change is considered at the next eligible scheduled checkpoint.','decision-context');}
 const rows=(r.checks||[]).map(x=>({...x}));
 if(!rows.length)rows.push({name:'Recorded rule checks',value:null,status:'not evaluated'});
 if(!r.gaze&&!r.playbackGaze)rows.push({name:'Eye coverage / task share',value:null,status:'not evaluated',note:'This branch did not evaluate eyes'});
 spatialPanel(box,c,context,state);
 const criteria=add(box,'details');criteria.open=state.criteria;add(criteria,'summary','Criteria & source records');criteria.ontoggle=()=>state.criteria=criteria.open;
 add(criteria,'p','Recorded outcome · '+info.gateText,'de-period-note');
 add(criteria,'p','Context reconstructed from saved fields · '+info.currentText+'. Original checks and records below are unchanged.','de-muted');
 add(criteria,'p','Base proposals use behavioral OR model triggers. B reviews additional conditions in sequence; individual Passed rows are not independent authorization.','de-muted');
 const scroll=add(criteria,'div',null,'table-scroll'),table=add(scroll,'table',null,'evidence-table'),head=add(table,'thead'),hr=add(head,'tr');
 for(const name of ['Evidence','Observed','Criterion','Basis','Result'])add(hr,'th',name);
 const body=add(table,'tbody');
 let group='';
 for(const row of rows){const nextGroup=/^(Base |Match trials|Non-match trials)/.test(row.name)?'Base proposal':row.status==='not evaluated'?'Not evaluated':'Recorded review';if(nextGroup!==group){group=nextGroup;const heading=add(body,'tr'),cell=add(heading,'th',group);cell.colSpan=5;}const tr=add(body,'tr');add(tr,'td',row.name);const percent=/accuracy|eye coverage|share of valid/i.test(row.name);add(tr,'td',row.value==null?'—':percent?fmt(row.value*100)+'%':fmt(row.value),'de-observed');add(tr,'td',row.operator?row.operator+' '+(percent?fmt(row.threshold*100)+'%':fmt(row.threshold)):'—','de-observed');add(tr,'td',row.denominator==null?(row.note||'—'):fmt(row.denominator)+' '+(/eye|overlap|playback/i.test(row.name)?'s':'trials'));const td=add(tr,'td');add(td,'span',({'met':'Passed','outside threshold':'Not met','missing':'Missing data','not evaluated':'Not evaluated'})[row.status]||row.status,'result-badge '+String(row.status).replaceAll(' ','-'));}
 add(criteria,'p','Passed = condition met · Not met = outside the criterion · Missing data = required value unavailable · Not evaluated = branch not reached.','check-legend');
 const technical=add(criteria,'details');add(technical,'summary','Technical details · timestamps, source data and Unity receipt');add(technical,'pre',JSON.stringify(c,null,2));
}
window.EvidenceUI={render,explain,describe};
})();
