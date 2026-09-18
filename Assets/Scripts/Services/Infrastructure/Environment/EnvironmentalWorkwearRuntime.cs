using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

internal sealed class CharacterEnvironmentAggregateState
{
    internal Dictionary<CharacterId, CharacterEnvironmentExposure> Exposures { get; } =
        new();
    internal Dictionary<CharacterId, EnvironmentalWorkKind> WorkContexts { get; } =
        new();
    internal Dictionary<CharacterId, ItemInstanceId> EquippedWorkwearByCharacter { get; } =
        new();
    internal float Accumulator { get; set; }
    internal int WorkwearVersion { get; set; }

    internal CharacterEnvironmentAggregateState CopyEnvironmentState()
    {
        CharacterEnvironmentAggregateState copy = new()
        {
            Accumulator = Accumulator,
            WorkwearVersion = WorkwearVersion + 1
        };
        foreach (KeyValuePair<CharacterId, CharacterEnvironmentExposure> pair in Exposures)
        {
            copy.Exposures.Add(pair.Key, pair.Value);
        }
        foreach (KeyValuePair<CharacterId, EnvironmentalWorkKind> pair in WorkContexts)
        {
            copy.WorkContexts.Add(pair.Key, pair.Value);
        }
        return copy;
    }
}

public sealed class CharacterEnvironmentAggregateStateStore
{
    private readonly DungeonRuntimeAggregateRootStore rootStore;

    public CharacterEnvironmentAggregateStateStore(
        DungeonRuntimeAggregateRootStore rootStore)
    {
        this.rootStore = rootStore
            ?? throw new ArgumentNullException(nameof(rootStore));
    }

    internal CharacterEnvironmentAggregateState Current =>
        rootStore.GetOrCreate(() => new CharacterEnvironmentAggregateState());

    internal void Replace(CharacterEnvironmentAggregateState restored)
    {
        rootStore.Replace(
            restored ?? throw new ArgumentNullException(nameof(restored)));
    }
}

public interface IEnvironmentalWorkwearQuery
{
    int Version { get; }
    bool TryGetEquipped(
        CharacterId characterId,
        out EnvironmentalWorkwearSO workwear);
    bool TryGetEquippedItemInstance(
        CharacterId characterId,
        out ItemInstanceId itemInstanceId,
        out EnvironmentalWorkwearSO workwear);
    int GetAvailableStock(string workwearId);
    bool CanAutoEquipForCold(
        CharacterActor actor,
        Vector2Int destination,
        out DomainFailure failure);
}

public interface IEnvironmentalWorkwearCommand
{
    bool TryEquip(
        CharacterActor actor,
        string workwearId,
        out DomainFailure failure);
    bool TryAutoEquipForCold(
        CharacterActor actor,
        Vector2Int destination,
        out DomainFailure failure);
    bool TryUnequip(CharacterId characterId, out DomainFailure failure);
}

public interface IEnvironmentalWorkwearPersistence
{
    IReadOnlyList<EnvironmentalWorkwearSaveData> CaptureEquipped();
    IReadOnlyDictionary<CharacterId, ItemInstanceId> PrepareRestoreEquipped(
        IReadOnlyList<EnvironmentalWorkwearSaveData> equipped,
        DungeonGameRestoreReport report = null);
    void ValidatePreparedProjection(
        IReadOnlyDictionary<CharacterId, ItemInstanceId> prepared,
        CharacterApparelRestoreCandidate apparel,
        DungeonGameRestoreReport report);
}

public sealed class NoEnvironmentalWorkwearCommand :
    IEnvironmentalWorkwearCommand
{
    public static NoEnvironmentalWorkwearCommand Instance { get; } = new();

    private NoEnvironmentalWorkwearCommand()
    {
    }

    public bool TryEquip(
        CharacterActor actor,
        string workwearId,
        out DomainFailure failure)
    {
        failure = new DomainFailure(
            FailureCode.EnvironmentWorkwearStockMissing,
            workwearId ?? string.Empty);
        return false;
    }

    public bool TryAutoEquipForCold(
        CharacterActor actor,
        Vector2Int destination,
        out DomainFailure failure)
    {
        failure = new DomainFailure(
            FailureCode.EnvironmentWorkwearStockMissing,
            actor?.SpeciesTag ?? string.Empty);
        return false;
    }

    public bool TryUnequip(
        CharacterId characterId,
        out DomainFailure failure)
    {
        failure = new DomainFailure(
            FailureCode.EnvironmentWorkwearNotEquipped,
            characterId.Value);
        return false;
    }
}

