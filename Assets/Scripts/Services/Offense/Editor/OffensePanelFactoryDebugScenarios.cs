using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public static class OffensePanelFactoryDebugScenarios
{
    public static bool RunPlayModeSmoke(out string report)
    {
        report = string.Empty;
        if (!EditorApplication.isPlaying)
        {
            report = "PlayMode is required.";
            return false;
        }

        LifetimeScope scope = Object.FindFirstObjectByType<LifetimeScope>(FindObjectsInactive.Include);
        if (scope == null || scope.Container == null)
        {
            report = "No active LifetimeScope/container.";
            return false;
        }

        IOffensePanelFactory factory = scope.Container.Resolve<IOffensePanelFactory>();
        IOffensePanelService service = scope.Container.Resolve<IOffensePanelService>();
        if (factory == null || service == null)
        {
            report = $"Resolve failed. factory={factory != null}, service={service != null}";
            return false;
        }

        OffenseWorldMapPanel worldMapPanel = factory.CreateWorldMapPanel();
        OffenseExpeditionPanel expeditionPanel = factory.CreateExpeditionPanel();
        bool worldValid = ValidatePanel(worldMapPanel, "OffenseWorldMapCanvas", 420, out string worldReport);
        bool expeditionValid = ValidatePanel(expeditionPanel, "OffenseExpeditionCanvas", 430, out string expeditionReport);

        report = $"scope={scope.name}, factoryResolved=True, serviceResolved=True, world=({worldReport}), expedition=({expeditionReport})";

        DestroyPanelCanvas(worldMapPanel);
        DestroyPanelCanvas(expeditionPanel);

        return worldValid && expeditionValid;
    }

    private static bool ValidatePanel(Component panel, string canvasName, int sortingOrder, out string report)
    {
        if (panel == null)
        {
            report = "panel=null";
            return false;
        }

        TMP_Text[] texts = panel.GetComponentsInChildren<TMP_Text>(true);
        Canvas canvas = panel.GetComponentInParent<Canvas>();
        bool active = panel.gameObject.activeSelf;
        bool hasTexts = texts.Length >= 2;
        bool canvasConfigured = canvas != null
            && canvas.name == canvasName
            && canvas.renderMode == RenderMode.ScreenSpaceOverlay
            && canvas.sortingOrder == sortingOrder;

        report = $"panel={panel.name}, active={active}, textCount={texts.Length}, canvasConfigured={canvasConfigured}";
        return active && hasTexts && canvasConfigured;
    }

    private static void DestroyPanelCanvas(Component panel)
    {
        if (panel == null)
        {
            return;
        }

        Canvas canvas = panel.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Object.Destroy(canvas.gameObject);
        }
    }
}
