#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;
using VContainer.Diagnostics;

public static class WorkAmountDebugScenarios
{
    public static void RunBdc01PlacementIdentityBatch()
    {
        List<string> unexpected = new();
        void OnLog(string condition, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                unexpected.Add(condition);
        }
        JObject report = new()
        {
            ["passed"] = false,
            ["fullAuditClosure"] = false,
            ["trainingEligible"] = false,
            ["humanApprovalClaimed"] = false,
            ["scope"] = "Controlled editor fixture: real grid placement, construction runtime and ledger; bounded material delivery; fresh work-order runtime and reconstructed facility candidate, not AI or whole-world restore."
        };
        Application.logMessageReceived += OnLog;
        try
        {
            ConstructionSupportRollbackB0DebugScenarios.RunOrThrow();
            report["rollbackBranches"] = new JArray(
                "existing-hallway-preserved", "partial-support-failure",
                "site-rejection", "order-rejection", "success-keeps-support");
            JArray branches = new();
            JArray identityValidation = new();
            report["identityBranches"] = branches;
            report["identityValidation"] = identityValidation;
            branches.Add(VerifyBdc01PlacementIdentity(false, false, identityValidation));
            branches.Add(VerifyBdc01PlacementIdentity(true, false, identityValidation));
            branches.Add(VerifyBdc01PlacementIdentity(false, true, identityValidation));
            branches.Add(VerifyBdc01PlacementIdentity(false, false, identityValidation, retiredWorld: true));
            BdcRequire(identityValidation.All(value => value["rejected"].Value<bool>()),
                "Malformed BDC-B01 identity accepted: " + string.Join(", ",
                    identityValidation.Where(value => !value["rejected"].Value<bool>())
                        .Select(value => value["case"].Value<string>())));
            BdcRequire(unexpected.Count == 0,
                "Unexpected BDC-B01 errors: " + string.Join(" | ", unexpected));
            report["passed"] = true;
            Debug.Log("BDC_B01_REAL_PLACEMENT_IDENTITY_PASS");
        }
        catch (Exception error)
        {
            report["failure"] = error.ToString();
            Debug.LogException(error);
            throw;
        }
        finally
        {
            Application.logMessageReceived -= OnLog;
            report["unexpectedErrors"] = new JArray(unexpected);
            string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
            if (!string.IsNullOrWhiteSpace(output))
            {
                Directory.CreateDirectory(output);
                using FileStream file = new(Path.Combine(output, "report.json"),
                    FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                using StreamWriter writer = new(file);
                writer.Write(report.ToString());
            }
        }
    }

    private static JObject VerifyBdc01PlacementIdentity(
        bool cancel, bool replaceWall, JArray identityValidation, bool retiredWorld = false)
    {
        string branch = retiredWorld ? "complete-retired-world"
            : cancel ? "cancel" : replaceWall ? "replace-wall" : "complete";
        BuildingSO hallway = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Hallway.asset");
        BuildingSO target = AssetDatabase.LoadAssetAtPath<BuildingSO>(replaceWall
            ? "Assets/Resources/SO/Building/InteriorDoor.asset"
            : "Assets/Resources/SO/Building/Modular/R01_간이침대.asset");
        BuildingSO wall = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Wall.asset");
        BdcRequire(hallway != null && target != null && wall != null,
            "BDC-B01 authored assets are unavailable.");
        List<BuildableObject> objects = new();
        Grid grid = BdcGrid();
        Vector2Int position = new(3, 0);
        IReadOnlyList<Vector2Int> footprint = target.GetGridPosList(position);
        FakeWorldItemStackRuntime items = new();
        ConstructionInputOwnerFixture inputs = new(items, objects);
        MigratedProducerOutcomeEditorFixture outcomes = new("run:bdc-b01:" + branch);
        MigratedProducerOutcomeProbeTransaction probe = new(outcomes.Transaction, outcomes.Recorder)
        {
            TargetKind = MigratedProducerOutcomeKind.WorkOrderTerminalResult,
            FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit
        };
        using DomainFactDispatcher constructionFacts =
            outcomes.CreateConstructionWorkFacts(probe);
        WorkOrderRuntime runtime = new(new FixedGridProvider(grid), items,
            new SingleBuildingLookup(target),
            new ScenarioObjectResolver(probe, constructionFacts),
            CreateExecutionServices(new TrackingWorkforceReplanService()),
            CreateStateStore(), new FakePhysicalItemSourcePublicationService(items), inputs.Runtime);
        void InjectAndTrack(BuildableObject value)
        {
            CharacterAiEditorTestDependencies.Inject(value);
            objects.Add(value);
        }
        GridBuildingFactory factory = new(InjectAndTrack);
        GridBuildingPlacementService placement = new(grid, hallway, _ => target,
            factory, BdcPlacementValidator(), runtime, InjectAndTrack);
        GridBuildingPlacementService immediate = new(grid, hallway, _ => target,
            factory, BdcPlacementValidator(), null);
        try
        {
            // Keep one preexisting support in the multi-cell branches. The door
            // branch must create its own support under the existing wall.
            string existingSupport = string.Empty;
            if (!replaceWall)
            {
                BdcRequire(immediate.TryPlaceBuildingImmediate(hallway, footprint[0], out _),
                    "Preexisting support could not be placed.");
                existingSupport = ((BuildableObject)grid.GetGridCell(footprint[0])
                    .GetOccupant(GridLayer.Hallway)).PersistentInstanceId.Value;
            }
            string replacedId = string.Empty;
            if (replaceWall)
            {
                BdcRequire(wall.IsStructuralWall && target.IsInteriorDoor
                    && immediate.TryPlaceBuildingImmediate(wall, position, out _),
                    "Door fixture requires a real structural wall.");
                replacedId = ((BuildableObject)grid.GetGridCell(position)
                    .GetOccupant(GridLayer.Building)).PersistentInstanceId.Value;
            }
            BdcRequire(placement.TryPlaceConstructionSite(target, position, out string failure),
                "Actual construction placement failed: " + failure);
            ConstructionSite site = grid.GetGridCell(position)
                .GetOccupant(GridLayer.Construction) as ConstructionSite;
            BdcRequire(site != null, "Actual site is missing.");
            WorkOrderSaveData initial = runtime.Capture().orders.Single();
            string[] supports = footprint.Select(cell => (BuildableObject)grid.GetGridCell(cell)
                    .GetOccupant(GridLayer.Hallway))
                .Select(value => value.PersistentInstanceId.Value)
                .Where(id => id != existingSupport).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            BdcRequire(supports.Length > 0 && initial.supportBuildingIds.SequenceEqual(supports)
                && initial.placementOperationId == "construction-placement:" + initial.workOrderId,
                "Actual support IDs were not attached, or preexisting support was included.");
            BdcRequire(runtime.TryAttachConstructionPlacement(initial.workOrderId,
                    initial.placementOperationId, supports.Reverse().ToArray(), out _)
                && !runtime.TryAttachConstructionPlacement(initial.workOrderId,
                    initial.placementOperationId + ":conflict", supports, out _),
                "Placement attachment replay/conflict contract failed.");
            foreach (WorkOrderItemMaterialSaveData material in initial.itemMaterials)
                items.AddFacilityItemBuffer(initial.materialDestinationId, material.itemId, material.required);
            BdcRequire(runtime.RefreshMaterialsReady(site), "Controlled material delivery was not accepted.");
            bool applied = false;
            bool completed = false;
            bool accepted = cancel
                ? runtime.CancelOrder(initial.workOrderId, refundDeliveredMaterials: true)
                : runtime.ApplyWork(null, site, BuiltInWorkTypeIds.Construct,
                    initial.requiredWork, out completed, out applied, out failure);
            BdcRequire(!accepted && !completed && (cancel || applied)
                && probe.CommitAttemptCount(MigratedProducerOutcomeKind.WorkOrderTerminalResult) == 1
                && outcomes.Ledger.GetGlobal(OutcomeCursor.FirstPage(8), OutcomeFilter.All).Items.Count == 0,
                "Terminal rejection did not leave real applied effects pending: " + failure);
            DungeonWorkOrderSaveData snapshot = JsonUtility.FromJson<DungeonWorkOrderSaveData>(
                JsonUtility.ToJson(runtime.Capture()));
            runtime.ValidateRestorePayload(snapshot);
            WorkOrderSaveData pending = snapshot.orders.Single();
            BuildableObject final = grid.GetGridCell(position).GetOccupant(GridLayer.Building) as BuildableObject;
            BdcRequire(pending.placementOperationId == initial.placementOperationId
                && pending.supportBuildingIds.SequenceEqual(supports)
                && pending.terminalPhase == WorkOrderTerminalPhase.EffectsAppliedPendingOutcome
                && pending.terminalKind == (cancel ? WorkOrderTerminalKind.Cancelled : WorkOrderTerminalKind.Completed)
                && pending.terminalFactCaptured
                && pending.terminalFact != null
                && pending.terminalFact.Matches(pending)
                && string.IsNullOrEmpty(pending.terminalPublicationKey)
                && (cancel ? final == null && pending.finalFacilityId.Length == 0
                    : final != null && pending.finalFacilityId == final.PersistentInstanceId.Value
                        && pending.finalFacilityDefinitionId == BdcDefinition(target))
                && pending.replacedFacilityId == replacedId
                && pending.replacedFacilityDefinitionId == (replaceWall ? BdcDefinition(wall) : string.Empty),
                "Real terminal receipt did not survive JSON with exact final/replaced/support identity.");
            BdcProbeInvalidSavedIdentities(runtime, snapshot, initial, branch, identityValidation);

            // Reconstruct only the facility candidate using real factories and
            // persisted IDs, then publish through the actual work restore API.
            Grid restoredGrid = BdcGrid();
            List<BuildableObject> restoredBuildings = new();
            foreach (BuildableObject original in retiredWorld ? Array.Empty<BuildableObject>() : objects.ToArray())
            {
                if (original == null || original is ConstructionSite
                    || !ReferenceEquals(grid.GetGridCell(original.centerPos)
                        .GetOccupant(original.BuildingData.Placement.Layer), original)) continue;
                BuildableObject copy = factory.Create(restoredGrid, original.BuildingData, original.centerPos);
                copy.RestorePersistentIdentity(original.PersistentInstanceId);
                copy.SetGrid(restoredGrid);
                copy.Initialization(original.BuildingData, original.centerPos);
                BdcRequire(restoredGrid.RegisterOccupant(copy, copy.BuildingData.Placement.Layer,
                    copy.buildPoses, copy.BuildingData.Placement.IsMovement), "Facility candidate registration failed.");
                restoredBuildings.Add(copy);
            }
            MigratedProducerOutcomeEditorFixture restoredOutcomes = new("run:bdc-b01:" + branch);
            using DomainFactDispatcher restoredConstructionFacts =
                restoredOutcomes.CreateConstructionWorkFacts();
            RestoreWorldCandidateIndex candidateIndex = new();
            ConstructionInputOwnerFixture restoredInputs = new(items, restoredBuildings);
            WorkOrderRuntime restored = new(new FixedGridProvider(restoredGrid), items,
                new SingleBuildingLookup(target),
                new ScenarioObjectResolver(
                    restoredOutcomes.Transaction,
                    restoredConstructionFacts),
                CreateExecutionServices(new TrackingWorkforceReplanService()),
                new WorkOrderAggregateStateStore(new DungeonRuntimeAggregateRootStore(), candidateIndex),
                new FakePhysicalItemSourcePublicationService(items), restoredInputs.Runtime);
            PublishWorkOrderSnapshot(restored, candidateIndex, restoredGrid, restoredBuildings, snapshot);
            ConstructionSite restoredSite = restoredGrid.GetGridCell(position)
                .GetOccupant(GridLayer.Construction) as ConstructionSite;
            if (restoredSite != null) objects.Add(restoredSite);
            BdcRequire(JsonUtility.ToJson(restored.Capture()) == JsonUtility.ToJson(snapshot),
                "Fresh work-order runtime changed pending identity during publication.");
            BuildableObject restoredFinal = restoredGrid.GetGridCell(position)
                .GetOccupant(GridLayer.Building) as BuildableObject;
            int materialCommits = items.PhysicalCommitCount;
            int released = items.ReleasedQuantity;
            restored.Tick();
            var rows = restoredOutcomes.Ledger.GetGlobal(OutcomeCursor.FirstPage(8), OutcomeFilter.All).Items;
            BdcRequire(rows.Count == 1 && restored.Capture().orders.Count == 0
                && !restoredGrid.GetGridCell(position).HasOccupantInLayer(GridLayer.Construction)
                && ReferenceEquals(restoredGrid.GetGridCell(position).GetOccupant(GridLayer.Building), restoredFinal)
                && items.PhysicalCommitCount == materialCommits && items.ReleasedQuantity == released,
                "Fresh restore did not publish exactly one parent without repeating placement/materials.");
            var exact = rows.Single().Exact;
            string Fact(string id) => exact.facts.Single(value => value.factId == id).value;
            BdcRequire(Fact(MigratedProducerOutcomeIds.PlacementOperationFact.Value) == pending.placementOperationId
                && Fact(MigratedProducerOutcomeIds.SupportFacilitiesFact.Value) == string.Join(",", supports)
                && Fact(MigratedProducerOutcomeIds.FinalFacilityFact.Value) == (cancel ? "none" : pending.finalFacilityId)
                && Fact(MigratedProducerOutcomeIds.ReplacedFacilityFact.Value) == (replaceWall ? replacedId : "none")
                && Fact(MigratedProducerOutcomeIds.TerminalKindFact.Value) == (cancel ? "cancelled" : "completed"),
                "Terminal ledger identity facts differ from the actual saved placement.");
            BdcRequire(footprint.All(cell =>
                    restoredGrid.GetGridCell(cell).HasOccupantInLayer(GridLayer.Hallway) != retiredWorld),
                "Resume changed the incoming support presence/retirement state.");
            restored.Tick();
            BdcRequire(!restored.CancelOrder(initial.workOrderId, true)
                && !restored.DebugCompleteOrder(initial.workOrderId, out _)
                && restoredOutcomes.Ledger.GetGlobal(OutcomeCursor.FirstPage(8), OutcomeFilter.All).Items.Count == 1,
                "Terminal replay duplicated a result.");
            return new JObject
            {
                ["branch"] = branch, ["passed"] = true,
                ["pending"] = JObject.Parse(JsonUtility.ToJson(pending)),
                ["preexistingSupportExcluded"] = existingSupport,
                ["freshRuntimeRestored"] = true, ["parentCount"] = rows.Count,
                ["historicalFacilitiesAbsentInCandidate"] = retiredWorld,
                ["materialsUnchangedOnResume"] = true, ["finalInstanceUnchangedOnResume"] = true,
                ["ledger"] = JObject.FromObject(exact)
            };
        }
        finally
        {
            foreach (BuildableObject value in objects.Distinct().Reverse())
                if (value != null) UnityEngine.Object.DestroyImmediate(value.gameObject);
        }
    }

    private static void BdcRequire(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void BdcProbeInvalidSavedIdentities(
        WorkOrderRuntime runtime, DungeonWorkOrderSaveData snapshot,
        WorkOrderSaveData initial, string branch, JArray observations)
    {
        string liveBefore = JsonUtility.ToJson(runtime.Capture());
        void Probe(string name, Action<WorkOrderSaveData> mutate, bool active = false)
        {
            DungeonWorkOrderSaveData malformed = JsonUtility.FromJson<DungeonWorkOrderSaveData>(
                JsonUtility.ToJson(snapshot));
            if (active)
                malformed.orders[0] = JsonUtility.FromJson<WorkOrderSaveData>(JsonUtility.ToJson(initial));
            mutate(malformed.orders[0]);
            bool rejected = false;
            string failure = string.Empty;
            try { runtime.ValidateRestorePayload(malformed); }
            catch (InvalidOperationException error) { rejected = true; failure = error.Message; }
            BdcRequire(JsonUtility.ToJson(runtime.Capture()) == liveBefore,
                "Identity preflight changed the live order.");
            observations.Add(new JObject
            {
                ["case"] = branch + ":" + name, ["rejected"] = rejected,
                ["liveUnchanged"] = true, ["failure"] = failure
            });
        }
        Probe("foreign-order-operation", value =>
            value.placementOperationId = "construction-placement:work:999999");
        Probe("noncanonical-support", value =>
            value.supportBuildingIds[0] = " " + value.supportBuildingIds[0]);
        Probe("supports-without-operation", value => value.placementOperationId = string.Empty, active: true);
        if (snapshot.orders[0].terminalKind == WorkOrderTerminalKind.Completed)
        {
            Probe("wrong-final-definition", value => value.finalFacilityDefinitionId = "building:999999");
            Probe("final-is-site", value => value.finalFacilityId = value.constructionSitePersistentId);
            Probe("final-is-support", value => value.finalFacilityId = value.supportBuildingIds[0]);
            if (snapshot.orders[0].replacedFacilityId.Length > 0)
                Probe("replaced-is-final", value => value.replacedFacilityId = value.finalFacilityId);
        }
        BuildingPlacementCommitReceipt partial = new(
            "building:test:bdc-final", "building:1020",
            "building:test:bdc-replaced", string.Empty);
        observations.Add(new JObject
        {
            ["case"] = branch + ":half-replaced-receipt", ["rejected"] = !partial.IsValid,
            ["liveUnchanged"] = true
        });
    }

    private static string BdcDefinition(BuildingSO value) =>
        string.IsNullOrWhiteSpace(value.ContentDefinitionId) ? "building:" + value.id : value.ContentDefinitionId;

    private static Grid BdcGrid()
    {
        Grid grid = new(8, 1);
        for (int x = 0; x < grid.width; x++)
            grid.SetAreaType(new Vector2Int(x, 0), GridCellAreaType.DungeonInterior);
        return grid;
    }

    private static BuildingPlacementValidator BdcPlacementValidator() => new(
        new GridPlacementValidator(), () => new BuildingConditionContext(
            null, null, null, new BdcPlacementRules()));

    private sealed class BdcPlacementRules : IDungeonDebugRuleQuery
    {
        public bool IsExecutingCommand => false;
        public bool IsEnabled(DungeonDebugCheat cheat) =>
            cheat == DungeonDebugCheat.IgnorePlacementRules || cheat == DungeonDebugCheat.FreeConstruction;
        public bool ShouldFreezeNeed(CharacterCondition condition, float delta) => false;
        public bool ShouldBlockFriendlyDamage(CharacterActor actor) => false;
        public bool ShouldBlockFacilityDamage(bool damaged) => false;
        public bool ShouldSkipCosts() => true;
    }

    [MenuItem("DungeonStory/Debug/Work/Run Work Amount Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("Work amount scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        RunScenario("save V18 carries work orders", VerifySaveV18CarriesWorkOrders, errors);
        RunScenario(
            "authored construction material projection",
            VerifyConfiguredWorkAmountFallback,
            errors);
        RunScenario(
            "all construction materials are concrete catalog items",
            VerifyAuthoredConstructionMaterialCatalog,
            errors);
        RunScenario("construction order lifecycle", VerifyConstructionOrderLifecycle, errors);
        RunScenario(
            "construction terminal outcome retry and save round trip",
            VerifyConstructionTerminalOutcomeRetry,
            errors);
        RunScenario(
            "repair outcome retry and save round trip",
            VerifyRepairOutcomeRetryAndSaveRoundTrip,
            errors);
        RunScenario(
            "repair outcome kind and material coverage",
            VerifyRepairOutcomeKindAndMaterialCoverage,
            errors);
        RunScenario(
            "construction site quantity-aware worker capacity",
            VerifyConstructionSiteParallelWorkerCapacity,
            errors);
        RunScenario(
            "purchased facility kit delivery",
            VerifyPurchasedFacilityKitDelivery,
            errors);
        RunScenario("construction cancellation refunds materials", VerifyConstructionCancellationRefund, errors);
        RunScenario(
            "construction restitution output restore preflight",
            VerifyRestitutionOutputRestorePreflight,
            errors);
        RunScenario("orphan construction auto-recovers materials", VerifyOrphanConstructionRecovery, errors);
        RunScenario(
            "work-order preflight preserves live state",
            VerifyInvalidRestorePreflightPreservesLiveState,
            errors);
        RunScenario(
            "work-order registry publishes detached construction site",
            VerifyRegistryPublishesDetachedConstructionSite,
            errors);
        RunScenario(
            "work-order late participant failure restores live construction site",
            VerifyLateParticipantFailureRestoresLiveConstructionSite,
            errors);
        RunScenario(
            "deferred dismantle cancellation preserves salvage across restore",
            VerifyDeferredDismantleCancellationRecovery,
            errors);
        RunScenario(
            "destructive-drain work-order save contract",
            VerifyDestructiveDrainSaveContract,
            errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("Work amount scenarios passed.");
        }

        return true;
    }

    public static void RunRepairObserversForBatchMode()
    {
        if (!VerifyRepairOutcomeRetryAndSaveRoundTrip())
            throw new InvalidOperationException(
                "Repair outcome retry and save round trip failed.");
        if (!VerifyRepairOutcomeKindAndMaterialCoverage())
            throw new InvalidOperationException(
                "Repair outcome kind and material coverage failed.");
        Debug.Log("REPAIR_WORK_OBSERVERS_FOCUSED_PASS");
    }

    public static void RunConstructionObserversForBatchMode()
    {
        if (!VerifyConstructionOrderLifecycle())
            throw new InvalidOperationException(
                "Construction kind/material observer scenario failed.");
        if (!VerifyConstructionTerminalOutcomeRetry())
            throw new InvalidOperationException(
                "Construction terminal save/retry observer scenario failed.");

        JArray identityValidation = new();
        JObject[] branches =
        {
            VerifyBdc01PlacementIdentity(
                cancel: false,
                replaceWall: false,
                identityValidation: identityValidation),
            VerifyBdc01PlacementIdentity(
                cancel: true,
                replaceWall: false,
                identityValidation: identityValidation),
            VerifyBdc01PlacementIdentity(
                cancel: false,
                replaceWall: true,
                identityValidation: identityValidation),
            VerifyBdc01PlacementIdentity(
                cancel: false,
                replaceWall: false,
                identityValidation: identityValidation,
                retiredWorld: true)
        };
        BdcRequire(
            branches.All(value => value?["passed"]?.Value<bool>() == true),
            "A focused BDC construction branch failed.");
        BdcRequire(
            identityValidation.All(value => value["rejected"].Value<bool>()),
            "A focused BDC construction identity corruption was accepted.");
        Debug.Log("CONSTRUCTION_WORK_OBSERVERS_FOCUSED_PASS");
    }

    public static JObject RunW02Focused()
    {
        List<string> errors = new List<string>();
        RunScenario(
            "construction contributors, materials, and placement identity",
            VerifyConstructionOrderLifecycle,
            errors);
        RunScenario(
            "construction cancellation restitution survives parent rejection",
            VerifyConstructionCancellationRefund,
            errors);
        RunScenario(
            "construction placement authority rejection and retry",
            VerifyConstructionTerminalOutcomeRetry,
            errors);
        RunScenario(
            "deferred dismantle world removal survives save/cancel recovery",
            VerifyDeferredDismantleCancellationRecovery,
            errors);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "W02 focused work-order scenarios failed: "
                + string.Join(", ", errors));
        }

        return new JObject
        {
            ["passed"] = true,
            ["branches"] = new JArray
            {
                W02Branch(
                    "multiple-workers-split-contribution",
                    "two persistent workers contribute 2 and 3 work units; the canonical terminal result preserves both exact identities"),
                W02Branch(
                    "material-restitution-parent-rejection",
                    "material restitution publishes once before the terminal parent rejects; JSON round trip and retry do not duplicate it"),
                W02Branch(
                    "placement-authority-rejection",
                    "construction placement applies once, terminal authority rejection remains pending, and retry commits once"),
                W02Branch(
                    "deferred-world-removal-save-recovery",
                    "real dismantle order retains its destructive-drain owner across save/cancel recovery and removes the world object before exact salvage cleanup")
            }
        };
    }

    private static JObject W02Branch(string id, string assertion) => new()
    {
        ["id"] = id,
        ["passed"] = true,
        ["assertion"] = assertion
    };

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (scenario()) return;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        errors.Add(name);
    }

