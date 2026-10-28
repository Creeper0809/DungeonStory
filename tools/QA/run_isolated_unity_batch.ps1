param(
    [Parameter(Mandatory=$true)][string]$ExecuteMethod,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [ValidateSet('edit', 'play', 'campaign')][string]$Mode = 'edit',
    [string]$UnityExecutable,
    [switch]$SerializedStartup,
    [string]$CampaignResumeWorld,
    [string]$CampaignResumeSha256,
    [switch]$FacilityContinuation,
    [switch]$FacilityContinuationRestRequest,
    [switch]$MedicalContinuation,
    [switch]$ShopContinuation,
    [switch]$W01CheckoutCleanup,
    [switch]$MealContinuation,
    [switch]$DrinkContinuation,
    [switch]$PersistentCookPause,
    [switch]$EEC03DriftCargo,
    [switch]$EEC05Apparel,
    [switch]$W05HarvestIdentity,
    [switch]$W05SurgeryIdentity,
    [switch]$W01SurgeryPublicationRecovery,
    [switch]$W01SurgeryClinicalFailure,
    [switch]$W01WorkTerminalJoin,
    [switch]$W01FireIdentity,
    [switch]$W01FireWaterIdentity,
    [switch]$W03MedicalLabor,
    [switch]$W04RepairLive,
    [switch]$W06ResourceCausality,
    [switch]$U0E01RegistryInventory,
    [switch]$U0E02HusbandryOutcome,
    [switch]$U0E03CropTreatment,
    [switch]$U0E06OperatingDay,
    [switch]$U0E07StockPolicySale,
    [switch]$U0E08CropAction,
    [switch]$U0E09EnvironmentExposure,
    [switch]$U0E10WorldResource,
    [switch]$U0E11Wildlife,
    [switch]$U0E11Capture,
    [switch]$U0E11Role,
    [switch]$U0E11Butchery,
    [switch]$U0E12Infrastructure,
    [switch]$U0E13Automation,
    [switch]$U0T01FacilityShop,
    [switch]$U0T02Fulfillment,
    [switch]$U0T03Retail,
    [switch]$U0T04RetailRecovery,
    [switch]$U0T07RegionalSupply,
    [switch]$U0D01CombatEvidence,
    [switch]$U0D02SharedCombat,
    [switch]$U0D03CombatCausal,
    [switch]$U0A03VisitorExit,
    [switch]$U0A04VandalismMacro,
    [switch]$U0A04VandalismBreakdown,
    [switch]$U0A04CorpseEating,
    [switch]$U0A09Deprivation,
    [switch]$U0A09SafeDrink,
    [switch]$U0EEC01CropFailure,
    [switch]$U0EEC02WildlifeArrival,
    [switch]$U0EEC06CombatCraft,
    [switch]$U0G01RecruitDelivery,
    [switch]$U0G02EmploymentSettlement,
    [switch]$U0G03PolicyRevision,
    [switch]$U0G04Application,
    [switch]$U0G06MilestonePressure,
    [switch]$U0G07KnowledgeCompletion,
    [switch]$U0G08Recovery,
    [switch]$U0G09ExternalInfluence,
    [switch]$U0G10StaffDiscontent,
    [switch]$U0G11DurableEquipment,
    [switch]$U0G12GolemUpkeep,
    [switch]$U0G14TraitAnalysis,
    [switch]$U0C01O02FuneralLineage,
    [switch]$U0C03ProductionLoss,
    [switch]$U0C04O05CareerObservation,
    [switch]$U0CN01AmbitionTransition,
    [switch]$U0CR01FamilyPlanning,
    [switch]$U0CR02R05Reproduction,
    [switch]$U0CR07Birth,
    [switch]$U0CR09Household,
    [switch]$U0CA01AgeAnatomy,
    [switch]$U0CA02DiseaseAnatomy,
    [switch]$TerminalDeathSave,
    [switch]$U0UI01DefenseService,
    [switch]$U0UI02ResidentEvacuation,
    [switch]$U0CON01ConsumablesPolicy,
    [switch]$U0BDCS01SpaceExpansion,
    [switch]$U0BDCD01DoorPolicy,
    [switch]$U0BDCD01DoorTraversal,
    [switch]$SourceV9ActualModelAuthoring,
    [switch]$SourceV9AuthorshipContract,
    [switch]$SourceV9SkillIntake,
    [switch]$SourceV9SkillExpeditionActor,
    [ValidateRange(1,2147483647)][int]$SourceV9InitialSeed,
    [switch]$SourceV9EquipmentIntake,
    [switch]$SourceV9FacilityIntake,
    [switch]$SourceV9EquipmentReplay,
    [switch]$SourceV9SkillReplay,
    [switch]$SkillReplayHoldNaturalInvasion,
    [switch]$SkillNormalWeaponPreparation,
    [switch]$SkillNonTargetHungerFixture,
    [switch]$SkillEquipmentPreparationFixture,
    [switch]$SkillPreparationEnvironmentFixture,
    [switch]$SkillPreparationInstantWorkFixture,
    [switch]$SkillThirtyFoodPreparationFixture,
    [switch]$SkillRecruitmentHistoryCapacityFixture,
    [switch]$SkillOneCraftingBenchFixture,
    [string]$SkillBattleActorId,
    [string]$SkillBattleSkillId,
    [int]$SourceV9SkillCandidateIndex = -1,
    [switch]$SourceV9TraitReplay,
    [switch]$SourceV9FacilityReplay,
    [string]$SourceV9Inventory,
    [string]$SourceV9MechanicsManifest,
    [string]$SourceV9PackageId,
    [string]$SourceV9ReplayPackage,
    [string]$SourceV9ReplayResponse,
    [string]$SourceV9AuthoringSource,
    [string]$SourceV9AuthoringManifestSha256,
    [string]$SourceV9AuthoringProfile
)
$ErrorActionPreference = 'Stop'
if ($ExecuteMethod -eq 'V20CampaignDebugScenarios.RequestWim044IsolatedBatch') {
    $qaCareerAllowedParameters = @('ExecuteMethod', 'OutputDirectory', 'Mode', 'UnityExecutable')
    if ($Mode -ne 'play' -or @($PSBoundParameters.Keys | Where-Object {
        $_ -notin $qaCareerAllowedParameters
    }).Count -ne 0) {
        throw 'WIM044 requires its dedicated fresh PlayMode entry without another selector or resume input.'
    }
}
if ($ExecuteMethod -eq 'ObserverCompositionClosureDebugScenarios.RequestActualContainerForBatch') {
    $qaCompositionParameters = @('ExecuteMethod','OutputDirectory','Mode','UnityExecutable','SerializedStartup')
    if ($Mode -ne 'play' -or @($PSBoundParameters.Keys | Where-Object { $_ -notin $qaCompositionParameters }).Count -ne 0) {
        throw 'Composition closure requires its dedicated fresh PlayMode entry without selectors or resume inputs.'
    }
}
if ($ExecuteMethod -eq 'GameWorldLineageDebugScenarios.RequestActualContainerForBatch') {
    $qaGameWorldLineageParameters = @('ExecuteMethod','OutputDirectory','Mode','UnityExecutable','SerializedStartup')
    if ($Mode -ne 'play' -or @($PSBoundParameters.Keys | Where-Object { $_ -notin $qaGameWorldLineageParameters }).Count -ne 0) {
        throw 'Game-world lineage requires its dedicated fresh PlayMode entry without selectors or resume inputs.'
    }
}
if ($ExecuteMethod -eq 'GameWorldLineageDebugScenarios.RequestCharacterRestoreLifetimeForBatch') {
    $qaCharacterRestoreLifetimeParameters = @('ExecuteMethod','OutputDirectory','Mode','UnityExecutable','SerializedStartup')
    if ($Mode -ne 'play' -or @($PSBoundParameters.Keys | Where-Object { $_ -notin $qaCharacterRestoreLifetimeParameters }).Count -ne 0) {
        throw 'Character restore lifetime requires its dedicated fresh PlayMode entry without selectors or resume inputs.'
    }
}
if ($ExecuteMethod -eq 'GameWorldLineageDebugScenarios.RequestRestoreOutcomeForBatch') {
    $qaRestoreOutcomeParameters = @('ExecuteMethod','OutputDirectory','Mode','UnityExecutable','SerializedStartup')
    if ($Mode -ne 'play' -or @($PSBoundParameters.Keys | Where-Object { $_ -notin $qaRestoreOutcomeParameters }).Count -ne 0) {
        throw 'Restore outcome requires its dedicated fresh PlayMode entry without selectors or resume inputs.'
    }
}
if ($ExecuteMethod -in @('MovementOwnershipDebugScenarios.RequestActualContainerForBatch', 'MovementOwnershipDebugScenarios.RequestDestinationlessForBatch', 'MovementOwnershipDebugScenarios.RequestDeprivationForBatch', 'MovementOwnershipDebugScenarios.RequestPrimitiveForBatch', 'MovementOwnershipDebugScenarios.RequestRestockForBatch', 'MovementOwnershipDebugScenarios.RequestMedicalTransferForBatch', 'MovementOwnershipDebugScenarios.RequestWorkAccidentForBatch', 'MovementOwnershipDebugScenarios.RequestFactionReinforcementForBatch', 'MovementOwnershipDebugScenarios.RequestSkillTargetingForBatch')) {
    $qaMovementOwnershipParameters = @('ExecuteMethod','OutputDirectory','Mode','UnityExecutable','SerializedStartup')
    if ($Mode -ne 'play' -or @($PSBoundParameters.Keys | Where-Object { $_ -notin $qaMovementOwnershipParameters }).Count -ne 0) {
        throw 'Movement ownership requires its dedicated fresh PlayMode entry without selectors or resume inputs.'
    }
}
if ($ExecuteMethod -eq 'PhysicalItemLogisticsPlayModeVerifier.RequestDestructiveDrainPreparedOutputForBatch') {
    $qaDestructiveDrainParameters = @('ExecuteMethod','OutputDirectory','Mode','UnityExecutable','SerializedStartup')
    if ($Mode -ne 'play' -or @($PSBoundParameters.Keys | Where-Object { $_ -notin $qaDestructiveDrainParameters }).Count -ne 0) {
        throw 'Physical destructive drain requires its dedicated fresh PlayMode entry without selectors or resume inputs.'
    }
}
if ($ExecuteMethod -eq 'NarrativeContextLifetimeDebugScenarios.RequestActualContainerForBatch') {
    $qaNarrativeContextLifetimeParameters = @('ExecuteMethod','OutputDirectory','Mode','UnityExecutable','SerializedStartup')
    if ($Mode -ne 'play' -or @($PSBoundParameters.Keys | Where-Object { $_ -notin $qaNarrativeContextLifetimeParameters }).Count -ne 0) {
        throw 'Narrative context lifetime requires its dedicated fresh PlayMode entry without selectors or resume inputs.'
    }
}
if ($ExecuteMethod -in @('ArchitectureRefactorVerificationBatch.RunCore', 'ArchitectureRefactorVerificationBatch.RunCampaign', 'ArchitectureRefactorVerificationBatch.RunProductionBills', 'ArchitectureRefactorVerificationBatch.RunBuildings', 'ArchitectureRefactorVerificationBatch.RunDefense', 'ArchitectureRefactorVerificationBatch.RunBuildingVisual', 'ArchitectureRefactorVerificationBatch.RunCropPersistence', 'ArchitectureRefactorVerificationBatch.RunCharacterDefinitions', 'ArchitectureRefactorVerificationBatch.RunEquipmentEvolution', 'ArchitectureRefactorVerificationBatch.RunConsumablePolicies', 'ArchitectureRefactorVerificationBatch.RunConsumableExecution')) {
    $qaArchitectureCoreParameters = @('ExecuteMethod','OutputDirectory','Mode','UnityExecutable','SerializedStartup')
    if ($Mode -ne 'edit' -or @($PSBoundParameters.Keys | Where-Object { $_ -notin $qaArchitectureCoreParameters }).Count -ne 0) {
        throw 'Architecture verification requires its dedicated edit-mode entry without selectors or resume inputs.'
    }
}
$qaFocusedRegressionEditEntries = @(
    'R29StructureRemediationBatch.RunContracts',
    'R26SoRemediationBatch.CapturePublicationCandidate',
    'R26SoRemediationBatch.RunAll',
    'V20ContentResolutionRecoveryDebugScenarios.RunPrioritizedEffectBoundaries',
    'V27CiEvidenceBatch.RunImpactEffectContracts',
    'V27CiEvidenceBatch.RunImpactCoreContracts',
    'V27CiEvidenceBatch.RunImpactCharacterContracts')
$qaFocusedRegressionPlayEntries = @(
    'V27CiEvidenceBatch.RunImpactWorldRestore',
    'V27CiEvidenceBatch.RunImpactCrops')
if ($ExecuteMethod -in @($qaFocusedRegressionEditEntries + $qaFocusedRegressionPlayEntries)) {
    $qaFocusedRegressionParameters = @('ExecuteMethod','OutputDirectory','Mode','UnityExecutable','SerializedStartup')
    $qaFocusedRegressionExpectedMode = if ($ExecuteMethod -in $qaFocusedRegressionEditEntries) { 'edit' } else { 'play' }
    if ($Mode -ne $qaFocusedRegressionExpectedMode -or
        @($PSBoundParameters.Keys | Where-Object { $_ -notin $qaFocusedRegressionParameters }).Count -ne 0) {
        throw 'Focused regression verification requires its exact owned mode without selectors or resume inputs.'
    }
}
if ($PSBoundParameters.ContainsKey('SerializedStartup')) {
    $qaSerializedSelectors = @($PSBoundParameters.GetEnumerator() | Where-Object {
        $_.Key -ne 'SerializedStartup' -and
        $_.Value -is [System.Management.Automation.SwitchParameter] -and [bool]$_.Value
    })
    $qaSerializedEntry = ($Mode -eq 'edit') -and (($U0T01FacilityShop -and
        $ExecuteMethod -eq 'FacilityShopPurchaseT01DebugScenarios.RunFromBatch') -or
        ($U0T02Fulfillment -and
        $ExecuteMethod -eq 'FacilityWishlistProcurementCausalProductionEntryDebugScenarios.RunT02FromBatch'))
    $qaSerializedObserverEntry = $Mode -eq 'play' -and $U0E09EnvironmentExposure -and
        $ExecuteMethod -eq 'DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch'
    $qaSerializedCropEntry = $Mode -eq 'edit' -and $qaSerializedSelectors.Count -eq 0 -and
        $ExecuteMethod -in @('EnvironmentCropProductionEntryDebugScenarios.RunCropCommandObserversFromBatch',
            'EnvironmentCropProductionEntryDebugScenarios.RunCropAndFastingObserversFromBatch',
            'ResearchWorkObserverDebugScenarios.RunFromBatch',
            'RemainingPresentationObserverDebugScenarios.RunForBatch',
            'MigratedProducerRunOffenseEntryE2EDebugScenarios.RunRelationshipObservationBatch',
            'ObserverCompositionClosureDebugScenarios.RunForBatch',
            'PilotInputExperienceProfileExporter.RunForBatch',
            'V25NarrativeInferenceDebugScenarios.RunSkillNarrativeEvidenceBatch',
            'MigratedProducerEconomyServiceEntryE2EDebugScenarios.RunFacilityUseObserverBatch')
    $qaSerializedComposition = $Mode -eq 'play' -and $qaSerializedSelectors.Count -eq 0 -and
        $ExecuteMethod -eq 'ObserverCompositionClosureDebugScenarios.RequestActualContainerForBatch'
    $qaSerializedGameWorldLineage = $Mode -eq 'play' -and $qaSerializedSelectors.Count -eq 0 -and
        $ExecuteMethod -eq 'GameWorldLineageDebugScenarios.RequestActualContainerForBatch'
    $qaSerializedCharacterRestoreLifetime = $Mode -eq 'play' -and $qaSerializedSelectors.Count -eq 0 -and
        $ExecuteMethod -eq 'GameWorldLineageDebugScenarios.RequestCharacterRestoreLifetimeForBatch'
    $qaSerializedRestoreOutcome = $Mode -eq 'play' -and $qaSerializedSelectors.Count -eq 0 -and
        $ExecuteMethod -eq 'GameWorldLineageDebugScenarios.RequestRestoreOutcomeForBatch'
    $qaSerializedMovementOwnership = $Mode -eq 'play' -and $qaSerializedSelectors.Count -eq 0 -and
        $ExecuteMethod -in @('MovementOwnershipDebugScenarios.RequestActualContainerForBatch', 'MovementOwnershipDebugScenarios.RequestDestinationlessForBatch', 'MovementOwnershipDebugScenarios.RequestDeprivationForBatch', 'MovementOwnershipDebugScenarios.RequestPrimitiveForBatch', 'MovementOwnershipDebugScenarios.RequestRestockForBatch', 'MovementOwnershipDebugScenarios.RequestMedicalTransferForBatch', 'MovementOwnershipDebugScenarios.RequestWorkAccidentForBatch', 'MovementOwnershipDebugScenarios.RequestFactionReinforcementForBatch', 'MovementOwnershipDebugScenarios.RequestSkillTargetingForBatch')
    $qaSerializedNarrativeContextLifetime = $Mode -eq 'play' -and $qaSerializedSelectors.Count -eq 0 -and
        $ExecuteMethod -eq 'NarrativeContextLifetimeDebugScenarios.RequestActualContainerForBatch'
    $qaSerializedDestructiveDrain = $Mode -eq 'play' -and $qaSerializedSelectors.Count -eq 0 -and
        $ExecuteMethod -eq 'PhysicalItemLogisticsPlayModeVerifier.RequestDestructiveDrainPreparedOutputForBatch'
    $qaSerializedArchitectureCore = $Mode -eq 'edit' -and $qaSerializedSelectors.Count -eq 0 -and
        $ExecuteMethod -in @('ArchitectureRefactorVerificationBatch.RunCore', 'ArchitectureRefactorVerificationBatch.RunCampaign', 'ArchitectureRefactorVerificationBatch.RunProductionBills', 'ArchitectureRefactorVerificationBatch.RunBuildings', 'ArchitectureRefactorVerificationBatch.RunDefense', 'ArchitectureRefactorVerificationBatch.RunBuildingVisual', 'ArchitectureRefactorVerificationBatch.RunCropPersistence', 'ArchitectureRefactorVerificationBatch.RunCharacterDefinitions', 'ArchitectureRefactorVerificationBatch.RunEquipmentEvolution', 'ArchitectureRefactorVerificationBatch.RunConsumablePolicies', 'ArchitectureRefactorVerificationBatch.RunConsumableExecution')
    $qaSerializedFocusedRegression = $qaSerializedSelectors.Count -eq 0 -and
        (($Mode -eq 'edit' -and $ExecuteMethod -in $qaFocusedRegressionEditEntries) -or
         ($Mode -eq 'play' -and $ExecuteMethod -in $qaFocusedRegressionPlayEntries))
    if (-not $qaSerializedCropEntry -and -not $qaSerializedComposition -and -not $qaSerializedGameWorldLineage -and
        -not $qaSerializedCharacterRestoreLifetime -and
        -not $qaSerializedRestoreOutcome -and
        -not $qaSerializedNarrativeContextLifetime -and -not $qaSerializedMovementOwnership -and -not $qaSerializedArchitectureCore -and
        -not $qaSerializedFocusedRegression -and
        -not $qaSerializedDestructiveDrain -and
        ((-not $qaSerializedEntry -and -not $qaSerializedObserverEntry) -or $qaSerializedSelectors.Count -ne 1)) {
        throw 'SerializedStartup accepts only explicitly owned serialized verification entries.'
    }
}
$qaFeedColdResume = $ExecuteMethod -eq 'CaptivityWildlifeLifecyclePlayModeVerifier.RunFeedColdResumeBatch'
$qaHusbandryPendingColdResume = $ExecuteMethod -eq 'CaptivityWildlifeLifecyclePlayModeVerifier.RunHusbandryPendingColdResumeBatch'
if ($qaFeedColdResume -or $qaHusbandryPendingColdResume) {
    $qaFeedAllowedParameters = @('ExecuteMethod','OutputDirectory','Mode','UnityExecutable',
        'U0E11Capture','CampaignResumeWorld','CampaignResumeSha256')
    if ($Mode -ne 'play' -or -not $U0E11Capture -or
        @($PSBoundParameters.Keys | Where-Object { $_ -notin $qaFeedAllowedParameters }).Count -ne 0 -or
        -not $PSBoundParameters.ContainsKey('CampaignResumeWorld') -or
        -not $PSBoundParameters.ContainsKey('CampaignResumeSha256')) {
        throw 'Dedicated cold resume requires only its isolated play capture entry and an explicit world/hash pair.'
    }
}
if ($U0BDCD01DoorTraversal -and @($PSBoundParameters.GetEnumerator() | Where-Object {
        $_.Key -ne 'U0BDCD01DoorTraversal' -and
        $_.Value -is [System.Management.Automation.SwitchParameter] -and
        [bool]$_.Value
    }).Count -ne 0) {
    throw 'U0 BDC-D01 door-traversal verification cannot be combined with another gameplay selector.'
}
if ($U0BDCD01DoorTraversal -and ($Mode -ne 'play' -or
    $ExecuteMethod -ne 'DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch')) {
    throw 'U0 BDC-D01 door-traversal verification requires the owned isolated PlayMode facade.'
}
if ($U0A04CorpseEating -and ($Mode -ne 'play' -or
    $ExecuteMethod -ne 'DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch' -or
    $U0A03VisitorExit -or $U0A04VandalismMacro -or $U0A04VandalismBreakdown -or
    $U0A09Deprivation -or $U0A09SafeDrink)) {
    throw 'A04 corpse eating requires its own isolated PlayMode facade scope.'
}
if ($SourceV9SkillExpeditionActor -and -not $SourceV9SkillIntake) {
    throw 'Expedition-eligible skill actor requires a fresh source-v9 skill intake.'
}
if ($PSBoundParameters.ContainsKey('SourceV9InitialSeed') -and
    (-not $SourceV9SkillIntake -or $Mode -ne 'campaign' -or
     $ExecuteMethod -ne 'DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch' -or
     $PSBoundParameters.ContainsKey('CampaignResumeWorld') -or
     $PSBoundParameters.ContainsKey('CampaignResumeSha256'))) {
    throw 'Pilot initial seed requires a fresh isolated skill intake before party creation, never a resumed world.'
}
if ($U0G08Recovery) {
    $qaG08AllowedParameters = @(
        'ExecuteMethod',
        'OutputDirectory',
        'Mode',
        'UnityExecutable',
        'U0G08Recovery')
    if (@($PSBoundParameters.Keys | Where-Object { $_ -notin $qaG08AllowedParameters }).Count -ne 0) {
        throw 'U0 G08 recovery verification cannot be combined with another explicit selector or input, including an explicitly false selector.'
    }
    if ($Mode -ne 'edit' -or
        $ExecuteMethod -ne 'V20ContentResolutionRecoveryDebugScenarios.RunIsolatedOrThrow') {
        throw 'U0 G08 recovery verification requires its dedicated isolated edit entry.'
    }
}
$qaSourceV9AuthoringInputKeys = @(
    'SourceV9AuthoringSource',
    'SourceV9AuthoringManifestSha256',
    'SourceV9AuthoringProfile')
