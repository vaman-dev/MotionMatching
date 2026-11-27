using System;
using MotionMatching.Core;
using Unity.Profiling;
using UnityEngine;

namespace MotionMatching.Runtime
{
    /// <summary>
    /// Sole capsule displacement owner. Normal-mode animation submits world-space
    /// horizontal metres before the gameplay controller calls Step in LateUpdate.
    /// Gameplay supplies gravity and jump carry even when animation submits nothing.
    /// </summary>
    public sealed class CharacterMotor
    {
        private static readonly ProfilerMarker MotorStepMarker = new ProfilerMarker("MotionMatching.MotorStep");
        private readonly CharacterController _controller;
        private readonly RootMotionAccumulator _rootMotion = new RootMotionAccumulator();
        private int _lastStepFrame = -1;

        public Vector3 ActualVelocity { get; private set; }
        public CollisionFlags CollisionFlags { get; private set; }

        public CharacterMotor(CharacterController controller)
        {
            _controller = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
        }

        public void SubmitRootMotion(Vector3 worldDeltaMeters)
        {
            // A late/manual evaluation cannot replay displacement after this frame's step.
            if (_lastStepFrame == Time.frameCount) return;
            _rootMotion.Submit(Time.frameCount, worldDeltaMeters.x, worldDeltaMeters.z);
        }

        public void ClearPendingRootMotion()
        {
            _rootMotion.Clear();
            ActualVelocity = Vector3.zero;
            CollisionFlags = CollisionFlags.None;
        }

        public void Step(Vector3 gameplayDeltaMeters, float deltaSeconds)
        {
            using var motorScope = MotorStepMarker.Auto();
            if (_lastStepFrame == Time.frameCount) return;
            _lastStepFrame = Time.frameCount;
            _rootMotion.Consume(Time.frameCount, out float x, out float z);
            ActualVelocity = Vector3.zero;
            CollisionFlags = CollisionFlags.None;
            if (!_controller.enabled || !_controller.gameObject.activeInHierarchy) return;

            Vector3 before = _controller.transform.position;
            CollisionFlags = _controller.Move(new Vector3(x, 0f, z) + gameplayDeltaMeters);
            if (deltaSeconds > 0f)
                ActualVelocity = (_controller.transform.position - before) / deltaSeconds;
        }
    }
}
