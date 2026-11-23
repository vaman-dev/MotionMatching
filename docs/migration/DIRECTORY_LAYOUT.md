# Directory ownership and staged cleanup

The migration adds cohesive first-party code beneath `Assets/MotionMatching` while preserving existing source assets, integration scripts and GUIDs. No asset moves or deletions are required for the current stage.

| Existing path | Current responsibility |
|---|---|
| `Assets/MotionMatching/Core` | Engine-independent frame-local root accumulation; Core assembly. |
| `Assets/MotionMatching/Runtime` | CharacterMotor and free-camera Look adapter; Runtime references Core, Input System and Cinemachine. |
| `Assets/MotionMatching/Editor` | Compiled assembly-boundary audit; Editor-only assembly. |
| `Assets/MotionMatching/Tests/Editor` | Core/imported-assembly tests; Editor test assembly. |
| `Assets/MotionMatching/Tests/Runtime` | Capsule tests and migration-only legacy motor regression fixtures; Runtime test assembly. |
| `Assets/MotionMatching/Animation` | Dedicated UnarmedLocomotion controller; no source animation pack relocation. |
| `Assets/Scripts/Player` | Retained gameplay/input/action/animation integration and original-scene presentation; predefined Assembly-CSharp. |
| `Assets/Scripts/Player/Animation` | Existing animation packs, original controller and mask; preserve importer metadata and source GUIDs. |
| `Assets/Scripts/Gun` | Original-scene weapon integration; inactive in MotionMatchingTest. Preserve until deliberate retirement. |
| `Assets/Editor` | Baseline observation harness in predefined Assembly-CSharp-Editor so it can observe legacy types. |
| `Assets/Scenes` | Original Test and dedicated MotionMatchingTest; original remains enabled in build settings. |
| `Assets/Prefabs`, `Material`, `Textures`, `Sounds`, `Settings` | Existing source/presentation/project assets. Retain established names, including `Chracter`. |
| `Packages`, `ProjectSettings` | Dependency and Unity configuration; preserve existing settings and authorized Pipeline installation. |
| `docs`, `agents`, `.agents`, `.codex`, `.github`, `tools`, `config` | Research/evidence, agent playbooks/skills/configuration, CI, validation tools and sample research configuration. |

Generated `Library`, `Logs`, `Temp`, `.vs`, `UserSettings`, and local `.utmp` evidence remain ignored. Existing Git LFS rules continue to own FBX storage. Do not capture packages/vendor content with a broad Assets or Scripts asmdef.

Final M1 checks preserved the original scene/controller, clips, metadata, LFS rules and Git configuration through an explicit migration allowlist. Evidence remains under `.utmp/m1-final`; the confirmed capture and seven EditMode/six PlayMode results validate the current layout's integration. ProjectSettings.asset and TimeManager.asset were restored to exact M0 bytes after Unity reserialization; pre-restoration copies remain in `settings-before-restoration`. These local artifacts are validation evidence, not distributable source or new package content.

Future feature extraction/baking, database storage, exact search and KD-tree code should enter the existing Core/Runtime/Editor boundaries according to their engine dependencies. Create focused subdirectories when real implementations need them. Add a separate training environment only when the learned experiment begins; introduce optional neural inference code/assembly at its milestone. No empty Neural, Research, package or feature placeholder directories are needed now.

Moving remaining player integrations out of `Assets/Scripts` requires a deliberate dependency split: named assemblies cannot reference Assembly-CSharp. Move only a coherent set, preserve each `.meta`, verify scene/script references and run Unity tests. Removing inactive Gun code, source clips or original-scene assets is a separate cleanup decision with reference/license review; scene detachment alone does not authorize source deletion. Standalone UPM packages remain gated by `ReusablePackages.md`.
