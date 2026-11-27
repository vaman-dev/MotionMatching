using System;
using MotionMatching.Core;
using Unity.Profiling;
using UnityEngine;

namespace MotionMatching.Runtime
{
    /// <summary>Opt-in grounded matching owner. The existing player/motor retain actions, gravity and collision resolution.</summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(PlayerController))]
    public sealed class MotionMatchingController : MonoBehaviour
    {
        private static readonly ProfilerMarker QueryBuildMarker = new ProfilerMarker("MotionMatching.QueryBuild");
        private static readonly ProfilerMarker KdSearchMarker = new ProfilerMarker("MotionMatching.KDTreeSearch");
        private static readonly ProfilerMarker SelectionMarker = new ProfilerMarker("MotionMatching.Selection");
        private static readonly ProfilerMarker TransitionMarker = new ProfilerMarker("MotionMatching.Transition");
        private static readonly ProfilerMarker DatabaseInitializationMarker = new ProfilerMarker("MotionMatching.DatabaseInitialization");
        [SerializeField] private MotionDatabaseAsset database;
        [Tooltip("Preserved migration dataset for explicit Inspector rollback; never selected automatically.")]
        [SerializeField] private MotionDatabaseAsset previousDatabase;
        [SerializeField] private MotionMatchingSettings settings;
        private PlayerController player;
        private PlayerAnimator facade;
        private Animator animator;
        private MotionPlayablePlayer playback;
        private MotionDatabaseData data;
        private AnimationClip[] clips;
        private KdTreeMotionSearch tree;
        private MotionFeatureWeights searchWeights;
        private Transform leftFoot, rightFoot, pelvis;
        private Vector3 lastLeft, lastRight, lastPelvis;
        private Vector3 leftVelocity, rightVelocity, pelvisVelocity;
        private bool poseHistory, started;
        private float queryTimer, clipTime;
        private int clipIndex = -1;
        private float[] trajectory;
        private readonly float[] liveQuery = new float[27], candidateGroups = new float[5], oracleGroups = new float[5];
        private readonly float[] trajectoryBuffer = new float[12];
        private Vector3 desiredVelocity, smoothedVelocity, velocityDerivative;
        private float desiredYaw, smoothedYaw, yawDerivative;
        private bool intentInitialized;
        public Vector3 DesiredVelocity => desiredVelocity;
        public Vector3 SmoothedVelocity => smoothedVelocity;
        public float DesiredYawRadians => desiredYaw;
        public float SmoothedYawRadians => smoothedYaw;
        private Vector3 lastSwitchIntent;
        private double lastSwitchTime;
        public float ContinuationCost { get; private set; }
        public int SwitchCount { get; private set; }
        public double LastSwitchTime => lastSwitchTime;
        public long QueryTicks { get; private set; }
        public int NodesVisited => tree?.NodesVisited ?? 0;
        private readonly double[] switchTimes = new double[32];
        private int switchTimeCursor;
        public float SwitchesPerSecond
        {
            get { int count=0; double now=Time.timeAsDouble; for(int i=0;i<switchTimes.Length;i++) if(switchTimes[i]>0 && now-switchTimes[i]<=1)count++; return count; }
        }
        public Vector3 LastRootDelta { get; private set; }
        public float LastRootYawDegrees { get; private set; }
        public int RootSubmissions { get; private set; }
        public long QueryAllocatedBytes { get; private set; } = -1;
        public bool AllocationCounterSupported { get; private set; }
        public long IndexBuildTicks { get; private set; }
        public bool IsReady => playback != null;
        public bool IsMatching => playback != null && playback.IsMatching;
        public bool OwnsGroundedYaw => IsMatching && settings != null && settings.ApplyRootYaw;
        public MotionSearchResult LastResult { get; private set; }
        public bool OracleAgrees { get; private set; } = true;
        public float PlaybackTimeSeconds => playback != null ? (float)playback.CurrentTimeSeconds : clipTime;
        public int CurrentClipIndex => clipIndex;
        public MotionPlayablePlayer Playback => playback;
        public MotionDatabaseAsset Database => database;
        public MotionDatabaseAsset PreviousDatabase => previousDatabase;
        public MotionMatchingSettings Settings => settings;

        private void Start() { started = true; TryInitialize(); }
        private void OnEnable() { if (started) TryInitialize(); }
        private void OnDisable() => Shutdown();
        private void OnDestroy() => Shutdown();

        public void Configure(MotionDatabaseAsset source, MotionMatchingSettings configuration)
        {
            Shutdown(); database = source; settings = configuration;
            if (started && isActiveAndEnabled) TryInitialize();
        }

