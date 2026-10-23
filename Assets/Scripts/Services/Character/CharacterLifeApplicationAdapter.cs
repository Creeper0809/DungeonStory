using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;

public readonly struct CharacterAgeConditionChangedEvent
{
    public CharacterAgeConditionChangedEvent(
        AgeConditionChange change,
        bool bodyProjectionScheduled = false)
    {
        Change = change;
        BodyProjectionScheduled = bodyProjectionScheduled;
    }

    public AgeConditionChange Change { get; }
    public bool BodyProjectionScheduled { get; }
}

public readonly struct CharacterLifeStageChangedEvent
{
    public CharacterLifeStageChangedEvent(
        CharacterId characterId,
        CharacterLifeStage previous,
        CharacterLifeStage current)
    {
        CharacterId = characterId;
        Previous = previous;
        Current = current;
    }

    public CharacterId CharacterId { get; }
    public CharacterLifeStage Previous { get; }
    public CharacterLifeStage Current { get; }
}

public sealed class CharacterLifeApplicationAdapter : IStartable, IDisposable, IDungeonSaveRestoreCompletedHook
{
    private readonly CharacterLifeRuntime life;
    private readonly IGameEventBus events;
    private readonly IHeritableTraitEffectQuery heritableTraits;
    private readonly IDomainFactPublisher facts;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly IAgeConditionAnatomyProjectionCommand anatomyProjection;
    private IDisposable dayEndedSubscription;
    private bool advancing;

