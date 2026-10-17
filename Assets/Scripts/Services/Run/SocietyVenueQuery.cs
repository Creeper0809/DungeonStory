using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Buildings;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class SocietyVenueCandidateSnapshot
{
    public BuildableObject Facility { get; internal set; }
    public string FacilityInstanceId { get; internal set; } = string.Empty;
    public string DisplayName { get; internal set; } = string.Empty;
    public Vector2Int AnchorCenter { get; internal set; }
    public Vector2Int AccessCell { get; internal set; }
    public RoomInstance Room { get; internal set; }
    public int PathCost { get; internal set; }
    public int SpareSeats { get; internal set; }
    public int SpareTables { get; internal set; }
    public int SpareServiceCapacity { get; internal set; }
    public int SpareVacantBeds { get; internal set; }
    public int GlobalResidentHeadroom { get; internal set; }
    public IReadOnlyList<Vector2Int> EventCells { get; internal set; } =
        Array.Empty<Vector2Int>();

    public string CapacitySummary =>
        $"좌석 {SpareSeats}, 탁자 {SpareTables}, 서비스 {SpareServiceCapacity}, "
        + $"빈 침상 {SpareVacantBeds}, 행사 칸 {EventCells.Count}";
}

public interface ISocietyVenueQuery
{
    bool TryFindDeliveryVenues(
        FacilityVenueRequirements requirements,
        Vector2Int supplyOrigin,
        string excludedClaimOwnerId,
        out IReadOnlyList<SocietyVenueCandidateSnapshot> candidates,
        out string failureReason);

    bool TryFindFestivalVenues(
        FacilityVenueRequirements requirements,
        IReadOnlyCollection<CharacterId> participantIds,
        int requiredEventCapacity,
        out IReadOnlyList<SocietyVenueCandidateSnapshot> candidates,
        out string failureReason);

    bool TryValidateDeliveryVenue(
        FacilityVenueRequirements requirements,
        Vector2Int supplyOrigin,
        string claimOwnerId,
        string facilityInstanceId,
        Vector2Int anchorCenter,
        out SocietyVenueCandidateSnapshot candidate,
        out string failureReason);

    bool TryValidateFestivalVenue(
        FacilityVenueRequirements requirements,
        IReadOnlyCollection<CharacterId> participantIds,
        int requiredEventCapacity,
        string facilityInstanceId,
        out SocietyVenueCandidateSnapshot candidate,
        out string failureReason);
}

public sealed class SocietyVenueQuery : ISocietyVenueQuery
{
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IRoomFacilityPolicy roomPolicy;
    private readonly IRoomEnvironmentQuery roomEnvironment;
    private readonly IGridSystemProvider grids;
    private readonly IGridTraversalAccessQuery traversal;
    private readonly IServiceSessionRuntime serviceSessions;
    private readonly ISettlementPopulationCapacityQuery populationCapacity;
    private readonly IHouseholdService households;
    private readonly ICharacterWorldQuery characters;
    private readonly ISocietyEventQuery society;
    private readonly ISocietyEventCatalog catalog;
    private readonly IWaterFixtureUseQuery wetUse;
    private readonly ISurgicalFacilityQuery surgery;

    public SocietyVenueQuery(
        IFacilityCapabilityQuery facilities,
        IBuildingWorldQuery buildingWorld,
        IRoomFacilityPolicy roomPolicy,
        IRoomEnvironmentQuery roomEnvironment,
        IGridSystemProvider grids,
        IGridTraversalAccessQuery traversal,
        IServiceSessionRuntime serviceSessions,
        ISettlementPopulationCapacityQuery populationCapacity,
        IHouseholdService households,
        ICharacterWorldQuery characters,
        ISocietyEventQuery society,
        ISocietyEventCatalog catalog,
        IWaterFixtureUseQuery wetUse,
        ISurgicalFacilityQuery surgery)
    {
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.roomPolicy = roomPolicy
            ?? throw new ArgumentNullException(nameof(roomPolicy));
        this.roomEnvironment = roomEnvironment
            ?? throw new ArgumentNullException(nameof(roomEnvironment));
        this.grids = grids ?? throw new ArgumentNullException(nameof(grids));
        this.traversal = traversal
            ?? throw new ArgumentNullException(nameof(traversal));
        this.serviceSessions = serviceSessions
            ?? throw new ArgumentNullException(nameof(serviceSessions));
        this.populationCapacity = populationCapacity
            ?? throw new ArgumentNullException(nameof(populationCapacity));
        this.households = households
            ?? throw new ArgumentNullException(nameof(households));
        this.characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
        this.society = society ?? throw new ArgumentNullException(nameof(society));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.wetUse = wetUse ?? throw new ArgumentNullException(nameof(wetUse));
        this.surgery = surgery ?? throw new ArgumentNullException(nameof(surgery));
    }

