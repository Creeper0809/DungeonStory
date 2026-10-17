param([string]$LauncherPath = (Join-Path $PSScriptRoot 'run_isolated_unity_batch.ps1'))

$ErrorActionPreference = 'Stop'
$qaTokens = $null
$qaErrors = $null
$qaAst = [System.Management.Automation.Language.Parser]::ParseFile(
    (Resolve-Path -LiteralPath $LauncherPath), [ref]$qaTokens, [ref]$qaErrors)
if ($qaErrors.Count -ne 0) { throw 'Launcher syntax is invalid.' }
$qaPrefixes = @(
    'if ($ExecuteMethod -in @(''ArchitectureRefactorVerificationBatch.RunCore'', ''ArchitectureRefactorVerificationBatch.RunCampaign'', ''ArchitectureRefactorVerificationBatch.RunProductionBills'', ''ArchitectureRefactorVerificationBatch.RunBuildings'', ''ArchitectureRefactorVerificationBatch.RunDefense'', ''ArchitectureRefactorVerificationBatch.RunBuildingVisual'', ''ArchitectureRefactorVerificationBatch.RunCropPersistence'', ''ArchitectureRefactorVerificationBatch.RunCharacterDefinitions'', ''ArchitectureRefactorVerificationBatch.RunEquipmentEvolution'', ''ArchitectureRefactorVerificationBatch.RunConsumablePolicies'', ''ArchitectureRefactorVerificationBatch.RunConsumableExecution''))',
    'if ($ExecuteMethod -in @(''MovementOwnershipDebugScenarios.RequestActualContainerForBatch'', ''MovementOwnershipDebugScenarios.RequestDestinationlessForBatch'', ''MovementOwnershipDebugScenarios.RequestDeprivationForBatch'', ''MovementOwnershipDebugScenarios.RequestPrimitiveForBatch'', ''MovementOwnershipDebugScenarios.RequestRestockForBatch'', ''MovementOwnershipDebugScenarios.RequestMedicalTransferForBatch'', ''MovementOwnershipDebugScenarios.RequestWorkAccidentForBatch'', ''MovementOwnershipDebugScenarios.RequestFactionReinforcementForBatch'', ''MovementOwnershipDebugScenarios.RequestSkillTargetingForBatch'')) {',
    'if ($ExecuteMethod -eq ''GameWorldLineageDebugScenarios.RequestCharacterRestoreLifetimeForBatch'') {',
    'if ($ExecuteMethod -eq ''GameWorldLineageDebugScenarios.RequestRestoreOutcomeForBatch'') {',
    'if ($ExecuteMethod -eq ''PhysicalItemLogisticsPlayModeVerifier.RequestDestructiveDrainPreparedOutputForBatch'') {',
    'if ($Mode -ne ''edit'' -and $ExecuteMethod -notin @(',
    'if ($PSBoundParameters.ContainsKey(''SerializedStartup''))'
)
$qaGuardTexts = foreach ($qaPrefix in $qaPrefixes) {
    $qaMatches = @($qaAst.FindAll({
        param($qaNode)
        $qaNode -is [System.Management.Automation.Language.IfStatementAst] -and
            $qaNode.Extent.Text.StartsWith($qaPrefix, [StringComparison]::Ordinal)
    }, $true))
    if ($qaMatches.Count -ne 1) { throw "Expected one production guard: $qaPrefix" }
    $qaMatches[0].Extent.Text
}
# Execute only the selected real admission guards, before any filesystem/native/Unity
# work in the launcher. Automatic PSBoundParameters comes from this param block.
$qaGuardParameters = @'
param(
    [string]$ExecuteMethod, [string]$OutputDirectory, [string]$Mode = 'edit',
    [string]$UnityExecutable, [switch]$SerializedStartup,
    [switch]$U0T01FacilityShop, [switch]$U0T02Fulfillment,
    [switch]$U0E09EnvironmentExposure, [switch]$U0E11Capture,
    [string]$CampaignResumeWorld
)
'@
$qaGuardFunction = [ScriptBlock]::Create($qaGuardParameters + "`n" + ($qaGuardTexts -join "`n"))
$qaCore = 'ArchitectureRefactorVerificationBatch.RunCore'
$qaCampaign = 'ArchitectureRefactorVerificationBatch.RunCampaign'
$qaProduction = 'ArchitectureRefactorVerificationBatch.RunProductionBills'
$qaBuildings = 'ArchitectureRefactorVerificationBatch.RunBuildings'
$qaDefense = 'ArchitectureRefactorVerificationBatch.RunDefense'
$qaBuildingVisual = 'ArchitectureRefactorVerificationBatch.RunBuildingVisual'
$qaCropPersistence = 'ArchitectureRefactorVerificationBatch.RunCropPersistence'
$qaCharacterDefinitions = 'ArchitectureRefactorVerificationBatch.RunCharacterDefinitions'
$qaEquipmentEvolution = 'ArchitectureRefactorVerificationBatch.RunEquipmentEvolution'
$qaConsumablePolicies = 'ArchitectureRefactorVerificationBatch.RunConsumablePolicies'
$qaConsumableExecution = 'ArchitectureRefactorVerificationBatch.RunConsumableExecution'
$qaMovementOwnership = 'MovementOwnershipDebugScenarios.RequestActualContainerForBatch'
$qaMovementDestinationless = 'MovementOwnershipDebugScenarios.RequestDestinationlessForBatch'
$qaMovementDeprivation = 'MovementOwnershipDebugScenarios.RequestDeprivationForBatch'
$qaMovementPrimitive = 'MovementOwnershipDebugScenarios.RequestPrimitiveForBatch'
$qaMovementRestock = 'MovementOwnershipDebugScenarios.RequestRestockForBatch'
$qaMovementMedicalTransfer = 'MovementOwnershipDebugScenarios.RequestMedicalTransferForBatch'
$qaMovementWorkAccident = 'MovementOwnershipDebugScenarios.RequestWorkAccidentForBatch'
$qaMovementFactionReinforcement = 'MovementOwnershipDebugScenarios.RequestFactionReinforcementForBatch'
$qaMovementSkillTargeting = 'MovementOwnershipDebugScenarios.RequestSkillTargetingForBatch'
$qaWorld = 'GameWorldLineageDebugScenarios.RequestActualContainerForBatch'
$qaCharacterRestoreLifetime = 'GameWorldLineageDebugScenarios.RequestCharacterRestoreLifetimeForBatch'
$qaRestoreOutcome = 'GameWorldLineageDebugScenarios.RequestRestoreOutcomeForBatch'
$qaPhysicalDestructiveDrain = 'PhysicalItemLogisticsPlayModeVerifier.RequestDestructiveDrainPreparedOutputForBatch'
$qaCrop = 'EnvironmentCropProductionEntryDebugScenarios.RunCropCommandObserversFromBatch'
$qaCases = @(
    @{ Name='movement plain prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaMovementOwnership+'Extra'); Mode='play' } },
    @{ Name='movement campaign'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementOwnership; Mode='campaign' } },
    @{ Name='unknown plain play'; Allowed=$false; Parameters=@{ ExecuteMethod='UnknownVerification.Request'; Mode='play' } },
    @{ Name='existing world plain play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaWorld; Mode='play' } },
    @{ Name='character restore lifetime plain play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaCharacterRestoreLifetime; Mode='play' } },
    @{ Name='character restore lifetime serialized play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaCharacterRestoreLifetime; Mode='play'; SerializedStartup=$true } },
    @{ Name='character restore lifetime wrong edit'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaCharacterRestoreLifetime; Mode='edit' } },
    @{ Name='character restore lifetime campaign'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaCharacterRestoreLifetime; Mode='campaign' } },
    @{ Name='character restore lifetime selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaCharacterRestoreLifetime; Mode='play'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='character restore lifetime resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaCharacterRestoreLifetime; Mode='play'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='character restore lifetime prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaCharacterRestoreLifetime+'Unexpected'); Mode='play'; SerializedStartup=$true } },
    @{ Name='restore outcome plain play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaRestoreOutcome; Mode='play' } },
    @{ Name='restore outcome serialized play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaRestoreOutcome; Mode='play'; SerializedStartup=$true } },
    @{ Name='restore outcome wrong edit'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaRestoreOutcome; Mode='edit' } },
    @{ Name='restore outcome campaign'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaRestoreOutcome; Mode='campaign' } },
    @{ Name='restore outcome selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaRestoreOutcome; Mode='play'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='restore outcome resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaRestoreOutcome; Mode='play'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='restore outcome prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaRestoreOutcome+'Unexpected'); Mode='play'; SerializedStartup=$true } },
    @{ Name='destructive drain plain play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaPhysicalDestructiveDrain; Mode='play' } },
    @{ Name='destructive drain serialized play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaPhysicalDestructiveDrain; Mode='play'; SerializedStartup=$true } },
    @{ Name='destructive drain wrong edit'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaPhysicalDestructiveDrain; Mode='edit' } },
    @{ Name='destructive drain serialized edit'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaPhysicalDestructiveDrain; Mode='edit'; SerializedStartup=$true } },
    @{ Name='destructive drain campaign'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaPhysicalDestructiveDrain; Mode='campaign' } },
    @{ Name='destructive drain selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaPhysicalDestructiveDrain; Mode='play'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='destructive drain resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaPhysicalDestructiveDrain; Mode='play'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='destructive drain prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaPhysicalDestructiveDrain+'Unexpected'); Mode='play'; SerializedStartup=$true } },
    @{ Name='movement regular play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaMovementOwnership; Mode='play' } },
    @{ Name='movement serialized play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaMovementOwnership; Mode='play'; SerializedStartup=$true } },
    @{ Name='movement wrong edit'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementOwnership; Mode='edit' } },
    @{ Name='movement serialized edit'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementOwnership; Mode='edit'; SerializedStartup=$true } },
    @{ Name='movement selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementOwnership; Mode='play'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='movement resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementOwnership; Mode='play'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='movement prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaMovementOwnership+'Extra'); Mode='play'; SerializedStartup=$true } },
    @{ Name='destinationless plain prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaMovementDestinationless+'Extra'); Mode='play' } },
    @{ Name='destinationless campaign'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementDestinationless; Mode='campaign' } },
    @{ Name='destinationless regular play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaMovementDestinationless; Mode='play' } },
    @{ Name='destinationless serialized play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaMovementDestinationless; Mode='play'; SerializedStartup=$true } },
    @{ Name='destinationless wrong edit'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementDestinationless; Mode='edit' } },
    @{ Name='destinationless serialized edit'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementDestinationless; Mode='edit'; SerializedStartup=$true } },
    @{ Name='destinationless selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementDestinationless; Mode='play'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='destinationless resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementDestinationless; Mode='play'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='destinationless prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaMovementDestinationless+'Extra'); Mode='play'; SerializedStartup=$true } },
    @{ Name='deprivation plain prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaMovementDeprivation+'Extra'); Mode='play' } },
    @{ Name='deprivation campaign'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementDeprivation; Mode='campaign' } },
    @{ Name='deprivation regular play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaMovementDeprivation; Mode='play' } },
    @{ Name='deprivation serialized play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaMovementDeprivation; Mode='play'; SerializedStartup=$true } },
    @{ Name='deprivation wrong edit'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementDeprivation; Mode='edit' } },
    @{ Name='deprivation serialized edit'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementDeprivation; Mode='edit'; SerializedStartup=$true } },
    @{ Name='deprivation selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementDeprivation; Mode='play'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='deprivation resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementDeprivation; Mode='play'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='deprivation prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaMovementDeprivation+'Extra'); Mode='play'; SerializedStartup=$true } },
    @{ Name='primitive plain prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaMovementPrimitive+'Extra'); Mode='play' } },
    @{ Name='primitive campaign'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementPrimitive; Mode='campaign' } },
    @{ Name='primitive regular play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaMovementPrimitive; Mode='play' } },
    @{ Name='primitive serialized play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaMovementPrimitive; Mode='play'; SerializedStartup=$true } },
    @{ Name='primitive wrong edit'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementPrimitive; Mode='edit' } },
    @{ Name='primitive serialized edit'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementPrimitive; Mode='edit'; SerializedStartup=$true } },
    @{ Name='primitive selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementPrimitive; Mode='play'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='primitive resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementPrimitive; Mode='play'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='primitive prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaMovementPrimitive+'Extra'); Mode='play'; SerializedStartup=$true } },
    @{ Name='restock plain prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaMovementRestock+'Extra'); Mode='play' } },
    @{ Name='restock campaign'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementRestock; Mode='campaign' } },
    @{ Name='restock regular play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaMovementRestock; Mode='play' } },
    @{ Name='restock serialized play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaMovementRestock; Mode='play'; SerializedStartup=$true } },
    @{ Name='restock wrong edit'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementRestock; Mode='edit' } },
    @{ Name='restock serialized edit'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementRestock; Mode='edit'; SerializedStartup=$true } },
    @{ Name='restock selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementRestock; Mode='play'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='restock resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementRestock; Mode='play'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='restock prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaMovementRestock+'Extra'); Mode='play'; SerializedStartup=$true } },
    @{ Name='medical transfer plain prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaMovementMedicalTransfer+'Extra'); Mode='play' } },
    @{ Name='medical transfer campaign'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementMedicalTransfer; Mode='campaign' } },
    @{ Name='medical transfer regular play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaMovementMedicalTransfer; Mode='play' } },
    @{ Name='medical transfer serialized play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaMovementMedicalTransfer; Mode='play'; SerializedStartup=$true } },
    @{ Name='medical transfer wrong edit'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementMedicalTransfer; Mode='edit' } },
    @{ Name='medical transfer serialized edit'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementMedicalTransfer; Mode='edit'; SerializedStartup=$true } },
    @{ Name='medical transfer selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementMedicalTransfer; Mode='play'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='medical transfer resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementMedicalTransfer; Mode='play'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='medical transfer prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaMovementMedicalTransfer+'Extra'); Mode='play'; SerializedStartup=$true } },
    @{ Name='work accident plain prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaMovementWorkAccident+'Extra'); Mode='play' } },
    @{ Name='work accident campaign'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementWorkAccident; Mode='campaign' } },
    @{ Name='work accident regular play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaMovementWorkAccident; Mode='play' } },
    @{ Name='work accident serialized play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaMovementWorkAccident; Mode='play'; SerializedStartup=$true } },
    @{ Name='work accident wrong edit'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementWorkAccident; Mode='edit' } },
    @{ Name='work accident serialized edit'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementWorkAccident; Mode='edit'; SerializedStartup=$true } },
    @{ Name='work accident selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementWorkAccident; Mode='play'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='work accident resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementWorkAccident; Mode='play'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='work accident prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaMovementWorkAccident+'Extra'); Mode='play'; SerializedStartup=$true } },
    @{ Name='faction reinforcement plain prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaMovementFactionReinforcement+'Extra'); Mode='play' } },
    @{ Name='faction reinforcement campaign'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementFactionReinforcement; Mode='campaign' } },
    @{ Name='faction reinforcement regular play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaMovementFactionReinforcement; Mode='play' } },
    @{ Name='faction reinforcement serialized play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaMovementFactionReinforcement; Mode='play'; SerializedStartup=$true } },
    @{ Name='faction reinforcement wrong edit'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementFactionReinforcement; Mode='edit' } },
    @{ Name='faction reinforcement serialized edit'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementFactionReinforcement; Mode='edit'; SerializedStartup=$true } },
    @{ Name='faction reinforcement selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementFactionReinforcement; Mode='play'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='faction reinforcement resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementFactionReinforcement; Mode='play'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='faction reinforcement prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaMovementFactionReinforcement+'Extra'); Mode='play'; SerializedStartup=$true } },
    @{ Name='skill targeting plain prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaMovementSkillTargeting+'Extra'); Mode='play' } },
    @{ Name='skill targeting campaign'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementSkillTargeting; Mode='campaign' } },
    @{ Name='skill targeting regular play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaMovementSkillTargeting; Mode='play' } },
    @{ Name='skill targeting serialized play'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaMovementSkillTargeting; Mode='play'; SerializedStartup=$true } },
    @{ Name='skill targeting wrong edit'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementSkillTargeting; Mode='edit' } },
    @{ Name='skill targeting serialized edit'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementSkillTargeting; Mode='edit'; SerializedStartup=$true } },
    @{ Name='skill targeting selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementSkillTargeting; Mode='play'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='skill targeting resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaMovementSkillTargeting; Mode='play'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='skill targeting prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaMovementSkillTargeting+'Extra'); Mode='play'; SerializedStartup=$true } },
    @{ Name='core regular'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaCore; Mode='edit' } },
    @{ Name='core serialized'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaCore; Mode='edit'; SerializedStartup=$true } },
    @{ Name='core explicit executable'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaCore; Mode='edit'; SerializedStartup=$true; UnityExecutable='owned-unity-path' } },
    @{ Name='core play'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaCore; Mode='play' } },
    @{ Name='core serialized play'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaCore; Mode='play'; SerializedStartup=$true } },
    @{ Name='core selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaCore; Mode='edit'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='core resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaCore; Mode='edit'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='core prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaCore+'Extra'); Mode='edit'; SerializedStartup=$true } },
    @{ Name='campaign regular'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaCampaign; Mode='edit' } },
    @{ Name='campaign serialized'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaCampaign; Mode='edit'; SerializedStartup=$true } },
    @{ Name='campaign play'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaCampaign; Mode='play' } },
    @{ Name='campaign serialized play'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaCampaign; Mode='play'; SerializedStartup=$true } },
    @{ Name='campaign selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaCampaign; Mode='edit'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='campaign resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaCampaign; Mode='edit'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='campaign prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaCampaign+'Extra'); Mode='edit'; SerializedStartup=$true } },
    @{ Name='production regular'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaProduction; Mode='edit' } },
    @{ Name='production serialized'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaProduction; Mode='edit'; SerializedStartup=$true } },
    @{ Name='production play'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaProduction; Mode='play' } },
    @{ Name='production serialized play'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaProduction; Mode='play'; SerializedStartup=$true } },
    @{ Name='production selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaProduction; Mode='edit'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='production resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaProduction; Mode='edit'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='production prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaProduction+'Extra'); Mode='edit'; SerializedStartup=$true } },
    @{ Name='buildings regular'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaBuildings; Mode='edit' } },
    @{ Name='buildings serialized'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaBuildings; Mode='edit'; SerializedStartup=$true } },
    @{ Name='buildings play'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaBuildings; Mode='play' } },
    @{ Name='buildings serialized play'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaBuildings; Mode='play'; SerializedStartup=$true } },
    @{ Name='buildings selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaBuildings; Mode='edit'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='buildings resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaBuildings; Mode='edit'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='buildings prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaBuildings+'Extra'); Mode='edit'; SerializedStartup=$true } },
    @{ Name='defense regular'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaDefense; Mode='edit' } },
    @{ Name='defense serialized'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaDefense; Mode='edit'; SerializedStartup=$true } },
    @{ Name='defense play'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaDefense; Mode='play' } },
    @{ Name='defense serialized play'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaDefense; Mode='play'; SerializedStartup=$true } },
    @{ Name='defense selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaDefense; Mode='edit'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='defense resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaDefense; Mode='edit'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='defense prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaDefense+'Extra'); Mode='edit'; SerializedStartup=$true } },
    @{ Name='building visual regular'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaBuildingVisual; Mode='edit' } },
    @{ Name='building visual serialized'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaBuildingVisual; Mode='edit'; SerializedStartup=$true } },
    @{ Name='building visual play'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaBuildingVisual; Mode='play' } },
    @{ Name='building visual serialized play'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaBuildingVisual; Mode='play'; SerializedStartup=$true } },
    @{ Name='building visual selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaBuildingVisual; Mode='edit'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='building visual resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaBuildingVisual; Mode='edit'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='building visual prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaBuildingVisual+'Extra'); Mode='edit'; SerializedStartup=$true } },
    @{ Name='crop persistence regular'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaCropPersistence; Mode='edit' } },
    @{ Name='crop persistence serialized'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaCropPersistence; Mode='edit'; SerializedStartup=$true } },
    @{ Name='crop persistence play'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaCropPersistence; Mode='play' } },
    @{ Name='crop persistence serialized play'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaCropPersistence; Mode='play'; SerializedStartup=$true } },
    @{ Name='crop persistence selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaCropPersistence; Mode='edit'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='crop persistence resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaCropPersistence; Mode='edit'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='crop persistence prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaCropPersistence+'Extra'); Mode='edit'; SerializedStartup=$true } },
    @{ Name='character definitions regular'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaCharacterDefinitions; Mode='edit' } },
    @{ Name='character definitions serialized'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaCharacterDefinitions; Mode='edit'; SerializedStartup=$true } },
    @{ Name='character definitions play'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaCharacterDefinitions; Mode='play' } },
    @{ Name='character definitions serialized play'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaCharacterDefinitions; Mode='play'; SerializedStartup=$true } },
    @{ Name='character definitions selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaCharacterDefinitions; Mode='edit'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='character definitions resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaCharacterDefinitions; Mode='edit'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='character definitions prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaCharacterDefinitions+'Extra'); Mode='edit'; SerializedStartup=$true } },
    @{ Name='equipment evolution regular'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaEquipmentEvolution; Mode='edit' } },
    @{ Name='equipment evolution serialized'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaEquipmentEvolution; Mode='edit'; SerializedStartup=$true } },
    @{ Name='equipment evolution play'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaEquipmentEvolution; Mode='play' } },
    @{ Name='equipment evolution serialized play'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaEquipmentEvolution; Mode='play'; SerializedStartup=$true } },
    @{ Name='equipment evolution selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaEquipmentEvolution; Mode='edit'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='equipment evolution resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaEquipmentEvolution; Mode='edit'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='equipment evolution prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaEquipmentEvolution+'Extra'); Mode='edit'; SerializedStartup=$true } },
    @{ Name='consumable policies regular'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaConsumablePolicies; Mode='edit' } },
    @{ Name='consumable policies serialized'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaConsumablePolicies; Mode='edit'; SerializedStartup=$true } },
    @{ Name='consumable policies play'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaConsumablePolicies; Mode='play' } },
    @{ Name='consumable policies serialized play'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaConsumablePolicies; Mode='play'; SerializedStartup=$true } },
    @{ Name='consumable policies selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaConsumablePolicies; Mode='edit'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='consumable policies resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaConsumablePolicies; Mode='edit'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='consumable policies prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaConsumablePolicies+'Extra'); Mode='edit'; SerializedStartup=$true } },
    @{ Name='consumable execution regular'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaConsumableExecution; Mode='edit' } },
    @{ Name='consumable execution serialized'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaConsumableExecution; Mode='edit'; SerializedStartup=$true } },
    @{ Name='consumable execution play'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaConsumableExecution; Mode='play' } },
    @{ Name='consumable execution serialized play'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaConsumableExecution; Mode='play'; SerializedStartup=$true } },
    @{ Name='consumable execution selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaConsumableExecution; Mode='edit'; SerializedStartup=$true; U0E11Capture=$true } },
    @{ Name='consumable execution resume'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaConsumableExecution; Mode='edit'; SerializedStartup=$true; CampaignResumeWorld='unread-resume-path' } },
    @{ Name='consumable execution prefix'; Allowed=$false; Parameters=@{ ExecuteMethod=($qaConsumableExecution+'Extra'); Mode='edit'; SerializedStartup=$true } },
    @{ Name='unowned entry'; Allowed=$false; Parameters=@{ ExecuteMethod='Unknown.Run'; Mode='edit'; SerializedStartup=$true } },
    @{ Name='existing world'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaWorld; Mode='play'; SerializedStartup=$true } },
    @{ Name='world wrong mode'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaWorld; Mode='edit'; SerializedStartup=$true } },
    @{ Name='existing crop'; Allowed=$true; Parameters=@{ ExecuteMethod=$qaCrop; Mode='edit'; SerializedStartup=$true } },
    @{ Name='crop extra selector'; Allowed=$false; Parameters=@{ ExecuteMethod=$qaCrop; Mode='edit'; SerializedStartup=$true; U0T02Fulfillment=$true } }
)
foreach ($qaCase in $qaCases) {
    $qaParameters = $qaCase.Parameters.Clone()
    $qaParameters.OutputDirectory = 'not-accessed-by-pure-guard-test'
    $qaAccepted = $true
    try { & $qaGuardFunction @qaParameters }
    catch { $qaAccepted = $false }
    if ($qaAccepted -ne $qaCase.Allowed) { throw "Startup guard mismatch: $($qaCase.Name)" }
}
Write-Output "ARCHITECTURE_CORE_STARTUP_GUARDS=PASS; cases=$($qaCases.Count); native/Unity execution=NONE"
