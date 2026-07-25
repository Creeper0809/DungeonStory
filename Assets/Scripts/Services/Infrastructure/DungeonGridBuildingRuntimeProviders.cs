using System;
using UnityEngine;

public interface IDungeonGridBuildingControllerProvider
{
    DungeonStoryGridBuildingController Controller { get; }
}

public interface IWorldPointerPositionProvider
{
    Vector3 MouseWorldPosition { get; }
}

public interface IMainCameraProvider
{
    Camera Camera { get; }
}

public interface IGridTextureProvider
{
    GridTexture Texture { get; }
}

public sealed class DungeonGridBuildingControllerProvider : IDungeonGridBuildingControllerProvider
{
    private readonly DungeonSceneRuntimeReferences sceneReferences;

    public DungeonGridBuildingControllerProvider(
        DungeonSceneRuntimeReferences sceneReferences)
    {
        this.sceneReferences = sceneReferences
            ?? throw new ArgumentNullException(nameof(sceneReferences));
    }

    public DungeonStoryGridBuildingController Controller =>
        sceneReferences.GridBuildingController != null
            ? sceneReferences.GridBuildingController
            : throw new InvalidOperationException(
                $"{nameof(IDungeonGridBuildingControllerProvider)} requires a registered {nameof(DungeonStoryGridBuildingController)}.");
}

public sealed class GridTextureProvider : IGridTextureProvider
{
    private readonly DungeonSceneRuntimeReferences sceneReferences;

    public GridTextureProvider(DungeonSceneRuntimeReferences sceneReferences)
    {
        this.sceneReferences = sceneReferences
            ?? throw new ArgumentNullException(nameof(sceneReferences));
    }

    public GridTexture Texture => sceneReferences.GridTexture != null
        ? sceneReferences.GridTexture
        : throw new InvalidOperationException(
            $"{nameof(IGridTextureProvider)} requires a registered {nameof(GridTexture)}.");
}

public sealed class SceneCameraWorldPointerPositionProvider : IWorldPointerPositionProvider
{
    private readonly IMainCameraProvider cameraProvider;
    private readonly IPlayerInputReader inputReader;

    public SceneCameraWorldPointerPositionProvider(
        IMainCameraProvider cameraProvider,
        IPlayerInputReader inputReader)
    {
        this.cameraProvider = cameraProvider ?? throw new ArgumentNullException(nameof(cameraProvider));
        this.inputReader = inputReader ?? throw new ArgumentNullException(nameof(inputReader));
    }

    public Vector3 MouseWorldPosition
    {
        get
        {
            Camera camera = cameraProvider.Camera;
            Vector3 mousePosition = inputReader.MousePosition;
            return camera.ScreenToWorldPoint(new Vector3(
                mousePosition.x,
                mousePosition.y,
                -camera.transform.position.z));
        }
    }
}

public sealed class SceneMainCameraProvider : IMainCameraProvider
{
    private readonly DungeonSceneRuntimeReferences sceneReferences;

    public SceneMainCameraProvider(DungeonSceneRuntimeReferences sceneReferences)
    {
        this.sceneReferences = sceneReferences
            ?? throw new ArgumentNullException(nameof(sceneReferences));
    }

    public Camera Camera => sceneReferences.MainCamera != null
        ? sceneReferences.MainCamera
        : throw new InvalidOperationException(
            $"{nameof(IMainCameraProvider)} requires a registered {nameof(Camera)}.");
}