    public bool TryFindDeliveryVenues(
        FacilityVenueRequirements requirements,
        Vector2Int supplyOrigin,
        string excludedClaimOwnerId,
        out IReadOnlyList<SocietyVenueCandidateSnapshot> candidates,
        out string failureReason)
    {
        CharacterActor[] workers = characters.Characters
            .Where(IsLivingActiveCharacter)
            .Where(value => CharacterWorkRoleUtility.TryGetWork(value, out _))
            .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .ToArray();
        if (workers.Length == 0)
        {
            candidates = Array.Empty<SocietyVenueCandidateSnapshot>();
            failureReason = "납품 경로를 확인할 실제 정착지 운반자가 없습니다.";
            return false;
        }
        RouteSubject[] routeSubjects = workers.Select(value => new RouteSubject(
                CharacterPersistentIdentity.Require(value),
                supplyOrigin))
            .ToArray();
        return TryFind(
            requirements,
            routeSubjects,
            requireEverySubject: false,
            Math.Max(1, requirements?.minimumEventCells ?? 1),
            excludedClaimOwnerId,
            out candidates,
            out failureReason);
    }

    public bool TryFindFestivalVenues(
        FacilityVenueRequirements requirements,
        IReadOnlyCollection<CharacterId> participantIds,
        int requiredEventCapacity,
        out IReadOnlyList<SocietyVenueCandidateSnapshot> candidates,
        out string failureReason)
    {
        Dictionary<CharacterId, CharacterActor> byId = characters.Characters
            .Where(IsLivingActiveCharacter)
            .Where(value => CharacterPersistentIdentity.TryGet(value, out _))
            .ToDictionary(value => CharacterPersistentIdentity.Require(value));
        CharacterId[] requested = (participantIds ?? Array.Empty<CharacterId>())
            .Where(value => value.IsValid)
            .Distinct()
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
        if (requested.Length == 0
            || requested.Any(value => !byId.ContainsKey(value)))
        {
            candidates = Array.Empty<SocietyVenueCandidateSnapshot>();
            failureReason = "축제 장소에 접근할 실제 생존 참가자가 부족합니다.";
            return false;
        }
        RouteSubject[] routeSubjects = requested.Select(value =>
                new RouteSubject(value, byId[value].GetNowXY()))
            .ToArray();
        return TryFind(
            requirements,
            routeSubjects,
            requireEverySubject: true,
            Math.Max(requiredEventCapacity, requested.Length),
            string.Empty,
            out candidates,
            out failureReason);
    }

    public bool TryValidateDeliveryVenue(
        FacilityVenueRequirements requirements,
        Vector2Int supplyOrigin,
        string claimOwnerId,
        string facilityInstanceId,
        Vector2Int anchorCenter,
        out SocietyVenueCandidateSnapshot candidate,
        out string failureReason)
    {
        candidate = null;
        if (!TryFindDeliveryVenues(
                requirements,
                supplyOrigin,
                claimOwnerId,
                out IReadOnlyList<SocietyVenueCandidateSnapshot> candidates,
                out failureReason))
            return false;
        candidate = candidates.FirstOrDefault(value => string.Equals(
                value.FacilityInstanceId,
                facilityInstanceId,
                StringComparison.Ordinal)
            && value.AnchorCenter == anchorCenter);
        if (candidate != null)
            return true;
        failureReason = "배정 시설이 더 이상 같은 위치의 운영·접근 가능한 장소가 아닙니다.";
        return false;
    }

