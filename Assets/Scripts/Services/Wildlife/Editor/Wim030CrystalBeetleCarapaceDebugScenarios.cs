#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Focused evidence for the WIM-030 authored slice. This executes the existing
/// physical carcass transform and its current item save route; it introduces no
/// species-specific runtime behavior or save schema.
/// </summary>
public static class Wim030CrystalBeetleCarapaceDebugScenarios
{
    private const string E11ReportPathEnvironmentVariable =
        "DUNGEONSTORY_U0_E11_BUTCHERY_REPORT_PATH";
    private const string ReportPath =
        "Artifacts/QA/wim-implementation/wim-030-crystal-carapace.txt";
    private const long CarcassMassGrams = 4000L;
    private const long CarapaceMassGrams = 1000L;
    private const long ButcherLossMassGrams = 3000L;

    [MenuItem("DungeonStory/WIM-030/Verify Crystal Beetle Carapace")]
    public static void RunFromMenu()
    {
        string report = Verify();
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Artifacts/QA");
        V27BalanceArtifactWriter.WriteIfDifferent(
            ReportPath,
            stream =>
            {
                using StreamWriter writer = new(
                    stream,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    bufferSize: 1024,
                    leaveOpen: true);
                writer.Write(report);
            });
        Debug.Log("WIM030 crystal-beetle carapace verification passed. Report: " + ReportPath);
    }

    public static string Verify() => Verify(null);

    private static string Verify(JArray observations)
    {
        IGameContentCatalog content = new ResourceGameContentCatalog(
            new UnityGameContentRootLoader());
        IItemDefinitionCatalog itemDefinitions = new ResourceItemDefinitionCatalog(content);
        Require(itemDefinitions.Validate().Count == 0,
            "WIM030 item catalog has validation errors.");
        IDungeonItemCatalogProvider itemCatalog =
            new ResourceDungeonItemCatalogProvider(itemDefinitions);
        IWildlifeSpeciesCatalogProvider speciesCatalog =
            new ResourceWildlifeSpeciesCatalogProvider(content, itemDefinitions);

        VerifyPublishedAuthoring(itemDefinitions, speciesCatalog);
        VerifyPhysicalCarcassTransform(itemCatalog, speciesCatalog, observations);

        return string.Join(
            Environment.NewLine,
            new[]
            {
                "WIM030 crystal-beetle carapace: PASS",
                "catalog=resource:crystal-beetle-carapace; mass=1000g; displayPrice=5; consumer=none (decorative output)",
                "butcher=wild:carcass:crystal_beetle 4000g -> 1 carapace 1000g + wildlife-carcass-butcher-loss 3000g",
                "parent=animal-husbandry-work-result; event=wildlife-general-butchery; exact=1; physicalChildJoined=true",
                "rejection=source preserved; retry=single committed output; repeated consumption=blocked; save=current physical-item round trip",
                "publisher-readonly-inspection=matches current authoring; secondPublish=NOT_RUN (use the dedicated no-op operator entry)",
                "publisher-target-paths="
                    + Wim030CrystalBeetleCarapaceAssetBuilder.TargetAssetPathsForOperator,
                "scope=isolated physical-item runtime fixture; not natural wildlife AI or whole-world-save proof; D03 workSeconds=1 is not asserted as 1 WU"
            }) + Environment.NewLine;
    }

