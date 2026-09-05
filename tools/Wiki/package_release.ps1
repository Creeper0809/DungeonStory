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

function Get-TreeDigest([string[]]$Roots, [string]$BasePath) {
    $entries = foreach ($root in $Roots) {
        Get-ChildItem -LiteralPath $root -File -Recurse | ForEach-Object {
            $relative = $_.FullName.Substring($BasePath.Length).TrimStart('\', '/') -replace '\\', '/'
            "$relative`t$(Get-FileSha256 $_.FullName)"
        }
    }
    $serialized = [string]::Join("`n", @($entries | Sort-Object))
    $bytes = [Text.Encoding]::UTF8.GetBytes($serialized)
    return ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))).ToLowerInvariant()
}

if ($GameVersion -notmatch '^\d+\.\d+\.\d+v$') {
    throw 'Game version must use {major}.{minor}.{patch}v, such as 0.0.1v.'
}

$wikiRoot = Join-Path $RepositoryRoot 'wiki'
$versionRoot = Join-Path $wikiRoot "game-versions\$GameVersion"
$distRoot = Join-Path $wikiRoot 'dist'
if (-not (Test-Path -LiteralPath $versionRoot -PathType Container)) {
    throw "Game-version snapshot does not exist: $GameVersion"
}
if (-not $ReleaseRoot) {
    $ReleaseRoot = Join-Path $wikiRoot '.generated\releases'
}

$siteUri = [Uri]$SiteUrl
if ($siteUri.AbsolutePath -ne '/' -or $siteUri.Query -or $siteUri.Fragment) {
    throw 'SiteUrl must be an HTTPS origin without a path, query, or fragment.'
}

$oldSiteUrl = $env:DUNGEONSTORY_WIKI_SITE_URL
try {
    $env:DUNGEONSTORY_WIKI_SITE_URL = $siteUri.AbsoluteUri.TrimEnd('/')
    python -X utf8 (Join-Path $RepositoryRoot 'Tools\Wiki\generate_wiki_model.py') --repo-root $RepositoryRoot --game-version $GameVersion
    if ($LASTEXITCODE -ne 0) { throw 'Wiki model generation failed.' }
    python -X utf8 (Join-Path $RepositoryRoot 'Tools\Wiki\validate_wiki_model.py') --repo-root $RepositoryRoot --game-version $GameVersion
    if ($LASTEXITCODE -ne 0) { throw 'Wiki model validation failed.' }

    Push-Location $wikiRoot
    try {
        npm run check
        if ($LASTEXITCODE -ne 0) { throw 'Astro type validation failed.' }
        npm exec astro build
        if ($LASTEXITCODE -ne 0) { throw 'Astro static build failed.' }
        npm exec pagefind -- --site dist
        if ($LASTEXITCODE -ne 0) { throw 'Pagefind indexing failed.' }
    } finally {
        Pop-Location
    }
    python -X utf8 (Join-Path $RepositoryRoot 'Tools\Wiki\audit_publication.py') --dist $distRoot --model (Join-Path $versionRoot 'data')
    if ($LASTEXITCODE -ne 0) { throw 'Publication audit failed.' }
} finally {
    $env:DUNGEONSTORY_WIKI_SITE_URL = $oldSiteUrl
}

$modelManifestPath = Join-Path $versionRoot 'data\manifest.json'
$gameVersionPath = Join-Path $versionRoot 'game-version.json'
$modelManifest = Get-Content -LiteralPath $modelManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$gameMetadata = Get-Content -LiteralPath $gameVersionPath -Raw -Encoding UTF8 | ConvertFrom-Json
$head = (git -C $RepositoryRoot rev-parse HEAD 2>$null)
if ($LASTEXITCODE -ne 0) { $head = $null }
$sourceDigestRoots = @(
    (Join-Path $wikiRoot 'src'),
    (Join-Path $wikiRoot 'public'),
    (Join-Path $wikiRoot 'game-versions'),
    (Join-Path $RepositoryRoot 'Tools\Wiki')
)
$sourceDigest = Get-TreeDigest -Roots $sourceDigestRoots -BasePath $RepositoryRoot
$artifactPrefix = (Get-FileSha256 $modelManifestPath).Substring(0, 12)
$timestamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$revision = if ($head) { $head.Substring(0, [Math]::Min(12, $head.Length)) } else { 'no-git-head' }
$releaseId = "$GameVersion-$timestamp-$revision-$artifactPrefix"
$releasePath = Join-Path $ReleaseRoot $releaseId
$temporary = Join-Path $ReleaseRoot ('.' + $releaseId + '.tmp-' + [Guid]::NewGuid().ToString('N'))

if (Test-Path -LiteralPath $releasePath) { throw "Release path already exists: $releasePath" }
New-Item -ItemType Directory -Path $temporary -Force | Out-Null
try {
    $sitePath = Join-Path $temporary 'site'
    Copy-Item -LiteralPath $distRoot -Destination $sitePath -Recurse -Force
    $siteFiles = @(Get-ChildItem -LiteralPath $sitePath -File -Recurse)
    if ($siteFiles.Count -lt 1 -or -not (Test-Path -LiteralPath (Join-Path $sitePath 'index.html'))) {
        throw 'Release payload does not contain a static site root.'
    }

    $archivePath = Join-Path $temporary 'site.zip'
    Compress-Archive -LiteralPath (Get-ChildItem -LiteralPath $sitePath -Force | ForEach-Object FullName) -DestinationPath $archivePath -CompressionLevel Optimal
    $archiveSha256 = Get-FileSha256 $archivePath
    $manifest = [ordered]@{
        schema_version = 1
        release_id = $releaseId
        created_at_utc = [DateTime]::UtcNow.ToString('o')
        game_version = $GameVersion
        game_version_status = $gameMetadata.status
        site_origin = $siteUri.AbsoluteUri.TrimEnd('/')
        repository_head = $head
        deployment_source_digest = $sourceDigest
        content_digest = $modelManifest.content_digest
        source_digests = $modelManifest.source_digests
        payload = [ordered]@{
            directory = 'site'
            file_count = $siteFiles.Count
            byte_count = [Int64](($siteFiles | Measure-Object -Property Length -Sum).Sum)
            archive = 'site.zip'
            archive_sha256 = $archiveSha256
        }
        verification = [ordered]@{
            model_validation = 'passed'
            astro_check = 'passed'
            static_build = 'passed'
            pagefind = 'passed'
            publication_audit = 'passed'
        }
    }
    $manifestPath = Join-Path $temporary 'release-manifest.json'
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding UTF8 -NoNewline
    Get-FileSha256 $archivePath | Set-Content -LiteralPath (Join-Path $temporary 'site.zip.sha256') -Encoding ASCII -NoNewline
    python -X utf8 (Join-Path $RepositoryRoot 'Tools\Wiki\verify_release_bundle.py') --release $temporary
    if ($LASTEXITCODE -ne 0) { throw 'Release bundle verification failed.' }
    Move-Item -LiteralPath $temporary -Destination $releasePath -ErrorAction Stop
} catch {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Recurse -Force }
    throw
}

Write-Output (Join-Path $releasePath 'release-manifest.json')
