# Final repository cleanup report

Date: 2026-11-24. Branch: `cleanup/retire-legacy-shooter`, based on merged main `3fb7e7b`.

## Result

MotionMatchingTest is the only enabled build scene. Nine LegacyShooter scripts and the original Test scene, AR-15 prefab, invalid Ch15 prefab variant and stale recovery scene have been retired through Unity AssetDatabase. A newly created, untracked recovery snapshot `Assets/_Recovery/0 (1).unity` also had a missing NativeCube prefab ancestor; it was preserved outside Assets and retired locally. No animation source or character model was deleted by this cleanup.

All edited retired files and metadata were preserved under ignored `.utmp/final-cleanup/pre-change/`, `legacy-shooter-sources-verified/` and `new-recovery-20261124/`. These local archives are not published. Existing unrelated user animation deletions/edits, scene edits, terrain, render settings and material/package settings are excluded from publication.

## Dependency decisions

Before retirement, Test serialized PlayerAimRigController, PlayerAimTargetProvider, PlayerInteractionSystem, PlayerLeftHandIK and TPSCameraController. Recovery/0 serialized PlayerInteractionSystem. The rifle prefab serialized WeaponPickupItem, WeaponAimAlignment and RifleAimAlignmentDebug. MouseWorldSphereTest had no serialized component consumer and was referenced by RifleAimAlignmentDebug. After those consumers were retired, all nine script GUIDs and the four retired asset GUIDs had zero retained references in the static audit. Runtime no longer needs its Animation Rigging assembly reference; the package remains required by the retained BoneRenderer visualization.

The visible demo character uses FBX/Avatar GUID `8cce7d34a61d4be479104c1586a5ad71`, independent of the broken variant's ancestor `83f01930545388442a05586e6a58b343`. Two FBX source hashes differ, so the variant was not blindly retargeted. The Ren Magica setup retains its character hierarchy and GUID; Unity removed two unusable missing-script stubs (head collider and Magica Cloth Hair). Saving also updated Unity serialization/default fields. Original prefab payload is archived. No missing plugin was installed. Existing inactive rifle/aim members in the shared Animator/controller facade remain to avoid changing locomotion behavior; this is not a claim that every historical rifle-named field was removed.

## Fresh validation

| Check | Actual result |
|---|---|
| Unity CLI recompile | Up to date; failed=false; zero compiler errors/warnings |
| EditMode assembly | 7 passed, 0 failed |
| PlayMode assembly | 6 passed, 0 failed |
| Python fixtures | 31 passed |
| Assembly/scaffold validators | Passed; local TOML/YAML parsing skipped (Python 3.10, no PyYAML) |
| Compiled assembly audit | Five boundaries valid; no reported errors |
| Retained character prefabs | Ren Basic/Magica: zero missing scripts |
| Final scene/prefab audit | MotionMatchingTest and two character prefabs load without Console errors |
| Build scenes | Only MotionMatchingTest enabled, original GUID retained |
| Locomotion capture | 1,141 consecutive 60 Hz frames; all 16 comparisons passed |
| Fresh Console after final audit | 0 errors, 0 warnings |

Walk/sprint/turn medians: 1.627355/5.544070/1.665459 m/s. Jump apex: 1.329979 m; dive path: 3.275255 m. Idle/post-dive drift: zero. Camera Look: 58.725395 degrees. Capture restored Editor settings and succeeded with no recorded errors.

The historical Editor PlayMode assertion did not recur during this session's tests/capture. Its root cause is still unproven; this is an observation, not an Editor defect fix. The final Console was cleared only after preserving full history, including the two errors emitted when auditing the newly broken recovery snapshot, then the retained asset audit was rerun. Diagnostics were not suppressed.

Exact artifacts and scripts: `.utmp/final-cleanup/` (`editmode-results.json`, `playmode-results.json`, `compilation.json`, `locomotion.json`, completion/comparison, reference-audit-final and console history/final). Commands: `unity command test run/status` for both named assemblies; `unity recompile . --timeout 60 --json`; existing `MigrationBaselineCapture.RunInteractive`; `py -3 -B -m unittest discover -s tools/tests -v`; `tools/validate_assemblies.py`; `tools/validate_scaffold.py`. Static validation is distinct from actual Unity execution.

## Limits and publication

Unity validation covers the complete local working tree, including newer user scene changes; those scene changes are not part of the cleanup commit. No target player build, full terrain/contact/coyote matrix or asset-complete clean-checkout Unity run was performed. The demo FBX and skybox remain untracked local prerequisites pending redistribution clearance. Therefore this is a validated local cleanup, not a clean-clone playable release or a claim that every source-animation importer is warning-free. Historical consolidation preservation checks intentionally prohibit these newly authorized deletions and are not a cleanup acceptance gate.

## Exact tracked deletion inventory

- `Assets/Prefabs/Chracter/Ch15_nonPBR.prefab`
- `Assets/Prefabs/Chracter/Ch15_nonPBR.prefab.meta`
- `Assets/Prefabs/Gun.meta`
- `Assets/Prefabs/Gun/ar15.prefab`
- `Assets/Prefabs/Gun/ar15.prefab.meta`
- `Assets/Scenes/Test.unity`
- `Assets/Scenes/Test.unity.meta`
- `Assets/Scripts/MotionMatching/Runtime/LegacyShooter.meta`
- `Assets/Scripts/MotionMatching/Runtime/LegacyShooter/MouseWorldSphereTestMouseWorldSphereTest.cs`
- `Assets/Scripts/MotionMatching/Runtime/LegacyShooter/MouseWorldSphereTestMouseWorldSphereTest.cs.meta`
- `Assets/Scripts/MotionMatching/Runtime/LegacyShooter/PlayerAimRigController.cs`
- `Assets/Scripts/MotionMatching/Runtime/LegacyShooter/PlayerAimRigController.cs.meta`
- `Assets/Scripts/MotionMatching/Runtime/LegacyShooter/PlayerAimTargetProvider.cs`
- `Assets/Scripts/MotionMatching/Runtime/LegacyShooter/PlayerAimTargetProvider.cs.meta`
- `Assets/Scripts/MotionMatching/Runtime/LegacyShooter/PlayerInteractionSystem.cs`
- `Assets/Scripts/MotionMatching/Runtime/LegacyShooter/PlayerInteractionSystem.cs.meta`
- `Assets/Scripts/MotionMatching/Runtime/LegacyShooter/PlayerLeftHandIK.cs`
- `Assets/Scripts/MotionMatching/Runtime/LegacyShooter/PlayerLeftHandIK.cs.meta`
- `Assets/Scripts/MotionMatching/Runtime/LegacyShooter/RifleAimAlignmentDebug.cs`
- `Assets/Scripts/MotionMatching/Runtime/LegacyShooter/RifleAimAlignmentDebug.cs.meta`
- `Assets/Scripts/MotionMatching/Runtime/LegacyShooter/TPSCameraController.cs`
- `Assets/Scripts/MotionMatching/Runtime/LegacyShooter/TPSCameraController.cs.meta`
- `Assets/Scripts/MotionMatching/Runtime/LegacyShooter/WeaponAimAlignment.cs`
- `Assets/Scripts/MotionMatching/Runtime/LegacyShooter/WeaponAimAlignment.cs.meta`
- `Assets/Scripts/MotionMatching/Runtime/LegacyShooter/WeaponPickupItem.cs`
- `Assets/Scripts/MotionMatching/Runtime/LegacyShooter/WeaponPickupItem.cs.meta`
- `Assets/_Recovery/0.unity`
- `Assets/_Recovery/0.unity.meta`
