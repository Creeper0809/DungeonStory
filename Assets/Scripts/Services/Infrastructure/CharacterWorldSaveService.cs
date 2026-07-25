using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface ICharacterWorldSaveService
{
    DungeonCharacterWorldSaveData Capture(Grid grid);
    void PrepareForWorldRestore();
    int Restore(Grid grid, DungeonCharacterWorldSaveData source, DungeonGameRestoreReport report);
    bool TryGetPersistentId(CharacterActor actor, out string persistentId);
    string GetOrAssignPersistentId(CharacterActor actor);
    bool TryGetRestoredActor(string persistentId, out CharacterActor actor);
}

public interface ICharacterIdRegistry
{
    bool TryGetPersistentId(CharacterActor actor, out string persistentId);
    string GetOrAssignPersistentId(CharacterActor actor);
}

public sealed class CharacterWorldSaveService : ICharacterWorldSaveService, ICharacterIdRegistry
{
    private const int MaxSavedLogEntries = 30;

    private readonly ICharacterWorldQuery characterWorldQuery;
    private readonly ICharacterLifetimeQuery characterLifetimeQuery;
    private readonly IRunCharacterCatalog characterCatalog;
    private readonly IOwnerRunManagerProvider ownerRunManagerProvider;
    private readonly ICharacterSpawnerProvider characterSpawnerProvider;
    private readonly ICharacterSpawnObjectFactory characterObjectFactory;
    private readonly IRunVariableRuntimeProvider runVariableRuntimeProvider;
    private readonly ICharacterPopulationService characterPopulationService;
    private readonly ISocialReputationRuntimeProvider socialReputationRuntimeProvider;
    private readonly Dictionary<CharacterActor, string> capturedActorIds = new Dictionary<CharacterActor, string>();
    private readonly Dictionary<string, CharacterActor> restoredActorsById =
        new Dictionary<string, CharacterActor>(StringComparer.Ordinal);
    private int nextPersistentSequence;

    public CharacterWorldSaveService(
        ICharacterWorldQuery characterWorldQuery,
        ICharacterLifetimeQuery characterLifetimeQuery,
        IRunCharacterCatalog characterCatalog,
        IOwnerRunManagerProvider ownerRunManagerProvider,
        ICharacterSpawnerProvider characterSpawnerProvider,
        ICharacterSpawnObjectFactory characterObjectFactory,
        IRunVariableRuntimeProvider runVariableRuntimeProvider,
        ICharacterPopulationService characterPopulationService,
        ISocialReputationRuntimeProvider socialReputationRuntimeProvider)
    {
        this.characterWorldQuery = characterWorldQuery
            ?? throw new ArgumentNullException(nameof(characterWorldQuery));
        this.characterLifetimeQuery = characterLifetimeQuery
            ?? throw new ArgumentNullException(nameof(characterLifetimeQuery));
        this.characterCatalog = characterCatalog ?? throw new ArgumentNullException(nameof(characterCatalog));
        this.ownerRunManagerProvider = ownerRunManagerProvider ?? throw new ArgumentNullException(nameof(ownerRunManagerProvider));
        this.characterSpawnerProvider = characterSpawnerProvider ?? throw new ArgumentNullException(nameof(characterSpawnerProvider));
        this.characterObjectFactory = characterObjectFactory ?? throw new ArgumentNullException(nameof(characterObjectFactory));
        this.runVariableRuntimeProvider = runVariableRuntimeProvider
            ?? throw new ArgumentNullException(nameof(runVariableRuntimeProvider));
        this.characterPopulationService = characterPopulationService
            ?? throw new ArgumentNullException(nameof(characterPopulationService));
        this.socialReputationRuntimeProvider = socialReputationRuntimeProvider
            ?? throw new ArgumentNullException(nameof(socialReputationRuntimeProvider));
    }