        private void TryInitialize()
        {
            if (playback != null || database == null || settings == null) return;
            using var initializationScope = DatabaseInitializationMarker.Auto();
            try
            {
                player = GetComponent<PlayerController>(); facade = player.PlayerAnimator;
                if (facade == null) throw new InvalidOperationException("Matching requires the retained PlayerAnimator facade.");
                animator = facade.UnityAnimator;
                if (animator == null || !animator.isHuman || animator.avatar == null || !animator.avatar.isValid)
                    throw new InvalidOperationException("Matching requires the character's valid Humanoid Animator/Avatar.");
                if (database.Avatar != animator.avatar) throw new InvalidOperationException("Matching database Avatar differs from the character Avatar; rebake for this rig.");
                data = database.Data; MotionDatabaseBinary.ValidateData(data); clips = database.SourceClips;
                if (clips.Length != data.Clips.Length) throw new InvalidOperationException("Database source mapping is incomplete.");
                for (int i = 0; i < clips.Length; i++)
                    if (clips[i] == null || clips[i].isLooping != data.Clips[i].IsLoop || Mathf.Abs(clips[i].length - data.Clips[i].Duration) > .0001f)
                        throw new InvalidOperationException("Source clip import/duration differs from the database; rebake before playback.");
                ValidateSettings();
                leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot); rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot); pelvis = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (leftFoot == null || rightFoot == null || pelvis == null) throw new InvalidOperationException("Matching requires mapped pelvis and both feet.");
                searchWeights = new MotionFeatureWeights { Groups = (float[])settings.Weights.Groups.Clone() };
                RebuildIndex();
                long beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
                var allocationProbe = new byte[1024];
                AllocationCounterSupported = GC.GetAllocatedBytesForCurrentThread() > beforeAllocation;
                GC.KeepAlive(allocationProbe);
                playback = new MotionPlayablePlayer(animator, settings); facade.BindMotionPlayback(playback); player.BindMotionMatching(this);
                poseHistory = false; clipIndex = -1; queryTimer = 0; intentInitialized = false; SwitchCount = 0;
            }
            catch (Exception exception) { Shutdown(); Debug.LogError("Motion Matching initialization failed; legacy Animator retained: " + exception.Message, this); }
        }

        private void ValidateSettings()
        {
            settings.Weights.Validate();
            foreach (float value in new[] { settings.WalkMetersPerSecond, settings.RunMetersPerSecond, settings.AccelerationMetersPerSecondSquared, settings.TurnRadiansPerSecond, settings.BlendSeconds, settings.RequiredCostImprovement, settings.ContinuationWindowSeconds, settings.SwitchPenalty, settings.AbsoluteMinimumImprovement, settings.MinimumSwitchIntervalSeconds, settings.IntentInterruptionSpeedChange, settings.IntentInterruptionAngleDegrees, settings.VelocityHalfLife, settings.FacingHalfLife, settings.SprintMetersPerSecond, settings.JogMetersPerSecond, settings.PoseOffsetHalfLife, settings.RotationOffsetHalfLife })
                if (!MotionMath.Finite(value) || value < 0) throw new ArgumentException("Matching settings must be finite and nonnegative.");
            if (!MotionMath.Finite(settings.SearchIntervalSeconds) || settings.SearchIntervalSeconds <= 0 || settings.RequiredCostImprovement > 1)
                throw new ArgumentException("Matching search interval/improvement is invalid.");
        }

        private void Update()
        {
            if (!IsReady) return;
            float dt = Time.deltaTime; playback.Update(dt);
            UpdateIntent(dt);
            bool action = player.CurrentStateId != PlayerStateId.Locomotion;
            var controller = playback.Controller;
            bool authored = controller.GetCurrentAnimatorStateInfo(0).IsName("Jump") || controller.GetCurrentAnimatorStateInfo(0).IsName("Dive Roll") || controller.IsInTransition(0);
            if (action || (clipIndex >= 0 && !IsMatching && authored))
            {
                playback.SetMatching(false); poseHistory = false; intentInitialized = false; queryTimer = 0; return;
            }
            if (IsMatching) clipTime = (float)playback.CurrentTimeSeconds;
            queryTimer -= dt;
            if (queryTimer > 0 && IsMatching) return;
            queryTimer = settings.SearchIntervalSeconds;
            float[] query;
            using (QueryBuildMarker.Auto())
            {
                MotionTrajectoryQuery.PredictIntent(player.transform, player.ActualVelocity, smoothedVelocity, smoothedYaw,
                    settings.AccelerationMetersPerSecondSquared, settings.TurnRadiansPerSecond, data.Settings.FutureSeconds, trajectoryBuffer);
                trajectory = trajectoryBuffer;
                query = BuildLiveQuery(trajectory);
            }
            var context = new MotionSearchContext { CurrentClip = clipIndex, CurrentTime = clipTime, ContinuationWindow = settings.ContinuationWindowSeconds, SwitchPenalty = settings.SwitchPenalty };
            bool weightsChanged = false;
            for(int g=0;g<5;g++) if(settings.Weights.Groups[g]!=searchWeights.Groups[g])weightsChanged=true;
            if(weightsChanged)
            {
                settings.Weights.Validate(); Array.Copy(settings.Weights.Groups,searchWeights.Groups,5); RebuildIndex();
            }
            long allocationBefore = AllocationCounterSupported ? GC.GetAllocatedBytesForCurrentThread() : 0;
            long queryStart = System.Diagnostics.Stopwatch.GetTimestamp();
            using (KdSearchMarker.Auto()) LastResult = tree.FindBest(query, context, candidateGroups);
            QueryTicks = System.Diagnostics.Stopwatch.GetTimestamp() - queryStart;
            QueryAllocatedBytes = AllocationCounterSupported ? GC.GetAllocatedBytesForCurrentThread()-allocationBefore : -1;
            using (SelectionMarker.Auto())
            {
                if (settings.CompareBruteForce) OracleAgrees = BruteForceMotionSearch.FindBest(data, query, searchWeights, context, oracleGroups).Row == LastResult.Row;
            Vector3 intent = desiredVelocity;
            bool urgent = Mathf.Abs(intent.magnitude - lastSwitchIntent.magnitude) >= settings.IntentInterruptionSpeedChange ||
                (intent.sqrMagnitude > .01f && lastSwitchIntent.sqrMagnitude > .01f && Vector3.Angle(intent, lastSwitchIntent) >= settings.IntentInterruptionAngleDegrees);
            bool select = !IsMatching || clipIndex < 0;
            if (!select)
            {
                var current = data.Clips[clipIndex]; float localTime = current.IsLoop ? clipTime % current.Duration : Mathf.Min(clipTime, current.Duration);
                float continuation = RowCost(query, clipIndex, localTime);
                ContinuationCost = continuation;
                float timeDistance = MotionSearchContext.PhaseDistance(LastResult.TimeSeconds, localTime, current.Duration, current.IsLoop);
                bool nearby = LastResult.ClipIndex == clipIndex && timeDistance <= settings.ContinuationWindowSeconds;
                bool ended = !current.IsLoop && clipTime >= current.Duration;
                bool allowed = (Time.timeAsDouble - lastSwitchTime >= settings.MinimumSwitchIntervalSeconds && (!playback.IsBlending || settings.AllowTransitionInterruption)) || urgent;
                select = ended || (allowed && !nearby && MotionSelectionPolicy.Improves(LastResult.Cost, continuation, settings.AbsoluteMinimumImprovement, settings.RequiredCostImprovement));
            }
            if (select)
            {
                clipIndex = LastResult.ClipIndex; clipTime = LastResult.TimeSeconds;
                using (TransitionMarker.Auto())
                    playback.Play(clips[clipIndex], clipTime, data.Clips[clipIndex].IsLoop, settings.BlendSeconds);
                SwitchCount++; lastSwitchTime = Time.timeAsDouble; lastSwitchIntent = intent;
                switchTimes[switchTimeCursor++ % switchTimes.Length] = lastSwitchTime;
            }
            playback.SetMatching(true);
            }
        }

        private void RebuildIndex()
        {
            long start=System.Diagnostics.Stopwatch.GetTimestamp();
            using (DatabaseInitializationMarker.Auto()) tree=new KdTreeMotionSearch(data,searchWeights);
            IndexBuildTicks=System.Diagnostics.Stopwatch.GetTimestamp()-start;
        }
        public void RecordRootMotion(Vector3 delta, Quaternion rotation)
        {
            LastRootDelta=delta; LastRootYawDegrees=Mathf.DeltaAngle(0,rotation.eulerAngles.y); RootSubmissions++;
        }

        private void UpdateIntent(float dt)
        {
            Vector2 input = player.InputReader != null ? player.InputReader.Move : Vector2.zero;
            bool sprint = player.InputReader != null && player.InputReader.SprintHeld;
            float speed = settings.UseThreeGaits ? (sprint ? settings.SprintMetersPerSecond : settings.JogMetersPerSecond) : (sprint ? settings.RunMetersPerSecond : settings.WalkMetersPerSecond);
            desiredVelocity = player.GetCameraRelativeMoveDirection(input) * (speed * Mathf.Clamp01(input.magnitude));
            Vector3 facing = settings.FaceCamera && player.CameraTransform != null ? player.CameraTransform.forward : desiredVelocity;
            if (facing.sqrMagnitude < 1e-6f) facing = player.transform.forward;
            desiredYaw = Mathf.Atan2(facing.x, facing.z);
            if (!intentInitialized)
            {
                smoothedVelocity = player.ActualVelocity; smoothedVelocity.y = 0; velocityDerivative = Vector3.zero;
                smoothedYaw = Mathf.Atan2(player.transform.forward.x, player.transform.forward.z); yawDerivative = 0; intentInitialized = true;
            }
            MotionSpring.Step(ref smoothedVelocity.x, ref velocityDerivative.x, desiredVelocity.x, settings.VelocityHalfLife, dt);
            MotionSpring.Step(ref smoothedVelocity.z, ref velocityDerivative.z, desiredVelocity.z, settings.VelocityHalfLife, dt);
            float goal = smoothedYaw + MotionMath.AngleDelta(smoothedYaw, desiredYaw);
            MotionSpring.Step(ref smoothedYaw, ref yawDerivative, goal, settings.FacingHalfLife, dt);
        }

        private float[] BuildLiveQuery(float[] predicted)
        {
            var query = liveQuery; Array.Clear(query, 0, 27); Array.Copy(predicted, query, 12);
            float yaw = Mathf.Atan2(player.transform.forward.x, player.transform.forward.z);
            Vector3 origin = animator.transform.position;
            Put(query, 12, Quaternion.Euler(0, -yaw * Mathf.Rad2Deg, 0) * (leftFoot.position - origin));
            Put(query, 15, Quaternion.Euler(0, -yaw * Mathf.Rad2Deg, 0) * (rightFoot.position - origin));
            if (poseHistory)
            {
                Quaternion local = Quaternion.Euler(0, -yaw * Mathf.Rad2Deg, 0);
                Put(query, 18, local * leftVelocity);
                Put(query, 21, local * rightVelocity);
                Put(query, 24, local * pelvisVelocity);
            }
            return query;
        }
        // After animation and the player's LateUpdate motor step, differentiate world positions
        // each frame. Query time then rotates those velocities into the current root yaw frame.
        private void LateUpdate()
        {
            if (!IsReady) return;
            float dt = Time.deltaTime;
            playback.CaptureDisplayedPose(dt);
            if (poseHistory && dt > 0)
            {
                leftVelocity = (leftFoot.position - lastLeft) / dt;
                rightVelocity = (rightFoot.position - lastRight) / dt;
                pelvisVelocity = (pelvis.position - lastPelvis) / dt;
            }
            else leftVelocity = rightVelocity = pelvisVelocity = player.ActualVelocity;
            lastLeft = leftFoot.position; lastRight = rightFoot.position; lastPelvis = pelvis.position; poseHistory = true;
        }
        private static void Put(float[] values, int offset, Vector3 vector) { values[offset] = vector.x; values[offset + 1] = vector.y; values[offset + 2] = vector.z; }
        private float RowCost(float[] raw, int clip, float time)
        {
            var record = data.Clips[clip];
            float index = time * data.Settings.SampleRate;
            int a = Mathf.Min(record.Samples.Length - 1, Mathf.FloorToInt(index));
            int b = record.IsLoop ? (a + 1) % record.Samples.Length : Mathf.Min(a + 1, record.Samples.Length - 1);
            float blend = index - a;
            double cost = 0;
            for (int d = 0; d < 27; d++) { double delta = (raw[d] - data.Means[d]) / data.NormalizationScales[d] - Mathf.Lerp(data.NormalizedFeatures[(record.FrameOffset + a) * 27 + d], data.NormalizedFeatures[(record.FrameOffset + b) * 27 + d], blend); cost += searchWeights.Dimension(d) * delta * delta; }
            return (float)cost;
        }
        public bool ConsumeSeekSuppression() => playback != null && playback.ConsumeSeekSuppression();
        private void Shutdown()
        {
            if (facade != null) facade.BindMotionPlayback(null);
            if (player != null && player.MotionMatching == this) player.BindMotionMatching(null);
            playback?.Dispose(); playback = null; poseHistory = false; clipIndex = -1; trajectory = null;
        }
        private void OnDrawGizmosSelected()
        {
            if (!IsReady || trajectory == null || trajectory.Length < 6 || settings == null || !settings.DrawDiagnostics) return;
            Gizmos.color = Color.green; Vector3 from = transform.position;
            for (int h = 0; h < 3; h++) { Vector3 to = transform.position + transform.rotation * new Vector3(trajectory[h * 2], 0, trajectory[h * 2 + 1]); Gizmos.DrawLine(from, to); Gizmos.DrawSphere(to, .04f); from = to; }
        }
    }
}
