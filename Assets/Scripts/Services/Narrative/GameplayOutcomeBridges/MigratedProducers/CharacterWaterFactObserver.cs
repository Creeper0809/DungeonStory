using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

// The domain publishes one drink. Only this observer decides whether that fact
// also produces the existing, atomically committed foul-water consequence.
public sealed class CharacterWaterFactObserver : IDurableDomainFactObserver<CharacterWaterConsumedFact>,
    IDurableDomainFactRecoveryObserver<CharacterWaterConsumedFact>, IDomainFactCommitGroup
{
    private readonly MigratedProducerOutcomeRuntime outcomes;
    private readonly GameplayDomainFactCommitGroup commits;
    public CharacterWaterFactObserver(MigratedProducerOutcomeRuntime outcomes, GameplayDomainFactCommitGroup commits)
    {
        this.outcomes = outcomes ?? throw new ArgumentNullException(nameof(outcomes));
        this.commits = commits ?? throw new ArgumentNullException(nameof(commits));
    }
    public bool TryReserve(in CharacterWaterConsumedFact proposal, out object reservation, out string failure)
    {
        reservation = null;
        if (proposal == null || !proposal.IsProposal)
        { failure = "water-observer-proposal-required"; return false; }
        var data = proposal.Data;
        if (!outcomes.TryReserve(MigratedProducerOutcomeKind.CharacterWaterConsumedEvent,
                "water-consumed:" + data.characterId, data.absoluteDay, GameplayOutcomeStatus.Succeeded,
                2, 11, 2, 3, out var drink, out failure)) return false;
        PreparedMigratedProducerOutcome consequence = default;
        try
        {
            if (data.quality != WorldWaterQuality.Clean && !outcomes.TryReserve(
                    MigratedProducerOutcomeKind.CharacterFoulWaterConsequenceResult,
                    "foul-water-consequence:" + data.characterId, data.absoluteDay, GameplayOutcomeStatus.Succeeded,
                    1, 8, 1, 2, out consequence, out failure))
            { outcomes.Cancel(drink); return false; }
            reservation = new Reservation(proposal, drink, consequence); return true;
        }
        catch { outcomes.Cancel(drink); if (consequence.IsValid) outcomes.Cancel(consequence); throw; }
    }
    public bool TryPrepare(in CharacterWaterConsumedFact actual, object reservation,
        out IDomainFactPreparedPublication prepared, out string failure)
    {
        prepared = null;
        if (reservation is not Reservation state || state.Used || actual == null
            || actual.IsProposal || !state.Proposal.MatchesHeader(actual))
        { failure = "water-observer-reserved-source-changed"; return false; }
        var captured = CharacterWaterFactObservation.CaptureWater(actual);
        if (!outcomes.TryPrepareObserved(state.Drink, captured.Details, commits, out var drink, out failure)) return false;
        GameplayDomainFactPublication consequence = null;
        try
        {
            if (state.Consequence.IsValid)
            {
                var foul = CharacterWaterFactObservation.CaptureConsequence(actual, state.Drink.ResultKey.OperationId.Value);
                if (!outcomes.TryPrepareObserved(state.Consequence, foul.Details, commits, out consequence, out failure))
                { drink.Cancel(); return false; }
            }
            state.Used = true;
            prepared = state.Publication = new Publication(this, drink, consequence);
            return true;
        }
        catch { drink.Cancel(); consequence?.Cancel(); throw; }
    }
    public bool TryPrepareReplay(string publicationKey, in CharacterWaterConsumedFact actual,
        out IDomainFactPreparedPublication prepared, out string failure)
    {
        prepared = null;
        if (actual == null || actual.IsProposal)
        { failure = "water-observer-actual-required"; return false; }
        string firstKey = publicationKey;
        string secondKey = null;
        bool foul = actual.Data.quality != WorldWaterQuality.Clean;
        bool pair = GameplayDomainFactPairProof.TryReadKeys(publicationKey, out var first, out var second);
        if (foul != pair)
        { failure = "water-observer-recovery-cohort-shape"; return false; }
        if (pair) { firstKey = first; secondKey = second; }
        if (!GameplayDomainFactDeliveryQuery.TryReadKey(firstKey, out var drinkKey))
        { failure = "water-observer-recovery-reference"; return false; }
        if (!outcomes.TryPrepareObservedRecovery(CharacterWaterFactObservation.CaptureWater(actual),
                firstKey, commits, out var drink, out failure)) return false;
        GameplayDomainFactPublication consequence = null;
        try
        {
            if (foul && !outcomes.TryPrepareObservedRecovery(
                    CharacterWaterFactObservation.CaptureConsequence(actual, drinkKey.OperationId.Value),
                    secondKey, commits, out consequence, out failure))
            { drink.Cancel(); return false; }
            var publication = new Publication(this, drink, consequence);
            if (publication.OperationKey != publicationKey || consequence != null && consequence.IsReplay != drink.IsReplay)
            { publication.Cancel(); failure = "water-observer-recovery-torn-cohort"; return false; }
            prepared = publication; return true;
        }
        catch { drink.Cancel(); consequence?.Cancel(); throw; }
    }
    public bool Commit(IReadOnlyList<IDomainFactPreparedPublication> publications, DomainFactDelivery[] results, out string failure)
    {
        if (publications?.Count != 1 || results?.Length != 1 || publications[0] is not Publication value
            || value.CommitGroup != this || value.Used) throw new ArgumentException("water-observer-cohort-invalid");
        value.Used = true;
        var children = value.Consequence == null
            ? new IDomainFactPreparedPublication[] { value.Drink }
            : new IDomainFactPreparedPublication[] { value.Drink, value.Consequence };
        var deliveries = new DomainFactDelivery[children.Length];
        bool accepted = commits.Commit(children, deliveries, out failure);
        results[0] = children.Length == 1 ? deliveries[0]
            : GameplayDomainFactPairProof.Combine(value.OperationKey, deliveries[0], deliveries[1]);
        return accepted;
    }
    public DomainFactDelivery Replay(in CharacterWaterConsumedFact actual) =>
        DomainFactDelivery.Reject("water-observer-replay-requires-saved-reference");
    public void Cancel(object reservation)
    {
        if (reservation is not Reservation state) return;
        state.Used = true; state.Publication?.Cancel();
        outcomes.Cancel(state.Drink); if (state.Consequence.IsValid) outcomes.Cancel(state.Consequence);
    }
    private sealed class Reservation : IDomainFactReservedIdentity
    {
        internal Reservation(CharacterWaterConsumedFact proposal, PreparedMigratedProducerOutcome drink,
            PreparedMigratedProducerOutcome consequence)
        {
            Proposal = proposal; Drink = drink; Consequence = consequence;
            PublicationKey = consequence.IsValid
                ? GameplayDomainFactPairProof.Key(drink.ResultKey.ToString(), consequence.ResultKey.ToString())
                : drink.ResultKey.ToString();
        }
        internal readonly CharacterWaterConsumedFact Proposal;
        internal readonly PreparedMigratedProducerOutcome Drink, Consequence;
        internal Publication Publication;
        internal bool Used;
        public string PublicationKey { get; }
    }
    private sealed class Publication : IDomainFactPreparedPublication, IDomainFactPreparedIdentity
    {
        internal Publication(CharacterWaterFactObserver owner, GameplayDomainFactPublication drink,
            GameplayDomainFactPublication consequence)
        {
            CommitGroup = owner; Drink = drink; Consequence = consequence;
            OperationKey = consequence == null ? drink.OperationKey
                : GameplayDomainFactPairProof.Key(drink.OperationKey, consequence.OperationKey);
        }
        internal readonly GameplayDomainFactPublication Drink, Consequence;
        internal bool Used;
        public IDomainFactCommitGroup CommitGroup { get; }
        public string OperationKey { get; }
        public long OwnerRevision => Drink.OwnerRevision;
        public bool IsReplay => Drink.IsReplay && (Consequence == null || Consequence.IsReplay);
        public void Cancel()
        {
            if (Used) return;
            Used = true; Drink.Cancel(); Consequence?.Cancel();
        }
    }
}

public sealed class CharacterWaterFactBinding : IDomainFactObserverBinding
{
    private readonly CharacterWaterFactObserver observer;
    public IReadOnlyCollection<Type> FactTypes { get; } =
        Array.AsReadOnly(new[] { typeof(CharacterWaterConsumedFact) });
    public CharacterWaterFactBinding(CharacterWaterFactObserver observer) => this.observer = observer;
    public IDisposable Connect(DomainFactDispatcher dispatcher) => dispatcher.Subscribe("survival.water-consumed", observer);
}