    public CharacterLifeApplicationAdapter(CharacterLifeRuntime life, IGameEventBus events,
        IHeritableTraitEffectQuery heritableTraits, IDomainFactPublisher facts,
        IDomainFactRecoveryPublisher recovery,
        IAgeConditionAnatomyProjectionCommand anatomyProjection = null)
    {
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.heritableTraits = heritableTraits ?? throw new ArgumentNullException(nameof(heritableTraits));
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        this.anatomyProjection = anatomyProjection;
    }
    public void Start()
    {
        dayEndedSubscription ??= events.Subscribe<OperatingDayEndedEvent>(OnDayEnded);
        RecoverSafe();
    }
    public void Dispose() { dayEndedSubscription?.Dispose(); dayEndedSubscription = null; }
    public void OnRestoreCompleted() => RecoverSafe();
    private void RecoverSafe()
    {
        try { ResumePending(); }
        catch (Exception exception) { Debug.LogException(exception); }
    }
    private void ResumePending()
    {
        var pending = life.PendingDailyObservation;
        if (pending.entries.Count > 0)
        {
            var deliveries = new DomainFactDelivery[pending.entries.Count];
            var replay = pending.entries.Select(row => DomainFactReplay.Create(
                row.publicationKey, new CharacterLifeTransitionFact(row.fact))).ToArray();
            if (!recovery.ReplayBatch(replay, deliveries, out string failure))
                throw new InvalidOperationException("life-daily-observation-replay-pending:" + failure);
            PublishReactions(life.CompleteDailyObservations(deliveries));
        }
        anatomyProjection?.ResumePendingRequired();
    }
    private void OnDayEnded(OperatingDayEndedEvent ended)
    {
        if (advancing) throw new InvalidOperationException("life-daily-advance-reentrant");
        advancing = true;
        try
        {
            ResumePending();
            int absoluteDay = checked(ended.day + 1);
            if (absoluteDay <= life.LastAdvancedAbsoluteDay) return;
            var multipliers = life.Records.OrderBy(row => row.CharacterId.Value, StringComparer.Ordinal)
                .ToDictionary(row => row.CharacterId, row => (double)heritableTraits.GetMultiplier(
                    row.CharacterId, HeritableTraitConsequenceKind.AgingRate, "biological-age"));
            var candidate = life.PrepareDailyAdvance(multipliers);
            var leases = new List<DomainFactLease>();
            bool publicationCrossed = false, sealedCandidate = false;
            try
            {
                var cohort = candidate.AgeConditionChanges.Select(value => CharacterLifeTransitionFact.FromAgeCondition(value, absoluteDay))
                    .Concat(candidate.LifeStageTransitions.Select(value => CharacterLifeTransitionFact.FromLifeStage(value, absoluteDay))).ToArray();
                foreach (var fact in cohort)
                {
                    if (!facts.TryReserve(fact, out var lease, out string failure))
                        throw new InvalidOperationException("life-daily-observation-reserve:" + failure);
                    leases.Add(lease);
                }
                life.StageDailyObservations(candidate, absoluteDay, cohort, leases.Select(value => value.PublicationKey).ToArray());
                life.PublishDailyAdvance(candidate);
                var deliveries = new DomainFactDelivery[cohort.Length];
                if (cohort.Length > 0)
                {
                    publicationCrossed = true;
                    var emissions = cohort.Select((fact, index) =>
                        DomainFactEmission.Create(leases[index], fact)).ToArray();
                    if (!facts.PublishBatch(emissions, deliveries, out string failure))
                    {
                        publicationCrossed = false;
                        throw new InvalidOperationException("life-daily-observation-rejected:" + failure);
                    }
                }
                life.CompleteDailyAdvance(candidate);
                sealedCandidate = true;
                if (cohort.Length > 0) PublishReactions(life.CompleteDailyObservations(deliveries));
            }
            catch
            {
                if (!sealedCandidate)
                {
                    // An unknown acknowledgement cannot roll back time/RNG after publication.
                    if (publicationCrossed) life.CompleteDailyAdvance(candidate);
                    else life.RollbackDailyAdvance(candidate);
                }
                throw;
            }
            finally { foreach (var lease in leases) facts.Cancel(lease); }
            anatomyProjection?.ResumePendingRequired();
        }
        finally { advancing = false; }
    }
    private void PublishReactions(IEnumerable<CharacterLifeTransitionFact> cohort)
    {
        foreach (var fact in cohort)
        {
            if (fact.Kind == CharacterLifeTransitionKind.AgeCondition)
                PublishObserver(new CharacterAgeConditionChangedEvent(fact.AgeChange, bodyProjectionScheduled: !fact.AgeChange.Resolved));
            else
            {
                var change = fact.StageChange;
                PublishObserver(new CharacterLifeStageChangedEvent(change.CharacterId, change.Previous, change.Current));
            }
        }
    }
    private void PublishObserver<TEvent>(TEvent gameEvent)
    {
        try { events.Publish(gameEvent); }
        catch (Exception exception) { Debug.LogException(exception); }
    }
}

public sealed class CharacterLifeCelebrationAdapter : IStartable, IDisposable
{
    private readonly ICharacterLifeQuery life;
    private readonly ICharacterWorldQuery world;
    private readonly IGameEventBus events;
    private IDisposable dayEndedSubscription;
    private IDisposable lifeStageSubscription;

