#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DungeonStory.Environment;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

/// <summary>
/// Production-live closure for work types that do not yet have a dedicated
/// verifier. A row can pass only after the real Brain -> AIWork -> AbilityWork
/// -> WorkTaskExecutor path changes approved work progress and closes its
/// typed lifecycle. Missing authored targets remain explicit blockers.
/// </summary>
public static class CharacterAiWorkTypeLiveMatrixPlayModeVerifier
{
    public static void RunW01FireIdentityRegressionBatch()
    {
        string output = RequireAuditOutput("W01 fire identity regression");
        if (Directory.Exists(output))
            throw new InvalidOperationException("W01 fire regression requires a new evidence directory.");
        Directory.CreateDirectory(output);
        var rows = new List<string>();
        bool passed = true;
        Run("response-operation-id", VerifyFireOperationIdentity);
        Run("fire-completion-terminal-contract", VerifyFireCompletionContract);
        Run("fire-domain-replay-restore", EnvironmentalFireFocusedDebugScenarios.RunAll);
        using (var writer = new StreamWriter(new FileStream(Path.Combine(output, "w01-fire-identity-regression.txt"),
            FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)))
        {
            writer.NewLine = "\n";
            writer.WriteLine("status=" + (passed ? "PASS" : "FAIL") + "; cases=3");
            foreach (string row in rows) writer.WriteLine(row);
        }
        if (!passed) throw new InvalidOperationException("W01 fire regression failed; preserved report has details.");
        Debug.Log("W01_FIRE_IDENTITY_REGRESSION=PASS cases=3; scope=operation ID, completion contract and controlled fire runtime, not actual AI identity");
        void Run(string name, Action test)
        {
            try { test(); rows.Add("PASS " + name); }
            catch (Exception error) { passed = false; rows.Add("FAIL " + name + " " + error); }
        }
    }

    private static void VerifyFireCompletionContract()
    {
        var owner = new EnvironmentalFireSuppressionSaveRecord
        {
            operationId = "fire:contract:1", fireId = "environmental-fire:1", workerId = "owner",
            targetId = "building:contract", workerDisplayName = "Owner", absoluteDay = 1, outcomeRevision = 2,
            commandOrigin = (int)CharacterCommandOrigin.DirectPlayerOrder, approvedWork = 1f,
            phase = (int)EnvironmentalFireSuppressionPhase.Applied,
            mode = (int)EnvironmentalFireSuppressionMode.InitialAttack, intensityAfter = 0.1f
        };
        Require(!EnvironmentalFireCompletionIdentity.RequiresIdentity(owner), "Intermediate initial attack requested identity.");
        owner.intensityAfter = 0f;
        Require(EnvironmentalFireCompletionIdentity.RequiresIdentity(owner), "Final initial attack lost identity.");
        var first = EnvironmentalFireCompletionIdentity.CreateRequest(owner);
        Require(EnvironmentalFireCompletionIdentity.RequireCompletionChild(first).ToString() == first.ChildResultKey
            && first.Origin == CharacterCommandOrigin.DirectPlayerOrder && first.OperationSequence == 0,
            "Fire completion lost its canonical child/origin.");
        owner.approvedWork = 2f;
        var changed = EnvironmentalFireCompletionIdentity.CreateRequest(owner);
        Require(first.DeliveryId == changed.DeliveryId && first.PayloadFingerprint != changed.PayloadFingerprint,
            "Changed contribution allocated a second identity instead of conflicting.");
        owner.workerId = "character:other";
        Require(first.DeliveryId != EnvironmentalFireCompletionIdentity.CreateRequest(owner).DeliveryId,
            "Different responder shared completion identity.");
        owner.mode = (int)EnvironmentalFireSuppressionMode.Water;
        owner.intensityAfter = 0.4f;
        Require(EnvironmentalFireCompletionIdentity.RequiresIdentity(owner), "Applied water work lost terminal identity.");
        owner.phase = (int)EnvironmentalFireSuppressionPhase.WaterCommitted;
        Require(!EnvironmentalFireCompletionIdentity.RequiresIdentity(owner), "Unapplied water work requested identity.");
        static void Require(bool value, string failure)
        {
            if (!value) throw new InvalidOperationException(failure);
        }
    }

    private static void VerifyFireOperationIdentity()
    {
        string first = Id("environmental-fire:1", "owner", 0f, 0);
        string[] distinct =
        {
            first, Id("environmental-fire:2", "owner", 0f, 0),
            Id("environmental-fire:1", "staff:1", 0f, 0),
            Id("environmental-fire:1", "owner", 1f, 0),
            Id("environmental-fire:1", "owner", 0f, 1),
            Id("environmental-fire:1", "owner", 0.5f, 0),
            Id("environmental-fire:1", new string('a', 192), float.MaxValue, int.MaxValue)
        };
        if (distinct.Distinct(StringComparer.Ordinal).Count() != distinct.Length
            || distinct.Any(value => !new GameplayOperationId(value).IsValid)
            || first != Id("environmental-fire:1", "owner", 0f, 0))
            throw new InvalidOperationException("Fire response operation IDs are not stable, bounded and input-sensitive.");
        var previousCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
            if (distinct[5] != Id("environmental-fire:1", "owner", 0.5f, 0))
                throw new InvalidOperationException("Fire operation identity depends on locale.");
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previousCulture; }
        Reject(() => Id("bad=fire", "owner", 0f, 0));
        Reject(() => Id("environmental-fire:1", "", 0f, 0));
        Reject(() => Id("environmental-fire:1", "owner", float.NaN, 0));
        Reject(() => Id("environmental-fire:1", "owner", float.PositiveInfinity, 0));
        Reject(() => Id("environmental-fire:1", "owner", -1f, 0));
        Reject(() => Id("environmental-fire:1", "owner", 0f, -1));
        static string Id(string fire, string worker, float completed, int water) =>
            EnvironmentalFireResponseRuntime.CreateSuppressionOperationId(fire, worker, completed, water);
        static void Reject(Func<string> invalid)
        {
            try { invalid(); }
            catch (ArgumentException) { return; }
            throw new InvalidOperationException("Invalid fire identity input was accepted.");
        }
    }

    public static void RunW01CropIdentityRegressionBatch()
    {
        string output = RequireAuditOutput("W01 crop identity regression");
        if (Directory.Exists(output))
            throw new InvalidOperationException("W01 crop regression requires a new evidence directory.");
        Directory.CreateDirectory(output);
        List<string> rows = new();
        bool passed = true;
        Run("crop-physical-restore-joins", () => CropPhysicalTransactionFixture.Run());
        Run("domain-output-publication-and-restore", () => { ProductionDomainOutputPublicationDebugScenarios.VerifyAll(); return true; });
        Run("terminal-identity-ownership", () => { WorkTerminalIdentityOwnershipDebugScenarios.RunOrThrow(); return true; });
        Run("crop-production-entry", () => EnvironmentCropProductionEntryDebugScenarios.RunAll(log: true));
        Run("work-identity-causal-restore", () => WorkCompletionIdentityCausalProductionEntryDebugScenarios.RunCausalOwnerFamily(log: true));
        Run("work-identity-rollback-and-recipient", () => { V26FounderTraitAuditScenario.RunWorkCompletionDeliveryRegression(); return true; });
        using (StreamWriter writer = new(new FileStream(Path.Combine(output, "w01-crop-identity-regression.txt"),
            FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)))
        {
            writer.NewLine = "\n";
            writer.WriteLine("status=" + (passed ? "PASS" : "FAIL"));
            foreach (string row in rows) writer.WriteLine(row);
        }
        if (!passed) throw new InvalidOperationException("W01 crop identity regressions failed; see preserved report.");
        Debug.Log("W01_CROP_IDENTITY_REGRESSION=PASS cases=6");
        void Run(string name, Func<bool> test)
        {
            try
            {
                if (!test()) throw new InvalidOperationException("Fixture returned false.");
                rows.Add("PASS " + name);
            }
            catch (Exception error)
            {
                passed = false;
                rows.Add("FAIL " + name + " " + error);
            }
        }
    }

    public const string ReportPath =
        "Artifacts/QA/character-ai-worktype-live-matrix.txt";
    private const string PersistentCookPauseEnvironment =
        "DUNGEONSTORY_PERSISTENT_COOK_PAUSE_VERIFY";
    private const string W05HarvestIdentityEnvironment =
        "DUNGEONSTORY_W05_HARVEST_IDENTITY_VERIFY";
    private const string AuditOutputEnvironment =
        "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT";
    private const string DefaultP15AutomationModesReportPath =
        "Artifacts/QA/v27-p15-production-execution-modes-playmode.txt";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private const string PendingPath =
        "Temp/character-ai-worktype-live-matrix.flag";
    private const string P15AutomationModesPendingPath =
        "Temp/v27-p15-production-execution-modes.flag";
    private const string W05HarvestIdentityPendingPath =
        "Temp/w05-harvest-identity.flag";

    public static bool IsPersistentCookPauseVerification => string.Equals(
        Environment.GetEnvironmentVariable(PersistentCookPauseEnvironment),
        "1",
        StringComparison.Ordinal);

    public static bool IsW05HarvestIdentityVerification => string.Equals(
        Environment.GetEnvironmentVariable(W05HarvestIdentityEnvironment),
        "1",
        StringComparison.Ordinal);

    public static string W05HarvestIdentityReportPath => Path.Combine(
        RequireAuditOutput("W05 harvest identity verification"),
        "w05-harvest-identity-report.txt");

    public static bool IsW01WorkTerminalJoinVerification => string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_W01_WORK_TERMINAL_JOIN_VERIFY"),
        "1", StringComparison.Ordinal);

    public static bool IsW03MedicalLaborVerification => string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_W03_MEDICAL_LABOR_VERIFY"),
        "1", StringComparison.Ordinal);

    public static bool IsW04RepairLiveVerification => string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_W04_REPAIR_LIVE_VERIFY"),
        "1", StringComparison.Ordinal);

    public static bool IsW06ResourceCausalityVerification => string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_W06_RESOURCE_CAUSALITY_VERIFY"),
        "1", StringComparison.Ordinal);

    public static string W03MedicalLaborReportPath => Path.Combine(
        RequireAuditOutput("W03 medical labor verification"), "w03-medical-labor-report.txt");

    public static string W04RepairLiveReportPath => Path.Combine(
        RequireAuditOutput("W04 repair live verification"),
        "w04-repair-live-report.txt");

    public static string W06ResourceCausalityReportPath => Path.Combine(
        RequireAuditOutput("W06 resource causality verification"),
        "w06-resource-causality-report.txt");

    public static string W01WorkTerminalJoinReportPath => Path.Combine(
        RequireAuditOutput("W01 work terminal join verification"),
        IsW01FireWaterIdentityVerification ? "w01-fire-water-identity-report.txt"
            : IsW01FireIdentityVerification ? "w01-fire-identity-report.txt"
            : "w01-work-terminal-join-report.txt");

    public static bool IsW01FireIdentityVerification => string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_W01_FIRE_IDENTITY_VERIFY"),
        "1", StringComparison.Ordinal);

    public static bool IsW01FireWaterIdentityVerification => string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_W01_FIRE_WATER_IDENTITY_VERIFY"),
        "1", StringComparison.Ordinal);

    public static string P15AutomationModesReportPath
    {
        get
        {
            if (!IsPersistentCookPauseVerification)
                return DefaultP15AutomationModesReportPath;

            string output = Environment.GetEnvironmentVariable(
                AuditOutputEnvironment);
            if (string.IsNullOrWhiteSpace(output))
            {
                throw new InvalidOperationException(
                    "Persistent Cook pause verification requires an isolated audit output directory.");
            }
            return Path.Combine(
                Path.GetFullPath(output),
                "persistent-cook-pause-report.txt");
        }
    }

    private static string RequireAuditOutput(string verification)
    {
        string output = Environment.GetEnvironmentVariable(
            AuditOutputEnvironment);
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                verification + " requires an isolated audit output directory.");
        }
        return Path.GetFullPath(output);
    }

    [MenuItem("DungeonStory/Debug/QA/Run Character AI WorkType Live Matrix")]
    public static void RequestRun()
    {
        if (EditorApplication.isPlaying)
        {
            StartRunner(p15AutomationModesOnly: false, w05HarvestIdentityOnly: false);
            return;
        }

        if (!File.Exists(GameplayScenePath))
        {
            throw new FileNotFoundException(
                "Character AI WorkType live matrix requires the official gameplay scene.",
                GameplayScenePath);
        }

        EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        Directory.CreateDirectory("Temp");
        File.WriteAllText(PendingPath, DateTime.UtcNow.ToString("O"));
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("DungeonStory/Debug/QA/Run P15 Production Execution Modes")]
    public static void RequestP15AutomationModesRun()
    {
        if (EditorApplication.isPlaying)
        {
            StartRunner(p15AutomationModesOnly: true, w05HarvestIdentityOnly: false);
            return;
        }

        if (!File.Exists(GameplayScenePath))
        {
            throw new FileNotFoundException(
                "P15 execution-mode verification requires the official gameplay scene.",
                GameplayScenePath);
        }

        EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        Directory.CreateDirectory("Temp");
        File.WriteAllText(P15AutomationModesPendingPath, DateTime.UtcNow.ToString("O"));
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("DungeonStory/Debug/QA/Run W05 Harvest Identity Verification")]
    public static void RequestW05HarvestIdentityRun()
    {
        if (!IsW05HarvestIdentityVerification)
        {
            throw new InvalidOperationException(
                "W05 harvest identity verification requires its isolated environment flag.");
        }
        string reportPath = W05HarvestIdentityReportPath;
        if (File.Exists(reportPath))
        {
            throw new IOException(
                "W05 harvest identity report already exists: " + reportPath);
        }
        if (EditorApplication.isPlaying)
        {
            StartRunner(p15AutomationModesOnly: false, w05HarvestIdentityOnly: true);
            return;
        }
        if (!File.Exists(GameplayScenePath))
        {
            throw new FileNotFoundException(
                "W05 harvest identity verification requires the official gameplay scene.",
                GameplayScenePath);
        }
        EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        Directory.CreateDirectory("Temp");
        File.WriteAllText(
            W05HarvestIdentityPendingPath,
            DateTime.UtcNow.ToString("O"));
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        bool p15AutomationModesOnly = File.Exists(P15AutomationModesPendingPath);
        bool w05HarvestIdentityOnly = File.Exists(W05HarvestIdentityPendingPath);
        if (!p15AutomationModesOnly
            && !w05HarvestIdentityOnly
            && !File.Exists(PendingPath))
            return;
        StartRunner(p15AutomationModesOnly, w05HarvestIdentityOnly);
    }

    internal static void MarkRunCompleted()
    {
        if (File.Exists(PendingPath))
            File.Delete(PendingPath);
        if (File.Exists(P15AutomationModesPendingPath))
            File.Delete(P15AutomationModesPendingPath);
        if (File.Exists(W05HarvestIdentityPendingPath))
            File.Delete(W05HarvestIdentityPendingPath);
    }

    private static void StartRunner(
        bool p15AutomationModesOnly,
        bool w05HarvestIdentityOnly)
    {
        if (UnityEngine.Object.FindFirstObjectByType<
                CharacterAiWorkTypeLiveMatrixPlayModeRunner>() != null)
        {
            return;
        }

        CharacterAiWorkTypeLiveMatrixPlayModeRunner runner =
            new GameObject("Character AI WorkType Live Matrix Runner")
                .AddComponent<CharacterAiWorkTypeLiveMatrixPlayModeRunner>();
        runner.P15AutomationModesOnly = p15AutomationModesOnly;
        runner.W05HarvestIdentityOnly = w05HarvestIdentityOnly;
    }
}

public sealed class CharacterAiWorkTypeLiveMatrixPlayModeRunner : MonoBehaviour
{
    public bool P15AutomationModesOnly { get; set; }
    public bool W05HarvestIdentityOnly { get; set; }
    public bool W01WorkTerminalJoinOnly { get; set; }
    public bool W03MedicalLaborOnly { get; set; }
    public bool W04RepairLiveOnly { get; set; }
    public bool W06ResourceCausalityOnly { get; set; }

    private const float MinimumProgressObservationSeconds = 8f;
    private const float MaximumProgressObservationSeconds = 120f;
    private const float MaximumCompletionObservationSeconds = 240f;
    private const float CompletionNoProgressSeconds = 30f;
    private const float MovementStallSeconds = 5f;
    private const float MovementPositionEpsilonSquared = 0.0001f;

    private static readonly WorkTypeId[] Rows =
    {
        BuiltInWorkTypeIds.Operate,
        BuiltInWorkTypeIds.Restock,
        BuiltInWorkTypeIds.Repair,
        BuiltInWorkTypeIds.Reception,
        BuiltInWorkTypeIds.Craft,
        BuiltInWorkTypeIds.Butcher,
        BuiltInWorkTypeIds.DrawWater,
        BuiltInWorkTypeIds.Cook,
        BuiltInWorkTypeIds.Refuel,
        BuiltInWorkTypeIds.Perform,
        BuiltInWorkTypeIds.Gather,
        BuiltInWorkTypeIds.Sow,
        BuiltInWorkTypeIds.Harvest,
        BuiltInWorkTypeIds.Logging,
        BuiltInWorkTypeIds.Quarry,
        BuiltInWorkTypeIds.AnimalCare,
        BuiltInWorkTypeIds.GrandProject,
        BuiltInWorkTypeIds.ThreatMitigation,
        BuiltInWorkTypeIds.Plumbing,
        BuiltInWorkTypeIds.Dismantle
    };

    private static readonly string[] RequiredFixtureResearchIds =
    {
        "research:agriculture:gathering",
        "research:agriculture:field",
        "research:forestry:logging",
        "research:mining:surface",
        "research:mining:quarry",
        "research:mining:stonecutting",
        "research:cuisine:livestock",
        "research:plumbing:sewer"
    };

    private readonly List<WorkTypeLiveRow> results =
        new List<WorkTypeLiveRow>(Rows.Length);
    private readonly List<string> consoleIssues = new List<string>(8);
    private readonly List<ActorPauseState> actorPauseStates =
        new List<ActorPauseState>(8);
    private readonly List<BuildableObject> roomFixtureBuildings =
        new List<BuildableObject>(32);
    private readonly List<BuildableObject> rowScopedFixtureBuildings =
        new List<BuildableObject>(16);
    private readonly List<string> rowScopedWildlifeIds = new List<string>(4);
    private readonly List<string> rowScopedItemStackIds = new List<string>(16);
    private readonly List<DisplacedMovementSnapshot> displacedRoomMovements =
        new List<DisplacedMovementSnapshot>(16);
    private readonly List<DisplacedWildlifeSnapshot> displacedRoomWildlife =
        new List<DisplacedWildlifeSnapshot>(4);
    private readonly List<FixtureAreaSnapshot> roomAreaSnapshots =
        new List<FixtureAreaSnapshot>(24);
    private DungeonRuntimeLifetimeScope runtimeScope;
    private IDungeonGameSaveService gameSaveService;
    private ICharacterAiWorldRegistry worldRegistry;
    private DungeonGameSaveData baseline;
    private CharacterActor actor;
    private AbilityWork work;
    private AIBrain brain;
    private Grid grid;
    private IProductionBillQuery productionBills;
    private IProductionBillOrderCommand productionOrders;
    private IProductionBillWorkExecution productionWork;
    private IAutomationInfrastructureQuery automation;
    private IAutomationInfrastructureCommand automationCommands;
    private IAutomationInfrastructurePersistence automationPersistence;
    private IPowerInfrastructureQuery power;
    private ISettlementLaborAccountingService settlementLabor;
    private IGameSpeedController gameSpeed;
    private IResourceEconomyContentCatalog economyContent;
    private IWorldItemStackRuntime physicalItems;
    private WorldItemRepository itemRepository;
    private IWildlifeRuntime wildlifeRuntime;
    private IWildlifeCarcassService carcassService;
    private IShopStockCatalog shopStockCatalog;
    private IWorldResourceRuntime worldResources;
    private CropPlotRuntime cropPlots;
    private IWorkOrderRuntime workOrders;
    private StaffDiscontentRuntime staffDiscontent;
    private ICharacterEnvironmentPersistence characterEnvironmentPersistence;
    private ICharacterEnvironmentStatusQuery characterEnvironmentStatus;
    private ICharacterDeprivationRuntime deprivationRuntime;
    private BuildableObject domainStage;
    private BuildableObject domainPen;
    private RoomInstance performanceRoom;
    private Vector2Int? roomExternalExitStand;
    private Vector2Int? roomBaselineReachableSentinel;
    private float previousTimeScale;
    private bool consoleGatePassed;
    private bool abortRemainingRows;
    private readonly List<WorkCompletedIdentityEvent> w05IdentityEvents = new();
    private IDisposable w05IdentitySubscription;
    private WorkCompletionIdentityDeliveryLedger w05IdentityDeliveries;
    private W01RepairHandlerObservation w01RepairObservation;
    private W05HarvestDeliveryGate w05HarvestDeliveryGate;
    private W01RepairDeliveryGate w01RepairDeliveryGate;
    private W04RepairDeliveryObservation w04RepairDeliveries;
    private IReadOnlyList<WorkCompletionIdentityDeliveryCursorSaveData>
        w05DeliveryBaseline = Array.Empty<
            WorkCompletionIdentityDeliveryCursorSaveData>();
    private string w05HarvestPlotId = string.Empty;

    private IEnumerator Start()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 8f;
        Application.logMessageReceived += CaptureConsoleIssue;
        yield return RunGuarded();
        Cleanup();
        Application.logMessageReceived -= CaptureConsoleIssue;
        consoleGatePassed = consoleIssues.Count == 0;
        results.Add(consoleGatePassed
            ? WorkTypeLiveRow.Info("console-warning-error-zero", "0/0")
            : new WorkTypeLiveRow(
                "global:console-warning-error-zero",
                "FAIL",
                "-",
                string.Join(" | ", consoleIssues)));
        bool reportPassed = WriteReport();
        CharacterAiWorkTypeLiveMatrixPlayModeVerifier.MarkRunCompleted();
        Time.timeScale = previousTimeScale;
        Destroy(gameObject);
        if (CharacterAiWorkTypeLiveMatrixPlayModeVerifier
                .IsPersistentCookPauseVerification
            || CharacterAiWorkTypeLiveMatrixPlayModeVerifier
                .IsW05HarvestIdentityVerification
            || CharacterAiWorkTypeLiveMatrixPlayModeVerifier
                .IsW01WorkTerminalJoinVerification
            || CharacterAiWorkTypeLiveMatrixPlayModeVerifier.IsW03MedicalLaborVerification
            || CharacterAiWorkTypeLiveMatrixPlayModeVerifier.IsW04RepairLiveVerification
            || CharacterAiWorkTypeLiveMatrixPlayModeVerifier.IsW06ResourceCausalityVerification)
        {
            DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
            EditorApplication.delayCall += () =>
                EditorApplication.Exit(reportPassed ? 0 : 1);
            yield break;
        }
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;
        };
    }

    public void AddInitialConsoleIssues(
        IEnumerable<string> warnings,
        IEnumerable<string> errors)
    {
        if (warnings != null)
        {
            consoleIssues.AddRange(warnings.Select(value =>
                "EarlyWarning:" + (value ?? string.Empty)));
        }
        if (errors != null)
        {
            consoleIssues.AddRange(errors.Select(value =>
                "EarlyError:" + (value ?? string.Empty)));
        }
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= CaptureConsoleIssue;
        w01RepairObservation?.Dispose();
        w05IdentitySubscription?.Dispose();
        w05IdentitySubscription = null;
    }

    private void CaptureConsoleIssue(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (type is LogType.Warning
            or LogType.Error
            or LogType.Exception
            or LogType.Assert)
        {
            consoleIssues.Add(type + ":" + condition);
        }
    }

    private IEnumerator RunGuarded()
    {
        IEnumerator scenario = null;
        try
        {
            scenario = RunScenario();
        }
        catch (Exception exception)
        {
            AddGlobalFailure("setup-exception", exception.ToString());
        }

        if (scenario == null)
            yield break;

        while (true)
        {
            bool moved;
            object current = null;
            try
            {
                moved = scenario.MoveNext();
                if (moved)
                    current = scenario.Current;
            }
            catch (Exception exception)
            {
                AddGlobalFailure("runtime-exception", exception.ToString());
                yield break;
            }

            if (!moved)
                yield break;
            yield return current;
        }
    }

    private IEnumerator RunScenario()
    {
        DungeonRuntimeLifetimeScope scope = null;
        float setupDeadline = Time.realtimeSinceStartup + 45f;
        while (Time.realtimeSinceStartup < setupDeadline)
        {
            DungeonRuntimeLifetimeScope[] candidates =
                FindObjectsByType<DungeonRuntimeLifetimeScope>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0; index < candidates.Length; index++)
            {
                DungeonRuntimeLifetimeScope candidate = candidates[index];
                if (candidate != null
                    && candidate.gameObject.scene.IsValid()
                    && candidate.gameObject.scene.isLoaded
                    && candidate.Container != null)
                {
                    scope = candidate;
                    break;
                }
            }
            if (scope != null)
                break;
            yield return null;
        }

        if (scope?.Container == null)
        {
            AddGlobalFailure(
                "runtime-scope-missing",
                $"DungeonRuntimeLifetimeScope was not published after 45 realtime seconds; "
                + $"activeScene={SceneManager.GetActiveScene().path}.");
            yield break;
        }
        runtimeScope = scope;

        worldRegistry = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        CharacterActor[] actors = LiveActors(worldRegistry);
        if (actors.Length < 3)
        {
            string promotion = StartPartyPreparationPlayModeVerifier
                .RunFastCommitForDebug();
            results.Add(WorkTypeLiveRow.Info("baseline-promotion", promotion));
            for (int frame = 0; frame < 8; frame++)
                yield return null;
            actors = LiveActors(worldRegistry);
        }

        if (actors.Length < 3 || actors.Count(value => value.Role == CharacterRole.Owner) != 1)
        {
            AddGlobalFailure(
                "started-party-invalid",
                "actors=" + actors.Length + "; owners="
                + actors.Count(value => value.Role == CharacterRole.Owner));
            yield break;
        }

        gameSaveService = scope.Container.Resolve<IDungeonGameSaveService>();
        grid = FindFirstObjectByType<GridSystemManager>()?.grid;
        actor = SelectStableWorkSubject(actors);
        if (grid == null || actor == null)
        {
            AddGlobalFailure(
                "fixture-world-authority-missing",
                "grid=" + (grid != null) + "; actor=" + (actor != null));
            yield break;
        }
        baseline = gameSaveService.Capture();
        string researchFixtureDetail = string.Empty;
        IReadOnlyList<string> fixtureResearchIds = P15AutomationModesOnly
            ? new[] { "research:cuisine:livestock" }
            : W06ResourceCausalityOnly
                ? new[]
                {
                    "research:agriculture:gathering",
                    "research:agriculture:field",
                    "research:forestry:logging"
                }
            : W01WorkTerminalJoinOnly || W03MedicalLaborOnly || W04RepairLiveOnly
                ? Array.Empty<string>()
                : W05HarvestIdentityOnly
                ? new[] { "research:agriculture:field" }
                : RequiredFixtureResearchIds;
        if (!EnsureFixtureResearchPrerequisites(
                scope.Container,
                fixtureResearchIds,
                includeGrandProject: !P15AutomationModesOnly
                    && !W05HarvestIdentityOnly && !W01WorkTerminalJoinOnly
                    && !W03MedicalLaborOnly && !W04RepairLiveOnly
                    && !W06ResourceCausalityOnly,
                out researchFixtureDetail))
        {
            AddGlobalFailure(
                "grand-project-research-fixture",
                researchFixtureDetail);
            yield break;
        }
        results.Add(WorkTypeLiveRow.Info(
            "fixture-research",
            researchFixtureDetail));
        for (int frame = 0; frame < 4; frame++)
            yield return null;
        actors = LiveActors(worldRegistry);

        productionBills = scope.Container.Resolve<IProductionBillQuery>();
        productionOrders = scope.Container.Resolve<IProductionBillOrderCommand>();
        productionWork = scope.Container.Resolve<IProductionBillWorkExecution>();
        automation = scope.Container.Resolve<IAutomationInfrastructureQuery>();
        automationCommands = scope.Container.Resolve<IAutomationInfrastructureCommand>();
        automationPersistence = scope.Container.Resolve<
            IAutomationInfrastructurePersistence>();
        power = scope.Container.Resolve<IPowerInfrastructureQuery>();
        settlementLabor = scope.Container.Resolve<ISettlementLaborAccountingService>();
        gameSpeed = scope.Container.Resolve<IGameSpeedController>();
        economyContent = scope.Container.Resolve<IResourceEconomyContentCatalog>();
        physicalItems = scope.Container.Resolve<IWorldItemStackRuntime>();
        itemRepository = scope.Container.Resolve<WorldItemRepository>();
        wildlifeRuntime = scope.Container.Resolve<IWildlifeRuntime>();
        carcassService = scope.Container.Resolve<IWildlifeCarcassService>();
        shopStockCatalog = scope.Container.Resolve<IShopStockCatalog>();
        worldResources = scope.Container.Resolve<IWorldResourceRuntime>();
        cropPlots = scope.Container.Resolve<CropPlotRuntime>();
        workOrders = scope.Container.Resolve<IWorkOrderRuntime>();
        characterEnvironmentPersistence = scope.Container
            .Resolve<ICharacterEnvironmentPersistence>();
        characterEnvironmentStatus = scope.Container
            .Resolve<ICharacterEnvironmentStatusQuery>();
        deprivationRuntime = scope.Container
            .Resolve<ICharacterDeprivationRuntime>();
        staffDiscontent = scope.Container
            .Resolve<CharacterSceneRuntimeReferences>()
            .StaffDiscontent;
        actor = SelectStableWorkSubject(actors);
        work = actor != null ? actor.GetComponent<AbilityWork>() : null;
        brain = actor != null ? actor.Brain : null;
        grid = FindFirstObjectByType<GridSystemManager>()?.grid;
        if (actor == null || work == null || brain == null || grid == null)
        {
            AddGlobalFailure(
                "worker-fixture-missing",
                "actor=" + (actor != null) + "; work=" + (work != null)
                + "; brain=" + (brain != null) + "; grid=" + (grid != null));
            yield break;
        }
        results.Add(WorkTypeLiveRow.Info(
            "subject",
            "id=" + (actor.Identity?.PersistentId ?? "-")
                + "; role=" + actor.Role
                + "; lifecycle=" + actor.CurrentLifecycleState));

        if (!W05HarvestIdentityOnly && !W01WorkTerminalJoinOnly
            && !W03MedicalLaborOnly && !W04RepairLiveOnly
            && worldResources is WorldResourceRuntime worldResourceRuntime)
        {
            float resourceDeadline = Time.realtimeSinceStartup + 8f;
            bool published = false;
            while (Time.realtimeSinceStartup < resourceDeadline)
            {
                worldResourceRuntime.Tick();
                published = HasAvailableWorldResource(BuiltInWorkTypeIds.Gather)
                    && HasAvailableWorldResource(BuiltInWorkTypeIds.Logging);
                if (published)
                    break;
                yield return null;
            }
            results.Add(WorkTypeLiveRow.Info(
                "world-resource-fixture",
                "gather=" + HasAvailableWorldResource(BuiltInWorkTypeIds.Gather)
                + "; logging=" + HasAvailableWorldResource(BuiltInWorkTypeIds.Logging)
                + "; nodes=" + worldResources.NodeCount));
        }

        // Remove live-fixture competition without mutating authored targets.
        // The selected subject remains active; every actor's exact prior pause
        // state is restored after the authoritative save baseline is restored.
        actorPauseStates.Clear();
        foreach (CharacterActor liveActor in actors)
        {
            string persistentId = liveActor.Identity?.PersistentId ?? string.Empty;
            actorPauseStates.Add(new ActorPauseState(
                persistentId,
                liveActor.IsAiPaused()));
            if (ReferenceEquals(liveActor, actor))
                continue;

            liveActor.Brain?.StopCurrentActionForReplan(
                "worktype live matrix isolate subject");
            liveActor.SetAiPaused(true);
        }
        results.Add(WorkTypeLiveRow.Info(
            "subject-isolation",
            "subject=" + (actor.Identity?.PersistentId ?? actor.name)
            + "; competitorsPaused=" + (actors.Length - 1)
            + "; priorPauseStatesCaptured=" + actorPauseStates.Count));
        yield return null;

        if (P15AutomationModesOnly)
        {
            IEnumerator focused = RunP15AutomationModesFocused();
            while (focused.MoveNext())
                yield return focused.Current;
            yield break;
        }

        if (W03MedicalLaborOnly)
        {
            IEnumerator medicalLabor = RunW03MedicalLaborFocused();
            while (medicalLabor.MoveNext()) yield return medicalLabor.Current;
            yield break;
        }

        if (W04RepairLiveOnly)
        {
            IEnumerator repair = RunW04RepairLiveFocused();
            while (repair.MoveNext())
                yield return repair.Current;
            yield break;
        }

        if (W06ResourceCausalityOnly)
        {
            IEnumerator causality = RunW06ResourceCausalityFocused();
            while (causality.MoveNext())
                yield return causality.Current;
            yield break;
        }

        if (W01WorkTerminalJoinOnly)
        {
            IEnumerator terminalJoin = RunW01WorkTerminalJoinFocused();
            while (terminalJoin.MoveNext())
                yield return terminalJoin.Current;
            yield break;
        }

        if (W05HarvestIdentityOnly)
        {
            IGameEventBus gameEvents = scope.Container.Resolve<IGameEventBus>();
            w05IdentitySubscription = gameEvents.Subscribe<
                WorkCompletedIdentityEvent>(w05IdentityEvents.Add);
            w05IdentityDeliveries = scope.Container.Resolve<
                WorkCompletionIdentityDeliveryLedger>();
            w05DeliveryBaseline = w05IdentityDeliveries.Capture();
            int identityBaseline = w05IdentityEvents.Count;
            IEnumerator harvest = RunW05HarvestIdentityFocused();
            while (harvest.MoveNext())
                yield return harvest.Current;
            VerifyW05HarvestTerminalIdentity(identityBaseline);
            yield break;
        }

        foreach (WorkTypeId workTypeId in Rows)
        {
            if (abortRemainingRows)
                break;
            IEnumerator row = RunRow(workTypeId);
            while (row.MoveNext())
                yield return row.Current;
        }
    }

    private IEnumerator RunP15AutomationModesFocused()
    {
        IEnumerator manual = RunP15NaturalExecutionModeArm(
            AutomationMode.Manual,
            "p15:manual");
        while (manual.MoveNext())
            yield return manual.Current;

        IEnumerator assisted = RunP15NaturalExecutionModeArm(
            AutomationMode.PoweredAssist,
            "p15:powered-assist");
        while (assisted.MoveNext())
            yield return assisted.Current;

        IEnumerator occupied = RunP15AllocatedWorkerTransitionArm();
        while (occupied.MoveNext())
            yield return occupied.Current;

        IEnumerator automatic = RunP15AutomaticExecutionArm();
        while (automatic.MoveNext())
            yield return automatic.Current;

        IEnumerator utilityFailure = RunP15UtilityFailureAtomicArm();
        while (utilityFailure.MoveNext())
            yield return utilityFailure.Current;

        IEnumerator persistentPause = RunP15PersistentCookPauseArm();
        while (persistentPause.MoveNext())
            yield return persistentPause.Current;
    }

    private IEnumerator RunP15NaturalExecutionModeArm(
        AutomationMode mode,
        string rowId)
    {
        P15FocusedFixture focused = null;
        string setupFailure = string.Empty;
        IEnumerator setup = PrepareP15FocusedFixture(
            value => focused = value,
            value => setupFailure = value);
        while (setup.MoveNext())
            yield return setup.Current;
        if (focused == null)
        {
            results.Add(new WorkTypeLiveRow(
                rowId,
                "FAIL",
                "P15",
                "fixture=" + setupFailure));
            yield break;
        }

        InfrastructureCommandResult modeCommand = automationCommands.SetMode(
            focused.Target,
            mode);
        float modeDeadline = Time.realtimeSinceStartup + 5f;
        AutomationFacilitySnapshot modeSnapshot = null;
        while (modeCommand.Succeeded
               && Time.realtimeSinceStartup < modeDeadline)
        {
            if (automation.TryGetFacility(focused.Target, out modeSnapshot)
                && modeSnapshot.Mode == mode
                && modeSnapshot.Powered
                && modeSnapshot.Operational)
            {
                break;
            }
            yield return null;
        }

        float speedMultiplier = automation.GetWorkSpeedMultiplier(focused.Target);
        ProductionBillSnapshot billBefore = FindFocusedBill(focused);
        SettlementLaborAccountingSnapshot laborBefore = settlementLabor.Capture();
        PrepareActor(BuiltInWorkTypeIds.Cook);
        WorkPhaseResult phase = null;
        IEnumerator phaseRun = RunLivePhase(
            BuiltInWorkTypeIds.Cook,
            focused.Target,
            WorkProbeFault.CancelAfterApprovedProgress,
            fixture: null,
            value => phase = value);
        while (phaseRun.MoveNext())
            yield return phaseRun.Current;
        ProductionBillSnapshot billAfter = FindFocusedBill(focused);
        SettlementLaborAccountingSnapshot laborAfter = settlementLabor.Capture();

        long actualDelta = DeltaCounterAcrossReset(
            laborBefore.ActualLaborMilliWu,
            laborAfter.ActualLaborMilliWu);
        long automaticDelta = DeltaCounterAcrossReset(
            laborBefore.DomainAutomationMilliWu,
            laborAfter.DomainAutomationMilliWu);
        float completedDelta = billBefore != null && billAfter != null
            ? billAfter.CompletedWork - billBefore.CompletedWork
            : 0f;
        bool modeReady = modeSnapshot != null
            && modeSnapshot.Mode == mode
            && modeSnapshot.Powered
            && modeSnapshot.Operational;
        bool multiplierValid = mode == AutomationMode.Manual
            ? Mathf.Approximately(speedMultiplier, 1f)
            : speedMultiplier > 1f;
        bool passed = modeCommand.Succeeded
            && modeReady
            && multiplierValid
            && phase?.Passed == true
            && completedDelta > 0f
            && actualDelta > 0L
            && automaticDelta == 0L;

        string detail = "modeCommand=" + modeCommand.Succeeded
            + "; modeReady=" + modeReady
            + "; multiplier=" + speedMultiplier.ToString("0.###")
            + "; naturalPhasePass=" + (phase?.Passed == true)
            + "; billProgress=" + (completedDelta > 0f)
            + "; actualLaborPositive=" + (actualDelta > 0L)
            + "; automaticLaborZero=" + (automaticDelta == 0L);
        if (!CloseP15FocusedFixture(focused, out string cleanupFailure))
        {
            passed = false;
            detail += "; cleanup=" + cleanupFailure;
        }
        results.Add(new WorkTypeLiveRow(
            rowId,
            passed ? "PASS" : "FAIL",
            "P15",
            detail));
    }

    private IEnumerator RunP15AllocatedWorkerTransitionArm()
    {
        const string RowId = "p15:allocated-worker-transition";
        P15FocusedFixture focused = null;
        string setupFailure = string.Empty;
        IEnumerator setup = PrepareP15FocusedFixture(
            value => focused = value,
            value => setupFailure = value);
        while (setup.MoveNext())
            yield return setup.Current;
        if (focused == null)
        {
            results.Add(new WorkTypeLiveRow(
                RowId,
                "FAIL",
                "P15",
                "fixture=" + setupFailure));
            yield break;
        }

        InfrastructureCommandResult manual = automationCommands.SetMode(
            focused.Target,
            AutomationMode.Manual);
        PrepareActor(BuiltInWorkTypeIds.Cook);
        WorkPhaseResult phase = null;
        bool occupancyOnlyObserved = false;
        bool transitionAttempted = false;
        InfrastructureCommandResult transition = default;
        IEnumerator phaseRun = RunLivePhase(
            BuiltInWorkTypeIds.Cook,
            focused.Target,
            WorkProbeFault.CancelAfterApprovedProgress,
            fixture: null,
            value => phase = value);
        while (phaseRun.MoveNext())
        {
            if (!transitionAttempted
                && focused.Target is IAllocatedWorkerOccupancyQuery occupancy
                && occupancy.HasAllocatedWorker
                && focused.Target.WorkerReservation == null
                && productionBills.GetBills(focused.Target).All(value =>
                    string.IsNullOrWhiteSpace(value.ReservedWorkerId)))
            {
                occupancyOnlyObserved = true;
                transitionAttempted = true;
                transition = automationCommands.SetMode(
                    focused.Target,
                    AutomationMode.Automatic);
            }
            yield return phaseRun.Current;
        }

        bool typedRejection = transitionAttempted
            && !transition.Succeeded
            && transition.Failure.Code == FailureCode.AutomationModeUnsupported
            && DomainFailureContains(
                transition.Failure,
                "automatic-mode-manual-worker-active");
        bool passed = manual.Succeeded
            && occupancyOnlyObserved
            && typedRejection
            && phase?.Passed == true;
        string detail = "manual=" + manual.Succeeded
            + "; occupancyOnly=" + occupancyOnlyObserved
            + "; transitionAttempted=" + transitionAttempted
            + "; typedRejection=" + typedRejection
            + "; naturalPhasePass=" + (phase?.Passed == true);
        if (!CloseP15FocusedFixture(focused, out string cleanupFailure))
        {
            passed = false;
            detail += "; cleanup=" + cleanupFailure;
        }
        results.Add(new WorkTypeLiveRow(
            RowId,
            passed ? "PASS" : "FAIL",
            "P15",
            detail));
    }

    private IEnumerator RunP15AutomaticExecutionArm()
    {
        const string RowId = "p15:automatic";
        P15FocusedFixture focused = null;
        string setupFailure = string.Empty;
        IEnumerator setup = PrepareP15FocusedFixture(
            value => focused = value,
            value => setupFailure = value);
        while (setup.MoveNext())
            yield return setup.Current;
        if (focused == null)
        {
            results.Add(new WorkTypeLiveRow(
                RowId,
                "FAIL",
                "P15",
                "fixture=" + setupFailure));
            yield break;
        }

        InfrastructureCommandResult automatic = automationCommands.SetMode(
            focused.Target,
            AutomationMode.Automatic);
        float modeDeadline = Time.realtimeSinceStartup + 5f;
        AutomationFacilitySnapshot modeSnapshot = null;
        while (automatic.Succeeded
               && Time.realtimeSinceStartup < modeDeadline)
        {
            if (automation.TryGetFacility(focused.Target, out modeSnapshot)
                && modeSnapshot.Mode == AutomationMode.Automatic
                && modeSnapshot.Powered
                && modeSnapshot.Operational)
            {
                break;
            }
            yield return null;
        }

        PrepareActor(BuiltInWorkTypeIds.Cook);
        runtimeScope.Container.Resolve<IFacilityCandidateCache>()
            .MarkDynamicStateDirty();
        yield return null;
        ProductionWorkAvailabilityResult availability =
            productionWork.CheckWorkAvailability(
                focused.Target,
                BuiltInWorkTypeIds.Cook);
        GridPathSearchResult search = grid.SearchPath(actor.GetNowXY());
        bool foundCandidate = work.TryGetBestWorkCandidate(
            BuiltInWorkTypeIds.Cook,
            search,
            out WorkTargetCandidate candidate);
        BuildableObject candidateTarget = foundCandidate
            ? WorkTargetCandidateRuntimeAdapter.ResolveBuilding(candidate)
            : null;
        bool priorityAccepted = work.TrySetPriorityWorkTarget(
            focused.Target,
            BuiltInWorkTypeIds.Cook,
            search,
            out string priorityFailure);
        ProductionWorkBeginResult begin = productionWork.BeginWork(
            actor,
            focused.Target,
            BuiltInWorkTypeIds.Cook);
        ProductionWorkExecutionResult execute = productionWork.ExecuteWork(
            actor,
            focused.Target,
            focused.BillId,
            0.25f);
        bool typedManualRejection = !availability.Available
            && DomainFailureContains(
                availability.Failure,
                ProductionWorkstationExecutionModeRules
                    .ManualDisabledByAutomaticMode)
            && !begin.Succeeded
            && DomainFailureContains(
                begin.Failure,
                ProductionWorkstationExecutionModeRules
                    .ManualDisabledByAutomaticMode)
            && !execute.Succeeded
            && DomainFailureContains(
                execute.Failure,
                ProductionWorkstationExecutionModeRules
                    .ManualDisabledByAutomaticMode)
            && !priorityAccepted;
        bool naturalTargetExcluded = !foundCandidate
            || !ReferenceEquals(candidateTarget, focused.Target);

        ProductionBillSnapshot billBefore = FindFocusedBill(focused);
        SettlementLaborAccountingSnapshot laborBefore = settlementLabor.Capture();
        long approvedBefore = work.ApprovedWorkProgressRevisionForDiagnostics;
        actor.SetAiPaused(false);
        brain.PreferWorkActionOnNextDecision(BuiltInWorkTypeIds.Cook, 2f);
        brain.RequestImmediateReplan(clearFailures: true);
        float progressDeadline = Time.realtimeSinceStartup + 12f;
        bool automaticProgressed = false;
        while (Time.realtimeSinceStartup < progressDeadline)
        {
            ProductionBillSnapshot current = FindFocusedBill(focused);
            SettlementLaborAccountingSnapshot currentLabor = settlementLabor.Capture();
            automaticProgressed = BillTokenChanged(billBefore, current)
                || DeltaCounterAcrossReset(
                    laborBefore.DomainAutomationMilliWu,
                    currentLabor.DomainAutomationMilliWu) > 0L;
            if (automaticProgressed)
                break;
            yield return null;
        }
        brain.StopCurrentActionForReplan(
            "p15 automatic execution-mode exclusion observation complete");
        actor.SetAiPaused(true);
        for (int frame = 0; frame < 2; frame++)
            yield return null;

        SettlementLaborAccountingSnapshot laborAfter = settlementLabor.Capture();
        long actualDelta = DeltaCounterAcrossReset(
            laborBefore.ActualLaborMilliWu,
            laborAfter.ActualLaborMilliWu);
        long automaticDelta = DeltaCounterAcrossReset(
            laborBefore.DomainAutomationMilliWu,
            laborAfter.DomainAutomationMilliWu);
        bool noNaturalProgress = work.ApprovedWorkProgressRevisionForDiagnostics
            == approvedBefore
            && !ReferenceEquals(work.assignedShop, focused.Target);
        bool noManualOwnership = focused.Target.WorkerReservation == null
            && (!(focused.Target is IAllocatedWorkerOccupancyQuery occupancy)
                || !occupancy.HasAllocatedWorker)
            && productionBills.GetBills(focused.Target).All(value =>
                string.IsNullOrWhiteSpace(value.ReservedWorkerId));

        DungeonAutomationSaveData savedMode = automationPersistence.Capture();
        InfrastructureCommandResult switchedManual = automationCommands.SetMode(
            focused.Target,
            AutomationMode.Manual);
        AutomationRestoreCandidate restoredMode = automationPersistence
            .PrepareRestore(savedMode);
        automationPersistence.Restore(restoredMode);
        yield return null;
        bool restoreExact = automation.TryGetFacility(
                focused.Target,
                out AutomationFacilitySnapshot restoredSnapshot)
            && restoredSnapshot.Mode == AutomationMode.Automatic
            && !productionWork.CheckWorkAvailability(
                focused.Target,
                BuiltInWorkTypeIds.Cook).Available;

        bool modeReady = modeSnapshot != null
            && modeSnapshot.Mode == AutomationMode.Automatic
            && modeSnapshot.Powered
            && modeSnapshot.Operational;
        bool passed = automatic.Succeeded
            && modeReady
            && typedManualRejection
            && naturalTargetExcluded
            && noNaturalProgress
            && automaticProgressed
            && automaticDelta > 0L
            && actualDelta == 0L
            && noManualOwnership
            && switchedManual.Succeeded
            && restoreExact;
        string detail = "modeCommand=" + automatic.Succeeded
            + "; modeReady=" + modeReady
            + "; typedManualRejection=" + typedManualRejection
            + "; priorityAccepted=" + priorityAccepted
            + "; naturalTargetExcluded=" + naturalTargetExcluded
            + "; noNaturalProgress=" + noNaturalProgress
            + "; automaticProgress=" + automaticProgressed
            + "; actualLaborZero=" + (actualDelta == 0L)
            + "; automaticLaborPositive=" + (automaticDelta > 0L)
            + "; noManualOwnership=" + noManualOwnership
            + "; restoreExact=" + restoreExact;
        if (!CloseP15FocusedFixture(focused, out string cleanupFailure))
        {
            passed = false;
            detail += "; cleanup=" + cleanupFailure;
        }
        results.Add(new WorkTypeLiveRow(
            RowId,
            passed ? "PASS" : "FAIL",
            "P15",
            detail));
    }

    private IEnumerator PrepareP15FocusedFixture(
        Action<P15FocusedFixture> completed,
        Action<string> failed)
    {
        if (!CleanupRowScopedFixtures(out string priorCleanupFailure))
        {
            failed("prior-cleanup=" + priorCleanupFailure);
            yield break;
        }
        if (!CleanupRoomFixture(out string priorRoomCleanupFailure))
        {
            failed("prior-room-cleanup=" + priorRoomCleanupFailure);
            yield break;
        }
        MaintainStableWorkSubject();
        actor.SetAiPaused(true);
        brain.StopCurrentActionForReplan(
            "P15 focused fixture deterministic setup");
        actor.GetAbility<AbilityMove>()?.CancelActiveMovement(
            "P15 focused fixture deterministic setup");
        yield return null;
        yield return null;
        if (!TryPlacePoweredP15Pair(
                out BuildableObject target,
                out string placementFailure))
        {
            failed("placement=" + placementFailure);
            yield break;
        }

        float powerDeadline = Time.realtimeSinceStartup + 8f;
        while (!power.IsPowered(target)
               && Time.realtimeSinceStartup < powerDeadline)
        {
            yield return null;
        }
        if (!power.IsPowered(target))
        {
            failed("P15 did not join the adjacent fuel-free I02 power network");
            yield break;
        }

        if (!TryPrepareProductionFixture(
                BuiltInWorkTypeIds.Cook,
                out MaterialWorkFixture fixture,
                out string fixtureFailure,
                target))
        {
            failed("bill=" + fixtureFailure);
            yield break;
        }
        ProductionBillSnapshot bill = productionBills.GetBills(target)
            .SingleOrDefault(value => value.RecipeId == "recipe:tallow");
        if (bill == null)
        {
            failed("prepared P15 tallow bill is missing");
            yield break;
        }

        runtimeScope.Container.Resolve<IFacilityCandidateCache>()
            .MarkDynamicStateDirty();
        yield return null;
        completed(new P15FocusedFixture(target, fixture, bill.BillId));
    }

    private IEnumerator RunP15UtilityFailureAtomicArm()
    {
        const string RowId = "p15:utility-failure-atomic";
        P15FocusedFixture focused = null;
        string setupFailure = string.Empty;
        IEnumerator setup = PrepareP15FocusedFixture(
            value => focused = value,
            value => setupFailure = value);
        while (setup.MoveNext())
            yield return setup.Current;
        if (focused == null)
        {
            results.Add(new WorkTypeLiveRow(
                RowId,
                "FAIL",
                "P15",
                "fixture=" + setupFailure));
            yield break;
        }

        InfrastructureCommandResult manual = automationCommands.SetMode(
            focused.Target,
            AutomationMode.Manual);
        IFluidWastewaterTransaction wastewater =
            runtimeScope.Container.Resolve<IFluidWastewaterTransaction>();
        bool fullyAccepted = wastewater.TryAddWastewater(
            focused.Target,
            100000f,
            out float acceptedWastewater,
            out DomainFailure overflowFailure);
        ProductionWorkAvailabilityResult availability =
            productionWork.CheckWorkAvailability(
                focused.Target,
                BuiltInWorkTypeIds.Cook);
        ProductionBillSnapshot billBefore = FindFocusedBill(focused);
        string physicalBefore = CaptureP15PhysicalStackFingerprint();
        int sludgeBefore = CapturePhysicalItemQuantity(
            IndustrialItemDefinitions.SludgeId);
        int waterBefore = CapturePhysicalItemQuantity("resource:clean-water");
        bool delegatedWork = false;
        IWorkExecutionHandlerRegistry handlers =
            runtimeScope.Container.Resolve<IWorkExecutionHandlerRegistry>();
        bool handlerResolved = handlers.TryGet(
            BuiltInWorkTypeIds.Cook,
            out IWorkExecutionHandler handler);
        WorkExecutionResult executionResult = new();
        if (handlerResolved)
        {
            WorkExecutionContext context = new(
                0,
                work,
                actor,
                focused.Target,
                BuiltInWorkTypeIds.Cook,
                (required, label, multiplier) =>
                {
                    delegatedWork = true;
                    return EmptyP15FocusedCoroutine();
                },
                () => true,
                (required, completed, label, multiplier, apply) =>
                {
                    delegatedWork = true;
                    return EmptyP15FocusedCoroutine();
                });
            IEnumerator execution = handler.Execute(context, executionResult);
            while (execution.MoveNext())
                yield return execution.Current;
        }

        ProductionBillSnapshot billAfter = FindFocusedBill(focused);
        string physicalAfter = CaptureP15PhysicalStackFingerprint();
        int sludgeAfter = CapturePhysicalItemQuantity(
            IndustrialItemDefinitions.SludgeId);
        int waterAfter = CapturePhysicalItemQuantity("resource:clean-water");
        bool utilityRejected = !availability.Available
            && availability.Failure.Code
                == FailureCode.ProductionUtilitiesUnavailable
            && DomainFailureContains(
                availability.Failure,
                FailureCode.FluidWastewaterUnavailable.ToString());
        bool billUnchanged = billBefore != null
            && billAfter != null
            && billAfter.BillId == billBefore.BillId
            && billAfter.MaterialsConsumed == billBefore.MaterialsConsumed
            && billAfter.ProcessFluidConsumed == billBefore.ProcessFluidConsumed
            && Mathf.Approximately(
                billAfter.CompletedWork,
                billBefore.CompletedWork)
            && billAfter.RemainingCycles == billBefore.RemainingCycles;
        bool passed = manual.Succeeded
            && !fullyAccepted
            && acceptedWastewater > 0f
            && overflowFailure.IsFailure
            && utilityRejected
            && handlerResolved
            && !executionResult.CompletedSuccessfully
            && !delegatedWork
            && billUnchanged
            && string.Equals(
                physicalAfter,
                physicalBefore,
                StringComparison.Ordinal)
            && sludgeAfter == sludgeBefore
            && waterAfter == waterBefore;
        string detail = "manual=" + manual.Succeeded
            + "; tankFilled=" + (!fullyAccepted && acceptedWastewater > 0f)
            + "; overflowTyped=" + overflowFailure.IsFailure
            + "; utilityRejected=" + utilityRejected
            + "; handlerResolved=" + handlerResolved
            + "; handlerRejected=" + !executionResult.CompletedSuccessfully
            + "; delegatedWork=" + delegatedWork
            + "; billUnchanged=" + billUnchanged
            + "; physicalUnchanged=" + string.Equals(
                physicalAfter,
                physicalBefore,
                StringComparison.Ordinal)
            + "; waterUnchanged=" + (waterAfter == waterBefore)
            + "; sludgeUnchanged=" + (sludgeAfter == sludgeBefore);
        if (!CloseP15FocusedFixture(focused, out string cleanupFailure))
        {
            passed = false;
            detail += "; cleanup=" + cleanupFailure;
        }
        results.Add(new WorkTypeLiveRow(
            RowId,
            passed ? "PASS" : "FAIL",
            "P15",
            detail));
    }

    private IEnumerator RunP15PersistentCookPauseArm()
    {
        const string RowId = "p15:persistent-cook-pause";
        P15FocusedFixture focused = null;
        string setupFailure = string.Empty;
        IEnumerator setup = PrepareP15FocusedFixture(
            value => focused = value,
            value => setupFailure = value);
        while (setup.MoveNext())
            yield return setup.Current;
        if (focused == null)
        {
            results.Add(new WorkTypeLiveRow(
                RowId,
                "FAIL",
                "P15",
                "fixture=" + setupFailure));
            yield break;
        }

        InfrastructureCommandResult manual = automationCommands.SetMode(
            focused.Target,
            AutomationMode.Manual);
        ProductionBillSnapshot billBefore = FindFocusedBill(focused);
        PrepareActor(BuiltInWorkTypeIds.Cook);
        WorkPhaseResult phase = null;
        IEnumerator phaseRun = RunLivePhase(
            BuiltInWorkTypeIds.Cook,
            focused.Target,
            WorkProbeFault.PauseAfterApprovedProgress,
            fixture: null,
            complete: value => phase = value,
            persistentPauseFixture: focused);
        while (phaseRun.MoveNext())
            yield return phaseRun.Current;
        ProductionBillSnapshot billAfter = FindFocusedBill(focused);

        float completedDelta = billBefore != null && billAfter != null
            ? billAfter.CompletedWork - billBefore.CompletedWork
            : 0f;
        bool passed = manual.Succeeded
            && phase?.Passed == true
            && completedDelta > 0f;
        string detail = "manual=" + manual.Succeeded
            + "; billProgress=" + (completedDelta > 0f)
            + "; phase=" + (phase?.Format() ?? "missing");
        if (!CloseP15FocusedFixture(focused, out string cleanupFailure))
        {
            passed = false;
            detail += "; cleanup=" + cleanupFailure;
        }
        results.Add(new WorkTypeLiveRow(
            RowId,
            passed ? "PASS" : "FAIL",
            "P15",
            detail));
    }

    private void VerifyW05HarvestTerminalIdentity(int identityBaseline)
    {
        CharacterId worker = CharacterPersistentIdentity.Require(actor);
        WorkCompletedIdentityEvent[] observed = w05IdentityEvents
            .Skip(identityBaseline)
            .Where(value => value.Character.Equals(worker)
                && string.Equals(
                    value.WorkId,
                    BuiltInWorkTypeIds.Harvest.Value,
                    StringComparison.Ordinal))
            .ToArray();
        WorkCompletedIdentityEvent[] eventBusDomain = observed
            .Where(value => !string.IsNullOrWhiteSpace(value.ProductId)
                && !value.ProductId.StartsWith("outcome:", StringComparison.Ordinal)
                && !value.ProductId.StartsWith("checkpoint:", StringComparison.Ordinal))
            .ToArray();
        int eventBusGenericCount = observed.Count(value =>
            string.IsNullOrWhiteSpace(value.ProductId));
        string domainStreamId = CropHarvestCompletionObservation.StreamPrefix
            + w05HarvestPlotId;
        WorkCompletionIdentityDeliveryCursorSaveData domainBefore =
            FindW05DeliveryCursor(w05DeliveryBaseline, domainStreamId);
        WorkCompletionIdentityDeliveryCursorSaveData directBefore =
            FindW05DeliveryCursor(
                w05DeliveryBaseline,
                WorkCompletionIdentityDeliveryLedger.DirectEventStreamId);
        IReadOnlyList<WorkCompletionIdentityDeliveryCursorSaveData> after =
            w05IdentityDeliveries?.Capture()
            ?? Array.Empty<WorkCompletionIdentityDeliveryCursorSaveData>();
        WorkCompletionIdentityDeliveryCursorSaveData domainAfter =
            FindW05DeliveryCursor(after, domainStreamId);
        WorkCompletionIdentityDeliveryCursorSaveData directAfter =
            FindW05DeliveryCursor(
                after,
                WorkCompletionIdentityDeliveryLedger.DirectEventStreamId);
        bool domainAppliedExactlyOnce = domainBefore == null
            && domainAfter != null
            && domainAfter.operationSequence == 0
            && domainAfter.disposition ==
                WorkCompletionIdentityDeliveryDisposition.EffectsAppliedWithOutcome
            && domainAfter.deliveryId.StartsWith(
                CropHarvestCompletionObservation.DeliveryPrefix,
                StringComparison.Ordinal)
            && domainAfter.payloadFingerprint?.Length == 64;
        bool directStreamUnchanged = W05DeliveryCursorEquals(
            directBefore,
            directAfter);
        bool passed = domainAppliedExactlyOnce
            && directStreamUnchanged
            && eventBusDomain.Length == 0
            && eventBusGenericCount == 0;
        results.Add(new WorkTypeLiveRow(
            "w05:harvest-terminal-identity",
            passed ? "PASS" : "FAIL",
            actor?.Identity?.PersistentId ?? "-",
            "domainDelivery=" + domainAppliedExactlyOnce
                + "; domainStream=" + domainStreamId
                + "; domainSequence="
                + (domainAfter?.operationSequence.ToString() ?? "missing")
                + "; domainDisposition="
                + (domainAfter?.disposition.ToString() ?? "missing")
                + "; directStreamUnchanged=" + directStreamUnchanged
                + "; eventBusDomain=" + eventBusDomain.Length
                + "; eventBusGeneric=" + eventBusGenericCount
                + "; allHarvestEvents=" + observed.Length));
        VerifyW01HarvestTerminalChildJoin(domainStreamId, worker);
    }

    private void VerifyW01HarvestTerminalChildJoin(
        string producerStreamId,
        CharacterId worker)
    {
        IGameplayOutcomeQuery query = runtimeScope.Container.Resolve<IGameplayOutcomeQuery>();
        List<GameplayOutcomeSnapshot> exact = new();
        OutcomeCursor cursor = OutcomeCursor.FirstPage(200);
        while (true)
        {
            GameplayOutcomeQueryPage page = query.GetGlobal(cursor, OutcomeFilter.All);
            if (page.Items.Count == 0)
                break;
            exact.AddRange(page.Items.Where(value => value.Exact != null)
                .Select(value => value.Exact));
            cursor = page.NextCursor;
        }

        GameplayOutcomeSnapshot[] children = exact.Where(value =>
            value.outcomeTypeId == EnvironmentOutcomeIds.CropPlanTerminal.Value
            && value.participants.Any(participant =>
                participant.entityId == w05HarvestPlotId)).ToArray();
        GameplayOutcomeSnapshot[] identities = exact.Where(value =>
            value.outcomeTypeId == WorkCompletionIdentityOutcomeIds.Applied.Value
            && Fact(value, WorkCompletionIdentityOutcomeIds.ProducerStreamFact)
                == producerStreamId).ToArray();
        bool singlePair = children.Length == 1 && identities.Length == 1;
        GameplayOutcomeSnapshot child = singlePair ? children[0] : null;
        GameplayOutcomeSnapshot identity = singlePair ? identities[0] : null;
        bool exactChild = singlePair
            && child.status == GameplayOutcomeStatus.Succeeded
            && child.immutablePayloadHash?.Length == 64
            && Fact(child, EnvironmentOutcomeIds.HarvesterFact) == worker.Value;
        bool childFirst = exactChild && child.sequence < identity.sequence;
        string childKey = child == null ? "missing" : new GameplayResultKey(
            child.producerId, new GameplayOperationId(child.operationId),
            child.commitRevision, child.localResultIndex).ToString();
        results.Add(new WorkTypeLiveRow(
            "w01:harvest-child-before-identity",
            childFirst ? "PASS" : "FAIL",
            w05HarvestPlotId,
            "children=" + children.Length + "; identities=" + identities.Length
                + "; childExact=" + exactChild
                + "; childSequence=" + (child?.sequence.ToString() ?? "missing")
                + "; identitySequence=" + (identity?.sequence.ToString() ?? "missing")
                + "; child=" + childKey));

        bool joined = exactChild
            && Fact(identity, WorkCompletionIdentityOutcomeIds.ChildResultKeyFact) == childKey
            && Fact(identity, WorkCompletionIdentityOutcomeIds.OperationIdFact) == child.operationId
            && Fact(identity, WorkCompletionIdentityOutcomeIds.TerminalOwnerFact) == child.producerId
            && identity.absoluteDay == child.absoluteDay
            && identity.participants.Any(participant => participant.entityId == worker.Value)
            && identity.subjects.Any(subject =>
                subject.entityKindId == WorkCompletionIdentityOutcomeIds.BuildingKind.Value
                && subject.entityId == w05HarvestPlotId);
        results.Add(new WorkTypeLiveRow(
            "w01:harvest-terminal-child-join",
            joined ? "PASS" : "FAIL",
            worker.Value,
            "joined=" + joined + "; child=" + childKey
                + "; identityChild=" + Fact(identity, WorkCompletionIdentityOutcomeIds.ChildResultKeyFact)
                + "; identityOperation=" + Fact(identity, WorkCompletionIdentityOutcomeIds.OperationIdFact)
                + "; identityOwner=" + Fact(identity, WorkCompletionIdentityOutcomeIds.TerminalOwnerFact)
                + "; plot=" + w05HarvestPlotId));

        static string Fact(GameplayOutcomeSnapshot outcome, GameplayOutcomeFactId fact) =>
            outcome?.facts.FirstOrDefault(value => value.factId == fact.Value)?.value ?? "missing";
    }

    private static WorkCompletionIdentityDeliveryCursorSaveData
        FindW05DeliveryCursor(
            IEnumerable<WorkCompletionIdentityDeliveryCursorSaveData> cursors,
            string streamId) => (cursors ?? Array.Empty<
            WorkCompletionIdentityDeliveryCursorSaveData>())
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.producerStreamId,
                    streamId,
                    StringComparison.Ordinal));

    private static bool W05DeliveryCursorEquals(
        WorkCompletionIdentityDeliveryCursorSaveData left,
        WorkCompletionIdentityDeliveryCursorSaveData right)
    {
        if (left == null || right == null)
            return left == null && right == null;
        return string.Equals(
                left.producerStreamId,
                right.producerStreamId,
                StringComparison.Ordinal)
            && left.operationSequence == right.operationSequence
            && string.Equals(
                left.deliveryId,
                right.deliveryId,
                StringComparison.Ordinal)
            && string.Equals(
                left.payloadFingerprint,
                right.payloadFingerprint,
                StringComparison.Ordinal)
            && left.disposition == right.disposition;
    }

    private IEnumerator RunW03MedicalLaborFocused()
    {
        if (!Application.isBatchMode || !DungeonPersistencePaths.IsIsolatedVerification)
            throw new InvalidOperationException("W03 requires the owned isolated PlayMode entry.");
        Time.timeScale = 4f;
        PrepareActor(BuiltInWorkTypeIds.Rescue);
        var medical = runtimeScope.Container.Resolve<ICharacterMedicalQuery>();
        var commands = runtimeScope.Container.Resolve<ICharacterMedicalCommand>();
        var medicalPersistence = runtimeScope.Container.Resolve<ICharacterMedicalPersistence>();
        var laborPersistence = runtimeScope.Container.Resolve<ISettlementLaborPersistence>();
        var bodies = runtimeScope.Container.Resolve<CharacterBodyHealthRuntime>();
        var events = runtimeScope.Container.Resolve<IGameEventBus>();
        string workerId = actor.Identity.PersistentId;
        CharacterActor patient = LiveActors(worldRegistry).First(value => value != actor);
        string patientId = patient.Identity.PersistentId;
        string carrierId = LiveActors(worldRegistry).First(value => value != actor && value != patient)
            .Identity.PersistentId;
        w05IdentitySubscription = events.Subscribe<WorkCompletedIdentityEvent>(w05IdentityEvents.Add);
        string output = Path.GetDirectoryName(CharacterAiWorkTypeLiveMatrixPlayModeVerifier.W03MedicalLaborReportPath);
        string orderId = string.Empty;
        bool sawAbility = false, sawCarriedPatient = false, sawTreatment = false;
        bool capturedSkillEvidenceFailure = false;
        int deliveries = 0;
        string medicineId = string.Empty;
        float firstPartial = 0f, secondPartial = 0f;
        long initialLabor = TotalLabor(), repairLabor = 0L;

        BuildingSO bedDefinition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Medical/M01_응급처치대.asset");
        BuildableObject bed = PlaceAuthoredBuilding(bedDefinition, out string bedFailure);
        Require(bed != null && bed.BuildingData.GetAbility<BuildingMedicalAbility>() != null,
            "authored injury treatment bed: " + bedFailure);
        string bedId = bed.RequirePersistentInstanceId().Value;
        runtimeScope.Container.Resolve<IFacilityCandidateCache>().MarkDynamicStateDirty();
        Vector2Int[] cells = grid.GetCells()
            .Where(value => value != null && value.AreaType == GridCellAreaType.DungeonInterior
                && grid.IsWalkable(value.Position) && !bed.buildPoses.Contains(value.Position))
            .OrderBy(value => Vector2Int.Distance(value.Position, bed.centerPos))
            .ThenBy(value => value.Position.y).ThenBy(value => value.Position.x)
            .Select(value => value.Position).Take(2).ToArray();
        Require(cells.Length == 2, "two reachable fixture actor cells");
        Position(patient, cells[0]);
        Position(actor, cells[1]);
        SetNeutralNeeds(patient);
        AnatomyHealthSnapshot healthy = bodies.GetAnatomySnapshot(patient);
        Require(healthy.ProfileId == "anatomy:slime",
            "authored starter patient must have the expected slime anatomy");
        AnatomyNodeHealthState membrane = healthy.Nodes.Single(value => value.nodeId == "membrane");
        // The authored starter is a Slime: all non-head compatibility parts
        // alias its membrane. Use the typed damage command so later aliases
        // cannot overwrite bleeding, and let the body authority create Downed.
        Require(bodies.TryDamageNode(patient, membrane.nodeId,
                membrane.currentHealth - membrane.maxHealth * 0.18f, 0.01f,
                "qa:w03:controlled-anatomy-injury"), "typed membrane injury rejected");
        yield return null;
        yield return null;
        CharacterMedicalOrder order = medical.ActiveOrders.Single(value =>
            value.IsActive && value.patientId == patientId);
        orderId = order.orderId;
        Require(patient.CurrentLifecycleState == CharacterLifecycleState.Downed
            && bodies.GetTotalBleeding(patient) > 0.001f && !order.stabilized
            && order.completedStabilizationWork == 0f
            && order.state == CharacterMedicalOrderState.AwaitingStabilization,
            "controlled anatomy injury must require real stabilization: " + Describe());
        var ranked = CharacterMedicalSupplyPolicy.RankMedicines(
            economyContent.Items.Where(value => value != null
                && value.Kind == ResourceItemKind.Medicine && value.SupportsInjuryTreatment)
                .Select(value => new CharacterMedicalMedicineCandidate(value.ItemId, value.UnitPrice,
                    value.TreatmentPotency, value.InfectionReduction, value.PainReduction)).ToArray(),
            order.requiredTreatmentWork);
        Require(ranked.Count > 0, "authored injury medicine missing");
        medicineId = ranked[0].ItemId;
        var warehouse = worldRegistry.Warehouses.FirstOrDefault(value => value != null
            && value.HasWarehouseInventory && value.Inventory.Accepts(StockCategory.Medicine)
            && value.Inventory.HasMassCapacityAuthority);
        Require(warehouse != null, "physical medicine warehouse missing");
        Require(runtimeScope.Container.Resolve<WorldItemWarehouseService>().SpawnItemStock(
                warehouse, medicineId, 8, "qa:w03:medical-labor", "generic:" + medicineId,
                out int spawned, out _, out DomainFailure spawnFailure) && spawned == 8,
            "controlled physical medicine admission: " + spawnFailure);
        Write("w03-initial-world.json", gameSaveService.ToJson(gameSaveService.Capture(), true));
        StartRescue();
        IEnumerator wait = WaitUntil(() => Current().completedStabilizationWork > 0.05f,
            "initial partial stabilization", false);
        while (wait.MoveNext()) yield return wait.Current;
        order = Current();
        firstPartial = order.completedStabilizationWork;
        Require(sawAbility && firstPartial < order.requiredStabilizationWork && !order.stabilized,
            "actual AbilityRescue partial observation missing: " + Describe());
        Require(TotalLabor() - initialLabor == EmergencyWuUnits.FromWu(firstPartial),
            "initial approved stabilization labor differs from real accounting");
        actor.SetAiPaused(true);
        brain.StopCurrentActionForReplan("qa:w03:cancel-medical-A");
        yield return null;
        order = Current();
        WorkCompletedIdentityEvent[] checkpoints = w05IdentityEvents.Where(value =>
            value.TargetEntityId == patientId
            && value.ProductId.StartsWith("checkpoint:medical-work-suspended:", StringComparison.Ordinal)).ToArray();
        Require(checkpoints.Length == 1 && checkpoints[0].WorkId == BuiltInWorkTypeIds.Treat.Value
            && checkpoints[0].OperationId.StartsWith("medical-work-checkpoint:" + orderId + ":", StringComparison.Ordinal)
            && checkpoints[0].TerminalOwnerId == WorkCompletionIdentityReferenceDefaults.CheckpointTerminalOwner
            && Mathf.Approximately(checkpoints[0].ContributionWorkUnits, firstPartial)
            && string.IsNullOrEmpty(order.rescuerId)
            && order.currentReservationStabilizationWork == 0f
            && Mathf.Approximately(order.completedStabilizationWork, firstPartial)
            && actor.GetComponent<AbilityRescue>()?.IsRescuing != true,
            "cancel did not preserve exact partial work/checkpoint and release rescue: " + Describe());
        Check("w03:rescue-partial-cancel", "work=" + firstPartial + "; sequence="
            + order.stabilizationLaborSequence + "; checkpoint=" + checkpoints[0].OperationId
            + "; target=" + checkpoints[0].TargetEntityId + "; labor=" + (TotalLabor() - initialLabor));

        MaterialWorkFixture repair = PrepareRepairFixture(out string repairFailure);
        Require(repair != null, "intervening repair fixture: " + repairFailure);
        w01RepairObservation = new W01RepairHandlerObservation(
            runtimeScope.Container.Resolve<IWorkExecutionHandlerRegistry>());
        PrepareActor(BuiltInWorkTypeIds.Repair);
        long laborBeforeRepair = TotalLabor();
        double repairCarryBefore = ExecutorLaborCarry(requireExecutor: false);
        WorkPhaseResult repairResult = null;
        IEnumerator repairRun = RunLivePhase(BuiltInWorkTypeIds.Repair, repair.Target,
            WorkProbeFault.CompleteNormally, repair, value => repairResult = value,
            completionAuthoritySatisfied: () => w01RepairObservation.CompletedCalls == 1
                && runtimeScope.Container.Resolve<IBuildingStructuralIntegrityRuntime>().TryGet(
                    repair.Target, out BuildingStructuralIntegritySnapshot state)
                && Mathf.Approximately(state.CurrentHitPoints, state.MaxHitPoints));
        while (repairRun.MoveNext()) yield return repairRun.Current;
        actor.SetAiPaused(true);
        brain.StopCurrentActionForReplan("qa:w03:repair-B-completed");
        work.ClearPriorityWorkTarget();
        repairLabor = TotalLabor() - laborBeforeRepair;
        Write("w03-after-repair-world.json", gameSaveService.ToJson(gameSaveService.Capture(), true));
        double repairCarry = ExecutorLaborCarry(requireExecutor: true);
        float returnedRepairWu = w01RepairObservation.Result?.ContributionWorkUnits ?? float.NaN;
        double exactRepairMilli = (double)returnedRepairWu * EmergencyWuUnits.UnitsPerWu;
        string repairAccounting = "actualMilli=" + repairLabor
            + "; returnedWu=" + returnedRepairWu
            + "; executorCarry=" + repairCarryBefore + "->" + repairCarry
            + "; calls=" + w01RepairObservation.CompletedCalls
            + "; completed=" + w01RepairObservation.Result?.CompletedSuccessfully
            + "; medicalA=" + Current().completedStabilizationWork + "/" + firstPartial;
        results.Add(WorkTypeLiveRow.Info("w03-repair-accounting", repairAccounting));
        Require(repairResult != null && repairResult.Passed && w01RepairObservation.CompletedCalls == 1
            && w01RepairObservation.Result.CompletedSuccessfully && repairLabor > 0
            && w01RepairObservation.Result.ContributionWorkUnits.HasValue
            && float.IsFinite(returnedRepairWu)
            && repairLabor == checked((long)Math.Floor(exactRepairMilli + repairCarryBefore + 1e-9d))
            && repairCarry >= 0d && repairCarry < 1d
            // Exact integer accounting plus the executor's retained fraction.
            // Only float accumulation error (< 0.001 milliWU) is tolerated.
            && Math.Abs(repairLabor + repairCarry - repairCarryBefore - exactRepairMilli) < 0.001d
            && Mathf.Approximately(Current().completedStabilizationWork, firstPartial),
            "actual B completion or preservation of A failed: " + repairAccounting + "; "
                + (repairResult == null ? "missing" : repairResult.Format()));
        Check("w03:intervening-repair", repairResult.Format() + "; actualLaborMilli=" + repairLabor
            + "; typedChild=" + w01RepairObservation.Result.ChildResultKey + "; medicalA=" + firstPartial);
        w01RepairObservation.Dispose();
        w01RepairObservation = null;

        StartRescue();
        wait = WaitUntil(() => Current().completedStabilizationWork > firstPartial + 0.05f,
            "resumed partial stabilization", false);
        while (wait.MoveNext()) yield return wait.Current;
        secondPartial = Current().completedStabilizationWork;
        Require(secondPartial < Current().requiredStabilizationWork && !Current().stabilized,
            "resumed A must still be partial before official capture");
        long beforeSaveLabor = TotalLabor();
        long expectedBeforeSave = EmergencyWuUnits.FromWu(firstPartial)
            + EmergencyWuUnits.FromWu(secondPartial - firstPartial) + repairLabor;
        Require(beforeSaveLabor - initialLabor == expectedBeforeSave,
            "A -> B -> A approved labor differs before save");
        DungeonGameSaveData pending = gameSaveService.Capture();
        Write("w03-partial-world.json", gameSaveService.ToJson(pending, true));
        string pendingMedical = JsonUtility.ToJson(medicalPersistence.Capture());
        string pendingLabor = JsonUtility.ToJson(laborPersistence.CaptureLaborSaveData());
        CharacterActor previousWorker = actor, previousPatient = patient;
        BuildableObject previousBed = bed;
        Require(gameSaveService.TryRestore(gameSaveService.FromJson(
                gameSaveService.ToJson(pending)), out DungeonGameRestoreReport restored) && restored.Success,
            "official partial restore failed: " + string.Join(" | ", restored.Errors));
        RefreshActors();
        bed = worldRegistry.Buildings.Single(value => value != null && value.PersistentInstanceId.Value == bedId);
        Require(pending.sections.Count == 81 && !ReferenceEquals(previousWorker, actor)
            && !ReferenceEquals(previousPatient, patient) && !ReferenceEquals(previousBed, bed)
            && Mathf.Approximately(Current().completedStabilizationWork, secondPartial)
            && TotalLabor() == beforeSaveLabor
            && pendingLabor == JsonUtility.ToJson(laborPersistence.CaptureLaborSaveData()),
            "official restore changed approved labor/progress or retained old objects");
        Write("w03-partial-restored-medical.json", JsonUtility.ToJson(medicalPersistence.Capture(), true));
        Write("w03-partial-medical-before.json", pendingMedical);
        Write("w03-partial-restored-world.json", gameSaveService.ToJson(gameSaveService.Capture(), true));
        Check("w03:partial-official-restore", "sections=" + pending.sections.Count + "; freshWorker/Patient/Bed=True"
            + "; completed=" + secondPartial + "; labor=" + beforeSaveLabor
            + "; restoredReservation=" + Current().rescuerId);

        StartRescue();
        wait = WaitUntil(() => !Current().IsActive
                && patient.CurrentLifecycleState == CharacterLifecycleState.Active
                && actor.GetComponent<AbilityRescue>()?.IsRescuing != true,
            "fresh actual stabilization, carry and treatment", true);
        while (wait.MoveNext()) yield return wait.Current;
        actor.SetAiPaused(true);
        brain.StopCurrentActionForReplan("qa:w03:medical-completed");
        GameplayOutcomeSnapshot[] outcomes = MedicalOutcomes();
        GameplayOutcomeSnapshot[] stabilization = outcomes.Where(value =>
            Fact(value, CharacterMedicalOutcomeIds.Procedure.Value) == "stabilization").ToArray();
        GameplayOutcomeSnapshot[] treatments = outcomes.Where(value =>
            Fact(value, CharacterMedicalOutcomeIds.Procedure.Value) == "treatment").ToArray();
        Require(stabilization.Length == 1 && treatments.Length > 0 && sawCarriedPatient && sawTreatment
            && outcomes.All(value => value.participants.Any(person =>
                person.roleId == CharacterMedicalOutcomeIds.Patient.Value && person.entityId == patientId)
                && value.participants.Any(person =>
                    person.roleId == CharacterMedicalOutcomeIds.Clinician.Value && person.entityId == workerId)),
            "actual stabilization/carry/treatment result or exact participants missing");
        float stabilizationTotal = (float)Metric(stabilization[0], CharacterMedicalOutcomeIds.CompletedWork.Value);
        long expectedMedical = EmergencyWuUnits.FromWu(firstPartial)
            + EmergencyWuUnits.FromWu(secondPartial - firstPartial)
            + EmergencyWuUnits.FromWu(stabilizationTotal - secondPartial)
            + treatments.Sum(value => EmergencyWuUnits.FromWu(
                (float)Metric(value, CharacterMedicalOutcomeIds.CompletedWork.Value)));
        Require(TotalLabor() - initialLabor == expectedMedical + repairLabor,
            "completed actual labor differs: recorded=" + (TotalLabor() - initialLabor)
                + "; medical=" + expectedMedical + "; repair=" + repairLabor);
        Require(treatments.Any(value => Metric(value, CharacterMedicalOutcomeIds.HealthAfter.Value)
                > Metric(value, CharacterMedicalOutcomeIds.HealthBefore.Value))
            && !bodies.GetSnapshot(patient).Downed, "treatment produced no physical recovery");
        Check("w03:actual-medical-completion", "stabilizations=" + stabilization.Length
            + "; treatments=" + treatments.Length + "; physicalCarry=" + sawCarriedPatient
            + "; controlledPhysicalDeliveries=" + deliveries + "; medicalMilli=" + expectedMedical
            + "; repairMilli=" + repairLabor + "; allActualMilli=" + (TotalLabor() - initialLabor));

        DungeonGameSaveData completed = gameSaveService.Capture();
        Write("w03-completed-world.json", gameSaveService.ToJson(completed, true));
        string completedSections = W05SectionPayloads(completed);
        string outcomeHashes = string.Join("|", outcomes.Select(value => value.immutablePayloadHash));
        // The production command rejects the completed owner, not a fixture
        // stage guard. Repeated calls must leave every official section intact.
        for (int attempt = 0; attempt < 2; attempt++)
        {
            commands.AdvanceStabilization(orderId, actor, 10f);
            commands.AdvanceTreatment(orderId, actor, 10f);
        }
        Require(completedSections == W05SectionPayloads(gameSaveService.Capture()),
            "completed live medical command replay changed official state");
        previousWorker = actor;
        Require(gameSaveService.TryRestore(gameSaveService.FromJson(gameSaveService.ToJson(completed)),
                out DungeonGameRestoreReport terminalRestore) && terminalRestore.Success,
            "official completed restore failed: " + string.Join(" | ", terminalRestore.Errors));
        RefreshActors();
        string restoredSections = W05SectionPayloads(gameSaveService.Capture());
        for (int attempt = 0; attempt < 2; attempt++)
        {
            commands.AdvanceStabilization(orderId, actor, 10f);
            commands.AdvanceTreatment(orderId, actor, 10f);
        }
        Require(!ReferenceEquals(previousWorker, actor)
            && restoredSections == W05SectionPayloads(gameSaveService.Capture())
            && outcomeHashes == string.Join("|", MedicalOutcomes().Select(value => value.immutablePayloadHash))
            && TotalLabor() - initialLabor == expectedMedical + repairLabor,
            "fresh completed-owner replay changed labor/body/exact results");
        Check("w03:terminal-official-replay", "sections=" + completed.sections.Count
            + "; liveAttempts=2; freshAttempts=2; exactOutcomes=" + outcomes.Length
            + "; allSectionsUnchanged=True; hashes=" + outcomeHashes);
        Write("w03-final-world.json", gameSaveService.ToJson(gameSaveService.Capture(), true));

        CharacterMedicalOrder Current() => medical.ActiveOrders.Single(value => value.orderId == orderId);
        double ExecutorLaborCarry(bool requireExecutor)
        {
            FieldInfo executorField = typeof(AbilityWork).GetField("taskExecutor",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("W03 executor observation field missing.");
            object executor = executorField.GetValue(work);
            if (executor == null)
            {
                // No executor yet means no prior work and no retained fraction.
                Require(!requireExecutor, "completed repair executor missing");
                return 0d;
            }
            FieldInfo carryField = typeof(WorkTaskExecutor).GetField("actualLaborMilliWuCarry",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("W03 labor carry observation field missing.");
            return (double)carryField.GetValue(executor);
        }
        long TotalLabor()
        {
            var saved = laborPersistence.CaptureLaborSaveData();
            return checked(saved.actualLaborMilliWu + saved.dailyRecords.Sum(value => value.actualLaborMilliWu));
        }
        void StartRescue()
        {
            PrepareActor(BuiltInWorkTypeIds.Rescue);
            Require(brain.availableActions.Any(value => value?.actionset is AIRescue),
                "authored AIRescue action missing");
            brain.PreferActionOnNextDecision<AIRescue>(180f);
            actor.SetAiPaused(false);
            brain.RequestImmediateReplan(clearFailures: true);
        }
        void RefreshActors()
        {
            actor = worldRegistry.Characters.Single(value => value != null && value.Identity.PersistentId == workerId);
            patient = worldRegistry.Characters.Single(value => value != null && value.Identity.PersistentId == patientId);
            brain = actor.Brain;
            work = actor.GetComponent<AbilityWork>();
            grid = FindFirstObjectByType<GridSystemManager>()?.grid;
            foreach (CharacterActor other in worldRegistry.Characters.Where(value => value != null && value != actor))
                other.SetAiPaused(true);
        }
        IEnumerator WaitUntil(Func<bool> condition, string stage, bool allowDelivery)
        {
            float simulationStart = actor.GameClock.Time, wallStart = Time.realtimeSinceStartup;
            float nextEnvironmentReset = simulationStart;
            int firstFrame = Time.frameCount;
            float lastProgressGame = simulationStart, lastProgressWall = wallStart;
            int lastProgressFrame = firstFrame, lastDeliveries = deliveries;
            CharacterMedicalOrder previous = Current();
            CharacterMedicalOrderState previousState = previous.state;
            float previousStabilization = previous.completedStabilizationWork;
            float previousTreatment = previous.completedTreatmentWork;
            int previousTerminalSequence = previous.nextTerminalOutcomeSequence;
            bool previousCarried = previous.carried;
            Vector3 previousWorkerPosition = actor.transform.position;
            Vector2Int previousPatientPosition = patient.GetNowXY();
            while (!condition())
            {
                MaintainStableWorkSubject();
                SetNeutralNeeds(patient);
                if (actor.GameClock.Time >= nextEnvironmentReset)
                {
                    ResetSubjectEnvironmentExposure();
                    nextEnvironmentReset = actor.GameClock.Time + 5f;
                }
                AbilityRescue rescue = actor.GetComponent<AbilityRescue>();
                sawAbility |= rescue?.IsRescuing == true && brain.bestAction?.actionset is AIRescue;
                CharacterMedicalOrder current = Current();
                sawCarriedPatient |= current.carried && patient.transform.IsChildOf(actor.transform);
                sawTreatment |= current.state == CharacterMedicalOrderState.Treating;
                if (allowDelivery && current.treatmentSupplyDeliveryRequested && !current.treatmentSupplyConsumed)
                    DeliverRequestedMedicine(current);
                // Sample real authority progress before testing expiry. An
                // external editor request can delay wall time without granting
                // the simulation enough execution time to finish clinical work.
                bool progressed = current.state != previousState
                    || current.completedStabilizationWork > previousStabilization + 0.0001f
                    || current.completedTreatmentWork > previousTreatment + 0.0001f
                    || current.nextTerminalOutcomeSequence > previousTerminalSequence
                    || current.carried != previousCarried || deliveries > lastDeliveries
                    || (actor.transform.position - previousWorkerPosition).sqrMagnitude >= 0.0001f
                    || patient.GetNowXY() != previousPatientPosition;
                if (progressed)
                {
                    lastProgressGame = actor.GameClock.Time;
                    lastProgressWall = Time.realtimeSinceStartup;
                    lastProgressFrame = Time.frameCount;
                    previousWorkerPosition = actor.transform.position;
                }
                previousState = current.state;
                previousStabilization = current.completedStabilizationWork;
                previousTreatment = current.completedTreatmentWork;
                previousTerminalSequence = current.nextTerminalOutcomeSequence;
                previousCarried = current.carried;
                previousPatientPosition = patient.GetNowXY();
                lastDeliveries = deliveries;
                if (!capturedSkillEvidenceFailure && consoleIssues.Any(value =>
                        value.Contains("evidence-binding-invalid", StringComparison.Ordinal)))
                {
                    Write("w03-evidence-error-world.json", gameSaveService.ToJson(gameSaveService.Capture(), true));
                    capturedSkillEvidenceFailure = true;
                }
                bool overallExpired = actor.GameClock.Time - simulationStart > 240f
                    && Time.frameCount - firstFrame > 180;
                bool authorityStalled = Time.realtimeSinceStartup - lastProgressWall >= 60f
                    && (actor.GameClock.Time - lastProgressGame >= 30f
                        || Time.frameCount - lastProgressFrame >= 120);
                if (overallExpired || authorityStalled)
                {
                    Write("w03-timeout-world.json", gameSaveService.ToJson(gameSaveService.Capture(), true));
                    throw new InvalidOperationException("W03 timed out at " + stage + ": " + Describe()
                        + "; game=" + (actor.GameClock.Time - simulationStart)
                        + "; frames=" + (Time.frameCount - firstFrame)
                        + "; wall=" + (Time.realtimeSinceStartup - wallStart)
                        + "; overall=" + overallExpired + "; authorityStalled=" + authorityStalled
                        + "; idleGame=" + (actor.GameClock.Time - lastProgressGame)
                        + "; idleFrames=" + (Time.frameCount - lastProgressFrame)
                        + "; idleWall=" + (Time.realtimeSinceStartup - lastProgressWall));
                }
                yield return null;
            }
        }
        void DeliverRequestedMedicine(CharacterMedicalOrder current)
        {
            if (physicalItems.GetAllStacks().Any(value => value.Quantity > 0
                    && value.DestinationId == current.treatmentMaterialDestinationId
                    && value.State == WorldItemStackState.FacilityBuffer))
                return;
            CharacterActor carrier = worldRegistry.Characters.Single(value =>
                value != null && value.Identity.PersistentId == carrierId);
            Require(deliveries < 8 && carrier.CarryInventory.Items.Count == 0,
                "bounded controlled carrier unavailable");
            Require(physicalItems.TryReserveBestHaulPlan(carrier, out WorldItemHaulPlan plan, out string failure),
                "medical physical haul plan: " + failure);
            Require(plan.PrimaryDestinationId == current.treatmentMaterialDestinationId
                && plan.ReservedStackQuantities.Count == 1 && plan.ReservedStackQuantities[0].Quantity == 1,
                "physical planner selected a different medical route");
            WorldItemReservedStackQuantity leg = plan.ReservedStackQuantities[0];
            Require(physicalItems.TryPickupReservedStackQuantity(carrier, carrier.CarryInventory, leg,
                    out int picked, out string pickupFailure) && picked == 1, pickupFailure);
            Require(physicalItems.TryCommitHaulPickup(leg.OwnerOperationId, carrier.CarryInventory,
                out string commitFailure), commitFailure);
            Require(physicalItems.TryDepositCarriedItemsToFacility(carrier, carrier.CarryInventory, current.BedPosition,
                current.treatmentMaterialDestinationId, new[] { leg.OwnerOperationId }, out string depositFailure),
                depositFailure);
            Require(runtimeScope.Container.Resolve<ItemQuantityReservationService>().ReleaseByOwner(
                    leg.OwnerOperationId, ItemReservationReleaseReason.Completed) > 0
                && physicalItems.ReleaseHaulDeliveryIntent(leg.OwnerOperationId)
                && carrier.CarryInventory.Items.Count == 0, "medical haul owner did not close");
            deliveries++;
        }
        GameplayOutcomeSnapshot[] MedicalOutcomes() => W01ExactOutcomes().Where(value =>
            value.outcomeTypeId == CharacterMedicalOutcomeIds.Completed.Value
            && Fact(value, CharacterMedicalOutcomeIds.Order.Value) == orderId).OrderBy(value => value.sequence).ToArray();
        string Describe()
        {
            CharacterMedicalOrder value = Current();
            return "order=" + orderId + "; state=" + value.state + "/" + value.statusCode
                + "; stabilization=" + value.completedStabilizationWork + "/" + value.requiredStabilizationWork
                + "; treatment=" + value.completedTreatmentWork + "/" + value.requiredTreatmentWork
                + "; phase=" + brain.CurrentActionPhase + "; failure=" + brain.LastActionFailure
                + "; rescue=" + actor.GetComponent<AbilityRescue>()?.LastRescueTerminalForDiagnostics;
        }
        void Check(string id, string detail) => results.Add(new WorkTypeLiveRow(id, "PASS", patientId, detail));
        void Write(string name, string json)
        {
            Directory.CreateDirectory(output);
            WriteUtf8LfCreateNew(Path.Combine(output, name), new[] { json });
        }
        static string Fact(GameplayOutcomeSnapshot value, string id) =>
            value.facts.FirstOrDefault(fact => fact.factId == id)?.value ?? string.Empty;
        static double Metric(GameplayOutcomeSnapshot value, string id) =>
            value.metrics.Single(metric => metric.metricId == id).value;
        static void Require(bool value, string failure)
        {
            if (!value) throw new InvalidOperationException("W03 medical labor: " + failure);
        }
        void Position(CharacterActor target, Vector2Int cell)
        {
            target.SetAiPaused(true);
            target.Brain?.StopCurrentActionForReplan("qa:w03:position");
            target.GetComponent<AbilityMove>()?.CancelActiveMovement();
            target.transform.position = grid.GetWorldPos(cell);
            target.Brain?.ClearPathSearchCache();
        }
    }

    private IEnumerator RunW01WorkTerminalJoinFocused()
    {
        if (CharacterAiWorkTypeLiveMatrixPlayModeVerifier.IsW01FireIdentityVerification)
        {
            IEnumerator fire = RunW01FireIdentityFocused();
            while (fire.MoveNext()) yield return fire.Current;
            yield break;
        }
        WorkTypeId workTypeId = BuiltInWorkTypeIds.Repair;
        MaintainStableWorkSubject();
        MaterialWorkFixture fixture = PrepareRepairFixture(out string fixtureFailure);
        if (fixture == null)
        {
            results.Add(WorkTypeLiveRow.Blocked(
                workTypeId, "structural-repair-fixture-unavailable", fixtureFailure));
            yield break;
        }
        BuildableObject target = fixture.Target;
        string targetId = target.RequirePersistentInstanceId().Value;
        string actorId = actor.Identity.PersistentId;
        IBuildingStructuralIntegrityRuntime integrity = runtimeScope.Container
            .Resolve<IBuildingStructuralIntegrityRuntime>();
        if (!integrity.TryGet(target, out BuildingStructuralIntegritySnapshot before))
            throw new InvalidOperationException("W01 structural state is missing.");
        IWorkExecutionHandlerRegistry registry = runtimeScope.Container
            .Resolve<IWorkExecutionHandlerRegistry>();
        w01RepairObservation = new W01RepairHandlerObservation(registry);
        w05IdentitySubscription = runtimeScope.Container.Resolve<IGameEventBus>()
            .Subscribe<WorkCompletedIdentityEvent>(w05IdentityEvents.Add);
        w05IdentityDeliveries = runtimeScope.Container
            .Resolve<WorkCompletionIdentityDeliveryLedger>();
        WorkCompletionIdentityDeliveryCursorSaveData directBefore =
            FindW05DeliveryCursor(w05IdentityDeliveries.Capture(),
                WorkCompletionIdentityDeliveryLedger.DirectEventStreamId);
        runtimeScope.Container.Resolve<IFacilityCandidateCache>().MarkDynamicStateDirty();
        yield return null;
        PrepareActor(workTypeId);

        HashSet<string> identityKeysBefore = new(
            CaptureRepairIdentities().Select(W01ResultKey), StringComparer.Ordinal);
        w01RepairDeliveryGate = new W01RepairDeliveryGate(
            (WorkOrderRuntime)workOrders,
            runtimeScope.Container.Resolve<IDomainFactPublisher>(),
            runtimeScope.Container.Resolve<IWorkCompletionIdentityDeliveryCommand>(),
            runtimeScope.Container.Resolve<WorkCompletionIdentityDeliveryLedger>(),
            runtimeScope.Container.Resolve<CharacterIdentityEventPublisher>(),
            runtimeScope.Container.Resolve<WorkIdentityAppliedFactMapping>());
        WorkPhaseResult completion = null;
        IEnumerator run = RunLivePhase(workTypeId, target,
            WorkProbeFault.CompleteNormally, fixture, value => completion = value,
            completionAuthoritySatisfied: () =>
                w01RepairObservation.CompletedCalls == 1
                && integrity.TryGet(target, out BuildingStructuralIntegritySnapshot state)
                && Mathf.Approximately(state.CurrentHitPoints, state.MaxHitPoints));
        while (run.MoveNext())
            yield return run.Current;
        w01RepairObservation.Dispose();
        WorkExecutionResult terminal = w01RepairObservation.Result;
        results.Add(new WorkTypeLiveRow(workTypeId.Value,
            completion?.Passed == true ? "PASS" : "FAIL", targetId,
            completion?.Format() ?? "actual AI completion was not observed"));
        if (terminal == null || !terminal.CompletedSuccessfully
            || string.IsNullOrEmpty(terminal.ChildResultKey))
        {
            results.Add(new WorkTypeLiveRow("w01:terminal-child-join", "FAIL", targetId,
                "handlerCalls=" + w01RepairObservation.CompletedCalls
                    + "; successful=" + terminal?.CompletedSuccessfully
                    + "; child=" + terminal?.ChildResultKey));
            yield break;
        }

        if (!w01RepairDeliveryGate.Held)
            throw new InvalidOperationException("W01 actual repair did not reach the mandatory identity command.");
        WorkCompletionIdentityDeliveryRequest frozenRequest = w01RepairDeliveryGate.Request;
        brain.StopCurrentActionForReplan("W01 repair post-child checkpoint");
        work.ClearPriorityWorkTarget();
        actor.SetAiPaused(true);
        DungeonGameSaveData pendingSave = gameSaveService.Capture();
        string pendingJson = gameSaveService.ToJson(pendingSave, true);
        WriteRepairEvidence("w01-repair-post-child-world.json", pendingJson);
        RepairWorkOutcomeSaveData pendingOwner = workOrders.Capture().repairOutcomes.Single(value =>
            value.operationId == frozenRequest.OperationId);
        GameplayOutcomeSnapshot pendingChild = W01ExactOutcomes().Single(value =>
            W01ResultKey(value) == frozenRequest.ChildResultKey);
        bool checkpoint = pendingOwner.terminal
            && pendingOwner.publicationConfirmed
            && pendingOwner.publicationKey == frozenRequest.ChildResultKey
            && pendingOwner.completionWorkerId == actorId && CaptureRepairIdentities().Length == identityKeysBefore.Count;
        DungeonGameSaveData invalid = gameSaveService.FromJson(pendingJson);
        DungeonSaveSectionEnvelope ledgerSection = invalid.sections.Single(value =>
            value.sectionId == GameplayOutcomeLedgerSaveSection.Id);
        var missingChild = DungeonSaveJson.Deserialize<GameplayOutcomeLedgerSaveData>(ledgerSection.payloadJson);
        int removed = missingChild.exactOutcomes.RemoveAll(value => W01ResultKey(value) == frozenRequest.ChildResultKey)
            + missingChild.outbox.RemoveAll(value => W01ResultKey(value) == frozenRequest.ChildResultKey);
        if (removed != 1) throw new InvalidOperationException("W01 requires one actual repair child for corruption test.");
        ledgerSection.payloadJson = JsonUtility.ToJson(missingChild);
        invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);
        WriteRepairEvidence("w01-repair-missing-child-invalid-world.json", gameSaveService.ToJson(invalid, true));
        var root = runtimeScope.Container.Resolve<DungeonRuntimeAggregateRootStore>();
        int revision = root.PublishedRestoreRevision;
        string beforeInvalid = W05SectionPayloads(gameSaveService.Capture());
        bool invalidAccepted = gameSaveService.TryRestore(invalid, out DungeonGameRestoreReport rejection);
        bool atomicallyRejected = !invalidAccepted && rejection.Errors.Any(value =>
                value.Contains("Repair completion", StringComparison.Ordinal))
            && revision == root.PublishedRestoreRevision && !root.IsRestoreStaging
            && beforeInvalid == W05SectionPayloads(gameSaveService.Capture());
        string rejectionDetail = "accepted=" + invalidAccepted + "; sectionsUnchanged=" + atomicallyRejected
            + "; errors=" + string.Join(" | ", rejection.Errors);
        WriteRepairEvidence("w01-repair-missing-child-witness.txt", rejectionDetail);
        results.Add(new WorkTypeLiveRow("w01:repair-missing-child-rejected",
            atomicallyRejected ? "PASS" : "FAIL", targetId, rejectionDetail));
        if (!atomicallyRejected) throw new InvalidOperationException("W01 invalid repair save was not rejected atomically.");
        if (!gameSaveService.TryRestore(gameSaveService.FromJson(pendingJson), out var pendingRestored)
            || !pendingRestored.Success)
            throw new InvalidOperationException("W01 pending repair restore failed: " + string.Join(" | ", pendingRestored.Errors));
        actor = LiveActors(worldRegistry).Single(value => value.Identity.PersistentId == actorId);
        work = actor.GetComponent<AbilityWork>();
        brain = actor.Brain;
        actor.SetAiPaused(true);
        grid = FindFirstObjectByType<GridSystemManager>()?.grid;
        BuildableObject pendingFreshTarget = worldRegistry.Buildings.Single(value =>
            value != null && value.PersistentInstanceId.Value == targetId);
        bool pendingFresh = !ReferenceEquals(target, pendingFreshTarget);
        target = pendingFreshTarget;
        w01RepairDeliveryGate.Released = true;
        float pendingGame = actor.GameClock.Time;
        float pendingWall = Time.realtimeSinceStartup;
        int pendingFrame = Time.frameCount;
        while (workOrders.Capture().repairOutcomes.Any(value => value.operationId == frozenRequest.OperationId))
        {
            if (actor.GameClock.Time - pendingGame > 15f && Time.frameCount - pendingFrame > 120
                || Time.realtimeSinceStartup - pendingWall > 120f)
                throw new InvalidOperationException("W01 pending repair did not finish via production Tick.");
            yield return null;
        }
        var resumedChildren = W01ExactOutcomes().Where(value => W01ResultKey(value) == frozenRequest.ChildResultKey).ToArray();
        bool resumed = checkpoint && pendingFresh && resumedChildren.Length == 1
            && resumedChildren[0].immutablePayloadHash == pendingChild.immutablePayloadHash
            && CaptureRepairIdentities().Length == identityKeysBefore.Count + 1
            && integrity.TryGet(target, out var resumedState)
            && Mathf.Approximately(resumedState.CurrentHitPoints, resumedState.MaxHitPoints);
        results.Add(new WorkTypeLiveRow("w01:repair-pending-save-resume", resumed ? "PASS" : "FAIL", targetId,
            "officialSections=" + pendingSave.sections.Count + "; checkpoint=" + checkpoint
                + "; freshTarget=" + pendingFresh + "; children=" + resumedChildren.Length
                + "; pendingOwners=" + workOrders.Capture().repairOutcomes.Count
                + "; identities=" + CaptureRepairIdentities().Length));
        w01RepairDeliveryGate.Dispose();
        w01RepairDeliveryGate = null;

        GameplayOutcomeSnapshot[] exactChildren = W01ExactOutcomes()
            .Where(value => W01ResultKey(value) == terminal.ChildResultKey).ToArray();
        GameplayOutcomeSnapshot child = exactChildren.SingleOrDefault();
        if (child == null)
            throw new InvalidOperationException("W01 returned child has no unique exact ledger record.");
        string operation = child.facts.Single(value =>
            value.factId == MigratedProducerOutcomeIds.OperationFact.Value).value;
        double totalMilliWork = child.metrics.Single(value =>
            value.metricId == MigratedProducerOutcomeIds.CompletedWorkMetric.Value).value;
        double ledgerBefore = child.metrics.Single(value =>
            value.metricId == MigratedProducerOutcomeIds.PreviousMetric.Value).value;
        double ledgerAfter = child.metrics.Single(value =>
            value.metricId == MigratedProducerOutcomeIds.CurrentMetric.Value).value;
        string contributors = child.facts.Single(value =>
            value.factId == MigratedProducerOutcomeIds.ContributorsFact.Value).value;
        float repairPerWork = target.BuildingData
            .GetAbility<BuildingStructuralIntegrityAbility>().repairHitPointsPerWork;
        double physicalWork = (ledgerAfter - ledgerBefore) / repairPerWork;
        bool amountMatches = terminal.ContributionWorkUnits.HasValue
            && Math.Abs(Math.Round(terminal.ContributionWorkUnits.Value * 1000d)
                - totalMilliWork) <= 1d
            && Math.Abs(terminal.ContributionWorkUnits.Value - physicalWork) < 0.001d;
        bool targetRestored = integrity.TryGet(target,
            out BuildingStructuralIntegritySnapshot after)
            && Mathf.Approximately(after.CurrentHitPoints, after.MaxHitPoints);
        bool directUnchanged = W05DeliveryCursorEquals(directBefore,
            FindW05DeliveryCursor(w05IdentityDeliveries.Capture(),
                WorkCompletionIdentityDeliveryLedger.DirectEventStreamId));
        int identityEvents = w05IdentityEvents.Count(value => value.WorkId == workTypeId.Value);
        bool joined = w01RepairObservation.CompletedCalls == 1
            && completion?.ProgressDelta > 1
            && w01RepairObservation.ActorId == actorId
            && w01RepairObservation.TargetId == targetId
            && terminal.CompletionIdentityAlreadyPublished
            && terminal.TerminalOwnerId == "repair-work-outcome"
            && terminal.TargetEntityId == targetId
            && child.outcomeTypeId == "work.repair-resolved"
            && child.status == GameplayOutcomeStatus.Succeeded
            && operation.StartsWith("repair-work:", StringComparison.Ordinal)
            && child.participants.Any(value => value.entityId == actorId
                && value.roleId == MigratedProducerOutcomeIds.ActorRole.Value)
            && child.participants.Any(value => value.entityId == targetId
                && value.roleId == MigratedProducerOutcomeIds.FacilityRole.Value)
            && Math.Abs(ledgerBefore - before.CurrentHitPoints) < 0.001d
            && Math.Abs(ledgerAfter - after.CurrentHitPoints) < 0.001d
            && totalMilliWork > 0d && amountMatches && targetRestored
            && identityEvents == 0 && directUnchanged;
        results.Add(new WorkTypeLiveRow("w01:terminal-child-join",
            joined ? "PASS" : "FAIL", targetId,
            "scope=actual-executor-return-to-domain-ledger; operation=" + operation
                + "; child=" + terminal.ChildResultKey
                + "; terminalOwner=" + terminal.TerminalOwnerId
                + "; actor=" + actorId + "; calls=" + w01RepairObservation.CompletedCalls
                + "; hp=" + ledgerBefore + "->" + ledgerAfter
                + "; returnedWork=" + terminal.ContributionWorkUnits
                + "; ledgerMilliWork=" + totalMilliWork
                + "; physicalWork=" + physicalWork
                + "; contributors=" + contributors + "; amountMatches=" + amountMatches
                + "; domainOwned=" + terminal.CompletionIdentityAlreadyPublished
                + "; genericEvents=" + identityEvents + "; directUnchanged=" + directUnchanged));

        GameplayOutcomeSnapshot[] identities = CaptureRepairIdentities()
            .Where(value => !identityKeysBefore.Contains(W01ResultKey(value))).ToArray();
        GameplayOutcomeSnapshot identity = identities.Length == 1 ? identities[0] : null;
        double identityWork = identity?.metrics.FirstOrDefault(value =>
            value.metricId == WorkCompletionIdentityOutcomeIds.ContributionMetric.Value)?.value
            ?? double.NaN;
        bool identityJoined = identity != null
            && identity.status == GameplayOutcomeStatus.Succeeded
            && identity.sequence > child.sequence
            && identity.absoluteDay == child.absoluteDay
            && Fact(identity, WorkCompletionIdentityOutcomeIds.ChildResultKeyFact)
                == terminal.ChildResultKey
            && Fact(identity, WorkCompletionIdentityOutcomeIds.OperationIdFact) == operation
            && Fact(identity, WorkCompletionIdentityOutcomeIds.TerminalOwnerFact)
                == terminal.TerminalOwnerId
            && identity.subjects.Any(value =>
                value.entityKindId == WorkCompletionIdentityOutcomeIds.BuildingKind.Value
                && value.entityId == targetId)
            && terminal.ContributionWorkUnits.HasValue
            && Math.Abs(identityWork - terminal.ContributionWorkUnits.Value) < 0.001d;
        results.Add(new WorkTypeLiveRow("w01:repair-identity-child-join",
            identityJoined ? "PASS" : "FAIL", targetId,
            "identities=" + identities.Length + "; worker=" + actorId
                + "; child=" + terminal.ChildResultKey
                + "; identityChild=" + Fact(identity, WorkCompletionIdentityOutcomeIds.ChildResultKeyFact)
                + "; operation=" + operation
                + "; identityOperation=" + Fact(identity, WorkCompletionIdentityOutcomeIds.OperationIdFact)
                + "; terminalOwner=" + terminal.TerminalOwnerId
                + "; identityOwner=" + Fact(identity, WorkCompletionIdentityOutcomeIds.TerminalOwnerFact)
                + "; childSequence=" + child.sequence + "; identitySequence=" + identity?.sequence
                + "; work=" + identityWork + "/" + terminal.ContributionWorkUnits));

        brain.StopCurrentActionForReplan("W01 terminal snapshot");
        work.ClearPriorityWorkTarget();
        actor.SetAiPaused(true);
        DungeonGameSaveData completedSave = gameSaveService.Capture();
        string completedJson = gameSaveService.ToJson(completedSave, true);
        string evidenceDirectory = Path.GetDirectoryName(
            CharacterAiWorkTypeLiveMatrixPlayModeVerifier.W01WorkTerminalJoinReportPath);
        Directory.CreateDirectory(evidenceDirectory);
        WriteUtf8LfCreateNew(Path.Combine(evidenceDirectory,
            "w01-repair-completed-world.json"), new[] { completedJson });
        DungeonGameSaveData detached = gameSaveService.FromJson(completedJson);
        if (!gameSaveService.TryRestore(detached, out DungeonGameRestoreReport restored)
            || !restored.Success)
            throw new InvalidOperationException("W01 completed world restore failed: "
                + string.Join(" | ", restored.Errors));
        actor = LiveActors(worldRegistry).Single(value =>
            value.Identity.PersistentId == actorId);
        work = actor.GetComponent<AbilityWork>();
        brain = actor.Brain;
        grid = FindFirstObjectByType<GridSystemManager>()?.grid;
        BuildableObject freshTarget = worldRegistry.Buildings.Single(value =>
            value != null && value.PersistentInstanceId.Value == targetId);
        bool freshObjects = !ReferenceEquals(target, freshTarget);
        GameplayOutcomeSnapshot[] restoredChildren = W01ExactOutcomes()
            .Where(value => W01ResultKey(value) == terminal.ChildResultKey).ToArray();
        IRepairWorkOutcomeRuntime repair = runtimeScope.Container.Resolve<IRepairWorkOutcomeRuntime>();
        string workStateBeforeRetry = JsonUtility.ToJson(workOrders.Capture());
        bool retryAccepted = repair.TryComplete(new RepairWorkOutcomeHandle(operation),
            new CharacterId(actorId), CharacterCommandOrigin.Autonomous,
            terminal.ContributionWorkUnits.Value,
            child.absoluteDay, true, RepairWorkPrimaryUnit.Point,
            (float)ledgerBefore, (float)ledgerAfter, 0f, 0f,
            RepairWorkMaterialReceipt.None, out string retryKey, out string retryFailure);
        GameplayOutcomeSnapshot[] retryChildren = W01ExactOutcomes()
            .Where(value => W01ResultKey(value) == terminal.ChildResultKey).ToArray();
        bool replaySafe = freshObjects && restoredChildren.Length == 1
            && restoredChildren[0].immutablePayloadHash == child.immutablePayloadHash
            && !retryAccepted && string.IsNullOrEmpty(retryKey)
            && retryFailure == "repair-work-outcome-owner-missing"
            && retryChildren.Length == 1
            && retryChildren[0].immutablePayloadHash == child.immutablePayloadHash
            && workStateBeforeRetry == JsonUtility.ToJson(workOrders.Capture())
            && integrity.TryGet(freshTarget, out BuildingStructuralIntegritySnapshot finalState)
            && Mathf.Approximately(finalState.CurrentHitPoints, (float)ledgerAfter)
            && !workOrders.Capture().repairOutcomes.Any(value => value.operationId == operation)
            && W05DeliveryCursorEquals(directBefore,
                FindW05DeliveryCursor(w05IdentityDeliveries.Capture(),
                    WorkCompletionIdentityDeliveryLedger.DirectEventStreamId));
        results.Add(new WorkTypeLiveRow("w01:terminal-save-replay",
            replaySafe ? "PASS" : "FAIL", targetId,
            "officialSections=" + completedSave.sections.Count + "; freshTarget=" + freshObjects
                + "; restoredChildren=" + restoredChildren.Length
                + "; retryChildren=" + retryChildren.Length + "; retryAccepted=" + retryAccepted
                + "; retryFailure=" + retryFailure + "; childHash=" + child.immutablePayloadHash));
        GameplayOutcomeSnapshot[] restoredIdentities = CaptureRepairIdentities()
            .Where(value => !identityKeysBefore.Contains(W01ResultKey(value))).ToArray();
        bool identityRestored = identityJoined && restoredIdentities.Length == 1
            && W01ResultKey(restoredIdentities[0]) == W01ResultKey(identity)
            && restoredIdentities[0].immutablePayloadHash == identity.immutablePayloadHash
            && Fact(restoredIdentities[0], WorkCompletionIdentityOutcomeIds.ChildResultKeyFact)
                == terminal.ChildResultKey;
        results.Add(new WorkTypeLiveRow("w01:repair-identity-save-join",
            identityRestored ? "PASS" : "FAIL", targetId,
            "officialSections=" + completedSave.sections.Count + "; freshTarget=" + freshObjects
                + "; identitiesBefore=" + identities.Length
                + "; identitiesAfterRestoreAndOwnerRetry=" + restoredIdentities.Length
                + "; identityHash=" + (identity?.immutablePayloadHash ?? "missing")));
        IWorkCompletionIdentityDeliveryCommand command = runtimeScope.Container.Resolve<IWorkCompletionIdentityDeliveryCommand>();
        string beforeReplay = W05SectionPayloads(gameSaveService.Capture());
        var replayOne = command.EnsureApplied(frozenRequest);
        var replayTwo = command.EnsureApplied(frozenRequest);
        var changedRequest = new WorkCompletionIdentityDeliveryRequest(frozenRequest.DeliveryId,
            frozenRequest.ProducerStreamId, frozenRequest.OperationSequence, frozenRequest.Character,
            frozenRequest.WorkId, frozenRequest.ProductId, frozenRequest.Origin, frozenRequest.AbsoluteDay,
            frozenRequest.OperationId, frozenRequest.TerminalOwnerId, frozenRequest.ChildResultKey,
            frozenRequest.TargetEntityId, frozenRequest.ContributionWorkUnits + 1f);
        var changed = command.EnsureApplied(changedRequest);
        bool exactReplay = replayOne.Status == WorkCompletionIdentityDeliveryStatus.AlreadyApplied
            && replayTwo.Status == WorkCompletionIdentityDeliveryStatus.AlreadyApplied
            && changed.Status == WorkCompletionIdentityDeliveryStatus.Conflict
            && beforeReplay == W05SectionPayloads(gameSaveService.Capture());
        results.Add(new WorkTypeLiveRow("w01:repair-identity-exact-replay", exactReplay ? "PASS" : "FAIL", targetId,
            "first=" + replayOne.Status + "; second=" + replayTwo.Status + "; changed=" + changed.Status
                + "; allSectionsUnchanged=" + exactReplay + "; detail=" + changed.FailureReason));
        bool sharedReplay = WorkCompletionIdentityOutcomeDebugScenarios.RunAll();
        results.Add(new WorkTypeLiveRow("w01:shared-delivery-replay",
            sharedReplay ? "PASS" : "FAIL", "bounded-fixture",
            "scope=separate shared-delivery sequence/replay/tamper/JSON test; not actual AI child join"));

        GameplayOutcomeSnapshot[] CaptureRepairIdentities() => W01ExactOutcomes()
            .Where(value => value.outcomeTypeId == WorkCompletionIdentityOutcomeIds.Applied.Value
                && Fact(value, WorkCompletionIdentityOutcomeIds.WorkIdFact) == workTypeId.Value
                && value.participants.Any(participant => participant.entityId == actorId
                    && participant.roleId == WorkCompletionIdentityOutcomeIds.WorkerRole.Value))
            .ToArray();

        void WriteRepairEvidence(string name, string content)
        {
            string directory = Path.GetDirectoryName(CharacterAiWorkTypeLiveMatrixPlayModeVerifier.W01WorkTerminalJoinReportPath);
            Directory.CreateDirectory(directory);
            WriteUtf8LfCreateNew(Path.Combine(directory, name), new[] { content });
        }
        static string Fact(GameplayOutcomeSnapshot outcome, GameplayOutcomeFactId fact) =>
            outcome?.facts.FirstOrDefault(value => value.factId == fact.Value)?.value ?? "missing";
    }

    private IEnumerator RunW01FireIdentityFocused()
    {
        bool waterMode = CharacterAiWorkTypeLiveMatrixPlayModeVerifier.IsW01FireWaterIdentityVerification;
        WorkTypeId workTypeId = BuiltInWorkTypeIds.ThreatMitigation;
        MaintainStableWorkSubject();
        PrepareActor(workTypeId);
        var targets = runtimeScope.Container.Resolve<IEnvironmentalFireTargetQuery>();
        var fires = runtimeScope.Container.Resolve<IEnvironmentalFireQuery>();
        var commands = runtimeScope.Container.Resolve<IEnvironmentalFireCommand>();
        var persistence = runtimeScope.Container.Resolve<IEnvironmentalFirePersistence>();
        var response = runtimeScope.Container.Resolve<IEnvironmentalFireSuppressionWorkRuntime>();
        if (response is not EnvironmentalFireResponseRuntime)
            throw new InvalidOperationException("W01 fire requires the production response runtime.");

        BuildableObject target = worldRegistry.Buildings.Where(value =>
                !waterMode && value != null && !value.isDestroy && IsReachableFromSubject(value)
                && targets.TryGetTarget(new EnvironmentalFireTargetRef(
                    EnvironmentalFireTargetKind.Building, value.PersistentInstanceId.Value), out var candidate)
                && candidate.CanBurn && !candidate.HasElectricalHazard
                && candidate.Accepts(EnvironmentalFireIgnitionKind.ActiveHeatSource))
            .OrderBy(value => Vector2Int.Distance(actor.GetNowXY(), value.centerPos))
            .ThenBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal).FirstOrDefault();
        if (target == null)
        {
            // This is a fixture placement, not evidence for construction or a
            // heat-source producer. Ignition and all response work use real owners.
            var warehouse = Resources.Load<BuildingSO>("SO/Building/Modular/L01_대형보관선반");
            target = PlaceAuthoredBuilding(warehouse, out string placementFailure);
            if (target == null)
                throw new InvalidOperationException("W01 fire warehouse unavailable: " + placementFailure);
        }
        string targetId = target.RequirePersistentInstanceId().Value;
        string actorId = actor.Identity.PersistentId;
        var targetRef = new EnvironmentalFireTargetRef(EnvironmentalFireTargetKind.Building, targetId);
        if (!targets.TryGetTarget(targetRef, out var targetSnapshot) || !targetSnapshot.CanBurn
            || targetSnapshot.HasElectricalHazard || !targetSnapshot.Accepts(EnvironmentalFireIgnitionKind.ActiveHeatSource))
            throw new InvalidOperationException("W01 fire target is not an authored combustible non-electrical heat target.");
        var ignitionRequest = new EnvironmentalFireIgnitionRequest("qa:w01:fire-identity",
            EnvironmentalFireIgnitionKind.ActiveHeatSource, "qa:w01:fire-fixture", targetRef,
            waterMode ? 0.4f : 0.06f, "qa:w01:actual-ai", targetDisplayName: targetSnapshot.DisplayName);
        EnvironmentalFireIgnitionResult ignition = commands.TryIgnite(ignitionRequest);
        if (!ignition.Created)
            throw new InvalidOperationException("W01 fire ignition rejected: " + ignition.Disposition + "; " + ignition.Reason);
        string fireId = ignition.FireId;
        if (waterMode)
        {
            IEnumerator haul = RunW01FireWaterHaul(target, fireId, fresh => target = fresh);
            while (haul.MoveNext()) yield return haul.Current;
            PrepareActor(workTypeId);
        }
        int physicalWaterBefore = CapturePhysicalItemQuantity("resource:clean-water");
        using var fireGate = new W01FireDeliveryGate((EnvironmentalFireRuntime)commands,
            runtimeScope.Container.Resolve<IWorkCompletionIdentityDeliveryCommand>());
        w01RepairObservation = new W01RepairHandlerObservation(
            runtimeScope.Container.Resolve<IWorkExecutionHandlerRegistry>(), observeFire: true);
        w05IdentitySubscription = runtimeScope.Container.Resolve<IGameEventBus>()
            .Subscribe<WorkCompletedIdentityEvent>(w05IdentityEvents.Add);
        w05IdentityDeliveries = runtimeScope.Container.Resolve<WorkCompletionIdentityDeliveryLedger>();
        var directBefore = FindW05DeliveryCursor(w05IdentityDeliveries.Capture(),
            WorkCompletionIdentityDeliveryLedger.DirectEventStreamId);
        HashSet<string> identityKeysBefore = new(CaptureIdentities().Select(W01ResultKey), StringComparer.Ordinal);
        // Only the normal response Tick adds the work marker/alert. Do not inject
        // a fake suppression port, mutate fire intensity, or call the handler directly.
        yield return null;
        if (!response.TryGetWork(target, actor, out var initialWork) || !initialWork.Available
            || initialWork.Mode != (waterMode ? EnvironmentalFireSuppressionMode.Water : EnvironmentalFireSuppressionMode.InitialAttack))
            throw new InvalidOperationException("W01 fire required suppression mode unavailable: " + initialWork.UnavailableReason);
        runtimeScope.Container.Resolve<IFacilityCandidateCache>().MarkDynamicStateDirty();
        var fixture = new MaterialWorkFixture(target, "actual-fire:" + fireId, null,
            (out string reason) => { reason = "fire red-test does not request invalidation"; return false; });
        WorkPhaseResult completion = null;
        IEnumerator run = RunLivePhase(workTypeId, target, WorkProbeFault.CompleteNormally,
            fixture, value => completion = value,
            completionAuthoritySatisfied: () => w01RepairObservation.CompletedCalls == 1
                && fires.History.Any(value => value.Fire.FireId == fireId
                    && value.EndReason == EnvironmentalFireEndReason.Suppressed));
        while (run.MoveNext()) yield return run.Current;
        w01RepairObservation.Dispose();
        WorkExecutionResult terminal = w01RepairObservation.Result;
        results.Add(new WorkTypeLiveRow(workTypeId.Value, completion?.Passed == true ? "PASS" : "FAIL",
            targetId, (completion?.Format() ?? "actual AI completion not observed")
                + "; fire=" + fireId + "; initialIntensity=" + initialWork.Fire.Intensity));
        if (terminal == null || !terminal.CompletedSuccessfully || string.IsNullOrEmpty(terminal.ChildResultKey))
        {
            results.Add(new WorkTypeLiveRow("w01:fire-terminal-child-join", "FAIL", targetId,
                "calls=" + w01RepairObservation.CompletedCalls + "; successful=" + terminal?.CompletedSuccessfully
                    + "; child=" + terminal?.ChildResultKey
                    + "; domainFailure=" + terminal?.Failure + "; axis=" + terminal?.FailureAxis));
            yield break;
        }
        if (!fireGate.Held)
            throw new InvalidOperationException("W01 actual fire did not reach its mandatory identity command.");
        var frozenRequest = fireGate.Request;
        brain.StopCurrentActionForReplan("W01 fire post-child checkpoint");
        work.ClearPriorityWorkTarget();
        actor.SetAiPaused(true);
        string fireEvidence = Path.GetDirectoryName(CharacterAiWorkTypeLiveMatrixPlayModeVerifier.W01WorkTerminalJoinReportPath);
        Directory.CreateDirectory(fireEvidence);
        var pendingSave = gameSaveService.Capture();
        string pendingJson = gameSaveService.ToJson(pendingSave, true);
        WriteUtf8LfCreateNew(Path.Combine(fireEvidence, "w01-fire-post-child-world.json"), new[] { pendingJson });
        var pendingOwner = persistence.Capture().suppressionOperations.Single(value => value.operationId == frozenRequest.OperationId);
        var pendingChild = W01ExactOutcomes().Single(value => W01ResultKey(value) == frozenRequest.ChildResultKey);
        bool checkpoint = EnvironmentalFireCompletionIdentity.RequiresIdentity(pendingOwner)
            && !pendingOwner.completionIdentityAcknowledged && !pendingOwner.outcomePending
            && pendingOwner.commandOrigin == (int)CharacterCommandOrigin.DirectPlayerOrder
            && CaptureIdentities().Length == identityKeysBefore.Count;
        var invalid = gameSaveService.FromJson(pendingJson);
        var ledgerSection = invalid.sections.Single(value => value.sectionId == GameplayOutcomeLedgerSaveSection.Id);
        var missingChild = DungeonSaveJson.Deserialize<GameplayOutcomeLedgerSaveData>(ledgerSection.payloadJson);
        int removed = missingChild.exactOutcomes.RemoveAll(value => W01ResultKey(value) == frozenRequest.ChildResultKey)
            + missingChild.outbox.RemoveAll(value => W01ResultKey(value) == frozenRequest.ChildResultKey);
        if (removed != 1) throw new InvalidOperationException("W01 requires one actual fire child for corruption test.");
        ledgerSection.payloadJson = JsonUtility.ToJson(missingChild);
        invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);
        WriteUtf8LfCreateNew(Path.Combine(fireEvidence, "w01-fire-missing-child-invalid-world.json"), new[] { gameSaveService.ToJson(invalid, true) });
        var root = runtimeScope.Container.Resolve<DungeonRuntimeAggregateRootStore>();
        int revision = root.PublishedRestoreRevision;
        string beforeInvalid = W05SectionPayloads(gameSaveService.Capture());
        bool accepted = gameSaveService.TryRestore(invalid, out var rejection);
        bool rejected = !accepted && rejection.Errors.Any(value => value.Contains("Fire completion", StringComparison.Ordinal))
            && revision == root.PublishedRestoreRevision && !root.IsRestoreStaging
            && beforeInvalid == W05SectionPayloads(gameSaveService.Capture());
        results.Add(new WorkTypeLiveRow("w01:fire-missing-child-rejected", rejected ? "PASS" : "FAIL", targetId,
            "accepted=" + accepted + "; unchanged=" + rejected + "; errors=" + string.Join(" | ", rejection.Errors)));
        if (!rejected) throw new InvalidOperationException("W01 missing fire child was not rejected atomically.");
        if (!gameSaveService.TryRestore(gameSaveService.FromJson(pendingJson), out var pendingRestore) || !pendingRestore.Success)
            throw new InvalidOperationException("W01 pending fire restore failed: " + string.Join(" | ", pendingRestore.Errors));
        actor = LiveActors(worldRegistry).Single(value => value.Identity.PersistentId == actorId);
        work = actor.GetComponent<AbilityWork>();
        brain = actor.Brain;
        actor.SetAiPaused(true);
        grid = FindFirstObjectByType<GridSystemManager>()?.grid;
        var pendingTarget = worldRegistry.Buildings.Single(value => value != null && value.PersistentInstanceId.Value == targetId);
        bool pendingFresh = !ReferenceEquals(target, pendingTarget);
        target = pendingTarget;
        fireGate.Released = true;
        float pendingGame = actor.GameClock.Time, pendingWall = Time.realtimeSinceStartup;
        int pendingFrame = Time.frameCount;
        while (!persistence.Capture().suppressionOperations.Single(value => value.operationId == frozenRequest.OperationId).completionIdentityAcknowledged)
        {
            if (actor.GameClock.Time - pendingGame > 15f && Time.frameCount - pendingFrame > 120
                || Time.realtimeSinceStartup - pendingWall > 120f)
                throw new InvalidOperationException("W01 pending fire identity did not finish via production Tick.");
            yield return null;
        }
        var resumedChildren = W01ExactOutcomes().Where(value => W01ResultKey(value) == frozenRequest.ChildResultKey).ToArray();
        bool resumed = checkpoint && pendingFresh && resumedChildren.Length == 1
            && resumedChildren[0].immutablePayloadHash == pendingChild.immutablePayloadHash
            && CaptureIdentities().Length == identityKeysBefore.Count + 1;
        results.Add(new WorkTypeLiveRow("w01:fire-pending-save-resume", resumed ? "PASS" : "FAIL", targetId,
            "sections=" + pendingSave.sections.Count + "; checkpoint=" + checkpoint + "; fresh=" + pendingFresh
                + "; childCount=" + resumedChildren.Length + "; identityCount=" + CaptureIdentities().Length));
        var child = W01ExactOutcomes().Single(value => W01ResultKey(value) == terminal.ChildResultKey);
        var suppression = persistence.Capture().suppressionOperations.Single(value => value.operationId == child.operationId);
        var history = fires.History.Single(value => value.Fire.FireId == fireId);
        bool directUnchanged = W05DeliveryCursorEquals(directBefore,
            FindW05DeliveryCursor(w05IdentityDeliveries.Capture(), WorkCompletionIdentityDeliveryLedger.DirectEventStreamId));
        int genericEvents = w05IdentityEvents.Count(value => value.WorkId == workTypeId.Value);
        bool joined = w01RepairObservation.CompletedCalls == 1 && completion?.ProgressDelta > 0
            && w01RepairObservation.ActorId == actorId && w01RepairObservation.TargetId == targetId
            && terminal.CompletionIdentityAlreadyPublished && terminal.CompletionEffectsAlreadyApplied
            && terminal.TerminalOwnerId == EnvironmentOutcomeIds.FireSuppressionProducer
            && terminal.TargetEntityId == targetId && terminal.ContributionWorkUnits.HasValue
            && Math.Abs(terminal.ContributionWorkUnits.Value - suppression.approvedWork) < 0.0001f
            && child.outcomeTypeId == EnvironmentOutcomeIds.FireSuppression.Value
            && child.status == GameplayOutcomeStatus.Succeeded && child.absoluteDay == suppression.absoluteDay
            && child.participants.Any(value => value.entityId == actorId && value.roleId == EnvironmentOutcomeIds.ActorRole.Value)
            && child.participants.Any(value => value.entityId == fireId && value.roleId == EnvironmentOutcomeIds.FireRole.Value)
            && suppression.workerId == actorId && suppression.fireId == fireId && !suppression.outcomePending
            && suppression.resultDisposition == (int)EnvironmentalFireSuppressionDisposition.Extinguished
            && suppression.intensityAfter == 0f && suppression.mode == (int)initialWork.Mode
            && suppression.waterCommittedQuantity == (waterMode ? initialWork.RequiredWater : 0)
            && (!waterMode || suppression.waterAcknowledged)
            && history.EndReason == EnvironmentalFireEndReason.Suppressed
            && history.Fire.TotalWaterConsumed == (waterMode ? initialWork.RequiredWater : 0)
            && genericEvents == 0 && directUnchanged;
        results.Add(new WorkTypeLiveRow("w01:fire-terminal-child-join", joined ? "PASS" : "FAIL", targetId,
            "fire=" + fireId + "; operation=" + child.operationId + "; child=" + terminal.ChildResultKey
                + "; owner=" + terminal.TerminalOwnerId + "; worker=" + actorId
                + "; intensity=" + suppression.intensityBefore + "->" + suppression.intensityAfter
                + "; finalWork=" + terminal.ContributionWorkUnits + "/" + suppression.approvedWork
                + "; totalWork=" + history.Fire.TotalSuppressionWork + "; water=" + history.Fire.TotalWaterConsumed
                + "; genericEvents=" + genericEvents + "; directUnchanged=" + directUnchanged));
        if (waterMode)
        {
            var waterRows = W01ExactOutcomes().Where(value => value.outcomeTypeId == EnvironmentOutcomeIds.FireWaterConsumed.Value
                && value.operationId == suppression.operationId).ToArray();
            var waterCompletionIdentity = CaptureIdentities().SingleOrDefault(value =>
                Fact(value, WorkCompletionIdentityOutcomeIds.ChildResultKeyFact) == terminal.ChildResultKey);
            // The parent reserves its ledger sequence before the physical sink.
            // Sequence is preparation order, not effect/commit time. Join the
            // actual physical receipt and require both results before identity.
            bool waterOnce = waterRows.Length == 1 && initialWork.RequiredWater == 1
                && physicalWaterBefore - CapturePhysicalItemQuantity("resource:clean-water") == initialWork.RequiredWater
                && !string.IsNullOrEmpty(suppression.waterLeaseId) && !string.IsNullOrEmpty(suppression.waterCommitId)
                && suppression.waterAcknowledged && waterCompletionIdentity != null
                && waterRows[0].sequence < waterCompletionIdentity.sequence
                && waterRows[0].status == GameplayOutcomeStatus.Succeeded
                && waterRows[0].absoluteDay == suppression.absoluteDay
                && Fact(waterRows[0], EnvironmentOutcomeIds.CommitFact) == suppression.waterCommitId
                && Fact(waterRows[0], EnvironmentOutcomeIds.CorrelationFact) == suppression.operationId
                && Fact(child, EnvironmentOutcomeIds.CorrelationFact) == suppression.operationId
                && waterRows[0].metrics.Single(value => value.metricId == EnvironmentOutcomeIds.QuantityMetric.Value).value == initialWork.RequiredWater
                && child.metrics.Single(value => value.metricId == EnvironmentOutcomeIds.QuantityMetric.Value).value == initialWork.RequiredWater
                && waterRows[0].participants.Any(value => value.entityId == actorId && value.roleId == EnvironmentOutcomeIds.ActorRole.Value)
                && waterRows[0].participants.Any(value => value.entityId == "resource:clean-water" && value.roleId == EnvironmentOutcomeIds.ItemRole.Value);
            results.Add(new WorkTypeLiveRow("w01:fire-water-consumed-once", waterOnce ? "PASS" : "FAIL", targetId,
                "required=" + initialWork.RequiredWater + "; physicalDelta=" + (physicalWaterBefore - CapturePhysicalItemQuantity("resource:clean-water"))
                    + "; rows=" + waterRows.Length + "; waterSequence=" + waterRows.FirstOrDefault()?.sequence
                    + "; childSequence=" + child.sequence + "; lease=" + suppression.waterLeaseId
                    + "; commit=" + suppression.waterCommitId + "; acknowledged=" + suppression.waterAcknowledged
                    + "; identitySequence=" + waterCompletionIdentity?.sequence
                    + "; receiptCommit=" + (waterRows.Length == 1 ? Fact(waterRows[0], EnvironmentOutcomeIds.CommitFact) : "missing")));
        }
        var identities = CaptureIdentities().Where(value => !identityKeysBefore.Contains(W01ResultKey(value))).ToArray();
        var identity = identities.SingleOrDefault(value => Fact(value, WorkCompletionIdentityOutcomeIds.ChildResultKeyFact) == terminal.ChildResultKey);
        double identityWork = identity?.metrics.FirstOrDefault(value =>
            value.metricId == WorkCompletionIdentityOutcomeIds.ContributionMetric.Value)?.value ?? double.NaN;
        bool identityJoined = identities.Length == 1 && suppression.completionIdentityAcknowledged
            && identity != null && identity.status == GameplayOutcomeStatus.Succeeded
            && identity.sequence > child.sequence && identity.absoluteDay == child.absoluteDay
            && Fact(identity, WorkCompletionIdentityOutcomeIds.OperationIdFact) == child.operationId
            && Fact(identity, WorkCompletionIdentityOutcomeIds.TerminalOwnerFact) == terminal.TerminalOwnerId
            && identity.subjects.Any(value => value.entityKindId == WorkCompletionIdentityOutcomeIds.BuildingKind.Value
                && value.entityId == targetId)
            && terminal.ContributionWorkUnits.HasValue
            && Math.Abs(identityWork - terminal.ContributionWorkUnits.Value) < 0.0001d;
        results.Add(new WorkTypeLiveRow("w01:fire-identity-child-join", identityJoined ? "PASS" : "FAIL", targetId,
            "identities=" + identities.Length + "; child=" + terminal.ChildResultKey
                + "; identityChild=" + Fact(identity, WorkCompletionIdentityOutcomeIds.ChildResultKeyFact)
                + "; childSequence=" + child.sequence + "; identitySequence=" + identity?.sequence
                + "; identityOperation=" + Fact(identity, WorkCompletionIdentityOutcomeIds.OperationIdFact)
                + "; identityOwner=" + Fact(identity, WorkCompletionIdentityOutcomeIds.TerminalOwnerFact)
                + "; work=" + identityWork + "/" + terminal.ContributionWorkUnits));
        brain.StopCurrentActionForReplan("W01 fire completed checkpoint");
        work.ClearPriorityWorkTarget();
        actor.SetAiPaused(true);
        var completedSave = gameSaveService.Capture();
        string completedJson = gameSaveService.ToJson(completedSave, true);
        string directory = Path.GetDirectoryName(CharacterAiWorkTypeLiveMatrixPlayModeVerifier.W01WorkTerminalJoinReportPath);
        Directory.CreateDirectory(directory);
        WriteUtf8LfCreateNew(Path.Combine(directory, "w01-fire-completed-world.json"), new[] { completedJson });
        if (!gameSaveService.TryRestore(gameSaveService.FromJson(completedJson), out var restored) || !restored.Success)
            throw new InvalidOperationException("W01 fire completed restore rejected: " + string.Join(" | ", restored.Errors));
        actor = LiveActors(worldRegistry).Single(value => value.Identity.PersistentId == actorId);
        work = actor.GetComponent<AbilityWork>();
        brain = actor.Brain;
        grid = FindFirstObjectByType<GridSystemManager>()?.grid;
        var freshTarget = worldRegistry.Buildings.Single(value => value != null && value.PersistentInstanceId.Value == targetId);
        bool fresh = !ReferenceEquals(target, freshTarget);
        var restoredChildren = W01ExactOutcomes().Where(value => W01ResultKey(value) == terminal.ChildResultKey).ToArray();
        bool childRestored = fresh && restoredChildren.Length == 1
            && restoredChildren[0].immutablePayloadHash == child.immutablePayloadHash
            && !fires.ActiveFires.Any(value => value.FireId == fireId)
            && fires.History.Single(value => value.Fire.FireId == fireId).EndReason == EnvironmentalFireEndReason.Suppressed;
        results.Add(new WorkTypeLiveRow("w01:fire-terminal-save-join", childRestored ? "PASS" : "FAIL", targetId,
            "officialSections=" + completedSave.sections.Count + "; freshTarget=" + fresh
                + "; children=" + restoredChildren.Length + "; childHash=" + child.immutablePayloadHash));
        string beforeReplay = W05SectionPayloads(gameSaveService.Capture());
        var replayCommand = new EnvironmentalFireSuppressionCommand(suppression.operationId, suppression.fireId,
            suppression.workerId, new Vector2Int(suppression.standX, suppression.standY),
            (EnvironmentalFireSuppressionMode)suppression.mode, suppression.approvedWork,
            suppression.waterLeaseId, suppression.waterQuantity, suppression.workerDisplayName,
            (CharacterCommandOrigin)suppression.commandOrigin);
        var replayOne = commands.TryApplySuppression(replayCommand);
        var replayTwo = commands.TryApplySuppression(replayCommand);
        var changed = commands.TryApplySuppression(new EnvironmentalFireSuppressionCommand(
            suppression.operationId, suppression.fireId, suppression.workerId, replayCommand.StandPosition,
            replayCommand.Mode, suppression.approvedWork + 1f, suppression.waterLeaseId,
            suppression.waterQuantity, suppression.workerDisplayName,
            (CharacterCommandOrigin)suppression.commandOrigin));
        bool replaySafe = replayOne.Disposition == EnvironmentalFireSuppressionDisposition.PreviouslyApplied
            && replayTwo.Disposition == EnvironmentalFireSuppressionDisposition.PreviouslyApplied
            && replayOne.OutcomeResultKey == terminal.ChildResultKey && replayTwo.OutcomeResultKey == terminal.ChildResultKey
            && changed.Disposition == EnvironmentalFireSuppressionDisposition.OperationConflict
            && beforeReplay == W05SectionPayloads(gameSaveService.Capture());
        results.Add(new WorkTypeLiveRow("w01:fire-domain-replay", replaySafe ? "PASS" : "FAIL", targetId,
            "first=" + replayOne.Disposition + "; second=" + replayTwo.Disposition + "; changed=" + changed.Disposition
                + "; unchanged=" + replaySafe + "; child=" + replayOne.OutcomeResultKey));
        var restoredIdentities = CaptureIdentities().Where(value => !identityKeysBefore.Contains(W01ResultKey(value))).ToArray();
        bool identityRestored = identityJoined && restoredIdentities.Count(value =>
            W01ResultKey(value) == W01ResultKey(identity) && value.immutablePayloadHash == identity.immutablePayloadHash) == 1
            && restoredIdentities.Length == identities.Length && directUnchanged;
        results.Add(new WorkTypeLiveRow("w01:fire-identity-save-join", identityRestored ? "PASS" : "FAIL", targetId,
            "identitiesBefore=" + identities.Length + "; identitiesAfter=" + restoredIdentities.Length
                + "; identityHash=" + (identity?.immutablePayloadHash ?? "missing")));
        string beforeIdentityReplay = W05SectionPayloads(gameSaveService.Capture());
        var delivery = runtimeScope.Container.Resolve<IWorkCompletionIdentityDeliveryCommand>();
        var identityReplayOne = delivery.EnsureApplied(frozenRequest);
        var identityReplayTwo = delivery.EnsureApplied(frozenRequest);
        var alteredRequest = new WorkCompletionIdentityDeliveryRequest(frozenRequest.DeliveryId,
            frozenRequest.ProducerStreamId, frozenRequest.OperationSequence, frozenRequest.Character,
            frozenRequest.WorkId, frozenRequest.ProductId, frozenRequest.Origin, frozenRequest.AbsoluteDay,
            frozenRequest.OperationId, frozenRequest.TerminalOwnerId, frozenRequest.ChildResultKey,
            frozenRequest.TargetEntityId, frozenRequest.ContributionWorkUnits + 1f);
        var identityConflict = delivery.EnsureApplied(alteredRequest);
        bool identitiesStable = identityReplayOne.Status == WorkCompletionIdentityDeliveryStatus.AlreadyApplied
            && identityReplayTwo.Status == WorkCompletionIdentityDeliveryStatus.AlreadyApplied
            && identityConflict.Status == WorkCompletionIdentityDeliveryStatus.Conflict
            && beforeIdentityReplay == W05SectionPayloads(gameSaveService.Capture());
        results.Add(new WorkTypeLiveRow("w01:fire-identity-replay", identitiesStable ? "PASS" : "FAIL", targetId,
            "first=" + identityReplayOne.Status + "; second=" + identityReplayTwo.Status
                + "; changed=" + identityConflict.Status + "; unchanged=" + identitiesStable));

        GameplayOutcomeSnapshot[] CaptureIdentities() => W01ExactOutcomes().Where(value =>
            value.outcomeTypeId == WorkCompletionIdentityOutcomeIds.Applied.Value
            && Fact(value, WorkCompletionIdentityOutcomeIds.WorkIdFact) == workTypeId.Value
            && value.participants.Any(participant => participant.entityId == actorId
                && participant.roleId == WorkCompletionIdentityOutcomeIds.WorkerRole.Value)).ToArray();
        static string Fact(GameplayOutcomeSnapshot value, GameplayOutcomeFactId fact) =>
            value?.facts.FirstOrDefault(item => item.factId == fact.Value)?.value ?? "missing";
    }

    private IEnumerator RunW04RepairLiveFocused()
    {
        const string RepairOutcomeType = "work.repair-resolved";
        WorkTypeId repairWork = BuiltInWorkTypeIds.Repair;
        string workerId = actor.Identity.PersistentId;
        w04RepairDeliveries = new W04RepairDeliveryObservation(
            (WorkOrderRuntime)workOrders,
            runtimeScope.Container.Resolve<IDomainFactPublisher>(),
            runtimeScope.Container.Resolve<IWorkCompletionIdentityDeliveryCommand>(),
            runtimeScope.Container.Resolve<WorkCompletionIdentityDeliveryLedger>(),
            runtimeScope.Container.Resolve<CharacterIdentityEventPublisher>(),
            runtimeScope.Container.Resolve<WorkIdentityAppliedFactMapping>());
        List<W04RepairArmResult> completedArms = new(5);

        BuildingSO generalDefinition = LoadAuthoredBuilding(data =>
            data.Facility?.SupportsWork(repairWork) == true
            && string.Equals(
                data.GetAbility<BuildingFacilityPartAbility>()?.code,
                "S02",
                StringComparison.Ordinal)
            && data.GetAbility<BuildingStructuralIntegrityAbility>() == null
            && data.GetAbility<BuildingAutomationAbility>() == null
            && data.GetAbility<BuildingEquipmentMaintenanceAbility>() == null
            && data.Defense?.IsDefenseFacility != true);
        BuildableObject general = PlaceAuthoredBuilding(
            generalDefinition,
            out string generalPlacementFailure);
        if (general == null)
        {
            results.Add(new WorkTypeLiveRow(
                "w04:general-repair", "FAIL", "general",
                "fixture=" + generalPlacementFailure));
        }
        else
        {
            general.SetDamaged(true);
            string targetId = general.RequirePersistentInstanceId().Value;
            W04RepairArmResult arm = null;
            IEnumerator run = RunW04RepairArm(
                general,
                targetId,
                "general-damage",
                () => !general.IsDamaged,
                value => arm = value);
            while (run.MoveNext()) yield return run.Current;
            bool passed = arm?.Joined == true
                && !general.IsDamaged
                && W04Metric(arm.Child, MigratedProducerOutcomeIds.PreviousMetric.Value) == 1d
                && W04Metric(arm.Child, MigratedProducerOutcomeIds.CurrentMetric.Value) == 0d;
            results.Add(new WorkTypeLiveRow(
                "w04:general-repair", passed ? "PASS" : "FAIL", targetId,
                (arm?.Detail ?? "arm-missing") + "; damaged=true->" + general.IsDamaged));
            if (arm != null) completedArms.Add(arm);
        }

        MaterialWorkFixture structuralFixture = PrepareRepairFixture(
            out string structuralFailure);
        if (structuralFixture == null)
        {
            results.Add(new WorkTypeLiveRow(
                "w04:structural-repair", "FAIL", "structural",
                "fixture=" + structuralFailure));
        }
        else
        {
            BuildableObject structural = structuralFixture.Target;
            string targetId = structural.RequirePersistentInstanceId().Value;
            IBuildingStructuralIntegrityRuntime integrity = runtimeScope.Container
                .Resolve<IBuildingStructuralIntegrityRuntime>();
            bool capturedBefore = integrity.TryGet(
                structural,
                out BuildingStructuralIntegritySnapshot before);
            W04RepairArmResult arm = null;
            IEnumerator run = RunW04RepairArm(
                structural,
                targetId,
                "structural-integrity",
                () => integrity.TryGet(
                    structural,
                    out BuildingStructuralIntegritySnapshot state)
                    && Mathf.Approximately(
                        state.CurrentHitPoints,
                        state.MaxHitPoints),
                value => arm = value,
                structuralFixture);
            while (run.MoveNext()) yield return run.Current;
            bool capturedAfter = integrity.TryGet(
                structural,
                out BuildingStructuralIntegritySnapshot after);
            bool passed = arm?.Joined == true
                && capturedBefore
                && capturedAfter
                && before.CurrentHitPoints < before.MaxHitPoints
                && Mathf.Approximately(after.CurrentHitPoints, after.MaxHitPoints)
                && Math.Abs(W04Metric(
                    arm.Child,
                    MigratedProducerOutcomeIds.PreviousMetric.Value)
                    - before.CurrentHitPoints) < 0.001d
                && Math.Abs(W04Metric(
                    arm.Child,
                    MigratedProducerOutcomeIds.CurrentMetric.Value)
                    - after.CurrentHitPoints) < 0.001d;
            results.Add(new WorkTypeLiveRow(
                "w04:structural-repair", passed ? "PASS" : "FAIL", targetId,
                (arm?.Detail ?? "arm-missing") + "; hp="
                    + (capturedBefore
                        ? before.CurrentHitPoints.ToString()
                        : "missing")
                    + "->" + (capturedAfter
                        ? after.CurrentHitPoints.ToString()
                        : "missing")
                    + "/" + (capturedAfter
                        ? after.MaxHitPoints.ToString()
                        : "missing")));
            if (arm != null) completedArms.Add(arm);
        }

        BuildingSO defenseDefinition = LoadAuthoredBuilding(data =>
            data.Facility?.SupportsWork(repairWork) == true
            && data.Defense?.IsDefenseFacility == true);
        DefenseFacility defense = PlaceAuthoredBuilding(
            defenseDefinition,
            out string defensePlacementFailure) as DefenseFacility;
        if (defense == null)
        {
            results.Add(new WorkTypeLiveRow(
                "w04:defense-repair", "FAIL", "defense",
                "fixture=" + defensePlacementFailure));
        }
        else
        {
            DefenseFacilityRuntime defenseRuntime = runtimeScope.Container
                .Resolve<IDefenseFacilityRuntime>() as DefenseFacilityRuntime;
            string targetId = defense.RequirePersistentInstanceId().Value;
            _ = defenseRuntime?.GetSnapshot(defense);
            DefenseFacilitySaveData save = defenseRuntime?.CaptureState();
            DefenseFacilityRecordSaveData row = save?.facilities.SingleOrDefault(
                value => value.facilityPersistentId == targetId);
            if (defenseRuntime == null || row == null)
            {
                results.Add(new WorkTypeLiveRow(
                    "w04:defense-repair", "FAIL", targetId,
                    "production defense state unavailable"));
            }
            else
            {
                row.condition = 45f;
                row.operationalState = DefenseFacilityOperationalState.Ready;
                row.blockedReason = string.Empty;
                defenseRuntime.PublishRestoreState(
                    defenseRuntime.PrepareRestoreState(save));
                defense.SetDamaged(true);
                DefenseFacilitySnapshot before = defenseRuntime.GetSnapshot(defense);
                W04RepairArmResult arm = null;
                IEnumerator run = RunW04RepairArm(
                    defense,
                    targetId,
                    "defense-facility",
                    () => defenseRuntime.GetSnapshot(defense).Condition >= 99.999f,
                    value => arm = value);
                while (run.MoveNext()) yield return run.Current;
                DefenseFacilitySnapshot after = defenseRuntime.GetSnapshot(defense);
                bool passed = arm?.Joined == true
                    && before.Condition < 100f
                    && after.Condition >= 99.999f
                    && Math.Abs(W04Metric(
                        arm.Child,
                        MigratedProducerOutcomeIds.PreviousMetric.Value)
                        - before.Condition) < 0.001d
                    && Math.Abs(W04Metric(
                        arm.Child,
                        MigratedProducerOutcomeIds.CurrentMetric.Value)
                        - after.Condition) < 0.001d;
                results.Add(new WorkTypeLiveRow(
                    "w04:defense-repair", passed ? "PASS" : "FAIL", targetId,
                    (arm?.Detail ?? "arm-missing") + "; condition="
                        + before.Condition + "->" + after.Condition));
                if (arm != null) completedArms.Add(arm);
            }
        }

        BuildingSO automationDefinition = LoadAuthoredBuilding(data =>
            data.Facility?.SupportsWork(repairWork) == true
            && data.GetAbility<BuildingAutomationAbility>() is
                BuildingAutomationAbility ability
            && ability.maximumMode >= AutomationMode.PoweredAssist);
        BuildableObject automationTarget = PlaceAuthoredBuilding(
            automationDefinition,
            out string automationPlacementFailure);
        if (automationTarget == null)
        {
            results.Add(new WorkTypeLiveRow(
                "w04:automation-repair", "FAIL", "automation",
                "fixture=" + automationPlacementFailure));
        }
        else
        {
            string targetId = automationTarget.RequirePersistentInstanceId().Value;
            _ = automation.TryGetFacility(automationTarget, out _);
            DungeonAutomationSaveData save = automationPersistence.Capture();
            AutomationFacilitySaveData row = save.facilities.SingleOrDefault(
                value => value.buildingInstanceId == targetId);
            if (row == null)
            {
                results.Add(new WorkTypeLiveRow(
                    "w04:automation-repair", "FAIL", targetId,
                    "production automation state unavailable"));
            }
            else
            {
                row.mode = AutomationMode.PoweredAssist;
                row.maintenance = 40f;
                row.fault = 20f;
                automationPersistence.Restore(
                    automationPersistence.PrepareRestore(save));
                automation.TryGetFacility(
                    automationTarget,
                    out AutomationFacilitySnapshot before);
                W04RepairArmResult arm = null;
                IEnumerator run = RunW04RepairArm(
                    automationTarget,
                    targetId,
                    "automation-maintenance",
                    () => automation.TryGetFacility(
                        automationTarget,
                        out AutomationFacilitySnapshot state)
                        && state.Maintenance >= 85f
                        && state.Fault <= 0.001f,
                    value => arm = value);
                while (run.MoveNext()) yield return run.Current;
                automation.TryGetFacility(
                    automationTarget,
                    out AutomationFacilitySnapshot after);
                bool passed = arm?.Joined == true
                    && before != null
                    && after != null
                    && before.Maintenance < 85f
                    && after.Maintenance >= 85f
                    && after.Fault <= 0.001f
                    && Math.Abs(W04Metric(
                        arm.Child,
                        MigratedProducerOutcomeIds.PreviousMetric.Value)
                        - before.Maintenance) < 0.001d
                    && Math.Abs(W04Metric(
                        arm.Child,
                        MigratedProducerOutcomeIds.CurrentMetric.Value)
                        - after.Maintenance) < 0.001d;
                results.Add(new WorkTypeLiveRow(
                    "w04:automation-repair", passed ? "PASS" : "FAIL", targetId,
                    (arm?.Detail ?? "arm-missing") + "; maintenance="
                        + before?.Maintenance + "->" + after?.Maintenance
                        + "; fault=" + before?.Fault + "->" + after?.Fault));
                if (arm != null) completedArms.Add(arm);
            }
        }

        IEnumerator equipmentRun = RunW04EquipmentRepair(
            completedArms,
            RepairOutcomeType);
        while (equipmentRun.MoveNext())
            yield return equipmentRun.Current;

        string[] expectedKinds =
        {
            "general-damage",
            "structural-integrity",
            "defense-facility",
            "automation-maintenance",
            "equipment-durability"
        };
        bool fiveKinds = completedArms.Count == 5
            && expectedKinds.All(kind => completedArms.Count(value =>
                value.Kind == kind && value.Joined) == 1)
            && completedArms.All(value => value.WorkerId == workerId)
            && completedArms.Select(value => value.ChildResultKey)
                .Distinct(StringComparer.Ordinal).Count() == 5
            && completedArms.Select(value => value.Request.DeliveryId)
                .Distinct(StringComparer.Ordinal).Count() == 5;
        results.Add(new WorkTypeLiveRow(
            "w04:five-kind-ledger-join",
            fiveKinds ? "PASS" : "FAIL",
            workerId,
            "actualHandlerKinds=" + string.Join(",", completedArms.Select(value => value.Kind))
                + "; joined=" + completedArms.Count(value => value.Joined)
                + "; children=" + completedArms.Select(value => value.ChildResultKey)
                    .Distinct(StringComparer.Ordinal).Count()
                + "; identities=" + completedArms.Select(value => value.Request.DeliveryId)
                    .Distinct(StringComparer.Ordinal).Count()
                + "; expectedOutcome=" + RepairOutcomeType));
    }

    private IEnumerator RunW06ResourceCausalityFocused()
    {
        if (!Application.isBatchMode || !DungeonPersistencePaths.IsIsolatedVerification)
        {
            throw new InvalidOperationException(
                "W06 requires the owned isolated PlayMode entry.");
        }

        CharacterActor[] workers = LiveActors(worldRegistry)
            .Where(value => value != null
                && value.Identity != null
                && !string.IsNullOrWhiteSpace(value.Identity.PersistentId)
                && value.GetComponent<AbilityWork>() != null)
            .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        if (workers.Length != 2)
        {
            throw new InvalidOperationException(
                "W06 requires two persistent production workers.");
        }

        IWorkExecutionHandlerRegistry registry = runtimeScope.Container
            .Resolve<IWorkExecutionHandlerRegistry>();
        if (!registry.TryGet(BuiltInWorkTypeIds.Sow, out IWorkExecutionHandler sowHandler)
            || !registry.TryGet(BuiltInWorkTypeIds.Gather, out IWorkExecutionHandler gatherHandler)
            || !registry.TryGet(BuiltInWorkTypeIds.Logging, out IWorkExecutionHandler loggingHandler)
            || sowHandler is not ResourceGatheringWorkExecutionHandler
            || !ReferenceEquals(sowHandler, gatherHandler)
            || !ReferenceEquals(sowHandler, loggingHandler))
        {
            throw new InvalidOperationException(
                "W06 requires the registered production resource handler for Sow/Gather/Logging.");
        }

        IGameCalendar calendar = runtimeScope.Container.Resolve<IGameCalendar>();
        int startedDay = calendar.Day;
        string firstWorkerId = workers[0].Identity.PersistentId;
        string secondWorkerId = workers[1].Identity.PersistentId;
        List<CropCycleExecutionReceiptSaveData> sowReceipts = new(2);
        List<float> sowRequiredWork = new(2);
        List<string> sowTargets = new(2);

        for (int plotIndex = 0; plotIndex < 2; plotIndex++)
        {
            if (!TryPrepareCropPlotFixture(
                    BuiltInWorkTypeIds.Sow,
                    out MaterialWorkFixture fixture,
                    out string fixtureFailure))
            {
                throw new InvalidOperationException(
                    "W06 sow fixture " + plotIndex + " failed: " + fixtureFailure);
            }

            BuildableObject target = fixture.Target;
            string targetId = target.RequirePersistentInstanceId().Value;
            if (!cropPlots.TryGetWork(
                    target,
                    BuiltInWorkTypeIds.Sow,
                    out CropPlotWorkSnapshot ready)
                || !ready.Available)
            {
                throw new InvalidOperationException(
                    "W06 sow target was not available: " + targetId);
            }

            if (plotIndex == 0)
            {
                WorkExecutionResult partial = null;
                float partialApplied = 0f;
                IEnumerator partialRun = RunW06HandlerSlice(
                    sowHandler,
                    workers[0],
                    target,
                    BuiltInWorkTypeIds.Sow,
                    ready.RequiredWork * 0.4f,
                    (result, applied) =>
                    {
                        partial = result;
                        partialApplied = applied;
                    });
                while (partialRun.MoveNext())
                    yield return partialRun.Current;
                if (partial == null
                    || partial.CompletedSuccessfully
                    || partialApplied <= 0f
                    || !cropPlots.TryGetWork(
                        target,
                        BuiltInWorkTypeIds.Sow,
                        out CropPlotWorkSnapshot remaining)
                    || remaining.CompletedWork <= 0f
                    || remaining.CompletedWork >= remaining.RequiredWork)
                {
                    throw new InvalidOperationException(
                        "W06 first sow worker did not leave authoritative partial progress.");
                }
            }

            CharacterActor terminalWorker = plotIndex == 0 ? workers[1] : workers[0];
            WorkExecutionResult terminal = null;
            float terminalApplied = 0f;
            IEnumerator terminalRun = RunW06HandlerSlice(
                sowHandler,
                terminalWorker,
                target,
                BuiltInWorkTypeIds.Sow,
                float.MaxValue,
                (result, applied) =>
                {
                    terminal = result;
                    terminalApplied = applied;
                });
            while (terminalRun.MoveNext())
                yield return terminalRun.Current;
            cropPlots.Tick();
            if (terminal == null || !terminal.CompletedSuccessfully
                || terminalApplied <= 0f)
            {
                throw new InvalidOperationException(
                    "W06 sow terminal handler failed: " + targetId);
            }

            CropPlotSaveData saved = cropPlots.Capture().plots.Single(value =>
                string.Equals(
                    value.buildingInstanceId,
                    targetId,
                    StringComparison.Ordinal));
            CropCycleExecutionReceiptSaveData receipt =
                saved.cycleExecutionReceipt?.DeepClone();
            if (receipt == null
                || receipt.status != CropCycleExecutionReceiptStatus.Active
                || receipt.plotId != targetId)
            {
                throw new InvalidOperationException(
                    "W06 sow receipt was not retained by the crop owner: " + targetId);
            }
            sowReceipts.Add(receipt);
            sowRequiredWork.Add(ready.RequiredWork);
            sowTargets.Add(targetId);
        }

        HashSet<string> resourceOutcomeKeysBefore = new(
            W01ExactOutcomes().Select(W01ResultKey),
            StringComparer.Ordinal);
        WorkTypeId[] resourceTypes =
        {
            BuiltInWorkTypeIds.Gather,
            BuiltInWorkTypeIds.Logging
        };
        List<GameplayOutcomeSnapshot> resourceOutcomes = new(2);
        List<float> resourceRequiredWork = new(2);
        List<string> resourceTargets = new(2);

        for (int resourceIndex = 0; resourceIndex < resourceTypes.Length; resourceIndex++)
        {
            WorkTypeId workTypeId = resourceTypes[resourceIndex];
            if (!TryPrepareWorldResourceFixture(
                    workTypeId,
                    out MaterialWorkFixture fixture,
                    out string fixtureFailure))
            {
                throw new InvalidOperationException(
                    "W06 " + workTypeId.Value + " fixture failed: " + fixtureFailure);
            }
            BuildableObject target = fixture.Target;
            WorldResourceNode node = (target as IWorldResourceNodeHost)?.ResourceNode;
            if (node == null
                || !worldResources.TryGetWork(
                    node,
                    workTypeId,
                    out WorldResourceWorkSnapshot ready)
                || !ready.Available)
            {
                throw new InvalidOperationException(
                    "W06 world-resource target was not available: " + workTypeId.Value);
            }

            if (resourceIndex == 0)
            {
                WorkExecutionResult partial = null;
                float partialApplied = 0f;
                IEnumerator partialRun = RunW06HandlerSlice(
                    gatherHandler,
                    workers[0],
                    target,
                    workTypeId,
                    ready.RequiredWork * 0.4f,
                    (result, applied) =>
                    {
                        partial = result;
                        partialApplied = applied;
                    });
                while (partialRun.MoveNext())
                    yield return partialRun.Current;
                if (partial == null
                    || partial.CompletedSuccessfully
                    || partialApplied <= 0f
                    || !worldResources.TryGetWork(
                        node,
                        workTypeId,
                        out WorldResourceWorkSnapshot remaining)
                    || remaining.CompletedWork <= 0f
                    || remaining.CompletedWork >= remaining.RequiredWork)
                {
                    throw new InvalidOperationException(
                        "W06 first resource worker did not leave authoritative partial progress.");
                }
            }

            WorkExecutionResult terminal = null;
            float terminalApplied = 0f;
            CharacterActor terminalWorker = resourceIndex == 0
                ? workers[1]
                : workers[0];
            IEnumerator terminalRun = RunW06HandlerSlice(
                resourceIndex == 0 ? gatherHandler : loggingHandler,
                terminalWorker,
                target,
                workTypeId,
                float.MaxValue,
                (result, applied) =>
                {
                    terminal = result;
                    terminalApplied = applied;
                });
            while (terminalRun.MoveNext())
                yield return terminalRun.Current;
            if (terminal == null || !terminal.CompletedSuccessfully
                || terminalApplied <= 0f)
            {
                throw new InvalidOperationException(
                    "W06 world-resource terminal handler failed: " + workTypeId.Value);
            }

            GameplayOutcomeSnapshot outcome = W01ExactOutcomes()
                .Where(value => !resourceOutcomeKeysBefore.Contains(W01ResultKey(value))
                    && value.outcomeTypeId == "world.resource-cycle-resolved"
                    && W04Fact(value, "work-type-id") == workTypeId.Value)
                .SingleOrDefault();
            if (outcome == null)
            {
                throw new InvalidOperationException(
                    "W06 world-resource outcome was not committed: " + workTypeId.Value);
            }
            resourceOutcomeKeysBefore.Add(W01ResultKey(outcome));
            resourceOutcomes.Add(outcome);
            resourceRequiredWork.Add(ready.RequiredWork);
            resourceTargets.Add(target.RequirePersistentInstanceId().Value);
        }

        bool sowMultiWorker = sowReceipts[0].sowContributions.Count == 2
            && sowReceipts[0].sowContributions.Any(value =>
                value.characterId == firstWorkerId && value.contributedWork > 0f)
            && sowReceipts[0].sowContributions.Any(value =>
                value.characterId == secondWorkerId && value.contributedWork > 0f);
        string gatherContributors = W04Fact(
            resourceOutcomes[0],
            MigratedProducerOutcomeIds.ContributorsFact.Value);
        bool resourceMultiWorker = gatherContributors.Split(',').Length == 2
            && gatherContributors.Contains(firstWorkerId + "=", StringComparison.Ordinal)
            && gatherContributors.Contains(secondWorkerId + "=", StringComparison.Ordinal);
        bool multiWorker = sowMultiWorker && resourceMultiWorker;
        results.Add(new WorkTypeLiveRow(
            "w06:multi-worker",
            multiWorker ? "PASS" : "FAIL",
            sowTargets[0] + "," + resourceTargets[0],
            "handler=ResourceGatheringWorkExecutionHandler; workers="
                + firstWorkerId + "," + secondWorkerId
                + "; sowContributors=" + string.Join(",", sowReceipts[0]
                    .sowContributions.Select(value => value.characterId + "="
                        + value.contributedWork.ToString("R")))
                + "; resourceContributors=" + gatherContributors));

        bool sameDayMultipleTargets = calendar.Day == startedDay
            && sowTargets.Distinct(StringComparer.Ordinal).Count() == 2
            && resourceOutcomes.Select(value => value.absoluteDay).Distinct().Count() == 1
            && resourceOutcomes[0].absoluteDay == startedDay
            && resourceOutcomes.Select(W01ResultKey)
                .Distinct(StringComparer.Ordinal).Count() == 2;
        results.Add(new WorkTypeLiveRow(
            "w06:same-day-multiple-targets",
            sameDayMultipleTargets ? "PASS" : "FAIL",
            string.Join(",", sowTargets.Concat(resourceTargets)),
            "day=" + startedDay + "->" + calendar.Day
                + "; plots=" + sowTargets.Count
                + "; resourceOperations=" + resourceOutcomes.Count
                + "; outcomeDays=" + string.Join(",",
                    resourceOutcomes.Select(value => value.absoluteDay))));

        bool sowCausality = sowReceipts.Select((receipt, index) => new
            {
                Receipt = receipt,
                Required = sowRequiredWork[index]
            })
            .All(value => value.Receipt.inputQuantity > 0
                && value.Receipt.inputMassGrams > 0L
                && !string.IsNullOrEmpty(value.Receipt.sowCommitId)
                && !string.IsNullOrEmpty(value.Receipt.inputVectorDigest)
                && value.Receipt.inputs.Sum(input => input.quantity)
                    == value.Receipt.inputQuantity
                && Math.Abs(value.Receipt.sowContributions
                    .Sum(contribution => contribution.contributedWork)
                    - value.Required) < 0.001f);
        bool resourceCausality = resourceOutcomes.Select((outcome, index) => new
            {
                Outcome = outcome,
                Required = resourceRequiredWork[index]
            })
            .All(value => W04Metric(
                    value.Outcome,
                    MigratedProducerOutcomeIds.PreviousMetric.Value)
                    > W04Metric(
                        value.Outcome,
                        MigratedProducerOutcomeIds.CurrentMetric.Value)
                && Math.Abs(W04Metric(
                        value.Outcome,
                        MigratedProducerOutcomeIds.CompletedWorkMetric.Value)
                    - Math.Round(value.Required * 1000d)) <= 1d
                && W04Metric(
                    value.Outcome,
                    MigratedProducerOutcomeIds.OutputQuantityMetric.Value) > 0d
                && W04Metric(
                    value.Outcome,
                    MigratedProducerOutcomeIds.OutputMassMetric.Value) > 0d
                && W04Fact(value.Outcome, "recipe-id") != "missing"
                && W04Fact(value.Outcome, "batch-commit-id") != "none"
                && W04Fact(value.Outcome, "output-vector") != "none");
        bool causal = sowCausality && resourceCausality;
        results.Add(new WorkTypeLiveRow(
            "w06:quantity-labor-causality",
            causal ? "PASS" : "FAIL",
            string.Join(",", resourceTargets),
            "sowInputs=" + string.Join(",", sowReceipts.Select(value =>
                    value.inputQuantity + "/" + value.inputMassGrams + "g"))
                + "; sowWork=" + string.Join(",", sowReceipts.Select(value =>
                    value.sowContributions.Sum(item => item.contributedWork)))
                + "; resource=" + string.Join(" | ", resourceOutcomes.Select(value =>
                    W04Fact(value, "work-type-id") + ":source="
                    + W04Metric(value, MigratedProducerOutcomeIds.PreviousMetric.Value)
                    + "->" + W04Metric(value, MigratedProducerOutcomeIds.CurrentMetric.Value)
                    + "; workMilli=" + W04Metric(value,
                        MigratedProducerOutcomeIds.CompletedWorkMetric.Value)
                    + "; output=" + W04Fact(value, "output-vector")))));
    }

    private IEnumerator RunW06HandlerSlice(
        IWorkExecutionHandler handler,
        CharacterActor worker,
        BuildableObject target,
        WorkTypeId workTypeId,
        float maximumApprovedWork,
        Action<WorkExecutionResult, float> completed)
    {
        AbilityWork workerAbility = worker?.GetComponent<AbilityWork>();
        if (handler == null || workerAbility == null || target == null
            || completed == null)
        {
            throw new ArgumentException("W06 handler slice arguments are incomplete.");
        }
        float appliedWork = 0f;
        WorkExecutionContext context = new(
            Time.frameCount,
            workerAbility,
            worker,
            target,
            workTypeId,
            (_, _, _) => W06UnexpectedTransientWork(),
            () => true,
            (required, current, _, _, applyDelta) => W06ApplyApprovedWork(
                required,
                current,
                maximumApprovedWork,
                applyDelta,
                amount => appliedWork += amount));
        WorkExecutionResult result = new();
        IEnumerator execution = handler.Execute(context, result);
        try
        {
            while (execution.MoveNext())
                yield return execution.Current;
        }
        finally
        {
            (execution as IDisposable)?.Dispose();
        }
        completed(result, appliedWork);
    }

    private static IEnumerator W06ApplyApprovedWork(
        float requiredWork,
        float completedWork,
        float maximumApprovedWork,
        Func<float, bool> applyDelta,
        Action<float> applied)
    {
        float remaining = Mathf.Max(0f, requiredWork - completedWork);
        float amount = Mathf.Min(remaining, maximumApprovedWork);
        if (amount <= 0f || !applyDelta(amount))
        {
            throw new InvalidOperationException(
                "W06 production handler rejected approved work.");
        }
        applied(amount);
        yield break;
    }

    private static IEnumerator W06UnexpectedTransientWork()
    {
        throw new InvalidOperationException(
            "W06 resource handler used the non-persistent work path.");
    }

    private IEnumerator RunW04RepairArm(
        BuildableObject target,
        string targetEntityId,
        string expectedKind,
        Func<bool> completionAuthority,
        Action<W04RepairArmResult> completed,
        MaterialWorkFixture fixture = null)
    {
        runtimeScope.Container.Resolve<IFacilityCandidateCache>()
            .MarkDynamicStateDirty();
        yield return null;
        const int MaximumAccidentRetries = 2;
        int accidentRetries = 0;
        W01RepairHandlerObservation observation = null;
        WorkPhaseResult phase = null;
        for (int attempt = 0; attempt <= MaximumAccidentRetries; attempt++)
        {
            PrepareActor(BuiltInWorkTypeIds.Repair);
            observation = new W01RepairHandlerObservation(
                runtimeScope.Container.Resolve<IWorkExecutionHandlerRegistry>());
            w01RepairObservation = observation;
            phase = null;
            IEnumerator run = RunLivePhase(
                BuiltInWorkTypeIds.Repair,
                target,
                WorkProbeFault.CompleteNormally,
                fixture,
                value => phase = value,
                completionAuthoritySatisfied: () =>
                    observation.CompletedCalls == 1 && completionAuthority());
            while (run.MoveNext())
                yield return run.Current;

            bool workAccident = phase?.ObservedTerminalKind
                    == CharacterAiActionTerminalKind.Failed
                && string.Equals(
                    brain.LastActionFailure.Reason,
                    "work-accident",
                    StringComparison.Ordinal);
            if (phase?.Passed == true
                || !workAccident
                || attempt == MaximumAccidentRetries)
            {
                break;
            }

            accidentRetries++;
            observation.Dispose();
            observation = null;
            w01RepairObservation = null;
        }
        observation.Dispose();
        w01RepairObservation = null;

        WorkExecutionResult terminal = observation.Result;
        GameplayOutcomeSnapshot[] children = string.IsNullOrEmpty(
                terminal?.ChildResultKey)
            ? Array.Empty<GameplayOutcomeSnapshot>()
            : W01ExactOutcomes().Where(value =>
                W01ResultKey(value) == terminal.ChildResultKey).ToArray();
        GameplayOutcomeSnapshot child = children.SingleOrDefault();
        WorkCompletionIdentityDeliveryRequest[] requests = terminal == null
            ? Array.Empty<WorkCompletionIdentityDeliveryRequest>()
            : w04RepairDeliveries.Requests.Where(value =>
                value.ChildResultKey == terminal.ChildResultKey).ToArray();
        WorkCompletionIdentityDeliveryRequest request = requests.SingleOrDefault();
        GameplayOutcomeSnapshot[] identities = terminal == null
            ? Array.Empty<GameplayOutcomeSnapshot>()
            : W01ExactOutcomes().Where(value =>
                value.outcomeTypeId == WorkCompletionIdentityOutcomeIds.Applied.Value
                && value.facts.Any(fact =>
                    fact.factId == WorkCompletionIdentityOutcomeIds.ChildResultKeyFact.Value
                    && fact.value == terminal.ChildResultKey)).ToArray();
        string workerId = actor.Identity.PersistentId;
        string facilityId = target.RequirePersistentInstanceId().Value;
        bool joined = phase?.Passed == true
            && observation.CompletedCalls == 1
            && observation.ActorId == workerId
            && observation.TargetId == facilityId
            && terminal?.CompletedSuccessfully == true
            && terminal.CompletionIdentityAlreadyPublished
            && terminal.TerminalOwnerId == "repair-work-outcome"
            && terminal.TargetEntityId == targetEntityId
            && terminal.ContributionWorkUnits is > 0f
            && children.Length == 1
            && child.outcomeTypeId == "work.repair-resolved"
            && child.status == GameplayOutcomeStatus.Succeeded
            && W04Fact(child, "repair-kind") == expectedKind
            && W04Fact(child, "repair-target-id") == targetEntityId
            && child.participants.Any(value =>
                value.entityId == workerId
                && value.roleId == MigratedProducerOutcomeIds.ActorRole.Value)
            && child.participants.Any(value =>
                value.entityId == facilityId
                && value.roleId == MigratedProducerOutcomeIds.FacilityRole.Value)
            && W04Metric(child, MigratedProducerOutcomeIds.CompletedWorkMetric.Value) > 0d
            && requests.Length == 1
            && request.Character.Value == workerId
            && request.TargetEntityId == targetEntityId
            && request.TerminalOwnerId == "repair-work-outcome"
            && identities.Length == 1
            && identities[0].sequence > child.sequence;
        completed(new W04RepairArmResult(
            expectedKind,
            workerId,
            facilityId,
            targetEntityId,
            phase,
            terminal,
            child,
            request,
            joined,
            "phase=" + (phase?.Passed == true)
                + "; accidentRetries=" + accidentRetries
                + "; phaseDetail=" + (phase?.Format() ?? "missing")
                + "; calls=" + observation.CompletedCalls
                + "; childCount=" + children.Length
                + "; identityCount=" + identities.Length
                + "; contribution=" + terminal?.ContributionWorkUnits
                + "; kind=" + W04Fact(child, "repair-kind")
                + "; target=" + W04Fact(child, "repair-target-id")
                + "; completedMilli="
                + W04Metric(child, MigratedProducerOutcomeIds.CompletedWorkMetric.Value)));
    }

    private IEnumerator RunW04EquipmentRepair(
        List<W04RepairArmResult> completedArms,
        string repairOutcomeType)
    {
        BuildingSO maintenanceDefinition = LoadAuthoredBuilding(data =>
            data.Facility?.SupportsWork(BuiltInWorkTypeIds.Repair) == true
            && data.GetAbility<BuildingEquipmentMaintenanceAbility>() != null);
        BuildableObject placedFacility = PlaceAuthoredBuilding(
            maintenanceDefinition,
            out string placementFailure);
        if (placedFacility == null)
        {
            results.Add(new WorkTypeLiveRow(
                "w04:equipment-material-shortage", "FAIL", "equipment",
                "fixture=" + placementFailure));
            results.Add(new WorkTypeLiveRow(
                "w04:equipment-partial-save-resume", "FAIL", "equipment",
                "fixture unavailable"));
            results.Add(new WorkTypeLiveRow(
                "w04:equipment-repair", "FAIL", "equipment",
                "fixture unavailable"));
            results.Add(new WorkTypeLiveRow(
                "w04:terminal-replay", "FAIL", "equipment",
                "fixture unavailable"));
            yield break;
        }

        ICombatEquipmentRuntime equipment = runtimeScope.Container
            .Resolve<ICombatEquipmentRuntime>();
        ICombatEquipmentMaintenanceRuntime maintenance = runtimeScope.Container
            .Resolve<ICombatEquipmentMaintenanceRuntime>();
        BuildingEquipmentMaintenanceAbility ability = maintenanceDefinition
            .GetAbility<BuildingEquipmentMaintenanceAbility>();
        string expectedMaterialItemId = ability?.RepairSupplyItemId
            ?? "tool:maintenance-kit";
        foreach (WorldItemStackSnapshot existing in physicalItems.GetAllStacks()
                     .Where(value => value != null
                         && value.ItemId == expectedMaterialItemId)
                     .ToArray())
        {
            if (!physicalItems.SetForbidden(existing.StackId, true))
            {
                throw new InvalidOperationException(
                    "W04 could not isolate existing repair supply: "
                    + existing.StackId);
            }
        }

        CombatEquipmentInstance instance = equipment.CreateInstance(
            "armor:cloth-hood",
            CombatEquipmentQuality.Normal,
            CombatEquipmentWorldState.Loose);
        if (instance == null
            || !equipment.TryApplyDurabilityDamage(instance.instanceId, 60f)
            || !equipment.TryGetInstance(
                instance.instanceId,
                out CombatEquipmentInstance damaged))
        {
            throw new InvalidOperationException(
                "W04 could not create the damaged equipment fixture.");
        }
        float durabilityBefore = damaged.durabilityRatio;
        int repairChildrenBefore = W01ExactOutcomes().Count(value =>
            value.outcomeTypeId == repairOutcomeType
            && W04Fact(value, "repair-target-id") == instance.instanceId);
        int materialBefore = CapturePhysicalItemQuantity(expectedMaterialItemId);
        DungeonWorkOrderSaveData ownersBefore = workOrders.Capture();
        long approvedBefore = work.ApprovedWorkProgressRevisionForDiagnostics;
        if (!maintenance.TryRequestManualRepair(
                instance.instanceId,
                out string requestMessage))
        {
            throw new InvalidOperationException(
                "W04 equipment repair request failed: " + requestMessage);
        }

        CombatEquipmentRepairOrder requested = maintenance.Orders.Single(value =>
            value.equipmentInstanceId == instance.instanceId);
        BuildableObject facility = worldRegistry.Buildings.Single(value =>
            value != null
            && value.PersistentInstanceId.Value == requested.facilityBuildingId);
        string facilityId = facility.RequirePersistentInstanceId().Value;
        string orderId = requested.orderId;
        string materialItemId = requested.materialItemId;
        if (!string.Equals(
                materialItemId,
                expectedMaterialItemId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "W04 authored maintenance supply differs from its order: expected="
                + expectedMaterialItemId + "; actual=" + materialItemId);
        }

        PrepareActor(BuiltInWorkTypeIds.Repair);
        GridPathSearchResult shortageSearch = grid.SearchPath(actor.GetNowXY());
        bool firstPriorityAccepted = work.TrySetPriorityWorkTarget(
            facility,
            BuiltInWorkTypeIds.Repair,
            shortageSearch,
            out string firstPriorityFailure);
        bool secondPriorityAccepted = work.TrySetPriorityWorkTarget(
            facility,
            BuiltInWorkTypeIds.Repair,
            shortageSearch,
            out string secondPriorityFailure);
        work.ClearPriorityWorkTarget();
        maintenance.HasRepairWorkFor(facility);
        CombatEquipmentRepairOrder shortage = maintenance.Orders.Single(value =>
            value.orderId == orderId);
        equipment.TryGetInstance(
            instance.instanceId,
            out CombatEquipmentInstance shortageEquipment);
        bool shortagePassed = shortage.state
                == CombatEquipmentRepairOrderState.WaitingForDelivery
            && !maintenance.HasRepairWorkFor(facility)
            && !firstPriorityAccepted
            && !secondPriorityAccepted
            && Mathf.Approximately(
                shortageEquipment.durabilityRatio,
                durabilityBefore)
            && CapturePhysicalItemQuantity(materialItemId) == materialBefore
            && work.ApprovedWorkProgressRevisionForDiagnostics == approvedBefore
            && workOrders.Capture().repairOutcomes.Count
                == ownersBefore.repairOutcomes.Count
            && W01ExactOutcomes().Count(value =>
                value.outcomeTypeId == repairOutcomeType
                && W04Fact(value, "repair-target-id") == instance.instanceId)
                == repairChildrenBefore;
        results.Add(new WorkTypeLiveRow(
            "w04:equipment-material-shortage",
            shortagePassed ? "PASS" : "FAIL",
            instance.instanceId,
            "state=" + shortage.state
                + "; repairAvailable=" + maintenance.HasRepairWorkFor(facility)
                + "; priority=" + firstPriorityAccepted + "/"
                + secondPriorityAccepted
                + "; failures=" + firstPriorityFailure + "/"
                + secondPriorityFailure
                + "; durability=" + durabilityBefore + "->"
                + shortageEquipment.durabilityRatio
                + "; material=" + materialBefore + "->"
                + CapturePhysicalItemQuantity(materialItemId)
                + "; ownerDelta="
                + (workOrders.Capture().repairOutcomes.Count
                    - ownersBefore.repairOutcomes.Count)));

        HashSet<string> oldStackIds = new(
            physicalItems.GetAllStacks().Where(value => value != null)
                .Select(value => value.StackId),
            StringComparer.Ordinal);
        if (!physicalItems.SpawnItemAt(
                materialItemId,
                requested.requiredMaterialAmount,
                actor.GetNowXY(),
                WorldItemStackState.Loose,
                string.Empty,
                out int spawned)
            || spawned != requested.requiredMaterialAmount)
        {
            throw new InvalidOperationException(
                "W04 could not create exact repair supplies.");
        }
        WorldItemStackSnapshot supply = physicalItems.GetAllStacks().Single(value =>
            value != null
            && !oldStackIds.Contains(value.StackId)
            && value.ItemId == materialItemId);
        int materialAfterSpawn = CapturePhysicalItemQuantity(materialItemId);
        int haulRuns = 0;
        IEnumerator haulRun = RunW04EquipmentHaulUntilReady(
            maintenance,
            facility,
            orderId,
            value => haulRuns = value);
        while (haulRun.MoveNext())
            yield return haulRun.Current;

        CombatEquipmentRepairOrder ready = maintenance.Orders.Single(value =>
            value.orderId == orderId);
        if (ready.state != CombatEquipmentRepairOrderState.Ready
            || !maintenance.HasRepairWorkFor(facility))
        {
            results.Add(new WorkTypeLiveRow(
                "w04:equipment-partial-save-resume", "FAIL",
                instance.instanceId,
                "delivery did not reach Ready; state=" + ready.state
                    + "; hauls=" + haulRuns));
            results.Add(new WorkTypeLiveRow(
                "w04:equipment-repair", "FAIL", instance.instanceId,
                "delivery did not reach Ready"));
            results.Add(new WorkTypeLiveRow(
                "w04:terminal-replay", "FAIL", instance.instanceId,
                "delivery did not reach Ready"));
            yield break;
        }

        PrepareActor(BuiltInWorkTypeIds.Repair);
        WorkPhaseResult partial = null;
        IEnumerator partialRun = RunLivePhase(
            BuiltInWorkTypeIds.Repair,
            facility,
            WorkProbeFault.CancelAfterApprovedProgress,
            null,
            value => partial = value);
        while (partialRun.MoveNext())
            yield return partialRun.Current;
        CombatEquipmentRepairOrder partialOrder = maintenance.Orders
            .SingleOrDefault(value => value.orderId == orderId);
        RepairWorkOutcomeSaveData partialOwner = workOrders.Capture()
            .repairOutcomes.SingleOrDefault(value =>
                value.kind == RepairWorkOutcomeKind.EquipmentDurability
                && value.targetEntityId == instance.instanceId);
        equipment.TryGetInstance(
            instance.instanceId,
            out CombatEquipmentInstance partialEquipment);
        bool partialAuthority = partial?.Passed == true
            && partialOrder != null
            && partialOrder.state == CombatEquipmentRepairOrderState.InProgress
            && partialOrder.completedWork > 0f
            && partialOrder.completedWork < partialOrder.requiredWork
            && !partialOrder.materialsConsumed
            && partialOwner != null
            && !partialOwner.terminal
            && partialOwner.contributions.Sum(value => value.contributedWork) > 0f
            && Mathf.Approximately(
                partialEquipment.durabilityRatio,
                durabilityBefore)
            && CapturePhysicalItemQuantity(materialItemId) == materialAfterSpawn;
        if (!partialAuthority)
        {
            results.Add(new WorkTypeLiveRow(
                "w04:equipment-partial-save-resume", "FAIL",
                instance.instanceId,
                (partial?.Format() ?? "partial-phase-missing")
                    + "; order=" + (partialOrder?.state.ToString() ?? "missing")
                    + "; work=" + partialOrder?.completedWork + "/"
                    + partialOrder?.requiredWork
                    + "; consumed=" + partialOrder?.materialsConsumed
                    + "; owner=" + (partialOwner != null)
                    + "; terminal=" + partialOwner?.terminal
                    + "; durability=" + partialEquipment?.durabilityRatio
                    + "; material=" + CapturePhysicalItemQuantity(materialItemId)
                    + "; expected=" + materialAfterSpawn));
            results.Add(new WorkTypeLiveRow(
                "w04:equipment-repair", "FAIL", instance.instanceId,
                "partial checkpoint was not available for resume"));
            results.Add(new WorkTypeLiveRow(
                "w04:terminal-replay", "FAIL", instance.instanceId,
                "partial checkpoint was not available for replay"));
            yield break;
        }

        string workerId = actor.Identity.PersistentId;
        DungeonGameSaveData partialWorld = gameSaveService.Capture();
        string partialJson = gameSaveService.ToJson(partialWorld, true);
        W04WriteEvidence("w04-equipment-partial-world.json", partialJson);
        CharacterActor staleActor = actor;
        BuildableObject staleFacility = facility;
        if (!gameSaveService.TryRestore(
                gameSaveService.FromJson(partialJson),
                out DungeonGameRestoreReport partialRestore)
            || !partialRestore.Success)
        {
            throw new InvalidOperationException(
                "W04 equipment partial restore failed: "
                + string.Join(" | ", partialRestore.Errors));
        }
        actor = LiveActors(worldRegistry).Single(value =>
            value.Identity.PersistentId == workerId);
        foreach (CharacterActor other in LiveActors(worldRegistry))
            other.SetAiPaused(true);
        work = actor.GetComponent<AbilityWork>();
        brain = actor.Brain;
        grid = FindFirstObjectByType<GridSystemManager>()?.grid;
        facility = worldRegistry.Buildings.Single(value => value != null
            && value.PersistentInstanceId.Value == facilityId);
        maintenance = runtimeScope.Container
            .Resolve<ICombatEquipmentMaintenanceRuntime>();
        equipment = runtimeScope.Container.Resolve<ICombatEquipmentRuntime>();
        CombatEquipmentRepairOrder restoredOrder = maintenance.Orders
            .SingleOrDefault(value => value.orderId == orderId);
        RepairWorkOutcomeSaveData restoredOwner = workOrders.Capture()
            .repairOutcomes.SingleOrDefault(value =>
                value.kind == RepairWorkOutcomeKind.EquipmentDurability
                && value.targetEntityId == instance.instanceId);
        bool partialRestored = !ReferenceEquals(staleActor, actor)
            && !ReferenceEquals(staleFacility, facility)
            && restoredOrder != null
            && restoredOrder.state == CombatEquipmentRepairOrderState.InProgress
            && Mathf.Approximately(
                restoredOrder.completedWork,
                partialOrder.completedWork)
            && restoredOwner != null
            && restoredOwner.operationId == partialOwner.operationId
            && restoredOwner.contributions.Sum(value => value.contributedWork)
                == partialOwner.contributions.Sum(value => value.contributedWork)
            && CapturePhysicalItemQuantity(materialItemId) == materialAfterSpawn;
        results.Add(new WorkTypeLiveRow(
            "w04:equipment-partial-save-resume",
            partialRestored ? "PASS" : "FAIL",
            instance.instanceId,
            "sections=" + partialWorld.sections.Count
                + "; freshActor=" + !ReferenceEquals(staleActor, actor)
                + "; freshFacility=" + !ReferenceEquals(staleFacility, facility)
                + "; state=" + restoredOrder?.state
                + "; work=" + restoredOrder?.completedWork + "/"
                + restoredOrder?.requiredWork
                + "; owner=" + restoredOwner?.operationId
                + "; material=" + CapturePhysicalItemQuantity(materialItemId)));
        if (!partialRestored)
        {
            results.Add(new WorkTypeLiveRow(
                "w04:equipment-repair", "FAIL", instance.instanceId,
                "official partial restore did not preserve repair state"));
            results.Add(new WorkTypeLiveRow(
                "w04:terminal-replay", "FAIL", instance.instanceId,
                "official partial restore did not preserve replay state"));
            yield break;
        }

        W04RepairArmResult arm = null;
        IEnumerator resumeRun = RunW04RepairArm(
            facility,
            instance.instanceId,
            "equipment-durability",
            () => equipment.TryGetInstance(
                    instance.instanceId,
                    out CombatEquipmentInstance state)
                && state.durabilityRatio >= restoredOrder.targetDurability
                    - 0.0001f,
            value => arm = value);
        while (resumeRun.MoveNext())
            yield return resumeRun.Current;
        equipment.TryGetInstance(
            instance.instanceId,
            out CombatEquipmentInstance completedEquipment);
        int materialAfter = CapturePhysicalItemQuantity(materialItemId);
        double materialQuantity = W04Metric(
            arm?.Child,
            MigratedProducerOutcomeIds.InputQuantityMetric.Value);
        double materialMass = W04Metric(
            arm?.Child,
            MigratedProducerOutcomeIds.InputMassMetric.Value);
        string materialSources = W04Fact(arm?.Child, "repair-material-sources");
        bool equipmentPassed = arm?.Joined == true
            && completedEquipment != null
            && completedEquipment.durabilityRatio
                >= restoredOrder.targetDurability - 0.0001f
            && Math.Abs(W04Metric(
                    arm.Child,
                    MigratedProducerOutcomeIds.PreviousMetric.Value)
                - durabilityBefore) < 0.0001d
            && Math.Abs(W04Metric(
                    arm.Child,
                    MigratedProducerOutcomeIds.CurrentMetric.Value)
                - completedEquipment.durabilityRatio) < 0.0001d
            && materialAfterSpawn - materialAfter
                == restoredOrder.requiredMaterialAmount
            && Math.Abs(materialQuantity - restoredOrder.requiredMaterialAmount)
                < 0.001d
            && materialMass > 0d
            && materialSources.Contains(materialItemId, StringComparison.Ordinal)
            && materialSources.Contains(supply.StackId, StringComparison.Ordinal);
        results.Add(new WorkTypeLiveRow(
            "w04:equipment-repair",
            equipmentPassed ? "PASS" : "FAIL",
            instance.instanceId,
            (arm?.Detail ?? "arm-missing")
                + "; durability=" + durabilityBefore + "->"
                + completedEquipment?.durabilityRatio
                + "; target=" + restoredOrder.targetDurability
                + "; material=" + materialAfterSpawn + "->" + materialAfter
                + "; required=" + restoredOrder.requiredMaterialAmount
                + "; childQuantity=" + materialQuantity
                + "; childMass=" + materialMass
                + "; sources=" + materialSources
                + "; hauls=" + haulRuns));
        if (arm != null)
            completedArms.Add(arm);
        if (arm == null || !equipmentPassed)
        {
            results.Add(new WorkTypeLiveRow(
                "w04:terminal-replay", "FAIL", instance.instanceId,
                "equipment completion did not produce an exact replay request"));
            yield break;
        }

        DungeonGameSaveData completedWorld = gameSaveService.Capture();
        string completedJson = gameSaveService.ToJson(completedWorld, true);
        W04WriteEvidence("w04-equipment-completed-world.json", completedJson);
        if (!gameSaveService.TryRestore(
                gameSaveService.FromJson(completedJson),
                out DungeonGameRestoreReport completedRestore)
            || !completedRestore.Success)
        {
            throw new InvalidOperationException(
                "W04 equipment completed restore failed: "
                + string.Join(" | ", completedRestore.Errors));
        }
        actor = LiveActors(worldRegistry).Single(value =>
            value.Identity.PersistentId == workerId);
        work = actor.GetComponent<AbilityWork>();
        brain = actor.Brain;
        grid = FindFirstObjectByType<GridSystemManager>()?.grid;
        equipment = runtimeScope.Container.Resolve<ICombatEquipmentRuntime>();
        equipment.TryGetInstance(
            instance.instanceId,
            out CombatEquipmentInstance replayEquipment);
        string beforeReplay = W05SectionPayloads(gameSaveService.Capture());
        IWorkCompletionIdentityDeliveryCommand delivery = runtimeScope.Container
            .Resolve<IWorkCompletionIdentityDeliveryCommand>();
        WorkCompletionIdentityDeliveryResult replayOne = delivery.EnsureApplied(
            arm.Request);
        WorkCompletionIdentityDeliveryResult replayTwo = delivery.EnsureApplied(
            arm.Request);
        GameplayOutcomeSnapshot[] replayChildren = W01ExactOutcomes().Where(value =>
            W01ResultKey(value) == arm.ChildResultKey).ToArray();
        GameplayOutcomeSnapshot[] replayIdentities = W01ExactOutcomes().Where(value =>
            value.outcomeTypeId == WorkCompletionIdentityOutcomeIds.Applied.Value
            && W04Fact(
                value,
                WorkCompletionIdentityOutcomeIds.ChildResultKeyFact.Value)
                == arm.ChildResultKey).ToArray();
        bool replayPassed = replayOne.Status
                == WorkCompletionIdentityDeliveryStatus.AlreadyApplied
            && replayTwo.Status
                == WorkCompletionIdentityDeliveryStatus.AlreadyApplied
            && beforeReplay == W05SectionPayloads(gameSaveService.Capture())
            && replayChildren.Length == 1
            && replayIdentities.Length == 1
            && replayEquipment != null
            && Mathf.Approximately(
                replayEquipment.durabilityRatio,
                completedEquipment.durabilityRatio)
            && CapturePhysicalItemQuantity(materialItemId) == materialAfter;
        results.Add(new WorkTypeLiveRow(
            "w04:terminal-replay",
            replayPassed ? "PASS" : "FAIL",
            instance.instanceId,
            "officialRestore=true; first=" + replayOne.Status
                + "; second=" + replayTwo.Status
                + "; sectionsUnchanged="
                + (beforeReplay == W05SectionPayloads(gameSaveService.Capture()))
                + "; childCount=" + replayChildren.Length
                + "; identityCount=" + replayIdentities.Length
                + "; material=" + CapturePhysicalItemQuantity(materialItemId)
                + "; durability=" + replayEquipment?.durabilityRatio));
    }

    private IEnumerator RunW04EquipmentHaulUntilReady(
        ICombatEquipmentMaintenanceRuntime maintenance,
        BuildableObject facility,
        string orderId,
        Action<int> completed)
    {
        const int MaximumHaulRuns = 4;
        int runs = 0;
        while (!maintenance.HasRepairWorkFor(facility)
               && runs < MaximumHaulRuns)
        {
            CombatEquipmentRepairOrder order = maintenance.Orders
                .SingleOrDefault(value => value.orderId == orderId);
            if (order == null)
                break;
            PrepareActor(BuiltInWorkTypeIds.Haul);
            AbilityHaul haul = AbilityHaul.Ensure(actor);
            long startsBefore = haul.RuntimeHaulStartCount;
            long terminalsBefore = haul.RuntimeHaulTerminalCount;
            actor.SetAiPaused(false);
            if (!brain.PreferActionOnNextDecision<AIHaul>())
            {
                throw new InvalidOperationException(
                    "W04 actor has no authored AIHaul action.");
            }
            brain.RequestImmediateReplan(clearFailures: true);
            float deadline = Time.realtimeSinceStartup + 45f;
            bool sawHaul = false;
            while (Time.realtimeSinceStartup < deadline)
            {
                MaintainStableWorkSubject();
                sawHaul |= brain.bestAction?.actionset is AIHaul
                    && brain.HasRunningAction;
                if (sawHaul
                    && haul.RuntimeHaulTerminalCount > terminalsBefore
                    && !haul.IsHauling)
                    break;
                yield return null;
            }
            actor.SetAiPaused(true);
            runs++;
            if (!sawHaul
                || haul.RuntimeHaulStartCount <= startsBefore
                || haul.RuntimeHaulTerminalCount <= terminalsBefore)
            {
                throw new InvalidOperationException(
                    "W04 actual Brain -> AIHaul delivery did not settle: run="
                    + runs + "; saw=" + sawHaul + "; starts="
                    + startsBefore + "->" + haul.RuntimeHaulStartCount
                    + "; terminals=" + terminalsBefore + "->"
                    + haul.RuntimeHaulTerminalCount);
            }
        }
        completed(runs);
    }

    private static string W04Fact(
        GameplayOutcomeSnapshot outcome,
        string factId) => outcome?.facts
            .FirstOrDefault(value => value.factId == factId)?.value
        ?? "missing";

    private static double W04Metric(
        GameplayOutcomeSnapshot outcome,
        string metricId) => outcome?.metrics
            .FirstOrDefault(value => value.metricId == metricId)?.value
        ?? double.NaN;

    private static void W04WriteEvidence(string name, string content)
    {
        string directory = Path.GetDirectoryName(
            CharacterAiWorkTypeLiveMatrixPlayModeVerifier.W04RepairLiveReportPath);
        Directory.CreateDirectory(directory);
        WriteUtf8LfCreateNew(Path.Combine(directory, name), new[] { content });
    }

    private IEnumerator RunW01FireWaterHaul(BuildableObject target, string fireId,
        Action<BuildableObject> completed)
    {
        const string waterId = "resource:clean-water";
        string actorId = actor.Identity.PersistentId;
        string targetId = target.PersistentInstanceId.Value;
        string destination = EnvironmentalFireWaterDestinationIdentity.Create(fireId);
        string directory = Path.GetDirectoryName(CharacterAiWorkTypeLiveMatrixPlayModeVerifier.W01WorkTerminalJoinReportPath);
        Directory.CreateDirectory(directory);
        // Only the original physical stock is a fixture. Response Tick must request
        // its delivery; Brain/AIHaul must pick it up and deliver it to the real claim.
        foreach (var existing in physicalItems.GetAllStacks().Where(value => value != null && value.ItemId == waterId).ToArray())
            if (!physicalItems.SetForbidden(existing.StackId, true))
                throw new InvalidOperationException("W01 could not isolate competing water stock: " + existing.StackId);
        HashSet<string> oldIds = new(physicalItems.GetAllStacks().Select(value => value.StackId), StringComparer.Ordinal);
        if (!physicalItems.SpawnItemAt(waterId, 1, actor.GetNowXY(), WorldItemStackState.Loose,
                string.Empty, out int spawned) || spawned != 1)
            throw new InvalidOperationException("W01 water source fixture could not be created.");
        var source = physicalItems.GetAllStacks().Single(value => !oldIds.Contains(value.StackId) && value.ItemId == waterId);
        PrepareActor(BuiltInWorkTypeIds.Haul);
        AbilityHaul haul = AbilityHaul.Ensure(actor);
        long startCount = haul.RuntimeHaulStartCount;
        long brainStartCount = brain.RuntimeActionStartCount;
        actor.SetAiPaused(false);
        if (!brain.PreferActionOnNextDecision<AIHaul>())
            throw new InvalidOperationException("W01 actor has no authored AIHaul action.");
        brain.RequestImmediateReplan(clearFailures: true);
        float startedGame = actor.GameClock.Time, startedWall = Time.realtimeSinceStartup;
        int startedFrame = Time.frameCount;
        bool sawBrainHaul = false;
        HaulDeliveryIntentSaveData picked = null;
        while (picked == null)
        {
            MaintainStableWorkSubject();
            sawBrainHaul |= brain.bestAction?.actionset is AIHaul && brain.HasRunningAction;
            HaulDeliveryIntentSaveData candidate = haul.CaptureDeliveryIntentForSave();
            if (candidate?.HasCommittedPickup == true && candidate.destinationId == destination)
                picked = candidate;
            else
            {
                RequireProgressTime("pickup");
                yield return null;
            }
        }
        var carryBefore = actor.CarryInventory.Items.Single(value => value.ownerOperationId == picked.operationId && value.itemId == waterId);
        bool pickupExact = sawBrainHaul && brain.RuntimeActionStartCount > brainStartCount
            && haul.RuntimeHaulStartCount > startCount && picked.ownerCharacterId == actorId
            && carryBefore.sourceStackId == source.StackId && carryBefore.quantity == 1
            && picked.commitments.Sum(value => value.quantity) == 1
            && picked.commitments.Single().carriedStackId == carryBefore.carriedStackId;
        // Supplemental authority check: an existing facility on the exact same
        // cell must not substitute for the fire's missing or foreign claim.
        var claimQuery = runtimeScope.Container.Resolve<IFacilityBufferDestinationClaimQuery>();
        var workOrders = runtimeScope.Container.Resolve<IWorkOrderQuery>();
        var emptyClaims = new FacilityBufferDestinationClaimRegistry();
        var foreignClaims = new FacilityBufferDestinationClaimRegistry();
        if (!foreignClaims.TryClaim(new FacilityBufferDestinationClaim(destination, target.centerPos,
                "qa:foreign-owner", fireId, targetId, FacilityBufferDestinationAnchorKind.LiveBuilding), out _, out _))
            throw new InvalidOperationException("W01 could not construct the foreign-claim negative fixture.");
        bool realResolved = WorldItemHaulDestinationAuthority.TryResolve(grid, worldRegistry, workOrders,
            claimQuery, WorldItemHaulDestinationKind.FacilityBuffer, destination, target.centerPos, out _, out string realFailure);
        bool missingResolved = WorldItemHaulDestinationAuthority.TryResolve(grid, worldRegistry, workOrders,
            emptyClaims, WorldItemHaulDestinationKind.FacilityBuffer, destination, target.centerPos, out _, out string missingFailure);
        bool foreignResolved = WorldItemHaulDestinationAuthority.TryResolve(grid, worldRegistry, workOrders,
            foreignClaims, WorldItemHaulDestinationKind.FacilityBuffer, destination, target.centerPos, out _, out string foreignFailure);
        bool exactAuthority = realResolved && !missingResolved && !foreignResolved
            && missingFailure.StartsWith("haul-destination-fire-water-claim-missing:", StringComparison.Ordinal)
            && foreignFailure.StartsWith("haul-destination-fire-water-owner-mismatch:", StringComparison.Ordinal);
        results.Add(new WorkTypeLiveRow("w01:fire-water-exact-claim-required", exactAuthority ? "PASS" : "FAIL", targetId,
            "supplementalAuthority=true; real=" + realResolved + "; realFailure=" + realFailure
                + "; missing=" + missingResolved + "; missingFailure=" + missingFailure
                + "; foreign=" + foreignResolved + "; foreignFailure=" + foreignFailure));
        if (!exactAuthority) throw new InvalidOperationException("W01 fire water accepted an inexact destination authority.");
        var carriedSave = gameSaveService.Capture();
        string carriedJson = gameSaveService.ToJson(carriedSave, true);
        WriteUtf8LfCreateNew(Path.Combine(directory, "w01-fire-water-carried-world.json"), new[] { carriedJson });
        if (!gameSaveService.TryRestore(gameSaveService.FromJson(carriedJson), out var restored) || !restored.Success)
            throw new InvalidOperationException("W01 carried fire water restore rejected: " + string.Join(" | ", restored.Errors));
        CharacterActor previousActor = actor;
        actor = LiveActors(worldRegistry).Single(value => value.Identity.PersistentId == actorId);
        foreach (var other in LiveActors(worldRegistry)) other.SetAiPaused(true);
        work = actor.GetComponent<AbilityWork>();
        brain = actor.Brain;
        grid = FindFirstObjectByType<GridSystemManager>()?.grid;
        haul = AbilityHaul.Ensure(actor);
        var rebound = haul.CaptureDeliveryIntentForSave();
        BuildableObject freshTarget = worldRegistry.Buildings.Single(value => value != null && value.PersistentInstanceId.Value == targetId);
        var carryAfter = actor.CarryInventory.Items.SingleOrDefault(value => value.ownerOperationId == picked.operationId && value.itemId == waterId);
        var claims = runtimeScope.Container.Resolve<IFacilityBufferDestinationClaimQuery>();
        bool claimFound = claims.TryGetClaim(destination, freshTarget.centerPos, out var claim);
        bool restoredExact = pickupExact && !ReferenceEquals(previousActor, actor) && !ReferenceEquals(target, freshTarget)
            && haul.HasBoundDeliveryIntent && rebound?.operationId == picked.operationId
            && rebound.destinationId == destination && carryAfter?.carriedStackId == carryBefore.carriedStackId
            && carryAfter.quantity == 1 && carryAfter.sourceStackId == source.StackId
            && claimFound
            && claim.OwnerOperationId == fireId && claim.OwnerDomain == EnvironmentalFireWaterDestinationIdentity.OwnerDomain;
        results.Add(new WorkTypeLiveRow("w01:fire-water-carried-save-join", restoredExact ? "PASS" : "FAIL", targetId,
            "sections=" + carriedSave.sections.Count + "; actualBrainPickup=" + pickupExact + "; fresh=" + !ReferenceEquals(previousActor, actor)
                + "; rebound=" + haul.HasBoundDeliveryIntent + "; operation=" + picked.operationId
                + "; source=" + source.StackId + "; restoredSource=" + carryAfter?.sourceStackId
                + "; carried=" + carryBefore.carriedStackId + "; restoredCarried=" + carryAfter?.carriedStackId
                + "; quantity=" + carryAfter?.quantity + "; freshTarget=" + !ReferenceEquals(target, freshTarget)
                + "; reboundOperation=" + rebound?.operationId + "; reboundDestination=" + rebound?.destinationId
                + "; claimFound=" + claimFound + "; claimOwner=" + claim?.OwnerOperationId
                + "; claimDomain=" + claim?.OwnerDomain + "; position=" + freshTarget.centerPos));
        if (!restoredExact) throw new InvalidOperationException("W01 fire water did not restore its exact carried intent/claim.");
        long restoredStarts = haul.RuntimeHaulStartCount, restoredTerminals = haul.RuntimeHaulTerminalCount;
        bool sawResumedBrainHaul = false;
        actor.SetAiPaused(false);
        if (!brain.PreferActionOnNextDecision<AIHaul>())
            throw new InvalidOperationException("W01 restored actor has no AIHaul action.");
        brain.RequestImmediateReplan(clearFailures: true);
        startedGame = actor.GameClock.Time;
        startedWall = Time.realtimeSinceStartup;
        startedFrame = Time.frameCount;
        WorldItemStackSnapshot delivered = null;
        while (delivered == null || haul.IsHauling || haul.RuntimeHaulTerminalCount <= restoredTerminals)
        {
            MaintainStableWorkSubject();
            sawResumedBrainHaul |= brain.bestAction?.actionset is AIHaul && brain.HasRunningAction;
            delivered = physicalItems.GetAllStacks().SingleOrDefault(value => value.ItemId == waterId
                && value.State == WorldItemStackState.FacilityBuffer && value.DestinationId == destination
                && value.Position == freshTarget.centerPos && value.Quantity == 1);
            RequireProgressTime("delivery");
            yield return null;
        }
        bool deliveredOnce = sawResumedBrainHaul && haul.RuntimeHaulStartCount == restoredStarts + 1
            && haul.RuntimeHaulTerminalCount == restoredTerminals + 1 && !haul.HasBoundDeliveryIntent
            && !actor.CarryInventory.Items.Any(value => value.ownerOperationId == picked.operationId)
            && physicalItems.CaptureHaulDeliveryIntentsByDestination(destination).Count == 0;
        results.Add(new WorkTypeLiveRow("w01:fire-water-actual-haul", deliveredOnce ? "PASS" : "FAIL", targetId,
            "resumedBrainHaul=" + sawResumedBrainHaul + "; starts=" + (haul.RuntimeHaulStartCount - restoredStarts)
                + "; terminals=" + (haul.RuntimeHaulTerminalCount - restoredTerminals) + "; destination=" + destination
                + "; stack=" + delivered.StackId + "; quantity=" + delivered.Quantity + "; failure=" + haul.LastFailureReason));
        if (!deliveredOnce) throw new InvalidOperationException("W01 fire water delivery did not complete exactly once.");
        actor.SetAiPaused(true);
        completed(freshTarget);

        void RequireProgressTime(string stage)
        {
            float game = actor.GameClock.Time - startedGame;
            float wall = Time.realtimeSinceStartup - startedWall;
            int frames = Time.frameCount - startedFrame;
            if (game > 60f && frames > 180 || wall > 180f && frames > 60)
                throw new InvalidOperationException("W01 water " + stage + " stalled: game=" + game + "; wall=" + wall
                    + "; frames=" + frames + "; phase=" + haul.CurrentExecutionStage + "; failure=" + haul.LastFailureReason
                    + "; action=" + brain.CurrentActionDebugLabel + "; path=" + haul.ActivePathDebug);
        }
    }

    private sealed class W01FireDeliveryGate : IWorkCompletionIdentityDeliveryCommand, IDisposable
    {
        private readonly EnvironmentalFireRuntime runtime;
        private readonly FieldInfo port;
        private readonly object previous;
        private readonly IWorkCompletionIdentityDeliveryCommand inner;
        public bool Held { get; private set; }
        public bool Released { get; set; }
        public WorkCompletionIdentityDeliveryRequest Request { get; private set; }
        public W01FireDeliveryGate(EnvironmentalFireRuntime runtime, IWorkCompletionIdentityDeliveryCommand inner)
        {
            this.runtime = runtime;
            this.inner = inner;
            port = typeof(EnvironmentalFireRuntime).GetField("completionDeliveries", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("W01 fire completion port seam is missing.");
            previous = port.GetValue(runtime);
            port.SetValue(runtime, this);
        }
        public WorkCompletionIdentityDeliveryResult EnsureApplied(WorkCompletionIdentityDeliveryRequest request)
        {
            if (Released) return inner.EnsureApplied(request);
            if (Held && Request.PayloadFingerprint != request.PayloadFingerprint)
                throw new InvalidOperationException("W01 fire pending request changed before acknowledgement.");
            Held = true;
            Request = request;
            return new(WorkCompletionIdentityDeliveryStatus.Deferred, "qa-fire-post-child-checkpoint");
        }
        public bool RetireProducerStream(string stream) => inner.RetireProducerStream(stream);
        public void Dispose() => port.SetValue(runtime, previous);
    }

    private List<GameplayOutcomeSnapshot> W01ExactOutcomes()
    {
        IGameplayOutcomeQuery query = runtimeScope.Container.Resolve<IGameplayOutcomeQuery>();
        List<GameplayOutcomeSnapshot> snapshots = new();
        OutcomeCursor cursor = OutcomeCursor.FirstPage(200);
        while (true)
        {
            GameplayOutcomeQueryPage page = query.GetGlobal(cursor, OutcomeFilter.All);
            if (page.Items.Count == 0)
                return snapshots;
            snapshots.AddRange(page.Items.Where(value => value.Exact != null)
                .Select(value => value.Exact));
            cursor = page.NextCursor;
        }
    }

    private static string W01ResultKey(GameplayOutcomeSnapshot value) =>
        new GameplayResultKey(value.producerId, new GameplayOperationId(value.operationId),
            value.commitRevision, value.localResultIndex).ToString();

    // QA-only observation. The production handler, context and result are forwarded unchanged.
    private sealed class W01RepairHandlerObservation : IWorkExecutionHandler,
        IWorkAccessStandExecutionHandler, IDisposable
    {
        private readonly Dictionary<WorkTypeId, IWorkExecutionHandler> handlers;
        private readonly IWorkExecutionHandler inner;
        private readonly WorkTypeId observedWorkType;
        public W01RepairHandlerObservation(IWorkExecutionHandlerRegistry registry,
            bool observeFire = false)
        {
            observedWorkType = observeFire ? BuiltInWorkTypeIds.ThreatMitigation : BuiltInWorkTypeIds.Repair;
            if (registry is not WorkExecutionHandlerRegistry production
                || !registry.TryGet(observedWorkType, out inner)
                || (observeFire ? inner is not ThreatMitigationWorkExecutionHandler
                    : inner is not RepairWorkExecutionHandler))
                throw new InvalidOperationException("W01 requires the production terminal registry/handler: " + observedWorkType);
            handlers = typeof(WorkExecutionHandlerRegistry).GetField("handlers",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(production)
                as Dictionary<WorkTypeId, IWorkExecutionHandler>
                ?? throw new InvalidOperationException("W01 registry observation seam is missing.");
            handlers[observedWorkType] = this;
        }
        public IReadOnlyCollection<WorkTypeId> WorkTypeIds => inner.WorkTypeIds;
        public bool RequiresWorkAccessStand(WorkTypeId workTypeId,
            CharacterActor actor, BuildableObject target) =>
            inner is IWorkAccessStandExecutionHandler access
            && access.RequiresWorkAccessStand(workTypeId, actor, target);
        public int CompletedCalls { get; private set; }
        public WorkExecutionResult Result { get; private set; }
        public string ActorId { get; private set; }
        public string TargetId { get; private set; }
        public IEnumerator Execute(WorkExecutionContext context, WorkExecutionResult result)
        {
            IEnumerator execution = inner.Execute(context, result);
            try
            {
                while (execution.MoveNext())
                    yield return execution.Current;
                CompletedCalls++;
                Result = result;
                ActorId = context.Actor.Identity.PersistentId;
                TargetId = context.Target.PersistentInstanceId.Value;
            }
            finally
            {
                (execution as IDisposable)?.Dispose();
            }
        }
        public void Dispose()
        {
            if (ReferenceEquals(handlers[observedWorkType], this))
                handlers[observedWorkType] = inner;
        }
    }

    private IEnumerator RunW05HarvestIdentityFocused()
    {
        WorkTypeId workTypeId = BuiltInWorkTypeIds.Harvest;
        if (!CleanupRowScopedFixtures(out string rowCleanupFailure))
        {
            results.Add(WorkTypeLiveRow.Blocked(
                workTypeId,
                "previous-row-fixture-cleanup-failed",
                rowCleanupFailure));
            yield break;
        }
        if (!CleanupRoomFixture(out string roomCleanupFailure))
        {
            results.Add(WorkTypeLiveRow.Blocked(
                workTypeId,
                "previous-room-fixture-cleanup-failed",
                roomCleanupFailure));
            yield break;
        }

        MaintainStableWorkSubject();
        if (!TryPrepareMaterialFixture(
                workTypeId,
                out MaterialWorkFixture fixture,
                out string fixtureFailure)
            || fixture == null)
        {
            results.Add(WorkTypeLiveRow.Blocked(
                workTypeId,
                "production-live-fixture-unavailable",
                fixtureFailure));
            yield break;
        }

        runtimeScope.Container.Resolve<IFacilityCandidateCache>()
            .MarkDynamicStateDirty();
        yield return null;
        PrepareActor(workTypeId);

        BuildableObject target = fixture.Target;
        string targetLabel = target != null
            ? target.name
            : "<missing-target>";
        string plotId = target != null
            ? target.RequirePersistentInstanceId().Value
            : string.Empty;
        w05HarvestPlotId = plotId;
        CropPlotSnapshot plotBefore = cropPlots.Plots.FirstOrDefault(value =>
            string.Equals(value.PlotId, plotId, StringComparison.Ordinal));
        CropDefinitionSO crop = plotBefore == null
            ? null
            : economyContent.Crops.FirstOrDefault(value => value != null
                && string.Equals(
                    value.CropId,
                    plotBefore.CropId,
                    StringComparison.Ordinal));
        if (target == null || plotBefore == null || crop == null
            || string.IsNullOrWhiteSpace(crop.HarvestItemId))
        {
            results.Add(new WorkTypeLiveRow(
                workTypeId.Value,
                "FAIL",
                targetLabel,
                "harvest-completion-authority-unavailable:plot=" + plotId
                    + "; crop=" + (plotBefore?.CropId ?? "-")
                    + "; harvestItem=" + (crop?.HarvestItemId ?? "-")));
            IEnumerator failedSetupCleanup =
                CleanupSingleDomainRoomAfterRow(workTypeId);
            while (failedSetupCleanup.MoveNext())
                yield return failedSetupCleanup.Current;
            yield break;
        }

        int outputBefore = CapturePhysicalItemQuantity(crop.HarvestItemId);
        string pendingWorldJson = null;
        GameplayOutcomeSnapshot pendingChild = null;
        w05HarvestDeliveryGate = new W05HarvestDeliveryGate(cropPlots,
            runtimeScope.Container.Resolve<IWorkCompletionIdentityDeliveryCommand>(),
            runtimeScope.Container.Resolve<WorkCompletionIdentityDeliveryLedger>(),
            runtimeScope.Container.Resolve<CharacterIdentityEventPublisher>(),
            runtimeScope.Container.Resolve<WorkIdentityAppliedFactMapping>());
        WorkPhaseResult completion = null;
        IEnumerator completionRun = RunLivePhase(
            workTypeId,
            target,
            WorkProbeFault.CompleteNormally,
            fixture,
            value => completion = value,
            completionAuthoritySatisfied: () =>
            {
                bool harvestStillAvailable = cropPlots.TryGetWork(
                        target,
                        workTypeId,
                        out CropPlotWorkSnapshot workAfter)
                    && workAfter.Available;
                return !harvestStillAvailable
                    && CapturePhysicalItemQuantity(crop.HarvestItemId)
                        > outputBefore;
            });
        while (completionRun.MoveNext())
        {
            if (pendingWorldJson == null && w05HarvestDeliveryGate.Held)
            {
                WorkCompletionIdentityDeliveryRequest held = w05HarvestDeliveryGate.Request;
                CropPlotSaveData pendingPlot = cropPlots.Capture().plots.Single(value =>
                    value.buildingInstanceId == plotId);
                pendingChild = W01ExactOutcomes().Single(value => W01ResultKey(value) == held.ChildResultKey);
                bool checkpointReady = pendingPlot.pendingHarvest.cropOutcomeAcknowledged
                    && !pendingPlot.pendingHarvest.completionEventPublished
                    && pendingPlot.pendingHarvest.outputPublication.outputAcknowledged
                    && FindW05DeliveryCursor(w05IdentityDeliveries.Capture(), held.ProducerStreamId) == null
                    && !W01ExactOutcomes().Any(value => W01ResultKey(value)
                        == WorkCompletionIdentityOutcomeIds.ResultKey(held).ToString())
                    && CapturePhysicalItemQuantity(crop.HarvestItemId) > outputBefore;
                if (!checkpointReady)
                    throw new InvalidOperationException("W05 child/identity checkpoint is not the claimed production boundary.");
                pendingWorldJson = gameSaveService.ToJson(gameSaveService.Capture(), true);
                W05WriteEvidence("w01-harvest-child-before-identity-world.json", pendingWorldJson);
                w05HarvestDeliveryGate.Released = true;
            }
            yield return completionRun.Current;
        }

        int outputAfter = CapturePhysicalItemQuantity(crop.HarvestItemId);
        CropPlotSnapshot plotAfter = cropPlots.Plots.FirstOrDefault(value =>
            string.Equals(value.PlotId, plotId, StringComparison.Ordinal));
        bool harvestAvailableAfter = cropPlots.TryGetWork(
                target,
                workTypeId,
                out CropPlotWorkSnapshot finalWork)
            && finalWork.Available;
        bool outputPreserved = outputAfter > outputBefore;
        bool passed = completion != null
            && completion.Passed
            && outputPreserved
            && !harvestAvailableAfter
            && plotAfter != null;
        results.Add(new WorkTypeLiveRow(
            workTypeId.Value,
            passed ? "PASS" : "FAIL",
            targetLabel,
            "completion=" + (completion?.Format() ?? "missing")
                + "; plot=" + plotId
                + "; phase=" + plotBefore.Phase
                + "->" + (plotAfter?.Phase.ToString() ?? "missing")
                + "; harvestAvailableAfter=" + harvestAvailableAfter
                + "; output=" + crop.HarvestItemId + ":"
                + outputBefore + "->" + outputAfter
                + "; outputPreserved=" + outputPreserved));

        if (passed && pendingWorldJson != null)
        {
            IEnumerator restore = VerifyW05HarvestPendingRestore(
                pendingWorldJson, pendingChild, target, crop.HarvestItemId, outputAfter,
                w05HarvestDeliveryGate.Request);
            while (restore.MoveNext()) yield return restore.Current;
        }
        else
            results.Add(new WorkTypeLiveRow("w01:harvest-pending-save-resume", "FAIL", plotId,
                "actualCompletionPassed=" + passed + "; pendingCheckpointCaptured="
                    + (pendingWorldJson != null) + "; restoreResume=NOT_RUN"));
        w05HarvestDeliveryGate.Dispose();
        w05HarvestDeliveryGate = null;

        IEnumerator cleanup = CleanupSingleDomainRoomAfterRow(workTypeId);
        while (cleanup.MoveNext())
            yield return cleanup.Current;
    }

    private IEnumerator VerifyW05HarvestPendingRestore(string pendingJson,
        GameplayOutcomeSnapshot childBefore, BuildableObject previousPlot, string itemId, int outputQuantity,
        WorkCompletionIdentityDeliveryRequest request)
    {
        brain.StopCurrentActionForReplan("W05 pending harvest restore checkpoint");
        work.ClearPriorityWorkTarget();
        actor.SetAiPaused(true);
        string actorId = actor.Identity.PersistentId;
        DungeonGameSaveData invalid = gameSaveService.FromJson(pendingJson);
        DungeonSaveSectionEnvelope ledgerSection = invalid.sections.Single(value =>
            value.sectionId == GameplayOutcomeLedgerSaveSection.Id);
        GameplayOutcomeLedgerSaveData missingChild = DungeonSaveJson.Deserialize<GameplayOutcomeLedgerSaveData>(
            ledgerSection.payloadJson);
        int removed = missingChild.exactOutcomes.RemoveAll(value => W01ResultKey(value) == request.ChildResultKey)
            + missingChild.outbox.RemoveAll(value => W01ResultKey(value) == request.ChildResultKey);
        if (removed != 1) throw new InvalidOperationException("W05 missing-child test requires one real committed child.");
        ledgerSection.payloadJson = JsonUtility.ToJson(missingChild);
        invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);
        W05WriteEvidence("w01-harvest-missing-child-invalid-world.json", gameSaveService.ToJson(invalid, true));
        var root = runtimeScope.Container.Resolve<DungeonRuntimeAggregateRootStore>();
        int revision = root.PublishedRestoreRevision;
        string beforeInvalid = W05SectionPayloads(gameSaveService.Capture());
        bool accepted = gameSaveService.TryRestore(invalid, out DungeonGameRestoreReport rejection);
        bool rejectedAtomically = !accepted && rejection.Errors.Any(value =>
                value.Contains("Crop", StringComparison.OrdinalIgnoreCase))
            && revision == root.PublishedRestoreRevision && !root.IsRestoreStaging
            && beforeInvalid == W05SectionPayloads(gameSaveService.Capture());
        string rejectionDetail = "accepted=" + accepted + "; sectionsUnchanged=" + rejectedAtomically
            + "; errors=" + string.Join(" | ", rejection.Errors);
        W05WriteEvidence("w01-harvest-missing-child-witness.txt", rejectionDetail);
        results.Add(new WorkTypeLiveRow("w01:harvest-missing-child-rejected",
            rejectedAtomically ? "PASS" : "FAIL", w05HarvestPlotId, rejectionDetail));
        if (!rejectedAtomically)
            throw new InvalidOperationException("W05 malformed child checkpoint was not rejected atomically.");

        DungeonGameSaveData pendingWorld = gameSaveService.FromJson(pendingJson);
        if (!gameSaveService.TryRestore(pendingWorld, out DungeonGameRestoreReport restored) || !restored.Success)
            throw new InvalidOperationException("W05 pending world restore failed: " + string.Join(" | ", restored.Errors));
        actor = LiveActors(worldRegistry).Single(value => value.Identity.PersistentId == actorId);
        work = actor.GetComponent<AbilityWork>();
        brain = actor.Brain;
        actor.SetAiPaused(true);
        grid = FindFirstObjectByType<GridSystemManager>()?.grid;
        BuildableObject freshPlot = worldRegistry.Buildings.Single(value =>
            value != null && value.PersistentInstanceId.Value == w05HarvestPlotId);
        bool freshObjects = !ReferenceEquals(previousPlot, freshPlot);
        float startTime = actor.GameClock.Time;
        int startFrame = Time.frameCount;
        float startWall = Time.realtimeSinceStartup;
        while (cropPlots.Capture().plots.Single(value => value.buildingInstanceId == w05HarvestPlotId)
            .pendingHarvest.phase != CropHarvestOutputPhase.None)
        {
            if (actor.GameClock.Time - startTime > 15f && Time.frameCount - startFrame > 120
                || Time.realtimeSinceStartup - startWall > 120f)
                throw new InvalidOperationException("W05 restored harvest did not finalize: gameSeconds="
                    + (actor.GameClock.Time - startTime) + "; frames=" + (Time.frameCount - startFrame));
            yield return null; // Production crop tick resumes the frozen owner; no synthetic work call.
        }
        GameplayOutcomeSnapshot[] children = W01ExactOutcomes().Where(value =>
            W01ResultKey(value) == request.ChildResultKey).ToArray();
        GameplayOutcomeSnapshot[] identities = W01ExactOutcomes().Where(value =>
            W01ResultKey(value) == WorkCompletionIdentityOutcomeIds.ResultKey(request).ToString()).ToArray();
        bool resumed = freshObjects && children.Length == 1 && identities.Length == 1
            && children[0].immutablePayloadHash == childBefore.immutablePayloadHash
            && children[0].sequence < identities[0].sequence
            && CapturePhysicalItemQuantity(itemId) == outputQuantity
            && FindW05DeliveryCursor(w05IdentityDeliveries.Capture(), request.ProducerStreamId)?.payloadFingerprint
                == request.PayloadFingerprint;
        results.Add(new WorkTypeLiveRow("w01:harvest-pending-save-resume", resumed ? "PASS" : "FAIL",
            w05HarvestPlotId, "officialSections=" + pendingWorld.sections.Count + "; freshPlot=" + freshObjects
                + "; children=" + children.Length + "; identities=" + identities.Length
                + "; output=" + CapturePhysicalItemQuantity(itemId) + "; expected=" + outputQuantity
                + "; childHash=" + children.SingleOrDefault()?.immutablePayloadHash));
        if (!resumed) throw new InvalidOperationException("W05 pending harvest replay changed its physical or ledger result.");

        string completedJson = gameSaveService.ToJson(gameSaveService.Capture(), true);
        W05WriteEvidence("w01-harvest-completed-world.json", completedJson);
        if (!gameSaveService.TryRestore(gameSaveService.FromJson(completedJson), out var completedRestore)
            || !completedRestore.Success)
            throw new InvalidOperationException("W05 completed world restore failed: "
                + string.Join(" | ", completedRestore.Errors));
        actor = LiveActors(worldRegistry).Single(value => value.Identity.PersistentId == actorId);
        work = actor.GetComponent<AbilityWork>();
        brain = actor.Brain;
        actor.SetAiPaused(true);
        grid = FindFirstObjectByType<GridSystemManager>()?.grid;
        string beforeReplay = W05SectionPayloads(gameSaveService.Capture());
        IWorkCompletionIdentityDeliveryCommand command = runtimeScope.Container.Resolve<IWorkCompletionIdentityDeliveryCommand>();
        var firstReplay = command.EnsureApplied(request);
        var secondReplay = command.EnsureApplied(request);
        bool replaySafe = firstReplay.Status == WorkCompletionIdentityDeliveryStatus.AlreadyApplied
            && secondReplay.Status == WorkCompletionIdentityDeliveryStatus.AlreadyApplied
            && beforeReplay == W05SectionPayloads(gameSaveService.Capture());
        results.Add(new WorkTypeLiveRow("w01:harvest-completed-save-replay", replaySafe ? "PASS" : "FAIL",
            request.Character.Value, "officialRestore=true; redeliveries=2; sectionsUnchanged=" + replaySafe));
    }

    private static string W05SectionPayloads(DungeonGameSaveData save) => string.Join("\n", save.sections
        .OrderBy(value => value.sectionId, StringComparer.Ordinal)
        .Select(value => value.sectionId + ":" + value.sectionVersion + ":" + value.payloadJson));

    private static void W05WriteEvidence(string name, string content)
    {
        string directory = Path.GetDirectoryName(CharacterAiWorkTypeLiveMatrixPlayModeVerifier.W05HarvestIdentityReportPath);
        Directory.CreateDirectory(directory);
        WriteUtf8LfCreateNew(Path.Combine(directory, name), new[] { content });
    }

    // QA-only port decorator: pause after the real crop child commits, then
    // forward the unchanged request to the real identity command. Never fabricate success.
    private sealed class W05HarvestDeliveryGate : IWorkCompletionIdentityDeliveryCommand, IDisposable
    {
        private readonly CropPlotRuntime runtime;
        private readonly IDomainFactPublisher previous;
        private readonly WorkCompletionDomainFactTestPublisher<CropHarvestCompletedFact> routed;
        private readonly IWorkCompletionIdentityDeliveryCommand inner;
        public bool Held { get; private set; }
        public bool Released { get; set; }
        public WorkCompletionIdentityDeliveryRequest Request { get; private set; }
        public W05HarvestDeliveryGate(CropPlotRuntime runtime, IWorkCompletionIdentityDeliveryCommand inner,
            WorkCompletionIdentityDeliveryLedger deliveries, CharacterIdentityEventPublisher events,
            WorkIdentityAppliedFactMapping childValidation)
        {
            this.runtime = runtime;
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            var port = typeof(CropPlotRuntime).GetField("domainFacts", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("W05 crop fact publisher seam is missing.");
            previous = port.GetValue(runtime) as IDomainFactPublisher
                ?? throw new InvalidOperationException("W05 requires the real crop fact publisher.");
            routed = new(previous, this, events, deliveries, CropHarvestCompletionObservation.CreateRequest,
                WorkCompletionNotificationMode.ApplyOnly, childValidation);
            runtime.ConstructDomainFacts(routed);
        }
        public WorkCompletionIdentityDeliveryResult EnsureApplied(WorkCompletionIdentityDeliveryRequest request)
        {
            if (!Released)
            {
                Held = true;
                Request = request;
                return new(WorkCompletionIdentityDeliveryStatus.Deferred, "qa-post-child-checkpoint");
            }
            return inner.EnsureApplied(request);
        }
        public bool RetireProducerStream(string stream) => inner.RetireProducerStream(stream);
        public void Dispose() { runtime.ConstructDomainFacts(previous); routed.Dispose(); }
    }

    private sealed class W01RepairDeliveryGate : IWorkCompletionIdentityDeliveryCommand, IDisposable
    {
        private readonly WorkOrderRuntime runtime;
        private readonly FieldInfo port;
        private readonly IDomainFactPublisher previous;
        private readonly WorkCompletionDomainFactTestPublisher<
            RepairWorkCompletionFact> routed;
        private readonly IWorkCompletionIdentityDeliveryCommand inner;
        public bool Held { get; private set; }
        public bool Released { get; set; }
        public WorkCompletionIdentityDeliveryRequest Request { get; private set; }
        public W01RepairDeliveryGate(
            WorkOrderRuntime runtime,
            IDomainFactPublisher production,
            IWorkCompletionIdentityDeliveryCommand inner,
            WorkCompletionIdentityDeliveryLedger deliveries,
            CharacterIdentityEventPublisher events,
            WorkIdentityAppliedFactMapping childValidation)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            port = typeof(WorkOrderRuntime).GetField(
                    "repairFacts",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(
                    "W01 repair fact publisher seam is missing.");
            previous = port.GetValue(runtime) as IDomainFactPublisher;
            IDomainFactPublisher actual = previous ?? production
                ?? throw new ArgumentNullException(nameof(production));
            routed = new WorkCompletionDomainFactTestPublisher<
                RepairWorkCompletionFact>(
                    actual,
                    this,
                    events,
                    deliveries,
                    RepairWorkCompletionObservation.CreateCompletionRequest,
                    WorkCompletionNotificationMode.ApplyOnly,
                    childValidation);
            port.SetValue(runtime, routed);
        }
        public WorkCompletionIdentityDeliveryResult EnsureApplied(WorkCompletionIdentityDeliveryRequest request)
        {
            if (Released) return inner.EnsureApplied(request);
            if (Held && Request.PayloadFingerprint != request.PayloadFingerprint)
                throw new InvalidOperationException("W01 repair pending request changed before ACK.");
            Held = true;
            Request = request;
            return new(WorkCompletionIdentityDeliveryStatus.Deferred, "qa-repair-post-child-checkpoint");
        }
        public bool RetireProducerStream(string stream) => inner.RetireProducerStream(stream);
        public void Dispose()
        {
            port.SetValue(runtime, previous);
            routed.Dispose();
        }
    }

    // QA-only observation port. It forwards the unchanged production request
    // immediately and retains the exact request so the verifier can join it to
    // the committed child and replay it after an official whole-world restore.
    private sealed class W04RepairDeliveryObservation :
        IWorkCompletionIdentityDeliveryCommand,
        IDisposable
    {
        private readonly WorkOrderRuntime runtime;
        private readonly FieldInfo port;
        private readonly IDomainFactPublisher previous;
        private readonly WorkCompletionDomainFactTestPublisher<
            RepairWorkCompletionFact> routed;
        private readonly IWorkCompletionIdentityDeliveryCommand inner;
        private readonly List<WorkCompletionIdentityDeliveryRequest> requests =
            new();

        public W04RepairDeliveryObservation(
            WorkOrderRuntime runtime,
            IDomainFactPublisher production,
            IWorkCompletionIdentityDeliveryCommand inner,
            WorkCompletionIdentityDeliveryLedger deliveries,
            CharacterIdentityEventPublisher events,
            WorkIdentityAppliedFactMapping childValidation)
        {
            this.runtime = runtime
                ?? throw new ArgumentNullException(nameof(runtime));
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));
            port = typeof(WorkOrderRuntime).GetField(
                    "repairFacts",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(
                    "W04 repair fact publisher seam is missing.");
            previous = port.GetValue(runtime) as IDomainFactPublisher;
            IDomainFactPublisher actual = previous ?? production
                ?? throw new ArgumentNullException(nameof(production));
            routed = new WorkCompletionDomainFactTestPublisher<
                RepairWorkCompletionFact>(
                    actual,
                    this,
                    events,
                    deliveries,
                    RepairWorkCompletionObservation.CreateCompletionRequest,
                    WorkCompletionNotificationMode.ApplyOnly,
                    childValidation);
            port.SetValue(runtime, routed);
        }

        public IReadOnlyList<WorkCompletionIdentityDeliveryRequest> Requests =>
            requests;

        public WorkCompletionIdentityDeliveryResult EnsureApplied(
            WorkCompletionIdentityDeliveryRequest request)
        {
            requests.Add(request);
            return inner.EnsureApplied(request);
        }

        public bool RetireProducerStream(string stream) =>
            inner.RetireProducerStream(stream);

        public void Dispose()
        {
            port.SetValue(runtime, previous);
            routed.Dispose();
        }
    }

    private static IEnumerator EmptyP15FocusedCoroutine()
    {
        yield break;
    }

    private string CaptureP15PhysicalStackFingerprint() => string.Join(
        "|",
        physicalItems.GetAllStacks()
            .Where(value => value != null)
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .Select(value => value.StackId + ":" + value.ItemId + ":"
                + value.Quantity + ":" + value.AvailableQuantity + ":"
                + (int)value.State + ":" + value.DestinationId));

    private int CapturePhysicalItemQuantity(string itemId) => physicalItems
        .GetAllStacks()
        .Where(value => value != null
            && string.Equals(value.ItemId, itemId, StringComparison.Ordinal))
        .Sum(value => value.Quantity);

    private bool TryPlacePoweredP15Pair(
        out BuildableObject target,
        out string failureReason)
    {
        target = null;
        failureReason = string.Empty;
        BuildingSO generatorData = LoadAuthoredBuilding(value =>
            string.Equals(
                value.GetAbility<BuildingFacilityPartAbility>()?.code,
                "I02",
                StringComparison.Ordinal));
        BuildingSO p15Data = LoadAuthoredBuilding(value =>
            string.Equals(
                value.GetAbility<BuildingFacilityPartAbility>()?.code,
                "P15",
                StringComparison.Ordinal));
        BuildingSO wastewaterTankData = LoadAuthoredBuilding(value =>
            string.Equals(
                value.GetAbility<BuildingFacilityPartAbility>()?.code,
                "I09",
                StringComparison.Ordinal)
            && value.GetAbility<BuildingWaterStorageAbility>() is
            {
                wastewaterCapacity: > 0f
            } storage
            && (storage.channels & UtilityChannel.Wastewater) != 0);
        BuildingSO wastewaterDuctData = LoadAuthoredBuilding(value =>
            string.Equals(
                value.GetAbility<BuildingFacilityPartAbility>()?.code,
                "U04",
                StringComparison.Ordinal)
            && (value.GetAbility<BuildingUtilityConnectionAbility>()?.channels
                & UtilityChannel.Wastewater) != 0);
        if (generatorData == null
            || p15Data == null
            || wastewaterTankData == null
            || wastewaterDuctData == null)
        {
            failureReason = "I02, P15, I09, or U04 authored building is missing";
            return false;
        }

        Vector2Int[] anchors = grid.GetCells()
            .Where(cell => cell != null)
            .Select(cell => cell.Position)
            .OrderBy(position => position.y)
            .ThenBy(position => position.x)
            .ToArray();
        foreach (Vector2Int generatorAnchor in anchors)
        {
            IReadOnlyList<Vector2Int> generatorCells =
                generatorData.GetGridPosList(generatorAnchor);
            if (generatorCells.Count == 0)
                continue;
            Vector2Int p15Anchor = new(
                generatorCells.Max(position => position.x) + 1,
                generatorCells.Min(position => position.y));
            IReadOnlyList<Vector2Int> p15Cells = p15Data.GetGridPosList(p15Anchor);
            Vector2Int wastewaterTankAnchor = new(
                p15Cells.Max(position => position.x) + 1,
                p15Cells.Min(position => position.y));
            IReadOnlyList<Vector2Int> wastewaterTankCells =
                wastewaterTankData.GetGridPosList(wastewaterTankAnchor);
            HashSet<Vector2Int> infrastructureCells = generatorCells
                .Concat(p15Cells)
                .Concat(wastewaterTankCells)
                .ToHashSet();
            if (infrastructureCells.Count
                    != generatorCells.Count + p15Cells.Count
                        + wastewaterTankCells.Count
                || generatorCells.Any(position =>
                    grid.GetGridCell(position) is not GridCell cell
                    || !cell.CanBuildInArea(generatorData)
                    || !cell.CanOccupy(generatorData.Placement.Layer))
                || p15Cells.Any(position =>
                    grid.GetGridCell(position) is not GridCell cell
                    || !cell.CanBuildInArea(p15Data)
                    || !cell.CanOccupy(p15Data.Placement.Layer))
                || wastewaterTankCells.Any(position =>
                    grid.GetGridCell(position) is not GridCell cell
                    || !cell.CanBuildInArea(wastewaterTankData)
                    || !cell.CanOccupy(wastewaterTankData.Placement.Layer)))
            {
                continue;
            }

            BuildableObject generator = PlaceAuthoredBuildingAt(
                generatorData,
                generatorAnchor,
                out string generatorFailure);
            if (generator == null)
                continue;
            rowScopedFixtureBuildings.Add(generator);
            BuildableObject p15 = PlaceAuthoredBuildingAt(
                p15Data,
                p15Anchor,
                out string p15Failure);
            if (p15 == null)
            {
                CleanupRowScopedFixtures(out _);
                failureReason = "P15 exact placement failed:" + p15Failure;
                continue;
            }
            rowScopedFixtureBuildings.Add(p15);
            BuildableObject wastewaterTank = PlaceAuthoredBuildingAt(
                wastewaterTankData,
                wastewaterTankAnchor,
                out string wastewaterTankFailure);
            if (wastewaterTank == null)
            {
                CleanupRowScopedFixtures(out _);
                failureReason = "I09 exact placement failed:"
                    + wastewaterTankFailure;
                continue;
            }
            rowScopedFixtureBuildings.Add(wastewaterTank);
            if (!TryOverlayP15WastewaterDucts(
                    p15,
                    wastewaterDuctData,
                    infrastructureCells,
                    out string wastewaterFailure))
            {
                CleanupRowScopedFixtures(out _);
                failureReason = "P15 wastewater overlay failed:"
                    + wastewaterFailure;
                continue;
            }
            GridPathSearchResult search = grid.SearchPath(actor.GetNowXY());
            if (!WorkTargetSelectionRules.IsReachable(p15, search))
            {
                CleanupRowScopedFixtures(out _);
                failureReason = "adjacent powered P15 has no reachable work access";
                continue;
            }

            target = p15;
            return true;
        }

        if (TryPlaceP15PairInVerifierSpan(
                generatorData,
                p15Data,
                wastewaterTankData,
                wastewaterDuctData,
                out target,
                out string spanFailure))
        {
            return true;
        }

        failureReason = "no reachable adjacent I02+P15 authored placement; span="
            + spanFailure;
        return false;
    }

    private bool TryPlaceP15PairInVerifierSpan(
        BuildingSO generatorData,
        BuildingSO p15Data,
        BuildingSO wastewaterTankData,
        BuildingSO wastewaterDuctData,
        out BuildableObject target,
        out string failureReason)
    {
        target = null;
        failureReason = "no safe six-cell verifier span";
        GridPathSearchResult preMutationSearch = grid.SearchPath(actor.GetNowXY());
        for (int y = 0; y < grid.height; y++)
        {
            for (int x = 0; x <= grid.width - 6; x++)
            {
                // Dungeon y is a floor rather than north/south space. Reserve
                // the reachable stand on the actor-facing left edge, then put
                // P15 before I02 so the five-cell powered pair cannot cut the
                // actor off from the workstation stand.
                Vector2Int access = new(x, y);
                if (!grid.IsValidGridPos(access)
                    || !grid.IsWalkable(access)
                    || !preMutationSearch.ContainsPosition(access))
                {
                    continue;
                }

                HashSet<BuildableObject> displacements = new();
                bool safe = true;
                for (int offset = 1; offset <= 5 && safe; offset++)
                {
                    Vector2Int position = new(x + offset, y);
                    GridCell cell = grid.GetGridCell(position);
                    if (cell == null)
                    {
                        safe = false;
                        break;
                    }
                    foreach (GridLayer layer in Enum.GetValues(typeof(GridLayer)))
                    {
                        IGridOccupant occupant = cell.GetOccupant(layer);
                        if (occupant == null)
                            continue;
                        if ((layer == GridLayer.Building
                                || layer == GridLayer.Hallway)
                            && occupant is BuildableObject movement
                            && movement is not Facility
                            && movement is not Door
                            && movement.Facility == null
                            && movement.IsGridMovement
                            && !movement.BlocksGridMovement)
                        {
                            displacements.Add(movement);
                            continue;
                        }
                        if (layer == generatorData.Placement.Layer
                            || layer == p15Data.Placement.Layer
                            || layer == wastewaterDuctData.Placement.Layer
                            || layer == GridLayer.Character
                            || layer == GridLayer.Wildlife)
                        {
                            safe = false;
                            break;
                        }
                    }
                }
                if (!safe)
                    continue;

                for (int offset = 1; offset <= 5; offset++)
                {
                    Vector2Int position = new(x + offset, y);
                    GridCell cell = grid.GetGridCell(position);
                    roomAreaSnapshots.Add(new FixtureAreaSnapshot(
                        position,
                        cell.AreaType));
                    grid.SetAreaType(position, GridCellAreaType.DungeonInterior);
                }
                bool displaced = true;
                foreach (BuildableObject movement in displacements
                             .OrderBy(value => value.centerPos.y)
                             .ThenBy(value => value.centerPos.x)
                             .ThenBy(value => value.GridId))
                {
                    GridLayer layer = movement.BuildingData.Placement.Layer;
                    Vector2Int[] positions = movement.buildPoses.ToArray();
                    if (!grid.RemoveOccupant(
                            movement,
                            layer,
                            positions,
                            movement.BuildingData.Placement.IsMovement))
                    {
                        displaced = false;
                        failureReason = "movement displacement failed:"
                            + movement.GridId;
                        break;
                    }
                    displacedRoomMovements.Add(new DisplacedMovementSnapshot(
                        movement,
                        layer,
                        positions,
                        movement.BuildingData.Placement.IsMovement));
                }
                if (!displaced)
                {
                    CleanupRoomFixture(out _);
                    continue;
                }

                // This focused verifier owns execution-mode admission, not a
                // long-distance routing benchmark. Move the isolated subject
                // onto the already-proven reachable stand before the five-cell
                // pair closes the one-dimensional floor corridor; the whole
                // save baseline restores the exact original actor position.
                actor.transform.position = grid.GetWorldPos(access);
                brain.ClearPathSearchCache();

                Vector2Int p15Anchor = new(x + 1, y);
                Vector2Int generatorAnchor = new(x + 3, y);
                BuildableObject generator = PlaceAuthoredBuildingAt(
                    generatorData,
                    generatorAnchor,
                    out string generatorFailure);
                if (generator == null)
                {
                    failureReason = "span I02 placement failed:" + generatorFailure;
                    CleanupRoomFixture(out _);
                    continue;
                }
                rowScopedFixtureBuildings.Add(generator);
                BuildableObject p15 = PlaceAuthoredBuildingAt(
                    p15Data,
                    p15Anchor,
                    out string p15Failure);
                if (p15 == null)
                {
                    failureReason = "span P15 placement failed:" + p15Failure;
                    CleanupRowScopedFixtures(out _);
                    CleanupRoomFixture(out _);
                    continue;
                }
                rowScopedFixtureBuildings.Add(p15);
                if (!TryPlaceP15WastewaterTankAndRoute(
                        p15,
                        wastewaterTankData,
                        wastewaterDuctData,
                        out string wastewaterFailure))
                {
                    failureReason = "span P15 wastewater route failed:"
                        + wastewaterFailure;
                    CleanupRowScopedFixtures(out _);
                    CleanupRoomFixture(out _);
                    continue;
                }
                GridPathSearchResult postSearch = grid.SearchPath(actor.GetNowXY());
                if (!WorkTargetSelectionRules.IsReachable(p15, postSearch))
                {
                    failureReason = "span P15 work access is unreachable";
                    CleanupRowScopedFixtures(out _);
                    CleanupRoomFixture(out _);
                    continue;
                }

                target = p15;
                return true;
            }
        }
        return false;
    }

    private bool TryPlaceP15WastewaterTankAndRoute(
        BuildableObject p15,
        BuildingSO wastewaterTankData,
        BuildingSO wastewaterDuctData,
        out string failureReason)
    {
        failureReason = "no same-floor I09 route";
        IReadOnlyList<Vector2Int> p15Cells = p15.BuildingData.GetGridPosList(
            p15.centerPos);
        int p15MinX = p15Cells.Min(value => value.x);
        int p15MaxX = p15Cells.Max(value => value.x);
        int floor = p15Cells.Min(value => value.y);
        Vector2Int[] anchors = Enumerable.Range(0, grid.width)
            .Select(x => new Vector2Int(x, floor))
            .OrderBy(value => Mathf.Min(
                Mathf.Abs(value.x - p15MinX),
                Mathf.Abs(value.x - p15MaxX)))
            .ThenBy(value => value.x)
            .ToArray();

        foreach (Vector2Int tankAnchor in anchors)
        {
            IReadOnlyList<Vector2Int> tankCells =
                wastewaterTankData.GetGridPosList(tankAnchor);
            if (tankCells.Count == 0
                || tankCells.Any(value => value.y != floor
                    || grid.GetGridCell(value) == null))
            {
                continue;
            }

            int tankMinX = tankCells.Min(value => value.x);
            int tankMaxX = tankCells.Max(value => value.x);
            int routeMinX = Mathf.Min(p15MinX, tankMinX);
            int routeMaxX = Mathf.Max(p15MaxX, tankMaxX);
            Vector2Int[] routeCells = Enumerable
                .Range(routeMinX, routeMaxX - routeMinX + 1)
                .Select(x => new Vector2Int(x, floor))
                .ToArray();
            bool routeAvailable = routeCells.All(position =>
            {
                GridCell cell = grid.GetGridCell(position);
                IGridOccupant utility = cell?.GetOccupant(GridLayer.Utility);
                if (utility == null)
                    return cell != null;
                return utility is BuildableObject existing
                    && (existing.BuildingData?.GetAbility<
                            BuildingUtilityConnectionAbility>()?.channels
                        & UtilityChannel.Wastewater) != 0;
            });
            if (!routeAvailable)
                continue;

            HashSet<BuildableObject> displacements = new();
            bool tankAreaAvailable = true;
            foreach (Vector2Int position in tankCells)
            {
                GridCell cell = grid.GetGridCell(position);
                foreach (GridLayer layer in Enum.GetValues(typeof(GridLayer)))
                {
                    IGridOccupant occupant = cell.GetOccupant(layer);
                    if (occupant == null)
                        continue;
                    if ((layer == GridLayer.Building
                            || layer == GridLayer.Hallway)
                        && occupant is BuildableObject movement
                        && movement is not Facility
                        && movement is not Door
                        && movement.Facility == null
                        && movement.IsGridMovement
                        && !movement.BlocksGridMovement)
                    {
                        displacements.Add(movement);
                        continue;
                    }
                    if (layer == wastewaterTankData.Placement.Layer
                        || layer == GridLayer.Character
                        || layer == GridLayer.Wildlife)
                    {
                        tankAreaAvailable = false;
                        break;
                    }
                }
                if (!tankAreaAvailable)
                    break;
            }
            if (!tankAreaAvailable)
                continue;

            foreach (Vector2Int position in tankCells)
            {
                GridCell cell = grid.GetGridCell(position);
                roomAreaSnapshots.Add(new FixtureAreaSnapshot(
                    position,
                    cell.AreaType));
                grid.SetAreaType(position, GridCellAreaType.DungeonInterior);
            }
            bool displaced = true;
            foreach (BuildableObject movement in displacements
                         .OrderBy(value => value.centerPos.y)
                         .ThenBy(value => value.centerPos.x)
                         .ThenBy(value => value.GridId))
            {
                GridLayer layer = movement.BuildingData.Placement.Layer;
                Vector2Int[] positions = movement.buildPoses.ToArray();
                if (!grid.RemoveOccupant(
                        movement,
                        layer,
                        positions,
                        movement.BuildingData.Placement.IsMovement))
                {
                    displaced = false;
                    failureReason = "I09 movement displacement failed:"
                        + movement.GridId;
                    break;
                }
                displacedRoomMovements.Add(new DisplacedMovementSnapshot(
                    movement,
                    layer,
                    positions,
                    movement.BuildingData.Placement.IsMovement));
            }
            if (!displaced)
                return false;

            BuildableObject tank = PlaceAuthoredBuildingAt(
                wastewaterTankData,
                tankAnchor,
                out string tankFailure);
            if (tank == null)
            {
                failureReason = "I09 placement failed:" + tankFailure;
                return false;
            }
            rowScopedFixtureBuildings.Add(tank);

            foreach (Vector2Int position in routeCells)
            {
                IGridOccupant existingUtility = grid.GetGridCell(position)
                    ?.GetOccupant(GridLayer.Utility);
                if (existingUtility != null)
                    continue;
                BuildableObject duct = PlaceAuthoredBuildingAt(
                    wastewaterDuctData,
                    position,
                    out string ductFailure);
                if (duct == null)
                {
                    failureReason = "U04 route placement failed at "
                        + position + ":" + ductFailure;
                    return false;
                }
                rowScopedFixtureBuildings.Add(duct);
            }

            IFluidWastewaterTransaction wastewater =
                runtimeScope.Container.Resolve<IFluidWastewaterTransaction>();
            if (wastewater.CanAcceptWastewater(
                    p15,
                    0.25f,
                    out DomainFailure wastewaterFailure))
            {
                return true;
            }
            failureReason = wastewaterFailure.ToString();
            return false;
        }
        return false;
    }

    private bool TryOverlayP15WastewaterDucts(
        BuildableObject p15,
        BuildingSO wastewaterDuctData,
        IEnumerable<Vector2Int> infrastructureCells,
        out string failureReason)
    {
        failureReason = string.Empty;
        foreach (Vector2Int position in infrastructureCells
                     .Distinct()
                     .OrderBy(value => value.y)
                     .ThenBy(value => value.x))
        {
            BuildableObject duct = PlaceAuthoredBuildingAt(
                wastewaterDuctData,
                position,
                out string ductFailure);
            if (duct == null)
            {
                failureReason = "U04 placement failed at " + position
                    + ":" + ductFailure;
                return false;
            }
            rowScopedFixtureBuildings.Add(duct);
        }

        IFluidWastewaterTransaction wastewater =
            runtimeScope.Container.Resolve<IFluidWastewaterTransaction>();
        if (!wastewater.CanAcceptWastewater(
                p15,
                0.25f,
                out DomainFailure wastewaterFailure))
        {
            failureReason = wastewaterFailure.IsFailure
                ? wastewaterFailure.ToString()
                : FailureCode.FluidWastewaterUnavailable.ToString();
            return false;
        }
        return true;
    }

    private ProductionBillSnapshot FindFocusedBill(P15FocusedFixture focused) =>
        focused == null
            ? null
            : productionBills.GetBills(focused.Target)
                .FirstOrDefault(value => value.BillId == focused.BillId);

    private bool CloseP15FocusedFixture(
        P15FocusedFixture focused,
        out string failureReason)
    {
        actor?.SetAiPaused(true);
        brain?.StopCurrentActionForReplan("P15 focused fixture cleanup");
        work?.ClearPriorityWorkTarget();
        string billFailure = string.Empty;
        bool billClosed = focused?.Fixture == null
            || focused.Fixture.TryInvalidate(out billFailure);
        bool fixturesClosed = CleanupRowScopedFixtures(out string fixtureFailure);
        bool roomClosed = CleanupRoomFixture(out string roomFailure);
        failureReason = string.Join(
            ";",
            new[]
            {
                billClosed ? string.Empty : "bill=" + billFailure,
                fixturesClosed ? string.Empty : "fixtures=" + fixtureFailure,
                roomClosed ? string.Empty : "room=" + roomFailure
            }.Where(value => value.Length > 0));
        return billClosed && fixturesClosed && roomClosed;
    }

    private static bool BillTokenChanged(
        ProductionBillSnapshot before,
        ProductionBillSnapshot after) =>
        before != null
        && (after == null
            || after.CompletedWork > before.CompletedWork
            || after.RemainingCycles < before.RemainingCycles);

    private static long DeltaCounterAcrossReset(long before, long after) =>
        after >= before ? after - before : Math.Max(0L, after);

    private static bool DomainFailureContains(
        DomainFailure failure,
        string expected)
    {
        if (!failure.IsFailure || string.IsNullOrEmpty(expected))
            return false;
        ReadOnlySpan<string> parameters = failure.Parameters;
        for (int index = 0; index < parameters.Length; index++)
        {
            if (string.Equals(parameters[index], expected, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private IEnumerator RunRow(WorkTypeId workTypeId)
    {
        if (!CleanupRowScopedFixtures(out string rowCleanupFailure))
        {
            results.Add(WorkTypeLiveRow.Blocked(
                workTypeId,
                "previous-row-fixture-cleanup-failed",
                rowCleanupFailure));
            yield break;
        }
        if (!CleanupRoomFixture(out string roomCleanupFailure))
        {
            results.Add(WorkTypeLiveRow.Blocked(
                workTypeId,
                "previous-room-fixture-cleanup-failed",
                roomCleanupFailure));
            yield break;
        }
        MaintainStableWorkSubject();
        MaterialWorkFixture fixture = null;
        string fixtureFailure = string.Empty;
        if (IsDomainFixtureWork(workTypeId))
        {
            IEnumerator domainSetup = PrepareDomainFixture(
                workTypeId,
                value => fixture = value,
                value => fixtureFailure = value);
            while (domainSetup.MoveNext())
                yield return domainSetup.Current;
        }
        else if (!TryPrepareMaterialFixture(
                     workTypeId,
                     out fixture,
                     out fixtureFailure))
        {
            results.Add(WorkTypeLiveRow.Blocked(
                workTypeId,
                "production-live-fixture-unavailable",
                fixtureFailure));
            yield break;
        }
        if (IsDomainFixtureWork(workTypeId) && fixture == null)
        {
            results.Add(WorkTypeLiveRow.Blocked(
                workTypeId,
                "production-live-domain-fixture-unavailable",
                fixtureFailure));
            IEnumerator cleanup = CleanupSingleDomainRoomAfterRow(workTypeId);
            while (cleanup.MoveNext())
                yield return cleanup.Current;
            yield break;
        }

        // Domain transactions change candidate availability without changing
        // building topology. Publish that production boundary before the
        // selector reads its incremental cache.
        if (fixture != null)
        {
            runtimeScope.Container.Resolve<IFacilityCandidateCache>()
                .MarkDynamicStateDirty();
            yield return null;
        }

        PrepareActor(workTypeId);
        WorkTargetCandidate candidate = default;
        BuildableObject target = null;
        float candidateDeadline = Time.realtimeSinceStartup + 2.5f;
        while (Time.realtimeSinceStartup < candidateDeadline)
        {
            MaintainStableWorkSubject();
            GridPathSearchResult search = grid.SearchPath(actor.GetNowXY());
            if (work.TryGetBestWorkCandidate(workTypeId, search, out candidate))
            {
                target = WorkTargetCandidateRuntimeAdapter.ResolveBuilding(candidate);
                if (target != null)
                    break;
            }
            yield return null;
        }

        if (target == null)
        {
            WorkTargetCandidate rejected = work.LastRejectedWorkCandidate;
            results.Add(WorkTypeLiveRow.Blocked(
                workTypeId,
                "production-target-unavailable",
                "candidate=" + candidate.FailureKind + ":" + candidate.FailureReason
                + "; rejected=" + rejected.FailureKind + ":" + rejected.FailureReason
                + "; fixtureAuthority=" + DescribeFixtureAuthority(workTypeId, fixture)
                + "; prerequisite=" + GetMissingPrerequisite(workTypeId)));
            IEnumerator cleanup = CleanupSingleDomainRoomAfterRow(workTypeId);
            while (cleanup.MoveNext())
                yield return cleanup.Current;
            yield break;
        }
        if (fixture != null && !fixture.AcceptsTarget(target))
        {
            // The live world may contain another equally valid target with a
            // higher autonomous urgency (for example an exterior drop zone).
            // This matrix already issues the public priority-work command in
            // RunLivePhase, so use the fixture target and let that production
            // command perform the real path/execution validation.  No handler
            // is called directly here.
            target = fixture.Target;
        }
        // Unity fake-null objects cannot be dereferenced after the invalidation
        // phase. Capture durable report identity before either phase mutates
        // the production target.
        string targetLabel = target != null
            ? target.name
            : "<missing-target>";

        WorkPhaseResult cancellation = null;
        IEnumerator cancellationRun = RunLivePhase(
            workTypeId,
            target,
            WorkProbeFault.CancelAfterApprovedProgress,
            null,
            value => cancellation = value);
        while (cancellationRun.MoveNext())
            yield return cancellationRun.Current;

        if (workTypeId == BuiltInWorkTypeIds.GrandProject)
        {
            IGrandProjectRuntime grand =
                runtimeScope.Container.Resolve<IGrandProjectRuntime>();
            IProjectWorkforceRuntime workforce =
                runtimeScope.Container.Resolve<IProjectWorkforceRuntime>();
            string projectId = grand.State.activeProjectId;
            float releaseDeadline = Time.realtimeSinceStartup + 2f;
            while (workforce.GetActiveWorkerCount(projectId) > 0
                   && Time.realtimeSinceStartup < releaseDeadline)
            {
                yield return null;
            }
            if (workforce.GetActiveWorkerCount(projectId) > 0)
            {
                results.Add(new WorkTypeLiveRow(
                    workTypeId.Value,
                    "FAIL",
                    targetLabel,
                    (cancellation?.Format() ?? "cancel-phase-missing")
                    + " | grand-project-worker-lease-not-released:project="
                    + projectId + "; activeWorkers="
                    + workforce.GetActiveWorkerCount(projectId)));
                yield break;
            }
        }

        PrepareActor(workTypeId);
        if (fixture != null
            && !fixture.TryPrepareInvalidationPhase(out string phaseFailure))
        {
            results.Add(new WorkTypeLiveRow(
                workTypeId.Value,
                "FAIL",
                targetLabel,
                (cancellation?.Format() ?? "cancel-phase-missing")
                + " | invalidation-fixture-failed:" + phaseFailure));
            IEnumerator cleanup = CleanupSingleDomainRoomAfterRow(workTypeId);
            while (cleanup.MoveNext())
                yield return cleanup.Current;
            yield break;
        }
        WorkPhaseResult invalidation = null;
        IEnumerator invalidationRun = RunLivePhase(
            workTypeId,
            target,
            WorkProbeFault.InvalidateTargetAfterStart,
            fixture,
            value => invalidation = value);
        while (invalidationRun.MoveNext())
            yield return invalidationRun.Current;

        WorkPhaseResult completion = null;
        string completionSetupFailure = string.Empty;
        string completionTargetLabel = string.Empty;
        if (workTypeId == BuiltInWorkTypeIds.Dismantle)
        {
            // The invalidation row intentionally cancels the order while
            // leaving its rejected facility in the world.  Remove that
            // verifier-owned fixture before creating the success fixture so it
            // can reuse the same safe authored anchor; otherwise the second
            // facility is pushed to the exposed top row and the environment
            // policy (correctly) interrupts it before any dismantle WU.
            if (target != null && !target.isDestroy)
            {
                target.DestroySelf();
                yield return null;
            }
            PrepareActor(workTypeId);
            if (TryPrepareDismantleFixture(
                    out MaterialWorkFixture completionFixture,
                    out completionSetupFailure,
                    requireFaultObservationWindow: false))
            {
                runtimeScope.Container.Resolve<IFacilityCandidateCache>()
                    .MarkDynamicStateDirty();
                yield return null;
                // The real construction and rejected-quality setup above can
                // publish actor mood/activity effects. Re-establish the clean
                // phase boundary after materialization and immediately before
                // the live AI command; never neutralize the actor after work
                // has started.
                PrepareActor(workTypeId);
                BuildableObject completionTarget = completionFixture.Target;
                completionTargetLabel = completionTarget != null
                    ? completionTarget.name
                    : "<missing-completion-target>";
                IEnumerator completionRun = RunLivePhase(
                    workTypeId,
                    completionTarget,
                    WorkProbeFault.CompleteNormally,
                    completionFixture,
                    value => completion = value);
                while (completionRun.MoveNext())
                    yield return completionRun.Current;
            }
        }

        bool passed = cancellation != null && cancellation.Passed
            && invalidation != null && invalidation.Passed
            && (workTypeId != BuiltInWorkTypeIds.Dismantle
                || completion != null && completion.Passed);
        results.Add(new WorkTypeLiveRow(
            workTypeId.Value,
            passed ? "PASS" : "FAIL",
            targetLabel,
            (cancellation?.Format() ?? "cancel-phase-missing")
            + " | " + (invalidation?.Format() ?? "invalidation-phase-missing")
            + (workTypeId != BuiltInWorkTypeIds.Dismantle
                ? string.Empty
                : " | completionTarget=" + completionTargetLabel
                    + "; " + (completion?.Format()
                        ?? "completion-phase-missing:"
                        + completionSetupFailure))));
        IEnumerator rowRoomCleanup = CleanupSingleDomainRoomAfterRow(workTypeId);
        while (rowRoomCleanup.MoveNext())
            yield return rowRoomCleanup.Current;
    }

    private IEnumerator RunLivePhase(
        WorkTypeId workTypeId,
        BuildableObject target,
        WorkProbeFault fault,
        MaterialWorkFixture fixture,
        Action<WorkPhaseResult> complete,
        P15FocusedFixture persistentPauseFixture = null,
        Func<bool> completionAuthoritySatisfied = null)
    {
        WorkPhaseResult result = new WorkPhaseResult(fault);
        if (workTypeId == BuiltInWorkTypeIds.Plumbing)
        {
            result.DomainTrace = "before-phase=" + CapturePlumbingState(target);
        }
        else if (workTypeId == BuiltInWorkTypeIds.Dismantle)
        {
            result.DomainTrace = "before-phase="
                + DescribeFixtureAuthority(workTypeId, fixture);
        }
        MaintainStableWorkSubject();
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            result.Blocker = "target-not-active-before-phase";
            complete(result);
            yield break;
        }

        // A need action which was already committed before this row owns a
        // higher-priority external intent.  Do not terminate it from the
        // verifier and do not mistake its primitive phase for a failed work
        // start.  Keep the authored needs neutral and wait for the production
        // action to reach its own terminal before asking the scheduler to
        // select this work row.
        float intentDeadline = Time.realtimeSinceStartup + 12f;
        while (brain.IsExternallyDrivenActionActive
               && Time.realtimeSinceStartup < intentDeadline)
        {
            MaintainStableWorkSubject();
            yield return null;
        }
        if (brain.IsExternallyDrivenActionActive)
        {
            result.Blocker = "higher-priority-intent-did-not-settle:owner="
                + brain.ExternalIntentOwnerId + "; kind=" + brain.ExternalIntentKind
                + "; phase=" + brain.CurrentActionPhase;
            complete(result);
            yield break;
        }
        brain.StopCurrentActionForReplan(
            "worktype live matrix higher-priority intent settled");
        float previousActionDeadline = Time.realtimeSinceStartup + 8f;
        while ((brain.HasRunningAction
                || brain.IsExternallyDrivenActionActive
                || work.HasActiveWorkRoutineForDiagnostics
                || work.isWorking)
               && Time.realtimeSinceStartup < previousActionDeadline)
        {
            MaintainStableWorkSubject();
            yield return null;
        }
        if (brain.HasRunningAction
            || brain.IsExternallyDrivenActionActive
            || work.HasActiveWorkRoutineForDiagnostics
            || work.isWorking)
        {
            result.Blocker = "previous-action-did-not-settle:running="
                + brain.HasRunningAction + "; external="
                + brain.IsExternallyDrivenActionActive + "; working="
                + work.isWorking + "; routine="
                + work.HasActiveWorkRoutineForDiagnostics + "; run="
                + work.ActiveWorkRunIdForDiagnostics + "; phase="
                + brain.CurrentActionPhase;
            complete(result);
            yield break;
        }
        for (int frame = 0; frame < 2; frame++)
        {
            MaintainStableWorkSubject();
            yield return null;
        }

        // Capture the lifecycle before the public priority command. Some work
        // commands can synchronously publish an action epoch when their target
        // is already reachable; capturing afterwards would lose that start and
        // misclassify a live DrawWater action as "did not start".
        CharacterAiRuntimeDiagnosticsSnapshot start =
            brain.CaptureRuntimeDiagnostics();
        CharacterAiRuntimeGateSnapshot startGate = start.Gate;
        long startEpoch = brain.RuntimeActionEpoch;
        long startWorkRevision =
            work.ApprovedWorkProgressRevisionForDiagnostics;
        result.StartEpoch = startEpoch;
        result.StartGate = startGate;

        GridPathSearchResult search = grid.SearchPath(actor.GetNowXY());
        if (!work.TrySetPriorityWorkTarget(
                target,
                workTypeId,
                search,
                out string priorityFailure))
        {
            WorkTargetCandidate firstRejected = work.LastRejectedWorkCandidate;
            // Forced work in an unsafe exterior/environment is a two-step
            // production command: first call publishes the warning, the exact
            // same call confirms it. This is the same public handshake used by
            // the player command UI; a second rejection remains a real red.
            if (!work.TrySetPriorityWorkTarget(
                    target,
                    workTypeId,
                    search,
                    out string confirmedFailure))
            {
                WorkTargetCandidate confirmedRejected =
                    work.LastRejectedWorkCandidate;
                result.Blocker = "priority-target-rejected:first="
                    + priorityFailure + "; firstTyped="
                    + FormatRejectedCandidate(firstRejected)
                    + "; confirmed=" + confirmedFailure
                    + "; confirmedTyped="
                    + FormatRejectedCandidate(confirmedRejected);
                complete(result);
                yield break;
            }

            result.PriorityCommandDetail = "confirmed-after-warning:"
                + priorityFailure + "; typed="
                + FormatRejectedCandidate(firstRejected);
        }

        // Publish the actor lifecycle before selecting the preferred action.
        // SetAiPaused(false) only queues a decision; no scheduler turn can race
        // this synchronous preference call on the Unity main thread.
        actor.SetAiPaused(false);
        if (!actor.CanRunAi)
        {
            result.Blocker = "actor-cannot-run-ai-after-unpause:lifecycle="
                + actor.CurrentLifecycleState + "; enabled=" + brain.enabled;
            actor.SetAiPaused(true);
            complete(result);
            yield break;
        }
        if (!brain.PreferWorkActionOnNextDecision(workTypeId, 45f))
        {
            result.Blocker = "brain-rejected-work-preference; actions="
                + FormatAvailableActions(brain.availableActions);
            actor.SetAiPaused(true);
            complete(result);
            yield break;
        }

        brain.RequestImmediateReplan(clearFailures: true);
        // A deferred path-search is not a rejected work action. The production
        // scheduler is allowed to run a short AIWait while its preferred
        // AIWork remains pending, then retry it on a later decision. Preserve
        // that real scheduling path, but bound and account every intervening
        // epoch so an unrelated action or an immortal wait cannot become a
        // false green.
        const int maxDeferredWaitEpochs = 6;
        HashSet<long> deferredWaitEpochs = new HashSet<long>();
        HashSet<long> observedAiWorkEpochs = new HashSet<long>();
        long lastRetriedWaitEpoch = 0L;
        float startDeadline = Time.realtimeSinceStartup + 12f;
        while (Time.realtimeSinceStartup < startDeadline)
        {
            MaintainStableWorkSubject();
            if (work.isWorking
                && work.AssignedWorkTypeId == workTypeId
                && brain.RuntimeActionEpoch > startEpoch)
            {
                result.Started = true;
                result.ObservedWorkType = work.AssignedWorkTypeId.Value;
                result.ObservedEpoch = brain.RuntimeActionEpoch;
                break;
            }

            long decidingEpoch = brain.RuntimeActionEpoch;
            AIActionSet decidingActionSet = brain.bestAction?.actionset;
            bool workStillPreferred =
                brain.IsActionPreferredForNextDecision<AIWork>();
            if (decidingEpoch > startEpoch
                && decidingActionSet is AIWork)
            {
                observedAiWorkEpochs.Add(decidingEpoch);
            }
            else if (decidingEpoch > startEpoch
                     && decidingActionSet is AIWait
                     && workStillPreferred)
            {
                deferredWaitEpochs.Add(decidingEpoch);
                result.PreStartDeferredWaits = deferredWaitEpochs.Count;
                if (deferredWaitEpochs.Count > maxDeferredWaitEpochs)
                {
                    result.Blocker = "preferred-aiwork-deferred-wait-limit:count="
                        + deferredWaitEpochs.Count + "; phase="
                        + brain.CurrentActionPhase + "; failure="
                        + brain.LastActionFailure;
                    break;
                }
            }
            else if (decidingEpoch > startEpoch
                     && decidingActionSet != null
                     && !workStillPreferred)
            {
                result.Blocker = "preferred-aiwork-lost-before-start:action="
                    + decidingActionSet.GetType().Name + "; phase="
                    + brain.CurrentActionPhase + "; failure="
                    + brain.LastActionFailure;
                break;
            }

            CharacterAiRuntimeDiagnosticsSnapshot deciding =
                brain.CaptureRuntimeDiagnostics();
            if (decidingEpoch > startEpoch
                && deciding.TryGetActionTerminal(
                    decidingEpoch,
                    out CharacterAiActionTerminalKind decidingTerminal))
            {
                if (deferredWaitEpochs.Contains(decidingEpoch)
                    && (decidingTerminal == CharacterAiActionTerminalKind.Completed
                        || decidingTerminal == CharacterAiActionTerminalKind.Cancelled))
                {
                    if (lastRetriedWaitEpoch != decidingEpoch)
                    {
                        lastRetriedWaitEpoch = decidingEpoch;
                        if (!brain.PreferWorkActionOnNextDecision(workTypeId, 45f))
                        {
                            result.Blocker =
                                "preferred-aiwork-retry-registration-failed";
                            break;
                        }
                        brain.RequestImmediateReplan(clearFailures: false);
                    }
                }
                else if (observedAiWorkEpochs.Contains(decidingEpoch))
                {
                    result.ObservedEpoch = decidingEpoch;
                    result.ObservedTerminalKind = decidingTerminal;
                    result.TerminalObserved = true;
                    result.Blocker =
                        "preferred-aiwork-terminal-before-running-observation:"
                        + decidingTerminal + "; failure=" + brain.LastActionFailure
                        + "; currentAction=" + brain.CurrentActionDebugLabel
                        + "; currentPhase=" + brain.CurrentActionPhase
                        + "; assigned=" + work.AssignedWorkTypeId.Value;
                    break;
                }
                else if (!deferredWaitEpochs.Contains(decidingEpoch))
                {
                    result.Blocker = "unaccounted-prestart-terminal:epoch="
                        + decidingEpoch + "; terminal=" + decidingTerminal
                        + "; action="
                        + (decidingActionSet?.GetType().Name ?? "null");
                    break;
                }
            }
            yield return null;
        }

        if (!result.Started)
        {
            if (string.IsNullOrEmpty(result.Blocker))
            {
                result.Blocker = "aiwork-did-not-start:action="
                    + brain.CurrentActionDebugLabel
                    + "; phase=" + brain.CurrentActionPhase
                    + "; failure=" + brain.LastActionFailure
                    + "; assigned=" + work.AssignedWorkTypeId.Value
                    + "; deferredWaits=" + deferredWaitEpochs.Count;
            }
            complete(result);
            yield break;
        }

        for (long skippedEpoch = startEpoch + 1L;
             skippedEpoch < result.ObservedEpoch;
             skippedEpoch++)
        {
            CharacterAiRuntimeDiagnosticsSnapshot now =
                brain.CaptureRuntimeDiagnostics();
            bool isAccountedDeferredWait =
                deferredWaitEpochs.Contains(skippedEpoch);
            bool hasTerminal = now.TryGetActionTerminal(
                skippedEpoch,
                out CharacterAiActionTerminalKind currentTerminal);
            if (!isAccountedDeferredWait
                || !hasTerminal
                || (currentTerminal != CharacterAiActionTerminalKind.Completed
                    && currentTerminal
                        != CharacterAiActionTerminalKind.Cancelled))
            {
                result.Blocker =
                    "unaccounted-action-epoch-before-aiwork:epoch="
                    + skippedEpoch + "; deferredWait="
                    + isAccountedDeferredWait + "; terminal="
                    + (hasTerminal ? currentTerminal.ToString() : "missing");
                break;
            }
        }
        if (!string.IsNullOrEmpty(result.Blocker))
        {
            brain.StopCurrentActionForReplan(
                "worktype live matrix unaccounted pre-start epoch");
            actor.SetAiPaused(true);
            complete(result);
            yield break;
        }

        AIAction startedAction = brain.bestAction;
        if (startedAction?.actionset is not AIWork startedWorkAction)
        {
            result.Blocker = "started-action-is-not-aiwork:type="
                + (startedAction?.actionset?.GetType().Name ?? "null")
                + "; assigned=" + work.AssignedWorkTypeId.Value;
            brain.StopCurrentActionForReplan(
                "worktype live matrix rejected non-AIWork start");
            actor.SetAiPaused(true);
            complete(result);
            yield break;
        }
        if (work.AssignedWorkTypeId != workTypeId
            || (startedWorkAction.WorkTypeId.IsValid
                && startedWorkAction.WorkTypeId != workTypeId))
        {
            result.Blocker = "started-aiwork-type-mismatch:requested="
                + workTypeId.Value + "; action="
                + (startedWorkAction.WorkTypeId.IsValid
                    ? startedWorkAction.WorkTypeId.Value
                    : "generic") + "; assigned="
                + work.AssignedWorkTypeId.Value;
            brain.StopCurrentActionForReplan(
                "worktype live matrix rejected mismatched AIWork start");
            actor.SetAiPaused(true);
            complete(result);
            yield break;
        }
        result.ObservedActionType = startedWorkAction.GetType().Name + "["
            + (startedWorkAction.WorkTypeId.IsValid
                 ? startedWorkAction.WorkTypeId.Value
                 : "generic->" + workTypeId.Value) + "]";
        if (workTypeId == BuiltInWorkTypeIds.Plumbing)
        {
            result.DomainTrace += "; after-start="
                + CapturePlumbingState(work.assignedShop)
                + "; expectedTarget=" + FormatBuildingIdentity(target)
                + "; assignedTarget=" + FormatBuildingIdentity(work.assignedShop);
        }
        else if (workTypeId == BuiltInWorkTypeIds.Dismantle)
        {
            result.DomainTrace += "; after-start="
                + DescribeFixtureAuthority(workTypeId, fixture)
                + "; assignedTarget=" + FormatBuildingIdentity(work.assignedShop)
                + "; execution="
                + work.LastWorkOrderExecutionDetailForDiagnostics;
        }

        // The official scheduler selected and started this action. Freeze only
        // subsequent decisions while its already-running production coroutine
        // travels, works and reaches a typed terminal. Repeated direct decision
        // calls here used to race the scheduler and manufacture extra epochs.
        actor.SetAiPaused(true);

        AbilityMove move = null;
        actor.TryGetAbility<AbilityMove>(out move);
        AIAction observedAction = startedAction;
        result.EstimatedTravelSeconds =
            CalculateBoundedTravelObservationSeconds(observedAction);
        result.MovementStartPosition = actor.GetNowXY();
        bool targetWasActive = target.gameObject.activeSelf;
        if (fault == WorkProbeFault.CancelAfterApprovedProgress
            || fault == WorkProbeFault.PauseAfterApprovedProgress)
        {
            float progressSoftDeadline = Time.realtimeSinceStartup
                + result.EstimatedTravelSeconds;
            float progressHardDeadline = Time.realtimeSinceStartup
                + MaximumProgressObservationSeconds;
            float lastMovementProgressAt = Time.realtimeSinceStartup;
            Vector3 lastWorldPosition = actor.transform.position;
            long lastGameplayProgress = startGate.GameplayProgressRevision;
            while (Time.realtimeSinceStartup < progressHardDeadline
                   && work.ApprovedWorkProgressRevisionForDiagnostics <= startWorkRevision)
            {
                MaintainStableWorkSubject();
                CharacterAiRuntimeDiagnosticsSnapshot currentDiagnostics =
                    brain.CaptureRuntimeDiagnostics();
                CharacterAiRuntimeGateSnapshot currentGate =
                    currentDiagnostics.Gate;
                Vector3 currentWorldPosition = actor.transform.position;
                bool positionAdvanced =
                    (currentWorldPosition - lastWorldPosition).sqrMagnitude
                    > MovementPositionEpsilonSquared;
                bool waypointAdvanced =
                    currentGate.GameplayProgressRevision > lastGameplayProgress;
                if (positionAdvanced || waypointAdvanced)
                {
                    result.MovementObserved = true;
                    lastMovementProgressAt = Time.realtimeSinceStartup;
                    lastWorldPosition = currentWorldPosition;
                    lastGameplayProgress = currentGate.GameplayProgressRevision;
                }

                if (currentDiagnostics.TryGetActionTerminal(
                        result.ObservedEpoch,
                        out CharacterAiActionTerminalKind earlyTerminal))
                {
                    result.ObservedTerminalKind = earlyTerminal;
                    result.TerminalObserved = true;
                    result.Blocker =
                        "action-terminal-before-approved-wu:" + earlyTerminal;
                    break;
                }

                bool moving = IsMovementOrAdmissionPhase(
                    brain.CurrentRuntimePhase);
                // Restock and other multi-leg executors can publish an initial
                // AIAction path only to the work target, then resolve a hidden
                // warehouse/facility leg inside WorkTaskExecutor.  The initial
                // path estimate is therefore a soft bound: keep observing only
                // while the production runtime still owns a movement/admission
                // phase and its position/waypoint heartbeat is advancing.  A
                // non-moving work phase remains bounded by the original ETA,
                // and every movement phase still has the independent 5-second
                // stall watchdog plus the absolute 120-second ceiling.
                if (Time.realtimeSinceStartup >= progressSoftDeadline
                    && !moving)
                {
                    break;
                }
                if (moving
                    && Time.realtimeSinceStartup - lastMovementProgressAt
                    >= MovementStallSeconds)
                {
                    result.MovementStalled = true;
                    result.Blocker = "movement-waypoint-stalled; "
                        + CaptureStageDetail(target, move);
                    break;
                }
                yield return null;
            }

            result.Progressed =
                work.ApprovedWorkProgressRevisionForDiagnostics > startWorkRevision;
            result.ProgressDelta =
                work.ApprovedWorkProgressRevisionForDiagnostics - startWorkRevision;
            result.StageBeforeTerminalRequest = CaptureStageDetail(target, move);
            if (workTypeId == BuiltInWorkTypeIds.Plumbing)
            {
                result.DomainTrace += "; before-terminal="
                    + CapturePlumbingState(work.assignedShop)
                    + "; expected-before-terminal="
                    + CapturePlumbingState(target);
            }
            if (fault == WorkProbeFault.PauseAfterApprovedProgress
                && result.Progressed
                && string.IsNullOrEmpty(result.Blocker))
            {
                IEnumerator pauseWindow = ObservePersistentCookPauseWindow(
                    target,
                    persistentPauseFixture,
                    result);
                while (pauseWindow.MoveNext())
                    yield return pauseWindow.Current;
            }
            result.TerminalRequested = brain.StopCurrentActionForReplan(
                result.Progressed
                    ? fault == WorkProbeFault.PauseAfterApprovedProgress
                        ? "persistent Cook pause regression complete"
                        : "worktype live matrix cancel after approved progress"
                    : "worktype live matrix abort after progress timeout");
            if (!result.Progressed && string.IsNullOrEmpty(result.Blocker))
                result.Blocker = "approved-wu-progress-timeout; "
                    + CaptureStageDetail(target, move);
            else if (result.Progressed && !result.TerminalRequested)
                result.Blocker = "cancel-request-rejected";
        }
        else if (fault == WorkProbeFault.CompleteNormally)
        {
            // No verifier mutation: the production WorkOrder/AbilityWork path
            // must reach the domain-owned completion boundary and publish its
            // own Completed terminal.
            result.TerminalRequested = true;
        }
        else
        {
            if (fixture != null)
            {
                result.TerminalRequested = fixture.TryInvalidate(
                    out string invalidationFailure);
                if (!result.TerminalRequested)
                    result.Blocker = "fixture-invalidation-failed:" + invalidationFailure;
            }
            else
            {
                target.gameObject.SetActive(false);
                result.TerminalRequested = true;
            }
        }

        float terminalObservationSeconds = fault switch
        {
            WorkProbeFault.InvalidateTargetAfterStart =>
                result.EstimatedTravelSeconds,
            WorkProbeFault.CompleteNormally =>
                MaximumCompletionObservationSeconds,
            _ => 5f
        };
        float terminalDeadline = Time.realtimeSinceStartup
            + terminalObservationSeconds;
        float terminalLastMovementAt = Time.realtimeSinceStartup;
        Vector3 terminalLastWorldPosition = actor.transform.position;
        long terminalLastGameplayProgress =
            brain.CaptureRuntimeDiagnostics().Gate.GameplayProgressRevision;
        long terminalLastApprovedWorkProgress =
            work.ApprovedWorkProgressRevisionForDiagnostics;
        float terminalLastAuthoritativeProgressAt = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup < terminalDeadline)
        {
            MaintainStableWorkSubject();
            CharacterAiRuntimeDiagnosticsSnapshot current =
                brain.CaptureRuntimeDiagnostics();
            if (current.TryGetActionTerminal(
                    result.ObservedEpoch,
                    out CharacterAiActionTerminalKind terminalKind))
            {
                result.TerminalObserved = true;
                result.ObservedTerminalKind = terminalKind;
                if (fault == WorkProbeFault.CompleteNormally)
                {
                    result.StageBeforeTerminalRequest =
                        CaptureStageDetail(target, move);
                }
                break;
            }

            Vector3 currentWorldPosition = actor.transform.position;
            bool positionAdvanced =
                (currentWorldPosition - terminalLastWorldPosition).sqrMagnitude
                > MovementPositionEpsilonSquared;
            bool waypointAdvanced = current.Gate.GameplayProgressRevision
                > terminalLastGameplayProgress;
            long currentApprovedWorkProgress =
                work.ApprovedWorkProgressRevisionForDiagnostics;
            bool approvedWorkAdvanced = currentApprovedWorkProgress
                > terminalLastApprovedWorkProgress;
            if (positionAdvanced || waypointAdvanced)
            {
                result.MovementObserved = true;
                terminalLastMovementAt = Time.realtimeSinceStartup;
                terminalLastAuthoritativeProgressAt = Time.realtimeSinceStartup;
                terminalLastWorldPosition = currentWorldPosition;
                terminalLastGameplayProgress =
                    current.Gate.GameplayProgressRevision;
            }
            if (approvedWorkAdvanced)
            {
                terminalLastAuthoritativeProgressAt = Time.realtimeSinceStartup;
                terminalLastApprovedWorkProgress = currentApprovedWorkProgress;
            }

            bool moving = IsMovementOrAdmissionPhase(
                brain.CurrentRuntimePhase);
            if (moving
                && Time.realtimeSinceStartup - terminalLastMovementAt
                >= MovementStallSeconds)
            {
                result.MovementStalled = true;
                if (string.IsNullOrEmpty(result.Blocker))
                {
                    result.Blocker = "movement-waypoint-stalled; "
                        + CaptureStageDetail(target, move);
                }
                break;
            }
            if (fault == WorkProbeFault.CompleteNormally
                && !moving
                && Time.realtimeSinceStartup - terminalLastAuthoritativeProgressAt
                    >= CompletionNoProgressSeconds)
            {
                if (string.IsNullOrEmpty(result.Blocker))
                {
                    result.Blocker = "completion-authority-no-progress; "
                        + CaptureStageDetail(target, move);
                }
                break;
            }
            yield return null;
        }

        result.MovementEndPosition = actor.GetNowXY();
        result.GameplayProgressDelta =
            brain.CaptureRuntimeDiagnostics().Gate.GameplayProgressRevision
            - startGate.GameplayProgressRevision;
        result.ProgressDelta =
            work.ApprovedWorkProgressRevisionForDiagnostics - startWorkRevision;
        result.Progressed = result.ProgressDelta > 0;
        result.CompletionTargetRemoved = target == null
            || target.isDestroy
            || !target.gameObject.activeInHierarchy;
        result.CompletionAuthoritySatisfied =
            fault != WorkProbeFault.CompleteNormally
            || (completionAuthoritySatisfied != null
                ? completionAuthoritySatisfied()
                : result.CompletionTargetRemoved);

        if (fault == WorkProbeFault.InvalidateTargetAfterStart
            && fixture == null)
            target.gameObject.SetActive(targetWasActive);

        for (int frame = 0; frame < 2; frame++)
        {
            MaintainStableWorkSubject();
            yield return null;
        }

        CharacterAiRuntimeDiagnosticsSnapshot end = brain.CaptureRuntimeDiagnostics();
        result.EndGate = end.Gate;
        result.ActionStarts = end.Gate.ActionStarts - startGate.ActionStarts;
        result.ActionTerminals = end.Gate.ActionTerminals - startGate.ActionTerminals;
        result.Cancelled = end.Gate.ActionCancelled - startGate.ActionCancelled;
        result.Failed = end.Gate.ActionFailed - startGate.ActionFailed;
        result.PathRequests = end.Gate.PathRequests - startGate.PathRequests;
        result.PathResults = end.Gate.PathResults - startGate.PathResults;
        result.ReservationAcquires =
            end.Gate.ReservationAcquires - startGate.ReservationAcquires;
        result.ReservationReleases =
            end.Gate.ReservationReleases - startGate.ReservationReleases;
        CharacterAiRuntimeGateSnapshot endGate = end.Gate;
        result.LifecycleConserved = endGate.ConservesLifecycleFrom(in startGate);
        result.PathsConserved = endGate.ConservesPathsFrom(in startGate);
        result.ReservationsConserved = endGate.ConservesReservationsFrom(in startGate);
        result.NoInvariantAnomaly =
            endGate.InvariantAnomalies == startGate.InvariantAnomalies;
        result.TypedTerminal = fault switch
        {
            WorkProbeFault.CancelAfterApprovedProgress =>
                result.ObservedTerminalKind
                    == CharacterAiActionTerminalKind.Cancelled,
            WorkProbeFault.PauseAfterApprovedProgress =>
                result.ObservedTerminalKind
                    == CharacterAiActionTerminalKind.Cancelled,
            WorkProbeFault.CompleteNormally =>
                result.ObservedTerminalKind
                    == CharacterAiActionTerminalKind.Completed,
            _ => result.ObservedTerminalKind
                    == CharacterAiActionTerminalKind.Failed
                || result.ObservedTerminalKind
                    == CharacterAiActionTerminalKind.Cancelled
        };
        if (!result.TerminalObserved && string.IsNullOrEmpty(result.Blocker))
            result.Blocker = "typed-terminal-timeout; "
                + CaptureStageDetail(target);
        if (!result.TypedTerminal && string.IsNullOrEmpty(result.Blocker))
            result.Blocker = "expected-terminal-kind-not-observed";
        if ((!result.LifecycleConserved
             || !result.PathsConserved
             || !result.ReservationsConserved)
            && string.IsNullOrEmpty(result.Blocker))
        {
            result.Blocker = "lifecycle-path-reservation-conservation-failed";
        }
        complete(result);
    }

    private IEnumerator ObservePersistentCookPauseWindow(
        BuildableObject target,
        P15FocusedFixture focused,
        WorkPhaseResult result)
    {
        const int RequiredPausedFrames = 120;
        if (focused == null
            || gameSpeed == null
            || target == null
            || !ReferenceEquals(target, focused.Target))
        {
            result.Blocker = "persistent-cook-pause-authority-missing";
            yield break;
        }

        ProductionBillSnapshot billBeforePause = FindFocusedBill(focused);
        if (billBeforePause == null)
        {
            result.Blocker = "persistent-cook-pause-bill-missing";
            yield break;
        }

        SettlementLaborAccountingSnapshot laborBeforePause =
            settlementLabor.Capture();
        int runIdBeforePause = work.ActiveWorkRunIdForDiagnostics;
        long approvedBeforePause =
            work.ApprovedWorkProgressRevisionForDiagnostics;
        string actorIdBeforePause = actor.Identity?.PersistentId ?? string.Empty;
        bool routineBeforePause = work.HasActiveWorkRoutineForDiagnostics;
        WorkTypeId workTypeBeforePause = work.AssignedWorkTypeId;
        BuildableObject targetBeforePause = work.assignedShop;
        bool wasPaused = gameSpeed.IsPaused;
        int speedBeforePause = gameSpeed.Speed;
        float scaleBeforePause = Time.timeScale;
        if (wasPaused || runIdBeforePause <= 0 || !routineBeforePause)
        {
            result.Blocker = "persistent-cook-pause-precondition-invalid:paused="
                + wasPaused + "; runId=" + runIdBeforePause
                + "; routine=" + routineBeforePause;
            yield break;
        }

        bool pauseHeld = true;
        gameSpeed.SetPaused(true);
        result.PauseObserved = gameSpeed.IsPaused
            && Mathf.Approximately(Time.timeScale, 0f);
        try
        {
            for (int frame = 0; frame < RequiredPausedFrames; frame++)
            {
                pauseHeld &= gameSpeed.IsPaused
                    && Mathf.Approximately(Time.timeScale, 0f);
                result.PauseFrameCount++;
                yield return null;
            }
        }
        finally
        {
            gameSpeed.SetPaused(wasPaused);
            if (!wasPaused)
                Time.timeScale = scaleBeforePause;
        }

        ProductionBillSnapshot billAfterPause = FindFocusedBill(focused);
        SettlementLaborAccountingSnapshot laborAfterPause =
            settlementLabor.Capture();
        bool terminalDuringPause = brain.CaptureRuntimeDiagnostics()
            .TryGetActionTerminal(
                result.ObservedEpoch,
                out CharacterAiActionTerminalKind pauseTerminal);
        result.PauseBillUnchanged = BillPauseStateEquals(
            billBeforePause,
            billAfterPause);
        result.PauseLaborUnchanged = LaborSnapshotEquals(
            laborBeforePause,
            laborAfterPause);
        result.PauseApprovedRevisionUnchanged =
            work.ApprovedWorkProgressRevisionForDiagnostics
                == approvedBeforePause;
        result.PauseWorkIdentityUnchanged =
            string.Equals(
                actor.Identity?.PersistentId,
                actorIdBeforePause,
                StringComparison.Ordinal)
            && work.ActiveWorkRunIdForDiagnostics == runIdBeforePause
            && work.HasActiveWorkRoutineForDiagnostics == routineBeforePause
            && work.AssignedWorkTypeId == workTypeBeforePause
            && ReferenceEquals(work.assignedShop, targetBeforePause)
            && ReferenceEquals(targetBeforePause, target)
            && !terminalDuringPause;
        result.PauseObserved &= pauseHeld
            && result.PauseFrameCount == RequiredPausedFrames
            && !gameSpeed.IsPaused
            && gameSpeed.Speed == speedBeforePause
            && Mathf.Approximately(Time.timeScale, scaleBeforePause);
        result.PauseBillWorkBefore = billBeforePause.CompletedWork;
        result.PauseBillWorkAfter = billAfterPause?.CompletedWork ?? float.NaN;
        result.PauseLaborRevisionBefore = laborBeforePause.Revision;
        result.PauseLaborRevisionAfter = laborAfterPause.Revision;
        if (!result.PauseObserved
            || !result.PauseBillUnchanged
            || !result.PauseLaborUnchanged
            || !result.PauseApprovedRevisionUnchanged
            || !result.PauseWorkIdentityUnchanged)
        {
            result.Blocker = "persistent-cook-mutated-while-paused:terminal="
                + (terminalDuringPause ? pauseTerminal.ToString() : "none");
            yield break;
        }

        float resumeDeadline = Time.realtimeSinceStartup + 20f;
        while (Time.realtimeSinceStartup < resumeDeadline)
        {
            ProductionBillSnapshot currentBill = FindFocusedBill(focused);
            bool sameWork = work.ActiveWorkRunIdForDiagnostics == runIdBeforePause
                && work.HasActiveWorkRoutineForDiagnostics
                && work.AssignedWorkTypeId == BuiltInWorkTypeIds.Cook
                && ReferenceEquals(work.assignedShop, target);
            bool billAdvanced = currentBill != null
                && currentBill.CompletedWork > billAfterPause.CompletedWork;
            bool approvedAdvanced =
                work.ApprovedWorkProgressRevisionForDiagnostics
                    > approvedBeforePause;
            if (sameWork && billAdvanced && approvedAdvanced)
            {
                result.ResumedSameWorkProgress = true;
                break;
            }
            if (!sameWork
                || brain.CaptureRuntimeDiagnostics().TryGetActionTerminal(
                    result.ObservedEpoch,
                    out _))
            {
                break;
            }
            yield return null;
        }
        if (!result.ResumedSameWorkProgress)
            result.Blocker = "persistent-cook-did-not-resume-same-work";
    }

    private static bool BillPauseStateEquals(
        ProductionBillSnapshot before,
        ProductionBillSnapshot after) =>
        before != null
        && after != null
        && before.BillId == after.BillId
        && before.Status == after.Status
        && before.RemainingCycles == after.RemainingCycles
        && before.RequiredWork.Equals(after.RequiredWork)
        && before.CompletedWork.Equals(after.CompletedWork)
        && before.MaterialsConsumed == after.MaterialsConsumed
        && before.ProcessFluidConsumed == after.ProcessFluidConsumed
        && before.BatchStage == after.BatchStage
        && before.RemainingProcessingHours.Equals(
            after.RemainingProcessingHours)
        && string.Equals(
            before.ReservedWorkerId,
            after.ReservedWorkerId,
            StringComparison.Ordinal)
        && string.Equals(
            before.OccupiedSupportNodeId,
            after.OccupiedSupportNodeId,
            StringComparison.Ordinal);

    private static bool LaborSnapshotEquals(
        SettlementLaborAccountingSnapshot before,
        SettlementLaborAccountingSnapshot after) =>
        before.ActualLaborMilliWu == after.ActualLaborMilliWu
        && before.ConvertedProcessOutputMilliWu
            == after.ConvertedProcessOutputMilliWu
        && before.DomainAutomationMilliWu == after.DomainAutomationMilliWu
        && before.LossMilliWu == after.LossMilliWu
        && before.EssentialMaintenanceMilliWu
            == after.EssentialMaintenanceMilliWu
        && before.EquipmentFacilityMaintenanceMilliWu
            == after.EquipmentFacilityMaintenanceMilliWu
        && before.OutputEquivalentMilliWu == after.OutputEquivalentMilliWu
        && before.RealizedGrowthMilliWu == after.RealizedGrowthMilliWu
        && before.GuaranteedGrowthMilliWu == after.GuaranteedGrowthMilliWu
        && before.CompletedDayCount == after.CompletedDayCount
        && before.RollingPerCapitaNetWuMedian.Equals(
            after.RollingPerCapitaNetWuMedian)
        && before.LatestDay.AbsoluteDay == after.LatestDay.AbsoluteDay
        && before.LatestDay.ActualLaborMilliWu
            == after.LatestDay.ActualLaborMilliWu
        && before.LatestDay.OutputEquivalentMilliWu
            == after.LatestDay.OutputEquivalentMilliWu
        && before.LatestDay.RealizedGrowthMilliWu
            == after.LatestDay.RealizedGrowthMilliWu
        && before.LatestDay.GuaranteedGrowthMilliWu
            == after.LatestDay.GuaranteedGrowthMilliWu
        && before.LatestDay.ProductiveAdultCount
            == after.LatestDay.ProductiveAdultCount
        && before.LatestDay.PerCapitaNetWuIndex.Equals(
            after.LatestDay.PerCapitaNetWuIndex)
        && before.Revision == after.Revision;

    private float CalculateBoundedTravelObservationSeconds(AIAction action)
    {
        float worldDistance = 0f;
        Vector3 previous = actor.transform.position;
        int pathStepCount = action?.pathSteps?.Count ?? 0;
        for (int index = 0; index < pathStepCount; index++)
        {
            Vector3 next = grid.GetWorldPos(action.pathSteps[index].To);
            worldDistance += Vector3.Distance(previous, next);
            previous = next;
        }

        // Facility admission can transfer the route from AIAction to the
        // facility worker-port routine before this verifier snapshots it. Use
        // the production work-access stand as the bounded fallback instead of
        // collapsing an actively moving long route to the old fixed 8 seconds.
        if (worldDistance <= 0.001f
            && work?.assignedShop != null
            && WorkTargetSelectionRules.TryGetReachableWorkAccessPosition(
                work.assignedShop,
                grid.SearchPath(actor.GetNowXY()),
                out Vector2Int workAccess))
        {
            worldDistance = Vector3.Distance(
                actor.transform.position,
                grid.GetWorldPos(workAccess));
        }

        float gameSeconds = worldDistance / Mathf.Max(0.1f, actor.GetMoveSpeed());
        float realSeconds = gameSeconds / Mathf.Max(0.01f, Time.timeScale);
        // Door traversal, scheduling stride and one path rebuild are bounded
        // overheads. This is deliberately conservative, but never converts a
        // waypoint stall into an unbounded wait.
        float observationSeconds = 4f + realSeconds * 2f
            + pathStepCount * 0.1f;
        return Mathf.Clamp(
            observationSeconds,
            MinimumProgressObservationSeconds,
            MaximumProgressObservationSeconds);
    }

    private string CaptureStageDetail(
        BuildableObject target,
        AbilityMove move = null)
    {
        CharacterAiRuntimeDiagnosticsSnapshot aiDiagnostics =
            brain?.CaptureRuntimeDiagnostics() ?? default;
        int activeWorkRunId = work?.ActiveWorkRunIdForDiagnostics ?? 0;
        int cancelledWorkRunId =
            work?.LastCancelledWorkRunIdForDiagnostics ?? 0;
        string cancellationReason =
            work?.LastActiveWorkCancellationReasonForDiagnostics
            ?? string.Empty;
        string cancellationLatchState = string.IsNullOrWhiteSpace(
                cancellationReason)
            ? "none"
            : cancelledWorkRunId == activeWorkRunId
                ? "current"
                : "stale";
        Vector2Int actorPosition = actor != null
            ? actor.GetNowXY()
            : default;
        return "phase=" + (brain?.CurrentActionPhase ?? "-")
            + "; failure=" + (brain?.LastActionFailure.ToString() ?? "-")
            + "; actor=" + actorPosition
            + "; target=" + (target != null ? target.centerPos.ToString() : "null")
            + "; active=" + (target != null && target.gameObject.activeInHierarchy)
            + "; assigned=" + (work?.AssignedWorkTypeId.Value ?? "-")
            + "; working=" + (work?.isWorking ?? false)
            + "; movementActive="
            + (move?.HasActiveMovementRoutineForDiagnostics ?? false)
            + "; movementFailure="
            + (move?.LastGridMoveFailureReason.ToString() ?? "-")
            + "; runtimePhase=" + (brain?.CurrentRuntimePhase.ToString() ?? "-")
            + "; lifecycle=" + (actor?.CurrentLifecycleState.ToString() ?? "-")
            + "; preWuExit="
            + (work?.LastPreWuExitKindForDiagnostics.ToString() ?? "-")
            + ":"
            + (work?.LastPreWuExitDetailForDiagnostics ?? string.Empty)
            + "; workOrder="
            + (work?.LastWorkOrderExecutionDetailForDiagnostics ?? string.Empty)
            + "; workCancellation="
            + (work?.ActiveWorkCancellationCountForDiagnostics ?? 0L)
            + "/run=" + cancelledWorkRunId
            + "/activeRun=" + activeWorkRunId
            + "/latch=" + cancellationLatchState
            + "/reason=" + cancellationReason
            + "; interruptedReplan="
            + aiDiagnostics.LastInterruptedReplanDetail
            + "; environment=" + FormatSubjectEnvironment()
            + "; discontent=" + FormatSubjectDiscontent()
            + "; workAccess=" + FormatWorkAccess(target);
    }

    private string CapturePlumbingState(BuildableObject target)
    {
        if (target == null || runtimeScope?.Container == null)
            return FormatBuildingIdentity(target) + ":query-unavailable";

        IFluidInfrastructureQuery query =
            runtimeScope.Container.Resolve<IFluidInfrastructureQuery>();
        return query.TryGetMaintenance(
                target,
                out float blockage,
                out float leak)
            ? FormatBuildingIdentity(target) + ":blockage="
                + blockage.ToString("0.###") + ",leak="
                + leak.ToString("0.###")
            : FormatBuildingIdentity(target) + ":not-a-fluid-node";
    }

    private static string FormatBuildingIdentity(BuildableObject target)
    {
        if (target == null)
            return "null";

        return target.PersistentInstanceId.IsValid
            ? target.PersistentInstanceId.Value
            : target.name + "@" + target.GetInstanceID();
    }

    private static CharacterActor SelectStableWorkSubject(
        IEnumerable<CharacterActor> actors)
    {
        return actors?
            .Where(value => value != null
                && value.characterType == CharacterType.NPC
                && value.GetComponent<AbilityWork>() != null
                && value.Brain != null)
            // The matrix accelerates several game days while exercising all
            // rows. A regular employee is legitimately eligible for permanent
            // departure during that time, which contaminates every later row
            // with Despawned. The founder uses the identical production
            // Brain/AIWork/AbilityWork path but is not a staff-discontent
            // subject, so it is the stable authority for this executor matrix.
            .OrderByDescending(value => value.Role == CharacterRole.Owner)
            .ThenBy(value => value.Identity?.PersistentId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool IsMovementOrAdmissionPhase(
        CharacterAiRuntimePhase phase) =>
        phase == CharacterAiRuntimePhase.Moving
        || phase == CharacterAiRuntimePhase.Repathing
        || phase == CharacterAiRuntimePhase.FacilityAdmission;

    private string FormatWorkAccess(BuildableObject target)
    {
        if (target == null || grid == null || actor == null)
            return "unavailable";
        GridPathSearchResult search = grid.SearchPath(actor.GetNowXY());
        return WorkTargetSelectionRules.TryGetReachableWorkAccessPosition(
                target,
                search,
                out Vector2Int access)
            ? access.ToString()
            : "none;footprint=" + string.Join(",", target.buildPoses);
    }

    private void PrepareActor(WorkTypeId selected)
    {
        // The reset is an atomic verifier boundary. Pause first so neither the
        // scheduler nor a deprivation runner can observe the half-reset state
        // between raw needs, persistent mood factors and deprivation burden.
        actor.SetAiPaused(true);
        brain.StopCurrentActionForReplan("worktype live matrix row reset");
        work.ClearPriorityWorkTarget();
        work.SetDutyState(AbilityWork.DutyState.OnDuty);
        foreach (WorkTypeId id in BuiltInWorkTypeIds.All)
            work.SetWorkPriority(id, id == selected
                ? WorkPriorityLevel.Priority1
                : WorkPriorityLevel.Off);

        ResetNeutralPersistentState();
        ResetSubjectEnvironmentExposure();
        // Keep the scheduler quiescent until the next phase has explicitly
        // installed its priority target.  The running coroutine (if any) is
        // allowed to unwind while paused and RunLivePhase verifies that it did.
        brain.enabled = true;
    }

    private void ResetNeutralPersistentState()
    {
        CharacterStats stats = actor?.Stats;
        if (stats == null)
        {
            throw new InvalidOperationException(
                "WorkType live matrix cannot neutralize a subject without CharacterStats.");
        }

        Dictionary<CharacterCondition, float> restoredStats =
            stats.StatSnapshot.ToDictionary(
                pair => pair.Key,
                pair => pair.Value);
        restoredStats[CharacterCondition.HUNGER] = 100f;
        restoredStats[CharacterCondition.THIRST] = 100f;
        restoredStats[CharacterCondition.SLEEP] = 100f;
        restoredStats[CharacterCondition.FUN] = 100f;
        restoredStats[CharacterCondition.MOOD] = 100f;
        restoredStats[CharacterCondition.EXCRETION] = 100f;
        restoredStats[CharacterCondition.HYGIENE] = 100f;
        stats.RestorePersistentState(
            restoredStats,
            actor.CurrentHealth,
            actor.InjurySeverity,
            100f,
            Array.Empty<CharacterMoodFactorSnapshot>());

        if (deprivationRuntime == null
            || !deprivationRuntime.DebugResetForDeterministicScenario(actor))
        {
            throw new InvalidOperationException(
                "WorkType live matrix deprivation reset rejected the selected subject.");
        }

        float effectiveMood = CharacterMoodImpulseUtility.GetMood01(actor);
        if (effectiveMood < 0.9f)
        {
            throw new InvalidOperationException(
                "WorkType live matrix neutral mood precondition failed: effectiveMood="
                + effectiveMood.ToString("0.###") + "; rawMood="
                + (stats.StatSnapshot.TryGetValue(
                        CharacterCondition.MOOD,
                        out float rawMood)
                    ? rawMood.ToString("0.###")
                    : "missing"));
        }
    }

    private void ResetSubjectEnvironmentExposure()
    {
        if (actor?.Identity == null
            || characterEnvironmentPersistence == null)
        {
            return;
        }

        string actorId = actor.Identity.PersistentId;
        DungeonCharacterEnvironmentSaveData state =
            characterEnvironmentPersistence.Capture();
        List<CharacterEnvironmentExposure> exposures =
            (state.exposures ?? Array.Empty<CharacterEnvironmentExposure>())
            .Where(value => value != null
                && !string.Equals(
                    value.characterId,
                    actorId,
                    StringComparison.Ordinal))
            .ToList();
        exposures.Add(new CharacterEnvironmentExposure
        {
            characterId = actorId,
            coldExposure = 0f,
            heatExposure = 0f,
            airborneExposure = 0f,
            visualStrain = 0f,
            physiologicalBand = EnvironmentalExposureBand.Stable,
            visualBand = EnvironmentalExposureBand.Stable,
            criticalDamageTimer = 0f,
            coldWorkCooldownActive = false
        });
        state.exposures = exposures
            .OrderBy(value => value.characterId, StringComparer.Ordinal)
            .ToArray();
        characterEnvironmentPersistence.PublishRestoreCandidate(
            characterEnvironmentPersistence.BuildRestoreCandidate(state));
        runtimeScope?.Container.Resolve<ICharacterEnvironmentWorkContext>()
            .ClearWorkContext(new CharacterId(actorId));
    }

    private string FormatSubjectEnvironment()
    {
        if (actor?.Identity == null || characterEnvironmentStatus == null)
            return "unavailable";

        CharacterId characterId = new CharacterId(
            actor.Identity.PersistentId);
        CharacterEnvironmentExposure exposure =
            characterEnvironmentStatus.GetExposure(characterId);
        if (exposure == null)
            return "phys=Stable,visual=Stable,exposure=unpublished";
        return "phys="
            + characterEnvironmentStatus.GetPhysiologicalBand(characterId)
            + ",visual="
            + characterEnvironmentStatus.GetVisualBand(characterId)
            + ",cold=" + exposure.coldExposure.ToString("0.###")
            + ",heat=" + exposure.heatExposure.ToString("0.###")
            + ",air=" + exposure.airborneExposure.ToString("0.###")
            + ",strain=" + exposure.visualStrain.ToString("0.###");
    }

    private static string FormatAvailableActions(IReadOnlyList<AIAction> actions)
    {
        if (actions == null)
            return "null";
        if (actions.Count == 0)
            return "empty";
        return string.Join(",", actions.Select(value =>
        {
            AIActionSet actionSet = value?.actionset;
            if (actionSet == null)
                return "<null>";
            if (actionSet is AIWork workAction)
            {
                return actionSet.GetType().Name + "["
                    + (workAction.WorkTypeId.IsValid
                        ? workAction.WorkTypeId.Value
                        : "generic") + "]";
            }
            return actionSet.GetType().Name;
        }));
    }

    private static string FormatRejectedCandidate(WorkTargetCandidate candidate)
    {
        return "kind=" + candidate.FailureKind
            + ",workType=" + candidate.WorkTypeId.Value
            + ",reason=" + (candidate.FailureReason ?? string.Empty)
            + ",breakdown=" + (candidate.BreakdownSummary ?? string.Empty);
    }

    private static void SetNeutralNeeds(CharacterActor targetActor)
    {
        IDictionary<CharacterCondition, float> needs = targetActor?.stats;
        if (needs != null)
        {
            needs[CharacterCondition.HUNGER] = 100f;
            needs[CharacterCondition.THIRST] = 100f;
            needs[CharacterCondition.SLEEP] = 100f;
            needs[CharacterCondition.FUN] = 100f;
            needs[CharacterCondition.MOOD] = 100f;
            needs[CharacterCondition.EXCRETION] = 100f;
            needs[CharacterCondition.HYGIENE] = 100f;
        }
    }

    private void MaintainStableWorkSubject()
    {
        SetNeutralNeeds(actor);
        // This verifier accelerates multiple authored days to exercise twenty
        // executor rows. Staff-discontent is a separate production system and
        // must not make a later row inherit PermanentDeparture from earlier
        // rows. Reset it through its public restore authority, never by
        // mutating records or lifecycle state directly.
        staffDiscontent?.RestoreSnapshots(
            Array.Empty<StaffDiscontentSnapshot>());
    }

    private string FormatSubjectDiscontent()
    {
        if (staffDiscontent == null || actor == null)
            return "unavailable";
        string actorId = actor.Identity?.PersistentId ?? string.Empty;
        StaffDiscontentSnapshot snapshot = staffDiscontent.CaptureSnapshots()
            .FirstOrDefault(value => value != null
                && string.Equals(value.staffId, actorId, StringComparison.Ordinal));
        return snapshot == null
            ? "none"
            : snapshot.stage + "/" + snapshot.outcome
                + "/days=" + snapshot.lowMoodDays
                + "/departed=" + snapshot.departed;
    }

    private static CharacterActor[] LiveActors(ICharacterAiWorldRegistry world) =>
        world?.Characters
            .Where(value => value != null && !value.IsDead)
            .OrderBy(value => value.Identity?.PersistentId ?? string.Empty, StringComparer.Ordinal)
            .ToArray()
        ?? Array.Empty<CharacterActor>();

    private static bool IsDomainFixtureWork(WorkTypeId workTypeId) =>
        workTypeId == BuiltInWorkTypeIds.Repair
        || workTypeId == BuiltInWorkTypeIds.ThreatMitigation
        || workTypeId == BuiltInWorkTypeIds.Plumbing
        || workTypeId == BuiltInWorkTypeIds.Perform
        || workTypeId == BuiltInWorkTypeIds.AnimalCare
        || workTypeId == BuiltInWorkTypeIds.GrandProject;

    private IEnumerator PrepareDomainFixture(
        WorkTypeId workTypeId,
        Action<MaterialWorkFixture> publish,
        Action<string> fail)
    {
        if (workTypeId == BuiltInWorkTypeIds.Repair)
        {
            publish(PrepareRepairFixture(out string reason));
            if (!string.IsNullOrEmpty(reason)) fail(reason);
            yield break;
        }
        if (workTypeId == BuiltInWorkTypeIds.ThreatMitigation)
        {
            publish(PrepareThreatFixture(out string reason));
            if (!string.IsNullOrEmpty(reason)) fail(reason);
            yield return null;
            yield break;
        }
        if (workTypeId == BuiltInWorkTypeIds.Plumbing)
        {
            BuildableObject target = PreparePlumbingTarget(out string reason);
            if (target == null)
            {
                fail(reason);
                yield break;
            }

            IFluidInfrastructureQuery query =
                runtimeScope.Container.Resolve<IFluidInfrastructureQuery>();
            float deadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < deadline
                   && (!query.TryGetMaintenance(target, out float blockage, out float leak)
                       || blockage <= 0.01f && leak <= 0.01f))
            {
                yield return null;
            }
            if (!query.TryGetMaintenance(target, out float readyBlockage, out float readyLeak)
                || readyBlockage <= 0.01f && readyLeak <= 0.01f)
            {
                fail("actual wastewater backflow did not publish plumbing maintenance");
                yield break;
            }

            publish(new MaterialWorkFixture(
                target,
                $"plumbing:blockage={readyBlockage:0.###};leak={readyLeak:0.###}",
                prepareInvalidation: null,
                invalidate: (out string invalidationReason) =>
                {
                    if (target == null || target.isDestroy)
                    {
                        invalidationReason = "plumbing facility already destroyed";
                        return false;
                    }
                    target.DestroySelf();
                    invalidationReason = string.Empty;
                    return true;
                }));
            yield break;
        }

        if (workTypeId == BuiltInWorkTypeIds.Perform)
        {
            publish(PreparePerformFixture(out string reason));
            if (!string.IsNullOrEmpty(reason)) fail(reason);
            yield break;
        }

        if (workTypeId == BuiltInWorkTypeIds.AnimalCare)
        {
            IEnumerator setup = PrepareAnimalCareFixture(publish, fail);
            while (setup.MoveNext())
                yield return setup.Current;
            yield break;
        }

        if (workTypeId == BuiltInWorkTypeIds.GrandProject)
        {
            publish(PrepareGrandProjectFixture(out string reason));
            if (!string.IsNullOrEmpty(reason)) fail(reason);
            yield break;
        }

        fail("production-live domain fixture pending for " + workTypeId.Value);
    }

    private MaterialWorkFixture PreparePerformFixture(out string failureReason)
    {
        failureReason = string.Empty;
        if (!EnsurePerformanceRoom(out failureReason))
            return null;

        ICircusRuntime circus = runtimeScope.Container.Resolve<ICircusRuntime>();
        CircusProgramModule program = circus.Programs
            .Where(value => value != null
                && !value.requiresCaptive
                && !value.requiresWildlife)
            .OrderBy(value => value.programId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (program == null)
        {
            failureReason = "no authored participant-free circus program";
            return null;
        }
        if (!circus.TrySchedule(
                domainStage,
                program.programId,
                CircusLethalityPolicy.StopWhenDowned,
                Array.Empty<string>(),
                Array.Empty<string>(),
                out CircusShowOrder order,
                out failureReason))
        {
            failureReason = "circus schedule authority rejected fixture: "
                + failureReason;
            return null;
        }

        return new MaterialWorkFixture(
            domainStage,
            $"perform:order={order.orderId};program={order.programId}",
            prepareInvalidation: null,
            invalidate: (out string reason) =>
            {
                if (domainStage == null || domainStage.isDestroy)
                {
                    reason = "circus stage already destroyed";
                    return false;
                }
                domainStage.DestroySelf();
                reason = string.Empty;
                return true;
            });
    }

    private IEnumerator PrepareAnimalCareFixture(
        Action<MaterialWorkFixture> publish,
        Action<string> fail)
    {
        if (!EnsureAnimalRoom(out string roomFailure))
        {
            fail(roomFailure);
            yield break;
        }

        IWildlifeSpeciesCatalogProvider speciesCatalog =
            runtimeScope.Container.Resolve<IWildlifeSpeciesCatalogProvider>();
        IWildlifeCaptureRuntime capture =
            runtimeScope.Container.Resolve<IWildlifeCaptureRuntime>();
        IAnimalHusbandryQuery husbandry =
            runtimeScope.Container.Resolve<IAnimalHusbandryQuery>();
        IAnimalHusbandryCommand husbandryCommand =
            runtimeScope.Container.Resolve<IAnimalHusbandryCommand>();
        string speciesId = speciesCatalog.All
            .Where(value => value != null)
            .Select(value => value.SpeciesId)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (string.IsNullOrWhiteSpace(speciesId))
        {
            fail("wildlife species catalog has no authored species");
            yield break;
        }

        HashSet<Vector2Int> penFootprint = domainPen.BuildingData
            .GetGridPosList(domainPen.centerPos)
            .ToHashSet();
        GridCell domesticBirthCell = performanceRoom.Cells
            .Select(grid.GetGridCell)
            .Where(cell => cell != null
                && !penFootprint.Contains(cell.Position)
                && cell.AreaType != GridCellAreaType.BlockedExterior
                && grid.IsWalkable(cell.Position)
                && !cell.HasOccupantInLayer(GridLayer.Wildlife)
                && !cell.HasOccupantInLayer(GridLayer.Character)
                && !cell.HasOccupantInLayer(GridLayer.DownedCharacter)
                && !cell.HasOccupantInLayer(GridLayer.Building)
                && !cell.HasOccupantInLayer(GridLayer.Construction)
                && !cell.HasOccupantInLayer(GridLayer.Conveyor))
            .OrderBy(cell => Mathf.Abs(
                    cell.Position.x - domainPen.centerPos.x)
                + Mathf.Abs(cell.Position.y - domainPen.centerPos.y))
            .ThenBy(cell => cell.Position.y)
            .ThenBy(cell => cell.Position.x)
            .FirstOrDefault();
        if (domesticBirthCell == null)
        {
            fail("usable pen room has no exact lawful domestic-birth cell");
            yield break;
        }
        int wildlifeCountBefore = wildlifeRuntime.Wildlife.Count;
        int capturedCountBefore = capture.CapturedAnimals.Count;
        if (!wildlifeRuntime.TrySpawnDomesticBirth(
                speciesId,
                domesticBirthCell.Position,
                out WildlifeActor animal,
                out string spawnMessage)
            || animal == null)
        {
            fail("domestic-birth authority rejected fixture: " + spawnMessage);
            yield break;
        }
        rowScopedWildlifeIds.Add(animal.WildlifeId);
        WildlifeActor publishedBirth = grid.GetGridCell(
                animal.GridPosition)?.GetOccupant(GridLayer.Wildlife)
            as WildlifeActor;
        if (wildlifeRuntime.Wildlife.Count != wildlifeCountBefore + 1
            || !string.Equals(animal.SpeciesId, speciesId, StringComparison.Ordinal)
            || animal.GridPosition != domesticBirthCell.Position
            || publishedBirth == null
            || !string.Equals(
                publishedBirth.WildlifeId,
                animal.WildlifeId,
                StringComparison.Ordinal)
            || !string.Equals(
                publishedBirth.SpeciesId,
                speciesId,
                StringComparison.Ordinal))
        {
            fail("domestic-birth publication identity/state/layer mismatch");
            yield break;
        }
        string penId = domainPen.RequirePersistentInstanceId().Value;
        if (!capture.TryRegisterPenBorn(
                animal,
                penId,
                domesticBirthCell.Position,
                out string registerFailure))
        {
            fail("pen-born registration authority rejected fixture: "
                + registerFailure);
            yield break;
        }
        CapturedWildlifeState[] pennedMatches = capture.CapturedAnimals
            .Where(value => value != null
                && string.Equals(
                    value.wildlifeId,
                    animal.WildlifeId,
                    StringComparison.Ordinal))
            .ToArray();
        CapturedWildlifeState penned = pennedMatches.Length == 1
            ? pennedMatches[0]
            : null;
        WildlifeActor publishedPenned = grid.GetGridCell(
                domesticBirthCell.Position)?.GetOccupant(GridLayer.Wildlife)
            as WildlifeActor;
        if (capture.CapturedAnimals.Count != capturedCountBefore + 1
            || penned == null
            || !string.Equals(penned.speciesId, speciesId, StringComparison.Ordinal)
            || !string.Equals(penned.penId, penId, StringComparison.Ordinal)
            || penned.penPosition != domesticBirthCell.Position
            || penned.transportState != CapturedWildlifeTransportState.Penned
            || penned.escaped
            || !penned.isTamed
            || animal.State != WildlifeState.Captured
            || animal.GridPosition != domesticBirthCell.Position
            || publishedPenned == null
            || !string.Equals(
                publishedPenned.WildlifeId,
                animal.WildlifeId,
                StringComparison.Ordinal)
            || !string.Equals(
                publishedPenned.SpeciesId,
                speciesId,
                StringComparison.Ordinal)
            || publishedPenned.State != WildlifeState.Captured)
        {
            fail("pen-born registration identity/species/position/state/layer mismatch");
            yield break;
        }

        float deadline = Time.realtimeSinceStartup + 8f;
        WildlifeInstanceId husbandryAnimalId = new(animal.WildlifeId);
        while (Time.realtimeSinceStartup < deadline
               && !husbandry.TryGetAnimal(husbandryAnimalId, out _))
        {
            yield return null;
        }
        if (!husbandry.TryGetAnimal(
                husbandryAnimalId,
                out HusbandryAnimalState projectedAnimal)
            || !projectedAnimal.Tamed)
        {
            fail("pen-born animal did not publish the tamed husbandry projection");
            yield break;
        }
        if (!husbandryCommand.DesignateSlaughter(
                husbandryAnimalId,
                true,
                out AnimalHusbandryFailure designationFailure))
        {
            fail("husbandry slaughter designation authority rejected fixture: "
                + designationFailure.Code);
            yield break;
        }
        runtimeScope.Container.Resolve<IFacilityCandidateCache>()
            .MarkDynamicStateDirty();
        deadline = Time.realtimeSinceStartup + 8f;
        while (Time.realtimeSinceStartup < deadline
               && !husbandry.TryGetWork(domainPen, actor, out _))
        {
            yield return null;
        }
        if (!husbandry.TryGetWork(
                domainPen,
                actor,
                out AnimalHusbandryWorkSnapshot workSnapshot))
        {
            fail("captured animal did not publish husbandry work");
            yield break;
        }
        if (workSnapshot.Kind != AnimalHusbandryWorkKind.Slaughter
            || !workSnapshot.AnimalId.Equals(husbandryAnimalId))
        {
            fail("husbandry authority published the wrong work identity: kind="
                + workSnapshot.Kind + "; animal="
                + workSnapshot.AnimalId.Value);
            yield break;
        }

        // The row explicitly tests AnimalCare at the highest work priority.
        // Publish that production policy before the preflight so the selector
        // observes the same authority that Brain will use when the row starts.
        work.SetWorkPriority(
            BuiltInWorkTypeIds.AnimalCare,
            WorkPriorityLevel.Priority1);
        AnimalCareAiPreflightSnapshot preflight =
            AnimalCareAiPreflight.Capture(
                actor,
                work,
                domainPen,
                husbandry,
                runtimeScope.Container.Resolve<IWorkPolicyRegistry>(),
                runtimeScope.Container.Resolve<IFacilityCandidateCache>());
        results.Add(WorkTypeLiveRow.Info(
            "animal-care-preflight",
            "birth=" + domesticBirthCell.Position + "; penned="
            + animal.GridPosition + "; "
            + preflight.Format()));
        if (!preflight.Passed)
        {
            fail("animal-care production preflight failed: "
                + preflight.Format());
            yield break;
        }

        publish(new MaterialWorkFixture(
            domainPen,
            $"animal-care:animal={animal.WildlifeId};kind={workSnapshot.Kind}",
            prepareInvalidation: null,
            invalidate: (out string reason) =>
            {
                if (domainPen == null || domainPen.isDestroy)
                {
                    reason = "animal pen already destroyed";
                    return false;
                }
                domainPen.DestroySelf();
                reason = string.Empty;
                return true;
            }));
    }

    private MaterialWorkFixture PrepareGrandProjectFixture(
        out string failureReason)
    {
        failureReason = string.Empty;
        IGrandProjectRuntime grand =
            runtimeScope.Container.Resolve<IGrandProjectRuntime>();
        BuildableObject office = worldRegistry.Buildings
            .Where(value => value != null
                && !value.isDestroy
                && value.SupportsWork(BuiltInWorkTypeIds.GrandProject)
                && value.HasSemanticTag("grand-project-office"))
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (office == null)
        {
            BuildingSO authored = LoadAuthoredBuilding(data =>
                data.Facility?.SupportsWork(BuiltInWorkTypeIds.GrandProject) == true
                && data.HasSemanticTag("grand-project-office"));
            office = PlaceAuthoredBuilding(authored, out failureReason);
        }
        if (office == null)
            return null;

        GrandProjectDefinition definition = grand.Definitions
            .Where(value => value != null
                && grand.GetStatus(value.ProjectId, out _)
                    == GrandProjectStatus.Available)
            .OrderBy(value => value.ProjectId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (definition == null)
        {
            failureReason = "no research-unlocked grand project is available";
            return null;
        }
        if (!grand.Start(definition.ProjectId, out string startMessage))
        {
            failureReason = "grand-project authority rejected start: "
                + startMessage;
            return null;
        }

        string destinationId = grand.State.destinationId;
        foreach (IGrouping<string, ItemAmountDefinition> group in
                 definition.Requirements.GroupBy(
                     value => value.ItemId,
                     StringComparer.Ordinal))
        {
            int amount = group.Sum(value => value.Amount);
            if (!physicalItems.SpawnItemAt(
                    group.Key,
                    amount,
                    office.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    destinationId,
                    out int spawned)
                || spawned != amount)
            {
                grand.CancelActive(out _);
                failureReason = "grand-project material delivery failed for "
                    + group.Key;
                return null;
            }
        }
        if (!grand.TryGetWork(
                office.RequirePersistentInstanceId(),
                out GrandProjectWorkSnapshot workSnapshot)
            || !workSnapshot.Available)
        {
            grand.CancelActive(out _);
            failureReason = "grand-project did not publish executable work: "
                + workSnapshot.UnavailableReason;
            return null;
        }

        return new MaterialWorkFixture(
            office,
            $"grand-project:id={definition.ProjectId};destination={destinationId}",
            prepareInvalidation: null,
            invalidate: (out string reason) =>
            {
                if (office == null || office.isDestroy)
                {
                    reason = "grand-project office already destroyed";
                    return false;
                }
                office.DestroySelf();
                reason = string.Empty;
                return true;
            });
    }

    private bool EnsurePerformanceRoom(out string failureReason)
    {
        return EnsureSingleDomainRoom(
            createPerformanceStage: true,
            out failureReason);
    }

    private bool EnsureAnimalRoom(out string failureReason)
    {
        if (domainPen != null && !domainPen.isDestroy
            && performanceRoom != null && performanceRoom.IsUsable)
        {
            failureReason = string.Empty;
            return true;
        }
        return EnsureSingleDomainRoom(
            createPerformanceStage: false,
            out failureReason);
    }

    private bool EnsureSingleDomainRoom(
        bool createPerformanceStage,
        out string failureReason,
        HashSet<string> rejectedCandidates = null,
        List<string> rejectionDetails = null)
    {
        rejectedCandidates ??= new HashSet<string>(StringComparer.Ordinal);
        rejectionDetails ??= new List<string>();
        failureReason = string.Empty;
        BuildingSO hallway = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Hallway.asset");
        BuildingSO door = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/InteriorDoor.asset");
        BuildingSO wall = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Wall.asset");
        BuildingSO content = createPerformanceStage
            ? LoadAuthoredBuilding(data =>
                string.Equals(
                    data.GetAbility<BuildingFacilityPartAbility>()?.code,
                    "CS01",
                    StringComparison.Ordinal)
                && data.GetAbility<BuildingCircusStageAbility>() != null)
            : LoadAuthoredBuilding(data =>
                string.Equals(
                    data.GetAbility<BuildingFacilityPartAbility>()?.code,
                    "CB01",
                    StringComparison.Ordinal)
                && data.GetAbility<BuildingBeastPenAbility>() != null);
        BuildingSO audienceSeating = createPerformanceStage
            ? LoadAuthoredBuilding(data =>
                string.Equals(
                    data.GetAbility<BuildingFacilityPartAbility>()?.code,
                    "CS02",
                    StringComparison.Ordinal)
                && data.GetAudienceSeatingAbility()?.IsValid == true)
            : null;
        if (hallway == null || door == null || !door.IsInteriorDoor
            || door.Placement.Width != 1 || wall == null || content == null
            || createPerformanceStage && audienceSeating == null)
        {
            failureReason = "authored single-room assets invalid: hallway="
                + (hallway != null) + "; door=" + (door != null)
                + "; wall=" + (wall != null) + "; content="
                + (content != null) + "; kind="
                + (createPerformanceStage ? "CS01" : "CB01")
                + "; CS02=" + (audienceSeating != null);
            return false;
        }

        int contentWidth = Mathf.Max(1, content.Placement.Width);
        int seatingWidth = audienceSeating != null
            ? Mathf.Max(1, audienceSeating.Placement.Width)
            : 0;
        // Keep one real hallway/work-access cell between the door and the
        // domain facility.  A two-cell CB01 in a four-cell shell otherwise
        // consumes the complete room interior and publishes a usable room
        // that no worker can actually enter.
        int spanWidth = Mathf.Max(
            4,
            contentWidth + seatingWidth + 3);
        const int roomRow = 1;
        if (grid == null || grid.height <= roomRow
            || grid.width < spanWidth + 2)
        {
            failureReason = "official grid cannot host a " + spanWidth
                + "-cell horizontal "
                + (createPerformanceStage ? "CS01" : "CB01")
                + " room";
            return false;
        }

        int start = -1;
        bool selectedDoorOnLeft = true;
        Vector2Int selectedContentAnchor = default;
        Vector2Int selectedSeatingAnchor = default;
        List<Vector2Int> selectedConnector = null;
        HashSet<BuildableObject> selectedDisplacements = null;
        HashSet<WildlifeActor> selectedWildlife = null;
        string selectedCandidateKey = string.Empty;
        string lastRejected = "none";
        GridPathSearchResult preMutationSearch = grid.SearchPath(actor.GetNowXY());
        for (int x = 1; x <= grid.width - spanWidth - 1 && start < 0; x++)
        {
            bool free = true;
            bool hasExterior = false;
            HashSet<BuildableObject> candidates = new();
            HashSet<WildlifeActor> wildlifeCandidates = new();
            // RoomDetector and grid movement both use left/right adjacency.
            // Reserving y=0 and y=2 made this one-dimensional room fixture
            // depend on unrelated outdoor resource hosts in those rows.
            // Materialize only the authored horizontal room domain at y=1.
            for (int y = roomRow; y <= roomRow && free; y++)
            {
                for (int offset = 0; offset < spanWidth; offset++)
                {
                    Vector2Int position = new(x + offset, y);
                    GridCell cell = grid.GetGridCell(position);
                    if (cell == null)
                    {
                        free = false;
                        lastRejected = "missing:" + position;
                        break;
                    }
                    hasExterior |= cell.AreaType
                        != GridCellAreaType.DungeonInterior;
                    foreach (GridLayer layer in Enum.GetValues(typeof(GridLayer)))
                    {
                        IGridOccupant occupant = cell.GetOccupant(layer);
                        if (occupant == null)
                            continue;
                        if (layer == GridLayer.Wildlife
                            && occupant is WildlifeActor wildlife
                            && wildlife.IsAlive
                            && wildlife.State != WildlifeState.Captured)
                        {
                            wildlifeCandidates.Add(wildlife);
                            continue;
                        }
                        if ((layer != GridLayer.Building
                                && layer != GridLayer.Hallway)
                            || occupant is not BuildableObject movement
                            || movement is Facility
                            || movement is Door
                            || movement.Facility != null
                            || !movement.IsGridMovement
                            || movement.BlocksGridMovement)
                        {
                            free = false;
                            lastRejected = "hard-occupant:" + position
                                + ":" + layer + ":"
                                + occupant.GetType().Name;
                            break;
                        }
                        candidates.Add(movement);
                    }
                    if (!free)
                        break;
                }
            }
            if (free && hasExterior)
            {
                Vector2Int leftOutside = new(x - 1, 1);
                Vector2Int rightOutside = new(x + spanWidth, 1);
                bool leftConnected = TryPlanRoomConnector(
                    x,
                    spanWidth,
                    leftOutside,
                    preMutationSearch,
                    out List<Vector2Int> leftConnector,
                    out HashSet<BuildableObject> leftDisplacements,
                    out Vector2Int leftExternalStand,
                    out string leftFailure);
                bool rightConnected = TryPlanRoomConnector(
                    x,
                    spanWidth,
                    rightOutside,
                    preMutationSearch,
                    out List<Vector2Int> rightConnector,
                    out HashSet<BuildableObject> rightDisplacements,
                    out Vector2Int rightExternalStand,
                    out string rightFailure);
                if (!leftConnected && !rightConnected)
                {
                    lastRejected = "isolated-door:" + leftOutside + "="
                        + leftFailure + "/" + rightOutside + "="
                        + rightFailure;
                    continue;
                }
                bool preferLeft = leftConnected && (!rightConnected
                    || leftConnector.Count <= rightConnector.Count);
                bool[] doorOptions = preferLeft
                    ? new[] { true, false }
                    : new[] { false, true };
                foreach (bool doorOnLeft in doorOptions)
                {
                    if (doorOnLeft ? !leftConnected : !rightConnected)
                        continue;
                    if (!TryResolveRoomContentAnchor(
                            content,
                            audienceSeating,
                            x,
                            spanWidth,
                            doorOnLeft,
                            out Vector2Int contentAnchor,
                            out Vector2Int workStand,
                            out Vector2Int seatingAnchor))
                    {
                        lastRejected = "no-horizontal-work-stand:"
                            + x + ":" + (doorOnLeft ? "L" : "R");
                        continue;
                    }
                    string candidateKey = x + ":"
                        + (doorOnLeft ? "L" : "R") + ":"
                        + contentAnchor.x + ":stand=" + workStand.x
                        + ":seat=" + (audienceSeating != null
                            ? seatingAnchor.x.ToString()
                            : "none");
                    if (rejectedCandidates.Contains(candidateKey))
                        continue;
                    start = x;
                    selectedDoorOnLeft = doorOnLeft;
                    selectedContentAnchor = contentAnchor;
                    selectedSeatingAnchor = seatingAnchor;
                    selectedConnector = doorOnLeft
                        ? leftConnector
                        : rightConnector;
                    roomExternalExitStand = doorOnLeft
                        ? leftExternalStand
                        : rightExternalStand;
                    roomBaselineReachableSentinel = actor.GetNowXY();
                    selectedDisplacements = new HashSet<BuildableObject>(
                        candidates);
                    selectedDisplacements.UnionWith(
                        doorOnLeft
                            ? leftDisplacements
                            : rightDisplacements);
                    selectedWildlife = wildlifeCandidates;
                    WildlifeActor exitWildlife = grid.GetGridCell(
                            roomExternalExitStand.Value)
                        ?.GetOccupant(GridLayer.Wildlife) as WildlifeActor;
                    if (exitWildlife != null
                        && exitWildlife.IsAlive
                        && exitWildlife.State != WildlifeState.Captured)
                    {
                        selectedWildlife.Add(exitWildlife);
                    }
                    selectedCandidateKey = candidateKey;
                    break;
                }
            }
        }
        if (start < 0)
        {
            failureReason = "no safe " + spanWidth
                + "-cell horizontal exterior "
                + (createPerformanceStage ? "CS01" : "CB01")
                + " room span; last=" + lastRejected
                + (rejectionDetails.Count == 0
                    ? string.Empty
                    : "; rejected=" + string.Join(
                        " || ",
                        rejectionDetails.TakeLast(6)));
            return false;
        }

        HashSet<Vector2Int> reservedRoomCells = new(
            selectedConnector ?? Enumerable.Empty<Vector2Int>());
        if (roomExternalExitStand.HasValue)
            reservedRoomCells.Add(roomExternalExitStand.Value);
        for (int offset = 0; offset < spanWidth; offset++)
            reservedRoomCells.Add(new Vector2Int(start + offset, roomRow));
        if (!TryDisplaceRoomWildlife(
                selectedWildlife,
                reservedRoomCells,
                out failureReason))
        {
            return false;
        }

        foreach (Vector2Int position in selectedConnector)
        {
            GridCell cell = grid.GetGridCell(position);
            roomAreaSnapshots.Add(
                new FixtureAreaSnapshot(position, cell.AreaType));
            grid.SetAreaType(position, GridCellAreaType.DungeonInterior);
            if (cell.AreaType != GridCellAreaType.DungeonInterior)
            {
                failureReason = "single-room connector area mutation failed at "
                    + position;
                return false;
            }
        }

        for (int y = roomRow; y <= roomRow; y++)
        {
            for (int offset = 0; offset < spanWidth; offset++)
            {
                Vector2Int position = new(start + offset, y);
                GridCell cell = grid.GetGridCell(position);
                roomAreaSnapshots.Add(
                    new FixtureAreaSnapshot(position, cell.AreaType));
                grid.SetAreaType(position, GridCellAreaType.DungeonInterior);
                if (cell.AreaType != GridCellAreaType.DungeonInterior)
                {
                    failureReason = "single-room area mutation failed at "
                        + position;
                    return false;
                }
            }
        }
        foreach (BuildableObject movement in selectedDisplacements
                     .OrderBy(value => value.centerPos.y)
                     .ThenBy(value => value.centerPos.x)
                     .ThenBy(value => value.GridId))
        {
            GridLayer layer = movement.BuildingData.Placement.Layer;
            Vector2Int[] positions = movement.buildPoses.ToArray();
            if (!grid.RemoveOccupant(
                    movement,
                    layer,
                    positions,
                    movement.BuildingData.Placement.IsMovement))
            {
                failureReason = "single-room movement displacement failed: "
                    + movement.GridId;
                return false;
            }
            displacedRoomMovements.Add(new DisplacedMovementSnapshot(
                movement,
                layer,
                positions,
                movement.BuildingData.Placement.IsMovement));
        }

        foreach (Vector2Int position in selectedConnector)
        {
            if (grid.GetGridCell(position)?.GetOccupant(GridLayer.Hallway) == null
                && PlaceRoomFixture(
                    hallway,
                    position,
                    out failureReason) == null)
            {
                return false;
            }
        }

        if (PlaceRoomFixture(
                door,
                new Vector2Int(
                    selectedDoorOnLeft ? start : start + spanWidth - 1,
                    1),
                out failureReason) == null
            || PlaceRoomFixture(
                wall,
                new Vector2Int(
                    selectedDoorOnLeft ? start + spanWidth - 1 : start,
                    1),
                out failureReason) == null)
            return false;
        for (int offset = 1; offset < spanWidth - 1; offset++)
        {
            Vector2Int position = new(start + offset, 1);
            if (grid.GetGridCell(position)?.GetOccupant(GridLayer.Hallway) == null
                && PlaceRoomFixture(
                    hallway,
                    position,
                    out failureReason) == null)
                return false;
        }

        BuildableObject domainContent = PlaceRoomFixture(
            content,
            selectedContentAnchor,
            out failureReason);
        if (domainContent == null)
            return false;
        BuildableObject domainSeating = null;
        if (audienceSeating != null)
        {
            domainSeating = PlaceRoomFixture(
                audienceSeating,
                selectedSeatingAnchor,
                out failureReason);
            if (domainSeating == null)
                return false;
        }
        if (createPerformanceStage)
            domainStage = domainContent;
        else
            domainPen = domainContent;

        IRoomLayoutCache rooms =
            runtimeScope.Container.Resolve<IRoomLayoutCache>();
        rooms.Clear();
        if (!rooms.TryGetRoom(domainContent, out RoomInstance room)
            || !room.IsUsable)
        {
            string candidateFailure = room == null
                ? "single domain room was not published"
                : "single domain room unusable: cells=" + room.Cells.Count
                  + "; doors=" + room.Doors.Count
                  + "; walls=" + room.Walls.Count
                  + "; open=" + room.OpenBoundaryCount
                  + "; solid=" + room.SolidBoundaryCount
                  + "; closed=" + room.IsClosed
                  + "; hasDoor=" + room.HasDoor;
            return RetrySingleDomainRoomCandidate(
                createPerformanceStage,
                selectedCandidateKey,
                candidateFailure,
                rejectedCandidates,
                rejectionDetails,
                out failureReason);
        }
        GridPathSearchResult search = grid.SearchPath(actor.GetNowXY());
        if (!WorkTargetSelectionRules.IsReachable(domainContent, search))
        {
            string candidateFailure =
                "usable single domain room lacks production work access; "
                + "roomCells=" + room.Cells.Count
                + "; roomDoors=" + room.Doors.Count
                + "; closed=" + room.IsClosed
                + "; hasDoor=" + room.HasDoor + "; "
                + FormatRoomAccessDiagnostics(
                    domainContent,
                    new Vector2Int(
                        selectedDoorOnLeft
                            ? start
                            : start + spanWidth - 1,
                        1),
                    search);
            return RetrySingleDomainRoomCandidate(
                createPerformanceStage,
                selectedCandidateKey,
                candidateFailure,
                rejectedCandidates,
                rejectionDetails,
                out failureReason);
        }
        performanceRoom = room;
        results.Add(WorkTypeLiveRow.Info(
            createPerformanceStage
                ? "performance-room-fixture"
                : "animal-room-fixture",
            "bounds=" + start + "," + roomRow + ".."
                + (start + spanWidth - 1) + "," + roomRow
                + "; cells=" + room.Cells.Count
                + "; doors=" + room.Doors.Count
                + "; doorSide=" + (selectedDoorOnLeft ? "left" : "right")
                + "; connectorCells=" + selectedConnector.Count
                + "; displacedWildlife=" + displacedRoomWildlife.Count
                + "; content="
                + domainContent.RequirePersistentInstanceId().Value
                + "; seating=" + (domainSeating != null
                    ? domainSeating.RequirePersistentInstanceId().Value
                    : "none")));
        return true;
    }

    private static bool TryResolveRoomContentAnchor(
        BuildingSO content,
        BuildingSO seating,
        int roomStart,
        int roomWidth,
        bool doorOnLeft,
        out Vector2Int anchor,
        out Vector2Int workStand,
        out Vector2Int seatingAnchor)
    {
        anchor = default;
        workStand = default;
        seatingAnchor = default;
        int interiorMinimum = roomStart + 1;
        int interiorMaximum = roomStart + roomWidth - 2;
        var contentCandidates = Enumerable.Range(
                interiorMinimum,
                interiorMaximum - interiorMinimum + 1)
            .Select(x => new Vector2Int(x, 1))
            .Select(candidate => new
            {
                Anchor = candidate,
                Footprint = content.GetGridPosList(candidate).ToArray()
            })
            .Where(candidate => candidate.Footprint.Length > 0
                && candidate.Footprint.All(position =>
                    position.y == 1
                    && position.x >= interiorMinimum
                    && position.x <= interiorMaximum))
            .Select(candidate => new
            {
                candidate.Anchor,
                candidate.Footprint,
                MinimumX = candidate.Footprint.Min(position => position.x),
                MaximumX = candidate.Footprint.Max(position => position.x)
            })
            .Select(candidate => new
            {
                candidate.Anchor,
                candidate.MinimumX,
                candidate.MaximumX,
                Stand = new Vector2Int(
                    doorOnLeft
                        ? candidate.MinimumX - 1
                        : candidate.MaximumX + 1,
                    1)
            })
            .Where(candidate => candidate.Stand.x >= interiorMinimum
                && candidate.Stand.x <= interiorMaximum)
            .ToArray();
        if (contentCandidates.Length == 0)
            return false;

        if (seating == null)
        {
            var selected = contentCandidates
                .OrderBy(candidate => doorOnLeft
                    ? -candidate.MaximumX
                    : candidate.MinimumX)
                .ThenBy(candidate => candidate.Anchor.x)
                .First();
            anchor = selected.Anchor;
            workStand = selected.Stand;
            return true;
        }

        var seatingCandidates = Enumerable.Range(
                interiorMinimum,
                interiorMaximum - interiorMinimum + 1)
            .Select(x => new Vector2Int(x, 1))
            .Select(candidate => new
            {
                Anchor = candidate,
                Footprint = seating.GetGridPosList(candidate).ToArray()
            })
            .Where(candidate => candidate.Footprint.Length > 0
                && candidate.Footprint.All(position =>
                    position.y == 1
                    && position.x >= interiorMinimum
                    && position.x <= interiorMaximum))
            .Select(candidate => new
            {
                candidate.Anchor,
                MinimumX = candidate.Footprint.Min(position => position.x),
                MaximumX = candidate.Footprint.Max(position => position.x)
            })
            .ToArray();
        var layouts = contentCandidates
            .SelectMany(contentCandidate => seatingCandidates
                .Where(seatingCandidate => doorOnLeft
                    ? seatingCandidate.MinimumX > contentCandidate.MaximumX
                    : seatingCandidate.MaximumX < contentCandidate.MinimumX)
                .Select(seatingCandidate => new
                {
                    Content = contentCandidate,
                    Seating = seatingCandidate
                }))
            .OrderBy(layout => doorOnLeft
                ? layout.Content.MinimumX
                : -layout.Content.MaximumX)
            .ThenBy(layout => doorOnLeft
                ? -layout.Seating.MaximumX
                : layout.Seating.MinimumX)
            .ThenBy(layout => layout.Content.Anchor.x)
            .ToArray();
        if (layouts.Length == 0)
            return false;
        anchor = layouts[0].Content.Anchor;
        workStand = layouts[0].Content.Stand;
        seatingAnchor = layouts[0].Seating.Anchor;
        return true;
    }

    private bool RetrySingleDomainRoomCandidate(
        bool createPerformanceStage,
        string candidateKey,
        string candidateFailure,
        HashSet<string> rejectedCandidates,
        List<string> rejectionDetails,
        out string failureReason)
    {
        rejectedCandidates.Add(candidateKey);
        rejectionDetails.Add(candidateKey + "=" + candidateFailure);
        if (!CleanupRoomFixture(out string cleanupFailure))
        {
            failureReason = candidateFailure
                + "; rollback=" + cleanupFailure;
            return false;
        }
        return EnsureSingleDomainRoom(
            createPerformanceStage,
            out failureReason,
            rejectedCandidates,
            rejectionDetails);
    }

    private string FormatRoomAccessDiagnostics(
        BuildableObject content,
        Vector2Int doorPosition,
        GridPathSearchResult search)
    {
        IReadOnlyList<Vector2Int> footprint =
            content.BuildingData.GetGridPosList(content.centerPos);
        HashSet<Vector2Int> footprintSet = footprint.ToHashSet();
        Vector2Int[] directions =
        {
            Vector2Int.left,
            Vector2Int.right
        };
        string[] stands = footprint
            .SelectMany(position => directions.Select(direction =>
                position + direction))
            .Where(position => !footprintSet.Contains(position))
            .Distinct()
            .OrderBy(position => position.y)
            .ThenBy(position => position.x)
            .Select(position => FormatRoomCell(position, search))
            .ToArray();
        return "actor=" + actor.GetNowXY()
            + "; door=" + FormatRoomCell(doorPosition, search)
            + "; content=" + string.Join(",", footprint)
            + "; stands=" + string.Join(" | ", stands);
    }

    private string FormatRoomCell(
        Vector2Int position,
        GridPathSearchResult search)
    {
        GridCell cell = grid.GetGridCell(position);
        if (cell == null)
            return position + "[missing]";
        string occupants = string.Join(",", cell.GetAllOccupants()
            .Where(value => value != null)
            .Select(value => value.GetType().Name));
        return position + "[area=" + cell.AreaType
            + ",walk=" + grid.IsWalkable(position)
            + ",reach=" + search.ContainsPosition(position)
            + ",occupants=" + occupants + "]";
    }

    private bool TryDisplaceRoomWildlife(
        IEnumerable<WildlifeActor> wildlife,
        ISet<Vector2Int> reservedCells,
        out string failureReason)
    {
        failureReason = string.Empty;
        foreach (WildlifeActor animal in (wildlife
                     ?? Enumerable.Empty<WildlifeActor>())
                 .Where(value => value != null)
                 .OrderBy(value => value.WildlifeId, StringComparer.Ordinal))
        {
            Vector2Int origin = animal.GridPosition;
            GridCell destination = grid.GetCells()
                .Where(cell => cell != null
                    && !reservedCells.Contains(cell.Position)
                    && Mathf.Abs(cell.Position.x - origin.x)
                        + Mathf.Abs(cell.Position.y - origin.y) <= 12
                    && CanPlaceWildlifeAt(
                        grid,
                        cell.Position,
                        animal.CanEnterDungeon))
                .OrderBy(cell => Mathf.Abs(cell.Position.x - origin.x)
                    + Mathf.Abs(cell.Position.y - origin.y))
                .ThenBy(cell => cell.Position.y)
                .ThenBy(cell => cell.Position.x)
                .FirstOrDefault();
            if (destination == null)
            {
                failureReason = "no bounded lawful wildlife displacement for "
                    + animal.WildlifeId + " at " + origin;
                return false;
            }

            displacedRoomWildlife.Add(new DisplacedWildlifeSnapshot(
                animal,
                origin,
                destination.Position));
            animal.WarpTo(destination.Position);
            if (animal.GridPosition != destination.Position
                || grid.GetGridCell(destination.Position)?.ContainsOccupant(
                    GridLayer.Wildlife,
                    animal) != true)
            {
                failureReason = "wildlife displacement authority failed for "
                    + animal.WildlifeId + ":" + origin + "->"
                    + destination.Position;
                return false;
            }
        }
        return true;
    }

    private static bool CanPlaceWildlifeAt(
        Grid targetGrid,
        Vector2Int position,
        bool canEnterDungeon)
    {
        GridCell cell = targetGrid?.GetGridCell(position);
        if (cell == null
            || !targetGrid.IsWalkable(position)
            || cell.HasOccupantInLayer(GridLayer.Wildlife)
            || cell.AreaType == GridCellAreaType.BlockedExterior)
        {
            return false;
        }

        if (cell.AreaType == GridCellAreaType.ExteriorPath
            && !WildlifeRuntime.IsOutdoorSurfaceCell(targetGrid, cell))
        {
            return false;
        }

        return canEnterDungeon
            || cell.AreaType != GridCellAreaType.DungeonInterior;
    }

    private bool TryPlanRoomConnector(
        int roomStart,
        int roomWidth,
        Vector2Int outsideDoor,
        GridPathSearchResult existingReachability,
        out List<Vector2Int> connector,
        out HashSet<BuildableObject> displacements,
        out Vector2Int externalStand,
        out string failureReason)
    {
        connector = new List<Vector2Int>();
        displacements = new HashSet<BuildableObject>();
        externalStand = default;
        failureReason = string.Empty;
        if (existingReachability.ContainsPosition(outsideDoor))
        {
            externalStand = outsideDoor;
            return true;
        }

        bool IsRoomCell(Vector2Int position) =>
            position.x >= roomStart
            && position.x < roomStart + roomWidth
            && position.y == outsideDoor.y;

        bool TryGetMutableCell(
            Vector2Int position,
            out HashSet<BuildableObject> movements)
        {
            movements = new HashSet<BuildableObject>();
            if (position.x < 0 || position.x >= grid.width
                || position.y < 0 || position.y >= grid.height
                || position.y != outsideDoor.y
                || IsRoomCell(position))
            {
                return false;
            }
            GridCell cell = grid.GetGridCell(position);
            if (cell == null)
                return false;
            foreach (GridLayer layer in Enum.GetValues(typeof(GridLayer)))
            {
                IGridOccupant occupant = cell.GetOccupant(layer);
                if (occupant == null)
                    continue;
                if ((layer != GridLayer.Building
                        && layer != GridLayer.Hallway)
                    || occupant is not BuildableObject movement
                    || movement is Facility
                    || movement is Door
                    || movement.Facility != null
                    || !movement.IsGridMovement
                    || movement.BlocksGridMovement)
                {
                    return false;
                }
                movements.Add(movement);
            }
            return true;
        }

        if (!TryGetMutableCell(outsideDoor, out _))
        {
            failureReason = "door-outside-not-mutable";
            return false;
        }

        Queue<Vector2Int> open = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> previous =
            new Dictionary<Vector2Int, Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>
        {
            outsideDoor
        };
        open.Enqueue(outsideDoor);
        Vector2Int[] directions =
        {
            Vector2Int.left,
            Vector2Int.right
        };
        Vector2Int lastMutable = default;
        bool connected = false;
        while (open.Count > 0 && !connected)
        {
            Vector2Int current = open.Dequeue();
            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;
                if (next.x < 0 || next.x >= grid.width
                    || next.y < 0 || next.y >= grid.height
                    || IsRoomCell(next)
                    || visited.Contains(next))
                    continue;
                if (existingReachability.ContainsPosition(next))
                {
                    lastMutable = current;
                    externalStand = next;
                    connected = true;
                    break;
                }
                if (!TryGetMutableCell(next, out _))
                    continue;
                visited.Add(next);
                previous[next] = current;
                open.Enqueue(next);
            }
        }
        if (!connected)
        {
            failureReason = "no-safe-corridor-to-reachable-cell";
            return false;
        }

        Vector2Int cursor = lastMutable;
        connector.Add(cursor);
        while (cursor != outsideDoor)
        {
            cursor = previous[cursor];
            connector.Add(cursor);
        }
        connector.Reverse();
        foreach (Vector2Int position in connector)
        {
            if (!TryGetMutableCell(position, out HashSet<BuildableObject> movements))
            {
                failureReason = "connector-became-invalid:" + position;
                return false;
            }
            displacements.UnionWith(movements);
        }
        return true;
    }

    private bool EnsurePerformanceAndAnimalRoomFixture(out string failureReason)
    {
        failureReason = string.Empty;
        if (performanceRoom != null && performanceRoom.IsUsable
            && domainStage != null && !domainStage.isDestroy
            && domainPen != null && !domainPen.isDestroy)
        {
            return true;
        }
        if (roomFixtureBuildings.Count > 0)
        {
            failureReason = "performance room fixture was partially invalidated";
            return false;
        }

        BuildingSO hallway = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Hallway.asset");
        BuildingSO door = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/InteriorDoor.asset");
        BuildingSO wall = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Wall.asset");
        BuildingSO stage = LoadAuthoredBuilding(data =>
            string.Equals(
                data.GetAbility<BuildingFacilityPartAbility>()?.code,
                "CS01",
                StringComparison.Ordinal)
            && data.GetAbility<BuildingCircusStageAbility>() != null);
        BuildingSO pen = LoadAuthoredBuilding(data =>
            data.GetAbility<BuildingBeastPenAbility>() != null);
        if (hallway == null || door == null || !door.IsInteriorDoor
            || door.Placement.Width != 1 || wall == null || stage == null
            || pen == null || stage.Placement.Width > 2)
        {
            failureReason = "authored performance room assets invalid: hallway="
                + (hallway != null) + "; door=" + (door != null)
                + "; wall=" + (wall != null) + "; CS01=" + (stage != null)
                + "; pen=" + (pen != null);
            return false;
        }
        if (grid == null || grid.height < 3 || grid.width < 10)
        {
            failureReason = "official grid cannot host an 8x3 performance room";
            return false;
        }

        int start = -1;
        HashSet<BuildableObject> selectedDisplacements = null;
        string lastRejected = "none";
        for (int x = 1; x <= grid.width - 9 && start < 0; x++)
        {
            bool free = true;
            bool hasExterior = false;
            HashSet<BuildableObject> candidates = new();
            for (int y = 0; y < 3 && free; y++)
            {
                for (int offset = 0; offset < 8; offset++)
                {
                    Vector2Int position = new(x + offset, y);
                    GridCell cell = grid.GetGridCell(position);
                    if (cell == null)
                    {
                        free = false;
                        lastRejected = "missing:" + position;
                        break;
                    }
                    hasExterior |= cell.AreaType != GridCellAreaType.DungeonInterior;
                    foreach (GridLayer layer in Enum.GetValues(typeof(GridLayer)))
                    {
                        IGridOccupant occupant = cell.GetOccupant(layer);
                        if (occupant == null)
                            continue;
                        if ((layer != GridLayer.Building && layer != GridLayer.Hallway)
                            || occupant is not BuildableObject movement
                            || movement is Facility
                            || movement is Door
                            || movement.Facility != null
                            || !movement.IsGridMovement
                            || movement.BlocksGridMovement)
                        {
                            free = false;
                            lastRejected = "hard-occupant:" + position
                                + ":" + layer + ":" + occupant.GetType().Name;
                            break;
                        }
                        candidates.Add(movement);
                    }
                    if (!free)
                        break;
                }
            }
            if (free && hasExterior)
            {
                start = x;
                selectedDisplacements = candidates;
            }
        }
        if (start < 0)
        {
            failureReason = "no safe 8x3 exterior performance-room span; last="
                + lastRejected;
            return false;
        }

        for (int y = 0; y < 3; y++)
        {
            for (int offset = 0; offset < 8; offset++)
            {
                Vector2Int position = new(start + offset, y);
                GridCell cell = grid.GetGridCell(position);
                roomAreaSnapshots.Add(new FixtureAreaSnapshot(position, cell.AreaType));
                grid.SetAreaType(position, GridCellAreaType.DungeonInterior);
                if (cell.AreaType != GridCellAreaType.DungeonInterior)
                {
                    failureReason = "performance room area mutation failed at " + position;
                    return false;
                }
            }
        }
        foreach (BuildableObject movement in selectedDisplacements
                     .OrderBy(value => value.centerPos.y)
                     .ThenBy(value => value.centerPos.x)
                     .ThenBy(value => value.GridId))
        {
            GridLayer layer = movement.BuildingData.Placement.Layer;
            Vector2Int[] positions = movement.buildPoses.ToArray();
            if (!grid.RemoveOccupant(
                    movement,
                    layer,
                    positions,
                    movement.BuildingData.Placement.IsMovement))
            {
                failureReason = "performance room movement displacement failed: "
                    + movement.GridId;
                return false;
            }
            displacedRoomMovements.Add(new DisplacedMovementSnapshot(
                movement,
                layer,
                positions,
                movement.BuildingData.Placement.IsMovement));
        }

        for (int offset = 0; offset < 8; offset++)
        {
            if (PlaceRoomFixture(wall, new Vector2Int(start + offset, 0), out failureReason) == null
                || PlaceRoomFixture(wall, new Vector2Int(start + offset, 2), out failureReason) == null)
                return false;
        }
        if (PlaceRoomFixture(door, new Vector2Int(start, 1), out failureReason) == null
            || PlaceRoomFixture(wall, new Vector2Int(start + 7, 1), out failureReason) == null)
            return false;
        for (int offset = 1; offset < 7; offset++)
        {
            Vector2Int position = new(start + offset, 1);
            if (grid.GetGridCell(position)?.GetOccupant(GridLayer.Hallway) == null
                && PlaceRoomFixture(hallway, position, out failureReason) == null)
                return false;
        }

        domainPen = PlaceRoomFixture(
            pen,
            new Vector2Int(start + 1, 1),
            out failureReason);
        domainStage = PlaceRoomFixture(
            stage,
            new Vector2Int(start + 3, 1),
            out failureReason);
        if (domainPen == null || domainStage == null)
            return false;

        IRoomLayoutCache rooms = runtimeScope.Container.Resolve<IRoomLayoutCache>();
        rooms.Clear();
        if (!rooms.TryGetRoom(domainStage, out RoomInstance room)
            || !room.IsUsable
            || !rooms.TryGetRoom(domainPen, out RoomInstance penRoom)
            || !ReferenceEquals(room, penRoom))
        {
            failureReason = room == null
                ? "CS01 room was not published"
                : "CS01 room unusable: cells=" + room.Cells.Count
                  + "; doors=" + room.Doors.Count
                  + "; walls=" + room.Walls.Count
                  + "; open=" + room.OpenBoundaryCount
                  + "; solid=" + room.SolidBoundaryCount
                  + "; closed=" + room.IsClosed
                  + "; hasDoor=" + room.HasDoor;
            return false;
        }
        GridPathSearchResult search = grid.SearchPath(actor.GetNowXY());
        if (!WorkTargetSelectionRules.IsReachable(domainStage, search)
            || !WorkTargetSelectionRules.IsReachable(domainPen, search))
        {
            failureReason = "usable performance room lacks production work access";
            return false;
        }
        performanceRoom = room;
        results.Add(WorkTypeLiveRow.Info(
            "performance-room-fixture",
            "bounds=" + start + ",0.." + (start + 7) + ",2"
            + "; cells=" + room.Cells.Count + "; doors=" + room.Doors.Count
            + "; stage=" + domainStage.RequirePersistentInstanceId().Value
            + "; pen=" + domainPen.RequirePersistentInstanceId().Value));
        return true;
    }

    private BuildableObject PlaceRoomFixture(
        BuildingSO data,
        Vector2Int position,
        out string failureReason)
    {
        BuildableObject building = PlaceAuthoredBuildingAt(
            data,
            position,
            out failureReason);
        if (building != null)
            roomFixtureBuildings.Add(building);
        return building;
    }

    private bool EnsureFixtureResearchPrerequisites(
        IObjectResolver resolver,
        IReadOnlyList<string> requiredResearchIds,
        bool includeGrandProject,
        out string detail)
    {
        detail = string.Empty;
        if (baseline?.sections == null || baseline.sections.Count == 0)
        {
            detail = "save baseline was not captured before fixture research promotion";
            return false;
        }

        IResearchProjectCatalog catalog =
            resolver.Resolve<IResearchProjectCatalog>();
        HashSet<string> closure = new(StringComparer.Ordinal);
        foreach (string projectId in requiredResearchIds)
        {
            if (!catalog.TryGet(
                    new ResearchProjectId(projectId),
                    out ResearchProjectSO project))
            {
                detail = "authored research prerequisite missing: " + projectId;
                return false;
            }
            AddResearchClosure(project, closure);
        }

        if (includeGrandProject)
        {
            IGrandProjectRuntime grand = resolver.Resolve<IGrandProjectRuntime>();
            GrandProjectDefinition grandDefinition = grand.Definitions
                .Where(value => value != null
                    && !string.IsNullOrWhiteSpace(value.RequiredResearchId))
                .OrderBy(value => value.ProjectId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (grandDefinition != null
                && catalog.TryGet(
                    new ResearchProjectId(grandDefinition.RequiredResearchId),
                    out ResearchProjectSO grandProject))
            {
                AddResearchClosure(grandProject, closure);
            }
        }

        DungeonGameSaveData fixtureSave = gameSaveService.FromJson(
            gameSaveService.ToJson(baseline));
        DungeonSaveSectionEnvelope researchEnvelope = fixtureSave.sections.FirstOrDefault(
            value => string.Equals(
                value.sectionId,
                BlueprintResearchSaveSection.Id,
                StringComparison.Ordinal));
        if (researchEnvelope == null)
        {
            detail = "official save registry has no research.blueprints section";
            return false;
        }

        DungeonResearchSaveData research = JsonUtility.FromJson<
            DungeonResearchSaveData>(researchEnvelope.payloadJson);
        if (research == null)
        {
            detail = "research.blueprints baseline payload is invalid";
            return false;
        }
        research.completedProjectIds ??= new List<string>();
        research.projectProgress ??=
            new List<DungeonResearchProjectProgressSaveData>();
        research.projectQueue ??= new List<DungeonResearchQueueEntrySaveData>();
        foreach (string projectId in closure.OrderBy(
                     value => value,
                     StringComparer.Ordinal))
        {
            research.completedProjectIds.RemoveAll(value => string.Equals(
                value,
                projectId,
                StringComparison.Ordinal));
            research.completedProjectIds.Add(projectId);
            research.projectProgress.RemoveAll(value => value != null
                && string.Equals(
                    value.projectId,
                    projectId,
                    StringComparison.Ordinal));
            research.projectQueue.RemoveAll(value => value != null
                && string.Equals(
                    value.projectId,
                    projectId,
                    StringComparison.Ordinal));
            if (string.Equals(
                    research.activeProjectId,
                    projectId,
                    StringComparison.Ordinal))
            {
                research.activeProjectId = string.Empty;
            }
        }
        research.completedProjectIds = research.completedProjectIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        researchEnvelope.payloadJson = JsonUtility.ToJson(research);
        fixtureSave.manifest = DungeonSaveManifest.Capture(fixtureSave.sections);

        if (!gameSaveService.TryRestore(fixtureSave, out DungeonGameRestoreReport report)
            || !report.Success)
        {
            detail = "legal fixture research restore failed: "
                + string.Join(" | ", report.Errors);
            return false;
        }

        BlueprintResearchRuntime runtime = resolver
            .Resolve<ProgressionSceneRuntimeReferences>()
            .BlueprintResearch;
        string[] missing = closure
            .Where(value => !runtime.State.Projects.IsCompleted(
                new ResearchProjectId(value)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (missing.Length > 0)
        {
            detail = "fixture research restore did not publish: "
                + string.Join(",", missing);
            return false;
        }

        detail = "official-game-save research closure="
            + string.Join(",", closure.OrderBy(
                value => value,
                StringComparer.Ordinal));
        return true;
    }

    private bool TryAddDeterministicWorldResourceFixture(
        List<DungeonSaveSectionEnvelope> fixtureSave,
        out string detail)
    {
        const string PatchId = "wildlife-habitat:qa-worktype-brush";
        const string NodeId = "building:qa-worktype-brush";
        detail = string.Empty;
        if (grid == null || actor == null)
        {
            detail = "world-resource fixture requires the official grid and subject";
            return false;
        }

        DungeonSaveSectionEnvelope wildlifeEnvelope = fixtureSave.FirstOrDefault(
            value => string.Equals(value.sectionId, WildlifeSaveSection.Id, StringComparison.Ordinal));
        DungeonSaveSectionEnvelope resourceEnvelope = fixtureSave.FirstOrDefault(
            value => string.Equals(value.sectionId, WorldResourceSaveSection.Id, StringComparison.Ordinal));
        if (wildlifeEnvelope == null || resourceEnvelope == null)
        {
            detail = "official save registry lacks wildlife/world-resource sections";
            return false;
        }

        DungeonWildlifeSaveData wildlife =
            JsonUtility.FromJson<DungeonWildlifeSaveData>(wildlifeEnvelope.payloadJson);
        DungeonWorldResourceSaveData resources =
            JsonUtility.FromJson<DungeonWorldResourceSaveData>(resourceEnvelope.payloadJson);
        if (wildlife?.ecosystem == null || resources == null)
        {
            detail = "wildlife/world-resource baseline payload is invalid";
            return false;
        }
        wildlife.ecosystem.patches ??= new List<WildlifeHabitatPatchSaveData>();
        resources.nodes ??= new List<WorldResourceNodeSaveData>();

        // A previous interrupted verifier run can leave this deterministic
        // fixture in the captured baseline. Remove only the verifier-owned
        // rows before choosing a cell so they cannot exclude themselves.
        wildlife.ecosystem.patches.RemoveAll(value => value != null
            && string.Equals(value.patchId, PatchId, StringComparison.Ordinal));
        resources.nodes.RemoveAll(value => value != null
            && string.Equals(
                value.buildingInstanceId,
                NodeId,
                StringComparison.Ordinal));

        GridPathSearchResult search = grid.SearchPath(actor.GetNowXY());
        HashSet<Vector2Int> occupiedResourceCells = resources.nodes
            .Where(value => value != null)
            .Select(value => new Vector2Int(value.gridX, value.gridY))
            .ToHashSet();
        GridCell selected = grid.GetCells()
            .Where(value => value != null
                && value.AreaType == GridCellAreaType.ExteriorPath
                && value.TerrainType == GridCellTerrainType.Dry
                && value.IsWalkableArea
                && WildlifeRuntime.IsOutdoorSurfaceCell(grid, value)
                && search.ContainsPosition(value.Position)
                && !occupiedResourceCells.Contains(value.Position))
            .OrderBy(value => Mathf.Abs(value.Position.x - actor.GetNowXY().x)
                + Mathf.Abs(value.Position.y - actor.GetNowXY().y))
            .ThenBy(value => value.Position.y)
            .ThenBy(value => value.Position.x)
            .FirstOrDefault();
        if (selected == null)
        {
            detail = "no reachable dry outdoor cell can host deterministic brush resources";
            return false;
        }

        wildlife.ecosystem.patches.Add(new WildlifeHabitatPatchSaveData
        {
            patchId = PatchId,
            habitatType = WildlifeHabitatType.Brush,
            gridX = selected.Position.x,
            gridY = selected.Position.y,
            radius = 2,
            resourceCapacity = 10f,
            currentResource = 10f,
            regenPerSecond = 0.02f,
            danger = 0f,
            preferredSpeciesTags = new List<string>()
        });
        resources.nodes.Add(new WorldResourceNodeSaveData
        {
            buildingInstanceId = NodeId,
            gridX = selected.Position.x,
            gridY = selected.Position.y,
            sources = new List<WorldResourceSourceSaveData>
            {
                new WorldResourceSourceSaveData
                {
                    workTypeId = BuiltInWorkTypeIds.Gather.Value,
                    recipeId = "source:grass",
                    completedWork = 0f,
                    remainingCycles = -1
                },
                new WorldResourceSourceSaveData
                {
                    workTypeId = BuiltInWorkTypeIds.Logging.Value,
                    recipeId = "source:logging",
                    completedWork = 0f,
                    remainingCycles = 1
                }
            }
        });
        wildlifeEnvelope.payloadJson = JsonUtility.ToJson(wildlife);
        resourceEnvelope.payloadJson = JsonUtility.ToJson(resources);
        detail = "deterministic-brush=" + selected.Position
            + "; patch=" + PatchId + "; node=" + NodeId;
        return true;
    }

    private bool HasAvailableWorldResource(WorkTypeId workTypeId) =>
        worldResources?.Nodes.Any(value => value != null
            && worldResources.TryGetWork(
                value,
                workTypeId,
                out WorldResourceWorkSnapshot snapshot)
            && snapshot.Available) == true;

    private static void AddResearchClosure(
        ResearchProjectSO project,
        ISet<string> closure)
    {
        if (project == null || !closure.Add(project.ProjectId.Value))
            return;
        foreach (ResearchProjectSO prerequisite in project.Prerequisites
                     .Where(value => value != null)
                     .OrderBy(
                         value => value.ProjectId.Value,
                         StringComparer.Ordinal))
        {
            AddResearchClosure(prerequisite, closure);
        }
    }

    private static DungeonSaveSectionEnvelope CloneEnvelope(
        DungeonSaveSectionEnvelope source) => new()
    {
        sectionId = source?.sectionId ?? string.Empty,
        sectionVersion = source?.sectionVersion ?? 1,
        restorePhase = source?.restorePhase
            ?? DungeonSaveRestorePhase.RuntimeState,
        optional = source?.optional ?? false,
        payloadJson = source?.payloadJson ?? string.Empty
    };

    private MaterialWorkFixture PrepareRepairFixture(out string failureReason)
    {
        failureReason = string.Empty;
        IBuildingStructuralIntegrityRuntime integrity =
            runtimeScope.Container.Resolve<IBuildingStructuralIntegrityRuntime>();
        BuildableObject target = worldRegistry.Buildings
            .Where(value => value != null
                && !value.isDestroy
                && value.SupportsWork(BuiltInWorkTypeIds.Repair)
                && integrity.TryGet(value, out _)
                && IsReachableFromSubject(value))
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (target == null)
        {
            BuildingSO authored = LoadAuthoredBuilding(data =>
                data.Facility?.SupportsWork(BuiltInWorkTypeIds.Repair) == true
                && data.GetAbility<BuildingStructuralIntegrityAbility>() != null);
            target = PlaceAuthoredBuilding(authored, out failureReason);
        }
        if (target == null)
        {
            if (string.IsNullOrEmpty(failureReason))
                failureReason = "no authored structural repair facility";
            return null;
        }

        BuildingStructuralDamageResult damaged = integrity.ApplyDamage(target, 24f);
        if (!damaged.Applied || damaged.Destroyed)
        {
            failureReason = "structural damage authority rejected nonlethal repair damage";
            return null;
        }

        bool Redamage(out string reason)
        {
            if (target == null || target.isDestroy)
            {
                reason = "repair target disappeared before invalidation phase";
                return false;
            }
            BuildingStructuralDamageResult result = integrity.ApplyDamage(target, 12f);
            reason = result.Applied && !result.Destroyed
                ? string.Empty
                : "repair target could not be re-damaged";
            return result.Applied && !result.Destroyed;
        }
        return new MaterialWorkFixture(
            target,
            $"repair:hp={damaged.Snapshot.CurrentHitPoints:0.###}/{damaged.Snapshot.MaxHitPoints:0.###}",
            prepareInvalidation: Redamage,
            invalidate: (out string reason) =>
            {
                if (!integrity.TryGet(target, out BuildingStructuralIntegritySnapshot before))
                {
                    reason = "repair target structural state missing";
                    return false;
                }
                BuildingStructuralDamageResult destroyed =
                    integrity.ApplyDamage(target, before.MaxHitPoints * 2f);
                reason = destroyed.Destroyed
                    ? string.Empty
                    : "structural damage did not destroy repair target";
                return destroyed.Destroyed;
            });
    }

    private MaterialWorkFixture PrepareThreatFixture(out string failureReason)
    {
        failureReason = string.Empty;
        IOffenseWorldSimulation offenseWorld =
            runtimeScope.Container.Resolve<IOffenseWorldSimulation>();
        IOffenseContentCatalog content =
            runtimeScope.Container.Resolve<IOffenseContentCatalog>();
        IOffenseUrgentMitigationRuntime mitigation =
            runtimeScope.Container.Resolve<IOffenseUrgentMitigationRuntime>();
        OffenseUrgentSiteDefinitionSO definition = content.UrgentSites
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.mitigationWorkTypeId)
                && !string.IsNullOrWhiteSpace(value.mitigationItemId)
                && value.mitigationItemAmount > 0)
            .OrderBy(value => value.urgentSiteId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (definition == null)
        {
            failureReason = "no authored urgent mitigation definition";
            return null;
        }
        WorkTypeId semanticWork = new(definition.mitigationWorkTypeId);
        BuildableObject facility = worldRegistry.Buildings
            .Where(value => value != null && !value.isDestroy
                && value.SupportsWork(semanticWork))
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (facility == null)
        {
            BuildingSO authored = LoadAuthoredBuilding(data =>
                data.Facility?.SupportsWork(semanticWork) == true);
            facility = PlaceAuthoredBuilding(authored, out failureReason);
        }
        if (facility == null) return null;

        OffenseHexTileState tile = offenseWorld.Tiles
            .Where(value => value != null && !value.blocked
                && offenseWorld.GetMinimumStepDistance(
                    offenseWorld.DungeonCoord, value.Coord) is >= 1 and <= 12)
            .OrderBy(value => value.q)
            .ThenBy(value => value.r)
            .FirstOrDefault();
        if (tile == null)
        {
            failureReason = "urgent incident/order authority rejected fixture: tile-missing";
            return null;
        }
        if (!offenseWorld.TrySpawnUrgentSite(
                definition.urgentSiteId, tile.Coord, out string siteId))
        {
            failureReason = "urgent incident authority rejected fixture spawn";
            return null;
        }
        if (!mitigation.TryStart(siteId, out string startMessage))
        {
            failureReason = "urgent mitigation authority rejected fixture start: "
                + startMessage;
            return null;
        }
        if (!mitigation.TryGetOrder(
                siteId,
                out OffenseUrgentMitigationOrderStateData order))
        {
            failureReason = "urgent mitigation authority did not publish order";
            return null;
        }
        if (!physicalItems.SpawnItemAt(
                definition.mitigationItemId,
                definition.mitigationItemAmount,
                facility.centerPos,
                WorldItemStackState.FacilityBuffer,
                order.destinationId,
                out int spawned)
            || spawned != definition.mitigationItemAmount)
        {
            failureReason = "urgent mitigation physical material delivery failed";
            return null;
        }

        return new MaterialWorkFixture(
            facility,
            $"threat:site={siteId};order={order.orderId};materials={spawned}",
            prepareInvalidation: null,
            invalidate: (out string reason) =>
            {
                bool destroyed = offenseWorld.TryDestroyUrgentSite(siteId);
                reason = destroyed ? string.Empty : "urgent incident already terminal";
                return destroyed;
            });
    }

    private BuildableObject PreparePlumbingTarget(out string failureReason)
    {
        failureReason = string.Empty;
        IFluidInfrastructureQuery query =
            runtimeScope.Container.Resolve<IFluidInfrastructureQuery>();
        IFluidWastewaterTransaction wastewater =
            runtimeScope.Container.Resolve<IFluidWastewaterTransaction>();
        BuildingSO tankDefinition = LoadAuthoredBuilding(data =>
            string.Equals(
                data.GetAbility<BuildingFacilityPartAbility>()?.code,
                "I09",
                StringComparison.Ordinal)
            && data.GetAbility<BuildingWaterStorageAbility>() is
            {
                wastewaterCapacity: > 0f
            } storage
            && (storage.channels & UtilityChannel.Wastewater) != 0);
        BuildingSO ductDefinition = LoadAuthoredBuilding(data =>
            string.Equals(
                data.GetAbility<BuildingFacilityPartAbility>()?.code,
                "U04",
                StringComparison.Ordinal)
            && data.Facility?.SupportsWork(BuiltInWorkTypeIds.Plumbing) == true
            && (data.GetAbility<BuildingUtilityConnectionAbility>()?.channels
                & UtilityChannel.Wastewater) != 0);
        BuildableObject tank = PlaceAuthoredBuilding(
            tankDefinition,
            out string tankFailure);
        if (tank == null || ductDefinition == null)
        {
            failureReason = "authored I09/U04 wastewater fixture unavailable: tank="
                + tankFailure + "; duct=" + (ductDefinition != null);
            return null;
        }

        BuildableObject target = null;
        foreach (Vector2Int position in tank.BuildingData.GetGridPosList(tank.centerPos))
        {
            BuildableObject duct = PlaceAuthoredBuildingAt(
                ductDefinition,
                position,
                out string ductFailure);
            if (duct == null)
            {
                failureReason = "U04 wastewater duct placement failed at "
                    + position + ": " + ductFailure;
                return null;
            }
            rowScopedFixtureBuildings.Add(duct);
            target ??= duct;
        }
        if (target is not IWorkableFacility
            || target.BuildingData?.Facility?.SupportsWork(
                BuiltInWorkTypeIds.Plumbing) != true)
        {
            failureReason = "authored U04 is not published as a plumbing "
                + "IWorkableFacility; rebuild Industrial Infrastructure assets";
            return null;
        }
        if (!query.TryGetMaintenance(target, out _, out _))
        {
            failureReason = "I09/U04 network did not publish maintenance authority";
            return null;
        }

        bool fullyAccepted = wastewater.TryAddWastewater(
            target,
            100000f,
            out float accepted,
            out DomainFailure domainFailure);
        if (accepted <= 0f)
        {
            failureReason = "wastewater transaction could not fill network: "
                + domainFailure;
            return null;
        }
        results.Add(WorkTypeLiveRow.Info(
            "plumbing-fixture",
            "tank=" + tank.RequirePersistentInstanceId().Value
            + "; target=" + target.RequirePersistentInstanceId().Value
            + "; accepted=" + accepted.ToString("0.###")
            + "; fullyAccepted=" + fullyAccepted
            + "; overflow=" + domainFailure));
        return target;
    }

    private BuildingSO LoadAuthoredBuilding(Func<BuildingSO, bool> predicate) =>
        AssetDatabase.FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(value => value != null)
            .OrderBy(value => value.id)
            .FirstOrDefault(predicate);

    private BuildableObject PlaceAuthoredBuilding(
        BuildingSO data,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (data == null)
        {
            failureReason = "authored building asset missing";
            return null;
        }
        GridPathSearchResult subjectSearch = actor != null
            ? grid.SearchPath(actor.GetNowXY())
            : null;
        Vector2Int[] anchorCandidates = grid.GetCells()
            .Where(cell => cell != null)
            .Select(cell => cell.Position)
            .OrderBy(position => position.y)
            .ThenBy(position => position.x)
            .Where(position => data.GetGridPosList(position)
                .All(cell => grid.GetGridCell(cell) is GridCell gridCell
                    && gridCell.CanBuildInArea(data)
                    && gridCell.CanOccupy(data.Placement.Layer)))
            .Where(position => HasReachableAuthoredAccess(
                data,
                data.GetGridPosList(position),
                subjectSearch))
            .ToArray();
        if (anchorCandidates.Length == 0)
        {
            failureReason = "no reachable authored placement for " + data.name;
            return null;
        }

        GridBuildingFactory factory = new(created =>
            runtimeScope.Container.InjectGameObject(created.gameObject));
        for (int anchorIndex = 0; anchorIndex < anchorCandidates.Length; anchorIndex++)
        {
            Vector2Int anchor = anchorCandidates[anchorIndex];
            IReadOnlyList<Vector2Int> positions = data.GetGridPosList(anchor);
            if (positions.Count == 0
                || positions.Any(cell =>
                    grid.GetGridCell(cell) is not GridCell gridCell
                    || !gridCell.CanBuildInArea(data)
                    || !gridCell.CanOccupy(data.Placement.Layer)))
            {
                continue;
            }

            BuildableObject building = factory.Create(grid, data, anchor);
            if (building == null)
                continue;

            building.SetGrid(grid);
            building.Initialization(data, anchor);
            if (!grid.RegisterOccupant(
                    building,
                    data.Placement.Layer,
                    positions,
                    data.Placement.IsMovement))
            {
                Destroy(building.gameObject);
                continue;
            }

            // Work-only facilities intentionally have no visitor role. Prove
            // post-placement access through the production work stand
            // authority, never through CanQueueVisit/visitor admission.
            GridPathSearchResult postPlacementSearch = actor != null
                ? grid.SearchPath(actor.GetNowXY())
                : null;
            if (actor != null
                && !WorkTargetSelectionRules.IsReachable(
                    building,
                    postPlacementSearch))
            {
                building.DestroySelf();
                continue;
            }

            if (data.Facility?.roles == FacilityRole.None
                && postPlacementSearch != null
                && WorkTargetSelectionRules.TryGetReachableWorkAccessPosition(
                    building,
                    postPlacementSearch,
                    out Vector2Int workAccess))
            {
                results.Add(new WorkTypeLiveRow(
                    "info:work-only-access:" + data.name,
                    "INFO",
                    building.name,
                    "roles=None; workAccess=" + workAccess
                    + "; visitorAccess="
                    + postPlacementSearch.ContainsVisitableOccupant(building)
                    + "; live execution remains owned by its WorkType row"));
            }

            worldRegistry.RegisterBuilding(building);
            rowScopedFixtureBuildings.Add(building);
            return building;
        }

        failureReason = "no post-placement work access for " + data.name;
        return null;
    }

    private BuildableObject PlaceAuthoredBuildingAt(
        BuildingSO data,
        Vector2Int anchor,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (data == null || grid == null)
        {
            failureReason = "authored building or grid missing";
            return null;
        }
        IReadOnlyList<Vector2Int> positions = data.GetGridPosList(anchor);
        if (positions.Count == 0 || positions.Any(position =>
                grid.GetGridCell(position) is not GridCell cell
                || !cell.CanBuildInArea(data)
                || !cell.CanOccupy(data.Placement.Layer)))
        {
            failureReason = "exact authored footprint is unavailable";
            return null;
        }

        GridBuildingFactory factory = new(created =>
            runtimeScope.Container.InjectGameObject(created.gameObject));
        BuildableObject building = factory.Create(grid, data, anchor);
        if (building == null)
        {
            failureReason = "grid building factory returned null";
            return null;
        }
        building.SetGrid(grid);
        building.Initialization(data, anchor);
        if (!grid.RegisterOccupant(
                building,
                data.Placement.Layer,
                positions,
                data.Placement.IsMovement))
        {
            Destroy(building.gameObject);
            failureReason = "grid rejected exact authored occupant";
            return null;
        }
        worldRegistry.RegisterBuilding(building);
        return building;
    }

    private bool HasReachableAuthoredAccess(
        BuildingSO data,
        IReadOnlyList<Vector2Int> positions,
        GridPathSearchResult subjectSearch)
    {
        if (subjectSearch == null)
            return true;
        if (data == null || positions == null || positions.Count == 0)
            return false;
        if (data.IsGridMovement)
            return positions.Any(subjectSearch.ContainsPosition);

        for (int index = 0; index < positions.Count; index++)
        {
            Vector2Int occupied = positions[index];
            Vector2Int left = occupied + Vector2Int.left;
            Vector2Int right = occupied + Vector2Int.right;
            if (grid.IsValidGridPos(left)
                && grid.IsWalkable(left)
                && subjectSearch.ContainsPosition(left)
                || grid.IsValidGridPos(right)
                && grid.IsWalkable(right)
                && subjectSearch.ContainsPosition(right))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsReachableFromSubject(BuildableObject target)
    {
        if (actor == null || grid == null || target == null || target.isDestroy)
            return false;

        GridPathSearchResult search = grid.SearchPath(actor.GetNowXY());
        return WorkTargetSelectionRules.IsReachable(target, search);
    }

    private bool TryPrepareMaterialFixture(
        WorkTypeId workTypeId,
        out MaterialWorkFixture fixture,
        out string failureReason)
    {
        fixture = null;
        failureReason = string.Empty;
        if (workTypeId == BuiltInWorkTypeIds.Craft
            || workTypeId == BuiltInWorkTypeIds.Cook
            || workTypeId == BuiltInWorkTypeIds.Quarry)
        {
            return TryPrepareProductionFixture(workTypeId, out fixture, out failureReason);
        }
        if (workTypeId == BuiltInWorkTypeIds.Restock)
            return TryPrepareRestockFixture(out fixture, out failureReason);
        if (workTypeId == BuiltInWorkTypeIds.Butcher)
            return TryPrepareButcherFixture(out fixture, out failureReason);
        if (workTypeId == BuiltInWorkTypeIds.Refuel)
            return TryPrepareRefuelFixture(out fixture, out failureReason);
        if (workTypeId == BuiltInWorkTypeIds.Gather
            || workTypeId == BuiltInWorkTypeIds.Logging)
        {
            return TryPrepareWorldResourceFixture(
                workTypeId,
                out fixture,
                out failureReason);
        }
        if (workTypeId == BuiltInWorkTypeIds.Sow
            || workTypeId == BuiltInWorkTypeIds.Harvest)
        {
            return TryPrepareCropPlotFixture(
                workTypeId,
                out fixture,
                out failureReason);
        }
        if (workTypeId == BuiltInWorkTypeIds.Dismantle)
            return TryPrepareDismantleFixture(out fixture, out failureReason);
        return true;
    }

    private bool TryPrepareProductionFixture(
        WorkTypeId workTypeId,
        out MaterialWorkFixture fixture,
        out string failureReason,
        BuildableObject requiredTarget = null)
    {
        fixture = null;
        failureReason = string.Empty;
        string requiredRecipeId;
        string requiredFacilityCode;
        string requiredResearchId;
        if (workTypeId == BuiltInWorkTypeIds.Craft)
        {
            requiredRecipeId = "recipe:stone-block";
            requiredFacilityCode = "P05";
            requiredResearchId = "research:mining:stonecutting";
        }
        else if (workTypeId == BuiltInWorkTypeIds.Cook)
        {
            requiredRecipeId = "recipe:tallow";
            requiredFacilityCode = "P15";
            requiredResearchId = "research:cuisine:livestock";
        }
        else if (workTypeId == BuiltInWorkTypeIds.Quarry)
        {
            requiredRecipeId = "source:quarry";
            requiredFacilityCode = "P22";
            requiredResearchId = "research:mining:quarry";
        }
        else
        {
            failureReason = "no authored production fixture contract for "
                + workTypeId.Value;
            return false;
        }

        ProductionRecipeSO[] recipes = economyContent.Recipes
            .Where(value => value != null
                && value.WorkTypeId == workTypeId
                && value.ProcessKind == ProductionProcessKind.WorkOnly
                && string.Equals(
                    value.RecipeId,
                    requiredRecipeId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.RequiredResearchId,
                    requiredResearchId,
                    StringComparison.Ordinal)
                && value.RequiredSupportTags.Count == 0
                && (workTypeId == BuiltInWorkTypeIds.Quarry
                    || value.Inputs.Count > 0))
            .OrderByDescending(value => value.RequiredWork)
            .ThenBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        if (recipes.Length == 0)
        {
            failureReason = "authored production recipe missing or mismatched: "
                + requiredRecipeId + "@" + requiredResearchId;
            return false;
        }

        ProductionRecipeSO recipe = null;
        BuildableObject target = null;
        List<string> placementFailures = new List<string>();
        foreach (ProductionRecipeSO candidate in recipes)
        {
            target = requiredTarget != null
                && !requiredTarget.isDestroy
                && requiredTarget.gameObject.activeInHierarchy
                && string.Equals(
                    requiredTarget.BuildingData?.GetAbility<
                        BuildingFacilityPartAbility>()?.code,
                    requiredFacilityCode,
                    StringComparison.Ordinal)
                && IsReachableFromSubject(requiredTarget)
                && requiredTarget.MatchesProductionWorkstation(candidate)
                    ? requiredTarget
                    : requiredTarget == null
                        ? worldRegistry.Buildings
                            .Where(building => building != null
                                && !building.isDestroy
                                && building.gameObject.activeInHierarchy
                                && string.Equals(
                                    building.BuildingData?.GetAbility<
                                        BuildingFacilityPartAbility>()?.code,
                                    requiredFacilityCode,
                                    StringComparison.Ordinal)
                                && IsReachableFromSubject(building)
                                && building.MatchesProductionWorkstation(candidate))
                            .OrderBy(
                                building => building.PersistentInstanceId.Value,
                                StringComparer.Ordinal)
                            .FirstOrDefault()
                        : null;
            if (target == null)
            {
                if (requiredTarget != null)
                {
                    string actualCode = requiredTarget.BuildingData?.GetAbility<
                        BuildingFacilityPartAbility>()?.code ?? "missing";
                    bool active = !requiredTarget.isDestroy
                        && requiredTarget.gameObject.activeInHierarchy;
                    bool reachable = IsReachableFromSubject(requiredTarget);
                    bool matches = requiredTarget.MatchesProductionWorkstation(
                        candidate);
                    placementFailures.Add(
                        candidate.RecipeId + "=required target mismatch: active="
                        + active + "; code=" + actualCode + "; expectedCode="
                        + requiredFacilityCode + "; reachable=" + reachable
                        + "; matches=" + matches + "; actor="
                        + actor.GetNowXY() + "; target=" + requiredTarget.centerPos);
                    continue;
                }
                BuildingSO authored = LoadAuthoredBuilding(data =>
                    data.Facility?.SupportsWork(workTypeId) == true
                    && string.Equals(
                        data.GetAbility<BuildingFacilityPartAbility>()?.code,
                        requiredFacilityCode,
                        StringComparison.Ordinal)
                    && string.Equals(
                        data.GetProductionWorkstationAbility()?.WorkstationTag,
                        candidate.WorkstationTag,
                        StringComparison.Ordinal));
                target = PlaceAuthoredBuilding(authored, out string placementFailure);
                if (target == null)
                {
                    placementFailures.Add(candidate.RecipeId + "=" + placementFailure);
                    continue;
                }
            }
            recipe = candidate;
            break;
        }
        if (recipe == null || target == null)
        {
            failureReason = "no placeable authored matching workstation: "
                + string.Join(" | ", placementFailures);
            return false;
        }

        foreach (ProductionBillSnapshot existing in productionBills.GetBills(target).ToArray())
        {
            ProductionBillCommandResult removed = productionOrders.RemoveBill(
                existing.BillId,
                returnMaterials: true);
            if (!removed.Succeeded)
            {
                failureReason = "could not clear pre-existing bill:" + removed.Failure;
                return false;
            }
        }

        ProductionBillCommandResult added = productionOrders.AddBill(
            target,
            recipe.RecipeId,
            ProductionOrderMode.RepeatCount,
            2);
        if (!added.Succeeded)
        {
            failureReason = "AddBill rejected:" + added.Failure;
            return false;
        }
        ProductionBillSnapshot bill = productionBills.GetBills(target)
            .FirstOrDefault(value => value.BillId == added.BillId);
        if (bill == null
            || (bill.Inputs.Count > 0
                && string.IsNullOrWhiteSpace(bill.MaterialDestinationId)))
        {
            failureReason = "added bill did not publish required material authority";
            return false;
        }

        List<string> materialStackIds = new List<string>();
        foreach (ItemAmountDefinition input in bill.Inputs)
        {
            HashSet<string> before = physicalItems.GetAllStacks()
                .Where(value => value != null)
                .Select(value => value.StackId)
                .ToHashSet(StringComparer.Ordinal);
            int amount = Mathf.Max(1, input.Amount) * 2;
            if (!physicalItems.SpawnItemAt(
                    input.ItemId,
                    amount,
                    target.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    bill.MaterialDestinationId,
                    out int spawned)
                || spawned != amount)
            {
                failureReason = "physical recipe input spawn failed:" + input.ItemId;
                return false;
            }
            materialStackIds.AddRange(physicalItems.GetAllStacks()
                .Where(value => value != null
                    && !before.Contains(value.StackId)
                    && string.Equals(value.ItemId, input.ItemId, StringComparison.Ordinal)
                    && string.Equals(
                        value.DestinationId,
                        bill.MaterialDestinationId,
                        StringComparison.Ordinal))
                .Select(value => value.StackId));
        }

        BuildingProcessFluidAbility processFluid =
            target.BuildingData?.GetAbility<BuildingProcessFluidAbility>();
        if (processFluid != null
            && processFluid.Supports(workTypeId)
            && processFluid.cleanWaterPerCycle > 0f)
        {
            string processWaterDestination =
                "plumbing:process-water:"
                + target.RequirePersistentInstanceId().Value
                + ":" + workTypeId.Value;
            HashSet<string> before = physicalItems.GetAllStacks()
                .Where(value => value != null)
                .Select(value => value.StackId)
                .ToHashSet(StringComparer.Ordinal);
            int containers = Mathf.Max(
                1,
                Mathf.CeilToInt(processFluid.cleanWaterPerCycle));
            if (!physicalItems.SpawnItemAt(
                    "resource:clean-water",
                    containers,
                    target.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    processWaterDestination,
                    out int spawnedWater)
                || spawnedWater != containers)
            {
                failureReason = "physical process-water spawn failed:"
                    + processWaterDestination;
                return false;
            }
            string[] waterStackIds = physicalItems.GetAllStacks()
                .Where(value => value != null
                    && !before.Contains(value.StackId)
                    && string.Equals(
                        value.ItemId,
                        "resource:clean-water",
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.DestinationId,
                        processWaterDestination,
                        StringComparison.Ordinal))
                .Select(value => value.StackId)
                .ToArray();
            materialStackIds.AddRange(waterStackIds);
        }

        foreach (string stackId in materialStackIds)
        {
            if (!rowScopedItemStackIds.Contains(stackId))
                rowScopedItemStackIds.Add(stackId);
        }

        IProcessFluidUseRuntime processFluids =
            runtimeScope.Container.Resolve<IProcessFluidUseRuntime>();
        if (!processFluids.EnsureCycleSupply(
                target,
                workTypeId,
                out DomainFailure fluidFailure))
        {
            failureReason = "physical process-fluid supply not live-ready:"
                + fluidFailure;
            return false;
        }

        ProductionWorkAvailabilityResult availability =
            productionBills is IProductionBillWorkExecution workExecution
                ? workExecution.CheckWorkAvailability(target, workTypeId)
                : default;
        if (!availability.Available || availability.Bill?.BillId != bill.BillId)
        {
            failureReason = "physical bill not live-ready:"
                + (availability.Failure.IsFailure
                    ? availability.Failure.ToString()
                    : "wrong bill selected");
            return false;
        }

        fixture = new MaterialWorkFixture(
            target,
            workTypeId.Value + ":bill=" + bill.BillId.Value
                + "; recipe=" + recipe.RecipeId
                + "; facility=" + requiredFacilityCode
                + "; research=" + requiredResearchId
                + "; inputStacks=" + materialStackIds.Count,
            prepareInvalidation: null,
            invalidate: (out string reason) =>
            {
                ProductionBillCommandResult removed = productionOrders.RemoveBill(
                    bill.BillId,
                    returnMaterials: false);
                reason = removed.Succeeded ? string.Empty : removed.Failure.ToString();
                return removed.Succeeded;
            });
        return true;
    }

    private bool TryPrepareRestockFixture(
        out MaterialWorkFixture fixture,
        out string failureReason)
    {
        fixture = null;
        failureReason = string.Empty;
        Shop[] liveShops = worldRegistry.Buildings
            .OfType<Shop>()
            .Where(value => value != null
                && !value.isDestroy
                && IsReachableFromSubject(value))
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .ToArray();
        Shop shop = null;
        SaleItem saleItem = null;
        foreach (Shop candidate in liveShops)
        {
            saleItem = ResolvePhysicalSaleItem(candidate);
            if (saleItem == null)
                continue;
            shop = candidate;
            break;
        }
        if (shop == null)
        {
            BuildingSO[] authoredShops = AssetDatabase.FindAssets(
                    "t:BuildingSO",
                    new[] { "Assets/Resources/SO/Building" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
                .Where(data => data != null
                    && data.runtimeArchetype == BuildingRuntimeArchetypeKind.Shop)
                .OrderBy(data => data.id)
                .ToArray();
            List<string> authoredFailures = new List<string>();
            foreach (BuildingSO authoredShop in authoredShops)
            {
                Shop placed = PlaceAuthoredBuilding(
                    authoredShop,
                    out string placementFailure) as Shop;
                if (placed == null)
                {
                    authoredFailures.Add(authoredShop.name + "=" + placementFailure);
                    continue;
                }
                SaleItem physical = ResolvePhysicalSaleItem(placed);
                if (physical == null)
                {
                    authoredFailures.Add(authoredShop.name
                        + "=no physical sale definition");
                    placed.DestroySelf();
                    continue;
                }
                shop = placed;
                saleItem = physical;
                break;
            }
            if (shop == null)
            {
                failureReason = "no live/placeable shop exposes a physical sale definition: "
                    + string.Join(" | ", authoredFailures);
                return false;
            }
        }
        shop.DebugClearStock();
        if (saleItem == null)
        {
            failureReason = "shop sale item lacks a physical item definition";
            return false;
        }
        IWarehouseFacility warehouse = worldRegistry.Warehouses
            .Where(value => value?.Inventory != null
                && value.HasWarehouseInventory
                && !ReferenceEquals(value, shop)
                && value.Inventory.Accepts(saleItem.category)
                && value.Inventory.CanStoreItem(
                    saleItem.AuthoredItemDefinitionId, 2)
                && value is BuildableObject building
                && IsReachableFromSubject(building))
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (warehouse == null)
            warehouse = PlaceAuthoredWarehouse(saleItem.category, 2, out failureReason);
        if (warehouse is not BuildableObject warehouseBuilding)
        {
            failureReason = "no compatible live warehouse for " + saleItem.category;
            return false;
        }

        string destination = WorldItemStackRuntime.WarehouseStorageDestinationPrefix
            + warehouse.PersistentInstanceId.Value;
        List<string> stackIds = new List<string>();
        bool Seed(out string reason)
        {
            // A cancelled first phase leaves its source stack physically live.
            // Rearm from one exact source set so the second phase cannot select
            // stale stock with an obsolete pickup stand.
            HashSet<string> liveIds = physicalItems.GetAllStacks()
                .Where(value => value != null)
                .Select(value => value.StackId)
                .ToHashSet(StringComparer.Ordinal);
            foreach (string stackId in stackIds)
            {
                if (liveIds.Contains(stackId))
                    physicalItems.DeleteStack(stackId);
                rowScopedItemStackIds.Remove(stackId);
            }
            stackIds.Clear();
            HashSet<string> before = physicalItems.GetAllStacks()
                .Where(value => value != null)
                .Select(value => value.StackId)
                .ToHashSet(StringComparer.Ordinal);
            int amount = Mathf.Clamp(shop.MissingStock, 2, 12);
            bool spawned = physicalItems.SpawnItemAt(
                saleItem.ItemDefinitionId.Value,
                amount,
                warehouseBuilding.centerPos,
                WorldItemStackState.Stored,
                destination,
                out int created) && created == amount;
            stackIds.AddRange(physicalItems.GetAllStacks()
                .Where(value => value != null
                    && !before.Contains(value.StackId)
                    && string.Equals(
                        value.ItemId,
                        saleItem.ItemDefinitionId.Value,
                        StringComparison.Ordinal)
                    && string.Equals(value.DestinationId, destination, StringComparison.Ordinal))
                .Select(value => value.StackId));
            rowScopedItemStackIds.AddRange(stackIds);
            reason = spawned ? string.Empty : "exact physical shop stock spawn failed";
            return spawned;
        }
        if (!Seed(out failureReason))
            return false;

        fixture = new MaterialWorkFixture(
            shop,
            "restock:item=" + saleItem.ItemDefinitionId.Value
                + "; warehouse=" + warehouse.PersistentInstanceId.Value,
            prepareInvalidation: (out string reason) =>
            {
                shop.DebugClearStock();
                return Seed(out reason);
            },
            invalidate: (out string reason) =>
            {
                if (shop == null || shop.isDestroy)
                {
                    reason = "restock target already absent";
                    return false;
                }

                shop.DestroySelf();
                reason = string.Empty;
                return true;
            });
        return true;
    }

    private SaleItem ResolvePhysicalSaleItem(Shop shop)
    {
        if (shop == null)
            return null;
        return shop.ProductSnapshots
            .Select(product => shopStockCatalog.TryGetSaleItem(
                    product.Id,
                    out SaleItem item)
                ? item
                : null)
            .FirstOrDefault(item => item != null
                && item.ItemDefinitionId.IsValid
                && !PhysicalItemIds.TryGetEquipmentDefinitionId(
                    item.ItemDefinitionId.Value,
                    out _)
                && !PhysicalItemIds.IsEquipmentModule(
                    item.ItemDefinitionId.Value));
    }

    private bool TryPrepareButcherFixture(
        out MaterialWorkFixture fixture,
        out string failureReason)
    {
        fixture = null;
        failureReason = string.Empty;
        BuildableObject target = worldRegistry.Buildings
            .Where(value => value != null
                && !value.isDestroy
                && value.BuildingData?.GetAbility<BuildingButcherAbility>() != null)
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (target == null)
        {
            BuildingSO authored = LoadAuthoredBuilding(data =>
                data.GetAbility<BuildingButcherAbility>() != null);
            target = PlaceAuthoredBuilding(authored, out failureReason);
        }
        WildlifeActor source = wildlifeRuntime.Wildlife
            .Where(value => value != null && value.IsAlive && value.Species != null)
            .OrderBy(value => value.WildlifeId, StringComparer.Ordinal)
            .FirstOrDefault();
        string wildlifeFailure = string.Empty;
        if (source == null)
        {
            IWildlifeSpeciesCatalogProvider speciesCatalog =
                runtimeScope.Container.Resolve<IWildlifeSpeciesCatalogProvider>();
            string speciesId = speciesCatalog.All
                .Where(value => value != null)
                .Select(value => value.SpeciesId)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (string.IsNullOrWhiteSpace(speciesId))
            {
                wildlifeFailure = "authored wildlife species catalog is empty";
            }
            else if (!wildlifeRuntime.TrySpawnArrival(
                         speciesId,
                         actor.GetNowXY(),
                         out source,
                         out wildlifeFailure))
            {
                source = null;
            }
        }
        if (target == null || source == null)
        {
            failureReason = "butcher fixture missing: facility=" + (target != null)
                + "; wildlife=" + (source != null)
                + "; facilityReason=" + failureReason
                + "; wildlifeReason=" + wildlifeFailure;
            return false;
        }
        HashSet<string> before = physicalItems.GetAllStacks()
            .Where(value => value != null)
            .Select(value => value.StackId)
            .ToHashSet(StringComparer.Ordinal);
        string sourceWildlifeId = source.WildlifeId;
        carcassService.SpawnCarcass(source);
        // The carcass is the production work input. Keeping the dead source
        // WildlifeActor on the grid adds no authority and polluted later
        // room fixtures with a hard Wildlife occupant. Remove it through the
        // public wildlife lifecycle command after the carcass was committed.
        if (!wildlifeRuntime.TryRemoveArrival(sourceWildlifeId))
        {
            failureReason = "production wildlife lifecycle did not retire "
                + "butcher source " + sourceWildlifeId;
            return false;
        }
        WorldItemStackSnapshot carcass = physicalItems.GetAllStacks()
            .FirstOrDefault(value => value != null
                && !before.Contains(value.StackId)
                && WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(
                    value.ItemId,
                    out _));
        if (carcass == null || !carcassService.HasButcherWorkAvailable(target))
        {
            failureReason = "production carcass service did not publish physical butcher work";
            return false;
        }
        fixture = new MaterialWorkFixture(
            target,
            "butcher:carcass=" + carcass.StackId,
            prepareInvalidation: null,
            invalidate: (out string reason) =>
            {
                bool removed = WorldItemRepositoryEditorAccess.TryRemoveStack(
                    itemRepository,
                    carcass.StackId);
                reason = removed ? string.Empty : "carcass already absent";
                return removed;
            });
        return true;
    }

    private bool TryPrepareRefuelFixture(
        out MaterialWorkFixture fixture,
        out string failureReason)
    {
        fixture = null;
        failureReason = string.Empty;
        BuildableObject target = worldRegistry.Buildings
            .Where(value => value != null
                && !value.isDestroy
                && value.BuildingData?.GetAbility<BuildingFuelConsumerAbility>() != null
                && value.BuildingData?.GetAbility<BuildingGolemRechargeAbility>() == null
                && IsReachableFromSubject(value))
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (target == null)
        {
            BuildingSO authored = LoadAuthoredBuilding(data =>
                data.GetAbility<BuildingFuelConsumerAbility>() != null
                && data.GetAbility<BuildingGolemRechargeAbility>() == null);
            target = PlaceAuthoredBuilding(authored, out failureReason);
        }
        IWarehouseFacility warehouse = worldRegistry.Warehouses
            .Where(value => value?.Inventory != null
                && value.HasWarehouseInventory
                && value.Inventory.Accepts(StockCategory.Fuel)
                && value.Inventory.RemainingMassGrams > 0L)
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (warehouse == null)
            warehouse = PlaceAuthoredWarehouse(StockCategory.Fuel, 4, out failureReason);
        if (target == null || warehouse == null)
        {
            failureReason = "fuel consumer or compatible physical warehouse missing";
            return false;
        }
        HashSet<string> before = physicalItems.GetAllStacks()
            .Where(value => value != null)
            .Select(value => value.StackId)
            .ToHashSet(StringComparer.Ordinal);
        if (!physicalItems.SpawnStockInWarehouse(
                warehouse,
                StockCategory.Fuel,
                4,
                out int spawned)
            || spawned != 4)
        {
            failureReason = "physical fuel spawn failed";
            return false;
        }
        List<string> fuelStackIds = physicalItems.GetAllStacks()
            .Where(value => value != null
                && !before.Contains(value.StackId)
                && value.StockCategory == StockCategory.Fuel)
            .Select(value => value.StackId)
            .ToList();
        fixture = new MaterialWorkFixture(
            target,
            "refuel:physicalStacks=" + fuelStackIds.Count,
            prepareInvalidation: null,
            invalidate: (out string reason) =>
            {
                int removed = 0;
                foreach (WorldItemStackSnapshot stack in physicalItems.GetAllStacks()
                             .Where(value => value != null
                                 && value.StockCategory == StockCategory.Fuel)
                             .ToArray())
                {
                    if (WorldItemRepositoryEditorAccess.TryRemoveStack(
                            itemRepository,
                            stack.StackId))
                        removed++;
                }
                reason = removed > 0 ? string.Empty : "fuel stack already absent";
                return removed > 0;
            });
        return true;
    }

    private bool TryPrepareWorldResourceFixture(
        WorkTypeId workTypeId,
        out MaterialWorkFixture fixture,
        out string failureReason)
    {
        fixture = null;
        failureReason = string.Empty;
        if (worldResources is not WorldResourceRuntime concrete)
        {
            failureReason = "official world-resource runtime is not available";
            return false;
        }
        IWorldResourcePersistence persistence =
            runtimeScope.Container.Resolve<IWorldResourcePersistence>();
        concrete.Tick();
        DungeonWorldResourceSaveData rowBaseline = persistence.Capture();

        WorldResourceNode node = worldResources.Nodes
            .Where(value => value != null)
            .OrderBy(value => value.NodeId, StringComparer.Ordinal)
            .FirstOrDefault(value => worldResources.TryGetWork(
                value,
                workTypeId,
                out WorldResourceWorkSnapshot workSnapshot)
                && workSnapshot.Available
                && IsReachableFromSubject(value.GetComponent<BuildableObject>()));
        if (node == null)
        {
            string[] observed = worldResources.Nodes
                .Where(value => value != null)
                .Select(value => worldResources.TryGetWork(
                        value,
                        workTypeId,
                        out WorldResourceWorkSnapshot snapshot)
                    ? value.NodeId + ":" + snapshot.UnavailableReason
                    : value.NodeId + ":no-" + workTypeId.Value + "-source")
                .ToArray();
            failureReason = "official world-resource topology has no reachable available "
                + workTypeId.Value + " source; nodes=" + worldResources.NodeCount
                + "; observed=" + string.Join(" | ", observed);
            return false;
        }
        BuildableObject target = node.GetComponent<BuildableObject>();
        if (target == null)
        {
            failureReason = "world-resource node host has no production BuildableObject";
            return false;
        }
        if (!worldResources.TryGetWork(
                node,
                workTypeId,
                out WorldResourceWorkSnapshot ready)
            || !ready.Available)
        {
            failureReason = "world-resource readiness changed during fixture selection";
            return false;
        }

        fixture = new MaterialWorkFixture(
            target,
            "world-resource:node=" + ready.NodeId
                + ";recipe=" + ready.RecipeId
                + ";remaining=" + (ready.RequiredWork - ready.CompletedWork).ToString("0.###"),
            prepareInvalidation: null,
            invalidate: (out string reason) =>
            {
                if (target == null || target.isDestroy)
                {
                    reason = "world-resource host already destroyed";
                    return false;
                }
                target.DestroySelf();
                // Destroying the live host is the fault under test. Re-publish
                // the exact authoritative resource aggregate immediately via
                // its production persistence boundary so this row cannot
                // remove a shared grass/tree/rock host needed by later rows.
                // The running action still owns the destroyed target instance
                // and must reach its typed terminal independently.
                persistence.Restore(persistence.BuildRestore(rowBaseline));
                concrete.Tick();
                bool republished = worldResources.Nodes.Any(value =>
                    value != null
                    && worldResources.TryGetWork(
                        value,
                        workTypeId,
                        out WorldResourceWorkSnapshot restored)
                    && restored.Available);
                if (!republished)
                {
                    reason = "world-resource authoritative row baseline did not republish";
                    return false;
                }
                reason = string.Empty;
                return true;
            });
        return true;
    }

    private bool TryPrepareCropPlotFixture(
        WorkTypeId workTypeId,
        out MaterialWorkFixture fixture,
        out string failureReason)
    {
        fixture = null;
        failureReason = string.Empty;
        BuildingSO plotDefinition = LoadAuthoredBuilding(data =>
            data.GetAbility<BuildingCropPlotAbility>() is { Indoor: false }
            && data.Facility?.SupportsWork(BuiltInWorkTypeIds.Sow) == true
            && data.Facility.SupportsWork(BuiltInWorkTypeIds.Harvest));
        BuildableObject plot = PlaceAuthoredBuilding(
            plotDefinition,
            out failureReason);
        if (plot == null)
            return false;

        cropPlots.Tick();
        CropDefinitionSO crop = economyContent.Crops
            .Where(value => value != null
                && string.Equals(
                    value.RequiredResearchId,
                    "research:agriculture:field",
                    StringComparison.Ordinal))
            .OrderBy(value => value.CropId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (crop == null)
        {
            failureReason = "no authored field-research crop for P23";
            return false;
        }
        if (!cropPlots.TrySetCrop(plot, crop.CropId, out string setMessage))
        {
            failureReason = "crop assignment authority rejected "
                + crop.CropId + ": " + setMessage;
            return false;
        }
        cropPlots.Tick();
        CropPlotSnapshot waiting = cropPlots.Plots.FirstOrDefault(value =>
            string.Equals(
                value.PlotId,
                plot.RequirePersistentInstanceId().Value,
                StringComparison.Ordinal));
        if (waiting == null || waiting.RequiredMaterials.Count == 0)
        {
            failureReason = "P23 did not publish versioned physical sowing materials";
            return false;
        }
        foreach (KeyValuePair<string, int> material in waiting.RequiredMaterials
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            IItemTransferService transfers =
                runtimeScope.Container.Resolve<IItemTransferService>();
            bool spawnedSuccessfully = string.Equals(
                    material.Key,
                    crop.SeedItemId,
                    StringComparison.Ordinal)
                ? crop.BaseGenome != null
                  && transfers.TrySpawnItemWithComponents(
                      material.Key,
                      material.Value,
                      plot.centerPos,
                      WorldItemStackState.FacilityBuffer,
                      waiting.MaterialDestinationId,
                      new[]
                      {
                          SeedLotItemStateCodec.Encode(new SeedLotState
                          {
                              cropId = crop.CropId,
                              cultivarGenomeId = crop.BaseGenome.GenomeId,
                              generation = 0,
                              pathogenLoad = 0f
                          })
                      },
                      out int spawnedSeed)
                  && spawnedSeed == material.Value
                : physicalItems.SpawnItemAt(
                    material.Key,
                    material.Value,
                    plot.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    waiting.MaterialDestinationId,
                    out int spawnedMaterial)
                  && spawnedMaterial == material.Value;
            if (!spawnedSuccessfully)
            {
                failureReason = "P23 physical sowing material spawn failed: "
                    + material.Key
                    + (string.Equals(material.Key, crop.SeedItemId, StringComparison.Ordinal)
                        && crop.BaseGenome == null
                        ? "; crop base genome missing"
                        : string.Empty);
                return false;
            }
        }
        cropPlots.Tick();

        if (workTypeId == BuiltInWorkTypeIds.Harvest)
        {
            DungeonCropPlotSaveData harvestState = cropPlots.Capture();
            CropPlotSaveData saved = harvestState.plots.FirstOrDefault(value =>
                string.Equals(
                    value.buildingInstanceId,
                    waiting.PlotId,
                    StringComparison.Ordinal));
            if (saved == null)
            {
                failureReason = "P23 versioned state is missing after material delivery";
                return false;
            }
            saved.phase = CropPlotPhase.ReadyToHarvest;
            saved.sowWork = crop.SowWork;
            saved.growthHours = crop.GrowthHours;
            saved.harvestWork = 0f;
            saved.materialsConsumed = true;
            cropPlots.Restore(cropPlots.BuildRestore(harvestState));
            cropPlots.Tick();
        }

        if (!cropPlots.TryGetWork(
                plot,
                workTypeId,
                out CropPlotWorkSnapshot ready)
            || !ready.Available)
        {
            failureReason = "P23 did not publish " + workTypeId.Value
                + " after authoritative setup: " + ready.UnavailableReason;
            return false;
        }
        fixture = new MaterialWorkFixture(
            plot,
            "crop-plot:plot=" + ready.PlotId
                + ";crop=" + crop.CropId
                + ";phase=" + cropPlots.Plots.First(value =>
                    string.Equals(value.PlotId, ready.PlotId, StringComparison.Ordinal)).Phase
                + ";materials=" + string.Join(",", waiting.RequiredMaterials
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
                    .Select(value => value.Key + "x" + value.Value)),
            prepareInvalidation: null,
            invalidate: (out string reason) =>
            {
                if (plot == null || plot.isDestroy)
                {
                    reason = "crop plot already destroyed";
                    return false;
                }
                plot.DestroySelf();
                reason = string.Empty;
                return true;
            });
        return true;
    }

    private bool TryPrepareDismantleFixture(
        out MaterialWorkFixture fixture,
        out string failureReason,
        bool requireFaultObservationWindow = true)
    {
        fixture = null;
        failureReason = string.Empty;
        IQualityTargetPipelineCommand quality =
            runtimeScope.Container.Resolve<IQualityTargetPipelineCommand>();
        IQualityTargetPipelineQuery qualityQuery =
            runtimeScope.Container.Resolve<IQualityTargetPipelineQuery>();
        BuildingSO[] authored = AssetDatabase.FindAssets(
                "t:BuildingSO",
                new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(value => value != null
                && value.runtimeArchetype
                    == BuildingRuntimeArchetypeKind.Facility
                && value.Facility != null
                && !value.IsStructuralWall
                && !value.IsInteriorDoor
                && value.GetConstructionMaterials().Count > 0)
            // This row must observe accepted dismantle WU before requesting the
            // cancellation terminal.  Tiny props (the 32-WU candle produces an
            // 8-WU dismantle order) can cross the completion boundary inside
            // the first accelerated verifier tick, which tests rebuild/output
            // completion instead of the intended mid-work lifecycle fault.
            // Fault rows prefer substantial authored facilities. The separate
            // normal-completion row starts from the smallest authored facility;
            // its actor is reset after fixture materialization so construction
            // and quality setup cannot contaminate the live AI phase.
            .OrderBy(value => (requireFaultObservationWindow ? -1f : 1f)
                * value.GetRequiredWork(BuiltInWorkTypeIds.Construct))
            .ThenBy(value => (requireFaultObservationWindow ? -1 : 1)
                * value.GetConstructionMaterials().Sum(material => material.Amount))
            .ThenBy(value => value.id)
            .ToArray();
        Dictionary<int, BuildingSO> byId = authored
            .GroupBy(value => value.id)
            .ToDictionary(value => value.Key, value => value.First());
        void Configure(BuildableObject created)
        {
            if (created == null)
                return;
            runtimeScope.Container.InjectGameObject(created.gameObject);
            worldRegistry.RegisterBuilding(created);
        }
        GridBuildingFactory factory = new(Configure);
        BuildingPlacementValidator placementValidator =
            new BuildingPlacementValidator();
        GridBuildingPlacementService placement = new(
            grid,
            hallwayBuilding: null,
            findBuildingData: id => byId.TryGetValue(id, out BuildingSO data)
                ? data
                : null,
            buildingFactory: factory,
            placementValidator: placementValidator,
            workOrderRuntime: workOrders,
            onConstructionSiteCreated: Configure);
        GridPathSearchResult subjectSearch = grid.SearchPath(actor.GetNowXY());

        List<string> attempts = new();
        foreach (BuildingSO definition in authored)
        {
            Vector2Int[] anchors = grid.GetCells()
                .Where(value => value != null)
                .Select(value => value.Position)
                .Where(position => definition.GetGridPosList(position).All(cell =>
                    grid.GetGridCell(cell) is GridCell gridCell
                    && gridCell.CanBuildInArea(definition)
                    && gridCell.CanOccupy(definition.Placement.Layer)
                    && gridCell.CanOccupy(GridLayer.Construction)))
                .Where(position => HasReachableAuthoredAccess(
                    definition,
                    definition.GetGridPosList(position),
                    subjectSearch))
                .OrderBy(position => position.y)
                .ThenBy(position => position.x)
                .Take(3)
                .ToArray();
            foreach (Vector2Int anchor in anchors)
            {
                if (!placement.TryPlaceConstructionSite(
                        definition,
                        anchor,
                        out string placementFailure))
                {
                    attempts.Add(definition.name + "@" + anchor + "=" + placementFailure);
                    continue;
                }
                ConstructionSite site = worldRegistry.Buildings
                    .OfType<ConstructionSite>()
                    .FirstOrDefault(value => value != null
                        && !value.isDestroy
                        && value.centerPos == anchor
                        && value.id == definition.id);
                if (site == null
                    || !workOrders.TryGetOrderFor(
                        site,
                        BuiltInWorkTypeIds.Construct,
                        out WorkOrderProgressState construction))
                {
                    attempts.Add(definition.name + "@" + anchor
                        + "=construction order was not published");
                    continue;
                }

                QualityTargetPipelineSaveData request = new()
                {
                    definitionId = definition.ContentDefinitionId,
                    minimumQuality = CraftsmanshipQualityTier.Good,
                    requiredAcceptedCount = 1,
                    rejectedDisposition =
                        RejectedOutputDisposition.DismantleFacilityAndRetry,
                    limitMode = QualityRepeatLimitMode.SafeLimits,
                    maximumAttempts = 2,
                    workBudget = Mathf.Max(1f, construction.RequiredWork * 3f),
                    workerPolicy = WorkerSelectionPolicySaveData.Anyone(
                        WorkerCandidateSortMode.BestExpectedQuality)
                };
                if (!quality.CreateForWorkOrder(
                        construction.WorkOrderId,
                        request,
                        out string pipelineId,
                        out DomainFailure qualityFailure))
                {
                    attempts.Add(definition.name + "@" + anchor
                        + "=quality pipeline rejected:" + qualityFailure);
                    workOrders.CancelOrder(
                        construction.WorkOrderId,
                        refundDeliveredMaterials: true);
                    continue;
                }
                if (!workOrders.TryGetOrderFor(
                        site,
                        BuiltInWorkTypeIds.Construct,
                        out construction)
                    || construction.Status
                        == WorkOrderStatus.TargetCurrentlyUnreachable)
                {
                    attempts.Add(definition.name + "@" + anchor
                        + "=good quality target is unexpectedly unreachable");
                    quality.CancelQualityPipeline(pipelineId, out _);
                    workOrders.CancelOrder(
                        construction?.WorkOrderId ?? string.Empty,
                        refundDeliveredMaterials: true);
                    continue;
                }

                foreach (KeyValuePair<string, int> material in
                         construction.ItemMaterialRequirements)
                {
                    if (!physicalItems.SpawnItemAt(
                            material.Key,
                            material.Value,
                            anchor,
                            WorldItemStackState.FacilityBuffer,
                            construction.MaterialDestinationId,
                            out int spawned)
                        || spawned != material.Value)
                    {
                        failureReason = "dismantle prerequisite construction material failed: "
                            + material.Key;
                        return false;
                    }
                }
                string completionMessage = string.Empty;
                if (!workOrders.RefreshMaterialsReady(site)
                    || !workOrders.ApplyWork(
                        actor,
                        site,
                        BuiltInWorkTypeIds.Construct,
                        construction.RequiredWork,
                        out bool completed,
                        out _,
                        out completionMessage)
                    || !completed)
                {
                    attempts.Add(definition.name + "@" + anchor
                        + "=production construction completion rejected:"
                        + completionMessage);
                    continue;
                }

                BuildableObject completedBuilding = worldRegistry.Buildings
                    .Where(value => value != null
                        && !value.isDestroy
                        && value is not ConstructionSite
                        && value.centerPos == anchor
                        && value.id == definition.id)
                    .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (completedBuilding == null
                    || !workOrders.TryGetOrderFor(
                        completedBuilding,
                        BuiltInWorkTypeIds.Dismantle,
                        out WorkOrderProgressState dismantle))
                {
                    attempts.Add(definition.name + "@" + anchor
                        + "=quality roll met target; no dismantle order");
                    if (completedBuilding != null && !completedBuilding.isDestroy)
                        completedBuilding.DestroySelf();
                    continue;
                }
                if (!qualityQuery.TryGetQualityPipeline(
                        pipelineId,
                        out QualityTargetPipelineSaveData rejectedPipeline)
                    || rejectedPipeline.stage
                        != QualityTargetPipelineStage.Dismantling
                    || (int)completedBuilding.Craftsmanship.Quality
                        >= (int)rejectedPipeline.minimumQuality)
                {
                    attempts.Add(definition.name + "@" + anchor
                        + "=dismantle order lacked an actual terminal quality rejection");
                    workOrders.CancelOrder(
                        dismantle.WorkOrderId,
                        refundDeliveredMaterials: true);
                    quality.CancelQualityPipeline(pipelineId, out _);
                    if (!completedBuilding.isDestroy)
                        completedBuilding.DestroySelf();
                    continue;
                }
                if (!placementValidator.CanDestroy(
                        grid,
                        definition,
                        completedBuilding,
                        out string destroyPreflightFailure))
                {
                    attempts.Add(definition.name + "@" + anchor
                        + "=production destroy preflight rejected:"
                        + destroyPreflightFailure);
                    workOrders.CancelOrder(
                        dismantle.WorkOrderId,
                        refundDeliveredMaterials: true);
                    quality.CancelQualityPipeline(pipelineId, out _);
                    if (!completedBuilding.isDestroy)
                        completedBuilding.DestroySelf();
                    continue;
                }
                // WorkTaskExecutor clamps the authored worker rate to 8 WU/s.
                // Require at least two game-seconds of dismantle work so the
                // accelerated PlayMode row cannot legitimately complete before
                // its first approved-work observation/cancellation boundary.
                const float minimumObservableDismantleWork = 16f;
                if (requireFaultObservationWindow
                    && dismantle.RequiredWork < minimumObservableDismantleWork)
                {
                    attempts.Add(definition.name + "@" + anchor
                        + "=dismantle order too short for mid-work fault:"
                        + dismantle.RequiredWork.ToString("0.###"));
                    quality.CancelQualityPipeline(pipelineId, out _);
                    if (!completedBuilding.isDestroy)
                        completedBuilding.DestroySelf();
                    continue;
                }
                if (!requireFaultObservationWindow
                    && dismantle.RequiredWork <= 0f)
                {
                    attempts.Add(definition.name + "@" + anchor
                        + "=dismantle order had no positive work");
                    workOrders.CancelOrder(
                        dismantle.WorkOrderId,
                        refundDeliveredMaterials: true);
                    quality.CancelQualityPipeline(pipelineId, out _);
                    if (!completedBuilding.isDestroy)
                        completedBuilding.DestroySelf();
                    continue;
                }
                GridPathSearchResult completedSearch =
                    grid.SearchPath(actor.GetNowXY());
                if (!WorkTargetSelectionRules.IsReachable(
                        completedBuilding,
                        completedSearch))
                {
                    attempts.Add(definition.name + "@" + anchor
                        + "=production dismantle target has no work access");
                    workOrders.CancelOrder(
                        dismantle.WorkOrderId,
                        refundDeliveredMaterials: true);
                    if (!completedBuilding.isDestroy)
                        completedBuilding.DestroySelf();
                    continue;
                }
                rowScopedFixtureBuildings.Add(completedBuilding);

                fixture = new MaterialWorkFixture(
                    completedBuilding,
                    "dismantle:order=" + dismantle.WorkOrderId
                        + ";pipeline=" + pipelineId
                        + ";actualQuality="
                        + completedBuilding.Craftsmanship.Quality
                        + ";minimumQuality="
                        + rejectedPipeline.minimumQuality
                        + ";destroyPreflight=passed"
                        + ";fixturePurpose="
                        + (requireFaultObservationWindow
                            ? "mid-work-fault"
                            : "normal-completion")
                        + ";requiredWork="
                        + dismantle.RequiredWork.ToString("0.###")
                        + ";building=" + definition.ContentDefinitionId,
                    prepareInvalidation: null,
                    invalidate: (out string reason) =>
                    {
                        bool cancelled = workOrders.CancelOrder(
                            dismantle.WorkOrderId,
                            refundDeliveredMaterials: true);
                        reason = cancelled
                            ? string.Empty
                            : "dismantle order already terminal";
                        return cancelled;
                    });
                return true;
            }
        }

        failureReason = "production quality pipeline could not publish a real "
            + "facility dismantle order: " + string.Join(" | ", attempts.Take(12));
        return false;
    }

    private IWarehouseFacility PlaceAuthoredWarehouse(
        StockCategory category,
        int minimumCapacity,
        out string failureReason)
    {
        BuildingSO authored = LoadAuthoredBuilding(data =>
            data.runtimeArchetype == BuildingRuntimeArchetypeKind.Facility
            && data.GetStorageCapacity() >= minimumCapacity
            && (data.StoresAllCategories()
                || data.GetStorageCategory() == category));
        BuildableObject building = PlaceAuthoredBuilding(authored, out failureReason);
        if (building is not IWarehouseFacility warehouse
            || !warehouse.HasWarehouseInventory
            || warehouse.Inventory == null
            || !warehouse.Inventory.Accepts(category)
            || warehouse.Inventory.MaxMassGrams <= 0L)
        {
            if (string.IsNullOrEmpty(failureReason))
                failureReason = "placed authored warehouse is incompatible with " + category;
            return null;
        }
        return warehouse;
    }

    private string DescribeFixtureAuthority(
        WorkTypeId workTypeId,
        MaterialWorkFixture fixture)
    {
        BuildableObject target = fixture?.Target;
        if (target == null)
            return "fixture-target-missing";
        if (workTypeId == BuiltInWorkTypeIds.Plumbing)
        {
            IFluidInfrastructureQuery query =
                runtimeScope.Container.Resolve<IFluidInfrastructureQuery>();
            return query.TryGetMaintenance(target, out float blockage, out float leak)
                ? "maintenance:blockage=" + blockage.ToString("0.###")
                    + ",leak=" + leak.ToString("0.###")
                : "maintenance-state-missing";
        }
        if (workTypeId == BuiltInWorkTypeIds.Dismantle)
        {
            return workOrders.TryGetOrderFor(
                    target,
                    BuiltInWorkTypeIds.Dismantle,
                    out WorkOrderProgressState order)
                ? "order=" + order.WorkOrderId + ",status=" + order.Status
                    + ",remaining="
                    + Mathf.Max(0f, order.RequiredWork - order.CompletedWork)
                        .ToString("0.###")
                : "dismantle-order-missing";
        }
        return fixture.Detail;
    }

    private static string GetMissingPrerequisite(WorkTypeId workTypeId)
    {
        if (workTypeId == BuiltInWorkTypeIds.Restock)
            return "warehouse with a physical restock deficit and available source stock";
        if (workTypeId == BuiltInWorkTypeIds.Repair)
            return "damaged repairable building with repair materials";
        if (workTypeId == BuiltInWorkTypeIds.Craft)
            return "active production bill, compatible facility and physical recipe inputs";
        if (workTypeId == BuiltInWorkTypeIds.Butcher)
            return "butchery bill, compatible facility and eligible carcass";
        if (workTypeId == BuiltInWorkTypeIds.Cook)
            return "active cooking bill, compatible facility and physical ingredients";
        if (workTypeId == BuiltInWorkTypeIds.Refuel)
            return "fuel-deficient facility and compatible physical fuel";
        if (workTypeId == BuiltInWorkTypeIds.Perform)
            return "active performance assignment and usable venue";
        if (workTypeId == BuiltInWorkTypeIds.Gather)
            return "authored harvestable resource with an active gather order";
        if (workTypeId == BuiltInWorkTypeIds.Sow)
            return "prepared plot, sow order and compatible seed";
        if (workTypeId == BuiltInWorkTypeIds.Harvest)
            return "mature crop with an active harvest order";
        if (workTypeId == BuiltInWorkTypeIds.Logging)
            return "eligible tree with an active logging order";
        if (workTypeId == BuiltInWorkTypeIds.Quarry)
            return "eligible quarry or vein with an active quarry order";
        if (workTypeId == BuiltInWorkTypeIds.AnimalCare)
            return "registered animal with a live husbandry care need";
        if (workTypeId == BuiltInWorkTypeIds.GrandProject)
            return "active grand-project stage with delivered BOM";
        if (workTypeId == BuiltInWorkTypeIds.ThreatMitigation)
            return "active mitigatable hazard or threat incident";
        if (workTypeId == BuiltInWorkTypeIds.Plumbing)
            return "active plumbing construction or repair demand";
        if (workTypeId == BuiltInWorkTypeIds.Dismantle)
            return "eligible building with an active dismantle order";
        return "authored production target and its domain prerequisites";
    }

    private bool CleanupRowScopedFixtures(out string failureReason)
    {
        failureReason = string.Empty;
        bool succeeded = true;
        HashSet<string> liveStackIds = physicalItems?.GetAllStacks()
            .Where(value => value != null)
            .Select(value => value.StackId)
            .ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);
        for (int index = rowScopedItemStackIds.Count - 1; index >= 0; index--)
        {
            string stackId = rowScopedItemStackIds[index];
            if (liveStackIds.Contains(stackId))
                succeeded &= physicalItems.DeleteStack(stackId);
        }
        rowScopedItemStackIds.Clear();
        for (int index = rowScopedWildlifeIds.Count - 1; index >= 0; index--)
        {
            string wildlifeId = rowScopedWildlifeIds[index];
            if (runtimeScope?.Container != null)
            {
                IAnimalHusbandryQuery husbandryQuery =
                    runtimeScope.Container.Resolve<IAnimalHusbandryQuery>();
                IAnimalHusbandryCommand husbandryCommand =
                    runtimeScope.Container.Resolve<IAnimalHusbandryCommand>();
                WildlifeInstanceId animalId = new(wildlifeId);
                if (husbandryQuery.TryGetAnimal(
                        animalId,
                        out HusbandryAnimalState husbandryState)
                    && husbandryState.SlaughterDesignated)
                {
                    succeeded &= husbandryCommand.DesignateSlaughter(
                        animalId,
                        false,
                        out _);
                }
                IWildlifeCaptureRuntime captureRuntime =
                    runtimeScope.Container.Resolve<IWildlifeCaptureRuntime>();
                if (captureRuntime.TryGetCaptured(wildlifeId, out _))
                {
                    succeeded &= captureRuntime.TryRelease(
                        wildlifeId,
                        out _);
                }
            }
            bool stillLive = wildlifeRuntime?.Wildlife.Any(value =>
                value != null
                && string.Equals(
                    value.WildlifeId,
                    wildlifeId,
                    StringComparison.Ordinal)) == true;
            if (stillLive)
            {
                bool removed = wildlifeRuntime.TryRemoveArrival(wildlifeId);
                succeeded &= removed
                    && !wildlifeRuntime.Wildlife.Any(value =>
                        value != null
                        && string.Equals(
                            value.WildlifeId,
                            wildlifeId,
                            StringComparison.Ordinal));
            }
        }
        rowScopedWildlifeIds.Clear();
        for (int index = rowScopedFixtureBuildings.Count - 1; index >= 0; index--)
        {
            BuildableObject building = rowScopedFixtureBuildings[index];
            if (building == null)
                continue;
            worldRegistry?.UnregisterBuilding(building);
            Grid buildingGrid = building.Grid;
            BuildingSO data = building.BuildingData;
            if (buildingGrid != null && data != null)
            {
                IReadOnlyList<Vector2Int> positions =
                    data.GetGridPosList(building.centerPos);
                bool removed = buildingGrid.RemoveOccupant(
                    building,
                    data.Placement.Layer,
                    positions,
                    data.Placement.IsMovement);
                succeeded &= removed || positions.All(position =>
                    buildingGrid.GetGridCell(position)?.ContainsOccupant(
                        data.Placement.Layer,
                        building) != true);
            }
            Destroy(building.gameObject);
        }
        rowScopedFixtureBuildings.Clear();
        if (!succeeded)
            failureReason = "one or more authored row fixtures retained grid occupancy";
        return succeeded;
    }

    private IEnumerator CleanupSingleDomainRoomAfterRow(
        WorkTypeId workTypeId)
    {
        bool ownsRoom = workTypeId == BuiltInWorkTypeIds.Perform
            || workTypeId == BuiltInWorkTypeIds.AnimalCare;
        if (!ownsRoom)
            yield break;

        actor?.SetAiPaused(true);
        AbilityMove cleanupMove = actor?.GetAbility<AbilityMove>();
        brain?.StopCurrentActionForReplan(
            "worktype live matrix single-domain row cleanup");
        work?.ClearPriorityWorkTarget();
        // Stop the action/work owners first, then cancel their movement as the
        // final synchronous ownership fence.  Cancelling movement before
        // OnStop/StopAssignedWork lets their later teardown cancel a newly
        // started protected cleanup move.
        cleanupMove?.CancelActiveMovement(
            "worktype live matrix single-domain cleanup settle");
        Vector2Int? baselineReachableSentinel =
            roomBaselineReachableSentinel;
        int settledFrames = 0;
        float settleDeadline = Time.realtimeSinceStartup + 5f;
        while (settledFrames < 2
               && Time.realtimeSinceStartup < settleDeadline)
        {
            CharacterAiRuntimeGateSnapshot gate =
                brain?.CaptureRuntimeGateSnapshot() ?? default;
            bool settled = brain?.HasRunningAction != true
                && brain?.IsExternallyDrivenActionActive != true
                && work?.isWorking != true
                && work?.HasActiveWorkRoutineForDiagnostics != true
                && cleanupMove?.HasActiveMovementRoutineForDiagnostics != true
                && gate.LivePathRequests == 0
                && gate.LiveReservations == 0;
            settledFrames = settled ? settledFrames + 1 : 0;
            if (settledFrames < 2)
                yield return null;
        }
        if (settledFrames < 2)
        {
            CharacterAiRuntimeGateSnapshot gate =
                brain?.CaptureRuntimeGateSnapshot() ?? default;
            AddGlobalFailure(
                "single-domain-room-cleanup-did-not-settle",
                workTypeId.Value + ":running="
                + (brain?.HasRunningAction == true) + "; external="
                + (brain?.IsExternallyDrivenActionActive == true)
                + "; working=" + (work?.isWorking == true)
                + "; routine="
                + (work?.HasActiveWorkRoutineForDiagnostics == true)
                + "; movement="
                + (cleanupMove?.HasActiveMovementRoutineForDiagnostics == true)
                + "; paths=" + gate.LivePathRequests
                + "; reservations=" + gate.LiveReservations);
            abortRemainingRows = true;
            yield break;
        }
        if (roomExternalExitStand.HasValue && actor != null)
        {
            Vector2Int requestedExit = roomExternalExitStand.Value;
            Vector2Int exitStand = requestedExit;
            AbilityMove move = cleanupMove;
            if (move == null)
            {
                AddGlobalFailure(
                    "single-domain-room-exit-move-missing",
                    workTypeId.Value);
            }
            else if (actor.GetNowXY() != requestedExit)
            {
                IGridPathSearchBroker pathBroker =
                    runtimeScope.Container.Resolve<IGridPathSearchBroker>();
                GridTraversalContext traversal = GridTraversalContext.ForCharacter(
                    CharacterPersistentIdentity.Require(actor),
                    DoorAccessOverrideKind.None);
                Queue<GridMoveStep> preflightPath = null;
                float pathDeadline = Time.realtimeSinceStartup + 3f;
                while (preflightPath == null
                       && Time.realtimeSinceStartup < pathDeadline)
                {
                    preflightPath = pathBroker.GetMovePathTo(
                        grid,
                        actor.GetNowXY(),
                        requestedExit,
                        GridPathSearchPriority.Urgent,
                        traversal);
                    if (preflightPath == null)
                        yield return null;
                }
                bool exitPathVerified = false;
                if (preflightPath == null || preflightPath.Count == 0)
                {
                    AddGlobalFailure(
                        "single-domain-room-exit-preflight-unreachable",
                        workTypeId.Value + ":actor=" + actor.GetNowXY()
                        + "; requested=" + requestedExit);
                }
                else
                {
                    Vector2Int pathEnd = preflightPath.Last().To;
                    if (pathEnd == requestedExit)
                    {
                        exitPathVerified = true;
                    }
                    else
                    {
                        GridPathSearchResult outsideNetwork =
                            baselineReachableSentinel.HasValue
                                ? grid.SearchPath(
                                    baselineReachableSentinel.Value)
                                : null;
                        GridCell pathEndCell = grid.GetGridCell(pathEnd);
                        bool lawfulPathEnd = pathEndCell != null
                            && grid.IsWalkable(pathEnd)
                            && performanceRoom?.ContainsCell(pathEnd) != true
                            && outsideNetwork?.ContainsPosition(pathEnd) == true;
                        if (lawfulPathEnd)
                        {
                            exitStand = pathEnd;
                            exitPathVerified = true;
                        }
                        else
                        {
                            AddGlobalFailure(
                                "single-domain-room-exit-preflight-end-mismatch",
                                workTypeId.Value + ":requested="
                                + requestedExit + "; pathEnd=" + pathEnd
                                + "; lawful=" + lawfulPathEnd);
                        }
                    }
                }
                if (!exitPathVerified)
                {
                    abortRemainingRows = true;
                    yield break;
                }
                int movementVersionBeforeStart =
                    move.MovementOperationVersionForDiagnostics;
                string cancellationBeforeStart =
                    move.LastMovementCancellationSourceForDiagnostics;
                string preemptionBeforeStart =
                    move.LastMovementOperationPreemptionForDiagnostics;
                string rejectedOwnerBeforeStart =
                    move.LastRejectedMovementOperationOwnerForDiagnostics;
                if (!move.TryStartProtectedSystemMove(
                        exitStand,
                        DoorAccessOverrideKind.None,
                        out string moveFailure))
                {
                    AddGlobalFailure(
                        "single-domain-room-exit-path-rejected",
                        workTypeId.Value + ":" + moveFailure
                        + "; actor=" + actor.GetNowXY()
                        + "; exit=" + exitStand);
                    abortRemainingRows = true;
                    yield break;
                }
                else
                {
                    float moveStartedAt = Time.realtimeSinceStartup;
                    int movementVersionAfterStart =
                        move.MovementOperationVersionForDiagnostics;
                    string movementOwnerAfterStart =
                        move.ActiveMovementOperationOwnerForDiagnostics;
                    float worldDistance = 0f;
                    Vector3 previousWorld = actor.transform.position;
                    foreach (GridMoveStep step in preflightPath)
                    {
                        Vector3 nextWorld = grid.GetWorldPos(step.To);
                        worldDistance += Vector3.Distance(
                            previousWorld,
                            nextWorld);
                        previousWorld = nextWorld;
                    }
                    float gameSeconds = worldDistance
                        / Mathf.Max(0.1f, actor.GetMoveSpeed());
                    float realSeconds = gameSeconds
                        / Mathf.Max(0.01f, Time.timeScale);
                    float observationSeconds = Mathf.Clamp(
                        5f + realSeconds * 2f + preflightPath.Count * 0.1f,
                        MinimumProgressObservationSeconds,
                        MaximumProgressObservationSeconds);
                    float softObservationDeadline = Time.realtimeSinceStartup
                        + observationSeconds;
                    float hardExitDeadline = Time.realtimeSinceStartup
                        + MaximumProgressObservationSeconds;
                    float lastProgressAt = Time.realtimeSinceStartup;
                    Vector3 lastWorldPosition = actor.transform.position;
                    Vector2Int lastGridPosition = actor.GetNowXY();
                    int worldProgressSamples = 0;
                    int gridProgressSamples = 0;
                    bool stalled = false;
                    bool movementOwnershipLost = false;
                    while (move.HasActiveMovementRoutineForDiagnostics
                           && Time.realtimeSinceStartup < hardExitDeadline)
                    {
                        if (actor.GetNowXY() != exitStand
                            && (move.MovementOperationVersionForDiagnostics
                                != movementVersionAfterStart
                                || !move.IsSystemMoveInProgressTo(exitStand)))
                        {
                            movementOwnershipLost = true;
                            break;
                        }
                        Vector3 currentWorldPosition = actor.transform.position;
                        if ((currentWorldPosition - lastWorldPosition).sqrMagnitude
                            > MovementPositionEpsilonSquared)
                        {
                            lastProgressAt = Time.realtimeSinceStartup;
                            lastWorldPosition = currentWorldPosition;
                            worldProgressSamples++;
                            Vector2Int currentGridPosition = actor.GetNowXY();
                            if (currentGridPosition != lastGridPosition)
                            {
                                lastGridPosition = currentGridPosition;
                                gridProgressSamples++;
                            }
                        }
                        else if (Time.realtimeSinceStartup - lastProgressAt
                                 >= MovementStallSeconds)
                        {
                            stalled = true;
                            break;
                        }
                        yield return null;
                    }
                    bool activeBeforeCancel =
                        move.HasActiveMovementRoutineForDiagnostics;
                    bool blockedBeforeCancel = move.LastGridMoveWasBlocked;
                    GridMoveFailureReason failureBeforeCancel =
                        move.LastGridMoveFailureReason;
                    int movementVersionBeforeCancel =
                        move.MovementOperationVersionForDiagnostics;
                    string movementOwnerBeforeCancel =
                        move.ActiveMovementOperationOwnerForDiagnostics;
                    string cancellationAtTerminal =
                        move.LastMovementCancellationSourceForDiagnostics;
                    string preemptionAtTerminal =
                        move.LastMovementOperationPreemptionForDiagnostics;
                    string rejectedOwnerAtTerminal =
                        move.LastRejectedMovementOperationOwnerForDiagnostics;
                    bool exited = actor.GetNowXY() == exitStand
                        && !blockedBeforeCancel
                        && !activeBeforeCancel;
                    move.CancelActiveMovement(
                        "worktype live matrix room exit completed");
                    if (!exited)
                    {
                        AddGlobalFailure(
                            "single-domain-room-exit-terminal-failed",
                            workTypeId.Value + ":actor=" + actor.GetNowXY()
                            + "; requested=" + requestedExit
                            + "; exit=" + exitStand + "; active="
                            + activeBeforeCancel + "; blocked="
                            + blockedBeforeCancel + "; failure="
                            + failureBeforeCancel + "; stalled=" + stalled
                            + "; ownershipLost=" + movementOwnershipLost
                            + "; softEtaExpired="
                            + (Time.realtimeSinceStartup
                               >= softObservationDeadline)
                            + "; softObservation="
                            + observationSeconds.ToString("0.###")
                            + "; hardObservation="
                            + MaximumProgressObservationSeconds.ToString("0.###")
                            + "; elapsed="
                            + (Time.realtimeSinceStartup - moveStartedAt)
                                .ToString("0.###")
                            + "; pathSteps=" + preflightPath.Count
                            + "; progressSamples=" + worldProgressSamples
                            + "/" + gridProgressSamples
                            + "; lastProgressAge="
                            + (Time.realtimeSinceStartup - lastProgressAt)
                                .ToString("0.###")
                            + "; operationVersion="
                            + movementVersionBeforeStart + "->"
                            + movementVersionAfterStart + "->"
                            + movementVersionBeforeCancel
                            + "; ownerAfterStart=" + movementOwnerAfterStart
                            + "; ownerBeforeCancel="
                            + movementOwnerBeforeCancel
                            + "; cancellation="
                            + cancellationBeforeStart + "->"
                            + cancellationAtTerminal
                            + "; preemption=" + preemptionBeforeStart + "->"
                            + preemptionAtTerminal
                            + "; rejectedOwner=" + rejectedOwnerBeforeStart
                            + "->"
                            + rejectedOwnerAtTerminal);
                        // Keep the still-valid connector and room intact. The
                        // final baseline cleanup owns recovery after the matrix
                        // stops; rolling back under an in-room actor would
                        // contaminate every later row with an unreachable worker.
                        abortRemainingRows = true;
                        yield break;
                    }
                }
            }
        }
        string[] animalIds = rowScopedWildlifeIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (!CleanupRowScopedFixtures(out string rowFailure))
        {
            AddGlobalFailure(
                "single-domain-row-fixture-cleanup-failed",
                workTypeId.Value + ":" + rowFailure);
        }

        if (animalIds.Length > 0 && runtimeScope?.Container != null)
        {
            IAnimalHusbandryQuery husbandry =
                runtimeScope.Container.Resolve<IAnimalHusbandryQuery>();
            float deadline = Time.realtimeSinceStartup + 8f;
            bool HasProjectedAnimal() => animalIds.Any(id =>
                husbandry.TryGetAnimal(new WildlifeInstanceId(id), out _));
            while (HasProjectedAnimal()
                   && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            if (HasProjectedAnimal())
            {
                AddGlobalFailure(
                    "single-domain-husbandry-projection-cleanup-timeout",
                    workTypeId.Value + ":" + string.Join(",", animalIds));
            }
        }

        if (!CleanupRoomFixture(out string roomFailure))
        {
            AddGlobalFailure(
                "single-domain-room-cleanup-failed",
                workTypeId.Value + ":" + roomFailure);
        }
        if (worldResources is WorldResourceRuntime worldResourceRuntime)
            worldResourceRuntime.Tick();
        if (runtimeScope?.Container != null)
        {
            runtimeScope.Container.Resolve<IFacilityCandidateCache>()
                .MarkDynamicStateDirty();
        }
        yield return null;
        GridPathSearchResult restoredSearch = actor != null
            ? grid.SearchPath(actor.GetNowXY())
            : null;
        bool actorRestored = actor != null
            && grid.IsWalkable(actor.GetNowXY())
            && (!baselineReachableSentinel.HasValue
                || restoredSearch?.ContainsPosition(
                    baselineReachableSentinel.Value) == true);
        if (!actorRestored)
        {
            AddGlobalFailure(
                "single-domain-room-actor-reachability-not-restored",
                workTypeId.Value + ":actor="
                + (actor != null ? actor.GetNowXY().ToString() : "missing")
                + "; sentinel="
                + (baselineReachableSentinel?.ToString() ?? "missing"));
        }
        bool exact = rowScopedWildlifeIds.Count == 0
            && roomFixtureBuildings.Count == 0
            && displacedRoomMovements.Count == 0
            && displacedRoomWildlife.Count == 0
            && roomAreaSnapshots.Count == 0
            && domainStage == null
            && domainPen == null
            && performanceRoom == null
            && !roomExternalExitStand.HasValue
            && !roomBaselineReachableSentinel.HasValue;
        if (!exact)
        {
            AddGlobalFailure(
                "single-domain-room-cleanup-incomplete",
                workTypeId.Value + ":rowWildlife="
                + rowScopedWildlifeIds.Count + "; buildings="
                + roomFixtureBuildings.Count + "; movements="
                + displacedRoomMovements.Count + "; displacedWildlife="
                + displacedRoomWildlife.Count + "; areas="
                + roomAreaSnapshots.Count + "; stage=" + (domainStage != null)
                + "; pen=" + (domainPen != null) + "; room="
                + (performanceRoom != null));
        }
        else
        {
            results.Add(WorkTypeLiveRow.Info(
                "single-domain-room-cleanup",
                workTypeId.Value
                + ": exact area/building/wildlife projection restored"));
        }
    }

    private bool CleanupRoomFixture(out string failureReason)
    {
        failureReason = string.Empty;
        bool hadFixtureState = roomFixtureBuildings.Count > 0
            || displacedRoomMovements.Count > 0
            || displacedRoomWildlife.Count > 0
            || roomAreaSnapshots.Count > 0
            || domainStage != null
            || domainPen != null
            || performanceRoom != null
            || roomExternalExitStand.HasValue
            || roomBaselineReachableSentinel.HasValue;
        if (!hadFixtureState)
            return true;
        bool buildingsRemoved = true;
        for (int index = roomFixtureBuildings.Count - 1; index >= 0; index--)
        {
            BuildableObject building = roomFixtureBuildings[index];
            if (building == null)
                continue;
            worldRegistry?.UnregisterBuilding(building);
            Grid buildingGrid = building.Grid;
            BuildingSO data = building.BuildingData;
            if (buildingGrid != null && data != null)
            {
                IReadOnlyList<Vector2Int> positions =
                    data.GetGridPosList(building.centerPos);
                bool removed = buildingGrid.RemoveOccupant(
                    building,
                    data.Placement.Layer,
                    positions,
                    data.Placement.IsMovement);
                buildingsRemoved &= removed || positions.All(position =>
                    buildingGrid.GetGridCell(position)?.ContainsOccupant(
                        data.Placement.Layer,
                        building) != true);
            }
            Destroy(building.gameObject);
        }
        roomFixtureBuildings.Clear();

        bool movementsRestored = true;
        foreach (DisplacedMovementSnapshot displaced in displacedRoomMovements)
        {
            bool registered = displaced.Building != null
                && grid != null
                && grid.RegisterOccupant(
                    displaced.Building,
                    displaced.Layer,
                    displaced.Positions,
                    displaced.ConnectPositions);
            movementsRestored &= registered
                && displaced.Positions.All(position =>
                    grid.GetGridCell(position)?.ContainsOccupant(
                        displaced.Layer,
                        displaced.Building) == true);
        }
        displacedRoomMovements.Clear();

        bool areasRestored = true;
        foreach (FixtureAreaSnapshot snapshot in roomAreaSnapshots)
        {
            GridCell cell = grid?.GetGridCell(snapshot.Position);
            if (cell == null)
            {
                areasRestored = false;
                continue;
            }
            grid.SetAreaType(snapshot.Position, snapshot.AreaType);
            areasRestored &= cell.AreaType == snapshot.AreaType;
        }
        roomAreaSnapshots.Clear();

        bool wildlifeRestored = true;
        for (int index = displacedRoomWildlife.Count - 1;
             index >= 0;
             index--)
        {
            DisplacedWildlifeSnapshot displaced =
                displacedRoomWildlife[index];
            WildlifeActor animal = displaced.Actor;
            if (animal == null || !animal.IsAlive)
            {
                wildlifeRestored = false;
                continue;
            }
            if (animal.GridPosition != displaced.Origin)
            {
                if (!CanPlaceWildlifeAt(
                        grid,
                        displaced.Origin,
                        animal.CanEnterDungeon))
                {
                    wildlifeRestored = false;
                    continue;
                }
                animal.WarpTo(displaced.Origin);
            }
            wildlifeRestored &= animal.GridPosition == displaced.Origin
                && grid.GetGridCell(displaced.Origin)?.ContainsOccupant(
                    GridLayer.Wildlife,
                    animal) == true;
        }
        displacedRoomWildlife.Clear();
        domainStage = null;
        domainPen = null;
        performanceRoom = null;
        roomExternalExitStand = null;
        roomBaselineReachableSentinel = null;
        if (runtimeScope?.Container != null)
            runtimeScope.Container.Resolve<IRoomLayoutCache>().Clear();

        if (!buildingsRemoved || !movementsRestored || !areasRestored
            || !wildlifeRestored)
        {
            failureReason = "buildings=" + buildingsRemoved
                + "; movements=" + movementsRestored
                + "; areas=" + areasRestored
                + "; wildlife=" + wildlifeRestored;
            return false;
        }
        return true;
    }

    private void Cleanup()
    {
        w04RepairDeliveries?.Dispose();
        w04RepairDeliveries = null;
        w01RepairDeliveryGate?.Dispose();
        w01RepairDeliveryGate = null;
        w05HarvestDeliveryGate?.Dispose();
        w05HarvestDeliveryGate = null;
        w01RepairObservation?.Dispose();
        w05IdentitySubscription?.Dispose();
        w05IdentitySubscription = null;
        try
        {
            brain?.StopCurrentActionForReplan("worktype live matrix cleanup");
            work?.ClearPriorityWorkTarget();
            if (!CleanupRowScopedFixtures(out string rowCleanupFailure))
                AddGlobalFailure("row-fixture-cleanup-failed", rowCleanupFailure);
            if (!CleanupRoomFixture(out string roomCleanupFailure))
                AddGlobalFailure("room-fixture-cleanup-failed", roomCleanupFailure);
            if (gameSaveService != null && baseline != null)
            {
                if (!gameSaveService.TryRestore(
                        baseline,
                        out DungeonGameRestoreReport report)
                    || !report.Success)
                {
                    AddGlobalFailure(
                        "baseline-restore-failed",
                        string.Join(" | ", report.Errors));
                }
                else
                {
                    results.Add(WorkTypeLiveRow.Info(
                        "baseline-official-restore",
                        "sections=" + (baseline.sections?.Count ?? 0)
                            + "; completedHooks=true"));
                }
            }
            RestoreActorPauseStates();
        }
        catch (Exception exception)
        {
            AddGlobalFailure("cleanup-exception", exception.ToString());
        }
    }

    private void AddGlobalFailure(string id, string detail)
    {
        results.Add(new WorkTypeLiveRow("global:" + id, "FAIL", "-", detail));
    }

    private void RestoreActorPauseStates()
    {
        if (worldRegistry == null || actorPauseStates.Count == 0)
            return;

        CharacterActor[] liveActors = LiveActors(worldRegistry);
        foreach (ActorPauseState state in actorPauseStates)
        {
            CharacterActor restored = liveActors.FirstOrDefault(value =>
                string.Equals(
                    value.Identity?.PersistentId,
                    state.PersistentId,
                    StringComparison.Ordinal));
            if (restored == null)
            {
                AddGlobalFailure(
                    "actor-pause-restore-target-missing",
                    state.PersistentId);
                continue;
            }
            restored.SetAiPaused(state.WasPaused);
        }
    }

    private bool WriteReport()
    {
        int passed = results.Count(row => row.Status == "PASS");
        int blocked = results.Count(row => row.Status == "BLOCKED");
        int failed = results.Count(row => row.Status == "FAIL");
        string[] focusedRows =
        {
            "p15:manual",
            "p15:powered-assist",
            "p15:allocated-worker-transition",
            "p15:automatic",
            "p15:utility-failure-atomic",
            "p15:persistent-cook-pause"
        };
        string[] w05Rows =
        {
            BuiltInWorkTypeIds.Harvest.Value,
            "w05:harvest-terminal-identity",
            "w01:harvest-child-before-identity",
            "w01:harvest-terminal-child-join",
            "w01:harvest-pending-save-resume",
            "w01:harvest-missing-child-rejected",
            "w01:harvest-completed-save-replay"
        };
        string[] w01Rows =
        {
            BuiltInWorkTypeIds.Repair.Value,
            "w01:repair-missing-child-rejected",
            "w01:repair-pending-save-resume",
            "w01:terminal-child-join",
            "w01:repair-identity-child-join",
            "w01:terminal-save-replay",
            "w01:repair-identity-save-join",
            "w01:repair-identity-exact-replay",
            "w01:shared-delivery-replay"
        };
        string[] w03Rows =
        {
            "w03:rescue-partial-cancel", "w03:intervening-repair",
            "w03:partial-official-restore", "w03:actual-medical-completion",
            "w03:terminal-official-replay"
        };
        string[] w04Rows =
        {
            "w04:general-repair",
            "w04:structural-repair",
            "w04:defense-repair",
            "w04:automation-repair",
            "w04:equipment-material-shortage",
            "w04:equipment-partial-save-resume",
            "w04:equipment-repair",
            "w04:terminal-replay",
            "w04:five-kind-ledger-join"
        };
        string[] w06Rows =
        {
            "w06:multi-worker",
            "w06:same-day-multiple-targets",
            "w06:quantity-labor-causality"
        };
        bool fireIdentity = CharacterAiWorkTypeLiveMatrixPlayModeVerifier.IsW01FireIdentityVerification;
        bool fireWaterIdentity = CharacterAiWorkTypeLiveMatrixPlayModeVerifier.IsW01FireWaterIdentityVerification;
        if (fireIdentity)
            w01Rows = new[]
            {
                BuiltInWorkTypeIds.ThreatMitigation.Value,
                "w01:fire-terminal-child-join",
                "w01:fire-identity-child-join",
                "w01:fire-missing-child-rejected",
                "w01:fire-pending-save-resume",
                "w01:fire-identity-replay",
                "w01:fire-terminal-save-join",
                "w01:fire-domain-replay",
                "w01:fire-identity-save-join"
            };
        if (fireWaterIdentity)
            w01Rows = w01Rows.Concat(new[] { "w01:fire-water-carried-save-join",
                "w01:fire-water-actual-haul", "w01:fire-water-consumed-once",
                "w01:fire-water-exact-claim-required" }).ToArray();
        bool complete = consoleGatePassed
            && (W06ResourceCausalityOnly
                ? w06Rows.All(id => results.Any(row =>
                    row.WorkTypeId == id && row.Status == "PASS"))
                : W04RepairLiveOnly
                ? w04Rows.All(id => results.Any(row =>
                    row.WorkTypeId == id && row.Status == "PASS"))
                : W03MedicalLaborOnly
                ? w03Rows.All(id => results.Any(row => row.WorkTypeId == id && row.Status == "PASS"))
                : W01WorkTerminalJoinOnly
                ? w01Rows.All(id => results.Any(row =>
                    row.WorkTypeId == id && row.Status == "PASS"))
                : P15AutomationModesOnly
                ? focusedRows.All(id => results.Any(row =>
                    row.WorkTypeId == id && row.Status == "PASS"))
                : W05HarvestIdentityOnly
                    ? w05Rows.All(id => results.Any(row =>
                        row.WorkTypeId == id && row.Status == "PASS"))
                    : Rows.All(id => results.Any(row =>
                        row.WorkTypeId == id.Value && row.Status == "PASS")));
        string reportPath = W06ResourceCausalityOnly
            ? CharacterAiWorkTypeLiveMatrixPlayModeVerifier
                .W06ResourceCausalityReportPath
            : W04RepairLiveOnly
            ? CharacterAiWorkTypeLiveMatrixPlayModeVerifier.W04RepairLiveReportPath
            : W03MedicalLaborOnly
            ? CharacterAiWorkTypeLiveMatrixPlayModeVerifier.W03MedicalLaborReportPath
            : W01WorkTerminalJoinOnly
            ? CharacterAiWorkTypeLiveMatrixPlayModeVerifier.W01WorkTerminalJoinReportPath
            : P15AutomationModesOnly
            ? CharacterAiWorkTypeLiveMatrixPlayModeVerifier
                .P15AutomationModesReportPath
            : W05HarvestIdentityOnly
                ? CharacterAiWorkTypeLiveMatrixPlayModeVerifier
                    .W05HarvestIdentityReportPath
                : CharacterAiWorkTypeLiveMatrixPlayModeVerifier.ReportPath;
        List<string> lines = new List<string>(results.Count + 8)
        {
            W06ResourceCausalityOnly
                ? "# W06 production resource worker/source/product causality"
                : W04RepairLiveOnly
                ? "# W04 actual five-kind repair, material shortage, partial restore and exact terminal replay"
                : W03MedicalLaborOnly
                ? "# W03 actual medical A -> cancel -> repair B -> resume A / official restore / exact labor"
                : W01WorkTerminalJoinOnly
                ? fireWaterIdentity ? "# W01 actual water fire haul, save-resume and completion identity"
                    : fireIdentity ? "# W01 actual environmental fire completion identity child join"
                    : "# W01 actual repair terminal and completion identity child join"
                : P15AutomationModesOnly
                ? "# P15 production execution-mode live matrix"
                : W05HarvestIdentityOnly
                    ? "# W05 harvest terminal identity production-live verification"
                    : "# Character AI WorkType production-live matrix",
            W06ResourceCausalityOnly
                ? "authority=ResourceGatheringWorkExecutionHandler -> CropPlotRuntime/WorldResourceRuntime -> physical transaction -> GameplayOutcomeLedger"
                : W04RepairLiveOnly
                ? "authority=Brain -> AIWork -> AbilityWork -> WorkTaskExecutor -> RepairWorkExecutionHandler; equipment input delivery uses Brain -> AIHaul"
                : W03MedicalLaborOnly
                ? "authority=Brain -> AIRescue -> AbilityRescue -> CharacterMedicalRuntime; intervening Brain -> AIWork -> AbilityWork -> WorkTaskExecutor"
                : P15AutomationModesOnly
                ? "authority=Manual/PoweredAssist:Brain -> AIWork -> AbilityWork -> WorkTaskExecutor; Automatic:AutomationRuntime -> ProductionBillWorkExecution"
                : "authority=Brain -> AIWork -> AbilityWork -> WorkTaskExecutor",
            W06ResourceCausalityOnly
                ? "scope=two persistent workers, two sow plots and Gather/Logging in one day; exact accepted work, crop inputs, source debit and physical outputs"
                : W04RepairLiveOnly
                ? "scope=controlled damage and exact repair supplies; actual five adapter dispatches, shortage rejection, partial whole-world restore, one-time material consumption and completion identity replay"
                : W03MedicalLaborOnly
                ? "scope=controlled injury and physical delivery commands; actual rescue/carry/treatment and official pending/completed restore; no natural injury or AI-haul claim"
                : W01WorkTerminalJoinOnly
                ? fireWaterIdentity ? "scope=actual Brain/AIHaul carried-water official save/fresh resume then Brain/AIWork Water suppression; controlled source/ignition; domain replay supplemental"
                    : fireIdentity ? "scope=actual initial attack; domain replay is supplemental; no water-mode or pre-terminal save claim"
                    : "shared-delivery-replay is bounded supplemental evidence; repair identity rows require actual AI outcomes"
                : "contract/direct-handler evidence is never accepted as PASS",
            "RESULT=" + (complete && failed == 0 && blocked == 0 ? "PASS" : "FAIL")
                + "; rows=" + (W06ResourceCausalityOnly
                    ? w06Rows.Length
                    : W04RepairLiveOnly
                    ? w04Rows.Length
                    : W03MedicalLaborOnly
                    ? w03Rows.Length : W01WorkTerminalJoinOnly
                    ? w01Rows.Length
                    : P15AutomationModesOnly
                    ? focusedRows.Length
                    : W05HarvestIdentityOnly
                        ? w05Rows.Length
                        : Rows.Length) + "; passed=" + passed
                + "; blocked=" + blocked + "; failed=" + failed,
            "status\tworkType\ttarget\tdetail"
        };
        IEnumerable<WorkTypeLiveRow> reportRows = W06ResourceCausalityOnly
            ? results.Where(row => w06Rows.Contains(
                    row.WorkTypeId,
                    StringComparer.Ordinal)
                || row.Status == "FAIL"
                || row.WorkTypeId == "info:console-warning-error-zero"
                || row.WorkTypeId == "info:baseline-official-restore"
                || row.WorkTypeId == "info:subject")
            : W04RepairLiveOnly
            ? results.Where(row => w04Rows.Contains(
                    row.WorkTypeId,
                    StringComparer.Ordinal)
                || row.Status == "FAIL"
                || row.WorkTypeId == "info:console-warning-error-zero"
                || row.WorkTypeId == "info:baseline-official-restore"
                || row.WorkTypeId == "info:subject")
            : P15AutomationModesOnly
            ? results.Where(row => focusedRows.Contains(
                    row.WorkTypeId,
                    StringComparer.Ordinal)
                || row.Status == "FAIL"
                || row.WorkTypeId == "info:console-warning-error-zero"
                || row.WorkTypeId == "info:baseline-official-restore")
            : W05HarvestIdentityOnly
                ? results.Where(row => w05Rows.Contains(
                        row.WorkTypeId,
                        StringComparer.Ordinal)
                    || row.Status == "FAIL"
                    || row.WorkTypeId == "info:console-warning-error-zero"
                    || row.WorkTypeId == "info:baseline-official-restore"
                    || row.WorkTypeId == "info:subject")
                : results;
        lines.AddRange(reportRows.Select(row =>
            row.Status + "\t" + row.WorkTypeId + "\t" + row.Target + "\t" + row.Detail));
        Directory.CreateDirectory(Path.GetDirectoryName(
            reportPath)
            ?? "Artifacts/QA");
        if (CharacterAiWorkTypeLiveMatrixPlayModeVerifier
                .IsPersistentCookPauseVerification
            || CharacterAiWorkTypeLiveMatrixPlayModeVerifier
                .IsW05HarvestIdentityVerification
            || CharacterAiWorkTypeLiveMatrixPlayModeVerifier
                .IsW01WorkTerminalJoinVerification
            || CharacterAiWorkTypeLiveMatrixPlayModeVerifier.IsW03MedicalLaborVerification
            || CharacterAiWorkTypeLiveMatrixPlayModeVerifier.IsW04RepairLiveVerification
            || CharacterAiWorkTypeLiveMatrixPlayModeVerifier.IsW06ResourceCausalityVerification)
        {
            WriteUtf8LfCreateNew(reportPath, lines);
        }
        else
        {
            WriteUtf8LfIfChanged(reportPath, lines);
        }
        string resultPrefix = W06ResourceCausalityOnly
            ? "W06_RESOURCE_CAUSALITY"
            : W04RepairLiveOnly
            ? "W04_REPAIR_LIVE"
            : W03MedicalLaborOnly
            ? "W03_MEDICAL_LABOR"
            : W01WorkTerminalJoinOnly
            ? "W01_WORK_TERMINAL_JOIN"
            : P15AutomationModesOnly
            ? "P15_PRODUCTION_EXECUTION_MODES"
            : W05HarvestIdentityOnly
                ? "W05_HARVEST_TERMINAL_IDENTITY"
                : "CHARACTER_AI_WORKTYPE_LIVE_MATRIX";
        Debug.Log(resultPrefix + "="
            + (complete && failed == 0 && blocked == 0 ? "PASS" : "FAIL")
            + "; passed=" + passed
            + "; blocked=" + blocked + "; failed=" + failed);
        return complete && failed == 0 && blocked == 0;
    }

    private static void WriteUtf8LfIfChanged(
        string path,
        IReadOnlyList<string> lines)
    {
        string contents = string.Join("\n", lines) + "\n";
        if (File.Exists(path)
            && string.Equals(
                File.ReadAllText(path, Encoding.UTF8),
                contents,
                StringComparison.Ordinal))
        {
            return;
        }
        File.WriteAllText(
            path,
            contents,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteUtf8LfCreateNew(
        string path,
        IReadOnlyList<string> lines)
    {
        string contents = string.Join("\n", lines) + "\n";
        using FileStream stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read);
        using StreamWriter writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(contents);
    }

    private enum WorkProbeFault
    {
        CancelAfterApprovedProgress,
        PauseAfterApprovedProgress,
        InvalidateTargetAfterStart,
        CompleteNormally
    }

    private delegate bool FixtureMutation(out string failureReason);

    private sealed class MaterialWorkFixture
    {
        private readonly FixtureMutation prepareInvalidation;
        private readonly FixtureMutation invalidate;

        public MaterialWorkFixture(
            BuildableObject target,
            string detail,
            FixtureMutation prepareInvalidation,
            FixtureMutation invalidate,
            bool allowEquivalentTarget = false)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Detail = detail ?? string.Empty;
            this.prepareInvalidation = prepareInvalidation;
            this.invalidate = invalidate
                ?? throw new ArgumentNullException(nameof(invalidate));
            AllowEquivalentTarget = allowEquivalentTarget;
        }

        public BuildableObject Target { get; }
        public string Detail { get; }
        public bool AllowEquivalentTarget { get; }

        public bool AcceptsTarget(BuildableObject target) =>
            ReferenceEquals(target, Target)
            || AllowEquivalentTarget && target != null && !target.isDestroy;

        public bool TryPrepareInvalidationPhase(out string failureReason)
        {
            if (prepareInvalidation == null)
            {
                failureReason = string.Empty;
                return true;
            }
            return prepareInvalidation(out failureReason);
        }

        public bool TryInvalidate(out string failureReason) =>
            invalidate(out failureReason);
    }

    private sealed class P15FocusedFixture
    {
        public P15FocusedFixture(
            BuildableObject target,
            MaterialWorkFixture fixture,
            ProductionBillId billId)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            BillId = billId;
        }

        public BuildableObject Target { get; }
        public MaterialWorkFixture Fixture { get; }
        public ProductionBillId BillId { get; }
    }

    private sealed class W04RepairArmResult
    {
        public W04RepairArmResult(
            string kind,
            string workerId,
            string facilityId,
            string targetEntityId,
            WorkPhaseResult phase,
            WorkExecutionResult terminal,
            GameplayOutcomeSnapshot child,
            WorkCompletionIdentityDeliveryRequest request,
            bool joined,
            string detail)
        {
            Kind = kind ?? string.Empty;
            WorkerId = workerId ?? string.Empty;
            FacilityId = facilityId ?? string.Empty;
            TargetEntityId = targetEntityId ?? string.Empty;
            Phase = phase;
            Terminal = terminal;
            Child = child;
            Request = request;
            Joined = joined;
            Detail = detail ?? string.Empty;
        }

        public string Kind { get; }
        public string WorkerId { get; }
        public string FacilityId { get; }
        public string TargetEntityId { get; }
        public WorkPhaseResult Phase { get; }
        public WorkExecutionResult Terminal { get; }
        public GameplayOutcomeSnapshot Child { get; }
        public WorkCompletionIdentityDeliveryRequest Request { get; }
        public bool Joined { get; }
        public string Detail { get; }
        public string ChildResultKey => Terminal?.ChildResultKey ?? string.Empty;
    }

    private sealed class WorkPhaseResult
    {
        public WorkPhaseResult(WorkProbeFault fault) { Fault = fault; }
        public WorkProbeFault Fault { get; }
        public bool Started;
        public bool Progressed;
        public bool CompletionTargetRemoved;
        public bool CompletionAuthoritySatisfied;
        public bool TerminalRequested;
        public bool TerminalObserved;
        public bool TypedTerminal;
        public bool LifecycleConserved;
        public bool PathsConserved;
        public bool ReservationsConserved;
        public bool NoInvariantAnomaly;
        public long StartEpoch;
        public long ObservedEpoch;
        public long ProgressDelta;
        public long ActionStarts;
        public long ActionTerminals;
        public long Cancelled;
        public long Failed;
        public long PathRequests;
        public long PathResults;
        public long ReservationAcquires;
        public long ReservationReleases;
        public long GameplayProgressDelta;
        public int PreStartDeferredWaits;
        public float EstimatedTravelSeconds;
        public bool MovementObserved;
        public bool MovementStalled;
        public bool PauseObserved;
        public bool PauseBillUnchanged;
        public bool PauseLaborUnchanged;
        public bool PauseApprovedRevisionUnchanged;
        public bool PauseWorkIdentityUnchanged;
        public bool ResumedSameWorkProgress;
        public int PauseFrameCount;
        public float PauseBillWorkBefore;
        public float PauseBillWorkAfter;
        public long PauseLaborRevisionBefore;
        public long PauseLaborRevisionAfter;
        public Vector2Int MovementStartPosition;
        public Vector2Int MovementEndPosition;
        public CharacterAiActionTerminalKind ObservedTerminalKind;
        public string ObservedWorkType = string.Empty;
        public string ObservedActionType = string.Empty;
        public string PriorityCommandDetail = "direct";
        public string StageBeforeTerminalRequest = string.Empty;
        public string DomainTrace = string.Empty;
        public string Blocker = string.Empty;
        public CharacterAiRuntimeGateSnapshot StartGate;
        public CharacterAiRuntimeGateSnapshot EndGate;

        public bool Passed => Started
            && (Fault == WorkProbeFault.InvalidateTargetAfterStart || Progressed)
            && (Fault != WorkProbeFault.PauseAfterApprovedProgress
                || PauseObserved
                && PauseBillUnchanged
                && PauseLaborUnchanged
                && PauseApprovedRevisionUnchanged
                && PauseWorkIdentityUnchanged
                && ResumedSameWorkProgress)
            && (Fault != WorkProbeFault.CompleteNormally
                || CompletionAuthoritySatisfied)
            && TerminalRequested
            && TerminalObserved
            && TypedTerminal
            && ObservedEpoch > StartEpoch
            && LifecycleConserved
            && PathsConserved
            && ReservationsConserved
            && NoInvariantAnomaly
            && string.IsNullOrEmpty(Blocker);

        public string Format() =>
            Fault + ":pass=" + Passed
            + "; epoch=" + StartEpoch + "->" + ObservedEpoch
            + "; workType=" + ObservedWorkType
            + "; actionType=" + ObservedActionType
            + "; priorityCommand=" + PriorityCommandDetail
            + "; progress=" + ProgressDelta
            + "; completionTargetRemoved=" + CompletionTargetRemoved
            + "; completionAuthority=" + CompletionAuthoritySatisfied
            + "; action=" + ActionStarts + "/" + ActionTerminals
            + "; terminal=" + Cancelled + "C/" + Failed + "F"
            + "; terminalEpochKind=" + ObservedTerminalKind
            + "; path=" + PathRequests + "/" + PathResults
            + "; reservation=" + ReservationAcquires + "/" + ReservationReleases
            + "; movement=" + MovementObserved + "/" + MovementStalled
            + "@" + MovementStartPosition + "->" + MovementEndPosition
            + (Fault != WorkProbeFault.PauseAfterApprovedProgress
                ? string.Empty
                : "; pause=" + PauseObserved
                    + "; frames=" + PauseFrameCount
                    + "; billExact=" + PauseBillUnchanged
                    + "@" + PauseBillWorkBefore.ToString("0.######")
                    + "->" + PauseBillWorkAfter.ToString("0.######")
                    + "; laborExact=" + PauseLaborUnchanged
                    + "@" + PauseLaborRevisionBefore
                    + "->" + PauseLaborRevisionAfter
                    + "; approvedExact="
                    + PauseApprovedRevisionUnchanged
                    + "; workIdentityExact="
                    + PauseWorkIdentityUnchanged
                    + "; resumedSameWork=" + ResumedSameWorkProgress)
            + "; gameplayProgress=" + GameplayProgressDelta
            + "; preStartDeferredWaits=" + PreStartDeferredWaits
            + "; etaCap=" + EstimatedTravelSeconds.ToString("0.###")
            + "; conserve=" + LifecycleConserved + "/" + PathsConserved
            + "/" + ReservationsConserved
            + "; invariant=" + NoInvariantAnomaly
            + (string.IsNullOrEmpty(StageBeforeTerminalRequest)
                ? string.Empty
                : "; preTerminal=" + StageBeforeTerminalRequest)
            + (string.IsNullOrEmpty(DomainTrace)
                ? string.Empty
                : "; domainTrace=" + DomainTrace)
            + "; blocker=" + (string.IsNullOrEmpty(Blocker) ? "none" : Blocker);
    }

    private readonly struct WorkTypeLiveRow
    {
        public WorkTypeLiveRow(
            string workTypeId,
            string status,
            string target,
            string detail)
        {
            WorkTypeId = workTypeId ?? string.Empty;
            Status = status ?? string.Empty;
            Target = target ?? string.Empty;
            Detail = (detail ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ');
        }

        public string WorkTypeId { get; }
        public string Status { get; }
        public string Target { get; }
        public string Detail { get; }

        public static WorkTypeLiveRow Blocked(
            WorkTypeId id,
            string blocker,
            string detail) =>
            new WorkTypeLiveRow(id.Value, "BLOCKED", "-", blocker + ":" + detail);

        public static WorkTypeLiveRow Info(string id, string detail) =>
            new WorkTypeLiveRow("info:" + id, "INFO", "-", detail);
    }

    private readonly struct ActorPauseState
    {
        public ActorPauseState(string persistentId, bool wasPaused)
        {
            PersistentId = persistentId ?? string.Empty;
            WasPaused = wasPaused;
        }

        public string PersistentId { get; }
        public bool WasPaused { get; }
    }

    private readonly struct DisplacedMovementSnapshot
    {
        public DisplacedMovementSnapshot(
            BuildableObject building,
            GridLayer layer,
            Vector2Int[] positions,
            bool connectPositions)
        {
            Building = building;
            Layer = layer;
            Positions = positions ?? Array.Empty<Vector2Int>();
            ConnectPositions = connectPositions;
        }

        public BuildableObject Building { get; }
        public GridLayer Layer { get; }
        public Vector2Int[] Positions { get; }
        public bool ConnectPositions { get; }
    }

    private readonly struct FixtureAreaSnapshot
    {
        public FixtureAreaSnapshot(
            Vector2Int position,
            GridCellAreaType areaType)
        {
            Position = position;
            AreaType = areaType;
        }

        public Vector2Int Position { get; }
        public GridCellAreaType AreaType { get; }
    }

    private readonly struct DisplacedWildlifeSnapshot
    {
        public DisplacedWildlifeSnapshot(
            WildlifeActor actor,
            Vector2Int origin,
            Vector2Int destination)
        {
            Actor = actor;
            Origin = origin;
            Destination = destination;
        }

        public WildlifeActor Actor { get; }
        public Vector2Int Origin { get; }
        public Vector2Int Destination { get; }
    }
}
#endif