    public bool TryValidateFestivalVenue(
        FacilityVenueRequirements requirements,
        IReadOnlyCollection<CharacterId> participantIds,
        int requiredEventCapacity,
        string facilityInstanceId,
        out SocietyVenueCandidateSnapshot candidate,
        out string failureReason)
    {
        candidate = null;
        if (!TryFindFestivalVenues(
                requirements,
                participantIds,
                requiredEventCapacity,
                out IReadOnlyList<SocietyVenueCandidateSnapshot> candidates,
                out failureReason))
            return false;
        candidate = candidates.FirstOrDefault(value => string.Equals(
            value.FacilityInstanceId,
            facilityInstanceId,
            StringComparison.Ordinal));
        if (candidate != null)
            return true;
        failureReason = "확정한 축제 장소가 더 이상 참가자에게 유효하지 않습니다.";
        return false;
    }

    private bool TryFind(
        FacilityVenueRequirements requirements,
        IReadOnlyList<RouteSubject> routeSubjects,
        bool requireEverySubject,
        int requiredEventCapacity,
        string excludedClaimOwnerId,
        out IReadOnlyList<SocietyVenueCandidateSnapshot> candidates,
        out string failureReason)
    {
        candidates = Array.Empty<SocietyVenueCandidateSnapshot>();
        failureReason = string.Empty;
        if (requirements == null || requirements.Validate("runtime").Count > 0)
        {
            failureReason = "작성된 장소 조건이 유효하지 않습니다.";
            return false;
        }
        if (!grids.TryGetGrid(out Grid grid))
        {
            failureReason = "현재 던전 격자를 확인할 수 없습니다.";
            return false;
        }
        if (routeSubjects == null || routeSubjects.Count == 0
            || routeSubjects.Any(value => !grid.IsValidGridPos(value.Origin)))
        {
            failureReason = "장소 접근 경로의 실제 출발점을 확인할 수 없습니다.";
            return false;
        }

        BuildableObject[] operational = facilities
            .FindOperational(FacilityCapabilityKind.None)
            .Where(value => value != null
                && value.RequirePersistentInstanceId().IsValid
                && IsOperationalFixture(value))
            .ToArray();
        HashSet<BuildableObject> operationalSet = operational.ToHashSet();
        VenueClaims claims = CaptureClaims(excludedClaimOwnerId);
        SettlementPopulationCapacitySnapshot population = populationCapacity
            .CapturePopulationCapacity();
        int reachableSleepingSlots = CountReachableSleepingSlots(
            grid,
            operational,
            routeSubjects);
        int globalHeadroom = Math.Max(0, Math.Min(
                population.VacantSleepingSlotCount,
                Math.Max(0, reachableSleepingSlots - population.ResidentCount))
            - claims.ResidentHeadroom);
        List<SocietyVenueCandidateSnapshot> result = new();
        string lastFailure = "조건을 만족하는 운영 중인 장소가 없습니다.";
        foreach (BuildableObject facility in operational
                     .Where(value => requirements.anchor.Matches(
                         DefinitionId(value.BuildingData),
                         FacilityRoles(value)))
                     .OrderBy(value => value.PersistentInstanceId.Value,
                         StringComparer.Ordinal))
        {
            FacilityRoomOperationalProfile profile =
                roomPolicy.GetOperationalProfile(facility);
            if (profile?.IsUsableRoom != true
                || profile.Room == null
                || !roomEnvironment.TryGetSnapshot(
                    facility,
                    out RoomEnvironmentSnapshot environment)
                || !environment.IsEnvironmentActive)
            {
                lastFailure = "후보 시설이 닫히고 사용 가능한 실제 방에 속하지 않습니다.";
                continue;
            }
            if ((profile.Room.Roles & requirements.requiredRoomFacilityRoles)
                != requirements.requiredRoomFacilityRoles)
            {
                lastFailure = "후보 방에 필요한 시설 역할이 모두 없습니다.";
                continue;
            }
            if (!HasRequiredRoomFacilities(
                    profile,
                    operationalSet,
                    requirements.requiredRoomFacilities))
            {
                lastFailure = "후보 방에 함께 있어야 하는 운영 시설이 부족합니다.";
                continue;
            }
            if (requirements.requireEmergencySurgeryFacility
                && !profile.Parts.Any(value => value != null
                    && operationalSet.Contains(value)
                    && surgery.Evaluate(value, SurgeryFacilityTag.Emergency)
                        .IsAvailable))
            {
                lastFailure = "후보 방에 응급 수술이 가능한 주 수술대가 없습니다.";
                continue;
            }
            if (requirements.requireWetBath
                && (!facility.SupportsFacilityRole(FacilityRole.Hygiene)
                    || !wetUse.CanBeginWetUse(facility, out _)))
            {
                lastFailure = "후보 방의 목욕 시설에 깨끗한 물을 공급할 수 없습니다.";
                continue;
            }

            string facilityId = facility.PersistentInstanceId.Value;
            VenueClaim claim = GetRoomClaim(
                profile.Room,
                claims.ByFacility);
            int reservedVisits = profile.Parts
                .Where(value => value != null)
                .Distinct()
                .Sum(value => value.ActiveVisitReservationCount
                    + value.WaitingVisitReservationCount);
            int occupied = profile.Parts
                .Where(value => value != null)
                .Distinct()
                .Sum(value => value.CurrentUserCount
                    + value.ActiveVisitReservationCount
                    + value.WaitingVisitReservationCount);
            int seats = Math.Max(0, profile.SeatCapacity - occupied - claim.Seats);
            int tables = Math.Max(
                0,
                profile.TableCapacity - occupied - claim.Tables);
            int serviceCapacity = Math.Max(
                0,
                profile.ServiceCapacity - occupied - claim.ServiceCapacity);
            int beds = Math.Max(
                0,
                CountVacantBeds(
                    grid,
                    profile,
                    operationalSet,
                    routeSubjects,
                    requireEverySubject)
                - claim.VacantBeds);
            if (seats < requirements.minimumSeats
                || tables < requirements.minimumTables
                || serviceCapacity < requirements.minimumServiceCapacity
                || beds < requirements.minimumVacantBeds
                || requirements.requireResidentHeadroom
                    && globalHeadroom < requirements.minimumVacantBeds)
            {
                lastFailure = "후보 방의 실제 좌석·탁자·서비스·침상 여유가 부족합니다.";
                continue;
            }

            if (!TryChooseAccess(
                    grid,
                    facility,
                    routeSubjects,
                    requireEverySubject,
                    out Vector2Int accessCell,
                    out int pathCost))
            {
                lastFailure = "후보 시설에 도달할 수 있는 실제 접근 경로가 없습니다.";
                continue;
            }
            IReadOnlyList<Vector2Int> eventCells = CaptureEventCells(
                grid,
                profile,
                checked(claim.EventCells + reservedVisits),
                routeSubjects.Select(value => value.Origin).Append(accessCell));
            int eventDemand = Math.Max(
                requirements.minimumEventCells,
                requiredEventCapacity);
            if (eventCells.Count < eventDemand)
            {
                lastFailure = "후보 방에 점유·예약·통행을 제외한 행사 공간이 부족합니다.";
                continue;
            }
            result.Add(new SocietyVenueCandidateSnapshot
            {
                Facility = facility,
                FacilityInstanceId = facilityId,
                DisplayName = facility.BuildingData?.objectName ?? facilityId,
                AnchorCenter = facility.centerPos,
                AccessCell = accessCell,
                Room = profile.Room,
                PathCost = pathCost,
                SpareSeats = seats,
                SpareTables = tables,
                SpareServiceCapacity = serviceCapacity,
                SpareVacantBeds = beds,
                GlobalResidentHeadroom = globalHeadroom,
                EventCells = eventCells
            });
        }
        candidates = result
            .OrderBy(value => value.PathCost)
            .ThenBy(value => value.FacilityInstanceId, StringComparer.Ordinal)
            .ThenBy(value => value.AccessCell.x)
            .ThenBy(value => value.AccessCell.y)
            .ToArray();
        if (candidates.Count == 0)
        {
            failureReason = lastFailure;
            return false;
        }
        return true;
    }