    public CharacterLifeCelebrationAdapter(
        ICharacterLifeQuery life,
        ICharacterWorldQuery world,
        IGameEventBus events)
    {
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Start()
    {
        dayEndedSubscription ??= events.Subscribe<OperatingDayEndedEvent>(
            OnDayEnded);
        lifeStageSubscription ??= events.Subscribe<CharacterLifeStageChangedEvent>(
            OnLifeStageChanged);
    }

    public void Dispose()
    {
        dayEndedSubscription?.Dispose();
        lifeStageSubscription?.Dispose();
        dayEndedSubscription = null;
        lifeStageSubscription = null;
    }

    private void OnDayEnded(OperatingDayEndedEvent ended)
    {
        int nextAbsoluteDay = ended.day + 1;
        int dayOfYear = GameCalendarRules.Project(nextAbsoluteDay, 0).DayOfYear;
        foreach (CharacterLifeRecord record in life.Records
                     .Where(value => value.BirthdayDayOfYear == dayOfYear))
        {
            CharacterActor actor = FindLivingActor(record.CharacterId);
            actor?.ApplyMoodFactor(
                "mood:birthday",
                "mood:birthday",
                3f,
                GameCalendarRules.SecondsPerDay * 5f,
                1);
        }
    }

    private void OnLifeStageChanged(CharacterLifeStageChangedEvent gameEvent)
    {
        if (gameEvent.Current != CharacterLifeStage.Adult)
            return;
        CharacterActor actor = FindLivingActor(gameEvent.CharacterId);
        actor?.ApplyMoodFactor(
            "mood:coming-of-age",
            "mood:coming-of-age",
            6f,
            GameCalendarRules.SecondsPerDay * 10f,
            1);
    }

    private CharacterActor FindLivingActor(CharacterId characterId) =>
        world.Characters.FirstOrDefault(candidate => candidate != null
            && !candidate.IsDead
            && CharacterPersistentIdentity.TryGet(candidate, out CharacterId id)
            && id.Equals(characterId));
}

public sealed class CharacterAgeConditionBodyHealthAdapter : IStartable, IDisposable
{
    private readonly ICharacterLifeDefinitionCatalog definitions;
    private readonly ICharacterWorldQuery world;
    private readonly IAnatomyHealthRuntime anatomy;
    private readonly IGameEventBus events;
    private IDisposable conditionSubscription;

    public CharacterAgeConditionBodyHealthAdapter(
        ICharacterLifeDefinitionCatalog definitions,
        ICharacterWorldQuery world,
        IAnatomyHealthRuntime anatomy,
        IGameEventBus events)
    {
        this.definitions = definitions
            ?? throw new ArgumentNullException(nameof(definitions));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Start() => conditionSubscription ??=
        events.Subscribe<CharacterAgeConditionChangedEvent>(OnConditionChanged);

    public void Dispose()
    {
        conditionSubscription?.Dispose();
        conditionSubscription = null;
    }

    private void OnConditionChanged(CharacterAgeConditionChangedEvent gameEvent)
    {
        if (gameEvent.BodyProjectionScheduled)
            return;
        AgeConditionChange change = gameEvent.Change;
        if (change.Resolved)
        {
            return;
        }

        CharacterActor actor = world.Characters.FirstOrDefault(candidate =>
            CharacterPersistentIdentity.TryGet(candidate, out CharacterId id)
            && id.Equals(change.CharacterId));
        if (actor == null || actor.IsDead)
        {
            return;
        }

        AgeConditionDefinition definition = definitions.RequireAgeCondition(
            change.ConditionId);
        AnatomyHealthSnapshot snapshot = anatomy.GetAnatomySnapshot(actor);
        Dictionary<string, AnatomyNodeHealthState> nodes = snapshot.Nodes
            .Where(node => node != null)
            .ToDictionary(node => node.nodeId, StringComparer.Ordinal);
        AnatomyNodeHealthState[] affected = definition.GetAffectedAnatomyNodeIds(snapshot.ProfileId)
            .Where(nodes.ContainsKey)
            .Select(nodeId => nodes[nodeId])
            .Where(node => !node.missing)
            .ToArray();
        if (affected.Length == 0)
        {
            throw new InvalidOperationException(
                $"Age condition '{definition.ConditionId}' has no matching anatomy node for '{snapshot.ProfileId}'.");
        }

        float fraction = DamageFraction(change.Current);
        string reasonCode =
            $"age-condition:{definition.ConditionId}:{change.Current}";
        foreach (AnatomyNodeHealthState node in affected)
        {
            float damage = change.CausesOrganFunctionLoss
                ? node.currentHealth
                : node.maxHealth * fraction;
            if (damage <= 0f)
            {
                continue;
            }

            if (!anatomy.TryDamageNodeWithCause(
                    actor,
                    node.nodeId,
                    damage,
                    bleeding: 0f,
                    CharacterDeathCauseCode.AgeConditionOrganFailure,
                    reasonCode))
            {
                throw new InvalidOperationException(
                    $"Could not project age condition '{definition.ConditionId}' to anatomy node '{node.nodeId}'.");
            }

            if (actor.IsDead)
            {
                break;
            }
        }
    }

    private static float DamageFraction(AgeConditionSeverity severity) =>
        severity switch
        {
            AgeConditionSeverity.Mild => 0.05f,
            AgeConditionSeverity.Moderate => 0.10f,
            AgeConditionSeverity.Severe => 0.20f,
            AgeConditionSeverity.Critical => 0.30f,
            AgeConditionSeverity.OrganFunctionLoss => 1f,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
        };
}

public sealed class CharacterDeathPopulationAdapter : IStartable, IDisposable
{
    private readonly ICharacterWorldQuery world;
    private readonly ICharacterLifeQuery life;
    private readonly IKinshipQuery kinship;
    private readonly IKinshipCommand kinshipCommands;
    private readonly IHouseholdService households;
    private readonly IReproductionProcessTransitionCommand reproductionTransitions;
    private readonly IGriefTraumaService grief;
    private readonly ICareerService careers;
    private readonly IGameEventBus events;
    private IDisposable deathSubscription;
    private IDisposable dayEndedSubscription;

