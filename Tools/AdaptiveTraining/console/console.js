"use strict";
(() => {
const root=document.getElementById("evidence-console");
const q=s=>root.querySelector(s), all=s=>[...root.querySelectorAll(s)];
const state={page:(["guide","physiology","decision"].includes(location.hash.slice(1))?location.hash.slice(1):"decision"),stage:0,node:"guard",feature:"theta",session:"",request:"",window:"",channel:0,rawChannel:"all",gazeMode:"distribution"};
state.stage=({practice:0,calibration:1,comparison:2})[sessionStorage.getItem('study.setupStage')]??0;
const forms={"calibration":"https://docs.google.com/forms/d/e/1FAIpQLSeghD4XMzg3o9_Z0C3wX79ns8VwIqtdPLRScHXn829bPKI0_A/viewform","training":"https://docs.google.com/forms/d/e/1FAIpQLScKnSC3dnP8edc19qBj5Zbs2sRH62p1eQ5s9zbi9y5T9LTlEQ/viewform"};
 const guideStages=[
 {name:'Practice',time:'Independent practice',intro:'Learn the controls before collecting a comparison.',actions:['In Study setup, choose Practice and select Task only or Monitor Mindtooth EEG. Create the assignment.','Open the configured Unity task. Complete placement and A/B response checks, then press Enter / X.','Follow the task prompts. Practice feedback and any permitted skip follow the existing Unity protocol.'],note:'Practice does not train the workload model or create an A/B result.'},
 {name:'Calibration',time:'3 min forest + 3 min arithmetic',intro:'Train the workload model (Personal RF) used in both comparison rounds.',actions:['In Study setup, choose Calibration → Train workload model · forest / arithmetic. Confirm the participant and EEG stream, then create the assignment.','Complete Unity preparation. Follow Relax and Calculate prompts; use A for present and B for absent during arithmetic.','After training, review the saved model and its calibration-recommended video thresholds. Select that model in Adaptive Comparison.'],note:'These recommendations describe held-out calibration windows. You can edit them for a later comparison; they do not guarantee video openings.'},
 {name:'Adaptive Comparison',time:'Two independently reviewed rounds',intro:'Round A executes every proposal and only observes the review. Round B enforces the review.',actions:['Choose a saved workload model, Desktop or Quest Pro, the round order and initial video thresholds in Study setup. Rehearsal checks task operation without EEG or a model.','Create the assignment, then complete preparation in Unity and press Enter / X. Follow the live workload score and decisions in Run monitor.','Complete each round and the questionnaire/rest prompts shown by Unity. Use the current session forms; confirm submission separately before continuing.','After both rounds, open the Session report. Inspect each proposal, review, command, Unity receipt and the matching gaze interval.'],note:'New comparisons use 60 stimuli per round, a check every two trials and five scored trials per decision. Existing sessions retain their recorded configuration. Threshold edits take effect only after Apply thresholds.'}
 ];
 function guide(){
  const config=data?.task?.config||{},s=guideStages[state.stage]||guideStages[0];
  const environment=config.comparisonEnvironment||state.guideEnvironment||sessionStorage.getItem('study.environment')||'quest_pro',desktop=environment==='desktop_fixed_gaze';
  q('.ec-stage-nav').innerHTML=guideStages.map((stage,index)=>`<button type="button" class="ec-stage cursor-interaction" data-stage="${index}" aria-pressed="${index===state.stage}"><span class="ec-stage-number">${index+1}</span><span>${stage.name}</span></button>`).join('');
  q('.ec-guide-reminders').innerHTML=`<h3>${desktop?'Desktop controls':'Quest Pro controls'}</h3><p>${desktop?'Place task: C · Respond: A / B · Continue: Enter · Pause/resume: P. No headset is required.':'Place task: left trigger · Respond: A / B · Continue: X · Pause/resume: Y. Enable eye permission and confirm tracked eyes.'}</p><p>${desktop?'Eye inputs use fixed simulated assumptions; no real heatmap is collected.':'Tracked eyes are required for genuine gaze evidence. Configuration alone does not establish acquisition.'}</p><p>For EEG sessions, keep electrodes in place. Follow Unity prompts when pausing or recording equipment movement.</p>`;
  const training=safeForm(config.trainingFormUrl),preference=safeForm(config.preferenceFormUrl);
  q('#ec-guide-content').innerHTML=`<section class="ec-panel ec-guide-section"><div class="ec-guide-title"><h2>${s.name}</h2><span class="ec-tag">${s.time}</span></div><label>Instructions for <select id="guide-environment" ${config.comparisonEnvironment?'disabled':''}><option value="desktop_fixed_gaze" ${desktop?'selected':''}>Desktop</option><option value="quest_pro" ${!desktop?'selected':''}>Quest Pro</option></select></label><p class="ec-small">${config.comparisonEnvironment?'Environment from the selected session.':'Reference only; set the actual environment in Study setup.'}</p><p>${s.intro}</p><ol>${s.actions.map(a=>`<li>${a}</li>`).join('')}</ol><div class="ec-guide-note">${s.note}</div><p><a href="/tests?stage=${['practice','calibration','comparison'][state.stage]||'practice'}">Open this stage in Study setup ↗</a></p><details><summary>Connection & recording</summary><p>Start Mindtooth EEG streaming when required. In Unity, use Evidence VR → Signal Sources to select the intended stream. Receiving data and model scoring are separate states. Closing the website does not stop acquisition.</p><p>Preserve the session ID, Unity records and service records. A command receipt confirms acknowledgment, not decoded video playback.</p></details>${state.stage===2?`<details><summary>Current A/B questionnaires</summary><p>Use the seven-point Raw TLX / UEQ+ form and final preference form specified by the session. Follow Unity for round order, rest and completion. Historical protocols retain their recorded questionnaire version.</p>${training?`<p><a href="${training}" target="_blank" rel="noopener noreferrer">Session training questionnaire ↗</a></p>`:'<p>Connect the session to display its configured form links.</p>'}${preference?`<p><a href="${preference}" target="_blank" rel="noopener noreferrer">Session final preference ↗</a></p>`:''}</details>`:''}</section><p class="ec-guide-legacy">Earlier allocations keep their own recorded protocol. Its steps are in Docs/ExperimentGuide.md. <a href="/tests?legacy">Open legacy session options ↗</a></p>`;
  q('#guide-environment').onchange=e=>{state.guideEnvironment=e.target.value;guide();};
 }
let data=null, offline=true, actualSession=null, guideKey="";
const esc=v=>String(v??'—').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const human=v=>v?String(v).replaceAll('_',' ').replace(/^./,c=>c.toUpperCase()):'—';
const fmt=(v,n=2)=>typeof v==='number'&&Number.isFinite(v)?v.toFixed(n):'—';
const text=(selector,value)=>{q(selector).textContent=value??'—';};
const pretty=v=>`<details><summary>Recorded detail</summary><pre>${esc(JSON.stringify(v??{},null,2))}</pre></details>`;
const kv=(name,value)=>`<div class="ec-kv"><span>${esc(name)}</span><b>${esc(value)}</b></div>`;
const tone=name=>getComputedStyle(root).getPropertyValue(name).trim()||'#1f3a5f';
/* Readable check label; the request ID stays available as the row tooltip. */
const checkLabel=x=>{const id=x.delivered?.requestId||'',stage=(/:(layer_[ab]):/.exec(id)||[])[1]||x.review?.stage;return `${stage?EvidenceUI.label('round',stage)+' · ':''}Trial ${x.review?.trial??x.base?.lastTrial??'—'}`;};
const windowKey=w=>`${w.stage}:${w.trial}`;
const featureInfo={theta:{index:0,name:'Theta relative power',description:'Natural log of 4–8 Hz power relative to 1–30 Hz, averaged over the configured theta channels.',unit:'ln ratio'},alpha:{index:1,name:'Alpha relative power',description:'Natural log of 8–13 Hz power relative to 1–30 Hz, averaged over the configured alpha channels.',unit:'ln ratio'}};
function options(selector,rows,value,initial){
 const select=q(selector),html=`<option value="">${esc(initial)}</option>`+rows.map(([id,label])=>`<option value="${esc(id)}">${esc(label)}</option>`).join('');
 if(select.innerHTML!==html)select.innerHTML=html;
 select.value=value;
}
function currentDecision(){return data?.decisions.find(d=>d.delivered.requestId===state.request)||data?.decisions.at(-1);}
function currentWindow(){return data?.windows.find(w=>windowKey(w)===state.window)||data?.windows.at(-1);}
function navigate(page){state.page=page;root.dataset.page=page;history.replaceState(null,"","#"+page);all('[data-page]').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.page===page)));for(const name of ['decision','physiology','guide'])q('#ec-'+name).hidden=name!==page;render();}
function safeForm(url){try{const u=new URL(url);return u.protocol==='https:'&&u.hostname==='docs.google.com'&&u.pathname.startsWith('/forms/')?u.href:'';}catch{return '';}}
function renderConnection(){
 const t=data?.task,stale=offline||!t||data.telemetryAge>5||!!data?.guard?.failure;
 text('#service-state',offline?'Service disconnected':'Service connected');q('#service-state').className='chip '+(offline?'warn':'ok');
 text('#session-status',t?`${EvidenceUI.label('kind',t.sessionType)} · ${EvidenceUI.label('round',t.stage)} · Video ${t.level===1?'ON':t.level===0?'OFF':'—'} · updated ${fmt(data.telemetryAge,1)} s ago`:'No Unity session · signal preview runs without Unity');
 options('#sessions',(data?.sessions||[]).map(s=>[s.id,`${s.id} · ${EvidenceUI.label('kind',s.kind)}`]),state.session,'Follow latest session');
 q('#connection').className='connection'+(offline||t&&stale?' warn':'');
 if(offline){q('#connection').innerHTML='<b>Python service is unavailable.</b> Reconnecting automatically. Last received data may be stale. Double-click StartEvidenceConsole.cmd if the service has stopped.';return;}
 const sensors=Object.values(data.sensors),origin=[...new Set(sensors.map(s=>EvidenceUI.label('signal',s.data_origin||'unknown')))].join(' + ')||'no source',detailsOpen=q('#connection details')?.open;
 q('#connection').innerHTML=`<b>${t?esc(t.participantCode)+' · '+esc(data.session):'Signal preview'}</b> · ${t?`Review mode ${esc(data.guard?.mode||'off')} · ${data.telemetryAge>5?'Unity telemetry is stale':'Unity connected'}`:'Waveforms do not need Unity. Start a Unity session when you are ready to run the task.'}<br>${data.guard?.failure?`<b>Current review fault: ${esc(data.guard.failure)}</b><br>`:''}Source: ${esc(origin)}. ${data.service.lslSelection?'Saved LSL selection loaded.':'No LSL selection loaded. Use Evidence VR > Signal Sources to scan and save the intended outlets, then restart the bridge before starting a session.'}<details${detailsOpen?' open':''}><summary>Session details</summary><span class="ec-small">Records: ${esc(data.output||data.service.output)} · Console ${esc(data.revision)}</span></details>`;
 const config=t?.config||{};
 forms.calibration=safeForm(config.calibrationFormUrl)||'https://docs.google.com/forms/d/e/1FAIpQLSeghD4XMzg3o9_Z0C3wX79ns8VwIqtdPLRScHXn829bPKI0_A/viewform';
 forms.training=safeForm(config.trainingFormUrl)||'https://docs.google.com/forms/d/e/1FAIpQLScKnSC3dnP8edc19qBj5Zbs2sRH62p1eQ5s9zbi9y5T9LTlEQ/viewform';
 text('#protocol-duration','Independent stages · follow the selected session’s timing in Unity');
}
function node(key,value,note,warn=false){const el=q(`[data-node="${key}"]`);el.querySelector('strong').textContent=value??'—';el.querySelector('.ec-small').textContent=note;el.classList.toggle('amber',warn);el.setAttribute('aria-pressed',String(state.node===key));}
let graphScore=null,scoreHistory=[],scoreKey='',lastScoreStamp=null,lastScoreAt=null,liveEvidenceKey='';
function scoreTrend(){
 const live=!state.request&&!state.session&&!offline&&graphScore,svg=q('#rf-trend');
 svg.hidden=!live || live.vendor;q('#rf-updated').hidden=!live;
 if(!live){return;}
 const key=(data?.session||'preview')+':'+(data?.task?.stage||'preview')+':'+live.model;
 if(key!==scoreKey){scoreKey=key;scoreHistory=[];lastScoreStamp=null;lastScoreAt=null;}
 if(Number.isFinite(live.value)&&live.stamp!=null&&live.stamp!==lastScoreStamp){lastScoreStamp=live.stamp;lastScoreAt=Date.now();scoreHistory.push([Date.now(),live.value]);scoreHistory=scoreHistory.slice(-45);}
 text('#rf-updated',lastScoreAt?'Last score received '+new Date(lastScoreAt).toLocaleTimeString()+(Number.isFinite(live.value)?'':' · currently unavailable'):'Waiting for a numeric score');
 const ns='http://www.w3.org/2000/svg';svg.replaceChildren();
 for(const threshold of [live.thresholds?.on,live.thresholds?.off])if(Number.isFinite(threshold)){const line=document.createElementNS(ns,'line');line.setAttribute('x1','0');line.setAttribute('x2','200');line.setAttribute('y1',String(42-threshold*40));line.setAttribute('y2',String(42-threshold*40));line.setAttribute('stroke',tone('--ec-line'));line.setAttribute('stroke-dasharray','4 3');svg.append(line);}
 const points=scoreHistory.filter(p=>Date.now()-p[0]<=90000);if(!points.length)return;
 const start=points[0][0],span=Math.max(1000,points.at(-1)[0]-start),path=document.createElementNS(ns,'path');let d='';
 points.forEach((p,i)=>{d+=(i===0||p[0]-points[i-1][0]>5000?'M':'L')+(2+(p[0]-start)/span*196).toFixed(1)+','+(42-p[1]*40).toFixed(1)+' ';});path.setAttribute('d',d);path.setAttribute('fill','none');path.setAttribute('stroke',tone('--ec-blue'));path.setAttribute('stroke-width','2');svg.append(path);
}
function runOverview(){
 const t=data?.task,fresh=!offline&&t&&data.telemetryAge<=5,c=t?.config||{},count=t?.completedTrials;
 const stride=c.evaluationPolicy==='behavior5-continuous-stride2-v3'?2:5,next=Number.isFinite(count)?(Math.floor(count/stride)+1)*stride:null;
 const stage=t?.stage==='layer_a'?'Round A':t?.stage==='layer_b'?'Round B':human(t?.stage);
 const budget=['layer_a','layer_b'].includes(t?.stage)?c.trainingTrials:null;
 const video=data?.playback?(data.playback.videoPlaying?'Playing':'Off'):'Not reported yet';
 q('#run-overview').innerHTML=[['Current stage',fresh?stage:'Waiting for Unity',fresh?data.session:'No fresh task telemetry'],['Trial progress',fresh&&Number.isFinite(count)?String(count)+(budget?' / '+budget:' completed'):'—','Completed positions; warmups are included'],['Next check',fresh&&['layer_a','layer_b'].includes(t.stage)&&next<c.trainingTrials?'In '+(next-count)+' trials':'—','Scheduled checkpoint, not a guaranteed change'],['Video',fresh?video:'—',fresh?'From decoded playback events':'Shown only from fresh Unity telemetry']].map(([label,value,detail])=>`<div><small>${esc(label)}</small><strong>${esc(value)}</strong><small>${esc(detail)}</small></div>`).join('');
}

