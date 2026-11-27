using System;
using MotionMatching.Core;
using UnityEngine;

namespace MotionMatching.Runtime
{
    /// <summary>Unity references plus the portable, versioned motion database payload.</summary>
    [CreateAssetMenu(menuName = "Motion Matching/Motion Database", fileName = "MotionDatabase")]
    public sealed class MotionDatabaseAsset : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector] private byte[] payload = Array.Empty<byte>();
        [SerializeField, HideInInspector] private AnimationClip[] sourceClips = Array.Empty<AnimationClip>();
        [SerializeField, HideInInspector] private GameObject character;
        [SerializeField, HideInInspector] private Avatar avatar;
        [SerializeField] private string engineVersion;
        [SerializeField, TextArea] private string provenance;
        [NonSerialized] private MotionDatabaseData cachedData;

        public AnimationClip[] SourceClips => (AnimationClip[])sourceClips.Clone();
        public GameObject Character => character;
        public Avatar Avatar => avatar;
        public string EngineVersion => engineVersion;
        public string Provenance => provenance;
        public int PayloadBytes => payload?.Length ?? 0;
        public MotionDatabaseData Data
        {
            get
            {
                if (cachedData == null)
                {
                    if (payload == null || payload.Length == 0)
                        throw new InvalidOperationException("This motion database has no baked payload.");
                    cachedData = MotionDatabaseBinary.Deserialize(payload);
                }
                return cachedData;
            }
        }

        /// <summary>
        /// Stores a validated portable payload. Editor tooling must additionally validate the
        /// source GUIDs, subasset identities and content hashes before calling this low-level API.
        /// </summary>
        public void SetData(MotionDatabaseData data, AnimationClip[] clips, GameObject model, Avatar humanoidAvatar)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (clips == null) throw new ArgumentNullException(nameof(clips));
            if (model == null || humanoidAvatar == null || !humanoidAvatar.isValid || !humanoidAvatar.isHuman)
                throw new ArgumentException("A character and a valid Humanoid Avatar are required.");
            if (data.Clips == null || clips.Length != data.Clips.Length)
                throw new ArgumentException("Source clip references must correspond to the baked clip records.");
            for (int i = 0; i < clips.Length; i++)
                if (clips[i] == null) throw new ArgumentException("Source clip references cannot be null.");

            // Round-trip through portable validation before any asset state is changed.
            byte[] encoded = MotionDatabaseBinary.Serialize(data);
            MotionDatabaseData verified = MotionDatabaseBinary.Deserialize(encoded);
            payload = encoded;
            cachedData = verified;
            sourceClips = (AnimationClip[])clips.Clone();
            character = model;
            avatar = humanoidAvatar;
            engineVersion = Application.unityVersion;
            provenance = "Editor Humanoid animation sampling; source identities and extraction schema are stored in the payload.";
        }

        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize() { cachedData = null; }
    }
}
