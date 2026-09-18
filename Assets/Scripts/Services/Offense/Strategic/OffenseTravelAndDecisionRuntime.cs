using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

internal sealed class OffenseDictionaryRestoreCandidate<T>
{
    internal OffenseDictionaryRestoreCandidate(Dictionary<string, T> values)
    {
        Values = values ?? throw new ArgumentNullException(nameof(values));
    }

    internal Dictionary<string, T> Values { get; }
}

public readonly struct OffenseReturnSafetySnapshot
{
    public OffenseReturnSafetySnapshot(
        int safeStepBudget,
        int forcedCombatCount,
        int nonCombatPitySteps)
    {
        SafeStepBudget = Mathf.Max(0, safeStepBudget);
        ForcedCombatCount = Mathf.Max(0, forcedCombatCount);
        NonCombatPitySteps = Mathf.Max(0, nonCombatPitySteps);
    }

    public int SafeStepBudget { get; }
    public int ForcedCombatCount { get; }
    public int NonCombatPitySteps { get; }
    public bool IsProtected => SafeStepBudget > 0;
    public float StressMultiplier => IsProtected ? 0.35f : 1f;
    public float DangerousEventWeightMultiplier => IsProtected ? 0.3f : 1f;
}

public interface IOffenseReturnSafetyRuntime
{
    OffenseReturnSafetySnapshot Get(string expeditionId);
    int GrantForObjective(
        string expeditionId,
        OffenseHexCoord currentCoord,
        OffenseHexCoord dungeonCoord);
    bool ConsumeMovedStep(string expeditionId);
    void RecordProtectedDangerousEvent(string expeditionId, bool forcedCombat);
    void ClearForSiteAttack(string expeditionId);
    void ClearOnArrival(string expeditionId);
    bool CanGenerateForcedCombat(
        string expeditionId,
        float averageHealthRatio,
        bool hasDownedMember,
        bool hasUsableWeaponForEveryActiveMember);
    bool MustUseNonCombatCard(string expeditionId);
    IReadOnlyList<OffenseReturnSafetyStateData> Capture();
}

public sealed class OffenseReturnSafetyRuntime : IOffenseReturnSafetyRuntime
{
    private readonly IOffenseWorldSimulation world;
    private Dictionary<string, OffenseReturnSafetyStateData> states =
        new Dictionary<string, OffenseReturnSafetyStateData>(StringComparer.Ordinal);

    public OffenseReturnSafetyRuntime(IOffenseWorldSimulation world)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public OffenseReturnSafetySnapshot Get(string expeditionId)
    {
        return states.TryGetValue(
            expeditionId ?? string.Empty,
            out OffenseReturnSafetyStateData state)
            ? new OffenseReturnSafetySnapshot(
                state.safeStepBudget,
                state.protectedForcedCombatCount,
                state.nonCombatPitySteps)
            : new OffenseReturnSafetySnapshot(0, 0, 0);
    }

    public int GrantForObjective(
        string expeditionId,
        OffenseHexCoord currentCoord,
        OffenseHexCoord dungeonCoord)
    {
        string id = RequireId(expeditionId);
        int steps = world.GetMinimumStepDistance(currentCoord, dungeonCoord);
        if (steps == int.MaxValue)
        {
            throw new InvalidOperationException(
                $"No traversable return path exists for expedition '{id}'.");
        }

        states[id] = new OffenseReturnSafetyStateData
        {
            expeditionId = id,
            safeStepBudget = Mathf.Max(0, steps),
            protectedForcedCombatCount = 0,
            nonCombatPitySteps = 0
        };
        return steps;
    }

    public bool ConsumeMovedStep(string expeditionId)
    {
        if (!states.TryGetValue(
                expeditionId ?? string.Empty,
                out OffenseReturnSafetyStateData state)
            || state.safeStepBudget <= 0)
        {
            return false;
        }

        state.safeStepBudget--;
        if (state.nonCombatPitySteps > 0)
        {
            state.nonCombatPitySteps--;
        }

        return true;
    }

    public void RecordProtectedDangerousEvent(
        string expeditionId,
        bool forcedCombat)
    {
        if (!states.TryGetValue(
                expeditionId ?? string.Empty,
                out OffenseReturnSafetyStateData state)
            || state.safeStepBudget <= 0)
        {
            return;
        }

        state.nonCombatPitySteps = Mathf.Max(state.nonCombatPitySteps, 2);
        if (forcedCombat)
        {
            state.protectedForcedCombatCount++;
        }
    }

    public void ClearForSiteAttack(string expeditionId) =>
        states.Remove(expeditionId ?? string.Empty);

    public void ClearOnArrival(string expeditionId) =>
        states.Remove(expeditionId ?? string.Empty);

    public bool CanGenerateForcedCombat(
        string expeditionId,
        float averageHealthRatio,
        bool hasDownedMember,
        bool hasUsableWeaponForEveryActiveMember)
    {
        OffenseReturnSafetySnapshot snapshot = Get(expeditionId);
        return !snapshot.IsProtected
            || (snapshot.ForcedCombatCount < 1
                && snapshot.NonCombatPitySteps <= 0
                && averageHealthRatio >= 0.4f
                && !hasDownedMember
                && hasUsableWeaponForEveryActiveMember);
    }

    public bool MustUseNonCombatCard(string expeditionId)
    {
        OffenseReturnSafetySnapshot snapshot = Get(expeditionId);
        return snapshot.IsProtected && snapshot.NonCombatPitySteps > 0;
    }

    public IReadOnlyList<OffenseReturnSafetyStateData> Capture()
    {
        return states.Values
            .OrderBy(state => state.expeditionId, StringComparer.Ordinal)
            .Select(Clone)
            .ToList();
    }

    internal OffenseDictionaryRestoreCandidate<OffenseReturnSafetyStateData>
        PrepareRestore(IEnumerable<OffenseReturnSafetyStateData> restored)
    {
        Dictionary<string, OffenseReturnSafetyStateData> candidate =
            new(StringComparer.Ordinal);
        foreach (OffenseReturnSafetyStateData source in
                 restored ?? throw new ArgumentNullException(nameof(restored)))
        {
            if (source == null
                || string.IsNullOrWhiteSpace(source.expeditionId)
                || source.safeStepBudget < 0
                || source.protectedForcedCombatCount < 0
                || source.nonCombatPitySteps < 0)
            {
                throw new InvalidOperationException(
                    $"Invalid offense return-safety state '{source?.expeditionId ?? "null"}'.");
            }

            if (candidate.ContainsKey(source.expeditionId))
            {
                throw new InvalidOperationException(
                    $"Duplicate offense return-safety state '{source.expeditionId}'.");
            }

            candidate.Add(source.expeditionId, Clone(source));
        }

        return new OffenseDictionaryRestoreCandidate<OffenseReturnSafetyStateData>(
            candidate);
    }