    private bool TryChooseAccess(
        Grid grid,
        BuildableObject facility,
        IReadOnlyList<RouteSubject> subjects,
        bool requireEverySubject,
        out Vector2Int selected,
        out int selectedCost)
    {
        selected = default;
        selectedCost = int.MaxValue;
        foreach (Vector2Int accessCell in BuildingWorkAccessRules
                     .EnumerateCandidates(
                         facility.buildPoses,
                         facility.BuildingData?.IsGridMovement == true)
                     .Where(value => grid.IsValidGridPos(value)
                         && grid.IsWalkable(value))
                     .OrderBy(value => value.x)
                     .ThenBy(value => value.y))
        {
            int reachable = 0;
            int totalCost = requireEverySubject ? 0 : int.MaxValue;
            foreach (RouteSubject subject in subjects)
            {
                GridTraversalContext context = GridTraversalContext.ForCharacter(
                    subject.CharacterId);
                GridPathSearchResult path = grid.SearchPathTo(
                    subject.Origin,
                    accessCell,
                    position => traversal.CanTraverse(
                        grid,
                        position,
                        context,
                        out _),
                    DefaultGridTraversalCostPolicy.Instance,
                    context);
                int cost = path.GetMoveCostTo(accessCell);
                if (cost == int.MaxValue)
                {
                    if (requireEverySubject)
                    {
                        reachable = -1;
                        break;
                    }
                    continue;
                }
                reachable++;
                totalCost = requireEverySubject
                    ? checked(totalCost + cost)
                    : Math.Min(totalCost, cost);
            }
            if (reachable <= 0
                || requireEverySubject && reachable != subjects.Count
                || totalCost >= selectedCost)
                continue;
            selected = accessCell;
            selectedCost = totalCost;
        }
        return selectedCost != int.MaxValue;
    }

