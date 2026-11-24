# Directory ownership after consolidation

All project-owned C# has one authoritative root: `Assets/Scripts/MotionMatching/`. Directory placement follows assembly responsibilities and does not introduce a second movement or animation system. Existing source types and Unity GUIDs are preserved.

```text
Assets/
  Scripts/MotionMatching/
    Core/                         # MotionMatching.Core
    Runtime/                      # MotionMatching.Runtime
      Input/
      Character/
      Motor/
      States/
      Animation/
    Editor/                       # MotionMatching.Editor
    Tests/
      EditMode/                   # MotionMatching.Tests.Editor
      PlayMode/                   # MotionMatching.Tests.Runtime
  Animations/MotionMatching/
    Source/                       # Retained source packs, original controller/mask
    UnarmedLocomotion.controller
  Scenes/                         # MotionMatchingTest only
  Prefabs/
  Material/                       # Existing spelling retained
  Settings/
docs/                             # Live project docs and historical Report
  migration/
  decisions/
AGENTS.md
README.md
```

| Location | Responsibility / preservation decision |
|---|---|
| `Core` | Engine-independent frame-local root accumulation; future feature/search math only when implemented. |
| `Runtime/Input`, `Character`, `States` | Existing input lifecycle/free-camera Look adapter, gameplay gravity/guards and authored action states. |
| `Runtime/Motor` | Sole CharacterController.Move owner; frame-local root handoff. |
| `Runtime/Animation` | Animator facade, RootMotionRelay and footstep behavior. Relay remains required; it submits root motion without integrating gravity. |
| `Runtime/LegacyShooter` | Referenced original Test/prefab/recovery-scene compatibility; inactive in MotionMatchingTest. It includes ADS/weapon/interaction/debug dependencies whose removal would leave dangling references or change retained behavior. |
| `Editor` | Assembly audit and migration capture harness, within the existing Editor-only assembly. |
| `Tests/EditMode`, `Tests/PlayMode` | Existing tests relocated with their assembly definitions and metadata. |
| `Assets/Animations/MotionMatching/Source` | Existing animation packs, original controller and mask. Retain all source clips/import settings; filenames do not establish useful research coverage. |
| `Assets/Animations/MotionMatching/UnarmedLocomotion.controller` | Dedicated one-layer Locomotion/Jump/Dive Roll graph; source assets are referenced, not duplicated. |
| `Assets/Scenes` | MotionMatchingTest is the dedicated laboratory. Original Test, AR-15 and recovery dependencies remain preserved. Build-scene state must be checked separately. |
| `docs` | PROJECT, PLAN, PROGRESS, architecture, Unity rules, package ledger and unchanged historical Report. Root README and AGENTS provide discovery. |
| `agents`, `.agents`, `.codex`, `.github`, `tools`, `config` | Existing playbooks, discoverable skills/agents/workflows, validation and sample research configuration remain at their established paths. |

Runtime references Core, Input System, Cinemachine and Animation Rigging. Editor references Core, Runtime, Input System and Cinemachine and is Editor-only. Tests reference the appropriate production/test assemblies. There is no broad Assets/Scripts assembly, no project code left under Assets/MotionMatching or Assets/Editor, and no Neural assembly. Third-party assets are outside these boundaries.

The script/reference audit and explicit retention decisions are in [CONSOLIDATION_AUDIT.md](CONSOLIDATION_AUDIT.md); assembly details are in [the architecture](../MotionMatchingArchitecture.md). Fresh test evidence belongs in [PROGRESS.md](../PROGRESS.md), separately from historical M1 results.

The relocation inventory, fresh validation and open preexisting defects are
recorded in [CONSOLIDATION_REPORT.md](CONSOLIDATION_REPORT.md).

No empty Trajectory, Databases, Neural, Research or package directories are required. Add them when real implementations need them. Generated Library, Logs, Temp, UserSettings and local .utmp evidence remain ignored. Existing Git LFS rules, unrelated dirty files and original asset GUIDs remain protected. Standalone packages require [the publishing gate](../ReusablePackages.md).


C2 update: Runtime/LegacyShooter and its serialized consumers have been retired; Runtime no longer references Animation Rigging. Earlier table entries describe the C1 retention decision. See FINAL_CLEANUP_REPORT.md for the current inventory.
