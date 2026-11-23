# Repository assessment and migration plan

Date: 2026-11-23. Inventory and report comparison below describe the baseline before migration changes. Subsequent motor/assembly implementation, scene detachment and final Unity validation are recorded separately below. Exact execution evidence is in [BASELINE.md](BASELINE.md) and `PROGRESS.md`. `Report.md` remains a historical snapshot.

## Confirmed baseline

| Area | Evidence | Finding |
|---|---|---|
| Source revision | `git rev-parse HEAD`, `git branch --show-current` | `13c84848ef2451067b4f8221670fbf4f64d28b86`, branch `master` at inspection. HEAD does not reproduce the dirty working tree. |
| Unity | `ProjectSettings/ProjectVersion.txt` | `6000.6.0f1`, revision `f7f8ed4d1e24`. This identifies the configured version, not successful Editor execution. |
| Packages | `Packages/manifest.json`, `Packages/packages-lock.json` | URP `17.6.0`, Input System `1.20.0`, Cinemachine `6.6.0`, Animation Rigging `6.6.0`, Test Framework `1.8.0`. Cinemachine and Animation Rigging lock entries use source `builtin`. |
| Additional declared tools | Manifest | AI Navigation `2.0.14`, Behavior `1.0.16`, ProBuilder `6.1.2`, Timeline `6.6.0`, UGUI `2.6.0`, Post Processing `3.5.4`, Visual Scripting `1.9.12`, Multiplayer Center `2.0.1`. Presence does not establish character usage. |
| Input | `ProjectSettings/ProjectSettings.asset`, input action asset | `activeInputHandler: 1`; existing Move/Look/Sprint/Jump/DiveRoll bindings must survive initial migration. |
| Rendering | `GraphicsSettings.asset`, `Assets/Settings/PC_RPAsset.asset.meta` | Custom pipeline references matching GUID `4b83569d67af61e458304325a23e5dfd`. |
| Character | `Test.unity`, `Assets/Prefabs/Chracter/Ch15_nonPBR.fbx.meta` | CharacterController capsule, Humanoid importer (`animationType: 3`), Animator root motion enabled (`m_ApplyRootMotion: 1`), normal update mode (`0`). |
| Scenes | `rg --files Assets`, `EditorBuildSettings.asset` | `Assets/Scenes/Test.unity` and `Assets/_Recovery/0.unity`; only Test is enabled in build settings. |
| First-party C# | `rg --files Assets` | 19 scripts, all under `Assets/Scripts`: 16 in Player and 3 in Gun. No first-party Editor API imports found. |
| Animation assets | Recursive animation-folder inventory | 31 `.anim`, 109 `.fbx`, one `.controller`, one `.mask` under `Assets/Scripts/Player/Animation`. There are 113 FBX files across Assets. Counts do not establish usable motion coverage. |
| Assembly/test baseline | Asset file inventory | No `.asmdef` files and no first-party Unity tests at inspection. Installed Test Framework is not executed test evidence. |
| Neural baseline | Manifest and first-party source inventory | No Sentis/inference package, learned runtime, database baker, trajectory predictor, brute-force search or KD-tree implementation. |
| Git preservation | `git status --short`, `.gitattributes`, `.gitignore` | Existing modified/untracked scripts, scenes, art, animation/import metadata, package settings and scaffold files. Existing deleted animation/mask/metadata paths are user changes. FBX LFS rule is `*.fbx filter=lfs diff=lfs merge=lfs -text`. |

Top-level source/tool directories are `Assets`, `Packages`, `ProjectSettings`, `agents`, `.agents`, `.codex`, `.github`, `config`, `docs`, and `tools`. Local generated directories include `Library`, `Logs`, `.vs`, and `UserSettings`; existing ignore rules already cover them. Retain established art directories and the existing `Chracter` spelling because renaming assets adds unnecessary migration risk. `Assets/Editor`, `ShaderGraph`, and `VFX` directory presence alone does not establish implemented editor tools or effects.

## Findings checked against the report

The source inventory, Unity/package versions, two scenes, enabled build scene, Humanoid setting, root-motion callback dependency, and rifle graph described by `Report.md` match the inspected files. The report's 109 FBX count refers to the animation subtree, not all of Assets.

The most consequential confirmed baseline coupling was `RootMotionRelay.OnAnimatorMove()` -> `PlayerController.ApplyRootMotionMovement()` -> `ApplyGravity()` and the sole first-party `CharacterController.Move` call. Baseline `PlayerController.Update()` ticked gameplay states without independently integrating gravity; disabling relay movement stopped both capsule movement and gravity. The M1 motor change described below removes that scheduling dependency.

