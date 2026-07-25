using UnityEngine;
using UnityEngine.UI;

public interface IDungeonUiCanvasProvider
{
    Canvas GetOrCreateCanvas();
}

public interface IEventAlertCanvasProvider : IDungeonUiCanvasProvider
{
}

public sealed class EventAlertCanvasProvider : IEventAlertCanvasProvider, IDungeonUiCanvasProvider
{
    private readonly DungeonSceneRuntimeReferences sceneReferences;
    private Canvas runtimeCanvas;

    public EventAlertCanvasProvider(DungeonSceneRuntimeReferences sceneReferences)
    {
        this.sceneReferences = sceneReferences
            ?? throw new System.ArgumentNullException(nameof(sceneReferences));
    }

    public Canvas GetOrCreateCanvas()
    {
        if (sceneReferences.Canvas != null)
        {
            return sceneReferences.Canvas;
        }

        if (runtimeCanvas != null)
        {
            return runtimeCanvas;
        }

        GameObject canvasObject = new GameObject("RuntimeUICanvas");
        runtimeCanvas = canvasObject.AddComponent<Canvas>();
        runtimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
        return runtimeCanvas;
    }
}
