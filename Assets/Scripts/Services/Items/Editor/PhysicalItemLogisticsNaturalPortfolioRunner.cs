#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public sealed partial class PhysicalItemLogisticsPlayModeVerificationRunner
{
    private const float NaturalPortfolioFixedCaptureDeltaTime = 1f / 60f;

    private IEnumerator VerifyNaturalOutputPortfolio(
        DungeonRuntimeLifetimeScope scope)
    {
        string sceneBefore = V27CurrentSourceEvidenceDigest
            .ComputeGameplaySceneDigest();
        bool sceneExact = string.Equals(
            sceneBefore,
            ProductionOutputClearanceNaturalRunIdentity
                .OfficialGameplaySceneSha256,
            StringComparison.Ordinal);
        Check(
            sceneExact,
            "NATURAL_PORTFOLIO_OFFICIAL_SCENE_BEFORE_EXACT",
            sceneBefore);
        if (!sceneExact || scope?.Container == null)
            yield break;

        IProductionAssemblyBridge productionBridge =
            Resolve<IProductionAssemblyBridge>(scope);
        IProductionWorkshopRuntime workshops =
            Resolve<IProductionWorkshopRuntime>(scope);
        IRandomStreamDiagnosticsQuery randomDiagnostics =
            Resolve<IRandomStreamDiagnosticsQuery>(scope);
        IFacilityOutputClearanceTelemetryControl telemetry =
            Resolve<IFacilityOutputClearanceTelemetryControl>(scope);
        IFacilityBufferPlannedOutputPublicationService publication =
            Resolve<IFacilityBufferPlannedOutputPublicationService>(scope);
        IProductionOutputCapabilityRegistry outputCapabilities =
            Resolve<IProductionOutputCapabilityRegistry>(scope);
        IProductionRecipeExecutionReceiptQuery recipeReceipts =
            Resolve<IProductionRecipeExecutionReceiptQuery>(scope);
        IProductionRecipeExecutionCorrelationCommand recipeCorrelations =
            Resolve<IProductionRecipeExecutionCorrelationCommand>(scope);
        CropPlotRuntime crops = Resolve<CropPlotRuntime>(scope);
        ICropPlanExecutionReceiptQuery cropReceipts =
            Resolve<ICropPlanExecutionReceiptQuery>(scope);
        ICropCycleExecutionCorrelationCommand cropCorrelations =
            Resolve<ICropCycleExecutionCorrelationCommand>(scope);
        ICombatEquipmentRuntime combat = Resolve<ICombatEquipmentRuntime>(scope);
        IApparelWorkOrderCommand apparelCommands =
            Resolve<IApparelWorkOrderCommand>(scope);
        IApparelWorkOrderQuery apparelOrders =
            Resolve<IApparelWorkOrderQuery>(scope);
        IWorkExecutionHandlerRegistry workHandlers =
            Resolve<IWorkExecutionHandlerRegistry>(scope);
        ICertifiedSeedCommand certifiedSeeds =
            Resolve<ICertifiedSeedCommand>(scope);
        ICertifiedSeedExecutionReceiptQuery certifiedReceipts =
            Resolve<ICertifiedSeedExecutionReceiptQuery>(scope);
        IDungeonSaveCommandService saveCommands =
            Resolve<IDungeonSaveCommandService>(scope);
        DungeonAutosaveService autosave = saveCommands as DungeonAutosaveService;

        object[] required =
        {
            productionBridge,
            workshops,
            randomDiagnostics,
            telemetry,
            publication,
            outputCapabilities,
            recipeReceipts,
            recipeCorrelations,
            crops,
            cropReceipts,
            cropCorrelations,
            combat,
            apparelCommands,
            apparelOrders,
            workHandlers,
            certifiedSeeds,
            certifiedReceipts,
            saveCommands,
            autosave
        };
        bool compositionReady = required.All(value => value != null);
        Check(
            compositionReady,
            "NATURAL_PORTFOLIO_RUNTIME_COMPOSITION_READY",
            $"resolved={required.Count(value => value != null)}/{required.Length}");
        if (!compositionReady)
            yield break;

        ProductionOutputClearanceNaturalCompletionCorrelationAuthority
            specialCompletions = new();
        ProductionOutputClearanceNaturalMeasurementHandlerRegistry handlers =
            new(new IProductionOutputClearanceNaturalMeasurementHandler[]
            {
                new ProductionOutputClearanceRecipeNaturalMeasurementHandler(
                    recipeReceipts,
                    recipeCorrelations,
                    publication),
                new ProductionOutputClearanceCropHarvestNaturalMeasurementHandler(
                    cropReceipts,
                    cropCorrelations,
                    publication),
                new ProductionOutputClearanceCombatCraftNaturalMeasurementHandler(
                    specialCompletions,
                    specialCompletions),
                new ProductionOutputClearanceApparelNaturalMeasurementHandler(
                    specialCompletions,
                    specialCompletions),
                new ProductionOutputClearanceCertifiedSeedNaturalMeasurementHandler(
                    specialCompletions,
                    specialCompletions)
            });

        IProductionOutputClearanceSpecialNaturalScenarioHost specialHost =
            CreateSpecialNaturalScenarioHost(scope);
        IProductionOutputClearanceRecipeNaturalScenarioDriver recipeDriver =
            CreateRecipeNaturalScenarioDriver(scope);
        bool hostsReady = specialHost != null && recipeDriver != null;
        Check(
            hostsReady,
            "NATURAL_PORTFOLIO_LIVE_HOSTS_READY",
            $"special={specialHost != null};recipe={recipeDriver != null}");
        if (!hostsReady)
            yield break;

        ProductionOutputClearanceNaturalLiveDriverServices liveServices = new(
            productionBridge,
            workshops,
            randomDiagnostics,
            telemetry,
            publication,
            outputCapabilities);
        List<IProductionOutputClearanceNaturalMeasurementExecutor> executors =
            new()
            {
                new ProductionOutputClearanceRecipeNaturalMeasurementExecutor(
                    recipeDriver,
                    recipeCorrelations,
                    handlers)
            };
        executors.AddRange(
            ProductionOutputClearanceSpecialNaturalLiveDriverFactory
                .CreateExecutors(
                    specialHost,
                    liveServices,
                    combat,
                    apparelCommands,
                    apparelOrders,
                    crops,
                    cropCorrelations,
                    workHandlers,
                    certifiedSeeds,
                    certifiedReceipts,
                    specialCompletions,
                    handlers));
        ProductionOutputClearanceNaturalMeasurementExecutorRegistry registry =
            new(executors);

        ProductionOutputClearanceNaturalPortfolioRunResult completed = null;
        IEnumerator execution = null;
        Exception executionFailure = null;
        NaturalPortfolioTimeScope timeScope = null;
        autosave.Dispose();
        try
        {
            timeScope = NaturalPortfolioTimeScope.Begin(
                Resolve<IGameTimeScaleController>(scope),
                NaturalPortfolioAcceleratedTimeScale,
                NaturalPortfolioFixedCaptureDeltaTime);
            execution = ProductionOutputClearanceNaturalPortfolioCoordinator
                .Execute(
                    scope.Container,
                    handlers,
                    registry,
                    CaptureNaturalPortfolioConsole,
                    value => completed = value);
            while (true)
            {
                bool moved;
                object current = null;
                try
                {
                    moved = execution.MoveNext();
                    if (moved)
                        current = execution.Current;
                }
                catch (Exception exception)
                {
                    executionFailure = exception;
                    break;
                }
                if (moved)
                    yield return current;
                else
                    break;
            }
        }
        finally
        {
            try
            {
                (execution as IDisposable)?.Dispose();
            }
            finally
            {
                timeScope?.Dispose();
                autosave.Start();
            }
        }

        Check(
            executionFailure == null,
            "NATURAL_PORTFOLIO_EXECUTION_NO_EXCEPTION",
            executionFailure?.ToString() ?? "none");
        if (executionFailure != null)
            yield break;

        int currentPlanCount = completed?.Current.PayloadCounts.Values.Sum() ?? 0;
        int expectedSeeds = completed?.Current.Portfolio.Seeds.Count ?? 0;
        int expectedObservations = completed?.Current.Portfolio.Fixtures.Count ?? 0;
        naturalOutputPortfolioPlanCount = completed?.Current.Shards.Count ?? -1;
        bool complete = completed != null
            && currentPlanCount
                >= ProductionOutputClearanceNaturalPortfolioCoordinator
                    .MinimumV27BaselinePlanCount
            && expectedSeeds == 32
            && completed.Current.Shards.Count == currentPlanCount
            && expectedObservations == checked(currentPlanCount * expectedSeeds)
            && completed.Accepted.Records.Count == expectedObservations
            && completed.ResumedObservationCount
                + completed.ExecutedObservationCount == expectedObservations;
        Check(
            complete,
            "NATURAL_PORTFOLIO_CURRENT_X_32_COMPLETE",
            completed == null
                ? "result=missing"
                : $"plans={completed.Current.Shards.Count};seeds="
                    + $"{completed.Current.Portfolio.Seeds.Count};observations="
                    + $"{completed.Accepted.Records.Count};resumed="
                    + $"{completed.ResumedObservationCount};executed="
                    + completed.ExecutedObservationCount);
        if (complete
            && ProductionOutputClearanceNaturalBootstrapProfileSource.IsRequested)
        {
            ProductionOutputClearanceFrozenProfilePipeline
                .StageFromCompletedBootstrap(completed);
        }

        string sceneAfter = V27CurrentSourceEvidenceDigest
            .ComputeGameplaySceneDigest();
        Check(
            string.Equals(sceneBefore, sceneAfter, StringComparison.Ordinal),
            "NATURAL_PORTFOLIO_OFFICIAL_SCENE_AFTER_EXACT",
            sceneBefore + "->" + sceneAfter);
    }

    private ProductionOutputClearanceNaturalConsoleSnapshot
        CaptureNaturalPortfolioConsole() => new(
            capturedWarnings.Count,
            capturedErrors.Count);
}

