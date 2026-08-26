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
    private const int ExpectedDeliveryInvocationCount = 59;
    private const int ExpectedDeliveryInvocationFileCount = 39;

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
            ["Assets/Scripts/Models/Captivity/Core/CaptivityPerformerRuntime.cs"] = "captivity.performer",
            ["Assets/Scripts/Models/Economy/Content/WasteProcessingRuntime.cs"] = "economy.waste-processing",
            ["Assets/Scripts/Services/Captivity/CaptivityInteractionRuntime.cs"] = "captivity.interaction",
            ["Assets/Scripts/Services/Captivity/CaptivityRuntime.cs"] = "captivity.care-labor",
            ["Assets/Scripts/Services/Captivity/CaptivityUnityEffectsAdapter.cs"] = "adapter.captivity-effects",
            ["Assets/Scripts/Services/Captivity/CircusRuntime.cs"] = "captivity.circus",
            ["Assets/Scripts/Services/Captivity/WildlifeCaptureRuntime.cs"] = "captivity.wildlife-care",
            ["Assets/Scripts/Services/Character/CareerApplicationAdapter.cs"] = "character.career",
            ["Assets/Scripts/Services/Character/ReproductionCommandRuntime.cs"] = "character.reproduction",
            ["Assets/Scripts/Services/Character/Work/WorkAmountSystem.cs"] = "work.construction",
            ["Assets/Scripts/Services/Combat/CharacterMedicalSupplyCoordinator.cs"] = "medical.character-supply",
            ["Assets/Scripts/Services/Combat/CombatEquipmentCraftingRuntime.cs"] = "combat.equipment-crafting",
            ["Assets/Scripts/Services/Combat/EquipmentEvolutionRuntime.cs"] = "combat.equipment-evolution",
            ["Assets/Scripts/Services/Combat/EquipmentMaintenanceRuntime.cs"] = "combat.equipment-maintenance",
            ["Assets/Scripts/Services/Combat/EquipmentModuleRuntime.cs"] = "combat.equipment-module",
            ["Assets/Scripts/Services/Defense/DefenseFacilityRuntime.cs"] = "combat.defense-facility",
            ["Assets/Scripts/Services/Economy/CertifiedSeedRuntime.cs"] = "economy.certified-seed",
            ["Assets/Scripts/Services/Economy/CropEcologyRuntime.cs"] = "economy.crop-plot",
            ["Assets/Scripts/Services/Economy/Planning/RegionalSupplyContractApplicationAdapter.cs"] = "economy.regional-contract",
            ["Assets/Scripts/Services/Economy/Planning/ResourceStockPolicyRuntime.cs"] = "economy.stock-policy-project",
            ["Assets/Scripts/Services/Economy/ProductionItemGateway.cs"] = "economy.production",
            ["Assets/Scripts/Services/Economy/Waste/WasteProcessingPortAdapters.cs"] = "adapter.waste-processing",
            ["Assets/Scripts/Services/FacilityEvolution/FacilityInstanceEvolutionRuntime.cs"] = "facility.evolution",
            ["Assets/Scripts/Services/Infrastructure/BlueprintResearchRuntime.cs"] = "research.blueprint-archive",
            ["Assets/Scripts/Services/Infrastructure/Core/Captivity/CaptivityPerformerDefaultPort.cs"] = "adapter.captivity-performer",
            ["Assets/Scripts/Services/Infrastructure/Environment/ClimateRuntime.cs"] = "infrastructure.climate",
            ["Assets/Scripts/Services/Infrastructure/Industrial/ElectricalNetworkRuntime.cs"] = "infrastructure.electrical",
            ["Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs"] = "infrastructure.fluid",
            ["Assets/Scripts/Services/Infrastructure/Industrial/ProcessFluidUseRuntime.cs"] = "infrastructure.process-fluid",
            ["Assets/Scripts/Services/Infrastructure/KnowledgeResidueProcessingRuntime.cs"] = "research.knowledge-residue",
            ["Assets/Scripts/Services/Infrastructure/ResearchWorkExecutionAdapter.cs"] = "research.arcane-index",
            ["Assets/Scripts/Services/Invasion/DefenseCombatExecutor.cs"] = "invasion.defense-kit",
            ["Assets/Scripts/Services/Invasion/InvasionDirectorRuntime.cs"] = "invasion.signal-horn",
            ["Assets/Scripts/Services/Items/ItemTransferService.cs"] = "adapter.item-transfer",
            ["Assets/Scripts/Services/Items/WorldItemStackRuntime.cs"] = "adapter.world-item-runtime",
            ["Assets/Scripts/Services/Medical/SurgeryLogisticsRuntime.cs"] = "medical.surgery",
            ["Assets/Scripts/Services/Medical/SurgicalPartRuntime.cs"] = "medical.surgical-part-storage",
            ["Assets/Scripts/Services/Run/V20ContentResolutionService.cs"] = "run.v20-content-resolution",
            ["Assets/Scripts/Services/Survival/CharacterConsumablesApplicationAdapters.cs"] = "survival.character-consumables"
        };

    private static readonly OwnerRow[] Rows =
    {
        Input("economy.production", "production:{billId}", "ProductionInputLogisticsService", "exact ReservedTarget/LiveFacility claim", "per-bill 2-3 cycle exact gram bound", "ProductionBill V13 + Physical Items + HaulIntent", "atomic destination/carry release", "migrated", "Assets/Scripts/Services/Economy/ProductionInputLogisticsService.cs"),
        Input("economy.production-sensor", "production-sensor:{facilityId}", "ProductionStockSensorRuntime", "same-cell inferred facility", "none", "Production V14 sensor owner", "destination release", "remaining", "Assets/Scripts/Models/Economy/Content/ProductionStockSensorRuntime.cs"),
        Input("economy.grand-project", "grand-project:{projectId}", "GrandProjectRuntime", "same-cell inferred facility", "none", "GrandProject save", "destination release", "remaining", "Assets/Scripts/Models/Economy/Content/GrandProjectRuntime.cs"),
        Input("economy.regional-contract", "regional-contract:{contractId}", "RegionalSupplyContractRuntime", "same-cell inferred dropoff", "none", "contract save + Transfer outbox", "destination release", "remaining", "Assets/Scripts/Models/Economy/Content/RegionalSupplyContractRuntime.cs"),
        Input("survival.character-consumables", "facility-input:meal:{facilityId}|facility-input:recreation-substance:{facilityId}", "CharacterConsumablesApplicationAdapters", "same-cell inferred facility", "none", "Consumables V7/V8", "action cancellation release", "remaining", "Assets/Scripts/Services/Survival/CharacterConsumablesApplicationAdapters.cs"),
        Input("captivity.interaction", "captivity-interaction:{captiveId}:{interactionId}", "CaptivityInteractionRuntime", "same-cell inferred facility", "none", "Captivity state", "interaction cancel/release", "remaining", "Assets/Scripts/Services/Captivity/CaptivityInteractionRuntime.cs"),
        Input("captivity.care-labor", "captive-care:{id}|captive-labor-tool:{id}", "CaptivityRuntime", "same-cell inferred facility", "none", "Captivity/Circus save", "care/labor release", "remaining", "Assets/Scripts/Services/Captivity/CaptivityRuntime.cs"),
        Input("captivity.performer", "captive-care:{id}", "CaptivityPerformerRuntime", "same-cell inferred facility", "none", "Captivity state", "shared care owner", "remaining", "Assets/Scripts/Models/Captivity/Core/CaptivityPerformerRuntime.cs"),
        Input("captivity.circus", "{stagePersistentId}", "CircusRuntime", "same-cell inferred facility", "none", "Circus V3", "performance cancel/release", "remaining", "Assets/Scripts/Services/Captivity/CircusRuntime.cs"),
        Input("captivity.wildlife-care", "{penPersistentId}", "WildlifeCaptureRuntime", "same-cell inferred facility", "none", "Wildlife/Circus save", "feed/capture release", "remaining", "Assets/Scripts/Services/Captivity/WildlifeCaptureRuntime.cs"),
        Input("character.career", "{academyPersistentId}", "CareerApplicationAdapter", "same-cell inferred facility", "none", "career state", "recomputed order", "remaining", "Assets/Scripts/Services/Character/CareerApplicationAdapter.cs"),
        Input("character.reproduction", "{facilityPersistentId}", "ReproductionCommandRuntime", "same-cell inferred facility", "none", "reproduction persistence", "order cancellation", "remaining", "Assets/Scripts/Services/Character/ReproductionCommandRuntime.cs"),
        Input("work.construction", "construction:{buildingId}:{x}:{y}", "WorkAmountSystem", "ConstructionSite + WorkOrder authority", "none", "WorkOrder + ConstructionSite save", "construction cancellation restitution", "remaining", "Assets/Scripts/Services/Character/Work/WorkAmountSystem.cs"),
        Input("medical.character-supply", "facility-input:medical:{orderId}", "CharacterMedicalSupplyCoordinator", "same-cell inferred facility", "none", "medical order + outbox", "order cancellation", "remaining", "Assets/Scripts/Services/Combat/CharacterMedicalSupplyCoordinator.cs"),
        Input("combat.equipment-crafting", "facility-input:combat-craft:{sequence}", "CombatEquipmentCraftingRuntime", "same-cell inferred facility", "none", "Craft V7 + WIP", "order release", "remaining", "Assets/Scripts/Services/Combat/CombatEquipmentCraftingRuntime.cs"),
        Input("combat.equipment-evolution", "facility-reforge:{orderId}|facility-reattune:{orderId}", "EquipmentEvolutionRuntime", "same-cell inferred facility", "none", "equipment evolution orders", "order release", "remaining", "Assets/Scripts/Services/Combat/EquipmentEvolutionRuntime.cs"),
        Input("combat.equipment-maintenance", "equipment-repair:{equipmentInstanceId}", "EquipmentMaintenanceRuntime", "exact LiveFacility claim; exact-stack managed admission", "one repair batch: dynamic unique equipment plus exact material grams", "maintenance save + profile restore + WIP receipt join", "owner-wide terminal close", "migrated", "Assets/Scripts/Services/Combat/EquipmentMaintenanceRuntime.cs"),
        Input("combat.equipment-module", "{progressionFacilityId}", "EquipmentModuleRuntime", "same-cell inferred facility", "none", "module/equipment state", "direct return also bypasses admission", "remaining", "Assets/Scripts/Services/Combat/EquipmentModuleRuntime.cs"),
        Input("combat.defense-facility", "facility-input:defense:{facilityId}|facility-input:defense-maintenance:{facilityId}", "DefenseFacilityRuntime", "same-cell inferred facility", "none", "defense state + outbox", "order release", "remaining", "Assets/Scripts/Services/Defense/DefenseFacilityRuntime.cs"),
        Input("economy.certified-seed", "certified-seed|{facility}|{crop}|{sequence}", "CertifiedSeedRuntime", "same-cell inferred facility", "none", "CertifiedSeed V1 + crop receipt", "order release", "remaining", "Assets/Scripts/Services/Economy/CertifiedSeedRuntime.cs"),
        Input("economy.crop-plot", "crop-materials:{plotId}[:treatment]", "CropEcologyRuntime", "same-cell inferred facility", "none", "Crop Plot V5", "plot/order release", "remaining", "Assets/Scripts/Services/Economy/CropEcologyRuntime.cs"),
        Input("economy.stock-policy", "stock-policy:sell:{itemId}|sale:quality-rejected", "ResourceStockPolicyRuntime", "same-cell inferred dropoff", "none", "stock policy + sale outbox", "policy release", "remaining", "Assets/Scripts/Services/Economy/Planning/ResourceStockPolicyRuntime.cs"),
        Input("facility.evolution", "facility-evolution:{orderId}|facility-input:relocation:{orderId}", "FacilityInstanceEvolutionRuntime", "same-cell inferred facility", "none", "modification/recalibration/relocation save", "order release", "remaining", "Assets/Scripts/Services/FacilityEvolution/FacilityInstanceEvolutionRuntime.cs"),
        Input("research.blueprint-archive", "research-archive:{facilityId}|research:{facilityId}", "BlueprintResearchRuntime", "archive exact LiveFacility; research inferred", "none", "research save + claim restore", "legacy destination release", "remaining", "Assets/Scripts/Services/Infrastructure/BlueprintResearchRuntime.cs"),
        Input("infrastructure.climate", "{towerPersistentId}", "ClimateRuntime", "same-cell inferred facility", "none", "climate runtime", "recomputed order", "remaining", "Assets/Scripts/Services/Infrastructure/Environment/ClimateRuntime.cs"),
        Input("infrastructure.electrical", "power:{nodeId}", "ElectricalNetworkRuntime", "exact LiveBuilding claim; exact-stack managed admission", "common positive-gram profile", "AIHaul; carried 350g save/restore; consumption", "terminal close", "migrated", "Assets/Scripts/Services/Infrastructure/Industrial/ElectricalNetworkRuntime.cs"),
        Input("infrastructure.fluid", "plumbing:manual-water:{fixtureId}|plumbing:water-transfer:{nodeId}", "FluidNetworkRuntime", "manual-water LiveBuilding special; transfer inferred", "none", "Fluid V6", "transfer cancellation", "remaining", "Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs"),
        Input("infrastructure.process-fluid", "plumbing:process-water:{nodeId}:{workTypeId}", "ProcessFluidUseRuntime", "same-cell inferred facility", "none", "production/surgery WIP", "work cancellation", "remaining", "Assets/Scripts/Services/Infrastructure/Industrial/ProcessFluidUseRuntime.cs"),
        Input("research.knowledge-residue", "knowledge:{taskId}", "KnowledgeResidueProcessingRuntime", "same-cell inferred facility", "none", "knowledge task save", "task release", "remaining", "Assets/Scripts/Services/Infrastructure/KnowledgeResidueProcessingRuntime.cs"),
        Input("research.arcane-index", "{researchFacilityId}", "ResearchWorkExecutionAdapter", "same-cell inferred facility", "none", "research projection", "work cancellation", "remaining", "Assets/Scripts/Services/Infrastructure/ResearchWorkExecutionAdapter.cs"),
        Input("invasion.defense-kit", "{signalPostPersistentId}", "DefenseCombatExecutor", "same-cell inferred facility", "none", "defense runtime", "day/order cleanup", "remaining", "Assets/Scripts/Services/Invasion/DefenseCombatExecutor.cs"),
        Input("invasion.signal-horn", "{signalPostPersistentId}", "InvasionDirectorRuntime", "same-cell inferred facility", "none", "invasion restore", "event cleanup", "remaining", "Assets/Scripts/Services/Invasion/InvasionDirectorRuntime.cs"),
        Input("medical.surgery", "surgery-materials:{orderId}", "SurgeryLogisticsRuntime", "exact ReservedTarget/LiveFacility claim", "none", "Surgery V9 + receipt + claim restore", "legacy destination release", "remaining", "Assets/Scripts/Services/Medical/SurgeryLogisticsRuntime.cs"),
        Input("medical.surgical-part-storage", "{organStorageId}|surgery-organ-storage-fuel:{facilityId}", "SurgicalPartRuntime", "same-cell inferred facility", "none", "surgical part/storage state", "storage/fuel release", "remaining", "Assets/Scripts/Services/Medical/SurgicalPartRuntime.cs"),
        Input("offense.expedition-supply", "expedition:{packageId}", "OffensePreparationService", "exact ReservedTarget claim", "none", "expedition package + Physical Items", "legacy destination release", "remaining", "Assets/Scripts/Services/Offense/OffensePreparationService.cs"),
        Input("offense.urgent-mitigation", "threat-mitigation:{siteId}:{sequence}", "OffenseUrgentMitigationRuntime", "same-cell inferred facility", "none", "mitigation save", "order release", "remaining", "Assets/Scripts/Services/Offense/Strategic/OffenseUrgentMitigationRuntime.cs"),
        Input("run.v20-administrative-seal", "{officePersistentId}", "V20ContentResolutionService", "same-cell inferred facility", "none", "V20 runtime + physical stack", "destination rollback", "remaining", "Assets/Scripts/Services/Run/V20ContentResolutionService.cs"),
        Input("economy.waste-direct-feed", "{callerPenOrDestinationId}", "WasteProcessingRuntime", "same-cell inferred facility", "none", "waste policy + physical stack", "task release", "remaining", "Assets/Scripts/Models/Economy/Content/WasteProcessingRuntime.cs"),
        Input("infrastructure.manual-water-fallback", "plumbing:manual-water:{fixtureId}", "FluidNetworkRuntime", "explicit LiveBuilding resolution", "none", "Fluid V6", "delegated transfer cancellation", "remaining", "Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs"),

        Bypass("research.blueprint-materialization", "research-archive:{facilityId}", "BlueprintResearchRuntime.EnsureAcquiredBlueprintItemsMaterialized", "direct SpawnUniqueItemAt FacilityBuffer without gram admission", "Assets/Scripts/Services/Infrastructure/BlueprintResearchRuntime.cs"),
        Bypass("combat.module-return", "{progressionFacilityId}", "EquipmentModuleRuntime.TryMaterializeReturnedModule", "direct SpawnExistingUniqueItemAt FacilityBuffer without gram admission", "Assets/Scripts/Services/Combat/EquipmentModuleRuntime.cs"),
        Bypass("run.v20-grant", "{eventGrantDestinationId}", "V20ContentResolutionService.TrySpawnGrants", "direct TrySpawnItem FacilityBuffer without gram admission", "Assets/Scripts/Services/Run/V20ContentResolutionService.cs"),
        Bypass("exterior.merchant-cart", "{incidentId}", "ExteriorIncidentHandlers.TryBegin", "direct SpawnItemAt FacilityBuffer without gram admission", "Assets/Scripts/Services/Infrastructure/Exterior/ExteriorIncidentHandlers.cs"),
        Bypass("infrastructure.conveyor", "{callerDestinationId}", "ConveyorItemGateway.TryCompleteToFacility", "Transit to FacilityBuffer without destination admission token", "Assets/Scripts/Services/Infrastructure/Industrial/ConveyorItemGateway.cs"),

        Orphan("items.building-stack-port", "{callerDestinationId}", "IBuildingItemStackPort.SpawnFacilityBufferItem|SpawnExistingFacilityBufferUniqueItem", "implementation exists with zero production caller", "Assets/Scripts/Services/Items/BuildingItemStackPortAdapter.cs"),

        Output("economy.production-output", "production-output:{facilityId}", "ProductionOutputExecutionService|ProductionItemGateway", "count reservation only", "remaining", "Assets/Scripts/Models/Economy/Content/ProductionOutputExecutionService.cs"),
        Output("combat.craft-output", "production-output:{facilityId}|sale:quality-rejected", "CombatEquipmentCraftingRuntime", "exact publication, no gram reservation", "remaining", "Assets/Scripts/Services/Combat/CombatEquipmentCraftingRuntime.cs"),
        Output("environment.apparel-output", "production-output:{facilityId}", "ApparelWorkOrderRuntime", "direct unique publication, no gram reservation", "remaining", "Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs"),
        Output("environment.workwear-output", "production-output:{facilityId}", "EnvironmentalWorkwearProductionOutputHandler", "commit component, no gram reservation", "remaining", "Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalWorkwearProductionOutputHandler.cs"),
        Output("economy.certified-seed-output", "certified-seed-output|{facilityId}", "CertifiedSeedRuntime", "output commit, no gram reservation", "remaining", "Assets/Scripts/Services/Economy/CertifiedSeedRuntime.cs"),
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
        Require(callsites.Length == ExpectedDeliveryInvocationCount,
            $"Facility-buffer delivery invocation drift: expected={ExpectedDeliveryInvocationCount}; actual={callsites.Length}.");
        Require(callsites.Select(value => value.SourcePath)
                    .Distinct(StringComparer.Ordinal).Count()
                == ExpectedDeliveryInvocationFileCount,
            "Facility-buffer delivery invocation file-count drifted.");

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
        int orphan = sorted.Count(value => value.Disposition == "orphan");
        int outputOwners = sorted.Count(value => IsOutputState(value.State));
        int outputMigrated = sorted.Count(value => IsOutputState(value.State)
            && value.Disposition == "migrated");
        int outputRemaining = sorted.Count(value => IsOutputState(value.State)
            && value.Disposition == "remaining");
        Require(inputOwners == 39 && inputMigrated == 3 && inputRemaining == 36
                && bypass == 5 && orphan == 1 && outputOwners == 6
                && outputMigrated == 1 && outputRemaining == 5,
            $"Facility-buffer owner classification drift: input={inputOwners}; "
            + $"inputMigrated={inputMigrated}; inputRemaining={inputRemaining}; "
            + $"bypass={bypass}; orphan={orphan}; output={outputOwners}; "
            + $"outputMigrated={outputMigrated}; outputRemaining={outputRemaining}.");

        string[] sourcePaths = sorted.Select(value => value.SourcePath)
            .Concat(callsites.Select(value => value.SourcePath))
            .Append("Assets/Scripts/Services/Combat/EquipmentMaintenanceRuntimeServices.cs")
            .Append("Assets/Scripts/Services/Combat/EquipmentRepairMaterialRestoreGuard.cs")
            .Append("Assets/Scripts/Services/Combat/Editor/EquipmentRepairMaterialOutboxFixture.cs")
            .Append("Assets/Scripts/Services/Items/Editor/PhysicalItemLogisticsPlayModeVerifier.cs")
            .Append(SelfPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string sourceDigest = ComputeCanonicalSourceDigest(root, sourcePaths);
        byte[] csv = BuildCsv(sorted, sourceDigest);
        string summary = "V27 FacilityBuffer owner manifest passed: "
            + $"inputOwners={inputOwners}; outputOwners={outputOwners}; "
            + $"inputMigrated={inputMigrated}; inputRemaining={inputRemaining}; "
            + $"outputMigrated={outputMigrated}; outputRemaining={outputRemaining}; "
            + $"bypass={bypass}; "
            + $"orphan={orphan}; deliveryInvocations={callsites.Length}; "
            + "unclassified=0; deterministic capture=PASS.";
        byte[] report = Utf8(
            "schemaVersion=2\n"
            + "scope=FacilityBuffer,FacilityOutputBuffer,DirectLooseOutput\n"
            + "fullStoredDestinationCoverage=false\n"
            + $"sourceDigest={sourceDigest}\n"
            + $"inputOwners={inputOwners}\n"
            + $"outputOwners={outputOwners}\n"
            + $"inputMigrated={inputMigrated}\n"
            + $"inputRemaining={inputRemaining}\n"
            + $"outputMigrated={outputMigrated}\n"
            + $"outputRemaining={outputRemaining}\n"
            + $"remaining={inputRemaining + outputRemaining}\n"
            + $"bypass={bypass}\n"
            + $"orphan={orphan}\n"
            + $"deliveryInvocations={callsites.Length}\n"
            + $"deliveryInvocationFiles={ExpectedDeliveryInvocationFileCount}\n"
            + "unclassified=0\n"
            + "classificationGate=PASS\n"
            + "fullMigrationGate=OPEN\n");
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
        Require(DeliveryCallsiteOwners.Count == ExpectedDeliveryInvocationFileCount,
            "Facility-buffer callsite classification count drifted.");
        Require(Rows.Count(value => value.State == "FacilityBuffer"
                && value.Disposition is "migrated" or "remaining") == 39,
            "FacilityBuffer live owner registry must contain exactly 39 families.");
        Require(Rows.Count(value => value.ClaimAuthority.IndexOf(
                    "exact", StringComparison.Ordinal) >= 0) == 6,
            "FacilityBuffer exact-claim owner count drifted.");

        string production = Read(root,
            "Assets/Scripts/Services/Economy/ProductionInputLogisticsService.cs");
        string productionClaims = Read(root,
            "Assets/Scripts/Services/Economy/ProductionInputDestinationClaimRuntime.cs");
        Require(Count(production, "items.RequestDeliveryWithinMassCapacity(") == 0
                && Count(production, "items.RequestDelivery(") == 1
                && Count(productionClaims, "InputBufferCapacitySchemaRevision =") == 1
                && Count(productionClaims, "new PhysicalMassGrams(maxInputBufferMassGrams)") == 1,
            "Production input common gram profile/token migration drifted.");

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
                && equipmentRepairRestoreGuard.Contains(
                    "ValidateOwnerSet(maintenance.Orders, physicalCandidates);",
                    StringComparison.Ordinal)
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
                     "EquipmentRepairPrefix", "SurgeryMaterialsPrefix",
                     "ResearchArchivePrefix", "PowerFuelPrefix"
                 })
        {
            Require(Count(claimSource, "const string " + prefix + " =") == 1,
                "Exact claim prefix drifted: " + prefix);
        }

        Require(CountAllProductionSources(root, "SpawnFacilityBufferItem(") == 2
                && CountAllProductionSources(
                    root, "SpawnExistingFacilityBufferUniqueItem(") == 2,
            "Building item-stack port orphan status drifted; classify any new caller.");

        Require(Read(root, "Assets/Scripts/Services/Infrastructure/BlueprintResearchRuntime.cs")
                    .Contains("WorldItemStackState.FacilityBuffer", StringComparison.Ordinal)
                && Read(root, "Assets/Scripts/Services/Combat/EquipmentModuleRuntime.cs")
                    .Contains("SpawnExistingUniqueItemAt(", StringComparison.Ordinal)
                && Read(root, "Assets/Scripts/Services/Run/V20ContentResolutionService.cs")
                    .Contains("WorldItemStackState.FacilityBuffer", StringComparison.Ordinal)
                && Read(root, "Assets/Scripts/Services/Infrastructure/Exterior/ExteriorIncidentHandlers.cs")
                    .Contains("items.SpawnItemAt(", StringComparison.Ordinal)
                && Read(root, "Assets/Scripts/Services/Infrastructure/Industrial/ConveyorItemGateway.cs")
                    .Contains("WorldItemStackState.FacilityBuffer", StringComparison.Ordinal),
            "A declared direct FacilityBuffer bypass marker is missing.");
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
