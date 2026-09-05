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
    internal IProductionOutputClearanceSpecialNaturalScenarioHost
        CreateSpecialNaturalScenarioHost(DungeonRuntimeLifetimeScope scope) =>
        new SpecialNaturalScenarioHost(this, scope);

    private sealed class SpecialNaturalScenarioHost :
        IProductionOutputClearanceSpecialNaturalScenarioHost
    {
        private readonly PhysicalItemLogisticsPlayModeVerificationRunner owner;
        private readonly DungeonRuntimeLifetimeScope scope;
        private PreparedState active;

        internal SpecialNaturalScenarioHost(
            PhysicalItemLogisticsPlayModeVerificationRunner owner,
            DungeonRuntimeLifetimeScope scope)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
        }

        public bool TryPrepare(
            ProductionOutputClearanceNaturalExecutionRequest request,
            out ProductionOutputClearanceNaturalPreparedScenario scenario,
            out string failureReason)
        {
            scenario = null;
            failureReason = string.Empty;
            if (active != null)
                return Fail("special-natural-host-already-active", out failureReason);
            if (!TryGetPayload(
                    request,
                    out IProductionOutputClearanceExecutablePayload payload,
                    out IReadOnlyList<ProductionOutputClearanceExecutableInput> inputs,
                    out IReadOnlyList<ProductionOutputClearanceExecutableOutput> outputs))
            {
                return Fail("special-natural-host-payload-mismatch", out failureReason);
            }

            PreparedState state = new(request, payload, inputs, outputs);
            active = state;
            if (!TryResolveAuthorities(state, out failureReason))
            {
                active = null;
                return false;
            }
            if (!TryCaptureBaseline(state, out failureReason))
            {
                active = null;
                return false;
            }
            EnsureNaturalMeasurementTimeScale();

            try
            {
                if (!TryCreatePhysicalFixture(state, out failureReason)
                    || !TryProvisionExactInputs(state, out failureReason))
                {
                    RestoreAndRelease(state, out _);
                    return false;
                }

                state.Random.Reseed(request.Fixture.DeterministicSeed);
                int failuresBeforeAiCapture = owner.failures.Count;
                owner.ConfigureNaturalClearanceAiMeasurement();
                if (owner.failures.Count != failuresBeforeAiCapture)
                {
                    failureReason = "special-natural-host-ai-capture-failed";
                    RestoreAndRelease(state, out _);
                    return false;
                }
                state.Scenario = new ProductionOutputClearanceNaturalPreparedScenario(
                    request.ActionId,
                    state.Facility,
                    state.Worker,
                    state.CertifiedSeedOperatingDay,
                    relevantCraftSkill: 0f,
                    maximumProductionSteps: 64);
                scenario = state.Scenario;
                return true;
            }
            catch
            {
                RestoreAndRelease(state, out _);
                return Fail("special-natural-host-prepare-exception", out failureReason);
            }
        }

        public IEnumerator DriveUntil(
            ProductionOutputClearanceNaturalExecutionRequest request,
            ProductionOutputClearanceNaturalPreparedScenario scenario,
            Func<bool> liveTerminalPredicate,
            ProductionOutputClearanceNaturalHostStageResult result)
        {
            if (!TryRequireActive(request, scenario, out PreparedState state,
                    out string ownerFailure)
                || liveTerminalPredicate == null)
            {
                result.Fail(ownerFailure.Length == 0
                    ? "special-natural-host-drive-invalid"
                    : ownerFailure);
                yield break;
            }

            if (!state.AiQuiesced)
            {
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
                    result.Fail("special-natural-host-ai-prefixture-not-idle");
                    yield break;
                }
                state.AiQuiesced = true;
            }

            bool terminalReady = false;
            for (int turn = 0; turn < NaturalSpecialDriveMaximumTurns; turn++)
            {
                EnsureNaturalMeasurementTimeScale();
                state.Distribution.Tick();
                state.Crops.Tick();
                if (liveTerminalPredicate())
                {
                    terminalReady = true;
                    break;
                }
                WakeHaulers();
                yield return null;
            }
            // The final yielded Unity update may publish the last delivery.
            // Recheck once without advancing another simulation turn so the
            // fixed turn cap is inclusive rather than an off-by-one timeout.
            terminalReady |= liveTerminalPredicate();
            if (terminalReady)
            {
                int failuresBeforeQuiesce = owner.failures.Count;
                IEnumerator quiesce =
                    owner.QuiesceNaturalClearanceAiPoolBeforeFixture();
                try
                {
                    while (quiesce.MoveNext())
                        yield return quiesce.Current;
                }
                finally
                {
                    (quiesce as IDisposable)?.Dispose();
                }
                if (owner.failures.Count != failuresBeforeQuiesce)
                {
                    result.Fail(
                        "special-natural-host-post-input-ai-not-idle");
                    yield break;
                }
                state.AiQuiesced = true;
                result.Complete();
                yield break;
            }
            result.Fail(
                "special-natural-host-drive-timeout:"
                + "turns=" + NaturalSpecialDriveMaximumTurns + ";"
                + CaptureDriveTimeoutDiagnostics(state));
        }

        private string CaptureDriveTimeoutDiagnostics(PreparedState state)
        {
            HashSet<string> inputItemIds = new(
                state.Inputs.Select(value => value.ItemId),
                StringComparer.Ordinal);
            string stackSummary = string.Join(
                ",",
                state.ItemRuntime.GetAllStacks()
                    .Where(stack => stack != null
                        && stack.Quantity > 0
                        && inputItemIds.Contains(stack.ItemId))
                    .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
                    .Select(stack => string.Join(
                        ":",
                        DiagnosticToken(stack.StackId),
                        DiagnosticToken(stack.ItemId),
                        stack.State,
                        stack.Quantity,
                        stack.ReservedQuantity,
                        stack.Position.x + "," + stack.Position.y,
                        DiagnosticToken(stack.DestinationId),
                        stack.HasDestinationPosition
                            ? stack.DestinationPosition.x + ","
                                + stack.DestinationPosition.y
                            : "none")));
            if (stackSummary.Length == 0)
                stackSummary = "none";

            string payloadSummary = "not-applicable";
            if (state.Payload is
                    ProductionOutputClearanceCropHarvestExecutablePayload
                && state.Crops != null
                && state.Facility != null)
            {
                string plotId = state.Facility.PersistentInstanceId.Value;
                CropPlotSnapshot plot = state.Crops.Plots.FirstOrDefault(value =>
                    string.Equals(value.PlotId, plotId, StringComparison.Ordinal));
                bool foundWork = state.Crops.TryGetWork(
                    state.Facility,
                    BuiltInWorkTypeIds.Sow,
                    out CropPlotWorkSnapshot sow);
                string required = plot == null
                    ? "none"
                    : string.Join(",", plot.RequiredMaterials
                        .OrderBy(value => value.Key, StringComparer.Ordinal)
                        .Select(value => DiagnosticToken(value.Key)
                            + "=" + value.Value));
                string delivered = plot == null
                    ? "none"
                    : string.Join(",", plot.DeliveredMaterials
                        .OrderBy(value => value.Key, StringComparer.Ordinal)
                        .Select(value => DiagnosticToken(value.Key)
                            + "=" + value.Value));
                payloadSummary = "crop:plot=" + DiagnosticToken(plotId)
                    + ":phase=" + (plot != null
                        ? plot.Phase.ToString()
                        : "missing")
                    + ":blocked=" + DiagnosticToken(plot?.BlockedReason)
                    + ":work=" + foundWork
                    + ":available=" + (foundWork && sow.Available)
                    + ":reason=" + DiagnosticToken(
                        foundWork ? sow.UnavailableReason : "work-missing")
                    + ":required=" + required
                    + ":delivered=" + delivered;
            }
            else if (state.Payload is
                     ProductionOutputClearanceCertifiedSeedExecutablePayload)
            {
                payloadSummary = "certified-seed:facility="
                    + DiagnosticToken(state.Facility?.PersistentInstanceId.Value)
                    + ":inputWarehouse="
                    + DiagnosticToken(
                        state.InputWarehouse?.PersistentInstanceId.Value)
                    + ":inputDestination="
                    + DiagnosticToken(state.InputWarehouseDestinationId)
                    + ":inputRegistered=" + state.InputWarehouseRegistered
                    + ":storedMass="
                    + (state.InputWarehouse?.Inventory?.StoredMassGrams ?? -1L)
                    + ":reservedMass="
                    + (state.InputWarehouse?.Inventory
                        ?.ReservedInboundMassGrams ?? -1L);
            }

            int actorIndex = 0;
            string actorSummary = string.Join(
                ",",
                owner.verificationActors
                    .Where(actor => actor != null && !actor.IsDead)
                    .OrderBy(actor => actor.name, StringComparer.Ordinal)
                    .Select(actor =>
                    {
                        AbilityHaul haul = AbilityHaul.Ensure(actor);
                        string canStartFailure = haul == null
                            ? "haul-missing"
                            : string.Empty;
                        bool canStart = haul != null
                            && haul.CanStartHauling(out canStartFailure);
                        bool preview = state.HaulPlanning.TryPreviewBestPlan(
                            actor,
                            out WorldItemHaulPlan plan,
                            out string previewFailure);
                        return string.Join(
                            ":",
                            actorIndex++,
                            DiagnosticToken(actor.name),
                            actor.Brain?.HasRunningAction == true
                                ? "running"
                                : "idle",
                            canStart
                                ? "can"
                                : DiagnosticToken(canStartFailure),
                            preview
                                ? "preview-" + plan.PrimaryDestination
                                : DiagnosticToken(previewFailure));
                    }));
            if (actorSummary.Length == 0)
                actorSummary = "none";

            return "stacks=" + stackSummary
                + ";payload=" + payloadSummary
                + ";actors=" + actorSummary;
        }

        private static string DiagnosticToken(string value)
        {
            string source = value ?? string.Empty;
            return string.Concat(source.Select(character =>
                char.IsWhiteSpace(character) ? '_' : character));
        }

        public IEnumerator AdvanceCropToMaximumHarvestReady(
            ProductionOutputClearanceNaturalExecutionRequest request,
            ProductionOutputClearanceNaturalPreparedScenario scenario,
            ProductionOutputClearanceCropHarvestExecutablePayload payload,
            ProductionOutputClearanceNaturalHostStageResult result)
        {
            if (!TryRequireActive(request, scenario, out PreparedState state,
                    out string ownerFailure)
                || payload == null
                || !ReferenceEquals(payload, state.Payload)
                || state.Crop == null
                || !ReferenceEquals(state.Worker, scenario.Worker))
            {
                result.Fail(ownerFailure.Length == 0
                    ? "special-natural-crop-growth-owner-mismatch"
                    : ownerFailure);
                yield break;
            }

            ResearchFacilityCommandKind[] requiredSupports =
                RequiredCropSupportCommands(payload.Indoor);
            if (requiredSupports.Any(command =>
                    state.FacilityCapabilities.FindOperational(command).Count == 0))
            {
                result.Fail("special-natural-crop-support-not-operational");
                yield break;
            }

            if (!TryRetireInputWarehouse(
                    state,
                    requireEmpty: true,
                    out string inputFailure))
            {
                result.Fail(inputFailure);
                yield break;
            }

            state.SurvivalDebug.DebugSetWeather(payload.Weather);
            EnsureNaturalMeasurementTimeScale();
            bool harvestReady = false;
            for (int turn = 0;
                 turn < NaturalSpecialCropGrowthMaximumTurns;
                 turn++)
            {
                PhysicalItemLogisticsPlayModeVerificationRunner
                    .EnsureVerificationTimeScale();
                state.Crops.Tick();
                if (state.Crops.TryGetWork(
                        state.Facility,
                        BuiltInWorkTypeIds.Harvest,
                        out CropPlotWorkSnapshot harvest)
                    && harvest.Available)
                {
                    harvestReady = true;
                    break;
                }
                yield return null;
            }
            if (!harvestReady
                && state.Crops.TryGetWork(
                    state.Facility,
                    BuiltInWorkTypeIds.Harvest,
                    out CropPlotWorkSnapshot finalHarvest)
                && finalHarvest.Available)
            {
                harvestReady = true;
            }
            if (!harvestReady)
            {
                result.Fail(
                    "special-natural-crop-growth-timeout:turns="
                    + NaturalSpecialCropGrowthMaximumTurns);
                yield break;
            }

            CropPlotSnapshot liveCrop = state.Crops.Plots.SingleOrDefault(value =>
                string.Equals(
                    value.PlotId,
                    state.Facility.PersistentInstanceId.Value,
                    StringComparison.Ordinal));
            if (liveCrop == null
                || state.CropGenomeWitness == null
                || !string.Equals(
                    liveCrop.CultivarGenomeId,
                    state.CropGenomeWitness.GenomeId,
                    StringComparison.Ordinal))
            {
                result.Fail(
                    "special-natural-crop-selected-genome-mismatch:actual="
                    + (liveCrop?.CultivarGenomeId ?? "<missing>")
                    + ";expected="
                    + (state.CropGenomeWitness?.GenomeId ?? "<missing>"));
                yield break;
            }

            if (!TryAlignCropGeneticsMaximumOutcome(
                    state,
                    out string geneticsFailure))
            {
                result.Fail(geneticsFailure);
                yield break;
            }

            if (!state.Crops.TryScheduleGoldenHarvest(
                    state.Facility,
                    state.Worker,
                    out _))
            {
                result.Fail("special-natural-crop-golden-schedule-failed");
                yield break;
            }
            state.ClockDiagnostics.RebaseDeterministicCheckpointTime(
                state.Clock.Time + GameCalendarRules.SecondsPerDay,
                checked(state.Clock.FrameCount + 1));
            if (state.Crops.TryGetGoldenHarvestDelay(
                    state.Facility,
                    state.Worker,
                    out _))
            {
                result.Fail("special-natural-crop-golden-not-mature");
                yield break;
            }

            EnsureNaturalMeasurementTimeScale();
            for (int frame = 0; frame < 4; frame++)
                yield return null;
            if (!TryProvisionMaximumOutputBenefits(
                    state,
                    out string benefitFailure))
            {
                result.Fail(benefitFailure);
                yield break;
            }
            result.Complete();
        }

        private bool TryRetireInputWarehouse(
            PreparedState state,
            bool requireEmpty,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (state?.InputWarehouse?.Inventory == null
                || !state.InputWarehouseRegistered
                || string.IsNullOrEmpty(state.InputWarehouseDestinationId))
            {
                return Fail(
                    "special-natural-input-warehouse-owner-missing",
                    out failureReason);
            }
            WorldItemStackSnapshot[] retained = state.ItemRuntime.GetAllStacks()
                .Where(value => value != null
                    && value.Quantity > 0
                    && value.State == WorldItemStackState.Stored
                    && value.Position == state.InputWarehouse.centerPos)
                .OrderBy(value => value.ItemId, StringComparer.Ordinal)
                .ThenBy(value => value.StackId, StringComparer.Ordinal)
                .ToArray();
            if (requireEmpty
                && (state.InputWarehouse.Inventory.StoredMassGrams != 0L
                    || state.InputWarehouse.Inventory.ReservedInboundMassGrams != 0L
                    || retained.Length != 0))
            {
                return Fail(
                    "special-natural-input-warehouse-not-empty-after-consume:stored="
                    + state.InputWarehouse.Inventory.StoredMassGrams
                    + ";reserved="
                    + state.InputWarehouse.Inventory.ReservedInboundMassGrams
                    + ";items="
                    + (retained.Length == 0
                        ? "none"
                        : string.Join(
                            "|",
                            retained.Select(value => value.ItemId + ":"
                                + value.Quantity + ":" + value.StackId))),
                    out failureReason);
            }
            if (!owner.UnregisterTemporaryWarehouse(state.InputWarehouse))
            {
                return Fail(
                    "special-natural-input-warehouse-unregister-failed",
                    out failureReason);
            }
            state.InputWarehouseRegistered = false;
            return true;
        }

        private static bool TryAlignCropGeneticsMaximumOutcome(
            PreparedState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            CultivarGenomeSaveData authored = state.CropGenomeWitness?
                .Definition?.CreateRuntimeDefinition();
            int locusCount = authored?.loci?.Count ?? 0;
            if (state.Random == null || locusCount <= 0)
            {
                failureReason =
                    "special-natural-crop-genetics-witness-invalid";
                return false;
            }

            IRandomStream live = state.Random.Get(
                CropEcologyRuntime.GeneticsRandomStreamId);
            DeterministicRandomSequence cursor = new(1);
            cursor.Restore(live.State);
            const int maximumProbeOffset = 4_096;
            for (int offset = 0; offset <= maximumProbeOffset; offset++)
            {
                DeterministicRandomSequence probe = new(1);
                probe.Restore(cursor.State);
                bool mutationFree = true;
                for (int locus = 0; locus < locusCount; locus++)
                {
                    if (probe.NextFloat() < 0.01f)
                    {
                        mutationFree = false;
                        break;
                    }
                }
                bool maximumSeedRoll = mutationFree
                    && probe.NextFloat() >= (2f / 3f);
                if (maximumSeedRoll)
                {
                    for (int skipped = 0; skipped < offset; skipped++)
                        live.NextFloat();
                    if (live.State != cursor.State)
                    {
                        failureReason =
                            "special-natural-crop-genetics-alignment-drift";
                        return false;
                    }
                    return true;
                }
                cursor.NextFloat();
            }

            failureReason =
                "special-natural-crop-maximum-genetics-key-unreachable";
            return false;
        }

        public IEnumerator DriveSchedulerOwnedOutputClearance(
            ProductionOutputClearanceNaturalExecutionRequest request,
            ProductionOutputClearanceNaturalPreparedScenario scenario,
            ProductionOutputClearanceExecutionReceiptSnapshot receipt,
            ProductionOutputClearanceNaturalSchedulerRunResult result)
        {
            if (!TryRequireActive(request, scenario, out PreparedState state,
                    out string ownerFailure)
                || receipt == null
                || !string.Equals(
                    receipt.ActionId,
                    request.ActionId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.RuntimeFacilityId,
                    state.Facility.PersistentInstanceId.Value,
                    StringComparison.Ordinal))
            {
                result.Fail(ownerFailure.Length == 0
                    ? "special-natural-clearance-receipt-owner-mismatch"
                    : ownerFailure);
                yield break;
            }

            if (!TryRestoreMaximumOutputBenefits(
                    state,
                    out string benefitRestoreFailure))
            {
                result.Fail(benefitRestoreFailure);
                yield break;
            }

            if (state.InputWarehouseRegistered
                && !TryRetireInputWarehouse(
                    state,
                    requireEmpty: false,
                    out string inputRetireFailure))
            {
                result.Fail(inputRetireFailure);
                yield break;
            }

            if (state.Warehouse?.Inventory == null
                || state.Warehouse.Inventory.RemainingMassGrams
                    < receipt.ActualBatchMassGrams)
            {
                result.Fail(
                    "special-natural-output-warehouse-headroom-insufficient:remaining="
                    + (state.Warehouse?.Inventory?.RemainingMassGrams ?? -1L)
                    + ";required=" + receipt.ActualBatchMassGrams
                    + ";stored="
                    + (state.Warehouse?.Inventory?.StoredMassGrams ?? -1L)
                    + ";reserved="
                    + (state.Warehouse?.Inventory?.ReservedInboundMassGrams ?? -1L));
                yield break;
            }

            NaturalClearanceExpectedSlice[] routedSlices = null;
            for (int turn = 0;
                 turn < NaturalRoutePublicationMaximumTurns;
                 turn++)
            {
                EnsureNaturalMeasurementTimeScale();
                state.Distribution.Tick();
                if (TryCaptureExactRoutedSlices(state, receipt, out routedSlices))
                    break;
                yield return null;
            }
            if (routedSlices == null || routedSlices.Length == 0)
            {
                TryCaptureExactRoutedSlices(state, receipt, out routedSlices);
            }
            if (routedSlices == null || routedSlices.Length == 0)
            {
                result.Fail(
                    "special-natural-exact-route-unavailable:"
                    + "turns=" + NaturalRoutePublicationMaximumTurns + ";"
                    + CaptureExactRouteDiagnostics(state, receipt));
                yield break;
            }
            owner.activeNaturalClearanceSeedRun = CreateRunState(state, receipt);
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
            if (owner.failures.Count != failuresBefore
                || run == null
                || string.IsNullOrWhiteSpace(run.OwnerRosterKey))
            {
                result.Fail("special-natural-scheduler-clearance-not-exact");
                yield break;
            }
            result.Complete(
                run.OwnerRosterKey,
                run.ActionEpochDelta,
                run.ActionStartDelta,
                run.HaulStartDelta,
                run.SchedulerProvenanceExact,
                run.DeliveryExact);
        }

        public bool TryRelease(
            ProductionOutputClearanceNaturalExecutionRequest request,
            ProductionOutputClearanceNaturalPreparedScenario scenario,
            out string failureReason)
        {
            if (!TryRequireActive(request, scenario, out PreparedState state,
                    out failureReason))
            {
                return false;
            }
            return RestoreAndRelease(state, out failureReason);
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
            state.ProductionBridge = owner.Resolve<IProductionAssemblyBridge>(scope);
            state.Workshops = owner.Resolve<IProductionWorkshopRuntime>(scope);
            state.Rooms = owner.Resolve<IRoomLayoutCache>(scope);
            state.ItemRuntime = owner.Resolve<IWorldItemStackRuntime>(scope);
            state.Transfers = owner.Resolve<IItemTransferService>(scope);
            state.HaulPlanning = owner.Resolve<IWorldItemHaulPlanningService>(scope);
            state.Distribution = owner.Resolve<IProductionDistributionQuery>(scope)
                as ProductionDistributionRuntime;
            state.ExactRoutes = owner.Resolve<
                IFacilityOutputExactRouteOutboxQuery>(scope);
            state.BuildingFactory = owner.Resolve<IGridBuildingObjectFactory>(scope);
            state.FacilityCapabilities = owner.Resolve<IFacilityCapabilityQuery>(scope);
            state.Crops = owner.Resolve<CropPlotRuntime>(scope);
            state.GrandProjects = owner.Resolve<IGrandProjectRuntime>(scope);
            state.GrandProjectBenefits = owner.Resolve<IGrandProjectBenefitQuery>(scope);
            state.Random = owner.Resolve<IRandomStreamProvider>(scope);
            state.RunSeed = owner.Resolve<IRunSeedProvider>(scope);
            state.Clock = owner.Resolve<IGameClock>(scope);
            state.ClockDiagnostics = state.Clock as IGameClockDiagnosticsControl;
            state.Calendar = owner.Resolve<IGameCalendar>(scope);
            state.SessionState = owner.Resolve<IGameSessionStateProvider>(scope);
            state.Progression = owner.Resolve<ProgressionSceneRuntimeReferences>(scope);
            state.CombatCatalog = owner.Resolve<ICombatEquipmentCatalog>(scope);
            state.CombatCraftDefinitions = owner.Resolve<
                ICombatCraftDefinitionCatalog>(scope);
            state.Narrative = owner.Resolve<ICharacterNarrativeCommand>(scope);
            state.Proficiencies = owner.Resolve<ICharacterProficiencyCommand>(scope);
            state.SurvivalEnvironment = owner.Resolve<ISurvivalEnvironmentQuery>(scope);
            state.SurvivalDebug = owner.Resolve<ISurvivalFoodDebugCommand>(scope);
            state.Power = owner.Resolve<IPowerInfrastructureQuery>(scope);
            state.Fluid = owner.Resolve<IFluidInfrastructureTransaction>(scope);
            state.Wastewater = owner.Resolve<IFluidWastewaterTransaction>(scope);

            if (state.Payload is
                    ProductionOutputClearanceCropHarvestExecutablePayload)
            {
                try
                {
                    state.CropGenomeWitnesses = new
                        CropGenomeReachableMaximumWitnessCatalog(
                            state.GameContent);
                }
                catch
                {
                    return Fail(
                        "special-natural-crop-genome-witness-invalid",
                        out failureReason);
                }
            }

            bool ready = state.Content != null
                && state.GameContent != null
                && state.World != null
                && state.World.TryGetGrid(out state.Grid)
                && state.Grid != null
                && state.SaveRegistry != null
                && state.ProductionBridge != null
                && state.Workshops != null
                && state.Rooms != null
                && state.ItemRuntime != null
                && state.Transfers != null
                && state.HaulPlanning != null
                && state.Distribution != null
                && state.ExactRoutes != null
                && state.BuildingFactory != null
                && state.FacilityCapabilities != null
                && state.Crops != null
                && state.Random != null
                && state.RunSeed != null
                && state.ClockDiagnostics != null
                && state.Calendar != null
                && state.SessionState != null
                && state.Progression?.BlueprintResearch != null
                && state.Power != null
                && state.Fluid != null
                && state.Wastewater != null;
            if (state.Payload is ProductionOutputClearanceCombatCraftExecutablePayload)
                ready &= state.CombatCraftDefinitions != null;
            if (state.Payload is ProductionOutputClearanceCropHarvestExecutablePayload)
            {
                ready &= state.Narrative != null
                    && state.Proficiencies != null
                    && state.SurvivalEnvironment != null
                    && state.SurvivalDebug != null
                    && state.CropGenomeWitnesses != null
                    && state.GrandProjects != null
                    && state.GrandProjectBenefits != null
                    && ReferenceEquals(
                        state.GrandProjects,
                        state.GrandProjectBenefits);
            }
            return ready
                || Fail("special-natural-host-authority-missing", out failureReason);
        }

        private static bool TryProvisionMaximumOutputBenefits(
            PreparedState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (state?.Payload is not
                    ProductionOutputClearanceCropHarvestExecutablePayload crop
                || !crop.Indoor)
            {
                return true;
            }
            if (state.GrandProjects == null)
            {
                return Fail(
                    "special-natural-crop-grand-project-authority-missing",
                    out failureReason);
            }

            if (state.MaximumOutputBenefitsProvisioned)
            {
                return Fail(
                    "special-natural-crop-grand-project-maximum-already-provisioned",
                    out failureReason);
            }

            DungeonGrandProjectSaveData original = state.GrandProjects.Capture();
            if (!TryValidateGrandProjectPayload(
                    state,
                    original,
                    "baseline",
                    out failureReason))
            {
                return false;
            }
            if (!string.IsNullOrEmpty(original.state.activeProjectId)
                || original.state.pendingPhysicalCommit?.phase
                    != GrandProjectPhysicalCommitPhase.None)
            {
                return Fail(
                    "special-natural-crop-grand-project-baseline-not-quiescent",
                    out failureReason);
            }

            ProductionOutputFactor maximum = ProductionOutputFactorAuthority
                .ResolveMaximumGrandProject("crop-indoor");
            ProductionOutputFactor originalFactor = ProductionOutputFactorAuthority
                .ResolveCurrent(state.GrandProjectBenefits, "crop-indoor");
            if (originalFactor.Numerator == maximum.Numerator
                && originalFactor.Denominator == maximum.Denominator)
            {
                return true;
            }

            DungeonGrandProjectSaveData synthetic = state.GrandProjects.Capture();
            synthetic.state.completedProjectIds ??= new List<string>();
            if (!synthetic.state.completedProjectIds.Contains(
                    GrandProjectRuntime.IndoorFarmNetworkId,
                    StringComparer.Ordinal))
            {
                synthetic.state.completedProjectIds.Add(
                    GrandProjectRuntime.IndoorFarmNetworkId);
                synthetic.state.completedProjectIds = synthetic.state
                    .completedProjectIds
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToList();
            }
            if (!TryValidateGrandProjectPayload(
                    state,
                    synthetic,
                    "synthetic-maximum",
                    out failureReason))
            {
                return false;
            }

            state.OriginalGrandProjectSave = original;
            state.OriginalGrandProjectOutputFactor = originalFactor;
            state.MaximumOutputBenefitsProvisioned = true;
            try
            {
                state.GrandProjects.PublishRestoreCandidate(
                    state.GrandProjects.BuildRestore(synthetic));
            }
            catch (Exception exception)
            {
                state.MaximumOutputBenefitsProvisioned = false;
                state.OriginalGrandProjectSave = null;
                return Fail(
                    "special-natural-crop-grand-project-maximum-publish-failed:"
                    + exception.GetType().Name,
                    out failureReason);
            }

            ProductionOutputFactor current = ProductionOutputFactorAuthority
                .ResolveCurrent(state.GrandProjectBenefits, "crop-indoor");
            if (current.Numerator == maximum.Numerator
                && current.Denominator == maximum.Denominator)
            {
                return true;
            }

            _ = TryRestoreMaximumOutputBenefits(state, out _);
            return Fail(
                "special-natural-crop-grand-project-maximum-unreachable",
                out failureReason);
        }

        private static bool TryRestoreMaximumOutputBenefits(
            PreparedState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (state == null || !state.MaximumOutputBenefitsProvisioned)
                return true;
            DungeonGrandProjectSaveData original =
                state.OriginalGrandProjectSave;
            if (original == null
                || !TryValidateGrandProjectPayload(
                    state,
                    original,
                    "restore-original",
                    out failureReason))
            {
                return false;
            }
            try
            {
                state.GrandProjects.PublishRestoreCandidate(
                    state.GrandProjects.BuildRestore(original));
            }
            catch (Exception exception)
            {
                return Fail(
                    "special-natural-crop-grand-project-original-restore-failed:"
                    + exception.GetType().Name,
                    out failureReason);
            }

            ProductionOutputFactor restored = ProductionOutputFactorAuthority
                .ResolveCurrent(state.GrandProjectBenefits, "crop-indoor");
            ProductionOutputFactor expected =
                state.OriginalGrandProjectOutputFactor;
            if (restored.Numerator != expected.Numerator
                || restored.Denominator != expected.Denominator)
            {
                return Fail(
                    "special-natural-crop-grand-project-original-factor-mismatch",
                    out failureReason);
            }
            state.MaximumOutputBenefitsProvisioned = false;
            state.OriginalGrandProjectSave = null;
            return true;
        }

        private static bool TryValidateGrandProjectPayload(
            PreparedState state,
            DungeonGrandProjectSaveData payload,
            string stage,
            out string failureReason)
        {
            DungeonGameRestoreReport report = new();
            GrandProjectSaveValidation.Validate(
                payload,
                state.GrandProjects.Definitions,
                report);
            if (report.Success)
            {
                failureReason = string.Empty;
                return true;
            }
            return Fail(
                "special-natural-crop-grand-project-" + stage
                + "-invalid:" + string.Join("|", report.Errors),
                out failureReason);
        }

        private bool TryCaptureBaseline(
            PreparedState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            state.CheckpointTime = state.Clock.Time;
            state.CheckpointFrame = state.Clock.FrameCount;
            state.OriginalTimeScale = Time.timeScale;
            state.HasOriginalWeather = state.SurvivalEnvironment != null;
            if (state.HasOriginalWeather)
            {
                state.OriginalWeather = state.SurvivalEnvironment
                    .GetEnvironmentSnapshot().Weather;
            }
            state.ClockDiagnostics.RebaseDeterministicCheckpointTime(
                state.CheckpointTime,
                state.CheckpointFrame);
            state.Baseline = state.SaveRegistry.CaptureAll();
            state.BaselineFingerprint = ComputeTextSha256(
                CaptureRestoreStableWholeRootSaveFingerprint(state.Baseline));
            return (state.Baseline.Count > 0
                    && state.BaselineFingerprint.Length == 64)
                || Fail("special-natural-host-checkpoint-capture-failed",
                    out failureReason);
        }

        private static void EnsureNaturalMeasurementTimeScale()
        {
            if (!Mathf.Approximately(
                    Time.timeScale,
                    NaturalPortfolioAcceleratedTimeScale))
            {
                Time.timeScale = NaturalPortfolioAcceleratedTimeScale;
            }
        }

        private bool TryCreatePhysicalFixture(
            PreparedState state,
            out string failureReason)
        {
            failureReason = string.Empty;
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
                return Fail("special-natural-facility-definition-ambiguous",
                    out failureReason);
            }
            state.FacilityAsset = facilityMatches[0];
            if (!TryValidateFacilityDefinition(state, out failureReason))
                return false;

            BuildingSO warehouseAsset = buildings
                .Where(value => value.GetStorageCapacity() > 0
                    && value.StoresAllCategories()
                    && value.GetStorageMassCapacityGrams() > 0L)
                .OrderByDescending(value => value.GetStorageMassCapacityGrams())
                .ThenBy(value => ProductionFacilityDefinitionIdentity.Resolve(value),
                    StringComparer.Ordinal)
                .FirstOrDefault();
            if (warehouseAsset == null)
                return Fail("special-natural-warehouse-capacity-missing",
                    out failureReason);
            if (!TrySelectOutputWarehouseAsset(
                    state,
                    buildings,
                    out BuildingSO outputWarehouseAsset,
                    out failureReason))
            {
                return false;
            }

            CharacterActor placementActor = FindHauler();
            if (placementActor == null)
                return Fail("special-natural-placement-actor-missing",
                    out failureReason);
            state.PlacementActor = placementActor;
            IReadOnlyList<Vector2Int> cells = CaptureReachableFixtureCells(
                state.Grid,
                placementActor.GetNowXY());
            List<NaturalFixtureBuildingRequirement> coreNodes = new()
            {
                new NaturalFixtureBuildingRequirement(
                    "facility",
                    state.FacilityAsset,
                    NaturalFixtureNodeRole.Facility,
                    requireReachableWorkAccess: true,
                    requireUsableRoom: state.FacilityAsset.RequiresRoomRole())
            };
            coreNodes.Add(new NaturalFixtureBuildingRequirement(
                "input-warehouse",
                warehouseAsset,
                NaturalFixtureNodeRole.Warehouse,
                requireReachableWorkAccess: true));
            coreNodes.Add(new NaturalFixtureBuildingRequirement(
                "output-warehouse",
                outputWarehouseAsset,
                NaturalFixtureNodeRole.Warehouse,
                requireReachableWorkAccess: true));
            NaturalFixturePlacementResult corePlacement =
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
                                    SpecialNaturalFixturePlacementDebugRules
                                        .Instance);
                            }),
                        Rooms = state.Rooms,
                        ActorOrigin = placementActor.GetNowXY(),
                        CandidateAnchors = cells,
                        ReachableCells = cells
                            .Append(placementActor.GetNowXY())
                            .Distinct()
                            .ToArray(),
                        Nodes = coreNodes,
                        UtilityEdges = Array.Empty<
                            NaturalFixtureUtilityRequirement>(),
                        MaximumVisitedNodes = 100000
                    });
            NaturalFixturePlacementChoice inputWarehouseChoice = null;
            NaturalFixturePlacementChoice outputWarehouseChoice = null;
            bool warehouseChoicesReady = corePlacement.Succeeded
                && corePlacement.Plan.TryGetChoice(
                    "input-warehouse",
                    out inputWarehouseChoice)
                && corePlacement.Plan.TryGetChoice(
                    "output-warehouse",
                    out outputWarehouseChoice);
            if (!warehouseChoicesReady
                || !corePlacement.Plan.TryGetChoice(
                    "facility",
                    out NaturalFixturePlacementChoice facilityChoice))
            {
                return Fail(
                    "special-natural-core-joint-placement-failed"
                    + ";code=" + corePlacement.FailureCode
                    + ";visited=" + corePlacement.VisitedNodes,
                    out failureReason);
            }
            state.InputWarehouse = owner.CreateInjectedFacility(
                scope,
                state.Grid,
                warehouseAsset,
                inputWarehouseChoice.Anchor,
                "QA_Special_Natural_Input_Warehouse",
                registerOnGrid: true);
            if (state.InputWarehouse?.Inventory?.HasMassCapacityAuthority
                != true)
            {
                return Fail("special-natural-input-warehouse-create-failed",
                    out failureReason);
            }
            owner.RegisterTemporaryWarehouse(scope, state.InputWarehouse);
            state.InputWarehouseRegistered = true;
            ClearInventory(state.InputWarehouse.Inventory);
            state.InputWarehouseDestinationId = WarehouseStorageIdentity
                .RequireDestinationId(state.InputWarehouse);

            state.Warehouse = owner.CreateInjectedFacility(
                scope,
                state.Grid,
                outputWarehouseAsset,
                outputWarehouseChoice.Anchor,
                "QA_Special_Natural_Output_Warehouse",
                registerOnGrid: true);
            if (state.Warehouse?.Inventory?.HasMassCapacityAuthority != true)
                return Fail("special-natural-output-warehouse-create-failed",
                    out failureReason);
            owner.RegisterTemporaryWarehouse(scope, state.Warehouse);
            ClearInventory(state.Warehouse.Inventory);
            state.WarehouseDestinationId = WarehouseStorageIdentity
                .RequireDestinationId(state.Warehouse);
            if (!TrySuspendOriginalWarehouses(state, out failureReason))
                return false;

            state.Facility = owner.CreateInjectedFacility(
                scope,
                state.Grid,
                state.FacilityAsset,
                facilityChoice.Anchor,
                "QA_Special_Natural_Facility",
                registerOnGrid: true);
            if (state.Facility == null
                || !string.Equals(
                    state.Facility.GetProductionWorkstationTag(),
                    state.Request.Descriptor.Plan.WorkstationTag,
                    StringComparison.Ordinal))
            {
                return Fail("special-natural-facility-create-failed",
                    out failureReason);
            }

            if (!TryEnsureTargetUtilities(
                    state,
                    buildings,
                    cells,
                    state.Facility,
                    out failureReason)
                || !TryValidateUtilities(state, state.Facility, out failureReason)
                || !TryEnsurePayloadSupports(state, buildings, cells,
                    out failureReason)
                || !TryValidateLiveFacility(state, out failureReason))
            {
                return false;
            }

            if (state.Payload is ProductionOutputClearanceCropHarvestExecutablePayload
                    cropPayload)
            {
                if (!TryCreateMaximumCropWorker(state, cropPayload,
                        out failureReason))
                    return false;
            }
            else
            {
                state.Worker = placementActor;
            }

            state.CertifiedSeedOperatingDay = checked(Math.Max(
                1,
                state.Calendar.Current.AbsoluteDay + 1));
            return true;
        }

        private static bool TrySelectOutputWarehouseAsset(
            PreparedState state,
            IReadOnlyList<BuildingSO> buildings,
            out BuildingSO warehouseAsset,
            out string failureReason)
        {
            warehouseAsset = null;
            failureReason = string.Empty;
            if (state?.ItemRuntime?.CatalogProvider == null
                || state.Outputs == null
                || state.Outputs.Count == 0
                || buildings == null)
            {
                return Fail(
                    "special-natural-output-warehouse-selection-owner-missing",
                    out failureReason);
            }

            long requiredMassGrams = checked(state.Outputs.Sum(value =>
                value.MassGrams));
            StockCategory[] categories = state.Outputs
                .Select(value =>
                {
                    if (!state.ItemRuntime.CatalogProvider.TryGetDefinition(
                            value.ItemId,
                            out DungeonItemDefinition definition))
                    {
                        throw new InvalidOperationException(
                            "special-natural-output-item-definition-missing:"
                            + value.ItemId);
                    }
                    return definition.StockCategory;
                })
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            warehouseAsset = buildings
                .Where(value => value != null
                    && value.GetStorageCapacity() > 0
                    && value.GetStorageMassCapacityGrams()
                        >= requiredMassGrams
                    && (value.StoresAllCategories()
                        || categories.All(category =>
                            value.GetStorageCategory() == category)))
                .OrderBy(value => value.GetGridPosList(Vector2Int.zero).Count)
                .ThenBy(value => value.GetStorageMassCapacityGrams())
                .ThenBy(value => ProductionFacilityDefinitionIdentity.Resolve(
                    value), StringComparer.Ordinal)
                .FirstOrDefault();
            return warehouseAsset != null || Fail(
                "special-natural-output-warehouse-compatible-asset-missing",
                out failureReason);
        }

        private static bool TrySuspendOriginalWarehouses(
            PreparedState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (state?.World == null
                || state.InputWarehouse == null
                || state.Warehouse == null
                || state.WarehousesSuspended)
            {
                return Fail(
                    "special-natural-warehouse-isolation-owner-invalid",
                    out failureReason);
            }
            IWarehouseFacility[] originals = state.World.Warehouses
                .Where(value => value != null
                    && !ReferenceEquals(value, state.InputWarehouse)
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
            int expectedRetained = ReferenceEquals(
                state.InputWarehouse,
                state.Warehouse)
                ? 1
                : 2;
            bool exact = retained.Length == expectedRetained
                && retained.Any(value => ReferenceEquals(
                    value,
                    state.InputWarehouse))
                && retained.Any(value => ReferenceEquals(
                    value,
                    state.Warehouse));
            return exact || Fail(
                "special-natural-warehouse-isolation-not-exact:retained="
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
                    "special-natural-warehouse-restore-owner-invalid",
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
                "special-natural-warehouse-restore-not-exact",
                out failureReason);
        }

        private bool TryValidateFacilityDefinition(
            PreparedState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            switch (state.Payload)
            {
                case ProductionOutputClearanceCombatCraftExecutablePayload combat:
                    CombatCraftFacilityEligibilitySnapshot eligibility;
                    try
                    {
                        eligibility = CombatCraftFacilityEligibility.Capture(
                            state.FacilityAsset,
                            state.CombatCraftDefinitions);
                    }
                    catch
                    {
                        return Fail("special-natural-combat-facility-invalid",
                            out failureReason);
                    }
                    if (!eligibility.Contains(combat.CraftDefinitionId))
                        return Fail("special-natural-combat-facility-not-eligible",
                            out failureReason);
                    if (state.CombatCatalog?.TryGet(
                            combat.CraftDefinitionId,
                            out CombatEquipmentDefinitionSO equipment) == true
                        && !string.IsNullOrEmpty(equipment.RequiredResearchId))
                    {
                        if (!state.Progression.BlueprintResearch
                            .TryCompleteProjectImmediatelyForVerification(
                                new ResearchProjectId(equipment.RequiredResearchId),
                                out string completionFailure))
                        {
                            return Fail(
                                "special-natural-combat-research-completion-failed:"
                                + completionFailure,
                                out failureReason);
                        }
                    }
                    return true;

                case ProductionOutputClearanceApparelExecutablePayload:
                    return ApparelTailoringFacilityEligibility.IsEligible(
                            state.FacilityAsset)
                        || Fail("special-natural-apparel-facility-not-eligible",
                            out failureReason);

                case ProductionOutputClearanceCropHarvestExecutablePayload crop:
                    BuildingCropPlotAbility cropAbility = state.FacilityAsset
                        .GetAbility<BuildingCropPlotAbility>();
                    if (cropAbility == null || cropAbility.Indoor != crop.Indoor
                        || !state.Content.TryGetCrop(crop.CropId, out state.Crop)
                        || state.Crop == null)
                    {
                        return Fail("special-natural-crop-facility-not-eligible",
                            out failureReason);
                    }
                    if (!string.IsNullOrEmpty(state.Crop.RequiredResearchId))
                    {
                        if (!state.Progression.BlueprintResearch
                            .TryCompleteProjectImmediatelyForVerification(
                                new ResearchProjectId(state.Crop.RequiredResearchId),
                                out string completionFailure))
                        {
                            return Fail(
                                "special-natural-crop-research-completion-failed:"
                                + completionFailure,
                                out failureReason);
                        }
                    }
                    return true;

                case ProductionOutputClearanceCertifiedSeedExecutablePayload:
                    return CertifiedSeedFacilityEligibility.IsEligible(
                            state.FacilityAsset)
                        || Fail("special-natural-certified-facility-not-eligible",
                            out failureReason);

                default:
                    return Fail("special-natural-host-payload-mismatch",
                        out failureReason);
            }
        }

        private bool TryEnsurePayloadSupports(
            PreparedState state,
            IReadOnlyList<BuildingSO> buildings,
            IReadOnlyList<Vector2Int> cells,
            out string failureReason)
        {
            failureReason = string.Empty;
            List<ResearchFacilityCommandKind> commands = new();
            if (state.Payload is ProductionOutputClearanceCombatCraftExecutablePayload
                    combat
                && state.CombatCatalog?.TryGet(
                    combat.CraftDefinitionId,
                    out CombatEquipmentDefinitionSO equipment) == true
                && string.Equals(
                    equipment.RequiredResearchId,
                    "research:equipment:weapon-patterns",
                    StringComparison.Ordinal))
            {
                commands.Add(ResearchFacilityCommandKind.WeaponPatternAccess);
            }
            if (state.Payload is ProductionOutputClearanceCropHarvestExecutablePayload
                    crop)
            {
                commands.AddRange(RequiredCropSupportCommands(crop.Indoor));
            }

            foreach (ResearchFacilityCommandKind command in commands
                         .Distinct()
                         .OrderBy(value => value))
            {
                BuildableObject[] existing = state.FacilityCapabilities
                    .FindOperational(command)
                    .ToArray();
                if (existing.Length > 0)
                {
                    bool utilityExact = false;
                    foreach (BuildableObject candidate in existing)
                    {
                        if (TryValidateUtilities(
                                state,
                                candidate,
                                out string utilityFailure))
                        {
                            utilityExact = true;
                            break;
                        }
                        failureReason = utilityFailure;
                    }
                    if (!utilityExact)
                        return false;
                    continue;
                }
                BuildingSO[] matches = buildings.Where(value =>
                        value.ResearchFacilityCommand == command)
                    .ToArray();
                if (matches.Length != 1)
                    return Fail("special-natural-support-definition-ambiguous",
                        out failureReason);
                BuildingSO supportAsset = matches[0];
                Vector2Int[] usableRoomCells = cells.Where(cell =>
                        state.Rooms.TryGetRoom(
                            state.Grid,
                            cell,
                            out RoomInstance room)
                        && room != null
                        && room.IsUsable
                        && supportAsset.GetGridPosList(cell).All(footprint =>
                            state.Rooms.TryGetRoom(
                                state.Grid,
                                footprint,
                                out RoomInstance footprintRoom)
                            && footprintRoom != null
                            && footprintRoom.Id == room.Id))
                    .ToArray();
                if (!TryFindRegisterablePosition(
                        state.Grid,
                        supportAsset,
                        usableRoomCells,
                        out Vector2Int supportPosition))
                {
                    return Fail("special-natural-support-space-missing",
                        out failureReason);
                }
                Facility support = owner.CreateInjectedFacility(
                    scope,
                    state.Grid,
                    supportAsset,
                    supportPosition,
                    "QA_Special_Natural_Support_" + command,
                    registerOnGrid: true);
                if (support == null
                    || !TryEnsureTargetUtilities(
                        state,
                        buildings,
                        cells,
                        support,
                        out failureReason)
                    || !TryValidateUtilities(state, support, out failureReason)
                    || state.FacilityCapabilities.FindOperational(command)
                        .Count(value => ReferenceEquals(value, support)) != 1)
                {
                    return failureReason.Length > 0
                        ? false
                        : Fail("special-natural-support-not-operational",
                            out failureReason);
                }
                state.CreatedSupports.Add(support);
            }
            return true;
        }

        private bool TryEnsureTargetUtilities(
            PreparedState state,
            IReadOnlyList<BuildingSO> buildings,
            IReadOnlyList<Vector2Int> cells,
            Facility target,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (target?.BuildingData == null)
                return Fail("special-natural-utility-target-missing",
                    out failureReason);

            BuildingSO powerSource = buildings
                .Where(value => value != null
                    && value.GetAbility<BuildingPowerProducerAbility>() is
                        BuildingPowerProducerAbility producer
                    && producer.productionPerSecond > 0f
                    && !producer.requiresFuel)
                .OrderBy(value => value.GetGridPosList(Vector2Int.zero).Count)
                .ThenBy(value => ProductionFacilityDefinitionIdentity.Resolve(value),
                    StringComparer.Ordinal)
                .FirstOrDefault();
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

            BuildingProcessFluidAbility process = target.BuildingData
                .GetAbility<BuildingProcessFluidAbility>();
            BuildingProductionSupportAbility support = target.BuildingData
                .GetProductionSupportAbility();
            float cleanWater = Math.Max(0f,
                support?.cleanWaterPerCycle ?? 0f);
            float wastewater = Math.Max(0f,
                support?.wastewaterPerCycle ?? 0f);
            // Only the crop payload uses this generic facility process-fluid
            // contract. Reserve the exact authored sow/harvest cycles; a fluid
            // ability attached for an unrelated work type is not a demand.
            if (state.Payload is ProductionOutputClearanceCropHarvestExecutablePayload
                && process != null)
            {
                int processCycles = 0;
                if (process.Supports(BuiltInWorkTypeIds.Sow))
                    processCycles++;
                if (process.Supports(BuiltInWorkTypeIds.Harvest))
                    processCycles++;
                cleanWater += Math.Max(0f, process.cleanWaterPerCycle)
                    * processCycles;
                wastewater += Math.Max(0f, process.wastewaterPerCycle)
                    * processCycles;
            }

            if (target.BuildingData.GetAbility<BuildingPowerConsumerAbility>() != null
                && !state.Power.IsPowered(target))
            {
                if (powerSource == null
                    || !TryCreateAdjacentUtility(
                        state,
                        powerSource,
                        target,
                        cells,
                        "QA_Special_Natural_Power",
                        out _))
                {
                    return Fail("special-natural-power-topology-failed",
                        out failureReason);
                }
            }

            if (cleanWater > 0f)
            {
                if (!HasUtilityChannel(target, UtilityChannel.CleanWater)
                    || cleanStorage == null
                    || !TryCreateAdjacentUtility(
                        state,
                        cleanStorage,
                        target,
                        cells,
                        "QA_Special_Natural_CleanWater",
                        out BuildableObject storage)
                    || !state.Fluid.TryAdd(
                        storage,
                        WorldWaterQuality.Clean,
                        cleanWater,
                        out float accepted)
                    || accepted + 0.0001f < cleanWater
                    || !state.Fluid.CanConsume(
                        target,
                        WorldWaterQuality.Clean,
                        cleanWater,
                        out _))
                {
                    return Fail("special-natural-clean-water-topology-failed",
                        out failureReason);
                }
            }

            if (wastewater > 0f
                && (!HasUtilityChannel(target, UtilityChannel.Wastewater)
                    || wasteStorage == null
                    || !TryCreateAdjacentUtility(
                        state,
                        wasteStorage,
                        target,
                        cells,
                        "QA_Special_Natural_Wastewater",
                        out _)
                    || !state.Wastewater.CanAcceptWastewater(
                        target,
                        wastewater,
                        out _)))
            {
                return Fail("special-natural-wastewater-topology-failed",
                    out failureReason);
            }
            return true;
        }

        private bool TryCreateAdjacentUtility(
            PreparedState state,
            BuildingSO asset,
            Facility target,
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
            utility = state.BuildingFactory.Create(state.Grid, asset, position);
            if (utility == null)
                return false;
            utility.gameObject.name = objectName;
            owner.temporaryObjects.Add(utility.gameObject);
            InjectGameObject(scope, utility.gameObject);
            utility.SetGrid(state.Grid);
            utility.Initialization(asset, position);
            if (state.Grid.RegisterOccupant(
                    utility,
                    asset.Placement.Layer,
                    asset.GetGridPosList(position),
                    asset.Placement.IsMovement))
            {
                return true;
            }
            owner.temporaryObjects.Remove(utility.gameObject);
            UnityEngine.Object.Destroy(utility.gameObject);
            utility = null;
            return false;
        }

        private static bool HasUtilityChannel(
            BuildableObject building,
            UtilityChannel channel)
        {
            if (building?.BuildingData == null)
                return false;
            BuildingSO data = building.BuildingData;
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
            return (channels & channel) != 0;
        }

        private static ResearchFacilityCommandKind[] RequiredCropSupportCommands(
            bool indoor)
        {
            List<ResearchFacilityCommandKind> commands = new()
            {
                ResearchFacilityCommandKind.SoilDiagnostics,
                ResearchFacilityCommandKind.SeedSelection,
                ResearchFacilityCommandKind.CropCalendar
            };
            if (indoor)
                commands.Add(ResearchFacilityCommandKind.ClimateControl);
            return commands.ToArray();
        }

        private bool TryValidateLiveFacility(
            PreparedState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            return state.Payload switch
            {
                ProductionOutputClearanceApparelExecutablePayload =>
                    ApparelTailoringFacilityEligibility.FindOperational(
                            state.FacilityCapabilities)
                        .Count(value => ReferenceEquals(value, state.Facility)) == 1
                    || Fail("special-natural-apparel-facility-not-operational",
                        out failureReason),
                ProductionOutputClearanceCertifiedSeedExecutablePayload =>
                    CertifiedSeedFacilityEligibility.FindOperational(
                            state.FacilityCapabilities)
                        .Count(value => ReferenceEquals(value, state.Facility)) == 1
                    || Fail("special-natural-certified-facility-not-operational",
                        out failureReason),
                _ => true
            };
        }

        private bool TryValidateUtilities(
            PreparedState state,
            BuildableObject facility,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (facility?.BuildingData == null)
                return Fail("special-natural-utility-facility-missing",
                    out failureReason);
            BuildingPowerConsumerAbility power = facility.BuildingData
                .GetAbility<BuildingPowerConsumerAbility>();
            if (power != null
                && (state.Power == null || !state.Power.IsPowered(facility)))
            {
                return Fail("special-natural-powered-facility-unavailable",
                    out failureReason);
            }
            BuildingProcessFluidAbility fluid = facility.BuildingData
                .GetAbility<BuildingProcessFluidAbility>();
            bool requiresCropFluid = state.Payload is
                    ProductionOutputClearanceCropHarvestExecutablePayload
                && fluid != null
                && fluid.cleanWaterPerCycle > 0f
                && (fluid.Supports(BuiltInWorkTypeIds.Sow)
                    || fluid.Supports(BuiltInWorkTypeIds.Harvest));
            if (requiresCropFluid
                && (state.Fluid == null
                    || !state.Fluid.CanConsume(
                        facility,
                        fluid.minimumQuality,
                        fluid.cleanWaterPerCycle,
                        out _)))
            {
                return Fail("special-natural-water-supply-unavailable",
                    out failureReason);
            }
            return true;
        }

        private bool TryCreateMaximumCropWorker(
            PreparedState state,
            ProductionOutputClearanceCropHarvestExecutablePayload payload,
            out string failureReason)
        {
            failureReason = string.Empty;
            string plotId = state.Facility.PersistentInstanceId.Value;
            string actorId = Enumerable.Range(0, 10_000)
                .Select(index => "character:qa:special-crop-natural:"
                    + index.ToString("D4"))
                .FirstOrDefault(value =>
                    GoldenHarvestDeterministicOutcomeAuthority.CaptureRoll01(
                        unchecked((ulong)(uint)state.RunSeed.RunSeed),
                        plotId,
                        0,
                        value) < 0.12f);
            if (string.IsNullOrEmpty(actorId))
                return Fail("special-natural-crop-golden-key-unreachable",
                    out failureReason);

            GameObject actorObject = CharacterAiPlanDebugFixtures.CreateActorObject(
                "Special Crop Natural Witness");
            if (actorObject == null)
                return Fail("special-natural-crop-worker-create-failed",
                    out failureReason);
            actorObject.SetActive(false);
            if (actorObject.GetComponent<AbilityWork>() == null)
                actorObject.AddComponent<AbilityWork>();
            owner.temporaryObjects.Add(actorObject);
            InjectGameObject(scope, actorObject);
            CharacterSO actorData = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(
                    CharacterType.NPC,
                    "Special Crop Natural Witness",
                    "Beastkin");
            state.CropActorObject = actorObject;
            state.CropActorData = actorData;
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            if (actor == null)
            {
                return Fail("special-natural-crop-worker-create-failed",
                    out failureReason);
            }
            CharacterDialogueRuntime dialogue =
                actorObject.GetComponent<CharacterDialogueRuntime>();
            if (dialogue != null)
                dialogue.enabled = false;
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId(new CharacterId(actorId));
            state.Narrative.Register(
                new CharacterId(actorId),
                new CharacterSpeciesId("Beastkin"),
                Array.Empty<string>(),
                Array.Empty<string>(),
                BuiltInCharacterProficiencyIds.All.Select(id =>
                    new CharacterStartingProficiencyExperience
                    {
                        proficiencyId = id.Value,
                        experience = 100,
                        learningMultiplier = 1f
                    }).ToArray());
            actorObject.SetActive(true);
            actor.RefreshAbilityCache();
            actor.Initialize(actorData);
            actor.Brain.UseStaffWorkActions();
            actor.Brain.ConfigureLogisticsMeasurementForDiagnostics(true);
            actorObject.transform.position = state.Grid.GetWorldPos(
                state.PlacementActor.GetNowXY());
            actor.Progression.ApplyPreparedIdentity(
                "Special Crop Natural Witness",
                "Beastkin",
                new[]
                {
                    NaturalGoldenHarvestReachableMaximumWitnessContributor
                        .GoldenHarvestTraitId
                },
                CharacterPotentialGrade.Ordinary,
                state.Request.Fixture.DeterministicSeed,
                autoChooseDrafts: false);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.SetAiPaused(true);
            CharacterId characterId = CharacterPersistentIdentity.Require(actor);
            state.Proficiencies.AddDirectExperience(
                characterId,
                BuiltInCharacterProficiencyIds.FoodProduction,
                3060f,
                state.Calendar.AbsoluteHour,
                applyLearningMultiplier: false);
            state.Proficiencies.AddDirectExperience(
                characterId,
                BuiltInCharacterProficiencyIds.Fieldwork,
                3060f,
                state.Calendar.AbsoluteHour,
                applyLearningMultiplier: false);
            CropGenomeReachableMaximumWitnessSnapshot genomeWitness;
            try
            {
                genomeWitness = state.CropGenomeWitnesses.Capture(
                    payload.CropId);
            }
            catch
            {
                return Fail("special-natural-crop-maximum-genome-missing",
                    out failureReason);
            }
            state.CropGenomeId = genomeWitness.GenomeId;
            state.CropGenomeWitness = genomeWitness;
            state.Worker = actor;
            return true;
        }

        private bool TryProvisionExactInputs(
            PreparedState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            foreach (ProductionOutputClearanceExecutableInput input in
                     state.Inputs.OrderBy(value => value.ItemId,
                         StringComparer.Ordinal))
            {
                int before = GetStoredItemQuantity(
                    state.ItemRuntime,
                    input.ItemId,
                    state.InputWarehouse.centerPos);
                bool spawned;
                int amount;
                if (state.Payload is
                        ProductionOutputClearanceCertifiedSeedExecutablePayload certified
                    && string.Equals(input.ItemId, certified.SeedItemId,
                        StringComparison.Ordinal))
                {
                    spawned = state.Transfers.TrySpawnItemWithComponents(
                        input.ItemId,
                        input.Quantity,
                        state.InputWarehouse.centerPos,
                        WorldItemStackState.Stored,
                        state.InputWarehouseDestinationId,
                        new[]
                        {
                            SeedLotItemStateCodec.Encode(
                                certified.InputSeedLot.CreateState())
                        },
                        out amount);
                }
                else if (state.Payload is
                             ProductionOutputClearanceCropHarvestExecutablePayload crop
                    && state.Crop != null
                    && string.Equals(input.ItemId, state.Crop.SeedItemId,
                        StringComparison.Ordinal))
                {
                    if (!TryForbidCompetingCropSeedLots(
                            state,
                            crop.CropId,
                            state.CropGenomeWitness,
                            out failureReason))
                    {
                        return false;
                    }
                    spawned = state.Transfers.TrySpawnItemWithComponents(
                        input.ItemId,
                        input.Quantity,
                        state.InputWarehouse.centerPos,
                        WorldItemStackState.Stored,
                        state.InputWarehouseDestinationId,
                        new[]
                        {
                            SeedLotItemStateCodec.Encode(
                                state.CropGenomeWitness
                                    .CreatePhysicalSeedLot())
                        },
                        out amount);
                }
                else
                {
                    spawned = state.ItemRuntime.SpawnItemAt(
                        input.ItemId,
                        input.Quantity,
                        state.InputWarehouse.centerPos,
                        WorldItemStackState.Stored,
                        state.InputWarehouseDestinationId,
                        out amount);
                }
                int after = GetStoredItemQuantity(
                    state.ItemRuntime,
                    input.ItemId,
                    state.InputWarehouse.centerPos);
                if (!spawned || amount != input.Quantity
                    || after - before != input.Quantity)
                {
                    return Fail("special-natural-input-provision-failed",
                    out failureReason);
                }
            }
            bool requiresExactInputIsolation = state.Payload is
                    ProductionOutputClearanceCropHarvestExecutablePayload
                || state.Payload is
                    ProductionOutputClearanceCertifiedSeedExecutablePayload;
            if (requiresExactInputIsolation
                && !TryIsolateExactSpecialNaturalInputStacks(
                    state,
                    out failureReason))
            {
                return false;
            }
            return true;
        }

        private static bool TryIsolateExactSpecialNaturalInputStacks(
            PreparedState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (state?.ItemRuntime == null
                || state.InputWarehouse == null
                || string.IsNullOrEmpty(state.InputWarehouseDestinationId))
            {
                return Fail(
                    "special-natural-input-isolation-owner-missing",
                    out failureReason);
            }
            Dictionary<string, int> requiredByItem = state.Inputs
                .GroupBy(value => value.ItemId, StringComparer.Ordinal)
                .ToDictionary(
                    value => value.Key,
                    value => checked(value.Sum(input => input.Quantity)),
                    StringComparer.Ordinal);
            WorldItemStackSnapshot[] exactInputs = state.ItemRuntime.GetAllStacks()
                .Where(value => value != null
                    && value.Quantity > 0
                    && value.State == WorldItemStackState.Stored
                    && value.Position == state.InputWarehouse.centerPos
                    && string.Equals(
                        value.DestinationId,
                        state.InputWarehouseDestinationId,
                        StringComparison.Ordinal)
                    && requiredByItem.ContainsKey(value.ItemId))
                .OrderBy(value => value.StackId, StringComparer.Ordinal)
                .ToArray();
            bool exactVector = requiredByItem.All(requirement =>
                exactInputs.Where(value => string.Equals(
                        value.ItemId,
                        requirement.Key,
                        StringComparison.Ordinal))
                    .Sum(value => value.Quantity) == requirement.Value);
            if (!exactVector)
            {
                return Fail(
                    "special-natural-input-isolation-vector-mismatch",
                    out failureReason);
            }
            HashSet<string> allowed = exactInputs
                .Select(value => value.StackId)
                .ToHashSet(StringComparer.Ordinal);
            WorldItemStackSnapshot[] ambient = state.ItemRuntime.GetAllStacks()
                .Where(value => value != null
                    && value.Quantity > 0
                    && !value.Forbidden
                    && !allowed.Contains(value.StackId)
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
                        "special-natural-input-isolation-forbid-failed:"
                        + stack.StackId,
                        out failureReason);
                }
            }
            return true;
        }

        private static bool TryForbidCompetingCropSeedLots(
            PreparedState state,
            string cropId,
            CropGenomeReachableMaximumWitnessSnapshot maximumWitness,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (state?.ItemRuntime == null
                || state.Crop == null
                || maximumWitness == null
                || !string.Equals(
                    maximumWitness.CropId,
                    cropId,
                    StringComparison.Ordinal))
            {
                return Fail(
                    "special-natural-crop-seed-isolation-invalid",
                    out failureReason);
            }

            foreach (WorldItemStackSnapshot stack in state.ItemRuntime
                         .GetAllStacks()
                         .Where(value => value != null
                             && value.Quantity > 0
                             && !value.Forbidden
                             && value.State is WorldItemStackState.Loose
                                 or WorldItemStackState.Stored
                             && string.Equals(
                                 value.ItemId,
                                 state.Crop.SeedItemId,
                                 StringComparison.Ordinal))
                         .OrderBy(value => value.StackId, StringComparer.Ordinal))
            {
                SeedLotState seedLot;
                try
                {
                    seedLot = SeedLotItemStateCodec.Decode(stack.Components);
                }
                catch
                {
                    continue;
                }
                if (seedLot == null
                    || !string.Equals(
                        seedLot.cropId,
                        cropId,
                        StringComparison.Ordinal)
                    || string.Equals(
                        seedLot.cultivarGenomeId,
                        maximumWitness.GenomeId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                if (!state.ItemRuntime.SetForbidden(stack.StackId, true))
                {
                    return Fail(
                        "special-natural-crop-competing-seed-forbid-failed:"
                        + stack.StackId,
                        out failureReason);
                }
            }
            return true;
        }

        private void WakeHaulers()
        {
            foreach (CharacterActor actor in owner.verificationActors
                         .Where(value => value != null && !value.IsDead))
            {
                AIBrain brain = actor.Brain;
                AIHaul haul = brain?.availableActions?
                    .Select(value => value?.actionset)
                    .OfType<AIHaul>()
                    .FirstOrDefault();
                if (brain == null || haul == null || brain.HasRunningAction)
                    continue;
                if (!brain.PreferActionOnNextDecision<AIHaul>(180f))
                    continue;
                actor.SetAiPaused(false);
                brain.RequestImmediateReplan(clearFailures: true);
            }
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
            HashSet<string> allowedCommits = new(
                receipt.RouteBatchCommitIds,
                StringComparer.Ordinal);
            FacilityOutputExactRoutePendingSnapshot[] ownedRoutes =
                state.ExactRoutes
                .CapturePendingRoutes()
                .Where(value => value?.Receipt != null
                    && value.Phase == FacilityOutputExactRoutePhase.Routable
                    && allowedCommits.Contains(value.Receipt.BatchCommitId)
                    && string.Equals(
                        value.DeliveryRevision.TargetDestinationId,
                        state.WarehouseDestinationId,
                        StringComparison.Ordinal))
                .OrderBy(
                    value => value.Receipt.BatchCommitId,
                    StringComparer.Ordinal)
                .ToArray();
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
                // Special-domain publication acknowledges the physical
                // FacilityBuffer batch and releases the same committed stack
                // to the ordinary AIHaul scheduler.  At this boundary the
                // warehouse destination may still be unassigned; the
                // downstream scheduler witness must prove the exact committed
                // intent and delivery.  Never accept an already-Stored stack,
                // because that would hide a production/measurement race.
                WorldItemStackSnapshot[] directlyRouted = state.ItemRuntime
                    .GetAllStacks()
                    .Where(value => value != null
                        && sourceByStack.ContainsKey(value.StackId)
                        && value.State == WorldItemStackState.Loose
                        && (string.IsNullOrEmpty(value.DestinationId)
                            || string.Equals(
                                value.DestinationId,
                                state.WarehouseDestinationId,
                                StringComparison.Ordinal)))
                    .OrderBy(value => value.StackId, StringComparer.Ordinal)
                    .ToArray();
                if (directlyRouted.Length != sourceByStack.Count)
                    return false;
                foreach (WorldItemStackSnapshot liveStack in directlyRouted)
                {
                    ProductionOutputClearanceExecutionOutputSliceSnapshot expected =
                        sourceByStack[liveStack.StackId];
                    if (!string.Equals(
                            liveStack.ItemId,
                            expected.ItemId,
                            StringComparison.Ordinal)
                        || !string.Equals(
                            liveStack.ItemInstanceId ?? string.Empty,
                            expected.ItemInstanceId,
                            StringComparison.Ordinal)
                        || liveStack.Quantity != expected.Quantity
                        || !TryCaptureLiveStackMassGrams(
                            liveStack,
                            out long liveMassGrams)
                        || liveMassGrams != expected.MassGrams)
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
                    != allowedCommits.Count
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
                if (owned.Sum(value => value.RoutedQuantity) != pair.Value.Quantity
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
                    && sourceByStack.TryGetValue(
                        route.SourceStackId,
                        out ProductionOutputClearanceExecutionOutputSliceSnapshot
                            expected)
                    && string.Equals(stack.ItemId, route.ItemId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        stack.ItemInstanceId ?? string.Empty,
                        expected.ItemInstanceId,
                        StringComparison.Ordinal)
                    && stack.Quantity == route.RoutedQuantity
                    && TryCaptureLiveStackMassGrams(
                        stack,
                        out long liveMassGrams)
                    && liveMassGrams == route.RoutedMassGrams
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

        private static string CaptureExactRouteDiagnostics(
            PreparedState state,
            ProductionOutputClearanceExecutionReceiptSnapshot receipt)
        {
            HashSet<string> receiptStackIds = receipt.Outputs
                .Select(value => value.StackId)
                .ToHashSet(StringComparer.Ordinal);
            string receiptSummary = string.Join(
                ",",
                receipt.Outputs
                    .OrderBy(value => value.StackId, StringComparer.Ordinal)
                    .Select(value => string.Join(
                        ":",
                        DiagnosticToken(value.StackId),
                        DiagnosticToken(value.ItemId),
                        value.Quantity,
                        value.MassGrams)));
            string commitSummary = string.Join(
                ",",
                receipt.RouteBatchCommitIds
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .Select(DiagnosticToken));
            string routeSummary = string.Join(
                ",",
                state.ExactRoutes.CapturePendingRoutes()
                    .Where(value => value?.Receipt != null)
                    .OrderBy(value => value.Receipt.RouteOperationId,
                        StringComparer.Ordinal)
                    .Select(value => string.Join(
                        ":",
                        DiagnosticToken(value.Receipt.RouteOperationId),
                        value.Phase,
                        DiagnosticToken(value.Receipt.BatchCommitId),
                        DiagnosticToken(value.Receipt.SourceDestinationId),
                        DiagnosticToken(value.Receipt.TargetDestinationId),
                        DiagnosticToken(
                            value.DeliveryRevision.TargetDestinationId),
                        string.Join(
                            "+",
                            value.Receipt.Slices
                                .OrderBy(slice => slice.SourceStackId,
                                    StringComparer.Ordinal)
                                .Select(slice =>
                                    DiagnosticToken(slice.SourceStackId)
                                    + ">"
                                    + DiagnosticToken(slice.RoutedStackId))))));
            string stackSummary = string.Join(
                ",",
                state.ItemRuntime.GetAllStacks()
                    .Where(value => value != null
                        && (receiptStackIds.Contains(value.StackId)
                            || value.State ==
                                WorldItemStackState.FacilityOutputBuffer))
                    .OrderBy(value => value.StackId, StringComparer.Ordinal)
                    .Select(value => string.Join(
                        ":",
                        DiagnosticToken(value.StackId),
                        DiagnosticToken(value.ItemId),
                        value.State,
                        value.Quantity,
                        value.ReservedQuantity,
                        DiagnosticToken(value.DestinationId),
                        value.HasDestinationPosition
                            ? value.DestinationPosition.x + "_"
                                + value.DestinationPosition.y
                            : "none")));
            return string.Join(
                ";",
                "warehouse=" + DiagnosticToken(state.WarehouseDestinationId),
                "commits=" + (commitSummary.Length == 0
                    ? "none"
                    : commitSummary),
                "receipt=" + (receiptSummary.Length == 0
                    ? "none"
                    : receiptSummary),
                "routes=" + (routeSummary.Length == 0
                    ? "none"
                    : routeSummary),
                "stacks=" + (stackSummary.Length == 0
                    ? "none"
                    : stackSummary));
        }

        private static bool TryCaptureLiveStackMassGrams(
            WorldItemStackSnapshot stack,
            out long massGrams)
        {
            massGrams = 0L;
            if (stack == null || stack.Quantity <= 0)
                return false;
            try
            {
                massGrams = PhysicalMassGrams
                    .FromCanonicalKilograms(stack.UnitWeight)
                    .Multiply(stack.Quantity)
                    .Value;
                return massGrams > 0L;
            }
            catch (Exception exception) when (exception is ArgumentException
                || exception is InvalidOperationException
                || exception is OverflowException)
            {
                massGrams = 0L;
                return false;
            }
        }

        private static NaturalClearanceSeedRunState CreateRunState(
            PreparedState state,
            ProductionOutputClearanceExecutionReceiptSnapshot receipt)
        {
            return new NaturalClearanceSeedRunState
            {
                SeedIndex = state.Request.Fixture.SeedIndex,
                DeterministicSeed = state.Request.Fixture.DeterministicSeed,
                RuntimeFacilityId = state.Facility.PersistentInstanceId.Value,
                DefinitionId = state.Request.Descriptor.Plan.DefinitionId,
                WorkstationTag = state.Request.Descriptor.Plan.WorkstationTag,
                RecipeId = ResolveBranchId(state.Payload),
                OutputLineId = string.Join(
                    "+",
                    receipt.Outputs.Select(value => value.OutputLineId)
                        .OrderBy(value => value, StringComparer.Ordinal)),
                ItemId = string.Join(
                    "+",
                    receipt.Outputs.Select(value => value.ItemId)
                        .OrderBy(value => value, StringComparer.Ordinal)),
                OutputQuantity = receipt.Outputs.Sum(value => value.Quantity),
                BatchMassGrams = receipt.ActualBatchMassGrams
            };
        }

        private bool TryRequireActive(
            ProductionOutputClearanceNaturalExecutionRequest request,
            ProductionOutputClearanceNaturalPreparedScenario scenario,
            out PreparedState state,
            out string failureReason)
        {
            state = active;
            failureReason = string.Empty;
            bool exact = state != null
                && ReferenceEquals(state.Request, request)
                && ReferenceEquals(state.Scenario, scenario);
            return exact || Fail("special-natural-host-owner-mismatch",
                out failureReason);
        }

        private bool RestoreAndRelease(
            PreparedState state,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (state == null || state.SaveRegistry == null
                || state.Baseline == null)
            {
                active = null;
                return Fail("special-natural-host-restore-authority-missing",
                    out failureReason);
            }
            bool suspendedWarehousesRestored = RestoreSuspendedWarehouses(
                state,
                out string warehouseRestoreFailure);
            owner.RestoreBrain();
            Time.timeScale = state.OriginalTimeScale;
            if (state.HasOriginalWeather && state.SurvivalDebug != null)
                state.SurvivalDebug.DebugSetWeather(state.OriginalWeather);
            state.ClockDiagnostics.RebaseDeterministicCheckpointTime(
                state.CheckpointTime,
                state.CheckpointFrame);
            DungeonGameRestoreReport report = new();
            bool restored = state.SaveRegistry.RestoreAll(state.Baseline, report)
                && report.Success;
            state.ClockDiagnostics.RebaseDeterministicCheckpointTime(
                state.CheckpointTime,
                state.CheckpointFrame);
            List<DungeonSaveSectionEnvelope> recaptured = restored
                ? state.SaveRegistry.CaptureAll()
                : new List<DungeonSaveSectionEnvelope>();
            string restoredFingerprint = restored
                ? ComputeTextSha256(
                    CaptureRestoreStableWholeRootSaveFingerprint(recaptured))
                : string.Empty;
            bool exact = restored && string.Equals(
                restoredFingerprint,
                state.BaselineFingerprint,
                StringComparison.Ordinal);
            bool fixtureReleased = !UnityEngine.Object
                .FindObjectsByType<BuildableObject>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Any(value => value != null
                    && value.name.StartsWith(
                        "QA_Special_Natural_",
                        StringComparison.Ordinal));
            string restoreDifference = restored
                ? DescribeRestoreStableWholeRootSaveDifference(
                    state.Baseline,
                    recaptured)
                : "recapture-skipped";
            string restoreErrors = report.Errors.Count == 0
                ? "none"
                : string.Join(" || ", report.Errors);
            string leakedFixtures = fixtureReleased
                ? "none"
                : string.Join(
                    ",",
                    UnityEngine.Object.FindObjectsByType<BuildableObject>(
                            FindObjectsInactive.Include,
                            FindObjectsSortMode.None)
                        .Where(value => value != null
                            && value.name.StartsWith(
                                "QA_Special_Natural_",
                                StringComparison.Ordinal))
                        .Select(value => value.name)
                        .OrderBy(value => value, StringComparer.Ordinal));
            if (state.CropActorData != null)
                UnityEngine.Object.Destroy(state.CropActorData);
            if (state.CropActorObject != null)
            {
                owner.temporaryObjects.Remove(state.CropActorObject);
                UnityEngine.Object.Destroy(state.CropActorObject);
            }
            if (fixtureReleased)
                owner.DiscardRestoredPreparedOutputFixtureReferences();
            owner.activeNaturalClearanceSeedRun = null;
            active = null;
            return exact && fixtureReleased && suspendedWarehousesRestored
                || Fail(
                    (!suspendedWarehousesRestored
                        ? warehouseRestoreFailure
                        : fixtureReleased
                        ? "special-natural-host-checkpoint-restore-failed"
                        : "special-natural-host-fixture-leaked-after-restore")
                    + $";restoreCall={restored};reportSuccess={report.Success}"
                    + $";baselineSha={state.BaselineFingerprint}"
                    + $";restoredSha={restoredFingerprint}"
                    + $";difference={restoreDifference}"
                    + $";errors={restoreErrors}"
                    + $";leakedFixtures={leakedFixtures}",
                    out failureReason);
        }

        private static bool TryGetPayload(
            ProductionOutputClearanceNaturalExecutionRequest request,
            out IProductionOutputClearanceExecutablePayload payload,
            out IReadOnlyList<ProductionOutputClearanceExecutableInput> inputs,
            out IReadOnlyList<ProductionOutputClearanceExecutableOutput> outputs)
        {
            payload = request?.Descriptor?.Payload;
            inputs = payload switch
            {
                ProductionOutputClearanceCombatCraftExecutablePayload value =>
                    value.Inputs,
                ProductionOutputClearanceApparelExecutablePayload value =>
                    value.Inputs,
                ProductionOutputClearanceCropHarvestExecutablePayload value =>
                    value.Inputs,
                ProductionOutputClearanceCertifiedSeedExecutablePayload value =>
                    value.Inputs,
                _ => null
            };
            outputs = payload switch
            {
                ProductionOutputClearanceCombatCraftExecutablePayload value =>
                    value.Outputs,
                ProductionOutputClearanceApparelExecutablePayload value =>
                    value.Outputs,
                ProductionOutputClearanceCropHarvestExecutablePayload value =>
                    value.Outputs,
                ProductionOutputClearanceCertifiedSeedExecutablePayload value =>
                    value.Outputs,
                _ => null
            };
            return inputs != null && inputs.Count > 0
                && outputs != null && outputs.Count > 0;
        }

        private static string ResolveBranchId(
            IProductionOutputClearanceExecutablePayload payload) => payload switch
        {
            ProductionOutputClearanceCombatCraftExecutablePayload value =>
                value.BranchId,
            ProductionOutputClearanceApparelExecutablePayload value =>
                value.BranchId,
            ProductionOutputClearanceCropHarvestExecutablePayload value =>
                value.BranchId,
            ProductionOutputClearanceCertifiedSeedExecutablePayload value =>
                value.BranchId,
            _ => throw new InvalidOperationException(
                "Unsupported special natural payload.")
        };

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

        private static bool Fail(string reason, out string failureReason)
        {
            failureReason = reason;
            return false;
        }

        /// <summary>
        /// The natural portfolio spans authored progression tiers. Only the
        /// unlock gate is bypassed; the production validator still owns every
        /// footprint, occupancy and access decision.
        /// </summary>
        private sealed class SpecialNaturalFixturePlacementDebugRules :
            IDungeonDebugRuleQuery
        {
            internal static readonly SpecialNaturalFixturePlacementDebugRules
                Instance = new();

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

        private sealed class PreparedState
        {
            internal PreparedState(
                ProductionOutputClearanceNaturalExecutionRequest request,
                IProductionOutputClearanceExecutablePayload payload,
                IReadOnlyList<ProductionOutputClearanceExecutableInput> inputs,
                IReadOnlyList<ProductionOutputClearanceExecutableOutput> outputs)
            {
                Request = request ?? throw new ArgumentNullException(nameof(request));
                Payload = payload ?? throw new ArgumentNullException(nameof(payload));
                Inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
                Outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
            }

            internal ProductionOutputClearanceNaturalExecutionRequest Request;
            internal IProductionOutputClearanceExecutablePayload Payload;
            internal IReadOnlyList<ProductionOutputClearanceExecutableInput> Inputs;
            internal IReadOnlyList<ProductionOutputClearanceExecutableOutput> Outputs;
            internal IResourceEconomyContentCatalog Content;
            internal IGameContentCatalog GameContent;
            internal ICharacterAiWorldRegistry World;
            internal IDungeonSaveSectionRegistry SaveRegistry;
            internal IProductionAssemblyBridge ProductionBridge;
            internal IProductionWorkshopRuntime Workshops;
            internal IRoomLayoutCache Rooms;
            internal IWorldItemStackRuntime ItemRuntime;
            internal IItemTransferService Transfers;
            internal IWorldItemHaulPlanningService HaulPlanning;
            internal ProductionDistributionRuntime Distribution;
            internal IFacilityOutputExactRouteOutboxQuery ExactRoutes;
            internal IGridBuildingObjectFactory BuildingFactory;
            internal IFacilityCapabilityQuery FacilityCapabilities;
            internal CropPlotRuntime Crops;
            internal IGrandProjectRuntime GrandProjects;
            internal IGrandProjectBenefitQuery GrandProjectBenefits;
            internal DungeonGrandProjectSaveData OriginalGrandProjectSave;
            internal ProductionOutputFactor OriginalGrandProjectOutputFactor;
            internal bool MaximumOutputBenefitsProvisioned;
            internal IRandomStreamProvider Random;
            internal IRunSeedProvider RunSeed;
            internal IGameClock Clock;
            internal IGameClockDiagnosticsControl ClockDiagnostics;
            internal IGameCalendar Calendar;
            internal IGameSessionStateProvider SessionState;
            internal ProgressionSceneRuntimeReferences Progression;
            internal ICombatEquipmentCatalog CombatCatalog;
            internal ICombatCraftDefinitionCatalog CombatCraftDefinitions;
            internal ICharacterNarrativeCommand Narrative;
            internal ICharacterProficiencyCommand Proficiencies;
            internal ISurvivalEnvironmentQuery SurvivalEnvironment;
            internal ISurvivalFoodDebugCommand SurvivalDebug;
            internal IPowerInfrastructureQuery Power;
            internal IFluidInfrastructureTransaction Fluid;
            internal IFluidWastewaterTransaction Wastewater;
            internal Grid Grid;
            internal CharacterActor PlacementActor;
            internal CharacterActor Worker;
            internal CharacterSO CropActorData;
            internal GameObject CropActorObject;
            internal Facility InputWarehouse;
            internal Facility Warehouse;
            internal Facility Facility;
            internal BuildingSO FacilityAsset;
            internal CropDefinitionSO Crop;
            internal string CropGenomeId = string.Empty;
            internal CropGenomeReachableMaximumWitnessSnapshot
                CropGenomeWitness;
            internal CropGenomeReachableMaximumWitnessCatalog
                CropGenomeWitnesses;
            internal readonly List<Facility> CreatedSupports = new();
            internal ProductionOutputClearanceNaturalPreparedScenario Scenario;
            internal List<DungeonSaveSectionEnvelope> Baseline;
            internal string BaselineFingerprint = string.Empty;
            internal float CheckpointTime;
            internal int CheckpointFrame;
            internal float OriginalTimeScale;
            internal bool HasOriginalWeather;
            internal SurvivalWeatherType OriginalWeather;
            internal string WarehouseDestinationId = string.Empty;
            internal string InputWarehouseDestinationId = string.Empty;
            internal bool InputWarehouseRegistered;
            internal IWarehouseFacility[] SuspendedWarehouses =
                Array.Empty<IWarehouseFacility>();
            internal bool WarehousesSuspended;
            internal int CertifiedSeedOperatingDay;
            internal bool AiQuiesced;
        }
    }
}
#endif
