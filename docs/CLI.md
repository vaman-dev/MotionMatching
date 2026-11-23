# CLI quick reference (Windows PowerShell + Codex)

From repository root (with Python 3.10+, Git and Codex installed):

```powershell
# Check tooling and repository state
powershell -ExecutionPolicy Bypass -File tools/mm.ps1 doctor
powershell -ExecutionPolicy Bypass -File tools/mm.ps1 status

# Validate scaffold (does not invoke Unity)
powershell -ExecutionPolicy Bypass -File tools/mm.ps1 validate
powershell -ExecutionPolicy Bypass -File tools/mm.ps1 test

# Current assembly checks and M1 source preservation (do not invoke Unity)
py -3 -B tools/validate_assemblies.py
py -3 -B tools/validate_migration.py --mode m1 --baseline .utmp/m0-baseline/manifest.json

# Start Codex in project context
codex
```

Within Codex: run `/plan` before multi-file edits, ask for named agents (`github_agent`, `bug_agent`, `test_agent`, `documentation_agent`) explicitly as needed, use `/review` before commits and `/compact` after milestones. These are workflow conventions; actual CLI availability/menus depend on installed Codex version.

Suggested first Codex instruction:

> Read AGENTS.md, PROJECT.md, PLAN.md, MotionMatchingArchitecture.md, Unity.md and the original Report.md. M0 only: inspect status, document baselines and cross-reference uncommitted work. Do not edit the Unity scene, assets or input files yet. Present a migration diff plan and list missing runtime evidence.

The GitHub agent may **propose** commit and push commands, but it must not push without explicit approval. `codex.yml` is a manually dispatched read-only review and requires repository secret `OPENAI_API_KEY`. It is not the user-signed-in Codex CLI session.

## Agent configuration and permission boundaries

The four project roles are standalone TOML files in `.codex/agents/`, each with `name`, `description` and `developer_instructions`. Their instructions link to the matching `agents/*-agent.md` playbook and `.agents/skills/*/SKILL.md` workflow. `.codex/config.toml` enables agents and caps concurrent spawned threads at four, excluding the primary agent. No model is pinned.

Custom agents inherit the parent's permissions. A `sandbox_mode` value in a standalone custom agent file does not create a separate sandbox. `bug_agent` is instructed to remain read-only; for an enforced read-only session, select read-only permissions on the parent before delegation. See the [official OpenAI subagent documentation](https://learn.chatgpt.com/docs/agent-configuration/subagents).

## Verified local tooling (2026-11-23)

- `codex --version`: `codex-cli 0.171.0`.
- `codex app-server --strict-config --print-startup-config`: exit 0; validates startup configuration without starting a model task. This does not prove each agent's runtime behavior.
- The `python` command resolves to a Windows Store alias and fails. `py -3` provides Python `3.10.11`; `tools/mm.ps1` selects a usable launcher.
- The helper invokes Python with `-B` to avoid generated `__pycache__` directories. For direct checks, use `py -3 -B tools/validate_scaffold.py` or `py -3 -B -m unittest discover -s tools/tests -v`.
- Python 3.10 lacks `tomllib`, and PyYAML is absent locally. The validator explicitly reports skipped TOML/YAML parsing while still checking required files, agent references and JSON. Python 3.11+ enables built-in TOML parsing. CI uses Python 3.12 and pinned PyYAML for both parsers.

Both workflows were inspected statically. `github.yml` checks repository structure and scaffold tests on pull requests, manual dispatch and pushes to `main` or `master`; the latter was added because the actual baseline is on local `master`. No Git remote is configured, so a remote default branch is unverified. `codex.yml` performs an optional manual review with read-only repository permissions and a fixed prompt file. Neither compiles Unity. No GitHub Actions run or authenticated API review was performed during this inspection. The [official OpenAI GitHub Action documentation](https://learn.chatgpt.com/docs/github-action) describes the secret, prompt and final-message output used here.

## Unity CLI and live Editor inspection

User-authorized installation on 2026-11-23 added `com.unity.pipeline` 1.0.1 through Unity CLI 1.0.1. The existing Unity 6000.6.0f1 Editor loaded the package and reports ready. Commands below were verified against this installed CLI; use the installed executable path if it is absent from PATH.

```powershell
unity status --json
unity command scene list_open --project-path . --json
unity command scene get_hierarchy --project-path . --json
```

The scene commands read current Editor memory, including dirty-state flags and hierarchy; they do not open or save scenes. Initial inspection found MotionMatchingTest active with seven roots and 102 GameObjects; M1 detachment reduces it to 82 objects. Readiness does not certify gameplay. Package backups and exact installation/test evidence are recorded in PROGRESS.md. Additional package/provider installations still require task authorization; no second bridge was installed.

M0 preservation still requires a byte-identical clone. After deliberate M1 detachment, use the explicit M1 mode above: it preserves the original scene/controller, metadata, source assets, settings and recorded deletions, allowing only the two motor/relay source edits, authorized package files and dedicated scene changes. Assembly validation is also part of the repository hygiene workflow. These checks do not replace Unity test execution.
