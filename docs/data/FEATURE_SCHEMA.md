# Motion database schema v1

This is the implemented M2 contract in `Core/MotionData.cs`; it is not a runtime matcher. `config/motion-matching.sample.json` remains a proposal and does not configure the baker. The saved dataset is intended for the M3 brute-force correctness reference, primary KD-tree backend and later neural export.

## Sampling and coordinate frames

The Editor sampler evaluates a Humanoid clip sequentially in a manual `PlayableGraph` on a separate preview-scene instance. Root motion is enabled; IK and events are disabled. It reads the evaluated Animator transform root, not Hips displacement. A separate temporary AnimatorController/`Animator.Update` path allows root translation/orientation parity checks. Neither graph drives gameplay.

The persistent character must have exactly one Animator, its own valid bound Humanoid Avatar, Hips/LeftFoot/RightFoot mappings, unit Animator world scale and no MonoBehaviour scripts. Scene instances, arbitrary Avatar replacements and Generic/Legacy clips are rejected. Frequency is configurable from 1 to 240 Hz, initially 60 Hz. Time is seconds; positions are metres when source import scale is correct.

Raw root/joint samples use the initial evaluated Animator position as origin and remove its initial rotation. Axes are X right, Y up, Z forward. Root rotation is a full quaternion XYZW. Matching positions and velocities use the **current root yaw** frame, preserving Y rather than applying root pitch/roll.

For root position `r`, yaw `a` and joint position `p`, local position is `RotateY(-a,p-r)`. Differentiate joint positions in the clip sampling frame first, then rotate velocity by `-a`. This does not differentiate moving local positions or subtract root velocity: a stationary planted foot has near-zero velocity while the root travels. Root linear velocity follows the same derivative-then-rotation rule.

Root angular velocity uses the shortest full quaternion difference `qAfter * inverse(qBefore)`, converted to an axis-angle rate in radians/second, then rotated into current yaw-local space. All three components are stored, not just yaw rate.

## Exact matching row layout

Rows contain 27 float32 values, packed row-major. Three strictly increasing positive horizons are configurable in seconds. Defaults are `20/60`, `40/60`, `1` seconds: 20/40/60 frames only at 60 Hz. Changing frequency does not automatically change these times.

| Zero-based indices | Contents | Units |
|---|---|---|
| 0–1 | Future root position at horizon 0: X,Z | m |
| 2–3 | Future root position at horizon 1: X,Z | m |
| 4–5 | Future root position at horizon 2: X,Z | m |
| 6–7 | Future facing at horizon 0: X,Z | unit direction |
| 8–9 | Future facing at horizon 1: X,Z | unit direction |
| 10–11 | Future facing at horizon 2: X,Z | unit direction |
| 12–14 | Left foot position: X,Y,Z | m |
| 15–17 | Right foot position: X,Y,Z | m |
| 18–20 | Left foot velocity: X,Y,Z | m/s |
| 21–23 | Right foot velocity: X,Y,Z | m/s |
| 24–26 | Pelvis velocity: X,Y,Z | m/s |

Future positions are relative to the current root position/yaw. Facing is `(sin(futureYaw-currentYaw),cos(futureYaw-currentYaw))`. Root position and shortest yaw interpolation handle horizons between samples. This adopts the 27D locomotion example from Holden et al. (2020), not their complete learned system.

## Derivatives and boundaries

Interior velocities use central differences over `2/sampleRate`; nonloop endpoints use forward/backward differences. Nonloops sample through the final grid point no later than duration. An off-grid exact endpoint is retained separately as root playback data. A row is searchable only if every horizon fits within the last sampled grid time. Tail rows remain for playback/debugging, with clamped diagnostic trajectories, but are excluded from search and normalization statistics. A database without searchable rows is rejected.

Loops require duration to equal an integer number of sample intervals within tolerance. Incompatible grids are rejected rather than silently changing frequency. Store N rows at `0 ... (N-1)/sampleRate` and a separate evaluated root endpoint at `duration=N/sampleRate`. Wrapping future trajectories and central differences applies the accumulated cycle translation/yaw, including inverse cycles for negative times. It never resets displacement to zero. A loop checkbox cannot repair authored pose seams.

## Normalization and contacts

Compute population mean/std over **valid searchable rows only**, in deterministic clip/frame order with double-precision accumulation. Store float32 means, actual standard deviations and scales `max(std,epsilon)` separately. Raw and normalized rows are retained. Normalize all stored rows as `(raw-mean)/scale`; index valid rows only. Constant dimensions retain zero std and use epsilon as scale. No metric weights are stored/applied yet; M3 must apply a consistent metric to queries and indexed rows.

M2 does not define a machine-learning training split. Future training must fit normalization on its training partition alone and retain those parameters; dataset-wide bake statistics must not leak held-out sequences into training.

Contact flags are auxiliary booleans, not matching dimensions or authored ground truth. For each foot, find its clip-wide minimum root-relative Y, then require height within `ContactHeight` and speed magnitude at/below `ContactSpeed`. Defaults are 0.05 m and 0.2 m/s. Terrain, steps, noise and source scale can invalidate this heuristic; inspect it before foot locking or training-label use.

