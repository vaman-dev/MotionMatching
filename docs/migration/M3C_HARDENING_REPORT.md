# M3C incremental hardening report

Updated 2026-11-25. **Work in progress:** dataset expansion, seek continuity, root-space correction, intent springs and complete-cost selection penalties are implemented with focused evidence. Experimental inertialization passes 8/9 checks but fails interrupted Humanoid pose continuity at 60 Hz; it remains disabled in the scene, with crossfade playback the default. Final combined acceptance remains pending. This report does not close M2.5, the M3 playable exit or M4.

## Dataset and preservation

`Assets/Animations/MotionMatching/Databases/HumanBasicMotions60.asset` is the newly assigned MotionMatchingTest database: 25 Kevin Iglesias Human Basic Motions FREE locomotion clips, 60 Hz, 27D, schema/binary v1, 1,276 rows, all searchable. `LocomotionBaseline.asset` remains the nine-clip rollback database (1,026 rows, 906 searchable), retained through the component's `PreviousDatabase` reference. The original database and animation sources are preserved. Local source/derived-data redistribution clearance is still required before publication.

The Movement folder inventory contains 31 clips. Six authored actions/idle transitions are excluded from this grounded locomotion bake. Their per-clip motion/import/parity audit export and the owner's complete inventory/rebake review remain pending at this sync. Do not interpret the folder count or audit script as proof that all 31 clips passed per-value review.

The usable 25-clip evidence is `.utmp/m3-smooth/dataset-inventory-baked.json` and `dataset-health.json`. All selected sources are Humanoid and imported as loops. Names below omit the `HumanM@` prefix and `[RM]` suffix. Speed ranges are sampled root speeds in m/s, not final gameplay speeds. Left/right turn yaw is measured over sampled rows, rather than a claim about an exact authored endpoint.

| Clip | Duration (s) | Rows | Root speed (m/s) | Sampled yaw (degrees) |
| --- | ---: | ---: | ---: | ---: |
| Idle01 | 2.7 | 162 | 0 | 0 |
| Idle02 | 2.7 | 162 | 0 | 0 |
| Run01_Backward | 0.6 | 36 | 3.856 | 0 |
| Run01_BackwardLeft | 0.6 | 36 | 3.856 | 0 |
| Run01_BackwardRight | 0.6 | 36 | 3.856 | 0 |
| Run01_Forward | 0.6 | 36 | 3.856 | 0 |
| Run01_ForwardLeft | 0.6 | 36 | 3.856 | 0 |
| Run01_ForwardRight | 0.6 | 36 | 3.856 | 0 |
| Run01_Left | 0.6 | 36 | 3.856 | 0 |
| Run01_Right | 0.6 | 36 | 3.856 | 0 |
| Sprint01_Forward | 0.533333 | 32 | 5.784 | 0 |
| Sprint01_ForwardLeft | 0.533333 | 32 | 5.807–7.154 | 0 |
| Sprint01_ForwardRight | 0.533333 | 32 | 5.807–7.154 | 0 |
| Sprint01_Left | 0.533333 | 32 | 5.830–8.300 | 0 |
| Sprint01_Right | 0.533333 | 32 | 5.830–8.300 | 0 |
| Turn01_Left | 1 | 60 | 0 | -88.653 |
| Turn01_Right | 1 | 60 | 0 | 88.641 |
| Walk01_Backward | 0.8 | 48 | 1.928 | 0 |
| Walk01_BackwardLeft | 0.8 | 48 | 1.928 | 0 |
| Walk01_BackwardRight | 0.8 | 48 | 1.928 | 0 |
| Walk01_Forward | 0.8 | 48 | 1.928 | 0 |
| Walk01_ForwardLeft | 0.8 | 48 | 1.928 | 0 |
| Walk01_ForwardRight | 0.8 | 48 | 1.928 | 0 |
| Walk01_Left | 0.8 | 48 | 1.928 | 0 |
| Walk01_Right | 0.8 | 48 | 1.928 | 0 |

