using System;
using System.Collections.Generic;
using MotionMatching.Core;
using MotionMatching.Runtime;
using UnityEditor;
using UnityEngine;

namespace MotionMatching.Editor
{
    /// <summary>Asset-only baking and data visualization; never modifies the gameplay scene.</summary>
    public sealed class MotionDatabaseBakerWindow : EditorWindow
    {
        [Serializable] private sealed class ClipEntry
        {
            public AnimationClip clip;
            public string tags = "locomotion";
            public bool loop;
        }

        [SerializeField] private GameObject character;
        [SerializeField] private Avatar avatar;
        [SerializeField] private List<ClipEntry> entries = new List<ClipEntry>();
        [SerializeField] private MotionDatabaseAsset database;
        [SerializeField] private float sampleRate = 60f;
        [SerializeField] private Vector3 futureSeconds = new Vector3(20f / 60f, 40f / 60f, 1f);
        [SerializeField] private float epsilon = new MotionBakeSettings().NormalizationEpsilon;
        [SerializeField] private float contactHeight = 0.05f, contactSpeed = 0.2f;
        [SerializeField] private int selectedClip, selectedSample;
        [SerializeField] private bool scenePreview;
        [SerializeField] private float velocityScale = 0.15f;
        private MotionDatabaseData preview;
        private AnimationClip[] bakedClips;
        private GameObject bakedCharacter;
        private Avatar bakedAvatar;
        private Vector2 scroll;
        private string status;
        private MessageType statusType;
        [SerializeField] private MotionPreviewDisplay displayMode = MotionPreviewDisplay.MeshAndSkeleton;
        [SerializeField] private bool storedPose, comparePose = true, diagnostics = true, extractionExpanded = true, healthExpanded;
        [SerializeField] private float playbackSpeed = 1;
        [SerializeField] private bool searchDiagnosticsExpanded;
        [SerializeField] private MotionFeatureWeights searchWeights = new MotionFeatureWeights();
        private bool hasSearchResult;
        private int searchQueryRow = -1;
        private MotionSearchResult kdSearchResult, bruteSearchResult;
        [SerializeField] private MotionHealthThresholds healthThresholds = new MotionHealthThresholds();
        private MotionHealthReport health;
        private MotionDatabasePreview viewport;
        private int viewportClip = -1;
        private MotionPreviewClock playbackClock = new MotionPreviewClock();

        [MenuItem("Tools/Motion Matching/Motion Database Baker")]
        public static void Open() => GetWindow<MotionDatabaseBakerWindow>("Motion Database Baker");

