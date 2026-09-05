using System;
using System.Collections.Generic;
using System.Linq;

// V18 required section: validation succeeds before the candidate Aggregate root is replaced.
public sealed class ResourceStockPolicySaveSection :
    DungeonStrictJsonSaveSection<
        DungeonResourceStockPolicySaveData,
        ResourceStockPolicyRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "economy.stock-policies";

    private static readonly string[] Dependencies =
    {
        PhysicalItemsSaveSection.Id,
        ProductionBillsSaveSection.Id
    };

    private readonly IResourceStockPolicyRuntime runtime;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;
    private readonly IEconomyProjectInputOwnerRestoreRuntime inputOwners;

    public ResourceStockPolicySaveSection(
        IResourceStockPolicyRuntime runtime,
        IResourceEconomyContentCatalog catalog,
        IPhysicalItemRestoreCandidateQuery physicalCandidates,
        IEconomyProjectInputOwnerRestoreRuntime inputOwners)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
        this.physicalCandidates = physicalCandidates
            ?? throw new ArgumentNullException(nameof(physicalCandidates));
        this.inputOwners = inputOwners
            ?? throw new ArgumentNullException(nameof(inputOwners));
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        DungeonResourceStockPolicySaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonResourceStockPolicySaveData CapturePayload()
    {
        return runtime.Capture();
    }

    protected override ResourceStockPolicyRestoreCandidate
        BuildRestoreCandidate(DungeonResourceStockPolicySaveData payload)
    {
        ValidateLocalPayload(payload);
        ValidatePhysicalRestoreCandidate(payload, physicalCandidates);
        ResourceStockPolicyRestoreCandidate candidate =
            runtime.PrepareRestoreCandidate(payload);
        if (!inputOwners.TryReplaceForRestore(
                EconomyProjectInputOwnerAuthority.StockPolicyDomain,
                BuildInputOwnerDescriptors(candidate),
                out string ownerFailure))
            throw new InvalidOperationException(
                "Stock-policy exact input-owner restore join failed: "
                + ownerFailure);
        return candidate;
    }

    private IReadOnlyList<EconomyProjectInputOwnerDescriptor>
        BuildInputOwnerDescriptors(ResourceStockPolicyRestoreCandidate candidate)
    {
        return (candidate?.State?.PolicyView
                ?? Array.Empty<ResourceStockPolicyData>())
            .Where(value => value != null
                && !string.IsNullOrEmpty(value.inputDestinationId))
            .OrderBy(value => value.inputDestinationId, StringComparer.Ordinal)
            .Select(value =>
            {
                if (!catalog.TryGetItem(
                        value.itemId,
                        out ResourceItemDefinitionSO item)
                    || item.MaxStack <= 0)
                {
                    throw new InvalidOperationException(
                        "Stock-policy input owner has no authored item capacity: "
                        + value.itemId);
                }
                return new EconomyProjectInputOwnerDescriptor(
                    EconomyProjectInputOwnerAuthority.StockPolicyDomain,
                    value.itemId,
                    value.inputDestinationId,
                    new UnityEngine.Vector2Int(
                        value.inputDestinationX,
                        value.inputDestinationY),
                    FacilityBufferDestinationAnchorKind.ReservedTarget,
                    string.Empty,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [value.itemId] = item.MaxStack
                    },
                    value.inputCapacityGrams,
                    value.inputMassAuthorityRevision,
                    value.inputCapacityFingerprint);
            })
            .ToArray();
    }

    protected override void ValidateParsedPayload(
        DungeonResourceStockPolicySaveData payload)
    {
        ValidateLocalPayload(payload);
        _ = runtime.PrepareRestoreCandidate(payload);
    }

    protected override void PublishRestoreCandidate(
        ResourceStockPolicyRestoreCandidate candidate) =>
        runtime.PublishRestoreCandidate(candidate);

    public static void ValidatePhysicalRestoreCandidate(
        DungeonResourceStockPolicySaveData payload,
        IPhysicalItemRestoreCandidateQuery physicalCandidates)
    {
        if (payload?.pendingSales == null
            || payload.pendingRejectedSales == null)
        {
            throw new InvalidOperationException(
                "Stock-policy physical restore join has no sale outbox payload.");
        }
        if (physicalCandidates == null
            || !physicalCandidates.IsCandidateAvailable)
        {
            throw new InvalidOperationException(
                "Stock-policy restore requires the incoming physical-item candidate.");
        }

        Dictionary<string, ResourceStockPolicyPendingSale> owners =
            payload.pendingSales.ToDictionary(
                pending => pending.operationId,
                StringComparer.Ordinal);
        Dictionary<string, QualityRejectedSalePending> rejectedOwners =
            payload.pendingRejectedSales.ToDictionary(
                pending => pending.operationId,
                StringComparer.Ordinal);
        foreach (KeyValuePair<string, ResourceStockPolicyPendingSale> pair in owners)
        {
            if (!physicalCandidates.TryGetPendingBatchDisposition(
                    pair.Key,
                    out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || !Matches(pair.Value, receipt))
            {
                throw new InvalidOperationException(
                    $"Stock-policy pending sale '{pair.Key}' has no exact incoming physical Transfer receipt.");
            }
        }
        foreach (KeyValuePair<string, QualityRejectedSalePending> pair in rejectedOwners)
        {
            if (pair.Value.phase == QualityRejectedSaleCommitPhase.Prepared)
            {
                if (physicalCandidates.TryGetPendingBatchDisposition(
                        pair.Key,
                        out _))
                {
                    throw new InvalidOperationException(
                        $"Prepared quality-rejected sale '{pair.Key}' unexpectedly owns a physical receipt.");
                }
                continue;
            }
            if (!physicalCandidates.TryGetPendingBatchDisposition(
                    pair.Key,
                    out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || !QualityRejectedSaleOutbox.ReceiptMatchesSaved(
                    pair.Value,
                    receipt))
            {
                throw new InvalidOperationException(
                    $"Quality-rejected sale '{pair.Key}' has no exact incoming physical Transfer receipt.");
            }
        }

        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 physicalCandidates.PendingBatchDispositions)
        {
            if (receipt?.OperationId == null
                || (!receipt.OperationId.StartsWith(
                        ResourceStockPolicySaleOutbox.OperationPrefix,
                        StringComparison.Ordinal)
                    && !receipt.OperationId.StartsWith(
                        QualityRejectedSaleOutbox.OperationPrefix,
                        StringComparison.Ordinal)))
            {
                continue;
            }
            bool genericMatch = owners.TryGetValue(
                    receipt.OperationId,
                    out ResourceStockPolicyPendingSale owner)
                && Matches(owner, receipt);
            bool rejectedMatch = rejectedOwners.TryGetValue(
                    receipt.OperationId,
                    out QualityRejectedSalePending rejectedOwner)
                && QualityRejectedSaleOutbox.ReceiptMatchesSaved(
                    rejectedOwner,
                    receipt);
            if (!genericMatch && !rejectedMatch)
            {
                throw new InvalidOperationException(
                    $"Incoming stock-policy physical Transfer '{receipt.OperationId}' has no exact sale owner.");
            }
        }
    }

    private void ValidateLocalPayload(
        DungeonResourceStockPolicySaveData payload)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        ResourceStockPolicySaveValidation.Validate(payload, catalog, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Stock-policy restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }
    }

    private static bool Matches(
        ResourceStockPolicyPendingSale owner,
        PhysicalItemRestoreCandidateDispositionSnapshot receipt) =>
        owner != null
        && receipt != null
        && receipt.Kind == PhysicalItemDispositionKind.Transfer
        && string.Equals(
            receipt.OperationId,
            owner.operationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            owner.reasonCode,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.CommitId,
            owner.commitId,
            StringComparison.Ordinal)
        && receipt.Quantity == owner.quantity
        && receipt.InputMassGrams == owner.inputMassGrams
        && (receipt.SourceStackIds ?? Array.Empty<string>())
            .SequenceEqual(
                owner.sourceStackIds ?? new List<string>(),
                StringComparer.Ordinal);
}
