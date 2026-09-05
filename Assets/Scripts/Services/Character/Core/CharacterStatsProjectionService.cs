using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct CharacterStatsProjectionContext
{
    public CharacterStatsProjectionContext(
        CharacterActor actor,
        CharacterIdentity identity,
        float sleep,
        float injurySeverity)
    {
        Actor = actor;
        Identity = identity;
        Sleep = sleep;
        InjurySeverity = injurySeverity;
    }

    public CharacterActor Actor { get; }
    public CharacterIdentity Identity { get; }
    public float Sleep { get; }
    public float InjurySeverity { get; }
    public CharacterRuntimeProfile EffectiveProfile =>
        Actor?.Progression != null
            ? Actor.Progression.GetEffectiveRuntimeProfile()
            : Identity?.Profile;
}

public static class CharacterEquipmentBurdenWorkSpeedAuthority
{
    public const string Schema =
        "character-equipment-burden-work-speed-authority@1";
    public const float MinimumFunctionalCapacityKilograms = 8f;
    public const float MinimumPositiveCapacityKilograms = 0.0001f;
    public const float OverloadThresholdRatio = 0.5f;
    public const float OverloadPenaltyPerRatio = 0.35f;
    public const float MinimumMultiplier = 0.45f;
    public const float MaximumMultiplier = 1f;

    public static float Resolve(
        float equippedWeightKilograms,
        float physicalMobility,
        float haulCapacityMultiplier)
    {
        if (!Finite(equippedWeightKilograms)
            || equippedWeightKilograms < 0f
            || !Finite(physicalMobility)
            || !Finite(haulCapacityMultiplier)
            || haulCapacityMultiplier <= 0f)
        {
            throw new InvalidOperationException(
                "Equipment burden inputs must be finite with positive haul "
                + "capacity.");
        }
        float capacity = Mathf.Max(
            MinimumPositiveCapacityKilograms,
            Mathf.Max(
                MinimumFunctionalCapacityKilograms,
                CharacterCarryTuning.NominalBaseCapacityKilograms
                    * physicalMobility)
            * haulCapacityMultiplier);
        float overload = Mathf.Max(
            0f,
            equippedWeightKilograms / capacity - OverloadThresholdRatio);
        return Mathf.Clamp(
            1f - overload * OverloadPenaltyPerRatio,
            MinimumMultiplier,
            MaximumMultiplier);
    }

    private static bool Finite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}

public static class CharacterFatigueWorkSpeedAuthority
{
    public const string Schema = "character-fatigue-work-speed-authority@1";
    public const float MinimumMultiplier = 0.65f;
    public const float MaximumMultiplier = 1f;
    public const float RestedStatValue = 100f;

    public static float Resolve(float sleep) => Mathf.Lerp(
        MinimumMultiplier,
        MaximumMultiplier,
        Mathf.Clamp01(sleep / RestedStatValue));
}

public static class CharacterExposureWorkSpeedAuthority
{
    public const string Schema = "character-exposure-work-speed-authority@1";
    public const float MaximumMultiplier = 1f;

