using System;
using UnityEngine;

public static class CharacterActivityKinds
{
    public const string Work = "activity:work";
    public const string FacilityUse = "activity:facility-use";
    public const string Stock = "activity:stock";
    public const string Command = "activity:command";
    public const string Duty = "activity:duty";
    public const string Health = "activity:health";
    public const string Lifecycle = "activity:lifecycle";
    public const string Combat = "activity:combat";
    public const string Social = "activity:social";
    public const string Shopping = "activity:shopping";
    public const string Wait = "activity:wait";
    public const string AiDecision = "activity:ai-decision";
    public const string FreeText = "activity:free-text";
}

public static class CharacterActivityOutcomes
{
    public const string Started = "outcome:started";
    public const string Progress = "outcome:progress";
    public const string Completed = "outcome:completed";
    public const string Failed = "outcome:failed";
    public const string Blocked = "outcome:blocked";
    public const string Cancelled = "outcome:cancelled";
    public const string Changed = "outcome:changed";
    public const string Responded = "outcome:responded";
    public const string Departed = "outcome:departed";
    public const string Returned = "outcome:returned";
    public const string Damaged = "outcome:damaged";
    public const string Defeated = "outcome:defeated";
    public const string Observed = "outcome:observed";
}

[Serializable]
public sealed class CharacterActivityEvent
{
    [SerializeField] private string kindId;
    [SerializeField] private string actorId;
    [SerializeField] private string actorName;
    [SerializeField] private string actionId;
    [SerializeField] private string targetId;
    [SerializeField] private string targetName;
    [SerializeField] private string placeId;
    [SerializeField] private string placeName;
    [SerializeField] private string outcomeId;
    [SerializeField] private string reasonCode;
    [SerializeField] private string factText;
    [SerializeField] private float value;
    [SerializeField] private int quantity;
    [SerializeField] private int facilityId = -1;
    [SerializeField] private float sentiment;
    [SerializeField] private bool narrativeEligible;
    [SerializeField] private bool bubbleEligible;
    [SerializeField] private bool visibleToPlayer = true;

    private CharacterActivityEvent()
    {
    }

    public string KindId => kindId;
    public string ActorId => actorId;
    public string ActorName => actorName;
    public string ActionId => actionId;
    public string TargetId => targetId;
    public string TargetName => targetName;
    public string PlaceId => placeId;
    public string PlaceName => placeName;
    public string OutcomeId => outcomeId;
    public string ReasonCode => reasonCode;
    public string FactText => factText;
    public float Value => value;
    public int Quantity => quantity;
    public int FacilityId => facilityId;
    public float Sentiment => sentiment;
    public bool NarrativeEligible => narrativeEligible;
    public bool BubbleEligible => bubbleEligible;
    public bool VisibleToPlayer => visibleToPlayer;

    public string AggregationKey => string.Join(
        "|",
        KindId,
        ActionId,
        TargetId,
        PlaceId,
        OutcomeId,
        ReasonCode,
        FacilityId.ToString());

    public static CharacterActivityEvent Create(
        string kindId,
        string outcomeId,
        string factText,
        string actionId = "",
        string targetId = "",
        string targetName = "",
        string placeId = "",
        string placeName = "",
        string reasonCode = "",
        float value = 0f,
        int quantity = 0,
        int facilityId = -1,
        float sentiment = 0f,
        bool narrativeEligible = true,
        bool bubbleEligible = false,
        bool visibleToPlayer = true)
    {
        if (string.IsNullOrWhiteSpace(kindId))
        {
            throw new ArgumentException("Activity kind id is required.", nameof(kindId));
        }

        return new CharacterActivityEvent
        {
            kindId = kindId.Trim(),
            outcomeId = outcomeId?.Trim() ?? string.Empty,
            factText = factText?.Trim() ?? string.Empty,
            actionId = actionId?.Trim() ?? string.Empty,
            targetId = targetId?.Trim() ?? string.Empty,
            targetName = targetName?.Trim() ?? string.Empty,
            placeId = placeId?.Trim() ?? string.Empty,
            placeName = placeName?.Trim() ?? string.Empty,
            reasonCode = reasonCode?.Trim() ?? string.Empty,
            value = value,
            quantity = quantity,
            facilityId = facilityId,
            sentiment = Mathf.Clamp(sentiment, -1f, 1f),
            narrativeEligible = narrativeEligible,
            bubbleEligible = bubbleEligible,
            visibleToPlayer = visibleToPlayer
        };
    }

