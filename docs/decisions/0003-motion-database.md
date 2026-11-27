# ADR 0003: Share one motion dataset across classical and neural matching

Date: 2026-11-24

Status: Accepted for M2 implementation; completion requires recorded Editor bake, reload, visualization and test evidence.

## Context

The product is a playable locomotion demo. M3 uses KD-tree as the primary matching backend with brute force as its correctness reference. Later MLP comparisons need consistent data and gameplay scenarios. Saving only 27 matching values would discard useful playback/pose targets; implementing a full learned system now would exceed M2.

## Decision

Keep one versioned dataset containing the precise 27D matching layout and auxiliary root, joint, pose and provenance data. Core owns engine-independent math, packed arrays, normalization and binary serialization. Runtime owns the Unity asset wrapper and references. Editor owns Humanoid sampling, source checks, baking and inspection. Preserve the five existing assemblies.

Sample an isolated Editor character through a manual graph with root motion enabled. Read the evaluated Animator transform instead of treating Hips as the motion root. Compare with independent AnimatorController evaluation. These offline graphs do not replace gameplay Animator, jump, dive roll or CharacterMotor ownership.

Use configurable frequency, initially 60 Hz, and three horizons in seconds, initially 20/60, 40/60 and 1. Matching features use current root yaw with X right/Y up/Z forward. Differentiate positions before rotating velocity. Retain full root quaternions/angular velocities and all mapped Humanoid parent-local bone positions/rotations. Save tags, validity, contact heuristics, exact normalization parameters and source hashes. The detailed contract is [FEATURE_SCHEMA.md](../data/FEATURE_SCHEMA.md).

Store raw and normalized arrays in a portable binary payload held by a ScriptableObject. Validate actual source identities/content in the Editor before save/reload. Preserve actual standard deviations separately from epsilon-clamped scales and compute statistics from eligible rows. Future neural export must define a training partition and fit normalization on that partition alone.

## Consequences and limits

Both classical backends consume identical rows/eligibility rules. Neural export reuses samples, but prediction targets and extra labels remain future decisions. An MLP is not the full Learned Motion Matching system.

Loops require an aligned sample grid and explicit root endpoint, preserving cycle translation/yaw across boundaries. Missing nonloop horizons mark tail rows ineligible. Source seams and motion coverage still need visual checks. Contact labels are height/speed heuristics. Avatar identity and unit world scale are enforced; arbitrary retargeting and other scale conventions require separate validation.

Mapped local bone poses do not constitute a full rig/muscle/hierarchy exchange format. Source/meta and Unity version hashes detect stale inputs but do not establish cross-platform bitwise portability. M2 completion requires measured sampling/serialization evidence. Runtime matching, gameplay Playables, foot locking, neural training and inference remain later milestones.
