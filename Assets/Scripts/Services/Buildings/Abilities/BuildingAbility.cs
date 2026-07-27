using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class BuildingAbilityDisplayNameAttribute : Attribute
{
    public BuildingAbilityDisplayNameAttribute(string displayName)
    {
        DisplayName = displayName;
    }

    public string DisplayName { get; }
}

[Serializable]
public sealed class BuildingAbilityCollection
{
    [SerializeReference, SerializeField]
    private List<BuildingAbility> items = new List<BuildingAbility>();
    [NonSerialized] private IReadOnlyList<BuildingAbility> itemsView;

    public IReadOnlyList<BuildingAbility> Items
    {
        get
        {
            items ??= new List<BuildingAbility>();
            return itemsView ??= ReadOnlyView.List(items);
        }
    }
    public int Count => items?.Count ?? 0;

    public void Add(BuildingAbility ability)
    {
        if (ability == null)
        {
            throw new ArgumentNullException(nameof(ability));
        }

        items ??= new List<BuildingAbility>();
        Type abilityType = ability.GetType();
        if (!abilityType.IsSerializable)
        {
            throw new InvalidOperationException(
                $"Building ability '{abilityType.FullName}' must be marked Serializable.");
        }

        if (items.Any(candidate => candidate != null && candidate.GetType() == abilityType))
        {
            throw new InvalidOperationException(
                $"Building ability type '{abilityType.FullName}' is already registered.");
        }

        items.Add(ability);
    }

    public int RemoveNullEntries()
    {
        return items?.RemoveAll(ability => ability == null) ?? 0;
    }

    public int Remove<TAbility>()
        where TAbility : BuildingAbility
    {
        return items?.RemoveAll(ability => ability is TAbility) ?? 0;
    }

    public int EnsureStableIds()
    {
        int changed = 0;
        if (items == null)
        {
            return changed;
        }

        foreach (BuildingAbility ability in items)
        {
            if (ability != null && ability.EnsureStableId())
            {
                changed++;
            }
        }

        return changed;
    }

    public void ValidateOrThrow(string ownerDescription)
    {
        if (items == null)
        {
            return;
        }

        string owner = string.IsNullOrWhiteSpace(ownerDescription)
            ? "Building ability collection"
            : ownerDescription;
        HashSet<Type> types = new HashSet<Type>();
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < items.Count; index++)
        {
            BuildingAbility ability = items[index];
            if (ability == null)
            {
                throw new InvalidOperationException($"{owner} contains a null or missing ability at index {index}.");
            }

            Type abilityType = ability.GetType();
            if (!abilityType.IsSerializable)
            {
                throw new InvalidOperationException(
                    $"{owner} ability '{abilityType.FullName}' must be marked Serializable.");
            }

            if (!types.Add(abilityType))
            {
                throw new InvalidOperationException(
                    $"{owner} contains duplicate ability type '{abilityType.FullName}'.");
            }

            string abilityId = ability.AbilityId;
            if (string.IsNullOrWhiteSpace(abilityId))
            {
                throw new InvalidOperationException(
                    $"{owner} ability '{abilityType.FullName}' has no stable ability ID.");
            }

            if (abilityId.Contains(':'))
            {
                throw new InvalidOperationException(
                    $"{owner} ability '{abilityType.FullName}' ID '{abilityId}' cannot contain ':'.");
            }

            if (!ids.Add(abilityId))
            {
                throw new InvalidOperationException(
                    $"{owner} contains duplicate ability ID '{abilityId}'.");
            }
        }
    }

    public bool TryGet<TAbility>(out TAbility ability)
        where TAbility : BuildingAbility
    {
        if (items != null)
        {
            foreach (BuildingAbility candidate in items)
            {
                if (candidate is TAbility typed)
                {
                    ability = typed;
                    return true;
                }
            }
        }

        ability = null;
        return false;
    }
}

[Serializable]
public abstract class BuildingAbility
{
    [SerializeField, InspectorName("안정 능력 ID")]
    private string abilityId;

    protected BuildingAbility()
    {
        abilityId = GetType().Name;
    }

    public string AbilityId => abilityId?.Trim() ?? string.Empty;

    internal bool EnsureStableId()
    {
        if (!string.IsNullOrWhiteSpace(abilityId))
        {
            string normalized = abilityId.Trim();
            if (string.Equals(abilityId, normalized, StringComparison.Ordinal))
            {
                return false;
            }

            abilityId = normalized;
            return true;
        }

        abilityId = GetType().Name;
        return true;
    }
}

