using System;
using MotionMatching.Core;
using UnityEngine;

namespace MotionMatching.Runtime
{
    /// <summary>Unity-facing adapter for the pure Core predictor. Reads scene state; never moves or rotates the character.</summary>
    public static class MotionTrajectoryQuery
    {
        public static float[] Predict(Transform character, Transform cameraTransform, Vector2 cameraRelativeInput,
            Vector3 collisionResolvedVelocity, Vector3 desiredFacingDirection, float maxSpeed,
            float acceleration, float turnRateRadiansPerSecond, float[] horizonsSeconds)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            Vector3 forward = cameraTransform != null ? cameraTransform.forward : character.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : character.right;
            forward.y = 0; right.y = 0;
            if (forward.sqrMagnitude < 1e-8f) forward = character.forward;
            if (right.sqrMagnitude < 1e-8f) right = character.right;
            forward.Normalize(); right.Normalize();
            desiredFacingDirection.y = 0;
            if (desiredFacingDirection.sqrMagnitude < 1e-8f) desiredFacingDirection = character.forward;
            desiredFacingDirection.Normalize();
            float facingYaw = Mathf.Atan2(character.forward.x, character.forward.z);
            float desiredYaw = Mathf.Atan2(desiredFacingDirection.x, desiredFacingDirection.z);
            var state = new MotionTrajectoryState
            {
                Position = ToMotion(character.position),
                CollisionResolvedVelocity = ToMotion(collisionResolvedVelocity),
                FacingYaw = facingYaw
            };
            return MotionTrajectoryPredictor.Predict(state, ToMotion(forward), ToMotion(right),
                cameraRelativeInput.x, cameraRelativeInput.y, desiredYaw, maxSpeed,
                acceleration, turnRateRadiansPerSecond, horizonsSeconds);
        }

        private static MotionVector ToMotion(Vector3 value) => new MotionVector(value.x, value.y, value.z);

        public static void PredictIntent(Transform character, Vector3 actualVelocity, Vector3 desiredVelocity, float desiredYawRadians,
            float acceleration, float turnRateRadiansPerSecond, float[] horizons, float[] output)
        {
            desiredVelocity.y = 0;
            float speed = desiredVelocity.magnitude;
            Vector3 direction = speed > 1e-6f ? desiredVelocity / speed : character.forward;
            var state = new MotionTrajectoryState { Position = ToMotion(character.position), CollisionResolvedVelocity = ToMotion(actualVelocity), FacingYaw = Mathf.Atan2(character.forward.x, character.forward.z) };
            MotionTrajectoryPredictor.Predict(state, ToMotion(direction), new MotionVector(), 0, 1, desiredYawRadians, speed, acceleration, turnRateRadiansPerSecond, horizons, output);
        }
    }
}
