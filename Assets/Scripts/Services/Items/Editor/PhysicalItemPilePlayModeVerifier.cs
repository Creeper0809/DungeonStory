#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

[InitializeOnLoad]
public static class PhysicalItemPilePlayModeVerifier
{
    public const string RequestPath = "Temp/physical-item-pile-playmode.request";
    public const string ReportPath = "Artifacts/QA/physical-item-pile-playmode-report.txt";
    public const string ListCapturePath = "Artifacts/QA/physical-item-pile-list.png";
    public const string DetailCapturePath = "Artifacts/QA/physical-item-pile-detail.png";
    public const string PriorityCapturePath = "Artifacts/QA/physical-item-character-priority.png";
    public const string AltCapturePath = "Artifacts/QA/physical-item-alt-select.png";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private static bool runnerCreated;

    static PhysicalItemPilePlayModeVerifier()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/Debug/QA/Request Physical Item Pile PlayMode Verification")]
    public static void RequestRunFromMenu()
    {
        PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(ReportPath);
        File.WriteAllText(RequestPath, DateTime.UtcNow.ToString("O"));
    }

    [MenuItem("DungeonStory/Debug/QA/Run Physical Item Pile PlayMode Verification")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("Physical item pile verification requires PlayMode in the gameplay scene.");
            return;
        }

        if (UnityEngine.Object.FindFirstObjectByType<PhysicalItemPilePlayModeVerificationRunner>() != null)
        {
            Debug.LogWarning("Physical item pile verification is already running.");
            return;
        }

        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(ReportPath);
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        new GameObject("Physical Item Pile PlayMode Verification Runner")
            .AddComponent<PhysicalItemPilePlayModeVerificationRunner>();
    }

    private static void OnEditorUpdate()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!string.Equals(
                SceneManager.GetActiveScene().path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        }

        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            runnerCreated = false;
            PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
            return;
        }

        if (change != PlayModeStateChange.EnteredPlayMode || runnerCreated || !File.Exists(RequestPath))
        {
            return;
        }

        runnerCreated = true;
        new GameObject("Physical Item Pile PlayMode Verification Runner")
            .AddComponent<PhysicalItemPilePlayModeVerificationRunner>();
    }
}

public sealed class PhysicalItemPilePlayModeVerificationRunner : MonoBehaviour
{
    private const string PreservedRationItemId = "food:preserved-ration";
    private const string BoltItemId = "ammo:bolt";
    private const string ManaCrystalItemId = "resource:mana-crystal";
    private const string LumberItemId = "material:lumber";

    private readonly List<string> report = new List<string>();
    private readonly List<string> failures = new List<string>();
    private readonly List<string> capturedErrors = new List<string>();
    private readonly List<string> capturedWarnings = new List<string>();
    private readonly List<string> createdStackIds = new List<string>();

    private InputSettings.EditorInputBehaviorInPlayMode originalEditorInputBehavior;
    private bool inputBehaviorCaptured;
    private Mouse originalMouse;
    private Mouse verificationMouse;
    private DungeonAutomationInputTestCapability automationInput;
    private int verificationMouseSerial;
    private Vector2 originalMousePosition;
    private float originalTimeScale;

    private CharacterActor movedActor;
    private Vector3 originalActorPosition;
    private bool actorStateCaptured;
    private Vector2Int pilePosition;
    private Vector2 pileScreenPoint;
    private IWorldItemStackRuntime itemRuntime;

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Artifacts/QA");
        Application.logMessageReceived += OnLogMessageReceived;
        EnsureEventSystem();
        SetupInput();
        originalTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        automationInput = new DungeonAutomationInputTestCapability();
        automationInput.Enable();

        yield return null;
        yield return null;

        DungeonRuntimeLifetimeScope scope = FindScope();
        itemRuntime = Resolve<IWorldItemStackRuntime>(scope);
        GridSystemManager gridSystem = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>();
        Grid grid = gridSystem != null ? gridSystem.grid : null;
        Camera camera = Camera.main;
        ItemPileInfoPanel itemPanel = UnityEngine.Object.FindFirstObjectByType<ItemPileInfoPanel>(FindObjectsInactive.Include);
        CharacterSummaryInfo characterSummary =
            UnityEngine.Object.FindFirstObjectByType<CharacterSummaryInfo>(FindObjectsInactive.Include);

        Check(scope != null && scope.Container != null, "SCOPE_READY", "gameplay LifetimeScope resolved");
        Check(itemRuntime != null, "ITEM_RUNTIME_READY", "IWorldItemStackRuntime resolved");
        Check(grid != null, "GRID_READY", "GridSystemManager grid resolved");
        Check(camera != null, "CAMERA_READY", "main camera resolved");
        Check(itemPanel != null, "ITEM_PANEL_READY", "ItemPileInfoPanel resolved");
        Check(characterSummary != null, "CHARACTER_PANEL_READY", "CharacterSummaryInfo resolved");

        if (scope == null || itemRuntime == null || grid == null || camera == null || itemPanel == null)
        {
            Finish();
            yield break;
        }

        yield return EnsurePlayableRun();
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(0.2f);

        CloseKnownPopups();
        yield return new WaitForSecondsRealtime(0.1f);

        if (!TryFindVisibleTestCell(grid, camera, out pilePosition, out pileScreenPoint))
        {
            Check(false, "PILE_TEST_CELL", "no visible non-UI grid cell found for item pile");
            Finish();
            yield break;
        }

        SpawnPile(itemRuntime, pilePosition);
        yield return null;
        yield return null;

