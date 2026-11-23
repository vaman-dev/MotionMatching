# C1 directory consolidation report

Date: 2026-11-23. Branch: `refactor/consolidate-motion-matching`, based on merged
`origin/main` at `efad2d3`. Implementation and focused regression checks are
complete. A project-wide zero-error certification remains open because of the
existing Editor/assertion and prefab defects below. Nothing was committed,
staged, pushed or opened as a PR during C1.

## What moved

All 28 project-owned C# files and the five existing assembly definitions now
belong under `Assets/Scripts/MotionMatching/`. There is no project C# left in
predefined assemblies: Unity's compiled inventory reports Core 1, Runtime 21,
Editor 2, EditMode tests 2 and PlayMode tests 2 source files.

| Destination | Contents |
|---|---|
| Core | Existing root-motion accumulator and engine-independent assembly |
| Runtime/Input | PlayerInputReader and FreeCameraLookInput |
| Runtime/Character | PlayerController |
| Runtime/Motor | CharacterMotor, the sole capsule movement owner |
| Runtime/States | State identifier/machine/interfaces and locomotion/jump/dive states |
| Runtime/Animation | Animator facade, root relay and footstep integration |
| Runtime/LegacyShooter | Nine scripts required by preserved original scenes/prefab |
| Editor | Assembly audit and existing input/capture harness |
| Tests/EditMode, Tests/PlayMode | Existing seven/six test cases and their definitions |
| Assets/Animations/MotionMatching/Source | 109 FBX, 31 animation clips, original controller and mask; all existing metadata retained |
| Assets/Animations/MotionMatching | Dedicated UnarmedLocomotion controller |
| docs | Seven former root project documents, including byte-identical Report |

[The audit](CONSOLIDATION_AUDIT.md) classifies every script. The reproducible
[move map](consolidation-moves.json) contains 379 path mappings: 372 existing
files (365 Unity assets/metadata and seven documents) were relocated; seven
already-missing animation paths remain missing at the mapped locations. The
eighth preexisting deletion is outside that moved subtree and remains intact.

## Removed and retained

Removed only four verified empty containers and their folder metadata:
`Assets/Scripts/Player/UI`, `Assets/Scripts/Player`, `Assets/Scripts/Gun` and
`Assets/Editor`. The former `Assets/MotionMatching` root was relocated intact;
it no longer competes with Scripts. No implementation or animation source was
deleted or duplicated. No empty Trajectory, Databases or Neural folders were added.

The original Test scene, recovery scene and rifle prefab still depend on weapon,
interaction, aiming, ADS and IK classes. These nine compatibility scripts remain
under LegacyShooter and are absent from MotionMatchingTest. PlayerAnimator's
rifle facade and shared aim compatibility remain required by those preserved
assets. FootstepResetStateBehaviour is unreferenced but retained as a reusable
callback for explicit later review. Retiring original assets is required before
their dependencies can be deleted safely.

## Necessary source changes

- Runtime adds its explicit Animation Rigging reference for retained compatibility.
- Editor adds Input System and Cinemachine references for the moved capture tool.
- AssemblyBoundaryAudit verifies actual compiled source ownership as well as boundaries.
- Regression tests use Runtime production types directly and the relocated controller.
  The shooter guard now checks the actual MouseWorldSphereTest class name.
- Python validators enforce the single source root, centralized docs, mapped byte/GUID
  preservation and package/API boundaries. Historical M0/M1 contracts remain available.

All gameplay C# contents, class names, namespaces, source animation bytes and
existing asset/assembly GUIDs match the pre-refactor snapshot. Compiled ownership
intentionally changes from Assembly-CSharp to the named assemblies. Preserved
diagnostic class identifiers in scene YAML still name the old assembly; actual
Unity MonoScript/component resolution succeeded without rewriting those scenes.

## Fresh validation

