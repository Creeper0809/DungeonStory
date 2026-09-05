#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

/// <summary>
/// One isolated official-scene fixture prepared for a single frozen request.
/// The host owns scene selection and physical source-stock provisioning; the
/// payload driver still owns every production command and terminal receipt.
/// </summary>
public sealed class ProductionOutputClearanceNaturalPreparedScenario
{
    public ProductionOutputClearanceNaturalPreparedScenario(
        string actionId,
        BuildableObject facility,
        CharacterActor worker,
        int certifiedSeedOperatingDay,
        float relevantCraftSkill,
        int maximumProductionSteps)
    {
        ProductionOutputClearanceProfileObservation.RequireCanonical(
            actionId,
            nameof(actionId));
        ActionId = actionId;
        Facility = facility ?? throw new ArgumentNullException(nameof(facility));
        Worker = worker ?? throw new ArgumentNullException(nameof(worker));
        if (!facility.PersistentInstanceId.IsValid)
            throw new ArgumentException(
                "A prepared natural scenario requires a persistent facility.",
                nameof(facility));
        if (certifiedSeedOperatingDay <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(certifiedSeedOperatingDay));
        if (!float.IsFinite(relevantCraftSkill)
            || relevantCraftSkill < 0f)
            throw new ArgumentOutOfRangeException(nameof(relevantCraftSkill));
        if (maximumProductionSteps <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumProductionSteps));
        CertifiedSeedOperatingDay = certifiedSeedOperatingDay;
        RelevantCraftSkill = relevantCraftSkill;
        MaximumProductionSteps = maximumProductionSteps;
    }

    public string ActionId { get; }
    public BuildableObject Facility { get; }
    public CharacterActor Worker { get; }
    public int CertifiedSeedOperatingDay { get; }
    public float RelevantCraftSkill { get; }
    public int MaximumProductionSteps { get; }
}

public sealed class ProductionOutputClearanceNaturalHostStageResult
{
    public bool IsTerminal { get; private set; }
    public bool Succeeded { get; private set; }
    public string FailureReason { get; private set; } = string.Empty;

    public void Complete()
    {
        RequireMutable();
        IsTerminal = true;
        Succeeded = true;
    }

    public void Fail(string failureReason)
    {
        RequireMutable();
        FailureReason = ProductionOutputClearanceNaturalProductionStageResult
            .Canonical(failureReason, nameof(failureReason));
        IsTerminal = true;
    }

    private void RequireMutable()
    {
        if (IsTerminal)
            throw new InvalidOperationException(
                "Natural scenario host stage is already terminal.");
    }
}

public sealed class ProductionOutputClearanceNaturalSchedulerRunResult
{
    public bool IsTerminal { get; private set; }
    public bool Succeeded { get; private set; }
    public string FailureReason { get; private set; } = string.Empty;
    public string OwnerRosterKey { get; private set; } = string.Empty;
    public long ActionEpochDelta { get; private set; }
    public long ActionStartDelta { get; private set; }
    public long HaulStartDelta { get; private set; }
    public bool SchedulerProvenanceExact { get; private set; }
    public bool DeliveryExact { get; private set; }

    public void Complete(
        string ownerRosterKey,
        long actionEpochDelta,
        long actionStartDelta,
        long haulStartDelta,
        bool schedulerProvenanceExact,
        bool deliveryExact)
    {
        RequireMutable();
        ProductionOutputClearanceProfileObservation.RequireCanonical(
            ownerRosterKey,
            nameof(ownerRosterKey));
        if (actionEpochDelta < 0L
            || actionStartDelta < 0L
            || haulStartDelta < 0L)
            throw new ArgumentOutOfRangeException(nameof(actionEpochDelta));
        OwnerRosterKey = ownerRosterKey;
        ActionEpochDelta = actionEpochDelta;
        ActionStartDelta = actionStartDelta;
        HaulStartDelta = haulStartDelta;
        SchedulerProvenanceExact = schedulerProvenanceExact;
        DeliveryExact = deliveryExact;
        IsTerminal = true;
        Succeeded = true;
    }

    public void Fail(string failureReason)
    {
        RequireMutable();
        FailureReason = ProductionOutputClearanceNaturalProductionStageResult
            .Canonical(failureReason, nameof(failureReason));
        IsTerminal = true;
    }

    private void RequireMutable()
    {
        if (IsTerminal)
            throw new InvalidOperationException(
                "Natural scheduler run is already terminal.");
    }
}

/// <summary>
/// Root-owned official-scene adapter. TryPrepare must provision the exact
/// payload inputs as real physical source stacks in ordinary storage, without
/// executing the payload's production command or publishing its output.
/// DriveUntil must advance the real scheduler/AI-haul/world ticks until the
/// supplied live readiness predicate succeeds. It may not teleport inputs.
/// </summary>
public interface IProductionOutputClearanceSpecialNaturalScenarioHost
{
    bool TryPrepare(
        ProductionOutputClearanceNaturalExecutionRequest request,
        out ProductionOutputClearanceNaturalPreparedScenario scenario,
        out string failureReason);

