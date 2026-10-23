using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public enum RoomExperienceActivity
{
    FacilityUse,
    Shopping,
    Work
}

public readonly struct RoomEnvironmentExperienceEvent
{
    public RoomEnvironmentExperienceEvent(
        CharacterActor actor,
        BuildableObject facility,
        RoomExperienceActivity activity)
        : this(actor, facility, activity, default)
    {
    }

    public RoomEnvironmentExperienceEvent(
        CharacterActor actor,
        BuildableObject facility,
        RoomExperienceActivity activity,
        WorkTypeId workTypeId)
    {
        Actor = actor;
        Facility = facility;
        Activity = activity;
        WorkTypeId = workTypeId;
        WorkType = WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            ? FacilityWorkTypeMap.GetRequired(definition)
            : FacilityWorkType.None;
    }

    public CharacterActor Actor { get; }
    public BuildableObject Facility { get; }
    public RoomExperienceActivity Activity { get; }
    public WorkTypeId WorkTypeId { get; }
    internal FacilityWorkType WorkType { get; }
}

public interface IRoomEnvironmentExperienceService
{
    bool Apply(RoomEnvironmentExperienceEvent eventType);
    RoomEnvironmentExperienceApplyResult ApplyDetailed(
        RoomEnvironmentExperienceEvent eventType)
    {
        return new RoomEnvironmentExperienceApplyResult(
            Apply(eventType),
            string.Empty);
    }
    IReadOnlyList<string> GetActiveConditionIds(BuildableObject facility);
}

public readonly struct RoomEnvironmentExperienceApplyResult
{
    public RoomEnvironmentExperienceApplyResult(
        bool applied,
        string outcomeResultKey)
    {
        Applied = applied;
        OutcomeResultKey = outcomeResultKey?.Trim() ?? string.Empty;
    }

    public bool Applied { get; }
    public string OutcomeResultKey { get; }
}