    public CharacterDeathPopulationAdapter(
        ICharacterWorldQuery world,
        ICharacterLifeQuery life,
        IKinshipQuery kinship,
        IKinshipCommand kinshipCommands,
        IHouseholdService households,
        IReproductionProcessTransitionCommand reproductionTransitions,
        IGriefTraumaService grief,
        ICareerService careers,
        IGameEventBus events)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.kinship = kinship ?? throw new ArgumentNullException(nameof(kinship));
        this.kinshipCommands = kinshipCommands
            ?? throw new ArgumentNullException(nameof(kinshipCommands));
        this.households = households
            ?? throw new ArgumentNullException(nameof(households));
        this.reproductionTransitions = reproductionTransitions
            ?? throw new ArgumentNullException(nameof(reproductionTransitions));
        this.grief = grief ?? throw new ArgumentNullException(nameof(grief));
        this.careers = careers ?? throw new ArgumentNullException(nameof(careers));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Start()
    {
        deathSubscription ??= events.Subscribe<CharacterDeathEvent>(OnDeath);
        dayEndedSubscription ??= events.Subscribe<OperatingDayEndedEvent>(OnDayEnded);
    }

    public void Dispose()
    {
        deathSubscription?.Dispose();
        dayEndedSubscription?.Dispose();
        deathSubscription = null;
        dayEndedSubscription = null;
    }

    private void OnDeath(CharacterDeathEvent gameEvent)
    {
        CharacterLifeDeathRecord death = gameEvent.ToLifeRecord();
        // The deceased no longer owns an active psychosocial aggregate. Keep
        // their death/kinship history and the survivors' grief independently.
        grief.RetireAfterDeath(death.CharacterId);
        careers.CancelRetirementByDeath(
            death.CharacterId,
            death.AbsoluteDay);
        CharacterActor deceasedActor = FindActor(death.CharacterId);
        if (!reproductionTransitions.TryNotifyCarrierDeath(
                gameEvent,
                out DomainFailure reproductionFailure))
        {
            Debug.LogError(
                "Carrier-death reproduction transition remains pending: "
                + reproductionFailure.Code + " "
                + string.Join(",", reproductionFailure.Parameters.ToArray()));
        }

        CharacterRoomAssignmentSaveData deceasedHousehold = null;
        households.TryGet(death.CharacterId, out deceasedHousehold);
        bool residentDeath = deceasedActor != null
            && deceasedActor.Identity?.CharacterType != CharacterType.Intruder;
        if (residentDeath)
        {
            foreach (CharacterActor survivor in world.Characters
                         .Where(candidate => candidate != null
                             && !candidate.IsDead
                             && candidate.Identity?.CharacterType
                                 != CharacterType.Intruder))
            {
                CharacterId survivorId = CharacterPersistentIdentity.Require(
                    survivor);
                if (survivorId.Equals(death.CharacterId))
                {
                    continue;
                }

                GriefRelationshipKind relationship = ResolveRelationship(
                    survivorId,
                    death.CharacterId,
                    deceasedHousehold);
                grief.RecordDeath(survivorId, death, relationship);
                ProjectPsychosocialMood(survivor, death.AbsoluteDay);
            }

            ReassignDependentGuardians(death.CharacterId, deceasedHousehold);
        }

        if (life.TryGet(death.CharacterId, out CharacterLifeRecord record))
        {
            kinshipCommands.ArchiveDeath(
                death.CharacterId,
                record.PhenotypeSpeciesId,
                death.AbsoluteDay - record.ChronologicalAgeDays,
                death.AbsoluteDay,
                deceasedActor?.IsOwner == true,
                ResolveHouseholdId(deceasedHousehold),
                kinship.GetGeneration(death.CharacterId));
        }

        kinshipCommands.ClearPartner(death.CharacterId);
        households.Clear(death.CharacterId);
    }

