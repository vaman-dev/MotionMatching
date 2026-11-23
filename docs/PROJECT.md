# PROJECT — Motion Matching and Learned Animation

## Vision

Create a professional **research and engineering** implementation of third-person character locomotion in Unity: first database-driven motion matching, then search acceleration, then a reproducible learned approximation with ONNX inference through Unity Sentis.

The project is intentionally a single-character locomotion laboratory, **not** a shooter and not an all-purpose animation framework on day one.

## Scope

**In:** camera-relative intent; desired velocity and facing; root/joint data extraction; feature normalization; pose and trajectory search; Playables playback; inertial or otherwise measured transition quality; collision-aware character motor; diagnostic visualization; benchmarking; eventual ANN experiments and runtime inference.

**Deferred:** jump/dive-roll matching (initially authored actions), motion warping, contacts/foot locking, terrain adaptation, neural stepping and decompression, generic editor tooling.

**Out:** weapon pickup, firing, rifle ADS, equipment masks, multiplayer, damage and UI inventory. Remove these only after verifying that their serialized dependencies have been detached.

## Engineering objectives

1. Correct results before speed; use brute force as reference oracle.
2. A portable, versioned animation dataset and feature schema shared by all backends.
3. A single movement/rotation owner per phase; preserve collision and vertical simulation.
4. Tests for math and storage, Unity EditMode tests for sampling, PlayMode checks for motor/animation.
5. Benchmarks recorded with dataset hash, feature version, platform and warm-up conditions.
6. No claims of neural superiority without quality/speed/memory evidence.

## Target environment

- Verified repository configuration: Unity `6000.6.0f1`, URP `17.6.0`, Input System `1.20.0`, Cinemachine `6.6.0`, Animation Rigging `6.6.0`, `CharacterController`, Humanoid avatar.
- User-authorized Pipeline `1.0.1` enables live Editor inspection and Unity test execution through CLI; readiness and scene reads are separate from gameplay validation.
- Neural inference is a later milestone: verify `com.unity.ai.inference` compatibility when M6 begins. It is currently absent; do **not** edit the manifest automatically.
- Training: Python + framework of choice in a **separate** `Research/` environment (not installed into Unity); export a compatible ONNX model after operator validation.

## Source evidence and limitations

`Report.md` is a historical architecture snapshot. Reinspection confirmed its 19 legacy first-party scripts, including `PlayerController`, `PlayerInputReader`, `PlayerAnimator`, `RootMotionRelay` and authored Jump/DiveRoll states. Its recorded HEAD was baseline commit `13c8484`, which omitted substantial existing working-tree changes. PR #1 subsequently merged as `efad2d3`; consolidation starts from updated origin/main while preserving the dirty local tree. Current findings and the implementation sequence are in `migration/REPOSITORY_ASSESSMENT.md`; actual execution evidence belongs in `PROGRESS.md`.

The original `Assets/Scenes/Test.unity` remains the enabled build scene and its scene bytes are preserved. `Assets/Scenes/MotionMatchingTest.unity` is now the dedicated unarmed scene: live Editor mutation reduced its hierarchy from 102 to 82 GameObjects while retaining seven roots. Pickup, weapon hierarchy/rigging and ADS components were detached. It uses `Assets/Animations/MotionMatching/UnarmedLocomotion.controller`, with one layer and Locomotion, Jump and Dive Roll states. The original controller and source clips remain intact; grounded playback still uses the legacy Animator approach.

`FreeCameraLookInput` on TPSCamera replaces the ADS controller's Look binding using the same input asset/action and a valid CameraTarget follow target. The relay exempts states tagged `RootMotionAction` from its Speed idle filter; only Dive Roll is tagged in the new controller. Post-detachment Unity tests and a confirmed scripted capture exercised these changes, including zero-input dive travel and free-camera Look.

The five requested assembly boundaries live beneath `Assets/Scripts/MotionMatching`: pure C# Core, Unity Runtime, Editor, and separate EditMode/PlayMode tests. Input, gameplay states, animation relay, camera and the single motor now share Runtime. Referenced original-scene shooter compatibility remains under `Runtime/LegacyShooter`; no weapon system is active in the dedicated scene. Editor code is isolated and third-party code remains outside custom roots. Runtime explicitly references Animation Rigging for retained compatibility code. No Neural assembly, motion database, matching backend or neural runtime is implemented.

M1 Unity execution passed seven EditMode and six PlayMode tests, including callback-free fall/jump regressions that failed before motor decoupling. The confirmed 1,140-sample, 60 Hz capture measured stable walk/sprint/turn speeds of 1.626/5.524/1.673 m/s, jump ascent of 1.330 m, dive travel of 3.275 m, zero measured idle/post-dive drift and approximately 58.725 degrees of free-camera Look. A rendered screenshot showed unarmed walking. Final Editor inspection found the scene clean with 82 objects/seven roots, compilation failure false and zero current Console errors; historical corrected-tooling errors remain recorded separately.

Exact artifacts and limits belong in `PROGRESS.md` and the assessment. Slopes, ledges, coyote boundaries and contact/foot-slide quality are not fully verified; no player build ran. Earlier isolated motor comparison found stable speed changes below 0.5% and camera offset below 0.8 mm; removal of traversal geometry prevents an identical whole-route comparison with the final capture.

Current folder ownership is documented in [DIRECTORY_LAYOUT.md](migration/DIRECTORY_LAYOUT.md). All project-owned scripts now have one source root; existing source animations move with their metadata to `Assets/Animations/MotionMatching/Source`. The historical Report is preserved byte-for-byte under docs; its relative links and old paths describe its original snapshot. Consolidation test results are recorded separately from the M1 baseline in PROGRESS.

## Definition of done

The C1 source consolidation has fresh 7 EditMode / 6 PlayMode / 31 Python passes
and a passing 1,141-frame input regression capture. All three scenes and the rifle
prefab resolve the moved scripts. Three current Console entries and two unused
prefab defects remain documented preexisting limits; this is not a project-wide
zero-error certification. See [the consolidation report](migration/CONSOLIDATION_REPORT.md).

Brute force and KD-tree produce the same best eligible candidate (within explicit tie rules); animation playback doesn't double-apply root motion; locomotion and collision cases pass; benchmarks are reproducible; later neural mode has an explicit reference baseline and graceful classical fallback.