        Check(itemRuntime.TryGetPileAt(pilePosition, out WorldItemPileSnapshot pile) && pile.Stacks.Count >= 3,
            "PILE_CREATED",
            pile != null
                ? $"position={pilePosition}; stacks={pile.Stacks.Count}; qty={pile.TotalQuantity}; kinds={pile.KindCount}"
                : $"position={pilePosition}; pile missing");
        Check(FindItemMarker(pilePosition) != null,
            "PILE_MARKER_CREATED",
            $"marker={FindItemMarker(pilePosition)?.name ?? "<none>"}");

        yield return VerifyStoredItemToggle(itemRuntime, grid, camera);

        yield return HoverWorldPoint(pileScreenPoint);
        TMP_Text tooltip = FindActiveText("TooltipText");
        Check(tooltip != null && tooltip.gameObject.activeInHierarchy && tooltip.text.Contains("외"),
            "PILE_HOVER_TOOLTIP",
            tooltip != null ? Compact(tooltip.text) : "tooltip missing");

        yield return PressMouse(pileScreenPoint);
        yield return ReleaseMouse(pileScreenPoint);
        yield return new WaitForSecondsRealtime(0.15f);

        Check(IsItemPanelOpen(), "PILE_CLICK_OPENS_LIST", DescribeItemPanelState());
        Check(FindStackRowButtons().Length >= 3,
            "PILE_LIST_ROWS",
            $"rows={FindStackRowButtons().Length}; text={GetVisibleTextSample()}");
        Check(VisibleTextsContain("아이템 더미") && VisibleTextsContain("종"),
            "PILE_LIST_TEXT",
            GetVisibleTextSample());
        yield return CaptureScreen(PhysicalItemPilePlayModeVerifier.ListCapturePath);

        Button detailRow = FindStackRowButtons().FirstOrDefault();
        string clickedRowName = detailRow != null ? detailRow.name : string.Empty;
        yield return ClickUiButton(detailRow, "first stack row");
        yield return new WaitForSecondsRealtime(0.1f);

        Check(FindVisibleButtonByName("Back") != null,
            "PILE_DETAIL_BACK_BUTTON",
            $"clicked={clickedRowName}; {DescribeItemPanelState()}");
        Check(FindActiveText("DetailText") != null
                && VisibleTextsContain("단가")
                && VisibleTextsContain("운반"),
            "PILE_DETAIL_TEXT",
            GetVisibleTextSample());
        yield return CaptureScreen(PhysicalItemPilePlayModeVerifier.DetailCapturePath);

        yield return ClickUiButton(FindVisibleButtonByName("Back"), "detail back");
        yield return new WaitForSecondsRealtime(0.1f);
        Check(FindStackRowButtons().Length >= 3,
            "PILE_BACK_RETURNS_TO_LIST",
            $"rows={FindStackRowButtons().Length}");

        yield return VerifyCharacterPriority(camera, characterSummary);
        yield return VerifyAltItemSelection();
        yield return VerifyMemoryErasureSealStoredUse(
            scope,
            grid,
            itemPanel);

