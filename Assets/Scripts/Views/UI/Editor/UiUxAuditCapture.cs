#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

// Audit-only: actual scene UI, engine-rendered pixels, no desktop input or asset writes.
public static class UiUxAuditCapture
{
    private const string Pending = "DungeonStory.UiUxAudit.Pending";
    private const string Started = "DungeonStory.UiUxAudit.Started";
    private const string Remaining = "DungeonStory.UiUxAudit.Remaining";
    private static readonly Stack<IEnumerator> Work = new();
    private static readonly List<string> Errors = new();
    private static readonly List<string> Captures = new();
    private static int width = 1920, height = 1080;
    private static double nextTick;
    private static IObjectResolver gameplay;
    private static readonly Dictionary<string, int> SizeIndices = new();

    public static void RequestForBatch()
        => Request(false);
    public static void RequestRemainingForBatch()
        => Request(true);

    private static void Request(bool remaining)
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification,
            "UI audit requires the isolated batch launcher and isolated persistence.");
        Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "UI audit needs real graphics output.");
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "PlayMode is already running.");
        SessionState.SetBool(Pending, true);
        SessionState.SetBool(Remaining, remaining);
        SessionState.SetString(Started, EditorApplication.timeSinceStartup.ToString(CultureInfo.InvariantCulture));
        EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity", OpenSceneMode.Single);
        Resume();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        EditorApplication.update -= Pump;
        EditorApplication.update += Pump;
        Application.logMessageReceived -= ObserveLog;
        Application.logMessageReceived += ObserveLog;
    }

    private static void ObserveLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            Errors.Add(type + ": " + message + "\n" + stack);
    }

    private static void Pump()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        try
        {
            Require(EditorApplication.timeSinceStartup - double.Parse(SessionState.GetString(Started, "0"),
                CultureInfo.InvariantCulture) < 600, "UI audit exceeded its bounded capture window.");
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + 0.08;
            if (gameplay != null)
            {
                gameplay.Resolve<IGameTimeScaleController>().Scale = 0;
                Time.timeScale = 0;
            }
            if (Work.Count == 0) Work.Push(Run());
            IEnumerator current = Work.Peek();
            if (current.MoveNext())
            {
                if (current.Current is IEnumerator nested) Work.Push(nested);
                return;
            }
            (Work.Pop() as IDisposable)?.Dispose();
            if (Work.Count == 0) Finish(0);
        }
        catch (Exception error)
        {
            Errors.Add(error.ToString());
            Debug.LogException(error);
            Finish(1);
        }
    }

    private static IEnumerator Run()
    {
        yield return Wait(() => ButtonNamed("StartNewRunButton") != null, "title ready");
        SetSize(1920, 1080);
        yield return Settle();
        if (!SessionState.GetBool(Remaining, false))
        {
        Capture("01-title-empty");
        Click("StartupSettingsButton");
        yield return Settle();
        foreach (string tab in new[] { "DisplaySettingsTab", "AudioSettingsTab", "AccessibilitySettingsTab", "DeveloperSettingsTab" })
        {
            Click(tab); yield return Settle(); Capture("02-settings-" + tab);
        }
        Click("SettingsCloseButton"); yield return Settle();
        Click("StartNewRunButton"); yield return Settle(); Capture("03-difficulty");
        Click("DifficultyNextButton");
        yield return Wait(() => ButtonNamed("PreparationOwnerNextButton") != null, "owner preparation ready");
        yield return Settle(); Capture("04-owner-selection");
        Click("PreparationOwnerNextButton"); yield return Settle();
        Capture("05-party-preparation");
        string[] preparationTabs = Active<Button>().Where(b => b.name.StartsWith("PreparationTab_", StringComparison.Ordinal))
            .Select(b => b.name).Distinct().OrderBy(n => n).ToArray();
        foreach (string tab in preparationTabs)
        {
            Click(tab); yield return Settle(); Capture("06-" + tab);
        }
        }
        else
        {
            Click("StartupSettingsButton"); yield return Settle(); Click("AccessibilitySettingsTab"); yield return Settle();
            Slider textScale = Active<Slider>().Single(s => s.name == "TextScaleSlider");
            textScale.value = textScale.maxValue;
            Active<Toggle>().Single(t => t.name == "HighContrastToggle").isOn = true;
            yield return Settle(); Capture("07-settings-max-contrast");
            Find<DungeonTitleLifetimeScope>().Container.Resolve<IDungeonUserSettingsService>().ResetDefaults();
            Click("SettingsCloseButton");
        }
        // Inspect the real preparation UI first. Gameplay coverage uses the existing
        // controlled party command; it is not evidence that model generation finished.
        SceneManager.LoadScene("GameplayScene");
        yield return Wait(() => Find<DungeonRuntimeLifetimeScope>()?.Container != null, "gameplay container ready");
        yield return Settle();
        gameplay = Find<DungeonRuntimeLifetimeScope>().Container;
        string bootstrap = StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
        Write("party-bootstrap.txt", bootstrap);
        Require(bootstrap.StartsWith("committed=True;", StringComparison.Ordinal), "Controlled party command failed: " + bootstrap);
        gameplay.Resolve<IGameTimeScaleController>().Scale = 0;
        Time.timeScale = 0;
        yield return Settle();
        Capture("10-gameplay-hud");
        UITabManager tabs = Find<UITabManager>();
        Require(tabs != null, "Gameplay tabs missing.");
        foreach (UITabDefinition tab in UITabCatalog.All.Where(t => !SessionState.GetBool(Remaining, false)))
        {
            tabs.ToggleTopTab(tab.Id); yield return Settle();
            yield return CapturePages("11-tab-" + tab.Id);
            if (tab.Id == TabId.Expedition)
            {
                Click("P1Action_OffenseOpenMap"); yield return Settle();
                yield return CapturePages("13-world-map"); Find<OffenseWorldMapPanel>().Hide();
                Click("P1Action_OffenseOpenExpedition"); yield return Settle();
                if (Find<OffenseExpeditionPanel>() != null)
                { yield return CapturePages("14-expedition-party"); Find<OffenseExpeditionPanel>().Hide(); }
                else Capture("14-expedition-research-blocked");
            }
            if (tab.Id == TabId.Research)
            {
                ResearchProjectSO project = gameplay.Resolve<IGameContentCatalog>().GetAll<ResearchProjectSO>().First();
                Find<ResearchTreeWindow>().SelectProject(project); yield return Settle();
                yield return CapturePages("15-research-selected");
            }
            if (tab.Id == TabId.Codex)
            {
                foreach (string mode in new[] { "P2Action_ArchiveReports", "P2Action_ArchiveEvents", "P2Action_OutcomeLedger" })
                { Click(mode); yield return Settle(); yield return CapturePages("17-" + mode); }
            }
            tabs.ToggleTopTab(tab.Id); yield return Settle();
        }
        Click("TreasuryButton"); yield return Settle(); yield return CapturePages("16-treasury");
        ClickWithin("TreasuryFinanceWindow", "Close"); yield return Settle();
        Click("SaveMenuButton"); yield return Settle(); Capture("12-save-empty");
        ClickWithin("SaveModal", "CloseButton"); yield return Settle();
        IDungeonGameSaveSlotService slots = gameplay.Resolve<IDungeonGameSaveSlotService>();
        foreach (string slot in new[] { DungeonGameSaveSlotService.ManualSaveSlot, DungeonGameSaveSlotService.QuickSaveSlot, DungeonGameSaveSlotService.AutoSaveSlot })
            slots.Save(slot, prettyPrint: true);
        Require(slots.HasSave(DungeonGameSaveSlotService.ManualSaveSlot), "Isolated save missing.");
        Click("SaveMenuButton"); yield return Settle(); Capture("12-save-populated");
        ClickWithin("SaveModal", "CloseButton"); yield return Settle();
        CharacterActor actor = UnityEngine.Object.FindObjectsByType<CharacterActor>(FindObjectsSortMode.None)
            .FirstOrDefault(a => a.Identity?.Profile != null && a.gameObject.activeInHierarchy);
        Require(actor != null, "Prepared character missing.");
        gameplay.Resolve<IGameEventBus>().ShowInfo(actor); yield return Settle();
        CharacterSummaryInfo summary = Find<CharacterSummaryInfo>();
        Require(summary != null, "Character summary missing.");
        var characterTabs = new (string, Action)[]
        {
            ("status", summary.ShowStatusTab), ("mood", summary.ShowMoodTab),
            ("health", summary.ShowHealthTab), ("growth", summary.ShowGrowthTab),
            ("records", summary.ShowRecordsTab), ("ai", summary.ShowAiTab),
            ("combat", summary.ShowCombatTab), ("population", summary.ShowPopulationTab)
        };
        foreach (var tab in characterTabs.Where(_ => !SessionState.GetBool(Remaining, false)))
        {
            tab.Item2(); yield return Settle(); yield return CapturePages("20-character-" + tab.Item1);
        }
        summary.OpenDetailedStats(); yield return Settle();
        foreach (CharacterDetailedStatsTab tab in Enum.GetValues(typeof(CharacterDetailedStatsTab)))
        {
            summary.ShowDetailedStatsTab(tab); yield return Settle();
            yield return CapturePages("21-stats-" + tab);
        }
        Click("MentorshipManageButton"); yield return Settle(); Capture("21-mentorship");
        ClickWithin("MentorshipPanel", "Close"); yield return Settle();
        summary.CloseDetailedStats(); summary.ShowHealthTab(); summary.OpenSurgeryWindow();
        yield return Settle(); yield return CapturePages("22-surgery");
        CharacterSurgeryWindowView surgery = Find<CharacterSurgeryWindowView>();
        if (surgery != null) surgery.gameObject.SetActive(false);
        summary.RequestClose(); yield return Settle();
        tabs.ToggleTopTab(TabId.Staff); yield return Settle();
        Click("P1Action_StaffModeManagement"); yield return Settle(); yield return CapturePages("24-staff-management");
        tabs.ToggleTopTab(TabId.Staff); yield return Settle();
        using (var memory = new MemoryErasureSealUseModal(gameplay.Resolve<IDungeonUiCanvasProvider>().GetOrCreateCanvas().transform,
                   gameplay.Resolve<ITmpKoreanFontService>(), gameplay.Resolve<IMemoryErasureSealCommandService>(), gameplay.Resolve<ICharacterWorldQuery>()))
        { memory.Open(); yield return Settle(); yield return CapturePages("25-memory-erasure"); }
        yield return Settle();
        UIBuildingInfo buildingInfo = Find<UIBuildingInfo>(true);
        var buildings = gameplay.Resolve<IBuildingWorldQuery>().Buildings.OfType<BuildableObject>().ToArray();
        Write("placed-buildings.txt", string.Join("\n", buildings.Select(b => b.BuildingData.name)));
        foreach (BuildableObject building in buildings.GroupBy(b => b.BuildingData).Select(g => g.First()))
        {
            buildingInfo.DisplayBuildingInfo(building); yield return Settle();
            yield return CapturePages("30-building-" + Safe(building.BuildingData.name));
            buildingInfo.CloseDispaly(); yield return Settle();
        }
        yield return CaptureAdditionalSurfaces(tabs, buildingInfo, actor);
        foreach (var resolution in new[] { new Vector2Int(1280, 720), new Vector2Int(2560, 1440) })
        {
            SetSize(resolution.x, resolution.y); yield return Settle();
            Capture("40-hud-" + width);
            foreach (TabId tab in new[] { TabId.Staff, TabId.Operations, TabId.Research })
            {
                tabs.ToggleTopTab(tab); yield return Settle(); Capture("41-" + tab + "-" + width);
                tabs.ToggleTopTab(tab); yield return Settle();
            }
        }
        SetSize(1920, 1080);
        yield return Settle();
        EventAlertRuntime alerts = Find<EventAlertRuntime>();
        var choices = Enumerable.Range(1, 4).Select(i => new EventAlertChoice("선택지 " + i, "결정의 비용과 결과 확인", () => { })).ToArray();
        alerts.OnTriggerEvent(new EventAlertRequestedEvent(new EventAlertRequest(
            "UI 감사 통제 사건 — 선택지 네 개", "장비가 고장 나서 생산이 중단되었습니다. 수리, 교체, 인력 재배치, 보류 중에서 선택합니다.",
            EventAlertImportance.High, "감사 fixture", choices, "ui-audit-four")));
        alerts.Open(alerts.EventLog.Last()); yield return Settle(); Capture("50-event-four-choices");
        alerts.CloseDetail();
        alerts.OnTriggerEvent(new EventAlertRequestedEvent(new EventAlertRequest(
            "UI 감사 통제 사건 — 긴 본문", string.Join("\n", Enumerable.Range(1, 20).Select(i => i + ". 시설 상태, 재료 부족, 직원 숙련과 결정의 영향을 확인하는 긴 설명입니다.")),
            EventAlertImportance.High, "감사 fixture", choices, "ui-audit-long")));
        alerts.Open(alerts.EventLog.Last()); yield return Settle(); Capture("51-event-long-detail");
        alerts.CloseDetail();
        RunResultPanel resultPanel = gameplay.Resolve<IRunResultPanelFactory>().CreateDefaultPanel();
        resultPanel.Render(new RunResultSnapshot(ownerName: "UI 감사 통제 결과", endReason: "던전 운영 종료", survivalSeconds: 86400,
            survivedOperatingDays: 14, settlementCount: 12, defendedInvasionCount: 3, legacyCurrency: 120));
        yield return Settle(); yield return CapturePages("52-run-result"); resultPanel.Hide();
        OffenseSceneRuntimeReferences offense = gameplay.Resolve<OffenseSceneRuntimeReferences>();
        OffenseExpeditionRun expedition = new("ui-audit-controlled-battle", offense.WorldMap.TargetDefinitions.First(t => t.id == "food_farm"), new[] { actor }, 20f);
        IOffenseBattleRuntime battle = gameplay.Resolve<IOffenseBattleRuntime>();
        Require(battle.TryStartBattle(expedition, out string battleMessage), battleMessage);
        yield return Settle(); yield return CapturePages("53-battle-controlled");
    }

    private static IEnumerator CaptureAdditionalSurfaces(UITabManager tabs, UIBuildingInfo info, CharacterActor actor)
    {
        tabs.ToggleTopTab(TabId.Construction); yield return Settle();
        GridConstructTab construction = Find<GridConstructTab>();
        foreach (int category in construction.selectButtonPanelList.Select(p => p.id).Distinct().ToArray())
        {
            construction.ToggleSelectButton(category); yield return Settle();
            Capture("31-construction-category-" + category);
        }
        tabs.ToggleTopTab(TabId.Construction); yield return Settle();
        Require(gameplay.Resolve<IGridSystemProvider>().TryGetGrid(out Grid grid), "Grid missing.");
        RoomInstance room = gameplay.Resolve<IRoomLayoutCache>().GetLayout(grid).Rooms
            .Where(r => r != null && !r.IsSelfContained).OrderByDescending(r => r.IsUsable).ThenByDescending(r => r.Cells.Count).First();
        IRoomInspectionService inspection = gameplay.Resolve<IRoomInspectionService>();
        Require(inspection.ShowRoom(grid, room), "Room inspection unavailable.");
        yield return Settle(); Capture("32-room-inspection"); inspection.SetEnabled(false);
        IWildlifeRuntime wildlife = gameplay.Resolve<IWildlifeRuntime>();
        WildlifeActor animal = wildlife.Wildlife.FirstOrDefault(a => a != null && a.IsAlive);
        if (animal == null)
        {
            Vector2Int position = grid.GetCells().First(c => WildlifeWorldRuntime.CanInitialSpawnAt(grid, c.Position)).Position;
            Require(wildlife.TrySpawnArrival("shadow_hare", position, out animal, out string reason), reason);
        }
        gameplay.Resolve<IGameEventBus>().ShowInfo(animal); yield return Settle(); Capture("33-wildlife");
        Find<WildlifeInfoPanel>().OnClose(); yield return Settle();
        Click("WildlifeEcosystemViewToggle"); yield return Settle(); Capture("33-wildlife-overlay");
        Click("WildlifeEcosystemViewToggle");
        WorldItemStackSnapshot stack = gameplay.Resolve<IWorldItemStackRuntime>().GetAllStacks()
            .First(s => s != null && s.Quantity > 0 && s.State == WorldItemStackState.Loose);
        gameplay.Resolve<IGameEventBus>().ShowInfo(new ItemPileInfoTarget(stack.Position));
        yield return Settle(); Capture("33-item-pile");
        Click("StackRow_" + stack.StackId); yield return Settle(); Capture("33-item-detail");
        Find<ItemPileInfoPanel>(true).OnClose(); yield return Settle();
        foreach (string name in new[] { "P01_제분소", "P23_야외경작지", "CB01_야수우리", "Door", "D04_배식카운터", "E10_냉각기", "V22_9301_재단_재봉_작업대", "P1_TreasuryBoltThrower" })
        {
            BuildingSO definition = gameplay.Resolve<IGameContentCatalog>().GetAll<BuildingSO>().Single(b => b.name == name);
            Vector2Int? position = null;
            for (int y = 0; y < grid.height && !position.HasValue; y++)
                for (int x = 0; x < grid.width && !position.HasValue; x++)
                {
                    Vector2Int candidate = new(x, y);
                    if (definition.GetGridPosList(candidate).All(p => grid.GetGridCell(p) is { } cell
                        && cell.AreaType != GridCellAreaType.BlockedExterior && cell.CanOccupy(definition.layer))) position = candidate;
                }
            Require(position.HasValue, "No fixture placement for " + name);
            BuildableObject building = gameplay.Resolve<IGridBuildingObjectFactory>().Create(grid, definition, position.Value);
            foreach (MonoBehaviour component in building.GetComponentsInChildren<MonoBehaviour>(true))
                if (component != null) gameplay.Inject(component);
            building.SetGrid(grid); building.Initialization(definition, position.Value);
            Require(grid.RegisterOccupant(building, definition.layer, definition.GetGridPosList(position.Value), definition.Placement.IsMovement), "Fixture placement rejected: " + name);
            info.DisplayBuildingInfo(building); yield return Settle();
            yield return CapturePages("34-facility-" + Safe(name)); info.CloseDispaly(); yield return Settle();
        }
        OffenseSceneRuntimeReferences offense = gameplay.Resolve<OffenseSceneRuntimeReferences>();
        OffenseExpeditionPanel panel = gameplay.Resolve<IOffensePanelService>().ShowExpedition(offense.Expedition);
        yield return Settle(); yield return CapturePages("35-expedition-view-only"); panel.Hide();
        // This public presentation call deliberately does not prove the research unlock.
    }

    private static IEnumerator CapturePages(string id)
    {
        yield return Settle(); Capture(id + "-top");
        ScrollRect[] scrolls = Active<ScrollRect>().Where(s => s.vertical && s.content != null && s.viewport != null
            && s.content.rect.height > s.viewport.rect.height + 10).ToArray();
        int index = 0;
        foreach (ScrollRect scroll in scrolls)
        {
            int pages = Mathf.CeilToInt(scroll.content.rect.height / Mathf.Max(1, scroll.viewport.rect.height));
            Require(pages < 60, "Unexpectedly large scroll capture: " + scroll.name + ", pages=" + pages);
            float previous = scroll.verticalNormalizedPosition;
            // Audit distinct layouts, retaining representative middle/end states.
            // This does not claim every data row or every combination was viewed.
            foreach (int page in new[] { pages / 2, pages - 1 }.Where(p => p > 0).Distinct())
            {
                if (scroll == null) break;
                scroll.verticalNormalizedPosition = 1f - (float)page / (pages - 1);
                yield return Settle(); Capture(id + "-s" + index + "-p" + page);
            }
            if (scroll != null) scroll.verticalNormalizedPosition = previous;
            index++;
        }
    }

    private static IEnumerator Wait(Func<bool> ready, string boundary)
    {
        double until = EditorApplication.timeSinceStartup + 45;
        while (!ready())
        {
            Require(EditorApplication.timeSinceStartup < until, "Timed out: " + boundary);
            yield return null;
        }
    }
    private static IEnumerator Settle()
    {
        for (int i = 0; i < 2; i++) { Canvas.ForceUpdateCanvases(); yield return null; }
    }
    private static T Find<T>(bool inactive = false) where T : UnityEngine.Object =>
        UnityEngine.Object.FindFirstObjectByType<T>(inactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
    private static T[] Active<T>() where T : Component => Resources.FindObjectsOfTypeAll<T>()
        .Where(c => c != null && c.gameObject.scene.IsValid() && c.gameObject.activeInHierarchy).ToArray();
    private static Button ButtonNamed(string name) => Active<Button>().FirstOrDefault(b => b.name == name);
    private static void Click(string name)
    {
        Button button = ButtonNamed(name);
        Require(button != null && button.interactable, "Visible enabled button missing: " + name);
        button.onClick.Invoke();
    }
    private static void ClickWithin(string parent, string name)
    {
        Button button = Active<Button>().FirstOrDefault(b => b.name == name && Hierarchy(b.transform).Contains("/" + parent + "/"));
        Require(button != null && button.interactable, "Visible button missing: " + parent + "/" + name);
        button.onClick.Invoke();
    }
    private static string Hierarchy(Transform item) => item.parent == null ? "/" + item.name : Hierarchy(item.parent) + "/" + item.name;
    private static string Safe(string value) => string.Concat(value.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_'));

    private static void SetSize(int w, int h)
    {
        width = w; height = h;
        Type view = typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeView", true);
        Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView", true);
        // This Editor exists only on the launcher's noninteractive desktop.
        // Request no focus; never use Focus(), ShowUtility(), or desktop input.
        EditorWindow gameView = EditorWindow.GetWindow(gameViewType, false, "UI audit", false);
        string key = w + "x" + h;
        if (!SizeIndices.TryGetValue(key, out int sizeIndex))
        {
            Type sizesType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes", true);
            object sizes = sizesType.BaseType.GetProperty("instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            object group = sizesType.GetProperty("currentGroup").GetValue(sizes);
            Type sizeType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSize", true);
            Type kind = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizeType", true);
            object size = Activator.CreateInstance(sizeType, Enum.Parse(kind, "FixedResolution"), w, h, "UI audit " + key);
            sizeIndex = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            SizeIndices.Add(key, sizeIndex);
        }
        // A targetSize assignment alone is overwritten by GameView's free-aspect layout.
        // Select an in-memory fixed size; never save the global GameViewSizes preference.
        gameViewType.GetMethod("SizeSelectionCallback").Invoke(gameView, new object[] { sizeIndex, null });
        MethodInfo set = typeof(EditorWindow).GetMethod("SetMainPlayModeViewSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Require(set != null, "Unity PlayModeView size API missing.");
        view.GetProperty("targetSize", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(gameView, new Vector2(w, h));
        set.Invoke(gameView, new object[] { new Vector2(w, h) });
        typeof(EditorWindow).GetMethod("SetPlayModeViewSize", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(gameView, new object[] { new Vector2(w, h) });
    }

    private static void Capture(string id)
    {
        Require(!Captures.Contains(id), "Duplicate capture id: " + id);
        Require(Screen.width == width && Screen.height == height,
            "Capture viewport mismatch: requested=" + width + "x" + height + "; actual=" + Screen.width + "x" + Screen.height);
        Canvas.ForceUpdateCanvases();
        MethodInfo render = typeof(EditorGUIUtility).GetMethod("RenderPlayModeViewCamerasInternal", BindingFlags.Static | BindingFlags.NonPublic);
        Require(render != null, "Unity actual GameView rendering API missing.");
        RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        Texture2D pixels = null;
        try
        {
            target.Create();
            render.Invoke(null, new object[] { target, 0, new Vector2(-1000, -1000), false, true });
            RenderTexture.active = target;
            pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            // Match GameView's graphicsUVStartsAtTop orientation handling on D3D.
            if (SystemInfo.graphicsUVStartsAtTop)
            {
                Color32[] source = pixels.GetPixels32();
                Color32[] upright = new Color32[source.Length];
                for (int y = 0; y < height; y++)
                    Array.Copy(source, y * width, upright, (height - 1 - y) * width, width);
                pixels.SetPixels32(upright);
            }
            pixels.Apply();
            byte[] png = pixels.EncodeToPNG();
            Require(png != null && png.Length > 10000, "Blank/invalid capture: " + id);
            WriteBytes(id + ".png", png);
            var snapshot = new CaptureMetadata
            {
                id = id, scene = SceneManager.GetActiveScene().name, width = width, height = height,
                screenWidth = Screen.width, screenHeight = Screen.height,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                texts = Active<TMP_Text>().Select(t => new TextSnapshot
                {
                    path = Hierarchy(t.transform), text = t.text, size = t.fontSize,
                    overflow = t.isTextOverflowing, width = t.rectTransform.rect.width, height = t.rectTransform.rect.height
                }).ToArray(),
                buttons = Active<Button>().Select(b => new ButtonSnapshot
                { path = Hierarchy(b.transform), label = b.GetComponentInChildren<TMP_Text>()?.text,
                    interactable = b.interactable }).ToArray()
            };
            Write(id + ".json", JsonUtility.ToJson(snapshot, true));
            Captures.Add(id); Debug.Log("UI_AUDIT_CAPTURE=" + id + "; bytes=" + png.Length);
        }
        finally
        {
            RenderTexture.active = previous;
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            RenderTexture.ReleaseTemporary(target);
        }
        if (Captures.Count % 12 == 0) GC.Collect();
    }

    private static string Output()
    {
        string root = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        Require(!string.IsNullOrWhiteSpace(root), "Missing isolated output root.");
        Directory.CreateDirectory(root); return root;
    }
    private static void Write(string name, string value) => WriteBytes(name, System.Text.Encoding.UTF8.GetBytes(value));
    private static void WriteBytes(string name, byte[] value)
    {
        using var file = new FileStream(Path.Combine(Output(), name), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        file.Write(value, 0, value.Length);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Finish(int code)
    {
        SessionState.SetBool(Pending, false); EditorApplication.update -= Pump;
        Application.logMessageReceived -= ObserveLog;
        try
        {
            Write("capture-manifest.json", JsonUtility.ToJson(new Manifest
                { completed = code == 0, captures = Captures.ToArray(), errors = Errors.ToArray() }, true));
            Debug.Log("UI_AUDIT_FINISHED=" + code + "; captures=" + Captures.Count + "; consoleErrors=" + Errors.Count);
        }
        finally { EditorApplication.Exit(code); }
    }
    [Serializable] private sealed class Manifest { public bool completed; public string[] captures, errors; }
    [Serializable] private sealed class CaptureMetadata
    { public string id, scene, graphicsDevice; public int width, height, screenWidth, screenHeight; public TextSnapshot[] texts; public ButtonSnapshot[] buttons; }
    [Serializable] private sealed class TextSnapshot { public string path, text; public float size, width, height; public bool overflow; }
    [Serializable] private sealed class ButtonSnapshot { public string path, label; public bool interactable; }
}
#endif
