#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

public static class IndustrialInfrastructurePlayModeVerifier
{
    public const string ReportPath =
        "Temp/IndustrialInfrastructure/playmode-live-report.txt";
    public const string PowerFuelEvidencePath =
        "Artifacts/QA/industrial-power-fuel-buffer-playmode-report.txt";
    public const string CommandOutcomeEvidencePath =
        "Artifacts/QA/GameplayOutcomeLedgerPhase80IndustrialCommands-20260919-r4/producer-runtime-report.txt";
    public const string ScreenshotPath =
        "Temp/IndustrialInfrastructure/playmode-live.png";
    public const string CommittedInvasionWarningEvidencePath =
        "Artifacts/QA/wim-implementation/wim-047-committed-invasion-warning.txt";
    public const string Wim009TemporarySupplyEvidencePath =
        "Artifacts/QA/wim-implementation/wim-009-temporary-supply-live.txt";

    [MenuItem(
        "DungeonStory/Debug/Infrastructure/Run Live Industrial PlayMode Scenario")]
    public static void Run()
    {
        CreateRunner(powerFuelOnly: false);
    }

    public static void RunPowerFuelOnly()
    {
        CreateRunner(powerFuelOnly: true);
    }

    public static void RunConveyorOnly() => CreateRunner(false, conveyorOnly: true);

    public static void RunConveyorFiltersOnly() => CreateRunner(false, conveyorFiltersOnly: true);

    public static void RunPowerConnectionsOnly() => CreateRunner(true, powerConnectionsOnly: true);

    public static void RunLightingOnly() => CreateRunner(true, lightingOnly: true);
    public static void RunDoorsOnly() => CreateRunner(false, doorsOnly: true);
    public static void RunReturnRestoreOnly() => CreateRunner(false, doorsOnly: true, returnRestoreOnly: true);
    public static void RunDoorConsumersOnly() => CreateRunner(false, doorConsumersOnly: true);
    public static void RunDoorConsumersRemainingOnly() =>
        CreateRunner(false, doorConsumersRemainingOnly: true);
    public static void RunCommittedInvasionWarningOnly() =>
        CreateRunner(false, committedInvasionWarningOnly: true);

    public static void RunWim009TemporarySupplyOnly() =>
        CreateRunner(false, temporarySupplyOnly: true);

    public static void RunInfrastructureCommandOutcomesOnly() =>
        CreateRunner(false, commandOutcomesOnly: true);

    private static void CreateRunner(bool powerFuelOnly, bool conveyorOnly = false, bool conveyorFiltersOnly = false,
        bool powerConnectionsOnly = false, bool lightingOnly = false, bool doorsOnly = false, bool returnRestoreOnly = false,
        bool doorConsumersOnly = false, bool doorConsumersRemainingOnly = false,
        bool committedInvasionWarningOnly = false,
        bool temporarySupplyOnly = false,
        bool commandOutcomesOnly = false)
    {
        if (!Application.isPlaying)
        {
            throw new InvalidOperationException(
                "실제 산업 검증은 Play Mode에서 실행해야 합니다.");
        }

        IndustrialInfrastructurePlayModeVerificationRunner existing =
            UnityEngine.Object.FindFirstObjectByType<
                IndustrialInfrastructurePlayModeVerificationRunner>();
        if (existing != null)
        {
            UnityEngine.Object.Destroy(existing.gameObject);
        }

        GameObject runnerObject = new GameObject(
            "IndustrialInfrastructurePlayModeVerifier");
        IndustrialInfrastructurePlayModeVerificationRunner runner =
            runnerObject.AddComponent<
                IndustrialInfrastructurePlayModeVerificationRunner>();
        runner.PowerFuelOnly = powerFuelOnly;
        runner.ConveyorOnly = conveyorOnly;
        runner.ConveyorFiltersOnly = conveyorFiltersOnly;
        runner.PowerConnectionsOnly = powerConnectionsOnly;
        runner.LightingOnly = lightingOnly;
        runner.DoorsOnly = doorsOnly;
        runner.ReturnRestoreOnly = returnRestoreOnly;
        runner.DoorConsumersOnly = doorConsumersOnly;
        runner.DoorConsumersRemainingOnly = doorConsumersRemainingOnly;
        runner.CommittedInvasionWarningOnly = committedInvasionWarningOnly;
        runner.TemporarySupplyOnly = temporarySupplyOnly;
        runner.CommandOutcomesOnly = commandOutcomesOnly;
    }
}

