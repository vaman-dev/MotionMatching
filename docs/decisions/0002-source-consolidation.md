# ADR-0002 - one project-owned source root

Date: 2026-11-23. Status: consolidation implemented; fresh validation tracked in [PROGRESS](../PROGRESS.md).

## Context

M1 introduced named MotionMatching assemblies while existing gameplay scripts remained in Assembly-CSharp. Keeping two authoritative source locations obscures ownership. Moving only the motor folder would preserve that dependency split. Removing every shooter-named script would break references in the retained original scene, prefab and recovery scene.

## Decision

Move all project-owned C# and their metadata into `Assets/Scripts/MotionMatching`, inside the existing five assemblies. Keep class names, namespaces and MonoScript GUIDs. Compiled assembly ownership intentionally changes from predefined assemblies to the named Runtime/Editor assemblies; no project SerializeReference payloads were found. Verify component resolution against preserved scenes rather than rewriting their diagnostic class identifiers. Put engine-independent buffering in Core; input, character, motor, states, animation and camera in Runtime; tooling in Editor; tests in EditMode/PlayMode subdirectories.

Retain referenced shooter compatibility under Runtime/LegacyShooter. Runtime explicitly references Animation Rigging because this retained code uses it. MotionMatchingTest remains unarmed. No additional assembly, gameplay motor, animation writer, search algorithm or neural placeholder is introduced.

Move the existing animation source subtree into `Assets/Animations/MotionMatching/Source` and the dedicated controller beside it. Preserve every moved asset's metadata and avoid duplicating clips. Imported/vendor assets outside that subtree stay in place.

Centralize live project documents in docs. Keep AGENTS/README and agent/skill/workflow discovery locations unchanged. Move Report byte-for-byte: its original relative links describe a historical snapshot, and current ownership is documented externally rather than rewritten into that report.

## Consequences and validation

The original scene/prefab compatibility prevents safe deletion of the retained shooter scripts during this milestone. Future retirement must remove all serialized and code dependencies deliberately. Folder consolidation is not evidence of dependency correctness: verify the acyclic assembly graph, import/compilation, scene and Animator references, tests and recorded locomotion behavior after the move.

The previous seven EditMode, six PlayMode and twenty Python passes are a baseline only. Current results and remaining limits belong in PROGRESS; source/reference classifications are in [the audit](../migration/CONSOLIDATION_AUDIT.md). This milestone does not authorize a commit, push or PR.