    internal void PublishRestore(
        OffenseDictionaryRestoreCandidate<OffenseReturnSafetyStateData> candidate)
    {
        states = (candidate ?? throw new ArgumentNullException(nameof(candidate)))
            .Values;
    }

    private static OffenseReturnSafetyStateData Clone(
        OffenseReturnSafetyStateData source)
    {
        return new OffenseReturnSafetyStateData
        {
            expeditionId = source.expeditionId,
            safeStepBudget = source.safeStepBudget,
            protectedForcedCombatCount = source.protectedForcedCombatCount,
            nonCombatPitySteps = source.nonCombatPitySteps
        };
    }

    private static string RequireId(string expeditionId)
    {
        return !string.IsNullOrWhiteSpace(expeditionId)
            ? expeditionId
            : throw new ArgumentException(
                "An expedition ID is required.",
                nameof(expeditionId));
    }
}

public readonly struct OffenseTravelStepResult
{
    public OffenseTravelStepResult(
        bool moved,
        bool arrived,
        OffenseHexCoord position,
        bool consumedSafeStep,
        string siteId)
    {
        Moved = moved;
        Arrived = arrived;
        Position = position;
        ConsumedSafeStep = consumedSafeStep;
        SiteId = siteId ?? string.Empty;
    }

    public bool Moved { get; }
    public bool Arrived { get; }
    public OffenseHexCoord Position { get; }
    public bool ConsumedSafeStep { get; }
    public string SiteId { get; }
}

public interface IOffenseTravelRuntime
{
    IReadOnlyCollection<OffenseTravelStateData> ActiveTravel { get; }
    event Action<string, OffenseTravelStepResult> StepCompleted;
    event Action<string, string> DecisionRequired;
    event Action<string, string> SiteReached;

    bool TryCreateExpedition(string expeditionId, out string reason);
    bool TrySetDestination(
        string expeditionId,
        OffenseHexCoord destination,
        string destinationSiteId,
        OffenseTravelProfile profile,
        bool startsSiteAttack,
        out string reason);
    bool TryAdvanceOneStep(
        string expeditionId,
        bool forcedMovement,
        out OffenseTravelStepResult result,
        out string reason);
    bool TryPauseForDecision(string expeditionId);
    bool TryResumeAfterDecision(string expeditionId);
    bool TryPauseForBattle(string expeditionId);
    bool TryResumeAfterBattle(string expeditionId);
    bool TryAdjustExposure(
        string expeditionId,
        float amount,
        out float exposure);
    bool TryGetState(string expeditionId, out OffenseTravelStateData state);
    bool TryRemove(string expeditionId);
    void Tick(float deltaTime);
    IReadOnlyList<OffenseTravelStateData> Capture();
}

public sealed class OffenseTravelRuntime : IOffenseTravelRuntime
{
    private const float BaseSegmentDurationSeconds = 2.5f;
    private const float SegmentComparisonTolerance = 0.0001f;
    private readonly IOffenseWorldSimulation world;
    private readonly IOffenseReturnSafetyRuntime returnSafety;
    private readonly IOffenseFieldMedicalRuntime fieldMedical;
    private readonly IClimateQuery climate;
    private readonly IClimateDefinitionCatalog climateDefinitions;
    private readonly IMilestoneGameplayModifierQuery milestoneModifiers;
    private readonly IFacilityCapabilityQuery facilities;
    private Dictionary<string, OffenseTravelStateData> states =
        new Dictionary<string, OffenseTravelStateData>(StringComparer.Ordinal);

