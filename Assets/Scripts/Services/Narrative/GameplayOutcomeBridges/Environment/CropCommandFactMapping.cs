using System;

public static class CropCommandFactMapping
{
    public static CropSelectionCommandReceipt Capture(
        CropSelectionChangedFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));
        CropSelectionCommandSaveData source = fact.Snapshot();
        return EnvironmentOutcomeReceiptFactory.CreateCropSelection(
            source,
            fact.PlotId,
            source.plotDisplayName,
            source.cropDisplayName,
            new CoreGridCell(source.locationX, source.locationY));
    }

    public static GoldenHarvestScheduleReceipt Capture(
        GoldenHarvestScheduledFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));
        CropGoldenHarvestScheduleSaveData source = fact.Snapshot();
        return EnvironmentOutcomeReceiptFactory.CreateGoldenHarvestSchedule(
            source,
            fact.PlotId,
            source.plotDisplayName,
            source.cropDisplayName,
            new CoreGridCell(source.locationX, source.locationY));
    }
}
