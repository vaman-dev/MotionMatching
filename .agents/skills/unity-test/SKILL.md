---
name: unity-test
description: Plan and run unit, Unity EditMode/PlayMode and performance checks for motion matching.
---

# Unity test skill

1. Read `agents/test-agent.md`, `docs/data/FEATURE_SCHEMA.md`, `docs/benchmarks/README.md`.
2. Implement deterministic golden fixtures for local-space transforms, finite differences, frame boundaries and search oracle parity.
3. Separate `python -m unittest` scaffold tests from actual Unity Test Framework execution.
4. When Unity is available use the configured Editor version and Test Runner; capture test report/Console and do not claim unrun tests pass.
5. For KD-tree compare the exact same features, weights, tags and tie policy as brute force.
6. For ANN/Sentis compare a frozen ONNX fixture with Python outputs and report error tolerance.
