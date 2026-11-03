using System;
using System.IO;
using EvidenceVR;
using UnityEngine;

public static class FixedSphereGapValidation
{
    public static object Run()
    {
        var root=new GameObject("Isolated sphere gap fixture");
        int checks=0;
        try
        {
            var view=root.AddComponent<SpatialTaskView>();view.bench=root.transform;
            var tray=GameObject.CreatePrimitive(PrimitiveType.Cylinder);tray.name="StimulusPlinth";tray.transform.SetParent(root.transform);
            tray.transform.localScale=new Vector3(.3f,.025f,.3f);
            var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphere.transform.SetParent(root.transform);
            var renderer=sphere.GetComponent<Renderer>();
            foreach(float height in new[]{.705f,1.01f,1.25f})foreach(float diameter in new[]{.12f,.1573f,.22f})
            {
                tray.transform.position=new Vector3(.2f,height,1.5f);
                var before=tray.transform.position;
                sphere.transform.localScale=Vector3.one*diameter;sphere.transform.position=new Vector3(.2f,2f,1.5f);
                view.PlaceStimulusAboveTray(renderer);
                float gap=renderer.bounds.min.y-tray.GetComponent<Renderer>().bounds.max.y;
                if(Mathf.Abs(gap-.0864f)>.0001f)throw new Exception("Surface gap changed with tray height or sphere size.");checks++;
                if(tray.transform.position!=before)throw new Exception("Tray was moved.");checks++;
                if(Mathf.Abs(sphere.transform.position.x-.2f)>.0001f || Mathf.Abs(sphere.transform.position.z-1.5f)>.0001f)throw new Exception("Horizontal position changed.");checks++;
                var placed=sphere.transform.position;view.PlaceStimulusAboveTray(renderer);
                if(Vector3.Distance(placed,sphere.transform.position)>.00001f)throw new Exception("Repeated placement drifted.");checks++;
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
        var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        if(owner==null || owner.Engine==null)throw new Exception("Start preview for integration readback.");
        var actualTray=owner.spatialView.bench.Find("StimulusPlinth").GetComponent<Renderer>();
        float actualGap=owner.stimulus.bounds.min.y-actualTray.bounds.max.y;
        if(Mathf.Abs(actualGap-.0864f)>.0001f)throw new Exception("Live placement did not apply fixed gap.");
        if(Mathf.Abs(actualTray.transform.position.y-Mathf.Max(.705f,owner.taskAnchor.position.y-.19f))>.0001f)throw new Exception("Existing tray height policy changed.");
        string metadata=File.ReadAllText(Path.Combine(owner.DataPath,"session.json"));
        if(!metadata.Contains("sphere-tray-clearance-v1") || !metadata.Contains("sphereTrayClearanceMeters"))throw new Exception("Placement metadata missing.");
        Debug.Log("Fixed sphere gap: PASS;fixture_checks="+checks+";live_gap_m="+actualGap+";tray_policy_unchanged;metadata_present");
        return new {passed=true,fixtureChecks=checks,actualGap,actualTrayY=actualTray.transform.position.y,sphereY=owner.stimulus.transform.position.y,owner.DataPath};
    }
}