Health reports zero errors, invalid feature values, constant dimensions, excluded rows and abnormal frames. Eleven clips warn that at least one foot has no estimated contacts: five run directions (Forward, ForwardLeft, ForwardRight, Left, Right), all five sprint clips, and Walk01_Left. Contact flags are heuristics; these warnings require inspection and do not automatically reject clips. Variable lateral/diagonal sprint root speed also merits quality review. Directional coverage expands, but authored starts/stops, acceleration/deceleration, pivots and general transitions remain unverified. Loop import and searchable turn rows do not certify perceptual turn-loop continuity.

## Verified causes and focused results

### Repeated selection seeks and root stalls

The corrected A/B/C comparison uses retained Animator playback (A), fixed matched-clip playback (B) and live matching/search playback (C), with 170 measured frames per mode on the **old database**. Walk capture `playback-comparison-20261125-140952.csv` records zero stalls in A/B, but ten zero-displacement frames and ten seeks in C. The zero frames still carried nonzero raw root magnitude (about 0.028882 m), separating lost applied motion from a stationary animation source. `zeroWithSwitch=0` means the summary does not claim every stall and seek occurred on the same sampled frame.

After seek/root-continuity hardening, `playback-comparison-20261125-142203.csv` records zero stalls and two seeks in C. Its maximum speed fell from 2.658157 to 1.802294 m/s; A/B also had zero stalls. The sprint comparison `playback-comparison-20261125-140215.csv` had zero stalls/seeks in all three modes and therefore did not reproduce the walk issue.

These figures come from `.utmp/m3-smooth/playback-summary.json`. They precede the later Avatar/world-scale correction and new-database combined validation. They establish a bounded seek-continuity improvement under that experiment, not final HumanBasicMotions60 movement quality, speed or performance.

### Avatar-local velocity and humanScale conversion

The stronger real-Avatar root fixture initially passed 5/11 and failed all six moving-clip cases at yaw 0, 90 and 173 degrees, at 60/120 Hz (`root-parity-before.json`). At 60 Hz the fixture reported native distance about 3.116898 m versus extracted distance 3.067929 m with `humanScale=1.015962`. Position errors increased with yaw, reaching 0.0636431128 m per checked delta. This exposed the extraction path's Avatar-local/world conversion and Humanoid scale mismatch; the earlier seven-case root result did not establish that stronger parity.

After correcting the conversion, the same eleven cases passed (`root-parity-after.json`): six moving-root cases, four left/right quarter-turn cases at 60/120 Hz, and blend-slot evaluation/disposal. The moving-root assertions require position error below 0.0001 m; the artifact records pass/fail rather than post-fix error maxima. CharacterMotor retains sole capsule movement, gravity remains independently scheduled, input yaw stays default and root yaw remains experimental.

### Intent springs and complete-cost selection

`MotionMatchingController` smooths desired horizontal velocity and facing with `MotionSpring`; prediction consumes the smoothed intent while collision-resolved velocity still initializes motion prediction. Facing follows the shortest angular difference. Intent history resets when matching yields to retained actions. This smooths the query target; it does not establish continuity of the displayed pose or grant the query ownership of capsule/root movement.

`MotionSearchContext` adds a nonnegative switch penalty to each candidate's complete cost inside both brute-force and KD-tree selection. Natural continuation within the current clip's time window is unpenalized, including circular phase distance for loops. The KD-tree compares complete costs during search; the penalty is not merely attached after returning the unpenalized winner. Runtime selection additionally checks absolute/relative improvement, switch interval and permitted blend interruption, with changed intent allowing an urgent interruption. The oracle must use the same query, weights, eligible rows and context.

Settings below are `MotionMatchingSettings` class defaults, not a claim that every experiment used them. The scene's `InertializationEnabled` remains false.

