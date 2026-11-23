# Directory consolidation dependency audit

Audit before asset moves, 2026-11-23. All 28 C# files under Assets are project-owned;
no vendor C# or DLL files were found there. Package code keeps package-owned assemblies.
The preserved original scene, recovery scene and rifle prefab still use shooter code.
There is no safely unreferenced shooter subsystem to delete in this milestone.

Targets below are relative to `Assets/Scripts/MotionMatching/`.

| Script | Classification | Target | Evidence / retained responsibility |
|---|---|---|---|
| RootMotionAccumulator | MOVE | Core | Motor and tests; frame-local root buffer |
| CharacterMotor | MOVE | Runtime/Motor | Sole capsule Move owner |
| FreeCameraLookInput | MOVE | Runtime/Input | Direct MotionMatchingTest component |
| AssemblyBoundaryAudit | MOVE + REFACTOR | Editor | Compiled graph and source ownership checks |
| MigrationBaselineCapture | MOVE | Editor | Existing actual-input regression observation tool |
| RootMotionAccumulatorTests | MOVE | Tests/EditMode | Core and compiled boundaries |
| LocomotionSceneTests | MOVE + REFACTOR | Tests/EditMode | Relocated controller path and assembly resolution |
| CharacterMotorTests | MOVE | Tests/PlayMode | Root lifecycle and collision tests |
| LegacyMotorRegressionTests | MOVE + REFACTOR | Tests/PlayMode | Replace Assembly-CSharp lookup with actual production types |
| PlayerController | MOVE + REVIEW | Runtime/Character | Required by all three scenes; gameplay gravity, ground/action guards and motor schedule; original aim compatibility |
| PlayerInputReader | MOVE | Runtime/Input | Required by all three scenes; original interaction/RMB fields still used |
| PlayerStateId | MOVE | Runtime/States | Also contains IPlayerState and PlayerStateMachine; controller/actions/capture use them |
| PlayerLocomotionState | MOVE | Runtime/States | Constructed by controller; free locomotion and original aim rotation |
| PlayerJumpState | MOVE | Runtime/States | Required authored jump |
| PlayerDiveRollState | MOVE | Runtime/States | Required authored dive |
| PlayerAnimator | MOVE + REVIEW | Runtime/Animation | Required by all scenes; rifle facade remains used by preserved scene/interaction |
| RootMotionRelay | MOVE | Runtime/Animation | Required by all scenes; horizontal root submission and action-tag filtering |
| FootstepAnimationEventReceiver | MOVE | Runtime/Animation | Required scene component and eight clips' animation events |
| FootstepResetStateBehaviour | MOVE + REVIEW | Runtime/Animation | No callers found; reusable callback retained conservatively |
| TPSCameraController | MOVE + REVIEW | Runtime/LegacyShooter | Original Test component; dedicated scene already uses FreeCameraLookInput |
| PlayerInteractionSystem | MOVE + REVIEW | Runtime/LegacyShooter | Original Test and recovery components; dirty user source |
| PlayerLeftHandIK | MOVE + REVIEW | Runtime/LegacyShooter | Original Test component; depends on interaction/pickup/Rigging |
| PlayerAimRigController | MOVE + REVIEW | Runtime/LegacyShooter | Original Test component; Rigging/controller/aim provider |
| PlayerAimTargetProvider | MOVE + REVIEW | Runtime/LegacyShooter | Original Test component; used by interaction/alignment/rig |
| MouseWorldSphereTestMouseWorldSphereTest | MOVE + REVIEW | Runtime/LegacyShooter | RifleAimAlignmentDebug uses its class; untracked user source |
| WeaponPickupItem | MOVE + REVIEW | Runtime/LegacyShooter | Rifle prefab component and interaction/IK dependency; dirty user source |
| WeaponAimAlignment | MOVE + REVIEW | Runtime/LegacyShooter | Rifle prefab component; untracked user source |
| RifleAimAlignmentDebug | MOVE + REVIEW | Runtime/LegacyShooter | Rifle prefab component; untracked user source |

## Serialized evidence

MotionMatchingTest directly references PlayerInputReader (`0808400b5e73e6b4e921889bf2d04486`),
PlayerController (`7c50137fe4c1e494b9fb0ecd91473138`), FreeCameraLookInput
(`3a9f7e4a51020eb45bec3a9fbe62fcd7`), FootstepAnimationEventReceiver
(`edadd5cd5f8e149478d437f32ce829f7`), PlayerAnimator
(`9dbd71d69e3ffd44dabf05afd4c37ad7`) and RootMotionRelay
(`49b229e8197cfbf499af6dec1552eef7`).

Original Test references Interaction (`4671d8fd8b711e440a2b22054d9472b9`),
TPSCamera (`5368a99841cae4d4c869655a8fecb3a0`), LeftHandIK
(`d8cdad7b6cc709d419d466baf84d525a`), AimRig
(`41ff7bb3ba91d1e48a20261fb8700f95`) and AimTarget
(`57dd368cd000eaa4493ac79b716669ea`). The dirty `ar15.prefab` references
WeaponPickupItem (`3fda23a64c0b5cf4489bd41d9076bd65`), RifleAimAlignmentDebug
(`1162fa230a2986c43b8f39dde3aeb659`) and WeaponAimAlignment
(`5ba3e25f48034fc4ea1b320d60ca392f`). Recovery scene `Assets/_Recovery/0.unity`
also retains interaction and shared character components.

Eight clips contain PlayFootstep events: LeftStraffe, walking, Fast Run,
RightStraffeRun, Walking Backward, LeftStraffRun, RightStraffe and Run Backward.
RightStraffeRun also invokes PlayRightFootstep. Plain state classes need no
serialized GUID caller to remain required.

## Decisions before relocation

- Move source and original metadata together, preserving class names/namespaces.
- Runtime adds Unity.Animation.Rigging for retained compatibility. Editor adds
  Input System and Cinemachine for the moved capture harness. Keep five assemblies.
- No project SerializeReference payloads were found. Scenes contain old
  Assembly-CSharp diagnostic class identifiers beside MonoScript GUIDs; verify
  actual Unity resolution after relocation rather than broad scene rewrites.
- Move the complete animation directory to Assets/Animations/MotionMatching/Source;
  preserve source/importer contents, provenance and existing missing/deleted assets.
  Move the unarmed controller alongside Source. This does not grant distribution rights.
- Keep PlayerAnimator rifle fields and APIs, Controller aim compatibility and legacy
  camera logic while original scene/prefab dependencies remain. Dedicated scene has
  none of their shooter writers. Retiring these assets is a separate scope decision.
- DELETE classification applies only to verified empty former folder containers
  after relocation. Zero C# implementations or animation sources are deleted.

This audit is static. Compilation, tests, actual-input capture, source ownership,
scene/Animator references and build-scene checks must rerun after the moves.
