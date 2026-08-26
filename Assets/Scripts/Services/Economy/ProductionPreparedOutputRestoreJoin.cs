using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ProductionPreparedOutputRestoreJoinPlan
{
    private readonly IReadOnlyList<
        FacilityBufferPlannedOutputRestoreBatchSnapshot> acknowledgements;

    internal ProductionPreparedOutputRestoreJoinPlan(
        DungeonProductionBillSaveData normalizedPayload,
        IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot>
            acknowledgements)
    {
        NormalizedPayload = normalizedPayload
            ?? throw new ArgumentNullException(nameof(normalizedPayload));
        this.acknowledgements = Array.AsReadOnly((acknowledgements
                ?? Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>())
            .ToArray());
    }

    public DungeonProductionBillSaveData NormalizedPayload { get; }
    public IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot>
        Acknowledgements => acknowledgements;
}

public interface IProductionPreparedOutputRestoreJoin
{
    ProductionPreparedOutputRestoreJoinPlan Build(
        DungeonProductionBillSaveData payload);

    void Acknowledge(ProductionPreparedOutputRestoreJoinPlan plan);
}

public sealed class EmptyProductionPreparedOutputRestoreJoin :
    IProductionPreparedOutputRestoreJoin
{
    public static readonly EmptyProductionPreparedOutputRestoreJoin Instance =
        new();

    private EmptyProductionPreparedOutputRestoreJoin()
    {
    }

    public ProductionPreparedOutputRestoreJoinPlan Build(
        DungeonProductionBillSaveData payload)
    {
        if ((payload?.bills ?? new List<ProductionBillSaveData>()).Any(value =>
                value?.preparedOutput != null
                && value.preparedOutput.phase !=
                    ProductionPreparedOutputPhase.Unresolved))
        {
            throw new InvalidOperationException(
                "Prepared production output restore requires the physical join authority.");
        }

        return new ProductionPreparedOutputRestoreJoinPlan(
            payload ?? throw new ArgumentNullException(nameof(payload)),
            Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>());
    }

    public void Acknowledge(ProductionPreparedOutputRestoreJoinPlan plan)
    {
        if (plan == null || plan.Acknowledgements.Count != 0)
        {
            throw new InvalidOperationException(
                "The empty prepared-output restore join cannot acknowledge physical output.");
        }
    }
}

