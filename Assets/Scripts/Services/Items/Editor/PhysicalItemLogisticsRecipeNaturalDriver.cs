#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Rooms;
using UnityEngine;

public sealed partial class PhysicalItemLogisticsPlayModeVerificationRunner
{
    internal IProductionOutputClearanceRecipeNaturalScenarioDriver
        CreateRecipeNaturalScenarioDriver(DungeonRuntimeLifetimeScope scope) =>
        new RecipeNaturalScenarioDriver(this, scope);

    private sealed class RecipeNaturalScenarioDriver :
        IProductionOutputClearanceRecipeNaturalScenarioDriver
    {
        private static readonly Vector2Int[] UtilityDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        private readonly PhysicalItemLogisticsPlayModeVerificationRunner owner;
        private readonly DungeonRuntimeLifetimeScope scope;
        private PreparedState active;

        internal RecipeNaturalScenarioDriver(
            PhysicalItemLogisticsPlayModeVerificationRunner owner,
            DungeonRuntimeLifetimeScope scope)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
        }

        public bool TryPrepare(
            ProductionOutputClearanceNaturalExecutionRequest request,
            out ProductionRecipeExecutionCorrelation correlation,
            out string failureReason)
        {
            correlation = null;
            failureReason = string.Empty;
            if (active != null)
                return Fail("recipe-natural-driver-already-active", out failureReason);
            if (request?.Descriptor?.Payload is not
                    ProductionOutputClearanceRecipeExecutablePayload payload)
            {
                return Fail("recipe-natural-driver-payload-mismatch", out failureReason);
            }

            PreparedState state = new(request, payload);
            active = state;
            if (!TryResolveAuthorities(state, out failureReason))
            {
                Debug.Log("V27_NATURAL_RECIPE_PREPARE_FAILURE "
                    + failureReason);
                RestoreAndRelease(state, terminalizeFixtureBill: true, out _);
                return false;
            }
            // Freeze actor creation and automatic consumable delivery before
            // CaptureAll. Otherwise characters.world can be captured first,
            // then a newly spawned actor can acquire a pending meal delivery
            // before survival.character-consumables is captured, producing a
            // cross-section snapshot that strict restore must reject.
            try
            {
                owner.ConfigureNaturalClearanceAiMeasurement();
            }
            catch (Exception exception)
            {
                active = null;
                owner.RestoreBrain();
                return Fail(
                    "recipe-natural-driver-isolation-failed:"
                    + exception.GetType().Name + ":" + exception.Message,
                    out failureReason);
            }
            if (!TryCaptureBaseline(state, out failureReason)
                || !TryCreatePhysicalFixture(state, out failureReason)
                || !TryCreateOneCycleBill(state, out failureReason))
            {
                Debug.Log("V27_NATURAL_RECIPE_PREPARE_FAILURE "
                    + (string.IsNullOrEmpty(failureReason)
                        ? "<empty>"
                        : failureReason));
                RestoreAndRelease(state, terminalizeFixtureBill: true, out _);
                return false;
            }

            state.Correlation = new ProductionRecipeExecutionCorrelation(
                state.Bill.BillId,
                1,
                state.Recipe.RecipeId,
                state.Facility.PersistentInstanceId);
            correlation = state.Correlation;
            return true;
        }

        public IEnumerator ExecutePreparedProduction(
            ProductionOutputClearanceNaturalExecutionRequest request,
            ProductionRecipeExecutionCorrelation correlation,
            ProductionOutputClearanceNaturalProductionStageResult result)
        {
            if (!TryRequireActive(request, correlation, out PreparedState state,
                    out string ownerFailure))
            {
                result.Fail(ownerFailure);
                yield break;
            }

            owner.ConfigureNaturalClearanceAiMeasurement();
            int failuresBefore = owner.failures.Count;
            IEnumerator quiesce = owner.QuiesceNaturalClearanceAiPoolBeforeFixture();
            try
            {
                while (quiesce.MoveNext())
                    yield return quiesce.Current;
            }
            finally
            {
                (quiesce as IDisposable)?.Dispose();
            }
            if (owner.failures.Count != failuresBefore)
            {
                result.Fail("recipe-natural-driver-ai-prefixture-not-idle");
                yield break;
            }
            if (!TryIsolateRecipeFixtureHaulCandidates(
                    state,
                    out string isolationFailure))
            {
                result.Fail(isolationFailure);
                yield break;
            }

            state.Random.Reseed(request.Fixture.DeterministicSeed);
            state.RandomBefore = state.RandomDiagnostics.Capture();
            state.TopologyBeforeDigest =
                ProductionOutputClearanceNaturalDiagnostics.CaptureTopologyDigest(
                    state.ProductionBridge,
                    state.Workshops,
                    state.Facility);
            state.TopologySourceDigest =
                ProductionOutputClearanceNaturalDiagnostics
                    .CaptureTopologySourceDigest(
                        state.ProductionBridge,
                        state.Workshops,
                        state.Facility);
            if (state.ClearanceTelemetry.IsCaptureActive)
            {
                result.Fail("recipe-natural-driver-telemetry-already-active");
                yield break;
            }
            state.ClearanceTelemetry.BeginCapture(
                "v27.output-clearance.recipe.portfolio");
            PhysicalItemLogisticsPlayModeVerificationRunner
                .EnsureVerificationTimeScale();
            Time.timeScale = NaturalPortfolioAcceleratedTimeScale;

            owner.activeNaturalClearanceSeedRun = new NaturalClearanceSeedRunState
            {
                SeedIndex = request.Fixture.SeedIndex,
                DeterministicSeed = request.Fixture.DeterministicSeed,
                RuntimeFacilityId = state.Facility.PersistentInstanceId.Value,
                DefinitionId = request.Descriptor.Plan.DefinitionId,
                WorkstationTag = request.Descriptor.Plan.WorkstationTag,
                RecipeId = state.Recipe.RecipeId,
                BatchMassGrams = request.Descriptor.Plan.Winner.Source
                    .MaximumSingleCompletionMassGrams,
                TopologySourceDigest = state.TopologySourceDigest,
                RuntimeTopologyBeforeDigest = state.TopologyBeforeDigest,
                RandomBefore = state.RandomBefore
            };

            for (int turn = 0;
                 turn < NaturalRecipeProductionMaximumTurns;
                 turn++)
            {
                PhysicalItemLogisticsPlayModeVerificationRunner
                    .EnsureVerificationTimeScale();
                Time.timeScale = NaturalPortfolioAcceleratedTimeScale;
                if (state.ReceiptQuery.TryCaptureExecutionReceipt(
                        request.ActionId,
                        out ProductionRecipeExecutionReceipt completed)
                    && completed != null)
                {
                    state.RuntimeReceiptDigest = completed.RuntimeReceiptDigest;
                    result.Complete();
                    yield break;
                }

                ProductionBillSnapshot bill = state.Bills
                    .GetBills(state.Facility)
                    .SingleOrDefault(value => value.BillId == state.Bill.BillId);
                bool retryableBlocked = bill != null
                    && bill.BlockedFailure.Code is
                        FailureCode.ProductionUtilitiesUnavailable
                        or FailureCode.ProductionMaterialsMissing
                        or FailureCode.ProductionOutputUnavailable
                        or FailureCode.ProductionOutputSpaceUnavailable;
                if (bill == null
                    || bill.BlockedFailure.IsFailure && !retryableBlocked)
                {
                    result.Fail("recipe-natural-driver-bill-missing-or-blocked");
                    yield break;
                }

                if (bill.BatchStage != ProductionBatchStage.Processing)
                {
                    ProductionWorkAvailabilityResult available = state.Work
                        .CheckWorkAvailability(
                            state.Facility,
                            state.Recipe.WorkTypeId);
                    if (!available.Available)
                    {
                        yield return null;
                        continue;
                    }
                    ProductionWorkBeginResult begun = state.Work.BeginWork(
                        state.Worker,
                        state.Facility,
                        state.Recipe.WorkTypeId);
                    if (!begun.Succeeded)
                    {
                        if (begun.Failure.Code is
                            FailureCode.ProductionUtilitiesUnavailable
                            or FailureCode.ProductionMaterialsMissing)
                        {
                            yield return null;
                            continue;
                        }
                        result.Fail("recipe-natural-driver-work-begin-failed");
                        yield break;
                    }
                    float workAmount = Mathf.Max(
                        1f,
                        Mathf.Max(bill.RequiredWork, state.Recipe.RequiredWork)
                            + 1f);
                    ProductionWorkExecutionResult executed = state.Work.ExecuteWork(
                        state.Worker,
                        state.Facility,
                        state.Bill.BillId,
                        workAmount);
                    if (!executed.Succeeded)
                    {
                        if (executed.Failure.Code is
                            FailureCode.ProductionOutputUnavailable
                            or FailureCode.ProductionOutputSpaceUnavailable)
                        {
                            yield return null;
                            continue;
                        }
                        result.Fail(
                            "recipe-natural-driver-work-execution-failed:"
                            + executed.Failure.Code
                            + ":"
                            + string.Join(",", executed.Failure.Parameters.ToArray()));
                        yield break;
                    }
                    if (executed.CycleCompleted)
                    {
                        // The production distribution entry point may advance
                        // the acknowledged prepared-output provenance to exact
                        // route custody on the next Unity update. Capture the
                        // correlated immutable execution receipt in this same
                        // call stack, while the completed physical batch still
                        // owns its production-publication join. This does not
                        // route or move output; the normal distribution and AI
                        // haul stages remain the only custody writers.
                        if (!state.ReceiptQuery.TryCaptureExecutionReceipt(
                                request.ActionId,
                                out ProductionRecipeExecutionReceipt
                                    completedSameFrame)
                            || completedSameFrame == null)
                        {
                            result.Fail(
                                "recipe-natural-driver-cycle-completed-receipt-missing");
                            yield break;
                        }
                        state.RuntimeReceiptDigest =
                            completedSameFrame.RuntimeReceiptDigest;
                        result.Complete();
                        yield break;
                    }
                }
                yield return null;
            }

            // The final yielded Unity update may complete or publish the
            // correlated receipt. Observe it once without consuming another
            // deterministic simulation turn before declaring exhaustion.
            if (state.ReceiptQuery.TryCaptureExecutionReceipt(
                    request.ActionId,
                    out ProductionRecipeExecutionReceipt finalCompleted)
                && finalCompleted != null)
            {
                state.RuntimeReceiptDigest = finalCompleted.RuntimeReceiptDigest;
                result.Complete();
                yield break;
            }

            ProductionBillSnapshot timedOut = state.Bills
                .GetBills(state.Facility)
                .SingleOrDefault(value => value.BillId == state.Bill.BillId);
            ProductionWorkAvailabilityResult timedOutAvailability = state.Work
                .CheckWorkAvailability(
                    state.Facility,
                    state.Recipe.WorkTypeId);
            string timeoutFailure =
                "recipe-natural-driver-production-timeout"
                + $";turns={NaturalRecipeProductionMaximumTurns}"
                + $";clock={state.Clock.Time:0.###}"
                + $";delta={state.Clock.DeltaTime:0.###}"
                + $";paused={state.Clock.IsPaused}"
                + $";timeScale={Time.timeScale:0.###}"
                + $";stage={timedOut?.BatchStage.ToString() ?? "missing"}"
                + $";remainingHours={timedOut?.RemainingProcessingHours ?? -1f:0.###}"
                + $";work={timedOut?.CompletedWork ?? -1f:0.###}/"
                + $"{timedOut?.RequiredWork ?? -1f:0.###}"
                + $";blocked={timedOut?.BlockedFailure.Code.ToString() ?? "missing"}"
                + ";blockedParameters=" + (timedOut == null
                    ? "missing"
                    : string.Join(",", timedOut.BlockedFailure.Parameters.ToArray()))
                + $";available={timedOutAvailability.Available}"
                + $";availability={timedOutAvailability.Failure.Code}";
            Debug.Log("V27_NATURAL_RECIPE_PRODUCTION_TIMEOUT " + timeoutFailure);
            result.Fail(timeoutFailure);
        }