[Serializable]
[BuildingAbilityDisplayName("경제")]
public sealed class BuildingEconomyAbility : BuildingAbility
{
    [Min(0), InspectorName("건설 비용")] public int constructionCost;
    [Min(0), InspectorName("일일 유지비")] public int maintenance;
    [Range(1, 3), InspectorName("해금 단계")] public int unlockPhase = 1;
    [Range(0f, 1f), InspectorName("철거 환급률")] public float demolitionRefundRate = 0.5f;
}

[Serializable]
[BuildingAbilityDisplayName("시설 운영")]
public sealed class BuildingFacilityAbility : BuildingAbility
{
    [InspectorName("시설 설정")] public FacilityData settings = new FacilityData();
}

[Serializable]
[BuildingAbilityDisplayName("내부 재고")]
public sealed class BuildingInternalStockAbility : BuildingAbility
{
    [Min(0), InspectorName("최대 재고")] public int capacity;
    [Min(0), InspectorName("보충 요청 기준")] public int restockRequestThreshold;
}

[Serializable]
[BuildingAbilityDisplayName("이용 시 재고 필요")]
public sealed class BuildingRequiresStockAbility : BuildingAbility { }

[Serializable]
[BuildingAbilityDisplayName("직원 서비스 필요")]
public sealed class BuildingStaffedServiceAbility : BuildingAbility { }

[Serializable]
[BuildingAbilityDisplayName("정식 방 필요")]
public sealed class BuildingRoomRequirementAbility : BuildingAbility { }

[Serializable]
[BuildingAbilityDisplayName("독립 공간")]
public sealed class BuildingSelfContainedRoomAbility : BuildingAbility { }

[Serializable]
[BuildingAbilityDisplayName("종족 선호")]
public sealed class BuildingSpeciesAffinityAbility : BuildingAbility
{
    [InspectorName("선호 종족 태그")]
    public string[] preferredTags = Array.Empty<string>();
    [InspectorName("기피 종족 태그")]
    public string[] dislikedTags = Array.Empty<string>();

    public bool HasAnyTag => (preferredTags?.Length ?? 0) > 0
        || (dislikedTags?.Length ?? 0) > 0;

    public bool IsPreferred(string speciesTag)
    {
        return Contains(preferredTags, speciesTag);
    }

    public bool IsDisliked(string speciesTag)
    {
        return Contains(dislikedTags, speciesTag);
    }

    private static bool Contains(IEnumerable<string> tags, string speciesTag)
    {
        return !string.IsNullOrWhiteSpace(speciesTag)
            && tags != null
            && tags.Any(tag => string.Equals(tag, speciesTag, StringComparison.OrdinalIgnoreCase));
    }
}

[Serializable]
[BuildingAbilityDisplayName("의미 태그")]
public sealed class BuildingSemanticTagsAbility : BuildingAbility
{
    [InspectorName("태그 목록")]
    public string[] tags = Array.Empty<string>();
}

[Serializable]
[BuildingAbilityDisplayName("시설 등급")]
public sealed class BuildingQualityAbility : BuildingAbility
{
    [Range(1, 5), InspectorName("등급")]
    public int star = 1;
}

[Serializable]
[BuildingAbilityDisplayName("방어 시설")]
public sealed class BuildingDefenseAbility : BuildingAbility
{
    [InspectorName("방어 설정")] public DefenseFacilityData settings = new DefenseFacilityData();
}

[Serializable]
[BuildingAbilityDisplayName("엄폐")]
public sealed class BuildingCoverAbility :
    BuildingAbility,
    IBuildingRuntimeStateAbility,
    IBuildingVisualRuntimeAbility
{
    [InspectorName("엄폐 높이")]
    public CombatCoverHeight height = CombatCoverHeight.Low;
    [Range(0f, 1f), InspectorName("기본 차단 확률")]
    public float blockChance = 0.35f;
    [InspectorName("보호 방향")]
    public Vector2Int facingDirection = Vector2Int.left;
    [InspectorName("모서리 사격 허용")]
    public bool allowsCornerPeek;
    [Min(1f), InspectorName("엄폐 내구")]
    public float coverHitPoints = 60f;

    public IBuildingStateModule CreateStateModule(BuildableObject building)
    {
        return CombatCoverDurability.Ensure(building, this);
    }

    public void ConfigureVisual(BuildableObject building)
    {
        CombatCoverDurability.Ensure(building, this);
    }
}

