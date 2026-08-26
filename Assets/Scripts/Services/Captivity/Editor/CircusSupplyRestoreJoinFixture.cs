using System;
using System.Collections.Generic;

public static class CircusSupplyRestoreJoinFixture
{
    public static string Run()
    {
        const string orderId = "circus:1", source = "stack:prop:1";
        string operation = $"circus-show-supplies:{orderId}:00000001";
        const long grams = 1250;
        string commit = $"physical-batch-disposition:3:{operation}:1:{grams}";
        CircusShowOrder owner = new CircusShowOrder {
            orderId=orderId, nextSupplyOperationSequence=1, pendingSupplyOperationSequence=1,
            pendingSupplyPhase=CircusShowSupplyCommitPhase.ItemCommitted,
            pendingSupplyOperationId=operation, pendingSupplyReasonCode="circus-performance-prop-consumed",
            pendingSupplyCommitId=commit, pendingSupplySourceStackIds=new List<string>{source},
            pendingSupplyQuantity=1, pendingSupplyMassGrams=grams,
            pendingSupplyCartStackId="stack:cart:1", pendingSupplyCartDurabilityBefore=100f,
            pendingSupplyCartDurabilityAfter=96f };
        PhysicalItemRestoreCandidateDispositionSnapshot receipt = Receipt(operation, source, grams, commit);
        CircusSaveData payload = new CircusSaveData { orders=new List<CircusShowOrder>{owner} };
        CircusSaveSection.ValidatePhysicalRestoreCandidate(payload, new Query(receipt));
        Reject(payload, new Query());
        Reject(new CircusSaveData(), new Query(receipt));
        Reject(payload, new Query(Receipt(operation, source, grams + 1, commit)));
        return "valid/missing/orphan/mismatch Circus physical joins are fail-closed";
    }

    private static PhysicalItemRestoreCandidateDispositionSnapshot Receipt(string op,string source,long grams,string commit) =>
        new PhysicalItemRestoreCandidateDispositionSnapshot(PhysicalItemDispositionKind.Sink,op,
            "circus-performance-prop-consumed","fixture",new[]{source},1,grams,commit);
    private static void Reject(CircusSaveData payload, Query query)
    {
        try { CircusSaveSection.ValidatePhysicalRestoreCandidate(payload, query); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Circus physical restore join accepted invalid provenance.");
    }
    private sealed class Query : IPhysicalItemRestoreCandidateQuery
    {
        private readonly IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot> values;
        public Query(params PhysicalItemRestoreCandidateDispositionSnapshot[] values) => this.values=values;
        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot> PendingBatchDispositions => values;
        public bool TryGetPendingBatchDisposition(string id,out PhysicalItemRestoreCandidateDispositionSnapshot found)
        { foreach(var value in values) if(value.OperationId==id){found=value;return true;} found=null;return false; }
    }
}
