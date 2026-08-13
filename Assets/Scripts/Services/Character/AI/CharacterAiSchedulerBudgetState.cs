using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

internal struct CharacterAiSchedulerBudgetSettings
{
    public bool AdaptBudgetsToFrameCost;
    public int MaxDecisionsPerFrame;
    public int MaxPathSearchesPerFrame;
    public int MinDecisionsPerFrame;
    public int MinPathSearchesPerFrame;
    public float TargetAiMilliseconds;
    public float TargetFrameMilliseconds;
    public float FrameHeadroomShare;
    public float BaselineBudgetRatio;
    public float MinimumUsefulSliceMilliseconds;
}

internal sealed class CharacterAiSchedulerBudgetState
{
    private readonly Dictionary<CharacterActor, double> actorDecisionCostMilliseconds =
        new Dictionary<CharacterActor, double>();
    private int pathBudgetFrame = -1;
    private int pathSearchesThisFrame;
    private int currentDecisionBudget;
    private int currentPathSearchBudget;
    private double estimatedDecisionMilliseconds;
    private double estimatedPathSearchMilliseconds;
    private double currentFrameBudgetMilliseconds;
    private double smoothedFrameMilliseconds;
    private float maximumObservedDecisionDeferralSeconds;

    public int CurrentDecisionBudget => Mathf.Max(0, currentDecisionBudget);
    public int CurrentPathSearchBudget => Mathf.Max(0, currentPathSearchBudget);
    public int LastPathSearchCount { get; private set; }
    public int LastBrokerPathSearchCount { get; private set; }
    public int LastBrokerUnboundedPathSearchCount { get; private set; }
    public int LastBrokerPathCacheHitCount { get; private set; }
    public int LastBrokerPathBudgetDeferralCount { get; private set; }
    public double CurrentFrameBudgetMilliseconds => currentFrameBudgetMilliseconds;
    public double EstimatedDecisionMilliseconds => estimatedDecisionMilliseconds;
    public double EstimatedPathSearchMilliseconds => estimatedPathSearchMilliseconds;
    public double SmoothedFrameMilliseconds => smoothedFrameMilliseconds;
    public float LastOldestDecisionDeferralSeconds { get; private set; }
    public float MaximumObservedDecisionDeferralSeconds =>
        maximumObservedDecisionDeferralSeconds;

    public void Clear(CharacterAiSchedulerBudgetSettings settings, int actorCount)
    {
        actorDecisionCostMilliseconds.Clear();
        maximumObservedDecisionDeferralSeconds = 0f;
        LastOldestDecisionDeferralSeconds = 0f;
        Reset(settings, actorCount);
        ResetPathWindow();
    }

    public void RemoveActor(CharacterActor actor)
    {
        if (actor != null)
        {
            actorDecisionCostMilliseconds.Remove(actor);
        }
    }

    public void ResetPathWindowForDebug(
        CharacterAiSchedulerBudgetSettings settings,
        int actorCount)
    {
        ResetPathWindow();
        EnsureInitialized(settings, actorCount);
    }

    public void ResetLastTickCounters()
    {
        LastPathSearchCount = 0;
        LastBrokerPathSearchCount = 0;
        LastBrokerUnboundedPathSearchCount = 0;
        LastBrokerPathCacheHitCount = 0;
        LastBrokerPathBudgetDeferralCount = 0;
    }

    public double GetDecisionWorkSliceMilliseconds(
        CharacterActor actor,
        CharacterAiSchedulerBudgetSettings settings,
        int actorCount)
    {
        EnsureInitialized(settings, actorCount);
        double frameShare = currentFrameBudgetMilliseconds * 0.18;
        double predictedCost = GetPredictedDecisionCost(actor, settings);
        double adaptiveSlice = Math.Min(predictedCost * 0.35, frameShare);
        return Math.Clamp(
            adaptiveSlice,
            settings.MinimumUsefulSliceMilliseconds,
            Math.Max(settings.MinimumUsefulSliceMilliseconds, 0.65));
    }

    public void BeginTick(
        CharacterAiSchedulerBudgetSettings settings,
        int actorCount,
        IUiClock uiClock,
        IDynamicFrameWorkBudget frameWorkBudget,
        int scheduledDecisionCount,
        bool oldestDecisionIsStarved,
        double lastProcessingMilliseconds)
    {
        UpdateFrameTimeBudget(
            settings,
            actorCount,
            uiClock,
            lastProcessingMilliseconds);
        frameWorkBudget.SetBacklog(
            DynamicFrameWorkDomain.AiDecision,
            scheduledDecisionCount);
        currentFrameBudgetMilliseconds = Math.Min(
            currentFrameBudgetMilliseconds,
            frameWorkBudget.GetSliceMilliseconds(
                DynamicFrameWorkDomain.AiDecision,
                settings.MinimumUsefulSliceMilliseconds,
                settings.TargetAiMilliseconds,
                oldestDecisionIsStarved));
    }

