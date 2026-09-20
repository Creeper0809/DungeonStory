using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Environment;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public static class EnvironmentalFireWaterDestinationIdentity
{
    public const string Prefix = "environmental-fire-water:";
    public const string OwnerDomain = "environmental-fire";

    public static string Create(string fireId)
    {
        string canonical = fireId?.Trim() ?? string.Empty;
        if (canonical.Length == 0)
            throw new ArgumentException("A fire ID is required.", nameof(fireId));
        return Prefix + canonical;
    }

    public static bool IsExactEmergencySupportClaim(
        FacilityBufferDestinationClaim claim)
    {
        if (claim == null
            || claim.AnchorKind
                != FacilityBufferDestinationAnchorKind.LiveBuilding
            || string.IsNullOrWhiteSpace(claim.OwnerFacilityId)
            || string.IsNullOrWhiteSpace(claim.OwnerOperationId)
            || !string.Equals(
                claim.OwnerDomain,
                OwnerDomain,
                StringComparison.Ordinal))
        {
            return false;
        }

        return string.Equals(
            claim.DestinationId,
            Prefix + claim.OwnerOperationId,
            StringComparison.Ordinal);
    }
}

public readonly struct EnvironmentalFireSuppressionWorkSnapshot
{
    public EnvironmentalFireSuppressionWorkSnapshot(
        bool available,
        EnvironmentalFireSnapshot fire,
        EnvironmentalFireSuppressionMode mode,
        float requiredWork,
        int requiredWater,
        string displayName,
        string unavailableReason)
    {
        Available = available;
        Fire = fire;
        Mode = mode;
        RequiredWork = requiredWork;
        RequiredWater = requiredWater;
        DisplayName = displayName?.Trim() ?? string.Empty;
        UnavailableReason = unavailableReason?.Trim() ?? string.Empty;
    }

    public bool Available { get; }
    public EnvironmentalFireSnapshot Fire { get; }
    public EnvironmentalFireSuppressionMode Mode { get; }
    public float RequiredWork { get; }
    public int RequiredWater { get; }
    public string DisplayName { get; }
    public string UnavailableReason { get; }
}

public interface IEnvironmentalFireSuppressionWorkRuntime
{
    // True means that the facility has an active fire. Availability and its
    // diagnostic remain in the snapshot so another ThreatMitigation producer
    // cannot run through an unsafe burning target.
    bool TryGetWork(
        BuildableObject target,
        CharacterActor actor,
        out EnvironmentalFireSuppressionWorkSnapshot work);

    bool TryBegin(
        BuildableObject target,
        CharacterActor actor,
        EnvironmentalFireSuppressionWorkSnapshot expectedWork,
        out EnvironmentalFireSuppressionAttempt attempt,
        out string failureReason);

    bool ApplyResponderExposure(
        EnvironmentalFireSuppressionAttempt attempt,
        CharacterActor actor,
        float approvedWork);

    EnvironmentalFireSuppressionResult Complete(
        EnvironmentalFireSuppressionAttempt attempt);
}

public sealed class EnvironmentalFireSuppressionAttempt : IDisposable
{
    private readonly IEnvironmentalFireCommand fire;
    private readonly IItemQuantityReservationService reservations;
    private bool terminal;
    private bool disposed;

    internal EnvironmentalFireSuppressionAttempt(
        EnvironmentalFireSuppressionCommand command,
        float exposureIntensity,
        IEnvironmentalFireCommand fire,
        IItemQuantityReservationService reservations)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        ExposureIntensity = exposureIntensity;
        this.fire = fire ?? throw new ArgumentNullException(nameof(fire));
        this.reservations = reservations
            ?? throw new ArgumentNullException(nameof(reservations));
    }

    public EnvironmentalFireSuppressionCommand Command { get; }
    public float ExposureIntensity { get; }

    internal void MarkTerminal() => terminal = true;

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;

        if (!terminal)
        {
            // TryApplySuppression is synchronous today, but this preserves the
            // domain cancellation boundary if it leaves a prepared operation
            // before a future/retried completion. OperationMissing is harmless
            // when cancellation happened before the commit call.
            fire.TryCancelSuppression(Command.OperationId);
        }

        if (Command.WaterLeaseId.Length > 0)
        {
            reservations.Release(
                Command.WaterLeaseId,
                terminal
                    ? ItemReservationReleaseReason.Completed
                    : ItemReservationReleaseReason.Cancelled);
        }
    }
}

