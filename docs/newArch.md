# TPS Shooter — Codebase and Aiming Architecture

> **Scope:** repository inspection on 2026-08-16 (commit `13c8484`). This report describes the code and the current `Test` scene configuration. It does not claim that disabled or unassigned systems are live.

## 1. Project at a glance

This is a Unity third-person-shooter prototype. It has one enabled build scene, `Assets/Scenes/Test.unity`, and a compact, MonoBehaviour-based gameplay architecture with no custom assembly definitions. Runtime gameplay code therefore compiles into Unity's default `Assembly-CSharp` assembly.

| Area | Current implementation |
|---|---|
| Unity | Unity 6.4.2f1 (`6000.4.2f1`) |
| Rendering | URP 17.4.0 (confirmed package dependency) |
| Input | Unity Input System 1.19.0; action asset: `Assets/InputSystem_Actions.inputactions` |
| Camera | Cinemachine 3.1.6, free-orbit and aim virtual cameras |
| Character | `CharacterController`, animator root motion, and a small custom state machine |
| Rigging | Animation Rigging 1.4.1; left-hand rig is configured, spine rig is not yet configured |
| Weapon | Pickup/equip flow, AR-15 prefab, muzzle and off-hand grip transforms |
| Tests | Unity Test Framework package is installed, but no first-party test assemblies or test scripts were found |

Other notable installed packages are AI Navigation, Unity Behavior, ProBuilder, Timeline, UGUI, Post Processing, and Visual Scripting. Their presence does not mean they are part of the current player/aim loop.

## 2. Directory map and ownership

```text
Assets/
  Scenes/Test.unity                 Current and only enabled startup scene
  Scripts/
    Player/                         Input, controller, states, animation, camera, rig helpers
    Gun/                            Pickup and weapon-alignment scripts
  Prefabs/
    Chracter/                       Character model/prefab assets (folder name is misspelled)
    Gun/ar15.prefab                 Pickup weapon and its muzzle/grip references
  InputSystem_Actions.inputactions  Gameplay actions
  Material, Sounds, Textures, VFX,
  Settings, ShaderGraph             Art/content and rendering assets
```

The project has no assembly boundaries, service layer, event bus, dependency injection, or networking implementation in first-party runtime code. Scene Inspector references and `GetComponent*` fallbacks wire the systems together.

## 3. Runtime architecture

```text
Input System actions
        |
        v
PlayerInputReader  ───────>  PlayerInteractionSystem ──> WeaponPickupItem
        |                            |                       |
        |                            v                       v
        |                       PlayerAnimator <── HasRifle / equipped socket
        v                            |
PlayerController <───────────────────+
        |
        +--> PlayerStateMachine: Locomotion / Jump / DiveRoll
        |         |
        |         +--> PlayerAnimator -> Animator root motion -> RootMotionRelay
        |                                            |
        +--------------------------------------------+--> CharacterController.Move
        |
        +--> TPSCameraController -> Cinemachine free/aim cameras
        +--> PlayerLeftHandIK -> Animation Rigging hand rig
        +--> PlayerAimRigController -> intended Animation Rigging spine rig
        +--> WeaponAimAlignment -> intended weapon pivot rotation
```

### Input

`PlayerInputReader` is the single gameplay input reader and is scheduled at execution order `-100`. It enables the configured actions, then exposes move, look, sprint, jump, dive roll, interact, and RMB state as properties. Look has a fallback lookup by the `Player/Look` action path.

### Player movement and animation

`PlayerController` owns the `CharacterController`, ground checking, gravity, coyote time, jump data, dive-roll data, and the `PlayerStateMachine`. Only `Locomotion`, `Jump`, and `DiveRoll` are registered; `Fall` and `Crouch` exist in `PlayerStateId` but have no states.

`PlayerAnimator` writes hashed Animator parameters such as `MoveX`, `MoveZ`, `Speed`, `IsMoving`, `IsSprinting`, `Jump`, `DiveRoll`, `HasRifle`, and `isAiming`. `RootMotionRelay` receives `Animator.deltaPosition`, removes vertical root motion, applies controller-owned gravity, and calls `CharacterController.Move`.

When aiming, root-motion direction is replaced with camera-relative input direction, while the animation clip still supplies stride distance. This is what permits aim strafing and backward input even when clips contain forward-authored root motion.

