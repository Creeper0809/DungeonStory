using System;
using DungeonStory.Foundation;
using UnityEngine;

/// <summary>Owns approved work and once-per-run proficiency/species projections.</summary>
internal sealed class WorkProficiencyProgress
{
    private readonly AbilityWork work;
    private readonly ICharacterProficiencyCommand proficiencyCommands;
    private readonly ICharacterProficiencyQuery proficiencyQuery;
    private readonly IGameCalendar calendar;
    private readonly ICombatEquipmentRuntime combatEquipmentRuntime;
    private readonly ICharacterSpeciesCommand speciesCommands;
    private readonly ICharacterSettlementStandingQuery settlementStandings;
    private float approvedProficiencyWork;
    private bool proficiencyAwarded;
    private float proficiencyRepetitionMultiplier = 1f;

    internal WorkProficiencyProgress(AbilityWork work,
        ICharacterProficiencyCommand proficiencyCommands, ICharacterProficiencyQuery proficiencyQuery,
        IGameCalendar calendar, ICombatEquipmentRuntime combatEquipmentRuntime,
        ICharacterSpeciesCommand speciesCommands, ICharacterSettlementStandingQuery settlementStandings)
    {
        this.work = work ?? throw new ArgumentNullException(nameof(work));
        this.proficiencyCommands = proficiencyCommands;
        this.proficiencyQuery = proficiencyQuery;
        this.calendar = calendar;
        this.combatEquipmentRuntime = combatEquipmentRuntime;
        this.speciesCommands = speciesCommands ?? throw new ArgumentNullException(nameof(speciesCommands));
        this.settlementStandings = settlementStandings;
    }

    internal float ApprovedWork => approvedProficiencyWork;
    internal void BeginRun()
    {
        approvedProficiencyWork = 0f;
        proficiencyAwarded = false;
        proficiencyRepetitionMultiplier = 1f;
    }
    internal void RecordApproved(float amount) => approvedProficiencyWork += amount;
    internal void SetQualityAttemptIndex(int attemptIndex) =>
        proficiencyRepetitionMultiplier = ResolveRepeatPracticeMultiplier(attemptIndex);

    internal void AwardApprovedWork(
        CharacterActor actor,
        ProficiencyWorkOutcome outcome,
        string sourceOperationId = null)
    {
        if (proficiencyAwarded
            || approvedProficiencyWork <= 0f
            || proficiencyCommands == null
            || calendar == null
            || !TryResolveProficiencyProfile(
                actor,
                work.assignedShop,
                work.AssignedWorkTypeId,
                out ProficiencyWorkProfile profile)
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId))
        {
            return;
        }

