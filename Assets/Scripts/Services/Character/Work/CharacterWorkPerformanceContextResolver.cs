using System;
using UnityEngine;

public readonly struct WorkAccidentRiskContext
{
    public WorkAccidentRiskContext(
        bool hasSleepState,
        float sleep,
        float sufficientSleep,
        float fatigueMultiplier,
        bool hasFacilityIntegrity,
        float facilityIntegrityRatio,
        float facilityMultiplier)
    {
        HasSleepState = hasSleepState;
        Sleep = sleep;
        SufficientSleep = sufficientSleep;
        FatigueMultiplier = Mathf.Max(1f, fatigueMultiplier);
        HasFacilityIntegrity = hasFacilityIntegrity;
        FacilityIntegrityRatio = Mathf.Clamp01(facilityIntegrityRatio);
        FacilityMultiplier = Mathf.Max(1f, facilityMultiplier);
    }

    public bool HasSleepState { get; }
    public float Sleep { get; }
    public float SufficientSleep { get; }
    public float FatigueMultiplier { get; }
    public bool HasFacilityIntegrity { get; }
    public float FacilityIntegrityRatio { get; }
    public float FacilityMultiplier { get; }
    public float CombinedMultiplier => FatigueMultiplier * FacilityMultiplier;

    public string ObservationDetail
    {
        get
        {
            string fatigue = HasSleepState
                ? FormattableString.Invariant(
                    $"fatigue(sleep={Sleep:0.###},sufficient={SufficientSleep:0.###},multiplier={FatigueMultiplier:0.######})")
                : "fatigue(sleep=unavailable,multiplier=1)";
            string facility = HasFacilityIntegrity
                ? FormattableString.Invariant(
                    $"facility(integrity={FacilityIntegrityRatio:0.######},multiplier={FacilityMultiplier:0.######})")
                : "facility(integrity=unavailable,multiplier=1)";
            return fatigue + ";" + facility;
        }
    }
}

public sealed class CharacterWorkPerformanceContextResolver
{
    private readonly ICharacterProficiencyQuery proficiencies;
    private readonly IGameCalendar calendar;
    private readonly ICombatEquipmentRuntime combatEquipment;

    public CharacterWorkPerformanceContextResolver(
        ICharacterProficiencyQuery proficiencies,
        IGameCalendar calendar,
        ICombatEquipmentRuntime combatEquipment)
    {
        this.proficiencies = proficiencies
            ?? throw new ArgumentNullException(nameof(proficiencies));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.combatEquipment = combatEquipment;
    }

