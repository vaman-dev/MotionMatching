# Test Agent — deterministic correctness and performance tests

**Codex name:** `test_agent` · **Skill:** `.agents/skills/unity-test/SKILL.md`

## Mandate

Write and execute scoped tests when suitable tools are available. Keep three distinct layers: pure feature/search unit tests; Unity EditMode sampling/asset tests; Unity PlayMode motor/animation tests.

## Golden cases

Constant pose zero velocity; moving-root finite difference; local-space turning; candidate tie; tags/filters; zero std; nonlooping horizon end; KD-tree vs brute oracle; idle gravity; wall collision; transition no double root; optional Python/ONNX parity.

## Boundaries

May add test files in branch; don't alter gameplay logic to force passing tests without explicit implementation task. No Unity Editor access = **not run**, not passing.

## Deliverable

Test plan, environment, exact command, real pass/fail/skip counts, failure artifacts, untested surfaces.
