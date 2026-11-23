# Motion Matching Research

**Status:** Initial M1 locomotion migration implemented and validated with seven Unity EditMode tests, six PlayMode tests and an interactive movement/camera capture. The retained Animator supplies animation root deltas to a single motor; gravity advances independently. No motion database, search backend or neural model is implemented yet. Actual evidence and remaining terrain/contact checks are recorded in `PROGRESS.md`.

The scaffold is now integrated with the existing `TPS_Shooter` repository. `Report.md` remains the historical architecture snapshot. Current source/settings evidence and the concrete migration plan are in [the repository assessment](docs/migration/REPOSITORY_ASSESSMENT.md).

## Operating model

1. `PROJECT.md` — purpose and scope.
2. `PLAN.md` and `PROGRESS.md` — ordered milestones and factual evidence.
3. `MotionMatchingArchitecture.md` — system/data contracts and search backends.
4. `Unity.md` — coding and serialized-asset rules.
5. `ReusablePackages.md` — criteria for extracting independently reusable UPM/Asset Store components.
6. `AGENTS.md`, `agents/*.md`, `.codex/agents/*.toml`, `.agents/skills/*/SKILL.md` — Codex guidance, four roles and reusable workflows.
7. `.github/workflows/github.yml` — repository/document validation; `.github/workflows/codex.yml` — optional, manually triggered, read-only Codex report. Neither claims to compile/test Unity.

## Development and preservation

- `tools/Preserve-Worktree.ps1` captures tracked and nonignored untracked source files, metadata and recorded deletions into a verified ZIP/SHA256 manifest under a new ignored workspace destination. It excludes ignored caches, local credentials, Git configuration/index and history; retain the existing Git object database. Keep local backups secure and unpublished.
- Migration branch: `feature/motion-matching-migration`. Existing user changes remain unstaged.
- The original `Assets/Scenes/Test.unity` remains the enabled build scene, with its original scene/controller bytes preserved. `Assets/Scenes/MotionMatchingTest.unity` is the dedicated unarmed scene: weapon, table, nearby pickup detection, ADS and weapon constraints are detached. Preserve all source animation assets.
- Do not delete rifle scripts or Animator assets as the first operation. Remove serialized component references and dependencies in a scene clone, then delete obsolete assets in separate, reviewable commits.
- On Windows use `powershell -NoProfile -ExecutionPolicy Bypass -File tools/mm.ps1 doctor`, then `validate` and `test`. The helper selects a usable Python 3.10+ interpreter. Python 3.11+ enables built-in TOML parsing; PyYAML enables workflow parsing. Missing parsers are explicitly skipped locally; CI uses Python 3.12 and pinned PyYAML.
- `py -3 -B tools/validate_migration.py --mode m1 --baseline .utmp/m0-baseline/manifest.json` verifies protected original bytes/deletions and unique clone GUID while allowing the explicit motor/package/clone edits. Default M0 mode still requires identical scene content. `tools/validate_assemblies.py` checks the five assembly boundaries. These Python checks do not compile Unity.
- The Editor-only `MigrationBaselineCapture.RunBatch` observes the retained player through synthetic Input System events in a dedicated batch session. See [the baseline record](docs/migration/BASELINE.md) for invocation and evidence limits. It never saves scenes.

## Project facts used

- Actual configuration: Editor `6000.6.0f1`, URP `17.6.0`, Input System `1.20.0`, Cinemachine `6.6.0`, Animation Rigging `6.6.0`, CharacterController and Humanoid avatar.
- PlayerController schedules gravity/carry in LateUpdate; CharacterMotor owns the sole capsule Move call. RootMotionRelay supplies frame-local horizontal animation deltas. Current scheduling supports normal Animator evaluation.
- Existing report lists 31 `.anim` and 109 `.fbx` assets; this is not proof of usable locomotion coverage.
- Desired future path: exact brute-force → KD-tree comparison → MLP-based learned projector → Sentis ONNX inference → optional learned stepping/decompression.

## Source references

- GDC 2016 slides: https://ubm-twvideo01.s3.amazonaws.com/o1/vault/gdc2016/Presentations/Clavet_Simon_MotionMatching.pdf
- GDC session: https://www.gdcvault.com/play/1023280/
- Holden et al. (2020) official paper: https://static-wordpress.ubisoft.com/montreal.ubisoft.com/wp-content/uploads/2020/07/09154101/Learned_Motion_Matching.pdf
- Unity Sentis package documentation: https://docs.unity.cn/Packages/com.unity.ai.inference%402.6/manual/index.html
- Codex agents: https://learn.chatgpt.com/docs/agent-configuration/subagents
- Codex GitHub Action: https://github.com/openai/codex-action

## Next milestone

M2 audits animation data and introduces reproducible feature extraction/database baking. The five implemented assembly boundaries are documented in `MotionMatchingArchitecture.md`; folder ownership is in [the directory guide](docs/migration/DIRECTORY_LAYOUT.md). Keep grounded Animator playback until the expanded locomotion matrix passes before M3 replacement. No Neural assembly or placeholder UPM packages now. See `PLAN.md` for the hard exit gates.