        private void OnEnable()
        {
            minSize = new Vector2(560, 500);
            if (playbackClock == null) playbackClock = new MotionPreviewClock();
            playbackClock.Reset(0, EditorApplication.timeSinceStartup);
            SceneView.duringSceneGui += DrawScenePreview;
            EditorApplication.update += UpdatePreview;
            AssemblyReloadEvents.beforeAssemblyReload += DisposeViewport;
        }
        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawScenePreview;
            EditorApplication.update -= UpdatePreview;
            AssemblyReloadEvents.beforeAssemblyReload -= DisposeViewport;
            DisposeViewport();
        }
        private void DisposeViewport() { playbackClock?.Reset(0, EditorApplication.timeSinceStartup); viewport?.Dispose(); viewport = null; viewportClip = -1; }
        private void UpdatePreview()
        {
            double now = EditorApplication.timeSinceStartup;
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) { DisposeViewport(); return; }
            if (playbackClock != null && playbackClock.IsPlaying && preview != null && selectedClip >= 0 && selectedClip < preview.Clips.Length)
            {
                double time = playbackClock.Advance(now, playbackSpeed, 0.1);
                MotionClipData clip = preview.Clips[selectedClip];
                if (clip.IsLoop)
                {
                    double localTime = time % clip.Duration;
                    selectedSample = Math.Min(clip.Samples.Length - 1, (int)(localTime * preview.Settings.SampleRate));
                }
                else
                {
                    double lastTime = (clip.Samples.Length - 1) / (double)preview.Settings.SampleRate;
                    if (time >= lastTime) { time = lastTime; playbackClock.Reset(time, now); }
                    selectedSample = Math.Min(clip.Samples.Length - 1, (int)(time * preview.Settings.SampleRate));
                }
                Repaint();
            }
            else if (playbackClock != null) playbackClock.Advance(now, playbackSpeed, 0.1);
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Humanoid Motion Database", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Samples clips in an isolated preview scene. Root trajectories and joint velocities are baked without changing scene objects or grounded animation playback.", MessageType.Info);
            extractionExpanded = EditorGUILayout.Foldout(extractionExpanded, "Sources and extraction settings", true);
            if (extractionExpanded)
            {
            EditorGUI.BeginChangeCheck();
            character = (GameObject)EditorGUILayout.ObjectField("Character asset", character, typeof(GameObject), false);
            Avatar nextAvatar = (Avatar)EditorGUILayout.ObjectField("Humanoid Avatar", avatar, typeof(Avatar), false);
            avatar = nextAvatar;
            if (avatar == null && character != null)
            {
                Animator animator = character.GetComponentInChildren<Animator>();
                if (animator != null) avatar = animator.avatar;
            }
            if (avatar != null && (!avatar.isValid || !avatar.isHuman))
                EditorGUILayout.HelpBox("The selected Avatar is not a valid Humanoid Avatar.", MessageType.Error);
            sampleRate = EditorGUILayout.FloatField("Sampling frequency (Hz)", sampleRate);
            futureSeconds = EditorGUILayout.Vector3Field("Future horizons (seconds)", futureSeconds);
            epsilon = EditorGUILayout.FloatField("Normalization epsilon", epsilon);
            contactHeight = EditorGUILayout.FloatField("Contact height (m)", contactHeight);
            contactSpeed = EditorGUILayout.FloatField("Contact speed (m/s)", contactSpeed);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Source clips", EditorStyles.boldLabel);
            for (int i = 0; i < entries.Count; i++)
            {
                ClipEntry entry = entries[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                AnimationClip next = (AnimationClip)EditorGUILayout.ObjectField(entry.clip, typeof(AnimationClip), false);
                if (next != entry.clip)
                {
                    entry.clip = next;
                    entry.loop = next != null && AnimationUtility.GetAnimationClipSettings(next).loopTime;
                }
                if (GUILayout.Button("Remove", GUILayout.Width(64)))
                {
                    entries.RemoveAt(i--);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    continue;
                }
                EditorGUILayout.EndHorizontal();
                entry.tags = EditorGUILayout.TextField("Gait / action tags", entry.tags);
                entry.loop = EditorGUILayout.Toggle("Loop", entry.loop);
                if (entry.clip == null || entry.clip.legacy || !entry.clip.humanMotion || entry.clip.length <= 0)
                    EditorGUILayout.HelpBox("Select a nonempty Humanoid animation clip with Legacy disabled.", MessageType.Error);
                else
                    EditorGUILayout.LabelField($"{entry.clip.length:F3} s; source {entry.clip.frameRate:F1} Hz", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add clip")) entries.Add(new ClipEntry());
            if (GUILayout.Button("Add selected clips"))
                foreach (UnityEngine.Object selected in Selection.objects)
                    if (selected is AnimationClip clip && !entries.Exists(item => item.clip == clip))
                        entries.Add(new ClipEntry { clip = clip, loop = AnimationUtility.GetAnimationClipSettings(clip).loopTime });
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck()) { DisposeViewport(); }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
                if (GUILayout.Button("Bake and inspect", GUILayout.Height(28))) Bake();
            }
            EditorGUILayout.Space();
            database = (MotionDatabaseAsset)EditorGUILayout.ObjectField("Database asset", database, typeof(MotionDatabaseAsset), false);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(database == null))
                if (GUILayout.Button("Reload and inspect")) Reload();
            using (new EditorGUI.DisabledScope(preview == null))
                if (GUILayout.Button("Save baked database…")) Save();
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, statusType);
            DrawPreviewControls();
            EditorGUILayout.EndScrollView();
        }

        private void Bake()
        {
            DisposeViewport(); health = null;
            preview = null;
            bakedClips = null;
            bakedCharacter = null;
            bakedAvatar = null;
            SceneView.RepaintAll();
            try
            {
                EditorUtility.DisplayProgressBar("Motion Database", "Sampling Humanoid animations…", 0.1f);
                var clips = new AnimationClip[entries.Count];
                var tags = new string[entries.Count];
                var loops = new bool[entries.Count];
                for (int i = 0; i < entries.Count; i++)
                {
                    clips[i] = entries[i].clip;
                    tags[i] = entries[i].tags;
                    loops[i] = entries[i].loop;
                }
                MotionBakeSettings settings = CreateSettings();
                preview = MotionDatabaseBaker.Bake(character, avatar, clips, settings, tags, loops);
                bakedClips = clips;
                bakedCharacter = character;
                bakedAvatar = avatar;
                selectedClip = selectedSample = 0;
                health = MotionDatabaseHealth.Analyze(preview, healthThresholds);
                extractionExpanded = false;
                status = "Bake complete. Inspect the path and velocities, then save the database asset.";
                statusType = MessageType.Info;
                SceneView.RepaintAll();
            }
            catch (Exception exception) { Report(exception); }
            finally { EditorUtility.ClearProgressBar(); }
        }

        private void Reload()
        {
            DisposeViewport(); health = null;
            preview = null;
            bakedClips = null;
            bakedCharacter = null;
            bakedAvatar = null;
            try
            {
                MotionDatabaseData loaded = database.Data;
                MotionDatabaseBaker.ValidateSources(loaded, database.SourceClips, database.Character, database.Avatar);
                preview = loaded;
                character = database.Character;
                avatar = database.Avatar;
                bakedCharacter = character;
                bakedAvatar = avatar;
                entries.Clear();
                AnimationClip[] clips = database.SourceClips;
                bakedClips = clips;
                for (int i = 0; i < clips.Length; i++)
                    entries.Add(new ClipEntry { clip = clips[i], loop = preview.Clips[i].IsLoop, tags = preview.Clips[i].Tag });
                sampleRate = preview.Settings.SampleRate;
                float[] horizons = preview.Settings.FutureSeconds;
                futureSeconds = new Vector3(horizons[0], horizons[1], horizons[2]);
                epsilon = preview.Settings.NormalizationEpsilon;
                contactHeight = preview.Settings.ContactHeight;
                contactSpeed = preview.Settings.ContactSpeed;
                selectedClip = selectedSample = 0;
                health = MotionDatabaseHealth.Analyze(preview, healthThresholds);
                extractionExpanded = false;
                status = $"Loaded {database.PayloadBytes:N0} bytes, baked in Unity {database.EngineVersion}.";
                statusType = MessageType.Info;
                SceneView.RepaintAll();
            }
            catch (Exception exception) { Report(exception); }
        }

        private void Save()
        {
            string path = EditorUtility.SaveFilePanelInProject("Save Motion Database", database != null ? database.name : "MotionDatabase", "asset", "Choose a database asset location.");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
                if (existing != null && !(existing is MotionDatabaseAsset))
                    throw new InvalidOperationException("That path belongs to another asset type. Choose a different path.");
                MotionDatabaseAsset target = existing as MotionDatabaseAsset;
                bool created = target == null;
                if (created) target = CreateInstance<MotionDatabaseAsset>();
                else Undo.RecordObject(target, "Update Motion Database");
                try
                {
                    MotionDatabaseBaker.Store(target, preview, bakedClips, bakedCharacter, bakedAvatar);
                    if (created) AssetDatabase.CreateAsset(target, path);
                    EditorUtility.SetDirty(target);
                    AssetDatabase.SaveAssetIfDirty(target);
                    database = target;
                    status = $"Saved {path}. Existing asset GUIDs are preserved on update.";
                    statusType = MessageType.Info;
                    EditorGUIUtility.PingObject(database);
                }
                catch { if (created && !AssetDatabase.Contains(target)) DestroyImmediate(target); throw; }
            }
            catch (Exception exception) { Report(exception); }
        }

        private void Report(Exception exception)
        {
            status = exception.Message;
            statusType = MessageType.Error;
            Debug.LogException(exception);
        }

        private MotionBakeSettings CreateSettings()
        {
            return new MotionBakeSettings
            {
                SampleRate = sampleRate,
                FutureSeconds = new[] { futureSeconds.x, futureSeconds.y, futureSeconds.z },
                NormalizationEpsilon = epsilon,
                ContactHeight = contactHeight,
                ContactSpeed = contactSpeed
            };
        }

        private void DrawPreviewControls()
        {
            if (preview == null || preview.Clips.Length == 0) return;
            EditorGUILayout.Space();
            int valid = 0;
            foreach (bool searchable in preview.ValidFrames) if (searchable) valid++;
            EditorGUILayout.LabelField($"{preview.Clips.Length} clips · {preview.FrameCount:N0} samples · {valid:N0} searchable", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Schema {preview.Settings.SchemaVersion} · {MotionDatabaseData.FeatureDimensions} features · {preview.Settings.SampleRate:F1} Hz", EditorStyles.miniLabel);
            string[] names = new string[preview.Clips.Length];
            for (int i = 0; i < names.Length; i++) names[i] = preview.Clips[i].Name;
            selectedClip = Mathf.Clamp(selectedClip, 0, names.Length - 1);
            EditorGUI.BeginChangeCheck();
            selectedClip = EditorGUILayout.Popup("Inspect clip", selectedClip, names);
            if (EditorGUI.EndChangeCheck()) { DisposeViewport(); selectedSample = 0; }
            MotionClipData clip = preview.Clips[selectedClip];
            selectedSample = Mathf.Clamp(selectedSample, 0, clip.Samples.Length - 1);
            EditorGUI.BeginChangeCheck();
            selectedSample = EditorGUILayout.IntSlider("Sample", selectedSample, 0, clip.Samples.Length - 1);
            scenePreview = EditorGUILayout.Toggle("Draw in Scene view", scenePreview);
            velocityScale = EditorGUILayout.Slider("Velocity arrow seconds", velocityScale, 0.01f, 1f);
            if (EditorGUI.EndChangeCheck()) { playbackClock.Reset(selectedSample / (double)preview.Settings.SampleRate, EditorApplication.timeSinceStartup); SceneView.RepaintAll(); }
            int frame = clip.FrameOffset + selectedSample;
            EditorGUILayout.LabelField($"t = {selectedSample / preview.Settings.SampleRate:F3} s · tag: {clip.Tag} · loop: {clip.IsLoop}");
            Vector3 rootVelocity = Packed(preview.RootLinearVelocities, frame * 3);
            Vector3 rootAngularVelocity = Packed(preview.RootAngularVelocities, frame * 3);
            EditorGUILayout.LabelField($"Root local velocity {rootVelocity:F3} m/s · yaw {rootAngularVelocity.y:F3} rad/s");
            EditorGUILayout.LabelField($"Pose bones: {clip.BoneNames.Length} · source GUID: {clip.Guid}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Searchable: {preview.ValidFrames[frame]} · heuristic foot contacts L: {preview.LeftFootContacts[frame]}, R: {preview.RightFootContacts[frame]}");
            DrawSearchDiagnostics(frame);
            EditorGUILayout.HelpBox("Plot: root path in metres (X right, Z up). Green point is selected sample. Cyan, yellow and magenta arrows show pelvis, left foot and right foot velocities. Contact flags are height/speed heuristics, not authored labels.", MessageType.None);
            Draw3DPreview();
            Rect rect = GUILayoutUtility.GetRect(100, 220, GUILayout.ExpandWidth(true));
            DrawPathPlot(rect, clip);
            DrawHealth();
        }

        private void DrawSearchDiagnostics(int frame)
        {
            searchDiagnosticsExpanded = EditorGUILayout.Foldout(searchDiagnosticsExpanded, "M3 search diagnostics", true);
            if (!searchDiagnosticsExpanded) return;
            EditorGUILayout.HelpBox("Uses the selected baked row as a deterministic query. This validates row mapping, weighting and backend agreement; it is not live player trajectory input.", MessageType.Info);
            string[] labels = { "Trajectory position", "Trajectory facing", "Foot position", "Foot velocity", "Pelvis velocity" };
            if (searchWeights == null) searchWeights = new MotionFeatureWeights();
            for (int i = 0; i < labels.Length; i++) searchWeights.Groups[i] = EditorGUILayout.Slider(labels[i], searchWeights.Groups[i], 0, 4);
            using (new EditorGUI.DisabledScope(!preview.ValidFrames[frame]))
                if (GUILayout.Button("Search selected database row"))
                {
                    var query = new float[MotionDatabaseData.FeatureDimensions];
                    Array.Copy(preview.RawFeatures, frame * query.Length, query, 0, query.Length);
                    var tree = new KdTreeMotionSearch(preview, searchWeights);
                    kdSearchResult = tree.FindBest(query);
                    bruteSearchResult = BruteForceMotionSearch.FindBest(preview, query, searchWeights);
                    searchQueryRow = frame; hasSearchResult = true;
                }
            if (!hasSearchResult) return;
            MotionClipData match = preview.Clips[kdSearchResult.ClipIndex];
            EditorGUILayout.LabelField($"Query row {searchQueryRow} → KD row {kdSearchResult.Row}; exact agreement: {kdSearchResult.Row == bruteSearchResult.Row}");
            EditorGUILayout.LabelField($"Selected: {match.Name} (clip {kdSearchResult.ClipIndex}, {match.Guid}) · frame {kdSearchResult.LocalFrame} · {kdSearchResult.TimeSeconds:F3} s · cost {kdSearchResult.Cost:F5}");
            EditorGUILayout.LabelField($"Group costs: trajectory position {kdSearchResult.GroupCosts[0]:F4}, facing {kdSearchResult.GroupCosts[1]:F4}, feet position {kdSearchResult.GroupCosts[2]:F4}, feet velocity {kdSearchResult.GroupCosts[3]:F4}, pelvis {kdSearchResult.GroupCosts[4]:F4}", EditorStyles.miniLabel);
            int eligible = 0; foreach (bool valid in preview.ValidFrames) if (valid) eligible++;
            EditorGUILayout.LabelField($"Eligible rows: {eligible} · brute cost {bruteSearchResult.Cost:F5}", EditorStyles.miniLabel);
        }

        private void Draw3DPreview()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.BeginHorizontal();
                displayMode = (MotionPreviewDisplay)EditorGUILayout.EnumPopup("3D display", displayMode);
                storedPose = EditorGUILayout.ToggleLeft("Stored baked pose", storedPose, GUILayout.Width(140));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("◀", GUILayout.Width(30))) { selectedSample = Mathf.Max(0, selectedSample - 1); playbackClock.Reset(selectedSample / (double)preview.Settings.SampleRate, EditorApplication.timeSinceStartup); }
                if (GUILayout.Button(playbackClock.IsPlaying ? "Pause" : "Play", GUILayout.Width(55))) playbackClock.SetPlaying(!playbackClock.IsPlaying, EditorApplication.timeSinceStartup);
                if (GUILayout.Button("▶", GUILayout.Width(30))) { selectedSample = Mathf.Min(preview.Clips[selectedClip].Samples.Length - 1, selectedSample + 1); playbackClock.Reset(selectedSample / (double)preview.Settings.SampleRate, EditorApplication.timeSinceStartup); }
                playbackSpeed = EditorGUILayout.Slider("Playback speed", playbackSpeed, 0.1f, 2);
                if (GUILayout.Button("Reset camera", GUILayout.Width(100))) viewport?.ResetCamera();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                diagnostics = EditorGUILayout.ToggleLeft("Motion diagnostics", diagnostics);
                comparePose = EditorGUILayout.ToggleLeft("Compare evaluated / stored pose", comparePose);
                EditorGUILayout.EndHorizontal();
                Rect rect = GUILayoutUtility.GetRect(100, 320, GUILayout.ExpandWidth(true));
                try
                {
                    if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
                    if (viewport == null || viewportClip != selectedClip)
                    {
                        DisposeViewport();
                        viewport = new MotionDatabasePreview(bakedCharacter, bakedAvatar, bakedClips[selectedClip], preview, selectedClip);
                        viewportClip = selectedClip;
                    }
                    viewport.Draw(rect, playbackClock.IsPlaying || playbackClock.Time > selectedSample / (double)preview.Settings.SampleRate ? playbackClock.Time : selectedSample / (double)preview.Settings.SampleRate,
                        storedPose, displayMode, diagnostics, velocityScale, comparePose);
                    if (comparePose)
                    {
                        var p = viewport.Comparison;
                        EditorGUILayout.LabelField($"Evaluated / stored max error: root {p.MaxRootPositionError:F6} m / {p.MaxRootAngleDegrees:F4}°; bones {p.MaxBonePositionError:F6} m / {p.MaxBoneAngleDegrees:F4}°", EditorStyles.miniLabel);
                    }
                    EditorGUILayout.LabelField("Drag: orbit · Shift-drag / middle: pan · Wheel: zoom. Pose cache is evaluated once per selected clip; no rebake on scrub.", EditorStyles.wordWrappedMiniLabel);
                }
                catch (Exception e) { DisposeViewport(); EditorGUILayout.HelpBox("3D preview unavailable: " + e.Message, MessageType.Error); }
                if (Event.current.type == EventType.Used) Repaint();
            }
        }

        private void DrawHealth()
        {
            healthExpanded = EditorGUILayout.Foldout(healthExpanded, "Motion Database Health", true);
            if (!healthExpanded || health == null) return;
            EditorGUILayout.LabelField($"{health.Clips.Length} clips · {health.Samples} samples · {health.Searchable} searchable · {health.Excluded} excluded");
            foreach (string error in health.Errors) EditorGUILayout.HelpBox(error, MessageType.Error);
            foreach (string warning in health.Warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
            EditorGUILayout.LabelField($"Near-constant feature dimensions: {health.ConstantDimensions}/27 · non-finite values: {health.InvalidFeatureValues}");
            EditorGUILayout.LabelField("Warning thresholds only (never discard clips)", EditorStyles.boldLabel);
            healthThresholds.RootSpeed = EditorGUILayout.FloatField("Root speed (m/s)", healthThresholds.RootSpeed);
            healthThresholds.AngularSpeed = EditorGUILayout.FloatField("Angular speed (rad/s)", healthThresholds.AngularSpeed);
            healthThresholds.JointSpeed = EditorGUILayout.FloatField("Joint speed (m/s)", healthThresholds.JointSpeed);
            healthThresholds.SeamAngleDegrees = EditorGUILayout.FloatField("Last/first pose angle (degrees)", healthThresholds.SeamAngleDegrees);
            if (GUILayout.Button("Refresh health report")) health = MotionDatabaseHealth.Analyze(preview, healthThresholds);
            foreach (var clip in health.Clips)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(clip.Name + " — " + clip.MeasuredMotion, EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Declared tags: {clip.DeclaredTags} · {clip.Searchable}/{clip.Samples} searchable");
                EditorGUILayout.LabelField($"Root speed {clip.MinRootSpeed:F3}–{clip.MaxRootSpeed:F3} m/s · angular {clip.MinAngularSpeed:F3}–{clip.MaxAngularSpeed:F3} rad/s · max joint {clip.MaxJointSpeed:F3} m/s");
                EditorGUILayout.LabelField($"Net X/Z {clip.NetX:F3}/{clip.NetZ:F3} m · heading Δ {clip.HeadingChangeDegrees:F1}° · loop root step {clip.SeamRootStep:F4} m · pose boundary {clip.SeamPoseAngleDegrees:F2}°");
                EditorGUILayout.LabelField($"Contact fraction L/R {clip.LeftContactFraction:P0}/{clip.RightContactFraction:P0} · changes {clip.LeftContactChanges}/{clip.RightContactChanges}");
                foreach (string warning in clip.Warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.LabelField("Normalization: dimension / mean / actual std / applied scale", EditorStyles.boldLabel);
            for (int d = 0; d < 27; d++) EditorGUILayout.LabelField($"{d}: {preview.Means[d]:G5} / {preview.StandardDeviations[d]:G5} / {preview.NormalizationScales[d]:G5}", EditorStyles.miniLabel);
        }

        private void DrawPathPlot(Rect rect, MotionClipData clip)
        {
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (MotionSample sample in clip.Samples)
            {
                minX = Mathf.Min(minX, sample.RootPosition.x); maxX = Mathf.Max(maxX, sample.RootPosition.x);
                minZ = Mathf.Min(minZ, sample.RootPosition.z); maxZ = Mathf.Max(maxZ, sample.RootPosition.z);
            }
            DrawJointVectors(clip, (from, to, color) =>
            {
                minX = Mathf.Min(minX, from.x, to.x); maxX = Mathf.Max(maxX, from.x, to.x);
                minZ = Mathf.Min(minZ, from.z, to.z); maxZ = Mathf.Max(maxZ, from.z, to.z);
            });
            float extent = Mathf.Max(1, maxX - minX, maxZ - minZ);
            float scale = Mathf.Min(rect.width - 32, rect.height - 32) / extent;
            Vector2 center = new Vector2((minX + maxX) / 2, (minZ + maxZ) / 2);
            Vector3 Point(Vector3 value) => new Vector3(rect.center.x + (value.x - center.x) * scale, rect.center.y - (value.z - center.y) * scale, 0);
            Handles.BeginGUI();
            Color old = Handles.color;
            try
            {
                Handles.color = Color.gray;
                var path = new Vector3[clip.Samples.Length];
                for (int i = 0; i < path.Length; i++) path[i] = Point(ToVector(clip.Samples[i].RootPosition));
                Handles.DrawAAPolyLine(2, path);
                MotionSample sample = clip.Samples[selectedSample];
                Vector3 origin = ToVector(sample.RootPosition);
                Handles.color = Color.green;
                Handles.DrawSolidDisc(Point(origin), Vector3.forward, 4);
                Quaternion facing = Quaternion.Euler(0, MotionMath.Yaw(sample.RootRotation) * Mathf.Rad2Deg, 0);
                Handles.DrawAAPolyLine(2, Point(origin), Point(origin + facing * Vector3.forward * 0.25f));
                DrawJointVectors(clip, (from, to, color) =>
                {
                    Handles.color = color;
                    Handles.DrawAAPolyLine(3, Point(from), Point(to));
                    Handles.DrawSolidDisc(Point(from), Vector3.forward, 3);
                });
            }
            finally { Handles.color = old; Handles.EndGUI(); }
            GUI.Label(new Rect(rect.x + 6, rect.y + 4, rect.width - 12, 20), $"Root XZ path · displayed width {rect.width / scale:F2} m");
        }

        private void DrawScenePreview(SceneView sceneView)
        {
            if (!scenePreview || preview == null || selectedClip < 0 || selectedClip >= preview.Clips.Length) return;
            MotionClipData clip = preview.Clips[selectedClip];
            if (selectedSample < 0 || selectedSample >= clip.Samples.Length) return;
            Color old = Handles.color;
            try
            {
                Handles.color = Color.green;
                var points = new Vector3[clip.Samples.Length];
                for (int i = 0; i < points.Length; i++) points[i] = ToVector(clip.Samples[i].RootPosition);
                Handles.DrawAAPolyLine(2, points);
                Vector3 root = points[selectedSample];
                Handles.Label(root + Vector3.up * 1.8f, $"Baked preview: {clip.Name} [{selectedSample}]\nRoot path in sampling coordinates; no scene objects created");
                DrawJointVectors(clip, (from, to, color) =>
                {
                    Handles.color = color;
                    Handles.DrawLine(from, to);
                    Handles.SphereHandleCap(0, from, Quaternion.identity, 0.04f, EventType.Repaint);
                    Vector3 direction = to - from;
                    if (direction.sqrMagnitude > 0.000001f)
                        Handles.ConeHandleCap(0, to, Quaternion.LookRotation(direction), 0.06f, EventType.Repaint);
                });
            }
            finally { Handles.color = old; }
        }

        private void DrawJointVectors(MotionClipData clip, Action<Vector3, Vector3, Color> draw)
        {
            int frame = clip.FrameOffset + selectedSample;
            MotionSample sample = clip.Samples[selectedSample];
            Vector3 root = ToVector(sample.RootPosition);
            Quaternion yaw = Quaternion.Euler(0, MotionMath.Yaw(sample.RootRotation) * Mathf.Rad2Deg, 0);
            Color[] colors = { Color.cyan, Color.yellow, Color.magenta };
            for (int joint = 0; joint < 3; joint++)
            {
                int offset = frame * 9 + joint * 3;
                Vector3 position = root + yaw * Packed(preview.JointPositions, offset);
                Vector3 velocity = yaw * Packed(preview.JointVelocities, offset);
                draw(position, position + velocity * velocityScale, colors[joint]);
            }
        }

        private static Vector3 ToVector(MotionVector value) => new Vector3(value.x, value.y, value.z);
        private static Vector3 Packed(float[] values, int offset) => new Vector3(values[offset], values[offset + 1], values[offset + 2]);
    }
}