    IEnumerator DriveUntil(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionOutputClearanceNaturalPreparedScenario scenario,
        Func<bool> liveTerminalPredicate,
        ProductionOutputClearanceNaturalHostStageResult result);

    /// <summary>
    /// Advances the real crop runtime, authored clock/weather and worker state
    /// from a completed sow to the maximum branch's naturally reachable
    /// harvest-ready state. It must not invoke harvest or publish output.
    /// </summary>
    IEnumerator AdvanceCropToMaximumHarvestReady(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionOutputClearanceNaturalPreparedScenario scenario,
        ProductionOutputClearanceCropHarvestExecutablePayload payload,
        ProductionOutputClearanceNaturalHostStageResult result);

    /// <summary>
    /// Runs scheduler-owned AI haul until every exact receipt slice reaches an
    /// admitted warehouse destination. Direct stack movement is forbidden.
    /// </summary>
    IEnumerator DriveSchedulerOwnedOutputClearance(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionOutputClearanceNaturalPreparedScenario scenario,
        ProductionOutputClearanceExecutionReceiptSnapshot receipt,
        ProductionOutputClearanceNaturalSchedulerRunResult result);

    bool TryRelease(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionOutputClearanceNaturalPreparedScenario scenario,
        out string failureReason);
}

public sealed class ProductionOutputClearanceNaturalLiveDriverServices
{
    public ProductionOutputClearanceNaturalLiveDriverServices(
        IProductionAssemblyBridge productionBridge,
        IProductionWorkshopRuntime workshops,
        IRandomStreamDiagnosticsQuery randomDiagnostics,
        IFacilityOutputClearanceTelemetryControl clearanceTelemetry,
        IFacilityBufferPlannedOutputPublicationService publication,
        IProductionOutputCapabilityRegistry outputCapabilities)
    {
        ProductionBridge = productionBridge
            ?? throw new ArgumentNullException(nameof(productionBridge));
        Workshops = workshops ?? throw new ArgumentNullException(nameof(workshops));
        RandomDiagnostics = randomDiagnostics
            ?? throw new ArgumentNullException(nameof(randomDiagnostics));
        ClearanceTelemetry = clearanceTelemetry
            ?? throw new ArgumentNullException(nameof(clearanceTelemetry));
        Publication = publication ?? throw new ArgumentNullException(nameof(publication));
        OutputCapabilities = outputCapabilities
            ?? throw new ArgumentNullException(nameof(outputCapabilities));
    }

    public IProductionAssemblyBridge ProductionBridge { get; }
    public IProductionWorkshopRuntime Workshops { get; }
    public IRandomStreamDiagnosticsQuery RandomDiagnostics { get; }
    public IFacilityOutputClearanceTelemetryControl ClearanceTelemetry { get; }
    public IFacilityBufferPlannedOutputPublicationService Publication { get; }
    public IProductionOutputCapabilityRegistry OutputCapabilities { get; }
}