### Weapon pickup

Every update, `PlayerInteractionSystem` searches a fixed non-alloc overlap buffer around `InteractionPoint` for the nearest `WeaponPickupItem` on the configured weapon layer. Pressing Interact:

1. parents the weapon root to `weaponSocket`;
2. applies the prefab's local position/rotation/scale;
3. makes all weapon rigidbodies kinematic and disables weapon colliders (when configured);
4. records the equipped weapon; and
5. sets `PlayerAnimator.HasRifle`.

`PlayerController.IsAiming` is true only when the animator's `HasRifle` value is true and RMB is held. Thus camera ADS, body aim, and weapon alignment all depend on equipping a weapon first.

## 4. Camera and aim flow

### Camera ADS (working configuration)

`TPSCameraController` runs at execution order `-50`. While not aiming, it continually calculates a world point at the centre of the rendered camera and rotates `aimCameraTarget` to face it. This is meant to avoid an ADS snap caused by the free and aim cameras being in different positions.

On RMB with a rifle equipped, it raises the aim Cinemachine camera priority above the free camera, disables the free camera's input-axis controller, and applies look delta to `aimCameraTarget` (`pitch` clamped from -35° to 70°). On release it restores the free camera's input controller and priority.

### Intended complete aiming chain

```text
RMB + HasRifle
    |
    +--> camera: aim virtual camera wins; look rotates aimCameraTarget
    |
    +--> body: camera centre ray -> one world target -> spine AimConstraint rig
    |
    +--> weapon: muzzle -> same world target -> weapon aim pivot rotation
    |
    +--> locomotion: player turns toward horizontal camera forward; A/D strafe
    |
    `--> left hand: target snaps to rifle LeftHandGripPoint -> hand rig blends in
```

The left-hand chain is wired in `Test.unity`: `PlayerLeftHandIK` references the interaction system, a hand rig, and a target. It follows the equipped rifle's `LeftHandGripPoint` and blends its rig weight toward one.

## 5. Confirmed aim blockers and drift diagnosis

These are the important issues that currently prevent the intended full aim setup from operating reliably.

| Severity | Finding | Evidence and effect |
|---|---|---|
| **Blocking** | The cursor-to-world aim provider is disabled in the `Test` scene. | `MouseWorldSphereTest` has `m_Enabled: 0`. `WeaponAimAlignment` requires its `HasAimPoint`, so it returns every frame and cannot rotate the muzzle/pivot. |
| **Blocking** | The spine rig reference is unassigned. | `PlayerAimRigController.spineRig` is `{fileID: 0}` in `Test.unity`; the `RigBuilder` also contains a second empty rig layer. The script can move `spineAimTarget`, but no spine-rig weight is applied and no torso/spine rotation occurs. |
| **High** | The previous weapon aim target is not a reticle target. | `MouseWorldSphereTest` uses the physical OS cursor and `ScreenToWorldPoint` at a fixed depth (scene value: 5 m). In a normal locked-cursor TPS, the cursor can remain at a stale/off-centre coordinate. More importantly, this is a depth-plane point, not the object/world point under the centre reticle. |
| **High** | Two aim solvers use different targets. | The body solver raycasts from camera centre up to 200 m, but weapon alignment aims at the mouse point on the fixed 5 m plane. Their directions only match by coincidence. This is the direct mathematical reason the muzzle direction drifts from camera/reticle aim. |
| **High** | Weapon orientation conflicts with animation/hand ownership. | `WeaponAimAlignment` rotates `weaponSocket`, which is the same transform used to parent the rifle to the hand. The socket is inside the animated character hierarchy; animation, rig constraints, and the alignment script can overwrite one another in different update phases. |
| **Medium** | Alignment is smoothed, not exact. | `WeaponAimAlignment` uses exponential `Slerp` at sharpness 20 in `LateUpdate`. Fast look motion intentionally leaves blue (actual muzzle) behind green (desired direction), creating a visible transient lag even after target unification. |
| **Medium** | There is no hit/fire source of truth. | No first-party shooting, projectile, hitscan, impact, recoil, or spread code exists. A future firing system could raycast from the muzzle and disagree with the camera unless it uses the shared target below. |
| **Medium** | Current asset/import state has recent errors. | Unity logs record asset-version/import errors for `WeaponAimAlignment.cs`, plus OneDrive-style sharing violations in `Library/StateCache`. These can leave editor state stale until the import issue is resolved. |

The AR-15 scene instance does override `WeaponAimAlignment` with the player, the disabled aim provider, its muzzle, and `weaponSocket`; those references are present. The blocker is the disabled provider, not missing scene overrides.

## 6. Recommended future aim design

Introduce one `AimTargetProvider` (or replace `MouseWorldSphereTest`) and make all three consumers use it. It should calculate once per frame from the **actual rendered camera centre/reticle**:

```text
camera centre ray
   -> Physics.Raycast with explicit aimable layers, ignoring player and weapon
   -> hit point, or ray origin + maxAimDistance
   -> AimTarget (world point) + AimDirection
       ├── spine target / Multi-Aim Constraint
       ├── weapon visual alignment
       └── firing ray / projectile initial direction
