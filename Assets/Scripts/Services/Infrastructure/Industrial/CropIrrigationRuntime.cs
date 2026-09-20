using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public enum CropIrrigationStatus
{
    Available = 0,
    NotRequired = 1,
    ManualRefillActive = 2,
    CapacityUnavailable = 3,
    NoOperationalIrrigator = 4,
    OutOfRange = 5,
    RouteBlocked = 6,
    NetworkUnavailable = 7,
    WaterUnavailable = 8,
    RateLimited = 9,
    InvalidRequest = 10
}

public readonly struct CropIrrigationRequest
{
    public CropIrrigationRequest(
        BuildableObject plot,
        bool needsWater,
        float currentWater,
        float waterCapacity,
        bool hasManualWaterRefillOwner)
    {
        Plot = plot;
        NeedsWater = needsWater;
        CurrentWater = currentWater;
        WaterCapacity = waterCapacity;
        HasManualWaterRefillOwner = hasManualWaterRefillOwner;
    }

    public BuildableObject Plot { get; }
    public bool NeedsWater { get; }
    public float CurrentWater { get; }
    public float WaterCapacity { get; }
    public bool HasManualWaterRefillOwner { get; }
}

public readonly struct CropIrrigationAssessment
{
    internal CropIrrigationAssessment(
        CropIrrigationStatus status,
        BuildingInstanceId plotId,
        BuildingInstanceId irrigatorId,
        float waterUnits,
        WorldWaterQuality minimumQuality,
        string reason)
    {
        Status = status;
        PlotId = plotId;
        IrrigatorId = irrigatorId;
        WaterUnits = waterUnits;
        MinimumQuality = minimumQuality;
        Reason = reason ?? string.Empty;
    }

    public CropIrrigationStatus Status { get; }
    public BuildingInstanceId PlotId { get; }
    public BuildingInstanceId IrrigatorId { get; }
    public float WaterUnits { get; }
    public WorldWaterQuality MinimumQuality { get; }
    public string Reason { get; }
    public bool CanSupply => Status == CropIrrigationStatus.Available;
}

public readonly struct CropIrrigationSupplyResult
{
    internal CropIrrigationSupplyResult(
        CropIrrigationAssessment assessment,
        bool succeeded,
        float suppliedWaterUnits,
        WorldWaterQuality consumedQuality)
    {
        Assessment = assessment;
        Succeeded = succeeded;
        SuppliedWaterUnits = suppliedWaterUnits;
        ConsumedQuality = consumedQuality;
    }

    public CropIrrigationAssessment Assessment { get; }
    public bool Succeeded { get; }
    public float SuppliedWaterUnits { get; }
    public WorldWaterQuality ConsumedQuality { get; }
}

public interface ICropIrrigationRuntime
{
    int Version { get; }
    CropIrrigationAssessment Assess(CropIrrigationRequest request);
    CropIrrigationSupplyResult TrySupply(CropIrrigationRequest request);
}

public sealed class PreparedCropIrrigationSupply
{
    internal PreparedCropIrrigationSupply(
        CropIrrigationSupplyResult result,
        BuildableObject irrigator,
        float suppliedAt,
        float minimumRefillIntervalSeconds,
        FluidInfrastructureMutationToken fluidMutation)
    {
        Result = result;
        Irrigator = irrigator;
        SuppliedAt = suppliedAt;
        MinimumRefillIntervalSeconds = minimumRefillIntervalSeconds;
        FluidMutation = fluidMutation;
        IsPending = true;
    }

    public CropIrrigationSupplyResult Result { get; }
    public string PlotDisplayName => FacilityShopService.GetBuildingName(
        Result.Assessment.PlotId.IsValid ? Plot?.BuildingData : null);
    public string IrrigatorDisplayName => FacilityShopService.GetBuildingName(
        Irrigator?.BuildingData);
    internal BuildableObject Plot { get; set; }
    internal BuildableObject Irrigator { get; }
    internal float SuppliedAt { get; }
    internal float MinimumRefillIntervalSeconds { get; }
    internal FluidInfrastructureMutationToken FluidMutation { get; }
    internal bool IsPending { get; set; }
}

public interface ICropIrrigationSupplyTransaction
{
    bool TryPrepareSupply(
        CropIrrigationRequest request,
        out PreparedCropIrrigationSupply prepared);
    void CommitPreparedSupply(PreparedCropIrrigationSupply prepared);
    void RollbackPreparedSupply(PreparedCropIrrigationSupply prepared);
}

