'use strict';
const el = id => document.getElementById(id);
const endpoint = '/tests/comparison-report?' + new URLSearchParams({session:new URLSearchParams(location.search).get('session') || ''});
let data, filteredCheckpoints=[], currentRequest='';
const T = () => window.EvidenceUI;
const round = s => T().label('round', s);
const fmt = v => typeof v === 'number' ? String(Math.round(v*1000)/1000) : v ?? 'Unavailable';
const fixed = (v,n) => typeof v === 'number' && Number.isFinite(v) ? v.toFixed(n) : '—';
function append(parent, tag, text, cls) {
  const item = document.createElement(tag); if(text!=null)item.textContent = text; if(cls)item.className=cls; parent.append(item); return item;
}
function details(parent, label, values, empty) {
  const panel = document.createElement('details'); parent.append(panel);
  append(panel,'summary',label);
  if(Array.isArray(values)&&!values.length)append(panel,'p',empty,'muted small');else append(panel,'pre',JSON.stringify(values,null,2));
}
function table(parent, headers, rows) {
  const t = document.createElement('table');t.className='evidence-table'; parent.append(t);
  const head = t.insertRow(); headers.forEach(h=>append(head,'th',h));
  for (const row of rows) { const tr=t.insertRow(); row.forEach(value=>append(tr,'td',fmt(value))); }
}
/* Session IDs begin with their UTC creation time. */
function sessionTime(id){
  const m=/^(\d{4})(\d{2})(\d{2})T(\d{2})(\d{2})/.exec(id||'');if(!m)return '';
  const d=new Date(Date.UTC(+m[1],m[2]-1,+m[3],+m[4],+m[5]));
  return d.toLocaleDateString('en-GB',{day:'numeric',month:'short',year:'numeric',timeZone:'UTC'})+', '+m[4]+':'+m[5]+' UTC';
}
function chooseTrial(parent, trial) {
  const button=append(parent,'button','Trial '+trial.trial); button.type='button'; button.onclick=()=>{
    el('trials').value=trial.id; drawTrial(); el('timeline').closest('details').open=true; el('timeline').scrollIntoView({block:'nearest'});
  };
}
function drawTrial() {
  if(!data)return;
  const box=el('timeline'); box.replaceChildren();
  if(data.gazeInput?.simulated)append(box,'p','Desktop mode uses fixed gaze values for decisions. The table below lists only raw observations; missing tracking is never replaced with invented eye positions.','muted small');
  const t=data.trials.find(t=>t.id===el('trials').value); if(!t){append(box,'p','No completed trials recorded yet.');return;}
  const response=t.response===null?'Omitted':'Response at '+fmt(t.response)+' s';
  append(box,'p',response+' · Sphere onset '+fmt(t.start)+' s · Deadline '+fmt(t.deadline)+' s · '+(t.scored?(t.correct?'Correct':'Incorrect'):'Unscored'));
  append(box,'p','Valid eye tracking before response or deadline: '+fmt(t.validEyeSeconds)+' of '+fmt(t.preResponseSeconds)+' s');
  const target=v=>T().human(v);
  const rows=[...t.preResponse.map(([a,b,x])=>[a,b,target(x),'Before response / deadline']),...t.afterResponse.map(([a,b,x])=>[a,b,target(x),'After response'])];
  if(rows.length)table(box,['From (s)','To (s)','Gaze target','Interval'],rows);else append(box,'p','No gaze samples in this trial.','muted small');
  details(box,'Actual playback and audio',t.media,'No playback in this trial.');
  details(box,'Clip boundaries (content cuts, not controller ON events)',(data.segmentCuts || []).filter(c=>c.time>=t.start && c.time<=t.deadline),'No clip boundary in this trial.');
  details(box,'Overlapping EEG windows',data.eegWindows.filter(w=>t.eegWindows.includes(w.id)),'No EEG window overlaps this trial.');
}
function drawDecision() {
 if(!data)return;
 const c=data.checkpoints.find(r=>r.request===currentRequest);
 const lane=c?data.checkpoints.filter(x=>x.stage===c.stage):[];
 EvidenceUI.render(el('decision'),c,c?{...data,position:{index:lane.indexOf(c)+1,total:lane.length}}:data);
 const box=el('decision-links');box.replaceChildren();if(!c)return;
 const r=c.review;
 const links=append(box,'details',null);append(links,'summary','Trials in this window & linked decisions');
 data.trials.filter(t=>t.stage===c.stage && (r.verificationWindowTrials || r.windowTrials)?.includes(t.trial-1)).forEach(t=>chooseTrial(links,t));
 for(const [key,label] of [[c.verificationRequest,'Open ten-trial verification'],[c.upgradeRequest,'Open original opening']])if(key){const b=append(links,'button',label);b.type='button';b.onclick=()=>{el('arm-filter').value='all';el('decision-filter').value='all';filterDecisions(key);};}
}
/* Marker kind is read from recorded proposal, review recommendation and delivered command. */
function kind(c){
 if(['ON','OFF'].includes(c.delivered))return c.delivered.toLowerCase();
 if(['ON','OFF'].includes(c.base.action)&&c.delivered==='HOLD')return 'blocked';
 return 'hold';
}
const hypothetical=c=>c.stage==='layer_a'&&c.base.action!=='HOLD'&&c.review?.recommended==='HOLD';
function caption(c){
 const k=kind(c),words={on:'Video ON executed',off:'Video OFF executed',blocked:`${c.base.action==='ON'?'Video ON blocked':'Video OFF deferred'} by review`,hold:'No change'};
 return `${round(c.stage)} · Trial ${c.trial} · ${words[k]}${hypothetical(c)?' · B rules would hold':''}`;
}
/* Fractional trial position of a Unity time in one round: trial k spans [k-1, k). */
function clock(stage){
 const starts=(data.trials||[]).filter(t=>t.stage===stage).map(t=>[t.start,t.trial]).sort((a,b)=>a[0]-b[0]);
 return time=>{if(!starts.length||typeof time!=='number')return null;let i=starts.length-1;while(i>0&&starts[i][0]>time)i--;return starts[i][1]-1+(time-starts[i][0])/4.5;};
}
function drawTimeline(){
 const box=el('timeline-lanes');box.replaceChildren();if(!data)return;
 const stages=['layer_a','layer_b'].filter(s=>el('arm-filter').value==='all'||el('arm-filter').value===s);
 const budget=data.trialBudget||Math.max(1,...data.checkpoints.map(c=>c.trial||0));
 const pos=t=>Math.max(0,Math.min(100,100*t/budget));
 const included=new Set(filteredCheckpoints.map(c=>c.request));
 for(const stage of stages){
  const rows=data.checkpoints.filter(c=>c.stage===stage);if(!rows.length)continue;
  const at=clock(stage),place=c=>at(c.time)??c.trial;
  const lane=append(box,'div',null,'tl-lane');
  const name=append(lane,'div',null,'tl-name');append(name,'strong',round(stage));append(name,'small',T().label('roundRole',stage));
  const track=append(lane,'div',null,'tl-track'),band=append(track,'div',null,'tl-band');
  const clips=data.evidenceRelations?.clips?.[stage];
  if(clips){
   /* Actual decoded clips; each is split by the share of its supported / unresolved / disputed relations. */
   band.classList.add('clips');
   for(const clip of clips){
    const k=clip.states,total=k.SUPPORTED+k.UNRESOLVED+k.DISPUTED,seg=append(band,'span',null,'tl-clip');
    const a=at(clip.start)??clip.startTrial-1,z=at(clip.end)??clip.endTrial;
    seg.style.left=pos(a)+'%';seg.style.width=Math.max(.6,pos(z)-pos(a))+'%';
    seg.title=`Clip ${Math.round(clip.seconds*10)/10} s · trials ${clip.startTrial}–${clip.endTrial} · ${k.SUPPORTED} supported · ${k.UNRESOLVED} unresolved · ${k.DISPUTED} disputed`;
    if(total)['SUPPORTED','UNRESOLVED','DISPUTED'].forEach((state,i)=>{if(k[state])append(seg,'i',null,['s-sup','s-unr','s-dis'][i]).style.width=100*k[state]/total+'%';});
   }
  }else{
   let level=rows[0].base.level===1?1:0,start=level?0:null;
   for(const c of rows){
    if(c.delivered==='ON'&&!level){level=1;start=c.trial;}
    else if(c.delivered==='OFF'&&level){const seg=append(band,'i');seg.style.left=pos(start)+'%';seg.style.width=(pos(c.trial)-pos(start))+'%';level=0;}
   }
   if(level){const seg=append(band,'i');seg.style.left=pos(start)+'%';seg.style.width=(100-pos(start))+'%';}
  }
  for(const c of rows){
   const b=append(track,'button',null,'tl-mark '+kind(c)+(hypothetical(c)?' hyp':'')+(included.has(c.request)?'':' dim'));
   b.type='button';b.style.left=pos(place(c))+'%';b.dataset.request=c.request;b.title=caption(c);b.setAttribute('aria-label',caption(c));
   b.tabIndex=c.request===currentRequest?0:-1;if(c.request===currentRequest)b.setAttribute('aria-current','true');
   b.disabled=!included.has(c.request);b.onclick=()=>selectCheckpoint(filteredCheckpoints.findIndex(x=>x.request===c.request));
  }
  if(clips)scoreRow(lane,stage,clips,budget);
 }
 const axis=append(box,'div',null,'tl-axis');append(axis,'span');const ticks=append(axis,'div');
 for(const t of [0,Math.round(budget/2),budget]){const s=append(ticks,'span',t?'Trial '+t:'Start');s.style.left=pos(t)+'%';}
}