    public static bool UsesPrecisionProjection(WorkTypeId workTypeId)
    {
        string id = workTypeId.Value ?? string.Empty;
        return id.IndexOf("research", StringComparison.OrdinalIgnoreCase) >= 0
            || id.IndexOf("craft", StringComparison.OrdinalIgnoreCase) >= 0
            || id.IndexOf("medical", StringComparison.OrdinalIgnoreCase) >= 0
            || id.IndexOf("treat", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

/// <summary>
/// Calculates derived character performance from authoritative character state
/// and injected domain policies. It stores no character state.
/// </summary>
public sealed class CharacterStatsProjectionService
{
    private readonly IStaffDiscontentRuntimeService staffDiscontent;
    private readonly IMetaProgressionRuntimeReader metaProgression;
    private readonly ICharacterDeprivationQuery deprivation;
    private readonly ICharacterSubstanceRuntime substances;
    private readonly ICharacterEnvironmentStatusQuery environmentStatus;
    private readonly IExternalCombatInfluenceQuery externalCombatInfluence;
    private readonly IContentWorkDelayQuery contentWorkDelays;
    private readonly IDiseaseSymptomEffectQuery diseaseSymptoms;
    private readonly ICharacterCombatSpecialStatusQuery combatStatuses;
    private readonly ICombatEquipmentBurdenQuery equipmentBurden;
    private readonly IGameCalendar calendar;
    private readonly CharacterDerivedStatsSnapshotProjector sharedEffects;
    private readonly IResourceStockPolicyQuery stockPolicies;
    private readonly ICharacterRitualFastingQuery ritualFasting;
    private readonly ICharacterPerformanceQuery performance;

    public CharacterStatsProjectionService(
        IStaffDiscontentRuntimeService staffDiscontent,
        IMetaProgressionRuntimeReader metaProgression,
        ICharacterDeprivationQuery deprivation,
        ICharacterSubstanceRuntime substances,
        ICharacterEnvironmentStatusQuery environmentStatus,
        IExternalCombatInfluenceQuery externalCombatInfluence,
        IContentWorkDelayQuery contentWorkDelays,
        IDiseaseSymptomEffectQuery diseaseSymptoms,
        ICharacterCombatSpecialStatusQuery combatStatuses,
        ICombatEquipmentBurdenQuery equipmentBurden,
        IGameCalendar calendar,
        CharacterDerivedStatsSnapshotProjector sharedEffects,
        IResourceStockPolicyQuery stockPolicies = null,
        ICharacterRitualFastingQuery ritualFasting = null,
        ICharacterPerformanceQuery performance = null)
    {
        this.staffDiscontent = staffDiscontent
            ?? throw new ArgumentNullException(nameof(staffDiscontent));
        this.metaProgression = metaProgression
            ?? throw new ArgumentNullException(nameof(metaProgression));
        this.deprivation = deprivation
            ?? throw new ArgumentNullException(nameof(deprivation));
        this.substances = substances
            ?? throw new ArgumentNullException(nameof(substances));
        this.environmentStatus = environmentStatus
            ?? throw new ArgumentNullException(nameof(environmentStatus));
        this.externalCombatInfluence = externalCombatInfluence
            ?? throw new ArgumentNullException(nameof(externalCombatInfluence));
        this.contentWorkDelays = contentWorkDelays
            ?? throw new ArgumentNullException(nameof(contentWorkDelays));
        this.diseaseSymptoms = diseaseSymptoms
            ?? throw new ArgumentNullException(nameof(diseaseSymptoms));
        this.combatStatuses = combatStatuses
            ?? throw new ArgumentNullException(nameof(combatStatuses));
        this.equipmentBurden = equipmentBurden
            ?? throw new ArgumentNullException(nameof(equipmentBurden));
        this.calendar = calendar
            ?? throw new ArgumentNullException(nameof(calendar));
        this.sharedEffects = sharedEffects
            ?? throw new ArgumentNullException(nameof(sharedEffects));
        this.stockPolicies = stockPolicies;
        this.ritualFasting = ritualFasting;
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
    }

    public float GetMoveSpeed(CharacterStatsProjectionContext context)
    {
        if (context.Actor == null)
            throw new InvalidOperationException("Movement performance requires a character actor.");
        if (performance == null)
            throw new InvalidOperationException(
                "Character performance query is required for movement projection.");
        float baseSpeed = context.Identity?.Data != null
            ? context.Identity.Data.moveSpeed
            : 1f;
        CharacterId characterId = new(context.Identity?.PersistentId);
        float contextFactor = GetFatigueEfficiencyMultiplier(context.Sleep)
            * deprivation.GetMoveSpeedMultiplier(context.Actor)
            * environmentStatus.GetMoveSpeedMultiplier(characterId)
            * GetEquipmentBurdenMultiplier(context, characterId)
            * externalCombatInfluence.GetMoveSpeedMultiplier(
                context.Identity?.PersistentId);
        CharacterPerformanceSnapshot snapshot = performance.Evaluate(
            context.Actor,
            "performance:survival:movement-speed",
            contextFactor,
            BuildCharacterEffectContext(context, null));
        return snapshot.IsApplicable ? baseSpeed * snapshot.Value : 0f;
    }

    public float GetWorkContextMultiplier(
        CharacterStatsProjectionContext context,
        WorkTypeDefinition definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        float discontentMultiplier = context.Actor != null
            ? staffDiscontent.GetWorkEfficiencyMultiplier(context.Actor)
            : 1f;
        return (definition.WorkTypeId == BuiltInWorkTypeIds.Research
                ? SharedMultiplier(
                    context,
                    GameplayEffectTargetIds.ResearchSpeed,
                    BuildWorkEffectContext(context, definition.WorkTypeId))
                : 1f)
            * GetFatigueEfficiencyMultiplier(context.Sleep)
            * discontentMultiplier
            * CharacterSkillRuntimeEffects.GetWorkSpeedMultiplier(context.Actor)
            * deprivation.GetWorkSpeedMultiplier(context.Actor)
            * substances.GetWorkSpeedMultiplier(context.Actor)
            * ResolveEnvironmentWorkSpeed(context, definition.WorkTypeId)
            * GetEquipmentBurdenMultiplier(
                context,
                new CharacterId(context.Identity?.PersistentId))
            * contentWorkDelays.GetWorkSpeedMultiplier(definition.WorkTypeId);
    }

    public float ResolveEnvironmentWorkSpeed(
        CharacterStatsProjectionContext context,
        WorkTypeId workTypeId)
    {
        bool precision = CharacterExposureWorkSpeedAuthority
            .UsesPrecisionProjection(workTypeId);
        CharacterId characterId = new(context.Identity?.PersistentId);
        return precision
            ? environmentStatus.GetPrecisionWorkSpeedMultiplier(characterId)
            : environmentStatus.GetWorkSpeedMultiplier(characterId);
    }

    public float GetCombatContextMultiplier(
        CharacterStatsProjectionContext context)
    {
        return substances.GetCombatMultiplier(context.Actor);
    }

    private float GetEquipmentBurdenMultiplier(
        CharacterStatsProjectionContext context,
        CharacterId characterId)
    {
        float weight = equipmentBurden.GetEquippedWeight(characterId.Value);
        if (performance == null)
            throw new InvalidOperationException(
                "Character performance query is required for equipment burden.");
        float mobility = performance.GetFunctionalCapacities(context.Actor)
            .Get(CharacterFunctionalCapacityId.PhysicalMobility).Value;
        return CharacterEquipmentBurdenWorkSpeedAuthority.Resolve(
            weight,
            mobility,
            GetDetailedStatMultiplier(
                context,
                GameplayEffectTargetIds.HaulCapacity));
    }

    public float GetSpendingMultiplier(
        CharacterStatsProjectionContext context)
    {
        return RequirePerformance().Evaluate(
            context.Actor,
            "performance:social:spending").Value;
    }

    public float GetStayDurationMultiplier(
        CharacterStatsProjectionContext context) =>
        (context.EffectiveProfile?.GetStayDurationMultiplier() ?? 1f)
        * SharedMultiplier(context, GameplayEffectTargetIds.StayDuration);

    public float GetCrowdSensitivityMultiplier(
        CharacterStatsProjectionContext context) => RequirePerformance().Evaluate(
            context.Actor,
            "performance:social:crowd-sensitivity").Value;

    public float GetWaitPatienceMultiplier(
        CharacterStatsProjectionContext context) => RequirePerformance().Evaluate(
            context.Actor,
            "performance:social:wait-patience").Value;

    public float GetConsumptionMultiplier(
        CharacterStatsProjectionContext context) => RequirePerformance().Evaluate(
            context.Actor,
            "performance:survival:food-consumption").Value;

    private ICharacterPerformanceQuery RequirePerformance() => performance
        ?? throw new InvalidOperationException(
            "Character performance query was not injected into the stats projection service.");

    public GameplayEffectProjectionResult ProjectDetailedStat(
        CharacterStatsProjectionContext context,
        string targetId,
        float baseValue,
        IEnumerable<string> activeConditionIds = null)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            throw new ArgumentException(
                "Gameplay effect target id is required.",
                nameof(targetId));
        if (float.IsNaN(baseValue) || float.IsInfinity(baseValue))
            throw new ArgumentOutOfRangeException(nameof(baseValue));
        if (context.Actor == null)
            return new GameplayEffectProjectionResult(
                baseValue,
                Array.Empty<GameplayEffectContribution>());

        return sharedEffects.ProjectValue(
            context.Actor,
            targetId,
            baseValue,
            BuildCharacterEffectContext(context, activeConditionIds));
    }

    public float GetDetailedStatMultiplier(
        CharacterStatsProjectionContext context,
        string targetId,
        IEnumerable<string> activeConditionIds = null) =>
        ProjectDetailedStat(
            context,
            targetId,
            1f,
            activeConditionIds).Value;

    private float SharedMultiplier(
        CharacterStatsProjectionContext context,
        string targetId,
        GameplayEffectContext effectContext = null) =>
        context.Actor == null
            ? 1f
            : sharedEffects.ProjectIncrementalMultiplier(
                context.Actor,
                targetId,
                effectContext ?? BuildCharacterEffectContext(context, null));

    private GameplayEffectContext BuildWorkEffectContext(
        CharacterStatsProjectionContext context,
        WorkTypeId workTypeId)
    {
        List<string> conditions = new();
        string id = workTypeId.Value?.Trim() ?? string.Empty;
        if (id.Length > 0)
            conditions.Add(id.StartsWith("work:", StringComparison.Ordinal)
                ? id
                : $"work:{id}");
        if (workTypeId == BuiltInWorkTypeIds.Guard
            || workTypeId == BuiltInWorkTypeIds.Hunt
            || workTypeId == BuiltInWorkTypeIds.Rescue
            || workTypeId == BuiltInWorkTypeIds.ThreatMitigation)
            conditions.Add("work:dangerous");
        if (workTypeId == BuiltInWorkTypeIds.Rescue
            || workTypeId == BuiltInWorkTypeIds.ThreatMitigation)
            conditions.Add("work:emergency");
        if (workTypeId == BuiltInWorkTypeIds.Clean)
        {
            conditions.Add("work:clean");
            conditions.Add("work:clean-maintenance");
            conditions.Add("work:contamination");
        }
        else
            conditions.Add("work:not-clean");
        if (workTypeId != BuiltInWorkTypeIds.Research)
            conditions.Add("work:not-research");
        string normalizedId = workTypeId.Value ?? string.Empty;
        if (normalizedId.IndexOf("research", StringComparison.OrdinalIgnoreCase) >= 0
            || normalizedId.IndexOf("craft", StringComparison.OrdinalIgnoreCase) >= 0
            || normalizedId.IndexOf("medical", StringComparison.OrdinalIgnoreCase) >= 0
            || normalizedId.IndexOf("treat", StringComparison.OrdinalIgnoreCase) >= 0)
            conditions.Add("work:precision");
        long hour = Math.Max(0L, calendar.AbsoluteHour) % 24L;
        conditions.Add(hour >= 18L || hour < 6L ? "shift:night" : "shift:day");
        if (context.Actor != null
            && context.Actor.TryGetAbility(out AbilityWork activeWork))
        {
            conditions.AddRange(activeWork.GetActiveGameplayEffectConditionIds());
        }
        CharacterId characterId = new(context.Identity?.PersistentId);
        if (workTypeId == BuiltInWorkTypeIds.Cook
            && environmentStatus.GetExposure(characterId)?.airborneExposure > 0.001f)
        {
            conditions.Add("work:contaminated-food");
        }
        return BuildCharacterEffectContext(context, conditions);
    }

    private GameplayEffectContext BuildCharacterEffectContext(
        CharacterStatsProjectionContext context,
        IEnumerable<string> additionalConditions)
    {
        HashSet<string> conditions = new(StringComparer.Ordinal);
        if (additionalConditions != null)
        {
            foreach (string condition in additionalConditions)
            {
                if (!string.IsNullOrWhiteSpace(condition))
                    conditions.Add(condition.Trim());
            }
        }

        if (stockPolicies?.GetEmergencyReadiness().Ready == true)
            conditions.Add("state:emergency-stocked");

        CharacterRitualFastStatus ritualStatus = ritualFasting?.GetStatus(
            context.Actor) ?? default;
        if (ritualStatus.Phase == CharacterRitualFastPhase.Fasting)
            conditions.Add("state:ritual-fasting");
        else if (ritualStatus.Phase == CharacterRitualFastPhase.AwaitingPostFastMeal)
            conditions.Add("state:ritual-fast-ended");

        CharacterStats stats = context.Actor?.Stats;
        if (stats != null)
        {
            if (stats.GetConditionValue(CharacterCondition.HUNGER, 0f) >= 80f)
                conditions.Add("state:sated");
            if (context.InjurySeverity > 0.001f)
                conditions.Add("state:pain");

            CharacterMoodSnapshot mood = stats.GetMoodSnapshot();
            foreach (CharacterMoodFactorSnapshot factor in mood.Factors)
            {
                string factorId = factor?.Id?.Trim() ?? string.Empty;
                if (factorId.IndexOf("food:sated", StringComparison.Ordinal) >= 0)
                    conditions.Add("state:sated");
                if (factorId.IndexOf("food:sweet", StringComparison.Ordinal) >= 0)
                    conditions.Add("state:sweet-fed");
                if (factorId.IndexOf("insult", StringComparison.Ordinal) >= 0)
                    conditions.Add("state:insulted");
            }
        }

        CharacterId characterId = new(context.Identity?.PersistentId);
        if (context.Actor != null
            && context.Actor.TryGetAbility(out AbilityWork work)
            && work.CachedGrid != null)
        {
            GridCell currentCell = work.CachedGrid.GetGridCell(
                work.CachedGrid.GetXY(context.Actor.transform.position));
            if (currentCell != null
                && currentCell.TerrainType != GridCellTerrainType.Dry)
            {
                conditions.Add("terrain:rough");
                conditions.Add("accident:fall-slip");
            }
        }
        CharacterEnvironmentExposure exposure = environmentStatus.GetExposure(characterId);
        float cold = exposure?.coldExposure ?? 0f;
        float heat = exposure?.heatExposure ?? 0f;
        if (cold <= 0.001f && heat <= 0.001f)
            conditions.Add("temperature:comfortable");
        else
        {
            conditions.Add("temperature:uncomfortable");
            if (cold >= heat) conditions.Add("temperature:cold");
            if (heat >= cold) conditions.Add("temperature:hot");
        }
        return new GameplayEffectContext(conditions);
    }

    public float CalculateMaximumHealth(
        CharacterStatsProjectionContext context)
    {
        const float maximum = 100f;
        return context.Identity != null && context.Identity.IsOwner
            ? maximum * metaProgression.GetOwnerMaxHealthMultiplier()
            : maximum;
    }

    public static float GetFatigueEfficiencyMultiplier(float sleep) =>
        CharacterFatigueWorkSpeedAuthority.Resolve(sleep);

    public static float GetInjuryEfficiencyMultiplier(float injurySeverity) =>
        Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(injurySeverity));
}

public sealed class NeutralSurgicalAugmentationQuery :
    ISurgicalAugmentationQuery
{
    public static readonly NeutralSurgicalAugmentationQuery Instance = new();
    private NeutralSurgicalAugmentationQuery() { }
    public string GetSpecialEffectLabel(SurgicalPartInstance part) => string.Empty;
}

public sealed class NeutralCharacterSubstanceRuntime :
    ICharacterSubstanceRuntime
{
    public static readonly NeutralCharacterSubstanceRuntime Instance = new();
    private NeutralCharacterSubstanceRuntime() { }
    public CharacterSubstancePolicyState GetPolicy(
        CharacterActor actor,
        string substanceId) => default;
    public CharacterSubstanceState GetState(
        CharacterActor actor,
        string substanceId) => default;
    public bool TryGetAutomaticUseRequest(
        CharacterActor actor,
        out CharacterSubstanceUseRequest request)
    {
        request = default;
        return false;
    }
    public float GetWorkSpeedMultiplier(CharacterActor actor) => 1f;
    public float GetCombatMultiplier(CharacterActor actor) => 1f;
    public void SetPolicy(
        CharacterActor actor,
        string substanceId,
        SubstancePolicyMode mode,
        float moodThreshold = 30f,
        int scheduledHour = 20)
    {
    }
    public bool TryConsume(
        CharacterActor actor,
        string substanceId,
        bool medicalContext,
        bool combatContext,
        out SubstanceUseResult result)
    {
        result = default;
        return false;
    }
    public bool TryConsumeAtFacility(
        CharacterActor actor,
        BuildableObject facility,
        out SubstanceUseResult result)
    {
        result = default;
        return false;
    }
    public bool TryConsume(
        ConsumeSubstanceCommand command,
        out SubstanceUseResult result)
    {
        result = default;
        return false;
    }
}

public sealed class NeutralCharacterEnvironmentStatusQuery :
    ICharacterEnvironmentStatusQuery
{
    public static readonly NeutralCharacterEnvironmentStatusQuery Instance = new();
    private NeutralCharacterEnvironmentStatusQuery() { }
    public CharacterEnvironmentExposure GetExposure(CharacterId characterId) =>
        default;
    public EnvironmentalExposureBand GetPhysiologicalBand(CharacterId characterId) =>
        default;
    public EnvironmentalExposureBand GetVisualBand(CharacterId characterId) =>
        default;
    public float GetWorkSpeedMultiplier(CharacterId characterId) => 1f;
    public float GetPrecisionWorkSpeedMultiplier(CharacterId characterId) => 1f;
    public float GetMoveSpeedMultiplier(CharacterId characterId) => 1f;
    public float GetAccuracyPenaltyPoints(CharacterId characterId) => 0f;
}

public sealed class NeutralExternalCombatInfluenceQuery :
    IExternalCombatInfluenceQuery
{
    public static readonly NeutralExternalCombatInfluenceQuery Instance = new();
    private NeutralExternalCombatInfluenceQuery() { }
    public float GetMoveSpeedMultiplier(string characterId) => 1f;
    public float GetAttackSpeedMultiplier(string characterId) => 1f;
    public bool IsDreadDefenseActive => false;
}

public sealed class NeutralDiseaseSymptomEffectQuery :
    IDiseaseSymptomEffectQuery
{
    public static readonly NeutralDiseaseSymptomEffectQuery Instance = new();
    private NeutralDiseaseSymptomEffectQuery() { }
    public System.Collections.Generic.IReadOnlyList<DiseaseSymptomEffectSnapshot>
        GetActiveSymptoms(CharacterId characterId) =>
            System.Array.Empty<DiseaseSymptomEffectSnapshot>();
    public float GetWorkSpeedMultiplier(CharacterId characterId) => 1f;
    public float GetMoveSpeedMultiplier(CharacterId characterId) => 1f;
}

public sealed class NeutralCharacterCombatSpecialStatusQuery :
    ICharacterCombatSpecialStatusQuery
{
    public static readonly NeutralCharacterCombatSpecialStatusQuery Instance =
        new();

    private NeutralCharacterCombatSpecialStatusQuery()
    {
    }

    public CharacterCombatSpecialStatusSnapshot GetCombatSpecialStatus(
        CharacterId characterId) => default;
}

public sealed class NeutralCombatEquipmentBurdenQuery :
    ICombatEquipmentBurdenQuery
{
    public static readonly NeutralCombatEquipmentBurdenQuery Instance = new();
    private NeutralCombatEquipmentBurdenQuery() { }
    public float GetEquippedWeight(string characterId) => 0f;
}