/// <summary>
/// Joins the detached Production V16 owner with the detached physical-item
/// planned-publication index before either aggregate root is published. A
/// pending physical batch is adopted exactly once and normalized to Completed;
/// the matching marker is then converted to durable, non-stacking provenance
/// inside the same aggregate-root staging transaction.
/// </summary>
public sealed class ProductionPreparedOutputRestoreJoin :
    IProductionPreparedOutputRestoreJoin
{
    private readonly IFacilityBufferPlannedOutputRestoreCandidateQuery query;
    private readonly IFacilityBufferPlannedOutputPublicationService publication;

    public ProductionPreparedOutputRestoreJoin(
        IFacilityBufferPlannedOutputRestoreCandidateQuery query,
        IFacilityBufferPlannedOutputPublicationService publication)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.publication = publication
            ?? throw new ArgumentNullException(nameof(publication));
    }

    public ProductionPreparedOutputRestoreJoinPlan Build(
        DungeonProductionBillSaveData payload)
    {
        if (payload?.bills == null)
        {
            throw new InvalidOperationException(
                "Production restore has no bill owner collection.");
        }
        if (!query.IsCandidateAvailable || query.Batches == null)
        {
            throw new InvalidOperationException(
                "Prepared production output restore requires the incoming physical candidate.");
        }

        Dictionary<string, ProductionBillSaveData> owners = new(
            StringComparer.Ordinal);
        List<FacilityBufferPlannedOutputRestoreBatchSnapshot> acknowledge = new();
        foreach (ProductionBillSaveData bill in payload.bills)
        {
            ProductionPreparedOutputBatchSaveData batch = bill?.preparedOutput;
            if (batch == null
                || batch.phase == ProductionPreparedOutputPhase.Unresolved
                || batch.phase == ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace)
            {
                continue;
            }
            if (!owners.TryAdd(batch.batchCommitId, bill))
            {
                throw new InvalidOperationException(
                    "Duplicate prepared-output restore owner '"
                    + batch.batchCommitId + "'.");
            }

            bool hasIncoming = query.TryGetBatch(
                batch.batchCommitId,
                out FacilityBufferPlannedOutputRestoreBatchSnapshot incoming);
            switch (batch.phase)
            {
                case ProductionPreparedOutputPhase.PublicationPrepared:
                    if (hasIncoming)
                    {
                        ValidateIncoming(batch, incoming);
                        AdoptPhysicalCandidates(batch, incoming);
                        batch.phase = ProductionPreparedOutputPhase.Completed;
                        acknowledge.Add(incoming);
                    }
                    break;

                case ProductionPreparedOutputPhase.PhysicalBatchCommittedPublicationPending:
                    if (!hasIncoming)
                    {
                        throw new InvalidOperationException(
                            "Prepared production output has no exact incoming physical batch: "
                            + batch.batchCommitId);
                    }
                    ValidateIncoming(batch, incoming);
                    ValidatePersistedCandidates(batch, incoming);
                    batch.phase = ProductionPreparedOutputPhase.Completed;
                    acknowledge.Add(incoming);
                    break;

                case ProductionPreparedOutputPhase.Completed:
                    if (hasIncoming)
                    {
                        throw new InvalidOperationException(
                            "Completed prepared output still has an unacknowledged physical marker: "
                            + batch.batchCommitId);
                    }
                    break;

                default:
                    throw new InvalidOperationException(
                        "Prepared production output has an unsupported restore phase.");
            }

            ProductionPreparedOutputContract.ValidateForBill(
                batch,
                (ProductionBillId)bill.billId,
                bill.recipeId,
                bill.cycleSequence,
                bill.outputDestinationId);
        }

        HashSet<string> acknowledgementIds = acknowledge
            .Select(value => value.BatchCommitId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> incomingIds = new(StringComparer.Ordinal);
        foreach (FacilityBufferPlannedOutputRestoreBatchSnapshot incoming in
                 query.Batches.OrderBy(value => value.BatchCommitId,
                     StringComparer.Ordinal))
        {
            if (incoming == null
                || !incomingIds.Add(incoming.BatchCommitId)
                || !owners.TryGetValue(incoming.BatchCommitId, out var owner)
                || owner.preparedOutput.phase != ProductionPreparedOutputPhase.Completed
                || !acknowledgementIds.Contains(incoming.BatchCommitId))
            {
                throw new InvalidOperationException(
                    "Incoming planned-output physical batch has no exact Production owner: "
                    + (incoming?.BatchCommitId ?? "<null>"));
            }
        }

        return new ProductionPreparedOutputRestoreJoinPlan(
            payload,
            acknowledge
                .OrderBy(value => value.BatchCommitId, StringComparer.Ordinal)
                .ToArray());
    }

    public void Acknowledge(ProductionPreparedOutputRestoreJoinPlan plan)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));

        foreach (FacilityBufferPlannedOutputRestoreBatchSnapshot candidate in
                 plan.Acknowledgements)
        {
            if (!publication.TryAcknowledgeRestoreCandidate(
                    candidate,
                    out FacilityBufferPlannedOutputPublicationFailureCode code,
                    out string reason))
            {
                throw new InvalidOperationException(
                    $"Prepared-output restore acknowledgement failed "
                    + $"({code}): {reason}");
            }
        }
    }

    private static void ValidateIncoming(
        ProductionPreparedOutputBatchSaveData owner,
        FacilityBufferPlannedOutputRestoreBatchSnapshot incoming)
    {
        if (owner == null
            || incoming == null
            || !string.Equals(owner.batchCommitId, incoming.BatchCommitId,
                StringComparison.Ordinal)
            || !string.Equals(owner.outcomeFingerprint,
                incoming.OutcomeFingerprint, StringComparison.Ordinal)
            || !string.Equals(owner.admissionFingerprint,
                incoming.PlannedOutputFingerprint, StringComparison.Ordinal)
            || owner.totalPhysicalMassGrams != incoming.TotalMassGrams
            || owner.lines.Where(IsPhysicalLine).Sum(value => (long)value.quantity)
                != incoming.TotalQuantity
            || owner.lines.Where(IsPhysicalLine).Sum(value => value.exactMassGrams)
                != incoming.TotalMassGrams)
        {
            throw new InvalidOperationException(
                "Prepared production output conflicts with its incoming physical batch: "
                + (owner?.batchCommitId ?? "<null>"));
        }

        Dictionary<string, ProductionPreparedOutputLineSaveData> lines =
            owner.lines.Where(IsPhysicalLine).ToDictionary(
                value => value.outputLineId,
                StringComparer.Ordinal);
        foreach (IGrouping<string, FacilityBufferPlannedOutputRestoreStackSnapshot>
                 group in incoming.Stacks.GroupBy(
                     value => value.OutputLineId,
                     StringComparer.Ordinal))
        {
            if (!lines.TryGetValue(group.Key, out var line)
                || group.Sum(value => value.Quantity) != line.quantity
                || group.Sum(value => value.MassGrams) != line.exactMassGrams
                || group.Any(value => value == null
                    || !string.Equals(value.BatchCommitId, owner.batchCommitId,
                        StringComparison.Ordinal)
                    || !string.Equals(value.OutcomeFingerprint,
                        owner.outcomeFingerprint, StringComparison.Ordinal)
                    || !string.Equals(value.PlannedOutputFingerprint,
                        owner.admissionFingerprint, StringComparison.Ordinal)
                    || !string.Equals(value.ItemId, line.itemId,
                        StringComparison.Ordinal)
                    || !string.Equals(value.DestinationId, owner.destinationId,
                        StringComparison.Ordinal)
                    || value.State != WorldItemStackState.FacilityOutputBuffer
                    || value.Quantity <= 0
                    || value.MassGrams <= 0L))
            {
                throw new InvalidOperationException(
                    "Prepared production output line conflicts with its incoming physical batch: "
                    + owner.batchCommitId + ":" + group.Key);
            }
        }
        if (incoming.Stacks.Select(value => value.OutputLineId)
            .Distinct(StringComparer.Ordinal).Count() != lines.Count)
        {
            throw new InvalidOperationException(
                "Prepared production output physical lines are partial: "
                + owner.batchCommitId);
        }
    }

    private static void ValidatePersistedCandidates(
        ProductionPreparedOutputBatchSaveData owner,
        FacilityBufferPlannedOutputRestoreBatchSnapshot incoming)
    {
        ProductionPreparedOutputPhysicalCandidateSaveData[] expected =
            BuildCandidates(owner, incoming);
        ProductionPreparedOutputPhysicalCandidateSaveData[] actual =
            (owner.physicalCandidates
                ?? new List<ProductionPreparedOutputPhysicalCandidateSaveData>())
            .OrderBy(value => value?.stackId, StringComparer.Ordinal)
            .ToArray();
        if (actual.Length != expected.Length
            || actual.Where((value, index) => !CandidateMatches(
                value,
                expected[index])).Any())
        {
            throw new InvalidOperationException(
                "Prepared production output saved candidates conflict with the physical restore batch: "
                + owner.batchCommitId);
        }
    }

    private static void AdoptPhysicalCandidates(
        ProductionPreparedOutputBatchSaveData owner,
        FacilityBufferPlannedOutputRestoreBatchSnapshot incoming) =>
        owner.physicalCandidates = BuildCandidates(owner, incoming).ToList();

    private static ProductionPreparedOutputPhysicalCandidateSaveData[]
        BuildCandidates(
            ProductionPreparedOutputBatchSaveData owner,
            FacilityBufferPlannedOutputRestoreBatchSnapshot incoming)
    {
        Dictionary<string, string> lineCommits = owner.lines
            .Where(IsPhysicalLine)
            .ToDictionary(
                value => value.outputLineId,
                value => value.lineCommitId,
                StringComparer.Ordinal);
        return incoming.Stacks
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .Select(value => new ProductionPreparedOutputPhysicalCandidateSaveData
            {
                stackId = value.StackId,
                batchCommitId = owner.batchCommitId,
                outputLineId = value.OutputLineId,
                lineCommitId = lineCommits[value.OutputLineId],
                itemId = value.ItemId,
                quantity = value.Quantity,
                massGrams = value.MassGrams,
                destinationId = value.DestinationId,
                state = ProductionPreparedPhysicalCandidateState.FacilityOutputBuffer
            })
            .ToArray();
    }

    private static bool CandidateMatches(
        ProductionPreparedOutputPhysicalCandidateSaveData left,
        ProductionPreparedOutputPhysicalCandidateSaveData right) =>
        left != null
        && right != null
        && left.quantity == right.quantity
        && left.massGrams == right.massGrams
        && left.state == right.state
        && string.Equals(left.stackId, right.stackId, StringComparison.Ordinal)
        && string.Equals(left.batchCommitId, right.batchCommitId,
            StringComparison.Ordinal)
        && string.Equals(left.outputLineId, right.outputLineId,
            StringComparison.Ordinal)
        && string.Equals(left.lineCommitId, right.lineCommitId,
            StringComparison.Ordinal)
        && string.Equals(left.itemId, right.itemId, StringComparison.Ordinal)
        && string.Equals(left.destinationId, right.destinationId,
            StringComparison.Ordinal);

    private static bool IsPhysicalLine(
        ProductionPreparedOutputLineSaveData line) =>
        line != null
        && line.role != ProductionOutputRole.DeclaredLoss
        && line.rollSucceeded
        && line.quantity > 0;
}
