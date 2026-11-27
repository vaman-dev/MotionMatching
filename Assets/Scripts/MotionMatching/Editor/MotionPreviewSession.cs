using System;
using System.Linq;
using MotionMatching.Core;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MotionMatching.Editor
{
    public struct MotionPoseComparison
    {
        public float MaxRootPositionError, MaxRootAngleDegrees, MaxBonePositionError, MaxBoneAngleDegrees;
    }

    /// <summary>Isolated, cached clip evaluation. Scrubbing never samples the gameplay scene or rebakes data.</summary>
    public sealed class MotionPreviewSession : IDisposable
    {
        private MotionAnimationSampler.SamplingRig rig;
        private readonly MotionClipData baked;
        private readonly Transform[] hierarchy;
        private readonly Vector3[] positions;
        private readonly Quaternion[] rotations;
        private readonly Vector3[] restPositions;
        private readonly Quaternion[] restRotations;
        private readonly Vector3[] roots;
        private readonly Quaternion[] rootRotations;
        private readonly Vector3[] endpointPositions;
        private readonly Quaternion[] endpointRotations;
        private readonly Vector3[] comparisonPositions;
        private readonly Quaternion[] comparisonRotations;
        private readonly float sampleRate;
        private readonly double duration;
        private readonly bool loops;
        public Transform Root => rig?.Animator.transform;
        public GameObject Instance => rig?.Instance;
        public Animator Animator => rig?.Animator;
        public Transform[] Bones { get; }
        public int[] BoneParents { get; }
        public int BoneCount => Bones.Length;
        public int CurrentSample { get; private set; }
        public bool IsDisposed => rig == null;

        public Vector3 TransformCyclePoint(Vector3 point, double seconds)
        {
            if (!loops || seconds < duration) return point;
            long cycles = (long)Math.Floor(seconds / duration);
            RigidTransform cycle = Pow(new RigidTransform(V(baked.EndRootPosition), Q(baked.EndRootRotation)), cycles);
            return cycle.Position + cycle.Rotation * point;
        }

        public MotionPreviewSession(GameObject character, Avatar avatar, AnimationClip clip,
            MotionClipData baked, MotionBakeSettings settings)
        {
            MotionAnimationSampler.ValidateInput(character, avatar, clip, settings);
            if (baked == null || baked.Samples == null || baked.Samples.Length < 2 || baked.BoneNames == null ||
                baked.LocalPosePositions == null || baked.LocalPoseRotations == null ||
                baked.LocalPosePositions.Length != baked.Samples.Length * baked.BoneNames.Length * 3 ||
                baked.LocalPoseRotations.Length != baked.Samples.Length * baked.BoneNames.Length * 4)
                throw new ArgumentException("Preview requires complete baked samples and mapped local poses.");
            if (baked.Guid != MotionAnimationSampler.ClipIdentity(clip)) throw new ArgumentException("Preview clip identity differs from baked source.");
            int expectedCount = baked.IsLoop ? (int)Math.Round((double)clip.length * settings.SampleRate) : (int)Math.Floor((double)clip.length * settings.SampleRate + 0.00001) + 1;
            if (Math.Abs(baked.Duration - clip.length) > 0.00001 || expectedCount != baked.Samples.Length)
                throw new ArgumentException("Preview timing differs from the baked clip/sample grid; reload or rebake.");
            this.baked = baked;
            sampleRate = settings.SampleRate;
            duration = baked.Duration;
            loops = baked.IsLoop;
            try
            {
                rig = new MotionAnimationSampler.SamplingRig(character, avatar);
                hierarchy = rig.Instance.GetComponentsInChildren<Transform>(true);
                restPositions = hierarchy.Select(t => t.localPosition).ToArray();
                restRotations = hierarchy.Select(t => t.localRotation).ToArray();
                Bones = new Transform[baked.BoneNames.Length];
                BoneParents = new int[Bones.Length];
                for (int i = 0; i < Bones.Length; i++)
                {
                    if (!Enum.TryParse(baked.BoneNames[i], out HumanBodyBones bone) || bone == HumanBodyBones.LastBone ||
                        (Bones[i] = rig.Animator.GetBoneTransform(bone)) == null)
                        throw new ArgumentException("Preview Avatar is missing mapped bone: " + baked.BoneNames[i]);
                }
                for (int i = 0; i < Bones.Length; i++)
                {
                    BoneParents[i] = -1;
                    for (Transform p = Bones[i].parent; p != null; p = p.parent)
                    {
                        int parent = Array.IndexOf(Bones, p);
                        if (parent >= 0) { BoneParents[i] = parent; break; }
                    }
                }
                int count = baked.Samples.Length;
                positions = new Vector3[count * hierarchy.Length]; rotations = new Quaternion[positions.Length];
                roots = new Vector3[count]; rootRotations = new Quaternion[count];
                endpointPositions = new Vector3[hierarchy.Length]; endpointRotations = new Quaternion[hierarchy.Length];
                comparisonPositions = new Vector3[Bones.Length]; comparisonRotations = new Quaternion[Bones.Length];
                var graph = PlayableGraph.Create("Motion Database 3D Preview Sampling");
                double evaluated = 0;
                try
                {
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var playable = AnimationClipPlayable.Create(graph, clip);
                    playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
                    AnimationPlayableOutput.Create(graph, "Preview", rig.Animator).SetSourcePlayable(playable);
                    graph.Play(); graph.Evaluate(0);
                    Vector3 origin = Root.position; Quaternion inverse = Quaternion.Inverse(Root.rotation);
                    for (int frame = 0; frame < count; frame++)
                    {
                        double time = frame / (double)settings.SampleRate;
                        if (frame > 0) graph.Evaluate((float)(time - evaluated));
                        evaluated = time;
                        roots[frame] = inverse * (Root.position - origin);
                        rootRotations[frame] = inverse * Root.rotation;
                        for (int b = 0; b < hierarchy.Length; b++)
                        {
                            int index = frame * hierarchy.Length + b;
                            positions[index] = hierarchy[b].localPosition; rotations[index] = hierarchy[b].localRotation;
                        }
                    }
                    if (clip.length > evaluated) graph.Evaluate((float)(clip.length - evaluated));
                    for (int b = 0; b < hierarchy.Length; b++)
                    {
                        endpointPositions[b] = hierarchy[b].localPosition;
                        endpointRotations[b] = hierarchy[b].localRotation;
                    }
                }
                finally { if (graph.IsValid()) graph.Destroy(); }
                rig.Animator.enabled = false;
                SetSample(0, false);
            }
            catch { Dispose(); throw; }
        }

        public void SetSample(int frame, bool storedPose)
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(MotionPreviewSession));
            if (frame < 0 || frame >= baked.Samples.Length) throw new ArgumentOutOfRangeException(nameof(frame));
            SetTime(frame / (double)sampleRate, storedPose);
        }

        /// <summary>Evaluates one deterministic cached pose at a continuous clip time. Loop root motion accumulates across cycles.</summary>
        public void SetTime(double seconds, bool storedPose)
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(MotionPreviewSession));
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            int count = baked.Samples.Length;
            long cycles = 0;
            double localTime;
            if (loops)
            {
                cycles = (long)Math.Floor(seconds / duration);
                localTime = seconds - cycles * duration;
            }
            else localTime = Math.Min(seconds, (count - 1) / (double)sampleRate);

            double framePosition = localTime * sampleRate;
            int first = Math.Min((int)Math.Floor(framePosition), count - 1);
            int second = first + 1;
            float alpha;
            bool toEndpoint = second >= count;
            if (toEndpoint)
            {
                second = loops ? 0 : count - 1;
                double intervalStart = first / (double)sampleRate;
                alpha = loops ? Mathf.Clamp01((float)((localTime - intervalStart) / Math.Max(1e-9, duration - intervalStart))) : 0;
            }
            else alpha = Mathf.Clamp01((float)(framePosition - first));

            CurrentSample = first;
            for (int b = 0; b < hierarchy.Length; b++)
            {
                int firstIndex = first * hierarchy.Length + b;
                if (storedPose)
                {
                    hierarchy[b].localPosition = restPositions[b];
                    hierarchy[b].localRotation = restRotations[b];
                }
                else
                {
                    Vector3 p1 = toEndpoint ? endpointPositions[b] : positions[second * hierarchy.Length + b];
                    Quaternion r1 = toEndpoint ? endpointRotations[b] : rotations[second * hierarchy.Length + b];
                    hierarchy[b].localPosition = Vector3.LerpUnclamped(positions[firstIndex], p1, alpha);
                    hierarchy[b].localRotation = Quaternion.SlerpUnclamped(rotations[firstIndex], r1, alpha);
                }
            }
            if (storedPose)
            {
                for (int b = 0; b < Bones.Length; b++)
                {
                    int p0 = (first * Bones.Length + b) * 3, q0 = (first * Bones.Length + b) * 4;
                    int p1 = (second * Bones.Length + b) * 3, q1 = (second * Bones.Length + b) * 4;
                    Vector3 a = new Vector3(baked.LocalPosePositions[p0], baked.LocalPosePositions[p0 + 1], baked.LocalPosePositions[p0 + 2]);
                    Vector3 z = new Vector3(baked.LocalPosePositions[p1], baked.LocalPosePositions[p1 + 1], baked.LocalPosePositions[p1 + 2]);
                    Quaternion qa = new Quaternion(baked.LocalPoseRotations[q0], baked.LocalPoseRotations[q0 + 1], baked.LocalPoseRotations[q0 + 2], baked.LocalPoseRotations[q0 + 3]);
                    Quaternion qz = new Quaternion(baked.LocalPoseRotations[q1], baked.LocalPoseRotations[q1 + 1], baked.LocalPoseRotations[q1 + 2], baked.LocalPoseRotations[q1 + 3]);
                    Bones[b].localPosition = Vector3.LerpUnclamped(a, z, alpha);
                    Bones[b].localRotation = Quaternion.SlerpUnclamped(qa, qz, alpha);
                }
            }
            Vector3 rootPosition0 = roots[first];
            Quaternion rootRotation0 = rootRotations[first];
            Vector3 rootPosition1 = toEndpoint ? V(baked.EndRootPosition) : roots[second];
            Quaternion rootRotation1 = toEndpoint ? Q(baked.EndRootRotation) : rootRotations[second];
            Vector3 rootPosition = Vector3.LerpUnclamped(rootPosition0, rootPosition1, alpha);
            Quaternion rootRotation = Quaternion.SlerpUnclamped(rootRotation0, rootRotation1, alpha);
            if (loops && cycles > 0)
            {
                RigidTransform cycle = Pow(new RigidTransform(V(baked.EndRootPosition), Q(baked.EndRootRotation)), cycles);
                rootPosition = cycle.Position + cycle.Rotation * rootPosition;
                rootRotation = cycle.Rotation * rootRotation;
            }
            Root.SetPositionAndRotation(rootPosition, rootRotation);
        }

        private struct RigidTransform
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public RigidTransform(Vector3 position, Quaternion rotation) { Position = position; Rotation = rotation; }
        }

        private static RigidTransform Pow(RigidTransform value, long exponent)
        {
            RigidTransform result = new RigidTransform(Vector3.zero, Quaternion.identity);
            while (exponent > 0)
            {
                if ((exponent & 1) != 0) result = Compose(result, value);
                value = Compose(value, value);
                exponent >>= 1;
            }
            return result;
        }

        private static RigidTransform Compose(RigidTransform a, RigidTransform b)
            => new RigidTransform(a.Position + a.Rotation * b.Position, a.Rotation * b.Rotation);

        public MotionPoseComparison CompareSample(int frame)
        {
            SetSample(frame, false);
            for (int b = 0; b < Bones.Length; b++) { comparisonPositions[b] = Bones[b].position; comparisonRotations[b] = Bones[b].rotation; }
            Vector3 root = Root.position; Quaternion rotation = Root.rotation;
            SetSample(frame, true);
            var result = new MotionPoseComparison { MaxRootPositionError = Vector3.Distance(root, Root.position), MaxRootAngleDegrees = Quaternion.Angle(rotation, Root.rotation) };
            for (int b = 0; b < Bones.Length; b++)
            {
                result.MaxBonePositionError = Mathf.Max(result.MaxBonePositionError, Vector3.Distance(comparisonPositions[b], Bones[b].position));
                result.MaxBoneAngleDegrees = Mathf.Max(result.MaxBoneAngleDegrees, Quaternion.Angle(comparisonRotations[b], Bones[b].rotation));
            }
            return result;
        }

        public void Dispose() { rig?.Dispose(); rig = null; }
        internal static Vector3 V(MotionVector v) => new Vector3(v.x, v.y, v.z);
        internal static Quaternion Q(MotionRotation q) => new Quaternion(q.x, q.y, q.z, q.w);
    }
}