        public IEnumerator ExecutePreparedClearance(
            ProductionOutputClearanceNaturalExecutionRequest request,
            ProductionRecipeExecutionCorrelation correlation,
            ProductionOutputClearanceExecutionReceiptSnapshot receipt,
            ProductionOutputClearanceNaturalClearanceStageResult result)
        {
            if (!TryRequireActive(request, correlation, out PreparedState state,
                    out string ownerFailure)
                || receipt == null
                || !string.Equals(receipt.RuntimeReceiptDigest,
                    state.RuntimeReceiptDigest, StringComparison.Ordinal))
            {
                result.Fail(ownerFailure.Length == 0
                    ? "recipe-natural-driver-receipt-owner-mismatch"
                    : ownerFailure);
                yield break;
            }

            NaturalClearanceExpectedSlice[] routedSlices = null;
            for (int turn = 0;
                 turn < NaturalRoutePublicationMaximumTurns;
                 turn++)
            {
                state.Distribution.Tick();
                if (TryCaptureExactRoutedSlices(
                        state,
                        receipt,
                        out routedSlices))
                {
                    break;
                }
                yield return null;
            }
            if (routedSlices == null || routedSlices.Length == 0)
            {
                TryCaptureExactRoutedSlices(
                    state,
                    receipt,
                    out routedSlices);
            }
            if (routedSlices == null || routedSlices.Length == 0)
            {
                EndTelemetryIfActive(state);
                result.Fail(
                    "recipe-natural-driver-exact-route-unavailable:"
                    + "turns=" + NaturalRoutePublicationMaximumTurns + ";"
                    + CaptureExactRouteDiagnostics(state, receipt));
                yield break;
            }

            int failuresBefore = owner.failures.Count;
            IEnumerator clearance = owner.VerifySchedulerOwnedPreparedOutputClearance(
                state.ItemRuntime,
                state.Worker,
                state.Warehouse,
                routedSlices,
                receipt.ActualBatchMassGrams,
                state.WarehouseDestinationId,
                NaturalPortfolioAcceleratedTimeScale,
                NaturalPortfolioMaximumSchedulerOwners);
            try
            {
                while (clearance.MoveNext())
                    yield return clearance.Current;
            }
            finally
            {
                (clearance as IDisposable)?.Dispose();
            }

            NaturalClearanceSeedRunState run = owner.activeNaturalClearanceSeedRun;
            if (run != null)
                run.BatchMassGrams = receipt.ActualBatchMassGrams;
            string topologyAfter =
                ProductionOutputClearanceNaturalDiagnostics.CaptureTopologyDigest(
                    state.ProductionBridge,
                    state.Workshops,
                    state.Facility);
            if (run != null)
                run.RuntimeTopologyAfterDigest = topologyAfter;
            FacilityOutputClearanceTelemetrySnapshot telemetry =
                EndTelemetryIfActive(state);
            IReadOnlyList<RandomStreamDiagnosticSnapshot> randomAfter =
                state.RandomDiagnostics.Capture();
            string randomStateDigest =
                ProductionOutputClearanceNaturalDiagnostics
                    .CaptureRandomStateDigest(randomAfter);
            long randomDrawDelta =
                ProductionOutputClearanceNaturalDiagnostics
                    .CaptureRandomDrawDelta(state.RandomBefore, randomAfter);

            if (owner.failures.Count != failuresBefore
                || run == null
                || string.IsNullOrEmpty(run.OwnerRosterKey))
            {
                result.Fail("recipe-natural-driver-clearance-not-exact");
                yield break;
            }

            ProductionOutputClearanceNaturalClearanceWitness witness = new(
                state.TopologySourceDigest,
                state.TopologyBeforeDigest,
                topologyAfter,
                run.OwnerRosterKey,
                run.ActionEpochDelta,
                run.ActionStartDelta,
                run.HaulStartDelta,
                telemetry,
                run.SchedulerProvenanceExact,
                run.DeliveryExact,
                randomStateDigest,
                randomDrawDelta);
            result.Complete(witness);
        }

        public bool TryRollbackPrepared(
            ProductionOutputClearanceNaturalExecutionRequest request,
            ProductionRecipeExecutionCorrelation correlation,
            out string failureReason)
        {
            if (!TryRequireActive(request, correlation, out PreparedState state,
                    out failureReason))
                return false;
            EndTelemetryIfActive(state);
            return RestoreAndRelease(
                state,
                terminalizeFixtureBill: true,
                out failureReason);
        }

        public bool TryFinalizeAccepted(
            ProductionOutputClearanceNaturalExecutionRequest request,
            ProductionRecipeExecutionCorrelation correlation,
            out string failureReason)
        {
            if (!TryRequireActive(request, correlation, out PreparedState state,
                    out failureReason))
                return false;
            EndTelemetryIfActive(state);
            return RestoreAndRelease(
                state,
                terminalizeFixtureBill: false,
                out failureReason);
        }

        private bool TryResolveAuthorities(
            PreparedState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            state.Content = owner.Resolve<IResourceEconomyContentCatalog>(scope);
            state.GameContent = owner.Resolve<IGameContentCatalog>(scope);
            state.World = owner.Resolve<ICharacterAiWorldRegistry>(scope);
            state.SaveRegistry = owner.Resolve<IDungeonSaveSectionRegistry>(scope);
            state.Orders = owner.Resolve<IProductionBillOrderCommand>(scope);
            state.Bills = owner.Resolve<IProductionBillQuery>(scope);
            state.Work = owner.Resolve<IProductionBillWorkExecution>(scope);
            state.ProductionBridge = owner.Resolve<IProductionAssemblyBridge>(scope);
            state.Workshops = owner.Resolve<IProductionWorkshopRuntime>(scope);
            state.Rooms = owner.Resolve<IRoomLayoutCache>(scope);
            state.ItemRuntime = owner.Resolve<IWorldItemStackRuntime>(scope);
            state.Distribution = owner.Resolve<IProductionDistributionQuery>(scope)
                as ProductionDistributionRuntime;
            state.ExactRoutes = owner.Resolve<
                IFacilityOutputExactRouteOutboxQuery>(scope);
            state.BuildingFactory = owner.Resolve<IGridBuildingObjectFactory>(scope);
            state.Power = owner.Resolve<IPowerInfrastructureQuery>(scope);
            state.Water = owner.Resolve<IFluidInfrastructureTransaction>(scope);
            state.Wastewater = owner.Resolve<IFluidWastewaterTransaction>(scope);
            state.ClearanceTelemetry = owner.Resolve<
                IFacilityOutputClearanceTelemetryControl>(scope);
            state.Random = owner.Resolve<IRandomStreamProvider>(scope);
            state.RandomDiagnostics = owner.Resolve<
                IRandomStreamDiagnosticsQuery>(scope);
            state.ReceiptQuery = owner.Resolve<
                IProductionRecipeExecutionReceiptQuery>(scope);
            state.Progression = owner.Resolve<
                ProgressionSceneRuntimeReferences>(scope);
            state.SessionState = owner.Resolve<IGameSessionStateProvider>(scope);
            state.Clock = owner.Resolve<IGameClock>(scope);
            state.ClockDiagnostics = state.Clock as IGameClockDiagnosticsControl;

            bool ready = state.Content != null
                && state.GameContent != null
                && state.World != null
                && state.World.TryGetGrid(out state.Grid)
                && state.Grid != null
                && state.SaveRegistry != null
                && state.Orders != null
                && state.Bills != null
                && state.Work != null
                && state.ProductionBridge != null
                && state.Workshops != null
                && state.Rooms != null
                && state.ItemRuntime != null
                && state.Distribution != null
                && state.ExactRoutes != null
                && state.BuildingFactory != null
                && state.Power != null
                && state.Water != null
                && state.Wastewater != null
                && state.ClearanceTelemetry != null
                && state.Random != null
                && state.RandomDiagnostics != null
                && state.ReceiptQuery != null
                && state.Progression?.BlueprintResearch != null
                && state.SessionState != null
                && state.ClockDiagnostics != null;
            return ready
                || Fail("recipe-natural-driver-authority-missing", out failureReason);
        }

        private bool TryCaptureBaseline(
            PreparedState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            state.CheckpointTime = state.Clock.Time;
            state.CheckpointFrame = state.Clock.FrameCount;
            state.ClockDiagnostics.RebaseDeterministicCheckpointTime(
                state.CheckpointTime,
                state.CheckpointFrame);
            state.Baseline = state.SaveRegistry.CaptureAll();
            state.BaselineFingerprint = ComputeTextSha256(
                CaptureRestoreStableWholeRootSaveFingerprint(state.Baseline));
            return (state.Baseline.Count > 0
                    && state.BaselineFingerprint.Length == 64)
                || Fail("recipe-natural-driver-checkpoint-capture-failed",
                    out failureReason);
        }

        private bool TryCreatePhysicalFixture(
            PreparedState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (!state.Content.TryGetRecipe(
                    state.Payload.RecipeId,
                    out state.Recipe)
                || state.Recipe == null)
            {
                return Fail("recipe-natural-driver-recipe-missing", out failureReason);
            }
            if (!string.IsNullOrEmpty(state.Recipe.RequiredResearchId))
            {
                if (!state.Progression.BlueprintResearch
                    .TryCompleteProjectImmediatelyForVerification(
                        new ResearchProjectId(state.Recipe.RequiredResearchId),
                        out string completionFailure))
                {
                    return Fail(
                        "recipe-natural-driver-research-completion-failed:"
                        + completionFailure,
                        out failureReason);
                }
            }

            BuildingSO[] buildings = state.GameContent.GetAll<BuildingSO>()
                .Where(value => value != null)
                .ToArray();
            BuildingSO[] facilityMatches = buildings.Where(value =>
                    string.Equals(
                        ProductionFacilityDefinitionIdentity.Resolve(value),
                        state.Request.Descriptor.Plan.DefinitionId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.GetProductionWorkstationAbility()
                            ?.WorkstationTag ?? string.Empty,
                        state.Request.Descriptor.Plan.WorkstationTag,
                        StringComparison.Ordinal))
                .ToArray();
            if (facilityMatches.Length != 1)
            {
                return Fail("recipe-natural-driver-facility-definition-ambiguous",
                    out failureReason);
            }
            BuildingSO warehouseAsset = buildings
                .Where(value => value.GetStorageCapacity() > 0
                    && value.StoresAllCategories()
                    && value.GetStorageMassCapacityGrams()
                        >= state.Request.Descriptor.Plan.Winner.Source
                            .MaximumSingleCompletionMassGrams)
                .OrderBy(value => value.GetStorageMassCapacityGrams())
                .ThenBy(value => ProductionFacilityDefinitionIdentity.Resolve(value),
                    StringComparer.Ordinal)
                .FirstOrDefault();
            if (warehouseAsset == null)
            {
                return Fail("recipe-natural-driver-warehouse-capacity-missing",
                    out failureReason);
            }

            state.Worker = FindHauler();
            if (state.Worker == null)
                return Fail("recipe-natural-driver-worker-missing", out failureReason);
            IReadOnlyList<Vector2Int> cells = CaptureReachableFixtureCells(
                state.Grid,
                state.Worker.GetNowXY());
            BuildingSO facilityAsset = facilityMatches[0];
            BuildingSO fixturePowerSource = FindFixturePowerSource(buildings);
            if (!TryResolveSupportPlacementRequirements(
                    state.Payload.Supports,
                    buildings,
                    out SupportPlacementRequirement[] supportRequirements))
            {
                return Fail("recipe-natural-driver-support-definition-ambiguous",
                    out failureReason);
            }
            if (!TryPlanJointPhysicalFixture(
                    state,
                    buildings,
                    facilityAsset,
                    warehouseAsset,
                    supportRequirements,
                    fixturePowerSource,
                    cells,
                    out NaturalFixturePlacementPlan placementPlan,
                    out JointUtilityDemand[] utilityDemands,
                    out failureReason)
                || !TryMaterializeJointPhysicalFixture(
                    state,
                    supportRequirements,
                    placementPlan,
                    utilityDemands,
                    out failureReason))
            {
                return false;
            }
            if (state.Warehouse?.Inventory?.HasMassCapacityAuthority != true)
                return Fail("recipe-natural-driver-warehouse-create-failed",
                    out failureReason);
            owner.RegisterTemporaryWarehouse(scope, state.Warehouse);
            state.WarehouseDestinationId = WarehouseStorageIdentity
                .RequireDestinationId(state.Warehouse);
            PhysicalItemLogisticsPlayModeVerificationRunner
                .ClearInventory(state.Warehouse.Inventory);
            if (!TrySuspendOriginalWarehouses(state, out failureReason))
                return false;

            string[] expectedSupports = state.Payload.Supports
                .SelectMany(value => Enumerable.Repeat(
                    value.SupportId,
                    value.InstanceCount))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] actualSupports = state.Workshops.GetLinks(state.Facility)
                .Select(value => value.SupportId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (!actualSupports.SequenceEqual(
                    expectedSupports,
                    StringComparer.Ordinal))
            {
                return Fail("recipe-natural-driver-support-link-mismatch",
                    out failureReason);
            }
            return true;
        }

