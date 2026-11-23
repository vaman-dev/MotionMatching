param(
  [ValidateSet('doctor','status','validate','test','codex')]
  [string]$Command = 'doctor'
)
$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
function Invoke-RepositoryPython {
  param([string[]]$PythonArguments)
  # Windows Store aliases may resolve in PATH without being runnable interpreters.
  $PythonCandidates = @(@('python'), @('py', '-3'))
  foreach ($candidate in $PythonCandidates) {
    $executable = Get-Command $candidate[0] -ErrorAction SilentlyContinue
    if (-not $executable) { continue }
    $prefix = @($candidate | Select-Object -Skip 1)
    try {
      $version = & $executable.Source @prefix -B -c 'import sys; print(sys.version_info >= (3, 10))' 2>$null
      if ($LASTEXITCODE -ne 0 -or $version -ne 'True') { continue }
    } catch { continue }
    & $executable.Source @prefix -B @PythonArguments
    if ($LASTEXITCODE -ne 0) { throw 'Repository Python validation failed' }
    return
  }
  throw 'No usable Python 3.10+ found. Python 3.11+ enables TOML parsing; PyYAML enables workflow parsing.'
}
Push-Location $Root
try {
  switch ($Command) {
    'doctor' {
      Write-Host "Repository: $Root"
      foreach ($bin in @('git','python','codex')) {
        $cmd = Get-Command $bin -ErrorAction SilentlyContinue
        if ($cmd) { Write-Host "$bin : $($cmd.Source)" }
        else { Write-Warning "$bin not found" }
      }
      if (Test-Path 'ProjectSettings/ProjectVersion.txt') {
        Get-Content 'ProjectSettings/ProjectVersion.txt'
      } else {
        Write-Host 'This is a scaffold directory. Merge selectively with the Unity project for Editor settings.'
      }
    }
    'status' { & git status --short; & git branch --show-current }
    'validate' { Invoke-RepositoryPython -PythonArguments @('tools/validate_scaffold.py') }
    'test' { Invoke-RepositoryPython -PythonArguments @('-m', 'unittest', 'discover', '-s', 'tools/tests', '-v') }
    'codex' { & codex }
  }
} finally { Pop-Location }
