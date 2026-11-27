using System;
using System.Collections.Generic;
using MotionMatching.Core;
using UnityEngine;

namespace MotionMatching.Editor
{
    [Serializable] public sealed class MotionHealthThresholds
    {
        public float RootSpeed = 12, AngularSpeed = 12, JointSpeed = 20, SeamAngleDegrees = 45;
    }
    [Serializable] public sealed class MotionClipHealth
    {
        public string Name, DeclaredTags, MeasuredMotion;
        public int Samples, Searchable, Excluded, AbnormalFrames;
        public float MinRootSpeed, MaxRootSpeed, MinAngularSpeed, MaxAngularSpeed, MaxJointSpeed;
        public float NetX, NetZ, HeadingChangeDegrees, SeamRootStep, SeamPoseAngleDegrees;
        public float LeftContactFraction, RightContactFraction;
        public int LeftContactChanges, RightContactChanges;
        public string[] Warnings;
    }
    [Serializable] public sealed class MotionHealthReport
    {
        public int Samples, Searchable, Excluded, ConstantDimensions, InvalidFeatureValues;
        public MotionClipHealth[] Clips = Array.Empty<MotionClipHealth>();
        public string[] Errors = Array.Empty<string>();
        public string[] Warnings = Array.Empty<string>();
    }

