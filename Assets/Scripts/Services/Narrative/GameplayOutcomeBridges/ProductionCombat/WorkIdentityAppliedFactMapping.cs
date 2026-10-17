using System;
using DungeonStory.Narrative.Korean;

public sealed class WorkIdentityAppliedFactMapping
{
    public const string ObserverId = "log:character.work-identity-applied";

    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;
    private readonly IGameplayOutcomeQuery query;

    public WorkIdentityAppliedFactMapping(
        IGameplayOutcomeDiagnosticsQuery diagnostics,
        IGameplayOutcomeQuery query)
    {
        this.diagnostics = diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
        this.query = query ?? throw new ArgumentNullException(nameof(query));
    }

    public WorkCompletionIdentityOutcomeReceipt Capture(
        WorkIdentityAppliedFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        WorkCompletionIdentityDeliveryRequest request = fact.Request;
        string failure = string.Empty;
        bool valid = fact.RequiresTerminalDelivery
            ? TryValidateTerminalDelivery(request, out failure)
            : request.ChildResultKey
                == WorkCompletionIdentityReferenceDefaults.None
                || TryValidateCommittedChild(request, out failure);
        if (!valid)
        {
            throw new ArgumentException(
                failure,
                nameof(fact));
        }

        return new WorkCompletionIdentityOutcomeReceipt(
            request,
            CharacterName(fact.CharacterDisplayName, request.Character));
    }

    public bool TryValidateCommittedChild(
        WorkCompletionIdentityDeliveryRequest request,
        out string failure)
    {
        GameplayResultKey child;
        try
        {
            child = request.WorkId == BuiltInWorkTypeIds.Repair.Value
                ? RepairWorkCompletionObservation.RequireCompletionChild(
                    request)
                : request.WorkId
                    == BuiltInWorkTypeIds.ThreatMitigation.Value
                    ? EnvironmentalFireCompletionIdentity
                        .RequireCompletionChild(request)
                    : WorkCompletionChildReference.Require(
                        request.OperationId,
                        request.TerminalOwnerId,
                        request.ChildResultKey);
        }
        catch (ArgumentException error)
        {
            failure = "work-identity-child-invalid:" + error.Message;
            return false;
        }

        if (!diagnostics.TryGetResultIdentity(
                child,
                out GameplayOutcomeReplayIdentity identity)
            || identity.State is not (GameplayOutcomeReplayState.Committed
                or GameplayOutcomeReplayState.DeliveryFaultPending
                or GameplayOutcomeReplayState.PublishedAwaitingAcknowledgement
                or GameplayOutcomeReplayState.PublishedAcknowledged
                or GameplayOutcomeReplayState.Compacted
                or GameplayOutcomeReplayState.Forgotten))
        {
            failure = "work-identity-child-not-committed:" + child;
            return false;
        }

        if (request.WorkId == BuiltInWorkTypeIds.Repair.Value
            && query.TryGetExact(
                identity.OutcomeId,
                out GameplayOutcomeSnapshot exact)
            && !RepairWorkCompletionObservation.MatchesCompletionChild(
                request,
                exact))
        {
            failure = "work-identity-repair-child-payload-conflict:"
                + child;
            return false;
        }
        if (request.WorkId
                == BuiltInWorkTypeIds.ThreatMitigation.Value
            && query.TryGetExact(
                identity.OutcomeId,
                out GameplayOutcomeSnapshot fireChild)
            && !EnvironmentalFireCompletionIdentity.MatchesCompletionChild(
                request,
                fireChild))
        {
            failure = "work-identity-fire-child-payload-conflict:"
                + child;
            return false;
        }
        if (request.WorkId == BuiltInWorkTypeIds.AnimalCare.Value)
        {
            string owner = MigratedProducerOutcomeCatalog.Get(
                    MigratedProducerOutcomeKind.AnimalHusbandryWorkResult)
                .ProducerId;
            if (request.TerminalOwnerId != owner
                || child.ProducerId != request.TerminalOwnerId)
            {
                failure = "work-identity-husbandry-child-owner-conflict:"
                    + child;
                return false;
            }
            if (query.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot husbandryChild)
                && !AnimalHusbandryCompletionObservation
                    .MatchesCompletionChild(request, husbandryChild))
            {
                failure =
                    "work-identity-husbandry-child-payload-conflict:"
                    + child;
                return false;
            }
        }

