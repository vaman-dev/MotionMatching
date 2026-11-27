using MotionMatching.Core;
using UnityEngine;

namespace MotionMatching.Runtime
{
    [CreateAssetMenu(menuName = "Motion Matching/Playback Settings")]
    public sealed class MotionMatchingSettings : ScriptableObject
    {
        [Min(0)] public float WalkMetersPerSecond = 1.6f;
        [Min(0)] public float RunMetersPerSecond = 5.5f;
        [Tooltip("When enabled, full non-sprint input requests running; partial analog input provides walking.")]
        public bool UseThreeGaits;
        [Min(0)] public float SprintMetersPerSecond = 5.5f;
        [Min(0)] public float JogMetersPerSecond = 3.3f;
        [Min(0)] public float AccelerationMetersPerSecondSquared = 12f;
        [Tooltip("Maximum grounded input/trajectory turn rate when input owns yaw. Calibrate against the active clips' measured root yaw; the current Turn01 clips are about 90 degrees per second.")]
        [Min(0)] public float TurnRadiansPerSecond = 1.57f;
        [Min(.01f)] public float SearchIntervalSeconds = .1f;
        [Min(0)] public float BlendSeconds = .15f;
        [Range(0, 1)] public float RequiredCostImprovement = .15f;
        [Min(0)] public float ContinuationWindowSeconds = .2f;
        [Header("Continuation and interruption")]
        [Min(0)] public float SwitchPenalty = .5f;
        [Min(0)] public float AbsoluteMinimumImprovement = .05f;
        [Min(0)] public float MinimumSwitchIntervalSeconds = .15f;
        public bool AllowTransitionInterruption = true;
        [Range(0,180)] public float IntentInterruptionAngleDegrees = 60;
        [Min(0)] public float IntentInterruptionSpeedChange = 1.5f;
        [Header("Intent springs (seconds)")]
        [Min(0)] public float VelocityHalfLife = .06f;
        [Min(0)] public float FacingHalfLife = .06f;
        [Header("Pose transitions")]
        [Tooltip("Experimental: keep disabled for validated crossfade playback. Humanoid interrupted-pose continuity currently has a failing 60 Hz test; see the M3C hardening report.")]
        public bool InertializationEnabled;
        [Min(0)] public float PoseOffsetHalfLife = .08f;
        [Min(0)] public float RotationOffsetHalfLife = .08f;
        public bool FaceCamera;
        [Tooltip("Experimental: authored clip yaw owns grounded facing. Off preserves existing input-driven yaw.")]
        public bool ApplyRootYaw;
        public bool CompareBruteForce;
        public bool DrawDiagnostics = true;
        [Tooltip("Trajectory position/facing, feet position/velocity, pelvis velocity. Calibrated for the baseline clips; revalidate after changing the dataset.")]
        public MotionFeatureWeights Weights = new MotionFeatureWeights { Groups = new[] { 10f, 2f, 1f, .25f, .25f } };
    }
}
