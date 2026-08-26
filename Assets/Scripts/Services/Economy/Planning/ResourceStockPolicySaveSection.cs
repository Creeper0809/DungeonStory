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

    public ResourceStockPolicySaveSection(
        IResourceStockPolicyRuntime runtime,
        IResourceEconomyContentCatalog catalog,
        IPhysicalItemRestoreCandidateQuery physicalCandidates)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
        this.physicalCandidates = physicalCandidates
            ?? throw new ArgumentNullException(nameof(physicalCandidates));
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
        return runtime.PrepareRestoreCandidate(payload);
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
        if (payload?.pendingSales == null)
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

        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 physicalCandidates.PendingBatchDispositions)
        {
            if (receipt?.OperationId == null
                || !receipt.OperationId.StartsWith(
                    ResourceStockPolicySaleOutbox.OperationPrefix,
                    StringComparison.Ordinal))
            {
                continue;
            }
            if (!owners.TryGetValue(
                    receipt.OperationId,
                    out ResourceStockPolicyPendingSale owner)
                || !Matches(owner, receipt))
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
