using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine;

public sealed class SceneUiBootstrapReferences
{
    public SceneUiBootstrapReferences(EventSystem eventSystem)
    {
        EventSystem = eventSystem;
    }

    public EventSystem EventSystem { get; private set; }

    public void RegisterEventSystem(EventSystem eventSystem)
    {
        EventSystem = eventSystem
            ?? throw new ArgumentNullException(nameof(eventSystem));
    }
}

public sealed class DungeonUserSettingsRuntimeTargets
{
    public DungeonUserSettingsRuntimeTargets(
        CameraManager cameraManager,
        IReadOnlyList<DungeonUiThemeRuntime> themes,
        GameManager gameManager = null)
    {
        CameraManager = cameraManager;
        Themes = themes ?? Array.Empty<DungeonUiThemeRuntime>();
        GameManager = gameManager;
    }

    public CameraManager CameraManager { get; }
    public IReadOnlyList<DungeonUiThemeRuntime> Themes { get; }
    public GameManager GameManager { get; }
}

public sealed class SceneValidationReferences
{
    public SceneValidationReferences(
        IReadOnlyList<GameObject> roots,
        IReadOnlyList<BuildableObject> buildables,
        IReadOnlyList<LocalLlmRequestQueue> localLlmQueues)
    {
        Roots = roots ?? Array.Empty<GameObject>();
        Buildables = buildables ?? Array.Empty<BuildableObject>();
        LocalLlmQueues = localLlmQueues ?? Array.Empty<LocalLlmRequestQueue>();
    }

    public IReadOnlyList<GameObject> Roots { get; }
    public IReadOnlyList<BuildableObject> Buildables { get; }
    public IReadOnlyList<LocalLlmRequestQueue> LocalLlmQueues { get; }
}

public sealed class DungeonSceneRuntimeReferences
{
    public DungeonSceneRuntimeReferences(
        UIManager uiManager,
        OperatingDaySettlementRuntime settlement,
        EventAlertRuntime alerts,
        RunVariableRuntime runVariables,
        Canvas canvas,
        GameManager gameManager,
        GridSystemManager gridSystemManager,
        DungeonStoryGridBuildingController gridBuildingController,
        GridTexture gridTexture,
        Camera mainCamera,
        OwnerSelectionPanel ownerSelectionPanel,
        UIBuildingInfo buildingInfo = null)
    {
        UIManager = uiManager;
        Settlement = settlement;
        Alerts = alerts;
        RunVariables = runVariables;
        Canvas = canvas;
        GameManager = gameManager;
        GridSystemManager = gridSystemManager;
        GridBuildingController = gridBuildingController;
        GridTexture = gridTexture;
        MainCamera = mainCamera;
        OwnerSelectionPanel = ownerSelectionPanel;
        BuildingInfo = buildingInfo;
    }

    public UIManager UIManager { get; }
    public OperatingDaySettlementRuntime Settlement { get; }
    public EventAlertRuntime Alerts { get; }
    public RunVariableRuntime RunVariables { get; }
    public Canvas Canvas { get; }
    public GameManager GameManager { get; private set; }
    public GridSystemManager GridSystemManager { get; }
    public DungeonStoryGridBuildingController GridBuildingController { get; private set; }
    public GridTexture GridTexture { get; private set; }
    public Camera MainCamera { get; private set; }
    public OwnerSelectionPanel OwnerSelectionPanel { get; private set; }
    public UIBuildingInfo BuildingInfo { get; private set; }

    public void RegisterGameManager(GameManager gameManager)
    {
        GameManager = gameManager;
    }

    public void RegisterGridBuildingController(
        DungeonStoryGridBuildingController controller)
    {
        GridBuildingController = controller;
    }

    public void RegisterGridTexture(GridTexture gridTexture)
    {
        GridTexture = gridTexture;
    }

    public void RegisterMainCamera(Camera camera)
    {
        MainCamera = camera;
    }

    public void RegisterOwnerSelectionPanel(OwnerSelectionPanel panel)
    {
        OwnerSelectionPanel = panel;
    }

    public void RegisterBuildingInfo(UIBuildingInfo buildingInfo)
    {
        BuildingInfo = buildingInfo;
    }
}
