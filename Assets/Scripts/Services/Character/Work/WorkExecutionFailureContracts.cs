using System;
using System.Collections.Generic;

public enum WorkExecutorRoute
{
    CommonFacilityTimed,
    CommonWorkOrder,
    RegisteredHandler,
    CommonDutyMonitor,
    RestockHaul,
    HaulAbility,
    HuntAbility,
    RescueAbility,
    RestAbility
}

public enum WorkFailureAxisCoverage
{
    CommonExecutor,
    SpecialExecutor,
    PolicyExempt,
    NotApplicable,
    Missing
}

[Flags]
public enum WorkReservationFailureKinds
{
    None = 0,
    Facility = 1 << 0,
    Recipe = 1 << 1,
    Item = 1 << 2
}

/// <summary>
/// Authored audit profile for the failure axes shared by every built-in work
/// type. This is validation metadata, not an execution fallback: Missing must
/// remain a failing matrix row until the live executor owns that transition.
/// </summary>
public sealed class WorkExecutionFailureProfile
{
    public WorkExecutionFailureProfile(
        WorkTypeId workTypeId,
        WorkExecutorRoute route,
        Type executorType,
        WorkReservationFailureKinds reservationKinds,
        WorkFailureAxisCoverage targetInvalidation,
        WorkFailureAxisCoverage reservationFailure,
        WorkFailureAxisCoverage safeCheckpointCancellation,
        string evidence)
    {
        if (!workTypeId.IsValid)
            throw new ArgumentException("Work type id is required.", nameof(workTypeId));
        WorkTypeId = workTypeId;
        Route = route;
        ExecutorType = executorType
            ?? throw new ArgumentNullException(nameof(executorType));
        ReservationKinds = reservationKinds;
        TargetInvalidation = targetInvalidation;
        ReservationFailure = reservationFailure;
        SafeCheckpointCancellation = safeCheckpointCancellation;
        Evidence = string.IsNullOrWhiteSpace(evidence)
            ? throw new ArgumentException("Failure-contract evidence is required.", nameof(evidence))
            : evidence.Trim();
    }

    public WorkTypeId WorkTypeId { get; }
    public WorkExecutorRoute Route { get; }
    public Type ExecutorType { get; }
    public WorkReservationFailureKinds ReservationKinds { get; }
    public WorkFailureAxisCoverage TargetInvalidation { get; }
    public WorkFailureAxisCoverage ReservationFailure { get; }
    public WorkFailureAxisCoverage SafeCheckpointCancellation { get; }
    public string Evidence { get; }

    public bool IsImplemented =>
        TargetInvalidation != WorkFailureAxisCoverage.Missing
        && ReservationFailure != WorkFailureAxisCoverage.Missing
        && SafeCheckpointCancellation != WorkFailureAxisCoverage.Missing;
}

public static class BuiltInWorkExecutionFailureProfiles
{
    private const WorkFailureAxisCoverage Common =
        WorkFailureAxisCoverage.CommonExecutor;
    private const WorkFailureAxisCoverage Special =
        WorkFailureAxisCoverage.SpecialExecutor;
    private const WorkFailureAxisCoverage Exempt =
        WorkFailureAxisCoverage.PolicyExempt;
    private const WorkFailureAxisCoverage NA =
        WorkFailureAxisCoverage.NotApplicable;

