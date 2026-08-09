using System;
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

/// <summary>
/// Calculates derived character performance from authoritative character state
/// and injected domain policies. It stores no character state.
/// </summary>
public sealed class CharacterStatsProjectionService
{
    private readonly IStaffDiscontentRuntimeService staffDiscontent;
    private readonly IMetaProgressionRuntimeReader metaProgression;
    private readonly ICharacterPhysicalCapacityQuery physicalCapacity;
    private readonly ICharacterDeprivationQuery deprivation;
    private readonly ICharacterSubstanceRuntime substances;
    private readonly ISurgicalAugmentationQuery surgicalAugmentation;
    private readonly ICharacterEnvironmentStatusQuery environmentStatus;
    private readonly IExternalCombatInfluenceQuery externalCombatInfluence;
    private readonly IContentWorkDelayQuery contentWorkDelays;
    private readonly IDiseaseSymptomEffectQuery diseaseSymptoms;
    private readonly ICharacterCombatSpecialStatusQuery combatStatuses;
    private readonly ICombatEquipmentBurdenQuery equipmentBurden;

    public CharacterStatsProjectionService(
        IStaffDiscontentRuntimeService staffDiscontent,
        IMetaProgressionRuntimeReader metaProgression,
        ICharacterPhysicalCapacityQuery physicalCapacity,
        ICharacterDeprivationQuery deprivation,
        ICharacterSubstanceRuntime substances,
        ISurgicalAugmentationQuery surgicalAugmentation,
        ICharacterEnvironmentStatusQuery environmentStatus,
        IExternalCombatInfluenceQuery externalCombatInfluence,
        IContentWorkDelayQuery contentWorkDelays,
        IDiseaseSymptomEffectQuery diseaseSymptoms,
        ICharacterCombatSpecialStatusQuery combatStatuses,
        ICombatEquipmentBurdenQuery equipmentBurden)
    {
        this.staffDiscontent = staffDiscontent
            ?? throw new ArgumentNullException(nameof(staffDiscontent));
        this.metaProgression = metaProgression
            ?? throw new ArgumentNullException(nameof(metaProgression));
        this.physicalCapacity = physicalCapacity
            ?? throw new ArgumentNullException(nameof(physicalCapacity));
        this.deprivation = deprivation
            ?? throw new ArgumentNullException(nameof(deprivation));
        this.substances = substances
            ?? throw new ArgumentNullException(nameof(substances));
        this.surgicalAugmentation = surgicalAugmentation
            ?? throw new ArgumentNullException(nameof(surgicalAugmentation));
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
    }

    public int GetCharacterStat(
        CharacterStatsProjectionContext context,
        CharacterStatType statType)
    {
        int baseValue = context.Actor?.Progression != null
            ? context.Actor.Progression.GetFinalStat(statType)
            : context.Identity?.Profile?.GetStat(statType) ?? 5;
        return Mathf.Max(
            0,
            baseValue + surgicalAugmentation.GetStatBonus(
                context.Identity?.PersistentId,
                statType));
    }

    public int GetCharacterStat(
        CharacterStatsProjectionContext context,
        string statId)
    {
        int baseValue = context.Actor?.Progression != null
            ? context.Actor.Progression.GetFinalStat(statId)
            : context.Identity?.Profile?.GetStat(statId) ?? 0;
        if (CharacterStatCatalog.TryGet(
                statId,
                out CharacterStatDefinition definition)
            && definition.LegacyType.HasValue)
        {
            baseValue += surgicalAugmentation.GetStatBonus(
                context.Identity?.PersistentId,
                definition.LegacyType.Value);
        }

        return Mathf.Max(0, baseValue);
    }