async function readGraphScore(snap){
 const get=async(path,options={})=>{
   const response=await fetch(path,{...options,signal:AbortSignal.timeout(4000)});
   if(!response.ok)throw Error('Model status unavailable');
   return response.json();
 };
 const status=await get('/tests/status');
 const current=status.sessions.find(r=>['prepared','claimed','running'].includes(r.state));
 if(current?.live){
   if(current.sessionId!==snap.session)return null;
   const vendor=current.config.testSessionMode==='vendor_workload';
   if(!vendor && !current.sourceModel)return null;
   const result={thresholds:current.scoreThresholds,model:vendor?'Mindtooth Workload':current.sourceModel.modelId,vendor};
   if(snap.telemetryAge>5)return {...result,note:'Unity telemetry stale'};
   if(snap.model?.mountIntact===false)return {...result,note:'Mount interrupted'};
   if(vendor){
     const v=status.neurometrics?.values,identity=current.vendorIdentity;
     const stamp=v?.calculatedAt>1e11?v.calculatedAt:v?.calculatedAt*1000;
     const age=Date.now()-stamp;
     const fresh=identity && current.live.modelStatus==='vendor_workload_ready' && status.neurometrics.state==='receiving' &&
       v?.participant===identity.participant && v?.session===identity.session && age>=0 && age<=3000;
     return {...result,value:fresh?v.workload:null,note:fresh?'Live vendor workload':'Waiting for vendor workload'};
   }
   const pred=snap.predictions.filter(p=>p.stage===snap.task?.stage).at(-1);
   if(!pred)return {...result,note:'Waiting for a scored window'};
   const value=pred.loadRevision?pred.loadScore:pred.score,age=snap.task.now-pred.end;
   if(!pred.valid || !Number.isFinite(value))return {...result,note:human(pred.loadReason||pred.reason)};
   if(!(age>=0 && age<=9))return {...result,note:'Waiting for fresh EEG'};
   return {...result,value,stamp:pred.end,note:'Task score · '+human(pred.stage)+' · trial '+(pred.trial+1)};
 }
 if(current && (current.config.testSessionMode!=='comparison_candidate' || current.sourceModel?.featureRevision!=='rest-math-iaf-rf-v2'))return null;
 const selected=current?{participantCode:current.participantCode,storageFolder:current.storageFolder,modelId:current.sourceModel.modelId}:
   JSON.parse(localStorage.getItem('study.rfPreview')||'null');
 if(!selected)return null;
 const preview=await get('/tests/workload-preview',{method:'POST',headers:{'Content-Type':'application/json','X-Test-Console':'engineering-model-reuse-v1'},body:JSON.stringify(selected)});
 const note=preview.reason==='ok'?'Live EEG · 10 s · screening off':human(preview.reason);
 return {thresholds:current?.scoreThresholds||selected.scoreThresholds,model:selected.modelId,value:preview.score,stamp:preview.end,note,quality:preview.quality};
}
function decisionView(){
 const d=currentDecision(),base=d?.base,delivered=d?.delivered,review=d?.probeReview || d?.review,view=review?.playbackGaze || review?.gaze || d?.viewChecks?.at(-1),ack=data?.acknowledgments?.[delivered?.requestId],b=base?.behavior;
 runOverview();scoreTrend();
 options('#requests',(data?.decisions||[]).map(x=>[x.delivered.requestId,checkLabel(x)]),state.request,'Latest check');
 node('behavior',b?human(b.state):null,b?`BA ${Number.isFinite(b.ba)?fmt(100*b.ba,0)+'%':'—'} · ${b.omissions} omissions`:'No behavioral result for this check');
 const live=!state.request && !state.session && !offline && graphScore;
 const vendorDecision=base?.basis==='mindtooth_vendor_workload';
 q('[data-node="physiology"] .ec-node-label').textContent=(live?live.vendor:vendorDecision)?'Mindtooth Workload':'Workload model';
 const cutoffs=live?live.thresholds:base?.lowThreshold!=null?{on:base.lowThreshold,off:base.highThreshold}:null;
 const limits=cutoffs && Number.isFinite(cutoffs.on) && Number.isFinite(cutoffs.off)?
   ` · ON ≤ ${cutoffs.on} · OFF ≥ ${cutoffs.off}`:'';
 if(live)node('physiology',fmt(live.value,3),live.note+limits);
 else if(vendorDecision)node('physiology',fmt(base.vendorMetrics?.workload),'Recorded vendor workload');
 else node('physiology',fmt(base?.score,3),base?'Recorded decision score'+limits:offline?'Service offline':'Waiting for model selection');
 node('judgment',base?.action,base?EvidenceUI.label('basis',base.basis):'Waiting for the first proposal');
 const viewPassed=view&&(d?.probeReview?(review.checks||[]).filter(c=>['Valid eye coverage','Task share of valid eye time'].includes(c.name)).every(c=>c.status==='met'):(review?.checks||[]).flat().some(c=>c.judgment===view.judgment&&c.valid));
 const gazeInput=review?.gazeInput || base?.gazeInput;
 node('gaze',gazeInput?.simulated?'Simulated gaze':view?(view.available?(viewPassed?'Supported':'Insufficient'):'Unavailable'):'Not reached',view?.available?`${fmt(100*view.gaze_valid_ratio,0)}% valid eyes · ${fmt(100*view.task_gaze_ratio,0)}% task / valid`:view?'No valid tracked-eye evidence':(gazeInput?.simulated?'Simulated 75% valid / 70% task · not reached':'No gaze check for this decision'),!!view&&!viewPassed);
 node('guard',delivered?EvidenceUI.label('verdict',delivered.guardVerdict||'review off'):null,delivered?`Command ${delivered.action} · review ${data.guard?.mode||'off'}`:'Waiting for a decision',delivered?.guardVerdict==='DEFER');
 node('execution',ack?`Video ${ack.level===1?'ON':'OFF'}`:'No receipt',ack?`${ack.action||'Action'} · Unity ${fmt(ack.time,2)} s`:'Waiting for Unity to confirm the command',!!d&&!ack);
 const detail={behavior:{title:'Behavioral basis',value:b?.state,copy:'Recorded behavior summary for this check. Current comparisons use five scored responses every two trials; older policies keep their recorded window size.',rows:b},physiology:live?{title:live.vendor?'Mindtooth Workload':'Workload model',value:fmt(live.value,3),copy:live.note,rows:{model:live.model,channelQuality:live.quality,recordedDecisionScore:base?.score}}:vendorDecision?{title:'Mindtooth Workload',value:fmt(base.vendorMetrics?.workload),copy:'Engineering decision using the vendor threshold and behavioral agreement.',rows:base.vendorMetrics}:{title:'Workload model',value:fmt(base?.score),copy:'Recorded workload score for the selected decision.',rows:{physiologicalEligible:base?.physiologicalEligible,modelStatus:data?.model?.status,report:data?.model?.report}},judgment:{title:'Proposal',value:base?.action,copy:'The controller proposal before review. Random scheduling and behavioral fallback remain explicitly labeled.',rows:base},gaze:{title:'Gaze check',value:view?(view.available?'Available':'Unavailable'):'Not reached',copy:'The recorded gaze check for this decision, not a current headset estimate. Some paths, such as reductions, need no gaze check.',rows:{gazeInput,view,checks:review?.checks}},guard:{title:'Review',value:delivered?EvidenceUI.label('verdict',delivered.guardVerdict||'review off'):'—',copy:delivered?.guardReason?EvidenceUI.explain(delivered.guardReason):delivered?'No review record. Review mode off passes proposals through.':'No decision yet.',rows:{review,judgments:d?.judgments,revokedNow:d?.revokedNow,failure:data?.guard?.failure}},execution:{title:'Unity receipt',value:ack?`Video ${ack.level===1?'ON':'OFF'}`:'No receipt',copy:'A command counts as executed only when a Unity receipt matches its check ID. The current video level is reported separately.',rows:ack}}[state.node];
 const detailOpen=q('#ec-decision-detail details')?.open;
 q('#ec-decision-detail').innerHTML=`<div><div class="ec-eyebrow">${detail.title}</div><div class="ec-verdict">${esc(detail.value)}</div></div><p>${esc(detail.copy)}</p>${kv('Check',d?checkLabel(d):'None')}${kv('Proposal',base?.action||'—')}${kv('Command',delivered?.action||'—')}${kv('Current video',data?.task?`${data.task.level===1?'ON':'OFF'}${offline||data.telemetryAge>5?' · stale':''}`:'—')}${pretty(detail.rows)}`;
 if(detailOpen&&q('#ec-decision-detail details'))q('#ec-decision-detail details').open=true;
 q('#history').innerHTML=(data?.decisions||[]).slice().reverse().map(x=>{const id=x.delivered.requestId,a=data.acknowledgments?.[id];return `<tr class="${d===x?'selected':''}"><td><button data-request="${esc(id)}" title="${esc(id)}">${esc(checkLabel(x))}</button></td><td>${esc(x.base.action)}</td><td>${esc(EvidenceUI.label('verdict',x.delivered.guardVerdict||'review off'))}</td><td>${esc(x.delivered.action)}</td><td>${a?`Video ${a.level===1?'ON':'OFF'} · ${fmt(a.time,1)} s`:'No receipt'}</td></tr>`;}).join('')||'<tr><td colspan="5">No decision checks received yet.</td></tr>';
 const c=d?{request:delivered.requestId,stage:review?.stage||data?.task?.stage,trial:review?.trial||base?.lastTrial,time:d.time,base,review:review||{},delivered:delivered.action,verdict:delivered.guardVerdict,reviewMode:data?.guard?.mode,viewChecks:d.viewChecks,receipt:ack,execution:ack?'confirmed':delivered.action==='HOLD'?'no change requested':'not confirmed'}:null;
 const context={session:data?.session,gazeInput:base?.gazeInput,trialBudget:data?.task?.config?.trainingTrials,decisionStride:data?.task?.config?.rollingStrideTrials};
 const evidenceKey=JSON.stringify([c,context]);if(evidenceKey!==liveEvidenceKey){liveEvidenceKey=evidenceKey;EvidenceUI.render(q('#live-evidence'),c,context);}
 q('#full-evidence').href=data?.session?'/comparison?'+new URLSearchParams({session:data.session}):'/tests';
 requestAnimationFrame(()=>edges(d,!!ack));
}
function edges(d,ack){
 const svg=q('.ec-edges');if(q('#ec-decision').hidden)return;
 const graph=q('.ec-graph'),box=graph.getBoundingClientRect();svg.setAttribute('viewBox',`0 0 ${box.width} ${box.height}`);
 const probe=d?.probeReview,valid=probe?!!probe.reason:d?.review?.chosen_path?.length>0;
 const viewing=probe?!!(probe.gaze||probe.playbackGaze):!!d?.viewChecks?.length;
 const paths=[['behavior','judgment',!!d?.base?.behavior],['physiology','judgment',!!d?.base?.physiologicalEligible],['judgment','guard',valid],['gaze','guard',valid&&viewing],['guard','execution',ack]];
 svg.innerHTML=paths.map(([a,b,ok])=>{const p=q(`[data-node=${a}]`).getBoundingClientRect(),r=q(`[data-node=${b}]`).getBoundingClientRect();return `<path d="M${p.x+p.width/2-box.x},${p.y+p.height/2-box.y} L${r.x+r.width/2-box.x},${r.y+r.height/2-box.y}" fill="none" stroke="${ok?tone('--ec-blue'):tone('--ec-line')}" stroke-width="1.6" ${ok?'':'stroke-dasharray="4 5"'}/>`;}).join('');
}
function plot(selector,points,{unit='',color=tone('--ec-blue'),small=false}={}){
 const el=q(selector),valid=points.filter(p=>Number.isFinite(p[0])&&typeof p[1]==='number'&&Number.isFinite(p[1]));
 if(!valid.length){el.innerHTML=`<div class="empty">${small?'No qualified feature window':'No numeric samples'}</div>`;return;}
 const width=Math.max(260,el.getBoundingClientRect().width||800),height=small?65:185,pad=small?3:78,lo=Math.min(...valid.map(p=>p[1])),hi=Math.max(...valid.map(p=>p[1])),span=hi-lo||1,t0=valid[0][0],t1=valid.at(-1)[0],dt=t1-t0||1;
 const x=t=>pad+(t-t0)/dt*(width-pad-12),y=v=>12+(hi-v)/span*(height-(small?20:42));let path='',broken=true;
 for(const p of points){if(p[1]===null||!Number.isFinite(p[1])){broken=true;continue;}path+=`${broken?'M':'L'}${x(p[0]).toFixed(2)},${y(p[1]).toFixed(2)} `;broken=false;}
 el.innerHTML=`<svg class="plot" viewBox="0 0 ${width} ${height}" role="img" aria-label="${esc(unit||'Feature trend')}"><path d="${path}" stroke="${color}" stroke-width="1.5" fill="none" vector-effect="non-scaling-stroke"/>${small?'':`<g font-family="system-ui" font-size="10" fill="#6e6e73"><text x="0" y="18">${fmt(hi,1)}</text><text x="0" y="${height-30}">${fmt(lo,1)}</text><text x="${pad}" y="${height-5}">${fmt(t0,1)} s</text><text x="${width-65}" y="${height-5}">${fmt(t1,1)} s</text><text x="${width/2}" y="${height-5}" text-anchor="middle">${esc(unit)} · auto scale</text></g>`}</svg>`;
}
function observationWaveforms(sensors,streams){
 const source=streams.find(s=>s.kind==='EEG')||streams.find(s=>s.kind==='OBSERVE'),configured=sensors.find(s=>['EEG','OBSERVE'].includes(s.kind));
 const shown=!!source||!!configured,view=q('#raw-signal-view');view.hidden=!shown;
 q('#mapped-signal-view').hidden=shown;
 text('#signal-monitor-title',shown?'Raw source waveforms':'Signal monitor');
 if(!shown)return;
 text('#raw-source-name',source?.config.name||configured?.name||'Selected source');
 if(!source){text('#raw-source-meta',data?.observedSignals===undefined?'The running Python service needs the waveform update. Source values continue above.':'Waiting for raw source samples. Unity Play Mode is not required.');q('#raw-charts').innerHTML='<div class="empty">Waiting for waveform buffer</div>';return;}
 const mapping=source.config.channels||[],labels=source.config.channel_labels||[];
 const channels=source.channels.map((points,i)=>({i,points,column:mapping[i]??i,label:(labels[mapping[i]]||'Channel '+(mapping[i]??i)).trim()}));
 const numeric=channels.filter(c=>c.points.some(p=>typeof p[1]==='number'&&Number.isFinite(p[1])));
 const isIndex=c=>/^sample[ _-]?index$/i.test(c.label);
 const chosen=state.rawChannel==='all'?numeric.filter(c=>!isIndex(c)):channels.filter(c=>String(c.i)===state.rawChannel);
 const select=q('#raw-channel'),html='<option value="all">All signal columns</option>'+channels.map(c=>`<option value="${c.i}">${esc(c.label)} · column ${c.column}${isIndex(c)?' (index)':''}</option>`).join('');
 if(select.innerHTML!==html)select.innerHTML=html;
 if(state.rawChannel!=='all'&&!channels.some(c=>String(c.i)===state.rawChannel))state.rawChannel='all';select.value=state.rawChannel;
 const stale=offline||source.lastSampleAge>3;
 text('#raw-source-meta',`${stale?'STALE · ':''}${source.config.sample_rate} Hz nominal · ${fmt(source.lastSampleAge,2)} s since last sample${offline?' at last service contact':''} · ${source.config.data_origin||'unknown'} provenance · LSL analysis units: ${source.config.units||'unconfirmed'}.`);
 q('#raw-charts').className='raw-charts'+(chosen.length===1?' single':'');
 q('#raw-charts').innerHTML=chosen.map(c=>`<section class="raw-channel-chart"><h3>${esc(c.label)} <span class="ec-small">· source column ${c.column}</span></h3><div id="raw-plot-${c.i}" class="raw-plot"></div></section>`).join('')||'<div class="empty">No numeric signal columns in this buffer. Text/event values remain available above.</div>';
 for(const c of chosen)plot('#raw-plot-'+c.i,c.points,{unit:'raw'});
}