    private static readonly WorkExecutionFailureProfile[] Profiles =
    {
        CommonTimed(BuiltInWorkTypeIds.Operate, typeof(WorkTaskExecutor), "generic timed facility operation"),
        Profile(BuiltInWorkTypeIds.Restock, WorkExecutorRoute.RestockHaul,
            typeof(WorkTaskExecutor), WorkReservationFailureKinds.Facility | WorkReservationFailureKinds.Item,
            Common, Special, Common,
            "stable restock operation owns a quantity Lease; cancellation releases and destination commit consumes exactly once"),
        CommonOrder(BuiltInWorkTypeIds.Construct, WorkReservationFailureKinds.Facility | WorkReservationFailureKinds.Item,
            "work-order material readiness and construction workforce lease"),
        Handler(BuiltInWorkTypeIds.Repair, typeof(RepairWorkExecutionHandler), WorkReservationFailureKinds.Facility,
            "repair handler plus common persistent/timed loops"),
        Handler(BuiltInWorkTypeIds.Clean, typeof(CleanWorkExecutionHandler), WorkReservationFailureKinds.Facility,
            "clean handler plus common timed loop"),
        Handler(BuiltInWorkTypeIds.Research, typeof(ResearchWorkExecutionHandler), WorkReservationFailureKinds.Facility,
            "research workforce lease and common timed loop"),
        Duty(BuiltInWorkTypeIds.Guard, "guard is emergency response; duty monitor owns invalidation"),
        CommonTimed(BuiltInWorkTypeIds.Reception, typeof(WorkTaskExecutor), "exterior reception timed work"),
        Profile(BuiltInWorkTypeIds.Rescue, WorkExecutorRoute.RescueAbility,
            typeof(AbilityRescue), WorkReservationFailureKinds.None,
            Special, NA, Exempt, "rescue ability owns patient invalidation; emergency-response work is not suspendible"),
        Profile(BuiltInWorkTypeIds.Rest, WorkExecutorRoute.RestAbility,
            typeof(AIRest), WorkReservationFailureKinds.Facility,
            Special, Special, Exempt, "rest adapter owns facility failure; protected recovery is not suspendible"),
        Handler(BuiltInWorkTypeIds.Craft, typeof(CraftWorkExecutionAdapter),
            WorkReservationFailureKinds.Facility | WorkReservationFailureKinds.Recipe | WorkReservationFailureKinds.Item,
            "production BeginWork owns recipe/input reservation failure"),
        Profile(BuiltInWorkTypeIds.Haul, WorkExecutorRoute.HaulAbility,
            typeof(AbilityHaul), WorkReservationFailureKinds.Item,
            Special, Special, Special,
            "AbilityHaul renews quantity leases, revalidates pickup/delivery, and cancels at movement/pickup boundaries"),
        Profile(BuiltInWorkTypeIds.Hunt, WorkExecutorRoute.HuntAbility,
            typeof(AbilityHunt), WorkReservationFailureKinds.Item,
            Special, Special, Special,
            "AbilityHunt owns target reservation and cancellation checks between movement/reload/attack phases"),
        Handler(BuiltInWorkTypeIds.Butcher, typeof(ButcherWorkExecutionHandler), WorkReservationFailureKinds.Facility,
            "butcher availability revalidation plus common timed loop"),
        Handler(BuiltInWorkTypeIds.DrawWater, typeof(SurvivalWorkExecutionHandler), WorkReservationFailureKinds.Facility,
            "survival availability revalidation plus common timed loop"),
        Handler(BuiltInWorkTypeIds.Cook, typeof(SurvivalWorkExecutionHandler),
            WorkReservationFailureKinds.Facility | WorkReservationFailureKinds.Recipe | WorkReservationFailureKinds.Item,
            "production BeginWork owns recipe/input reservation failure"),
        Handler(BuiltInWorkTypeIds.Treat, typeof(SurvivalWorkExecutionHandler),
            WorkReservationFailureKinds.Facility | WorkReservationFailureKinds.Item,
            "medical availability and item consumption failure return through the common terminal path",
            safeCheckpoint: Exempt),
        Handler(BuiltInWorkTypeIds.Surgery, typeof(SurgeryWorkExecutionHandler),
            WorkReservationFailureKinds.Facility | WorkReservationFailureKinds.Item,
            "surgery TryReserveWork/ReleaseDoctor owns the special non-interruptible reservation lifecycle",
            safeCheckpoint: Exempt),
        Handler(BuiltInWorkTypeIds.Refuel, typeof(SurvivalWorkExecutionHandler),
            WorkReservationFailureKinds.Facility | WorkReservationFailureKinds.Item,
            "fuel/recharge begin and persistent application failures return through the common terminal path"),
        Handler(BuiltInWorkTypeIds.Warden, typeof(WardenWorkExecutionUnityAdapter), WorkReservationFailureKinds.Facility,
            "warden adapter plus common timed/persistent loop"),
        Handler(BuiltInWorkTypeIds.Perform, typeof(PerformWorkExecutionUnityAdapter), WorkReservationFailureKinds.Facility,
            "performance adapter plus common timed/persistent loop"),
        Handler(BuiltInWorkTypeIds.Gather, typeof(ResourceGatheringWorkExecutionHandler), WorkReservationFailureKinds.Facility,
            "world-resource persistent progress revalidation"),
        Handler(BuiltInWorkTypeIds.Sow, typeof(ResourceGatheringWorkExecutionHandler), WorkReservationFailureKinds.Facility,
            "crop persistent progress revalidation"),
        Handler(BuiltInWorkTypeIds.Harvest, typeof(ResourceGatheringWorkExecutionHandler), WorkReservationFailureKinds.Facility,
            "crop persistent progress revalidation"),
        Handler(BuiltInWorkTypeIds.Logging, typeof(ResourceGatheringWorkExecutionHandler), WorkReservationFailureKinds.Facility,
            "world-resource persistent progress revalidation"),
        Handler(BuiltInWorkTypeIds.Quarry, typeof(ResourceGatheringWorkExecutionHandler),
            WorkReservationFailureKinds.Facility | WorkReservationFailureKinds.Recipe | WorkReservationFailureKinds.Item,
            "resource node or production BeginWork owns quarry failure"),
        Handler(BuiltInWorkTypeIds.AnimalCare, typeof(AnimalHusbandryWorkExecutionAdapter), WorkReservationFailureKinds.Facility,
            "husbandry adapter plus common persistent loop"),
        Handler(BuiltInWorkTypeIds.GrandProject, typeof(GrandProjectWorkExecutionHandler),
            WorkReservationFailureKinds.Facility | WorkReservationFailureKinds.Item,
            "project workforce/material state plus common persistent loop"),
        Handler(BuiltInWorkTypeIds.ThreatMitigation, typeof(ThreatMitigationWorkExecutionHandler),
            WorkReservationFailureKinds.Facility,
            "threat adapter owns target invalidation; emergency-response work is not suspendible",
            safeCheckpoint: Exempt),
        Handler(BuiltInWorkTypeIds.Plumbing, typeof(PlumbingWorkExecutionHandler), WorkReservationFailureKinds.Facility,
            "plumbing query revalidation plus common timed loop"),
        CommonOrder(BuiltInWorkTypeIds.Dismantle, WorkReservationFailureKinds.Facility,
            "work-order cancellation/material state and common checkpoint loop")
    };

