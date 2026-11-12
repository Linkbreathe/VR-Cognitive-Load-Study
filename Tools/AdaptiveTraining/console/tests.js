'use strict';
const $ = id => document.getElementById(id);
let models = [], initialized = false, busy = false, lastStatus = null, shownSession = null, independentReady=false;
const comparisonStages={fixed_exposure:"Fixed Exposure",layer_a:"Adaptive — Without Evidence",layer_b:"Adaptive — With Evidence"};
let chosenStage="practice", historyLimit=6, historyKey="", refreshing=false;
const active = r => ['prepared','claimed','running'].includes(r.state);
function notice(text, error=false) { $('notice').textContent=text; $('notice').className='notice'+(error?' error':''); }
async function api(path, body) {
  const response=await fetch(path, body===undefined?{}:{method:'POST',headers:{'Content-Type':'application/json','X-Test-Console':'engineering-model-reuse-v1'},body:JSON.stringify(body)});
  const value=await response.json(); if(!response.ok)throw Error(value.error||response.statusText); return value;
}
async function action(fn) { try {busy=true; await fn();} catch(e){notice(e.message,true);} finally {busy=false; await refresh();} }
function vendorIdentity(){const values=lastStatus?.neurometrics?.values;return values?.participant&&values?.session?{participant:values.participant,session:values.session}:null;}
function vendorLabel(){const selected=$('mode').value==='vendor_workload',v=vendorIdentity();$('vendor-identity').hidden=!selected;$('vendor-identity').textContent=v?`Vendor participant ${v.participant} · session ${v.session}. Confirm this matches the local participant ID.`:'Connect Mindtooth on the Physiology page and enable AI-Based Workload.';}
const options = {
 practice:[['practice','Task only'],['practice_eeg','Monitor Mindtooth EEG']],
 calibration:[['calibration','Train workload model · forest / arithmetic'],['calibration_rehearsal','Forest / arithmetic rehearsal without EEG'],['control_calibration','Legacy control calibration · separate protocol']],
 comparison:[['comparison_candidate','A/B with a saved workload model'],['comparison_rehearsal','A/B task rehearsal without EEG or model']],
 legacy:[['bioadaptive','Single-period saved model test'],['vendor_workload','Single-period vendor workload test'],['full','Continuous legacy protocol']]
};
for(const key of Object.keys(comparisonStages))options[key]=[['comparison_candidate','Saved workload model'],['comparison_rehearsal','Task rehearsal without EEG or model']];
function setStage(stage, mode){
 if(stage==='comparison')stage='fixed_exposure';
 chosenStage=stage;sessionStorage.setItem("study.setupStage",stage);
 if(stage==='legacy' && !$('stage').querySelector('[value=legacy]')){
   const legacy=document.createElement('button');legacy.type='button';legacy.value='legacy';legacy.textContent='Legacy sessions';$('stage').append(legacy);
 }
 for(const button of $('stage').querySelectorAll('button'))button.setAttribute('aria-pressed',String(button.value===stage));
 $('mode').replaceChildren(...options[stage].map(([value,label])=>new Option(label,value)));
 if(mode)$('mode').value=mode;
 modes();
}
function environmentNote(){
 for(const b of document.querySelectorAll("[data-environment]")){b.setAttribute("aria-pressed",String(b.dataset.environment===$("environment").value));b.disabled=!!lastStatus?.sessions.find(active);}
 $('environment-note').textContent=$('environment').value==='desktop_fixed_gaze'?
 'No headset required. Use the Unity desktop view and keyboard. Decision gaze inputs are fixed at 75% valid coverage and 70% task share of valid eye time, marked Simulated gaze. EEG/model and response inputs remain unchanged.':
 'Wear Quest Pro and enable real eye tracking. Missing tracking is never replaced with fixed values. The selected environment is locked for this stage.';
}
function modes(){vendorLabel();$('calibration-mode-note').hidden=chosenStage!=='calibration';$('calibration-mode-note').textContent=$('mode').value==='control_calibration'?'Legacy calibration uses a separate protocol. It is not required to reuse a forest / arithmetic workload model in A/B.':$('mode').value==='calibration_rehearsal'?'Rehearsal checks the task sequence without EEG and does not train a model.':'Trains a new 5-second workload model (Personal RF) and video thresholds. Historical 10-second models require recalibration.';$('environment-wrap').hidden=!['comparison','comparison_candidate','comparison_rehearsal'].includes($('mode').value);environmentNote();$('comparison-policy').hidden=!['comparison','comparison_candidate','comparison_rehearsal'].includes($('mode').value);const reuse=['bioadaptive','comparison','comparison_candidate'].includes($('mode').value);$('order-wrap').hidden=!['comparison','comparison_candidate','comparison_rehearsal'].includes($('mode').value);$('order').parentElement.hidden=chosenStage in comparisonStages;$('order-heading').textContent=chosenStage in comparisonStages?'One independent 34-trial stage':'Two comparison rounds';$('arrangement-note').textContent=chosenStage==='fixed_exposure'?'2 observe-only warmups, then 32 scored trials: 16 video off and 16 video on. Video starts at total trial 19. No adaptive decisions.':'A observes the review; B enforces it. Each selected stage starts fresh.';$('adaptive-rules').hidden=chosenStage==='fixed_exposure';$('candidate-wrap').hidden=$('mode').value!=='bioadaptive';$('model-wrap').hidden=!reuse;$('blocks-wrap').hidden=!['calibration','calibration_rehearsal'].includes($('mode').value);thresholdEditor();workspace();}
function modelLabel(m){
 if(m.featureRevision==='rest-math-iaf-rf-v3-5s')return 'Workload model · forest / arithmetic'+(m.bandProfile?.mode==='fixed_bands_fallback'?' · fixed bands':'')+(Number.isFinite(m.meanHeldAuc)?` · AUC ${m.meanHeldAuc.toFixed(3)}`:'');
 if(m.featureRevision==='rest-math-120s-v1')return 'Personal load reference (observation)';
 return m.admitted?'Admitted at source session':'UNVALIDATED CANDIDATE';
}
let thresholdKey='', thresholdDirty=false;
function thresholdValues(){
 return {on:$('on-threshold').valueAsNumber,off:$('off-threshold').valueAsNumber};
}
function thresholdEditor(){
 const m=models.find(x=>x.modelId===$('model').value);
 const visible=$('mode').value==='comparison_candidate' && m?.featureRevision==='rest-math-iaf-rf-v3-5s';
 const session=lastStatus?.sessions.find(active);
 const canEdit=!!lastStatus?.thresholdEditing;
 $('threshold-wrap').hidden=!visible;
 for(const id of ['on-threshold','off-threshold'])$(id).disabled=!visible || busy || !!session&&!canEdit;
 $('apply-thresholds').hidden=!session || !visible;
 if(!visible){thresholdKey='';return;}
 const profile=m.thresholdProfile, recommended=profile?.recommended;
 const key=JSON.stringify([$('folder').value,m.participantCode,m.modelId,session?.sessionId]);
 if(key!==thresholdKey){
   let remembered=null;
   try{const prior=JSON.parse(localStorage.getItem('study.rfPreview')||'null'),v=prior?.scoreThresholds;
     if(prior?.modelId===m.modelId && prior.participantCode===m.participantCode && prior.storageFolder===$('folder').value && Number.isFinite(v?.on)&&Number.isFinite(v?.off)&&0<=v.on&&v.on<v.off&&v.off<=1)remembered=v;
   }catch{}
   const values=session?.scoreThresholds || remembered || recommended || {on:.45,off:.65};
   $('on-threshold').value=values.on;$('off-threshold').value=values.off;thresholdKey=key;thresholdDirty=false;
 }
 if(session && !thresholdDirty){$('on-threshold').value=session.scoreThresholds.on;$('off-threshold').value=session.scoreThresholds.off;}
 $('threshold-recommendation').textContent=recommended?
   `Calibration recommendation: ON ≤ ${recommended.on.toFixed(2)} · OFF ≥ ${recommended.off.toFixed(2)}`:
   'Held-out scores unavailable. Showing legacy defaults.';
 if(recommended && profile.arithmetic.filter(v=>v>=recommended.off).length < profile.arithmetic.length/4){
   $('threshold-recommendation').textContent+=' · Limited OFF evidence in the arithmetic windows.';
 }
 const {on,off}=thresholdValues();
 const valid=Number.isFinite(on)&&Number.isFinite(off)&&0<=on&&on<off&&off<=1;
 $('apply-thresholds').disabled=!canEdit || busy || !valid || !session || (on===session.scoreThresholds?.on && off===session.scoreThresholds?.off);
 $('threshold-current').textContent=session?.scoreThresholds?
   `Current applied: ON ≤ ${session.scoreThresholds.on} · OFF ≥ ${session.scoreThresholds.off}. ${on!==session.scoreThresholds.on||off!==session.scoreThresholds.off?'Unapplied edits in the fields; press Apply thresholds to use them.':'No pending changes.'}`:
   'Draft for the next session · these values are saved when you create the assignment.';
 if(!canEdit)$('threshold-current').textContent+=' Restart the Console service to enable in-session edits.';
 $('restore-thresholds').hidden=!recommended;$('restore-thresholds').disabled=busy||!!session&&!canEdit;
 $('off-threshold').setCustomValidity(on>=off?'OFF must be higher than ON.':'');
 for(const [side,cut] of [['on',on],['off',off]]){
   const count=scores=>scores.filter(v=>side==='on'?v<=cut:v>=cut).length;
   $(side+'-count').textContent=profile && Number.isFinite(cut)?
     `Calibration windows · Forest ${count(profile.forest)}/${profile.forest.length} · Arithmetic ${count(profile.arithmetic)}/${profile.arithmetic.length}`:'';
 }
}
function rememberGraphSelection(){
 const model=models.find(m=>m.modelId===$('model').value && m.participantCode===$('participant').value.trim());
 if($('mode').value==='comparison_candidate' && model?.featureRevision==='rest-math-iaf-rf-v3-5s'){
   localStorage.setItem('study.rfPreview',JSON.stringify({participantCode:model.participantCode,storageFolder:$('folder').value,modelId:model.modelId,scoreThresholds:thresholdValues()}));
 }else localStorage.removeItem('study.rfPreview');
}
function info(){
 thresholdEditor();
 const m=models.find(m=>m.modelId===$('model').value);
 $('model-summary').textContent=m?modelLabel(m)+' · '+m.createdUtc:'No model selected.';
 $('model-info').textContent=m?JSON.stringify({model:m.modelId,created:m.createdUtc,sourceSession:m.sourceSession,status:modelLabel(m),comparisonCompatibility:m.comparisonStatus,meanHeldAuc:m.meanHeldAuc,foldAuc:m.foldAuc,aucGating:m.aucGating,reasons:m.reasons,features:m.features,signalMapping:m.signalContract},null,2):'No saved model selected. Complete calibration with usable signals first.';
}
async function refreshModels(){models=await api('/tests/models?'+new URLSearchParams({root:$('folder').value,participant:$('participant').value}));const old=$('model').value;$('model').replaceChildren(new Option('Select a saved model',''));for(const m of models)$('model').add(new Option(`${m.createdUtc} · ${modelLabel(m)} · ${m.modelId}`,m.modelId));if(models.some(m=>m.modelId===old))$('model').value=old;else {const prior=JSON.parse(localStorage.getItem('study.rfPreview')||'null');const match=models.find(m=>m.participantCode===$('participant').value.trim() && m.modelId===prior?.modelId);if(match)$('model').value=match.modelId;else if(models.length===1)$('model').value=models[0].modelId;}info();rememberGraphSelection();workspace();}
function para(parent,text,cls){const p=document.createElement('p');p.textContent=text;if(cls)p.className=cls;parent.append(p);}
const modeNames=EvidenceUI.terms.mode;
const stateNames={prepared:'Awaiting Unity',claimed:'Preparing in Unity',running:'In progress',complete:'Completed',aborted:'Interrupted',service_restarted:'Service restarted · incomplete'};
function tag(parent,text,cls){const span=document.createElement('span');span.className='chip'+(cls?' '+cls:'');span.textContent=text;parent.append(span);}
function render(r){
 const card=document.createElement('article');card.className='test-card';
 const head=document.createElement('div');head.className='section-heading';card.append(head);
 const h=document.createElement('h3');h.textContent=r.participantCode+' · '+(comparisonStages[r.config.comparisonStage]||modeNames[r.config.testSessionMode]||r.config.testSessionMode);head.append(h);tag(head,stateNames[r.state]||r.state);
 para(card,r.sessionId,'session-meta');const tags=document.createElement('div');tags.className='session-tags';card.append(tags);
 if(r.config.comparisonEnvironment)tag(tags,EvidenceUI.label('environment',r.config.comparisonEnvironment),r.config.comparisonEnvironment==='desktop_fixed_gaze'?'sim':'');
 else if(r.config.testSessionMode.startsWith('comparison'))tag(tags,'Legacy eye configuration');
 tag(tags,r.config.testSessionMode.includes('rehearsal')?'Task rehearsal · no EEG model':'Engineering session');
 if(r.scoreThresholds)tag(tags,`ON ≤ ${Number(r.scoreThresholds.on).toFixed(2)} · OFF ≥ ${Number(r.scoreThresholds.off).toFixed(2)}`,'num');
 if(r.sourceModel)para(card,modelLabel(r.sourceModel)+' · '+r.sourceModel.modelId,'session-meta');
 if(r.lastTraining){
   const t=r.lastTraining,auc=Number.isFinite(t.meanHeldAuc)?` · mean AUC ${t.meanHeldAuc.toFixed(3)}`:'';
   const folds=t.foldAuc?.length?` · folds ${t.foldAuc.map(v=>v.toFixed(3)).join(' / ')}`:'';
   para(card,`Training: ${EvidenceUI.label('training',t.status).toLowerCase()} · ${t.hasModel?'model saved':'no model produced'}${auc}${folds}${t.aucGating===false?' · AUC reported only':''}`,'session-meta');
 }
 const detail=document.createElement('details');card.append(detail);const sum=document.createElement('summary');sum.textContent='Session details';detail.append(sum);
 para(detail,'Storage: '+r.folder,'session-meta');para(detail,'Core archive: '+r.archive.status,'session-meta');
 const pre=document.createElement('pre');pre.textContent=JSON.stringify({environment:r.config.comparisonEnvironment,stage:r.config.comparisonStage||'legacy_pair',order:r.config.comparisonStage?null:r.config.counterbalanceGroup>=2?'BA':'AB',model:r.config.testModelId,thresholds:r.scoreThresholds,initialThresholds:r.initialScoreThresholds,thresholdHistory:r.thresholdHistory,training:r.lastTraining},null,2);detail.append(pre);
 const actions=document.createElement('div');actions.className='actions';card.append(actions);
 if(['comparison','comparison_candidate','comparison_rehearsal'].includes(r.config.testSessionMode)){
  const audit=document.createElement('a');audit.className='button primary';audit.textContent='Open report';audit.href='/comparison?'+new URLSearchParams({session:r.sessionId});actions.append(audit);
  const download=document.createElement('a');download.className='button';download.textContent='Download JSON';download.href='/tests/comparison-report?'+new URLSearchParams({session:r.sessionId});download.download='comparison-report.json';actions.append(download);
 }
 const open=document.createElement('button');open.type='button';open.textContent='Open folder';open.onclick=()=>action(()=>api('/tests/open-folder',{folder:r.folder}));actions.append(open);return card;
}
function historyView(){
 const selected=$('history-person').value==='current'?$('participant').value.trim():null,filter=$('history-state').value;
 const rows=(lastStatus?.sessions||[]).slice().reverse().filter(r=>(!selected||r.participantCode===selected)&&(filter==='all'||filter==='active'&&active(r)||filter==='complete'&&r.state==='complete'||filter==='incomplete'&&!active(r)&&r.state!=='complete'));
 $('history-count').textContent=`${rows.length} matching · latest 20 saved sessions`;
 const shown=rows.slice(0,historyLimit),key=JSON.stringify(shown.map(({live,...r})=>r))+historyLimit+selected+filter;
 if(key!==historyKey){historyKey=key;$('sessions').replaceChildren(...shown.map(render));if(!shown.length)para($('sessions'),'No sessions match these filters.','empty-state');}
 $('more-sessions').hidden=rows.length<=historyLimit;
}
function workspace(){
 const mode=$('mode').value,comparison=['comparison','comparison_candidate','comparison_rehearsal'].includes(mode),rehearsal=mode==='comparison_rehearsal';
 const current=lastStatus?.sessions.find(active),locked=!!current,live=current?.live,fresh=live && live.telemetryAge<=5;
 const model=models.find(m=>m.modelId===$('model').value),needsModel=['comparison_candidate','comparison','bioadaptive'].includes(mode),needsEEG=!['practice','calibration_rehearsal','comparison_rehearsal'].includes(mode);
 const titles={practice:['Prepare practice','Get familiar with the controls and task before recording an experiment.'],calibration:['Train a personal model','Collect calibration EEG, then review and save the resulting model.'],comparison:['Prepare A/B comparison','Choose the model and environment. Video thresholds remain editable during either round.'],legacy:['Legacy engineering setup','Existing protocol options remain available for compatibility.']};
 for(const [stage,label] of Object.entries(comparisonStages))titles[stage]=['Prepare '+label,stage==='fixed_exposure'?'2 observe-only warmups, then 16 scored trials without video and 16 with continuous video. Review the score distributions and exploratory threshold candidates in the report.':'2 observe-only warmups and 32 scored trials with the existing controller. Select the same saved model and thresholds when comparing stages.'];
 $('stage-title').textContent=titles[chosenStage][0];$('stage-description').textContent=titles[chosenStage][1];$('mode-wrap').hidden=comparison;
 $('rehearsal').hidden=!comparison;$('rehearsal').disabled=locked||busy;$('rehearsal').textContent=rehearsal?'Use a saved model instead':'Task rehearsal instead';$('rehearsal-note').hidden=!rehearsal;
 const config=current?.config,short=!config||config.evaluationPolicy==='behavior5-continuous-stride2-v3';
 $('protocol-facts').replaceChildren();for(const [value,label] of (chosenStage==='fixed_exposure'?[[config?.trainingTrials||34,'total trials (2 warmups)'],[config?.comparisonRevision==='comparison-three-stages30-v1'?13:16,'scored video-off trials'],[config?.comparisonRevision==='comparison-three-stages30-v1'?15:16,'scored video-on trials']]:[[config?.trainingTrials||(chosenStage in comparisonStages?34:60),'trials in this stage'],[short?2:5,'trials between checks'],[short?5:10,'scored trials per decision']])){const d=document.createElement('div');d.className='metric';const b=document.createElement('strong');b.textContent=value;d.append(b);const span=document.createElement('span');span.textContent=label;d.append(span);$('protocol-facts').append(d);}
 const receiving=lastStatus?.physiology.connection==='receiving',rows=[];
 /* Show only the inputs this run option uses; an unused input is not listed as a warning. */
 if(needsEEG)rows.push(['EEG',receiving?'Receiving':'Not receiving',receiving?'ready':'blocked']);
 if(needsModel)rows.push(['Model',model?'Selected':'Select a saved workload model',model?'ready':'waiting']);
 else if(mode==='calibration')rows.push(['Model','Trained in this session','info']);
 rows.push(['Unity',fresh?'Connected':locked?'Waiting for Unity':'Connects after you create the assignment',fresh?'ready':'info']);
 $('eeg-rate').hidden=!needsEEG;
 if(comparison)rows.push(['Eye input',$('environment').value==='desktop_fixed_gaze'?'Simulated · 75% / 70%':fresh?(live.gazeStatus||'Awaiting tracked eyes'):'Quest Pro check in Unity',$('environment').value==='desktop_fixed_gaze'?'ready':fresh&&live.gazeStatus==='TRACKED EYES'?'ready':fresh?'waiting':'info']);
 $('readiness').replaceChildren();for(const [name,value,status] of rows){const d=document.createElement('div');d.className='ready-row';d.dataset.state=status;const i=document.createElement('span'),a=document.createElement('span'),b=document.createElement('span');i.className='ready-icon';i.textContent={ready:'✓',blocked:'✗',waiting:'•',info:'○'}[status];i.setAttribute('aria-hidden','true');a.textContent=name;b.textContent=value;d.append(i,a,b);$('readiness').append(d);}
 const eegMissing=needsEEG&&!receiving;
 $('next-title').textContent=locked?(fresh?'Continue in Unity':'Open Unity'):(needsModel&&!model?'Select a model':eegMissing?'Start the EEG stream':'Create the session');
 $('next-detail').textContent=locked?(fresh?'Complete the seating and response checks in Unity. You can still edit and apply video thresholds here.':'Your assignment is saved. Open the updated Unity task to connect and complete preparation.'):(needsModel&&!model?'Choose a saved workload model for this participant, or use Task rehearsal.':eegMissing?'Not ready to record: EEG is not arriving. Start streaming in the Mindtooth app before starting the task in Unity.':'Review these settings, then create the assignment. Headset checks happen in Unity.');
 const restartRequired=chosenStage in comparisonStages&&!independentReady;
 $('start').disabled=busy||locked||needsModel&&!model||restartRequired;$('run-card').hidden=!current;
 if(restartRequired){$('next-title').textContent='Restart the Console service';$('next-detail').textContent='This backend still uses the earlier comparison protocol. Restart the service and refresh this page before creating a 34-trial stage.';}
 if(current){const box=$('run-summary');box.replaceChildren();para(box,current.participantCode+' · '+(fresh?EvidenceUI.label('round',live.stage):stateNames[current.state]),'status');para(box,!fresh?'No fresh Unity telemetry':live.videoPlaying?'Video playing':'Video off','muted');if(fresh&&Number.isFinite(live.completedTrials))para(box,`${live.completedTrials}${['fixed_exposure','layer_a','layer_b'].includes(live.stage)?' / '+current.config.trainingTrials:''} trials completed`,'muted');}
 environmentNote();historyView();
}
async function refresh(){if(refreshing)return;refreshing=true;try{const s=await api('/tests/status');const health=await api('/console/health');independentReady=health.comparisonRevision==='comparison-three-stages34-v2'||health.revision==='evidence-console-v1.32-three-stages34';lastStatus=s;vendorLabel();$('signal-summary').textContent=`Mindtooth EEG · ${s.physiology.sourceName || "Waiting for the device"} · ${s.physiology.connection} (${s.physiology.source}) · Mapped roles: ${s.signalRoles.join(', ')||'none'} · Mindtooth app: µV · LSL conversion to µV: ${s.eegUnitsConfirmed?'confirmed':'pending verification before calibration'}.`;if(!initialized){$('participant').value=s.settings.participantCode;$('folder').value=s.settings.storageFolder;initialized=true;notice('Connected to the Console service. Choose a stage, participant and run option.');await refreshModels();}const current=s.sessions.find(active);const locked=!!current;thresholdEditor();
 const quality=current?.live?.calibrationQuality, banner=$('calibration-quality');
 banner.hidden=!quality || current.live.telemetryAge>5;
 banner.textContent=banner.hidden?'':quality.text;
 banner.className='notice'+(quality?.warning?' amber':'');
 if(current && shownSession!==current.sessionId){shownSession=current.sessionId;$('participant').value=current.participantCode;$('folder').value=current.storageFolder;const run=current.config.testSessionMode==='comparison'?'comparison_candidate':current.config.testSessionMode;const stage=current.config.comparisonStage||Object.keys(options).find(k=>options[k].some(([v])=>v===run));setStage(stage,run);$('environment').value=current.config.comparisonEnvironment || 'quest_pro';$('order').value=current.config.counterbalanceGroup>=2?'BA':'AB';$('candidate').checked=current.config.allowCandidateModel;modes();await refreshModels();$('model').value=current.config.testModelId;info();}if(current && !$('notice').classList.contains('error'))notice(current.live?`Unity connected · ${current.participantCode} · ${EvidenceUI.label('round',current.live.stage)} · ${EvidenceUI.human(current.live.modelStatus)}`:`${current.state==='prepared'?'Waiting for Unity to claim the assignment':'Waiting for Unity configuration acknowledgement'} · ${current.sessionId}`);for(const id of ['participant','stage','order','mode','environment','folder','model','candidate','start','browse','refresh'])$(id).disabled=locked||busy;$('people').replaceChildren(...[...new Set(s.sessions.map(r=>r.participantCode))].map(p=>new Option(p,p)));for(const button of $('stage').querySelectorAll('button'))button.disabled=locked||busy;workspace();}catch(e){$('calibration-quality').hidden=true;notice('Service unavailable: '+e.message,true);$('start').disabled=true;}finally{refreshing=false;}}