function gazeOverview(){
 const g=data?.task?.gazeOverview,accepted=g?.revision==='gaze-console-front-plane-v1'&&g.source==='tracked_eyes';
 const now=accepted&&Number.isFinite(g.now)?g.now:null;
 const seen=new Set(),rows=now===null?[]:(Array.isArray(g.samples)?g.samples:[]).slice(-360).filter(r=>{
  if(!r||!Number.isFinite(r.time)||r.time<now-10||r.time>now||seen.has(r.id))return false;
  seen.add(r.id);return true;
 }).sort((a,b)=>a.time-b.time);
 const last=rows.at(-1),stale=offline||!Number.isFinite(data?.telemetryAge)||data.telemetryAge>3;
 const paused=accepted&&['Paused','Complete','Aborted'].includes(g.phase);
 let duration=rows.length?Math.min(10,Math.max(0,now-rows[0].time)):0,covered=0,valid=0,task=0,movie=0;
 for(let i=0;i<rows.length;i++){
  const r=rows[i],dt=Math.max(0,Math.min(now,rows[i+1]?.time??now,r.time+.1)-r.time);
  covered+=dt;
  if(r.valid===true){valid+=dt;if(r.target==='task')task+=dt;if(r.target==='movie')movie+=dt;}
 }
 const measurable=accepted&&duration>0&&!paused;
 text('#gaze-task',measurable&&valid>0?fmt(100*task/valid,0)+'%':'—');
 text('#gaze-movie',measurable&&valid>0?fmt(100*movie/valid,0)+'%':'—');
 text('#gaze-valid',measurable?fmt(100*valid/duration,0)+'%':'—');
 const rectValid=r=>r?.available===true&&[r.x,r.y,r.width,r.height].every(Number.isFinite)&&r.width>0&&r.height>0;
 const candidate=g?.layout,layoutValid=accepted&&candidate?.available===true&&['minX','maxX','minY','maxY'].every(k=>Number.isFinite(candidate[k]))&&candidate.maxX>candidate.minX&&candidate.maxY>candidate.minY&&candidate.maxX-candidate.minX<100&&candidate.maxY-candidate.minY<100&&rectValid(candidate.movie);
 const layout=layoutValid?candidate:{minX:-2.2,maxX:2.2,minY:-2,maxY:1.4,movie:{available:true,x:-1.6,y:-.9,width:3.2,height:1.8},sphere:{available:true,x:-.10,y:-1.5,width:.2,height:.2},tray:{available:true,x:-.3,y:-1.65,width:.6,height:.07},task:{available:true,x:-.4,y:-1.75,width:.8,height:.55}};
 const scale=Math.min(700/(layout.maxX-layout.minX),350/(layout.maxY-layout.minY));
 const left=(800-scale*(layout.maxX-layout.minX))/2,top=45+(350-scale*(layout.maxY-layout.minY))/2;
 const x=v=>left+(v-layout.minX)*scale,y=v=>top+(layout.maxY-v)*scale;
 const inside=r=>r.projected===true&&Number.isFinite(r.x)&&Number.isFinite(r.y)&&r.x>=layout.minX&&r.x<=layout.maxX&&r.y>=layout.minY&&r.y<=layout.maxY;
 const projected=rows.filter(r=>r.valid===true&&r.projected===true&&Number.isFinite(r.x)&&Number.isFinite(r.y));
 const visible=layoutValid&&!paused?projected.filter(inside):[],offView=projected.length-visible.length;
 const age=last&&now!==null?Math.max(0,now-last.time)+(data?.telemetryAge||0):null;
 const current=layoutValid&&!stale&&!paused&&last?.valid===true&&inside(last)&&age<=1.5?last:null;
 let status=!data?.task?'Waiting for Unity':!accepted?'Gaze telemetry unavailable':stale?'Telemetry stale':paused?'Paused':!last?'Waiting for eye samples':last.valid!==true?'Tracking unavailable':!layoutValid?'Projection unavailable':!inside(last)?'Outside projection':age>1.5?'Waiting for fresh gaze':'Tracked eyes';
 text('#gaze-status',status);q('#gaze-status').className='ec-tag '+(current?'blue':'amber');
 all('[data-gaze-mode]').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.gazeMode===state.gazeMode)));
 let drawing=`<rect x="22" y="20" width="756" height="408" rx="22" fill="none" stroke="var(--ec-line)" stroke-width="1"/><text x="400" y="43" text-anchor="middle" font-size="12" fill="var(--ec-muted)">${layoutValid?'Front-wall reference plane':'Schematic · waiting for scene geometry'}</text>`;
 const box=(r,role,active)=>rectValid(r)?`<rect class="gaze-region" data-region="${role}" x="${x(r.x)}" y="${y(r.y+r.height)}" width="${r.width*scale}" height="${r.height*scale}" rx="${role==='movie'?8:4}" fill="${role==='task'?'none':'var(--ec-surface)'}" stroke="var(--ec-line)" stroke-width="${role==='task'?1:1.5}" ${active?'':'stroke-dasharray="5 5"'}/>`:'';
 drawing+=box(layout.movie,'movie',layoutValid&&last?.movieVisible);
 if(rectValid(layout.movie))drawing+=`<text x="${x(layout.movie.x+layout.movie.width/2)}" y="${y(layout.movie.y+layout.movie.height/2)}" text-anchor="middle" fill="var(--ec-muted)" font-size="14">Movie screen</text>`;
 drawing+=box(layout.task,'task',false)+box(layout.tray,'tray',layoutValid&&last?.taskVisible);
 if(rectValid(layout.sphere))drawing+=`<ellipse data-region="sphere" cx="${x(layout.sphere.x+layout.sphere.width/2)}" cy="${y(layout.sphere.y+layout.sphere.height/2)}" rx="${layout.sphere.width*scale/2}" ry="${layout.sphere.height*scale/2}" fill="var(--ec-line)" stroke="var(--ec-muted)" stroke-width="1" ${layoutValid&&last?.taskVisible?'':'stroke-dasharray="3 3"'}/>`;
 if(rectValid(layout.tray))drawing+=`<text x="${x(layout.tray.x+layout.tray.width/2)}" y="${y(layout.tray.y)+22}" text-anchor="middle" fill="var(--ec-muted)" font-size="12">Task sphere &amp; tray</text>`;
 if(state.gazeMode==='distribution')drawing+=visible.map(r=>`<circle class="gaze-history-dot" cx="${x(r.x)}" cy="${y(r.y)}" r="5" fill="var(--ec-blue)" opacity="${(stale ? .025 : .04)+(stale ? .04 : .18)*Math.max(0,1-(now-r.time)/10)}"/>`).join('');
 if(current)drawing+=`<circle cx="${x(current.x)}" cy="${y(current.y)}" r="13" fill="var(--ec-blue)" opacity=".12"/><circle id="gaze-current-point" cx="${x(current.x)}" cy="${y(current.y)}" r="5.5" fill="var(--ec-blue)" stroke="var(--ec-surface)" stroke-width="2"/>`;
 q('#gaze-scene').innerHTML=drawing;
 text('#gaze-coverage',measurable?`${fmt(duration,1)} s window · ${fmt(covered,1)} s covered · ${offView} projected samples outside view`:'Waiting for a measurable tracked-eye window.');
 let message=!data?.task?'Connect a Unity session to receive genuine tracked-eye samples. No gaze points are simulated.':!accepted?'This Unity session has no compatible gaze-position telemetry. Use the updated Editor source or a rebuilt Player.':stale?'Last received distribution. The current gaze point is hidden until fresh telemetry arrives.':paused?'Gaze display is paused. No current point is shown.':!layoutValid?'Waiting for a valid scene projection. No gaze points are drawn.':last?.valid!==true?`No valid current eye tracking (${g.status||'unavailable'}). Faint points, if present, are earlier valid samples.`:current?`Latest sample ${fmt(age,2)} s ago · ${human(last.target)}. Dots show real tracked-eye rays projected onto the reference plane.`:'Current tracked eyes do not yield an in-view projection; no point is clamped to the diagram edge.';
 text('#gaze-message',message);
}

