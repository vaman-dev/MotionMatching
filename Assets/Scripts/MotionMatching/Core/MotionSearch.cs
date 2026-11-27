using System;
using System.Collections.Generic;

namespace MotionMatching.Core
{
    /// <summary>Five nonnegative weights in schema order: trajectory position/facing, feet position/velocity, pelvis velocity.</summary>
    [Serializable]
    public sealed class MotionFeatureWeights
    {
        public float[] Groups = { 1, 1, 1, 1, 1 };
        public void Validate()
        {
            if (Groups == null || Groups.Length != 5) throw new ArgumentException("Exactly five feature-group weights are required.");
            foreach (float value in Groups) if (!MotionMath.Finite(value) || value < 0) throw new ArgumentException("Feature weights must be finite and nonnegative.");
        }
        public float Dimension(int dimension)
        {
            int group = dimension < 6 ? 0 : dimension < 12 ? 1 : dimension < 18 ? 2 : dimension < 24 ? 3 : 4;
            return Groups[group];
        }
    }

    public struct MotionSearchResult
    {
        public int Row, ClipIndex, LocalFrame;
        public float TimeSeconds, Cost;
        public float FeatureCost;
        public readonly float[] GroupCosts;
        public MotionSearchResult(int row, int clip, int frame, float time, float cost, float[] groups)
        { Row = row; ClipIndex = clip; LocalFrame = frame; TimeSeconds = time; Cost = cost; FeatureCost = cost; GroupCosts = groups; }
    }

    /// <summary>Nonnegative complete-cost penalty. Natural continuation uses circular clip time for loops.</summary>
    public struct MotionSearchContext
    {
        public int CurrentClip;
        public float CurrentTime, ContinuationWindow, SwitchPenalty;
        public double Penalty(MotionDatabaseData data, int row)
        {
            if (SwitchPenalty == 0 || CurrentClip < 0 || CurrentClip >= data.Clips.Length) return 0;
            var clip = data.Clips[CurrentClip];
            int local = row - clip.FrameOffset;
            if (local < 0 || local >= clip.Samples.Length) return SwitchPenalty;
            return PhaseDistance(local / data.Settings.SampleRate, CurrentTime, clip.Duration, clip.IsLoop) <= ContinuationWindow ? 0 : SwitchPenalty;
        }
        public static float PhaseDistance(float a, float b, float duration, bool loop)
        {
            if (!loop) return Math.Abs(a - b);
            float d = Math.Abs((a % duration + duration) % duration - (b % duration + duration) % duration);
            return Math.Min(d, duration - d);
        }
        public void Validate()
        {
            if (!MotionMath.Finite(CurrentTime) || !MotionMath.Finite(ContinuationWindow) || ContinuationWindow < 0 || !MotionMath.Finite(SwitchPenalty) || SwitchPenalty < 0)
                throw new ArgumentException("Continuation time/window/penalty must be finite and nonnegative.");
        }
    }

    public static class MotionSelectionPolicy
    {
        public static bool Improves(float candidateCost, float continuationCost, float absoluteMinimum, float relativeMinimum)
            => candidateCost + absoluteMinimum < continuationCost * (1 - relativeMinimum);
    }