    /// <summary>Read-only diagnostics: warning thresholds never discard a clip or change its features.</summary>
    public static class MotionDatabaseHealth
    {
        public static MotionHealthReport Analyze(MotionDatabaseData data, MotionHealthThresholds thresholds = null)
        {
            var report = new MotionHealthReport();
            if (data?.RawFeatures != null) foreach (float value in data.RawFeatures) if (!float.IsFinite(value)) report.InvalidFeatureValues++;
            if (data?.NormalizedFeatures != null) foreach (float value in data.NormalizedFeatures) if (!float.IsFinite(value)) report.InvalidFeatureValues++;
            try { MotionDatabaseBinary.ValidateData(data); }
            catch (Exception e) { report.Errors = new[] { e.Message }; return report; }
            thresholds = thresholds ?? new MotionHealthThresholds();
            if (!float.IsFinite(thresholds.RootSpeed) || thresholds.RootSpeed <= 0 || !float.IsFinite(thresholds.AngularSpeed) || thresholds.AngularSpeed <= 0 ||
                !float.IsFinite(thresholds.JointSpeed) || thresholds.JointSpeed <= 0 || !float.IsFinite(thresholds.SeamAngleDegrees) || thresholds.SeamAngleDegrees <= 0)
            { report.Errors = new[] { "Health warning thresholds must be finite and positive." }; return report; }
            report.Samples = data.FrameCount;
            var errors = new List<string>(); var warnings = new List<string>();
            report.Clips = new MotionClipHealth[data.Clips.Length];
            for (int c = 0; c < data.Clips.Length; c++)
            {
                var clip = data.Clips[c]; var notes = new List<string>();
                var h = new MotionClipHealth { Name = clip.Name, DeclaredTags = clip.Tag, Samples = clip.Samples.Length, MinRootSpeed = float.PositiveInfinity, MinAngularSpeed = float.PositiveInfinity };
                float meanX = 0, meanZ = 0;
                int contactsL = 0, contactsR = 0;
                for (int i = 0; i < clip.Samples.Length; i++)
                {
                    int frame = clip.FrameOffset + i;
                    if (data.ValidFrames[frame]) h.Searchable++;
                    bool eligible = clip.IsLoop || i / data.Settings.SampleRate + data.Settings.FutureSeconds[2] <= (clip.Samples.Length - 1) / data.Settings.SampleRate + 0.000001f;
                    if (eligible != data.ValidFrames[frame]) errors.Add($"{clip.Name} row {i}: eligibility differs from saved horizons.");
                    float speed = Packed(data.RootLinearVelocities, frame * 3).magnitude;
                    float angular = Packed(data.RootAngularVelocities, frame * 3).magnitude;
                    float jointSpeed = 0;
                    for (int j = 0; j < 3; j++) jointSpeed = Mathf.Max(jointSpeed, Packed(data.JointVelocities, frame * 9 + j * 3).magnitude);
                    h.MinRootSpeed = Mathf.Min(h.MinRootSpeed, speed); h.MaxRootSpeed = Mathf.Max(h.MaxRootSpeed, speed);
                    h.MaxAngularSpeed = Mathf.Max(h.MaxAngularSpeed, angular); h.MaxJointSpeed = Mathf.Max(h.MaxJointSpeed, jointSpeed);
                    h.MinAngularSpeed = Mathf.Min(h.MinAngularSpeed, angular);
                    if (speed > thresholds.RootSpeed || angular > thresholds.AngularSpeed || jointSpeed > thresholds.JointSpeed) h.AbnormalFrames++;
                    meanX += data.RootLinearVelocities[frame * 3]; meanZ += data.RootLinearVelocities[frame * 3 + 2];
                    if (i > 0) h.HeadingChangeDegrees += MotionMath.AngleDelta(MotionMath.Yaw(clip.Samples[i - 1].RootRotation), MotionMath.Yaw(clip.Samples[i].RootRotation)) * Mathf.Rad2Deg;
                    bool left = data.LeftFootContacts[frame], right = data.RightFootContacts[frame];
                    if (left) contactsL++; if (right) contactsR++;
                    if (i > 0)
                    {
                        if (left != data.LeftFootContacts[frame - 1]) h.LeftContactChanges++;
                        if (right != data.RightFootContacts[frame - 1]) h.RightContactChanges++;
                    }
                }
                h.Excluded = h.Samples - h.Searchable; report.Searchable += h.Searchable;
                h.LeftContactFraction = contactsL / (float)h.Samples; h.RightContactFraction = contactsR / (float)h.Samples;
                h.NetX = clip.EndRootPosition.x - clip.Samples[0].RootPosition.x;
                h.NetZ = clip.EndRootPosition.z - clip.Samples[0].RootPosition.z;
                h.MeasuredMotion = h.MaxRootSpeed < 0.1f ? "Root stationary / in-place (inspect pose)" :
                    Mathf.Abs(meanX) > Mathf.Abs(meanZ) ? (meanX < 0 ? "Left travel candidate" : "Right travel candidate") :
                    (meanZ < 0 ? "Backward travel candidate" : "Forward travel candidate");
                if (Mathf.Abs(h.HeadingChangeDegrees) > 45) h.MeasuredMotion += "; turning candidate";
                if (clip.IsLoop)
                {
                    h.SeamRootStep = Vector3.Distance(MotionPreviewSession.V(clip.Samples[h.Samples - 1].RootPosition), MotionPreviewSession.V(clip.EndRootPosition));
                    for (int b = 0; b < clip.BoneNames.Length; b++)
                    {
                        int a = b * 4, z = ((h.Samples - 1) * clip.BoneNames.Length + b) * 4;
                        h.SeamPoseAngleDegrees = Mathf.Max(h.SeamPoseAngleDegrees, Quaternion.Angle(Q(clip.LocalPoseRotations, a), Q(clip.LocalPoseRotations, z)));
                    }
                    if (h.SeamPoseAngleDegrees > thresholds.SeamAngleDegrees) notes.Add("Large last-to-first mapped pose rotation; includes one normal frame step. Inspect loop seam.");
                }
                if (h.AbnormalFrames > 0) notes.Add($"{h.AbnormalFrames} frames exceed configurable speed warning thresholds.");
                if (h.LeftContactFraction == 0 || h.RightContactFraction == 0) notes.Add("At least one foot has no estimated contacts; heuristic/scale/motion needs inspection.");
                if (h.Excluded > 0) notes.Add($"{h.Excluded} tail samples lack all future horizons and remain excluded.");
                h.Warnings = notes.ToArray(); report.Clips[c] = h;
            }
            report.Excluded = report.Samples - report.Searchable;
            int normalizationMismatches = 0;
            for (int d = 0; d < 27; d++)
            {
                if (data.StandardDeviations[d] <= data.Settings.NormalizationEpsilon) report.ConstantDimensions++;
                double sum = 0, variance = 0;
                for (int i = 0; i < data.FrameCount; i++)
                {
                    int index = i * 27 + d;
                    float expected = (data.RawFeatures[index] - data.Means[d]) / data.NormalizationScales[d];
                    if (Mathf.Abs(expected - data.NormalizedFeatures[index]) > 0.0001f * Mathf.Max(1, Mathf.Abs(expected))) normalizationMismatches++;
                    if (data.ValidFrames[i]) sum += data.RawFeatures[index];
                }
                double mean = sum / report.Searchable;
                for (int i = 0; i < data.FrameCount; i++) if (data.ValidFrames[i])
                { double delta = data.RawFeatures[i * 27 + d] - mean; variance += delta * delta; }
                float std = (float)Math.Sqrt(variance / report.Searchable);
                if (Math.Abs(mean - data.Means[d]) > 0.0001 * Math.Max(1, Math.Abs(mean)) ||
                    Math.Abs(std - data.StandardDeviations[d]) > 0.0001 * Math.Max(1, std) ||
                    Mathf.Abs(data.NormalizationScales[d] - Mathf.Max(std, data.Settings.NormalizationEpsilon)) > 0.0001f * Mathf.Max(1, std))
                    errors.Add($"Feature dimension {d}: saved normalization statistics differ from eligible raw rows.");
            }
            if (normalizationMismatches > 0) errors.Add($"{normalizationMismatches} normalized values disagree with the saved transform.");
            warnings.Add("Measured direction is a candidate, not verified gait/action coverage. Starts/stops/pivots require source and pose review.");
            warnings.Add("Contact flags are heuristic; no-contact uncertainty is a warning, never an automatic rejection.");
            report.Errors = errors.ToArray(); report.Warnings = warnings.ToArray();
            return report;
        }
        private static Vector3 Packed(float[] a, int i) => new Vector3(a[i], a[i + 1], a[i + 2]);
        private static Quaternion Q(float[] a, int i) => new Quaternion(a[i], a[i + 1], a[i + 2], a[i + 3]);
    }
}