| Check | Actual result |
|---|---|
| Unity 6000.6.0f1 compilation | Succeeded; compilationFailed=false |
| EditMode | 7 passed, 0 failed |
| PlayMode | 6 passed, 0 failed |
| Python fixtures | 31 passed |
| Static assembly/scaffold checks | Passed; local TOML/YAML parsing explicitly skipped |
| Strict snapshot/move preservation | Passed after settings restoration |
| Actual input capture | 1,141 consecutive frames at 60 Hz; capture/restoration succeeded |
| Baseline comparison | All 16 checks passed |
| Dedicated/original/recovery scenes | 82/102/90 objects; zero missing scripts; moved components resolve |
| Rifle prefab | Zero missing scripts; all three moved components resolve |
| Compiled boundaries | All five; no project sources in predefined assemblies; no boundary errors |
| Build configuration | Original Test.unity remains the only enabled build scene; unchanged |

Stable capsule speeds were walk 1.627362, sprint 5.544619 and turn 1.664739 m/s:
changes of +0.082%, +0.374% and -0.470% from M1. Jump apex was 1.329979 m;
dive path 3.275254 m. Jump/Dive state coverage remained 39/55 frames, initial
and post-dive drift were zero, and camera Look remained 58.725395 degrees.
The one extra capture sample was an idle frame; no movement-feel change was intended.

Exact commands were the existing CLI test run/status workflow for both named
test assemblies, the capture harness's RunInteractive entry point, and:

```powershell
py -3 -B -m unittest discover -s tools/tests -v
py -3 -B tools/validate_assemblies.py
py -3 -B tools/validate_scaffold.py
py -3 -B tools/validate_migration.py --mode consolidation --baseline .utmp/consolidation-start/manifest.json --move-map docs/migration/consolidation-moves.json
```

Local evidence is under ignored `.utmp/consolidation-validation/`: compilation,
EditMode/PlayMode results, locomotion samples/completion, capture-comparison,
reference-audit, actual current-console entries and final Editor/scene status.
The verified source snapshot is `.utmp/consolidation-start/manifest.json` with
archive SHA256 `02b98b4bfa63e0bea1e5303a13f8329312f4f7643dcea9036a25e4a3f8719c80`.
These ignored artifacts are not published and are not prerequisites for ordinary
Python fixture tests. Specialized bug, test, documentation and Git agents reviewed
their respective scopes.

## Remaining defects and limits

The final actual Console contains three entries, not zero:

1. An Editor assertion with only Internal_PlayModeStateChanged in its stack. The
   identical signature is recorded before C1 at 07:26:39 UTC; its current visibility
   is unexplained. No project root cause is established, and it was not cleared.
2. Two messages produced by auditing Ch15_nonPBR.prefab: the variant's parent GUID
   83f01930545388442a05586e6a58b343 is missing. It matches the already user-deleted
   model metadata under Assets/Material/Character/FBX. The prefab is byte-identical
   to both snapshots; its empty loaded root is not evidence that it is valid.

The unused Ren_Magica2Setup.prefab also has two missing script components. Its
bytes are identical in M0, C1 baseline and current tree, and neither missing GUID
was present in either baseline metadata inventory. Neither defective prefab is
referenced by the current scenes. They were preserved rather than repaired by
restoring user deletions, deleting components or installing unrelated packages.

Unity reserialized ProjectSettings and TimeManager during validation. Reviewed
before/after copies are in settings-reserialization under the evidence folder.
Only these generated changes were restored to the verified snapshot bytes, with
hash guards against intervening edits. No Unity calls were made after restoration.
Future Editor saves can perform that format upgrade again. The final dedicated
scene is active, stopped and clean; its dirty post-Play copy was byte-identical to
disk and was preserved before reopening. Background execution is restored false.

No target player build, remote C1 CI, comprehensive terrain/contact/coyote matrix,
or clean-checkout Unity validation was performed. Previously excluded character
and skybox assets still need a publication/redistribution decision. Future staging
must separate retained user edits from the consolidation; do not stage destination
folders blindly. The existing ignore/LFS rules, source assets and user deletions
remain preserved. No database, brute-force/KD search or neural functionality was added.

The next technical milestone remains animation feature extraction and database
baking after review/merge and acknowledgment or resolution of the open validation
limits. No commit or publication is authorized by this report.