public sealed class IndustrialInfrastructurePlayModeVerificationRunner :
    MonoBehaviour
{
    public bool PowerFuelOnly { get; set; }
    public bool ConveyorOnly { get; set; }
    public bool ConveyorFiltersOnly { get; set; }
    public bool PowerConnectionsOnly { get; set; }
    public bool LightingOnly { get; set; }
    public bool DoorsOnly { get; set; }
    public bool ReturnRestoreOnly { get; set; }
    public bool DoorConsumersOnly { get; set; }
    public bool DoorConsumersRemainingOnly { get; set; }
    public bool CommittedInvasionWarningOnly { get; set; }
    public bool TemporarySupplyOnly { get; set; }
    public bool CommandOutcomesOnly { get; set; }
    private const string ConveyorDestinationPrefix = "qa:industrial-output:";
    private const string ConveyorBufferOwnerDomain =
        "qa.infrastructure.conveyor";
    private const string ConveyorBufferOperationId =
        "qa:industrial-conveyor-output";
    private const string NormalStackIdPrefix = "qa:normal-stack";
    private const string OverflowPayloadPrefix = "qa:overflow-payload:";
    private const float TimeoutSeconds = 30f;

    private readonly List<BuildableObject> createdBuildings =
        new List<BuildableObject>();
    private readonly List<string> report = new List<string>();

    private DungeonRuntimeLifetimeScope scope;
    private Grid grid;
    private IGridBuildingObjectFactory buildingFactory;
    private IPowerInfrastructureQuery power;
    private IPowerInfrastructureCommand powerCommands;
    private IPowerInfrastructurePersistence powerPersistence;
    private IFluidInfrastructureQuery water;
    private IFluidInfrastructureCommand fluidCommands;
    private IFluidInfrastructurePersistence fluidPersistence;
    private IFluidWastewaterTransaction wastewater;
    private IWaterFixtureUseRuntime fixtures;
    private IConveyorInfrastructureQuery conveyor;
    private IConveyorInfrastructureCommand conveyorCommands;
    private IConveyorPayloadTransaction conveyorTransactions;
    private IConveyorInfrastructurePersistence conveyorPersistence;
    private IAutomationInfrastructureQuery automation;
    private IAutomationInfrastructureCommand automationCommands;
    private IAutomationInfrastructurePersistence automationPersistence;
    private IInfrastructureCommandOutcomePersistence commandOutcomePersistence;
    private IGameplayOutcomeQuery gameplayOutcomes;
    private IWorldItemStackRuntime items;
    private IItemTransferService itemTransfers;
    private IDungeonSaveSectionRegistry saveSections;
    private IFacilityBufferPhysicalOccupancyQuery bufferOccupancy;
    private IFacilityBufferDestinationLifecycleCommand bufferLifecycle;
    private IFacilityBufferDestinationClaimAuthorityQuery bufferClaimAuthority;
    private IFacilityBufferMassCapacityAuthorityQuery bufferCapacityAuthority;
    private IFacilityBufferMassCapacityQuery bufferCapacity;
    private IPhysicalItemMassQuery itemMass;
    private IGameClock clock;
    private GameManager gameManager;
    private OwnerSelectionPanel ownerSelection;
    private Camera mainCamera;
    private CharacterActor fuelHauler;
    private Vector3 originalCameraPosition;
    private float originalCameraSize;
    private float originalTimeScale;
    private bool originalPause;
    private bool ownerSelectionWasActive;
    private bool originalFuelHaulerAiPause;
    private Vector3 originalFuelHaulerWorldPosition;
    private string originalFuelHaulerPersistentId = string.Empty;

    private DungeonPowerInfrastructureSaveData originalPower;
    private DungeonFluidInfrastructureSaveData originalFluid;
    private DungeonConveyorInfrastructureSaveData originalConveyor;
    private DungeonAutomationSaveData originalAutomation;
    private DungeonPhysicalItemSaveData originalItems;
    private List<DungeonSaveSectionEnvelope> originalWorld;
    private bool originalStateCaptured;
    private bool exitPlayModeOnCompletion;
    private DungeonAutosaveService isolatedAutosave;
    private MetaProfilePersistenceService isolatedMetaPersistence;
    private readonly Dictionary<string, string> realPersistenceBefore =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private string conveyorDestination = string.Empty;
    private FacilityBufferDestinationClaim[] originalConveyorBufferClaims =
        Array.Empty<FacilityBufferDestinationClaim>();
    private FacilityBufferCapacityProfile[] originalConveyorBufferProfiles =
        Array.Empty<FacilityBufferCapacityProfile>();
    private Exception verificationFailure;
    private readonly List<Exception> cleanupFailures = new();

    private IEnumerator Start()
    {
        yield return ExecuteGuarded(RunVerification());

        Cleanup();
        Exception terminalFailure = verificationFailure
            ?? cleanupFailures.FirstOrDefault();
        bool passed = terminalFailure == null;
        report.Add(passed ? "result=PASS" : "result=FAIL");
        if (!passed)
        {
            report.Add("failure=" + terminalFailure.Message);
            if (verificationFailure != null)
                Debug.LogException(verificationFailure);
            foreach (Exception cleanupFailure in cleanupFailures)
                Debug.LogException(cleanupFailure);
        }

        try
        {
            WriteReport();
            if (passed)
            {
                Debug.Log(
                    "Live industrial PlayMode verification passed. "
                    + IndustrialInfrastructurePlayModeVerifier.ReportPath);
            }
            else
            {
                Debug.LogError(
                    "Live industrial PlayMode verification failed: "
                    + terminalFailure);
            }
        }
        finally
        {
            if (exitPlayModeOnCompletion)
            {
                EditorApplication.ExitPlaymode();
            }
            Destroy(gameObject);
        }
    }

    private IEnumerator RunVerification()
    {
            if (CommittedInvasionWarningOnly
                || TemporarySupplyOnly
                || CommandOutcomesOnly)
            {
                // Dedicated batch verification must leave both the live world
                // and the user's durable profile/save slots unchanged.
                exitPlayModeOnCompletion = true;
            }
            yield return ResolveRuntime();
            if (CommittedInvasionWarningOnly
                || TemporarySupplyOnly
                || CommandOutcomesOnly)
            {
                IsolateCommittedInvasionPersistence();
            }
            yield return EnsurePlayableRun();
            if (CommittedInvasionWarningOnly)
            {
                EstablishCommittedInvasionRestorableBaseline();
            }
            Require(scope.Container.Resolve<IGridSystemProvider>().TryGetGrid(out grid),
                "시작 인원 확정 이후 현재 Grid를 찾지 못했습니다.");
            fuelHauler = FindHauler();
            Require(fuelHauler != null,
                "발전기 exact-stack 운반을 실행할 실제 캐릭터가 없습니다.");
            CaptureOriginalState();
            VerifyDetachedRestorePreparation();
            ConfigureVerificationTime();
            SetOwnerSelectionVisible(false);

            if (CommittedInvasionWarningOnly)
            {
                yield return WimCommittedInvasionWarningScenario.Run(
                    scope,
                    saveSections,
                    report);
                report.Add("mode=committed-invasion-warning-only");
                yield break;
            }
            if (DoorsOnly)
            {
                yield return WimDoorOperationScenario.Run(scope, fuelHauler, grid, PlaceBuilding, report, ReturnRestoreOnly);
                report.Add("mode=doors-only-no-power-fixture");
                yield break;
            }
            if (DoorConsumersOnly)
            {
                yield return WimDoorConsumerScenario.Run(scope, fuelHauler, grid, PlaceBuilding, report);
                report.Add("mode=door-consumers-only-no-prior-door-or-return-suite");
                yield break;
            }
            if (DoorConsumersRemainingOnly)
            {
                yield return WimDoorConsumerScenario.RunRemaining(
                    scope, fuelHauler, grid, PlaceBuilding, report);
                report.Add("mode=door-consumers-remaining-only-restock-and-launch");
                yield break;
            }

            Dictionary<string, BuildingSO> assets = LoadAssets(
                LightingOnly,
                CommandOutcomesOnly);
            if (TemporarySupplyOnly)
            {
                Require(assets.ContainsKey("I04"),
                    "WIM009 temporary-supply verification requires authored I04 battery.");
            }
            BuildingSO automationAsset = LoadAutomationFacility();
            Require(automationAsset != null,
                "자동화 모듈이 부착된 생산 시설 자산이 없습니다.");
            Vector2Int origin = FindScenarioOrigin(
                assets,
                automationAsset);
            PlaceScenario(origin, assets, automationAsset);
            FocusCamera(origin);

            yield return WaitUntil(
                () => power.Networks.Count > 0
                    && water.Networks.Count > 0
                    && conveyor.Networks.Count >= 2,
                "실제 배치 이후 기반 시설 토폴로지가 생성되지 않았습니다.");

            if (CommandOutcomesOnly)
            {
                InfrastructureCommandOutcomeProducerPlayModeScenario.Verify(
                    createdBuildings,
                    power,
                    powerCommands,
                    powerPersistence,
                    fluidCommands,
                    fluidPersistence,
                    conveyorCommands,
                    conveyorPersistence,
                    automation,
                    automationCommands,
                    automationPersistence,
                    commandOutcomePersistence,
                    gameplayOutcomes,
                    items,
                    itemTransfers,
                    itemMass,
                    (destinationId,
                        output,
                        outputFacilityId,
                        outputDropPosition,
                        capacity,
                        capacityRevision) =>
                    {
                        conveyorDestination = destinationId;
                        PublishConveyorOutputAuthority(
                            output,
                            outputFacilityId,
                            outputDropPosition,
                            capacity,
                            capacityRevision);
                    },
                    report);
                report.Add("mode=infrastructure-command-outcomes-only");
                yield break;
            }

            if (TemporarySupplyOnly)
            {
                string[] fixtureBuildingIds = createdBuildings
                    .Select(GetNodeId)
                    .ToArray();
                yield return Wim009TemporarySupplyLiveScenario.Run(
                    scope,
                    FindBuilding("I03"),
                    FindBuilding("I04"),
                    FindBuilding("I07"),
                    FindBuilding("I08"),
                    report);
                Require(scope.Container.Resolve<IGridSystemProvider>()
                        .TryGetGrid(out grid),
                    "WIM009 whole-save restore did not republish the current Grid.");
                createdBuildings.Clear();
                foreach (string fixtureId in fixtureBuildingIds)
                {
                    Require(TryResolveLiveBuilding(
                            fixtureId,
                            out BuildableObject restoredBuilding),
                        "WIM009 whole-save restore lost fixture " + fixtureId);
                    createdBuildings.Add(restoredBuilding);
                }
                report.Add("mode=wim-009-temporary-supply-only");
                yield break;
            }

            yield return VerifyPowerAndFluids();
            if (LightingOnly)
                yield return WimLightingSupplyScenario.Run(scope, FindBuilding("I15"), FindBuilding("E01"), report);
            if (PowerConnectionsOnly)
                yield return WimPowerConnectionScenario.Run(scope, FindBuilding("I03"),
                    createdBuildings.First(building => building.BuildingData.GetAbility<BuildingAutomationAbility>() != null),
                    FindBuilding("U04"), report);
            if (PowerFuelOnly)
            {
                report.Add("mode=power-fuel-only");
                yield break;
            }
            if (ConveyorFiltersOnly)
            {
                BuildableObject filterOutput = FindBuilding("C03");
                conveyorDestination = ConveyorDestinationPrefix + GetNodeId(filterOutput);
                PublishConveyorOutputAuthority(filterOutput, GetNodeId(filterOutput),
                    filterOutput.BuildingData.GetGridPosList(filterOutput.centerPos).First(),
                    itemMass.GetQuantityMass((ItemDefinitionId)"material:lumber",
                        PhysicalItemMassSubject.ForDefinition((ItemDefinitionId)"material:lumber"), 20), 1L);
                Require(conveyorCommands.SetPortDestination(filterOutput, conveyorDestination).Succeeded,
                    "Cannot set filter-test physical output.");
                yield return WimConveyorFilterReserveScenario.Run(scope, FindBuilding("C02"), filterOutput,
                    FindBuilding("C09"), conveyorDestination, report);
                report.Add("mode=conveyor-filter-reserve-only");
                yield break;
            }
            yield return VerifyConveyorTransport(origin);
            if (ConveyorOnly)
            {
                yield return VerifyAuthoredFuelDestination(origin);
                report.Add("mode=conveyor-destination-only");
                yield break;
            }
            yield return VerifyAutomation();
            yield return VerifyDeadlockAndOverflow();
            yield return CaptureVisual();
    }

    private IEnumerator ExecuteGuarded(IEnumerator root)
    {
        Stack<IEnumerator> routines = new Stack<IEnumerator>();
        routines.Push(root);
        while (routines.Count > 0 && verificationFailure == null)
        {
            IEnumerator routine = routines.Peek();
            bool moved = false;
            object current = null;
            try
            {
                moved = routine.MoveNext();
                if (moved)
                {
                    current = routine.Current;
                }
            }
            catch (Exception exception)
            {
                verificationFailure = exception;
            }

            if (verificationFailure != null)
            {
                break;
            }

            if (!moved)
            {
                (routine as IDisposable)?.Dispose();
                routines.Pop();
                continue;
            }

            if (current is IEnumerator nested)
            {
                routines.Push(nested);
                continue;
            }

            yield return current;
        }

        while (routines.Count > 0)
        {
            (routines.Pop() as IDisposable)?.Dispose();
        }
    }

    private IEnumerator ResolveRuntime()
    {
        float startedAt = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - startedAt < TimeoutSeconds)
        {
            scope = UnityEngine.Object.FindFirstObjectByType<
                DungeonRuntimeLifetimeScope>();
            if (scope?.Container != null)
            {
                break;
            }

            yield return null;
        }

        Require(scope?.Container != null,
            "DungeonRuntimeLifetimeScope가 준비되지 않았습니다.");
        IGridSystemProvider gridProvider =
            scope.Container.Resolve<IGridSystemProvider>();
        Require(gridProvider.TryGetGrid(out grid) && grid != null,
            "플레이 중인 물리 Grid를 찾지 못했습니다.");

        buildingFactory =
            scope.Container.Resolve<IGridBuildingObjectFactory>();
        power = scope.Container.Resolve<IPowerInfrastructureQuery>();
        powerCommands = scope.Container.Resolve<IPowerInfrastructureCommand>();
        powerPersistence =
            scope.Container.Resolve<IPowerInfrastructurePersistence>();
        water = scope.Container.Resolve<IFluidInfrastructureQuery>();
        fluidCommands = scope.Container.Resolve<IFluidInfrastructureCommand>();
        fluidPersistence =
            scope.Container.Resolve<IFluidInfrastructurePersistence>();
        wastewater =
            scope.Container.Resolve<IFluidWastewaterTransaction>();
        fixtures = scope.Container.Resolve<IWaterFixtureUseRuntime>();
        conveyor = scope.Container.Resolve<IConveyorInfrastructureQuery>();
        conveyorCommands =
            scope.Container.Resolve<IConveyorInfrastructureCommand>();
        conveyorTransactions =
            scope.Container.Resolve<IConveyorPayloadTransaction>();
        conveyorPersistence =
            scope.Container.Resolve<IConveyorInfrastructurePersistence>();
        automation =
            scope.Container.Resolve<IAutomationInfrastructureQuery>();
        automationCommands =
            scope.Container.Resolve<IAutomationInfrastructureCommand>();
        automationPersistence =
            scope.Container.Resolve<IAutomationInfrastructurePersistence>();
        commandOutcomePersistence = scope.Container.Resolve<
            IInfrastructureCommandOutcomePersistence>();
        gameplayOutcomes = scope.Container.Resolve<IGameplayOutcomeQuery>();
        items = scope.Container.Resolve<IWorldItemStackRuntime>();
        itemTransfers = scope.Container.Resolve<IItemTransferService>();
        saveSections = scope.Container.Resolve<IDungeonSaveSectionRegistry>();
        bufferOccupancy = scope.Container.Resolve<
            IFacilityBufferPhysicalOccupancyQuery>();
        bufferLifecycle = scope.Container.Resolve<
            IFacilityBufferDestinationLifecycleCommand>();
        bufferClaimAuthority = scope.Container.Resolve<
            IFacilityBufferDestinationClaimAuthorityQuery>();
        bufferCapacityAuthority = scope.Container.Resolve<
            IFacilityBufferMassCapacityAuthorityQuery>();
        bufferCapacity = scope.Container.Resolve<
            IFacilityBufferMassCapacityQuery>();
        itemMass = scope.Container.Resolve<IPhysicalItemMassQuery>();
        clock = scope.Container.Resolve<IGameClock>();
        gameManager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        ownerSelection =
            UnityEngine.Object.FindFirstObjectByType<OwnerSelectionPanel>();
        mainCamera = Camera.main;
    }

    private IEnumerator EnsurePlayableRun()
    {
        OwnerRunManager ownerManager =
            UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        if (ownerManager == null || ownerManager.CurrentOwnerActor == null)
        {
            report.Add("fastPartyCommit="
                + StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug());
            for (int frame = 0; frame < 8; frame++)
                yield return null;
        }

        ownerManager = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        Require(ownerManager != null && ownerManager.CurrentOwnerActor != null,
            "산업 PlayMode 검증용 실제 플레이 파티를 준비하지 못했습니다.");
    }

    private void CaptureOriginalState()
    {
        originalWorld = saveSections.CaptureAll();
        originalPower = powerPersistence.Capture();
        originalFluid = fluidPersistence.Capture();
        originalConveyor = conveyorPersistence.Capture();
        originalAutomation = automationPersistence.Capture();
        originalItems = items.Capture();
        originalConveyorBufferClaims = bufferClaimAuthority
            .CaptureAuthorityClaims()
            .Where(value => string.Equals(
                value.OwnerDomain,
                ConveyorBufferOwnerDomain,
                StringComparison.Ordinal))
            .ToArray();
        originalConveyorBufferProfiles = bufferCapacityAuthority
            .CaptureAuthorityProfiles()
            .Where(value => string.Equals(
                value.OwnerDomain,
                ConveyorBufferOwnerDomain,
                StringComparison.Ordinal))
            .ToArray();
        originalStateCaptured = true;
        originalTimeScale = Time.timeScale;
        originalPause = gameManager != null && gameManager.isPause;
        ownerSelectionWasActive =
            ownerSelection != null && ownerSelection.gameObject.activeSelf;
        originalFuelHaulerPersistentId =
            CharacterPersistentIdentity.Require(fuelHauler).Value;
        originalFuelHaulerAiPause = fuelHauler.IsAiPaused();
        originalFuelHaulerWorldPosition = fuelHauler.transform.position;
        if (mainCamera != null)
        {
            originalCameraPosition = mainCamera.transform.position;
            originalCameraSize = mainCamera.orthographicSize;
        }
    }

    private void VerifyDetachedRestorePreparation()
    {
        string powerBefore = JsonUtility.ToJson(powerPersistence.Capture());
        string fluidBefore = JsonUtility.ToJson(fluidPersistence.Capture());
        string conveyorBefore = JsonUtility.ToJson(
            conveyorPersistence.Capture());
        string automationBefore = JsonUtility.ToJson(
            automationPersistence.Capture());

        Require(powerPersistence.PrepareRestore(originalPower) != null
                && fluidPersistence.PrepareRestore(originalFluid) != null
                && conveyorPersistence.PrepareRestore(originalConveyor) != null
                && automationPersistence.PrepareRestore(originalAutomation)
                    != null,
            "Industrial restore preparation did not produce detached candidates.");
        Require(powerBefore == JsonUtility.ToJson(powerPersistence.Capture())
                && fluidBefore == JsonUtility.ToJson(
                    fluidPersistence.Capture())
                && conveyorBefore == JsonUtility.ToJson(
                    conveyorPersistence.Capture())
                && automationBefore == JsonUtility.ToJson(
                    automationPersistence.Capture()),
            "Industrial restore preparation mutated live runtime state.");

        ConveyorInfrastructureSaveSection section =
            new ConveyorInfrastructureSaveSection(conveyorPersistence);
        DungeonGameRestoreReport invalid = new DungeonGameRestoreReport();
        section.ValidatePayload(
            string.Empty,
            DungeonConveyorInfrastructureSaveData.CurrentVersion,
            invalid);
        Require(!invalid.Success
                && conveyorBefore == JsonUtility.ToJson(
                    conveyorPersistence.Capture()),
            "Invalid conveyor preflight mutated live runtime state.");
    }

    private void ConfigureVerificationTime()
    {
        if (gameManager != null)
        {
            gameManager.isPause = false;
        }

        Time.timeScale = 5f;
        fuelHauler.SetAiPaused(true);
        fuelHauler.Brain?.StopAllAiForLifecycleTransition(
            "qa-industrial-power-fuel-isolation");
        fuelHauler.GetComponent<AbilityMove>()?.CancelActiveMovement();
        fuelHauler.GetComponent<AbilityHaul>()?.StopHauling(
            "qa-industrial-power-fuel-isolation");
    }

    private void SetOwnerSelectionVisible(bool visible)
    {
        if (ownerSelection != null)
        {
            ownerSelection.gameObject.SetActive(visible);
        }
    }

    private static Dictionary<string, BuildingSO> LoadAssets(
        bool includeLighting,
        bool includeCommandOutcomeFixtures)
    {
        string[] requiredCodes =
        {
            "U04",
            "I03",
            "I07",
            "I08",
            "I09",
            "I14",
            "C01R",
            "C01L",
            "C01U",
            "C01D",
            "C02",
            "C03",
            "C04",
            "C09"
        };
        Dictionary<string, BuildingSO> assets =
            AssetDatabase.FindAssets(
                    "t:BuildingSO",
                    new[] { "Assets/Resources/SO/Building/Industrial" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
                .Where(asset => asset != null)
                .Select(asset => new
                {
                    Asset = asset,
                    Code = asset.GetAbility<
                        BuildingFacilityPartAbility>()?.code
                })
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Code))
                .ToDictionary(
                    entry => entry.Code,
                    entry => entry.Asset,
                    StringComparer.Ordinal);
        foreach (string code in requiredCodes)
        {
            Require(assets.ContainsKey(code),
                $"산업 검증 자산 {code}를 찾지 못했습니다.");
        }

        if (includeCommandOutcomeFixtures)
        {
            Require(assets.ContainsKey("I05"),
                "Authored circuit breaker fixture is missing.");
            Require(assets.ContainsKey("I10"),
                "Authored water-transfer fixture is missing.");
        }

        if (includeLighting)
        {
            Require(assets.ContainsKey("I15"), "Authored electric arc lamp missing.");
            BuildingSO torch = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/Modular/E01_벽횃불.asset");
            Require(torch != null && torch.GetAbility<BuildingLightingAbility>() != null
                && torch.GetAbility<BuildingFuelConsumerAbility>() != null, "Authored fuel torch missing.");
            assets.Add("E01", torch);
        }
        return assets;
    }

    private static BuildingSO LoadAutomationFacility()
    {
        return AssetDatabase.FindAssets(
                "t:BuildingSO",
                new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(asset => asset != null
                && asset.layer == GridLayer.Building
                && asset.GetAbility<BuildingAutomationAbility>() != null)
            .OrderBy(asset => asset.id)
            .FirstOrDefault();
    }

    private Vector2Int FindScenarioOrigin(
        IReadOnlyDictionary<string, BuildingSO> assets,
        BuildingSO automationAsset)
    {
        Vector2Int[] positions = grid.GetCells()
            .Where(cell => cell != null)
            .Select(cell => cell.Position)
            .ToArray();
        Require(positions.Length > 0, "Grid에 검증 가능한 셀이 없습니다.");
        int minX = positions.Min(position => position.x);
        int maxX = positions.Max(position => position.x);
        int minY = positions.Min(position => position.y);
        int maxY = positions.Max(position => position.y);
        for (int y = minY; y <= maxY - 2; y++)
        {
            for (int x = minX; x <= maxX - 11; x++)
            {
                Vector2Int origin = new Vector2Int(x, y);
                if (CanPlaceScenario(origin, assets, automationAsset))
                {
                    return origin;
                }
            }
        }

        if (CommandOutcomesOnly)
        {
            return ExpandCommandOutcomeScenarioGrid(
                assets,
                automationAsset);
        }

        throw new InvalidOperationException(
            "실제 플레이 Grid에서 12x3 산업 검증 구역을 확보하지 못했습니다.");
    }

    private Vector2Int ExpandCommandOutcomeScenarioGrid(
        IReadOnlyDictionary<string, BuildingSO> assets,
        BuildingSO automationAsset)
    {
        GridSystemManager manager = UnityEngine.Object.FindFirstObjectByType<
            GridSystemManager>();
        Require(manager != null,
            "Command-outcome fixture cannot resolve the live grid manager.");

        Grid original = grid;
        const int fixtureWidth = 12;
        Grid expanded = original.TryExpandGrid(fixtureWidth, 0);
        Require(expanded != null,
            "Command-outcome fixture could not allocate a temporary grid strip.");
        Vector2Int origin = new Vector2Int(original.width, 0);
        for (int x = origin.x; x < origin.x + fixtureWidth; x++)
        {
            for (int y = 0; y < expanded.height; y++)
            {
                expanded.SetAreaType(
                    new Vector2Int(x, y),
                    GridCellAreaType.DungeonInterior);
            }
        }

        Require(manager.TryPublishGrid(
                original,
                expanded,
                out string failureReason),
            "Command-outcome fixture could not publish its temporary grid: "
            + failureReason);
        manager.CompleteGridPublication();
        grid = expanded;
        Require(CanPlaceScenario(origin, assets, automationAsset),
            "Temporary command-outcome grid strip cannot host the authored fixtures.");
        report.Add("scenarioSpace=temporary-expanded-grid;previousWidth="
            + original.width + ";newWidth=" + expanded.width);
        return origin;
    }

    private bool CanPlaceScenario(
        Vector2Int origin,
        IReadOnlyDictionary<string, BuildingSO> assets,
        BuildingSO automationAsset)
    {
        List<(BuildingSO Asset, Vector2Int Position)> placements =
            CreatePlacementPlan(origin, assets, automationAsset);
        HashSet<(GridLayer Layer, Vector2Int Position)> claimed =
            new HashSet<(GridLayer Layer, Vector2Int Position)>();
        foreach ((BuildingSO asset, Vector2Int position) in placements)
        {
            foreach (Vector2Int cellPosition in asset.GetGridPosList(position))
            {
                GridCell cell = grid.GetGridCell(cellPosition);
                if (cell == null
                    || cell.AreaType == GridCellAreaType.BlockedExterior
                    || !cell.CanOccupy(asset.layer)
                    || !claimed.Add((asset.layer, cellPosition)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void PlaceScenario(
        Vector2Int origin,
        IReadOnlyDictionary<string, BuildingSO> assets,
        BuildingSO automationAsset)
    {
        foreach ((BuildingSO asset, Vector2Int position) in
                 CreatePlacementPlan(origin, assets, automationAsset))
        {
            PlaceBuilding(asset, position);
        }

        conveyorCommands.MarkTopologyDirty();
        report.Add($"origin={origin.x},{origin.y}");
        report.Add($"placedBuildings={createdBuildings.Count}");
    }

    private List<(BuildingSO Asset, Vector2Int Position)>
        CreatePlacementPlan(
            Vector2Int origin,
            IReadOnlyDictionary<string, BuildingSO> assets,
            BuildingSO automationAsset)
    {
        List<(BuildingSO, Vector2Int)> result =
            new List<(BuildingSO, Vector2Int)>();
        void Add(string code, int x, int y) =>
            result.Add((assets[code], origin + new Vector2Int(x, y)));

        Add("C02", 4, 0);
        for (int x = 5; x <= 9; x++)
        {
            Add("C01R", x, 0);
        }
        Add("C03", 10, 0);

        Add("C01R", 0, 1);
        Add("C04", 1, 1);
        Add("C09", 2, 1);
        Add("C01D", 0, 2);
        Add("C01L", 1, 2);

        Add("I03", 0, 0);
        if (TemporarySupplyOnly)
        {
            Add("I04", 3, 1);
        }
        if (CommandOutcomesOnly)
        {
            Add("I05", 4, 1);
            Add("I10", 6, 1);
        }
        Add("I07", 3, 0);
        Add("I08", 5, 0);
        Add("I14", 7, 0);
        Add("I09", 9, 0);
        if (LightingOnly)
        {
            Add("I15", 6, 1);
            Add("E01", 8, 1);
        }
        result.Add((automationAsset, origin + new Vector2Int(11, 0)));

        HashSet<Vector2Int> utilityCells = new HashSet<Vector2Int>();
        for (int x = 0; x <= 11; x++)
        {
            utilityCells.Add(origin + new Vector2Int(x, 0));
        }
        for (int x = 0; x <= 2; x++)
        {
            utilityCells.Add(origin + new Vector2Int(x, 1));
        }
        if (TemporarySupplyOnly)
        {
            utilityCells.Add(origin + new Vector2Int(3, 1));
            utilityCells.Add(origin + new Vector2Int(4, 1));
        }
        if (CommandOutcomesOnly)
        {
            for (int x = 4; x <= 7; x++)
            {
                utilityCells.Add(origin + new Vector2Int(x, 1));
            }
        }
        for (int x = 0; x <= 1; x++)
        {
            utilityCells.Add(origin + new Vector2Int(x, 2));
        }

        foreach (Vector2Int utilityCell in utilityCells
                     .OrderBy(cell => cell.y)
                     .ThenBy(cell => cell.x))
        {
            result.Add((assets["U04"], utilityCell));
        }

        return result;
    }

    private BuildableObject PlaceBuilding(
        BuildingSO asset,
        Vector2Int position)
    {
        BuildableObject building = buildingFactory.Create(
            grid,
            asset,
            position);
        Require(building != null,
            $"{asset.objectName} 런타임 오브젝트 생성에 실패했습니다.");
        foreach (MonoBehaviour component in
                 building.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component != null)
            {
                scope.Container.Inject(component);
            }
        }

        building.SetGrid(grid);
        building.Initialization(asset, position);
        bool registered = grid.RegisterOccupant(
            building,
            asset.layer,
            asset.GetGridPosList(position),
            asset.Placement.IsMovement);
        Require(registered,
            $"{asset.objectName}을 실제 Grid에 등록하지 못했습니다.");
        createdBuildings.Add(building);
        return building;
    }

    private IEnumerator VerifyPowerAndFluids()
    {
        BuildableObject generator = FindBuilding("I03");
        BuildableObject pump = FindBuilding("I07");
        BuildableObject cleanTank = FindBuilding("I08");
        BuildableObject shower = FindBuilding("I14");
        BuildableObject wastewaterTank = FindBuilding("I09");
        string generatorNodeId = GetNodeId(generator);
        string generatorDestination = "power:" + generatorNodeId;
        string pumpNodeId = GetNodeId(pump);
        Vector2Int generatorAccess = new[]
            {
                generator.centerPos + Vector2Int.right,
                generator.centerPos + Vector2Int.left,
                generator.centerPos + Vector2Int.up,
                generator.centerPos + Vector2Int.down
            }
            .First(position => grid.IsValidGridPos(position)
                && grid.IsWalkable(position));
        Vector2Int fuelSourcePosition = grid.SearchPath(generatorAccess)
            .GetReachablePositions()
            .Where(position => grid.IsValidGridPos(position)
                && grid.IsWalkable(position))
            .Where(position =>
            {
                int distance = Mathf.Abs(position.x - generator.centerPos.x)
                    + Mathf.Abs(position.y - generator.centerPos.y);
                return distance >= 3 && distance <= 8;
            })
            .OrderByDescending(position =>
                Mathf.Abs(position.x - generator.centerPos.x)
                + Mathf.Abs(position.y - generator.centerPos.y))
            .ThenBy(position => position.y)
            .ThenBy(position => position.x)
            .FirstOrDefault();
        if (!grid.IsValidGridPos(fuelSourcePosition)
            || !grid.IsWalkable(fuelSourcePosition))
        {
            fuelSourcePosition = generatorAccess;
        }
        fuelHauler.transform.position = grid.GetWorldPos(fuelSourcePosition);
        fuelHauler.Brain?.ClearPathSearchCache();
        Require(items.SpawnItemAt(
                "resource:mana-crystal",
                1,
                fuelSourcePosition,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawnedFuel)
            && spawnedFuel == 1,
            "마나 발전기 운반용 실제 연료 스택을 생성하지 못했습니다.");
        WorldItemStackSnapshot fuelSource = items.GetStacksAt(fuelSourcePosition)
            .Single(value => string.Equals(
                value.ItemId,
                "resource:mana-crystal",
                StringComparison.Ordinal));
        Require(!items.TryRouteStackToDestination(
                fuelSource.StackId,
                WorldItemStackState.FacilityBuffer,
                generatorDestination,
                generator.centerPos,
                out string rawRouteFailure)
            && rawRouteFailure.Contains(
                "facility_buffer_managed_route_required",
                StringComparison.Ordinal),
            "발전기 연료가 공통 질량 admission을 우회해 직접 라우팅됐습니다.");
        report.Add(
            "powerRawRoute=REJECTED:facility_buffer_managed_route_required");
        Require(items.TryRequestStackDelivery(
                fuelSource.StackId,
                1,
                generator.centerPos,
                generatorDestination,
                out int requestedFuel,
                out string requestFailure)
            && requestedFuel == 1,
            "발전기 exact-stack 연료 운반 요청이 실패했습니다: "
            + requestFailure);

        AIHaul fuelHaulAction = ScriptableObject.CreateInstance<AIHaul>();
        try
        {
            yield return WaitUntil(() => fuelHaulAction.CanStart(fuelHauler),
                "발전기 exact-stack 요청을 실제 AIHaul이 선택하지 못했습니다: "
                + DescribePowerFuelHaul(generatorDestination));
            AbilityHaul.Ensure(fuelHauler);
            fuelHaulAction.Execute(fuelHauler);
            yield return WaitUntil(
                () => items.CaptureHaulDeliveryIntentsByDestination(
                        generatorDestination)
                    .Any(intent => intent.HasCommittedPickup),
                "발전기 연료 픽업의 exact carried intent가 발행되지 않았습니다: "
                + DescribePowerFuelHaul(generatorDestination),
                realTimeTimeout: 10f);
            HaulDeliveryIntentSaveData carriedBeforeRestore = items
                .CaptureHaulDeliveryIntentsByDestination(generatorDestination)
                .Single(intent => intent.HasCommittedPickup);
            long carriedMassBeforeRestore = bufferOccupancy
                .Capture(generatorDestination)
                .CommittedCarriedMassGrams;
            Require(carriedMassBeforeRestore > 0L,
                "복원 직전 발전기 연료 carried 질량이 0g입니다.");

            List<DungeonSaveSectionEnvelope> checkpoint =
                saveSections.CaptureAll();
            string[] fixtureBuildingIds = createdBuildings.Select(GetNodeId).ToArray();
            DungeonGameRestoreReport restoreReport = new();
            Require(saveSections.RestoreAll(checkpoint, restoreReport),
                "발전기 연료 carried checkpoint 복원 실패: "
                + string.Join(" | ", restoreReport.Errors));
            for (int settleFrame = 0; settleFrame < 4; settleFrame++)
                yield return null;

            Require(scope.Container.Resolve<IGridSystemProvider>().TryGetGrid(out grid),
                "복원된 산업 시설 Grid를 찾지 못했습니다.");
            createdBuildings.Clear();
            foreach (string fixtureId in fixtureBuildingIds)
            {
                Require(TryResolveLiveBuilding(fixtureId, out BuildableObject restoredBuilding),
                    "복원된 산업 fixture가 없습니다: " + fixtureId);
                createdBuildings.Add(restoredBuilding);
            }
            cleanTank = FindBuilding("I08");
            shower = FindBuilding("I14");
            wastewaterTank = FindBuilding("I09");

            fuelHauler = FindHauler();
            Require(fuelHauler != null,
                "발전기 연료 복원 후 운반자를 다시 찾지 못했습니다.");
            HaulDeliveryIntentSaveData carriedAfterRestore = items
                .CaptureHaulDeliveryIntentsByDestination(generatorDestination)
                .Single(intent => intent.HasCommittedPickup);
            long carriedMassAfterRestore = bufferOccupancy
                .Capture(generatorDestination)
                .CommittedCarriedMassGrams;
            Require(string.Equals(
                    carriedBeforeRestore.operationId,
                    carriedAfterRestore.operationId,
                    StringComparison.Ordinal)
                && carriedMassAfterRestore == carriedMassBeforeRestore,
                "발전기 연료 save/restore가 exact carried intent 또는 gram을 바꿨습니다.");
            report.Add($"powerFuelCarriedRestore={carriedMassAfterRestore}g");

            yield return WaitUntil(
                () => TryResolveLiveBuilding(pumpNodeId, out BuildableObject livePump)
                    && power.IsPowered(livePump),
                "exact-stack 요청 연료가 AI 운반·입고·소비되지 않아 양수 펌프에 전력이 공급되지 않았습니다: "
                + DescribePowerFuelHaul(generatorDestination));
        }
        finally
        {
            Destroy(fuelHaulAction);
        }
        Require(items.CaptureHaulDeliveryIntentsByDestination(
                    generatorDestination).Count == 0,
            "연료 입고·소비 이후 발전기 haul intent가 남았습니다.");

        if (PowerFuelOnly)
        {
            Require(TryResolveLiveBuilding(
                    generatorNodeId,
                    out BuildableObject liveGenerator),
                "발전기 연료 복원 후 실제 발전기를 다시 찾지 못했습니다.");
            PowerNetworkSnapshot focusedPowerNetwork = power.Networks.First(
                network => network.Nodes.Any(node =>
                    node.BuildingId.Equals(
                        liveGenerator.RequirePersistentInstanceId())));
            report.Add(
                $"power={focusedPowerNetwork.ProductionPerSecond:0.##}/"
                + $"{focusedPowerNetwork.DemandPerSecond:0.##}");
            report.Add("powerFuelRoute=exact-stack-admission-ai-haul");
            yield break;
        }

        yield return WaitUntil(
            () => water.TryGetNetwork(
                    cleanTank,
                    out FluidNetworkSnapshot snapshot)
                && snapshot.CleanWater >= 0.5f,
            "전동 양수 펌프가 실제 상수 탱크에 물을 공급하지 않았습니다.");

        Require(fixtures.TryBeginUse(
                shower,
                default,
                out WaterFixtureUseTicket ticket,
                out DomainFailure fixtureFailure),
            "샤워 시설의 실제 급수 사용을 시작하지 못했습니다: "
            + fixtureFailure.Code);
        Require(ticket.SupplyKind == WaterFixtureSupplyKind.Piped,
            "배관 연결 샤워가 수동 물통으로 처리됐습니다.");
        fixtures.CompleteUse(shower, ticket);

        yield return WaitUntil(
            () => water.TryGetNetwork(
                    wastewaterTank,
                    out FluidNetworkSnapshot snapshot)
                && snapshot.Channel == UtilityChannel.Wastewater
                && snapshot.Wastewater >= 0.4f,
            "샤워 사용 후 실제 오수가 하수 탱크에 들어오지 않았습니다.");

        Require(TryResolveLiveBuilding(
                generatorNodeId,
                out BuildableObject currentGenerator),
            "발전기 연료 복원 후 실제 발전기를 다시 찾지 못했습니다.");
        PowerNetworkSnapshot poweredNetwork = power.Networks.First(
            network => network.Nodes.Any(node =>
                node.BuildingId.Equals(
                    currentGenerator.RequirePersistentInstanceId())));
        FluidNetworkSnapshot cleanNetwork = water.Networks.First(
            network => network.Channel == UtilityChannel.CleanWater
                && network.CleanWater > 0f);
        FluidNetworkSnapshot wasteNetwork = water.Networks.First(
            network => network.Channel == UtilityChannel.Wastewater
                && network.Wastewater > 0f);
        report.Add(
            $"power={poweredNetwork.ProductionPerSecond:0.##}/"
            + $"{poweredNetwork.DemandPerSecond:0.##}");
        report.Add("powerFuelRoute=exact-stack-admission-ai-haul");
        report.Add($"cleanWater={cleanNetwork.CleanWater:0.###}");
        report.Add($"wastewater={wasteNetwork.Wastewater:0.###}");
    }

    private IEnumerator VerifyConveyorTransport(Vector2Int origin)
    {
        BuildableObject input = FindBuilding("C02");
        BuildableObject output = FindBuilding("C03");
        IPowerInfrastructureCommand powerCommands = scope.Container.Resolve<IPowerInfrastructureCommand>();
        BuildableObject[] deliveryBelt = createdBuildings.Where(building =>
            building.BuildingData.GetAbility<BuildingConveyorSegmentAbility>() != null
            && building.centerPos.y == origin.y
            && building.centerPos.x >= origin.x + 4 && building.centerPos.x <= origin.x + 10).ToArray();
        foreach (BuildableObject belt in deliveryBelt)
            Require(powerCommands.SetPriority(belt, PowerPriority.Critical).Succeeded,
                "Could not prioritize the focused delivery belt.");
        foreach (BuildableObject belt in deliveryBelt)
            report.Add("focused-belt-power=" + GetNodeId(belt) + ";" + belt.centerPos + ";"
                + (power.TryGetNode(belt, out PowerNodeSnapshot state)
                    ? $"network={state.NetworkId};powered={state.Powered};demand={state.DemandPerSecond};supplied={state.SuppliedFraction};fault={state.Fault};heat={state.Heat}"
                    : "missing-node"));
        yield return WaitUntil(() => deliveryBelt.All(power.IsPowered),
            "Focused delivery belt lacks authored power supply.");
        string outputFacilityId =
            output.RequirePersistentInstanceId().Value;
        conveyorDestination = ConveyorDestinationPrefix + outputFacilityId;
        Vector2Int outputDropPosition = output.BuildingData
            .GetGridPosList(output.centerPos)
            .First();
        PhysicalMassGrams blockedCapacity = itemMass.GetQuantityMass(
            (ItemDefinitionId)"material:lumber",
            PhysicalItemMassSubject.ForDefinition(
                (ItemDefinitionId)"material:lumber"),
            2);
        PhysicalMassGrams outputCapacity = itemMass.GetQuantityMass(
            (ItemDefinitionId)"material:lumber",
            PhysicalItemMassSubject.ForDefinition(
                (ItemDefinitionId)"material:lumber"),
            3);
        PublishConveyorOutputAuthority(
            output,
            outputFacilityId,
            outputDropPosition,
            blockedCapacity,
            1L);
        Require(conveyor.GetDestinationChoices(output).Any(choice => choice.DestinationId == conveyorDestination
                && choice.DropPosition == outputDropPosition && choice.MaximumMassGrams == blockedCapacity.Value),
            "출력 목적지 선택이 실제 claim/gram profile에서 나오지 않았습니다.");
        Require(conveyorCommands.SetPortDestination(
                output,
                conveyorDestination).Succeeded,
            "컨베이어 출력 목적지를 설정하지 못했습니다.");
        Require(conveyor.GetDestinationChoices(input).Any(choice => choice.DestinationId == conveyorDestination),
            "연결된 출력 포트가 입력 목적지 목록에 없습니다.");
        Require(conveyorCommands.SetPortDestination(input, conveyorDestination).Succeeded,
            "컨베이어 입력 목적지를 설정하지 못했습니다.");
        Require(conveyorCommands.SetPortDestination(input, string.Empty).Succeeded, "Clear input for UI test.");
        Require(conveyorCommands.SetPortDestination(output, string.Empty).Succeeded, "Clear output for UI test.");
        UITabManager tabs = UnityEngine.Object.FindFirstObjectByType<UITabManager>();
        Require(tabs != null, "Main tab manager missing.");
        tabs.ToggleSelectButton(10);
        yield return null;
        ClickConveyorUi("ConveyorDestination_" + outputFacilityId);
        yield return null;
        ClickConveyorUi("ConveyorDestinationChoice_" + outputFacilityId + "_" + conveyorDestination);
        yield return null;
        ClickConveyorUi("ConveyorDestination_" + GetNodeId(input));
        yield return null;
        ClickConveyorUi("ConveyorDestinationChoice_" + GetNodeId(input) + "_" + conveyorDestination);
        DungeonConveyorInfrastructureSaveData configured = conveyorPersistence.Capture();
        Require(configured.nodes.Single(value => value.buildingInstanceId == GetNodeId(input)).destinationId
            == conveyorDestination, "Input UI did not publish selected destination.");
        Require(configured.nodes.Single(value => value.buildingInstanceId == outputFacilityId).destinationId
            == conveyorDestination, "Output UI did not publish selected destination.");
        string configuredJson = JsonUtility.ToJson(configured);
        Require(conveyorCommands.SetPortDestination(input, string.Empty).Succeeded, "Clear for restore test.");
        conveyorPersistence.Restore(conveyorPersistence.PrepareRestore(
            JsonUtility.FromJson<DungeonConveyorInfrastructureSaveData>(configuredJson)));
        Require(JsonUtility.ToJson(conveyorPersistence.Capture()) == configuredJson,
            "Configured conveyor domain serialization round trip changed state.");
        report.Add("destination-main-EventSystem-input-output-and-serialized-restore=PASS");
        string beforeRejectedCommand = JsonUtility.ToJson(conveyorPersistence.Capture());
        Require(!conveyorCommands.SetPortDestination(input, " " + conveyorDestination).Succeeded
            && !conveyorCommands.SetPortDestination(input, "unknown:wim-destination").Succeeded
            && beforeRejectedCommand == JsonUtility.ToJson(conveyorPersistence.Capture()),
            "잘못된 목적지 명령이 상태를 변경했습니다.");
        report.Add("destination-choices-exact-profile-and-invalid-command-atomic=PASS");
        HashSet<string> preexistingInputStackIds = items
            .GetStacksAt(input.centerPos, includeStored: true)
            .Select(stack => stack.StackId)
            .ToHashSet(StringComparer.Ordinal);
        Require(items.SpawnItemAt(
                "material:lumber",
                3,
                input.centerPos,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawned)
            && spawned == 3,
            "컨베이어 입력 칸에 실제 물리 스택을 생성하지 못했습니다.");
        WorldItemStackSnapshot source = items.GetStacksAt(input.centerPos)
            .SingleOrDefault(stack =>
                stack.ItemId == "material:lumber"
                && !preexistingInputStackIds.Contains(stack.StackId));
        Require(source != null,
            "컨베이어 입력 스택 ID를 확인하지 못했습니다.");

        string originalStackId = source.StackId;
        Require(conveyorTransactions.TryLoadStack(
                new ItemStackId(originalStackId),
                input,
                conveyorDestination,
                out string payloadId,
                out DomainFailure loadFailure),
            "실제 물리 스택을 컨베이어에 적재하지 못했습니다: "
            + loadFailure.Code);
        Require(!string.IsNullOrWhiteSpace(payloadId),
            "컨베이어 화물 ID가 생성되지 않았습니다.");
        WorldItemStackSnapshot transitBeforeFailure = items.GetAllStacks()
            .Single(stack => stack.StackId == originalStackId);

        yield return WaitUntil(
            () => conveyor.Networks
                .SelectMany(network => network.Payloads)
                .Any(payload => string.Equals(
                        payload.PayloadId,
                        payloadId,
                        StringComparison.Ordinal)
                    && payload.StallReason
                        == ConveyorStallReason.DestinationFull),
            "출력 버퍼 질량 부족이 실제 컨베이어 화물을 정지시키지 않았습니다.");
        Require(itemTransfers.TryGetTransitStack(
                new ItemStackId(originalStackId),
                payloadId,
                out ItemTransitStackSnapshot retained)
            && retained.Quantity == 3,
            "출력 버퍼 입고 실패가 InTransit 화물을 보존하지 못했습니다.");
        WorldItemStackSnapshot transitAfterFailure = items.GetAllStacks()
            .Single(stack => stack.StackId == originalStackId);
        report.Add($"transit-during-travel=content:{transitBeforeFailure.ContentRevision}->{transitAfterFailure.ContentRevision};reservation:{transitBeforeFailure.ReservationRevision}->{transitAfterFailure.ReservationRevision};position:{transitBeforeFailure.Position}->{transitAfterFailure.Position};signature:{transitBeforeFailure.StackSignature}->{transitAfterFailure.StackSignature}");
        // Compare one rejected admission in the same game-time instant; travel
        // and normal item maintenance between ticks are not that transaction.
        Require(!itemTransfers.TryCompleteTransitToFacilityBuffer(new ItemStackId(originalStackId),
                payloadId, outputDropPosition, conveyorDestination, out _, out _),
            "Undersized output capacity unexpectedly admitted the payload.");
        RequireSameTransitCustody(
            transitAfterFailure,
            items.GetAllStacks().Single(stack => stack.StackId == originalStackId),
            payloadId);
        Require(conveyor.Networks
                .SelectMany(network => network.Payloads)
                .Count(payload => string.Equals(
                    payload.PayloadId,
                    payloadId,
                    StringComparison.Ordinal)) == 1,
            "출력 버퍼 입고 실패 후 컨베이어 payload가 유실·복제됐습니다.");
        Require(bufferOccupancy.Capture(conveyorDestination)
                .TotalMassGrams == 0L,
            "실패한 컨베이어 입고가 출력 버퍼 점유 질량을 남겼습니다.");
        Require(bufferCapacity.TryGetCapacity(
                conveyorDestination,
                outputDropPosition,
                out FacilityBufferMassCapacitySnapshot blockedSnapshot)
            && blockedSnapshot.ReservedMassGrams == 0L,
            "실패한 컨베이어 입고가 예약 질량을 남겼습니다.");
        report.Add(
            $"blockedPayload={payloadId};"
            + $"capacityGrams={blockedCapacity.Value};"
            + "state=InTransit;retry=retained");

        Require(conveyorCommands.SetPortDestination(output, string.Empty).Succeeded,
            "출력 목적지 해제가 실패했습니다.");
        yield return WaitUntil(() => conveyor.Networks.SelectMany(network => network.Payloads)
                .Any(payload => payload.PayloadId == payloadId && payload.StallReason == ConveyorStallReason.NoRoute),
            "설정 해제 후 목적지 없는 화물이 보존 정지하지 않았습니다.");
        Require(itemTransfers.TryGetTransitStack(new ItemStackId(originalStackId), payloadId, out var retainedAfterChange)
            && retainedAfterChange.Quantity == 3,
            "목적지 변경이 화물을 바닥 배출하거나 삭제했습니다.");
        Require(conveyorCommands.SetPortDestination(output, conveyorDestination).Succeeded,
            "출력 목적지 복구 실패.");
        report.Add("in-transit-destination-clear-no-ground-drop-and-resume=PASS");

        Require(bufferLifecycle.TryReplaceOwnedAuthorities(ConveyorBufferOwnerDomain,
                originalConveyorBufferClaims, originalConveyorBufferProfiles, out string removedReason),
            "Could not remove fixture output owner: " + removedReason);
        Require(!conveyor.GetDestinationChoices(input).Any(choice => choice.DestinationId == conveyorDestination),
            "Removed destination remained selectable.");
        yield return WaitUntil(() => conveyor.Networks.SelectMany(network => network.Payloads)
                .Any(payload => payload.PayloadId == payloadId && payload.StallReason == ConveyorStallReason.NoRoute),
            "Removed destination did not retain its payload as NoRoute.");
        Require(itemTransfers.TryGetTransitStack(new ItemStackId(originalStackId), payloadId, out var ownerRemoved)
                && ownerRemoved.Quantity == 3, "Removed destination lost its physical payload.");
        report.Add("destination-owner-removal-retained-no-route=PASS");

        PublishConveyorOutputAuthority(
            output,
            outputFacilityId,
            outputDropPosition,
            outputCapacity,
            2L);
        yield return WaitUntil(
            () => items.GetStacksAt(
                    outputDropPosition,
                    includeStored: true)
                .Any(stack =>
                    stack.StackId == originalStackId
                    && stack.State == WorldItemStackState.FacilityBuffer
                    && stack.DestinationId == conveyorDestination),
            "화물이 실제 벨트를 이동해 출력 버퍼에 도착하지 않았습니다.");
        Require(!conveyor.Networks
                .SelectMany(network => network.Payloads)
                .Any(payload => string.Equals(
                    payload.PayloadId,
                    payloadId,
                    StringComparison.Ordinal)),
            "정상 배송이 끝난 뒤 컨베이어 화물이 남았습니다.");
        WorldItemStackSnapshot delivered = items.GetStacksAt(
                outputDropPosition,
                includeStored: true)
            .Single(stack => stack.StackId == originalStackId);
        Require(delivered.State == WorldItemStackState.FacilityBuffer
                && delivered.Quantity == 3
                && delivered.DestinationId == conveyorDestination
                && delivered.Position == outputDropPosition
                && delivered.StackSignature
                    == transitBeforeFailure.StackSignature
                && delivered.ItemInstanceId
                    == transitBeforeFailure.ItemInstanceId,
            "재시도 성공 후 exact 물리 lot이 달라졌습니다.");
        Require(bufferOccupancy.Capture(conveyorDestination)
                .TotalMassGrams == outputCapacity.Value,
            "재시도 성공 후 출력 버퍼 점유 질량이 exact payload gram과 다릅니다.");
        report.Add(
            $"normalPayload={payloadId};"
            + $"distance={Mathf.Abs(output.centerPos.x - origin.x)};"
            + $"capacityGrams={outputCapacity.Value};"
            + "admission=exact-owner-profile");
    }

    private IEnumerator VerifyAuthoredFuelDestination(Vector2Int origin)
    {
        BuildableObject generator = FindBuilding("I03");
        string destination = "power:" + GetNodeId(generator);
        BuildableObject input = PlaceBuilding(FindBuilding("C02").BuildingData, origin + Vector2Int.left);
        BuildableObject output = PlaceBuilding(FindBuilding("C03").BuildingData, generator.centerPos);
        conveyorCommands.MarkTopologyDirty();
        Require(conveyor.GetDestinationChoices(output).Any(value => value.DestinationId == destination
            && value.OwnerFacilityId == GetNodeId(generator)), "Authored generator destination is not selectable.");
        foreach (P0FeatureSurfacePanel panel in UnityEngine.Object.FindObjectsByType<P0FeatureSurfacePanel>(FindObjectsSortMode.None))
            if (panel.gameObject.activeInHierarchy) panel.Refresh();
        yield return null;
        ClickConveyorUi("ConveyorDestination_" + GetNodeId(output));
        yield return null;
        ClickConveyorUi("ConveyorDestinationChoice_" + GetNodeId(output) + "_" + destination);
        yield return null;
        ClickConveyorUi("ConveyorDestination_" + GetNodeId(input));
        yield return null;
        ClickConveyorUi("ConveyorDestinationChoice_" + GetNodeId(input) + "_" + destination);
        Require(items.SpawnItemAt("resource:mana-crystal", 1, input.centerPos,
            WorldItemStackState.Loose, string.Empty, out int count) && count == 1, "Fuel source creation failed.");
        WorldItemStackSnapshot source = items.GetStacksAt(input.centerPos).Single(value => value.ItemId == "resource:mana-crystal");
        Require(conveyorTransactions.TryLoadStack(new ItemStackId(source.StackId), input, destination,
            out _, out DomainFailure failure), "Authored fuel conveyor load failed: " + failure.Code);
        yield return WaitUntil(() => items.GetStacksAt(generator.centerPos, includeStored: true)
            .Any(value => value.StackId == source.StackId && value.State == WorldItemStackState.FacilityBuffer
                && value.DestinationId == destination && value.Quantity == 1),
            "Real generator buffer did not receive the same physical fuel lot.");
        report.Add("authored-generator-owner-main-UI-conveyor-physical-fuel-delivery=PASS");
    }

    private void PublishConveyorOutputAuthority(
        BuildableObject output,
        string outputFacilityId,
        Vector2Int outputDropPosition,
        PhysicalMassGrams capacity,
        long capacityRevision)
    {
        Require(bufferLifecycle.TryReplaceOwnedAuthorities(
                ConveyorBufferOwnerDomain,
                originalConveyorBufferClaims.Concat(new[]
                {
                    new FacilityBufferDestinationClaim(
                        conveyorDestination,
                        outputDropPosition,
                        ConveyorBufferOwnerDomain,
                        ConveyorBufferOperationId + ":" + outputFacilityId,
                        outputFacilityId,
                        FacilityBufferDestinationAnchorKind.LiveBuilding)
                }).ToArray(),
                originalConveyorBufferProfiles.Concat(new[]
                {
                    new FacilityBufferCapacityProfile(
                        conveyorDestination,
                        outputDropPosition,
                        ConveyorBufferOwnerDomain,
                        ConveyorBufferOperationId + ":" + outputFacilityId,
                        outputFacilityId,
                        capacity,
                        capacityRevision)
                }).ToArray(),
                out string authorityFailure),
            "컨베이어 출력 버퍼의 exact owner/capacity 권위를 게시하지 못했습니다: "
            + authorityFailure);
    }

    private static void RequireSameTransitCustody(
        WorldItemStackSnapshot before,
        WorldItemStackSnapshot after,
        string payloadId)
    {
        Require(before != null && after != null
                && before.StackId == after.StackId
                && before.ItemId == after.ItemId
                && before.ItemInstanceId == after.ItemInstanceId
                && before.Quantity == after.Quantity
                && before.ContentRevision == after.ContentRevision
                && before.ReservationRevision == after.ReservationRevision
                && before.StackSignature == after.StackSignature
                && before.Position == after.Position
                && before.SourceStorageDestinationId
                    == after.SourceStorageDestinationId
                && before.HasDestinationPosition
                    == after.HasDestinationPosition
                && before.DestinationPosition == after.DestinationPosition
                && after.State == WorldItemStackState.InTransit
                && after.DestinationId == payloadId,
            "실패한 컨베이어 입고가 exact InTransit custody를 변경했습니다.");
    }

    private IEnumerator VerifyAutomation()
    {
        BuildableObject facility = createdBuildings.First(building =>
            building.BuildingData.GetAbility<
                BuildingAutomationAbility>() != null);
        InfrastructureCommandResult command = automationCommands.SetMode(
            facility,
            AutomationMode.Automatic);
        Require(command.Succeeded,
            "실제 생산 시설을 자동 모드로 전환하지 못했습니다: "
            + command.Failure.Code);
        yield return WaitUntil(
            () => automation.TryGetFacility(
                    facility,
                    out AutomationFacilitySnapshot snapshot)
                && snapshot.Mode == AutomationMode.Automatic
                && snapshot.Powered
                && snapshot.Operational,
            "자동화 시설이 전력을 받아 실제 무인 모드로 가동되지 않았습니다.");

        automation.TryGetFacility(
            facility,
            out AutomationFacilitySnapshot result);
        report.Add(
            $"automation={result.Mode};"
            + $"workRate={result.WorkRate:0.###};"
            + $"status={result.Status.Code}");
    }

    private IEnumerator VerifyDeadlockAndOverflow()
    {
        BuildableObject loopRight = FindBuildingAtCodeAndPosition(
            "C01R",
            building => building.centerPos.y
                > createdBuildings.Min(candidate => candidate.centerPos.y));
        BuildableObject splitter = FindBuilding("C04");
        BuildableObject overflow = FindBuilding("C09");
        string splitterNodeId = GetNodeId(splitter);
        string loopNodeId = GetNodeId(loopRight);
        ConveyorNetworkSnapshot targetNetwork =
            conveyor.Networks.FirstOrDefault(network =>
                network.IsCyclic
                && network.Nodes.Any(node =>
                    string.Equals(
                        node.BuildingId.Value,
                        loopNodeId,
                        StringComparison.Ordinal)));
        Require(targetNetwork != null,
            "배치한 순환 벨트의 실제 네트워크를 찾지 못했습니다.");
        HashSet<string> createdNodeIds = createdBuildings
            .Select(GetNodeId)
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .ToHashSet(StringComparer.Ordinal);
        string[] foreignNodeIds = targetNetwork.Nodes
            .Select(node => node.BuildingId.Value)
            .Where(nodeId => !createdNodeIds.Contains(nodeId))
            .OrderBy(nodeId => nodeId, StringComparer.Ordinal)
            .ToArray();
        report.Add("deadlockExistingNetworkNodes="
            + foreignNodeIds.Length);
        List<string> nodeIds = targetNetwork.Nodes
            .SelectMany(node => Enumerable.Repeat(
                node.BuildingId.Value,
                Mathf.Max(1, node.Capacity)))
            .ToList();
        int splitterIndex = nodeIds.FindIndex(nodeId =>
            string.Equals(
                nodeId,
                splitterNodeId,
                StringComparison.Ordinal));
        Require(splitterIndex >= 0,
            "오버플로에 연결된 분배기가 순환 네트워크에서 누락됐습니다.");
        nodeIds.RemoveAt(splitterIndex);
        nodeIds.Insert(0, splitterNodeId);
        DungeonConveyorInfrastructureSaveData snapshot =
            new DungeonConveyorInfrastructureSaveData();
        List<string> transitStackIds = new List<string>(nodeIds.Count);
        for (int index = 0; index < nodeIds.Count; index++)
        {
            BuildableObject nodeBuilding = ResolveLiveBuilding(
                nodeIds[index]);
            Require(items.SpawnItemAt(
                    "material:lumber",
                    1,
                    nodeBuilding.centerPos,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int spawned)
                && spawned == 1,
                "교착 검증용 물리 스택을 생성하지 못했습니다.");
            WorldItemStackSnapshot physicalStack = items
                .GetStacksAt(nodeBuilding.centerPos)
                .Last(stack => stack.ItemId == "material:lumber"
                    && stack.State == WorldItemStackState.Loose);
            string payloadId = OverflowPayloadPrefix + index;
            ItemStackId stackId = new ItemStackId(physicalStack.StackId);
            Require(itemTransfers.TryBeginTransit(
                    stackId,
                    nodeBuilding.centerPos,
                    payloadId,
                    out _,
                    out DomainFailure transitFailure),
                "교착 검증용 스택의 transit 전환 실패: "
                + transitFailure.Code);
            transitStackIds.Add(stackId.Value);
            snapshot.payloads.Add(new ConveyorPayloadSaveData
            {
                payloadId = payloadId,
                itemStackId = stackId.Value,
                segmentBuildingInstanceId = nodeIds[index],
                destinationId = "qa:unreachable",
                lastMovedAt = 0f,
                stalledSince = 0f,
                routeVersion = 0
            });
        }

        conveyorPersistence.Restore(
            conveyorPersistence.PrepareRestore(snapshot));
        Require(conveyorCommands.SetOverflowPolicy(
                overflow,
                ConveyorOverflowPolicy.ManualApproval,
                string.Empty).Succeeded,
            "교착 검증용 수동 오버플로 정책을 설정하지 못했습니다.");

        float startedAt = Time.realtimeSinceStartup;
        bool IsExpectedDeadlock() => conveyor.Networks.Any(network =>
            network.State == ConveyorNetworkState.Deadlocked
            && network.PayloadCount == nodeIds.Count);
        while (!IsExpectedDeadlock()
               && Time.realtimeSinceStartup - startedAt < 10f)
        {
            if (gameManager != null)
            {
                gameManager.isPause = false;
            }

            Time.timeScale = 5f;
            yield return null;
        }

        report.Add(
            "deadlockObservation="
            + string.Join(
                "|",
                conveyor.Networks
                    .Where(network => network.PayloadCount > 0)
                    .Select(network =>
                        $"{network.State},payloads={network.PayloadCount},"
                        + $"capacity={network.Capacity},"
                        + $"cyclic={network.IsCyclic},"
                        + $"stall={network.LongestStallSeconds:0.##},"
                        + $"reason={network.PrimaryReason}")));
        ConveyorNetworkSnapshot observedNetwork = conveyor.Networks
            .FirstOrDefault(network =>
                network.PayloadCount == nodeIds.Count);
        if (observedNetwork != null)
        {
            report.Add(
                "deadlockNodes="
                + string.Join(
                    "|",
                    observedNetwork.Nodes.Select(node =>
                        $"{node.BuildingId.Value}:{node.Capacity}")));
            report.Add(
                "deadlockPayloadReasons="
                + string.Join(
                    "|",
                    observedNetwork.Payloads
                        .GroupBy(payload => payload.StallReason)
                        .OrderBy(group => group.Key)
                        .Select(group =>
                            $"{group.Key}:{group.Count()}")));
        }

        Require(
            IsExpectedDeadlock(),
            "30게임초 동안 막힌 실제 순환 벨트가 Deadlocked로 전환되지 않았습니다.");
        Require(conveyorCommands.ApproveOverflow(
                OverflowPayloadPrefix + "0").Succeeded,
            "가장 오래 정지한 화물의 오버플로 배출을 승인하지 못했습니다.");
        yield return WaitUntil(
            () => items.GetAllStacks().Any(stack =>
                stack.StackId == transitStackIds[0]
                && stack.State == WorldItemStackState.Loose
                && stack.ItemId == "material:lumber"
                && stack.Quantity == 1),
            "승인한 교착 화물이 loose stack으로 배출되지 않았습니다.");

        WorldItemStackSnapshot restored = items.GetAllStacks().First(
            stack => stack.StackId == transitStackIds[0]);
        Require(restored.State == WorldItemStackState.Loose
                && restored.ItemId == "material:lumber"
                && restored.Quantity == 1,
            "오버플로 배출 중 물리 스택 권위가 손실됐습니다.");
        Require(conveyor.Networks.All(network =>
                network.State != ConveyorNetworkState.Deadlocked),
            "오버플로 배출 후 순환 교착이 해소되지 않았습니다.");
        report.Add(
            $"deadlockPayloads={nodeIds.Count};"
            + $"overflowRestored={restored.StackId}");
    }

    private IEnumerator CaptureVisual()
    {
        string directory = Path.GetDirectoryName(
            IndustrialInfrastructurePlayModeVerifier.ScreenshotPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(
            IndustrialInfrastructurePlayModeVerifier.ScreenshotPath);
        yield return new WaitForSecondsRealtime(0.5f);
        report.Add(
            "screenshot="
            + IndustrialInfrastructurePlayModeVerifier.ScreenshotPath);
    }

    private IEnumerator WaitUntil(
        Func<bool> predicate,
        string failureMessage,
        float realTimeTimeout = TimeoutSeconds)
    {
        float startedAt = Time.realtimeSinceStartup;
        while (!predicate()
               && Time.realtimeSinceStartup - startedAt < realTimeTimeout)
        {
            if (gameManager != null)
            {
                gameManager.isPause = false;
            }

            Time.timeScale = 5f;
            yield return null;
        }

        Require(predicate(), failureMessage);
    }

    private BuildableObject FindBuilding(string code)
    {
        return FindBuildingAtCodeAndPosition(code, _ => true);
    }

    private BuildableObject FindBuildingAtCodeAndPosition(
        string code,
        Func<BuildableObject, bool> predicate)
    {
        BuildableObject result = createdBuildings.FirstOrDefault(
            building => string.Equals(
                    building.BuildingData.GetAbility<
                        BuildingFacilityPartAbility>()?.code,
                    code,
                    StringComparison.Ordinal)
                && predicate(building));
        Require(result != null, $"배치한 산업 시설 {code}를 찾지 못했습니다.");
        return result;
    }

    private static string GetNodeId(BuildableObject building)
    {
        return building?.RequirePersistentInstanceId().Value
            ?? string.Empty;
    }

    private static BuildableObject ResolveLiveBuilding(string nodeId)
    {
        BuildableObject[] matches = UnityEngine.Object
            .FindObjectsByType<BuildableObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(building => string.Equals(
                GetNodeId(building),
                nodeId,
                StringComparison.Ordinal))
            .ToArray();
        Require(matches.Length == 1,
            "교착 검증 네트워크 노드의 실제 시설을 정확히 찾지 못했습니다: "
            + nodeId
            + ";matches="
            + matches.Length);
        return matches[0];
    }

    private static bool TryResolveLiveBuilding(
        string nodeId,
        out BuildableObject building)
    {
        BuildableObject[] matches = UnityEngine.Object
            .FindObjectsByType<BuildableObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(candidate => string.Equals(
                GetNodeId(candidate),
                nodeId,
                StringComparison.Ordinal))
            .ToArray();
        building = matches.Length == 1 ? matches[0] : null;
        return building != null;
    }

    private static CharacterActor FindHauler()
    {
        return CharacterActorCollection.DistinctByGameObject(
                UnityEngine.Object.FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None))
            .Where(actor => actor != null && !actor.IsDead)
            .Where(actor => actor.TryGetAbility(out AbilityWork work)
                && work.WorkPriorities != null
                && work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul) != WorkPriorityLevel.Off
                && work.PriorityWorkTarget == null)
            .OrderByDescending(actor => actor.TryGetAbility(out AbilityWork _))
            .ThenBy(actor => actor.Identity != null
                && actor.Identity.Role == CharacterRole.Owner ? 1 : 0)
            .ThenBy(actor => actor.BuildingCharacterId.Value, StringComparer.Ordinal)
            .FirstOrDefault(actor => actor.TryGetAbility(out AbilityMove _)
                && (actor.TryGetAbility(out AbilityWork _)
                    || actor.Identity != null
                    && actor.Identity.Role == CharacterRole.Owner));
    }

    private string DescribePowerFuelHaul(string destinationId)
    {
        AbilityHaul ability = fuelHauler?.GetComponent<AbilityHaul>();
        string stacks = string.Join(
            "|",
            items.GetAllStacks()
                .Where(value => value != null
                    && (string.Equals(
                            value.DestinationId,
                            destinationId,
                            StringComparison.Ordinal)
                        || string.Equals(
                            value.ItemId,
                            "resource:mana-crystal",
                            StringComparison.Ordinal)))
                .OrderBy(value => value.StackId, StringComparer.Ordinal)
                .Select(value =>
                    $"{value.StackId}:{value.State}:{value.Quantity}:"
                    + $"{value.DestinationId}:{value.Position}"));
        return $"actor={fuelHauler?.BuildingCharacterId.Value ?? "missing"};"
            + $"hauling={ability?.IsHauling == true};"
            + $"intents={items.CaptureHaulDeliveryIntentsByDestination(destinationId).Count};"
            + $"stacks={stacks}";
    }

    private void FocusCamera(Vector2Int origin)
    {
        if (mainCamera == null)
        {
            return;
        }

        Vector3 world = grid.GetWorldPos(origin + new Vector2Int(6, 1));
        mainCamera.transform.position = new Vector3(
            world.x,
            world.y + 3f,
            originalCameraPosition.z);
        mainCamera.orthographicSize = Mathf.Max(7f, originalCameraSize);
    }

    private static void ClickConveyorUi(string actionName)
    {
        Canvas.ForceUpdateCanvases();
        Button button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .SingleOrDefault(value => value.gameObject.activeInHierarchy && value.name == actionName);
        Require(button != null && button.IsInteractable() && EventSystem.current != null,
            "Main industrial UI action missing: " + actionName);
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current)
            { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    }

    private void WriteReport()
    {
        string directory = Path.GetDirectoryName(
            IndustrialInfrastructurePlayModeVerifier.ReportPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        report.Add($"unityFrame={Time.frameCount}");
        report.Add($"gameTime={clock?.Time ?? 0f:0.###}");
        File.WriteAllLines(
            IndustrialInfrastructurePlayModeVerifier.ReportPath,
            report);
        if (CommandOutcomesOnly)
        {
            string commandOutcomeDirectory = Path.GetDirectoryName(
                IndustrialInfrastructurePlayModeVerifier
                    .CommandOutcomeEvidencePath);
            if (!string.IsNullOrWhiteSpace(commandOutcomeDirectory))
            {
                Directory.CreateDirectory(commandOutcomeDirectory);
            }
            File.WriteAllLines(
                IndustrialInfrastructurePlayModeVerifier
                    .CommandOutcomeEvidencePath,
                report.Where(line => !line.StartsWith(
                        "fastPartyCommit=",
                        StringComparison.Ordinal)
                    && !line.StartsWith(
                        "unityFrame=",
                        StringComparison.Ordinal)
                    && !line.StartsWith(
                        "gameTime=",
                        StringComparison.Ordinal)),
                new UTF8Encoding(false));
        }
        if (ConveyorOnly)
        {
            Directory.CreateDirectory("Artifacts/QA/wim-implementation");
            File.WriteAllLines("Artifacts/QA/wim-implementation/wim-007-conveyor-live.txt",
                report.Where(line => !line.StartsWith("fastPartyCommit=", StringComparison.Ordinal)
                    && !line.StartsWith("unityFrame=", StringComparison.Ordinal)
                    && !line.StartsWith("gameTime=", StringComparison.Ordinal)), new UTF8Encoding(false));
        }
        if (PowerFuelOnly
            && verificationFailure == null
            && cleanupFailures.Count == 0)
            WritePowerFuelEvidence();
        if (ConveyorFiltersOnly)
        {
            Directory.CreateDirectory("Artifacts/QA/wim-implementation");
            File.WriteAllLines("Artifacts/QA/wim-implementation/wim-008-conveyor-filter-reserve.txt",
                report.Where(line => !line.StartsWith("fastPartyCommit=", StringComparison.Ordinal)
                    && !line.StartsWith("unityFrame=", StringComparison.Ordinal)
                    && !line.StartsWith("gameTime=", StringComparison.Ordinal)), new UTF8Encoding(false));
        }
        if (PowerConnectionsOnly)
        {
            Directory.CreateDirectory("Artifacts/QA/wim-implementation");
            File.WriteAllLines("Artifacts/QA/wim-implementation/wim-013-main-power.txt",
                report.Where(line => !line.StartsWith("fastPartyCommit=", StringComparison.Ordinal)
                    && !line.StartsWith("unityFrame=", StringComparison.Ordinal)
                    && !line.StartsWith("gameTime=", StringComparison.Ordinal)), new UTF8Encoding(false));
        }
        if (LightingOnly)
        {
            Directory.CreateDirectory("Artifacts/QA/wim-implementation");
            File.WriteAllLines("Artifacts/QA/wim-implementation/wim-011-main-lighting.txt",
                report.Where(line => !line.StartsWith("fastPartyCommit=", StringComparison.Ordinal)
                    && !line.StartsWith("unityFrame=", StringComparison.Ordinal)
                    && !line.StartsWith("gameTime=", StringComparison.Ordinal)), new UTF8Encoding(false));
        }
        if (DoorsOnly)
        {
            Directory.CreateDirectory("Artifacts/QA/wim-implementation");
            File.WriteAllLines(ReturnRestoreOnly ? "Artifacts/QA/wim-implementation/wim-045-return-restore.txt"
                : "Artifacts/QA/wim-implementation/wim-045-012-main-doors.txt", report
                .Where(line => !line.StartsWith("fastPartyCommit=", StringComparison.Ordinal)
                    && !line.StartsWith("unityFrame=", StringComparison.Ordinal)
                    && !line.StartsWith("gameTime=", StringComparison.Ordinal)), new UTF8Encoding(false));
        }
        if (DoorConsumersOnly)
        {
            Directory.CreateDirectory("Artifacts/QA/wim-implementation");
            File.WriteAllLines("Artifacts/QA/wim-implementation/wim-045-door-consumers.txt", report
                .Where(line => !line.StartsWith("fastPartyCommit=", StringComparison.Ordinal)
                    && !line.StartsWith("unityFrame=", StringComparison.Ordinal)
                    && !line.StartsWith("gameTime=", StringComparison.Ordinal)), new UTF8Encoding(false));
        }
        if (DoorConsumersRemainingOnly)
        {
            Directory.CreateDirectory("Artifacts/QA/wim-implementation");
            File.WriteAllLines(
                "Artifacts/QA/wim-implementation/wim-045-door-consumers-remaining.txt",
                report.Where(line => !line.StartsWith("fastPartyCommit=", StringComparison.Ordinal)
                    && !line.StartsWith("unityFrame=", StringComparison.Ordinal)
                    && !line.StartsWith("gameTime=", StringComparison.Ordinal)),
                new UTF8Encoding(false));
        }
        if (CommittedInvasionWarningOnly)
        {
            Directory.CreateDirectory("Artifacts/QA/wim-implementation");
            File.WriteAllLines(
                IndustrialInfrastructurePlayModeVerifier
                    .CommittedInvasionWarningEvidencePath,
                report.Where(line => !line.StartsWith(
                        "fastPartyCommit=",
                        StringComparison.Ordinal)
                    && !line.StartsWith("unityFrame=", StringComparison.Ordinal)
                    && !line.StartsWith("gameTime=", StringComparison.Ordinal)),
                new UTF8Encoding(false));
        }
        if (TemporarySupplyOnly)
        {
            Directory.CreateDirectory("Artifacts/QA/wim-implementation");
            File.WriteAllLines(
                IndustrialInfrastructurePlayModeVerifier
                    .Wim009TemporarySupplyEvidencePath,
                report.Where(line => !line.StartsWith(
                        "fastPartyCommit=",
                        StringComparison.Ordinal)
                    && !line.StartsWith("unityFrame=", StringComparison.Ordinal)
                    && !line.StartsWith("gameTime=", StringComparison.Ordinal)),
                new UTF8Encoding(false));
        }
    }

    private void WritePowerFuelEvidence()
    {
        string[] stableEvidence =
        {
            "schemaVersion=1",
            "scenario=industrial-power-fuel-buffer",
            "route=loose-source->exact-stack-managed-admission->actual-ai-haul"
                + "->carried-save-restore->facility-buffer->fuel-consumption->power",
            RequireSingleReportLine("powerRawRoute="),
            RequireSingleReportLine("powerFuelCarriedRestore="),
            RequireSingleReportLine("power="),
            RequireSingleReportLine("powerFuelRoute="),
            RequireSingleReportLine("mode="),
            RequireSingleReportLine("result=")
        };
        V27BalanceArtifactWriter.WriteIfDifferent(
            IndustrialInfrastructurePlayModeVerifier.PowerFuelEvidencePath,
            stream =>
            {
                using StreamWriter writer = new(
                    stream,
                    new UTF8Encoding(false, true),
                    4096,
                    leaveOpen: true);
                writer.NewLine = "\n";
                foreach (string line in stableEvidence)
                    writer.WriteLine(line);
                writer.Flush();
            });
    }

    private string RequireSingleReportLine(string prefix)
    {
        string[] matches = report
            .Where(line => line.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one '{prefix}' evidence line; actual={matches.Length}.");
        }
        return matches[0];
    }

    private void Cleanup()
    {
        string cleanupResult = originalStateCaptured
            ? null
            : "not-required";
        try
        {
            if (fuelHauler == null
                && !string.IsNullOrEmpty(originalFuelHaulerPersistentId))
            {
                fuelHauler = UnityEngine.Object.FindObjectsByType<CharacterActor>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .SingleOrDefault(actor => string.Equals(
                        actor.Identity?.PersistentId,
                        originalFuelHaulerPersistentId,
                        StringComparison.Ordinal));
            }
            fuelHauler?.GetComponent<AbilityHaul>()?.StopHauling(
                "qa-industrial-power-fuel-cleanup");
            if (fuelHauler != null && originalStateCaptured)
            {
                fuelHauler.SetAiPaused(originalFuelHaulerAiPause);
                fuelHauler.transform.position = originalFuelHaulerWorldPosition;
                fuelHauler.Brain?.ClearPathSearchCache();
            }
            if (originalStateCaptured)
            {
                Time.timeScale = originalTimeScale;
                if (gameManager != null)
                    gameManager.isPause = originalPause;
            }

            if (originalStateCaptured)
            {
                SetOwnerSelectionVisible(ownerSelectionWasActive);
                if (mainCamera != null)
                {
                    mainCamera.transform.position = originalCameraPosition;
                    mainCamera.orthographicSize = originalCameraSize;
                }
            }

            // The whole-registry transaction below owns building, actor, stack,
            // lease and admission restoration together. Replacing only items
            // first invalidates other actors' live carried commitments.
        }
        catch (Exception exception)
        {
            RecordCleanupFailure(
                "산업 PlayMode 수동 정리 실패.",
                exception);
        }

        bool worldRestored = false;
        try
        {
            if (originalStateCaptured)
            {
                if (saveSections == null)
                    throw new InvalidOperationException(
                        "whole-registry save authority is missing during cleanup.");
                if (originalWorld == null)
                    throw new InvalidOperationException(
                        "whole-registry baseline snapshot is missing during cleanup.");
                DungeonGameRestoreReport restoreReport = new();
                worldRestored = saveSections.RestoreAll(
                    originalWorld,
                    restoreReport)
                    && restoreReport.Success;
                if (!worldRestored)
                {
                    throw new InvalidOperationException(
                        "whole-registry baseline restore failed: "
                        + string.Join(" | ", restoreReport.Errors));
                }
            }
        }
        catch (Exception exception)
        {
            RecordCleanupFailure(
                "산업 PlayMode 전체 baseline 복원 실패.",
                exception);
        }

        try
        {
            if (originalStateCaptured)
            {
                if (bufferLifecycle == null)
                    throw new InvalidOperationException(
                        "FacilityBuffer lifecycle authority is missing during cleanup.");
                if (!bufferLifecycle.TryReplaceOwnedAuthorities(
                        ConveyorBufferOwnerDomain,
                        originalConveyorBufferClaims,
                        originalConveyorBufferProfiles,
                        out string authorityRestoreFailure))
                {
                    throw new InvalidOperationException(authorityRestoreFailure);
                }
                if (!worldRestored)
                    throw new InvalidOperationException(
                        "whole-registry baseline was not restored; byte validation is unavailable.");
            }

            if (worldRestored)
            {
                List<DungeonSaveSectionEnvelope> after =
                    saveSections.CaptureAll();
                bool byteExact = SaveEnvelopesEqual(originalWorld, after);
                if (!byteExact && SaveEnvelopesEqual(
                        originalWorld,
                        after,
                        allowExpectedRestoreNormalization: true))
                    cleanupResult = "canonical-except-gameplay-outcome-world-epoch"
                        + "-and-meta-elapsed-float-rounding-under-0.00001s";
                else if (byteExact)
                    cleanupResult = CommittedInvasionWarningOnly
                        ? "post-restore-scenario-baseline-byte-exact;"
                            + "original-transient-visitors=playmode-discard"
                        : "byte-exact";
                else
                {
                    Directory.CreateDirectory("Temp/IndustrialInfrastructure/cleanup-diff");
                    foreach (DungeonSaveSectionEnvelope before in originalWorld)
                    {
                        DungeonSaveSectionEnvelope restored = after.SingleOrDefault(value => value.sectionId == before.sectionId);
                        if (restored != null && before.payloadJson == restored.payloadJson) continue;
                        report.Add("cleanup-difference=" + before.sectionId);
                        File.WriteAllText("Temp/IndustrialInfrastructure/cleanup-diff/" + before.sectionId + ".before.json", before.payloadJson);
                        File.WriteAllText("Temp/IndustrialInfrastructure/cleanup-diff/" + before.sectionId + ".after.json", restored?.payloadJson ?? "null");
                    }
                    throw new InvalidOperationException(
                        "whole-registry baseline is not byte-equivalent after cleanup.");
                }
            }
        }
        catch (Exception exception)
        {
            RecordCleanupFailure(
                "산업 PlayMode FacilityBuffer 권위/byte 복원 실패.",
                exception);
        }

        CheckCommittedInvasionPersistenceIntegrity();

        if (cleanupFailures.Count == 0)
        {
            report.Add("cleanup=" + (cleanupResult ?? "not-required"));
            return;
        }

        report.Add("cleanup=FAIL");
        for (int index = 0; index < cleanupFailures.Count; index++)
        {
            report.Add("cleanupFailure[" + index + "]="
                + DescribeExceptionChain(cleanupFailures[index]));
        }
    }

    private void EstablishCommittedInvasionRestorableBaseline()
    {
        List<DungeonSaveSectionEnvelope> live = saveSections.CaptureAll();
        DungeonSaveSectionEnvelope charactersBefore = live.Single(value =>
            string.Equals(
                value.sectionId,
                CharacterWorldSaveSection.Id,
                StringComparison.Ordinal));
        DungeonCharacterWorldSaveData before = JsonUtility.FromJson<
            DungeonCharacterWorldSaveData>(charactersBefore.payloadJson);
        int visitingBefore = before?.populationProfiles?
            .Count(value => value != null && value.isVisiting) ?? 0;
        DungeonCharacterWorldSaveData expectedCharacters = JsonUtility.FromJson<
            DungeonCharacterWorldSaveData>(charactersBefore.payloadJson);
        foreach (WorldCharacterProfile profile in expectedCharacters
                     ?.populationProfiles ?? new List<WorldCharacterProfile>())
        {
            if (profile == null || !profile.isVisiting)
            {
                continue;
            }
            Require(!profile.isStaff
                    && profile.settlementStanding
                    == CharacterSettlementStanding.Visitor,
                "WIM047 found an unsupported visiting-profile shape before "
                + "baseline restore: " + profile.persistentId + ".");
            profile.isVisiting = false;
            profile.settlementStanding =
                CharacterSettlementStanding.PreparedCandidate;
        }

        DungeonGameRestoreReport restoreReport = new();
        bool restored = saveSections.RestoreAll(live, restoreReport)
            && restoreReport.Success;
        Require(restored,
            "WIM047 could not establish a save-restorable world baseline: "
            + string.Join(" | ", restoreReport.Errors));

        List<DungeonSaveSectionEnvelope> canonical = saveSections.CaptureAll();
        DungeonSaveSectionEnvelope charactersAfter = canonical.Single(value =>
            string.Equals(
                value.sectionId,
                CharacterWorldSaveSection.Id,
                StringComparison.Ordinal));
        DungeonCharacterWorldSaveData after = JsonUtility.FromJson<
            DungeonCharacterWorldSaveData>(charactersAfter.payloadJson);
        int visitingAfter = after?.populationProfiles?
            .Count(value => value != null && value.isVisiting) ?? 0;
        Require(visitingAfter == 0,
            "WIM047 save-restorable baseline retained transient visiting profiles: "
            + visitingAfter + ".");
        Require(live.Count == canonical.Count,
            "WIM047 pre-baseline restore changed the save-section count.");
        foreach (DungeonSaveSectionEnvelope previous in live)
        {
            DungeonSaveSectionEnvelope current = canonical.SingleOrDefault(value =>
                string.Equals(
                    value.sectionId,
                    previous.sectionId,
                    StringComparison.Ordinal));
            Require(current != null
                    && current.sectionVersion == previous.sectionVersion
                    && current.restorePhase == previous.restorePhase
                    && current.optional == previous.optional,
                "WIM047 pre-baseline restore changed save-section identity: "
                + previous.sectionId + ".");
            string expectedPayload = string.Equals(
                    previous.sectionId,
                    CharacterWorldSaveSection.Id,
                    StringComparison.Ordinal)
                ? JsonUtility.ToJson(expectedCharacters)
                : previous.payloadJson;
            bool payloadMatches = string.Equals(
                expectedPayload,
                current.payloadJson,
                StringComparison.Ordinal);
            if (!payloadMatches
                && string.Equals(
                    previous.sectionId,
                    MetaProgressionSaveSection.Id,
                    StringComparison.Ordinal))
            {
                payloadMatches = MetaClockRoundingOnly(
                    expectedPayload,
                    current.payloadJson);
            }
            Require(payloadMatches,
                "WIM047 pre-baseline restore changed an unexpected durable field: "
                + previous.sectionId + ".");
        }
        report.Add("wim047-restorable-scenario-baseline=PASS;visitingProfiles="
            + visitingBefore + "->" + visitingAfter
            + ";allowedChange=characters.world:isVisiting:true-to-false+"
            + "settlementStanding:Visitor-to-PreparedCandidate-only;"
            + "metaClockRoundingUnder=0.00001s;"
            + "originalTransientVisitorStateRestored=False;"
            + "terminalDisposition=PlayModeDiscard");
    }

    private void IsolateCommittedInvasionPersistence()
    {
        string evidencePrefix = CommandOutcomesOnly
            ? "phase80-infrastructure-command"
            : "wim047";
        IMetaProfileStore profileStore = scope.Container.Resolve<
            IMetaProfileStore>();
        IDungeonSaveSlotCatalog slotCatalog = scope.Container.Resolve<
            IDungeonSaveSlotCatalog>();
        string[] paths =
        {
            profileStore.ProfilePath,
            slotCatalog.GetPath(DungeonGameSaveSlotService.AutoSaveSlot),
            slotCatalog.GetPath(DungeonGameSaveSlotService.QuickSaveSlot),
            slotCatalog.GetPath(DungeonGameSaveSlotService.ManualSaveSlot)
        };
        Require(paths.All(path => !string.IsNullOrWhiteSpace(path))
                && paths.Select(Path.GetFullPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count()
                    == paths.Length,
            "WIM047 real profile/save-slot paths are missing or duplicated.");
        foreach (string path in paths)
        {
            string fullPath = Path.GetFullPath(path);
            realPersistenceBefore.Add(
                fullPath,
                CaptureFileFingerprint(fullPath));
        }

        isolatedAutosave = scope.Container.Resolve<IDungeonSaveCommandService>()
            as DungeonAutosaveService;
        isolatedMetaPersistence = scope.Container.Resolve<
            MetaProfilePersistenceService>();
        Require(isolatedAutosave != null && isolatedMetaPersistence != null,
            "WIM047 production autosave/meta persistence services are unavailable.");
        isolatedAutosave.Dispose();
        isolatedMetaPersistence.Dispose();
        report.Add(evidencePrefix
            + "-persistence-isolation=PASS;autosave=True;"
            + "metaProfile=True;realFiles=" + realPersistenceBefore.Count
            + ";terminalDisposition=PlayModeDiscard");
    }

    private void CheckCommittedInvasionPersistenceIntegrity()
    {
        if ((!CommittedInvasionWarningOnly
                && !TemporarySupplyOnly
                && !CommandOutcomesOnly)
            || realPersistenceBefore.Count == 0)
        {
            return;
        }
        try
        {
            string[] changed = realPersistenceBefore
                .Where(pair => !string.Equals(
                    pair.Value,
                    CaptureFileFingerprint(pair.Key),
                    StringComparison.Ordinal))
                .Select(pair => pair.Key)
                .ToArray();
            Require(changed.Length == 0,
                "WIM047 changed real profile/save-slot bytes; files will not be rewritten: "
                + string.Join(",", changed));
            string evidencePrefix = CommandOutcomesOnly
                ? "phase80-infrastructure-command"
                : "wim047";
            report.Add(evidencePrefix + "-real-persistence=PASS;unchanged="
                + realPersistenceBefore.Count + "/"
                + realPersistenceBefore.Count);
        }
        catch (Exception exception)
        {
            RecordCleanupFailure(
                "WIM047 real profile/save-slot integrity check failed.",
                exception);
        }
    }

    private static string CaptureFileFingerprint(string path)
    {
        if (!File.Exists(path))
        {
            return "MISSING";
        }
        using FileStream stream = File.Open(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using SHA256 sha = SHA256.Create();
        return stream.Length + ":"
            + BitConverter.ToString(sha.ComputeHash(stream))
                .Replace("-", string.Empty);
    }

    private void RecordCleanupFailure(string message, Exception exception) =>
        cleanupFailures.Add(new InvalidOperationException(message, exception));

    private static string DescribeExceptionChain(Exception exception)
    {
        List<string> messages = new();
        for (Exception current = exception; current != null; current = current.InnerException)
        {
            messages.Add((current.Message ?? current.GetType().Name)
                .Replace('\r', ' ')
                .Replace('\n', ' '));
        }
        return string.Join(" <- ", messages);
    }

    private static bool SaveEnvelopesEqual(
        IReadOnlyList<DungeonSaveSectionEnvelope> left,
        IReadOnlyList<DungeonSaveSectionEnvelope> right,
        bool allowExpectedRestoreNormalization = false)
    {
        if (left == null || right == null || left.Count != right.Count)
            return false;
        for (int index = 0; index < left.Count; index++)
        {
            DungeonSaveSectionEnvelope a = left[index];
            DungeonSaveSectionEnvelope b = right[index];
            bool payloadMatches = string.Equals(
                a.payloadJson,
                b.payloadJson,
                StringComparison.Ordinal);
            if (!payloadMatches && allowExpectedRestoreNormalization)
            {
                payloadMatches = a.sectionId == MetaProgressionSaveSection.Id
                    ? MetaClockRoundingOnly(a.payloadJson, b.payloadJson)
                    : a.sectionId == GameplayOutcomeLedgerSaveSection.Id
                        && GameplayOutcomeEpochOnly(
                            a.payloadJson,
                            b.payloadJson);
            }
            if (!string.Equals(a.sectionId, b.sectionId, StringComparison.Ordinal)
                || a.sectionVersion != b.sectionVersion
                || a.restorePhase != b.restorePhase
                || a.optional != b.optional
                || !payloadMatches)
            {
                return false;
            }
        }
        return true;
    }

    private static bool MetaClockRoundingOnly(string before, string after)
    {
        DungeonMetaProgressionSaveData left = JsonUtility.FromJson<DungeonMetaProgressionSaveData>(before);
        DungeonMetaProgressionSaveData right = JsonUtility.FromJson<DungeonMetaProgressionSaveData>(after);
        if (left?.runProgress == null || right?.runProgress == null
            || !(Math.Abs((double)left.runProgress.elapsedSeconds - right.runProgress.elapsedSeconds) < 0.00001d))
            return false;
        // MetaRunProgressTracker reprojects clock - (clock - elapsed) as float.
        // This fixture exception never applies to physical stock, currency,
        // orders, destination state, or production restore validation.
        right.runProgress.elapsedSeconds = left.runProgress.elapsedSeconds;
        return JsonUtility.ToJson(left) == JsonUtility.ToJson(right);
    }

    private static bool GameplayOutcomeEpochOnly(string before, string after)
    {
        GameplayOutcomeLedgerSaveData left = JsonUtility.FromJson<
            GameplayOutcomeLedgerSaveData>(before);
        GameplayOutcomeLedgerSaveData right = JsonUtility.FromJson<
            GameplayOutcomeLedgerSaveData>(after);
        if (left == null || right == null)
            return false;
        left.worldEpoch = 0L;
        right.worldEpoch = 0L;
        foreach (GameplayOutcomeConsolidationJobSnapshot job in
                 left.consolidationJobs
                 ?? new List<GameplayOutcomeConsolidationJobSnapshot>())
        {
            if (job != null)
                job.worldEpoch = 0L;
        }
        foreach (GameplayOutcomeConsolidationJobSnapshot job in
                 right.consolidationJobs
                 ?? new List<GameplayOutcomeConsolidationJobSnapshot>())
        {
            if (job != null)
                job.worldEpoch = 0L;
        }
        return JsonUtility.ToJson(left) == JsonUtility.ToJson(right);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
