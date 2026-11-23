# M1 pull request handoff

> Historical M0/M1 record: original source paths and test artifacts below describe that stage. Current source ownership is in [DIRECTORY_LAYOUT.md](DIRECTORY_LAYOUT.md), the consolidation audit is [CONSOLIDATION_AUDIT.md](CONSOLIDATION_AUDIT.md), and current execution evidence is in [PROGRESS.md](../PROGRESS.md).

Published review: [PR #1](https://github.com/vaman-dev/MotionMatching/pull/1),
merged as `efad2d3` on 2026-11-23. It was opened as draft with the prerequisites
below. Its merge does not resolve the documented licensed-asset prerequisites.

## Scope

The published review branch is `feature/motion-matching-m1-publication`, targeting
`vaman-dev/MotionMatching`. The destination had no refs at inspection. Its
`main` base is snapshot commit `e7f01d8`, whose tree is identical to original
local baseline `13c8484`. Old history contains a 115 MB pre-LFS FBX blob, so
publication uses a separate root with the existing LFS pointers. Every original
local branch, including `main` and `feature/motion-matching-m1-pr`, is preserved;
no force push or local history rewrite was used. Publication commits `b7005f6`
and `6a255eb` reproduce the two reviewed commit trees with the new parent chain.

Commits separate repository tooling/documentation from the initial motor and
locomotion migration. The runtime commit includes the existing player input,
Animator facade and locomotion-state prerequisites, and the Unity/package
versions used in local validation. These prerequisites predate this migration.
Unrelated original scene, rifle assets, material edits and existing deletions
remain outside the commits. The existing `.gitignore` and LFS rules are retained.

## Draft status and asset prerequisites

The dedicated scene was validated in the full local working tree, not a clean
checkout of this branch. It references these preexisting untracked assets:

- `Assets/Prefabs/Chracter/Ch15_nonPBR.fbx` and its metadata (Avatar GUID
  `8cce7d34a61d4be479104c1586a5ad71`).
- The material and HDR texture under `Assets/Material/Skybox/` (including
  `Material/sky-3.mat` and `Sky-3.hdr`).

These source assets are excluded pending redistribution clearance. The skybox
metadata identifies an Asset Store package. A clean-checkout Unity scene/test
run is blocked until licensed assets are supplied with the required GUIDs or
reviewed replacements are provided. These prerequisites were the reason for
opening the PR as draft; its later merge does not certify a clean checkout.
No source assets were deleted or rewritten to prepare this PR.

## Validation

Fresh precommit validation on 2026-11-23:

- `py -3 -B -m unittest discover -s tools/tests -v`: 20 passed.
- `py -3 -B tools/validate_assemblies.py`: passed static checks.
- `py -3 -B tools/validate_scaffold.py`: passed file/reference checks; local
  TOML/YAML parsers unavailable, explicitly skipped. CI supplies parsers.
- `py -3 -B tools/validate_migration.py --mode m1 --baseline .utmp/m0-baseline/manifest.json`:
  passed preservation checks before Git publication changes.

Prior actual Unity 6000.6.0f1 validation in the full local tree: seven EditMode
and six PlayMode tests passed; the accepted 1,140-frame capture exercised
unarmed locomotion, jump, dive and free camera. See `PROGRESS.md` for results
and limitations. These Unity checks were not repeated during Git preparation.
Local ignored `.utmp` evidence is not part of the published repository.

Terrain/contact coverage, target player builds and clean-checkout Unity
validation remain open. No database bake, matching search or neural inference
is included.
