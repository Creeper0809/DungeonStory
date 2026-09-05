<#
.SYNOPSIS
Validates one completed V27 natural central exact-union and stages it into the
authoritative project's local natural shard store with backup and rollback.

.DESCRIPTION
Run once with -ValidateOnly. If that passes, stop/avoid any main natural
portfolio request and run again without -ValidateOnly. The helper copies only
the exact barrier-owned 92 state files into a sibling staging directory,
validates the staged copy again, renames an existing current-source directory
to a retained backup, and renames the stage into place. A failed promotion is
quarantined and the prior directory is restored.

.EXAMPLE
$source = '<64-character-current-source-digest>'
$mode = 'bootstrap'
$authority = '<64-character-profile-authority-digest>'
$central = "../DungeonStoryV27NaturalCentral/$source/$mode/$authority"
& ./tools/V27Balance/stage_central_natural_union.ps1 `
    -CentralUnionDirectory $central `
    -ExpectedSourceDigest $source `
    -ExpectedProfileMode $mode `
    -ExpectedProfileAuthorityDigest $authority `
    -ExpectedShardKeySetDigest '<64-character-shard-key-set-digest>' `
    -ExpectedPortfolioDigest '<64-character-current-portfolio-digest>' `
    -ExpectedMeasurementPortfolioDigest '<64-character-measurement-portfolio-digest>' `
    -ValidateOnly

