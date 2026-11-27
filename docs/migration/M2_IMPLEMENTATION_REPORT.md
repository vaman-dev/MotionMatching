# M2 — Animation feature extraction and database baking

Date: 2026-11-24. Unity 6000.6.0f1, existing Pipeline 1.0.1. Changes remain uncommitted. No new package or assembly was added.

## Implemented result

Five existing unarmed clips are sampled into a reproducible database, saved/reloaded as a Unity asset and inspected in the baker window. This meets the local M2 functional gate. It does not certify an asset-complete clean checkout, a final-quality locomotion dataset or a fix for the historical intermittent Editor assertion described below. Grounded Animator playback, CharacterMotor, input, camera, jump and dive roll code were not changed.

Open **Tools > Motion Matching > Motion Database Baker**, select `Assets/Animations/MotionMatching/Databases/LocomotionBaseline.asset`, then **Reload and inspect**. Select a clip and sample to inspect its root path, facing and joint-velocity vectors. The optional Scene view draws data without creating scene objects. To rebake, choose the character model asset and its own bound Humanoid Avatar, add clips, configure rate/horizons/tags/loop flags and choose **Bake and inspect**, then save. Save/reload rejects source-identity/hash mismatches. Saving an existing database preserves its GUID and saves that asset only.

## Files created

| File beneath Assets/Scripts/MotionMatching | Responsibility |
|---|---|
| Core/MotionData.cs | Engine-independent samples, schema/settings, coordinate/derivative math, features, normalization, contacts, packed database and deterministic binary serializer |
| Runtime/Animation/MotionDatabaseAsset.cs | Portable payload with Unity clip/character/Avatar references and lazy decode |
| Editor/MotionAnimationSampler.cs | Preview-scene Humanoid evaluation, provenance, root parity helper and validated bake/store entry points |
| Editor/MotionDatabaseBakerWindow.cs | Configurable baker, save/reload, counts, trajectory and velocity inspection |
| Tests/EditMode/MotionDataTests.cs | 19 mathematical/data tests |
| Tests/EditMode/MotionSamplingTests.cs | 6 real sampling/source-integrity/asset-reload tests |

Each C# file has a Unity-generated `.meta`. The database asset and Databases folder also have metadata. Existing five asmdefs and retained GUIDs were preserved. Documentation adds ADR-0003 and updates the feature schema, architecture, plan and progress.

## Real source audit and bake

Unity enumerated 140 imported animation-asset paths under the source root; the actual inventory is `.utmp/m2/clip-inventory.txt`. The selected model is `Assets/Prefabs/Chracter/Ch15_nonPBR.fbx`; its bound Avatar is valid Humanoid. All selected clips are Humanoid, looping, have root curves and are imported at 30 fps; evaluated samples are taken at 60 Hz. No source clip/import setting was rewritten.

| Clip | Samples | Mapped pose bones | Max root position disagreement with AnimatorController playback |
|---|---:|---:|---:|
| Idle | 498 | 52 | 0.0000003212 m |
| walking | 62 | 52 | 0.0000014696 m |
| Fast Run | 32 | 52 | 0.0000007154 m |
| LeftStraffe | 62 | 52 | 0.0000017364 m |
| RightStraffe | 62 | 52 | 0.0000010991 m |

Root orientation disagreement was 0 degrees for these sources. Parity compares the isolated manual-graph samples with independent temporary AnimatorController/Animator.Update evaluation at the same times, including root endpoint translation. This is actual Unity animation evaluation in Edit Mode, not a comparison against Hips translation or an independent mathematical ground truth. Neither evaluation drives the gameplay character.

The asset contains **716 rows, 716 searchable frames, 27 matching dimensions and 52 mapped parent-local Humanoid transforms per sample**. Portable payload: **1,319,273 bytes**. Two complete five-clip bakes produced identical bytes. Asset reload preserved the payload exactly.

SHA-256: `de31ca563eba7870750549ac88e164bb25aa1276b0f176ff0fd697ceb8af716e`.

## Shared data contract

The precise contract is [FEATURE_SCHEMA.md](../data/FEATURE_SCHEMA.md), with rationale in [ADR-0003](../decisions/0003-motion-database.md).

