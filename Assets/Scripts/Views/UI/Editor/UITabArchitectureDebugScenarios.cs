using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class UITabArchitectureDebugScenarios
{
    [MenuItem("DungeonStory/Debug/UI/Run Tab Architecture Checks")]
    public static void RunAllFromMenu()
    {
        RunAll();
    }

    public static void RunAll()
    {
        VerifyCatalogContract();
        VerifyDisplayLabelDoesNotOwnRouting();
        VerifyContentPresenterRegistration();
        VerifyFeaturePresenterRegistration();
        VerifyManagerHasNoLabelReverseLookup();
        Debug.Log("UITabArchitectureDebugScenarios passed: stable ids, label-independent routing, and presenter registries.");
    }

    public static void RunLiveLabelRoutingCheck()
    {
        Require(Application.isPlaying, "Live tab routing check requires PlayMode.");
        UITabButtonBinding binding = UnityEngine.Object.FindObjectsByType<UITabButtonBinding>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault((candidate) => candidate.Id == TabId.Research);
        UITabIdentity panel = UnityEngine.Object.FindObjectsByType<UITabIdentity>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault((candidate) => candidate.Id == TabId.Research);
        Require(binding != null && panel != null, "Research tab binding or panel is missing in PlayMode.");

        Button button = binding.GetComponent<Button>();
        TMP_Text label = binding.GetComponentInChildren<TMP_Text>(true);
        Require(button != null && label != null, "Research top-tab button is incomplete.");
        string originalLabel = label.text;
        try
        {
            label.text = "표시명과 라우팅 분리 검증";
            if (panel.gameObject.activeSelf)
            {
                button.onClick.Invoke();
            }

            button.onClick.Invoke();
            Require(panel.gameObject.activeInHierarchy,
                "Changing the research label prevented its stable-ID panel from opening.");
            Require(panel.GetComponent<P0FeatureSurfacePanel>() != null,
                "Research route did not attach the registered feature surface.");
            button.onClick.Invoke();
        }
        finally
        {
            label.text = originalLabel;
        }

        Debug.Log("UITab live label routing passed: changed display text still opened the Research presenter.");
    }

    private static void VerifyCatalogContract()
    {
        Require(UITabCatalog.All.Count == 11, "Expected eleven top-level tab definitions.");
        Require(UITabCatalog.All.Select((definition) => definition.Id).Distinct().Count() == UITabCatalog.All.Count,
            "Top-level tab IDs are not unique.");
        Require(UITabCatalog.All.Select((definition) => definition.Order).Distinct().Count() == UITabCatalog.All.Count,
            "Top-level tab orders are not unique.");
        Require(UITabCatalog.All.All((definition) => !string.IsNullOrWhiteSpace(definition.ButtonLabel)),
            "A top-level tab has no display label.");
        Require(UITabCatalog.TryFromLegacyId(0, out TabId construction) && construction == TabId.Construction,
            "Serialized construction UnityEvent compatibility changed.");
        Require(!UITabCatalog.TryFromLegacyId(999, out _), "Unknown legacy tab ID was accepted.");
    }

    private static void VerifyDisplayLabelDoesNotOwnRouting()
    {
        GameObject buttonObject = new GameObject("TabArchitectureButton", typeof(RectTransform), typeof(Image), typeof(Button));
        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        try
        {
            labelObject.transform.SetParent(buttonObject.transform, false);
            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            UITabButtonBinding binding = buttonObject.AddComponent<UITabButtonBinding>();
            binding.Set(TabId.Research);

            label.text = "연구";
            TabId before = binding.Id;
            label.text = "완전히 다른 표시 문구";
            TabId after = binding.Id;

            Require(before == TabId.Research && after == TabId.Research,
                "Changing display text changed the button route.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(buttonObject);
        }
    }

    private static void VerifyContentPresenterRegistration()
    {
        UITabContentTextProvider provider = new UITabContentTextProvider(new IUITabContentPresenter[]
        {
            new ScenarioContentPresenter(TabId.Buildings, "registered-building-presenter")
        });
        Require(provider.Build(TabId.Buildings) == "registered-building-presenter",
            "Registered content presenter was not resolved by stable ID.");
        Require(provider.Build(TabId.Shop) == string.Empty,
            "Unregistered content tab did not return an empty placeholder.");

        RequireThrows<InvalidOperationException>(() => new UITabContentTextProvider(new IUITabContentPresenter[]
        {
            new ScenarioContentPresenter(TabId.Buildings, "first"),
            new ScenarioContentPresenter(TabId.Buildings, "duplicate")
        }), "Duplicate content presenter IDs were accepted.");
    }

    private static void VerifyFeaturePresenterRegistration()
    {
        IFeatureSurfaceTabPresenter[] complete =
        {
            new ScenarioFeaturePresenter(TabId.Buildings),
            new ScenarioFeaturePresenter(TabId.Shop),
            new ScenarioFeaturePresenter(TabId.Warehouse),
            new ScenarioFeaturePresenter(TabId.Operations),
            new ScenarioFeaturePresenter(TabId.Defense),
            new ScenarioFeaturePresenter(TabId.Expedition),
            new ScenarioFeaturePresenter(TabId.Research),
            new ScenarioFeaturePresenter(TabId.Codex),
            new ScenarioFeaturePresenter(TabId.Industry)
        };
        FeatureSurfaceTabPresenterRegistry registry = new FeatureSurfaceTabPresenterRegistry(complete);
        foreach (UITabDefinition definition in UITabCatalog.All
                     .Where((candidate) => candidate.SurfaceKind == UITabSurfaceKind.Feature))
        {
            Require(registry.TryGet(definition.Id, out IFeatureSurfaceTabPresenter presenter)
                    && presenter.Id == definition.Id,
                $"Feature presenter missing for {definition.Id}.");
        }

        RequireThrows<InvalidOperationException>(() => new FeatureSurfaceTabPresenterRegistry(
                complete.Where((presenter) => presenter.Id != TabId.Codex)),
            "Missing feature presenter was accepted.");
        RequireThrows<InvalidOperationException>(() => new FeatureSurfaceTabPresenterRegistry(
                complete.Concat(new IFeatureSurfaceTabPresenter[]
                {
                    new ScenarioFeaturePresenter(TabId.Buildings)
                })),
            "Duplicate feature presenter ID was accepted.");
    }

    private static void VerifyManagerHasNoLabelReverseLookup()
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        string[] forbiddenMethodNames =
        {
            "TryGetTopTabId",
            "NormalizeTopTabLabel",
            "GetButtonLabel"
        };
        MethodInfo[] methods = typeof(UITabManager).GetMethods(flags);
        foreach (string methodName in forbiddenMethodNames)
        {
            Require(methods.All((method) => method.Name != methodName),
                $"UITabManager still routes through display text method {methodName}.");
        }

        Require(typeof(UITabManager).GetFields(flags)
                .All((field) => field.FieldType != typeof(Dictionary<string, int>)),
            "UITabManager still owns a display-label-to-integer route dictionary.");
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class ScenarioContentPresenter : IUITabContentPresenter
    {
        private readonly string value;

        public ScenarioContentPresenter(TabId id, string value)
        {
            Id = id;
            this.value = value;
        }

        public TabId Id { get; }
        public string Build() => value;
    }

    private sealed class ScenarioFeaturePresenter : IFeatureSurfaceTabPresenter
    {
        public ScenarioFeaturePresenter(TabId id)
        {
            Id = id;
        }

        public TabId Id { get; }
        public void Present(IFeatureSurfaceView view)
        {
        }
    }
}
