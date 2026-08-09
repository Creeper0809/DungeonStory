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
    private readonly IStockQuery stock;

    public EnvironmentalWorkwearRuntime(
        IEnvironmentalWorkwearCatalog catalog,
        IBuildingWorldQuery buildingWorld,
        IBlueprintResearchStateService research,
        ICharacterApparelQuery apparel,
        ICharacterApparelCommand apparelCommands,
        IWorldItemStackRuntime items,
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
        itemInstanceId = default;
        workwear = null;
        foreach (EquippedApparelSnapshot equipped in apparel.GetEquipped(characterId))
        {
            if (!TryFindPhysicalItem(equipped.ItemInstanceId, out WorldItemStackSnapshot stack)
                || !catalog.TryGetByItemDefinitionId(stack.ItemId, out workwear))
            {
                continue;
            }

            itemInstanceId = equipped.ItemInstanceId;
            return true;
        }

        return false;
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

        CharacterId characterId = new(actor.Identity.PersistentId);
        if (!characterId.IsValid)
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentWorkwearCharacterMissing);
            return false;
        }
        if (TryGetEquippedItemInstance(
                characterId,
                out _,
                out EnvironmentalWorkwearSO current)
            && string.Equals(
                current.WorkwearId,
                definition.WorkwearId,
                StringComparison.Ordinal))
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

        return apparelCommands.TryPlanChange(
                characterId,
                candidateId,
                out ApparelChangePlan plan,
                out failure)
            && apparelCommands.TryCommitChange(plan, out failure);
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
                && GetAvailableStock(candidate.WorkwearId) > 0
                && IsResearchUnlocked(candidate))
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

        return TryEquip(actor, best.WorkwearId, out failure);
    }

    public bool TryUnequip(CharacterId characterId, out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!TryGetEquippedItemInstance(
                characterId,
                out ItemInstanceId itemInstanceId,
                out _))
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentWorkwearNotEquipped,
                characterId.Value);
            return false;
        }
        return apparelCommands.TryUnequip(characterId, itemInstanceId, out failure);
    }

    public IReadOnlyList<EnvironmentalWorkwearSaveData> CaptureEquipped()
    {
        return apparel.GetAllEquipped()
            .Where(value => value.ItemInstanceId.IsValid
                && TryFindPhysicalItem(value.ItemInstanceId, out WorldItemStackSnapshot stack)
                && catalog.TryGetByItemDefinitionId(stack.ItemId, out _))
            .GroupBy(value => value.CharacterId)
            .Select(group => group
                .OrderBy(value => value.ItemInstanceId.Value, StringComparer.Ordinal)
                .First())
            .OrderBy(value => value.CharacterId.Value, StringComparer.Ordinal)
            .Select(value => new EnvironmentalWorkwearSaveData
            {
                characterId = value.CharacterId.Value,
                itemInstanceId = value.ItemInstanceId.Value
            })
            .ToArray();
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
                || !TryFindPhysicalItem(instanceId, out WorldItemStackSnapshot stack)
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
            && !stack.IsReserved
            && !stack.Forbidden
            && stack.State is WorldItemStackState.Loose
                or WorldItemStackState.Stored
                or WorldItemStackState.FacilityOutputBuffer
            && !(stack.DestinationId ?? string.Empty).StartsWith(
                CharacterApparelAggregate.EquippedDestinationPrefix,
                StringComparison.Ordinal);
    }

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

}

public sealed class CharacterEnvironmentProtectionResolver :
    ICharacterEnvironmentProtectionResolver
{
    private readonly IEnvironmentalWorkwearQuery workwear;

    public CharacterEnvironmentProtectionResolver(
        IEnvironmentalWorkwearQuery workwear)
    {
        this.workwear = workwear
            ?? throw new ArgumentNullException(nameof(workwear));
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
        if (characterId.IsValid
            && workwear.TryGetEquipped(
                characterId,
                out EnvironmentalWorkwearSO equipped))
        {
            result.Add(equipped.Protection);
        }

        return result;
    }
}
