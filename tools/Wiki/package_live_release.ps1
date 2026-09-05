[CmdletBinding()]
param(
    [string]$GameVersion = (Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\..\docs\wiki\GAME_VERSION') -Raw -Encoding UTF8).Trim(),
    [Parameter(Mandatory = $true)][ValidatePattern('^https://[^/]+/?$')][string]$SiteUrl,
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$ReleaseRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-FileSha256([string]$Path) {
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}

function Get-TreeFiles([string[]]$Roots) {
    return @(
        foreach ($root in $Roots) {
            if (Test-Path -LiteralPath $root -PathType Container) {
                Get-ChildItem -LiteralPath $root -File -Recurse
            } elseif (Test-Path -LiteralPath $root -PathType Leaf) {
                Get-Item -LiteralPath $root
            } else {
                throw "Release source does not exist: $root"
            }
        }
    )
}

function Get-TreeDigest([string[]]$Roots, [string]$BasePath) {
    $entries = foreach ($file in (Get-TreeFiles -Roots $Roots)) {
        $relative = $file.FullName.Substring($BasePath.Length).TrimStart('\', '/') -replace '\\', '/'
        "$relative`t$(Get-FileSha256 $file.FullName)"
    }
    $serialized = [string]::Join("`n", @($entries | Sort-Object))
    $bytes = [Text.Encoding]::UTF8.GetBytes($serialized)
    return ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))).ToLowerInvariant()
}

function Get-TreeStats([string[]]$Roots) {
    $files = @(Get-TreeFiles -Roots $Roots)
    return [ordered]@{
        file_count = $files.Count
        byte_count = [Int64](($files | Measure-Object -Property Length -Sum).Sum)
    }
}

function Assert-ZipEntry([string]$ArchivePath, [string]$ExpectedEntry) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        $normalizedExpected = $ExpectedEntry -replace '\\', '/'
        $found = $archive.Entries | Where-Object {
            ($_.FullName -replace '\\', '/') -eq $normalizedExpected
        } | Select-Object -First 1
        if (-not $found) {
            throw "Archive is missing required entry '$ExpectedEntry': $ArchivePath"
        }
    } finally {
        $archive.Dispose()
    }
}

if ($GameVersion -notmatch '^\d+\.\d+\.\d+v$') {
    throw 'Game version must use {major}.{minor}.{patch}v, such as 0.0.1v.'
}

$repositoryRootPath = [IO.Path]::GetFullPath($RepositoryRoot)
$wikiRoot = Join-Path $repositoryRootPath 'wiki'
$gameVersionsRoot = Join-Path $wikiRoot 'game-versions'
$versionRoot = Join-Path $gameVersionsRoot $GameVersion
$distRoot = Join-Path $wikiRoot 'dist'
$serverEntry = Join-Path $distRoot 'server\entry.mjs'
$modelManifestPath = Join-Path $versionRoot 'data\manifest.json'
$gameVersionPath = Join-Path $versionRoot 'game-version.json'

if (-not (Test-Path -LiteralPath $versionRoot -PathType Container)) {
    throw "Game-version snapshot does not exist: $GameVersion"
}
if (-not $ReleaseRoot) {
    $ReleaseRoot = Join-Path $wikiRoot '.generated\live-releases'
}
$releaseRootPath = [IO.Path]::GetFullPath($ReleaseRoot)
New-Item -ItemType Directory -Path $releaseRootPath -Force | Out-Null

$siteUri = [Uri]$SiteUrl
if ($siteUri.AbsolutePath -ne '/' -or $siteUri.Query -or $siteUri.Fragment) {
    throw 'SiteUrl must be an HTTPS origin without a path, query, or fragment.'
}

$oldSiteUrl = $env:DUNGEONSTORY_WIKI_SITE_URL
try {
    $env:DUNGEONSTORY_WIKI_SITE_URL = $siteUri.AbsoluteUri.TrimEnd('/')

    Push-Location $wikiRoot
    try {
        npm run model
        if ($LASTEXITCODE -ne 0) { throw 'Wiki model generation failed.' }
        npm run audit
        if ($LASTEXITCODE -ne 0) { throw 'Wiki audit failed.' }
        npm exec astro build
        if ($LASTEXITCODE -ne 0) { throw 'Astro server build failed.' }
    } finally {
        Pop-Location
    }
} finally {
    $env:DUNGEONSTORY_WIKI_SITE_URL = $oldSiteUrl
}

