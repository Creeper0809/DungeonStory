using System;
using System.Linq;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class OffenseArrivalSettlementObservationData
{
    public bool hasFact, pending, projectionPending;
    public string publicationKey = string.Empty;
    public OffenseArrivalSettlementFactData fact = new();
    public OffenseArrivalSettlementObservationData Clone() => new()
    { hasFact = hasFact, pending = pending, projectionPending = projectionPending, publicationKey = publicationKey, fact = fact.Clone() };
}

/// <summary>Terminal state/publication transaction. Physical capture and escape stay with the world owner.</summary>
public sealed class OffenseArrivalSettlementRuntime
{
    private readonly IDomainFactPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private bool busy;
    public string LastFailure { get; private set; } = string.Empty;
    public OffenseArrivalSettlementRuntime(IDomainFactPublisher publisher, IDomainFactRecoveryPublisher recovery)
    { this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher)); this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery)); }
    public void RequireIdle() { if (busy) throw new InvalidOperationException("offense-arrival-settlement-active"); }

    public DomainFactDelivery Settle(OffenseReturnArrivalState arrival, int secured, int escaped, int day)
    {
        RequireIdle();
        ValidateState(arrival);
        if (arrival.stage != OffenseReturnArrivalStage.AwaitingContainment || arrival.settlement.hasFact)
            return DomainFactDelivery.Reject("offense-arrival-already-settled-or-not-ready");
        var fact = new OffenseArrivalSettledFact(new OffenseArrivalSettlementFactData
        {
            arrivalId = arrival.arrivalId, expeditionId = arrival.expeditionId, targetId = arrival.targetId,
            kind = arrival.kind, absoluteDay = day, requestedAmount = arrival.requestedAmount,
            materializedAmount = arrival.materializedIds.Count, securedAmount = secured, escapedAmount = escaped,
            materializedIds = new(arrival.materializedIds), escapedIds = new(arrival.escapedIds)
        });
        DomainFactLease lease = default;
        var before = new Before(arrival);
        bool applied = false, attempted = false;
        busy = true;
        try
        {
            if (!publisher.TryReserve(fact, out lease, out string failure))
                return Reject("offense-arrival-reserve:" + failure);
            if (!lease.IsValid || string.IsNullOrWhiteSpace(lease.PublicationKey))
                return Reject("offense-arrival-reservation-invalid");
            arrival.settlement = new() { hasFact = true, pending = true, projectionPending = true,
                publicationKey = lease.PublicationKey, fact = fact.Snapshot() };
            arrival.stage = escaped > 0 ? OffenseReturnArrivalStage.Escaped : OffenseReturnArrivalStage.Secured;
            arrival.settledMaterializedAmount = arrival.materializedIds.Count;
            arrival.settledSecuredAmount = secured;
            arrival.settledEscapedAmount = escaped;
            arrival.escapeRisk = 0f;
            arrival.lastStatus = escaped > 0 ? $"{escaped}개 대상이 수용 전에 달아났습니다." : "수용 절차가 완료되었습니다.";
            applied = true;
            attempted = true;
            DomainFactDelivery result = publisher.Publish(lease, fact);
            if (Delivered(arrival.settlement.publicationKey, result)) arrival.settlement.pending = false;
            else if (result.Completed) result = DomainFactDelivery.Uncertain("offense-arrival-proof-invalid");
            if (!result.MayBeCommitted) before.Restore(arrival);
            LastFailure = result.Completed ? string.Empty : result.Detail;
            return result;
        }
        catch (Exception exception) when (Recoverable(exception))
        {
            if (!attempted && applied) before.Restore(arrival);
            LastFailure = "offense-arrival-publication:" + exception.GetType().Name + ":" + exception.Message;
            return attempted ? DomainFactDelivery.Uncertain(LastFailure) : DomainFactDelivery.Reject(LastFailure);
        }
        finally
        {
            try { publisher.Cancel(lease); }
            catch (Exception exception) when (Recoverable(exception)) { LastFailure = "offense-arrival-cancel:" + exception.GetType().Name; }
            busy = false;
        }
    }
    public bool Recover(OffenseReturnArrivalState arrival, out string failure)
    {
        RequireIdle(); ValidateState(arrival); failure = string.Empty;
        if (!arrival.settlement.hasFact || !arrival.settlement.pending) return true;
        busy = true;
        try
        {
            var row = arrival.settlement;
            var replies = new DomainFactDelivery[1];
            if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(row.publicationKey, new OffenseArrivalSettledFact(row.fact)) }, replies, out failure)
                || !Delivered(row.publicationKey, replies[0]))
            { failure = "offense-arrival-recovery-pending:" + failure + ":" + replies[0].Detail; LastFailure = failure; return false; }
            row.pending = false;
            LastFailure = string.Empty;
            return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failure = "offense-arrival-recovery:" + exception.GetType().Name + ":" + exception.Message; LastFailure = failure; return false; }
        finally { busy = false; }
    }
    public bool DeliverProjection(OffenseReturnArrivalState arrival, Action publish)
    {
        RequireIdle(); ValidateState(arrival);
        if (!arrival.settlement.projectionPending) return true;
        busy = true;
        try
        {
            publish();
            arrival.settlement.projectionPending = false;
            if (LastFailure.StartsWith("offense-arrival-projection:", StringComparison.Ordinal)) LastFailure = string.Empty;
            return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { LastFailure = "offense-arrival-projection:" + exception.GetType().Name + ":" + exception.Message; return false; }
        finally { busy = false; }
    }
    public static void ValidateState(OffenseReturnArrivalState arrival)
    {
        if (arrival?.settlement == null || arrival.settlement.fact == null)
            throw new ArgumentException("offense-arrival-observation-missing");
        var row = arrival.settlement;
        bool terminal = arrival.stage is OffenseReturnArrivalStage.Secured or OffenseReturnArrivalStage.Escaped;
        if (row.hasFact != terminal) throw new ArgumentException("offense-arrival-terminal-observation-conflict");
        var data = row.fact;
        if (!row.hasFact)
        {
            if (row.pending || row.projectionPending || row.publicationKey != string.Empty
                || data.arrivalId != string.Empty || data.expeditionId != string.Empty || data.targetId != string.Empty
                || data.kind != default || data.absoluteDay != 0 || data.requestedAmount != 0 || data.materializedAmount != 0
                || data.securedAmount != 0 || data.escapedAmount != 0 || data.materializedIds?.Count != 0 || data.escapedIds?.Count != 0)
                throw new ArgumentException("offense-arrival-empty-observation-conflict");
            return;
        }
        _ = new OffenseArrivalSettledFact(data);
        if (!DomainFactValueSyntax.IsText(row.publicationKey, 1024, false)
            || data.arrivalId != arrival.arrivalId || data.expeditionId != arrival.expeditionId || data.targetId != arrival.targetId
            || data.kind != arrival.kind || data.requestedAmount != arrival.requestedAmount
            || data.materializedAmount != arrival.settledMaterializedAmount || data.securedAmount != arrival.settledSecuredAmount
            || data.escapedAmount != arrival.settledEscapedAmount || arrival.escapeRisk != 0f
            || (data.escapedAmount == 0) != (arrival.stage == OffenseReturnArrivalStage.Secured)
            || !data.materializedIds.SequenceEqual(arrival.materializedIds, StringComparer.Ordinal)
            || !data.escapedIds.SequenceEqual(arrival.escapedIds, StringComparer.Ordinal))
            throw new ArgumentException("offense-arrival-owner-fact-conflict");
    }

    // History receipts are a projection, never an independently applied effect.
    // Rebuild terminal entries at capture so a failed UI/event listener cannot tear a save.
    public static void ProjectTerminalReceipts(DungeonOffenseSaveData expeditions, DungeonOffenseReturnArrivalSaveData arrivals)
    {
        if (expeditions?.resultHistory == null || arrivals?.arrivals == null)
            throw new ArgumentException("offense-arrival-projection-source-missing");
        foreach (var arrival in arrivals.arrivals)
        {
            ValidateState(arrival);
            if (!arrival.settlement.hasFact) continue;
            var result = expeditions.resultHistory.SingleOrDefault(value => value.expeditionId == arrival.expeditionId)
                ?? throw new InvalidOperationException("offense-arrival-projection-result-missing");
            if (result.targetId != arrival.targetId || result.arrivalReceipts == null)
                throw new InvalidOperationException("offense-arrival-projection-result-conflict");
            var previous = result.arrivalReceipts.SingleOrDefault(value => value.arrivalId == arrival.arrivalId)
                ?? throw new InvalidOperationException("offense-arrival-projection-receipt-missing");
            if (previous.kind != arrival.kind.ToString() || previous.requestedAmount != arrival.requestedAmount)
                throw new InvalidOperationException("offense-arrival-projection-receipt-conflict");
            var actual = new OffenseArrivalSettledFact(arrival.settlement.fact).Receipt;
            result.arrivalReceipts[result.arrivalReceipts.IndexOf(previous)] = new()
            {
                arrivalId = actual.arrivalId, kind = actual.kind, requestedAmount = actual.requestedAmount,
                materializedAmount = actual.materializedAmount, securedAmount = actual.securedAmount,
                escapedAmount = actual.escapedAmount, resolution = actual.resolution
            };
        }
    }
    private DomainFactDelivery Reject(string failure) { LastFailure = failure; return DomainFactDelivery.Reject(failure); }
    private static bool Delivered(string key, DomainFactDelivery result) => result.Completed && result.OperationKey == key
        && !string.IsNullOrWhiteSpace(result.PublicationId) && result.Fingerprint?.Length == 64
        && result.Fingerprint.All(value => value >= '0' && value <= '9' || value >= 'a' && value <= 'f' || value >= 'A' && value <= 'F');
    private static bool Recoverable(Exception exception) => exception is not OutOfMemoryException && exception is not StackOverflowException && exception is not AccessViolationException;
    private sealed class Before
    {
        private readonly OffenseReturnArrivalStage stage;
        private readonly int materialized, secured, escaped;
        private readonly float risk;
        private readonly string status;
        private readonly OffenseArrivalSettlementObservationData observation;
        internal Before(OffenseReturnArrivalState source)
        { stage = source.stage; materialized = source.settledMaterializedAmount; secured = source.settledSecuredAmount;
            escaped = source.settledEscapedAmount; risk = source.escapeRisk; status = source.lastStatus; observation = source.settlement; }
        internal void Restore(OffenseReturnArrivalState target)
        { target.stage = stage; target.settledMaterializedAmount = materialized; target.settledSecuredAmount = secured;
            target.settledEscapedAmount = escaped; target.escapeRisk = risk; target.lastStatus = status; target.settlement = observation; }
    }
}