    public bool TryResolve(
        CharacterActor actor,
        BuildableObject target,
        WorkTypeId workTypeId,
        out ProficiencyWorkProfile profile,
        out string failureReason)
    {
        profile = default;
        failureReason = string.Empty;
        ProficiencyWorkProfileAuthoring authored = ResolveAuthoredProfile(
            target,
            workTypeId);
        if (authored?.IsValid == true)
        {
            profile = authored.CombinationMode == ProficiencyCombinationMode.Higher
                ? ResolveHigherCombatProfile(actor)
                : new ProficiencyWorkProfile(
                    authored.Primary,
                    authored.Secondary,
                    authored.PrimaryWeight);
            return ValidateWeight(profile, workTypeId, out failureReason);
        }

        if (workTypeId == BuiltInWorkTypeIds.Operate)
        {
            failureReason = $"Facility '{target?.BuildingData?.objectName}' has no authored operation proficiency.";
            return false;
        }

        if (workTypeId == BuiltInWorkTypeIds.Guard)
        {
            profile = new ProficiencyWorkProfile(ResolveActiveCombat(actor));
            return true;
        }
        if (workTypeId == BuiltInWorkTypeIds.Rest)
        {
            // Rest intentionally has no learned proficiency. Its sleep,
            // fatigue, vitality, anatomy, environment, and status channels are
            // resolved by the authored performance formula with a neutral
            // proficiency factor.
            profile = default;
            return true;
        }
        if (!WorkTypeProficiencyRules.TryResolve(workTypeId, out profile))
        {
            failureReason = $"Work type '{workTypeId.Value}' has no proficiency profile.";
            return false;
        }
        if (workTypeId == BuiltInWorkTypeIds.Hunt)
        {
            profile = new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.FoodProduction,
                ResolveActiveCombat(actor),
                .80f);
        }
        else if (workTypeId == BuiltInWorkTypeIds.Warden)
        {
            profile = new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.Social,
                ResolveHigherCombatProfile(actor).Primary,
                .80f);
        }
        else if (workTypeId == BuiltInWorkTypeIds.Craft
            && IsRuneCraftTarget(target))
        {
            profile = new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.Crafting,
                BuiltInCharacterProficiencyIds.Scholarship,
                .80f);
        }
        return ValidateWeight(profile, workTypeId, out failureReason);
    }

    public CharacterPerformanceEvaluationContext BuildEvaluationContext(
        ProficiencyWorkProfile profile,
        GameplayEffectContext effects = null,
        float contextFactor = 1f) => new()
    {
        ContextFactor = contextFactor,
        GameplayEffectContext = effects,
        PrimaryProficiencyOverride = profile.Primary.Value,
        SecondaryProficiencyOverride = profile.Secondary.IsValid
            ? profile.Secondary.Value
            : profile.Primary.Value
    };

    public WorkAccidentRiskContext ResolveAccidentRiskContext(
        CharacterActor actor,
        WorkTypeId assignedWorkTypeId,
        bool hasFacilityIntegrity,
        float facilityIntegrityRatio)
    {
        if (actor == null)
            throw new ArgumentNullException(nameof(actor));
        if (!assignedWorkTypeId.IsValid)
            throw new ArgumentException(
                "Work accident risk requires the exact assigned work type.",
                nameof(assignedWorkTypeId));

        float sleepValue = 100f;
        bool hasSleepState = actor.Stats != null
            && actor.Stats.TryGetConditionValue(
                CharacterCondition.SLEEP,
                out sleepValue);
        float sleep = hasSleepState
            ? Mathf.Clamp(sleepValue, 0f, 100f)
            : 100f;
        float sufficientSleep = hasSleepState
            ? Mathf.Clamp(
                actor.Stats.GetNeedResponse(CharacterCondition.SLEEP).resumeTarget,
                0f,
                100f)
            : 100f;
        float fatigueDeficit = sufficientSleep > 0f
            ? Mathf.Clamp01((sufficientSleep - sleep) / sufficientSleep)
            : 0f;
        float fatigueMultiplier = 1f + fatigueDeficit;

        float integrityRatio = hasFacilityIntegrity
            ? Mathf.Clamp01(facilityIntegrityRatio)
            : 1f;
        float facilityMultiplier = 1f + (1f - integrityRatio);

        return new WorkAccidentRiskContext(
            hasSleepState,
            sleep,
            sufficientSleep,
            fatigueMultiplier,
            hasFacilityIntegrity,
            integrityRatio,
            facilityMultiplier);
    }

    private CharacterProficiencyId ResolveActiveCombat(CharacterActor actor)
    {
        if (actor != null
            && combatEquipment != null
            && combatEquipment.TryGetActiveWeapon(
                actor.Identity?.PersistentId ?? string.Empty,
                out CombatWeaponSnapshot weapon)
            && weapon?.IsRanged == true)
            return BuiltInCharacterProficiencyIds.RangedCombat;
        return BuiltInCharacterProficiencyIds.MeleeCombat;
    }

    private ProficiencyWorkProfile ResolveHigherCombatProfile(CharacterActor actor)
    {
        if (actor == null
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId id))
            throw new InvalidOperationException(
                "Higher-combat proficiency selection requires a persistent character id.");
        if (!proficiencies.TryGetProficiency(
                id,
                BuiltInCharacterProficiencyIds.MeleeCombat,
                calendar.AbsoluteHour,
                out CharacterProficiencySnapshot melee)
            || !proficiencies.TryGetProficiency(
                id,
                BuiltInCharacterProficiencyIds.RangedCombat,
                calendar.AbsoluteHour,
                out CharacterProficiencySnapshot ranged))
            throw new InvalidOperationException(
                $"Character '{id.Value}' lacks a combat proficiency record.");
        return new ProficiencyWorkProfile(
            ranged.CurrentMilliExperience > melee.CurrentMilliExperience
                ? BuiltInCharacterProficiencyIds.RangedCombat
                : BuiltInCharacterProficiencyIds.MeleeCombat);
    }

    private static bool ValidateWeight(
        ProficiencyWorkProfile profile,
        WorkTypeId workTypeId,
        out string failureReason)
    {
        if (!profile.Primary.IsValid)
        {
            failureReason = $"Work type '{workTypeId.Value}' has no primary proficiency.";
            return false;
        }
        if (profile.SecondaryWeight > .200001f)
        {
            failureReason = $"Work type '{workTypeId.Value}' secondary proficiency exceeds 20%.";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private static ProficiencyWorkProfileAuthoring ResolveAuthoredProfile(
        BuildableObject target,
        WorkTypeId workTypeId)
    {
        BuildingSO building = target?.BuildingData;
        if (building == null) return null;
        if (workTypeId == BuiltInWorkTypeIds.Operate)
            return building.OperationProficiency;
        if (workTypeId == BuiltInWorkTypeIds.Construct
            || workTypeId == BuiltInWorkTypeIds.Repair
            || workTypeId == BuiltInWorkTypeIds.Plumbing
            || workTypeId == BuiltInWorkTypeIds.Dismantle
            || workTypeId == BuiltInWorkTypeIds.GrandProject)
            return building.ConstructionProficiency;
        return null;
    }

    private static bool IsRuneCraftTarget(BuildableObject target)
    {
        string value = target?.BuildingData?.objectName ?? string.Empty;
        return value.IndexOf("rune", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("룬", StringComparison.Ordinal) >= 0;
    }
}