    private static bool TryValidatePhysicalCandidate(
        DungeonWorkOrderSaveData payload,
        IPhysicalItemRestoreCandidateQuery query)
    {
        try
        {
            WorkOrdersSaveSection.ValidatePhysicalRestoreCandidate(
                payload,
                query);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool VerifySaveV18CarriesWorkOrders()
    {
        DungeonGameSaveData save = new DungeonGameSaveData();
        DungeonSaveSectionPayload.Write(
            save,
            WorkOrdersSaveSection.Id,
            DungeonWorkOrderSaveData.CurrentVersion,
            DungeonSaveRestorePhase.RuntimeState,
            new DungeonWorkOrderSaveData());
        DungeonWorkOrderSaveData workOrders =
            DungeonSaveSectionPayload.ReadOrNew<DungeonWorkOrderSaveData>(
                save,
                WorkOrdersSaveSection.Id);
        return save.version == DungeonGameSaveData.CurrentVersion
            && workOrders.version == DungeonWorkOrderSaveData.CurrentVersion;
    }

    private static bool VerifyConfiguredWorkAmountFallback()
    {
        BuildingSO configured = CreateTestBuilding(91001, "작업량 테스트 시설", 2, 1, 12f, 4);
        BuildingSO fallback = CreateTestBuilding(91002, "기본 작업량 테스트 시설", 3, 1, 0f, 0, addWorkAbility: false);
        try
        {
            bool configuredValid = Mathf.Approximately(
                    configured.GetRequiredWork(BuiltInWorkTypeIds.Construct),
                    12f)
                && Mathf.Approximately(configured.GetRequiredWork(BuiltInWorkTypeIds.Research), 6f)
                && configured.GetConstructionMaterials().Count == 1
                && configured.GetConstructionMaterials()[0].ItemId == "material:lumber"
                && configured.GetConstructionMaterials()[0].Amount == 4;

            bool fallbackValid = fallback.GetRequiredWork(BuiltInWorkTypeIds.Construct) > 0f
                && fallback.GetRequiredWork(BuiltInWorkTypeIds.Repair) > 0f
                && fallback.GetConstructionMaterials().Count == 0;
            return configuredValid && fallbackValid;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(configured);
            UnityEngine.Object.DestroyImmediate(fallback);
        }
    }

    private static bool VerifyAuthoredConstructionMaterialCatalog()
    {
        GameContentCatalogSO root = AssetDatabase.LoadAssetAtPath<GameContentCatalogSO>(
            "Assets/Resources/SO/GameContentCatalog.asset");
        ItemDefinitionCatalogSO itemDefinitions =
            root?.GetItemDefinitions<ItemDefinitionCatalogSO>();
        if (itemDefinitions == null)
        {
            return false;
        }

        ResourceItemDefinitionCatalog catalog =
            new ResourceItemDefinitionCatalog(itemDefinitions.Definitions);
        GameDomainContentCatalogSO domainCatalog = root.DomainCatalogs
            .OfType<GameDomainContentCatalogSO>()
            .SingleOrDefault();
        BuildingSO[] authoredBuildings = domainCatalog?.GetAll<BuildingSO>()
            .Where(building => building != null)
            .Distinct()
            .ToArray();
        if (authoredBuildings == null || authoredBuildings.Length == 0)
        {
            return false;
        }

        foreach (BuildingSO building in authoredBuildings)
        {
            BuildingWorkAmountAbility ability =
                building.GetAbility<BuildingWorkAmountAbility>();
            if (ability == null || ability.ConstructionMaterials.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Cataloged building '{building.id}' at "
                    + $"'{AssetDatabase.GetAssetPath(building)}' has no concrete construction materials.");
            }

            ability.ValidateConstructionMaterialsOrThrow(itemId =>
                catalog.TryGet((ItemDefinitionId)itemId, out _));
        }

        if (typeof(BuildingWorkAmountAbility).GetField(
                "constructionMaterialCategory") != null
            || typeof(BuildingWorkAmountAbility).GetField(
                "constructionMaterialAmount") != null
            || typeof(BuildingWorkAmountAbility).GetField(
                "materialUnitsPerConstructionCost") != null)
        {
            return false;
        }

        return RejectsConstructionMaterials(new[]
            {
                new ItemAmountDefinition("material:lumber", 1),
                new ItemAmountDefinition("material:lumber", 2)
            })
            && RejectsConstructionMaterials(Array.Empty<ItemAmountDefinition>())
            && RejectsConstructionMaterials(new[]
            {
                new ItemAmountDefinition("stock-item:general", 1)
            })
            && RejectsConstructionMaterials(new[]
            {
                new ItemAmountDefinition(string.Empty, 1)
            });
    }

    private static bool RejectsConstructionMaterials(
        IEnumerable<ItemAmountDefinition> materials)
    {
        try
        {
            BuildingWorkAmountAbility ability = new BuildingWorkAmountAbility();
            ability.SetConstructionMaterials(materials);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool VerifyConstructionOrderLifecycle()
    {
        BuildingSO building = CreateTestBuilding(91003, "공사 주문 테스트 시설", 2, 1, 5f, 2);
        GameObject siteObject = new GameObject("WorkAmountConstructionSite");
        ConstructionSite site = siteObject.AddComponent<ConstructionSite>();
        site.RestorePersistentIdentity(
            (BuildingInstanceId)"building:test:work-amount-construction");
        CharacterAiEditorTestDependencies.Inject(site);
        GameObject firstWorkerObject = new GameObject(
            "WorkAmountConstructionWorkerOne");
        CharacterActor firstWorker =
            firstWorkerObject.AddComponent<CharacterActor>();
        CharacterAiEditorTestDependencies.Inject(firstWorkerObject);
        firstWorker.EnsureRuntimeState();
        firstWorker.Identity.SetPersistentId(
            (CharacterId)"character:test:construction-worker-one");
        GameObject secondWorkerObject = new GameObject(
            "WorkAmountConstructionWorkerTwo");
        CharacterActor secondWorker =
            secondWorkerObject.AddComponent<CharacterActor>();
        CharacterAiEditorTestDependencies.Inject(secondWorkerObject);
        secondWorker.EnsureRuntimeState();
        secondWorker.Identity.SetPersistentId(
            (CharacterId)"character:test:construction-worker-two");
        FakeWorldItemStackRuntime itemRuntime = new FakeWorldItemStackRuntime();
        ConstructionInputOwnerFixture constructionInputs = new(
            itemRuntime,
            site);
        MigratedProducerOutcomeEditorFixture outcomeFixture = new(
            "run:work-order-terminal-lifecycle");
        using DomainFactDispatcher constructionFacts =
            outcomeFixture.CreateConstructionWorkFacts();
        TrackingWorkforceReplanService workforceReplan = new TrackingWorkforceReplanService();
        WorkOrderRuntime runtime = new WorkOrderRuntime(
            new NoGridProvider(),
            itemRuntime,
            new SingleBuildingLookup(building),
            new ScenarioObjectResolver(
                outcomeFixture.Transaction,
                CharacterAiEditorTestDependencies.NeutralPerformance,
                constructionFacts),
            CreateExecutionServices(workforceReplan),
            CreateStateStore(),
            new FakePhysicalItemSourcePublicationService(itemRuntime),
            constructionInputs.Runtime);
        bool placed = false;
        bool removed = false;
        try
        {
            site.Initialization(building, new Vector2Int(3, 0));
            bool created = runtime.TryCreateConstructionOrder(
                site,
                building,
                site.centerPos,
                out string orderId,
                out string failureReason);
            if (!created)
            {
                Debug.LogError($"Could not create construction order: {failureReason}");
                return false;
            }

            if (!runtime.TryAttachConstructionPlacement(
                    orderId,
                    "construction-placement:" + orderId,
                    new[] { "building:test:construction-support" },
                    out _))
            {
                return false;
            }

            site.ConfigureSite(
                orderId,
                () =>
                {
                    placed = true;
                    return new BuildingPlacementCommitReceipt(
                        "building:test:completed-work-amount-facility",
                        "building:91003",
                        string.Empty,
                        string.Empty);
                },
                () => removed = true);

            bool waiting = runtime.TryGetOrderFor(
                    site,
                    BuiltInWorkTypeIds.Construct,
                    out WorkOrderProgressState order)
                && order.Status == WorkOrderStatus.WaitingForMaterials
                && itemRuntime.RequestedItems.TryGetValue("material:lumber", out int requested)
                && requested == 2
                && itemRuntime.PrioritizedStackIds.Count == 1
                && workforceReplan.HaulReplans == 1;
            if (!waiting)
            {
                return false;
            }

            if (runtime.RefreshMaterialsReady(site))
            {
                return false;
            }

            itemRuntime.AddFacilityItemBuffer(
                order.MaterialDestinationId,
                "material:lumber",
                2);
            itemRuntime.FailNextAcknowledgement = true;
            bool firstAcknowledgementFailed = !runtime.RefreshMaterialsReady(site);
            DungeonWorkOrderSaveData pendingSave = runtime.Capture();
            runtime.ValidateRestorePayload(pendingSave);
            WorkOrderMaterialTransferSaveData pendingOwner =
                pendingSave.orders.Single().materialTransfer;
            PhysicalItemRestoreCandidateDispositionSnapshot pendingReceipt = new(
                PhysicalItemDispositionKind.Transfer,
                pendingOwner.operationId,
                pendingOwner.reasonCode,
                pendingOwner.requestFingerprint,
                pendingOwner.sources.Select(value => value.stackId)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                pendingOwner.inputQuantity,
                pendingOwner.inputMassGrams,
                pendingOwner.commitId);
            bool restoreJoinPassed = TryValidatePhysicalCandidate(
                    pendingSave,
                    new CandidateQuery(pendingReceipt))
                && !TryValidatePhysicalCandidate(
                    pendingSave,
                    CandidateQuery.Empty)
                && !TryValidatePhysicalCandidate(
                    new DungeonWorkOrderSaveData(),
                    new CandidateQuery(pendingReceipt));
            bool ready = firstAcknowledgementFailed
                && pendingOwner.phase ==
                    WorkOrderMaterialTransferPhase.CustodyPublished
                && itemRuntime.PhysicalCommitCount == 1
                && itemRuntime.PhysicalAcknowledgementCount == 0
                && restoreJoinPassed
                && runtime.RefreshMaterialsReady(site)
                && runtime.TryGetOrderFor(site, BuiltInWorkTypeIds.Construct, out order)
                && order.Status == WorkOrderStatus.Ready
                && order.DeliveredItemMaterials.TryGetValue("material:lumber", out int delivered)
                && delivered == 2
                && itemRuntime.PhysicalCommitCount == 1
                && itemRuntime.PhysicalAcknowledgementCount == 1
                && runtime.Capture().orders.Single().materialTransfer.phase ==
                    WorkOrderMaterialTransferPhase.Acknowledged;
            if (!ready)
            {
                return false;
            }

            bool firstWork = runtime.ApplyWork(
                    firstWorker,
                    site,
                    BuiltInWorkTypeIds.Construct,
                    2f,
                    out bool completed,
                    out bool appliedEffects,
                    out _)
                && !completed
                && !appliedEffects
                && runtime.TryGetOrderFor(site, BuiltInWorkTypeIds.Construct, out order)
                && Mathf.Approximately(order.CompletedWork, 2f)
                && order.Status == WorkOrderStatus.InProgress;
            if (!firstWork)
            {
                return false;
            }

            bool finalWork = runtime.ApplyWork(
                    secondWorker,
                    site,
                    BuiltInWorkTypeIds.Construct,
                    10f,
                    out completed,
                    out appliedEffects,
                    out _)
                && completed
                && appliedEffects
                && placed
                && removed
                && !runtime.TryGetOrderFor(site, BuiltInWorkTypeIds.Construct, out _)
                && runtime.Capture().orders.Count == 0
                && outcomeFixture.Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(8),
                    OutcomeFilter.All).Items.Count == 1
                && outcomeFixture.Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(8),
                    OutcomeFilter.All).Items[0].Exact.facts.Any(value =>
                        value.factId ==
                            MigratedProducerOutcomeIds.TerminalKindFact.Value
                        && value.value == "completed")
                && outcomeFixture.Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(8),
                    OutcomeFilter.All).Items[0].Exact.facts.Any(value =>
                        value.factId ==
                            MigratedProducerOutcomeIds.SupportFacilitiesFact.Value
                        && value.value ==
                            "building:test:construction-support")
                && outcomeFixture.Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(8),
                    OutcomeFilter.All).Items[0].Exact.facts.Any(value =>
                        value.factId ==
                            MigratedProducerOutcomeIds.ContributorsFact.Value
                        && value.value ==
                            "character:test:construction-worker-one=2000,"
                            + "character:test:construction-worker-two=3000");
            if (!finalWork)
            {
                Debug.LogError(
                    "construction lifecycle terminal detail: "
                    + runtime.LastTerminalOutcomeFailureForDebug);
            }
            return finalWork;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(secondWorkerObject);
            UnityEngine.Object.DestroyImmediate(firstWorkerObject);
            UnityEngine.Object.DestroyImmediate(siteObject);
            UnityEngine.Object.DestroyImmediate(building);
        }
    }

    private static bool VerifyConstructionCancellationRefund()
    {
        BuildingSO building = CreateTestBuilding(
            91004,
            "Construction cancellation refund",
            1,
            1,
            5f,
            2);
        GameObject siteObject = new GameObject("WorkAmountCancellationSite");
        ConstructionSite site = siteObject.AddComponent<ConstructionSite>();
        site.RestorePersistentIdentity(
            (BuildingInstanceId)"building:test:work-amount-cancellation");
        CharacterAiEditorTestDependencies.Inject(site);
        FakeWorldItemStackRuntime itemRuntime = new FakeWorldItemStackRuntime();
        FakePhysicalItemSourcePublicationService materialSources =
            new FakePhysicalItemSourcePublicationService(itemRuntime);
        ConstructionInputOwnerFixture constructionInputs = new(
            itemRuntime,
            site);
        MigratedProducerOutcomeEditorFixture outcomeFixture = new(
            "run:work-order-terminal-cancellation");
        MigratedProducerOutcomeProbeTransaction probe = new(
            outcomeFixture.Transaction,
            outcomeFixture.Recorder)
        {
            TargetKind = MigratedProducerOutcomeKind.WorkOrderTerminalResult,
            FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit
        };
        using DomainFactDispatcher constructionFacts =
            outcomeFixture.CreateConstructionWorkFacts(probe);
        WorkOrderRuntime runtime = new WorkOrderRuntime(
            new NoGridProvider(),
            itemRuntime,
            new SingleBuildingLookup(building),
            new ScenarioObjectResolver(probe, constructionFacts),
            CreateExecutionServices(new TrackingWorkforceReplanService()),
            CreateStateStore(),
            materialSources,
            constructionInputs.Runtime);
        try
        {
            site.Initialization(building, new Vector2Int(5, 0));
            if (!runtime.TryCreateConstructionOrder(
                    site,
                    building,
                    site.centerPos,
                    out string orderId,
                    out _))
            {
                return false;
            }
            if (!runtime.TryAttachConstructionPlacement(
                    orderId,
                    "construction-placement:" + orderId,
                    Array.Empty<string>(),
                    out _))
            {
                return false;
            }

            if (!runtime.TryGetOrderFor(
                    site,
                    BuiltInWorkTypeIds.Construct,
                    out WorkOrderProgressState order))
            {
                return false;
            }
            itemRuntime.AddFacilityItemBuffer(
                order.MaterialDestinationId,
                "material:lumber",
                2);
            if (!runtime.RefreshMaterialsReady(site))
            {
                return false;
            }

            string restitutionOperation =
                WorkOrderMaterialDebugContract.RestitutionOperationId(orderId);
            bool rejectedAfterRestitution = !runtime.CancelOrder(
                    orderId,
                    refundDeliveredMaterials: true)
                && itemRuntime.ReleasedQuantity == 2
                && itemRuntime.PhysicalCommitCount == 1
                && itemRuntime.PhysicalAcknowledgementCount == 1
                && itemRuntime.GetAllStacks().Where(stack =>
                        stack != null
                        && string.Equals(
                            stack.ItemId,
                            "material:lumber",
                            StringComparison.Ordinal)
                        && ProductionOutputCommitComponentCodec.Matches(
                            stack.Components,
                            "physical-source:" + restitutionOperation
                            + ":material:lumber:2:2000"))
                    .Sum(stack => stack.Quantity) == 2
                && runtime.TryGetOrderFor(
                    site,
                    BuiltInWorkTypeIds.Construct,
                    out WorkOrderProgressState rejectedOrder)
                && rejectedOrder.Status == WorkOrderStatus.Blocked
                && probe.CommitAttemptCount(
                    MigratedProducerOutcomeKind.WorkOrderTerminalResult) == 1
                && probe.CommitCount(
                    MigratedProducerOutcomeKind.WorkOrderTerminalResult) == 0
                && outcomeFixture.Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(8),
                    OutcomeFilter.All).Items.Count == 0;
            DungeonWorkOrderSaveData pending = runtime.Capture();
            DungeonWorkOrderSaveData roundTrip =
                JsonUtility.FromJson<DungeonWorkOrderSaveData>(
                    JsonUtility.ToJson(pending));
            runtime.ValidateRestorePayload(roundTrip);
            WorkOrderSaveData pendingOrder = roundTrip.orders.SingleOrDefault();
            bool saved = pendingOrder != null
                && pendingOrder.terminalPhase ==
                    WorkOrderTerminalPhase.EffectsAppliedPendingOutcome
                && pendingOrder.terminalKind ==
                    WorkOrderTerminalKind.Cancelled
                && pendingOrder.terminalFactCaptured
                && pendingOrder.terminalFact != null
                && pendingOrder.terminalFact.Matches(pendingOrder)
                && string.IsNullOrEmpty(pendingOrder.terminalPublicationKey)
                && pendingOrder.materialTransfer.phase ==
                    WorkOrderMaterialTransferPhase.RestitutionPending;

            probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            bool retried = runtime.CancelOrder(
                    orderId,
                    refundDeliveredMaterials: true)
                && itemRuntime.ReleasedQuantity == 2
                && itemRuntime.PhysicalCommitCount == 1
                && itemRuntime.PhysicalAcknowledgementCount == 1
                && itemRuntime.GetAllStacks().Where(stack =>
                        stack != null
                        && string.Equals(
                            stack.ItemId,
                            "material:lumber",
                            StringComparison.Ordinal)
                        && ProductionOutputCommitComponentCodec.Matches(
                            stack.Components,
                            "physical-source:" + restitutionOperation
                            + ":material:lumber:2:2000"))
                    .Sum(stack => stack.Quantity) == 2
                && !runtime.TryGetOrderFor(
                    site,
                    BuiltInWorkTypeIds.Construct,
                    out _)
                && probe.CommitAttemptCount(
                    MigratedProducerOutcomeKind.WorkOrderTerminalResult) == 2
                && probe.CommitCount(
                    MigratedProducerOutcomeKind.WorkOrderTerminalResult) == 1
                && outcomeFixture.Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(8),
                    OutcomeFilter.All).Items.Single().Exact.facts.Any(value =>
                        value.factId ==
                            MigratedProducerOutcomeIds.TerminalKindFact.Value
                        && value.value == "cancelled")
                && outcomeFixture.Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(8),
                    OutcomeFilter.All).Items.Single().Exact.facts.Any(value =>
                        value.factId ==
                            MigratedProducerOutcomeIds.RestitutionOperationFact.Value
                        && value.value == restitutionOperation);
            if (!rejectedAfterRestitution || !saved || !retried)
            {
                Debug.LogError(
                    "construction cancellation terminal detail: "
                    + runtime.LastTerminalOutcomeFailureForDebug
                    + "; rejected=" + rejectedAfterRestitution
                    + "; saved=" + saved
                    + "; retried=" + retried);
            }
            return rejectedAfterRestitution && saved && retried;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(siteObject);
            UnityEngine.Object.DestroyImmediate(building);
        }
    }

    private static bool VerifyConstructionTerminalOutcomeRetry()
    {
        BuildingSO building = CreateTestBuilding(
            91013,
            "Construction terminal retry",
            1,
            1,
            3f,
            1);
        GameObject siteObject = new("WorkAmountTerminalRetrySite");
        ConstructionSite site = siteObject.AddComponent<ConstructionSite>();
        site.RestorePersistentIdentity(
            (BuildingInstanceId)"building:test:work-terminal-retry-site");
        CharacterAiEditorTestDependencies.Inject(site);
        FakeWorldItemStackRuntime items = new();
        ConstructionInputOwnerFixture constructionInputs = new(items, site);
        MigratedProducerOutcomeEditorFixture outcomes = new(
            "run:work-order-terminal-retry");
        MigratedProducerOutcomeProbeTransaction probe = new(
            outcomes.Transaction,
            outcomes.Recorder)
        {
            TargetKind = MigratedProducerOutcomeKind.WorkOrderTerminalResult,
            FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit
        };
        using DomainFactDispatcher constructionFacts =
            outcomes.CreateConstructionWorkFacts(probe);
        WorkOrderRuntime runtime = new(
            new NoGridProvider(),
            items,
            new SingleBuildingLookup(building),
            new ScenarioObjectResolver(probe, constructionFacts),
            CreateExecutionServices(new TrackingWorkforceReplanService()),
            CreateStateStore(),
            new FakePhysicalItemSourcePublicationService(items),
            constructionInputs.Runtime);
        bool removed = false;
        int placementCount = 0;
        try
        {
            site.Initialization(building, new Vector2Int(9, 0));
            if (!runtime.TryCreateConstructionOrder(
                    site,
                    building,
                    site.centerPos,
                    out string orderId,
                    out _)
                || !runtime.TryAttachConstructionPlacement(
                    orderId,
                    "construction-placement:" + orderId,
                    new[] { "building:test:work-terminal-retry-support" },
                    out _))
            {
                return false;
            }
            site.ConfigureSite(
                orderId,
                () =>
                {
                    placementCount++;
                    return new BuildingPlacementCommitReceipt(
                        "building:test:work-terminal-retry-final",
                        "building:91013",
                        string.Empty,
                        string.Empty);
                },
                () => removed = true);
            if (!runtime.TryGetOrderFor(
                    site,
                    BuiltInWorkTypeIds.Construct,
                    out WorkOrderProgressState order))
            {
                return false;
            }
            items.AddFacilityItemBuffer(
                order.MaterialDestinationId,
                "material:lumber",
                1);
            if (!runtime.RefreshMaterialsReady(site))
            {
                return false;
            }

            bool rejected = !runtime.ApplyWork(
                    null,
                    site,
                    BuiltInWorkTypeIds.Construct,
                    3f,
                    out bool completed,
                    out bool applied,
                    out _)
                && !completed
                && applied
                && placementCount == 1
                && !removed
                && probe.CommitAttemptCount(
                    MigratedProducerOutcomeKind.WorkOrderTerminalResult) == 1
                && probe.CommitCount(
                    MigratedProducerOutcomeKind.WorkOrderTerminalResult) == 0;
            if (!rejected)
            {
                Debug.LogError(
                    "construction retry rejection detail: "
                    + runtime.LastTerminalOutcomeFailureForDebug
                    + "; placement=" + placementCount
                    + "; removed=" + removed);
            }
            DungeonWorkOrderSaveData pending = runtime.Capture();
            string json = JsonUtility.ToJson(pending);
            DungeonWorkOrderSaveData roundTrip =
                JsonUtility.FromJson<DungeonWorkOrderSaveData>(json);
            runtime.ValidateRestorePayload(roundTrip);
            bool saved = roundTrip.orders.Count == 1
                && roundTrip.orders[0].terminalPhase ==
                    WorkOrderTerminalPhase.EffectsAppliedPendingOutcome
                && roundTrip.orders[0].terminalKind ==
                    WorkOrderTerminalKind.Completed
                && roundTrip.orders[0].terminalFactCaptured
                && roundTrip.orders[0].terminalFact != null
                && roundTrip.orders[0].terminalFact.Matches(roundTrip.orders[0])
                && string.IsNullOrEmpty(
                    roundTrip.orders[0].terminalPublicationKey)
                && roundTrip.orders[0].finalFacilityId ==
                    "building:test:work-terminal-retry-final"
                && roundTrip.orders[0].supportBuildingIds.SequenceEqual(
                    new[] { "building:test:work-terminal-retry-support" });

            probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            string beforeTransition = JsonUtility.ToJson(runtime.Capture());
            bool completionAccepted = runtime.DebugCompleteOrder(orderId, out _);
            string afterTransition = JsonUtility.ToJson(runtime.Capture());
            bool retried = completionAccepted
                && removed
                && placementCount == 1
                && runtime.Capture().orders.Count == 0
                && probe.CommitAttemptCount(
                    MigratedProducerOutcomeKind.WorkOrderTerminalResult) == 2
                && probe.CommitCount(
                    MigratedProducerOutcomeKind.WorkOrderTerminalResult) == 1
                && outcomes.Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(8),
                    OutcomeFilter.All).Items.Count == 1;
            if (!saved || !retried)
            {
                Debug.LogError(
                    "construction retry saved/retried detail: "
                    + runtime.LastTerminalOutcomeFailureForDebug
                    + "; saved=" + saved
                    + "; retried=" + retried
                    + "; placement=" + placementCount
                    + "; removed=" + removed);
            }
            if (retried)
            {
                _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                    outcomes,
                    MigratedProducerOutcomeKind.WorkOrderTerminalResult,
                    probe,
                    beforeTransition,
                    afterTransition);
            }
            return rejected && saved && retried;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(siteObject);
            UnityEngine.Object.DestroyImmediate(building);
        }
    }

    private static bool VerifyRepairOutcomeRetryAndSaveRoundTrip()
    {
        BuildingSO building = CreateTestBuilding(
            91023,
            "Repair outcome retry",
            1,
            1,
            3f,
            1);
        MigratedProducerOutcomeEditorFixture outcomes = new(
            "run:repair-work-outcome-retry");
        MigratedProducerOutcomeProbeTransaction probe = new(
            outcomes.Transaction,
            outcomes.Recorder)
        {
            TargetKind = MigratedProducerOutcomeKind.RepairWorkResult,
            FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit
        };
        try
        {
            WorkOrderAggregateStateStore firstStore = CreateStateStore();
            FakeRepairCompletionDeliveryCommand deliveries = new();
            using DomainFactDispatcher repairFacts =
                outcomes.CreateRepairWorkFacts(deliveries, probe);
            WorkOrderRuntime first = new(
                new NoGridProvider(),
                new FakeWorldItemStackRuntime(),
                new SingleBuildingLookup(building),
                new ScenarioObjectResolver(probe, deliveries, repairFacts),
                CreateExecutionServices(new TrackingWorkforceReplanService()),
                firstStore,
                new FakePhysicalItemSourcePublicationService(
                    new FakeWorldItemStackRuntime()));
            CharacterId firstWorker = new("character:repair-worker-a");
            CharacterId secondWorker = new("character:repair-worker-b");
            RepairWorkOutcomeHandle handle = first.BeginOrResume(
                RepairWorkOutcomeKind.StructuralIntegrity,
                "building:repair-facility",
                "building:repair-facility",
                "building-definition:91023",
                firstWorker,
                4,
                boundsKnown: true,
                RepairWorkPrimaryUnit.Point,
                25f,
                0f);
            first.RecordProgress(
                handle,
                firstWorker,
                2f,
                boundsKnown: true,
                29f,
                0f);
            first.RecordProgress(
                handle,
                secondWorker,
                3f,
                boundsKnown: true,
                35f,
                0f);
            bool rejected = !first.TryComplete(
                    handle,
                    firstWorker,
                    CharacterCommandOrigin.Autonomous,
                    2f,
                    4,
                    boundsKnown: true,
                    RepairWorkPrimaryUnit.Point,
                    25f,
                    35f,
                    0f,
                    0f,
                    RepairWorkMaterialReceipt.None,
                    out _,
                    out _)
                && probe.CommitAttemptCount(
                    MigratedProducerOutcomeKind.RepairWorkResult) == 1
                && probe.CommitCount(
                    MigratedProducerOutcomeKind.RepairWorkResult) == 0;

            DungeonWorkOrderSaveData captured = first.Capture();
            DungeonWorkOrderSaveData roundTrip = JsonUtility.FromJson<
                DungeonWorkOrderSaveData>(JsonUtility.ToJson(captured));
            first.ValidateRestorePayload(roundTrip);
            bool saved = roundTrip.version ==
                    DungeonWorkOrderSaveData.CurrentVersion
                && roundTrip.nextRepairOutcomeSequence == 2
                && roundTrip.repairOutcomes.Count == 1
                && roundTrip.repairOutcomes[0].terminal
                && roundTrip.repairOutcomes[0].boundsKnown
                && roundTrip.repairOutcomes[0].contributions.Count == 2
                && roundTrip.repairOutcomes[0].completionWorkerId
                    == firstWorker.Value
                && roundTrip.repairOutcomes[0].completionOrigin
                    == CharacterCommandOrigin.Autonomous
                && Mathf.Approximately(
                    roundTrip.repairOutcomes[0].completionContributionWork,
                    2f)
                && string.IsNullOrEmpty(roundTrip.repairOutcomes[0].publicationKey)
                && !roundTrip.repairOutcomes[0].publicationConfirmed
                && string.IsNullOrEmpty(
                    roundTrip.repairOutcomes[0].completionPublicationKey)
                && Mathf.Approximately(
                    roundTrip.repairOutcomes[0].primaryBefore,
                    25f)
                && Mathf.Approximately(
                    roundTrip.repairOutcomes[0].primaryAfter,
                    35f);

            WorkOrderAggregateState restoredState = new()
            {
                NextOrderSequence = roundTrip.nextOrderSequence,
                NextRepairOutcomeSequence =
                    roundTrip.nextRepairOutcomeSequence
            };
            foreach (RepairWorkOutcomeSaveData beforeChildOwner in
                     roundTrip.repairOutcomes)
            {
                restoredState.RepairOutcomesById.Add(
                    beforeChildOwner.operationId,
                    beforeChildOwner.Clone());
            }
            WorkOrderAggregateStateStore freshStore = CreateStateStore();
            freshStore.Replace(restoredState);
            probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            WorkOrderRuntime fresh = new(
                new NoGridProvider(),
                new FakeWorldItemStackRuntime(),
                new SingleBuildingLookup(building),
                new ScenarioObjectResolver(probe, deliveries, repairFacts),
                CreateExecutionServices(new TrackingWorkforceReplanService()),
                freshStore,
                new FakePhysicalItemSourcePublicationService(
                    new FakeWorldItemStackRuntime()));
            string beforeTransition = JsonUtility.ToJson(fresh.Capture());
            fresh.Tick();
            DungeonWorkOrderSaveData deferred = fresh.Capture();
            string afterTransition = JsonUtility.ToJson(deferred);
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                outcomes,
                MigratedProducerOutcomeKind.RepairWorkResult,
                probe,
                beforeTransition,
                afterTransition);
            RepairWorkOutcomeSaveData pending = deferred.repairOutcomes
                .SingleOrDefault();
            string expectedChild = RepairWorkCompletionObservation.ResultKey(
                roundTrip.repairOutcomes[0].operationId).ToString();
            WorkCompletionIdentityDeliveryRequest initialRequest =
                deliveries.Requests.SingleOrDefault();
            bool deferredOwned = pending != null
                && pending.publicationConfirmed
                && pending.publicationKey == expectedChild
                && pending.completionPublicationKey
                    == WorkCompletionIdentityOutcomeIds.ResultKey(initialRequest).ToString()
                && pending.completionWorkerId == firstWorker.Value
                && pending.completionOrigin == CharacterCommandOrigin.Autonomous
                && Mathf.Approximately(pending.completionContributionWork, 2f)
                && initialRequest.WorkId == BuiltInWorkTypeIds.Repair.Value
                && initialRequest.Character.Equals(firstWorker)
                && initialRequest.Origin == CharacterCommandOrigin.Autonomous
                && initialRequest.OperationId == pending.operationId
                && initialRequest.TerminalOwnerId
                    == RepairWorkCompletionObservation.TerminalOwnerId
                && initialRequest.ChildResultKey == expectedChild
                && Mathf.Approximately(
                    initialRequest.ContributionWorkUnits,
                    2f)
                && probe.CommitAttemptCount(
                    MigratedProducerOutcomeKind.RepairWorkResult) == 2
                && probe.CommitCount(
                    MigratedProducerOutcomeKind.RepairWorkResult) == 1
                && outcomes.Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(8),
                    OutcomeFilter.All).Items.Count == 1;

            DungeonWorkOrderSaveData deferredRoundTrip = JsonUtility.FromJson<
                DungeonWorkOrderSaveData>(JsonUtility.ToJson(deferred));
            fresh.ValidateRestorePayload(deferredRoundTrip);
            bool corruptFrozenRejected = RejectFrozen(value => value.completionWorkerId = string.Empty)
                && RejectFrozen(value => value.completionOrigin = (CharacterCommandOrigin)int.MaxValue)
                && RejectFrozen(value => value.completionContributionWork = 3f)
                && RejectFrozen(value => value.publicationKey = "unrelated|child|1|0");
            RepairWorkCompletionObservation.ValidateCompletionRestoreJoin(
                deferredRoundTrip.repairOutcomes,
                Array.Empty<WorkCompletionIdentityDeliveryCursorSaveData>(),
                outcomes.Ledger.CaptureGameplayOutcomes(),
                outcomes.Transaction.Capture());
            bool childMatches = RepairWorkCompletionObservation.MatchesCompletionChild(initialRequest,
                outcomes.Ledger.GetGlobal(OutcomeCursor.FirstPage(8), OutcomeFilter.All).Items.Single().Exact);
            WorkOrderAggregateState resumedState = new()
            {
                NextOrderSequence = deferredRoundTrip.nextOrderSequence,
                NextRepairOutcomeSequence =
                    deferredRoundTrip.nextRepairOutcomeSequence
            };
            foreach (RepairWorkOutcomeSaveData savedPending in
                     deferredRoundTrip.repairOutcomes)
            {
                resumedState.RepairOutcomesById.Add(
                    savedPending.operationId,
                    savedPending.Clone());
            }
            WorkOrderAggregateStateStore resumedStore = CreateStateStore();
            resumedStore.Replace(resumedState);
            deliveries.Status = WorkCompletionIdentityDeliveryStatus.Applied;
            WorkOrderRuntime resumed = new(
                new NoGridProvider(),
                new FakeWorldItemStackRuntime(),
                new SingleBuildingLookup(building),
                new ScenarioObjectResolver(probe, deliveries, repairFacts),
                CreateExecutionServices(new TrackingWorkforceReplanService()),
                resumedStore,
                new FakePhysicalItemSourcePublicationService(
                    new FakeWorldItemStackRuntime()));
            resumed.Tick();
            WorkCompletionIdentityDeliveryRequest acknowledgedRequest =
                deliveries.Requests.LastOrDefault();
            bool acknowledged = resumed.Capture().repairOutcomes.Count == 0
                && deliveries.Requests.Count == 2
                && acknowledgedRequest.PayloadFingerprint
                    == initialRequest.PayloadFingerprint
                && probe.CommitAttemptCount(
                    MigratedProducerOutcomeKind.RepairWorkResult) == 2
                && probe.CommitCount(
                    MigratedProducerOutcomeKind.RepairWorkResult) == 1
                && outcomes.Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(8),
                    OutcomeFilter.All).Items.Count == 1;
            return rejected && saved && deferredOwned && acknowledged && corruptFrozenRejected && childMatches;

            bool RejectFrozen(Action<RepairWorkOutcomeSaveData> change)
            {
                var invalid = JsonUtility.FromJson<DungeonWorkOrderSaveData>(JsonUtility.ToJson(deferredRoundTrip));
                change(invalid.repairOutcomes.Single());
                try
                {
                    fresh.ValidateRestorePayload(invalid);
                    RepairWorkCompletionObservation.ValidateCompletionRestoreJoin(
                        invalid.repairOutcomes,
                        Array.Empty<WorkCompletionIdentityDeliveryCursorSaveData>(),
                        outcomes.Ledger.CaptureGameplayOutcomes(),
                        outcomes.Transaction.Capture());
                    return false;
                }
                catch (InvalidOperationException) { return true; }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(building);
        }
    }

    private static bool VerifyRepairOutcomeKindAndMaterialCoverage()
    {
        BuildingSO building = CreateTestBuilding(
            91024,
            "Repair outcome kind coverage",
            1,
            1,
            3f,
            1);
        MigratedProducerOutcomeEditorFixture outcomes = new(
            "run:repair-work-kind-coverage");
        try
        {
            FakeRepairCompletionDeliveryCommand deliveries = new()
            {
                Status = WorkCompletionIdentityDeliveryStatus.Applied
            };
            using DomainFactDispatcher repairFacts =
                outcomes.CreateRepairWorkFacts(deliveries);
            WorkOrderRuntime runtime = new(
                new NoGridProvider(),
                new FakeWorldItemStackRuntime(),
                new SingleBuildingLookup(building),
                new ScenarioObjectResolver(
                    outcomes.Transaction,
                    deliveries,
                    repairFacts),
                CreateExecutionServices(new TrackingWorkforceReplanService()),
                CreateStateStore(),
                new FakePhysicalItemSourcePublicationService(
                    new FakeWorldItemStackRuntime()));
            CharacterId worker = new("character:repair-kind-worker");
            RepairWorkOutcomeKind[] kinds =
            {
                RepairWorkOutcomeKind.GeneralDamage,
                RepairWorkOutcomeKind.StructuralIntegrity,
                RepairWorkOutcomeKind.DefenseFacility,
                RepairWorkOutcomeKind.EquipmentDurability,
                RepairWorkOutcomeKind.AutomationMaintenance
            };
            RepairWorkPrimaryUnit[] units =
            {
                RepairWorkPrimaryUnit.Flag,
                RepairWorkPrimaryUnit.Point,
                RepairWorkPrimaryUnit.Percent,
                RepairWorkPrimaryUnit.Ratio,
                RepairWorkPrimaryUnit.Percent
            };
            float[] primaryBefore = { 1f, 25f, 40f, 0.25f, 30f };
            float[] primaryAfter = { 0f, 35f, 100f, 0.9f, 85f };
            float[] secondaryBefore = { 0f, 0f, 1f, 0f, 20f };
            float[] secondaryAfter = { 0f, 0f, 0f, 0f, 0f };

            for (int index = 0; index < kinds.Length; index++)
            {
                string facilityId = "building:repair-kind-" + index;
                string targetId = kinds[index]
                    == RepairWorkOutcomeKind.EquipmentDurability
                        ? "equipment:repair-kind-target"
                        : facilityId;
                RepairWorkOutcomeHandle handle = runtime.BeginOrResume(
                    kinds[index],
                    facilityId,
                    targetId,
                    kinds[index] == RepairWorkOutcomeKind.EquipmentDurability
                        ? string.Empty
                        : "building-definition:91024",
                    worker,
                    9,
                    boundsKnown: true,
                    units[index],
                    primaryBefore[index],
                    secondaryBefore[index]);
                runtime.RecordProgress(
                    handle,
                    worker,
                    1f + index,
                    boundsKnown: true,
                    primaryAfter[index],
                    secondaryAfter[index]);
                RepairWorkMaterialReceipt material = kinds[index]
                    == RepairWorkOutcomeKind.EquipmentDurability
                        ? new RepairWorkMaterialReceipt(
                            "transfer:repair-kind-material",
                            "commit-repair-kind-material",
                            2000L,
                            new[]
                            {
                                new RepairWorkMaterialSourceSaveData
                                {
                                    stackId = "stack:repair-kind-ingot",
                                    itemId = "item:iron-ingot",
                                    quantity = 2
                                }
                            })
                        : RepairWorkMaterialReceipt.None;
                if (!runtime.TryComplete(
                        handle,
                        worker,
                        CharacterCommandOrigin.Autonomous,
                        1f + index,
                        9,
                        boundsKnown: true,
                        units[index],
                        primaryBefore[index],
                        primaryAfter[index],
                        secondaryBefore[index],
                        secondaryAfter[index],
                        material,
                        out _,
                        out _))
                {
                    return false;
                }
            }

            GameplayOutcomeSnapshot[] exact = outcomes.Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(16),
                    OutcomeFilter.All)
                .Items
                .Select(value => value.Exact)
                .Where(value => value != null)
                .ToArray();
            string[] expectedKinds =
            {
                "general-damage",
                "structural-integrity",
                "defense-facility",
                "equipment-durability",
                "automation-maintenance"
            };
            GameplayOutcomeSnapshot equipment = exact.SingleOrDefault(value =>
                value.facts.Any(fact => fact.factId == "repair-kind"
                    && fact.value == "equipment-durability"));
            return runtime.Capture().repairOutcomes.Count == 0
                && exact.Length == 5
                && deliveries.Requests.Count == 5
                && expectedKinds.All(kind => exact.Any(value =>
                    value.facts.Any(fact => fact.factId == "repair-kind"
                        && fact.value == kind)))
                && equipment != null
                && equipment.facts.Any(fact =>
                    fact.factId == "repair-material-sources"
                    && fact.value
                        == "stack:repair-kind-ingot=item:iron-ingot=2")
                && equipment.metrics.Any(metric =>
                    metric.metricId == MigratedProducerOutcomeIds.InputMassMetric.Value
                    && Math.Abs(metric.value - 2000d) < 0.001d);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(building);
        }
    }

    private static bool VerifyPurchasedFacilityKitDelivery()
    {
        BuildingSO building = CreateTestBuilding(
            91006,
            "설치 키트 테스트 시설",
            1,
            1,
            5f,
            4);
        GameObject siteObject =
            new GameObject("WorkAmountInstallationKitSite");
        ConstructionSite site =
            siteObject.AddComponent<ConstructionSite>();
        site.RestorePersistentIdentity(
            (BuildingInstanceId)"building:test:work-amount-installation-kit");
        CharacterAiEditorTestDependencies.Inject(site);
        FakeWorldItemStackRuntime itemRuntime =
            new FakeWorldItemStackRuntime();
        string kitItemId =
            FacilityInstallationKitItemIds.ForBuilding(building);
        itemRuntime.AddAvailableItem(kitItemId, 1);
        ConstructionInputOwnerFixture constructionInputs = new(
            itemRuntime,
            site);
        WorkOrderRuntime runtime = new WorkOrderRuntime(
            new NoGridProvider(),
            itemRuntime,
            new SingleBuildingLookup(building),
            new ScenarioObjectResolver(),
            CreateExecutionServices(new TrackingWorkforceReplanService()),
            CreateStateStore(),
            new FakePhysicalItemSourcePublicationService(itemRuntime),
            constructionInputs.Runtime);
        try
        {
            site.Initialization(building, new Vector2Int(4, 0));
            if (!runtime.TryCreateConstructionOrder(
                    site,
                    building,
                    site.centerPos,
                    out _,
                    out _)
                || !runtime.TryGetOrderFor(
                    site,
                    BuiltInWorkTypeIds.Construct,
                    out WorkOrderProgressState order)
                || !order.ItemMaterialRequirements.TryGetValue(
                    kitItemId,
                    out int required)
                || required != 1
                || !itemRuntime.RequestedItems.TryGetValue(
                    kitItemId,
                    out int requested)
                || requested != 1)
            {
                return false;
            }

            itemRuntime.AddFacilityItemBuffer(
                order.MaterialDestinationId,
                kitItemId,
                1);
            bool ready = runtime.RefreshMaterialsReady(site)
                && runtime.TryGetOrderFor(
                    site,
                    BuiltInWorkTypeIds.Construct,
                    out order)
                && order.Status == WorkOrderStatus.Ready
                && order.DeliveredItemMaterials.TryGetValue(
                    kitItemId,
                    out int delivered)
                && delivered == 1;
            DungeonWorkOrderSaveData save = runtime.Capture();
            return ready
                && save.version == DungeonWorkOrderSaveData.CurrentVersion
                && save.orders.Count == 1
                && save.orders[0].itemMaterials.Count == 1
                && string.Equals(
                    save.orders[0].itemMaterials[0].itemId,
                    kitItemId,
                    StringComparison.Ordinal)
                && save.orders[0].itemMaterials[0].delivered == 1;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(siteObject);
            UnityEngine.Object.DestroyImmediate(building);
        }
    }

    private static bool VerifyOrphanConstructionRecovery()
    {
        BuildingSO building = CreateTestBuilding(
            91005,
            "Orphan construction recovery",
            1,
            1,
            5f,
            2);
        GameObject siteObject = new GameObject("WorkAmountOrphanSite");
        ConstructionSite site = siteObject.AddComponent<ConstructionSite>();
        site.RestorePersistentIdentity(
            (BuildingInstanceId)"building:test:work-amount-orphan");
        CharacterAiEditorTestDependencies.Inject(site);
        FakeWorldItemStackRuntime itemRuntime = new FakeWorldItemStackRuntime();
        ConstructionInputOwnerFixture constructionInputs = new(
            itemRuntime,
            site);
        WorkOrderRuntime runtime = new WorkOrderRuntime(
            new NoGridProvider(),
            itemRuntime,
            new SingleBuildingLookup(building),
            new ScenarioObjectResolver(),
            CreateExecutionServices(new TrackingWorkforceReplanService()),
            CreateStateStore(),
            new FakePhysicalItemSourcePublicationService(itemRuntime),
            constructionInputs.Runtime);
        try
        {
            site.Initialization(building, new Vector2Int(6, 0));
            if (!runtime.TryCreateConstructionOrder(
                    site,
                    building,
                    site.centerPos,
                    out _,
                    out _))
            {
                return false;
            }

            WorkOrderSaveData owner = runtime.Capture().orders.Single();
            WorkConstructionInputOwnerDescriptor ownerDescriptor = new(
                owner.workOrderId,
                owner.materialDestinationId,
                owner.constructionSitePersistentId,
                new Vector2Int(owner.gridX, owner.gridY),
                owner.itemMaterials.ToDictionary(
                    value => value.itemId,
                    value => value.required,
                    StringComparer.Ordinal),
                owner.materialBufferCapacityGrams,
                owner.materialMassAuthorityRevision,
                owner.materialCapacityFingerprint);
            UnityEngine.Object.DestroyImmediate(siteObject);
            bool activeMutationRejected =
                !constructionInputs.Runtime.TryValidateAuthority(
                    ownerDescriptor,
                    out string activeFailure)
                && string.Equals(
                    activeFailure,
                    "work-construction-input-descriptor-invalid",
                    StringComparison.Ordinal);
            bool terminalReleaseAccepted =
                constructionInputs.Runtime.TryPrepareTerminalRelease(
                    ownerDescriptor,
                    "work-construction-orphaned-fixture",
                    out string terminalFailure);
            if (!activeMutationRejected || !terminalReleaseAccepted)
            {
                Debug.LogWarning(
                    "Orphan construction terminal authority diagnostic "
                    + $"activeFailure={activeFailure} "
                    + $"terminalFailure={terminalFailure}");
                return false;
            }
            runtime.Tick();
            return runtime.Capture().orders.Count == 0
                && itemRuntime.ReleasedQuantity == 2;
        }
        finally
        {
            if (siteObject != null)
            {
                UnityEngine.Object.DestroyImmediate(siteObject);
            }
            UnityEngine.Object.DestroyImmediate(building);
        }
    }

    private static bool VerifyInvalidRestorePreflightPreservesLiveState()
    {
        BuildingSO building = CreateTestBuilding(
            91007,
            "Work-order invalid restore",
            1,
            1,
            5f,
            1);
        GameObject siteObject = new GameObject(
            "WorkAmountInvalidRestoreLiveSite");
        ConstructionSite site = siteObject.AddComponent<ConstructionSite>();
        site.RestorePersistentIdentity(
            (BuildingInstanceId)"building:test:work-order-preflight");
        CharacterAiEditorTestDependencies.Inject(site);
        FakeWorldItemStackRuntime items = new FakeWorldItemStackRuntime();
        ConstructionInputOwnerFixture constructionInputs = new(items, site);
        WorkOrderRuntime runtime = new WorkOrderRuntime(
            new NoGridProvider(),
            items,
            new SingleBuildingLookup(building),
            new ScenarioObjectResolver(),
            CreateExecutionServices(new TrackingWorkforceReplanService()),
            CreateStateStore(),
            new FakePhysicalItemSourcePublicationService(items),
            constructionInputs.Runtime);
        try
        {
            site.Initialization(building, new Vector2Int(2, 0));
            if (!runtime.TryCreateConstructionOrder(
                    site,
                    building,
                    site.centerPos,
                    out string liveOrderId,
                    out _))
            {
                return false;
            }

            DungeonWorkOrderSaveData invalid = runtime.Capture();
            invalid.nextOrderSequence = 0;
            invalid.orders.Add(invalid.orders[0]);
            WorkOrdersSaveSection section = new WorkOrdersSaveSection(
                runtime,
                EmptyPhysicalItemRestoreCandidateQuery.Instance,
                EmptyPhysicalItemRestoreCandidateOutputQuery.Instance);
            DungeonGameRestoreReport report = new DungeonGameRestoreReport();
            bool rejected = false;
            try
            {
                section.StageRestore(
                    JsonUtility.ToJson(invalid),
                    DungeonWorkOrderSaveData.CurrentVersion,
                    report);
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }

            DungeonWorkOrderSaveData after = runtime.Capture();
            return rejected
                && site != null
                && after.orders.Count == 1
                && string.Equals(
                    after.orders[0].workOrderId,
                    liveOrderId,
                    StringComparison.Ordinal);
        }
        finally
        {
            if (siteObject != null)
            {
                UnityEngine.Object.DestroyImmediate(siteObject);
            }

            UnityEngine.Object.DestroyImmediate(building);
        }
    }

    private static bool VerifyRegistryPublishesDetachedConstructionSite()
    {
        BuildingSO building = CreateTestBuilding(
            91008,
            "Detached construction restore",
            1,
            1,
            5f,
            1);
        GameObject liveSiteObject = new GameObject(
            "WorkAmountRegistryLiveSite");
        ConstructionSite liveSite =
            liveSiteObject.AddComponent<ConstructionSite>();
        liveSite.RestorePersistentIdentity(
            (BuildingInstanceId)"building:test:work-order-registry-live");
        CharacterAiEditorTestDependencies.Inject(liveSite);
        FakeWorldItemStackRuntime items = new FakeWorldItemStackRuntime();
        DungeonRuntimeAggregateRootStore rootStore =
            new DungeonRuntimeAggregateRootStore();
        RestoreWorldCandidateIndex candidateIndex =
            new RestoreWorldCandidateIndex();
        Grid liveGrid = new Grid(12, 4);
        ConstructionInputOwnerFixture constructionInputs = new(items, liveSite);
        WorkOrderRuntime runtime = new WorkOrderRuntime(
            new NoGridProvider(),
            items,
            new SingleBuildingLookup(building),
            new ScenarioObjectResolver(),
            CreateExecutionServices(new TrackingWorkforceReplanService()),
            new WorkOrderAggregateStateStore(rootStore, candidateIndex),
            new FakePhysicalItemSourcePublicationService(items),
            constructionInputs.Runtime);
        CandidateFacilitySection facilitySection =
            new CandidateFacilitySection(candidateIndex, new Grid(12, 4));
        PassiveSaveSection physicalItems = new PassiveSaveSection(
            PhysicalItemsSaveSection.Id,
            DungeonSaveRestorePhase.Items);
        WorkOrdersSaveSection workOrders = new WorkOrdersSaveSection(
            runtime,
            EmptyPhysicalItemRestoreCandidateQuery.Instance,
            EmptyPhysicalItemRestoreCandidateOutputQuery.Instance);
        WorkOrderRestoreLifecycleProbe successProbe =
            new WorkOrderRestoreLifecycleProbe(
                failAfterPublish: false,
                verifyPublishedState: () =>
                {
                    ConstructionSite incoming = Resources
                        .FindObjectsOfTypeAll<ConstructionSite>()
                        .FirstOrDefault(candidate => candidate != null
                            && candidate.WorkOrderId == "work:000002");
                    return liveSite != null
                        && liveSite.gameObject.activeSelf
                        && incoming != null
                        && incoming.IsDetachedRestoreCandidate
                        && !incoming.gameObject.activeSelf;
                });
        ConstructionSite restoredSite = null;
        try
        {
            liveSite.SetGrid(liveGrid);
            liveSite.Initialization(building, new Vector2Int(1, 0));
            if (!liveGrid.RegisterOccupant(
                    liveSite,
                    GridLayer.Construction,
                    liveSite.buildPoses,
                    connectPositions: false))
            {
                return false;
            }
            if (!runtime.TryCreateConstructionOrder(
                    liveSite,
                    building,
                    liveSite.centerPos,
                    out _,
                    out _))
            {
                return false;
            }

            DungeonSaveSectionRegistry registry =
                new DungeonSaveSectionRegistry(
                    new IDungeonSaveSection[]
                    {
                        facilitySection,
                        physicalItems,
                        workOrders
                    },
                    rootStore,
                    new IDungeonRestoreTransactionParticipant[]
                    {
                        facilitySection,
                        runtime,
                        constructionInputs.Claims,
                        constructionInputs.Capacities,
                        successProbe
                    });
            List<DungeonSaveSectionEnvelope> envelopes = registry.CaptureAll();
            DungeonWorkOrderSaveData incoming = CreateRestoredOrder(
                building,
                "work:000002",
                3,
                new Vector2Int(4, 0));
            string expectedJson = JsonUtility.ToJson(incoming);
            DungeonSaveSectionEnvelope workEnvelope = envelopes.Single(
                envelope => string.Equals(
                    envelope.sectionId,
                    WorkOrdersSaveSection.Id,
                    StringComparison.Ordinal));
            workEnvelope.payloadJson = JsonUtility.ToJson(incoming);

            DungeonGameRestoreReport report = new DungeonGameRestoreReport();
            bool restored = registry.RestoreAll(envelopes, report);
            restoredSite = Resources.FindObjectsOfTypeAll<ConstructionSite>()
                .FirstOrDefault(candidate => candidate != null
                    && string.Equals(
                        candidate.WorkOrderId,
                        "work:000002",
                        StringComparison.Ordinal));
            DungeonWorkOrderSaveData captured = runtime.Capture();
            string capturedJson = JsonUtility.ToJson(captured);
            bool passed = restored
                && report.Success
                && liveSite == null
                && restoredSite != null
                && restoredSite.gameObject.activeSelf
                && !restoredSite.IsDetachedRestoreCandidate
                && runtime.TryGetOrderFor(
                    restoredSite,
                    BuiltInWorkTypeIds.Construct,
                    out WorkOrderProgressState order)
                && order.Status == WorkOrderStatus.Ready
                && captured.nextOrderSequence == 3
                && captured.orders.Count == 1
                && captured.orders[0].workOrderId == "work:000002"
                && string.Equals(
                    expectedJson,
                    capturedJson,
                    StringComparison.Ordinal)
                && successProbe.PublishCount == 1
                && successProbe.PublishedStateWasReversible
                && successProbe.RollbackCount == 0
                && successProbe.CompleteCount == 1
                && rootStore.PublishedRestoreRevision == 1
                && CountDetachedConstructionSites() == 0
                && report.Warnings.Count == 0;
            if (!passed)
            {
                Debug.LogWarning(
                    "Work-order publish diagnostic "
                    + $"restored={restored} success={report.Success} "
                    + $"errors={string.Join(" | ", report.Errors)} "
                    + $"warnings={string.Join(" | ", report.Warnings)} "
                    + $"liveDestroyed={liveSite == null} "
                    + $"restoredSite={restoredSite != null} "
                    + $"active={restoredSite != null && restoredSite.gameObject.activeSelf} "
                    + $"detached={restoredSite != null && restoredSite.IsDetachedRestoreCandidate} "
                    + $"probePublish={successProbe.PublishCount} "
                    + $"probeReversible={successProbe.PublishedStateWasReversible} "
                    + $"probeRollback={successProbe.RollbackCount} "
                    + $"probeComplete={successProbe.CompleteCount} "
                    + $"revision={rootStore.PublishedRestoreRevision} "
                    + $"expected={expectedJson} actual={capturedJson} "
                    + $"detachedCount={CountDetachedConstructionSites()}");
            }

            return passed;
        }
        finally
        {
            if (restoredSite != null)
            {
                UnityEngine.Object.DestroyImmediate(restoredSite.gameObject);
            }

            if (liveSiteObject != null)
            {
                UnityEngine.Object.DestroyImmediate(liveSiteObject);
            }

            UnityEngine.Object.DestroyImmediate(building);
        }
    }

    private static DungeonWorkOrderSaveData CreateRestoredOrder(
        BuildingSO building,
        string orderId,
        int nextSequence,
        Vector2Int position)
    {
        return new DungeonWorkOrderSaveData
        {
            version = DungeonWorkOrderSaveData.CurrentVersion,
            nextOrderSequence = nextSequence,
            orders = new List<WorkOrderSaveData>
            {
                new WorkOrderSaveData
                {
                    workOrderId = orderId,
                    workTypeId = BuiltInWorkTypeIds.Construct.Value,
                    targetBuildingId = building.id,
                    gridX = position.x,
                    gridY = position.y,
                    requiredWork = 5f,
                    completedWork = 1f,
                    materialDestinationId =
                        WorkConstructionInputOwnerAuthority.DestinationFor(
                            orderId),
                    constructionSitePersistentId =
                        "building:test:work-order-restored:" + orderId,
                    reservedWorkerPersistentId = string.Empty,
                    qualityRoll = new CraftQualityRollSaveData
                    {
                        attemptIndex = 0,
                        randomA = 0,
                        randomB = 0,
                        randomC = 0
                    },
                    qualityAttemptIndex = 0,
                    status = WorkOrderStatus.Ready,
                    itemMaterials = new List<WorkOrderItemMaterialSaveData>(),
                    recoveryOutputs = new List<WorkOrderItemMaterialSaveData>()
                }
            }
        };
    }

    private static bool VerifyLateParticipantFailureRestoresLiveConstructionSite()
    {
        BuildingSO building = CreateTestBuilding(
            91009,
            "Discarded construction restore",
            1,
            1,
            5f,
            1);
        GameObject liveSiteObject = new GameObject(
            "WorkAmountDiscardLiveSite");
        ConstructionSite liveSite =
            liveSiteObject.AddComponent<ConstructionSite>();
        liveSite.RestorePersistentIdentity(
            (BuildingInstanceId)"building:test:work-order-discard-live");
        CharacterAiEditorTestDependencies.Inject(liveSite);
        DungeonRuntimeAggregateRootStore rootStore =
            new DungeonRuntimeAggregateRootStore();
        RestoreWorldCandidateIndex candidateIndex =
            new RestoreWorldCandidateIndex();
        Grid liveGrid = new Grid(12, 4);
        FakeWorldItemStackRuntime items = new FakeWorldItemStackRuntime();
        ConstructionInputOwnerFixture constructionInputs = new(items, liveSite);
        WorkOrderRuntime runtime = new WorkOrderRuntime(
            new NoGridProvider(),
            items,
            new SingleBuildingLookup(building),
            new ScenarioObjectResolver(),
            CreateExecutionServices(new TrackingWorkforceReplanService()),
            new WorkOrderAggregateStateStore(rootStore, candidateIndex),
            new FakePhysicalItemSourcePublicationService(items),
            constructionInputs.Runtime);
        CandidateFacilitySection facilitySection =
            new CandidateFacilitySection(candidateIndex, new Grid(12, 4));
        PassiveSaveSection physicalItems = new PassiveSaveSection(
            PhysicalItemsSaveSection.Id,
            DungeonSaveRestorePhase.Items);
        WorkOrdersSaveSection workOrders = new WorkOrdersSaveSection(
            runtime,
            EmptyPhysicalItemRestoreCandidateQuery.Instance,
            EmptyPhysicalItemRestoreCandidateOutputQuery.Instance);
        WorkOrderRestoreLifecycleProbe lateProbe =
            new WorkOrderRestoreLifecycleProbe(
                failAfterPublish: true,
                verifyPublishedState: () =>
                {
                    ConstructionSite incoming = Resources
                        .FindObjectsOfTypeAll<ConstructionSite>()
                        .FirstOrDefault(candidate => candidate != null
                            && candidate.WorkOrderId == "work:000002");
                    return liveSite != null
                        && liveSite.gameObject.activeSelf
                        && incoming != null
                        && incoming.IsDetachedRestoreCandidate
                        && !incoming.gameObject.activeSelf
                        && runtime.TryGetOrderFor(
                            incoming,
                            BuiltInWorkTypeIds.Construct,
                            out WorkOrderProgressState publishedOrder)
                        && publishedOrder.WorkOrderId == "work:000002";
                });
        ConstructionSite survivingSite = null;
        try
        {
            liveSite.SetGrid(liveGrid);
            liveSite.Initialization(building, new Vector2Int(1, 0));
            if (!liveGrid.RegisterOccupant(
                    liveSite,
                    GridLayer.Construction,
                    liveSite.buildPoses,
                    connectPositions: false))
            {
                return false;
            }
            if (!runtime.TryCreateConstructionOrder(
                    liveSite,
                    building,
                    liveSite.centerPos,
                    out string liveOrderId,
                    out _))
            {
                return false;
            }

            DungeonSaveSectionRegistry registry =
                new DungeonSaveSectionRegistry(
                    new IDungeonSaveSection[]
                    {
                        facilitySection,
                        physicalItems,
                        workOrders
                    },
                    rootStore,
                    new IDungeonRestoreTransactionParticipant[]
                    {
                        facilitySection,
                        runtime,
                        constructionInputs.Claims,
                        constructionInputs.Capacities,
                        lateProbe
                    });
            List<DungeonSaveSectionEnvelope> envelopes = registry.CaptureAll();
            DungeonSaveSectionEnvelope workEnvelope = envelopes.Single(
                envelope => envelope.sectionId == WorkOrdersSaveSection.Id);
            workEnvelope.payloadJson = JsonUtility.ToJson(
                CreateRestoredOrder(
                    building,
                    "work:000002",
                    3,
                    new Vector2Int(4, 0)));

            DungeonGameRestoreReport report = new DungeonGameRestoreReport();
            string expectedLiveJson = JsonUtility.ToJson(runtime.Capture());
            int detachedBefore = CountDetachedConstructionSites();
            bool restored = registry.RestoreAll(envelopes, report);
            ConstructionSite[] allSites =
                Resources.FindObjectsOfTypeAll<ConstructionSite>();
            bool incomingLeaked = allSites.Any(candidate => candidate != null
                && candidate.WorkOrderId == "work:000002");
            survivingSite = liveSite;
            DungeonWorkOrderSaveData captured = runtime.Capture();
            bool candidateIndexClear = !candidateIndex.TryGetGrid(out _)
                && !candidateIndex.TryGetBuildings(out _)
                && !candidateIndex.TryGetExteriorZones(out _);
            bool liveOrderPreserved = liveSite != null
                && runtime.TryGetOrderFor(
                    liveSite,
                    BuiltInWorkTypeIds.Construct,
                    out WorkOrderProgressState liveOrder)
                && string.Equals(
                    liveOrder.WorkOrderId,
                    liveOrderId,
                    StringComparison.Ordinal);
            string liveDestination =
                WorkConstructionInputOwnerAuthority.DestinationFor(
                    liveOrderId);
            bool liveInputAuthorityPreserved =
                constructionInputs.Claims.CaptureAuthorityClaims()
                    .Count(value => value != null && string.Equals(
                        value.DestinationId,
                        liveDestination,
                        StringComparison.Ordinal)) == 1
                && constructionInputs.Capacities.CaptureAuthorityProfiles()
                    .Count(value => value != null && string.Equals(
                        value.DestinationId,
                        liveDestination,
                        StringComparison.Ordinal)) == 1;
            bool passed = !restored
                && !report.Success
                && lateProbe.PublishCount == 1
                && lateProbe.PublishedStateWasReversible
                && lateProbe.RollbackCount == 1
                && lateProbe.CompleteCount == 0
                && !incomingLeaked
                && ReferenceEquals(survivingSite, liveSite)
                && liveOrderPreserved
                && liveInputAuthorityPreserved
                && liveSiteObject.activeSelf
                && liveGrid.GetGridCell(liveSite.centerPos)
                    ?.ContainsOccupant(
                        GridLayer.Construction,
                        liveSite) == true
                && captured.orders.Count == 1
                && captured.orders[0].workOrderId == liveOrderId
                && string.Equals(
                    expectedLiveJson,
                    JsonUtility.ToJson(captured),
                    StringComparison.Ordinal)
                && candidateIndexClear
                && !rootStore.IsRestoreStaging
                && rootStore.PublishedRestoreRevision == 0
                && CountDetachedConstructionSites() == detachedBefore;
            if (!passed)
            {
                Debug.LogWarning(
                    "Work-order late-failure diagnostic "
                    + $"restored={restored} success={report.Success} "
                    + $"errors={string.Join(" | ", report.Errors)} "
                    + $"publishes={lateProbe.PublishCount} incomingLeaked={incomingLeaked} "
                    + $"reversible={lateProbe.PublishedStateWasReversible} "
                    + $"rollbacks={lateProbe.RollbackCount} "
                    + $"completes={lateProbe.CompleteCount} "
                    + $"sameLive={ReferenceEquals(survivingSite, liveSite)} "
                    + $"liveOrder={liveOrderPreserved} "
                    + $"liveInputAuthority={liveInputAuthorityPreserved} "
                    + $"liveActive={liveSiteObject.activeSelf} "
                    + $"liveRegistered={liveGrid.GetGridCell(liveSite.centerPos)?.ContainsOccupant(GridLayer.Construction, liveSite) == true} "
                    + $"expected={expectedLiveJson} actual={JsonUtility.ToJson(captured)} "
                    + $"candidateClear={candidateIndexClear} "
                    + $"staging={rootStore.IsRestoreStaging} "
                    + $"revision={rootStore.PublishedRestoreRevision} "
                    + $"detachedBefore={detachedBefore} "
                    + $"detachedAfter={CountDetachedConstructionSites()}");
            }

            return passed;
        }
        finally
        {
            if (survivingSite != null)
            {
                UnityEngine.Object.DestroyImmediate(survivingSite.gameObject);
            }

            if (liveSiteObject != null)
            {
                UnityEngine.Object.DestroyImmediate(liveSiteObject);
            }

            UnityEngine.Object.DestroyImmediate(building);
        }
    }

    private static WorkOrderAggregateStateStore CreateStateStore()
    {
        return new WorkOrderAggregateStateStore(
            new DungeonRuntimeAggregateRootStore(),
            new RestoreWorldCandidateIndex());
    }

    private static WorkOrderExecutionServices CreateExecutionServices(
        IWorkforceReplanService workforce,
        DungeonStory.Foundation.IUiClock uiClock = null)
    {
        return new WorkOrderExecutionServices(
            workforce,
            new FixedGameClock(),
            uiClock ?? new FixedUiClock(),
            DisabledDungeonDebugRuleQuery.Instance);
    }

    private sealed class FixedGameClock :
        DungeonStory.Foundation.IGameClock
    {
        public float DeltaTime => 0.02f;
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private sealed class FixedUiClock :
        DungeonStory.Foundation.IUiClock
    {
        public float DeltaTime => 0.02f;
        public float Time => 0f;
    }

    private sealed class ManualUiClock :
        DungeonStory.Foundation.IUiClock
    {
        private float time;

        public float DeltaTime => 0.02f;
        public float Time => time;

        public void Advance(float seconds)
        {
            time += Mathf.Max(0f, seconds);
        }
    }

    private static int CountDetachedConstructionSites()
    {
        return Resources.FindObjectsOfTypeAll<ConstructionSite>()
            .Count(site => site != null && site.IsDetachedRestoreCandidate);
    }

    private static bool VerifyConstructionSiteParallelWorkerCapacity()
    {
        BuildingSO building = CreateTestBuilding(
            991_204,
            "Industrial capacity audit",
            2,
            2,
            constructionWork: 100f,
            materialAmount: 1);
        building.GetAbility<BuildingWorkAmountAbility>()
            .SetConstructionProjectScale(ProjectScale.IndustrialFacility);
        GameObject siteObject = new GameObject("WorkAmountParallelConstructionSite");
        ConstructionSite site = siteObject.AddComponent<ConstructionSite>();
        CharacterAiEditorTestDependencies.Inject(site);
        TestBuildingCharacterPort[] workers = Enumerable.Range(0, 5)
            .Select(index => new TestBuildingCharacterPort($"character:test:builder:{index}"))
            .ToArray();
        TestBuildingCharacterPort sameCharacterAdapter =
            new TestBuildingCharacterPort(workers[0].BuildingCharacterId.Value);
        try
        {
            site.Initialization(building, Vector2Int.zero);
            if (site.MaximumWorkers != 4)
                return false;

            if (!site.TryReserveWorker(
                    workers[0],
                    out FacilityAssignmentStatus firstIdentity,
                    seconds: 30f)
                || !firstIdentity.IsAllowed
                || !site.TryReserveWorker(
                    sameCharacterAdapter,
                    out FacilityAssignmentStatus adapterIdentity,
                    seconds: 30f)
                || !adapterIdentity.IsAllowed
                || site.OccupiedWorkerSlotCount != 1)
            {
                return false;
            }

            site.ReleaseWorkerReservation(sameCharacterAdapter);
            if (site.OccupiedWorkerSlotCount != 0)
            {
                return false;
            }

            for (int index = 0; index < 4; index++)
            {
                if (!site.TryReserveWorker(
                        workers[index],
                        out FacilityAssignmentStatus status,
                        seconds: 30f)
                    || !status.IsAllowed)
                {
                    return false;
                }
            }
            if (site.OccupiedWorkerSlotCount != 4
                || site.TryReserveWorker(
                    workers[4],
                    out FacilityAssignmentStatus overflow,
                    seconds: 30f)
                || overflow.IsAllowed)
            {
                return false;
            }

            site.ReleaseWorkerReservation(workers[1]);
            return site.OccupiedWorkerSlotCount == 3
                && site.TryReserveWorker(
                    workers[4],
                    out FacilityAssignmentStatus replacement,
                    seconds: 30f)
                && replacement.IsAllowed
                && site.OccupiedWorkerSlotCount == 4;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(siteObject);
            UnityEngine.Object.DestroyImmediate(building);
        }
    }

    private static BuildingSO CreateTestBuilding(
        int id,
        string objectName,
        int width,
        int height,
        float constructionWork,
        int materialAmount,
        bool addWorkAbility = true)
    {
        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        building.id = id;
        building.objectName = objectName;
        building.width = width;
        building.height = height;
        building.layer = GridLayer.Building;
        building.category = BuildingCategory.Shop;
        building.unlocked = true;
        if (addWorkAbility)
        {
            BuildingWorkAmountAbility workAmount = new BuildingWorkAmountAbility
            {
                constructionWorkRequired = Mathf.Max(0.1f, constructionWork),
                repairWorkRequired = 3f,
                cleanWorkRequired = 2f,
                researchWorkRequired = 6f
            };
            workAmount.SetConstructionMaterials(
                materialAmount > 0
                    ? new[]
                    {
                        new ItemAmountDefinition(
                            "material:lumber",
                            materialAmount)
                    }
                    : Array.Empty<ItemAmountDefinition>());
            building.AbilityModules.Add(workAmount);
        }

        return building;
    }

    private sealed class TestBuildingCharacterPort : IBuildingCharacterPort
    {
        public TestBuildingCharacterPort(string characterId)
        {
            BuildingCharacterId = (CharacterId)characterId;
        }

        public CharacterId BuildingCharacterId { get; }
        public string BuildingDisplayName => BuildingCharacterId.Value;
        public bool IsBuildingInteractionAvailable => true;
    }

    private sealed class CandidateFacilitySection :
        IDungeonSaveSection,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection,
        IDungeonRestoreTransactionParticipant
    {
        private readonly IRestoreWorldCandidatePublisher publisher;
        private readonly Grid candidateGrid;
        private bool transactionActive;
        private bool candidateReady;

        public CandidateFacilitySection(
            IRestoreWorldCandidatePublisher publisher,
            Grid candidateGrid)
        {
            this.publisher = publisher;
            this.candidateGrid = candidateGrid;
        }

        public string SectionId => ModularFacilityWorldSaveSection.Id;
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.World;
        public IReadOnlyList<string> DependsOn => Array.Empty<string>();
        public string ParticipantId => "100.world.facilities.test";

        public string Capture() => "{}";

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (transactionActive || candidateReady)
            {
                throw new InvalidOperationException(
                    "Facility test candidate staging is already active.");
            }

            publisher.SetFacilityCandidate(
                candidateGrid,
                Array.Empty<BuildableObject>());
            candidateReady = true;
            return new DungeonCandidateSaveRestoreStage<
                CandidateFacilityRestoreCandidate>(
                SectionId,
                new CandidateFacilityRestoreCandidate(this),
                StagePreparedCandidate);
        }

        private void StagePreparedCandidate(
            CandidateFacilityRestoreCandidate candidate)
        {
            if (!transactionActive || !candidateReady)
            {
                throw new InvalidOperationException(
                    "Facility test candidate transaction is inactive or unprepared.");
            }

            candidate.Take(this);
        }

        private void DiscardPreparedCandidate()
        {
            publisher.ClearFacilityCandidate();
            candidateReady = false;
        }

        public void BeginRestoreCandidate()
        {
            transactionActive = true;
        }

        public void PublishRestoreCandidate()
        {
            if (!transactionActive || !candidateReady)
            {
                throw new InvalidOperationException(
                    "Facility test candidate is not ready.");
            }

            publisher.ClearFacilityCandidate();
            transactionActive = false;
            candidateReady = false;
        }

        public void DiscardRestoreCandidate()
        {
            publisher.ClearFacilityCandidate();
            transactionActive = false;
            candidateReady = false;
        }

        private sealed class CandidateFacilityRestoreCandidate :
            IDungeonDiscardableRestoreCandidate
        {
            private CandidateFacilitySection owner;

            internal CandidateFacilityRestoreCandidate(
                CandidateFacilitySection owner)
            {
                this.owner = owner
                    ?? throw new ArgumentNullException(nameof(owner));
            }

            internal void Take(CandidateFacilitySection expectedOwner)
            {
                if (!ReferenceEquals(owner, expectedOwner))
                {
                    throw new InvalidOperationException(
                        "Facility test candidate has the wrong owner or was already consumed.");
                }

                owner = null;
            }

            public void Discard()
            {
                CandidateFacilitySection current = owner;
                owner = null;
                current?.DiscardPreparedCandidate();
            }
        }
    }

    private sealed class PassiveSaveSection :
        IDungeonSaveSection,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private readonly string sectionId;
        private readonly DungeonSaveRestorePhase restorePhase;

        public PassiveSaveSection(
            string sectionId,
            DungeonSaveRestorePhase restorePhase)
        {
            this.sectionId = sectionId;
            this.restorePhase = restorePhase;
        }

        public string SectionId => sectionId;
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase => restorePhase;
        public IReadOnlyList<string> DependsOn => Array.Empty<string>();
        public string Capture() => "{}";

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            return new DungeonDelegateSaveRestoreStage(SectionId, _ => { });
        }
    }

    private sealed class WorkOrderRestoreLifecycleProbe :
        IDungeonRestoreTransactionParticipant
    {
        private readonly bool failAfterPublish;
        private readonly Func<bool> verifyPublishedState;
        private bool transactionActive;

        internal WorkOrderRestoreLifecycleProbe(
            bool failAfterPublish,
            Func<bool> verifyPublishedState)
        {
            this.failAfterPublish = failAfterPublish;
            this.verifyPublishedState = verifyPublishedState
                ?? throw new ArgumentNullException(nameof(verifyPublishedState));
        }

        public string ParticipantId => "999.test.work-orders.lifecycle-probe";
        public int PublishCount { get; private set; }
        public int RollbackCount { get; private set; }
        public int CompleteCount { get; private set; }
        public bool PublishedStateWasReversible { get; private set; }

        public void BeginRestoreCandidate()
        {
            transactionActive = true;
        }

        public void PublishRestoreCandidate()
        {
            if (!transactionActive)
            {
                throw new InvalidOperationException(
                    "Injected participant was not started.");
            }

            PublishCount++;
            PublishedStateWasReversible = verifyPublishedState();
            if (failAfterPublish)
            {
                throw new InvalidOperationException(
                    "Injected failure after work-order publication.");
            }
        }

        public void RollbackPublishedRestoreCandidate()
        {
            if (!transactionActive)
            {
                throw new InvalidOperationException(
                    "Injected participant has no publication to roll back.");
            }

            RollbackCount++;
            transactionActive = false;
        }

        public void CompleteRestoreCandidate()
        {
            if (!transactionActive)
            {
                throw new InvalidOperationException(
                    "Injected participant has no publication to complete.");
            }

            CompleteCount++;
            transactionActive = false;
        }

        public void DiscardRestoreCandidate()
        {
            transactionActive = false;
        }
    }

    private sealed class SingleBuildingLookup : IBuildingDefinitionLookup
    {
        private readonly BuildingSO building;

        public SingleBuildingLookup(BuildingSO building)
        {
            this.building = building;
        }

        public BuildingSO GetBuilding(int id)
        {
            return building != null && building.id == id ? building : null;
        }
    }

    /// <summary>
    /// Wires the production construction input-owner runtime to the same paired
    /// destination-claim and gram-capacity authorities used by gameplay. Tests
    /// may keep their lightweight physical world, but may not bypass the owner
    /// contract that production restore and construction creation require.
    /// </summary>
    private sealed class ConstructionInputOwnerFixture
    {
        internal ConstructionInputOwnerFixture(
            FakeWorldItemStackRuntime items,
            params BuildableObject[] buildings)
            : this(items, (IEnumerable<BuildableObject>)buildings)
        {
        }

        internal ConstructionInputOwnerFixture(
            FakeWorldItemStackRuntime items,
            IEnumerable<BuildableObject> buildings)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            FixedConstructionMassQuery mass = new();
            Claims = new FacilityBufferDestinationClaimRegistry();
            Capacities = new FacilityBufferMassAdmissionService(
                Claims,
                EmptyFacilityBufferOccupancy.Instance,
                mass);
            FacilityBufferDestinationLifecycleService lifecycle = new(
                Claims,
                Claims,
                Capacities,
                Capacities);
            Runtime = new WorkConstructionInputOwnerRuntime(
                mass,
                items,
                new FixedBuildingWorldQuery(buildings),
                Claims,
                Capacities,
                lifecycle,
                new FixtureFacilityBufferRelease(items));
        }

        internal WorkConstructionInputOwnerRuntime Runtime { get; }
        internal FacilityBufferDestinationClaimRegistry Claims { get; }
        internal FacilityBufferMassAdmissionService Capacities { get; }
    }

    private sealed class FixedBuildingWorldQuery : IBuildingWorldQuery
    {
        private readonly IEnumerable<BuildableObject> buildings;

        internal FixedBuildingWorldQuery(
            IEnumerable<BuildableObject> buildings)
        {
            this.buildings = buildings ?? Array.Empty<BuildableObject>();
        }

        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings => buildings
            .Where(value => value != null)
            .ToArray();
    }

    private sealed class FixedConstructionMassQuery : IPhysicalItemMassQuery
    {
        private const long UnitMassGrams = 1_000L;

        public long AuthorityRevision => 1L;

        public PhysicalMassGrams GetDefinitionUnitMass(
            ItemDefinitionId itemId) => new(UnitMassGrams);

        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) => new(UnitMassGrams);

        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) => new(UnitMassGrams);

        public PhysicalMassGrams GetStackTotalMass(
            PhysicalItemLotSnapshot lot) => new(checked(
            UnitMassGrams * lot.Quantity));

        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => new(checked(UnitMassGrams * quantity));
    }

    private sealed class EmptyFacilityBufferOccupancy :
        IFacilityBufferPhysicalOccupancyQuery
    {
        internal static readonly EmptyFacilityBufferOccupancy Instance = new();

        public FacilityBufferPhysicalOccupancySnapshot Capture(
            string destinationId) => new(0L, 0L);

        public bool TryCaptureExactLot(
            IReadOnlyList<FacilityBufferMassLotSlice> slices,
            out FacilityBufferExactLotSnapshot lot,
            out string failureReason)
        {
            lot = default;
            failureReason = "work-amount-fixture-has-no-admission-lot";
            return false;
        }
    }

    private sealed class FixtureFacilityBufferRelease :
        IFacilityBufferDestinationReleaseService
    {
        private readonly FakeWorldItemStackRuntime items;

        internal FixtureFacilityBufferRelease(
            FakeWorldItemStackRuntime items)
        {
            this.items = items
                ?? throw new ArgumentNullException(nameof(items));
        }

        public bool TryReleaseAtOwnerPosition(
            string destinationId,
            Vector2Int ownerPosition,
            string reasonCode,
            out int releasedQuantity,
            out string failureReason)
        {
            if (string.IsNullOrWhiteSpace(destinationId)
                || string.IsNullOrWhiteSpace(reasonCode))
            {
                releasedQuantity = 0;
                failureReason = "work-amount-fixture-release-invalid";
                return false;
            }

            releasedQuantity = items.ReleaseStacksByDestination(
                destinationId,
                ownerPosition);
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FakeRepairCompletionDeliveryCommand :
        IWorkCompletionIdentityDeliveryCommand
    {
        private readonly List<WorkCompletionIdentityDeliveryRequest> requests =
            new();

        internal WorkCompletionIdentityDeliveryStatus Status { get; set; } =
            WorkCompletionIdentityDeliveryStatus.Deferred;

        internal IReadOnlyList<WorkCompletionIdentityDeliveryRequest> Requests =>
            requests;

        public WorkCompletionIdentityDeliveryResult EnsureApplied(
            WorkCompletionIdentityDeliveryRequest request)
        {
            requests.Add(request);
            // This is a controlled port acknowledgement only; it is not evidence
            // that the real identity ledger applied the request.
            return Status == WorkCompletionIdentityDeliveryStatus.Applied
                ? new WorkCompletionIdentityDeliveryResult(Status)
                : new WorkCompletionIdentityDeliveryResult(
                    Status,
                    "work-amount-fixture-identity-deferred");
        }

        public bool RetireProducerStream(string producerStreamId) => false;
    }

    private sealed class ScenarioObjectResolver : IObjectResolver
    {
        private readonly IReadOnlyList<object> services;
        private readonly IDomainFactPublisher domainFacts;

        public ScenarioObjectResolver(params object[] services)
        {
            this.services = (services ?? Array.Empty<object>())
                .Where(value => value != null)
                .ToArray();
            domainFacts = this.services
                .OfType<IDomainFactPublisher>()
                .FirstOrDefault();
        }

        public object ApplicationOrigin => null;
        public DiagnosticsCollector Diagnostics { get; set; }

        public object Resolve(Type type, object key = null)
        {
            if (TryResolve(type, out object resolved, key))
                return resolved;
            throw new InvalidOperationException(
                $"Work-order scenario cannot resolve {type?.Name ?? "null"}.");
        }

        public bool TryResolve(
            Type type,
            out object resolved,
            object key = null)
        {
            if (type == typeof(IDomainFactPublisher)
                || type == typeof(IDomainFactRecoveryPublisher))
            {
                resolved = type.IsInstanceOfType(domainFacts)
                    ? domainFacts
                    : null;
                return resolved != null;
            }
            resolved = services.FirstOrDefault(value =>
                type != null && type.IsInstanceOfType(value));
            return resolved != null;
        }

        public object Resolve(Registration registration)
        {
            throw new InvalidOperationException(
                "Work-order scenario does not resolve registrations.");
        }

        public IScopedObjectResolver CreateScope(
            Action<IContainerBuilder> installation = null)
        {
            throw new InvalidOperationException(
                "Work-order scenario does not create scopes.");
        }

        public void Inject(object instance)
        {
            if (instance is BuildableObject building)
            {
                CharacterAiEditorTestDependencies.Inject(building);
            }
        }

        public bool TryGetRegistration(
            Type type,
            out Registration registration,
            object key = null)
        {
            registration = null;
            return false;
        }

        public void Dispose()
        {
        }
    }

    private sealed class NoGridProvider : IGridSystemProvider
    {
        public GridSystemManager Manager => null;
        public Grid Grid => null;

        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }

        public bool TryGetGrid(out Grid grid)
        {
            grid = null;
            return false;
        }
    }

    private sealed class EmptyPhysicalItemRestoreCandidateQuery :
        IPhysicalItemRestoreCandidateQuery
    {
        internal static readonly EmptyPhysicalItemRestoreCandidateQuery Instance =
            new EmptyPhysicalItemRestoreCandidateQuery();

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            PendingBatchDispositions =>
                Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>();

        public bool TryGetPendingBatchDisposition(
            string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot disposition)
        {
            disposition = null;
            return false;
        }
    }

    private sealed class FixedGridProvider : IGridSystemProvider
    {
        public FixedGridProvider(Grid grid)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        public GridSystemManager Manager => null;
        public Grid Grid { get; }

        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }

        public bool TryGetGrid(out Grid grid)
        {
            grid = Grid;
            return true;
        }
    }

    private sealed class FakeMaterialSalvageCalculator :
        IMaterialSalvageCalculator
    {
        private readonly IReadOnlyList<ItemAmountDefinition> recovered;

        public FakeMaterialSalvageCalculator(
            params ItemAmountDefinition[] recovered)
        {
            this.recovered = (recovered
                    ?? Array.Empty<ItemAmountDefinition>())
                .Where(value => value != null && value.Amount > 0)
                .OrderBy(value => value.ItemId, StringComparer.Ordinal)
                .ToArray();
        }

        public MaterialSalvageResult Calculate(
            DismantleTargetKind targetKind,
            float originalWork,
            IEnumerable<ItemAmountDefinition> originalInputs,
            float workerSkill) => new(
                Mathf.Max(0.1f, originalWork * 0.25f),
                recovered);
    }

    private sealed class FakeDeferredBuildingDestructiveLoss :
        IBuildingDestructiveLossRuntime
    {
        private BuildableObject pending;

        public int ApplyCount { get; private set; }

        public BuildingDestructiveLossResult Apply(
            BuildableObject building,
            ProductionFacilityDestructiveDrainCause cause)
        {
            if (building == null
                || cause != ProductionFacilityDestructiveDrainCause
                    .ExplicitDemolition)
            {
                return new BuildingDestructiveLossResult(
                    BuildingDestructiveLossDisposition.Conflict,
                    "invalid deferred demolition fixture request");
            }
            ApplyCount++;
            if (pending != null && !ReferenceEquals(pending, building))
            {
                return new BuildingDestructiveLossResult(
                    BuildingDestructiveLossDisposition.Conflict,
                    "another deferred demolition is pending");
            }
            pending = building;
            return new BuildingDestructiveLossResult(
                BuildingDestructiveLossDisposition.DeferredAccepted,
                "injected deferred demolition");
        }

        public void CompleteRemoval()
        {
            BuildableObject building = pending
                ?? throw new InvalidOperationException(
                    "No deferred demolition is pending.");
            pending = null;
            building.DestroySelf();
        }
    }

    private static bool VerifyDeferredDismantleCancellationRecovery()
    {
        const string orderId = "work:000001";
        const string pipelineId = "quality:work:000001";
        Vector2Int position = new Vector2Int(3, 1);
        BuildingSO building = CreateTestBuilding(
            991_205,
            "Deferred dismantle recovery",
            1,
            1,
            constructionWork: 4f,
            materialAmount: 2);
        Grid grid = new Grid(12, 4);
        GameObject buildingObject = new GameObject(
            "DeferredDismantleRecoveryBuilding");
        BuildableObject live = buildingObject.AddComponent<BuildableObject>();
        CharacterAiEditorTestDependencies.Inject(live);
        live.RestorePersistentIdentity(
            (BuildingInstanceId)"building:test:deferred-dismantle");
        live.SetGrid(grid);
        live.Initialization(building, position);
        if (!grid.RegisterOccupant(
                live,
                building.layer,
                building.GetGridPosList(position),
                building.Placement.IsMovement))
        {
            UnityEngine.Object.DestroyImmediate(buildingObject);
            UnityEngine.Object.DestroyImmediate(building);
            return false;
        }

        RestoreWorldCandidateIndex candidateIndex =
            new RestoreWorldCandidateIndex();
        WorkOrderAggregateStateStore stateStore =
            new WorkOrderAggregateStateStore(
                new DungeonRuntimeAggregateRootStore(),
                candidateIndex);
        FakeWorldItemStackRuntime items = new FakeWorldItemStackRuntime
        {
            AllowLooseSpawns = true
        };
        FakeMaterialSalvageCalculator salvage =
            new FakeMaterialSalvageCalculator(
                new ItemAmountDefinition("material:lumber", 2));
        ScenarioObjectResolver resolver =
            new ScenarioObjectResolver(salvage);
        FixedGridProvider gridProvider = new FixedGridProvider(grid);
        ManualUiClock uiClock = new ManualUiClock();
        ConstructionInputOwnerFixture constructionInputs = new(items, live);
        WorkOrderRuntime runtime = new WorkOrderRuntime(
            gridProvider,
            items,
            new SingleBuildingLookup(building),
            resolver,
            CreateExecutionServices(
                new TrackingWorkforceReplanService(),
                uiClock),
            stateStore,
            new FakePhysicalItemSourcePublicationService(items),
            constructionInputs.Runtime);
        FakeDeferredBuildingDestructiveLoss destructiveLoss =
            new FakeDeferredBuildingDestructiveLoss();
        GridBuildingPlacementService placement =
            new GridBuildingPlacementService(
                grid,
                null,
                id => id == building.id ? building : null,
                new GridBuildingFactory(new GridBuildingObjectFactory()),
                new BuildingPlacementValidator(),
                runtime,
                _ => { },
                warehouseLifecycle: null,
                destructiveLoss: destructiveLoss);

        CraftQualityRollSaveData roll = new CraftQualityRollSaveData
        {
            attemptIndex = 0,
            randomA = 0,
            randomB = 0,
            randomC = 0
        };
        DungeonWorkOrderSaveData initial = new DungeonWorkOrderSaveData
        {
            nextOrderSequence = 2,
            orders = new List<WorkOrderSaveData>
            {
                new WorkOrderSaveData
                {
                    workOrderId = orderId,
                    workTypeId = BuiltInWorkTypeIds.Dismantle.Value,
                    targetBuildingId = building.id,
                    gridX = position.x,
                    gridY = position.y,
                    requiredWork = 1f,
                    completedWork = 0f,
                    qualityRoll = roll.Clone(),
                    qualityPipelineId = pipelineId,
                    qualityAttemptIndex = 0,
                    status = WorkOrderStatus.Ready
                }
            },
            qualityPipelines = new List<QualityTargetPipelineSaveData>
            {
                new QualityTargetPipelineSaveData
                {
                    pipelineId = pipelineId,
                    definitionId = building.id.ToString(),
                    facilityPipeline = true,
                    requiredAcceptedCount = 1,
                    maximumAttempts = 2,
                    attemptIndex = 0,
                    currentRoll = roll.Clone(),
                    stage = QualityTargetPipelineStage.Dismantling,
                    footprintX = position.x,
                    footprintY = position.y,
                    footprintWidth = 1,
                    footprintHeight = 1
                }
            }
        };

        try
        {
            PublishWorkOrderSnapshot(
                runtime,
                candidateIndex,
                grid,
                new[] { live },
                initial);
            bool completionAccepted = runtime.DebugCompleteOrder(
                orderId,
                out string completionMessage);
            if (!completionAccepted || destructiveLoss.ApplyCount != 1)
            {
                throw new InvalidOperationException(
                    "Deferred dismantle was not accepted exactly once: accepted="
                    + completionAccepted + "; applyCount="
                    + destructiveLoss.ApplyCount + "; message="
                    + completionMessage);
            }

            DungeonWorkOrderSaveData deferred = runtime.Capture();
            WorkOrderSaveData pending = deferred.orders.SingleOrDefault();
            string expectedOperation =
                ProductionFacilityDestructiveDrainOperationId.FromFacility(
                    live.PersistentInstanceId).Value;
            bool capturedRetained =
                ProductionFacilityDestructiveDrainOperationId.TryParse(
                    expectedOperation,
                    out ProductionFacilityDestructiveDrainOperationId
                        retainedOperation)
                && runtime.TryCaptureRetention(
                    retainedOperation,
                    out WorkOrderDestructiveDrainRetentionSnapshot
                        retainedSnapshot,
                    out _)
                && retainedSnapshot.HasOwner
                && retainedSnapshot.OwnerIds.Count == 1
                && string.Equals(
                    retainedSnapshot.OwnerIds[0],
                    orderId,
                    StringComparison.Ordinal);
            if (pending == null
                || pending.status != WorkOrderStatus.Blocked
                || pending.facilityRemovedForRetry
                || !capturedRetained
                || !string.Equals(
                    pending.destructiveDrainOperationId,
                    expectedOperation,
                    StringComparison.Ordinal)
                || deferred.qualityPipelines.Single().stage
                    != QualityTargetPipelineStage.Recovering
                || !runtime.CancelQualityPipeline(pipelineId, out _))
            {
                throw new InvalidOperationException(
                    "Deferred dismantle did not persist/cancel canonically: order="
                    + (pending == null ? "missing" : pending.status.ToString())
                    + "; removed=" + (pending?.facilityRemovedForRetry ?? false)
                    + "; operation="
                    + (pending?.destructiveDrainOperationId ?? "missing")
                    + "; expected=" + expectedOperation + "; pipeline="
                    + deferred.qualityPipelines.Single().stage);
            }

            DungeonWorkOrderSaveData cancelledPending = runtime.Capture();
            WorkOrderSaveData cancelledOrder =
                cancelledPending.orders.SingleOrDefault();
            if (cancelledOrder == null
                || !cancelledOrder.cancelRebuildAfterDestructiveDrain
                || cancelledOrder.facilityRemovedForRetry
                || cancelledPending.qualityPipelines.Single().stage
                    != QualityTargetPipelineStage.Cancelled)
            {
                throw new InvalidOperationException(
                    "Cancelled deferred dismantle lost its salvage owner: order="
                    + (cancelledOrder == null ? "missing" : cancelledOrder.status.ToString())
                    + "; cancelCleanup="
                    + (cancelledOrder?.cancelRebuildAfterDestructiveDrain ?? false)
                    + "; removed="
                    + (cancelledOrder?.facilityRemovedForRetry ?? false)
                    + "; pipeline="
                    + cancelledPending.qualityPipelines.Single().stage);
            }

            // Re-publish the current-format checkpoint to prove the event
            // observation is rebuilt from the exact operation/facility join.
            PublishWorkOrderSnapshot(
                runtime,
                candidateIndex,
                grid,
                new[] { live },
                cancelledPending);
            runtime.Tick();
            destructiveLoss.CompleteRemoval();
            uiClock.Advance(1f);
            runtime.Tick();
            uiClock.Advance(1f);
            runtime.Tick();

            int recovered = items.GetAllStacks()
                .Where(stack => stack != null
                    && stack.State == WorldItemStackState.Loose
                    && string.Equals(
                        stack.ItemId,
                        "material:lumber",
                        StringComparison.Ordinal))
                .Sum(stack => stack.Quantity);
            DungeonWorkOrderSaveData completed = runtime.Capture();
            bool capturedReleased = runtime.TryCaptureRetention(
                    retainedOperation,
                    out WorkOrderDestructiveDrainRetentionSnapshot
                        releasedSnapshot,
                    out _)
                && !releasedSnapshot.HasOwner;
            bool worldRemoved = !grid.GetGridCell(position)
                .GetAllOccupants()
                .OfType<BuildableObject>()
                .Any(value => value != null
                    && value.id == building.id);
            if (recovered != 2
                || completed.orders.Count != 0
                || completed.qualityPipelines.Count != 1
                || completed.qualityPipelines[0].stage
                    != QualityTargetPipelineStage.Cancelled
                || !capturedReleased
                || !worldRemoved)
            {
                throw new InvalidOperationException(
                    "Deferred dismantle salvage did not close exactly once: recovered="
                    + recovered + "; orders=" + completed.orders.Count
                    + "; pipelines=" + completed.qualityPipelines.Count
                    + "; stage="
                    + (completed.qualityPipelines.Count == 1
                        ? completed.qualityPipelines[0].stage.ToString()
                        : "missing")
                    + "; worldRemoved=" + worldRemoved);
            }
            return true;
        }
        finally
        {
            if (candidateIndex.TryGetGrid(out _))
                candidateIndex.ClearFacilityCandidate();
            if (live != null)
                UnityEngine.Object.DestroyImmediate(live.gameObject);
            UnityEngine.Object.DestroyImmediate(building);
        }
    }

    private static bool VerifyDestructiveDrainSaveContract()
    {
        DungeonWorkOrderSaveData valid =
            CreateDestructiveDrainValidationPayload(
                BuiltInWorkTypeIds.Dismantle.Value,
                includeOperation: true,
                removed: true,
                cancelledCleanup: true);
        if (!TryValidateWorkOrderPayload(valid))
            return false;

        DungeonWorkOrderSaveData wrongWorkType =
            CreateDestructiveDrainValidationPayload(
                BuiltInWorkTypeIds.Repair.Value,
                includeOperation: true,
                removed: true,
                cancelledCleanup: false);
        DungeonWorkOrderSaveData missingOperation =
            CreateDestructiveDrainValidationPayload(
                BuiltInWorkTypeIds.Dismantle.Value,
                includeOperation: false,
                removed: true,
                cancelledCleanup: true);
        return !TryValidateWorkOrderPayload(wrongWorkType)
            && !TryValidateWorkOrderPayload(missingOperation);
    }

    private static bool TryValidateWorkOrderPayload(
        DungeonWorkOrderSaveData payload)
    {
        FakeWorldItemStackRuntime items = new FakeWorldItemStackRuntime();
        ConstructionInputOwnerFixture constructionInputs = new(items);
        WorkOrderRuntime runtime = new WorkOrderRuntime(
            new NoGridProvider(),
            items,
            new SingleBuildingLookup(null),
            new ScenarioObjectResolver(),
            CreateExecutionServices(new TrackingWorkforceReplanService()),
            CreateStateStore(),
            new FakePhysicalItemSourcePublicationService(items),
            constructionInputs.Runtime);
        try
        {
            runtime.ValidateRestorePayload(payload);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static DungeonWorkOrderSaveData
        CreateDestructiveDrainValidationPayload(
            string workTypeId,
            bool includeOperation,
            bool removed,
            bool cancelledCleanup)
    {
        const string pipelineId = "quality:work:000001";
        CraftQualityRollSaveData roll = new CraftQualityRollSaveData
        {
            attemptIndex = 0
        };
        return new DungeonWorkOrderSaveData
        {
            nextOrderSequence = 2,
            orders = new List<WorkOrderSaveData>
            {
                new WorkOrderSaveData
                {
                    workOrderId = "work:000001",
                    workTypeId = workTypeId,
                    targetBuildingId = 991_205,
                    requiredWork = 1f,
                    completedWork = 1f,
                    qualityRoll = roll.Clone(),
                    qualityPipelineId = pipelineId,
                    destructiveDrainOperationId = includeOperation
                        ? "production-facility-destructive-drain:building:test:save-contract"
                        : string.Empty,
                    facilityRemovedForRetry = removed,
                    cancelRebuildAfterDestructiveDrain = cancelledCleanup,
                    status = WorkOrderStatus.Blocked
                }
            },
            qualityPipelines = new List<QualityTargetPipelineSaveData>
            {
                new QualityTargetPipelineSaveData
                {
                    pipelineId = pipelineId,
                    definitionId = "991205",
                    facilityPipeline = true,
                    currentRoll = roll.Clone(),
                    requiredAcceptedCount = 1,
                    maximumAttempts = 2,
                    stage = cancelledCleanup
                        ? QualityTargetPipelineStage.Cancelled
                        : QualityTargetPipelineStage.Recovering
                }
            }
        };
    }

    private static void PublishWorkOrderSnapshot(
        WorkOrderRuntime runtime,
        RestoreWorldCandidateIndex candidateIndex,
        Grid grid,
        IReadOnlyList<BuildableObject> buildings,
        DungeonWorkOrderSaveData snapshot)
    {
        runtime.BeginRestoreCandidate();
        candidateIndex.SetFacilityCandidate(grid, buildings);
        try
        {
            WorkOrderRestoreCandidate candidate =
                runtime.PrepareRestoreCandidate(snapshot);
            runtime.PublishRestoreCandidate(candidate);
            runtime.PublishRestoreCandidate();
            runtime.CompleteRestoreCandidate();
        }
        catch
        {
            runtime.DiscardRestoreCandidate();
            throw;
        }
        finally
        {
            candidateIndex.ClearFacilityCandidate();
        }
    }

    private static bool VerifyRestitutionOutputRestorePreflight()
    {
        const string orderId = "work:000321";
        string restitutionOperation =
            WorkOrderMaterialDebugContract.RestitutionOperationId(orderId);
        DungeonWorkOrderSaveData payload = new()
        {
            orders = new List<WorkOrderSaveData>
            {
                new()
                {
                    workOrderId = orderId,
                    gridX = 4,
                    gridY = 7,
                    itemMaterials = new List<WorkOrderItemMaterialSaveData>
                    {
                        new()
                        {
                            itemId = "material:lumber",
                            required = 2,
                            delivered = 2
                        },
                        new()
                        {
                            itemId = "material:steel-ingot",
                            required = 1,
                            delivered = 1
                        }
                    },
                    materialTransfer = new WorkOrderMaterialTransferSaveData
                    {
                        phase = WorkOrderMaterialTransferPhase.RestitutionPending,
                        inputQuantity = 3,
                        inputMassGrams = 3000L,
                        restitutionOperationId = restitutionOperation
                    }
                }
            }
        };
        PhysicalItemRestoreCandidateOutputSnapshot lumber = new(
            "physical-source:" + restitutionOperation
                + ":material:lumber:2:2000",
            "stack:restitution:lumber",
            "material:lumber",
            2,
            2000L,
            WorldItemStackState.Loose,
            new Vector2Int(4, 7),
            string.Empty);
        WorkOrdersSaveSection.ValidateRestitutionOutputCandidate(
            payload,
            new OutputCandidateQuery(lumber));
        PhysicalItemRestoreCandidateOutputSnapshot steel = new(
            "physical-source:" + restitutionOperation
                + ":material:steel-ingot:1:1000",
            "stack:restitution:steel",
            "material:steel-ingot",
            1,
            1000L,
            WorldItemStackState.Loose,
            new Vector2Int(4, 7),
            string.Empty);
        WorkOrdersSaveSection.ValidateRestitutionOutputCandidate(
            payload,
            new OutputCandidateQuery(lumber, steel));

        bool tamperRejected = false;
        try
        {
            WorkOrdersSaveSection.ValidateRestitutionOutputCandidate(
                payload,
                new OutputCandidateQuery(
                    lumber,
                    new PhysicalItemRestoreCandidateOutputSnapshot(
                        steel.CommitId,
                        steel.StackId,
                        steel.ItemId,
                        steel.Quantity,
                        999L,
                        steel.State,
                        steel.Position,
                        steel.DestinationId)));
        }
        catch (InvalidOperationException)
        {
            tamperRejected = true;
        }
        return tamperRejected;
    }

    private sealed class CandidateQuery : IPhysicalItemRestoreCandidateQuery
    {
        internal static readonly CandidateQuery Empty = new CandidateQuery();
        private readonly IReadOnlyList<
            PhysicalItemRestoreCandidateDispositionSnapshot> values;

        internal CandidateQuery(
            params PhysicalItemRestoreCandidateDispositionSnapshot[] values)
        {
            this.values = values
                ?? Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>();
        }

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            PendingBatchDispositions => values;

        public bool TryGetPendingBatchDisposition(
            string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot disposition)
        {
            disposition = values.FirstOrDefault(value => string.Equals(
                value.OperationId,
                operationId,
                StringComparison.Ordinal));
            return disposition != null;
        }
    }

    private sealed class OutputCandidateQuery :
        IPhysicalItemRestoreCandidateOutputQuery
    {
        private readonly IReadOnlyList<
            PhysicalItemRestoreCandidateOutputSnapshot> values;

        internal OutputCandidateQuery(
            params PhysicalItemRestoreCandidateOutputSnapshot[] values)
        {
            this.values = (values
                    ?? Array.Empty<
                        PhysicalItemRestoreCandidateOutputSnapshot>())
                .OrderBy(value => value.CommitId, StringComparer.Ordinal)
                .ThenBy(value => value.StackId, StringComparer.Ordinal)
                .ToArray();
        }

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot>
            CommittedOutputs => values;

        public bool TryGetCommittedOutput(
            string commitId,
            out IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot> outputs)
        {
            outputs = values.Where(value => string.Equals(
                    value.CommitId,
                    commitId,
                    StringComparison.Ordinal))
                .ToArray();
            return outputs.Count > 0;
        }
    }

    private sealed class FakePhysicalItemSourcePublicationService :
        IPhysicalItemSourcePublicationService
    {
        private readonly FakeWorldItemStackRuntime items;

        internal FakePhysicalItemSourcePublicationService(
            FakeWorldItemStackRuntime items)
        {
            this.items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public bool TryEnsureLooseOutputs(
            IReadOnlyDictionary<string, int> outputs,
            Vector2Int outputPosition,
            string operationId,
            string reasonCode,
            out PhysicalItemSourcePublicationReceipt receipt,
            out string failureReason)
        {
            List<string> commits = new();
            int quantity = 0;
            foreach (KeyValuePair<string, int> output in outputs
                         .OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                string commit = $"physical-source:{operationId}:{output.Key}:"
                    + $"{output.Value}:{output.Value * 1000L}";
                if (!items.EnsureCommittedLooseOutput(
                        output.Key,
                        output.Value,
                        outputPosition,
                        commit))
                {
                    receipt = default;
                    failureReason = "fake physical source publication failed";
                    return false;
                }
                commits.Add(commit);
                quantity += output.Value;
            }
            receipt = new PhysicalItemSourcePublicationReceipt(
                operationId,
                reasonCode,
                commits,
                quantity,
                quantity * 1000L);
            failureReason = string.Empty;
            return receipt.IsCommitted;
        }
    }

    private sealed class FakeWorldItemStackRuntime : IWorldItemStackRuntime
    {
        private readonly IDungeonItemCatalogProvider catalogProvider =
            EditorItemCatalogFactory.Create();

        public bool SpawnItemAtWithComponents(
            string itemId,
            int amount,
            Vector2Int position,
            WorldItemStackState state,
            string destinationId,
            IReadOnlyList<ItemInstanceComponentSaveData> components,
            out int spawned)
        {
            spawned = Mathf.Max(0, amount);
            if (spawned <= 0)
            {
                return false;
            }
            stacks.Add(new WorldItemStackSnapshot
            {
                StackId = $"fake-component-output:{stacks.Count + 1}",
                ItemId = itemId ?? string.Empty,
                Quantity = spawned,
                State = state,
                Position = position,
                DestinationId = destinationId ?? string.Empty,
                Components = (components ?? Array.Empty<ItemInstanceComponentSaveData>())
                    .Select(value => value?.Clone())
                    .Where(value => value != null)
                    .ToArray()
            });
            return true;
        }
        public bool TryRemoveInstanceComponent(string stackId, string componentTypeId) => false;
        private readonly Dictionary<string, Dictionary<StockCategory, int>> buffers =
            new Dictionary<string, Dictionary<StockCategory, int>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, int>> itemBuffers =
            new Dictionary<string, Dictionary<string, int>>(
                StringComparer.Ordinal);
        private readonly List<WorldItemStackSnapshot> stacks =
            new List<WorldItemStackSnapshot>();
        private readonly Dictionary<string, PhysicalItemBatchDispositionReceipt>
            pendingDispositions = new(StringComparer.Ordinal);

        public readonly Dictionary<StockCategory, int> Requested = new Dictionary<StockCategory, int>();
        public readonly Dictionary<string, int> RequestedItems =
            new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly HashSet<string> PrioritizedStackIds =
            new HashSet<string>(StringComparer.Ordinal);
        public int ReleasedQuantity { get; private set; }
        public int PhysicalCommitCount { get; private set; }
        public int PhysicalAcknowledgementCount { get; private set; }
        public bool FailNextAcknowledgement { get; set; }
        public bool AllowLooseSpawns { get; set; }

        public IDungeonItemCatalogProvider CatalogProvider => catalogProvider;
        public IPhysicalItemMassQuery MassQuery => null;
        public IItemHaulingSettingsProvider HaulingSettingsProvider => null;
        public bool StoredItemMarkersVisible => false;
        public int ItemStackVersion => 0;
        public int HaulJobVersion => 0;
        public int GetCommittedHaulDeliveryQuantity(
            string destinationId,
            string itemId) => 0;
        public long GetCommittedHaulDeliveryMassGrams(string destinationId) => 0L;
        public bool TryCommitHaulPickup(
            string ownerOperationId,
            CharacterCarryInventory inventory,
            out string failureReason)
        {
            failureReason = "fake haul delivery authority unavailable";
            return false;
        }
        public bool TryCaptureHaulDeliveryIntent(
            string ownerOperationId,
            out HaulDeliveryIntentSaveData intent)
        {
            intent = null;
            return false;
        }

        public IReadOnlyList<HaulDeliveryIntentSaveData>
            CaptureHaulDeliveryIntentsByDestination(string destinationId) =>
            Array.Empty<HaulDeliveryIntentSaveData>();
        public bool ReleaseHaulDeliveryIntent(string ownerOperationId) => false;

        public bool TryCommitBatchPhysicalDisposition(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason)
        {
            if (!TryCommitPendingBatchPhysicalDisposition(
                    inputs,
                    kind,
                    operationId,
                    reasonCode,
                    out receipt,
                    out failureReason))
            {
                return false;
            }
            return AcknowledgeBatchPhysicalDisposition(
                receipt.CommitId,
                out failureReason);
        }

        public bool AcknowledgeBatchPhysicalDisposition(
            string commitId,
            out string failureReason)
        {
            if (FailNextAcknowledgement)
            {
                FailNextAcknowledgement = false;
                failureReason = "injected work-order acknowledgement failure";
                return false;
            }
            KeyValuePair<string, PhysicalItemBatchDispositionReceipt> pending =
                pendingDispositions.FirstOrDefault(pair => string.Equals(
                    pair.Value.CommitId,
                    commitId,
                    StringComparison.Ordinal));
            if (string.IsNullOrEmpty(pending.Key))
            {
                failureReason = string.Empty;
                return true;
            }
            pendingDispositions.Remove(pending.Key);
            PhysicalAcknowledgementCount++;
            failureReason = string.Empty;
            return true;
        }

        public bool TryCommitPendingBatchPhysicalDisposition(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason)
        {
            string operation = operationId ?? string.Empty;
            if (pendingDispositions.TryGetValue(operation, out receipt))
            {
                failureReason = string.Empty;
                return true;
            }
            PhysicalItemTransformInput[] requested = (inputs
                    ?? Array.Empty<PhysicalItemTransformInput>())
                .OrderBy(value => value.StackId, StringComparer.Ordinal)
                .ToArray();
            if (kind != PhysicalItemDispositionKind.Transfer
                || requested.Length == 0
                || requested.Any(input => input.Quantity <= 0))
            {
                receipt = default;
                failureReason = "fake physical disposition request invalid";
                return false;
            }
            List<(WorldItemStackSnapshot Stack, int Quantity)> mutations = new();
            foreach (PhysicalItemTransformInput input in requested)
            {
                WorldItemStackSnapshot stack = stacks.FirstOrDefault(value =>
                    value != null
                    && string.Equals(
                        value.StackId,
                        input.StackId,
                        StringComparison.Ordinal));
                if (stack == null
                    || stack.ReservedQuantity != 0
                    || stack.Quantity < input.Quantity)
                {
                    receipt = default;
                    failureReason = "fake physical disposition source missing";
                    return false;
                }
                mutations.Add((stack, input.Quantity));
            }
            int total = requested.Sum(value => value.Quantity);
            string fingerprint = "work-order-fixture:"
                + string.Join(",", requested.Select(value =>
                    $"{value.StackId}:{value.Quantity}"));
            receipt = WorkOrderMaterialDebugContract.CreateReceipt(
                kind,
                operation,
                reasonCode,
                fingerprint,
                requested.Select(value => value.StackId).ToArray(),
                total,
                total * 1000L);
            pendingDispositions.Add(operation, receipt);
            foreach ((WorldItemStackSnapshot stack, int quantity) in mutations)
            {
                stack.Quantity -= quantity;
                if (stack.Quantity == 0)
                {
                    stacks.Remove(stack);
                }
            }
            PhysicalCommitCount++;
            failureReason = string.Empty;
            return true;
        }

        public bool TryGetPendingBatchPhysicalDisposition(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt)
        {
            return pendingDispositions.TryGetValue(
                operationId ?? string.Empty,
                out receipt);
        }

        public DungeonPhysicalItemSaveData Capture() => new DungeonPhysicalItemSaveData();
        public void Restore(DungeonPhysicalItemSaveData snapshot) { }
        public void SetStoredItemMarkersVisible(bool visible) { }
        public bool SpawnItemAtDropoff(string itemId, int amount, string sourceLabel, out int spawned)
        {
            spawned = 0;
            return false;
        }

        public bool SpawnStockAtDropoff(StockCategory category, int amount, string sourceLabel, out int spawned)
        {
            spawned = 0;
            return false;
        }

        public bool SpawnStockAtDropoff(
            StockCategory category,
            int amount,
            string sourceLabel,
            WorldItemStackState state,
            string destinationId,
            out int spawned)
        {
            spawned = 0;
            return false;
        }

        public bool SpawnStockInWarehouse(
            IWarehouseFacility warehouse,
            StockCategory category,
            int amount,
            out int spawned)
        {
            spawned = 0;
            return false;
        }

        public bool SpawnItemAt(
            string itemId,
            int amount,
            Vector2Int position,
            WorldItemStackState state,
            string destinationId,
            out int spawned)
        {
            spawned = AllowLooseSpawns ? Mathf.Max(0, amount) : 0;
            if (spawned <= 0)
                return false;
            stacks.Add(new WorldItemStackSnapshot
            {
                StackId = $"fake-loose-output:{stacks.Count + 1}",
                ItemId = itemId ?? string.Empty,
                Quantity = spawned,
                State = state,
                Position = position,
                DestinationId = destinationId ?? string.Empty
            });
            return true;
        }

        public bool SpawnWasteAt(
            string itemId,
            int amount,
            Vector2Int position,
            WasteOriginKind wasteOrigin,
            float contamination,
            out int spawned)
        {
            spawned = Mathf.Max(0, amount);
            if (spawned <= 0)
            {
                return false;
            }

            stacks.Add(new WorldItemStackSnapshot
            {
                StackId = $"fake-waste:{stacks.Count + 1}",
                ItemId = itemId ?? string.Empty,
                Quantity = spawned,
                State = WorldItemStackState.Loose,
                Position = position,
                WasteOrigin = wasteOrigin,
                Contamination = Mathf.Clamp(contamination, 0f, 100f)
            });
            return true;
        }

        public bool SpawnUniqueItemAt(
            string itemId,
            Vector2Int position,
            WorldItemStackState state,
            string destinationId,
            out string stackId)
        {
            stackId = string.Empty;
            return false;
        }

        public bool SpawnExistingUniqueItemAt(
            string itemId,
            ItemInstanceId itemInstanceId,
            Vector2Int position,
            WorldItemStackState state,
            string destinationId,
            out string stackId)
        {
            stackId = string.Empty;
            return false;
        }

        public bool SpawnExistingUniqueItemAt(
            string itemId,
            ItemInstanceId itemInstanceId,
            Vector2Int position,
            WorldItemStackState state,
            string destinationId,
            IReadOnlyList<ItemInstanceComponentSaveData> components,
            out string stackId)
        {
            stackId = string.Empty;
            return false;
        }

        public bool TryAbsorbUniqueItemStack(
            string stackId,
            ItemInstanceId expectedInstanceId) => false;

        public bool SpawnUniqueItemAt(
            string itemId,
            Vector2Int position,
            WorldItemStackState state,
            string destinationId,
            Vector2Int destinationPosition,
            out string stackId)
        {
            stackId = string.Empty;
            return false;
        }

        public bool SpawnHumanoidCorpse(
            CharacterActor source,
            Vector2Int position,
            string deathReason,
            out string stackId)
        {
            stackId = string.Empty;
            return false;
        }

        public bool TryRequestFacilityDelivery(
            StockCategory category,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out string failureReason)
        {
            requested = 0;
            failureReason =
                $"The work-order fixture requires an explicit item definition ID; category '{category}' is not a deliverable item.";
            return false;
        }

        public bool TryRequestItemDelivery(
            string itemId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out string failureReason)
        {
            requested = Mathf.Max(0, amount);
            failureReason = string.Empty;
            if (requested <= 0)
            {
                return false;
            }

            RequestedItems[itemId ?? string.Empty] =
                RequestedItems.TryGetValue(
                    itemId ?? string.Empty,
                    out int current)
                    ? current + requested
                    : requested;
            stacks.Add(new WorldItemStackSnapshot
            {
                StackId = $"fake-request:{stacks.Count + 1}",
                ItemId = itemId ?? string.Empty,
                StockCategory = StockCategory.Blueprint,
                Quantity = requested,
                State = WorldItemStackState.Loose,
                Position = Vector2Int.zero,
                DestinationId = destinationId ?? string.Empty,
                HasDestinationPosition = true,
                DestinationPosition = destinationPosition
            });
            return true;
        }

        public bool TryRequestStackDelivery(
            string stackId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out string failureReason)
        {
            requested = 0;
            failureReason = "fake stack not found";
            WorldItemStackSnapshot stack = stacks.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(
                    candidate.StackId,
                    stackId,
                    StringComparison.Ordinal));
            if (stack == null)
            {
                return false;
            }

            requested = Mathf.Min(Mathf.Max(0, amount), stack.Quantity);
            if (requested <= 0)
            {
                return false;
            }

            stack.DestinationId = destinationId ?? string.Empty;
            stack.HasDestinationPosition = true;
            stack.DestinationPosition = destinationPosition;
            failureReason = string.Empty;
            return true;
        }

        public bool TryGetPileAt(Vector2Int position, out WorldItemPileSnapshot pile)
        {
            pile = null;
            return false;
        }

        public bool TryGetPileTargetAt(
            Vector2Int position,
            out ItemPileInfoTarget target,
            out UnityEngine.Object markerObject)
        {
            target = null;
            markerObject = null;
            return false;
        }

        public IReadOnlyList<WorldItemStackSnapshot> GetStacksAt(Vector2Int position, bool includeStored = false) =>
            Array.Empty<WorldItemStackSnapshot>();

        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() => stacks;
        public bool TryFindNearestAvailableStock(
            Vector2Int origin,
            StockCategory category,
            bool preferStored,
            out WorldItemStackSnapshot stack)
        {
            stack = null;
            return false;
        }

        public void CopyAvailableStockCandidates(
            StockCategory category,
            List<WorldItemStockCandidate> destination)
        {
            destination?.Clear();
        }

        public bool TryFindBestAvailableStack(
            Vector2Int origin,
            Func<string, int> rankSelector,
            out WorldItemStackSnapshot stack)
        {
            stack = null;
            return false;
        }

        public bool HasAvailableHaulJob(CharacterActor actor) => false;
        public bool TryReserveBestHaulPlan(CharacterActor actor, out WorldItemHaulPlan plan, out string failureReason)
        {
            plan = null;
            failureReason = "no fake haul";
            return false;
        }

        public bool TryReserveStoredItemForDirectPickup(
            CharacterActor actor,
            string itemId,
            int quantity,
            out WorldItemReservedStackQuantity reservation,
            out Vector2Int pickupStandPosition,
            out string failureReason)
        {
            reservation = default;
            pickupStandPosition = default;
            failureReason = "no fake direct pickup";
            return false;
        }

        public bool TryReserveBestHaulJob(CharacterActor actor, out WorldItemHaulJob job, out string failureReason)
        {
            job = default;
            failureReason = "no fake haul";
            return false;
        }

        public bool TryPickupReservedStackQuantity(
            CharacterActor actor,
            CharacterCarryInventory inventory,
            WorldItemReservedStackQuantity reservation,
            out int pickedUp,
            out string failureReason)
        {
            pickedUp = 0;
            failureReason = "no fake pickup";
            return false;
        }

        public bool TryPickupReservedStack(
            CharacterActor actor,
            CharacterCarryInventory inventory,
            WorldItemHaulJob job,
            out string failureReason)
        {
            failureReason = "no fake pickup";
            return false;
        }

        public bool TryDepositCarriedItems(
            CharacterActor actor,
            CharacterCarryInventory inventory,
            IWarehouseFacility warehouse,
            out string failureReason)
        {
            failureReason = "no fake deposit";
            return false;
        }

        public bool TryDepositCarriedItems(
            CharacterActor actor,
            CharacterCarryInventory inventory,
            IWarehouseFacility warehouse,
            IReadOnlyCollection<string> ownerOperationIds,
            out string failureReason) =>
            TryDepositCarriedItems(actor, inventory, warehouse, out failureReason);

        public bool TryDepositCarriedItemsToFacility(
            CharacterActor actor,
            CharacterCarryInventory inventory,
            Vector2Int destinationPosition,
            string destinationId,
            out string failureReason)
        {
            failureReason = "no fake facility deposit";
            return false;
        }

        public bool TryDepositCarriedItemsToFacility(
            CharacterActor actor,
            CharacterCarryInventory inventory,
            Vector2Int destinationPosition,
            string destinationId,
            IReadOnlyCollection<string> ownerOperationIds,
            out string failureReason) =>
            TryDepositCarriedItemsToFacility(
                actor,
                inventory,
                destinationPosition,
                destinationId,
                out failureReason);

        public bool TryConsumeFacilityBuffer(
            string destinationId,
            IReadOnlyDictionary<StockCategory, int> costs,
            out string failureReason)
        {
            failureReason = string.Empty;
            string normalizedDestination = destinationId ?? string.Empty;
            if (!buffers.TryGetValue(normalizedDestination, out Dictionary<StockCategory, int> byCategory))
            {
                failureReason = "buffer missing";
                return false;
            }

            foreach (KeyValuePair<StockCategory, int> pair in costs ?? new Dictionary<StockCategory, int>())
            {
                if (!byCategory.TryGetValue(pair.Key, out int available) || available < pair.Value)
                {
                    failureReason = "buffer shortage";
                    return false;
                }
            }

            foreach (KeyValuePair<StockCategory, int> pair in costs ?? new Dictionary<StockCategory, int>())
            {
                byCategory[pair.Key] -= pair.Value;
            }

            return true;
        }

        public bool TryConsumeFacilityItemBuffer(
            string destinationId,
            IReadOnlyDictionary<string, int> costs,
            out string failureReason)
        {
            failureReason = string.Empty;
            string normalizedDestination = destinationId ?? string.Empty;
            if (!itemBuffers.TryGetValue(
                    normalizedDestination,
                    out Dictionary<string, int> byItem))
            {
                failureReason = "item buffer missing";
                return false;
            }

            foreach (KeyValuePair<string, int> pair
                     in costs ?? new Dictionary<string, int>())
            {
                if (!byItem.TryGetValue(pair.Key, out int available)
                    || available < pair.Value)
                {
                    failureReason = "item buffer shortage";
                    return false;
                }
            }

            foreach (KeyValuePair<string, int> pair
                     in costs ?? new Dictionary<string, int>())
            {
                byItem[pair.Key] -= pair.Value;
            }

            return true;
        }

        public bool TryStealLooseItem(
            CharacterActor actor,
            int searchRadius,
            out WorldItemStackSnapshot stolenItem,
            out string failureReason)
        {
            stolenItem = null;
            failureReason = "no fake theft";
            return false;
        }

        public void ReleaseReservation(string stackId, string persistentId) { }
        public bool TryClearReservation(string stackId) => false;
        public bool SetForbidden(string stackId, bool forbidden) => false;
        public bool PrioritizeHaul(string stackId) =>
            !string.IsNullOrWhiteSpace(stackId) && PrioritizedStackIds.Add(stackId);
        public bool TryRouteStackToDestination(
            string stackId,
            WorldItemStackState state,
            string destinationId,
            Vector2Int destinationPosition,
            out string failureReason)
        {
            failureReason = string.Empty;
            return false;
        }
        public bool TryRestoreStackRoute(
            WorldItemStackSnapshot original,
            WorldItemStackState appliedState,
            string appliedDestinationId,
            out string failureReason)
        {
            failureReason = "unsupported-work-fixture-route-restore";
            return false;
        }
        public bool DeleteStack(string stackId) => false;
        public bool TryConsumeStackQuantity(
            string stackId,
            int quantity,
            out WorldItemStackSnapshot consumed)
        {
            consumed = null;
            return false;
        }

        public bool TrySetInstanceComponent(
            string stackId,
            ItemInstanceComponentSaveData component) => false;
        public bool TrySetFoodFreshness(
            string stackId,
            double remainingSeconds,
            bool preserved,
            out string failureReason)
        {
            failureReason = "unsupported-work-fixture";
            return false;
        }
        public bool SetEmergencyButcheryAllowed(string stackId, bool allowed) => false;
        public int RemoveStacksByStateAndDestination(WorldItemStackState state, string destinationId) => 0;
        public int ReleaseStacksByDestination(
            string destinationId,
            Vector2Int releasePosition)
        {
            int released = stacks
                .Where(stack => string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal))
                .Sum(stack => stack.Quantity);
            ReleasedQuantity += released;
            stacks.RemoveAll(stack => string.Equals(
                stack.DestinationId,
                destinationId,
                StringComparison.Ordinal));
            return released;
        }

        public void AddFacilityBuffer(string destinationId, StockCategory category, int amount)
        {
            string normalizedDestination = destinationId ?? string.Empty;
            if (!buffers.TryGetValue(normalizedDestination, out Dictionary<StockCategory, int> byCategory))
            {
                byCategory = new Dictionary<StockCategory, int>();
                buffers[normalizedDestination] = byCategory;
            }

            byCategory[category] = byCategory.TryGetValue(category, out int current)
                ? current + amount
                : amount;
        }

        public void AddAvailableItem(string itemId, int amount)
        {
            stacks.Add(new WorldItemStackSnapshot
            {
                StackId = $"fake-item:{stacks.Count + 1}",
                ItemId = itemId ?? string.Empty,
                StockCategory = StockCategory.General,
                Quantity = Mathf.Max(0, amount),
                State = WorldItemStackState.Loose,
                Position = Vector2Int.zero
            });
        }

        public void AddFacilityItemBuffer(
            string destinationId,
            string itemId,
            int amount)
        {
            string normalizedDestination = destinationId ?? string.Empty;
            if (!itemBuffers.TryGetValue(
                    normalizedDestination,
                    out Dictionary<string, int> byItem))
            {
                byItem = new Dictionary<string, int>(StringComparer.Ordinal);
                itemBuffers[normalizedDestination] = byItem;
            }

            string normalizedItemId = itemId ?? string.Empty;
            byItem[normalizedItemId] = byItem.TryGetValue(
                normalizedItemId,
                out int current)
                ? current + amount
                : amount;
            stacks.Add(new WorldItemStackSnapshot
            {
                StackId = $"fake-facility-buffer:{stacks.Count + 1}",
                ItemId = normalizedItemId,
                StockCategory = StockCategory.General,
                Quantity = Mathf.Max(0, amount),
                ReservedQuantity = 0,
                State = WorldItemStackState.FacilityBuffer,
                Position = Vector2Int.zero,
                DestinationId = normalizedDestination
            });
        }

        internal bool EnsureCommittedLooseOutput(
            string itemId,
            int quantity,
            Vector2Int position,
            string commitId)
        {
            WorldItemStackSnapshot existing = stacks.FirstOrDefault(stack =>
                ProductionOutputCommitComponentCodec.Matches(
                    stack.Components,
                    commitId));
            if (existing != null)
            {
                return string.Equals(
                        existing.ItemId,
                        itemId,
                        StringComparison.Ordinal)
                    && existing.Quantity == quantity
                    && existing.Position == position
                    && existing.State == WorldItemStackState.Loose;
            }
            return SpawnItemAtWithComponents(
                    itemId,
                    quantity,
                    position,
                    WorldItemStackState.Loose,
                    string.Empty,
                    new[]
                    {
                        ProductionOutputCommitComponentCodec.Create(commitId)
                    },
                    out int spawned)
                && spawned == quantity;
        }
    }

    private sealed class TrackingWorkforceReplanService : IWorkforceReplanService
    {
        public int HaulReplans { get; private set; }

        public void RequestIdleWorkersToReplan(bool clearFailures = true)
        {
        }

        public void RequestOneWorkerToReplanFor(
            WorkTypeId workTypeId,
            bool clearFailures = true,
            bool forceInterrupt = false)
        {
        }

        public void RequestOneHaulerToReplan(
            bool clearFailures = true,
            bool forceInterrupt = false,
            CharacterId protectedCharacterId = default,
            bool forcePriorityWakeFanout = false)
        {
            HaulReplans++;
        }
    }
}
#endif
