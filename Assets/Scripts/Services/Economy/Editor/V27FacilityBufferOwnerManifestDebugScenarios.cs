#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class V27FacilityBufferOwnerManifestDebugScenarios
{
    public const string CsvPath =
        "Artifacts/QA/v27-facility-buffer-owner-manifest.csv";
    public const string ReportPath =
        "Artifacts/QA/v27-facility-buffer-owner-manifest.txt";

    private const string SelfPath =
        "Assets/Scripts/Services/Economy/Editor/V27FacilityBufferOwnerManifestDebugScenarios.cs";

    private static readonly Regex DeliveryInvocationPattern = new Regex(
        @"\.\s*(TryRequestFacilityDelivery|TryRequestItemDelivery|TryRequestStackDelivery)\s*\(",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ProductionOutputHandlerRegistrationPattern =
        new Regex(
            @"Register<(?<handler>[A-Za-z0-9_]+)>\s*\([^;]+?\.As<IProductionOutputHandler>\s*\(\s*\)",
            RegexOptions.CultureInvariant
            | RegexOptions.Compiled
            | RegexOptions.Singleline);

    private static readonly IReadOnlyDictionary<string, string> DeliveryCallsiteOwners =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Assets/Scripts/Models/Economy/Content/WasteProcessingRuntime.cs"] = "economy.waste-processing",
            ["Assets/Scripts/Services/Captivity/CaptivityInteractionMaterialRuntime.cs"] = "captivity.interaction",
            ["Assets/Scripts/Services/Captivity/CaptivityRuntime.cs"] = "captivity.care-labor",
            ["Assets/Scripts/Services/Captivity/WildlifeCaptureRuntime.cs"] = "captivity.wildlife-care",
            ["Assets/Scripts/Services/Character/Work/WorkConstructionInputOwnerRuntime.cs"] = "work.construction",
            ["Assets/Scripts/Services/Combat/CharacterMedicalSupplyCoordinator.cs"] = "medical.character-supply",
            ["Assets/Scripts/Services/Combat/CombatEquipmentCraftInputDestinationRuntime.cs"] = "combat.equipment-crafting",
            ["Assets/Scripts/Services/Combat/EquipmentEvolutionInputOwnerRuntime.cs"] = "combat.equipment-evolution",
            ["Assets/Scripts/Services/Combat/EquipmentMaintenanceRuntime.cs"] = "combat.equipment-maintenance",
            ["Assets/Scripts/Services/Combat/EquipmentModuleInputOwnerRuntime.cs"] = "combat.equipment-module",
            ["Assets/Scripts/Services/Defense/DefenseFacilityRuntime.cs"] = "combat.defense-facility",
            ["Assets/Scripts/Services/Economy/CertifiedSeedRuntime.cs"] = "economy.certified-seed",
            ["Assets/Scripts/Services/Economy/CropEcologyRuntime.cs"] = "economy.crop-plot",
            ["Assets/Scripts/Services/Economy/Planning/RegionalSupplyContractApplicationAdapter.cs"] = "economy.regional-contract",
            ["Assets/Scripts/Services/Economy/Planning/ResourceStockPolicyRuntime.cs"] = "economy.stock-policy",
            ["Assets/Scripts/Services/Economy/ProductionItemGateway.cs"] = "economy.production",
            ["Assets/Scripts/Services/Economy/Waste/WasteProcessingPortAdapters.cs"] = "adapter.waste-processing",
            ["Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionInputOwnerRuntime.cs"] = "facility.evolution",
            ["Assets/Scripts/Services/Infrastructure/BlueprintResearchRuntime.cs"] = "research.blueprint-archive",
            ["Assets/Scripts/Services/Infrastructure/Industrial/ElectricalNetworkRuntime.cs"] = "infrastructure.electrical",
            ["Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs"] = "infrastructure.fluid",
            ["Assets/Scripts/Services/Infrastructure/Industrial/ProcessFluidUseRuntime.cs"] = "infrastructure.process-fluid",
            ["Assets/Scripts/Services/Infrastructure/KnowledgeResidueProcessingRuntime.cs"] = "research.knowledge-residue",
            ["Assets/Scripts/Services/Items/ItemTransferService.cs"] = "adapter.item-transfer",
            ["Assets/Scripts/Services/Items/DurableFacilityEquipmentSlotRuntime.cs"] = "adapter.durable-facility-equipment",
            ["Assets/Scripts/Services/Items/WorldItemStackRuntime.cs"] = "adapter.world-item-runtime",
            ["Assets/Scripts/Services/Medical/SurgeryLogisticsRuntime.cs"] = "medical.surgery",
            ["Assets/Scripts/Services/Medical/SurgicalPartRuntime.cs"] = "medical.surgical-part-storage",
            ["Assets/Scripts/Services/Survival/CharacterConsumablesApplicationAdapters.cs"] = "survival.character-consumables"
        };

    private static readonly OwnerRow[] Rows =
    {
        Input("economy.production", "production:{billId}", "ProductionInputLogisticsService", "exact ReservedTarget/LiveFacility claim", "per-bill 2-3 cycle exact gram bound", "ProductionBill V13 + Physical Items + HaulIntent", "atomic destination/carry release", "migrated", "Assets/Scripts/Services/Economy/ProductionInputLogisticsService.cs"),
        Input("economy.production-sensor", "production-sensor:{facilityId}", "ProductionStockSensorRuntime|ProductionStockSensorDestinationAuthorityRuntime", "exact LiveFacility claim; exact-stack managed admission", "one authored stock-sensor panel in exact current grams", "Production V14 sensor owner + derived current-facility claim/profile + Physical Items", "atomic destination release; mutation rollback restores exact authority", "migrated", "Assets/Scripts/Models/Economy/Content/ProductionStockSensorRuntime.cs"),
        Input("economy.grand-project", "facility-input:exact:economy.grand-project:{escapedProjectId}", "GrandProjectRuntime|EconomyProjectInputOwnerRuntime", "exact LiveFacility claim; ExactGramRequired policy", "exact positive authored project requirement vector in current grams", "Grand Project current format + projection fingerprint/revision + typed Sink + Physical Items child authority", "carried-aware terminal release before paired revoke", "migrated", "Assets/Scripts/Services/Economy/EconomyProjectInputOwnerRuntime.cs"),
        Input("economy.regional-contract", "facility-input:exact:economy.regional-contract:{escapedContractId}", "RegionalSupplyContractRuntime|EconomyProjectInputOwnerRuntime", "owner-neutral exact ReservedTarget claim; ExactGramRequired policy", "exact positive contract requirement vector in current grams", "Regional Contract current format + projection fingerprint/revision + typed Transfer + Physical Items child authority", "carried-aware terminal release before paired revoke", "migrated", "Assets/Scripts/Services/Economy/EconomyProjectInputOwnerRuntime.cs"),
        Input("survival.character-consumables", "facility-input:exact:survival.character-consumables:v1:{meal|recreation-substance}:{escapedFacilityId}:{escapedItemId}", "CharacterConsumablesRuntime|CharacterConsumablesInputOwnerDescriptorSource|CharacterConsumablesInputOwnerRuntime|CharacterConsumablesInputOwnerLifecycleRuntime", "exact LiveFacility claim; ExactGramRequired policy", "one exact current item unit in positive grams per item-specific destination", "Consumables V8 + derived claim/profile restore join + Physical Items child authority", "lost-facility/capability carried-aware release before paired revoke", "migrated", "Assets/Scripts/Services/Survival/CharacterConsumablesInputOwnerRuntime.cs"),
        Input("captivity.interaction", "facility-input:exact:captivity.interaction:v1:{captiveId}:{interactionId}:{facilityId}:{position}:{massRevision}:{capacity}:{fingerprint}", "CaptivityInteractionRuntime|CaptivityInteractionMaterialRuntime|CaptivityInteractionMaterialLifecycleRuntime", "exact LiveFacility claim; ExactGramRequired policy", "category requirement count x maximum current catalog unit grams, with actual delivered exact lots selected at Sink", "Captivity state committed Sink token + claim/profile replacement + Physical Items pending receipt join", "owner-position carried-aware close; restore publish rollback restores prior authority", "migrated", "Assets/Scripts/Services/Captivity/CaptivityInteractionRuntime.cs"),
        Input("captivity.care-labor", "captive-care:{id}|captive-labor-tool:{id}", "CaptivityRuntime|CaptivityCareLaborInputOwnerRuntime", "exact LiveFacility captive-housing claim; ExactGramRequired policy", "maximum current positive Food unit grams | exact current prisoner-work-kit unit grams", "Captivity V3 derived pair + Physical Items child authority; restore participant 218 before shared 220", "owner-position carried-aware release before paired revoke", "migrated", "Assets/Scripts/Services/Captivity/CaptivityCareLaborInputOwnerRuntime.cs"),
        Input("captivity.performer", "delegated:captivity.care-labor", "CaptivityPerformerRuntime", "no independent physical owner", "delegated to captivity.care-labor", "Captivity state-only carePriorityUnlocked", "duplicate delivery chain removed", "removed-duplicate-owner", "Assets/Scripts/Models/Captivity/Core/CaptivityPerformerRuntime.cs"),
        Input("captivity.circus", "facility-input:exact:durable-equipment:captivity.circus:{stagePersistentId}:{assignmentSequence}", "CircusRuntime|CircusPerformanceSupplyRuntime|CircusPerformanceSupplyLifecycleRuntime|DurableFacilityEquipmentSlotRuntime", "exact LiveFacility claim; durable exact-stack slot policy", "one 1,950g performance prop box plus one 3,150g banquet cart = 5,100g", "Circus V4 pending Sink + durable slot + Physical Items child authority", "lost-stage/capability close and carried-aware terminal drain", "migrated", "Assets/Scripts/Services/Captivity/CircusRuntime.cs"),
        Input("captivity.wildlife-care", "facility-input:exact:captivity.wildlife-care:{penPersistentId}", "WildlifeCaptureRuntime|WildlifeCareInputOwnerSource|WildlifeCareInputOwnerRuntime|WildlifeCareInputOwnerLifecycleRuntime", "exact LiveFacility pen claim; ExactGramRequired policy", "active animals x authored daily food/water units x current catalog maximum eligible unit grams", "Wildlife/Circus current format + derived owner pair + Physical Items child authority", "carried-aware shrink/terminal release before paired authority revoke", "migrated", "Assets/Scripts/Services/Captivity/WildlifeCareInputOwnerRuntime.cs"),
        Input("character.career", "durable-equipment:character.career:{academyPersistentId}:1", "CareerApplicationAdapter|DurableFacilityEquipmentSlotRuntime", "exact LiveFacility claim; durable exact-stack slot policy", "policy-derived positive gram profile for one career-ledger lot", "durable slot lifecycle + career aggregate effect + Physical Items child authority", "owner-neutral slot reconcile and terminal lifecycle drain", "migrated", "Assets/Scripts/Services/Character/CareerApplicationAdapter.cs"),
        Input("character.reproduction", "durable-equipment:character.reproduction:{facilityPersistentId}:1", "ReproductionCommandRuntime|DurableFacilityEquipmentSlotRuntime", "exact LiveFacility claim; durable exact-stack slot policy", "policy-derived positive gram profile for one breeding-ledger lot", "durable slot lifecycle + reproduction aggregate effect + Physical Items child authority", "owner-neutral slot reconcile and terminal lifecycle drain", "migrated", "Assets/Scripts/Services/Character/ReproductionCommandRuntime.cs"),
        Input("work.construction", "facility-input:exact:work.construction:{orderId}", "WorkAmountSystem|WorkConstructionInputOwnerRuntime", "exact LiveBuilding ConstructionSite claim; ExactGramRequired policy", "exact positive remaining construction material vector in current grams", "Work Order V8 projection fingerprint/revision + typed material Transfer + Physical Items child authority", "carried-aware cancellation/terminal release before paired revoke", "migrated", "Assets/Scripts/Services/Character/Work/WorkConstructionInputOwnerRuntime.cs"),
        Input("medical.character-supply", "facility-input:exact:medical.character-supply:{orderId}:{destinationSequence}", "CharacterMedicalSupplyDestinationRuntime|CharacterMedicalSupplyCoordinator|CharacterMedicalSupplyDestinationDrainRuntime", "exact LiveFacility claim; ExactGramRequired policy", "one capability-derived treatment medicine or extracted-blood item in exact current grams", "Character Medical V6 + claim/profile + active-1/closed-N upper joins + Physical Items child + raw/candidate cross-save preflight", "owner-neutral carried-aware sequence drain and child-first/upper-last atomic checkpoint collection", "migrated", "Assets/Scripts/Services/Combat/CharacterMedicalSupplyDestinationRuntime.cs"),
        Input("combat.equipment-crafting", "facility-input:exact:combat.equipment-crafting:{orderId}", "CombatEquipmentCraftingRuntime|CombatEquipmentCraftInputDestinationRuntime", "exact LiveFacility claim; ExactGramRequired policy", "current recipe exact material lots in positive frozen grams", "Craft V7 + WIP + exact owner pair + Physical Items child authority", "carried-aware terminal release before paired authority revoke", "migrated", "Assets/Scripts/Services/Combat/CombatEquipmentCraftInputDestinationRuntime.cs"),
        Input("combat.equipment-evolution", "facility-reforge:{orderId}|facility-reattune:{orderId}", "EquipmentEvolutionRuntime|EquipmentEvolutionInputOwnerRuntime", "exact LiveFacility claim; equipment-crafting capability; ExactGramRequired policy", "exact positive equipment/component/material grams with frozen fingerprint and revision", "Equipment Evolution V5 orders + exact owner pair + Physical Items child authority", "carried-aware release before paired revoke", "migrated", "Assets/Scripts/Services/Combat/EquipmentEvolutionInputOwnerRuntime.cs"),
        Input("combat.equipment-maintenance", "equipment-repair:{equipmentInstanceId}", "EquipmentMaintenanceRuntime", "exact LiveFacility claim; exact-stack managed admission", "one repair batch: dynamic unique equipment plus exact material grams", "maintenance save + profile restore + WIP receipt join", "owner-wide terminal close", "migrated", "Assets/Scripts/Services/Combat/EquipmentMaintenanceRuntime.cs"),
        Input("combat.equipment-module", "facility-input:exact:combat.equipment-module:{facilityPersistentId}", "EquipmentModuleRuntime|EquipmentModuleInputOwnerRuntime|EquipmentModuleInputOwnerLifecycleRuntime", "exact LiveFacility claim; ExactGramRequired policy", "exact positive appraisal/module supply vector in current grams", "derived current facility pair + equipment/module custody + restore participant 219 + Physical Items child authority", "carried-aware lost-facility/capability release before paired revoke", "migrated", "Assets/Scripts/Services/Combat/EquipmentModuleInputOwnerRuntime.cs"),
        Input("combat.defense-facility", "facility-input:defense:{facilityId}|facility-input:defense-maintenance:{facilityId}", "DefenseFacilityRuntime|DefenseFacilityInputOwnerRuntime|DefenseFacilityInputOwnerLifecycleRuntime", "exact LiveFacility claim; ExactGramRequired policy", "current exact supply and maintenance item grams with positive-capacity projection", "Defense state/outbox + derived claim/profile restore join + Physical Items child authority", "carried-aware retired-owner release before paired authority revoke", "migrated", "Assets/Scripts/Services/Defense/DefenseFacilityRuntime.cs"),
        Input("economy.certified-seed", "facility-input:exact:economy.certified-seed:{facilityId}:{cropId}:{sequence}", "CertifiedSeedRuntime|CertifiedSeedInputOwnerRuntime", "exact LiveFacility claim; ExactGramRequired policy", "exact seed and certification-kit lots in current positive grams", "CertifiedSeed current format + crop receipt + exact input pair + common output owner + Physical Items child authority", "carried-aware committed/aborted/lost-facility release before paired authority revoke", "migrated", "Assets/Scripts/Services/Economy/CertifiedSeedInputOwnerRuntime.cs"),
        Input("economy.crop-plot", "facility-input:exact:economy.crop-plot:{sow|treatment}:{escapedPersistentPlotId}:{sequence:D8}", "CropPlotRuntime|CropPlotInputOwnerRuntime", "exact LiveFacility claim; ExactGramRequired policy", "exact sow/treatment input lots in current positive grams", "Crop Plot current format + exact owner pair + Physical Items child authority", "carried-aware cancel/completion/destroy/removal release before paired authority revoke", "migrated", "Assets/Scripts/Services/Economy/CropPlotInputOwnerRuntime.cs"),
        Input("economy.stock-policy", "facility-input:exact:economy.stock-policy:{escapedItemId}", "ResourceStockPolicyRuntime|EconomyProjectInputOwnerRuntime", "owner-neutral exact ReservedTarget claim; ExactGramRequired policy", "one authored max-stack lot of the exact policy item in current grams", "Stock Policy current format + projection fingerprint/revision + typed sale Transfer + Physical Items child authority", "carried-aware disable/sale completion release before paired revoke", "migrated", "Assets/Scripts/Services/Economy/EconomyProjectInputOwnerRuntime.cs"),
        Input("facility.evolution", "facility-input:exact:facility.evolution:{modification|recalibration|relocation}:{orderId}", "FacilityInstanceEvolutionRuntime|FacilityEvolutionInputOwnerRuntime", "modification/recalibration exact LiveFacility; relocation owner-neutral ReservedTarget; ExactGramRequired", "exact positive order material/package vector in current grams", "Evolution current format projections + exact package custody + restore participant 219 + Physical Items child authority", "carried-aware terminal release before paired revoke", "migrated", "Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionInputOwnerRuntime.cs"),
        Input("research.blueprint-archive", "research-archive:{facilityPersistentId}", "BlueprintResearchRuntime|ResearchBlueprintArchiveQuery", "exact LiveBuilding claim; ExactGramRequired policy", "authored archive count x maximum exact catalog blueprint unit grams; current 8 x 150g = 1,200g", "Research V6 derived claim/profile restore + Physical Items dependency", "carried-aware owner-position release before paired authority retirement", "migrated", "Assets/Scripts/Services/Infrastructure/ResearchBlueprintArchiveAdapter.cs"),
        Input("infrastructure.climate", "facility-input:exact:durable-equipment:infrastructure.climate:{towerPersistentId}:{assignmentSequence}", "ClimateRuntime|ClimateDurableEquipmentRuntime|ClimateDurableEquipmentLifecycleRuntime|DurableFacilityEquipmentSlotRuntime", "exact LiveFacility claim; durable exact-stack slot policy", "policy-derived positive gram profile for one seasonal almanac and one weather observation kit", "durable slot lifecycle + climate projection + Physical Items child authority", "owner-neutral lost-tower/capability close and carried-aware terminal drain", "migrated", "Assets/Scripts/Services/Infrastructure/Environment/ClimateRuntime.cs"),
        Input("infrastructure.electrical", "power:{nodeId}", "ElectricalNetworkRuntime", "exact LiveBuilding claim; exact-stack managed admission", "common positive-gram profile", "AIHaul; carried 350g save/restore; consumption", "terminal close", "migrated", "Assets/Scripts/Services/Infrastructure/Industrial/ElectricalNetworkRuntime.cs"),
        Input("infrastructure.fluid", "plumbing:manual-water:{fixtureId}|plumbing:water-transfer:{nodeId}", "FluidNetworkRuntime|FluidFacilityInputOwnerAuthority", "manual-water exact LiveBuilding; transfer exact LiveFacility; ExactGramRequired", "exact clean-water unit grams x authored requested units", "Fluid V6 typed Transfer + projection fingerprint/revision + Physical Items child authority", "carried-aware replacement/cancellation release before paired revoke", "migrated", "Assets/Scripts/Services/Infrastructure/Industrial/FluidFacilityInputOwnerAuthority.cs"),
        Input("infrastructure.process-fluid", "plumbing:process-water:{nodeId}:{workTypeId}", "ProcessFluidUseRuntime|FluidFacilityInputOwnerAuthority", "exact LiveFacility claim; ExactGramRequired policy", "exact clean-water item grams x authored process requirement", "production/surgery WIP + Fluid V6 typed Transfer + derived pair", "carried-aware work cancellation release before paired revoke", "migrated", "Assets/Scripts/Services/Infrastructure/Industrial/FluidFacilityInputOwnerAuthority.cs"),
        Input("research.knowledge-residue", "facility-input:exact:research.knowledge-residue:{taskId}:{assignmentSequence:D8}", "KnowledgeResidueDestinationRuntime|KnowledgeResidueProcessingRuntime", "exact LiveFacility claim; ExactGramRequired policy", "one exact captivity:memory-residue item in current positive grams", "Research V6 task projection + claim/profile + Physical Items pending Sink restore join", "invalid/lost owner-position physical release before revoke; exact Sink publication before revoke/ack", "migrated", "Assets/Scripts/Services/Infrastructure/KnowledgeResidueDestinationRuntime.cs"),
        Input("research.arcane-index", "{researchFacilityId}", "ResearchWorkExecutionAdapter|DurableFacilityEquipmentSlotRuntime", "exact LiveFacility claim; durable exact-stack slot policy", "policy-derived positive gram profile for the exact arcane-index equipment lot", "durable slot lifecycle + research projection + Physical Items child authority", "owner-neutral slot reconcile and terminal lifecycle drain", "migrated", "Assets/Scripts/Services/Infrastructure/ResearchWorkExecutionAdapter.cs"),
        Input("invasion.defense-kit", "facility-input:exact:durable-equipment:invasion.defense-kit:{signalPostPersistentId}:{assignmentSequence}", "DefenseCombatExecutor|InvasionDefenseKitSupplyRuntime|InvasionDefenseKitSupplyLifecycleRuntime|DurableFacilityEquipmentSlotRuntime", "exact LiveFacility claim; durable exact-stack slot policy", "one exact 1,150g alliance signal kit", "durable slot delivery/save join + existing Run milestone typed Sink authority + Physical Items child authority", "owner-neutral lost-post/capability close and carried-aware terminal drain", "migrated", "Assets/Scripts/Services/Invasion/DefenseCombatExecutor.cs"),
        Input("invasion.signal-horn", "facility-input:exact:durable-equipment:invasion.signal-horn:{signalPostPersistentId}:{assignmentSequence}", "InvasionDirectorRuntime|InvasionSignalHornDurableEquipmentRuntime|RunInvasionDurableEquipmentLifecycleRuntime|DurableFacilityEquipmentSlotRuntime", "exact LiveFacility claim; durable exact-stack slot policy", "policy-derived positive gram profile for one 2,350g max-stack-1 watch signal horn", "durable slot lifecycle + invasion publish effect + Physical Items child authority", "owner-neutral lost-post/capability close and carried-aware terminal drain", "migrated", "Assets/Scripts/Services/Invasion/InvasionDirectorRuntime.cs"),
        Input("medical.surgery", "surgery-materials:{orderId}", "SurgeryMaterialDestinationRuntime|SurgeryLogisticsRuntime|SurgeryMaterialTerminalRuntime", "exact ReservedTarget/LiveFacility claim; ExactGramRequired policy", "required materials + exact corpse/selected-part positive gram bound", "Surgery V12 + sink/terminal join + raw/candidate restore preflight + durable checkpoint GC", "owner-neutral carried-aware terminal drain and owner/child atomic checkpoint collection", "migrated", "Assets/Scripts/Services/Medical/SurgeryMaterialDestinationRuntime.cs"),
        Input("medical.surgical-part-storage", "{organStorageFacilityId}|surgery-organ-storage-fuel:{facilityId}", "SurgicalPartRuntime|SurgicalPartStorageInputOwnerAuthority", "exact LiveFacility claim; ExactGramRequired policy", "authored organ storage mass plus one exact positive fuel unit", "Surgery current format storage projection + exact typed fuel Sink + restore join", "carried-aware storage/fuel release before paired revoke", "migrated", "Assets/Scripts/Services/Medical/SurgicalPartStorageInputOwnerAuthority.cs"),
        Input("offense.expedition-supply", "expedition:{packageId}", "OffensePreparationService", "exact ReservedTarget claim; exact-stack managed admission", "one package exact cost-vector grams", "expedition package costs + projected claim/profile + Physical Items", "atomic destination release and owner-wide terminal close", "migrated", "Assets/Scripts/Services/Offense/OffensePreparationService.cs"),
        Input("offense.urgent-mitigation", "facility-input:exact:offense.urgent-mitigation:{escapedOrderId}", "OffenseUrgentMitigationRuntime|OffenseUrgentMitigationInputOwnerRuntime", "exact LiveFacility claim; ExactGramRequired policy", "one exact mitigation material line in current positive grams", "Offense current schema V8 + exact owner pair + Physical Items child authority", "carried-aware cancel/facility-loss/completion release before paired authority revoke", "migrated", "Assets/Scripts/Services/Offense/Strategic/OffenseUrgentMitigationInputOwnerRuntime.cs"),
        Input("run.v20-administrative-seal", "facility-input:exact:durable-equipment:run.v20-administrative-seal:{officePersistentId}:{assignmentSequence}", "V20ContentResolutionService|RunAdministrativeSealDurableEquipmentRuntime|RunInvasionDurableEquipmentLifecycleRuntime|DurableFacilityEquipmentSlotRuntime", "exact LiveFacility claim; durable exact-stack slot policy", "policy-derived positive gram profile for one 2,350g max-stack-1 administrative seal", "durable slot lifecycle + campaign effect + Physical Items child authority", "owner-neutral lost-office/capability close and carried-aware terminal drain", "migrated", "Assets/Scripts/Services/Run/V20ContentResolutionService.cs"),
        Input("economy.waste-direct-feed", "facility-input:exact:captivity.wildlife-care:{penPersistentId}", "WasteProcessingRuntime delegated consumer of WildlifeCareInputOwnerRuntime", "validated exact LiveFacility wildlife-care claim/profile", "delegates positive gram capacity to captivity.wildlife-care owner", "waste typed Sink outbox + wildlife-care current-format owner", "no independent authority; arbitrary destination rejected", "delegated-consumer", "Assets/Scripts/Services/Economy/Waste/WasteProcessingPortAdapters.cs"),
        Input("infrastructure.manual-water-fallback", "plumbing:manual-water:{fixtureId}", "FluidNetworkRuntime", "same exact LiveBuilding authority as infrastructure.fluid", "same clean-water projection; not counted twice", "Fluid V6 pending Transfer and save authority", "delegated transfer cancellation", "duplicate-authority", "Assets/Scripts/Services/Infrastructure/Industrial/FluidFacilityInputOwnerAuthority.cs"),

        DirectOutput("combat.module-source", "physical-source-buffer:combat.module-create:{moduleInstanceId}|physical-source-buffer:combat.module-return:{equipmentInstanceId}:{slotIndex}:{moduleInstanceId}", "EquipmentModuleRuntime|EquipmentModulePreparedOutputBinder|PhysicalItemExactSourcePublicationService", "exact unique module component and gram reservation, stack-identity binding, atomic Source publication, targeted facility release, rollback, and save capture guard", "migrated", "Assets/Scripts/Services/Combat/EquipmentModuleRuntime.cs"),
        DirectOutput("run.v20-grant", "physical-source-buffer:run.v20-grant:{actionId}", "V20ContentResolutionService|PhysicalItemExactSourcePublicationService", "exact multi-line gram plan, atomic prepared publication, cost rollback, acknowledged Loose release, and save capture guard", "migrated", "Assets/Scripts/Services/Run/V20ContentResolutionService.cs"),
        DirectOutput("economy.world-resource-output", "physical-source-buffer:economy.world-resource-output:world-resource:{nodeId}:{workTypeId}:{cycleSequence}", "WorldResourceRuntime|WorldResourceOutputPublicationPortAdapter|PhysicalItemExactSourcePublicationService", "key-addressed frozen multi-line outcome, exact gram publication, exact finite/renewable source debit, admission-commit forward retry, acknowledged Loose release, V3 frozen restore validation, and transient save capture guard", "migrated", "Assets/Scripts/Models/Economy/Content/WorldResourceRuntime.cs"),
        Output("exterior.merchant-cart", "physical-source-buffer:exterior.merchant-cart:{incidentId}", "MerchantCartExteriorIncidentHandler|PhysicalItemExactSourcePublicationService", "exact retained batch with acknowledged provenance, exact sink/release, and Exterior V3 + Physical Items V18 restore-authority join", "migrated", "Assets/Scripts/Services/Infrastructure/Exterior/ExteriorIncidentHandlers.cs"),
        new OwnerRow(
            "FacilityBuffer",
            "infrastructure.conveyor",
            "{callerDestinationId}",
            "ConveyorItemGateway.TryCompleteToFacility",
            "arrival-time exact current claim/profile; V3 dynamic destination intent",
            "arrival exact-lot grams; failed unload retains exact InTransit custody",
            "V3 canonical payload plus bidirectional payload/InTransit restore join; route and current authority are re-evaluated after restore",
            "capacity or commit failure preserves InTransit payload and releases transient grams",
            "transport-delegated-exact",
            "Assets/Scripts/Services/Infrastructure/Industrial/ConveyorItemGateway.cs"),

        Output("economy.production-output", "production-output:{facilityId}", "ProductionPreparedOutputExecutionAdapter|StandardDefinitionProductionOutputCapability", "all definition-only recipe outputs are capability-selected into the common prepared-output gram reservation and atomic publication owner; the standard capability is descriptor-only and mixed standard/special vectors fail before RNG or publication", "migrated", "Assets/Scripts/Services/Economy/ProductionPreparedOutputExecutionAdapter.cs"),
        Output("combat.craft-output", "production-output:{facilityId}|sale:quality-rejected", "CombatEquipmentCraftOutputTransaction|ProductionDomainOutputPublicationService", "equipment/ammunition exact component and gram reservation, atomic planned publication, acknowledgement release, restore join and durable rejected-sale settlement", "migrated", "Assets/Scripts/Services/Combat/CombatEquipmentCraftOutputTransaction.cs"),
        Output("environment.apparel-output", "production-output:{facilityId}", "ApparelPhysicalTransaction", "exact unique component and gram reservation, atomic planned publication, restore adoption and acknowledgement", "migrated", "Assets/Scripts/Services/Infrastructure/Environment/ApparelPhysicalTransaction.cs"),
        Output("environment.workwear-output", "production-output:{facilityId}", "EnvironmentalWorkwearProductionOutputHandler|FacilityBufferPlannedOutputPublicationService", "exact unique apparel component, gram reservation, atomic planned publication, durable acknowledgement and idempotent replay", "migrated", "Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalWorkwearProductionOutputHandler.cs"),
        Output("economy.certified-seed-output", "production-output:{facilityId}", "CertifiedSeedRuntime|ProductionDomainOutputPublicationService", "exact seed-lot component, gram reservation, planned publication, common restore ownership join, input/output acknowledgement", "migrated", "Assets/Scripts/Services/Economy/CertifiedSeedRuntime.cs"),
        Output("medical.surgical-part-output", "production-output:{facilityId}", "SurgicalPartProductionOutputHandler|SurgicalPartRuntime", "exact unique component, gram reservation, planned publication, physical join, and acknowledgement", "migrated", "Assets/Scripts/Services/Medical/SurgicalPartProductionOutputHandler.cs")
    };

    [MenuItem("DungeonStory/V27/Physical Mass/Capture Facility Buffer Owner Manifest")]
    public static void RunFromMenu()
    {
        CaptureResult first = Capture();
        CaptureResult second = Capture();
        Require(first.Csv.SequenceEqual(second.Csv),
            "Facility-buffer owner CSV changed between identical captures.");
        Require(first.Report.SequenceEqual(second.Report),
            "Facility-buffer owner report changed between identical captures.");
        V27BalanceArtifactWriter.WriteIfDifferent(CsvPath, stream =>
            stream.Write(first.Csv, 0, first.Csv.Length));
        V27BalanceArtifactWriter.WriteIfDifferent(ReportPath, stream =>
            stream.Write(first.Report, 0, first.Report.Length));
        Debug.Log(first.Summary);
    }

    public static string RunAll() =>
        Encoding.UTF8.GetString(Capture().Report);

    public static void RequireClassificationCoverage()
    {
        CaptureResult result = Capture();
        Require(result.UnclassifiedCallsites == 0,
            "Facility-buffer delivery inventory contains unclassified callsites.");
    }

    public static void RequireOutputClosure()
    {
        // Capture owns the current-source ratchet: every output row discovered
        // from the owner registry must be migrated, with output remaining 0
        // and bypass/orphan/unclassified 0. The live FacilityBuffer input-owner
        // registry remains the independent Batch C denominator.
        CaptureResult result = Capture();
        Require(result.Bypass == 0
                && result.Orphan == 0
                && result.UnclassifiedCallsites == 0,
            "Facility-output closure contains bypass, orphan or unclassified authority.");
    }

    public static void RequireFullyMigrated()
    {
        CaptureResult result = Capture();
        Require(result.Remaining == 0 && result.Bypass == 0 && result.Orphan == 0,
            $"Facility-buffer migration is incomplete: remaining={result.Remaining}; "
            + $"bypass={result.Bypass}; orphan={result.Orphan}.");
    }

    private static CaptureResult Capture()
    {
        string root = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        DeliveryCallsite[] callsites = CaptureDeliveryCallsites(root);
        Require(callsites.Length > 0,
            "Current source contains no FacilityBuffer delivery invocation.");
        string[] deliveryInvocationFiles = callsites
            .Select(value => value.SourcePath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        string[] unclassified = callsites
            .Select(value => value.SourcePath)
            .Distinct(StringComparer.Ordinal)
            .Where(path => !DeliveryCallsiteOwners.ContainsKey(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        string[] staleClassifications = DeliveryCallsiteOwners.Keys
            .Where(path => !callsites.Any(value => string.Equals(
                value.SourcePath, path, StringComparison.Ordinal)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Require(unclassified.Length == 0,
            "Unclassified FacilityBuffer delivery callsite(s): "
            + string.Join(",", unclassified));
        Require(staleClassifications.Length == 0,
            "Stale FacilityBuffer delivery classification(s): "
            + string.Join(",", staleClassifications));

        VerifyRegistryShape(root);
        VerifyRegisteredProductionOutputHandlers(root);
        OwnerRow[] sorted = Rows
            .OrderBy(value => value.State, StringComparer.Ordinal)
            .ThenBy(value => value.OwnerDomain, StringComparer.Ordinal)
            .ThenBy(value => value.DestinationRule, StringComparer.Ordinal)
            .ThenBy(value => value.SourcePath, StringComparer.Ordinal)
            .ThenBy(value => value.ProducerSymbol, StringComparer.Ordinal)
            .ToArray();
        Require(sorted.Select(value => value.Key)
                    .Distinct(StringComparer.Ordinal).Count() == sorted.Length,
            "Facility-buffer owner manifest contains a duplicate stable row key.");

        int inputOwners = sorted.Count(value => value.State == "FacilityBuffer"
            && value.Disposition is "migrated" or "remaining");
        int inputMigrated = sorted.Count(value => value.State == "FacilityBuffer"
            && value.Disposition == "migrated");
        int inputRemaining = sorted.Count(value => value.State == "FacilityBuffer"
            && value.Disposition == "remaining");
        int bypass = sorted.Count(value => value.Disposition == "bypass");
        int transportDelegatedExact = sorted.Count(
            value => value.Disposition == "transport-delegated-exact");
        int delegatedConsumer = sorted.Count(
            value => value.Disposition == "delegated-consumer");
        int duplicateAuthority = sorted.Count(
            value => value.Disposition == "duplicate-authority");
        int orphan = sorted.Count(value => value.Disposition == "orphan");
        int outputOwners = sorted.Count(value => IsOutputState(value.State));
        int outputMigrated = sorted.Count(value => IsOutputState(value.State)
            && value.Disposition == "migrated");
        int outputRemaining = sorted.Count(value => IsOutputState(value.State)
            && value.Disposition == "remaining");
        string[] unknownDispositions = sorted
            .Select(value => value.Disposition)
            .Where(value => value is not (
                "migrated" or "remaining" or "transport-delegated-exact"
                or "delegated-consumer" or "duplicate-authority"
                or "removed-duplicate-owner" or "bypass" or "orphan"))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(inputOwners > 0 && inputMigrated == inputOwners
                && inputRemaining == 0 && bypass == 0
                && transportDelegatedExact > 0 && delegatedConsumer > 0
                && duplicateAuthority > 0 && orphan == 0
                && outputOwners > 0 && outputMigrated == outputOwners
                && outputRemaining == 0 && unknownDispositions.Length == 0,
            $"Facility-buffer owner classification drift: input={inputOwners}; "
            + $"inputMigrated={inputMigrated}; inputRemaining={inputRemaining}; "
            + $"bypass={bypass}; transportDelegatedExact={transportDelegatedExact}; "
            + $"delegatedConsumer={delegatedConsumer}; "
            + $"duplicateAuthority={duplicateAuthority}; "
            + $"orphan={orphan}; output={outputOwners}; "
            + $"outputMigrated={outputMigrated}; outputRemaining={outputRemaining}; "
            + $"unknownDispositions={string.Join(",", unknownDispositions)}.");

        string[] sourcePaths = sorted.Select(value => value.SourcePath)
            .Concat(callsites.Select(value => value.SourcePath))
            .Append("Assets/Scripts/Services/Combat/EquipmentMaintenanceRuntimeServices.cs")
            .Append("Assets/Scripts/Services/Combat/EquipmentRepairMaterialRestoreGuard.cs")
            .Append("Assets/Scripts/Services/Combat/Editor/EquipmentRepairMaterialOutboxFixture.cs")
            .Append("Assets/Scripts/Services/Economy/ProductionStockSensorDestinationAuthorityRuntime.cs")
            .Append("Assets/Scripts/Services/Economy/ProductionFacilityMutationFence.cs")
            .Append("Assets/Scripts/Services/Economy/Editor/ProductionEconomyDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Economy/Diagnostics/ProductionOutputDestinationLifecycleDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Economy/Editor/ProductionFacilityDestructiveDrainAuthorityRevokerDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Economy/Editor/ProductionFacilityDestructiveDrainCoordinatorDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Economy/Editor/ProductionFacilityDestructiveDrainRecoveryRuntimeDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Economy/ProductionFacilityDestructiveDrainAuthorityRevoker.cs")
            .Append("Assets/Scripts/Services/Economy/ProductionFacilityDestructiveDrainAuthorityStateQuery.cs")
            .Append("Assets/Scripts/Services/Economy/Editor/ProductionPreparedOutputFullPersistenceDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Buildings/BuildingDestructiveLossRuntime.cs")
            .Append("Assets/Scripts/Services/Buildings/BuildingStructuralIntegrityRuntime.cs")
            .Append("Assets/Scripts/Services/Buildings/ProductionFacilityDestructiveDrainRecoveryRuntime.cs")
            .Append("Assets/Scripts/Services/Combat/CombatCoverDurability.cs")
            .Append("Assets/Scripts/Services/Debugging/DungeonDebugCommandProviders.cs")
            .Append("Assets/Scripts/Services/Grid/Building/GridBuildingRuntime.cs")
            .Append("Assets/Scripts/Models/Economy/Content/ProductionBillRuntime.cs")
            .Append("Assets/Scripts/Services/Economy/ProductionFacilityMutationEpochRuntime.cs")
            .Append("Assets/Scripts/Services/Economy/ProductionFacilityMutationAuthorityGate.cs")
            .Append("Assets/Scripts/Services/FacilityEvolution/FacilityRelocationWorldService.cs")
            .Append("Assets/Scripts/Services/FacilityEvolution/Editor/FacilityRelocationCompletionFenceFixture.cs")
            .Append("Assets/Scripts/Services/Items/ItemTransferService.cs")
            .Append("Assets/Scripts/Services/Items/FacilityBufferDestinationClaimRegistry.cs")
            .Append("Assets/Scripts/Services/Items/FacilityBufferDestinationAdmissionFenceQuery.cs")
            .Append("Assets/Scripts/Services/Items/FacilityBufferMassAdmissionService.cs")
            .Append("Assets/Scripts/Services/Items/WorldItemStackRuntime.cs")
            .Append("Assets/Scripts/Services/Items/WorldItemWarehouseService.cs")
            .Append("Assets/Scripts/Models/Medical/Core/SurgeryModels.cs")
            .Append("Assets/Scripts/Models/Medical/Core/SurgerySaveValidation.cs")
            .Append("Assets/Scripts/Services/Medical/SurgeryRuntimeServices.cs")
            .Append("Assets/Scripts/Services/Medical/SurgeryMaterialTerminalRuntime.cs")
            .Append("Assets/Scripts/Services/Medical/SurgeryMaterialTerminalCheckpointGc.cs")
            .Append("Assets/Scripts/Services/Medical/SurgeryMaterialTerminalCrossAggregateSaveValidation.cs")
            .Append("Assets/Scripts/Services/Items/FacilityBufferDestinationCustodyDrainService.cs")
            .Append("Assets/Scripts/Models/Items/Core/FacilityBufferDestinationCustodyDrainContracts.cs")
            .Append("Assets/Scripts/Services/Items/Editor/PhysicalStockQueryV18DebugScenarios.cs")
            .Append("Assets/Scripts/Services/Infrastructure/Industrial/ConveyorPersistence.cs")
            .Append("Assets/Scripts/Services/Infrastructure/Industrial/ConveyorPhysicalCustodySaveValidation.cs")
            .Append("Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureModels.cs")
            .Append("Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureSaveValidation.cs")
            .Append("Assets/Scripts/Services/Infrastructure/Industrial/Editor/IndustrialInfrastructureDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Infrastructure/Industrial/Editor/IndustrialInfrastructurePlayModeVerifier.cs")
            .Append("Assets/Scripts/Services/Infrastructure/Registration/DungeonSaveRegistration.cs")
            .Append("Assets/Scripts/Services/Infrastructure/Registration/DungeonProgressionOffenseRegistration.cs")
            .Append("Assets/Scripts/Services/Infrastructure/Registration/DungeonCombatRegistration.cs")
            .Append("Assets/Scripts/Services/Infrastructure/Registration/DungeonWorldSimulationRegistration.cs")
            .Append("Assets/Scripts/Models/Economy/Content/WorldResourceOutputContracts.cs")
            .Append("Assets/Scripts/Models/Economy/Content/WorldResourcePorts.cs")
            .Append("Assets/Scripts/Services/Economy/WorldResourcePortAdapters.cs")
            .Append("Assets/Scripts/Services/Economy/Editor/WorldResourceDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Items/Editor/PhysicalItemLogisticsPlayModeVerifier.cs")
            .Append("Assets/Scripts/Services/Economy/Editor/V27BatchAOutputClosureDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Infrastructure/BlueprintResearchSaveSection.cs")
            .Append("Assets/Scripts/Services/Infrastructure/KnowledgeResidueProcessingRuntime.cs")
            .Append("Assets/Scripts/Services/Infrastructure/Editor/KnowledgeResiduePhysicalRestoreJoinDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Infrastructure/Editor/V16IntegrationDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Research/Editor/ResearchTreeDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Run/RunAdministrativeSealDurableEquipmentRuntime.cs")
            .Append("Assets/Scripts/Services/Invasion/InvasionSignalHornDurableEquipmentRuntime.cs")
            .Append("Assets/Scripts/Services/Invasion/InvasionDefenseKitSupplyRuntime.cs")
            .Append("Assets/Scripts/Services/Invasion/Editor/InvasionDefenseKitSupplyDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Items/RunInvasionDurableEquipmentLifecycleRuntime.cs")
            .Append("Assets/Scripts/Services/Run/Editor/RunInvasionDurableEquipmentDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Captivity/CircusPerformanceSupplyRuntime.cs")
            .Append("Assets/Scripts/Services/Captivity/CircusPerformanceSupplyLifecycleRuntime.cs")
            .Append("Assets/Scripts/Services/Captivity/Editor/CircusPerformanceSupplyDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Captivity/Editor/CircusSupplyRestoreJoinFixture.cs")
            .Append("Assets/Scripts/Services/Captivity/CaptivityInteractionMaterialRuntime.cs")
            .Append("Assets/Scripts/Services/Captivity/CaptivityInteractionMaterialLifecycleRuntime.cs")
            .Append("Assets/Scripts/Services/Captivity/Editor/CaptivityInteractionMaterialDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Defense/DefenseFacilityInputOwnerContracts.cs")
            .Append("Assets/Scripts/Services/Defense/DefenseFacilityInputOwnerRuntime.cs")
            .Append("Assets/Scripts/Services/Defense/DefenseFacilityInputOwnerLifecycleRuntime.cs")
            .Append("Assets/Scripts/Services/Defense/Editor/DefenseFacilityInputOwnerDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Combat/CombatEquipmentCraftInputDestinationRuntime.cs")
            .Append("Assets/Scripts/Services/Combat/Editor/CombatEquipmentCraftInputDestinationDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Economy/CertifiedSeedInputOwnerRuntime.cs")
            .Append("Assets/Scripts/Services/Economy/CertifiedSeedSaveSection.cs")
            .Append("Assets/Scripts/Services/Economy/Editor/CertifiedSeedInputOwnerDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Offense/Strategic/OffenseUrgentMitigationInputOwnerRuntime.cs")
            .Append("Assets/Scripts/Services/Offense/Strategic/OffenseUrgentMitigationRuntime.cs")
            .Append("Assets/Scripts/Services/Offense/Strategic/OffenseStrategicModels.cs")
            .Append("Assets/Scripts/Services/Offense/OffenseAggregateSaveValidation.cs")
            .Append("Assets/Scripts/Services/Offense/Editor/OffenseUrgentMitigationInputOwnerDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Character/Work/Editor/WorkConstructionInputOwnerDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Combat/Editor/EquipmentModuleInputOwnerDebugScenarios.cs")
            .Append("Assets/Scripts/Services/FacilityEvolution/Editor/FacilityEvolutionInputOwnerDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Infrastructure/Registration/DungeonFacilityRegistration.cs")
            .Append("Assets/Scripts/Services/Economy/Editor/EconomyProjectInputOwnerDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Economy/Planning/GrandProjectSaveSection.cs")
            .Append("Assets/Scripts/Services/Economy/Planning/RegionalSupplyContractSaveSection.cs")
            .Append("Assets/Scripts/Services/Economy/Planning/ResourceStockPolicySaveSection.cs")
            .Append("Assets/Scripts/Services/Infrastructure/Industrial/ProcessFluidUseRuntime.cs")
            .Append("Assets/Scripts/Services/Medical/Editor/SurgicalPartStorageInputOwnerDebugScenarios.cs")
            .Append("Assets/Scripts/Services/Economy/Waste/Editor/WasteDirectFeedAuthorityDebugScenarios.cs")
            .Append(SelfPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string sourceDigest = ComputeCanonicalSourceDigest(root, sourcePaths);
        string deliveryInvocationSetDigest = ComputeCanonicalLineSetDigest(
            callsites.Select(value => value.CanonicalLine));
        byte[] csv = BuildCsv(sorted, sourceDigest);
        string summary = "V27 FacilityBuffer owner manifest passed: "
            + $"inputOwners={inputOwners}; outputOwners={outputOwners}; "
            + $"inputMigrated={inputMigrated}; inputRemaining={inputRemaining}; "
            + $"outputMigrated={outputMigrated}; outputRemaining={outputRemaining}; "
            + $"bypass={bypass}; "
            + $"transportDelegatedExact={transportDelegatedExact}; "
            + $"delegatedConsumer={delegatedConsumer}; "
            + $"duplicateAuthority={duplicateAuthority}; "
            + $"orphan={orphan}; deliveryInvocations={callsites.Length}; "
            + $"deliveryInvocationFiles={deliveryInvocationFiles.Length}; "
            + "unclassified=0; deterministic capture=PASS.";
        byte[] report = Utf8(
            "schemaVersion=3\n"
            + "scope=FacilityBuffer,FacilityOutputBuffer,DirectLooseOutput\n"
            + "fullStoredDestinationCoverage=true\n"
            + $"sourceDigest={sourceDigest}\n"
            + $"inputOwners={inputOwners}\n"
            + $"outputOwners={outputOwners}\n"
            + $"inputMigrated={inputMigrated}\n"
            + $"inputRemaining={inputRemaining}\n"
            + $"outputMigrated={outputMigrated}\n"
            + $"outputRemaining={outputRemaining}\n"
            + $"remaining={inputRemaining + outputRemaining}\n"
            + $"bypass={bypass}\n"
            + $"transportDelegatedExact={transportDelegatedExact}\n"
            + $"delegatedConsumer={delegatedConsumer}\n"
            + $"duplicateAuthority={duplicateAuthority}\n"
            + $"orphan={orphan}\n"
            + $"deliveryInvocations={callsites.Length}\n"
            + $"deliveryInvocationFiles={deliveryInvocationFiles.Length}\n"
            + $"deliveryInvocationSetDigest={deliveryInvocationSetDigest}\n"
            + "destructiveDrainLiveCallers=3\n"
            + "destructiveDrainLegacyTransactionCallers=0\n"
            + "destructiveDrainDebugBypass=0\n"
            + "destructiveDrainDirectDestroyAllowlisted=8\n"
            + "unclassified=0\n"
            + "classificationGate=PASS\n"
            + "fullMigrationGate=PASS\n");
        return new CaptureResult(
            csv, report, summary, inputRemaining + outputRemaining,
            bypass, orphan, unclassified.Length);
    }

    private static DeliveryCallsite[] CaptureDeliveryCallsites(string root)
    {
        string scripts = Path.Combine(root, "Assets", "Scripts");
        List<DeliveryCallsite> rows = new List<DeliveryCallsite>();
        foreach (string absolute in Directory.GetFiles(
                     scripts, "*.cs", SearchOption.AllDirectories))
        {
            string path = CanonicalPath(Path.GetRelativePath(root, absolute));
            if (path.IndexOf("/Editor/", StringComparison.Ordinal) >= 0)
                continue;
            string source = File.ReadAllText(absolute);
            foreach (Match match in DeliveryInvocationPattern.Matches(source))
            {
                int line = 1;
                for (int index = 0; index < match.Index; index++)
                    if (source[index] == '\n') line++;
                rows.Add(new DeliveryCallsite(
                    path, line, match.Groups[1].Value));
            }
        }
        return rows.OrderBy(value => value.SourcePath, StringComparer.Ordinal)
            .ThenBy(value => value.Line)
            .ThenBy(value => value.Api, StringComparer.Ordinal)
            .ToArray();
    }

    private static void VerifyRegistryShape(string root)
    {
        OwnerRow[] liveInputOwners = Rows.Where(value =>
                value.State == "FacilityBuffer"
                && value.Disposition is "migrated" or "remaining")
            .ToArray();
        Require(liveInputOwners.Length > 0
                && liveInputOwners.All(value => value.ClaimAuthority.IndexOf(
                    "exact", StringComparison.OrdinalIgnoreCase) >= 0),
            "Every current FacilityBuffer live owner must declare an exact claim authority.");

        string production = Read(root,
            "Assets/Scripts/Services/Economy/ProductionInputLogisticsService.cs");
        string productionClaims = Read(root,
            "Assets/Scripts/Services/Economy/ProductionInputDestinationClaimRuntime.cs");
        Require(Count(production, "items.RequestDeliveryWithinMassCapacity(") == 0
                && Count(production, "items.RequestDelivery(") == 1
                && Count(productionClaims, "InputBufferCapacitySchemaRevision =") == 1
                && Count(productionClaims, "new PhysicalMassGrams(maxInputBufferMassGrams)") == 1,
            "Production input common gram profile/token migration drifted.");

        string researchAdapter = Read(root,
            "Assets/Scripts/Services/Infrastructure/ResearchWorkExecutionAdapter.cs");
        string researchPolicy = Read(root,
            "Assets/Scripts/Services/Infrastructure/ResearchArcaneIndexEquipmentPolicySource.cs");
        string researchLifecycle = Read(root,
            "Assets/Scripts/Services/Infrastructure/ResearchDurableEquipmentLifecycleRuntime.cs");
        string researchRegistration = Read(root,
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonWorldSimulationRegistration.cs");
        string researchFixture = Read(root,
            "Assets/Scripts/Services/Infrastructure/Editor/ResearchArcaneIndexEquipmentDebugScenarios.cs");
        int researchInject = researchAdapter.IndexOf(
            "[VContainer.Inject]",
            StringComparison.Ordinal);
        int injectedConstructor = researchInject >= 0
            ? researchAdapter.IndexOf(
                "public ResearchWorkExecutionHandler(",
                researchInject,
                StringComparison.Ordinal)
            : -1;
        int nextPublicConstructor = injectedConstructor >= 0
            ? researchAdapter.IndexOf(
                "public ResearchWorkExecutionHandler(",
                injectedConstructor + 1,
                StringComparison.Ordinal)
            : -1;
        int injectedConstructorBoundary = nextPublicConstructor >= 0
            ? nextPublicConstructor
            : injectedConstructor >= 0
                ? researchAdapter.IndexOf(
                    "private ResearchWorkExecutionHandler(",
                    injectedConstructor,
                    StringComparison.Ordinal)
                : -1;
        int injectedEquipmentDependency = injectedConstructor >= 0
            ? researchAdapter.IndexOf(
                "IResearchDurableEquipmentWorkPolicyQuery equipmentWorkPolicies",
                injectedConstructor,
                StringComparison.Ordinal)
            : -1;
        Require(
            Count(researchAdapter, "TryRequestItemDelivery(") == 0
            && Count(researchAdapter, "TryRequestDelivery(") == 0
            && Count(researchAdapter, "TryRequestExactStackDelivery(") == 0
            && Count(researchAdapter, "ReleaseStacksByDestination(") == 0
            && Count(researchAdapter, "record:arcane-index") == 0
            && Count(researchAdapter, "equipmentSlots.TryReconcile(assignment)") == 1
            && Count(researchAdapter, "equipmentSlots.TryEnsureSupply(assignment.Key)") == 1
            && Count(researchAdapter, "equipmentUse.TryApplyWearAndEffect(") == 1
            && Count(researchAdapter, "[VContainer.Inject]") == 1
            && researchInject >= 0
            && injectedConstructor > researchInject
            && injectedEquipmentDependency > injectedConstructor
            && injectedConstructorBoundary > injectedEquipmentDependency
            && Count(researchPolicy,
                "public sealed class ResearchArcaneIndexEquipmentPolicySource") == 1
            && Count(researchRegistration,
                "Register<ResearchArcaneIndexEquipmentPolicySource>(") == 1
            && Count(researchRegistration,
                ".As<IDurableFacilityEquipmentPolicySource>()") == 8
            && Count(researchRegistration,
                ".As<IResearchDurableEquipmentWorkPolicySource>()") == 1
            && Count(researchRegistration,
                "RegisterEntryPoint<ResearchDurableEquipmentLifecycleRuntime>(") == 1
            && Count(researchLifecycle, "public void ValidateBeforeCapture()") == 1
            && researchFixture.Contains(
                "Run Research Arcane Index Equipment Contracts",
                StringComparison.Ordinal)
            && researchFixture.Contains(
                "VerifyLiveResearchAdapter",
                StringComparison.Ordinal),
            "Research arcane-index common slot/use/lifecycle migration drifted.");

        string circusRuntime = Read(root,
            "Assets/Scripts/Services/Captivity/CircusRuntime.cs");
        string circusSupply = Read(root,
            "Assets/Scripts/Services/Captivity/CircusPerformanceSupplyRuntime.cs");
        string circusLifecycle = Read(root,
            "Assets/Scripts/Services/Captivity/CircusPerformanceSupplyLifecycleRuntime.cs");
        string circusFixture = Read(root,
            "Assets/Scripts/Services/Captivity/Editor/CircusPerformanceSupplyDebugScenarios.cs");
        Require(
            Count(circusRuntime, "TryRequestItemDelivery(") == 0
            && Count(circusSupply,
                "public sealed class CircusPerformanceSupplyPolicySource") == 1
            && Count(circusSupply, "slots.TryReconcile(") == 1
            && Count(circusSupply, "slots.TryEnsureSupply(") == 1
            && Count(circusSupply, "use.TryApplyWearAndEffect(") == 1
            && Count(circusSupply,
                "PerformancePropBoxMassGrams") >= 1
            && Count(circusFixture, "BanquetCartMassGrams") >= 1
            && Count(circusLifecycle, "public void ValidateBeforeCapture()") == 1
            && Count(circusLifecycle, "commands.TryClose(") == 1
            && Count(researchRegistration,
                "Register<CircusPerformanceSupplyPolicySource>(") == 1
            && Count(researchRegistration,
                "RegisterEntryPoint<CircusPerformanceSupplyLifecycleRuntime>(") == 1
            && Count(circusFixture,
                "DurableFacilityEquipmentSaveDebugScenarios.RunAll();") == 1,
            "Circus exact performance-supply migration drifted.");

        string captivityInteraction = Read(root,
            "Assets/Scripts/Services/Captivity/CaptivityInteractionRuntime.cs");
        string captivityMaterials = Read(root,
            "Assets/Scripts/Services/Captivity/CaptivityInteractionMaterialRuntime.cs");
        string captivityMaterialLifecycle = Read(root,
            "Assets/Scripts/Services/Captivity/CaptivityInteractionMaterialLifecycleRuntime.cs");
        string captivityRuntime = Read(root,
            "Assets/Scripts/Services/Captivity/CaptivityRuntime.cs");
        string captivityMaterialFixture = Read(root,
            "Assets/Scripts/Services/Captivity/Editor/CaptivityInteractionMaterialDebugScenarios.cs");
        Require(
            Count(captivityInteraction, "TryRequestFacilityDelivery(") == 0
            && Count(captivityInteraction, "TryConsumeFacilityBuffer(") == 0
            && Count(captivityMaterials, "TryRequestFacilityDelivery(") == 1
            && Count(captivityMaterials,
                "PhysicalItemDispositionKind.Sink") >= 2
            && Count(captivityMaterials,
                "FacilityBufferDestinationAnchorKind.LiveFacility") >= 2
            && Count(captivityMaterials, "TryReleaseAtOwnerPosition(") == 1
            && Count(captivityRuntime,
                "interactionMaterialLifecycle.ValidateBeforeCapture(Captives)") == 1
            && Count(captivityRuntime,
                "interactionMaterialLifecycle.TryReplaceRestoreAuthorities(") == 0
            && Count(captivityMaterialLifecycle,
                "TryReplaceRestoreAuthorities(") == 3
            && Count(captivityMaterialLifecycle,
                "public sealed class CaptivityInteractionMaterialRestoreParticipant") == 1
            && Count(captivityMaterialLifecycle, "materials.TryReplace(") == 2
            && Count(researchRegistration,
                "Register<CaptivityInteractionMaterialRuntime>(") == 1
            && Count(researchRegistration,
                "Register<CaptivityInteractionMaterialLifecycleRuntime>(") == 1
            && Count(researchRegistration,
                "Register<CaptivityInteractionMaterialRestoreParticipant>(") == 1
            && captivityMaterialFixture.Contains(
                "VerifyCommittedTokenRoundTrip",
                StringComparison.Ordinal),
            "Captivity interaction exact material Sink/save owner migration drifted.");

        string defenseRuntime = Read(root,
            "Assets/Scripts/Services/Defense/DefenseFacilityRuntime.cs");
        string defenseOwner = Read(root,
            "Assets/Scripts/Services/Defense/DefenseFacilityInputOwnerRuntime.cs");
        string defenseOwnerContracts = Read(root,
            "Assets/Scripts/Services/Defense/DefenseFacilityInputOwnerContracts.cs");
        string defenseOwnerLifecycle = Read(root,
            "Assets/Scripts/Services/Defense/DefenseFacilityInputOwnerLifecycleRuntime.cs");
        string defenseOwnerFixture = Read(root,
            "Assets/Scripts/Services/Defense/Editor/DefenseFacilityInputOwnerDebugScenarios.cs");
        string defenseRegistration = Read(root,
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonCombatRegistration.cs");
        Require(
            Count(defenseRuntime, "TryEnsureInputAuthority(") >= 4
            && defenseOwnerContracts.Contains(
                "FacilityBufferDestinationAdmissionPolicy.ExactGramRequired",
                StringComparison.Ordinal)
            && defenseOwner.Contains(
                "releases.TryReleaseAtOwnerPosition(",
                StringComparison.Ordinal)
            && defenseOwner.Contains(
                "lifecycle.TryReplaceOwnedAuthorities(",
                StringComparison.Ordinal)
            && defenseOwnerContracts.Contains(
                "public const string OwnerDomain = \"combat.defense-facility\";",
                StringComparison.Ordinal)
            && defenseOwnerLifecycle.Contains(
                "public void ValidateBeforeCapture()",
                StringComparison.Ordinal)
            && defenseOwnerLifecycle.Contains(
                "owner.TryReconcileRestore(",
                StringComparison.Ordinal)
            && Count(defenseRegistration,
                "Register<DefenseFacilityInputOwnerRuntime>(") == 1
            && Count(defenseRegistration,
                "RegisterEntryPoint<DefenseFacilityInputOwnerLifecycleRuntime>(") == 1
            && Count(defenseRegistration,
                "Register<DefenseFacilityInputOwnerRestoreParticipant>(") == 1
            && defenseOwnerFixture.Contains(
                "VerifyCapacityExpansionRetainsPhysicalCustody",
                StringComparison.Ordinal),
            "Defense facility exact input-owner migration drifted.");

        string combatCrafting = Read(root,
            "Assets/Scripts/Services/Combat/CombatEquipmentCraftingRuntime.cs");
        string combatCraftOwner = Read(root,
            "Assets/Scripts/Services/Combat/CombatEquipmentCraftInputDestinationRuntime.cs");
        string combatCraftFixture = Read(root,
            "Assets/Scripts/Services/Combat/Editor/CombatEquipmentCraftInputDestinationDebugScenarios.cs");
        Require(
            Count(combatCrafting, "TryRequestItemDelivery(") == 0
            && Count(combatCraftOwner, "TryRequestItemDelivery(") == 1
            && combatCraftOwner.Contains(
                "FacilityBufferDestinationAdmissionPolicy.ExactGramRequired",
                StringComparison.Ordinal)
            && combatCraftOwner.Contains(
                "releases.TryReleaseAtOwnerPosition(",
                StringComparison.Ordinal)
            && combatCraftOwner.Contains(
                "lifecycle.TryReplaceOwnedAuthorities(",
                StringComparison.Ordinal)
            && Count(defenseRegistration,
                "Register<CombatEquipmentCraftInputDestinationRuntime>(") == 1
            && combatCraftFixture.Contains(
                "one-gram restore drift was accepted",
                StringComparison.Ordinal),
            "Combat equipment crafting exact input-owner migration drifted.");

        string certifiedSeedRuntime = Read(root,
            "Assets/Scripts/Services/Economy/CertifiedSeedRuntime.cs");
        string certifiedSeedOwner = Read(root,
            "Assets/Scripts/Services/Economy/CertifiedSeedInputOwnerRuntime.cs");
        string certifiedSeedSave = Read(root,
            "Assets/Scripts/Services/Economy/CertifiedSeedSaveSection.cs");
        string certifiedSeedFixture = Read(root,
            "Assets/Scripts/Services/Economy/Editor/CertifiedSeedInputOwnerDebugScenarios.cs");
        string worldRegistration = Read(root,
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonWorldSimulationRegistration.cs");
        Require(
            Count(certifiedSeedRuntime, "TryRequestItemDelivery(") == 1
            && certifiedSeedOwner.Contains(
                "FacilityBufferDestinationAdmissionPolicy.ExactGramRequired",
                StringComparison.Ordinal)
            && certifiedSeedOwner.Contains(
                "releases.TryReleaseAtOwnerPosition(",
                StringComparison.Ordinal)
            && certifiedSeedOwner.Contains(
                "lifecycle.TryReplaceOwnedAuthorities(",
                StringComparison.Ordinal)
            && certifiedSeedSave.Contains(
                "inputOwners.TryReplaceForRestore(",
                StringComparison.Ordinal)
            && Count(worldRegistration,
                "Register<CertifiedSeedInputOwnerRuntime>(") == 1
            && Count(worldRegistration,
                ".As<ICertifiedSeedInputOwnerDescriptorSource>()") == 1
            && certifiedSeedFixture.Contains(
                "CERTIFIED_SEED_INPUT_OWNER_PASS",
                StringComparison.Ordinal),
            "Certified-seed exact input-owner migration drifted.");

        string cropPlotRuntime = Read(root,
            "Assets/Scripts/Services/Economy/CropPlotRuntime.cs");
        string cropPlotOwner = Read(root,
            "Assets/Scripts/Services/Economy/CropPlotInputOwnerRuntime.cs");
        string cropPlotSave = Read(root,
            "Assets/Scripts/Services/Economy/CropPlotSaveSection.cs");
        string cropPlotFixture = Read(root,
            "Assets/Scripts/Services/Economy/Editor/CropPlotInputOwnerDebugScenarios.cs");
        Require(
            Count(cropPlotRuntime, "items.ReleaseDestination(") == 0
            && cropPlotOwner.Contains(
                "FacilityBufferDestinationAdmissionPolicy.ExactGramRequired",
                StringComparison.Ordinal)
            && cropPlotOwner.Contains(
                "releases.TryReleaseAtOwnerPosition(",
                StringComparison.Ordinal)
            && cropPlotOwner.Contains(
                "lifecycle.TryReplaceOwnedAuthorities(",
                StringComparison.Ordinal)
            && cropPlotSave.Contains(
                "inputOwners.TryReplaceForRestore(",
                StringComparison.Ordinal)
            && Count(worldRegistration,
                "Register<CropPlotInputOwnerRuntime>(") == 1
            && Count(worldRegistration,
                ".As<ICropPlotInputOwnerDescriptorSource>()") == 1
            && cropPlotFixture.Contains(
                "CROP_PLOT_INPUT_OWNER_PASS",
                StringComparison.Ordinal),
            "Crop-plot exact input-owner migration drifted.");

        string wildlifeCareOwner = Read(root,
            "Assets/Scripts/Services/Captivity/WildlifeCareInputOwnerRuntime.cs");
        string wildlifeCapture = Read(root,
            "Assets/Scripts/Services/Captivity/WildlifeCaptureRuntime.cs");
        string wildlifeCareFixture = Read(root,
            "Assets/Scripts/Services/Captivity/Editor/WildlifeCareInputOwnerDebugScenarios.cs");
        Require(
            wildlifeCareOwner.Contains(
                "FacilityBufferDestinationAdmissionPolicy.ExactGramRequired",
                StringComparison.Ordinal)
            && wildlifeCareOwner.Contains(
                "releases.TryReleaseAtOwnerPosition(",
                StringComparison.Ordinal)
            && wildlifeCareOwner.Contains(
                "lifecycle.TryReplaceOwnedAuthorities(",
                StringComparison.Ordinal)
            && wildlifeCapture.Contains(
                "WildlifeCareInputOwnerAuthority",
                StringComparison.Ordinal)
            && Count(worldRegistration,
                "Register<WildlifeCareInputOwnerSource>(") == 1
            && Count(worldRegistration,
                "Register<WildlifeCareInputOwnerRuntime>(") == 1
            && Count(worldRegistration,
                "RegisterEntryPoint<WildlifeCareInputOwnerLifecycleRuntime>(") == 1
            && Count(worldRegistration,
                "Register<WildlifeCareInputOwnerRestoreParticipant>(") == 1
            && wildlifeCareFixture.Contains(
                "WILDLIFE_CARE_INPUT_OWNER_FOCUSED_PASS",
                StringComparison.Ordinal),
            "Wildlife-care exact input-owner migration drifted.");

        string consumablesOwner = Read(root,
            "Assets/Scripts/Services/Survival/CharacterConsumablesInputOwnerRuntime.cs");
        string consumablesSave = Read(root,
            "Assets/Scripts/Services/Infrastructure/Core/Save/CharacterConsumablesSaveSection.cs");
        string consumablesFixture = Read(root,
            "Assets/Scripts/Services/Survival/Editor/CharacterConsumablesInputOwnerDebugScenarios.cs");
        Require(
            consumablesOwner.Contains(
                "FacilityBufferDestinationAdmissionPolicy.ExactGramRequired",
                StringComparison.Ordinal)
            && consumablesOwner.Contains(
                "releases.TryReleaseAtOwnerPosition(",
                StringComparison.Ordinal)
            && consumablesOwner.Contains(
                "lifecycle.TryReplaceOwnedAuthorities(",
                StringComparison.Ordinal)
            && consumablesSave.Contains(
                "inputOwners.TryReplaceForRestore(",
                StringComparison.Ordinal)
            && Count(worldRegistration,
                "Register<CharacterConsumablesInputOwnerDescriptorSource>(") == 1
            && Count(worldRegistration,
                "Register<CharacterConsumablesInputOwnerRuntime>(") == 1
            && Count(worldRegistration,
                "RegisterEntryPoint<CharacterConsumablesInputOwnerLifecycleRuntime>(") == 1
            && consumablesFixture.Contains(
                "CHARACTER_CONSUMABLES_INPUT_OWNER_PASS",
                StringComparison.Ordinal),
            "Character-consumables exact input-owner migration drifted.");

        string captivityCareOwner = Read(root,
            "Assets/Scripts/Services/Captivity/CaptivityCareLaborInputOwnerRuntime.cs");
        string captivityPerformer = Read(root,
            "Assets/Scripts/Models/Captivity/Core/CaptivityPerformerRuntime.cs");
        string captivityCareFixture = Read(root,
            "Assets/Scripts/Services/Captivity/Editor/CaptivityCareLaborInputOwnerDebugScenarios.cs");
        Require(
            captivityCareOwner.Contains(
                "FacilityBufferDestinationAdmissionPolicy.ExactGramRequired",
                StringComparison.Ordinal)
            && captivityCareOwner.Contains(
                "releases.TryReleaseAtOwnerPosition(",
                StringComparison.Ordinal)
            && captivityCareOwner.Contains(
                "lifecycle.TryReplaceOwnedAuthorities(",
                StringComparison.Ordinal)
            && !captivityPerformer.Contains(
                "TryRequestFacilityDelivery(",
                StringComparison.Ordinal)
            && !captivityPerformer.Contains(
                "captive-care:",
                StringComparison.Ordinal)
            && Count(worldRegistration,
                "Register<CaptivityCareLaborInputOwnerRuntime>(") == 1
            && Count(worldRegistration,
                "Register<CaptivityCareLaborInputOwnerRestoreParticipant>(") == 1
            && captivityCareFixture.Contains(
                "CAPTIVITY_CARE_LABOR_INPUT_OWNER_FOCUSED_PASS",
                StringComparison.Ordinal),
            "Captivity care/labor exact owner or performer duplicate-owner removal drifted.");

        string equipmentEvolutionOwner = Read(root,
            "Assets/Scripts/Services/Combat/EquipmentEvolutionInputOwnerRuntime.cs");
        string equipmentEvolutionRuntime = Read(root,
            "Assets/Scripts/Services/Combat/EquipmentEvolutionRuntime.cs");
        string equipmentEvolutionFixture = Read(root,
            "Assets/Scripts/Services/Combat/Editor/EquipmentEvolutionInputOwnerDebugScenarios.cs");
        string equipmentEvolutionRegistration = Read(root,
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonCombatRegistration.cs");
        Require(
            equipmentEvolutionOwner.Contains(
                "FacilityBufferDestinationAdmissionPolicy.ExactGramRequired",
                StringComparison.Ordinal)
            && equipmentEvolutionOwner.Contains(
                "TryReleaseAtOwnerPosition(",
                StringComparison.Ordinal)
            && equipmentEvolutionOwner.Contains(
                "TryReplaceOwnedAuthorities(",
                StringComparison.Ordinal)
            && Count(equipmentEvolutionRuntime,
                "inputOwners.TryOpen(") == 2
            && Count(equipmentEvolutionRuntime,
                "inputOwners.TryRequest(") == 2
            && Count(equipmentEvolutionRuntime,
                "items.TryRequestItemDelivery(") == 0
            && Count(equipmentEvolutionRuntime,
                "items.TryRequestStackDelivery(") == 0
            && Count(equipmentEvolutionRuntime,
                "ReleaseStacksByDestination(") == 0
            && Count(equipmentEvolutionRegistration,
                "Register<EquipmentEvolutionInputDeliveryGateway>(") == 1
            && Count(equipmentEvolutionRegistration,
                "Register<EquipmentEvolutionInputOwnerRuntime>(") == 1
            && equipmentEvolutionFixture.Contains(
                "EQUIPMENT_EVOLUTION_INPUT_OWNER_PASS",
                StringComparison.Ordinal),
            "Equipment-evolution exact input-owner migration drifted.");

        string workConstructionOwner = Read(root,
            "Assets/Scripts/Services/Character/Work/WorkConstructionInputOwnerRuntime.cs");
        string workConstructionFixture = Read(root,
            "Assets/Scripts/Services/Character/Work/Editor/WorkConstructionInputOwnerDebugScenarios.cs");
        string equipmentModuleOwner = Read(root,
            "Assets/Scripts/Services/Combat/EquipmentModuleInputOwnerRuntime.cs");
        string equipmentModuleFixture = Read(root,
            "Assets/Scripts/Services/Combat/Editor/EquipmentModuleInputOwnerDebugScenarios.cs");
        string facilityEvolutionOwner = Read(root,
            "Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionInputOwnerRuntime.cs");
        string facilityEvolutionFixture = Read(root,
            "Assets/Scripts/Services/FacilityEvolution/Editor/FacilityEvolutionInputOwnerDebugScenarios.cs");
        string facilityRegistration = Read(root,
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonFacilityRegistration.cs");
        Require(
            workConstructionOwner.Contains(
                "FacilityBufferDestinationAdmissionPolicy.ExactGramRequired",
                StringComparison.Ordinal)
            && workConstructionOwner.Contains(
                "TryReleaseAtOwnerPosition(", StringComparison.Ordinal)
            && Count(worldRegistration,
                "Register<WorkConstructionInputOwnerRuntime>(") == 1
            && workConstructionFixture.Contains(
                "work.construction exact input-owner scenarios passed",
                StringComparison.Ordinal)
            && equipmentModuleOwner.Contains(
                "FacilityBufferDestinationAdmissionPolicy.ExactGramRequired",
                StringComparison.Ordinal)
            && equipmentModuleOwner.Contains(
                "TryReleaseAtOwnerPosition(", StringComparison.Ordinal)
            && Count(equipmentEvolutionRegistration,
                "Register<EquipmentModuleInputOwnerRuntime>(") == 1
            && Count(equipmentEvolutionRegistration,
                "Register<EquipmentModuleInputOwnerRestoreParticipant>(") == 1
            && equipmentModuleFixture.Contains(
                "combat.equipment-module exact input-owner scenarios passed",
                StringComparison.Ordinal)
            && facilityEvolutionOwner.Contains(
                "FacilityBufferDestinationAdmissionPolicy.ExactGramRequired",
                StringComparison.Ordinal)
            && facilityEvolutionOwner.Contains(
                "TryReleaseAtOwnerPosition(", StringComparison.Ordinal)
            && Count(facilityRegistration,
                "Register<FacilityEvolutionInputOwnerRuntime>(") == 1
            && Count(facilityRegistration,
                "Register<FacilityEvolutionInputOwnerRestoreParticipant>(") == 1
            && facilityEvolutionFixture.Contains(
                "facility.evolution exact input-owner scenarios passed",
                StringComparison.Ordinal),
            "Construction/equipment-module/facility-evolution owner migration drifted.");

        string economyProjectOwner = Read(root,
            "Assets/Scripts/Services/Economy/EconomyProjectInputOwnerRuntime.cs");
        string economyProjectFixture = Read(root,
            "Assets/Scripts/Services/Economy/Editor/EconomyProjectInputOwnerDebugScenarios.cs");
        string grandProjectSave = Read(root,
            "Assets/Scripts/Services/Economy/Planning/GrandProjectSaveSection.cs");
        string regionalContractSave = Read(root,
            "Assets/Scripts/Services/Economy/Planning/RegionalSupplyContractSaveSection.cs");
        string stockPolicySave = Read(root,
            "Assets/Scripts/Services/Economy/Planning/ResourceStockPolicySaveSection.cs");
        Require(
            economyProjectOwner.Contains(
                "FacilityBufferDestinationAdmissionPolicy.ExactGramRequired",
                StringComparison.Ordinal)
            && economyProjectOwner.Contains(
                "releases.TryReleaseAtOwnerPosition(",
                StringComparison.Ordinal)
            && grandProjectSave.Contains(
                "inputOwners.TryReplaceForRestore(", StringComparison.Ordinal)
            && regionalContractSave.Contains(
                "inputOwners.TryReplaceForRestore(", StringComparison.Ordinal)
            && stockPolicySave.Contains(
                "inputOwners.TryReplaceForRestore(", StringComparison.Ordinal)
            && Count(worldRegistration,
                "Register<EconomyProjectInputOwnerRuntime>(") == 1
            && economyProjectFixture.Contains(
                "ECONOMY_PROJECT_INPUT_OWNER_PASS",
                StringComparison.Ordinal),
            "Economy project exact input-owner migration drifted.");

        string fluidOwner = Read(root,
            "Assets/Scripts/Services/Infrastructure/Industrial/FluidFacilityInputOwnerAuthority.cs");
        string processFluid = Read(root,
            "Assets/Scripts/Services/Infrastructure/Industrial/ProcessFluidUseRuntime.cs");
        string surgicalStorageOwner = Read(root,
            "Assets/Scripts/Services/Medical/SurgicalPartStorageInputOwnerAuthority.cs");
        string surgicalStorageFixture = Read(root,
            "Assets/Scripts/Services/Medical/Editor/SurgicalPartStorageInputOwnerDebugScenarios.cs");
        string wasteAdapter = Read(root,
            "Assets/Scripts/Services/Economy/Waste/WasteProcessingPortAdapters.cs");
        string wasteFixture = Read(root,
            "Assets/Scripts/Services/Economy/Waste/Editor/WasteDirectFeedAuthorityDebugScenarios.cs");
        Require(
            fluidOwner.Contains(
                "FacilityBufferDestinationAdmissionPolicy.ExactGramRequired",
                StringComparison.Ordinal)
            && fluidOwner.Contains(
                "releases.TryReleaseAtOwnerPosition(",
                StringComparison.Ordinal)
            && Count(processFluid, "TryRequestItemDelivery(") == 3
            && Count(processFluid, "TryRequestFacilityDelivery(") == 0
            && surgicalStorageOwner.Contains(
                "FacilityBufferDestinationAdmissionPolicy.ExactGramRequired",
                StringComparison.Ordinal)
            && surgicalStorageOwner.Contains(
                "TryReleaseAtOwnerPosition(", StringComparison.Ordinal)
            && surgicalStorageFixture.Contains(
                "exact owner and typed fuel checks passed",
                StringComparison.Ordinal)
            && wasteAdapter.Contains(
                "HasExactWildlifeCareDestinationAuthority(",
                StringComparison.Ordinal)
            && wasteFixture.Contains(
                "delegated exact-authority check passed",
                StringComparison.Ordinal),
            "Fluid/process-fluid/surgical-storage/delegated-waste owner migration drifted.");

        string urgentMitigation = Read(root,
            "Assets/Scripts/Services/Offense/Strategic/OffenseUrgentMitigationRuntime.cs");
        string urgentMitigationOwner = Read(root,
            "Assets/Scripts/Services/Offense/Strategic/OffenseUrgentMitigationInputOwnerRuntime.cs");
        string urgentMitigationModels = Read(root,
            "Assets/Scripts/Services/Offense/Strategic/OffenseStrategicModels.cs");
        string urgentMitigationFixture = Read(root,
            "Assets/Scripts/Services/Offense/Editor/OffenseUrgentMitigationInputOwnerDebugScenarios.cs");
        string offenseRegistration = Read(root,
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonProgressionOffenseRegistration.cs");
        Require(
            Count(urgentMitigation, "ReleaseDestination(") == 0
            && urgentMitigation.Contains(
                "inputOwners.TryValidateForCapture(",
                StringComparison.Ordinal)
            && urgentMitigationOwner.Contains(
                "FacilityBufferDestinationAdmissionPolicy.ExactGramRequired",
                StringComparison.Ordinal)
            && urgentMitigationOwner.Contains(
                "releases.TryReleaseAtOwnerPosition(",
                StringComparison.Ordinal)
            && urgentMitigationOwner.Contains(
                "lifecycle.TryReplaceOwnedAuthorities(",
                StringComparison.Ordinal)
            && urgentMitigationModels.Contains(
                "public const int CurrentVersion = 8;",
                StringComparison.Ordinal)
            && Count(offenseRegistration,
                "Register<OffenseUrgentMitigationInputOwnerRuntime>(") == 1
            && urgentMitigationFixture.Contains(
                "OFFENSE_URGENT_MITIGATION_INPUT_OWNER_PASS",
                StringComparison.Ordinal),
            "Offense urgent-mitigation exact input-owner migration drifted.");

        string blueprintArchive = Read(root,
            "Assets/Scripts/Services/Infrastructure/ResearchBlueprintArchiveAdapter.cs");
        string blueprintRuntime = Read(root,
            "Assets/Scripts/Services/Infrastructure/BlueprintResearchRuntime.cs");
        string blueprintSave = Read(root,
            "Assets/Scripts/Services/Infrastructure/BlueprintResearchSaveSection.cs");
        string knowledgeDestination = Read(root,
            "Assets/Scripts/Services/Infrastructure/KnowledgeResidueDestinationRuntime.cs");
        string knowledgeRuntime = Read(root,
            "Assets/Scripts/Services/Infrastructure/KnowledgeResidueProcessingRuntime.cs");
        string knowledgeRestoreFixture = Read(root,
            "Assets/Scripts/Services/Infrastructure/Editor/KnowledgeResiduePhysicalRestoreJoinDebugScenarios.cs");
        string researchTreeFixture = Read(root,
            "Assets/Scripts/Services/Research/Editor/ResearchTreeDebugScenarios.cs");
        string progressionRegistration = Read(root,
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonProgressionOffenseRegistration.cs");
        Require(
            blueprintArchive.Contains(
                "FacilityBufferDestinationAdmissionPolicy.ExactGramRequired",
                StringComparison.Ordinal)
            && blueprintArchive.Contains(
                "public static FacilityBufferCapacityProfile[] BuildProfiles(",
                StringComparison.Ordinal)
            && blueprintArchive.Contains(
                "used.TotalMassGrams",
                StringComparison.Ordinal)
            && blueprintArchive.Contains(
                "+ capacity.ReservedMassGrams",
                StringComparison.Ordinal)
            && blueprintRuntime.Contains(
                "archiveReleases.TryReleaseAtOwnerPosition(",
                StringComparison.Ordinal)
            && blueprintRuntime.Contains(
                "archiveDestinations.TryReplaceOwnedAuthorities(",
                StringComparison.Ordinal)
            && !blueprintRuntime.Contains(
                "ReleaseStacksByDestination(",
                StringComparison.Ordinal)
            && !blueprintRuntime.Contains(
                "TryConsumeKnowledgeResidue(",
                StringComparison.Ordinal)
            && blueprintSave.Contains(
                "private const int CurrentVersion = 6;",
                StringComparison.Ordinal)
            && blueprintSave.Contains(
                "ResearchBlueprintArchiveDestinationAuthority.BuildProfiles(",
                StringComparison.Ordinal)
            && blueprintSave.Contains(
                "archiveDestinations.TryReplaceOwnedAuthorities(",
                StringComparison.Ordinal)
            && knowledgeDestination.Contains(
                "FacilityBufferDestinationAdmissionPolicy.ExactGramRequired",
                StringComparison.Ordinal)
            && knowledgeDestination.Contains(
                "MemoryResidueItemId = \"captivity:memory-residue\"",
                StringComparison.Ordinal)
            && knowledgeRuntime.Contains(
                "TryCommitSinkPending(",
                StringComparison.Ordinal)
            && knowledgeRuntime.Contains(
                "TryFinalizeCommittedTask(",
                StringComparison.Ordinal)
            && knowledgeRuntime.Contains(
                "releases.TryReleaseAtOwnerPosition(",
                StringComparison.Ordinal)
            && !knowledgeRuntime.Contains(
                "TryConsumeFacilityBuffer(",
                StringComparison.Ordinal)
            && knowledgeRestoreFixture.Contains(
                "[PASS] knowledge residue physical restore join",
                StringComparison.Ordinal)
            && researchTreeFixture.Contains(
                "ResearchArchiveAuthorityFixture",
                StringComparison.Ordinal)
            && researchTreeFixture.Contains(
                "profilesAfterRollback",
                StringComparison.Ordinal)
            && progressionRegistration.Contains(
                "Register<KnowledgeResidueDestinationRuntime>",
                StringComparison.Ordinal),
            "Research blueprint-archive/knowledge-residue exact authority migration drifted.");

        string careerAdapter = Read(root,
            "Assets/Scripts/Services/Character/CareerApplicationAdapter.cs");
        string careerPolicy = Read(root,
            "Assets/Scripts/Services/Character/CareerDurableEquipmentPolicySource.cs");
        string careerFixture = Read(root,
            "Assets/Scripts/Services/Character/Editor/CareerDurableEquipmentDebugScenarios.cs");
        string reproductionAdapter = Read(root,
            "Assets/Scripts/Services/Character/ReproductionCommandRuntime.cs");
        string reproductionUse = Read(root,
            "Assets/Scripts/Services/Character/ReproductionDurableEquipmentUseRuntime.cs");
        string reproductionPolicy = Read(root,
            "Assets/Scripts/Services/Character/ReproductionDurableEquipmentPolicySource.cs");
        string reproductionFixture = Read(root,
            "Assets/Scripts/Services/Character/Editor/ReproductionDurableEquipmentDebugScenarios.cs");
        Require(
            Count(careerAdapter, "TryRequestItemDelivery(") == 0
            && Count(careerAdapter, "TrySetInstanceComponent(") == 0
            && Count(careerAdapter, "GetAllStacks(") == 0
            && Count(careerAdapter, "careerEquipment.TryCommitAward(") == 1
            && Count(careerPolicy,
                "public sealed class CareerDurableEquipmentPolicySource") == 1
            && Count(careerFixture,
                "DurableFacilityEquipmentUseRuntimeDebugScenarios.RunAll();") == 1
            && Count(researchRegistration,
                "Register<CareerDurableEquipmentPolicySource>(") == 1
            && Count(reproductionAdapter, "TryRequestItemDelivery(") == 0
            && Count(reproductionAdapter, "TrySetInstanceComponent(") == 0
            && Count(reproductionAdapter, "items.GetAllStacks(") == 0
            && Count(reproductionAdapter,
                "breedingEquipment.TryCommitPlan(") == 1
            && Count(reproductionUse,
                "slots.TryReconcile(") == 1
            && Count(reproductionUse,
                "slots.TryEnsureSupply(") == 1
            && Count(reproductionUse,
                "use.TryApplyWearAndEffect(") == 1
            && Count(reproductionPolicy,
                "public sealed class ReproductionDurableEquipmentPolicySource") == 1
            && Count(reproductionFixture,
                "DurableFacilityEquipmentUseRuntimeDebugScenarios.RunAll();") == 1
            && Count(researchRegistration,
                "Register<ReproductionDurableEquipmentPolicySource>(") == 1,
            "Character career/reproduction common durable-equipment migration drifted.");

        string climateAdapter = Read(root,
            "Assets/Scripts/Services/Infrastructure/Environment/ClimateRuntime.cs");
        string climateUse = Read(root,
            "Assets/Scripts/Services/Infrastructure/Environment/ClimateDurableEquipmentRuntime.cs");
        string climatePolicy = Read(root,
            "Assets/Scripts/Services/Infrastructure/Environment/ClimateDurableEquipmentPolicySource.cs");
        string climateFixture = Read(root,
            "Assets/Scripts/Services/Infrastructure/Environment/Editor/ClimateDurableEquipmentDebugScenarios.cs");
        string climateLifecycle = Read(root,
            "Assets/Scripts/Services/Infrastructure/Environment/ClimateDurableEquipmentLifecycleRuntime.cs");
        Require(
            Count(climateAdapter, "TryRequestItemDelivery(") == 0
            && Count(climateAdapter, "TrySetInstanceComponent(") == 0
            && Count(climateAdapter, "GetAllStacks(") == 0
            && Count(climateAdapter, "observationEquipment.TryMaintain(") == 1
            && Count(climateUse, "slots.TryReconcile(") == 1
            && Count(climateUse, "slots.TryEnsureSupply(") == 1
            && Count(climateUse, "use.TryApplyWearAndEffect(") == 2
            && Count(climatePolicy,
                "public sealed class ClimateDurableEquipmentPolicySource") == 1
            && Count(researchRegistration,
                "Register<ClimateDurableEquipmentPolicySource>(") == 1
            && Count(researchRegistration,
                "RegisterEntryPoint<ClimateDurableEquipmentLifecycleRuntime>(") == 1
            && Count(climateLifecycle, "public void ValidateBeforeCapture()") == 1
            && Count(climateLifecycle, "commands.TryClose(") == 1
            && Count(climateFixture,
                "DurableFacilityEquipmentUseRuntimeDebugScenarios.RunAll();") == 1,
            "Climate common durable-equipment migration drifted.");

        string administrationAdapter = Read(root,
            "Assets/Scripts/Services/Run/V20ContentResolutionService.cs");
        string administrationEquipment = Read(root,
            "Assets/Scripts/Services/Run/RunAdministrativeSealDurableEquipmentRuntime.cs");
        string invasionAdapter = Read(root,
            "Assets/Scripts/Services/Invasion/InvasionDirectorRuntime.cs");
        string invasionEquipment = Read(root,
            "Assets/Scripts/Services/Invasion/InvasionSignalHornDurableEquipmentRuntime.cs");
        string eventToolLifecycle = Read(root,
            "Assets/Scripts/Services/Items/RunInvasionDurableEquipmentLifecycleRuntime.cs");
        string eventToolFixture = Read(root,
            "Assets/Scripts/Services/Run/Editor/RunInvasionDurableEquipmentDebugScenarios.cs");
        Require(
            Count(administrationAdapter, "TryRequestItemDelivery(") == 0
            && Count(administrationAdapter, "TrySetInstanceComponent(") == 0
            && Count(administrationAdapter,
                "administrativeSealEquipment.TryCommitResolution(") == 1
            && Count(administrationEquipment,
                "public sealed class RunAdministrativeSealDurableEquipmentPolicySource") == 1
            && Count(administrationEquipment, "slots.TryReconcile(") == 1
            && Count(administrationEquipment, "slots.TryEnsureSupply(") == 1
            && Count(administrationEquipment, "use.TryApplyWearAndEffect(") == 1
            && Count(invasionAdapter, "TryRequestItemDelivery(") == 0
            && Count(invasionAdapter, "TrySetInstanceComponent(") == 0
            && Count(invasionAdapter, "GetAllStacks(") == 0
            && Count(invasionAdapter, "signalHornEquipment.TryEnsureReady(") == 1
            && Count(invasionAdapter, "signalHornEquipment.TryCommitRally(") == 1
            && Count(invasionEquipment,
                "public sealed class InvasionSignalHornDurableEquipmentPolicySource") == 1
            && Count(invasionEquipment, "slots.TryReconcile(") == 1
            && Count(invasionEquipment, "slots.TryEnsureSupply(") == 1
            && Count(invasionEquipment, "use.TryApplyWearAndEffect(") == 1
            && Count(eventToolLifecycle, "public void ValidateBeforeCapture()") == 1
            && Count(eventToolLifecycle, "commands.TryClose(") == 1
            && Count(researchRegistration,
                "Register<RunAdministrativeSealDurableEquipmentPolicySource>(") == 1
            && Count(researchRegistration,
                "Register<InvasionSignalHornDurableEquipmentPolicySource>(") == 1
            && Count(researchRegistration,
                "RegisterEntryPoint<RunInvasionDurableEquipmentLifecycleRuntime>(") == 1
            && Count(eventToolFixture,
                "DurableFacilityEquipmentUseRuntimeDebugScenarios.RunAll();") == 1
            && Count(eventToolFixture,
                "DurableFacilityEquipmentSaveDebugScenarios.RunAll();") == 1,
            "Run/invasion common durable-equipment migration drifted.");

        string defenseCombat = Read(root,
            "Assets/Scripts/Services/Invasion/DefenseCombatExecutor.cs");
        string defenseKitSupply = Read(root,
            "Assets/Scripts/Services/Invasion/InvasionDefenseKitSupplyRuntime.cs");
        string defenseKitFixture = Read(root,
            "Assets/Scripts/Services/Invasion/Editor/InvasionDefenseKitSupplyDebugScenarios.cs");
        Require(
            Count(defenseCombat, "TryRequestItemDelivery(") == 0
            && Count(defenseCombat, "defenseKitSupply.TryEnsureReady(") == 1
            && Count(defenseKitSupply,
                "public sealed class InvasionDefenseKitSupplyPolicySource") == 1
            && Count(defenseKitSupply, "slots.TryReconcile(") == 1
            && Count(defenseKitSupply, "slots.TryEnsureSupply(") == 1
            && Count(defenseKitSupply, "public void ValidateBeforeCapture()") == 1
            && Count(researchRegistration,
                "Register<InvasionDefenseKitSupplyPolicySource>(") == 1
            && Count(researchRegistration,
                "RegisterEntryPoint<InvasionDefenseKitSupplyLifecycleRuntime>(") == 1
            && defenseKitFixture.Contains(
                "VerifyExactPolicyAndPositiveAuthoredMass",
                StringComparison.Ordinal)
            && defenseKitFixture.Contains(
                "VerifyLostOwnerClosesCommonSlot",
                StringComparison.Ordinal),
            "Invasion defense-kit exact owner migration drifted.");

        string equipmentRepair = Read(root,
            "Assets/Scripts/Services/Combat/EquipmentMaintenanceRuntime.cs");
        string equipmentRepairRestoreGuard = Read(root,
            "Assets/Scripts/Services/Combat/EquipmentRepairMaterialRestoreGuard.cs");
        string equipmentRepairFixture = Read(root,
            "Assets/Scripts/Services/Combat/Editor/EquipmentRepairMaterialOutboxFixture.cs");
        string equipmentRepairPlayMode = Read(root,
            "Assets/Scripts/Services/Items/Editor/PhysicalItemLogisticsPlayModeVerifier.cs");
        Require(Count(
                    equipmentRepair,
                    "destinationLifecycle.TryReplaceOwnedAuthorities(") == 2
                && Count(equipmentRepair, "destinationClaimCommands.TryClaim(") == 0
                && Count(equipmentRepair, "destinationClaimCommands.TryRevoke(") == 0
                && Count(equipmentRepair, "destinationClaimCommands.TryReplaceOwnedClaims(") == 0
                && Count(equipmentRepair, "RepairBufferCapacitySchemaRevision = 1L") == 1
                && Count(equipmentRepair, "RequireRepairBufferAuthority(order, facility);") == 1
                && Count(equipmentRepair, "EquipmentRepairMaterialOutbox.TryCommitOrResume(") == 1
                && Count(equipmentRepair, "EquipmentItemStateCodec.Encode(instance, attachedModules)") == 1
                && Count(equipmentRepair, ".Multiply(materialQuantity)") == 1
                && Count(equipmentRepair, "new PhysicalMassGrams(totalMass)") == 1
                && Count(equipmentRepairRestoreGuard, "ValidateOwnerSet(") == 4
                && Count(
                    equipmentRepairRestoreGuard,
                    "terminalEffects.TerminalEffects") == 1
                && equipmentRepairFixture.Contains(
                    "VerifyFacilityBufferAuthorityLifecycle",
                    StringComparison.Ordinal)
                && equipmentRepairPlayMode.Contains(
                    "MATERIAL_REPAIR_POSITIVE_GRAM_PROFILE_EXACT",
                    StringComparison.Ordinal)
                && equipmentRepairPlayMode.Contains(
                    "MATERIAL_REPAIR_CAPACITY_PROFILE_ZERO_AFTER_COMPLETE",
                    StringComparison.Ordinal),
            "Equipment-repair common profile/token/restore/WIP/terminal migration drifted.");

        string claimSource = Read(root,
            "Assets/Scripts/Services/Items/FacilityBufferDestinationClaimRegistry.cs");
        foreach (string prefix in new[]
                 {
                     "ProductionInputPrefix", "ExpeditionPrefix",
                     "EquipmentRepairPrefix", "ProductionStockSensorPrefix",
                     "SurgeryMaterialsPrefix",
                     "ResearchArchivePrefix", "PowerFuelPrefix"
                 })
        {
            Require(Count(claimSource, "const string " + prefix + " =") == 1,
                "Exact claim prefix drifted: " + prefix);
        }

        string surgeryMaterialAuthority = Read(
            root,
            "Assets/Scripts/Services/Medical/SurgeryMaterialDestinationRuntime.cs");
        string surgeryRuntime = Read(
            root,
            "Assets/Scripts/Services/Medical/SurgeryRuntime.cs");
        string surgeryDelivery = Read(
            root,
            "Assets/Scripts/Services/Medical/SurgeryLogisticsRuntime.cs");
        string surgeryTerminal = Read(
            root,
            "Assets/Scripts/Services/Medical/SurgeryMaterialTerminalRuntime.cs");
        string surgeryTerminalGc = Read(
            root,
            "Assets/Scripts/Services/Medical/SurgeryMaterialTerminalCheckpointGc.cs");
        string surgeryTerminalPreflight = Read(
            root,
            "Assets/Scripts/Services/Medical/SurgeryMaterialTerminalCrossAggregateSaveValidation.cs");
        string ownerNeutralDrain = Read(
            root,
            "Assets/Scripts/Services/Items/FacilityBufferDestinationCustodyDrainService.cs");
        string warehouseDeliveryPolicy = Read(
            root,
            "Assets/Scripts/Services/Items/WorldItemWarehouseService.cs");
        Require(
            surgeryMaterialAuthority.Contains(
                "SurgeryMaterialCapacityFingerprint.Create(order)",
                StringComparison.Ordinal)
            && surgeryMaterialAuthority.Contains(
                "lifecycle.TryReplaceOwnedAuthorities(",
                StringComparison.Ordinal)
            && surgeryRuntime.Contains(
                "materialDestinations.TryClaim(",
                StringComparison.Ordinal)
            && surgeryDelivery.Contains(
                "SurgeryMaterialSinkIdentity.FormatOperationId(",
                StringComparison.Ordinal)
            && surgeryTerminal.Contains(
                "IFacilityBufferDestinationCustodyDrainService",
                StringComparison.Ordinal)
            && surgeryTerminalGc.Contains(
                "ClosedAwaitingCheckpointGc",
                StringComparison.Ordinal)
            && surgeryTerminalGc.Contains(
                "childGc.RollbackCheckpointGarbageCollection",
                StringComparison.Ordinal)
            && surgeryTerminalPreflight.Contains(
                "IDungeonCapturedSavePreflightValidator",
                StringComparison.Ordinal)
            && surgeryTerminalPreflight.Contains(
                "SurgeryMaterialTerminalCrossAggregateJoin.Validate(",
                StringComparison.Ordinal)
            && ownerNeutralDrain.Contains(
                "IFacilityBufferDestinationCustodyDrainCheckpointGcPort",
                StringComparison.Ordinal)
            && warehouseDeliveryPolicy.Contains(
                "RequiresFacilityBufferMassAdmission(",
                StringComparison.Ordinal)
            && !warehouseDeliveryPolicy.Contains(
                "ReservedTargetDestinationIdentity.SurgeryMaterialsPrefix",
                StringComparison.Ordinal),
            "Surgery exact-gram destination capability or sink join drifted.");

        string offenseSupply = Read(root,
            "Assets/Scripts/Services/Offense/OffensePreparationService.cs");
        string warehouseDelivery = Read(root,
            "Assets/Scripts/Services/Items/WorldItemWarehouseService.cs");
        Require(Count(
                    offenseSupply,
                    "destinationLifecycle.TryReplaceOwnedAuthorities(") == 1
                && Count(
                    offenseSupply,
                    "itemGateway.GetDefinitionQuantityMassGrams(") == 1
                && Count(
                    offenseSupply,
                    "InputBufferCapacitySchemaRevision = 1L") == 1
                && Count(
                    warehouseDelivery,
                    "ReservedTargetDestinationIdentity.ExpeditionPrefix") == 1,
            "Expedition supply exact claim/profile/mass admission migration drifted.");

        string stockSensorAuthority = Read(root,
            "Assets/Scripts/Services/Economy/ProductionStockSensorDestinationAuthorityRuntime.cs");
        string stockSensorRuntime = Read(root,
            "Assets/Scripts/Models/Economy/Content/ProductionStockSensorRuntime.cs");
        string productionBillRuntime = Read(root,
            "Assets/Scripts/Models/Economy/Content/ProductionBillRuntime.cs");
        string productionMutationFence = Read(root,
            "Assets/Scripts/Services/Economy/ProductionFacilityMutationFence.cs");
        string worldItemRuntime = Read(root,
            "Assets/Scripts/Services/Items/WorldItemStackRuntime.cs");
        string stockSensorPersistenceFixture = Read(root,
            "Assets/Scripts/Services/Economy/Editor/ProductionPreparedOutputFullPersistenceDebugScenarios.cs");
        string stockSensorLifecycleFixture = Read(root,
            "Assets/Scripts/Services/Economy/Diagnostics/ProductionOutputDestinationLifecycleDebugScenarios.cs");
        string worldSimulationRegistration = Read(root,
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonWorldSimulationRegistration.cs");
        string saveRegistration = Read(root,
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonSaveRegistration.cs");
        string relocationWorld = Read(root,
            "Assets/Scripts/Services/FacilityEvolution/FacilityRelocationWorldService.cs");
        string relocationFixture = Read(root,
            "Assets/Scripts/Services/FacilityEvolution/Editor/FacilityRelocationCompletionFenceFixture.cs");
        string destructiveRecovery = Read(root,
            "Assets/Scripts/Services/Buildings/ProductionFacilityDestructiveDrainRecoveryRuntime.cs");
        string destructiveFacade = Read(root,
            "Assets/Scripts/Services/Buildings/BuildingDestructiveLossRuntime.cs");
        string gridBuilding = Read(root,
            "Assets/Scripts/Services/Grid/Building/GridBuildingRuntime.cs");
        string structuralIntegrity = Read(root,
            "Assets/Scripts/Services/Buildings/BuildingStructuralIntegrityRuntime.cs");
        string combatCover = Read(root,
            "Assets/Scripts/Services/Combat/CombatCoverDurability.cs");
        string debugCommands = Read(root,
            "Assets/Scripts/Services/Debugging/DungeonDebugCommandProviders.cs");
        string combatRegistration = Read(root,
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonCombatRegistration.cs");
        string mutationEpochRuntime = Read(root,
            "Assets/Scripts/Services/Economy/ProductionFacilityMutationEpochRuntime.cs");
        string mutationAuthorityGate = Read(root,
            "Assets/Scripts/Services/Economy/ProductionFacilityMutationAuthorityGate.cs");
        string activeRetarget = Read(root,
            "Assets/Scripts/Services/Economy/ProductionActiveMultiFacilityRetargetAdapter.cs");
        string activeRetargetFixture = Read(root,
            "Assets/Scripts/Services/Economy/Editor/ProductionActiveMultiFacilityRetargetDebugScenarios.cs");
        Require(
            Count(worldSimulationRegistration,
                "Register<ProductionGenericBillIdentityRetargetAdapter>(") == 0
            && Count(worldSimulationRegistration,
                "Register<ProductionActiveFacilityRetargetStateStore>(") == 1
            && Count(worldSimulationRegistration,
                ".As<IProductionActiveFacilityRetargetStateStore>()") == 1
            && Count(worldSimulationRegistration,
                "Register<ProductionActiveMultiFacilityRetargetAdapter>(") == 1
            && Count(worldSimulationRegistration,
                "Register<ProductionPreparedRoutingIdentityRetargetAdapter>(") == 1
            && activeRetarget.Contains(
                "active-multi-facility-retarget@1",
                StringComparison.Ordinal)
            && activeRetarget.Contains(
                "ProductionActiveFacilityRetargetSnapshotProjector.TryProject(",
                StringComparison.Ordinal)
            && activeRetargetFixture.Contains(
                "VerifyOneFailureRestoresExactAuthoritySet",
                StringComparison.Ordinal),
            "Active multi-facility retarget registration or exact rollback drifted.");
        string admissionFenceQuery = Read(root,
            "Assets/Scripts/Services/Items/FacilityBufferDestinationAdmissionFenceQuery.cs");
        string massAdmission = Read(root,
            "Assets/Scripts/Services/Items/FacilityBufferMassAdmissionService.cs");
        Require(
            Count(worldSimulationRegistration,
                "Register<ProductionFacilityMutationAdmissionFenceSource>(") == 1
            && Count(worldSimulationRegistration,
                "Register<ProductionFacilityDestructiveDrainAdmissionFenceSource>(") == 0
            && Count(worldSimulationRegistration,
                ".As<IProductionFacilityMutationEpochQuery>()") == 1
            && Count(mutationEpochRuntime,
                "public bool TryCaptureOpen(") == 1
            && Count(mutationAuthorityGate,
                "public bool TryCaptureOpen(") == 1
            && Count(admissionFenceQuery,
                "public sealed class ProductionFacilityMutationAdmissionFenceSource") == 1
            && Count(admissionFenceQuery,
                "mutations.TryCaptureOpen(") == 1
            && Count(massAdmission, "OwnerMutationFenceOpen") == 7
            && Count(massAdmission, "IsOwnerMutationFenceOpen(") == 3,
            "Unified production facility mutation admission fence drifted.");
        Require(
            Count(stockSensorAuthority, "public const string OwnerDomain = \"economy.production-sensor\";") == 1
            && Count(stockSensorAuthority, "public const long CapacitySchemaRevision = 1L;") == 1
            && Count(stockSensorAuthority, "lifecycle.TryReplaceOwnedAuthorities(") == 3
            && Count(stockSensorRuntime, "destinationAuthorities.TryEnsure(") == 1
            && Count(stockSensorRuntime, "destinationAuthorities.TryReplaceProjected(") == 1
            && Count(productionBillRuntime, "stockSensors.TryReconcileDestinationAuthorities(") == 2
            && Count(productionMutationFence, "stockSensors.HasOwnedPhysicalState(handle)") == 2
            && Count(productionMutationFence, "stockSensorAuthority.TryRequireEmpty(") == 3
            && Count(warehouseDelivery,
                "ReservedTargetDestinationIdentity.ProductionStockSensorPrefix") == 2
            && Count(worldItemRuntime,
                "ReservedTargetDestinationIdentity.ProductionStockSensorPrefix") == 1
            && Count(worldSimulationRegistration,
                "Register<ProductionStockSensorDestinationAuthorityRuntime>(") == 1
            && Count(worldSimulationRegistration,
                ".As<IProductionFacilityDestructiveDrainParticipant>()") == 6
            && Count(worldSimulationRegistration,
                "Register<ProductionFacilityDestructiveDrainParticipantRegistry>(") == 1
            && Count(worldSimulationRegistration,
                "Register<ProductionFacilityDestructiveDrainJournal>(") == 1
            && Count(worldSimulationRegistration,
                "Register<ProductionFacilityDestructiveDrainCoordinator>(") == 1
            && Count(worldSimulationRegistration,
                "Register<ProductionPhysicalCustodyDrainService>(") == 1
            && Count(worldSimulationRegistration,
                "Register<CombatEquipmentTerminalDrainOutbox>(") == 1
            && Count(worldSimulationRegistration,
                "Register<ProductionApparelOrderTerminalDrainOutbox>(") == 1
            && Count(saveRegistration,
                "Register<ProductionGenericBillTerminalDrainSaveSection>(") == 1
            && Count(saveRegistration,
                "Register<CombatEquipmentTerminalDrainSaveSection>(") == 1
            && Count(saveRegistration,
                "Register<ProductionApparelOrderTerminalDrainSaveSection>(") == 1
            && Count(saveRegistration,
                "Register<ProductionFacilityDestructiveDrainSaveSection>(") == 1
            && Count(relocationWorld, "retarget.TryBegin(") == 1
            && Count(relocationWorld, "retarget.TryCommit(") == 1
            && Count(relocationWorld, "retarget.TryComplete(") == 1
            && relocationFixture.Contains(
                "qa-late-stock-sensor-delivery",
                StringComparison.Ordinal)
            && stockSensorPersistenceFixture.Contains(
                "VerifyStockSensorExactAdmissionAndRetry",
                StringComparison.Ordinal)
            && stockSensorPersistenceFixture.Contains(
                "A live sensor lot allowed same-ID authority anchor mutation.",
                StringComparison.Ordinal)
            && stockSensorLifecycleFixture.Contains(
                "VerifyStockSensorRollbackWhenOutputRevokeFails",
                StringComparison.Ordinal),
            "Production stock-sensor one-panel authority/admission/restore/mutation migration drifted.");

        // This gate intentionally reports every destructive-drain ratchet count;
        // stale Editor assemblies otherwise make source/verification drift opaque.
        Require(
            Count(gridBuilding, "destructiveLoss.Apply(") == 1
            && Count(gridBuilding,
                "ProductionFacilityDestructiveDrainCause.ExplicitDemolition") == 1
            && Count(structuralIntegrity, "destructiveLoss.Apply(") == 1
            && Count(structuralIntegrity,
                "ProductionFacilityDestructiveDrainCause.StructuralIntegrity") == 1
            && Count(combatCover, "destructiveLoss.Apply(") == 1
            && Count(combatCover,
                "ProductionFacilityDestructiveDrainCause.CombatCover") == 1
            && Count(destructiveFacade, "drains.RequestAndDrive(") == 1
            && Count(destructiveRecovery,
                "coordinator.DriveToAuthorityRevoke(") == 2
            && Count(destructiveRecovery, "revoker.TryConverge(") == 1
            && Count(destructiveRecovery,
                "coordinator.RecordAuthorityRevoked(") == 1
            && Count(destructiveRecovery, "world.TryEnsureRemoved(") == 1
            && Count(destructiveRecovery,
                "coordinator.RecordWorldRemoved(") == 1
            && Count(destructiveRecovery, "facility.DestroySelf();") == 1
            && Count(debugCommands, "building.DestroySelf();") == 0
            && Count(debugCommands, "destructiveLoss.Apply(") == 1
            && Count(worldSimulationRegistration,
                "Register<ProductionFacilityDestructiveDrainAuthorityStateQuery>(") == 1
            && Count(worldSimulationRegistration,
                "Register<ProductionFacilityDestructiveDrainAuthorityRevoker>(") == 1
            && Count(worldSimulationRegistration,
                "Register<ProductionFacilityDestructiveDrainWorldRemovalPort>(") == 1
            && Count(worldSimulationRegistration,
                "ProductionFacilityDestructiveDrainRecoveryRuntime>(") == 2
            && Count(worldSimulationRegistration,
                ".As<IProductionFacilityDestructiveDrainRecoveryRuntime>()") == 1
            && Count(worldSimulationRegistration,
                ".As<IDungeonSaveRestoreCompletedHook>()") == 3
            && Count(worldSimulationRegistration,
                ".As<IDungeonSaveCaptureGuard>()") == 10
            && Count(combatRegistration,
                "Register<BuildingDestructiveLossRuntime>(") == 1
            && Count(saveRegistration,
                "Register<ProductionFacilityDestructiveDrainCrossAggregateSaveValidation>(") == 1
            && CountAllRuntimeSources(root, ".TryPrepareEmpty(") == 0
            && CountAllRuntimeSources(root, ".TryCommitAuthorityRevoke(") == 0
            && CountAllRuntimeSources(root, ".TryAbort(") == 0
            && CountAllRuntimeSources(root, ".DestroySelf();") == 4,
            "Live destructive-drain facade/recovery/save/legacy-bypass topology drifted: "
            + $"captureGuards={Count(worldSimulationRegistration, ".As<IDungeonSaveCaptureGuard>()")}; "
            + $"restoreHooks={Count(worldSimulationRegistration, ".As<IDungeonSaveRestoreCompletedHook>()")}; "
            + $"runtimePrepare={CountAllRuntimeSources(root, ".TryPrepareEmpty(")}; "
            + $"runtimeCommit={CountAllRuntimeSources(root, ".TryCommitAuthorityRevoke(")}; "
            + $"runtimeAbort={CountAllRuntimeSources(root, ".TryAbort(")}; "
            + $"runtimeDestroy={CountAllRuntimeSources(root, ".DestroySelf();")}");

        Require(CountAllProductionSources(root, "SpawnFacilityBufferItem(") == 0
                && CountAllProductionSources(
                    root, "SpawnExistingFacilityBufferUniqueItem(") == 0,
            "Removed building item-stack FacilityBuffer surface was reintroduced.");

        string conveyorGateway = Read(
            root,
            "Assets/Scripts/Services/Infrastructure/Industrial/ConveyorItemGateway.cs");
        string itemTransfer = Read(
            root,
            "Assets/Scripts/Services/Items/ItemTransferService.cs");
        string physicalStockScenarios = Read(
            root,
            "Assets/Scripts/Services/Items/Editor/PhysicalStockQueryV18DebugScenarios.cs");
        string conveyorCustody = Read(
            root,
            "Assets/Scripts/Services/Infrastructure/Industrial/ConveyorPhysicalCustodySaveValidation.cs");
        string industrialSaveValidation = Read(
            root,
            "Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureSaveValidation.cs");
        string industrialPlayMode = Read(
            root,
            "Assets/Scripts/Services/Infrastructure/Industrial/Editor/IndustrialInfrastructurePlayModeVerifier.cs");
        string v20ContentResolution = Read(
            root,
            "Assets/Scripts/Services/Run/V20ContentResolutionService.cs");
        string exteriorIncidentHandlers = Read(
            root,
            "Assets/Scripts/Services/Infrastructure/Exterior/ExteriorIncidentHandlers.cs");
        string exactSourcePublication = Read(
            root,
            "Assets/Scripts/Services/Items/PhysicalItemExactSourcePublicationService.cs");
        string worldResourceRuntime = Read(
            root,
            "Assets/Scripts/Models/Economy/Content/WorldResourceRuntime.cs");
        string worldResourceAdapter = Read(
            root,
            "Assets/Scripts/Services/Economy/WorldResourcePortAdapters.cs");
        string equipmentModuleRuntime = Read(
            root,
            "Assets/Scripts/Services/Combat/EquipmentModuleRuntime.cs");
        string equipmentModuleBinder = Read(
            root,
            "Assets/Scripts/Services/Combat/EquipmentModulePreparedOutputBinder.cs");
        Require(!Read(root, "Assets/Scripts/Services/Infrastructure/BlueprintResearchRuntime.cs")
                    .Contains("EnsureAcquiredBlueprintItemsMaterialized(", StringComparison.Ordinal)
                && !equipmentModuleRuntime.Contains(
                    "SpawnExistingUniqueItemAt(",
                    StringComparison.Ordinal)
                && equipmentModuleRuntime.Contains(
                    "exactSources.TryPrepare(",
                    StringComparison.Ordinal)
                && equipmentModuleRuntime.Contains(
                    "exactSources.TryCommitReleased(",
                    StringComparison.Ordinal)
                && equipmentModuleBinder.Contains(
                    "IFacilityBufferPlannedUniqueOutputBinder",
                    StringComparison.Ordinal)
                && !v20ContentResolution.Contains("transfers.TrySpawnItem(", StringComparison.Ordinal)
                && v20ContentResolution.Contains("exactSources.TryPrepare(", StringComparison.Ordinal)
                && v20ContentResolution.Contains("exactSources.TryCommitReleased(", StringComparison.Ordinal)
                && !worldResourceRuntime.Contains("SpawnOutput(", StringComparison.Ordinal)
                && worldResourceRuntime.Contains("outputPublication.TryPrepare(", StringComparison.Ordinal)
                && worldResourceRuntime.Contains("outputPublication.CommitReleased(", StringComparison.Ordinal)
                && worldResourceAdapter.Contains("exactSources.TryPrepare(", StringComparison.Ordinal)
                && worldResourceAdapter.Contains("exactSources.TryCommitReleased(", StringComparison.Ordinal)
                && worldSimulationRegistration.Contains(
                    ".As<IWorldResourceOutputPublicationPort>()",
                    StringComparison.Ordinal)
                && !exteriorIncidentHandlers.Contains("items.SpawnItemAt(", StringComparison.Ordinal)
                && exteriorIncidentHandlers.Contains("exactSources.TryCommitRetained(", StringComparison.Ordinal)
                && exteriorIncidentHandlers.Contains("exactSources.TryReleaseRetained(", StringComparison.Ordinal)
                && exteriorIncidentHandlers.Contains("exactSources.TrySinkRetained(", StringComparison.Ordinal)
                && exactSourcePublication.Contains(
                    "IPhysicalItemExactSourceRestoreAuthorityCommand",
                    StringComparison.Ordinal)
                && worldSimulationRegistration.Contains(
                    ".As<IPhysicalItemExactSourcePublicationService>()",
                    StringComparison.Ordinal)
                && worldSimulationRegistration.Contains(
                    ".As<IPhysicalItemExactSourceRestoreAuthorityCommand>()",
                    StringComparison.Ordinal),
            "Exact source publication migration or a direct Source bypass drifted.");
        Require(
            Count(conveyorGateway, "TryCompleteTransitToFacilityBuffer(") == 1
            && !conveyorGateway.Contains(
                "WorldItemStackState.FacilityBuffer",
                StringComparison.Ordinal)
            && itemTransfer.Contains(
                "if (destinationState != WorldItemStackState.Loose)",
                StringComparison.Ordinal)
            && Count(
                physicalStockScenarios,
                "TryCompleteTransitToFacilityBuffer(") >= 4
            && physicalStockScenarios.Contains(
                "DebugFailBeforeFacilityTransitAdmissionCommit",
                StringComparison.Ordinal)
            && conveyorCustody.Contains(
                "InTransit physical stack",
                StringComparison.Ordinal)
            && conveyorCustody.Contains(
                "payload.itemStackId",
                StringComparison.Ordinal)
            && industrialSaveValidation.Contains(
                "payloadStackIds.Add(payload.itemStackId)",
                StringComparison.Ordinal)
            && industrialPlayMode.Contains(
                "retry=retained",
                StringComparison.Ordinal)
            && industrialPlayMode.Contains(
                "SaveEnvelopesEqual(originalWorld, after)",
                StringComparison.Ordinal),
            "Conveyor arrival-time exact admission or generic bypass closure drifted.");
    }

    private static void VerifyRegisteredProductionOutputHandlers(string root)
    {
        string scripts = Path.Combine(root, "Assets", "Scripts");
        string[] registered = Directory.GetFiles(
                scripts,
                "*.cs",
                SearchOption.AllDirectories)
            .Where(path => CanonicalPath(Path.GetRelativePath(root, path))
                .IndexOf("/Editor/", StringComparison.Ordinal) < 0)
            .SelectMany(path => ProductionOutputHandlerRegistrationPattern
                .Matches(File.ReadAllText(path))
                .Cast<Match>()
                .Select(match => match.Groups["handler"].Value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] classified = Rows
            .Where(value => IsOutputState(value.State))
            .SelectMany(value => value.ProducerSymbol.Split('|'))
            .Where(value => value.EndsWith(
                "ProductionOutputHandler",
                StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(registered.SequenceEqual(classified, StringComparer.Ordinal),
            "Registered production output handler census drifted: registered="
            + string.Join(",", registered)
            + "; classified=" + string.Join(",", classified));
    }

    private static bool IsOutputState(string state) => state is
        "FacilityOutputBuffer" or "DirectLooseOutput";

    private static byte[] BuildCsv(
        IReadOnlyList<OwnerRow> rows,
        string sourceDigest)
    {
        using MemoryStream stream = new MemoryStream();
        V27Utf8CsvWriter writer = new V27Utf8CsvWriter(stream, 16384);
        WriteCsvRow(writer, new[]
        {
            "schemaVersion", "state", "ownerDomain", "destinationRule",
            "producerSymbol", "claimAuthority", "capacityAuthority",
            "consumerAndPersistence", "cancelRelease", "disposition",
            "sourcePath", "sourceDigest"
        });
        foreach (OwnerRow row in rows)
        {
            WriteCsvRow(writer, new[]
            {
                "1", row.State, row.OwnerDomain, row.DestinationRule,
                row.ProducerSymbol, row.ClaimAuthority, row.CapacityAuthority,
                row.ConsumerAndPersistence, row.CancelRelease, row.Disposition,
                row.SourcePath, sourceDigest
            });
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteCsvRow(
        V27Utf8CsvWriter writer,
        IReadOnlyList<string> fields)
    {
        for (int index = 0; index < fields.Count; index++)
        {
            if (index != 0) writer.WriteAscii(',');
            writer.WriteEscapedField((fields[index] ?? string.Empty).AsSpan());
        }
        writer.WriteCrLf();
    }

    private static string ComputeCanonicalSourceDigest(
        string root,
        IEnumerable<string> paths)
    {
        using SHA256 sha = SHA256.Create();
        foreach (string path in paths.OrderBy(value => value, StringComparer.Ordinal))
        {
            string source = Read(root, path).Replace("\r\n", "\n");
            byte[] bytes = Encoding.UTF8.GetBytes(path + "\n" + source + "\n");
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Hex(sha.Hash);
    }

    private static string ComputeCanonicalLineSetDigest(
        IEnumerable<string> lines)
    {
        using SHA256 sha = SHA256.Create();
        foreach (string line in lines.OrderBy(
                     value => value, StringComparer.Ordinal))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(
                (line ?? string.Empty) + "\n");
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Hex(sha.Hash);
    }

    private static int CountAllProductionSources(string root, string token)
    {
        string scripts = Path.Combine(root, "Assets", "Scripts");
        int count = 0;
        foreach (string path in Directory.GetFiles(
                     scripts, "*.cs", SearchOption.AllDirectories))
        {
            string canonical = CanonicalPath(Path.GetRelativePath(root, path));
            if (canonical.IndexOf("/Editor/", StringComparison.Ordinal) >= 0)
                continue;
            count += Count(File.ReadAllText(path), token);
        }
        return count;
    }

    private static int CountAllRuntimeSources(string root, string token)
    {
        string scripts = Path.Combine(root, "Assets", "Scripts");
        int count = 0;
        foreach (string path in Directory.GetFiles(
                     scripts, "*.cs", SearchOption.AllDirectories))
        {
            string canonical = CanonicalPath(Path.GetRelativePath(root, path));
            if (canonical.IndexOf("/Editor/", StringComparison.Ordinal) >= 0
                || canonical.IndexOf("/Diagnostics/", StringComparison.Ordinal) >= 0)
            {
                continue;
            }
            count += Count(File.ReadAllText(path), token);
        }
        return count;
    }

    private static int Count(string source, string token)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
    }

    private static string Read(string root, string path) =>
        File.ReadAllText(Path.Combine(
            root, path.Replace('/', Path.DirectorySeparatorChar)));

    private static string CanonicalPath(string path) =>
        (path ?? string.Empty).Replace('\\', '/');

    private static byte[] Utf8(string text) =>
        new UTF8Encoding(false, true).GetBytes(text);

    private static string Hex(byte[] bytes)
    {
        const string Alphabet = "0123456789abcdef";
        char[] result = new char[bytes.Length * 2];
        for (int index = 0; index < bytes.Length; index++)
        {
            result[index * 2] = Alphabet[bytes[index] >> 4];
            result[index * 2 + 1] = Alphabet[bytes[index] & 0x0f];
        }
        return new string(result);
    }

    private static OwnerRow Input(
        string owner, string destination, string producer, string claim,
        string capacity, string consumer, string cancel, string disposition,
        string path) => new OwnerRow(
            "FacilityBuffer", owner, destination, producer, claim, capacity,
            consumer, cancel, disposition, path);

    private static OwnerRow Output(
        string owner, string destination, string producer, string capacity,
        string disposition, string path) => new OwnerRow(
            "FacilityOutputBuffer", owner, destination, producer,
            "output owner", capacity, "output WIP/commit authority",
            "WaitingForOutputSpace/route", disposition, path);

    private static OwnerRow DirectOutput(
        string owner, string destination, string producer, string capacity,
        string disposition, string path) => new OwnerRow(
            "DirectLooseOutput", owner, destination, producer,
            "output owner", capacity, "output WIP/commit authority",
            "direct Loose publication", disposition, path);

    private static OwnerRow Bypass(
        string owner, string destination, string producer, string detail,
        string path) => new OwnerRow(
            "FacilityBuffer", owner, destination, producer, "owner-specific",
            "none", detail, "owner rollback only", "bypass", path);

    private static OwnerRow Orphan(
        string owner, string destination, string producer, string detail,
        string path) => new OwnerRow(
            "FacilityBuffer", owner, destination, producer, "none", "none",
            detail, "not applicable", "orphan", path);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private readonly struct DeliveryCallsite
    {
        public DeliveryCallsite(string sourcePath, int line, string api)
        {
            SourcePath = sourcePath;
            Line = line;
            Api = api;
        }
        public string SourcePath { get; }
        public int Line { get; }
        public string Api { get; }
        public string CanonicalLine => SourcePath + "|"
            + Line.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "|" + Api;
    }

    private sealed class OwnerRow
    {
        public OwnerRow(
            string state, string ownerDomain, string destinationRule,
            string producerSymbol, string claimAuthority,
            string capacityAuthority, string consumerAndPersistence,
            string cancelRelease, string disposition, string sourcePath)
        {
            State = state;
            OwnerDomain = ownerDomain;
            DestinationRule = destinationRule;
            ProducerSymbol = producerSymbol;
            ClaimAuthority = claimAuthority;
            CapacityAuthority = capacityAuthority;
            ConsumerAndPersistence = consumerAndPersistence;
            CancelRelease = cancelRelease;
            Disposition = disposition;
            SourcePath = sourcePath;
        }
        public string State { get; }
        public string OwnerDomain { get; }
        public string DestinationRule { get; }
        public string ProducerSymbol { get; }
        public string ClaimAuthority { get; }
        public string CapacityAuthority { get; }
        public string ConsumerAndPersistence { get; }
        public string CancelRelease { get; }
        public string Disposition { get; }
        public string SourcePath { get; }
        public string Key => State + "|" + OwnerDomain + "|" + DestinationRule
            + "|" + ProducerSymbol;
    }

    private sealed class CaptureResult
    {
        public CaptureResult(
            byte[] csv, byte[] report, string summary, int remaining,
            int bypass, int orphan, int unclassifiedCallsites)
        {
            Csv = csv;
            Report = report;
            Summary = summary;
            Remaining = remaining;
            Bypass = bypass;
            Orphan = orphan;
            UnclassifiedCallsites = unclassifiedCallsites;
        }
        public byte[] Csv { get; }
        public byte[] Report { get; }
        public string Summary { get; }
        public int Remaining { get; }
        public int Bypass { get; }
        public int Orphan { get; }
        public int UnclassifiedCallsites { get; }
    }
}
#endif
