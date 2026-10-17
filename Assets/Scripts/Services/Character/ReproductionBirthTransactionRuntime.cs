using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Characters;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public interface IReproductionBirthCommand
{
    bool TryPublish(string processId, int absoluteDay, out DomainFailure failure);
}

public interface IReproductionBirthRecovery
{
    bool TryResumePending(out DomainFailure failure);
}

public interface IReproductionBirthPublicationPort
{
    bool TryEnsureLife(
        ReproductionProcess process,
        CharacterId childId,
        int absoluteDay,
        out string failureReason);
    bool TryEnsureNarrative(
        ReproductionProcess process,
        CharacterId childId,
        out string failureReason);
    bool TryEnsureKinship(
        ReproductionProcess process,
        CharacterId childId,
        out string failureReason);
    bool TryEnsureActor(
        ReproductionProcess process,
        CharacterId childId,
        out string failureReason);
    bool TryEnsurePopulation(
        ReproductionProcess process,
        CharacterId childId,
        out string failureReason);
    bool TryEnsureHousingResolution(
        ReproductionProcess process,
        CharacterId childId,
        int absoluteDay,
        out string failureReason);
    ReproductionFactLocation ResolveLocation(ReproductionProcess process);
}

/// <summary>
/// Resumes one completed reproduction result through durable, idempotent
/// authority boundaries. The reproduction aggregate owns the deterministic
/// child ID and saved phase; external authorities only ensure that exact child.
/// </summary>
public sealed class ReproductionBirthTransactionRuntime :
    IReproductionBirthCommand,
    IReproductionBirthRecovery
{
    private readonly IReproductionPersistence persistence;
    private readonly IReproductionBirthPublicationPort publication;
    private readonly ReproductionTransitionPublication publications;

    public ReproductionBirthTransactionRuntime(
        IReproductionPersistence persistence,
        IReproductionBirthPublicationPort publication,
        ReproductionTransitionPublication publications)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.publication = publication
            ?? throw new ArgumentNullException(nameof(publication));
        this.publications = publications ?? throw new ArgumentNullException(nameof(publications));
    }

    public bool TryPublish(
        string processId,
        int absoluteDay,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!ReproductionStableIdSyntax.IsValid(processId) || absoluteDay < 1)
            return Fail("reproduction-birth-request-invalid", out failure);

        for (int iteration = 0; iteration < 12; iteration++)
        {
            ReproductionWorldAggregate candidate = persistence.PrepareRestore(
                persistence.Capture());
            if (!candidate.TryGet(processId, out ReproductionProcess process))
                return Fail("reproduction-birth-process-missing", out failure);
            if (process.PendingTransition != null)
            {
                if (!publications.TryResume(processId, out failure)) return false;
                continue;
            }
            if (process.ResultPublished)
                return true;
            if (process.Status != ReproductionProcessStatus.Completed)
                return Fail("reproduction-birth-process-not-completed", out failure);

            if (process.PendingBirth == null)
            {
                process.PrepareBirthTransaction(
                    CharacterId.FromStableSuffix(
                        "reproduction-birth:" + process.ProcessId),
                    absoluteDay);
                persistence.PublishRestore(candidate);
                continue;
            }

            ReproductionBirthTransactionSaveData transaction =
                process.PendingBirth;
            CharacterId childId = new(transaction.childCharacterId);
            switch (transaction.phase)
            {
                case ReproductionBirthTransactionPhase.Prepared:
                    if (!publication.TryEnsureLife(
                            process,
                            childId,
                            transaction.absoluteDay,
                            out string lifeFailure))
                        return Fail(lifeFailure, out failure);
                    Advance(
                        candidate,
                        process,
                        ReproductionBirthTransactionPhase.Prepared,
                        ReproductionBirthTransactionPhase.LifeRegistered);
                    break;

                case ReproductionBirthTransactionPhase.LifeRegistered:
                    if (!publication.TryEnsureNarrative(
                            process,
                            childId,
                            out string narrativeFailure))
                        return Fail(narrativeFailure, out failure);
                    Advance(
                        candidate,
                        process,
                        ReproductionBirthTransactionPhase.LifeRegistered,
                        ReproductionBirthTransactionPhase.NarrativeRegistered);
                    break;

                case ReproductionBirthTransactionPhase.NarrativeRegistered:
                    if (!publication.TryEnsureKinship(
                            process,
                            childId,
                            out string kinshipFailure))
                        return Fail(kinshipFailure, out failure);
                    Advance(
                        candidate,
                        process,
                        ReproductionBirthTransactionPhase.NarrativeRegistered,
                        ReproductionBirthTransactionPhase.KinshipRegistered);
                    break;

                case ReproductionBirthTransactionPhase.KinshipRegistered:
                    if (!publication.TryEnsureActor(
                            process,
                            childId,
                            out string actorFailure))
                        return Fail(actorFailure, out failure);
                    Advance(
                        candidate,
                        process,
                        ReproductionBirthTransactionPhase.KinshipRegistered,
                        ReproductionBirthTransactionPhase.ActorPublished);
                    break;

                case ReproductionBirthTransactionPhase.ActorPublished:
                    if (!publication.TryEnsurePopulation(
                            process,
                            childId,
                            out string populationFailure))
                        return Fail(populationFailure, out failure);
                    Advance(
                        candidate,
                        process,
                        ReproductionBirthTransactionPhase.ActorPublished,
                        ReproductionBirthTransactionPhase.PopulationRegistered);
                    break;

                case ReproductionBirthTransactionPhase.PopulationRegistered:
                    if (!publication.TryEnsureHousingResolution(
                            process,
                            childId,
                            transaction.absoluteDay,
                            out string housingFailure))
                        return Fail(housingFailure, out failure);
                    Advance(
                        candidate,
                        process,
                        ReproductionBirthTransactionPhase.PopulationRegistered,
                        ReproductionBirthTransactionPhase
                            .HousingResolutionRecorded);
                    break;

                case ReproductionBirthTransactionPhase
                        .HousingResolutionRecorded:
                    process.PrepareBirthPublicationMarker();
                    persistence.PublishRestore(candidate);
                    break;

                case ReproductionBirthTransactionPhase.PublicationPrepared:
                    if (!TryPublishFact(
                            candidate,
                            process,
                            childId,
                            out failure))
                        return false;
                    return true;

                case ReproductionBirthTransactionPhase.ObservationCommitted:
                    process.CompleteBirthTransaction();
                    persistence.PublishRestore(candidate);
                    return true;

                default:
                    return Fail("reproduction-birth-phase-invalid", out failure);
            }
        }

        return Fail("reproduction-birth-recovery-iteration-limit", out failure);
    }

    public bool TryResumePending(out DomainFailure failure)
    {
        ReproductionProcessSaveData[] pending = persistence.Capture().processes
            .Where(value => value?.pendingBirth != null)
            .OrderBy(value => value.processId, StringComparer.Ordinal)
            .ToArray();
        foreach (ReproductionProcessSaveData process in pending)
            if (!TryPublish(
                    process.processId,
                    process.pendingBirth.absoluteDay,
                    out failure))
                return false;
        failure = DomainFailure.None;
        return true;
    }

    private bool TryPublishFact(
        ReproductionWorldAggregate candidate,
        ReproductionProcess process,
        CharacterId childId,
        out DomainFailure failure)
    {
        ReproductionProcessSaveData before = process.Capture();
        before.transitionRevision = checked(
            process.PendingBirth.publicationRevision - 1L);
        ReproductionTransitionFact fact;
        try
        {
            fact = publications.Source.Create(before, process, "birth-published",
                process.Mode == ReproductionMode.GolemAssembly ? "golem-activated" : "offspring-born",
                "none", process.PendingBirth.absoluteDay, publication.ResolveLocation(process), offspring: childId);
        }
        catch (Exception exception) { return Fail("reproduction-birth-fact:" + exception.Message, out failure); }
        return publications.TryCommit(persistence.Capture(), candidate, process, fact, out failure);
    }

    private void Advance(
        ReproductionWorldAggregate candidate,
        ReproductionProcess process,
        ReproductionBirthTransactionPhase expected,
        ReproductionBirthTransactionPhase next)
    {
        process.AdvanceBirthTransaction(expected, next);
        persistence.PublishRestore(candidate);
    }

    private static bool Fail(
        string detail,
        out DomainFailure failure)
    {
        failure = new DomainFailure(
            FailureCode.ExternalInfluenceUnavailable,
            string.IsNullOrWhiteSpace(detail)
                ? "reproduction-birth-boundary-failed"
                : detail.Trim());
        return false;
    }
}

