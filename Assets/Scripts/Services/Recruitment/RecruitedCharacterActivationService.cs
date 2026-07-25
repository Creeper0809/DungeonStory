using System;
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
    private readonly IOffenseWorldMapRuntimeProvider worldMapProvider;

    public RecruitedCharacterActivationService(
        ICharacterWorldQuery characterWorld,
        ICharacterSpawnerProvider spawnerProvider,
        ICharacterSpawnObjectFactory characterObjectFactory,
        ICharacterPopulationService characterPopulationService,
        IOffenseWorldMapRuntimeProvider worldMapProvider = null)
    {
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.spawnerProvider = spawnerProvider ?? throw new ArgumentNullException(nameof(spawnerProvider));
        this.characterObjectFactory = characterObjectFactory
            ?? throw new ArgumentNullException(nameof(characterObjectFactory));
        this.characterPopulationService = characterPopulationService
            ?? throw new ArgumentNullException(nameof(characterPopulationService));
        this.worldMapProvider = worldMapProvider;
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

            GameObject createdObject = characterObjectFactory.Create(spawner.characterPrefab);
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

        bool startMayRunAfterActivation = created || !actor.gameObject.activeInHierarchy;
        if (startMayRunAfterActivation)
        {
            actor.PrepareForPersistentRestore();
        }

        AIBrain brain = actor.Brain;
        brain?.StopCurrentActionForReplan("Recruited as dungeon staff.");
        actor.GetAbility<AbilityMove>()?.CancelActiveMovement();
        actor.Blackboard?.ClearMacroGoal("Recruited as dungeon staff.");
        actor.Blackboard?.ClearMoodImpulse("Recruited as dungeon staff.");

        if (actor.GetComponent<AbilityWork>() == null)
        {
            actor.gameObject.AddComponent<AbilityWork>();
        }

        characterObjectFactory.Inject(actor.gameObject);
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
        actor.gameObject.SetActive(true);
        PromoteActorToStaff(actor);
        ApplyCampaignRecruitCatchUp(actor, record);

        brain = actor.Brain;
        brain?.UseStaffWorkActions();
        brain?.RequestImmediateReplan(clearFailures: true);
        characterPopulationService.PromoteToStaff(actor);
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

    private void ApplyCampaignRecruitCatchUp(CharacterActor actor, RegularCustomerRecord record)
    {
        CharacterProgression progression = actor != null ? actor.Progression : null;
        if (progression == null)
        {
            return;
        }

        progression.SetAutoChooseSkillDrafts(true);
        int completedTargets = 0;
        if (worldMapProvider != null
            && worldMapProvider.TryGetRuntime(out OffenseWorldMapRuntime worldMap)
            && worldMap?.State != null)
        {
            completedTargets = worldMap.State.CompletedTargetCount;
        }

        int minimumLevel = GetCampaignRecruitMinimumLevel(completedTargets);
        if (record != null && record.VisitCount >= 3 && completedTargets >= 3)
        {
            minimumLevel = Mathf.Min(CharacterProgression.MaxLevel, minimumLevel + 2);
        }

        if (!progression.EnsureMinimumLevel(
                minimumLevel,
                minimumLevel > 1 ? "원정 합류 훈련을 마쳤다." : string.Empty))
        {
            return;
        }

        actor.Heal(actor.MaxHealth);
        actor.Lifecycle?.RestoreExpeditionRecovery(new CharacterExpeditionRecoveryState());
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
