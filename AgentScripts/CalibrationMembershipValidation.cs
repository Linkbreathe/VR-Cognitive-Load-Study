using System;
using System.Collections.Generic;
using System.Linq;
using EvidenceVR;
using UnityEngine;
using UnityEngine.Video;
public static class CalibrationMembershipValidation
{
    static void Require(bool condition,string detail){if(!condition)throw new Exception(detail);}
    public static string Run()
    {
        CheckInstructionPanel();
        var clip=Resources.Load<VideoClip>("CalibrationForest");
        Require(clip!=null && clip.audioTrackCount==1 && Math.Abs(clip.length-180)<.1,"Forest import");
        int checkedRows=0;
        for(int seed=1;seed<=20;seed++)
        {
            var config=AdaptiveTrainingConfig.Personalized();
            config.operatorManaged=true;config.testSessionMode="calibration";config.seed=seed;
            var engine=new TaskEngine(new TaskConfig(),config);
            var rows=new List<TaskEvent>();engine.OnEvent+=rows.Add;
            engine.Advance(0,0);engine.Advance(1,1);engine.Tick(181,2);engine.Advance(182,3);
            int presentCount=0;
            int decoysInGroup=0;
            for(int i=0;i<18;i++)
            {
                double now=182+10*i;
                engine.Tick(now,10+i);
                var q=rows.Last(e=>e.type=="calibration_math_question").detail.Split(';')
                    .Select(pair=>pair.Split('=')).ToDictionary(pair=>pair[0],pair=>pair[1]);
                var nums=q["expression"].Split(' ');
                int a=int.Parse(nums[0]), b=int.Parse(nums[2]);
                int answer=a*b-int.Parse(nums[4])+int.Parse(nums[6]);
                var values=q["options"].Split(',').Select(int.Parse).ToArray();
                bool present=values.Contains(answer);
                Require(a<=25 && b<=25 && values.Distinct().Count()==4 && answer==int.Parse(q["answer"]) && present==bool.Parse(q["present"]),"Invalid prompt");
                int decoys=values.Count(v=>v!=answer && v%10==answer%10);
                Require(decoys==(bool.Parse(q["sameDigitDecoy"])?1:0),"Incorrect same-digit decoy count");
                decoysInGroup+=decoys;
                if((i+1)%6==0)
                {
                    Require(decoysInGroup==1,"Expected one same-digit decoy per six questions");
                    decoysInGroup=0;
                }
                Require(engine.MathAnswerCorrect==null,"Feedback carried into a new prompt");
                if(present)presentCount++;
                if(i%3!=0 && i!=17)
                {
                    bool choice=i%3==1?present:!present;
                    Require(engine.Respond(choice,now+2,30+i),"Answer rejected");
                    Require(engine.MathAnswerCorrect==(choice==present),"Incorrect feedback state");
                    Require(!engine.Respond(!choice,now+3,60+i),"Duplicate accepted");
                }
            }
            engine.Tick(362,80);
            var results=rows.Where(e=>e.type=="calibration_math_result").ToArray();
            Require(results.Length==18 && presentCount==9,"Unbalanced or missing results");
            for(int i=0;i<18;i++)
                Require(results[i].detail.Contains(new[]{"response=omitted;correct=false;rt_seconds=","correct=true;rt_seconds=2","correct=false;rt_seconds=2"}[i==17?0:i%3]),"Incorrect outcome");
            Require(engine.MathTimeoutUntil>362 && engine.TimedOutExpression==engine.MathPrompt.Split('\n')[0],"Final omission feedback missing");
            checkedRows+=results.Length;
            var retry=new TaskEngine(new TaskConfig(),config);
            rows.Clear();
            retry.OnEvent+=rows.Add;
            retry.Advance(0,0);retry.Advance(1,1);retry.Tick(181,2);retry.Advance(182,3);
            string firstPrompt=retry.MathPrompt;
            retry.Pause(184,6);retry.Resume(190,7);retry.Advance(191,8);
            Require(retry.MathPrompt==firstPrompt,"Restart changed the seeded prompt");
            retry.Abort(192,9,"engineering_check");
            var interrupted=rows.Where(e=>e.type=="calibration_math_result").ToArray();
            Require(interrupted.Length==2 && interrupted[0].detail.Contains("attempt=1;slot=1;response=interrupted")
                && interrupted[1].detail.Contains("attempt=2;slot=1;response=interrupted"),"Interrupted attempts");
        }
        return checkedRows+" prompts and results passed; Unity forest import "+clip.frameCount+" frames, "+clip.audioTrackCount+" audio track.";
    }
    static void CheckInstructionPanel()
    {
        var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/TaskPlatform/Scenes/NBackExperiment.unity");
        try
        {
            var owner=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<TaskPlatformController>(true)).Single();
            var cfg=AdaptiveTrainingConfig.Personalized();
            cfg.operatorManaged=true;cfg.testSessionMode="calibration";
            var engine=new TaskEngine(new TaskConfig(),cfg);
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            typeof(TaskPlatformController).GetField("config",flags).SetValue(owner,engine.Config);
            typeof(TaskPlatformController).GetProperty("Engine").GetSetMethod(true).Invoke(owner,new object[]{engine});
            engine.Advance(0,0);engine.Advance(1,1);engine.Tick(181,2);
            owner.presentation.Refresh();
            Require(owner.heading.text=="Calculate" && owner.body.text.Contains("238") && owner.footer.text.Contains("Result is absent"),"Calculate panel content");
            Require(!owner.presentation.exampleCards.activeSelf && !owner.presentation.matchKey.transform.parent.parent.gameObject.activeSelf
                && !owner.presentation.nonmatchKey.transform.parent.parent.gameObject.activeSelf,"2-back cards leaked into arithmetic");
            owner.body.ForceMeshUpdate(true);
            Require(!owner.body.isTextOverflowing,"Calculate example overflow");
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }
}
