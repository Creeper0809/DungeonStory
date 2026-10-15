#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

internal sealed class EquipmentRelocationNarrativeEvidenceCapture : IDisposable
{
    private readonly WorldItemStackRuntime items;
    private readonly EvolutionGameplayOutcomeEditorFixture outcomes;

    internal EquipmentRelocationNarrativeEvidenceCapture(
        WorldItemStackRuntime items,
        CombatEquipmentDefinitionSO definition,
        CombatEquipmentInstance instance,
        string ownerId,
        string sourceStackId,
        string destinationStackId,
        GameplayOutcomeReplayIdentity outcome,
        IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> evidence,
        GameplayOutcomeLedger ledger,
        EvolutionGameplayOutcomeEditorFixture outcomes,
        JObject checkpoint)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Instance = instance?.Clone() ?? throw new ArgumentNullException(nameof(instance));
        OwnerId = GameplayOutcomeStableIdSyntax.Require(ownerId, nameof(ownerId));
        SourceStackId = GameplayOutcomeStableIdSyntax.Require(
            sourceStackId, nameof(sourceStackId));
        DestinationStackId = GameplayOutcomeStableIdSyntax.Require(
            destinationStackId, nameof(destinationStackId));
        Outcome = outcome;
        Evidence = (evidence ?? throw new ArgumentNullException(nameof(evidence)))
            .Select(value => value?.Clone()
                ?? throw new InvalidOperationException(
                    "Equipment relocation evidence contains a null binding."))
            .ToArray();
        Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.outcomes = outcomes ?? throw new ArgumentNullException(nameof(outcomes));
        ModuleSelections = outcomes.ModuleSelections;
        Checkpoint = checkpoint == null
            ? throw new ArgumentNullException(nameof(checkpoint))
            : (JObject)checkpoint.DeepClone();
    }

    internal CombatEquipmentDefinitionSO Definition { get; }
    internal CombatEquipmentInstance Instance { get; }
    internal string OwnerId { get; }
    internal string SourceStackId { get; }
    internal string DestinationStackId { get; }
    internal GameplayOutcomeReplayIdentity Outcome { get; }
    internal IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> Evidence { get; }
    internal GameplayOutcomeLedger Ledger { get; }
    internal EquipmentModuleSelectionPublicationRuntime ModuleSelections { get; }
    internal JObject Checkpoint { get; }

    public void Dispose()
    {
        try
        {
            items.Dispose();
        }
        finally
        {
            outcomes.Dispose();
        }
    }
}

internal static class EquipmentRelocationNarrativeEvidenceFixture
{
    private const int AbsoluteDay = 41;

