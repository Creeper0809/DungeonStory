using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class UITabManager : MonoBehaviour
{
    public List<UITab> tabList;
    public Transform buttonPanel;

    [SerializeField] private bool autoBindTopButtons = true;
    [SerializeField] private bool autoCreateMissingTabs = true;

    private const float BottomTabBarHeight = 60f;
    private const int HudCanvasSortingOrder = 300;

    private readonly Dictionary<TabId, TMP_Text> generatedTabBodies = new Dictionary<TabId, TMP_Text>();
    private IUITabContentTextProvider contentTextProvider;
    private IUiPopupService popupService;
    private ITmpKoreanFontService tmpKoreanFontService;
    private IUITabGeneratedPanelFactory generatedPanelFactory;
    private IStaffWorkPriorityPanelFactory staffWorkPriorityPanelFactory;
    private IP0FeatureSurfacePanelFactory p0FeatureSurfacePanelFactory;
    private IUITabTopButtonFactory topButtonFactory;
    private IDungeonGridBuildingControllerProvider buildingControllerProvider;
    private IUiClock uiClock;
    private DungeonUiThemeRuntime themeRuntime;
    private bool isConfigured;

    private void Start()
    {
        ConfigureTopTabs();
    }

    [Inject]
    public void Construct(
        IUITabContentTextProvider contentTextProvider,
        IUiPopupService popupService,
        ITmpKoreanFontService tmpKoreanFontService,
        IUITabGeneratedPanelFactory generatedPanelFactory,
        IStaffWorkPriorityPanelFactory staffWorkPriorityPanelFactory,
        IP0FeatureSurfacePanelFactory p0FeatureSurfacePanelFactory,
        IUITabTopButtonFactory topButtonFactory,
        IDungeonGridBuildingControllerProvider buildingControllerProvider,
        IUiClock uiClock)
    {
        this.contentTextProvider = contentTextProvider
            ?? throw new ArgumentNullException(nameof(contentTextProvider));
        this.popupService = popupService
            ?? throw new ArgumentNullException(nameof(popupService));
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
        this.generatedPanelFactory = generatedPanelFactory
            ?? throw new ArgumentNullException(nameof(generatedPanelFactory));
        this.staffWorkPriorityPanelFactory = staffWorkPriorityPanelFactory
            ?? throw new ArgumentNullException(nameof(staffWorkPriorityPanelFactory));
        this.p0FeatureSurfacePanelFactory = p0FeatureSurfacePanelFactory
            ?? throw new ArgumentNullException(nameof(p0FeatureSurfacePanelFactory));
        this.topButtonFactory = topButtonFactory
            ?? throw new ArgumentNullException(nameof(topButtonFactory));
        this.buildingControllerProvider = buildingControllerProvider
            ?? throw new ArgumentNullException(nameof(buildingControllerProvider));
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
    }

    // Compatibility endpoint for existing serialized UnityEvent calls.
    public void ToggleSelectButton(int legacyId)
    {
        if (!UITabCatalog.TryFromLegacyId(legacyId, out TabId tabId))
        {
            Debug.LogWarning($"UITabManager.ToggleSelectButton() : unknown legacy tab id {legacyId}.");
            return;
        }

        ToggleTopTab(tabId);
    }

    public void ToggleTopTab(TabId tabId)
    {
        CancelActiveGridPlacement();
        ConfigureTopTabs();
        EnsureTopButtons();
        EnsureSpecializedTabContent(tabId);
        RefreshGeneratedTab(tabId);

        UITab target = GetTopLevelTabs()
            .FirstOrDefault((tab) => TryGetTabId(tab, out TabId candidateId) && candidateId == tabId);
        bool shouldOpen = target != null && !target.gameObject.activeSelf;

        CloseAllTabsImmediate();

        if (shouldOpen)
        {
            OpenTabImmediate(target);
            EnsureSpecializedTabContent(tabId);
        }

        ResolveThemeRuntime()?.SetActiveTab(shouldOpen ? tabId : null);

        if (target == null)
        {
            Debug.LogWarning($"UITabManager.ToggleTopTab() : no panel is registered for {tabId}.");
        }
    }

    private void CancelActiveGridPlacement()
    {
        DungeonStoryGridBuildingController controller = buildingControllerProvider?.Controller;
        if (controller != null && controller.GridSystem.Mode != GridMode.None)
        {
            controller.SetGridModeNone();
        }
    }

    public void ResgisterTab(UITab tab)
    {
        tabList ??= new List<UITab>();
        if (tab == null || tabList.Contains(tab)) return;
        tabList.Add(tab);
    }

    public void UnRegisterTab(UITab tab)
    {
        if (tabList == null || tab == null || !tabList.Contains(tab)) return;
        tabList.Remove(tab);
    }

    private void ConfigureTopTabs()
    {
        EnsureHudCanvasRendersAboveWorld();

        if (isConfigured) return;
        isConfigured = true;

        tabList ??= new List<UITab>();
        RegisterExistingTabs();
        EnsureLegacyTopTabIdentities();

        if (autoCreateMissingTabs)
        {
            foreach (UITabDefinition definition in UITabCatalog.All.Where((candidate) => candidate.IsGenerated))
            {
                EnsureGeneratedTab(definition);
            }
        }

        EnsureSpecializedTabContent();

        if (autoBindTopButtons)
        {
            EnsureTopButtons();
            BindTopButtons();
        }

        foreach (TabId tabId in generatedTabBodies.Keys.ToArray())
        {
            RefreshGeneratedTab(tabId);
        }

        CloseAllTabsForInitialState();
        ResolveThemeRuntime()?.ApplyNow();
    }

    private void EnsureHudCanvasRendersAboveWorld()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, HudCanvasSortingOrder);
        themeRuntime = DungeonUiThemeRuntime.Ensure(canvas, tmpKoreanFontService, uiClock);
    }

    private void RegisterExistingTabs()
    {
        foreach (UITab tab in GetComponentsInChildren<UITab>(true))
        {
            if (tab != null && !tabList.Contains(tab))
            {
                tabList.Add(tab);
            }
        }

        tabList = tabList
            .Where((tab) => tab != null)
            .Distinct()
            .ToList();
    }

    private void EnsureLegacyTopTabIdentities()
    {
        foreach (UITab tab in tabList.Where((candidate) => candidate != null && candidate.transform.parent == transform))
        {
            if (tab.TryGetComponent(out UITabIdentity _))
            {
                continue;
            }

            if (!UITabCatalog.TryFromLegacyId(tab.id, out TabId legacyId))
            {
                Debug.LogError($"Top-level UITab '{tab.name}' has no {nameof(UITabIdentity)} and legacy id {tab.id} is invalid.");
                continue;
            }

            tab.gameObject.AddComponent<UITabIdentity>().Set(legacyId);
        }
    }

    private void EnsureSpecializedTabContent()
    {
        foreach (UITab tab in GetTopLevelTabs())
        {
            EnsureSpecializedTabContent(tab);
        }
    }

    private void EnsureSpecializedTabContent(TabId id)
    {
        UITabDefinition definition = UITabCatalog.GetRequired(id);
        if (definition.SurfaceKind == UITabSurfaceKind.Construction)
        {
            return;
        }

        foreach (UITab tab in GetTopLevelTabs()
                     .Where((candidate) => TryGetTabId(candidate, out TabId candidateId) && candidateId == id))
        {
            EnsureSpecializedTabContent(tab);
        }
    }

    private void EnsureSpecializedTabContent(UITab tab)
    {
        if (tab == null || !TryGetTabId(tab, out TabId id))
        {
            return;
        }

        UITabDefinition definition = UITabCatalog.GetRequired(id);
        switch (definition.SurfaceKind)
        {
            case UITabSurfaceKind.Staff:
            {
                StaffWorkPriorityPanel panel = RequireStaffWorkPriorityPanelFactory().Ensure(tab.gameObject);
                panel?.Refresh();
                break;
            }
            case UITabSurfaceKind.Feature:
            {
                P0FeatureSurfacePanel panel = RequireP0FeatureSurfacePanelFactory().Ensure(tab.gameObject, id);
                panel?.Refresh();
                break;
            }
        }
    }

    private IEnumerable<UITab> GetAllTabs()
    {
        RegisterExistingTabs();
        return tabList
            .Concat(GetComponentsInChildren<UITab>(true))
            .Where((tab) => tab != null)
            .Distinct();
    }

    private IEnumerable<UITab> GetTopLevelTabs()
    {
        return GetAllTabs().Where((tab) => tab.transform.parent == transform);
    }

    private static bool TryGetTabId(UITab tab, out TabId id)
    {
        UITabIdentity identity = tab != null ? tab.GetComponent<UITabIdentity>() : null;
        id = identity != null ? identity.Id : default;
        return identity != null && UITabCatalog.TryGet(id, out _);
    }

    private void CloseAllTabsImmediate()
    {
        ResolvePopupService().CloseAll();

        foreach (UITab tab in GetAllTabs())
        {
            if (tab.gameObject.activeSelf)
            {
                tab.OnClose();
            }
        }
    }

    private void CloseAllTabsForInitialState()
    {
        foreach (UITab tab in GetAllTabs())
        {
            tab.gameObject.SetActive(false);
        }

        ResolveThemeRuntime()?.SetActiveTab(null);
    }

    private void OpenTabImmediate(UITab tab)
    {
        if (tab == null) return;

        ResolvePopupService().Open(tab);
        tab.gameObject.SetActive(true);
    }

    private void BindTopButtons()
    {
        Transform root = ResolveButtonPanel();
        if (root == null)
        {
            Debug.LogWarning("UITabManager : TabButtons 오브젝트를 찾지 못해 상단 탭 자동 연결을 건너뜁니다.");
            return;
        }

        foreach (Button button in GetDirectButtons(root))
        {
            UITabButtonBinding binding = button.GetComponent<UITabButtonBinding>();
            if (binding == null)
            {
                Debug.LogError($"Top tab button '{button.name}' has no {nameof(UITabButtonBinding)}.");
                continue;
            }

            if (button.onClick.GetPersistentEventCount() > 0)
            {
                continue;
            }

            TabId capturedId = binding.Id;
            button.onClick.AddListener(() => ToggleTopTab(capturedId));
        }
    }

    private Transform ResolveButtonPanel()
    {
        if (buttonPanel != null)
        {
            return buttonPanel;
        }

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "TabButtons")
            {
                buttonPanel = child;
                return buttonPanel;
            }
        }

        return null;
    }

    private void EnsureTopButtons()
    {
        Transform root = ResolveButtonPanel();
        if (root == null)
        {
            return;
        }

        EnsureLegacyButtonBindings(root);
        Button template = GetDirectButtons(root)
            .FirstOrDefault((button) => button.GetComponent<UITabButtonBinding>() != null);

        foreach (UITabDefinition definition in UITabCatalog.All)
        {
            Button existing = FindTopButtonForId(root, definition.Id);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                SetButtonLabel(existing, definition.ButtonLabel);
                continue;
            }

            if (template == null)
            {
                Debug.LogError("UITabManager requires at least one top tab button template with a stable binding.");
                return;
            }

            RequireTopButtonFactory().CreateButton(template, root, definition.Id, definition.ButtonLabel);
        }

        NormalizeTopButtons(root);
        ArrangeTopButtonsInSingleRow(root);

        foreach (Button button in GetDirectButtons(root).Where((candidate) => candidate.gameObject.activeSelf))
        {
            PolishTopButton(button);
        }
    }

    private static void EnsureLegacyButtonBindings(Transform root)
    {
        HashSet<TabId> used = GetDirectButtons(root)
            .Select((button) => button.GetComponent<UITabButtonBinding>())
            .Where((binding) => binding != null)
            .Select((binding) => binding.Id)
            .ToHashSet();
        Queue<TabId> available = new Queue<TabId>(UITabCatalog.All
            .OrderBy((definition) => definition.Order)
            .Select((definition) => definition.Id)
            .Where((id) => !used.Contains(id)));

        foreach (Button button in GetDirectButtons(root)
                     .Where((candidate) => candidate.GetComponent<UITabButtonBinding>() == null)
                     .OrderBy((candidate) => candidate.transform.GetSiblingIndex()))
        {
            if (available.Count == 0)
            {
                Debug.LogError($"Cannot migrate top tab button '{button.name}': no unused tab id remains.");
                continue;
            }

            button.gameObject.AddComponent<UITabButtonBinding>().Set(available.Dequeue());
        }
    }

    private void ArrangeTopButtonsInSingleRow(Transform root)
    {
        RequireTopButtonFactory().EnsureSingleRowLayout(
            root,
            BottomTabBarHeight,
            "TopTabPrimaryRow",
            "TopTabSecondaryRow");
        OrderTopButtons(root);
    }

    private static void OrderTopButtons(Transform root)
    {
        foreach (Button button in GetDirectButtons(root))
        {
            UITabButtonBinding binding = button.GetComponent<UITabButtonBinding>();
            if (binding == null || !UITabCatalog.TryGet(binding.Id, out UITabDefinition definition))
            {
                continue;
            }

            button.transform.SetSiblingIndex(definition.Order);
        }
    }

    private static Button FindTopButtonForId(Transform root, TabId id)
    {
        return GetDirectButtons(root).FirstOrDefault((button) =>
        {
            UITabButtonBinding binding = button.GetComponent<UITabButtonBinding>();
            return binding != null && binding.Id == id;
        });
    }

    private void NormalizeTopButtons(Transform root)
    {
        Button[] buttons = GetDirectButtons(root).ToArray();
        foreach (UITabDefinition definition in UITabCatalog.All)
        {
            Button[] matches = buttons
                .Where((button) => button.TryGetComponent(out UITabButtonBinding binding)
                    && binding.Id == definition.Id)
                .ToArray();
            if (matches.Length == 0)
            {
                continue;
            }

            Button keep = matches[0];
            keep.gameObject.SetActive(true);
            SetButtonLabel(keep, definition.ButtonLabel);
            keep.transform.SetSiblingIndex(definition.Order);

            foreach (Button duplicate in matches.Skip(1))
            {
                duplicate.gameObject.SetActive(false);
            }
        }
    }

    private static IEnumerable<Button> GetDirectButtons(Transform root)
    {
        return root.GetComponentsInChildren<Button>(true)
            .Where((button) => button.transform.parent == root);
    }

    private void SetButtonLabel(Button button, string title)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;

        RequireTmpKoreanFontService().Apply(label);
        label.text = title;
    }

    private void PolishTopButton(Button button)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;

        RequireTmpKoreanFontService().Apply(label);
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f;
        label.fontSizeMax = 24f;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void EnsureGeneratedTab(UITabDefinition definition)
    {
        if (GetTopLevelTabs().Any((tab) =>
                TryGetTabId(tab, out TabId candidateId) && candidateId == definition.Id))
        {
            return;
        }

        GeneratedUITabPanel generatedPanel = RequireGeneratedPanelFactory()
            .Create(transform, definition.Id, definition.PanelTitle);
        tabList.Add(generatedPanel.Tab);
        generatedTabBodies[definition.Id] = generatedPanel.BodyText;
    }

    private void RefreshGeneratedTab(TabId id)
    {
        if (UITabCatalog.GetRequired(id).SurfaceKind == UITabSurfaceKind.Staff)
        {
            EnsureSpecializedTabContent(id);
            return;
        }

        if (!generatedTabBodies.TryGetValue(id, out TMP_Text body) || body == null)
        {
            return;
        }

        body.text = BuildTabContent(id);
    }

    private string BuildTabContent(TabId id)
    {
        if (contentTextProvider == null)
        {
            throw new InvalidOperationException(
                $"{nameof(UITabManager)} requires VContainer injection of {nameof(IUITabContentTextProvider)}.");
        }

        return contentTextProvider.Build(id);
    }

    private IUiPopupService ResolvePopupService()
    {
        return popupService
            ?? throw new InvalidOperationException(
                $"{nameof(UITabManager)} requires VContainer injection of {nameof(IUiPopupService)}.");
    }

    private ITmpKoreanFontService RequireTmpKoreanFontService()
    {
        return tmpKoreanFontService
            ?? throw new InvalidOperationException(
                $"{nameof(UITabManager)} requires VContainer injection of {nameof(ITmpKoreanFontService)}.");
    }

    private IUITabGeneratedPanelFactory RequireGeneratedPanelFactory()
    {
        return generatedPanelFactory
            ?? throw new InvalidOperationException(
                $"{nameof(UITabManager)} requires VContainer injection of {nameof(IUITabGeneratedPanelFactory)}.");
    }

    private IStaffWorkPriorityPanelFactory RequireStaffWorkPriorityPanelFactory()
    {
        return staffWorkPriorityPanelFactory
            ?? throw new InvalidOperationException(
                $"{nameof(UITabManager)} requires VContainer injection of {nameof(IStaffWorkPriorityPanelFactory)}.");
    }

    private IP0FeatureSurfacePanelFactory RequireP0FeatureSurfacePanelFactory()
    {
        return p0FeatureSurfacePanelFactory
            ?? throw new InvalidOperationException(
                $"{nameof(UITabManager)} requires VContainer injection of {nameof(IP0FeatureSurfacePanelFactory)}.");
    }

    private IUITabTopButtonFactory RequireTopButtonFactory()
    {
        return topButtonFactory
            ?? throw new InvalidOperationException(
                $"{nameof(UITabManager)} requires VContainer injection of {nameof(IUITabTopButtonFactory)}.");
    }

    private DungeonUiThemeRuntime ResolveThemeRuntime()
    {
        if (themeRuntime != null)
        {
            return themeRuntime;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return null;
        }

        themeRuntime = DungeonUiThemeRuntime.Ensure(canvas, tmpKoreanFontService, uiClock);
        return themeRuntime;
    }
}