    public float GetMoveSpeed(CharacterStatsProjectionContext context)
    {
        float baseSpeed = context.Identity?.Data != null
            ? context.Identity.Data.moveSpeed
            : 1f;
        float statMultiplier = Mathf.Clamp(
            1f + ((GetCharacterStat(context, CharacterStatType.MoveSpeed) - 5)
                * 0.08f),
            0.5f,
            1.8f);
        CharacterId characterId = new(context.Identity?.PersistentId);
        return baseSpeed
            * statMultiplier
            * (context.EffectiveProfile?.GetMoveModifierOnly() ?? 1f)
            * GetFatigueEfficiencyMultiplier(context.Sleep)
            * physicalCapacity.GetMoveMultiplier(context.Actor)
            * deprivation.GetMoveSpeedMultiplier(context.Actor)
            * environmentStatus.GetMoveSpeedMultiplier(characterId)
            * diseaseSymptoms.GetMoveSpeedMultiplier(characterId)
            * GetSedationActivityMultiplier(characterId)
            * GetEquipmentBurdenMultiplier(context, characterId)
            * externalCombatInfluence.GetMoveSpeedMultiplier(
                context.Identity?.PersistentId);
    }

    public float GetWorkSpeedMultiplier(
        CharacterStatsProjectionContext context,
        WorkTypeDefinition definition)
    {
        if (definition == null)
        {
            return 1f;
        }

        float discontentMultiplier = context.Actor != null
            ? staffDiscontent.GetWorkEfficiencyMultiplier(context.Actor)
            : 1f;
        CharacterStatType workStat = CharacterWorkStatRules.GetBestWorkStat(
            FacilityWorkTypeMap.GetRequired(definition));
        float statMultiplier = Mathf.Clamp(
            1f + ((GetCharacterStat(context, workStat) - 5) * 0.06f),
            0.5f,
            2f);
        return statMultiplier
            * (context.EffectiveProfile?.GetWorkModifierOnly(
                definition.WorkTypeId) ?? 1f)
            * GetFatigueEfficiencyMultiplier(context.Sleep)
            * physicalCapacity.GetWorkMultiplier(
                context.Actor,
                definition.WorkTypeId)
            * discontentMultiplier
            * CharacterSkillRuntimeEffects.GetWorkSpeedMultiplier(context.Actor)
            * deprivation.GetWorkSpeedMultiplier(context.Actor)
            * substances.GetWorkSpeedMultiplier(context.Actor)
            * ResolveEnvironmentWorkSpeed(context, definition.WorkTypeId)
            * diseaseSymptoms.GetWorkSpeedMultiplier(
                new CharacterId(context.Identity?.PersistentId))
            * GetSedationActivityMultiplier(
                new CharacterId(context.Identity?.PersistentId))
            * GetEquipmentBurdenMultiplier(
                context,
                new CharacterId(context.Identity?.PersistentId))
            * contentWorkDelays.GetWorkSpeedMultiplier(definition.WorkTypeId);
    }

    public float ResolveEnvironmentWorkSpeed(
        CharacterStatsProjectionContext context,
        WorkTypeId workTypeId)
    {
        string id = workTypeId.Value ?? string.Empty;
        bool precision =
            id.IndexOf("research", StringComparison.OrdinalIgnoreCase) >= 0
            || id.IndexOf("craft", StringComparison.OrdinalIgnoreCase) >= 0
            || id.IndexOf("medical", StringComparison.OrdinalIgnoreCase) >= 0
            || id.IndexOf("treat", StringComparison.OrdinalIgnoreCase) >= 0;
        CharacterId characterId = new(context.Identity?.PersistentId);
        return precision
            ? environmentStatus.GetPrecisionWorkSpeedMultiplier(characterId)
            : environmentStatus.GetWorkSpeedMultiplier(characterId);
    }

