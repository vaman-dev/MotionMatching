# TPS Shooter — Architecture Flow

> Historical shooter architecture: source paths and behavior below predate consolidation. See [current architecture](MotionMatchingArchitecture.md) and [directory ownership](migration/DIRECTORY_LAYOUT.md).

This document explains how the project works end-to-end and how the **aim pipeline**
operates across the entire system. It is a Unity TPS prototype built from 18 scripts
across four cooperating subsystems.

---

## 1. Overall System Architecture

| Subsystem | Scripts | Job |
|---|---|---|
| **Input** | `PlayerInputReader` | Single source of truth for all gameplay input (Unity Input System). `[DefaultExecutionOrder(-100)]` so it runs first every frame. |
| **Player / Locomotion** | `PlayerController`, `PlayerStateMachine` (+ `PlayerLocomotionState`, `PlayerJumpState`, `PlayerDiveRollState`), `PlayerAnimator`, `RootMotionRelay` | Character movement via `CharacterController` + a small custom state machine; drives the Animator. |
| **Camera** | `TPSCameraController` | Owns two Cinemachine cameras (Free orbit + Aim) and switches/blends between them. |
| **Combat / Aim** | `PlayerAimRigController`, `MouseWorldSphereTest`, `WeaponAimAlignment`, `RifleAimAlignmentDebug`, `PlayerInteractionSystem`, `WeaponPickupItem`, `PlayerLeftHandIK` | Pickup, ADS camera, spine-aim rig, and barrel alignment. |

### Per-Frame Order (enforced by `DefaultExecutionOrder`)

```
PlayerInputReader      (-100)   read raw input
TPSCameraController     (-50)   camera priority switch + aim yaw/pitch
PlayerAimRigController   (-1)   spine aim target + rig weight
PlayerController          (0)   stateMachine.Tick() -> rotation + animator
PlayerInteractionSystem   (0)   weapon detection + equip
MouseWorldSphereTest      (0)   cursor -> world AimPoint
RootMotionRelay   (OnAnimatorMove, between Update and LateUpdate)
WeaponAimAlignment       (LateUpdate)  barrel -> AimPoint
PlayerLeftHandIK         (LateUpdate)  hand -> grip point
```

---

## 2. Weapon Acquisition (Prerequisite to Aim)

1. `PlayerInteractionSystem.Update` -> `FindNearbyWeapon()` uses
   `Physics.OverlapSphereNonAlloc` around an interaction point to find the nearest
   `WeaponPickupItem`.
2. On **Interact (E)** press -> `EquipWeapon()`:
   - `WeaponPickupItem.AttachToWeaponSocket()` parents the rifle to the right-hand
     `weaponSocket`, zeroes rigidbody physics, disables colliders.
   - `PlayerAnimator.SetHasRifle(true)` sets the Animator's `HasRifle` bool.
3. `PlayerController.IsAiming` = `PlayerAnimator.HasRifle` **AND** `RightMouseHeld`.
   Aiming is only possible after a rifle is equipped.

---

## 3. The Aim Pipeline (3 Parallel Layers)

When RMB is held and `IsAiming == true`, three independent systems activate in
parallel. Each aligns a different part of the rig to a (slightly different) target
point.

### Layer A — Camera ADS (Zoom) — `TPSCameraController`

- On entering aim, `SyncAimTargetWithCamera()` raycasts from the **free camera's
  viewport center**, computes the yaw/pitch that look *at the world point the free
  camera center hits*, and stores them. This prevents a snap when switching cameras.
- `UpdateCameraPriority(true)` -> free camera priority stays `10`, aim camera priority
  becomes `20` -> Cinemachine blends to the **aim camera**.
- `SetFreeCameraInputEnabled(true)` disables the free camera's
  `CinemachineInputAxisController` so it stops consuming Look input.
- `UpdateAimRotation()`: `yaw += Look.x * sens`, `pitch -= Look.y * sens`, clamped to
  `[-35, 70]`, applied as `aimCameraTarget.rotation`. The aim camera follows that target,
  so **the camera looks where the mouse looks**.

### Layer B — Body Aim (Spine Rig) — `PlayerAimRigController` (execution order -1)

- Casts a ray from the **aim camera's screen center** (`ScreenPointToRay` at 50%, 50%).
  Target = `hit.point` within 200m, else `origin + dir * 200`.
- Sets `spineAimTarget.position` to that world point.
- Blends `spineRig.weight` 0 -> 1 (speed 10), so the **Animations Rigging spine rig
  rotates the character's torso/upper body** toward the aim target. The character
  *visually* aims at what the camera center points at.