[Serializable]
[BuildingAbilityDisplayName("진화 기여")]
public sealed class BuildingEvolutionAbility : BuildingAbility
{
    [InspectorName("진화 설정")]
    public FacilityEvolutionContributionData settings = new FacilityEvolutionContributionData();
}

[Serializable]
[BuildingAbilityDisplayName("욕구 회복")]
public sealed class BuildingNeedRecoveryAbility : BuildingAbility
{
    [InspectorName("회복 수치")] public FacilityNeedRecoveryData recovery;

    public bool HasEffect => recovery.HasEffect;
}

[Serializable]
[BuildingAbilityDisplayName("보관")]
public sealed class BuildingStorageAbility : BuildingAbility
{
    [InspectorName("품목 분류")] public StockCategory category = StockCategory.General;
    [Min(0), InspectorName("보관량")] public int capacity;
    [InspectorName("모든 품목 허용")] public bool allCategories;

    public bool IsValid => capacity > 0;
}

[Serializable]
[BuildingAbilityDisplayName("좌석")]
public sealed class BuildingSeatingAbility : BuildingAbility
{
    [Min(0), InspectorName("좌석 수")] public int capacity = 1;

    public bool IsValid => capacity > 0;
}

[Serializable]
[BuildingAbilityDisplayName("테이블")]
public sealed class BuildingTableAbility : BuildingAbility
{
    [Min(0), InspectorName("이용 인원")] public int capacity = 1;

    public bool IsValid => capacity > 0;
}

[Serializable]
[BuildingAbilityDisplayName("서비스")]
public sealed class BuildingServiceAbility : BuildingAbility, IBuildingStockCategorySignal
{
    [Min(0), InspectorName("동시 이용 인원")] public int capacity = 1;
    public bool contributesStockCategory;
    public StockCategory stockCategory = StockCategory.General;

    public bool IsValid => capacity > 0;

    public IEnumerable<StockCategory> GetStockCategorySignals()
    {
        if (contributesStockCategory)
        {
            yield return stockCategory;
        }
    }
}

[Serializable]
[BuildingAbilityDisplayName("생산")]
public sealed class BuildingProductionAbility : BuildingAbility,
    IBuildingWorkCompletionAbility,
    IBuildingRuntimeStateAbility,
    IBuildingStockCategorySignal
{
    [InspectorName("생산 품목")] public StockCategory outputCategory = StockCategory.General;
    [Min(0), InspectorName("생산량")] public int amount;

    public bool IsValid => amount > 0;

    public IBuildingStateModule CreateStateModule(BuildableObject building)
    {
        return new BuildingProductionStateModule(building, this);
    }

    public IEnumerable<StockCategory> GetStockCategorySignals()
    {
        if (IsValid)
        {
            yield return outputCategory;
        }
    }
}

[Serializable]
[BuildingAbilityDisplayName("조명")]
public sealed class BuildingLightingAbility : BuildingAbility, IBuildingVisualRuntimeAbility
{
    [InspectorName("공용 조명 설정")] public BuildingLightingSettingsSO settings;
    [Min(0f), InspectorName("빛 세기")] public float intensity = 0.75f;
    [Min(0f), InspectorName("빛 반경")] public float radius = 2.8f;

    public bool IsValid => intensity > 0f && radius > 0f;

    public float InnerRadiusRatio => settings != null
        ? settings.innerRadiusRatio
        : BuildingLightingSettingsSO.DefaultInnerRadiusRatio;

    public float FalloffIntensity => settings != null
        ? settings.falloffIntensity
        : BuildingLightingSettingsSO.DefaultFalloffIntensity;

    public Color Color => settings != null
        ? settings.color
        : BuildingLightingSettingsSO.DefaultColor;

    public int[] GetTargetSortingLayerIds()
    {
        string[] layerNames = settings != null
            && settings.targetSortingLayers != null
            && settings.targetSortingLayers.Length > 0
                ? settings.targetSortingLayers
                : BuildingLightingSettingsSO.DefaultTargetSortingLayers;
        return layerNames
            .Select(SortingLayer.NameToID)
            .Where(SortingLayer.IsValid)
            .ToArray();
    }

