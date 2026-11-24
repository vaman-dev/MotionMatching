# Unity.md — Unity engineering rules

**Cleanup update (2026-11-24):** The original Test scene, rifle prefab, broken Ch15 variant, stale recovery scenes and nine LegacyShooter scripts have been retired. MotionMatchingTest is the sole enabled build scene; Runtime references Core, Input System and Cinemachine. Earlier preservation/legacy-retention statements below describe the M1/C1 baseline. Fresh cleanup validation and remaining publication limits are in [FINAL_CLEANUP_REPORT.md](migration/FINAL_CLEANUP_REPORT.md).


Scope: Unity `6000.6.0f1` project migrated from `TPS_Shooter`. Enforce these rules in code reviews, tests and agent prompts.

## Architecture

- Use high cohesion, narrow dependencies and one clear owner for each state/transform.
- MonoBehaviours at lifecycle boundaries; pure C# types for search maths, feature structures and algorithms.
- Follow single responsibility, interface segregation and composition; don't invent a generic DI/Service Locator architecture for a small project.
- Inspector dependencies via `[SerializeField] private` and explicit validation in `OnValidate`/`Awake`. Fail with actionable diagnostics rather than silently using wrong roots.
- Runtime tuning values in versioned ScriptableObject settings or database metadata (sample rate, horizon, speed, weights, thresholds). Avoid unexplained magic numbers and `FindObjectOfType` hot paths.
- Keep editor-only APIs under Editor assemblies/folders. `UnityEditor` must never enter runtime assemblies.
- Ship `.meta` files alongside any Unity asset; never regenerate GUIDs accidentally while moving existing referenced files.

## Motion and simulation contracts

- Input sample -> desired world velocity/facing -> trajectory prediction -> search -> animation output -> single motor.
- Never couple gravity to the occurrence of a root-motion callback; standing idle still needs vertical simulation.
- One `CharacterController.Move` owner per simulation tick. Treat world/local coordinate systems and delta-time units explicitly.
- Never use `Animator.speed`, blend-tree parameter `Speed` or frame indexes as physical metres per second.
- Root translation/rotation extraction from Humanoid animations must account for root projection and import options. Validate against Unity's evaluated root delta.
- Never allocate in the inner per-candidate search loop. Prefer packed row-major storage and reused buffers after profiling.
- Keep normalizing means/stds immutable and saved with database version. Handle constant-valued dimensions deterministically.
- Distinguish animation root velocity, requested velocity, actual collision-resolved velocity and joint-local velocity in naming.
- Current normal-mode schedule: relay submits horizontal world-space metres; `PlayerController.LateUpdate` first integrates gameplay gravity/carry and steps `CharacterMotor`, then refreshes existing timers/grounding. Preserve frame-local consume-once buffering, reject submissions after the step, and reject repeated same-frame steps. Fixed/manual animation evaluation requires separate scheduling validation.
- `CharacterMotor` is the sole capsule Move owner; gameplay retains vertical velocity, gravity/jump settings and action guards. Reuse existing serialized parameters during this migration rather than adding duplicate configuration or a second scene motor component.
- The dedicated unarmed controller tags Dive Roll `RootMotionAction`; the relay checks current/next base-layer states to preserve authored travel when locomotion Speed is zero. Keep Jump untagged unless its root-travel policy is deliberately changed and tested.

## Playables and graphs

- Own and dispose a `PlayableGraph` explicitly. Do not have an Animator state machine and a Playables graph independently drive the same skeleton.
- Ensure RigBuilder and any procedural constraints have a documented evaluation order before adding foot IK.
- A search result selects a clip/time; state transition mechanics and root motion application belong to separate components.
- Reset/seek clip events deliberately; do not inadvertently replay footsteps on a match jump.

## Testing and telemetry

- Unit test feature math, finite differences, nearest-neighbor costs, coordinate transforms and KD-tree search against exact oracle.
- Unity EditMode tests for sample extraction and serialized asset validation; PlayMode tests for root/capsule lifecycle, gravity, input and locomotion.
- Profile on a defined dataset and machine; report per-query cost, GC allocations, frame time, memory footprint and visual quality.
- Treat a static Inspector/report observation as **not** a runtime test. Do not claim it compiled if Unity did not run.
- M1 evidence includes seven passing EditMode and six passing PlayMode tests plus a confirmed 1,140-sample 60 Hz unarmed locomotion/action/Look capture. Full slope/ledge/coyote-boundary/contact-quality matrices and player builds remain unverified. Current Console errors and retained historical tooling errors are separate observations.
- C1 reran 7 EditMode, 6 PlayMode and 31 Python tests and passed a 1,141-frame regression capture. Compiled source ownership and scene references were checked after relocation. The final Console retains an existing Editor assertion and two messages from an already broken unused variant; another unused prefab has two preexisting missing scripts. Do not claim zero Console errors or repair unrelated deleted metadata without a scoped decision. See [the report](migration/CONSOLIDATION_REPORT.md).

## Repository discipline

- Small, reviewable commits; avoid moving unrelated Unity assets during locomotion changes.
- No hardcoded absolute machine paths, secrets, platform-specific file separators or hidden external services.
- Preserve existing `Packages/manifest.json`, input asset, scenes and `.meta` until a deliberate migration commit.
- Keep `Library/`, `Temp/`, `Logs/`, `Obj/`, generated build artifacts and local training environments untracked via existing `.gitignore` (merge rules carefully; don't overwrite it).
- Preserve Git LFS rules already in the repository for large FBX/animation assets; review usage rights before publishing anything.
- Unity reserialized ProjectSettings.asset and TimeManager.asset during M1 validation; both were restored to exact M0 bytes, including background execution false. Pre-restoration copies are in `.utmp/m1-final/settings-before-restoration`. Recheck preservation with the explicit migration allowlist after Editor runs.
- New public APIs and tools must have examples, tests and documentation.

## Naming and style

- C# `PascalCase` for types/methods/properties, `_camelCase` private instance fields, `I` prefixes for interfaces.
- `Runtime`, `Editor`, `Tests` assembly separation when creating a reusable package; avoid assemblies that are too granular.
- All project-owned C# belongs beneath `Assets/Scripts/MotionMatching`, inside the existing five assembly boundaries. Core is engine-independent; Runtime references Core, Input System, Cinemachine and Animation Rigging. Editor contains the audit/capture tooling and is restricted to the Editor platform. Tests live under `Tests/EditMode` and `Tests/PlayMode`. No project-owned script should fall into Assembly-CSharp. Third-party assets remain outside this root. No Neural assembly exists.
- Follow [the directory guide](migration/DIRECTORY_LAYOUT.md). Source animations are under `Assets/Animations/MotionMatching/Source`; preserve importer metadata and source GUIDs. Keep original-scene compatibility under `Runtime/LegacyShooter` until serialized dependencies are retired. Do not create empty feature/package directories.
- Use explicit units in names: `Seconds`, `MetersPerSecond`, `RadiansPerSecond`, `FrameIndex`.
- Do not add abstractions until at least two genuine uses justify them.