- Default frequency: 60 Hz. Horizons: 20/60, 40/60 and 1 second. Three horizons remain configurable in seconds when rate changes.
- Matching order: trajectory XZ positions (6), trajectory XZ facing (6), left/right foot XYZ positions (6), foot XYZ velocities (6), pelvis XYZ velocity (3).
- X right, Y up, Z forward. Root/joint samples are rebased to the first evaluated root. Matching features use current-root yaw. Velocities differentiate sampling-frame positions before rotating into that frame. Root angular velocity retains all three quaternion-derived components in rad/s.
- Loop rows exclude the duplicate endpoint; the exact root endpoint supports rigid translation/yaw continuation. Loop duration must align to the sample grid. Nonloop rows without every future horizon are retained but not searchable or included in statistics.
- Statistics use valid rows with double accumulation, retaining float means, actual population standard deviations and applied scales `max(std,epsilon)`. Raw features are retained so future training can fit normalization only to its training partition.
- Root trajectories/quaternions, root/joint velocities, pose-local transforms, source identities/hashes, clip/time offsets and tags remain available beyond the 27 features. Neural targets, additional labels and full rig/muscle interchange remain future decisions.
- Foot contacts are auxiliary per-clip minimum-height/speed heuristics. They are not reliable authored contact ground truth and must be reviewed before foot locking or training use.

## Tests and actual validation

| Check | Result |
|---|---|
| Unity compilation | Successful compile; final check up to date; no compiler errors/warnings |
| EditMode | 32 passed, 0 failed, 0 skipped (7 existing + 25 M2) |
| PlayMode | 6 passed, 0 failed, 0 skipped |
| Python | 31 passed |
| Static assembly/scaffold | Passed; local TOML/YAML syntax parser skips remain explicit |
| Five-clip repeated bake | Byte-identical |
| Binary and Unity asset reload | Byte-identical payload and source references |
| Root trajectory/orientation parity | Passed for all five clips |
| Baker visual inspection | Populated window screenshot inspected: root path, joint arrows, counts, selection and contact metadata render |
| Existing locomotion capture | 1,141 consecutive 60 Hz frames; all **15** comparisons passed; completion restored=true/succeeded=true/errors=[] |
| Settled controlled Play Mode cycle | 0 Console errors and 0 warnings |
| Final Console status after the final EditMode run | compilationFailed=false, compiling=false, 0 errors, 0 warnings (5 informational logs) |

Gameplay capture medians: walk 1.626991 m/s, sprint 5.541888 m/s, turn 1.664813 m/s. Jump apex 1.329979 m; dive path 3.275255 m; idle/post-dive drift zero; free-camera Look 58.725395 degrees. This confirms the existing grounded/action/camera behavior in the tested scenarios without introducing matching playback.

Actual commands: `unity recompile . --timeout 60 --json`; `unity command test run --mode editor --filter MotionMatching.Tests.Editor --filter-type Assembly --async-tests true --timeout 10 --json`, with `test status`; equivalent PlayMode command; `py -3 -B -m unittest discover -s tools/tests -v`; `tools/validate_assemblies.py`; `tools/validate_scaffold.py`. Scripts under `.utmp/m2/` invoke real bake, independent per-clip root parity, asset reload and existing MigrationBaselineCapture. Evidence includes compilation-final, editmode-final, playmode-final, bake-report, clip-parity, capture-comparison and baker-window.png.

## Diagnostics, preservation and limits

An early Core C# declaration error was corrected before passing compilation/tests; its history is preserved. The historical `EditorApplication.Internal_PlayModeStateChanged` assertion recurred immediately after capture completion, with no project stack. The completed capture itself has no recorded errors. One additional manual Play attempt overlapped Editor-code recompilation and produced transient controller initialization exceptions; that attempt was stopped and preserved. With compilation idle, the repeated manual Play cycle produced zero errors/warnings. No controller code was changed to mask reload behavior, and no root-cause fix for the intermittent assertion is claimed. See `.utmp/m2/console-errors-history.json`, `console-before-clean-control.json`, `assertion-control.json` and `clean-control.json`. The fresh Console was cleared only after archiving diagnostics for the controlled experiment.

The local model/Avatar GUID `8cce7d34a61d4be479104c1586a5ad71` still belongs to an untracked FBX. The local skybox is also untracked and is needed by the demo scene, not the sampler. Existing scene/material/terrain/source-animation/settings/skill changes remain outside M2 ownership. The generated database contains derived pose data; do not publish/package it without source redistribution clearance. No clean-checkout Unity or player-build claim is made.

The dataset establishes a working baker, not comprehensive motion coverage. Starts/stops, pivots, dedicated turn clips, backward travel and contact polish need further source selection before the portfolio controller is complete. Loop continuation assumes upright locomotion with planar yaw; arbitrary tumbling root cycles are unsupported. Nonaligned loops are rejected. Retargeting to unrelated Avatars, nonunit scale, Generic clips, gameplay-script prefabs and scene-instance sampling are deliberately rejected. Local mapped bone poses do not include every unmapped intermediary transform or Humanoid muscle target. Same-environment determinism is verified; bitwise equality across Unity versions/platforms is not certified.

No runtime brute-force/KD-tree search, trajectory predictor, matching playback, neural training/inference or gameplay replacement was added. M3 will use this shared dataset for the brute-force correctness reference and primary KD-tree controller.
