using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;
using UnityEngine;

public sealed class DefenseThreatContext
{
    public DefenseThreatContext(
        InvasionSceneRuntimeReferences invasionRuntimes,
        IInvasionCampaignRuntime campaign,
        IFactionRuntime factions,
        IDefenseRaidAwarenessRuntime raidAwareness)
    {
        invasionRuntimes = invasionRuntimes
            ?? throw new ArgumentNullException(nameof(invasionRuntimes));
        Threat = invasionRuntimes.Threat
            ?? throw new InvalidOperationException(
                $"{nameof(DefenseThreatContext)} requires a loaded {nameof(InvasionThreatRuntime)}.");
        Director = invasionRuntimes.Director
            ?? throw new InvalidOperationException(
                $"{nameof(DefenseThreatContext)} requires a loaded {nameof(InvasionDirectorRuntime)}.");
        Reports = invasionRuntimes.CombatReport
            ?? throw new InvalidOperationException(
                $"{nameof(DefenseThreatContext)} requires a loaded {nameof(InvasionCombatReportRuntime)}.");
        Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
        Factions = factions ?? throw new ArgumentNullException(nameof(factions));
        RaidAwareness = raidAwareness
            ?? throw new ArgumentNullException(nameof(raidAwareness));
    }

    public InvasionThreatRuntime Threat { get; }
    public InvasionDirectorRuntime Director { get; }
    public InvasionCombatReportRuntime Reports { get; }
    public IInvasionCampaignRuntime Campaign { get; }
    public IFactionRuntime Factions { get; }
    public IDefenseRaidAwarenessRuntime RaidAwareness { get; }
}

public sealed class DefenseOperationsContext
{
    public DefenseOperationsContext(
        IDefenseEngagementRuntime engagementRuntime,
        IInvasionOwnerEvacuationService ownerEvacuation,
        IDefenseResponsePolicyRuntime policyRuntime,
        IStaffWorkforceQueryService workforceQuery)
    {
        EngagementRuntime = engagementRuntime
            ?? throw new ArgumentNullException(nameof(engagementRuntime));
        OwnerEvacuation = ownerEvacuation
            ?? throw new ArgumentNullException(nameof(ownerEvacuation));
        PolicyRuntime = policyRuntime
            ?? throw new ArgumentNullException(nameof(policyRuntime));
        WorkforceQuery = workforceQuery
            ?? throw new ArgumentNullException(nameof(workforceQuery));
    }

    public IDefenseEngagementRuntime EngagementRuntime { get; }
    public IInvasionOwnerEvacuationService OwnerEvacuation { get; }
    public IDefenseResponsePolicyRuntime PolicyRuntime { get; }
    public IStaffWorkforceQueryService WorkforceQuery { get; }
}

public sealed class DefenseFacilityContext
{
    public DefenseFacilityContext(
        IBuildingWorldQuery buildingWorld,
        IDefenseFacilityRuntime defenseFacilities,
        IDefenseFacilityNetworkRuntime facilityNetwork,
        IBuildingStructuralIntegrityRuntime structuralIntegrity)
    {
        BuildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        DefenseFacilities = defenseFacilities
            ?? throw new ArgumentNullException(nameof(defenseFacilities));
        FacilityNetwork = facilityNetwork
            ?? throw new ArgumentNullException(nameof(facilityNetwork));
        StructuralIntegrity = structuralIntegrity
            ?? throw new ArgumentNullException(nameof(structuralIntegrity));
    }

    public IBuildingWorldQuery BuildingWorld { get; }
    public IDefenseFacilityRuntime DefenseFacilities { get; }
    public IDefenseFacilityNetworkRuntime FacilityNetwork { get; }
    public IBuildingStructuralIntegrityRuntime StructuralIntegrity { get; }
}

public sealed class DefenseFeatureQueryService : IDefenseFeatureQueryService
{
    private const int MaxVisibleCards = 8;

