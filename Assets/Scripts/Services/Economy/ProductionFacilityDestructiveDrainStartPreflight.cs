using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public enum ProductionFacilityDestructiveDrainStartPreflightStatus
{
    Ready = 0,
    Deferred = 1,
    Conflict = 2
}

public readonly struct ProductionFacilityDestructiveDrainStartPreflightResult
{
    public ProductionFacilityDestructiveDrainStartPreflightResult(
        ProductionFacilityDestructiveDrainStartPreflightStatus status,
        string reasonCode,
        string sourceFingerprint)
    {
        if (!Enum.IsDefined(
                typeof(ProductionFacilityDestructiveDrainStartPreflightStatus),
                status)
            || status == ProductionFacilityDestructiveDrainStartPreflightStatus.Ready
                != string.IsNullOrEmpty(reasonCode)
            || !ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                sourceFingerprint))
        {
            throw new ArgumentException(
                "A destructive-drain start-preflight result is invalid.");
        }

        Status = status;
        ReasonCode = reasonCode ?? string.Empty;
        SourceFingerprint = sourceFingerprint;
    }

    public ProductionFacilityDestructiveDrainStartPreflightStatus Status { get; }
    public string ReasonCode { get; }
    public string SourceFingerprint { get; }
    public bool CanStart =>
        Status == ProductionFacilityDestructiveDrainStartPreflightStatus.Ready;
}

public interface IProductionFacilityDestructiveDrainStartPreflight
{
    ProductionFacilityDestructiveDrainStartPreflightResult Assess(
        BuildingInstanceId facilityId);
}