    private void OnDayEnded(OperatingDayEndedEvent ended)
    {
        int nextDay = ended.day + 1;
        CharacterId[] livingResidents = world.Characters
            .Where(candidate => candidate != null && !candidate.IsDead
                && candidate.Identity?.CharacterType != CharacterType.Intruder)
            .Select(CharacterPersistentIdentity.Require)
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToArray();
        kinshipCommands.ArchiveColdData(nextDay, livingResidents);
        foreach (CharacterActor actor in world.Characters.Where(candidate =>
                     candidate != null && !candidate.IsDead
                     && candidate.Identity?.CharacterType != CharacterType.Intruder))
        {
            CharacterGriefAggregate state = grief.Require(
                CharacterPersistentIdentity.Require(actor));
            state.AdvanceToDay(nextDay);
            ProjectPsychosocialMood(actor, nextDay);
        }
    }

    private GriefRelationshipKind ResolveRelationship(
        CharacterId survivor,
        CharacterId deceased,
        CharacterRoomAssignmentSaveData deceasedHousehold)
    {
        if (kinship.GetPartner(survivor).Equals(deceased)
            || kinship.GetParents(deceased, includeAdoptive: true)
                .Contains(survivor))
        {
            return GriefRelationshipKind.PartnerOrChild;
        }

        if (kinship.GetParents(survivor, includeAdoptive: true)
                .Contains(deceased)
            || kinship.IsSibling(survivor, deceased)
            || kinship.GetGuardian(survivor).Equals(deceased))
        {
            return GriefRelationshipKind.ParentSiblingOrGuardian;
        }

        if (deceasedHousehold != null
            && households.TryGet(
                survivor,
                out CharacterRoomAssignmentSaveData survivorHousehold)
            && string.Equals(
                survivorHousehold.householdId,
                deceasedHousehold.householdId,
                StringComparison.Ordinal))
        {
            return GriefRelationshipKind.Household;
        }

        return GriefRelationshipKind.Colleague;
    }

    private void ProjectPsychosocialMood(CharacterActor actor, int absoluteDay)
    {
        CharacterGriefAggregate state = grief.Require(
            CharacterPersistentIdentity.Require(actor));
        actor.ApplyMoodFactor(
            "mood:grief",
            "mood:grief",
            state.GetProjectedGriefMood(absoluteDay),
            GameCalendarRules.SecondsPerDay,
            1);
        float resolve = state.GetProjectedMemorialResolve(absoluteDay);
        if (resolve > 0f)
        {
            actor.ApplyMoodFactor(
                "mood:memorial-resolve",
                "mood:memorial-resolve",
                resolve,
                GameCalendarRules.SecondsPerDay,
                1);
        }
    }

    private CharacterActor FindActor(CharacterId characterId) =>
        world.Characters.FirstOrDefault(candidate =>
            CharacterPersistentIdentity.TryGet(candidate, out CharacterId id)
            && id.Equals(characterId));

