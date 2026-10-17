using System;

internal static class CropPlotOperationIdentity
{
    internal const string WaterRefillOperationPrefix = "crop-water-refill:";
    internal const string IrrigationOperationPrefix = "crop-irrigation:";
    internal const string CropSelectionOperationPrefix = "crop-selection:";
    internal const string GoldenHarvestScheduleOperationPrefix =
        "golden-harvest-schedule:";
    internal const string HarvestOutputBatchCommitPrefix =
        ProductionDomainOutputPublicationIdentity.BatchCommitPrefix
        + "crop-harvest:";
    internal const string HarvestOutputPublicationOperationPrefix =
        ProductionDomainOutputPublicationIdentity.PublicationOperationPrefix
        + "crop-harvest:";

    internal static string FormatHarvestOperationId(
        BuildingInstanceId plotId,
        int sequence)
    {
        if (!plotId.IsValid || sequence < 0)
            throw new ArgumentException(
                "A valid crop plot and non-negative harvest sequence are required.");
        return "crop-harvest:"
            + plotId.Value + ":"
            + sequence.ToString(
                "D6",
                System.Globalization.CultureInfo.InvariantCulture);
    }

    internal static string FormatWaterRefillOperationId(
        BuildingInstanceId plotId,
        int sequence)
    {
        if (!plotId.IsValid || sequence < 0)
            throw new ArgumentException(
                "A valid crop plot and non-negative water-refill sequence are required.");
        return WaterRefillOperationPrefix
            + plotId.Value + ":"
            + sequence.ToString(
                "D6",
                System.Globalization.CultureInfo.InvariantCulture);
    }
}
