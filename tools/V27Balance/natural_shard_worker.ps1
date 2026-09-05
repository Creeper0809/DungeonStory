param(
    [Parameter(Mandatory = $true)][string]$WorkerProject,
    [Parameter(Mandatory = $true)][string]$PortfolioCsv,
    [Parameter(Mandatory = $true)][string]$CentralStateDirectory,
    [Parameter(Mandatory = $true)][string]$ExpectedSourceDigest,
    [Parameter(Mandatory = $true)][string]$ExpectedPortfolioDigest,
    [Parameter(Mandatory = $true)][string]$ExpectedMeasurementPortfolioDigest,
    [Parameter(Mandatory = $true)][string]$ExpectedAuthorityTreeDigest,
    [Parameter(Mandatory = $true)]
    [ValidateSet('bootstrap', 'strict')]
    [string]$ProfileMode,
    [Parameter(Mandatory = $true)][string]$ExpectedProfileAuthorityDigest,
    [Parameter(Mandatory = $true)][int]$PartitionIndex,
    [Parameter(Mandatory = $true)][int]$PartitionCount,
    [int]$ExpectedSeedCount = 32,
    [int]$ExpectedFirstSeed = 157181,
    [int]$PartitionTimeoutMinutes = 240,
    [switch]$SeedAssignedStatesFromCentral,
    [string]$UnityExecutable =
        'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$officialSceneSha256 =
    '6c35a17693d3cedca2c85b89b22a8bff9b5bae6de88c01b255481c058d2aee40'
$officialSceneMetaSha256 =
    '0c7f1e7dac6804c5e9bad630433c81b9bb944b7532a9e63d5d938f9f3b56849e'
$officialBuildSettingsSha256 =
    '8ee44400b9ae37cbd1092f671fe364f277601cc02357b02e3908d1d4e9a95ad4'
$executeMethod =
    'PhysicalItemLogisticsPlayModeVerifier.RequestNaturalOutputPortfolioRunFromMenu'
if ($PartitionCount -notin @(3, 6, 12, 24, 48) -or $PartitionIndex -lt 0 -or
    $PartitionIndex -ge $PartitionCount -or $ExpectedSeedCount -ne 32 -or
    $ExpectedFirstSeed -lt 0 -or $PartitionTimeoutMinutes -lt 1) {
    throw 'The V27 natural run requires 3, 6, 12, 24, or 48 partitions and 32 seeds.'
}
$resolvedWorker = [System.IO.Path]::GetFullPath($WorkerProject).TrimEnd('\')
$resolvedCentral = [System.IO.Path]::GetFullPath($CentralStateDirectory)
$resolvedPortfolio = [System.IO.Path]::GetFullPath($PortfolioCsv)
$resolvedUnity = [System.IO.Path]::GetFullPath($UnityExecutable)
if (-not (Test-Path -LiteralPath $resolvedWorker -PathType Container) -or
    -not (Test-Path -LiteralPath $resolvedPortfolio -PathType Leaf) -or
    -not (Test-Path -LiteralPath $resolvedUnity -PathType Leaf) -or
    $ExpectedSourceDigest -notmatch '^[0-9a-f]{64}$' -or
    $ExpectedPortfolioDigest -notmatch '^[0-9a-f]{64}$' -or
    $ExpectedMeasurementPortfolioDigest -notmatch '^[0-9a-f]{64}$' -or
    $ExpectedAuthorityTreeDigest -notmatch '^[0-9a-f]{64}$' -or
    $ExpectedProfileAuthorityDigest -notmatch '^[0-9a-f]{64}$') {
    throw 'Natural shard worker inputs are invalid.'
}
function Get-AuthorityTreeDigest([string]$ProjectRoot) {
    $authorityRoot = Join-Path $ProjectRoot 'Assets\Resources'
    if (-not (Test-Path -LiteralPath $authorityRoot -PathType Container)) {
        throw "Worker authority root is missing: $authorityRoot"
    }

    [string[]]$relativePaths = @(Get-ChildItem -LiteralPath $authorityRoot `
        -File -Recurse | ForEach-Object {
            [System.IO.Path]::GetRelativePath(
                $authorityRoot,
                $_.FullName).Replace('\', '/')
        })
    [System.Array]::Sort(
        $relativePaths,
        [System.StringComparer]::Ordinal)

    $canonical = [System.Text.StringBuilder]::new()
    foreach ($relativePath in $relativePaths) {
        $absolutePath = Join-Path $authorityRoot `
            $relativePath.Replace('/', '\')
        $fileHash = (Get-FileHash -LiteralPath $absolutePath `
            -Algorithm SHA256).Hash.ToLowerInvariant()
        [void]$canonical.Append($relativePath)
        [void]$canonical.Append('=')
        [void]$canonical.Append($fileHash)
        [void]$canonical.Append("`n")
    }

    $encoding = [System.Text.UTF8Encoding]::new($false, $true)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha.ComputeHash($encoding.GetBytes($canonical.ToString()))
        return ([System.BitConverter]::ToString($hash)).Replace(
            '-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}
$actualAuthorityTreeDigest = Get-AuthorityTreeDigest $resolvedWorker
if (-not [string]::Equals(
        $actualAuthorityTreeDigest,
        $ExpectedAuthorityTreeDigest,
        [System.StringComparison]::Ordinal)) {
    throw (
        'Natural shard worker authority tree differs before launch: expected=' +
        $ExpectedAuthorityTreeDigest + ';actual=' +
        $actualAuthorityTreeDigest + '.')
}
$localStateDirectory = Join-Path $resolvedWorker `
    'Temp\v27-output-clearance-natural-shards'
$localSourceDirectory = Join-Path $localStateDirectory $ExpectedSourceDigest
$centralSourceDirectory = Join-Path `
    (Join-Path (Join-Path $resolvedCentral $ExpectedSourceDigest) $ProfileMode) `
    $ExpectedProfileAuthorityDigest
$logDirectory = Join-Path $resolvedWorker 'Temp\v27-natural-partition-logs'
$quarantineDirectory = Join-Path $resolvedWorker 'Temp\v27-quarantine'
$centralQuarantineDirectory = Join-Path $resolvedCentral '_quarantine'
$donePath = Join-Path $logDirectory `
    ('partition-' + $PartitionIndex + '-of-' + $PartitionCount + '.done')
$requestPath = Join-Path $resolvedWorker `
    'Temp\v27-production-output-clearance-natural-portfolio.request'
$failureReportPath = Join-Path $resolvedWorker `
    'Artifacts\QA\v27-production-output-clearance-natural-portfolio-runner.txt'
New-Item -ItemType Directory -Force -Path $localStateDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $localSourceDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $centralSourceDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $quarantineDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $centralQuarantineDirectory | Out-Null
function Move-ToQuarantine(
    [string]$Path,
    [string]$Reason,
    [string]$QuarantineRoot = $quarantineDirectory) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return
    }
    $safeReason = $Reason -replace '[^A-Za-z0-9_.-]', '-'
    $name = [System.IO.Path]::GetFileName($Path) + '.' + $safeReason + '.' +
        [DateTime]::UtcNow.Ticks + '.' + [Guid]::NewGuid().ToString('N') +
        '.quarantined'
    New-Item -ItemType Directory -Force -Path $QuarantineRoot | Out-Null
    Move-Item -LiteralPath $Path -Destination (Join-Path $QuarantineRoot $name)
}
function Recover-MarkerOnlySyntheticLease {
    $marker = Join-Path $resolvedWorker `
        'Temp\v27-synthetic-gameplay-scene-lease.json'
    if (-not (Test-Path -LiteralPath $marker -PathType Leaf)) {
        return
    }

    $officialScene = Join-Path $resolvedWorker `
        'Assets\Scenes\GameplayScene.unity'
    $officialMeta = $officialScene + '.meta'
    $buildSettings = Join-Path $resolvedWorker `
        'ProjectSettings\EditorBuildSettings.asset'
    $temporaryDirectory = Join-Path $resolvedWorker `
        'Assets\__V27SyntheticPreparedOutputCanary'
    $temporaryPaths = @(
        $temporaryDirectory,
        $temporaryDirectory + '.meta',
        (Join-Path $temporaryDirectory 'GameplayScene.unity'),
        (Join-Path $temporaryDirectory 'GameplayScene.unity.meta'))

    if ($temporaryPaths | Where-Object { Test-Path -LiteralPath $_ }) {
        throw 'Synthetic lease marker still owns a temporary asset; refusing pre-clean.'
    }
    if (Test-Path -LiteralPath $requestPath) {
        throw 'Synthetic lease marker still has a live natural request; refusing pre-clean.'
    }
    $sceneHash = (Get-FileHash -LiteralPath $officialScene `
        -Algorithm SHA256).Hash.ToLowerInvariant()
    $metaHash = (Get-FileHash -LiteralPath $officialMeta `
        -Algorithm SHA256).Hash.ToLowerInvariant()
    $buildHash = (Get-FileHash -LiteralPath $buildSettings `
        -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($sceneHash -ne $officialSceneSha256 -or
        $metaHash -ne $officialSceneMetaSha256 -or
        $buildHash -ne $officialBuildSettingsSha256) {
        throw 'Synthetic lease marker pre-clean authority differs from the official scene/build settings.'
    }

    $manifest = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
    $expectedScenes = @(
        'Assets/Scenes/TitleScene.unity',
        'Assets/Scenes/StartPreparationScene.unity',
        'Assets/Scenes/GameplayScene.unity',
        'Assets/Scenes/SampleScene.unity')
    $manifestScenes = @($manifest.originalBuildScenes)
    if ($manifest.schemaVersion -ne 1 -or
        $manifest.replacedBuildSceneIndex -ne 2 -or
        $manifestScenes.Count -ne $expectedScenes.Count) {
        throw 'Synthetic lease marker-only orphan has a noncanonical manifest.'
    }
    for ($index = 0; $index -lt $expectedScenes.Count; $index++) {
        if ([string]$manifestScenes[$index].path -ne $expectedScenes[$index] -or
            -not [bool]$manifestScenes[$index].enabled) {
            throw 'Synthetic lease marker-only orphan build-scene ownership differs.'
        }
    }
    Move-ToQuarantine $marker 'marker-only-orphan'
}
Recover-MarkerOnlySyntheticLease
# A prior successful partition marker is not evidence for the process that is
# starting now. Preserve it for diagnosis, but remove it from the live status
# location before any current-source work begins.
Move-ToQuarantine $donePath 'stale-partition-completion-marker'
# Preserve the old flat @1 layout instead of letting it collide with the
# current-source/run-identity namespace.
foreach ($legacy in Get-ChildItem -LiteralPath $localStateDirectory `
             -Filter '*.state' -File -ErrorAction SilentlyContinue) {
    Move-ToQuarantine $legacy.FullName 'legacy-flat-local-state'
}
foreach ($legacy in Get-ChildItem -LiteralPath $resolvedCentral `
             -Filter '*.state' -File -ErrorAction SilentlyContinue) {
    Move-ToQuarantine $legacy.FullName 'legacy-flat-central-state' `
        $centralQuarantineDirectory
}
$keySet = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::Ordinal)
$seedIndexesByKey = @{}
$portfolioDigests = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::Ordinal)
$portfolioRowCount = 0
foreach ($row in Import-Csv -LiteralPath $resolvedPortfolio) {
    $portfolioRowCount++
    $rowPortfolioDigest = [string]$row.portfolioSourceDigest
    if ($rowPortfolioDigest -notmatch '^[0-9a-f]{64}$') {
        throw 'Natural shard portfolio has a noncanonical portfolioSourceDigest.'
    }
    [void]$portfolioDigests.Add($rowPortfolioDigest)
    $key = [string]$row.definitionId + '|' + [string]$row.workstationTag
    if ($keySet.Add($key)) {
        $seedIndexesByKey[$key] =
            [System.Collections.Generic.HashSet[int]]::new()
    }
    $seedIndex = 0
    if (-not [int]::TryParse(
            [string]$row.seedIndex,
            [System.Globalization.NumberStyles]::Integer,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [ref]$seedIndex) -or
        $seedIndex -lt 0 -or $seedIndex -ge $ExpectedSeedCount) {
        throw "Natural shard portfolio has an invalid seed index for '$key'."
    }
    $deterministicSeed = 0
    if (-not [int]::TryParse(
            [string]$row.deterministicSeed,
            [System.Globalization.NumberStyles]::Integer,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [ref]$deterministicSeed) -or
        $deterministicSeed -ne ($ExpectedFirstSeed + $seedIndex)) {
        throw "Natural shard portfolio has a foreign deterministic seed for '$key/$seedIndex'."
    }
    if (-not $seedIndexesByKey[$key].Add($seedIndex)) {
        throw "Natural shard portfolio contains duplicate seed index '$seedIndex' for '$key'."
    }
}
$keys = [string[]]$keySet
[System.Array]::Sort($keys, [System.StringComparer]::Ordinal)
if ($keys.Count -ne 92) {
    throw "Natural shard denominator must be exactly 92: $($keys.Count)."
}
foreach ($key in $keys) {
    if ($seedIndexesByKey[$key].Count -ne $ExpectedSeedCount) {
        throw "Natural shard portfolio seed denominator is incomplete for '$key': $($seedIndexesByKey[$key].Count)."
    }
}
[int]$expectedPortfolioRows = 92 * $ExpectedSeedCount
if ($portfolioRowCount -ne $expectedPortfolioRows) {
    throw "Natural shard portfolio row denominator must be exactly ${expectedPortfolioRows}: $portfolioRowCount."
}
if ($portfolioDigests.Count -ne 1 -or
    -not $portfolioDigests.Contains($ExpectedMeasurementPortfolioDigest)) {
    throw (
        'Natural shard measurement portfolio digest differs before launch: expected=' +
        $ExpectedMeasurementPortfolioDigest + ';actual=' +
        [string]::Join(',', [string[]]$portfolioDigests) + '.')
}
$allShardIds = [System.Collections.Generic.List[string]]::new()
foreach ($key in $keys) {
    $parts = $key.Split([char]'|')
    if ($parts.Count -ne 2 -or [string]::IsNullOrWhiteSpace($parts[0]) -or
        [string]::IsNullOrWhiteSpace($parts[1])) {
        throw "Natural shard portfolio key is malformed: $key."
    }
    $allShardIds.Add(
        'natural-output-clearance-shard:' + $parts[0] + ':' + $parts[1])
}
$allShardIds.Sort([System.StringComparer]::Ordinal)
$canonicalKeySet = [string]::Join("`n", $allShardIds) + "`n"
$keySetBytes = [System.Text.UTF8Encoding]::new($false, $true).GetBytes(
    $canonicalKeySet)
$keySetSha = [System.Security.Cryptography.SHA256]::Create()
try {
    $keySetHashText = [System.BitConverter]::ToString(
        $keySetSha.ComputeHash($keySetBytes))
    $expectedKeySetDigest = $keySetHashText.Replace('-', '').ToLowerInvariant()
}
finally {
    $keySetSha.Dispose()
}
$assigned = [System.Collections.Generic.List[string]]::new()
for ($index = 0; $index -lt $allShardIds.Count; $index++) {
    if (($index % $PartitionCount) -ne $PartitionIndex) {
        continue
    }
    $assigned.Add($allShardIds[$index])
}
$expectedPartitionSize = if ($PartitionIndex -ge $allShardIds.Count) {
    0
} else {
    1 + [int][Math]::Floor(
        ($allShardIds.Count - 1 - $PartitionIndex) / [double]$PartitionCount)
}
if ($expectedPartitionSize -le 0 -or
    $assigned.Count -ne $expectedPartitionSize) {
    throw "Natural shard partition size drifted: $($assigned.Count)."
}
function Move-StaleRequest {
    if (-not (Test-Path -LiteralPath $requestPath -PathType Leaf)) {
        return
    }
    $name = 'natural-request.partition-' + $PartitionIndex + '.' +
        [DateTime]::UtcNow.Ticks + '.stale'
    Move-Item -LiteralPath $requestPath -Destination `
        (Join-Path $quarantineDirectory $name)
}
function Read-StateLines([string]$Path) {
    $encoding = [System.Text.UTF8Encoding]::new($false, $true)
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        try {
            $share = [System.IO.FileShare]::ReadWrite -bor `
                [System.IO.FileShare]::Delete
            $stream = [System.IO.FileStream]::new(
                $Path,
                [System.IO.FileMode]::Open,
                [System.IO.FileAccess]::Read,
                $share)
            try {
                $reader = [System.IO.StreamReader]::new(
                    $stream,
                    $encoding,
                    $true)
                try {
                    $lines = [System.Collections.Generic.List[string]]::new()
                    while (-not $reader.EndOfStream) {
                        $lines.Add($reader.ReadLine())
                    }
                    return $lines.ToArray()
                }
                finally {
                    $reader.Dispose()
                }
            }
            finally {
                $stream.Dispose()
            }
        }
        catch [System.IO.IOException] {
            if ($attempt -eq 49) {
                throw
            }
            Start-Sleep -Milliseconds 100
        }
    }
    throw "Natural shard state read retry exhausted: $Path"
}
function Get-Sha256Hex([string]$Path) {
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        try {
            $share = [System.IO.FileShare]::ReadWrite -bor `
                [System.IO.FileShare]::Delete
            $stream = [System.IO.FileStream]::new(
                $Path,
                [System.IO.FileMode]::Open,
                [System.IO.FileAccess]::Read,
                $share)
            try {
                $sha = [System.Security.Cryptography.SHA256]::Create()
                try {
                    return ([System.BitConverter]::ToString(
                        $sha.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
                }
                finally {
                    $sha.Dispose()
                }
            }
            finally {
                $stream.Dispose()
            }
        }
        catch [System.IO.IOException] {
            if ($attempt -eq 49) {
                throw
            }
            Start-Sleep -Milliseconds 100
        }
    }
    throw "Natural shard hash retry exhausted: $Path"
}

function Get-TextSha256([string]$Text) {
    $encoding = [System.Text.UTF8Encoding]::new($false, $true)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hex = [System.BitConverter]::ToString(
            $sha.ComputeHash($encoding.GetBytes($Text)))
        return $hex.Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Get-CanonicalSemanticDigest([object[]]$Tokens) {
    $encoding = [System.Text.UTF8Encoding]::new($false, $true)
    $canonical = [System.Text.StringBuilder]::new()
    foreach ($token in $Tokens) {
        $value = if ($null -eq $token) {
            ''
        } else {
            [System.Convert]::ToString(
                $token,
                [System.Globalization.CultureInfo]::InvariantCulture)
        }
        [void]$canonical.Append($encoding.GetByteCount($value).ToString(
                [System.Globalization.CultureInfo]::InvariantCulture))
        [void]$canonical.Append(':')
        [void]$canonical.Append($value)
        [void]$canonical.Append('|')
    }
    return Get-TextSha256 $canonical.ToString()
}

function Test-CanonicalStateToken(
    [string]$Value,
    [switch]$AllowEmptySentinel) {
    if ($Value -eq '~') {
        return [bool]$AllowEmptySentinel
    }
    return -not [string]::IsNullOrWhiteSpace($Value) -and
        [string]::Equals(
            $Value, $Value.Trim(), [System.StringComparison]::Ordinal) -and
        $Value.IndexOfAny([char[]]@('|', "`r", "`n", '=')) -lt 0
}
function Read-ValidatedState(
    [System.IO.FileInfo]$State,
    [string]$ShardId,
    [switch]$RequireComplete) {
    $lines = @(Read-StateLines $State.FullName)
    $headers = [System.Collections.Generic.Dictionary[string,string]]::new(
        [System.StringComparer]::Ordinal)
    $records = [System.Collections.Generic.List[string[]]]::new()
    $slices = [System.Collections.Generic.List[string[]]]::new()
    $routeBatches = [System.Collections.Generic.List[string[]]]::new()
    foreach ($line in $lines) {
        if ($line.StartsWith('R|', [System.StringComparison]::Ordinal)) {
            $fields = $line.Split([char]'|')
            if ($fields.Count -ne 38) {
                throw "Natural shard record width drifted: $ShardId."
            }
            $records.Add($fields)
            continue
        }
        if ($line.StartsWith('S|', [System.StringComparison]::Ordinal)) {
            $fields = $line.Split([char]'|')
            if ($fields.Count -ne 10) {
                throw "Natural shard slice width drifted: $ShardId."
            }
            $slices.Add($fields)
            continue
        }
        if ($line.StartsWith('B|', [System.StringComparison]::Ordinal)) {
            $fields = $line.Split([char]'|')
            if ($fields.Count -ne 4) {
                throw "Natural shard route-batch width drifted: $ShardId."
            }
            $routeBatches.Add($fields)
            continue
        }
        $separator = $line.IndexOf('=')
        if ($separator -le 0) {
            throw "Natural shard header is malformed: $ShardId."
        }
        $headerName = $line.Substring(0, $separator)
        if ($headers.ContainsKey($headerName)) {
            throw "Natural shard header is malformed: $ShardId."
        }
        $headers.Add($headerName, $line.Substring($separator + 1))
    }
    $required = @(
        'schema','identity','currentSource','scene','portfolio','descriptors',
        'measurements','shardCount','shardKeySet','shardId','shard','handlers',
        'executors','clearanceProfileMode','clearanceProfileAuthority')
    if ($headers.Count -ne $required.Count -or
        @($required | Where-Object { -not $headers.ContainsKey($_) }).Count -ne 0 -or
        $headers['schema'] -ne 'production-output-clearance-natural-shard-store@4' -or
        $headers['currentSource'] -ne $ExpectedSourceDigest -or
        $headers['scene'] -ne $officialSceneSha256 -or
        $headers['portfolio'] -ne $ExpectedPortfolioDigest -or
        $headers['measurements'] -ne $ExpectedMeasurementPortfolioDigest -or
        $headers['clearanceProfileMode'] -ne $ProfileMode -or
        $headers['clearanceProfileAuthority'] -ne
            $ExpectedProfileAuthorityDigest -or
        $headers['shardCount'] -ne $allShardIds.Count.ToString(
            [System.Globalization.CultureInfo]::InvariantCulture) -or
        $headers['shardKeySet'] -ne $expectedKeySetDigest -or
        $headers['shardId'] -ne $ShardId) {
        throw "Natural shard identity is foreign or incomplete: $ShardId."
    }
    foreach ($name in @(
            'identity','currentSource','scene','portfolio','descriptors',
            'measurements','shardKeySet','shard','handlers','executors',
            'clearanceProfileAuthority')) {
        if ($headers[$name] -notmatch '^[0-9a-f]{64}$') {
            throw "Natural shard digest is noncanonical: $ShardId/$name."
        }
    }
    if ($RequireComplete -and $records.Count -ne $ExpectedSeedCount) {
        throw "Natural shard record denominator is incomplete: $ShardId."
    }
    if ($records.Count -gt $ExpectedSeedCount) {
        throw "Natural shard record denominator overflowed: $ShardId."
    }
    $recordIds = @($records | ForEach-Object { $_[1] })
    $recordSeedIndices = @($records | ForEach-Object { [int]$_[3] })
    $recordCommitKeys = @($records | ForEach-Object { $_[5] + "`u{1f}" + $_[8] })
    if (@($recordIds | Sort-Object -Unique).Count -ne $records.Count -or
        @($recordSeedIndices | Sort-Object -Unique).Count -ne $records.Count -or
        @($recordCommitKeys | Sort-Object -Unique).Count -ne $records.Count) {
        throw "Natural shard records are not bijective: $ShardId."
    }
    if ($RequireComplete) {
        $expectedIndices = 0..($ExpectedSeedCount - 1)
        if ((Compare-Object $expectedIndices ($recordSeedIndices | Sort-Object)).Count) {
            throw "Natural shard seed indices are incomplete: $ShardId."
        }
        foreach ($record in $records) {
            $seedIndex = 0
            $deterministicSeed = 0
            if (-not [int]::TryParse(
                    $record[3],
                    [System.Globalization.NumberStyles]::Integer,
                    [System.Globalization.CultureInfo]::InvariantCulture,
                    [ref]$seedIndex) -or
                -not [int]::TryParse(
                    $record[4],
                    [System.Globalization.NumberStyles]::Integer,
                    [System.Globalization.CultureInfo]::InvariantCulture,
                    [ref]$deterministicSeed) -or
                $deterministicSeed -ne ($ExpectedFirstSeed + $seedIndex)) {
                throw "Natural shard record has a foreign deterministic seed: $ShardId."
            }
        }
        $sliceIds = @($slices | ForEach-Object { $_[1] })
        if (@($sliceIds | Where-Object { $_ -notin $recordIds }).Count -ne 0 -or
            @($recordIds | Where-Object { $_ -notin $sliceIds }).Count -ne 0) {
            throw "Natural shard slices are orphaned or missing: $ShardId."
        }
        $sliceMassByObservation = @{}
        $sliceDigestsByObservation = @{}
        foreach ($slice in $slices) {
            $quantity = 0
            $massGrams = [long]0
            if (-not (Test-CanonicalStateToken $slice[2]) -or
                -not (Test-CanonicalStateToken $slice[3]) -or
                -not (Test-CanonicalStateToken $slice[4] -AllowEmptySentinel) -or
                -not (Test-CanonicalStateToken $slice[5]) -or
                -not [int]::TryParse(
                    $slice[6],
                    [System.Globalization.NumberStyles]::Integer,
                    [System.Globalization.CultureInfo]::InvariantCulture,
                    [ref]$quantity) -or
                $quantity -le 0 -or
                $slice[6] -ne $quantity.ToString(
                    [System.Globalization.CultureInfo]::InvariantCulture) -or
                -not [long]::TryParse(
                    $slice[7],
                    [System.Globalization.NumberStyles]::Integer,
                    [System.Globalization.CultureInfo]::InvariantCulture,
                    [ref]$massGrams) -or
                $massGrams -le 0 -or
                $slice[7] -ne $massGrams.ToString(
                    [System.Globalization.CultureInfo]::InvariantCulture) -or
                $slice[8] -notmatch '^[0-9a-f]{64}$' -or
                $slice[9] -notmatch '^[0-9a-f]{64}$') {
                throw "Natural shard output slice is noncanonical: $ShardId/$($slice[1])."
            }
            $itemInstanceId = if ($slice[4] -eq '~') { '' } else { $slice[4] }
            $computedSliceDigest = Get-CanonicalSemanticDigest @(
                'production-output-clearance-execution-output-slice@1',
                $slice[2],
                $slice[3],
                $itemInstanceId,
                $slice[5],
                $quantity,
                $massGrams,
                $slice[8])
            if (-not [string]::Equals(
                    $computedSliceDigest, $slice[9],
                    [System.StringComparison]::Ordinal)) {
                throw "Natural shard output slice source digest drifted: $ShardId/$($slice[1])."
            }
            if (-not $sliceDigestsByObservation.ContainsKey($slice[1])) {
                $sliceDigestsByObservation[$slice[1]] =
                    [System.Collections.Generic.HashSet[string]]::new(
                        [System.StringComparer]::Ordinal)
            }
            if (-not $sliceDigestsByObservation[$slice[1]].Add($slice[9])) {
                throw "Natural shard output slice is duplicated: $ShardId/$($slice[1])."
            }
            if (-not $sliceMassByObservation.ContainsKey($slice[1])) {
                $sliceMassByObservation[$slice[1]] = [long]0
            }
            if ($sliceMassByObservation[$slice[1]] -gt
                ([long]::MaxValue - $massGrams)) {
                throw "Natural shard output slice mass overflowed: $ShardId/$($slice[1])."
            }
            $sliceMassByObservation[$slice[1]] =
                [long]$sliceMassByObservation[$slice[1]] + $massGrams
        }
        foreach ($record in $records) {
            $expectedMassGrams = [long]0
            if (-not [long]::TryParse(
                    $record[10],
                    [System.Globalization.NumberStyles]::Integer,
                    [System.Globalization.CultureInfo]::InvariantCulture,
                    [ref]$expectedMassGrams) -or
                $expectedMassGrams -le 0 -or
                -not $sliceMassByObservation.ContainsKey($record[1]) -or
                [long]$sliceMassByObservation[$record[1]] -ne
                    $expectedMassGrams) {
                throw "Natural shard output slice mass does not close: $ShardId/$($record[1])."
            }
        }
        $routeRowsByObservation = @{}
        foreach ($routeBatch in $routeBatches) {
            $observationId = $routeBatch[1]
            if ($observationId -notin $recordIds) {
                throw "Natural shard route batch is orphaned: $ShardId/$observationId."
            }
            $ordinal = 0
            if (-not [int]::TryParse(
                    $routeBatch[2],
                    [System.Globalization.NumberStyles]::Integer,
                    [System.Globalization.CultureInfo]::InvariantCulture,
                    [ref]$ordinal) -or
                $ordinal -lt 0 -or
                [string]::IsNullOrWhiteSpace($routeBatch[3]) -or
                $routeBatch[3] -eq '~' -or
                @($routeBatch[3].ToCharArray() | Where-Object {
                        [char]::IsWhiteSpace($_)
                    }).Count -ne 0 -or
                $routeBatch[3] -ne $routeBatch[3].Trim()) {
                throw "Natural shard route batch is noncanonical: $ShardId/$observationId."
            }
            if (-not $routeRowsByObservation.ContainsKey($observationId)) {
                $routeRowsByObservation[$observationId] =
                    [System.Collections.Generic.List[object]]::new()
            }
            $routeRowsByObservation[$observationId].Add([pscustomobject]@{
                    Ordinal = $ordinal
                    RouteBatchCommitId = $routeBatch[3]
                })
        }
        foreach ($record in $records) {
            $observationId = $record[1]
            if (-not $routeRowsByObservation.ContainsKey($observationId)) {
                throw "Natural shard record has no route batch: $ShardId/$observationId."
            }
            $rows = @($routeRowsByObservation[$observationId] | Sort-Object Ordinal)
            $routeIds = [System.Collections.Generic.HashSet[string]]::new(
                [System.StringComparer]::Ordinal)
            foreach ($row in $rows) {
                [void]$routeIds.Add($row.RouteBatchCommitId)
            }
            $completedCount = 0
            if (-not [int]::TryParse(
                    $record[19],
                    [System.Globalization.NumberStyles]::Integer,
                    [System.Globalization.CultureInfo]::InvariantCulture,
                    [ref]$completedCount) -or
                $completedCount -le 0 -or
                $rows.Count -ne $completedCount -or
                $routeIds.Count -ne $rows.Count) {
                throw "Natural shard route-batch denominator drifted: $ShardId/$observationId."
            }
            for ($index = 0; $index -lt $rows.Count; $index++) {
                if ($rows[$index].Ordinal -ne $index) {
                    throw "Natural shard route-batch ordinals are incomplete: $ShardId/$observationId."
                }
            }
        }
    }
    return [pscustomobject]@{
        File = $State
        Headers = $headers
        RecordCount = $records.Count
        ShardId = $ShardId
    }
}
function Build-CurrentStateIndex {
    $index = @{}
    foreach ($file in Get-ChildItem -LiteralPath $localSourceDirectory `
                 -Filter '*.state' -File -Recurse -ErrorAction SilentlyContinue) {
        $head = @(Read-StateLines $file.FullName | Select-Object -First 15)
        if ($head -notcontains ('currentSource=' + $ExpectedSourceDigest) -or
            $head -notcontains ('clearanceProfileMode=' + $ProfileMode) -or
            $head -notcontains ('clearanceProfileAuthority=' +
                $ExpectedProfileAuthorityDigest)) {
            continue
        }
        $shardHeaders = @($head | Where-Object {
            $_.StartsWith('shardId=', [System.StringComparison]::Ordinal)
        })
        if ($shardHeaders.Count -ne 1) {
            throw "Natural shard state has a missing or duplicate shardId header: $($file.FullName)"
        }
        $shardId = $shardHeaders[0].Substring('shardId='.Length)
        if ($shardId -notin $allShardIds) {
            throw "Natural shard state names an unknown current shard: $shardId."
        }
        if (-not $index.ContainsKey($shardId)) {
            $index[$shardId] = [System.Collections.Generic.List[
                System.IO.FileInfo]]::new()
        }
        $index[$shardId].Add($file)
    }
    return ,$index
}
function Find-CurrentState(
    [string]$ShardId,
    [switch]$RequireComplete,
    [hashtable]$Index = $null) {
    $matches = [System.Collections.Generic.List[object]]::new()
    $files = if ($null -eq $Index) {
        @(Get-ChildItem -LiteralPath $localSourceDirectory `
            -Filter '*.state' -File -Recurse -ErrorAction SilentlyContinue)
    } elseif ($Index.ContainsKey($ShardId)) {
        @($Index[$ShardId])
    } else {
        @()
    }
    foreach ($file in $files) {
        if ($null -eq $Index) {
            $head = @(Read-StateLines $file.FullName | Select-Object -First 15)
            if ($head -notcontains ('currentSource=' + $ExpectedSourceDigest) -or
                $head -notcontains ('clearanceProfileMode=' + $ProfileMode) -or
                $head -notcontains ('clearanceProfileAuthority=' +
                    $ExpectedProfileAuthorityDigest) -or
                $head -notcontains ('shardId=' + $ShardId)) {
                continue
            }
        }
        $matches.Add((Read-ValidatedState $file $ShardId `
            -RequireComplete:$RequireComplete))
    }
    if ($matches.Count -gt 1) {
        throw "Multiple current states exist for $ShardId."
    }
    if ($matches.Count -eq 1) {
        return $matches[0]
    }
    return $null
}
function Publish-State([object]$Validated, [string]$ShardId) {
    if ($null -eq $Validated -or $Validated.RecordCount -ne $ExpectedSeedCount) {
        throw "Refusing to publish incomplete state for $ShardId."
    }
    $state = $Validated.File
    $stateFullPath = [System.IO.Path]::GetFullPath($state.FullName)
    $sourcePrefix = [System.IO.Path]::GetFullPath(
        $localSourceDirectory).TrimEnd('\') + '\'
    if (-not $stateFullPath.StartsWith(
            $sourcePrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Natural shard state escaped its source namespace: $ShardId."
    }
    $relative = $stateFullPath.Substring($sourcePrefix.Length).Replace('\', '/')
    $destination = Join-Path $centralSourceDirectory $relative
    if ([System.IO.Path]::GetFullPath($state.FullName) -eq
        [System.IO.Path]::GetFullPath($destination)) {
        return
    }
    New-Item -ItemType Directory -Force -Path `
        ([System.IO.Path]::GetDirectoryName($destination)) | Out-Null
    $sourceHash = Get-Sha256Hex $state.FullName
    if (Test-Path -LiteralPath $destination -PathType Leaf) {
        $targetHash = Get-Sha256Hex $destination
        if ($sourceHash -ne $targetHash) {
            throw "Complete shard state is nondeterministic: $ShardId."
        }
        return
    }
    $temporary = $destination + '.' + $PID + '.' +
        [Guid]::NewGuid().ToString('N') + '.tmp'
    Copy-Item -LiteralPath $state.FullName -Destination $temporary
    if ((Get-Sha256Hex $temporary) -ne $sourceHash) {
        throw "Natural shard temporary copy drifted: $ShardId."
    }
    try {
        Move-Item -LiteralPath $temporary -Destination $destination
    }
    catch {
        if (-not (Test-Path -LiteralPath $destination -PathType Leaf) -or
            (Get-Sha256Hex $destination) -ne $sourceHash) {
            throw
        }
    }
    finally {
        if (Test-Path -LiteralPath $temporary -PathType Leaf) {
            Remove-Item -LiteralPath $temporary
        }
    }
}
function Publish-CentralBarrierIfExact {
    $byShard = [System.Collections.Generic.Dictionary[string,object]]::new(
        [System.StringComparer]::Ordinal)
    $identitySet = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    $commonNames = @(
        'currentSource','scene','portfolio','descriptors','measurements',
        'shardCount','shardKeySet','handlers','executors',
        'clearanceProfileMode','clearanceProfileAuthority')
    $common = $null
    foreach ($state in Get-ChildItem -LiteralPath $centralSourceDirectory `
                 -Filter '*.state' -File -Recurse -ErrorAction SilentlyContinue) {
        $head = @(Read-StateLines $state.FullName | Select-Object -First 15)
        $shardLine = $head | Where-Object {
            $_.StartsWith('shardId=', [System.StringComparison]::Ordinal)
        }
        if (@($shardLine).Count -ne 1) {
            throw "Central state has no exact shard identity: $($state.FullName)."
        }
        $shardId = $shardLine.Substring('shardId='.Length)
        if ($shardId -notin $allShardIds -or $byShard.ContainsKey($shardId)) {
            throw "Central state union is foreign or duplicated: $shardId."
        }
        $validated = Read-ValidatedState $state $shardId -RequireComplete
        $headers = $validated.Headers
        if (-not $identitySet.Add($headers['identity'])) {
            throw "Central state union duplicates a run identity: $($headers['identity'])."
        }
        $expectedRelative = $headers['identity'].Substring(0, 32) + '/' +
            $headers['shard'].Substring(0, 16) + '.state'
        $actualRelative = [System.IO.Path]::GetRelativePath(
            $centralSourceDirectory, $state.FullName).Replace('\', '/')
        if (-not [string]::Equals(
                $actualRelative, $expectedRelative,
                [System.StringComparison]::Ordinal)) {
            throw "Central state path is not bound to its identity: expected=$expectedRelative actual=$actualRelative."
        }
        if ($null -eq $common) {
            $common = @{}
            foreach ($name in $commonNames) {
                $common[$name] = $headers[$name]
            }
        }
        else {
            foreach ($name in $commonNames) {
                if (-not [string]::Equals(
                        $common[$name], $headers[$name],
                        [System.StringComparison]::Ordinal)) {
                    throw "Central state run identity drifts at '$name': $($state.FullName)."
                }
            }
        }
        $byShard.Add($shardId, $validated)
    }
    if ($byShard.Count -lt $allShardIds.Count) {
        return $null
    }
    if ($byShard.Count -ne $allShardIds.Count) {
        throw "Central state denominator overflowed: $($byShard.Count)."
    }
    foreach ($shardId in $allShardIds) {
        if (-not $byShard.ContainsKey($shardId) -or
            $byShard[$shardId].RecordCount -ne $ExpectedSeedCount) {
            return $null
        }
    }
    $stateHashLines = [System.Collections.Generic.List[string]]::new()
    foreach ($shardId in $allShardIds) {
        $stateHash = Get-Sha256Hex $byShard[$shardId].File.FullName
        $stateHashLines.Add($shardId + '=' + $stateHash)
    }
    $aggregateBytes = [System.Text.UTF8Encoding]::new($false, $true).GetBytes(
        [string]::Join("`n", $stateHashLines) + "`n")
    $aggregateSha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $stateSetHashText = [System.BitConverter]::ToString(
            $aggregateSha.ComputeHash($aggregateBytes))
        $stateSetDigest = $stateSetHashText.Replace('-', '').ToLowerInvariant()
    }
    finally {
        $aggregateSha.Dispose()
    }
    $barrierPath = Join-Path $centralSourceDirectory 'exact-union.barrier'
    $barrierText =
        "schema=v27-natural-partition-exact-union@1`n" +
        "result=EXACT_UNION_READY`n" +
        "currentSource=$ExpectedSourceDigest`n" +
        "scene=$officialSceneSha256`n" +
        "clearanceProfileMode=$ProfileMode`n" +
        "clearanceProfileAuthority=$ExpectedProfileAuthorityDigest`n" +
        "shardKeySet=$expectedKeySetDigest`n" +
        "shards=$($allShardIds.Count)`n" +
        "seedsPerShard=$ExpectedSeedCount`n" +
        "observations=$($allShardIds.Count * $ExpectedSeedCount)`n" +
        "stateSetDigest=$stateSetDigest`n"
    $barrierTemporary = $barrierPath + '.' + $PID + '.' +
        [Guid]::NewGuid().ToString('N') + '.tmp'
    [System.IO.File]::WriteAllText(
        $barrierTemporary,
        $barrierText,
        [System.Text.UTF8Encoding]::new($false, $true))
    if (Test-Path -LiteralPath $barrierPath -PathType Leaf) {
        $existing = [System.IO.File]::ReadAllText(
            $barrierPath,
            [System.Text.UTF8Encoding]::new($false, $true))
        if (-not [string]::Equals(
                $existing, $barrierText, [System.StringComparison]::Ordinal)) {
            Remove-Item -LiteralPath $barrierTemporary
            throw 'Central exact-union barrier is nondeterministic.'
        }
        Remove-Item -LiteralPath $barrierTemporary
        return $barrierPath
    }
    try {
        Move-Item -LiteralPath $barrierTemporary -Destination $barrierPath
    }
    catch {
        if (-not (Test-Path -LiteralPath $barrierPath -PathType Leaf) -or
            -not [string]::Equals(
                [System.IO.File]::ReadAllText(
                    $barrierPath,
                    [System.Text.UTF8Encoding]::new($false, $true)),
                $barrierText,
                [System.StringComparison]::Ordinal)) {
            throw
        }
    }
    finally {
        if (Test-Path -LiteralPath $barrierTemporary -PathType Leaf) {
            Remove-Item -LiteralPath $barrierTemporary
        }
    }
    return $barrierPath
}
function Seed-AssignedCompleteStatesFromCentral {
    $centralByShard =
        [System.Collections.Generic.Dictionary[string,object]]::new(
            [System.StringComparer]::Ordinal)
    foreach ($state in Get-ChildItem -LiteralPath $centralSourceDirectory `
                 -Filter '*.state' -File -Recurse -ErrorAction SilentlyContinue) {
        $head = @(Read-StateLines $state.FullName | Select-Object -First 15)
        $shardHeaders = @($head | Where-Object {
            $_.StartsWith('shardId=', [System.StringComparison]::Ordinal)
        })
        if ($shardHeaders.Count -ne 1) {
            throw "Central seed state has no exact shard identity: $($state.FullName)."
        }
        $shardId = $shardHeaders[0].Substring('shardId='.Length)
        if ($shardId -notin $allShardIds -or
            $centralByShard.ContainsKey($shardId)) {
            throw "Central seed state is foreign or duplicated: $shardId."
        }
        $validated = Read-ValidatedState $state $shardId -RequireComplete
        $headers = $validated.Headers
        $expectedRelative = $headers['identity'].Substring(0, 32) + '/' +
            $headers['shard'].Substring(0, 16) + '.state'
        $actualRelative = [System.IO.Path]::GetRelativePath(
            $centralSourceDirectory,
            $state.FullName).Replace('\', '/')
        if (-not [string]::Equals(
                $actualRelative,
                $expectedRelative,
                [System.StringComparison]::Ordinal)) {
            throw "Central seed state path is not bound to its identity: expected=$expectedRelative actual=$actualRelative."
        }
        $centralByShard.Add($shardId, $validated)
    }

    $localIndex = Build-CurrentStateIndex
    $seeded = 0
    $sameHash = 0
    foreach ($shardId in $assigned) {
        if (-not $centralByShard.ContainsKey($shardId)) {
            continue
        }
        $source = $centralByShard[$shardId]
        $headers = $source.Headers
        $relative = $headers['identity'].Substring(0, 32) + '/' +
            $headers['shard'].Substring(0, 16) + '.state'
        $destination = Join-Path $localSourceDirectory $relative
        $destinationFullPath = [System.IO.Path]::GetFullPath($destination)
        $localPrefix = [System.IO.Path]::GetFullPath(
            $localSourceDirectory).TrimEnd('\') + '\'
        if (-not $destinationFullPath.StartsWith(
                $localPrefix,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Central seed destination escaped its source namespace: $shardId."
        }

        $existing = Find-CurrentState $shardId -Index $localIndex
        if ($null -ne $existing) {
            if ($existing.RecordCount -ne $ExpectedSeedCount -or
                -not [string]::Equals(
                    [System.IO.Path]::GetFullPath($existing.File.FullName),
                    $destinationFullPath,
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Local seed state is partial or not identity-bound: $shardId."
            }
            if ((Get-Sha256Hex $existing.File.FullName) -ne
                (Get-Sha256Hex $source.File.FullName)) {
                throw "Local seed state conflicts with central authority: $shardId."
            }
            $sameHash++
            continue
        }
        if (Test-Path -LiteralPath $destinationFullPath) {
            throw "Local seed destination contains an unvalidated state: $shardId."
        }

        New-Item -ItemType Directory -Force -Path `
            ([System.IO.Path]::GetDirectoryName($destinationFullPath)) | Out-Null
        $sourceHash = Get-Sha256Hex $source.File.FullName
        $temporary = $destinationFullPath + '.' + $PID + '.' +
            [Guid]::NewGuid().ToString('N') + '.tmp'
        Copy-Item -LiteralPath $source.File.FullName -Destination $temporary
        try {
            if ((Get-Sha256Hex $temporary) -ne $sourceHash) {
                throw "Central seed temporary copy drifted: $shardId."
            }
            try {
                Move-Item -LiteralPath $temporary -Destination $destinationFullPath
            }
            catch {
                if (-not (Test-Path -LiteralPath $destinationFullPath) -or
                    (Get-Sha256Hex $destinationFullPath) -ne $sourceHash) {
                    throw
                }
            }
        }
        finally {
            if (Test-Path -LiteralPath $temporary) {
                Remove-Item -LiteralPath $temporary
            }
        }
        $seeded++
    }
    Write-Output (
        'Central assigned-state seed complete: copied=' + $seeded +
        ';sameHash=' + $sameHash + ';available=' + $centralByShard.Count +
        ';assigned=' + $assigned.Count + '.')
}
function Stop-VerifiedWorker([System.Diagnostics.Process]$Process) {
    if ($null -eq $Process -or $Process.HasExited) {
        return
    }
    $live = Get-CimInstance Win32_Process -Filter `
        "ProcessId = $($Process.Id)"
    $commandLine = [string]$live.CommandLine
    $processPath = (Get-Process -Id $Process.Id -ErrorAction Stop).Path
    if (-not [string]::Equals(
            [System.IO.Path]::GetFullPath($processPath),
            $resolvedUnity,
            [System.StringComparison]::OrdinalIgnoreCase) -or
        $commandLine.IndexOf($resolvedWorker,
            [System.StringComparison]::OrdinalIgnoreCase) -lt 0 -or
        $commandLine.IndexOf('-batchmode',
            [System.StringComparison]::OrdinalIgnoreCase) -lt 0 -or
        $commandLine.IndexOf($executeMethod,
            [System.StringComparison]::Ordinal) -lt 0) {
        throw "Refusing to stop unverified shard Worker $($Process.Id)."
    }
    Stop-Process -Id $Process.Id
    if (-not $Process.WaitForExit(30000)) {
        throw "Shard Worker did not exit after verified stop: $($Process.Id)."
    }
}
function Invoke-OfficialLeaseRecovery {
    $recoveryLogPath = Join-Path $logDirectory (
        'partition-' + $PartitionIndex + '-of-' + $PartitionCount +
        '-lease-recovery.log')
    $recoveryArguments = @(
        '-batchmode',
        '-nographics',
        '-quit',
        '-projectPath', $resolvedWorker,
        '-executeMethod',
        'SyntheticPreparedOutputCanaryGameplaySceneLease.RestoreOwned',
        '-logFile', $recoveryLogPath)
    $recovery = Start-Process -FilePath $resolvedUnity `
        -ArgumentList $recoveryArguments -WindowStyle Hidden -PassThru -Wait
    if ($recovery.ExitCode -ne 0) {
        $tail = if (Test-Path -LiteralPath $recoveryLogPath) {
            (Get-Content -LiteralPath $recoveryLogPath -Tail 160) -join "`n"
        } else {
            'log missing'
        }
        throw "Official synthetic lease recovery failed.`n$tail"
    }
    $leaseMarker = Join-Path $resolvedWorker `
        'Temp\v27-synthetic-gameplay-scene-lease.json'
    $leaseAssetDirectory = Join-Path $resolvedWorker `
        'Assets\__V27SyntheticPreparedOutputCanary'
    if ((Test-Path -LiteralPath $leaseMarker) -or
        (Test-Path -LiteralPath $leaseAssetDirectory)) {
        throw 'Official synthetic lease recovery left owned state behind.'
    }
}
if ($SeedAssignedStatesFromCentral) {
    Seed-AssignedCompleteStatesFromCentral
}
$alreadyComplete = 0
foreach ($shardId in $assigned) {
    $existing = Find-CurrentState $shardId
    if ($null -ne $existing -and $existing.RecordCount -eq $ExpectedSeedCount) {
        $validated = Read-ValidatedState $existing.File $shardId -RequireComplete
        Publish-State $validated $shardId
        $alreadyComplete++
    }
}
$process = $null
if ($alreadyComplete -ne $assigned.Count) {
    Move-StaleRequest
    Move-ToQuarantine $failureReportPath `
        ('prelaunch-runner-report-partition-' + $PartitionIndex)
    $logPath = Join-Path $logDirectory `
        ('partition-' + $PartitionIndex + '-of-' + $PartitionCount + '.log')
    $arguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath', $resolvedWorker,
        '-executeMethod', $executeMethod,
        '-logFile', $logPath)
    $runStartedUtc = [DateTime]::UtcNow
    try {
        $env:UNITY_MCP_DISABLE_BATCH = '1'
        $env:V27_NATURAL_FOCUS_SHARD = $null
        $env:V27_NATURAL_PARTITION_INDEX = $PartitionIndex.ToString(
            [System.Globalization.CultureInfo]::InvariantCulture)
        $env:V27_NATURAL_PARTITION_COUNT = $PartitionCount.ToString(
            [System.Globalization.CultureInfo]::InvariantCulture)
        $env:V27_EXPECTED_CURRENT_SOURCE_DIGEST = $ExpectedSourceDigest
        $env:V27_NATURAL_EXPECTED_SHARD_COUNT = $allShardIds.Count.ToString(
            [System.Globalization.CultureInfo]::InvariantCulture)
        $env:V27_NATURAL_EXPECTED_SHARD_KEYSET_DIGEST = $expectedKeySetDigest
        if ($ProfileMode -eq 'bootstrap') {
            $env:V27_OUTPUT_CLEARANCE_PROFILE_BOOTSTRAP =
                'natural-92x32-authored-cycle-baseline@1'
        }
        else {
            $env:V27_OUTPUT_CLEARANCE_PROFILE_BOOTSTRAP = $null
        }
        $process = Start-Process -FilePath $resolvedUnity `
            -ArgumentList $arguments -WindowStyle Hidden -PassThru
    }
    finally {
        $env:V27_NATURAL_PARTITION_INDEX = $null
        $env:V27_NATURAL_PARTITION_COUNT = $null
        $env:V27_EXPECTED_CURRENT_SOURCE_DIGEST = $null
        $env:V27_NATURAL_EXPECTED_SHARD_COUNT = $null
        $env:V27_NATURAL_EXPECTED_SHARD_KEYSET_DIGEST = $null
        $env:V27_OUTPUT_CLEARANCE_PROFILE_BOOTSTRAP = $null
    }
    $deadline = [DateTime]::UtcNow.AddMinutes($PartitionTimeoutMinutes)
    $requestSeen = $false
    $publishedShardIds = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    try {
        while ($true) {
            Start-Sleep -Seconds 2
            $complete = 0
            $currentStateIndex = Build-CurrentStateIndex
            foreach ($shardId in $assigned) {
                if ($publishedShardIds.Contains($shardId)) {
                    $complete++
                    continue
                }
                $state = Find-CurrentState $shardId -Index $currentStateIndex
                if ($null -ne $state -and
                    $state.RecordCount -eq $ExpectedSeedCount) {
                    $validated = Read-ValidatedState `
                        $state.File $shardId -RequireComplete
                    Publish-State $validated $shardId
                    [void]$publishedShardIds.Add($shardId)
                    $complete++
                }
            }
            if ($complete -eq $assigned.Count) {
                # Partition mode intentionally keeps the coordinator alive
                # after its assigned durable states are complete. The wrapper
                # owns settlement: stop only the verified batch process, then
                # invoke the lease authority's official recovery entry point.
                break
            }
            if (Test-Path -LiteralPath $failureReportPath -PathType Leaf) {
                $failureFile = Get-Item -LiteralPath $failureReportPath
                if ($failureFile.LastWriteTimeUtc -lt $runStartedUtc) {
                    Move-ToQuarantine $failureReportPath `
                        ('stale-runner-report-partition-' + $PartitionIndex)
                    throw 'Natural partition runner report predates this Worker.'
                }
                $failureText = Get-Content -LiteralPath $failureReportPath -Raw
                $sourceMatch = [System.Text.RegularExpressions.Regex]::Match(
                    $failureText,
                    '(?m)^currentSourceDigest=([0-9a-f]{64})\r?$')
                if ($sourceMatch.Success -and
                    $sourceMatch.Groups[1].Value -ne $ExpectedSourceDigest) {
                    Move-ToQuarantine $failureReportPath `
                        ('foreign-source-runner-report-partition-' + $PartitionIndex)
                    throw 'Natural partition runner report has a foreign source digest.'
                }
                if ($failureText -match 'RESULT=FAIL|\[FAIL\]') {
                    if (-not $sourceMatch.Success -and
                        $failureText -notmatch '\[FAIL\] EDITOR_BOOT_GUARD:') {
                        throw 'Natural partition failure report has no source identity.'
                    }
                    throw "Natural partition boot/run failed.`n$failureText"
                }
            }
            if (Test-Path -LiteralPath $requestPath -PathType Leaf) {
                $requestSeen = $true
            }
            # The durable request is only a launch handshake. Unity may remove
            # it while the final coroutine/report flush is still in flight, so
            # disappearance alone is not a completion or failure authority.
            # State cardinality, the typed runner report, process exit, and the
            # deadline below remain the fail-loud authorities.
            $process.Refresh()
            if ($process.HasExited) {
                $tail = if (Test-Path -LiteralPath $logPath) {
                    (Get-Content -LiteralPath $logPath -Tail 160) -join "`n"
                } else {
                    'log missing'
                }
                throw "Worker exited before completing partition.`n$tail"
            }
            if ([DateTime]::UtcNow -ge $deadline) {
                throw "Natural partition timed out after $PartitionTimeoutMinutes minutes."
            }
        }
    }
    finally {
        Stop-VerifiedWorker $process
        Invoke-OfficialLeaseRecovery
        Move-StaleRequest
    }
}
foreach ($shardId in $assigned) {
    $state = Find-CurrentState $shardId -RequireComplete
    Publish-State $state $shardId
}
$centralBarrier = Publish-CentralBarrierIfExact
if ($null -eq $centralBarrier) {
    $centralBarrierStatus = 'PENDING_OTHER_PARTITIONS'
}
else {
    $centralBarrierStatus = 'EXACT_UNION_READY'
}
$doneTemporary = $donePath + '.' + $PID + '.tmp'
[System.IO.File]::WriteAllText(
    $doneTemporary,
    'source=' + $ExpectedSourceDigest + "`n" +
    'clearanceProfileMode=' + $ProfileMode + "`n" +
    'clearanceProfileAuthority=' + $ExpectedProfileAuthorityDigest + "`n" +
    'shardKeySet=' + $expectedKeySetDigest + "`n" +
    'completed=' + $assigned.Count + "`n" +
    'seedsPerShard=' + $ExpectedSeedCount + "`n" +
    'centralBarrier=' + $centralBarrierStatus + "`n",
    [System.Text.UTF8Encoding]::new($false, $true))
Move-Item -LiteralPath $doneTemporary -Destination $donePath -Force