    public OffenseTravelRuntime(
        IOffenseWorldSimulation world,
        IOffenseReturnSafetyRuntime returnSafety,
        IOffenseFieldMedicalRuntime fieldMedical,
        IClimateQuery climate,
        IClimateDefinitionCatalog climateDefinitions,
        IMilestoneGameplayModifierQuery milestoneModifiers = null,
        IFacilityCapabilityQuery facilities = null)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.returnSafety = returnSafety
            ?? throw new ArgumentNullException(nameof(returnSafety));
        this.fieldMedical = fieldMedical;
        this.climate = climate ?? throw new ArgumentNullException(nameof(climate));
        this.climateDefinitions = climateDefinitions
            ?? throw new ArgumentNullException(nameof(climateDefinitions));
        this.milestoneModifiers = milestoneModifiers
            ?? NeutralMilestoneGameplayModifierQuery.Instance;
        this.facilities = facilities;
    }

    public IReadOnlyCollection<OffenseTravelStateData> ActiveTravel =>
        states.Values;
    public event Action<string, OffenseTravelStepResult> StepCompleted;
    public event Action<string, string> DecisionRequired;
    public event Action<string, string> SiteReached;

    public bool TryCreateExpedition(string expeditionId, out string reason)
    {
        if (string.IsNullOrWhiteSpace(expeditionId))
        {
            reason = "원정 식별자가 없습니다.";
            return false;
        }

        if (states.ContainsKey(expeditionId))
        {
            reason = "이미 이동 중인 원정대입니다.";
            return false;
        }

        states.Add(expeditionId, new OffenseTravelStateData
        {
            expeditionId = expeditionId,
            currentQ = world.DungeonCoord.Q,
            currentR = world.DungeonCoord.R,
            destinationQ = world.DungeonCoord.Q,
            destinationR = world.DungeonCoord.R,
            activeSegmentQ = world.DungeonCoord.Q,
            activeSegmentR = world.DungeonCoord.R
        });
        reason = string.Empty;
        return true;
    }

    public bool TrySetDestination(
        string expeditionId,
        OffenseHexCoord destination,
        string destinationSiteId,
        OffenseTravelProfile profile,
        bool startsSiteAttack,
        out string reason)
    {
        if (!states.TryGetValue(
                expeditionId ?? string.Empty,
                out OffenseTravelStateData state))
        {
            reason = "원정 이동 상태를 찾을 수 없습니다.";
            return false;
        }

        if (fieldMedical?.IsStranded(expeditionId) == true || state.stranded)
        {
            reason = string.IsNullOrWhiteSpace(state.strandedReason)
                ? "원정대가 조난되어 구조가 필요합니다."
                : state.strandedReason;
            return false;
        }

        if (state.pausedForBattle || state.pausedForDecision)
        {
            reason = "현재 사건이나 전투를 먼저 해결해야 합니다.";
            return false;
        }

        if (!IsFinite(profile.RoadMultiplier)
            || !IsFinite(profile.WeatherMultiplier)
            || !IsFinite(profile.InjuryMultiplier)
            || !IsFinite(profile.LoadMultiplier)
            || Mathf.Abs(profile.InjuryMultiplier - 1f)
                > SegmentComparisonTolerance)
        {
            reason = "원정 부상 이동 보정은 fieldMedical에서 한 번만 제공해야 합니다.";
            return false;
        }

        OffenseTravelProfile traversalProfile = WithoutMedicalProfile(profile);
        if (!world.TryFindPath(
                state.CurrentCoord,
                destination,
                traversalProfile,
                out IReadOnlyList<OffenseHexCoord> path,
                out _))
        {
            reason = "목적지까지 도달 가능한 경로가 없습니다.";
            return false;
        }

        OffenseTravelStateData candidate = Clone(state);
        candidate.destinationQ = destination.Q;
        candidate.destinationR = destination.R;
        candidate.destinationSiteId = destinationSiteId ?? string.Empty;
        candidate.remainingPath = path.Select(OffenseHexCoordSaveData.From).ToList();
        candidate.routeRoadMultiplier = traversalProfile.RoadMultiplier;
        candidate.routeWeatherMultiplier = traversalProfile.WeatherMultiplier;
        candidate.routeLoadMultiplier = traversalProfile.LoadMultiplier;
        ClearActiveSegment(candidate);
        if (candidate.remainingPath.Count > 0
            && !TryFreezeNextSegment(candidate, out reason))
        {
            return false;
        }

        if (startsSiteAttack && !string.IsNullOrWhiteSpace(destinationSiteId))
        {
            returnSafety.ClearForSiteAttack(expeditionId);
        }

        CommitDestination(state, candidate);

        reason = string.Empty;
        return true;
    }

    public bool TryAdvanceOneStep(
        string expeditionId,
        bool forcedMovement,
        out OffenseTravelStepResult result,
        out string reason)
    {
        return TryAdvanceOneStep(
            expeditionId,
            forcedMovement,
            fromNormalTick: false,
            out result,
            out reason,
            out _);
    }

    private bool TryAdvanceOneStep(
        string expeditionId,
        bool forcedMovement,
        bool fromNormalTick,
        out OffenseTravelStepResult result,
        out string reason,
        out bool canCarryElapsed)
    {
        result = default;
        canCarryElapsed = false;
        if (!states.TryGetValue(
                expeditionId ?? string.Empty,
                out OffenseTravelStateData state))
        {
            reason = "원정 이동 상태를 찾을 수 없습니다.";
            return false;
        }

        if (fieldMedical?.IsStranded(expeditionId) == true || state.stranded)
        {
            reason = string.IsNullOrWhiteSpace(state.strandedReason)
                ? "원정대가 조난되어 이동할 수 없습니다."
                : state.strandedReason;
            return false;
        }

        if (state.pausedForBattle || state.pausedForDecision)
        {
            reason = "현재 사건이나 전투를 먼저 해결해야 합니다.";
            return false;
        }

        if (!fromNormalTick)
        {
            state.progressToNextTile = 0f;
        }

        if (state.remainingPath == null || state.remainingPath.Count == 0)
        {
            reason = "이동 경로가 비어 있습니다.";
            return false;
        }

        if (!TryEnsureActiveSegment(state, out reason))
        {
            return false;
        }

        if (state.remainingPath[0] == null)
        {
            reason = "다음 이동 구간 좌표가 없습니다.";
            return false;
        }

        OffenseHexCoord next = state.remainingPath[0].ToCoord();
        if (state.CurrentCoord.DistanceTo(next) != 1
            || state.ActiveSegmentCoord != next
            || !world.TryGetTile(next, out OffenseHexTileState tile)
            || tile.blocked)
        {
            state.remainingPath.Clear();
            ClearActiveSegment(state);
            reason = "경로가 변경되어 목적지를 다시 선택해야 합니다.";
            return false;
        }

        string completedSegmentWeatherFrontId =
            state.activeSegmentWeatherFrontId;
        List<OffenseHexCoordSaveData> continuingPath = state.remainingPath;
        int expectedDestinationQ = state.destinationQ;
        int expectedDestinationR = state.destinationR;
        string expectedDestinationSiteId = state.destinationSiteId;
        state.remainingPath.RemoveAt(0);
        state.currentQ = next.Q;
        state.currentR = next.R;
        ClearActiveSegment(state);
        OffenseHexCoord[] expectedRemainingPath = state.remainingPath
            .Select(value => value.ToCoord())
            .ToArray();
        bool consumedSafeStep = returnSafety.ConsumeMovedStep(expeditionId);
        bool arrived = state.remainingPath.Count == 0
            && next == state.DestinationCoord;
        result = new OffenseTravelStepResult(
            moved: true,
            arrived,
            next,
            consumedSafeStep,
            arrived ? state.destinationSiteId : string.Empty);
        StepCompleted?.Invoke(expeditionId, result);

        if (arrived && next == world.DungeonCoord)
        {
            returnSafety.ClearOnArrival(expeditionId);
            fieldMedical?.ClearOnDungeonArrival(expeditionId);
        }

        if (!TryGetUnchangedRouteState(
                expeditionId,
                state,
                continuingPath,
                expectedRemainingPath,
                expectedDestinationQ,
                expectedDestinationR,
                expectedDestinationSiteId,
                out state))
        {
            reason = string.Empty;
            return true;
        }

        arrived = state.remainingPath.Count == 0
            && state.CurrentCoord == state.DestinationCoord
            && state.CurrentCoord == next;
        if (arrived)
        {
            if (!string.IsNullOrWhiteSpace(state.destinationSiteId))
            {
                SiteReached?.Invoke(expeditionId, state.destinationSiteId);
            }

            reason = string.Empty;
            return true;
        }

        if (state.pausedForBattle
            || state.pausedForDecision
            || state.stranded
            || fieldMedical?.IsStranded(expeditionId) == true)
        {
            reason = string.Empty;
            return true;
        }

        if (ShouldRequireTravelDecision(state, forcedMovement))
        {
            state.pausedForDecision = true;
            DecisionRequired?.Invoke(
                expeditionId,
                completedSegmentWeatherFrontId);
            if (TryGetUnchangedRouteState(
                    expeditionId,
                    state,
                    continuingPath,
                    expectedRemainingPath,
                    expectedDestinationQ,
                    expectedDestinationR,
                    expectedDestinationSiteId,
                    out OffenseTravelStateData resumedState)
                && !resumedState.pausedForDecision
                && !resumedState.pausedForBattle
                && !resumedState.stranded
                && fieldMedical?.IsStranded(expeditionId) != true)
            {
                TryEnsureActiveSegment(resumedState, out _);
            }

            reason = string.Empty;
            return true;
        }

        if (!TryGetUnchangedRouteState(
                expeditionId,
                state,
                continuingPath,
                expectedRemainingPath,
                expectedDestinationQ,
                expectedDestinationR,
                expectedDestinationSiteId,
                out state)
            || state.pausedForBattle
            || state.pausedForDecision
            || state.stranded
            || fieldMedical?.IsStranded(expeditionId) == true
            || !TryEnsureActiveSegment(state, out _))
        {
            reason = string.Empty;
            return true;
        }

        canCarryElapsed = fromNormalTick;
        reason = string.Empty;
        return true;
    }

    public bool TryPauseForDecision(string expeditionId)
    {
        return SetPause(expeditionId, decision: true, paused: true);
    }

    public bool TryResumeAfterDecision(string expeditionId)
    {
        return SetPause(expeditionId, decision: true, paused: false);
    }

    public bool TryPauseForBattle(string expeditionId)
    {
        return SetPause(expeditionId, decision: false, paused: true);
    }

    public bool TryResumeAfterBattle(string expeditionId)
    {
        return SetPause(expeditionId, decision: false, paused: false);
    }

    public bool TryAdjustExposure(
        string expeditionId,
        float amount,
        out float exposure)
    {
        exposure = 0f;
        if (!states.TryGetValue(
                expeditionId ?? string.Empty,
                out OffenseTravelStateData state))
        {
            return false;
        }

        state.exposure = Mathf.Clamp(state.exposure + amount, 0f, 100f);
        exposure = state.exposure;
        return true;
    }

    public bool TryGetState(string expeditionId, out OffenseTravelStateData state)
    {
        return states.TryGetValue(expeditionId ?? string.Empty, out state);
    }

    public bool TryRemove(string expeditionId)
    {
        return !string.IsNullOrWhiteSpace(expeditionId)
            && states.Remove(expeditionId);
    }

    public void Tick(float deltaTime)
    {
        if (!IsFinite(deltaTime) || deltaTime <= 0f)
        {
            return;
        }

        float elapsed = deltaTime;

        string[] movingExpeditions = states.Values
            .Where(state => state != null
                && !state.pausedForBattle
                && !state.pausedForDecision
                && fieldMedical?.IsStranded(state.expeditionId) != true
                && state.remainingPath != null
                && state.remainingPath.Count > 0)
            .Select(state => state.expeditionId)
            .ToArray();
        foreach (string expeditionId in movingExpeditions)
        {
            float remainingElapsed = elapsed;
            while (remainingElapsed > 0f
                && TryGetTickableState(expeditionId, out OffenseTravelStateData state))
            {
                if (!TryEnsureActiveSegment(state, out _))
                {
                    break;
                }

                float required = Mathf.Max(
                    0f,
                    state.activeSegmentDurationSeconds
                    - state.progressToNextTile);
                if (remainingElapsed + SegmentComparisonTolerance < required)
                {
                    state.progressToNextTile += remainingElapsed;
                    break;
                }

                remainingElapsed = Mathf.Max(0f, remainingElapsed - required);
                state.progressToNextTile = state.activeSegmentDurationSeconds;
                if (!TryAdvanceOneStep(
                        expeditionId,
                        forcedMovement: false,
                        fromNormalTick: true,
                        out _,
                        out _,
                        out bool canCarryElapsed)
                    || !canCarryElapsed)
                {
                    break;
                }
            }
        }
    }

    private bool TryGetTickableState(
        string expeditionId,
        out OffenseTravelStateData state)
    {
        return states.TryGetValue(expeditionId, out state)
            && state != null
            && !state.pausedForBattle
            && !state.pausedForDecision
            && !state.stranded
            && fieldMedical?.IsStranded(expeditionId) != true
            && state.remainingPath != null
            && state.remainingPath.Count > 0;
    }

    private float FacilityTravelMultiplier()
    {
        if (facilities == null)
        {
            return 1f;
        }
        float multiplier = 1f;
        if (facilities.FindOperational(
                ResearchFacilityCommandKind.ClimateMapping).Count > 0)
        {
            multiplier *= 0.95f;
        }
        if (facilities.FindOperational(
                ResearchFacilityCommandKind.ChronometricNavigation).Count > 0)
        {
            multiplier *= 0.95f;
        }
        return multiplier;
    }

    private bool TryEnsureActiveSegment(
        OffenseTravelStateData state,
        out string reason)
    {
        if (state.activeSegmentDurationSeconds > 0f)
        {
            if (state.remainingPath == null
                || state.remainingPath.Count == 0
                || state.remainingPath[0] == null)
            {
                reason = "활성 이동 구간에 다음 경로가 없습니다.";
                return false;
            }

            OffenseHexCoord expected = state.remainingPath[0].ToCoord();
            if (state.ActiveSegmentCoord == expected
                && state.activeSegmentTraversalCost
                    >= OffenseTraversalCostRules.MinimumStepCost
                && state.progressToNextTile >= 0f
                && state.progressToNextTile
                    <= state.activeSegmentDurationSeconds
                        + SegmentComparisonTolerance)
            {
                reason = string.Empty;
                return true;
            }

            reason = "활성 이동 구간 상태가 현재 경로와 일치하지 않습니다.";
            return false;
        }

        return TryFreezeNextSegment(state, out reason);
    }

    private bool TryFreezeNextSegment(
        OffenseTravelStateData state,
        out string reason)
    {
        if (state.remainingPath == null || state.remainingPath.Count == 0)
        {
            ClearActiveSegment(state);
            reason = string.Empty;
            return true;
        }

        if (state.remainingPath[0] == null)
        {
            reason = "다음 이동 구간 좌표가 없습니다.";
            return false;
        }

        OffenseHexCoord next = state.remainingPath[0].ToCoord();
        if (state.CurrentCoord.DistanceTo(next) != 1
            || !world.TryGetTile(next, out OffenseHexTileState tile)
            || tile.blocked)
        {
            reason = "다음 이동 구간을 확정할 수 없습니다.";
            return false;
        }

        string weatherFrontId = climate.WeatherFrontId?.Trim() ?? string.Empty;
        if (weatherFrontId.Length == 0)
        {
            reason = "현재 원정 날씨 식별자가 없습니다.";
            return false;
        }

        WeatherFrontDefinition weatherFront;
        try
        {
            weatherFront = climateDefinitions.RequireFront(weatherFrontId);
        }
        catch (KeyNotFoundException exception)
        {
            reason = exception.Message;
            return false;
        }
        if (!weatherFront.IsValid
            || !string.Equals(
                weatherFront.Id,
                weatherFrontId,
                StringComparison.Ordinal))
        {
            reason = $"현재 원정 날씨 정의 '{weatherFrontId}'가 유효하지 않습니다.";
            return false;
        }

        float activeWeatherMultiplier =
            weatherFront.ExpeditionTravelMultiplier;
        float composedWeatherMultiplier =
            state.routeWeatherMultiplier * activeWeatherMultiplier;
        if (!IsFinite(activeWeatherMultiplier)
            || !IsFinite(composedWeatherMultiplier)
            || activeWeatherMultiplier < 0.1f
            || composedWeatherMultiplier < 0.1f)
        {
            reason = "현재 원정 날씨 이동 보정이 유효하지 않습니다.";
            return false;
        }

        OffenseTravelProfile profile = new OffenseTravelProfile(
            state.routeRoadMultiplier,
            composedWeatherMultiplier,
            1f,
            state.routeLoadMultiplier);
        float traversalCost = OffenseTraversalCostRules.GetStepCost(tile, profile);
        float medicalMultiplier = fieldMedical?.GetMovementTimeMultiplier(
                state.expeditionId)
            ?? 1f;
        float milestoneMultiplier = Mathf.Clamp(
            milestoneModifiers.ExpeditionTravelTimeMultiplier,
            0.1f,
            1f);
        float facilityMultiplier = FacilityTravelMultiplier();
        float duration = BaseSegmentDurationSeconds
            * traversalCost
            * Mathf.Max(1f, medicalMultiplier)
            * milestoneMultiplier
            * facilityMultiplier;
        if (!IsFinite(traversalCost)
            || !IsFinite(medicalMultiplier)
            || !IsFinite(milestoneMultiplier)
            || !IsFinite(facilityMultiplier)
            || !IsFinite(duration)
            || duration <= 0f)
        {
            reason = "다음 이동 구간의 시간 보정이 유효하지 않습니다.";
            return false;
        }

        state.activeSegmentQ = next.Q;
        state.activeSegmentR = next.R;
        state.activeSegmentWeatherFrontId = weatherFrontId;
        state.activeSegmentWeatherMultiplier = activeWeatherMultiplier;
        state.activeSegmentTraversalCost = traversalCost;
        state.progressToNextTile = 0f;
        state.movementTimeMultiplier = Mathf.Max(1f, medicalMultiplier);
        state.milestoneTimeMultiplier = milestoneMultiplier;
        state.facilityTimeMultiplier = facilityMultiplier;
        state.activeSegmentDurationSeconds = duration;
        reason = string.Empty;
        return true;
    }

    private static void ClearActiveSegment(OffenseTravelStateData state)
    {
        state.activeSegmentQ = state.currentQ;
        state.activeSegmentR = state.currentR;
        state.activeSegmentWeatherFrontId = string.Empty;
        state.activeSegmentWeatherMultiplier = 1f;
        state.activeSegmentTraversalCost = 0f;
        state.progressToNextTile = 0f;
        state.movementTimeMultiplier = 1f;
        state.milestoneTimeMultiplier = 1f;
        state.facilityTimeMultiplier = 1f;
        state.activeSegmentDurationSeconds = 0f;
    }

    private static void CommitDestination(
        OffenseTravelStateData state,
        OffenseTravelStateData candidate)
    {
        state.destinationQ = candidate.destinationQ;
        state.destinationR = candidate.destinationR;
        state.destinationSiteId = candidate.destinationSiteId;
        state.remainingPath = candidate.remainingPath;
        state.routeRoadMultiplier = candidate.routeRoadMultiplier;
        state.routeWeatherMultiplier = candidate.routeWeatherMultiplier;
        state.routeLoadMultiplier = candidate.routeLoadMultiplier;
        state.activeSegmentQ = candidate.activeSegmentQ;
        state.activeSegmentR = candidate.activeSegmentR;
        state.activeSegmentWeatherFrontId = candidate.activeSegmentWeatherFrontId;
        state.activeSegmentWeatherMultiplier = candidate.activeSegmentWeatherMultiplier;
        state.activeSegmentTraversalCost = candidate.activeSegmentTraversalCost;
        state.progressToNextTile = candidate.progressToNextTile;
        state.movementTimeMultiplier = candidate.movementTimeMultiplier;
        state.milestoneTimeMultiplier = candidate.milestoneTimeMultiplier;
        state.facilityTimeMultiplier = candidate.facilityTimeMultiplier;
        state.activeSegmentDurationSeconds = candidate.activeSegmentDurationSeconds;
    }

    private static OffenseTravelProfile WithoutMedicalProfile(
        OffenseTravelProfile profile)
    {
        return new OffenseTravelProfile(
            profile.RoadMultiplier,
            profile.WeatherMultiplier,
            1f,
            profile.LoadMultiplier);
    }

    private bool TryGetUnchangedRouteState(
        string expeditionId,
        OffenseTravelStateData expectedState,
        List<OffenseHexCoordSaveData> expectedPath,
        IReadOnlyList<OffenseHexCoord> expectedPathCoordinates,
        int expectedDestinationQ,
        int expectedDestinationR,
        string expectedDestinationSiteId,
        out OffenseTravelStateData state)
    {
        return states.TryGetValue(expeditionId, out state)
            && ReferenceEquals(state, expectedState)
            && ReferenceEquals(state.remainingPath, expectedPath)
            && state.remainingPath != null
            && state.remainingPath.All(value => value != null)
            && state.remainingPath
                .Select(value => value.ToCoord())
                .SequenceEqual(expectedPathCoordinates)
            && state.destinationQ == expectedDestinationQ
            && state.destinationR == expectedDestinationR
            && string.Equals(
                state.destinationSiteId,
                expectedDestinationSiteId,
                StringComparison.Ordinal);
    }

    public IReadOnlyList<OffenseTravelStateData> Capture()
    {
        return states.Values
            .OrderBy(state => state.expeditionId, StringComparer.Ordinal)
            .Select(Clone)
            .ToList();
    }

    internal OffenseDictionaryRestoreCandidate<OffenseTravelStateData>
        PrepareRestore(
            IEnumerable<OffenseTravelStateData> restored,
            IReadOnlyDictionary<OffenseHexCoord, OffenseHexTileState>
                restoredWorldTiles = null)
    {
        Dictionary<string, OffenseTravelStateData> candidate =
            new(StringComparer.Ordinal);
        foreach (OffenseTravelStateData source in
                 restored ?? throw new ArgumentNullException(nameof(restored)))
        {
            if (source == null
                || string.IsNullOrWhiteSpace(source.expeditionId)
                || candidate.ContainsKey(source.expeditionId)
                || !TryGetRestoreTile(source.CurrentCoord, out _)
                || !TryGetRestoreTile(source.DestinationCoord, out _)
                || source.remainingPath == null
                || source.remainingPath.Any(value => value == null)
                || source.progressToNextTile < 0f
                || source.exposure < 0f
                || source.exposure > 100f
                || source.eventSequence < 0
                || source.routeRoadMultiplier < 0.1f
                || source.routeWeatherMultiplier < 0.1f
                || source.routeLoadMultiplier < 0.1f
                || source.movementTimeMultiplier is < 1f or > 2.5f
                || source.milestoneTimeMultiplier is < 0.1f or > 1f
                || source.facilityTimeMultiplier is <= 0f or > 1f
                || source.activeSegmentWeatherMultiplier < 0f
                || source.activeSegmentTraversalCost < 0f
                || source.activeSegmentDurationSeconds < 0f
                || !IsFinite(source.progressToNextTile)
                || !IsFinite(source.exposure)
                || !IsFinite(source.routeRoadMultiplier)
                || !IsFinite(source.routeWeatherMultiplier)
                || !IsFinite(source.routeLoadMultiplier)
                || !IsFinite(source.movementTimeMultiplier)
                || !IsFinite(source.milestoneTimeMultiplier)
                || !IsFinite(source.facilityTimeMultiplier)
                || !IsFinite(source.activeSegmentWeatherMultiplier)
                || !IsFinite(source.activeSegmentTraversalCost)
                || !IsFinite(source.activeSegmentDurationSeconds))
            {
                throw new InvalidOperationException(
                    $"Invalid or duplicate offense travel state '{source?.expeditionId ?? "null"}'.");
            }

            OffenseTravelStateData clone = Clone(source);
            if (clone.remainingPath.Any(coord =>
                    !TryGetRestoreTile(
                        coord.ToCoord(),
                        out OffenseHexTileState tile)
                    || tile.blocked))
            {
                throw new InvalidOperationException(
                    $"Offense travel state '{source.expeditionId}' contains an invalid or blocked path tile.");
            }

            ValidateRestoredRoute(clone);

            candidate.Add(clone.expeditionId, clone);
        }

        return new OffenseDictionaryRestoreCandidate<OffenseTravelStateData>(
            candidate);

        bool TryGetRestoreTile(
            OffenseHexCoord coordinate,
            out OffenseHexTileState tile)
        {
            return restoredWorldTiles != null
                ? restoredWorldTiles.TryGetValue(coordinate, out tile)
                : world.TryGetTile(coordinate, out tile);
        }

        void ValidateRestoredRoute(OffenseTravelStateData travel)
        {
            OffenseHexCoord previous = travel.CurrentCoord;
            foreach (OffenseHexCoordSaveData pathCoordinate in travel.remainingPath)
            {
                OffenseHexCoord coordinate = pathCoordinate.ToCoord();
                if (previous.DistanceTo(coordinate) != 1)
                {
                    throw new InvalidOperationException(
                        $"Offense travel state '{travel.expeditionId}' contains a non-adjacent path segment.");
                }
                previous = coordinate;
            }

            if (travel.remainingPath.Count > 0
                && previous != travel.DestinationCoord)
            {
                throw new InvalidOperationException(
                    $"Offense travel state '{travel.expeditionId}' path does not end at its destination.");
            }

            bool hasActiveSegment = travel.activeSegmentDurationSeconds > 0f;
            if (!hasActiveSegment)
            {
                if (travel.activeSegmentTraversalCost != 0f
                    || travel.progressToNextTile != 0f
                    || travel.ActiveSegmentCoord != travel.CurrentCoord
                    || travel.movementTimeMultiplier != 1f
                    || travel.milestoneTimeMultiplier != 1f
                    || travel.facilityTimeMultiplier != 1f
                    || !string.IsNullOrEmpty(
                        travel.activeSegmentWeatherFrontId)
                    || !IsLegacyNeutralWeatherMultiplier(
                        travel.activeSegmentWeatherMultiplier))
                {
                    throw new InvalidOperationException(
                        $"Offense travel state '{travel.expeditionId}' has a partial inactive segment.");
                }
                travel.activeSegmentWeatherFrontId = string.Empty;
                travel.activeSegmentWeatherMultiplier = 1f;
                return;
            }

            if (travel.remainingPath.Count == 0
                || travel.ActiveSegmentCoord
                    != travel.remainingPath[0].ToCoord()
                || travel.activeSegmentTraversalCost
                    < OffenseTraversalCostRules.MinimumStepCost
                || travel.progressToNextTile
                    > travel.activeSegmentDurationSeconds
                        + SegmentComparisonTolerance)
            {
                throw new InvalidOperationException(
                    $"Offense travel state '{travel.expeditionId}' has an inconsistent active segment.");
            }

            if (string.IsNullOrEmpty(travel.activeSegmentWeatherFrontId))
            {
                if (!IsLegacyNeutralWeatherMultiplier(
                        travel.activeSegmentWeatherMultiplier))
                {
                    throw new InvalidOperationException(
                        $"Offense travel state '{travel.expeditionId}' has invalid legacy weather provenance.");
                }
                travel.activeSegmentWeatherMultiplier = 1f;
            }
            else
            {
                if (!string.Equals(
                        travel.activeSegmentWeatherFrontId,
                        travel.activeSegmentWeatherFrontId.Trim(),
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Offense travel state '{travel.expeditionId}' has a non-canonical weather front ID.");
                }
                WeatherFrontDefinition restoredFront =
                    climateDefinitions.RequireFront(
                        travel.activeSegmentWeatherFrontId);
                if (Mathf.Abs(
                        restoredFront.ExpeditionTravelMultiplier
                        - travel.activeSegmentWeatherMultiplier)
                    > SegmentComparisonTolerance)
                {
                    throw new InvalidOperationException(
                        $"Offense travel state '{travel.expeditionId}' has a torn active-segment weather snapshot.");
                }
            }

            float expectedDuration = BaseSegmentDurationSeconds
                * travel.activeSegmentTraversalCost
                * travel.movementTimeMultiplier
                * travel.milestoneTimeMultiplier
                * travel.facilityTimeMultiplier;
            if (Mathf.Abs(
                    expectedDuration - travel.activeSegmentDurationSeconds)
                > SegmentComparisonTolerance)
            {
                throw new InvalidOperationException(
                    $"Offense travel state '{travel.expeditionId}' has a torn active-segment duration.");
            }
        }
    }

    internal void PublishRestore(
        OffenseDictionaryRestoreCandidate<OffenseTravelStateData> candidate)
    {
        states = (candidate ?? throw new ArgumentNullException(nameof(candidate)))
            .Values;
    }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool IsLegacyNeutralWeatherMultiplier(float value) =>
        value == 0f
        || Mathf.Abs(value - 1f) <= SegmentComparisonTolerance;

    private bool SetPause(string expeditionId, bool decision, bool paused)
    {
        if (!states.TryGetValue(
                expeditionId ?? string.Empty,
                out OffenseTravelStateData state))
        {
            return false;
        }

        if (decision)
        {
            state.pausedForDecision = paused;
        }
        else
        {
            state.pausedForBattle = paused;
        }

        if (!paused
            && !state.pausedForDecision
            && !state.pausedForBattle
            && !state.stranded
            && fieldMedical?.IsStranded(expeditionId) != true
            && state.remainingPath != null
            && state.remainingPath.Count > 0
            && !TryEnsureActiveSegment(state, out _))
        {
            if (decision)
            {
                state.pausedForDecision = true;
            }
            else
            {
                state.pausedForBattle = true;
            }
            return false;
        }

        return true;
    }

    private static bool ShouldRequireTravelDecision(
        OffenseTravelStateData state,
        bool forcedMovement)
    {
        if (forcedMovement)
        {
            return false;
        }

        uint hash = DeterministicHash(
            state.expeditionId,
            state.eventSequence,
            state.currentQ,
            state.currentR);
        state.eventSequence++;
        return hash % 100u < 32u;
    }

    private static OffenseTravelStateData Clone(OffenseTravelStateData source)
    {
        return new OffenseTravelStateData
        {
            expeditionId = source.expeditionId,
            currentQ = source.currentQ,
            currentR = source.currentR,
            destinationQ = source.destinationQ,
            destinationR = source.destinationR,
            destinationSiteId = source.destinationSiteId,
            routeRoadMultiplier = source.routeRoadMultiplier,
            routeWeatherMultiplier = source.routeWeatherMultiplier,
            routeLoadMultiplier = source.routeLoadMultiplier,
            activeSegmentQ = source.activeSegmentQ,
            activeSegmentR = source.activeSegmentR,
            activeSegmentWeatherFrontId = source.activeSegmentWeatherFrontId,
            activeSegmentWeatherMultiplier = source.activeSegmentWeatherMultiplier,
            activeSegmentTraversalCost = source.activeSegmentTraversalCost,
            movementTimeMultiplier = source.movementTimeMultiplier,
            milestoneTimeMultiplier = source.milestoneTimeMultiplier,
            facilityTimeMultiplier = source.facilityTimeMultiplier,
            activeSegmentDurationSeconds = source.activeSegmentDurationSeconds,
            stranded = source.stranded,
            strandedReason = source.strandedReason,
            remainingPath = source.remainingPath
                .Select(coord => new OffenseHexCoordSaveData
                {
                    q = coord.q,
                    r = coord.r
                })
                .ToList(),
            progressToNextTile = source.progressToNextTile,
            exposure = source.exposure,
            pausedForDecision = source.pausedForDecision,
            pausedForBattle = source.pausedForBattle,
            eventSequence = source.eventSequence
        };
    }

    internal static uint DeterministicHash(
        string id,
        int sequence,
        int q,
        int r)
    {
        unchecked
        {
            uint hash = 2166136261u;
            string value = id ?? string.Empty;
            for (int index = 0; index < value.Length; index++)
            {
                hash ^= value[index];
                hash *= 16777619u;
            }

            hash ^= (uint)sequence * 0x9E3779B9u;
            hash ^= (uint)q * 0x85EBCA6Bu;
            hash ^= (uint)r * 0xC2B2AE35u;
            hash ^= hash >> 16;
            return hash;
        }
    }
}

