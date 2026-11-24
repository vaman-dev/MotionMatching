# PLAN — milestones and hard exit gates

Never mark a milestone complete from code inspection alone. Progress evidence is logged in `PROGRESS.md` and is required at every gate.

## M0 — Preserve and characterize the original project

- [x] Back up tracked and nonignored untracked working files, including existing metadata and recorded deletions; verify archive hashes. Local evidence: `.utmp/m0-baseline/manifest.json` (ignored local files are outside this source snapshot).
- [x] Inspect source/assets/settings against Report; validate agent/skill references and available repository tooling.
- [x] Install user-authorized Unity Pipeline 1.0.1 through CLI and verify a ready live Editor plus open-scene/hierarchy reads (102 objects in MotionMatchingTest).
- [x] Run Unity `6000.6.0f1` batch import/script compilation; capture baseline log. This does not close gameplay gates.
- [x] Observe original Test and cloned scene in Unity `6000.6.0f1`; record compilation and interactive capture evidence.
- [x] Record baseline idle/walk/sprint/turn/jump/dive-roll and camera offset/collision observations, including blocked walking and zero-travel dive defects.
- [x] Measure actual horizontal capsule speed; the old Animator `Speed=0.5/1.0` has no m/s meaning.
- [x] Create `feature/motion-matching-migration` and byte-identical `MotionMatchingTest` scene clone with a unique scene GUID; preserve Test, controller and build settings.

**Exit:** reproducible snapshot and known baseline defects; original scene untouched.

Current execution: initial M1 implementation and focused validation completed. `migration/REPOSITORY_ASSESSMENT.md` defines the staged plan. Original/clone/motor captures establish measured baselines; full rendered contact and terrain coverage remains an explicit follow-up below.

## M1 — Strip shooter dependencies and separate motor ownership

- [x] Map and detach weapon/ADS/IK/overlay dependencies in the dedicated scene; preserve original source assets.
- [x] Retain free camera, input, visual character, ground detection, actions, state machine, motor and environment collision; remove the weapon, table and nearby pickup sphere setup.
- [x] Schedule gravity independently of root callbacks and establish one `CharacterController.Move` owner.
- [x] Validate callback-free gravity, jump/apex/landing, stale-root rejection and wall collision before animation provider replacement. Retain original coyote settings/guards.
- [x] Remove rifle graph and equipment writers from dedicated scene; retain inactive bool parameters for legacy facade compatibility.
- [x] Preserve free-camera Look binding with a separate adapter and unarmed controller; validate mouse-driven orbit.
- [x] Introduce and compile Core, Runtime, Editor and two test assemblies with the documented acyclic graph; no Neural assembly. Keep third-party assets outside custom assembly roots.
- [x] Fix zero-speed dive root suppression through a dedicated authored-action tag; verify travel and ordinary idle drift.
- [ ] Expand terrain/contact validation: slopes, steps, ledges, coyote boundary/retrigger cases, rendered contact quality and target player build. These are not certified by focused tests/captures.

**Exit:** clean locomotion-only scene compiles; idle, jump/fall and collision remain functional; motor has one writer.

Focused exit evidence: seven EditMode tests, six PlayMode tests, 1,140 samples at 60 Hz with successful Editor restoration; see latest PROGRESS entry. Grounded animation still uses the retained Animator. Directory consolidation C1 precedes M2; finish the expanded locomotion matrix before changing playback in M3.

## C1 - Repository refactoring and directory consolidation

This organizational milestone precedes M2 and preserves grounded Animator playback and movement behavior.

- [x] Audit every project-owned script and serialized dependency; record KEEP/MOVE/REFACTOR/DELETE/REVIEW decisions before removal.
- [x] Consolidate source, five asmdefs and tests beneath `Assets/Scripts/MotionMatching`, preserving metadata, namespaces and script GUIDs; verify intentional compiled-assembly identity changes in Unity.
- [x] Separate retained animation assets into `Assets/Animations/MotionMatching` without duplication or source deletion.
- [x] Retain referenced shooter compatibility for original assets; remove only verified empty old code containers.
- [x] Centralize project documents under docs; update discovery paths and document dependency boundaries.
- [x] Rerun Unity compilation, Console checks, EditMode and PlayMode tests after relocation. Compilation and 7/6 tests passed; Console defects are documented below, not certified clear.
- [x] Rerun Python/static scene, Animator, GUID and assembly validation; verify build-scene configuration. 31 fixtures passed; all three scenes/rifle script references resolve, old unused prefab defects remain.
- [x] Rerun input/locomotion/jump/dive/free-camera regression capture and obtain specialist review. 1,141 frames, all 16 comparisons passed.
- [ ] Close the project-wide zero-error gate: investigate the existing Editor assertion and decide recovery of the preexisting broken variant/missing prefab scripts. No unrelated asset repair is included in C1.

**Exit:** one project-owned source root, preserved GUIDs/working behavior, no dangling references or unexpected predefined-assembly capture, fresh evidence in PROGRESS. No commit, push or PR without explicit approval for this milestone. Next technical work is M2 animation feature extraction/database baking.

