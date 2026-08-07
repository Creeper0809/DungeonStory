using System;

public enum FailureCode
{
    None = 0,
    RequiredResearchUnavailable,
    EquipmentDefinitionMissing,
    EquipmentInstanceMissing,
    EquipmentModuleMissing,
    EquipmentOrModuleMissing,
    ModuleNotUnidentified,
    ModuleNotRestorable,
    ModuleNotTunable,
    ModuleSlotMissing,
    ModuleSlotEmpty,
    EquipmentLineageMismatch,
    ModuleLineageMismatch,
    ModuleNeedsRestoration,
    ModuleNeedsRuneTuning,
    ModuleAlreadyAttached,
    HistorySourceHasModules,
    HistoryTransferAlreadyActive,
    LineageSealMissing,
    HistoryTransferOrderMissing,
    HistoryTransferEquipmentMissing,
    HistoryTransferSealMissing,
    EquipmentProgressionFacilityUnavailable,

    // Core-session / external influence commands.
    HostileRumorUnavailable,
    OperatingDayNotStarted,
    RumorMitigationAlreadyUsed,
    InsufficientRenown,
    InsufficientGold,
    DreadDefenseAlreadyArmed,
    InsufficientDread,
    ExpeditionSiteIdMissing,
    InsufficientScoutingLabor,
    TrailCharmMissing,
    ExpeditionSiteExpired,
    ExternalPaymentRejected,
    OffenseTargetUnknown,
    ExternalInfluenceUnavailable,

    // Core-session / service-room commands.
    ServiceClosed,
    ServiceProcessIdMissing,
    ServiceHubUnavailable,
    ServiceCapacityFull,
    ServiceActorAlreadyActive,
    ServiceProcessContractMissing,
    ServiceSessionMissing,
    ServiceStageNotAllowed,
    ServiceStageIncomplete,
    ServiceModeUnsupported,
    ServiceFeatureMissing,
    ServiceSupportUnpowered,

    // Environment field, evacuation, and protective workwear commands.
    EnvironmentThermostatUnsupported,
    EnvironmentEvacuationContextInvalid,
    EnvironmentEvacuationCellUnavailable,
    EnvironmentWorkwearCharacterMissing,
    EnvironmentWorkwearDefinitionMissing,
    EnvironmentWorkwearSpeciesIncompatible,
    EnvironmentWorkwearResearchLocked,
    EnvironmentWorkwearStockMissing,
    EnvironmentWorkwearTransferFailed,
    EnvironmentWorkwearInstanceIdMissing,
    EnvironmentWorkwearLockerUnreachable,
    EnvironmentWorkwearNotEquipped,
    EnvironmentWorkwearPhysicalItemMissing,
    EnvironmentWorkwearProductionContextInvalid,
    EnvironmentWorkwearOutputSpawnFailed,
    EnvironmentWorkTargetUnavailable,
    EnvironmentColdWorkCooldownActive,
    EnvironmentProtectionInsufficient,
    EnvironmentExposureCritical,

    // Medical / surgery commands and planning.
    SurgerySubjectInvalid,
    SurgeryProcedureMissing,
    SurgerySubjectMaintenanceOnly,
    SurgeryPreferredDoctorInvalid,
    SurgerySelfOperationForbidden,
    SurgerySubjectAlreadyScheduled,
    SurgeryFacilityMissing,
    SurgeryFacilityUnavailable,
    SurgeryOrderMissing,
    SurgeryPartUnavailable,
    SurgeryPartKindMismatch,
    SurgeryPartNodeMismatch,
    SurgerySubjectKindUnsupported,
    SurgeryAnatomyFamilyUnsupported,
    SurgerySpeciesUnsupported,
    SurgeryConstructProcedureRequired,
    SurgeryConstructProcedureBiologicalMismatch,
    SurgeryTargetNodeMissing,
    SurgeryResearchStateUnavailable,
    SurgeryResearchIncomplete,
    SurgeryCorpseMissing,
    SurgeryCorpseStale,
    SurgeryNodeAlreadyExtracted,
    SurgeryLivingSubjectUnavailable,
    SurgeryWildlifeSubjectUnavailable,
    SurgeryTargetNodeUnavailable,
    SurgeryOperatorMissing,
    SurgeryOperatorStatInsufficient,
    SurgeryOperatorSkillInsufficient,
    SurgeryOperatorIneligible,
    SurgeryStaffOnly,
    SurgeryPreferredDoctorOnly,
    SurgeryDoctorAlreadyAssigned,
    SurgeryFacilityOrProcedureMissing,
    SurgeryReservedDoctorMismatch,
    SurgeryMaterialUnavailable,
    SurgeryEffectHandlerMissing,
    SurgeryEffectFailed,
    SurgeryTransportOrderMissing,
    SurgeryTransportCarrierMismatch,
    SurgeryTransportUnavailable,
    SurgeryExtractionAlreadyRecorded,
    SurgeryEnvironmentUnsafe,
    SurgeryOutcomeFailed,