public abstract class ProductionOutputClearanceSpecialNaturalLiveDriver<TPayload> :
    IProductionOutputClearanceNaturalLiveScenarioDriver
    where TPayload : class, IProductionOutputClearanceExecutablePayload
{
    private sealed class ActiveRun
    {
        public ProductionOutputClearanceNaturalPreparedScenario Scenario;
        public string TopologySourceDigest = string.Empty;
        public string TopologyBeforeDigest = string.Empty;
        public IReadOnlyList<RandomStreamDiagnosticSnapshot> RandomBefore =
            Array.Empty<RandomStreamDiagnosticSnapshot>();
        public bool TelemetryStarted;
    }

    private readonly Dictionary<string, ActiveRun> active =
        new(StringComparer.Ordinal);
    protected readonly IProductionOutputClearanceSpecialNaturalScenarioHost Host;
    protected readonly ProductionOutputClearanceNaturalLiveDriverServices Services;

    protected ProductionOutputClearanceSpecialNaturalLiveDriver(
        string driverId,
        int contractVersion,
        string payloadKind,
        IProductionOutputClearanceSpecialNaturalScenarioHost host,
        ProductionOutputClearanceNaturalLiveDriverServices services)
    {
        DriverId = ProductionOutputClearanceNaturalProductionStageResult
            .Canonical(driverId, nameof(driverId));
        PayloadKind = ProductionOutputClearanceNaturalProductionStageResult
            .Canonical(payloadKind, nameof(payloadKind));
        if (contractVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(contractVersion));
        ContractVersion = contractVersion;
        Host = host ?? throw new ArgumentNullException(nameof(host));
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public string DriverId { get; }
    public int ContractVersion { get; }
    public string PayloadKind { get; }

    public IEnumerator ExecuteProduction(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionOutputClearanceNaturalProductionStageResult result)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (result == null) throw new ArgumentNullException(nameof(result));
        if (request.Descriptor.Payload is not TPayload payload
            || active.ContainsKey(request.ActionId))
        {
            result.Fail("special-natural-production-request-invalid");
            yield break;
        }
        if (!Host.TryPrepare(
                request,
                out ProductionOutputClearanceNaturalPreparedScenario scenario,
                out string prepareFailure)
            || scenario == null)
        {
            result.Fail(CanonicalFailure(
                prepareFailure,
                "special-natural-prepare-failed"));
            yield break;
        }
        if (!string.Equals(scenario.ActionId, request.ActionId,
                StringComparison.Ordinal)
            || !MatchesRequestedFacility(request, scenario.Facility))
        {
            ReleaseOrThrow(request, scenario);
            result.Fail("special-natural-prepared-facility-mismatch");
            yield break;
        }

        ActiveRun run = new()
        {
            Scenario = scenario,
            TopologySourceDigest = ProductionOutputClearanceNaturalDiagnostics
                .CaptureTopologySourceDigest(
                    Services.ProductionBridge,
                    Services.Workshops,
                    scenario.Facility),
            TopologyBeforeDigest = ProductionOutputClearanceNaturalDiagnostics
                .CaptureTopologyDigest(
                    Services.ProductionBridge,
                    Services.Workshops,
                    scenario.Facility),
            RandomBefore = Services.RandomDiagnostics.Capture()
        };
        if (Services.ClearanceTelemetry.IsCaptureActive)
        {
            ReleaseOrThrow(request, scenario);
            result.Fail("special-natural-telemetry-already-active");
            yield break;
        }
        Services.ClearanceTelemetry.BeginCapture(
            "v27.output-clearance.natural:" + PayloadKind + ":"
            + request.ActionId);
        run.TelemetryStarted = true;
        active.Add(request.ActionId, run);

        IEnumerator production = ExecuteProductionCore(
            request,
            payload,
            scenario,
            result);
        if (production == null)
        {
            AbortRun(request, run);
            throw new InvalidOperationException(
                "A special natural production driver returned null.");
        }
        bool productionEnumerationCompleted = false;
        try
        {
            while (production.MoveNext())
                yield return production.Current;
            productionEnumerationCompleted = true;
        }
        finally
        {
            try
            {
                (production as IDisposable)?.Dispose();
            }
            finally
            {
                if (!productionEnumerationCompleted)
                    AbortRun(request, run);
            }
        }
        if (!result.IsTerminal)
            throw new InvalidOperationException(
                "A special natural production driver did not terminate.");
        if (!result.Succeeded)
            AbortRun(request, run);
    }

    public IEnumerator ExecuteClearance(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionOutputClearanceExecutionReceiptSnapshot receipt,
        ProductionOutputClearanceNaturalClearanceStageResult result)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (receipt == null) throw new ArgumentNullException(nameof(receipt));
        if (result == null) throw new ArgumentNullException(nameof(result));
        if (!active.TryGetValue(request.ActionId, out ActiveRun run)
            || !string.Equals(receipt.ActionId, request.ActionId,
                StringComparison.Ordinal)
            || !string.Equals(receipt.RuntimeFacilityId,
                run.Scenario.Facility.PersistentInstanceId.Value,
                StringComparison.Ordinal))
        {
            result.Fail("special-natural-clearance-owner-mismatch");
            yield break;
        }

        ProductionOutputClearanceNaturalSchedulerRunResult scheduler = new();
        IEnumerator execution = Host.DriveSchedulerOwnedOutputClearance(
            request,
            run.Scenario,
            receipt,
            scheduler);
        if (execution == null)
            throw new InvalidOperationException(
                "Natural scheduler clearance host returned null.");
        FacilityOutputClearanceTelemetrySnapshot telemetry = default;
        bool captureEnded = false;
        try
        {
            while (execution.MoveNext())
                yield return execution.Current;
            if (!scheduler.IsTerminal || !scheduler.Succeeded)
            {
                result.Fail(CanonicalFailure(
                    scheduler.FailureReason,
                    scheduler.IsTerminal
                        ? "special-natural-scheduler-failed"
                        : "special-natural-scheduler-not-terminal"));
                yield break;
            }

            string topologyAfter = ProductionOutputClearanceNaturalDiagnostics
                .CaptureTopologyDigest(
                    Services.ProductionBridge,
                    Services.Workshops,
                    run.Scenario.Facility);
            IReadOnlyList<RandomStreamDiagnosticSnapshot> randomAfter =
                Services.RandomDiagnostics.Capture();
            string randomDigest = ProductionOutputClearanceNaturalDiagnostics
                .CaptureRandomStateDigest(randomAfter);
            long randomDrawDelta = ProductionOutputClearanceNaturalDiagnostics
                .CaptureRandomDrawDelta(run.RandomBefore, randomAfter);
            telemetry = Services.ClearanceTelemetry.EndCapture();
            captureEnded = true;
            run.TelemetryStarted = false;
            ProductionOutputClearanceNaturalClearanceWitness witness = new(
                run.TopologySourceDigest,
                run.TopologyBeforeDigest,
                topologyAfter,
                scheduler.OwnerRosterKey,
                scheduler.ActionEpochDelta,
                scheduler.ActionStartDelta,
                scheduler.HaulStartDelta,
                telemetry,
                scheduler.SchedulerProvenanceExact,
                scheduler.DeliveryExact,
                randomDigest,
                randomDrawDelta);
            result.Complete(witness);
        }
        finally
        {
            (execution as IDisposable)?.Dispose();
            if (!captureEnded && run.TelemetryStarted
                && Services.ClearanceTelemetry.IsCaptureActive)
            {
                Services.ClearanceTelemetry.EndCapture();
                run.TelemetryStarted = false;
            }
        }
    }

    public bool TryFinalize(
        ProductionOutputClearanceNaturalExecutionRequest request,
        bool receiptAccepted,
        out string failureReason)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        _ = receiptAccepted;
        failureReason = string.Empty;
        if (!active.TryGetValue(request.ActionId, out ActiveRun run))
            return true;

        if (run.TelemetryStarted && Services.ClearanceTelemetry.IsCaptureActive)
        {
            Services.ClearanceTelemetry.EndCapture();
            run.TelemetryStarted = false;
        }
        if (Host.TryRelease(request, run.Scenario, out failureReason))
        {
            active.Remove(request.ActionId);
            failureReason = string.Empty;
            return true;
        }
        failureReason = CanonicalFailure(
            failureReason,
            "special-natural-finalize-release-failed");
        return false;
    }

    protected abstract IEnumerator ExecuteProductionCore(
        ProductionOutputClearanceNaturalExecutionRequest request,
        TPayload payload,
        ProductionOutputClearanceNaturalPreparedScenario scenario,
        ProductionOutputClearanceNaturalProductionStageResult result);

    protected IEnumerator DriveUntil(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionOutputClearanceNaturalPreparedScenario scenario,
        Func<bool> predicate,
        ProductionOutputClearanceNaturalProductionStageResult result,
        string fallbackFailure)
    {
        ProductionOutputClearanceNaturalHostStageResult hostResult = new();
        IEnumerator execution = Host.DriveUntil(
            request,
            scenario,
            predicate,
            hostResult);
        if (execution == null)
            throw new InvalidOperationException(
                "Natural scenario DriveUntil returned null.");
        try
        {
            while (execution.MoveNext())
                yield return execution.Current;
        }
        finally
        {
            (execution as IDisposable)?.Dispose();
        }
        if (!hostResult.IsTerminal || !hostResult.Succeeded)
            result.Fail(CanonicalFailure(
                hostResult.FailureReason,
                fallbackFailure));
    }

    private void AbortRun(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ActiveRun run)
    {
        if (run.TelemetryStarted && Services.ClearanceTelemetry.IsCaptureActive)
        {
            Services.ClearanceTelemetry.EndCapture();
            run.TelemetryStarted = false;
        }
        active.Remove(request.ActionId);
        ReleaseOrThrow(request, run.Scenario);
    }

    private void ReleaseOrThrow(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionOutputClearanceNaturalPreparedScenario scenario)
    {
        if (!Host.TryRelease(request, scenario, out string failureReason))
            throw new InvalidOperationException(
                "Natural scenario release failed: " + failureReason);
    }

    private static bool MatchesRequestedFacility(
        ProductionOutputClearanceNaturalExecutionRequest request,
        BuildableObject facility) => facility?.BuildingData != null
        && string.Equals(
            ProductionFacilityDefinitionIdentity.Resolve(
                facility.BuildingData),
            request.Descriptor.Plan.DefinitionId,
            StringComparison.Ordinal)
        && string.Equals(
            facility.GetProductionWorkstationTag(),
            request.Descriptor.Plan.WorkstationTag,
            StringComparison.Ordinal);

    protected static string CanonicalFailure(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Any(char.IsWhiteSpace))
            return fallback;
        return value;
    }
}