## Storage and provenance

Core `MotionDatabaseData` has no Unity types. Runtime `MotionDatabaseAsset` stores the portable binary payload plus actual clip, character and Avatar references, Unity engine version and provenance. Editor save/reload validates source identities and current hashes. Runtime decoding validates structure and values but cannot check AssetDatabase content.

Binary v1 starts with magic integer `0x32444D4D`, binary format version 1, feature schema version 1 and extraction settings. `BinaryWriter` uses little-endian scalars, length-prefixed arrays and UTF-8 strings. Unsupported versions, inconsistent dimensions/offsets, non-finite values and trailing bytes are rejected. Packed floats are contiguous.

Retained data:

- Ordered clip name, GUID plus local file identifier, source SHA-256, gait/action tag string, duration, loop flag and global frame offset. Local time is `localFrame/sampleRate`.
- Sampled root position/quaternion and pelvis/feet positions, plus exact evaluated root endpoint position/quaternion.
- Root linear/angular velocities (three components each), yaw-local joint positions/velocities (pelvis, left foot, right foot: nine components each), raw/normalized feature rows, normalization parameters, validity/contact masks.
- All mapped Humanoid bone names in enum order and each sample's parent-local position XYZ/quaternion XYZW. Pose indexing is `(frame*boneCount+boneIndex)` followed by 3/4 components. These rig Transform local poses are a different frame from yaw-local matching features.

Source hashing includes Unity version, clip GUID/local ID, ordered unique character/Avatar/clip paths, AssetDatabase dependency hashes and SHA-256 hashes of source and `.meta` bytes. Source/import changes require rebaking. Binary SHA-256 supports same-input determinism comparisons; this does not certify cross-machine/Unity-version bitwise identity.

Feature order, units, space or binary structure changes require explicit schema/format decisions. Settings are saved per dataset; equal dimension counts alone do not establish compatibility. Future query/model manifests must check the full contract.

## M2.5 consumer contract and inspection

M2.5 changes inspection only: Core/Runtime code, extraction and normalization semantics, 27D order, schema v1 and binary v1 remain unchanged. The DTO arrays are mutable; consumers must treat saved features, means, standard deviations and normalization scales as read-only. Do not renormalize these arrays in place.

For M3, map a clip's rows through `FrameOffset` to `FrameOffset + Samples.Length - 1`; local time is `localRow / SampleRate`. Index only rows whose `ValidFrames` flag is true. Normalize a query with the dataset's existing means and applied scales, using the same feature order, coordinate frame, horizons and sample-rate contract.

The proposed M3 distance convention has five groups: trajectory position [0–5], trajectory facing [6–11], feet position [12–17], feet velocity [18–23], pelvis velocity [24–26]. Each group defaults to weight 1. Define cost as the sum, over dimensions, of `groupWeight * (normalizedQuery - normalizedRow)^2`. For KD-tree coordinates multiply both query and row dimensions by `sqrt(groupWeight)` once. Keep weights separate from normalization and apply the same metric to the brute-force oracle. This documents the intended future search contract; M2.5 implements no search or weighting backend.

The baker's evaluated-source viewport caches the complete evaluated hierarchy in an isolated graph once per selected clip. Stored-pose inspection resets unmapped transforms to the reference pose, then applies the saved root and mapped parent-local bone transforms (52 bones in the baseline). These modes can differ because the database does not retain the complete hierarchy. Viewport comparison measures world-space root/bone pose errors. The independent `CompareControllerPose` diagnostic compares mapped parent-local bones and rebased roots against temporary AnimatorController evaluation at selected times; it is a separate check, not a replacement for visual inspection. Actual evidence and coverage limits belong in the [M2.5 report](../migration/M25_IMPLEMENTATION_REPORT.md).

## Scope and evidence

The initial M2 sources were Idle, walking, Fast Run, LeftStraffe and RightStraffe. The nine-clip `LocomotionBaseline.asset` later added Walking Backward, Run Backward and measured left/right 180-degree turns; it is retained as the rollback database. The active `HumanBasicMotions60.asset` has 25 Kevin Iglesias locomotion clips and 1,276 searchable rows, but no 180-degree turn clips. Filenames do not certify coverage or quality. Current counts and parity evidence are recorded in [PROGRESS.md](../PROGRESS.md); the initial bake is preserved as historical evidence in the [M2 report](../migration/M2_IMPLEMENTATION_REPORT.md). This specification defines the data contract.

Retained root/pose data lets neural export reuse this pipeline. It does not define neural targets, muscle curves, intermediate unmapped transforms, compressed poses or a full hierarchy interchange format. M5 must define additional targets and partition/export metadata. M2 adds no runtime search, KD-tree, gameplay Playables replacement, training or inference.

Reference: [Holden et al., Learned Motion Matching (2020)](https://doi.org/10.1145/3386569.3392440).
