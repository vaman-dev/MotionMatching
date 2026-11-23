# ReusablePackages.md — component extraction and publishing policy

**Rule:** research code is *not* automatically a reusable package. Package candidates earn extraction through demonstrated reuse, isolated dependencies and tests. This file tracks components intended for later Unity Package Manager and possible Asset Store distribution.

## Candidate packages (planned, not implemented)

| Candidate | Standalone value | Extraction gate |
|---|---|---|
| `MotionDataSampler` | Deterministic clip sampling, root/joint trajectories, editor diagnostics | No dependency on project player/camera; repeatable bake fixture |
| `MotionFeatureDatabase` | Versioned feature storage and validation | Stable schema, documented serialization and migration tests |
| `MotionSearch` | Brute force/KD-tree algorithms against a generic feature data interface | Proven parity and performance data; no Unity scene references |
| `TrajectoryPreview` | Editor visualization/gizmos of future paths | Clean inspector integration, no hot-loop allocations |
| `MotionBenchmark` | Benchmark runner/export utility | Reproducibility and representative sample data |
| `SentisMotionAdapter` | Neural projector inference bridge | Optional package dependency, shape/feature parity, model lifecycle |

## Provisional package layout (only create when candidate passes gate)

```text
Packages/
  com.yourstudio.motionsearch/
    package.json
    Runtime/
      YourStudio.MotionSearch.asmdef
    Editor/
      YourStudio.MotionSearch.Editor.asmdef
    Tests/
      Runtime/
      Editor/
    Samples~/
    Documentation~/
    CHANGELOG.md
    LICENSE.md
```

**Do not create placeholder packages in active `Packages/` yet**. Unity may try to resolve/integrate them before their APIs are real. Create them in a deliberate milestone, test embedded package installation, then test from a fresh Unity project.

## Publication quality gate

1. Stable documented public API, assembly names and semver.
2. Unrelated shooter gameplay eliminated; sample works without proprietary assets.
3. Editor UI is keyboard accessible, Undo/Redo-aware, null-safe and resists invalid imports.
4. Validation messages are actionable; no heavy work in editor `OnGUI` hot paths.
5. Third-party clip and model rights verified; never distribute unlicensed motion packs.
6. Tests passing on supported Unity versions, clean fresh-import test, no project settings overrides.
7. Manual package review, demo scene, license, version notes and dependency declaration.
8. Asset Store submission format and packaging rules verified against current Unity publisher documentation before release.

## Component ledger

| Component | Status | Owner | Evidence / next action |
|---|---|---|---|
| Clip sampler | idea | project owner | M2 needs tested bake |
| Feature database | idea | project owner | M2 schema contract |
| Search backends | idea | project owner | M3/M4 parity test |
| Editor diagnostics | idea | project owner | M2/M3 UX tests |
| Sentis adapter | idea | project owner | M6 ONNX golden data |

Keep this ledger factual. A passing test in the research project is not equivalent to importing a self-contained package into a clean game.
