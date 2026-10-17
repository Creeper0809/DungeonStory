public enum CharacterOperationBlockAxis
{
    Unknown = 0,
    Power,
    Water,
    Fuel,
    Cleanliness,
    Environment,
    Tool,
    Access,
    Identity,
    Risk,
    Materials,
    OutputSpace,
    Facility
}
internal static class CharacterOperationBlockClassification
{
    internal static CharacterOperationBlockAxis Classify(
        DomainFailure failure,
        AIActionFailureKind failureKind)
    {
        if (failure.IsFailure)
        {
            switch (failure.Code)
            {
                case FailureCode.PowerConsumerUnavailable:
                case FailureCode.PowerBreakerUnavailable:
                case FailureCode.AutomationUnpowered:
                case FailureCode.DefensePowerUnavailable:
                case FailureCode.ServiceSupportUnpowered:
                case FailureCode.TemporalStasisPowerInsufficient:
                    return CharacterOperationBlockAxis.Power;
                case FailureCode.SurvivalWaterSourceUnsupported:
                case FailureCode.SurvivalWaterFrozen:
                case FailureCode.FluidNetworkUnavailable:
                case FailureCode.FluidInsufficientWater:
                case FailureCode.FluidManualWaterUnavailable:
                case FailureCode.FluidWastewaterUnavailable:
                    return CharacterOperationBlockAxis.Water;
                case FailureCode.SurvivalFuelStockMissing:
                    return CharacterOperationBlockAxis.Fuel;
                case FailureCode.EnvironmentWorkTargetUnavailable:
                case FailureCode.EnvironmentColdWorkCooldownActive:
                case FailureCode.EnvironmentProtectionInsufficient:
                case FailureCode.EnvironmentExposureCritical:
                case FailureCode.SurgeryEnvironmentUnsafe:
                    return CharacterOperationBlockAxis.Environment;
                case FailureCode.EquipmentDefinitionMissing:
                case FailureCode.EquipmentInstanceMissing:
                case FailureCode.EquipmentModuleMissing:
                case FailureCode.EquipmentOrModuleMissing:
                case FailureCode.EnvironmentWorkwearDefinitionMissing:
                case FailureCode.EnvironmentWorkwearNotEquipped:
                case FailureCode.EnvironmentWorkwearPhysicalItemMissing:
                case FailureCode.ChildSafetyProtectiveEquipmentRequired:
                    return CharacterOperationBlockAxis.Tool;
                case FailureCode.EnvironmentWorkwearLockerUnreachable:
                case FailureCode.ProductionDistributionRouteUnavailable:
                case FailureCode.ConveyorRouteUnavailable:
                case FailureCode.ConveyorDestinationUnavailable:
                case FailureCode.CharacterMedicalDestinationUnavailable:
                    return CharacterOperationBlockAxis.Access;
                case FailureCode.WorkOrderWorkerIneligible:
                case FailureCode.ProductionBillReservedByOtherWorker:
                case FailureCode.ConveyorTransitOwnershipMismatch:
                case FailureCode.SurgeryPreferredDoctorOnly:
                case FailureCode.SurgeryReservedDoctorMismatch:
                case FailureCode.ItemReservationSignatureMismatch:
                    return CharacterOperationBlockAxis.Identity;
                case FailureCode.ProductionMaterialsMissing:
                case FailureCode.SurvivalFoodStockMissing:
                case FailureCode.SurvivalTreatmentMaterialMissing:
                case FailureCode.SurgeryMaterialUnavailable:
                    return CharacterOperationBlockAxis.Materials;
                case FailureCode.ProductionOutputSpaceUnavailable:
                case FailureCode.WarehouseMassCapacityUnavailable:
                    return CharacterOperationBlockAxis.OutputSpace;
                case FailureCode.ProductionFacilityMissing:
                case FailureCode.AutomationFacilityUnavailable:
                case FailureCode.SurgeryFacilityUnavailable:
                case FailureCode.CharacterMedicalFacilityUnavailable:
                    return CharacterOperationBlockAxis.Facility;
            }
        }

        return failureKind switch
        {
            AIActionFailureKind.NoPath or
                AIActionFailureKind.NoGrid or
                AIActionFailureKind.DestinationOccupied =>
                CharacterOperationBlockAxis.Access,
            AIActionFailureKind.FacilityAdmissionRejected or
                AIActionFailureKind.FacilityServiceUnavailable or
                AIActionFailureKind.Destroyed =>
                CharacterOperationBlockAxis.Facility,
            _ => CharacterOperationBlockAxis.Unknown
        };
    }
}
