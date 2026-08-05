using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

internal sealed class GameplayPerformanceWorldSummaryCollector
{
    private const int VisibleStressActorCount = 96;

    private readonly GameplayPerformanceOptions options;
    private readonly GameplayPerformanceReport report;

    public GameplayPerformanceWorldSummaryCollector(
        GameplayPerformanceOptions options,
        GameplayPerformanceReport report)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.report = report ?? throw new ArgumentNullException(nameof(report));
    }

public void Capture(
    int warningCount,
    int errorCount,
    IReadOnlyList<string> capturedMessages)
{
    Scene scene = SceneManager.GetActiveScene();
    CharacterActor[] actors = FindSceneComponents<CharacterActor>(scene);
    BuildableObject[] buildings = FindSceneComponents<BuildableObject>(scene);
    Renderer[] renderers = FindSceneComponents<Renderer>(scene);
    Canvas[] canvases = FindSceneComponents<Canvas>(scene);
    WorldCharacterNameplate[] nameplates =
        FindSceneComponents<WorldCharacterNameplate>(scene);
    GridSystemManager gridSystem = FindSceneComponent<GridSystemManager>(scene);
    CharacterAiScheduler scheduler = FindSceneComponent<CharacterAiScheduler>(scene);
    DungeonRuntimeLifetimeScope scope =
        FindSceneComponent<DungeonRuntimeLifetimeScope>(scene);

    report.actualActorCount = actors.Count(actor =>
        actor != null && actor.gameObject.activeInHierarchy);
    report.actualBuildingCount = buildings.Count(building =>
        building != null && !building.isDestroy && building.gameObject.activeInHierarchy);
    report.activeRendererCount = renderers.Count(renderer =>
        renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy);
    report.visibleRendererCount = renderers.Count(renderer =>
        renderer != null
        && renderer.enabled
        && renderer.gameObject.activeInHierarchy
        && renderer.isVisible);
    report.activeCanvasCount = canvases.Count(canvas =>
        canvas != null && canvas.enabled && canvas.gameObject.activeInHierarchy);
    report.activeNameplateCount = nameplates.Count(nameplate =>
        nameplate != null && nameplate.gameObject.activeInHierarchy);
    if (scope?.Container != null)
    {
        DynamicFrameWorkSnapshot workSnapshot = scope.Container
            .Resolve<IDynamicFrameWorkBudget>()
            .GetSnapshot();
        report.dynamicWorkSmoothedFrameMilliseconds =
            workSnapshot.SmoothedFrameMilliseconds;
        report.dynamicWorkAvailableMilliseconds =
            workSnapshot.AvailableMilliseconds;
        report.dynamicWorkConsumedMilliseconds =
            workSnapshot.ConsumedMilliseconds;
        report.dynamicWorkBacklog = workSnapshot.TotalBacklog;
    }
    report.gridWidth = gridSystem?.grid?.width ?? 0;
    report.gridHeight = gridSystem?.grid?.height ?? 0;
    report.schedulerRegisteredCharacters =
        scheduler != null ? scheduler.RegisteredCharacterCount : 0;
    report.schedulerLastMilliseconds =
        scheduler != null ? scheduler.LastProcessingMilliseconds : 0d;
    report.schedulerLastDecisions =
        scheduler != null ? scheduler.LastProcessedDecisionCount : 0;
    report.schedulerLastLegacyFallbacks =
        scheduler != null ? scheduler.LastLegacyFallbackCount : 0;
    report.schedulerLastPathSearches =
        scheduler != null ? scheduler.LastPathSearchCount : 0;
    report.schedulerCurrentBudgetMilliseconds =
        scheduler != null ? scheduler.CurrentFrameBudgetMilliseconds : 0d;
    report.schedulerEstimatedDecisionMilliseconds =
        scheduler != null ? scheduler.EstimatedDecisionMilliseconds : 0d;
    report.schedulerEstimatedPathMilliseconds =
        scheduler != null ? scheduler.EstimatedPathSearchMilliseconds : 0d;
    report.schedulerSmoothedFrameMilliseconds =
        scheduler != null ? scheduler.SmoothedFrameMilliseconds : 0d;
    report.schedulerProcessedDecisions =
        scheduler != null ? scheduler.CumulativeProcessedDecisionCount : 0L;
    report.schedulerStarvedDecisions =
        scheduler != null ? scheduler.CumulativeStarvedDecisionCount : 0L;
    report.schedulerSkippedDecisions =
        scheduler != null ? scheduler.CumulativeSkippedDecisionCount : 0L;
    report.schedulerLegacyFallbacks =
        scheduler != null ? scheduler.CumulativeLegacyFallbackCount : 0L;
    report.schedulerOldestDeferralSeconds =
        scheduler != null ? scheduler.LastOldestDecisionDeferralSeconds : 0f;
    report.schedulerMaximumDeferralSeconds =
        scheduler != null ? scheduler.MaximumObservedDecisionDeferralSeconds : 0f;
    report.schedulerBudgetExhausted =
        scheduler != null && scheduler.LastBudgetExhausted;
    if (scope != null && scope.Container != null)
    {
        IFacilityCandidateCache facilityCache =
            scope.Container.Resolve<IFacilityCandidateCache>();
        report.facilityCandidateIndexPending =
            facilityCache.HasPendingIndexBuild;
        report.facilityCandidateIndexVersion =
            facilityCache.CandidateIndexVersion;
        report.aiPerformance = CreateAiPerformanceSnapshot(
            scope.Container
                .Resolve<ICharacterAiPerformanceRecorder>()
                .CaptureReport(report.schedulerRegisteredCharacters));
        ICharacterPresentationScheduler presentationScheduler =
            scope.Container.Resolve<ICharacterPresentationScheduler>();
        report.presentationRegisteredCharacters =
            presentationScheduler.RegisteredCount;
        report.presentationVisibleCharacters =
            presentationScheduler.VisibleCount;
        report.actualWildlifeCount = scope.Container
            .Resolve<IWildlifeRuntime>()
            .Wildlife
            .Count(actor => actor != null && actor.IsAlive);
        report.actualLivestockCount = scope.Container
            .Resolve<IAnimalHusbandryQuery>()
            .Animals
            .Count;
        CaptureDeprivationSummary(scope, actors);
    }
    report.warningCount = warningCount;
    report.errorCount = errorCount;
    report.logMessages = capturedMessages?.ToArray() ?? Array.Empty<string>();
}

