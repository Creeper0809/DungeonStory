[CmdletBinding()]
param(
    [ValidatePattern('^https://[^/]+/?$')]
    [string]$SiteUrl = 'https://creeper0809.synology.me',
    [string]$SshHost = 'dungeonstory-nas',
    [string]$RemoteReleaseRoot = '/volume1/homes/creeper0809/wiki-releases',
    [string]$ReleaseManifestPath,
    [switch]$SkipPackage,
    [switch]$SkipPublicVerification
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-CheckedNative {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$ArgumentList
    )

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE."
    }
}

function Get-FileSha256([string]$Path) {
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$packageScript = Join-Path $PSScriptRoot 'package_live_release.ps1'

if ($SkipPackage -and -not $ReleaseManifestPath) {
    throw '-SkipPackage requires -ReleaseManifestPath.'
}

if (-not $ReleaseManifestPath) {
    $packageOutput = @(& $packageScript -SiteUrl $SiteUrl -RepositoryRoot $repositoryRoot)
    $ReleaseManifestPath = @(
        $packageOutput |
            Where-Object { $_ -is [string] -and $_.EndsWith('release-manifest.json', [StringComparison]::OrdinalIgnoreCase) }
    ) | Select-Object -Last 1
    if (-not $ReleaseManifestPath) {
        throw 'The live release packager did not return a release manifest path.'
    }
}

$manifestPath = [IO.Path]::GetFullPath($ReleaseManifestPath)
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Release manifest does not exist: $manifestPath"
}

$releaseDirectory = Split-Path -Parent $manifestPath
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$releaseId = [string]$manifest.release_id
if ($releaseId -notmatch '^0\.0\.1v-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{12}-[0-9a-f]{12}$') {
    throw "Invalid release id in manifest: $releaseId"
}

$expectedOrigin = ([Uri]$SiteUrl).AbsoluteUri.TrimEnd('/')
if ([string]$manifest.site_origin -ne $expectedOrigin) {
    throw "Manifest origin '$($manifest.site_origin)' does not match '$expectedOrigin'."
}

$files = @(
    'content.zip',
    'renderer-context.zip',
    'release-manifest.json',
    'SHA256SUMS'
)
foreach ($name in $files) {
    $path = Join-Path $releaseDirectory $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Release file is missing: $path"
    }
}

foreach ($payloadName in @('content', 'renderer_context')) {
    $payload = $manifest.payloads.$payloadName
    $payloadPath = Join-Path $releaseDirectory ([string]$payload.archive)
    $actualHash = Get-FileSha256 $payloadPath
    if ($actualHash -ne [string]$payload.archive_sha256) {
        throw "Release hash mismatch: $payloadPath"
    }
}

$remoteIncoming = "$($RemoteReleaseRoot.TrimEnd('/'))/$releaseId"
Invoke-CheckedNative -FilePath 'ssh' -ArgumentList @(
    '-o', 'BatchMode=yes',
    $SshHost,
    "mkdir -p '$remoteIncoming'"
)

$uploadArguments = @('-q', '-o', 'BatchMode=yes')
$uploadArguments += $files | ForEach-Object { Join-Path $releaseDirectory $_ }
$uploadArguments += "$SshHost`:$remoteIncoming/"
Invoke-CheckedNative -FilePath 'scp' -ArgumentList $uploadArguments

Invoke-CheckedNative -FilePath 'ssh' -ArgumentList @(
    '-tt',
    '-o', 'BatchMode=yes',
    $SshHost,
    "sudo -n /usr/local/sbin/dungeonstory-wiki-deploy '$releaseId'"
)

if (-not $SkipPublicVerification) {
    $checks = @(
        @{ Path = '/'; Marker = 'DungeonStory' },
        @{ Path = '/guide/residents-and-work/'; Marker = '스킬 경험과 감소' },
        @{ Path = '/guide/combat-and-equipment/'; Marker = '장비 소재와 품질' },
        @{ Path = '/game-versions/0.0.1v/guide/residents-and-work/'; Marker = '스킬 경험과 감소' }
    )

    foreach ($check in $checks) {
        $uri = "$expectedOrigin$($check.Path)"
        $response = Invoke-WebRequest -Uri $uri -MaximumRedirection 3 -TimeoutSec 20
        if ($response.StatusCode -ne 200 -or -not $response.Content.Contains([string]$check.Marker)) {
            throw "Public verification failed: $uri"
        }
        Write-Output "VERIFIED $($response.StatusCode) $uri"
    }
}

Write-Output "DEPLOYED_RELEASE=$releaseId"
Write-Output "PUBLIC_ORIGIN=$expectedOrigin"