    public void ConfigureVisual(BuildableObject building)
    {
        ModularFacilityRuntimeEffects.ConfigureLighting(building, this);
    }
}

[Serializable]
[BuildingAbilityDisplayName("작업량")]
public sealed class BuildingWorkAmountAbility : BuildingAbility, IBuildingWorkAmountRuntimeAbility
{
    [Min(0.1f), InspectorName("건설 작업량")] public float constructionWorkRequired = 30f;
    [Min(0.1f), InspectorName("수리 작업량")] public float repairWorkRequired = 8f;
    [Min(0.1f), InspectorName("청소 작업량")] public float cleanWorkRequired = 6f;
    [Min(0.1f), InspectorName("연구 작업량")] public float researchWorkRequired = 6f;
    [Min(0.1f), InspectorName("기본 운영 작업량")] public float operateWorkRequired = 10f;
    [InspectorName("건설 재료 분류")] public StockCategory constructionMaterialCategory = StockCategory.General;
    [Min(0), InspectorName("건설 재료 수량")] public int constructionMaterialAmount;
    [Min(0f), InspectorName("비용 대비 기본 재료 비율")] public float materialUnitsPerConstructionCost = 0.05f;

    public float GetRequiredWork(BuildableObject building, WorkTypeId workTypeId)
    {
        return WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            ? GetConfiguredRequiredWork(definition.Type)
            : 0f;
    }

    private float GetConfiguredRequiredWork(FacilityWorkType workType)
    {
        return workType switch
        {
            FacilityWorkType.Construct => Mathf.Max(0.1f, constructionWorkRequired),
            FacilityWorkType.Repair => Mathf.Max(0.1f, repairWorkRequired),
            FacilityWorkType.Clean => Mathf.Max(0.1f, cleanWorkRequired),
            FacilityWorkType.Research => Mathf.Max(0.1f, researchWorkRequired),
            FacilityWorkType.Operate => Mathf.Max(0.1f, operateWorkRequired),
            _ => 0f
        };
    }

    public Dictionary<StockCategory, int> GetConstructionMaterials(BuildingSO building)
    {
        Dictionary<StockCategory, int> result = new Dictionary<StockCategory, int>();
        int amount = constructionMaterialAmount;
        if (amount <= 0 && building != null && materialUnitsPerConstructionCost > 0f)
        {
            amount = Mathf.CeilToInt(building.GetConstructionCost() * materialUnitsPerConstructionCost);
        }

        if (amount > 0)
        {
            result[constructionMaterialCategory] = amount;
        }

        return result;
    }
}

[Serializable]
[BuildingAbilityDisplayName("Equipment Maintenance")]
public sealed class BuildingEquipmentMaintenanceAbility : BuildingAbility
{
    [Min(0.1f)] public float workSpeedMultiplier = 1f;
    [Min(1)] public int simultaneousRepairSlots = 1;
}

[Serializable]
[BuildingAbilityDisplayName("Expedition Recovery")]
public sealed class BuildingExpeditionRecoveryAbility : BuildingAbility, IBuildingUseCompletedRuntimeAbility
{
    [Range(0f, 1f)] public float healthHealRatio = 0.2f;
    [Range(0f, 1f)] public float injuryReduction = 0.1f;
    [Min(0f)] public float stressRecovery = 25f;

    public void ApplyUseCompleted(CharacterActor actor, BuildableObject building)
    {
        actor?.Lifecycle?.ApplyExpeditionRecovery(
            healthHealRatio,
            injuryReduction,
            stressRecovery);
    }
}

[Serializable]
[BuildingAbilityDisplayName("Entrance Reception")]
public sealed class BuildingReceptionAbility : BuildingAbility,
    IBuildingExteriorWorkRuntimeAbility,
    IBuildingWorkCompletionAbility
{
    [Min(0.1f)] public float workSeconds = 1.2f;
    [Range(0f, 100f)] public float readinessGain = 35f;
    [Min(0f)] public float firstImpressionBonus = 4f;
    public float moodBonus = 1.5f;
    [Min(0f)] public float moodDurationSeconds = 120f;

    public bool SupportsExteriorWork(WorkTypeId workTypeId)
    {
        return workTypeId == BuiltInWorkTypeIds.Reception;
    }

    public bool IsExteriorWorkAvailable(CharacterActor actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return SupportsExteriorWork(workTypeId)
            && building is ExteriorZoneMarker marker
            && marker.CanRunReceptionWork;
    }

    public float GetExteriorWorkSeconds(CharacterActor actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return SupportsExteriorWork(workTypeId) ? Mathf.Max(0.1f, workSeconds) : 0f;
    }

    public float GetExteriorWorkUrgency(CharacterActor actor, BuildableObject building, WorkTypeId workTypeId)
    {
        if (!IsExteriorWorkAvailable(actor, building, workTypeId)
            || building is not ExteriorZoneMarker marker)
        {
            return 0f;
        }

        return marker.GetReceptionUrgency();
    }

}

