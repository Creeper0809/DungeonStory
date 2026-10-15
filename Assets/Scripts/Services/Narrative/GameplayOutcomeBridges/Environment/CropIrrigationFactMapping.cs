using System;

public static class CropIrrigationFactMapping
{
    public static CropIrrigationSupplyOutcomeReceipt Capture(
        CropIrrigationSuppliedFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        CropIrrigationPublicationSaveData source = fact.Snapshot();
        var assessment = new CropIrrigationAssessment(
            CropIrrigationStatus.Available,
            new BuildingInstanceId(source.plotId),
            new BuildingInstanceId(source.irrigatorId),
            source.quantity,
            WorldWaterQuality.Clean,
            string.Empty);
        var result = new CropIrrigationSupplyResult(
            assessment,
            true,
            source.quantity,
            WorldWaterQuality.Clean);
        return EnvironmentOutcomeReceiptFactory.CreateCropIrrigationSupply(
            result,
            source.operationId,
            checked((long)source.operationSequence + 1L),
            source.plotDisplayName,
            source.irrigatorDisplayName,
            new CoreGridCell(source.locationX, source.locationY),
            source.absoluteDay,
            source.waterBefore,
            source.waterAfter,
            source.networkId,
            source.networkWaterBefore,
            source.networkWaterAfter);
    }
}
