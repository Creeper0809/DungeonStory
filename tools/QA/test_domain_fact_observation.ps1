$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName Microsoft.CodeAnalysis
Add-Type -AssemblyName Microsoft.CodeAnalysis.CSharp
$observationRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$observationPaths = @(
    'Assets/Scripts/Services/Foundation/Events/DomainFactValueSyntax.cs',
    'Assets/Scripts/Services/Foundation/Events/DomainFactContracts.cs',
    'Assets/Scripts/Services/Foundation/Events/DomainFactDispatcher.cs',
    'Assets/Scripts/Services/Foundation/DomainFailure.cs',
    'Assets/Scripts/Services/Foundation/CanonicalSemanticDigestBuilder.cs',
    'Assets/Scripts/Services/Foundation/RuntimeArchitectureAttributes.cs',
    'Assets/Scripts/Services/Foundation/Time/GameSimulationTimeRules.cs',
    'Assets/Scripts/Services/Narrative/GameplayOutcomes/GameplayOutcomeIdentifiers.cs',
    'Assets/Scripts/Services/Narrative/GameplayOutcomes/GameplayDomainFactPairProof.cs',
    'Assets/Scripts/Services/Narrative/GameplayOutcomes/GameplayPostCommitFactObserver.cs',
    'Assets/Scripts/Services/Foundation/PersistentEntityIds.cs',
    'Assets/Scripts/Services/Items/PhysicalItemDisposedFact.cs',
    'Assets/Scripts/Services/Items/PhysicalItemSourceFactSaveCodec.cs',
    'Assets/Scripts/Models/Items/Core/PhysicalItemDispositionSourceFact.cs',
    'Assets/Scripts/Models/Environment/Core/EnvironmentalFireResourceRecord.cs',
    'Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFireResourceFact.cs',
    'Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFireResourceTransaction.cs',
    'Assets/Scripts/Services/Combat/CharacterHealingFactData.cs',
    'Assets/Scripts/Services/Combat/CharacterHealingFact.cs',
    'Assets/Scripts/Services/Combat/CharacterFatalityFactData.cs',
    'Assets/Scripts/Services/Combat/CharacterFatalityFact.cs',
    'Assets/Scripts/Models/CoreSession/ServiceSessionContracts.cs',
    'Assets/Scripts/Models/CoreSession/ServiceModeChangedFact.cs',
    'Assets/Scripts/Models/CoreSession/ServiceSessionTerminalFact.cs',
    'Assets/Scripts/Models/ServiceRooms/Core/ServiceSessionAggregate.cs',
    'Assets/Scripts/Models/Buildings/Core/FacilityVandalismFacts.cs',
    'Assets/Scripts/Models/Environment/Core/EnvironmentalFireDomain.cs',
    'Assets/Scripts/Models/Environment/Core/EnvironmentalFireDamageFact.cs',
    'Assets/Scripts/Models/Environment/Core/EnvironmentalFireActionFacts.cs',
    'Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFireRuntime.cs',
    'Assets/Scripts/Models/Species/Core/ReproductionCommandIdentity.cs',
    'Assets/Scripts/Models/Species/Core/ReproductionTransitionFact.cs',
    'Assets/Scripts/Models/Species/Core/ReproductionDomain.cs',
    'Assets/Scripts/Models/Survival/Core/CharacterWaterConsumptionFacts.cs',
    'Assets/Scripts/Services/Survival/CharacterWaterConsumedFact.cs',
    'Assets/Scripts/Services/Narrative/GameplayOutcomes/Editor/GameplayDomainFactPairProofDebugScenarios.cs',
    'Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/Editor/DomainFactDispatcherSmokeScenarios.cs'
)
$observationUsings = [Collections.Generic.HashSet[string]]::new()
$observationMembers = [Collections.Generic.List[string]]::new()
$observationPins = [Collections.Generic.List[object]]::new()
$observationOptions = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithLanguageVersion(
    [Microsoft.CodeAnalysis.CSharp.LanguageVersion]::CSharp9).WithPreprocessorSymbols([string[]]@('UNITY_EDITOR'))
