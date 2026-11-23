# M0 baseline record

Date: 2026-11-23. Baseline commit: `13c84848ef2451067b4f8221670fbf4f64d28b86`. Migration branch: `feature/motion-matching-migration`. This record preserves early M0 attempts; subsequent interactive baselines and M1 validation are recorded below and in `PROGRESS.md`. Extended terrain/contact checks remain open.

## Preservation

`powershell -NoProfile -ExecutionPolicy Bypass -File tools/Preserve-Worktree.ps1` archived and verified 550 existing tracked/nonignored untracked source files and recorded eight existing tracked deletions. Evidence is local and ignored: `.utmp/m0-baseline/worktree.zip` and `manifest.json`. Archive SHA256: `9f5cde6670270c233ebf1f7d2c4fe2a7abd3f18d0812ba33807a381a01a9eb2f`.

The archive is a working-source backup, not a Git-history or machine backup. It excludes ignored files, Git config/index and credentials ignored by Git. Keep the Git object database. Original agent/CLI bytes additionally exist in `.utmp/m0-baseline/original-tooling/` with their own hash manifest because environment repair ran concurrently with preservation. The source archive's protected asset/settings hashes are the baseline for migration checks.

All 515 protected preexisting paths under Assets, Packages, ProjectSettings, plus `.gitignore`, `.gitattributes` and historical Report were compared after Unity import with zero hash/existence changes. Existing user deletions remain deleted. Original scene SHA256: `bdfb11240f8ac2e2ce1a244792509bc585374af8a579c8457d389737b35cb5c3`.

The new `Assets/Scenes/MotionMatchingTest.unity` has identical bytes and unique GUID `8cb0673cccd24d9a943ed465cd47ec71`. Test's GUID remains `99c9720ab356a0642a771bea13969a05`. The clone retains all shooter content for characterization; it is not yet a locomotion-only scene. Build settings remain unchanged.

## Actual Unity execution

Initial command (repository root; substitute the installed Editor path on another machine):

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe' -batchmode -nographics -quit -projectPath (Get-Location).Path -logFile '.utmp/m0-baseline/unity-compile.log'
```

The log identifies Unity `6000.6.0f1 (f7f8ed4d1e24)`, script compilation, assembly reload and asset refresh. It ends with `Exiting batchmode successfully now!` and return code `0`; no C# compiler-error markers were found. This is actual batch import/script compilation evidence, not a player build or gameplay result. Licensing handshake/access-token refresh errors occurred before successful startup; Cinemachine emitted a missing optional HDRP assembly-reference diagnostic. Headless rendering reported unsupported conservative rasterization. These are retained in the local log; do not describe the Console as clean.

The first Editor-only capture compiled, opened the clone, entered Play Mode and recorded 50,590 samples in `playmode-baseline.json`, with full log `unity-playmode.log`. **This is a failed movement capture:** all observed movement/action inputs remained zero and capsule horizontal velocity was zero. Its zero recorded errors applies only to its delayed subscription window. It does not establish walk/sprint speed, jump, dive, turn or camera correctness. Retain it as evidence of an input-routing failure in the harness, not a gameplay defect.

The revised capture uses a transient InputSettings copy to ignore Game View focus in batch mode, queues synthetic keyboard events only during dynamic game input updates, records requested scenario separately from observed input, rejects captures with missing input coverage, and samples a fixed 60 Hz simulation. It restores its temporary input settings and removes the virtual keyboard. This measures simulation behavior, not device performance. Error collection begins across Play Mode domain reload; the complete Editor log remains required for startup/shutdown errors. Unexpected script recompilation during capture is unsupported.

The revised batch attempt logged process exit `1` before project initialization because an interactive Editor had opened this same project. Evidence: `unity-playmode-v2.log` and `unity-playmode-v2.exitcode.txt`. No capture JSON was produced by that attempt. The active Editor was left running; revised capture execution remains pending unless a later entry records a successful retry.

To retry after saving and closing other Editors using this project, choose fresh evidence paths and omit `-quit` because the tool exits after leaving Play Mode:

```powershell
$editor = 'C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe'
$captureProcess = Start-Process -FilePath $editor -WindowStyle Hidden -PassThru -ArgumentList @(
    '-batchmode', '-nographics', '-projectPath', (Get-Location).Path,
    '-executeMethod', 'MigrationBaselineCapture.RunBatch',
    '-baselineScene', 'Assets/Scenes/MotionMatchingTest.unity',
    '-baselineOutput', '.utmp/m0-baseline/playmode-baseline-v3.json',
    '-logFile', '.utmp/m0-baseline/unity-playmode-v3.log'
)
$captureProcess.WaitForExit()
$captureProcess.ExitCode
```

The tool is currently in predefined Assembly-CSharp-Editor because it observes legacy Assembly-CSharp types. It never saves the scene or replaces runtime scripts. It is an observation harness, not Unity Test Framework coverage. Analyze observed input and stable windows, excluding transition samples, before reporting capsule speeds. A completed capture does not prove correct animation contacts, collision behavior, camera look or action eligibility boundaries.

## Remaining gate evidence

Record idle, walk, sprint and turn behavior with measured capsule speeds; stationary-input/moving jumps, coyote/held-button/retrigger cases; ground/air dive timing and released-input action travel; walls, slopes, steps and ledges; free-camera look/orbit and disable/re-enable behavior. Record scene values, simulation rate, observation, Console state and known defects. Visual assessment requires a rendered Editor. The initial idle sample shows no measured horizontal drift in that run only.

Do not begin motor replacement or shooter detachment until the required M0 baseline is reproducible. M1 then keeps the current Animator, makes gravity independent of callbacks, validates the single motor, detaches shooter dependencies in the clone and introduces the documented assembly definitions.

## Subsequent interactive baseline and M1 validation

Pipeline-enabled interactive captures under `.utmp/m0-baseline` recorded original Test, initial clone and motor-only clone behavior. Original/clone observations identified table-blocked walk travel and zero-distance dive despite animation root displacement. Original and clone action states agreed; motor-only jump apex/carry agreed and sprint/turn median changes stayed below 0.5%, with camera-offset differences below 0.8 mm. Earlier capture/restoration failures remain historical evidence, not successful certification.

The final detached scene uses `.utmp/m1-final/locomotion-confirm.txt` (JSON schema 3) and its completion sidecar: 1,140 consecutive samples at 60 Hz, restored=true, succeeded=true, no errors. Walk/sprint/turn medians are 1.626/5.524/1.673 m/s, jump apex 1.330 m, dive path 3.275 m, idle/post-dive drift zero. Mouse input changed orbit and actual camera rotation by 58.73 degrees. Seven EditMode and six PlayMode tests passed; final compilation failed=false and current Console errors=0. A prior final-capture sidecar with missing sample JSON is excluded; use only the confirmed repeat. Slopes, steps, ledges, coyote boundaries, rendered contact quality and target player builds remain unverified. See the latest PROGRESS entry for exact commands, artifacts and preservation checks.