public sealed class RoomEnvironmentExperienceService :
    IRoomEnvironmentExperienceService,
    IRoomEnvironmentOutcomePersistence
{
    private readonly IRoomLayoutCache roomLayoutCache;
    private readonly IRoomEnvironmentEvaluator evaluator;
    private readonly IRoomEnvironmentSettingsProvider settingsProvider;
    private readonly IGameEventBus events;
    private readonly IGameCalendar calendar;
    private readonly IHeritableTraitEffectQuery heritableTraits;
    private readonly IDomainFactPublisher factPublisher;
    private readonly Dictionary<string, float> lastCleanlinessByObserverRoom =
        new(StringComparer.Ordinal);
    private long nextOutcomeSequence = 1L;
    private string lastCommittedExperienceResultKey = string.Empty;
    private PendingRoomExperiencePublication inDoubtExperience;
    private PendingRoomConditionPublication inDoubtCondition;

    public RoomEnvironmentExperienceService(
        IRoomLayoutCache roomLayoutCache,
        IRoomEnvironmentEvaluator evaluator,
        IRoomEnvironmentSettingsProvider settingsProvider,
        IGameEventBus events,
        IGameCalendar calendar,
        IHeritableTraitEffectQuery heritableTraits,
        IDomainFactPublisher factPublisher)
    {
        this.roomLayoutCache = roomLayoutCache
            ?? throw new ArgumentNullException(nameof(roomLayoutCache));
        this.evaluator = evaluator
            ?? throw new ArgumentNullException(nameof(evaluator));
        this.settingsProvider = settingsProvider
            ?? throw new ArgumentNullException(nameof(settingsProvider));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.heritableTraits = heritableTraits
            ?? throw new ArgumentNullException(nameof(heritableTraits));
        this.factPublisher = factPublisher
            ?? throw new ArgumentNullException(nameof(factPublisher));
    }

    public bool Apply(RoomEnvironmentExperienceEvent eventType)
    {
        ResolveInDoubtPublications();
        CharacterActor actor = eventType.Actor;
        BuildableObject facility = eventType.Facility;
        if (actor == null
            || facility == null
            || facility.isDestroy)
        {
            return false;
        }

        PublishManaExposure(eventType);
        if (facility.Grid == null
            || !TryGetFacilityRoom(facility, out RoomInstance room)
            || room == null
            || room.IsSelfContained
            || !room.IsUsable)
        {
            return false;
        }

        RoomEnvironmentSnapshot snapshot = evaluator.Evaluate(facility.Grid, room);
        if (!snapshot.IsEnvironmentActive)
        {
            return false;
        }

        RoomEnvironmentSettingsSO settings = settingsProvider.Settings;
        DungeonStory.Rooms.RoomMoodDecision mood = settings.EvaluateMood(
            snapshot.Impressiveness,
            snapshot.Cleanliness);
        float impressionMood = mood.ImpressionMood;
        float cleanlinessMood = mood.CleanlinessMood;
        BuildingInstanceId facilityId = facility.RequirePersistentInstanceId();
        string roomKey = $"room:{facilityId.Value}:{room.Bounds.xMin}:{room.Bounds.yMin}:{room.Bounds.xMax}:{room.Bounds.yMax}";
        string roomName = RoomEnvironmentPresentation.GetRoomName(snapshot);
        string action = GetActionLabel(eventType, facility);
        string actorDisplayName = actor.Identity?.DisplayName?.Trim() ?? string.Empty;
        string facilityDisplayName = facility.BuildingData?.objectName?.Trim()
            ?? string.Empty;
        if (!CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId)
            || actorDisplayName.Length == 0
            || facilityDisplayName.Length == 0
            || string.IsNullOrWhiteSpace(roomName))
        {
            throw new InvalidOperationException(
                "Room environment outcomes require stable identities and immutable display snapshots.");
        }
        int absoluteDay = calendar.Day;
        CoreGridCell roomLocation = new(room.Bounds.xMin, room.Bounds.yMin);

        CommitRoomCondition(
            characterId,
            actorDisplayName,
            facilityId,
            facilityDisplayName,
            roomKey,
            roomName,
            snapshot.Cleanliness,
            roomLocation,
            absoluteDay);

        if (Mathf.Approximately(impressionMood, 0f)
            && Mathf.Approximately(cleanlinessMood, 0f))
            return false;

        long sequence = nextOutcomeSequence;
        long advancedSequence = checked(sequence + 1L);
        var experienceFact = new RoomEnvironmentExperienceFact(
                sequence,
                characterId,
                actorDisplayName,
                facilityId,
                facilityDisplayName,
                roomKey,
                roomName,
                eventType.Activity,
                eventType.WorkTypeId,
                impressionMood,
                cleanlinessMood,
                settings.MoodDurationSeconds,
                roomLocation,
                absoluteDay);
        if (!factPublisher.TryReserve(
                experienceFact,
                out DomainFactLease experienceLease,
                out string reserveFailure))
            throw new InvalidOperationException(
                "Room environment fact reserve failed: " + reserveFailure);
        if (actor.Stats == null)
        {
            factPublisher.Cancel(experienceLease);
            throw new InvalidOperationException(
                "Room environment outcome requires character mood state.");
        }
        CharacterMoodDeliveryTransactionSnapshot moodBefore =
            actor.Stats.CaptureMoodDeliveryTransactionState();

        try
        {
            if (!Mathf.Approximately(impressionMood, 0f))
            {
                actor.ApplyMoodFactor(
                    $"{roomKey}:impression",
                    BuildImpressionLabel(snapshot.Impressiveness, roomName, action),
                    impressionMood,
                    settings.MoodDurationSeconds,
                    1);
            }

            if (!Mathf.Approximately(cleanlinessMood, 0f))
            {
                actor.ApplyMoodFactor(
                    $"{roomKey}:cleanliness",
                    BuildCleanlinessLabel(snapshot.Cleanliness, roomName),
                    cleanlinessMood,
                    settings.MoodDurationSeconds,
                    1);
            }
        }
        catch
        {
            actor.Stats.RestoreMoodDeliveryTransactionState(moodBefore);
            factPublisher.Cancel(experienceLease);
            throw;
        }

        DomainFactDelivery experienceDelivery;
        try
        {
            experienceDelivery = factPublisher.Publish(
                experienceLease,
                experienceFact);
        }
        catch
        {
            actor.Stats.RestoreMoodDeliveryTransactionState(moodBefore);
            factPublisher.Cancel(experienceLease);
            throw;
        }
        if (experienceDelivery.Phase == DomainFactDeliveryPhase.InDoubt)
        {
            inDoubtExperience = new PendingRoomExperiencePublication(
                experienceFact,
                advancedSequence);
            ResolveInDoubtPublications();
        }
        else if (!experienceDelivery.MayBeCommitted)
        {
            actor.Stats.RestoreMoodDeliveryTransactionState(moodBefore);
            throw new InvalidOperationException(
                "Room environment fact publication failed: "
                + experienceDelivery.Detail);
        }
        else
        {
            CompleteExperiencePublication(
                experienceDelivery,
                advancedSequence);
        }

        return !Mathf.Approximately(impressionMood, 0f)
            || !Mathf.Approximately(cleanlinessMood, 0f);
    }

    public RoomEnvironmentExperienceApplyResult ApplyDetailed(
        RoomEnvironmentExperienceEvent eventType)
    {
        string before = lastCommittedExperienceResultKey;
        bool applied = Apply(eventType);
        string after = lastCommittedExperienceResultKey;
        return new RoomEnvironmentExperienceApplyResult(
            applied,
            applied && !string.Equals(before, after, StringComparison.Ordinal)
                ? after
                : string.Empty);
    }

    public IReadOnlyList<string> GetActiveConditionIds(BuildableObject facility)
    {
        if (facility == null
            || facility.Grid == null
            || !TryGetFacilityRoom(facility, out RoomInstance room)
            || room == null
            || !room.IsUsable)
            return Array.Empty<string>();

        RoomEnvironmentSnapshot snapshot = evaluator.Evaluate(facility.Grid, room);
        bool noisy = snapshot.Fixtures.Any(fixture =>
            fixture != null
            && fixture != facility
            && (fixture.BuildingData?.category is BuildingCategory.Production
                or BuildingCategory.Crafting
                || fixture.BuildingData?.GetAbility<BuildingProductionAbility>() != null
                || fixture.SupportsFacilityRole(FacilityRole.Training)
                || fixture.SupportsFacilityRole(FacilityRole.Mana)));
        List<string> conditions = new(2);
        if (noisy)
            conditions.Add("room:noise");
        if (snapshot.Area <= 8 && snapshot.DoorCount > 0)
            conditions.Add("room:private");
        return conditions;
    }

    private void CommitRoomCondition(
        CharacterId characterId,
        string actorDisplayName,
        BuildingInstanceId facilityId,
        string facilityDisplayName,
        string roomKey,
        string roomDisplayName,
        float currentCleanliness,
        CoreGridCell location,
        int absoluteDay)
    {
        string observerRoomKey = characterId.Value + "\n" + roomKey;
        if (lastCleanlinessByObserverRoom.TryGetValue(
                observerRoomKey,
                out float previousCleanliness)
            && !Mathf.Approximately(previousCleanliness, currentCleanliness))
        {
            long sequence = nextOutcomeSequence;
            long advancedSequence = checked(sequence + 1L);
            var conditionFact = new RoomEnvironmentConditionFact(
                    sequence,
                    characterId,
                    actorDisplayName,
                    facilityId,
                    facilityDisplayName,
                    roomKey,
                    roomDisplayName,
                    previousCleanliness,
                    currentCleanliness,
                    location,
                    absoluteDay);
            if (!factPublisher.TryReserve(
                    conditionFact,
                    out DomainFactLease conditionLease,
                    out string reserveFailure))
                throw new InvalidOperationException(
                    "Room condition fact reserve failed: " + reserveFailure);

            lastCleanlinessByObserverRoom[observerRoomKey] = currentCleanliness;
            DomainFactDelivery delivery;
            try
            {
                delivery = factPublisher.Publish(conditionLease, conditionFact);
            }
            catch
            {
                lastCleanlinessByObserverRoom[observerRoomKey] = previousCleanliness;
                factPublisher.Cancel(conditionLease);
                throw;
            }
            if (delivery.Phase == DomainFactDeliveryPhase.InDoubt)
            {
                inDoubtCondition = new PendingRoomConditionPublication(
                    conditionFact,
                    advancedSequence);
                ResolveInDoubtPublications();
                return;
            }
            if (!delivery.MayBeCommitted)
            {
                lastCleanlinessByObserverRoom[observerRoomKey] = previousCleanliness;
                throw new InvalidOperationException(
                    "Room condition fact publication failed: " + delivery.Detail);
            }
            CompleteConditionPublication(conditionFact, advancedSequence);
            return;
        }
        lastCleanlinessByObserverRoom[observerRoomKey] = currentCleanliness;
    }

    private void ResolveInDoubtPublications()
    {
        if (inDoubtCondition != null && inDoubtExperience != null)
            throw new InvalidOperationException(
                "Room environment has multiple unresolved fact publications.");

        if (inDoubtCondition != null)
        {
            PendingRoomConditionPublication pending = inDoubtCondition;
            DomainFactDelivery replay = factPublisher.Replay(pending.Fact);
            if (replay.Phase is DomainFactDeliveryPhase.InDoubt or DomainFactDeliveryPhase.Rejected)
                throw new InvalidOperationException(
                    "Room condition fact publication remains unresolved; new room outcomes are blocked: " + replay.Detail);
            inDoubtCondition = null;
            CompleteConditionPublication(pending.Fact, pending.AdvancedSequence);
        }

        if (inDoubtExperience != null)
        {
            PendingRoomExperiencePublication pending = inDoubtExperience;
            DomainFactDelivery replay = factPublisher.Replay(pending.Fact);
            if (replay.Phase is DomainFactDeliveryPhase.InDoubt or DomainFactDeliveryPhase.Rejected)
                throw new InvalidOperationException(
                    "Room experience fact publication remains unresolved; new room outcomes are blocked: " + replay.Detail);
            inDoubtExperience = null;
            CompleteExperiencePublication(replay, pending.AdvancedSequence);
        }
    }

    private void CompleteExperiencePublication(
        in DomainFactDelivery delivery,
        long advancedSequence)
    {
        nextOutcomeSequence = advancedSequence;
        lastCommittedExperienceResultKey = delivery.OperationKey;
    }

    private void CompleteConditionPublication(
        RoomEnvironmentConditionFact fact,
        long advancedSequence)
    {
        nextOutcomeSequence = advancedSequence;
        events.Publish(new RoomConditionChangedEvent(
            fact.ObserverId,
            fact.RoomId,
            fact.PreviousCleanliness / 100f,
            fact.CurrentCleanliness / 100f,
            fact.AbsoluteDay));
    }

    private sealed class PendingRoomExperiencePublication
    {
        public PendingRoomExperiencePublication(
            RoomEnvironmentExperienceFact fact,
            long advancedSequence)
        {
            Fact = fact ?? throw new ArgumentNullException(nameof(fact));
            AdvancedSequence = advancedSequence;
        }

        public RoomEnvironmentExperienceFact Fact { get; }
        public long AdvancedSequence { get; }
    }

    private sealed class PendingRoomConditionPublication
    {
        public PendingRoomConditionPublication(
            RoomEnvironmentConditionFact fact,
            long advancedSequence)
        {
            Fact = fact ?? throw new ArgumentNullException(nameof(fact));
            AdvancedSequence = advancedSequence;
        }

        public RoomEnvironmentConditionFact Fact { get; }
        public long AdvancedSequence { get; }
    }

    public RoomEnvironmentOutcomeSaveData CaptureOutcomeState() => new()
    {
        nextOutcomeSequence = nextOutcomeSequence,
        pendingFacts = CapturePendingFacts(),
        observations = lastCleanlinessByObserverRoom
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => new RoomEnvironmentObservationSaveData
            {
                observerRoomKey = value.Key,
                cleanliness = value.Value
            })
            .ToList()
    };

    private List<RoomPendingFactSaveData> CapturePendingFacts()
    {
        var rows = new List<RoomPendingFactSaveData>(1);
        var fact = RoomPendingFactSaveData.Capture(inDoubtExperience?.Fact)
            ?? RoomPendingFactSaveData.Capture(inDoubtCondition?.Fact);
        if (fact != null) rows.Add(fact);
        return rows;
    }

    public RoomEnvironmentOutcomeRestoreCandidate PrepareOutcomeRestore(
        RoomEnvironmentOutcomeSaveData data)
    {
        if (data == null
            || data.version != RoomEnvironmentOutcomeSaveData.CurrentVersion
            || data.nextOutcomeSequence <= 0L
            || data.pendingFacts == null || data.pendingFacts.Count > 1
            || (data.pendingFacts.Count == 1 && data.pendingFacts[0] == null))
            throw new InvalidOperationException("Room environment outcome save header is invalid.");
        Dictionary<string, float> restored = new(StringComparer.Ordinal);
        foreach (RoomEnvironmentObservationSaveData row in
                 data.observations ?? new List<RoomEnvironmentObservationSaveData>())
        {
            if (row == null
                || string.IsNullOrWhiteSpace(row.observerRoomKey)
                || !float.IsFinite(row.cleanliness)
                || row.cleanliness < 0f
                || row.cleanliness > 100f
                || !restored.TryAdd(row.observerRoomKey, row.cleanliness))
                throw new InvalidOperationException(
                    "Room environment observation save row is invalid.");
        }
        return new RoomEnvironmentOutcomeRestoreCandidate(
            data.nextOutcomeSequence,
            restored,
            RestorePendingExperience(data),
            RestorePendingCondition(data, restored));
    }

    private static RoomEnvironmentExperienceFact RestorePendingExperience(RoomEnvironmentOutcomeSaveData data)
    {
        if (data.pendingFacts.Count == 0) return null;
        data.pendingFacts[0].Validate(data.nextOutcomeSequence);
        return data.pendingFacts[0].Experience();
    }

    private static RoomEnvironmentConditionFact RestorePendingCondition(RoomEnvironmentOutcomeSaveData data,
        IReadOnlyDictionary<string, float> observations)
    {
        var fact = data.pendingFacts.Count == 0 ? null : data.pendingFacts[0].Condition();
        if (fact != null && (!observations.TryGetValue(fact.ObserverId.Value + "\n" + fact.RoomId, out float current)
            || current != fact.CurrentCleanliness))
            throw new InvalidOperationException("Pending room condition disagrees with its saved domain observation.");
        return fact;
    }

    public void PublishOutcomeRestore(RoomEnvironmentOutcomeRestoreCandidate candidate)
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));
        lastCleanlinessByObserverRoom.Clear();
        foreach (KeyValuePair<string, float> row in candidate.Observations)
            lastCleanlinessByObserverRoom.Add(row.Key, row.Value);
        nextOutcomeSequence = candidate.NextOutcomeSequence;
        inDoubtExperience = candidate.PendingExperience == null ? null : new PendingRoomExperiencePublication(
            candidate.PendingExperience, checked(candidate.NextOutcomeSequence + 1L));
        inDoubtCondition = candidate.PendingCondition == null ? null : new PendingRoomConditionPublication(
            candidate.PendingCondition, checked(candidate.NextOutcomeSequence + 1L));
        lastCommittedExperienceResultKey = string.Empty;
    }

    private void PublishManaExposure(RoomEnvironmentExperienceEvent eventType)
    {
        FacilityRole roles = eventType.Facility.Facility?.roles ?? FacilityRole.None;
        if ((roles & FacilityRole.Mana) == 0
            || !CharacterPersistentIdentity.TryGet(eventType.Actor, out CharacterId characterId))
        {
            return;
        }

        float exposureHours = eventType.Activity switch
        {
            RoomExperienceActivity.Work => 8f,
            RoomExperienceActivity.FacilityUse => 2f,
            _ => 1f
        };
        events.Publish(new PopulationDiseaseRouteExposureEvent(
            characterId,
            "disease:mana-pox",
            DiseaseTransmissionRoute.ManaExposure,
            exposureHours,
            heritableTraits.GetMultiplier(
                characterId,
                HeritableTraitConsequenceKind.ManaOverloadDamage,
                "mana-exposure") / heritableTraits.GetMultiplier(characterId,
                    HeritableTraitConsequenceKind.ManaAffinity, "stability"),
            sourceKind: "room-mana-exposure",
            sourceId: eventType.Facility.RequirePersistentInstanceId().Value));
    }

    private bool TryGetFacilityRoom(BuildableObject facility, out RoomInstance room)
    {
        room = null;
        if (facility == null || facility.Grid == null)
        {
            return false;
        }

        if (facility.buildPoses != null)
        {
            foreach (Vector2Int position in facility.buildPoses)
            {
                if (roomLayoutCache.TryGetRoom(facility.Grid, position, out room))
                {
                    return true;
                }
            }
        }

        return roomLayoutCache.TryGetRoom(facility.Grid, facility.centerPos, out room);
    }

    private static string GetActionLabel(
        RoomEnvironmentExperienceEvent eventType,
        BuildableObject facility)
    {
        if (eventType.Activity == RoomExperienceActivity.Shopping)
        {
            return "쇼핑함";
        }

        if (eventType.Activity == RoomExperienceActivity.Work)
        {
            return eventType.WorkType switch
            {
                FacilityWorkType.Research => "연구함",
                FacilityWorkType.Construct => "건설함",
                FacilityWorkType.Repair => "수리함",
                FacilityWorkType.Clean => "청소함",
                FacilityWorkType.Guard => "근무함",
                FacilityWorkType.Restock => "보급함",
                FacilityWorkType.Rescue => "구조 작업을 함",
                FacilityWorkType.Rest => "휴식함",
                _ => "일함"
            };
        }

        FacilityRole roles = facility.Facility?.roles ?? FacilityRole.None;
        if ((roles & FacilityRole.Meal) != 0) return "식사함";
        if ((roles & FacilityRole.Rest) != 0) return "휴식함";
        if ((roles & FacilityRole.Hygiene) != 0) return "씻음";
        if ((roles & FacilityRole.Toilet) != 0) return "용변을 봄";
        if ((roles & FacilityRole.Training) != 0) return "훈련함";
        if ((roles & FacilityRole.Research) != 0) return "연구함";
        if ((roles & FacilityRole.Mana) != 0) return "마력을 다룸";
        if ((roles & FacilityRole.Purchase) != 0) return "쇼핑함";
        return "시간을 보냄";
    }

    private static string BuildImpressionLabel(float value, string roomName, string action)
    {
        if (value < 20f) return $"끔찍한 {roomName}에서 {action}";
        if (value < 40f) return $"초라한 {roomName}에서 {action}";
        if (value < 80f) return $"쾌적한 {roomName}에서 {action}";
        return $"인상적인 {roomName}에서 {action}";
    }

    private static string BuildCleanlinessLabel(float value, string roomName)
    {
        if (value < 20f) return $"지저분한 {roomName}이 신경 쓰임";
        if (value < 40f) return $"어수선한 {roomName}이 거슬림";
        return "깔끔한 공간이 마음에 듦";
    }
}
