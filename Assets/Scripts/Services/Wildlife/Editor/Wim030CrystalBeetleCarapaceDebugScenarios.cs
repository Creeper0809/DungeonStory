#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Focused evidence for the WIM-030 authored slice. This executes the existing
/// physical carcass transform and its current item save route; it introduces no
/// species-specific runtime behavior or save schema.
/// </summary>
public static class Wim030CrystalBeetleCarapaceDebugScenarios
{
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

    public static string Verify()
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
        VerifyPhysicalCarcassTransform(itemCatalog, speciesCatalog);

        return string.Join(
            Environment.NewLine,
            new[]
            {
                "WIM030 crystal-beetle carapace: PASS",
                "catalog=resource:crystal-beetle-carapace; mass=1000g; displayPrice=5; consumer=none (decorative output)",
                "butcher=wild:carcass:crystal_beetle 4000g -> 1 carapace 1000g + wildlife-carcass-butcher-loss 3000g",
                "rejection=source preserved; retry=single committed output; repeated consumption=blocked; save=current physical-item round trip",
                "publisher-readonly-inspection=matches current authoring; secondPublish=NOT_RUN (use the dedicated no-op operator entry)",
                "publisher-target-paths="
                    + Wim030CrystalBeetleCarapaceAssetBuilder.TargetAssetPathsForOperator,
                "scope=isolated physical-item runtime fixture; not natural wildlife AI or whole-world-save proof; D03 workSeconds=1 is not asserted as 1 WU"
            }) + Environment.NewLine;
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
        IWildlifeSpeciesCatalogProvider speciesCatalog)
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

        IPhysicalItemTransformService rejectingTransform =
            new PhysicalItemTransformService(
                repository,
                spawner,
                runtime.MassQuery,
                new CarapaceExcludedCatalog(itemCatalog),
                EditorNullItemMarkerPresenter.Instance);
        WildlifeCarcassService rejectingService = new(
            runtime,
            rejectingTransform,
            speciesCatalog,
            new GameEventBus());
        bool rejected = rejectingService.TryButcherNextCarcass(
            butcher: null,
            building: null,
            out int rejectedProduced,
            out _);
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
                    EditorNullItemMarkerPresenter.Instance));
        WildlifeCarcassService committedService = new(
            runtime,
            committedTransform,
            speciesCatalog,
            new GameEventBus());
        Require(committedService.TryButcherNextCarcass(
                    butcher: null,
                    building: null,
                    out int produced,
                    out _)
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
        Require(!committedService.TryButcherNextCarcass(
                    butcher: null,
                    building: null,
                    out int repeatedProduced,
                    out _)
                && repeatedProduced == 0,
            "WIM030 consumed a carcass more than once.");

        DungeonPhysicalItemSaveData snapshot = runtime.Capture();
        Require(snapshot.version == DungeonPhysicalItemSaveData.CurrentVersion,
            "WIM030 did not use the current physical-item save format.");
        runtime.Restore(snapshot);
        Require(FindOnly(
                    runtime,
                    Wim030CrystalBeetleCarapaceAssetBuilder.CarapaceItemId)
                .Quantity == 1,
            "WIM030 carapace did not survive the current physical-item save round trip.");
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

    private sealed class RecordingTransformService : IPhysicalItemTransformService
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
    }
}
#endif
