using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public readonly struct CharacterAgeConditionChangedEvent
{
    public CharacterAgeConditionChangedEvent(AgeConditionChange change)
    {
        Change = change;
    }

    public AgeConditionChange Change { get; }
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

public sealed class CharacterLifeApplicationAdapter : IStartable, IDisposable
{
    private readonly CharacterLifeRuntime life;
    private readonly IGameEventBus events;
    private readonly IHeritableTraitEffectQuery heritableTraits;
    private IDisposable dayEndedSubscription;

    public CharacterLifeApplicationAdapter(
        CharacterLifeRuntime life,
        IGameEventBus events,
        IHeritableTraitEffectQuery heritableTraits)
    {
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.heritableTraits = heritableTraits
            ?? throw new ArgumentNullException(nameof(heritableTraits));
    }

    public void Start()
    {
        dayEndedSubscription = events.Subscribe<OperatingDayEndedEvent>(OnDayEnded);
    }

    public void Dispose()
    {
        dayEndedSubscription?.Dispose();
        dayEndedSubscription = null;
    }

    private void OnDayEnded(OperatingDayEndedEvent _)
    {
        Dictionary<CharacterId, CharacterLifeStage> previousStages = life.Records
            .ToDictionary(value => value.CharacterId, value => value.LifeStage);
        List<AgeConditionChange> changes = new();
        foreach (CharacterId characterId in previousStages.Keys
                     .OrderBy(value => value.Value, StringComparer.Ordinal))
        {
            changes.AddRange(life.AdvanceDay(
                characterId,
                heritableTraits.GetMultiplier(
                    characterId,
                    HeritableTraitConsequenceKind.AgingRate,
                    "biological-age")));
        }
        for (int index = 0; index < changes.Count; index++)
        {
            events.Publish(new CharacterAgeConditionChangedEvent(changes[index]));
        }
        foreach (CharacterLifeRecord record in life.Records)
        {
            if (previousStages.TryGetValue(
                    record.CharacterId,
                    out CharacterLifeStage previous)
                && previous != record.LifeStage)
            {
                events.Publish(new CharacterLifeStageChangedEvent(
                    record.CharacterId,
                    previous,
                    record.LifeStage));
            }
        }
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
        AnatomyNodeHealthState[] affected = definition.AffectedAnatomyNodeIds
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
    private readonly IReproductionService reproduction;
    private readonly IGriefTraumaService grief;
    private readonly IGameEventBus events;
    private IDisposable deathSubscription;
    private IDisposable dayEndedSubscription;

    public CharacterDeathPopulationAdapter(
        ICharacterWorldQuery world,
        ICharacterLifeQuery life,
        IKinshipQuery kinship,
        IKinshipCommand kinshipCommands,
        IHouseholdService households,
        IReproductionService reproduction,
        IGriefTraumaService grief,
        IGameEventBus events)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.kinship = kinship ?? throw new ArgumentNullException(nameof(kinship));
        this.kinshipCommands = kinshipCommands
            ?? throw new ArgumentNullException(nameof(kinshipCommands));
        this.households = households
            ?? throw new ArgumentNullException(nameof(households));
        this.reproduction = reproduction
            ?? throw new ArgumentNullException(nameof(reproduction));
        this.grief = grief ?? throw new ArgumentNullException(nameof(grief));
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
        CharacterActor deceasedActor = FindActor(death.CharacterId);
        reproduction.NotifyCarrierDeath(death.CharacterId, death.AbsoluteDay);

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