public sealed class EnvironmentalWorkwearRuntime :
    IEnvironmentalWorkwearQuery,
    IEnvironmentalWorkwearCommand,
    IEnvironmentalWorkwearPersistence
{
    private readonly IEnvironmentalWorkwearCatalog catalog;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IBlueprintResearchStateService research;
    private readonly ICharacterApparelQuery apparel;
    private readonly ICharacterApparelCommand apparelCommands;
    private readonly IWorldItemStackRuntime items;
    private readonly IPhysicalItemRestoreCandidateStackQuery restoreCandidateItems;
    private readonly IStockQuery stock;

    public EnvironmentalWorkwearRuntime(
        IEnvironmentalWorkwearCatalog catalog,
        IBuildingWorldQuery buildingWorld,
        IBlueprintResearchStateService research,
        ICharacterApparelQuery apparel,
        ICharacterApparelCommand apparelCommands,
        IWorldItemStackRuntime items,
        IPhysicalItemRestoreCandidateStackQuery restoreCandidateItems,
        IStockQuery stock)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.research = research
            ?? throw new ArgumentNullException(nameof(research));
        this.apparel = apparel
            ?? throw new ArgumentNullException(nameof(apparel));
        this.apparelCommands = apparelCommands
            ?? throw new ArgumentNullException(nameof(apparelCommands));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.restoreCandidateItems = restoreCandidateItems
            ?? throw new ArgumentNullException(nameof(restoreCandidateItems));
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
    }

    public int Version => apparel.Version;

    public bool TryGetEquipped(
        CharacterId characterId,
        out EnvironmentalWorkwearSO workwear)
    {
        return TryGetEquippedItemInstance(characterId, out _, out workwear);
    }

    public bool TryGetEquippedItemInstance(
        CharacterId characterId,
        out ItemInstanceId itemInstanceId,
        out EnvironmentalWorkwearSO workwear)
    {
        return TrySelectCanonicalEquipped(
            apparel.GetEquipped(characterId),
            useRestoreCandidate: false,
            out itemInstanceId,
            out workwear);
    }

    public int GetAvailableStock(string workwearId)
    {
        if (!catalog.TryGet(workwearId, out EnvironmentalWorkwearSO definition)
            || stock.GetGlobalQuantity(definition.ItemDefinitionId) <= 0)
        {
            return 0;
        }

        return items.GetAllStacks().Count(stack =>
            IsAvailablePhysicalItem(stack, definition.ItemDefinitionId));
    }

    public bool CanAutoEquipForCold(
        CharacterActor actor,
        Vector2Int destination,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        CharacterId characterId = new(actor?.Identity?.PersistentId);
        if (actor == null || !characterId.IsValid)
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentWorkwearCharacterMissing);
            return false;
        }
        if (TryGetEquippedItemInstance(
                characterId,
                out _,
                out EnvironmentalWorkwearSO equipped)
            && ProvidesColdProtection(equipped))
        {
            return true;
        }
        if (apparel.GetPolicy(characterId).HasTemporaryOverride)
        {
            failure = new DomainFailure(
                FailureCode.ApparelPlanStale,
                characterId.Value);
            return false;
        }
        if (!HasReachableLocker(actor.GetNowXY(), destination))
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentWorkwearLockerUnreachable,
                destination.x.ToString(CultureInfo.InvariantCulture),
                destination.y.ToString(CultureInfo.InvariantCulture));
            return false;
        }
        bool found = catalog.Definitions.Any(candidate => candidate != null
            && ProvidesColdProtection(candidate)
            && candidate.AllowsSpecies(actor.SpeciesTag)
            && IsResearchUnlocked(candidate)
            && FindAvailablePhysicalItem(candidate.ItemDefinitionId) != null);
        if (!found)
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentWorkwearStockMissing,
                actor.SpeciesTag ?? string.Empty);
        }
        return found;
    }

    public bool TryEquip(
        CharacterActor actor,
        string workwearId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (actor?.Identity == null
            || string.IsNullOrWhiteSpace(actor.Identity.PersistentId))
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentWorkwearCharacterMissing);
            return false;
        }
        if (!catalog.TryGet(workwearId, out EnvironmentalWorkwearSO definition))
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentWorkwearDefinitionMissing,
                workwearId ?? string.Empty);
            return false;
        }
        if (!IsResearchUnlocked(definition))
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentWorkwearResearchLocked,
                definition.RequiredResearchId);
            return false;
        }
        if (!definition.AllowsSpecies(actor.SpeciesTag))
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentWorkwearSpeciesIncompatible,
                actor.SpeciesTag ?? string.Empty,
                definition.WorkwearId);
            return false;
        }

        CharacterId characterId = new(actor.Identity.PersistentId);
        if (!characterId.IsValid)
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentWorkwearCharacterMissing);
            return false;
        }
        if (HasEquippedWorkwear(characterId, definition.WorkwearId))
        {
            return true;
        }

        WorldItemStackSnapshot candidate = FindAvailablePhysicalItem(
            definition.ItemDefinitionId);
        if (candidate == null)
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentWorkwearStockMissing,
                definition.ItemDefinitionId);
            return false;
        }

        ItemInstanceId candidateId = (ItemInstanceId)candidate.ItemInstanceId;
        if (!candidateId.IsValid)
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentWorkwearInstanceIdMissing,
                candidate.StackId);
            return false;
        }

        return apparelCommands.TryBeginTemporaryOverride(
            characterId,
            candidateId,
            "environment:hauling-harness",
            out failure);
    }

    public bool TryAutoEquipForCold(
        CharacterActor actor,
        Vector2Int destination,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (actor == null)
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentWorkwearCharacterMissing);
            return false;
        }
        CharacterId characterId = new(actor.Identity?.PersistentId);
        if (!characterId.IsValid)
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentWorkwearCharacterMissing);
            return false;
        }
        if (TryGetEquippedItemInstance(
                characterId,
                out _,
                out EnvironmentalWorkwearSO equipped)
            && ProvidesColdProtection(equipped))
        {
            return true;
        }
        if (apparel.GetPolicy(characterId).HasTemporaryOverride)
        {
            failure = new DomainFailure(
                FailureCode.ApparelPlanStale,
                characterId.Value);
            return false;
        }
        if (!HasReachableLocker(actor.GetNowXY(), destination))
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentWorkwearLockerUnreachable,
                destination.x.ToString(CultureInfo.InvariantCulture),
                destination.y.ToString(CultureInfo.InvariantCulture));
            return false;
        }

        EnvironmentalWorkwearSO best = catalog.Definitions
            .Where(candidate => candidate != null
                && ProvidesColdProtection(candidate)
                && GetAvailableStock(candidate.WorkwearId) > 0
                && IsResearchUnlocked(candidate)
                && candidate.AllowsSpecies(actor.SpeciesTag))
            .OrderBy(candidate => candidate.Protection.comfortMinimumOffset)
            .ThenBy(candidate => candidate.Protection.coldExposureMultiplier)
            .FirstOrDefault();
        if (best == null)
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentWorkwearStockMissing,
                actor.SpeciesTag ?? string.Empty);
            return false;
        }

        WorldItemStackSnapshot candidate = FindAvailablePhysicalItem(
            best.ItemDefinitionId);
        if (candidate == null)
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentWorkwearStockMissing,
                best.ItemDefinitionId);
            return false;
        }
        ItemInstanceId itemInstanceId = (ItemInstanceId)candidate.ItemInstanceId;
        return apparelCommands.TryBeginTemporaryOverride(
            characterId,
            itemInstanceId,
            "environment:cold-work",
            out failure);
    }

    public bool TryUnequip(CharacterId characterId, out DomainFailure failure)
    {
        failure = DomainFailure.None;
        return apparelCommands.TryRestoreTemporaryOverride(
            characterId,
            out failure);
    }

    public IReadOnlyList<EnvironmentalWorkwearSaveData> CaptureEquipped()
    {
        List<EnvironmentalWorkwearSaveData> result = new();
        foreach (IGrouping<CharacterId, EquippedApparelSnapshot> group in
                 apparel.GetAllEquipped()
                     .Where(value => value.ItemInstanceId.IsValid)
                     .GroupBy(value => value.CharacterId)
                     .OrderBy(value => value.Key.Value, StringComparer.Ordinal))
        {
            if (TrySelectCanonicalEquipped(
                    group,
                    useRestoreCandidate: false,
                    out ItemInstanceId itemInstanceId,
                    out _))
            {
                result.Add(new EnvironmentalWorkwearSaveData
                {
                    characterId = group.Key.Value,
                    itemInstanceId = itemInstanceId.Value
                });
            }
        }
        return result;
    }

    public IReadOnlyDictionary<CharacterId, ItemInstanceId> PrepareRestoreEquipped(
        IReadOnlyList<EnvironmentalWorkwearSaveData> equipped,
        DungeonGameRestoreReport report = null)
    {
        Dictionary<CharacterId, ItemInstanceId> prepared = new();
        HashSet<ItemInstanceId> restoredInstances = new();
        foreach (EnvironmentalWorkwearSaveData entry in equipped
                     ?? Array.Empty<EnvironmentalWorkwearSaveData>())
        {
            CharacterId characterId = new(entry?.characterId);
            ItemInstanceId instanceId = (ItemInstanceId)entry?.itemInstanceId;
            if (!characterId.IsValid
                || !instanceId.IsValid
                || !restoredInstances.Add(instanceId)
                || !TryFindRestorePhysicalItem(
                    instanceId,
                    out PhysicalItemRestoreCandidateStackSnapshot stack)
                || !catalog.TryGetByItemDefinitionId(stack.ItemId, out _)
                || !string.Equals(
                    stack.DestinationId,
                    CharacterApparelAggregate.EquippedDestinationPrefix
                        + characterId.Value,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Invalid equipped physical workwear '{entry?.itemInstanceId}'.");
            }

            if (!prepared.TryAdd(characterId, instanceId))
            {
                throw new InvalidOperationException(
                    $"Duplicate equipped workwear character '{characterId.Value}'.");
            }
        }

        return prepared;
    }

    public void ValidatePreparedProjection(
        IReadOnlyDictionary<CharacterId, ItemInstanceId> prepared,
        CharacterApparelRestoreCandidate apparelCandidate,
        DungeonGameRestoreReport report)
    {
        Dictionary<CharacterId, ItemInstanceId> derived =
            (apparelCandidate?.State.Characters
                ?? new Dictionary<CharacterId, CharacterApparelRecord>())
                .Select(pair =>
                {
                    EquippedApparelSnapshot[] equipped = pair.Value.Equipped
                        .Select(value => new EquippedApparelSnapshot(
                            pair.Key,
                            (ItemInstanceId)value.itemInstanceId,
                            value.apparelDefinitionId,
                            value.layer,
                            (AnatomyAttachmentPoint)value.occupiedPoints))
                        .ToArray();
                    return TrySelectCanonicalEquipped(
                            equipped,
                            useRestoreCandidate: true,
                            out ItemInstanceId selected,
                            out _)
                        ? (valid: true, characterId: pair.Key, selected)
                        : (valid: false, characterId: pair.Key, selected: default(ItemInstanceId));
                })
                .Where(value => value.valid)
                .ToDictionary(value => value.characterId, value => value.selected);
        IReadOnlyDictionary<CharacterId, ItemInstanceId> source = prepared
            ?? new Dictionary<CharacterId, ItemInstanceId>();
        if (source.Count != derived.Count
            || source.Any(pair => !derived.TryGetValue(
                pair.Key,
                out ItemInstanceId value)
                || !value.Equals(pair.Value)))
        {
            report?.AddError(
                "Current equippedWorkwear checksum does not match the apparel physical projection.");
        }
    }

    private bool IsResearchUnlocked(EnvironmentalWorkwearSO definition)
    {
        string researchId = definition.RequiredResearchId;
        return string.IsNullOrWhiteSpace(researchId)
            || research.GetState().Projects.IsCompleted(
                new ResearchProjectId(researchId));
    }

    private bool HasReachableLocker(Vector2Int origin, Vector2Int destination)
    {
        IReadOnlyList<BuildableObject> buildings =
            buildingWorld.Buildings ?? Array.Empty<BuildableObject>();
        for (int i = 0; i < buildings.Count; i++)
        {
            BuildableObject building = buildings[i];
            BuildingProtectiveEquipmentLockerAbility locker =
                building?.BuildingData
                    ?.GetAbility<BuildingProtectiveEquipmentLockerAbility>();
            if (building == null || building.isDestroy || locker == null)
            {
                continue;
            }

            int fromOrigin = Mathf.Abs(building.centerPos.x - origin.x)
                + Mathf.Abs(building.centerPos.y - origin.y);
            int fromDestination = Mathf.Abs(building.centerPos.x - destination.x)
                + Mathf.Abs(building.centerPos.y - destination.y);
            if (Mathf.Min(fromOrigin, fromDestination) <= locker.serviceRadius)
            {
                return true;
            }
        }

        return false;
    }

    private WorldItemStackSnapshot FindAvailablePhysicalItem(string itemId)
    {
        return items.GetAllStacks()
            .Where(stack => IsAvailablePhysicalItem(stack, itemId))
            .OrderBy(stack => stack.State == WorldItemStackState.Stored ? 0 : 1)
            .ThenBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool IsAvailablePhysicalItem(
        WorldItemStackSnapshot stack,
        string itemId)
    {
        return stack != null
            && stack.Quantity == 1
            && ((ItemInstanceId)stack.ItemInstanceId).IsValid
            && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
            && stack.AvailableQuantity > 0
            && !stack.Forbidden
            && stack.State is WorldItemStackState.Loose
                or WorldItemStackState.Stored
                or WorldItemStackState.FacilityOutputBuffer
            && !(stack.DestinationId ?? string.Empty).StartsWith(
                CharacterApparelAggregate.EquippedDestinationPrefix,
                StringComparison.Ordinal)
            && ApparelItemStateCodec.TryRead(
                stack.Components,
                out ApparelInstanceState state)
            && CharacterApparelAggregate.IsConditionAvailableForSelection(state);
    }

    private bool TrySelectCanonicalEquipped(
        IEnumerable<EquippedApparelSnapshot> equipped,
        bool useRestoreCandidate,
        out ItemInstanceId itemInstanceId,
        out EnvironmentalWorkwearSO workwear)
    {
        List<(ItemInstanceId itemInstanceId, EnvironmentalWorkwearSO workwear)>
            candidates = new();
        foreach (EquippedApparelSnapshot candidate in equipped
                     ?? Array.Empty<EquippedApparelSnapshot>())
        {
            string itemId;
            if (useRestoreCandidate)
            {
                if (!TryFindRestorePhysicalItem(
                        candidate.ItemInstanceId,
                        out PhysicalItemRestoreCandidateStackSnapshot stack))
                {
                    continue;
                }
                itemId = stack.ItemId;
            }
            else
            {
                if (!TryFindPhysicalItem(
                        candidate.ItemInstanceId,
                        out WorldItemStackSnapshot stack))
                {
                    continue;
                }
                itemId = stack.ItemId;
            }
            if (catalog.TryGetByItemDefinitionId(
                    itemId,
                    out EnvironmentalWorkwearSO definition))
            {
                candidates.Add((candidate.ItemInstanceId, definition));
            }
        }
        (ItemInstanceId itemInstanceId, EnvironmentalWorkwearSO workwear)
            selected = candidates
                .OrderBy(value => ProvidesColdProtection(value.workwear) ? 0 : 1)
                .ThenBy(value => value.workwear.Protection.comfortMinimumOffset)
                .ThenBy(value => value.workwear.Protection.coldExposureMultiplier)
                .ThenBy(value => value.itemInstanceId.Value, StringComparer.Ordinal)
                .FirstOrDefault();
        itemInstanceId = selected.itemInstanceId;
        workwear = selected.workwear;
        return itemInstanceId.IsValid && workwear != null;
    }

    private bool HasEquippedWorkwear(
        CharacterId characterId,
        string workwearId)
    {
        foreach (EquippedApparelSnapshot equipped in
                 apparel.GetEquipped(characterId))
        {
            if (TryFindPhysicalItem(
                    equipped.ItemInstanceId,
                    out WorldItemStackSnapshot stack)
                && catalog.TryGetByItemDefinitionId(
                    stack.ItemId,
                    out EnvironmentalWorkwearSO definition)
                && string.Equals(
                    definition.WorkwearId,
                    workwearId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool ProvidesColdProtection(
        EnvironmentalWorkwearSO definition) =>
        definition?.Protection != null
        && (definition.Protection.comfortMinimumOffset < 0f
            || definition.Protection.safeMinimumOffset < 0f
            || definition.Protection.coldExposureMultiplier < 1f);

    private bool TryFindPhysicalItem(
        ItemInstanceId itemInstanceId,
        out WorldItemStackSnapshot result)
    {
        result = items.GetAllStacks().FirstOrDefault(stack => stack != null
            && string.Equals(
                stack.ItemInstanceId,
                itemInstanceId.Value,
                StringComparison.Ordinal));
        return result != null;
    }

    private bool TryFindRestorePhysicalItem(
        ItemInstanceId itemInstanceId,
        out PhysicalItemRestoreCandidateStackSnapshot result)
    {
        if (restoreCandidateItems.IsCandidateAvailable)
        {
            return restoreCandidateItems.TryGetStack(itemInstanceId, out result);
        }

        if (!TryFindPhysicalItem(
                itemInstanceId,
                out WorldItemStackSnapshot live))
        {
            result = null;
            return false;
        }

        result = new PhysicalItemRestoreCandidateStackSnapshot(
            live.StackId,
            itemInstanceId,
            live.ItemId,
            live.Quantity,
            live.State,
            live.Position,
            live.DestinationId,
            live.Forbidden);
        return true;
    }

}

public sealed class CharacterEnvironmentProtectionResolver :
    ICharacterEnvironmentProtectionResolver
{
    private readonly IEnvironmentalWorkwearQuery workwear;
    private readonly ICharacterApparelQuery apparel;
    private readonly IWorldItemStackRuntime items;
    private readonly IApparelDefinitionCatalog apparelDefinitions;
    private readonly ITextileMaterialCatalog materials;
    private readonly IApparelMaterialProjector projector;
    private readonly IAnatomyAttachmentQuery anatomy;
    private readonly Dictionary<string, WorldItemStackSnapshot> stackByInstance =
        new(StringComparer.Ordinal);
    private int cachedItemStackVersion = int.MinValue;

    public CharacterEnvironmentProtectionResolver(
        IEnvironmentalWorkwearQuery workwear,
        ICharacterApparelQuery apparel,
        IWorldItemStackRuntime items,
        IApparelDefinitionCatalog apparelDefinitions,
        ITextileMaterialCatalog materials,
        IApparelMaterialProjector projector,
        IAnatomyAttachmentQuery anatomy)
    {
        this.workwear = workwear
            ?? throw new ArgumentNullException(nameof(workwear));
        this.apparel = apparel ?? throw new ArgumentNullException(nameof(apparel));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.apparelDefinitions = apparelDefinitions
            ?? throw new ArgumentNullException(nameof(apparelDefinitions));
        this.materials = materials
            ?? throw new ArgumentNullException(nameof(materials));
        this.projector = projector
            ?? throw new ArgumentNullException(nameof(projector));
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
    }

    public ThermalProtectionProfile Resolve(CharacterActor actor)
    {
        ThermalProtectionProfile result = new();
        IReadOnlyList<CharacterTraitSO> traits =
            actor?.Progression?.ResolveSelectedTraits()
            ?? Array.Empty<CharacterTraitSO>();
        for (int i = 0; i < traits.Count; i++)
        {
            result.Add(traits[i]?.environmentalProtection);
        }

        CharacterId characterId = new(actor?.Identity?.PersistentId);
        if (characterId.IsValid)
        {
            result.Add(ResolveMaterialProtection(characterId));
        }
        if (characterId.IsValid
            && workwear.TryGetEquipped(
                characterId,
                out EnvironmentalWorkwearSO equipped))
        {
            result.Add(equipped.Protection);
        }

        return result;
    }

    private ThermalProtectionProfile ResolveMaterialProtection(
        CharacterId characterId)
    {
        IReadOnlyList<EquippedApparelSnapshot> equipped =
            apparel.GetEquipped(characterId);
        if (equipped.Count == 0)
        {
            return ThermalProtectionProfile.None;
        }

        IReadOnlyDictionary<string, WorldItemStackSnapshot> physicalItems =
            GetPhysicalItemsByInstance();

        float totalWarmth = 0f;
        float totalHeatResistance = 0f;
        HashSet<ItemInstanceId> projectedInstances = new();
        for (int index = 0; index < equipped.Count; index++)
        {
            EquippedApparelSnapshot entry = equipped[index];
            if (!entry.CharacterId.Equals(characterId)
                || !entry.ItemInstanceId.IsValid
                || !projectedInstances.Add(entry.ItemInstanceId)
                || !physicalItems.TryGetValue(
                    entry.ItemInstanceId.Value,
                    out WorldItemStackSnapshot stack)
                || stack.Quantity != 1
                || stack.State != WorldItemStackState.Carried
                || !string.Equals(
                    stack.DestinationId,
                    CharacterApparelAggregate.EquippedDestinationPrefix
                        + characterId.Value,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Equipped apparel '{entry.ItemInstanceId.Value}' has no exact physical ownership.");
            }

            if (!ApparelItemStateCodec.TryRead(
                    stack.Components,
                    out ApparelInstanceState state))
            {
                throw new InvalidOperationException(
                    $"Equipped apparel '{entry.ItemInstanceId.Value}' has no authored apparel component.");
            }
            if (!apparelDefinitions.TryGetByItemId(
                    stack.ItemId,
                    out ApparelDefinitionSO definition)
                || !string.Equals(
                    definition.ApparelId,
                    entry.ApparelDefinitionId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    definition.ApparelId,
                    state.apparelDefinitionId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Equipped apparel '{entry.ItemInstanceId.Value}' has inconsistent definition identity.");
            }
            if (!materials.TryGet(
                    state.primaryMaterialId,
                    out TextileMaterialDefinitionSO material))
            {
                throw new InvalidOperationException(
                    $"Equipped apparel '{entry.ItemInstanceId.Value}' references missing material '{state.primaryMaterialId}'.");
            }
            if (!anatomy.CanEquip(
                    characterId,
                    definition,
                    state,
                    out ApparelFitAssessment fit,
                    out _))
            {
                continue;
            }

            int apparelIndex = apparelDefinitions.GetIndex(definition.ApparelId);
            int materialIndex = materials.GetIndex(material.MaterialId);
            if (apparelIndex < 0 || materialIndex < 0)
            {
                throw new InvalidOperationException(
                    $"Equipped apparel '{entry.ItemInstanceId.Value}' is absent from its material projection catalog.");
            }
            ApparelDerivedStats projected = projector.GetOrCreate(
                new ApparelProjectionKey(
                    apparelIndex,
                    materialIndex,
                    state.craftsmanshipQuality,
                    ApparelMaterialProtectionRules.ResolveDurabilityBand(
                        state.durability),
                    TextileConditionRules.ResolveCondition(
                        state.moisture,
                        state.contamination),
                    fit.UnusedOpenings,
                    fit.AdjacentSize));
            totalWarmth += projected.Warmth;
            totalHeatResistance += projected.HeatResistance;
        }

        return ApparelMaterialProtectionRules.CreateThermalProfile(
            totalWarmth,
            totalHeatResistance);
    }

    private IReadOnlyDictionary<string, WorldItemStackSnapshot>
        GetPhysicalItemsByInstance()
    {
        int itemStackVersion = items.ItemStackVersion;
        if (cachedItemStackVersion == itemStackVersion)
        {
            return stackByInstance;
        }

        IReadOnlyList<WorldItemStackSnapshot> stacks = items.GetAllStacks();
        Dictionary<string, WorldItemStackSnapshot> rebuilt =
            new(StringComparer.Ordinal);
        for (int index = 0; index < stacks.Count; index++)
        {
            WorldItemStackSnapshot stack = stacks[index];
            if (stack == null || string.IsNullOrWhiteSpace(stack.ItemInstanceId))
            {
                continue;
            }
            if (!rebuilt.TryAdd(stack.ItemInstanceId, stack))
            {
                throw new InvalidOperationException(
                    $"Duplicate physical item instance '{stack.ItemInstanceId}'.");
            }
        }

        stackByInstance.Clear();
        foreach (KeyValuePair<string, WorldItemStackSnapshot> pair in rebuilt)
        {
            stackByInstance.Add(pair.Key, pair.Value);
        }
        cachedItemStackVersion = itemStackVersion;
        return stackByInstance;
    }
}