[Serializable]
[BuildingAbilityDisplayName("Exterior Patrol")]
public sealed class BuildingPatrolPostAbility : BuildingAbility,
    IBuildingExteriorWorkRuntimeAbility,
    IBuildingWorkCompletionAbility
{
    [Min(0.1f)] public float workSeconds = 1.6f;
    [Range(0f, 100f)] public float patrolReadinessGain = 30f;
    [Range(0f, 1f)] public float incidentDetectionBonus = 0.15f;

    public bool SupportsExteriorWork(WorkTypeId workTypeId)
    {
        return workTypeId == BuiltInWorkTypeIds.Guard;
    }

    public bool IsExteriorWorkAvailable(CharacterActor actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return SupportsExteriorWork(workTypeId)
            && building is ExteriorZoneMarker marker
            && marker.CanRunPatrolWork;
    }

    public float GetExteriorWorkSeconds(CharacterActor actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return SupportsExteriorWork(workTypeId) ? Mathf.Max(0.1f, workSeconds) : 0f;
    }

    public float GetExteriorWorkUrgency(CharacterActor actor, BuildableObject building, WorkTypeId workTypeId)
    {
        if (!IsExteriorWorkAvailable(actor, building, workTypeId)
            || building is not ExteriorZoneMarker marker)
        {
            return 0f;
        }

        return marker.GetPatrolUrgency();
    }

}

[Serializable]
[BuildingAbilityDisplayName("Outdoor Rest")]
public sealed class BuildingOutdoorRestAbility : BuildingAbility,
    IBuildingExteriorWorkRuntimeAbility,
    IBuildingWorkCompletionAbility
{
    [Min(0.1f)] public float workSeconds = 1.4f;
    public float moodBonus = 4f;
    [Min(0f)] public float stressRecovery = 8f;
    [Min(0f)] public float moodDurationSeconds = 180f;

    public bool SupportsExteriorWork(WorkTypeId workTypeId)
    {
        return workTypeId == BuiltInWorkTypeIds.Rest;
    }

    public bool IsExteriorWorkAvailable(CharacterActor actor, BuildableObject building, WorkTypeId workTypeId)
    {
        if (!SupportsExteriorWork(workTypeId)
            || actor == null
            || building is not ExteriorZoneMarker marker
            || !marker.IsOutdoorRestSpot)
        {
            return false;
        }

        return actor.Mood.Value < 85f
            || (actor.Lifecycle != null && actor.Lifecycle.ExpeditionRecovery.stress > 0f);
    }

    public float GetExteriorWorkSeconds(CharacterActor actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return SupportsExteriorWork(workTypeId) ? Mathf.Max(0.1f, workSeconds) : 0f;
    }

    public float GetExteriorWorkUrgency(CharacterActor actor, BuildableObject building, WorkTypeId workTypeId)
    {
        if (!IsExteriorWorkAvailable(actor, building, workTypeId))
        {
            return 0f;
        }

        float moodNeed = Mathf.Clamp(85f - actor.Mood.Value, 0f, 85f);
        float stress = actor.Lifecycle != null ? actor.Lifecycle.ExpeditionRecovery.stress : 0f;
        return Mathf.Clamp(moodNeed * 0.75f + stress * 0.45f, 15f, 80f);
    }

}

