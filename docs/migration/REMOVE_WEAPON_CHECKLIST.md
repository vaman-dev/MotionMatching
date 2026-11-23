# Weapon removal — staged checklist

The original report identifies `PlayerInteractionSystem`, `WeaponPickupItem`, `WeaponAimAlignment`, `RifleAimAlignmentDebug`, `PlayerLeftHandIK`, `PlayerAimRigController`, `PlayerAimTargetProvider`, `TPSCameraController`, and Animator rifle layers. Many references are serialized into `Test.unity`; do not mass-delete scripts first.

1. Clone `Assets/Scenes/Test.unity`; test in the clone.
2. Record baseline and separate motor from root callback before changing Animator logic.
3. Keep `PlayerInputReader` Move/Look/Sprint/Jump actions. Remove/detach Interact, aim and weapon effects from the locomotion-only scene; leave unused bindings initially if removing them could break readers.
4. Keep free Cinemachine camera; detach aim camera, `AimCameraTarget`, shared aim provider, spine targeting, weapon alignment and hand rig after reference audit.
5. Remove the weapon pickup and `WeaponAimPivot`/weapon socket references from the cloned character hierarchy once no active scripts need them.
6. Remove rifle layer/blend states/mask only after a working unarmed-only Animator fallback is verified.
7. Replace `PlayerController.IsAiming`/`PlayerAnimator.HasRifle` dependencies where still used. Strip gameplay equipment logic, not just the prefab.
8. Search project-wide for class and parameter references; check compilation and scene serialization.
9. Keep source assets in Git until the modified project is verified and rollback is easy; delete in a separate commit with GUID/asset-reference checks.

**Exit:** no missing script components, compile errors or rifle-specific runtime references in the dedicated locomotion scene; motor/gravity still work. The original scene remains available for comparison.