        Finish();
    }

    private void SetupInput()
    {
        originalEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        inputBehaviorCaptured = true;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        originalMouse = Mouse.current;
        if (originalMouse != null)
        {
            originalMousePosition = originalMouse.position.ReadValue();
            InputSystem.DisableDevice(originalMouse);
        }

        CreateVerificationMouse();
    }

    private void SpawnPile(IWorldItemStackRuntime itemRuntime, Vector2Int position)
    {
        SpawnItem(itemRuntime, PreservedRationItemId, 12, position);
        SpawnItem(itemRuntime, BoltItemId, 4, position);
        SpawnItem(itemRuntime, ManaCrystalItemId, 2, position);
    }

    private void SpawnItem(IWorldItemStackRuntime itemRuntime, string itemId, int amount, Vector2Int position)
    {
        if (itemRuntime.SpawnItemAt(itemId, amount, position, WorldItemStackState.Loose, string.Empty, out int spawned))
        {
            WorldItemStackSnapshot latest = itemRuntime.GetStacksAt(position)
                .LastOrDefault(stack => string.Equals(stack.ItemId, itemId, StringComparison.Ordinal));
            if (latest != null)
            {
                createdStackIds.Add(latest.StackId);
            }

            report.Add($"SPAWN_ITEM; item={itemId}; requested={amount}; spawned={spawned}; position={position}");
        }
        else
        {
            Check(false, "SPAWN_ITEM", $"item={itemId}; amount={amount}; position={position}");
        }
    }

    private IEnumerator VerifyStoredItemToggle(IWorldItemStackRuntime itemRuntime, Grid grid, Camera camera)
    {
        if (!TryFindVisibleTestCell(grid, camera, out Vector2Int storedPosition, out _))
        {
            Check(false, "STORED_ITEM_TEST_CELL", "no visible free cell for stored-stack toggle");
            yield break;
        }

        bool spawned = itemRuntime.SpawnItemAt(
            LumberItemId,
            7,
            storedPosition,
            WorldItemStackState.Stored,
            "warehouse:qa-toggle",
            out int spawnedAmount);
        Check(spawned && spawnedAmount == 7,
            "STORED_ITEM_SPAWNED",
            $"position={storedPosition}; spawned={spawnedAmount}");
        if (!spawned)
        {
            yield break;
        }

        WorldItemStackSnapshot stored = itemRuntime.GetStacksAt(storedPosition, includeStored: true)
            .FirstOrDefault(stack => stack.State == WorldItemStackState.Stored);
        if (stored != null)
        {
            createdStackIds.Add(stored.StackId);
        }

        Check(!itemRuntime.TryGetPileAt(storedPosition, out _)
                && FindItemMarker(storedPosition) == null,
            "STORED_ITEM_HIDDEN_BY_DEFAULT",
            $"position={storedPosition}; marker={FindItemMarker(storedPosition)?.name ?? "<none>"}");

        Button toggle = FindVisibleButtonByName("ItemStackViewToggle");
        yield return ClickUiButton(toggle, "item view toggle");
        yield return new WaitForSecondsRealtime(0.1f);

        Check(itemRuntime.StoredItemMarkersVisible
                && itemRuntime.TryGetPileAt(storedPosition, out WorldItemPileSnapshot pile)
                && pile.Stacks.Any(stack => stack.State == WorldItemStackState.Stored),
            "STORED_ITEM_VISIBLE_WITH_TOGGLE",
            $"toggle={itemRuntime.StoredItemMarkersVisible}; stacks={itemRuntime.GetStacksAt(storedPosition, includeStored: true).Count}");
        Check(FindItemMarker(storedPosition) != null,
            "STORED_ITEM_MARKER_CREATED",
            $"marker={FindItemMarker(storedPosition)?.name ?? "<none>"}");

        yield return ClickUiButton(toggle, "item view toggle off");
        yield return new WaitForSecondsRealtime(0.1f);
        Check(!itemRuntime.StoredItemMarkersVisible
                && !itemRuntime.TryGetPileAt(storedPosition, out _),
            "STORED_ITEM_HIDES_AGAIN",
            $"toggle={itemRuntime.StoredItemMarkersVisible}");
    }

    private IEnumerator VerifyCharacterPriority(Camera camera, CharacterSummaryInfo characterSummary)
    {
        CloseKnownPopups();
        yield return null;
        yield return null;

        CharacterActor actor = FindClickableActor();
        Check(actor != null, "CHARACTER_FOR_PRIORITY", actor != null ? actor.name : "no clickable actor");
        if (actor == null || camera == null || characterSummary == null)
        {
            yield break;
        }

        Collider2D actorCollider = actor.GetComponentsInChildren<Collider2D>(true)
            .FirstOrDefault(collider => collider != null && collider.enabled);
        Check(actorCollider != null, "CHARACTER_COLLIDER", actorCollider != null ? actorCollider.name : "missing");
        if (actorCollider == null)
        {
            yield break;
        }

        movedActor = actor;
        originalActorPosition = actor.transform.position;
        actorStateCaptured = true;
        Vector3 targetWorld = camera.ScreenToWorldPoint(new Vector3(
            pileScreenPoint.x,
            pileScreenPoint.y,
            -camera.transform.position.z));
        actor.transform.position += targetWorld - actorCollider.bounds.center;
        Physics2D.SyncTransforms();
        yield return null;

        yield return PressMouse(pileScreenPoint);
        yield return ReleaseMouse(pileScreenPoint);
        yield return new WaitForSecondsRealtime(0.15f);

        Check(IsCharacterPanelOpen(characterSummary) && !IsItemPanelOpen(),
            "CHARACTER_BEATS_ITEM",
            $"characterPanel={IsCharacterPanelOpen(characterSummary)}; itemPanel={IsItemPanelOpen()}");
        yield return CaptureScreen(PhysicalItemPilePlayModeVerifier.PriorityCapturePath);
    }

    private IEnumerator VerifyAltItemSelection()
    {
        CloseKnownPopups();
        yield return null;
        yield return null;

        ApplyMouseState(new MouseState { position = pileScreenPoint });
        automationInput.MovePointer(pileScreenPoint);
        automationInput.HoldKey(KeyCode.LeftAlt, 0.5f);
        automationInput.ClickPointer(0);
        yield return null;
        yield return null;
        yield return new WaitForSecondsRealtime(0.15f);

        Check(IsItemPanelOpen(),
            "ALT_CLICK_FORCES_ITEM",
            $"itemPanel={IsItemPanelOpen()}; rows={FindStackRowButtons().Length}");
        yield return CaptureScreen(PhysicalItemPilePlayModeVerifier.AltCapturePath);
        automationInput.ReleaseKey(KeyCode.LeftAlt);
    }

    private IEnumerator VerifyMemoryErasureSealStoredUse(
        DungeonRuntimeLifetimeScope scope,
        Grid grid,
        ItemPileInfoPanel itemPanel)
    {
        CloseKnownPopups();
        yield return null;

        IMemoryErasureSealCommandService commands =
            Resolve<IMemoryErasureSealCommandService>(scope);
        IGameContentDefinitionSource content =
            Resolve<IGameContentDefinitionSource>(scope);
        ICharacterWorldQuery characters = Resolve<ICharacterWorldQuery>(scope);
        IWarehouseWorldQuery warehouseWorld =
            Resolve<IWarehouseWorldQuery>(scope);
        Check(commands != null,
            "MEMORY_SEAL_COMMAND_READY",
            commands != null ? "command service resolved" : "missing");
        Check(content != null,
            "MEMORY_SEAL_CONTENT_READY",
            content != null ? "content source resolved" : "missing");
        Check(characters != null,
            "MEMORY_SEAL_CHARACTER_WORLD_READY",
            characters != null ? "character world resolved" : "missing");
        Check(warehouseWorld != null,
            "MEMORY_SEAL_WAREHOUSE_WORLD_READY",
            warehouseWorld != null ? "warehouse world resolved" : "missing");
        if (commands == null
            || content == null
            || characters == null
            || warehouseWorld == null)
        {
            yield break;
        }

        CharacterActor actor = characters.Characters
            .Where(IsMemorySealActorEligible)
            .Where(value => !value.Progression
                .CaptureAcquiredTraitState().HasPersistentData)
            .SelectMany(value => warehouseWorld.Warehouses
                .OfType<Facility>()
                .Where(warehouse => warehouse != null
                    && !warehouse.isDestroy
                    && !warehouse.IsGridDestroyed
                    && warehouse.HasWarehouseInventory
                    && warehouse.Inventory != null)
                .Select(warehouse => new
                {
                    Actor = value,
                    Warehouse = warehouse,
                    Distance = Manhattan(
                        value.GetNowXY(),
                        warehouse.centerPos)
                }))
            .Where(candidate => candidate.Distance >= 3)
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate =>
                candidate.Actor.Identity?.PersistentId,
                StringComparer.Ordinal)
            .Select(candidate => candidate.Actor)
            .FirstOrDefault();
        Facility warehouseBuilding = actor == null
            ? null
            : warehouseWorld.Warehouses
                .OfType<Facility>()
                .Where(warehouse => warehouse != null
                    && !warehouse.isDestroy
                    && !warehouse.IsGridDestroyed
                    && warehouse.HasWarehouseInventory
                    && warehouse.Inventory != null
                    && Manhattan(actor.GetNowXY(), warehouse.centerPos) >= 3)
                .OrderBy(warehouse =>
                    Manhattan(actor.GetNowXY(), warehouse.centerPos))
                .ThenBy(warehouse =>
                    warehouse.PersistentInstanceId.Value,
                    StringComparer.Ordinal)
                .FirstOrDefault();
        Check(actor != null && warehouseBuilding != null,
            "MEMORY_SEAL_ACTOR_WAREHOUSE_PAIR",
            actor != null && warehouseBuilding != null
                ? $"actor={actor.Identity?.PersistentId}; warehouse="
                    + warehouseBuilding.PersistentInstanceId.Value
                : "no empty-progression active actor and live warehouse pair at distance >=3");
        if (actor == null || warehouseBuilding == null)
        {
            yield break;
        }

        WorldItemStackSnapshot[] preexistingSeals = itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.Stored
                && string.Equals(
                    stack.ItemId,
                    MemoryErasureSealItemRules.ItemId,
                    StringComparison.Ordinal))
            .ToArray();
        Check(preexistingSeals.Length == 0,
            "MEMORY_SEAL_NO_PREEXISTING_STORED_SOURCE",
            $"storedSealCount={preexistingSeals.Length}");
        if (preexistingSeals.Length != 0)
        {
            yield break;
        }

        CharacterAcquiredTraitSettingsSO settings =
            content.RequireSingle<CharacterAcquiredTraitSettingsSO>();
        CharacterAcquiredTraitModuleSO[] modules = content
            .GetAll<CharacterAcquiredTraitModuleSO>()
            .Where(value => value != null)
            .OrderBy(value => value.ModuleId, StringComparer.Ordinal)
            .ToArray();
        CharacterAcquiredTraitModuleSO module = modules.FirstOrDefault();
        Check(settings != null && module != null,
            "MEMORY_SEAL_AUTHORED_TRAIT_READY",
            settings != null && module != null
                ? $"module={module.ModuleId}"
                : "authored settings or module missing");
        if (settings == null || module == null)
        {
            yield break;
        }

        string traitInstanceId = "acquired-trait-instance:qa:stored-use";
        string[] evidence =
        {
            "qa:stored-use:a",
            "qa:stored-use:b",
            "qa:stored-use:c"
        };
        CharacterNarrativeDomain domain = module.DomainAffinities[0];
        foreach (string factId in evidence)
        {
            actor.Progression.NarrativeLedger.Record(
                domain,
                factId,
                "target:qa:stored-use",
                "completed");
        }
        CharacterAcquiredTraitAggregateState seeded = new()
        {
            revision = 1,
            processedMilestones = new List<int> { 3 },
            instances = new List<CharacterAcquiredTraitInstanceState>
            {
                new()
                {
                    instanceId = traitInstanceId,
                    combinationId = CharacterAcquiredTraitCombinationIdentity
                        .Build(new[] { module.ModuleId }),
                    moduleIds = new List<string> { module.ModuleId },
                    displayName = module.DisplayName,
                    description = module.Description,
                    narrativeReason = "실제 창고 사용 경로 검증용 후천 특성",
                    evidenceFactIds = evidence.OrderBy(
                        value => value,
                        StringComparer.Ordinal).ToList(),
                    manifestationMilestone = 3,
                    originatingRequestId = "request:qa:stored-use",
                    originatingRequestKey = "request-key:qa:stored-use",
                    candidatePacketHash = NarrativeInferenceHash
                        .ComputeSha256Utf8("packet:qa:stored-use"),
                    selectionAuditId = "audit:qa:stored-use",
                    acceptedRevision = 1,
                    erasedAt = CharacterAcquiredTraitInstanceState
                        .NotErasedAtAbsoluteHour
                }
            },
            pendingRequests = new List<CharacterAcquiredTraitPendingRequestState>()
        };
        bool seededTrait = actor.Progression.TryCommitAcquiredTraitState(
            seeded,
            expectedRevision: 0,
            settings,
            modules,
            out IReadOnlyList<CharacterAcquiredTraitValidationIssue> issues);
        Check(seededTrait,
            "MEMORY_SEAL_TRAIT_SEEDED",
            seededTrait
                ? traitInstanceId
                : string.Join(" | ", issues.Select(issue => issue.Message)));
        if (!seededTrait)
        {
            yield break;
        }

        int ledgerFactCount = actor.Progression.NarrativeLedger.facts.Count;
        int[] processedMilestones = seeded.processedMilestones.ToArray();
        Vector2Int actorBeforeCommand = actor.GetNowXY();
        Vector2Int warehousePosition = warehouseBuilding.centerPos;
        string warehouseDestinationId;
        try
        {
            warehouseDestinationId = WarehouseStorageIdentity
                .RequireDestinationId(warehouseBuilding);
        }
        catch (Exception exception)
        {
            Check(false,
                "MEMORY_SEAL_WAREHOUSE_ID",
                exception.Message);
            yield break;
        }

        HashSet<string> stackIdsBefore = itemRuntime.GetAllStacks()
            .Where(stack => stack != null)
            .Select(stack => stack.StackId)
            .ToHashSet(StringComparer.Ordinal);
        bool spawned = itemRuntime.SpawnItemAt(
            MemoryErasureSealItemRules.ItemId,
            MemoryErasureSealItemRules.UseQuantity,
            warehousePosition,
            WorldItemStackState.Stored,
            warehouseDestinationId,
            out int spawnedAmount);
        WorldItemStackSnapshot source = itemRuntime
            .GetStacksAt(warehousePosition, includeStored: true)
            .SingleOrDefault(stack => stack != null
                && !stackIdsBefore.Contains(stack.StackId)
                && stack.State == WorldItemStackState.Stored
                && string.Equals(
                    stack.ItemId,
                    MemoryErasureSealItemRules.ItemId,
                    StringComparison.Ordinal));
        Check(spawned
                && spawnedAmount == MemoryErasureSealItemRules.UseQuantity
                && source != null,
            "MEMORY_SEAL_STORED_SOURCE_CREATED",
            $"spawned={spawnedAmount}; source={source?.StackId ?? "<none>"}; "
                + $"warehouse={warehouseDestinationId}");
        if (source == null)
        {
            yield break;
        }
        createdStackIds.Add(source.StackId);

        bool storedVisibilityBefore = itemRuntime.StoredItemMarkersVisible;
        if (!storedVisibilityBefore)
        {
            yield return ClickUiButton(
                FindVisibleButtonByName("ItemStackViewToggle"),
                "show stored items for memory seal");
        }
        itemPanel.OnTriggerEvent(
            new InfoFeedEvent(new ItemPileInfoTarget(warehousePosition)));
        yield return null;
        yield return ClickUiButton(
            FindVisibleButtonByName("StackRow_" + source.StackId),
            "stored memory seal row");
        yield return ClickUiButton(
            FindVisibleButtonByLabel("사용"),
            "stored memory seal use action");
        yield return ClickUiButton(
            FindVisibleButtonByName(
                "MemoryErasureSealTarget_"
                + actor.Identity.PersistentId),
            "memory seal target");
        yield return ClickUiButton(
            FindVisibleButtonByName(
                "MemoryErasureSealTrait_" + traitInstanceId),
            "memory seal acquired trait");

        WorldItemStackSnapshot beforeConfirmation = itemRuntime
            .GetStacksAt(warehousePosition, includeStored: true)
            .SingleOrDefault(stack => string.Equals(
                stack.StackId,
                source.StackId,
                StringComparison.Ordinal));
        Check(VisibleTextsContain("기억 소거 확인")
                && beforeConfirmation != null
                && beforeConfirmation.State == WorldItemStackState.Stored
                && beforeConfirmation.AvailableQuantity == 1
                && beforeConfirmation.ReservedQuantity == 0
                && actor.GetNowXY() == actorBeforeCommand,
            "MEMORY_SEAL_CONFIRMATION_IS_NON_MUTATING",
            beforeConfirmation != null
                ? $"state={beforeConfirmation.State}; available="
                    + beforeConfirmation.AvailableQuantity
                    + $"; reserved={beforeConfirmation.ReservedQuantity}; "
                    + $"actor={actor.GetNowXY()}"
                : "source stack missing before confirmation");

        string targetCharacterId = actor.BuildingCharacterId.Value;
        List<MemoryErasureSealOrderStage> observedStages = new();
        bool completed = false;
        MemoryErasureSealUseResult completion = default;
        void OnOrderChanged(MemoryErasureSealOrderSnapshot order)
        {
            if (string.Equals(
                    order.TargetCharacterId,
                    targetCharacterId,
                    StringComparison.Ordinal)
                && string.Equals(
                    order.TraitInstanceId,
                    traitInstanceId,
                    StringComparison.Ordinal))
            {
                observedStages.Add(order.Stage);
            }
        }
        void OnUseCompleted(MemoryErasureSealUseResult result)
        {
            if (string.Equals(
                    result.TargetCharacterId,
                    targetCharacterId,
                    StringComparison.Ordinal)
                && string.Equals(
                    result.TraitInstanceId,
                    traitInstanceId,
                    StringComparison.Ordinal))
            {
                completion = result;
                completed = true;
            }
        }

        commands.OrderChanged += OnOrderChanged;
        commands.UseCompleted += OnUseCompleted;
        yield return ClickUiButton(
            FindVisibleButtonByName("MemoryErasureSealConfirm"),
            "confirm memory seal use");
        Time.timeScale = 4f;
        float deadline = Time.realtimeSinceStartup + 15f;
        while (!completed && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
        Time.timeScale = 0f;
        commands.OrderChanged -= OnOrderChanged;
        commands.UseCompleted -= OnUseCompleted;

        CharacterAcquiredTraitAggregateState after = actor.Progression
            .CaptureAcquiredTraitState();
        bool erased = after.TryGetInstance(
                traitInstanceId,
                out CharacterAcquiredTraitInstanceState erasedTrait)
            && erasedTrait.erased;
        bool sourceGone = itemRuntime.GetAllStacks().All(stack =>
            stack == null
            || !string.Equals(
                stack.StackId,
                source.StackId,
                StringComparison.Ordinal));
        bool operationCargoGone = actor.CarryInventory == null
            || actor.CarryInventory.Items.All(item => item == null
                || !string.Equals(
                    item.ownerOperationId,
                    completion.OperationId,
                    StringComparison.Ordinal));
        MemoryErasureSealOrderStage[] expectedStages =
        {
            MemoryErasureSealOrderStage.Reserved,
            MemoryErasureSealOrderStage.MovingToWarehouse,
            MemoryErasureSealOrderStage.Carrying,
            MemoryErasureSealOrderStage.Applying
        };
        Check(completed
                && completion.Status == MemoryErasureSealUseStatus.Succeeded
                && ContainsInOrder(observedStages, expectedStages)
                && !commands.TryGetActiveOrder(actor, out _)
                && sourceGone
                && operationCargoGone
                && erased
                && after.processedMilestones.SequenceEqual(
                    processedMilestones)
                && actor.Progression.NarrativeLedger.facts.Count
                    == ledgerFactCount
                && actor.GetNowXY() != actorBeforeCommand
                && VisibleTextsContain("기억 소거 결과")
                && VisibleTextsContain("소거 완료"),
            "MEMORY_SEAL_STORED_UI_MOVEMENT_TRANSACTION",
            $"completed={completed}; status={completion.Status}; stages="
                + string.Join(",", observedStages)
                + $"; sourceGone={sourceGone}; cargoGone={operationCargoGone}; "
                + $"erased={erased}; start={actorBeforeCommand}; "
                + $"end={actor.GetNowXY()}; text={GetVisibleTextSample()}");

        CloseKnownPopups();
        if (itemRuntime.StoredItemMarkersVisible != storedVisibilityBefore)
        {
            yield return ClickUiButton(
                FindVisibleButtonByName("ItemStackViewToggle"),
                "restore stored item visibility");
        }
    }

    private IEnumerator EnsurePlayableRun()
    {
        Button startNew = FindSceneButton("StartNewRunButton");
        if (startNew != null && startNew.gameObject.activeInHierarchy)
        {
            yield return ClickUiButton(startNew, "new game");
            if (startNew.gameObject.activeInHierarchy)
            {
                yield return ClickUiButton(startNew, "confirm new game");
            }

            yield return new WaitForSecondsRealtime(0.2f);
        }

        OwnerRunManager ownerManager = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        if (ownerManager == null || ownerManager.CurrentOwnerActor == null)
        {
            string fastCommit = StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            report.Add("[INFO] FAST_PARTY_COMMIT " + fastCommit);
            for (int i = 0; i < 8; i++)
            {
                yield return null;
            }
        }

        ownerManager = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        Check(ownerManager != null && ownerManager.CurrentOwnerActor != null,
            "RUN_READY",
            ownerManager != null && ownerManager.CurrentOwnerActor != null
                ? $"owner={ownerManager.CurrentOwnerActor.name}"
                : "owner missing");
    }

    private bool TryFindVisibleTestCell(Grid grid, Camera camera, out Vector2Int cell, out Vector2 screenPoint)
    {
        cell = default;
        screenPoint = default;
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        foreach (GridCell candidate in grid.GetCells()
                     .OrderBy(candidate =>
                     {
                         Vector3 world = grid.GetWorldPos(candidate.Position) + new Vector3(0f, 0.18f, 0f);
                         Vector3 screen = camera.WorldToScreenPoint(world);
                         return Vector2.Distance(screen, screenCenter);
                     }))
        {
            Vector2Int position = candidate.Position;
            Vector3 world = grid.GetWorldPos(position) + new Vector3(0f, 0.18f, 0f);
            Vector3 screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0f
                || screen.x < 40f
                || screen.x > Screen.width - 40f
                || screen.y < 210f
                || screen.y > Screen.height - 40f)
            {
                continue;
            }

            if (IsScreenPointOverUi(screen)
                || itemRuntime?.TryGetPileAt(position, out _) == true)
            {
                continue;
            }

            if (Physics2D.OverlapPointAll(world)
                .Any(hit => hit != null && hit.GetComponentInParent<CharacterActor>() != null))
            {
                continue;
            }

            cell = position;
            screenPoint = screen;
            return true;
        }

        return false;
    }

    private IEnumerator HoverWorldPoint(Vector2 screenPoint)
    {
        ApplyMouseState(new MouseState { position = screenPoint });
        automationInput.MovePointer(screenPoint);
        yield return null;
        yield return null;
    }

    private IEnumerator PressMouse(Vector2 screenPoint)
    {
        ApplyMouseState(new MouseState { position = screenPoint }.WithButton(MouseButton.Left, true));
        automationInput.MovePointer(screenPoint);
        yield return null;
        yield return null;
    }

    private IEnumerator ReleaseMouse(Vector2 screenPoint)
    {
        ApplyMouseState(new MouseState { position = screenPoint });
        automationInput.MovePointer(screenPoint);
        yield return null;
        yield return null;
    }

    private void ApplyMouseState(MouseState state)
    {
        EnsureVerificationMouse();
        if (verificationMouse == null || !verificationMouse.added)
        {
            return;
        }

        verificationMouse.MakeCurrent();
        InputState.Change(verificationMouse, state);
        InputSystem.QueueStateEvent(verificationMouse, state);
        InputSystem.Update();
    }

    private void EnsureVerificationMouse()
    {
        if (verificationMouse == null || !verificationMouse.added)
        {
            CreateVerificationMouse();
            return;
        }

        if (!verificationMouse.enabled)
        {
            InputSystem.EnableDevice(verificationMouse);
        }

        verificationMouse.MakeCurrent();
    }

    private void CreateVerificationMouse()
    {
        if (verificationMouse != null && verificationMouse.added)
        {
            InputSystem.RemoveDevice(verificationMouse);
        }

        verificationMouse = InputSystem.AddDevice<Mouse>($"PhysicalItemPileVerificationMouse{++verificationMouseSerial}");
        InputSystem.EnableDevice(verificationMouse);
        verificationMouse.MakeCurrent();
    }

    private IEnumerator ClickUiButton(Button button, string label)
    {
        bool available = button != null && button.gameObject.activeInHierarchy && button.interactable;
        Check(available, "UI_POINTER_TARGET", available ? label : label + " missing");
        if (!available)
        {
            yield break;
        }

        Canvas.ForceUpdateCanvases();
        RectTransform rect = button.transform as RectTransform;
        Vector2 position = rect != null
            ? RectTransformUtility.WorldToScreenPoint(GetCanvasCamera(rect), rect.TransformPoint(rect.rect.center))
            : Vector2.zero;
        yield return PressMouse(position);
        yield return ReleaseMouse(position);
        yield return new WaitForSecondsRealtime(0.05f);
    }

    private IEnumerator CaptureScreen(string path)
    {
        yield return PlayModeVerificationFrameWait.CaptureReady();
        Texture2D capture = PlayModeVerificationFrameWait.CaptureScreenshotAsTexture();
        if (capture == null)
        {
            Check(false, "SCREEN_CAPTURE", path + " capture returned null");
            yield break;
        }

        try
        {
            byte[] bytes = capture.EncodeToPNG();
            bool written = TryWriteCapture(path, bytes, out string writtenPath, out string failure);
            Check(written, "SCREEN_CAPTURE_WRITE", failure);
            if (written)
            {
                report.Add($"SCREEN_CAPTURE_PATH={writtenPath}");
                Check(bytes.Length > 1000, "SCREEN_CAPTURE_NONBLANK", $"{writtenPath}; bytes={bytes.Length}");
            }
        }
        finally
        {
            Destroy(capture);
        }
    }

    private static bool TryWriteCapture(
        string requestedPath,
        byte[] bytes,
        out string writtenPath,
        out string failure)
    {
        Exception lastFailure = null;
        string directory = Path.GetDirectoryName(requestedPath) ?? string.Empty;
        string fileName = Path.GetFileNameWithoutExtension(requestedPath);
        string extension = Path.GetExtension(requestedPath);

        for (int attempt = 0; attempt <= 8; attempt++)
        {
            string candidate = attempt == 0
                ? requestedPath
                : Path.Combine(directory, $"{fileName}-retry-{attempt}{extension}");
            try
            {
                File.WriteAllBytes(candidate, bytes);
                writtenPath = candidate;
                failure = string.Empty;
                return true;
            }
            catch (IOException exception)
            {
                lastFailure = exception;
            }
            catch (UnauthorizedAccessException exception)
            {
                lastFailure = exception;
            }
        }

        writtenPath = string.Empty;
        failure = $"{requestedPath}; {lastFailure?.GetType().Name}: {lastFailure?.Message}";
        return false;
    }

    private void CloseKnownPopups()
    {
        foreach (ItemPileInfoPanel panel in UnityEngine.Object.FindObjectsByType<ItemPileInfoPanel>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            panel.OnClose();
        }

        foreach (CharacterSummaryInfo panel in UnityEngine.Object.FindObjectsByType<CharacterSummaryInfo>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            panel.OnClose();
        }

        foreach (BuildingSummaryInfo panel in UnityEngine.Object.FindObjectsByType<BuildingSummaryInfo>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            panel.OnClose();
        }

        foreach (UIBuildingInfo panel in UnityEngine.Object.FindObjectsByType<UIBuildingInfo>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            panel.CloseDispaly();
        }
    }

    private static bool IsItemPanelOpen()
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .Any(item => item != null
                && item.scene.IsValid()
                && item.activeInHierarchy
                && string.Equals(item.name, "ItemPileInfoCanvas", StringComparison.Ordinal));
    }

    private static string DescribeItemPanelState()
    {
        return $"itemPanel={IsItemPanelOpen()}; rows={FindStackRowButtons().Length}; text={GetVisibleTextSample()}";
    }

    private static Button[] FindStackRowButtons()
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .Where(button => button != null
                && button.gameObject.scene.IsValid()
                && button.gameObject.activeInHierarchy
                && button.interactable
                && button.name.StartsWith("StackRow_", StringComparison.Ordinal))
            .OrderBy(button => button.transform.GetSiblingIndex())
            .ToArray();
    }

    private static Button FindVisibleButtonByName(string name)
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(button => button != null
                && button.gameObject.scene.IsValid()
                && button.gameObject.activeInHierarchy
                && button.interactable
                && string.Equals(button.name, name, StringComparison.Ordinal));
    }

    private static Button FindVisibleButtonByLabel(string label)
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(button => button != null
                && button.gameObject.scene.IsValid()
                && button.gameObject.activeInHierarchy
                && button.interactable
                && button.GetComponentsInChildren<TMP_Text>(true)
                    .Any(text => text != null
                        && string.Equals(
                            text.text,
                            label,
                            StringComparison.Ordinal)));
    }

    private static Button FindSceneButton(string name)
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(button => button != null
                && button.gameObject.scene.IsValid()
                && string.Equals(button.name, name, StringComparison.Ordinal));
    }

    private static TMP_Text FindActiveText(string name)
    {
        return Resources.FindObjectsOfTypeAll<TMP_Text>()
            .FirstOrDefault(text => text != null
                && text.gameObject.scene.IsValid()
                && text.gameObject.activeInHierarchy
                && string.Equals(text.name, name, StringComparison.Ordinal));
    }

    private static bool VisibleTextsContain(string value)
    {
        return Resources.FindObjectsOfTypeAll<TMP_Text>()
            .Any(text => text != null
                && text.gameObject.scene.IsValid()
                && text.gameObject.activeInHierarchy
                && !string.IsNullOrEmpty(text.text)
                && text.text.Contains(value, StringComparison.Ordinal));
    }

    private static string GetVisibleTextSample()
    {
        string[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>()
            .Where(text => text != null
                && text.gameObject.scene.IsValid()
                && text.gameObject.activeInHierarchy
                && !string.IsNullOrWhiteSpace(text.text))
            .Select(text => Compact(text.text))
            .Take(12)
            .ToArray();
        return string.Join(" || ", texts);
    }

    private static GameObject FindItemMarker(Vector2Int position)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(item => item != null
                && item.scene.IsValid()
                && item.name == $"ItemPile_{position.x}_{position.y}");
    }

    private static CharacterActor FindClickableActor()
    {
        return CharacterActorCollection.DistinctByGameObject(
                UnityEngine.Object.FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None))
            .FirstOrDefault(actor => actor != null
                && !actor.IsDead
                && actor.GetComponentsInChildren<Collider2D>(true)
                    .Any(collider => collider != null && collider.enabled));
    }

    private static bool IsMemorySealActorEligible(CharacterActor actor) =>
        actor != null
        && actor.isActiveAndEnabled
        && actor.gameObject.activeInHierarchy
        && !actor.IsDead
        && actor.CurrentLifecycleState == CharacterLifecycleState.Active
        && actor.Progression != null
        && actor.Identity != null
        && !string.IsNullOrWhiteSpace(actor.Identity.PersistentId)
        && actor.Brain != null
        && actor.PathSearchBroker != null
        && actor.GetAbility<AbilityMove>() != null;

    private static int Manhattan(Vector2Int left, Vector2Int right) =>
        Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);

    private static bool ContainsInOrder<T>(
        IReadOnlyList<T> actual,
        IReadOnlyList<T> expected)
    {
        if (actual == null || expected == null)
        {
            return false;
        }

        int expectedIndex = 0;
        for (int index = 0;
             index < actual.Count && expectedIndex < expected.Count;
             index++)
        {
            if (EqualityComparer<T>.Default.Equals(
                    actual[index],
                    expected[expectedIndex]))
            {
                expectedIndex++;
            }
        }
        return expectedIndex == expected.Count;
    }

    private static bool IsCharacterPanelOpen(CharacterSummaryInfo characterSummary)
    {
        return characterSummary != null
            && characterSummary.UI != null
            && characterSummary.UI.activeInHierarchy;
    }

    private static Camera GetCanvasCamera(RectTransform rect)
    {
        Canvas canvas = rect != null ? rect.GetComponentInParent<Canvas>() : null;
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
    }

    private static bool IsScreenPointOverUi(Vector2 screenPoint)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || !eventSystem.isActiveAndEnabled)
        {
            return false;
        }

        PointerEventData pointer = new PointerEventData(eventSystem)
        {
            position = screenPoint
        };
        List<RaycastResult> results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointer, results);
        return results.Any(result => result.module is GraphicRaycaster);
    }

    private static DungeonRuntimeLifetimeScope FindScope()
    {
        return UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(scope => scope != null && scope.Container != null);
    }

    private static T Resolve<T>(DungeonRuntimeLifetimeScope scope) where T : class
    {
        try
        {
            return scope != null && scope.Container != null
                ? scope.Container.Resolve<T>()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        new GameObject("QA_EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private bool Check(bool passed, string key, string detail)
    {
        report.Add($"[{(passed ? "PASS" : "FAIL")}] {key} {detail}");
        if (!passed)
        {
            failures.Add($"{key}: {detail}");
        }

        return passed;
    }

    private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Warning)
        {
            capturedWarnings.Add(condition);
        }
        else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            capturedErrors.Add(string.IsNullOrWhiteSpace(stackTrace)
                ? condition
                : condition + "\n" + stackTrace);
        }
    }

    private void Finish()
    {
        Cleanup();
        Application.logMessageReceived -= OnLogMessageReceived;
        report.Add($"capturedErrors={capturedErrors.Count}; {Compact(capturedErrors)}");
        report.Add($"capturedWarnings={capturedWarnings.Count}; {Compact(capturedWarnings)}");
        bool passed = failures.Count == 0 && capturedErrors.Count == 0 && capturedWarnings.Count == 0;
        report.Add($"RESULT={(passed ? "PASS" : "FAIL")}; failures={failures.Count}; {Compact(failures)}");
        File.WriteAllText(PhysicalItemPilePlayModeVerifier.ReportPath, string.Join("\n", report));
        File.Delete(PhysicalItemPilePlayModeVerifier.RequestPath);

        if (passed)
        {
            Debug.Log("Physical item pile PlayMode verification passed. "
                + PhysicalItemPilePlayModeVerifier.ReportPath);
        }
        else
        {
            Debug.LogError("Physical item pile PlayMode verification failed. "
                + PhysicalItemPilePlayModeVerifier.ReportPath);
        }

        Destroy(gameObject);
    }

    private void Cleanup()
    {
        CloseKnownPopups();
        foreach (string stackId in createdStackIds.ToArray())
        {
            itemRuntime?.DeleteStack(stackId);
        }

        createdStackIds.Clear();
        if (actorStateCaptured && movedActor != null)
        {
            movedActor.transform.position = originalActorPosition;
            Physics2D.SyncTransforms();
        }

        actorStateCaptured = false;
        automationInput?.Dispose();
        automationInput = null;
        if (verificationMouse != null && verificationMouse.added)
        {
            InputSystem.RemoveDevice(verificationMouse);
        }

        if (originalMouse != null && originalMouse.added)
        {
            InputSystem.EnableDevice(originalMouse);
            originalMouse.MakeCurrent();
            InputSystem.QueueStateEvent(originalMouse, new MouseState { position = originalMousePosition });
            InputSystem.Update();
        }

        if (inputBehaviorCaptured)
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInputBehavior;
            inputBehaviorCaptured = false;
        }

        Time.timeScale = originalTimeScale;
    }

    private void OnDestroy()
    {
        Cleanup();
        Application.logMessageReceived -= OnLogMessageReceived;
    }

    private static string Compact(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace('\n', ' ').Replace('\r', ' ').Trim();
    }

    private static string Compact(IEnumerable<string> values)
    {
        if (values == null)
        {
            return "<none>";
        }

        string[] compact = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(Compact)
            .ToArray();
        return compact.Length == 0 ? "<none>" : string.Join(" || ", compact);
    }
}
#endif