### Layer C — Weapon Barrel Alignment — `MouseWorldSphereTest` + `WeaponAimAlignment`

- `MouseWorldSphereTest.Update`: reads the raw **mouse cursor**, converts via
  `ScreenToWorldPoint` at a **fixed depth of 10m** from the camera -> `AimPoint`. A
  debug sphere smoothly follows it. This is a mouse-projected world point, not the
  camera-center ray, so the barrel tracks the cursor itself.
- `WeaponAimAlignment.LateUpdate` (only while `playerController.IsAiming`):
  - `desiredDirection = AimPoint - muzzlePoint.position`
  - `correction = Quaternion.FromToRotation(muzzlePoint.forward, desiredDirection)`
  - `targetRotation = correction * weaponAimPivot.rotation`, then `Quaternion.Slerp`
    toward it (sharpness 20).
  - Result: the **weapon's aim pivot rotates so the barrel points at the mouse
    world-point**. Debug rays: **blue** = where barrel actually points, **green** = where
    it *should* point, **red** = line to AimPoint.

> **Note:** Layer B aims the *body* at the camera-center ray; Layer C aims the *barrel*
> at the mouse-projected point. The blue vs. green debug rays in `WeaponAimAlignment`
> visualize any remaining divergence — the gap between the "cursor world point (10m
> plane)" and the "camera center hit point".

### Supporting Layer — Locomotion / Aim Mode

- `PlayerLocomotionState.Tick`: when `IsAiming`, calls `RotateTowardsCameraForward()`
  (horizontal only) so the **character faces the camera**; A/D become strafe, S becomes
  backward movement.
- `PlayerAnimator.SetAiming(isAiming)` flips the `isAiming` Animator bool -> ADS
  animation state.
- `RootMotionRelay.OnAnimatorMove`: in aim mode, the **horizontal root-motion direction
  is replaced with the input direction** (stride length preserved). This prevents
  forward-authored clips from making S move the player forward.

### Left-Hand IK — `PlayerLeftHandIK`

- Snaps `leftHandTarget` to the weapon's `LeftHandGripPoint` and blends
  `handRig.weight` 0 -> 1 so the off-hand stays on the rifle grip.

---

## 4. What Happens on RMB Release

`IsAiming` becomes false, and the whole pipeline reverses:

- `UpdateCameraPriority(false)` -> aim camera priority 0 -> Cinemachine blends back to
  the free camera.
- Free camera look input re-enabled.
- `SyncAimTargetWithCamera()` continues updating while not aiming, so the next ADS entry
  starts from the current free-camera orientation.
- `spineRig.weight` -> 0 (body returns to normal).
- `isAiming` Animator bool -> false.
- `WeaponAimAlignment` stops correcting (weapon stays where it last pointed).

---

## 5. Flow Charts

### A. Overall Game Loop (per frame)

```
UNITY GAME LOOP (per frame)
          |
          v
 PlayerInputReader  (-100)
 Reads Move/Look/Sprint/Jump/DiveRoll/Interact/RMB
          |
          v
 TPSCameraController  (-50)
 IsAiming? -> switch camera priority
 Aim: manual yaw/pitch on aimCameraTarget
          |
          v
 PlayerAimRigController  (-1)
 raycast cam-center -> spineAimTarget
 blend spineRig weight 0->1
          |
          v
 PlayerController.Update  (0)
 stateMachine.Tick()
  |- Locomotion/Jump/DiveRoll
  |- RotateTowardsCameraForward (while aiming)
  `- PlayerAnimator: SetAiming + UpdateLocomotion
          |
          v
 PlayerInteractionSystem  (0)   weapon detect + equip
          |
          v
 MouseWorldSphereTest  (0)
 mouse -> ScreenToWorldPoint (fixedDepth 10m) -> AimPoint
          |
          v
 RootMotionRelay  (OnAnimatorMove)
 root motion -> ApplyRootMotionMovement
 (aim: horizontal direction overridden by input)
          |
          v
 LateUpdate:
 WeaponAimAlignment -> rotate weaponAimPivot toward AimPoint
 PlayerLeftHandIK   -> snap hand to LeftHandGripPoint
