# Motion Matching — Intelligent Character Locomotion

Portfolio-grade Unity character locomotion. The local workspace has an M2 database baker and an experimental M3C KD-tree/Playables locomotion integration. CharacterMotor still owns capsule movement and the Animator component receives the single Playables output. Polished movement and full regression acceptance remain open. Start with [current status](docs/STATUS.md) for evidence and open gates.

All project-owned C# lives under `Assets/Scripts/MotionMatching` in five assemblies; animations live outside Scripts. Open **Tools > Motion Matching > Motion Database Baker** to bake or inspect the initial locomotion database. See the [M2 report](docs/migration/M2_IMPLEMENTATION_REPORT.md) for setup and limitations, including local model/Avatar prerequisites.

This public code branch does not publish the locally imported Kevin Iglesias animation pack, baked pose databases, or the local scene's references to them. Import licensed source animations into your own Unity project and bake a compatible database before enabling the experimental matching component. The checked-in scene remains the grounded migration baseline; local M3C test results in the documentation are not a clean-checkout acceptance claim.

## Operating model

Read only what the task needs; this list is a map, not a startup checklist.

1. `docs/STATUS.md` — compact current state; `docs/PROJECT.md` — purpose and scope.
2. `docs/PLAN.md` and `docs/PROGRESS.md` — ordered milestones and factual evidence.
3. `docs/MotionMatchingArchitecture.md` — system/data contracts and search backends.
4. `docs/Unity.md` — coding and serialized-asset rules.
5. `docs/ReusablePackages.md` — criteria for extracting independently reusable UPM/Asset Store components.
6. `AGENTS.md`, `agents/*.md`, `.codex/agents/*.toml`, `.agents/skills/*/SKILL.md` — Codex guidance, four roles and reusable workflows.
7. `.github/workflows/github.yml` — repository/document validation. The Codex GitHub Actions workflow was removed at the owner's request; repository CI does not compile or test Unity.

## Development and preservation

- `tools/Preserve-Worktree.ps1` captures tracked and nonignored untracked source files, metadata and recorded deletions into a verified ZIP/SHA256 manifest under a new ignored workspace destination. It excludes ignored caches, local credentials, Git configuration/index and history; retain the existing Git object database. Keep local backups secure and unpublished.
- Inspect live branch/status before editing; preserve unrelated changes and require explicit publication authorization.
- `Assets/Scenes/MotionMatchingTest.unity` is the sole enabled build scene. C2 retired the obsolete Test scene, rifle prefabs and nine LegacyShooter compatibility scripts after auditing references. Preserve retained character and source animation assets.
- Source removal requires a dependency audit and task authorization; keep GUID-bearing metadata with retained assets.
- On Windows use `powershell -NoProfile -ExecutionPolicy Bypass -File tools/mm.ps1 doctor`, then `validate` and `test`. The helper selects a usable Python 3.10+ interpreter. Python 3.11+ enables built-in TOML parsing; PyYAML enables workflow parsing. Missing parsers are explicitly skipped locally; CI uses Python 3.12 and pinned PyYAML.
- `py -3 -B tools/validate_migration.py --mode consolidation --baseline .utmp/consolidation-start/manifest.json --move-map docs/migration/consolidation-moves.json` checks the current relocation against the preservation snapshot and recorded source/destination map. Historical M0/M1 modes remain for their original layouts. `tools/validate_assemblies.py` checks the five assembly boundaries. These Python checks do not compile Unity.
- The Editor-only `MigrationBaselineCapture.RunBatch` observes the retained player through synthetic Input System events in a dedicated batch session. See [the baseline record](docs/migration/BASELINE.md) for invocation and evidence limits. It never saves scenes.

## Project facts used

- Actual configuration: Editor `6000.6.0f1`, URP `17.6.0`, Input System `1.20.0`, Cinemachine `6.6.0`, Animation Rigging `6.6.0`, CharacterController and Humanoid avatar.
- PlayerController schedules gravity/carry in LateUpdate; CharacterMotor owns the sole capsule Move call. RootMotionRelay supplies frame-local horizontal animation deltas. Current scheduling supports normal Animator evaluation.
- Existing report lists 31 `.anim` and 109 `.fbx` assets; this is not proof of usable locomotion coverage.
- Trajectory prediction and primary KD-tree matching with a brute-force correctness reference are implemented locally. Next are natural locomotion polish and asset-complete validation; MLP matching, Unity neural inference and the portfolio demo follow later.

## Source references

- GDC 2016 slides: https://ubm-twvideo01.s3.amazonaws.com/o1/vault/gdc2016/Presentations/Clavet_Simon_MotionMatching.pdf
- GDC session: https://www.gdcvault.com/play/1023280/
- Holden et al. (2020) official paper: https://static-wordpress.ubisoft.com/montreal.ubisoft.com/wp-content/uploads/2020/07/09154101/Learned_Motion_Matching.pdf
- Unity Sentis package documentation: https://docs.unity.cn/Packages/com.unity.ai.inference%402.6/manual/index.html
- Codex agents: https://learn.chatgpt.com/docs/agent-configuration/subagents
- Codex GitHub Action: https://github.com/openai/codex-action

## Current milestone

M2's local functional gate is validated; M3C matching is experimental and has unresolved transition and action-regression gates. [STATUS](docs/STATUS.md) summarizes the current facts; [PLAN](docs/PLAN.md) owns exit gates and [PROGRESS](docs/PROGRESS.md) retains dated evidence. No Neural assembly or placeholder packages exist. Historical shooter architecture remains archived as documentation, not live instructions.