function Read-ObservationSource([string]$RelativePath, [switch]$SkipUsings) {
    $absolute = Join-Path $observationRoot $RelativePath
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($absolute), $observationOptions)
    $errors = @($tree.GetDiagnostics() | Where-Object Severity -eq Error)
    if ($errors.Count) { throw ($errors -join [Environment]::NewLine) }
    $observationPins.Add([pscustomobject]@{ path = $RelativePath; sha256 = (Get-FileHash -LiteralPath $absolute -Algorithm SHA256).Hash })
    $root = $tree.GetRoot()
    if (-not $SkipUsings) {
        foreach ($directive in $root.Usings) { [void]$observationUsings.Add($directive.ToString()) }
    }
    return $root
}
foreach ($path in $observationPaths) {
    $root = Read-ObservationSource $path
    foreach ($member in $root.Members) { $observationMembers.Add($member.ToString()) }
}
# The neutral death event is compiled exactly; its Unity actor/witness discovery factory is not.
foreach ($selection in @(
    @{ Path='Assets/Scripts/Services/Character/Core/CharacterDeathEvent.cs'; Names=@('CharacterDeathEvent') },
    @{ Path='Assets/Scripts/Models/CoreSession/ExternalInfluenceContracts.cs'; Names=@('CoreGridCell') },
    @{ Path='Assets/Scripts/Models/Species/Core/GriefTraumaDomain.cs'; Names=@('CharacterDeathCauseCode','CharacterLifeDeathRecord') }
)) {
    $selectedRoot = Read-ObservationSource $selection.Path -SkipUsings
    $selectedTypes = @($selectedRoot.Members | Where-Object { $selection.Names -contains $_.Identifier.ValueText })
    if ($selectedTypes.Count -ne $selection.Names.Count) { throw ('Missing death declaration: '+$selection.Path) }
    foreach ($member in $selectedTypes) { $observationMembers.Add($member.ToString()) }
}
# Exact production declarations are selected, not replaced by test implementations.
# Unity/ledger-dependent delivery reconciliation is deliberately outside this pure smoke.
$hashRoot = Read-ObservationSource 'Assets/Scripts/Models/AI/Core/V25NarrativeInferenceContracts.cs'
$hash = @($hashRoot.Members | Where-Object { $_.Identifier.ValueText -eq 'NarrativeInferenceHash' })
if ($hash.Count -ne 1) { throw 'Expected the exact production hash declaration.' }
$observationMembers.Add($hash[0].ToString())
$queryRoot = Read-ObservationSource 'Assets/Scripts/Services/Narrative/GameplayOutcomes/GameplayDomainFactDeliveryQuery.cs'
$query = @($queryRoot.Members | Where-Object { $_.Identifier.ValueText -eq 'GameplayDomainFactDeliveryQuery' })
$parser = @($query[0].Members | Where-Object { $_.Identifier.ValueText -eq 'TryReadKey' })
if ($parser.Count -ne 1) { throw 'Expected the exact production opaque-key parser.' }
$observationMembers.Add('internal static class GameplayDomainFactDeliveryQuery {' + $parser[0].ToString() + '}')
# The production enum members are unchanged; only Unity's assembly-move metadata
# is omitted in this non-Unity host. No substitute water/domain implementation.
$waterRoot = Read-ObservationSource 'Assets/Scripts/Models/Survival/Core/SurvivalPrimitives.cs' -SkipUsings
$waterEnum = @($waterRoot.Members | Where-Object { $_.Identifier.ValueText -eq 'WorldWaterQuality' })
if ($waterEnum.Count -ne 1) { throw 'Expected the exact production water-quality enum.' }
$emptyAttributes = [Activator]::CreateInstance([Microsoft.CodeAnalysis.SyntaxList[Microsoft.CodeAnalysis.CSharp.Syntax.AttributeListSyntax]])
$observationMembers.Add($waterEnum[0].WithAttributeLists($emptyAttributes).ToString())
# Minimal engine value compatibility for the exact physical source-fact declaration.
# This carries coordinates only; no Unity or item-domain behavior is substituted.
$observationMembers.Add(@'
namespace UnityEngine
{
    public readonly struct Vector2Int
    {
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
        public int x { get; }
        public int y { get; }
    }
    public static class Time
    {
        public static float unscaledTime { get; set; }
    }
    public static class Debug
    {
        public static void LogWarning(object message) { }
    }
}
namespace VContainer.Unity
{
    public interface IStartable { void Start(); }
    public interface ITickable { void Tick(); }
}
namespace VContainer
{
    [System.AttributeUsage(System.AttributeTargets.Constructor)]
    public sealed class InjectAttribute : System.Attribute { }
}
'@)
# Select the exact item receipt/kind declarations without compiling their Unity
# services, and derive the fire-resource constants from the production adapter.
$physicalReceiptRoot = Read-ObservationSource 'Assets/Scripts/Services/Items/PhysicalItemBatchDispositionService.cs' -SkipUsings
$physicalReceipts = @($physicalReceiptRoot.Members | Where-Object {
    @('PhysicalItemBatchDispositionReceipt', 'IPhysicalItemBatchDispositionService') -contains $_.Identifier.ValueText
})
if ($physicalReceipts.Count -ne 2) { throw 'Expected the exact physical batch-disposition receipt/service declarations.' }
foreach ($member in $physicalReceipts) { $observationMembers.Add($member.ToString()) }
$physicalTransformRoot = Read-ObservationSource 'Assets/Scripts/Services/Items/PhysicalItemTransformService.cs' -SkipUsings
$physicalTransformInputs = @($physicalTransformRoot.Members | Where-Object {
    $_.Identifier.ValueText -eq 'PhysicalItemTransformInput'
})
if ($physicalTransformInputs.Count -ne 1) { throw 'Expected the exact physical transform-input declaration.' }
$observationMembers.Add($physicalTransformInputs[0].ToString())
$physicalEffectRoot = Read-ObservationSource 'Assets/Scripts/Services/Items/PhysicalItemDispositionEffectContracts.cs' -SkipUsings
$physicalEffects = @($physicalEffectRoot.Members | Where-Object {
    $_.Identifier.ValueText -eq 'IPhysicalItemDispositionEffect'
})
if ($physicalEffects.Count -ne 1) { throw 'Expected the exact physical disposition-effect interface.' }
$observationMembers.Add($physicalEffects[0].ToString())
$saveSectionRoot = Read-ObservationSource 'Assets/Scripts/Services/Foundation/Save/DungeonSaveSections.cs' -SkipUsings
$restoreParticipants = @($saveSectionRoot.Members | Where-Object {
    $_.Identifier.ValueText -eq 'IDungeonRestoreTransactionParticipant'
})
if ($restoreParticipants.Count -ne 1) { throw 'Expected the exact restore transaction-participant interface.' }
$observationMembers.Add($restoreParticipants[0].ToString())
$aggregatePreflightRoot = Read-ObservationSource 'Assets/Scripts/Services/Infrastructure/Save/DungeonAggregateReferencePreflight.cs' -SkipUsings
$restoreHooks = @($aggregatePreflightRoot.Members | Where-Object {
    $_.Identifier.ValueText -eq 'IDungeonSaveRestoreCompletedHook'
})
if ($restoreHooks.Count -ne 1) { throw 'Expected the exact restore-completed hook interface.' }
$observationMembers.Add($restoreHooks[0].ToString())
$physicalRestoreRoot = Read-ObservationSource 'Assets/Scripts/Models/Items/Core/PhysicalItemRestoreContracts.cs' -SkipUsings
$physicalKinds = @($physicalRestoreRoot.Members | Where-Object {
    $_.Identifier.ValueText -eq 'PhysicalItemDispositionKind'
})
if ($physicalKinds.Count -ne 1) { throw 'Expected the exact physical item disposition-kind enum.' }
$observationMembers.Add($physicalKinds[0].ToString())
$fireAdapterRoot = Read-ObservationSource 'Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFireApplicationAdapters.cs' -SkipUsings
$fireAdapters = @($fireAdapterRoot.Members | Where-Object {
    $_.Identifier.ValueText -eq 'EnvironmentalFireWorldAdapter'
})
if ($fireAdapters.Count -ne 1) { throw 'Expected the production environmental-fire world adapter.' }
$fireConstantNames = @('CleanWaterItemId', 'WaterSinkReasonCode', 'FuelLossSinkReasonCode')
$fireConstants = @($fireAdapters[0].Members | Where-Object {
    $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax] -and
        @($_.Declaration.Variables | Where-Object { $fireConstantNames -contains $_.Identifier.ValueText }).Count -gt 0
})
if ($fireConstants.Count -ne 3) { throw 'Expected the three production environmental-fire resource constants.' }
$observationMembers.Add('public sealed class EnvironmentalFireWorldAdapter {' +
    (($fireConstants | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine) + '}')
# The full engine-free fire domain and exact runtime are compiled above. Only
# the global character-command enum and VContainer attribute need extraction.
$characterIdentityRoot = Read-ObservationSource 'Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs' -SkipUsings
$characterCommandOrigins = @($characterIdentityRoot.Members | Where-Object {
    $_.Identifier.ValueText -eq 'CharacterCommandOrigin'
})
if ($characterCommandOrigins.Count -ne 1) { throw 'Expected the exact production character-command origin enum.' }
$observationMembers.Add($characterCommandOrigins[0].ToString())
$calendarRoot = Read-ObservationSource 'Assets/Scripts/Models/CoreSession/CoreSessionContracts.cs' -SkipUsings
$calendarContracts = @($calendarRoot.Members | Where-Object {
    @('TimeOfDay', 'Season', 'GameCalendarRules', 'CalendarDateTime', 'IGameCalendar') -contains $_.Identifier.ValueText
})
if ($calendarContracts.Count -ne 5) { throw 'Expected the exact production game-calendar contracts.' }
foreach ($member in $calendarContracts) { $observationMembers.Add($member.ToString()) }
$randomRoot = Read-ObservationSource 'Assets/Scripts/Services/Foundation/Random/RandomStreamProvider.cs' -SkipUsings
$randomContracts = @($randomRoot.DescendantNodes() | Where-Object {
    $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax] -and
        @(
            'IRandomStream',
            'IRandomStreamProvider',
            'RandomStreamRestoreCandidate',
            'RandomStreamStateSnapshot',
            'RandomStreamAggregateState'
        ) -contains $_.Identifier.ValueText
})
if ($randomContracts.Count -ne 5) { throw 'Expected the exact production random-stream contracts.' }
$observationMembers.Add('namespace DungeonStory.Foundation {' +
    (($randomContracts | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine) + '}')
# Compile the exact engine-free reproduction aggregate/process. Its dependency
# declarations are selected from production files; only Unity serialization/move
# attributes are omitted from this pure C# host.
$lifeContractsRoot = Read-ObservationSource 'Assets/Scripts/Models/Species/Core/LifeSimulationContracts.cs' -SkipUsings
$reproductionContracts = @($lifeContractsRoot.Members | Where-Object {
    @('ReproductionMode', 'ReproductionPhaseKind', 'ReproductionPhaseDefinition') -contains $_.Identifier.ValueText
})
if ($reproductionContracts.Count -ne 3) { throw 'Expected the exact production reproduction-mode/phase declarations.' }
foreach ($member in $reproductionContracts) {
    $source = $member.WithAttributeLists($emptyAttributes).ToString()
    $source = $source -replace '\[Min\(1\)\]\s*', ''
    $observationMembers.Add($source)
}
$speciesRoot = Read-ObservationSource 'Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs' -SkipUsings
$speciesId = @($speciesRoot.Members | Where-Object { $_.Identifier.ValueText -eq 'CharacterSpeciesId' })
if ($speciesId.Count -ne 1) { throw 'Expected the exact production character-species ID declaration.' }
$observationMembers.Add($speciesId[0].WithAttributeLists($emptyAttributes).ToString())
$kinshipRoot = Read-ObservationSource 'Assets/Scripts/Models/Species/Core/KinshipDomain.cs' -SkipUsings
$kinshipDependencies = @($kinshipRoot.Members | Where-Object {
    @('KinshipRestriction', 'CharacterTombstoneSaveData', 'IKinshipQuery') -contains $_.Identifier.ValueText
})
if ($kinshipDependencies.Count -ne 3) { throw 'Expected the exact production reproduction kinship dependencies.' }
foreach ($member in $kinshipDependencies) { $observationMembers.Add($member.ToString()) }
# Select the exact knowledge DTO declarations and their two neutral dependencies.
# Unity's assembly-move attribute is omitted from the enum in this pure host.
$knowledgeRoot = Read-ObservationSource 'Assets/Scripts/Models/Research/Core/KnowledgeCompletionFacts.cs'
$knowledgeTypes = @($knowledgeRoot.Members | Where-Object {
    @('KnowledgeCompletionExperienceData', 'KnowledgeCompletionFactData') -contains $_.Identifier.ValueText
})
if ($knowledgeTypes.Count -ne 2) { throw 'Expected the exact production knowledge-completion DTO declarations.' }
foreach ($member in $knowledgeTypes) { $observationMembers.Add($member.ToString()) }
$seedRoot = Read-ObservationSource 'Assets/Scripts/Models/Economy/Core/CertifiedSeedCompletionFactData.cs'
$seedTypes = @($seedRoot.Members | Where-Object {
    $_.Identifier.ValueText -eq 'CertifiedSeedCompletionFactData'
})
if ($seedTypes.Count -ne 1) { throw 'Expected the exact production certified-seed completion DTO declaration.' }
$observationMembers.Add($seedTypes[0].ToString())
$outputPublicationRoot = Read-ObservationSource 'Assets/Scripts/Services/Foundation/ProductionOutputCapabilityContracts.cs'
$outputPublicationTypes = @($outputPublicationRoot.Members | Where-Object {
    @(
        'ProductionDomainPublishedStackSaveData',
        'ProductionDomainOutputAcknowledgementDisposition',
        'ProductionDomainOutputPublicationSaveData'
    ) -contains $_.Identifier.ValueText
})
if ($outputPublicationTypes.Count -ne 3) {
    throw 'Expected the exact production output-publication DTO declarations.'
}
foreach ($member in $outputPublicationTypes) { $observationMembers.Add($member.ToString()) }
$medicalRoot = Read-ObservationSource 'Assets/Scripts/Services/Combat/CharacterMedicalProcedureFactData.cs'
$medicalTypes = @($medicalRoot.Members | Where-Object {
    @(
        'CharacterMedicalProcedureKind',
        'CharacterMedicalPartFactData',
        'CharacterMedicalAnatomyFactData',
        'CharacterMedicalVitalsFactData',
        'CharacterMedicalProcedureFactData',
        'CharacterMedicalPendingFactData'
    ) -contains $_.Identifier.ValueText
})
if ($medicalTypes.Count -ne 6) { throw 'Expected the exact production medical fact DTO declarations.' }
foreach ($member in $medicalTypes) { $observationMembers.Add($member.ToString()) }
$medicalSupplyRoot = Read-ObservationSource 'Assets/Scripts/Models/Medical/Core/CharacterMedicalSupplyPolicy.cs' -SkipUsings
$medicalSupplyKind = @($medicalSupplyRoot.Members | Where-Object {
    $_.Identifier.ValueText -eq 'CharacterMedicalSupplyKind'
})
if ($medicalSupplyKind.Count -ne 1) { throw 'Expected the exact production medical supply-kind enum.' }
$observationMembers.Add($medicalSupplyKind[0].WithAttributeLists($emptyAttributes).ToString())
$itemRoot = Read-ObservationSource 'Assets/Scripts/Models/Items/Core/ItemPrimitives.cs' -SkipUsings
$itemTypes = @($itemRoot.Members | Where-Object {
    @('PhysicalItemBatchDispositionSaveData', 'PhysicalItemDispositionSourceFactSaveData') -contains $_.Identifier.ValueText
})
if ($itemTypes.Count -ne 2) { throw 'Expected the exact production physical-disposition DTO declarations.' }
foreach ($member in $itemTypes) { $observationMembers.Add($member.ToString()) }
$knowledgeContractRoot = Read-ObservationSource 'Assets/Scripts/Models/Research/Core/KnowledgeResidueContracts.cs' -SkipUsings
$knowledgeUse = @($knowledgeContractRoot.Members | Where-Object { $_.Identifier.ValueText -eq 'KnowledgeResidueUse' })
if ($knowledgeUse.Count -ne 1) { throw 'Expected the exact production knowledge-residue use enum.' }
$observationMembers.Add($knowledgeUse[0].WithAttributeLists($emptyAttributes).ToString())
$observationMembers.Add(@'
public static class WaterFactHostSmoke
{
    public static int Run()
    {
        int checks = 0;
        var data = new CharacterWaterFactData
        {
            characterId = "character:host-water", characterName = "생존자", sourceId = "water:host",
            sourceKind = CharacterWaterSourceKind.WorldWater, quality = WorldWaterQuality.Clean,
            pathogenDiseaseId = "", absoluteDay = 3, consumed = 1, thirstBefore = 5, thirstAfter = 70
        };
        var fact = new CharacterWaterConsumedFact(data);
        data.thirstAfter = 0;
        Require(fact.Data.thirstAfter == 70, "constructor did not freeze data"); checks++;
        var exposed = fact.Data; exposed.thirstAfter = 0;
        Require(fact.Snapshot().thirstAfter == 70, "getter exposed mutable data"); checks++;
        var saved = new CharacterWaterPublicationData { publicationKey = "opaque:water", fact = fact.Snapshot() };
        var copy = saved.Clone(); copy.fact.thirstAfter = 0; copy.notificationDispatched = true;
        Require(saved.fact.thirstAfter == 70 && !saved.notificationDispatched, "save clone shares state"); checks++;
        var proposed = fact.Data; proposed.consumed = 0;
        var proposal = new CharacterWaterConsumedFact(proposed, true);
        Require(proposal.MatchesHeader(fact) && !fact.IsProposal, "proposal header changed"); checks++;
        bool rejected = false;
        try { proposal.Snapshot(); } catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "proposal became actual evidence"); checks++;
        Action<CharacterWaterFactData>[] invalid =
        {
            value => value.consumed = 0, value => value.thirstAfter = float.NaN,
            value => value.healthBefore = float.PositiveInfinity, value => value.absoluteDay = 0,
            value => value.sourceId = "", value => value.quality = (WorldWaterQuality)99
        };
        foreach (var change in invalid)
        {
            var value = fact.Data; change(value); rejected = false;
            try { new CharacterWaterConsumedFact(value); } catch (ArgumentException) { rejected = true; }
            Require(rejected, "invalid fact accepted"); checks++;
        }
        var other = fact.Data; other.sourceId = "water:other";
        Require(!proposal.MatchesHeader(new CharacterWaterConsumedFact(other)), "changed source matched"); checks++;
        var noPathogen = fact.Data; noPathogen.pathogenDiseaseId = null;
        Require(new CharacterWaterConsumedFact(noPathogen).Snapshot().pathogenDiseaseId == "",
            "optional pathogen was not represented canonically in the frozen save"); checks++;
        return checks;
    }
    private static void Require(bool valid, string detail)
    { if (!valid) throw new InvalidOperationException(detail); }
}
'@)
$observationMembers.Add(@'
public static class KnowledgeDtoHostSmoke
{
    public static int Run()
    {
        int checks = 0;
        var empty = new KnowledgeCompletionFactData();
        Require(empty.IsEmpty, "default knowledge DTO was not empty"); checks++;

        var payload = new KnowledgeCompletionFactData();
        payload.experience.taskId = "knowledge-host";
        Require(!payload.IsEmpty, "knowledge payload remained empty"); checks++;

        var original = new KnowledgeCompletionFactData
        {
            experience = new KnowledgeCompletionExperienceData
            {
                taskId = "knowledge-host", facilityId = "facility:host",
                facilityName = "연구실", codexClue = "단서", absoluteDay = 3,
                rewardBefore = 1, rewardAfter = 2
            },
            physical = new PhysicalItemBatchDispositionSaveData
            {
                kind = 1, operationId = "operation:host", reasonCode = "reason:host",
                requestFingerprint = "1:reason:host:stack:host=1",
                sourceStackIds = new List<string> { "stack:host" }, quantity = 1,
                inputMassGrams = 200, commitId = "commit:host", ownerRevision = 4,
                sourceFacts = new List<PhysicalItemDispositionSourceFactSaveData>
                {
                    new PhysicalItemDispositionSourceFactSaveData
                    {
                        stackId = "stack:host", itemDefinitionId = "captivity:memory-residue",
                        quantity = 1, massGrams = 200, displayText = "기억 잔재"
                    }
                }
            }
        };
        var copy = original.Clone();
        copy.experience.facilityName = "변경";
        Require(original.experience.facilityName == "연구실", "experience clone shares state"); checks++;
        copy.physical.quantity = 2;
        Require(original.physical.quantity == 1, "physical clone shares state"); checks++;
        copy.physical.sourceStackIds[0] = "stack:changed";
        Require(original.physical.sourceStackIds[0] == "stack:host", "source-id clone shares state"); checks++;
        copy.physical.sourceFacts[0].displayText = "변경된 재료";
        copy.physical.sourceFacts.Add(new PhysicalItemDispositionSourceFactSaveData());
        Require(original.physical.sourceFacts.Count == 1
            && original.physical.sourceFacts[0].displayText == "기억 잔재",
            "source-fact clone shares list or element state"); checks++;

        var noExperience = new KnowledgeCompletionFactData { experience = null };
        Require(!noExperience.IsEmpty && noExperience.Clone().experience == null,
            "null experience was not preserved as non-empty"); checks++;
        var noPhysical = new KnowledgeCompletionFactData { physical = null };
        Require(!noPhysical.IsEmpty && noPhysical.Clone().physical == null,
            "null physical payload was not preserved as non-empty"); checks++;

        Action<PhysicalItemBatchDispositionSaveData>[] publicationChanges =
        {
            value => value.publicationExpected = true,
            value => value.publicationKey = "publication:key",
            value => value.publicationId = "publication:id",
            value => value.publicationFingerprint = new string('a', 64),
            value => value.publicationDelivered = true
        };
        foreach (var change in publicationChanges)
        {
            var changed = new KnowledgeCompletionFactData();
            change(changed.physical);
            Require(!changed.IsEmpty, "publication metadata remained empty");
        }
        checks++;
        return checks;
    }

    private static void Require(bool valid, string detail)
    { if (!valid) throw new InvalidOperationException(detail); }
}
'@)
$observationMembers.Add(@'
public static class CertifiedSeedDtoHostSmoke
{
    public static int Run()
    {
        int checks = 0;
        Require(new CertifiedSeedCompletionFactData().IsEmpty,
            "default certified-seed DTO was not empty"); checks++;

        var fields = typeof(CertifiedSeedCompletionFactData).GetFields();
        Require(fields.Length == 15, "certified-seed DTO field count drifted"); checks++;
        foreach (var field in fields)
        {
            var changed = new CertifiedSeedCompletionFactData();
            object changedValue = field.FieldType == typeof(string)
                ? (object)"changed"
                : field.FieldType == typeof(int) ? (object)1 : 1L;
            field.SetValue(changed, changedValue);
            Require(!changed.IsEmpty, "changed field remained empty: " + field.Name); checks++;

            if (field.FieldType == typeof(string))
            {
                var nullString = new CertifiedSeedCompletionFactData();
                field.SetValue(nullString, null);
                Require(!nullString.IsEmpty,
                    "null string was treated as default: " + field.Name); checks++;
            }
        }

        var snapshot = new CertifiedSeedCompletionFactData
        {
            actionId = "action:opaque", orderId = "order:opaque", orderSequence = 0,
            destinationId = "destination:opaque", facilityInstanceId = "facility:opaque",
            cropId = "crop:opaque", facilityDisplayName = "종자 연구실",
            cropDisplayName = "달 호밀", outputItemDefinitionId = "item:seed",
            outputItemDisplayName = "달 호밀 종자", outputQuantity = 2,
            outputMassGrams = 40, outputOutcomeFingerprint = new string('a', 64),
            absoluteDay = 3, ownerRevision = 9
        };
        var copy = snapshot.Clone();
        Require(copy.orderSequence == 0 && copy.actionId == snapshot.actionId
            && copy.outputOutcomeFingerprint == snapshot.outputOutcomeFingerprint,
            "zero-order snapshot values did not clone exactly"); checks++;
        copy.actionId = "action:changed";
        Require(snapshot.actionId == "action:opaque", "seed clone shares mutable state"); checks++;
        return checks;
    }

    private static void Require(bool valid, string detail)
    { if (!valid) throw new InvalidOperationException(detail); }
}
'@)
$observationMembers.Add(@'
public static class ProductionOutputPublicationDtoHostSmoke
{
    public static int Run()
    {
        int checks = 0;
        var publication = new ProductionDomainOutputPublicationSaveData
        {
            observationExpected = true,
            observationOwnerRevision = 23,
            observationKey = " opaque key :: member/01 ",
            observationId = " observation id ? no parsing ",
            observationFingerprint = "NOT-normalized-or-hashed",
            observationDelivered = true,
            stacks = new List<ProductionDomainPublishedStackSaveData>
            {
                new ProductionDomainPublishedStackSaveData
                {
                    outputLineId = "line:one", itemId = "item:one",
                    itemInstanceId = "instance:one", stackId = "stack:one",
                    quantity = 2, massGrams = 40, displayText = "원문 표시"
                }
            }
        };
        Require(ProductionDomainOutputPublicationSaveData.CurrentSchemaVersion == 7
            && publication.schemaVersion == 7, "output publication schema is not 7"); checks++;

        var copy = publication.Clone();
        Require(copy.observationExpected == publication.observationExpected
            && copy.observationOwnerRevision == publication.observationOwnerRevision
            && copy.observationKey == publication.observationKey
            && copy.observationId == publication.observationId
            && copy.observationFingerprint == publication.observationFingerprint
            && copy.observationDelivered == publication.observationDelivered,
            "opaque observation fields changed during clone"); checks++;
        Require(copy.observationKey == " opaque key :: member/01 "
            && copy.observationId == " observation id ? no parsing "
            && copy.observationFingerprint == "NOT-normalized-or-hashed",
            "opaque observation strings were parsed or normalized"); checks++;
        Require(copy.stacks.Count == 1 && !ReferenceEquals(copy.stacks, publication.stacks)
            && !ReferenceEquals(copy.stacks[0], publication.stacks[0]),
            "published stack was not deeply cloned"); checks++;
        copy.stacks[0].displayText = "변경";
        copy.stacks.Add(new ProductionDomainPublishedStackSaveData());
        Require(publication.stacks.Count == 1
            && publication.stacks[0].displayText == "원문 표시",
            "published stack clone shares list or element state"); checks++;

        var retired = new HashSet<string>
        {
            "displaySnapshotRevision", "pronunciation", "locale"
        };
        var publicFields = typeof(ProductionDomainOutputPublicationSaveData).GetFields()
            .Concat(typeof(ProductionDomainPublishedStackSaveData).GetFields());
        Require(!publicFields.Any(field =>
                field.Name.StartsWith("gameplayOutcome", StringComparison.Ordinal)
                || field.Name.StartsWith("expectedOutcome", StringComparison.Ordinal)
                || field.Name.StartsWith("pronunciation", StringComparison.Ordinal)
                || field.Name == "explicitFinalConsonant"
                || retired.Contains(field.Name)),
            "retired gameplay/display fields remain on the output DTO"); checks++;
        return checks;
    }

    private static void Require(bool valid, string detail)
    { if (!valid) throw new InvalidOperationException(detail); }
}
'@)
$observationMembers.Add(@'
public static class CharacterMedicalDtoHostSmoke
{
    public static int Run()
    {
        int checks = 0;
        var pending = new CharacterMedicalPendingFactData
        {
            publicationKey = "opaque:medical-publication",
            fact = new CharacterMedicalProcedureFactData
            {
                orderId = "medical:7", patientId = "character:patient",
                patientName = "환자", clinicianId = "character:clinician",
                clinicianName = "치료자", facilityId = "facility:clinic",
                materialItemId = "item:medicine", sequence = 7,
                absoluteDay = 11, materialQuantity = 1,
                procedure = CharacterMedicalProcedureKind.Treatment,
                materialKind = CharacterMedicalSupplyKind.Medicine,
                completedWork = 4.5f, requiredWork = 4.5f,
                before = Vitals(30f, 2f, true, 20f, 1f, "torso", 12f, 0.5f),
                after = Vitals(55f, 1f, false, 35f, 0f, "torso", 30f, 0f)
            }
        };
        CharacterMedicalPendingFactData copy = pending.Clone();
        Require(!ReferenceEquals(copy, pending), "pending root clone shares identity"); checks++;
        Require(!ReferenceEquals(copy.fact, pending.fact), "medical fact clone shares identity"); checks++;
        Require(!ReferenceEquals(copy.fact.before, pending.fact.before), "before vitals clone shares identity"); checks++;
        Require(!ReferenceEquals(copy.fact.after, pending.fact.after), "after vitals clone shares identity"); checks++;
        Require(!ReferenceEquals(copy.fact.before.parts, pending.fact.before.parts)
            && !ReferenceEquals(copy.fact.after.parts, pending.fact.after.parts),
            "part lists share identity"); checks++;
        Require(!ReferenceEquals(copy.fact.before.anatomyNodes, pending.fact.before.anatomyNodes)
            && !ReferenceEquals(copy.fact.after.anatomyNodes, pending.fact.after.anatomyNodes),
            "anatomy lists share identity"); checks++;
        Require(!ReferenceEquals(copy.fact.before.parts[0], pending.fact.before.parts[0])
            && !ReferenceEquals(copy.fact.after.parts[0], pending.fact.after.parts[0]),
            "part elements share identity"); checks++;
        Require(!ReferenceEquals(copy.fact.before.anatomyNodes[0], pending.fact.before.anatomyNodes[0])
            && !ReferenceEquals(copy.fact.after.anatomyNodes[0], pending.fact.after.anatomyNodes[0]),
            "anatomy elements share identity"); checks++;
        Require(copy.publicationKey == pending.publicationKey
            && copy.fact.orderId == pending.fact.orderId
            && copy.fact.patientId == pending.fact.patientId
            && copy.fact.patientName == pending.fact.patientName
            && copy.fact.clinicianId == pending.fact.clinicianId
            && copy.fact.clinicianName == pending.fact.clinicianName
            && copy.fact.facilityId == pending.fact.facilityId
            && copy.fact.materialItemId == pending.fact.materialItemId
            && copy.fact.sequence == pending.fact.sequence
            && copy.fact.absoluteDay == pending.fact.absoluteDay
            && copy.fact.materialQuantity == pending.fact.materialQuantity,
            "medical scalar metadata changed during clone"); checks++;
        Require(copy.fact.procedure == CharacterMedicalProcedureKind.Treatment
            && copy.fact.materialKind == CharacterMedicalSupplyKind.Medicine
            && copy.fact.completedWork == 4.5f && copy.fact.requiredWork == 4.5f,
            "medical enum or clinical work changed during clone"); checks++;
        Require(copy.fact.before.health == 30f && copy.fact.before.bloodLoss == 2f
            && copy.fact.before.downed && copy.fact.after.health == 55f
            && copy.fact.after.bloodLoss == 1f && !copy.fact.after.downed,
            "medical clinical values changed during clone"); checks++;
        copy.fact.before.parts[0].currentHealth = 1f;
        copy.fact.after.anatomyNodes[0].nodeId = "changed";
        Require(pending.fact.before.parts[0].currentHealth == 20f
            && pending.fact.after.anatomyNodes[0].nodeId == "torso",
            "before/after clone mutation changed the source"); checks++;

        var nullFact = new CharacterMedicalPendingFactData
            { publicationKey = null, fact = null }.Clone();
        Require(nullFact.publicationKey == null && nullFact.fact == null,
            "null pending fields were normalized"); checks++;
        var nullVitals = new CharacterMedicalProcedureFactData
            { before = null, after = null }.Clone();
        Require(nullVitals.before == null && nullVitals.after == null,
            "null before/after vitals were normalized"); checks++;
        var nullCollections = new CharacterMedicalVitalsFactData
        {
            parts = null,
            anatomyNodes = new List<CharacterMedicalAnatomyFactData> { null }
        }.Clone();
        Require(nullCollections.parts == null && nullCollections.anatomyNodes.Count == 1
            && nullCollections.anatomyNodes[0] == null,
            "null list or element was normalized"); checks++;
        return checks;
    }

    private static CharacterMedicalVitalsFactData Vitals(
        float health, float bloodLoss, bool downed,
        float partHealth, float partBleeding,
        string nodeId, float nodeHealth, float nodeBleeding) => new()
    {
        health = health,
        bloodLoss = bloodLoss,
        downed = downed,
        parts = new List<CharacterMedicalPartFactData>
        {
            new CharacterMedicalPartFactData
            { currentHealth = partHealth, maxHealth = 40f, bleedingPerSecond = partBleeding }
        },
        anatomyNodes = new List<CharacterMedicalAnatomyFactData>
        {
            new CharacterMedicalAnatomyFactData
            {
                nodeId = nodeId, currentHealth = nodeHealth, maxHealth = 40f,
                bleedingPerSecond = nodeBleeding, missing = false
            }
        }
    };

    private static void Require(bool valid, string detail)
    { if (!valid) throw new InvalidOperationException(detail); }
}
'@)
$observationMembers.Add(@'
public static class CharacterHealingFactHostSmoke
{
    public static int Run()
    {
        int checks = 0;
        CharacterHealingFactData source = Data(10f, 50f, true, false);
        var both = new CharacterHealingFact(source);
        Require(both.CrossedCritical && both.Recovered,
            "normal healing fact did not expose both transitions"); checks++;
        source.healthAfter = 99f;
        Require(both.Snapshot().healthAfter == 50f,
            "healing fact did not freeze constructor data"); checks++;
        CharacterHealingFactData exposed = both.Snapshot();
        exposed.healthAfter = 1f;
        Require(both.Snapshot().healthAfter == 50f,
            "healing snapshot exposed mutable state"); checks++;

        Require(new CharacterHealingFact(Data(20f, 21f, false, false)).CrossedCritical,
            "20-to-21 critical boundary was not detected"); checks++;
        Require(new CharacterHealingFact(Data(21f, 20f, false, false)).CrossedCritical,
            "21-to-20 critical boundary was not detected"); checks++;
        Require(!new CharacterHealingFact(Data(20f, 20f, false, false)).CrossedCritical,
            "20-to-20 incorrectly crossed the critical boundary"); checks++;

        var unchanged = new CharacterHealingFact(Data(30f, 40f, false, false));
        Require(!unchanged.CrossedCritical && !unchanged.Recovered
            && Rejected(() => _ = new CharacterCriticalHealthTransitionFact(unchanged))
            && Rejected(() => _ = new CharacterMobilityRecoveredFact(unchanged)),
            "actual wrappers accepted a fact without their transition"); checks++;
        var proposal = new CharacterHealingFact(Data(30f, 40f, false, false), true);
        _ = new CharacterCriticalHealthTransitionFact(proposal);
        _ = new CharacterMobilityRecoveredFact(proposal);
        Require(proposal.IsProposal, "proposal wrappers rejected a reservation shape"); checks++;
        bool snapshotRejected = false;
        try { proposal.Snapshot(); } catch (InvalidOperationException) { snapshotRejected = true; }
        Require(snapshotRejected, "proposal exposed an applied snapshot"); checks++;

        Require(Rejected(() => _ = new CharacterHealingFact(
                Data(10f, 50f, true, false, characterId: "invalid"))),
            "invalid character ID was accepted"); checks++;
        Require(Rejected(() => { var value = Data(10f, 50f, true, false); value.absoluteDay = 0;
                _ = new CharacterHealingFact(value); }),
            "day zero was accepted"); checks++;
        Require(Rejected(() => _ = new CharacterHealingFact(
                Data(float.NaN, 50f, true, false))),
            "NaN health was accepted"); checks++;
        Require(Rejected(() => { var value = Data(10f, 50f, true, false);
                value.maximumHealthBefore = 0f; _ = new CharacterHealingFact(value); }),
            "zero maximum health was accepted"); checks++;
        Require(Rejected(() => { var value = Data(10f, 50f, true, false);
                value.healthAfter = 101f; _ = new CharacterHealingFact(value); }),
            "health above maximum was accepted"); checks++;

        CharacterHealingFact.ValidatePending(new List<CharacterHealingPendingFactData>
        {
            Pending(both, "critical:both", "recovery:both")
        });
        Require(true, "two-key pending healing fact was rejected"); checks++;
        var criticalOnly = new CharacterHealingFact(Data(10f, 50f, false, false));
        CharacterHealingFact.ValidatePending(new List<CharacterHealingPendingFactData>
        {
            Pending(criticalOnly, "critical:only", "")
        });
        Require(true, "critical-only pending key selection was rejected"); checks++;
        var recoveryOnly = new CharacterHealingFact(Data(30f, 40f, true, false));
        CharacterHealingFact.ValidatePending(new List<CharacterHealingPendingFactData>
        {
            Pending(recoveryOnly, "", "recovery:only")
        });
        Require(true, "recovery-only pending key selection was rejected"); checks++;
        Require(Rejected(() => CharacterHealingFact.ValidatePending(
                new List<CharacterHealingPendingFactData>
                { Pending(both, "critical:missing-recovery", "") })),
            "missing pending transition key was accepted"); checks++;
        Require(Rejected(() => CharacterHealingFact.ValidatePending(
                new List<CharacterHealingPendingFactData>
                { Pending(unchanged, "critical:wrong", "") })),
            "wrong pending transition key was accepted"); checks++;
        Require(Rejected(() => CharacterHealingFact.ValidatePending(
                new List<CharacterHealingPendingFactData>
                {
                    Pending(criticalOnly, "critical:duplicate", ""),
                    Pending(criticalOnly, "critical:duplicate", "")
                })),
            "duplicate pending publication keys were accepted"); checks++;
        Require(Rejected(() => CharacterHealingFact.ValidatePending(
                new List<CharacterHealingPendingFactData>
                {
                    Pending(criticalOnly, "critical:first", ""),
                    Pending(criticalOnly, "critical:second", "")
                })),
            "multiple unresolved facts for the same healing owner were accepted"); checks++;

        var nullClone = new CharacterHealingPendingFactData
        {
            criticalPublicationKey = null,
            recoveryPublicationKey = null,
            fact = null
        }.Clone();
        Require(nullClone.criticalPublicationKey == null
            && nullClone.recoveryPublicationKey == null
            && nullClone.fact == null,
            "healing DTO clone normalized null fields"); checks++;
        return checks;
    }

    private static CharacterHealingFactData Data(
        float before,
        float after,
        bool downedBefore,
        bool downedAfter,
        string characterId = "character:test") => new()
    {
        characterId = characterId,
        characterName = "테스트",
        absoluteDay = 1,
        healthBefore = before,
        maximumHealthBefore = 100f,
        healthAfter = after,
        maximumHealthAfter = 100f,
        downedBefore = downedBefore,
        downedAfter = downedAfter
    };

    private static CharacterHealingPendingFactData Pending(
        CharacterHealingFact fact,
        string critical,
        string recovery) => new()
    {
        criticalPublicationKey = critical,
        recoveryPublicationKey = recovery,
        fact = fact.Snapshot()
    };

    private static bool Rejected(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return true; }
        return false;
    }

    private static void Require(bool valid, string detail)
    { if (!valid) throw new InvalidOperationException(detail); }
}
'@)
$observationMembers.Add(@'
public static class ServiceSessionTerminalFactHostSmoke
{
    private const string HubId = "building:service-terminal";
    private const string HubName = "서비스 터미널 허브";
    private const string PublicationKey = "opaque|terminal key|한글";
    private const string PublicationId = "opaque observation id | 한글";
    private const string PublicationFingerprint = "not-a-sha fingerprint | 한글";

    public static int Run()
    {
        int checks = 0;
        DungeonStory.ServiceRooms.ServiceSessionAggregate aggregate =
            CreateActive(HubId, out ServiceSessionSnapshot active);
        Require(aggregate.TryComplete(
                active.SessionId,
                completedAt: 5f,
                completedAbsoluteDay: 8,
                out DungeonStory.ServiceRooms.ServiceSessionCompletionTransition
                    transition,
                out DomainFailure completeFailure)
            && !completeFailure.IsFailure
            && transition.Completed.TerminalPhase
                == ServiceSessionTerminalPhase.Prepared
            && !transition.Completed.PaymentCommitted
            && transition.EconomicCommand != null
            && transition.EconomicCommand.Amount == 25,
            "service completion did not begin in Prepared with one payment command"); checks++;

        ServiceSessionTerminalFact proposal = ServiceSessionTerminalFact.Propose(
            transition.Completed,
            ServiceSessionStage.Completed,
            8,
            HubName);
        Require(proposal.IsProposal
            && proposal.Snapshot().Stage == ServiceSessionStage.Completed
            && !proposal.Snapshot().PaymentCommitted,
            "unpaid service completion did not remain proposal-only"); checks++;

        Require(!aggregate.TryRecordPayment(
                active.SessionId,
                "transaction:before-bind",
                ServiceSessionTerminalIdentity.PaymentSourceId(active.SessionId),
                25,
                100,
                125),
            "service payment was recorded before terminal observation binding"); checks++;
        bool firstBinding = aggregate.TryBindTerminalObservation(
            active.SessionId, PublicationKey, HubName);
        bool repeatedBinding = aggregate.TryBindTerminalObservation(
            active.SessionId, PublicationKey, HubName);
        bool changedBinding = aggregate.TryBindTerminalObservation(
            active.SessionId, PublicationKey, "변경된 허브");
        bool foundBound = aggregate.TryGetSession(
            active.SessionId, out ServiceSessionSnapshot bound);
        Require(firstBinding
            && repeatedBinding
            && !changedBinding
            && foundBound
            && bound.TerminalPublicationKey == PublicationKey
            && bound.TerminalHubName == HubName,
            "terminal observation binding did not freeze opaque key and hub name"); checks++;
        Require(ArgumentRejected(() => ServiceSessionTerminalFact.Actual(bound)),
            "unpaid bound service completion became an actual fact"); checks++;

        string paymentSource = ServiceSessionTerminalIdentity.PaymentSourceId(
            active.SessionId);
        bool paymentRecorded = aggregate.TryRecordPayment(
            active.SessionId,
            "transaction:service-terminal",
            paymentSource,
            25,
            100,
            125);
        bool duplicatePayment = aggregate.TryRecordPayment(
            active.SessionId,
            "transaction:duplicate",
            paymentSource,
            25,
            125,
            150);
        bool foundPaid = aggregate.TryGetSession(
            active.SessionId, out ServiceSessionSnapshot paid);
        Require(paymentRecorded
            && !duplicatePayment
            && foundPaid
            && paid.TerminalPhase == ServiceSessionTerminalPhase.PaymentCommitted
            && paid.PaymentCommitted
            && paid.EconomyTransactionId == "transaction:service-terminal"
            && paid.EconomyTransactionSourceId == paymentSource
            && paid.PaymentAmount == 25
            && paid.BalanceBefore == 100
            && paid.BalanceAfter == 125,
            "service terminal payment receipt was not recorded exactly once"); checks++;

        ServiceSessionTerminalFact actual = ServiceSessionTerminalFact.Actual(paid);
        ServiceSessionSnapshot exposed = actual.Snapshot();
        exposed.TerminalHubName = "변경";
        exposed.Contract.supportIds[0] = "support:changed";
        ServiceSessionSnapshot frozen = actual.Snapshot();
        Require(!actual.IsProposal
            && actual.HubName == HubName
            && frozen.TerminalHubName == HubName
            && frozen.Contract.supportIds[0] == "support:terminal"
            && frozen.TerminalPublicationId.Length == 0
            && frozen.TerminalPublicationFingerprint.Length == 0,
            "actual service terminal fact did not return a deep frozen snapshot"); checks++;

        ServiceRoomsSaveData pendingSave = aggregate.Capture();
        var restored = DungeonStory.ServiceRooms.ServiceSessionAggregate.CreateRestored(
            pendingSave,
            aggregate.Version);
        pendingSave.sessions[0].terminalPublicationKey = "changed";
        pendingSave.sessions[0].terminalHubName = "changed";
        pendingSave.sessions[0].paymentAmount = 999;
        Require(restored.TryGetSession(
                active.SessionId, out ServiceSessionSnapshot restoredPending)
            && restoredPending.TerminalPhase
                == ServiceSessionTerminalPhase.PaymentCommitted
            && restoredPending.TerminalPublicationKey == PublicationKey
            && restoredPending.TerminalHubName == HubName
            && restoredPending.PaymentCommitted
            && restoredPending.PaymentAmount == 25
            && restoredPending.TerminalPublicationId.Length == 0
            && restoredPending.TerminalPublicationFingerprint.Length == 0,
            "unknown terminal publication did not survive fresh aggregate restore"); checks++;

        Require(restored.TryRecordObservationCommit(
                active.SessionId,
                PublicationId,
                PublicationFingerprint)
            && !restored.TryRecordObservationCommit(
                active.SessionId,
                "changed observation id",
                PublicationFingerprint),
            "terminal observation commit accepted a changed opaque proof"); checks++;
        Require(!restored.TryAcknowledgeTerminal(
                active.SessionId,
                "changed observation id",
                PublicationFingerprint)
            && restored.TryAcknowledgeTerminal(
                active.SessionId,
                PublicationId,
                PublicationFingerprint)
            && restored.TryGetSession(
                active.SessionId, out ServiceSessionSnapshot acknowledged)
            && acknowledged.TerminalPhase
                == ServiceSessionTerminalPhase.Acknowledged
            && acknowledged.TerminalPublicationKey == PublicationKey
            && acknowledged.TerminalPublicationId == PublicationId
            && acknowledged.TerminalPublicationFingerprint
                == PublicationFingerprint
            && restored.PendingTerminalSessions.Count == 0,
            "terminal ACK changed proof or required key/SHA parsing"); checks++;

        var cancelledAggregate = CreateActive(
            "building:service-terminal:cancelled",
            out ServiceSessionSnapshot cancellable);
        Require(cancelledAggregate.CancelSession(
                cancellable.SessionId,
                "service.cancelled:test",
                cancelledAt: 3f,
                cancelledAbsoluteDay: 9)
            && cancelledAggregate.TryBindTerminalObservation(
                cancellable.SessionId,
                "opaque cancellation key",
                "취소 허브")
            && cancelledAggregate.TryGetSession(
                cancellable.SessionId,
                out ServiceSessionSnapshot cancelled)
            && !cancelled.PaymentCommitted
            && cancelled.EconomyTransactionId.Length == 0
            && !cancelledAggregate.TryRecordPayment(
                cancellable.SessionId,
                "transaction:cancelled",
                ServiceSessionTerminalIdentity.PaymentSourceId(
                    cancellable.SessionId),
                25,
                100,
                125)
            && !ServiceSessionTerminalFact.Actual(cancelled).IsProposal
            && cancelledAggregate.TryRecordObservationCommit(
                cancellable.SessionId,
                "opaque cancellation observation",
                "opaque cancellation fingerprint")
            && cancelledAggregate.TryAcknowledgeTerminal(
                cancellable.SessionId,
                "opaque cancellation observation",
                "opaque cancellation fingerprint"),
            "cancelled service terminal required or recorded a payment"); checks++;
        return checks;
    }

    private static DungeonStory.ServiceRooms.ServiceSessionAggregate CreateActive(
        string hubId,
        out ServiceSessionSnapshot active)
    {
        var aggregate = new DungeonStory.ServiceRooms.ServiceSessionAggregate();
        bool began = aggregate.TryBegin(
            new DungeonStory.ServiceRooms.ServiceSessionBeginCommand
            {
                HubId = hubId,
                ActorId = "character:service-terminal",
                ProcessId = "service-process:terminal",
                Category = ServiceCategory.Dining,
                Capacity = 1,
                StartedAt = 1f,
                Contract = new ServiceSessionContractSnapshot
                {
                    mode = ServiceOperationMode.Direct,
                    activeStages = ServiceProcessStageMask.Service,
                    serviceSeconds = 4f,
                    price = 25,
                    paymentRequired = true,
                    supportIds = new[] { "support:terminal" }
                }
            },
            out active,
            out DomainFailure failure);
        Require(began && !failure.IsFailure && active != null,
            "service terminal fixture could not begin its actual aggregate session");
        return aggregate;
    }

    private static bool ArgumentRejected(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return true; }
        return false;
    }

    private static void Require(bool valid, string detail)
    { if (!valid) throw new InvalidOperationException(detail); }
}
'@)
$observationMembers.Add(@'
public static class ServiceModeAggregateHostSmoke
{
    private const string HubId = "building:service-mode:aggregate";
    private const string PublicationKey = "mode:aggregate:managed-direct";

    public static int Run()
    {
        int checks = 0;
        DungeonStory.ServiceRooms.ServiceSessionAggregate aggregate =
            CreatePendingAggregate();
        Require(aggregate.ResolveMode(HubId) == ServiceOperationMode.Direct
            && aggregate.PendingModeFacts.Count == 1
            && aggregate.PendingModeFacts[0].publicationKey == PublicationKey,
            "managed-to-direct mode mutation did not record its pending fact"); checks++;

        ServiceRoomsSaveData mutableCapture = aggregate.Capture();
        mutableCapture.pendingModeFacts[0].publicationKey = "mode:mutated";
        mutableCapture.pendingModeFacts[0].fact.hubName = "변경";
        ServiceRoomsSaveData freshCapture = aggregate.Capture();
        Require(freshCapture.pendingModeFacts[0].publicationKey == PublicationKey
            && freshCapture.pendingModeFacts[0].fact.hubName == "서비스 허브",
            "service aggregate capture shared pending fact state"); checks++;

        ServiceRoomsSaveData restoreSource = aggregate.Capture();
        var restored = DungeonStory.ServiceRooms.ServiceSessionAggregate.CreateRestored(
            restoreSource, aggregate.Version);
        restoreSource.hubs[0].mode = ServiceOperationMode.Managed;
        restoreSource.pendingModeFacts[0].fact.hubName = "복원 입력 변경";
        Require(restored.ResolveMode(HubId) == ServiceOperationMode.Direct
            && restored.PendingModeFacts.Count == 1
            && restored.PendingModeFacts[0].fact.hubName == "서비스 허브",
            "restored service aggregate retained mutable save input or lost mode state"); checks++;

        Require(InvalidRejected(() => restored.SetMode(
                HubId, ServiceOperationMode.Automated, out _)),
            "pending service mode fact allowed another mode mutation"); checks++;
        restored.ClearModeFact(HubId, PublicationKey);
        Require(restored.SetMode(HubId, ServiceOperationMode.Automated, out var previous)
            && previous == ServiceOperationMode.Direct
            && restored.ResolveMode(HubId) == ServiceOperationMode.Automated,
            "clearing the pending service mode fact did not release the owner"); checks++;

        var retired = DungeonStory.ServiceRooms.ServiceSessionAggregate.CreateRestored(
            aggregate.Capture(), aggregate.Version);
        Require(retired.RemoveHubModesExcept(new HashSet<string>()) == 1
            && retired.PendingModeFacts.Count == 1,
            "removing retired hub modes removed the pending historical fact"); checks++;
        var retiredRestored = DungeonStory.ServiceRooms.ServiceSessionAggregate.CreateRestored(
            retired.Capture(), retired.Version);
        Require(retiredRestored.ResolveMode(HubId) == ServiceOperationMode.Direct
            && retiredRestored.PendingModeFacts.Count == 1
            && retiredRestored.PendingModeFacts[0].publicationKey == PublicationKey,
            "retired-hub pending fact did not survive aggregate restore"); checks++;

        ServiceRoomsSaveData mismatch = aggregate.Capture();
        mismatch.hubs[0].mode = ServiceOperationMode.Managed;
        Require(InvalidRejected(() =>
                DungeonStory.ServiceRooms.ServiceSessionAggregate.CreateRestored(
                    mismatch, aggregate.Version)),
            "pending service mode fact accepted mismatched saved owner state"); checks++;

        ServiceRoomsSaveData duplicateOwner = aggregate.Capture();
        duplicateOwner.pendingModeFacts.Add(new ServiceModePendingFactData
        {
            publicationKey = "mode:aggregate:duplicate-owner",
            fact = duplicateOwner.pendingModeFacts[0].fact.Clone()
        });
        Require(InvalidRejected(() =>
                DungeonStory.ServiceRooms.ServiceSessionAggregate.CreateRestored(
                    duplicateOwner, aggregate.Version)),
            "duplicate pending service mode owner was accepted on restore"); checks++;

        ServiceRoomsSaveData duplicateKey = aggregate.Capture();
        const string OtherHubId = "building:service-mode:aggregate:other";
        duplicateKey.hubs.Add(new ServiceHubModeSaveData
        {
            hubId = OtherHubId,
            mode = ServiceOperationMode.Direct
        });
        duplicateKey.pendingModeFacts.Add(new ServiceModePendingFactData
        {
            publicationKey = PublicationKey,
            fact = ModeFact(
                OtherHubId,
                ServiceOperationMode.Managed,
                ServiceOperationMode.Direct).Snapshot()
        });
        Require(InvalidRejected(() =>
                DungeonStory.ServiceRooms.ServiceSessionAggregate.CreateRestored(
                    duplicateKey, aggregate.Version)),
            "duplicate pending service mode publication key was accepted on restore"); checks++;
        return checks;
    }

    private static DungeonStory.ServiceRooms.ServiceSessionAggregate
        CreatePendingAggregate()
    {
        var aggregate = new DungeonStory.ServiceRooms.ServiceSessionAggregate();
        Require(aggregate.SetMode(
                HubId, ServiceOperationMode.Managed, out var initial)
            && initial == ServiceOperationMode.Direct,
            "service aggregate did not establish the managed source mode");
        Require(aggregate.SetMode(
                HubId, ServiceOperationMode.Direct, out var previous)
            && previous == ServiceOperationMode.Managed,
            "service aggregate did not apply managed-to-direct mode mutation");
        aggregate.RecordModeFact(new ServiceModePendingFactData
        {
            publicationKey = PublicationKey,
            fact = ModeFact(
                HubId,
                ServiceOperationMode.Managed,
                ServiceOperationMode.Direct).Snapshot()
        });
        return aggregate;
    }

    private static ServiceModeChangedFact ModeFact(
        string hubId,
        ServiceOperationMode previous,
        ServiceOperationMode requested) => new(new ServiceModeChangedFactData
        {
            hubId = hubId,
            hubName = "서비스 허브",
            previousMode = previous,
            requestedMode = requested,
            absoluteDay = 4
        });

    private static bool InvalidRejected(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return true; }
        return false;
    }

    private static void Require(bool valid, string detail)
    { if (!valid) throw new InvalidOperationException(detail); }
}
'@)
$observationMembers.Add(@'
public static class ServiceModeFactHostSmoke
{
    public static int Run()
    {
        int checks = 0;
        ServiceModeChangedFactData source = Data(
            "building:service-mode", ServiceOperationMode.Direct,
            ServiceOperationMode.Managed, 0);
        var directToManaged = new ServiceModeChangedFact(source);
        ServiceModeChangedFactData first = directToManaged.Snapshot();
        Require(first.previousMode == ServiceOperationMode.Direct
            && first.requestedMode == ServiceOperationMode.Managed
            && first.absoluteDay == 0,
            "direct-to-managed service mode fact changed values"); checks++;
        var automatedToDirect = new ServiceModeChangedFact(Data(
            "building:service-mode:reverse", ServiceOperationMode.Automated,
            ServiceOperationMode.Direct, 7));
        Require(automatedToDirect.Snapshot().requestedMode == ServiceOperationMode.Direct,
            "automated-to-direct service mode fact was rejected or changed"); checks++;

        source.hubName = "변경";
        source.requestedMode = ServiceOperationMode.Automated;
        Require(directToManaged.Snapshot().hubName == "서비스 허브"
            && directToManaged.Snapshot().requestedMode == ServiceOperationMode.Managed,
            "service mode fact retained mutable constructor input"); checks++;
        ServiceModeChangedFactData snapshot = directToManaged.Snapshot();
        snapshot.hubName = "스냅숏 변경";
        Require(directToManaged.Snapshot().hubName == "서비스 허브",
            "service mode snapshot exposed mutable fact state"); checks++;
        ServiceModeChangedFactData dto = Data(
            "building:service-mode:dto", ServiceOperationMode.Managed,
            ServiceOperationMode.Automated, 2);
        ServiceModeChangedFactData dtoClone = dto.Clone();
        dtoClone.hubId = "building:changed";
        Require(!ReferenceEquals(dto, dtoClone)
            && dto.hubId == "building:service-mode:dto",
            "service mode DTO clone shared mutable root state"); checks++;

        Require(ArgumentRejected(() => _ = new ServiceModeChangedFact(null)),
            "null service mode fact data was accepted"); checks++;
        Require(ArgumentRejected(() => _ = new ServiceModeChangedFact(Data(
                "building:bad id", ServiceOperationMode.Direct,
                ServiceOperationMode.Managed, 0))),
            "invalid service hub identity was accepted"); checks++;
        Require(ArgumentRejected(() => { var value = Data(); value.hubName = " ";
                _ = new ServiceModeChangedFact(value); }),
            "blank service hub name was accepted"); checks++;
        Require(ArgumentRejected(() => _ = new ServiceModeChangedFact(Data(
                "building:bad-enum", (ServiceOperationMode)99,
                ServiceOperationMode.Managed, 0))),
            "undefined service operation mode was accepted"); checks++;
        Require(ArgumentRejected(() => _ = new ServiceModeChangedFact(Data(
                "building:bad-day", ServiceOperationMode.Direct,
                ServiceOperationMode.Managed, -1))),
            "negative service mode day was accepted"); checks++;
        Require(ArgumentRejected(() => _ = new ServiceModeChangedFact(Data(
                "building:same-mode", ServiceOperationMode.Managed,
                ServiceOperationMode.Managed, 1))),
            "same-mode transition was accepted"); checks++;

        ServiceModeChangedFact.ValidatePending(new List<ServiceModePendingFactData>
        {
            Pending(directToManaged, "mode:direct-managed"),
            Pending(automatedToDirect, "mode:automated-direct")
        });
        Require(true, "valid service mode pending facts were rejected"); checks++;
        Require(PendingRejected(() => ServiceModeChangedFact.ValidatePending(null)),
            "null service mode pending list was accepted"); checks++;
        var overCapacity = new List<ServiceModePendingFactData>(
            ServiceModeChangedFact.MaximumPending + 1);
        for (int index = 0; index <= ServiceModeChangedFact.MaximumPending; index++)
        {
            overCapacity.Add(Pending(new ServiceModeChangedFact(Data(
                "building:capacity:" + index, ServiceOperationMode.Direct,
                ServiceOperationMode.Managed, index)), "mode:capacity:" + index));
        }
        Require(PendingRejected(() => ServiceModeChangedFact.ValidatePending(overCapacity)),
            "over-capacity service mode pending list was accepted"); checks++;
        Require(PendingRejected(() => ServiceModeChangedFact.ValidatePending(
                new List<ServiceModePendingFactData>
                {
                    Pending(directToManaged, "mode:hub:first"),
                    Pending(directToManaged, "mode:hub:second")
                })),
            "duplicate service hub pending facts were accepted"); checks++;
        Require(PendingRejected(() => ServiceModeChangedFact.ValidatePending(
                new List<ServiceModePendingFactData>
                {
                    Pending(directToManaged, "mode:duplicate"),
                    Pending(automatedToDirect, "mode:duplicate")
                })),
            "duplicate service mode publication keys were accepted"); checks++;
        Require(PendingRejected(() => ServiceModeChangedFact.ValidatePending(
                new List<ServiceModePendingFactData>
                { Pending(directToManaged, " ") })),
            "missing service mode publication key was accepted"); checks++;

        ServiceModePendingFactData pending = Pending(
            directToManaged, "mode:clone");
        ServiceModePendingFactData pendingClone = pending.Clone();
        pendingClone.publicationKey = "mode:changed";
        pendingClone.fact.hubName = "변경";
        Require(!ReferenceEquals(pending, pendingClone)
            && !ReferenceEquals(pending.fact, pendingClone.fact)
            && pending.publicationKey == "mode:clone"
            && pending.fact.hubName == "서비스 허브",
            "service mode pending clone was not deeply isolated"); checks++;
        return checks;
    }

    private static ServiceModeChangedFactData Data(
        string hubId = "building:service-mode:default",
        ServiceOperationMode previous = ServiceOperationMode.Direct,
        ServiceOperationMode requested = ServiceOperationMode.Managed,
        int day = 0) => new()
    {
        hubId = hubId,
        hubName = "서비스 허브",
        previousMode = previous,
        requestedMode = requested,
        absoluteDay = day
    };

    private static ServiceModePendingFactData Pending(
        ServiceModeChangedFact fact,
        string publicationKey) => new()
    {
        publicationKey = publicationKey,
        fact = fact.Snapshot()
    };

    private static bool ArgumentRejected(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return true; }
        return false;
    }

    private static bool PendingRejected(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return true; }
        return false;
    }

    private static void Require(bool valid, string detail)
    { if (!valid) throw new InvalidOperationException(detail); }
}
'@)
$observationMembers.Add(@'
public static class CharacterFatalityFactHostSmoke
{
    public static int Run()
    {
        int checks = 0;
        CharacterFatalityFactData source = Data(100f, 0f, false, true, "character:fatality");
        var fatality = new CharacterFatalityFact(source);
        Require(fatality.CrossedCritical && fatality.BecameDowned && fatality.RatioBefore == 1f,
            "100-to-zero fatality did not expose critical and downed transitions"); checks++;

        var noCritical = new CharacterFatalityFact(
            Data(20f, 0f, false, true, "character:threshold"));
        Require(!noCritical.CrossedCritical,
            "20-percent fatality incorrectly crossed the critical boundary"); checks++;
        var alreadyDowned = new CharacterFatalityFact(
            Data(100f, 0f, true, true, "character:already-downed"));
        Require(!alreadyDowned.BecameDowned,
            "already-downed fatality reported a new downed transition"); checks++;

        source.healthBefore = 1f;
        Require(fatality.Snapshot().healthBefore == 100f,
            "fatality fact did not freeze constructor data"); checks++;
        CharacterFatalityFactData exposed = fatality.Data;
        exposed.characterName = "변경";
        Require(fatality.Data.characterName == "테스트",
            "fatality Data getter exposed mutable state"); checks++;
        CharacterFatalityFactData dto = Data(80f, 0f, false, true, "character:dto");
        CharacterFatalityFactData dtoClone = dto.Clone();
        dtoClone.reasonCode = "changed";
        Require(!ReferenceEquals(dto, dtoClone) && dto.reasonCode == "combat",
            "fatality DTO clone retained mutable root state"); checks++;
        CharacterFatalityPendingFactData pendingSource = Pending(
            fatality, "death:clone", "critical:clone", "downed:clone");
        CharacterFatalityPendingFactData pendingClone = pendingSource.Clone();
        pendingClone.fact.characterName = "변경";
        pendingClone.deathPublicationKey = "death:changed";
        Require(!ReferenceEquals(pendingSource, pendingClone)
            && !ReferenceEquals(pendingSource.fact, pendingClone.fact)
            && pendingSource.fact.characterName == "테스트"
            && pendingSource.deathPublicationKey == "death:clone",
            "fatality pending DTO clone was not isolated"); checks++;
        CharacterFatalityPendingFactData nullClone = new CharacterFatalityPendingFactData
        {
            deathPublicationKey = null,
            criticalPublicationKey = null,
            downedPublicationKey = null,
            fact = null
        }.Clone();
        Require(nullClone.deathPublicationKey == null
            && nullClone.criticalPublicationKey == null
            && nullClone.downedPublicationKey == null
            && nullClone.fact == null,
            "fatality pending DTO clone normalized null fields"); checks++;

        var proposal = new CharacterFatalityFact(
            Data(20f, 10f, false, false, "character:proposal"), true);
        bool snapshotRejected = false;
        try { proposal.Snapshot(); } catch (InvalidOperationException) { snapshotRejected = true; }
        Require(snapshotRejected, "fatality proposal exposed an applied snapshot"); checks++;
        _ = new CharacterFatalCriticalTransitionFact(proposal);
        _ = new CharacterFatalDownedFact(proposal);
        Require(proposal.IsProposal, "fatality proposal wrappers rejected a reservation shape"); checks++;
        Require(Rejected(() => _ = new CharacterFatalCriticalTransitionFact(noCritical)),
            "actual critical wrapper accepted a fatality without the transition"); checks++;
        Require(Rejected(() => _ = new CharacterFatalDownedFact(alreadyDowned)),
            "actual downed wrapper accepted a fatality without the transition"); checks++;

        Require(Rejected(() => _ = new CharacterFatalityFact(
                Data(100f, 0f, false, true, "invalid"))),
            "invalid fatality character ID was accepted"); checks++;
        Require(Rejected(() => { var value = Data(); value.absoluteDay = 0;
                _ = new CharacterFatalityFact(value); }),
            "fatality day zero was accepted"); checks++;
        Require(Rejected(() => { var value = Data(); value.cause = (CharacterDeathCauseCode)999;
                _ = new CharacterFatalityFact(value); }),
            "undefined fatality cause was accepted"); checks++;
        Require(Rejected(() => _ = new CharacterFatalityFact(Data(float.NaN, 0f, false, true))),
            "NaN fatality health was accepted"); checks++;
        Require(Rejected(() => _ = new CharacterFatalityFact(Data(-1f, 0f, false, true))),
            "negative fatality health was accepted"); checks++;
        Require(Rejected(() => { var value = Data(); value.maximumHealth = 0f;
                _ = new CharacterFatalityFact(value); }),
            "zero fatality maximum health was accepted"); checks++;
        Require(Rejected(() => _ = new CharacterFatalityFact(Data(100f, 1f, false, true))),
            "actual fatality with positive remaining health was accepted"); checks++;
        Require(Rejected(() => { var value = Data(); value.reasonCode = null;
                _ = new CharacterFatalityFact(value); }),
            "null fatality reason was accepted"); checks++;

        CharacterFatalityFact.ValidatePending(new List<CharacterFatalityPendingFactData>
        {
            Pending(fatality, "death:primary", "critical:primary", "downed:primary"),
            Pending(noCritical, "death:threshold", "", "downed:threshold"),
            Pending(alreadyDowned, "death:already", "critical:already", "")
        });
        Require(true, "valid fatality pending key shapes were rejected"); checks++;
        Require(Rejected(() => CharacterFatalityFact.ValidatePending(
                new List<CharacterFatalityPendingFactData>
                { Pending(fatality, "", "critical:required", "downed:required") })),
            "fatality pending entry without its required death key was accepted"); checks++;
        Require(Rejected(() => CharacterFatalityFact.ValidatePending(
                new List<CharacterFatalityPendingFactData>
                { Pending(fatality, "death:mismatch", "", "downed:mismatch") })),
            "fatality pending optional transition keys were inconsistent with the fact"); checks++;
        Require(Rejected(() => CharacterFatalityFact.ValidatePending(
                new List<CharacterFatalityPendingFactData>
                { Pending(fatality, "same:key", "same:key", "downed:unique") })),
            "duplicate fatality publication keys were accepted"); checks++;
        Require(Rejected(() => CharacterFatalityFact.ValidatePending(
                new List<CharacterFatalityPendingFactData>
                {
                    Pending(fatality, "death:first", "critical:first", "downed:first"),
                    Pending(fatality, "death:second", "critical:second", "downed:second")
                })),
            "multiple unresolved fatality facts for the same owner were accepted"); checks++;
        return checks;
    }

    private static CharacterFatalityFactData Data(
        float before = 100f,
        float after = 0f,
        bool downedBefore = false,
        bool downedAfter = true,
        string characterId = "character:fatality") => new()
    {
        characterId = characterId,
        characterName = "테스트",
        absoluteDay = 1,
        cause = CharacterDeathCauseCode.Combat,
        reasonCode = "combat",
        healthBefore = before,
        maximumHealth = 100f,
        healthAfter = after,
        downedBefore = downedBefore,
        downedAfter = downedAfter
    };

    private static CharacterFatalityPendingFactData Pending(
        CharacterFatalityFact fact,
        string death,
        string critical,
        string downed) => new()
    {
        deathPublicationKey = death,
        criticalPublicationKey = critical,
        downedPublicationKey = downed,
        fact = fact.Snapshot()
    };

    private static bool Rejected(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return true; }
        return false;
    }

    private static void Require(bool valid, string detail)
    { if (!valid) throw new InvalidOperationException(detail); }
}
'@)
$observationMembers.Add(@'
public static class CharacterDeathReferenceHostSmoke
{
    public static int Run()
    {
        int checks = 0;
        var character = new CharacterId("character:death-subject");
        var witness = new CharacterId("character:death-witness");
        var source = new[] { witness, character, witness, default(CharacterId) };
        const string opaque = "uninterpreted|death reference|한글";
        var death = new CharacterDeathEvent(character, CharacterDeathCauseCode.Combat, 9,
            new CoreGridCell(2, 3), source, opaque);
        Require(death.ParentPublicationKey == opaque, "opaque death reference was parsed or normalized"); checks++;
        Require(death.WitnessIds.Count == 1 && death.WitnessIds[0].Equals(witness),
            "death witnesses were not filtered/deduplicated"); checks++;
        source[0] = character;
        Require(death.WitnessIds[0].Equals(witness), "death event retained mutable caller array"); checks++;
        bool immutable = false;
        try { ((IList<CharacterId>)death.WitnessIds)[0] = character; }
        catch (NotSupportedException) { immutable = true; }
        Require(immutable && death.WitnessIds[0].Equals(witness), "death event exposed a mutable witness list"); checks++;
        var life = death.ToLifeRecord();
        Require(life.CharacterId.Equals(character) && life.AbsoluteDay == 9
            && life.Cause == CharacterDeathCauseCode.Combat && life.Location.Equals(new CoreGridCell(2, 3)),
            "neutral life record lost death facts"); checks++;
        Require(new CharacterDeathEvent(character, CharacterDeathCauseCode.Unknown, 1, default,
            null).ParentPublicationKey == "", "absent observation acquired a fabricated parent"); checks++;
        foreach (string invalid in new string[] { null, " " })
        {
            Require(Rejected(() => new CharacterDeathEvent(character, CharacterDeathCauseCode.Unknown,
                1, default, null, invalid)), "null/blank death reference accepted"); checks++;
        }
        Require(Rejected(() => new CharacterDeathEvent(default, CharacterDeathCauseCode.Combat,
            1, default, null)), "invalid death actor accepted"); checks++;
        Require(Rejected(() => new CharacterDeathEvent(character, CharacterDeathCauseCode.Combat,
            0, default, null)), "invalid death day accepted"); checks++;
        Require(Rejected(() => new CharacterDeathEvent(character, (CharacterDeathCauseCode)999,
            1, default, null)), "invalid death cause accepted"); checks++;
        var pending = new ReproductionCarrierDeathNotificationSaveData { parentPublicationKey = opaque };
        Require(pending.parentPublicationKey == opaque
            && typeof(ReproductionCarrierDeathNotificationSaveData).GetField("parentOutcomeRunId") == null
            && typeof(ReproductionCarrierDeathNotificationSaveData).GetField("parentOutcomeSequence") == null,
            "carrier notification still carries typed ledger representation"); checks++;
        return checks;
    }
    private static bool Rejected(Action action)
    { try { action(); } catch (ArgumentException) { return true; } return false; }
    private static void Require(bool valid, string detail)
    { if (!valid) throw new InvalidOperationException(detail); }
}
'@)
$observationMembers.Add(@'
public static class FacilityVandalismFactHostSmoke
{
    private const string FirstKey = "opaque vandalism key | 한글";
    private const string SecondKey = "another opaque key without parsing";

    public static int Run()
    {
        int checks = 0;
        FacilityVandalismCommitState source = State(1, FirstKey);
        var fact = new FacilityVandalismFact(source);
        source.actorName = "변경된 행위자";
        source.witnessPublicationKeys[0] = "changed witness";
        FacilityVandalismCommitState exposed = fact.Snapshot();
        exposed.facilityName = "변경된 시설";
        exposed.witnessPublicationKeys[0] = "changed exposed witness";
        FacilityVandalismCommitState frozen = fact.Snapshot();
        Require(frozen.actorName == "파괴자"
            && frozen.facilityName == "훈련장"
            && frozen.witnessPublicationKeys.SequenceEqual(
                new[] { "opaque witness a", "opaque witness b" })
            && frozen.publicationKey == string.Empty,
            "facility vandalism fact did not deep-freeze its source"); checks++;

        var pending = new FacilityVandalismPendingState();
        FacilityVandalismCommitState first = State(1, FirstKey);
        pending.Add(first);
        Require(first.IsValid(2, out string firstError)
            && firstError.Length == 0
            && pending.Contains(FirstKey)
            && !pending.Contains("opaque vandalism key"),
            "opaque publication key was parsed or normalized"); checks++;

        FacilityVandalismCommitState reversed = State(1, FirstKey);
        reversed.witnessPublicationKeys.Reverse();
        Require(ArgumentRejected(() => new FacilityVandalismFact(reversed)),
            "reverse-ordered witness references were accepted"); checks++;
        FacilityVandalismCommitState duplicatedWitness = State(1, FirstKey);
        duplicatedWitness.witnessPublicationKeys[1] =
            duplicatedWitness.witnessPublicationKeys[0];
        Require(ArgumentRejected(() => new FacilityVandalismFact(
                duplicatedWitness)),
            "duplicate witness references were accepted"); checks++;

        FacilityVandalismCommitState badDamage = State(1, FirstKey);
        badDamage.damagedBefore = true;
        Require(ArgumentRejected(() => new FacilityVandalismFact(badDamage)),
            "already-damaged facility state was accepted as a new fact"); checks++;
        FacilityVandalismCommitState changedIntegrity = State(1, FirstKey);
        changedIntegrity.structuralHitPointsAfter = 49f;
        Require(ArgumentRejected(() => new FacilityVandalismFact(
                changedIntegrity)),
            "vandalism fact changed structural hit points"); checks++;

        FacilityVandalismCommitState duplicateOwner = State(
            1, "altered opaque key");
        Require(StateRejected(() => pending.Add(duplicateOwner))
            && pending.Count == 1,
            "same facility/sequence was accepted under an altered key"); checks++;

        FacilityVandalismCommitState second = State(2, SecondKey);
        pending.Add(second);
        FacilityVandalismPendingSaveData captured = pending.Capture();
        FacilityVandalismPendingState restored =
            FacilityVandalismPendingState.Restore(captured);
        captured.pending[0].actorName = "저장본 변경";
        captured.pending[0].witnessPublicationKeys[0] = "저장 witness 변경";
        FacilityVandalismPendingSaveData recaptured = restored.Capture();
        Require(restored.Count == 2
            && restored.Contains(FirstKey)
            && restored.Contains(SecondKey)
            && recaptured.pending[0].actorName == "파괴자"
            && recaptured.pending[0].witnessPublicationKeys[0]
                == "opaque witness a"
            && recaptured.pending.Select(value => value.operationSequence)
                .SequenceEqual(new long[] { 1, 2 }),
            "two successive pending damage facts did not restore independently of a live building owner"); checks++;

        Require(StateRejected(() => restored.Remove("changed key"))
            && restored.Count == 2,
            "changed publication key removed pending vandalism"); checks++;
        restored.Remove(FirstKey);
        Require(restored.Count == 1
            && !restored.Contains(FirstKey)
            && restored.Contains(SecondKey),
            "exact publication key did not remove one pending fact"); checks++;

        var wrongVersion = pending.Capture();
        wrongVersion.version++;
        var malformed = pending.Capture();
        malformed.pending.Add(null);
        Require(StateRejected(() => FacilityVandalismPendingState.Restore(
                wrongVersion))
            && StateRejected(() => FacilityVandalismPendingState.Restore(
                malformed)),
            "malformed or unsupported pending payload restored"); checks++;
        Require(new FacilityVandalismCommitState().IsValid(1, out _)
            && !new FacilityVandalismCommitState().IsValid(2, out string emptyError)
            && !string.IsNullOrWhiteSpace(emptyError),
            "empty damage receipt hid advanced sequence"); checks++;
        return checks;
    }

    private static FacilityVandalismCommitState State(
        long sequence,
        string publicationKey) => new()
    {
        operationSequence = sequence,
        facilityId = "facility:vandalism-host",
        buildingDefinitionId = "building-definition:vandalism-host",
        actorId = "character:vandalism-host",
        actorName = "파괴자",
        facilityName = "훈련장",
        source = "macro",
        reason = "통제된 시설 파손",
        absoluteDay = 5,
        positionX = 2,
        positionY = 3,
        damagedBefore = false,
        damagedAfter = true,
        hasStructuralIntegrity = true,
        structuralHitPointsBefore = 50f,
        structuralHitPointsAfter = 50f,
        phase = (int)FacilityVandalismCommitPhase.Applied,
        publicationKey = publicationKey,
        witnessPublicationKeys = new List<string>
        {
            "opaque witness a",
            "opaque witness b"
        }
    };

    private static bool ArgumentRejected(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return true; }
        return false;
    }

    private static bool StateRejected(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return true; }
        return false;
    }

    private static void Require(bool valid, string detail)
    { if (!valid) throw new InvalidOperationException(detail); }
}
'@)
$observationMembers.Add(@'
namespace DungeonStory.Environment
{
public static class EnvironmentalFireDamageFactHostSmoke
{
    public static int Run()
    {
        int checks = 0;
        EnvironmentalFireDamageRecord prepared = Record(
            EnvironmentalFireDamagePhase.Prepared);
        prepared.publicationKey = "pre-reserved opaque key | 한글";
        var preparedFact = new EnvironmentalFireDamageFact(prepared);
        prepared.targetDisplayName = "변경된 대상";
        prepared.requestedDamage = 999f;
        EnvironmentalFireDamageRecord exposedPrepared = preparedFact.Snapshot();
        exposedPrepared.targetDisplayName = "노출 변경";
        EnvironmentalFireDamageRecord frozenPrepared = preparedFact.Snapshot();
        Require(frozenPrepared.phase == EnvironmentalFireDamagePhase.Prepared
            && frozenPrepared.appliedDamage == 0f
            && frozenPrepared.targetDisplayName == "화재 대상"
            && frozenPrepared.requestedDamage == 10f
            && frozenPrepared.publicationKey == string.Empty,
            "prepared fire proposal did not freeze payload or clear its reserved key"); checks++;

        EnvironmentalFireDamageRecord delivered = Record(
            EnvironmentalFireDamagePhase.Delivered);
        Bind(delivered);
        var appliedFact = new EnvironmentalFireDamageFact(delivered);
        delivered.appliedDamage = 1f;
        EnvironmentalFireDamageRecord applied = appliedFact.Snapshot();
        Require(applied.phase == EnvironmentalFireDamagePhase.Applied
            && applied.appliedDamage == 8f
            && applied.publicationKey == string.Empty
            && applied.publicationId == string.Empty
            && applied.publicationFingerprint == string.Empty,
            "delivered fire damage was not frozen as an applied domain fact"); checks++;

        EnvironmentalFireDamageRecord opaque = Record(
            EnvironmentalFireDamagePhase.Delivered);
        opaque.publicationKey = "opaque key | 한글";
        opaque.publicationId = "unparsed observation id / 한글";
        opaque.publicationFingerprint = "not-a-sha fingerprint | 한글";
        Require(Accepted(() => EnvironmentalFireDamageFact.ValidateRecord(
                opaque)),
            "opaque delivery proof was parsed instead of preserved"); checks++;

        EnvironmentalFireDamageRecord awaiting = Record(
            EnvironmentalFireDamagePhase.AwaitingWorldRemoval);
        BindNonDelivered(awaiting);
        Require(Accepted(() => EnvironmentalFireDamageFact.ValidateRecord(
                awaiting))
            && new EnvironmentalFireDamageFact(awaiting).Snapshot().phase
                == EnvironmentalFireDamagePhase.AwaitingWorldRemoval,
            "valid building removal boundary was rejected"); checks++;
        EnvironmentalFireDamageRecord awaitingCharacter = awaiting.Clone();
        awaitingCharacter.targetKind = (int)EnvironmentalFireTargetKind.Character;
        RefreshIdentity(awaitingCharacter);
        EnvironmentalFireDamageRecord awaitingCombustible = awaiting.Clone();
        awaitingCombustible.targetRemainsCombustible = true;
        RefreshIdentity(awaitingCombustible);
        Require(Rejected(() => EnvironmentalFireDamageFact.ValidateRecord(
                awaitingCharacter))
            && Rejected(() => EnvironmentalFireDamageFact.ValidateRecord(
                awaitingCombustible)),
            "world-removal phase accepted a character or combustible building"); checks++;

        EnvironmentalFireDamageRecord itemTarget = Record(
            EnvironmentalFireDamagePhase.Applied);
        itemTarget.targetKind = (int)EnvironmentalFireTargetKind.ItemStack;
        RefreshIdentity(itemTarget);
        BindNonDelivered(itemTarget);
        Require(Rejected(() => EnvironmentalFireDamageFact.ValidateRecord(
                itemTarget)),
            "unsupported item-stack damage target was accepted"); checks++;

        EnvironmentalFireDamageRecord zeroRequest = Record(
            EnvironmentalFireDamagePhase.Applied);
        zeroRequest.requestedDamage = 0f;
        RefreshIdentity(zeroRequest);
        BindNonDelivered(zeroRequest);
        EnvironmentalFireDamageRecord excessApplied = Record(
            EnvironmentalFireDamagePhase.Applied);
        excessApplied.appliedDamage = 21f;
        BindNonDelivered(excessApplied);
        Require(Rejected(() => EnvironmentalFireDamageFact.ValidateRecord(
                zeroRequest))
            && Rejected(() => EnvironmentalFireDamageFact.ValidateRecord(
                excessApplied)),
            "zero requested or excessive applied fire damage was accepted"); checks++;

        EnvironmentalFireDamageRecord nanIntensity = Record(
            EnvironmentalFireDamagePhase.Applied);
        nanIntensity.intensity = float.NaN;
        RefreshIdentity(nanIntensity);
        BindNonDelivered(nanIntensity);
        EnvironmentalFireDamageRecord infiniteDamage = Record(
            EnvironmentalFireDamagePhase.Applied);
        infiniteDamage.appliedDamage = float.PositiveInfinity;
        BindNonDelivered(infiniteDamage);
        Require(Rejected(() => EnvironmentalFireDamageFact.ValidateRecord(
                nanIntensity))
            && Rejected(() => EnvironmentalFireDamageFact.ValidateRecord(
                infiniteDamage)),
            "non-finite fire damage input was accepted"); checks++;

        EnvironmentalFireDamageRecord preparedWithDamage = Record(
            EnvironmentalFireDamagePhase.Prepared);
        preparedWithDamage.appliedDamage = 1f;
        EnvironmentalFireDamageRecord appliedWithoutDamage = Record(
            EnvironmentalFireDamagePhase.Applied);
        appliedWithoutDamage.appliedDamage = 0f;
        BindNonDelivered(appliedWithoutDamage);
        Require(Rejected(() => new EnvironmentalFireDamageFact(
                preparedWithDamage))
            && Rejected(() => EnvironmentalFireDamageFact.ValidateRecord(
                appliedWithoutDamage)),
            "phase-specific zero/positive damage contract was weakened"); checks++;

        EnvironmentalFireDamageRecord invalidPhase = Record(
            EnvironmentalFireDamagePhase.Applied);
        invalidPhase.phase = (EnvironmentalFireDamagePhase)99;
        BindNonDelivered(invalidPhase);
        Require(Rejected(() => EnvironmentalFireDamageFact.ValidateRecord(
                invalidPhase)),
            "undefined fire damage phase was accepted"); checks++;

        EnvironmentalFireDamageRecord missingKey = Record(
            EnvironmentalFireDamagePhase.Applied);
        EnvironmentalFireDamageRecord missingDeliveredProof = Record(
            EnvironmentalFireDamagePhase.Delivered);
        missingDeliveredProof.publicationKey = "opaque delivered key";
        EnvironmentalFireDamageRecord strayAppliedProof = Record(
            EnvironmentalFireDamagePhase.Applied);
        strayAppliedProof.publicationKey = "opaque applied key";
        strayAppliedProof.publicationId = "unexpected id";
        Require(Rejected(() => EnvironmentalFireDamageFact.ValidateRecord(
                missingKey))
            && Rejected(() => EnvironmentalFireDamageFact.ValidateRecord(
                missingDeliveredProof))
            && Rejected(() => EnvironmentalFireDamageFact.ValidateRecord(
                strayAppliedProof)),
            "required or phase-specific publication proof was not enforced"); checks++;

        EnvironmentalFireDamageRecord identity = Record(
            EnvironmentalFireDamagePhase.Applied);
        string expectedRequest = RequestFingerprint(identity);
        long expectedOwner = OwnerRevision(identity.operationId);
        Require(identity.requestFingerprint == expectedRequest
            && identity.requestFingerprint.Length == 64
            && identity.requestFingerprint.All(value =>
                value >= '0' && value <= '9' || value >= 'a' && value <= 'f')
            && identity.ownerRevision == expectedOwner
            && identity.ownerRevision > 0
            && EnvironmentalFireDamageFactIdentity.OwnerRevision(
                identity.operationId) == expectedOwner,
            "fire request hash or owner revision diverged from canonical framing"); checks++;

        EnvironmentalFireDamageRecord tamperedHash = identity.Clone();
        tamperedHash.requestFingerprint = new string('0', 64);
        BindNonDelivered(tamperedHash);
        EnvironmentalFireDamageRecord tamperedOwner = identity.Clone();
        tamperedOwner.ownerRevision++;
        BindNonDelivered(tamperedOwner);
        Require(Rejected(() => EnvironmentalFireDamageFact.ValidateRecord(
                tamperedHash))
            && Rejected(() => EnvironmentalFireDamageFact.ValidateRecord(
                tamperedOwner)),
            "changed request hash or owner revision was accepted"); checks++;
        return checks;
    }

    private static EnvironmentalFireDamageRecord Record(
        EnvironmentalFireDamagePhase phase)
    {
        var value = new EnvironmentalFireDamageRecord
        {
            operationId = "environmental-fire-damage:host",
            fireId = "environmental-fire:host",
            targetKind = (int)EnvironmentalFireTargetKind.Building,
            targetId = "building:fire-host",
            targetDisplayName = "화재 대상",
            positionX = 4,
            positionY = 7,
            intensity = 0.75f,
            requestedDamage = 10f,
            appliedDamage = phase == EnvironmentalFireDamagePhase.Prepared
                ? 0f
                : 8f,
            targetRemainsCombustible = phase
                != EnvironmentalFireDamagePhase.AwaitingWorldRemoval,
            absoluteDay = 6,
            phase = phase
        };
        RefreshIdentity(value);
        return value;
    }

    private static void Bind(EnvironmentalFireDamageRecord value)
    {
        value.publicationKey = "opaque fire publication key";
        value.publicationId = "opaque fire publication id";
        value.publicationFingerprint = "opaque fire publication fingerprint";
    }

    private static void BindNonDelivered(EnvironmentalFireDamageRecord value)
    {
        value.publicationKey = "opaque fire publication key";
        value.publicationId = string.Empty;
        value.publicationFingerprint = string.Empty;
    }

    private static void RefreshIdentity(EnvironmentalFireDamageRecord value)
    {
        value.requestFingerprint =
            EnvironmentalFireDamageFactIdentity.RequestFingerprint(value);
        value.ownerRevision =
            EnvironmentalFireDamageFactIdentity.OwnerRevision(value.operationId);
    }

    private static string RequestFingerprint(
        EnvironmentalFireDamageRecord value)
    {
        var digest = new CanonicalSemanticDigestBuilder();
        digest.Append("environmental-fire-damage-request@1");
        digest.Append(value.operationId);
        digest.Append(value.fireId);
        digest.Append(value.targetKind);
        digest.Append(value.targetId);
        digest.Append(value.positionX);
        digest.Append(value.positionY);
        digest.Append(value.intensity.ToString(
            "R", System.Globalization.CultureInfo.InvariantCulture));
        digest.Append(value.requestedDamage.ToString(
            "R", System.Globalization.CultureInfo.InvariantCulture));
        digest.Append(value.targetDisplayName?.Trim() ?? string.Empty);
        digest.Append(value.absoluteDay);
        return digest.ComputeSha256();
    }

    private static long OwnerRevision(string operationId)
    {
        var digest = new CanonicalSemanticDigestBuilder();
        digest.Append("environmental-fire-damage-owner-revision@1");
        digest.Append(operationId?.Trim() ?? string.Empty);
        return Math.Max(1L, long.Parse(
            digest.ComputeSha256().Substring(0, 15),
            System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture));
    }

    private static bool Accepted(Action action)
    {
        try { action(); return true; }
        catch (InvalidOperationException) { return false; }
    }

    private static bool Rejected(Action action) => !Accepted(action);

    private static void Require(bool valid, string detail)
    { if (!valid) throw new InvalidOperationException(detail); }
}
}
'@)
$observationMembers.Add(@'
namespace DungeonStory.Environment
{
public static class EnvironmentalFireActionFactHostSmoke
{
    public static int Run()
    {
        int checks = 0;
        EnvironmentalFireIgnitionRequest request = IgnitionRequest();
        var ignitionResult = new EnvironmentalFireIgnitionResult(
            EnvironmentalFireIgnitionDisposition.Ignited,
            "environmental-fire:action-host",
            "The target ignited.");
        EnvironmentalFireIgnitedFact ignited = EnvironmentalFireIgnitedFact.Create(
            request, ignitionResult, new UnityEngine.Vector2Int(3, 5), 8, 4, 0.6f);
        EnvironmentalFireCauseSaveRecord ignition = ignited.Snapshot();
        Require(ignition.requestedIntensity == 0.85f
            && ignition.appliedIntensity == 0.6f
            && ignited.ToRequest().IgnitionIntensity == 0.85f
            && ignition.publicationKey == string.Empty
            && !ignition.outcomePending,
            "ignition fact did not retain original requested intensity beside the applied clamp"); checks++;

        ignition.requestedIntensity = 0.1f;
        ignition.targetDisplayName = "변경";
        Require(ignited.Snapshot().requestedIntensity == 0.85f
            && ignited.Snapshot().targetDisplayName == "가공실",
            "ignition snapshot shared mutable state"); checks++;

        EnvironmentalFireCauseSaveRecord changedFingerprint = ignited.Snapshot();
        changedFingerprint.fingerprint = "changed-fingerprint";
        EnvironmentalFireCauseSaveRecord changedIdentity = ignited.Snapshot();
        changedIdentity.producerId = "producer:changed-fire-host";
        Require(Rejected(() => new EnvironmentalFireIgnitedFact(changedFingerprint))
            && Rejected(() => new EnvironmentalFireIgnitedFact(changedIdentity)),
            "changed ignition request fingerprint or identity was accepted"); checks++;

        Require(Rejected(() => EnvironmentalFireIgnitedFact.Create(
                request, ignitionResult, new UnityEngine.Vector2Int(3, 5), 8, 4, 0.9f))
            && Rejected(() => EnvironmentalFireIgnitedFact.Create(
                request, ignitionResult, new UnityEngine.Vector2Int(3, 5), 8, 4, -0.1f))
            && Rejected(() => EnvironmentalFireIgnitedFact.Create(
                request, ignitionResult, new UnityEngine.Vector2Int(3, 5), 8, 4, float.NaN)),
            "invalid applied ignition intensity was accepted"); checks++;

        EnvironmentalFireIgnitedFact missing = EnvironmentalFireIgnitedFact.Create(
            request,
            new EnvironmentalFireIgnitionResult(
                EnvironmentalFireIgnitionDisposition.TargetMissing,
                string.Empty,
                "The target is missing."),
            new UnityEngine.Vector2Int(0, 0), 8, 5, 0f, false);
        Require(missing.Snapshot().fireId == string.Empty
            && missing.Snapshot().appliedIntensity == 0f
            && !missing.Snapshot().hasLocation,
            "no-fire rejection did not preserve its exact result shape"); checks++;

        EnvironmentalFireIgnitedFact alreadyBurning = EnvironmentalFireIgnitedFact.Create(
            request,
            new EnvironmentalFireIgnitionResult(
                EnvironmentalFireIgnitionDisposition.AlreadyBurning,
                "environmental-fire:existing-host",
                "The target is already burning."),
            new UnityEngine.Vector2Int(3, 5), 8, 6, 0f);
        Require(alreadyBurning.Snapshot().fireId == "environmental-fire:existing-host"
            && alreadyBurning.Snapshot().appliedIntensity == 0f,
            "already-burning result lost its existing fire identity"); checks++;

        EnvironmentalFireCauseSaveRecord missingWithFire = missing.Snapshot();
        missingWithFire.fireId = "environmental-fire:unexpected";
        EnvironmentalFireCauseSaveRecord burningWithoutFire = alreadyBurning.Snapshot();
        burningWithoutFire.fireId = string.Empty;
        Require(Rejected(() => new EnvironmentalFireIgnitedFact(missingWithFire))
            && Rejected(() => new EnvironmentalFireIgnitedFact(burningWithoutFire)),
            "ignition rejection/fire identity relationship was not enforced"); checks++;

        EnvironmentalFireSuppressionCommand waterCommand = SuppressionCommand(
            EnvironmentalFireSuppressionMode.Water);
        var suppressionResult = new EnvironmentalFireSuppressionResult(
            EnvironmentalFireSuppressionDisposition.AppliedAwaitingWaterAcknowledgement,
            0.7f, 0f, 2, "physical-disposition:water-host",
            "Water suppression applied.");
        EnvironmentalFireSuppressedFact suppressed = EnvironmentalFireSuppressedFact.Create(
            waterCommand, suppressionResult, "building:fire-action-host",
            new UnityEngine.Vector2Int(3, 5), 8, 7);
        EnvironmentalFireSuppressionSaveRecord suppression = suppressed.Snapshot();
        Require(suppression.intensityBefore == 0.7f
            && suppression.intensityAfter == 0f
            && suppression.resultDisposition == (int)EnvironmentalFireSuppressionDisposition.Extinguished
            && suppression.waterQuantity == 2
            && suppression.waterCommittedQuantity == 2
            && suppressed.ToCommand().WaterQuantity == 2,
            "suppression fact did not freeze actual intensity, canonical disposition, or water quantity"); checks++;

        suppression.intensityBefore = 0.1f;
        suppression.waterQuantity = 99;
        suppression.resultReason = "변경";
        Require(suppressed.Snapshot().intensityBefore == 0.7f
            && suppressed.Snapshot().waterQuantity == 2
            && suppressed.Snapshot().resultReason == "The fire was extinguished.",
            "suppression snapshot shared mutable state"); checks++;

        EnvironmentalFireSuppressionSaveRecord badIntensity = suppressed.Snapshot();
        badIntensity.intensityAfter = 0.8f;
        EnvironmentalFireSuppressionSaveRecord badWater = suppressed.Snapshot();
        badWater.waterCommittedQuantity = 1;
        Require(Rejected(() => new EnvironmentalFireSuppressedFact(badIntensity))
            && Rejected(() => new EnvironmentalFireSuppressedFact(badWater)),
            "invalid suppression intensity or physical water quantity was accepted"); checks++;

        EnvironmentalFireSuppressionSaveRecord delivered = suppressed.Snapshot();
        delivered.publicationKey = "opaque child proof / no parser";
        var completion = new EnvironmentalFireSuppressionCompletedFact(delivered);
        EnvironmentalFireSuppressionSaveRecord completionCopy = completion.Snapshot();
        completionCopy.publicationKey = "changed";
        Require(completion.Snapshot().publicationKey == "opaque child proof / no parser"
            && completion.Snapshot().completionPublicationKey == string.Empty
            && !completion.Snapshot().completionIdentityAcknowledged,
            "completion fact parsed its opaque key or exposed mutable state"); checks++;

        EnvironmentalFireSuppressionSaveRecord pending = delivered.Clone();
        pending.outcomePending = true;
        EnvironmentalFireSuppressionSaveRecord missingKey = delivered.Clone();
        missingKey.publicationKey = string.Empty;
        EnvironmentalFireSuppressedFact nonterminal = EnvironmentalFireSuppressedFact.Create(
            SuppressionCommand(EnvironmentalFireSuppressionMode.InitialAttack),
            new EnvironmentalFireSuppressionResult(
                EnvironmentalFireSuppressionDisposition.Applied,
                0.7f, 0.4f, 0, string.Empty, "Initial attack applied."),
            "building:fire-action-host", new UnityEngine.Vector2Int(3, 5), 8, 8);
        EnvironmentalFireSuppressionSaveRecord nonterminalChild = nonterminal.Snapshot();
        nonterminalChild.publicationKey = "opaque nonterminal child";
        Require(Rejected(() => new EnvironmentalFireSuppressionCompletedFact(pending))
            && Rejected(() => new EnvironmentalFireSuppressionCompletedFact(missingKey))
            && Rejected(() => new EnvironmentalFireSuppressionCompletedFact(nonterminalChild)),
            "completion accepted a pending, unproven, or nonterminal child"); checks++;
        return checks;
    }

    private static EnvironmentalFireIgnitionRequest IgnitionRequest() => new(
        "environmental-fire-cause:action-host",
        EnvironmentalFireIgnitionKind.ElectricalFault,
        "producer:fire-action-host",
        new EnvironmentalFireTargetRef(
            EnvironmentalFireTargetKind.Building,
            "building:fire-action-host"),
        0.85f,
        "evidence:fire-action-host",
        null,
        "가공실");

    private static EnvironmentalFireSuppressionCommand SuppressionCommand(
        EnvironmentalFireSuppressionMode mode) => new(
        "environmental-fire-suppression:action-host",
        "environmental-fire:action-host",
        "character:fire-worker-host",
        new UnityEngine.Vector2Int(4, 6),
        mode,
        2.5f,
        mode == EnvironmentalFireSuppressionMode.Water ? "water-lease:action-host" : string.Empty,
        mode == EnvironmentalFireSuppressionMode.Water ? 2 : 0,
        "소방 작업자",
        CharacterCommandOrigin.DirectPlayerOrder);

    private static bool Rejected(Action action)
    {
        try { action(); return false; }
        catch (ArgumentException) { return true; }
    }

    private static void Require(bool valid, string detail)
    { if (!valid) throw new InvalidOperationException(detail); }
}
}
'@)
$observationMembers.Add(@'
public static class EnvironmentalFireRuntimeHostSmoke
{
    public static int Run()
    {
        int checks = 0;
        string phase = "initial-attack";
        try
        {

        FireRuntimeHostFixture once = new();
        once.Facts.Enqueue(BoundaryCommit.Delivered);
        EnvironmentalFireIgnitionResult smallFire = once.Runtime.TryIgnite(
            Ignition("environmental-fire-cause:initial-host", 0.25f));
        once.Facts.Enqueue(BoundaryCommit.Delivered);
        EnvironmentalFireSuppressionCommand initial = InitialAttack(
            "environmental-fire-suppression:initial-host",
            smallFire.FireId,
            13f);
        EnvironmentalFireSuppressionResult first =
            once.Runtime.TryApplySuppression(initial);
        DungeonStory.Environment.DungeonEnvironmentalFireSaveData firstSave =
            once.Runtime.Capture();
        EnvironmentalFireSuppressionResult repeated =
            once.Runtime.TryApplySuppression(initial);
        DungeonStory.Environment.DungeonEnvironmentalFireSaveData repeatedSave =
            once.Runtime.Capture();
        Require(first.Disposition
                == DungeonStory.Environment.EnvironmentalFireSuppressionDisposition.Extinguished
            && first.IntensityBefore == 0.25f
            && first.IntensityAfter == 0f
            && firstSave.history.Count == 1
            && firstSave.history[0].fire.totalSuppressionWork == 13f,
            "13-work initial attack did not extinguish the 0.25 fire exactly once"); checks++;
        Require(repeated.Disposition
                == DungeonStory.Environment.EnvironmentalFireSuppressionDisposition.PreviouslyApplied
            && repeatedSave.history.Count == 1
            && repeatedSave.history[0].fire.totalSuppressionWork == 13f
            && repeatedSave.suppressionOperations.Count == 1
            && once.Facts.SuppressionStages == 1,
            "same suppression command reapplied work or staged a duplicate fact"); checks++;

        phase = "known-rejected-ignition";
        FireRuntimeHostFixture rejectedIgnition = new();
        rejectedIgnition.Facts.Enqueue(BoundaryCommit.Rejected);
        Require(Throws(() => rejectedIgnition.Runtime.TryIgnite(
                Ignition("environmental-fire-cause:rejected-host", 0.25f))),
            "known-rejected ignition did not fail"); checks++;
        DungeonStory.Environment.DungeonEnvironmentalFireSaveData rejectedIgnitionSave =
            rejectedIgnition.Runtime.Capture();
        Require(rejectedIgnitionSave.activeFires.Count == 0
            && rejectedIgnitionSave.processedCauses.Count == 0
            && rejectedIgnitionSave.nextFireSequence == 1
            && rejectedIgnitionSave.nextOutcomeSequence == 1,
            "known-rejected ignition retained reversible fire state"); checks++;

        phase = "known-rejected-suppression";
        FireRuntimeHostFixture rejectedSuppression = new();
        rejectedSuppression.Facts.Enqueue(BoundaryCommit.Delivered);
        EnvironmentalFireIgnitionResult rejectionOwner =
            rejectedSuppression.Runtime.TryIgnite(
                Ignition("environmental-fire-cause:suppression-reject-host", 0.25f));
        rejectedSuppression.Facts.Enqueue(BoundaryCommit.Rejected);
        Require(Throws(() => rejectedSuppression.Runtime.TryApplySuppression(
                InitialAttack(
                    "environmental-fire-suppression:rejected-host",
                    rejectionOwner.FireId,
                    5f))),
            "known-rejected initial attack did not fail"); checks++;
        DungeonStory.Environment.DungeonEnvironmentalFireSaveData rejectedSuppressionSave =
            rejectedSuppression.Runtime.Capture();
        Require(rejectedSuppressionSave.activeFires.Count == 1
            && rejectedSuppressionSave.activeFires[0].intensity == 0.25f
            && rejectedSuppressionSave.activeFires[0].totalSuppressionWork == 0f
            && rejectedSuppressionSave.suppressionOperations.Count == 0,
            "known-rejected initial attack did not restore the original fire"); checks++;

        phase = "clamped-ignition";
        FireRuntimeHostFixture clamped = new();
        clamped.Facts.Enqueue(BoundaryCommit.Delivered);
        clamped.Runtime.TryIgnite(
            Ignition("environmental-fire-cause:clamped-host", 0.8f));
        DungeonStory.Environment.DungeonEnvironmentalFireSaveData clampedSave =
            clamped.Runtime.Capture();
        Require(clamped.Facts.LastIgnition.requestedIntensity == 0.8f
            && clamped.Facts.LastIgnition.appliedIntensity == 0.6f
            && clampedSave.processedCauses[0].requestedIntensity == 0.8f
            && clampedSave.processedCauses[0].appliedIntensity == 0.6f
            && clampedSave.activeFires[0].intensity == 0.6f,
            "staged or saved ignition lost original requested intensity beside the clamp"); checks++;

        phase = "uncertain-before";
        checks += VerifyUncertain(BoundaryCommit.ThrowBefore, 0);
        phase = "uncertain-after";
        checks += VerifyUncertain(BoundaryCommit.ThrowAfter, 1);
        return checks;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "fire-runtime-host-phase:" + phase + ":" + exception,
                exception);
        }
    }

    private static int VerifyUncertain(
        BoundaryCommit behavior,
        int expectedSimulatedObserverCommits)
    {
        int checks = 0;
        FireRuntimeHostWaterSink water = new();
        FireRuntimeHostFixture owner = new(water);
        owner.Facts.Enqueue(BoundaryCommit.Delivered);
        DungeonStory.Environment.EnvironmentalFireIgnitionResult ignition =
            owner.Runtime.TryIgnite(Ignition(
                "environmental-fire-cause:uncertain-" + behavior,
                0.8f));
        owner.Facts.Enqueue(behavior);
        DungeonStory.Environment.EnvironmentalFireSuppressionCommand command =
            WaterSuppression(
                "environmental-fire-suppression:uncertain-" + behavior,
                ignition.FireId);
        Require(Throws(() => owner.Runtime.TryApplySuppression(command)),
            "uncertain suppression did not report its pending publication");
        DungeonStory.Environment.DungeonEnvironmentalFireSaveData durable =
            owner.Runtime.Capture();
        Require(durable.activeFires.Count == 0
            && durable.history.Count == 1
            && durable.history[0].fire.totalWaterConsumed == 2
            && durable.suppressionOperations.Count == 1
            && durable.suppressionOperations[0].phase
                == (int)DungeonStory.Environment.EnvironmentalFireSuppressionPhase.Applied
            && durable.suppressionOperations[0].outcomePending
            && water.CommitCalls == 1
            && water.AcknowledgeCalls == 0
            && owner.Facts.LastStage.SimulatedObserverCommits
                == expectedSimulatedObserverCommits,
            "uncertain suppression did not retain the exact applied pending journal"); checks++;

        FireRuntimeHostFixture restored = new(water);
        DungeonStory.Environment.EnvironmentalFireRestoreCandidate candidate =
            restored.Runtime.PrepareRestore(durable);
        durable.history[0].fire.totalWaterConsumed = 99;
        durable.processedCauses[0].targetDisplayName = "변경";
        durable.suppressionOperations[0].waterQuantity = 99;
        restored.Runtime.Restore(candidate);
        DungeonStory.Environment.DungeonEnvironmentalFireSaveData beforeRecovery =
            restored.Runtime.Capture();
        Require(beforeRecovery.history[0].fire.totalWaterConsumed == 2
            && beforeRecovery.processedCauses[0].targetDisplayName == "가공실"
            && beforeRecovery.suppressionOperations[0].waterQuantity == 2,
            "restore candidate shared mutable capture state"); checks++;

        int dayBefore = restored.Calendar.Day;
        IReadOnlyList<DungeonStory.Environment.EnvironmentalFireSuppressionResult>
            recovered = restored.Runtime.ResumePendingSuppressions();
        DungeonStory.Environment.DungeonEnvironmentalFireSaveData afterRecovery =
            restored.Runtime.Capture();
        Require(recovered.Count == 1
            && recovered[0].Disposition
                == DungeonStory.Environment.EnvironmentalFireSuppressionDisposition.PreviouslyApplied
            && restored.Recovery.ReplayCalls == 1
            && water.CommitCalls == 1
            && water.AcknowledgeCalls == 1
            && restored.Calendar.Day == dayBefore
            && restored.Facts.IgnitionStages == 0
            && afterRecovery.activeFires.Count == 0
            && afterRecovery.history.Count == 1
            && afterRecovery.processedCauses.Count == 1
            && afterRecovery.history[0].fire.totalWaterConsumed == 2
            && !afterRecovery.suppressionOperations[0].outcomePending
            && afterRecovery.suppressionOperations[0].waterAcknowledged,
            "fresh recovery repeated water/fire/day effects or failed to clear publication pending"); checks++;
        return checks;
    }

    private static DungeonStory.Environment.EnvironmentalFireIgnitionRequest
        Ignition(string causeId, float intensity) => new(
            causeId,
            DungeonStory.Environment.EnvironmentalFireIgnitionKind.ActiveHeatSource,
            "producer:fire-runtime-host",
            FireRuntimeHostTargetQuery.Target,
            intensity,
            "evidence:fire-runtime-host",
            null,
            "가공실");

    private static DungeonStory.Environment.EnvironmentalFireSuppressionCommand
        InitialAttack(string operationId, string fireId, float work) => new(
            operationId,
            fireId,
            "character:fire-runtime-worker",
            new UnityEngine.Vector2Int(1, 2),
            DungeonStory.Environment.EnvironmentalFireSuppressionMode.InitialAttack,
            work,
            workerDisplayName: "소방 작업자",
            commandOrigin: CharacterCommandOrigin.DirectPlayerOrder);

    private static DungeonStory.Environment.EnvironmentalFireSuppressionCommand
        WaterSuppression(string operationId, string fireId) => new(
            operationId,
            fireId,
            "character:fire-runtime-worker",
            new UnityEngine.Vector2Int(1, 2),
            DungeonStory.Environment.EnvironmentalFireSuppressionMode.Water,
            4f,
            "water-lease:fire-runtime-host",
            2,
            "소방 작업자",
            CharacterCommandOrigin.DirectPlayerOrder);

    private static bool Throws(Action action)
    {
        try { action(); return false; }
        catch (InvalidOperationException) { return true; }
    }

    private static void Require(bool valid, string detail)
    { if (!valid) throw new InvalidOperationException(detail); }
}

/// <summary>
/// Pure-host fixture over the exact runtime. Its collaborators expose only
/// controlled target/access/water/random/calendar/publication boundaries.
/// </summary>
public sealed class FireRuntimeHostFixture
{
    public FireRuntimeHostFixture(FireRuntimeHostWaterSink water = null)
    {
        Water = water ?? new FireRuntimeHostWaterSink();
        Facts = new FireRuntimeHostFactBoundary();
        Recovery = new BoundaryRecoveryPublisher();
        Calendar = new FireRuntimeHostCalendar();
        Runtime = new EnvironmentalFireRuntime(
            new FireRuntimeHostTargetQuery(),
            new FireRuntimeHostDamageCommand(),
            new FireRuntimeHostAccessQuery(),
            new FireRuntimeHostElectricalQuery(),
            Water,
            new EnvironmentalFireRuntimeSettings(5f, 0.35f, 0.02f),
            new FireRuntimeHostRandomProvider(),
            Facts,
            Recovery,
            new FireRuntimeHostCompletionPublisher(),
            Calendar);
    }

    public EnvironmentalFireRuntime Runtime { get; }
    public FireRuntimeHostFactBoundary Facts { get; }
    public BoundaryRecoveryPublisher Recovery { get; }
    public FireRuntimeHostWaterSink Water { get; }
    public FireRuntimeHostCalendar Calendar { get; }
}

/// <summary>Observer boundary only; it does not execute a native observer or ledger.</summary>
public sealed class FireRuntimeHostFactBoundary : IDomainFactStagingPublisher
{
    private readonly Queue<BoundaryCommit> behaviors = new();
    public int IgnitionStages { get; private set; }
    public int SuppressionStages { get; private set; }
    public DungeonStory.Environment.EnvironmentalFireCauseSaveRecord LastIgnition { get; private set; }
    public DungeonStory.Environment.EnvironmentalFireSuppressionSaveRecord LastSuppression { get; private set; }
    public BoundaryFactStage LastStage { get; private set; }
    public void Enqueue(BoundaryCommit value) => behaviors.Enqueue(value);

    public bool TryStage<TFact>(in TFact fact, out IDomainFactStage stage, out string failure)
    {
        long revision;
        if (fact is DungeonStory.Environment.EnvironmentalFireIgnitedFact ignition)
        {
            LastIgnition = ignition.Snapshot();
            revision = LastIgnition.outcomeRevision;
            IgnitionStages++;
        }
        else if (fact is DungeonStory.Environment.EnvironmentalFireSuppressedFact suppression)
        {
            LastSuppression = suppression.Snapshot();
            revision = LastSuppression.outcomeRevision;
            SuppressionStages++;
        }
        else
        {
            stage = null;
            failure = "fire-runtime-host-unsupported-fact";
            return false;
        }

        BoundaryCommit behavior = behaviors.Count == 0
            ? BoundaryCommit.Delivered
            : behaviors.Dequeue();
        LastStage = new BoundaryFactStage(
            behavior,
            "opaque fire runtime publication " + revision,
            revision);
        stage = LastStage;
        failure = string.Empty;
        return true;
    }
}

/// <summary>Physical-water boundary double; no inventory debit is executed.</summary>
public sealed class FireRuntimeHostWaterSink : IEnvironmentalFireWaterSink
{
    private readonly Dictionary<string, DungeonStory.Environment.EnvironmentalFireWaterReceipt> pending = new();
    public int CommitCalls { get; private set; }
    public int AcknowledgeCalls { get; private set; }
    public bool TryCommitReservedWaterPending(
        string leaseId,
        int quantity,
        string operationId,
        string workerId,
        string fireId,
        UnityEngine.Vector2Int standPosition,
        UnityEngine.Vector2Int targetPosition,
        out DungeonStory.Environment.EnvironmentalFireWaterReceipt receipt,
        out string failureReason)
    {
        CommitCalls++;
        receipt = new DungeonStory.Environment.EnvironmentalFireWaterReceipt(
            operationId,
            leaseId,
            quantity,
            "water-commit:" + operationId);
        pending[operationId] = receipt;
        failureReason = string.Empty;
        return true;
    }
    public bool TryGetPending(
        string operationId,
        string leaseId,
        out DungeonStory.Environment.EnvironmentalFireWaterReceipt receipt) =>
        pending.TryGetValue(operationId, out receipt)
        && receipt.LeaseId == leaseId;
    public bool TryAcknowledge(string commitId, out string failureReason)
    {
        AcknowledgeCalls++;
        failureReason = string.Empty;
        return true;
    }
}

public sealed class FireRuntimeHostTargetQuery : IEnvironmentalFireTargetQuery
{
    public static readonly DungeonStory.Environment.EnvironmentalFireTargetRef Target = new(
        DungeonStory.Environment.EnvironmentalFireTargetKind.Building,
        "building:fire-runtime-host");
    private static readonly DungeonStory.Environment.EnvironmentalFireProfile Profile = new(
        10f, 0.6f, 0.05f, 0.1f, 1f, 0.5f, 0f, 0.5f, 0.4f);
    private static readonly DungeonStory.Environment.EnvironmentalFireTargetSnapshot Snapshot = new(
        Target,
        new UnityEngine.Vector2Int(2, 2),
        true,
        true,
        false,
        Profile,
        DungeonStory.Environment.EnvironmentalFireIgnitionSources.All,
        "가공실");
    public bool TryGetTarget(
        DungeonStory.Environment.EnvironmentalFireTargetRef target,
        out DungeonStory.Environment.EnvironmentalFireTargetSnapshot snapshot)
    {
        snapshot = Snapshot;
        return target.Equals(Target);
    }
    public IReadOnlyList<DungeonStory.Environment.EnvironmentalFireTargetSnapshot>
        GetAdjacentTargets(DungeonStory.Environment.EnvironmentalFireTargetRef target) =>
        Array.Empty<DungeonStory.Environment.EnvironmentalFireTargetSnapshot>();
}

public sealed class FireRuntimeHostAccessQuery : IEnvironmentalFireSuppressionAccessQuery
{
    public bool CanSuppress(
        string workerId,
        UnityEngine.Vector2Int standPosition,
        DungeonStory.Environment.EnvironmentalFireTargetRef target,
        out string failureReason)
    { failureReason = string.Empty; return true; }
}

public sealed class FireRuntimeHostElectricalQuery : IEnvironmentalFireElectricalSafetyQuery
{
    public bool TryGetSafety(
        DungeonStory.Environment.EnvironmentalFireTargetRef target,
        out DungeonStory.Environment.EnvironmentalFireElectricalSafetySnapshot snapshot)
    {
        snapshot = new DungeonStory.Environment.EnvironmentalFireElectricalSafetySnapshot(
            target, false, false, true, false, false);
        return true;
    }
}

public sealed class FireRuntimeHostDamageCommand : IEnvironmentalFireDamageCommand
{
    public bool TryApply(
        DungeonStory.Environment.EnvironmentalFireDamageCommand command,
        out DungeonStory.Environment.EnvironmentalFireDamageResult result)
    {
        result = new DungeonStory.Environment.EnvironmentalFireDamageResult(
            false, 0f, true, "fire-runtime-host-no-tick");
        return false;
    }
}

public sealed class FireRuntimeHostCalendar : IGameCalendar
{
    public int Day { get; private set; } = 1;
    public int Hour => 0;
    public int Year => 1;
    public int DayOfYear => 1;
    public Season Season => Season.Spring;
    public int DayOfSeason => 1;
    public long AbsoluteHour => 0;
    public float ElapsedSeconds => 0f;
    public TimeOfDay TimeOfDay => TimeOfDay.Morning;
    public bool IsRunning => true;
    public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);
    public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
        GameCalendarRules.ProjectRegional(Day, Hour, utcOffsetHours);
    public void Start() { }
    public void SetDateTime(int day, int hour) => Day = day;
}

public sealed class FireRuntimeHostRandomProvider : DungeonStory.Foundation.IRandomStreamProvider
{
    private readonly DungeonStory.Foundation.IRandomStream stream = new FireRuntimeHostRandomStream();
    public int RootSeed => 1;
    public DungeonStory.Foundation.IRandomStream Get(string streamId) => stream;
    public void Reseed(int rootSeed) { }
    public IReadOnlyList<DungeonStory.Foundation.RandomStreamStateSnapshot> CaptureStates() =>
        Array.Empty<DungeonStory.Foundation.RandomStreamStateSnapshot>();
    public DungeonStory.Foundation.RandomStreamRestoreCandidate BuildRestoreStates(
        int rootSeed,
        IEnumerable<DungeonStory.Foundation.RandomStreamStateSnapshot> snapshots) =>
        new(new DungeonStory.Foundation.RandomStreamAggregateState());
    public void RestoreStates(DungeonStory.Foundation.RandomStreamRestoreCandidate candidate) { }
    public void RestoreStates(
        int rootSeed,
        IEnumerable<DungeonStory.Foundation.RandomStreamStateSnapshot> snapshots) { }
}

public sealed class FireRuntimeHostRandomStream : DungeonStory.Foundation.IRandomStream
{
    public ulong State => 1UL;
    public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    public float NextFloat() => 0f;
    public bool Chance(float probability) => false;
    public void Restore(ulong state) { }
}

/// <summary>Completion publication boundary; no observer or ledger is executed.</summary>
public sealed class FireRuntimeHostCompletionPublisher : IDomainFactPublisher
{
    public bool TryReserve<TFact>(
        in TFact proposed,
        out DomainFactLease lease,
        out string failure)
    {
        lease = default;
        failure = "fire-runtime-host-completion-not-executed";
        return false;
    }
    public DomainFactDelivery Publish<TFact>(
        in DomainFactLease lease,
        in TFact actual) => DomainFactDelivery.Reject("not-executed");
    public bool PublishBatch(
        IReadOnlyList<DomainFactEmission> facts,
        DomainFactDelivery[] results,
        out string failure)
    { failure = "not-executed"; return false; }
    public DomainFactDelivery Replay<TFact>(in TFact fact) =>
        DomainFactDelivery.Reject("not-executed");
    public void Cancel(in DomainFactLease lease) { }
}
'@)
$observationMembers.Add(@'
public static class EnvironmentalFireResourceFactHostSmoke
{
    public static int Run()
    {
        int checks = 0;
        EnvironmentalFireResourceRecord fuel = Fuel();
        EnvironmentalFireResourceRecord water = Water();
        var fuelFact = new EnvironmentalFireFuelLostFact(fuel);
        var waterFact = new EnvironmentalFireWaterConsumedFact(water);
        Require(fuelFact.Physical.Receipt.Kind == PhysicalItemDispositionKind.Sink
            && fuelFact.Physical.Receipt.SourceFacts.Count == 1
            && fuelFact.Physical.Receipt.SourceFacts[0].StackId
                == fuel.targetId
            && waterFact.Physical.Receipt.SourceFacts.Count == 2
            && waterFact.Physical.Receipt.Quantity == 2
            && waterFact.Physical.Receipt.InputMassGrams == 1000,
            "valid fuel and water facts did not preserve exact physical debits"); checks++;

        fuel.targetId = "stack:mutated-fuel";
        water.workerName = "변경된 작업자";
        water.physical.sourceStackIds[0] = "stack:mutated-water";
        water.physical.sourceFacts[0].displayText = "변경된 물";
        EnvironmentalFireResourceRecord exposed = waterFact.Snapshot();
        exposed.workerName = "노출 변경";
        exposed.physical.sourceStackIds[0] = "stack:exposed";
        exposed.physical.sourceFacts[0].displayText = "노출된 물";
        EnvironmentalFireResourceRecord frozenFuel = fuelFact.Snapshot();
        EnvironmentalFireResourceRecord frozenWater = waterFact.Snapshot();
        Require(frozenFuel.targetId == "stack:fire-fuel"
            && frozenFuel.publicationKey == string.Empty
            && frozenWater.workerName == "소방 작업자"
            && frozenWater.physical.sourceStackIds[0]
                == "stack:fire-water-a"
            && frozenWater.physical.sourceFacts[0].displayText == "깨끗한 물"
            && waterFact.Physical.Receipt.SourceFacts[0].DisplayName
                == "깨끗한 물",
            "fire resource fact or nested physical DTO shared mutable aliases"); checks++;

        EnvironmentalFireResourceRecord badQuantity = Fuel();
        badQuantity.physical.quantity++;
        RefreshCommitId(badQuantity.physical);
        EnvironmentalFireResourceRecord badMass = Fuel();
        badMass.physical.inputMassGrams++;
        RefreshCommitId(badMass.physical);
        Require(Rejected(() => new EnvironmentalFireFuelLostFact(badQuantity))
            && Rejected(() => new EnvironmentalFireFuelLostFact(badMass)),
            "physical quantity or mass mismatch was accepted"); checks++;

        EnvironmentalFireResourceRecord badOrder = Water();
        badOrder.physical.sourceStackIds.Reverse();
        EnvironmentalFireResourceRecord badRequest = Water();
        badRequest.physical.requestFingerprint = "mismatched request";
        Require(Rejected(() => new EnvironmentalFireWaterConsumedFact(
                badOrder))
            && Rejected(() => new EnvironmentalFireWaterConsumedFact(
                badRequest)),
            "physical source ordering or request mismatch was accepted"); checks++;

        EnvironmentalFireResourceRecord wrongFuelTarget = Fuel();
        wrongFuelTarget.targetId = "stack:other-fuel";
        Require(Rejected(() => new EnvironmentalFireFuelLostFact(
                wrongFuelTarget)),
            "fuel fact accepted a target other than its exact source stack"); checks++;

        EnvironmentalFireResourceRecord wrongWaterItem = Water();
        wrongWaterItem.physical.sourceFacts[0].itemDefinitionId =
            "resource:unclean-water";
        Require(Rejected(() => new EnvironmentalFireWaterConsumedFact(
                wrongWaterItem)),
            "water fact accepted a non-clean-water source"); checks++;

        EnvironmentalFireResourceRecord mixedWaterName = Water();
        mixedWaterName.physical.sourceFacts[1].displayText = "다른 물";
        Require(Rejected(() => new EnvironmentalFireWaterConsumedFact(
                mixedWaterName)),
            "water fact accepted mixed frozen display names"); checks++;

        EnvironmentalFireResourceRecord missingWorker = Water();
        missingWorker.workerId = string.Empty;
        EnvironmentalFireResourceRecord invalidLease = Water();
        invalidLease.leaseId = "invalid lease";
        Require(Rejected(() => new EnvironmentalFireWaterConsumedFact(
                missingWorker))
            && Rejected(() => new EnvironmentalFireWaterConsumedFact(
                invalidLease)),
            "water fact accepted a missing worker or invalid lease"); checks++;

        EnvironmentalFireResourceRecord expectedPublication = Fuel();
        expectedPublication.physical.publicationExpected = true;
        EnvironmentalFireResourceRecord malformedProof = Fuel();
        malformedProof.physical.publicationKey = "unexpected physical key";
        malformedProof.physical.publicationId = null;
        Require(Rejected(() => new EnvironmentalFireFuelLostFact(
                expectedPublication))
            && Rejected(() => new EnvironmentalFireFuelLostFact(
                malformedProof)),
            "nested physical publication proof was accepted"); checks++;

        EnvironmentalFireResourceRecord wrongDay = Fuel();
        wrongDay.physical.defaultObservationDay++;
        EnvironmentalFireResourceRecord wrongPosition = Fuel();
        wrongPosition.physical.sourceFacts[0].sourceX++;
        Require(Rejected(() => new EnvironmentalFireFuelLostFact(wrongDay))
            && Rejected(() => new EnvironmentalFireFuelLostFact(
                wrongPosition)),
            "fire context accepted a changed physical day or position"); checks++;
        return checks;
    }

    private static EnvironmentalFireResourceRecord Fuel()
    {
        const int day = 8;
        const int x = 3;
        const int y = 5;
        PhysicalItemBatchDispositionSaveData physical = Physical(
            "environmental-fire-fuel:host",
            EnvironmentalFireWorldAdapter.FuelLossSinkReasonCode,
            day,
            new PhysicalItemDispositionSourceFactSaveData
            {
                stackId = "stack:fire-fuel",
                itemDefinitionId = "resource:fire-fuel",
                quantity = 3,
                massGrams = 3000,
                sourceX = x,
                sourceY = y,
                displayText = "연료"
            });
        return new EnvironmentalFireResourceRecord
        {
            use = EnvironmentalFireResourceUse.FuelLoss,
            targetId = "stack:fire-fuel",
            positionX = x,
            positionY = y,
            absoluteDay = day,
            physical = physical,
            publicationKey = "opaque fire resource reservation"
        };
    }

    private static EnvironmentalFireResourceRecord Water()
    {
        const int day = 9;
        const int x = 6;
        const int y = 7;
        PhysicalItemBatchDispositionSaveData physical = Physical(
            "environmental-fire-water:host",
            EnvironmentalFireWorldAdapter.WaterSinkReasonCode,
            day,
            new PhysicalItemDispositionSourceFactSaveData
            {
                stackId = "stack:fire-water-a",
                itemDefinitionId = EnvironmentalFireWorldAdapter.CleanWaterItemId,
                itemInstanceId = "item-instance:fire-water-a",
                quantity = 1,
                massGrams = 500,
                sourceX = x,
                sourceY = y,
                displayText = "깨끗한 물"
            },
            new PhysicalItemDispositionSourceFactSaveData
            {
                stackId = "stack:fire-water-b",
                itemDefinitionId = EnvironmentalFireWorldAdapter.CleanWaterItemId,
                itemInstanceId = "item-instance:fire-water-b",
                quantity = 1,
                massGrams = 500,
                sourceX = x,
                sourceY = y,
                displayText = "깨끗한 물"
            });
        return new EnvironmentalFireResourceRecord
        {
            use = EnvironmentalFireResourceUse.SuppressionWater,
            fireId = "environmental-fire:water-host",
            leaseId = "item-lease:fire-water-host",
            workerId = "character:fire-worker",
            workerName = "소방 작업자",
            positionX = x,
            positionY = y,
            absoluteDay = day,
            physical = physical,
            publicationKey = "opaque water resource reservation"
        };
    }

    private static PhysicalItemBatchDispositionSaveData Physical(
        string operationId,
        string reasonCode,
        int day,
        params PhysicalItemDispositionSourceFactSaveData[] sources)
    {
        var sourceFacts = sources.ToList();
        var physical = new PhysicalItemBatchDispositionSaveData
        {
            kind = (int)PhysicalItemDispositionKind.Sink,
            operationId = operationId,
            reasonCode = reasonCode,
            sourceStackIds = sourceFacts.Select(value => value.stackId).ToList(),
            quantity = sourceFacts.Sum(value => value.quantity),
            inputMassGrams = sourceFacts.Sum(value => value.massGrams),
            ownerRevision = 1,
            defaultObservationDay = day,
            sourceFacts = sourceFacts
        };
        physical.requestFingerprint = physical.kind + ":" + reasonCode + ":"
            + string.Join(",", sourceFacts.Select(value =>
                value.stackId + "=" + value.quantity));
        RefreshCommitId(physical);
        return physical;
    }

    private static void RefreshCommitId(
        PhysicalItemBatchDispositionSaveData physical) =>
        physical.commitId = "physical-batch-disposition:"
            + physical.kind + ":" + physical.operationId + ":"
            + physical.quantity + ":" + physical.inputMassGrams;

    private static bool Rejected(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return true; }
        return false;
    }

    private static void Require(bool valid, string detail)
    { if (!valid) throw new InvalidOperationException(detail); }
}
'@)
$observationMembers.Add(@'
public static class EnvironmentalFireResourceTransactionHostSmoke
{
    private const string OperationKey =
        "opaque fire resource observation key | 한글";

    public static int Run()
    {
        int checks = 0;
        EnvironmentalFireResourceTransaction cancelled = Staged(
            BoundaryCommit.Cancelled,
            out IDomainFactStage cancelledStage,
            out BoundaryFactPublisher cancelledFacts,
            out _);
        cancelledStage.Cancel();
        Require(cancelled.Capture().Count == 0
            && cancelledFacts.LastStage.CancelCalls == 1,
            "cancelled resource stage retained a journal"); checks++;

        EnvironmentalFireResourceTransaction rejected = Staged(
            BoundaryCommit.Rejected,
            out IDomainFactStage rejectedStage,
            out _,
            out _);
        DomainFactDelivery rejection = rejectedStage.Commit();
        Require(rejection.Phase == DomainFactDeliveryPhase.Rejected
            && rejected.Capture().Count == 0,
            "known observer rejection retained a resource journal"); checks++;

        EnvironmentalFireResourceTransaction beforeThrow = Staged(
            BoundaryCommit.ThrowBefore,
            out IDomainFactStage beforeThrowStage,
            out BoundaryFactPublisher beforeThrowFacts,
            out _);
        Require(Throws(() => beforeThrowStage.Commit())
            && ExactJournal(beforeThrow.Capture())
            && beforeThrowFacts.LastStage.SimulatedObserverCommits == 0,
            "pre-observer exception lost or changed the frozen journal"); checks++;

        EnvironmentalFireResourceTransaction afterThrow = Staged(
            BoundaryCommit.ThrowAfter,
            out IDomainFactStage afterThrowStage,
            out BoundaryFactPublisher afterThrowFacts,
            out _);
        Require(Throws(() => afterThrowStage.Commit())
            && ExactJournal(afterThrow.Capture())
            && afterThrowFacts.LastStage.SimulatedObserverCommits == 1,
            "post-observer exception lost or changed the frozen journal"); checks++;

        IReadOnlyList<EnvironmentalFireResourceRecord> isolated =
            afterThrow.Capture();
        isolated[0].targetId = "stack:changed";
        isolated[0].physical.quantity = 99;
        isolated[0].physical.sourceFacts[0].displayText = "변경";
        Require(ExactJournal(afterThrow.Capture()),
            "resource transaction capture exposed mutable journal state"); checks++;

        IReadOnlyList<EnvironmentalFireResourceRecord> durable =
            beforeThrow.Capture();
        var recovery = new BoundaryRecoveryPublisher();
        var physical = new BoundaryPhysicalDispositionService();
        var fresh = new EnvironmentalFireResourceTransaction(
            new BoundaryFactPublisher(BoundaryCommit.Delivered),
            recovery,
            physical);
        fresh.Restore(durable);
        Require(fresh.TryAcknowledge(
                Receipt().CommitId,
                out string acknowledgeFailure)
            && acknowledgeFailure.Length == 0
            && recovery.ReplayCalls == 1
            && physical.AcknowledgeCalls == 1
            && fresh.Capture().Count == 0,
            "fresh owner did not replay proof and acknowledge physical debit once"); checks++;

        var duplicateOwner = new EnvironmentalFireResourceTransaction(
            new BoundaryFactPublisher(BoundaryCommit.Delivered),
            new BoundaryRecoveryPublisher(),
            new BoundaryPhysicalDispositionService());
        duplicateOwner.Restore(durable);
        var duplicated = new List<EnvironmentalFireResourceRecord>
        {
            durable[0].Clone(),
            durable[0].Clone()
        };
        Require(Throws(() => duplicateOwner.Restore(duplicated))
            && ExactJournal(duplicateOwner.Capture()),
            "duplicate restore replaced the prior resource journal"); checks++;

        var rollbackOwner = new EnvironmentalFireResourceTransaction(
            new BoundaryFactPublisher(BoundaryCommit.Delivered),
            new BoundaryRecoveryPublisher(),
            new BoundaryPhysicalDispositionService());
        rollbackOwner.Restore(durable);
        rollbackOwner.BeginRestoreCandidate();
        rollbackOwner.Restore(Array.Empty<EnvironmentalFireResourceRecord>());
        rollbackOwner.PublishRestoreCandidate();
        bool candidatePublished = rollbackOwner.Capture().Count == 0;
        rollbackOwner.RollbackPublishedRestoreCandidate();
        Require(candidatePublished && ExactJournal(rollbackOwner.Capture()),
            "restore publication rollback did not preserve the old journal"); checks++;

        var invalidRequestOwner = new EnvironmentalFireResourceTransaction(
            new BoundaryFactPublisher(BoundaryCommit.Delivered),
            new BoundaryRecoveryPublisher(),
            new BoundaryPhysicalDispositionService());
        Require(ThrowsArgument(() => invalidRequestOwner.PrepareFuel(
                null,
                new UnityEngine.Vector2Int(2, 4),
                6)),
            "invalid fuel request reached staging"); checks++;
        return checks;
    }

    private static EnvironmentalFireResourceTransaction Staged(
        BoundaryCommit behavior,
        out IDomainFactStage stage,
        out BoundaryFactPublisher facts,
        out BoundaryPhysicalDispositionService physical)
    {
        facts = new BoundaryFactPublisher(behavior);
        physical = new BoundaryPhysicalDispositionService();
        var owner = new EnvironmentalFireResourceTransaction(
            facts,
            new BoundaryRecoveryPublisher(),
            physical);
        var request = new EnvironmentalFireFuelLossRequest(
            new EnvironmentalFireTargetRef(
                EnvironmentalFireTargetKind.ItemStack,
                "stack:fire-owner-fuel"),
            2);
        IPhysicalItemDispositionEffect effect = owner.PrepareFuel(
            request,
            new UnityEngine.Vector2Int(2, 4),
            6);
        PhysicalItemBatchDispositionReceipt receipt = Receipt();
        Require(effect.TryStage(
                receipt,
                3,
                out stage,
                out string failure)
            && failure.Length == 0
            && stage != null
            && stage.OperationKey == OperationKey,
            "boundary fixture could not stage the exact fuel fact");
        return owner;
    }

    private static PhysicalItemBatchDispositionReceipt Receipt()
    {
        var sources = new[]
        {
            new PhysicalItemDispositionSourceFact(
                "stack:fire-owner-fuel",
                "resource:fire-fuel",
                string.Empty,
                2,
                200,
                new UnityEngine.Vector2Int(2, 4),
                "연료")
        };
        string reason = EnvironmentalFireWorldAdapter.FuelLossSinkReasonCode;
        return new PhysicalItemBatchDispositionReceipt(
            PhysicalItemDispositionKind.Sink,
            "environmental-fire-fuel:owner-host",
            reason,
            "3:" + reason + ":stack:fire-owner-fuel=2",
            new[] { "stack:fire-owner-fuel" },
            2,
            200,
            3,
            sources);
    }

    private static bool ExactJournal(
        IReadOnlyList<EnvironmentalFireResourceRecord> values) =>
        values.Count == 1
        && values[0].use == EnvironmentalFireResourceUse.FuelLoss
        && values[0].targetId == "stack:fire-owner-fuel"
        && values[0].positionX == 2
        && values[0].positionY == 4
        && values[0].absoluteDay == 6
        && values[0].publicationKey == OperationKey
        && values[0].physical.operationId
            == "environmental-fire-fuel:owner-host"
        && values[0].physical.quantity == 2
        && values[0].physical.inputMassGrams == 200
        && values[0].physical.ownerRevision == 3
        && values[0].physical.sourceFacts.Count == 1
        && values[0].physical.sourceFacts[0].displayText == "연료";

    private static bool Throws(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return true; }
        return false;
    }

    private static bool ThrowsArgument(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return true; }
        return false;
    }

    private static void Require(bool valid, string detail)
    { if (!valid) throw new InvalidOperationException(detail); }
}

public enum BoundaryCommit
{
    Cancelled,
    Rejected,
    Delivered,
    ThrowBefore,
    ThrowAfter
}

/// <summary>Pure-host boundary double; it does not execute a gameplay observer.</summary>
public sealed class BoundaryFactPublisher : IDomainFactStagingPublisher
{
    private readonly BoundaryCommit behavior;
    public BoundaryFactPublisher(BoundaryCommit behavior) =>
        this.behavior = behavior;
    public BoundaryFactStage LastStage { get; private set; }
    public bool TryStage<TFact>(
        in TFact fact,
        out IDomainFactStage stage,
        out string failure)
    {
        if (fact is not EnvironmentalFireFuelLostFact)
        {
            stage = null;
            failure = "boundary-fact-type";
            return false;
        }
        LastStage = new BoundaryFactStage(
            behavior,
            EnvironmentalFireResourceTransactionHostSmokeOperation.Key,
            3);
        stage = LastStage;
        failure = string.Empty;
        return true;
    }
}

internal static class EnvironmentalFireResourceTransactionHostSmokeOperation
{
    internal const string Key =
        "opaque fire resource observation key | 한글";
}

/// <summary>Observer-boundary double with explicit before/after commit faults.</summary>
public sealed class BoundaryFactStage : IDomainFactStage
{
    private readonly BoundaryCommit behavior;
    private bool active = true;
    public BoundaryFactStage(
        BoundaryCommit behavior,
        string operationKey,
        long ownerRevision)
    {
        this.behavior = behavior;
        OperationKey = operationKey;
        OwnerRevision = ownerRevision;
    }
    public string OperationKey { get; }
    public long OwnerRevision { get; }
    public bool IsReplay => false;
    public bool IsActive => active;
    public int CancelCalls { get; private set; }
    public int SimulatedObserverCommits { get; private set; }
    public DomainFactDelivery Commit()
    {
        if (!active)
            return DomainFactDelivery.Reject("boundary-stage-closed");
        active = false;
        if (behavior == BoundaryCommit.ThrowBefore)
            throw new InvalidOperationException("boundary-before-observer-commit");
        if (behavior == BoundaryCommit.ThrowAfter)
        {
            SimulatedObserverCommits++;
            throw new InvalidOperationException("boundary-after-observer-commit");
        }
        if (behavior == BoundaryCommit.Rejected)
            return DomainFactDelivery.Reject("boundary-known-rejection");
        SimulatedObserverCommits++;
        return new DomainFactDelivery(
            DomainFactDeliveryPhase.Delivered,
            OperationKey,
            "boundary publication id",
            "boundary publication fingerprint",
            string.Empty);
    }
    public void Cancel()
    {
        if (!active) return;
        active = false;
        CancelCalls++;
    }
}

/// <summary>Replay proof double; it does not execute the real observer or ledger.</summary>
public sealed class BoundaryRecoveryPublisher : IDomainFactRecoveryPublisher
{
    public int ReplayCalls { get; private set; }
    public bool ReplayBatch(
        IReadOnlyList<DomainFactReplay> facts,
        DomainFactDelivery[] results,
        out string failure)
    {
        ReplayCalls++;
        failure = string.Empty;
        if (facts == null || facts.Count != 1
            || results == null || results.Length != 1)
            return false;
        results[0] = new DomainFactDelivery(
            DomainFactDeliveryPhase.Delivered,
            facts[0].PublicationKey,
            "replayed publication id",
            "replayed publication fingerprint",
            string.Empty);
        return true;
    }
}

/// <summary>Physical-owner boundary double; no inventory debit is performed.</summary>
public sealed class BoundaryPhysicalDispositionService :
    IPhysicalItemBatchDispositionService
{
    public int AcknowledgeCalls { get; private set; }
    public bool TryCommit(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = "boundary-no-debit";
        return false;
    }
    public bool TryCommitPending(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason) => TryCommit(
            inputs,
            kind,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);
    public bool Acknowledge(string commitId, out string failureReason)
    {
        AcknowledgeCalls++;
        failureReason = string.Empty;
        return commitId == EnvironmentalFireResourceTransactionHostSmokeCommit.Id;
    }
    public bool TryGetPending(
        string operationId,
        out PhysicalItemBatchDispositionReceipt receipt)
    {
        receipt = default;
        return false;
    }
}

internal static class EnvironmentalFireResourceTransactionHostSmokeCommit
{
    internal const string Id =
        "physical-batch-disposition:3:environmental-fire-fuel:owner-host:2:200";
}
'@)
$observationMembers.Add(@'
public static class ReproductionCoreHostSmoke
{
    private const string PlannedKey = "opaque reproduction planned key";
    private const string StartKey = "opaque reproduction start key";
    private const string BirthKey = "opaque reproduction birth key";

    public static int Run()
    {
        int checks = 0;
        ReproductionDefinition definition = Definition();
        ReproductionProcess process = Process(definition);

        process.MarkPlannedTransition();
        ReproductionTransitionFactData plannedData = Transition(
            null, process, "planned", day: 1);
        process.StageTransition(
            Fact(plannedData), PlannedKey);
        plannedData.facilityName = "변경된 시설";
        plannedData.people[0].name = "변경된 인물";
        ReproductionPendingTransitionData exposedPending =
            process.PendingTransition;
        exposedPending.fact.facilityName = "노출 변경";
        exposedPending.fact.people[0].name = "노출 인물 변경";
        ReproductionPendingTransitionData frozenPending =
            process.PendingTransition;
        Require(frozenPending.publicationKey == PlannedKey
            && frozenPending.fact.facilityName == "번식 연구실"
            && frozenPending.fact.people[0].name != "변경된 인물"
            && frozenPending.fact.people[0].name != "노출 인물 변경",
            "planned transition did not deep-freeze pending display names"); checks++;

        Require(StateRejected(() => process.AdvanceDay(
                new ReproductionDailyContext(2, 100, 100, 20), 0)),
            "a pending transition did not block reproduction progress"); checks++;
        Require(StateRejected(() => process.AcknowledgeTransition(
                "changed planned key", "planned id", "planned fingerprint"))
            && process.PendingTransition != null,
            "a changed planned ACK key cleared pending state"); checks++;
        process.AcknowledgeTransition(
            PlannedKey, "planned observation id", "planned fingerprint");
        Require(process.PendingTransition == null
            && process.LastPublicationKey == PlannedKey,
            "planned transition did not ACK through the aggregate"); checks++;

        ReproductionProcess badOwner = Process(definition);
        badOwner.MarkPlannedTransition();
        ReproductionTransitionFactData wrongOwner = Transition(
            null, badOwner, "planned", day: 1);
        wrongOwner.facilityId = "facility:wrong-owner";
        Require(StateRejected(() => badOwner.StageTransition(
                Fact(wrongOwner), "opaque wrong owner")),
            "a transition fact for another state owner was staged"); checks++;

        ReproductionProcessSaveData startBefore = process.Capture();
        process.PrepareStartTransaction(
            "reproduction-start:host", 2, useFertilityTreatment: true,
            new[]
            {
                new ReproductionStartCostSaveData
                {
                    stackId = "stack:fertility-host", itemId = "item:fertility-host",
                    quantity = 2, quantityBefore = 5, quantityAfter = 3
                }
            });
        process.MarkStartMaterialsConsumed();
        process.ApplyStartTransition();
        process.StageTransition(
            Fact(Transition(startBefore, process, "started", day: 2)),
            StartKey);
        ReproductionProcessSaveData startPendingSave = process.Capture();
        Require(process.Status == ReproductionProcessStatus.Active
            && process.PendingStart.phase
                == ReproductionStartTransactionPhase.MaterialsConsumed
            && startPendingSave.pendingTransitions.Count == 1,
            "start did not freeze an active MaterialsConsumed transaction"); checks++;

        ReproductionProcess restoredStart = ReproductionProcess.Restore(
            startPendingSave, definition);
        startPendingSave.pendingTransitions[0].fact.facilityName = "저장본 변경";
        startPendingSave.pendingStart.costs[0].quantityAfter = 99;
        ReproductionProcessSaveData restoredStartCapture = restoredStart.Capture();
        Require(restoredStart.Status == ReproductionProcessStatus.Active
            && restoredStartCapture.pendingTransitions[0].fact.facilityName
                == "번식 연구실"
            && restoredStartCapture.pendingStart.costs[0].quantityAfter == 3,
            "start restore retained mutable save references"); checks++;

        ReproductionProcessSaveData badProof = process.Capture();
        badProof.pendingStart.publicationId = "unexpected proof";
        Require(StateRejected(() => ReproductionProcess.Restore(
                badProof, definition)),
            "a pre-commit start transaction accepted partial publication proof"); checks++;
        ReproductionProcessSaveData badState = process.Capture();
        badState.status = ReproductionProcessStatus.Planned;
        Require(StateRejected(() => ReproductionProcess.Restore(
                badState, definition)),
            "an active pending start transaction restored into planned state"); checks++;

        restoredStart.AcknowledgeTransition(
            StartKey, "start observation id", "start fingerprint");
        Require(restoredStart.PendingTransition == null
            && restoredStart.PendingStart == null
            && restoredStart.Status == ReproductionProcessStatus.Active
            && restoredStart.LastPublicationKey == StartKey,
            "start ACK did not clear its transaction exactly once"); checks++;
        Require(StateRejected(() => restoredStart.AcknowledgeTransition(
                StartKey, "start observation id", "start fingerprint")),
            "a completed start transaction accepted a second ACK"); checks++;

        restoredStart.AdvanceDay(
            new ReproductionDailyContext(3, 100, 100, 20), 0);
        Require(restoredStart.Status == ReproductionProcessStatus.Completed,
            "single-phase reproduction fixture did not complete"); checks++;
        CharacterId child = new("character:reproduction-child");
        restoredStart.PrepareBirthTransaction(child, 4);
        ReproductionBirthTransactionPhase[] phases =
        {
            ReproductionBirthTransactionPhase.LifeRegistered,
            ReproductionBirthTransactionPhase.NarrativeRegistered,
            ReproductionBirthTransactionPhase.KinshipRegistered,
            ReproductionBirthTransactionPhase.ActorPublished,
            ReproductionBirthTransactionPhase.PopulationRegistered,
            ReproductionBirthTransactionPhase.HousingResolutionRecorded
        };
        ReproductionBirthTransactionPhase current =
            ReproductionBirthTransactionPhase.Prepared;
        foreach (ReproductionBirthTransactionPhase next in phases)
        {
            restoredStart.AdvanceBirthTransaction(current, next);
            current = next;
        }
        ReproductionProcessSaveData birthBefore = restoredStart.Capture();
        restoredStart.PrepareBirthPublicationMarker();
        restoredStart.StageTransition(
            Fact(Transition(
                birthBefore, restoredStart, "birth-published", 4, child)),
            BirthKey);
        ReproductionProcess restoredBirth = ReproductionProcess.Restore(
            restoredStart.Capture(), definition);
        Require(restoredBirth.PendingBirth.phase
                == ReproductionBirthTransactionPhase.PublicationPrepared
            && restoredBirth.PendingTransition.fact.transitionKind
                == "birth-published",
            "birth PublicationPrepared state did not survive round-trip"); checks++;
        restoredBirth.AcknowledgeTransition(
            BirthKey, "birth observation id", "birth fingerprint");
        Require(restoredBirth.PendingBirth == null
            && restoredBirth.PendingTransition == null
            && restoredBirth.ResultPublished
            && restoredBirth.ResultCharacterId.Equals(child)
            && restoredBirth.LastPublicationKey == BirthKey,
            "birth ACK did not publish the exact child once"); checks++;
        Require(StateRejected(() => restoredBirth.AcknowledgeTransition(
                BirthKey, "birth observation id", "birth fingerprint"))
            && ReproductionCommandIdentity.Hash("reproduction:host")
                == NarrativeInferenceHash.ComputeSha256Utf8("reproduction:host"),
            "birth accepted a second ACK or command hashing was unstable"); checks++;
        return checks;
    }

    private static ReproductionDefinition Definition() => new(
        new CharacterSpeciesId("species:reproduction-host"),
        ReproductionMode.Pregnancy,
        1f,
        -100f,
        100f,
        new[]
        {
            new ReproductionPhaseDefinition
            {
                phase = ReproductionPhaseKind.Delivery,
                durationDays = 1
            }
        });

    private static ReproductionProcess Process(
        ReproductionDefinition definition) => new(
        "reproduction:host",
        new CharacterId("character:reproduction-parent-a"),
        new CharacterId("character:reproduction-parent-b"),
        new CharacterId("character:reproduction-carrier"),
        new CharacterSpeciesId("species:reproduction-host"),
        definition,
        startedAbsoluteDay: 1,
        crossLineageIncubatorUsed: false,
        supportFacilityInstanceId: "facility:reproduction-host",
        expressedTraitIds: new[] { "trait:host" },
        latentTraitIds: Array.Empty<string>(),
        aptitudes: Array.Empty<InnateAptitudeSaveData>(),
        startActive: false);

    private static ReproductionTransitionFactData Transition(
        ReproductionProcessSaveData before,
        ReproductionProcess after,
        string kind,
        int day,
        CharacterId child = default)
    {
        var data = new ReproductionTransitionFactData
        {
            processId = after.ProcessId,
            transitionKind = kind,
            mode = after.Mode,
            phenotypeSpeciesId = after.PhenotypeSpeciesId.Value,
            hasBefore = before != null,
            statusBefore = before?.status ?? default,
            statusAfter = after.Status,
            failureBefore = before?.failure ?? default,
            failureAfter = after.Failure,
            cause = kind,
            facilityId = after.SupportFacilityInstanceId,
            facilityName = "번식 연구실",
            costs = "none",
            absoluteDay = day,
            revisionBefore = before?.transitionRevision
                ?? checked(after.TransitionRevision - 1),
            revisionAfter = after.TransitionRevision,
            phaseBefore = before?.currentPhaseIndex ?? 0,
            phaseAfter = after.CurrentPhaseIndex,
            phaseElapsedBefore = before?.currentPhaseElapsedDays ?? 0,
            phaseElapsedAfter = after.CurrentPhaseElapsedDays,
            progressBefore = before?.totalProgressDays ?? 0,
            progressAfter = after.TotalProgressDays,
            previousPublicationKey = before?.lastPublicationKey
                ?? string.Empty,
            locationInstanceId = after.SupportFacilityInstanceId,
            locationRegionId = "region:reproduction-host"
        };
        Add(after.FirstParentId, "첫 부모", "genetic-parent");
        Add(after.SecondParentId, "둘째 부모", "genetic-parent");
        Add(after.CarrierId, "운반자", "reproduction-carrier");
        if (child.IsValid)
            Add(child, "아이", "offspring");
        return data;

        void Add(CharacterId id, string name, string role) =>
            data.people.Add(new ReproductionTransitionPersonData
            {
                characterId = id.Value,
                name = name,
                role = role
            });
    }

    private static bool StateRejected(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return true; }
        catch (ArgumentException) { return true; }
        return false;
    }

    private static ReproductionTransitionFact Fact(
        ReproductionTransitionFactData data)
    {
        try { return new ReproductionTransitionFact(data); }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "reproduction fact fixture rejected: " + data?.transitionKind
                    + "; people=" + (data?.people?.Count ?? -1)
                    + "; roles=" + string.Join(",", data?.people
                        ?.Select(value => value?.role ?? "<null>")
                        ?? Array.Empty<string>()),
                exception);
        }
    }

    private static void Require(bool valid, string detail)
    { if (!valid) throw new InvalidOperationException(detail); }
}
'@)
$observationMembers.Add(@'
public static class PostCommitObservationHostSmoke
{
    public static int Run()
    {
        int calls = 0;
        using var facts = new DungeonStory.Foundation.Facts.DomainFactDispatcher();
        var observer = new GameplayPostCommitFactObserver<int>(value => "notification:" + value, value => calls += value);
        using var binding = facts.Subscribe("host:post-commit", observer);
        if (!facts.TryReserve(3, out var lease, out var failure) || !facts.Publish(lease, 3).Completed || calls != 3)
            throw new InvalidOperationException("Post-commit callback did not execute once: " + failure);
        if (facts.Publish(lease, 3).Completed || calls != 3)
            throw new InvalidOperationException("Post-commit lease was reused.");
        if (facts.Replay(3).Completed || calls != 3)
            throw new InvalidOperationException("Transient notification was replayed.");
        binding.Dispose();
        if (facts.TryReserve(3, out _, out _) || calls != 3)
            throw new InvalidOperationException("Disposed observer remained connected.");
        return 4;
    }
}
'@)
$observationSource = ($observationUsings -join [Environment]::NewLine) + [Environment]::NewLine +
    ($observationMembers -join [Environment]::NewLine)
Add-Type -TypeDefinition $observationSource -CompilerOptions '/langversion:9'
$dispatcher = [DomainFactDispatcherSmokeScenarios]::Run()
[GameplayDomainFactPairProofDebugScenarios]::Run()
$postCommit = [PostCommitObservationHostSmoke]::Run()
$waterFact = [WaterFactHostSmoke]::Run()
$knowledgeDto = [KnowledgeDtoHostSmoke]::Run()
$certifiedSeedDto = [CertifiedSeedDtoHostSmoke]::Run()
$productionOutputPublicationDto = [ProductionOutputPublicationDtoHostSmoke]::Run()
$medicalDtoClone = [CharacterMedicalDtoHostSmoke]::Run()
$healingFact = [CharacterHealingFactHostSmoke]::Run()
$serviceTerminalFact = [ServiceSessionTerminalFactHostSmoke]::Run()
$serviceModeAggregate = [ServiceModeAggregateHostSmoke]::Run()
$serviceModeFact = [ServiceModeFactHostSmoke]::Run()
$fatalityFact = [CharacterFatalityFactHostSmoke]::Run()
$deathReference = [CharacterDeathReferenceHostSmoke]::Run()
$facilityVandalismFact = [FacilityVandalismFactHostSmoke]::Run()
$environmentalFireDamageFact = [DungeonStory.Environment.EnvironmentalFireDamageFactHostSmoke]::Run()
$environmentalFireActionFact = [DungeonStory.Environment.EnvironmentalFireActionFactHostSmoke]::Run()
$environmentalFireRuntime = [EnvironmentalFireRuntimeHostSmoke]::Run()
$environmentalFireResourceFact = [EnvironmentalFireResourceFactHostSmoke]::Run()
$environmentalFireResourceOwner = [EnvironmentalFireResourceTransactionHostSmoke]::Run()
$reproductionCore = [ReproductionCoreHostSmoke]::Run()
[pscustomobject]@{
    result = 'PASS'
    scope = 'Current pure C# declarations compiled and executed in memory; healing/fatality/service-terminal/service-mode/death checks exercise neutral fact/event/DTO state and the engine-free service aggregate, reproduction checks exercise the exact V9 aggregate/process/fact declarations, facility-vandalism checks exercise only its engine-free fact/pending aggregate, and environmental-fire checks exercise its exact engine-free domain/runtime/action/damage/resource sources, restore candidate, and resource transaction journal with labelled target/access/water/random/calendar/observer/replay/physical boundary doubles plus a coordinate/Inject shim and production-derived resource constants; Unity/CharacterBodyHealthRuntime, ServiceSessionRuntime, reproduction/vandalism command runtime, native fire observers/ledger, JSON save envelope, authority duplicate detection, canonical parent lookup, native damage/rollback and real physical debit NOT_RUN'
    dispatcher = $dispatcher
    pairedPublicationProof = 'PASS'
    postCommitChecks = $postCommit
    waterFactChecks = $waterFact
    knowledgeDtoChecks = $knowledgeDto
    certifiedSeedDtoChecks = $certifiedSeedDto
    productionOutputPublicationDtoChecks = $productionOutputPublicationDto
    'medical-dto-clone' = $medicalDtoClone
    'healing-fact-checks' = $healingFact
    'service-terminal-fact-checks' = $serviceTerminalFact
    'service-mode-aggregate-checks' = $serviceModeAggregate
    'service-mode-fact-checks' = $serviceModeFact
    'fatality-fact-checks' = $fatalityFact
    'death-reference-checks' = $deathReference
    'facility-vandalism-fact-checks' = $facilityVandalismFact
    'environmental-fire-damage-fact-checks' = $environmentalFireDamageFact
    'environmental-fire-action-fact-checks' = $environmentalFireActionFact
    'environmental-fire-runtime-checks' = $environmentalFireRuntime
    'environmental-fire-runtime-prior-blockers' = 'CS0103 EnvironmentalFireRestoreRules.IsCanonical missing; default EnvironmentalFireWaterReceipt.IsCommitted null-reference on known-rejected InitialAttack; main fixed both before this pinned PASS'
    'environmental-fire-resource-fact-checks' = $environmentalFireResourceFact
    'environmental-fire-resource-owner-checks' = $environmentalFireResourceOwner
    'reproduction-core-checks' = $reproductionCore
    pins = $observationPins
} | ConvertTo-Json -Depth 5
