using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvidenceVR
{
    // Observes the existing actions. It never supplies or changes a scored response.
    public sealed class InputTimingAudit : IDisposable
    {
        public const string Revision="input-action-edges-v1";
        [Serializable] public sealed class Edge
        {
            public long id;
            public string action,control,edge,stage,condition,phase;
            public int frame,block,trial,phaseTrial,trainingOrdinal;
            public double inputTime,callbackTime,onsetTime=-1,rtFromOnset=-1,heldSeconds=-1;
        }
        readonly TaskEngine engine;
        readonly InputAction[] actions;
        readonly Dictionary<string,double> down=new Dictionary<string,double>();
        readonly Dictionary<string,Edge> latest=new Dictionary<string,Edge>();
        long sequence;
        public InputTimingAudit(TaskEngine engine,params InputAction[] actions)
        {
            this.engine=engine;this.actions=actions;
            foreach(var a in actions){a.performed+=Performed;a.canceled+=Canceled;}
        }
        void Performed(InputAction.CallbackContext context)=>Record(context,"performed_press");
        void Canceled(InputAction.CallbackContext context)=>Record(context,"canceled_release");
        void Record(InputAction.CallbackContext context,string edge)
        {
            var t=engine.CurrentResult;string key=context.action.name+":"+context.control.path;
            var row=new Edge{id=++sequence,action=context.action.name,control=context.control.path,edge=edge,
                inputTime=context.time,callbackTime=Time.realtimeSinceStartupAsDouble,frame=Time.frameCount,
                block=engine.Block,trial=engine.TrialIndex,phaseTrial=t?.phaseTrial??-1,trainingOrdinal=engine.TrainingOrdinal,
                stage=engine.Adaptive?.Stage??"fixed",condition=LayerABStudy.Condition(engine.Adaptive?.Stage),phase=engine.Phase.ToString(),
                onsetTime=t?.actualOnset??-1};
            if(t!=null)row.rtFromOnset=context.time-t.actualOnset;
            if(edge=="performed_press")
            {
                down[key]=context.time;
                // Preserve the first press in this input update for the existing frame-polled response.
                if(!latest.TryGetValue(context.action.name,out var previous) || previous.frame!=row.frame)latest[context.action.name]=row;
            }
            else if(down.TryGetValue(key,out double start)){row.heldSeconds=context.time-start;down.Remove(key);}
            engine.Note("input_edge",JsonUtility.ToJson(row),context.time,row.frame);
        }
        public void LinkAcceptedResponse(InputAction action,int frame)
        {
            var t=engine.CurrentResult;
            if(t!=null && latest.TryGetValue(action.name,out var edge) && edge.frame==frame)
            {t.inputEventId=edge.id;t.inputEventTime=edge.inputTime;t.inputReactionTime=edge.inputTime-t.actualOnset;}
        }
        public void Dispose(){foreach(var a in actions){a.performed-=Performed;a.canceled-=Canceled;}}
    }
}