public sealed class CropIrrigationRuntime :
    ICropIrrigationRuntime,
    ICropIrrigationSupplyTransaction
{
    private const float Epsilon = 0.0001f;
    private const float ApprovedWaterUnits = 1f;
    private const WorldWaterQuality RequiredQuality = WorldWaterQuality.Clean;

    private readonly IFacilityCapabilityQuery facilities;
    private readonly IBlueprintResearchStateService research;
    private readonly IFluidInfrastructureQuery fluidQuery;
    private readonly IFluidInfrastructureTransaction fluid;
    private readonly IFluidInfrastructureMutationTransaction fluidMutations;
    private readonly IGameClock clock;
    private readonly Dictionary<string, SupplyStamp> irrigatorSupplies =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, SupplyStamp> plotSupplies =
        new(StringComparer.Ordinal);

    public CropIrrigationRuntime(
        IFacilityCapabilityQuery facilities,
        IBlueprintResearchStateService research,
        IFluidInfrastructureQuery fluidQuery,
        IFluidInfrastructureTransaction fluid,
        IFluidInfrastructureMutationTransaction fluidMutations,
        IGameClock clock)
    {
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        this.research = research
            ?? throw new ArgumentNullException(nameof(research));
        this.fluidQuery = fluidQuery
            ?? throw new ArgumentNullException(nameof(fluidQuery));
        this.fluid = fluid
            ?? throw new ArgumentNullException(nameof(fluid));
        this.fluidMutations = fluidMutations
            ?? throw new ArgumentNullException(nameof(fluidMutations));
        this.clock = clock
            ?? throw new ArgumentNullException(nameof(clock));
    }

#if UNITY_EDITOR
    public CropIrrigationRuntime(
        IFacilityCapabilityQuery facilities,
        IBlueprintResearchStateService research,
        IFluidInfrastructureQuery fluidQuery,
        IFluidInfrastructureTransaction fluid,
        IGameClock clock)
        : this(
            facilities,
            research,
            fluidQuery,
            fluid,
            fluid as IFluidInfrastructureMutationTransaction
                ?? throw new InvalidOperationException(
                    "Crop irrigation fixtures require the exact fluid mutation transaction."),
            clock)
    {
    }
#endif

    public int Version => fluidQuery.Version;

    public CropIrrigationAssessment Assess(CropIrrigationRequest request) =>
        Evaluate(request).Assessment;

    public CropIrrigationSupplyResult TrySupply(CropIrrigationRequest request)
        => throw new InvalidOperationException(
            "Direct crop irrigation supply is disabled; the plot owner must use the joint irrigation supply transaction.");

    public bool TryPrepareSupply(
        CropIrrigationRequest request,
        out PreparedCropIrrigationSupply prepared)
    {
        Evaluation evaluation = Evaluate(request);
        if (!evaluation.Assessment.CanSupply)
        {
            prepared = null;
            return false;
        }

        FluidInfrastructureMutationToken fluidBefore =
            fluidMutations.CaptureMutation();
        if (!fluid.TryConsume(
                evaluation.Irrigator,
                RequiredQuality,
                evaluation.Assessment.WaterUnits,
                out WorldWaterQuality consumedQuality,
                out DomainFailure failure))
        {
            prepared = new PreparedCropIrrigationSupply(
                new CropIrrigationSupplyResult(
                    CreateFluidFailure(
                        evaluation.Assessment.PlotId,
                        evaluation.Assessment.IrrigatorId,
                        evaluation.Assessment.WaterUnits,
                        failure),
                    false,
                    0f,
                    RequiredQuality),
                evaluation.Irrigator,
                clock.Time,
                evaluation.Ability.minimumRefillIntervalSeconds,
                default)
            {
                Plot = request.Plot,
                IsPending = false
            };
            return false;
        }

        prepared = new PreparedCropIrrigationSupply(
            new CropIrrigationSupplyResult(
                evaluation.Assessment,
                true,
                evaluation.Assessment.WaterUnits,
                consumedQuality),
            evaluation.Irrigator,
            clock.Time,
            evaluation.Ability.minimumRefillIntervalSeconds,
            fluidBefore)
        {
            Plot = request.Plot
        };
        return true;
    }

    public void CommitPreparedSupply(PreparedCropIrrigationSupply prepared)
    {
        RequirePending(prepared);
        SupplyStamp stamp = new(
            prepared.Irrigator,
            prepared.SuppliedAt,
            prepared.MinimumRefillIntervalSeconds);
        irrigatorSupplies[prepared.Result.Assessment.IrrigatorId.Value] = stamp;
        plotSupplies[prepared.Result.Assessment.PlotId.Value] = new SupplyStamp(
            prepared.Plot,
            prepared.SuppliedAt,
            prepared.MinimumRefillIntervalSeconds);
        prepared.IsPending = false;
    }

    public void RollbackPreparedSupply(PreparedCropIrrigationSupply prepared)
    {
        RequirePending(prepared);
        fluidMutations.RestoreMutation(prepared.FluidMutation);
        prepared.IsPending = false;
    }

    private static void RequirePending(PreparedCropIrrigationSupply prepared)
    {
        if (prepared == null || !prepared.IsPending
            || !prepared.Result.Succeeded
            || !prepared.FluidMutation.IsValid)
            throw new InvalidOperationException(
                "A pending exact crop irrigation supply is required.");
    }

    private Evaluation Evaluate(CropIrrigationRequest request)
    {
        BuildingInstanceId plotId = request.Plot?.PersistentInstanceId
            ?? default;
        if (request.Plot == null
            || !plotId.IsValid
            || request.Plot.IsBuildingDestroyed
            || request.Plot.BuildingData == null
            || request.Plot.Grid == null
            || !IsFiniteNonNegative(request.CurrentWater)
            || !IsFiniteNonNegative(request.WaterCapacity)
            || request.CurrentWater > request.WaterCapacity)
        {
            return Evaluation.Rejected(CreateAssessment(
                CropIrrigationStatus.InvalidRequest,
                plotId,
                default,
                0f,
                "관개 요청의 농지 또는 수분 값이 유효하지 않습니다."));
        }

        if (!request.NeedsWater)
        {
            return Evaluation.Rejected(CreateAssessment(
                CropIrrigationStatus.NotRequired,
                plotId,
                default,
                0f,
                "현재 작물은 물 보충이 필요하지 않습니다."));
        }
        if (request.HasManualWaterRefillOwner)
        {
            return Evaluation.Rejected(CreateAssessment(
                CropIrrigationStatus.ManualRefillActive,
                plotId,
                default,
                0f,
                "직원 물리 급수 작업이 진행 중입니다."));
        }
        if (request.CurrentWater + ApprovedWaterUnits
            > request.WaterCapacity)
        {
            return Evaluation.Rejected(CreateAssessment(
                CropIrrigationStatus.CapacityUnavailable,
                plotId,
                default,
                ApprovedWaterUnits,
                "농지에 관개 1회분을 받을 빈 수분 용량이 없습니다."));
        }

        float now = clock.Time;
        bool foundOperational = false;
        bool foundInRange = false;
        bool foundRoute = false;
        bool plotRateLimited = IsCoolingDown(
            plotSupplies,
            plotId.Value,
            request.Plot,
            now);
        bool foundRateLimited = plotRateLimited;
        bool foundNetwork = false;
        bool foundWaterShortage = false;
        BlueprintResearchState unlocks = research.GetState()
            ?? throw new InvalidOperationException(
                $"{nameof(CropIrrigationRuntime)} requires current blueprint research state.");
        HashSet<string> seenIds = new(StringComparer.Ordinal);

        IReadOnlyList<BuildableObject> candidates = facilities
            .FindOperational(FacilityCapabilityKind.None);
        for (int index = 0; index < candidates.Count; index++)
        {
            BuildableObject candidate = candidates[index];
            BuildingCropIrrigationAbility ability = candidate?.BuildingData
                ?.GetAbility<BuildingCropIrrigationAbility>();
            if (ability == null
                || candidate.Grid == null
                || !ReferenceEquals(candidate.Grid, request.Plot.Grid)
                || !IsUnlocked(candidate.BuildingData, unlocks))
            {
                continue;
            }

            BuildingInstanceId irrigatorId = candidate.PersistentInstanceId;
            if (!irrigatorId.IsValid)
            {
                throw new InvalidOperationException(
                    "An operational crop irrigator has no persistent building ID.");
            }
            if (!seenIds.Add(irrigatorId.Value))
            {
                throw new InvalidOperationException(
                    $"Duplicate operational crop irrigator ID '{irrigatorId.Value}'.");
            }
            ValidateAbility(candidate, ability);
            foundOperational = true;

            if (!IsInRange(candidate, request.Plot, ability.rangeManhattan))
            {
                continue;
            }
            foundInRange = true;
            if (!HasRoute(candidate, request.Plot))
            {
                continue;
            }
            foundRoute = true;

            bool irrigatorRateLimited = IsCoolingDown(
                    irrigatorSupplies,
                    irrigatorId.Value,
                    candidate,
                    now);
            if (plotRateLimited || irrigatorRateLimited)
            {
                foundRateLimited = true;
                continue;
            }

            if (!fluid.CanConsume(
                    candidate,
                    RequiredQuality,
                    ability.waterPerRefill,
                    out DomainFailure failure))
            {
                if (failure.Code == FailureCode.FluidInsufficientWater)
                {
                    foundNetwork = true;
                    foundWaterShortage = true;
                }
                continue;
            }

            foundNetwork = true;
            return new Evaluation(
                CreateAssessment(
                    CropIrrigationStatus.Available,
                    plotId,
                    irrigatorId,
                    ability.waterPerRefill,
                    "연결된 관개 시설에서 깨끗한 물 1을 공급할 수 있습니다."),
                candidate,
                ability);
        }

        CropIrrigationStatus status;
        string reason;
        if (!foundOperational)
        {
            status = CropIrrigationStatus.NoOperationalIrrigator;
            reason = "작동 중이며 해금된 관개 시설이 없습니다.";
        }
        else if (!foundInRange)
        {
            status = CropIrrigationStatus.OutOfRange;
            reason = "농지가 관개 시설의 맨해튼 반경 밖에 있습니다.";
        }
        else if (!foundRoute)
        {
            status = CropIrrigationStatus.RouteBlocked;
            reason = "관개 시설과 농지 사이의 정상 접근 경로가 막혀 있습니다.";
        }
        else if (foundRateLimited)
        {
            status = CropIrrigationStatus.RateLimited;
            reason = "관개 시설 또는 농지의 다음 공급 시간이 아직 되지 않았습니다.";
        }
        else if (foundWaterShortage)
        {
            status = CropIrrigationStatus.WaterUnavailable;
            reason = "연결된 배관망에 깨끗한 물이 부족합니다.";
        }
        else
        {
            status = CropIrrigationStatus.NetworkUnavailable;
            reason = foundNetwork
                ? "연결된 관개 시설에서 물을 공급할 수 없습니다."
                : "관개 시설이 깨끗한 물 배관망에 연결되지 않았습니다.";
        }
        return Evaluation.Rejected(CreateAssessment(
            status,
            plotId,
            default,
            0f,
            reason));
    }

    private static CropIrrigationAssessment CreateFluidFailure(
        BuildingInstanceId plotId,
        BuildingInstanceId irrigatorId,
        float waterUnits,
        DomainFailure failure)
    {
        CropIrrigationStatus status =
            failure.Code == FailureCode.FluidInsufficientWater
                ? CropIrrigationStatus.WaterUnavailable
                : CropIrrigationStatus.NetworkUnavailable;
        return CreateAssessment(
            status,
            plotId,
            irrigatorId,
            waterUnits,
            status == CropIrrigationStatus.WaterUnavailable
                ? "공급 직전 배관망의 깨끗한 물이 부족해졌습니다."
                : "공급 직전 관개 시설의 배관 연결을 사용할 수 없게 되었습니다.");
    }

    private static CropIrrigationAssessment CreateAssessment(
        CropIrrigationStatus status,
        BuildingInstanceId plotId,
        BuildingInstanceId irrigatorId,
        float waterUnits,
        string reason) =>
        new(
            status,
            plotId,
            irrigatorId,
            waterUnits,
            RequiredQuality,
            reason);

    private static bool IsFiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    private static bool IsUnlocked(
        BuildingSO definition,
        BlueprintResearchState unlocks) =>
        definition != null
        && (definition.unlocked || unlocks.IsBuildingUnlocked(definition.id));

    private static void ValidateAbility(
        BuildableObject irrigator,
        BuildingCropIrrigationAbility ability)
    {
        BuildingUtilityConnectionAbility utility = irrigator.BuildingData
            .GetAbility<BuildingUtilityConnectionAbility>();
        if (ability.rangeManhattan < 1
            || !IsFinitePositive(ability.waterPerRefill)
            || !IsFinitePositive(ability.minimumRefillIntervalSeconds)
            || ability.minimumRefillIntervalSeconds + Epsilon < 1f
            || ability.waterPerRefill != ApprovedWaterUnits
            || utility == null
            || (utility.channels & UtilityChannel.CleanWater) == 0
            || !IsFinitePositive(utility.maxThroughput)
            || utility.maxThroughput + Epsilon
                < ability.waterPerRefill
                    / ability.minimumRefillIntervalSeconds)
        {
            throw new InvalidOperationException(
                $"Crop irrigator '{irrigator.PersistentInstanceId.Value}' has invalid irrigation or clean-water throughput authoring.");
        }
    }

    private static bool IsFinitePositive(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

    private static bool IsInRange(
        BuildableObject irrigator,
        BuildableObject plot,
        int rangeManhattan)
    {
        IReadOnlyList<Vector2Int> left = irrigator.buildPoses;
        IReadOnlyList<Vector2Int> right = plot.buildPoses;
        if (left == null || left.Count == 0 || right == null || right.Count == 0)
        {
            return false;
        }

        for (int leftIndex = 0; leftIndex < left.Count; leftIndex++)
        {
            for (int rightIndex = 0; rightIndex < right.Count; rightIndex++)
            {
                int distance = Mathf.Abs(left[leftIndex].x - right[rightIndex].x)
                    + Mathf.Abs(left[leftIndex].y - right[rightIndex].y);
                if (distance <= rangeManhattan)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool HasRoute(
        BuildableObject irrigator,
        BuildableObject plot)
    {
        Grid grid = irrigator.Grid;
        IReadOnlyList<Vector2Int> starts = BuildingWorkAccessRules
            .EnumerateCandidates(
                irrigator.buildPoses,
                irrigator.BuildingData.IsGridMovement);
        IReadOnlyList<Vector2Int> destinations = BuildingWorkAccessRules
            .EnumerateCandidates(
                plot.buildPoses,
                plot.BuildingData.IsGridMovement);
        for (int startIndex = 0; startIndex < starts.Count; startIndex++)
        {
            Vector2Int start = starts[startIndex];
            if (!grid.IsValidGridPos(start) || !grid.IsWalkable(start))
            {
                continue;
            }
            for (int destinationIndex = 0;
                 destinationIndex < destinations.Count;
                 destinationIndex++)
            {
                Vector2Int destination = destinations[destinationIndex];
                if (!grid.IsValidGridPos(destination)
                    || !grid.IsWalkable(destination))
                {
                    continue;
                }
                if (grid.SearchPathTo(start, destination)
                    .ContainsPosition(destination))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool IsCoolingDown(
        IReadOnlyDictionary<string, SupplyStamp> supplies,
        string id,
        BuildableObject currentOwner,
        float now)
    {
        if (!supplies.TryGetValue(id, out SupplyStamp prior)
            || !ReferenceEquals(prior.Owner, currentOwner)
            || now + Epsilon < prior.SuppliedAt)
        {
            return false;
        }
        return now - prior.SuppliedAt + Epsilon < prior.MinimumInterval;
    }

    private sealed class SupplyStamp
    {
        public SupplyStamp(
            BuildableObject owner,
            float suppliedAt,
            float minimumInterval)
        {
            Owner = owner;
            SuppliedAt = suppliedAt;
            MinimumInterval = minimumInterval;
        }

        public BuildableObject Owner { get; }
        public float SuppliedAt { get; }
        public float MinimumInterval { get; }
    }

    private readonly struct Evaluation
    {
        public Evaluation(
            CropIrrigationAssessment assessment,
            BuildableObject irrigator,
            BuildingCropIrrigationAbility ability)
        {
            Assessment = assessment;
            Irrigator = irrigator;
            Ability = ability;
        }

        public CropIrrigationAssessment Assessment { get; }
        public BuildableObject Irrigator { get; }
        public BuildingCropIrrigationAbility Ability { get; }

        public static Evaluation Rejected(CropIrrigationAssessment assessment) =>
            new(assessment, null, null);
    }
}