    // Character medical rescue, treatment, and restore projection.
    CharacterMedicalRuntimeUnavailable,
    CharacterMedicalRescuerUnavailable,
    CharacterMedicalPatientUnavailable,
    CharacterMedicalParticipantsInvalid,
    CharacterMedicalOrderUnavailable,
    CharacterMedicalOrderMissing,
    CharacterMedicalOrderCreationFailed,
    CharacterMedicalNoTreatableInjury,
    CharacterMedicalAmbulatoryTreatmentUnsupported,
    CharacterMedicalFacilityUnavailable,
    CharacterMedicalFacilityReserved,
    CharacterMedicalStabilizationRequired,
    CharacterMedicalBedUnavailable,
    CharacterMedicalDestinationUnavailable,
    CharacterMedicalReservationMismatch,
    CharacterMedicalProjectionPositionInvalid,
    CharacterMedicalProjectionGridOccupied,

    // Character-species maintenance commands.
    CharacterSpeciesStateUnavailable,
    CharacterSpeciesRechargeUnsupported,
    CharacterSpeciesRepairUnsupported,

    // Survival facility work commands.
    SurvivalTargetFacilityMissing,
    SurvivalWorkUnsupported,
    SurvivalWaterSourceUnsupported,
    SurvivalWaterFrozen,
    SurvivalOutputUnavailable,
    SurvivalCookingUnsupported,
    SurvivalFoodStockMissing,
    SurvivalFuelStockMissing,
    SurvivalTreatmentUnsupported,
    SurvivalTreatmentTargetMissing,
    SurvivalTreatmentMaterialMissing,
    SurvivalRefuelUnsupported,

    // Industrial infrastructure commands and item transit.
    IndustrialBuildingUnavailable,
    IndustrialCommandInvalid,
    PowerConsumerUnavailable,
    PowerBreakerUnavailable,
    FluidNetworkUnavailable,
    FluidInsufficientWater,
    FluidManualWaterUnavailable,
    FluidWastewaterUnavailable,
    FluidMaintenanceUnavailable,
    ConveyorStackUnavailable,
    ConveyorStackReserved,
    ConveyorStackOutOfRange,
    ConveyorPortUnavailable,
    ConveyorPortFull,
    ConveyorFilterMismatch,
    ConveyorPayloadMissing,
    ConveyorRouteUnavailable,
    ConveyorOverflowApprovalRequired,
    ConveyorTransitOwnershipMismatch,
    ConveyorDestinationUnavailable,
    AutomationFacilityUnavailable,
    AutomationModeUnsupported,
    AutomationMaintenanceRequired,
    AutomationUnpowered,
    AutomationFaulted,

    // Production bills, physical production logistics, and waste processing.
    ProductionFacilityMissing,
    ProductionRecipeMissing,
    ProductionWorkstationMismatch,
    ProductionResearchLocked,
    ProductionSupportUnavailable,
    ProductionStockSensorRequired,
    ProductionBillMissing,
    ProductionBillUnavailable,
    ProductionBillReservedByOtherWorker,
    ProductionMaterialsMissing,
    ProductionUtilitiesUnavailable,
    ProductionOutputUnavailable,
    ProductionOutputSpaceUnavailable,
    ProductionDistributionRouteUnavailable,
    ProductionProcessingActive,
    ProductionTargetStockSatisfied,
    ProductionBatchRuined,
    ProductionWorkstationMissing,
    ItemTransferStackUnavailable,
    ItemTransferDestinationMissing,
    ItemTransferRequestFailed,
    ItemTransferConsumptionFailed,
    WastePolicyInvalid,
    WastePolicyUnsupported,
    WasteFeedUnavailable,
    WasteFeedDeliveryFailed,
    WasteFeedBufferUnavailable,