internal sealed class NaturalPortfolioTimeScope : IDisposable
{
    private static bool active;
    private static int generation;
    private static IGameTimeScaleController controller;
    private static float originalTimeScale;
    private static float originalFixedDeltaTime;
    private static float originalCaptureDeltaTime;
    private readonly int ownerGeneration;
    private bool disposed;

    static NaturalPortfolioTimeScope()
    {
        AssemblyReloadEvents.beforeAssemblyReload += RestoreActiveState;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.quitting += RestoreActiveState;
    }

    private NaturalPortfolioTimeScope(int ownerGeneration)
    {
        this.ownerGeneration = ownerGeneration;
    }

    internal static bool IsActive => active;

    internal static NaturalPortfolioTimeScope Begin(
        IGameTimeScaleController timeController, float scale, float captureDeltaTime)
    {
        if (timeController == null) throw new ArgumentNullException(nameof(timeController));
        if (!float.IsFinite(scale) || scale <= 0f)
            throw new ArgumentOutOfRangeException(nameof(scale));
        if (!float.IsFinite(captureDeltaTime) || captureDeltaTime <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(captureDeltaTime),
                captureDeltaTime,
                "Natural portfolio capture delta must be positive and finite.");
        }
        if (active)
        {
            throw new InvalidOperationException(
                "Natural portfolio time scope is already active.");
        }