```

For third-person shooting, use two stages when firing:

1. raycast from the camera centre to choose the intended reticle target;
2. raycast from the muzzle to that target to catch nearby cover; if blocked, fire into the obstruction, otherwise fire toward the target.

This keeps the crosshair honest while preventing shots from passing through a wall beside the muzzle.

### Spine-rig integration plan

1. Add a named spine/upper-body `Rig` under the existing `RigBuilder` and assign it to `PlayerAimRigController.spineRig`.
2. Add a Multi-Aim Constraint (typically on upper chest/spine) driven by `spineAimTarget` from the shared aim provider.
3. Use an upper-body Avatar Mask or rig constraints so lower-body locomotion remains responsible for feet and root motion.
4. Blend the rig only while `PlayerController.IsAiming`; tune constraint axes and offsets against the rifle animation rather than rotating the whole player transform for pitch.
5. Keep horizontal player turning in `PlayerLocomotionState`, but consider a yaw dead zone/turn-in-place threshold so the torso can lead briefly before the pelvis follows.

### Weapon hierarchy recommendation

Do not rotate `weaponSocket` directly for fine aim correction. Make a dedicated `WeaponAimPivot` below the socket and above the weapon mesh (or a constrained weapon root), then rotate that pivot. Let animation/hand attachment own the socket, the spine rig own torso pose, and the weapon pivot own the small residual muzzle correction. The left-hand IK target should follow the final weapon grip transform.

## 7. Update order and ownership to preserve

| Order | System | Owns |
|---:|---|---|
| -100 | `PlayerInputReader.Update` | gameplay input snapshot |
| -50 | `TPSCameraController.Update` | camera selection and aim camera target rotation |
| -1 | `PlayerAimRigController.Update` | intended spine target and rig weight |
| 0 | player/controller/state updates | state, player yaw, animator parameters, pickup |
| Animator callback | `RootMotionRelay.OnAnimatorMove` | character movement through `CharacterController` |
| LateUpdate | weapon/hand IK | final visual aim and left-hand placement |

The shared aim target should be calculated after the camera's final orientation is known and before rig/weapon consumers run. If Cinemachine's final camera pose is only valid in `LateUpdate`, calculate it in a dedicated late phase and ensure weapon/rig consumers run afterwards; otherwise they will read the prior frame's camera direction.

## 8. Constraints and maintenance notes

- The working tree already contains many unrelated, in-progress asset, scene, animation, and script changes. This document deliberately makes no gameplay or asset edits.
- `Assets/_Recovery/0.unity` is a recovery scene, not a configured build scene.
- `docs/ARCHITECTURE.md` already exists; this file is the current inspection/report requested as `docs/newArch.md`.
- Inspector references are a major dependency in this project. Add defensive validation messages for required camera, rig, muzzle, and target references before expanding the aim system.

## 9. Source evidence inspected

- `ProjectSettings/ProjectVersion.txt`, `ProjectSettings/EditorBuildSettings.asset`, `Packages/manifest.json`
- `Assets/Scenes/Test.unity`, `Assets/Prefabs/Gun/ar15.prefab`
- `Assets/Scripts/Player/*.cs` and `Assets/Scripts/Gun/*.cs`
- Unity import logs under `Logs/` (used only for the reported import/cache failures)