The legacy controller's actual scene values include ground radius `0.13`, jump-forward force `5`, and grounded gravity `-13`. These differ from source defaults and must be preserved for parity. Animation/root Y is discarded while gameplay owns vertical motion. States currently own yaw; relay root rotation is disabled in the scene.

The Animator asset has `MovementLayer` and `RifleLayer`, unarmed and rifle locomotion trees, Jump and Dive Roll states, and rifle-dependent transitions. `Test.unity` contains TPSCamera, TPSAimCamera, WeaponAimPivot, spine and hand rigs. RigBuilder serializes the spine layer inactive and the hand layer active. `rifleSprintForwardOnly: 0` remains the actual configuration. These are serialized facts, not visual confirmation.

`PlayerController.IsAiming` still reads Animator `HasRifle` and input RMB. The root relay uses that condition to redirect horizontal motion. Its idle Speed filter applies to action deltas as well as ordinary locomotion; action travel needs specific regression coverage before detaching rifle/ADS logic.

Reinspection identified two stale scaffold statements, both repaired during M0: `PROGRESS.md` now labels unavailable code/assets as the scaffold author's historical limitation, and `PROJECT.md` clarifies that the baseline HEAD is the same commit but lacks the dirty snapshot. The report predates the now-present agents, skills, workflows and scaffold tests. Its statements that those were absent apply to its earlier inspection scope. Its recommendation to preserve and later integrate rifle/ADS systems is superseded by the user's exclusive Motion Matching scope. Preserve the report unchanged and record current decisions in live documents.

## Concrete staged implementation

M0 is the first execution boundary. Complete its preservation and inspection work before altering motor or playback behavior. Record partial evidence honestly; no static check can close a runtime exit gate.

| Stage | Small reviewable change | Required evidence before advancing |
|---|---|---|
| M0a: preserve | Capture baseline Git status and relevant source/scene hashes; preserve all existing modified, untracked and deleted paths. Avoid automatic commit, reset, asset restoration, or LFS changes. | Reviewable preservation record identifying original scene/controller and existing user changes. A hash/status inventory is not a complete asset backup. |
| M0b: configure | Validate the four agent definitions, role/skill links, Codex configuration, workflow syntax and scaffold tests. Correct stale references/configuration with narrow diffs. | Exact validator/test commands and results; distinguish local parsing from Codex acceptance and remote workflow execution. |
| M0c: dedicated scene | Copy the current on-disk Test scene into `Assets/Scenes/MotionMatchingTest.unity` with a new scene GUID; keep the original scene, controller and build settings intact. Retain existing Animator, relay and authored actions initially. | Byte/hash comparison of source against baseline; clone content equality and distinct GUID; open/import/compile in Unity when available. A clone alone still contains shooter systems. |
| M0d: characterize | Capture baseline Console status and idle/walk/sprint/turn/jump/dive, wall/step/slope/ledge and free-camera behavior. Measure actual capsule displacement rather than interpreting Animator Speed as m/s. | Unity Editor logs and repeatable scenario observations. Until captured, runtime baseline remains open. |
| M1a: authoritative motor | Keep grounded animation selection unchanged. Extract one scheduled capsule movement/vertical simulation owner; relay submits horizontal animation deltas only. Define consume-once root buffering, explicit normal-update scheduling, yaw authority, and disabled/no-callback behavior. Preserve collision, scene values, jump/coyote locks and dive timing. | Unity compile plus idle gravity, disabled relay/Animator gravity, stationary/moving jump, ledge fall, wall/slopes and dive matrix; exactly one capsule movement/integration per simulation step. No double root delta. |
| M1b: locomotion-only scene | In the dedicated scene detach pickup, rifle equipment, aiming providers, weapon alignment, weapon IK/constraints, aim camera/ADS and rifle-only hierarchy. Use a dedicated unarmed controller retaining locomotion, Jump and Dive Roll. Preserve original scene/controller and all source clips/import metadata. | Serialized-reference audit, no missing scripts, Unity compile/Play Mode matrix; free camera/input/actions/collision/root motion retain expected behavior. Zero active shooter dependencies in the dedicated scene. |
| M1c: assembly boundaries | Introduce the five requested assembly definitions under a dedicated first-party MotionMatching subtree. Move only deliberately migrated source with metadata preservation; leave vendor packages and legacy scene code outside. | Parse/reference/cycle and ownership checks; Unity Editor compilation and test discovery after import. No runtime UnityEditor references or accidental package capture. |
| M2: data pipeline | Audit a small coherent unarmed clip set, define versioned 27D schema/root conventions, sample and bake deterministically with metadata/source hashes and visualization. | Golden coordinate/root tests, deterministic bake/reload, valid horizons/loop boundaries, missing-bone/non-finite/zero-variance checks. |
| M3: exact matching | Implement normalized weighted brute-force selection, explicit tie/filter policy and configured cadence. Add controlled playback only after the motor gate; retain authored Jump/Dive ownership. | Exact search fixtures and Unity motion/action handoff tests; no duplicate movement, acceptable transitions/contact behavior measured. |
| M4: KD-tree | Reuse identical feature rows, weights, filters and tie rules; benchmark against brute force. | Oracle parity and recorded p50/p95 timing, memory/build costs, dataset hash and hardware. Keep brute force fallback. |
| M5/M6: learned experiments | Define teacher target and source-sequence train/validation/test split; later validate ONNX and introduce optional Sentis integration. | Independent model quality evaluation and golden Python/Unity parity; package compatibility checked when this milestone begins. No neural assembly or dependency now. |
| M7: optional full learned methods | Investigate stepping and compression/decompression after prior gates. | Reproducible quality, memory and temporal stability comparison. Projector-only MLP is not full Learned Motion Matching. |

