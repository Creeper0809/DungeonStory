#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

public static class DomainFailureLocalizationAssetBuilder
{
    private const string CollectionName = "DomainFailures";
    private const string EnglishLocalePath = "Assets/Localization/Locale_en.asset";
    private const string EnglishTablePath =
        "Assets/Localization/DomainFailures_en.asset";

    private static readonly IReadOnlyDictionary<string, string> English =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AgeTreatmentAnatomyUnavailable"] = "The required anatomy is unavailable. Character: {0}, condition: {1}, anatomy: {2}",
            ["AgeTreatmentCharacterMissing"] = "The age-treatment character is unavailable. Character: {0}",
            ["AgeTreatmentCooldownActive"] = "Rejuvenation is still on cooldown. Character: {0}, remaining days: {1}",
            ["AgeTreatmentDefinitionMissing"] = "The age-treatment definition is missing. Treatment: {0}",
            ["AgeTreatmentProcedureMismatch"] = "The treatment does not match its medical procedure. Treatment: {0}, procedure: {1}",
            ["AgeTreatmentSupplyUnavailable"] = "Age-treatment supplies are unavailable. Facility: {0}, treatment: {1}, reason: {2}",
            ["AgeTreatmentTooYoung"] = "The character is too young for rejuvenation. Character: {0}, minimum age: {1}",
            ["ChildSafetyApprenticeshipDisabled"] = "Supervised apprenticeship is disabled. Character: {0}",
            ["ChildSafetyAuthorizationInvalid"] = "The child-safety authorization is invalid.",
            ["ChildSafetyCharacterPermissionRequired"] = "This adolescent needs explicit apprenticeship permission. Character: {0}",
            ["ChildSafetyCombatForbidden"] = "Children cannot enter combat or combat-supply routes.",
            ["ChildSafetyHazardEscapeDirectionInvalid"] = "A child inside a hazard may only move toward a strictly safer cell.",
            ["ChildSafetyLifeStateUnavailable"] = "The character life state is unavailable. Character: {0}",
            ["ChildSafetyProtectiveEquipmentRequired"] = "Required protective equipment is missing. Character: {0}",
            ["ChildSafetySupervisorTooFar"] = "The adult supervisor is farther than six cells. Supervisor: {0}",
            ["ChildSafetySupervisorUnavailable"] = "The adult supervisor is unavailable. Supervisor: {0}",
            ["ChildSafetyWorkConfirmationRequired"] = "This apprenticeship work requires explicit confirmation. Character: {0}",
            ["ChildSafetyWorkForbidden"] = "This life stage cannot perform the requested work. Character: {0}",
            ["CropTreatmentDefinitionMissing"] = "The crop-treatment definition is missing. Treatment: {0}",
            ["CropTreatmentKindUnsupported"] = "The crop treatment has the wrong treatment kind. Treatment: {0}, kind: {1}",
            ["CropTreatmentPlotMissing"] = "The crop plot is unavailable. Plot: {0}",
            ["CropTreatmentSupplyUnavailable"] = "Crop-treatment supplies are unavailable. Plot: {0}, treatment: {1}, reason: {2}",
            ["PopulationHealthCharacterMissing"] = "The population-health character is unavailable. Character: {0}",
            ["TemporalStasisFacilityMissing"] = "The temporal-stasis facility is unavailable. Facility: {0}",
            ["TemporalStasisMaintenanceUnavailable"] = "Temporal-stasis maintenance supplies are unavailable.",
            ["TemporalStasisPowerInsufficient"] = "Temporal stasis requires more rune power. Facility: {0}, required power: {1}",
            ["VaccineDefinitionMissing"] = "The vaccine definition is missing. Vaccine: {0}",
            ["VaccineDiseaseMismatch"] = "The vaccine does not match the requested disease. Vaccine: {0}, disease: {1}",
            ["VaccineDoseUnavailable"] = "A physical vaccine dose is unavailable. Destination: {0}, vaccine: {1}, reason: {2}",
["AlreadyProcessed"] = "Already processed.",
            ["AnesthesiaInProgress"] = "Anesthesia in progress.",
            ["AutomationFacilityUnavailable"] = "The automation facility is unavailable.",
            ["AutomationFaulted"] = "Automation is stopped by a facility fault.",
            ["AutomationMaintenanceRequired"] = "Maintenance is required before automation can resume.",
            ["AutomationModeUnsupported"] = "This facility does not support the selected automation mode.",
            ["AutomationUnpowered"] = "Automation is waiting for power.",
            ["Cancelled"] = "Cancelled.",
            ["CharacterMedicalAmbulatoryTreatmentUnsupported"] = "Ambulatory patients cannot use rescue medical orders. Patient: {0}",
            ["CharacterMedicalBedUnavailable"] = "No treatment bed is available. Order: {0}",
            ["CharacterMedicalDestinationUnavailable"] = "The treatment destination is unavailable. Order: {0}",
            ["CharacterMedicalFacilityReserved"] = "The medical facility is reserved for another patient. Facility: {0}, patient: {1}",
            ["CharacterMedicalFacilityUnavailable"] = "This medical facility cannot receive the patient. Facility: {0}",
            ["CharacterMedicalNoTreatableInjury"] = "The patient has no injury requiring treatment. Patient: {0}",
            ["CharacterMedicalOrderCreationFailed"] = "A treatment order could not be created for the patient. Patient: {0}",
            ["CharacterMedicalOrderMissing"] = "The medical order could not be found. Order: {0}",
            ["CharacterMedicalOrderUnavailable"] = "The medical order is unavailable. Patient or order: {0}",
            ["CharacterMedicalParticipantsInvalid"] = "Character medical participants invalid.",
            ["CharacterMedicalPatientUnavailable"] = "Character medical patient unavailable.",
            ["CharacterMedicalProjectionGridOccupied"] = "The patient's restore position is occupied. Position: ({0}, {1})",
            ["CharacterMedicalProjectionPositionInvalid"] = "The patient's restore position is invalid or duplicated. Patient: {0}, position: ({1}, {2})",
            ["CharacterMedicalRescuerUnavailable"] = "Character medical rescuer unavailable.",
            ["CharacterMedicalReservationMismatch"] = "The rescuer does not own the medical-order reservation. Order: {0}, rescuer: {1}",
            ["CharacterMedicalRuntimeUnavailable"] = "Character medical runtime unavailable.",
            ["CharacterMedicalStabilizationRequired"] = "Field stabilization is required before transport. Order: {0}",
            ["CharacterMedicalStatusAdditionalTreatmentRequired"] = "Character medical status additional treatment required.",
            ["CharacterMedicalStatusAwaitingBed"] = "Character medical status awaiting bed.",
            ["CharacterMedicalStatusAwaitingExtractedBloodDelivery"] = "Character medical status awaiting extracted blood delivery.",
            ["CharacterMedicalStatusAwaitingMedicineDelivery"] = "Character medical status awaiting medicine delivery.",
            ["CharacterMedicalStatusAwaitingRescue"] = "Character medical status awaiting rescue.",
            ["CharacterMedicalStatusAwaitingStabilization"] = "Character medical status awaiting stabilization.",
            ["CharacterMedicalStatusCancelled"] = "Character medical status cancelled.",
            ["CharacterMedicalStatusCarrying"] = "Character medical status carrying.",
            ["CharacterMedicalStatusManualRescueAssigned"] = "Character medical status manual rescue assigned.",
            ["CharacterMedicalStatusMedicineReady"] = "Character medical status medicine ready.",
            ["CharacterMedicalStatusPatientDied"] = "Character medical status patient died.",
            ["CharacterMedicalStatusPatientMissing"] = "Character medical status patient missing.",
            ["CharacterMedicalStatusPatientPathUnavailable"] = "Character medical status patient path unavailable.",
            ["CharacterMedicalStatusPreparingStabilization"] = "Character medical status preparing stabilization.",
            ["CharacterMedicalStatusPreparingTransfer"] = "Character medical status preparing transfer.",
            ["CharacterMedicalStatusRescueInterrupted"] = "Character medical status rescue interrupted.",
            ["CharacterMedicalStatusRescuerDied"] = "Character medical status rescuer died.",
            ["CharacterMedicalStatusRescuerMissing"] = "Character medical status rescuer missing.",
            ["CharacterMedicalStatusReservationReleased"] = "Character medical status reservation released.",
            ["CharacterMedicalStatusRestarted"] = "Character medical status restarted.",
            ["CharacterMedicalStatusStabilizationInterrupted"] = "Character medical status stabilization interrupted.",
            ["CharacterMedicalStatusStabilizedWithInfectionRisk"] = "Character medical status stabilized with infection risk.",
            ["CharacterMedicalStatusStabilizing"] = "Character medical status stabilizing.",
            ["CharacterMedicalStatusSupplyUnavailable"] = "Character medical status supply unavailable.",
            ["CharacterMedicalStatusTreating"] = "Character medical status treating.",
            ["CharacterMedicalStatusTreatingWithExtractedBlood"] = "Character medical status treating with extracted blood.",
            ["CharacterMedicalStatusTreatmentCompleted"] = "Character medical status treatment completed.",
            ["CharacterMedicalStatusTreatmentInterrupted"] = "Character medical status treatment interrupted.",
            ["CharacterMedicalStatusTreatmentPathUnavailable"] = "Character medical status treatment path unavailable.",
            ["CharacterMedicalStatusTreatmentRequested"] = "Character medical status treatment requested.",
            ["CharacterMissing"] = "Character missing.",
            ["CharacterSpeciesRechargeUnsupported"] = "This species cannot be recharged. Species: {0}",
            ["CharacterSpeciesRepairUnsupported"] = "This species cannot receive integrity maintenance. Species: {0}",
            ["CharacterSpeciesStateUnavailable"] = "The character species state is unavailable. Character: {0}",
            ["Completed"] = "Completed.",
            ["CompletedWithMajorFailure"] = "Completed with major failure.",
            ["CompletedWithMinorFailure"] = "Completed with minor failure.",
            ["ConveyorDestinationUnavailable"] = "The conveyor destination is unavailable.",
            ["ConveyorFilterMismatch"] = "The cargo does not match the conveyor filter.",
            ["ConveyorOverflowApprovalRequired"] = "Manual approval is required for overflow discharge.",
            ["ConveyorPayloadMissing"] = "The conveyor payload no longer exists.",
            ["ConveyorPortFull"] = "The conveyor port is full.",
            ["ConveyorPortUnavailable"] = "No compatible conveyor port is available.",
            ["ConveyorRouteUnavailable"] = "No conveyor route is available.",
            ["ConveyorStackOutOfRange"] = "The requested cargo is outside conveyor range.",
            ["ConveyorStackReserved"] = "The requested cargo is already reserved.",
            ["ConveyorStackUnavailable"] = "The requested conveyor cargo is unavailable.",
            ["ConveyorTransitOwnershipMismatch"] = "The conveyor transit reservation does not own this cargo.",
            ["CorpseReady"] = "Corpse ready.",
            ["CorpseTransportPending"] = "Corpse transport pending.",
            ["DeliveryPending"] = "Delivery pending.",
            ["DefenseAutomaticControlUnavailable"] = "Automatic control is unavailable for this defense facility.",
            ["DefenseConditionCritical"] = "The defense facility is in critical condition.",
            ["DefenseCooldownActive"] = "The defense facility is cooling down.",
            ["DefenseDestroyed"] = "The defense facility has been destroyed.",
            ["DefenseFacilityUnavailable"] = "The defense facility is unavailable.",
            ["DefenseMaintenanceDeliveryPending"] = "Waiting for maintenance parts to be delivered.",
            ["DefenseMaintenancePartMissing"] = "The required maintenance part is missing.",
            ["DefenseManualActivationRequired"] = "Manual activation is required.",
            ["DefenseMechanicalJam"] = "The defense facility is mechanically jammed.",
            ["DefenseNotJammed"] = "The defense facility is not jammed.",
            ["DefensePartialMisfire"] = "Some of the defense facility's shots misfired.",
            ["DefensePhysicalSupplyUnsupported"] = "This defense facility cannot use that physical supply.",
            ["DefensePowerUnavailable"] = "The defense facility has no power.",
            ["DefenseRepairAmountInvalid"] = "The requested repair amount is invalid.",
            ["DefenseSupplyCapacityFull"] = "The defense supply buffer is full.",
            ["DefenseSupplyDeliveryPending"] = "Waiting for defense supplies to be delivered.",
            ["DefenseSupplyUnavailable"] = "No compatible defense supply is available.",
            ["DefenseTargetDisallowed"] = "This defense facility cannot target the selected unit.",
            ["DefenseTriggerUnsupported"] = "This defense facility does not support that trigger mode.",
            ["DoctorReplacementRequested"] = "Doctor replacement requested.",
            ["DreadDefenseAlreadyArmed"] = "Dread defense already armed.",
            ["EmergencyProcedureContinuing"] = "Emergency surgery continuing · temperature {2:0.#}°C · air {3:0.#} · light {4:0.#}",
            ["EnvironmentColdWorkCooldownActive"] = "New cold-storage work cannot be assigned until exposure falls below the recovery threshold. Current exposure: {0}",
            ["EnvironmentEvacuationCellUnavailable"] = "No reachable evacuation cell is available. Character: {0}",
            ["EnvironmentEvacuationContextInvalid"] = "Environment evacuation context invalid.",
            ["EnvironmentExposureCritical"] = "Predicted environmental exposure is critical. Hazard cell: ({0}, {1}), stage: {2}, cold: {3}, heat: {4}, air: {5}, visibility: {6}",
            ["EnvironmentProtectionInsufficient"] = "Environment protection insufficient.",
            ["EnvironmentRecoveryIdle"] = "Environment recovery idle.",
            ["EnvironmentRecoveryRequested"] = "Environmental recovery tasks requested: {5}",
            ["EnvironmentRestored"] = "Environment restored · next stage {6}",
            ["EnvironmentStabilizing"] = "Stabilizing environment · {2:0.0}/5.0 sec",
            ["EnvironmentThermostatUnsupported"] = "A target temperature cannot be set at this facility position. Position: ({0}, {1})",
            ["EnvironmentUnsafe"] = "Environmental recovery required · temperature {2:0.#}°C · air {3:0.#} · light {4:0.#}",
            ["EnvironmentWorkTargetUnavailable"] = "Environment work target unavailable.",
            ["EnvironmentWorkwearCharacterMissing"] = "Environment workwear character missing.",
            ["EnvironmentWorkwearDefinitionMissing"] = "The environmental workwear definition is missing. Equipment: {0}",
            ["EnvironmentWorkwearInstanceIdMissing"] = "The environmental workwear has no physical instance ID. Stack: {0}",
            ["EnvironmentWorkwearLockerUnreachable"] = "No reachable workwear locker is near the route. Destination: ({0}, {1})",
            ["EnvironmentWorkwearNotEquipped"] = "The character is not wearing environmental protection. Character: {0}",
            ["EnvironmentWorkwearOutputSpawnFailed"] = "The environmental workwear output could not be spawned. Item: {0}, quantity: {1}",
            ["EnvironmentWorkwearPhysicalItemMissing"] = "The equipped environmental workwear item is missing. Instance: {0}",
            ["EnvironmentWorkwearProductionContextInvalid"] = "The environmental workwear production request is invalid. Item: {0}, quantity: {1}",
            ["EnvironmentWorkwearResearchLocked"] = "The required environmental workwear research is incomplete. Research: {0}",
            ["EnvironmentWorkwearSpeciesIncompatible"] = "Equipment {0} cannot be worn by species {1}.",
            ["EnvironmentWorkwearStockMissing"] = "No environmental workwear stock is available. Query: {0}",
            ["EnvironmentWorkwearTransferFailed"] = "The environmental workwear could not be transferred. Item: {0}",
            ["EquipmentDefinitionMissing"] = "Equipment definition missing.",
            ["EquipmentProgressionFacilityUnavailable"] = "A dedicated equipment progression facility is required.",
            ["EquipmentInstanceMissing"] = "Equipment instance missing.",
            ["EquipmentLineageMismatch"] = "Equipment lineage mismatch.",
            ["EquipmentModuleMissing"] = "Equipment module missing.",
            ["EquipmentOrModuleMissing"] = "Equipment or module missing.",
            ["ExpeditionSiteExpired"] = "The site expired on Day {0}; payment was cancelled.",
            ["ExpeditionSiteIdMissing"] = "Expedition site id missing.",
            ["ExternalInfluenceUnavailable"] = "External influence unavailable.",
            ["ExternalPaymentRejected"] = "External payment rejected.",
            ["FacilityMissing"] = "Facility missing.",
            ["FacilityUnavailable"] = "Facility unavailable.",
            ["FailedFatal"] = "Failed fatal.",
            ["FluidInsufficientWater"] = "The clean-water supply is insufficient.",
            ["FluidMaintenanceUnavailable"] = "Fluid-network maintenance cannot be performed.",
            ["FluidManualWaterUnavailable"] = "Manual water supply is unavailable.",
            ["FluidNetworkUnavailable"] = "No compatible fluid network is available.",
            ["FluidWastewaterUnavailable"] = "Wastewater storage or drainage is unavailable.",
            ["HistorySourceHasModules"] = "History source has modules.",
            ["HistoryTransferAlreadyActive"] = "History transfer already active.",
            ["HistoryTransferEquipmentMissing"] = "History transfer equipment missing.",
            ["HistoryTransferOrderMissing"] = "History transfer order missing.",
            ["HistoryTransferSealMissing"] = "History transfer seal missing.",
            ["HostileRumorUnavailable"] = "Hostile rumor unavailable.",
            ["IncisionInProgress"] = "Incision in progress.",
            ["IndustrialBuildingUnavailable"] = "The industrial facility is unavailable.",
            ["IndustrialCommandInvalid"] = "The industrial command is invalid.",
            ["InfrastructureStatusConveyorDeadlocked"] = "The conveyor network is deadlocked.",
            ["InfrastructureStatusConveyorDestinationFull"] = "The conveyor destination is full.",
            ["InfrastructureStatusConveyorFilterMismatch"] = "The cargo does not match the conveyor filter.",
            ["InfrastructureStatusConveyorOverflowApprovalRequired"] = "Overflow discharge requires manual approval.",
            ["InfrastructureStatusConveyorRouteUnavailable"] = "No conveyor route is available.",
            ["InfrastructureStatusInputDeliveryPending"] = "Waiting for input delivery.",
            ["InfrastructureStatusMaintenanceRequired"] = "Maintenance is required.",
            ["InfrastructureStatusOutputSpaceUnavailable"] = "Waiting for output space.",
            ["InfrastructureStatusOutputTargetReached"] = "The output target has been reached.",
            ["InfrastructureStatusPowerUnavailable"] = "Waiting for power.",
            ["InfrastructureStatusProductionMaterialUnavailable"] = "Production materials are unavailable.",
            ["InfrastructureStatusProductionOrderUnavailable"] = "No production order is available.",
            ["InfrastructureStatusProductionOutputUnavailable"] = "Production output is unavailable.",
            ["InfrastructureStatusStorageCapacityUnavailable"] = "Storage capacity is unavailable.",
            ["InsufficientDread"] = "Insufficient dread. Available {0} / required {1}",
            ["InsufficientGold"] = "Insufficient gold. Available {0} / required {1}",
            ["InsufficientRenown"] = "Insufficient renown. Available {0} / required {1}",
            ["InsufficientScoutingLabor"] = "Insufficient scouting labor. Available {0} / required {1}",
            ["InvalidCommand"] = "Invalid command.",
            ["ItemDefinitionMissing"] = "Item definition missing.",
            ["ItemNotConsumable"] = "Item not consumable.",
            ["ItemStackMissing"] = "Item stack missing.",
            ["ItemTransferConsumptionFailed"] = "The reserved item could not be consumed.",
            ["ItemTransferDestinationMissing"] = "The item-transfer destination is missing.",
            ["ItemTransferRequestFailed"] = "The item transfer could not be requested.",
            ["ItemTransferStackUnavailable"] = "The item stack is unavailable.",
            ["LineageSealMissing"] = "Lineage seal missing.",
            ["MaterialsDeliveryPending"] = "Materials delivery pending.",
            ["ModuleAlreadyAttached"] = "Module already attached.",
            ["ModuleLineageMismatch"] = "Module lineage mismatch.",
            ["ModuleNeedsRestoration"] = "Module needs restoration.",
            ["ModuleNeedsRuneTuning"] = "Module needs rune tuning.",
            ["ModuleNotRestorable"] = "Module not restorable.",
            ["ModuleNotTunable"] = "Module not tunable.",
            ["ModuleNotUnidentified"] = "Module not unidentified.",
            ["ModuleSlotEmpty"] = "Module slot {0} has no module to remove.",
            ["ModuleSlotMissing"] = "This equipment has no module slot {0}.",
            ["OffenseTargetUnknown"] = "This expedition target has not been discovered: {0}",
            ["OperatingDayNotStarted"] = "Operating day not started.",
            ["OperationStarted"] = "Operation started.",
            ["PatientAdmissionCellMissing"] = "Patient admission cell missing.",
            ["PatientAdmissionWaiting"] = "Patient admission waiting.",
            ["PatientAdmitted"] = "Patient admitted.",
            ["PatientCurrentMovePending"] = "Patient current move pending.",
            ["PatientMissing"] = "Patient missing.",
            ["PatientMovingToSurgery"] = "Patient moving to surgery.",
            ["PatientRestraintInProgress"] = "Patient restraint in progress.",
            ["PatientRestraintRequired"] = "Patient restraint required.",
            ["PatientTransportByRescuer"] = "Patient transport by rescuer.",
            ["PhysicalConsumptionFailed"] = "Physical consumption failed.",
            ["PolicyForbidden"] = "Policy forbidden.",
            ["PowerBreakerUnavailable"] = "The circuit breaker is unavailable.",
            ["PowerConsumerUnavailable"] = "The power consumer is unavailable.",
            ["PrisonReturnCompleted"] = "Prison return completed.",
            ["PrisonReturnInProgress"] = "Prison return in progress.",
            ["ProcedureInProgress"] = "Procedure in progress.",
            ["ProcedureInterruptedOpenWound"] = "Procedure interrupted open wound.",
            ["ProcedurePaused"] = "Procedure paused.",
            ["ProcessFluidUnavailable"] = "Process fluid unavailable.",
            ["ProductionBatchRuined"] = "The production batch was ruined.",
            ["ProductionBillMissing"] = "The production order is missing.",
            ["ProductionBillReservedByOtherWorker"] = "Another worker has reserved this production order.",
            ["ProductionBillUnavailable"] = "The production order is unavailable.",
            ["ProductionDistributionRouteUnavailable"] = "No output distribution route is available.",
            ["ProductionFacilityMissing"] = "The production facility is missing.",
            ["ProductionMaterialsMissing"] = "Required production materials are missing.",
            ["ProductionOutputSpaceUnavailable"] = "There is no reserved output space.",
            ["ProductionOutputUnavailable"] = "The production output cannot be created.",
            ["ProductionProcessingActive"] = "The production batch is still in progress.",
            ["ProductionRecipeMissing"] = "The production recipe is missing.",
            ["ProductionResearchLocked"] = "The required research is not complete.",
            ["ProductionStockSensorRequired"] = "A stock sensor is required for this order mode.",
            ["ProductionSupportUnavailable"] = "Required production support is unavailable.",
            ["ProductionTargetStockSatisfied"] = "The target stock level is already satisfied.",
            ["ProductionUtilitiesUnavailable"] = "Required utilities are unavailable.",
            ["ProductionWorkstationMismatch"] = "This workstation cannot run the selected recipe.",
            ["ProductionWorkstationMissing"] = "The production workstation is missing.",
            ["RecoveryCompleted"] = "Recovery completed.",
            ["RecoveryObservation"] = "Recovery observation.",
            ["RequiredResearchUnavailable"] = "Required research or facility is unavailable. Research: {0}, facility: {1}",
            ["RumorMitigationAlreadyUsed"] = "Rumors have already been mitigated on Day {0}.",
            ["RunResultEmpty"] = "No run result is available.",
            ["RunResultNextRun"] = "Next run",
            ["ServiceActorAlreadyActive"] = "Service actor already active.",
            ["ServiceCapacityFull"] = "Service capacity full.",
            ["ServiceClosed"] = "Service closed.",
            ["ServiceFeatureMissing"] = "A required connected facility is missing from the room: {0}",
            ["ServiceHubUnavailable"] = "Service hub unavailable.",
            ["ServiceModeUnsupported"] = "Service mode unsupported.",
            ["ServiceProcessContractMissing"] = "Process {0} has no {1} operations contract.",
            ["ServiceProcessIdMissing"] = "Service process id missing.",
            ["ServiceSessionMissing"] = "Service session missing.",
            ["ServiceStageIncomplete"] = "Service stage incomplete.",
            ["ServiceStageNotAllowed"] = "Stage {0} is not allowed by the current contract.",
            ["ServiceSupportUnpowered"] = "The support facility has no power: {0}",
            ["SurgeryAnatomyFamilyUnsupported"] = "This surgery does not support the anatomy family. Family: {0}",
            ["SurgeryConstructProcedureBiologicalMismatch"] = "Surgery construct procedure biological mismatch.",
            ["SurgeryConstructProcedureRequired"] = "Surgery construct procedure required.",
            ["SurgeryCorpseMissing"] = "The target corpse item could not be found. Corpse: {0}",
            ["SurgeryCorpseStale"] = "Surgery corpse stale.",
            ["SurgeryDoctorAlreadyAssigned"] = "Another surgeon is already operating. Surgeon: {0}",
            ["SurgeryEffectFailed"] = "The surgery effect failed. Effect: {0}",
            ["SurgeryEffectHandlerMissing"] = "The surgery effect is not registered. Effect: {0}",
            ["SurgeryEnvironmentUnsafe"] = "The surgery environment is unsafe. Order: {0}",
            ["SurgeryExtractionAlreadyRecorded"] = "An extraction is already recorded for this node. Corpse: {0}, node: {1}",
            ["SurgeryFacilityMissing"] = "The surgery facility could not be found. Facility: {0}",
            ["SurgeryFacilityOrProcedureMissing"] = "Surgery facility or procedure missing.",
            ["SurgeryFacilityUnavailable"] = "The surgery facility is unavailable. Facility: {0}",
            ["SurgeryLivingSubjectUnavailable"] = "The living surgery subject could not be found. Subject: {0}",
            ["SurgeryMaterialUnavailable"] = "The surgery material is unavailable. Item: {0}",
            ["SurgeryNodeAlreadyExtracted"] = "The target anatomy node has already been extracted. Node: {0}",
            ["SurgeryOperatorIneligible"] = "Surgery operator ineligible.",
            ["SurgeryOperatorMissing"] = "Surgery operator missing.",
            ["SurgeryOperatorSkillInsufficient"] = "Surgeon skill is insufficient. Required: {0}, current: {1}",
            ["SurgeryOperatorStatInsufficient"] = "Surgeon stat is insufficient. Stat: {0}, required: {1}, current: {2}",
            ["SurgeryOrderMissing"] = "No actionable surgery order was found. Order: {0}",
            ["SurgeryOutcomeFailed"] = "The surgery outcome failed. Severity: {0}",
            ["SurgeryPartKindMismatch"] = "Surgery part kind mismatch.",
            ["SurgeryPartNodeMismatch"] = "Surgery part node mismatch.",
            ["SurgeryPartUnavailable"] = "Surgery part unavailable.",
            ["SurgeryPreferredDoctorInvalid"] = "The preferred surgeon could not be found or assigned. Surgeon: {0}",
            ["SurgeryPreferredDoctorOnly"] = "Only the preferred surgeon may perform this operation. Surgeon: {0}",
            ["SurgeryProcedureMissing"] = "The surgery procedure could not be found. Procedure: {0}",
            ["SurgeryResearchIncomplete"] = "Required surgery research is incomplete. Research: {0}",
            ["SurgeryResearchStateUnavailable"] = "Surgery research state unavailable.",
            ["SurgeryReservedDoctorMismatch"] = "This is not the reserved surgeon. Surgeon: {0}",
            ["SurgeryRiskEnvironmentAdjusted"] = "Environment accumulated {9} stage(s) · success -{5:P1} · infection +{6:P1} · bleeding +{7:P1} · organ damage +{8:P1}",
            ["SurgeryRiskEvaluated"] = "Success {0:P1} · infection {1:P1} · bleeding {2:P1} · organ damage {3:P1} · death {4:P1}",
            ["SurgeryRiskProcedureMissing"] = "Surgery risk procedure missing.",
            ["SurgerySelfOperationForbidden"] = "Surgery self operation forbidden.",
            ["SurgerySpeciesUnsupported"] = "This surgery does not support the species. Species: {0}",
            ["SurgeryStaffOnly"] = "Surgery staff only.",
            ["SurgerySubjectAlreadyScheduled"] = "The subject already has an active surgery order. Subject: {0}",
            ["SurgerySubjectInvalid"] = "Surgery subject invalid.",
            ["SurgerySubjectKindUnsupported"] = "Surgery subject kind unsupported.",
            ["SurgerySubjectMaintenanceOnly"] = "{0} requires maintenance rather than biological surgery.",
            ["SurgeryTargetNodeMissing"] = "Surgery target node missing.",
            ["SurgeryTargetNodeUnavailable"] = "The subject does not have the target anatomy node. Node: {0}",
            ["SurgeryTransportCarrierMismatch"] = "This is not the reserved carrier. Carrier: {0}",
            ["SurgeryTransportOrderMissing"] = "The wildlife patient transport order could not be found. Order: {0}",
            ["SurgeryTransportUnavailable"] = "The wildlife patient cannot be transported. Subject: {0}",
            ["SurgeryWildlifeSubjectUnavailable"] = "The living wildlife surgery subject could not be found. Subject: {0}",
            ["SurvivalCookingUnsupported"] = "This facility cannot cook. Facility: {0}",
            ["SurvivalFoodStockMissing"] = "Insufficient cooking ingredients. Required quantity: {0}",
            ["SurvivalFuelStockMissing"] = "Insufficient fuel. Required quantity: {0}",
            ["SurvivalOutputUnavailable"] = "There is no space for the output. Item: {0}",
            ["SurvivalRefuelUnsupported"] = "This facility cannot be refueled. Facility: {0}",
            ["SurvivalTargetFacilityMissing"] = "Survival target facility missing.",
            ["SurvivalTreatmentMaterialMissing"] = "Survival treatment material missing.",
            ["SurvivalTreatmentTargetMissing"] = "There is no treatment target. Facility: {0}",
            ["SurvivalTreatmentUnsupported"] = "This facility cannot provide treatment. Facility: {0}",
            ["SurvivalWaterFrozen"] = "Cold weather has blocked the water supply. Facility: {0}",
            ["SurvivalWaterSourceUnsupported"] = "This facility cannot provide water. Facility: {0}",
            ["SurvivalWorkUnsupported"] = "This survival task is unsupported. Task: {0}",
            ["SuturingInProgress"] = "Suturing in progress.",
            ["TrailCharmMissing"] = "Trail charm missing.",
            ["WasteFeedBufferUnavailable"] = "No delivered waste feed is available in the destination buffer.",
            ["WasteFeedDeliveryFailed"] = "Waste feed delivery could not be requested.",
            ["WasteFeedUnavailable"] = "No safe waste feed matches the current policy.",
            ["WastePolicyInvalid"] = "The waste policy is invalid.",
            ["WastePolicyUnsupported"] = "This waste origin does not support the selected disposition.",
            ["WildlifePatientMissing"] = "Wildlife patient missing.",
            ["WildlifePatientReady"] = "Wildlife patient ready.",
            ["WildlifePatientReturnCompleted"] = "Wildlife patient return completed.",
            ["WildlifePatientReturning"] = "Wildlife patient returning.",
            ["WildlifePatientTransporting"] = "Wildlife patient transporting.",
            ["WildlifeRestraintRequired"] = "Wildlife restraint required.",
        };

    private static readonly IReadOnlyDictionary<string, string> Korean =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AgeTreatmentAnatomyUnavailable"] = "치료에 필요한 해부 구조를 사용할 수 없습니다. 캐릭터: {0}, 질환: {1}, 해부 구조: {2}",
            ["AgeTreatmentCharacterMissing"] = "노화 치료 대상 캐릭터를 찾을 수 없습니다. 캐릭터: {0}",
            ["AgeTreatmentCooldownActive"] = "회춘 시술의 재시술 제한 기간입니다. 캐릭터: {0}, 남은 일수: {1}",
            ["AgeTreatmentDefinitionMissing"] = "노화 치료 정의를 찾을 수 없습니다. 치료: {0}",
            ["AgeTreatmentProcedureMismatch"] = "치료와 의료 시술이 일치하지 않습니다. 치료: {0}, 시술: {1}",
            ["AgeTreatmentSupplyUnavailable"] = "노화 치료 물자가 부족합니다. 시설: {0}, 치료: {1}, 사유: {2}",
            ["AgeTreatmentTooYoung"] = "회춘 시술을 받기에는 너무 어립니다. 캐릭터: {0}, 최소 나이: {1}",
            ["ChildSafetyApprenticeshipDisabled"] = "감독 도제 정책이 비활성화되어 있습니다. 캐릭터: {0}",
            ["ChildSafetyAuthorizationInvalid"] = "아동 안전 허가가 유효하지 않습니다.",
            ["ChildSafetyCharacterPermissionRequired"] = "이 청소년은 개별 도제 허용이 필요합니다. 캐릭터: {0}",
            ["ChildSafetyCombatForbidden"] = "아동은 전투 및 전투 보급 경로에 진입할 수 없습니다.",
            ["ChildSafetyHazardEscapeDirectionInvalid"] = "위험 구역 안의 아동은 더 안전한 칸으로만 이동할 수 있습니다.",
            ["ChildSafetyLifeStateUnavailable"] = "캐릭터의 생애 상태를 확인할 수 없습니다. 캐릭터: {0}",
            ["ChildSafetyProtectiveEquipmentRequired"] = "필수 보호구가 없습니다. 캐릭터: {0}",
            ["ChildSafetySupervisorTooFar"] = "성인 감독자가 6칸보다 멀리 있습니다. 감독자: {0}",
            ["ChildSafetySupervisorUnavailable"] = "성인 감독자를 사용할 수 없습니다. 감독자: {0}",
            ["ChildSafetyWorkConfirmationRequired"] = "이 도제 작업은 명시적 확인이 필요합니다. 캐릭터: {0}",
            ["ChildSafetyWorkForbidden"] = "현재 생애 단계에서는 이 작업을 수행할 수 없습니다. 캐릭터: {0}",
            ["CropTreatmentDefinitionMissing"] = "작물 처리제 정의를 찾을 수 없습니다. 처리제: {0}",
            ["CropTreatmentKindUnsupported"] = "작물 처리제의 용도가 맞지 않습니다. 처리제: {0}, 용도: {1}",
            ["CropTreatmentPlotMissing"] = "재배지를 찾을 수 없습니다. 재배지: {0}",
            ["CropTreatmentSupplyUnavailable"] = "작물 처리제가 부족합니다. 재배지: {0}, 처리제: {1}, 사유: {2}",
            ["PopulationHealthCharacterMissing"] = "인구 보건 대상 캐릭터를 찾을 수 없습니다. 캐릭터: {0}",
            ["TemporalStasisFacilityMissing"] = "시간 고정 시설을 사용할 수 없습니다. 시설: {0}",
            ["TemporalStasisMaintenanceUnavailable"] = "시간 고정 유지 물자가 부족합니다.",
            ["TemporalStasisPowerInsufficient"] = "시간 고정에 필요한 룬 동력이 부족합니다. 시설: {0}, 필요 동력: {1}",
            ["VaccineDefinitionMissing"] = "백신 정의를 찾을 수 없습니다. 백신: {0}",
            ["VaccineDiseaseMismatch"] = "백신과 대상 질병이 일치하지 않습니다. 백신: {0}, 질병: {1}",
            ["VaccineDoseUnavailable"] = "물리 백신 투여분이 부족합니다. 목적지: {0}, 백신: {1}, 사유: {2}",
            ["DefenseAutomaticControlUnavailable"] = "이 방어 시설은 자동 통제를 사용할 수 없습니다.",
            ["DefenseConditionCritical"] = "방어 시설의 상태가 위험 수준입니다.",
            ["DefenseCooldownActive"] = "방어 시설이 재사용 대기 중입니다.",
            ["DefenseDestroyed"] = "방어 시설이 파괴되었습니다.",
            ["DefenseFacilityUnavailable"] = "방어 시설을 사용할 수 없습니다.",
            ["DefenseMaintenanceDeliveryPending"] = "정비 부품 운반을 기다리고 있습니다.",
            ["DefenseMaintenancePartMissing"] = "필요한 정비 부품이 없습니다.",
            ["DefenseManualActivationRequired"] = "수동으로 작동시켜야 합니다.",
            ["DefenseMechanicalJam"] = "방어 시설의 기계 장치가 걸렸습니다.",
            ["DefenseNotJammed"] = "방어 시설이 걸림 상태가 아닙니다.",
            ["DefensePartialMisfire"] = "방어 시설의 일부 발사가 실패했습니다.",
            ["DefensePhysicalSupplyUnsupported"] = "이 방어 시설은 해당 물리 보급품을 사용할 수 없습니다.",
            ["DefensePowerUnavailable"] = "방어 시설에 전력이 공급되지 않습니다.",
            ["DefenseRepairAmountInvalid"] = "요청한 수리량이 올바르지 않습니다.",
            ["DefenseSupplyCapacityFull"] = "방어 시설의 보급 저장 공간이 가득 찼습니다.",
            ["DefenseSupplyDeliveryPending"] = "방어 보급품 운반을 기다리고 있습니다.",
            ["DefenseSupplyUnavailable"] = "사용할 수 있는 방어 보급품이 없습니다.",
            ["DefenseTargetDisallowed"] = "이 방어 시설은 선택한 대상을 공격할 수 없습니다.",
            ["DefenseTriggerUnsupported"] = "이 방어 시설은 해당 작동 방식을 지원하지 않습니다.",