public sealed class ReproductionBirthRecoveryAdapter : IStartable
{
    private readonly IReproductionBirthRecovery recovery;

    public ReproductionBirthRecoveryAdapter(IReproductionBirthRecovery recovery) =>
        this.recovery = recovery
            ?? throw new ArgumentNullException(nameof(recovery));

    public void Start()
    {
        if (!recovery.TryResumePending(out DomainFailure failure))
            Debug.LogError(
                "Reproduction birth recovery remains pending: "
                + failure.Code + " "
                + string.Join(",", failure.Parameters.ToArray()));
    }
}

public sealed class ReproductionBirthPublicationPort :
    IReproductionBirthPublicationPort
{
    private readonly ICharacterWorldQuery world;
    private readonly ICharacterLifeDefinitionCatalog lifeDefinitions;
    private readonly ICharacterLifeQuery life;
    private readonly ICharacterLifeCommand lifeCommands;
    private readonly ICharacterNarrativeCommand narrativeCommands;
    private readonly ICharacterNarrativePersistence narrativePersistence;
    private readonly IKinshipQuery kinshipQuery;
    private readonly IKinshipCommand kinshipCommands;
    private readonly ICharacterPopulationService population;
    private readonly IHouseholdBirthAssignmentCommand householdAssignments;
    private readonly IGameContentCatalog content;
    private readonly ICharacterSpawnObjectFactory characterObjects;
    private readonly ICharacterSpawnerProvider spawners;

    public ReproductionBirthPublicationPort(
        ICharacterWorldQuery world,
        ICharacterLifeDefinitionCatalog lifeDefinitions,
        ICharacterLifeQuery life,
        ICharacterLifeCommand lifeCommands,
        ICharacterNarrativeCommand narrativeCommands,
        ICharacterNarrativePersistence narrativePersistence,
        IKinshipQuery kinshipQuery,
        IKinshipCommand kinshipCommands,
        ICharacterPopulationService population,
        IHouseholdBirthAssignmentCommand householdAssignments,
        IGameContentCatalog content,
        ICharacterSpawnObjectFactory characterObjects,
        ICharacterSpawnerProvider spawners)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.lifeDefinitions = lifeDefinitions
            ?? throw new ArgumentNullException(nameof(lifeDefinitions));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.lifeCommands = lifeCommands
            ?? throw new ArgumentNullException(nameof(lifeCommands));
        this.narrativeCommands = narrativeCommands
            ?? throw new ArgumentNullException(nameof(narrativeCommands));
        this.narrativePersistence = narrativePersistence
            ?? throw new ArgumentNullException(nameof(narrativePersistence));
        this.kinshipQuery = kinshipQuery
            ?? throw new ArgumentNullException(nameof(kinshipQuery));
        this.kinshipCommands = kinshipCommands
            ?? throw new ArgumentNullException(nameof(kinshipCommands));
        this.population = population
            ?? throw new ArgumentNullException(nameof(population));
        this.householdAssignments = householdAssignments
            ?? throw new ArgumentNullException(nameof(householdAssignments));
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.characterObjects = characterObjects
            ?? throw new ArgumentNullException(nameof(characterObjects));
        this.spawners = spawners ?? throw new ArgumentNullException(nameof(spawners));
    }

    public bool TryEnsureLife(
        ReproductionProcess process,
        CharacterId childId,
        int absoluteDay,
        out string failureReason)
    {
        SpeciesLifeHistoryDefinition history = lifeDefinitions.RequireLifeHistory(
            process.PhenotypeSpeciesId);
        bool construct = process.Mode == ReproductionMode.GolemAssembly;
        double biologicalAge = construct ? history.AdultAgeDayUnits : 0d;
        int birthday = GameCalendarRules.Project(absoluteDay, 0).DayOfYear;
        if (life.TryGet(childId, out CharacterLifeRecord existing))
        {
            bool exact = existing.PhenotypeSpeciesId.Equals(
                    process.PhenotypeSpeciesId)
                && existing.ChronologicalAgeDays == 0
                && Math.Abs(existing.BiologicalAgeDayUnits - biologicalAge)
                    <= 0.000001d
                && existing.BirthdayDayOfYear == birthday;
            failureReason = exact
                ? string.Empty
                : "reproduction-birth-life-conflict";
            return exact;
        }
        lifeCommands.Register(
            childId,
            process.PhenotypeSpeciesId,
            0,
            biologicalAge,
            birthday);
        failureReason = string.Empty;
        return true;
    }

    public bool TryEnsureNarrative(
        ReproductionProcess process,
        CharacterId childId,
        out string failureReason)
    {
        CharacterNarrativeSaveData existing = narrativePersistence.Capture()
            .characters
            .SingleOrDefault(value => value.characterId == childId.Value);
        if (existing != null)
        {
            bool exact = existing.phenotypeSpeciesId
                    == process.PhenotypeSpeciesId.Value
                && SameStrings(
                    existing.expressedHeritableTraitIds,
                    process.ExpressedTraitIds)
                && SameStrings(
                    existing.latentHeritableTraitIds,
                    process.LatentTraitIds);
            failureReason = exact
                ? string.Empty
                : "reproduction-birth-narrative-conflict";
            return exact;
        }
        narrativeCommands.Register(
            childId,
            process.PhenotypeSpeciesId,
            process.ExpressedTraitIds,
            process.LatentTraitIds);
        failureReason = string.Empty;
        return true;
    }

    public bool TryEnsureKinship(
        ReproductionProcess process,
        CharacterId childId,
        out string failureReason)
    {
        CharacterId[] parents = kinshipQuery.GetParents(
                childId,
                includeAdoptive: true)
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
        if (process.Mode == ReproductionMode.GolemAssembly)
        {
            CharacterId guardian = kinshipQuery.GetGuardian(childId);
            if (parents.Length != 0
                || guardian.IsValid && !guardian.Equals(process.FirstParentId))
            {
                failureReason = "reproduction-birth-kinship-conflict";
                return false;
            }
            if (!guardian.IsValid)
                kinshipCommands.SetGuardian(childId, process.FirstParentId);
        }
        else
        {
            CharacterId[] expected = new[]
                {
                    process.FirstParentId,
                    process.SecondParentId
                }
                .OrderBy(value => value.Value, StringComparer.Ordinal)
                .ToArray();
            if (parents.Except(expected).Any())
            {
                failureReason = "reproduction-birth-kinship-conflict";
                return false;
            }
            foreach (CharacterId parent in expected.Except(parents))
                kinshipCommands.AddParent(childId, parent, adoptive: false);
            parents = kinshipQuery.GetParents(childId, includeAdoptive: true)
                .OrderBy(value => value.Value, StringComparer.Ordinal)
                .ToArray();
            if (!parents.SequenceEqual(expected))
            {
                failureReason = "reproduction-birth-kinship-incomplete";
                return false;
            }
        }
        failureReason = string.Empty;
        return true;
    }

    public bool TryEnsureActor(
        ReproductionProcess process,
        CharacterId childId,
        out string failureReason)
    {
        CharacterActor[] existing = world.Characters.Where(value =>
                value != null
                && CharacterPersistentIdentity.TryGet(value, out CharacterId id)
                && id.Equals(childId))
            .ToArray();
        if (existing.Length > 1)
        {
            failureReason = "reproduction-birth-actor-duplicated";
            return false;
        }
        if (existing.Length == 1)
        {
            bool exact = existing[0].Identity?.Profile != null
                && existing[0].Identity.Profile.PhenotypeSpeciesId.Equals(
                    process.PhenotypeSpeciesId);
            failureReason = exact
                ? string.Empty
                : "reproduction-birth-actor-conflict";
            return exact;
        }
        if (!spawners.TryGetSpawner(out CharacterSpawner spawner)
            || spawner.characterPrefab == null)
        {
            failureReason = "reproduction-birth-spawner-unavailable";
            return false;
        }

        CharacterSO archetype = ResolveArchetype(process.PhenotypeSpeciesId);
        CharacterSpawnRequest request = CreateSpawnRequest(process, archetype);
        GameObject characterObject = null;
        try
        {
            characterObject = characterObjects.CreateInactive(
                spawner.characterPrefab);
            CharacterActor actor = CharacterActorCollection.GetCanonical(
                characterObject.GetComponent<CharacterActor>());
            if (actor == null)
            {
                failureReason = "reproduction-birth-actor-component-missing";
                characterObjects.Destroy(characterObject);
                return false;
            }
            actor.Initialize(archetype, request);
            actor.Identity.SetPersistentId(childId);
            actor.characterType = CharacterType.NPC;
            actor.gameObject.name = $"Newborn {process.PhenotypeSpeciesId.Value} {childId.Value}";
            actor.transform.position = ResolveBirthPosition(process, spawner);
            characterObjects.Publish(characterObject);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.Brain?.UseStaffWorkActions();
            actor.Brain?.RequestImmediateReplan(clearFailures: true);
            failureReason = string.Empty;
            return true;
        }
        catch
        {
            if (characterObject != null
                && !world.Characters.Any(value => value != null
                    && CharacterPersistentIdentity.TryGet(
                        value,
                        out CharacterId id)
                    && id.Equals(childId)))
                characterObjects.Destroy(characterObject);
            throw;
        }
    }

    public bool TryEnsurePopulation(
        ReproductionProcess process,
        CharacterId childId,
        out string failureReason)
    {
        CharacterActor actor = FindActor(childId);
        if (actor == null)
        {
            failureReason = "reproduction-birth-population-actor-missing";
            return false;
        }
        population.SetSettlementStanding(
            actor,
            CharacterSettlementStanding.Resident);
        WorldCharacterProfile[] matches = population.Profiles.Where(value =>
                value != null && value.persistentId == childId.Value)
            .ToArray();
        bool exact = matches.Length == 1
            && matches[0].settlementStanding
                == CharacterSettlementStanding.Resident;
        failureReason = exact
            ? string.Empty
            : "reproduction-birth-population-conflict";
        return exact;
    }

    public bool TryEnsureHousingResolution(
        ReproductionProcess process,
        CharacterId childId,
        int absoluteDay,
        out string failureReason)
    {
        return householdAssignments.TryResolveBirth(
            childId,
            process.FirstParentId,
            process.Mode == ReproductionMode.GolemAssembly
                ? default
                : process.SecondParentId,
            process.CarrierId,
            absoluteDay,
            out _,
            out failureReason);
    }

    public ReproductionFactLocation ResolveLocation(
        ReproductionProcess process)
    {
        CharacterActor source = FindActor(process.CarrierId)
            ?? FindActor(process.FirstParentId);
        Vector3 position = source != null ? source.transform.position : Vector3.zero;
        return new ReproductionFactLocation(
            process.SupportFacilityInstanceId,
            string.Empty,
            Mathf.RoundToInt(position.x),
            Mathf.RoundToInt(position.y));
    }

    private CharacterActor FindActor(CharacterId id) => world.Characters
        .FirstOrDefault(value => value != null
            && CharacterPersistentIdentity.TryGet(value, out CharacterId candidate)
            && candidate.Equals(id));

    private CharacterSO ResolveArchetype(CharacterSpeciesId speciesId) =>
        content.GetAll<CharacterSO>()
            .Where(value => value != null
                && value.DefinitionId.IsValid
                && value.species != null
                && value.species.DefinitionId.Equals(speciesId)
                && value.role != CharacterRole.Owner)
            .OrderBy(value => value.characterType == CharacterType.NPC ? 0 : 1)
            .ThenBy(value => value.DefinitionId.Value, StringComparer.Ordinal)
            .FirstOrDefault()
        ?? throw new InvalidOperationException(
            $"No authored non-owner character archetype exists for phenotype '{speciesId.Value}'.");

    private static CharacterSpawnRequest CreateSpawnRequest(
        ReproductionProcess process,
        CharacterSO archetype) => new(
        archetype.DefinitionId,
        process.PhenotypeSpeciesId,
        archetype.VisualVariantId,
        ResolveOffspringRole(process),
        (archetype.traits ?? Array.Empty<CharacterTraitSO>())
            .Where(value => value != null)
            .Select(value => value.DefinitionId),
        Array.Empty<CharacterTraitId>(),
        process.InnateAptitudes.ToDictionary(
            value => value.skillId,
            value => Math.Clamp(value.value, 0, 100),
            StringComparer.Ordinal));

    private Vector3 ResolveBirthPosition(
        ReproductionProcess process,
        CharacterSpawner spawner)
    {
        CharacterActor source = FindActor(process.CarrierId)
            ?? FindActor(process.FirstParentId);
        return source != null
            ? source.transform.position
            : spawner.GetEntryDoorWorldPosition();
    }

    private static ReproductiveRole ResolveOffspringRole(
        ReproductionProcess process)
    {
        bool first = (PersistentEntityId.GetStableHash32(process.ProcessId) & 1u)
            == 0u;
        return process.Mode switch
        {
            ReproductionMode.Pregnancy => first
                ? ReproductiveRole.Carrier
                : ReproductiveRole.Contributor,
            ReproductionMode.Egg => first
                ? ReproductiveRole.Layer
                : ReproductiveRole.Fertilizer,
            ReproductionMode.Spore => ReproductiveRole.SporeContributor,
            ReproductionMode.CoreDivision => ReproductiveRole.DivisionCore,
            ReproductionMode.GolemAssembly => ReproductiveRole.Assembler,
            _ => ReproductiveRole.None
        };
    }

    private static bool SameStrings(
        IEnumerable<string> left,
        IEnumerable<string> right) => (left ?? Array.Empty<string>())
        .OrderBy(value => value, StringComparer.Ordinal)
        .SequenceEqual((right ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal));
}