function physiologyView(){
 gazeOverview();
 const w=currentWindow(),signals=data?.signals||[],eeg=signals.find(s=>s.kind==='EEG');
 const sensors=Object.values(data?.sensors||{}),fresh=s=>!offline&&s.connected&&Number.isFinite(s.last_sample)&&data.clock-s.last_sample>=0&&data.clock-s.last_sample<=3;
 const receiving=sensors.filter(fresh),observed=sensors.filter(s=>s.kind==='OBSERVE'||s.kind==='FEATURES');
 const sourceStatus=offline?'Service disconnected':receiving.length?`Receiving · ${receiving.length} / ${sensors.length} sources`:sensors.length?'Waiting for fresh source samples':'No LSL source configured';
 text('#incoming-status',sourceStatus);q('#incoming-status').className='ec-tag '+(receiving.length?'green':'amber');
 q('#incoming-values').innerHTML=sensors.map(s=>`<div class="source-row"><b>${esc(s.name||s.source_id)}</b> <span class="ec-tag ${fresh(s)?'green':'amber'}">${fresh(s)?'Receiving':offline?'Service offline':'No fresh samples'}</span><p class="ec-small">${esc(s.sample_rate)} Hz nominal · ${s.kind==='OBSERVE'?'Receive only':s.kind==='FEATURES'?'External features':esc(s.kind)+' mapping'} · ${esc(s.data_origin||'unknown')} provenance · ${Number.isFinite(s.last_sample)?fmt(Math.max(0,data.clock-s.last_sample),2)+' s since last sample':'No sample timestamp'}${offline?' at last service contact':''}</p>${s.external_values?.length?`<div class="channel-values">${s.external_values.map(v=>`<div class="channel-value"><span>${esc(v.label?.trim()||'Channel '+v.channel)}</span><strong>${typeof v.value==='number'?fmt(v.value,2):esc(v.value)}</strong><small>Source column ${esc(v.channel)}</small></div>`).join('')}</div>`:'<p class="ec-small">Mapped waveform shown below when session samples are available.</p>'}</div>`).join('')||'No source selected. Save a source selection before connecting the bridge.';
 options('#windows',(data?.windows||[]).map(x=>[windowKey(x),`${x.stage} · trial ${x.trial+1} · ${x.valid?'valid':'rejected'}`]),state.window,'Latest window');
 const n=eeg?.channels.length||0,channel=q('#channels');const labels=eeg?.config.channel_labels||[],mapped=eeg?.config.channels||[];
 const html=n?Array.from({length:n},(_,i)=>`<option value="${i}">${esc(labels[mapped[i]]||'Source channel '+mapped[i])}</option>`).join(''):'<option value="0">No mapped EEG</option>';
 if(channel.innerHTML!==html)channel.innerHTML=html;if(state.channel>=n)state.channel=0;channel.value=state.channel;
 text('#eeg-meta',eeg ? `· ${eeg.config.sample_rate} Hz nominal · ${eeg.config.data_origin}` : '· waiting for samples');
 plot('#ec-eeg-chart',eeg?.channels[state.channel]||[],{unit:eeg?.config.units||'source units'});
 text('#signal-status',receiving.length?'Receiving Mindtooth EEG':signals.length?`${offline?'Service offline · ':''}Waveform sample age ${fmt(Math.max(...signals.map(s=>s.lastSampleAge)),1)} s`:observed.length?'Receive-only values shown above':'Waveform unavailable');
 observationWaveforms(sensors,data?.observedSignals||[]);
 if(observed.length){const raw=data?.observedSignals?.[0];text('#signal-status',raw?(offline||raw.lastSampleAge>3?'Raw samples stale':'Receiving raw waveforms'):data?.observedSignals===undefined?'Waveform service update required':'Waiting for raw buffer');}
 const model=data?.model,admitted=model?.report?.admitted&&model.mountIntact;
 const qualityOpen=q('#quality details')?.open;
 q('#quality').innerHTML=`<div><div class="ec-eyebrow">Selected feature window</div><h2>${w?(w.valid?(w.quality?.eeg?.artifactScreening===false?'Artifact screening off':'Signal checks passed'):'Window rejected'):'Awaiting a window'}</h2><p>${esc(w?.reason?human(w.reason):'Five-trial windows are extracted by Python; this page does not calculate substitute features.')}</p></div>${kv('Source origin',w?.origin||'Unavailable')}${kv('Unity interval',w?`${fmt(w.start,1)}–${fmt(w.end,1)} s`:'—')}${kv('Model status',model?human(model.status):'Unavailable')}${kv('Model admitted',model?(admitted?'Yes':'No'):'Unknown')}${kv('Mount intact',model?(model.mountIntact?'Yes':'No · restart required'):'Unknown')}${kv('EEG accepted segments',w?.quality?.eeg?.subsegments!==undefined?`${w.quality.eeg.accepted_subsegments} / ${w.quality.eeg.subsegments}`:'—')}${pretty(w?.quality)}<div class="ec-eligibility">Waveforms show the latest received samples. Feature selection shows a completed historical trial window; they are separate time ranges.</div>`;
 if(qualityOpen&&q('#quality details'))q('#quality details').open=true;
 for(const [key,f] of Object.entries(featureInfo)){
  text(`[data-value=${key}]`,w?.valid?fmt(w.features[f.index],2):'—');
  const rows=(data?.windows||[]).slice(-24);plot(`[data-spark=${key}]`,rows.map((x,i)=>[i,x.valid?x.features[f.index]:null]),{small:true});
  q(`[data-feature=${key}]`).setAttribute('aria-pressed',String(state.feature===key));
 }
 const f=featureInfo[state.feature];q('#ec-feature-description').innerHTML=`<h3>${f.name}</h3><p>${f.description}</p><p class="ec-small">Trends: latest 24 retained windows; rejected windows are gaps. Scales are independent.</p>`;
 text('#movie',w?.valid?fmt(w.features[2])+' fraction':'—');
 text('#window-label',w?`${w.stage} · trial ${w.trial+1} · L${w.level} · ${fmt(w.start,1)}–${fmt(w.end,1)} s`:'No completed window');
 q('#sources').innerHTML=Object.values(data?.sensors||{}).map(s=>`<div class="source-row"><b>${esc(s.kind)} · ${esc(s.name||s.source_id)}</b> <span class="ec-tag">${esc(s.data_origin||'unknown')}</span><p>${esc(s.state)} · ${s.connected&&data.clock-s.last_sample<=3?'receiving':'not receiving fresh samples'} · ${esc(s.sample_rate)} Hz nominal</p><p class="ec-small">Source ID: ${esc(s.source_id)} · UID: ${esc(s.uid)}</p>${s.external_values?pretty(s.external_values):''}</div>`).join('')||'No LSL inputs configured. Select exact sources and verified channel mappings before starting a session.';
}
function modelOutput(){
 const vendor=q('#phys-model').value==='vendor';
 q('#vendor-controls').hidden=!vendor;
 if(!vendor){
   const prediction=data?.predictions?.at(-1);
   if(!offline && data?.model?.report?.featureRevision==='rest-math-iaf-rf-v2'){
     const r=data.model.report;
     text('#model-output',`Workload model (Personal RF) · arithmetic-class score 0–1: ${fmt(prediction?.loadScore)} · resting alpha peak ${fmt(r.iafHz)} Hz · held-out AUC ${fmt(r.meanHeldAuc)} · ${r.reasons.map(EvidenceUI.human).join(', ')}`);
     return;
   }
   if(!offline && data?.model?.report?.featureRevision==='rest-math-120s-v1'){
     const r=data.model.report;
     text('#model-output',`Personal load score: ${fmt(prediction?.loadScore)} · Rest = 0 · Arithmetic = 1 · ${r.status} · ${r.reasons.join(', ')}`);
     return;
   }
   text('#model-output',offline?'Service offline':prediction?.score!=null?'Personal RF · Struggling probability '+fmt(prediction.score):'Waiting for a scored workload window');
   return;
 }
 const stream=data?.neurometrics;
 const values=stream?.values||{};
 const summary=['workload','workload_th','stress','vigilance'].map(key=>key+' '+(values[key]==null?'—':fmt(values[key]))).join(' · ');
 text('#model-output',offline?'Service offline':`Live vendor monitor · ${stream?.state||'disconnected'} · ${summary}`+(values.participant?` · Participant ${values.participant} · Session ${values.session}`:''));
}
q('#phys-model').onchange=modelOutput;
async function vendorConnection(enabled){
 try{
   const response=await fetch('/tests/neurometrics',{method:'POST',headers:{'Content-Type':'application/json','X-Test-Console':'engineering-model-reuse-v1'},body:JSON.stringify({enabled})});
   const result=await response.json();
   text('#vendor-message',response.ok?'':result.error||'Connection failed');
 }catch{text('#vendor-message','Bridge unavailable');}
}
q('#vendor-connect').onclick=()=>vendorConnection(true);
q('#vendor-stop').onclick=()=>vendorConnection(false);
function render(){
 const quality=data?.calibrationQuality, banner=q('#calibration-quality');
 banner.hidden=!quality || offline || data.telemetryAge>5;
 banner.className='connection'+(quality?.warning?' warn':'');
 banner.textContent=banner.hidden?'':quality.text;
 modelOutput();renderConnection();if(state.page==='decision')decisionView();if(state.page==='physiology')physiologyView();const key=JSON.stringify([state.stage,forms,data?.session,data?.task?.config?.comparisonEnvironment,data?.task?.config?.trainingFormUrl]);if(state.page==='guide'&&guideKey!==key){guide();guideKey=key;}}