        private bool TryCreateOneCycleBill(
            PreparedState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            ProductionBillCommandResult added = state.Orders.AddBill(
                state.Facility,
                state.Recipe.RecipeId,
                ProductionOrderMode.RepeatCount,
                1);
            state.Bill = added.Succeeded
                ? state.Bills.GetBills(state.Facility)
                    .SingleOrDefault(value => value.BillId == added.BillId)
                : null;
            if (!added.Succeeded || state.Bill == null)
                return Fail("recipe-natural-driver-bill-create-failed",
                    out failureReason);
            HashSet<string> beforeStackIds = state.ItemRuntime.GetAllStacks()
                .Where(value => value != null)
                .Select(value => value.StackId)
                .ToHashSet(StringComparer.Ordinal);
            ProductionFacilityHandle facilityHandle = state.ProductionBridge
                .CaptureFacility(state.Facility);
            Dictionary<string, int> exactInputs = state.ProductionBridge
                .ToCycleInputMap(null, state.Recipe, facilityHandle);
            foreach (KeyValuePair<string, int> input in exactInputs
                         .OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                if (!state.ItemRuntime.SpawnItemAt(
                        input.Key,
                        input.Value,
                        state.Facility.centerPos,
                        WorldItemStackState.FacilityBuffer,
                        state.Bill.MaterialDestinationId,
                        out int spawned)
                    || spawned != input.Value)
                {
                    return Fail("recipe-natural-driver-input-publication-failed",
                        out failureReason);
                }
            }
            if (state.ManualWaterUnits > 0
                && (!state.ItemRuntime.SpawnItemAt(
                        FluidFacilityInputOwnerProjectionAuthority.CleanWaterItemId,
                        state.ManualWaterUnits,
                        state.Warehouse.centerPos,
                        WorldItemStackState.Loose,
                        string.Empty,
                        out int spawnedWater)
                    || spawnedWater != state.ManualWaterUnits))
            {
                return Fail("recipe-natural-driver-manual-water-source-failed",
                    out failureReason);
            }
            return TryCaptureRecipeFixtureOwnedHaulStacks(
                state,
                beforeStackIds,
                out failureReason);
        }

        private static bool TryCaptureRecipeFixtureOwnedHaulStacks(
            PreparedState state,
            HashSet<string> beforeStackIds,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (state?.ItemRuntime == null
                || state.ProductionBridge == null
                || state.Recipe == null
                || state.Bill == null
                || state.Facility == null
                || state.Warehouse == null
                || beforeStackIds == null)
            {
                return Fail(
                    "recipe-natural-driver-haul-fixture-authority-missing",
                    out failureReason);
            }

            ProductionFacilityHandle facilityHandle = state.ProductionBridge
                .CaptureFacility(state.Facility);
            Dictionary<string, int> requiredByItem = state.ProductionBridge
                .ToCycleInputMap(null, state.Recipe, facilityHandle)
                .Where(value => value.Value > 0)
                .ToDictionary(
                    value => value.Key,
                    value => value.Value,
                    StringComparer.Ordinal);
            WorldItemStackSnapshot[] live = state.ItemRuntime.GetAllStacks()
                .Where(value => value != null
                    && value.Quantity > 0
                    && !beforeStackIds.Contains(value.StackId))
                .ToArray();
            WorldItemStackSnapshot[] exactInputs = live
                .Where(value => !value.Forbidden
                    && value.State == WorldItemStackState.FacilityBuffer
                    && value.Position == state.Facility.centerPos
                    && string.Equals(
                        value.DestinationId,
                        state.Bill.MaterialDestinationId,
                        StringComparison.Ordinal)
                    && requiredByItem.ContainsKey(value.ItemId))
                .OrderBy(value => value.StackId, StringComparer.Ordinal)
                .ToArray();
            bool exactInputVector = requiredByItem.All(requirement =>
                exactInputs.Where(value => string.Equals(
                        value.ItemId,
                        requirement.Key,
                        StringComparison.Ordinal))
                    .Sum(value => value.Quantity) == requirement.Value)
                && exactInputs.All(value => requiredByItem.ContainsKey(value.ItemId));
            if (!exactInputVector)
            {
                return Fail(
                    "recipe-natural-driver-input-isolation-vector-mismatch",
                    out failureReason);
            }

            HashSet<string> allowed = exactInputs
                .Select(value => value.StackId)
                .ToHashSet(StringComparer.Ordinal);
            if (state.ManualWaterUnits > 0)
            {
                WorldItemStackSnapshot[] manualWater = live
                    .Where(value => !value.Forbidden
                        && value.State == WorldItemStackState.Loose
                        && value.Position == state.Warehouse.centerPos
                        && string.IsNullOrEmpty(value.DestinationId)
                        && string.Equals(
                            value.ItemId,
                            FluidFacilityInputOwnerProjectionAuthority
                                .CleanWaterItemId,
                            StringComparison.Ordinal))
                    .OrderBy(value => value.StackId, StringComparer.Ordinal)
                    .ToArray();
                if (manualWater.Sum(value => value.Quantity)
                    != state.ManualWaterUnits)
                {
                    return Fail(
                        "recipe-natural-driver-manual-water-isolation-vector-mismatch",
                        out failureReason);
                }
                allowed.UnionWith(manualWater.Select(value => value.StackId));
            }

            if (allowed.Count != live.Length)
            {
                return Fail(
                    "recipe-natural-driver-haul-fixture-publication-mismatch"
                    + ";published=" + string.Join(
                        ",",
                        live.OrderBy(value => value.StackId, StringComparer.Ordinal)
                            .Select(value => string.Join(
                                ":",
                                value.StackId,
                                value.ItemId,
                                value.State,
                                value.Quantity,
                                value.DestinationId,
                                value.Position.x + "_" + value.Position.y)))
                    + ";allowed=" + string.Join(
                        ",",
                        allowed.OrderBy(value => value, StringComparer.Ordinal)),
                    out failureReason);
            }
            state.FixtureOwnedHaulStackIds.Clear();
            state.FixtureOwnedHaulStackIds.UnionWith(allowed);
            return true;
        }

        private static bool TryIsolateRecipeFixtureHaulCandidates(
            PreparedState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (state?.ItemRuntime == null)
            {
                return Fail(
                    "recipe-natural-driver-haul-isolation-authority-missing",
                    out failureReason);
            }

            WorldItemStackSnapshot[] ambient = state.ItemRuntime.GetAllStacks()
                .Where(value => value != null && value.Quantity > 0)
                .Where(value => !value.Forbidden
                    && !state.FixtureOwnedHaulStackIds.Contains(value.StackId)
                    && value.State is WorldItemStackState.Loose
                        or WorldItemStackState.Stored
                        or WorldItemStackState.FacilityBuffer
                        or WorldItemStackState.FacilityOutputBuffer)
                .OrderBy(value => value.StackId, StringComparer.Ordinal)
                .ToArray();
            foreach (WorldItemStackSnapshot stack in ambient)
            {
                if (!state.ItemRuntime.SetForbidden(stack.StackId, true))
                {
                    return Fail(
                        "recipe-natural-driver-ambient-isolation-forbid-failed:"
                        + stack.StackId,
                        out failureReason);
                }
            }

            bool exactIsolation = state.ItemRuntime.GetAllStacks()
                .Where(value => value != null
                    && value.Quantity > 0
                    && !state.FixtureOwnedHaulStackIds.Contains(value.StackId)
                    && value.State is WorldItemStackState.Loose
                        or WorldItemStackState.Stored
                        or WorldItemStackState.FacilityBuffer
                        or WorldItemStackState.FacilityOutputBuffer)
                .All(value => value.Forbidden);
            return exactIsolation || Fail(
                "recipe-natural-driver-ambient-isolation-incomplete",
                out failureReason);
        }

        private static bool TrySuspendOriginalWarehouses(
            PreparedState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (state?.World == null
                || state.Warehouse == null
                || state.WarehousesSuspended)
            {
                return Fail(
                    "recipe-natural-driver-warehouse-isolation-owner-invalid",
                    out failureReason);
            }
            IWarehouseFacility[] originals = state.World.Warehouses
                .Where(value => value != null
                    && !ReferenceEquals(value, state.Warehouse))
                .Distinct()
                .OrderBy(
                    WarehouseStorageIdentity.RequireDestinationId,
                    StringComparer.Ordinal)
                .ToArray();
            foreach (IWarehouseFacility warehouse in originals)
                state.World.UnregisterWarehouse(warehouse);
            state.SuspendedWarehouses = originals;
            state.WarehousesSuspended = true;
            IWarehouseFacility[] retained = state.World.Warehouses
                .Where(value => value != null)
                .ToArray();
            bool exact = retained.Length == 1
                && ReferenceEquals(retained[0], state.Warehouse);
            return exact || Fail(
                "recipe-natural-driver-warehouse-isolation-not-exact:retained="
                + string.Join(
                    "|",
                    retained.Select(
                            WarehouseStorageIdentity.RequireDestinationId)
                        .OrderBy(value => value, StringComparer.Ordinal)),
                out failureReason);
        }

        private static bool RestoreSuspendedWarehouses(
            PreparedState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (state?.WarehousesSuspended != true)
                return true;
            if (state.World == null || state.SuspendedWarehouses == null)
            {
                return Fail(
                    "recipe-natural-driver-warehouse-restore-owner-invalid",
                    out failureReason);
            }
            foreach (IWarehouseFacility warehouse in state.SuspendedWarehouses)
            {
                if (warehouse != null
                    && !state.World.Warehouses.Any(value =>
                        ReferenceEquals(value, warehouse)))
                {
                    state.World.RegisterWarehouse(warehouse);
                }
            }
            bool exact = state.SuspendedWarehouses.All(warehouse =>
                warehouse != null
                && state.World.Warehouses.Any(value =>
                    ReferenceEquals(value, warehouse)));
            if (exact)
            {
                state.WarehousesSuspended = false;
                state.SuspendedWarehouses = Array.Empty<IWarehouseFacility>();
                return true;
            }
            return Fail(
                "recipe-natural-driver-warehouse-restore-not-exact",
                out failureReason);
        }