        proficiencyAwarded = true;
        float standingExperienceMultiplier = settlementStandings
            ?.GetApprovedWorkExperienceMultiplier(actor) ?? 1f;
        proficiencyCommands.AddApprovedWork(
            characterId,
            profile,
            approvedProficiencyWork * Mathf.Clamp01(
                standingExperienceMultiplier),
            difficultyMultiplier: ResolveDifficultyMultiplier(
                actor,
                work.assignedShop,
                work.AssignedWorkTypeId,
                profile),
            outcome: outcome,
            learningMultiplier:
                CharacterProficiencyLearningRules.Resolve(
                    actor,
                    profile,
                    work.AssignedWorkTypeId),
            repetitionMultiplier: work.AssignedWorkTypeId
                == BuiltInWorkTypeIds.Dismantle
                    ? Mathf.Min(0.20f, proficiencyRepetitionMultiplier)
                    : proficiencyRepetitionMultiplier,
            absoluteHour: calendar.AbsoluteHour,
            sourceOperationId: string.IsNullOrWhiteSpace(sourceOperationId)
                ? "work:"
                    + characterId.Value
                    + ":"
                    + work.AssignedWorkTypeId.Value
                    + ":"
                    + calendar.AbsoluteHour
                : sourceOperationId);
    }

    internal void RecordSpeciesCompletedWork(
        CharacterActor actor,
        WorkTypeId workTypeId,
        float completedWork,
        string sourceOperationId)
    {
        if (completedWork <= 0f)
            return;
        if (!CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId))
            throw new InvalidOperationException(
                "Species completed-work projection requires a persistent character id.");
        DomainFailure failure;
        bool recorded;
        if (speciesCommands is ICharacterSpeciesContributionCommand exact)
        {
            recorded = exact.RecordCompletedWork(
                characterId,
                sourceOperationId,
                workTypeId.Value,
                completedWork,
                out failure);
        }
        else
        {
            recorded = speciesCommands.RecordCompletedWork(
                characterId,
                workTypeId.Value,
                completedWork,
                out failure);
        }
        if (!recorded)
            throw new InvalidOperationException(
                $"Species completed-work projection failed: {failure.Code}");
    }

    private bool TryResolveProficiencyProfile(
        CharacterActor actor,
        BuildableObject target,
        WorkTypeId workTypeId,
        out ProficiencyWorkProfile profile)
    {
        ProficiencyWorkProfileAuthoring authored = ResolveAuthoredProfile(
            target,
            workTypeId);
        if (authored?.IsValid == true)
        {
            if (authored.CombinationMode == ProficiencyCombinationMode.Higher)
            {
                profile = ResolveHigherCombatProfile(actor);
                return profile.IsValid;
            }
            profile = new ProficiencyWorkProfile(
                authored.Primary,
                authored.Secondary,
                authored.PrimaryWeight);
            return profile.IsValid;
        }
        if (workTypeId == BuiltInWorkTypeIds.Operate)
        {
            profile = default;
            return false;
        }
        if (!WorkTypeProficiencyRules.TryResolve(workTypeId, out profile))
        {
            return false;
        }

        if (workTypeId == BuiltInWorkTypeIds.Hunt)
        {
            CharacterProficiencyId combat =
                BuiltInCharacterProficiencyIds.MeleeCombat;
            if (actor != null
                && combatEquipmentRuntime != null
                && combatEquipmentRuntime.TryGetActiveWeapon(
                    actor.Identity?.PersistentId ?? string.Empty,
                    out CombatWeaponSnapshot weapon)
                && weapon?.IsRanged == true)
            {
                combat = BuiltInCharacterProficiencyIds.RangedCombat;
            }
            profile = new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.FoodProduction,
                combat,
                0.80f);
        }
        else if (workTypeId == BuiltInWorkTypeIds.Warden
            && actor != null
            && proficiencyQuery != null
            && calendar != null
            && CharacterPersistentIdentity.TryGet(
                actor,
                out CharacterId characterId))
        {
            proficiencyQuery.TryGetProficiency(
                characterId,
                BuiltInCharacterProficiencyIds.MeleeCombat,
                calendar.AbsoluteHour,
                out CharacterProficiencySnapshot melee);
            proficiencyQuery.TryGetProficiency(
                characterId,
                BuiltInCharacterProficiencyIds.RangedCombat,
                calendar.AbsoluteHour,
                out CharacterProficiencySnapshot ranged);
            profile = new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.Social,
                ranged.CurrentExperience > melee.CurrentExperience
                    ? BuiltInCharacterProficiencyIds.RangedCombat
                    : BuiltInCharacterProficiencyIds.MeleeCombat,
                0.80f);
        }
        else if (workTypeId == BuiltInWorkTypeIds.Craft
            && IsRuneCraftTarget(target))
        {
            profile = new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.Crafting,
                BuiltInCharacterProficiencyIds.Scholarship,
                0.80f);
        }
        return true;
    }

    private float ResolveDifficultyMultiplier(
        CharacterActor actor,
        BuildableObject target,
        WorkTypeId workTypeId,
        ProficiencyWorkProfile profile)
    {
        ProficiencyWorkProfileAuthoring authored = ResolveAuthoredProfile(
            target,
            workTypeId);
        if (authored?.IsValid != true
            || proficiencyQuery == null
            || calendar == null
            || actor == null
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
            || !proficiencyQuery.TryGetProficiency(
                id,
                profile.Primary,
                calendar.AbsoluteHour,
                out CharacterProficiencySnapshot current))
        {
            return 1f;
        }

        int rankDifference = (int)current.Rank - (int)authored.RecommendedRank;
        if (rankDifference < 0) return 1.25f;
        if (rankDifference == 0) return 1f;
        if (rankDifference == 1) return 0.55f;
        return 0.20f;
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

    private ProficiencyWorkProfile ResolveHigherCombatProfile(
        CharacterActor actor)
    {
        if (actor == null || proficiencyQuery == null || calendar == null
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId id))
        {
            return new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.MeleeCombat);
        }
        proficiencyQuery.TryGetProficiency(
            id,
            BuiltInCharacterProficiencyIds.MeleeCombat,
            calendar.AbsoluteHour,
            out CharacterProficiencySnapshot melee);
        proficiencyQuery.TryGetProficiency(
            id,
            BuiltInCharacterProficiencyIds.RangedCombat,
            calendar.AbsoluteHour,
            out CharacterProficiencySnapshot ranged);
        return new ProficiencyWorkProfile(
            ranged.CurrentMilliExperience > melee.CurrentMilliExperience
                ? BuiltInCharacterProficiencyIds.RangedCombat
                : BuiltInCharacterProficiencyIds.MeleeCombat);
    }

    private static float ResolveRepeatPracticeMultiplier(int attemptIndex)
    {
        int attemptNumber = Mathf.Max(0, attemptIndex) + 1;
        if (attemptNumber <= 3) return 1f;
        if (attemptNumber <= 10) return 0.50f;
        return 0.15f;
    }

    private static bool IsRuneCraftTarget(BuildableObject target)
    {
        string name = target?.BuildingData?.objectName ?? string.Empty;
        return name.IndexOf("rune", StringComparison.OrdinalIgnoreCase) >= 0
            || name.Contains("룬", StringComparison.Ordinal);
    }

}
