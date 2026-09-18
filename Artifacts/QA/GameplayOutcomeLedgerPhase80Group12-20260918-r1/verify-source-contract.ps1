$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$inventory = Import-Csv (Join-Path $root 'Artifacts\QA\GameplayOutcomeLedgerPhase80Inventory-20260918-r1\producer-inventory.csv')
$closure = Import-Csv (Join-Path $PSScriptRoot 'group12-closure.csv')
$records = @($inventory | Where-Object {
    $_.domain_group -in @('1', '2') -and $_.coverage_disposition -eq 'Record'
})

if ($records.Count -ne 42) {
    throw "Expected 42 group-1/2 Record rows, found $($records.Count)."
}
if ($closure.Count -ne 42) {
    throw "Expected 42 closure rows, found $($closure.Count)."
}
if (@($closure | Where-Object { $_.closure_status -ne 'blocked' }).Count -ne 0) {
    throw 'This artifact must not claim closure while the audited owner seams remain incomplete.'
}

$missing = @($records.type | Where-Object { $_ -notin $closure.type })
$extra = @($closure.type | Where-Object { $_ -notin $records.type })
if ($missing.Count -ne 0 -or $extra.Count -ne 0) {
    throw "Manifest inventory mismatch. missing=$($missing -join '|') extra=$($extra -join '|')"
}

$adapter = Get-Content -Raw (Join-Path $root 'Assets\Scripts\Services\Narrative\GameplayOutcomeBridges\ProductionCombat\ProductionCombatOutcomeAdapters.cs')
$receipt = Get-Content -Raw (Join-Path $root 'Assets\Scripts\Services\Narrative\GameplayOutcomeBridges\ProductionCombat\ProductionCombatOutcomeReceipts.cs')
$applier = Get-Content -Raw (Join-Path $root 'Assets\Scripts\Services\Combat\CombatCommandResultApplier.cs')
$production = Get-Content -Raw (Join-Path $root 'Assets\Scripts\Services\Economy\ProductionRecipeExecutionReceiptAuthority.cs')

if ($adapter -match 'GameplayParticipationKind\.Direct,\s*true\s*\)') {
    throw 'Production/combat adapter still uses the obsolete participant constructor without a display snapshot.'
}
if (($adapter -notmatch 'attack-operation') -or
    ($receipt -notmatch 'damage-type') -or
    ($receipt -notmatch 'body-part')) {
    throw 'Combat replay provenance/facts hardening is missing.'
}
if ($adapter -notmatch 'production-bill' -or $adapter -notmatch 'recipe-definition') {
    throw 'Production replay provenance hardening is missing.'
}
if ($applier -notmatch 'new SocialConflictEvent\([\s\S]*?calendar\.Day,\s*attackOperationId\)') {
    throw 'Derived social publication does not propagate the canonical attack operation ID.'
}

# These are deliberate closure blockers. Their disappearance means the
# manifest must be re-audited instead of silently continuing to say BLOCKED.
if ($applier -notmatch 'TryApplyUntrackedMechanical') {
    throw 'Direct-combat blocker changed; re-audit all miss/cover/wildlife paths.'
}
if ($production -notmatch 'TryCommitPreparedGameplayOutcome') {
    throw 'Production owner seam changed; re-audit exact receipt joint commit.'
}

Write-Output 'GROUP12_SOURCE_CONTRACT PASS_HONEST_BLOCKED records=42 integrated=0 substitutes=0 blocked=42'
