using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum AutomationMode
{
    Manual = 0,
    PoweredAssist = 1,
    Automatic = 2
}

/// <summary>
/// Root-backed read authority for execution admission. It exposes only the
/// current authored-facility mode and does not own or cache automation state.
/// </summary>
public interface IAutomationExecutionModeQuery
{
    AutomationMode GetMode(BuildingInstanceId facilityId);
}

public static class AutomationModeTransitionRules
{
    public static bool HasActiveManualExecution(
        bool hasFacilityReservation,
        bool hasAllocatedWorker,
        bool hasBillReservation) =>
        hasFacilityReservation || hasAllocatedWorker || hasBillReservation;

    public static bool TryAuthorize(
        AutomationMode targetMode,
        bool hasActiveManualWorker,
        out string failureReason)
    {
        if (!Enum.IsDefined(typeof(AutomationMode), targetMode))
        {
            failureReason = "automation-mode-invalid";
            return false;
        }
        if (targetMode == AutomationMode.Automatic && hasActiveManualWorker)
        {
            failureReason = "automatic-mode-manual-worker-active";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }
}

public readonly struct AutomationPowerDemandProfile
{
    public AutomationPowerDemandProfile(
        float assistedPowerDemand,
        float automaticPowerDemand)
    {
        AssistedPowerDemand = assistedPowerDemand;
        AutomaticPowerDemand = automaticPowerDemand;
    }

    public float AssistedPowerDemand { get; }
    public float AutomaticPowerDemand { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class AutomationPowerDemandRules
{
    public static float Resolve(
        AutomationMode mode,
        AutomationPowerDemandProfile profile)
    {
        return mode switch
        {
            AutomationMode.PoweredAssist =>
                Math.Max(0f, profile.AssistedPowerDemand),
            AutomationMode.Automatic =>
                Math.Max(0f, profile.AutomaticPowerDemand),
            _ => 0f
        };
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum InfrastructureStatusCode
{
    None = 0,
    PowerUnavailable,
    OutputTargetReached,
    OutputSpaceUnavailable,
    InputDeliveryPending,
    StorageCapacityUnavailable,
    MaintenanceRequired,
    ProductionOrderUnavailable,
    ProductionMaterialUnavailable,
    ProductionOutputUnavailable,
    ConveyorRouteUnavailable,
    ConveyorFilterMismatch,
    ConveyorDestinationFull,
    ConveyorDeadlocked,
    ConveyorOverflowApprovalRequired
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct InfrastructureStatus
{
    public InfrastructureStatus(
        InfrastructureStatusCode code,
        params string[] parameters)
    {
        Code = code;
        Parameters = parameters ?? Array.Empty<string>();
    }

    public InfrastructureStatusCode Code { get; }
    public IReadOnlyList<string> Parameters { get; }
    public bool IsBlocked => Code != InfrastructureStatusCode.None;
    public static InfrastructureStatus None =>
        new InfrastructureStatus(InfrastructureStatusCode.None);
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AutomationFacilitySaveData
{
    public string buildingInstanceId = string.Empty;
    public AutomationMode mode;
    public float maintenance;
    public float fault;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonAutomationSaveData
{
    public const int CurrentVersion = 2;
    public int version = CurrentVersion;
    public List<AutomationFacilitySaveData> facilities =
        new List<AutomationFacilitySaveData>();
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AutomationFacilitySnapshot
{
    public BuildingInstanceId BuildingId { get; set; }
    public AutomationMode Mode { get; set; }
    public bool Powered { get; set; }
    public bool Operational { get; set; }
    public float WorkRate { get; set; }
    public float Maintenance { get; set; }
    public float Fault { get; set; }
    public InfrastructureStatus Status { get; set; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
internal sealed class AutomationFacilityState
{
    public AutomationMode Mode = AutomationMode.Manual;
    public float Maintenance = 100f;
    public float Fault;
    public InfrastructureStatus Status;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
internal sealed class AutomationAggregateState
{
    public readonly Dictionary<string, AutomationFacilityState> Facilities =
        new Dictionary<string, AutomationFacilityState>(StringComparer.Ordinal);
    public int Version;

    public AutomationAggregateState DeepClone()
    {
        AutomationAggregateState clone =
            new AutomationAggregateState { Version = Version };
        foreach (KeyValuePair<string, AutomationFacilityState> pair in Facilities)
        {
            AutomationFacilityState source = pair.Value;
            clone.Facilities.Add(pair.Key, new AutomationFacilityState
            {
                Mode = source.Mode,
                Maintenance = source.Maintenance,
                Fault = source.Fault,
                Status = source.Status
            });
        }

        return clone;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AutomationRestoreCandidate
{
    internal AutomationRestoreCandidate(AutomationAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal AutomationAggregateState State { get; }
}

public sealed class AutomationFacilityStateSession
{
    private readonly AutomationFacilityState state;

    internal AutomationFacilityStateSession(AutomationFacilityState state)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public AutomationMode Mode => state.Mode;
    public float Maintenance => state.Maintenance;
    public float Fault => state.Fault;
    public InfrastructureStatus Status => state.Status;

    public void SetMode(AutomationMode mode) => state.Mode = mode;
    public void SetCondition(float maintenance, float fault)
    {
        state.Maintenance = Math.Max(0f, Math.Min(100f, maintenance));
        state.Fault = Math.Max(0f, Math.Min(100f, fault));
    }
    public void ApplyMaintenance(float amount)
    {
        float applied = Math.Max(0f, amount);
        SetCondition(
            state.Maintenance + applied,
            state.Fault - applied * 0.5f);
        state.Status = InfrastructureStatus.None;
    }
    public void SetStatus(InfrastructureStatus status) => state.Status = status;
}

public sealed class AutomationStateSession
{
    private readonly DungeonRuntimeAggregateRootStore rootStore;

    public AutomationStateSession(DungeonRuntimeAggregateRootStore rootStore)
    {
        this.rootStore = rootStore
            ?? throw new ArgumentNullException(nameof(rootStore));
    }

    private AutomationAggregateState Writable =>
        rootStore.GetOrCreateWritable(
            () => new AutomationAggregateState(),
            state => state.DeepClone());
    private AutomationAggregateState Current =>
        rootStore.GetOrCreate(() => new AutomationAggregateState());

    public int Version => Current.Version;
    public void IncrementVersion() => Writable.Version++;
    public AutomationFacilityStateSession GetOrCreate(string facilityId)
    {
        string id = facilityId?.Trim() ?? string.Empty;
        if (!Writable.Facilities.TryGetValue(id, out AutomationFacilityState state))
        {
            state = new AutomationFacilityState();
            Writable.Facilities[id] = state;
        }
        return new AutomationFacilityStateSession(state);
    }
    public bool TryGet(
        string facilityId,
        out AutomationFacilityStateSession facility)
    {
        if (!string.IsNullOrWhiteSpace(facilityId)
            && Current.Facilities.TryGetValue(
                facilityId.Trim(),
                out AutomationFacilityState state))
        {
            facility = new AutomationFacilityStateSession(state);
            return true;
        }
        facility = null;
        return false;
    }
    public DungeonAutomationSaveData Capture() => new()
    {
        facilities = Current.Facilities
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new AutomationFacilitySaveData
            {
                buildingInstanceId = pair.Key,
                mode = pair.Value.Mode,
                maintenance = pair.Value.Maintenance,
                fault = pair.Value.Fault
            })
            .ToList()
    };
    public static AutomationRestoreCandidate CreateRestoreCandidate(
        IEnumerable<AutomationFacilitySaveData> facilities)
    {
        AutomationAggregateState restored = new() { Version = 1 };
        foreach (AutomationFacilitySaveData saved in facilities
                 ?? Array.Empty<AutomationFacilitySaveData>())
        {
            if (saved == null
                || !new BuildingInstanceId(saved.buildingInstanceId).IsValid)
            {
                continue;
            }
            restored.Facilities[saved.buildingInstanceId.Trim()] =
                new AutomationFacilityState
                {
                    Mode = Enum.IsDefined(typeof(AutomationMode), saved.mode)
                        ? saved.mode
                        : AutomationMode.Manual,
                    Maintenance = Math.Max(0f, Math.Min(100f, saved.maintenance)),
                    Fault = Math.Max(0f, Math.Min(100f, saved.fault))
                };
        }
        return new AutomationRestoreCandidate(restored);
    }
    public void Restore(AutomationRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        rootStore.Replace(candidate.State);
    }
}