    // Defense-facility activation, supply, and maintenance commands.
    DefenseFacilityUnavailable,
    DefenseManualActivationRequired,
    DefenseTargetDisallowed,
    DefenseConditionCritical,
    DefensePowerUnavailable,
    DefenseAutomaticControlUnavailable,
    DefenseCooldownActive,
    DefenseMechanicalJam,
    DefensePartialMisfire,
    DefensePhysicalSupplyUnsupported,
    DefenseSupplyCapacityFull,
    DefenseSupplyDeliveryPending,
    DefenseSupplyUnavailable,
    DefenseNotJammed,
    DefenseMaintenanceDeliveryPending,
    DefenseMaintenancePartMissing,
    DefenseRepairAmountInvalid,
    DefenseDestroyed,
    DefenseTriggerUnsupported,

    // V19 child-safety routing and supervised apprenticeship.
    ChildSafetyLifeStateUnavailable,
    ChildSafetyWorkForbidden,
    ChildSafetyCombatForbidden,
    ChildSafetyApprenticeshipDisabled,
    ChildSafetyCharacterPermissionRequired,
    ChildSafetyWorkConfirmationRequired,
    ChildSafetyProtectiveEquipmentRequired,
    ChildSafetySupervisorUnavailable,
    ChildSafetySupervisorTooFar,
    ChildSafetyAuthorizationInvalid,
    ChildSafetyHazardEscapeDirectionInvalid,

    // V19 physical population-health and crop-treatment commands.
    PopulationHealthCharacterMissing,
    VaccineDefinitionMissing,
    VaccineDiseaseMismatch,
    VaccineDoseUnavailable,
    CropTreatmentDefinitionMissing,
    CropTreatmentKindUnsupported,
    CropTreatmentPlotMissing,
    CropTreatmentSupplyUnavailable,
    AgeTreatmentCharacterMissing,
    AgeTreatmentDefinitionMissing,
    AgeTreatmentProcedureMismatch,
    AgeTreatmentSupplyUnavailable,
    AgeTreatmentAnatomyUnavailable,
    AgeTreatmentTooYoung,
    AgeTreatmentCooldownActive,
    TemporalStasisFacilityMissing,
    TemporalStasisPowerInsufficient,
    TemporalStasisMaintenanceUnavailable
}

/// <summary>
/// Localization-neutral domain failure. Parameters are stable IDs or scalar
/// values; presentation resolves the code through a String Table.
/// </summary>
[Serializable]
public readonly struct DomainFailure : IEquatable<DomainFailure>
{
    private readonly string[] parameters;

    public DomainFailure(FailureCode code, params string[] parameters)
    {
        Code = code;
        this.parameters = parameters == null || parameters.Length == 0
            ? Array.Empty<string>()
            : (string[])parameters.Clone();
    }

    public FailureCode Code { get; }
    public ReadOnlySpan<string> Parameters => parameters ?? Array.Empty<string>();
    public bool IsFailure => Code != FailureCode.None;

    public static DomainFailure None => new DomainFailure(FailureCode.None);

    public bool Equals(DomainFailure other)
    {
        if (Code != other.Code || Parameters.Length != other.Parameters.Length)
        {
            return false;
        }
        for (int index = 0; index < Parameters.Length; index++)
        {
            if (!string.Equals(
                    Parameters[index],
                    other.Parameters[index],
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    public override bool Equals(object obj) =>
        obj is DomainFailure other && Equals(other);

    public override int GetHashCode()
    {
        HashCode hash = new HashCode();
        hash.Add(Code);
        foreach (string parameter in Parameters)
        {
            hash.Add(parameter, StringComparer.Ordinal);
        }
        return hash.ToHashCode();
    }
}
