# M2.5 — Database inspection and readiness

Date: 2026-11-24. M2.5 implementation and data/inspection checks are validated in Unity 6000.6.0f1. The milestone remains open because the locomotion regression capture was refused while the active scene was dirty; the scene was preserved without saving or reverting it.

**Subsequent update (2026-11-24):** after this five-clip baseline report was recorded, four measured backward/180-degree turn clips were added to the same database asset. Current data counts, parity and the remaining coverage limitations are recorded in the later [PROGRESS entry](../PROGRESS.md#2026-11-24--m2-data-expansion--backward-locomotion-and-180-degree-turns). The five-clip numbers below describe the M2.5 inspection baseline at that time.

## Scope

M2.5 extends the existing baker with data-health inspection, an isolated 3D character/skeleton viewport, measured source-coverage reporting and a runtime data-contract check. The five assemblies, binary/schema versions and 27D feature order remain the boundaries of this task. Search, matching playback, gameplay changes and neural work belong to later milestones.

The controls and evidence below reflect the completed source and actual Editor inspection. M2 gameplay results remain historical; the attempted M2.5 regression capture did not run.

## Baker usage

1. Open **Tools > Motion Matching > Motion Database Baker** outside Play Mode.
2. Choose `Assets/Animations/MotionMatching/Databases/LocomotionBaseline.asset` and **Reload and inspect** to load the existing data and source references.
3. To bake, select a persistent character model and its bound valid Humanoid Avatar. Add the intended Humanoid clips, tags and loop flags; configure the sample rate, three future horizons, normalization epsilon and heuristic contact thresholds. Choose **Bake and inspect**.
4. Select a clip and **Sample**. Choose **3D display** (mesh, skeleton or both); toggle **Stored baked pose** to switch from evaluated source animation to stored transforms. Use step buttons or **Play**/**Pause**, **Playback speed** and **Reset camera**. Drag to orbit, Shift-drag or middle-drag to pan, and scroll to zoom. The evaluated pose cache is built once per selected clip, so scrubbing does not rebake.
5. Use **Save baked database…** to save only the intended asset. Reload to verify source identity and current hashes. Source/import changes require rebaking.

Enable **Motion diagnostics** for overlays and **Compare evaluated / stored pose** for maximum root/bone position and angle errors. Expand **Motion Database Health** for counts, root/angular/joint speeds, boundary diagnostics, contact fractions/changes and per-dimension mean/std/applied scale. Thresholds produce warnings; they do not discard clips. Choose **Refresh health report** after changing thresholds. The attached Baker was visually inspected in mesh-only, skeleton-only and combined display, in evaluated and stored modes, with the 2D graph visible.

Source mode caches every transform in the isolated evaluated hierarchy once per clip selection. Stored mode resets unmapped transforms to the reference pose before applying the saved root and 52 baseline mapped local poses. The comparison reports viewport world-pose errors. Independent `CompareControllerPose` diagnostics instead compare mapped parent-local bones and rebased roots against AnimatorController evaluation at selected times. Cache construction can take time; scrubbing uses the cache and does not rebake. Neither stored mapped transforms nor a small numerical comparison establish full-rig, muscle-curve or gameplay playback equivalence.

The working M2 baseline uses 60 Hz, horizons of 20/60, 40/60 and 1 second, and five clips. Its recorded 716 searchable rows and 52 mapped pose bones are baseline facts, not proof of M2.5 correctness or sufficient locomotion coverage.

## Coverage matrix

Coverage is measured from sampled roots; names and tags are hints only. The saved baseline health report evaluates its existing 60 Hz data. Candidate metrics below come from separate 30 Hz nonloop diagnostic samples, not a rebake or an expansion of the saved database.

| Capability | Baseline candidates | M2.5 measured result |
|---|---|---|
| Idle | Idle | Root speed 0.00022–0.01382 m/s; near-stationary root |
| Forward walking | walking | Root speed 1.249–1.822 m/s; net Z +1.665 m |
| Forward running | Fast Run | Root speed 4.914–5.865 m/s; net Z +2.914 m |
| Leftward travel | LeftStraffe | Net X/Z −1.070/+1.361 m: diagonal forward-left, not pure lateral travel |
| Rightward travel | RightStraffe | Net X/Z +1.034/+1.388 m: diagonal forward-right, not pure lateral travel |
| Backward walking/running | No selected dedicated source | Locomotion Pack `Ch15_nonPBR@Walking Backward.fbx`: net Z −1.4156 m, mean speed 1.1503 m/s; `Ch15_nonPBR@Run Backward.fbx`: −1.9210 m, 3.0453 m/s |
| Quarter turns | No selected dedicated source | Locomotion Pack `left turn 90.fbx`: −89.60°; `right turn 90.fbx`: +103.71°, not exactly 90° |
| Half turns | No selected dedicated source | Locomotion Pack `left turn.fbx`: −176.14°; `right turn.fbx`: +175.07° |
| Deceleration / stopping | No selected dedicated source | Action Adventure Pack `run to stop.fbx`: start/end speed 3.5835/0.2624 m/s over 0.9 s, with +34.00° heading change |
| Starts, acceleration, directional transitions | No verified selected source | Remain unverified; speed-change heuristics alone do not establish suitable transitions |

Backward/turn/stop candidates have not been added to the saved five-clip database and still require 3D pose/suitability review. Several Action Adventure/Basic Shooter clips named as turns measure approximately zero heading change. Do not infer turn coverage from those names. Missing coverage is not a format error. Record any additions with source identity, sampled behavior and bake evidence.

The baseline health report records 716/716 searchable samples, zero excluded samples, zero non-finite feature values and zero near-constant dimensions. Fast Run reports missing estimated contacts for at least one foot; this is a heuristic warning requiring inspection, not proof of bad source animation. Last/first pose-angle differences include 16.919° for Fast Run and 3.058–4.775° for walking/diagonal travel; these boundary measurements alone do not certify visual seam quality.

Evidence: `.utmp/m25/source-inventory.txt` contains actual imported clip metadata; `.utmp/m25/coverage-metrics.txt` contains sampled candidate metrics; `.utmp/m25/health.json` contains the baseline analysis. The audit completed after the initial CLI request timed out; the completed artifacts, rather than that timeout, support these measurements.

## Validation evidence

| Check | Actual result |
|---|---|
| Unity Editor / compilation | Unity 6000.6.0f1; final settled Editor reports `compilationFailed=false`, `compiling=false`, 0 Console errors and 0 warnings. |
| EditMode / PlayMode | EditMode 61/61 passed; PlayMode 6/6 passed (`test_status` completed). |
| Python / static checks | 31/31 Python tests passed; `validate_assemblies` and `validate_scaffold` passed. Scaffold explicitly skipped TOML parsing (Python 3.11 required) and workflow YAML parsing (PyYAML absent). |
| 3D viewport and pose comparison | Mesh-only, skeleton-only and combined modes inspected in evaluated-source and stored-pose modes at walking sample 25; 2D graph visible. Stored/evaluated comparison reported zero. Screenshots: `.utmp/m25/baker-*.png`. |
| Independent evaluated/baked parity | Across all five clips: maximum root error 1.73636e-6 m, mapped-bone local error 2.66575e-7 m, angular error 0 degrees; preview-world parity was exactly zero. |
| Database health and identity | Baseline database SHA-256 begins `de31ca` and ends `716e`; 716 searchable, 0 excluded, 0 constant, 0 invalid/error rows. Fast Run retains a heuristic contact warning. |
| Runtime contract and preservation | Runtime baseline hashes for all five protected files match. Assembly validator passed; schema/binary version and database identity remained unchanged. |
| Coverage audit | Sampled-root metrics and gaps are recorded above. Candidate clips were not added to the database; starts, acceleration and directional transitions remain unverified, and candidate pose suitability is not certified. |
| Locomotion regression capture | Attempted but refused because active `MotionMatchingTest` was `isDirty`. The scene was neither saved nor reverted; its on-disk asset matches the protected baseline. No fresh M2.5 gameplay regression result is claimed. |

## Limits and handoff

Foot contacts remain heuristic. Stored mapped local poses omit unmapped intermediate transforms and Humanoid muscle curves; an evaluated source pose and a reconstructed stored pose must be distinguished in inspection. Local model/Avatar and skybox prerequisites, redistribution clearance, clean-checkout/player-build validation, cross-platform tolerance and the intermittent Editor assertion remain open as documented in the [M2 report](M2_IMPLEMENTATION_REPORT.md).

The owning contracts are [FEATURE_SCHEMA.md](../data/FEATURE_SCHEMA.md) and [MotionMatchingArchitecture.md](../MotionMatchingArchitecture.md). Keep M2.5 and the M3 playback gate open until a regression capture can run from a clean active scene and the remaining motion coverage gaps are addressed or explicitly accepted.

M2.5 changes no Core/Runtime code, extraction/normalization semantics or data format. Consumers must treat the mutable DTO arrays as read-only. M3 will index only valid rows, preserve clip frame-offset/time mapping, normalize queries with the saved statistics, and keep proposed five-group weights separate from normalization. The exact grouping and square-root coordinate scaling convention are documented in the feature contract; no weighting/search implementation is claimed.
