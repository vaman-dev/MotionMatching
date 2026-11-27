using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Unity.Collections;

namespace MotionMatching.Runtime
{
    /// <summary>
    /// Owns the Animator's single animation output. Call Update in MonoBehaviour.Update,
    /// route parameters through Controller, and read source root motion in OnAnimatorMove.
    /// The graph evaluates automatically in GameTime; this type never moves the character.
    /// </summary>
    public sealed class MotionPlayablePlayer : IDisposable
    {
        private readonly Animator _animator;
        private readonly RuntimeAnimatorController _originalController;
        private readonly bool _originalApplyRootMotion;
        private PlayableGraph _graph;
        private AnimationMixerPlayable _rootMixer;
        private AnimationMixerPlayable _clipMixer;
        private readonly AnimationClipPlayable[] _clips = new AnimationClipPlayable[2];
        private readonly AnimationScriptPlayable[] _rootReaders = new AnimationScriptPlayable[2];
        private NativeArray<MotionRootSample> _rootSamples;
        private MotionInertializer _inertializer;
        private readonly MotionMatchingSettings _settings;
        private int _activeSlot = -1;
        private int _outgoingSlot = -1;
        private float _blendElapsedSeconds;
        private float _blendDurationSeconds;
        private bool _disposed;

        public AnimatorControllerPlayable Controller { get; private set; }
        public bool IsMatching { get; private set; }
        public double CurrentTimeSeconds => _activeSlot >= 0 && _clips[_activeSlot].IsValid() ? _clips[_activeSlot].GetTime() : 0;
        public bool IsBlending => _outgoingSlot >= 0;
        public int SeekCount { get; private set; }
        public float ActiveWeight => _activeSlot >= 0 ? _clipMixer.GetInputWeight(_activeSlot) : 0;
        public PlayableGraph Graph => _graph;
        public AnimationMixerPlayable MatchedMixer => _clipMixer;
        public AnimationMixerPlayable RootMixer => _rootMixer;
        public MotionInertialStatistics InertialStatistics => _inertializer?.Statistics ?? default;
        public void CaptureDisplayedPose(float deltaSeconds)
        {
            ThrowIfDisposed();
            if (!IsFinite(deltaSeconds) || deltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            _inertializer?.CaptureDisplayedPose(deltaSeconds);
        }

        public MotionPlayablePlayer(Animator animator, MotionMatchingSettings settings = null)
        {
            _settings = settings;
            if (animator == null) throw new ArgumentNullException(nameof(animator));
            if (animator.runtimeAnimatorController == null)
                throw new ArgumentException("Motion playback requires the character's original Animator Controller.", nameof(animator));
            if (animator.updateMode != AnimatorUpdateMode.Normal)
                throw new ArgumentException("Motion playback requires Animator Normal update mode before LateUpdate.", nameof(animator));

            _animator = animator;
            _originalController = animator.runtimeAnimatorController;
            _originalApplyRootMotion = animator.applyRootMotion;
            // Capture before detaching the Animator's controller. An initialized
            // character may already be midway through a jump/dive when enabled.
            AnimatorStateInfo[] initialStates = null;
            float[] initialLayerWeights = null;
            ControllerParameterSnapshot[] initialParameters = null;
            if (animator.isInitialized)
            {
                initialStates = new AnimatorStateInfo[animator.layerCount];
                initialLayerWeights = new float[animator.layerCount];
                for (int layer = 0; layer < initialStates.Length; layer++)
                {
                    initialStates[layer] = animator.GetCurrentAnimatorStateInfo(layer);
                    initialLayerWeights[layer] = animator.GetLayerWeight(layer);
                }
                var animatorParameters = animator.parameters;
                initialParameters = new ControllerParameterSnapshot[animatorParameters.Length];
                for (int index = 0; index < initialParameters.Length; index++)
                {
                    var parameter = animatorParameters[index];
                    var snapshot = new ControllerParameterSnapshot { Hash = parameter.nameHash, Type = parameter.type };
                    switch (parameter.type)
                    {
                        case AnimatorControllerParameterType.Float: snapshot.FloatValue = animator.GetFloat(snapshot.Hash); break;
                        case AnimatorControllerParameterType.Int: snapshot.IntValue = animator.GetInteger(snapshot.Hash); break;
                        case AnimatorControllerParameterType.Bool: snapshot.BoolValue = animator.GetBool(snapshot.Hash); break;
                        // Pending triggers cannot be read, so neither handoff preserves them.
                    }
                    initialParameters[index] = snapshot;
                }
            }
            try
            {
                _graph = PlayableGraph.Create("Motion Matching Playback");
                _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                _rootMixer = AnimationMixerPlayable.Create(_graph, 2);
                _clipMixer = AnimationMixerPlayable.Create(_graph, 2);
                _rootSamples = new NativeArray<MotionRootSample>(2, Allocator.Persistent);
                // Traverse both branches, including the zero-weight action controller.
                _rootMixer.SetTraversalMode(PlayableTraversalMode.Mix);
                _clipMixer.SetTraversalMode(PlayableTraversalMode.Mix);
                Controller = AnimatorControllerPlayable.Create(_graph, _originalController);
                Controller.SetPropagateSetTime(false);
                _graph.Connect(Controller, 0, _rootMixer, 0);
                _graph.Connect(_clipMixer, 0, _rootMixer, 1);
                _rootMixer.SetInputWeight(0, 1f);
                _rootMixer.SetInputWeight(1, 0f);

                // The original state machine lives inside this graph, never alongside it.
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = true;
                var output = AnimationPlayableOutput.Create(_graph, "Character Animation", animator);
                output.SetSourcePlayable(_rootMixer);
                if (settings != null)
                {
                    _inertializer = new MotionInertializer(_graph, animator, _rootMixer);
                    _inertializer.Configure(false, settings.PoseOffsetHalfLife, settings.RotationOffsetHalfLife);
                    output.SetSourcePlayable(_inertializer.Playable);
                }
                _graph.Play();
                if (initialParameters != null)
                {
                    foreach (var parameter in initialParameters)
                    {
                        switch (parameter.Type)
                        {
                            case AnimatorControllerParameterType.Float: Controller.SetFloat(parameter.Hash, parameter.FloatValue); break;
                            case AnimatorControllerParameterType.Int: Controller.SetInteger(parameter.Hash, parameter.IntValue); break;
                            case AnimatorControllerParameterType.Bool: Controller.SetBool(parameter.Hash, parameter.BoolValue); break;
                        }
                    }
                    int layerCount = Math.Min(initialStates.Length, Controller.GetLayerCount());
                    for (int layer = 0; layer < layerCount; layer++)
                    {
                        Controller.SetLayerWeight(layer, initialLayerWeights[layer]);
                        var state = initialStates[layer];
                        if (state.fullPathHash != 0 && IsFinite(state.normalizedTime) && Controller.HasState(layer, state.fullPathHash))
                            Controller.Play(state.fullPathHash, layer, state.normalizedTime);
                    }
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// Selects a source clip at seconds from its start. Loop must match the clip's
        /// imported loop flag; native clip looping preserves continuous root motion.
        /// An interrupted blend retains its heavier slot as the new outgoing pose.
        /// </summary>
        public void Play(AnimationClip clip, float timeSeconds, bool loop, float blendSeconds)
        {
            ThrowIfDisposed();
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            if (clip.legacy || clip.length <= 0f)
                throw new ArgumentException("Motion playback requires a non-legacy clip with positive duration.", nameof(clip));
            if (clip.isLooping != loop)
                throw new ArgumentException("The requested loop policy must match the clip's imported Loop Time setting.", nameof(loop));
            if (!IsFinite(timeSeconds) || timeSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(timeSeconds));
            if (!IsFinite(blendSeconds) || blendSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(blendSeconds));
            bool inertialize = _settings != null && _settings.InertializationEnabled && _inertializer != null;
            _inertializer?.Configure(inertialize, _settings.PoseOffsetHalfLife, _settings.RotationOffsetHalfLife);
            if (inertialize) { _inertializer.Begin(clip, timeSeconds); blendSeconds = 0; }

            // With two slots an interrupted blend cannot retain all three source poses.
            int outgoing = _activeSlot;
            if (_outgoingSlot >= 0 && _clipMixer.GetInputWeight(_outgoingSlot) > _clipMixer.GetInputWeight(_activeSlot))
                outgoing = _outgoingSlot;
            int incoming = outgoing < 0 ? 0 : 1 - outgoing;
            DestroySlot(incoming);
            var playable = AnimationClipPlayable.Create(_graph, clip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            playable.SetPropagateSetTime(false);
            // Do not manually wrap time each lap: that would create another root seek.
            playable.SetDuration(loop ? double.PositiveInfinity : clip.length);
            double startSeconds = loop ? timeSeconds % clip.length : Mathf.Min(timeSeconds, clip.length);
            playable.SetTime(startSeconds);
            playable.SetTime(startSeconds);
            _clips[incoming] = playable;
            _rootSamples[incoming] = default;
            var reader = AnimationScriptPlayable.Create(_graph, new MotionRootStream { Samples = _rootSamples, Slot = incoming }, 1);
            _rootReaders[incoming] = reader;
            _graph.Connect(playable, 0, reader, 0); reader.SetInputWeight(0, 1);
            _graph.Connect(reader, 0, _clipMixer, incoming);
            _activeSlot = incoming;
            _outgoingSlot = outgoing;
            _blendElapsedSeconds = 0f;
            _blendDurationSeconds = blendSeconds;
            SeekCount++;

            if (outgoing < 0 || blendSeconds == 0f)
            {
                _clipMixer.SetInputWeight(incoming, 1f);
                FinishBlend();
            }
            else
            {
                _clipMixer.SetInputWeight(outgoing, 1f);
                _clipMixer.SetInputWeight(incoming, 0f);
            }
        }

        /// <summary>Switches the root output between matching and the live action controller.</summary>
        public void SetMatching(bool matching)
        {
            ThrowIfDisposed();
            if (matching && _activeSlot < 0)
                throw new InvalidOperationException("Select a matching clip before enabling matching playback.");
            if (matching == IsMatching) return;
            IsMatching = matching;
            _inertializer?.Configure(matching && _settings.InertializationEnabled, _settings.PoseOffsetHalfLife, _settings.RotationOffsetHalfLife);
            _rootMixer.SetInputWeight(0, matching ? 0f : 1f);
            _rootMixer.SetInputWeight(1, matching ? 1f : 0f);
            // Native clip clocks are seeded with both current/previous time. Never
            // discard a legitimate outgoing root step merely because a new clip starts.
        }

        /// <summary>Advances crossfade weights only; GameTime advances both clip clocks.</summary>
        public void Update(float deltaTimeSeconds)
        {
            ThrowIfDisposed();
            if (!IsFinite(deltaTimeSeconds) || deltaTimeSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTimeSeconds));
            _inertializer?.Configure(IsMatching && _settings.InertializationEnabled, _settings.PoseOffsetHalfLife, _settings.RotationOffsetHalfLife);
            if (_outgoingSlot < 0) return;
            _blendElapsedSeconds += deltaTimeSeconds;
            float weight = Mathf.Clamp01(_blendElapsedSeconds / _blendDurationSeconds);
            _clipMixer.SetInputWeight(_activeSlot, weight);
            _clipMixer.SetInputWeight(_outgoingSlot, 1f - weight);
            if (weight >= 1f) FinishBlend();
        }

        /// <summary>Compatibility for probes: correctly seeded clips no longer discard seek-frame travel.</summary>
        public bool ConsumeSeekSuppression() => false;

        /// <summary>Blend evaluated per-source root velocities, separately from skeletal pose offsets.</summary>
        public void ReadRootMotion(float deltaSeconds, out Vector3 position, out Quaternion rotation)
        {
            ThrowIfDisposed();
            if (!IsFinite(deltaSeconds) || deltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            position = Vector3.zero; Vector3 angular = Vector3.zero; float sum = 0;
            for (int slot = 0; slot < 2; slot++)
            {
                if (!_clips[slot].IsValid()) continue;
                float weight = _clipMixer.GetInputWeight(slot);
                var sample = _rootSamples[slot];
                position += sample.Velocity * weight;
                angular += sample.AngularVelocity * weight; sum += weight;
            }
            // Humanoid stream velocity is Avatar-local and normalized by humanScale.
            // Animator.deltaPosition has already undergone this conversion; source
            // streams have not. Keep yaw angular velocity local for right multiplication.
            if (sum > 0)
            {
                position = _animator.transform.TransformVector(position * _animator.humanScale) * (deltaSeconds / sum);
                angular *= deltaSeconds / sum;
            }
            float angle = angular.magnitude;
            rotation = angle > 1e-8f ? Quaternion.AngleAxis(angle * Mathf.Rad2Deg, angular / angle) : Quaternion.identity;
        }

        /// <summary>
        /// Restores the action controller's current states and readable parameters.
        /// Pending triggers and in-flight transition blends cannot be reconstructed;
        /// each layer resumes its current state at the captured normalized time.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            IsMatching = false;
            AnimatorStateInfo[] states = null;
            float[] layerWeights = null;
            ControllerParameterSnapshot[] parameters = null;
            if (_animator != null && _originalController != null && _graph.IsValid() && Controller.IsValid())
            {
                int layerCount = Controller.GetLayerCount();
                states = new AnimatorStateInfo[layerCount];
                layerWeights = new float[layerCount];
                for (int layer = 0; layer < layerCount; layer++)
                {
                    states[layer] = Controller.GetCurrentAnimatorStateInfo(layer);
                    layerWeights[layer] = Controller.GetLayerWeight(layer);
                }
                parameters = new ControllerParameterSnapshot[Controller.GetParameterCount()];
                for (int index = 0; index < parameters.Length; index++)
                {
                    var parameter = Controller.GetParameter(index);
                    var snapshot = new ControllerParameterSnapshot { Hash = parameter.nameHash, Type = parameter.type };
                    switch (parameter.type)
                    {
                        case AnimatorControllerParameterType.Float: snapshot.FloatValue = Controller.GetFloat(snapshot.Hash); break;
                        case AnimatorControllerParameterType.Int: snapshot.IntValue = Controller.GetInteger(snapshot.Hash); break;
                        case AnimatorControllerParameterType.Bool: snapshot.BoolValue = Controller.GetBool(snapshot.Hash); break;
                        // Unity exposes no way to read pending trigger state.
                    }
                    parameters[index] = snapshot;
                }
            }
            if (_graph.IsValid()) _graph.Destroy();
            _inertializer?.Dispose(); _inertializer = null;
            if (_rootSamples.IsCreated) _rootSamples.Dispose();
            Controller = default;
            if (_animator != null)
            {
                _animator.runtimeAnimatorController = _originalController;
                _animator.applyRootMotion = _originalApplyRootMotion;
                if (_originalController != null && parameters != null)
                {
                    foreach (var parameter in parameters)
                    {
                        switch (parameter.Type)
                        {
                            case AnimatorControllerParameterType.Float: _animator.SetFloat(parameter.Hash, parameter.FloatValue); break;
                            case AnimatorControllerParameterType.Int: _animator.SetInteger(parameter.Hash, parameter.IntValue); break;
                            case AnimatorControllerParameterType.Bool: _animator.SetBool(parameter.Hash, parameter.BoolValue); break;
                        }
                    }
                    int layerCount = Math.Min(states.Length, _animator.layerCount);
                    for (int layer = 0; layer < layerCount; layer++)
                    {
                        _animator.SetLayerWeight(layer, layerWeights[layer]);
                        var state = states[layer];
                        // Play is invalid once Unity has deactivated the rig (for example
                        // during scene unload). Ownership/parameters still restore; active
                        // component-only handoffs also restore the authored action state.
                        if (_animator.isActiveAndEnabled && state.fullPathHash != 0 && IsFinite(state.normalizedTime) && _animator.HasState(layer, state.fullPathHash))
                            _animator.Play(state.fullPathHash, layer, state.normalizedTime);
                    }
                    // Defer evaluation to the normal animation phase: Update(0) here
                    // would deliver an extra root-motion callback outside its schedule.
                }
            }
        }

        private struct ControllerParameterSnapshot
        {
            public int Hash;
            public AnimatorControllerParameterType Type;
            public float FloatValue;
            public int IntValue;
            public bool BoolValue;
        }

        private void FinishBlend()
        {
            if (_outgoingSlot >= 0) DestroySlot(_outgoingSlot);
            _outgoingSlot = -1;
        }

        private void DestroySlot(int slot)
        {
            if (!_clips[slot].IsValid()) return;
            _clipMixer.SetInputWeight(slot, 0f);
            _graph.Disconnect(_clipMixer, slot);
            if (_rootReaders[slot].IsValid()) _graph.DestroyPlayable(_rootReaders[slot]);
            _rootReaders[slot] = default;
            _graph.DestroyPlayable(_clips[slot]);
            _clips[slot] = default;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MotionPlayablePlayer));
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