    public void BeginPathBudgetWindow(
        CharacterAiSchedulerBudgetSettings settings,
        int actorCount,
        int frameCount,
        bool limitPathSearches,
        IGridPathSearchBroker pathSearchBroker)
    {
        EnsureInitialized(settings, actorCount);
        pathSearchBroker.BeginFrame(
            GetPathSearchBudgetForFrame(settings, actorCount),
            limitPathSearches,
            currentFrameBudgetMilliseconds * 0.3);
        pathBudgetFrame = frameCount;
        pathSearchesThisFrame = 0;
        LastPathSearchCount = 0;
        LastBrokerPathSearchCount = 0;
        LastBrokerUnboundedPathSearchCount = 0;
        LastBrokerPathCacheHitCount = 0;
        LastBrokerPathBudgetDeferralCount = 0;
    }

    public double AdvanceIncrementalWorldIndex(
        IFacilityCandidateCache facilityCandidateCache,
        IDynamicFrameWorkBudget frameWorkBudget,
        CharacterAiSchedulerBudgetSettings settings)
    {
        if (facilityCandidateCache?.HasPendingIndexBuild != true)
        {
            frameWorkBudget.SetBacklog(DynamicFrameWorkDomain.WorldIndex, 0);
            return 0.0;
        }

        frameWorkBudget.SetBacklog(DynamicFrameWorkDomain.WorldIndex, 1);
        double indexBudgetMilliseconds = frameWorkBudget.GetSliceMilliseconds(
            DynamicFrameWorkDomain.WorldIndex,
            settings.MinimumUsefulSliceMilliseconds,
            Math.Max(
                settings.MinimumUsefulSliceMilliseconds,
                currentFrameBudgetMilliseconds * 0.25));
        if (indexBudgetMilliseconds < settings.MinimumUsefulSliceMilliseconds)
        {
            return 0.0;
        }

        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        facilityCandidateCache.AdvanceIndex(indexBudgetMilliseconds);
        double elapsedMilliseconds =
            (System.Diagnostics.Stopwatch.GetTimestamp() - started)
            * 1000.0
            / System.Diagnostics.Stopwatch.Frequency;
        frameWorkBudget.ReportConsumed(
            DynamicFrameWorkDomain.WorldIndex,
            elapsedMilliseconds);
        return elapsedMilliseconds;
    }

    public bool TryConsumePathSearchBudget(
        CharacterAiSchedulerBudgetSettings settings,
        int actorCount,
        int frameCount)
    {
        ResetPathBudgetIfNeeded(frameCount);
        if (pathSearchesThisFrame >= GetPathSearchBudgetForFrame(settings, actorCount))
        {
            return false;
        }

        pathSearchesThisFrame++;
        return true;
    }

    public int GetDecisionBudgetForFrame(
        CharacterAiSchedulerBudgetSettings settings,
        int actorCount)
    {
        EnsureInitialized(settings, actorCount);
        return Mathf.Clamp(
            currentDecisionBudget,
            Mathf.Max(0, settings.MinDecisionsPerFrame),
            ResolveDecisionSafetyLimit(settings, actorCount));
    }

    public double GetPredictedDecisionCost(
        CharacterActor actor,
        CharacterAiSchedulerBudgetSettings settings)
    {
        if (actor != null
            && actorDecisionCostMilliseconds.TryGetValue(actor, out double actorEstimate)
            && actorEstimate > 0.0)
        {
            return Math.Max(
                settings.MinimumUsefulSliceMilliseconds,
                actorEstimate);
        }

        return Math.Max(
            settings.MinimumUsefulSliceMilliseconds,
            estimatedDecisionMilliseconds);
    }

    public void RecordDecisionCost(
        CharacterActor actor,
        double elapsedMilliseconds,
        CharacterAiSchedulerBudgetSettings settings)
    {
        if (elapsedMilliseconds <= 0.0)
        {
            return;
        }

        double cappedSample = Math.Min(
            elapsedMilliseconds,
            Math.Max(
                settings.TargetAiMilliseconds,
                estimatedDecisionMilliseconds * 4.0));
        if (estimatedDecisionMilliseconds <= 0.0)
        {
            estimatedDecisionMilliseconds = cappedSample;
            return;
        }

        const double recentSampleWeight = 0.2;
        estimatedDecisionMilliseconds +=
            (cappedSample - estimatedDecisionMilliseconds)
            * recentSampleWeight;

        if (actor == null)
        {
            return;
        }

        if (!actorDecisionCostMilliseconds.TryGetValue(actor, out double actorEstimate)
            || actorEstimate <= 0.0)
        {
            actorDecisionCostMilliseconds[actor] = cappedSample;
            return;
        }

        actorDecisionCostMilliseconds[actor] =
            actorEstimate
            + (cappedSample - actorEstimate)
            * recentSampleWeight;
    }