function selectRequest(id){state.request=id;const d=currentDecision(),predictionIds=(d?.judgments||[]).flatMap(j=>j.evidence_ids||j.dependencies||[]);const matched=(data?.windows||[]).filter(w=>predictionIds.includes('signal:'+w.stage+':'+w.trial));state.window=matched.length?windowKey(matched.at(-1)):'';render();}
root.addEventListener('click',e=>{const button=e.target.closest('button');if(!button)return;if(button.dataset.page)navigate(button.dataset.page);if(button.dataset.stage!==undefined){state.stage=Number(button.dataset.stage);render();}if(button.dataset.node){state.node=button.dataset.node;decisionView();}if(button.dataset.gazeMode){state.gazeMode=button.dataset.gazeMode;gazeOverview();}if(button.dataset.feature){state.feature=button.dataset.feature;physiologyView();}if(button.dataset.request)selectRequest(button.dataset.request);});
q('#raw-channel').addEventListener('change',e=>{state.rawChannel=e.target.value;physiologyView();});
q('#sessions').addEventListener('change',e=>{state.session=e.target.value;state.request='';state.window='';data=null;offline=true;render();});
q('#requests').addEventListener('change',e=>selectRequest(e.target.value));q('#windows').addEventListener('change',e=>{state.window=e.target.value;physiologyView();});q('#channels').addEventListener('change',e=>{state.channel=Number(e.target.value);physiologyView();});
window.addEventListener('resize',()=>{if(state.page==='decision')decisionView();});
window.addEventListener('hashchange',()=>{const page=location.hash.slice(1);if(['guide','physiology','decision'].includes(page)&&page!==state.page)navigate(page);});
async function poll(){const selected=state.session;try{const response=await fetch('/console/snapshot'+(selected?'?session='+encodeURIComponent(selected):''),{cache:'no-store',signal:AbortSignal.timeout(4000)});if(!response.ok)throw Error(response.status);const next=await response.json();if(selected!==state.session)return;if(actualSession!==next.session){state.request='';state.window='';actualSession=next.session;}let score=null;
 if(state.page==='decision' && !selected && !state.request){
   try{score=await readGraphScore(next);}catch{score={note:'Model status unavailable'};}
 }
 if(selected!==state.session)return;
 graphScore=score;data=next;offline=false;render();}catch{if(selected===state.session){offline=true;render();}}finally{setTimeout(poll,1000);}}
navigate(state.page);poll();
})();