.EXAMPLE
& ./tools/V27Balance/stage_central_natural_union.ps1 `
    -CentralUnionDirectory $central `
    -ExpectedSourceDigest $source `
    -ExpectedProfileMode $mode `
    -ExpectedProfileAuthorityDigest $authority `
    -ExpectedShardKeySetDigest '<64-character-shard-key-set-digest>' `
    -ExpectedPortfolioDigest '<64-character-current-portfolio-digest>' `
    -ExpectedMeasurementPortfolioDigest '<64-character-measurement-portfolio-digest>'
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$CentralUnionDirectory,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{64}$')]
    [string]$ExpectedSourceDigest,

    [Parameter(Mandatory = $true)]
    [ValidateSet('bootstrap', 'strict')]
    [string]$ExpectedProfileMode,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{64}$')]
    [string]$ExpectedProfileAuthorityDigest,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{64}$')]
    [string]$ExpectedShardKeySetDigest,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{64}$')]
    [string]$ExpectedPortfolioDigest,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{64}$')]
    [string]$ExpectedMeasurementPortfolioDigest,

    [string]$MainProject = (
        Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),

    [ValidateRange(1, 4096)]
    [int]$ExpectedShardCount = 92,

    [ValidateRange(1, 4096)]
    [int]$ExpectedSeedCount = 32,

    [int]$ExpectedFirstSeed = 157181,

    [ValidatePattern('^[0-9a-f]{64}$')]
    [string]$ExpectedSceneDigest =
        '6c35a17693d3cedca2c85b89b22a8bff9b5bae6de88c01b255481c058d2aee40',

    [switch]$ValidateOnly
)

$ErrorActionPreference = 'Stop'
$strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
$stateSchema = 'production-output-clearance-natural-shard-store@4'
$barrierSchema = 'v27-natural-partition-exact-union@1'

function Get-FullPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw 'A non-empty path is required.'
    }
    return [System.IO.Path]::GetFullPath($Path).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
}

function Assert-DescendantPath(
    [string]$Candidate,
    [string]$Parent,
    [string]$Label) {
    $candidateFull = Get-FullPath $Candidate
    $parentFull = Get-FullPath $Parent
    $prefix = $parentFull + [System.IO.Path]::DirectorySeparatorChar
    if (-not $candidateFull.StartsWith(
            $prefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label escaped its required parent: $candidateFull"
    }
}

function Get-Sha256Hex([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
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

function Get-TextSha256([string]$Text) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hex = [System.BitConverter]::ToString(
            $sha.ComputeHash($strictUtf8.GetBytes($Text)))
        return $hex.Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Get-CanonicalSemanticDigest([object[]]$Tokens) {
    $canonical = [System.Text.StringBuilder]::new()
    foreach ($token in $Tokens) {
        $value = if ($null -eq $token) {
            ''
        } else {
            [System.Convert]::ToString(
                $token,
                [System.Globalization.CultureInfo]::InvariantCulture)
        }
        [void]$canonical.Append($strictUtf8.GetByteCount($value).ToString(
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

function Read-ExactHeaderFile(
    [string]$Path,
    [string[]]$RequiredNames,
    [switch]$AllowRecords) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required evidence file is missing: $Path"
    }
    $headers = [System.Collections.Generic.Dictionary[string,string]]::new(
        [System.StringComparer]::Ordinal)
    $records = [System.Collections.Generic.List[string[]]]::new()
    $slices = [System.Collections.Generic.List[string[]]]::new()
    $routeBatches = [System.Collections.Generic.List[string[]]]::new()
    foreach ($line in [System.IO.File]::ReadAllLines($Path, $strictUtf8)) {
        if ($AllowRecords -and $line.StartsWith(
                'R|', [System.StringComparison]::Ordinal)) {
            $fields = $line.Split([char]'|')
            if ($fields.Count -ne 38) {
                throw "Natural state record width is not 38: $Path"
            }
            $records.Add($fields)
            continue
        }
        if ($AllowRecords -and $line.StartsWith(
                'S|', [System.StringComparison]::Ordinal)) {
            $fields = $line.Split([char]'|')
            if ($fields.Count -ne 10) {
                throw "Natural state slice width is not 10: $Path"
            }
            $slices.Add($fields)
            continue
        }
        if ($AllowRecords -and $line.StartsWith(
                'B|', [System.StringComparison]::Ordinal)) {
            $fields = $line.Split([char]'|')
            if ($fields.Count -ne 4) {
                throw "Natural state route-batch width is not 4: $Path"
            }
            $routeBatches.Add($fields)
            continue
        }
        $separator = $line.IndexOf('=')
        if ($separator -le 0) {
            throw "Malformed or unexpected evidence line: $Path :: $line"
        }
        $name = $line.Substring(0, $separator)
        $value = $line.Substring($separator + 1)
        if (-not $headers.TryAdd($name, $value)) {
            throw "Duplicate evidence header '$name': $Path"
        }
    }
    if ($headers.Count -ne $RequiredNames.Count) {
        throw "Evidence header cardinality drifted: $Path"
    }
    foreach ($name in $RequiredNames) {
        if (-not $headers.ContainsKey($name)) {
            throw "Required evidence header '$name' is missing: $Path"
        }
    }
    return [pscustomobject]@{
        Headers = $headers
        Records = $records
        Slices = $slices
        RouteBatches = $routeBatches
    }
}

function Require-Header(
    [System.Collections.Generic.Dictionary[string,string]]$Headers,
    [string]$Name,
    [string]$Expected,
    [string]$Path) {
    if (-not $Headers.ContainsKey($Name) -or
        -not [string]::Equals(
            $Headers[$Name], $Expected, [System.StringComparison]::Ordinal)) {
        $actual = if ($Headers.ContainsKey($Name)) {
            $Headers[$Name]
        } else {
            '<missing>'
        }
        throw "Evidence header drifted: $Path :: $Name expected=$Expected actual=$actual"
    }
}

function Require-LowercaseDigest(
    [string]$Value,
    [string]$Label,
    [string]$Path) {
    if ($Value -notmatch '^[0-9a-f]{64}$') {
        throw "Noncanonical SHA-256 for ${Label}: $Path"
    }
}

function Test-Union([string]$UnionRoot) {
    $root = Get-FullPath $UnionRoot
    if (-not (Test-Path -LiteralPath $root -PathType Container)) {
        throw "Central union directory is missing: $root"
    }
    $barrierPath = Join-Path $root 'exact-union.barrier'
    $barrierNames = @(
        'schema', 'result', 'currentSource', 'scene',
        'clearanceProfileMode', 'clearanceProfileAuthority', 'shardKeySet',
        'shards', 'seedsPerShard', 'observations', 'stateSetDigest')
    $barrier = Read-ExactHeaderFile $barrierPath $barrierNames
    $b = $barrier.Headers
    Require-Header $b 'schema' $barrierSchema $barrierPath
    Require-Header $b 'result' 'EXACT_UNION_READY' $barrierPath
    Require-Header $b 'currentSource' $ExpectedSourceDigest $barrierPath
    Require-Header $b 'scene' $ExpectedSceneDigest $barrierPath
    Require-Header $b 'clearanceProfileMode' $ExpectedProfileMode $barrierPath
    Require-Header $b 'clearanceProfileAuthority' `
        $ExpectedProfileAuthorityDigest $barrierPath
    Require-Header $b 'shardKeySet' $ExpectedShardKeySetDigest $barrierPath
    Require-Header $b 'shards' `
        $ExpectedShardCount.ToString(
            [System.Globalization.CultureInfo]::InvariantCulture) $barrierPath
    Require-Header $b 'seedsPerShard' `
        $ExpectedSeedCount.ToString(
            [System.Globalization.CultureInfo]::InvariantCulture) $barrierPath
    Require-Header $b 'observations' `
        ([long]$ExpectedShardCount * [long]$ExpectedSeedCount).ToString(
            [System.Globalization.CultureInfo]::InvariantCulture) $barrierPath
    Require-LowercaseDigest $b['stateSetDigest'] 'barrier state set' $barrierPath

    $reparseDirectories = @(Get-ChildItem -LiteralPath $root -Directory `
        -Recurse -Force | Where-Object {
            ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
        })
    if ($reparseDirectories.Count -ne 0) {
        throw "Central union contains a reparse directory: $($reparseDirectories[0].FullName)"
    }
    $allFiles = @(Get-ChildItem -LiteralPath $root -File -Recurse -Force)
    foreach ($file in $allFiles) {
        Assert-DescendantPath $file.FullName $root 'Central union file'
        if (($file.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Central union contains a reparse file: $($file.FullName)"
        }
    }
    $unexpected = @($allFiles | Where-Object {
        -not [string]::Equals(
            $_.FullName, $barrierPath,
            [System.StringComparison]::OrdinalIgnoreCase) -and
        -not [string]::Equals(
            $_.Extension, '.state', [System.StringComparison]::Ordinal)
    })
    if ($unexpected.Count -ne 0) {
        throw "Central union contains an unexpected file: $($unexpected[0].FullName)"
    }
    $stateFiles = @($allFiles | Where-Object {
        [string]::Equals(
            $_.Extension, '.state', [System.StringComparison]::Ordinal)
    })
    if ($stateFiles.Count -ne $ExpectedShardCount) {
        throw "Central union state denominator drifted: expected=$ExpectedShardCount actual=$($stateFiles.Count)"
    }

    $headerNames = @(
        'schema', 'identity', 'currentSource', 'scene', 'portfolio',
        'descriptors', 'measurements', 'shardCount', 'shardKeySet', 'shardId',
        'shard', 'handlers', 'executors', 'clearanceProfileMode',
        'clearanceProfileAuthority')
    $digestNames = @(
        'identity', 'currentSource', 'scene', 'portfolio', 'descriptors',
        'measurements', 'shardKeySet', 'shard', 'handlers', 'executors',
        'clearanceProfileAuthority')
    $commonNames = @(
        'currentSource', 'scene', 'portfolio', 'descriptors', 'measurements',
        'shardCount', 'shardKeySet', 'handlers', 'executors',
        'clearanceProfileMode', 'clearanceProfileAuthority')
    $common = $null
    $byShardId = [System.Collections.Generic.Dictionary[string,object]]::new(
        [System.StringComparer]::Ordinal)
    $identitySet = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)

    foreach ($file in $stateFiles) {
        $state = Read-ExactHeaderFile $file.FullName $headerNames -AllowRecords
        $h = $state.Headers
        Require-Header $h 'schema' $stateSchema $file.FullName
        Require-Header $h 'currentSource' $ExpectedSourceDigest $file.FullName
        Require-Header $h 'scene' $ExpectedSceneDigest $file.FullName
        Require-Header $h 'portfolio' $ExpectedPortfolioDigest $file.FullName
        Require-Header $h 'measurements' `
            $ExpectedMeasurementPortfolioDigest $file.FullName
        Require-Header $h 'shardCount' `
            $ExpectedShardCount.ToString(
                [System.Globalization.CultureInfo]::InvariantCulture) `
            $file.FullName
        Require-Header $h 'shardKeySet' $b['shardKeySet'] $file.FullName
        Require-Header $h 'clearanceProfileMode' `
            $ExpectedProfileMode $file.FullName
        Require-Header $h 'clearanceProfileAuthority' `
            $ExpectedProfileAuthorityDigest $file.FullName
        foreach ($name in $digestNames) {
            Require-LowercaseDigest $h[$name] $name $file.FullName
        }
        if ([string]::IsNullOrWhiteSpace($h['shardId']) -or
            -not [string]::Equals(
                $h['shardId'], $h['shardId'].Trim(),
                [System.StringComparison]::Ordinal)) {
            throw "Shard ID is not canonical: $($file.FullName)"
        }
        if (-not $identitySet.Add($h['identity'])) {
            throw "Duplicate run identity: $($h['identity'])"
        }
        if (-not $byShardId.TryAdd($h['shardId'], [pscustomobject]@{
                    File = $file
                    State = $state
                })) {
            throw "Duplicate shard ID: $($h['shardId'])"
        }

        $expectedRelative = $h['identity'].Substring(0, 32) + '/' +
            $h['shard'].Substring(0, 16) + '.state'
        $actualRelative = [System.IO.Path]::GetRelativePath(
            $root, $file.FullName).Replace('\', '/')
        if (-not [string]::Equals(
                $actualRelative, $expectedRelative,
                [System.StringComparison]::Ordinal)) {
            throw "State path is not bound to run identity/shard digest: expected=$expectedRelative actual=$actualRelative"
        }

        if ($null -eq $common) {
            $common = @{}
            foreach ($name in $commonNames) {
                $common[$name] = $h[$name]
            }
        }
        else {
            foreach ($name in $commonNames) {
                if (-not [string]::Equals(
                        $common[$name], $h[$name],
                        [System.StringComparison]::Ordinal)) {
                    throw "Cross-shard run identity field drifted: $name :: $($file.FullName)"
                }
            }
        }

        if ($state.Records.Count -ne $ExpectedSeedCount) {
            throw "Shard seed denominator drifted: $($h['shardId']) expected=$ExpectedSeedCount actual=$($state.Records.Count)"
        }
        $observationIds = [System.Collections.Generic.HashSet[string]]::new(
            [System.StringComparer]::Ordinal)
        $commitKeys = [System.Collections.Generic.HashSet[string]]::new(
            [System.StringComparer]::Ordinal)
        $seedIndexes = [System.Collections.Generic.HashSet[int]]::new()
        foreach ($record in $state.Records) {
            $seedIndex = 0
            $deterministicSeed = 0
            if (-not [int]::TryParse(
                    $record[3],
                    [System.Globalization.NumberStyles]::Integer,
                    [System.Globalization.CultureInfo]::InvariantCulture,
                    [ref]$seedIndex) -or
                $seedIndex -lt 0 -or $seedIndex -ge $ExpectedSeedCount -or
                -not [int]::TryParse(
                    $record[4],
                    [System.Globalization.NumberStyles]::Integer,
                    [System.Globalization.CultureInfo]::InvariantCulture,
                    [ref]$deterministicSeed) -or
                $deterministicSeed -ne ($ExpectedFirstSeed + $seedIndex)) {
                throw "Shard contains a foreign seed cohort: $($h['shardId'])"
            }
            if (-not $seedIndexes.Add($seedIndex) -or
                -not $observationIds.Add($record[1]) -or
                -not $commitKeys.Add($record[5] + [char]0x1f + $record[8])) {
                throw "Shard records are not bijective: $($h['shardId'])"
            }
        }
        for ($index = 0; $index -lt $ExpectedSeedCount; $index++) {
            if (-not $seedIndexes.Contains($index)) {
                throw "Shard seed index is missing: $($h['shardId'])/$index"
            }
        }
        $sliceObservationIds = [System.Collections.Generic.HashSet[string]]::new(
            [System.StringComparer]::Ordinal)
        $sliceMassByObservation = @{}
        $sliceDigestsByObservation = @{}
        foreach ($slice in $state.Slices) {
            if (-not $observationIds.Contains($slice[1])) {
                throw "Shard contains an orphan output slice: $($h['shardId'])"
            }
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
                throw "Shard output slice is noncanonical: $($h['shardId'])/$($slice[1])"
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
                throw "Shard output slice source digest drifted: $($h['shardId'])/$($slice[1])"
            }
            if (-not $sliceDigestsByObservation.ContainsKey($slice[1])) {
                $sliceDigestsByObservation[$slice[1]] =
                    [System.Collections.Generic.HashSet[string]]::new(
                        [System.StringComparer]::Ordinal)
            }
            if (-not $sliceDigestsByObservation[$slice[1]].Add($slice[9])) {
                throw "Shard output slice is duplicated: $($h['shardId'])/$($slice[1])"
            }
            if (-not $sliceMassByObservation.ContainsKey($slice[1])) {
                $sliceMassByObservation[$slice[1]] = [long]0
            }
            if ($sliceMassByObservation[$slice[1]] -gt
                ([long]::MaxValue - $massGrams)) {
                throw "Shard output slice mass overflowed: $($h['shardId'])/$($slice[1])"
            }
            $sliceMassByObservation[$slice[1]] =
                [long]$sliceMassByObservation[$slice[1]] + $massGrams
            [void]$sliceObservationIds.Add($slice[1])
        }
        foreach ($record in $state.Records) {
            $observationId = $record[1]
            $expectedMassGrams = [long]0
            if (-not $sliceObservationIds.Contains($observationId) -or
                -not [long]::TryParse(
                    $record[10],
                    [System.Globalization.NumberStyles]::Integer,
                    [System.Globalization.CultureInfo]::InvariantCulture,
                    [ref]$expectedMassGrams) -or
                $expectedMassGrams -le 0 -or
                -not $sliceMassByObservation.ContainsKey($observationId) -or
                [long]$sliceMassByObservation[$observationId] -ne
                    $expectedMassGrams) {
                throw "Shard output slices are missing or mass-incomplete: $($h['shardId'])/$observationId"
            }
        }
        $routeRowsByObservation =
            [System.Collections.Generic.Dictionary[string,
                System.Collections.Generic.List[object]]]::new(
                    [System.StringComparer]::Ordinal)
        foreach ($routeBatch in $state.RouteBatches) {
            $observationId = $routeBatch[1]
            if (-not $observationIds.Contains($observationId)) {
                throw "Shard contains an orphan route batch: $($h['shardId'])/$observationId"
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
                -not [string]::Equals(
                    $routeBatch[3], $routeBatch[3].Trim(),
                    [System.StringComparison]::Ordinal)) {
                throw "Shard route batch is noncanonical: $($h['shardId'])/$observationId"
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
        foreach ($record in $state.Records) {
            $observationId = $record[1]
            if (-not $routeRowsByObservation.ContainsKey($observationId)) {
                throw "Shard record has no route batch: $($h['shardId'])/$observationId"
            }
            $rows = @($routeRowsByObservation[$observationId] |
                Sort-Object Ordinal)
            $routeIdSet = [System.Collections.Generic.HashSet[string]]::new(
                [System.StringComparer]::Ordinal)
            foreach ($row in $rows) {
                [void]$routeIdSet.Add($row.RouteBatchCommitId)
            }
            $telemetryCompletedCount = 0
            if (-not [int]::TryParse(
                    $record[19],
                    [System.Globalization.NumberStyles]::Integer,
                    [System.Globalization.CultureInfo]::InvariantCulture,
                    [ref]$telemetryCompletedCount) -or
                $telemetryCompletedCount -le 0 -or
                $rows.Count -ne $telemetryCompletedCount -or
                $routeIdSet.Count -ne $rows.Count) {
                throw "Shard route-batch denominator drifted: $($h['shardId'])/$observationId"
            }
            for ($index = 0; $index -lt $rows.Count; $index++) {
                if ($rows[$index].Ordinal -ne $index) {
                    throw "Shard route-batch ordinals are incomplete: $($h['shardId'])/$observationId"
                }
            }
        }
    }

    $shardIds = [string[]]$byShardId.Keys
    [System.Array]::Sort($shardIds, [System.StringComparer]::Ordinal)
    $computedKeySet = Get-TextSha256 (
        [string]::Join("`n", $shardIds) + "`n")
    if (-not [string]::Equals(
            $computedKeySet, $b['shardKeySet'],
            [System.StringComparison]::Ordinal)) {
        throw "Central shard key-set digest drifted: expected=$($b['shardKeySet']) actual=$computedKeySet"
    }

    $stateHashLines = [System.Collections.Generic.List[string]]::new()
    foreach ($shardId in $shardIds) {
        $stateHashLines.Add(
            $shardId + '=' + (Get-Sha256Hex $byShardId[$shardId].File.FullName))
    }
    $computedStateSet = Get-TextSha256 (
        [string]::Join("`n", $stateHashLines) + "`n")
    if (-not [string]::Equals(
            $computedStateSet, $b['stateSetDigest'],
            [System.StringComparison]::Ordinal)) {
        throw "Central state-set digest drifted: expected=$($b['stateSetDigest']) actual=$computedStateSet"
    }

    return [pscustomobject]@{
        Root = $root
        BarrierPath = $barrierPath
        BarrierSha256 = Get-Sha256Hex $barrierPath
        ShardKeySetDigest = $computedKeySet
        StateSetDigest = $computedStateSet
        ShardCount = $stateFiles.Count
        ObservationCount = [long]$stateFiles.Count * [long]$ExpectedSeedCount
        StateFiles = @($stateFiles | Sort-Object FullName)
    }
}

function Move-FailedDirectory(
    [string]$Path,
    [string]$StoreRoot,
    [string]$Reason) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        return $null
    }
    Assert-DescendantPath $Path $StoreRoot 'Failed staging directory'
    $failedRoot = Join-Path $StoreRoot '_failed-imports'
    [System.IO.Directory]::CreateDirectory($failedRoot) | Out-Null
    $safeReason = $Reason -replace '[^A-Za-z0-9_.-]', '-'
    $destination = Join-Path $failedRoot (
        [System.IO.Path]::GetFileName($Path) + '.' + $safeReason + '.' +
        [DateTime]::UtcNow.Ticks + '.' + [Guid]::NewGuid().ToString('N'))
    [System.IO.Directory]::Move($Path, $destination)
    return $destination
}

$central = Get-FullPath $CentralUnionDirectory
$project = Get-FullPath $MainProject
if (-not (Test-Path -LiteralPath (Join-Path $project 'Assets') -PathType Container) -or
    -not (Test-Path -LiteralPath (Join-Path $project 'Packages') -PathType Container)) {
    throw "MainProject is not a Unity project root: $project"
}

$validated = Test-Union $central
if ($ValidateOnly) {
    [pscustomobject]@{
        Result = 'VALIDATION_PASS'
        SourceDigest = $ExpectedSourceDigest
        ProfileMode = $ExpectedProfileMode
        ProfileAuthorityDigest = $ExpectedProfileAuthorityDigest
        PortfolioDigest = $ExpectedPortfolioDigest
        MeasurementPortfolioDigest = $ExpectedMeasurementPortfolioDigest
        Shards = $validated.ShardCount
        SeedsPerShard = $ExpectedSeedCount
        Observations = $validated.ObservationCount
        ShardKeySetDigest = $validated.ShardKeySetDigest
        StateSetDigest = $validated.StateSetDigest
        BarrierSha256 = $validated.BarrierSha256
    } | Format-List
    return
}

$storeRoot = Get-FullPath (
    Join-Path $project 'Temp/v27-output-clearance-natural-shards')
[System.IO.Directory]::CreateDirectory($storeRoot) | Out-Null
Assert-DescendantPath $storeRoot $project 'Main local shard store'
$target = Get-FullPath (Join-Path $storeRoot $ExpectedSourceDigest)
Assert-DescendantPath $target $storeRoot 'Main source shard directory'

$nonce = [Guid]::NewGuid().ToString('N')
$stage = Get-FullPath (Join-Path $storeRoot (
    '.stage-' + $ExpectedSourceDigest + '-' + $nonce))
$backupRoot = Get-FullPath (Join-Path $storeRoot '_import-backups')
$failedRoot = Get-FullPath (Join-Path $storeRoot '_failed-imports')
Assert-DescendantPath $stage $storeRoot 'Staging directory'
Assert-DescendantPath $backupRoot $storeRoot 'Backup directory'
Assert-DescendantPath $failedRoot $storeRoot 'Failed import directory'

$lockPath = Get-FullPath (Join-Path $storeRoot '.v27-natural-stage.lock')
Assert-DescendantPath $lockPath $storeRoot 'Import lock'
$requestPath = Get-FullPath (Join-Path $project `
    'Temp/v27-production-output-clearance-natural-portfolio.request')