        int currentGeneration = checked(generation + 1);
        controller = timeController;
        originalTimeScale = Time.timeScale;
        originalFixedDeltaTime = Time.fixedDeltaTime;
        originalCaptureDeltaTime = Time.captureDeltaTime;
        active = true;
        generation = currentGeneration;
        try
        {
            ApplyTimeScale(scale);
            Time.captureDeltaTime = captureDeltaTime;
            Debug.Log($"V27_NATURAL_CLOCK scale={Time.timeScale:R};fixed={Time.fixedDeltaTime:R};capture={Time.captureDeltaTime:R}");
            return new NaturalPortfolioTimeScope(currentGeneration);
        }
        catch
        {
            RestoreActiveState();
            throw;
        }
    }

    internal static void ApplyTimeScale(float scale)
    {
        if (!active || controller == null)
            throw new InvalidOperationException("Natural portfolio time scope is not active.");
        if (!float.IsFinite(scale) || scale <= 0f)
            throw new ArgumentOutOfRangeException(nameof(scale));
        controller.Scale = scale;
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        if (active && ownerGeneration == generation)
            RestoreActiveState();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
            RestoreActiveState();
    }

    private static void RestoreActiveState()
    {
        if (!active)
            return;
        try { controller.Scale = originalTimeScale; }
        finally
        {
            Time.timeScale = originalTimeScale;
            Time.fixedDeltaTime = originalFixedDeltaTime;
            Time.captureDeltaTime = originalCaptureDeltaTime;
            controller = null;
            originalTimeScale = originalFixedDeltaTime = originalCaptureDeltaTime = 0f;
            active = false;
            Debug.Log($"V27_NATURAL_CLOCK_RESTORED scale={Time.timeScale:R};fixed={Time.fixedDeltaTime:R};capture={Time.captureDeltaTime:R}");
        }
    }
}
#endif