    public DungeonCharacterWorldSaveData Capture(Grid grid)
    {
        if (grid == null)
        {
            throw new ArgumentNullException(nameof(grid));
        }

        DungeonCharacterWorldSaveData result = new DungeonCharacterWorldSaveData
        {
            populationProfiles = characterPopulationService.CaptureProfiles(),
            globalFacilityReputation =
                socialReputationRuntimeProvider.TryGetRuntime(out SocialReputationRuntime reputation)
                    ? reputation.CaptureSnapshot()
                    : new GlobalFacilityReputationSnapshot()
        };
        List<CharacterActor> persistentActors = CharacterActorCollection
            .DistinctByGameObject(characterLifetimeQuery.AllCharacters)
            .Where(IsPersistentActor)
            .OrderBy(actor => actor.IsOwner ? 0 : 1)
            .ThenBy(actor => actor.Identity.Data.id)
            .ThenBy(actor => grid.GetXY(actor.transform.position).y)
            .ThenBy(actor => grid.GetXY(actor.transform.position).x)
            .ToList();
        capturedActorIds.Clear();
        foreach (CharacterActor actor in persistentActors)
        {
            string persistentId = GetOrAssignPersistentId(actor);

            DungeonCharacterSaveData actorSave = CaptureActor(grid, actor);
            actorSave.persistentId = persistentId;
            result.actors.Add(actorSave);
            capturedActorIds[actor] = persistentId;
        }

        EnsureUniqueIds(result.actors.Select(actor => actor.persistentId), "save capture");
        EnsureUniqueIds(result.populationProfiles.Select(profile => profile.persistentId), "population capture");
        EnsureUniqueIds(
            result.actors.Select(actor => actor.persistentId)
                .Concat(result.populationProfiles.Select(profile => profile.persistentId)),
            "character world capture",
            allowActorProfileOverlap: true);

        return result;
    }

    public bool TryGetPersistentId(CharacterActor actor, out string persistentId)
    {
        actor = CharacterActorCollection.GetCanonical(actor);
        persistentId = actor != null && actor.Identity != null
            ? actor.Identity.PersistentId
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(persistentId))
        {
            return true;
        }

