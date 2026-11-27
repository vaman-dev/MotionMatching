# CLI quick reference (Windows PowerShell + Codex)

From repository root (with Python 3.10+, Git and Codex installed):

```powershell
# Check tooling and repository state
powershell -ExecutionPolicy Bypass -File tools/mm.ps1 doctor
powershell -ExecutionPolicy Bypass -File tools/mm.ps1 status

# Validate scaffold (does not invoke Unity)
powershell -ExecutionPolicy Bypass -File tools/mm.ps1 validate
powershell -ExecutionPolicy Bypass -File tools/mm.ps1 test

# Current assembly and consolidation preservation checks (do not invoke Unity)
py -3 -B tools/validate_assemblies.py
py -3 -B tools/validate_migration.py --mode consolidation --baseline .utmp/consolidation-start/manifest.json --move-map docs/migration/consolidation-moves.json

# Start Codex in project context
codex
```

Within Codex: plan substantial edits, review before commits and compact at milestone boundaries. Use one agent by default; ask for a named role only for substantial independent work or review. Slash-command availability depends on the installed client.

Suggested first Codex instruction:

> Follow AGENTS.md and read docs/STATUS.md once. Inspect the scoped diff and affected source. Load only relevant contract sections; preserve unrelated changes. Plan substantial edits and run checks appropriate to the change.

## Context and delegation budget

- `docs/STATUS.md` is the short current-state entry point. PROJECT owns purpose, PLAN owns milestone gates, architecture/schema own contracts and PROGRESS owns dated evidence. Read relevant sections rather than every file.
- Historical Report/ARCHITECTURE/newArch and migration reports are searchable references, not startup requirements. Keep original historical evidence intact.
- Reuse information already read unless it changed. A delegate gets exact ownership, relevant paths, existing results and a compact expected response; omit full thread history when unnecessary. Do not rerun completed exploration or tests without a reason.
- Keep raw logs in ignored local artifacts. Return counts, failure excerpts, paths and limitations. Use one coordinator for live Editor compilation/Play/test operations; do not run competing Editor jobs.
- Use docs/link/config checks for prose/configuration changes; reserve Unity suites for affected Unity behavior. Record substantive changes, not every read-only answer, in PROGRESS. Update STATUS only when current facts change.
- Keep account model defaults; do not assume a cheaper model is suitable for root-motion/math investigations. Select lower effort or a supported lower-cost model explicitly for narrow routine work when appropriate. No model override is required by this repository.

The GitHub agent may **propose** commit and push commands, but it must not push without explicit approval. The owner removed the Codex GitHub Actions workflow. The project-scoped Codex agents and skills remain available; `.github/codex-review-prompt.md` remains a manual review template.

## Agent configuration and permission boundaries

The four project roles are standalone TOML files in `.codex/agents/`, each with `name`, `description` and `developer_instructions`. Their instructions link to the matching `agents/*-agent.md` playbook and `.agents/skills/*/SKILL.md` workflow. `.codex/config.toml` enables agents and caps concurrent spawned threads at two, excluding the primary agent. The cap limits concurrency, not total token usage. No model is pinned; agents inherit parent settings unless explicitly configured otherwise. Apply changed startup guidance/configuration in a new CLI session; an existing thread may retain earlier context.

Custom agents inherit the parent's permissions. A `sandbox_mode` value in a standalone custom agent file does not create a separate sandbox. `bug_agent` is instructed to remain read-only; for an enforced read-only session, select read-only permissions on the parent before delegation. See the [official OpenAI subagent documentation](https://learn.chatgpt.com/docs/agent-configuration/subagents).

## Verified local tooling (2026-11-23)

- `codex --version`: `codex-cli 0.171.0`.
- `codex app-server --strict-config --print-startup-config`: exit 0; validates startup configuration without starting a model task. This does not prove each agent's runtime behavior.
- The `python` command resolves to a Windows Store alias and fails. `py -3` provides Python `3.10.11`; `tools/mm.ps1` selects a usable launcher.
- The helper invokes Python with `-B` to avoid generated `__pycache__` directories. For direct checks, use `py -3 -B tools/validate_scaffold.py` or `py -3 -B -m unittest discover -s tools/tests -v`.
- Python 3.10 lacks `tomllib`, and PyYAML is absent locally. The validator explicitly reports skipped TOML/YAML parsing while still checking required files, agent references and JSON. Python 3.11+ enables built-in TOML parsing. CI uses Python 3.12 and pinned PyYAML for both parsers.

`github.yml` checks repository structure, assembly boundaries and Python tests on pull requests, manual dispatch and pushes to `main` or `master`. Origin is `https://github.com/vaman-dev/MotionMatching.git`, with default branch main. M1 PR #1 merged as `efad2d3`; its final repository-hygiene run passed. These are historical M1 remote results, not C1 CI coverage. The optional Codex workflow has been deleted at the owner's request; the validator no longer requires it. Repository hygiene CI does not compile Unity.

## Unity CLI and live Editor inspection

User-authorized installation on 2026-11-23 added `com.unity.pipeline` 1.0.1 through Unity CLI 1.0.1. The existing Unity 6000.6.0f1 Editor loaded the package and reports ready. Commands below were verified against this installed CLI; use the installed executable path if it is absent from PATH.

```powershell
unity status --json
unity command scene list_open --project-path . --json
unity command scene get_hierarchy --project-path . --json
```

The scene commands read current Editor memory, including dirty-state flags and hierarchy; they do not open or save scenes. Initial inspection found MotionMatchingTest active with seven roots and 102 GameObjects; M1 detachment reduces it to 82 objects. Readiness does not certify gameplay. Package backups and exact installation/test evidence are recorded in PROGRESS.md. Additional package/provider installations still require task authorization; no second bridge was installed.

M0/M1 validator modes retain their historical contracts and original paths for old snapshots. After C1 relocation, use consolidation mode with the verified local pre-refactor snapshot and reviewed move map. It checks relocated gameplay/source/metadata bytes, recorded deletions, protected settings/scenes and the unchanged historical Report. The snapshot is ignored local evidence; it is not available in a fresh checkout. Assembly validation is also part of the repository hygiene workflow. These checks do not replace Unity test execution.