        private bool TryCreateUtilityFixtures(
            PreparedState state,
            IReadOnlyList<BuildingSO> buildings,
            IReadOnlyList<Vector2Int> cells,
            out string failureReason)
        {
            failureReason = string.Empty;
            BuildingSO powerSource = FindFixturePowerSource(buildings);
            BuildingSO powerConduit = FindFixtureUtilitySegment(
                buildings,
                UtilityChannel.Power);
            BuildingSO cleanWaterConduit = FindFixtureUtilitySegment(
                buildings,
                UtilityChannel.CleanWater);
            BuildingSO wastewaterConduit = FindFixtureUtilitySegment(
                buildings,
                UtilityChannel.Wastewater);
            BuildingSO cleanStorage = buildings
                .Where(value => value != null
                    && value.GetAbility<BuildingWaterStorageAbility>() is
                        BuildingWaterStorageAbility storage
                    && (storage.channels & UtilityChannel.CleanWater) != 0
                    && storage.cleanWaterCapacity > 0f)
                .OrderByDescending(value =>
                    value.GetAbility<BuildingWaterStorageAbility>()
                        .cleanWaterCapacity)
                .ThenBy(value => value.GetGridPosList(Vector2Int.zero).Count)
                .ThenBy(value => ProductionFacilityDefinitionIdentity.Resolve(value),
                    StringComparer.Ordinal)
                .FirstOrDefault();
            BuildingSO wasteStorage = buildings
                .Where(value => value != null
                    && value.GetAbility<BuildingWaterStorageAbility>() is
                        BuildingWaterStorageAbility storage
                    && (storage.channels & UtilityChannel.Wastewater) != 0
                    && storage.wastewaterCapacity > 0f)
                .OrderByDescending(value =>
                    value.GetAbility<BuildingWaterStorageAbility>()
                        .wastewaterCapacity)
                .ThenBy(value => value.GetGridPosList(Vector2Int.zero).Count)
                .ThenBy(value => ProductionFacilityDefinitionIdentity.Resolve(value),
                    StringComparer.Ordinal)
                .FirstOrDefault();

            List<UtilityDemand> demands = new();
            BuildingProcessFluidAbility facilityFluid = state.Facility.BuildingData
                .GetAbility<BuildingProcessFluidAbility>();
            bool applies = facilityFluid != null
                && facilityFluid.Supports(state.Recipe.WorkTypeId);
            float facilityWater = (applies
                    ? Mathf.Max(0f, facilityFluid.cleanWaterPerCycle)
                    : 0f)
                + state.Recipe.CleanWaterPerCycle;
            float facilityWastewater = (applies
                    ? Mathf.Max(0f, facilityFluid.wastewaterPerCycle)
                    : 0f)
                + state.Recipe.WastewaterPerCycle;
            bool facilityManual =
                (!applies
                    || facilityFluid.cleanWaterPerCycle <= 0f
                    || facilityFluid.allowsManualWaterFallback)
                && (state.Recipe.CleanWaterPerCycle <= 0f
                    || state.Recipe.AllowsManualWaterFallback);
            demands.Add(new UtilityDemand(
                state.Facility,
                requiresPower: state.Facility.BuildingData
                    .GetAbility<BuildingPowerConsumerAbility>() != null,
                facilityWater,
                facilityWastewater,
                facilityManual));
            foreach (BuildableObject support in state.SupportFacilities)
            {
                BuildingProductionSupportAbility ability = support.BuildingData
                    .GetProductionSupportAbility();
                if (ability == null)
                {
                    return Fail("recipe-natural-driver-support-utility-missing",
                        out failureReason);
                }
                demands.Add(new UtilityDemand(
                    support,
                    ability.requiresPower,
                    Mathf.Max(0f, ability.cleanWaterPerCycle),
                    Mathf.Max(0f, ability.wastewaterPerCycle),
                    ability.allowsManualWaterFallback));
            }

            foreach (UtilityDemand demand in demands)
            {
                if (demand.RequiresPower
                    && (powerSource == null
                        || powerConduit == null
                        || !TryCreateConnectedUtility(
                            state,
                            powerSource,
                            powerConduit,
                            demand.Target,
                            cells,
                            "QA_Natural_Recipe_Power",
                            out _)
                        || !state.Power.IsPowered(demand.Target)))
                {
                    return Fail("recipe-natural-driver-power-topology-failed",
                        out failureReason);
                }

                bool canPipeCleanWater = HasUtilityChannel(
                    demand.Target,
                    UtilityChannel.CleanWater);
                bool requiresPipedWater = demand.CleanWater > 0f
                    && (canPipeCleanWater || !demand.AllowsManualWater);
                if (requiresPipedWater)
                {
                    if (!canPipeCleanWater
                        || cleanStorage == null
                        || cleanWaterConduit == null
                        || !TryCreateConnectedUtility(
                            state,
                            cleanStorage,
                            cleanWaterConduit,
                            demand.Target,
                            cells,
                            "QA_Natural_Recipe_CleanWater",
                            out BuildableObject storage))
                    {
                        return Fail("recipe-natural-driver-clean-water-topology-failed",
                            out failureReason);
                    }
                    if (!state.Water.TryAdd(
                                storage,
                                WorldWaterQuality.Clean,
                                demand.CleanWater,
                                out float accepted)
                            || accepted + 0.0001f < demand.CleanWater
                            || !state.Water.CanConsume(
                                demand.Target,
                                WorldWaterQuality.Clean,
                                demand.CleanWater,
                                out _))
                    {
                        return Fail("recipe-natural-driver-clean-water-topology-failed",
                            out failureReason);
                    }
                }
                if (demand.Wastewater > 0f)
                {
                    if (!HasUtilityChannel(
                            demand.Target,
                            UtilityChannel.Wastewater)
                        || wasteStorage == null
                        || wastewaterConduit == null
                        || !TryCreateConnectedUtility(
                            state,
                            wasteStorage,
                            wastewaterConduit,
                            demand.Target,
                            cells,
                            "QA_Natural_Recipe_Wastewater",
                            out _)
                        || !state.Wastewater.CanAcceptWastewater(
                            demand.Target,
                            demand.Wastewater,
                            out _))
                    {
                        return Fail("recipe-natural-driver-wastewater-topology-failed",
                            out failureReason);
                    }
                }
                if (demand.CleanWater > 0f && !requiresPipedWater)
                {
                    BuildingProcessFluidAbility manualProcess = demand.Target
                        .BuildingData
                        .GetAbility<BuildingProcessFluidAbility>();
                    int manualDemand = Mathf.Max(
                        1,
                        Mathf.CeilToInt(demand.CleanWater));
                    int manualOwnerCapacity = manualProcess != null
                        && manualProcess.Supports(state.Recipe.WorkTypeId)
                        && manualProcess.allowsManualWaterFallback
                        && manualProcess.cleanWaterPerCycle > 0f
                            ? Mathf.Max(
                                1,
                                Mathf.CeilToInt(
                                    manualProcess.cleanWaterPerCycle))
                            : 0;
                    if (manualOwnerCapacity < manualDemand)
                    {
                        return Fail(
                            "recipe-natural-driver-manual-water-owner-capacity-missing",
                            out failureReason);
                    }
                    state.ManualWaterUnits = checked(
                        state.ManualWaterUnits + manualDemand);
                }
            }
            return true;
        }

        private bool TryPlanJointPhysicalFixture(
            PreparedState state,
            IReadOnlyList<BuildingSO> buildings,
            BuildingSO facilityAsset,
            BuildingSO warehouseAsset,
            IReadOnlyList<SupportPlacementRequirement> supports,
            BuildingSO powerSource,
            IReadOnlyList<Vector2Int> cells,
            out NaturalFixturePlacementPlan plan,
            out JointUtilityDemand[] utilityDemands,
            out string failureReason)
        {
            plan = null;
            utilityDemands = Array.Empty<JointUtilityDemand>();
            failureReason = string.Empty;
            BuildingSO powerConduit = FindFixtureUtilitySegment(
                buildings,
                UtilityChannel.Power);
            BuildingSO cleanWaterConduit = FindFixtureUtilitySegment(
                buildings,
                UtilityChannel.CleanWater);
            BuildingSO wastewaterConduit = FindFixtureUtilitySegment(
                buildings,
                UtilityChannel.Wastewater);
            BuildingSO cleanStorage = (buildings ?? Array.Empty<BuildingSO>())
                .Where(value => value != null
                    && value.GetAbility<BuildingWaterStorageAbility>() is
                        BuildingWaterStorageAbility storage
                    && (storage.channels & UtilityChannel.CleanWater) != 0
                    && storage.cleanWaterCapacity > 0f)
                .OrderByDescending(value => value
                    .GetAbility<BuildingWaterStorageAbility>().cleanWaterCapacity)
                .ThenBy(value => value.GetGridPosList(Vector2Int.zero).Count)
                .ThenBy(ProductionFacilityDefinitionIdentity.Resolve,
                    StringComparer.Ordinal)
                .FirstOrDefault();
            BuildingSO wasteStorage = (buildings ?? Array.Empty<BuildingSO>())
                .Where(value => value != null
                    && value.GetAbility<BuildingWaterStorageAbility>() is
                        BuildingWaterStorageAbility storage
                    && (storage.channels & UtilityChannel.Wastewater) != 0
                    && storage.wastewaterCapacity > 0f)
                .OrderByDescending(value => value
                    .GetAbility<BuildingWaterStorageAbility>().wastewaterCapacity)
                .ThenBy(value => value.GetGridPosList(Vector2Int.zero).Count)
                .ThenBy(ProductionFacilityDefinitionIdentity.Resolve,
                    StringComparer.Ordinal)
                .FirstOrDefault();

            List<NaturalFixtureBuildingRequirement> nodes = new()
            {
                new NaturalFixtureBuildingRequirement(
                    "facility",
                    facilityAsset,
                    NaturalFixtureNodeRole.Facility,
                    requireReachableWorkAccess: true,
                    requireUsableRoom: true,
                    roomGroupId: "production-room"),
                new NaturalFixtureBuildingRequirement(
                    "warehouse",
                    warehouseAsset,
                    NaturalFixtureNodeRole.Warehouse,
                    requireReachableWorkAccess: true)
            };
            foreach (SupportPlacementRequirement support in supports)
            {
                nodes.Add(new NaturalFixtureBuildingRequirement(
                    SupportNodeId(support),
                    support.Asset,
                    NaturalFixtureNodeRole.Support,
                    requireReachableWorkAccess: true,
                    requireUsableRoom: true,
                    roomGroupId: "production-room"));
            }

            List<NaturalFixtureUtilityRequirement> edges = new();
            List<JointUtilityDemand> demands = new();
            if (!TryAppendUtilityDemands(
                    "facility",
                    facilityAsset,
                    facilityAsset.GetAbility<BuildingPowerConsumerAbility>() != null,
                    ResolveFacilityCleanWaterDemand(state, facilityAsset),
                    ResolveFacilityWastewaterDemand(state, facilityAsset),
                    ResolveFacilityManualWaterAllowed(state, facilityAsset),
                    powerSource,
                    powerConduit,
                    cleanStorage,
                    cleanWaterConduit,
                    wasteStorage,
                    wastewaterConduit,
                    nodes,
                    edges,
                    demands,
                    state,
                    out failureReason))
            {
                return false;
            }
            foreach (SupportPlacementRequirement support in supports)
            {
                BuildingProductionSupportAbility ability = support.Asset
                    .GetProductionSupportAbility();
                if (ability == null
                    || !TryAppendUtilityDemands(
                        SupportNodeId(support),
                        support.Asset,
                        ability.requiresPower,
                        Mathf.Max(0f, ability.cleanWaterPerCycle),
                        Mathf.Max(0f, ability.wastewaterPerCycle),
                        ability.allowsManualWaterFallback,
                        powerSource,
                        powerConduit,
                        cleanStorage,
                        cleanWaterConduit,
                        wasteStorage,
                        wastewaterConduit,
                        nodes,
                        edges,
                        demands,
                        state,
                        out failureReason))
                {
                    return false;
                }
            }

            NaturalFixturePlacementResult result =
                ProductionOutputClearanceNaturalFixturePlacementPlanner.Plan(new
                    NaturalFixturePlacementRequest
                    {
                        Grid = state.Grid,
                        PlacementValidator = new BuildingPlacementValidator(
                            new GridPlacementValidator(),
                            () =>
                            {
                                GameSessionState gameData = null;
                                state.SessionState.TryGetSessionState(out gameData);
                                return new BuildingConditionContext(
                                    gameData,
                                    state.Progression.BlueprintResearch.State,
                                    null,
                                    NaturalFixturePlacementDebugRules.Instance);
                            }),
                        Rooms = state.Rooms,
                        ActorOrigin = state.Worker.GetNowXY(),
                        CandidateAnchors = cells,
                        ReachableCells = cells
                            .Append(state.Worker.GetNowXY())
                            .Distinct()
                            .ToArray(),
                        Nodes = nodes,
                        UtilityEdges = edges,
                        MaximumVisitedNodes = 250000
                    });
            if (!result.Succeeded)
            {
                return Fail(
                    "recipe-natural-driver-joint-placement-failed"
                    + ";code=" + result.FailureCode
                    + ";visited=" + result.VisitedNodes
                    + ";reason=" + result.FailureReason,
                    out failureReason);
            }
            plan = result.Plan;
            utilityDemands = demands.ToArray();
            return true;
        }

        private bool TryAppendUtilityDemands(
            string targetNodeId,
            BuildingSO targetAsset,
            bool requiresPower,
            float cleanWater,
            float wastewater,
            bool allowsManualWater,
            BuildingSO powerSource,
            BuildingSO powerConduit,
            BuildingSO cleanStorage,
            BuildingSO cleanWaterConduit,
            BuildingSO wasteStorage,
            BuildingSO wastewaterConduit,
            ICollection<NaturalFixtureBuildingRequirement> nodes,
            ICollection<NaturalFixtureUtilityRequirement> edges,
            ICollection<JointUtilityDemand> demands,
            PreparedState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (requiresPower
                && !TryAppendPipedUtility(
                    targetNodeId,
                    UtilityChannel.Power,
                    0f,
                    powerSource,
                    powerConduit,
                    nodes,
                    edges,
                    demands))
            {
                return Fail("recipe-natural-driver-power-authority-missing",
                    out failureReason);
            }

            if (cleanWater > 0f)
            {
                bool canPipe = HasUtilityChannel(targetAsset,
                    UtilityChannel.CleanWater);
                bool requiresPipe = canPipe || !allowsManualWater;
                if (requiresPipe)
                {
                    if (!canPipe
                        || !TryAppendPipedUtility(
                            targetNodeId,
                            UtilityChannel.CleanWater,
                            cleanWater,
                            cleanStorage,
                            cleanWaterConduit,
                            nodes,
                            edges,
                            demands))
                    {
                        return Fail(
                            "recipe-natural-driver-clean-water-authority-missing",
                            out failureReason);
                    }
                }
                else
                {
                    int manualDemand = Mathf.Max(1, Mathf.CeilToInt(cleanWater));
                    int manualCapacity = ResolveManualWaterCapacity(
                        state,
                        targetAsset);
                    if (manualCapacity < manualDemand)
                    {
                        return Fail(
                            "recipe-natural-driver-manual-water-owner-capacity-missing",
                            out failureReason);
                    }
                    state.ManualWaterUnits = checked(
                        state.ManualWaterUnits + manualDemand);
                }
            }

            if (wastewater > 0f
                && (!HasUtilityChannel(targetAsset, UtilityChannel.Wastewater)
                    || !TryAppendPipedUtility(
                        targetNodeId,
                        UtilityChannel.Wastewater,
                        wastewater,
                        wasteStorage,
                        wastewaterConduit,
                        nodes,
                        edges,
                        demands)))
            {
                return Fail("recipe-natural-driver-wastewater-authority-missing",
                    out failureReason);
            }
            return true;
        }