        return actor != null && capturedActorIds.TryGetValue(actor, out persistentId);
    }

    public string GetOrAssignPersistentId(CharacterActor actor)
    {
        actor = CharacterActorCollection.GetCanonical(actor);
        if (actor == null)
        {
            throw new ArgumentNullException(nameof(actor));
        }

        actor.EnsureRuntimeState();
        CharacterIdentity identity = actor.Identity
            ?? throw new InvalidOperationException("CharacterActor requires CharacterIdentity.");
        if (identity.IsOwner)
        {
            identity.SetPersistentId("owner");
            capturedActorIds[actor] = "owner";
            return "owner";
        }

        if (!string.IsNullOrWhiteSpace(identity.PersistentId))
        {
            capturedActorIds[actor] = identity.PersistentId;
            return identity.PersistentId;
        }

        int runSeed = runVariableRuntimeProvider.TryGetRuntime(out RunVariableRuntime runVariables)
            ? runVariables.RunSeed
            : 0;
        HashSet<string> usedIds = characterLifetimeQuery.AllCharacters
            .Where(value => value != null && value.Identity != null)
            .Select(value => value.Identity.PersistentId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Concat(restoredActorsById.Keys)
            .ToHashSet(StringComparer.Ordinal);
        string candidate;
        do
        {
            candidate = $"staff:{runSeed}:{nextPersistentSequence++:D6}";
        }
        while (usedIds.Contains(candidate));

        identity.SetPersistentId(candidate);
        capturedActorIds[actor] = candidate;
        return candidate;
    }

    public bool TryGetRestoredActor(string persistentId, out CharacterActor actor)
    {
        actor = null;
        return !string.IsNullOrWhiteSpace(persistentId)
            && restoredActorsById.TryGetValue(persistentId, out actor)
            && actor != null;
    }

    public void PrepareForWorldRestore()
    {
        foreach (CharacterActor actor in CharacterActorCollection.DistinctByGameObject(
            characterWorldQuery.Characters))
        {
            if (actor == null)
            {
                continue;
            }

            actor.GetAbility<AbilityWork>()?.ReleaseAssignedWorkTarget();
            actor.GetAbility<AbilityMove>()?.CancelActiveMovement();
            actor.Brain?.RequestImmediateReplan(clearFailures: true);
        }
    }

    public int Restore(Grid grid, DungeonCharacterWorldSaveData source, DungeonGameRestoreReport report)
    {
        if (grid == null)
        {
            throw new ArgumentNullException(nameof(grid));
        }

        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        characterPopulationService.RestoreProfiles(source?.populationProfiles);
        if (socialReputationRuntimeProvider.TryGetRuntime(
                out SocialReputationRuntime reputation))
        {
            reputation.RestoreSnapshot(source?.globalFacilityReputation);
        }
        List<DungeonCharacterSaveData> savedActors = source?.actors?
            .Where(actor => actor != null)
            .ToList() ?? new List<DungeonCharacterSaveData>();
        EnsureUniqueIds(savedActors.Select(actor => actor.persistentId), "character restore");
        EnsureUniqueIds(
            source?.populationProfiles?.Where(profile => profile != null).Select(profile => profile.persistentId)
                ?? Enumerable.Empty<string>(),
            "population restore");
        if (savedActors.Count == 0)
        {
            report.AddWarning("The save contains no persistent character state; current actors were preserved.");
            return 0;
        }

        restoredActorsById.Clear();
        capturedActorIds.Clear();

        Dictionary<int, CharacterSO> charactersById = characterCatalog.Characters
            .Where(data => data != null)
            .GroupBy(data => data.id)
            .ToDictionary(group => group.Key, group => group.First());
        foreach (CharacterActor existingActor in CharacterActorCollection.DistinctByGameObject(
            characterLifetimeQuery.AllCharacters))
        {
            CharacterSO runtimeData = existingActor != null && existingActor.Identity != null
                ? existingActor.Identity.Data
                : null;
            if (runtimeData != null && !charactersById.ContainsKey(runtimeData.id))
            {
                charactersById.Add(runtimeData.id, runtimeData);
            }
        }

        List<CharacterActor> availableStaff = FindExistingStaff();

        int restoredCount = 0;
        DungeonCharacterSaveData ownerSave = savedActors.FirstOrDefault(actor => actor.isOwner);
        if (ownerSave != null)
        {
            if (!charactersById.TryGetValue(ownerSave.dataId, out CharacterSO ownerData))
            {
                report.AddWarning($"Owner character data {ownerSave.dataId} no longer exists.");
            }
            else if (!ownerRunManagerProvider.TryGetManager(out OwnerRunManager manager))
            {
                report.AddWarning("Owner manager was not present; owner state was skipped.");
            }
            else
            {
                CharacterActor owner = manager.RestoreOwner(ownerData);
                owner.PrepareForPersistentRestore();
                ApplyActorState(grid, owner, ownerSave, report);
                RegisterRestoredActor(ownerSave, owner);
                restoredCount++;
            }
        }
        else
        {
            report.AddWarning("The save contains no owner character; the current owner was preserved.");
        }

        List<DungeonCharacterSaveData> staffSaves = savedActors.Where(actor => !actor.isOwner).ToList();
        CharacterSpawner spawner = null;
        bool spawnerResolved = false;

        foreach (DungeonCharacterSaveData staffSave in staffSaves)
        {
            if (!charactersById.TryGetValue(staffSave.dataId, out CharacterSO staffData))
            {
                report.AddWarning($"Staff character data {staffSave.dataId} no longer exists.");
                continue;
            }

            int reusableIndex = availableStaff.FindIndex(actor => actor != null
                && actor.Identity != null
                && actor.Identity.Data != null
                && actor.Identity.Data.id == staffSave.dataId);
            CharacterActor staff = reusableIndex >= 0 ? availableStaff[reusableIndex] : null;
            if (reusableIndex >= 0)
            {
                availableStaff.RemoveAt(reusableIndex);
            }

            if (staff == null)
            {
                if (!spawnerResolved)
                {
                    spawnerResolved = characterSpawnerProvider.TryGetSpawner(out spawner)
                        && spawner.characterPrefab != null;
                }

                if (!spawnerResolved)
                {
                    report.AddWarning($"Character spawner prefab was not present; staff {staffSave.dataId} was skipped.");
                    continue;
                }

                GameObject createdObject = characterObjectFactory.Create(spawner.characterPrefab);
                characterObjectFactory.Inject(createdObject);
                staff = CharacterActorCollection.GetCanonical(
                    createdObject.GetComponent<CharacterActor>());
                if (staff == null)
                {
                    createdObject.SetActive(false);
                    characterObjectFactory.Destroy(createdObject);
                    report.AddWarning($"Character prefab could not restore staff {staffSave.dataId}: CharacterActor is missing.");
                    continue;
                }
            }

            GameObject staffObject = staff.gameObject;
            staffObject.name = string.IsNullOrWhiteSpace(staffSave.displayName)
                ? staffData.characterName
                : staffSave.displayName;
            staff.EnsureRuntimeState();
            staff.PrepareForPersistentRestore();
            staff.SetLifecycleState(CharacterLifecycleState.Active);
            staff.Initialize(staffData);
            staff.characterType = staffSave.characterType;
            staffObject.SetActive(true);
            ApplyActorState(grid, staff, staffSave, report);
            RegisterRestoredActor(staffSave, staff);
            restoredCount++;
        }

        foreach (CharacterActor extraStaff in availableStaff)
        {
            if (extraStaff == null)
            {
                continue;
            }

            extraStaff.gameObject.SetActive(false);
            characterObjectFactory.Destroy(extraStaff.gameObject);
        }

        return restoredCount;
    }

    private void RegisterRestoredActor(DungeonCharacterSaveData source, CharacterActor actor)
    {
        if (source == null || actor == null || string.IsNullOrWhiteSpace(source.persistentId))
        {
            return;
        }

        if (restoredActorsById.ContainsKey(source.persistentId))
        {
            throw new InvalidOperationException(
                $"Duplicate persistent character ID '{source.persistentId}' encountered during restore.");
        }

        restoredActorsById.Add(source.persistentId, actor);
        actor.Identity?.SetPersistentId(source.persistentId);
        capturedActorIds[actor] = source.persistentId;
    }

    private static bool IsPersistentActor(CharacterActor actor)
    {
        CharacterIdentity identity = actor != null ? actor.Identity : null;
        return actor != null
            && actor.gameObject.activeInHierarchy
            && identity != null
            && identity.Data != null
            && !actor.IsDead
            && actor.CurrentLifecycleState != CharacterLifecycleState.Despawned
            && (actor.IsOwner
                || (identity.CharacterType == CharacterType.NPC
                    && actor.TryGetAbility(out AbilityWork _)));
    }

    private static void EnsureUniqueIds(
        IEnumerable<string> ids,
        string operation,
        bool allowActorProfileOverlap = false)
    {
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string rawId in ids ?? Enumerable.Empty<string>())
        {
            string id = rawId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException($"A character without a persistent ID was found during {operation}.");
            }

            if (!seen.Add(id) && !allowActorProfileOverlap)
            {
                throw new InvalidOperationException(
                    $"Duplicate persistent character ID '{id}' was found during {operation}.");
            }
        }
    }

    private static DungeonCharacterSaveData CaptureActor(Grid grid, CharacterActor actor)
    {
        CharacterIdentity identity = actor.Identity;
        Vector2Int gridPosition = grid.GetXY(actor.transform.position);
        CharacterMoodSnapshot mood = actor.Stats.GetMoodSnapshot();
        actor.TryGetAbility(out AbilityWork work);
        actor.TryGetAbility(out AbilityShopping shopping);
        CharacterProgressionSnapshot progression = actor.Progression?.CapturePersistentState();

        return new DungeonCharacterSaveData
        {
            persistentId = identity.PersistentId,
            dataId = identity.Data.id,
            isOwner = actor.IsOwner,
            displayName = identity.DisplayName,
            characterType = identity.CharacterType,
            role = identity.Role,
            gridX = gridPosition.x,
            gridY = gridPosition.y,
            lifecycleState = actor.CurrentLifecycleState,
            currentHealth = actor.CurrentHealth,
            injurySeverity = actor.InjurySeverity,
            baseMood = mood.BaseValue,
            conditions = actor.Stats.StatSnapshot
                .OrderBy(pair => pair.Key)
                .Select(pair => new DungeonCharacterConditionSaveData
                {
                    condition = pair.Key,
                    value = pair.Value
                })
                .ToList(),
            moodFactors = mood.Factors
                .Where(factor => factor != null && factor.Kind == CharacterMoodFactorKind.Interaction)
                .Select(factor => new DungeonCharacterMoodFactorSaveData
                {
                    id = factor.Id,
                    label = factor.Label,
                    value = factor.Value,
                    remainingSeconds = factor.RemainingSeconds
                })
                .ToList(),
            workPriorities = work?.WorkPriorities?.Entries
                .Where(entry => entry != null)
                .Select(entry => new DungeonCharacterWorkPrioritySaveData
                {
                    workTypeId = entry.WorkTypeId,
                    priority = entry.Priority
                })
                .ToList() ?? new List<DungeonCharacterWorkPrioritySaveData>(),
            dutyState = work != null ? work.CurrentDutyState : AbilityWork.DutyState.OnDuty,
            visitCount = shopping?.visitCount ?? 0,
            lookAroundCount = shopping?.lookAroundCount ?? 0,
            holdingMoney = shopping?.HoldingMoney ?? 0,
            recentLogEntries = actor.LogComponent?.Entries
                .TakeLast(MaxSavedLogEntries)
                .ToList() ?? new List<string>(),
            level = progression?.Level ?? 1,
            currentExperience = progression?.CurrentExperience ?? 0,
            learnedSkillIds = progression?.LearnedSkillIds.ToList() ?? new List<string>(),
            equippedSkillIds = progression?.EquippedSkillIds.ToList() ?? new List<string>(),
            growth = progression?.GrowthState?.Clone() ?? new CharacterGrowthState(),
            narrative = progression?.NarrativeLedger?.Clone() ?? new CharacterNarrativeLedger(),
            socialMemory = actor.SocialMemory?.CaptureSnapshot() ?? new CharacterSocialMemorySnapshot(),
            expeditionRecovery = actor.Lifecycle?.ExpeditionRecovery?.Clone()
                ?? new CharacterExpeditionRecoveryState(),
            carryInventory = actor.GetComponent<CharacterCarryInventory>()?.Capture()
                ?? new CharacterCarryInventorySaveData()
        };
    }

    private List<CharacterActor> FindExistingStaff()
    {
        return CharacterActorCollection
            .DistinctByGameObject(characterWorldQuery.Characters)
            .Where(actor => actor != null
                && actor.gameObject.activeInHierarchy
                && !actor.IsOwner
                && actor.Identity != null
                && actor.Identity.Data != null
                && actor.Identity.CharacterType == CharacterType.NPC
                && actor.GetAbility<AbilityWork>() != null)
            .OrderBy(actor => actor.Identity.Data.id)
            .ThenBy(actor => actor.GetInstanceID())
            .ToList();
    }

    private static void ApplyActorState(
        Grid grid,
        CharacterActor actor,
        DungeonCharacterSaveData source,
        DungeonGameRestoreReport report)
    {
        Vector2Int requestedPosition = new Vector2Int(source.gridX, source.gridY);
        if (!grid.IsValidGridPos(requestedPosition))
        {
            if (!grid.TryFindNearestWalkablePosition(requestedPosition, out requestedPosition))
            {
                report.AddWarning($"Character {source.dataId} has no valid restore position.");
                requestedPosition = grid.GetXY(actor.transform.position);
            }
            else
            {
                report.AddWarning($"Character {source.dataId} moved to the nearest valid grid cell during restore.");
            }
        }

        actor.transform.position = grid.GetWorldPos(requestedPosition);

        Dictionary<CharacterCondition, float> conditions = (source.conditions
                ?? new List<DungeonCharacterConditionSaveData>())
            .Where(entry => entry != null)
            .GroupBy(entry => entry.condition)
            .ToDictionary(group => group.Key, group => group.Last().value);
        List<CharacterMoodFactorSnapshot> moodFactors = (source.moodFactors
                ?? new List<DungeonCharacterMoodFactorSaveData>())
            .Where(factor => factor != null)
            .Select(factor => new CharacterMoodFactorSnapshot(
                factor.id,
                factor.label,
                factor.value,
                CharacterMoodFactorKind.Interaction,
                factor.remainingSeconds))
            .ToList();
        actor.Stats.RestorePersistentState(
            conditions,
            source.currentHealth,
            source.injurySeverity,
            source.baseMood,
            moodFactors);
        actor.Lifecycle?.RestoreExpeditionRecovery(source.expeditionRecovery);

        actor.SetLifecycleState(CharacterLifecycleState.Active);
        if (source.lifecycleState == CharacterLifecycleState.OnExpedition)
        {
            actor.BeginExpedition();
        }
        else if (source.lifecycleState != CharacterLifecycleState.Active)
        {
            report.AddWarning(
                $"Character {source.dataId} lifecycle {source.lifecycleState} was normalized to Active during restore.");
        }

        AbilityWork work = actor.GetAbility<AbilityWork>();
        if (work != null)
        {
            work.ClearPriorityWorkTarget();
            foreach (DungeonCharacterWorkPrioritySaveData priority in source.workPriorities
                ?? new List<DungeonCharacterWorkPrioritySaveData>())
            {
                if (priority != null
                    && WorkTypeCatalog.TryGet(priority.workTypeId, out WorkTypeDefinition definition))
                {
                    work.SetWorkPriority(definition.WorkTypeId, priority.priority);
                }
            }

            work.SetDutyState(source.dutyState);
        }

        actor.GetAbility<AbilityShopping>()?.RestorePersistentState(
            source.visitCount,
            source.lookAroundCount,
            source.holdingMoney);
        actor.Progression?.RestorePersistentState(new CharacterProgressionSnapshot(
            source.level,
            source.currentExperience,
            source.growth ?? new CharacterGrowthState(),
            source.narrative ?? new CharacterNarrativeLedger()));
        CharacterCarryInventory.Ensure(actor)?.Restore(source.carryInventory);
        actor.SocialMemory?.RestoreSnapshot(source.socialMemory);
        actor.LogComponent?.RestoreVisibleEntries(source.recentLogEntries ?? new List<string>());
        actor.state = CharacterDecisionState.DECIDE;
        actor.Brain?.RequestImmediateReplan(clearFailures: true);
    }
}