public sealed class OffenseTravelTicker : ITickable
{
    private readonly IOffenseTravelRuntime travel;
    private readonly IGameClock gameClock;

    public OffenseTravelTicker(
        IOffenseTravelRuntime travel,
        IGameClock gameClock)
    {
        this.travel = travel ?? throw new ArgumentNullException(nameof(travel));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
    }

    public void Tick()
    {
        travel.Tick(gameClock.DeltaTime);
    }
}

public sealed class OffenseDecisionContext
{
    public string expeditionId;
    public int sequence;
    public OffenseDecisionStage stage;
    public HashSet<string> tags = new HashSet<string>(StringComparer.Ordinal);
    public bool protectedMovement;
    public bool forceNonCombat;
    public bool canGenerateForcedCombat = true;
}

public readonly struct OffenseDecisionChoiceView
{
    public OffenseDecisionChoiceView(
        string choiceId,
        string label,
        string description,
        string directionLabel,
        int severity,
        bool transformed)
    {
        ChoiceId = choiceId ?? string.Empty;
        Label = label ?? string.Empty;
        Description = description ?? string.Empty;
        DirectionLabel = directionLabel ?? string.Empty;
        Severity = Mathf.Clamp(severity, 0, 3);
        Transformed = transformed;
    }

    public string ChoiceId { get; }
    public string Label { get; }
    public string Description { get; }
    public string DirectionLabel { get; }
    public int Severity { get; }
    public bool Transformed { get; }
}