if ($SourceV9ActualModelAuthoring) {
    $qaSourceV9AuthoringAllowedParameters = @(
        'ExecuteMethod',
        'OutputDirectory',
        'Mode',
        'UnityExecutable',
        'SourceV9ActualModelAuthoring',
        'SourceV9AuthoringSource',
        'SourceV9AuthoringManifestSha256',
        'SourceV9AuthoringProfile')
    if (@($PSBoundParameters.Keys | Where-Object { $_ -notin $qaSourceV9AuthoringAllowedParameters }).Count -ne 0) {
        throw 'Source-v9 actual model authoring cannot be combined with another explicit selector or input, including an explicitly false selector.'
    }
    if ($Mode -ne 'edit' -or
        $ExecuteMethod -ne 'GameplayModuleSelectionActualModelAuthoringBatchRunner.RunOrThrow') {
        throw 'Source-v9 actual model authoring requires its dedicated isolated edit entry.'
    }
    if (@($qaSourceV9AuthoringInputKeys | Where-Object {
            -not $PSBoundParameters.ContainsKey($_) -or
            [string]::IsNullOrWhiteSpace([string]$PSBoundParameters[$_])
        }).Count -ne 0) {
        throw 'Source-v9 actual model authoring requires source, manifest SHA-256 and profile inputs.'
    }
} elseif (@($qaSourceV9AuthoringInputKeys | Where-Object { $PSBoundParameters.ContainsKey($_) }).Count -ne 0) {
    throw 'Source-v9 authoring inputs require the explicit actual-model-authoring opt-in.'
}
if ($SourceV9AuthorshipContract) {
    $qaSourceV9AuthorshipContractAllowedParameters = @(
        'ExecuteMethod',
        'OutputDirectory',
        'Mode',
        'UnityExecutable',
        'SourceV9AuthorshipContract')
    if (@($PSBoundParameters.Keys | Where-Object { $_ -notin $qaSourceV9AuthorshipContractAllowedParameters }).Count -ne 0) {
        throw 'Source-v9 authorship contract checks cannot be combined with actual authoring, source inputs or another explicit selector, including an explicitly false selector.'
    }
    if ($Mode -ne 'edit' -or
        $ExecuteMethod -ne 'GameplayModuleSelectionActualModelAuthoringBatchRunner.RunContractChecksOrThrow') {
        throw 'Source-v9 authorship contract checks require their dedicated isolated edit entry.'
    }
}

