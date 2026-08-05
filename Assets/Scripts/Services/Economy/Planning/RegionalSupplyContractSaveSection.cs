using System;
using System.Collections.Generic;

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

    public RegionalSupplyContractSaveSection(
        IRegionalSupplyContractRuntime runtime,
        IResourceEconomyContentCatalog catalog)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
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
        return runtime.PrepareRestoreCandidate(payload);
    }

    protected override void PublishRestoreCandidate(
        RegionalSupplyContractRestoreCandidate candidate) =>
        runtime.PublishRestoreCandidate(candidate);
}