public sealed class OffenseDecisionView
{
    public string cardId;
    public string title;
    public string situation;
    public OffenseDecisionStage stage;
    public IReadOnlyList<OffenseDecisionChoiceView> choices;
}

public interface IOffenseDecisionRuntime
{
    event Action Changed;
    bool TryCreateDecision(
        OffenseDecisionContext context,
        out OffenseDecisionView decision,
        out string reason);
    bool TryResolve(
        string expeditionId,
        string choiceId,
        out OffenseDecisionChoiceDefinition choice,
        out string reason);
    bool TryGetActiveChoice(
        string expeditionId,
        string choiceId,
        out OffenseDecisionChoiceDefinition choice,
        out int deterministicRoll,
        out string reason);
    bool TryGetActiveDecision(
        string expeditionId,
        out OffenseDecisionView decision);
    IReadOnlyList<OffenseDecisionStateData> Capture();
}

public sealed class OffenseDecisionRuntime : IOffenseDecisionRuntime
{
    private readonly IOffenseContentCatalog content;
    private readonly IOffenseReturnSafetyRuntime returnSafety;
    private Dictionary<string, OffenseDecisionStateData> active =
        new Dictionary<string, OffenseDecisionStateData>(StringComparer.Ordinal);

    public OffenseDecisionRuntime(
        IOffenseContentCatalog content,
        IOffenseReturnSafetyRuntime returnSafety)
    {
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.returnSafety = returnSafety
            ?? throw new ArgumentNullException(nameof(returnSafety));
    }