    public static CharacterActivityEvent Work(
        WorkTypeId workTypeId,
        string outcomeId,
        string factText,
        BuildableObject target = null,
        string reasonCode = "",
        float value = 0f,
        int quantity = 0,
        bool bubbleEligible = false)
    {
        string workId = WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            ? definition.Id
            : workTypeId.IsValid
                ? workTypeId.Value
                : "work:unknown";
        return Create(
            CharacterActivityKinds.Work,
            outcomeId,
            factText,
            actionId: workId,
            targetId: GetTargetId(target),
            targetName: GetTargetName(target),
            reasonCode: reasonCode,
            value: value,
            quantity: quantity,
            facilityId: target != null ? target.id : -1,
            sentiment: GetOutcomeSentiment(outcomeId),
            narrativeEligible: true,
            bubbleEligible: bubbleEligible);
    }

    internal static CharacterActivityEvent Work(
        FacilityWorkType workType,
        string outcomeId,
        string factText,
        BuildableObject target = null,
        string reasonCode = "",
        float value = 0f,
        int quantity = 0,
        bool bubbleEligible = false)
    {
        WorkTypeId workTypeId = WorkTypeCatalog.TryGet(
                workType,
                out WorkTypeDefinition definition)
            ? definition.WorkTypeId
            : new WorkTypeId($"work:{(int)workType}");
        return Work(
            workTypeId,
            outcomeId,
            factText,
            target,
            reasonCode,
            value,
            quantity,
            bubbleEligible);
    }

    public static CharacterActivityEvent Facility(
        string kindId,
        string outcomeId,
        string factText,
        BuildableObject target,
        string actionId = "",
        string reasonCode = "",
        float value = 0f,
        int quantity = 0,
        bool bubbleEligible = false)
    {
        return Create(
            kindId,
            outcomeId,
            factText,
            actionId: actionId,
            targetId: GetTargetId(target),
            targetName: GetTargetName(target),
            reasonCode: reasonCode,
            value: value,
            quantity: quantity,
            facilityId: target != null ? target.id : -1,
            sentiment: GetOutcomeSentiment(outcomeId),
            narrativeEligible: true,
            bubbleEligible: bubbleEligible);
    }

    public static CharacterActivityEvent FreeText(string message, bool narrativeEligible = false)
    {
        return Create(
            CharacterActivityKinds.FreeText,
            CharacterActivityOutcomes.Observed,
            message,
            narrativeEligible: narrativeEligible,
            visibleToPlayer: true);
    }

    public static CharacterActivityEvent InternalAi(string outcomeId, string reasonCode, string factText)
    {
        return Create(
            CharacterActivityKinds.AiDecision,
            outcomeId,
            factText,
            reasonCode: reasonCode,
            narrativeEligible: false,
            bubbleEligible: false,
            visibleToPlayer: false);
    }

    internal CharacterActivityEvent WithActor(CharacterActor actor)
    {
        if (actor == null)
        {
            return this;
        }

        CharacterIdentity identity = actor.Identity;
        string boundActorId = !string.IsNullOrWhiteSpace(identity?.PersistentId)
            ? identity.PersistentId
            : actor.GetInstanceID().ToString();
        string boundActorName = !string.IsNullOrWhiteSpace(identity != null ? identity.DisplayName : null)
            ? identity.DisplayName
            : actor.name;

        if (string.Equals(actorId, boundActorId, StringComparison.Ordinal)
            && string.Equals(actorName, boundActorName, StringComparison.Ordinal))
        {
            return this;
        }

        return new CharacterActivityEvent
        {
            kindId = kindId,
            actorId = boundActorId,
            actorName = boundActorName,
            actionId = actionId,
            targetId = targetId,
            targetName = targetName,
            placeId = placeId,
            placeName = placeName,
            outcomeId = outcomeId,
            reasonCode = reasonCode,
            factText = factText,
            value = value,
            quantity = quantity,
            facilityId = facilityId,
            sentiment = sentiment,
            narrativeEligible = narrativeEligible,
            bubbleEligible = bubbleEligible,
            visibleToPlayer = visibleToPlayer
        };
    }

