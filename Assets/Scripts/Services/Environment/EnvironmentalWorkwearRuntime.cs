using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IEnvironmentalWorkwearRuntime
{
    int Version { get; }
    bool TryGetEquipped(
        string characterId,
        out EnvironmentalWorkwearSO workwear);
    int GetAvailableStock(string workwearId);
    bool TryAddStock(string workwearId, int amount, out string failureReason);
    bool TryEquip(
        CharacterActor actor,
        string workwearId,
        out string failureReason);
    bool TryAutoEquipForCold(
        CharacterActor actor,
        Vector2Int destination,
        out string failureReason);
    bool TryUnequip(string characterId, out string failureReason);
    IReadOnlyList<EnvironmentalWorkwearSaveData> CaptureEquipped();
    IReadOnlyList<EnvironmentalWorkwearStockSaveData> CaptureStock();
    void Restore(
        IReadOnlyList<EnvironmentalWorkwearSaveData> equipped,
        IReadOnlyList<EnvironmentalWorkwearStockSaveData> stock,
        DungeonGameRestoreReport report = null);
    void Reset();
}

public sealed class EnvironmentalWorkwearRuntime :
    IEnvironmentalWorkwearRuntime
{
    private readonly IEnvironmentalWorkwearCatalog catalog;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IBlueprintResearchStateService research;
    private readonly Dictionary<string, string> equippedByCharacter =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> stockByWorkwear =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public EnvironmentalWorkwearRuntime(
        IEnvironmentalWorkwearCatalog catalog,
        IBuildingWorldQuery buildingWorld,
        IBlueprintResearchStateService research = null)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.research = research;
    }

    public int Version { get; private set; }

    public bool TryGetEquipped(
        string characterId,
        out EnvironmentalWorkwearSO workwear)
    {
        workwear = null;
        return equippedByCharacter.TryGetValue(
                characterId?.Trim() ?? string.Empty,
                out string workwearId)
            && catalog.TryGet(workwearId, out workwear);
    }

    public int GetAvailableStock(string workwearId)
    {
        return stockByWorkwear.TryGetValue(
            workwearId?.Trim() ?? string.Empty,
            out int amount)
                ? Mathf.Max(0, amount)
                : 0;
    }

    public bool TryAddStock(
        string workwearId,
        int amount,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (amount <= 0
            || !catalog.TryGet(workwearId, out EnvironmentalWorkwearSO definition))
        {
            failureReason = "유효한 환경 작업복과 양이 필요합니다.";
            return false;
        }

        int capacity = GetLockerCapacity();
        int stored = stockByWorkwear.Values.Sum(value => Mathf.Max(0, value))
            + equippedByCharacter.Count;
        if (stored + amount > capacity)
        {
            failureReason =
                $"보호장비 보관함 용량이 부족합니다. {stored + amount}/{capacity}";
            return false;
        }

        stockByWorkwear[definition.WorkwearId] =
            GetAvailableStock(definition.WorkwearId) + amount;
        Touch();
        return true;
    }

    public bool TryEquip(
        CharacterActor actor,
        string workwearId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null
            || actor.Identity == null
            || string.IsNullOrWhiteSpace(actor.Identity.PersistentId))
        {
            failureReason = "작업복을 장착할 영속 캐릭터가 없습니다.";
            return false;
        }

        if (!catalog.TryGet(workwearId, out EnvironmentalWorkwearSO definition))
        {
            failureReason = $"환경 작업복 '{workwearId}'을 찾을 수 없습니다.";
            return false;
        }

        if (!definition.AllowsSpecies(actor.SpeciesTag))
        {
            failureReason =
                $"{definition.DisplayName}은(는) {actor.SpeciesTag} 종족이 착용할 수 없습니다.";
            return false;
        }

        if (!IsResearchUnlocked(definition))
        {
            failureReason =
                $"{definition.DisplayName} 연구가 완료되지 않았습니다.";
            return false;
        }

        string characterId = actor.Identity.PersistentId;
        if (TryGetEquipped(characterId, out EnvironmentalWorkwearSO equipped)
            && string.Equals(
                equipped.WorkwearId,
                definition.WorkwearId,
                StringComparison.Ordinal))
        {
            return true;
        }

        if (GetAvailableStock(definition.WorkwearId) <= 0)
        {
            failureReason =
                $"{definition.DisplayName} 재고가 보호장비 보관함에 없습니다.";
            return false;
        }

        if (TryGetEquipped(characterId, out EnvironmentalWorkwearSO previous))
        {
            stockByWorkwear[previous.WorkwearId] =
                GetAvailableStock(previous.WorkwearId) + 1;
        }

        stockByWorkwear[definition.WorkwearId] =
            GetAvailableStock(definition.WorkwearId) - 1;
        equippedByCharacter[characterId] = definition.WorkwearId;
        Touch();
        return true;
    }

    public bool TryAutoEquipForCold(
        CharacterActor actor,
        Vector2Int destination,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null)
        {
            failureReason = "방한 장비를 받을 캐릭터가 없습니다.";
            return false;
        }

        if (!HasReachableLocker(actor.GetNowXY(), destination))
        {
            failureReason = "작업 경로에 사용 가능한 보호장비 보관함이 없습니다.";
            return false;
        }

        EnvironmentalWorkwearSO best = catalog.Definitions
            .Where(candidate => candidate != null
                && candidate.AllowsSpecies(actor.SpeciesTag)
                && GetAvailableStock(candidate.WorkwearId) > 0
                && IsResearchUnlocked(candidate))
            .OrderBy(candidate =>
                candidate.Protection.comfortMinimumOffset)
            .ThenBy(candidate =>
                candidate.Protection.coldExposureMultiplier)
            .FirstOrDefault();
        if (best == null)
        {
            failureReason = "착용 가능한 방한 장비 재고가 없습니다.";
            return false;
        }

        return TryEquip(actor, best.WorkwearId, out failureReason);
    }

    public bool TryUnequip(
        string characterId,
        out string failureReason)
    {
        failureReason = string.Empty;
        string id = characterId?.Trim() ?? string.Empty;
        if (!equippedByCharacter.TryGetValue(id, out string workwearId))
        {
            failureReason = "장착 중인 환경 작업복이 없습니다.";
            return false;
        }

        if (stockByWorkwear.Values.Sum(value => Mathf.Max(0, value))
            >= GetLockerCapacity())
        {
            failureReason = "반납할 보호장비 보관함 공간이 없습니다.";
            return false;
        }

        equippedByCharacter.Remove(id);
        stockByWorkwear[workwearId] = GetAvailableStock(workwearId) + 1;
        Touch();
        return true;
    }

    public IReadOnlyList<EnvironmentalWorkwearSaveData> CaptureEquipped()
    {
        return equippedByCharacter
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new EnvironmentalWorkwearSaveData
            {
                characterId = pair.Key,
                workwearId = pair.Value
            })
            .ToArray();
    }

    public IReadOnlyList<EnvironmentalWorkwearStockSaveData> CaptureStock()
    {
        return stockByWorkwear
            .Where(pair => pair.Value > 0)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new EnvironmentalWorkwearStockSaveData
            {
                workwearId = pair.Key,
                amount = pair.Value
            })
            .ToArray();
    }

    public void Restore(
        IReadOnlyList<EnvironmentalWorkwearSaveData> equipped,
        IReadOnlyList<EnvironmentalWorkwearStockSaveData> stock,
        DungeonGameRestoreReport report = null)
    {
        equippedByCharacter.Clear();
        stockByWorkwear.Clear();
        foreach (EnvironmentalWorkwearStockSaveData entry in stock
                     ?? Array.Empty<EnvironmentalWorkwearStockSaveData>())
        {
            if (entry == null
                || entry.amount <= 0
                || !catalog.TryGet(entry.workwearId, out EnvironmentalWorkwearSO definition))
            {
                report?.AddWarning(
                    $"Invalid environmental workwear stock '{entry?.workwearId}' was ignored.");
                continue;
            }

            stockByWorkwear[definition.WorkwearId] = entry.amount;
        }

        foreach (EnvironmentalWorkwearSaveData entry in equipped
                     ?? Array.Empty<EnvironmentalWorkwearSaveData>())
        {
            if (entry == null
                || string.IsNullOrWhiteSpace(entry.characterId)
                || !catalog.TryGet(entry.workwearId, out EnvironmentalWorkwearSO definition))
            {
                report?.AddWarning(
                    $"Invalid equipped environmental workwear '{entry?.workwearId}' was ignored.");
                continue;
            }

            equippedByCharacter[entry.characterId.Trim()] =
                definition.WorkwearId;
        }

        Touch();
    }

    public void Reset()
    {
        equippedByCharacter.Clear();
        stockByWorkwear.Clear();
        Touch();
    }

    private bool IsResearchUnlocked(EnvironmentalWorkwearSO definition)
    {
        string researchId = definition.RequiredResearchId;
        return string.IsNullOrWhiteSpace(researchId)
            || research != null
            && research.GetState().Projects.IsCompleted(
                new ResearchProjectId(researchId));
    }

    private int GetLockerCapacity()
    {
        int capacity = 0;
        IReadOnlyList<BuildableObject> buildings =
            buildingWorld.Buildings ?? Array.Empty<BuildableObject>();
        for (int i = 0; i < buildings.Count; i++)
        {
            BuildableObject building = buildings[i];
            if (building == null || building.isDestroy)
            {
                continue;
            }

            BuildingProtectiveEquipmentLockerAbility locker =
                building.BuildingData
                    ?.GetAbility<BuildingProtectiveEquipmentLockerAbility>();
            capacity += locker?.capacity ?? 0;
        }

        return Mathf.Max(0, capacity);
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
            if (building == null
                || building.isDestroy
                || locker == null)
            {
                continue;
            }

            int fromOrigin = Mathf.Abs(building.centerPos.x - origin.x)
                + Mathf.Abs(building.centerPos.y - origin.y);
            int fromDestination =
                Mathf.Abs(building.centerPos.x - destination.x)
                + Mathf.Abs(building.centerPos.y - destination.y);
            if (Mathf.Min(fromOrigin, fromDestination) <= locker.serviceRadius)
            {
                return true;
            }
        }

        return false;
    }

    private void Touch()
    {
        unchecked
        {
            Version++;
        }
    }
}

public sealed class CharacterEnvironmentProtectionResolver :
    ICharacterEnvironmentProtectionResolver
{
    private readonly IEnvironmentalWorkwearRuntime workwear;

    public CharacterEnvironmentProtectionResolver(
        IEnvironmentalWorkwearRuntime workwear)
    {
        this.workwear = workwear
            ?? throw new ArgumentNullException(nameof(workwear));
    }

    public ThermalProtectionProfile Resolve(CharacterActor actor)
    {
        ThermalProtectionProfile result =
            new ThermalProtectionProfile();
        IReadOnlyList<CharacterTraitSO> traits =
            actor?.Progression?.ResolveSelectedTraits()
            ?? Array.Empty<CharacterTraitSO>();
        for (int i = 0; i < traits.Count; i++)
        {
            result.Add(traits[i]?.environmentalProtection);
        }

        string characterId = actor?.Identity?.PersistentId;
        if (!string.IsNullOrWhiteSpace(characterId)
            && workwear.TryGetEquipped(
                characterId,
                out EnvironmentalWorkwearSO equipped))
        {
            result.Add(equipped.Protection);
        }

        return result;
    }
}
