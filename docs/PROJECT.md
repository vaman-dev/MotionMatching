# PROJECT — Motion Matching: Intelligent Character Locomotion

**Cleanup update (2026-11-24):** The original Test scene, rifle prefab, broken Ch15 variant, stale recovery scenes and nine LegacyShooter scripts have been retired. MotionMatchingTest is the sole enabled build scene; Runtime references Core, Input System and Cinemachine. Earlier preservation/legacy-retention statements below describe the M1/C1 baseline. Fresh cleanup validation and remaining publication limits are in [FINAL_CLEANUP_REPORT.md](migration/FINAL_CLEANUP_REPORT.md).


## Vision

Build a portfolio-grade, fully playable third-person character controller in Unity whose grounded locomotion is driven by motion matching instead of traditional Animator Blend Trees. The project develops practical gameplay and animation engineering experience and demonstrates natural, responsive movement.

Research papers and implementations, including Ubisoft's work, are technical references that inform implementation decisions. Reproducing a paper is not the deliverable; the playable character is.

## Scope

**In:** camera-relative intent; desired velocity and facing; root/joint feature extraction; a reusable animation database; KD-tree-based classical motion matching; a brute-force correctness and benchmark reference; trajectory prediction; Playables playback; smooth transitions; root-motion and collision integration; locomotion polish; diagnostics; comparative benchmarks; later MLP/ANN experiments and Unity Sentis inference.

**Deferred:** jump/dive-roll matching (initially authored actions), motion warping, foot locking, terrain adaptation and full learned stepping/decompression. These may follow the core playable matching system where they improve the demo.

**Out:** weapon pickup, firing, rifle ADS, equipment masks, multiplayer, damage and UI inventory. Remove these only after verifying that their serialized dependencies have been detached.

## Success criteria

The character should show smooth starts and stops, believable acceleration and deceleration, natural turns and pivots, responsive 90°/180° directional changes, continuous poses, minimal planted-foot sliding, and reliable wall/terrain collision. Evaluate both classical and neural modes using the same motion data and gameplay scenarios. Record matching/inference cost, frame performance and memory alongside observed locomotion quality. Algorithms must be correct and maintainable, but algorithm completion alone does not satisfy the project goal.

Engineering foundations remain: a portable, versioned feature database; a single movement/rotation owner per phase; collision-aware motor behavior; useful tests; reproducible benchmarks; and no claims of neural superiority without comparable quality, speed and memory evidence.

## Target environment

- Verified repository configuration: Unity `6000.6.0f1`, URP `17.6.0`, Input System `1.20.0`, Cinemachine `6.6.0`, Animation Rigging `6.6.0`, `CharacterController`, Humanoid avatar.
- User-authorized Pipeline `1.0.1` enables live Editor inspection and Unity test execution through CLI; readiness and scene reads are separate from gameplay validation.
- Neural inference is a later milestone: verify `com.unity.ai.inference` compatibility when M6 begins. It is currently absent; do **not** edit the manifest automatically.
- Training: Python + framework of choice in a **separate** `Research/` environment (not installed into Unity); export a compatible ONNX model after operator validation.

## Historical source evidence and limitations (M1/C1)

`Report.md` is a historical architecture snapshot. Reinspection confirmed its 19 legacy first-party scripts, including `PlayerController`, `PlayerInputReader`, `PlayerAnimator`, `RootMotionRelay` and authored Jump/DiveRoll states. Its recorded HEAD was baseline commit `13c8484`, which omitted substantial existing working-tree changes. PR #1 subsequently merged as `efad2d3`; consolidation starts from updated origin/main while preserving the dirty local tree. Current findings and the implementation sequence are in `migration/REPOSITORY_ASSESSMENT.md`; actual execution evidence belongs in `PROGRESS.md`.

