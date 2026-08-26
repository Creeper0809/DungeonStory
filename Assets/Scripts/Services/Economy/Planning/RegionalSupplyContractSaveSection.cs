using System;
using System.Collections.Generic;
using System.Linq;

// V18 required section: validation succeeds before the candidate Aggregate root is replaced.
public sealed class RegionalSupplyContractSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonRegionalSupplyContractSaveData,
        RegionalSupplyContractRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "economy.regional-contracts";

    private static readonly string[] Dependencies =
    {
        PhysicalItemsSaveSection.Id,
        ResourceStockPolicySaveSection.Id
    };

    private readonly IRegionalSupplyContractRuntime runtime;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;

    public RegionalSupplyContractSaveSection(
        IRegionalSupplyContractRuntime runtime,
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
        DungeonRegionalSupplyContractSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonRegionalSupplyContractSaveData CapturePayload()
    {
        return runtime.Capture();
    }

    protected override RegionalSupplyContractRestoreCandidate
        BuildRestoreCandidate(DungeonRegionalSupplyContractSaveData payload)
    {
        ValidateLocalPayload(payload);
        ValidatePhysicalRestoreCandidate(payload, physicalCandidates);
        return runtime.PrepareRestoreCandidate(payload);
    }

    protected override void ValidateParsedPayload(
        DungeonRegionalSupplyContractSaveData payload)
    {
        ValidateLocalPayload(payload);
        _ = runtime.PrepareRestoreCandidate(payload);
    }

    protected override void PublishRestoreCandidate(
        RegionalSupplyContractRestoreCandidate candidate) =>
        runtime.PublishRestoreCandidate(candidate);

    public static void ValidatePhysicalRestoreCandidate(
        DungeonRegionalSupplyContractSaveData payload,
        IPhysicalItemRestoreCandidateQuery physicalCandidates)
    {
        if (payload?.contracts == null)
        {
            throw new InvalidOperationException(
                "Regional-contract physical restore join has no contract payload.");
        }
        if (physicalCandidates == null
            || !physicalCandidates.IsCandidateAvailable)
        {
            throw new InvalidOperationException(
                "Regional-contract restore requires the incoming physical-item candidate.");
        }

        Dictionary<string, RegionalSupplyContractState> owners = payload.contracts
            .Where(RegionalSupplyContractDeliveryOutbox.HasPending)
            .ToDictionary(
                contract => contract.deliveryOperationId,
                StringComparer.Ordinal);
        foreach (KeyValuePair<string, RegionalSupplyContractState> pair in owners)
        {
            if (!physicalCandidates.TryGetPendingBatchDisposition(
                    pair.Key,
                    out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || !Matches(pair.Value, receipt))
            {
                throw new InvalidOperationException(
                    $"Regional-contract pending delivery '{pair.Key}' has no exact incoming physical Transfer receipt.");
            }
        }

        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 physicalCandidates.PendingBatchDispositions)
        {
            if (receipt?.OperationId == null
                || !receipt.OperationId.StartsWith(
                    "regional-supply-transfer:",
                    StringComparison.Ordinal))
            {
                continue;
            }
            if (!owners.TryGetValue(
                    receipt.OperationId,
                    out RegionalSupplyContractState owner)
                || !Matches(owner, receipt))
            {
                throw new InvalidOperationException(
                    $"Incoming regional-supply physical Transfer '{receipt.OperationId}' has no exact contract owner.");
            }
        }
    }

    private void ValidateLocalPayload(
        DungeonRegionalSupplyContractSaveData payload)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        RegionalSupplyContractSaveValidation.Validate(
            payload,
            catalog,
            report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Regional-contract restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }
    }

    private static bool Matches(
        RegionalSupplyContractState owner,
        PhysicalItemRestoreCandidateDispositionSnapshot receipt) =>
        owner != null
        && receipt != null
        && receipt.Kind == PhysicalItemDispositionKind.Transfer
        && string.Equals(
            receipt.OperationId,
            owner.deliveryOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            RegionalSupplyContractDeliveryOutbox.TransferReason,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.CommitId,
            owner.deliveryCommitId,
            StringComparison.Ordinal)
        && receipt.Quantity == owner.deliveryQuantity
        && receipt.InputMassGrams == owner.deliveryMassGrams
        && (receipt.SourceStackIds ?? Array.Empty<string>())
            .SequenceEqual(
                owner.deliverySourceStackIds ?? new List<string>(),
                StringComparer.Ordinal);
}