    public event Action Changed;

    public bool TryCreateDecision(
        OffenseDecisionContext context,
        out OffenseDecisionView decision,
        out string reason)
    {
        decision = null;
        if (context == null || string.IsNullOrWhiteSpace(context.expeditionId))
        {
            reason = "사건 생성 정보가 없습니다.";
            return false;
        }

        List<OffenseDecisionCardSO> candidates = content.DecisionCards
            .Where(card => IsValidCard(card)
                && card.stage == context.stage
                && HasRequiredWorldTags(card, context.tags)
                && (!context.forceNonCombat
                    && !returnSafety.MustUseNonCombatCard(context.expeditionId)
                    || card.choices.All(choice => !choice.mayStartCombat))
                && (context.canGenerateForcedCombat
                    || card.choices.All(choice => !choice.mayStartCombat)))
            .OrderBy(card => card.cardId, StringComparer.Ordinal)
            .ToList();
        if (candidates.Count == 0)
        {
            reason = "현재 조건에 맞는 2지선다 사건이 없습니다.";
            return false;
        }

        uint hash = OffenseTravelRuntime.DeterministicHash(
            context.expeditionId,
            context.sequence,
            (int)context.stage,
            candidates.Count);
        int index = (int)(hash % (uint)candidates.Count);
        OffenseDecisionCardSO selected = candidates[index];
        active[context.expeditionId] = new OffenseDecisionStateData
        {
            expeditionId = context.expeditionId,
            cardId = selected.cardId,
            sequence = context.sequence,
            stage = context.stage,
            deterministicRoll = unchecked((int)hash),
            resolved = false
        };
        decision = new OffenseDecisionView
        {
            cardId = selected.cardId,
            title = selected.title,
            situation = selected.situation,
            stage = selected.stage,
            choices = selected.choices
                .Select(choice => CreateChoiceView(choice, context.tags))
                .ToArray()
        };
        reason = string.Empty;
        Changed?.Invoke();
        return true;
    }