["AlreadyProcessed"] = "이미 처리된 소모품 사용입니다.",
            ["AnesthesiaInProgress"] = "마취 진행 중",
            ["AutomationFacilityUnavailable"] = "자동화 시설을 사용할 수 없습니다.",
            ["AutomationFaulted"] = "시설 고장으로 자동화가 중단되었습니다.",
            ["AutomationMaintenanceRequired"] = "자동화를 재개하려면 정비가 필요합니다.",
            ["AutomationModeUnsupported"] = "이 시설은 선택한 자동화 모드를 지원하지 않습니다.",
            ["AutomationUnpowered"] = "자동화 설비가 전력을 기다리고 있습니다.",
            ["Cancelled"] = "수술 취소",
            ["CharacterMedicalAmbulatoryTreatmentUnsupported"] = "보행 가능한 환자는 구조 의료 주문을 사용할 수 없습니다. 환자: {0}",
            ["CharacterMedicalBedUnavailable"] = "사용 가능한 치료 침상이 없습니다. 주문: {0}",
            ["CharacterMedicalDestinationUnavailable"] = "치료 목적지를 사용할 수 없습니다. 주문: {0}",
            ["CharacterMedicalFacilityReserved"] = "의료 시설이 다른 환자에게 예약되어 있습니다. 시설: {0}, 환자: {1}",
            ["CharacterMedicalFacilityUnavailable"] = "환자를 수용할 수 있는 의료 시설이 아닙니다. 시설: {0}",
            ["CharacterMedicalNoTreatableInjury"] = "치료가 필요한 부상이 없습니다. 환자: {0}",
            ["CharacterMedicalOrderCreationFailed"] = "환자의 치료 주문을 만들 수 없습니다. 환자: {0}",
            ["CharacterMedicalOrderMissing"] = "의료 주문을 찾을 수 없습니다. 주문: {0}",
            ["CharacterMedicalOrderUnavailable"] = "의료 주문을 사용할 수 없습니다. 환자 또는 주문: {0}",
            ["CharacterMedicalParticipantsInvalid"] = "구조자 또는 환자의 상태가 유효하지 않습니다.",
            ["CharacterMedicalPatientUnavailable"] = "치료하거나 구조할 환자가 없습니다.",
            ["CharacterMedicalProjectionGridOccupied"] = "환자의 복원 위치가 이미 점유되어 있습니다. 위치: ({0}, {1})",
            ["CharacterMedicalProjectionPositionInvalid"] = "환자의 복원 위치가 유효하지 않거나 중복되었습니다. 환자: {0}, 위치: ({1}, {2})",
            ["CharacterMedicalRescuerUnavailable"] = "구조 임무를 수행할 수 있는 캐릭터가 아닙니다.",
            ["CharacterMedicalReservationMismatch"] = "의료 주문을 예약한 구조자와 일치하지 않습니다. 주문: {0}, 구조자: {1}",
            ["CharacterMedicalRuntimeUnavailable"] = "의료 구조 시스템을 사용할 수 없습니다.",
            ["CharacterMedicalStabilizationRequired"] = "환자를 운반하기 전에 현장 안정화가 필요합니다. 주문: {0}",
            ["CharacterMedicalStatusAdditionalTreatmentRequired"] = "추가 치료 필요",
            ["CharacterMedicalStatusAwaitingBed"] = "치료 침상 필요",
            ["CharacterMedicalStatusAwaitingExtractedBloodDelivery"] = "추출 혈액 운반 대기",
            ["CharacterMedicalStatusAwaitingMedicineDelivery"] = "약품 운반 대기",
            ["CharacterMedicalStatusAwaitingRescue"] = "구조 대기",
            ["CharacterMedicalStatusAwaitingStabilization"] = "현장 안정화 필요",
            ["CharacterMedicalStatusCancelled"] = "의료 주문 취소",
            ["CharacterMedicalStatusCarrying"] = "병상으로 이송 중",
            ["CharacterMedicalStatusManualRescueAssigned"] = "직접 구조 명령으로 전환",
            ["CharacterMedicalStatusMedicineReady"] = "약품 투약 준비 완료",
            ["CharacterMedicalStatusPatientDied"] = "환자 사망",
            ["CharacterMedicalStatusPatientMissing"] = "환자 정보를 찾을 수 없음",
            ["CharacterMedicalStatusPatientPathUnavailable"] = "환자에게 이동할 수 없음",
            ["CharacterMedicalStatusPreparingStabilization"] = "현장 안정화 준비",
            ["CharacterMedicalStatusPreparingTransfer"] = "병상 이송 준비",
            ["CharacterMedicalStatusRescueInterrupted"] = "구조 중단",
            ["CharacterMedicalStatusRescuerDied"] = "구조자 사망",
            ["CharacterMedicalStatusRescuerMissing"] = "구조자 정보를 찾을 수 없음",
            ["CharacterMedicalStatusReservationReleased"] = "구조 예약 해제",
            ["CharacterMedicalStatusRestarted"] = "구조 작업 재시작",
            ["CharacterMedicalStatusStabilizationInterrupted"] = "현장 안정화 중단",
            ["CharacterMedicalStatusStabilizedWithInfectionRisk"] = "응급 처치 완료 · 감염 위험",
            ["CharacterMedicalStatusStabilizing"] = "현장 안정화 중",
            ["CharacterMedicalStatusSupplyUnavailable"] = "약품과 추출 혈액 부족",
            ["CharacterMedicalStatusTreating"] = "병상 치료 중",
            ["CharacterMedicalStatusTreatingWithExtractedBlood"] = "혈액 대체 치료 중",
            ["CharacterMedicalStatusTreatmentCompleted"] = "치료 완료",
            ["CharacterMedicalStatusTreatmentInterrupted"] = "치료 중단",
            ["CharacterMedicalStatusTreatmentPathUnavailable"] = "치료 시설로 이동할 수 없음",
            ["CharacterMedicalStatusTreatmentRequested"] = "부상 치료 대기",
            ["CharacterMissing"] = "캐릭터를 찾을 수 없습니다.",
            ["CharacterSpeciesRechargeUnsupported"] = "이 종족은 충전할 수 없습니다. 종족: {0}",
            ["CharacterSpeciesRepairUnsupported"] = "이 종족은 건전도 정비를 받을 수 없습니다. 종족: {0}",
            ["CharacterSpeciesStateUnavailable"] = "캐릭터 종족 상태를 찾을 수 없습니다. 캐릭터: {0}",
            ["Completed"] = "수술 완료",
            ["CompletedWithMajorFailure"] = "수술 실패 · 중대한 합병증",
            ["CompletedWithMinorFailure"] = "수술 완료 · 경미한 합병증",
            ["ConveyorDestinationUnavailable"] = "컨베이어 목적지를 사용할 수 없습니다.",
            ["ConveyorFilterMismatch"] = "화물이 컨베이어 필터와 맞지 않습니다.",
            ["ConveyorOverflowApprovalRequired"] = "오버플로 배출에 수동 승인이 필요합니다.",
            ["ConveyorPayloadMissing"] = "컨베이어 화물이 존재하지 않습니다.",
            ["ConveyorPortFull"] = "컨베이어 포트가 가득 찼습니다.",
            ["ConveyorPortUnavailable"] = "호환되는 컨베이어 포트가 없습니다.",
            ["ConveyorRouteUnavailable"] = "사용 가능한 컨베이어 경로가 없습니다.",
            ["ConveyorStackOutOfRange"] = "화물이 컨베이어 범위 밖에 있습니다.",
            ["ConveyorStackReserved"] = "화물이 이미 예약되어 있습니다.",
            ["ConveyorStackUnavailable"] = "운반할 화물을 찾을 수 없습니다.",
            ["ConveyorTransitOwnershipMismatch"] = "컨베이어 운송 예약과 화물 소유권이 일치하지 않습니다.",
            ["CorpseReady"] = "사체 해부 준비 완료",
            ["CorpseTransportPending"] = "사체 운반 대기",
            ["DeliveryPending"] = "식사 배달을 기다리고 있습니다.",
            ["DoctorReplacementRequested"] = "집도의 행동 불능 · 대체 집도의 요청",
            ["DreadDefenseAlreadyArmed"] = "다음 침입에 적용할 공포 방어가 이미 준비되어 있습니다.",
            ["EmergencyProcedureContinuing"] = "응급 수술 계속 · 온도 {2:0.#}°C · 공기 {3:0.#} · 조명 {4:0.#}",
            ["EnvironmentColdWorkCooldownActive"] = "냉기 노출이 회복 기준 미만이 될 때까지 새 냉장 작업을 배정할 수 없습니다. 현재 노출: {0}",
            ["EnvironmentEvacuationCellUnavailable"] = "도달 가능한 대피 셀이 없습니다. 캐릭터: {0}",
            ["EnvironmentEvacuationContextInvalid"] = "대피할 캐릭터 또는 그리드가 없습니다.",
            ["EnvironmentExposureCritical"] = "예상 환경 노출이 위급합니다. 위험 셀: ({0}, {1}), 단계: {2}, 냉기: {3}, 열기: {4}, 공기: {5}, 시각: {6}",
            ["EnvironmentProtectionInsufficient"] = "보호장비를 착용해도 예상 환경 노출이 위급합니다.",
            ["EnvironmentRecoveryIdle"] = "환경 정상 · 집도의 대기",
            ["EnvironmentRecoveryRequested"] = "환경 복구 작업 요청 {5}건",
            ["EnvironmentRestored"] = "환경 복구 완료 · 다음 단계 {6}",
            ["EnvironmentStabilizing"] = "환경 안정화 중 · {2:0.0}/5.0초",
            ["EnvironmentThermostatUnsupported"] = "이 시설 위치에서는 목표 온도를 설정할 수 없습니다. 위치: ({0}, {1})",
            ["EnvironmentUnsafe"] = "환경 복구 필요 · 온도 {2:0.#}°C · 공기 {3:0.#} · 조명 {4:0.#}",
            ["EnvironmentWorkTargetUnavailable"] = "작업 대상의 환경 정보를 확인할 수 없습니다.",
            ["EnvironmentWorkwearCharacterMissing"] = "환경 보호장비를 사용할 캐릭터 식별자가 없습니다.",
            ["EnvironmentWorkwearDefinitionMissing"] = "환경 보호장비 정의를 찾을 수 없습니다. 장비: {0}",
            ["EnvironmentWorkwearInstanceIdMissing"] = "환경 보호장비의 물리 인스턴스 식별자가 없습니다. 스택: {0}",
            ["EnvironmentWorkwearLockerUnreachable"] = "경로 주변에 접근 가능한 보호장비 보관함이 없습니다. 목적지: ({0}, {1})",
            ["EnvironmentWorkwearNotEquipped"] = "캐릭터가 환경 보호장비를 착용하고 있지 않습니다. 캐릭터: {0}",
            ["EnvironmentWorkwearOutputSpawnFailed"] = "환경 보호장비 생산품을 생성할 수 없습니다. 항목: {0}, 수량: {1}",
            ["EnvironmentWorkwearPhysicalItemMissing"] = "착용한 환경 보호장비의 물리 아이템을 찾을 수 없습니다. 인스턴스: {0}",
            ["EnvironmentWorkwearProductionContextInvalid"] = "환경 보호장비 생산 요청이 유효하지 않습니다. 항목: {0}, 수량: {1}",
            ["EnvironmentWorkwearResearchLocked"] = "환경 보호장비에 필요한 연구가 완료되지 않았습니다. 연구: {0}",
            ["EnvironmentWorkwearSpeciesIncompatible"] = "장비 {0}은(는) 종족 {1}이(가) 착용할 수 없습니다.",
            ["EnvironmentWorkwearStockMissing"] = "사용 가능한 환경 보호장비 재고가 없습니다. 조회 기준: {0}",
            ["EnvironmentWorkwearTransferFailed"] = "환경 보호장비를 이동할 수 없습니다. 항목: {0}",
            ["EquipmentDefinitionMissing"] = "장비 정의를 찾을 수 없습니다.",
            ["EquipmentProgressionFacilityUnavailable"] = "이 작업을 수행할 수 있는 전용 장비 시설이 필요합니다.",
            ["EquipmentInstanceMissing"] = "장비 인스턴스를 찾을 수 없습니다.",
            ["EquipmentLineageMismatch"] = "무기·방어구·방패 계열이 같은 장비끼리만 계승할 수 있습니다.",
            ["EquipmentModuleMissing"] = "장비 부품을 찾을 수 없습니다.",
            ["EquipmentOrModuleMissing"] = "장비 또는 부품을 찾을 수 없습니다.",
            ["ExpeditionSiteExpired"] = "거점이 Day {0}에 만료되어 결제를 취소했습니다.",
            ["ExpeditionSiteIdMissing"] = "원정지 ID가 필요합니다.",
            ["ExternalInfluenceUnavailable"] = "원정 정보 교환 체계가 연결되지 않았습니다.",
            ["ExternalPaymentRejected"] = "외부 영향 결제가 승인되지 않았습니다.",
            ["FacilityMissing"] = "시설을 찾을 수 없습니다.",
            ["FacilityUnavailable"] = "수술 시설 사용 불가",
            ["FailedFatal"] = "수술 실패 · 치명적 결과",
            ["FluidInsufficientWater"] = "깨끗한 물이 부족합니다.",
            ["FluidMaintenanceUnavailable"] = "유체망을 정비할 수 없습니다.",
            ["FluidManualWaterUnavailable"] = "수동 급수를 사용할 수 없습니다.",
            ["FluidNetworkUnavailable"] = "연결 가능한 유체망이 없습니다.",
            ["FluidWastewaterUnavailable"] = "오수 저장소나 배수로를 사용할 수 없습니다.",
            ["HistorySourceHasModules"] = "원본 장비의 부품을 먼저 모두 제거해야 합니다.",
            ["HistoryTransferAlreadyActive"] = "선택한 장비에 이미 진행 중인 계보 작업이 있습니다.",
            ["HistoryTransferEquipmentMissing"] = "계보 작업에 필요한 장비가 사라졌습니다.",
            ["HistoryTransferOrderMissing"] = "진행할 계보 이전 작업이 없습니다.",
            ["HistoryTransferSealMissing"] = "계보 인장이 사라져 작업을 완료할 수 없습니다.",
            ["HostileRumorUnavailable"] = "수습할 적대적 소문이 없습니다.",
            ["IncisionInProgress"] = "절개 중",
            ["IndustrialBuildingUnavailable"] = "산업 시설을 사용할 수 없습니다.",
            ["IndustrialCommandInvalid"] = "산업 시설 명령이 올바르지 않습니다.",
            ["InfrastructureStatusConveyorDeadlocked"] = "컨베이어망이 교착 상태입니다.",
            ["InfrastructureStatusConveyorDestinationFull"] = "컨베이어 목적지가 가득 찼습니다.",
            ["InfrastructureStatusConveyorFilterMismatch"] = "화물이 컨베이어 필터와 맞지 않습니다.",
            ["InfrastructureStatusConveyorOverflowApprovalRequired"] = "오버플로 배출에 수동 승인이 필요합니다.",
            ["InfrastructureStatusConveyorRouteUnavailable"] = "컨베이어 경로가 없습니다.",
            ["InfrastructureStatusInputDeliveryPending"] = "재료 운반을 기다리는 중입니다.",
            ["InfrastructureStatusMaintenanceRequired"] = "정비가 필요합니다.",
            ["InfrastructureStatusOutputSpaceUnavailable"] = "출력 공간을 기다리는 중입니다.",
            ["InfrastructureStatusOutputTargetReached"] = "목표 생산량을 충족했습니다.",
            ["InfrastructureStatusPowerUnavailable"] = "전력을 기다리는 중입니다.",
            ["InfrastructureStatusProductionMaterialUnavailable"] = "생산 재료가 부족합니다.",
            ["InfrastructureStatusProductionOrderUnavailable"] = "실행할 생산 주문이 없습니다.",
            ["InfrastructureStatusProductionOutputUnavailable"] = "생산품을 출력할 수 없습니다.",
            ["InfrastructureStatusStorageCapacityUnavailable"] = "저장 공간을 사용할 수 없습니다.",
            ["InsufficientDread"] = "공포가 부족합니다. 보유 {0} / 필요 {1}",
            ["InsufficientGold"] = "골드가 부족합니다. 보유 {0} / 필요 {1}",
            ["InsufficientRenown"] = "명성이 부족합니다. 보유 {0} / 필요 {1}",
            ["InsufficientScoutingLabor"] = "정찰 노동이 부족합니다. 보유 {0} / 필요 {1}",
            ["InvalidCommand"] = "소모품 사용 명령이 올바르지 않습니다.",
            ["ItemDefinitionMissing"] = "아이템 정의를 찾을 수 없습니다.",
            ["ItemNotConsumable"] = "이 아이템은 먹거나 복용할 수 없습니다.",
            ["ItemStackMissing"] = "사용할 아이템 재고가 없습니다.",
            ["ItemTransferConsumptionFailed"] = "예약한 아이템을 소비하지 못했습니다.",
            ["ItemTransferDestinationMissing"] = "아이템 운반 목적지가 없습니다.",
            ["ItemTransferRequestFailed"] = "아이템 운반을 요청하지 못했습니다.",
            ["ItemTransferStackUnavailable"] = "아이템 묶음을 사용할 수 없습니다.",
            ["LineageSealMissing"] = "지역 최종 보스에게 얻은 계보 인장이 필요합니다.",
            ["MaterialsDeliveryPending"] = "수술 재료 운반 대기",
            ["ModuleAlreadyAttached"] = "이미 다른 장비에 장착된 부품입니다.",
            ["ModuleLineageMismatch"] = "부품과 장비의 공격·방어 계열이 일치하지 않습니다.",
            ["ModuleNeedsRestoration"] = "상태 75% 이상의 복원된 부품만 장착할 수 있습니다.",
            ["ModuleNeedsRuneTuning"] = "4등급 부품은 룬 조율을 마쳐야 합니다.",
            ["ModuleNotRestorable"] = "복원할 수 있는 분리된 감정 부품이 아닙니다.",
            ["ModuleNotTunable"] = "복원 완료된 4등급 부품만 조율할 수 있습니다.",
            ["ModuleNotUnidentified"] = "감정할 수 있는 미확인 부품이 아닙니다.",
            ["ModuleSlotEmpty"] = "부품 슬롯 {0}에 제거할 부품이 없습니다.",
            ["ModuleSlotMissing"] = "해당 장비에 부품 슬롯 {0}이(가) 없습니다.",
            ["OffenseTargetUnknown"] = "발견되지 않은 원정 대상입니다: {0}",
            ["OperatingDayNotStarted"] = "영업일이 시작된 뒤 실행할 수 있습니다.",
            ["OperationStarted"] = "집도 시작",
            ["PatientAdmissionCellMissing"] = "수술대에 접근할 수 있는 환자 칸이 없습니다.",
            ["PatientAdmissionWaiting"] = "환자 입실 대기",
            ["PatientAdmitted"] = "환자 입실 완료",
            ["PatientCurrentMovePending"] = "환자의 현재 이동이 끝나기를 기다리는 중",
            ["PatientMissing"] = "환자를 찾을 수 없습니다.",
            ["PatientMovingToSurgery"] = "환자가 수술실로 이동 중",
            ["PatientRestraintInProgress"] = "환자 고정 중",
            ["PatientRestraintRequired"] = "비동의 환자는 먼저 구속해야 합니다.",
            ["PatientTransportByRescuer"] = "구조자가 환자를 수술실로 이송 중",
            ["PhysicalConsumptionFailed"] = "물리 아이템 소비에 실패했습니다.",
            ["PolicyForbidden"] = "현재 섭취 정책에서 허용되지 않습니다.",
            ["PowerBreakerUnavailable"] = "차단기를 조작할 수 없습니다.",
            ["PowerConsumerUnavailable"] = "전력 소비 시설을 찾을 수 없습니다.",
            ["PrisonReturnCompleted"] = "수술 완료 · 감방 복귀 완료",
            ["PrisonReturnInProgress"] = "수술 완료 · 감방 복귀 중",
            ["ProcedureInProgress"] = "수술 처치 중",
            ["ProcedureInterruptedOpenWound"] = "수술 중단 · 열린 상처",
            ["ProcedurePaused"] = "수술 일시 중단",
            ["ProcessFluidUnavailable"] = "수술 공정 유체 대기",
            ["ProductionBatchRuined"] = "생산 배치가 손상되었습니다.",
            ["ProductionBillMissing"] = "생산 주문이 없습니다.",
            ["ProductionBillReservedByOtherWorker"] = "다른 작업자가 이 생산 주문을 예약했습니다.",
            ["ProductionBillUnavailable"] = "생산 주문을 사용할 수 없습니다.",
            ["ProductionDistributionRouteUnavailable"] = "생산품을 배분할 경로가 없습니다.",
            ["ProductionFacilityMissing"] = "생산 시설이 없습니다.",
            ["ProductionMaterialsMissing"] = "생산 재료가 부족합니다.",
            ["ProductionOutputSpaceUnavailable"] = "예약할 출력 공간이 없습니다.",
            ["ProductionOutputUnavailable"] = "생산품을 만들 수 없습니다.",
            ["ProductionProcessingActive"] = "생산 배치가 아직 진행 중입니다.",
            ["ProductionRecipeMissing"] = "생산 조합식이 없습니다.",
            ["ProductionResearchLocked"] = "필요한 연구가 완료되지 않았습니다.",
            ["ProductionStockSensorRequired"] = "이 주문 방식에는 재고 감지반이 필요합니다.",
            ["ProductionSupportUnavailable"] = "필요한 생산 지원 설비를 사용할 수 없습니다.",
            ["ProductionTargetStockSatisfied"] = "목표 재고량을 이미 충족했습니다.",
            ["ProductionUtilitiesUnavailable"] = "필요한 기반 설비를 사용할 수 없습니다.",
            ["ProductionWorkstationMismatch"] = "이 작업대에서는 해당 조합식을 생산할 수 없습니다.",
            ["ProductionWorkstationMissing"] = "생산 작업대가 없습니다.",
            ["RecoveryCompleted"] = "수술 후 회복 완료",
            ["RecoveryObservation"] = "수술 완료 · 회복 관찰 중",
            ["RequiredResearchUnavailable"] = "필요 연구 또는 시설이 준비되지 않았습니다. 연구: {0}, 시설: {1}",
            ["RumorMitigationAlreadyUsed"] = "Day {0}에는 이미 소문을 수습했습니다.",
            ["RunResultEmpty"] = "런 결과가 없습니다.",
            ["RunResultNextRun"] = "다음 런",
            ["ServiceActorAlreadyActive"] = "대상에게 이미 진행 중인 서비스 세션이 있습니다.",
            ["ServiceCapacityFull"] = "서비스 용량이 가득 찼습니다.",
            ["ServiceClosed"] = "서비스가 휴업 중입니다.",
            ["ServiceFeatureMissing"] = "같은 방에 연결된 필수 시설이 없습니다: {0}",
            ["ServiceHubUnavailable"] = "사용 가능한 서비스 시설을 찾을 수 없습니다.",
            ["ServiceModeUnsupported"] = "시설이 선택한 운영 모드를 지원하지 않습니다.",
            ["ServiceProcessContractMissing"] = "공정 {0}에 {1} 운영 계약이 없습니다.",
            ["ServiceProcessIdMissing"] = "서비스 공정 ID가 필요합니다.",
            ["ServiceSessionMissing"] = "활성 서비스 세션을 찾을 수 없습니다.",
            ["ServiceStageIncomplete"] = "서비스 이용이 끝나기 전에는 결제를 확정할 수 없습니다.",
            ["ServiceStageNotAllowed"] = "현재 계약에서 단계 {0}을 진행할 수 없습니다.",
            ["ServiceSupportUnpowered"] = "보조 시설에 전력이 없습니다: {0}",
            ["SurgeryAnatomyFamilyUnsupported"] = "이 수술은 해당 해부 계열을 지원하지 않습니다. 계열: {0}",
            ["SurgeryConstructProcedureBiologicalMismatch"] = "구성체 정비 절차는 생물 대상에게 적용할 수 없습니다.",
            ["SurgeryConstructProcedureRequired"] = "구성체 대상에는 정비 절차가 필요합니다.",
            ["SurgeryCorpseMissing"] = "수술 대상 사체 물품을 찾을 수 없습니다. 사체: {0}",
            ["SurgeryCorpseStale"] = "사체가 추출에 사용할 수 없을 만큼 부패했습니다.",
            ["SurgeryDoctorAlreadyAssigned"] = "다른 집도의가 이미 수술을 진행 중입니다. 집도의: {0}",
            ["SurgeryEffectFailed"] = "수술 효과 적용에 실패했습니다. 효과: {0}",
            ["SurgeryEffectHandlerMissing"] = "등록되지 않은 수술 효과입니다. 효과: {0}",
            ["SurgeryEnvironmentUnsafe"] = "수술 환경이 안전하지 않습니다. 주문: {0}",
            ["SurgeryExtractionAlreadyRecorded"] = "해당 부위의 추출 기록이 이미 존재합니다. 사체: {0}, 부위: {1}",
            ["SurgeryFacilityMissing"] = "수술 시설을 찾을 수 없습니다. 시설: {0}",
            ["SurgeryFacilityOrProcedureMissing"] = "수술 시설 또는 절차가 사라졌습니다.",
            ["SurgeryFacilityUnavailable"] = "수술 시설을 사용할 수 없습니다. 시설: {0}",
            ["SurgeryLivingSubjectUnavailable"] = "생존 수술 대상을 찾을 수 없습니다. 대상: {0}",
            ["SurgeryMaterialUnavailable"] = "수술 재료를 사용할 수 없습니다. 물품: {0}",
            ["SurgeryNodeAlreadyExtracted"] = "대상 해부 부위는 이미 추출되었습니다. 부위: {0}",
            ["SurgeryOperatorIneligible"] = "현재 작업자는 수술을 집도할 수 없습니다.",
            ["SurgeryOperatorMissing"] = "집도의가 없습니다.",
            ["SurgeryOperatorSkillInsufficient"] = "집도 숙련이 부족합니다. 필요: {0}, 현재: {1}",
            ["SurgeryOperatorStatInsufficient"] = "집도 능력치가 부족합니다. 능력치: {0}, 필요: {1}, 현재: {2}",
            ["SurgeryOrderMissing"] = "진행 가능한 수술 주문을 찾을 수 없습니다. 주문: {0}",
            ["SurgeryOutcomeFailed"] = "수술 결과가 실패했습니다. 심각도: {0}",
            ["SurgeryPartKindMismatch"] = "선택한 부품 종류가 수술 절차와 맞지 않습니다.",
            ["SurgeryPartNodeMismatch"] = "선택한 부품이 대상 해부 부위와 맞지 않습니다.",
            ["SurgeryPartUnavailable"] = "사용 가능한 장기 또는 보조 부품을 선택해야 합니다.",
            ["SurgeryPreferredDoctorInvalid"] = "지정한 집도의를 찾거나 배정할 수 없습니다. 집도의: {0}",
            ["SurgeryPreferredDoctorOnly"] = "지정된 집도의만 이 수술을 진행할 수 있습니다. 집도의: {0}",
            ["SurgeryProcedureMissing"] = "수술 절차를 찾을 수 없습니다. 절차: {0}",
            ["SurgeryResearchIncomplete"] = "필요한 수술 연구가 완료되지 않았습니다. 연구: {0}",
            ["SurgeryResearchStateUnavailable"] = "수술 연구 상태를 확인할 수 없습니다.",
            ["SurgeryReservedDoctorMismatch"] = "예약된 집도의가 아닙니다. 집도의: {0}",
            ["SurgeryRiskEnvironmentAdjusted"] = "환경 누적 {9}단계 · 성공 -{5:P1} · 감염 +{6:P1} · 출혈 +{7:P1} · 장기 손상 +{8:P1}",
            ["SurgeryRiskEvaluated"] = "성공 {0:P1} · 감염 {1:P1} · 출혈 {2:P1} · 장기 손상 {3:P1} · 사망 {4:P1}",
            ["SurgeryRiskProcedureMissing"] = "수술 절차가 없어 위험도를 계산할 수 없습니다.",
            ["SurgerySelfOperationForbidden"] = "환자는 자신의 수술을 집도할 수 없습니다.",
            ["SurgerySpeciesUnsupported"] = "이 수술은 해당 종족을 지원하지 않습니다. 종족: {0}",
            ["SurgeryStaffOnly"] = "사장 또는 직원만 수술을 집도할 수 있습니다.",
            ["SurgerySubjectAlreadyScheduled"] = "대상에게 이미 진행 중인 수술 주문이 있습니다. 대상: {0}",
            ["SurgerySubjectInvalid"] = "수술 대상이 유효하지 않습니다.",
            ["SurgerySubjectKindUnsupported"] = "이 수술은 해당 대상 종류를 지원하지 않습니다.",
            ["SurgerySubjectMaintenanceOnly"] = "{0}은(는) 생물 수술 대신 정비 절차가 필요합니다.",
            ["SurgeryTargetNodeMissing"] = "대상 해부 부위를 선택해야 합니다.",
            ["SurgeryTargetNodeUnavailable"] = "대상에게 해당 해부 부위가 없습니다. 부위: {0}",
            ["SurgeryTransportCarrierMismatch"] = "예약된 운반자가 아닙니다. 운반자: {0}",
            ["SurgeryTransportOrderMissing"] = "야생동물 환자 운반 주문을 찾을 수 없습니다. 주문: {0}",
            ["SurgeryTransportUnavailable"] = "야생동물 환자를 운반할 수 없습니다. 대상: {0}",
            ["SurgeryWildlifeSubjectUnavailable"] = "생존 야생동물 수술 대상을 찾을 수 없습니다. 대상: {0}",
            ["SurvivalCookingUnsupported"] = "조리 가능한 시설이 아닙니다. 시설: {0}",
            ["SurvivalFoodStockMissing"] = "조리할 식재료가 부족합니다. 필요 수량: {0}",
            ["SurvivalFuelStockMissing"] = "연료가 부족합니다. 필요 수량: {0}",
            ["SurvivalOutputUnavailable"] = "생산품을 배치할 공간이 없습니다. 아이템: {0}",
            ["SurvivalRefuelUnsupported"] = "연료를 보충할 수 있는 시설이 아닙니다. 시설: {0}",
            ["SurvivalTargetFacilityMissing"] = "생존 작업 대상 시설이 없습니다.",
            ["SurvivalTreatmentMaterialMissing"] = "치료 재료가 부족합니다.",
            ["SurvivalTreatmentTargetMissing"] = "치료할 대상이 없습니다. 시설: {0}",
            ["SurvivalTreatmentUnsupported"] = "치료 가능한 시설이 아닙니다. 시설: {0}",
            ["SurvivalWaterFrozen"] = "추위 때문에 물길이 막혔습니다. 시설: {0}",
            ["SurvivalWaterSourceUnsupported"] = "물을 얻을 수 있는 시설이 아닙니다. 시설: {0}",
            ["SurvivalWorkUnsupported"] = "지원하지 않는 생존 작업입니다. 작업: {0}",
            ["SuturingInProgress"] = "봉합 중",
            ["TrailCharmMissing"] = "사용 가능한 길잡이 부적이 없습니다.",
            ["WasteFeedBufferUnavailable"] = "목적지 버퍼에 도착한 폐기물 사료가 없습니다.",
            ["WasteFeedDeliveryFailed"] = "폐기물 사료 운반을 요청하지 못했습니다.",
            ["WasteFeedUnavailable"] = "현재 정책에 맞는 안전한 폐기물 사료가 없습니다.",
            ["WastePolicyInvalid"] = "폐기물 처리 정책이 올바르지 않습니다.",
            ["WastePolicyUnsupported"] = "이 폐기물 원산지는 선택한 처리 방식을 지원하지 않습니다.",
            ["WildlifePatientMissing"] = "살아 있는 동물 환자를 찾을 수 없습니다.",
            ["WildlifePatientReady"] = "동물 환자 입실 완료",
            ["WildlifePatientReturnCompleted"] = "동물 환자 우리 복귀 완료",
            ["WildlifePatientReturning"] = "동물 환자를 우리로 돌려보내는 중",
            ["WildlifePatientTransporting"] = "직원이 동물 환자를 수술실로 운반 중",
            ["WildlifeRestraintRequired"] = "비동의 동물은 먼저 제압하고 포획해야 합니다.",
        };

    [MenuItem("Tools/DungeonStory/Content/Update Domain Failure Localization")]
    public static void Rebuild()
    {
        RequireNoDirtyAssets(GetPotentialOutputPathsForPreflight());
        HashSet<string> touchedPaths = new(StringComparer.Ordinal);
        try
        {
            IReadOnlyList<string> outputPaths = RebuildWithoutSaving(
                path => touchedPaths.Add(path));
            SaveOwnedOutputs(outputPaths);
            AssetDatabase.Refresh();
            ReleaseRuntimeTableAfterSave();
            Debug.Log(
                $"DomainFailures localization synchronized: {GetRequiredKeys().Length} keys, ko/en parity complete.");
        }
        catch (Exception exception)
        {
            string touched = touchedPaths.Count == 0
                ? "<none recorded>"
                : string.Join(", ", touchedPaths.OrderBy(path => path, StringComparer.Ordinal));
            Debug.LogError(
                "DomainFailures localization synchronization failed. Changed or created "
                + $"outputs may require review. Recorded outputs=[{touched}]. "
                + $"Failure={exception.GetType().Name}: {exception.Message}");
            throw;
        }
    }

    internal static IReadOnlyList<string> RebuildWithoutSaving(
        Action<string> recordTouchedOutput = null)
    {
        ValidateAuthoredContracts();

        HashSet<string> changedOutputPaths = new(StringComparer.Ordinal);
        StringTableCollection collection =
            LocalizationEditorSettings.GetStringTableCollection(CollectionName)
            ?? throw new InvalidOperationException(
                $"String Table collection '{CollectionName}' is missing.");
        Locale koreanLocale = LocalizationEditorSettings.GetLocale("ko")
            ?? throw new InvalidOperationException("Korean locale is missing.");
        StringTable korean = collection.GetTable(koreanLocale.Identifier)
            as StringTable
            ?? throw new InvalidOperationException(
                "DomainFailures Korean String Table is missing.");
        Locale englishLocale = EnsureEnglishLocale(
            koreanLocale,
            changedOutputPaths,
            recordTouchedOutput,
            out bool englishLocaleCreated);
        StringTable english = collection.GetTable(englishLocale.Identifier) as StringTable;
        bool englishTableCreated = english == null;
        if (englishTableCreated)
        {
            ReportPotentialTouched(recordTouchedOutput, collection);
            ReportPotentialTouched(recordTouchedOutput, EnglishTablePath);
            ReportPotentialAddressableOutputs(
                recordTouchedOutput,
                collection.SharedData,
                korean,
                englishLocale);
            try
            {
                english = collection.AddNewTable(
                    englishLocale.Identifier,
                    EnglishTablePath) as StringTable;
            }
            catch
            {
                ReportPotentialTouched(recordTouchedOutput, collection);
                ReportPotentialTouched(recordTouchedOutput, EnglishTablePath);
                ReportPotentialAddressableOutputs(
                    recordTouchedOutput,
                    collection.SharedData,
                    korean,
                    AssetDatabase.LoadMainAssetAtPath(EnglishTablePath),
                    englishLocale);
                throw;
            }
            if (english == null)
            {
                throw new InvalidOperationException(
                    "DomainFailures English String Table could not be created.");
            }

            RecordChangedOutput(changedOutputPaths, recordTouchedOutput, collection);
            EditorUtility.SetDirty(collection);
            RecordChangedOutput(changedOutputPaths, recordTouchedOutput, english);
            RecordDirtyAddressableOutputs(
                changedOutputPaths,
                recordTouchedOutput,
                collection.SharedData,
                korean,
                english,
                englishLocale);
        }

        string[] requiredKeys = GetRequiredKeys();
        foreach (string key in requiredKeys)
        {
            bool sharedKeyMissing =
                collection.SharedData.GetId(key) == SharedTableData.EmptyId;
            bool koreanChanged = EnsureEntry(
                korean,
                key,
                RequireAuthored(Korean, key, "ko"),
                () =>
                {
                    RecordChangedOutput(
                        changedOutputPaths,
                        recordTouchedOutput,
                        korean);
                    if (sharedKeyMissing)
                    {
                        RecordChangedOutput(
                            changedOutputPaths,
                            recordTouchedOutput,
                            collection.SharedData);
                    }
                });
            bool englishChanged = EnsureEntry(
                english,
                key,
                RequireAuthored(English, key, "en"),
                () =>
                {
                    RecordChangedOutput(
                        changedOutputPaths,
                        recordTouchedOutput,
                        english);
                    if (sharedKeyMissing)
                    {
                        RecordChangedOutput(
                            changedOutputPaths,
                            recordTouchedOutput,
                            collection.SharedData);
                    }
                });

            if (koreanChanged)
            {
                EditorUtility.SetDirty(korean);
            }
            if (englishChanged)
            {
                EditorUtility.SetDirty(english);
            }
            if (sharedKeyMissing && (koreanChanged || englishChanged))
            {
                EditorUtility.SetDirty(collection.SharedData);
            }
        }
        ValidateTablesOrThrow(korean, english);

        if (englishLocaleCreated || englishTableCreated)
        {
            RecordDirtyAddressableOutputs(
                changedOutputPaths,
                recordTouchedOutput,
                collection.SharedData,
                korean,
                english,
                englishLocale);
        }

        return changedOutputPaths
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    internal static IReadOnlyList<string> GetPotentialOutputPathsForPreflight()
    {
        StringTableCollection collection =
            LocalizationEditorSettings.GetStringTableCollection(CollectionName)
            ?? throw new InvalidOperationException(
                $"String Table collection '{CollectionName}' is missing.");
        Locale koreanLocale = LocalizationEditorSettings.GetLocale("ko")
            ?? throw new InvalidOperationException("Korean locale is missing.");
        Locale englishLocale = LocalizationEditorSettings.GetLocale("en");
        StringTable korean = collection.GetTable(koreanLocale.Identifier)
            as StringTable
            ?? throw new InvalidOperationException(
                "DomainFailures Korean String Table is missing.");
        StringTable english = englishLocale == null
            ? null
            : collection.GetTable(englishLocale.Identifier) as StringTable;

        HashSet<string> paths = new(StringComparer.Ordinal);
        AddOutputPath(paths, collection.SharedData);
        AddOutputPath(paths, korean);
        AddOutputPath(paths, english);
        if (englishLocale == null || english == null)
        {
            AddOutputPath(paths, collection);
            AddAddressableOutputs(
                paths,
                collection.SharedData,
                korean,
                englishLocale,
                koreanLocale);
        }
        return paths.OrderBy(path => path, StringComparer.Ordinal).ToArray();
    }

    internal static string GetCanonicalProvenanceInput()
    {
        ValidateAuthoredContracts();
        return string.Join(
            "\n",
            GetRequiredKeys().Select(key =>
                key
                + "\tko=" + RequireAuthored(Korean, key, "ko")
                + "\ten=" + RequireAuthored(English, key, "en")));
    }

    private static void RequireNoDirtyAssets(IEnumerable<string> candidatePaths)
    {
        string[] dirtyPaths = candidatePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .Select(path => new
            {
                Path = path,
                Asset = AssetDatabase.LoadMainAssetAtPath(path)
            })
            .Where(entry => entry.Asset != null && EditorUtility.IsDirty(entry.Asset))
            .Select(entry => entry.Path)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (dirtyPaths.Length > 0)
        {
            throw new InvalidOperationException(
                "Localization migration-owned assets already have unsaved changes. "
                + "Save or revert them before rebuilding localization:\n"
                + string.Join("\n", dirtyPaths));
        }
    }

    private static void SaveOwnedOutputs(IEnumerable<string> outputPaths)
    {
        foreach (string path in outputPaths
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null)
            {
                AssetDatabase.SaveAssetIfDirty(asset);
            }
        }
    }

    private static void AddAddressableOutputs(
        ICollection<string> outputPaths,
        params UnityEngine.Object[] localizedAssets)
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            return;
        }

        AddOutputPath(outputPaths, settings);
        foreach (UnityEngine.Object localizedAsset in localizedAssets)
        {
            string assetPath = AssetDatabase.GetAssetPath(localizedAsset);
            string guid = string.IsNullOrWhiteSpace(assetPath)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(assetPath);
            AddressableAssetEntry entry = string.IsNullOrWhiteSpace(guid)
                ? null
                : settings.FindAssetEntry(guid);
            AddressableAssetGroup group = entry?.parentGroup;
            AddOutputPath(outputPaths, group);
            if (group == null)
            {
                continue;
            }

            foreach (AddressableAssetGroupSchema schema in group.Schemas)
            {
                AddOutputPath(outputPaths, schema);
            }
        }
    }

    private static void ReportPotentialAddressableOutputs(
        Action<string> recordTouchedOutput,
        params UnityEngine.Object[] localizedAssets)
    {
        if (recordTouchedOutput == null)
        {
            return;
        }
        HashSet<string> paths = new(StringComparer.Ordinal);
        AddAddressableOutputs(paths, localizedAssets);
        foreach (string path in paths)
        {
            recordTouchedOutput(path);
        }
    }

    private static void RecordDirtyAddressableOutputs(
        ICollection<string> changedOutputPaths,
        Action<string> recordTouchedOutput,
        params UnityEngine.Object[] localizedAssets)
    {
        HashSet<string> candidates = new(StringComparer.Ordinal);
        AddAddressableOutputs(candidates, localizedAssets);
        foreach (string path in candidates)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null && EditorUtility.IsDirty(asset))
            {
                RecordChangedOutput(
                    changedOutputPaths,
                    recordTouchedOutput,
                    path);
            }
        }
    }

    private static void AddOutputPath(
        ICollection<string> outputPaths,
        UnityEngine.Object asset)
    {
        if (asset == null)
        {
            return;
        }

        string path = AssetDatabase.GetAssetPath(asset);
        if (!string.IsNullOrWhiteSpace(path))
        {
            outputPaths.Add(path.Replace('\\', '/'));
        }
    }

    private static void RecordChangedOutput(
        ICollection<string> changedOutputPaths,
        Action<string> recordTouchedOutput,
        UnityEngine.Object asset)
    {
        string path = asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset);
        RecordChangedOutput(changedOutputPaths, recordTouchedOutput, path);
    }

    private static void RecordChangedOutput(
        ICollection<string> changedOutputPaths,
        Action<string> recordTouchedOutput,
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        string normalized = path.Replace('\\', '/');
        changedOutputPaths.Add(normalized);
        recordTouchedOutput?.Invoke(normalized);
    }

    private static void ReportPotentialTouched(
        Action<string> recordTouchedOutput,
        UnityEngine.Object asset)
    {
        if (asset != null)
        {
            ReportPotentialTouched(recordTouchedOutput, AssetDatabase.GetAssetPath(asset));
        }
    }

    private static void ReportPotentialTouched(
        Action<string> recordTouchedOutput,
        string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            recordTouchedOutput?.Invoke(path.Replace('\\', '/'));
        }
    }

    internal static void ReleaseRuntimeTableAfterSave()
    {
        if (LocalizationSettings.HasSettings)
        {
            LocalizationSettings.StringDatabase.ReleaseTable(CollectionName);
        }
    }

    public static string[] GetRequiredKeys() =>
        Enum.GetValues(typeof(FailureCode))
            .Cast<FailureCode>()
            .Where(code => code != FailureCode.None)
            .Select(code => code.ToString())
            .Concat(Enum.GetValues(typeof(CharacterConsumablesFailureCode))
                .Cast<CharacterConsumablesFailureCode>()
                .Where(code => code != CharacterConsumablesFailureCode.None)
                .Select(code => code.ToString()))
            .Concat(Enum.GetValues(typeof(SurgeryStatusCode))
                .Cast<SurgeryStatusCode>()
                .Where(code => code != SurgeryStatusCode.None)
                .Select(code => code.ToString()))
            .Concat(Enum.GetValues(typeof(SurgeryRiskSummaryCode))
                .Cast<SurgeryRiskSummaryCode>()
                .Where(code => code != SurgeryRiskSummaryCode.None)
                .Select(code => code.ToString()))
            .Concat(Enum.GetValues(typeof(CharacterMedicalStatusCode))
                .Cast<CharacterMedicalStatusCode>()
                .Where(code => code != CharacterMedicalStatusCode.Unknown)
                .Select(code => "CharacterMedicalStatus" + code))
            .Concat(Enum.GetValues(typeof(InfrastructureStatusCode))
                .Cast<InfrastructureStatusCode>()
                .Where(code => code != InfrastructureStatusCode.None)
                .Select(code => "InfrastructureStatus" + code))
            .Concat(new[] { "RunResultEmpty", "RunResultNextRun" })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

    private static Locale EnsureEnglishLocale(
        Locale koreanLocale,
        ICollection<string> changedOutputPaths,
        Action<string> recordTouchedOutput,
        out bool createdLocale)
    {
        Locale existing = LocalizationEditorSettings.GetLocale("en");
        if (existing != null)
        {
            createdLocale = false;
            return existing;
        }

        createdLocale = true;
        Locale created = Locale.CreateLocale(SystemLanguage.English);
        created.name = "Locale_en";
        ReportPotentialTouched(recordTouchedOutput, EnglishLocalePath);
        AssetDatabase.CreateAsset(created, EnglishLocalePath);
        RecordChangedOutput(changedOutputPaths, recordTouchedOutput, created);
        ReportPotentialAddressableOutputs(recordTouchedOutput, koreanLocale, created);
        try
        {
            LocalizationEditorSettings.AddLocale(created);
        }
        catch
        {
            ReportPotentialAddressableOutputs(recordTouchedOutput, koreanLocale, created);
            throw;
        }
        RecordDirtyAddressableOutputs(
            changedOutputPaths,
            recordTouchedOutput,
            koreanLocale,
            created);
        return created;
    }

    private static bool EnsureEntry(
        StringTable table,
        string key,
        string value,
        Action beforeMutation)
    {
        StringTableEntry existing = table.GetEntry(key);
        if (existing == null)
        {
            beforeMutation?.Invoke();
            table.AddEntry(key, value);
            return true;
        }

        if (!string.Equals(existing.Value, value, StringComparison.Ordinal))
        {
            beforeMutation?.Invoke();
            existing.Value = value;
            return true;
        }
        return false;
    }

    public static void ValidateAuthoredContracts()
    {
        string[] requiredKeys = GetRequiredKeys();
        ValidateAuthoredLocale("ko", Korean, requiredKeys);
        ValidateAuthoredLocale("en", English, requiredKeys);
        foreach (string key in requiredKeys)
        {
            string korean = RequireAuthored(Korean, key, "ko");
            string english = RequireAuthored(English, key, "en");
            DomainFailureLocalizationFormatContract.ValidateTemplatePair(
                key,
                korean,
                english);
        }
    }

    public static void ValidateTablesOrThrow(
        StringTable koreanTable,
        StringTable englishTable)
    {
        if (koreanTable == null)
        {
            throw new ArgumentNullException(nameof(koreanTable));
        }
        if (englishTable == null)
        {
            throw new ArgumentNullException(nameof(englishTable));
        }

        ValidateAuthoredContracts();
        foreach (string key in GetRequiredKeys())
        {
            ValidateTableEntry(
                koreanTable,
                key,
                RequireAuthored(Korean, key, "ko"),
                "ko");
            ValidateTableEntry(
                englishTable,
                key,
                RequireAuthored(English, key, "en"),
                "en");
        }
    }

    private static void ValidateAuthoredLocale(
        string locale,
        IReadOnlyDictionary<string, string> authored,
        IReadOnlyCollection<string> requiredKeys)
    {
        HashSet<string> required = new(requiredKeys, StringComparer.Ordinal);
        string[] missing = required.Where(key => !authored.ContainsKey(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        string[] unexpected = authored.Keys.Where(key => !required.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        if (missing.Length > 0 || unexpected.Length > 0)
        {
            throw new InvalidOperationException(
                $"DomainFailures {locale} authored key set differs from the required contract. "
                + $"Missing=[{string.Join(", ", missing)}], "
                + $"Unexpected=[{string.Join(", ", unexpected)}].");
        }

        foreach (KeyValuePair<string, string> entry in authored)
        {
            if (string.IsNullOrWhiteSpace(entry.Value))
            {
                throw new InvalidOperationException(
                    $"DomainFailures {locale} entry '{entry.Key}' is blank.");
            }
            if (string.Equals(locale, "en", StringComparison.Ordinal)
                && entry.Value.Any(IsHangulCharacter))
            {
                throw new InvalidOperationException(
                    $"DomainFailures en entry '{entry.Key}' contains Hangul. "
                    + "This usually indicates mojibake or cross-locale text leakage.");
            }
            DomainFailureLocalizationFormatContract.ValidateNoMojibake(
                entry.Key,
                locale,
                entry.Value);
        }
    }

    private static bool IsHangulCharacter(char character) =>
        character is >= '\u1100' and <= '\u11FF'
        || character is >= '\u3130' and <= '\u318F'
        || character is >= '\uA960' and <= '\uA97F'
        || character is >= '\uAC00' and <= '\uD7AF'
        || character is >= '\uD7B0' and <= '\uD7FF';

    private static void ValidateTableEntry(
        StringTable table,
        string key,
        string expected,
        string locale)
    {
        StringTableEntry entry = table.GetEntry(key);
        if (entry == null)
        {
            throw new InvalidOperationException(
                $"DomainFailures {locale} String Table is missing '{key}'.");
        }
        if (!string.Equals(entry.Value, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"DomainFailures {locale} entry '{key}' differs from its authored source.");
        }
    }

    private static string RequireAuthored(
        IReadOnlyDictionary<string, string> authored,
        string key,
        string locale)
    {
        if (!authored.TryGetValue(key, out string value)
            || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"DomainFailures {locale} authored entry '{key}' is missing.");
        }
        return value;
    }
}
#endif
