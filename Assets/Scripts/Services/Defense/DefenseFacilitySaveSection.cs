using System;

public sealed class DefenseFacilityPersistenceAdapter :
    IDefenseFacilityPersistence
{
    private readonly DefenseFacilityRuntime runtime;

    public DefenseFacilityPersistenceAdapter(DefenseFacilityRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public DefenseFacilitySaveData CaptureState() => runtime.CaptureState();

    public DefenseFacilityRestoreCandidate PrepareRestoreState(
        DefenseFacilitySaveData data) =>
        runtime.PrepareRestoreState(data);

    public void PublishRestoreState(DefenseFacilityRestoreCandidate candidate) =>
        runtime.PublishRestoreState(candidate);
}
