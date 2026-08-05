using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface ICharacterWorldSaveService
{
    DungeonCharacterWorldSaveData Capture(Grid grid);
    void ValidateRestorePayload(Grid grid, DungeonCharacterWorldSaveData source);
    CharacterWorldRestoreCandidate PrepareRestoreCandidate(
        Grid grid,
        DungeonCharacterWorldSaveData source);
    void StageRestoreCandidate(CharacterWorldRestoreCandidate candidate);
    bool TryGetPersistentId(CharacterActor actor, out string persistentId);
    string GetOrAssignPersistentId(CharacterActor actor);
    bool TryGetRestoredActor(string persistentId, out CharacterActor actor);
}

public sealed class CharacterWorldRestoreCandidate :
    IDungeonDiscardableRestoreCandidate,
    IDungeonRestoreReportContributor
{
    private readonly CharacterWorldSaveService owner;
    private CharacterWorldSaveService.DetachedCharacterWorldCandidate world;

    internal CharacterWorldRestoreCandidate(
        CharacterWorldSaveService owner,
        CharacterWorldSaveService.DetachedCharacterWorldCandidate world,
        int restoredCount)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        RestoredCount = restoredCount;
    }

    public int RestoredCount { get; }

    internal CharacterWorldSaveService.DetachedCharacterWorldCandidate Take(
        CharacterWorldSaveService expectedOwner)
    {
        if (!ReferenceEquals(owner, expectedOwner) || world == null)
        {
            throw new InvalidOperationException(
                "Character-world restore candidate has the wrong owner or was already consumed.");
        }
        CharacterWorldSaveService.DetachedCharacterWorldCandidate result = world;
        world = null;
        return result;
    }

    public void Discard()
    {
        if (world == null)
        {
            return;
        }
        owner.DiscardPreparedCandidate(world);
        world = null;
    }

    public void RecordRestoreResult(DungeonGameRestoreReport report)
    {
        (report ?? throw new ArgumentNullException(nameof(report)))
            .RecordRestoredCharacters(RestoredCount);
    }
}

public interface ICharacterIdRegistry
{
    bool TryGetPersistentId(CharacterActor actor, out string persistentId);
    string GetOrAssignPersistentId(CharacterActor actor);
}

/// <summary>
/// Cohesive character-construction boundary used only while rebuilding a
/// detached character world. Keeping these factories together prevents the
/// save coordinator from becoming the composition root for character spawn.
/// </summary>
public sealed class CharacterWorldSpawnDependencies
{
    public CharacterWorldSpawnDependencies(
        IRunCharacterCatalog characterCatalog,
        IOwnerRunManagerProvider ownerRunManagerProvider,
        ICharacterSpawnerProvider characterSpawnerProvider,
        ICharacterSpawnObjectFactory characterObjectFactory)
    {
        CharacterCatalog = characterCatalog
            ?? throw new ArgumentNullException(nameof(characterCatalog));
        OwnerRunManagerProvider = ownerRunManagerProvider
            ?? throw new ArgumentNullException(nameof(ownerRunManagerProvider));
        CharacterSpawnerProvider = characterSpawnerProvider
            ?? throw new ArgumentNullException(nameof(characterSpawnerProvider));
        CharacterObjectFactory = characterObjectFactory
            ?? throw new ArgumentNullException(nameof(characterObjectFactory));
    }

    public IRunCharacterCatalog CharacterCatalog { get; }
    public IOwnerRunManagerProvider OwnerRunManagerProvider { get; }
    public ICharacterSpawnerProvider CharacterSpawnerProvider { get; }
    public ICharacterSpawnObjectFactory CharacterObjectFactory { get; }
}