| Setting | Default | Purpose |
| --- | ---: | --- |
| `VelocityHalfLife` / `FacingHalfLife` | 0.06 s / 0.06 s | Intent response damping |
| `SwitchPenalty` | 0.5 | Additional complete candidate cost outside continuation |
| `AbsoluteMinimumImprovement` / `RequiredCostImprovement` | 0.05 / 0.15 | Absolute and fractional reselection improvement |
| `ContinuationWindowSeconds` / `MinimumSwitchIntervalSeconds` | 0.2 s / 0.15 s | Natural continuation and ordinary switch spacing |
| `AllowTransitionInterruption` | true | Permit selecting during a blend |
| `IntentInterruptionAngleDegrees` / `IntentInterruptionSpeedChange` | 60 degrees / 1.5 m/s | Urgent intent-change thresholds |
| `BlendSeconds` | 0.15 s | Default crossfade duration |
| `InertializationEnabled` | false | Experimental pose-transition path |
| `PoseOffsetHalfLife` / `RotationOffsetHalfLife` | 0.08 s / 0.08 s | Experimental inertial offset decay |

### Experimental inertialization: failing Humanoid continuity gate

The latest focused experimental run passed 8/9, with `InterruptedTransitionContinuesFromLastDisplayedPoseAndVelocity(60)` still failing: 0.677239239 degrees against the unchanged 0.1-degree gate (`.utmp/m3-smooth/inertial-actual-history.json`). `MotionMatchingController.LateUpdate` now captures actual final bone Transforms read-only for transition history; it does not write Transforms. Requested animation-job bone rotations differ from the final Humanoid result, so recording job outputs alone was not sufficient to establish displayed-pose continuity.

Quaternion-normalization and timestep hypotheses were rejected using normalized/logged rotation metrics and the correct elapsed time. A two-output experiment using ordered `PreviousInputs` did not fix the failure and was reverted. The retained production graph has **one output**. Native-buffer disposal now handles an invalid graph before releasing buffers; that guard is a lifecycle correction, not evidence that pose continuity is solved.

Unity's [UUM-27271 resolution](https://issuetracker.unity.com/issues/18047/wrong-rotations-are-set-when-using-setlocalrotation-in-animation) explains that Humanoid muscle-space conversion and solver limits can change requested rotations; arbitrary bone rotations are not guaranteed to survive exactly. This supports the observed contract limitation, but does not by itself prove the exact cause of every project residual. Future inertialization needs a Humanoid-compatible pose contract and implementation, with final displayed-pose/velocity validation. Preserve the failing test, without Ignore or tolerance relaxation. `InertializationEnabled=false` retains crossfade playback while that work remains open.

The Animator controller has not been removed. Its root-mixer branch has weight zero during grounded matching; it remains available for authored actions and fallback. Neither the disabled experimental path nor a zero grounded branch weight establishes a controller-free implementation.

### Evidence ledger

| Executed check | Result | Evidence / scope |
| --- | --- | --- |
| Independent saved-dataset audit | 7/7 passed | Owner's executed Editor evidence; covers source identities, contract, eligibility, finite reload/normalization and independent source parity. Final artifact reference pending. |
| Penalized search | 3/3 passed | Owner's executed Core search checks; final artifact reference pending. Keep brute force as oracle under identical features, weights, eligibility and penalty. |
| Earlier focused root fixture | 7/7 passed | Earlier narrower check; superseded for real-Avatar moving-root parity by the eleven-case fixture. |
| Stronger root fixture before conversion fix | 5/11 passed, 6 failed | `.utmp/m3-smooth/root-parity-before.json` |
| Same root fixture after conversion fix | 11/11 passed | `.utmp/m3-smooth/root-parity-after.json`, 4.37 s |
| Playable lifecycle/action/seek/tail regression | 7/7 passed | `.utmp/m3-smooth/phase2-playable-results.json`, 4.17 s |
| Spring / inertial math | 14/14 passed | Owner's executed focused math checks; final artifact reference pending. |
| Selection and spring integration checks | 4/4 passed | Owner's executed focused checks; final artifact reference pending. |
| Python regression | 32/32 passed | Owner's executed regression checks; no Unity acceptance inferred. |
| Experimental inertialization | 8/9 passed, 1 failed | `.utmp/m3-smooth/inertial-actual-history.json`, 2.82 s; interrupted 60 Hz continuity remains open. Scene setting stays false. |

