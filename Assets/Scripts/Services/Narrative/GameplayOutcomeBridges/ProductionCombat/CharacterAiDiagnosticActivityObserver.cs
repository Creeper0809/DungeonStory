using System;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class CharacterAiDiagnosticActivityObserver :
    IInitializable,
    IDisposable
{
    private readonly IGameEventBus events;
    private IDisposable diagnosticSubscription;
    private IDisposable distantFacilitySubscription;

    public CharacterAiDiagnosticActivityObserver(IGameEventBus events) =>
        this.events = events ?? throw new ArgumentNullException(nameof(events));

    public void Initialize()
    {
        if (diagnosticSubscription != null)
        {
            return;
        }

        diagnosticSubscription =
            events.Subscribe<CharacterAiDiagnosticEvent>(Observe);
        try
        {
            distantFacilitySubscription =
                events.Subscribe<CharacterAiDistantFacilitySelectionEvent>(
                    Observe);
        }
        catch
        {
            diagnosticSubscription.Dispose();
            diagnosticSubscription = null;
            throw;
        }
    }

    public void Dispose()
    {
        distantFacilitySubscription?.Dispose();
        distantFacilitySubscription = null;
        diagnosticSubscription?.Dispose();
        diagnosticSubscription = null;
    }

    public static CharacterActivityEvent Project(
        in CharacterAiDiagnosticEvent value)
    {
        return value.Transition switch
        {
            CharacterAiDiagnosticTransition.RunningActionReplanDeferred =>
                CharacterActivityEvent.InternalAi(
                    CharacterActivityOutcomes.Changed,
                    value.ReasonCode,
                    "Destructive AI replan deferred while an action owns its lifecycle: "
                    + value.Detail),
            CharacterAiDiagnosticTransition.DuplicateExecutionSuppressed =>
                CharacterActivityEvent.InternalAi(
                    CharacterActivityOutcomes.Changed,
                    value.ReasonCode,
                    $"AI duplicate execution suppressed: action={value.Detail}; "
                    + $"count={value.Count}"),
            CharacterAiDiagnosticTransition.OrphanWorkActionRecovered =>
                CharacterActivityEvent.InternalAi(
                    CharacterActivityOutcomes.Failed,
                    value.ReasonCode,
                    $"AI work lifecycle recovered: {value.Detail}"),
            CharacterAiDiagnosticTransition.InteractionActionReplaced =>
                CharacterActivityEvent.InternalAi(
                    CharacterActivityOutcomes.Failed,
                    value.ReasonCode,
                    "AI interaction ownership was replaced: " + value.Detail),
            CharacterAiDiagnosticTransition.Replan =>
                CharacterActivityEvent.InternalAi(
                    CharacterActivityOutcomes.Changed,
                    value.ReasonCode,
                    $"AI replan: {value.Detail}"),
            CharacterAiDiagnosticTransition.ActionFailed =>
                CharacterActivityEvent.InternalAi(
                    CharacterActivityOutcomes.Failed,
                    value.ReasonCode,
                    $"AI \uC2E4\uD328: {value.Detail}"),
            CharacterAiDiagnosticTransition.NoAction =>
                CharacterActivityEvent.InternalAi(
                    CharacterActivityOutcomes.Blocked,
                    value.ReasonCode,
                    $"AI \uB300\uAE30: {value.Detail}"),
            CharacterAiDiagnosticTransition.ExecutionFailed =>
                CharacterActivityEvent.InternalAi(
                    CharacterActivityOutcomes.Failed,
                    value.ReasonCode,
                    $"AI execution failed: {value.Detail}"),
            CharacterAiDiagnosticTransition.IdleSelected =>
                CharacterActivityEvent.InternalAi(
                    CharacterActivityOutcomes.Started,
                    value.ReasonCode,
                    $"AI idle: {value.Detail}"),
            CharacterAiDiagnosticTransition.IdleFailed =>
                CharacterActivityEvent.InternalAi(
                    CharacterActivityOutcomes.Failed,
                    value.ReasonCode,
                    $"AI idle failed: {value.Detail}"),
            CharacterAiDiagnosticTransition.IdleMovementUnavailable =>
                CharacterActivityEvent.Create(
                    CharacterActivityKinds.Wait,
                    CharacterActivityOutcomes.Blocked,
                    $"\uB300\uAE30 \uC774\uB3D9 \uBD88\uAC00: {value.Detail}",
                    actionId: "wait:idle-behavior",
                    reasonCode: value.ReasonCode,
                    sentiment: -0.35f,
                    bubbleEligible: true),
            CharacterAiDiagnosticTransition.IdleBehaviorFailed =>
                CharacterActivityEvent.Create(
                    CharacterActivityKinds.Wait,
                    CharacterActivityOutcomes.Failed,
                    $"\uB300\uAE30 \uC2E4\uD328: {value.Detail}",
                    actionId: "wait:idle-behavior",
                    reasonCode: value.ReasonCode,
                    sentiment: -0.5f,
                    bubbleEligible: true),
            CharacterAiDiagnosticTransition.PrimitiveStaleExecutionBlocked =>
                CharacterActivityEvent.InternalAi(
                    CharacterActivityOutcomes.Cancelled,
                    value.ReasonCode,
                    "Primitive fallback cancelled at execution because authored "
                    + $"service became available: action={value.Detail}."),
            CharacterAiDiagnosticTransition.VandalismTargetMissing =>
                CharacterActivityEvent.InternalAi(
                    CharacterActivityOutcomes.Failed,
                    value.ReasonCode,
                    $"AI macro vandalize failed: target not found - {value.Detail}"),
            CharacterAiDiagnosticTransition.VandalismTargetRejected =>
                CharacterActivityEvent.InternalAi(
                    CharacterActivityOutcomes.Failed,
                    value.ReasonCode,
                    $"AI macro vandalize failed: {value.Detail}"),
            CharacterAiDiagnosticTransition.VandalismTransactionFailed =>
                CharacterActivityEvent.InternalAi(
                    CharacterActivityOutcomes.Failed,
                    value.ReasonCode,
                    $"AI macro vandalize failed: {value.Detail}"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value.Transition,
                "Unknown character AI diagnostic transition.")
        };
    }

    public static CharacterActivityEvent Project(
        in CharacterAiDistantFacilitySelectionEvent value)
    {
        string selectedWhy = FormatEvidence(value.SelectedEvidence);
        string nearestWhy = FormatEvidence(value.NearestEvidence);
        Vector2Int selectedPosition =
            new Vector2Int(value.SelectedX, value.SelectedY);
        Vector2Int nearestPosition =
            new Vector2Int(value.NearestX, value.NearestY);
        return CharacterActivityEvent.InternalAi(
            CharacterActivityOutcomes.Observed,
            "facility-distant-selection",
            $"role={value.Role}; selected={value.SelectedName}@{selectedPosition}"
            + $":score={value.SelectedScore:0.###}:travel={value.SelectedTravel:0.##}"
            + $"; nearest={value.NearestName}@{nearestPosition}"
            + $":score={value.NearestScore:0.###}:travel={value.NearestTravel:0.##}"
            + $"; extraTravel={value.ExtraTravel:0.##}; selectedWhy={selectedWhy}; nearestWhy={nearestWhy}");
    }

    private static string FormatEvidence(
        CharacterAiUtilityBreakdownEvidence evidence)
    {
        if (!evidence.HasBreakdown)
        {
            return $"score={evidence.FallbackScore:0.###}";
        }

        CharacterAiUtilityBreakdown breakdown = new CharacterAiUtilityBreakdown(
            evidence.Intention,
            evidence.CandidateLabel,
            captureDetails: true);
        CharacterAiUtilityFactor[] factors = evidence.SnapshotFactors();
        for (int index = 0; index < factors.Length; index++)
        {
            CharacterAiUtilityFactor factor = factors[index];
            breakdown.Add(
                factor.Kind,
                factor.Score,
                factor.Weight,
                factor.Reason);
        }

        if (!string.IsNullOrWhiteSpace(evidence.RejectionReason))
        {
            breakdown.Reject(evidence.RejectionReason);
        }
        else
        {
            breakdown.SetFinalScore(evidence.FinalScore);
        }

        return breakdown.ToCompactString(6);
    }

    private static void Observe(CharacterAiDiagnosticEvent value) =>
        value.Actor.AddActivity(Project(value));

    private static void Observe(CharacterAiDistantFacilitySelectionEvent value) =>
        value.Actor.AddActivity(Project(value));
}
