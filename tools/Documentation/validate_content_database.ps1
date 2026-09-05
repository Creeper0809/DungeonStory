param(
    [string]$DatabaseRoot = "docs/content-db"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path -LiteralPath $DatabaseRoot
$projectRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "../..")
$index = @(Import-Csv (Join-Path $root "content-type-index.csv"))
$content = @()
$relations = @()
$incoming = @()
$codeConsumers = @()
$errors = @()
$schemaWidths = @()

foreach ($entry in $index) {
    $typeContent = @(Import-Csv (Join-Path $root $entry.content_csv))
    $typeRelations = @(Import-Csv (Join-Path $root $entry.relation_csv))
    $typeIncoming = @(Import-Csv (Join-Path $root $entry.incoming_csv))
    $typeCodeConsumers = @(Import-Csv (Join-Path $root $entry.code_consumer_csv))
    $typeFields = @(Import-Csv (Join-Path $root $entry.field_csv))
    $content += $typeContent
    $relations += $typeRelations
    $incoming += $typeIncoming
    $codeConsumers += $typeCodeConsumers
    if (
        $typeContent.Count -ne [int]$entry.content_count -or
        $typeRelations.Count -ne [int]$entry.relation_count -or
        $typeIncoming.Count -ne [int]$entry.incoming_reference_count -or
        $typeFields.Count -ne [int]$entry.field_count -or
        $typeCodeConsumers.Count -ne [int]$entry.code_consumer_count
    ) {
        $errors += "count:$($entry.content_type)"
    }
    if ($typeFields.Count -eq 0) {
        $errors += "empty-fields:$($entry.content_type)"
    }
    $schemaWidths += @($typeContent[0].PSObject.Properties).Count
}

foreach ($consumer in $codeConsumers) {
    if (-not (Test-Path -LiteralPath (Join-Path $projectRoot $consumer.source_path))) {
        $errors += "missing-code-consumer:$($consumer.source_path)"
    }
    if ($consumer.scope -notin @("content-type", "stable-id")) {
        $errors += "invalid-code-consumer-scope:$($consumer.content_type):$($consumer.scope)"
    }
    if ($consumer.scope -eq "stable-id" -and [string]::IsNullOrWhiteSpace($consumer.stable_id)) {
        $errors += "missing-code-consumer-id:$($consumer.content_type):$($consumer.source_path)"
    }
}

$recordKeys = @{}
foreach ($row in $content) {
    if ($recordKeys.ContainsKey($row.record_key)) {
        $errors += "duplicate-record:$($row.record_key)"
    } else {
        $recordKeys[$row.record_key] = $true
    }
    if (-not (Test-Path -LiteralPath (Join-Path $projectRoot $row.source_path))) {
        $errors += "missing-source:$($row.source_path)"
    }
    foreach ($fieldName in @(
        "stable_id",
        "existence_reason",
        "strategic_niche",
        "costs_and_risks",
        "comparison_group",
        "alternative_candidates",
        "removal_impact",
        "runtime_status",
        "runtime_evidence",
        "lifecycle_status",
        "rationale_evidence"
    )) {
        if ([string]::IsNullOrWhiteSpace($row.$fieldName)) {
            $errors += "empty:$($row.record_key):$fieldName"
        }
    }
}

$manualReview = @(Import-Csv (Join-Path $root "manual-review.csv"))
foreach ($row in $manualReview) {
    if ([string]::IsNullOrWhiteSpace($row.review_reason)) {
        $errors += "missing-review-reason:$($row.record_key)"
    }
}

$relationKeys = @{}
foreach ($relation in $relations) {
    $relationKey = @(
        $relation.source_record_key,
        $relation.field_path,
        $relation.kind,
        $relation.target_id,
        $relation.amount,
        $relation.duration
    ) -join "|"
    if ($relationKeys.ContainsKey($relationKey)) {
        $errors += "duplicate-relation:$relationKey"
    } else {
        $relationKeys[$relationKey] = $true
    }
    if ($relation.resolution_status -eq "resolved-content") {
        foreach ($target in ($relation.target_record_keys -split "; ")) {
            if ($target -and -not $recordKeys.ContainsKey($target)) {
                $errors += "missing-target:$target"
            }
        }
    }
}

$incomingGroups = $incoming | Group-Object target_record_key -AsHashTable -AsString
foreach ($row in $content) {
    $actual = if ($incomingGroups.ContainsKey($row.record_key)) {
        @($incomingGroups[$row.record_key]).Count
    } else {
        0
    }
    if ($actual -ne [int]$row.incoming_reference_count) {
        $errors += "incoming-count:$($row.record_key)"
    }
}

