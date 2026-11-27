using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Editor-only observation tool; never saves scenes or calls gameplay actions directly.
[InitializeOnLoad]
public static class MigrationBaselineCapture
{
    private const string Prefix = "MotionMatching.Baseline.";
    private static readonly string[] Names = { "idle", "walk", "settle", "sprint", "turn", "settle", "jump", "settle", "dive", "settle", "look" };
    private static readonly float[] Seconds = { 2, 3, 1, 3, 2, 1, 2, 1, 2, 1, 1 };
    private static Capture _capture;
    private static Keyboard _keyboard, _previousKeyboard;
    private static Mouse _mouse, _previousMouse;
    private static Unity.Cinemachine.CinemachineOrbitalFollow _orbitalFollow;
    private static PlayerController _player;
    private static PlayerStateMachine _stateMachine;
    private static readonly List<InputDevice> DisabledDevices = new List<InputDevice>();
    private static InputSettings _originalInputSettings, _captureInputSettings;
    private static int _scenario, _lastFrame = -1;
    private static float _scenarioStartSeconds;
    private static double _deadline;
    private static Key[] _keys = Array.Empty<Key>();
    private static bool _move, _sprint, _jump, _dive, _jumpState, _diveState;
    private static bool _lookObserved, _lookStarted;
    private static float _lookStartDegrees, _lookMaxChangeDegrees;
    private static Vector2 _mouseDelta;

    [Serializable] private sealed class Capture
    {
        public int schemaVersion = 3;
        public string unityVersion, scene, capturedUtc;
        public int simulationHz = 60;
        public string scope = "Synthetic keyboard/mouse observations of existing gameplay and free camera; no visual or collision certification.";
        public List<Sample> samples = new List<Sample>();
        public List<string> errors = new List<string>();
    }
    [Serializable] private sealed class Sample
    {
        public string requestedScenario, observedPlayerState, animatorClips;
        public float scenarioElapsedSeconds, timeSeconds, deltaSeconds, animatorNormalizedTime;
        public int frame, animatorBaseStateHash;
        public Vector3 capsulePositionMeters, capsuleVelocityMetersPerSecond, animatorDeltaPositionMeters;
        public Quaternion capsuleRotation, animatorDeltaRotation;
        public Vector2 observedMoveInput, observedLookInput;
        public float cameraOrbitHorizontalDegrees;
        public bool observedSprintHeld, observedJumpHeld, observedDiveHeld, grounded;
        public Vector3 cameraPositionMeters;
        public Quaternion cameraRotation;
    }
    [Serializable] private sealed class SavedScene { public string path; public bool loaded, active; }
    [Serializable] private sealed class SavedEditor
    {
        public List<SavedScene> scenes = new List<SavedScene>();
        public List<string> selection = new List<string>();
        public float captureDeltaTime, timeScale, fixedDeltaTime;
        public bool runInBackground, paused;
    }
    [Serializable] private sealed class Completion
    {
        public string sampleOutput, completedUtc;
        public bool restored, succeeded;
        public List<string> errors = new List<string>();
    }

