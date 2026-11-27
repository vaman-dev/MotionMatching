using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MotionMatching.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MotionMatching.Editor
{
    /// <summary>Editor-only evaluated Humanoid sampling in an isolated preview scene.</summary>
    public static class MotionAnimationSampler
    {
        public static MotionClipData Sample(GameObject character, Avatar avatar, AnimationClip clip,
            MotionBakeSettings settings, string tag, bool loop)
        {
            ValidateInput(character, avatar, clip, settings);
            double intervals = (double)clip.length * settings.SampleRate;
            if (loop && Math.Abs(intervals - Math.Round(intervals)) > 0.001)
                throw new ArgumentException($"Loop '{clip.name}' duration must align to the sample grid. Choose a compatible sample rate or bake as nonlooping.");
            int count = loop ? (int)Math.Round(intervals) : (int)Math.Floor(intervals + 0.00001) + 1;
            if (count < 2 || count > 1000000) throw new ArgumentException("Clip must have 2 to 1,000,000 samples.");
            using (var rig = new SamplingRig(character, avatar))
            {
                var graph = PlayableGraph.Create("Motion Database Sampling");
                try
                {
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var playable = AnimationClipPlayable.Create(graph, clip);
                    playable.SetApplyFootIK(false);
                    playable.SetApplyPlayableIK(false);
                    AnimationPlayableOutput.Create(graph, "Humanoid", rig.Animator).SetSourcePlayable(playable);
                    graph.Play(); graph.Evaluate(0);
                    var initialPosition = rig.Animator.transform.position;
                    var initialRotation = rig.Animator.transform.rotation;
                    var inverse = Quaternion.Inverse(initialRotation);
                    var bones = Enum.GetValues(typeof(HumanBodyBones)).Cast<HumanBodyBones>()
                        .Where(b => b != HumanBodyBones.LastBone && rig.Animator.GetBoneTransform(b) != null).ToArray();
                    var transforms = bones.Select(rig.Animator.GetBoneTransform).ToArray();
                    var data = new MotionClipData {
                        Name = clip.name, Guid = ClipIdentity(clip), SourceHash = SourceHash(character, avatar, clip),
                        Tag = tag ?? "", Duration = clip.length, IsLoop = loop, Samples = new MotionSample[count],
                        BoneNames = bones.Select(b => b.ToString()).ToArray(),
                        LocalPosePositions = new float[count * bones.Length * 3],
                        LocalPoseRotations = new float[count * bones.Length * 4]
                    };
                    double evaluated = 0;
                    for (int frame = 0; frame < count; frame++)
                    {
                        double target = Math.Min(clip.length, frame / (double)settings.SampleRate);
                        if (frame > 0) graph.Evaluate((float)(target - evaluated));
                        evaluated = target;
                        data.Samples[frame] = new MotionSample {
                            RootPosition = V(inverse * (rig.Animator.transform.position - initialPosition)),
                            RootRotation = Q(inverse * rig.Animator.transform.rotation),
                            PelvisPosition = V(inverse * (rig.Hips.position - initialPosition)),
                            LeftFootPosition = V(inverse * (rig.LeftFoot.position - initialPosition)),
                            RightFootPosition = V(inverse * (rig.RightFoot.position - initialPosition))
                        };
                        for (int b = 0; b < bones.Length; b++)
                        {
                            int p = (frame * bones.Length + b) * 3, q = (frame * bones.Length + b) * 4;
                            var position = transforms[b].localPosition; var rotation = transforms[b].localRotation;
                            data.LocalPosePositions[p] = position.x; data.LocalPosePositions[p+1] = position.y; data.LocalPosePositions[p+2] = position.z;
                            data.LocalPoseRotations[q] = rotation.x; data.LocalPoseRotations[q+1] = rotation.y;
                            data.LocalPoseRotations[q+2] = rotation.z; data.LocalPoseRotations[q+3] = rotation.w;
                        }
                    }
                    if (clip.length > evaluated) graph.Evaluate((float)(clip.length - evaluated));
                    data.EndRootPosition = V(inverse * (rig.Animator.transform.position - initialPosition));
                    data.EndRootRotation = Q(inverse * rig.Animator.transform.rotation);
                    return data;
                }
                finally { if (graph.IsValid()) graph.Destroy(); }
            }
        }

        /// <summary>Independent AnimatorController/Animator.Update root path for sampler parity checks.</summary>
        public static Vector3[] SampleControllerRoot(GameObject character, Avatar avatar, AnimationClip clip, float sampleRate)
            => SampleControllerRoot(character, avatar, clip, sampleRate, out _);

        public static Vector3[] SampleControllerRoot(GameObject character, Avatar avatar, AnimationClip clip, float sampleRate, out Quaternion[] rotations)
        {
            ValidateInput(character, avatar, clip, new MotionBakeSettings { SampleRate = sampleRate });
            using (var rig = new SamplingRig(character, avatar))
            {
                var controller = new AnimatorController();
                controller.AddLayer("Sampling");
                var state = controller.AddMotion(clip, 0);
                var machine = controller.layers[0].stateMachine;
                machine.defaultState = state;
                try
                {
                    rig.Animator.runtimeAnimatorController = controller;
                    rig.Animator.Rebind(); rig.Animator.Update(0);
                    rig.Animator.Play(state.nameHash, 0, 0); rig.Animator.Update(0);
                    var initial = rig.Animator.transform.position;
                    var inverse = Quaternion.Inverse(rig.Animator.transform.rotation);
                    int n = (int)Math.Round((double)clip.length * sampleRate);
                    var result = new Vector3[n+1];
                    rotations = new Quaternion[n+1]; rotations[0] = Quaternion.identity;
                    for (int i=1; i<=n; i++) {
                        rig.Animator.Update(1/sampleRate);
                        result[i] = inverse * (rig.Animator.transform.position - initial);
                        rotations[i] = inverse * rig.Animator.transform.rotation;
                    }
                    return result;
                }
                finally {
                    rig.Animator.runtimeAnimatorController = null;
                    Object.DestroyImmediate(state); Object.DestroyImmediate(machine); Object.DestroyImmediate(controller);
                }
            }
        }

        /// <summary>Independent controller evaluation: compare rebased roots and mapped parent-local bones.</summary>
        public static MotionPoseComparison CompareControllerPose(GameObject character, Avatar avatar, AnimationClip clip,
            MotionClipData baked, MotionBakeSettings settings, int[] frames)
        {
            ValidateInput(character, avatar, clip, settings);
            if (baked == null || frames == null || frames.Length == 0 || frames.Any(f => f < 0 || f >= baked.Samples.Length))
                throw new ArgumentException("Choose valid baked sample indices for controller parity.");
            using (var rig = new SamplingRig(character, avatar))
            {
                var controller = new AnimatorController(); controller.AddLayer("Parity");
                var state = controller.AddMotion(clip, 0); var machine = controller.layers[0].stateMachine;
                machine.defaultState = state;
                try
                {
                    rig.Animator.runtimeAnimatorController = controller; rig.Animator.Rebind(); rig.Animator.Update(0);
                    rig.Animator.Play(state.nameHash, 0, 0); rig.Animator.Update(0);
                    var origin = rig.Animator.transform.position; var inverse = Quaternion.Inverse(rig.Animator.transform.rotation);
                    var bones = baked.BoneNames.Select(name => rig.Animator.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), name))).ToArray();
                    var result = new MotionPoseComparison();
                    int last = frames.Max(); double previous = 0;
                    for (int frame = 0; frame <= last; frame++)
                    {
                        double time = frame / (double)settings.SampleRate;
                        if (frame > 0) rig.Animator.Update((float)(time - previous)); previous = time;
                        if (Array.IndexOf(frames, frame) < 0) continue;
                        var sample = baked.Samples[frame];
                        result.MaxRootPositionError = Mathf.Max(result.MaxRootPositionError, Vector3.Distance(inverse * (rig.Animator.transform.position - origin), MotionPreviewSession.V(sample.RootPosition)));
                        result.MaxRootAngleDegrees = Mathf.Max(result.MaxRootAngleDegrees, Quaternion.Angle(inverse * rig.Animator.transform.rotation, MotionPreviewSession.Q(sample.RootRotation)));
                        for (int b = 0; b < bones.Length; b++)
                        {
                            int p = (frame * bones.Length + b) * 3, q = (frame * bones.Length + b) * 4;
                            var position = new Vector3(baked.LocalPosePositions[p], baked.LocalPosePositions[p + 1], baked.LocalPosePositions[p + 2]);
                            var rotation = new Quaternion(baked.LocalPoseRotations[q], baked.LocalPoseRotations[q + 1], baked.LocalPoseRotations[q + 2], baked.LocalPoseRotations[q + 3]);
                            result.MaxBonePositionError = Mathf.Max(result.MaxBonePositionError, Vector3.Distance(position, bones[b].localPosition));
                            result.MaxBoneAngleDegrees = Mathf.Max(result.MaxBoneAngleDegrees, Quaternion.Angle(rotation, bones[b].localRotation));
                        }
                    }
                    return result;
                }
                finally
                {
                    rig.Animator.runtimeAnimatorController = null;
                    Object.DestroyImmediate(state); Object.DestroyImmediate(machine); Object.DestroyImmediate(controller);
                }
            }
        }

        public static void ValidateInput(GameObject character, Avatar avatar, AnimationClip clip, MotionBakeSettings settings)
        {
            if (character == null || !EditorUtility.IsPersistent(character)) throw new ArgumentException("Select a character model/prefab asset, not a scene instance.");
            if (avatar == null || !avatar.isHuman || !avatar.isValid) throw new ArgumentException("Select a valid Humanoid Avatar.");
            if (clip == null || !EditorUtility.IsPersistent(clip) || clip.legacy || !clip.humanMotion || !float.IsFinite(clip.length) || clip.length <= 0)
                throw new ArgumentException("Select a nonlegacy, finite, nonempty Humanoid animation asset.");
            if (settings == null || !float.IsFinite(settings.SampleRate) || settings.SampleRate < 1 || settings.SampleRate > 240)
                throw new ArgumentException("Sampling frequency must be between 1 and 240 Hz.");
            var animators = character.GetComponentsInChildren<Animator>(true);
            if (animators.Length != 1) throw new ArgumentException("Character must contain exactly one Animator.");
            if (animators[0].avatar != avatar) throw new ArgumentException("Avatar must be the selected character's bound Avatar; retargeting to another skeleton requires separate validation.");
            if (character.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new ArgumentException("Select the source model or a character prefab without gameplay/ExecuteAlways scripts for isolated sampling.");
        }

        public static string ClipIdentity(AnimationClip clip)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string guid, out long id);
            return guid + ":" + id;
        }

        public static string SourceHash(GameObject character, Avatar avatar, AnimationClip clip)
        {
            var paths = new[] {AssetDatabase.GetAssetPath(character), AssetDatabase.GetAssetPath(avatar), AssetDatabase.GetAssetPath(clip)}
                .Distinct().OrderBy(p => p, StringComparer.Ordinal);
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Application.unityVersion); writer.Write(ClipIdentity(clip));
                foreach (var path in paths) {
                    writer.Write(path); writer.Write(AssetDatabase.GetAssetDependencyHash(path).ToString());
                    foreach(var source in new[]{path,path+".meta"}) {
                        var bytes=File.ReadAllBytes(source); using(var sha=SHA256.Create()) writer.Write(sha.ComputeHash(bytes));
                    }
                }
                writer.Flush(); using (var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }

        static MotionVector V(Vector3 v) => new MotionVector(v.x,v.y,v.z);
        static MotionRotation Q(Quaternion q) => new MotionRotation(q.x,q.y,q.z,q.w);

        internal sealed class SamplingRig : IDisposable
        {
            readonly Scene scene; readonly GameObject instance;
            public GameObject Instance => instance;
            public Animator Animator {get;}
            public Transform Hips {get;}
            public Transform LeftFoot {get;}
            public Transform RightFoot {get;}
            public SamplingRig(GameObject model, Avatar avatar)
            {
                scene = EditorSceneManager.NewPreviewScene();
                try {
                    instance = Object.Instantiate(model); instance.name="Motion Sampling Preview";
                    SceneManager.MoveGameObjectToScene(instance, scene);
                    instance.hideFlags = HideFlags.HideAndDontSave;
                    foreach(var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
                    Animator = instance.GetComponentInChildren<Animator>(true);
                    Animator.gameObject.SetActive(true); instance.SetActive(true);
                    Animator.avatar=avatar; Animator.runtimeAnimatorController=null;
                    Animator.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                    if ((Animator.transform.lossyScale-Vector3.one).sqrMagnitude>0.000001f) throw new ArgumentException("Character Animator must have unit world scale; bake source importer scale instead.");
                    Animator.enabled=true; Animator.applyRootMotion=true; Animator.fireEvents=false;
                    Animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    Hips=Animator.GetBoneTransform(HumanBodyBones.Hips);LeftFoot=Animator.GetBoneTransform(HumanBodyBones.LeftFoot);RightFoot=Animator.GetBoneTransform(HumanBodyBones.RightFoot);
                    if (Hips==null||LeftFoot==null||RightFoot==null)throw new ArgumentException("Avatar requires Hips, LeftFoot and RightFoot.");
                } catch { Dispose();throw; }
            }
            public void Dispose() { if(instance!=null)Object.DestroyImmediate(instance);if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene); }
        }
    }

    public static class MotionDatabaseBaker
    {
        public static void ValidateSources(MotionDatabaseData data, AnimationClip[] clips, GameObject character, Avatar avatar)
        {
            MotionDatabaseBinary.ValidateData(data);
            if(clips==null||clips.Length!=data.Clips.Length)throw new ArgumentException("Source reference count differs from the baked database.");
            for(int i=0;i<clips.Length;i++) {
                MotionAnimationSampler.ValidateInput(character,avatar,clips[i],data.Settings);
                if(MotionAnimationSampler.ClipIdentity(clips[i])!=data.Clips[i].Guid)
                    throw new ArgumentException($"Source clip identity mismatch at index {i}; rebake instead of replacing references.");
                if(MotionAnimationSampler.SourceHash(character,avatar,clips[i])!=data.Clips[i].SourceHash)
                    throw new ArgumentException($"Source/import/Avatar content changed for '{clips[i].name}'; rebake the database.");
            }
        }

        public static void Store(MotionMatching.Runtime.MotionDatabaseAsset asset, MotionDatabaseData data,
            AnimationClip[] clips, GameObject character, Avatar avatar)
        {
            if(asset==null)throw new ArgumentNullException(nameof(asset));
            ValidateSources(data,clips,character,avatar);
            asset.SetData(data,clips,character,avatar);
        }

        public static MotionDatabaseData Bake(GameObject character, Avatar avatar, AnimationClip[] clips,
            MotionBakeSettings settings, string[] tags, bool[] loops)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Bake in Edit Mode.");
            if(clips==null||clips.Length==0||tags==null||loops==null||tags.Length!=clips.Length||loops.Length!=clips.Length)
                throw new ArgumentException("Provide clips with matching tag/loop arrays.");
            if(clips.Distinct().Count()!=clips.Length)throw new ArgumentException("Duplicate clip references are not allowed.");
            var sampled=new MotionClipData[clips.Length];
            for(int i=0;i<clips.Length;i++)sampled[i]=MotionAnimationSampler.Sample(character,avatar,clips[i],settings,tags[i],loops[i]);
            return MotionDatabaseBuilder.Build(sampled,settings);
        }
    }
}
