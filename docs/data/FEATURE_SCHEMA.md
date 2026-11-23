# Versioned motion-feature schema contract

`config/motion-matching.sample.json` is a proposal, **not** live runtime configuration or a baked database.

## V1 reference features

- 60 fixed samples per second in the offline baker.
- Three future points at +20, +40 and +60 frames = +0.333333, +0.666667 and +1.0 seconds.
- Features ordered: future XZ positions (6), future XZ facing directions (6), left/right foot XYZ positions (6), left/right foot XYZ velocities (6), pelvis XYZ velocity (3).
- All joint features expressed in a declared **character-relative** frame (X right, Y up, Z forward); trajectory facing normalized in the XZ ground plane.
- Apply per-feature `(value - mean) / max(std, epsilon)` and metric weights consistently to both dataset and query. Keep means/stds/weights and schema hash with baked DB.
- Optional classification tags for idle, gait, turn direction, action, ground-only and clip validity; eligibility filtering is part of search semantics, **not** an afterthought.
- Clip ends: if requested future samples are unavailable, reject or explicitly handle loop semantics. Don't wrap ends of nonlooping clips.
- Velocity dimensions: meters per second if source scale is correct. Finite difference in seconds; validate scaling.

## Version and compatibility

- Increment schema version for feature order, root-space definition, sample cadence, trajectory horizon or normalization changes.
- Re-bake and re-train any model whose input schema changes; don't silently load stale model/database pairs.
- Store source animation GUID + hash, rig/avatar identifier, import settings hash, metadata hash and sample rate.
- Test known planted-foot and constant-velocity clips; assert finite values, correct local orientation and stable frame boundaries.