# This wrapper never launches Unity directly. The native launcher must prove
# noninteractive station/desktop containment before executing any Unity code.
# Caller owns the SOURCE_EDIT -> UNITY_VERIFY freeze and single-run policy.
$qaProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$qaRoot = (Join-Path $qaProject 'Artifacts\QA') + '\'
$qaSourceV9AuthoringSourcePath = $null
if ($SourceV9ActualModelAuthoring) {
    if (-not [IO.Path]::IsPathRooted($SourceV9AuthoringSource)) {
        throw 'Source-v9 actual model authoring source must be an absolute directory path.'
    }
    $qaSourceV9AuthoringSourcePath = [IO.Path]::GetFullPath($SourceV9AuthoringSource).TrimEnd('\')
    if (-not $qaSourceV9AuthoringSourcePath.StartsWith($qaRoot, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $qaSourceV9AuthoringSourcePath -PathType Container)) {
        throw 'Source-v9 actual model authoring source must be an existing directory inside this project Artifacts/QA.'
    }
    for ($qaCursor = Get-Item -LiteralPath $qaSourceV9AuthoringSourcePath -Force; $null -ne $qaCursor; $qaCursor = $qaCursor.Parent) {
        if ($qaCursor.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'Reparse-point source-v9 actual model authoring paths are not allowed.'
        }
    }
    $qaSourceV9AuthoringManifestPath = Join-Path $qaSourceV9AuthoringSourcePath 'manifest.json'
    if (-not (Test-Path -LiteralPath $qaSourceV9AuthoringManifestPath -PathType Leaf)) {
        throw 'Source-v9 actual model authoring source must contain manifest.json.'
    }
    $qaSourceV9AuthoringManifestItem = Get-Item -LiteralPath $qaSourceV9AuthoringManifestPath -Force
    if ($qaSourceV9AuthoringManifestItem.Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw 'Reparse-point source-v9 actual model authoring manifests are not allowed.'
    }
    if ($SourceV9AuthoringManifestSha256 -cnotmatch '^sha256:[0-9a-f]{64}$') {
        throw 'Source-v9 actual model authoring manifest SHA-256 must use sha256: followed by 64 lowercase hexadecimal characters.'
    }
    $qaSourceV9ActualManifestSha256 = 'sha256:' + (Get-FileHash -LiteralPath $qaSourceV9AuthoringManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if (-not [string]::Equals($qaSourceV9ActualManifestSha256, $SourceV9AuthoringManifestSha256, [StringComparison]::Ordinal)) {
        throw 'Source-v9 actual model authoring manifest SHA-256 does not match manifest.json bytes.'
    }
    if ($SourceV9AuthoringProfile -cnotin @(
            'CharacterSkillModuleSelection',
            'AcquiredTraitModuleSelection',
            'EquipmentEvolutionModuleSelection',
            'FacilityEvolutionModuleSelection')) {
        throw 'Source-v9 actual model authoring profile is not one of the four approved exact profiles.'
    }
}
if (-not [IO.Path]::IsPathRooted($OutputDirectory)) { throw 'Absolute evidence path required.' }
$qaOutput = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\')
if (-not $qaOutput.StartsWith($qaRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Evidence must be inside this project Artifacts/QA.'
}
for ($qaCursor = [IO.DirectoryInfo]::new([IO.Path]::GetDirectoryName($qaOutput)); $null -ne $qaCursor; $qaCursor = $qaCursor.Parent) {
    if ($qaCursor.Exists -and ($qaCursor.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Reparse-point evidence paths are not allowed.'
    }
}
if ($ExecuteMethod -notmatch '^[A-Za-z_][A-Za-z0-9_.]+$') { throw 'Invalid execute method.' }
if ($Mode -ne 'edit' -and $ExecuteMethod -notin $qaFocusedRegressionPlayEntries -and $ExecuteMethod -notin @(
        'DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch',
        'ObserverCompositionClosureDebugScenarios.RequestActualContainerForBatch',
        'GameWorldLineageDebugScenarios.RequestActualContainerForBatch',
        'GameWorldLineageDebugScenarios.RequestCharacterRestoreLifetimeForBatch',
        'GameWorldLineageDebugScenarios.RequestRestoreOutcomeForBatch',
        'MovementOwnershipDebugScenarios.RequestActualContainerForBatch',
        'MovementOwnershipDebugScenarios.RequestDestinationlessForBatch',
        'MovementOwnershipDebugScenarios.RequestDeprivationForBatch',
        'MovementOwnershipDebugScenarios.RequestPrimitiveForBatch',
        'MovementOwnershipDebugScenarios.RequestRestockForBatch',
        'MovementOwnershipDebugScenarios.RequestMedicalTransferForBatch',
        'MovementOwnershipDebugScenarios.RequestWorkAccidentForBatch',
        'MovementOwnershipDebugScenarios.RequestFactionReinforcementForBatch',
        'MovementOwnershipDebugScenarios.RequestSkillTargetingForBatch',
        'PhysicalItemLogisticsPlayModeVerifier.RequestDestructiveDrainPreparedOutputForBatch',
        'NarrativeContextLifetimeDebugScenarios.RequestActualContainerForBatch',
        'V20CampaignDebugScenarios.RequestWim044IsolatedBatch',
        'DungeonFullWorldRoundTripPlayModeFacade.RequestInjuredReturneeSaveRegression',
        'DungeonFullWorldRoundTripPlayModeFacade.RequestExteriorDepartureExposureRegression',
        'DungeonLiveNarrativeCampaignRunner.RequestSkillBlueprintHaulDiagnostics',
        'DungeonLiveNarrativeCampaignRunner.RequestConstructionHaulDiagnostics',
        'DungeonLiveNarrativeCampaignRunner.RequestSkillResearchDiagnostics',
        'DungeonLiveNarrativeCampaignRunner.RequestSkillBattleDiagnostics',
        'DungeonLiveNarrativeCampaignRunner.RequestCompletedCraftDeliveryDiagnostics',
        'DungeonLiveNarrativeCampaignRunner.RequestOriginalSkillPendingDiagnostics',
        'CaptivityWildlifeLifecyclePlayModeVerifier.RunTransportBatch',
        'CaptivityWildlifeLifecyclePlayModeVerifier.RunBatch',
        'CaptivityWildlifeLifecyclePlayModeVerifier.RunFeedColdResumeBatch',
        'CaptivityWildlifeLifecyclePlayModeVerifier.RunHusbandryPendingColdResumeBatch',
        'Wim029CompanionRoleOutcomePlayModeVerifier.RunBatch',
        'Wim029CompanionRoleOutcomePlayModeVerifier.RunHaulBatch',
        'InfrastructureCommandOutcomePlayModeFacade.RequestRunFromMenu',
        'DefenseEngagementPlayModeVerifier.StartWim048Batch')) {
    throw 'Play/campaign requires the owned isolated PlayMode facade.'
}
if ($ExecuteMethod -eq 'CaptivityWildlifeLifecyclePlayModeVerifier.RunBatch' -and
    ($Mode -ne 'play' -or -not $U0E11Capture)) {
    throw 'Full captivity/wildlife lifecycle requires isolated PlayMode and the explicit U0E11Capture report binding.'
}
if ($TerminalDeathSave -and ($Mode -ne 'play' -or $ExecuteMethod -ne
        'DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch')) {
    throw 'Terminal death/save verification requires the owned isolated PlayMode facade.'
}
if ($U0A03VisitorExit -and $Mode -ne 'play') {
    throw 'U0 A03 visitor-exit verification requires isolated PlayMode.'
}
if ($U0A03VisitorExit -and $ExecuteMethod -ne 'DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch') {
    throw 'U0 A03 visitor-exit verification requires the owned isolated PlayMode facade.'
}
if (($U0A04VandalismMacro -or $U0A04VandalismBreakdown) -and $Mode -ne 'play') {
    throw 'U0 A04 vandalism verification requires isolated PlayMode.'
}
if (($U0A04VandalismMacro -or $U0A04VandalismBreakdown) -and $ExecuteMethod -ne 'DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch') {
    throw 'U0 A04 vandalism verification requires the owned isolated PlayMode facade.'
}
if ($U0A03VisitorExit -and ($U0A04VandalismMacro -or $U0A04VandalismBreakdown)) {
    throw 'U0 A03 and A04 verifications require separate isolated Unity processes.'
}
if ($U0A04VandalismMacro -and $U0A04VandalismBreakdown) {
    throw 'U0 A04 macro and breakdown verifications require separate isolated Unity processes.'
}
if ($U0A09Deprivation -and $Mode -ne 'play') {
    throw 'U0 A09 deprivation verification requires isolated PlayMode.'
}
if ($U0A09Deprivation -and $ExecuteMethod -ne 'DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch') {
    throw 'U0 A09 deprivation verification requires the owned isolated PlayMode facade.'
}
if ($U0A09Deprivation -and ($U0A03VisitorExit -or $U0A04VandalismMacro -or $U0A04VandalismBreakdown)) {
    throw 'U0 A09 deprivation verification requires its own isolated Unity process.'
}
if ($U0A09SafeDrink -and $Mode -ne 'play') {
    throw 'U0 A09 safe-drink verification requires isolated PlayMode.'
}
if ($U0A09SafeDrink -and $ExecuteMethod -ne 'DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch') {
    throw 'U0 A09 safe-drink verification requires the owned isolated PlayMode facade.'
}
if ($U0A09SafeDrink -and ($U0A03VisitorExit -or $U0A04VandalismMacro -or $U0A04VandalismBreakdown -or $U0A09Deprivation)) {
    throw 'U0 A09 safe-drink verification requires its own isolated Unity process.'
}
if ($U0EEC01CropFailure -and $Mode -ne 'edit') {
    throw 'U0 EEC01 crop-failure verification requires isolated edit mode.'
}
if ($U0EEC01CropFailure -and $ExecuteMethod -ne 'EnvironmentCropProductionEntryDebugScenarios.RunEec01FromBatch') {
    throw 'U0 EEC01 crop-failure verification requires its dedicated batch entry.'
}
if ($U0EEC02WildlifeArrival -and $Mode -ne 'edit') {
    throw 'U0 EEC02 wildlife-arrival verification requires isolated edit mode.'
}
if ($U0EEC02WildlifeArrival -and $ExecuteMethod -ne 'SeasonalWildlifeArrivalOutcomeDebugScenarios.RunEec02FromBatch') {
    throw 'U0 EEC02 wildlife-arrival verification requires its dedicated batch entry.'
}
if ($U0EEC06CombatCraft -and $Mode -ne 'edit') {
    throw 'U0 EEC06 combat-craft verification requires isolated edit mode.'
}
if ($U0EEC06CombatCraft -and $ExecuteMethod -ne 'CombatEquipmentCraftCompletionOutcomeDebugScenarios.RunEec06FromBatch') {
    throw 'U0 EEC06 combat-craft verification requires its dedicated batch entry.'
}
if ($U0G01RecruitDelivery -and $Mode -ne 'edit') {
    throw 'U0 G01 recruit-delivery verification requires isolated edit mode.'
}
if ($U0G01RecruitDelivery -and $ExecuteMethod -ne 'MigratedProducerEconomyServiceEntryE2EDebugScenarios.RunG01FromBatch') {
    throw 'U0 G01 recruit-delivery verification requires its dedicated batch entry.'
}
if ($U0G02EmploymentSettlement -and $Mode -ne 'edit') {
    throw 'U0 G02 employment-settlement verification requires isolated edit mode.'
}
if ($U0G02EmploymentSettlement -and $ExecuteMethod -ne 'EmploymentSettlementG02DebugScenarios.RunG02FromBatch') {
    throw 'U0 G02 employment-settlement verification requires its dedicated batch entry.'
}
if ($U0G03PolicyRevision -and $Mode -ne 'edit') {
    throw 'U0 G03 policy-revision verification requires isolated edit mode.'
}
if ($U0G03PolicyRevision -and $ExecuteMethod -ne 'DoorHeldOpenOutcomeDebugScenarios.RunG03FromBatch') {
    throw 'U0 G03 policy-revision verification requires its dedicated batch entry.'
}
if ($U0G04Application -and $Mode -ne 'edit') {
    throw 'U0 G04 V20 application verification requires isolated edit mode.'
}
if ($U0G04Application -and $ExecuteMethod -ne 'V20ContentResolutionRecoveryDebugScenarios.RunG04OrThrow') {
    throw 'U0 G04 V20 application verification requires its dedicated batch entry.'
}
if ($U0G06MilestonePressure -and $Mode -ne 'edit') {
    throw 'U0 G06 milestone-pressure verification requires isolated edit mode.'
}
if ($U0G06MilestonePressure -and $ExecuteMethod -ne 'MigratedProducerCombatDefenseEntryE2EDebugScenarios.RunG06FromBatch') {
    throw 'U0 G06 milestone-pressure verification requires its dedicated batch entry.'
}
if ($U0G07KnowledgeCompletion -and $Mode -ne 'edit') {
    throw 'U0 G07 knowledge-completion verification requires isolated edit mode.'
}
if ($U0G07KnowledgeCompletion -and $ExecuteMethod -ne 'KnowledgeResearchOutcomeDebugScenarios.RunG07FromBatch') {
    throw 'U0 G07 knowledge-completion verification requires its dedicated batch entry.'
}
if ($U0G09ExternalInfluence -and $Mode -ne 'edit') {
    throw 'U0 G09 external-influence verification requires isolated edit mode.'
}
if ($U0G09ExternalInfluence -and $ExecuteMethod -ne 'ExternalInfluenceG09DebugScenarios.RunG09FromBatch') {
    throw 'U0 G09 external-influence verification requires its dedicated batch entry.'
}
if ($U0G10StaffDiscontent -and $Mode -ne 'edit') {
    throw 'U0 G10 staff-discontent verification requires isolated edit mode.'
}
if ($U0G10StaffDiscontent -and $ExecuteMethod -ne 'StaffDiscontentDebugScenarios.RunG10FromBatch') {
    throw 'U0 G10 staff-discontent verification requires its dedicated batch entry.'
}
if ($U0G11DurableEquipment -and $Mode -ne 'edit') {
    throw 'U0 G11 durable-equipment verification requires isolated edit mode.'
}
if ($U0G11DurableEquipment -and $ExecuteMethod -ne 'RunInvasionDurableEquipmentDebugScenarios.RunG11FromBatch') {
    throw 'U0 G11 durable-equipment verification requires its dedicated batch entry.'
}
if ($U0G12GolemUpkeep -and $Mode -ne 'play') {
    throw 'U0 G12 Golem-upkeep verification requires isolated PlayMode.'
}
if ($U0G12GolemUpkeep -and $ExecuteMethod -ne 'DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch') {
    throw 'U0 G12 Golem-upkeep verification requires the owned isolated PlayMode facade.'
}
if ($U0G14TraitAnalysis -and $Mode -ne 'edit') {
    throw 'U0 G14 trait-analysis verification requires isolated edit mode.'
}
if ($U0G14TraitAnalysis -and $ExecuteMethod -ne 'TraitAnalysisRecoveryDebugScenarios.RunG14FromBatch') {
    throw 'U0 G14 trait-analysis verification requires its dedicated batch entry.'
}
if ($U0C01O02FuneralLineage -and $Mode -ne 'edit') {
    throw 'U0 C:O01-O02 funeral/lineage verification requires isolated edit mode.'
}
if ($U0C01O02FuneralLineage -and $ExecuteMethod -ne 'MigratedProducerCharacterSurvivalEntryE2EDebugScenarios.RunC01O02FromBatch') {
    throw 'U0 C:O01-O02 funeral/lineage verification requires its dedicated batch entry.'
}
if ($U0C03ProductionLoss -and $Mode -ne 'edit') {
    throw 'U0 C:O03 production-loss verification requires isolated edit mode.'
}
if ($U0C03ProductionLoss -and $ExecuteMethod -ne 'MigratedProducerCharacterSurvivalEntryE2EDebugScenarios.RunC03FromBatch') {
    throw 'U0 C:O03 production-loss verification requires its dedicated batch entry.'
}
if ($U0C04O05CareerObservation -and $Mode -ne 'edit') {
    throw 'U0 C:O04-O05 career-observation verification requires isolated edit mode.'
}
if ($U0C04O05CareerObservation -and $ExecuteMethod -ne 'MigratedProducerCharacterSurvivalEntryE2EDebugScenarios.RunC04O05FromBatch') {
    throw 'U0 C:O04-O05 career-observation verification requires its dedicated batch entry.'
}
if ($U0CN01AmbitionTransition -and $Mode -ne 'edit') {
    throw 'U0 C:N01 ambition-transition verification requires isolated edit mode.'
}
if ($U0CN01AmbitionTransition -and $ExecuteMethod -ne 'CharacterAmbitionTransitionOutcomeDebugScenarios.RunCN01FromBatch') {
    throw 'U0 C:N01 ambition-transition verification requires its dedicated batch entry.'
}
if ($U0CR01FamilyPlanning -and $Mode -ne 'edit') {
    throw 'U0 C:R01 family-planning verification requires isolated edit mode.'
}
if ($U0CR01FamilyPlanning -and $ExecuteMethod -ne 'FamilyPlanningPolicyReachabilityDebugScenarios.RunCR01FromBatch') {
    throw 'U0 C:R01 family-planning verification requires its dedicated batch entry.'
}
if ($U0CR02R05Reproduction -and $Mode -ne 'edit') {
    throw 'U0 C:R02-R05 reproduction verification requires isolated edit mode.'
}
if ($U0CR02R05Reproduction -and $ExecuteMethod -ne 'ReproductionProcessTransitionDebugScenarios.RunCR02R05FromBatch') {
    throw 'U0 C:R02-R05 reproduction verification requires its dedicated batch entry.'
}
if ($U0CR07Birth -and $Mode -ne 'edit') {
    throw 'U0 C:R07 birth verification requires isolated edit mode.'
}
if ($U0CR07Birth -and $ExecuteMethod -ne 'ReproductionProcessTransitionDebugScenarios.RunCR07FromBatch') {
    throw 'U0 C:R07 birth verification requires its dedicated batch entry.'
}
if ($U0CR09Household -and $Mode -ne 'edit') {
    throw 'U0 C:R09 household verification requires isolated edit mode.'
}
if ($U0CR09Household -and $ExecuteMethod -ne 'ReproductionProcessTransitionDebugScenarios.RunCR09FromBatch') {
    throw 'U0 C:R09 household verification requires its dedicated batch entry.'
}
if ($U0CA01AgeAnatomy -and $Mode -ne 'edit') {
    throw 'U0 C:A01 age-anatomy verification requires isolated edit mode.'
}
if ($U0CA01AgeAnatomy -and $ExecuteMethod -ne 'AgeConditionAnatomyProjectionDebugScenarios.RunCA01FromBatch') {
    throw 'U0 C:A01 age-anatomy verification requires its dedicated batch entry.'
}
if ($U0CA02DiseaseAnatomy -and $Mode -ne 'edit') {
    throw 'U0 C:A02 disease-anatomy verification requires isolated edit mode.'
}
if ($U0CA02DiseaseAnatomy -and $ExecuteMethod -ne 'DiseaseAnatomyBurdenProjectionDebugScenarios.RunCA02FromBatch') {
    throw 'U0 C:A02 disease-anatomy verification requires its dedicated batch entry.'
}
if ($U0UI01DefenseService -and $Mode -ne 'edit') {
    throw 'U0 UI-01 defense-service verification requires isolated edit mode.'
}
if ($U0UI01DefenseService -and $ExecuteMethod -ne 'DefenseFacilityServiceOutcomeDebugScenarios.RunUI01FromBatch') {
    throw 'U0 UI-01 defense-service verification requires its dedicated batch entry.'
}
if ($U0UI02ResidentEvacuation -and $Mode -ne 'play') {
    throw 'U0 UI-02 resident-evacuation verification requires isolated PlayMode.'
}
if ($U0UI02ResidentEvacuation -and $ExecuteMethod -ne 'DefenseEngagementPlayModeVerifier.StartWim048Batch') {
    throw 'U0 UI-02 resident-evacuation verification requires its dedicated batch entry.'
}
if ($U0CON01ConsumablesPolicy -and $Mode -ne 'edit') {
    throw 'U0 CON01 consumables-policy verification requires isolated edit mode.'
}
if ($U0CON01ConsumablesPolicy -and $ExecuteMethod -ne 'SurvivalDebugScenarios.RunCON01FromBatch') {
    throw 'U0 CON01 consumables-policy verification requires its dedicated batch entry.'
}
if ($U0BDCS01SpaceExpansion -and $Mode -ne 'edit') {
    throw 'U0 BDC-S01 space-expansion verification requires isolated edit mode.'
}
if ($U0BDCS01SpaceExpansion -and $ExecuteMethod -ne 'DungeonSpaceExpansionDebugScenarios.RunBDCS01FromBatch') {
    throw 'U0 BDC-S01 space-expansion verification requires its dedicated batch entry.'
}
if ($U0BDCD01DoorPolicy -and $Mode -ne 'edit') {
    throw 'U0 BDC-D01 door-policy verification requires isolated edit mode.'
}
if ($U0BDCD01DoorPolicy -and $ExecuteMethod -ne 'DoorHeldOpenOutcomeDebugScenarios.RunBDCD01FromBatch') {
    throw 'U0 BDC-D01 door-policy verification requires its dedicated batch entry.'
}
if ($FacilityContinuation -and $Mode -ne 'campaign') { throw 'Facility continuation requires isolated campaign capture-only mode.' }
if ($FacilityContinuationRestRequest -and -not $FacilityContinuation) { throw 'Rest request requires facility continuation verification.' }
if ($MedicalContinuation -and $Mode -ne 'campaign') { throw 'Medical continuation requires isolated campaign capture-only mode.' }
if ($MedicalContinuation -and $FacilityContinuation) { throw 'Medical and facility continuation verification are mutually exclusive.' }
if ($ShopContinuation -and $Mode -ne 'campaign') { throw 'Shop continuation requires isolated campaign capture-only mode.' }
if ($W01CheckoutCleanup -and -not $ShopContinuation) { throw 'W01 checkout cleanup requires isolated shop continuation verification.' }
if ($ShopContinuation -and $FacilityContinuation) { throw 'Shop and facility continuation verification are mutually exclusive.' }
if ($ShopContinuation -and $MedicalContinuation) { throw 'Shop and medical continuation verification are mutually exclusive.' }
if ($MealContinuation -and $Mode -ne 'campaign') { throw 'Meal continuation requires isolated campaign capture-only mode.' }
if ($MealContinuation -and $FacilityContinuation) { throw 'Meal and facility continuation verification are mutually exclusive.' }
if ($MealContinuation -and $MedicalContinuation) { throw 'Meal and medical continuation verification are mutually exclusive.' }
if ($MealContinuation -and $ShopContinuation) { throw 'Meal and shop continuation verification are mutually exclusive.' }
if ($DrinkContinuation -and $Mode -ne 'campaign') { throw 'Drink continuation requires isolated campaign capture-only mode.' }
if ($DrinkContinuation -and $FacilityContinuation) { throw 'Drink and facility continuation verification are mutually exclusive.' }
if ($DrinkContinuation -and $MedicalContinuation) { throw 'Drink and medical continuation verification are mutually exclusive.' }
if ($DrinkContinuation -and $ShopContinuation) { throw 'Drink and shop continuation verification are mutually exclusive.' }
if ($DrinkContinuation -and $MealContinuation) { throw 'Drink and meal continuation verification are mutually exclusive.' }
if ($PersistentCookPause -and $Mode -ne 'play') { throw 'Persistent Cook pause verification requires isolated play mode.' }
if ($W01SurgeryPublicationRecovery -and $Mode -ne 'play') { throw 'W01 surgery publication recovery requires isolated play mode.' }
if ($W01SurgeryPublicationRecovery -and -not $W05SurgeryIdentity) { throw 'W01 surgery publication recovery requires W05 surgery identity verification.' }
if ($W01SurgeryClinicalFailure -and $Mode -ne 'play') { throw 'W01 surgery clinical failure requires isolated play mode.' }
if ($W01SurgeryClinicalFailure -and -not $W05SurgeryIdentity) { throw 'W01 surgery clinical failure requires W05 surgery identity verification.' }
if ($W01SurgeryClinicalFailure -and $W01SurgeryPublicationRecovery) { throw 'W01 surgery clinical failure and publication recovery require separate Unity processes.' }
if ($W03MedicalLabor -and $Mode -ne 'play') { throw 'W03 medical labor verification requires isolated play mode.' }
if ($W03MedicalLabor -and ($FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity)) {
    throw 'W03 medical labor verification requires its own isolated Unity process.'
}
if ($W04RepairLive -and $Mode -ne 'play') { throw 'W04 repair live verification requires isolated play mode.' }
if ($W04RepairLive -and ($FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor)) {
    throw 'W04 repair live verification requires its own isolated Unity process.'
}
if ($W06ResourceCausality -and $Mode -ne 'play') { throw 'W06 resource causality verification requires isolated play mode.' }
if ($W06ResourceCausality -and ($FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive)) {
    throw 'W06 resource causality verification requires its own isolated Unity process.'
}
if ($U0E01RegistryInventory -and $Mode -ne 'play') {
    throw 'U0 E01 registry inventory verification requires isolated play mode.'
}
if ($U0E01RegistryInventory -and ($FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality -or $U0E02HusbandryOutcome -or $U0E03CropTreatment -or $SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $SourceV9FacilityIntake -or $SourceV9EquipmentReplay -or $SourceV9SkillReplay -or $SourceV9TraitReplay)) {
    throw 'U0 E01 registry inventory verification requires its own isolated Unity process.'
}
if ($U0E02HusbandryOutcome -and $Mode -ne 'edit') {
    throw 'U0 E02 husbandry outcome verification requires isolated edit mode.'
}
if ($U0E02HusbandryOutcome -and $ExecuteMethod -ne 'AnimalHusbandryOutcomeDebugScenarios.RunFromBatch') {
    throw 'U0 E02 husbandry outcome verification requires its dedicated batch entry.'
}
if ($U0E02HusbandryOutcome -and ($FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality -or $U0E01RegistryInventory -or $U0E03CropTreatment -or $SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $SourceV9FacilityIntake -or $SourceV9EquipmentReplay -or $SourceV9SkillReplay -or $SourceV9TraitReplay)) {
    throw 'U0 E02 husbandry outcome verification requires its own isolated Unity process.'
}
if ($U0E03CropTreatment -and $Mode -ne 'edit') {
    throw 'U0 E03 crop-treatment verification requires isolated edit mode.'
}
if ($U0E03CropTreatment -and $ExecuteMethod -ne 'EnvironmentCropProductionEntryDebugScenarios.RunE03FromBatch') {
    throw 'U0 E03 crop-treatment verification requires its dedicated batch entry.'
}
if ($U0E03CropTreatment -and ($FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality -or $U0E01RegistryInventory -or $U0E02HusbandryOutcome -or $SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $SourceV9FacilityIntake -or $SourceV9EquipmentReplay -or $SourceV9SkillReplay -or $SourceV9TraitReplay)) {
    throw 'U0 E03 crop-treatment verification requires its own isolated Unity process.'
}
if ($U0E06OperatingDay -and $Mode -ne 'edit') {
    throw 'U0 E06 operating-day verification requires isolated edit mode.'
}
if ($U0E06OperatingDay -and $ExecuteMethod -ne 'MigratedProducerEconomyEntryExtendedE2EDebugScenarios.RunOperatingDayE06FromBatch') {
    throw 'U0 E06 operating-day verification requires its dedicated batch entry.'
}
if ($U0E06OperatingDay -and ($FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality -or $U0E01RegistryInventory -or $U0E02HusbandryOutcome -or $U0E03CropTreatment -or $SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $SourceV9FacilityIntake -or $SourceV9EquipmentReplay -or $SourceV9SkillReplay -or $SourceV9TraitReplay)) {
    throw 'U0 E06 operating-day verification requires its own isolated Unity process.'
}
if ($U0E07StockPolicySale -and $Mode -ne 'edit') {
    throw 'U0 E07 stock-policy sale verification requires isolated edit mode.'
}
if ($U0E07StockPolicySale -and $ExecuteMethod -ne 'ResourceStockPolicySaleOutcomeE2EDebugScenarios.RunE07FromBatch') {
    throw 'U0 E07 stock-policy sale verification requires its dedicated batch entry.'
}
if ($U0E07StockPolicySale -and ($FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality -or $U0E01RegistryInventory -or $U0E02HusbandryOutcome -or $U0E03CropTreatment -or $U0E06OperatingDay -or $SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $SourceV9FacilityIntake -or $SourceV9EquipmentReplay -or $SourceV9SkillReplay -or $SourceV9TraitReplay)) {
    throw 'U0 E07 stock-policy sale verification requires its own isolated Unity process.'
}
if ($U0E08CropAction -and $Mode -ne 'edit') {
    throw 'U0 E08 crop-action verification requires isolated edit mode.'
}
if ($U0E08CropAction -and $ExecuteMethod -ne 'EnvironmentCropProductionEntryDebugScenarios.RunE08FromBatch') {
    throw 'U0 E08 crop-action verification requires its dedicated batch entry.'
}
if ($U0E08CropAction -and ($FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality -or $U0E01RegistryInventory -or $U0E02HusbandryOutcome -or $U0E03CropTreatment -or $U0E06OperatingDay -or $U0E07StockPolicySale -or $SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $SourceV9FacilityIntake -or $SourceV9EquipmentReplay -or $SourceV9SkillReplay -or $SourceV9TraitReplay)) {
    throw 'U0 E08 crop-action verification requires its own isolated Unity process.'
}
if ($U0E09EnvironmentExposure -and $Mode -ne 'play') {
    throw 'U0 E09 environment-exposure verification requires isolated PlayMode.'
}
if ($U0E09EnvironmentExposure -and $ExecuteMethod -ne 'DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch') {
    throw 'U0 E09 environment-exposure verification requires the owned isolated PlayMode facade.'
}
if ($U0E09EnvironmentExposure -and ($FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality -or $U0E01RegistryInventory -or $U0E02HusbandryOutcome -or $U0E03CropTreatment -or $U0E06OperatingDay -or $U0E07StockPolicySale -or $U0E08CropAction -or $SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $SourceV9FacilityIntake -or $SourceV9EquipmentReplay -or $SourceV9SkillReplay -or $SourceV9TraitReplay)) {
    throw 'U0 E09 environment-exposure verification requires its own isolated Unity process.'
}
if ($U0E10WorldResource -and $Mode -ne 'edit') {
    throw 'U0 E10 world-resource verification requires isolated edit mode.'
}
if ($U0E10WorldResource -and $ExecuteMethod -ne 'WorldResourceTransactionFaultDebugScenarios.RunE10FromBatch') {
    throw 'U0 E10 world-resource verification requires its dedicated batch entry.'
}
if ($U0E10WorldResource -and ($FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality -or $U0E01RegistryInventory -or $U0E02HusbandryOutcome -or $U0E03CropTreatment -or $U0E06OperatingDay -or $U0E07StockPolicySale -or $U0E08CropAction -or $U0E09EnvironmentExposure -or $SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $SourceV9FacilityIntake -or $SourceV9EquipmentReplay -or $SourceV9SkillReplay -or $SourceV9TraitReplay)) {
    throw 'U0 E10 world-resource verification requires its own isolated Unity process.'
}
if ($U0E11Wildlife -and $Mode -ne 'play') {
    throw 'U0 E11 wildlife verification requires isolated PlayMode.'
}
if ($U0E11Wildlife -and $ExecuteMethod -ne 'DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch') {
    throw 'U0 E11 wildlife verification requires the owned isolated PlayMode facade.'
}
if ($U0E11Wildlife -and ($FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality -or $U0E01RegistryInventory -or $U0E02HusbandryOutcome -or $U0E03CropTreatment -or $U0E06OperatingDay -or $U0E07StockPolicySale -or $U0E08CropAction -or $U0E09EnvironmentExposure -or $U0E10WorldResource -or $SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $SourceV9FacilityIntake -or $SourceV9EquipmentReplay -or $SourceV9SkillReplay -or $SourceV9TraitReplay)) {
    throw 'U0 E11 wildlife verification requires its own isolated Unity process.'
}
if ($U0E11Capture -and $Mode -ne 'play') {
    throw 'U0 E11 capture verification requires isolated PlayMode.'
}
if ($U0E11Capture -and $ExecuteMethod -notin @(
        'CaptivityWildlifeLifecyclePlayModeVerifier.RunTransportBatch',
        'CaptivityWildlifeLifecyclePlayModeVerifier.RunBatch',
        'CaptivityWildlifeLifecyclePlayModeVerifier.RunFeedColdResumeBatch',
        'CaptivityWildlifeLifecyclePlayModeVerifier.RunHusbandryPendingColdResumeBatch')) {
    throw 'U0 E11 capture verification requires its dedicated PlayMode entry.'
}
if ($U0E11Role -and $Mode -ne 'play') {
    throw 'U0 E11 role verification requires isolated PlayMode.'
}
if ($ExecuteMethod -eq 'Wim029CompanionRoleOutcomePlayModeVerifier.RunHaulBatch' -and
    ($Mode -ne 'play' -or -not $U0E11Role)) {
    throw 'U0 E11 haul requires its explicit isolated role report binding.'
}
if ($U0E11Role -and $ExecuteMethod -notin @(
        'Wim029CompanionRoleOutcomePlayModeVerifier.RunBatch',
        'Wim029CompanionRoleOutcomePlayModeVerifier.RunHaulBatch')) {
    throw 'U0 E11 role verification requires its dedicated PlayMode entry.'
}
if ($U0E11Butchery -and $Mode -ne 'edit') {
    throw 'U0 E11 butchery verification requires isolated edit mode.'
}
if ($U0E11Butchery -and $ExecuteMethod -ne 'Wim030CrystalBeetleCarapaceDebugScenarios.RunE11Batch') {
    throw 'U0 E11 butchery verification requires its dedicated batch entry.'
}
$qaU0E11FocusedCount = @(
    [bool]$U0E11Wildlife,
    [bool]$U0E11Capture,
    [bool]$U0E11Role,
    [bool]$U0E11Butchery
).Where({ $_ }).Count
if ($qaU0E11FocusedCount -gt 1) {
    throw 'U0 E11 focused verifications require separate isolated Unity processes.'
}
if ($U0E12Infrastructure -and $Mode -ne 'edit') {
    throw 'U0 E12 infrastructure verification requires isolated edit mode.'
}
if ($U0E12Infrastructure -and $ExecuteMethod -ne 'IndustrialInfrastructureDebugScenarios.RunE12FromBatch') {
    throw 'U0 E12 infrastructure verification requires its dedicated batch entry.'
}
if ($U0E13Automation -and $Mode -ne 'play') {
    throw 'U0 E13 automation verification requires isolated PlayMode.'
}
if ($U0E13Automation -and $ExecuteMethod -ne 'InfrastructureCommandOutcomePlayModeFacade.RequestRunFromMenu') {
    throw 'U0 E13 automation verification requires its dedicated PlayMode entry.'
}
if ($U0E13Automation -and ($FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality -or $U0E01RegistryInventory -or $U0E02HusbandryOutcome -or $U0E03CropTreatment -or $U0E06OperatingDay -or $U0E07StockPolicySale -or $U0E08CropAction -or $U0E09EnvironmentExposure -or $U0E10WorldResource -or $U0E11Wildlife -or $U0E11Capture -or $U0E11Role -or $U0E11Butchery -or $U0E12Infrastructure -or $SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $SourceV9FacilityIntake -or $SourceV9EquipmentReplay -or $SourceV9SkillReplay -or $SourceV9TraitReplay)) {
    throw 'U0 E13 automation verification requires its own isolated Unity process.'
}
if ($U0T01FacilityShop -and $Mode -ne 'edit') {
    throw 'U0 T01 facility-shop verification requires isolated edit mode.'
}
if ($U0T01FacilityShop -and $ExecuteMethod -ne 'FacilityShopPurchaseT01DebugScenarios.RunFromBatch') {
    throw 'U0 T01 facility-shop verification requires its dedicated batch entry.'
}
if ($U0T01FacilityShop -and ($FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality -or $U0E01RegistryInventory -or $U0E02HusbandryOutcome -or $U0E03CropTreatment -or $U0E06OperatingDay -or $U0E07StockPolicySale -or $U0E08CropAction -or $U0E09EnvironmentExposure -or $U0E10WorldResource -or $U0E11Wildlife -or $U0E11Capture -or $U0E11Role -or $U0E11Butchery -or $U0E12Infrastructure -or $U0E13Automation -or $SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $SourceV9FacilityIntake -or $SourceV9EquipmentReplay -or $SourceV9SkillReplay -or $SourceV9TraitReplay)) {
    throw 'U0 T01 facility-shop verification requires its own isolated Unity process.'
}
if ($U0T02Fulfillment -and $Mode -ne 'edit') {
    throw 'U0 T02 fulfillment verification requires isolated edit mode.'
}
if ($U0T02Fulfillment -and $ExecuteMethod -ne 'FacilityWishlistProcurementCausalProductionEntryDebugScenarios.RunT02FromBatch') {
    throw 'U0 T02 fulfillment verification requires its dedicated batch entry.'
}
if ($U0T02Fulfillment -and ($FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality -or $U0E01RegistryInventory -or $U0E02HusbandryOutcome -or $U0E03CropTreatment -or $U0E06OperatingDay -or $U0E07StockPolicySale -or $U0E08CropAction -or $U0E09EnvironmentExposure -or $U0E10WorldResource -or $U0E11Wildlife -or $U0E11Capture -or $U0E11Role -or $U0E11Butchery -or $U0E12Infrastructure -or $U0E13Automation -or $U0T01FacilityShop -or $SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $SourceV9FacilityIntake -or $SourceV9EquipmentReplay -or $SourceV9SkillReplay -or $SourceV9TraitReplay)) {
    throw 'U0 T02 fulfillment verification requires its own isolated Unity process.'
}
if ($U0T03Retail -and $Mode -ne 'edit') {
    throw 'U0 T03 retail verification requires isolated edit mode.'
}
if ($U0T03Retail -and $ExecuteMethod -ne 'RetailPurchaseT03DebugScenarios.RunFromBatch') {
    throw 'U0 T03 retail verification requires its dedicated batch entry.'
}
if ($U0T03Retail -and ($FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality -or $U0E01RegistryInventory -or $U0E02HusbandryOutcome -or $U0E03CropTreatment -or $U0E06OperatingDay -or $U0E07StockPolicySale -or $U0E08CropAction -or $U0E09EnvironmentExposure -or $U0E10WorldResource -or $U0E11Wildlife -or $U0E11Capture -or $U0E11Role -or $U0E11Butchery -or $U0E12Infrastructure -or $U0E13Automation -or $U0T01FacilityShop -or $U0T02Fulfillment -or $SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $SourceV9FacilityIntake -or $SourceV9EquipmentReplay -or $SourceV9SkillReplay -or $SourceV9TraitReplay)) {
    throw 'U0 T03 retail verification requires its own isolated Unity process.'
}
if ($U0T04RetailRecovery -and $Mode -ne 'edit') {
    throw 'U0 T04 retail-recovery verification requires isolated edit mode.'
}
if ($U0T04RetailRecovery -and $ExecuteMethod -ne 'RetailPurchaseRecoveryB0T04DebugScenarios.RunT04FromBatch') {
    throw 'U0 T04 retail-recovery verification requires its dedicated batch entry.'
}
if ($U0T04RetailRecovery -and ($FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality -or $U0E01RegistryInventory -or $U0E02HusbandryOutcome -or $U0E03CropTreatment -or $U0E06OperatingDay -or $U0E07StockPolicySale -or $U0E08CropAction -or $U0E09EnvironmentExposure -or $U0E10WorldResource -or $U0E11Wildlife -or $U0E11Capture -or $U0E11Role -or $U0E11Butchery -or $U0E12Infrastructure -or $U0E13Automation -or $U0T01FacilityShop -or $U0T02Fulfillment -or $U0T03Retail -or $SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $SourceV9FacilityIntake -or $SourceV9EquipmentReplay -or $SourceV9SkillReplay -or $SourceV9TraitReplay)) {
    throw 'U0 T04 retail-recovery verification requires its own isolated Unity process.'
}
if ($U0T07RegionalSupply -and $Mode -ne 'edit') {
    throw 'U0 T07 regional-supply verification requires isolated edit mode.'
}
if ($U0T07RegionalSupply -and $ExecuteMethod -ne 'RegionalSupplyContractTransferOutboxDebugScenarios.RunT07FromBatch') {
    throw 'U0 T07 regional-supply verification requires its dedicated batch entry.'
}
if ($U0T07RegionalSupply -and ($FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality -or $U0E01RegistryInventory -or $U0E02HusbandryOutcome -or $U0E03CropTreatment -or $U0E06OperatingDay -or $U0E07StockPolicySale -or $U0E08CropAction -or $U0E09EnvironmentExposure -or $U0E10WorldResource -or $U0E11Wildlife -or $U0E11Capture -or $U0E11Role -or $U0E11Butchery -or $U0E12Infrastructure -or $U0E13Automation -or $U0T01FacilityShop -or $U0T02Fulfillment -or $U0T03Retail -or $U0T04RetailRecovery -or $SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $SourceV9FacilityIntake -or $SourceV9EquipmentReplay -or $SourceV9SkillReplay -or $SourceV9TraitReplay)) {
    throw 'U0 T07 regional-supply verification requires its own isolated Unity process.'
}
if ($U0D01CombatEvidence -and $Mode -ne 'edit') {
    throw 'U0 D01 combat-evidence verification requires isolated edit mode.'
}
if ($U0D01CombatEvidence -and $ExecuteMethod -ne 'MigratedProducerCombatDefenseEntryE2EDebugScenarios.RunD01FromBatch') {
    throw 'U0 D01 combat-evidence verification requires its dedicated batch entry.'
}
if ($U0D01CombatEvidence -and ($FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality -or $U0E01RegistryInventory -or $U0E02HusbandryOutcome -or $U0E03CropTreatment -or $U0E06OperatingDay -or $U0E07StockPolicySale -or $U0E08CropAction -or $U0E09EnvironmentExposure -or $U0E10WorldResource -or $U0E11Wildlife -or $U0E11Capture -or $U0E11Role -or $U0E11Butchery -or $U0E12Infrastructure -or $U0E13Automation -or $U0T01FacilityShop -or $U0T02Fulfillment -or $U0T03Retail -or $U0T04RetailRecovery -or $U0T07RegionalSupply -or $SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $SourceV9FacilityIntake -or $SourceV9EquipmentReplay -or $SourceV9SkillReplay -or $SourceV9TraitReplay)) {
    throw 'U0 D01 combat-evidence verification requires its own isolated Unity process.'
}
if ($U0D02SharedCombat -and $Mode -ne 'edit') {
    throw 'U0 D02 shared-combat verification requires isolated edit mode.'
}
if ($U0D02SharedCombat -and $ExecuteMethod -ne 'MigratedProducerCombatDefenseEntryE2EDebugScenarios.RunD02FromBatch') {
    throw 'U0 D02 shared-combat verification requires its dedicated batch entry.'
}
if ($U0D02SharedCombat -and ($FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality -or $U0E01RegistryInventory -or $U0E02HusbandryOutcome -or $U0E03CropTreatment -or $U0E06OperatingDay -or $U0E07StockPolicySale -or $U0E08CropAction -or $U0E09EnvironmentExposure -or $U0E10WorldResource -or $U0E11Wildlife -or $U0E11Capture -or $U0E11Role -or $U0E11Butchery -or $U0E12Infrastructure -or $U0E13Automation -or $U0T01FacilityShop -or $U0T02Fulfillment -or $U0T03Retail -or $U0T04RetailRecovery -or $U0T07RegionalSupply -or $U0D01CombatEvidence -or $SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $SourceV9FacilityIntake -or $SourceV9EquipmentReplay -or $SourceV9SkillReplay -or $SourceV9TraitReplay)) {
    throw 'U0 D02 shared-combat verification requires its own isolated Unity process.'
}
if ($U0D03CombatCausal -and $Mode -ne 'edit') {
    throw 'U0 D03 combat-causal verification requires isolated edit mode.'
}
if ($U0D03CombatCausal -and $ExecuteMethod -ne 'MigratedProducerCombatDefenseEntryE2EDebugScenarios.RunD03FromBatch') {
    throw 'U0 D03 combat-causal verification requires its dedicated batch entry.'
}
if ($U0D03CombatCausal -and ($FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality -or $U0E01RegistryInventory -or $U0E02HusbandryOutcome -or $U0E03CropTreatment -or $U0E06OperatingDay -or $U0E07StockPolicySale -or $U0E08CropAction -or $U0E09EnvironmentExposure -or $U0E10WorldResource -or $U0E11Wildlife -or $U0E11Capture -or $U0E11Role -or $U0E11Butchery -or $U0E12Infrastructure -or $U0E13Automation -or $U0T01FacilityShop -or $U0T02Fulfillment -or $U0T03Retail -or $U0T04RetailRecovery -or $U0T07RegionalSupply -or $U0D01CombatEvidence -or $U0D02SharedCombat -or $SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $SourceV9FacilityIntake -or $SourceV9EquipmentReplay -or $SourceV9SkillReplay -or $SourceV9TraitReplay)) {
    throw 'U0 D03 combat-causal verification requires its own isolated Unity process.'
}
if ($W01FireIdentity -and -not $W01WorkTerminalJoin) { throw 'W01 fire identity requires W01 work terminal join verification.' }
if ($W01FireWaterIdentity -and -not $W01FireIdentity) { throw 'W01 water fire identity requires W01 fire identity verification.' }
if ($W01WorkTerminalJoin -and $Mode -ne 'play') { throw 'W01 work terminal join requires isolated play mode.' }
if ($W01WorkTerminalJoin -and ($FacilityContinuation -or $MedicalContinuation -or $ShopContinuation -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity)) {
    throw 'W01 work terminal join requires its own isolated Unity process.'
}
if ($PersistentCookPause -and ($FacilityContinuation -or $MedicalContinuation -or $ShopContinuation -or $MealContinuation -or $DrinkContinuation)) {
    throw 'Persistent Cook pause verification is mutually exclusive with continuation verification.'
}
if (($EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity) -and $Mode -ne 'play') {
    throw 'Focused live verification requires the owned isolated PlayMode facade.'
}
if ($W05HarvestIdentity -and $W05SurgeryIdentity) {
    throw 'W05 harvest and surgery identity verifications require separate Unity processes.'
}
if (($W05HarvestIdentity -or $W05SurgeryIdentity) -and ($FacilityContinuation -or $MedicalContinuation -or $ShopContinuation -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause)) {
    throw 'W05 identity verification is mutually exclusive with other focused verification modes.'
}
if ($EEC03DriftCargo -and ($FacilityContinuation -or $MedicalContinuation -or $ShopContinuation -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity)) {
    throw 'EEC03 drift-cargo verification is mutually exclusive with other focused verification modes.'
}
if ($EEC05Apparel -and ($FacilityContinuation -or $MedicalContinuation -or $ShopContinuation -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $W05HarvestIdentity -or $W05SurgeryIdentity)) {
    throw 'EEC05 apparel verification is mutually exclusive with other focused verification modes.'
}
$qaResumePath = $null
$qaResumeHash = $null
$qaHasResumeWorld = $PSBoundParameters.ContainsKey('CampaignResumeWorld')
$qaHasResumeHash = $PSBoundParameters.ContainsKey('CampaignResumeSha256')
if ($qaHasResumeWorld -ne $qaHasResumeHash) {
    throw 'Campaign resume world and SHA-256 must be provided together.'
}
if ($U0BDCD01DoorTraversal -and $qaHasResumeWorld) {
    throw 'U0 BDC-D01 door-traversal verification requires a fresh fixture and cannot resume a campaign world.'
}
if ($ExecuteMethod -in @('DungeonLiveNarrativeCampaignRunner.RequestSkillBlueprintHaulDiagnostics',
        'DungeonLiveNarrativeCampaignRunner.RequestConstructionHaulDiagnostics',
        'DungeonLiveNarrativeCampaignRunner.RequestSkillResearchDiagnostics',
        'DungeonLiveNarrativeCampaignRunner.RequestSkillBattleDiagnostics',
        'DungeonLiveNarrativeCampaignRunner.RequestCompletedCraftDeliveryDiagnostics',
        'DungeonLiveNarrativeCampaignRunner.RequestOriginalSkillPendingDiagnostics')) {
    if ($Mode -ne 'campaign' -or -not $qaHasResumeWorld) {
        throw 'Focused skill admission diagnostics require a hashed campaign checkpoint.'
    }
    $qaHaulAllowedParameters = @('ExecuteMethod', 'OutputDirectory', 'Mode', 'UnityExecutable',
        'CampaignResumeWorld', 'CampaignResumeSha256')
    if ($ExecuteMethod -eq 'DungeonLiveNarrativeCampaignRunner.RequestSkillBattleDiagnostics') {
        $qaHaulAllowedParameters += 'SkillBattleActorId', 'SkillBattleSkillId', 'SkillReplayHoldNaturalInvasion', 'SkillNormalWeaponPreparation', 'SkillNonTargetHungerFixture', 'SkillEquipmentPreparationFixture', 'SkillPreparationEnvironmentFixture', 'SkillPreparationInstantWorkFixture', 'SkillThirtyFoodPreparationFixture', 'SkillRecruitmentHistoryCapacityFixture', 'SkillOneCraftingBenchFixture'
    }
    if (@($PSBoundParameters.Keys | Where-Object { $_ -notin $qaHaulAllowedParameters }).Count -ne 0) {
        throw 'Focused skill admission diagnostics cannot be combined with intake/replay/other verification inputs.'
    }
}
if ($U0E02HusbandryOutcome -and $qaHasResumeWorld) {
    throw 'U0 E02 husbandry outcome verification requires a fresh fixture.'
}
if ($U0E03CropTreatment -and $qaHasResumeWorld) {
    throw 'U0 E03 crop-treatment verification requires a fresh fixture.'
}
if ($U0E06OperatingDay -and $qaHasResumeWorld) {
    throw 'U0 E06 operating-day verification requires a fresh fixture.'
}
if ($U0E07StockPolicySale -and $qaHasResumeWorld) {
    throw 'U0 E07 stock-policy sale verification requires a fresh fixture.'
}
if ($U0E08CropAction -and $qaHasResumeWorld) {
    throw 'U0 E08 crop-action verification requires a fresh fixture.'
}
if ($U0EEC01CropFailure -and $qaHasResumeWorld) {
    throw 'U0 EEC01 crop-failure verification requires a fresh fixture.'
}
if ($U0EEC02WildlifeArrival -and $qaHasResumeWorld) {
    throw 'U0 EEC02 wildlife-arrival verification requires a fresh fixture.'
}
if ($U0EEC06CombatCraft -and $qaHasResumeWorld) {
    throw 'U0 EEC06 combat-craft verification requires a fresh fixture.'
}
if ($U0G01RecruitDelivery -and $qaHasResumeWorld) {
    throw 'U0 G01 recruit-delivery verification requires a fresh fixture.'
}
if ($U0G02EmploymentSettlement -and $qaHasResumeWorld) {
    throw 'U0 G02 employment-settlement verification requires a fresh fixture.'
}
if ($U0G03PolicyRevision -and $qaHasResumeWorld) {
    throw 'U0 G03 policy-revision verification requires a fresh fixture.'
}
if ($U0G04Application -and $qaHasResumeWorld) {
    throw 'U0 G04 V20 application verification requires a fresh fixture.'
}
if ($U0G06MilestonePressure -and $qaHasResumeWorld) {
    throw 'U0 G06 milestone-pressure verification requires a fresh fixture.'
}
if ($U0G07KnowledgeCompletion -and $qaHasResumeWorld) {
    throw 'U0 G07 knowledge-completion verification requires a fresh fixture.'
}
if ($U0G09ExternalInfluence -and $qaHasResumeWorld) {
    throw 'U0 G09 external-influence verification requires a fresh fixture.'
}
if ($U0G10StaffDiscontent -and $qaHasResumeWorld) {
    throw 'U0 G10 staff-discontent verification requires a fresh fixture.'
}
if ($U0G11DurableEquipment -and $qaHasResumeWorld) {
    throw 'U0 G11 durable-equipment verification requires a fresh fixture.'
}
if ($U0G12GolemUpkeep -and $qaHasResumeWorld) {
    throw 'U0 G12 Golem-upkeep verification requires a fresh fixture.'
}
if ($U0G14TraitAnalysis -and $qaHasResumeWorld) {
    throw 'U0 G14 trait-analysis verification requires a fresh fixture.'
}
if ($U0C01O02FuneralLineage -and $qaHasResumeWorld) {
    throw 'U0 C:O01-O02 funeral/lineage verification requires a fresh fixture.'
}
if ($TerminalDeathSave -and $qaHasResumeWorld) {
    throw 'Terminal death/save verification requires a fresh fixture.'
}
if ($U0C03ProductionLoss -and $qaHasResumeWorld) {
    throw 'U0 C:O03 production-loss verification requires a fresh fixture.'
}
if ($U0C04O05CareerObservation -and $qaHasResumeWorld) {
    throw 'U0 C:O04-O05 career-observation verification requires a fresh fixture.'
}
if ($U0CN01AmbitionTransition -and $qaHasResumeWorld) {
    throw 'U0 C:N01 ambition-transition verification requires a fresh fixture.'
}
if ($U0CR01FamilyPlanning -and $qaHasResumeWorld) {
    throw 'U0 C:R01 family-planning verification requires a fresh fixture.'
}
if ($U0CR02R05Reproduction -and $qaHasResumeWorld) {
    throw 'U0 C:R02-R05 reproduction verification requires a fresh fixture.'
}
if ($U0CR07Birth -and $qaHasResumeWorld) {
    throw 'U0 C:R07 birth verification requires a fresh fixture.'
}
if ($U0CR09Household -and $qaHasResumeWorld) {
    throw 'U0 C:R09 household verification requires a fresh fixture.'
}
if ($U0CA01AgeAnatomy -and $qaHasResumeWorld) {
    throw 'U0 C:A01 age-anatomy verification requires a fresh fixture.'
}
if ($U0CA02DiseaseAnatomy -and $qaHasResumeWorld) {
    throw 'U0 C:A02 disease-anatomy verification requires a fresh fixture.'
}
if ($U0UI01DefenseService -and $qaHasResumeWorld) {
    throw 'U0 UI-01 defense-service verification requires a fresh fixture.'
}
if ($U0UI02ResidentEvacuation -and $qaHasResumeWorld) {
    throw 'U0 UI-02 resident-evacuation verification requires a fresh fixture.'
}
if ($U0CON01ConsumablesPolicy -and $qaHasResumeWorld) {
    throw 'U0 CON01 consumables-policy verification requires a fresh fixture.'
}
if ($U0BDCS01SpaceExpansion -and $qaHasResumeWorld) {
    throw 'U0 BDC-S01 space-expansion verification requires a fresh fixture.'
}
if ($U0BDCD01DoorPolicy -and $qaHasResumeWorld) {
    throw 'U0 BDC-D01 door-policy verification requires a fresh fixture.'
}
if ($U0E09EnvironmentExposure -and $qaHasResumeWorld) {
    throw 'U0 E09 environment-exposure verification requires a fresh fixture.'
}
if ($U0E10WorldResource -and $qaHasResumeWorld) {
    throw 'U0 E10 world-resource verification requires a fresh fixture.'
}
if ($U0E11Wildlife -and $qaHasResumeWorld) {
    throw 'U0 E11 wildlife verification requires a fresh fixture.'
}
if (($U0E11Capture -or $U0E11Role -or $U0E11Butchery) -and $qaHasResumeWorld -and -not ($qaFeedColdResume -or $qaHusbandryPendingColdResume)) {
    throw 'U0 E11 focused verification requires a fresh fixture.'
}
if ($U0E12Infrastructure -and $qaHasResumeWorld) {
    throw 'U0 E12 infrastructure verification requires a fresh fixture.'
}
if ($U0E13Automation -and $qaHasResumeWorld) {
    throw 'U0 E13 automation verification requires a fresh fixture.'
}
if ($U0T01FacilityShop -and $qaHasResumeWorld) {
    throw 'U0 T01 facility-shop verification requires a fresh fixture.'
}
if ($U0T02Fulfillment -and $qaHasResumeWorld) {
    throw 'U0 T02 fulfillment verification requires a fresh fixture.'
}
if ($U0T03Retail -and $qaHasResumeWorld) {
    throw 'U0 T03 retail verification requires a fresh fixture.'
}
if ($U0T04RetailRecovery -and $qaHasResumeWorld) {
    throw 'U0 T04 retail-recovery verification requires a fresh fixture.'
}
if ($U0T07RegionalSupply -and $qaHasResumeWorld) {
    throw 'U0 T07 regional-supply verification requires a fresh fixture.'
}
if ($U0D01CombatEvidence -and $qaHasResumeWorld) {
    throw 'U0 D01 combat-evidence verification requires a fresh fixture.'
}
if ($U0D02SharedCombat -and $qaHasResumeWorld) {
    throw 'U0 D02 shared-combat verification requires a fresh fixture.'
}
if ($U0D03CombatCausal -and $qaHasResumeWorld) {
    throw 'U0 D03 combat-causal verification requires a fresh fixture.'
}
if ($MedicalContinuation -and $qaHasResumeWorld) {
    throw 'Medical continuation requires a fresh fixture and cannot resume a campaign world.'
}
if ($W03MedicalLabor -and $qaHasResumeWorld) {
    throw 'W03 medical labor verification cannot resume a campaign world.'
}
if ($W04RepairLive -and $qaHasResumeWorld) {
    throw 'W04 repair live verification cannot resume a campaign world.'
}
if ($W06ResourceCausality -and $qaHasResumeWorld) {
    throw 'W06 resource causality verification cannot resume a campaign world.'
}
if ($SourceV9SkillIntake -and $Mode -ne 'campaign') {
    throw 'Source-v9 skill intake requires isolated campaign mode.'
}
if ($SourceV9SkillIntake -and ($FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality)) {
    throw 'Source-v9 skill intake requires its own fresh isolated Unity process.'
}
if ($SourceV9SkillIntake -and $qaHasResumeWorld -and -not $SourceV9SkillExpeditionActor) {
    throw 'Resumed source-v9 skill intake requires explicit normal expedition staff selection.'
}
if ($SourceV9EquipmentIntake -and $Mode -ne 'campaign') {
    throw 'Source-v9 equipment intake requires isolated campaign mode.'
}
if ($SourceV9EquipmentIntake -and -not $qaHasResumeWorld) {
    throw 'Source-v9 equipment intake requires an explicit official campaign checkpoint and SHA-256.'
}
if ($SourceV9EquipmentIntake -and ($SourceV9SkillIntake -or $FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality)) {
    throw 'Source-v9 equipment intake requires its own isolated Unity process.'
}
if ($SourceV9FacilityIntake -and $Mode -ne 'campaign') {
    throw 'Source-v9 facility intake requires isolated campaign mode.'
}
if ($SourceV9FacilityIntake -and -not $qaHasResumeWorld) {
    throw 'Source-v9 facility intake requires an explicit official campaign checkpoint and SHA-256.'
}
if ($SourceV9FacilityIntake -and ($SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality)) {
    throw 'Source-v9 facility intake requires its own isolated Unity process.'
}
if ($SourceV9EquipmentReplay -and $Mode -ne 'campaign') {
    throw 'Source-v9 equipment replay requires isolated campaign mode.'
}
if ($SourceV9EquipmentReplay -and -not $qaHasResumeWorld) {
    throw 'Source-v9 equipment replay requires its source package checkpoint and SHA-256.'
}
if ($SourceV9EquipmentReplay -and ($SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $SourceV9FacilityIntake -or $SourceV9TraitReplay -or $SourceV9FacilityReplay -or $FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality)) {
    throw 'Source-v9 equipment replay requires its own isolated Unity process.'
}
if ($SourceV9SkillReplay -and $Mode -ne 'campaign') {
    throw 'Source-v9 skill replay requires isolated campaign mode.'
}
$qaSkillSupportContinuation = -not [string]::IsNullOrEmpty($SkillBattleActorId)
if ($qaSkillSupportContinuation -ne (-not [string]::IsNullOrEmpty($SkillBattleSkillId)) -or
    ($qaSkillSupportContinuation -and ($Mode -ne 'campaign' -or -not $qaHasResumeWorld -or
        $ExecuteMethod -ne 'DungeonLiveNarrativeCampaignRunner.RequestSkillBattleDiagnostics'))) {
    throw 'Selected support continuation requires its explicit actor/skill pair, hashed world and focused campaign entry.'
}
if ($SkillReplayHoldNaturalInvasion -and -not ($SourceV9SkillReplay -or $qaSkillSupportContinuation)) {
    throw 'Natural invasion hold is an explicit controlled skill replay/selected-support fixture only.'
}
if ($SkillNormalWeaponPreparation -and -not ($SourceV9SkillReplay -or $qaSkillSupportContinuation)) {
    throw 'Normal weapon preparation requires the explicit support skill replay/continuation.'
}
if ($SkillNonTargetHungerFixture) {
    $qaNonTargetHungerReplayRoute = $SourceV9SkillReplay -and
        $ExecuteMethod -eq 'DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch'
    $qaNonTargetHungerSupportRoute = $qaSkillSupportContinuation -and
        $ExecuteMethod -eq 'DungeonLiveNarrativeCampaignRunner.RequestSkillBattleDiagnostics'
    if ($Mode -ne 'campaign' -or -not $qaHasResumeWorld -or
        -not $SkillNormalWeaponPreparation -or -not $SkillReplayHoldNaturalInvasion -or
        -not ($qaNonTargetHungerReplayRoute -xor $qaNonTargetHungerSupportRoute)) {
        throw 'Non-target hunger fixture requires a hashed campaign resume with hold and normal preparation on exactly one owned skill replay/support route.'
    }
}
if ($SkillEquipmentPreparationFixture) {
    $qaSkillEquipmentReplayRoute = $SourceV9SkillReplay -and
        $ExecuteMethod -eq 'DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch'
    $qaSkillEquipmentSupportRoute = $qaSkillSupportContinuation -and
        $ExecuteMethod -eq 'DungeonLiveNarrativeCampaignRunner.RequestSkillBattleDiagnostics'
    $qaSkillEquipmentPreparationAllowedParameters = @(
        'ExecuteMethod',
        'OutputDirectory',
        'Mode',
        'UnityExecutable',
        'CampaignResumeWorld',
        'CampaignResumeSha256',
        'SkillReplayHoldNaturalInvasion',
        'SkillNormalWeaponPreparation',
        'SkillNonTargetHungerFixture',
        'SkillEquipmentPreparationFixture',
        'SkillPreparationEnvironmentFixture',
        'SkillPreparationInstantWorkFixture')
    if ($qaSkillEquipmentReplayRoute) {
        $qaSkillEquipmentPreparationAllowedParameters += 'SourceV9SkillReplay',
            'SourceV9SkillCandidateIndex', 'SourceV9ReplayPackage', 'SourceV9ReplayResponse'
    } elseif ($qaSkillEquipmentSupportRoute) {
        $qaSkillEquipmentPreparationAllowedParameters += 'SkillBattleActorId',
            'SkillBattleSkillId', 'SkillRecruitmentHistoryCapacityFixture'
    }
    if (@($PSBoundParameters.Keys | Where-Object { $_ -notin $qaSkillEquipmentPreparationAllowedParameters }).Count -ne 0) {
        throw 'Skill equipment preparation fixture cannot combine replay/support routes or unrelated gameplay selectors, including explicitly false selectors.'
    }
    if ($Mode -ne 'campaign' -or -not $qaHasResumeWorld -or
        -not ($qaSkillEquipmentReplayRoute -xor $qaSkillEquipmentSupportRoute) -or
        -not $SkillNormalWeaponPreparation -or -not $SkillReplayHoldNaturalInvasion -or
        -not $SkillNonTargetHungerFixture) {
        throw 'Skill equipment preparation fixture requires exactly one hashed owned skill replay/support route with hold, normal preparation and non-target hunger fixture.'
    }
    if ($qaSkillEquipmentSupportRoute -and
        (-not $PSBoundParameters.ContainsKey('SkillBattleActorId') -or
         -not $PSBoundParameters.ContainsKey('SkillBattleSkillId'))) {
        throw 'Support preparation requires its explicit actor and skill pair.'
    }
    if ($qaSkillEquipmentReplayRoute -and
        (-not $PSBoundParameters.ContainsKey('SourceV9SkillCandidateIndex') -or
         -not $PSBoundParameters.ContainsKey('SourceV9ReplayPackage') -or
         -not $PSBoundParameters.ContainsKey('SourceV9ReplayResponse'))) {
        throw 'Replay preparation requires its explicit source package, response package and candidate index.'
    }
}
if ($SkillPreparationEnvironmentFixture -and -not $SkillEquipmentPreparationFixture) {
    throw 'Skill preparation environment fixture requires the explicit equipment preparation fixture and its focused support scope.'
}
if ($SkillPreparationInstantWorkFixture -and
    (-not $SkillEquipmentPreparationFixture -or -not $SkillPreparationEnvironmentFixture)) {
    throw 'Skill preparation instant-work fixture requires the explicit equipment and environment preparation fixtures.'
}
if ($SkillThirtyFoodPreparationFixture) {
    $qaThirtyFoodAllowedParameters = @(
        'ExecuteMethod',
        'OutputDirectory',
        'Mode',
        'UnityExecutable',
        'CampaignResumeWorld',
        'CampaignResumeSha256',
        'SkillReplayHoldNaturalInvasion',
        'SkillNormalWeaponPreparation',
        'SkillThirtyFoodPreparationFixture',
        'SkillBattleActorId',
        'SkillBattleSkillId')
    if (@($PSBoundParameters.Keys | Where-Object { $_ -notin $qaThirtyFoodAllowedParameters }).Count -ne 0) {
        throw 'Thirty-food preparation fixture cannot be combined with replay, intake or another gameplay selector, including an explicitly false selector.'
    }
    $qaThirtyFoodActorId = 'character:staff:1:01'
    $qaThirtyFoodSkillId = 'skill:character:staff:1:01:Active:5:r1:g1:l1:formula:sha256:b9a8e87f26ed7303b80b49027e75770dae3e042142e10fd623cfbaf8b3e37508'
    $qaThirtyFoodResumeHash = '54FFB75B344E07FF0DCCC45C0901FF604E0414633F2992DD14BA054756422ADA'
    if (-not $qaSkillSupportContinuation -or -not $SkillReplayHoldNaturalInvasion -or
        -not $SkillNormalWeaponPreparation -or
        $Mode -ne 'campaign' -or
        $ExecuteMethod -ne 'DungeonLiveNarrativeCampaignRunner.RequestSkillBattleDiagnostics' -or
        -not $qaHasResumeWorld -or
        -not [string]::Equals($SkillBattleActorId, $qaThirtyFoodActorId, [StringComparison]::Ordinal) -or
        -not [string]::Equals($SkillBattleSkillId, $qaThirtyFoodSkillId, [StringComparison]::Ordinal) -or
        -not [string]::Equals($CampaignResumeSha256, $qaThirtyFoodResumeHash, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Thirty-food preparation fixture requires only the exact selected support continuation, natural-invasion hold, normal weapon preparation and approved r26 checkpoint.'
    }
}
if ($SkillRecruitmentHistoryCapacityFixture) {
    $qaRecruitmentHistoryAllowedParameters = @(
        'ExecuteMethod',
        'OutputDirectory',
        'Mode',
        'UnityExecutable',
        'CampaignResumeWorld',
        'CampaignResumeSha256',
        'SkillBattleActorId',
        'SkillBattleSkillId',
        'SkillReplayHoldNaturalInvasion',
        'SkillNormalWeaponPreparation',
        'SkillNonTargetHungerFixture',
        'SkillEquipmentPreparationFixture',
        'SkillPreparationEnvironmentFixture',
        'SkillPreparationInstantWorkFixture',
        'SkillRecruitmentHistoryCapacityFixture')
    if (@($PSBoundParameters.Keys | Where-Object { $_ -notin $qaRecruitmentHistoryAllowedParameters }).Count -ne 0) {
        throw 'Recruitment-history capacity fixture cannot be combined with replay, intake, food, bench or another gameplay selector, including an explicitly false selector.'
    }
    $qaRecruitmentHistoryActorId = 'character:staff:1:01'
    $qaRecruitmentHistorySkillId = 'skill:character:staff:1:01:Active:5:r1:g1:l1:formula:sha256:b9a8e87f26ed7303b80b49027e75770dae3e042142e10fd623cfbaf8b3e37508'
    if (-not $qaSkillSupportContinuation -or -not $SkillReplayHoldNaturalInvasion -or
        -not $SkillNormalWeaponPreparation -or
        $Mode -ne 'campaign' -or
        $ExecuteMethod -ne 'DungeonLiveNarrativeCampaignRunner.RequestSkillBattleDiagnostics' -or
        -not $qaHasResumeWorld -or
        -not [string]::Equals($SkillBattleActorId, $qaRecruitmentHistoryActorId, [StringComparison]::Ordinal) -or
        -not [string]::Equals($SkillBattleSkillId, $qaRecruitmentHistorySkillId, [StringComparison]::Ordinal)) {
        throw 'Recruitment-history capacity fixture requires only the exact selected support continuation, natural-invasion hold, normal weapon preparation and an explicit hashed checkpoint.'
    }
}
if ($SkillOneCraftingBenchFixture -and -not ($SkillNormalWeaponPreparation -and
        ($SourceV9SkillReplay -or $qaSkillSupportContinuation))) {
    throw 'One approved crafting bench requires explicit isolated normal support weapon preparation.'
}
if (($SourceV9SkillReplay -and $SourceV9SkillCandidateIndex -notin 0,1,2) -or
    (-not $SourceV9SkillReplay -and $SourceV9SkillCandidateIndex -ne -1)) {
    throw 'Skill replay requires an explicit simulated-player candidate index (0, 1 or 2); other modes must omit it.'
}
if ($SourceV9SkillReplay -and -not $qaHasResumeWorld) {
    throw 'Source-v9 skill replay requires its source package checkpoint and SHA-256.'
}
if ($SourceV9SkillReplay -and ($SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $SourceV9FacilityIntake -or $SourceV9EquipmentReplay -or $SourceV9TraitReplay -or $SourceV9FacilityReplay -or $FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality)) {
    throw 'Source-v9 skill replay requires its own isolated Unity process.'
}
if ($SourceV9TraitReplay -and $Mode -ne 'campaign') {
    throw 'Source-v9 trait replay requires isolated campaign mode.'
}
if ($SourceV9TraitReplay -and -not $qaHasResumeWorld) {
    throw 'Source-v9 trait replay requires its source package checkpoint and SHA-256.'
}
if ($SourceV9TraitReplay -and ($SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $SourceV9FacilityIntake -or $SourceV9EquipmentReplay -or $SourceV9SkillReplay -or $SourceV9FacilityReplay -or $FacilityContinuation -or $FacilityContinuationRestRequest -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive -or $W06ResourceCausality)) {
    throw 'Source-v9 trait replay requires its own isolated Unity process.'
}
$qaSourceV9FacilityReplayConflicts = $SourceV9SkillIntake -or $SourceV9EquipmentIntake `
    -or $SourceV9FacilityIntake -or $SourceV9EquipmentReplay -or $SourceV9SkillReplay `
    -or $SourceV9TraitReplay -or $FacilityContinuation -or $FacilityContinuationRestRequest `
    -or $MedicalContinuation -or $ShopContinuation -or $W01CheckoutCleanup `
    -or $MealContinuation -or $DrinkContinuation -or $PersistentCookPause `
    -or $EEC03DriftCargo -or $EEC05Apparel -or $W05HarvestIdentity `
    -or $W05SurgeryIdentity -or $W01SurgeryPublicationRecovery `
    -or $W01SurgeryClinicalFailure -or $W01WorkTerminalJoin -or $W01FireIdentity `
    -or $W01FireWaterIdentity -or $W03MedicalLabor -or $W04RepairLive `
    -or $W06ResourceCausality
if ($SourceV9FacilityReplay -and $Mode -ne 'campaign') {
    throw 'Source-v9 facility replay requires isolated campaign mode.'
}
if ($SourceV9FacilityReplay -and -not $qaHasResumeWorld) {
    throw 'Source-v9 facility replay requires its source package checkpoint and SHA-256.'
}
if ($SourceV9FacilityReplay -and $qaSourceV9FacilityReplayConflicts) {
    throw 'Source-v9 facility replay requires its own isolated Unity process.'
}
$qaSourceV9InventoryPath = $null
$qaSourceV9MechanicsManifestPath = $null
if ($SourceV9SkillIntake -or $SourceV9EquipmentIntake -or $SourceV9FacilityIntake) {
    if ([string]::IsNullOrWhiteSpace($SourceV9Inventory) -or [string]::IsNullOrWhiteSpace($SourceV9MechanicsManifest) -or [string]::IsNullOrWhiteSpace($SourceV9PackageId)) {
        throw 'Source-v9 intake requires inventory, mechanics manifest and package ID.'
    }
    if ($SourceV9PackageId -notmatch '^[A-Za-z0-9][A-Za-z0-9._:-]{1,127}$') {
        throw 'Source-v9 package ID has invalid syntax.'
    }
    $qaProjectPrefix = $qaProject.TrimEnd('\') + '\'
    $qaSourceV9InventoryPath = [IO.Path]::GetFullPath($SourceV9Inventory)
    $qaSourceV9MechanicsManifestPath = [IO.Path]::GetFullPath($SourceV9MechanicsManifest)
    foreach ($qaSourceV9Input in @($qaSourceV9InventoryPath, $qaSourceV9MechanicsManifestPath)) {
        if (-not $qaSourceV9Input.StartsWith($qaProjectPrefix, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $qaSourceV9Input -PathType Leaf)) {
            throw 'Source-v9 inputs must be existing files inside this project.'
        }
        $qaSourceV9Item = Get-Item -LiteralPath $qaSourceV9Input -Force
        if ($qaSourceV9Item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'Reparse-point source-v9 inputs are not allowed.'
        }
    }
} elseif (-not [string]::IsNullOrWhiteSpace($SourceV9Inventory) -or -not [string]::IsNullOrWhiteSpace($SourceV9MechanicsManifest) -or -not [string]::IsNullOrWhiteSpace($SourceV9PackageId)) {
    throw 'Source-v9 inputs require a source-v9 intake mode.'
}
$qaSourceV9ReplayPackagePath = $null
$qaSourceV9ReplayResponsePath = $null
if ($SourceV9EquipmentReplay -or $SourceV9SkillReplay -or $SourceV9TraitReplay -or $SourceV9FacilityReplay) {
    if ([string]::IsNullOrWhiteSpace($SourceV9ReplayPackage) -or [string]::IsNullOrWhiteSpace($SourceV9ReplayResponse)) {
        throw 'Source-v9 replay requires source and response package directories.'
    }
    $qaSourceV9ReplayPackagePath = [IO.Path]::GetFullPath($SourceV9ReplayPackage).TrimEnd('\')
    $qaSourceV9ReplayResponsePath = [IO.Path]::GetFullPath($SourceV9ReplayResponse).TrimEnd('\')
    foreach ($qaReplayInput in @($qaSourceV9ReplayPackagePath, $qaSourceV9ReplayResponsePath)) {
        if (-not $qaReplayInput.StartsWith($qaRoot, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $qaReplayInput -PathType Container)) {
            throw 'Source-v9 replay packages must be existing directories inside this project Artifacts/QA.'
        }
        for ($qaCursor = Get-Item -LiteralPath $qaReplayInput -Force; $null -ne $qaCursor; $qaCursor = $qaCursor.Parent) {
            if ($qaCursor.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'Reparse-point source-v9 replay package paths are not allowed.'
            }
        }
    }
} elseif (-not [string]::IsNullOrWhiteSpace($SourceV9ReplayPackage) -or -not [string]::IsNullOrWhiteSpace($SourceV9ReplayResponse)) {
    throw 'Source-v9 replay package inputs require replay mode.'
}
if ($ShopContinuation -and $qaHasResumeWorld) {
    throw 'Shop continuation requires a fresh fixture and cannot resume a campaign world.'
}
if ($DrinkContinuation -and -not $qaHasResumeWorld) {
    throw 'Drink continuation requires a campaign resume world and SHA-256.'
}
if ($qaHasResumeWorld) {
    if ([string]::IsNullOrWhiteSpace($CampaignResumeWorld) -or [string]::IsNullOrWhiteSpace($CampaignResumeSha256)) {
        throw 'Campaign resume world and SHA-256 cannot be empty.'
    }
    if ($Mode -ne 'campaign' -and -not ($qaFeedColdResume -or $qaHusbandryPendingColdResume)) { throw 'Resume inputs require campaign mode or a dedicated cold entry.' }
    if (-not [IO.Path]::IsPathRooted($CampaignResumeWorld)) { throw 'Campaign resume world must be an absolute JSON file path.' }
    $qaResumePath = [IO.Path]::GetFullPath($CampaignResumeWorld)
    if (-not $qaResumePath.StartsWith($qaRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Campaign resume world must be inside this project Artifacts/QA.'
    }
    if (-not [string]::Equals([IO.Path]::GetExtension($qaResumePath), '.json', [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $qaResumePath -PathType Leaf)) {
        throw 'Campaign resume world must be an existing JSON file.'
    }
    $qaResumeItem = Get-Item -LiteralPath $qaResumePath -Force
    if ($qaResumeItem.Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw 'Reparse-point campaign resume files are not allowed.'
    }
    for ($qaCursor = $qaResumeItem.Directory; $null -ne $qaCursor; $qaCursor = $qaCursor.Parent) {
        if ($qaCursor.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'Reparse-point campaign resume paths are not allowed.'
        }
    }
    if ($CampaignResumeSha256 -notmatch '^[0-9A-Fa-f]{64}$') { throw 'Campaign resume SHA-256 must be exactly 64 hexadecimal characters.' }
    $qaResumeHash = $CampaignResumeSha256.ToUpperInvariant()
    $qaActualResumeHash = (Get-FileHash -LiteralPath $qaResumePath -Algorithm SHA256).Hash
    if (-not [string]::Equals($qaActualResumeHash, $qaResumeHash, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Campaign resume world SHA-256 does not match the supplied value.'
    }
    if ($qaFeedColdResume) {
        $qaFeedObservationPath = [IO.Path]::ChangeExtension($qaResumePath, '.observation.json')
        if (-not (Test-Path -LiteralPath $qaFeedObservationPath -PathType Leaf) -or
            ((Get-Item -LiteralPath $qaFeedObservationPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Feed cold resume requires the original paired non-reparse observation file.'
        }
        $qaFeedObservation = Get-Content -LiteralPath $qaFeedObservationPath -Raw | ConvertFrom-Json
        if ($qaFeedObservation.schema -cne 'dungeonstory-completed-feed-checkpoint@1' -or
            $qaFeedObservation.worldSha256 -cne $qaResumeHash.ToLowerInvariant() -or
            $qaFeedObservation.trainingEligible -ne $false -or
            $qaFeedObservation.coldProcessResumeExecuted -ne $false) {
            throw 'Feed checkpoint observation schema or exact source-world binding is invalid.'
        }
        $qaFeedObservationHash = (Get-FileHash -LiteralPath $qaFeedObservationPath -Algorithm SHA256).Hash
    }
    if ($qaHusbandryPendingColdResume -and $qaResumeItem.Name -cne 'real-capture-slaughter-identity-pending-world.json') {
        throw 'Husbandry pending cold requires the original actual slaughter identity-pending checkpoint.'
    }
    if ($SkillThirtyFoodPreparationFixture) {
        $qaThirtyFoodResumePath = Join-Path $qaProject 'Artifacts\QA\AuditRemediation-20260926-r1\skill-support-current-v5-bench-r26\unity-evidence\incomplete-world.json'
        if (-not [string]::Equals($qaResumePath, $qaThirtyFoodResumePath, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Thirty-food preparation fixture requires the approved r26 incomplete-world checkpoint path.'
        }
    }
    if ($SkillRecruitmentHistoryCapacityFixture) {
        $qaRecruitmentEvidenceDirectory = $qaResumeItem.Directory
        $qaRecruitmentRunDirectory = $qaRecruitmentEvidenceDirectory.Parent
        $qaRecruitmentLegacyRoute = $null -ne $qaRecruitmentRunDirectory -and
            $qaRecruitmentRunDirectory.Name -match '^skill-support-current-v5-bench-r[0-9]+$'
        $qaRecruitmentActualRoute = $null -ne $qaRecruitmentRunDirectory -and
            $qaRecruitmentRunDirectory.Name -match '^u6-actual-skill-support-r[0-9]+-r[0-9]+$'
        if (-not [string]::Equals($qaRecruitmentEvidenceDirectory.Name, 'unity-evidence', [StringComparison]::OrdinalIgnoreCase) -or
            $qaResumeItem.Name -notlike '*world*.json' -or
            -not ($qaRecruitmentLegacyRoute -or $qaRecruitmentActualRoute)) {
            throw 'Recruitment-history capacity fixture requires an official skill-support-current-v5-bench-rN or u6-actual-skill-support-rN-rN unity-evidence world checkpoint.'
        }
        if ($qaRecruitmentActualRoute -and
            (-not $SkillEquipmentPreparationFixture -or -not $SkillNonTargetHungerFixture)) {
            throw 'U6 actual skill-support recruitment history requires explicit equipment preparation and non-target hunger fixtures.'
        }
    }
}
if (Get-Process -Name Unity -ErrorAction SilentlyContinue) { throw 'An Editor is already running; do not overlap verification.' }
$qaBuild = $qaOutput + '.launcher'
if ((Test-Path -LiteralPath $qaOutput) -or (Test-Path -LiteralPath $qaBuild)) {
    throw 'Create-only evidence/build target already exists.'
}
if ([string]::IsNullOrWhiteSpace($UnityExecutable)) {
    $qaVersionLine = Get-Content -LiteralPath (Join-Path $qaProject 'ProjectSettings\ProjectVersion.txt') |
        Where-Object { $_ -match '^m_EditorVersion: ([0-9A-Za-z.]+)$' } | Select-Object -First 1
    if ($null -eq $qaVersionLine -or $qaVersionLine -notmatch '^m_EditorVersion: ([0-9A-Za-z.]+)$') {
        throw 'Cannot resolve the project Unity version; no fallback version is selected.'
    }
    $UnityExecutable = Join-Path $env:ProgramFiles ('Unity\Hub\Editor\' + $Matches[1] + '\Editor\Unity.exe')
}
if (-not [IO.Path]::IsPathRooted($UnityExecutable) -or -not (Test-Path -LiteralPath $UnityExecutable -PathType Leaf)) {
    throw 'An existing absolute Unity executable path is required.'
}
$qaCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $qaCompiler -PathType Leaf)) { throw 'The .NET Framework x64 compiler is unavailable.' }
$qaSource = Join-Path $PSScriptRoot 'IsolatedUnityLauncher.cs'
$qaBinary = Join-Path $qaBuild 'IsolatedUnityLauncher.exe'
New-Item -ItemType Directory -Path $qaBuild | Out-Null

function Invoke-QAConsoleProcess(
    [string]$Program,
    [string[]]$Arguments,
    [string]$EvidencePrefix,
    [Collections.IDictionary]$ResumeEnvironment = $null
) {
    # All accepted paths are ordinary Windows paths without embedded quotes or
    # control characters. Reject them instead of relying on shell expansion.
    foreach ($qaArgument in $Arguments) {
        if ($qaArgument -match '["\x00-\x1f]' -or $qaArgument.EndsWith('\')) {
            throw 'Unsupported process argument; no process started.'
        }
    }
    $qaStart = [Diagnostics.ProcessStartInfo]::new()
    $qaStart.FileName = $Program
    $qaStart.Arguments = ($Arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $qaStart.WorkingDirectory = $qaProject
    $qaStart.UseShellExecute = $false
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_W01_WORK_TERMINAL_JOIN_VERIFY')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_W01_FIRE_IDENTITY_VERIFY')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_W01_FIRE_WATER_IDENTITY_VERIFY')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_W01_CHECKOUT_CLEANUP_VERIFY')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_SOURCE_V9_SKILL_EXPEDITION_ACTOR')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_SOURCE_V9_INITIAL_SEED')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_SOURCE_V9_SKILL_CANDIDATE_INDEX')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_SOURCE_V9_ACTUAL_AUTHORING')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_SOURCE_V9_AUTHORSHIP_CONTRACT')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_SOURCE_V9_AUTHORING_SOURCE')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_SOURCE_V9_AUTHORING_MANIFEST_SHA256')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_SOURCE_V9_AUTHORING_PROFILE')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_SKILL_BLUEPRINT_HAUL_DIAGNOSTICS')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_SKILL_RESEARCH_DIAGNOSTICS')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_SKILL_BATTLE_DIAGNOSTICS')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_ORIGINAL_SKILL_PENDING_DIAGNOSTICS')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_SKILL_BATTLE_ACTOR_ID')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_SKILL_BATTLE_SKILL_ID')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_SKILL_REPLAY_HOLD_NATURAL_INVASION')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_SKILL_NORMAL_WEAPON_PREPARATION')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_SKILL_NON_TARGET_HUNGER_FIXTURE')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_SKILL_EQUIPMENT_PREPARATION_FIXTURE')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_SKILL_PREPARATION_ENVIRONMENT_FIXTURE')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_SKILL_PREPARATION_INSTANT_WORK_FIXTURE')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_SKILL_THIRTY_FOOD_PREPARATION_FIXTURE')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_SKILL_RECRUITMENT_HISTORY_CAPACITY_FIXTURE')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_SKILL_ONE_CRAFTING_BENCH_FIXTURE')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_DEPRIVATION_CORPSE_ONLY')
    foreach ($qaResumeVariable in @('DUNGEONSTORY_LIVE_NARRATIVE_RESUME_WORLD', 'DUNGEONSTORY_LIVE_NARRATIVE_RESUME_SHA256', 'DUNGEONSTORY_FACILITY_CONTINUATION_VERIFY', 'DUNGEONSTORY_FACILITY_CONTINUATION_REST_REQUEST', 'DUNGEONSTORY_MEDICAL_CONTINUATION_VERIFY', 'DUNGEONSTORY_SHOP_CONTINUATION_VERIFY', 'DUNGEONSTORY_MEAL_CONTINUATION_VERIFY', 'DUNGEONSTORY_DRINK_CONTINUATION_VERIFY', 'DUNGEONSTORY_PERSISTENT_COOK_PAUSE_VERIFY', 'DUNGEONSTORY_EEC03_DRIFT_CARGO_VERIFY', 'DUNGEONSTORY_W05_HARVEST_IDENTITY_VERIFY', 'DUNGEONSTORY_W05_SURGERY_IDENTITY_VERIFY', 'DUNGEONSTORY_W01_SURGERY_PUBLICATION_RECOVERY_VERIFY', 'DUNGEONSTORY_W01_SURGERY_CLINICAL_FAILURE_VERIFY', 'DUNGEONSTORY_W03_MEDICAL_LABOR_VERIFY', 'DUNGEONSTORY_W04_REPAIR_LIVE_VERIFY', 'DUNGEONSTORY_W06_RESOURCE_CAUSALITY_VERIFY', 'DUNGEONSTORY_VISITOR_EXIT_ONLY', 'DUNGEONSTORY_VISITOR_VANDALISM_ONLY', 'DUNGEONSTORY_VISITOR_CONTROL_REPORT_PATH', 'DUNGEONSTORY_DEPRIVATION_VANDALISM_ONLY', 'DUNGEONSTORY_DEPRIVATION_A09_FULL', 'DUNGEONSTORY_DEPRIVATION_BT_REPORT_PATH', 'DUNGEONSTORY_A09_SAFE_DRINK', 'DUNGEONSTORY_A09_SAFE_DRINK_REPORT_PATH', 'DUNGEONSTORY_U0_G12_GOLEM_UPKEEP_VERIFY', 'DUNGEONSTORY_U0_G14_TRAIT_ANALYSIS_VERIFY', 'DUNGEONSTORY_U0_C01O02_FUNERAL_LINEAGE_VERIFY', 'DUNGEONSTORY_U0_C03_PRODUCTION_LOSS_VERIFY', 'DUNGEONSTORY_U0_C04O05_CAREER_OBSERVATION_VERIFY', 'DUNGEONSTORY_U0_CON01_CONSUMABLES_POLICY_VERIFY', 'DUNGEONSTORY_U0_BDCS01_SPACE_EXPANSION_VERIFY', 'DUNGEONSTORY_U0_BDCD01_DOOR_POLICY_VERIFY', 'DUNGEONSTORY_U0_BDCD01_DOOR_TRAVERSAL_VERIFY', 'DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT', 'DUNGEONSTORY_U0_EEC01_CROP_FAILURE_REPORT_PATH', 'DUNGEONSTORY_U0_EEC02_WILDLIFE_ARRIVAL_REPORT_PATH', 'DUNGEONSTORY_U0_EEC06_COMBAT_CRAFT_REPORT_PATH', 'DUNGEONSTORY_U0_G01_RECRUIT_DELIVERY_REPORT_PATH', 'DUNGEONSTORY_U0_G02_EMPLOYMENT_SETTLEMENT_REPORT_PATH', 'DUNGEONSTORY_U0_G03_POLICY_REVISION_REPORT_PATH', 'DUNGEONSTORY_SOURCE_V9_SKILL_INTAKE', 'DUNGEONSTORY_SOURCE_V9_EQUIPMENT_INTAKE', 'DUNGEONSTORY_SOURCE_V9_FACILITY_INTAKE', 'DUNGEONSTORY_SOURCE_V9_INVENTORY', 'DUNGEONSTORY_SOURCE_V9_MECHANICS_MANIFEST', 'DUNGEONSTORY_SOURCE_V9_PACKAGE_ID', 'DUNGEONSTORY_SOURCE_V9_EQUIPMENT_REPLAY', 'DUNGEONSTORY_SOURCE_V9_SKILL_REPLAY', 'DUNGEONSTORY_SOURCE_V9_TRAIT_REPLAY', 'DUNGEONSTORY_SOURCE_V9_FACILITY_REPLAY', 'DUNGEONSTORY_SOURCE_V9_REPLAY_PACKAGE', 'DUNGEONSTORY_SOURCE_V9_REPLAY_RESPONSE')) {
        [void]$qaStart.EnvironmentVariables.Remove($qaResumeVariable)
    }
    if ($null -ne $ResumeEnvironment) {
        foreach ($qaEntry in $ResumeEnvironment.GetEnumerator()) {
            $qaStart.EnvironmentVariables[[string]$qaEntry.Key] = [string]$qaEntry.Value
        }
    }
    $qaStart.CreateNoWindow = $true
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_FEED_RESUME_WORLD')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_FEED_RESUME_SHA256')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_FEED_OBSERVATION_SHA256')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_HUSBANDRY_PENDING_RESUME_WORLD')
    [void]$qaStart.EnvironmentVariables.Remove('DUNGEONSTORY_HUSBANDRY_PENDING_RESUME_SHA256')
    if ($null -ne $ResumeEnvironment) {
        foreach ($qaFeedVariable in @('DUNGEONSTORY_FEED_RESUME_WORLD', 'DUNGEONSTORY_FEED_RESUME_SHA256', 'DUNGEONSTORY_FEED_OBSERVATION_SHA256', 'DUNGEONSTORY_HUSBANDRY_PENDING_RESUME_WORLD', 'DUNGEONSTORY_HUSBANDRY_PENDING_RESUME_SHA256')) {
            if ($ResumeEnvironment.ContainsKey($qaFeedVariable)) {
                $qaStart.EnvironmentVariables[$qaFeedVariable] = [string]$ResumeEnvironment[$qaFeedVariable]
            }
        }
    }
    $qaStart.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $qaStart.RedirectStandardOutput = $true
    $qaStart.RedirectStandardError = $true
    $qaProcess = [Diagnostics.Process]::Start($qaStart)
    try {
        Write-Host ('QA_PROCESS pid=' + $qaProcess.Id + '; executable=' + [IO.Path]::GetFileName($Program))
        $qaStdout = $qaProcess.StandardOutput.ReadToEndAsync()
        $qaStderr = $qaProcess.StandardError.ReadToEndAsync()
        $qaProcess.WaitForExit()
        [IO.File]::WriteAllText($EvidencePrefix + '.stdout.txt', $qaStdout.Result)
        [IO.File]::WriteAllText($EvidencePrefix + '.stderr.txt', $qaStderr.Result)
        if ($qaProcess.ExitCode -ne 0) {
            throw ('Process failed with exit ' + $qaProcess.ExitCode + '; inspect ' + $EvidencePrefix + '.stderr.txt and preserved Unity evidence.')
        }
    } finally { $qaProcess.Dispose() }
}

Invoke-QAConsoleProcess $qaCompiler @('/nologo', '/target:exe', '/platform:x64', ('/out:' + $qaBinary), $qaSource) (Join-Path $qaBuild 'build')
Invoke-QAConsoleProcess $qaBinary @('--self-test') (Join-Path $qaBuild 'self-test')
$qaBuildManifest = [ordered]@{
    launcherSourceSha256 = (Get-FileHash -LiteralPath $qaSource -Algorithm SHA256).Hash
    launcherBinarySha256 = (Get-FileHash -LiteralPath $qaBinary -Algorithm SHA256).Hash
    wrapperSha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
    mode = $Mode
    serializedStartup = [bool]$SerializedStartup
    facilityContinuation = [bool]$FacilityContinuation
    facilityContinuationRestRequest = [bool]$FacilityContinuationRestRequest
    medicalContinuation = [bool]$MedicalContinuation
    shopContinuation = [bool]$ShopContinuation
    w01CheckoutCleanup = [bool]$W01CheckoutCleanup
    mealContinuation = [bool]$MealContinuation
    drinkContinuation = [bool]$DrinkContinuation
    persistentCookPause = [bool]$PersistentCookPause
    eec03DriftCargo = [bool]$EEC03DriftCargo
    eec05Apparel = [bool]$EEC05Apparel
    w05HarvestIdentity = [bool]$W05HarvestIdentity
    w05SurgeryIdentity = [bool]$W05SurgeryIdentity
    w01SurgeryPublicationRecovery = [bool]$W01SurgeryPublicationRecovery
    w01SurgeryClinicalFailure = [bool]$W01SurgeryClinicalFailure
    w01WorkTerminalJoin = [bool]$W01WorkTerminalJoin
    w01FireIdentity = [bool]$W01FireIdentity
    w01FireWaterIdentity = [bool]$W01FireWaterIdentity
    w03MedicalLabor = [bool]$W03MedicalLabor
    w04RepairLive = [bool]$W04RepairLive
    w06ResourceCausality = [bool]$W06ResourceCausality
    terminalDeathSave = [bool]$TerminalDeathSave
    sourceV9ActualModelAuthoring = [bool]$SourceV9ActualModelAuthoring
    sourceV9AuthorshipContract = [bool]$SourceV9AuthorshipContract
    sourceV9AuthoringSource = $qaSourceV9AuthoringSourcePath
    sourceV9AuthoringManifestSha256 = $SourceV9AuthoringManifestSha256
    sourceV9AuthoringProfile = $SourceV9AuthoringProfile
    sourceV9SkillIntake = [bool]$SourceV9SkillIntake
    sourceV9SkillExpeditionActor = [bool]$SourceV9SkillExpeditionActor
    sourceV9InitialSeed = $SourceV9InitialSeed
    sourceV9EquipmentIntake = [bool]$SourceV9EquipmentIntake
    sourceV9FacilityIntake = [bool]$SourceV9FacilityIntake
    u0E01RegistryInventory = [bool]$U0E01RegistryInventory
    u0E02HusbandryOutcome = [bool]$U0E02HusbandryOutcome
    u0E03CropTreatment = [bool]$U0E03CropTreatment
    u0E06OperatingDay = [bool]$U0E06OperatingDay
    u0E07StockPolicySale = [bool]$U0E07StockPolicySale
    u0E08CropAction = [bool]$U0E08CropAction
    u0EEC01CropFailure = [bool]$U0EEC01CropFailure
    u0EEC02WildlifeArrival = [bool]$U0EEC02WildlifeArrival
    u0EEC06CombatCraft = [bool]$U0EEC06CombatCraft
    u0G01RecruitDelivery = [bool]$U0G01RecruitDelivery
    u0G02EmploymentSettlement = [bool]$U0G02EmploymentSettlement
    u0G03PolicyRevision = [bool]$U0G03PolicyRevision
    u0G04Application = [bool]$U0G04Application
    u0G06MilestonePressure = [bool]$U0G06MilestonePressure
    u0G07KnowledgeCompletion = [bool]$U0G07KnowledgeCompletion
    u0G08Recovery = [bool]$U0G08Recovery
    u0G09ExternalInfluence = [bool]$U0G09ExternalInfluence
    u0G10StaffDiscontent = [bool]$U0G10StaffDiscontent
    u0G11DurableEquipment = [bool]$U0G11DurableEquipment
    u0G12GolemUpkeep = [bool]$U0G12GolemUpkeep
    u0G14TraitAnalysis = [bool]$U0G14TraitAnalysis
    u0C01O02FuneralLineage = [bool]$U0C01O02FuneralLineage
    u0C03ProductionLoss = [bool]$U0C03ProductionLoss
    u0C04O05CareerObservation = [bool]$U0C04O05CareerObservation
    u0CN01AmbitionTransition = [bool]$U0CN01AmbitionTransition
    u0CR01FamilyPlanning = [bool]$U0CR01FamilyPlanning
    u0CR02R05Reproduction = [bool]$U0CR02R05Reproduction
    u0CR07Birth = [bool]$U0CR07Birth
    u0CR09Household = [bool]$U0CR09Household
    u0CA01AgeAnatomy = [bool]$U0CA01AgeAnatomy
    u0CA02DiseaseAnatomy = [bool]$U0CA02DiseaseAnatomy
    u0UI01DefenseService = [bool]$U0UI01DefenseService
    u0UI02ResidentEvacuation = [bool]$U0UI02ResidentEvacuation
    u0CON01ConsumablesPolicy = [bool]$U0CON01ConsumablesPolicy
    u0BDCS01SpaceExpansion = [bool]$U0BDCS01SpaceExpansion
    u0BDCD01DoorPolicy = [bool]$U0BDCD01DoorPolicy
    u0BDCD01DoorTraversal = [bool]$U0BDCD01DoorTraversal
    u0E09EnvironmentExposure = [bool]$U0E09EnvironmentExposure
    u0E10WorldResource = [bool]$U0E10WorldResource
    u0E11Wildlife = [bool]$U0E11Wildlife
    u0E11Capture = [bool]$U0E11Capture
    u0E11CaptureMode = if ($U0E11Capture) {
        if ($ExecuteMethod -eq 'CaptivityWildlifeLifecyclePlayModeVerifier.RunBatch') { 'full' }
        elseif ($qaFeedColdResume) { 'feed-cold-resume' }
        elseif ($qaHusbandryPendingColdResume) { 'husbandry-pending-cold-resume' }
        else { 'transport-only' }
    } else { $null }
    u0E11Role = [bool]$U0E11Role
    u0E11Butchery = [bool]$U0E11Butchery
    u0E12Infrastructure = [bool]$U0E12Infrastructure
    u0E13Automation = [bool]$U0E13Automation
    u0T01FacilityShop = [bool]$U0T01FacilityShop
    u0T02Fulfillment = [bool]$U0T02Fulfillment
    u0T03Retail = [bool]$U0T03Retail
    u0T04RetailRecovery = [bool]$U0T04RetailRecovery
    u0T07RegionalSupply = [bool]$U0T07RegionalSupply
    u0D01CombatEvidence = [bool]$U0D01CombatEvidence
    u0D02SharedCombat = [bool]$U0D02SharedCombat
    u0D03CombatCausal = [bool]$U0D03CombatCausal
    u0A03VisitorExit = [bool]$U0A03VisitorExit
    u0A04VandalismMacro = [bool]$U0A04VandalismMacro
    u0A04VandalismBreakdown = [bool]$U0A04VandalismBreakdown
    u0A04CorpseEating = [bool]$U0A04CorpseEating
    u0A09Deprivation = [bool]$U0A09Deprivation
    u0A09SafeDrink = [bool]$U0A09SafeDrink
    sourceV9EquipmentReplay = [bool]$SourceV9EquipmentReplay
    sourceV9SkillReplay = [bool]$SourceV9SkillReplay
    skillReplayHoldNaturalInvasion = [bool]$SkillReplayHoldNaturalInvasion
    skillNormalWeaponPreparation = [bool]$SkillNormalWeaponPreparation
    skillNonTargetHungerFixture = [bool]$SkillNonTargetHungerFixture
    skillEquipmentPreparationFixture = [bool]$SkillEquipmentPreparationFixture
    skillPreparationEnvironmentFixture = [bool]$SkillPreparationEnvironmentFixture
    skillPreparationInstantWorkFixture = [bool]$SkillPreparationInstantWorkFixture
    skillThirtyFoodPreparationFixture = [bool]$SkillThirtyFoodPreparationFixture
    skillRecruitmentHistoryCapacityFixture = [bool]$SkillRecruitmentHistoryCapacityFixture
    skillOneCraftingBenchFixture = [bool]$SkillOneCraftingBenchFixture
    skillBattleActorId = $SkillBattleActorId
    skillBattleSkillId = $SkillBattleSkillId
    skillBlueprintHaulDiagnostics = $ExecuteMethod -eq 'DungeonLiveNarrativeCampaignRunner.RequestSkillBlueprintHaulDiagnostics'
    constructionHaulDiagnostics = $ExecuteMethod -eq 'DungeonLiveNarrativeCampaignRunner.RequestConstructionHaulDiagnostics'
    skillResearchDiagnostics = $ExecuteMethod -eq 'DungeonLiveNarrativeCampaignRunner.RequestSkillResearchDiagnostics'
    completedCraftDeliveryDiagnostics = $ExecuteMethod -eq 'DungeonLiveNarrativeCampaignRunner.RequestCompletedCraftDeliveryDiagnostics'
    originalSkillPendingDiagnostics = $ExecuteMethod -eq 'DungeonLiveNarrativeCampaignRunner.RequestOriginalSkillPendingDiagnostics'
    sourceV9SkillCandidateIndex = $SourceV9SkillCandidateIndex
    sourceV9TraitReplay = [bool]$SourceV9TraitReplay
    sourceV9FacilityReplay = [bool]$SourceV9FacilityReplay
    sourceV9Inventory = $qaSourceV9InventoryPath
    sourceV9MechanicsManifest = $qaSourceV9MechanicsManifestPath
    sourceV9PackageId = $SourceV9PackageId
    sourceV9ReplayPackage = $qaSourceV9ReplayPackagePath
    sourceV9ReplayResponse = $qaSourceV9ReplayResponsePath
    executeMethod = $ExecuteMethod
    unityExecutable = $UnityExecutable
    resumePath = $qaResumePath
    resumeSha256 = $qaResumeHash
    feedObservationPath = $qaFeedObservationPath
    feedObservationSha256 = $qaFeedObservationHash
    interactiveFallback = $false
}
[IO.File]::WriteAllText((Join-Path $qaBuild 'build-manifest.json'), ($qaBuildManifest | ConvertTo-Json))
$qaResumeEnvironment = @{}
if ($TerminalDeathSave) {
    $qaResumeEnvironment['DUNGEONSTORY_TERMINAL_DEATH_SAVE_VERIFY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_LIVE_NARRATIVE_CAPTURE'] = '1'
}
if ($FacilityContinuation) { $qaResumeEnvironment['DUNGEONSTORY_FACILITY_CONTINUATION_VERIFY'] = '1' }
if ($FacilityContinuationRestRequest) { $qaResumeEnvironment['DUNGEONSTORY_FACILITY_CONTINUATION_REST_REQUEST'] = '1' }
if ($MedicalContinuation) { $qaResumeEnvironment['DUNGEONSTORY_MEDICAL_CONTINUATION_VERIFY'] = '1' }
if ($ShopContinuation) { $qaResumeEnvironment['DUNGEONSTORY_SHOP_CONTINUATION_VERIFY'] = '1' }
if ($W01CheckoutCleanup) { $qaResumeEnvironment['DUNGEONSTORY_W01_CHECKOUT_CLEANUP_VERIFY'] = '1' }
if ($MealContinuation) { $qaResumeEnvironment['DUNGEONSTORY_MEAL_CONTINUATION_VERIFY'] = '1' }
if ($DrinkContinuation) { $qaResumeEnvironment['DUNGEONSTORY_DRINK_CONTINUATION_VERIFY'] = '1' }
if ($PersistentCookPause) { $qaResumeEnvironment['DUNGEONSTORY_PERSISTENT_COOK_PAUSE_VERIFY'] = '1' }
if ($EEC03DriftCargo) { $qaResumeEnvironment['DUNGEONSTORY_EEC03_DRIFT_CARGO_VERIFY'] = '1' }
if ($EEC05Apparel) { $qaResumeEnvironment['DUNGEONSTORY_EEC05_APPAREL_VERIFY'] = '1' }
if ($W05HarvestIdentity) { $qaResumeEnvironment['DUNGEONSTORY_W05_HARVEST_IDENTITY_VERIFY'] = '1' }
if ($W05SurgeryIdentity) { $qaResumeEnvironment['DUNGEONSTORY_W05_SURGERY_IDENTITY_VERIFY'] = '1' }
if ($W01SurgeryPublicationRecovery) { $qaResumeEnvironment['DUNGEONSTORY_W01_SURGERY_PUBLICATION_RECOVERY_VERIFY'] = '1' }
if ($W01SurgeryClinicalFailure) { $qaResumeEnvironment['DUNGEONSTORY_W01_SURGERY_CLINICAL_FAILURE_VERIFY'] = '1' }
if ($W01WorkTerminalJoin) { $qaResumeEnvironment['DUNGEONSTORY_W01_WORK_TERMINAL_JOIN_VERIFY'] = '1' }
if ($W01FireIdentity) { $qaResumeEnvironment['DUNGEONSTORY_W01_FIRE_IDENTITY_VERIFY'] = '1' }
if ($W01FireWaterIdentity) { $qaResumeEnvironment['DUNGEONSTORY_W01_FIRE_WATER_IDENTITY_VERIFY'] = '1' }
if ($W03MedicalLabor) { $qaResumeEnvironment['DUNGEONSTORY_W03_MEDICAL_LABOR_VERIFY'] = '1' }
if ($W04RepairLive) { $qaResumeEnvironment['DUNGEONSTORY_W04_REPAIR_LIVE_VERIFY'] = '1' }
if ($W06ResourceCausality) { $qaResumeEnvironment['DUNGEONSTORY_W06_RESOURCE_CAUSALITY_VERIFY'] = '1' }
if ($U0A03VisitorExit) {
    $qaResumeEnvironment['DUNGEONSTORY_VISITOR_EXIT_ONLY'] = '1'
    $qaVisitorReport = Join-Path $qaOutput 'visitor-exit-a03-playmode.txt'
    $qaResumeEnvironment['DUNGEONSTORY_VISITOR_CONTROL_REPORT_PATH'] =
        $qaVisitorReport.Substring($qaProject.Length + 1).Replace('\', '/')
}
if ($U0A04VandalismMacro) {
    $qaResumeEnvironment['DUNGEONSTORY_VISITOR_VANDALISM_ONLY'] = '1'
    $qaVisitorReport = Join-Path $qaOutput 'facility-vandalism-a04-macro-playmode.txt'
    $qaResumeEnvironment['DUNGEONSTORY_VISITOR_CONTROL_REPORT_PATH'] =
        $qaVisitorReport.Substring($qaProject.Length + 1).Replace('\', '/')
}
if ($U0A04VandalismBreakdown) {
    $qaResumeEnvironment['DUNGEONSTORY_DEPRIVATION_VANDALISM_ONLY'] = '1'
    $qaBreakdownReport = Join-Path $qaOutput 'facility-vandalism-a04-breakdown-playmode.txt'
    $qaResumeEnvironment['DUNGEONSTORY_DEPRIVATION_BT_REPORT_PATH'] =
        $qaBreakdownReport.Substring($qaProject.Length + 1).Replace('\', '/')
}
if ($U0A04CorpseEating) {
    $qaResumeEnvironment['DUNGEONSTORY_DEPRIVATION_CORPSE_ONLY'] = '1'
    $qaCorpseReport = Join-Path $qaOutput 'corpse-eating-a04-playmode.txt'
    $qaResumeEnvironment['DUNGEONSTORY_DEPRIVATION_BT_REPORT_PATH'] =
        $qaCorpseReport.Substring($qaProject.Length + 1).Replace('\', '/')
}
if ($U0A09Deprivation) {
    $qaResumeEnvironment['DUNGEONSTORY_DEPRIVATION_A09_FULL'] = '1'
    $qaDeprivationReport = Join-Path $qaOutput 'deprivation-a09-playmode.txt'
    $qaResumeEnvironment['DUNGEONSTORY_DEPRIVATION_BT_REPORT_PATH'] =
        $qaDeprivationReport.Substring($qaProject.Length + 1).Replace('\', '/')
}
if ($U0A09SafeDrink) {
    $qaResumeEnvironment['DUNGEONSTORY_A09_SAFE_DRINK'] = '1'
    $qaSafeDrinkReport = Join-Path $qaOutput 'safe-drink-a09-playmode.txt'
    $qaResumeEnvironment['DUNGEONSTORY_A09_SAFE_DRINK_REPORT_PATH'] =
        $qaSafeDrinkReport.Substring($qaProject.Length + 1).Replace('\', '/')
}
if ($U0E01RegistryInventory) { $qaResumeEnvironment['DUNGEONSTORY_U0_E01_REGISTRY_INVENTORY_VERIFY'] = '1' }
if ($U0E02HusbandryOutcome) {
    $qaResumeEnvironment['DUNGEON_ANIMAL_HUSBANDRY_OUTCOME_REPORT_PATH'] =
        (Join-Path $qaOutput 'animal-husbandry-outcome-report.txt')
}
if ($U0E03CropTreatment) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_E03_CROP_TREATMENT_REPORT_PATH'] =
        (Join-Path $qaOutput 'crop-treatment-outcome-report.txt')
}
if ($U0E06OperatingDay) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_E06_OPERATING_DAY_REPORT_PATH'] =
        (Join-Path $qaOutput 'operating-day-report.txt')
}
if ($U0E07StockPolicySale) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_E07_STOCK_POLICY_SALE_REPORT_PATH'] =
        (Join-Path $qaOutput 'stock-policy-sale-report.txt')
}
if ($U0E08CropAction) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_E08_CROP_ACTION_REPORT_PATH'] =
        (Join-Path $qaOutput 'crop-action-report.txt')
}
if ($U0EEC01CropFailure) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_EEC01_CROP_FAILURE_REPORT_PATH'] =
        (Join-Path $qaOutput 'crop-failure-eec01-report.txt')
}
if ($U0EEC02WildlifeArrival) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_EEC02_WILDLIFE_ARRIVAL_REPORT_PATH'] =
        (Join-Path $qaOutput 'seasonal-wildlife-eec02-report.txt')
}
if ($U0EEC06CombatCraft) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_EEC06_COMBAT_CRAFT_REPORT_PATH'] =
        (Join-Path $qaOutput 'combat-craft-eec06-report.txt')
}
if ($U0G01RecruitDelivery) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_G01_RECRUIT_DELIVERY_REPORT_PATH'] =
        (Join-Path $qaOutput 'recruit-delivery-g01-report.txt')
}
if ($U0G02EmploymentSettlement) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_G02_EMPLOYMENT_SETTLEMENT_REPORT_PATH'] =
        (Join-Path $qaOutput 'employment-settlement-g02-report.txt')
}
if ($U0G03PolicyRevision) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_G03_POLICY_REVISION_REPORT_PATH'] =
        (Join-Path $qaOutput 'policy-revision-g03-report.txt')
}
if ($U0G04Application) {
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0G06MilestonePressure) {
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0G07KnowledgeCompletion) {
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0G08Recovery) {
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0G09ExternalInfluence) {
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0G10StaffDiscontent) {
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0G11DurableEquipment) {
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0G12GolemUpkeep) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_G12_GOLEM_UPKEEP_VERIFY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0G14TraitAnalysis) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_G14_TRAIT_ANALYSIS_VERIFY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0C01O02FuneralLineage) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_C01O02_FUNERAL_LINEAGE_VERIFY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0C03ProductionLoss) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_C03_PRODUCTION_LOSS_VERIFY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0C04O05CareerObservation) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_C04O05_CAREER_OBSERVATION_VERIFY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0CN01AmbitionTransition) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_CN01_AMBITION_TRANSITION_VERIFY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0CR01FamilyPlanning) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_CR01_FAMILY_PLANNING_VERIFY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0CR02R05Reproduction) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_CR02R05_REPRODUCTION_VERIFY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0CR07Birth) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_CR07_BIRTH_VERIFY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0CR09Household) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_CR09_HOUSEHOLD_VERIFY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0CA01AgeAnatomy) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_CA01_AGE_ANATOMY_VERIFY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0CA02DiseaseAnatomy) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_CA02_DISEASE_ANATOMY_VERIFY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0UI01DefenseService) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_UI01_DEFENSE_SERVICE_VERIFY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0CON01ConsumablesPolicy) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_CON01_CONSUMABLES_POLICY_VERIFY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0BDCS01SpaceExpansion) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_BDCS01_SPACE_EXPANSION_VERIFY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0BDCD01DoorPolicy) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_BDCD01_DOOR_POLICY_VERIFY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0BDCD01DoorTraversal) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_BDCD01_DOOR_TRAVERSAL_VERIFY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($U0E09EnvironmentExposure) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_E09_ENVIRONMENT_EXPOSURE_VERIFY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_U0_E09_ENVIRONMENT_EXPOSURE_REPORT_PATH'] =
        (Join-Path $qaOutput 'environment-exposure-report.txt')
}
if ($U0E10WorldResource) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_E10_WORLD_RESOURCE_REPORT_PATH'] =
        (Join-Path $qaOutput 'world-resource-report.txt')
}
if ($U0E11Wildlife) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_E11_WILDLIFE_VERIFY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_U0_E11_WILDLIFE_REPORT_PATH'] =
        (Join-Path $qaOutput 'wildlife-ai-hunt-report.txt')
}
if ($U0E11Capture) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_E11_CAPTURE_REPORT_PATH'] =
        if ($ExecuteMethod -eq 'CaptivityWildlifeLifecyclePlayModeVerifier.RunBatch' -or $qaFeedColdResume -or $qaHusbandryPendingColdResume) {
            Join-Path $qaOutput 'captivity-wildlife-lifecycle-report.txt'
        } else {
            Join-Path $qaOutput 'wildlife-capture-release-report.txt'
        }
}
if ($U0E11Role) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_E11_ROLE_REPORT_PATH'] =
        if ($ExecuteMethod -eq 'Wim029CompanionRoleOutcomePlayModeVerifier.RunHaulBatch') {
            Join-Path $qaOutput 'wildlife-haul-role-report.txt'
        } else {
            Join-Path $qaOutput 'wildlife-companion-role-report.txt'
        }
}
if ($U0E11Butchery) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_E11_BUTCHERY_REPORT_PATH'] =
        (Join-Path $qaOutput 'wildlife-butchery-report.txt')
}
if ($U0E12Infrastructure) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_E12_INFRASTRUCTURE_REPORT_PATH'] =
        (Join-Path $qaOutput 'infrastructure-autonomous-report.txt')
}
if ($U0E13Automation) {
    $qaE13Report = (Join-Path $qaOutput 'automation-maintenance-production-report.txt')
    $qaResumeEnvironment['DUNGEON_INFRASTRUCTURE_COMMAND_REPORT_PATH'] =
        $qaE13Report.Substring($qaProject.Length + 1).Replace('\', '/')
}
if ($U0T01FacilityShop) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_T01_FACILITY_SHOP_REPORT_PATH'] =
        (Join-Path $qaOutput 'facility-shop-t01-report.txt')
}
if ($U0T02Fulfillment) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_T02_FULFILLMENT_REPORT_PATH'] =
        (Join-Path $qaOutput 'facility-fulfillment-t02-report.txt')
}
if ($U0T03Retail) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_T03_RETAIL_REPORT_PATH'] =
        (Join-Path $qaOutput 'retail-purchase-t03-report.txt')
}
if ($U0T04RetailRecovery) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_T04_RETAIL_RECOVERY_REPORT_PATH'] =
        (Join-Path $qaOutput 'retail-purchase-t04-recovery-report.txt')
}
if ($U0T07RegionalSupply) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_T07_REGIONAL_SUPPLY_REPORT_PATH'] =
        (Join-Path $qaOutput 'regional-supply-t07-report.txt')
}
if ($U0D01CombatEvidence) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_D01_COMBAT_EVIDENCE_REPORT_PATH'] =
        (Join-Path $qaOutput 'combat-d01-evidence-report.txt')
}
if ($U0D02SharedCombat) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_D02_SHARED_COMBAT_REPORT_PATH'] =
        (Join-Path $qaOutput 'combat-d02-shared-report.txt')
}
if ($U0D03CombatCausal) {
    $qaResumeEnvironment['DUNGEONSTORY_U0_D03_COMBAT_CAUSAL_REPORT_PATH'] =
        (Join-Path $qaOutput 'combat-d03-causal-report.txt')
}
if ($SourceV9ActualModelAuthoring) {
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_ACTUAL_AUTHORING'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_AUTHORING_SOURCE'] = $qaSourceV9AuthoringSourcePath
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_AUTHORING_MANIFEST_SHA256'] = $SourceV9AuthoringManifestSha256
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_AUTHORING_PROFILE'] = $SourceV9AuthoringProfile
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($SourceV9AuthorshipContract) {
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_AUTHORSHIP_CONTRACT'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT'] = $qaOutput
}
if ($SourceV9SkillIntake) {
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_SKILL_INTAKE'] = '1'
    if ($SourceV9SkillExpeditionActor) {
        $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_SKILL_EXPEDITION_ACTOR'] = '1'
    }
    if ($PSBoundParameters.ContainsKey('SourceV9InitialSeed')) {
        $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_INITIAL_SEED'] = $SourceV9InitialSeed.ToString([Globalization.CultureInfo]::InvariantCulture)
    }
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_INVENTORY'] = $qaSourceV9InventoryPath
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_MECHANICS_MANIFEST'] = $qaSourceV9MechanicsManifestPath
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_PACKAGE_ID'] = $SourceV9PackageId
}
if ($SourceV9EquipmentIntake) {
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_EQUIPMENT_INTAKE'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_INVENTORY'] = $qaSourceV9InventoryPath
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_MECHANICS_MANIFEST'] = $qaSourceV9MechanicsManifestPath
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_PACKAGE_ID'] = $SourceV9PackageId
}
if ($SourceV9FacilityIntake) {
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_FACILITY_INTAKE'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_INVENTORY'] = $qaSourceV9InventoryPath
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_MECHANICS_MANIFEST'] = $qaSourceV9MechanicsManifestPath
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_PACKAGE_ID'] = $SourceV9PackageId
}
if ($SourceV9EquipmentReplay) {
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_EQUIPMENT_REPLAY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_REPLAY_PACKAGE'] = $qaSourceV9ReplayPackagePath
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_REPLAY_RESPONSE'] = $qaSourceV9ReplayResponsePath
}
if ($SkillReplayHoldNaturalInvasion) {
    $qaResumeEnvironment['DUNGEONSTORY_SKILL_REPLAY_HOLD_NATURAL_INVASION'] = '1'
}
if ($SkillNormalWeaponPreparation) {
    $qaResumeEnvironment['DUNGEONSTORY_SKILL_NORMAL_WEAPON_PREPARATION'] = '1'
}
if ($SkillNonTargetHungerFixture) {
    $qaResumeEnvironment['DUNGEONSTORY_SKILL_NON_TARGET_HUNGER_FIXTURE'] = '1'
}
if ($SkillEquipmentPreparationFixture) {
    $qaResumeEnvironment['DUNGEONSTORY_SKILL_EQUIPMENT_PREPARATION_FIXTURE'] = '1'
}
if ($SkillPreparationEnvironmentFixture) {
    $qaResumeEnvironment['DUNGEONSTORY_SKILL_PREPARATION_ENVIRONMENT_FIXTURE'] = '1'
}
if ($SkillPreparationInstantWorkFixture) {
    $qaResumeEnvironment['DUNGEONSTORY_SKILL_PREPARATION_INSTANT_WORK_FIXTURE'] = '1'
}
if ($SkillThirtyFoodPreparationFixture) {
    $qaResumeEnvironment['DUNGEONSTORY_SKILL_THIRTY_FOOD_PREPARATION_FIXTURE'] = '1'
}
if ($SkillRecruitmentHistoryCapacityFixture) {
    $qaResumeEnvironment['DUNGEONSTORY_SKILL_RECRUITMENT_HISTORY_CAPACITY_FIXTURE'] = '1'
}
if ($SkillOneCraftingBenchFixture) {
    $qaResumeEnvironment['DUNGEONSTORY_SKILL_ONE_CRAFTING_BENCH_FIXTURE'] = '1'
}
if ($qaSkillSupportContinuation) {
    $qaResumeEnvironment['DUNGEONSTORY_SKILL_BATTLE_ACTOR_ID'] = $SkillBattleActorId
    $qaResumeEnvironment['DUNGEONSTORY_SKILL_BATTLE_SKILL_ID'] = $SkillBattleSkillId
}
if ($SourceV9SkillReplay) {
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_SKILL_REPLAY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_SKILL_CANDIDATE_INDEX'] = [string]$SourceV9SkillCandidateIndex
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_REPLAY_PACKAGE'] = $qaSourceV9ReplayPackagePath
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_REPLAY_RESPONSE'] = $qaSourceV9ReplayResponsePath
}
if ($SourceV9TraitReplay) {
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_TRAIT_REPLAY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_REPLAY_PACKAGE'] = $qaSourceV9ReplayPackagePath
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_REPLAY_RESPONSE'] = $qaSourceV9ReplayResponsePath
}
if ($SourceV9FacilityReplay) {
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_FACILITY_REPLAY'] = '1'
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_REPLAY_PACKAGE'] = $qaSourceV9ReplayPackagePath
    $qaResumeEnvironment['DUNGEONSTORY_SOURCE_V9_REPLAY_RESPONSE'] = $qaSourceV9ReplayResponsePath
}
if ($qaFeedColdResume) {
    $qaResumeEnvironment['DUNGEONSTORY_FEED_RESUME_WORLD'] = $qaResumePath
    $qaResumeEnvironment['DUNGEONSTORY_FEED_RESUME_SHA256'] = $qaResumeHash
    $qaResumeEnvironment['DUNGEONSTORY_FEED_OBSERVATION_SHA256'] = $qaFeedObservationHash
} elseif ($qaHusbandryPendingColdResume) {
    $qaResumeEnvironment['DUNGEONSTORY_HUSBANDRY_PENDING_RESUME_WORLD'] = $qaResumePath
    $qaResumeEnvironment['DUNGEONSTORY_HUSBANDRY_PENDING_RESUME_SHA256'] = $qaResumeHash
} elseif ($null -ne $qaResumePath) {
    $qaResumeEnvironment['DUNGEONSTORY_LIVE_NARRATIVE_RESUME_WORLD'] = $qaResumePath
    $qaResumeEnvironment['DUNGEONSTORY_LIVE_NARRATIVE_RESUME_SHA256'] = $qaResumeHash
}
$qaLauncherArguments = @('--unity', $UnityExecutable, $qaProject, $ExecuteMethod, $qaOutput, $Mode)
if ($SerializedStartup) { $qaLauncherArguments += 'serial-import-job1' }
Invoke-QAConsoleProcess $qaBinary $qaLauncherArguments (Join-Path $qaBuild 'run') $qaResumeEnvironment
Write-Output ('UNITY_PROCESS_EXIT=0; evidence=' + $qaOutput)
Write-Output 'Read the test report separately; process exit alone is not a test PASS.'