        failure = string.Empty;
        return true;
    }

    public static GameplayDomainFactObserverBinding<
        WorkIdentityAppliedFact,
        WorkCompletionIdentityOutcomeReceipt> CreateBinding(
        IGameplayOutcomeRegistry registry,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeRecorder recorder,
        GameplayDomainFactCommitGroup commits,
        IGameplayOutcomeDiagnosticsQuery diagnostics,
        IGameplayOutcomeQuery query)
    {
        var mapping = new WorkIdentityAppliedFactMapping(
            diagnostics,
            query);
        return new GameplayDomainFactObserverBinding<
            WorkIdentityAppliedFact,
            WorkCompletionIdentityOutcomeReceipt>(
                ObserverId,
                mapping.Capture,
                registry,
                ledger,
                recorder,
                commits);
    }

    private bool TryValidateTerminalDelivery(
        WorkCompletionIdentityDeliveryRequest request,
        out string failure)
    {
        bool surgery = request.WorkId?.StartsWith(
                "surgery:",
                StringComparison.Ordinal) == true
            && request.ChildResultKey
                != WorkCompletionIdentityReferenceDefaults.None;
        if (surgery)
        {
            WorkCompletionIdentityDeliveryRequest canonical =
                WorkCompletionIdentityDeliveryLedger
                    .CreateReferencedRequest(request.ToEvent());
            GameplayResultKey child;
            try
            {
                child = WorkCompletionChildReference.Require(
                    request.OperationId,
                    request.TerminalOwnerId,
                    request.ChildResultKey);
            }
            catch (ArgumentException error)
            {
                failure = "work-identity-child-invalid:" + error.Message;
                return false;
            }
            if (child.ProducerId != "medical.surgery"
                || request.TerminalOwnerId != child.ProducerId
                || request.PayloadFingerprint
                    != canonical.PayloadFingerprint)
            {
                failure =
                    "Surgery completion requires its canonical child/worker delivery.";
                return false;
            }
            return TryValidateCommittedChild(request, out failure);
        }

        if (request.WorkId == BuiltInWorkTypeIds.AnimalCare.Value)
        {
            WorkCompletionIdentityDeliveryRequest canonical =
                WorkCompletionIdentityDeliveryLedger
                    .CreateReferencedRequest(request.ToEvent());
            if (request.PayloadFingerprint != canonical.PayloadFingerprint)
            {
                failure =
                    "Husbandry completion requires its canonical child/worker delivery.";
                return false;
            }
            return TryValidateCommittedChild(request, out failure);
        }

        if (request.WorkId == BuiltInWorkTypeIds.Repair.Value
            || request.WorkId
                == BuiltInWorkTypeIds.ThreatMitigation.Value)
        {
            return TryValidateCommittedChild(request, out failure);
        }

        if (request.Origin != CharacterCommandOrigin.Autonomous
            || !string.Equals(
                request.WorkId,
                BuiltInWorkTypeIds.Harvest.Value,
                StringComparison.Ordinal))
        {
            failure =
                "Durable work completion supports autonomous harvest and referenced surgery/repair/fire/husbandry only.";
            return false;
        }

        if (!TryValidateCommittedChild(request, out failure))
            return false;
        GameplayResultKey harvest = WorkCompletionChildReference.Require(
            request.OperationId,
            request.TerminalOwnerId,
            request.ChildResultKey);
        if (harvest.ProducerId != EnvironmentOutcomeIds.CropPlanProducer
            || request.TerminalOwnerId != harvest.ProducerId)
        {
            failure =
                "Harvest completion requires its committed crop terminal child.";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private static KoreanNameSnapshot CharacterName(
        string displayName,
        CharacterId character)
    {
        string stableId = character.Value;
        return new KoreanNameSnapshot(
            displayName,
            "runtime-character-current-v1:" + stableId,
            KoreanPronunciationHint.AutoHangulDisplay(
                "runtime-character-pronunciation-v1:" + stableId),
            "ko-KR");
    }
}
