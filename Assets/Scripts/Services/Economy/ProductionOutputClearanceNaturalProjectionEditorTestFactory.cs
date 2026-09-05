#if UNITY_EDITOR
using System.Collections.Generic;

/// <summary>
/// Editor-only bridge for constructing immutable clearance authority records in
/// focused tests without widening their production constructors.
/// </summary>
public static class ProductionOutputClearanceNaturalProjectionEditorTestFactory
{
    public static ProductionOutputClearanceMeasurementCandidate CreateCandidate(
        ProductionOutputClearanceMeasurementSourceBranch source,
        string measurementCapabilityId,
        string contributorId,
        int contributorContractVersion) => new(
        source,
        measurementCapabilityId,
        contributorId,
        contributorContractVersion);

    public static ProductionOutputClearanceMeasurementPlan CreatePlan(
        string definitionId,
        string workstationTag,
        IReadOnlyList<ProductionOutputClearanceMeasurementCandidate> candidates,
        string registryFingerprint,
        string contextSourceDigest) => new(
        definitionId,
        workstationTag,
        candidates,
        registryFingerprint,
        contextSourceDigest);

    public static ProductionOutputClearanceMeasurementFixture CreateFixture(
        ProductionOutputClearanceMeasurementPlan plan,
        int seedIndex,
        int deterministicSeed) => new(plan, seedIndex, deterministicSeed);

    public static ProductionOutputClearanceExecutableDescriptor CreateDescriptor(
        ProductionOutputClearanceMeasurementPlan plan,
        string facilitySourceDigest,
        int outputBufferCycleCapacity,
        IProductionOutputClearanceExecutablePayload payload) => new(
        plan,
        facilitySourceDigest,
        outputBufferCycleCapacity,
        payload);
}
#endif