    private readonly InvasionThreatRuntime threat;
    private readonly InvasionDirectorRuntime director;
    private readonly InvasionCombatReportRuntime reports;
    private readonly IDefenseEngagementRuntime engagementRuntime;
    private readonly IInvasionOwnerEvacuationService ownerEvacuation;
    private readonly IDefenseResponsePolicyRuntime policyRuntime;
    private readonly IStaffWorkforceQueryService workforceQuery;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IDefenseFacilityRuntime defenseFacilities;
    private readonly IInvasionCampaignRuntime campaign;
    private readonly IFactionRuntime factions;
    private readonly IDefenseRaidAwarenessRuntime raidAwareness;
    private readonly IDefenseFacilityNetworkRuntime facilityNetwork;
    private readonly IBuildingStructuralIntegrityRuntime structuralIntegrity;
    private readonly IDefenseUiTextQuery text;

    public DefenseFeatureQueryService(
        DefenseThreatContext threatContext,
        DefenseOperationsContext operations,
        DefenseFacilityContext facilities,
        IDefenseUiTextQuery text)
    {
        threatContext = threatContext
            ?? throw new ArgumentNullException(nameof(threatContext));
        operations = operations
            ?? throw new ArgumentNullException(nameof(operations));
        facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        threat = threatContext.Threat;
        director = threatContext.Director;
        reports = threatContext.Reports;
        campaign = threatContext.Campaign;
        factions = threatContext.Factions;
        raidAwareness = threatContext.RaidAwareness;
        engagementRuntime = operations.EngagementRuntime;
        ownerEvacuation = operations.OwnerEvacuation;
        policyRuntime = operations.PolicyRuntime;
        workforceQuery = operations.WorkforceQuery;
        buildingWorld = facilities.BuildingWorld;
        defenseFacilities = facilities.DefenseFacilities;
        facilityNetwork = facilities.FacilityNetwork;
        structuralIntegrity = facilities.StructuralIntegrity;
        this.text = text ?? throw new ArgumentNullException(nameof(text));
    }

    public DefenseFeatureSurfaceModel Capture(string selectedPolicyId)
    {
        IReadOnlyList<InvasionIntruderRuntime> intruders = director != null
            ? director.ActiveIntruders
            : Array.Empty<InvasionIntruderRuntime>();
        DefenseResponsePolicyData selected = ResolveSelectedPolicy(selectedPolicyId);
        return new DefenseFeatureSurfaceModel
        {
            DefenseHudSummary = CreateDefenseHudSummary(intruders),
            ThreatSummary = CreateThreatSummary(threat),
            ThreatFactors = threat != null
                ? text.Get("ThreatFactors", threat.LatestSnapshot.factors)
                : text.Get("ThreatUnavailable"),
            CampaignSummary = CreateCampaignSummary(),
            ReinforcementSummary = CreateReinforcementSummary(),
            OwnerEvacuationSummary = CreateOwnerEvacuationSummary(),
            Intruders = intruders
                .Take(MaxVisibleCards)
                .Select(CreateIntruderRow)
                .ToArray(),
            Policies = policyRuntime.Policies
                .Select((policy, index) => CreatePolicyRow(policy, index, selected?.id))
                .ToArray(),
            SelectedPolicy = selected != null
                ? CreatePolicyRow(selected, 0, selected.id)
                : null,
            Guards = CreateGuardRows(selected),
            Facilities = buildingWorld.Buildings
                .OfType<DefenseFacility>()
                .Where(facility =>
                    facility != null && !facility.isDestroy && facility.Defense != null)
                .Take(MaxVisibleCards)
                .Select((facility, index) => CreateFacilityRow(facility, index))
                .ToArray(),
            Reports = reports != null
                ? reports.ReportHistory
                    .Take(MaxVisibleCards)
                    .Select((report, index) => CreateReportRow(report, index))
                    .ToArray()
                : Array.Empty<DefenseFeatureReportRow>()
        };
    }

    private DefenseResponsePolicyData ResolveSelectedPolicy(string selectedPolicyId)
    {
        return policyRuntime.Policies.FirstOrDefault(policy =>
                   policy != null
                   && string.Equals(policy.id, selectedPolicyId, StringComparison.Ordinal))
            ?? policyRuntime.Policies.FirstOrDefault();
    }

