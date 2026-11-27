using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

namespace MotionMatching.Runtime
{
    /// <summary>Reads authored root velocity before pose crossfades can introduce root-position offsets.</summary>
    public struct MotionRootSample
    {
        public Vector3 Velocity, AngularVelocity;
        public float DeltaSeconds;
    }

    public struct MotionRootStream : IAnimationJob
    {
        public NativeArray<MotionRootSample> Samples;
        public int Slot;
        public void ProcessAnimation(AnimationStream stream) { }
        public void ProcessRootMotion(AnimationStream stream)
        {
            Samples[Slot] = new MotionRootSample { Velocity = stream.velocity, AngularVelocity = stream.angularVelocity, DeltaSeconds = stream.deltaTime };
        }
    }
}