    /// <summary>Exact weighted nearest-neighbour reference; query and database use the same baked normalization.</summary>
    public static class BruteForceMotionSearch
    {
        public static MotionSearchResult FindBest(MotionDatabaseData data, float[] rawQuery, MotionFeatureWeights weights,
            MotionSearchContext context = default, float[] groupBuffer = null)
        {
            Validate(data, rawQuery, weights);
            context.Validate();
            int best = -1; double bestCost = double.PositiveInfinity;
            for (int row = 0; row < data.FrameCount; row++)
            {
                if (!data.ValidFrames[row]) continue;
                double cost = Cost(data, row, rawQuery, weights, null) + context.Penalty(data, row);
                if (cost < bestCost || (cost == bestCost && row < best)) { best = row; bestCost = cost; }
            }
            if (best < 0) throw new InvalidOperationException("Motion database has no eligible rows.");
            return MakeResult(data, best, rawQuery, weights, (float)bestCost, groupBuffer);
        }
        internal static void Validate(MotionDatabaseData data, float[] raw, MotionFeatureWeights weights)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (raw == null || raw.Length != MotionDatabaseData.FeatureDimensions) throw new ArgumentException("Query must contain exactly 27 raw feature values.");
            if (weights == null) throw new ArgumentNullException(nameof(weights)); weights.Validate();
            foreach (float v in raw) if (!MotionMath.Finite(v)) throw new ArgumentException("Query contains a non-finite value.");
        }
        internal static double Cost(MotionDatabaseData d, int row, float[] raw, MotionFeatureWeights weights, float[] groupCosts)
        {
            double total = 0;
            for (int i = 0; i < 27; i++)
            {
                double q = (raw[i] - d.Means[i]) / d.NormalizationScales[i];
                double delta = q - d.NormalizedFeatures[row * 27 + i];
                double term = weights.Dimension(i) * delta * delta; total += term;
                if (groupCosts != null) groupCosts[i < 6 ? 0 : i < 12 ? 1 : i < 18 ? 2 : i < 24 ? 3 : 4] += (float)term;
            }
            return total;
        }
        internal static MotionSearchResult MakeResult(MotionDatabaseData d, int row, float[] raw, MotionFeatureWeights weights, float cost, float[] buffer = null)
        {
            int clipIndex = 0;
            while (clipIndex + 1 < d.Clips.Length && row >= d.Clips[clipIndex + 1].FrameOffset) clipIndex++;
            int frame = row - d.Clips[clipIndex].FrameOffset;
            var groups = buffer ?? new float[5];
            if (groups.Length != 5) throw new ArgumentException("Five group-cost slots are required.");
            Array.Clear(groups, 0, 5);
            double featureCost = Cost(d, row, raw, weights, groups);
            return new MotionSearchResult(row, clipIndex, frame, frame / d.Settings.SampleRate, cost, groups) { FeatureCost = (float)featureCost };
        }
    }

    /// <summary>Balanced cyclic-split KD-tree. Exact branch pruning, schema-normalized weighted coordinates and stable row-index ties.</summary>
    public sealed class KdTreeMotionSearch
    {
        private struct Node { public int Row, Axis, Left, Right; }
        private readonly MotionDatabaseData data;
        private readonly MotionFeatureWeights weights;
        private readonly Node[] nodes;
        private int used;
        private readonly float[] queryWorkspace = new float[27];
        public int NodesVisited { get; private set; }
        public int IndexedRowCount => nodes.Length;

        public KdTreeMotionSearch(MotionDatabaseData database, MotionFeatureWeights featureWeights)
        {
            data = database ?? throw new ArgumentNullException(nameof(database));
            if (featureWeights == null) throw new ArgumentNullException(nameof(featureWeights));
            featureWeights.Validate();
            weights = new MotionFeatureWeights { Groups = (float[])featureWeights.Groups.Clone() };
            var rows = new List<int>(); for (int i = 0; i < data.FrameCount; i++) if (data.ValidFrames[i]) rows.Add(i);
            if (rows.Count == 0) throw new ArgumentException("Motion database has no eligible rows.");
            nodes = new Node[rows.Count]; Root = Build(rows.ToArray(), 0, rows.Count, 0);
        }
        private int Root;
        private int Build(int[] rows, int start, int count, int depth)
        {
            int axis = depth % 27;
            Array.Sort(rows, start, count, Comparer<int>.Create((a, b) => { int cmp = Coordinate(a, axis).CompareTo(Coordinate(b, axis)); return cmp != 0 ? cmp : a.CompareTo(b); }));
            int mid = start + count / 2, slot = used++;
            nodes[slot] = new Node { Row = rows[mid], Axis = axis, Left = -1, Right = -1 };
            int leftCount = mid - start, rightCount = start + count - mid - 1;
            if (leftCount > 0) nodes[slot].Left = Build(rows, start, leftCount, depth + 1);
            if (rightCount > 0) nodes[slot].Right = Build(rows, mid + 1, rightCount, depth + 1);
            return slot;
        }
        private float Coordinate(int row, int axis) => data.NormalizedFeatures[row * 27 + axis] * (float)Math.Sqrt(weights.Dimension(axis));
        public MotionSearchResult FindBest(float[] rawQuery, MotionSearchContext context = default, float[] groupBuffer = null)
        {
            BruteForceMotionSearch.Validate(data, rawQuery, weights);
            context.Validate(); NodesVisited = 0;
            for (int i = 0; i < 27; i++) queryWorkspace[i] = (rawQuery[i] - data.Means[i]) / data.NormalizationScales[i] * (float)Math.Sqrt(weights.Dimension(i));
            int best = -1; double bestCost = double.PositiveInfinity; Search(Root, queryWorkspace, rawQuery, context, ref best, ref bestCost);
            return BruteForceMotionSearch.MakeResult(data, best, rawQuery, weights, (float)bestCost, groupBuffer);
        }
        private void Search(int index, float[] query, float[] raw, MotionSearchContext context, ref int best, ref double bestCost)
        {
            if (index < 0) return;
            Node node = nodes[index]; float delta = query[node.Axis] - Coordinate(node.Row, node.Axis);
            int near = delta <= 0 ? node.Left : node.Right, far = delta <= 0 ? node.Right : node.Left;
            NodesVisited++;
            Search(near, query, raw, context, ref best, ref bestCost);
            double cost = BruteForceMotionSearch.Cost(data, node.Row, raw, weights, null) + context.Penalty(data, node.Row);
            if (cost < bestCost || (cost == bestCost && (best < 0 || node.Row < best))) { best = node.Row; bestCost = cost; }
            // Coordinates are stored as float32 while costs accumulate in double; a tiny
            // conservative margin prevents rounding from pruning an equal-cost lower row.
            if (delta * delta <= bestCost + Math.Max(1e-7, Math.Abs(bestCost) * 1e-7)) Search(far, query, raw, context, ref best, ref bestCost);
        }
    }

    public struct MotionTrajectoryState
    {
        public MotionVector Position, CollisionResolvedVelocity;
        public float FacingYaw;
    }

    /// <summary>Pure camera-relative predictor. Collision-resolved velocity seeds inertia; CharacterMotor remains the movement owner.</summary>
    public static class MotionTrajectoryPredictor
    {
        public static float[] Predict(MotionTrajectoryState state, MotionVector cameraForward, MotionVector cameraRight,
            float inputX, float inputY, float desiredFacingYaw, float maxSpeed, float acceleration, float turnRateRadiansPerSecond, float[] horizons, float[] output = null)
        {
            if (horizons == null || horizons.Length != 3) throw new ArgumentException("Exactly three horizons are required.");
            if (!MotionMath.Finite(maxSpeed) || maxSpeed < 0 || !MotionMath.Finite(acceleration) || acceleration < 0 || !MotionMath.Finite(turnRateRadiansPerSecond) || turnRateRadiansPerSecond < 0) throw new ArgumentException("Prediction settings must be finite and nonnegative.");
            if (!MotionMath.Finite(state.Position.x) || !MotionMath.Finite(state.Position.y) || !MotionMath.Finite(state.Position.z) || !MotionMath.Finite(state.CollisionResolvedVelocity.x) || !MotionMath.Finite(state.CollisionResolvedVelocity.y) || !MotionMath.Finite(state.CollisionResolvedVelocity.z) || !MotionMath.Finite(state.FacingYaw) || !MotionMath.Finite(desiredFacingYaw) || !MotionMath.Finite(inputX) || !MotionMath.Finite(inputY)) throw new ArgumentException("Trajectory state and intent must be finite.");
            if (!MotionMath.Finite(cameraForward.x) || !MotionMath.Finite(cameraForward.y) || !MotionMath.Finite(cameraForward.z) || !MotionMath.Finite(cameraRight.x) || !MotionMath.Finite(cameraRight.y) || !MotionMath.Finite(cameraRight.z)) throw new ArgumentException("Camera basis must be finite.");
            output = output ?? new float[12];
            if (output.Length != 12) throw new ArgumentException("Prediction buffer must contain 12 slots.");
            float inputMag = (float)Math.Sqrt(inputX * inputX + inputY * inputY);
            if (inputMag > 1) { inputX /= inputMag; inputY /= inputMag; inputMag = 1; }
            var desired = cameraRight * inputX + cameraForward * inputY;
            desired.y = 0; float desiredLen = Length(desired); if (desiredLen > 1e-6f) desired = desired * (1 / desiredLen);
            desired = desired * (maxSpeed * inputMag);
            for (int h = 0; h < 3; h++)
            {
                float t = horizons[h]; if (!MotionMath.Finite(t) || t <= 0 || (h > 0 && t <= horizons[h - 1])) throw new ArgumentException("Horizons must be finite, positive and increasing.");
                float step = Math.Min(t, 1f / 60f);
                var p = state.Position; float yaw = state.FacingYaw;
                var velocity = state.CollisionResolvedVelocity; velocity.y = 0;
                // Bounded fixed integration makes prediction reproducible across render frame rates.
                int steps = (int)Math.Ceiling(t / step); step = t / steps;
                for (int i = 0; i < steps; i++)
                {
                    velocity = Approach(velocity, desired, acceleration * step);
                    p += velocity * step;
                    float turn = MotionMath.AngleDelta(yaw, desiredFacingYaw), maxTurn = turnRateRadiansPerSecond * step;
                    yaw += Math.Max(-maxTurn, Math.Min(maxTurn, turn));
                }
                var local = MotionMath.ToLocal(p, state.Position, state.FacingYaw);
                output[h * 2] = local.x; output[h * 2 + 1] = local.z;
                float relative = yaw - state.FacingYaw; output[6 + h * 2] = (float)Math.Sin(relative); output[7 + h * 2] = (float)Math.Cos(relative);
            }
            return output;
        }
        private static MotionVector Approach(MotionVector a, MotionVector b, float amount)
        {
            var d = b - a; float length = Length(d); return length <= amount || length < 1e-8f ? b : a + d * (amount / length);
        }
        private static float Length(MotionVector v) => (float)Math.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
    }

    public static class MotionQueryBuilder
    {
        /// <summary>Combines predicted trajectory with current baked-schema yaw-local pose features, then normalizes using database parameters at search time.</summary>
        public static float[] BuildRaw(MotionDatabaseData db, int currentRow, float[] predictedTrajectory)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (currentRow < 0 || currentRow >= db.FrameCount) throw new ArgumentOutOfRangeException(nameof(currentRow));
            if (predictedTrajectory == null || predictedTrajectory.Length != 12) throw new ArgumentException("Trajectory must contain six positions and six facing values.");
            var query = new float[27]; Array.Copy(predictedTrajectory, query, 12);
            Array.Copy(db.RawFeatures, currentRow * 27 + 12, query, 12, 15);
            return query;
        }
    }
}
