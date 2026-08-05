using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CombatCoverDurabilitySaveData
{
    public float currentHitPoints;
}

public interface ICombatCoverDurabilityRegistry : IBuildingCoverDurabilityPort
{
    void Register(CombatCoverDurability durability);
    void Unregister(CombatCoverDurability durability);
}

public sealed class CombatCoverDurabilityRegistry :
    ICombatCoverDurabilityRegistry
{
    private readonly Dictionary<string, CombatCoverDurability> bySourceId =
        new Dictionary<string, CombatCoverDurability>(StringComparer.Ordinal);

    public void Register(CombatCoverDurability durability)
    {
        if (durability == null || string.IsNullOrWhiteSpace(durability.SourceId)) return;
        bySourceId[durability.SourceId] = durability;
    }

    public void Unregister(CombatCoverDurability durability)
    {
        if (durability == null || string.IsNullOrWhiteSpace(durability.SourceId)) return;
        if (bySourceId.TryGetValue(durability.SourceId, out CombatCoverDurability current)
            && ReferenceEquals(current, durability))
        {
            bySourceId.Remove(durability.SourceId);
        }
    }

    public bool TryApplyDamage(string sourceId, float damage)
    {
        if (string.IsNullOrWhiteSpace(sourceId)
            || !bySourceId.TryGetValue(sourceId, out CombatCoverDurability durability)
            || durability == null)
        {
            return false;
        }

        return durability.ApplyDamage(damage);
    }
}

public sealed class CombatCoverDurability : MonoBehaviour, IBuildingStateModule
{
    private BuildableObject building;
    private BuildingCoverAbility ability;
    private ICombatCoverDurabilityRegistry registry;
    private float currentHitPoints;
    private bool initialized;

    public string SourceId => building == null
        ? string.Empty
        : $"cover:{building.RequirePersistentInstanceId().Value}";
    public float MaxHitPoints => Mathf.Max(1f, ability?.coverHitPoints ?? 1f);
    public float CurrentHitPoints => Mathf.Clamp(currentHitPoints, 0f, MaxHitPoints);
    public float DurabilityRatio => CurrentHitPoints / MaxHitPoints;
    public string ModuleId => BuildingStateModuleIds.ForAbility(
        "cover",
        ability?.AbilityId ?? nameof(BuildingCoverAbility));
    public int CurrentVersion => 1;

    public static CombatCoverDurability Ensure(
        BuildableObject building,
        BuildingCoverAbility ability,
        ICombatCoverDurabilityRegistry registry)
    {
        if (building == null) throw new ArgumentNullException(nameof(building));
        CombatCoverDurability runtime =
            building.GetComponent<CombatCoverDurability>()
            ?? building.gameObject.AddComponent<CombatCoverDurability>();
        runtime.Configure(building, ability, registry);
        return runtime;
    }

    public bool ApplyDamage(float damage)
    {
        if (damage <= 0f || building == null || building.isDestroy) return false;
        currentHitPoints = Mathf.Max(0f, currentHitPoints - damage);
        building.SetDamaged(DurabilityRatio <= 0.5f);
        if (currentHitPoints <= 0f)
        {
            registry.Unregister(this);
            building.DestroySelf();
        }
        return true;
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(new CombatCoverDurabilitySaveData
        {
            currentHitPoints = CurrentHitPoints
        });
    }

    public bool TryRestoreState(int version, string payload, out string error)
    {
        if (version != CurrentVersion)
        {
            error = $"Unsupported cover state version {version}.";
            return false;
        }
        CombatCoverDurabilitySaveData save =
            JsonUtility.FromJson<CombatCoverDurabilitySaveData>(payload);
        if (save == null)
        {
            error = "Cover restore data is missing.";
            return false;
        }
        currentHitPoints = Mathf.Clamp(save.currentHitPoints, 0f, MaxHitPoints);
        initialized = true;
        building?.SetDamaged(DurabilityRatio <= 0.5f);
        error = string.Empty;
        return true;
    }

    private void Configure(
        BuildableObject owner,
        BuildingCoverAbility sourceAbility,
        ICombatCoverDurabilityRegistry registry)
    {
        this.registry?.Unregister(this);
        building = owner;
        ability = sourceAbility ?? throw new ArgumentNullException(nameof(sourceAbility));
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        if (!initialized)
        {
            currentHitPoints = MaxHitPoints;
            initialized = true;
        }
        registry.Register(this);
    }

    private void OnEnable()
    {
        if (building != null && building.PersistentInstanceId.IsValid) registry?.Register(this);
    }

    private void OnDisable()
    {
        registry?.Unregister(this);
    }
}