/// <summary>
/// Read-only barrier executed before the upper destructive journal freezes the
/// participant graph. A latent physical publication must first be normalized by
/// the ordinary prepared-output lifecycle, while a completed batch is accepted
/// only when the exact durable routing owner already exists.
/// </summary>
public sealed class ProductionFacilityDestructiveDrainStartPreflight :
    IProductionFacilityDestructiveDrainStartPreflight
{
    private const string MissingPendingPrefix = "planned-output-batch-missing:";

    private readonly IProductionFacilityDestructiveDrainPreparedOutputQuery owners;
    private readonly IProductionPreparedOutputRoutingBatchQuery routing;
    private readonly IFacilityBufferPlannedOutputPublicationService publication;

    public ProductionFacilityDestructiveDrainStartPreflight(
        IProductionFacilityDestructiveDrainPreparedOutputQuery owners,
        IProductionPreparedOutputRoutingBatchQuery routing,
        IFacilityBufferPlannedOutputPublicationService publication)
    {
        this.owners = owners ?? throw new ArgumentNullException(nameof(owners));
        this.routing = routing ?? throw new ArgumentNullException(nameof(routing));
        this.publication = publication
            ?? throw new ArgumentNullException(nameof(publication));
    }

    public ProductionFacilityDestructiveDrainStartPreflightResult Assess(
        BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
            throw new ArgumentException("A valid facility ID is required.", nameof(facilityId));

        ProductionFacilityDestructiveDrainPreparedOutputOwner[] captured =
            (owners.CapturePreparedOutputOwners(facilityId)
                ?? Array.Empty<
                    ProductionFacilityDestructiveDrainPreparedOutputOwner>())
            .OrderBy(value => value.BillId.Value, StringComparer.Ordinal)
            .ToArray();
        string sourceFingerprint = Fingerprint(captured);

        foreach (ProductionFacilityDestructiveDrainPreparedOutputOwner owner in
                 captured)
        {
            if (!owner.FacilityId.Equals(facilityId))
            {
                return Result(
                    ProductionFacilityDestructiveDrainStartPreflightStatus.Conflict,
                    "prepared-output-owner-facility-mismatch:" + owner.BillId.Value,
                    sourceFingerprint);
            }

            switch (owner.Phase)
            {
                case ProductionPreparedOutputPhase.Unresolved:
                case ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace:
                    continue;

                case ProductionPreparedOutputPhase.PublicationPrepared:
                    if (publication.TryCapturePendingBatch(
                            owner.BatchCommitId,
                            out _,
                            out _,
                            out string pendingFailure))
                    {
                        return Result(
                            ProductionFacilityDestructiveDrainStartPreflightStatus
                                .Deferred,
                            "prepared-output-publication-normalization-required:"
                                + owner.BillId.Value,
                            sourceFingerprint);
                    }
                    if (!pendingFailure.StartsWith(
                            MissingPendingPrefix,
                            StringComparison.Ordinal))
                    {
                        return Result(
                            ProductionFacilityDestructiveDrainStartPreflightStatus
                                .Conflict,
                            "prepared-output-publication-marker-invalid:"
                                + owner.BillId.Value + ":" + pendingFailure,
                            sourceFingerprint);
                    }
                    continue;

                case ProductionPreparedOutputPhase
                    .PhysicalBatchCommittedPublicationPending:
                    return Result(
                        ProductionFacilityDestructiveDrainStartPreflightStatus.Deferred,
                        "prepared-output-routing-publication-pending:"
                            + owner.BillId.Value,
                        sourceFingerprint);

                case ProductionPreparedOutputPhase.Completed:
                    if (!routing.TryCaptureBatch(
                            owner.BatchCommitId,
                            out ProductionPreparedOutputRoutingBatchSnapshot batch))
                    {
                        return Result(
                            ProductionFacilityDestructiveDrainStartPreflightStatus
                                .Deferred,
                            "prepared-output-routing-batch-missing:"
                                + owner.BillId.Value,
                            sourceFingerprint);
                    }
                    if (!Matches(owner, batch))
                    {
                        return Result(
                            ProductionFacilityDestructiveDrainStartPreflightStatus
                                .Conflict,
                            "prepared-output-routing-batch-mismatch:"
                                + owner.BillId.Value,
                            sourceFingerprint);
                    }
                    continue;

                default:
                    return Result(
                        ProductionFacilityDestructiveDrainStartPreflightStatus.Conflict,
                        "prepared-output-phase-invalid:" + owner.BillId.Value,
                        sourceFingerprint);
            }
        }

        return Result(
            ProductionFacilityDestructiveDrainStartPreflightStatus.Ready,
            string.Empty,
            sourceFingerprint);
    }

    private static bool Matches(
        ProductionFacilityDestructiveDrainPreparedOutputOwner owner,
        ProductionPreparedOutputRoutingBatchSnapshot batch) =>
        batch != null
        && string.Equals(batch.BatchCommitId, owner.BatchCommitId,
            StringComparison.Ordinal)
        && string.Equals(batch.OwnerBillId, owner.BillId.Value,
            StringComparison.Ordinal)
        && string.Equals(batch.OwnerRecipeId, owner.RecipeId,
            StringComparison.Ordinal)
        && string.Equals(batch.OwnerFacilityId, owner.FacilityId.Value,
            StringComparison.Ordinal)
        && batch.CycleSequence == owner.CycleSequence
        && string.Equals(batch.OutcomeFingerprint, owner.OutcomeFingerprint,
            StringComparison.Ordinal)
        && string.Equals(batch.SourceDestinationId, owner.DestinationId,
            StringComparison.Ordinal);

    private static ProductionFacilityDestructiveDrainStartPreflightResult Result(
        ProductionFacilityDestructiveDrainStartPreflightStatus status,
        string reasonCode,
        string sourceFingerprint) => new(status, reasonCode, sourceFingerprint);

    private static string Fingerprint(
        IReadOnlyList<ProductionFacilityDestructiveDrainPreparedOutputOwner> values)
    {
        StringBuilder canonical = new(64 + values.Count * 256);
        foreach (ProductionFacilityDestructiveDrainPreparedOutputOwner value in values)
        {
            canonical.Append(value.BillId.Value).Append('\n')
                .Append(value.FacilityId.Value).Append('\n')
                .Append(value.RecipeId).Append('\n')
                .Append(value.CycleSequence).Append('\n')
                .Append(value.DestinationId).Append('\n')
                .Append((int)value.Phase).Append('\n')
                .Append(value.BatchCommitId).Append('\n')
                .Append(value.OutcomeFingerprint).Append('\n');
        }
        return ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
            canonical.ToString());
    }
}