    private void ReassignDependentGuardians(
        CharacterId deceasedId,
        CharacterRoomAssignmentSaveData deceasedHousehold)
    {
        CharacterId[] dependentIds = world.Characters
            .Where(actor => actor != null && !actor.IsDead
                && actor.Identity?.CharacterType != CharacterType.Intruder)
            .Select(CharacterPersistentIdentity.Require)
            .Where(id => IsMinor(id)
                && (kinship.GetGuardian(id).Equals(deceasedId)
                    || kinship.GetParents(id, includeAdoptive: true)
                        .Contains(deceasedId)))
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToArray();

        foreach (CharacterId childId in dependentIds)
        {
            CharacterId guardian = FindSuccessorGuardian(
                childId,
                deceasedId,
                deceasedHousehold);
            if (!guardian.IsValid)
            {
                throw new InvalidOperationException(
                    $"No living adult can serve as public guardian for '{childId.Value}'.");
            }
            kinshipCommands.SetGuardian(childId, guardian);
        }
    }

    private CharacterId FindSuccessorGuardian(
        CharacterId childId,
        CharacterId deceasedId,
        CharacterRoomAssignmentSaveData deceasedHousehold)
    {
        CharacterId[] geneticParents = kinship.GetParents(
                childId,
                includeAdoptive: false)
            .Where(id => !id.Equals(deceasedId))
            .ToArray();
        CharacterId genetic = FirstEligibleAdult(geneticParents, childId);
        if (genetic.IsValid) return genetic;

        HashSet<CharacterId> geneticSet = geneticParents.ToHashSet();
        CharacterId adoptive = FirstEligibleAdult(
            kinship.GetParents(childId, includeAdoptive: true)
                .Where(id => !id.Equals(deceasedId) && !geneticSet.Contains(id)),
            childId);
        if (adoptive.IsValid) return adoptive;

        CharacterId sibling = FirstEligibleAdult(
            LivingResidentIds().Where(id => kinship.IsSibling(childId, id)),
            childId);
        if (sibling.IsValid) return sibling;

        HouseholdId householdId = ResolveHouseholdId(deceasedHousehold);
        if (householdId.IsValid)
        {
            CharacterId householdAdult = FirstEligibleAdult(
                households.GetMembers(householdId),
                childId);
            if (householdAdult.IsValid) return householdAdult;
        }

        CharacterId owner = FirstEligibleAdult(
            world.Characters
                .Where(actor => actor != null && !actor.IsDead && actor.IsOwner)
                .Select(CharacterPersistentIdentity.Require),
            childId);
        if (owner.IsValid) return owner;

        // The deterministic first living adult is the concrete projection of the
        // dungeon public guardian. A guardian link must always reference a real character.
        return FirstEligibleAdult(LivingResidentIds(), childId);
    }

    private CharacterId FirstEligibleAdult(
        IEnumerable<CharacterId> candidates,
        CharacterId childId) =>
        candidates
            .Where(id => id.IsValid && !id.Equals(childId) && IsAdult(id))
            .Distinct()
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .FirstOrDefault();

    private IEnumerable<CharacterId> LivingResidentIds() =>
        world.Characters
            .Where(actor => actor != null && !actor.IsDead
                && actor.Identity?.CharacterType != CharacterType.Intruder)
            .Select(CharacterPersistentIdentity.Require);

    private bool IsMinor(CharacterId id) =>
        life.TryGet(id, out CharacterLifeRecord record)
        && record.LifeStage < CharacterLifeStage.Adult;

    private bool IsAdult(CharacterId id) =>
        life.TryGet(id, out CharacterLifeRecord record)
        && record.LifeStage >= CharacterLifeStage.Adult;

    private static HouseholdId ResolveHouseholdId(
        CharacterRoomAssignmentSaveData assignment) =>
        assignment != null && !string.IsNullOrWhiteSpace(assignment.householdId)
            ? new HouseholdId(assignment.householdId)
            : default;
}