The exact motor simulation phase must be tested against the existing normal Animator update and Cinemachine tracking. A buffered relay delta consumed once in a documented motor phase is a candidate design, not validated timing. Do not add a second movement owner to hide ordering defects.

## Implemented assembly boundaries

The five definitions now live beneath `Assets/MotionMatching/{Core,Runtime,Editor,Tests/Editor,Tests/Runtime}`. The dependency arrows below mean “references.”

```text
MotionMatching.Core                 -> no project assembly
MotionMatching.Runtime              -> MotionMatching.Core, Unity.InputSystem,
                                       Unity.Cinemachine
MotionMatching.Editor               -> MotionMatching.Core, MotionMatching.Runtime
MotionMatching.Tests.Editor         -> MotionMatching.Core, MotionMatching.Runtime,
                                       MotionMatching.Editor, Unity test assemblies
MotionMatching.Tests.Runtime        -> MotionMatching.Core, MotionMatching.Runtime,
                                       Unity runtime test assembly
```

Core currently holds pure C# frame-local `RootMotionAccumulator` and disallows engine references. Runtime contains `CharacterMotor` and `FreeCameraLookInput`; the latter requires Input System/Cinemachine. Editor is Editor-platform-only and contains `AssemblyBoundaryAudit`; sampling/baking remain planned. Editor tests are Editor-only; runtime tests have no Editor or `UnityEditor.TestRunner` reference. Both test assemblies use explicit NUnit/test-runner references, `autoReferenced: false`, and `UNITY_INCLUDE_TESTS`.

Do not put an asmdef at Assets root or indiscriminately around all existing Scripts. New assemblies must not depend on the predefined `Assembly-CSharp`; that prevents moving one half of mutually coupled legacy player/weapon code into a named assembly. Keep existing global-namespace legacy code outside these boundaries until an intentional dependency split is ready. PackageCache/vendor assemblies stay owned by their packages. No `MotionMatching.Neural` assembly, empty package skeleton, new DI container, or placeholder runtime APIs are needed at this stage.

## Validation scope and open evidence

Inspection commands used `Get-Content` for required documents and settings, `rg --files Assets` for inventory, `rg` against source/scene/controller fields, and `git status --short`, `git rev-parse HEAD`, `git branch --show-current`. These establish files and structural dependencies. They do not establish Unity import, compilation, test discovery, Play Mode, remote CI, package resolution, motion coverage or runtime performance.

The configured Editor is installed and has completed a batch import/script compilation. A verified working-source backup and scene clone exist; see `BASELINE.md`. Following explicit user authorization, Unity CLI installed Pipeline 1.0.1 and the interactive Editor reported ready. M0 live queries found MotionMatchingTest active, loaded and clean, with seven roots and 102 GameObjects; retain that point-in-time result separately from the later 82-object mutation. Final tests and confirmed post-detachment capture are summarized below. Remaining evidence includes full slope/ledge/coyote-boundary/contact-quality matrices, player builds, target hardware/platform budgets and source clip licensing/coverage. Keep corresponding PLAN gates open until their evidence exists.

## M1 start preservation evidence

Before motor edits, a fresh working-source snapshot was recorded under `.utmp/m1-start-20261123-124849/`: `worktree.zip`, `manifest.json`, and `preservation-review.json`. The review records 556 archived files, eight preserved preexisting deletions, and archive SHA256 `6cf47857f805c4f9d55ae965425c77aa2d01246229de98635679a1072e65edd5`. Archived paths matched the working files after snapshot capture. The snapshot excludes ignored local files and Git configuration/index/history; it supplements the original M0 archive.