    private int CountVacantBeds(
        Grid grid,
        FacilityRoomOperationalProfile profile,
        ISet<BuildableObject> operational,
        IReadOnlyList<RouteSubject> routeSubjects,
        bool requireEverySubject)
    {
        int total = 0;
        foreach (BuildableObject bed in profile.Parts
                     .Where(value => value != null
                         && operational.Contains(value)
                         && value.GetServiceHubAbility()?.serviceCategory
                             == ServiceCategory.Lodging)
                     .Distinct())
        {
            ServiceHubSnapshot hub = serviceSessions.GetHubSnapshot(bed);
            if ((hub.State is ServiceOperatingState.Closed
                    or ServiceOperatingState.Suspended)
                || !TryChooseAccess(
                    grid,
                    bed,
                    routeSubjects,
                    requireEverySubject,
                    out _,
                    out _))
                continue;
            int assigned = characters.Characters
                .Where(value => CharacterPersistentIdentity.TryGet(value, out _))
                .Count(value => households.TryGet(
                        CharacterPersistentIdentity.Require(value),
                        out CharacterRoomAssignmentSaveData assignment)
                    && string.Equals(
                        assignment.bedBuildingId,
                        bed.PersistentInstanceId.Value,
                        StringComparison.Ordinal));
            total += Math.Max(
                0,
                hub.Capacity
                    - Math.Max(assigned, hub.ActiveSessions)
                    - bed.ActiveVisitReservationCount
                    - bed.WaitingVisitReservationCount);
        }
        return total;
    }

    private int CountReachableSleepingSlots(
        Grid grid,
        IReadOnlyList<BuildableObject> operational,
        IReadOnlyList<RouteSubject> routeSubjects)
    {
        int total = 0;
        foreach (BuildableObject bed in operational
                     .Where(value => value.GetServiceHubAbility()?.serviceCategory
                         == ServiceCategory.Lodging)
                     .Distinct())
        {
            ServiceHubSnapshot hub = serviceSessions.GetHubSnapshot(bed);
            if ((hub.State is ServiceOperatingState.Closed
                    or ServiceOperatingState.Suspended)
                || !TryChooseAccess(
                    grid,
                    bed,
                    routeSubjects,
                    requireEverySubject: false,
                    out _,
                    out _))
                continue;
            total = checked(total + hub.Capacity);
        }
        return total;
    }

    private bool IsOperationalFixture(BuildableObject facility)
    {
        if (facility == null
            || facility.IsBuildingDestroyed
            || facility.IsGridDestroyed
            || facility.BuildingData == null
            || facility.IsDamaged
                && facility.Facility?.disabledWhenDamaged == true)
            return false;
        if (facility.Facility?.IsVisitorFacility == true
            && !facility.CanQueueVisit(null, out _))
            return false;
        if (facility.GetServiceHubAbility() == null)
            return true;
        ServiceOperatingState state = serviceSessions
            .GetHubSnapshot(facility)
            .State;
        return state is ServiceOperatingState.Direct
            or ServiceOperatingState.Managed
            or ServiceOperatingState.Automated;
    }

    private static bool HasRequiredRoomFacilities(
        FacilityRoomOperationalProfile profile,
        ISet<BuildableObject> operational,
        IReadOnlyList<FacilityVenueAnchorSelector> required)
    {
        if (required == null)
            return false;
        return required.All(selector => profile.Parts.Any(part => part != null
            && operational.Contains(part)
            && selector.Matches(
                DefinitionId(part.BuildingData),
                FacilityRoles(part))));
    }