private void CaptureDeprivationSummary(
    DungeonRuntimeLifetimeScope scope,
    IReadOnlyList<CharacterActor> actors)
{
    ICharacterDeprivationQuery deprivationRuntime =
        scope.Container.Resolve<ICharacterDeprivationQuery>();
    IWorldItemStackRuntime itemRuntime =
        scope.Container.Resolve<IWorldItemStackRuntime>();
    var waterCandidates = new List<WorldItemStockCandidate>();
    itemRuntime.CopyAvailableStockCandidates(
        StockCategory.Water,
        waterCandidates);

    float totalThirst = 0f;
    int actorCount = 0;
    report.minimumThirst = 100f;
    report.maximumThirst = 0f;
    report.actorsBelowSafeDrinkThreshold = 0;
    report.actorsWithCriticalThirst = 0;
    report.actorsWithThirstWarningBurden = 0;
    report.actorsWithThirstBreakdownBurden = 0;
    report.activeDeprivationBreakdowns = 0;
    report.activeDesperateDrinkBreakdowns = 0;
    CharacterDeprivationDiagnosticsSnapshot deprivationDiagnostics =
        deprivationRuntime.GetDiagnostics();
    report.safeReliefRequests =
        deprivationDiagnostics.SafeReliefRequests;
    report.safeReliefPlanFailures =
        deprivationDiagnostics.SafeReliefPlanFailures;
    report.safeReliefActionsStarted =
        deprivationDiagnostics.SafeReliefActionsStarted;
    report.safeReliefStoredStackPlans =
        deprivationDiagnostics.SafeReliefStoredStackPlans;
    report.safeReliefMoveFailures =
        deprivationDiagnostics.SafeReliefMoveFailures;
    report.safeReliefBreakdownMoveFailures =
        deprivationDiagnostics.SafeReliefBreakdownMoveFailures;
    report.safeReliefBlockedMoveFailures =
        deprivationDiagnostics.SafeReliefBlockedMoveFailures;
    report.safeReliefOtherMoveFailures =
        deprivationDiagnostics.SafeReliefOtherMoveFailures;
    report.safeReliefStaleStartFailures =
        deprivationDiagnostics.SafeReliefStaleStartFailures;
    report.safeReliefWallBlockedFailures =
        deprivationDiagnostics.SafeReliefWallBlockedFailures;
    report.safeReliefDoorDeniedFailures =
        deprivationDiagnostics.SafeReliefDoorDeniedFailures;
    report.safeReliefDefenseReservationFailures =
        deprivationDiagnostics.SafeReliefDefenseReservationFailures;
    report.safeReliefTraversalChangedFailures =
        deprivationDiagnostics.SafeReliefTraversalChangedFailures;
    report.safeReliefArrivals =
        deprivationDiagnostics.SafeReliefArrivals;
    report.safeReliefInteractionAttempts =
        deprivationDiagnostics.SafeReliefInteractionAttempts;
    report.safeReliefSuccesses =
        deprivationDiagnostics.SafeReliefSuccesses;
    report.safeReliefRunningActions =
        deprivationDiagnostics.SafeReliefRunningActions;
    report.safeReliefActionsFinished =
        deprivationDiagnostics.SafeReliefActionsFinished;
    report.safeReliefPlannedPathSteps =
        deprivationDiagnostics.SafeReliefPlannedPathSteps;
    report.safeReliefAveragePlannedPathSteps =
        deprivationDiagnostics.SafeReliefActionsStarted > 0
            ? (float)deprivationDiagnostics.SafeReliefPlannedPathSteps
                / deprivationDiagnostics.SafeReliefActionsStarted
            : 0f;
    report.safeReliefMaximumPlannedPathSteps =
        deprivationDiagnostics.SafeReliefMaximumPlannedPathSteps;
    report.safeReliefAverageDurationSeconds =
        deprivationDiagnostics.SafeReliefActionsFinished > 0
            ? deprivationDiagnostics.SafeReliefCompletedDurationSeconds
                / deprivationDiagnostics.SafeReliefActionsFinished
            : 0f;
    report.safeReliefMaximumDurationSeconds =
        deprivationDiagnostics.SafeReliefMaximumDurationSeconds;
    report.safeReliefCancelledMoveFailures =
        deprivationDiagnostics.SafeReliefCancelledMoveFailures;
    report.safeReliefMissingPathFailures =
        deprivationDiagnostics.SafeReliefMissingPathFailures;
    report.safeReliefMissingMovementHandlerFailures =
        deprivationDiagnostics.SafeReliefMissingMovementHandlerFailures;
    report.safeReliefGridUnavailableFailures =
        deprivationDiagnostics.SafeReliefGridUnavailableFailures;
    report.safeReliefInvalidSpeedFailures =
        deprivationDiagnostics.SafeReliefInvalidSpeedFailures;
    report.safeReliefNoFailureReasonFailures =
        deprivationDiagnostics.SafeReliefNoFailureReasonFailures;
    report.safeReliefActorDeadMoveFailures =
        deprivationDiagnostics.SafeReliefActorDeadMoveFailures;
    report.safeReliefActorMissingMoveFailures =
        deprivationDiagnostics.SafeReliefActorMissingMoveFailures;
    report.safeReliefCrossFloorTargetPlans =
        deprivationDiagnostics.SafeReliefCrossFloorTargetPlans;
    report.safeReliefPathsWithVerticalTraversal =
        deprivationDiagnostics.SafeReliefPathsWithVerticalTraversal;
    report.safeReliefVerticalTraversalSteps =
        deprivationDiagnostics.SafeReliefVerticalTraversalSteps;
    report.desperateDrinkAttempts =
        deprivationDiagnostics.DesperateDrinkAttempts;
    report.desperateDrinkStackMoveFailures =
        deprivationDiagnostics.DesperateDrinkStackMoveFailures;
    report.desperateDrinkStackArrivals =
        deprivationDiagnostics.DesperateDrinkStackArrivals;
    report.desperateDrinkStackConsumptions =
        deprivationDiagnostics.DesperateDrinkStackConsumptions;
    report.waterStockCandidateCount = waterCandidates.Count;
    report.storedWaterCandidateCount = 0;
    report.looseWaterCandidateCount = 0;
    report.storedWaterQuantity = 0;
    report.looseWaterQuantity = 0;
    report.availableWaterQuantity = 0;
    report.waterCandidateCountByFloor =
        new int[Mathf.Max(1, report.gridHeight)];
    report.waterQuantityByFloor =
        new int[report.waterCandidateCountByFloor.Length];
    for (int index = 0; index < waterCandidates.Count; index++)
    {
        WorldItemStockCandidate candidate = waterCandidates[index];
        int quantity = Mathf.Max(0, candidate.Quantity);
        report.availableWaterQuantity += quantity;
        if (candidate.Position.y >= 0
            && candidate.Position.y
                < report.waterCandidateCountByFloor.Length)
        {
            report.waterCandidateCountByFloor[candidate.Position.y]++;
            report.waterQuantityByFloor[candidate.Position.y] += quantity;
        }
        if (candidate.State == WorldItemStackState.Stored)
        {
            report.storedWaterCandidateCount++;
            report.storedWaterQuantity += quantity;
        }
        else if (candidate.State == WorldItemStackState.Loose)
        {
            report.looseWaterCandidateCount++;
            report.looseWaterQuantity += quantity;
        }
    }

    for (int index = 0; index < actors.Count; index++)
    {
        CharacterActor actor = actors[index];
        if (actor != null
            && actor.gameObject.activeInHierarchy
            && actor.IsDead)
        {
            report.deadActorCount++;
        }
        if (actor != null && actor.IsOwner)
        {
            report.ownerPresent = true;
            report.ownerAlive = !actor.IsDead;
        }
        if (actor == null
            || actor.IsDead
            || !actor.gameObject.activeInHierarchy
            || actor.Stats == null
            || !actor.Stats.TryGetConditionValue(
                CharacterCondition.THIRST,
                out float thirst))
        {
            continue;
        }

        actorCount++;
        totalThirst += thirst;
        report.minimumThirst = Mathf.Min(report.minimumThirst, thirst);
        report.maximumThirst = Mathf.Max(report.maximumThirst, thirst);
        if (thirst < 65f)
        {
            report.actorsBelowSafeDrinkThreshold++;
        }
        if (thirst < 20f)
        {
            report.actorsWithCriticalThirst++;
        }

        if (!deprivationRuntime.TryGetSnapshot(
                actor,
                out CharacterDeprivationSnapshot snapshot))
        {
            continue;
        }

        if (snapshot.Burdens != null
            && snapshot.Burdens.TryGetValue(
                DeprivationKind.Thirst,
                out float burden))
        {
            if (burden >= 40f)
            {
                report.actorsWithThirstWarningBurden++;
            }
            if (burden >= 70f)
            {
                report.actorsWithThirstBreakdownBurden++;
            }
        }

        if (snapshot.Breakdown?.active == true)
        {
            report.activeDeprivationBreakdowns++;
            if (snapshot.Breakdown.kind ==
                CharacterBreakdownKind.DesperateDrink)
            {
                report.activeDesperateDrinkBreakdowns++;
            }
        }
    }

    report.averageThirst = actorCount > 0
        ? totalThirst / actorCount
        : 0f;
    if (actorCount == 0)
    {
        report.minimumThirst = 0f;
    }
}

    private static GameplayCharacterAiPerformanceReport CreateAiPerformanceSnapshot(
        CharacterAiPerformanceReport source)
    {
        if (source == null)
        {
            return null;
        }

        return new GameplayCharacterAiPerformanceReport
        {
            valid = source.valid,
            actorCount = source.actorCount,
            sampleFrames = source.sampleFrames,
            scheduler = CreateAiPerformanceMetricSnapshot(source.scheduler),
            behaviorTree = CreateAiPerformanceMetricSnapshot(source.behaviorTree),
            pathBroker = CreateAiPerformanceMetricSnapshot(source.pathBroker),
            garbageCollection = CreateAiPerformanceMetricSnapshot(
                source.garbageCollection),
            metrics = source.metrics != null
                ? source.metrics
                    .Select(CreateAiPerformanceMetricSnapshot)
                    .ToList()
                : null,
            brokerSearches = source.brokerSearches,
            brokerCacheHits = source.brokerCacheHits,
            brokerBudgetDeferrals = source.brokerBudgetDeferrals,
            summary = source.summary
        };
    }

    private static GameplayCharacterAiPerformanceMetric
        CreateAiPerformanceMetricSnapshot(CharacterAiPerformanceMetric source)
    {
        return source == null
            ? null
            : new GameplayCharacterAiPerformanceMetric
            {
                name = source.name,
                sampleCount = source.sampleCount,
                average = source.average,
                p95 = source.p95,
                max = source.max,
                gcBytes = source.gcBytes
            };
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        T[] components = FindSceneComponents<T>(scene);
        return components.FirstOrDefault(component => component != null);
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid())
        {
            return Array.Empty<T>();
        }

        List<T> result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            result.AddRange(root.GetComponentsInChildren<T>(true));
        }

        return result.ToArray();
    }
}
