using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;

public sealed class ResourceDungeonFactionCatalogApplicationAdapter
{
    private readonly IReadOnlyList<FactionDefinitionSnapshot> definitions;

    public ResourceDungeonFactionCatalogApplicationAdapter(IGameContentCatalog content)
    {
        List<DungeonFactionDefinitionSO> loaded =
            (content ?? throw new ArgumentNullException(nameof(content)))
            .GetAll<DungeonFactionDefinitionSO>()
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.StableId))
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .ToList();
        if (loaded.Count == 0)
        {
            throw new InvalidOperationException(
                "The root content catalog contains no authored faction definitions.");
        }

        string[] duplicateIds = loaded
            .GroupBy(value => value.StableId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"The root content catalog contains duplicate faction definition IDs: {string.Join(", ", duplicateIds)}.");
        }

        definitions = loaded.Select(value => value.ToSnapshot()).ToArray();
    }

    public IReadOnlyList<FactionDefinitionSnapshot> Definitions => definitions;
}

public sealed class FactionAllianceBenefitRouteBudgetSnapshot
{
    public FactionAllianceBenefitRouteBudgetSnapshot(
        string factionId,
        int cooldownDays,
        string supplyQuoteSourceDigest,
        long debitMilliEwu)
    {
        FactionId = factionId ?? string.Empty;
        CooldownDays = cooldownDays;
        SupplyQuoteSourceDigest = supplyQuoteSourceDigest ?? string.Empty;
        DebitMilliEwu = debitMilliEwu;
    }

    public string FactionId { get; }
    public int CooldownDays { get; }
    public string SupplyQuoteSourceDigest { get; }
    public long DebitMilliEwu { get; }
}

public sealed class ResourceFactionAllianceBenefitBudgetApplicationAdapter
{
    private readonly IReadOnlyDictionary<string,
        FactionAllianceBenefitRouteBudgetSnapshot> routes;
    private readonly IReadOnlyList<
        FactionAllianceBenefitRouteBudgetSnapshot> orderedRoutes;

    public ResourceFactionAllianceBenefitBudgetApplicationAdapter()
    {
        FactionAllianceBenefitBudgetSO source = UnityEngine.Resources.Load<
            FactionAllianceBenefitBudgetSO>(
            FactionAllianceBenefitBudgetSO.ResourcePath);
        if (source == null)
        {
            throw new InvalidOperationException(
                "The required faction alliance-benefit budget authority is missing.");
        }
        IReadOnlyList<string> errors = source.ValidateDefinition();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "The faction alliance-benefit budget authority is invalid: "
                + string.Join(" ", errors));
        }

        SchemaVersion = source.schemaVersion;
        AuthorityDigest = source.approvedBalanceSourceDigest;
        CapacityMilliEwu = source.capacityMilliEwu;
        RefillNumeratorMilliEwu = source.refillNumeratorMilliEwu;
        RefillDenominatorDays = source.refillDenominatorDays;
        orderedRoutes = source.routeCosts
            .Select(value =>
                new FactionAllianceBenefitRouteBudgetSnapshot(
                    value.factionId,
                    value.cooldownDays,
                    value.supplyQuoteSourceDigest,
                    value.debitMilliEwu))
            .ToArray();
        routes = orderedRoutes.ToDictionary(
            value => value.FactionId,
            value => value,
            StringComparer.Ordinal);
    }

    public int SchemaVersion { get; }
    public string AuthorityDigest { get; }
    public long CapacityMilliEwu { get; }
    public long RefillNumeratorMilliEwu { get; }
    public long RefillDenominatorDays { get; }
    public IReadOnlyList<FactionAllianceBenefitRouteBudgetSnapshot> Routes =>
        orderedRoutes;

    public bool TryGetRoute(
        string factionId,
        out FactionAllianceBenefitRouteBudgetSnapshot route) =>
        routes.TryGetValue(factionId ?? string.Empty, out route);
}

public sealed class FactionItemLogisticsDependencies
{
    public FactionItemLogisticsDependencies(
        IWorldItemSpawner itemSpawner,
        IWorldItemStackRuntime itemRuntime,
        IPhysicalItemBatchDispositionService batchDispositions,
        IWorldDropZoneQuery dropZones,
        IPhysicalItemExactSourcePublicationService exactSources)
    {
        ItemSpawner = itemSpawner
            ?? throw new ArgumentNullException(nameof(itemSpawner));
        ItemRuntime = itemRuntime
            ?? throw new ArgumentNullException(nameof(itemRuntime));
        BatchDispositions = batchDispositions
            ?? throw new ArgumentNullException(nameof(batchDispositions));
        DropZones = dropZones ?? throw new ArgumentNullException(nameof(dropZones));
        ExactSources = exactSources
            ?? throw new ArgumentNullException(nameof(exactSources));
    }

    public IWorldItemSpawner ItemSpawner { get; }
    public IWorldItemStackRuntime ItemRuntime { get; }
    public IPhysicalItemBatchDispositionService BatchDispositions { get; }
    public IWorldDropZoneQuery DropZones { get; }
    public IPhysicalItemExactSourcePublicationService ExactSources { get; }
}

public sealed class FactionCharacterSpawnDependencies
{
    public FactionCharacterSpawnDependencies(
        IRunCharacterCatalog characterCatalog,
        ICharacterSpawnerProvider spawnerProvider,
        ICharacterSpawnObjectFactory characterFactory,
        ICharacterAiWorldRegistry worldRegistry)
    {
        CharacterCatalog = characterCatalog
            ?? throw new ArgumentNullException(nameof(characterCatalog));
        SpawnerProvider = spawnerProvider
            ?? throw new ArgumentNullException(nameof(spawnerProvider));
        CharacterFactory = characterFactory
            ?? throw new ArgumentNullException(nameof(characterFactory));
        WorldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
    }

    public IRunCharacterCatalog CharacterCatalog { get; }
    public ICharacterSpawnerProvider SpawnerProvider { get; }
    public ICharacterSpawnObjectFactory CharacterFactory { get; }
    public ICharacterAiWorldRegistry WorldRegistry { get; }
}