$enumChecks = @(
    @{ Path = "fields/items/generic-item.csv"; Field = "stockCategory"; Expected = 710 },
    @{ Path = "fields/characters-traits/character-trait.csv"; Field = "polarity"; Expected = 113 },
    @{ Path = "fields/characters-traits/character-trait.csv"; Field = "selectionRarity"; Expected = 113 },
    @{ Path = "fields/research-effects/research-project.csv"; Field = "field"; Expected = 180 },
    @{ Path = "fields/production-facilities/building.csv"; Field = "category"; Expected = 419 }
)
foreach ($check in $enumChecks) {
    $rows = @(
        Import-Csv (Join-Path $root $check.Path) |
            Where-Object field_path -eq $check.Field
    )
    $bad = @(
        $rows | Where-Object {
            [string]::IsNullOrWhiteSpace($_.enum_type) -or
            [string]::IsNullOrWhiteSpace($_.enum_label) -or
            [string]::IsNullOrWhiteSpace($_.value_origin)
        }
    )
    if ($rows.Count -ne $check.Expected -or $bad.Count -gt 0) {
        $errors += "enum:$($check.Path):$($check.Field):$($rows.Count):$($bad.Count)"
    }
}

$traitFields = Import-Csv (Join-Path $root "fields/characters-traits/character-trait.csv")
$implicitTraitDefaults = @(
    $traitFields | Where-Object {
        $_.field_path -eq "polarity" -and
        $_.value_origin -eq "implicit-csharp-default"
    }
)
if ($implicitTraitDefaults.Count -ne 13) {
    $errors += "trait-default-count:$($implicitTraitDefaults.Count)"
}

$trait = Import-Csv (Join-Path $root "csv/characters-traits/character-trait.csv") |
    Where-Object stable_id -eq "trait:101"
if (
    $trait.mechanics -notmatch "효과 2" -or
    $trait.mechanics -notmatch "정체성 규칙 2" -or
    $trait.mechanics -notmatch "유효 구형 보정 0" -or
    $trait.mechanics -notmatch "전투 능력 0" -or
    $trait.mechanics -notmatch "환경 보호 0"
) {
    $errors += "trait-semantics"
}

$eventKinds = @(
    Import-Csv (Join-Path $root "relations/events-campaign/life-event.csv") |
        Select-Object -ExpandProperty kind -Unique
)
if (
    $eventKinds -notcontains "grants-skill-experience" -or
    $eventKinds -notcontains "changes-mood" -or
    $eventKinds -contains "applies-effect"
) {
    $errors += "event-effect-semantics"
}

$researchUnlockKinds = @(
    Import-Csv (Join-Path $root "relations/research-effects/research-project.csv") |
        Where-Object kind -like "unlocks-*" |
        Select-Object -ExpandProperty kind -Unique
)
if (
    $researchUnlockKinds -notcontains "unlocks-building" -or
    $researchUnlockKinds -notcontains "unlocks-recipe"
) {
    $errors += "research-managed-reference-unlocks"
}

$linkFailures = @()
$linkPattern = '\[[^\]]*\]\(([^)]+)\)'
foreach ($file in Get-ChildItem $root -Recurse -File -Filter "*.md") {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($match in [regex]::Matches($text, $linkPattern)) {
        $target = $match.Groups[1].Value.Trim()
        if ($target -match '^(?:https?://|#|mailto:)') {
            continue
        }
        $target = $target.Split('#')[0]
        if (-not $target) {
            continue
        }
        $resolved = [IO.Path]::GetFullPath((Join-Path $file.DirectoryName $target))
        if (-not (Test-Path -LiteralPath $resolved)) {
            $linkFailures += "$($file.FullName):$target"
        }
    }
}
$errors += $linkFailures | ForEach-Object { "link:$_" }

$obsoleteMonoliths = @(
    @("content-master.csv", "content-relations.csv") |
        Where-Object { Test-Path (Join-Path $root $_) }
)
$summary = [ordered]@{
    type_count = $index.Count
    content_rows = $content.Count
    relation_rows = $relations.Count
    incoming_rows = $incoming.Count
    code_consumer_rows = $codeConsumers.Count
    validation_errors = $errors.Count
    markdown_link_errors = $linkFailures.Count
    implicit_trait_enum_defaults = $implicitTraitDefaults.Count
    min_type_columns = ($schemaWidths | Measure-Object -Minimum).Minimum
    max_type_columns = ($schemaWidths | Measure-Object -Maximum).Maximum
    parse_errors = @(Import-Csv (Join-Path $root "parse-errors.csv")).Count
    unresolved_content = @(Import-Csv (Join-Path $root "unresolved-references.csv")).Count
    manual_review_rows = $manualReview.Count
    duplicate_groups = @(Import-Csv (Join-Path $root "duplicate-content.csv")).Count
    obsolete_monoliths = $obsoleteMonoliths.Count
}

$summary | ConvertTo-Json
if ($errors.Count -gt 0) {
    $errors | Select-Object -First 30 | Write-Error
    throw "Content DB validation failed: $($errors.Count) error(s)."
}
