using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IRecruitedCharacterActivationService
{
    bool TryActivate(
        RegularCustomerRecord record,
        out CharacterActor actor,
        out string message);
}

public sealed class RecruitedCharacterActivationService : IRecruitedCharacterActivationService
{
    private readonly ICharacterWorldQuery characterWorld;
    private readonly ICharacterSpawnerProvider spawnerProvider;
    private readonly ICharacterSpawnObjectFactory characterObjectFactory;
    private readonly ICharacterPopulationService characterPopulationService;
    private readonly IOffenseQuery offense;
    private readonly ICharacterProficiencyQuery proficiencyQuery;
    private readonly ICharacterProficiencyCommand proficiencyCommands;
    private readonly IGameCalendar calendar;

    public RecruitedCharacterActivationService(
        ICharacterWorldQuery characterWorld,
        ICharacterSpawnerProvider spawnerProvider,
        ICharacterSpawnObjectFactory characterObjectFactory,
        ICharacterPopulationService characterPopulationService,
        IOffenseQuery offense,
        ICharacterProficiencyQuery proficiencyQuery,
        ICharacterProficiencyCommand proficiencyCommands,
        IGameCalendar calendar)
    {
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.spawnerProvider = spawnerProvider ?? throw new ArgumentNullException(nameof(spawnerProvider));
        this.characterObjectFactory = characterObjectFactory
            ?? throw new ArgumentNullException(nameof(characterObjectFactory));
        this.characterPopulationService = characterPopulationService
            ?? throw new ArgumentNullException(nameof(characterPopulationService));
        this.offense = offense ?? throw new ArgumentNullException(nameof(offense));
        this.proficiencyQuery = proficiencyQuery
            ?? throw new ArgumentNullException(nameof(proficiencyQuery));
        this.proficiencyCommands = proficiencyCommands
            ?? throw new ArgumentNullException(nameof(proficiencyCommands));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
    }

    public bool TryActivate(
        RegularCustomerRecord record,
        out CharacterActor actor,
        out string message)
    {
        actor = null;
        if (record == null || record.SourceData == null)
        {
            message = "Recruit source data is missing.";
            return false;
        }

        actor = CharacterActorCollection.GetCanonical(record.ActiveActor);
        if (!MatchesRecord(actor, record))
        {
            actor = CharacterActorCollection.DistinctByGameObject(characterWorld.Characters)
                .FirstOrDefault(candidate => MatchesRecord(candidate, record));
        }

        bool created = actor == null;
        CharacterSpawner spawner = null;
        if (created)
        {
            if (!spawnerProvider.TryGetSpawner(out spawner) || spawner.characterPrefab == null)
            {
                message = "Recruit character prefab was not found.";
                return false;
            }

            GameObject createdObject = characterObjectFactory.CreateInactive(
                spawner.characterPrefab,
                EnsureWorkAbility);
            actor = createdObject != null
                ? CharacterActorCollection.GetCanonical(createdObject.GetComponent<CharacterActor>())
                : null;
            if (actor == null)
            {
                characterObjectFactory.Destroy(createdObject);
                message = "Recruit character prefab has no CharacterActor.";
                return false;
            }
        }

        AIBrain brain = actor.Brain;
        brain?.StopCurrentActionForReplan("Recruited as dungeon staff.");
        actor.GetAbility<AbilityMove>()?.CancelActiveMovement();
        actor.Blackboard?.ClearMacroGoal("Recruited as dungeon staff.");
        actor.Blackboard?.ClearMoodImpulse("Recruited as dungeon staff.");

        if (actor.GetComponent<AbilityWork>() == null)
        {
            AbilityWork work = actor.gameObject.AddComponent<AbilityWork>();
            characterObjectFactory.InjectAddedAbility(work);
        }

        if (created)
        {
            actor.gameObject.name = record.DisplayName;
            actor.Initialize(record.SourceData);
            actor.Identity?.SetPersistentId(record.CustomerId);
            actor.transform.position = spawner.GetEntryDoorWorldPosition();
        }
        else
        {
            actor.EnsureRuntimeState();
            actor.Identity?.SetPersistentId(record.CustomerId);
            actor.RefreshAbilityCache();
            actor.GetAbility<AbilityWork>()?.Initializtion(actor.data);
        }

        PromoteActorToStaff(actor);
        if (created)
        {
            characterObjectFactory.Publish(actor.gameObject);
        }
        else
        {
            actor.gameObject.SetActive(true);
        }
        PromoteActorToStaff(actor);
        brain = actor.Brain;
        brain?.UseStaffWorkActions();
        brain?.RequestImmediateReplan(clearFailures: true);
        characterPopulationService.PromoteToStaff(actor);
        ApplyCampaignRecruitCatchUp(actor, record);
        if (!IsActiveStaffActor(actor))
        {
            if (created)
            {
                characterObjectFactory.Destroy(actor.gameObject);
            }

            actor = null;
            message = "Recruit activation did not produce an active staff actor.";
            return false;
        }

        message = created
            ? "Recruit character was placed as staff."
            : "Active visitor was converted to staff.";
        return true;
    }