    private static string GetTargetId(BuildableObject target)
    {
        return target != null ? $"facility:{target.id}" : string.Empty;
    }

    private static string GetTargetName(BuildableObject target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        return target.BuildingData != null && !string.IsNullOrWhiteSpace(target.BuildingData.objectName)
            ? target.BuildingData.objectName
            : target.name;
    }

    private static float GetOutcomeSentiment(string outcomeId)
    {
        if (string.Equals(outcomeId, CharacterActivityOutcomes.Completed, StringComparison.Ordinal)
            || string.Equals(outcomeId, CharacterActivityOutcomes.Returned, StringComparison.Ordinal))
        {
            return 0.45f;
        }

        if (string.Equals(outcomeId, CharacterActivityOutcomes.Failed, StringComparison.Ordinal)
            || string.Equals(outcomeId, CharacterActivityOutcomes.Blocked, StringComparison.Ordinal)
            || string.Equals(outcomeId, CharacterActivityOutcomes.Damaged, StringComparison.Ordinal))
        {
            return -0.7f;
        }

        return 0f;
    }
}

public static class CharacterNarrativeDomainUtility
{
    public static CharacterNarrativeDomain FromActivity(CharacterActivityEvent activity)
    {
        return activity?.KindId switch
        {
            CharacterActivityKinds.Work => CharacterNarrativeDomain.Work,
            CharacterActivityKinds.FacilityUse or CharacterActivityKinds.Stock
                or CharacterActivityKinds.Shopping => CharacterNarrativeDomain.FacilityUse,
            CharacterActivityKinds.Health => string.Equals(
                activity.OutcomeId,
                CharacterActivityOutcomes.Damaged,
                StringComparison.Ordinal)
                    ? CharacterNarrativeDomain.Injury
                    : CharacterNarrativeDomain.Survival,
            CharacterActivityKinds.Combat => CharacterNarrativeDomain.Combat,
            CharacterActivityKinds.Social => CharacterNarrativeDomain.Relationship,
            CharacterActivityKinds.Lifecycle => CharacterNarrativeDomain.Survival,
            _ => CharacterNarrativeDomain.Mood
        };
    }
}

public static class CharacterActivityFormatter
{
    public static string Format(CharacterActivityEvent activity)
    {
        if (activity == null)
        {
            return "기록 없음";
        }

        if (!string.IsNullOrWhiteSpace(activity.FactText))
        {
            return activity.FactText.Trim();
        }

        string action = ResolveActionLabel(activity.ActionId);
        string target = !string.IsNullOrWhiteSpace(activity.TargetName)
            ? $" · {activity.TargetName}"
            : string.Empty;
        string outcome = ResolveOutcomeLabel(activity.OutcomeId);
        return $"{action}{target} · {outcome}";
    }

    private static string ResolveActionLabel(string actionId)
    {
        if (WorkTypeCatalog.TryGet(actionId, out WorkTypeDefinition work))
        {
            return work.DisplayName;
        }

        return string.IsNullOrWhiteSpace(actionId) ? "행동" : actionId;
    }

    private static string ResolveOutcomeLabel(string outcomeId)
    {
        return outcomeId switch
        {
            CharacterActivityOutcomes.Started => "시작",
            CharacterActivityOutcomes.Progress => "진행",
            CharacterActivityOutcomes.Completed => "완료",
            CharacterActivityOutcomes.Failed => "실패",
            CharacterActivityOutcomes.Blocked => "막힘",
            CharacterActivityOutcomes.Cancelled => "중단",
            CharacterActivityOutcomes.Changed => "변경",
            CharacterActivityOutcomes.Departed => "출발",
            CharacterActivityOutcomes.Returned => "복귀",
            CharacterActivityOutcomes.Damaged => "손상",
            _ => "기록"
        };
    }
}
