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

- Current opt-in M3 flow: input -> desired velocity/facing -> trajectory prediction -> weighted KD-tree search (brute-force oracle) -> matched clip/time in one Playables graph -> root-motion relay -> single motor. MotionMatchingTest enables the experimental matching component; polished playable acceptance remains open.
- Never couple gravity to the occurrence of a root-motion callback; standing idle still needs vertical simulation.
- One `CharacterController.Move` owner per simulation tick. Treat world/local coordinate systems and delta-time units explicitly.
- Never use `Animator.speed`, blend-tree parameter `Speed` or frame indexes as physical metres per second.
- Root translation/rotation extraction from Humanoid animations must account for root projection and import options. Validate against Unity's evaluated root delta.
- Never allocate in the inner per-candidate search loop. Prefer packed row-major storage and reused buffers after profiling.
- Keep normalizing means/stds immutable and saved with database version. Handle constant-valued dimensions deterministically.
- Distinguish animation root velocity, requested velocity, actual collision-resolved velocity and joint-local velocity in naming.
- Current normal-mode schedule: relay submits horizontal world-space metres; `PlayerController.LateUpdate` first integrates gameplay gravity/carry and steps `CharacterMotor`, then refreshes existing timers/grounding. Preserve frame-local consume-once buffering, reject submissions after the step, and reject repeated same-frame steps. Fixed/manual animation evaluation requires separate scheduling validation.
- `CharacterMotor` is the sole capsule Move owner; gameplay retains vertical velocity, gravity/jump settings and action guards. Reuse existing serialized parameters during this migration rather than adding duplicate configuration or a second scene motor component.
- Grounded matching uses input-owned yaw by default, capped by `MotionMatchingSettings.TurnRadiansPerSecond` (saved/default 1.57 rad/s). `ApplyRootYaw` is experimental. The active 25-clip database has no 180-degree turn clips, so a reversal test does not establish authored half-turn playback. Preserve the separate action/root-rotation policy.
- The dedicated unarmed controller tags Dive Roll `RootMotionAction`; the relay checks current/next base-layer states to preserve authored travel when locomotion Speed is zero. Keep Jump untagged unless its root-travel policy is deliberately changed and tested.

## Playables and graphs

- Own and dispose a `PlayableGraph` explicitly. Do not have an Animator state machine and a Playables graph independently drive the same skeleton.
- The live graph has one output mixing the retained action controller with matched clips. During grounded matching, the retained controller branch has zero weight; jump/dive and fallback use that controller. Crossfade is the saved/default transition path; experimental inertialization remains disabled pending Humanoid interrupted-pose continuity validation.
- Ensure RigBuilder and any procedural constraints have a documented evaluation order before adding foot IK.
- A search result selects a clip/time; state transition mechanics and root motion application belong to separate components.
- Reset/seek clip events deliberately; do not inadvertently replay footsteps on a match jump.

## Testing and telemetry

- Unit test feature math, finite differences, nearest-neighbor costs, coordinate transforms and KD-tree search against exact oracle.
- Unity EditMode tests for sample extraction and serialized asset validation; PlayMode tests for root/capsule lifecycle, gravity, input and locomotion.
- Profile on a defined dataset and machine; report per-query cost, GC allocations, frame time, memory footprint and visual quality.
- Treat a static Inspector/report observation as **not** a runtime test. Do not claim it compiled if Unity did not run.
- Current validation summary and open gates are in [STATUS.md](STATUS.md); exact M2 commands/results are in [the M2 report](migration/M2_IMPLEMENTATION_REPORT.md). Earlier M1/C1 results remain historical evidence. The focused reversal PlayMode test passed 1/1 at 60/10 Hz; EditMode passed 103/103, Python 32/32 and the static assembly audit passed. Two attempted combined PlayMode runs were invalidated by mid-run script recompilation; the prior valid 39/42 result included the known inertialization interruption failure and two Jump/Dive Roll facade failures. Distinguish fresh Console checks from archived diagnostics; no intermittent assertion fix is established. Terrain/contact matrices and Player builds remain unverified.

## Performance-oriented coding and review

