using System.Collections.Generic;

internal static class CharacterMedicalOrderPersistence
{
    public static readonly IReadOnlyDictionary<StockCategory, int> MedicineCost =
        new Dictionary<StockCategory, int>
        {
            [StockCategory.Medicine] = 1
        };

    public static readonly IReadOnlyDictionary<StockCategory, int> ExtractedBloodCost =
        new Dictionary<StockCategory, int>
        {
            [StockCategory.Biological] = 1
        };

    public static CharacterMedicalOrder Clone(CharacterMedicalOrder source)
    {
        return new CharacterMedicalOrder
        {
            orderId = source.orderId ?? string.Empty,
            patientId = source.patientId ?? string.Empty,
            rescuerId = source.rescuerId ?? string.Empty,
            treatmentFacilityId = source.treatmentFacilityId ?? string.Empty,
            state = source.state,
            stabilized = source.stabilized,
            carried = source.carried,
            requiredStabilizationWork = source.requiredStabilizationWork,
            completedStabilizationWork = source.completedStabilizationWork,
            requiredTreatmentWork = source.requiredTreatmentWork,
            completedTreatmentWork = source.completedTreatmentWork,
            treatmentSupply = source.treatmentSupply,
            treatmentSupplyConsumed = source.treatmentSupplyConsumed,
            treatmentSupplyDeliveryRequested = source.treatmentSupplyDeliveryRequested,
            treatmentItemId = source.treatmentItemId ?? string.Empty,
            treatmentPotency = source.treatmentPotency,
            treatmentInfectionReduction = source.treatmentInfectionReduction,
            treatmentPainReduction = source.treatmentPainReduction,
            treatmentMaterialDestinationId =
                source.treatmentMaterialDestinationId ?? string.Empty,
            patientX = source.patientX,
            patientY = source.patientY,
            bedX = source.bedX,
            bedY = source.bedY,
            statusCode = source.statusCode,
            statusParameters = source.statusParameters != null
                ? new List<string>(source.statusParameters)
                : new List<string>()
        };
    }
}
