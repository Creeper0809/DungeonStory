using System;

namespace DungeonStory.AI
{
    public enum AiActionCommandKind
    {
        None,
        BeginBreakdown,
        BeginShopping,
        ExitDungeon,
        LookAround,
        Wait,
        RunSelectedAction,
        StopCurrentAction,
        ClearMacroGoal
    }

    public readonly struct AiCharacterDecisionSnapshot
    {
        public AiCharacterDecisionSnapshot(
            CharacterId characterId,
            bool exists,
            bool hasShopping = false,
            bool hasWorkRole = false,
            bool isOffDuty = false,
            bool shouldUseRestProtection = false,
            bool canLookAround = false,
            bool shouldExitDungeon = false,
            int visitCount = 0,
            float hungerUtility = 0f,
            float sleepUtility = 0f,
            float expeditionRecoveryNeed = 0f,
            float facilityNeed = 0f,
            float workUtility = 0f,
            bool hasCandidate = false,
            bool hasOffDutyVisitCandidate = false,
            bool hasDeprivationBreakdown = false)
        {
            CharacterId = characterId;
            Exists = exists;
            HasShopping = hasShopping;
            HasWorkRole = hasWorkRole;
            IsOffDuty = isOffDuty;
            ShouldUseRestProtection = shouldUseRestProtection;
            CanLookAround = canLookAround;
            ShouldExitDungeon = shouldExitDungeon;
            VisitCount = visitCount;
            HungerUtility = hungerUtility;
            SleepUtility = sleepUtility;
            ExpeditionRecoveryNeed = expeditionRecoveryNeed;
            FacilityNeed = facilityNeed;
            WorkUtility = workUtility;
            HasCandidate = hasCandidate;
            HasOffDutyVisitCandidate = hasOffDutyVisitCandidate;
            HasDeprivationBreakdown = hasDeprivationBreakdown;
        }

        public CharacterId CharacterId { get; }
        public bool Exists { get; }
        public bool HasShopping { get; }
        public bool HasWorkRole { get; }
        public bool IsOffDuty { get; }
        public bool ShouldUseRestProtection { get; }
        public bool CanLookAround { get; }
        public bool ShouldExitDungeon { get; }
        public int VisitCount { get; }
        public float HungerUtility { get; }
        public float SleepUtility { get; }
        public float ExpeditionRecoveryNeed { get; }
        public float FacilityNeed { get; }
        public float WorkUtility { get; }
        public bool HasCandidate { get; }
        public bool HasOffDutyVisitCandidate { get; }
        public bool HasDeprivationBreakdown { get; }
        public bool HasPersistentIdentity => CharacterId.IsValid;
    }

    public readonly struct AiActionRequest
    {
        public AiActionRequest(
            CharacterId characterId,
            AiActionCommandKind command,
            BuildingInstanceId destinationId = default,
            float duration = 0f,
            int argument = 0)
        {
            CharacterId = characterId;
            Command = command;
            DestinationId = destinationId;
            Duration = duration;
            Argument = argument;
        }

        public CharacterId CharacterId { get; }
        public AiActionCommandKind Command { get; }
        public BuildingInstanceId DestinationId { get; }
        public float Duration { get; }
        public int Argument { get; }
        public bool IsValid => CharacterId.IsValid && Command != AiActionCommandKind.None;
    }

    public readonly struct AiActionDecision
    {
        public AiActionDecision(
            bool allowed,
            AIActionFailureKind failureKind = AIActionFailureKind.None,
            string detail = "",
            AiActionRequest request = default)
        {
            Allowed = allowed;
            FailureKind = failureKind;
            Detail = detail ?? string.Empty;
            Request = request;
        }

        public bool Allowed { get; }
        public AIActionFailureKind FailureKind { get; }
        public string Detail { get; }
        public AiActionRequest Request { get; }

        public static AiActionDecision Allow(AiActionRequest request = default) =>
            new(true, request: request);

        public static AiActionDecision Reject(
            AIActionFailureKind failureKind,
            string detail = "") =>
            new(false, failureKind, detail);
    }

    public static class AiDecisionMath
    {
        public static float Clamp01(float value) =>
            value < 0f ? 0f : value > 1f ? 1f : value;

        public static float ScoreAtLeast(float minimum, float value) =>
            Clamp01(Math.Max(minimum, value));

        public static float ScoreAtMost(float maximum, float value) =>
            Clamp01(Math.Min(maximum, value));

        public static AiActionDecision ResolveDestination(
            bool selected,
            bool pending,
            bool supported = true)
        {
            if (!supported)
            {
                return AiActionDecision.Reject(
                    AIActionFailureKind.Unsupported,
                    "쇼핑 능력 없음");
            }

            if (selected) return AiActionDecision.Allow();
            return AiActionDecision.Reject(
                pending
                    ? AIActionFailureKind.PathSearchDeferred
                    : AIActionFailureKind.NoDestination,
                pending ? "시설 후보를 나누어 확인하는 중" : string.Empty);
        }
    }
}
