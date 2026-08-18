#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class IndustrialInfrastructurePlayModeVerifier
{
    public const string ReportPath =
        "Temp/IndustrialInfrastructure/playmode-live-report.txt";
    public const string ScreenshotPath =
        "Temp/IndustrialInfrastructure/playmode-live.png";

    [MenuItem(
        "DungeonStory/Debug/Infrastructure/Run Live Industrial PlayMode Scenario")]
    public static void Run()
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
        runnerObject.AddComponent<
            IndustrialInfrastructurePlayModeVerificationRunner>();
    }
}

public sealed class IndustrialInfrastructurePlayModeVerificationRunner :
    MonoBehaviour
{
    private const string ConveyorDestination = "qa:industrial-output";
    private const string NormalStackIdPrefix = "qa:normal-stack";
    private const string OverflowPayloadPrefix = "qa:overflow-payload:";
    private const float TimeoutSeconds = 12f;

    private readonly List<BuildableObject> createdBuildings =
        new List<BuildableObject>();
    private readonly List<string> report = new List<string>();

    private DungeonRuntimeLifetimeScope scope;
    private Grid grid;
    private IGridBuildingObjectFactory buildingFactory;
    private IPowerInfrastructureQuery power;
    private IPowerInfrastructurePersistence powerPersistence;
    private IFluidInfrastructureQuery water;
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
    private IWorldItemStackRuntime items;
    private IItemTransferService itemTransfers;
    private IGameClock clock;
    private GameManager gameManager;
    private OwnerSelectionPanel ownerSelection;
    private Camera mainCamera;
    private Vector3 originalCameraPosition;
    private float originalCameraSize;
    private float originalTimeScale;
    private bool originalPause;
    private bool ownerSelectionWasActive;

    private DungeonPowerInfrastructureSaveData originalPower;
    private DungeonFluidInfrastructureSaveData originalFluid;
    private DungeonConveyorInfrastructureSaveData originalConveyor;
    private DungeonAutomationSaveData originalAutomation;
    private DungeonPhysicalItemSaveData originalItems;
    private Exception verificationFailure;

    private IEnumerator Start()
    {
        yield return ExecuteGuarded(RunVerification());

        bool passed = verificationFailure == null;
        report.Add(passed ? "result=PASS" : "result=FAIL");
        if (!passed)
        {
            report.Add("failure=" + verificationFailure.Message);
            Debug.LogException(verificationFailure);
        }

        WriteReport();
        Cleanup();

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
                + verificationFailure);
        }

        Destroy(gameObject);
    }

    private IEnumerator RunVerification()
    {
            yield return ResolveRuntime();
            CaptureOriginalState();
            VerifyDetachedRestorePreparation();
            ConfigureVerificationTime();
            SetOwnerSelectionVisible(false);

            Dictionary<string, BuildingSO> assets = LoadAssets();
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

            yield return VerifyPowerAndFluids();
            yield return VerifyConveyorTransport(origin);
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
        powerPersistence =
            scope.Container.Resolve<IPowerInfrastructurePersistence>();
        water = scope.Container.Resolve<IFluidInfrastructureQuery>();
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
        items = scope.Container.Resolve<IWorldItemStackRuntime>();
        itemTransfers = scope.Container.Resolve<IItemTransferService>();
        clock = scope.Container.Resolve<IGameClock>();
        gameManager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        ownerSelection =
            UnityEngine.Object.FindFirstObjectByType<OwnerSelectionPanel>();
        mainCamera = Camera.main;
    }

    private void CaptureOriginalState()
    {
        originalPower = powerPersistence.Capture();
        originalFluid = fluidPersistence.Capture();
        originalConveyor = conveyorPersistence.Capture();
        originalAutomation = automationPersistence.Capture();
        originalItems = items.Capture();
        originalTimeScale = Time.timeScale;
        originalPause = gameManager != null && gameManager.isPause;
        ownerSelectionWasActive =
            ownerSelection != null && ownerSelection.gameObject.activeSelf;
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
    }

    private void SetOwnerSelectionVisible(bool visible)
    {
        if (ownerSelection != null)
        {
            ownerSelection.gameObject.SetActive(visible);
        }
    }

    private static Dictionary<string, BuildingSO> LoadAssets()
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

        throw new InvalidOperationException(
            "실제 플레이 Grid에서 12x3 산업 검증 구역을 확보하지 못했습니다.");
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

    private static List<(BuildingSO Asset, Vector2Int Position)>
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
        Add("I07", 3, 0);
        Add("I08", 5, 0);
        Add("I14", 7, 0);
        Add("I09", 9, 0);
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
        string generatorDestination = "power:" + GetNodeId(generator);
        Require(items.SpawnItemAt(
                "resource:mana-crystal",
                1,
                generator.centerPos,
                WorldItemStackState.FacilityBuffer,
                generatorDestination,
                out int spawnedFuel)
            && spawnedFuel == 1,
            "마나 발전기에 실제 연료 스택을 공급하지 못했습니다.");

        yield return WaitUntil(
            () => power.IsPowered(pump),
            "발전기와 연결된 양수 펌프에 전력이 공급되지 않았습니다.");
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

        PowerNetworkSnapshot poweredNetwork = power.Networks.First(
            network => network.Nodes.Any(node =>
                node.BuildingId.Equals(
                    generator.RequirePersistentInstanceId())));
        FluidNetworkSnapshot cleanNetwork = water.Networks.First(
            network => network.Channel == UtilityChannel.CleanWater
                && network.CleanWater > 0f);
        FluidNetworkSnapshot wasteNetwork = water.Networks.First(
            network => network.Channel == UtilityChannel.Wastewater
                && network.Wastewater > 0f);
        report.Add(
            $"power={poweredNetwork.ProductionPerSecond:0.##}/"
            + $"{poweredNetwork.DemandPerSecond:0.##}");
        report.Add($"cleanWater={cleanNetwork.CleanWater:0.###}");
        report.Add($"wastewater={wasteNetwork.Wastewater:0.###}");
    }

    private IEnumerator VerifyConveyorTransport(Vector2Int origin)
    {
        BuildableObject input = FindBuilding("C02");
        BuildableObject output = FindBuilding("C03");
        Require(conveyorCommands.SetPortDestination(
                input,
                ConveyorDestination).Succeeded,
            "컨베이어 입력 목적지를 설정하지 못했습니다.");
        Require(conveyorCommands.SetPortDestination(
                output,
                ConveyorDestination).Succeeded,
            "컨베이어 출력 목적지를 설정하지 못했습니다.");
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
            .FirstOrDefault(stack =>
                stack.ItemId == "material:lumber");
        Require(source != null,
            "컨베이어 입력 스택 ID를 확인하지 못했습니다.");

        string originalStackId = source.StackId;
        Require(conveyorTransactions.TryLoadStack(
                new ItemStackId(originalStackId),
                input,
                ConveyorDestination,
                out string payloadId,
                out DomainFailure loadFailure),
            "실제 물리 스택을 컨베이어에 적재하지 못했습니다: "
            + loadFailure.Code);
        Require(!string.IsNullOrWhiteSpace(payloadId),
            "컨베이어 화물 ID가 생성되지 않았습니다.");

        yield return WaitUntil(
            () => items.GetStacksAt(
                    output.centerPos,
                    includeStored: true)
                .Any(stack =>
                    stack.StackId == originalStackId
                    && stack.State == WorldItemStackState.FacilityBuffer
                    && stack.DestinationId == ConveyorDestination),
            "화물이 실제 벨트를 이동해 출력 버퍼에 도착하지 않았습니다.");
        Require(conveyor.Networks.Sum(network => network.PayloadCount) == 0,
            "정상 배송이 끝난 뒤 컨베이어 화물이 남았습니다.");
        report.Add(
            $"normalPayload={payloadId};"
            + $"distance={Mathf.Abs(output.centerPos.x - origin.x)}");
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
            BuildableObject nodeBuilding = createdBuildings.First(building =>
                string.Equals(
                    GetNodeId(building),
                    nodeIds[index],
                    StringComparison.Ordinal));
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
                stack.StackId == transitStackIds[0]),
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
    }

    private void Cleanup()
    {
        Time.timeScale = originalTimeScale;
        if (gameManager != null)
        {
            gameManager.isPause = originalPause;
        }

        SetOwnerSelectionVisible(ownerSelectionWasActive);
        if (mainCamera != null)
        {
            mainCamera.transform.position = originalCameraPosition;
            mainCamera.orthographicSize = originalCameraSize;
        }

        for (int index = createdBuildings.Count - 1; index >= 0; index--)
        {
            BuildableObject building = createdBuildings[index];
            if (building == null)
            {
                continue;
            }

            BuildingSO data = building.BuildingData;
            if (data != null)
            {
                grid.RemoveOccupant(
                    building,
                    data.layer,
                    data.GetGridPosList(building.centerPos),
                    data.Placement.IsMovement);
            }

            Destroy(building.gameObject);
        }

        createdBuildings.Clear();
        if (powerPersistence != null && originalPower != null)
        {
            powerPersistence.Restore(
                powerPersistence.PrepareRestore(originalPower));
        }
        if (fluidPersistence != null && originalFluid != null)
        {
            fluidPersistence.Restore(
                fluidPersistence.PrepareRestore(originalFluid));
        }
        items?.Restore(originalItems);
        if (conveyorPersistence != null && originalConveyor != null)
        {
            conveyorPersistence.Restore(
                conveyorPersistence.PrepareRestore(originalConveyor));
        }
        if (automationPersistence != null && originalAutomation != null)
        {
            automationPersistence.Restore(
                automationPersistence.PrepareRestore(originalAutomation));
        }
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
