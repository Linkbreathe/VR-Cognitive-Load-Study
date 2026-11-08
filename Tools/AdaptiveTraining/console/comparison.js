'use strict';
const el = id => document.getElementById(id);
const endpoint = '/tests/comparison-report?' + new URLSearchParams({session:new URLSearchParams(location.search).get('session') || ''});
let data, filteredCheckpoints=[], currentRequest='';
const arm = s => s === 'layer_a' ? 'A' : 'B';
const explanations = {
  playback_score_rise_with_stable_behavior_and_task_gaze:'The score crossed the OFF threshold during video onset. Performance stayed stable and the eyes stayed on the task before the response.',
  playback_eye_tracking_insufficient:'The score crossed the OFF threshold. Too little pre-response eye tracking was available to defer closure.',
  playback_task_view_insufficient:'Tracking coverage passed, but too little valid eye time was directed at the task to defer closure.',
  insufficient_pre_response_playback:'Less than one second of decoded playback overlapped the eligible pre-response intervals.',
  eye_tracking_insufficient:'Too little valid eye tracking was available to authorize opening.',
  task_view_insufficient:'Task-directed eye time was below the opening criterion.'
};
const fmt = v => typeof v === 'number' ? String(Math.round(v*1000)/1000) : v ?? 'Unavailable';
function append(parent, tag, text) {
  const item = document.createElement(tag); item.textContent = text; parent.append(item); return item;
}
function details(parent, label, values) {
  const panel = document.createElement('details'); parent.append(panel);
  append(panel,'summary',label); append(panel,'pre',JSON.stringify(values,null,2));
}
function table(parent, headers, rows) {
  const t = document.createElement('table');t.className='evidence-table'; parent.append(t);
  const head = t.insertRow(); headers.forEach(h=>append(head,'th',h));
  for (const row of rows) { const tr=t.insertRow(); row.forEach(value=>append(tr,'td',fmt(value))); }
}
function chooseTrial(parent, trial) {
  const button=append(parent,'button','Trial '+trial.trial); button.onclick=()=>{
    el('trials').value=trial.id; drawTrial(); el('timeline').closest('details').open=true; el('timeline').scrollIntoView({block:'nearest'});
  };
}
function drawTrial() {
  if(!data)return;
  const box=el('timeline'); box.replaceChildren();
  if(data.gazeInput?.simulated)append(box,'p','Desktop mode uses fixed gaze aggregates for decisions. The timeline below contains only raw observations; missing tracking is not replaced with invented eye positions.');
  const t=data.trials.find(t=>t.id===el('trials').value); if(!t){append(box,'p','No closed trials recorded yet.');return;}
  const response=t.response===null?'Omitted':'Response at '+fmt(t.response)+' s';
  append(box,'p',response+' · Sphere onset '+fmt(t.start)+' s · Deadline '+fmt(t.deadline)+' s · '+(t.scored?(t.correct?'Correct':'Incorrect'):'Unscored'));
  append(box,'p','Valid eye tracking before response or deadline: '+fmt(t.validEyeSeconds)+' / '+fmt(t.preResponseSeconds)+' s');
  table(box,['From (s)','To (s)','Tracked target','Interval'],[
    ...t.preResponse.map(([a,b,target])=>[a,b,target,'Before response / deadline']),
    ...t.afterResponse.map(([a,b,target])=>[a,b,target,'After response'])]);
  details(box,'Actual playback and audio',t.media);
  details(box,'Clip boundaries (not controller ON events)',(data.segmentCuts || []).filter(c=>c.time>=t.start && c.time<=t.deadline));
  details(box,'Overlapping EEG windows',data.eegWindows.filter(w=>t.eegWindows.includes(w.id)));
}
function drawDecision() {
 if(!data)return;
 const c=data.checkpoints.find(r=>r.request===currentRequest);
 EvidenceUI.render(el('decision'),c,data);const box=el('decision-links');box.replaceChildren();if(!c)return;
 const r=c.review;
 const links=append(box,'details',null);append(links,'summary','Contributing trials & linked decisions');
 data.trials.filter(t=>t.stage===c.stage && (r.verificationWindowTrials || r.windowTrials)?.includes(t.trial-1)).forEach(t=>chooseTrial(links,t));
 for(const [key,label] of [[c.verificationRequest,'Open ten-trial verification'],[c.upgradeRequest,'Open original upgrade']])if(key)append(links,'button',label).onclick=()=>{el('arm-filter').value='all';el('decision-filter').value='all';filterDecisions(key);};
}
function selectCheckpoint(index){
 const rows=filteredCheckpoints,i=Math.max(0,Math.min(rows.length-1,Number(index)||0)),c=rows[i],slider=el('requests');
 currentRequest=c?.request||'';slider.max=String(Math.max(0,rows.length-1));slider.value=String(i);slider.disabled=!rows.length;
 const caption=c?`${arm(c.stage)} · Trial ${c.trial} · ${c.base.action} → ${fmt(c.delivered)}`:'No decisions match these filters';
 el('checkpoint-caption').textContent=caption;el('checkpoint-position').textContent=rows.length?`${i+1} / ${rows.length}`:'0 / 0';
 slider.setAttribute('aria-valuetext',rows.length?`Checkpoint ${i+1} of ${rows.length}: ${caption}`:caption);
 slider.style.setProperty('--checkpoint-fill',rows.length>1?`${100*i/(rows.length-1)}%`:'0%');
 const endpoint=c=>c?`${arm(c.stage)} · Trial ${c.trial}`:'';
 el('checkpoint-first').textContent=endpoint(rows[0]);el('checkpoint-last').textContent=rows.length>1?endpoint(rows.at(-1)):'';
 el('checkpoint-prev').disabled=!rows.length||i===0;el('checkpoint-next').disabled=!rows.length||i===rows.length-1;
 drawDecision();
}
function filterDecisions(preferred){
 if(!data)return;
 const previous=preferred||currentRequest;
 filteredCheckpoints=data.checkpoints.filter(c=>(el('arm-filter').value==='all'||c.stage===el('arm-filter').value)&&(el('decision-filter').value==='all'||el('decision-filter').value==='deferred'&&c.base.action!=='HOLD'&&c.review.recommended==='HOLD'||el('decision-filter').value==='changes'&&['ON','OFF'].includes(c.delivered)));
 selectCheckpoint(Math.max(0,filteredCheckpoints.findIndex(c=>c.request===previous)));
}
function cards(){
 const box=el('arm-cards');box.replaceChildren();
 for(const [stage,v] of Object.entries(data.summary)){
  const card=append(box,'section',null);card.className='card arm-card';append(card,'p',stage==='layer_a'?'BASE CONTROLLER · REVIEW OBSERVED':'PATENT LAYER · REVIEW ENFORCED').className='eyebrow';append(card,'h2','Round '+arm(stage));
  const checkpoints=data.checkpoints.filter(c=>c.stage===stage),metric=append(card,'div',null);metric.className='metrics';
  for(const [value,label] of [[checkpoints.length,'decision checkpoints'],[v.executedChanges,'confirmed changes'],[fmt(v.videoSeconds)+' s','recorded video duration'],[v.deferredON+v.deferredOFF,stage==='layer_a'?'hypothetical deferrals':'deferred proposals']]){const item=append(metric,'div',null);item.className='metric';append(item,'strong',value);append(item,'span',label);}
  const reasons={};for(const c of checkpoints)if(c.base.action!=='HOLD'&&c.review.recommended==='HOLD')reasons[c.review.reason]=(reasons[c.review.reason]||0)+1;
  const list=append(card,'div',null);list.className='reason-list';for(const [reason,count] of Object.entries(reasons)){const row=append(list,'div',null);row.className='reason-row';append(row,'span',EvidenceUI.explain(reason));append(row,'strong',count);}
  if(!Object.keys(reasons).length)append(list,'p','No deferred proposals recorded.').className='muted';
 }
 const scope=typeof data.evidenceScope==='string'?data.evidenceScope:JSON.stringify(data.evidenceScope||'Unspecified');
 el('provenance').textContent='Evidence scope: '+scope+' · EEG source: '+(data.signalSource||'not recorded')+'. '+(data.taskInputSource==='simulated_answers_gaze_playback_receipts'?'Answers, gaze, playback and execution receipts are simulated. ':'')+(data.gazeInput?.simulated?'Eye inputs are fixed simulated aggregates. ':'')+'Check acquisition/replay provenance before interpreting A/B differences. Engineering and simulated outcomes do not establish participant benefit.';
}
async function refresh(){
  el('refresh').disabled=true;
  try{
    const res=await fetch(endpoint);const result=await res.json();if(!res.ok)throw Error(result.error);
    data=result;
    el('identity').textContent=[data.participant,data.session,data.mode,data.model || 'No control model'].join(' · ');
    if(data.gazeInput)el('identity').textContent+=' · '+(data.gazeInput.simulated?'Desktop / simulated gaze':'Quest Pro configured');
    const box=el('totals');box.replaceChildren();
    table(box,['Arm','Deferred ON / proposed','Deferred OFF / proposed','Deferral episodes','Eye coverage insufficient','Executed changes','Video (s)','Post-upgrade errors / observed'],
      Object.entries(data.summary).map(([s,v])=>[arm(s)+(s==='layer_a'?' · hypothetical':' · enforced'),v.deferredON+' / '+v.ON,v.deferredOFF+' / '+v.OFF,v.deferredEpisodes,v.missingEyeChecks,v.executedChanges,v.videoSeconds,v.postUpgradeErrors+' / '+v.postUpgradeObserved]));
    const prev=currentRequest, trial=el('trials').value;
    filterDecisions(prev);cards();
    el('trials').replaceChildren(...data.trials.map(t=>new Option(arm(t.stage)+' · Trial '+t.trial,t.id)));

    if(data.trials.some(t=>t.id===trial))el('trials').value=trial;
    drawDecision();drawTrial();
    el('status').textContent=data.checkpoints.length+' checkpoints · '+data.trials.length+' closed trials';
  }catch(error){el('status').textContent=error.message;}finally{el('refresh').disabled=false;}
}
el('arm-filter').onchange=()=>filterDecisions();el('decision-filter').onchange=()=>filterDecisions();el('download').href=endpoint;el('refresh').onclick=refresh;el('trials').onchange=drawTrial;el('requests').oninput=()=>selectCheckpoint(el('requests').value);el('checkpoint-prev').onclick=()=>selectCheckpoint(Number(el('requests').value)-1);el('checkpoint-next').onclick=()=>selectCheckpoint(Number(el('requests').value)+1);refresh();
