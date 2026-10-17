using System;

public enum CharacterAiDiagnosticTransition
{
    RunningActionReplanDeferred = 1,
    DuplicateExecutionSuppressed = 2,
    OrphanWorkActionRecovered = 3,
    InteractionActionReplaced = 4,
    Replan = 5,
    ActionFailed = 6,
    NoAction = 7,
    ExecutionFailed = 8,
    IdleSelected = 9,
    IdleFailed = 10,
    IdleMovementUnavailable = 11,
    IdleBehaviorFailed = 12,
    PrimitiveStaleExecutionBlocked = 13,
    VandalismTargetMissing = 14,
    VandalismTargetRejected = 15,
    VandalismTransactionFailed = 16
}

/// <summary>
/// Ephemeral factual evidence for an AI diagnostic. Presentation policy remains
/// in the narrative observer and this event never owns AI decisions or state.
/// </summary>
public readonly struct CharacterAiDiagnosticEvent
{
    public CharacterAiDiagnosticEvent(
        CharacterActor actor,
        CharacterAiDiagnosticTransition transition,
        string reasonCode,
        string detail,
        long count = 0L)
    {
        if (!Enum.IsDefined(typeof(CharacterAiDiagnosticTransition), transition))
            throw new ArgumentOutOfRangeException(nameof(transition));
        if (string.IsNullOrWhiteSpace(reasonCode))
            throw new ArgumentException(
                "A diagnostic reason code is required.",
                nameof(reasonCode));
        if (count < 0L)
            throw new ArgumentOutOfRangeException(nameof(count));

        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Transition = transition;
        ReasonCode = reasonCode;
        Detail = detail ?? string.Empty;
        Count = count;
    }

    public CharacterActor Actor { get; }
    public CharacterAiDiagnosticTransition Transition { get; }
    public string ReasonCode { get; }
    public string Detail { get; }
    public long Count { get; }
}

/// <summary>
/// Detached decision evidence used to explain why a facility candidate won.
/// It intentionally stores no rendered diagnostic text.
/// </summary>
public readonly struct CharacterAiUtilityBreakdownEvidence
{
    private readonly CharacterAiUtilityFactor[] factors;

    private CharacterAiUtilityBreakdownEvidence(
        bool hasBreakdown,
        CharacterAiIntentionType intention,
        string candidateLabel,
        float finalScore,
        string rejectionReason,
        CharacterAiUtilityFactor[] factors,
        float fallbackScore)
    {
        HasBreakdown = hasBreakdown;
        Intention = intention;
        CandidateLabel = candidateLabel ?? string.Empty;
        FinalScore = finalScore;
        RejectionReason = rejectionReason ?? string.Empty;
        this.factors = factors ?? Array.Empty<CharacterAiUtilityFactor>();
        FallbackScore = fallbackScore;
    }

    public bool HasBreakdown { get; }
    public CharacterAiIntentionType Intention { get; }
    public string CandidateLabel { get; }
    public float FinalScore { get; }
    public string RejectionReason { get; }
    public float FallbackScore { get; }

    public CharacterAiUtilityFactor[] SnapshotFactors() =>
        factors == null || factors.Length == 0
            ? Array.Empty<CharacterAiUtilityFactor>()
            : (CharacterAiUtilityFactor[])factors.Clone();

    public static CharacterAiUtilityBreakdownEvidence Capture(
        CharacterAiUtilityBreakdown value,
        float fallbackScore)
    {
        if (value == null)
        {
            return new CharacterAiUtilityBreakdownEvidence(
                false,
                default,
                string.Empty,
                0f,
                string.Empty,
                Array.Empty<CharacterAiUtilityFactor>(),
                fallbackScore);
        }

        CharacterAiUtilityFactor[] snapshot =
            new CharacterAiUtilityFactor[value.Factors.Count];
        for (int index = 0; index < snapshot.Length; index++)
        {
            snapshot[index] = value.Factors[index];
        }

        return new CharacterAiUtilityBreakdownEvidence(
            true,
            value.Intention,
            value.CandidateLabel,
            value.FinalScore01,
            value.RejectionReason,
            snapshot,
            fallbackScore);
    }
}

/// <summary>
/// Ephemeral raw evidence for a non-nearest facility choice. Rendering and
/// activity policy remain in the narrative observer.
/// </summary>
public readonly struct CharacterAiDistantFacilitySelectionEvent
{
    public CharacterAiDistantFacilitySelectionEvent(
        CharacterActor actor,
        FacilityRole role,
        string selectedName,
        int selectedX,
        int selectedY,
        float selectedScore,
        float selectedTravel,
        CharacterAiUtilityBreakdownEvidence selectedEvidence,
        string nearestName,
        int nearestX,
        int nearestY,
        float nearestScore,
        float nearestTravel,
        CharacterAiUtilityBreakdownEvidence nearestEvidence,
        float extraTravel)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Role = role;
        SelectedName = selectedName ?? string.Empty;
        SelectedX = selectedX;
        SelectedY = selectedY;
        SelectedScore = selectedScore;
        SelectedTravel = selectedTravel;
        SelectedEvidence = selectedEvidence;
        NearestName = nearestName ?? string.Empty;
        NearestX = nearestX;
        NearestY = nearestY;
        NearestScore = nearestScore;
        NearestTravel = nearestTravel;
        NearestEvidence = nearestEvidence;
        ExtraTravel = extraTravel;
    }

    public CharacterActor Actor { get; }
    public FacilityRole Role { get; }
    public string SelectedName { get; }
    public int SelectedX { get; }
    public int SelectedY { get; }
    public float SelectedScore { get; }
    public float SelectedTravel { get; }
    public CharacterAiUtilityBreakdownEvidence SelectedEvidence { get; }
    public string NearestName { get; }
    public int NearestX { get; }
    public int NearestY { get; }
    public float NearestScore { get; }
    public float NearestTravel { get; }
    public CharacterAiUtilityBreakdownEvidence NearestEvidence { get; }
    public float ExtraTravel { get; }
}