public sealed class ProductionOutputClearanceCombatCraftNaturalLiveDriver :
    ProductionOutputClearanceSpecialNaturalLiveDriver<
        ProductionOutputClearanceCombatCraftExecutablePayload>
{
    public const string Id = "natural-live-driver:combat-craft";
    public const int Version = 1;
    private readonly ICombatEquipmentRuntime equipment;
    private readonly ProductionOutputClearanceNaturalCompletionCorrelationAuthority
        completions;

    public ProductionOutputClearanceCombatCraftNaturalLiveDriver(
        IProductionOutputClearanceSpecialNaturalScenarioHost host,
        ProductionOutputClearanceNaturalLiveDriverServices services,
        ICombatEquipmentRuntime equipment,
        ProductionOutputClearanceNaturalCompletionCorrelationAuthority completions)
        : base(Id, Version, "combat-craft", host, services)
    {
        this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        this.completions = completions
            ?? throw new ArgumentNullException(nameof(completions));
    }

    protected override IEnumerator ExecuteProductionCore(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionOutputClearanceCombatCraftExecutablePayload payload,
        ProductionOutputClearanceNaturalPreparedScenario scenario,
        ProductionOutputClearanceNaturalProductionStageResult result)
    {
        HashSet<string> before = equipment.CraftQueue
            .Where(value => value != null)
            .Select(value => value.orderId)
            .ToHashSet(StringComparer.Ordinal);
        if (!equipment.TryQueueCraft(
                payload.CraftDefinitionId,
                payload.SelectedMaterialId,
                scenario.Facility,
                out string queueFailure))
        {
            result.Fail(CanonicalFailure(
                queueFailure,
                "combat-natural-queue-failed"));
            yield break;
        }
        CombatEquipmentCraftOrderSaveData[] created = equipment.CraftQueue
            .Where(value => value != null
                && !before.Contains(value.orderId)
                && string.Equals(value.definitionId,
                    payload.CraftDefinitionId, StringComparison.Ordinal)
                && string.Equals(value.materialId,
                    payload.SelectedMaterialId, StringComparison.Ordinal)
                && string.Equals(value.facilityPersistentId,
                    scenario.Facility.PersistentInstanceId.Value,
                    StringComparison.Ordinal))
            .ToArray();
        if (created.Length != 1)
        {
            result.Fail("combat-natural-created-order-ambiguous");
            yield break;
        }
        CombatEquipmentCraftOrderSaveData order = created[0];
        string[] craftableDefinitionIds = { payload.CraftDefinitionId };
        IEnumerator delivery = DriveUntil(
            request,
            scenario,
            () => equipment.TryGetNextCraftMaterialContext(
                    craftableDefinitionIds,
                    scenario.Worker,
                    out string readyDefinitionId,
                    out string readyMaterialId,
                    out _)
                && string.Equals(
                    readyDefinitionId,
                    payload.CraftDefinitionId,
                    StringComparison.Ordinal)
                && string.Equals(
                    readyMaterialId,
                    payload.SelectedMaterialId,
                    StringComparison.Ordinal),
            result,
            "combat-natural-input-delivery-failed");
        while (delivery.MoveNext())
            yield return delivery.Current;
        if (result.IsTerminal)
            yield break;

        int steps = 0;
        while (equipment.CraftQueue.Contains(order)
            && steps++ < scenario.MaximumProductionSteps)
        {
            int completed = equipment.ApplyCraftWork(
                craftableDefinitionIds,
                Math.Max(0.1f, order.RemainingWork),
                scenario.Worker,
                scenario.RelevantCraftSkill,
                out string completedDefinition,
                out string completedMaterial,
                out CombatEquipmentQuality _,
                out MythicProvenanceSaveData _);
            if (completed > 0
                && (!string.Equals(completedDefinition,
                        payload.CraftDefinitionId, StringComparison.Ordinal)
                    || !string.Equals(completedMaterial,
                        payload.SelectedMaterialId, StringComparison.Ordinal)))
            {
                result.Fail("combat-natural-completion-identity-mismatch");
                yield break;
            }
            if (equipment.CraftQueue.Contains(order))
                yield return null;
        }
        string publishFailure = string.Empty;
        if (equipment.CraftQueue.Contains(order)
            || !completions.TryPublishCombatCraft(
                request.ActionId,
                order,
                Services.Publication,
                out publishFailure))
        {
            result.Fail(CanonicalFailure(
                publishFailure,
                "combat-natural-terminal-publish-failed"));
            yield break;
        }
        result.Complete();
    }
}