    private DefenseFeatureIntruderRow CreateIntruderRow(
        InvasionIntruderRuntime intruder,
        int index)
    {
        CharacterActor actor = intruder != null ? intruder.IntruderActor : null;
        InvasionIntruderPatternDefinition pattern = intruder?.Pattern;
        DefenseEngagement engagement = null;
        bool hasEngagement = intruder != null
            && engagementRuntime.TryGetEngagement(intruder, out engagement);
        string front = hasEngagement
            ? text.Get(
                "IntruderFront",
                FormatEngagementState(engagement.State),
                GetCharacterName(engagement.LeadGuard),
                GetCharacterName(engagement.ReserveGuard),
                engagement.ExchangeCount)
            : CreateIntruderAdvanceSummary(intruder);
        string target = intruder?.CurrentPriorityTarget != null
            ? GetBuildingName(intruder.CurrentPriorityTarget)
            : text.Get("PrimaryTargetFallback");
        return new DefenseFeatureIntruderRow
        {
            Index = index,
            Title = text.Get(
                "IntruderTitle",
                pattern?.title ?? text.Get("UnknownIntruder"),
                actor != null ? actor.name : text.Get("IntruderFallback")),
            Detail = text.Get(
                "IntruderDetail",
                FormatIntruderState(intruder?.State ?? InvasionIntruderState.Finished),
                intruder?.Focus ?? 0f,
                target,
                front)
        };
    }

