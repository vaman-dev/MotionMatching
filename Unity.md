# Unity.md — Unity engineering rules

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
- The five `MotionMatching.*` boundaries beneath `Assets/MotionMatching` are implemented and imported in Unity; see `MotionMatchingArchitecture.md`. Core is independent of Unity; Runtime references Core, Input System and Cinemachine for the motor/free-camera adapter and cannot reference Editor/tests. Vendor code and legacy player scripts remain outside the subtree. Legacy Assembly-CSharp consumes the auto-referenced motor; migration-only regression tests use reflection to observe legacy types. M0's observation tool remains in the existing `Assets/Editor` compilation boundary. No Neural assembly exists.
- Follow `docs/migration/DIRECTORY_LAYOUT.md` for current versus future folder ownership. Keep legacy integrations and source animation packs in place until a separately validated cleanup; do not create empty feature/package directories.
- Use explicit units in names: `Seconds`, `MetersPerSecond`, `RadiansPerSecond`, `FrameIndex`.
- Do not add abstractions until at least two genuine uses justify them.