if (-not (Test-Path -LiteralPath $serverEntry -PathType Leaf)) {
    throw "Astro standalone server entry is missing: $serverEntry"
}
if (-not (Test-Path -LiteralPath $modelManifestPath -PathType Leaf)) {
    throw "Wiki model manifest is missing: $modelManifestPath"
}

$modelManifest = Get-Content -LiteralPath $modelManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$gameMetadata = Get-Content -LiteralPath $gameVersionPath -Raw -Encoding UTF8 | ConvertFrom-Json
$head = [string](git -C $repositoryRootPath rev-parse HEAD 2>$null)
if ($LASTEXITCODE -ne 0) { $head = $null }

$rendererSources = @(
    (Join-Path $wikiRoot 'src'),
    (Join-Path $wikiRoot 'public'),
    (Join-Path $wikiRoot 'package.json'),
    (Join-Path $wikiRoot 'package-lock.json'),
    (Join-Path $wikiRoot 'astro.config.mjs'),
    (Join-Path $wikiRoot 'tsconfig.json'),
    (Join-Path $wikiRoot 'Dockerfile'),
    (Join-Path $wikiRoot 'docker-compose.live.yml')
)
$deploymentSources = @($rendererSources + $gameVersionsRoot + (Join-Path $repositoryRootPath 'Tools\Wiki'))
$deploymentSourceDigest = Get-TreeDigest -Roots $deploymentSources -BasePath $repositoryRootPath
$contentTreeDigest = Get-TreeDigest -Roots @($gameVersionsRoot) -BasePath $wikiRoot
$rendererTreeDigest = Get-TreeDigest -Roots $rendererSources -BasePath $repositoryRootPath
$artifactPrefix = (Get-FileSha256 $modelManifestPath).Substring(0, 12)
$timestamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$revision = if ($head) { $head.Substring(0, [Math]::Min(12, $head.Length)) } else { 'no-git-head' }
$releaseId = "$GameVersion-$timestamp-$revision-$artifactPrefix"
$releasePath = Join-Path $releaseRootPath $releaseId
$temporary = Join-Path $releaseRootPath ('.' + $releaseId + '.tmp-' + [Guid]::NewGuid().ToString('N'))