[Serializable]
[BuildingAbilityDisplayName("Exterior Maintenance")]
public sealed class BuildingExteriorMaintenanceAbility : BuildingAbility,
    IBuildingExteriorWorkRuntimeAbility,
    IBuildingWorkCompletionAbility
{
    [Min(0.1f)] public float cleanWorkSeconds = 1.1f;
    [Min(0.1f)] public float repairWorkSeconds = 1.3f;
    [Range(0f, 100f)] public float cleanlinessGain = 35f;
    [Range(0f, 100f)] public float damageReduction = 35f;

    public bool SupportsExteriorWork(WorkTypeId workTypeId)
    {
        return workTypeId == BuiltInWorkTypeIds.Clean || workTypeId == BuiltInWorkTypeIds.Repair;
    }

    public bool IsExteriorWorkAvailable(CharacterActor actor, BuildableObject building, WorkTypeId workTypeId)
    {
        if (!SupportsExteriorWork(workTypeId) || building is not ExteriorZoneMarker marker)
        {
            return false;
        }

        return workTypeId == BuiltInWorkTypeIds.Clean
            ? marker.CanRunExteriorCleanWork
            : marker.CanRunExteriorRepairWork;
    }

    public float GetExteriorWorkSeconds(CharacterActor actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return workTypeId == BuiltInWorkTypeIds.Clean
            ? Mathf.Max(0.1f, cleanWorkSeconds)
            : SupportsExteriorWork(workTypeId)
                ? Mathf.Max(0.1f, repairWorkSeconds)
                : 0f;
    }

    public float GetExteriorWorkUrgency(CharacterActor actor, BuildableObject building, WorkTypeId workTypeId)
    {
        if (!IsExteriorWorkAvailable(actor, building, workTypeId)
            || building is not ExteriorZoneMarker marker)
        {
            return 0f;
        }

        return workTypeId == BuiltInWorkTypeIds.Clean
            ? marker.GetCleanUrgency()
            : marker.GetRepairUrgency();
    }

}

[Serializable]
[BuildingAbilityDisplayName("Facility Part")]
public sealed class BuildingFacilityPartAbility : BuildingAbility
{
    public string code;
    public bool IsValid => !string.IsNullOrWhiteSpace(code);
}

[Serializable]
[BuildingAbilityDisplayName("판매")]
public sealed class BuildingRetailAbility : BuildingAbility, IBuildingStockCategorySignal
{
    public StockCategory category = StockCategory.General;

    public IEnumerable<StockCategory> GetStockCategorySignals()
    {
        yield return category;
    }
}

[Serializable]
[BuildingAbilityDisplayName("훈련")]
public sealed class BuildingTrainingAbility : BuildingAbility, IBuildingUseCompletedRuntimeAbility
{
    public string moodLabel;
    public float moodAmount;
    [Min(0f)] public float durationSeconds = 180f;
    [Min(0)] public int experienceAmount = 24;

    public void ApplyUseCompleted(CharacterActor actor, BuildableObject building)
    {
        if (actor == null || building == null)
        {
            return;
        }

        actor.Progression?.AddExperience(Mathf.Max(0, experienceAmount));
        if (Mathf.Approximately(moodAmount, 0f))
        {
            return;
        }

        actor.ApplyMoodFactor(
            $"facility-training:{building.GetInstanceID()}:{AbilityId}",
            string.IsNullOrWhiteSpace(moodLabel) ? "훈련을 마침" : moodLabel,
            moodAmount,
            Mathf.Max(0f, durationSeconds),
            1);
    }
}

[Serializable]
[BuildingAbilityDisplayName("청소")]
public sealed class BuildingCleaningAbility :
    BuildingAbility,
    IBuildingWorkCompletionAbility
{
    [Range(0f, 100f)] public float restoredCleanliness = 100f;

}

[Serializable]
[BuildingAbilityDisplayName("경비")]
public sealed class BuildingSecurityAbility : BuildingAbility,
    IBuildingWorkCompletionAbility,
    IBuildingRuntimeStateAbility
{
    [Min(1)] public int maxAlarmCharges = 3;
    [Min(1)] public int chargesPerGuardWork = 1;

    public IBuildingStateModule CreateStateModule(BuildableObject building)
    {
        return new BuildingSecurityStateModule(building, this);
    }

}

[Serializable]
[BuildingAbilityDisplayName("범죄 위험 보정")]
public sealed class BuildingCrimeRiskModifierAbility : BuildingAbility, IBuildingCrimeRiskModifier
{
    [Min(0f), InspectorName("위험 배율")] public float multiplier = 1f;
    [Range(-1f, 1f), InspectorName("고정 위험 보정")] public float flatOffset;

    public float ModifyCrimePressure(float pressure, FacilityCrimeRiskContext context)
    {
        return Mathf.Max(0f, (Mathf.Max(0f, pressure) * multiplier) + flatOffset);
    }
}