    private static void EnsureWorkAbility(GameObject characterObject)
    {
        if (characterObject != null
            && characterObject.GetComponent<AbilityWork>() == null)
        {
            characterObject.AddComponent<AbilityWork>();
        }
    }

    private void ApplyCampaignRecruitCatchUp(CharacterActor actor, RegularCustomerRecord record)
    {
        CharacterProgression progression = actor != null ? actor.Progression : null;
        if (progression == null)
        {
            return;
        }

        progression.SetAutoChooseSkillDrafts(true);
        int completedTargets = offense.Capture().CompletedTargetCount;
        ApplyRecruitProficiencyCatchUp(
            actor,
            completedTargets,
            calendar.AbsoluteHour);

        int minimumLevel = EstimateCampaignRecruitLevel(
            record,
            completedTargets);

        if (!progression.EnsureMinimumLevel(
                minimumLevel,
                minimumLevel > 1 ? "원정 합류 훈련을 마쳤다." : string.Empty))
        {
            return;
        }

        actor.Heal(actor.MaxHealth);
        actor.Lifecycle?.RestoreExpeditionRecovery(new CharacterExpeditionRecoveryState());
    }

    private void ApplyRecruitProficiencyCatchUp(
        CharacterActor actor,
        int completedTargets,
        long absoluteHour)
    {
        int targetExperience = RecruitProficiencyCatchUpRules
            .ResolvePrimaryExperienceFloor(completedTargets);
        CharacterId characterId = (CharacterId)(
            actor?.Identity?.PersistentId ?? string.Empty);
        if (targetExperience <= 0 || !characterId.IsValid)
        {
            return;
        }

        CharacterProficiencyId probeId =
            BuiltInCharacterProficiencyIds.All[0];
        if (!proficiencyQuery.TryGetProficiency(
                characterId,
                probeId,
                absoluteHour,
                out _))
        {
            return;
        }
        IReadOnlyList<CharacterProficiencySnapshot> proficiencies =
            proficiencyQuery.GetAllProficiencies(
                characterId,
                absoluteHour);

        foreach (CharacterProficiencySnapshot proficiency in proficiencies
                     .OrderByDescending(value => value.CurrentMilliExperience)
                     .ThenBy(value => value.ProficiencyId.Value, StringComparer.Ordinal)
                     .Take(RecruitProficiencyCatchUpRules.SpecializedProficiencyCount))
        {
            float missing = targetExperience
                - proficiency.CurrentMilliExperience
                    / (float)ProficiencyProgressionRules.MilliPerExperience;
            if (missing <= 0f)
            {
                continue;
            }

            proficiencyCommands.AddDirectExperience(
                characterId,
                proficiency.ProficiencyId,
                missing,
                absoluteHour,
                applyLearningMultiplier: false);
        }
    }

    private static int GetCampaignRecruitMinimumLevel(int completedTargets)
    {
        return Mathf.Clamp(completedTargets, 0, 6) switch
        {
            0 => 1,
            1 => 18,
            2 => 32,
            3 => 44,
            _ => CharacterProgression.MaxLevel
        };
    }

    public static int EstimateCampaignRecruitLevel(
        RegularCustomerRecord record,
        IOffenseQuery offense)
    {
        if (offense == null) throw new ArgumentNullException(nameof(offense));
        return EstimateCampaignRecruitLevel(
            record,
            offense.Capture().CompletedTargetCount);
    }

    private static int EstimateCampaignRecruitLevel(
        RegularCustomerRecord record,
        int completedTargets)
    {
        int minimumLevel = GetCampaignRecruitMinimumLevel(completedTargets);
        if (record != null && record.VisitCount >= 3 && completedTargets >= 3)
        {
            minimumLevel = Mathf.Min(
                CharacterProgression.MaxLevel,
                minimumLevel + 2);
        }

        return minimumLevel;
    }

    private static bool MatchesRecord(CharacterActor actor, RegularCustomerRecord record)
    {
        actor = CharacterActorCollection.GetCanonical(actor);
        return actor != null
            && !actor.IsOwner
            && RegularCustomerService.GetCustomerId(actor) == record.CustomerId;
    }

    private static void PromoteActorToStaff(CharacterActor actor)
    {
        if (actor == null)
        {
            return;
        }

        actor.characterType = CharacterType.NPC;
        actor.Identity?.SetCharacterType(CharacterType.NPC);
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        actor.Lifecycle?.RestoreExpeditionRecovery(new CharacterExpeditionRecoveryState());
        actor.Heal(actor.MaxHealth);
        actor.RefreshAbilityCache();
        actor.GetAbility<AbilityWork>()?.Initializtion(actor.data);
    }

    private static bool IsActiveStaffActor(CharacterActor actor)
    {
        return actor != null
            && actor.gameObject.activeInHierarchy
            && actor.Identity != null
            && actor.Identity.CharacterType == CharacterType.NPC
            && actor.CurrentLifecycleState == CharacterLifecycleState.Active
            && actor.TryGetAbility(out AbilityWork _);
    }
}
