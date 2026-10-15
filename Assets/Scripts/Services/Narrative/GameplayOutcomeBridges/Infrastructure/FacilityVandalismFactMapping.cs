using System;

public static class FacilityVandalismFactMapping
{
    public static FacilityVandalismOutcomeReceipt Capture(FacilityVandalismFact fact)
    {
        var value = fact.Snapshot();
        return new FacilityVandalismOutcomeReceipt(new BuildingInstanceId(value.facilityId), value.operationSequence,
            value.actorId, value.actorName, value.facilityName, value.buildingDefinitionId,
            value.source == "macro" ? FacilityVandalismSourceKind.Macro : FacilityVandalismSourceKind.Deprivation,
            value.reason, value.absoluteDay, value.positionX, value.positionY, value.damagedBefore, value.damagedAfter,
            value.hasStructuralIntegrity, value.structuralHitPointsBefore, value.structuralHitPointsAfter,
            value.witnessPublicationKeys);
    }
}
