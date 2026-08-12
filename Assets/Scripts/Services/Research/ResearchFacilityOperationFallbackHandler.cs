using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Auditable ownership map for every non-production research facility command.
/// The connection validator requires every command value to have one owner.
/// </summary>
public static class ResearchFacilityCommandConsumerRegistry
{
    public static bool HasExecutionContract(ResearchFacilityCommandKind command) =>
        command != ResearchFacilityCommandKind.None
        && DomainOwner(command).Length > 0;

    public static string DomainOwner(ResearchFacilityCommandKind command) =>
        command switch
        {
            ResearchFacilityCommandKind.GatheringPreparation or
            ResearchFacilityCommandKind.LoggingPreparation or
            ResearchFacilityCommandKind.DirectionalFelling => "workforce",
            ResearchFacilityCommandKind.BloodStageDrainage => "captivity",
            ResearchFacilityCommandKind.SelectiveBreeding or
            ResearchFacilityCommandKind.StableHarnessing or
            ResearchFacilityCommandKind.WildlifeTaming or
            ResearchFacilityCommandKind.BreedingSchedule => "husbandry",
            ResearchFacilityCommandKind.FlowMetering => "infrastructure",
            ResearchFacilityCommandKind.WeaponPatternAccess or
            ResearchFacilityCommandKind.ResonanceTuning => "equipment",
            ResearchFacilityCommandKind.CropCalendar or
            ResearchFacilityCommandKind.SoilDiagnostics or
            ResearchFacilityCommandKind.ClimateControl or
            ResearchFacilityCommandKind.SeedSelection => "agriculture",
            ResearchFacilityCommandKind.HouseholdRegistry or
            ResearchFacilityCommandKind.NurseryCare or
            ResearchFacilityCommandKind.ClassroomEducation or
            ResearchFacilityCommandKind.SupervisedApprenticeship or
            ResearchFacilityCommandKind.GenerationArchive or
            ResearchFacilityCommandKind.FamilyPartition or
            ResearchFacilityCommandKind.GuardianRegistry or
            ResearchFacilityCommandKind.CorpseCare or
            ResearchFacilityCommandKind.RetireeCare or
            ResearchFacilityCommandKind.MentorAcademy => "character-society",
            ResearchFacilityCommandKind.AgingAssessment or
            ResearchFacilityCommandKind.BiologicalAgeMeasurement or
            ResearchFacilityCommandKind.GeriatricCare or
            ResearchFacilityCommandKind.ChronicCare or
            ResearchFacilityCommandKind.PathogenDiagnosis or
            ResearchFacilityCommandKind.Serology => "medical",
            ResearchFacilityCommandKind.EpidemicBoard => "population-health",
            ResearchFacilityCommandKind.GeneticArchive or
            ResearchFacilityCommandKind.GeneticCounseling => "genetics",
            ResearchFacilityCommandKind.ClimateMapping or
            ResearchFacilityCommandKind.ChronometricNavigation => "expedition",
            ResearchFacilityCommandKind.SecureTradeVault => "commerce",
            ResearchFacilityCommandKind.DefenseControl => "defense",
            ResearchFacilityCommandKind.ApparelTailoring or
            ResearchFacilityCommandKind.ApparelDecoration or
            ResearchFacilityCommandKind.HandLaundry or
            ResearchFacilityCommandKind.IndoorDrying or
            ResearchFacilityCommandKind.PoweredLaundry or
            ResearchFacilityCommandKind.ApparelDisplay or
            ResearchFacilityCommandKind.DressingChange or
            ResearchFacilityCommandKind.ApparelRepair or
            ResearchFacilityCommandKind.FiberSorting or
            ResearchFacilityCommandKind.FiberScouring or
            ResearchFacilityCommandKind.ManualSpinning or
            ResearchFacilityCommandKind.TextileFinishing or
            ResearchFacilityCommandKind.PoweredSpinning or
            ResearchFacilityCommandKind.PoweredWeaving => "apparel-textile",
            _ => string.Empty
        };
}