        private static bool TryAppendPipedUtility(
            string targetNodeId,
            UtilityChannel channel,
            float amount,
            BuildingSO sourceAsset,
            BuildingSO conduitAsset,
            ICollection<NaturalFixtureBuildingRequirement> nodes,
            ICollection<NaturalFixtureUtilityRequirement> edges,
            ICollection<JointUtilityDemand> demands)
        {
            if (sourceAsset == null || conduitAsset == null)
                return false;
            string token = ((int)channel).ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            string sourceNodeId = "utility-source:" + token;
            string edgeId = "utility-edge:" + targetNodeId + ":" + token;
            if (!nodes.Any(value => string.Equals(
                    value.StableNodeId,
                    sourceNodeId,
                    StringComparison.Ordinal)))
            {
                nodes.Add(new NaturalFixtureBuildingRequirement(
                    sourceNodeId,
                    sourceAsset,
                    NaturalFixtureNodeRole.UtilitySource,
                    requireReachableWorkAccess: false,
                    placementPolicy:
                        NaturalFixturePlacementPolicy.FixtureInfrastructureOccupancy));
            }
            edges.Add(new NaturalFixtureUtilityRequirement(
                edgeId,
                sourceNodeId,
                targetNodeId,
                channel,
                NaturalFixtureUtilityConnectionMode.ConduitRoute,
                conduitAsset));
            demands.Add(new JointUtilityDemand(
                targetNodeId,
                sourceNodeId,
                channel,
                amount));
            return true;
        }

        private bool TryMaterializeJointPhysicalFixture(
            PreparedState state,
            IReadOnlyList<SupportPlacementRequirement> supports,
            NaturalFixturePlacementPlan plan,
            IReadOnlyList<JointUtilityDemand> demands,
            out string failureReason)
        {
            failureReason = string.Empty;
            Dictionary<string, BuildableObject> materialized = new(
                StringComparer.Ordinal);
            if (!TryMaterializeNode("facility", "QA_Natural_Recipe_Facility",
                    out BuildableObject facility)
                || facility is not Facility productionFacility)
            {
                return Fail("recipe-natural-driver-facility-create-failed",
                    out failureReason);
            }
            state.Facility = productionFacility;
            materialized.Add("facility", facility);

            foreach (SupportPlacementRequirement support in supports)
            {
                string nodeId = SupportNodeId(support);
                if (!TryMaterializeNode(
                        nodeId,
                        "QA_Natural_Recipe_Support_" + support.SupportId + "_"
                            + support.Instance,
                        out BuildableObject supportBuilding))
                {
                    return Fail("recipe-natural-driver-support-create-failed:"
                        + nodeId, out failureReason);
                }
                state.SupportFacilities.Add(supportBuilding);
                materialized.Add(nodeId, supportBuilding);
            }

            if (!TryMaterializeNode("warehouse", "QA_Natural_Recipe_Warehouse",
                    out BuildableObject warehouse)
                || warehouse is not Facility warehouseFacility)
            {
                return Fail("recipe-natural-driver-warehouse-create-failed",
                    out failureReason);
            }
            state.Warehouse = warehouseFacility;
            materialized.Add("warehouse", warehouse);

            foreach (NaturalFixturePlacementChoice choice in plan.Choices
                         .Where(value => value.Requirement.Role
                             == NaturalFixtureNodeRole.UtilitySource)
                         .OrderBy(value => value.Requirement.StableNodeId,
                             StringComparer.Ordinal))
            {
                string nodeId = choice.Requirement.StableNodeId;
                if (!TryMaterializeNode(
                        nodeId,
                        "QA_Natural_Recipe_" + nodeId,
                        out BuildableObject source))
                {
                    return Fail("recipe-natural-driver-utility-source-create-failed:"
                        + nodeId, out failureReason);
                }
                materialized.Add(nodeId, source);
            }
            foreach (NaturalFixtureUtilityRoute route in plan.UtilityRoutes
                         .OrderBy(value => value.Requirement.StableEdgeId,
                             StringComparer.Ordinal))
            {
                for (int index = 0; index < route.ConduitAnchors.Count; index++)
                {
                    if (!TryCreateGridBuilding(
                            state,
                            route.Requirement.ConduitAsset,
                            route.ConduitAnchors[index],
                            "QA_Natural_Recipe_Conduit_"
                                + route.Requirement.StableEdgeId + "_" + index,
                            out _))
                    {
                        return Fail(
                            "recipe-natural-driver-utility-route-create-failed:"
                            + route.Requirement.StableEdgeId,
                            out failureReason);
                    }
                }
            }

            foreach (JointUtilityDemand demand in demands)
            {
                BuildableObject target = materialized[demand.TargetNodeId];
                BuildableObject source = materialized[demand.SourceNodeId];
                if (demand.Channel == UtilityChannel.Power)
                {
                    if (!state.Power.IsPowered(target))
                        return Fail("recipe-natural-driver-power-topology-failed",
                            out failureReason);
                }
                else if (demand.Channel == UtilityChannel.CleanWater)
                {
                    if (!state.Water.TryAdd(
                            source,
                            WorldWaterQuality.Clean,
                            demand.Amount,
                            out float accepted)
                        || accepted + 0.0001f < demand.Amount
                        || !state.Water.CanConsume(
                            target,
                            WorldWaterQuality.Clean,
                            demand.Amount,
                            out _))
                    {
                        return Fail(
                            "recipe-natural-driver-clean-water-topology-failed",
                            out failureReason);
                    }
                }
                else if (demand.Channel == UtilityChannel.Wastewater
                    && !state.Wastewater.CanAcceptWastewater(
                        target,
                        demand.Amount,
                        out _))
                {
                    return Fail(
                        "recipe-natural-driver-wastewater-topology-failed",
                        out failureReason);
                }
            }

            if (!state.Facility.MatchesProductionWorkstation(state.Recipe)
                || !state.Rooms.TryGetRoom(
                    state.Grid,
                    state.Facility.centerPos,
                    out RoomInstance room)
                || room == null
                || !room.IsUsable)
            {
                return Fail("recipe-natural-driver-facility-room-missing",
                    out failureReason);
            }
            return true;

            bool TryMaterializeNode(
                string nodeId,
                string objectName,
                out BuildableObject building)
            {
                building = null;
                return plan.TryGetChoice(nodeId, out NaturalFixturePlacementChoice choice)
                    && TryCreateGridBuilding(
                        state,
                        choice.Requirement.Asset,
                        choice.Anchor,
                        objectName,
                        out building);
            }
        }

        private static float ResolveFacilityCleanWaterDemand(
            PreparedState state,
            BuildingSO facilityAsset)
        {
            BuildingProcessFluidAbility process = facilityAsset
                .GetAbility<BuildingProcessFluidAbility>();
            bool applies = process != null && process.Supports(state.Recipe.WorkTypeId);
            return (applies ? Mathf.Max(0f, process.cleanWaterPerCycle) : 0f)
                + state.Recipe.CleanWaterPerCycle;
        }

        private static float ResolveFacilityWastewaterDemand(
            PreparedState state,
            BuildingSO facilityAsset)
        {
            BuildingProcessFluidAbility process = facilityAsset
                .GetAbility<BuildingProcessFluidAbility>();
            bool applies = process != null && process.Supports(state.Recipe.WorkTypeId);
            return (applies ? Mathf.Max(0f, process.wastewaterPerCycle) : 0f)
                + state.Recipe.WastewaterPerCycle;
        }

        private static bool ResolveFacilityManualWaterAllowed(
            PreparedState state,
            BuildingSO facilityAsset)
        {
            BuildingProcessFluidAbility process = facilityAsset
                .GetAbility<BuildingProcessFluidAbility>();
            bool applies = process != null && process.Supports(state.Recipe.WorkTypeId);
            return (!applies
                    || process.cleanWaterPerCycle <= 0f
                    || process.allowsManualWaterFallback)
                && (state.Recipe.CleanWaterPerCycle <= 0f
                    || state.Recipe.AllowsManualWaterFallback);
        }

        private static int ResolveManualWaterCapacity(
            PreparedState state,
            BuildingSO targetAsset)
        {
            BuildingProcessFluidAbility process = targetAsset
                .GetAbility<BuildingProcessFluidAbility>();
            if (process != null
                && process.Supports(state.Recipe.WorkTypeId)
                && process.allowsManualWaterFallback
                && process.cleanWaterPerCycle > 0f)
            {
                return Mathf.Max(1, Mathf.CeilToInt(process.cleanWaterPerCycle));
            }
            BuildingProductionSupportAbility support = targetAsset
                .GetProductionSupportAbility();
            return support != null
                && support.allowsManualWaterFallback
                && support.cleanWaterPerCycle > 0f
                    ? Mathf.Max(1, Mathf.CeilToInt(support.cleanWaterPerCycle))
                    : 0;
        }

        private static string SupportNodeId(SupportPlacementRequirement support) =>
            "support:" + support.SupportId + ":"
                + support.Instance.ToString("D4",
                    System.Globalization.CultureInfo.InvariantCulture);

