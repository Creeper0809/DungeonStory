using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public interface ICharacterIdentityEvent
{
    string EventId { get; }
    int AbsoluteDay { get; }
    CharacterCommandOrigin Origin { get; }
}

public readonly struct CharacterKilledEvent : ICharacterIdentityEvent
{
    public CharacterKilledEvent(
        CharacterId killer,
        CharacterId victim,
        IReadOnlyList<CharacterId> witnesses,
        bool wasHostile,
        bool wasPrisoner,
        bool wasInnocent,
        CharacterCommandOrigin origin,
        int absoluteDay)
    {
        if (!killer.IsValid) throw new ArgumentException("Killer is required.", nameof(killer));
        if (!victim.IsValid) throw new ArgumentException("Victim is required.", nameof(victim));
        Killer = killer;
        Victim = victim;
        Witnesses = (witnesses ?? Array.Empty<CharacterId>())
            .Where(value => value.IsValid && !value.Equals(killer) && !value.Equals(victim))
            .Distinct()
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
        WasHostile = wasHostile;
        WasPrisoner = wasPrisoner;
        WasInnocent = wasInnocent;
        Origin = origin;
        AbsoluteDay = Math.Max(0, absoluteDay);
    }

    public string EventId => WasInnocent
        ? "event:character-killed-innocent"
        : WasPrisoner
            ? "event:character-killed-prisoner"
            : "event:character-killed-hostile";
    public CharacterId Killer { get; }
    public CharacterId Victim { get; }
    public IReadOnlyList<CharacterId> Witnesses { get; }
    public bool WasHostile { get; }
    public bool WasPrisoner { get; }
    public bool WasInnocent { get; }
    public CharacterCommandOrigin Origin { get; }
    public int AbsoluteDay { get; }
}

public readonly struct CharacterDiedEvent : ICharacterIdentityEvent
{
    public CharacterDiedEvent(CharacterId character, string causeId, int absoluteDay)
    {
        if (!character.IsValid) throw new ArgumentException("Character is required.", nameof(character));
        Character = character;
        CauseId = string.IsNullOrWhiteSpace(causeId)
            ? throw new ArgumentException("Cause is required.", nameof(causeId))
            : causeId.Trim();
        AbsoluteDay = Math.Max(0, absoluteDay);
    }
    public string EventId => "event:character-died";
    public CharacterId Character { get; }
    public string CauseId { get; }
    public int AbsoluteDay { get; }
    public CharacterCommandOrigin Origin => CharacterCommandOrigin.ScriptedForced;
}