    public static void RunE11Batch()
    {
        string output = Environment.GetEnvironmentVariable(
            E11ReportPathEnvironmentVariable);
        Require(
            !string.IsNullOrWhiteSpace(output),
            "A create-only E11 butchery report path is required.");
        string rawOutput = Path.ChangeExtension(output, ".raw.json");
        if (File.Exists(output) || File.Exists(rawOutput))
        {
            throw new InvalidOperationException(
                "E11 butchery evidence is immutable and already exists: "
                + output);
        }

        string directory = Path.GetDirectoryName(output);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var observations = new JArray();
        var checks = new List<string> { "E11_WILDLIFE_BUTCHERY" };
        Exception failure = null;
        try
        {
            // Execute both original phase80 named steps, without running its
            // unrelated whole suite or invoking legacy overwrite writers.
            Require(WildlifeGameplayOutcomeDebugScenarios.RunRegression(),
                "Original wildlife command/action outcome recovery regression failed.");
            checks.Add("wildlife-command-action-outcome-recovery=PASS; originalChecks=4; controlled-owner-bridge-fixture");
            string general = Verify(observations);
            Require(general.Contains(
                "parent=animal-husbandry-work-result; event=wildlife-general-butchery; exact=1; physicalChildJoined=true",
                StringComparison.Ordinal), "Original general-butchery physical-join requirement failed.");
            checks.Add("wildlife-general-butchery-physical-join=PASS");
            checks.Add("generalButchery=PASS");
            checks.Add(general.TrimEnd());
            string taboo = MigratedProducerCharacterSurvivalEntryE2EDebugScenarios
                .VerifyE11TabooButcheryFocused();
            checks.Add("tabooButchery=PASS");
            checks.Add(taboo);
        }
        catch (Exception exception)
        {
            failure = exception;
            checks.Add(exception.ToString());
        }
        finally
        {
            checks.Add("humanApprovalClaimed=false");
            checks.Add("trainingEligible=false");
            checks.Add(failure == null ? "RESULT=PASS" : "RESULT=FAIL");
            var raw = new JObject
            {
                ["schemaVersion"] = 1,
                ["passed"] = failure == null,
                ["trainingEligible"] = false,
                ["naturalGameplay"] = false,
                ["wholeWorldRestore"] = false,
                ["coldProcessRestore"] = false,
                ["fixtureScope"] = "actual carcass transform/item/ledger owners with controlled carcass/catalog rejection; physical-owner JSON round trip only; original generic recovery and taboo assertions preserved",
                ["observations"] = observations,
                ["checks"] = new JArray(checks),
                ["failure"] = failure?.ToString()
            };
            using var rawStream = new FileStream(rawOutput, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using var rawWriter = new StreamWriter(rawStream, new UTF8Encoding(false));
            rawWriter.Write(raw.ToString());
        }
        string report = string.Join(Environment.NewLine, checks) + Environment.NewLine;
        using FileStream stream = new(
            output,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read);
        using StreamWriter writer = new(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(report);
        if (failure != null) throw new InvalidOperationException("E11 wildlife butchery verification failed.", failure);
        Debug.Log("E11_WILDLIFE_BUTCHERY=PASS");
    }

    private static void VerifyPublishedAuthoring(
        IItemDefinitionCatalog itemDefinitions,
        IWildlifeSpeciesCatalogProvider speciesCatalog)
    {
        ItemDefinitionSO[] carapaces = itemDefinitions.All
            .Where(value => value != null
                && string.Equals(
                    value.ItemId,
                    Wim030CrystalBeetleCarapaceAssetBuilder.CarapaceItemId,
                    StringComparison.Ordinal))
            .ToArray();
        Require(carapaces.Length == 1, "WIM030 requires exactly one cataloged carapace.");
        Require(carapaces[0] is ResourceItemDefinitionSO,
            "WIM030 carapace must be a resource item definition.");
        ResourceItemDefinitionSO carapace = (ResourceItemDefinitionSO)carapaces[0];
        Require(carapace.id == 8110
            && carapace.StockCategory == StockCategory.General
            && carapace.Kind == ResourceItemKind.AnimalProduct
            && carapace.IngredientTags == ResourceIngredientTag.Mineral
            && carapace.UnitWeight == 1f
            && carapace.UnitPrice == 5
            && carapace.MaxStack == 40
            && string.IsNullOrEmpty(carapace.RequiredResearchId),
            "WIM030 carapace catalog, mass, price, storage, or no-research contract changed.");

        ItemDefinitionSO carcass = itemDefinitions.GetRequired(
            (ItemDefinitionId)Wim030CrystalBeetleCarapaceAssetBuilder.CrystalBeetleCarcassItemId);
        Require(carcass.UnitWeight == 4f && carcass.UnitPrice == 4,
            "WIM030 existing crystal-beetle carcass baseline changed.");

        Require(speciesCatalog.TryGetSpecies(
                Wim030CrystalBeetleCarapaceAssetBuilder.CrystalBeetleSpeciesId,
                out WildlifeSpeciesDefinition crystalBeetle),
            "WIM030 crystal beetle is absent from the wildlife catalog.");
        WildlifeButcherYield[] yields = (crystalBeetle.ButcherYields
                ?? Array.Empty<WildlifeButcherYield>())
            .Where(value => value != null)
            .ToArray();
        Require(yields.Length == 1
            && string.Equals(
                yields[0].itemId,
                Wim030CrystalBeetleCarapaceAssetBuilder.CarapaceItemId,
                StringComparison.Ordinal)
            && yields[0].amount == 1,
            "WIM030 requires exactly one carapace from a crystal-beetle carcass.");

        Wim030CrystalBeetleCarapaceReadOnlyInspection inspection =
            Wim030CrystalBeetleCarapaceAssetBuilder.InspectPublishedStateReadOnly();
        Require(inspection.MatchesPublishedState,
            "WIM030 read-only authoring inspection does not match the published slice.");
    }

    private static void VerifyPhysicalCarcassTransform(
        IDungeonItemCatalogProvider itemCatalog,
        IWildlifeSpeciesCatalogProvider speciesCatalog,
        JArray observations)
    {
        WorldItemStackRuntime runtime =
            PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                itemCatalog,
                out WorldItemRepository repository,
                out _,
                out _,
                out _,
                out _,
                out _);
        IWorldItemSpawner spawner = new WorldItemSpawner(
            itemCatalog,
            repository,
            EditorNullItemMarkerPresenter.Instance);
        int spawned = spawner.Spawn(
            Wim030CrystalBeetleCarapaceAssetBuilder.CrystalBeetleCarcassItemId,
            amount: 1,
            position: new Vector2Int(7, 11),
            state: WorldItemStackState.Loose,
            destinationId: string.Empty);
        Require(spawned == 1, "WIM030 fixture could not create one existing carcass.");
        WorldItemStackSnapshot source = FindOnly(
            runtime,
            Wim030CrystalBeetleCarapaceAssetBuilder.CrystalBeetleCarcassItemId);
        var outcomeFixture = new MigratedProducerOutcomeEditorFixture(
            "run:wim030-crystal-beetle-transform");
        using DomainFactDispatcher physicalFacts =
            outcomeFixture.CreatePhysicalTransformationFacts();
        var session = new FixedGameSessionStateProvider();
        ObserveButchery(observations, "prepared-carcass", runtime, outcomeFixture, source.StackId);

        IPhysicalItemTransformService rejectingTransform =
            new PhysicalItemTransformService(
                repository,
                spawner,
                runtime.MassQuery,
                new CarapaceExcludedCatalog(itemCatalog),
                EditorNullItemMarkerPresenter.Instance,
                session,
                physicalFacts, clock: new DungeonStory.Foundation.UnityUiClock());
        WildlifeCarcassService rejectingService = new(
            runtime,
            rejectingTransform,
            speciesCatalog,
            new GameEventBus());
        rejectingService.ConstructFactPublisher(
            EditorFixedGameCalendar.Instance,
            physicalFacts);
        bool rejected = rejectingService.TryButcherNextCarcass(
            butcher: null,
            building: null,
            out int rejectedProduced,
            out _);
        ObserveButchery(observations, "missing-output-rejected", runtime, outcomeFixture, source.StackId);
        Require(!rejected && rejectedProduced == 0,
            "WIM030 missing-output rejection unexpectedly committed.");
        Require(FindOnly(
                    runtime,
                    Wim030CrystalBeetleCarapaceAssetBuilder.CrystalBeetleCarcassItemId)
                .StackId == source.StackId,
            "WIM030 rejected transformation did not preserve its source carcass.");
        Require(!runtime.GetAllStacks().Any(value => value != null
                && string.Equals(
                    value.ItemId,
                    Wim030CrystalBeetleCarapaceAssetBuilder.CarapaceItemId,
                    StringComparison.Ordinal)),
            "WIM030 rejected transformation produced a carapace.");

        IPhysicalItemTransformService committedTransform =
            new RecordingTransformService(
                new PhysicalItemTransformService(
                    repository,
                    spawner,
                    runtime.MassQuery,
                    itemCatalog,
                    EditorNullItemMarkerPresenter.Instance,
                    session,
                    physicalFacts, clock: new DungeonStory.Foundation.UnityUiClock()));
        WildlifeCarcassService committedService = new(
            runtime,
            committedTransform,
            speciesCatalog,
            new GameEventBus());
        committedService.ConstructFactPublisher(
            EditorFixedGameCalendar.Instance,
            physicalFacts);
        bool committed = committedService.TryButcherNextCarcass(
                    butcher: null,
                    building: null,
                    out int produced,
                    out _);
        ObserveButchery(observations, "retry-committed", runtime, outcomeFixture, source.StackId,
            committed ? new JObject
            {
                ["committed"] = committed,
                ["produced"] = produced,
                ["reasonCode"] = ((RecordingTransformService)committedTransform).LastReceipt.ReasonCode,
                ["inputMassGrams"] = ((RecordingTransformService)committedTransform).LastReceipt.InputMassGrams,
                ["outputMassGrams"] = ((RecordingTransformService)committedTransform).LastReceipt.OutputMassGrams,
                ["lossMassGrams"] = ((RecordingTransformService)committedTransform).LastReceipt.LossMassGrams,
                ["outputQuantity"] = ((RecordingTransformService)committedTransform).LastReceipt.OutputQuantity
            } : new JObject { ["committed"] = false, ["produced"] = produced });
        Require(committed
                && produced == 1,
            "WIM030 retry did not produce exactly one carapace.");

        RecordingTransformService recording =
            (RecordingTransformService)committedTransform;
        PhysicalItemTransformReceipt receipt = recording.LastReceipt;
        Require(receipt.IsCommitted
            && string.Equals(
                receipt.ReasonCode,
                "wildlife-carcass-butcher-loss",
                StringComparison.Ordinal)
            && receipt.InputMassGrams == CarcassMassGrams
            && receipt.OutputMassGrams == CarapaceMassGrams
            && receipt.LossMassGrams == ButcherLossMassGrams
            && receipt.OutputQuantity == 1,
            "WIM030 transform receipt lost the approved mass or loss contract.");
        Require(outcomeFixture.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(value =>
                value != null
                && value.outcomeTypeId == MigratedProducerOutcomeCatalog.Get(
                    MigratedProducerOutcomeKind.AnimalHusbandryWorkResult)
                    .OutcomeTypeId.Value
                && value.facts.Any(fact => fact != null
                    && fact.factId == MigratedProducerOutcomeIds.SummaryFact.Value
                    && fact.value.Contains(
                        "carcassStackId=" + source.StackId,
                        StringComparison.Ordinal))) == 1,
            "WIM030 general butchery parent result was missing or duplicated.");
        Require(!runtime.GetAllStacks().Any(value => value != null
                && string.Equals(
                    value.ItemId,
                    Wim030CrystalBeetleCarapaceAssetBuilder.CrystalBeetleCarcassItemId,
                    StringComparison.Ordinal)),
            "WIM030 committed transformation retained its source carcass.");
        Require(FindOnly(
                    runtime,
                    Wim030CrystalBeetleCarapaceAssetBuilder.CarapaceItemId)
                .Quantity == 1,
            "WIM030 committed transformation has an incorrect carapace quantity.");
        string completedLedger = JsonUtility.ToJson(outcomeFixture.Ledger.CaptureGameplayOutcomes());
        string completedPhysical = JsonUtility.ToJson(runtime.Capture());
        bool repeated = committedService.TryButcherNextCarcass(
                    butcher: null,
                    building: null,
                    out int repeatedProduced,
                    out _);
        ObserveButchery(observations, "same-carcass-repeat-blocked", runtime, outcomeFixture, source.StackId);
        Require(!repeated
                && repeatedProduced == 0,
            "WIM030 consumed a carcass more than once.");
        Require(JsonUtility.ToJson(outcomeFixture.Ledger.CaptureGameplayOutcomes()) == completedLedger
            && JsonUtility.ToJson(runtime.Capture()) == completedPhysical,
            "Repeated butchery changed published completion identity or physical output.");

        DungeonPhysicalItemSaveData snapshot = runtime.Capture();
        Require(snapshot.version == DungeonPhysicalItemSaveData.CurrentVersion,
            "WIM030 did not use the current physical-item save format.");
        runtime.Restore(JsonUtility.FromJson<DungeonPhysicalItemSaveData>(JsonUtility.ToJson(snapshot)));
        ObserveButchery(observations, "physical-json-restored", runtime, outcomeFixture, source.StackId);
        Require(FindOnly(
                    runtime,
                    Wim030CrystalBeetleCarapaceAssetBuilder.CarapaceItemId)
                .Quantity == 1,
            "WIM030 carapace did not survive the current physical-item save round trip.");
        Require(JsonUtility.ToJson(outcomeFixture.Ledger.CaptureGameplayOutcomes()) == completedLedger
            && JsonUtility.ToJson(runtime.Capture()) == completedPhysical,
            "Physical JSON round trip changed completion identity or output state.");
        repeated = committedService.TryButcherNextCarcass(null, null, out repeatedProduced, out _);
        ObserveButchery(observations, "restored-carcass-repeat-blocked", runtime, outcomeFixture, source.StackId);
        Require(!repeated && repeatedProduced == 0
            && JsonUtility.ToJson(outcomeFixture.Ledger.CaptureGameplayOutcomes()) == completedLedger
            && JsonUtility.ToJson(runtime.Capture()) == completedPhysical,
            "Restored butchery repeated output or changed completion identity.");
    }

    private static void ObserveButchery(JArray observations, string stage,
        WorldItemStackRuntime runtime, MigratedProducerOutcomeEditorFixture fixture,
        string sourceStackId, JObject receipt = null)
    {
        if (observations == null) return;
        observations.Add(new JObject
        {
            ["stage"] = stage,
            ["sourceStackId"] = sourceStackId,
            ["physical"] = JObject.Parse(JsonUtility.ToJson(runtime.Capture())),
            ["ledger"] = JObject.Parse(JsonUtility.ToJson(fixture.Ledger.CaptureGameplayOutcomes())),
            ["transformReceipt"] = receipt
        });
    }

    private static WorldItemStackSnapshot FindOnly(
        IWorldItemStackRuntime runtime,
        string itemId)
    {
        WorldItemStackSnapshot[] matches = runtime.GetAllStacks()
            .Where(value => value != null
                && string.Equals(value.ItemId, itemId, StringComparison.Ordinal))
            .ToArray();
        Require(matches.Length == 1, "Expected exactly one physical stack for '" + itemId + "'.");
        return matches[0];
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class CarapaceExcludedCatalog : IDungeonItemCatalogProvider
    {
        private readonly IDungeonItemCatalogProvider inner;

        public CarapaceExcludedCatalog(IDungeonItemCatalogProvider inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public IReadOnlyList<DungeonItemDefinition> All => inner.All
            .Where(value => !string.Equals(
                value.ItemId,
                Wim030CrystalBeetleCarapaceAssetBuilder.CarapaceItemId,
                StringComparison.Ordinal))
            .ToArray();

        public DungeonItemDefinition GetDefinition(string itemId)
        {
            if (string.Equals(
                    itemId,
                    Wim030CrystalBeetleCarapaceAssetBuilder.CarapaceItemId,
                    StringComparison.Ordinal))
            {
                throw new KeyNotFoundException("WIM030 rejection fixture excludes carapace output.");
            }
            return inner.GetDefinition(itemId);
        }

        public bool TryGetDefinition(string itemId, out DungeonItemDefinition definition)
        {
            if (string.Equals(
                    itemId,
                    Wim030CrystalBeetleCarapaceAssetBuilder.CarapaceItemId,
                    StringComparison.Ordinal))
            {
                definition = null;
                return false;
            }
            return inner.TryGetDefinition(itemId, out definition);
        }
    }

    private sealed class RecordingTransformService :
        IPhysicalItemTransformService,
        IPhysicalItemTransformBatchService,
        IPhysicalItemTransformRecoveryService
    {
        private readonly IPhysicalItemTransformService inner;

        public RecordingTransformService(IPhysicalItemTransformService inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public PhysicalItemTransformReceipt LastReceipt { get; private set; }

        public bool TryTransformWholeStack(
            string sourceStackId,
            IReadOnlyList<PhysicalItemTransformOutput> outputs,
            string operationId,
            string reasonCode,
            out PhysicalItemTransformReceipt receipt,
            out PhysicalItemTransformFailureCode failureCode,
            out string failureReason)
        {
            bool committed = inner.TryTransformWholeStack(
                sourceStackId,
                outputs,
                operationId,
                reasonCode,
                out receipt,
                out failureCode,
                out failureReason);
            if (committed)
                LastReceipt = receipt;
            return committed;
        }

        public bool TryTransformQuantity(
            string sourceStackId,
            int sourceQuantity,
            IReadOnlyList<PhysicalItemTransformOutput> outputs,
            string operationId,
            string reasonCode,
            out PhysicalItemTransformReceipt receipt,
            out PhysicalItemTransformFailureCode failureCode,
            out string failureReason)
        {
            bool committed = inner.TryTransformQuantity(
                sourceStackId,
                sourceQuantity,
                outputs,
                operationId,
                reasonCode,
                out receipt,
                out failureCode,
                out failureReason);
            if (committed)
                LastReceipt = receipt;
            return committed;
        }

        public bool TryTransformQuantities(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            IReadOnlyList<PhysicalItemTransformOutput> outputs,
            string operationId,
            string reasonCode,
            out PhysicalItemTransformReceipt receipt,
            out PhysicalItemTransformFailureCode failureCode,
            out string failureReason)
        {
            bool committed = inner.TryTransformQuantities(
                inputs,
                outputs,
                operationId,
                reasonCode,
                out receipt,
                out failureCode,
                out failureReason);
            if (committed)
                LastReceipt = receipt;
            return committed;
        }

        public bool TryTransformWholeStackWithCompanion(
            string sourceStackId,
            IReadOnlyList<PhysicalItemTransformOutput> outputs,
            string operationId,
            string reasonCode,
            DomainFactEmission companion,
            int companionAbsoluteDay,
            out PhysicalItemTransformReceipt receipt,
            out PhysicalItemTransformFailureCode failureCode,
            out string failureReason)
        {
            if (inner is not IPhysicalItemTransformBatchService batch)
                throw new InvalidOperationException(
                    "Recording transform requires the batch companion boundary.");
            bool committed = batch.TryTransformWholeStackWithCompanion(
                sourceStackId,
                outputs,
                operationId,
                reasonCode,
                companion,
                companionAbsoluteDay,
                out receipt,
                out failureCode,
                out failureReason);
            if (committed)
                LastReceipt = receipt;
            return committed;
        }

        public bool HasTransformation(string operationId)
        {
            if (inner is not IPhysicalItemTransformRecoveryService recovery)
                throw new InvalidOperationException(
                    "Recording transform requires the recovery boundary.");
            return recovery.HasTransformation(operationId);
        }

        public bool TryRecoverTransformation(
            string operationId,
            DomainFactReplay companion,
            out PhysicalItemTransformReceipt receipt,
            out string failureReason)
        {
            if (inner is not IPhysicalItemTransformRecoveryService recovery)
                throw new InvalidOperationException(
                    "Recording transform requires the recovery boundary.");
            bool recovered = recovery.TryRecoverTransformation(
                operationId,
                companion,
                out receipt,
                out failureReason);
            if (recovered)
                LastReceipt = receipt;
            return recovered;
        }
    }
}
#endif