    public void UpdateBacklogTelemetry(
        CharacterAiDecisionSchedule decisionSchedule,
        float now)
    {
        if (!decisionSchedule.TryPeekDue(
                now,
                out CharacterAiScheduledDecision scheduled))
        {
            LastOldestDecisionDeferralSeconds = 0f;
            return;
        }

        LastOldestDecisionDeferralSeconds = Mathf.Max(
            0f,
            now - scheduled.DueTime);
        maximumObservedDecisionDeferralSeconds = Mathf.Max(
            maximumObservedDecisionDeferralSeconds,
            LastOldestDecisionDeferralSeconds);
    }

    public void CapturePathResults(
        IGridPathSearchBroker pathSearchBroker)
    {
        LastPathSearchCount = pathSearchesThisFrame;
        LastBrokerPathSearchCount = pathSearchBroker.SearchesThisFrame;
        LastBrokerUnboundedPathSearchCount = pathSearchBroker.UnboundedSearchesThisFrame;
        LastBrokerPathCacheHitCount = pathSearchBroker.CacheHitsThisFrame;
        LastBrokerPathBudgetDeferralCount = pathSearchBroker.BudgetDeferralsThisFrame;
    }

    public void RecordPathSearchCost(
        IGridPathSearchBroker pathSearchBroker,
        CharacterAiSchedulerBudgetSettings settings)
    {
        UpdatePathSearchCostEstimate(pathSearchBroker, settings);
    }

    private void EnsureInitialized(
        CharacterAiSchedulerBudgetSettings settings,
        int actorCount)
    {
        if (estimatedDecisionMilliseconds <= 0.0
            || estimatedPathSearchMilliseconds <= 0.0)
        {
            Reset(settings, actorCount);
        }
    }

    private void Reset(
        CharacterAiSchedulerBudgetSettings settings,
        int actorCount)
    {
        estimatedDecisionMilliseconds = 0.25;
        estimatedPathSearchMilliseconds = 0.35;
        currentFrameBudgetMilliseconds = Mathf.Max(
            settings.MinimumUsefulSliceMilliseconds,
            settings.TargetAiMilliseconds * settings.BaselineBudgetRatio);
        smoothedFrameMilliseconds = settings.TargetFrameMilliseconds;
        RecalculateWorkUnitBudgets(settings, actorCount);
    }

    private void ResetPathWindow()
    {
        pathBudgetFrame = -1;
        pathSearchesThisFrame = 0;
        LastPathSearchCount = 0;
        LastBrokerPathSearchCount = 0;
        LastBrokerUnboundedPathSearchCount = 0;
        LastBrokerPathCacheHitCount = 0;
        LastBrokerPathBudgetDeferralCount = 0;
    }

    private void ResetPathBudgetIfNeeded(int frameCount)
    {
        if (pathBudgetFrame == frameCount)
        {
            return;
        }

        pathBudgetFrame = frameCount;
        pathSearchesThisFrame = 0;
    }

    public int GetPathSearchBudgetForFrame(
        CharacterAiSchedulerBudgetSettings settings,
        int actorCount)
    {
        EnsureInitialized(settings, actorCount);
        int authoredMaximum = Mathf.Clamp(
            settings.MaxPathSearchesPerFrame,
            1,
            4096);
        int forwardProgressFloor = Mathf.Min(
            Mathf.Max(0, actorCount),
            authoredMaximum);
        int minimum = Mathf.Max(
            Mathf.Max(0, settings.MinPathSearchesPerFrame),
            forwardProgressFloor);
        return Mathf.Clamp(
            currentPathSearchBudget,
            minimum,
            authoredMaximum);
    }

    private static int ResolveDecisionSafetyLimit(
        CharacterAiSchedulerBudgetSettings settings,
        int actorCount)
    {
        // MaxDecisionsPerFrame is an authored hard ceiling. Actor count affects
        // backlog and deferral telemetry, but must never silently widen the
        // per-frame decision budget at settlement scale.
        return Mathf.Clamp(settings.MaxDecisionsPerFrame, 1, 4096);
    }

    private static int ResolvePathSearchSafetyLimit(
        CharacterAiSchedulerBudgetSettings settings,
        int actorCount)
    {
        return Mathf.Clamp(settings.MaxPathSearchesPerFrame, 1, 4096);
    }