for(const id of ['on-threshold','off-threshold'])$(id).oninput=()=>{thresholdDirty=true;thresholdEditor();rememberGraphSelection();workspace();};
$('restore-thresholds').onclick=()=>{const profile=models.find(m=>m.modelId===$('model').value)?.thresholdProfile;if(!profile?.recommended)return;$('on-threshold').value=profile.recommended.on;$('off-threshold').value=profile.recommended.off;thresholdDirty=true;thresholdEditor();rememberGraphSelection();notice('Recommendation copied to the draft. For a running session, press Apply thresholds to use it.');};
$('apply-thresholds').onclick=()=>{const r=lastStatus?.sessions.find(active);if(!r)return;action(async()=>{
 const updated=await api('/tests/thresholds',{sessionId:r.sessionId,expectedRevision:r.thresholdRevision||0,scoreThresholds:thresholdValues()});
 Object.assign(r,updated);thresholdDirty=false;thresholdEditor();rememberGraphSelection();
 notice('Thresholds applied. Subsequent decisions use these values; previous decisions keep their recorded thresholds.');
});};
$('setup').onsubmit=e=>{e.preventDefault();action(async()=>{if(chosenStage in comparisonStages&&!independentReady)throw Error('Restart the Console service before creating an independent 34-trial stage.');const r=await api('/tests/prepare',{participantCode:$('participant').value.trim(),storageFolder:$('folder').value,sessionMode:$('mode').value,comparisonStage:chosenStage in comparisonStages?chosenStage:'',comparisonEnvironment:$('environment-wrap').hidden?'quest_pro':$('environment').value,order:$('order').value,vendorIdentity:vendorIdentity(),modelId:$('model').value,...($('threshold-wrap').hidden?{}:{scoreThresholds:thresholdValues()}),allowCandidate:$('candidate').checked});notice(`Prepared ${r.sessionId}. Waiting for Unity to claim the assignment. Complete the preparation checks in Unity before starting trials.`);});};
$('stage').onclick=e=>{const button=e.target.closest('button');if(button&&!button.disabled){setStage(button.value);rememberGraphSelection();}};$('mode').onchange=()=>{modes();rememberGraphSelection();};$('model').onchange=()=>{info();rememberGraphSelection();workspace();};$('refresh').onclick=()=>action(refreshModels);
for(const id of ['participant','folder'])$(id).onchange=()=>{localStorage.removeItem('study.rfPreview');action(refreshModels);};
$('browse').onclick=()=>action(async()=>{notice('Choose a storage folder in the desktop dialog.');const result=await api('/tests/choose-folder',{});if(result.folder){localStorage.removeItem('study.rfPreview');$('folder').value=result.folder;await refreshModels();notice('Folder selected. It will be checked and saved when you start a session.');}});
$('open').onclick=()=>action(()=>api('/tests/open-folder',{folder:$('folder').value}));
const initialStage=location.search==='?legacy'?'legacy':new URLSearchParams(location.search).get('stage')||sessionStorage.getItem('study.setupStage')||'practice';setStage(Object.hasOwn(options,initialStage)?initialStage:'practice');async function poll(){if(!busy)await refresh();setTimeout(poll,1000);}poll();

$('environment').onchange=workspace;
for(const b of document.querySelectorAll('[data-environment]'))b.onclick=()=>{if(lastStatus?.sessions.find(active))return;$('environment').value=b.dataset.environment;sessionStorage.setItem('study.environment',b.dataset.environment);workspace();};
if(sessionStorage.getItem('study.environment'))$('environment').value=sessionStorage.getItem('study.environment');
$('rehearsal').onclick=()=>{if(lastStatus?.sessions.find(active))return;$('mode').value=$('mode').value==='comparison_rehearsal'?'comparison_candidate':'comparison_rehearsal';modes();rememberGraphSelection();};
for(const id of ['history-person','history-state'])$(id).onchange=()=>{historyLimit=6;historyView();};
$('more-sessions').onclick=()=>{historyLimit+=6;historyView();};
$('stop-active').onclick=()=>{const r=lastStatus?.sessions.find(active);if(r)action(async()=>{await api('/tests/stop',{sessionId:r.sessionId});notice('Stop requested. The recorded session is retained.');});};