public sealed class ProductionOutputClearanceApparelNaturalLiveDriver :
    ProductionOutputClearanceSpecialNaturalLiveDriver<
        ProductionOutputClearanceApparelExecutablePayload>
{
    public const string Id = "natural-live-driver:apparel";
    public const int Version = 1;
    private readonly IApparelWorkOrderCommand commands;
    private readonly IApparelWorkOrderQuery orders;
    private readonly ProductionOutputClearanceNaturalCompletionCorrelationAuthority
        completions;

    public ProductionOutputClearanceApparelNaturalLiveDriver(
        IProductionOutputClearanceSpecialNaturalScenarioHost host,
        ProductionOutputClearanceNaturalLiveDriverServices services,
        IApparelWorkOrderCommand commands,
        IApparelWorkOrderQuery orders,
        ProductionOutputClearanceNaturalCompletionCorrelationAuthority completions)
        : base(Id, Version, "apparel", host, services)
    {
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.orders = orders ?? throw new ArgumentNullException(nameof(orders));
        this.completions = completions
            ?? throw new ArgumentNullException(nameof(completions));
    }

    protected override IEnumerator ExecuteProductionCore(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionOutputClearanceApparelExecutablePayload payload,
        ProductionOutputClearanceNaturalPreparedScenario scenario,
        ProductionOutputClearanceNaturalProductionStageResult result)
    {
        ApparelCraftOrderRequest orderRequest = new(
            payload.ApparelId,
            payload.SelectedSize,
            payload.SelectedModifications,
            ApparelMaterialSelectionPolicy.ExactMaterial,
            payload.SelectedMaterialId,
            CraftsmanshipQualityTier.Awful,
            WorkerSelectionPolicySaveData.Anyone(),
            RejectedOutputDisposition.MarkForSale,
            QualityRepeatLimitMode.SafeLimits,
            maximumAttempts: 1,
            requiredAcceptedCount: 1);
        if (!commands.CreateCraft(
                orderRequest,
                out string orderId,
                out DomainFailure createFailure))
        {
            result.Fail("apparel-natural-create-failed");
            yield break;
        }
        ApparelWorkOrderSaveData order = orders.Orders.SingleOrDefault(value =>
            value != null
            && string.Equals(value.orderId, orderId, StringComparison.Ordinal));
        if (order == null
            || !string.Equals(order.facilityInstanceId,
                scenario.Facility.PersistentInstanceId.Value,
                StringComparison.Ordinal))
        {
            commands.Cancel(orderId, out _);
            result.Fail("apparel-natural-created-order-owner-mismatch");
            yield break;
        }

        IEnumerator delivery = DriveUntil(
            request,
            scenario,
            () => order.state is ApparelWorkOrderState.Ready
                or ApparelWorkOrderState.InProgress
                or ApparelWorkOrderState.WaitingForOutputSpace
                or ApparelWorkOrderState.Completed,
            result,
            "apparel-natural-input-delivery-failed");
        while (delivery.MoveNext())
            yield return delivery.Current;
        if (result.IsTerminal)
            yield break;

        int steps = 0;
        DomainFailure firstWorkFailure = DomainFailure.None;
        ApparelWorkOrderState firstWorkFailureState = order.state;
        DomainFailure lastWorkFailure = DomainFailure.None;
        while (order.state != ApparelWorkOrderState.Completed
            && steps++ < scenario.MaximumProductionSteps)
        {
            float remaining = Math.Max(
                0.1f,
                order.requiredWork - order.completedWork);
            bool workApplied = commands.ApplyWork(
                orderId,
                scenario.Worker,
                remaining,
                out lastWorkFailure);
            if (!workApplied && !firstWorkFailure.IsFailure)
            {
                firstWorkFailure = lastWorkFailure;
                firstWorkFailureState = order.state;
            }
            if (!workApplied
                && order.state is not (
                    ApparelWorkOrderState.WaitingForMaterials
                    or ApparelWorkOrderState.WaitingForOutputSpace
                    or ApparelWorkOrderState.WaitingForDispositionFinalization))
            {
                result.Fail(
                    "apparel-natural-work-failed:state="
                    + order.state
                    + ";failure="
                    + FormatFailure(lastWorkFailure));
                yield break;
            }
            if (order.state != ApparelWorkOrderState.Completed)
                yield return null;
        }
        if (order.state != ApparelWorkOrderState.Completed)
        {
            result.Fail(
                "apparel-natural-work-timeout:state="
                + order.state
                + ";steps="
                + steps
                + ";completed-work="
                + order.completedWork.ToString("R", CultureInfo.InvariantCulture)
                + ";required-work="
                + order.requiredWork.ToString("R", CultureInfo.InvariantCulture)
                + ";first-failure-state="
                + firstWorkFailureState
                + ";first-failure="
                + FormatFailure(firstWorkFailure)
                + ";failure="
                + FormatFailure(lastWorkFailure));
            yield break;
        }
        string publishFailure = string.Empty;
        if (order.state != ApparelWorkOrderState.Completed
            || !completions.TryPublishApparel(
                request.ActionId,
                order,
                Services.Publication,
                out publishFailure))
        {
            result.Fail(CanonicalFailure(
                publishFailure,
                "apparel-natural-terminal-publish-failed"));
            yield break;
        }
        result.Complete();
    }

    private static string FormatFailure(DomainFailure failure) =>
        failure.Code + ":" + string.Join(",", failure.Parameters.ToArray());
}