public sealed class CharacterWorldSaveService :
    ICharacterWorldSaveService,
    IDungeonRestoreTransactionParticipant
{
    private const int MaxSavedLogEntries = 30;

    private readonly ICharacterWorldQuery characterWorldQuery;
    private readonly ICharacterLifetimeQuery characterLifetimeQuery;
    private readonly IRunCharacterCatalog characterCatalog;
    private readonly IOwnerRunManagerProvider ownerRunManagerProvider;
    private readonly ICharacterSpawnerProvider characterSpawnerProvider;
    private readonly ICharacterSpawnObjectFactory characterObjectFactory;
    private readonly ICharacterPopulationService characterPopulationService;
    private readonly SocialReputationRuntime socialReputation;
    private readonly ICharacterIdRegistry characterIds;
    private readonly IRestoreWorldCandidatePublisher restoreWorldCandidates;
    private IReadOnlyDictionary<string, CharacterActor> restoredActorsById =
        new Dictionary<string, CharacterActor>(StringComparer.Ordinal);
    private bool restoreTransactionActive;
    private DetachedCharacterWorldCandidate preparedCandidate;
    private DetachedCharacterWorldCandidate stagedCandidate;
    private CharacterWorldPublication activePublication;

    public string ParticipantId => "200.world.characters";

    public CharacterWorldSaveService(
        ICharacterWorldQuery characterWorldQuery,
        ICharacterLifetimeQuery characterLifetimeQuery,
        CharacterWorldSpawnDependencies spawning,
        ICharacterPopulationService characterPopulationService,
        CharacterSceneRuntimeReferences characterRuntimes,
        ICharacterIdRegistry characterIds,
        IRestoreWorldCandidatePublisher restoreWorldCandidates)
    {
        this.characterWorldQuery = characterWorldQuery
            ?? throw new ArgumentNullException(nameof(characterWorldQuery));
        this.characterLifetimeQuery = characterLifetimeQuery
            ?? throw new ArgumentNullException(nameof(characterLifetimeQuery));
        spawning = spawning ?? throw new ArgumentNullException(nameof(spawning));
        characterCatalog = spawning.CharacterCatalog;
        ownerRunManagerProvider = spawning.OwnerRunManagerProvider;
        characterSpawnerProvider = spawning.CharacterSpawnerProvider;
        characterObjectFactory = spawning.CharacterObjectFactory;
        this.characterPopulationService = characterPopulationService
            ?? throw new ArgumentNullException(nameof(characterPopulationService));
        socialReputation = (characterRuntimes
                ?? throw new ArgumentNullException(nameof(characterRuntimes)))
            .SocialReputation
            ?? throw new InvalidOperationException(
                $"{nameof(CharacterWorldSaveService)} requires a loaded {nameof(SocialReputationRuntime)}.");
        this.characterIds = characterIds
            ?? throw new ArgumentNullException(nameof(characterIds));
        this.restoreWorldCandidates = restoreWorldCandidates
            ?? throw new ArgumentNullException(nameof(restoreWorldCandidates));
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
            globalFacilityReputation = socialReputation.CaptureSnapshot()
        };
        if (result.populationProfiles == null)
        {
            throw new InvalidOperationException(
                "Character population capture returned a null profile collection.");
        }

        if (result.globalFacilityReputation == null)
        {
            throw new InvalidOperationException(
                "Character reputation capture returned a null snapshot.");
        }

        List<CharacterActor> persistentActors = CharacterActorCollection
            .DistinctByGameObject(characterLifetimeQuery.AllCharacters)
            .Where(IsPersistentActor)
            .OrderBy(actor => actor.IsOwner ? 0 : 1)
            .ThenBy(actor => actor.Identity.Data.id)
            .ThenBy(actor => grid.GetXY(actor.transform.position).y)
            .ThenBy(actor => grid.GetXY(actor.transform.position).x)
            .ToList();
        int ownerCount = persistentActors.Count(actor => actor.IsOwner);
        if (ownerCount != 1)
        {
            throw new InvalidOperationException(
                $"Character world capture requires exactly one owner actor, but found {ownerCount}.");
        }

        foreach (CharacterActor actor in persistentActors)
        {
            string persistentId = GetOrAssignPersistentId(actor);

            DungeonCharacterSaveData actorSave = CaptureActor(grid, actor);
            actorSave.persistentId = persistentId;
            result.actors.Add(actorSave);
        }

        EnsureUniqueIds(result.actors.Select(actor => actor.persistentId), "save capture");
        EnsureUniqueIds(result.populationProfiles.Select(profile => profile.persistentId), "population capture");
        EnsureUniqueIds(
            result.actors.Select(actor => actor.persistentId)
                .Concat(result.populationProfiles.Select(profile => profile.persistentId)),
            "character world capture",
            allowActorProfileOverlap: true);

        DungeonGameRestoreReport validation = new DungeonGameRestoreReport();
        ValidateRestore(grid, result, validation);
        if (!validation.Success)
        {
            throw new InvalidOperationException(
                $"Character world capture produced a non-canonical V18 payload: {string.Join(" | ", validation.Errors)}");
        }

        return result;
    }

    public bool TryGetPersistentId(CharacterActor actor, out string persistentId)
    {
        return characterIds.TryGetPersistentId(actor, out persistentId);
    }

    public string GetOrAssignPersistentId(CharacterActor actor)
    {
        return characterIds.GetOrAssignPersistentId(actor);
    }

    public bool TryGetRestoredActor(string persistentId, out CharacterActor actor)
    {
        actor = null;
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            return false;
        }

        IReadOnlyDictionary<string, CharacterActor> source =
            preparedCandidate != null
                ? preparedCandidate.ActorsById
                : stagedCandidate != null
                    ? stagedCandidate.ActorsById
                    : restoredActorsById;
        return source.TryGetValue(persistentId, out actor)
            && actor != null;
    }

    public void BeginRestoreCandidate()
    {
        if (restoreTransactionActive || activePublication != null)
        {
            throw new InvalidOperationException(
                "A character world restore candidate is already active.");
        }

        restoreTransactionActive = true;
        stagedCandidate = null;
    }

    public void PublishRestoreCandidate()
    {
        if (!restoreTransactionActive
            || stagedCandidate == null
            || activePublication != null)
        {
            throw new InvalidOperationException(
                "No character world restore candidate is ready to publish.");
        }

        CharacterWorldPublication publication = new CharacterWorldPublication(
            stagedCandidate,
            restoredActorsById);
        activePublication = publication;
        PublishCharacterCandidate(publication);
    }

    public void RollbackPublishedRestoreCandidate()
    {
        if (!restoreTransactionActive || activePublication == null)
        {
            DiscardRestoreCandidate();
            return;
        }

        CharacterWorldPublication publication = activePublication;
        List<Exception> failures = new List<Exception>();
        try
        {
            RollbackCharacterPublication(publication);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
        try
        {
            restoreWorldCandidates.ClearCharacterCandidate();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
        try
        {
            DestroyCharacterCandidates(
                publication.Candidate.Characters,
                requireDetached: false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
        finally
        {
            activePublication = null;
            stagedCandidate = null;
            restoreTransactionActive = false;
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                "Character world publication rollback encountered one or more failures after attempting every reversal.",
                failures);
        }
    }

    public void CompleteRestoreCandidate()
    {
        if (!restoreTransactionActive || activePublication == null)
        {
            throw new InvalidOperationException(
                "No published character world restore candidate is ready to complete.");
        }

        CompleteCharacterPublication(activePublication);
        restoreWorldCandidates.ClearCharacterCandidate();
        activePublication = null;
        stagedCandidate = null;
        restoreTransactionActive = false;
    }

    public void DiscardRestoreCandidate()
    {
        if (activePublication != null)
        {
            RollbackPublishedRestoreCandidate();
            return;
        }

        restoreWorldCandidates.ClearCharacterCandidate();
        if (stagedCandidate != null)
        {
            DestroyCharacterCandidates(stagedCandidate.Characters);
        }
        stagedCandidate = null;
        restoreTransactionActive = false;
    }

    private static void PrepareForWorldRetirement(
        IEnumerable<CharacterActor> retiringActors)
    {
        foreach (CharacterActor actor in CharacterActorCollection.DistinctByGameObject(
            retiringActors))
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

    private void ValidateRestore(
        Grid grid,
        DungeonCharacterWorldSaveData source,
        DungeonGameRestoreReport report)
    {
        if (grid == null)
        {
            throw new ArgumentNullException(nameof(grid));
        }

        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (source == null)
        {
            report.AddError("Character world payload is missing.");
            return;
        }

        if (source.actors == null)
        {
            report.AddError("Character world actor collection is missing.");
        }

        if (source.populationProfiles == null)
        {
            report.AddError("Character world population profile collection is missing.");
        }

        if (source.globalFacilityReputation == null)
        {
            report.AddError("Character world reputation snapshot is missing.");
        }

        List<DungeonCharacterSaveData> actors = source.actors
            ?? new List<DungeonCharacterSaveData>();
        List<WorldCharacterProfile> profiles = source.populationProfiles
            ?? new List<WorldCharacterProfile>();
        if (actors.Any(actor => actor == null))
        {
            report.AddError("Character world payload contains a null actor entry.");
        }

        if (profiles.Any(profile => profile == null))
        {
            report.AddError("Character world payload contains a null population profile.");
        }

        DungeonCharacterSaveData[] concreteActors = actors
            .Where(actor => actor != null)
            .ToArray();
        WorldCharacterProfile[] concreteProfiles = profiles
            .Where(profile => profile != null)
            .ToArray();
        ValidateUniquePersistentIds(
            concreteActors.Select(actor => actor.persistentId),
            "character actor",
            report);
        ValidateUniquePersistentIds(
            concreteProfiles.Select(profile => profile.persistentId),
            "population profile",
            report);

        int ownerCount = concreteActors.Count(actor => actor.isOwner);
        if (ownerCount != 1)
        {
            report.AddError(
                $"Character world payload requires exactly one owner actor, but contains {ownerCount}.");
        }

        Dictionary<int, CharacterSO> charactersById = characterCatalog.Characters
            .Where(data => data != null)
            .GroupBy(data => data.id)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (DungeonCharacterSaveData actor in concreteActors)
        {
            CharacterId id = (CharacterId)actor.persistentId;
            if (!id.IsValid)
            {
                report.AddError(
                    $"Character data {actor.dataId} has no valid persistent ID.");
            }
            else if (actor.isOwner && !id.Equals(CharacterId.Owner))
            {
                report.AddError(
                    $"Owner character uses persistent ID '{id.Value}' instead of '{CharacterId.Owner.Value}'.");
            }
            else if (!actor.isOwner && id.Equals(CharacterId.Owner))
            {
                report.AddError(
                    "A non-owner character uses the reserved owner persistent ID.");
            }

            if (!charactersById.TryGetValue(actor.dataId, out CharacterSO definition))
            {
                report.AddError(
                    $"Character definition {actor.dataId} does not exist in the run catalog.");
            }
            else if (actor.role != definition.role)
            {
                report.AddError(
                    $"Character '{id.Value}' role '{actor.role}' does not match authored role '{definition.role}'.");
            }

            if (!Enum.IsDefined(typeof(CharacterType), actor.characterType)
                || !Enum.IsDefined(typeof(CharacterRole), actor.role)
                || !Enum.IsDefined(
                    typeof(CharacterLifecycleState),
                    actor.lifecycleState)
                || !Enum.IsDefined(
                    typeof(AbilityWork.DutyState),
                    actor.dutyState))
            {
                report.AddError(
                    $"Character '{id.Value}' contains an unknown enum value.");
            }

            if (!IsFinite(actor.currentHealth)
                || !IsFinite(actor.injurySeverity)
                || !IsFinite(actor.baseMood))
            {
                report.AddError(
                    $"Character '{id.Value}' contains a non-finite health or mood value.");
            }

            Vector2Int position = new Vector2Int(actor.gridX, actor.gridY);
            if (RequiresWalkableRestoreCell(actor.lifecycleState)
                && (!grid.IsValidGridPos(position) || !grid.IsWalkable(position)))
            {
                report.AddError(
                    $"Character '{id.Value}' restore cell ({actor.gridX}, {actor.gridY}) is not walkable in the candidate grid.");
            }

            CharacterWorldSaveValidation.ValidateActor(actor, id.Value, report);
        }

        foreach (WorldCharacterProfile profile in concreteProfiles)
        {
            CharacterId id = (CharacterId)profile.persistentId;
            if (!id.IsValid)
            {
                report.AddError(
                    $"Population profile {profile.characterDataId} has no valid persistent ID.");
            }

            if (!charactersById.ContainsKey(profile.characterDataId))
            {
                report.AddError(
                    $"Population profile '{id.Value}' references missing character definition {profile.characterDataId}.");
            }
        }

        CharacterWorldSaveValidation.ValidatePopulationProfiles(
            concreteProfiles,
            report);
        CharacterWorldSaveValidation.ValidateReputation(
            source.globalFacilityReputation,
            report);
    }

    public void ValidateRestorePayload(
        Grid grid,
        DungeonCharacterWorldSaveData source)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        ValidateRestore(grid, source, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Character-world restore payload is invalid: "
                + string.Join(" | ", report.Errors));
        }
    }

    public CharacterWorldRestoreCandidate PrepareRestoreCandidate(
        Grid grid,
        DungeonCharacterWorldSaveData source)
    {
        if (grid == null)
        {
            throw new ArgumentNullException(nameof(grid));
        }
        if (preparedCandidate != null)
        {
            throw new InvalidOperationException(
                "A character-world restore candidate is already prepared.");
        }

        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        ValidateRestore(grid, source, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Character-world restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }

        List<DungeonCharacterSaveData> savedActors = source.actors;
        EnsureUniqueIds(savedActors.Select(actor => actor.persistentId), "character restore");
        EnsureUniqueIds(
            source.populationProfiles.Select(profile => profile.persistentId),
            "population restore");

        Dictionary<int, CharacterSO> charactersById = characterCatalog.Characters
            .Where(data => data != null)
            .GroupBy(data => data.id)
            .ToDictionary(group => group.Key, group => group.First());

        List<CharacterActor> existingStaff = FindExistingStaff();
        List<CharacterRestoreCandidate> candidates =
            new List<CharacterRestoreCandidate>();
        Dictionary<string, CharacterActor> candidateActorsById =
            new Dictionary<string, CharacterActor>(StringComparer.Ordinal);
        OwnerRunManager ownerManager = null;
        CharacterRestoreCandidate ownerCandidate = null;
        DungeonCharacterSaveData ownerSave = savedActors.Single(actor => actor.isOwner);
        try
        {
            CharacterSO ownerData = charactersById[ownerSave.dataId];
            if (!ownerRunManagerProvider.TryGetManager(out ownerManager))
            {
                throw new InvalidOperationException(
                    "Owner manager was not present for character restore.");
            }

            CharacterActor owner = ownerManager.CreateRestoreCandidate(ownerData);
            ownerCandidate = new CharacterRestoreCandidate(
                ownerSave,
                ownerData,
                owner,
                isOwner: true);
            candidates.Add(ownerCandidate);
            owner.PrepareForPersistentRestore();
            ApplyActorState(grid, owner, ownerSave);
            AddCandidateIdentity(
                candidateActorsById,
                ownerSave,
                owner);

            List<DungeonCharacterSaveData> staffSaves = savedActors
                .Where(actor => !actor.isOwner)
                .ToList();
            CharacterSpawner spawner = null;
            if (staffSaves.Count > 0
                && (!characterSpawnerProvider.TryGetSpawner(out spawner)
                    || spawner.characterPrefab == null))
            {
                throw new InvalidOperationException(
                    "Character spawner prefab was not present for staff restore.");
            }

            foreach (DungeonCharacterSaveData staffSave in staffSaves)
            {
                CharacterSO staffData = charactersById[staffSave.dataId];
                GameObject staffObject = characterObjectFactory.CreateDetached(
                    spawner.characterPrefab,
                    EnsureRestoredStaffWorkAbility);
                CharacterActor staff = CharacterActorCollection.GetCanonical(
                    staffObject.GetComponent<CharacterActor>());
                if (staff == null)
                {
                    characterObjectFactory.Destroy(staffObject);
                    throw new InvalidOperationException(
                        $"Character prefab for staff {staffSave.dataId} has no CharacterActor.");
                }

                CharacterRestoreCandidate staffCandidate =
                    new CharacterRestoreCandidate(
                        staffSave,
                        staffData,
                        staff,
                        isOwner: false);
                candidates.Add(staffCandidate);
                staffObject.name = staffSave.displayName;
                staff.EnsureRuntimeState();
                staff.PrepareForPersistentRestore();
                staff.SetLifecycleState(CharacterLifecycleState.Active);
                staff.Initialize(staffData);
                staff.characterType = staffSave.characterType;
                ApplyActorState(grid, staff, staffSave);
                AddCandidateIdentity(
                    candidateActorsById,
                    staffSave,
                    staff);
            }

            if (!report.Success)
            {
                throw new InvalidOperationException(
                    "Character-world restore candidate contains invalid actor state: "
                    + string.Join(" | ", report.Errors));
            }

            DetachedCharacterWorldCandidate worldCandidate =
                new DetachedCharacterWorldCandidate(
                    candidates,
                    candidateActorsById,
                    existingStaff,
                    ownerManager,
                    characterPopulationService.BuildRestoreCandidate(
                        source.populationProfiles),
                    socialReputation.BuildRestoreCandidate(
                        source.globalFacilityReputation));
            restoreWorldCandidates.SetCharacterCandidate(
                BuildCandidateCharacterView(worldCandidate));
            preparedCandidate = worldCandidate;
            return new CharacterWorldRestoreCandidate(
                this,
                worldCandidate,
                candidates.Count);
        }
        catch (Exception exception)
        {
            DestroyCharacterCandidates(candidates);
            throw new InvalidOperationException(
                $"Character restore candidate preparation failed: {exception.Message}",
                exception);
        }
    }

    public void StageRestoreCandidate(CharacterWorldRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        if (!restoreTransactionActive
            || stagedCandidate != null
            || activePublication != null
            || preparedCandidate == null)
        {
            throw new InvalidOperationException(
                "Character restore candidate staging is not active or already has a value.");
        }

        DetachedCharacterWorldCandidate taken = candidate.Take(this);
        if (!ReferenceEquals(taken, preparedCandidate))
        {
            throw new InvalidOperationException(
                "Character restore candidate does not match the prepared world.");
        }
        preparedCandidate = null;
        stagedCandidate = taken;
    }

    internal void DiscardPreparedCandidate(
        DetachedCharacterWorldCandidate candidate)
    {
        restoreWorldCandidates.ClearCharacterCandidate();
        if (candidate != null)
        {
            DestroyCharacterCandidates(candidate.Characters);
        }
        if (ReferenceEquals(preparedCandidate, candidate))
        {
            preparedCandidate = null;
        }
    }

    private IReadOnlyList<CharacterActor> BuildCandidateCharacterView(
        DetachedCharacterWorldCandidate candidate)
    {
        return candidate.Characters
            .Where(character => character?.Actor != null)
            .Select(character => character.Actor)
            .ToArray();
    }

    private void PublishCharacterCandidate(
        CharacterWorldPublication publication)
    {
        DetachedCharacterWorldCandidate candidate = publication.Candidate;
        publication.PopulationTransaction =
            characterPopulationService.ApplyRestoreCandidate(
                candidate.PopulationCandidate);
        publication.ReputationTransaction =
            socialReputation.ApplyRestoreCandidate(
                candidate.ReputationCandidate);
        foreach (CharacterRestoreCandidate character in candidate.Characters
                     .Where(character => !character.IsOwner))
        {
            DetachedCharacterPublication staffPublication =
                characterObjectFactory.PublishDetachedInactive(
                    character.Actor.gameObject);
            publication.PublishedStaff.Add(staffPublication);
        }

        CharacterRestoreCandidate owner = candidate.Characters
            .FirstOrDefault(character => character.IsOwner);
        if (owner != null)
        {
            publication.OwnerPublication =
                candidate.OwnerManager.BeginRestoreCandidatePublication(
                owner.Data,
                owner.Actor);
        }

        foreach (DetachedCharacterPublication staffPublication in
                 publication.PublishedStaff)
        {
            characterObjectFactory.ValidateDetachedPublication(
                staffPublication);
        }

        restoredActorsById = candidate.ActorsById;
        publication.ActorIndexPublished = true;
    }

    private void RollbackCharacterPublication(
        CharacterWorldPublication publication)
    {
        List<Exception> failures = new List<Exception>();
        void Attempt(Action rollback)
        {
            try
            {
                rollback();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (publication.ActorIndexPublished)
        {
            restoredActorsById = publication.PreviousActorIndex;
            publication.ActorIndexPublished = false;
        }

        if (publication.OwnerPublication != null)
        {
            Attempt(() => publication.Candidate.OwnerManager
                .RollbackRestoreCandidatePublication(
                    publication.OwnerPublication));
            publication.OwnerPublication = null;
        }

        for (int index = publication.PublishedStaff.Count - 1;
             index >= 0;
             index--)
        {
            DetachedCharacterPublication staffPublication =
                publication.PublishedStaff[index];
            Attempt(() => characterObjectFactory.RollbackDetachedPublication(
                staffPublication));
        }
        publication.PublishedStaff.Clear();

        if (publication.ReputationTransaction != null)
        {
            Attempt(() => socialReputation.RollbackRestore(
                publication.ReputationTransaction));
            publication.ReputationTransaction = null;
        }

        if (publication.PopulationTransaction != null)
        {
            Attempt(() => characterPopulationService.RollbackRestore(
                publication.PopulationTransaction));
            publication.PopulationTransaction = null;
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                "Character world publication rollback was not clean.",
                failures);
        }
    }

    private void CompleteCharacterPublication(
        CharacterWorldPublication publication)
    {
        DetachedCharacterWorldCandidate candidate = publication.Candidate;

        if (publication.ReputationTransaction != null)
        {
            socialReputation.CompleteRestore(
                publication.ReputationTransaction);
        }

        if (publication.PopulationTransaction != null)
        {
            characterPopulationService.CompleteRestore(
                publication.PopulationTransaction);
        }

        IEnumerable<CharacterActor> retiringActors =
            candidate.ExistingStaff.Concat(
                publication.OwnerPublication?.PreviousOwner != null
                    ? new[] { publication.OwnerPublication.PreviousOwner }
                    : Array.Empty<CharacterActor>());
        PrepareForWorldRetirement(retiringActors);

        foreach (CharacterActor oldStaff in candidate.ExistingStaff)
        {
            if (oldStaff == null)
            {
                continue;
            }

            oldStaff.gameObject.SetActive(false);
        }

        foreach (DetachedCharacterPublication staffPublication in
                 publication.PublishedStaff)
        {
            characterObjectFactory.CompleteDetachedPublication(
                staffPublication);
        }

        foreach (CharacterActor oldStaff in candidate.ExistingStaff)
        {
            if (oldStaff == null)
            {
                continue;
            }

            characterObjectFactory.Destroy(oldStaff.gameObject);
        }

        characterPopulationService.ReplenishPreparedPoolBestEffort();

        if (publication.OwnerPublication != null)
        {
            candidate.OwnerManager.CompleteRestoreCandidatePublication(
                publication.OwnerPublication);
        }
    }

    private static void AddCandidateIdentity(
        IDictionary<string, CharacterActor> actorsById,
        DungeonCharacterSaveData source,
        CharacterActor actor)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (actor == null)
        {
            throw new ArgumentNullException(nameof(actor));
        }

        if (string.IsNullOrWhiteSpace(source.persistentId))
        {
            throw new InvalidOperationException(
                "A restore candidate cannot be indexed without a persistent character ID.");
        }

        if (actorsById.ContainsKey(source.persistentId))
        {
            throw new InvalidOperationException(
                $"Duplicate persistent character ID '{source.persistentId}' encountered during restore.");
        }

        actor.Identity?.SetPersistentId(source.persistentId);
        actorsById.Add(source.persistentId, actor);
    }

    private static void EnsureRestoredStaffWorkAbility(GameObject staffObject)
    {
        if (staffObject != null && staffObject.GetComponent<AbilityWork>() == null)
        {
            staffObject.AddComponent<AbilityWork>();
        }
    }

    private void DestroyCharacterCandidates(
        IEnumerable<CharacterRestoreCandidate> candidates,
        bool requireDetached = true)
    {
        foreach (CharacterRestoreCandidate candidate in
                 candidates ?? Enumerable.Empty<CharacterRestoreCandidate>())
        {
            if (candidate?.Actor != null
                && (!requireDetached
                    || candidate.Actor.IsDetachedRestoreCandidate))
            {
                candidate.Actor.gameObject.SetActive(false);
                characterObjectFactory.Destroy(candidate.Actor.gameObject);
            }
        }
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

    private static void ValidateUniquePersistentIds(
        IEnumerable<string> ids,
        string label,
        DungeonGameRestoreReport report)
    {
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string rawId in ids ?? Enumerable.Empty<string>())
        {
            string id = rawId?.Trim() ?? string.Empty;
            if (id.Length == 0)
            {
                report.AddError($"A {label} has an empty persistent ID.");
            }
            else if (!seen.Add(id))
            {
                report.AddError(
                    $"Duplicate {label} persistent ID '{id}'.");
            }
        }
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool RequiresWalkableRestoreCell(
        CharacterLifecycleState lifecycleState)
    {
        return lifecycleState == CharacterLifecycleState.Active
            || lifecycleState == CharacterLifecycleState.Downed;
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
        DungeonCharacterSaveData source)
    {
        Vector2Int requestedPosition = new Vector2Int(source.gridX, source.gridY);
        if (RequiresWalkableRestoreCell(source.lifecycleState)
            && (!grid.IsValidGridPos(requestedPosition)
                || !grid.IsWalkable(requestedPosition)))
        {
            throw new InvalidOperationException(
                $"Character {source.persistentId} has an invalid restore position ({source.gridX}, {source.gridY}).");
        }

        actor.transform.position = grid.GetWorldPos(requestedPosition);

        Dictionary<CharacterCondition, float> conditions = source.conditions
            .ToDictionary(entry => entry.condition, entry => entry.value);
        List<CharacterMoodFactorSnapshot> moodFactors = source.moodFactors
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
        actor.SetLifecycleState(source.lifecycleState);

        actor.RefreshAbilityCache();
        AbilityWork work = actor.GetAbility<AbilityWork>();
        if (!source.isOwner && work == null)
        {
            throw new InvalidOperationException(
                $"Restored staff character {source.persistentId} has no AbilityWork after composition.");
        }

        if (work != null)
        {
            work.ClearPriorityWorkTarget();
            foreach (DungeonCharacterWorkPrioritySaveData priority in source.workPriorities)
            {
                if (!WorkTypeCatalog.TryGet(
                        priority.workTypeId,
                        out WorkTypeDefinition definition))
                {
                    throw new InvalidOperationException(
                        $"Character {source.persistentId} references unknown work type '{priority.workTypeId}'.");
                }

                work.SetWorkPriority(definition.WorkTypeId, priority.priority);
            }

            work.SetDutyState(source.dutyState);
            foreach (DungeonCharacterWorkPrioritySaveData priority in source.workPriorities)
            {
                WorkTypeId workTypeId = new WorkTypeId(priority.workTypeId);
                if (work.WorkPriorities.GetPriority(workTypeId) != priority.priority)
                {
                    throw new InvalidOperationException(
                        $"Character {source.persistentId} did not restore work priority '{priority.workTypeId}'.");
                }
            }

            if (work.CurrentDutyState != source.dutyState)
            {
                throw new InvalidOperationException(
                    $"Character {source.persistentId} did not restore duty state '{source.dutyState}'.");
            }
        }

        actor.GetAbility<AbilityShopping>()?.RestorePersistentState(
            source.visitCount,
            source.lookAroundCount,
            source.holdingMoney);
        actor.Progression?.RestorePersistentState(new CharacterProgressionSnapshot(
            source.level,
            source.currentExperience,
            source.growth,
            source.narrative));
        CharacterCarryInventory.Ensure(actor).Restore(source.carryInventory);
        actor.SocialMemory?.RestoreSnapshot(source.socialMemory);
        actor.LogComponent?.RestoreVisibleEntries(source.recentLogEntries);
        actor.state = CharacterDecisionState.DECIDE;
        actor.Brain?.RequestImmediateReplan(clearFailures: true);
    }

    internal sealed class CharacterRestoreCandidate
    {
        public CharacterRestoreCandidate(
            DungeonCharacterSaveData saveData,
            CharacterSO data,
            CharacterActor actor,
            bool isOwner)
        {
            SaveData = saveData
                ?? throw new ArgumentNullException(nameof(saveData));
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            IsOwner = isOwner;
        }

        public DungeonCharacterSaveData SaveData { get; }
        public CharacterSO Data { get; }
        public CharacterActor Actor { get; }
        public bool IsOwner { get; }
    }

    internal sealed class DetachedCharacterWorldCandidate
    {
        public DetachedCharacterWorldCandidate(
            IReadOnlyList<CharacterRestoreCandidate> characters,
            IReadOnlyDictionary<string, CharacterActor> actorsById,
            IReadOnlyList<CharacterActor> existingStaff,
            OwnerRunManager ownerManager,
            CharacterPopulationRestoreCandidate populationCandidate,
            GlobalFacilityReputationRestoreCandidate reputationCandidate)
        {
            Characters = characters
                ?? throw new ArgumentNullException(nameof(characters));
            ActorsById = actorsById
                ?? throw new ArgumentNullException(nameof(actorsById));
            ExistingStaff = existingStaff
                ?? throw new ArgumentNullException(nameof(existingStaff));
            OwnerManager = ownerManager;
            PopulationCandidate = populationCandidate
                ?? throw new ArgumentNullException(nameof(populationCandidate));
            ReputationCandidate = reputationCandidate
                ?? throw new ArgumentNullException(nameof(reputationCandidate));
            if (Characters.Any(character => character.IsOwner)
                && OwnerManager == null)
            {
                throw new InvalidOperationException(
                    "An owner restore candidate requires OwnerRunManager.");
            }
        }

        public IReadOnlyList<CharacterRestoreCandidate> Characters { get; }
        public IReadOnlyDictionary<string, CharacterActor> ActorsById { get; }
        public IReadOnlyList<CharacterActor> ExistingStaff { get; }
        public OwnerRunManager OwnerManager { get; }
        public CharacterPopulationRestoreCandidate PopulationCandidate { get; }
        public GlobalFacilityReputationRestoreCandidate ReputationCandidate { get; }
    }

    private sealed class CharacterWorldPublication
    {
        public CharacterWorldPublication(
            DetachedCharacterWorldCandidate candidate,
            IReadOnlyDictionary<string, CharacterActor> previousActorIndex)
        {
            Candidate = candidate
                ?? throw new ArgumentNullException(nameof(candidate));
            PreviousActorIndex = previousActorIndex
                ?? throw new ArgumentNullException(nameof(previousActorIndex));
        }

        public DetachedCharacterWorldCandidate Candidate { get; }
        public IReadOnlyDictionary<string, CharacterActor> PreviousActorIndex { get; }
        public List<DetachedCharacterPublication> PublishedStaff { get; } =
            new List<DetachedCharacterPublication>();
        public CharacterPopulationRestoreTransaction PopulationTransaction { get; set; }
        public GlobalFacilityReputationRestoreTransaction ReputationTransaction { get; set; }
        public OwnerRestorePublication OwnerPublication { get; set; }
        public bool ActorIndexPublished { get; set; }
    }
}