    private IReadOnlyList<Vector2Int> CaptureEventCells(
        Grid grid,
        FacilityRoomOperationalProfile profile,
        int claimedCells,
        IEnumerable<Vector2Int> excludedOrigins)
    {
        HashSet<Vector2Int> protectedCells = new();
        protectedCells.UnionWith(excludedOrigins ?? Array.Empty<Vector2Int>());

        // Side-view rooms are horizontal and character occupancy is not Grid
        // traversal topology. Reserve explicit landings and one usable stand per
        // operating fixture instead of treating every floor tile as a corridor
        // articulation point.
        foreach (BuildableObject part in profile.Parts
                     .Where(value => value?.BuildingData != null)
                     .Distinct())
        {
            Vector2Int[] legalAccess = BuildingWorkAccessRules
                .EnumerateCandidates(
                    part.buildPoses,
                    part.BuildingData.IsGridMovement)
                .Where(value => profile.Room.ContainsCell(value)
                    && grid.IsValidGridPos(value)
                    && grid.IsWalkable(value))
                .Distinct()
                .OrderBy(value => value.x)
                .ThenBy(value => value.y)
                .ToArray();
            if (part.BuildingData.IsDoor
                || part.BuildingData.runtimeArchetype
                    == BuildingRuntimeArchetypeKind.Stair)
            {
                protectedCells.UnionWith(part.buildPoses);
                protectedCells.UnionWith(legalAccess);
                continue;
            }

            FacilityUseClassification use =
                part.BuildingData.EffectiveUseClassification;
            if (IsOperationalFixture(part)
                && use is not FacilityUseClassification.Structure
                and not FacilityUseClassification.Decoration
                && legalAccess.Length > 0
                && !legalAccess.Any(protectedCells.Contains))
            {
                protectedCells.Add(legalAccess[0]);
            }
        }

        foreach (BuildableObject door in profile.Room.Doors
                     .Where(value => value != null))
        {
            protectedCells.UnionWith(door.buildPoses);
            protectedCells.UnionWith(BuildingWorkAccessRules
                .EnumerateCandidates(
                    door.buildPoses,
                    door.BuildingData?.IsGridMovement == true)
                .Where(value => profile.Room.ContainsCell(value)
                    && grid.IsValidGridPos(value)
                    && grid.IsWalkable(value)));
        }

        Vector2Int[] available = profile.Room.Cells
            .Where(value => IsVacantEventCell(grid, value)
                && !protectedCells.Contains(value))
            .OrderBy(value => value.x)
            .ThenBy(value => value.y)
            .Skip(Math.Max(0, claimedCells))
            .ToArray();
        return Array.AsReadOnly(available);
    }

    private static bool IsVacantEventCell(Grid grid, Vector2Int position)
    {
        GridCell cell = grid.GetGridCell(position);
        return cell != null
            && cell.AreaType == GridCellAreaType.DungeonInterior
            && grid.IsWalkable(position)
            && !cell.HasOccupantInLayer(GridLayer.Building)
            && !cell.HasOccupantInLayer(GridLayer.WallFixture)
            && !cell.HasOccupantInLayer(GridLayer.Character)
            && !cell.HasOccupantInLayer(GridLayer.DownedCharacter)
            && !cell.HasOccupantInLayer(GridLayer.Wildlife)
            && !cell.HasOccupantInLayer(GridLayer.Item)
            && !cell.HasOccupantInLayer(GridLayer.Construction)
            && !cell.HasOccupantInLayer(GridLayer.Conveyor)
            && !cell.HasOccupantInLayer(GridLayer.Utility)
            && !cell.HasOccupantInLayer(GridLayer.Filth);
    }

