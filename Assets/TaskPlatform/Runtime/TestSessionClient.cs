using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace EvidenceVR
{
    // The operator assigns identity before recording. Local core records survive network loss.
    public sealed class TestSessionClient : MonoBehaviour
    {
        public const string GazeDebugInheritanceRevision="engineering-gaze-claim-v1";
        [Serializable] public sealed class GazeDebugPreferences
        {
            public string revision=GazeDebugInheritanceRevision;
            public bool highlight,ray;
        }
        [Serializable] sealed class Claim {public string clientId,controllerUrl;public GazeDebugPreferences gazeDebug;}
        [Serializable] sealed class Assignment {public bool ready;public string sessionId,gazeDebugInheritanceRevision;public AdaptiveTrainingConfig config;}
        [Serializable] sealed class Upload {public string sessionId,clientId,name,data;public long offset;public Entry[] manifest;}
        [Serializable] sealed class Entry {public string name,sha256;public long size;}
        [Serializable] sealed class Ack {public bool ok;public long offset;}
        [Serializable] sealed class LocalSession {public string sessionId,status;}
        readonly string[] coreFiles={"session.json","adaptive-training-config.json","trials.csv","practice_trials.csv","events.jsonl"};
        readonly Dictionary<string,long> offsets=new Dictionary<string,long>();
        TaskPlatformController owner;
        string endpoint;
        public string ClientId {get;private set;}
        public string Status {get;private set;}="Waiting for assignment";
        public static GazeDebugPreferences CaptureGazeDebugPreferences(AdaptiveTrainingConfig local)
        {
            bool allowed=EngineeringGazeRay.IsAllowed(local);
            return new GazeDebugPreferences{highlight=allowed && local.engineeringGazeHighlight,ray=allowed && local.engineeringGazeRay};
        }
        Claim CreateClaim()
        {
            AdaptiveTrainingConfig local=null;
            string path=Path.Combine(Application.persistentDataPath,"adaptive-training-config.json");
            try {if(File.Exists(path))local=JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText(path));}
            catch(Exception e){Debug.LogWarning("Eye debug preferences unavailable; requesting disabled overlays: "+e.Message);}
            return new Claim{clientId=ClientId,controllerUrl=endpoint,gazeDebug=CaptureGazeDebugPreferences(local)};
        }
        public void Bind(TaskPlatformController controller,string url)
        {
            owner=controller;endpoint=url.TrimEnd('/');
            ClientId=PlayerPrefs.GetString("EngineeringTestClientId","");
            if(string.IsNullOrEmpty(ClientId)){ClientId=Guid.NewGuid().ToString("N");PlayerPrefs.SetString("EngineeringTestClientId",ClientId);PlayerPrefs.Save();}
            StartCoroutine(Work());
        }
        UnityWebRequest Post(string path,object value)
        {
            var request=new UnityWebRequest(endpoint+path,"POST");
            request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(value)));
            request.downloadHandler=new DownloadHandlerBuffer();request.timeout=3;
            request.SetRequestHeader("Content-Type","application/json");return request;
        }
        bool Apply(string json)
        {
            try
            {
                var assignment=JsonUtility.FromJson<Assignment>(json);
                if(!assignment.ready)return false;
                if(assignment.config.testSessionId!=assignment.sessionId || assignment.config.controllerUrl.TrimEnd('/')!=endpoint)
                    throw new InvalidOperationException("Operator assignment mismatch");
                owner.StartAssignedTest(assignment.config);
                if(assignment.gazeDebugInheritanceRevision==GazeDebugInheritanceRevision)
                {
                    string detail=GazeDebugInheritanceRevision+";highlight="+assignment.config.engineeringGazeHighlight+";ray="+assignment.config.engineeringGazeRay+";source=local_engineering_preferences";
                    owner.Engine.Note("gaze_debug_inheritance",detail,Time.realtimeSinceStartupAsDouble,Time.frameCount);
                    Debug.Log("Engineering gaze preferences inherited: "+detail);
                }
                Status="Assignment confirmed locally; waiting for service acknowledgement";return true;
            }
            catch(Exception e){Status=e.Message;Debug.LogWarning("Operator test assignment: "+e.Message);return false;}
        }
        IEnumerator Work()
        {
            while(true)
            {
                if(owner.Engine==null || owner.Engine.Phase==TaskPhase.Complete || owner.Engine.Phase==TaskPhase.Aborted)
                {
                    using(var request=Post("/tests/claim",CreateClaim()))
                    {
                        yield return request.SendWebRequest();
                        if(request.result==UnityWebRequest.Result.Success)Apply(request.downloadHandler.text);
                        else Status="Study computer unavailable: "+request.error;
                    }
                    owner.ShowTestWaiting("Open Test sessions on the study computer.\n"+Status);
                }
                if(owner.Engine?.IsOperatorTest==true && owner.Engine.Phase==TaskPhase.Running && !owner.AdaptiveRuntime.ServiceConnected)
                    owner.Engine.Pause(Time.realtimeSinceStartupAsDouble,Time.frameCount,"test_service_connection_lost");
                foreach(var directory in PendingDirectories())yield return Sync(directory);
                yield return new WaitForSecondsRealtime(2);
            }
        }
        string[] PendingDirectories()
        {
            try
            {
                var root=Path.Combine(Application.persistentDataPath,"AdaptiveTrainingData");
                if(!Directory.Exists(root))return Array.Empty<string>();
                return Directory.GetFiles(root,"adaptive-training-config.json",SearchOption.AllDirectories)
                    .Where(p=>!File.Exists(Path.Combine(Path.GetDirectoryName(p),"test-core-sync-complete.json")) &&
                        JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText(p)).operatorManaged &&
                        JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText(p)).controllerUrl.TrimEnd('/')==endpoint)
                    .Select(Path.GetDirectoryName).ToArray();
            }
            catch(Exception e){Status="Archive scan: "+e.Message;return Array.Empty<string>();}
        }
        static byte[] ReadChunk(string path,long offset,int count)
        {
            using(var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
            {f.Position=offset;var data=new byte[Math.Min(count,Math.Max(0,f.Length-offset))];int n=f.Read(data,0,data.Length);if(n!=data.Length)Array.Resize(ref data,n);return data;}
        }
        Upload Chunk(string directory,string name,string sid)
        {
            var path=Path.Combine(directory,name);if(!File.Exists(path))return null;
            long offset=name.EndsWith(".json")?0:offsets.TryGetValue(path,out long value)?value:0;
            var bytes=ReadChunk(path,offset,65536);
            if(bytes.Length==0)return null;
            if(name.EndsWith(".json") && new FileInfo(path).Length>65536)throw new IOException("Core metadata exceeds archive chunk limit");
            return new Upload{sessionId=sid,clientId=ClientId,name=name,offset=offset,data=Convert.ToBase64String(bytes)};
        }
        Upload SafeChunk(string directory,string name,string sid)
        {try{return Chunk(directory,name,sid);}catch(Exception e){Status="Archive read: "+e.Message;return null;}}
        Entry[] Manifest(string directory)
        {
            try
            {
                return coreFiles.Select(name=>{
                    var path=Path.Combine(directory,name);
                    using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
                    using(var hash=SHA256.Create())return new Entry{name=name,size=stream.Length,sha256=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant()};
                }).ToArray();
            }
            catch(Exception e){Status="Archive manifest: "+e.Message;return null;}
        }
        IEnumerator Sync(string directory)
        {
            var sid=Path.GetFileName(directory);
            foreach(var name in coreFiles)
            {
                // Bounded chunks retain responsiveness; subsequent passes catch up larger event logs.
                for(int i=0;i<8;i++)
                {
                    var chunk=SafeChunk(directory,name,sid);if(chunk==null)break;
                    using(var request=Post("/tests/archive",chunk))
                    {
                        yield return request.SendWebRequest();
                        if(request.result!=UnityWebRequest.Result.Success){Status="Archive pending: "+request.downloadHandler.text;yield break;}
                        var ack=JsonUtility.FromJson<Ack>(request.downloadHandler.text);
                        offsets[Path.Combine(directory,name)]=ack.offset;
                        if(!ack.ok)break;
                    }
                    if(name.EndsWith(".json"))break;
                }
            }
            var entries=Manifest(directory);if(entries==null)yield break;
            bool terminal=false;
            try {terminal=JsonUtility.FromJson<LocalSession>(File.ReadAllText(Path.Combine(directory,"session.json"))).status!="in_progress";}
            catch(Exception e){Status=e.Message;}
            if(!terminal || entries.Any(e=>!e.name.EndsWith(".json") && (!offsets.TryGetValue(Path.Combine(directory,e.name),out long n) || n!=e.size)))yield break;
            using(var request=Post("/tests/archive",new Upload{sessionId=sid,clientId=ClientId,manifest=entries}))
            {
                yield return request.SendWebRequest();
                if(request.result==UnityWebRequest.Result.Success)
                {
                    File.WriteAllText(Path.Combine(directory,"test-core-sync-complete.json"),request.downloadHandler.text);
                    Status="Core Unity records archived: "+sid;Debug.Log("TEST CORE ARCHIVE COMPLETE: "+sid);
                }
                else Status="Archive verification pending: "+request.downloadHandler.text;
            }
        }
    }
}
