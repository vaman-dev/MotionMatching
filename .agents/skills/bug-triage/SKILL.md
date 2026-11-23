---
name: bug-triage
description: Diagnose Unity motion matching bugs with reproducible steps, tracing root ownership and coordinate conventions.
---

# Bug triage skill

1. Read `agents/bug-agent.md` and `MotionMatchingArchitecture.md`.
2. Record scene, game state, input, frame/time, clip/frame, database/schema, motor grounded state, actual vs desired velocity.
3. Trace query construction -> search -> selected clip -> Playables -> root delta -> CharacterController; find competing writers.
4. Reproduce if Unity is available, otherwise mark `unverified` explicitly.
5. Form a falsifiable root-cause hypothesis; specify a single minimal experiment and a regression test.
6. Do not solve data coverage problems by hiding them with magic-number interpolation.