Results above are separate runs. Do not add them into an alleged complete-suite rerun. Earlier M3C EditMode 78/78, PlayMode 18/18 and movement captures remain historical evidence in [PROGRESS.md](../PROGRESS.md), preceding these hardening changes. No fresh full combined suite, final new-database A/B/C capture, full-rate visual acceptance or Player-build result is claimed here.

## Staged acceptance plan

1. Preserve the old database and scene rollback reference; export the six excluded candidates before claiming a complete 31-clip audit. Retain the shared feature/schema contract.
2. Keep the verified root-space correction and seek-continuity regression evidence. Compare penalized KD-tree selection to the brute-force oracle using the same metric and policy.
3. Retain the passed 14-case math and four-case selection/spring evidence; finish comparable runtime movement review for starts, release, reversal, facing and varied frame steps. Spring smoothing of intent does not prove pose continuity.
4. Keep crossfade enabled while defining and implementing Humanoid-compatible inertialization. Preserve the failing 60 Hz interruption gate and verify actual displayed pose/velocity, interrupted transitions, reset/disable/action handoff and root-motion authority before enabling the experimental setting.
5. Finish the owner-run all-31 inventory/export and rebake review, current-dataset benchmark, full combined checks and final movement capture; all remain pending at this sync. Repeat comparable A/B/C scenarios on HumanBasicMotions60, record stalls/seeks/root and pose discontinuities, and inspect continuous full-rate rendering. Record contact sliding, wall/terrain behavior and Player performance before closing playable acceptance.

## Source and artifact references

- Dataset contract remains [FEATURE_SCHEMA.md](../data/FEATURE_SCHEMA.md); no schema change is part of this increment.
- Source ownership: [MotionSearch.cs](../../Assets/Scripts/MotionMatching/Core/MotionSearch.cs), [MotionSpring.cs](../../Assets/Scripts/MotionMatching/Core/MotionSpring.cs), [MotionMatchingController.cs](../../Assets/Scripts/MotionMatching/Runtime/Animation/MotionMatchingController.cs), [MotionPlayablePlayer.cs](../../Assets/Scripts/MotionMatching/Runtime/Animation/MotionPlayablePlayer.cs), [MotionRootStream.cs](../../Assets/Scripts/MotionMatching/Runtime/Animation/MotionRootStream.cs), [RootMotionRelay.cs](../../Assets/Scripts/MotionMatching/Runtime/Animation/RootMotionRelay.cs) and [MotionRootMotionTests.cs](../../Assets/Scripts/MotionMatching/Tests/PlayMode/MotionRootMotionTests.cs).
- Ignored local artifacts under `.utmp/m3-smooth/`: `dataset-inventory-baked.json`, `dataset-health.json`, `playback-summary.json`, the three named comparison CSVs, `root-parity-before.json`, `root-parity-after.json` and `phase2-playable-results.json`. Raw local artifacts are not a clean-checkout prerequisite or a publication guarantee.
- [Unity AnimationStream reference](https://docs.unity.cn/6000.1/Documentation/ScriptReference/Animations.AnimationStream.html) documents stream velocity and root-motion fields; [AnimationHumanStream reference](https://docs.unity.cn/6000.0/Documentation/ScriptReference/Animations.AnimationHumanStream.html) documents Avatar humanScale. The coordinate/scale correction above is supported by this project's parity fixture, rather than an assumption that API field descriptions certify world-space conversion.
- Daniel Holden's [Spring-It-On](https://theorangeduck.com/page/spring-roll-call) describes exact spring damping and offset-decay inertialization. It informs the implemented intent springs and experimental transition work; passing spring math does not certify Humanoid displayed-pose continuity.
- Simon Clavet's [GDC 2016 Motion Matching presentation](https://ubm-twvideo01.s3.amazonaws.com/o1/vault/gdc2016/Presentations/Clavet_Simon_MotionMatching.pdf) is separate from Holden et al.'s [ACM TOG 2020 Learned Motion Matching paper](https://doi.org/10.1145/3386569.3392440). This increment remains classical matching; no neural projector, stepper or full Learned Motion Matching implementation is claimed.