public readonly struct ApparelChangedEvent : ICharacterIdentityEvent
{
    public ApparelChangedEvent(CharacterId character, string apparelId, bool equipped, CharacterCommandOrigin origin, int day)
    { Character = character; ApparelId = Require(apparelId); Equipped = equipped; Origin = origin; AbsoluteDay = Math.Max(0, day); }
    public string EventId => "event:apparel-changed";
    public CharacterId Character { get; }
    public string ApparelId { get; }
    public bool Equipped { get; }
    public CharacterCommandOrigin Origin { get; }
    public int AbsoluteDay { get; }
    private static string Require(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Apparel id is required.") : value.Trim();
}

public readonly struct WorkCompletedIdentityEvent : ICharacterIdentityEvent
{
    public WorkCompletedIdentityEvent(CharacterId character, string workId, string productId, CharacterCommandOrigin origin, int day)
    { Character = character; WorkId = Required(workId); ProductId = productId?.Trim() ?? string.Empty; Origin = origin; AbsoluteDay = Math.Max(0, day); }
    public string EventId => "event:work-completed";
    public CharacterId Character { get; }
    public string WorkId { get; }
    public string ProductId { get; }
    public CharacterCommandOrigin Origin { get; }
    public int AbsoluteDay { get; }
    private static string Required(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Work id is required.") : value.Trim();
}

public readonly struct WorkCompletionIdentityDeliveryRequest
{
    public WorkCompletionIdentityDeliveryRequest(
        string deliveryId,
        string producerStreamId,
        int operationSequence,
        CharacterId character,
        string workId,
        string productId,
        CharacterCommandOrigin origin,
        int absoluteDay)
    {
        DeliveryId = RequireCanonical(deliveryId, nameof(deliveryId));
        ProducerStreamId = RequireCanonical(
            producerStreamId,
            nameof(producerStreamId));
        if (operationSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(operationSequence));
        if (!character.IsValid)
            throw new ArgumentException(
                "A valid completion character is required.",
                nameof(character));
        OperationSequence = operationSequence;
        Character = character;
        WorkId = RequireCanonical(workId, nameof(workId));
        ProductId = RequireCanonical(productId, nameof(productId));
        if (!Enum.IsDefined(typeof(CharacterCommandOrigin), origin))
            throw new ArgumentOutOfRangeException(nameof(origin));
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        Origin = origin;
        AbsoluteDay = absoluteDay;
        PayloadFingerprint = ComputeFingerprint(
            DeliveryId,
            ProducerStreamId,
            OperationSequence,
            Character,
            WorkId,
            ProductId,
            Origin,
            AbsoluteDay);
    }

    public string DeliveryId { get; }
    public string ProducerStreamId { get; }
    public int OperationSequence { get; }
    public CharacterId Character { get; }
    public string WorkId { get; }
    public string ProductId { get; }
    public CharacterCommandOrigin Origin { get; }
    public int AbsoluteDay { get; }
    public string PayloadFingerprint { get; }

    public WorkCompletedIdentityEvent ToEvent() => new(
        Character,
        WorkId,
        ProductId,
        Origin,
        AbsoluteDay);

    public static string ComputeFingerprint(
        string deliveryId,
        string producerStreamId,
        int operationSequence,
        CharacterId character,
        string workId,
        string productId,
        CharacterCommandOrigin origin,
        int absoluteDay)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("work-completion-identity-delivery@1");
        digest.Append(deliveryId);
        digest.Append(producerStreamId);
        digest.Append(operationSequence);
        digest.Append(character.Value);
        digest.Append(workId);
        digest.Append(productId);
        digest.AppendEnum(origin);
        digest.Append(absoluteDay);
        return digest.ComputeSha256();
    }

    private static string RequireCanonical(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new ArgumentException(
                "A nonempty canonical token is required.",
                parameterName);
        return value;
    }
}

public enum WorkCompletionIdentityDeliveryStatus
{
    Applied = 0,
    AlreadyApplied = 1,
    Deferred = 2,
    Conflict = 3
}

public readonly struct WorkCompletionIdentityDeliveryResult
{
    public WorkCompletionIdentityDeliveryResult(
        WorkCompletionIdentityDeliveryStatus status,
        string failureReason = "")
    {
        Status = status;
        FailureReason = failureReason ?? string.Empty;
    }

    public WorkCompletionIdentityDeliveryStatus Status { get; }
    public string FailureReason { get; }
    public bool IsApplied => Status is WorkCompletionIdentityDeliveryStatus.Applied
        or WorkCompletionIdentityDeliveryStatus.AlreadyApplied;
}

public interface IWorkCompletionIdentityDeliveryCommand
{
    WorkCompletionIdentityDeliveryResult EnsureApplied(
        WorkCompletionIdentityDeliveryRequest request);

    bool RetireProducerStream(string producerStreamId);
}

public readonly struct RestOutcomeIdentityEvent : ICharacterIdentityEvent
{
    public RestOutcomeIdentityEvent(
        CharacterId character,
        float previousSleep,
        float currentSleep,
        IReadOnlyList<string> conditionIds,
        int day)
    {
        if (!character.IsValid)
            throw new ArgumentException("Character is required.", nameof(character));
        Character = character;
        PreviousSleep = Mathf.Clamp(previousSleep, 0f, 100f);
        CurrentSleep = Mathf.Clamp(currentSleep, 0f, 100f);
        ConditionIds = (conditionIds ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        AbsoluteDay = Math.Max(0, day);
    }

    public string EventId => "event:rest-outcome";
    public CharacterId Character { get; }
    public float PreviousSleep { get; }
    public float CurrentSleep { get; }
    public IReadOnlyList<string> ConditionIds { get; }
    public int AbsoluteDay { get; }
    public CharacterCommandOrigin Origin => CharacterCommandOrigin.Autonomous;
}

public readonly struct ProductQualityResolvedEvent : ICharacterIdentityEvent
{
    public ProductQualityResolvedEvent(CharacterId maker, string definitionId, CraftsmanshipQualityTier quality, int attemptIndex, int day, bool rejectedBelowMinimum = false)
    { Maker = maker; DefinitionId = string.IsNullOrWhiteSpace(definitionId) ? throw new ArgumentException("Definition id is required.") : definitionId.Trim(); Quality = quality; AttemptIndex = Math.Max(0, attemptIndex); AbsoluteDay = Math.Max(0, day); RejectedBelowMinimum = rejectedBelowMinimum; }
    public string EventId => "event:product-quality-resolved";
    public CharacterId Maker { get; }
    public string DefinitionId { get; }
    public CraftsmanshipQualityTier Quality { get; }
    public int AttemptIndex { get; }
    public int AbsoluteDay { get; }
    public bool RejectedBelowMinimum { get; }
    public CharacterCommandOrigin Origin => CharacterCommandOrigin.Autonomous;
}

public readonly struct WorkStartedIdentityEvent : ICharacterIdentityEvent
{
    public WorkStartedIdentityEvent(
        CharacterId character,
        string workId,
        CharacterCommandOrigin origin,
        int day)
    { Character = Require(character); WorkId = Require(workId); Origin = origin; AbsoluteDay = Math.Max(0, day); }
    public string EventId => "event:work-started";
    public CharacterId Character { get; }
    public string WorkId { get; }
    public CharacterCommandOrigin Origin { get; }
    public int AbsoluteDay { get; }
    private static CharacterId Require(CharacterId value) => value.IsValid ? value : throw new ArgumentException("Character is required.");
    private static string Require(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Work id is required.") : value.Trim();
}

public readonly struct MealConsumedEvent : ICharacterIdentityEvent
{
    public MealConsumedEvent(CharacterId character, string mealId, IReadOnlyList<string> tags, bool wasSufficient, CharacterCommandOrigin origin, int day)
    { Character = Valid(character); MealId = Text(mealId); Tags = Normalize(tags); WasSufficient = wasSufficient; Origin = origin; AbsoluteDay = Math.Max(0, day); }
    public string EventId => "event:meal-consumed";
    public CharacterId Character { get; }
    public string MealId { get; }
    public IReadOnlyList<string> Tags { get; }
    public bool WasSufficient { get; }
    public CharacterCommandOrigin Origin { get; }
    public int AbsoluteDay { get; }
    private static CharacterId Valid(CharacterId value) => value.IsValid ? value : throw new ArgumentException("Character is required.");
    private static string Text(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Meal id is required.") : value.Trim();
    private static string[] Normalize(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
}

public readonly struct MealMissedEvent : ICharacterIdentityEvent
{
    public MealMissedEvent(CharacterId character, int consecutiveMisses, int day)
    { Character = character.IsValid ? character : throw new ArgumentException("Character is required."); ConsecutiveMisses = Math.Max(1, consecutiveMisses); AbsoluteDay = Math.Max(0, day); }
    public string EventId => "event:meal-missed";
    public CharacterId Character { get; }
    public int ConsecutiveMisses { get; }
    public int AbsoluteDay { get; }
    public CharacterCommandOrigin Origin => CharacterCommandOrigin.Autonomous;
}

public readonly struct ResearchProgressEvent : ICharacterIdentityEvent
{
    public ResearchProgressEvent(CharacterId researcher, string projectId, float approvedWork, float progressDelta, int day)
    { Researcher = researcher.IsValid ? researcher : throw new ArgumentException("Researcher is required."); ProjectId = Text(projectId); ApprovedWork = Mathf.Max(0f, approvedWork); ProgressDelta = progressDelta; AbsoluteDay = Math.Max(0, day); }
    public string EventId => "event:research-progress";
    public CharacterId Researcher { get; }
    public string ProjectId { get; }
    public float ApprovedWork { get; }
    public float ProgressDelta { get; }
    public int AbsoluteDay { get; }
    public CharacterCommandOrigin Origin => CharacterCommandOrigin.Autonomous;
    private static string Text(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Project id is required.") : value.Trim();
}

public readonly struct ResearchOutcomeEvent : ICharacterIdentityEvent
{
    public ResearchOutcomeEvent(CharacterId researcher, string projectId, string outcomeId, CharacterCommandOrigin origin, int day)
    { Researcher = researcher.IsValid ? researcher : throw new ArgumentException("Researcher is required."); ProjectId = Text(projectId); OutcomeId = Text(outcomeId); Origin = origin; AbsoluteDay = Math.Max(0, day); }
    public string EventId => "event:research-outcome";
    public CharacterId Researcher { get; }
    public string ProjectId { get; }
    public string OutcomeId { get; }
    public CharacterCommandOrigin Origin { get; }
    public int AbsoluteDay { get; }
    private static string Text(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Research outcome field is required.") : value.Trim();
}

public readonly struct SocialConflictEvent : ICharacterIdentityEvent
{
    public SocialConflictEvent(CharacterId instigator, CharacterId target, string conflictId, float severity, CharacterCommandOrigin origin, int day)
    { Instigator = Valid(instigator); Target = Valid(target); ConflictId = Text(conflictId); Severity = Mathf.Max(0f, severity); Origin = origin; AbsoluteDay = Math.Max(0, day); }
    public string EventId => "event:social-conflict";
    public CharacterId Instigator { get; }
    public CharacterId Target { get; }
    public string ConflictId { get; }
    public float Severity { get; }
    public CharacterCommandOrigin Origin { get; }
    public int AbsoluteDay { get; }
    private static CharacterId Valid(CharacterId value) => value.IsValid ? value : throw new ArgumentException("Conflict participant is required.");
    private static string Text(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Conflict id is required.") : value.Trim();
}

public readonly struct ApologyEvent : ICharacterIdentityEvent
{
    public ApologyEvent(CharacterId offender, CharacterId recipient, string offenseId, bool restitutionProvided, int day)
    { Offender = Valid(offender); Recipient = Valid(recipient); OffenseId = Text(offenseId); RestitutionProvided = restitutionProvided; AbsoluteDay = Math.Max(0, day); }
    public string EventId => "event:apology";
    public CharacterId Offender { get; }
    public CharacterId Recipient { get; }
    public string OffenseId { get; }
    public bool RestitutionProvided { get; }
    public int AbsoluteDay { get; }
    public CharacterCommandOrigin Origin => CharacterCommandOrigin.DirectPlayerOrder;
    private static CharacterId Valid(CharacterId value) => value.IsValid ? value : throw new ArgumentException("Apology participant is required.");
    private static string Text(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Offense id is required.") : value.Trim();
}

public readonly struct FestivalOutcomeEvent : ICharacterIdentityEvent
{
    public FestivalOutcomeEvent(CharacterId participant, string festivalId, string outcomeId, int day)
    { Participant = participant.IsValid ? participant : throw new ArgumentException("Participant is required."); FestivalId = Text(festivalId); OutcomeId = Text(outcomeId); AbsoluteDay = Math.Max(0, day); }
    public string EventId => "event:festival-outcome";
    public CharacterId Participant { get; }
    public string FestivalId { get; }
    public string OutcomeId { get; }
    public int AbsoluteDay { get; }
    public CharacterCommandOrigin Origin => CharacterCommandOrigin.ScriptedForced;
    private static string Text(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Festival outcome field is required.") : value.Trim();
}

public readonly struct PrisonerDecisionEvent : ICharacterIdentityEvent
{
    public PrisonerDecisionEvent(CharacterId decider, CharacterId prisoner, string decisionId, CharacterCommandOrigin origin, int day)
    { Decider = Valid(decider); Prisoner = Valid(prisoner); DecisionId = Text(decisionId); Origin = origin; AbsoluteDay = Math.Max(0, day); }
    public string EventId => "event:prisoner-decision";
    public CharacterId Decider { get; }
    public CharacterId Prisoner { get; }
    public string DecisionId { get; }
    public CharacterCommandOrigin Origin { get; }
    public int AbsoluteDay { get; }
    private static CharacterId Valid(CharacterId value) => value.IsValid ? value : throw new ArgumentException("Prisoner decision participant is required.");
    private static string Text(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Decision id is required.") : value.Trim();
}

public readonly struct HealthThresholdCrossedEvent : ICharacterIdentityEvent
{
    public HealthThresholdCrossedEvent(CharacterId character, float previousRatio, float currentRatio, bool coreOrganCritical, int day)
    { Character = character.IsValid ? character : throw new ArgumentException("Character is required."); PreviousRatio = Mathf.Clamp01(previousRatio); CurrentRatio = Mathf.Clamp01(currentRatio); CoreOrganCritical = coreOrganCritical; AbsoluteDay = Math.Max(0, day); }
    public string EventId => "event:health-threshold-crossed";
    public CharacterId Character { get; }
    public float PreviousRatio { get; }
    public float CurrentRatio { get; }
    public bool CoreOrganCritical { get; }
    public int AbsoluteDay { get; }
    public CharacterCommandOrigin Origin => CharacterCommandOrigin.ScriptedForced;
}

public readonly struct CharacterInjuredIdentityEvent : ICharacterIdentityEvent
{
    public CharacterInjuredIdentityEvent(
        CharacterId character,
        CharacterId attacker,
        CombatDamageType damageType,
        float appliedDamage,
        int day)
    {
        Character = character.IsValid
            ? character
            : throw new ArgumentException("Injured character is required.");
        Attacker = attacker;
        DamageType = damageType;
        AppliedDamage = Mathf.Max(0f, appliedDamage);
        AbsoluteDay = Math.Max(0, day);
    }

    public string EventId => "event:character-injured";
    public CharacterId Character { get; }
    public CharacterId Attacker { get; }
    public CombatDamageType DamageType { get; }
    public float AppliedDamage { get; }
    public int AbsoluteDay { get; }
    public CharacterCommandOrigin Origin => CharacterCommandOrigin.ScriptedForced;
}

public readonly struct ExpeditionOutcomeEvent : ICharacterIdentityEvent
{
    public ExpeditionOutcomeEvent(string expeditionId, IReadOnlyList<CharacterId> participants, string outcomeId, int day)
    { ExpeditionId = Text(expeditionId); Participants = (participants ?? Array.Empty<CharacterId>()).Where(value => value.IsValid).Distinct().OrderBy(value => value.Value, StringComparer.Ordinal).ToArray(); if (Participants.Count == 0) throw new ArgumentException("Expedition participants are required."); OutcomeId = Text(outcomeId); AbsoluteDay = Math.Max(0, day); }
    public string EventId => "event:expedition-outcome";
    public string ExpeditionId { get; }
    public IReadOnlyList<CharacterId> Participants { get; }
    public string OutcomeId { get; }
    public int AbsoluteDay { get; }
    public CharacterCommandOrigin Origin => CharacterCommandOrigin.ScriptedForced;
    private static string Text(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Expedition outcome field is required.") : value.Trim();
}

public readonly struct RoomConditionChangedEvent : ICharacterIdentityEvent
{
    public RoomConditionChangedEvent(CharacterId observer, string roomId, float previousCleanliness, float currentCleanliness, int day)
    { Observer = observer.IsValid ? observer : throw new ArgumentException("Observer is required."); RoomId = Text(roomId); PreviousCleanliness = Mathf.Clamp01(previousCleanliness); CurrentCleanliness = Mathf.Clamp01(currentCleanliness); AbsoluteDay = Math.Max(0, day); }
    public string EventId => "event:room-condition-changed";
    public CharacterId Observer { get; }
    public string RoomId { get; }
    public float PreviousCleanliness { get; }
    public float CurrentCleanliness { get; }
    public int AbsoluteDay { get; }
    public CharacterCommandOrigin Origin => CharacterCommandOrigin.ScriptedForced;
    private static string Text(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Room id is required.") : value.Trim();
}

public sealed class CharacterIdentityEventPublisher
{
    private readonly IGameEventBus events;
    public CharacterIdentityEventPublisher(IGameEventBus events) =>
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    [GameplayInternalOnly(
        "Typed domain adapters publish identity events through one event-bus boundary.",
        "WorkTaskExecutor|BlueprintResearchRuntime")]
    public void Publish<T>(T gameEvent) where T : struct, ICharacterIdentityEvent =>
        events.Publish(gameEvent);
}

public sealed class WorkCompletionIdentityDeliveryLedger
{
    private readonly Dictionary<string,
        WorkCompletionIdentityDeliveryCursorSaveData> cursors =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> activeDeliveries =
        new(StringComparer.Ordinal);

    public IReadOnlyList<WorkCompletionIdentityDeliveryCursorSaveData> Capture()
    {
        if (activeDeliveries.Count != 0)
            throw new InvalidOperationException(
                "Work-completion delivery capture is unavailable during apply.");
        return cursors.Values
            .OrderBy(value => value.producerStreamId, StringComparer.Ordinal)
            .Select(value => value.Clone())
            .ToArray();
    }

    public void Restore(
        IEnumerable<WorkCompletionIdentityDeliveryCursorSaveData> source)
    {
        if (activeDeliveries.Count != 0)
            throw new InvalidOperationException(
                "Work-completion delivery restore is unavailable during apply.");
        WorkCompletionIdentityDeliveryCursorSaveData[] candidate =
            ValidateAndClone(source);
        cursors.Clear();
        foreach (WorkCompletionIdentityDeliveryCursorSaveData cursor in candidate)
            cursors.Add(cursor.producerStreamId, cursor);
    }

    public WorkCompletionIdentityDeliveryStatus Inspect(
        WorkCompletionIdentityDeliveryRequest request,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!cursors.TryGetValue(
                request.ProducerStreamId,
                out WorkCompletionIdentityDeliveryCursorSaveData cursor))
        {
            if (request.OperationSequence == 0)
                return WorkCompletionIdentityDeliveryStatus.Applied;
            failureReason =
                "The completion delivery stream has no preceding sequence.";
            return WorkCompletionIdentityDeliveryStatus.Conflict;
        }

        if (request.OperationSequence == cursor.operationSequence)
        {
            if (string.Equals(
                    request.DeliveryId,
                    cursor.deliveryId,
                    StringComparison.Ordinal)
                && string.Equals(
                    request.PayloadFingerprint,
                    cursor.payloadFingerprint,
                    StringComparison.Ordinal))
                return WorkCompletionIdentityDeliveryStatus.AlreadyApplied;
            failureReason =
                "The completion delivery sequence has conflicting provenance.";
            return WorkCompletionIdentityDeliveryStatus.Conflict;
        }

        if (request.OperationSequence == checked(cursor.operationSequence + 1))
            return WorkCompletionIdentityDeliveryStatus.Applied;

        failureReason = request.OperationSequence < cursor.operationSequence
            ? "The completion delivery sequence is stale."
            : "The completion delivery sequence is not contiguous.";
        return WorkCompletionIdentityDeliveryStatus.Conflict;
    }

    public WorkCompletionIdentityDeliveryStatus Commit(
        WorkCompletionIdentityDeliveryRequest request,
        out string failureReason,
        WorkCompletionIdentityDeliveryDisposition disposition =
            WorkCompletionIdentityDeliveryDisposition.EffectsApplied)
    {
        if (!Enum.IsDefined(
                typeof(WorkCompletionIdentityDeliveryDisposition),
                disposition))
            throw new ArgumentOutOfRangeException(nameof(disposition));
        WorkCompletionIdentityDeliveryStatus status = Inspect(
            request,
            out failureReason);
        if (status == WorkCompletionIdentityDeliveryStatus.Conflict
            || status == WorkCompletionIdentityDeliveryStatus.AlreadyApplied)
            return status;
        cursors[request.ProducerStreamId] = new()
        {
            producerStreamId = request.ProducerStreamId,
            operationSequence = request.OperationSequence,
            deliveryId = request.DeliveryId,
            payloadFingerprint = request.PayloadFingerprint,
            disposition = disposition
        };
        return WorkCompletionIdentityDeliveryStatus.Applied;
    }

    public void BeginApply(WorkCompletionIdentityDeliveryRequest request)
    {
        if (!activeDeliveries.TryAdd(
                request.ProducerStreamId,
                request.DeliveryId))
            throw new InvalidOperationException(
                "Work-completion delivery stream is already applying.");
    }

    public void EndApply(WorkCompletionIdentityDeliveryRequest request)
    {
        if (!activeDeliveries.TryGetValue(
                request.ProducerStreamId,
                out string activeDeliveryId)
            || !string.Equals(
                activeDeliveryId,
                request.DeliveryId,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Work-completion delivery apply scope is inconsistent.");
        activeDeliveries.Remove(request.ProducerStreamId);
    }

    public bool RetireProducerStream(string producerStreamId)
    {
        if (string.IsNullOrWhiteSpace(producerStreamId)
            || !string.Equals(
                producerStreamId,
                producerStreamId.Trim(),
                StringComparison.Ordinal))
            throw new ArgumentException(
                "A nonempty canonical producer stream is required.",
                nameof(producerStreamId));
        if (activeDeliveries.ContainsKey(producerStreamId))
            throw new InvalidOperationException(
                "An active work-completion delivery stream cannot retire.");
        cursors.Remove(producerStreamId);
        return true;
    }

    public static WorkCompletionIdentityDeliveryCursorSaveData[]
        ValidateAndClone(
            IEnumerable<WorkCompletionIdentityDeliveryCursorSaveData> source)
    {
        Dictionary<string, WorkCompletionIdentityDeliveryCursorSaveData> result =
            new(StringComparer.Ordinal);
        foreach (WorkCompletionIdentityDeliveryCursorSaveData value in
                 source ?? Array.Empty<
                     WorkCompletionIdentityDeliveryCursorSaveData>())
        {
            if (value == null
                || string.IsNullOrWhiteSpace(value.producerStreamId)
                || !string.Equals(
                    value.producerStreamId,
                    value.producerStreamId.Trim(),
                    StringComparison.Ordinal)
                || value.operationSequence < 0
                || string.IsNullOrWhiteSpace(value.deliveryId)
                || !string.Equals(
                    value.deliveryId,
                    value.deliveryId.Trim(),
                    StringComparison.Ordinal)
                || value.payloadFingerprint?.Length != 64
                || value.payloadFingerprint.Any(character =>
                    character is not (>= '0' and <= '9')
                        and not (>= 'a' and <= 'f'))
                || !Enum.IsDefined(
                    typeof(WorkCompletionIdentityDeliveryDisposition),
                    value.disposition)
                || !result.TryAdd(value.producerStreamId, value.Clone()))
                throw new InvalidOperationException(
                    "Work-completion delivery cursor is invalid or duplicated.");
        }
        return result.Values
            .OrderBy(value => value.producerStreamId, StringComparer.Ordinal)
            .ToArray();
    }
}

public interface ICharacterIdentityDeathStateRetentionPolicy
{
    bool TryRetainForPendingExternalOwner(
        string characterId,
        CharacterIdentityRuleStateSaveData state);
}

[Serializable]
public sealed class CharacterIdentityRuleStateSaveData
{
    public string traitDefinitionId = string.Empty;
    public string ruleId = string.Empty;
    public int revision = 1;
    public string statePayload = string.Empty;

    public CharacterIdentityRuleStateSaveData Clone() => new()
    {
        traitDefinitionId = traitDefinitionId?.Trim() ?? string.Empty,
        ruleId = ruleId?.Trim() ?? string.Empty,
        revision = revision,
        statePayload = statePayload ?? string.Empty
    };
}

[Serializable]
public sealed class CharacterIdentityRuntimeStateSaveData
{
    public string characterId = string.Empty;
    public List<CharacterIdentityRuleStateSaveData> rules = new();

    public CharacterIdentityRuntimeStateSaveData Clone() => new()
    {
        characterId = characterId?.Trim() ?? string.Empty,
        rules = (rules ?? new List<CharacterIdentityRuleStateSaveData>())
            .Where(value => value != null)
            .Select(value => value.Clone())
            .ToList()
    };
}

public sealed class CharacterIdentityStateStore
{
    private sealed class Entry
    {
        public string CharacterId;
        public CharacterIdentityRuleStateSaveData State;
    }

    private readonly Dictionary<string, Entry> states = new(StringComparer.Ordinal);

    public static string BuildKey(string characterId, string traitDefinitionId, string ruleId) =>
        $"{EscapeKeyPart(Required(characterId))}+{EscapeKeyPart(Required(traitDefinitionId))}+{EscapeKeyPart(Required(ruleId))}";

    [GameplayInternalOnly(
        "Only identity-rule runtimes may mutate their own stable rule-state record.",
        "CharacterPersistentNeedRuntime|ExtremeTraitRuntime|CharacterRitualFastingRuntime")]
    public void Set(string characterId, string traitDefinitionId, string ruleId, int revision, string payload)
    {
        if (revision <= 0) throw new ArgumentOutOfRangeException(nameof(revision));
        string key = BuildKey(characterId, traitDefinitionId, ruleId);
        states[key] = new Entry
        {
            CharacterId = characterId.Trim(),
            State = new CharacterIdentityRuleStateSaveData
            {
                traitDefinitionId = traitDefinitionId.Trim(),
                ruleId = ruleId.Trim(),
                revision = revision,
                statePayload = payload ?? string.Empty
            }
        };
    }

    public bool TryGet(
        string characterId,
        string traitDefinitionId,
        string ruleId,
        out CharacterIdentityRuleStateSaveData state)
    {
        if (states.TryGetValue(
                BuildKey(characterId, traitDefinitionId, ruleId),
                out Entry stored))
        {
            state = stored.State.Clone();
            return true;
        }
        state = null;
        return false;
    }

    [GameplayInternalOnly(
        "Character death cleanup removes every identity-rule state owned by that character.",
        "CharacterDeathIdentityEventAdapter")]
    public int RemoveCharacter(
        string characterId,
        IEnumerable<ICharacterIdentityDeathStateRetentionPolicy>
            retentionPolicies = null)
    {
        string normalized = Required(characterId);
        ICharacterIdentityDeathStateRetentionPolicy[] policies =
            (retentionPolicies
                ?? Array.Empty<ICharacterIdentityDeathStateRetentionPolicy>())
            .Where(value => value != null)
            .ToArray();
        KeyValuePair<string, Entry>[] owned = states
            .Where(pair => string.Equals(
                pair.Value.CharacterId,
                normalized,
                StringComparison.Ordinal))
            .ToArray();
        string[] keys = owned
            .Where(pair => !policies.Any(policy =>
                policy.TryRetainForPendingExternalOwner(
                    normalized,
                    pair.Value.State.Clone())))
            .Select(pair => pair.Key)
            .ToArray();
        foreach (string key in keys)
            states.Remove(key);
        return keys.Length;
    }

    [GameplayInternalOnly(
        "A completed external owner retires a death-retained identity rule state.",
        "ExtremeTraitRuntime")]
    public bool RemoveRule(
        string characterId,
        string traitDefinitionId,
        string ruleId) => states.Remove(BuildKey(
            characterId,
            traitDefinitionId,
            ruleId));

    public IReadOnlyList<CharacterIdentityRuntimeStateSaveData> Capture() => states
        .GroupBy(pair => pair.Value.CharacterId,
            pair => pair.Value.State,
            StringComparer.Ordinal)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .Select(group => new CharacterIdentityRuntimeStateSaveData
        {
            characterId = group.Key,
            rules = group.OrderBy(value => value.traitDefinitionId, StringComparer.Ordinal)
                .ThenBy(value => value.ruleId, StringComparer.Ordinal)
                .Select(value => value.Clone())
                .ToList()
        }).ToArray();

    internal void RestoreTrustedTransactionSnapshot(
        IEnumerable<CharacterIdentityRuntimeStateSaveData> source)
    {
        Dictionary<string, Entry> restored = new(StringComparer.Ordinal);
        foreach (CharacterIdentityRuntimeStateSaveData character in
                 source ?? throw new ArgumentNullException(nameof(source)))
        {
            if (character == null
                || string.IsNullOrWhiteSpace(character.characterId)
                || !string.Equals(
                    character.characterId,
                    character.characterId.Trim(),
                    StringComparison.Ordinal)
                || character.rules == null)
                throw new InvalidOperationException(
                    "Trusted identity transaction snapshot is invalid.");
            foreach (CharacterIdentityRuleStateSaveData state in character.rules)
            {
                if (state == null
                    || state.revision <= 0
                    || string.IsNullOrWhiteSpace(state.traitDefinitionId)
                    || string.IsNullOrWhiteSpace(state.ruleId)
                    || !string.Equals(
                        state.traitDefinitionId,
                        state.traitDefinitionId.Trim(),
                        StringComparison.Ordinal)
                    || !string.Equals(
                        state.ruleId,
                        state.ruleId.Trim(),
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Trusted identity transaction rule snapshot is invalid.");
                string key = BuildKey(
                    character.characterId,
                    state.traitDefinitionId,
                    state.ruleId);
                if (!restored.TryAdd(key, new Entry
                    {
                        CharacterId = character.characterId,
                        State = state.Clone()
                    }))
                    throw new InvalidOperationException(
                        "Trusted identity transaction snapshot is duplicated.");
            }
        }

        states.Clear();
        foreach (KeyValuePair<string, Entry> pair in restored)
            states.Add(pair.Key, pair.Value);
    }

    [GameplayInternalOnly(
        "Character narrative restore validates the complete candidate state before atomic replacement.",
        "CharacterNarrativeRuntime")]
    public void Restore(
        IEnumerable<CharacterIdentityRuntimeStateSaveData> source,
        IEnumerable<CharacterTraitSO> traitCatalog)
    {
        Dictionary<string, CharacterTraitSO> traits = (traitCatalog ?? Array.Empty<CharacterTraitSO>())
            .Where(value => value != null)
            .ToDictionary(value => value.DefinitionId.Value, StringComparer.Ordinal);
        Dictionary<string, Entry> restored = new(StringComparer.Ordinal);
        foreach (CharacterIdentityRuntimeStateSaveData character in source ?? Array.Empty<CharacterIdentityRuntimeStateSaveData>())
        {
            string characterId = Required(character?.characterId);
            foreach (CharacterIdentityRuleStateSaveData state in character.rules ?? new List<CharacterIdentityRuleStateSaveData>())
            {
                if (state == null || state.revision != 1)
                    throw new InvalidOperationException("Identity rule state has an invalid revision.");
                if (!traits.TryGetValue(Required(state.traitDefinitionId), out CharacterTraitSO trait)
                    || !(trait.identityRules ?? new List<CharacterIdentityRule>()).Any(rule => rule != null && string.Equals(rule.ruleId, state.ruleId?.Trim(), StringComparison.Ordinal)))
                    throw new InvalidOperationException($"Unknown identity rule state '{state?.traitDefinitionId}:{state?.ruleId}'.");
                string key = BuildKey(characterId, state.traitDefinitionId, state.ruleId);
                if (!restored.TryAdd(key, new Entry
                    {
                        CharacterId = characterId,
                        State = state.Clone()
                    }))
                    throw new InvalidOperationException($"Duplicate identity rule state '{key}'.");
            }
        }
        states.Clear();
        foreach (var pair in restored) states.Add(pair.Key, pair.Value);
    }

    private static string Required(string value) => string.IsNullOrWhiteSpace(value)
        ? throw new InvalidOperationException("Identity state key component is empty.")
        : value.Trim();

    private static string EscapeKeyPart(string value) => value
        .Replace("%", "%25")
        .Replace("+", "%2B");
}

[Serializable]
public sealed class CharacterPersistentNeedRuntimeState
{
    public int firstDeprivedDay = -1;
    public int lastSatisfiedDay = -1;
    public int lastMoodAppliedDay = -1;
}

public sealed class CharacterPersistentNeedRuntime
{
    private readonly CharacterIdentityStateStore states;
    private readonly IGameClock gameClock;

    public CharacterPersistentNeedRuntime(
        CharacterIdentityStateStore states,
        IGameClock gameClock)
    {
        this.states = states ?? throw new ArgumentNullException(nameof(states));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
    }

    public float ResolveMoodDelta(CharacterActor actor, string eventId)
    {
        if (actor?.Progression == null || string.IsNullOrWhiteSpace(eventId))
            return 0f;
        string normalizedEvent = eventId.Trim();
        string characterId = actor.Identity?.PersistentId?.Trim() ?? string.Empty;
        if (characterId.Length == 0)
            throw new InvalidOperationException(
                "Persistent identity needs require a persistent character id.");
        int absoluteDay = Math.Max(
            0,
            (int)Math.Floor(gameClock.Time / GameCalendarRules.SecondsPerDay));
        float moodDelta = 0f;

        foreach (CharacterTraitSO trait in actor.Progression.ResolveSelectedTraits()
                     .Where(value => value != null)
                     .OrderBy(value => value.id))
        foreach (PersistentNeedRule rule in (trait.identityRules
                     ?? new List<CharacterIdentityRule>())
                     .OfType<PersistentNeedRule>()
                     .OrderBy(value => value.priority)
                     .ThenBy(value => value.ruleId, StringComparer.Ordinal))
        {
            bool satisfied = string.Equals(
                rule.satisfiedEventId,
                normalizedEvent,
                StringComparison.Ordinal);
            bool deprived = string.Equals(
                rule.deprivedEventId,
                normalizedEvent,
                StringComparison.Ordinal);
            if (!satisfied && !deprived)
                continue;

            CharacterPersistentNeedRuntimeState state = Read(
                characterId,
                trait,
                rule);
            if (satisfied)
            {
                state.lastSatisfiedDay = absoluteDay;
                state.firstDeprivedDay = -1;
                state.lastMoodAppliedDay = absoluteDay;
                moodDelta += rule.satisfiedMoodDelta;
            }
            else
            {
                if (state.firstDeprivedDay < 0
                    || state.lastSatisfiedDay >= state.firstDeprivedDay)
                    state.firstDeprivedDay = absoluteDay;
                int deprivedDays = absoluteDay - state.firstDeprivedDay + 1;
                if (deprivedDays >= Math.Max(1, rule.deprivationDays)
                    && state.lastMoodAppliedDay != absoluteDay)
                {
                    state.lastMoodAppliedDay = absoluteDay;
                    moodDelta += rule.deprivedMoodDelta;
                }
            }
            states.Set(
                characterId,
                trait.DefinitionId.Value,
                rule.ruleId,
                1,
                JsonUtility.ToJson(state));
        }
        return moodDelta;
    }

    [GameplayInternalOnly(
        "Authoritative domain outcomes reset persistent-needs state without granting an invented outcome.",
        "ResearchIdentityEventAdapter|WorkIdentityEventAdapter")]
    public void MarkSatisfied(CharacterActor actor, string needId)
    {
        if (actor?.Progression == null || string.IsNullOrWhiteSpace(needId))
            return;
        string normalizedNeed = needId.Trim();
        string characterId = actor.Identity?.PersistentId?.Trim() ?? string.Empty;
        if (characterId.Length == 0)
            throw new InvalidOperationException(
                "Persistent identity needs require a persistent character id.");
        int absoluteDay = Math.Max(
            0,
            (int)Math.Floor(gameClock.Time / GameCalendarRules.SecondsPerDay));
        foreach (CharacterTraitSO trait in actor.Progression.ResolveSelectedTraits()
                     .Where(value => value != null)
                     .OrderBy(value => value.id))
        foreach (PersistentNeedRule rule in (trait.identityRules
                     ?? new List<CharacterIdentityRule>())
                     .OfType<PersistentNeedRule>()
                     .Where(value => string.Equals(
                         value.needId,
                         normalizedNeed,
                         StringComparison.Ordinal)))
        {
            CharacterPersistentNeedRuntimeState state = Read(
                characterId,
                trait,
                rule);
            state.lastSatisfiedDay = absoluteDay;
            state.firstDeprivedDay = -1;
            states.Set(
                characterId,
                trait.DefinitionId.Value,
                rule.ruleId,
                1,
                JsonUtility.ToJson(state));
        }
    }

    private CharacterPersistentNeedRuntimeState Read(
        string characterId,
        CharacterTraitSO trait,
        PersistentNeedRule rule)
    {
        if (states.TryGet(
                characterId,
                trait.DefinitionId.Value,
                rule.ruleId,
                out CharacterIdentityRuleStateSaveData saved)
            && !string.IsNullOrWhiteSpace(saved.statePayload))
            return JsonUtility.FromJson<CharacterPersistentNeedRuntimeState>(
                       saved.statePayload)
                   ?? new CharacterPersistentNeedRuntimeState();
        return new CharacterPersistentNeedRuntimeState();
    }
}

[Serializable]
public sealed class ExtremeCraftInspirationRuntimeState
{
    public string lastProductDefinitionId = string.Empty;
    public int consecutiveEligibleCompletions;
    public float lastCompletionElapsedSeconds = -1f;
}

public sealed class ExtremeCraftInspirationRuntime
{
    public const string RuleId = "extreme:mythic-inspiration";
    private readonly CharacterIdentityStateStore states;
    private readonly CharacterMoodPolicyService moods;

    public ExtremeCraftInspirationRuntime(
        CharacterIdentityStateStore states,
        CharacterMoodPolicyService moods)
    {
        this.states = states ?? throw new ArgumentNullException(nameof(states));
        this.moods = moods ?? throw new ArgumentNullException(nameof(moods));
    }

    internal static bool TryResolveRule(
        CharacterActor maker,
        out ExtremeCraftInspirationRule rule)
    {
        rule = maker?.Progression?.ResolveSelectedTraits()
            .FirstOrDefault(value => value != null
                && value.id == MythicCraftInspirationRules.SourceTraitId)
            ?.identityRules?
            .OfType<ExtremeCraftInspirationRule>()
            .FirstOrDefault(value => string.Equals(
                value.ruleId,
                RuleId,
                StringComparison.Ordinal));
        return rule != null;
    }

    [GameplayInternalOnly(
        "Only an authoritative eligible equipment or apparel completion may advance repetition state.",
        "CombatEquipmentCraftingRuntime|ApparelWorkOrderRuntime")]
    public float RecordEligibleCompletion(
        CharacterActor maker,
        string productDefinitionId,
        bool mythic,
        float elapsedSeconds)
    {
        CharacterTraitSO trait = maker?.Progression?.ResolveSelectedTraits()
            .FirstOrDefault(value => value != null
                && value.id == MythicCraftInspirationRules.SourceTraitId);
        TryResolveRule(maker, out ExtremeCraftInspirationRule rule);
        string characterId = maker?.Identity?.PersistentId?.Trim() ?? string.Empty;
        string definitionId = productDefinitionId?.Trim() ?? string.Empty;
        if (trait == null || rule == null || characterId.Length == 0 || definitionId.Length == 0)
        {
            return 0f;
        }

        string traitDefinitionId = trait.DefinitionId.Value;
        ExtremeCraftInspirationRuntimeState state = new();
        if (states.TryGet(characterId, traitDefinitionId, RuleId, out CharacterIdentityRuleStateSaveData saved)
            && !string.IsNullOrWhiteSpace(saved.statePayload))
        {
            state = JsonUtility.FromJson<ExtremeCraftInspirationRuntimeState>(saved.statePayload)
                ?? new ExtremeCraftInspirationRuntimeState();
        }

        float resetSeconds = Mathf.Max(1, rule.resetAfterHours)
            * (GameCalendarRules.SecondsPerDay / 24f);
        bool reset = !string.Equals(
                state.lastProductDefinitionId,
                definitionId,
                StringComparison.Ordinal)
            || state.lastCompletionElapsedSeconds < 0f
            || elapsedSeconds - state.lastCompletionElapsedSeconds >= resetSeconds;
        state.consecutiveEligibleCompletions = reset
            ? 1
            : Mathf.Max(1, state.consecutiveEligibleCompletions + 1);
        state.lastProductDefinitionId = definitionId;
        state.lastCompletionElapsedSeconds = Mathf.Max(0f, elapsedSeconds);

        float moodDelta = 0f;
        if (mythic)
        {
            state.consecutiveEligibleCompletions = 0;
            state.lastProductDefinitionId = string.Empty;
            moodDelta = moods.Apply(
                maker,
                "mood:mythic-inspiration-success",
                rule.mythicMoodDelta,
                rule.mythicMoodDurationDays,
                "신화적 영감");
        }
        else if (state.consecutiveEligibleCompletions > rule.repetitionFreeCount)
        {
            float penalty = rule.repetitionMoodStep
                * (state.consecutiveEligibleCompletions - rule.repetitionFreeCount);
            moodDelta = moods.Apply(
                maker,
                "mood:repetitive-crafting",
                Mathf.Max(rule.repetitionMoodMinimum, penalty),
                1,
                "반복 제작 권태");
        }

        states.Set(
            characterId,
            traitDefinitionId,
            RuleId,
            revision: 1,
            JsonUtility.ToJson(state));
        return moodDelta;
    }
}

public sealed class CharacterIdentityRuleRouter
{
    public IReadOnlyList<(int TraitId, CharacterIdentityRule Rule)> Resolve(CharacterActor actor) =>
        (actor?.Progression?.ResolveSelectedTraits() ?? Array.Empty<CharacterTraitSO>())
        .Where(trait => trait != null)
        .SelectMany(trait => (trait.identityRules ?? new List<CharacterIdentityRule>())
            .Where(rule => rule != null)
            .Select(rule => (trait.id, rule)))
        .OrderBy(value => value.rule.priority)
        .ThenBy(value => value.id)
        .ThenBy(value => value.rule.ruleId, StringComparer.Ordinal)
        .Select(value => (value.id, value.rule))
        .ToArray();

    public float ResolveMood(CharacterActor actor, string eventId, float baseDelta)
    {
        string id = eventId?.Trim() ?? string.Empty;
        var rules = Resolve(actor);
        if (rules.Any(value => value.Rule is MoodImmunityRule immunity
                && string.Equals(immunity.eventId, id, StringComparison.Ordinal)))
            return 0f;
        float value = baseDelta;
        foreach (MoodTransformRule rule in rules.Select(item => item.Rule).OfType<MoodTransformRule>()
                     .Where(rule => string.Equals(rule.eventId, id, StringComparison.Ordinal)))
            value = value * rule.multiplier + rule.additiveDelta;
        value += rules.Select(item => item.Rule).OfType<EventMoodRule>()
            .Where(rule => string.Equals(rule.eventId, id, StringComparison.Ordinal))
            .Sum(rule => rule.moodDelta);
        return value;
    }

    public int ResolveMoodDurationDays(
        CharacterActor actor,
        string eventId,
        int fallbackDays)
    {
        string id = eventId?.Trim() ?? string.Empty;
        int[] authored = Resolve(actor)
            .Select(item => item.Rule)
            .Select(rule => rule switch
            {
                EventMoodRule mood when string.Equals(
                    mood.eventId,
                    id,
                    StringComparison.Ordinal) => mood.durationDays,
                PersistentNeedRule need when string.Equals(
                        need.satisfiedEventId,
                        id,
                        StringComparison.Ordinal)
                    || string.Equals(
                        need.deprivedEventId,
                        id,
                        StringComparison.Ordinal) => need.moodDurationDays,
                _ => 0
            })
            .Where(days => days > 0)
            .ToArray();
        return authored.Length > 0
            ? authored.Max()
            : Math.Max(0, fallbackDays);
    }

    public IReadOnlyList<PostActionConsequenceRule> ResolvePostAction(
        CharacterActor actor,
        string actionTag,
        CharacterCommandOrigin origin)
    {
        string normalizedTag = actionTag?.Trim() ?? string.Empty;
        return Resolve(actor)
            .Select(item => item.Rule)
            .OfType<PostActionConsequenceRule>()
            .Where(rule => string.Equals(rule.actionTag, normalizedTag, StringComparison.Ordinal))
            .Where(rule => !rule.directOrdersOnly || origin == CharacterCommandOrigin.DirectPlayerOrder)
            .ToArray();
    }
}

public static class CharacterAutonomousWorkPolicy
{
    public static bool IsAllowed(
        CharacterActor actor,
        WorkTypeId workTypeId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null || !workTypeId.IsValid)
            return true;

        string[] actionTags = ResolveActionTags(workTypeId);
        if (actionTags.Length == 0)
            return true;
        AutonomousWorkRestrictionRule rule = (actor.Progression?
                .ResolveSelectedTraits() ?? Array.Empty<CharacterTraitSO>())
            .Where(trait => trait != null)
            .OrderBy(trait => trait.id)
            .SelectMany(trait => trait.identityRules
                ?? new List<CharacterIdentityRule>())
            .OfType<AutonomousWorkRestrictionRule>()
            .Where(candidate => actionTags.Contains(
                candidate.actionTag?.Trim() ?? string.Empty,
                StringComparer.Ordinal))
            .OrderBy(candidate => candidate.priority)
            .ThenBy(candidate => candidate.ruleId, StringComparer.Ordinal)
            .FirstOrDefault(candidate => !IsRequiredConditionSatisfied(
                workTypeId,
                candidate.requiredConditionId));
        if (rule == null)
            return true;
        failureReason = string.IsNullOrWhiteSpace(rule.failureReason)
            ? $"특성 규칙 '{rule.ruleId}'이 자율 작업을 제한합니다."
            : rule.failureReason.Trim();
        return false;
    }

    private static string[] ResolveActionTags(WorkTypeId workTypeId)
    {
        if (workTypeId == BuiltInWorkTypeIds.Guard
            || workTypeId == BuiltInWorkTypeIds.Hunt
            || workTypeId == BuiltInWorkTypeIds.ThreatMitigation
            || workTypeId == BuiltInWorkTypeIds.Rescue)
            return new[] { "work:dangerous" };
        return Array.Empty<string>();
    }

    private static bool IsRequiredConditionSatisfied(
        WorkTypeId workTypeId,
        string conditionId)
    {
        if (string.IsNullOrWhiteSpace(conditionId))
            return false;
        return string.Equals(
                conditionId.Trim(),
                "condition:no-safe-alternative",
                StringComparison.Ordinal)
            && (workTypeId == BuiltInWorkTypeIds.Rescue
                || workTypeId == BuiltInWorkTypeIds.ThreatMitigation);
    }
}

public sealed class CharacterMoodPolicyService
{
    private readonly CharacterIdentityRuleRouter router;
    private readonly CharacterPersistentNeedRuntime persistentNeeds;
    public CharacterMoodPolicyService(
        CharacterIdentityRuleRouter router,
        CharacterPersistentNeedRuntime persistentNeeds)
    {
        this.router = router ?? throw new ArgumentNullException(nameof(router));
        this.persistentNeeds = persistentNeeds
            ?? throw new ArgumentNullException(nameof(persistentNeeds));
    }

    [GameplayInternalOnly(
        "Typed identity adapters submit day-based mood impulses through immunity, transform and need policies.",
        "MealIdentityEventAdapter|WorkIdentityEventAdapter")]
    public float Apply(CharacterActor actor, string eventId, float baseDelta, int durationDays, string label)
    {
        if (actor == null) return 0f;
        float resolved = router.ResolveMood(actor, eventId, baseDelta)
            + persistentNeeds.ResolveMoodDelta(actor, eventId);
        if (!Mathf.Approximately(resolved, 0f))
        {
            float durationSeconds = router.ResolveMoodDurationDays(
                    actor,
                    eventId,
                    durationDays)
                * GameCalendarRules.SecondsPerDay;
            if (resolved < 0f)
            {
                CharacterPerformanceSnapshot duration = actor.Stats.EvaluatePerformance(
                    CharacterPerformanceFormulaIds.NegativeMoodDuration);
                if (!duration.IsApplicable)
                    throw new InvalidOperationException(
                        duration.Failure?.Message
                        ?? "Negative mood duration is unavailable.");
                durationSeconds *= duration.Value;
                CharacterPerformanceExecutionTrace.Record(
                    CharacterPerformanceFormulaIds.NegativeMoodDuration,
                    "CharacterMoodPolicyService.Apply",
                    durationDays * GameCalendarRules.SecondsPerDay,
                    durationSeconds,
                    eventId);
            }
            actor.ApplyResolvedMoodFactor(
                $"identity:{eventId}",
                label?.Trim() ?? eventId,
                resolved,
                Mathf.Max(0f, durationSeconds),
                1);
        }
        return resolved;
    }

    [GameplayInternalOnly(
        "Legacy-duration domain impulses still enter the same identity mood policy.",
        "CharacterActor|CharacterDirectOrderCostPreviewService")]
    public float ApplySeconds(
        CharacterActor actor,
        string eventId,
        float baseDelta,
        float durationSeconds,
        string label,
        int maxStacks = 1)
    {
        if (actor == null) return 0f;
        float resolved = router.ResolveMood(actor, eventId, baseDelta)
            + persistentNeeds.ResolveMoodDelta(actor, eventId);
        if (!Mathf.Approximately(resolved, 0f))
        {
            float resolvedDuration = Mathf.Max(0f, durationSeconds);
            int authoredDays = router.ResolveMoodDurationDays(
                actor,
                eventId,
                0);
            if (authoredDays > 0)
                resolvedDuration = authoredDays * GameCalendarRules.SecondsPerDay;
            if (resolved < 0f)
            {
                CharacterPerformanceSnapshot duration = actor.Stats.EvaluatePerformance(
                    CharacterPerformanceFormulaIds.NegativeMoodDuration);
                if (!duration.IsApplicable)
                    throw new InvalidOperationException(
                        duration.Failure?.Message
                        ?? "Negative mood duration is unavailable.");
                resolvedDuration *= duration.Value;
                CharacterPerformanceExecutionTrace.Record(
                    CharacterPerformanceFormulaIds.NegativeMoodDuration,
                    "CharacterMoodPolicyService.ApplySeconds",
                    durationSeconds,
                    resolvedDuration,
                    eventId);
            }
            actor.ApplyResolvedMoodFactor(
                eventId?.Trim() ?? string.Empty,
                label?.Trim() ?? eventId,
                resolved,
                Mathf.Max(0f, resolvedDuration),
                Mathf.Max(1, maxStacks));
        }
        return resolved;
    }
}

public sealed class CombatIdentityEventAdapter : IStartable, IDisposable
{
    private readonly IGameEventBus events;
    private readonly ICharacterWorldQuery world;
    private readonly CharacterMoodPolicyService moods;
    private IDisposable subscription;

    public CombatIdentityEventAdapter(IGameEventBus events, ICharacterWorldQuery world, CharacterMoodPolicyService moods)
    { this.events = events ?? throw new ArgumentNullException(nameof(events)); this.world = world ?? throw new ArgumentNullException(nameof(world)); this.moods = moods ?? throw new ArgumentNullException(nameof(moods)); }

    [GameplayInternalOnly(
        "The runtime entry-point container starts the registered combat identity adapter.",
        "IStartable|DungeonCharacterRegistration")]
    public void Start() => subscription ??= events.Subscribe<CharacterKilledEvent>(OnKilled);

    [GameplayInternalOnly(
        "The runtime lifetime container disposes the combat identity subscription.",
        "IDisposable|DungeonCharacterRegistration")]
    public void Dispose() { subscription?.Dispose(); subscription = null; }

    private void OnKilled(CharacterKilledEvent gameEvent)
    {
        CharacterActor killer = Find(gameEvent.Killer);
        string guiltEvent = gameEvent.WasInnocent
            ? "mood:innocent-kill-guilt"
            : gameEvent.WasPrisoner
                ? "mood:hostile-execution-guilt"
                : "mood:hostile-kill-guilt";
        float guilt = gameEvent.WasInnocent ? -8f : -3f;
        if (gameEvent.WasPrisoner && killer != null)
        {
            guilt *= killer.GetDetailedStatMultiplier(
                "character:combat-stress",
                new[] { "event:hostile-execution" });
        }
        moods.Apply(killer, guiltEvent, guilt, 3, "살해의 기억");
        foreach (CharacterId witnessId in gameEvent.Witnesses)
            moods.Apply(Find(witnessId), "mood:witnessed-kill", -2f, 2, "살해 목격");
    }

    private CharacterActor Find(CharacterId id) => world.Characters.FirstOrDefault(actor =>
        CharacterPersistentIdentity.TryGet(actor, out CharacterId candidate) && candidate.Equals(id));
}
