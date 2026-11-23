---
name: motion-data-audit
description: Inspect and validate animation clip metadata, root trajectories and 27D feature extraction before any database bake.
---

# Motion data audit skill

1. Read `MotionMatchingArchitecture.md`, `docs/data/FEATURE_SCHEMA.md`, original `Report.md`.
2. Inventory actual animation source, Avatar, looping/import settings, root curves, foot contact and gait coverage.
3. Check whether the sampled hips track root motion; for Humanoid root, verify projected/evaluated `Animator.deltaPosition` semantics and import configuration.
4. Validate sample Hz, clip length, horizon boundaries and consistent Unity X-right Y-up Z-forward local basis.
5. Report invalid data, naming ambiguity, mismatched scale and missing turn/stop coverage.
6. Re-bake only on explicit instruction; never replace source animations.