/// <summary>
/// Bridges active fires into the existing Red-alert, ThreatMitigation, haul,
/// exact reservation, and work-exposure authorities. It deliberately owns no
/// parallel character scheduler or inventory.
/// </summary>
public sealed class EnvironmentalFireResponseRuntime :
    IEnvironmentalFireSuppressionWorkRuntime,
    ITickable
{
    private sealed class ResponseState
    {
        public string FireId;
        public BuildableObject Building;
        public Vector2Int Position;
        public int SeverityBand;
        public bool IsolationBlocked;
        public bool WaterReady;
        public FacilityBufferDestinationClaim WaterClaim;
    }

    private const float Epsilon = 0.0001f;
    private const float WaterApplicationWork = 2.5f;
    private const float HeatExposurePerWorkAtFullIntensity = 4f;
    private const float MaximumCleanWaterContamination = 0.01f;
    private const string CleanWaterItemId = "resource:clean-water";
    private const string WorkSourcePrefix = "environmental-fire-work:";
    private const string IncidentPrefix = "environmental-fire-incident:";
    private const string ClaimOwnerDomain =
        EnvironmentalFireWaterDestinationIdentity.OwnerDomain;
    private const string ClaimReleaseReason = "environmental-fire-water-retired";

    private readonly IEnvironmentalFireQuery fires;
    private readonly IEnvironmentalFireCommand fireCommands;
    private readonly IEnvironmentalFireTargetQuery targets;
    private readonly IEnvironmentalFireSuppressionAccessQuery suppressionAccess;
    private readonly IEnvironmentalFireElectricalSafetyQuery electricalSafety;
    private readonly EnvironmentalFireRuntimeSettings settings;
    private readonly IBuildingWorldQuery buildings;
    private readonly IWorldItemStackRuntime items;
    private readonly IItemTransferService transfers;
    private readonly IItemQuantityReservationService reservations;
    private readonly IFacilityBufferDestinationClaimQuery destinationClaimQuery;
    private readonly IFacilityBufferDestinationClaimCommand destinationClaims;
    private readonly IFacilityBufferDestinationReleaseService destinationRelease;
    private readonly IFacilityCandidateCache facilityCandidates;
    private readonly IWorkforceReplanService workforce;
    private readonly ISettlementAlertService alerts;
    private readonly IGameEventBus events;
    private readonly ICharacterEnvironmentExposureCommand exposure;
    private readonly Dictionary<string, ResponseState> stateByFireId =
        new(StringComparer.Ordinal);

    public EnvironmentalFireResponseRuntime(
        IEnvironmentalFireQuery fires,
        IEnvironmentalFireCommand fireCommands,
        IEnvironmentalFireTargetQuery targets,
        IEnvironmentalFireSuppressionAccessQuery suppressionAccess,
        IEnvironmentalFireElectricalSafetyQuery electricalSafety,
        EnvironmentalFireRuntimeSettings settings,
        IBuildingWorldQuery buildings,
        IWorldItemStackRuntime items,
        IItemTransferService transfers,
        IItemQuantityReservationService reservations,
        IFacilityBufferDestinationClaimQuery destinationClaimQuery,
        IFacilityBufferDestinationClaimCommand destinationClaims,
        IFacilityBufferDestinationReleaseService destinationRelease,
        IFacilityCandidateCache facilityCandidates,
        IWorkforceReplanService workforce,
        ISettlementAlertService alerts,
        IGameEventBus events,
        ICharacterEnvironmentExposureCommand exposure)
    {
        this.fires = fires ?? throw new ArgumentNullException(nameof(fires));
        this.fireCommands = fireCommands
            ?? throw new ArgumentNullException(nameof(fireCommands));
        this.targets = targets ?? throw new ArgumentNullException(nameof(targets));
        this.suppressionAccess = suppressionAccess
            ?? throw new ArgumentNullException(nameof(suppressionAccess));
        this.electricalSafety = electricalSafety
            ?? throw new ArgumentNullException(nameof(electricalSafety));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.transfers = transfers ?? throw new ArgumentNullException(nameof(transfers));
        this.reservations = reservations
            ?? throw new ArgumentNullException(nameof(reservations));
        this.destinationClaimQuery = destinationClaimQuery
            ?? throw new ArgumentNullException(nameof(destinationClaimQuery));
        this.destinationClaims = destinationClaims
            ?? throw new ArgumentNullException(nameof(destinationClaims));
        this.destinationRelease = destinationRelease
            ?? throw new ArgumentNullException(nameof(destinationRelease));
        this.facilityCandidates = facilityCandidates
            ?? throw new ArgumentNullException(nameof(facilityCandidates));
        this.workforce = workforce ?? throw new ArgumentNullException(nameof(workforce));
        this.alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.exposure = exposure ?? throw new ArgumentNullException(nameof(exposure));
    }

    public void Tick()
    {
        EnvironmentalFireSnapshot[] active = fires.ActiveFires
            .Where(fire => fire != null)
            .OrderBy(fire => fire.FireId, StringComparer.Ordinal)
            .ToArray();
        var activeIds = new HashSet<string>(
            active.Select(fire => fire.FireId),
            StringComparer.Ordinal);
        SettlementAlertSnapshot alertSnapshot = alerts.Capture();
        var activeIncidents = new HashSet<string>(
            alertSnapshot.ActiveIncidentIds ?? Array.Empty<string>(),
            StringComparer.Ordinal);

        foreach (EnvironmentalFireSnapshot fire in active)
            ReconcileActiveFire(fire, activeIncidents);

        foreach (ResponseState ended in stateByFireId.Values
                     .Where(state => !activeIds.Contains(state.FireId))
                     .OrderBy(state => state.FireId, StringComparer.Ordinal)
                     .ToArray())
        {
            ReconcileEndedFire(ended, activeIncidents);
        }

        // A save can occur after fire suppression but before this projection's
        // next frame. Resolve that restored orphan incident from fire history.
        foreach (string incidentId in activeIncidents
                     .Where(id => id.StartsWith(IncidentPrefix, StringComparison.Ordinal))
                     .OrderBy(id => id, StringComparer.Ordinal))
        {
            string fireId = incidentId.Substring(IncidentPrefix.Length);
            if (activeIds.Contains(fireId) || stateByFireId.ContainsKey(fireId))
                continue;
            ResolveIncident(incidentId);
            EnvironmentalFireHistorySnapshot history = FindHistory(fireId);
            if (history?.EndReason == EnvironmentalFireEndReason.Suppressed)
                RaiseExtinguished(history.Fire);
        }
    }

    public bool TryGetWork(
        BuildableObject target,
        CharacterActor actor,
        out EnvironmentalFireSuppressionWorkSnapshot work)
    {
        work = default;
        string targetId = target != null && target.PersistentInstanceId.IsValid
            ? target.PersistentInstanceId.Value
            : string.Empty;
        EnvironmentalFireSnapshot fire = fires.ActiveFires
            .Where(value => value != null
                && value.Target.Kind == EnvironmentalFireTargetKind.Building
                && string.Equals(
                    value.Target.TargetId,
                    targetId,
                    StringComparison.Ordinal))
            .OrderBy(value => value.FireId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (fire == null)
            return false;

        if (!targets.TryGetTarget(
                fire.Target,
                out EnvironmentalFireTargetSnapshot targetSnapshot)
            || !targetSnapshot.CanBurn)
        {
            work = Unavailable(fire, "environmental-fire-target-unavailable");
            return true;
        }

        if (fire.RequiresElectricalIsolation
            && (!electricalSafety.TryGetSafety(fire.Target, out var safety)
                || !safety.Target.Equals(fire.Target)
                || !safety.IsolationConfirmed))
        {
            work = Unavailable(
                fire,
                "environmental-fire-electrical-isolation-required");
            return true;
        }

        if (fire.Intensity <= settings.MaximumInitialAttackIntensity + Epsilon)
        {
            float required = Mathf.Ceil(
                fire.Intensity / settings.InitialAttackSuppressionPerWork);
            work = new EnvironmentalFireSuppressionWorkSnapshot(
                true,
                fire,
                EnvironmentalFireSuppressionMode.InitialAttack,
                Mathf.Max(1f, required),
                0,
                "화재 초기 진화",
                string.Empty);
            return true;
        }

        int water = RequiredWater(fire, targetSnapshot.Profile);
        string destinationId = EnvironmentalFireWaterDestinationIdentity.Create(
            fire.FireId);
        bool ready = FindDeliveredWater(
            destinationId,
            targetSnapshot.Position,
            water) != null;
        work = new EnvironmentalFireSuppressionWorkSnapshot(
            ready,
            fire,
            EnvironmentalFireSuppressionMode.Water,
            WaterApplicationWork,
            water,
            "실물 물로 화재 진화",
            ready
                ? string.Empty
                : $"environmental-fire-clean-water-pending:{water}");
        return true;
    }

    public bool TryBegin(
        BuildableObject target,
        CharacterActor actor,
        EnvironmentalFireSuppressionWorkSnapshot expectedWork,
        out EnvironmentalFireSuppressionAttempt attempt,
        out string failureReason)
    {
        attempt = null;
        failureReason = string.Empty;
        if (!TryGetWork(target, actor, out var work) || !work.Available)
        {
            failureReason = string.IsNullOrWhiteSpace(work.UnavailableReason)
                ? "environmental-fire-work-unavailable"
                : work.UnavailableReason;
            return false;
        }
        if (expectedWork.Fire == null
            || !string.Equals(
                expectedWork.Fire.FireId,
                work.Fire.FireId,
                StringComparison.Ordinal)
            || expectedWork.Mode != work.Mode
            || !IsFinitePositive(expectedWork.RequiredWork))
        {
            failureReason = "environmental-fire-work-changed-during-response";
            return false;
        }
        if (!CharacterPersistentIdentity.TryGet(actor, out CharacterId workerId))
        {
            failureReason = "environmental-fire-worker-identity-missing";
            return false;
        }

        Vector2Int stand = target.Grid != null
            ? target.Grid.GetXY(actor.transform.position)
            : work.Fire.Position;
        if (!suppressionAccess.CanSuppress(
                workerId.Value,
                stand,
                work.Fire.Target,
                out failureReason))
        {
            return false;
        }
        string operationId = string.Concat(
            "environmental-fire-suppression:",
            work.Fire.FireId,
            ":",
            workerId.Value,
            ":work=",
            work.Fire.TotalSuppressionWork.ToString(
                "R",
                CultureInfo.InvariantCulture),
            ":water=",
            work.Fire.TotalWaterConsumed.ToString(CultureInfo.InvariantCulture));
        string leaseId = string.Empty;
        if (work.Mode == EnvironmentalFireSuppressionMode.Water)
        {
            string destinationId = EnvironmentalFireWaterDestinationIdentity.Create(
                work.Fire.FireId);
            WorldItemStackSnapshot stack = FindDeliveredWater(
                destinationId,
                work.Fire.Position,
                work.RequiredWater);
            ItemQuantityLease lease = null;
            DomainFailure reserveFailure = DomainFailure.None;
            if (stack == null
                || !reservations.TryReserve(
                    operationId,
                    workerId.Value,
                    ItemReservationPurpose.FacilityBuffer,
                    destinationId,
                    new ItemQuantityReservationRequest(
                        new ItemStackId(stack.StackId),
                        work.RequiredWater,
                        stack.ReservationSignature),
                    out lease,
                    out reserveFailure))
            {
                failureReason = "environmental-fire-water-reservation-failed:"
                    + (reserveFailure.IsFailure
                        ? reserveFailure.Code.ToString()
                        : destinationId);
                return false;
            }
            leaseId = lease.leaseId;
        }

        var command = new EnvironmentalFireSuppressionCommand(
            operationId,
            work.Fire.FireId,
            workerId.Value,
            stand,
            work.Mode,
            expectedWork.RequiredWork,
            leaseId,
            work.RequiredWater,
            actor.Identity?.DisplayName);
        attempt = new EnvironmentalFireSuppressionAttempt(
            command,
            work.Fire.Intensity,
            fireCommands,
            reservations);
        return true;
    }

    public bool ApplyResponderExposure(
        EnvironmentalFireSuppressionAttempt attempt,
        CharacterActor actor,
        float approvedWork)
    {
        if (attempt == null
            || actor == null
            || !IsFinitePositive(approvedWork)
            || !fires.TryGet(attempt.Command.FireId, out _)
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId actorId)
            || !string.Equals(
                actorId.Value,
                attempt.Command.WorkerId,
                StringComparison.Ordinal))
        {
            return false;
        }

        float amount = approvedWork
            * HeatExposurePerWorkAtFullIntensity
            * Mathf.Clamp01(attempt.ExposureIntensity);
        return amount > 0f && exposure.AddHeatExposure(actorId, amount);
    }

    public EnvironmentalFireSuppressionResult Complete(
        EnvironmentalFireSuppressionAttempt attempt)
    {
        if (attempt == null)
        {
            return new EnvironmentalFireSuppressionResult(
                EnvironmentalFireSuppressionDisposition.InvalidRequest,
                0f,
                0f,
                0,
                string.Empty,
                "A live environmental-fire work attempt is required.");
        }

        EnvironmentalFireSuppressionResult result =
            fireCommands.TryApplySuppression(attempt.Command);
        if (result.Applied)
            attempt.MarkTerminal();
        return result;
    }

    private void ReconcileActiveFire(
        EnvironmentalFireSnapshot fire,
        HashSet<string> activeIncidents)
    {
        if (!targets.TryGetTarget(
                fire.Target,
                out EnvironmentalFireTargetSnapshot targetSnapshot)
            || !TryResolveBuilding(fire.Target.TargetId, out BuildableObject building))
        {
            return;
        }

        bool created = !stateByFireId.TryGetValue(
            fire.FireId,
            out ResponseState state);
        if (created)
        {
            state = new ResponseState
            {
                FireId = fire.FireId,
                Building = building,
                Position = targetSnapshot.Position,
                SeverityBand = SeverityBand(fire, targetSnapshot.Profile),
                IsolationBlocked = IsIsolationBlocked(fire),
                WaterReady = false
            };
            stateByFireId.Add(fire.FireId, state);
            AddWorkMarker(state);
        }
        else
        {
            bool rebound = !ReferenceEquals(state.Building, building);
            state.Building = building;
            state.Position = targetSnapshot.Position;
            if (rebound)
            {
                state.SeverityBand = SeverityBand(fire, targetSnapshot.Profile);
                state.IsolationBlocked = IsIsolationBlocked(fire);
                state.WaterReady = false;
            }
        }
        EnsureWorkMarker(state);

        string incidentId = IncidentId(fire.FireId);
        if (!activeIncidents.Contains(incidentId))
        {
            PublishIncident(fire, state.SeverityBand);
            activeIncidents.Add(incidentId);
            RaiseStarted(fire);
        }

        int severity = SeverityBand(fire, targetSnapshot.Profile);
        bool worsened = severity > state.SeverityBand;
        if (worsened)
        {
            PublishIncident(fire, severity);
            RaiseWorsened(fire);
        }
        state.SeverityBand = severity;

        bool isolationBlocked = IsIsolationBlocked(fire);
        bool isolationResolved = state.IsolationBlocked && !isolationBlocked;
        state.IsolationBlocked = isolationBlocked;

        bool needsWater = fire.Intensity
            > settings.MaximumInitialAttackIntensity + Epsilon;
        if (needsWater)
        {
            EnsureWaterClaim(state);
            int required = RequiredWater(fire, targetSnapshot.Profile);
            RequestMissingCleanWater(state, required);
            bool ready = FindDeliveredWater(
                EnvironmentalFireWaterDestinationIdentity.Create(fire.FireId),
                state.Position,
                required) != null;
            bool becameReady = ready && !state.WaterReady;
            state.WaterReady = ready;
            if (becameReady)
                workforce.RequestOneWorkerToReplanFor(
                    BuiltInWorkTypeIds.ThreatMitigation,
                    forceInterrupt: false);
        }
        else
        {
            state.WaterReady = false;
            RetireWaterDestination(state);
        }

        if (created || worsened || isolationResolved)
        {
            facilityCandidates.MarkDynamicStateDirty();
            workforce.RequestOneWorkerToReplanFor(
                BuiltInWorkTypeIds.ThreatMitigation,
                forceInterrupt: false);
        }
    }

    private void ReconcileEndedFire(
        ResponseState state,
        HashSet<string> activeIncidents)
    {
        RemoveWorkMarker(state);
        if (!RetireWaterDestination(state))
            return;

        string incidentId = IncidentId(state.FireId);
        if (activeIncidents.Contains(incidentId))
        {
            ResolveIncident(incidentId);
            activeIncidents.Remove(incidentId);
        }
        EnvironmentalFireHistorySnapshot history = FindHistory(state.FireId);
        if (history?.EndReason == EnvironmentalFireEndReason.Suppressed)
            RaiseExtinguished(history.Fire);
        stateByFireId.Remove(state.FireId);
        facilityCandidates.Clear();
    }

    private void AddWorkMarker(ResponseState state)
    {
        RuntimeWorkCapabilityMarker marker =
            state.Building.GetComponent<RuntimeWorkCapabilityMarker>()
            ?? state.Building.gameObject.AddComponent<RuntimeWorkCapabilityMarker>();
        marker.Add(
            WorkSourcePrefix + state.FireId,
            BuiltInWorkTypeIds.ThreatMitigation);
        if (facilityCandidates is IRuntimeWorkCandidateCache runtimeCandidates)
        {
            runtimeCandidates.PrioritizeRuntimeWorkCandidate(
                state.Building,
                BuiltInWorkTypeIds.ThreatMitigation);
        }
        else
        {
            facilityCandidates.Clear();
        }
    }

    private void EnsureWorkMarker(ResponseState state)
    {
        if (RuntimeWorkCapabilityUtility.Supports(
                state.Building,
                BuiltInWorkTypeIds.ThreatMitigation))
        {
            return;
        }
        AddWorkMarker(state);
    }

    private static void RemoveWorkMarker(ResponseState state)
    {
        if (state.Building != null)
        {
            state.Building.GetComponent<RuntimeWorkCapabilityMarker>()?
                .RemoveSource(WorkSourcePrefix + state.FireId);
        }
    }

    private void EnsureWaterClaim(ResponseState state)
    {
        string destinationId = EnvironmentalFireWaterDestinationIdentity.Create(
            state.FireId);
        if (state.WaterClaim != null
            && state.WaterClaim.DropPosition == state.Position
            && destinationClaimQuery.TryGetClaim(
                state.WaterClaim.DestinationId,
                state.WaterClaim.DropPosition,
                out FacilityBufferDestinationClaim current)
            && string.Equals(
                current.OwnerDomain,
                ClaimOwnerDomain,
                StringComparison.Ordinal)
            && string.Equals(
                current.OwnerOperationId,
                state.FireId,
                StringComparison.Ordinal))
        {
            return;
        }
        if (state.WaterClaim != null)
        {
            bool claimStillPublished = destinationClaimQuery.TryGetClaim(
                state.WaterClaim.DestinationId,
                state.WaterClaim.DropPosition,
                out _);
            if (claimStillPublished && !RetireWaterDestination(state))
                return;
            if (!claimStillPublished)
                state.WaterClaim = null;
        }

        var desired = new FacilityBufferDestinationClaim(
            destinationId,
            state.Position,
            ClaimOwnerDomain,
            state.FireId,
            state.Building.PersistentInstanceId.Value,
            FacilityBufferDestinationAnchorKind.LiveBuilding);
        if (destinationClaims.TryClaim(desired, out _, out _))
            state.WaterClaim = desired;
    }

    private bool RetireWaterDestination(ResponseState state)
    {
        if (state.WaterClaim == null)
            return true;
        if (!destinationRelease.TryReleaseAtOwnerPosition(
                state.WaterClaim.DestinationId,
                state.WaterClaim.DropPosition,
                ClaimReleaseReason,
                out _,
                out _))
        {
            return false;
        }

        bool revoked = destinationClaims.TryRevoke(
            state.WaterClaim,
            out FacilityBufferDestinationClaimFailureCode failure,
            out _);
        if (!revoked
            && failure != FacilityBufferDestinationClaimFailureCode.ClaimNotFound)
        {
            return false;
        }
        state.WaterClaim = null;
        return true;
    }

    private void RequestMissingCleanWater(ResponseState state, int required)
    {
        if (state.WaterClaim == null || required <= 0)
            return;
        string destinationId = state.WaterClaim.DestinationId;
        WorldItemStackSnapshot[] snapshots = items.GetAllStacks()
            .Where(stack => stack != null)
            .ToArray();
        Dictionary<string, WorldItemStackSnapshot> byId = snapshots
            .Where(stack => !string.IsNullOrWhiteSpace(stack.StackId))
            .ToDictionary(stack => stack.StackId, StringComparer.Ordinal);
        int destined = snapshots
            .Where(stack => IsCleanWater(stack)
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            .Sum(stack => Mathf.Max(0, stack.Quantity));
        int carried = items.CaptureHaulDeliveryIntentsByDestination(destinationId)
            .Where(intent => intent != null)
            .SelectMany(intent => intent.commitments
                ?? new List<HaulDeliveryItemCommitmentSaveData>())
            .Where(commitment => commitment != null
                && byId.TryGetValue(
                    commitment.carriedStackId,
                    out WorldItemStackSnapshot stack)
                && IsCleanWater(stack))
            .Sum(commitment => Mathf.Max(0, commitment.quantity));
        int missing = Mathf.Max(0, required - destined - carried);
        if (missing <= 0)
            return;

        bool requestedAny = false;
        foreach (WorldItemStackSnapshot source in snapshots
                     .Where(IsUnassignedCleanWaterSource)
                     .OrderBy(stack => stack.StackId, StringComparer.Ordinal))
        {
            if (missing <= 0)
                break;
            int requestedAmount = Mathf.Min(missing, source.AvailableQuantity);
            if (!transfers.TryRequestStackDelivery(
                    new ItemStackId(source.StackId),
                    requestedAmount,
                    state.Position,
                    destinationId,
                    out int requested,
                    out _)
                || requested <= 0)
            {
                continue;
            }
            items.PrioritizeHaul(source.StackId);
            missing -= requested;
            requestedAny = true;
        }

        if (requestedAny)
        {
            workforce.RequestOneHaulerToReplan(
                clearFailures: true,
                forceInterrupt: true,
                forcePriorityWakeFanout: true);
        }
    }

    private WorldItemStackSnapshot FindDeliveredWater(
        string destinationId,
        Vector2Int position,
        int required) => items.GetAllStacks()
        .Where(stack => IsCleanWater(stack)
            && stack.State == WorldItemStackState.FacilityBuffer
            && stack.Position == position
            && stack.AvailableQuantity >= required
            && string.Equals(
                stack.DestinationId,
                destinationId,
                StringComparison.Ordinal))
        .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
        .FirstOrDefault();

    private static bool IsUnassignedCleanWaterSource(
        WorldItemStackSnapshot stack) => IsCleanWater(stack)
        && stack.AvailableQuantity > 0
        && !stack.Forbidden
        && (stack.State == WorldItemStackState.Loose
            && string.IsNullOrWhiteSpace(stack.DestinationId)
            || stack.State == WorldItemStackState.Stored
            && string.IsNullOrWhiteSpace(stack.SourceStorageDestinationId));

    private static bool IsCleanWater(WorldItemStackSnapshot stack) =>
        stack != null
        && string.Equals(
            stack.ItemId,
            CleanWaterItemId,
            StringComparison.Ordinal)
        && IsFiniteNonNegative(stack.Contamination)
        && stack.Contamination <= MaximumCleanWaterContamination + Epsilon;

    private static int RequiredWater(
        EnvironmentalFireSnapshot fire,
        EnvironmentalFireProfile profile) => Mathf.Max(
        1,
        Mathf.CeilToInt(fire.Intensity / profile.WaterSuppressionPerUnit));

    private bool IsIsolationBlocked(EnvironmentalFireSnapshot fire) =>
        fire.RequiresElectricalIsolation
        && (!electricalSafety.TryGetSafety(fire.Target, out var safety)
            || !safety.Target.Equals(fire.Target)
            || !safety.IsolationConfirmed);

    private int SeverityBand(
        EnvironmentalFireSnapshot fire,
        EnvironmentalFireProfile profile)
    {
        if (fire.Intensity + Epsilon >= profile.MinimumSpreadIntensity)
            return 2;
        return fire.Intensity
            > settings.MaximumInitialAttackIntensity + Epsilon ? 1 : 0;
    }

    private static EnvironmentalFireSuppressionWorkSnapshot Unavailable(
        EnvironmentalFireSnapshot fire,
        string reason) => new(
            false,
            fire,
            EnvironmentalFireSuppressionMode.InitialAttack,
            0f,
            0,
            "화재 진화",
            reason);

    private void PublishIncident(EnvironmentalFireSnapshot fire, int severity)
    {
        string incidentId = IncidentId(fire.FireId);
        EmergencyAccountingResult result = alerts.PublishIncidentSignal(
            new SettlementIncidentSignal(
                incidentId,
                SettlementThreatAlertLevel.Red,
                alerts.GetNextIncidentRevision(incidentId),
                "environmental-fire",
                $"fire={fire.FireId}; intensity={fire.Intensity:0.###}; severity={severity}"));
        RequireAlertSuccess(result);
    }

    private void ResolveIncident(string incidentId)
    {
        EmergencyAccountingResult result = alerts.ResolveIncident(
            incidentId,
            alerts.GetNextIncidentRevision(incidentId));
        if (!result.Success
            && !string.Equals(
                result.Code,
                "SettlementIncidentMissing",
                StringComparison.Ordinal))
        {
            RequireAlertSuccess(result);
        }
    }

    private void RaiseStarted(EnvironmentalFireSnapshot fire) => events.RaiseAlert(
        "화재 발생",
        $"시설 화재가 발생했습니다. 강도 {fire.Intensity:0.##}",
        EventAlertImportance.High,
        "화재");

    private void RaiseWorsened(EnvironmentalFireSnapshot fire) => events.RaiseAlert(
        "화재 악화",
        $"시설 화재가 의미 있는 위험 단계로 악화되었습니다. 강도 {fire.Intensity:0.##}",
        EventAlertImportance.High,
        "화재");

    private void RaiseExtinguished(EnvironmentalFireSnapshot fire) =>
        events.RaiseAlert(
            "화재 진화",
            $"시설 화재를 진화했습니다. 사용한 물 {fire?.TotalWaterConsumed ?? 0}개",
            EventAlertImportance.Medium,
            "화재");

    private EnvironmentalFireHistorySnapshot FindHistory(string fireId) =>
        fires.History.LastOrDefault(entry => entry?.Fire != null
            && string.Equals(
                entry.Fire.FireId,
                fireId,
                StringComparison.Ordinal));

    private bool TryResolveBuilding(
        string targetId,
        out BuildableObject building)
    {
        building = (buildings.Buildings ?? Array.Empty<BuildableObject>())
            .FirstOrDefault(candidate => candidate != null
                && !candidate.isDestroy
                && candidate.PersistentInstanceId.IsValid
                && string.Equals(
                    candidate.PersistentInstanceId.Value,
                    targetId,
                    StringComparison.Ordinal));
        return building != null;
    }

    private static string IncidentId(string fireId) => IncidentPrefix + fireId;

    private static void RequireAlertSuccess(EmergencyAccountingResult result)
    {
        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Environmental-fire alert publication failed: {result.Code}: {result.Message}");
        }
    }

    private static bool IsFinitePositive(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

    private static bool IsFiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
}