public sealed class ProductionOutputClearanceCropHarvestNaturalLiveDriver :
    ProductionOutputClearanceSpecialNaturalLiveDriver<
        ProductionOutputClearanceCropHarvestExecutablePayload>
{
    public const string Id = "natural-live-driver:crop-harvest";
    public const int Version = 1;
    private readonly CropPlotRuntime crops;
    private readonly ICropCycleExecutionCorrelationCommand correlations;
    private readonly IWorkExecutionHandlerRegistry workHandlers;

    public ProductionOutputClearanceCropHarvestNaturalLiveDriver(
        IProductionOutputClearanceSpecialNaturalScenarioHost host,
        ProductionOutputClearanceNaturalLiveDriverServices services,
        CropPlotRuntime crops,
        ICropCycleExecutionCorrelationCommand correlations,
        IWorkExecutionHandlerRegistry workHandlers)
        : base(Id, Version, "crop-harvest", host, services)
    {
        this.crops = crops ?? throw new ArgumentNullException(nameof(crops));
        this.correlations = correlations
            ?? throw new ArgumentNullException(nameof(correlations));
        this.workHandlers = workHandlers
            ?? throw new ArgumentNullException(nameof(workHandlers));
    }

    protected override IEnumerator ExecuteProductionCore(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionOutputClearanceCropHarvestExecutablePayload payload,
        ProductionOutputClearanceNaturalPreparedScenario scenario,
        ProductionOutputClearanceNaturalProductionStageResult result)
    {
        string plotId = scenario.Facility.PersistentInstanceId.Value;
        string setFailure = string.Empty;
        string bindFailure = string.Empty;
        if (!crops.TrySetCrop(
                scenario.Facility,
                payload.CropId,
                out setFailure)
            || !correlations.TryBindNextCycle(
                request.ActionId,
                plotId,
                payload.CropId,
                out bindFailure))
        {
            result.Fail(CanonicalFailure(
                setFailure.Length == 0 ? bindFailure : setFailure,
                "crop-natural-cycle-bind-failed"));
            yield break;
        }
        crops.Tick();
        IEnumerator delivery = DriveUntil(
            request,
            scenario,
            () => crops.TryGetWork(
                scenario.Facility,
                BuiltInWorkTypeIds.Sow,
                out CropPlotWorkSnapshot sow) && sow.Available,
            result,
            "crop-natural-input-delivery-failed");
        while (delivery.MoveNext())
            yield return delivery.Current;
        if (result.IsTerminal)
            yield break;
        IEnumerator sowExecution = ExecuteCropWork(
            scenario,
            BuiltInWorkTypeIds.Sow,
            result,
            "crop-natural-sow-failed");
        while (sowExecution.MoveNext())
            yield return sowExecution.Current;
        if (result.IsTerminal)
            yield break;

        ProductionOutputClearanceNaturalHostStageResult growth = new();
        IEnumerator advance = Host.AdvanceCropToMaximumHarvestReady(
            request,
            scenario,
            payload,
            growth);
        if (advance == null)
            throw new InvalidOperationException(
                "Natural crop growth host returned null.");
        try
        {
            while (advance.MoveNext())
                yield return advance.Current;
        }
        finally
        {
            (advance as IDisposable)?.Dispose();
        }
        if (!growth.IsTerminal || !growth.Succeeded)
        {
            result.Fail(CanonicalFailure(
                growth.FailureReason,
                "crop-natural-growth-failed"));
            yield break;
        }
        IEnumerator harvestExecution = ExecuteCropWork(
            scenario,
            BuiltInWorkTypeIds.Harvest,
            result,
            "crop-natural-harvest-failed");
        while (harvestExecution.MoveNext())
            yield return harvestExecution.Current;
        if (!result.IsTerminal)
            result.Complete();
    }

    private IEnumerator ExecuteCropWork(
        ProductionOutputClearanceNaturalPreparedScenario scenario,
        WorkTypeId workTypeId,
        ProductionOutputClearanceNaturalProductionStageResult stage,
        string failureReason)
    {
        if (!workHandlers.TryGet(workTypeId, out IWorkExecutionHandler handler)
            || handler == null)
        {
            stage.Fail(failureReason);
            yield break;
        }
        AbilityWork work = scenario.Worker.GetComponent<AbilityWork>();
        if (work == null)
        {
            stage.Fail(failureReason);
            yield break;
        }
        WorkExecutionResult workResult = new();
        WorkExecutionContext context = new(
            runId: 1,
            work,
            scenario.Worker,
            scenario.Facility,
            workTypeId,
            ExecuteImmediateWorkAmount,
            canContinue: () => true,
            executePersistentWorkAmount:
                ExecuteImmediatePersistentWorkAmount);
        IEnumerator execution = handler.Execute(context, workResult);
        if (execution == null)
            throw new InvalidOperationException(
                "Crop production handler returned null.");
        try
        {
            while (execution.MoveNext())
                yield return execution.Current;
        }
        finally
        {
            (execution as IDisposable)?.Dispose();
        }
        if (!workResult.CompletedSuccessfully)
            stage.Fail(failureReason);
    }

    private static IEnumerator ExecuteImmediateWorkAmount(
        float requiredWork,
        string label,
        float extraMultiplier)
    {
        if (!float.IsFinite(requiredWork)
            || requiredWork <= 0f
            || !float.IsFinite(extraMultiplier)
            || extraMultiplier <= 0f)
            throw new InvalidOperationException(
                "Immediate natural crop work authority is invalid.");
        yield break;
    }

    private static IEnumerator ExecuteImmediatePersistentWorkAmount(
        float requiredWork,
        float completedWork,
        string label,
        float extraMultiplier,
        Func<float, bool> applyDelta)
    {
        float delta = requiredWork - completedWork;
        if (!float.IsFinite(requiredWork)
            || !float.IsFinite(completedWork)
            || !float.IsFinite(extraMultiplier)
            || extraMultiplier <= 0f
            || delta <= 0f
            || applyDelta == null
            || !applyDelta(delta))
            throw new InvalidOperationException(
                "Immediate natural crop persistent work was rejected.");
        yield break;
    }
}