The original `Assets/Scenes/Test.unity` remains the enabled build scene and its scene bytes are preserved. `Assets/Scenes/MotionMatchingTest.unity` is now the dedicated unarmed scene: live Editor mutation reduced its hierarchy from 102 to 82 GameObjects while retaining seven roots. Pickup, weapon hierarchy/rigging and ADS components were detached. It uses `Assets/Animations/MotionMatching/UnarmedLocomotion.controller`, with one layer and Locomotion, Jump and Dive Roll states. The original controller and source clips remain intact; grounded playback still uses the legacy Animator approach.

`FreeCameraLookInput` on TPSCamera replaces the ADS controller's Look binding using the same input asset/action and a valid CameraTarget follow target. The relay exempts states tagged `RootMotionAction` from its Speed idle filter; only Dive Roll is tagged in the new controller. Post-detachment Unity tests and a confirmed scripted capture exercised these changes, including zero-input dive travel and free-camera Look.

The five requested assembly boundaries live beneath `Assets/Scripts/MotionMatching`: pure C# Core, Unity Runtime, Editor, and separate EditMode/PlayMode tests. Input, gameplay states, animation relay, camera and the single motor now share Runtime. Referenced original-scene shooter compatibility remains under `Runtime/LegacyShooter`; no weapon system is active in the dedicated scene. Editor code is isolated and third-party code remains outside custom roots. Runtime explicitly references Animation Rigging for retained compatibility code. No Neural assembly, motion database, matching backend or neural runtime is implemented.

M1 Unity execution passed seven EditMode and six PlayMode tests, including callback-free fall/jump regressions that failed before motor decoupling. The confirmed 1,140-sample, 60 Hz capture measured stable walk/sprint/turn speeds of 1.626/5.524/1.673 m/s, jump ascent of 1.330 m, dive travel of 3.275 m, zero measured idle/post-dive drift and approximately 58.725 degrees of free-camera Look. A rendered screenshot showed unarmed walking. Final Editor inspection found the scene clean with 82 objects/seven roots, compilation failure false and zero current Console errors; historical corrected-tooling errors remain recorded separately.

Exact artifacts and limits belong in `PROGRESS.md` and the assessment. Slopes, ledges, coyote boundaries and contact/foot-slide quality are not fully verified; no player build ran. Earlier isolated motor comparison found stable speed changes below 0.5% and camera offset below 0.8 mm; removal of traversal geometry prevents an identical whole-route comparison with the final capture.

Current folder ownership is documented in [DIRECTORY_LAYOUT.md](migration/DIRECTORY_LAYOUT.md). All project-owned scripts now have one source root; existing source animations move with their metadata to `Assets/Animations/MotionMatching/Source`. The historical Report is preserved byte-for-byte under docs; its relative links and old paths describe its original snapshot. Consolidation test results are recorded separately from the M1 baseline in PROGRESS.

## Current state and next milestone

Foundation, directory consolidation, local legacy cleanup and the M2 database baker are implemented. M2 samples five unarmed clips at 60 Hz into 716 searchable rows, retaining 27D matching features, roots, joint velocities and 52 mapped local bone poses. Repeated baking and asset reload are byte-identical in the current Editor environment; the baker window provides trajectory and velocity inspection. See the [M2 report](migration/M2_IMPLEMENTATION_REPORT.md) and [feature contract](data/FEATURE_SCHEMA.md).

Fresh M2 validation passed 32 EditMode, 6 PlayMode and 31 Python tests, plus all 15 locomotion comparisons over a 1,141-frame capture. The historical Editor assertion recurred after capture restoration; a settled controlled Play cycle had zero errors/warnings, but the assertion's cause remains unresolved. Local model/skybox prerequisites, expanded source coverage, terrain/contact validation and a target player build remain open. Grounded gameplay still uses Animator and the existing CharacterMotor.

The next technical milestone is M3: trajectory prediction, the brute-force correctness reference, primary KD-tree matching and validated matching playback over this common database. The final success criterion is a playable portfolio demo whose movement quality is immediately visible, with the database, algorithms and performance evidence supporting that result.
