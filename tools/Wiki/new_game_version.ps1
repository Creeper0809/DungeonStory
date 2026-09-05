[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$From,
    [Parameter(Mandatory = $true)][string]$To,
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

$versionPattern = '^\d+\.\d+\.\d+v$'
if ($From -notmatch $versionPattern -or $To -notmatch $versionPattern) {
    throw 'Game versions must use {major}.{minor}.{patch}v, such as 0.0.2v.'
}

$versionsRoot = Join-Path $RepositoryRoot 'wiki\game-versions'
$source = Join-Path $versionsRoot $From
$target = Join-Path $versionsRoot $To
if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw "Source game-version folder does not exist: $From" }
if (Test-Path -LiteralPath $target) { throw "Target game-version folder already exists: $To" }

$registryPath = Join-Path $versionsRoot 'registry.json'
$registry = Get-Content -LiteralPath $registryPath -Raw -Encoding UTF8 | ConvertFrom-Json
$sourceEntry = @($registry.versions | Where-Object { $_.game_version -eq $From })[0]
if ($null -eq $sourceEntry -or $sourceEntry.status -notin @('planned', 'published')) {
    throw "Only a planned baseline or published game version may be copied: $From"
}

$temporary = Join-Path $versionsRoot ('.' + $To + '.tmp-' + [guid]::NewGuid().ToString('N'))
try {
    Copy-Item -LiteralPath $source -Destination $temporary -Recurse -ErrorAction Stop
    $metadataPath = Join-Path $temporary 'game-version.json'
    $metadata = Get-Content -LiteralPath $metadataPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $metadata.game_version = $To
    $metadata.parent_game_version = $From
    $metadata.status = 'draft'
    $metadata.published_at = $null
    $metadata.content_digest = $null
    $metadata.source_digests = @{}
    $metadata | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $metadataPath -Encoding UTF8 -NoNewline

    $updatePath = Join-Path $temporary 'update.md'
    @"
---
game_version: $To
parent_game_version: $From
status: draft
approved_at: null
published_at: null
---

# $To 업데이트 초안

이 게임 버전 폴더는 $From 전체 snapshot을 복사해 만들었습니다. 변경한 플레이어 영향과 검증 근거를 여기에 기록한 뒤에만 공개합니다.
"@ | Set-Content -LiteralPath $updatePath -Encoding UTF8 -NoNewline

    Move-Item -LiteralPath $temporary -Destination $target -ErrorAction Stop
    $registry | Add-Member -NotePropertyName candidate_game_version -NotePropertyValue $To -Force
    $registry.versions += [pscustomobject]@{ game_version = $To; parent_game_version = $From; status = 'draft'; content_digest = $null }
    $registry | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $registryPath -Encoding UTF8 -NoNewline
} catch {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Recurse -Force }
    throw
}

Write-Output "Created draft game-version folder $To from $From. GAME_VERSION and current_game_version remain $($registry.current_game_version)."