    private void UpdateFrameTimeBudget(
        CharacterAiSchedulerBudgetSettings settings,
        int actorCount,
        IUiClock uiClock,
        double lastProcessingMilliseconds)
    {
        EnsureInitialized(settings, actorCount);
        if (!settings.AdaptBudgetsToFrameCost)
        {
            currentFrameBudgetMilliseconds = settings.TargetAiMilliseconds;
            RecalculateWorkUnitBudgets(settings, actorCount);
            return;
        }

        double observedFrameMilliseconds = uiClock != null
            ? Math.Max(0.0, uiClock.DeltaTime * 1000.0)
            : settings.TargetFrameMilliseconds;
        if (smoothedFrameMilliseconds <= 0.0)
        {
            smoothedFrameMilliseconds = observedFrameMilliseconds;
        }
        else
        {
            const double frameSampleWeight = 0.12;
            smoothedFrameMilliseconds +=
                (observedFrameMilliseconds - smoothedFrameMilliseconds)
                * frameSampleWeight;
        }

        double headroomMilliseconds = settings.TargetFrameMilliseconds
            - smoothedFrameMilliseconds;
        double baselineMilliseconds =
            settings.TargetAiMilliseconds * settings.BaselineBudgetRatio;
        if (headroomMilliseconds < 0.0)
        {
            double overrunRatio = Math.Min(
                1.0,
                -headroomMilliseconds
                / Math.Max(1.0, settings.TargetFrameMilliseconds * 0.5));
            baselineMilliseconds *= 1.0 - overrunRatio;
        }

        double desiredBudgetMilliseconds = baselineMilliseconds
            + Math.Max(0.0, headroomMilliseconds) * settings.FrameHeadroomShare;
        if (lastProcessingMilliseconds > settings.TargetAiMilliseconds)
        {
            desiredBudgetMilliseconds *= Math.Max(
                0.2,
                settings.TargetAiMilliseconds
                / Math.Max(settings.TargetAiMilliseconds, lastProcessingMilliseconds));
        }

        desiredBudgetMilliseconds = Math.Clamp(
            desiredBudgetMilliseconds,
            0.0,
            settings.TargetAiMilliseconds);
        double adjustmentWeight = desiredBudgetMilliseconds
            < currentFrameBudgetMilliseconds
                ? 0.45
                : 0.16;
        currentFrameBudgetMilliseconds +=
            (desiredBudgetMilliseconds - currentFrameBudgetMilliseconds)
            * adjustmentWeight;
        if (currentFrameBudgetMilliseconds < settings.MinimumUsefulSliceMilliseconds)
        {
            currentFrameBudgetMilliseconds = 0.0;
        }

        RecalculateWorkUnitBudgets(settings, actorCount);
    }

    private void RecalculateWorkUnitBudgets(
        CharacterAiSchedulerBudgetSettings settings,
        int actorCount)
    {
        double usableBudgetMilliseconds = Math.Max(
            0.0,
            currentFrameBudgetMilliseconds);
        currentDecisionBudget = usableBudgetMilliseconds
            < settings.MinimumUsefulSliceMilliseconds
                ? 0
                : Mathf.Clamp(
                    (int)Math.Floor(
                        usableBudgetMilliseconds
                        / Math.Max(
                            settings.MinimumUsefulSliceMilliseconds,
                            estimatedDecisionMilliseconds)),
                    Mathf.Max(0, settings.MinDecisionsPerFrame),
                    ResolveDecisionSafetyLimit(settings, actorCount));

        double pathBudgetMilliseconds = usableBudgetMilliseconds * 0.3;
        currentPathSearchBudget = pathBudgetMilliseconds
            < settings.MinimumUsefulSliceMilliseconds
                ? 0
                : Mathf.Clamp(
                    (int)Math.Floor(
                        pathBudgetMilliseconds
                        / Math.Max(
                            settings.MinimumUsefulSliceMilliseconds,
                            estimatedPathSearchMilliseconds)),
                    Mathf.Max(0, settings.MinPathSearchesPerFrame),
                    ResolvePathSearchSafetyLimit(settings, actorCount));
    }

    private void UpdatePathSearchCostEstimate(
        IGridPathSearchBroker pathSearchBroker,
        CharacterAiSchedulerBudgetSettings settings)
    {
        if (pathSearchBroker.SearchesThisFrame <= 0)
        {
            return;
        }

        double rawSample = pathSearchBroker.SearchMillisecondsThisFrame
            / pathSearchBroker.SearchesThisFrame;
        if (rawSample <= 0.0)
        {
            return;
        }

        double sample = Math.Min(
            rawSample,
            Math.Max(
                settings.TargetAiMilliseconds * 0.5,
                estimatedPathSearchMilliseconds * 4.0));
        const double recentSampleWeight = 0.2;
        estimatedPathSearchMilliseconds +=
            (sample - estimatedPathSearchMilliseconds)
            * recentSampleWeight;
    }
}