    private IReadOnlyList<DefenseFeatureGuardRow> CreateGuardRows(
        DefenseResponsePolicyData selected)
    {
        if (selected == null)
        {
            return Array.Empty<DefenseFeatureGuardRow>();
        }

        return workforceQuery.FindActiveWorkers()
            .Where(actor => actor != null && !actor.IsOwner)
            .Select((guard, index) =>
            {
                DefenseResponsePolicyData assigned = policyRuntime.GetPolicy(guard);
                CharacterWorkRoleUtility.TryGetWork(guard, out AbilityWork work);
                string duty = text.Get(
                    work != null && work.IsOffDuty ? "DutyOff" : "DutyOn");
                string priority = work != null
                    ? work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Guard).ToString()
                    : text.Get("None");
                return new DefenseFeatureGuardRow
                {
                    Index = index,
                    ActorRuntimeId = guard.GetInstanceID(),
                    Name = GetCharacterName(guard),
                    Detail = text.Get(
                        "GuardDetail",
                        assigned?.displayName ?? text.Get("PolicyStandard"),
                        duty,
                        priority),
                    UsesSelectedPolicy = string.Equals(
                        assigned?.id,
                        selected.id,
                        StringComparison.Ordinal)
                };
            })
            .ToArray();
    }

    private DefenseFeaturePolicyRow CreatePolicyRow(
        DefenseResponsePolicyData policy,
        int index,
        string selectedPolicyId)
    {
        return new DefenseFeaturePolicyRow
        {
            Index = index,
            PolicyId = policy.id,
            DisplayName = policy.displayName,
            Detail = text.Get(
                "PolicyDetail",
                policy.minimumDispatchHealthRatio,
                FormatRetreat(policy.retreatHealthRatio),
                policy.rejoinHealthRatio,
                text.Get(policy.holdWithoutReplacement
                    ? "PolicyHold"
                    : "PolicyRetreat")),
            IsSelected = string.Equals(policy.id, selectedPolicyId, StringComparison.Ordinal),
            IsCustom = policy.kind == DefenseResponsePolicyKind.Custom,
            AutoRespond = policy.autoRespond,
            MinimumDispatchHealthRatio = policy.minimumDispatchHealthRatio,
            RetreatHealthRatio = policy.retreatHealthRatio,
            HoldWithoutReplacement = policy.holdWithoutReplacement,
            RejoinHealthRatio = policy.rejoinHealthRatio
        };
    }

    private DefenseFeatureFacilityRow CreateFacilityRow(
        DefenseFacility facility,
        int index)
    {
        DefenseFacilitySnapshot snapshot =
            defenseFacilities.GetSnapshot(facility);
        DefenseFacilityNetworkSnapshot network =
            facilityNetwork.GetSnapshot(facility);
        int capacity = Mathf.Max(
            0,
            facility.Defense.supplyCapacity
                + (facility.Defense.growth?.capacityLevel ?? 0));
        string supply = facility.Defense.UsesPhysicalSupply
            ? $"{snapshot.Supply}/{capacity}"
            : facility.Defense.requiresPower
                ? text.Get(snapshot.Powered ? "PowerNormal" : "PowerOutage")
                : text.Get("SupplyNotRequired");
        return new DefenseFeatureFacilityRow
        {
            Index = index,
            RuntimeId = facility.GetInstanceID(),
            Name = GetBuildingName(facility),
            ArmingPolicy = snapshot.ArmingPolicy,
            OperationalState = snapshot.OperationalState,
            Detail = text.Get(
                    "FacilityDetail",
                    text.Get("AttackConcept." + facility.Defense.concept),
                    text.Get("ArmingPolicy." + snapshot.ArmingPolicy),
                    text.Get("OperationalState." + snapshot.OperationalState),
                    supply,
                    snapshot.Condition,
                    FormatLink(network.HasDetectionLink),
                    FormatLink(network.HasControlLink),
                    FormatLink(network.HasSupplyLink),
                    FormatLink(network.HasMaintenanceLink))
                + (string.IsNullOrWhiteSpace(snapshot.BlockedReason)
                    ? string.Empty
                    : text.Get(
                        "FacilityBlocked",
                        LocalizeBlockedReason(snapshot.BlockedReason)))
        };
    }

    private string CreateDefenseHudSummary(
        IReadOnlyList<InvasionIntruderRuntime> intruders)
    {
        if (intruders == null || intruders.Count == 0)
        {
            return text.Get("DefenseHudIdle");
        }

        InvasionIntruderRuntime intruder = intruders
            .FirstOrDefault(value =>
                value != null
                && value.State == InvasionIntruderState.Breaching)
            ?? intruders.FirstOrDefault(value => value != null);
        if (intruder == null)
        {
            return text.Get("IntruderStateUnavailable");
        }

        DefenseRaidAwarenessSnapshot awareness =
            raidAwareness.GetSnapshot(intruder.RaidId);
        string operation = FormatOperation(intruder.OperationKind);
        string identified = awareness.IdentificationStage switch
        {
            >= 3 => text.Get("Identification.Full"),
            2 => text.Get("Identification.Target"),
            1 => text.Get("Identification.Sign"),
            _ => text.Get("Identification.None")
        };
        string route = awareness.ExpectedPath.Count > 0
            ? text.Get("ExpectedRoute", awareness.ExpectedPath.Count)
            : text.Get("ExpectedRouteUnknown");
        string reason = string.IsNullOrWhiteSpace(
            awareness.RouteChangeReason)
            ? text.Get("RouteChangeNone")
            : awareness.RouteChangeReason;

        if (intruder.CurrentBreachTarget != null
            && structuralIntegrity.TryGet(
                intruder.CurrentBreachTarget,
                out BuildingStructuralIntegritySnapshot structure))
        {
            int attackers = Mathf.Max(1, intruder.BreachAttackerCount);
            float estimatedSeconds =
                structure.CurrentHitPoints
                / Mathf.Max(1f, attackers * 10f)
                * (intruder.IsEnragedBreach ? 0.65f : 1f);
            return text.Get(
                "DefenseHudBreach",
                operation,
                identified,
                GetBuildingName(intruder.CurrentBreachTarget),
                structure.CurrentHitPoints,
                structure.MaxHitPoints,
                attackers,
                estimatedSeconds,
                intruder.IsEnragedBreach
                    ? text.Get("EnragedBreachSuffix")
                    : string.Empty,
                route,
                reason);
        }

        string phase = intruder.State == InvasionIntruderState.Rallying
            ? text.Get(
                "RallyPhase",
                Mathf.CeilToInt(intruder.RallySecondsRemaining))
            : text.Get("EngagementPhase", FormatIntruderState(intruder.State));
        return text.Get(
            "DefenseHudActive",
            phase,
            operation,
            identified,
            route,
            awareness.KnownRisks.Count,
            reason);
    }

    private string FormatOperation(InvasionOperationKind kind)
    {
        return text.Get("Operation." + kind);
    }

    private string CreateCampaignSummary()
    {
        ScheduledInvasionOperationState operation =
            campaign.Operations.LastOrDefault();
        string operationText = operation != null
            ? text.Get(
                "CampaignOperation",
                text.Get("Operation." + operation.kind),
                operation.objectiveId,
                operation.intelligenceConfidence)
            : text.Get("CampaignOperationNone");
        string weakest = campaign.Branches
            .OrderBy(branch => branch.strength)
            .Select(branch =>
                text.Get(
                    "CampaignBranch",
                    branch.displayName,
                    branch.strength)
                + (string.IsNullOrWhiteSpace(branch.recoveryReason)
                    ? string.Empty
                    : text.Get("CampaignBranchRecovery", branch.recoveryReason)))
            .FirstOrDefault() ?? text.Get("CampaignBranchNone");
        return text.Get("CampaignSummary", operationText, weakest);
    }

    private string CreateReinforcementSummary()
    {
        FactionRouteState[] active = factions.Routes
            .Where(route => route != null
                && route.kind == FactionRouteKind.Reinforcement
                && route.status is FactionRouteStatus.Traveling
                    or FactionRouteStatus.Delayed
                    or FactionRouteStatus.Arrived)
            .ToArray();
        if (active.Length == 0)
        {
            return text.Get("ReinforcementNone");
        }

        return string.Join(
            "\n",
            active.Select(route =>
                text.Get(
                    "ReinforcementRoute",
                    route.factionId,
                    text.Get("FactionRouteStatus." + route.status),
                    route.estimatedArrivalDay,
                    route.strength)));
    }

    private DefenseFeatureReportRow CreateReportRow(
        InvasionCombatReportSnapshot report,
        int index)
    {
        string outcome = text.Get(
            report.Defended ? "ReportDefended" : "ReportFailed");
        return new DefenseFeatureReportRow
        {
            Index = index,
            Title = text.Get(
                "ReportTitle",
                outcome,
                report.ThreatSnapshot.threat),
            Summary = text.Get(
                "ReportSummary",
                report.ResidualRisk,
                report.DefenseContributions.Count,
                report.DamagedFacilities.Count),
            Detail = report.ToDetailText()
        };
    }

    private string CreateThreatSummary(InvasionThreatRuntime threat)
    {
        return threat != null
            ? text.Get(
                "ThreatSummary",
                threat.CurrentThreat,
                text.Get("ThreatStage." + threat.CurrentStage),
                threat.SafetyRemaining,
                text.Get(threat.IsCandidatePending
                    ? "ThreatForecastPending"
                    : "ThreatForecastNone"))
            : text.Get("ThreatInformationNone");
    }

    private string CreateOwnerEvacuationSummary()
    {
        if (!ownerEvacuation.IsEvacuating)
        {
            return text.Get("OwnerEvacuationIdle");
        }

        return text.Get(
                "OwnerEvacuationActive",
                ownerEvacuation.StatusText,
                ownerEvacuation.TargetCell)
            + (ownerEvacuation.HasReachedTarget
                ? text.Get("OwnerEvacuationCompleteSuffix")
                : string.Empty);
    }

    private string CreateIntruderAdvanceSummary(InvasionIntruderRuntime intruder)
    {
        if (intruder == null)
        {
            return text.Get("IntruderAdvanceNone");
        }

        if (intruder.State == InvasionIntruderState.Rallying)
        {
            return text.Get(
                "IntruderAdvanceRallying",
                Mathf.CeilToInt(intruder.RallySecondsRemaining));
        }

        return intruder.HasBreachedDungeonInterior
            ? text.Get("IntruderAdvanceInterior")
            : text.Get("IntruderAdvanceEntrance");
    }

    private string FormatIntruderState(InvasionIntruderState state)
    {
        return text.Get("IntruderState." + state);
    }

    private string FormatEngagementState(DefenseEngagementState state)
    {
        return text.Get("EngagementState." + state);
    }

    private string FormatRetreat(float ratio)
    {
        return ratio > 0f ? ratio.ToString("P0") : text.Get("None");
    }

    private string GetCharacterName(CharacterActor actor)
    {
        return actor != null
            ? actor.Identity?.DisplayName ?? actor.name
            : text.Get("None");
    }

    private string GetBuildingName(BuildableObject building)
    {
        return building != null
            ? building.BuildingData?.objectName ?? building.name
            : text.Get("BuildingFallback");
    }

    private string FormatLink(bool linked) =>
        text.Get(linked ? "LinkConnected" : "LinkDisconnected");

    private string LocalizeBlockedReason(string blockedReason)
    {
        if (!Enum.TryParse(
                blockedReason,
                ignoreCase: false,
                out FailureCode code)
            || code == FailureCode.None)
        {
            throw new InvalidOperationException(
                $"Unknown defense blocked-reason code '{blockedReason}'.");
        }

        return text.Get(new DomainFailure(code));
    }
}