        private bool TryCreateConnectedUtility(
            PreparedState state,
            BuildingSO sourceAsset,
            BuildingSO conduitAsset,
            BuildableObject target,
            IReadOnlyList<Vector2Int> cells,
            string objectName,
            out BuildableObject source)
        {
            source = null;
            if (state?.Grid == null || sourceAsset == null
                || conduitAsset == null || target == null || cells == null)
            {
                return false;
            }
            Vector2Int[] targetCells = target.buildPoses
                .DefaultIfEmpty(target.centerPos)
                .Distinct()
                .ToArray();
            Vector2Int[] sourceCandidates = cells
                .OrderBy(cell => targetCells.Min(targetCell =>
                    Mathf.Abs(cell.x - targetCell.x)
                        + Mathf.Abs(cell.y - targetCell.y)))
                .ThenBy(cell => cell.x)
                .ThenBy(cell => cell.y)
                .ToArray();
            if (!TryFindRegisterablePosition(
                    state.Grid,
                    sourceAsset,
                    sourceCandidates,
                    out Vector2Int sourcePosition)
                || !TryCreateGridBuilding(
                    state,
                    sourceAsset,
                    sourcePosition,
                    objectName + "_Source",
                    out source))
            {
                return false;
            }
            Vector2Int[] sourceCells = source.buildPoses
                .DefaultIfEmpty(source.centerPos)
                .Distinct()
                .ToArray();
            if (!TryFindUtilityRoute(
                    state.Grid,
                    conduitAsset,
                    cells,
                    sourceCells,
                    targetCells,
                    out Vector2Int[] route))
            {
                return false;
            }
            for (int index = 0; index < route.Length; index++)
            {
                if (!TryCreateGridBuilding(
                        state,
                        conduitAsset,
                        route[index],
                        objectName + "_Conduit_" + index,
                        out _))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool TryFindUtilityRoute(
            Grid grid,
            BuildingSO conduitAsset,
            IReadOnlyList<Vector2Int> reachableCells,
            IReadOnlyList<Vector2Int> sourceCells,
            IReadOnlyList<Vector2Int> targetCells,
            out Vector2Int[] route)
        {
            route = Array.Empty<Vector2Int>();
            if (grid == null || conduitAsset == null
                || reachableCells == null || sourceCells == null
                || sourceCells.Count == 0 || targetCells == null
                || targetCells.Count == 0)
            {
                return false;
            }
            HashSet<Vector2Int> allowed = new(reachableCells);
            allowed.UnionWith(sourceCells);
            allowed.UnionWith(targetCells);
            allowed.RemoveWhere(cell =>
                grid.GetGridCell(cell)?.CanOccupy(
                    conduitAsset.Placement.Layer) != true);
            HashSet<Vector2Int> targets = new(targetCells.Where(allowed.Contains));
            Queue<Vector2Int> pending = new();
            Dictionary<Vector2Int, Vector2Int> parent = new();
            HashSet<Vector2Int> visited = new();
            foreach (Vector2Int source in sourceCells
                .Where(allowed.Contains)
                .OrderBy(value => value.x)
                .ThenBy(value => value.y))
            {
                if (visited.Add(source))
                    pending.Enqueue(source);
            }
            Vector2Int reached = default;
            bool found = false;
            while (pending.Count > 0 && !found)
            {
                Vector2Int current = pending.Dequeue();
                if (targets.Contains(current))
                {
                    reached = current;
                    found = true;
                    break;
                }
                foreach (Vector2Int direction in UtilityDirections)
                {
                    Vector2Int next = current + direction;
                    if (!allowed.Contains(next) || !visited.Add(next))
                        continue;
                    parent[next] = current;
                    pending.Enqueue(next);
                }
            }
            if (!found)
                return false;
            List<Vector2Int> reversed = new() { reached };
            while (parent.TryGetValue(reached, out Vector2Int previous))
            {
                reached = previous;
                reversed.Add(reached);
            }
            reversed.Reverse();
            route = reversed.ToArray();
            return route.Length > 0;
        }

        private bool TryCreateAdjacentUtility(
            PreparedState state,
            BuildingSO asset,
            BuildableObject target,
            IReadOnlyList<Vector2Int> cells,
            string objectName,
            out BuildableObject utility)
        {
            utility = null;
            Vector2Int[] targetCells = target.buildPoses
                .DefaultIfEmpty(target.centerPos)
                .ToArray();
            Vector2Int[] candidates = cells
                .Where(cell => asset.GetGridPosList(cell).Any(footprint =>
                    targetCells.Any(targetCell =>
                        Mathf.Abs(footprint.x - targetCell.x)
                        + Mathf.Abs(footprint.y - targetCell.y) == 1)))
                .OrderBy(cell => targetCells.Min(targetCell =>
                    Mathf.Abs(cell.x - targetCell.x)
                    + Mathf.Abs(cell.y - targetCell.y)))
                .ThenBy(cell => cell.x)
                .ThenBy(cell => cell.y)
                .ToArray();
            if (!TryFindRegisterablePosition(
                    state.Grid,
                    asset,
                    candidates,
                    out Vector2Int position))
            {
                return false;
            }
            return TryCreateGridBuilding(
                state,
                asset,
                position,
                objectName,
                out utility);
        }

        private bool TryCreateGridBuilding(
            PreparedState state,
            BuildingSO asset,
            Vector2Int position,
            string objectName,
            out BuildableObject building)
        {
            building = state.BuildingFactory.Create(
                state.Grid,
                asset,
                position);
            if (building == null)
                return false;
            building.gameObject.name = objectName;
            owner.temporaryObjects.Add(building.gameObject);
            InjectGameObject(scope, building.gameObject);
            building.SetGrid(state.Grid);
            building.Initialization(asset, position);
            if (state.Grid.RegisterOccupant(
                    building,
                    asset.Placement.Layer,
                    asset.GetGridPosList(position),
                    asset.Placement.IsMovement))
            {
                return true;
            }
            owner.temporaryObjects.Remove(building.gameObject);
            UnityEngine.Object.Destroy(building.gameObject);
            building = null;
            return false;
        }

        private static bool HasUtilityChannel(
            BuildableObject building,
            UtilityChannel channel) => HasUtilityChannel(
            building?.BuildingData,
            channel);

        private static bool HasUtilityChannel(
            BuildingSO data,
            UtilityChannel channel)
        {
            if (data == null)
                return false;
            UtilityChannel channels = data
                    .GetAbility<BuildingUtilityConnectionAbility>()
                    ?.channels
                ?? UtilityChannel.None;
            BuildingWaterStorageAbility storage =
                data.GetAbility<BuildingWaterStorageAbility>();
            if (storage != null)
                channels |= storage.channels;
            if (data.GetAbility<BuildingWaterProducerAbility>() != null
                || data.GetAbility<BuildingWaterFixtureAbility>() != null)
            {
                channels |= UtilityChannel.CleanWater;
            }
            if (data.GetAbility<BuildingWaterFixtureAbility>() != null
                || data.GetAbility<BuildingWastewaterProcessorAbility>() != null)
            {
                channels |= UtilityChannel.Wastewater;
            }
            if (data.GetProductionSupportAbility() is
                    BuildingProductionSupportAbility support
                && support.requiresPower)
            {
                channels |= UtilityChannel.Power;
            }
            return (channels & channel) != 0;
        }

        private static bool TryCaptureExactRoutedSlices(
            PreparedState state,
            ProductionOutputClearanceExecutionReceiptSnapshot receipt,
            out NaturalClearanceExpectedSlice[] result)
        {
            result = null;
            Dictionary<string, ProductionOutputClearanceExecutionOutputSliceSnapshot>
                sourceByStack = receipt.Outputs.ToDictionary(
                    value => value.StackId,
                    value => value,
                    StringComparer.Ordinal);
            HashSet<string> routeBatchCommitIds = new(
                receipt.RouteBatchCommitIds,
                StringComparer.Ordinal);
            FacilityOutputExactRoutePendingSnapshot[] ownedRoutes = state.ExactRoutes
                .CapturePendingRoutes()
                .Where(value => value?.Receipt != null
                    && value.Phase == FacilityOutputExactRoutePhase.Routable
                    && routeBatchCommitIds.Contains(value.Receipt.BatchCommitId))
                .OrderBy(
                    value => value.Receipt.BatchCommitId,
                    StringComparer.Ordinal)
                .ToArray();
            string[] routedWarehouseIds = ownedRoutes
                .Select(value => value.DeliveryRevision.TargetDestinationId)
                .Where(value => !string.IsNullOrEmpty(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (ownedRoutes.Length > 0
                && (routedWarehouseIds.Length != 1
                    || !TrySelectRoutedWarehouse(
                        state,
                        routedWarehouseIds[0])))
            {
                return false;
            }
            FacilityOutputExactRouteSliceReceipt[] routes = ownedRoutes
                .SelectMany(value => value.Receipt.Slices)
                .Where(value => value != null
                    && sourceByStack.ContainsKey(value.SourceStackId))
                .OrderBy(value => value.SourceStackId, StringComparer.Ordinal)
                .ThenBy(value => value.RoutedOffsetQuantity)
                .ThenBy(value => value.RoutedStackId, StringComparer.Ordinal)
                .ToArray();
            if (routes.Length == 0)
            {
                // Exact-capability publication owns one live physical commit per
                // completed unit. The legacy distribution route relocates a full
                // lot in place, so the committed stack ID itself is the exact
                // source witness. A compatible-warehouse route deliberately leaves
                // the Loose stack untargeted until the production haul planner
                // chooses a concrete warehouse; the scheduler-owned clearance below
                // proves that choice and delivery. No descriptor-shaped route is
                // synthesized when the physical record is absent.
                WorldItemStackSnapshot[] directlyRouted = state.ItemRuntime
                    .GetAllStacks()
                    .Where(value => value != null
                        && sourceByStack.ContainsKey(value.StackId)
                        && value.State == WorldItemStackState.Loose)
                    .OrderBy(value => value.StackId, StringComparer.Ordinal)
                    .ToArray();
                if (directlyRouted.Length != sourceByStack.Count)
                    return false;
                string[] directWarehouseIds = directlyRouted
                    .Select(value => value.DestinationId)
                    .Where(value => !string.IsNullOrEmpty(value))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                bool allUntargeted = directlyRouted.All(value =>
                    string.IsNullOrEmpty(value.DestinationId)
                    && !value.HasDestinationPosition);
                bool exactTargetedWarehouse = directWarehouseIds.Length == 1
                    && directlyRouted.All(value => string.Equals(
                        value.DestinationId,
                        directWarehouseIds[0],
                        StringComparison.Ordinal))
                    && TrySelectRoutedWarehouse(state, directWarehouseIds[0]);
                bool exactPlannerPendingWarehouse = allUntargeted
                    && state.Warehouse?.Inventory != null
                    && string.Equals(
                        WarehouseStorageIdentity.RequireDestinationId(
                            state.Warehouse),
                        state.WarehouseDestinationId,
                        StringComparison.Ordinal);
                if (!exactTargetedWarehouse && !exactPlannerPendingWarehouse)
                {
                    return false;
                }
                foreach (WorldItemStackSnapshot liveStack in directlyRouted)
                {
                    ProductionOutputClearanceExecutionOutputSliceSnapshot expected =
                        sourceByStack[liveStack.StackId];
                    if (!string.Equals(
                            liveStack.ItemId,
                            expected.ItemId,
                            StringComparison.Ordinal)
                        || !string.Equals(
                            liveStack.ItemInstanceId,
                            expected.ItemInstanceId,
                            StringComparison.Ordinal)
                        || liveStack.Quantity != expected.Quantity
                        || PhysicalMassGrams.FromCanonicalKilograms(
                                liveStack.UnitWeight)
                            .Multiply(liveStack.Quantity).Value
                            != expected.MassGrams)
                    {
                        return false;
                    }
                }
                result = directlyRouted.Select(value =>
                    {
                        ProductionOutputClearanceExecutionOutputSliceSnapshot expected =
                            sourceByStack[value.StackId];
                        return new NaturalClearanceExpectedSlice(
                            value.StackId,
                            value.ItemId,
                            value.Quantity,
                            expected.MassGrams);
                    })
                    .ToArray();
                return result.Sum(value => value.MassGrams)
                    == receipt.ActualBatchMassGrams;
            }
            if (ownedRoutes.Select(value => value.Receipt.BatchCommitId)
                    .Distinct(StringComparer.Ordinal).Count()
                    != routeBatchCommitIds.Count
                || routes.Select(value => value.RoutedStackId)
                    .Distinct(StringComparer.Ordinal).Count() != routes.Length)
            {
                return false;
            }
            foreach (KeyValuePair<string,
                         ProductionOutputClearanceExecutionOutputSliceSnapshot>
                     pair in sourceByStack)
            {
                FacilityOutputExactRouteSliceReceipt[] owned = routes
                    .Where(value => string.Equals(
                        value.SourceStackId,
                        pair.Key,
                        StringComparison.Ordinal))
                    .ToArray();
                if (owned.Sum(value => value.RoutedQuantity)
                        != pair.Value.Quantity
                    || owned.Sum(value => value.RoutedMassGrams)
                        != pair.Value.MassGrams
                    || owned.Any(value => !string.Equals(
                        value.ItemId,
                        pair.Value.ItemId,
                        StringComparison.Ordinal)))
                {
                    return false;
                }
            }
            WorldItemStackSnapshot[] live = state.ItemRuntime.GetAllStacks()
                .Where(value => value != null)
                .ToArray();
            if (routes.Any(route => !live.Any(stack =>
                    string.Equals(stack.StackId, route.RoutedStackId,
                        StringComparison.Ordinal)
                    && string.Equals(stack.ItemId, route.ItemId,
                        StringComparison.Ordinal)
                    && stack.Quantity == route.RoutedQuantity
                    && stack.State == WorldItemStackState.Loose
                    && string.Equals(stack.DestinationId,
                        state.WarehouseDestinationId,
                        StringComparison.Ordinal))))
            {
                return false;
            }
            result = routes.Select(value => new NaturalClearanceExpectedSlice(
                    value.RoutedStackId,
                    value.ItemId,
                    value.RoutedQuantity,
                    value.RoutedMassGrams))
                .ToArray();
            return result.Sum(value => value.MassGrams)
                == receipt.ActualBatchMassGrams;
        }

        private static bool TrySelectRoutedWarehouse(
            PreparedState state,
            string destinationId)
        {
            Facility selected = (state.World.Warehouses
                    ?? Array.Empty<IWarehouseFacility>())
                .OfType<Facility>()
                .SingleOrDefault(value => string.Equals(
                    WarehouseStorageIdentity.RequireDestinationId(value),
                    destinationId,
                    StringComparison.Ordinal));
            if (selected == null)
                return false;
            state.Warehouse = selected;
            state.WarehouseDestinationId = destinationId;
            return true;
        }

        private static string CaptureExactRouteDiagnostics(
            PreparedState state,
            ProductionOutputClearanceExecutionReceiptSnapshot receipt)
        {
            HashSet<string> receiptStackIds = receipt.Outputs
                .Select(value => value.StackId)
                .ToHashSet(StringComparer.Ordinal);
            string commits = string.Join(
                ",",
                receipt.RouteBatchCommitIds.OrderBy(
                    value => value,
                    StringComparer.Ordinal));
            string routes = string.Join(
                ",",
                state.ExactRoutes.CapturePendingRoutes()
                    .Where(value => value?.Receipt != null)
                    .OrderBy(
                        value => value.Receipt.RouteOperationId,
                        StringComparer.Ordinal)
                    .Select(value => string.Join(
                        ":",
                        value.Receipt.RouteOperationId,
                        value.Phase,
                        value.Receipt.BatchCommitId,
                        value.Receipt.SourceDestinationId,
                        value.Receipt.TargetDestinationId,
                        value.DeliveryRevision.TargetDestinationId,
                        string.Join(
                            "+",
                            value.Receipt.Slices
                                .OrderBy(slice => slice.SourceStackId,
                                    StringComparer.Ordinal)
                                .Select(slice => slice.SourceStackId
                                    + ">" + slice.RoutedStackId)))));
            string stacks = string.Join(
                ",",
                state.ItemRuntime.GetAllStacks()
                    .Where(value => value != null
                        && (receiptStackIds.Contains(value.StackId)
                            || value.State ==
                                WorldItemStackState.FacilityOutputBuffer))
                    .OrderBy(value => value.StackId, StringComparer.Ordinal)
                    .Select(value => string.Join(
                        ":",
                        value.StackId,
                        value.ItemId,
                        value.State,
                        value.Quantity,
                        value.ReservedQuantity,
                        value.DestinationId,
                        value.HasDestinationPosition
                            ? value.DestinationPosition.x + "_"
                                + value.DestinationPosition.y
                            : "none")));
            return string.Join(
                ";",
                "warehouse=" + state.WarehouseDestinationId,
                "commits=" + (commits.Length == 0 ? "none" : commits),
                "routes=" + (routes.Length == 0 ? "none" : routes),
                "stacks=" + (stacks.Length == 0 ? "none" : stacks));
        }

        private bool TryRequireActive(
            ProductionOutputClearanceNaturalExecutionRequest request,
            ProductionRecipeExecutionCorrelation correlation,
            out PreparedState state,
            out string failureReason)
        {
            state = active;
            failureReason = string.Empty;
            bool exact = state != null
                && ReferenceEquals(state.Request, request)
                && correlation != null
                && state.Correlation != null
                && string.Equals(state.Correlation.SourceDigest,
                    correlation.SourceDigest, StringComparison.Ordinal);
            return exact || Fail("recipe-natural-driver-owner-mismatch",
                out failureReason);
        }

        private FacilityOutputClearanceTelemetrySnapshot EndTelemetryIfActive(
            PreparedState state) => state?.ClearanceTelemetry?.IsCaptureActive == true
                ? state.ClearanceTelemetry.EndCapture()
                : default;

        private bool RestoreAndRelease(
            PreparedState state,
            bool terminalizeFixtureBill,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (state == null || state.SaveRegistry == null)
            {
                active = null;
                return Fail("recipe-natural-driver-restore-authority-missing",
                    out failureReason);
            }
            ProductionBillSnapshot liveFixtureBill = state.Bill != null
                && state.Facility != null
                    ? state.Bills.GetBills(state.Facility)
                        .SingleOrDefault(value => value.BillId == state.Bill.BillId)
                    : null;
            bool terminalExactRoutingOwner = liveFixtureBill != null
                && liveFixtureBill.Mode == ProductionOrderMode.RepeatCount
                && liveFixtureBill.RemainingCycles <= 0
                && !liveFixtureBill.MaterialsConsumed
                && !ProductionPreparedOutputCapabilitySelection
                    .UsesPreparedOutputMaterializer(
                        state.Recipe,
                        state.ProductionBridge);
            if (terminalizeFixtureBill
                && liveFixtureBill != null
                && !terminalExactRoutingOwner)
            {
                ProductionBillCommandResult removed = state.Orders.RemoveBill(
                    state.Bill.BillId,
                    returnMaterials: true);
                if (!removed.Succeeded)
                {
                    return Fail(
                        "recipe-natural-driver-fixture-bill-cleanup-failed:"
                        + removed.Failure.Code
                        + ":"
                        + string.Join(",", removed.Failure.Parameters.ToArray()),
                        out failureReason);
                }
            }
            bool suspendedWarehousesRestored = RestoreSuspendedWarehouses(
                state,
                out string warehouseRestoreFailure);
            owner.RestoreBrain();
            state.ClockDiagnostics?.RebaseDeterministicCheckpointTime(
                state.CheckpointTime,
                state.CheckpointFrame);
            DungeonGameRestoreReport report = new();
            bool restored = state.SaveRegistry.RestoreAll(
                    state.Baseline,
                    report)
                && report.Success;
            state.ClockDiagnostics?.RebaseDeterministicCheckpointTime(
                state.CheckpointTime,
                state.CheckpointFrame);
            List<DungeonSaveSectionEnvelope> recaptured = restored
                ? state.SaveRegistry.CaptureAll()
                : new List<DungeonSaveSectionEnvelope>();
            string restoredFingerprint = restored
                ? ComputeTextSha256(
                    CaptureRestoreStableWholeRootSaveFingerprint(recaptured))
                : string.Empty;
            int leakedFixtureCount = Resources
                .FindObjectsOfTypeAll<BuildableObject>()
                .Count(value => value != null
                    && value.gameObject.scene.IsValid()
                    && value.gameObject.name.StartsWith(
                        "QA_Natural_Recipe_",
                        StringComparison.Ordinal));
            bool exact = suspendedWarehousesRestored
                && restored
                && string.Equals(restoredFingerprint,
                    state.BaselineFingerprint, StringComparison.Ordinal)
                && leakedFixtureCount == 0;
            string restoreDifference = restored
                ? DescribeRestoreStableWholeRootSaveDifference(
                    state.Baseline,
                    recaptured)
                : "recapture-skipped";
            string restoreErrors = report.Errors.Count == 0
                ? "none"
                : string.Join(" || ", report.Errors);
            string leakedFixtures = leakedFixtureCount == 0
                ? "none"
                : string.Join(
                    ",",
                    Resources.FindObjectsOfTypeAll<BuildableObject>()
                        .Where(value => value != null
                            && value.gameObject.scene.IsValid()
                            && value.gameObject.name.StartsWith(
                                "QA_Natural_Recipe_",
                                StringComparison.Ordinal))
                        .Select(value => value.gameObject.name)
                        .OrderBy(value => value, StringComparer.Ordinal));
            owner.DiscardRestoredPreparedOutputFixtureReferences();
            owner.activeNaturalClearanceSeedRun = null;
            active = null;
            return exact || Fail(
                (!suspendedWarehousesRestored
                    ? warehouseRestoreFailure
                    : "recipe-natural-driver-checkpoint-restore-failed")
                + $";restoreCall={restored}"
                + $";reportSuccess={report.Success}"
                + $";baselineSha={state.BaselineFingerprint}"
                + $";restoredSha={restoredFingerprint}"
                + $";difference={restoreDifference}"
                + $";errors={restoreErrors}"
                + $";leakedFixtures={leakedFixtures}",
                out failureReason);
        }

        private static bool Fail(string reason, out string failureReason)
        {
            failureReason = reason;
            return false;
        }

        private static IReadOnlyList<Vector2Int> CaptureReachableFixtureCells(
            Grid grid,
            Vector2Int actorPosition)
        {
            if (grid == null)
                return Array.Empty<Vector2Int>();
            return grid.SearchPath(actorPosition)
                .GetReachablePositions()
                .Where(position => grid.IsValidGridPos(position)
                    && grid.IsWalkable(position))
                .Distinct()
                .OrderBy(position =>
                    Mathf.Abs(position.x - actorPosition.x)
                    + Mathf.Abs(position.y - actorPosition.y))
                .ThenBy(position => position.x)
                .ThenBy(position => position.y)
                .Skip(1)
                .ToArray();
        }

        private static bool IsSingleUsableRoomFootprint(
            Grid grid,
            IRoomLayoutCache rooms,
            BuildingSO asset,
            Vector2Int anchor)
        {
            if (grid == null || rooms == null || asset == null)
                return false;
            Vector2Int[] footprint = asset.GetGridPosList(anchor).ToArray();
            if (footprint.Length == 0)
                return false;
            RoomInstance selected = null;
            foreach (Vector2Int cell in footprint)
            {
                if (!rooms.TryGetRoom(grid, cell, out RoomInstance current)
                    || current == null
                    || !current.IsUsable)
                {
                    return false;
                }
                if (selected == null)
                    selected = current;
                else if (current.Id != selected.Id)
                    return false;
            }
            return selected != null;
        }

        private static bool TryResolveSupportPlacementRequirements(
            IEnumerable<ProductionOutputClearanceExecutableSupport> supports,
            IEnumerable<BuildingSO> buildings,
            out SupportPlacementRequirement[] requirements)
        {
            List<SupportPlacementRequirement> resolved = new();
            BuildingSO[] authored = (buildings ?? Array.Empty<BuildingSO>())
                .Where(value => value != null)
                .ToArray();
            foreach (ProductionOutputClearanceExecutableSupport support in
                     (supports ?? Array.Empty<ProductionOutputClearanceExecutableSupport>())
                     .OrderBy(value => value.SupportId, StringComparer.Ordinal))
            {
                BuildingSO[] matches = authored.Where(value => string.Equals(
                        value.GetProductionSupportAbility()?.SupportId,
                        support.SupportId,
                        StringComparison.Ordinal))
                    .ToArray();
                if (matches.Length != 1 || support.InstanceCount <= 0)
                {
                    requirements = Array.Empty<SupportPlacementRequirement>();
                    return false;
                }
                BuildingSO asset = matches[0];
                bool requiresPower = asset.GetProductionSupportAbility()
                    ?.requiresPower == true;
                for (int instance = 0; instance < support.InstanceCount; instance++)
                {
                    resolved.Add(new SupportPlacementRequirement(
                        support.SupportId,
                        instance,
                        asset,
                        requiresPower));
                }
            }
            requirements = resolved.ToArray();
            return true;
        }

        private static bool TryPlanRequiredSupportPlacements(
            Grid grid,
            IRoomLayoutCache rooms,
            BuildingSO facilityAsset,
            Vector2Int facilityAnchor,
            IReadOnlyList<SupportPlacementRequirement> requirements,
            BuildingSO fixturePowerSource,
            IReadOnlyList<Vector2Int> candidates,
            out PlannedSupportPlacement[] placements)
        {
            placements = Array.Empty<PlannedSupportPlacement>();
            if (grid == null || rooms == null || facilityAsset == null
                || requirements == null || candidates == null)
            {
                return false;
            }
            Vector2Int[] facilityFootprint = facilityAsset
                .GetGridPosList(facilityAnchor)
                .ToArray();
            if (facilityFootprint.Length == 0
                || !rooms.TryGetRoom(
                    grid,
                    facilityFootprint[0],
                    out RoomInstance facilityRoom)
                || facilityRoom == null
                || !facilityRoom.IsUsable
                || facilityFootprint.Any(cell =>
                    !rooms.TryGetRoom(grid, cell, out RoomInstance room)
                    || room == null
                    || room.Id != facilityRoom.Id
                    || grid.GetGridCell(cell)?.CanOccupy(
                        facilityAsset.Placement.Layer) != true))
            {
                return false;
            }

            HashSet<Vector2Int> occupied = new(facilityFootprint);

            List<PlannedSupportPlacement> planned = new();
            bool exact = TryPlanSupportAt(0);
            placements = exact
                ? planned.ToArray()
                : Array.Empty<PlannedSupportPlacement>();
            return exact;

            bool TryPlanSupportAt(int index)
            {
                if (index >= requirements.Count)
                    return true;
                SupportPlacementRequirement requirement = requirements[index];
                foreach (Vector2Int anchor in candidates
                    .OrderBy(value => Mathf.Abs(value.x - facilityAnchor.x)
                        + Mathf.Abs(value.y - facilityAnchor.y))
                    .ThenBy(value => value.x)
                    .ThenBy(value => value.y))
                {
                    Vector2Int[] footprint = requirement.Asset
                        .GetGridPosList(anchor)
                        .ToArray();
                    if (footprint.Length == 0
                        || footprint.Any(occupied.Contains)
                        || footprint.Any(cell =>
                            !rooms.TryGetRoom(grid, cell, out RoomInstance room)
                            || room == null
                            || room.Id != facilityRoom.Id
                            || grid.GetGridCell(cell)?.CanOccupy(
                                requirement.Asset.Placement.Layer) != true))
                    {
                        continue;
                    }

                    HashSet<Vector2Int> added = new(footprint);
                    occupied.UnionWith(footprint);

                    planned.Add(new PlannedSupportPlacement(
                        requirement.SupportId,
                        requirement.Instance,
                        requirement.Asset,
                        anchor));
                    if (TryPlanSupportAt(index + 1))
                        return true;
                    planned.RemoveAt(planned.Count - 1);
                    occupied.ExceptWith(added);
                }
                return false;
            }
        }

        private static string DescribeFacilitySupportCandidate(
            Grid grid,
            IRoomLayoutCache rooms,
            BuildingSO facilityAsset,
            Vector2Int facilityAnchor,
            IReadOnlyList<SupportPlacementRequirement> requirements,
            BuildingSO fixturePowerSource,
            IReadOnlyList<Vector2Int> candidates)
        {
            Vector2Int[] facilityFootprint = facilityAsset
                .GetGridPosList(facilityAnchor)
                .ToArray();
            int roomId = rooms.TryGetRoom(
                    grid,
                    facilityFootprint[0],
                    out RoomInstance facilityRoom)
                && facilityRoom != null
                    ? facilityRoom.Id
                    : -1;
            HashSet<Vector2Int> reserved = new(facilityFootprint);
            string power = "none";
            if (fixturePowerSource != null
                && TryFindAdjacentRegisterableUtilityPosition(
                    grid,
                    fixturePowerSource,
                    facilityFootprint,
                    candidates,
                    reserved,
                    out Vector2Int powerAnchor))
            {
                power = powerAnchor.x + "/" + powerAnchor.y;
                reserved.UnionWith(
                    fixturePowerSource.GetGridPosList(powerAnchor));
            }
            string supports = string.Join(
                "+",
                requirements.Select(requirement =>
                {
                    int raw = candidates.Count(anchor =>
                    {
                        IReadOnlyList<Vector2Int> footprint = requirement.Asset
                            .GetGridPosList(anchor);
                        return footprint.Count > 0
                            && !footprint.Any(reserved.Contains)
                            && footprint.All(cell =>
                                rooms.TryGetRoom(
                                    grid,
                                    cell,
                                    out RoomInstance room)
                                && room != null
                                && room.Id == roomId
                                && grid.GetGridCell(cell)?.CanOccupy(
                                    requirement.Asset.Placement.Layer) == true);
                    });
                    return requirement.SupportId + "=" + raw;
                }));
            return facilityAnchor.x + "/" + facilityAnchor.y
                + "@r" + roomId
                + ":fp=" + string.Join(
                    "+",
                    facilityFootprint.Select(value =>
                    {
                        GridCell cell = grid.GetGridCell(value);
                        IGridOccupant occupant = cell?.GetOccupant(
                            facilityAsset.Placement.Layer);
                        string occupantName = occupant is UnityEngine.Object unity
                            ? unity.name
                            : occupant?.GetType().Name ?? "none";
                        return value.x + "/" + value.y
                            + "[can="
                            + (cell?.CanOccupy(
                                facilityAsset.Placement.Layer) == true)
                            + ";occ=" + occupantName + "]";
                    }))
                + ":power=" + power
                + ":supportFree=" + supports;
        }

        private static BuildingSO FindFixturePowerSource(
            IEnumerable<BuildingSO> buildings) => (buildings
                ?? Array.Empty<BuildingSO>())
            .Where(value => value != null
                && value.GetAbility<BuildingPowerProducerAbility>() is
                    BuildingPowerProducerAbility producer
                && producer.productionPerSecond > 0f
                && !producer.requiresFuel)
            .OrderBy(value => value.GetGridPosList(Vector2Int.zero).Count)
            .ThenBy(value => ProductionFacilityDefinitionIdentity.Resolve(value),
                StringComparer.Ordinal)
            .FirstOrDefault();

        private static BuildingSO FindFixtureUtilitySegment(
            IEnumerable<BuildingSO> buildings,
            UtilityChannel channel) => (buildings
                ?? Array.Empty<BuildingSO>())
            .Where(value => value != null
                && value.Placement.Layer == GridLayer.Utility
                && value.GetAbility<BuildingUtilityConnectionAbility>() is
                    BuildingUtilityConnectionAbility connection
                && (connection.channels & channel) != 0)
            .OrderBy(value => value
                .GetAbility<BuildingUtilityConnectionAbility>().channels == channel
                    ? 0
                    : 1)
            .ThenBy(value => value.GetGridPosList(Vector2Int.zero).Count)
            .ThenBy(value => ProductionFacilityDefinitionIdentity.Resolve(value),
                StringComparer.Ordinal)
            .FirstOrDefault();

        private static bool HasAdjacentRegisterableUtilityPosition(
            Grid grid,
            BuildingSO utilityAsset,
            IReadOnlyList<Vector2Int> targetFootprint,
            IReadOnlyList<Vector2Int> candidates)
            => TryFindAdjacentRegisterableUtilityPosition(
                grid,
                utilityAsset,
                targetFootprint,
                candidates,
                null,
                out _);

        private static bool TryFindAdjacentRegisterableUtilityPosition(
            Grid grid,
            BuildingSO utilityAsset,
            IReadOnlyList<Vector2Int> targetFootprint,
            IReadOnlyList<Vector2Int> candidates,
            ICollection<Vector2Int> additionalOccupied,
            out Vector2Int position)
        {
            position = default;
            if (grid == null || utilityAsset == null
                || targetFootprint == null || targetFootprint.Count == 0
                || candidates == null)
            {
                return false;
            }
            HashSet<Vector2Int> occupied = new(targetFootprint);
            if (additionalOccupied != null)
                occupied.UnionWith(additionalOccupied);
            foreach (Vector2Int anchor in candidates
                .OrderBy(value => targetFootprint.Min(target =>
                    Mathf.Abs(value.x - target.x)
                        + Mathf.Abs(value.y - target.y)))
                .ThenBy(value => value.x)
                .ThenBy(value => value.y))
            {
                IReadOnlyList<Vector2Int> footprint =
                    utilityAsset.GetGridPosList(anchor);
                if (footprint.Count == 0
                    || footprint.Any(occupied.Contains)
                    || !footprint.Any(cell => targetFootprint.Any(target =>
                        Mathf.Abs(cell.x - target.x)
                            + Mathf.Abs(cell.y - target.y) == 1)))
                {
                    continue;
                }
                if (footprint.All(cell => grid.GetGridCell(cell)?.CanOccupy(
                        utilityAsset.Placement.Layer) == true))
                {
                    position = anchor;
                    return true;
                }
            }
            return false;
        }

        private sealed class SupportPlacementRequirement
        {
            internal SupportPlacementRequirement(
                string supportId,
                int instance,
                BuildingSO asset,
                bool requiresPower)
            {
                SupportId = supportId;
                Instance = instance;
                Asset = asset;
                RequiresPower = requiresPower;
            }

            internal string SupportId { get; }
            internal int Instance { get; }
            internal BuildingSO Asset { get; }
            internal bool RequiresPower { get; }
        }

        private sealed class JointUtilityDemand
        {
            internal JointUtilityDemand(
                string targetNodeId,
                string sourceNodeId,
                UtilityChannel channel,
                float amount)
            {
                TargetNodeId = targetNodeId;
                SourceNodeId = sourceNodeId;
                Channel = channel;
                Amount = amount;
            }

            internal string TargetNodeId { get; }
            internal string SourceNodeId { get; }
            internal UtilityChannel Channel { get; }
            internal float Amount { get; }
        }

        private sealed class PlannedSupportPlacement
        {
            internal PlannedSupportPlacement(
                string supportId,
                int instance,
                BuildingSO asset,
                Vector2Int anchor)
            {
                SupportId = supportId;
                Instance = instance;
                Asset = asset;
                Anchor = anchor;
            }

            internal string SupportId { get; }
            internal int Instance { get; }
            internal BuildingSO Asset { get; }
            internal Vector2Int Anchor { get; }
        }

        private sealed class PreparedState
        {
            internal PreparedState(
                ProductionOutputClearanceNaturalExecutionRequest request,
                ProductionOutputClearanceRecipeExecutablePayload payload)
            {
                Request = request ?? throw new ArgumentNullException(nameof(request));
                Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            }

            internal ProductionOutputClearanceNaturalExecutionRequest Request;
            internal ProductionOutputClearanceRecipeExecutablePayload Payload;
            internal IResourceEconomyContentCatalog Content;
            internal IGameContentCatalog GameContent;
            internal ICharacterAiWorldRegistry World;
            internal IDungeonSaveSectionRegistry SaveRegistry;
            internal IProductionBillOrderCommand Orders;
            internal IProductionBillQuery Bills;
            internal IProductionBillWorkExecution Work;
            internal IProductionAssemblyBridge ProductionBridge;
            internal IProductionWorkshopRuntime Workshops;
            internal IRoomLayoutCache Rooms;
            internal IWorldItemStackRuntime ItemRuntime;
            internal ProductionDistributionRuntime Distribution;
            internal IFacilityOutputExactRouteOutboxQuery ExactRoutes;
            internal IGridBuildingObjectFactory BuildingFactory;
            internal IPowerInfrastructureQuery Power;
            internal IFluidInfrastructureTransaction Water;
            internal IFluidWastewaterTransaction Wastewater;
            internal IFacilityOutputClearanceTelemetryControl ClearanceTelemetry;
            internal IRandomStreamProvider Random;
            internal IRandomStreamDiagnosticsQuery RandomDiagnostics;
            internal IProductionRecipeExecutionReceiptQuery ReceiptQuery;
            internal ProgressionSceneRuntimeReferences Progression;
            internal IGameSessionStateProvider SessionState;
            internal IGameClock Clock;
            internal IGameClockDiagnosticsControl ClockDiagnostics;
            internal Grid Grid;
            internal CharacterActor Worker;
            internal Facility Warehouse;
            internal Facility Facility;
            internal readonly List<BuildableObject> SupportFacilities = new();
            internal int ManualWaterUnits;
            internal ProductionRecipeSO Recipe;
            internal ProductionBillSnapshot Bill;
            internal ProductionRecipeExecutionCorrelation Correlation;
            internal List<DungeonSaveSectionEnvelope> Baseline;
            internal string BaselineFingerprint = string.Empty;
            internal float CheckpointTime;
            internal int CheckpointFrame;
            internal string WarehouseDestinationId = string.Empty;
            internal readonly HashSet<string> FixtureOwnedHaulStackIds =
                new(StringComparer.Ordinal);
            internal IWarehouseFacility[] SuspendedWarehouses =
                Array.Empty<IWarehouseFacility>();
            internal bool WarehousesSuspended;
            internal IReadOnlyList<RandomStreamDiagnosticSnapshot> RandomBefore =
                Array.Empty<RandomStreamDiagnosticSnapshot>();
            internal string TopologyBeforeDigest = string.Empty;
            internal string TopologySourceDigest = string.Empty;
            internal string RuntimeReceiptDigest = string.Empty;
        }

        /// <summary>
        /// The exhaustive natural portfolio intentionally verifies authored
        /// facilities from every progression tier. Only the unlock gate is
        /// bypassed; footprint, support, room, utility, occupancy and access
        /// rules still run through the production placement validator.
        /// </summary>
        private sealed class NaturalFixturePlacementDebugRules :
            IDungeonDebugRuleQuery
        {
            internal static readonly NaturalFixturePlacementDebugRules Instance =
                new();

            public bool IsExecutingCommand => false;
            public bool IsEnabled(DungeonDebugCheat cheat) =>
                cheat == DungeonDebugCheat.IgnoreUnlocks;
            public bool ShouldFreezeNeed(
                CharacterCondition condition,
                float delta) => false;
            public bool ShouldBlockFriendlyDamage(CharacterActor actor) => false;
            public bool ShouldBlockFacilityDamage(bool damaged) => false;
            public bool ShouldSkipCosts() => false;
        }

        private readonly struct UtilityDemand
        {
            internal UtilityDemand(
                BuildableObject target,
                bool requiresPower,
                float cleanWater,
                float wastewater,
                bool allowsManualWater)
            {
                Target = target ?? throw new ArgumentNullException(nameof(target));
                RequiresPower = requiresPower;
                CleanWater = Mathf.Max(0f, cleanWater);
                Wastewater = Mathf.Max(0f, wastewater);
                AllowsManualWater = allowsManualWater;
            }

            internal BuildableObject Target { get; }
            internal bool RequiresPower { get; }
            internal float CleanWater { get; }
            internal float Wastewater { get; }
            internal bool AllowsManualWater { get; }
        }
    }
}
#endif