    public bool TryResolve(
        string expeditionId,
        string choiceId,
        out OffenseDecisionChoiceDefinition choice,
        out string reason)
    {
        choice = null;
        if (!active.TryGetValue(
                expeditionId ?? string.Empty,
                out OffenseDecisionStateData state)
            || state.resolved)
        {
            reason = "해결할 사건이 없습니다.";
            return false;
        }

        OffenseDecisionCardSO card = content.DecisionCards.FirstOrDefault(
            candidate => candidate != null && candidate.cardId == state.cardId);
        choice = card?.choices.FirstOrDefault(
            candidate => candidate != null && candidate.choiceId == choiceId);
        if (choice == null)
        {
            reason = "선택지를 찾을 수 없습니다.";
            return false;
        }

        state.resolved = true;
        state.selectedChoiceId = choice.choiceId;
        if (choice.mayStartCombat)
        {
            returnSafety.RecordProtectedDangerousEvent(
                expeditionId,
                forcedCombat: true);
        }
        else if (choice.severity >= 2)
        {
            returnSafety.RecordProtectedDangerousEvent(
                expeditionId,
                forcedCombat: false);
        }

        reason = string.Empty;
        Changed?.Invoke();
        return true;
    }

    public bool TryGetActiveChoice(
        string expeditionId,
        string choiceId,
        out OffenseDecisionChoiceDefinition choice,
        out int deterministicRoll,
        out string reason)
    {
        choice = null;
        deterministicRoll = 0;
        if (!active.TryGetValue(
                expeditionId ?? string.Empty,
                out OffenseDecisionStateData state)
            || state == null
            || state.resolved)
        {
            reason = "해결할 사건이 없습니다.";
            return false;
        }

        OffenseDecisionCardSO card = content.DecisionCards.FirstOrDefault(
            candidate => candidate != null
                && string.Equals(
                    candidate.cardId,
                    state.cardId,
                    StringComparison.Ordinal));
        choice = card?.choices.FirstOrDefault(
            candidate => candidate != null
                && string.Equals(
                    candidate.choiceId,
                    choiceId,
                    StringComparison.Ordinal));
        if (choice == null)
        {
            reason = "선택지를 찾을 수 없습니다.";
            return false;
        }

        deterministicRoll = state.deterministicRoll;
        reason = string.Empty;
        return true;
    }