Assert-DescendantPath $requestPath $project 'Natural portfolio request'
if (Test-Path -LiteralPath $requestPath -PathType Leaf) {
    throw "Refusing to swap shard state while a main natural run request exists: $requestPath"
}
$lock = $null
$lockCreated = $false
$backup = $null
$promoted = $false
try {
    $lock = [System.IO.FileStream]::new(
        $lockPath,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::ReadWrite,
        [System.IO.FileShare]::None)
    $lockCreated = $true
    [System.IO.Directory]::CreateDirectory($stage) | Out-Null
    [System.IO.File]::Copy(
        $validated.BarrierPath,
        (Join-Path $stage 'exact-union.barrier'),
        $false)
    foreach ($stateFile in $validated.StateFiles) {
        $relative = [System.IO.Path]::GetRelativePath(
            $validated.Root, $stateFile.FullName)
        $destination = Get-FullPath (Join-Path $stage $relative)
        Assert-DescendantPath $destination $stage 'Staged state file'
        [System.IO.Directory]::CreateDirectory(
            [System.IO.Path]::GetDirectoryName($destination)) | Out-Null
        [System.IO.File]::Copy($stateFile.FullName, $destination, $false)
    }

    $stagedValidation = Test-Union $stage
    if (-not [string]::Equals(
            $stagedValidation.StateSetDigest, $validated.StateSetDigest,
            [System.StringComparison]::Ordinal) -or
        -not [string]::Equals(
            $stagedValidation.BarrierSha256, $validated.BarrierSha256,
            [System.StringComparison]::Ordinal)) {
        throw 'Staged union differs from the validated central union.'
    }

    if (Test-Path -LiteralPath $target -PathType Container) {
        [System.IO.Directory]::CreateDirectory($backupRoot) | Out-Null
        $backup = Get-FullPath (Join-Path $backupRoot (
            $ExpectedSourceDigest + '.' + [DateTime]::UtcNow.Ticks + '.' +
            $nonce))
        Assert-DescendantPath $backup $backupRoot 'Backup destination'
        [System.IO.Directory]::Move($target, $backup)
    }
    [System.IO.Directory]::Move($stage, $target)
    $promoted = $true

    $targetValidation = Test-Union $target
    if (-not [string]::Equals(
            $targetValidation.StateSetDigest, $validated.StateSetDigest,
            [System.StringComparison]::Ordinal) -or
        -not [string]::Equals(
            $targetValidation.BarrierSha256, $validated.BarrierSha256,
            [System.StringComparison]::Ordinal)) {
        throw 'Promoted local union differs from the validated central union.'
    }

    [pscustomobject]@{
        Result = 'STAGED_COPY_PASS'
        SourceDigest = $ExpectedSourceDigest
        ProfileMode = $ExpectedProfileMode
        ProfileAuthorityDigest = $ExpectedProfileAuthorityDigest
        Target = $target
        Backup = if ($null -eq $backup) { '<none>' } else { $backup }
        Shards = $targetValidation.ShardCount
        SeedsPerShard = $ExpectedSeedCount
        Observations = $targetValidation.ObservationCount
        ShardKeySetDigest = $targetValidation.ShardKeySetDigest
        StateSetDigest = $targetValidation.StateSetDigest
        BarrierSha256 = $targetValidation.BarrierSha256
    } | Format-List
}
catch {
    $failure = $_
    try {
        if ($promoted -and (Test-Path -LiteralPath $target -PathType Container)) {
            [void](Move-FailedDirectory $target $storeRoot 'post-promotion-failure')
        }
        elseif (Test-Path -LiteralPath $stage -PathType Container) {
            [void](Move-FailedDirectory $stage $storeRoot 'pre-promotion-failure')
        }
        if ($null -ne $backup -and
            (Test-Path -LiteralPath $backup -PathType Container) -and
            -not (Test-Path -LiteralPath $target)) {
            [System.IO.Directory]::Move($backup, $target)
        }
    }
    catch {
        throw [System.AggregateException]::new(
            'Natural union import failed and rollback also failed.',
            [System.Exception[]]@($failure.Exception, $_.Exception))
    }
    throw $failure
}
finally {
    if ($null -ne $lock) {
        $lock.Dispose()
    }
    if ($lockCreated -and (Test-Path -LiteralPath $lockPath -PathType Leaf)) {
        [System.IO.File]::Delete($lockPath)
    }
}
