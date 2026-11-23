# ADR-0001 — one CharacterController movement owner

Status: implemented on 2026-11-23; Unity tests and confirmed unarmed locomotion/action/camera capture passed. Full slope/ledge/coyote-boundary/contact-quality matrices remain open.

## Context

The original `RootMotionRelay.OnAnimatorMove()` route forwarded Animator displacement to `PlayerController.ApplyRootMotionMovement()`, which also integrated gravity and called `CharacterController.Move`. Without callbacks, vertical motion stopped. Callback-free fall and jump tests reproduced this before the change.

## Decision

Use the plain C# `MotionMatching.Runtime.CharacterMotor` as the sole capsule displacement owner. `PlayerController` retains serialized gameplay parameters, gravity/jump/carry state and action guards. Its first LateUpdate operation integrates gameplay movement and steps the motor before existing timer/ground updates, regardless of animation callback availability. No extra serialized settings or scene component is introduced.

The relay retains the existing normal-mode animation root source and filtering, but only submits horizontal world-space deltas. Core `RootMotionAccumulator` sums and consumes the current frame's XZ deltas once; Runtime rejects repeated same-frame steps and late submissions. Missing callbacks and disabled capsule/controller paths cannot replay stale motion. The motor combines approved animation and gameplay deltas in one `CharacterController.Move`, exposing actual collision-resolved velocity and collision flags. Existing scene yaw/root-rotation policy and grounded Animator remain unchanged.

## Consequences

- Existing scene values, metadata and grounded animation selection are preserved; the rendered migration matrix remains a separate gate.
- Animator, Playables and gameplay code must not call capsule movement independently.
- Actual displacement is exposed now; trajectory prediction is future work.
- The current schedule supports normal Animator evaluation. Future fixed/manual Playables evaluation requires explicit timing tests.
- The five named assemblies isolate pure buffering, Unity runtime, Editor audit and tests. At M1, legacy gameplay remained in Assembly-CSharp; ADR-0002 consolidates it into Runtime while preserving the motor contract.

## Validation evidence

Unity CLI's final run passed seven EditMode tests (`.utmp/m1-final/editmode-results.json`) and six PlayMode tests (`.utmp/m1-final/playmode-completed.json`). The two legacy callback-free fall/jump tests failed before the patch (`.utmp/m0-baseline/legacy-motor-before-results.json`) and passed afterward. Coverage includes root consume-once behavior, stale/late callbacks, disabled capsule, wall collision, gravity without an Animator, authored jump ascent/apex/landing, assembly import and dedicated unarmed scene/controller references.

Shooter scene detachment is complete. The confirmed 1,140-sample 60 Hz capture (`.utmp/m1-final/locomotion-confirm.txt`, metrics in `locomotion-confirm-analysis.json`) completed and restored temporary state successfully with no sample errors. It measured 1.330 m jump ascent, 3.275 m dive travel, zero idle/post-dive drift and approximately 58.725 degrees of free-camera Look. The Dive Roll `RootMotionAction` tag bypasses the relay's idle Speed filter, correcting the earlier zero-travel action. A rendered screenshot showed unarmed walking. These results do not certify full slopes/ledges/coyote boundaries, all dive timing windows, contact quality or foot sliding; no player build ran.