    internal static EquipmentRelocationNarrativeEvidenceCapture Capture(
        string scope,
        CombatEquipmentDefinitionSO definition,
        int variant = 0)
    {
        string canonicalScope = GameplayOutcomeStableIdSyntax.Require(
            scope, nameof(scope));
        if (definition == null || string.IsNullOrWhiteSpace(definition.EquipmentId))
            throw new ArgumentException(
                "A canonical combat-equipment definition is required.",
                nameof(definition));

        string ownerId = "owner:equipment-relocation:" + canonicalScope;
        string instanceId = "item-instance:equipment-relocation:" + canonicalScope;
        string operationId = "equipment-relocation:" + canonicalScope;
        string itemId = PhysicalItemIds.ForEquipment(definition.EquipmentId);
        using var outcomes = new EvolutionGameplayOutcomeEditorFixture(
            "run:equipment-relocation:" + canonicalScope,
            includeTradeInventory: true);
        GameplayOutcomeLedger ledger = outcomes.Ledger;
        GameplayOutcomeRecorder recorder = outcomes.Recorder;
        FixedCalendar calendar = new();
        using DomainFactDispatcher facts = new();
        facts.Subscribe(
            "physical-relocation",
            new GameplayDomainFactObserver<
                PhysicalItemRelocatedFact,
                TradeInventoryOutcomeReceipt>(
                PhysicalItemRelocationFactObservation.Capture,
                outcomes.Registry,
                ledger,
                recorder,
                new GameplayDomainFactCommitGroup(recorder, ledger)));
        facts.Subscribe(
            "physical-disposition",
            new GameplayDomainFactObserver<
                PhysicalItemDisposedFact,
                TradeInventoryOutcomeReceipt>(
                PhysicalItemDispositionFactObservation.Capture,
                outcomes.Registry,
                ledger,
                recorder,
                new GameplayDomainFactCommitGroup(recorder, ledger)));
        WorldItemStackRuntime items = PhysicalItemDebugScenarios
            .CreateRuntimeForCrossDomainFixture(
                EditorItemCatalogFactory.Create(),
                aggregateRootStore: null,
                out WorldItemRepository repository,
                out _,
                out ItemQuantityReservationService quantityReservations,
                out _,
                out _,
                out _);
        EvolutionGameplayOutcomeEditorFixture restoredOutcomes = null;
        try
        {
            CombatEquipmentInstance instance = new()
            {
                instanceId = instanceId,
                definitionId = definition.EquipmentId,
                materialId = "material:iron",
                quality = CombatEquipmentQuality.Normal,
                durabilityRatio = 1f,
                powerCharge = 100f,
                worldState = CombatEquipmentWorldState.Loose,
                ownerCharacterId = string.Empty,
                sourceStackId = string.Empty,
                evolution = new EquipmentEvolutionState(),
                moduleSlots = new List<EquipmentModuleSlotState>()
            };
            ItemInstanceComponentSaveData encoded = EquipmentItemStateCodec.Encode(
                instance);
            string sourceStackId = WorldItemRepositoryEditorAccess.AddStack(
                repository,
                itemId,
                1,
                WorldItemStackState.Loose,
                position: new Vector2Int(3, 4),
                itemInstanceId: instanceId,
                components: new[] { encoded });
            var prepared = new PreparedPhysicalItemRelocationService(
                repository,
                items.MassQuery,
                items.CatalogProvider,
                EditorNullItemMarkerPresenter.Instance);
            var service = new PhysicalItemRelocationService(
                repository,
                prepared,
                facts,
                facts,
                calendar);
            if (!service.TryRelocateQuantity(
                    sourceStackId,
                    1,
                    new Vector2Int(9, 7),
                    WorldItemStackState.Loose,
                    string.Empty,
                    operationId,
                    "module-selection-source",
                    out PhysicalItemRelocationReceipt receipt,
                    out string failureReason)
                || !receipt.IsCommitted)
            {
                throw new InvalidOperationException(
                    "Actual equipment relocation failed: " + failureReason);
            }

            string destinationStackId = receipt.DestinationStackId;
            int relocationCount = 1 + Math.Abs(variant % 3);
            for (int relocationIndex = 1;
                 relocationIndex < relocationCount;
                 relocationIndex++)
            {
                string nextOperationId = operationId + ":"
                    + relocationIndex.ToString();
                if (!service.TryRelocateQuantity(
                        destinationStackId,
                        1,
                        new Vector2Int(9 + relocationIndex, 7 + relocationIndex),
                        WorldItemStackState.Loose,
                        string.Empty,
                        nextOperationId,
                        "module-selection-source-follow-up",
                        out PhysicalItemRelocationReceipt nextReceipt,
                        out failureReason)
                    || !nextReceipt.IsCommitted)
                {
                    throw new InvalidOperationException(
                        "Actual equipment follow-up relocation failed: "
                        + failureReason);
                }
                destinationStackId = nextReceipt.DestinationStackId;
            }
            WorldItemStackSnapshot destination = items.GetAllStacks().Single(value =>
                string.Equals(value.StackId, destinationStackId,
                    StringComparison.Ordinal));
            ItemInstanceComponentSaveData destinationEquipment =
                destination.Components.Single(value =>
                    string.Equals(
                        value.componentTypeId,
                        ItemInstanceComponentIds.Equipment,
                        StringComparison.Ordinal));
            string decodeFailure = string.Empty;
            if (!string.Equals(destination.ItemInstanceId, instanceId,
                    StringComparison.Ordinal)
                || destination.Quantity != 1
                || !EquipmentItemStateCodec.TryDecode(
                    destinationEquipment,
                    out CombatEquipmentInstance restoredInstance,
                    out decodeFailure)
                || !string.Equals(restoredInstance.instanceId, instanceId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Relocated combat equipment did not preserve its physical state: "
                    + decodeFailure);
            }

            GameplayResultKey resultKey = new(
                "trade-inventory.physical-item-relocation-receipt",
                new GameplayOperationId(operationId),
                0L,
                0);
            if (!ledger.TryGetResultIdentity(
                    resultKey,
                    out GameplayOutcomeReplayIdentity outcome)
                || !outcome.HasCanonicalPayloadHash)
                throw new InvalidOperationException(
                    "Equipment relocation did not publish an exact outcome hash.");

            if ((variant & 1) != 0)
            {
                var dispositions = new PhysicalItemBatchDispositionService(
                    repository,
                    items.MassQuery,
                    EditorNullItemMarkerPresenter.Instance,
                    quantityReservations,
                    items.CatalogProvider,
                    facts,
                    facts,
                    new GameplayDomainFactDeliveryQuery(recorder, ledger),
                    calendar);
                if (!dispositions.TryCommit(
                        new[]
                        {
                            new PhysicalItemTransformInput(destinationStackId, 1)
                        },
                        PhysicalItemDispositionKind.Transfer,
                        "equipment-transfer:" + canonicalScope,
                        "module-selection-custody-transfer",
                        out PhysicalItemBatchDispositionReceipt disposition,
                        out failureReason)
                    || !disposition.IsCommitted)
                {
                    throw new InvalidOperationException(
                        "Actual equipment custody transfer failed: "
                        + failureReason);
                }
            }

            GameplayOutcomeLedgerSaveData saved = ledger.CaptureGameplayOutcomes();
            restoredOutcomes = new EvolutionGameplayOutcomeEditorFixture(
                "run:equipment-relocation-restored:" + canonicalScope,
                includeTradeInventory: true);
            GameplayOutcomeLedger restoredLedger = restoredOutcomes.Ledger;
            GameplayOutcomeLedgerRestoreCandidate candidate = restoredLedger
                .PrepareGameplayOutcomeRestore(saved);
            restoredLedger.BeginRestoreCandidate();
            restoredLedger.PublishGameplayOutcomeRestore(candidate);
            restoredLedger.PublishRestoreCandidate();
            restoredLedger.CompleteRestoreCandidate();
            EquipmentModuleSelectionPublicationSaveData savedPublications =
                EquipmentModuleSelectionPublicationRuntime.ValidateAndClone(
                    outcomes.ModuleSelections.Capture());
            foreach (EquipmentModuleSelectionPublicationData observation
                in savedPublications.observations)
            {
                EquipmentModuleSelectedFactPreflight.ValidateObservation(
                    observation,
                    restoredLedger.CaptureGameplayOutcomes());
            }
            restoredOutcomes.ModuleSelections.Restore(savedPublications);
            GameplayEntityId subject = new(
                TradeInventoryOutcomeIds.ItemInstanceKind,
                instanceId);
            IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> evidence =
                ProductionCausalNarrativeEvidence.Capture(
                    restoredLedger,
                    subject,
                    8);
            if (evidence.Any(value => !string.Equals(
                    value.subjectId,
                    instanceId,
                    StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Equipment evidence escaped its item-instance subject.");
            }

            JObject checkpoint = new()
            {
                ["schema"] = "dungeonstory-module-source-equipment-owner@2",
                ["ownerId"] = ownerId,
                ["ownerKind"] = "owner-kind:combat-equipment-instance",
                ["revision"] = 1,
                ["instanceId"] = instanceId,
                ["definitionId"] = definition.EquipmentId,
                ["sourceStackId"] = sourceStackId,
                ["destinationStackId"] = destinationStackId,
                ["equipmentState"] = JObject.Parse(JsonUtility.ToJson(
                    restoredInstance)),
                ["outcomeLedger"] = JObject.Parse(JsonUtility.ToJson(saved)),
                ["moduleSelectionPublications"] =
                    CaptureModuleSelectionPublications(
                        restoredOutcomes.ModuleSelections)
            };
            var capture = new EquipmentRelocationNarrativeEvidenceCapture(
                items,
                definition,
                restoredInstance,
                ownerId,
                sourceStackId,
                destinationStackId,
                outcome,
                evidence,
                restoredLedger,
                restoredOutcomes,
                checkpoint);
            restoredOutcomes = null;
            return capture;
        }
        catch
        {
            restoredOutcomes?.Dispose();
            items.Dispose();
            throw;
        }
    }

    internal static EvolutionGameplayOutcomeEditorFixture RestoreOutcomeFixture(
        JObject sourceCheckpoint,
        string runId)
    {
        if (sourceCheckpoint == null
            || (string)sourceCheckpoint["schema"] != "dungeonstory-module-source-equipment-owner@2"
            || sourceCheckpoint["outcomeLedger"] is not JObject ledgerJson
            || sourceCheckpoint["moduleSelectionPublications"]
                is not JObject publicationsJson)
        {
            throw new InvalidOperationException(
                "Equipment replay source checkpoint has no outcome ledger or module-selection journal.");
        }
        GameplayOutcomeLedgerSaveData saved = JsonUtility.FromJson<
            GameplayOutcomeLedgerSaveData>(
            ledgerJson.ToString(Newtonsoft.Json.Formatting.None));
        var outcomes = new EvolutionGameplayOutcomeEditorFixture(
            GameplayOutcomeStableIdSyntax.Require(runId, nameof(runId)),
            includeTradeInventory: true);
        try
        {
            GameplayOutcomeLedger ledger = outcomes.Ledger;
            GameplayOutcomeLedgerRestoreCandidate candidate = ledger
                .PrepareGameplayOutcomeRestore(saved);
            ledger.BeginRestoreCandidate();
            ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate();
            ledger.CompleteRestoreCandidate();

            string publicationJson = publicationsJson.ToString(
                Newtonsoft.Json.Formatting.None);
            EquipmentModuleSelectionPublicationSaveSection.ValidateJson(
                publicationJson);
            EquipmentModuleSelectionPublicationSaveData publications =
                EquipmentModuleSelectionPublicationRuntime.ValidateAndClone(
                    DungeonSaveJson.Deserialize<
                        EquipmentModuleSelectionPublicationSaveData>(
                            publicationJson));
            GameplayOutcomeLedgerSaveData canonicalLedger =
                ledger.CaptureGameplayOutcomes();
            foreach (EquipmentModuleSelectionPublicationData observation
                in publications.observations)
            {
                EquipmentModuleSelectedFactPreflight.ValidateObservation(
                    observation,
                    canonicalLedger);
            }
            outcomes.ModuleSelections.Restore(publications);
            return outcomes;
        }
        catch
        {
            outcomes.Dispose();
            throw;
        }
    }

    internal static JObject CaptureModuleSelectionPublications(
        EquipmentModuleSelectionPublicationRuntime publications)
    {
        string json = new EquipmentModuleSelectionPublicationSaveSection(
                publications ?? throw new ArgumentNullException(
                    nameof(publications)))
            .Capture();
        EquipmentModuleSelectionPublicationSaveSection.ValidateJson(json);
        return JObject.Parse(json);
    }

    private sealed class FixedCalendar : IGameCalendar
    {
        public int Day => AbsoluteDay;
        public int Hour => 10;
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => ((long)Day - 1L)
            * GameCalendarRules.HoursPerDay + Hour;
        public float ElapsedSeconds => 0f;
        public TimeOfDay TimeOfDay => TimeOfDay.Noon;
        public bool IsRunning => false;
        public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(Day, Hour, utcOffsetHours);
        public void Start() => throw new NotSupportedException();
        public void SetDateTime(int day, int hour) =>
            throw new NotSupportedException();
    }
}

public static class EquipmentRelocationNarrativeEvidenceDebugScenarios
{
    [MenuItem("Tools/DungeonStory/QA/Equipment Relocation Narrative Evidence")]
    public static void RunOrThrow()
    {
        CombatEquipmentDefinitionSO definition = Resources
            .LoadAll<CombatEquipmentDefinitionSO>(
                ResourceCombatEquipmentCatalog.ResourcePath)
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.EquipmentId))
            .OrderBy(value => value.EquipmentId, StringComparer.Ordinal)
            .First();
        using EquipmentRelocationNarrativeEvidenceCapture capture =
            EquipmentRelocationNarrativeEvidenceFixture.Capture(
                "debug",
                definition);
        if (capture.Evidence.Count == 0
            || !string.Equals(
                capture.Evidence[0].subjectKindId,
                TradeInventoryOutcomeIds.ItemInstanceKind.Value,
                StringComparison.Ordinal)
            || !string.Equals(
                capture.Evidence[0].subjectId,
                capture.Instance.instanceId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The actual relocation result is not queryable by equipment instance.");
        }
        Debug.Log("Equipment relocation -> item-instance evidence -> save restore PASS");
    }
}
#endif