- Treat performance as a measured requirement, not a promise made by coding style. A choice of algorithm or coding pattern alone cannot guarantee that frame drops will not occur; hardware, rendering, synchronization and workload also determine frame time.
- Before optimizing, state the gameplay scenario, target platform, target frame budget and measured baseline. Use frame time in milliseconds and inspect median, P95 and worst-frame behavior after warm-up. A 60 FPS target has a 16.67 ms total frame budget across all work; it is a target, not a claim that this project currently meets it.
- Profile a Development Player on the target platform for game-performance conclusions. Profile the Editor separately when investigating Editor overhead. Deep Profiling adds overhead; use it only as a follow-up when normal markers and call stacks do not explain a hotspot. Separate active CPU work from waits, rendering, animation jobs and GPU/presentation time before assigning a cause.
- The current Editor capture's frame 190 took 110.81 ms, including 98.38 ms of presentation wait; matching Update/LateUpdate medians were 0.103/0.029 ms. This does not establish Player performance. Static markers now scope query build, KD search, selection, transition, database initialization, root relay and motor step; capture those stages in a Development Player before drawing performance conclusions.
- Add low-overhead `ProfilerMarker` scopes around substantial project-owned hot paths whose costs need attribution, such as trajectory construction, candidate search, selection policy, graph transitions, root-motion submission and motor work. Keep marker names stable and avoid per-frame diagnostic logging or allocations.
- For every per-frame or per-candidate algorithm, identify its input size and expected time and memory complexity. Avoid accidental nested scans or work that scales with the whole database when only a local update is required. Precompute immutable data, reuse buffers and rebuild indexes only when their inputs change. Choose asymptotically more complex structures only when benchmarks on the real data show a benefit; retain an exact reference implementation where correctness depends on search results.
- Keep recurring managed allocations out of measured hot paths where practical: reuse collections and arrays, avoid temporary LINQ/iterator/closure work in update loops, and cache stable component references. Confirm allocation call stacks in the Profiler. `GC.Alloc` reports allocation size and is not itself a reliable measure of the allocation's CPU duration; measure surrounding work and GC pauses as well.
- Do not trade correctness, input responsiveness, collision behavior or animation quality for a lower operation count without an explicit acceptance test. Validate optimizations against the same scenarios and data, report frame-time distributions, CPU thread markers, animation/job cost, allocations and visual behavior, and keep before/after evidence.
- When a report shows presentation or synchronization waits, do not label them as script execution or infer a GPU bottleneck without corresponding render-thread/GPU evidence. Instrument the missing stage and repeat the controlled capture before changing gameplay algorithms.

Unity references: [Profiler markers (6000.6)](https://docs.unity.com/en-us/engine/6000.6/manual/analysis/profiler/markers), [CPU Usage Profiler (6000.6)](https://docs.unity.com/en-us/engine/6000.6/manual/analysis/profiler/visualizing-data/cpu/cpu), [tracking managed allocations (6000.6)](https://docs.unity.com/en-us/engine/6000.6/manual/analysis/performance-memory/managed-memory/track-garbage-collection), and [Unity performance profiling best practices](https://unity.com/how-to/best-practices-for-profiling-game-performance).

## Repository discipline

- Small, reviewable commits; avoid moving unrelated Unity assets during locomotion changes.
- No hardcoded absolute machine paths, secrets, platform-specific file separators or hidden external services.
- Preserve existing `Packages/manifest.json`, input asset, scenes and `.meta` until a deliberate migration commit.
- Keep `Library/`, `Temp/`, `Logs/`, `Obj/`, generated build artifacts and local training environments untracked via existing `.gitignore` (merge rules carefully; don't overwrite it).
- Preserve Git LFS rules already in the repository for large FBX/animation assets; review usage rights before publishing anything.
- Check settings diffs after Editor runs. M1 restoration details are historical; never restore an old snapshot over newer user changes without checking ownership and authorization.
- New public APIs and tools must have examples, tests and documentation.

## Naming and style

- C# `PascalCase` for types/methods/properties, `_camelCase` private instance fields, `I` prefixes for interfaces.
- `Runtime`, `Editor`, `Tests` assembly separation when creating a reusable package; avoid assemblies that are too granular.
- All project-owned C# belongs beneath `Assets/Scripts/MotionMatching`, inside the existing five assembly boundaries. Core is engine-independent; Runtime references Core, Input System and Cinemachine. Editor contains sampling/baking and audit/capture tooling and is restricted to the Editor platform. Tests live under `Tests/EditMode` and `Tests/PlayMode`. No project-owned script should fall into Assembly-CSharp. Third-party assets remain outside this root. No Neural assembly exists.
- Original source animations are under `Assets/Animations/MotionMatching/Source`; the current active database also references imported Kevin Iglesias clips under `Assets/Kevin Iglesias/`. The saved active asset is `Assets/Animations/MotionMatching/Databases/HumanBasicMotions60.asset`; `LocomotionBaseline.asset` is the rollback reference. Preserve importer metadata and source GUIDs. C2 retired LegacyShooter and its obsolete serialized consumers; do not recreate compatibility code. Do not create empty feature/package directories.
- Use explicit units in names: `Seconds`, `MetersPerSecond`, `RadiansPerSecond`, `FrameIndex`.
- Do not add abstractions until at least two genuine uses justify them.
