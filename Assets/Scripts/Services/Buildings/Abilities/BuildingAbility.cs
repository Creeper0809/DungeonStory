using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


[Serializable]
[BuildingAbilityDisplayName("시설 운영")]
public sealed class BuildingFacilityAbility : BuildingAbility
{
    [InspectorName("시설 설정")] public FacilityData settings = new FacilityData();
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
        return CombatCoverDurability.Ensure(
            building,
            this,
            RequireCombatCoverDurabilityRegistry(building));
    }

    public void ConfigureVisual(BuildableObject building)
    {
        CombatCoverDurability.Ensure(
            building,
            this,
            RequireCombatCoverDurabilityRegistry(building));
    }

    private static ICombatCoverDurabilityRegistry
        RequireCombatCoverDurabilityRegistry(BuildableObject building)
    {
        return building.RequireCoverDurabilityRegistry()
            as ICombatCoverDurabilityRegistry
            ?? throw new InvalidOperationException(
                $"{nameof(BuildingCoverAbility)} requires a combat cover durability adapter.");
    }
}

[Serializable]
[BuildingAbilityDisplayName("구조 내구도")]
public sealed class BuildingStructuralIntegrityAbility :
    BuildingAbility,
    IBuildingRuntimeStateAbility,
    IBuildingVisualRuntimeAbility
{
    [Min(1f), InspectorName("최대 내구도")]
    public float maxHitPoints = 300f;
    [Min(0f), InspectorName("구조 강도")]
    public float toughness = 18f;
    [Min(0.01f), InspectorName("작업량당 수리량")]
    public float repairHitPointsPerWork = 2f;
    [InspectorName("파괴 가능")]
    public bool breachable = true;

    public IBuildingStateModule CreateStateModule(BuildableObject building)
    {
        return BuildingStructuralIntegrity.Ensure(building, this);
    }

    public void ConfigureVisual(BuildableObject building)
    {
        BuildingStructuralIntegrity.Ensure(building, this);
    }
}