Comparison of the 515 original protected M0 paths found only the separately authorized Pipeline changes in `Packages/manifest.json` and `Packages/packages-lock.json`: addition of `com.unity.pipeline` 1.0.1 and Mono.Cecil 1.11.6 dependency depth changing from 2 to 1. Existing package versions/sources, original scene/controller, existing metadata, historical Report, LFS rules and ignore rules were preserved. The review inspected 258 original metadata paths and found an empty staged diff. These are findings before motor edits; subsequent implementation evidence follows.

## M1 implementation and remaining evidence

The motor and five assemblies were implemented without moving legacy scripts or source assets. `PlayerController` retains its serialized gameplay parameters and vertical/carry state; its first LateUpdate operation now steps `CharacterMotor` before existing ground/timer refreshes. The relay submits horizontal metres, Core buffers only the current frame, and Runtime makes the sole capsule Move call. Unity generated metadata for the new assets. Four EditMode and six PlayMode tests passed before scene detachment, including callback-free gravity/jump regressions that failed before the patch; see `MotionMatchingArchitecture.md` and PROGRESS for exact artifacts.

Motor comparison observed stable speed changes below 0.5%, approximately 1.33 m jump ascent, camera offset below 0.8 mm and no measured idle drift. These capture-specific results do not establish full collision/action parity.

Live scene edits then reduced MotionMatchingTest from 102 to 82 GameObjects with seven roots retained. Pickup/table/AR-15 content, interaction sphere component and InteractionPoint, ADS camera/target, weaponSocket/pivot, hand/spine rigs and RigTargets, five shooter components and RigBuilder were removed from the clone. BoneRenderer debug visualization remains. Original Test scene/controller and animation clips were preserved.

The clone uses new `Assets/MotionMatching/Animation/UnarmedLocomotion.controller`: one layer and Locomotion, Jump and Dive Roll states, with rifle graph/subassets removed and compatibility bool parameters retained. `FreeCameraLookInput` binds TPSCamera to the same Look input asset; CameraTarget is assigned. Runtime explicitly references Input System/Cinemachine. The relay's configurable `RootMotionAction` tag bypasses the Speed idle filter for current/next base-layer action states; only Dive Roll is tagged. Final tests and confirmed capture exercised the scene, camera and tagged-action changes. No database, search or neural functionality exists. Current directory ownership and future cleanup are documented in [DIRECTORY_LAYOUT.md](DIRECTORY_LAYOUT.md).

## Final M1 validation and preservation

Actual Unity execution passed seven EditMode tests (`.utmp/m1-final/editmode-results.json`) and six PlayMode tests (`.utmp/m1-final/playmode-completed.json`). The confirmed schema-3 capture `.utmp/m1-final/locomotion-confirm.txt` contains 1,140 consecutive samples at 60 Hz over approximately 19 seconds; completion and restoration succeeded with empty error arrays. Complete metrics are in `.utmp/m1-final/locomotion-confirm-analysis.json`.

| Confirmed capture measurement | Result |
|---|---:|
| Stable walk / sprint / turn speed | 1.626 / 5.524 / 1.673 m/s |
| Jump apex above start / horizontal carry path | 1.32998 / 1.60444 m |
| Dive horizontal path | 3.27525 m; earlier idle-filtered run had zero travel |
| Idle / post-dive horizontal drift | 0 / 0 m |
| Look orbit / actual camera orientation change | 58.7254 / 58.7255 degrees |

A rendered screenshot showed unarmed walking. Final Editor inspection found MotionMatchingTest clean with 82 objects/seven roots, compilation failure false and zero current Console errors. Five historical errors from corrected tooling remain separately recorded. The confirmed repeat is authoritative; an earlier absent sample artifact was excluded. Obstacle/table removal changed traversal, preventing identical whole-route comparisons. Full slopes, ledges, coyote boundaries, contact quality and foot sliding remain unverified; no player build ran.

Twenty Python tests and assembly/scaffold checks passed. Local TOML/YAML parser availability remains limited; skipped parsing is reported explicitly. GitHub Actions were reviewed statically without remote CI execution.

The final strict preservation allowlist passed; Git configuration was unchanged. Original Test scene/controller, existing metadata, source clips and LFS rules were preserved. Unity's automatic reserialization of `ProjectSettings/ProjectSettings.asset` and `ProjectSettings/TimeManager.asset` was reversed to their exact M0 bytes, including background execution false; backups are `.utmp/m1-final/settings-before-restoration`. No commit or push was performed.
