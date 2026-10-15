using System;
using System.Linq;
using DungeonStory.Foundation.Facts;

// Owns applied wildlife facts awaiting observation, never ledger payloads or policy.
internal sealed class WildlifeGameplayOutcomeRuntime
{
    private const int MaximumPendingOutcomes = 128;
    private readonly IWildlifeGameplayOutcomeOwner state;
    private readonly IDomainFactPublisher facts;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly IGameCalendar calendar;

    public WildlifeGameplayOutcomeRuntime(IWildlifeGameplayOutcomeOwner state,
        IDomainFactPublisher facts, IDomainFactRecoveryPublisher recovery, IGameCalendar calendar)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
    }

    public int AbsoluteDay => Math.Max(0, calendar.Current.AbsoluteDay);
    public long NextOwnerRevision => Math.Max(1L, (long)state.NextGameplayOutcomeSequence + 1L);

    public string CreateOperationId(WildlifeGameplayOutcomeProfile profile,
        string eventKind, string wildlifeId, long ownerRevision) =>
        $"{(profile == WildlifeGameplayOutcomeProfile.Command ? "wildlife-command" : "wildlife-action")}:{eventKind}:{wildlifeId}:{ownerRevision}";

    public bool TryReserve(WildlifeGameplayOutcomeSaveData draft,
        out DomainFactLease reserved, out string failureReason)
    {
        reserved = default;
        failureReason = string.Empty;
        try { (draft ?? throw new ArgumentNullException(nameof(draft))).Validate(proposed: true); }
        catch (ArgumentException exception) { failureReason = exception.Message; return false; }
        if (draft.ownerRevision != NextOwnerRevision
            || draft.publicationKey.Length != 0
            || state.GameplayOutcomes.Count >= MaximumPendingOutcomes
            || state.GameplayOutcomes.Any(value => value?.operationId == draft.operationId))
        { failureReason = "wildlife-fact-draft-conflict"; return false; }

        bool accepted = draft.profile == WildlifeGameplayOutcomeProfile.Command
            ? facts.TryReserve(new WildlifeCommandFact(draft, proposed: true), out reserved, out failureReason)
            : facts.TryReserve(new WildlifeActionFact(draft, proposed: true), out reserved, out failureReason);
        if (!accepted) return false;
        if (string.IsNullOrWhiteSpace(reserved.PublicationKey))
        {
            facts.Cancel(reserved);
            failureReason = "wildlife-fact-publication-identity-missing";
            return false;
        }
        draft.publicationKey = reserved.PublicationKey;
        return true;
    }

    public void Cancel(in DomainFactLease reserved) => facts.Cancel(reserved);

    public void RecordMutation(WildlifeGameplayOutcomeSaveData completed)
    {
        (completed ?? throw new ArgumentNullException(nameof(completed))).Validate();
        if (completed.ownerRevision != NextOwnerRevision
            || string.IsNullOrWhiteSpace(completed.publicationKey)
            || state.GameplayOutcomes.Count >= MaximumPendingOutcomes)
            throw new InvalidOperationException("wildlife-fact-completed-owner-conflict");
        state.GameplayOutcomes.Add(Clone(completed));
        state.NextGameplayOutcomeSequence = checked((int)completed.ownerRevision);
    }

    public DomainFactDelivery CommitReserved(WildlifeGameplayOutcomeSaveData completed,
        in DomainFactLease reserved, out string failureReason)
    {
        // Read the saved, frozen fact; the caller's mutable working copy is not
        // a second authority after effects have been recorded.
        WildlifeGameplayOutcomeSaveData pending = state.GameplayOutcomes.SingleOrDefault(
            value => value?.operationId == completed?.operationId);
        if (pending == null || pending.publicationKey != reserved.PublicationKey)
            throw new InvalidOperationException("wildlife-fact-pending-reservation-conflict");
        DomainFactDelivery delivery;
        try
        {
            delivery = pending.profile == WildlifeGameplayOutcomeProfile.Command
                ? facts.Publish(reserved, new WildlifeCommandFact(pending))
                : facts.Publish(reserved, new WildlifeActionFact(pending));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException && exception is not StackOverflowException)
        {
            // Publisher contract: escaped errors are pre-commit. The wildlife
            // effects already exist, so retain the fact for later delivery.
            facts.Cancel(reserved);
            delivery = DomainFactDelivery.Reject("wildlife-fact-publication-failed:" + exception.Message);
        }
        failureReason = delivery.Detail;
        if (delivery.Completed) RemovePending(pending.operationId);
        return delivery;
    }

    public void AdvancePending()
    {
        foreach (WildlifeGameplayOutcomeSaveData pending in state.GameplayOutcomes
            .OrderBy(value => value.ownerRevision).ToArray())
        {
            pending.Validate();
            if (string.IsNullOrWhiteSpace(pending.publicationKey))
                throw new InvalidOperationException("wildlife-fact-recovery-identity-missing");
            DomainFactReplay replay = pending.profile == WildlifeGameplayOutcomeProfile.Command
                ? DomainFactReplay.Create(pending.publicationKey, new WildlifeCommandFact(pending))
                : DomainFactReplay.Create(pending.publicationKey, new WildlifeActionFact(pending));
            var results = new DomainFactDelivery[1];
            bool accepted = recovery.ReplayBatch(new[] { replay }, results, out string failure);
            if (results[0].Completed) RemovePending(pending.operationId);
            else if (!accepted || !results[0].MayBeCommitted)
                throw new InvalidOperationException("wildlife-fact-recovery-rejected:" + failure + ":" + results[0].Detail);
        }
    }

    public static WildlifeGameplayOutcomeSaveData Clone(WildlifeGameplayOutcomeSaveData source) => source?.Clone();
    private void RemovePending(string operationId) => state.GameplayOutcomes.RemoveAll(
        value => value != null && value.operationId == operationId);
}