public sealed class ProductionOutputClearanceCertifiedSeedNaturalLiveDriver :
    ProductionOutputClearanceSpecialNaturalLiveDriver<
        ProductionOutputClearanceCertifiedSeedExecutablePayload>
{
    public const string Id = "natural-live-driver:certified-seed";
    public const int Version = 1;
    private readonly ICertifiedSeedCommand commands;
    private readonly ICertifiedSeedExecutionReceiptQuery receipts;
    private readonly ProductionOutputClearanceNaturalCompletionCorrelationAuthority
        completions;

    public ProductionOutputClearanceCertifiedSeedNaturalLiveDriver(
        IProductionOutputClearanceSpecialNaturalScenarioHost host,
        ProductionOutputClearanceNaturalLiveDriverServices services,
        ICertifiedSeedCommand commands,
        ICertifiedSeedExecutionReceiptQuery receipts,
        ProductionOutputClearanceNaturalCompletionCorrelationAuthority completions)
        : base(Id, Version, "certified-seed", host, services)
    {
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
        this.completions = completions
            ?? throw new ArgumentNullException(nameof(completions));
    }

    protected override IEnumerator ExecuteProductionCore(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionOutputClearanceCertifiedSeedExecutablePayload payload,
        ProductionOutputClearanceNaturalPreparedScenario scenario,
        ProductionOutputClearanceNaturalProductionStageResult result)
    {
        DomainFailure planFailure = DomainFailure.None;
        bool planned = false;
        int planAttempts = 0;
        while (planAttempts < scenario.MaximumProductionSteps)
        {
            planAttempts++;
            if (commands.TryPlan(
                    request.ActionId,
                    payload.CropId,
                    scenario.Facility.PersistentInstanceId.Value,
                    out planFailure))
            {
                planned = true;
                break;
            }
            if (!IsDeliveryReachabilityDeferred(planFailure))
                break;
            yield return null;
        }
        if (!planned)
        {
            result.Fail(
                "certified-seed-natural-plan-failed:attempts="
                + planAttempts.ToString(CultureInfo.InvariantCulture)
                + ";failure="
                + FormatFailure(planFailure));
            yield break;
        }
        if (!receipts.TryCapturePlanReceipt(
                request.ActionId,
                out CertifiedSeedPlanExecutionReceipt planReceipt)
            || planReceipt == null)
        {
            result.Fail("certified-seed-natural-plan-receipt-missing");
            yield break;
        }
        if (!string.Equals(
                planReceipt.CropId,
                payload.CropId,
                StringComparison.Ordinal))
        {
            result.Fail(
                "certified-seed-natural-plan-crop-mismatch:expected="
                + payload.CropId
                + ";actual="
                + planReceipt.CropId);
            yield break;
        }
        bool completed = false;
        IEnumerator delivery = DriveUntil(
            request,
            scenario,
            () =>
            {
                if (!receipts.IsPlanReadyForCompletion(request.ActionId))
                    return false;
                completed = commands.CompleteDeliveredPlans(
                    scenario.CertifiedSeedOperatingDay) > 0;
                return completed;
            },
            result,
            "certified-seed-natural-input-delivery-failed");
        while (delivery.MoveNext())
            yield return delivery.Current;
        if (result.IsTerminal)
            yield break;
        string publishFailure = string.Empty;
        if (!completed
            || !completions.TryPublishCertifiedSeed(
                planReceipt,
                Services.Publication,
                Services.OutputCapabilities,
                out publishFailure))
        {
            result.Fail(CanonicalFailure(
                publishFailure,
                "certified-seed-natural-terminal-publish-failed"));
            yield break;
        }
        result.Complete();
    }

    private static bool IsDeliveryReachabilityDeferred(DomainFailure failure)
    {
        if (failure.Code != FailureCode.ItemTransferStackUnavailable)
            return false;
        foreach (string parameter in failure.Parameters)
        {
            if (string.Equals(
                    parameter,
                    "delivery-reachability-deferred",
                    StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static string FormatFailure(DomainFailure failure) =>
        failure.Code + ":" + string.Join(",", failure.Parameters.ToArray());
}

public static class ProductionOutputClearanceSpecialNaturalLiveDriverFactory
{
    public static IReadOnlyList<
        IProductionOutputClearanceNaturalMeasurementExecutor> CreateExecutors(
        IProductionOutputClearanceSpecialNaturalScenarioHost host,
        ProductionOutputClearanceNaturalLiveDriverServices services,
        ICombatEquipmentRuntime combat,
        IApparelWorkOrderCommand apparelCommands,
        IApparelWorkOrderQuery apparelOrders,
        CropPlotRuntime crops,
        ICropCycleExecutionCorrelationCommand cropCorrelations,
        IWorkExecutionHandlerRegistry workHandlers,
        ICertifiedSeedCommand certifiedSeeds,
        ICertifiedSeedExecutionReceiptQuery certifiedReceipts,
        ProductionOutputClearanceNaturalCompletionCorrelationAuthority
            completions,
        ProductionOutputClearanceNaturalMeasurementHandlerRegistry handlers)
    {
        if (handlers == null) throw new ArgumentNullException(nameof(handlers));
        return Array.AsReadOnly(new
            IProductionOutputClearanceNaturalMeasurementExecutor[]
        {
            new ProductionOutputClearanceCombatCraftNaturalExecutor(
                new ProductionOutputClearanceCombatCraftNaturalLiveDriver(
                    host, services, combat, completions),
                handlers),
            new ProductionOutputClearanceApparelNaturalExecutor(
                new ProductionOutputClearanceApparelNaturalLiveDriver(
                    host,
                    services,
                    apparelCommands,
                    apparelOrders,
                    completions),
                handlers),
            new ProductionOutputClearanceCropHarvestNaturalExecutor(
                new ProductionOutputClearanceCropHarvestNaturalLiveDriver(
                    host,
                    services,
                    crops,
                    cropCorrelations,
                    workHandlers),
                handlers),
            new ProductionOutputClearanceCertifiedSeedNaturalExecutor(
                new ProductionOutputClearanceCertifiedSeedNaturalLiveDriver(
                    host,
                    services,
                    certifiedSeeds,
                    certifiedReceipts,
                    completions),
                handlers)
        });
    }
}
#endif
