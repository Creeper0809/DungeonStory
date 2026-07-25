using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CombatCoverDurabilitySaveData
{
    public float currentHitPoints;
}

public sealed class CombatCoverDurability : MonoBehaviour, IBuildingStateModule
{
    private static readonly Dictionary<string, CombatCoverDurability> BySourceId =
        new Dictionary<string, CombatCoverDurability>(StringComparer.Ordinal);

    private BuildableObject building;
    private BuildingCoverAbility ability;
    private float currentHitPoints;
    private bool initialized;

    public string SourceId => $"cover:{GetInstanceID()}";
    public float MaxHitPoints => Mathf.Max(1f, ability?.coverHitPoints ?? 1f);
    public float CurrentHitPoints => Mathf.Clamp(currentHitPoints, 0f, MaxHitPoints);
    public float DurabilityRatio => CurrentHitPoints / MaxHitPoints;
    public string ModuleId => BuildingStateModuleIds.ForAbility(
        "cover",
        ability?.AbilityId ?? nameof(BuildingCoverAbility));
    public int CurrentVersion => 1;

    public static CombatCoverDurability Ensure(
        BuildableObject building,
        BuildingCoverAbility ability)
    {
        if (building == null)
        {
            throw new ArgumentNullException(nameof(building));
        }

        CombatCoverDurability runtime = building.GetComponent<CombatCoverDurability>();
        if (runtime == null)
        {
            runtime = building.gameObject.AddComponent<CombatCoverDurability>();
        }

        runtime.Configure(building, ability);
        return runtime;
    }

    public static bool TryApplyDamage(string sourceId, float damage)
    {
        return !string.IsNullOrWhiteSpace(sourceId)
            && BySourceId.TryGetValue(sourceId, out CombatCoverDurability runtime)
            && runtime != null
            && runtime.ApplyDamage(damage);
    }

    public bool ApplyDamage(float damage)
    {
        if (damage <= 0f || building == null || building.isDestroy)
        {
            return false;
        }

        currentHitPoints = Mathf.Max(0f, currentHitPoints - damage);
        building.SetDamaged(DurabilityRatio <= 0.5f);
        if (currentHitPoints <= 0f)
        {
            BySourceId.Remove(SourceId);
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
            error = $"지원하지 않는 엄폐 상태 버전 {version}";
            return false;
        }

        CombatCoverDurabilitySaveData save = JsonUtility.FromJson<CombatCoverDurabilitySaveData>(payload);
        if (save == null)
        {
            error = "엄폐 내구 데이터가 없습니다.";
            return false;
        }

        currentHitPoints = Mathf.Clamp(save.currentHitPoints, 0f, MaxHitPoints);
        initialized = true;
        building?.SetDamaged(DurabilityRatio <= 0.5f);
        error = string.Empty;
        return true;
    }

    private void Configure(BuildableObject owner, BuildingCoverAbility sourceAbility)
    {
        building = owner;
        ability = sourceAbility ?? throw new ArgumentNullException(nameof(sourceAbility));
        if (!initialized)
        {
            currentHitPoints = MaxHitPoints;
            initialized = true;
        }

        BySourceId[SourceId] = this;
    }

    private void OnEnable()
    {
        BySourceId[SourceId] = this;
    }

    private void OnDisable()
    {
        if (BySourceId.TryGetValue(SourceId, out CombatCoverDurability current)
            && ReferenceEquals(current, this))
        {
            BySourceId.Remove(SourceId);
        }
    }
}