/* Score row: the workload score with the session cutoffs. A reverse check is a crossing of those cutoffs,
   drawn where it happened; a filled mark linked to a clip was traced back to that clip. Played clips are
   shaded, each followed by the 10 s the forward check looks at. Positions use fractional trials. */
function scoreRow(lane,stage,clips,budget){
 const ev=data.evidenceRelations,bands=ev.summary?.[stage]?.bands,ns='http://www.w3.org/2000/svg';
 const at=time=>clock(stage)(time)??0;
 const x=v=>10*Math.max(0,Math.min(100,100*v/budget));
 const windows=(data.eegWindows||[]).filter(w=>w.stage===stage&&w.valid&&typeof w.score==='number').sort((a,b)=>a.end-b.end);
 const scores=windows.map(w=>w.score).concat(bands?[bands.stable_max,bands.struggling_min]:[]);
 const lo=Math.min(...scores)-.02,hi=Math.max(...scores)+.02,y=v=>5+(hi-v)/(hi-lo)*46;
 const label=append(lane,'div',null,'tl-name tl-score-name');append(label,'small','Workload score');
 const row=append(lane,'div',null,'tl-score'),svg=document.createElementNS(ns,'svg');row.append(svg);
 svg.setAttribute('viewBox','0 0 1000 56');svg.setAttribute('preserveAspectRatio','none');svg.setAttribute('aria-hidden','true');
 const draw=(tag,attrs)=>{const e=document.createElementNS(ns,tag);for(const [k,v] of Object.entries(attrs))e.setAttribute(k,v);svg.append(e);return e;};
 for(const clip of clips){
  const a=x(at(clip.start)),b=x(at(clip.end)),tail=x(at(clip.end)+10/4.5);
  draw('rect',{x:a,y:0,width:Math.max(2,b-a),height:56,class:'sc-clip'});
  draw('rect',{x:b,y:0,width:Math.max(0,tail-b),height:56,class:'sc-tail'});
 }
 if(bands){
  draw('rect',{x:0,y:y(bands.struggling_min),width:1000,height:Math.max(.5,y(bands.stable_max)-y(bands.struggling_min)),class:'sc-buffer'});
  for(const v of [bands.stable_max,bands.struggling_min])draw('line',{x1:0,x2:1000,y1:y(v),y2:y(v),class:'sc-cut'});
 }
 let d='',last=null;
 for(const w of windows){const px=x(at(w.end));d+=(last===null||w.start-last>12?'M':'L')+px.toFixed(1)+','+y(w.score).toFixed(1)+' ';last=w.end;}
 draw('path',{d,class:'sc-line'});
 const byId=Object.fromEntries((data.eegWindows||[]).map(w=>[w.id,w]));
 /* The selected decision: a guide at its time and the score window it actually used. */
 const chosen=data.checkpoints.find(c=>c.request===currentRequest&&c.stage===stage);
 if(chosen){
  const used=byId[chosen.evidenceRelations?.signal],gx=x(at(chosen.time));
  if(used){draw('rect',{x:x(at(used.start)),y:0,width:Math.max(2,x(at(used.end))-x(at(used.start))),height:56,class:'sc-used'});}
  draw('line',{x1:gx,x2:gx,y1:0,y2:56,class:'sc-guide'});
  if(used&&typeof used.score==='number'){const ring=append(row,'span',null,'sc-ring');ring.style.left=(x(at(used.end))/10)+'%';ring.style.top=(y(used.score)/56*100)+'%';
   ring.title=`Score used by this decision: ${Math.round(used.score*1000)/1000} (window ended ${Math.round((chosen.time-used.end)*10)/10} s before the decision)`;}
 }
 for(const m of ev.reverseMarks?.[stage]||[]){
  const w=byId[m.signal];if(!w)continue;
  const px=x(at(w.end)),py=y(w.score),clip=clips.find(c=>c.id===m.claimedEvent);
  if(clip){const end=x(at(clip.end));if(px>end)draw('line',{x1:end,x2:px,y1:py,y2:py,class:'sc-link'});}
  const dot=append(row,'span',null,'sc-dot '+(m.direction==='down'?'down':'up')+(m.claimedEvent?' linked':''));
  dot.style.left=(px/10)+'%';dot.style.top=(py/56*100)+'%';
  dot.title=`Reverse check · score ${m.direction==='down'?'fell into stable':'rose into struggling'} at ${Math.round(w.score*1000)/1000}`+
   (clip?` · traced back to the clip in trials ${clip.startTrial}–${clip.endTrial}`:' · no clip played before it');
 }
}
function selectCheckpoint(index){
 const rows=filteredCheckpoints,i=Math.max(0,Math.min(rows.length-1,Number(index)||0)),c=rows[i];
 currentRequest=c?.request||'';
 el('checkpoint-position').textContent=rows.length?`${i+1} of ${rows.length} shown · ${caption(c)}`:'No decisions match these filters';
 el('checkpoint-caption').textContent=c?caption(c):'No decisions match these filters';
 el('checkpoint-prev').disabled=!rows.length||i===0;el('checkpoint-next').disabled=!rows.length||i===rows.length-1;
 drawTimeline();drawDecision();
}
function step(delta){const i=filteredCheckpoints.findIndex(c=>c.request===currentRequest);selectCheckpoint(i+delta);el('timeline-lanes').querySelector('[aria-current]')?.focus();}
function filterDecisions(preferred){
 if(!data)return;
 const previous=preferred||currentRequest;
 filteredCheckpoints=data.checkpoints.filter(c=>(el('arm-filter').value==='all'||c.stage===el('arm-filter').value)&&(el('decision-filter').value==='all'||el('decision-filter').value==='deferred'&&c.base.action!=='HOLD'&&c.review.recommended==='HOLD'||el('decision-filter').value==='changes'&&['ON','OFF'].includes(c.delivered)));
 selectCheckpoint(Math.max(0,filteredCheckpoints.findIndex(c=>c.request===previous)));
}
/* Executed ON/OFF are derived from proposed minus applied holds, shown only when they reconcile with the recorded total. */
function executedSplit(stage,v){
 const enforced=stage==='layer_b',on=v.ON-(enforced?v.deferredON:0),off=v.OFF-(enforced?v.deferredOFF:0);
 return on+off===v.executedChanges&&on>=0&&off>=0?`${on} ON · ${off} OFF`:'';
}
function summary(){
 const box=el('totals');box.replaceChildren();
 const stages=Object.keys(data.summary).filter(s=>['layer_a','layer_b'].includes(s));
 const t=document.createElement('table');t.className='compare-table';box.append(t);
 const head=t.createTHead().insertRow();append(head,'th');
 for(const s of stages){const th=append(head,'th');append(th,'strong',round(s));append(th,'span',T().label('roundRole',s));}
 const body=t.createTBody(),maxVideo=Math.max(...stages.map(s=>data.summary[s].videoSeconds||0),1);
 const row=(label,cells)=>{const tr=body.insertRow();append(tr,'th',label).scope='row';for(const cell of cells){const td=tr.insertCell();append(td,'span',cell.main,'big'+(cell.muted?' muted':''));if(cell.sub)append(td,'span',cell.sub,'sub');if(cell.bar!=null){const bar=append(td,'span',null,'bar');append(bar,'i').style.width=cell.bar+'%';}if(cell.stack){const total=cell.stack.reduce((a,b)=>a+b,0)||1,bar=append(td,'span',null,'bar stack');cell.stack.forEach((v,i)=>{append(bar,'i',null,['s-sup','s-unr','s-dis'][i]).style.width=100*v/total+'%';});}}};
 row('Video on screen',stages.map(s=>({main:fixed(data.summary[s].videoSeconds,1)+' s',bar:100*(data.summary[s].videoSeconds||0)/maxVideo})));
 row('Video changes executed',stages.map(s=>({main:String(data.summary[s].executedChanges),sub:executedSplit(s,data.summary[s])})));
 row('Proposals held by review',stages.map(s=>{const v=data.summary[s],held=v.deferredON+v.deferredOFF;return s==='layer_a'?{main:String(held),muted:true,sub:'B rules would hold · not applied'}:{main:`${held} of ${v.ON+v.OFF}`,sub:`${v.deferredON} ON · ${v.deferredOFF} OFF`};}));
 row('Hold episodes (consecutive checks)',stages.map(s=>({main:String(data.summary[s].deferredEpisodes)})));
 row('Held for insufficient eye data',stages.map(s=>({main:String(data.summary[s].missingEyeChecks)})));
 row('Errors in the 10 trials after each ON',stages.map(s=>{const v=data.summary[s];return {main:v.postUpgradeObserved?Math.round(100*v.postUpgradeErrors/v.postUpgradeObserved)+'%':'—',sub:`${v.postUpgradeErrors} of ${v.postUpgradeObserved} trials`};}));
 row('Decision checks',stages.map(s=>({main:String(data.checkpoints.filter(c=>c.stage===s).length)})));
 if(stages.some(s=>data.summary[s].support))row('Openings · normal / logged probe',stages.map(s=>{const v=data.summary[s].support;return v?{main:`${v.normalOn} / ${v.probeOn}`,sub:`${v.probePassed} passed · ${v.probeFailed} failed · ${v.probeInconclusive??0} inconclusive · ${v.probeWindowClosed} held near round end`}:{main:'—',muted:true,sub:'Not applied in this round'};}));
 const ev=data.evidenceRelations;
 if(ev){
  const group=body.insertRow();group.className='group';const th=append(group,'th','Evidence relations');th.colSpan=stages.length+1;
  row('Forward checks · video → score',stages.map(s=>({main:`${ev.summary[s].clips} clips`,sub:`${ev.summary[s].relatedWindows} score windows related`})));
  row('Relation states',stages.map(s=>{const k=ev.summary[s].states;return {main:`${k.SUPPORTED} · ${k.UNRESOLVED} · ${k.DISPUTED}`,sub:'supported · unresolved · disputed',stack:[k.SUPPORTED,k.UNRESOLVED,k.DISPUTED]};}));
  row('Reverse checks · score → sources',stages.map(s=>{const r=ev.summary[s].reverse;return {main:String(r.total),sub:r.total?`${r.up} up · ${r.down} down · ${r.withClip} with a clip`:'no stable ↔ struggling crossing'};}));
  const first=stages.map(s=>ev.summary[s]).find(x=>x.bands),unresolved=stages.reduce((a,s)=>a+ev.summary[s].unresolvedDifferentVideoState,0);
  const note=append(box,'p',null,'evidence-note');
  note.textContent=(first?.bands?`Reverse bands follow the session cutoffs: stable ≤ ${fixed(first.bands.stable_max,2)}, struggling ≥ ${fixed(first.bands.struggling_min,2)}, minimum change ${fixed(first.bands.min_change,2)}. `:'This session records no reverse bands; the engine used 0.45 / 0.65. ')+
   `${unresolved} unresolved relations pair a score window with a clip in a different video state. Audio exposure is the software playback state; visual exposure needs tracked eyes. Relations show timing and exposure only and are not used by any decision.`;
 }
 const cards=el('arm-cards');cards.replaceChildren();
 for(const s of stages){
  const card=append(cards,'section',null,'arm-card');append(card,'p',s==='layer_a'?'WHY B RULES WOULD HOLD · ROUND A':'WHY THE REVIEW HELD · ROUND B','eyebrow');
  const reasons={};for(const c of data.checkpoints.filter(c=>c.stage===s))if(c.base.action!=='HOLD'&&c.review.recommended==='HOLD')reasons[c.review.reason]=(reasons[c.review.reason]||0)+1;
  const list=append(card,'div',null,'reason-list');
  for(const [reason,count] of Object.entries(reasons).sort((a,b)=>b[1]-a[1])){const r=append(list,'div',null,'reason-row');append(r,'strong',count);append(r,'span',EvidenceUI.explain(reason));}
  if(!Object.keys(reasons).length)append(list,'p','No proposals were held.','muted');
 }
}
function header(){
 el('report-kind').textContent='SESSION REPORT · '+T().label('mode',data.mode).toUpperCase();
 el('report-title').textContent=[data.participant,sessionTime(data.session)].filter(Boolean).join(' · ');
 const chips=el('identity');chips.replaceChildren();
 const chip=(text,cls)=>append(chips,'span',text,'chip'+(cls?' '+cls:''));
 if(data.gazeInput)chip(data.gazeInput.simulated?'Desktop · simulated gaze':'Quest Pro · tracked eyes',data.gazeInput.simulated?'sim':'');
 chip(T().label('scope',typeof data.evidenceScope==='string'?data.evidenceScope:'unspecified'));
 if(data.trialBudget)chip(data.trialBudget+' trials per round');
 const th=data.currentScoreThresholds||data.initialScoreThresholds;
 if(th)chip(`ON ≤ ${fixed(th.on,2)} · OFF ≥ ${fixed(th.off,2)}`,'num');
 if(data.thresholdHistory?.length)chip(`Thresholds edited ${data.thresholdHistory.length}×`,'sim');
 const scope=typeof data.evidenceScope==='string'?data.evidenceScope:JSON.stringify(data.evidenceScope||'unspecified');
 el('provenance').textContent=[T().label('scope',scope),T().label('signal',data.signalSource||'not recorded')+(data.syntheticSignal?' (synthetic)':''),data.gazeInput?.simulated?`simulated gaze (${Math.round(100*(data.gazeInput.validRatio??.75))}% valid, ${Math.round(100*(data.gazeInput.taskShare??.7))}% on task)`:null].filter(Boolean).join(' · ')+'. '+
  (data.taskInputSource==='simulated_answers_gaze_playback_receipts'?'Answers, gaze, playback and Unity receipts are simulated. ':'')+
  'This report shows what each round’s controller did. It is not evidence of participant benefit.';
 const facts=el('session-facts');facts.replaceChildren();
 for(const [k,v] of [['Session ID',data.session],['Participant',data.participant],['Run option',T().label('mode',data.mode)],['Workload model',data.model||'No control model'],['Model hash',data.modelHash],['Decision policy',data.configRevision],['Review policy',data.reviewRevision],['Gaze evidence',data.spatialEvidenceRevision],['EEG source',data.signalSource],['Initial thresholds',data.initialScoreThresholds?`ON ≤ ${data.initialScoreThresholds.on} · OFF ≥ ${data.initialScoreThresholds.off}`:null]])if(v!=null&&v!==''){append(facts,'dt',k);append(facts,'dd',v);}
}
async function refresh(){
  el('refresh').disabled=true;
  try{
    const res=await fetch(endpoint);const result=await res.json();if(!res.ok)throw Error(result.error);
    data=result;header();summary();
    const prev=currentRequest, trial=el('trials').value;
    filterDecisions(prev);
    el('trials').replaceChildren(...data.trials.map(t=>new Option(round(t.stage)+' · Trial '+t.trial,t.id)));
    if(data.trials.some(t=>t.id===trial))el('trials').value=trial;
    drawTrial();
    el('status').textContent=data.checkpoints.length+' decision checks · '+data.trials.length+' completed trials';
  }catch(error){el('status').textContent='Report unavailable: '+error.message;}finally{el('refresh').disabled=false;}
}
el('arm-filter').onchange=()=>filterDecisions();el('decision-filter').onchange=()=>filterDecisions();el('download').href=endpoint;el('refresh').onclick=refresh;el('trials').onchange=drawTrial;
el('checkpoint-prev').onclick=()=>step(-1);el('checkpoint-next').onclick=()=>step(1);
el('timeline-lanes').addEventListener('keydown',e=>{if(e.key==='ArrowRight'||e.key==='ArrowLeft'){e.preventDefault();step(e.key==='ArrowRight'?1:-1);}else if(e.key==='Home'||e.key==='End'){e.preventDefault();selectCheckpoint(e.key==='Home'?0:filteredCheckpoints.length-1);el('timeline-lanes').querySelector('[aria-current]')?.focus();}});
refresh();
