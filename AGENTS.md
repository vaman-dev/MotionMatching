# AGENTS.md — Codex operating instructions

**Read first:** `docs/PROJECT.md`, `docs/PLAN.md`, `docs/MotionMatchingArchitecture.md`, `docs/Unity.md`, `docs/PROGRESS.md`. Refer to original `docs/Report.md` for existing source/scene ownership; it is *not* live Editor evidence.

## Non-negotiable rules

1. Execute tasks in milestone order. Do not migrate animation playback until motor gravity/root authority is verified.
2. Make the smallest coherent edit and describe exactly what was changed; don't refactor unrelated assets.
3. Never erase or overwrite existing user working-tree changes, scenes, animations, GUID-bearing `.meta` files, or Git LFS configuration.
4. No unasked `git push`, force push, destructive reset, asset deletion, or installation of packages.
5. No Unity compilation/Play Mode/test success claim unless Unity actually ran and the logs support it.
6. Default work is research, code review and tests; keep new feature schema/version reproducible and all search backends comparable.
7. Neural stages are future milestones; an MLP projector is not equivalent to Ubisoft's full Learned Motion Matching.
8. Keep secrets out of repository, workflows and logs; never include proprietary animation assets in a public package.
9. Before completing a task, update `docs/PROGRESS.md` with evidence and mark `docs/PLAN.md` checkboxes only for validated work.

## Specialized roles (project-scoped Codex agents)

- `github_agent`: Git hygiene and **proposed** commit/push commands; see `agents/github-agent.md` and skill `.agents/skills/git-workflow/SKILL.md`.
- `bug_agent`: reproducible defect investigation; see `agents/bug-agent.md` and skill `.agents/skills/bug-triage/SKILL.md`.
- `test_agent`: deterministic unit/Unity test design and execution reports; see `agents/test-agent.md` and skill `.agents/skills/unity-test/SKILL.md`.
- `documentation_agent`: architecture, decision and progress sync; see `agents/documentation-agent.md` and skill `.agents/skills/docs-sync/SKILL.md`.

Delegate narrow, non-overlapping tasks. Review agent work before merging. The owner alone controls scope, branch strategy and release/push authorization.

## Definition of validated work

- Identify modified files and intended behavior.
- Run available Python/scaffold tests; run Unity tests only in a configured Unity Editor environment.
- Provide exact command/result, known limitations and follow-ups.
- When motion-search backends change, compare to brute-force oracle using same features/filters/weights.

## Codex usage

`codex` from repository root; use `/plan` before multi-file changes and `/review` before commits; compact context between milestones. Custom agents are TOML in `.codex/agents/`; skills live in `.agents/skills/`. See `docs/CLI.md`.
