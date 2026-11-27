using System;
using MotionMatching.Core;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MotionMatching.Runtime
{
    public static class MotionTransitionMath
    {
        public static Vector3 Log(Quaternion q)
        {
            q = q.normalized;
            if (q.w < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            var v = new Vector3(q.x, q.y, q.z); float length = v.magnitude;
            return length < 1e-7f ? 2 * v : v * (2 * Mathf.Atan2(length, q.w) / length);
        }
        public static Quaternion Exp(Vector3 vector)
        {
            float angle = vector.magnitude;
            if (angle < 1e-7f) return new Quaternion(vector.x * .5f, vector.y * .5f, vector.z * .5f, 1).normalized;
            float s = Mathf.Sin(angle * .5f) / angle;
            return new Quaternion(vector.x * s, vector.y * s, vector.z * s, Mathf.Cos(angle * .5f));
        }
        // Map spatial offset angular velocity to the derivative of its log vector.
        public static Vector3 LogDerivative(Vector3 log, Vector3 spatialAngularVelocity)
        {
            float theta = log.magnitude;
            float coefficient = theta < .001f ? 1f / 12 : (1 - .5f * theta / Mathf.Tan(.5f * theta)) / (theta * theta);
            Vector3 cross = Vector3.Cross(log, spatialAngularVelocity);
            return spatialAngularVelocity - .5f * cross + coefficient * Vector3.Cross(log, cross);
        }
        public static void Decay(ref Vector3 offset, ref Vector3 velocity, float halfLife, float dt)
        {
            MotionSpring.Step(ref offset.x, ref velocity.x, 0, halfLife, dt);
            MotionSpring.Step(ref offset.y, ref velocity.y, 0, halfLife, dt);
            MotionSpring.Step(ref offset.z, ref velocity.z, 0, halfLife, dt);
        }
    }

    public struct MotionInertialStatistics
    {
        public int AppliedGeneration, Evaluations, ReferenceEvaluations;
        public float MaxPositionOffset, MaxRotationOffsetDegrees, MaxPoseStepDegrees;
        public float LastDeltaSeconds, MaxOutputRotationErrorDegrees;
    }

    public struct MotionInertialBone
    {
        public Vector3 DisplayedPosition, DisplayedVelocity, DisplayedAngularVelocity;
        public Quaternion DisplayedRotation;
        public Vector3 PositionOffset, PositionOffsetVelocity, RotationOffset, RotationOffsetVelocity;
        public Vector3 RawPosition, RawScale, IncomingVelocity, IncomingAngular;
        public Quaternion RawRotation;
        public Quaternion RequestedRotation;
        public int Initialized;
    }

    /// <summary>One downstream pose writer inside the animation stream. Root reconciliation remains separate.</summary>
    public struct MotionInertialJob : IAnimationJob
    {
        public NativeArray<TransformStreamHandle> Handles;
        public NativeArray<MotionInertialBone> Bones;
        public NativeArray<MotionInertialStatistics> Statistics;
        public int Generation;
        public bool Enabled, HasReference;
        public float ReferenceDelta, PositionHalfLife, RotationHalfLife;

        public void ProcessRootMotion(AnimationStream stream)
        {
            var source = stream.GetInputStream(0);
            stream.velocity = source.velocity; stream.angularVelocity = source.angularVelocity;
        }
        public void ProcessAnimation(AnimationStream stream)
        {
            var source = stream.GetInputStream(0);
            AnimationStream reference = default;
            bool referenceValid = HasReference && stream.inputStreamCount > 1;
            if (referenceValid) { reference = stream.GetInputStream(1); referenceValid = reference.isValid; }
            float dt = stream.deltaTime;
            var stats = Statistics[0];
            stats.LastDeltaSeconds = dt;
            bool transition = Enabled && referenceValid && stats.AppliedGeneration != Generation;
            stats.MaxPositionOffset = stats.MaxRotationOffsetDegrees = stats.MaxPoseStepDegrees = 0;
            // Humanoid stream transform reads may synchronize the skeleton. Finish all
            // source/reference reads before modifying any output transform channels.
            for (int i = 0; i < Handles.Length; i++)
            {
                var handle = Handles[i];
                if (!handle.IsValid(source) || !handle.IsValid(stream)) continue;
                var bone = Bones[i];
                handle.GetLocalTRS(source, out bone.RawPosition, out bone.RawRotation, out bone.RawScale);
                if (transition)
                {
                    bone.IncomingVelocity = (bone.RawPosition - handle.GetLocalPosition(reference)) / -ReferenceDelta;
                    bone.IncomingAngular = MotionTransitionMath.Log(bone.RawRotation * Quaternion.Inverse(handle.GetLocalRotation(reference))) / -ReferenceDelta;
                }
                Bones[i] = bone;
            }
            for (int i = 0; i < Handles.Length; i++)
            {
                var handle = Handles[i];
                if (!handle.IsValid(stream)) continue;
                var bone = Bones[i];
                Vector3 rawPosition = bone.RawPosition;
                Quaternion rawRotation = bone.RawRotation;
                bool initialize = transition && bone.Initialized != 0 && dt > 0;
                if (!Enabled)
                {
                    bone.PositionOffset = bone.PositionOffsetVelocity = bone.RotationOffset = bone.RotationOffsetVelocity = Vector3.zero;
                }
                else if (initialize)
                {
                    // Use the last actually displayed result, including any previous inertial offsets.
                    Vector3 incomingVelocity = bone.IncomingVelocity;
                    Vector3 incomingAngular = bone.IncomingAngular;
                    Vector3 expectedPosition = bone.DisplayedPosition + bone.DisplayedVelocity * dt;
                    Quaternion expectedRotation = MotionTransitionMath.Exp(bone.DisplayedAngularVelocity * dt) * bone.DisplayedRotation;
                    bone.PositionOffset = expectedPosition - rawPosition;
                    bone.PositionOffsetVelocity = bone.DisplayedVelocity - incomingVelocity;
                    Quaternion rotationOffset = expectedRotation * Quaternion.Inverse(rawRotation);
                    bone.RotationOffset = MotionTransitionMath.Log(rotationOffset);
                    Vector3 offsetAngular = bone.DisplayedAngularVelocity - rotationOffset * incomingAngular;
                    bone.RotationOffsetVelocity = MotionTransitionMath.LogDerivative(bone.RotationOffset, offsetAngular);
                }
                else if (dt > 0)
                {
                    MotionTransitionMath.Decay(ref bone.PositionOffset, ref bone.PositionOffsetVelocity, PositionHalfLife, dt);
                    MotionTransitionMath.Decay(ref bone.RotationOffset, ref bone.RotationOffsetVelocity, RotationHalfLife, dt);
                }
                Vector3 displayedPosition = rawPosition + bone.PositionOffset;
                Quaternion displayedRotation = MotionTransitionMath.Exp(bone.RotationOffset) * rawRotation;
                handle.SetLocalTRS(stream, displayedPosition, displayedRotation, bone.RawScale, false);
                bone.RequestedRotation = displayedRotation;
                stats.MaxPositionOffset = Mathf.Max(stats.MaxPositionOffset, bone.PositionOffset.magnitude);
                stats.MaxRotationOffsetDegrees = Mathf.Max(stats.MaxRotationOffsetDegrees, bone.RotationOffset.magnitude * Mathf.Rad2Deg);
                Bones[i] = bone;
            }
            if (transition && dt > 0) { stats.AppliedGeneration = Generation; stats.ReferenceEvaluations++; }
            stats.Evaluations++; Statistics[0] = stats;
        }
    }

    public sealed class MotionInertializer : IDisposable
    {
        private readonly PlayableGraph graph;
        private NativeArray<TransformStreamHandle> handles;
        private NativeArray<MotionInertialBone> bones;
        private NativeArray<MotionInertialStatistics> statistics;
        private Transform[] displayedTransforms;
        private AnimationClipPlayable reference;
        private int generation;
        public AnimationScriptPlayable Playable { get; private set; }
        public MotionInertialStatistics Statistics => statistics.IsCreated ? statistics[0] : default;
        public bool Enabled { get; private set; }

        public MotionInertializer(PlayableGraph graph, Animator animator, Playable source)
        {
            this.graph = graph;
            try
            {
                var transforms = animator.GetComponentsInChildren<Transform>(true);
                int count = 0; foreach (var t in transforms) if (t != animator.transform) count++;
                handles = new NativeArray<TransformStreamHandle>(count, Allocator.Persistent);
                displayedTransforms = new Transform[count];
                bones = new NativeArray<MotionInertialBone>(count, Allocator.Persistent);
                statistics = new NativeArray<MotionInertialStatistics>(1, Allocator.Persistent);
                int index = 0;
                foreach (var t in transforms) if (t != animator.transform)
                {
                    displayedTransforms[index] = t;
                    handles[index++] = animator.BindStreamTransform(t);
                }
                Playable = AnimationScriptPlayable.Create(graph, new MotionInertialJob { Handles = handles, Bones = bones, Statistics = statistics }, 2);
                Playable.SetTraversalMode(PlayableTraversalMode.Mix);
                graph.Connect(source, 0, Playable, 0); Playable.SetInputWeight(0, 1);
            }
            catch { Dispose(); throw; }
        }
        public void Configure(bool enabled, float positionHalfLife, float rotationHalfLife)
        {
            Enabled = enabled;
            var job = Playable.GetJobData<MotionInertialJob>();
            job.Enabled = enabled; job.PositionHalfLife = positionHalfLife; job.RotationHalfLife = rotationHalfLife;
            Playable.SetJobData(job);
            // Destroy the temporary velocity reference after its single transition evaluation.
            if (reference.IsValid() && (!enabled || Statistics.AppliedGeneration == generation)) ClearReference();
        }
        /// <summary>Read final Humanoid output after animation; never writes scene transforms.</summary>
        public void CaptureDisplayedPose(float deltaSeconds)
        {
            if (!bones.IsCreated) return;
            float maxStep = 0;
            float maxOutputError = 0;
            for (int i = 0; i < displayedTransforms.Length; i++)
            {
                var t = displayedTransforms[i];
                if (t == null) continue;
                var bone = bones[i];
                Vector3 position = t.localPosition;
                Quaternion rotation = t.localRotation.normalized;
                if (Statistics.Evaluations > 0)
                    maxOutputError = Mathf.Max(maxOutputError, MotionTransitionMath.Log(rotation * Quaternion.Inverse(bone.RequestedRotation)).magnitude * Mathf.Rad2Deg);
                if (bone.Initialized != 0 && deltaSeconds > 0)
                {
                    bone.DisplayedVelocity = (position - bone.DisplayedPosition) / deltaSeconds;
                    Vector3 step = MotionTransitionMath.Log(rotation * Quaternion.Inverse(bone.DisplayedRotation));
                    bone.DisplayedAngularVelocity = step / deltaSeconds;
                    maxStep = Mathf.Max(maxStep, step.magnitude * Mathf.Rad2Deg);
                }
                bone.DisplayedPosition = position; bone.DisplayedRotation = rotation; bone.Initialized = 1;
                bones[i] = bone;
            }
            var stats = statistics[0]; stats.MaxPoseStepDegrees = maxStep; stats.MaxOutputRotationErrorDegrees = maxOutputError; statistics[0] = stats;
        }
        public void Begin(AnimationClip clip, float time)
        {
            if (!Enabled) return;
            ClearReference();
            float step = Mathf.Min(1f / 60f, clip.length * .25f);
            float start = clip.isLooping ? time % clip.length : Mathf.Min(time, clip.length);
            float delta = start >= step ? -step : step;
            reference = AnimationClipPlayable.Create(graph, clip);
            reference.SetApplyFootIK(false); reference.SetApplyPlayableIK(false); reference.SetPropagateSetTime(false);
            reference.SetTime(start + delta); reference.SetTime(start + delta);
            graph.Connect(reference, 0, Playable, 1); Playable.SetInputWeight(1, 0);
            var job = Playable.GetJobData<MotionInertialJob>(); job.Generation = ++generation; job.ReferenceDelta = delta; job.HasReference = true;
            Playable.SetJobData(job);
        }
        private void ClearReference()
        {
            if (reference.IsValid()) { graph.Disconnect(Playable, 1); graph.DestroyPlayable(reference); reference = default; }
            if (Playable.IsValid()) { var job = Playable.GetJobData<MotionInertialJob>(); job.HasReference = false; Playable.SetJobData(job); }
        }
        public void Dispose()
        {
            // Also safe for a partially constructed processor before graph ownership transfers.
            if (graph.IsValid())
            {
                if (Playable.IsValid()) graph.DestroyPlayable(Playable);
                if (reference.IsValid()) graph.DestroyPlayable(reference);
            }
            Playable = default; reference = default;
            if (handles.IsCreated) handles.Dispose();
            if (bones.IsCreated) bones.Dispose();
            if (statistics.IsCreated) statistics.Dispose();
        }
    }
}
