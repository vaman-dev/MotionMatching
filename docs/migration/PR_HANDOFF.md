# M1 pull request handoff

## Scope

The review branch is `feature/motion-matching-m1-pr`, targeting
`vaman-dev/MotionMatching`. The destination had no refs at inspection. Its
`main` base is initialized from the original local baseline commit `13c8484`;
the existing local `main` branch is preserved.

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
reviewed replacements are provided. The PR must remain draft for this reason.
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
