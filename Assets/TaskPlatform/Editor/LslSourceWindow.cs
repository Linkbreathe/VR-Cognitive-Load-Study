using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace EvidenceVR.Editor
{
    [Serializable] public sealed class LslInputSelection
    {
        public string kind="EEG", name="", source_id="", uid="", stream_type="", data_origin="unknown";
        public double sample_rate=250, scale_to_units=1, amplitude_limit=150, window_seconds=12, hrv_window_seconds=60;
        public int[] channels={0};
        public int channel_count, channel_format;
        public bool differential, synthetic;
    }
    [Serializable] public sealed class LslSelectionFile { public string revision="lsl-source-selector-v2"; public LslInputSelection[] streams, profiles; }
    [Serializable] public sealed class LslBridgeStatus { public PhysiologyDisplayStatus physiologyStatus; }
    [Serializable] public sealed class LslDiscoveredStream
    {
        public string name, source_id, uid, stream_type, discovery_error;
        public double sample_rate;
        public int channel_count, channel_format;
        public bool recorded;
        public string[] channel_labels;
    }
    [Serializable] public sealed class LslDiscovery { public LslDiscoveredStream[] streams; }

    public sealed class LslSourceWindow : EditorWindow
    {
        public const string AssetsRoot="Assets/TaskPlatform/Editor/";
        public static string ProjectRoot=>Path.GetDirectoryName(Application.dataPath);
        public static string SelectionPath=>Path.Combine(ProjectRoot,"UserSettings/lsl-source-selection.json");
        const string ProcessKey="EvidenceVR.LslSource.Process", StartKey="EvidenceVR.LslSource.Start", LogKey="EvidenceVR.LslSource.Log";
        static Process cachedBridge;
        [SerializeField] LslDiscoveredStream selected;
        [SerializeField] List<LslInputSelection> mappings=new List<LslInputSelection>();
        [SerializeField] List<LslInputSelection> profiles=new List<LslInputSelection>();
        [SerializeField] string provenance="unknown";
        [SerializeField] string pythonPath;
        [SerializeField] int port=8765;
        LslDiscoveredStream[] discovered=Array.Empty<LslDiscoveredStream>();
        bool busy,polling;
        double nextPoll;
        string connection="Disconnected";
        Label status,serviceStatus;
        VisualElement configuration;
        Button scan,start,stop;

        [MenuItem("Evidence VR/Stage 2/LSL Signal Sources")]
        public static void Open()=>GetWindow<LslSourceWindow>("LSL Signal Sources");

        void OnEnable()
        {
            minSize=new Vector2(560,420);
            if(string.IsNullOrEmpty(pythonPath))pythonPath=Path.Combine(ProjectRoot,"Tools/AdaptiveTraining/.venv/Scripts/python.exe");
            if(File.Exists(SelectionPath))
            {
                try{LoadSelection(SelectionPath);}catch(Exception e){Debug.LogWarning("LSL selection could not be loaded: "+e.Message);}
            }
            EditorApplication.update+=RefreshState;
        }
        void OnDisable(){EditorApplication.update-=RefreshState;}

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AssetsRoot+"LslSourceWindow.uxml").CloneTree(rootVisualElement);
            status=rootVisualElement.Q<Label>("status");serviceStatus=rootVisualElement.Q<Label>("serviceStatus");
            configuration=rootVisualElement.Q("configuration");
            BuildFields();
            var actions=rootVisualElement.Q("actions");
            scan=new Button(()=>Run(ScanAsync)){text="Scan"};actions.Add(scan);
            start=new Button(()=>Run(StartBridgeAsync)){text="Connect"};actions.Add(start);
            stop=new Button(()=>TryAction(()=>{StopBridge();connection="Disconnected";SetStatus("Disconnected. You can choose another source.");})){text="Disconnect"};actions.Add(stop);
            status.enableRichText=false;serviceStatus.enableRichText=false;
            SetStatus(OwnedBridge()==null?"Start your publisher, scan, then choose a signal source.":"Source selection is locked until you disconnect.");
            RefreshState();
        }
        void BuildFields()
        {
            configuration.Clear();
            var options=new List<string>{"Select a signal source..."};
            options.AddRange(discovered.Select(s=>$"{s.name} | {s.stream_type} | {s.channel_count} ch | {(s.sample_rate==0?"events":s.sample_rate.ToString("g",CultureInfo.InvariantCulture)+" Hz")} | {ShortUid(s.uid)}"));
            int current=selected==null?0:Array.FindIndex(discovered,s=>s.uid==selected.uid)+1;
            if(selected!=null && current==0){options.Add(selected.name+" (saved; scan to refresh)");current=options.Count-1;}
            var popup=new PopupField<string>("Signal source",options,current){name="sourcePicker"};configuration.Add(popup);
            popup.RegisterValueChangedCallback(e=>TryAction(()=>{int i=options.IndexOf(e.newValue)-1;if(i>=0 && i<discovered.Length)SelectSource(discovered[i]);else if(i==-1){RememberMappings();selected=null;mappings.Clear();BuildFields();}}));
            var summary=new Label(selected==null?"No source selected.":selected.name+" — "+(mappings.Count==0?"Receive only":string.Join(" + ",mappings.Select(m=>m.kind))+" mapping")){name="sourceSummary",enableRichText=false};summary.AddToClassList("lsl-note");configuration.Add(summary);
            var advanced=new Foldout{text="Advanced settings",name="advancedSettings",value=false};configuration.Add(advanced);
            var detail=new Label(selected==null?"Select a source to inspect its identity.":"Source ID: "+selected.source_id+"\nOutlet UID: "+selected.uid+"\nChannels: "+string.Join(", ",selected.channel_labels??Array.Empty<string>())){enableRichText=false};detail.AddToClassList("lsl-note");advanced.Add(detail);
            var modes=new List<string>{"Receive only","EEG mapping","ECG mapping","EEG + ECG mapping"};
            string mode=mappings.Any(m=>m.kind=="EEG")?(mappings.Any(m=>m.kind=="ECG")?modes[3]:modes[1]):mappings.Any(m=>m.kind=="ECG")?modes[2]:modes[0];
            var processing=new PopupField<string>("Processing",modes,modes.IndexOf(mode));advanced.Add(processing);
            processing.RegisterValueChangedCallback(e=>TryAction(()=>
            {
                if(selected==null)throw new InvalidOperationException("Select a signal source first.");
                if(e.newValue!=modes[0] && (selected.sample_rate<100 || selected.channel_format==3)){processing.SetValueWithoutNotify(mode);throw new InvalidOperationException("Raw EEG/ECG processing requires numeric samples at 100 Hz or above.");}
                RememberMappings();mappings.Clear();
                if(e.newValue==modes[1] || e.newValue==modes[3])mappings.Add(NewMapping("EEG"));
                if(e.newValue==modes[2] || e.newValue==modes[3])mappings.Add(NewMapping("ECG"));
                BuildFields();rootVisualElement.Q<Foldout>("advancedSettings").value=true;
            }));
            foreach(var mapping in mappings)AddMapping(advanced,mapping);
            var origins=new List<string>{"Unknown / unreviewed","Live device (unvalidated)","Recorded replay","Synthetic signal"};
            var codes=new[]{"unknown","live_sensor","recorded_xdf_replay","synthetic"};
            var origin=new PopupField<string>("Source provenance",origins,Math.Max(0,Array.IndexOf(codes,provenance)));advanced.Add(origin);
            origin.SetEnabled(selected?.recorded!=true);
            origin.RegisterValueChangedCallback(e=>provenance=codes[origins.IndexOf(e.newValue)]);
            var python=new TextField("Python executable"){value=pythonPath,tooltip="Python environment used to start the receiving service. Normally keep the project default."};python.RegisterValueChangedCallback(e=>pythonPath=e.newValue);advanced.Add(python);
            var p=new IntegerField("Bridge port"){value=port,tooltip="HTTP port between Unity and the Python service. This is not the LSL publisher port."};p.RegisterValueChangedCallback(e=>port=e.newValue);advanced.Add(p);
            var buttons=new VisualElement();buttons.AddToClassList("lsl-actions");advanced.Add(buttons);
            buttons.Add(new Button(()=>TryAction(SaveSelection)){text="Save settings"});
            buttons.Add(new Button(()=>{var path=EditorUtility.OpenFilePanel("Import LSL mapping",ProjectRoot,"json");if(path.Length>0)TryAction(()=>ImportMappings(path));}){text="Import mapping"});
            var diagnostics=new VisualElement();diagnostics.AddToClassList("lsl-actions");advanced.Add(diagnostics);
            diagnostics.Add(new Button(()=>Application.OpenURL("http://127.0.0.1:"+port+"/")){text="Open monitor"});
            diagnostics.Add(new Button(()=>{var path=SessionState.GetString(LogKey,"");if(File.Exists(path))EditorUtility.RevealInFinder(path);}){text="Show log"});
            diagnostics.name="diagnostics";
            var note=new Label("Receive only preserves numeric or text samples without inferring physiology. Raw EEG/ECG mappings require explicit channel/units configuration. Replay provenance stays in logs. A restarted publisher has a new outlet UID: scan and select it again.");note.AddToClassList("lsl-note");advanced.Add(note);
        }
        static string ShortUid(string uid)=>string.IsNullOrEmpty(uid)?"no UID":uid.Substring(0,Math.Min(8,uid.Length));
        static LslInputSelection Clone(LslInputSelection input)=>JsonUtility.FromJson<LslInputSelection>(JsonUtility.ToJson(input));
        public static bool ProfileMatches(LslInputSelection input,LslDiscoveredStream source)
        {
            return source!=null && !string.IsNullOrEmpty(input.name) && !string.IsNullOrEmpty(input.stream_type) &&
                input.name==source.name && input.stream_type==source.stream_type && input.source_id==source.source_id &&
                (!string.IsNullOrEmpty(input.source_id) || input.uid==source.uid) && input.channel_count==source.channel_count &&
                input.channel_format==source.channel_format && Math.Abs(input.sample_rate-source.sample_rate)<.01;
        }
        void StampIdentity(LslInputSelection input)
        {
            input.name=selected.name;input.source_id=selected.source_id??"";input.uid=selected.uid;input.stream_type=selected.stream_type;
            input.sample_rate=selected.sample_rate;input.channel_count=selected.channel_count;input.channel_format=selected.channel_format;
            input.data_origin=selected.recorded?"recorded_xdf_replay":provenance;
            input.synthetic=input.data_origin=="recorded_xdf_replay" || input.data_origin=="synthetic";
        }
        void RememberMappings()
        {
            if(selected==null)return;
            profiles.RemoveAll(m=>ReusableProfile(m,selected,discovered));
            var remembered=mappings.Count>0?mappings:new List<LslInputSelection>{new LslInputSelection{kind="OBSERVE",channels=Enumerable.Range(0,selected.channel_count).ToArray(),scale_to_units=1,amplitude_limit=1,window_seconds=1}};
            foreach(var m in remembered){StampIdentity(m);profiles.Add(Clone(m));}
        }
        public static bool ReusableProfile(LslInputSelection input,LslDiscoveredStream source,LslDiscoveredStream[] inventory)
        {return ProfileMatches(input,source) && (input.uid==source.uid || inventory.Count(s=>s.source_id==source.source_id && s.name==source.name && s.stream_type==source.stream_type)==1);}
        public void SelectSource(LslDiscoveredStream source)
        {
            RequireEditable();
            if(string.IsNullOrEmpty(source.uid) || source.channel_count<1 || source.sample_rate<0)throw new InvalidOperationException("The outlet is missing a valid UID or stream layout. Scan again.");
            if(!string.IsNullOrEmpty(source.discovery_error))throw new InvalidOperationException(source.discovery_error);
            RememberMappings();selected=source;
            var remembered=profiles.Where(m=>ReusableProfile(m,source,discovered)).ToList();
            mappings=remembered.Where(m=>m.kind=="EEG" || m.kind=="ECG").Select(Clone).ToList();
            provenance=source.recorded?"recorded_xdf_replay":remembered.FirstOrDefault()?.data_origin??"unknown";
            BuildFields();SetStatus("Selected "+source.name+". "+(mappings.Count==0?"Ready to receive without a channel mapping.":"Saved channel mapping applied."));
        }
        LslInputSelection NewMapping(string kind)
        {
            var prior=profiles.FirstOrDefault(m=>m.kind==kind && ReusableProfile(m,selected,discovered));
            if(prior!=null)return Clone(prior);
            var input=new LslInputSelection{kind=kind,window_seconds=kind=="ECG"?30:12,amplitude_limit=kind=="ECG"?5:150,channels=Array.Empty<int>()};StampIdentity(input);return input;
        }
        void AddMapping(VisualElement parent,LslInputSelection input)
        {
            var box=new VisualElement();box.AddToClassList("lsl-section");parent.Add(box);box.Add(new Label(input.kind+" channel mapping (zero-based)"));
            var channels=new TextField("Channels (e.g. 0,1)"){value=string.Join(",",input.channels??Array.Empty<int>())};box.Add(channels);
            channels.RegisterValueChangedCallback(e=>{try{input.channels=e.newValue.Split(',').Select(x=>int.Parse(x.Trim(),CultureInfo.InvariantCulture)).ToArray();}catch{input.channels=Array.Empty<int>();SetStatus("Channels must be comma-separated integer indices.");}});
            Number(box,"Scale to analysis units",input.scale_to_units,v=>input.scale_to_units=v);
            if(input.kind!="FEATURES")Number(box,"Amplitude limit",input.amplitude_limit,v=>input.amplitude_limit=v);
            if(input.kind=="ECG")
            {
                var diff=new Toggle("Differential (first minus second)"){value=input.differential};diff.RegisterValueChangedCallback(e=>input.differential=e.newValue);box.Add(diff);
            }
        }
        static void Text(VisualElement parent,string title,string value,Action<string> changed)
        {var field=new TextField(title){value=value};field.RegisterValueChangedCallback(e=>changed(e.newValue));parent.Add(field);}
        static void Number(VisualElement parent,string title,double value,Action<double> changed)
        {var field=new DoubleField(title){value=value};field.RegisterValueChangedCallback(e=>changed(e.newValue));parent.Add(field);}
        void LoadSelection(string path)
        {
            var file=JsonUtility.FromJson<LslSelectionFile>(File.ReadAllText(path));
            if(file?.streams==null || file.streams.Length==0)throw new InvalidOperationException("No saved stream selection.");
            profiles=(file.profiles??file.streams).Select(Clone).ToList();
            foreach(var profile in profiles)if(profile.synthetic && profile.data_origin!="recorded_xdf_replay")profile.data_origin="synthetic";
            var first=file.streams[0];
            selected=new LslDiscoveredStream{name=first.name,source_id=first.source_id,uid=first.uid,stream_type=first.stream_type,sample_rate=first.sample_rate,channel_count=first.channel_count,channel_format=first.channel_format,recorded=first.data_origin=="recorded_xdf_replay"};
            mappings=file.streams.Where(m=>(m.kind=="EEG" || m.kind=="ECG") && ProfileMatches(m,selected) && m.uid==selected.uid).Select(Clone).ToList();provenance=first.synthetic && first.data_origin!="recorded_xdf_replay"?"synthetic":first.data_origin;
        }
        void ImportMappings(string path)
        {
            RequireEditable();if(selected==null)throw new InvalidOperationException("Select the target signal source first.");
            var file=JsonUtility.FromJson<LslSelectionFile>(File.ReadAllText(path));
            var imported=(file?.streams??Array.Empty<LslInputSelection>()).Where(m=>m.kind=="EEG" || m.kind=="ECG").ToList();
            if(imported.Count==0 || imported.GroupBy(m=>m.kind).Any(g=>g.Count()>1))throw new InvalidOperationException("Import one EEG and/or ECG mapping.");
            if(imported.Any(m=>m.source_id!=selected.source_id || Math.Abs(m.sample_rate-selected.sample_rate)>.01 || (!string.IsNullOrEmpty(m.name) && m.name!=selected.name) || (!string.IsNullOrEmpty(m.stream_type) && m.stream_type!=selected.stream_type)))throw new InvalidOperationException("The mapping does not identify this source. Review the source/channel configuration before importing.");
            provenance=selected.recorded?"recorded_xdf_replay":imported[0].data_origin;
            foreach(var m in imported)StampIdentity(m);
            ValidateSelection(new LslSelectionFile{streams=imported.ToArray()});mappings=imported;RememberMappings();BuildFields();
            SetStatus("Imported channel mapping for "+selected.name+". Review it in Advanced settings.");
        }
        public static void ValidateSelection(LslSelectionFile file)
        {
            if(file?.streams==null || file.streams.Length==0)throw new InvalidOperationException("Select a signal source.");
            foreach(var s in file.streams)
            {
                if(string.IsNullOrWhiteSpace(s.uid) && string.IsNullOrWhiteSpace(s.source_id))throw new InvalidOperationException("Select a valid outlet identity.");
                if(double.IsNaN(s.sample_rate)||double.IsInfinity(s.sample_rate)||s.sample_rate<0 || new[]{s.scale_to_units,s.amplitude_limit}.Any(x=>double.IsNaN(x)||double.IsInfinity(x)||x<=0))throw new InvalidOperationException("Invalid stream rate, scale or amplitude limit.");
                if((s.kind=="EEG" || s.kind=="ECG") && (s.sample_rate<100 || s.channel_format==3))throw new InvalidOperationException("Raw EEG/ECG processing requires numeric samples at 100 Hz or above.");
                if(s.channels==null || s.channels.Length==0 || s.channels.Any(c=>c<0 || (s.channel_count>0 && c>=s.channel_count)) || s.channels.Distinct().Count()!=s.channels.Length)throw new InvalidOperationException(s.kind+": invalid or duplicate channel indices.");
                if((s.differential && (s.kind!="ECG" || s.channels.Length!=2)) || (s.kind=="ECG" && !s.differential && s.channels.Length!=1))throw new InvalidOperationException("Use one ECG channel or exactly two differential channels.");
                if(s.data_origin=="recorded_xdf_replay" || s.data_origin=="synthetic")s.synthetic=true;
                if(s.kind=="OBSERVE" && (s.scale_to_units!=1 || s.differential))throw new InvalidOperationException("Receive only preserves original sample values.");
            }
        }
        void RequireEditable()
        {if(EditorApplication.isPlayingOrWillChangePlaymode || OwnedBridge()!=null)throw new InvalidOperationException("Stop Play Mode and the owned bridge before changing the selection.");}
        public void SaveSelection()
        {
            RequireEditable();
            if(selected==null || string.IsNullOrEmpty(selected.uid))throw new InvalidOperationException("Scan and select a current signal source first.");
            RememberMappings();var inputs=mappings.Select(Clone).ToList();
            if(inputs.Count==0)inputs.Add(new LslInputSelection{kind="OBSERVE",channels=Enumerable.Range(0,selected.channel_count).ToArray(),scale_to_units=1,amplitude_limit=1,window_seconds=1});
            foreach(var input in inputs)StampIdentity(input);
            var file=new LslSelectionFile{streams=inputs.ToArray(),profiles=profiles.ToArray()};ValidateSelection(file);
            Directory.CreateDirectory(Path.GetDirectoryName(SelectionPath));File.WriteAllText(SelectionPath,JsonUtility.ToJson(file,true));
            SetStatus("Saved "+selected.name+".");
        }
        async Task ScanAsync()
        {
            RequireEditable();string output=Path.Combine(ProjectRoot,"Temp/lsl-discovery-"+Guid.NewGuid().ToString("N")+".json");
            await RunPython("lsl_sources.py","--discover","--output",output);
            discovered=JsonUtility.FromJson<LslDiscovery>(File.ReadAllText(output)).streams??Array.Empty<LslDiscoveredStream>();
            BuildFields();SetStatus(discovered.Length+" signal source(s) found. Choose one from the list.");
        }
        async Task StartBridgeAsync()
        {
            SaveSelection();if(port<1024 || port>65535)throw new InvalidOperationException("Use a port between 1024 and 65535.");
            var adaptivePath=Path.Combine(Application.persistentDataPath,"adaptive-training-config.json");
            if(File.Exists(adaptivePath))
            {
                var adaptive=JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText(adaptivePath));
                if(adaptive.enabled && (!Uri.TryCreate(adaptive.controllerUrl,UriKind.Absolute,out var uri) || !uri.IsLoopback || uri.Port!=port))throw new InvalidOperationException("The enabled Editor configuration must point to http://127.0.0.1:"+port+" before starting this local bridge.");
            }
            string output=Path.Combine(ProjectRoot,"Evidence/LslSourceSelection/Bridge-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));Directory.CreateDirectory(output);
            string snapshot=Path.Combine(output,"lsl-selection.json");
            await RunPython("lsl_sources.py","--validate",SelectionPath,"--output",snapshot);
            RequireEditable();string log=Path.Combine(output,"bridge.log");
            var process=Process.Start(ProcessInfo("service.py","--output",Path.Combine(output,"service"),"--lsl-config",snapshot,"--port",port.ToString(CultureInfo.InvariantCulture),"--log-file",log));cachedBridge=process;
            SessionState.SetInt(ProcessKey,process.Id);SessionState.SetString(StartKey,process.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));SessionState.SetString(LogKey,log);
            await Task.Delay(900);
            if(OwnedBridge()==null)throw new InvalidOperationException("Bridge exited. "+(File.Exists(log)?File.ReadAllText(log):"Check Python and the selected port."));
            connection="Connecting";nextPoll=0;SetStatus("Source selection is locked until you disconnect.");
            Debug.Log("LSL source bridge started from "+snapshot+". Monitor: http://127.0.0.1:"+port+"/");
        }
        public static Process OwnedBridge()
        {
            try
            {
                if(cachedBridge!=null)
                {
                    if(!cachedBridge.HasExited)return cachedBridge;
                    cachedBridge.Dispose();cachedBridge=null;
                }
                var p=Process.GetProcessById(SessionState.GetInt(ProcessKey,-1));
                if(!p.HasExited && p.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture)==SessionState.GetString(StartKey,"")){cachedBridge=p;return p;}
                p.Dispose();return null;
            }
            catch{return null;}
        }
        public static void StopBridge()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before stopping the source bridge.");
            var process=OwnedBridge();if(process!=null){process.Kill();process.WaitForExit(2000);}
            cachedBridge?.Dispose();cachedBridge=null;
            SessionState.EraseInt(ProcessKey);SessionState.EraseString(StartKey);
        }
        [InitializeOnLoadMethod] static void RegisterShutdown()
        {EditorApplication.quitting+=()=>{var p=OwnedBridge();if(p!=null)p.Kill();};}
        public static string QuoteArgument(string value)
        {
            var result=new StringBuilder("\"");int slashes=0;
            foreach(char c in value)
            {
                if(c=='\\'){slashes++;continue;}
                result.Append('\\',c=='"'?slashes*2+1:slashes);result.Append(c);slashes=0;
            }
            result.Append('\\',slashes*2);return result.Append('"').ToString();
        }
        ProcessStartInfo ProcessInfo(string script,params string[] args)
        {
            if(!File.Exists(pythonPath))throw new FileNotFoundException("Choose the project's Python environment executable.",pythonPath);
            return new ProcessStartInfo{FileName=pythonPath,Arguments=string.Join(" ",new[]{Path.Combine(ProjectRoot,"Tools/AdaptiveTraining",script)}.Concat(args).Select(QuoteArgument)),WorkingDirectory=ProjectRoot,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
        }
        async Task RunPython(string script,params string[] args)
        {
            var info=ProcessInfo(script,args);info.RedirectStandardOutput=true;info.RedirectStandardError=true;
            using(var process=Process.Start(info))
            {
                var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
                bool done=await Task.Run(()=>process.WaitForExit(20000));
                if(!done){process.Kill();throw new TimeoutException("LSL discovery timed out; check the publisher and network.");}
                string output=await stdout,error=await stderr;
                if(process.ExitCode!=0)throw new InvalidOperationException(error+output);
            }
        }
        async void Run(Func<Task> action)
        {busy=true;RefreshState();try{await action();}catch(Exception e){SetStatus(e.Message);Debug.LogWarning("LSL source panel: "+e.Message);}finally{busy=false;RefreshState();}}
        void TryAction(Action action){try{action();}catch(Exception e){SetStatus(e.Message);}}
        void SetStatus(string text){if(status!=null)status.text=text;}
        void RefreshState()
        {
            if(configuration==null || start==null)return;
            bool running=OwnedBridge()!=null,playing=EditorApplication.isPlayingOrWillChangePlaymode,editable=!busy&&!running&&!playing;
            configuration.SetEnabled(true);
            configuration.Q<PopupField<string>>("sourcePicker")?.SetEnabled(editable);
            var advanced=configuration.Q<Foldout>("advancedSettings");
            if(advanced!=null)foreach(var element in advanced.contentContainer.Children())element.SetEnabled(element.name=="diagnostics" || editable);
            scan.SetEnabled(editable);start.SetEnabled(editable && selected!=null && !string.IsNullOrEmpty(selected.uid));stop.SetEnabled(running&&!playing&&!busy);
            serviceStatus.text="Status: "+(running?connection:"Disconnected")+(playing?" — stop Play Mode to change sources.":"");
            if(running && !polling && EditorApplication.timeSinceStartup>=nextPoll)PollBridge();
        }
        async void PollBridge()
        {
            polling=true;nextPoll=EditorApplication.timeSinceStartup+1;
            try
            {
                using(var request=UnityWebRequest.Get("http://127.0.0.1:"+port+"/status"))
                {
                    request.timeout=2;var operation=request.SendWebRequest();while(!operation.isDone)await Task.Yield();
                    if(request.result!=UnityWebRequest.Result.Success){connection="Service unavailable";return;}
                    var reply=JsonUtility.FromJson<LslBridgeStatus>(request.downloadHandler.text)?.physiologyStatus;
                    connection=reply==null?"Source status unavailable":(reply.sourceName??selected?.name??"")+" — "+
                        (reply.connection=="receiving"?"Receiving":reply.connection=="idle"?"Connected, waiting for events":reply.connection=="partial"?"Partial reception":reply.connection=="disconnected"?"Signal lost":"Waiting for selected outlet");
                }
            }
            catch{connection="Service unavailable";}
            finally{polling=false;}
        }
    }
}