[Serializable]
[BuildingAbilityDisplayName("시설 진화 기여")]
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
public sealed class BuildingWorkAmountAbility : BuildingAbility,
    IBuildingWorkAmountRuntimeAbility,
    IBuildingConstructionMaterialValidator
{
    [Min(0.1f), InspectorName("건설 작업량")] public float constructionWorkRequired = 30f;
    [Min(0.1f), InspectorName("수리 작업량")] public float repairWorkRequired = 8f;
    [Min(0.1f), InspectorName("청소 작업량")] public float cleanWorkRequired = 6f;
    [Min(0.1f), InspectorName("연구 작업량")] public float researchWorkRequired = 6f;
    [Min(0.1f), InspectorName("기본 운영 작업량")] public float operateWorkRequired = 10f;
    [SerializeField, InspectorName("건설 재료")]
    private WorkerSelectionPolicySaveData defaultWorkerPolicy =
        WorkerSelectionPolicySaveData.Anyone(
            WorkerCandidateSortMode.SpecificThenBestExpectedQuality);
    [SerializeField]
    private List<ItemAmountDefinition> constructionMaterials =
        new List<ItemAmountDefinition>();
    [NonSerialized] private IReadOnlyList<ItemAmountDefinition> constructionMaterialsView;

    public IReadOnlyList<ItemAmountDefinition> ConstructionMaterials
    {
        get
        {
            constructionMaterials ??= new List<ItemAmountDefinition>();
            return constructionMaterialsView ??=
                ReadOnlyView.List(constructionMaterials);
        }
    }

    public WorkerSelectionPolicySaveData DefaultWorkerPolicy =>
        defaultWorkerPolicy?.CloneNormalized()
        ?? WorkerSelectionPolicySaveData.Anyone(
            WorkerCandidateSortMode.SpecificThenBestExpectedQuality);

    public float GetRequiredWork(BuildableObject building, WorkTypeId workTypeId)
    {
        return WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            ? GetConfiguredRequiredWork(FacilityWorkTypeMap.GetRequired(definition))
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

    public IReadOnlyList<ItemAmountDefinition> GetConstructionMaterials()
    {
        ValidateConstructionMaterialsOrThrow();
        return ConstructionMaterials;
    }

#if UNITY_EDITOR
    public void SetConstructionMaterials(
        IEnumerable<ItemAmountDefinition> materials)
    {
        ItemAmountDefinition[] authored =
            (materials ?? Array.Empty<ItemAmountDefinition>()).ToArray();
        if (authored.Any(material => material == null))
        {
            throw new InvalidOperationException(
                "Construction materials cannot contain null entries.");
        }

        constructionMaterials = authored
            .Select(material =>
                new ItemAmountDefinition(material.ItemId, material.Amount))
            .ToList();
        constructionMaterialsView = null;
        ValidateConstructionMaterialsOrThrow();
    }
#endif

    public void ValidateConstructionMaterialsOrThrow(
        Func<string, bool> itemDefinitionExists = null)
    {
        if (ConstructionMaterials.Count == 0)
        {
            throw new InvalidOperationException(
                "Construction materials require at least one authored item.");
        }

        HashSet<string> itemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (ItemAmountDefinition material in ConstructionMaterials)
        {
            string itemId = material?.ItemId ?? string.Empty;
            if (material == null
                || !material.HasCanonicalAuthoredValue
                || itemId.StartsWith("stock-item:", StringComparison.Ordinal)
                || !itemIds.Add(itemId))
            {
                throw new InvalidOperationException(
                    $"Construction materials contain an invalid, abstract, or duplicate item ID '{itemId}'.");
            }

            if (itemDefinitionExists != null && !itemDefinitionExists(itemId))
            {
                throw new InvalidOperationException(
                    $"Construction material '{itemId}' has no authored item definition.");
            }
        }
    }
}


[Serializable]
[BuildingAbilityDisplayName("Expedition Recovery")]
public sealed class BuildingExpeditionRecoveryAbility : BuildingAbility, IBuildingUseCompletedRuntimeAbility
{
    [Range(0f, 1f)] public float healthHealRatio = 0.2f;
    [Range(0f, 1f)] public float injuryReduction = 0.1f;
    [Min(0f)] public float stressRecovery = 25f;

    public void ApplyUseCompleted(IBuildingVisitorPort actor, BuildableObject building)
    {
        actor?.ApplyExpeditionRecovery(
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

    public bool IsExteriorWorkAvailable(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return SupportsExteriorWork(workTypeId)
            && building is ExteriorZoneMarker marker
            && marker.CanRunReceptionWork;
    }

    public float GetExteriorWorkSeconds(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return SupportsExteriorWork(workTypeId) ? Mathf.Max(0.1f, workSeconds) : 0f;
    }

    public float GetExteriorWorkUrgency(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
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

    public bool IsExteriorWorkAvailable(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return SupportsExteriorWork(workTypeId)
            && building is ExteriorZoneMarker marker
            && marker.CanRunPatrolWork;
    }

    public float GetExteriorWorkSeconds(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return SupportsExteriorWork(workTypeId) ? Mathf.Max(0.1f, workSeconds) : 0f;
    }

    public float GetExteriorWorkUrgency(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
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

    public bool IsExteriorWorkAvailable(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        if (!SupportsExteriorWork(workTypeId)
            || actor == null
            || building is not ExteriorZoneMarker marker
            || !marker.IsOutdoorRestSpot)
        {
            return false;
        }

        BuildingVisitorSnapshot visitor = actor.VisitorSnapshot;
        return visitor.Mood < 85f || visitor.ExpeditionStress > 0f;
    }

    public float GetExteriorWorkSeconds(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return SupportsExteriorWork(workTypeId) ? Mathf.Max(0.1f, workSeconds) : 0f;
    }

    public float GetExteriorWorkUrgency(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        if (!IsExteriorWorkAvailable(actor, building, workTypeId))
        {
            return 0f;
        }

        BuildingVisitorSnapshot visitor = actor.VisitorSnapshot;
        float moodNeed = Mathf.Clamp(85f - visitor.Mood, 0f, 85f);
        float stress = visitor.ExpeditionStress;
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

    public bool IsExteriorWorkAvailable(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        if (!SupportsExteriorWork(workTypeId) || building is not ExteriorZoneMarker marker)
        {
            return false;
        }

        return workTypeId == BuiltInWorkTypeIds.Clean
            ? marker.CanRunExteriorCleanWork
            : marker.CanRunExteriorRepairWork;
    }

    public float GetExteriorWorkSeconds(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return workTypeId == BuiltInWorkTypeIds.Clean
            ? Mathf.Max(0.1f, cleanWorkSeconds)
            : SupportsExteriorWork(workTypeId)
                ? Mathf.Max(0.1f, repairWorkSeconds)
                : 0f;
    }

    public float GetExteriorWorkUrgency(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
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
[BuildingAbilityDisplayName("훈련")]
public sealed class BuildingTrainingAbility : BuildingAbility, IBuildingUseCompletedRuntimeAbility
{
    public string moodLabel;
    public float moodAmount;
    [Min(0f)] public float durationSeconds = 180f;
    [Min(0)] public int experienceAmount = 24;

    public void ApplyUseCompleted(IBuildingVisitorPort actor, BuildableObject building)
    {
        if (actor == null || building == null)
        {
            return;
        }

        actor.AddExperience(Mathf.Max(0, experienceAmount));
        if (Mathf.Approximately(moodAmount, 0f))
        {
            return;
        }

        actor.ApplyMoodFactor(
            $"facility-training:{building.RequirePersistentInstanceId().Value}:{AbilityId}",
            string.IsNullOrWhiteSpace(moodLabel) ? "훈련을 마침" : moodLabel,
            moodAmount,
            Mathf.Max(0f, durationSeconds),
            1);
    }
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