    public bool TryGetActiveDecision(
        string expeditionId,
        out OffenseDecisionView decision)
    {
        decision = null;
        if (!active.TryGetValue(
                expeditionId ?? string.Empty,
                out OffenseDecisionStateData state)
            || state == null
            || state.resolved)
        {
            return false;
        }

        OffenseDecisionCardSO card = content.DecisionCards.FirstOrDefault(
            candidate => candidate != null
                && string.Equals(
                    candidate.cardId,
                    state.cardId,
                    StringComparison.Ordinal));
        if (card == null || card.choices == null || card.choices.Count != 2)
        {
            return false;
        }

        decision = new OffenseDecisionView
        {
            cardId = card.cardId,
            title = card.title,
            situation = card.situation,
            stage = state.stage,
            choices = card.choices.Select(choice =>
                new OffenseDecisionChoiceView(
                    choice.choiceId,
                    choice.label,
                    choice.description,
                    choice.directionLabel,
                    choice.severity,
                    transformed: false)).ToArray()
        };
        return true;
    }

    public IReadOnlyList<OffenseDecisionStateData> Capture()
    {
        return active.Values
            .OrderBy(state => state.expeditionId, StringComparer.Ordinal)
            .Select(Clone)
            .ToList();
    }

    internal OffenseDictionaryRestoreCandidate<OffenseDecisionStateData>
        PrepareRestore(IEnumerable<OffenseDecisionStateData> states)
    {
        Dictionary<string, OffenseDecisionStateData> candidate =
            new(StringComparer.Ordinal);
        foreach (OffenseDecisionStateData source in
                 states ?? throw new ArgumentNullException(nameof(states)))
        {
            if (source == null
                || string.IsNullOrWhiteSpace(source.expeditionId)
                || candidate.ContainsKey(source.expeditionId)
                || source.sequence < 0
                || !Enum.IsDefined(typeof(OffenseDecisionStage), source.stage)
                || !content.DecisionCards.Any(card =>
                    card != null && card.cardId == source.cardId)
                || source.resolved != !string.IsNullOrWhiteSpace(
                    source.selectedChoiceId))
            {
                throw new InvalidOperationException(
                    $"Invalid or duplicate offense decision '{source?.expeditionId ?? "null"}'.");
            }

            candidate.Add(source.expeditionId, Clone(source));
        }

        return new OffenseDictionaryRestoreCandidate<OffenseDecisionStateData>(
            candidate);
    }

    internal void PublishRestore(
        OffenseDictionaryRestoreCandidate<OffenseDecisionStateData> candidate)
    {
        active = (candidate ?? throw new ArgumentNullException(nameof(candidate)))
            .Values;
        Changed?.Invoke();
    }

    private static bool IsValidCard(OffenseDecisionCardSO card)
    {
        return card != null
            && !string.IsNullOrWhiteSpace(card.cardId)
            && card.choices != null
            && card.choices.Count == 2
            && card.choices.All(choice =>
                choice != null && !string.IsNullOrWhiteSpace(choice.choiceId))
            && card.choices.Select(choice => choice.choiceId).Distinct().Count() == 2;
    }

    private static bool HasRequiredWorldTags(
        OffenseDecisionCardSO card,
        ISet<string> tags)
    {
        return card.requiredWorldTags == null
            || card.requiredWorldTags.All(tag =>
                string.IsNullOrWhiteSpace(tag) || tags.Contains(tag));
    }

    private static OffenseDecisionChoiceView CreateChoiceView(
        OffenseDecisionChoiceDefinition choice,
        ISet<string> tags)
    {
        bool transformed = !string.IsNullOrWhiteSpace(choice.requiredTag)
            && tags.Contains(choice.requiredTag);
        return new OffenseDecisionChoiceView(
            choice.choiceId,
            transformed && !string.IsNullOrWhiteSpace(choice.transformedLabel)
                ? choice.transformedLabel
                : choice.label,
            transformed && !string.IsNullOrWhiteSpace(choice.transformedDescription)
                ? choice.transformedDescription
                : choice.description,
            choice.directionLabel,
            choice.severity,
            transformed);
    }

    private static OffenseDecisionStateData Clone(OffenseDecisionStateData source)
    {
        return new OffenseDecisionStateData
        {
            expeditionId = source.expeditionId,
            cardId = source.cardId,
            sequence = source.sequence,
            stage = source.stage,
            deterministicRoll = source.deterministicRoll,
            resolved = source.resolved,
            selectedChoiceId = source.selectedChoiceId
        };
    }
}
