---
name: git-workflow
description: Repository hygiene and release preparation for Unity assets, git commits and PR descriptions.
---

# Git workflow skill

1. Read `agents/github-agent.md` and `AGENTS.md`.
2. Run `git status --short`, `git diff --stat`, `git diff --cached --stat` and inspect changed Unity `.meta` pairs/renames before staging.
3. Stage only scoped files; propose commit split if code, data bake and scene serialization change together.
4. Check LFS patterns for FBX/large binary assets. Avoid adding generated Library/Temp/logs and API keys.
5. Require recorded test outcome (not assumptions) in commit/PR summary.
6. Show commit message and exact push target for approval. Never push unless explicitly instructed.
