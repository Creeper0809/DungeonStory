using System;

public sealed class CropCycleTerminalFact
{
    private readonly CropCycleExecutionReceiptSaveData snapshot;

    public CropCycleTerminalFact(
        BuildingInstanceId plotId,
        CropCycleExecutionReceiptSaveData terminal,
        string plotDisplayName,
        string cropDisplayName,
        int absoluteDay,
        long ownerRevision)
    {
        if (terminal == null)
            throw new ArgumentNullException(nameof(terminal));

        CropPlanExecutionReceiptAuthority.ValidateTerminal(terminal);
        if (!plotId.IsValid
            || !string.Equals(
                terminal.plotId,
                plotId.Value,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Crop-cycle terminal plot identity does not match the fact plot.",
                nameof(plotId));
        }
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        if (ownerRevision <= 0L)
            throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        if (terminal.status == CropCycleExecutionReceiptStatus.Completed
            && ownerRevision != checked((long)terminal.harvestOperationSequence + 1L))
        {
            throw new ArgumentException(
                "Completed crop-cycle owner revision must follow the harvest sequence.",
                nameof(ownerRevision));
        }

        PlotId = plotId;
        PlotDisplayName = FreezeDisplayName(
            plotDisplayName,
            nameof(plotDisplayName));
        CropDisplayName = FreezeDisplayName(
            cropDisplayName,
            nameof(cropDisplayName));
        if (string.Equals(CropDisplayName, terminal.cropId, StringComparison.Ordinal))
            throw new ArgumentException("Crop display name cannot be its internal identity.", nameof(cropDisplayName));
        AbsoluteDay = absoluteDay;
        OwnerRevision = ownerRevision;
        snapshot = terminal.DeepClone();
    }

    public BuildingInstanceId PlotId { get; }
    public string PlotDisplayName { get; }
    public string CropDisplayName { get; }
    public int AbsoluteDay { get; }
    public long OwnerRevision { get; }

    public CropCycleExecutionReceiptSaveData Snapshot() => snapshot.DeepClone();

    private static string FreezeDisplayName(string value, string paramName)
    {
        string frozen = value?.Trim() ?? string.Empty;
        if (frozen.Length == 0)
            throw new ArgumentException("Display name is required.", paramName);
        return frozen;
    }

}
