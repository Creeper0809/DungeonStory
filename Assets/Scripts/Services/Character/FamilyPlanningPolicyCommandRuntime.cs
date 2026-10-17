using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using VContainer.Unity;

public interface IFamilyPlanningPolicyCommand
{
    bool TrySet(
        FamilyPlanningPolicy policy,
        string source,
        out DomainFailure failure);
}

/// <summary>
/// The only player/AI command boundary for the global family-planning policy.
/// The reproduction aggregate owns the value, revision, and exact pending fact.
/// Observation capacity is reserved before mutation; uncertain publication is
/// retained for exact replay rather than reported as a completed command.
/// </summary>
public sealed class FamilyPlanningPolicyCommandRuntime :
    IFamilyPlanningPolicyCommand,
    IStartable,
    ITickable
{
    private readonly IReproductionService reproduction;
    private readonly IReproductionPersistence persistence;
    private readonly IDomainFactPublisher domainFacts;
    private readonly IGameSessionStateProvider sessionStateProvider;
    private bool pendingFailureLogged;

    public FamilyPlanningPolicyCommandRuntime(
        IReproductionService reproduction,
        IReproductionPersistence persistence,
        IDomainFactPublisher domainFacts,
        IGameSessionStateProvider sessionStateProvider)
    {
        this.reproduction = reproduction
            ?? throw new ArgumentNullException(nameof(reproduction));
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.domainFacts = domainFacts
            ?? throw new ArgumentNullException(nameof(domainFacts));
        this.sessionStateProvider = sessionStateProvider
            ?? throw new ArgumentNullException(nameof(sessionStateProvider));
    }

    public bool TrySet(
        FamilyPlanningPolicy policy,
        string source,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        string normalizedSource = source?.Trim() ?? string.Empty;
        if (!Enum.IsDefined(typeof(FamilyPlanningPolicy), policy)
            || !ReproductionStableIdSyntax.IsValid(normalizedSource))
        {
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                "family-planning-policy-input-invalid");
            return false;
        }
        if (!TryResumePending(out failure))
            return false;
        if (reproduction.FamilyPlanningPolicy == policy)
            return true;
        if (!TryGetCurrentAbsoluteDay(out int absoluteDay))
        {
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                "family-planning-policy-session-day-unavailable");
            return false;
        }

        ReproductionWorldSaveData beforeState = persistence.Capture();
        ReproductionWorldAggregate candidate;
        FamilyPlanningPolicyChangedFact fact;
        try
        {
            candidate = persistence.PrepareRestore(beforeState);
            FamilyPlanningPolicy before = candidate.FamilyPlanningPolicy;
            long beforeRevision = candidate.FamilyPlanningPolicyRevision;
            long afterRevision = checked(beforeRevision + 1L);
            fact = new FamilyPlanningPolicyChangedFact(
                before,
                policy,
                beforeRevision,
                afterRevision,
                normalizedSource,
                absoluteDay);
            candidate.StageFamilyPlanningPolicyChange(fact.Snapshot());
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                "family-planning-policy-candidate-invalid",
                exception.GetType().Name);
            return false;
        }

        if (!domainFacts.TryReserve(
                fact,
                out DomainFactLease lease,
                out string prepareFailure))
        {
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                "family-planning-policy-outcome-prepare-rejected",
                prepareFailure);
            return false;
        }

        bool published = false;
        bool publicationStarted = false;
        try
        {
            persistence.PublishRestore(candidate);
            published = true;
            publicationStarted = true;
            DomainFactDelivery delivery = domainFacts.Publish(lease, fact);
            if (!delivery.MayBeCommitted)
            {
                domainFacts.Cancel(lease);
                persistence.PublishRestore(persistence.PrepareRestore(beforeState));
                failure = new DomainFailure(
                    FailureCode.ExternalInfluenceUnavailable,
                    "family-planning-policy-outcome-commit-rejected",
                    delivery.Detail);
                return false;
            }
            if (!delivery.Completed)
            {
                failure = new DomainFailure(
                    FailureCode.ExternalInfluenceUnavailable,
                    "family-planning-policy-outcome-pending",
                    "authoritative-policy-applied-pending-fact:"
                        + delivery.Detail);
                return false;
            }
            return TryAcknowledgePending(fact.AfterRevision, out failure);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            if (!publicationStarted)
            {
                domainFacts.Cancel(lease);
                if (published)
                    persistence.PublishRestore(
                        persistence.PrepareRestore(beforeState));
            }
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                publicationStarted
                    ? "family-planning-policy-publication-in-doubt"
                    : "family-planning-policy-transaction-failed",
                publicationStarted
                    ? "authoritative-policy-applied-pending-fact:"
                        + exception.GetType().Name
                    : exception.GetType().Name);
            return false;
        }
    }

    public bool TryResumePending(out DomainFailure failure)
    {
        if (!reproduction.HasPendingFamilyPlanningPolicyChange)
        {
            failure = DomainFailure.None;
            return true;
        }
        return TryResolvePending(out failure);
    }

    public void Start()
    {
        ResumePendingForLifecycle();
    }

    public void Tick()
    {
        if (!reproduction.HasPendingFamilyPlanningPolicyChange)
        {
            pendingFailureLogged = false;
            return;
        }
        ResumePendingForLifecycle();
    }

    private void ResumePendingForLifecycle()
    {
        if (TryResumePending(out DomainFailure failure))
        {
            pendingFailureLogged = false;
            return;
        }
        if (pendingFailureLogged)
            return;
        pendingFailureLogged = true;
        UnityEngine.Debug.LogError(
            "Family-planning policy recovery remains pending: "
            + failure.Code + " "
            + string.Join(",", failure.Parameters.ToArray()));
    }

    private bool TryResolvePending(out DomainFailure failure)
    {
        failure = DomainFailure.None;
        try
        {
            ReproductionWorldSaveData current = persistence.Capture();
            FamilyPlanningPolicyPendingChangeSaveData pending =
                current.pendingFamilyPlanningPolicyChanges?.SingleOrDefault();
            if (pending == null)
                return true;
            FamilyPlanningPolicyChangedFact fact =
                FamilyPlanningPolicyChangedFact.Restore(pending);
            DomainFactDelivery delivery = domainFacts.Replay(fact);
            if (!delivery.Completed)
            {
                failure = new DomainFailure(
                    FailureCode.ExternalInfluenceUnavailable,
                    "family-planning-policy-pending-unresolved",
                    delivery.Detail);
                return false;
            }
            return TryAcknowledgePending(fact.AfterRevision, out failure);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                "family-planning-policy-pending-replay-failed",
                exception.GetType().Name);
            return false;
        }
    }

    private bool TryAcknowledgePending(
        long afterRevision,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        try
        {
            ReproductionWorldAggregate acknowledged =
                persistence.PrepareRestore(persistence.Capture());
            acknowledged.AcknowledgeFamilyPlanningPolicyChange(afterRevision);
            persistence.PublishRestore(acknowledged);
            return true;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                "family-planning-policy-pending-acknowledge-failed",
                exception.GetType().Name);
            return false;
        }
    }

    private bool TryGetCurrentAbsoluteDay(out int absoluteDay)
    {
        absoluteDay = 0;
        if (!sessionStateProvider.TryGetSessionState(
                out GameSessionState state)
            || state?.day == null
            || state.day.Value < 0)
        {
            return false;
        }
        absoluteDay = state.day.Value;
        return true;
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is ArgumentException
            or InvalidOperationException
            or OverflowException;
}
