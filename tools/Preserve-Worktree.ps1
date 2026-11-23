param([string]$Destination = '.utmp/m0-baseline')
$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $Root
try {
    # Include tracked files and nonignored untracked files, including binary assets.
    # Git objects, ignored caches and credentials excluded by Git are not archived.
    $DestinationPath = [IO.Path]::GetFullPath((Join-Path $Root $Destination))
    if (-not $DestinationPath.StartsWith(($Root + [IO.Path]::DirectorySeparatorChar), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Backup destination must be inside this workspace.'
    }
    & git check-ignore --quiet -- $Destination
    if ($LASTEXITCODE -ne 0) { throw 'Backup destination must already be ignored by Git.' }
    if (Test-Path -LiteralPath $DestinationPath) { throw 'Destination exists; choose a new destination to preserve prior evidence.' }
    $Paths = ((& git -c core.quotepath=false ls-files --cached --others --exclude-standard -z) -join "`n").Split([char]0) | Where-Object { $_ }
    if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate working tree.' }
    $Head = & git rev-parse HEAD
    $Branch = & git branch --show-current
    $Status = @(& git status --porcelain=v1 --untracked-files=all)
    New-Item -ItemType Directory -Path $DestinationPath | Out-Null
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $ArchivePath = Join-Path $DestinationPath 'worktree.zip'
    $Rows = @()
    $Archive = [IO.Compression.ZipFile]::Open($ArchivePath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($RelativePath in $Paths) {
            $Source = Join-Path $Root $RelativePath
            if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) {
                $Rows += [pscustomobject]@{path=$RelativePath; exists=$false; sha256=$null; bytes=0}
                continue
            }
            $Hash = (Get-FileHash -LiteralPath $Source -Algorithm SHA256).Hash.ToLowerInvariant()
            $Length = (Get-Item -LiteralPath $Source).Length
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($Archive, $Source, $RelativePath.Replace('\','/'), [IO.Compression.CompressionLevel]::Fastest) | Out-Null
            $Rows += [pscustomobject]@{path=$RelativePath; exists=$true; sha256=$Hash; bytes=$Length}
        }
    } finally { $Archive.Dispose() }
    # Read every archived entry back and compare against the source hash.
    $Archive = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        foreach ($Row in $Rows) {
            if (-not $Row.exists) { continue }
            $Entry = $Archive.GetEntry($Row.path.Replace('\','/'))
            $Stream = $Entry.Open()
            $Hasher = [Security.Cryptography.SHA256]::Create()
            try { $Actual = [BitConverter]::ToString($Hasher.ComputeHash($Stream)).Replace('-','').ToLowerInvariant() }
            finally { $Hasher.Dispose(); $Stream.Dispose() }
            if ($Actual -ne $Row.sha256) { throw "Archive hash mismatch: $($Row.path)" }
        }
    } finally { $Archive.Dispose() }
    $Manifest = [ordered]@{
        schemaVersion=1; createdUtc=[DateTime]::UtcNow.ToString('o'); head=$Head; branch=$Branch
        archive='worktree.zip'; archiveSha256=(Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash.ToLowerInvariant()
        status=$Status; files=$Rows; verified=$true
        scope='Tracked and nonignored untracked working files, including tracked deletions. Requires existing Git object database for history; ignored local files and Git config/index are not included.'
    }
    $Manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $DestinationPath 'manifest.json') -Encoding UTF8
    $Present = @($Rows | Where-Object { $_.exists })
    Write-Output "Verified backup: $Destination ($($Present.Count) files; $(@($Rows | Where-Object { -not $_.exists }).Count) recorded deletions)"
} finally { Pop-Location }