    static MigrationBaselineCapture()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        if (SessionState.GetString(Prefix + "Phase", "") == "running") Resume();
        else if (SessionState.GetString(Prefix + "Phase", "") == "finished")
            Application.logMessageReceived += RecordError;
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("RunBatch requires a dedicated batch Editor.");
        Start(Argument("-baselineScene", "Assets/Scenes/MotionMatchingTest.unity"),
            Argument("-baselineOutput", ".utmp/m0-baseline/playmode-baseline.json"), true);
    }
    public static void RunInteractive()
    {
        RunInteractive("Assets/Scenes/MotionMatchingTest.unity",
            ".utmp/m0-baseline/interactive-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffff") + ".json");
    }
    public static void RunInteractive(string scenePath, string outputPath)
    {
        if (Application.isBatchMode) throw new InvalidOperationException("Use RunBatch in a batch Editor.");
        Start(scenePath, outputPath, false);
    }
    public static void RecoverInteractive()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating ||
            SessionState.GetString(Prefix + "Phase", "") != "recovery" ||
            SessionState.GetString(Prefix + "SavedEditor", "") == "")
            throw new InvalidOperationException("Recovery requires an idle interactive Editor with retained recovery state.");
        CleanupInput();
        RestoreEditor();
        Debug.Log("Baseline Editor recovery completed; prior sample and completion evidence remain unchanged.");
    }
    private static void Start(string scene, string outputPath, bool batch)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            SessionState.GetString(Prefix + "Phase", "") != "")
            throw new InvalidOperationException("Capture requires an idle Edit Mode Editor with no active capture.");
        bool dirtyScene = false;
        for (int index = 0; index < UnityEngine.SceneManagement.SceneManager.sceneCount; index++)
            dirtyScene |= UnityEngine.SceneManagement.SceneManager.GetSceneAt(index).isDirty;
        if (dirtyScene || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Capture refuses dirty scenes and open Prefab Stages.");
        string output = Path.GetFullPath(outputPath);
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (!output.StartsWith(Path.Combine(root, ".utmp") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Capture output must be under the ignored workspace .utmp directory.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null || File.Exists(output) || File.Exists(Path.ChangeExtension(output, "completion.json")))
            throw new InvalidOperationException("Scene must exist and output must be fresh; evidence is never overwritten.");
        var saved = new SavedEditor {
            captureDeltaTime = Time.captureDeltaTime, timeScale = Time.timeScale,
            fixedDeltaTime = Time.fixedDeltaTime, runInBackground = Application.runInBackground, paused = EditorApplication.isPaused
        };
        foreach (SceneSetup setup in EditorSceneManager.GetSceneManagerSetup())
        {
            if (string.IsNullOrEmpty(setup.path)) throw new InvalidOperationException("Untitled scenes cannot be safely restored.");
            saved.scenes.Add(new SavedScene { path = setup.path, loaded = setup.isLoaded, active = setup.isActive });
        }
        foreach (UnityEngine.Object item in Selection.objects)
            saved.selection.Add(GlobalObjectId.GetGlobalObjectIdSlow(item).ToString());
        SessionState.SetString(Prefix + "SavedEditor", JsonUtility.ToJson(saved));
        SessionState.SetString(Prefix + "Scene", scene);
        SessionState.SetString(Prefix + "Output", output);
        SessionState.SetBool(Prefix + "Batch", batch);
        SessionState.SetString(Prefix + "Completion", JsonUtility.ToJson(new Completion()));
        SessionState.SetString(Prefix + "Phase", "running");
        try
        {
            EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            Resume();
            EditorApplication.isPaused = false;
            EditorApplication.isPlaying = true;
        }
        catch { CleanupInput(); RestoreEditor(); throw; }
    }
    private static void Resume()
    {
        // An unfocused Editor may stop its player loop before frame 3. Enable this
        // before entering Play and again after its domain reload; RestoreTime owns restoration.
        Application.runInBackground = true;
        _capture = new Capture { unityVersion = Application.unityVersion,
            scene = SessionState.GetString(Prefix + "Scene", ""), capturedUtc = DateTime.UtcNow.ToString("o") };
        _scenario = 0; _lastFrame = -1; _player = null; _stateMachine = null;
        _move = _sprint = _jump = _dive = _jumpState = _diveState = false;
        _lookObserved = _lookStarted = false;
        _lookStartDegrees = _lookMaxChangeDegrees = 0f;
        _mouseDelta = Vector2.zero;
        _orbitalFollow = null;
        _keys = Array.Empty<Key>();
        _deadline = EditorApplication.timeSinceStartup + 120;
        Application.logMessageReceived -= RecordError;
        Application.logMessageReceived += RecordError;
        EditorApplication.update -= Observe;
        EditorApplication.update += Observe;
    }
    private static string Argument(string name, string fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
    }
    private static void BeforeReload()
    {
        if (_player != null && SessionState.GetString(Prefix + "Phase", "") == "running")
        {
            _capture.errors.Add("Script reload interrupted capture.");
            Finish();
        }
    }
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        string phase = SessionState.GetString(Prefix + "Phase", "");
        if (phase == "") return;
        if (state == PlayModeStateChange.ExitingPlayMode && phase == "running")
        {
            _capture.errors.Add("Play Mode stopped before all scenarios completed.");
            Finish();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall -= CompleteRestore;
            EditorApplication.delayCall += CompleteRestore;
        }
    }
    private static void Observe()
    {
        if (SessionState.GetString(Prefix + "Phase", "") != "running") return;
        try
        {
            if (EditorApplication.timeSinceStartup > _deadline)
            {
                _capture.errors.Add("Capture exceeded the 120 second Editor deadline.");
                Finish(); return;
            }
            if (!EditorApplication.isPlaying || Time.frameCount < 3 || Time.frameCount == _lastFrame) return;
            _lastFrame = Time.frameCount;
            if (_player == null)
            {
                _player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                if (_player == null || _player.InputReader == null || _player.CharacterController == null || _player.PlayerAnimator == null)
                    throw new InvalidOperationException("Baseline requires the existing player, input reader, capsule and Animator.");
                _stateMachine = typeof(PlayerController).GetField("stateMachine", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_player) as PlayerStateMachine;
                if (_stateMachine == null) throw new InvalidOperationException("Cannot observe the legacy state machine.");
                _orbitalFollow = UnityEngine.Object.FindAnyObjectByType<Unity.Cinemachine.CinemachineOrbitalFollow>();
                if (_orbitalFollow == null) throw new InvalidOperationException("Capture requires the retained free-camera orbital follow.");
                _originalInputSettings = InputSystem.settings;
                _captureInputSettings = UnityEngine.Object.Instantiate(_originalInputSettings);
                _captureInputSettings.hideFlags = HideFlags.HideAndDontSave;
                _captureInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                _captureInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                _captureInputSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                InputSystem.settings = _captureInputSettings;
                _previousKeyboard = Keyboard.current;
                _previousMouse = Mouse.current;
                foreach (InputDevice device in InputSystem.devices)
                    if (device.enabled) DisabledDevices.Add(device);
                foreach (InputDevice device in DisabledDevices) InputSystem.DisableDevice(device);
                _keyboard = InputSystem.AddDevice<Keyboard>();
                _mouse = InputSystem.AddDevice<Mouse>();
                InputSystem.onBeforeUpdate += ProvideInput;
                _scenarioStartSeconds = Time.time;
                Time.captureDeltaTime = 1f / _capture.simulationHz;
                Time.timeScale = 1;
                Application.runInBackground = true;
                return;
            }
            float elapsed = Time.time - _scenarioStartSeconds;
            if (elapsed >= Seconds[_scenario])
            {
                _scenario++; _scenarioStartSeconds = Time.time; elapsed = 0;
                if (_scenario == Names.Length) { Finish(); return; }
            }
            string scenario = Names[_scenario];
            _mouseDelta = scenario == "look" ? Vector2.right : Vector2.zero;
            if (scenario == "look")
            {
                if (!_lookStarted)
                {
                    _lookStarted = true;
                    _lookStartDegrees = _orbitalFollow.HorizontalAxis.Value;
                }
                _lookObserved |= _player.InputReader.Look.sqrMagnitude > 0.01f;
                _lookMaxChangeDegrees = Mathf.Max(_lookMaxChangeDegrees,
                    Mathf.Abs(Mathf.DeltaAngle(_lookStartDegrees, _orbitalFollow.HorizontalAxis.Value)));
            }
            _keys = scenario == "walk" ? new[] { Key.W } :
                scenario == "sprint" ? new[] { Key.W, Key.LeftShift } :
                scenario == "turn" ? new[] { Key.D } :
                scenario == "jump" && elapsed < 0.15f ? new[] { Key.Space } :
                scenario == "dive" && elapsed < 0.15f ? new[] { Key.Tab } : Array.Empty<Key>();
            _move |= _player.InputReader.Move.sqrMagnitude > 0.01f;
            _sprint |= _player.InputReader.SprintHeld;
            _jump |= _player.InputReader.JumpHeld;
            _dive |= _player.InputReader.DiveRollHeld;
            _jumpState |= _stateMachine.CurrentStateId == PlayerStateId.Jump;
            _diveState |= _stateMachine.CurrentStateId == PlayerStateId.DiveRoll;
            Animator animator = _player.PlayerAnimator.UnityAnimator;
            if (animator == null) throw new InvalidOperationException("Player Animator is missing.");
            AnimatorStateInfo state = _player.PlayerAnimator.ReadState(0);
            var clips = new List<string>();
            var matching = _player.MotionMatching;
            if (matching != null && matching.IsMatching && matching.CurrentClipIndex >= 0)
                clips.Add(matching.Database.SourceClips[matching.CurrentClipIndex].name);
            else if (matching != null && matching.IsReady)
            {
                foreach (AnimatorClipInfo clip in matching.Playback.Controller.GetCurrentAnimatorClipInfo(0)) clips.Add(clip.clip.name);
            }
            else
                foreach (AnimatorClipInfo clip in animator.GetCurrentAnimatorClipInfo(0)) clips.Add(clip.clip.name);
            Camera camera = Camera.main;
            _capture.samples.Add(new Sample {
                requestedScenario = scenario, scenarioElapsedSeconds = elapsed,
                frame = Time.frameCount, timeSeconds = Time.time, deltaSeconds = Time.deltaTime,
                capsulePositionMeters = _player.transform.position, capsuleRotation = _player.transform.rotation,
                capsuleVelocityMetersPerSecond = _player.CharacterController.velocity,
                observedMoveInput = _player.InputReader.Move, observedSprintHeld = _player.InputReader.SprintHeld,
                observedLookInput = _player.InputReader.Look, cameraOrbitHorizontalDegrees = _orbitalFollow.HorizontalAxis.Value,
                observedJumpHeld = _player.InputReader.JumpHeld, observedDiveHeld = _player.InputReader.DiveRollHeld,
                observedPlayerState = _stateMachine.CurrentStateId.ToString(),
                animatorBaseStateHash = state.fullPathHash, animatorNormalizedTime = state.normalizedTime,
                animatorClips = string.Join(",", clips), animatorDeltaPositionMeters = animator.deltaPosition,
                animatorDeltaRotation = animator.deltaRotation, grounded = _player.IsGrounded,
                cameraPositionMeters = camera != null ? camera.transform.position : Vector3.zero,
                cameraRotation = camera != null ? camera.transform.rotation : Quaternion.identity
            });
        }
        catch (Exception exception) { _capture.errors.Add(exception.ToString()); Finish(); }
    }
    private static void ProvideInput()
    {
        if (InputState.currentUpdateType != InputUpdateType.Dynamic) return;
        if (_keyboard != null) InputSystem.QueueStateEvent(_keyboard, new KeyboardState(_keys));
        if (_mouse != null) InputSystem.QueueStateEvent(_mouse, new MouseState { delta = _mouseDelta });
    }
    private static void RecordError(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (SessionState.GetString(Prefix + "Phase", "") == "running")
        {
            if (_capture != null) _capture.errors.Add(message + "\n" + stack);
        }
        else
        {
            Completion completion = JsonUtility.FromJson<Completion>(SessionState.GetString(Prefix + "Completion", "{}"));
            completion.errors.Add(message + "\n" + stack);
            SessionState.SetString(Prefix + "Completion", JsonUtility.ToJson(completion));
        }
    }
    private static void CleanupInput()
    {
        InputSystem.onBeforeUpdate -= ProvideInput;
        if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
        _keyboard = null;
        if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
        _mouse = null;
        foreach (InputDevice device in DisabledDevices) if (device.added) InputSystem.EnableDevice(device);
        DisabledDevices.Clear();
        if (_previousKeyboard != null && _previousKeyboard.added) _previousKeyboard.MakeCurrent();
        _previousKeyboard = null;
        if (_previousMouse != null && _previousMouse.added) _previousMouse.MakeCurrent();
        _previousMouse = null;
        if (_originalInputSettings != null) InputSystem.settings = _originalInputSettings;
        if (_captureInputSettings != null) UnityEngine.Object.DestroyImmediate(_captureInputSettings);
        _captureInputSettings = null; _originalInputSettings = null;
        string json = SessionState.GetString(Prefix + "SavedEditor", "");
        if (json != "") RestoreTime(JsonUtility.FromJson<SavedEditor>(json));
    }
    private static void RestoreTime(SavedEditor saved)
    {
        Time.captureDeltaTime = saved.captureDeltaTime; Time.timeScale = saved.timeScale;
        Time.fixedDeltaTime = saved.fixedDeltaTime; Application.runInBackground = saved.runInBackground;
    }
    private static void Finish()
    {
        if (SessionState.GetString(Prefix + "Phase", "") != "running") return;
        SessionState.SetString(Prefix + "Phase", "finished");
        EditorApplication.update -= Observe;
        try { CleanupInput(); }
        catch (Exception exception) { RecordError(exception.Message, exception.StackTrace, LogType.Exception); }
        if (!_move || !_sprint || !_jump || !_dive)
            _capture.errors.Add("Capture incomplete: requested movement/action input was never observed by PlayerInputReader.");
        if (!_jumpState || !_diveState)
            _capture.errors.Add("Capture incomplete: Jump/DiveRoll gameplay state was never observed.");
        if (!_lookObserved || _lookMaxChangeDegrees <= 1f)
            _capture.errors.Add("Capture incomplete: free-camera Look input and orbital change greater than one degree were not both observed.");
        string output = SessionState.GetString(Prefix + "Output", "");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            using (var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(_capture, true));
            Debug.Log($"Baseline captured {_capture.samples.Count} observations and {_capture.errors.Count} errors: {output}");
        }
        catch (Exception exception) { _capture.errors.Add(exception.ToString()); Debug.LogException(exception); }
        int exitCode = _capture.errors.Count == 0 ? 0 : 2;
        SessionState.SetInt(Prefix + "ExitCode", exitCode);
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
        else EditorApplication.delayCall += CompleteRestore;
    }
    private static void CompleteRestore()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += CompleteRestore;
            return;
        }
        bool batch = SessionState.GetBool(Prefix + "Batch", false);
        string output = SessionState.GetString(Prefix + "Output", "");
        bool restored = false;
        try { CleanupInput(); RestoreEditor(); restored = true; }
        catch (Exception exception)
        {
            // Keep the saved setup for explicit recovery; never discard dirty user state.
            RecordError(exception.Message, exception.StackTrace, LogType.Exception);
            SessionState.SetString(Prefix + "Phase", "recovery");
            Debug.LogWarning("Baseline restoration needs manual recovery; saved Editor setup retained in SessionState.");
        }
        Completion completion = JsonUtility.FromJson<Completion>(SessionState.GetString(Prefix + "Completion", "{}"));
        completion.sampleOutput = output;
        completion.completedUtc = DateTime.UtcNow.ToString("o");
        completion.restored = restored;
        completion.succeeded = restored && completion.errors.Count == 0 && SessionState.GetInt(Prefix + "ExitCode", 2) == 0;
        try
        {
            using (var stream = new FileStream(Path.ChangeExtension(output, "completion.json"), FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(completion, true));
        }
        finally { Application.logMessageReceived -= RecordError; }
        if (batch) EditorApplication.Exit(completion.succeeded ? 0 : 2);
    }
    private static void RestoreEditor()
    {
        EditorApplication.update -= Observe;
        string json = SessionState.GetString(Prefix + "SavedEditor", "");
        if (json != "")
        {
            SavedEditor saved = JsonUtility.FromJson<SavedEditor>(json);
            var setup = new List<SceneSetup>();
            foreach (SavedScene scene in saved.scenes)
                setup.Add(new SceneSetup { path = scene.path, isLoaded = scene.loaded, isActive = scene.active });
            SceneSetup[] current = EditorSceneManager.GetSceneManagerSetup();
            bool sameSetup = current.Length == setup.Count;
            for (int index = 0; sameSetup && index < current.Length; index++)
                sameSetup = current[index].path == setup[index].path && current[index].isLoaded == setup[index].isLoaded && current[index].isActive == setup[index].isActive;
            if (!sameSetup)
            {
                for (int index = 0; index < UnityEngine.SceneManagement.SceneManager.sceneCount; index++)
                    if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(index).isDirty)
                        throw new InvalidOperationException("Refusing to restore over a dirty scene; saved setup retained for recovery.");
                EditorSceneManager.RestoreSceneManagerSetup(setup.ToArray());
            }
            var selection = new List<UnityEngine.Object>();
            foreach (string identifier in saved.selection)
                if (GlobalObjectId.TryParse(identifier, out GlobalObjectId id))
                {
                    UnityEngine.Object item = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
                    if (item != null) selection.Add(item);
                }
            Selection.objects = selection.ToArray(); RestoreTime(saved); EditorApplication.isPaused = saved.paused;
        }
        SessionState.SetString(Prefix + "Phase", "");
        SessionState.SetString(Prefix + "SavedEditor", "");
    }
}