    private VenueClaims CaptureClaims(string excludedOwnerId)
    {
        Dictionary<string, VenueClaim> byFacility = new(StringComparer.Ordinal);
        int residentHeadroom = 0;
        foreach (V20ActiveEventSaveData active in society.ActiveSocietyEvents
                     .Where(value => value != null
                         && !string.Equals(
                             value.instanceId,
                             excludedOwnerId,
                             StringComparison.Ordinal)
                         && !string.IsNullOrEmpty(
                             value.guestDelivery?.venueFacilityInstanceId)
                         && value.guestDelivery.inputOwnerActive
                         && value.guestDelivery.phase
                             != GuestRequestDeliveryPhase.None))
        {
            GuestRequestDefinitionSO definition = catalog.GuestRequests
                .SingleOrDefault(value => value != null && string.Equals(
                    value.StableId,
                    active.definitionId,
                    StringComparison.Ordinal));
            FacilityVenueRequirements requirement = definition?.venueRequirements;
            if (requirement == null)
                continue;
            string facilityId = active.guestDelivery.venueFacilityInstanceId;
            VenueClaim current = byFacility.TryGetValue(
                facilityId,
                out VenueClaim claim)
                ? claim
                : VenueClaim.Empty;
            byFacility[facilityId] = current.Add(requirement);
            if (requirement.requireResidentHeadroom)
                residentHeadroom = checked(
                    residentHeadroom + requirement.minimumVacantBeds);
        }
        return new VenueClaims(byFacility, residentHeadroom);
    }

    private VenueClaim GetRoomClaim(
        RoomInstance room,
        IReadOnlyDictionary<string, VenueClaim> claims)
    {
        VenueClaim total = VenueClaim.Empty;
        foreach (KeyValuePair<string, VenueClaim> claim in claims)
        {
            BuildableObject claimedFacility = buildingWorld.Buildings
                .FirstOrDefault(value => value != null && string.Equals(
                    value.PersistentInstanceId.Value,
                    claim.Key,
                    StringComparison.Ordinal));
            if (claimedFacility == null
                || !ReferenceEquals(
                    roomPolicy.GetOperationalProfile(claimedFacility)?.Room,
                    room))
                continue;
            total = total.Add(claim.Value);
        }
        return total;
    }

    private static FacilityRole FacilityRoles(BuildableObject building)
    {
        FacilityRole result = FacilityRole.None;
        foreach (FacilityRole role in Enum.GetValues(typeof(FacilityRole)))
        {
            if (role != FacilityRole.None && building.SupportsFacilityRole(role))
                result |= role;
        }
        return result;
    }

    private static string DefinitionId(BuildingSO building) =>
        building == null
            ? string.Empty
            : BuildingDefinitionIdentity.Resolve(building);

    private static bool IsLivingActiveCharacter(CharacterActor actor) =>
        actor != null
        && actor.Identity != null
        && !actor.IsDead
        && actor.CurrentLifecycleState == CharacterLifecycleState.Active;

    private readonly struct RouteSubject
    {
        public RouteSubject(CharacterId characterId, Vector2Int origin)
        {
            CharacterId = characterId;
            Origin = origin;
        }

        public CharacterId CharacterId { get; }
        public Vector2Int Origin { get; }
    }

    private readonly struct VenueClaims
    {
        public VenueClaims(
            IReadOnlyDictionary<string, VenueClaim> byFacility,
            int residentHeadroom)
        {
            ByFacility = byFacility;
            ResidentHeadroom = residentHeadroom;
        }

        public IReadOnlyDictionary<string, VenueClaim> ByFacility { get; }
        public int ResidentHeadroom { get; }
    }

    private readonly struct VenueClaim
    {
        public static readonly VenueClaim Empty = new(0, 0, 0, 0, 0);

        private VenueClaim(
            int seats,
            int tables,
            int serviceCapacity,
            int vacantBeds,
            int eventCells)
        {
            Seats = seats;
            Tables = tables;
            ServiceCapacity = serviceCapacity;
            VacantBeds = vacantBeds;
            EventCells = eventCells;
        }

        public int Seats { get; }
        public int Tables { get; }
        public int ServiceCapacity { get; }
        public int VacantBeds { get; }
        public int EventCells { get; }

        public VenueClaim Add(FacilityVenueRequirements value) => new(
            checked(Seats + value.minimumSeats),
            checked(Tables + value.minimumTables),
            checked(ServiceCapacity + value.minimumServiceCapacity),
            checked(VacantBeds + value.minimumVacantBeds),
            checked(EventCells + value.minimumEventCells));

        public VenueClaim Add(VenueClaim value) => new(
            checked(Seats + value.Seats),
            checked(Tables + value.Tables),
            checked(ServiceCapacity + value.ServiceCapacity),
            checked(VacantBeds + value.VacantBeds),
            checked(EventCells + value.EventCells));
    }
}