$releaseRootPrefix = $releaseRootPath.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
if (-not $temporary.StartsWith($releaseRootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Temporary release path escaped its root: $temporary"
}
if (Test-Path -LiteralPath $releasePath) {
    throw "Release path already exists: $releasePath"
}

New-Item -ItemType Directory -Path $temporary -Force | Out-Null
try {
    $contentStaging = Join-Path $temporary '.content-staging'
    $rendererStaging = Join-Path $temporary '.renderer-staging'
    $contentRoot = Join-Path $contentStaging 'game-versions'
    $rendererWikiRoot = Join-Path $rendererStaging 'wiki'
    New-Item -ItemType Directory -Path $contentStaging, $rendererWikiRoot -Force | Out-Null

    Copy-Item -LiteralPath $gameVersionsRoot -Destination $contentRoot -Recurse -Force
    foreach ($directoryName in @('src', 'public')) {
        Copy-Item -LiteralPath (Join-Path $wikiRoot $directoryName) -Destination (Join-Path $rendererWikiRoot $directoryName) -Recurse -Force
    }
    foreach ($fileName in @('package.json', 'package-lock.json', 'astro.config.mjs', 'tsconfig.json', 'Dockerfile', 'docker-compose.live.yml')) {
        Copy-Item -LiteralPath (Join-Path $wikiRoot $fileName) -Destination (Join-Path $rendererWikiRoot $fileName) -Force
    }
    Copy-Item -LiteralPath $gameVersionsRoot -Destination (Join-Path $rendererWikiRoot 'game-versions') -Recurse -Force

    $contentArchivePath = Join-Path $temporary 'content.zip'
    $rendererArchivePath = Join-Path $temporary 'renderer-context.zip'
    Compress-Archive -Path (Join-Path $contentStaging '*') -DestinationPath $contentArchivePath -CompressionLevel Optimal
    Compress-Archive -Path (Join-Path $rendererStaging '*') -DestinationPath $rendererArchivePath -CompressionLevel Optimal

    Assert-ZipEntry -ArchivePath $contentArchivePath -ExpectedEntry 'game-versions/registry.json'
    Assert-ZipEntry -ArchivePath $rendererArchivePath -ExpectedEntry 'wiki/Dockerfile'
    Assert-ZipEntry -ArchivePath $rendererArchivePath -ExpectedEntry 'wiki/package-lock.json'
    Assert-ZipEntry -ArchivePath $rendererArchivePath -ExpectedEntry "wiki/game-versions/$GameVersion/game-version.json"

    $contentStats = Get-TreeStats -Roots @($gameVersionsRoot)
    $rendererContextRoots = @(
        (Join-Path $rendererWikiRoot 'src'),
        (Join-Path $rendererWikiRoot 'public'),
        (Join-Path $rendererWikiRoot 'game-versions'),
        (Join-Path $rendererWikiRoot 'package.json'),
        (Join-Path $rendererWikiRoot 'package-lock.json'),
        (Join-Path $rendererWikiRoot 'astro.config.mjs'),
        (Join-Path $rendererWikiRoot 'tsconfig.json'),
        (Join-Path $rendererWikiRoot 'Dockerfile'),
        (Join-Path $rendererWikiRoot 'docker-compose.live.yml')
    )
    $rendererContextStats = Get-TreeStats -Roots $rendererContextRoots
    $contentArchiveSha256 = Get-FileSha256 $contentArchivePath
    $rendererArchiveSha256 = Get-FileSha256 $rendererArchivePath

    $manifest = [ordered]@{
        schema_version = 2
        release_id = $releaseId
        created_at_utc = [DateTime]::UtcNow.ToString('o')
        game_version = $GameVersion
        game_version_status = $gameMetadata.status
        site_origin = $siteUri.AbsoluteUri.TrimEnd('/')
        repository_head = $head
        deployment_model = 'astro-node-docker-read-only-content'
        deployment_source_digest = $deploymentSourceDigest
        content_digest = $modelManifest.content_digest
        source_digests = $modelManifest.source_digests
        targets = [ordered]@{
            content_root = '/volume1/wiki-content/game-versions'
            image = 'dungeonstory-wiki:local'
            loopback_endpoint = 'http://127.0.0.1:4321'
        }
        payloads = [ordered]@{
            content = [ordered]@{
                archive = 'content.zip'
                archive_sha256 = $contentArchiveSha256
                tree_sha256 = $contentTreeDigest
                file_count = $contentStats.file_count
                byte_count = $contentStats.byte_count
                archive_root = 'game-versions'
            }
            renderer_context = [ordered]@{
                archive = 'renderer-context.zip'
                archive_sha256 = $rendererArchiveSha256
                source_tree_sha256 = $rendererTreeDigest
                file_count = $rendererContextStats.file_count
                byte_count = $rendererContextStats.byte_count
                archive_root = 'wiki'
                dockerfile = 'wiki/Dockerfile'
                compose_file = 'wiki/docker-compose.live.yml'
            }
        }
        verification = [ordered]@{
            model_generation = 'passed'
            model_validation = 'passed'
            document_authority = 'passed'
            source_coverage = 'passed'
            markdown_tests = 'passed'
            astro_check = 'passed'
            astro_server_build = 'passed'
            archive_structure = 'passed'
        }
        balance_classification = '밸런스 영향 없음'
    }

    $manifestPath = Join-Path $temporary 'release-manifest.json'
    $manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $manifestPath -Encoding UTF8 -NoNewline
    @(
        "$contentArchiveSha256  content.zip"
        "$rendererArchiveSha256  renderer-context.zip"
    ) | Set-Content -LiteralPath (Join-Path $temporary 'SHA256SUMS') -Encoding ASCII

    Remove-Item -LiteralPath $contentStaging, $rendererStaging -Recurse -Force
    Move-Item -LiteralPath $temporary -Destination $releasePath -ErrorAction Stop
} catch {
    if (Test-Path -LiteralPath $temporary) {
        Remove-Item -LiteralPath $temporary -Recurse -Force
    }
    throw
}

$finalManifestPath = Join-Path $releasePath 'release-manifest.json'
$finalManifest = Get-Content -LiteralPath $finalManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($payloadName in @('content', 'renderer_context')) {
    $payload = $finalManifest.payloads.$payloadName
    $payloadPath = Join-Path $releasePath $payload.archive
    if ((Get-FileSha256 $payloadPath) -ne $payload.archive_sha256) {
        throw "Final archive hash mismatch: $($payload.archive)"
    }
}

Write-Output $finalManifestPath