    public float GetAccidentChanceMultiplier(
        CharacterStatsProjectionContext context)
    {
        float enduranceMultiplier = Mathf.Clamp(
            1f - ((GetCharacterStat(context, CharacterStatType.Endurance) - 5)
                * 0.03f),
            0.5f,
            1.5f);
        float toughnessMultiplier = Mathf.Clamp(
            1f - ((GetCharacterStat(context, CharacterStatType.Toughness) - 5)
                * 0.02f),
            0.6f,
            1.4f);
        return (context.EffectiveProfile?.GetAccidentModifierOnly() ?? 1f)
            * enduranceMultiplier
            * toughnessMultiplier;
    }

    public float GetCombatPowerMultiplier(
        CharacterStatsProjectionContext context)
    {
        return (context.EffectiveProfile?.GetCombatPowerMultiplier() ?? 1f)
            * GetInjuryEfficiencyMultiplier(context.InjurySeverity)
            * substances.GetCombatMultiplier(context.Actor)
            * GetSedationActivityMultiplier(
                new CharacterId(context.Identity?.PersistentId));
    }

    private float GetSedationActivityMultiplier(CharacterId characterId)
    {
        CharacterCombatSpecialStatusSnapshot status =
            combatStatuses.GetCombatSpecialStatus(characterId);
        return status.SedationRemainingSeconds > 0f
            ? 1f - Mathf.Clamp(status.SedationRatio, 0f, 0.8f)
            : 1f;
    }

    private float GetEquipmentBurdenMultiplier(
        CharacterStatsProjectionContext context,
        CharacterId characterId)
    {
        float weight = equipmentBurden.GetEquippedWeight(characterId.Value);
        float capacity = Mathf.Max(
            8f,
            GetCharacterStat(context, CharacterStatType.Strength) * 4f);
        float overload = Mathf.Max(0f, weight / capacity - 0.5f);
        return Mathf.Clamp(1f - overload * 0.35f, 0.45f, 1f);
    }

    public float GetSpendingMultiplier(
        CharacterStatsProjectionContext context)
    {
        float statMultiplier = Mathf.Clamp(
            1f + ((GetCharacterStat(context, CharacterStatType.Sales) - 5)
                * 0.05f),
            0.5f,
            2f);
        return statMultiplier
            * (context.EffectiveProfile?.GetSpendingModifierOnly() ?? 1f);
    }

    public float CalculateMaximumHealth(
        CharacterStatsProjectionContext context)
    {
        int toughness = GetCharacterStat(context, CharacterStatType.Toughness);
        int endurance = GetCharacterStat(context, CharacterStatType.Endurance);
        float maximum = 60f + (toughness * 8f) + (endurance * 4f);
        return context.Identity != null && context.Identity.IsOwner
            ? maximum * metaProgression.GetOwnerMaxHealthMultiplier()
            : maximum;
    }

    public static float GetFatigueEfficiencyMultiplier(float sleep) =>
        Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(sleep / 100f));

    public static float GetInjuryEfficiencyMultiplier(float injurySeverity) =>
        Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(injurySeverity));
}

public sealed class NeutralCharacterPhysicalCapacityQuery :
    ICharacterPhysicalCapacityQuery
{
    public static readonly NeutralCharacterPhysicalCapacityQuery Instance = new();
    private NeutralCharacterPhysicalCapacityQuery() { }
    public CharacterPhysicalCapacitySnapshot GetSnapshot(CharacterActor actor) =>
        default;
    public float GetMoveMultiplier(CharacterActor actor) =>
        actor?.Stats != null
            ? CharacterStatsProjectionService.GetInjuryEfficiencyMultiplier(
                actor.Stats.InjurySeverity)
            : 1f;
    public float GetWorkMultiplier(CharacterActor actor, WorkTypeId workTypeId) =>
        GetMoveMultiplier(actor);
}

public sealed class NeutralSurgicalAugmentationQuery :
    ISurgicalAugmentationQuery
{
    public static readonly NeutralSurgicalAugmentationQuery Instance = new();
    private NeutralSurgicalAugmentationQuery() { }
    public int GetStatBonus(string subjectId, CharacterStatType statType) => 0;
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