/// <summary>
/// Executes authored research-facility commands through the normal work path.
/// Facilities with a production recipe use the production runtime instead;
/// command facilities reach this fallback after a completed operate cycle.
/// </summary>
public sealed class ResearchFacilityOperationFallbackHandler :
    IBuildingWorkCompletionFallbackHandler
{
    private static readonly WorkTypeId[] WorkTypes =
    {
        BuiltInWorkTypeIds.Operate
    };

    private readonly IApparelWorkOrderCommand apparelCommands;
    private readonly IApparelWorkOrderQuery apparelOrders;

    public ResearchFacilityOperationFallbackHandler(
        IApparelWorkOrderCommand apparelCommands,
        IApparelWorkOrderQuery apparelOrders)
    {
        this.apparelCommands = apparelCommands
            ?? throw new ArgumentNullException(nameof(apparelCommands));
        this.apparelOrders = apparelOrders
            ?? throw new ArgumentNullException(nameof(apparelOrders));
    }

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => WorkTypes;

    public int Apply(BuildingAbilityWorkContext context)
    {
        BuildableObject building = context.Building;
        ResearchFacilityCommandKind command =
            building?.BuildingData?.ResearchFacilityCommand
            ?? ResearchFacilityCommandKind.None;
        if (command == ResearchFacilityCommandKind.None)
        {
            return 0;
        }
        if (!ResearchFacilityCommandConsumerRegistry.HasExecutionContract(command))
        {
            throw new InvalidOperationException(
                $"Research facility command '{command}' has no execution contract.");
        }

        if (ResearchFacilityCommandConsumerRegistry.DomainOwner(command)
            == "apparel-textile")
        {
            return ApplyApparelOrder(context, command);
        }

        IBuildingVisitorPort actor = context.Actor;
        ApplyOperatorRecovery(actor, command, building);
        actor?.RecordActivity(
            building,
            new BuildingActivitySnapshot(
                BuildingActivityKinds.Work,
                BuildingActivityOutcomes.Completed,
                $"{DisplayName(building)}에서 {command} 작업을 완료했다.",
                BuiltInWorkTypeIds.Operate.Value,
                $"research-facility:{command}",
                "typed-facility-command-completed",
                1f,
                1,
                false));
        return 0;
    }

    private int ApplyApparelOrder(
        BuildingAbilityWorkContext context,
        ResearchFacilityCommandKind command)
    {
        string facilityId = context.Building.RequirePersistentInstanceId().Value;
        ApparelWorkOrderSaveData order = apparelOrders.Orders
            .Where(value => value != null
                && value.state != ApparelWorkOrderState.Completed
                && value.state != ApparelWorkOrderState.Failed
                && string.Equals(
                    value.facilityInstanceId,
                    facilityId,
                    StringComparison.Ordinal)
                && Matches(command, value))
            .OrderBy(value => value.orderId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (order == null
            || !apparelCommands.ApplyWork(
                order.orderId,
                CharacterBuildingVisitorAdapter.GetActorOrNull(context.Actor),
                order.requiredWork - order.completedWork,
                out _))
        {
            return 0;
        }

        context.Actor?.RecordActivity(
            context.Building,
            new BuildingActivitySnapshot(
                BuildingActivityKinds.Work,
                BuildingActivityOutcomes.Completed,
                $"{DisplayName(context.Building)}에서 {order.kind} 주문을 완료했다.",
                BuiltInWorkTypeIds.Operate.Value,
                order.orderId,
                "apparel-work-order-completed",
                1f,
                1,
                false));
        return 0;
    }

    private static bool Matches(
        ResearchFacilityCommandKind command,
        ApparelWorkOrderSaveData order) => command switch
        {
            ResearchFacilityCommandKind.HandLaundry =>
                order.kind == ApparelWorkOrderKind.Laundry && !order.powered,
            ResearchFacilityCommandKind.PoweredLaundry =>
                order.kind == ApparelWorkOrderKind.Laundry && order.powered,
            ResearchFacilityCommandKind.IndoorDrying =>
                order.kind == ApparelWorkOrderKind.Drying,
            ResearchFacilityCommandKind.ApparelRepair =>
                order.kind == ApparelWorkOrderKind.Repair,
            ResearchFacilityCommandKind.DressingChange =>
                order.kind == ApparelWorkOrderKind.Alteration
                && order.shortWardrobeOperation,
            ResearchFacilityCommandKind.ApparelTailoring =>
                order.kind is ApparelWorkOrderKind.Craft
                    or ApparelWorkOrderKind.Alteration,
            _ => false
        };

    private static void ApplyOperatorRecovery(
        IBuildingVisitorPort actor,
        ResearchFacilityCommandKind command,
        BuildableObject building)
    {
        if (actor == null)
        {
            return;
        }

        if (command is ResearchFacilityCommandKind.NurseryCare
            or ResearchFacilityCommandKind.GeriatricCare
            or ResearchFacilityCommandKind.ChronicCare
            or ResearchFacilityCommandKind.RetireeCare)
        {
            actor.ApplyNeedRecovery(new BuildingNeedRecoverySnapshot(
                sleep: 4f,
                mood: 3f,
                fun: 1f,
                hunger: 0f,
                excretion: 0f,
                hygiene: 2f,
                sourceId: $"research-facility:{command}",
                sourceName: DisplayName(building)));
        }
        else if (command is ResearchFacilityCommandKind.HouseholdRegistry
                 or ResearchFacilityCommandKind.GenerationArchive
                 or ResearchFacilityCommandKind.GuardianRegistry
                 or ResearchFacilityCommandKind.GeneticArchive)
        {
            actor.ApplyMoodFactor(
                $"research-facility:{command}",
                "정리된 기록으로 불확실성이 줄었다.",
                2f,
                GameCalendarRules.SecondsPerDay,
                1);
        }
    }

    private static string DisplayName(BuildableObject building) =>
        building?.BuildingData?.objectName
        ?? "연구 시설";
}
