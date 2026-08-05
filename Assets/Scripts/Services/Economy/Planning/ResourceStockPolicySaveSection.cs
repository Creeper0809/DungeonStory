using System;
using System.Collections.Generic;

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

    public ResourceStockPolicySaveSection(
        IResourceStockPolicyRuntime runtime,
        IResourceEconomyContentCatalog catalog)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
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
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        ResourceStockPolicySaveValidation.Validate(payload, catalog, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Stock-policy restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }
        return runtime.PrepareRestoreCandidate(payload);
    }

    protected override void PublishRestoreCandidate(
        ResourceStockPolicyRestoreCandidate candidate) =>
        runtime.PublishRestoreCandidate(candidate);
}