    public static IReadOnlyList<WorkExecutionFailureProfile> All => Profiles;

    private static WorkExecutionFailureProfile CommonTimed(
        WorkTypeId id,
        Type executorType,
        string evidence) =>
        Profile(id, WorkExecutorRoute.CommonFacilityTimed, executorType,
            WorkReservationFailureKinds.Facility, Common, Common, Common, evidence);

    private static WorkExecutionFailureProfile CommonOrder(
        WorkTypeId id,
        WorkReservationFailureKinds reservations,
        string evidence) =>
        Profile(id, WorkExecutorRoute.CommonWorkOrder, typeof(WorkTaskExecutor),
            reservations, Common, Common, Common, evidence);

    private static WorkExecutionFailureProfile Duty(WorkTypeId id, string evidence) =>
        Profile(id, WorkExecutorRoute.CommonDutyMonitor, typeof(WorkDutyController),
            WorkReservationFailureKinds.Facility, Common, Common, Exempt, evidence);

    private static WorkExecutionFailureProfile Handler(
        WorkTypeId id,
        Type handlerType,
        WorkReservationFailureKinds reservations,
        string evidence,
        WorkFailureAxisCoverage safeCheckpoint = Common) =>
        Profile(id, WorkExecutorRoute.RegisteredHandler, handlerType,
            reservations, Common, Special, safeCheckpoint, evidence);

    private static WorkExecutionFailureProfile Profile(
        WorkTypeId id,
        WorkExecutorRoute route,
        Type executorType,
        WorkReservationFailureKinds reservations,
        WorkFailureAxisCoverage target,
        WorkFailureAxisCoverage reservation,
        WorkFailureAxisCoverage safeCheckpoint,
        string evidence) =>
        new WorkExecutionFailureProfile(
            id,
            route,
            executorType,
            reservations,
            target,
            reservations == WorkReservationFailureKinds.None ? NA : reservation,
            safeCheckpoint,
            evidence);
}