```

### B. Detailed Aim Pipeline

```
RMB held -> PlayerInputReader.RightMouseHeld = true
                  |
                  v
   PlayerController.IsAiming  (HasRifle && RMB held)
                  |
   +-------------+-------------+------------------+---------------+
   |             |             |                  |               |
   v             v             v                  v               v
 [A] CAMERA    [B] BODY/     [C] BARREL        [D] LOCOMOTION  [E] LEFTHAND
 TPSCamera     SPINE         MouseWorldSphere  PlayerLocomotion PlayerLeft
 Controller    PlayerAimRig  + WeaponAimAlign  + RootMotionRelay HandIK
   |             |                  |                  |              |
 copy free-cam  ray from aim       |            face character   snap hand to
 yaw/pitch from camera screen      |            toward camera    LeftHandGrip
 view-center   center (50%,50%)    |            forward (strafe)  Point; handRig
 world point   |                   |                  |          weight 0->1
   |             v                  |            A/D strafe,         |
   v        spineAimTarget     mouse cursor        S moves back      |
 aimCameraPri- .position =     -> ScreenToWorld    |                 |
 ority 10->20  hit.point or    Point (fixed         |                 |
 (blend to     200m fallback    depth 10m)          |                 |
 aim camera)       |                 |              |                 |
   |               v                 v              |                 |
   v         spineRig.weight    AimPoint (world)    |                 |
 yaw+=L.x*  0 -> 1 (torso     -> WeaponAimAlign     |                 |
 sens;       rotates toward    correction =          |                 |
 pitch-=L.y* target point)     FromToRotation(       |                 |
 sens; clamp                    muzzle.forward,      |                 |
 [-35,70]                        AimPoint-muzzle)    |                 |
   |                              targetRot =        |                 |
   v                              correction*        |                 |
 aimCameraTarget.                 pivot.rot;         |                 |
 rotation =                       Slerp ->           |                 |
 Euler(pitch,yaw)                 BARREL POINTS AT   |                 |
   |                              CURSOR WORLDPOINT  |                 |
   v                               |                 |                 |
 CAMERA LOOKS                     +------------------+-----------------+
 WHERE MOUSE
 LOOKS (zoom)     +---- PlayerAnimator.isAiming = true -> ADS anim state
                  |        (whole character: body + gun + camera align)
                  |        body -> camera center ray
                  |        barrel -> mouse world point
                  v
RMB released -> IsAiming = false -> pipeline reverses:
   aimCam priority 0 (blend back), free-cam input on,
   spineRig.weight -> 0, isAiming = false, barrel stops correcting
```

### C. Execution Order Timeline

```
 -100  PlayerInputReader
  -50  TPSCameraController
   -1  PlayerAimRigController
    0  PlayerController -> StateMachine -> Locomotion/Animator
    0  PlayerInteractionSystem (pickup)
    0  MouseWorldSphereTest (AimPoint)
  Animator update -> RootMotionRelay.OnAnimatorMove
  LateUpdate      -> WeaponAimAlignment (barrel)
  LateUpdate      -> PlayerLeftHandIK (hand)
```

---

## 6. Key Files

| File | Responsibility |
|---|---|
| `Assets/Scripts/Player/PlayerInputReader.cs` | All gameplay input state |
| `Assets/Scripts/Player/PlayerController.cs` | Movement, rotation, ground/jump/dive-roll, `IsAiming` |
| `Assets/Scripts/Player/PlayerStateId.cs` | State machine + `IPlayerState` |
| `Assets/Scripts/Player/PlayerLocomotionState.cs` | Rotation mode (aim strafe vs free) + animator drive |
| `Assets/Scripts/Player/PlayerAnimator.cs` | Animator param hashing + rifle/aim state |
| `Assets/Scripts/Player/TPSCameraController.cs` | Free/aim camera blend + aim yaw/pitch |
| `Assets/Scripts/Player/PlayerAimRigController.cs` | Spine aim target + rig weight blend |
| `Assets/Scripts/Player/MouseWorldSphereTestMouseWorldSphereTest.cs` | Cursor -> world `AimPoint` |
| `Assets/Scripts/Gun/WeaponAimAlignment.cs` | Barrel pivot rotation toward `AimPoint` |
| `Assets/Scripts/Gun/RifleAimAlignmentDebug.cs` | Debug rays for barrel alignment |
| `Assets/Scripts/Player/PlayerInteractionSystem.cs` | Weapon detect + equip |
| `Assets/Scripts/Gun/WeaponPickupItem.cs` | Attach to socket, disable physics |
| `Assets/Scripts/Player/PlayerLeftHandIK.cs` | Off-hand grip IK |
| `Assets/Scripts/Player/RootMotionRelay.cs` | Root motion -> CharacterController (aim-safe) |