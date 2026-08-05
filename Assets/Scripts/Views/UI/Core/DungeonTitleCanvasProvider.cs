using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class DungeonTitleCanvasProvider : IDungeonUiCanvasProvider
{
    private readonly SceneUiBootstrapReferences runtimeReferences;
    private Canvas canvas;

    public DungeonTitleCanvasProvider(
        SceneUiBootstrapReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
    }

    public Canvas GetOrCreateCanvas()
    {
        if (canvas != null)
        {
            return canvas;
        }

        EnsureEventSystem();
        GameObject canvasObject = new GameObject(
            "TitleCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private void EnsureEventSystem()
    {
        if (runtimeReferences.EventSystem != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));
        runtimeReferences.RegisterEventSystem(
            eventSystemObject.GetComponent<EventSystem>());
    }
}