Historical C1 result: consolidation and focused regression checks completed with the broader zero-error gate open; see [the C1 report](migration/CONSOLIDATION_REPORT.md). C2 below supersedes the local Console/prefab gate with fresh validation. The historical assertion's cause, expanded terrain/contact/player-build coverage and clean-checkout prerequisites remain unverified.

## C2 — Final legacy cleanup

- [x] Audit and retire obsolete serialized shooter consumers, preserving local recovery copies.
- [x] Remove the nine unreferenced compatibility scripts and unused Runtime package reference.
- [x] Repair/retire broken unused prefabs and select MotionMatchingTest for builds.
- [x] Run fresh Unity tests, compiled/reference audits, Python checks and locomotion capture; document zero current Console errors/warnings.
- [ ] Establish an asset-complete clean checkout and target player build; resolve redistribution of local character/skybox prerequisites.

The older Editor assertion did not reproduce; its root cause remains unproven. See [FINAL_CLEANUP_REPORT.md](migration/FINAL_CLEANUP_REPORT.md). No algorithm implementation was introduced.

## M2 — Animation feature extraction and motion database

- [ ] Inventory `.anim` and `.fbx` candidates by actual motion coverage, not filenames.
- [ ] Author fixed sample rate, local-coordinate and root conventions with golden-case tests.
- [ ] Bake joint/root velocities; 27D reference features; flags and valid frame ranges.
- [ ] Store schema and source hashes with baked data; test round-trip and cross-platform float tolerance.
- [ ] Add Unity Editor visualization: sampled root path, foot vectors, contact markers and bad frames.

**Exit:** a representative animation subset deterministically bakes, reloads and visualizes correctly, ready to drive a playable matching controller.

## M3 — KD-tree motion matching: first playable system

- [ ] Build trajectory prediction from player intent and collision-resolved motor state.
- [ ] Implement brute-force nearest-neighbor search as the correctness oracle and benchmark reference.
- [ ] Implement KD-tree search as the first primary matching backend over the same normalized feature rows and eligibility rules.
- [ ] Measure KD-tree candidate parity, query time, build time and memory against brute force; define deterministic tie behavior.
- [ ] Play selected clip/frame through Animation Playables with controlled transitions and root-motion/capsule integration.
- [ ] Add EditMode and PlayMode coverage for query construction, search, transitions, motor ownership and collisions.

**Exit:** a player can move, start, stop, run, turn, reverse and encounter obstacles using matching poses without abrupt snapping or double-applied root motion.

## M4 — Natural locomotion polish

- [ ] Improve starts/stops, acceleration/deceleration, turning, pivots, reversals and responsive directional changes.
- [ ] Inspect pose continuity and planted-foot sliding in representative gameplay scenarios; add foot locking only when evidence warrants it.
- [ ] Verify wall and terrain collision behavior and root-motion reconciliation.
- [ ] Add a visual matching/trajectory diagnostic suitable for iteration and demonstration.
- [ ] Record repeatable quality and performance measurements for the classical controller.

**Exit:** the classical controller is a credible playable locomotion system, with its quality and known limits demonstrated in the project scene.

## M5 — MLP/ANN matching experiment

- [ ] Define precise prediction target (feature/latent, not arbitrary frame ID).
- [ ] Export labeled train/val/test queries; split by source sequences to avoid temporal leakage.
- [ ] Train MLP with documented architecture, seeds, normalization and metrics.
- [ ] Benchmark quality **against** the classical KD-tree controller on unseen trajectories and actions.

**Exit:** reproducible training and an independently evaluated neural matching experiment, compared with the classical controller on the same data and gameplay scenarios.

## M6 — Unity Sentis/Inference Engine integration

- [ ] Install a tested `com.unity.ai.inference` version compatible with this Editor.
- [ ] Export supported ONNX; verify model/operator and shape compatibility.
- [ ] Compare Unity inference outputs with Python for a fixed golden fixture.
- [ ] Profile CPU/GPU behavior and memory/tensor lifecycle; fall back to the classical KD-tree controller on failure.

**Exit:** repeatable numerical parity, gameplay comparison, performance and memory measurements, and a reliable classical fallback.

## M7 — Portfolio demo

- [ ] Deliver a polished playable build and clearly documented controls/setup.
- [ ] Provide a motion visualizer and comparable classical/neural modes where available.
- [ ] Capture a comparison video and publish reproducible benchmarks and limitations.
- [ ] Review project presentation, scene startup, visual quality and documentation as a portfolio handoff.

**Exit:** a reviewer can run and play the demo, see the movement quality, inspect useful diagnostics and understand the engineering tradeoffs from the included documentation.

## Optional follow-up — fuller learned motion matching

- [ ] If useful to the portfolio goals, study and implement learned Projector, Stepper and pose compression/decompression components.
- [ ] Validate pose reconstruction, temporal stability, locomotion quality, memory and performance against the existing classical and MLP modes.

This optional work does not replace the playable classical and portfolio milestones.

## Publishing gate

No Asset Store package extraction until a component has a clear standalone API, sample scene, tests, XML/API documentation, license clearance and verified external-project import. See `ReusablePackages.md`.
