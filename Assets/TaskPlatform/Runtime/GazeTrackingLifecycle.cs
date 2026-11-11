using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace EvidenceVR
{
    public sealed partial class AdaptiveRuntime
    {
        readonly EyeTrackingContinuity eyeContinuity=new EyeTrackingContinuity();
        readonly List<XRInputSubsystem> gazeSubsystems=new List<XRInputSubsystem>();
        readonly List<XRInputSubsystem> currentGazeSubsystems=new List<XRInputSubsystem>();
        long gazeTrackingEpoch=long.MinValue;
        Matrix4x4 gazeTrackingMatrix;
        bool gazeTrackingFrameKnown;
        int gazeContinuityGeneration;
        UnityEngine.XR.InputDevice gazeHeadDevice,gazeEyeDevice;
        ulong gazeDeviceGeneration;
        bool gazeDevicesKnown;

        void ObserveGazeDevices(UnityEngine.XR.InputDevice headDevice,UnityEngine.XR.InputDevice eyeDevice,double now)
        {
            if(gazeDevicesKnown && (!headDevice.Equals(gazeHeadDevice) || !eyeDevice.Equals(gazeEyeDevice)))
            {gazeDeviceGeneration++;InvalidateGazeFrame("tracking_device_changed",now);}
            gazeHeadDevice=headDevice;gazeEyeDevice=eyeDevice;gazeDevicesKnown=true;
        }

        public static bool IsHeadTracked(UnityEngine.XR.InputDevice device)
            =>device.isValid && device.TryGetFeatureValue(CommonUsages.isTracked,out bool tracked) && tracked &&
              device.TryGetFeatureValue(CommonUsages.trackingState,out InputTrackingState state) &&
              (state&(InputTrackingState.Position|InputTrackingState.Rotation))==(InputTrackingState.Position|InputTrackingState.Rotation) &&
              (!device.TryGetFeatureValue(CommonUsages.userPresence,out bool present) || present);

        void PrepareGazeFrame(double now)
        {
            currentGazeSubsystems.Clear();SubsystemManager.GetSubsystems(currentGazeSubsystems);
            foreach(var input in gazeSubsystems)
                if(!currentGazeSubsystems.Contains(input))input.trackingOriginUpdated-=OnGazeTrackingOriginUpdated;
            foreach(var input in currentGazeSubsystems)
                if(!gazeSubsystems.Contains(input))input.trackingOriginUpdated+=OnGazeTrackingOriginUpdated;
            gazeSubsystems.Clear();gazeSubsystems.AddRange(currentGazeSubsystems);
            var matrix=owner.head.parent.localToWorldMatrix;
            long epoch=QuestLinkEyeSpaceFeature.TrackingEpoch;
            if(gazeTrackingFrameKnown && (epoch!=gazeTrackingEpoch || matrix!=gazeTrackingMatrix))
                InvalidateGazeFrame("tracking_space_or_session_changed",now);
            gazeTrackingEpoch=epoch;gazeTrackingMatrix=matrix;gazeTrackingFrameKnown=true;
        }
        void OnGazeTrackingOriginUpdated(XRInputSubsystem input)
            =>InvalidateGazeFrame("tracking_origin_updated",Time.realtimeSinceStartupAsDouble);
        void InvalidateGazeFrame(string reason,double now)
        {
            if(disposed)return;
            eyeContinuity.Reset();eyeFreshness.Invalidate();eyeFreeze.Reset();previousGuardGaze=null;
            GazeTrackingStatus="EYE COORDINATES RECOVERING";GazeDebug?.Clear();GazeRay?.Hold();
            engine?.Note("gaze_tracking_status",EyeTrackingContinuity.Revision+";"+reason,now,Time.frameCount);
            Debug.Log("Eye coordinates reset: "+EyeTrackingContinuity.Revision+";"+reason);
        }
        void DisposeGazeTracking()
        {
            foreach(var input in gazeSubsystems)input.trackingOriginUpdated-=OnGazeTrackingOriginUpdated;
            gazeSubsystems.Clear();currentGazeSubsystems.Clear();
        }
    }
}
