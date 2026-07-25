#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

public static class ExpeditionEquipmentPlayModeVerifier
{
    public const string ReportPath = "Artifacts/QA/expedition-equipment-playmode-report.txt";
    public const string CapturePath = "Artifacts/QA/expedition-equipment-ui.png";

    [MenuItem("DungeonStory/Debug/QA/Run Expedition Equipment PlayMode Verification")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("Expedition equipment verification requires PlayMode in the gameplay scene.");
            return;
        }

        if (UnityEngine.Object.FindFirstObjectByType<ExpeditionEquipmentPlayModeVerificationRunner>() != null)
        {
            Debug.LogWarning("Expedition equipment verification is already running.");
            return;
        }

        new GameObject("Expedition Equipment PlayMode Verification Runner")
            .AddComponent<ExpeditionEquipmentPlayModeVerificationRunner>();
    }
}

public sealed class ExpeditionEquipmentPlayModeVerificationRunner : MonoBehaviour
{
    private const string IronEdgeId = "weapon:attack-iron";
    private const string BenchPath = "Assets/Resources/SO/Building/Modular/S08_대장작업대.asset";
    private const string WeaponStoragePath = "Assets/Resources/SO/Building/Modular/S07_무기보관함.asset";
    private const string FallbackWarehousePath = "Assets/Resources/SO/Building/P1/P1_Warehouse.asset";

    private readonly List<string> report = new List<string>();
    private readonly List<string> failures = new List<string>();
    private readonly List<string> capturedErrors = new List<string>();
    private readonly List<string> capturedWarnings = new List<string>();
    private readonly List<GameObject> temporaryObjects = new List<GameObject>();
    private IWorldItemStackRuntime itemRuntime;

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Artifacts/QA");
        Application.logMessageReceived += OnLogMessageReceived;
        EnsureEventSystem();
        yield return null;
        yield return null;

        DungeonRuntimeLifetimeScope scope = FindScope();
        if (!Check(scope != null && scope.Container != null, "SCOPE_READY", "gameplay LifetimeScope resolved"))
        {
            Finish();
            yield break;
        }

        IExpeditionEquipmentRuntime equipment = Resolve<IExpeditionEquipmentRuntime>(scope);
        itemRuntime = Resolve<IWorldItemStackRuntime>(scope);
        IFacilityEvolutionWarehouseInventoryQuery inventoryQuery =
            Resolve<IFacilityEvolutionWarehouseInventoryQuery>(scope);
        IOffenseExpeditionRuntimeProvider expeditionProvider = Resolve<IOffenseExpeditionRuntimeProvider>(scope);
        OffenseExpeditionRuntime expedition = null;
        if (!Check(equipment != null, "EQUIPMENT_RUNTIME", "IExpeditionEquipmentRuntime resolved")
            || !Check(itemRuntime != null, "ITEM_RUNTIME", "IWorldItemStackRuntime resolved")
            || !Check(inventoryQuery != null,
                "WAREHOUSE_QUERY",
                "facility warehouse inventory query resolved")
            || !Check(expeditionProvider != null && expeditionProvider.TryGetRuntime(out expedition),
                "EXPEDITION_RUNTIME",
                "OffenseExpeditionRuntime resolved"))
        {
            Finish();
            yield break;
        }

        yield return EnsurePartyHasMember(expedition);
        CharacterActor member = expedition.GetAvailableMemberActors().FirstOrDefault();
        if (!Check(member != null, "EXPEDITION_MEMBER", "one non-owner active staff member is available"))
        {
            Finish();
            yield break;
        }

        Facility warehouse = CreateInjectedFacility(scope, WeaponStoragePath, new Vector2Int(-40, -40), "QA_Weapon_Storage");
        if (warehouse == null
            || warehouse.Inventory == null
            || warehouse.Inventory.Deposit(StockCategory.Weapon, 20) <= 0)
        {
            warehouse = CreateInjectedFacility(scope, FallbackWarehousePath, new Vector2Int(-43, -40), "QA_Fallback_Warehouse");
        }

        Facility bench = CreateInjectedFacility(scope, BenchPath, new Vector2Int(-36, -40), "QA_S08_EquipmentCraftBench");
        if (!Check(warehouse != null && warehouse.Inventory != null, "WAREHOUSE_READY", "weapon craft materials can be stored")
            || !Check(bench != null && bench.BuildingData != null, "CRAFT_BENCH_READY", "S08 crafting bench initialized")
            || !Check(bench.BuildingData.GetAbility<BuildingEquipmentCraftingAbility>() != null,
                "CRAFT_ABILITY_READY",
                "S08 has BuildingEquipmentCraftingAbility"))
        {
            Finish();
            yield break;
        }

        WarehouseInventory materialInventory = FindQueriedCraftMaterialInventory(inventoryQuery, warehouse);
        if (materialInventory == null)
        {
            materialInventory = warehouse.Inventory;
        }

        EnsureWeaponCraftMaterial(materialInventory, 20);
        int queriedInventoryCount = inventoryQuery.GetInventories().Count(inventory => inventory != null);
        int weaponStockBefore = GetQueriedWeaponStock(inventoryQuery, materialInventory);
        Check(weaponStockBefore >= 2,
            "CRAFT_MATERIAL_VISIBLE",
            $"weaponStock={weaponStockBefore}; queriedInventories={queriedInventoryCount}; tempWarehouseSeen={ReferenceEquals(materialInventory, warehouse.Inventory)}");

        yield return VerifyBuildingCraftButton(bench, equipment, weaponStockBefore, materialInventory, inventoryQuery);
        yield return VerifyCraftWorkCompletes(bench, warehouse, member, equipment);
        yield return VerifyExpeditionEquipButton(expedition, equipment, member);
        yield return CaptureScreen();

        Finish();
    }

    private IEnumerator EnsurePartyHasMember(OffenseExpeditionRuntime expedition)
    {
        if (expedition.GetAvailableMemberActors().Count > 0)
        {
            yield break;
        }

        string fastCommit = StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
        Time.timeScale = 1f;
        report.Add("[INFO] FAST_PARTY_COMMIT " + fastCommit);
        for (int i = 0; i < 8; i++)
        {
            yield return null;
        }
        Canvas.ForceUpdateCanvases();
        Check(expedition.GetAvailableMemberActors().Count > 0,
            "FAST_PARTY_AVAILABLE",
            $"members={expedition.GetAvailableMemberActors().Count}; {DescribeExpeditionCandidates()}");
    }

    private static string DescribeExpeditionCandidates()
    {
        CharacterActor[] actors = CharacterActorCollection.DistinctByGameObject(
                UnityEngine.Object.FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None))
            .ToArray();
        return string.Join(" || ", actors.Select(actor =>
        {
            if (actor == null)
            {
                return "null";
            }

            actor.EnsureRuntimeState();
            bool hasWork = actor.AbilityCache != null
                && actor.AbilityCache.TryGetAbility(out AbilityWork _);
            bool canJoin = OffenseExpeditionService.CanJoinExpedition(actor, out string reason);
            return $"{actor.name}:id={actor.Identity?.PersistentId};scene={actor.gameObject.scene.name};"
                + $"activeSelf={actor.gameObject.activeSelf};activeHierarchy={actor.gameObject.activeInHierarchy};"
                + $"type={actor.Identity?.CharacterType};role={actor.Identity?.Role};"
                + $"life={actor.CurrentLifecycleState};health={actor.CurrentHealth:0.#}/{actor.MaxHealth:0.#};"
                + $"hasWork={hasWork};canJoin={canJoin};reason={reason}";
        }));
    }

    private IEnumerator VerifyBuildingCraftButton(
        Facility bench,
        IExpeditionEquipmentRuntime equipment,
        int weaponStockBefore,
        WarehouseInventory materialInventory,
        IFacilityEvolutionWarehouseInventoryQuery inventoryQuery)
    {
        UIBuildingInfo info = Resources.FindObjectsOfTypeAll<UIBuildingInfo>()
            .FirstOrDefault(item => item != null && item.gameObject.scene.IsValid());
        if (!Check(info != null, "BUILDING_INFO_UI", "UIBuildingInfo scene component resolved"))
        {
            yield break;
        }

        info.CloseDispaly();
        yield return new WaitForSecondsRealtime(0.2f);
        info.DisplayBuildingInfo(bench);
        Canvas.ForceUpdateCanvases();
        yield return null;

        Button craftButton = FindVisibleButtonByName("BuildingCraft_weapon_attack_iron");
        if (!Check(craftButton != null, "CRAFT_BUTTON_VISIBLE", "Iron Edge craft button is visible in building info"))
        {
            yield break;
        }

        int queueBefore = equipment.CraftQueue.Count;
        yield return Click(craftButton);
        Canvas.ForceUpdateCanvases();
        yield return null;

        string craftStatus = GetVisibleTextByName("BuildingCraftStatus");
        bool queued = equipment.CraftQueue.Count == queueBefore + 1
            && equipment.CraftQueue.Any(order => order != null
                && string.Equals(order.equipmentId, IronEdgeId, StringComparison.Ordinal));
        Check(queued,
            "CRAFT_BUTTON_QUEUES_ORDER",
            $"queue={queueBefore}->{equipment.CraftQueue.Count}; status={craftStatus}");
        ExpeditionEquipmentCraftOrderSaveData queuedOrder = equipment.CraftQueue
            .LastOrDefault(order => order != null
                && string.Equals(order.equipmentId, IronEdgeId, StringComparison.Ordinal));
        Check(queuedOrder != null
                && !queuedOrder.materialsReady
                && !string.IsNullOrWhiteSpace(queuedOrder.materialDestinationId),
            "CRAFT_ORDER_WAITS_FOR_MATERIALS",
            queuedOrder != null
                ? $"ready={queuedOrder.materialsReady}; destination={queuedOrder.materialDestinationId}"
                : "missing order");

        int weaponStockAfter = GetQueriedWeaponStock(inventoryQuery, materialInventory);
        Check(weaponStockAfter == weaponStockBefore,
            "CRAFT_COST_RESERVED_UNTIL_PICKUP",
            $"weaponStock={weaponStockBefore}->{weaponStockAfter}; status={craftStatus}");
        int deliveryStacks = itemRuntime != null && queuedOrder != null
            ? itemRuntime.GetAllStacks().Count(stack => stack != null
                && stack.HasDestinationPosition
                && string.Equals(stack.DestinationId, queuedOrder.materialDestinationId, StringComparison.Ordinal))
            : 0;
        Check(deliveryStacks > 0,
            "CRAFT_MATERIAL_DELIVERY_STACK",
            $"deliveryStacks={deliveryStacks}; destination={queuedOrder?.materialDestinationId ?? "none"}");
    }

    private IEnumerator VerifyCraftWorkCompletes(
        Facility bench,
        IWarehouseFacility warehouse,
        CharacterActor worker,
        IExpeditionEquipmentRuntime equipment)
    {
        int inventoryBefore = equipment.Inventory.TryGetValue(IronEdgeId, out int before)
            ? before
            : 0;
        yield return SimulateCraftMaterialDeliveryForVerification(bench, equipment);
        int guard = 0;
        while (equipment.CraftQueue.Any(order => order != null
                   && string.Equals(order.equipmentId, IronEdgeId, StringComparison.Ordinal))
               && guard++ < 30)
        {
            ModularFacilityRuntimeEffects.ApplyWorkCompleted(worker, bench, BuiltInWorkTypeIds.Craft);
            yield return null;
        }

        string outputItemId = DungeonItemCatalogSO.EquipmentItemId(IronEdgeId);
        WorldItemStackSnapshot outputStack = itemRuntime != null
            ? itemRuntime.GetAllStacks()
                .FirstOrDefault(stack => stack != null
                    && stack.State == WorldItemStackState.FacilityBuffer
                    && string.Equals(stack.ItemId, outputItemId, StringComparison.Ordinal))
            : null;
        Check(outputStack != null,
            "CRAFT_OUTPUT_STACK_CREATED",
            outputStack != null
                ? $"stack={outputStack.StackId}; qty={outputStack.Quantity}; pos={outputStack.Position}"
                : "missing output stack");

        if (outputStack != null
            && warehouse != null
            && itemRuntime != null)
        {
            CharacterCarryInventory carry = CharacterCarryInventory.Ensure(worker);
            if (carry != null
                && carry.TryAdd(
                    outputStack.StackId,
                    outputStack.ItemId,
                    outputStack.Quantity,
                    itemRuntime.CatalogProvider,
                    itemRuntime.HaulingSettingsProvider,
                    out _))
            {
                itemRuntime.DeleteStack(outputStack.StackId);
                itemRuntime.TryDepositCarriedItems(
                    worker,
                    carry,
                    warehouse,
                    out _);
            }
        }

        int inventoryAfter = equipment.Inventory.TryGetValue(IronEdgeId, out int after)
            ? after
            : 0;
        Check(inventoryAfter == inventoryBefore + 1,
            "CRAFT_WORK_COMPLETES",
            $"Iron Edge inventory={inventoryBefore}->{inventoryAfter}; cycles={guard}");
    }

    private IEnumerator SimulateCraftMaterialDeliveryForVerification(
        Facility bench,
        IExpeditionEquipmentRuntime equipment)
    {
        ExpeditionEquipmentCraftOrderSaveData order = equipment.CraftQueue
            .FirstOrDefault(item => item != null
                && string.Equals(item.equipmentId, IronEdgeId, StringComparison.Ordinal)
                && !item.materialsReady);
        if (order == null || itemRuntime == null)
        {
            yield break;
        }

        WorldItemStackSnapshot[] deliveries = itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && stack.HasDestinationPosition
                && string.Equals(stack.DestinationId, order.materialDestinationId, StringComparison.Ordinal))
            .ToArray();
        foreach (WorldItemStackSnapshot delivery in deliveries)
        {
            itemRuntime.SpawnItemAt(
                delivery.ItemId,
                delivery.Quantity,
                bench.centerPos,
                WorldItemStackState.FacilityBuffer,
                order.materialDestinationId,
                out _);
            itemRuntime.DeleteStack(delivery.StackId);
        }

        yield return null;
        bool ready = equipment.HasPendingCraftWork(new[] { IronEdgeId });
        Check(ready,
            "CRAFT_MATERIALS_READY",
            $"deliveredStacks={deliveries.Length}; destination={order.materialDestinationId}");
    }

    private IEnumerator VerifyExpeditionEquipButton(
        OffenseExpeditionRuntime expedition,
        IExpeditionEquipmentRuntime equipment,
        CharacterActor member)
    {
        foreach (UIBuildingInfo info in Resources.FindObjectsOfTypeAll<UIBuildingInfo>()
                     .Where(item => item != null && item.gameObject.scene.IsValid()))
        {
            info.CloseDispaly();
        }
        yield return null;

        if (equipment.GetAvailableCount(IronEdgeId) <= 0)
        {
            equipment.AddInventory(IronEdgeId, 1);
        }

        OffenseExpeditionPanel panel = expedition.ShowExpeditionPanel();
        yield return null;
        Canvas.ForceUpdateCanvases();

        string memberName = GetActorName(member);
        Button memberButton = FindVisibleButtonContaining(panel, memberName);
        if (!Check(memberButton != null, "EXPEDITION_MEMBER_BUTTON", $"member button for {memberName} visible"))
        {
            yield break;
        }

        yield return Click(memberButton);
        yield return null;
        Canvas.ForceUpdateCanvases();

        Button equipButton = FindVisibleButtonContaining(panel, "Iron Edge");
        if (!Check(equipButton != null, "EQUIP_BUTTON_VISIBLE", "Iron Edge equip button is visible after selecting one member"))
        {
            yield break;
        }

        yield return Click(equipButton);
        yield return null;
        Canvas.ForceUpdateCanvases();

        bool equipped = expedition.TryGetEquippedEquipment(
            member,
            ExpeditionEquipmentSlot.Weapon,
            out ExpeditionEquipmentDefinition definition)
            && string.Equals(definition.id, IronEdgeId, StringComparison.Ordinal);
        Check(equipped,
            "EQUIP_BUTTON_CHANGES_STATE",
            equipped ? $"equipped={definition.displayName}" : "weapon slot did not change");

        TMP_Text detail = Resources.FindObjectsOfTypeAll<TMP_Text>()
            .FirstOrDefault(text => text != null
                && text.gameObject.scene.IsValid()
                && text.gameObject.activeInHierarchy
                && text.name == "OffenseExpeditionDetail");
        bool detailShowsEquipment = detail != null
            && detail.text.Contains("Iron Edge", StringComparison.Ordinal)
            && detail.text.Contains("공격", StringComparison.Ordinal);
        detailShowsEquipment = detailShowsEquipment
            || (equipped
                && detail != null
                && detail.text.Contains("Iron Edge", StringComparison.Ordinal));
        Check(detailShowsEquipment,
            "EQUIPMENT_DETAIL_VISIBLE",
            detail != null ? Compact(detail.text) : "detail text missing");

        if (panel != null)
        {
            panel.Hide();
        }
    }

    private IEnumerator CaptureScreen()
    {
        yield return PlayModeVerificationFrameWait.CaptureReady();
        Texture2D capture = PlayModeVerificationFrameWait.CaptureScreenshotAsTexture();
        Color32[] pixels = capture != null ? capture.GetPixels32() : Array.Empty<Color32>();
        bool nonBlank = pixels.Any(pixel => pixel.a > 0 && (pixel.r > 8 || pixel.g > 8 || pixel.b > 8));
        Check(nonBlank,
            "SCREEN_CAPTURE_NONBLANK",
            capture != null ? $"{capture.width}x{capture.height}; pixels={pixels.Length}" : "capture missing");
        if (capture != null)
        {
            File.WriteAllBytes(ExpeditionEquipmentPlayModeVerifier.CapturePath, capture.EncodeToPNG());
            Destroy(capture);
        }
    }

    private Facility CreateInjectedFacility(
        DungeonRuntimeLifetimeScope scope,
        string assetPath,
        Vector2Int position,
        string objectName)
    {
        BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(assetPath);
        if (building == null)
        {
            Check(false, "LOAD_BUILDING_ASSET", assetPath);
            return null;
        }

        GameObject obj = new GameObject(objectName);
        temporaryObjects.Add(obj);
        Facility facility = obj.AddComponent<Facility>();
        InjectGameObject(scope, obj);
        facility.Initialization(building, position);
        return facility;
    }

    private static WarehouseInventory FindQueriedCraftMaterialInventory(
        IFacilityEvolutionWarehouseInventoryQuery inventoryQuery,
        Facility preferredWarehouse)
    {
        WarehouseInventory preferred = preferredWarehouse != null
            ? preferredWarehouse.Inventory
            : null;
        WarehouseInventory[] inventories = inventoryQuery?.GetInventories()
            .Where(inventory => inventory != null)
            .ToArray() ?? Array.Empty<WarehouseInventory>();
        if (preferred != null && inventories.Any(inventory => ReferenceEquals(inventory, preferred)))
        {
            return preferred;
        }

        return inventories.FirstOrDefault(inventory => inventory.Accepts(StockCategory.Weapon))
            ?? inventories.FirstOrDefault();
    }

    private static void EnsureWeaponCraftMaterial(WarehouseInventory inventory, int desiredStock)
    {
        if (inventory == null)
        {
            return;
        }

        int missing = Mathf.Max(0, desiredStock - inventory.GetStock(StockCategory.Weapon));
        if (missing == 0)
        {
            return;
        }

        int deposited = inventory.Deposit(StockCategory.Weapon, missing);
        missing -= deposited;
        if (missing > 0)
        {
            inventory.AddStock(StockCategory.Weapon, missing);
        }
    }

    private static int GetQueriedWeaponStock(
        IFacilityEvolutionWarehouseInventoryQuery inventoryQuery,
        WarehouseInventory fallbackInventory)
    {
        WarehouseInventory[] inventories = inventoryQuery?.GetInventories()
            .Where(inventory => inventory != null)
            .ToArray() ?? Array.Empty<WarehouseInventory>();
        if (inventories.Length > 0)
        {
            return inventories.Sum(inventory => inventory.GetStock(StockCategory.Weapon));
        }

        return fallbackInventory != null
            ? fallbackInventory.GetStock(StockCategory.Weapon)
            : 0;
    }

    private static void InjectGameObject(DungeonRuntimeLifetimeScope scope, GameObject target)
    {
        if (scope == null || scope.Container == null || target == null)
        {
            return;
        }

        foreach (MonoBehaviour component in target.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component != null)
            {
                scope.Container.Inject(component);
            }
        }
    }

    private static T Resolve<T>(DungeonRuntimeLifetimeScope scope) where T : class
    {
        try
        {
            return scope.Container.Resolve<T>();
        }
        catch
        {
            return null;
        }
    }

    private static DungeonRuntimeLifetimeScope FindScope()
    {
        return UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(scope => scope != null && scope.Container != null);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        new GameObject("QA_EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private IEnumerator Click(Button button)
    {
        if (button == null)
        {
            yield break;
        }

        RectTransform rect = button.transform as RectTransform;
        Vector2 position = RectTransformUtility.WorldToScreenPoint(
            null,
            rect != null ? rect.TransformPoint(rect.rect.center) : button.transform.position);
        PlayModeVerificationFrameWait.DispatchPointerClick(button.gameObject, position);
        yield return null;
        yield return null;
    }

    private static Button FindVisibleButtonByName(string objectName)
    {
        return UnityEngine.Object.FindObjectsByType<Button>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(button => button != null
                && button.interactable
                && string.Equals(button.name, objectName, StringComparison.Ordinal));
    }

    private static Button FindVisibleButtonContaining(string text)
    {
        return FindVisibleButtonContaining(null, text);
    }

    private static Button FindVisibleButtonContaining(Component root, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        IEnumerable<Button> buttons = root != null
            ? root.GetComponentsInChildren<Button>(false)
            : UnityEngine.Object.FindObjectsByType<Button>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        return buttons
            .FirstOrDefault(button => button != null
                && button.interactable
                && button.GetComponentsInChildren<TMP_Text>(true)
                    .Any(label => label != null
                        && label.text.Contains(text, StringComparison.Ordinal)));
    }

    private static string GetVisibleTextByName(string objectName)
    {
        TMP_Text text = Resources.FindObjectsOfTypeAll<TMP_Text>()
            .FirstOrDefault(label => label != null
                && label.gameObject.scene.IsValid()
                && label.gameObject.activeInHierarchy
                && string.Equals(label.name, objectName, StringComparison.Ordinal));
        return text != null ? Compact(text.text) : string.Empty;
    }

    private static string GetActorName(CharacterActor actor)
    {
        actor?.EnsureRuntimeState();
        return actor?.Identity != null
            ? actor.Identity.DisplayName
            : actor != null ? actor.name : string.Empty;
    }

    private bool Check(bool condition, string id, string detail)
    {
        report.Add($"[{(condition ? "PASS" : "FAIL")}] {id} {detail}");
        if (!condition)
        {
            failures.Add($"{id}: {detail}");
        }

        return condition;
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
        foreach (GameObject obj in temporaryObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }

        Application.logMessageReceived -= OnLogMessageReceived;
        report.Add($"capturedErrors={capturedErrors.Count}; {Compact(capturedErrors)}");
        report.Add($"capturedWarnings={capturedWarnings.Count}; {Compact(capturedWarnings)}");
        bool passed = failures.Count == 0 && capturedErrors.Count == 0 && capturedWarnings.Count == 0;
        report.Add($"RESULT={(passed ? "PASS" : "FAIL")}; failures={failures.Count}; {Compact(failures)}");
        File.WriteAllText(ExpeditionEquipmentPlayModeVerifier.ReportPath, string.Join("\n", report));
        if (passed)
        {
            Debug.Log("Expedition equipment PlayMode verification passed. "
                + ExpeditionEquipmentPlayModeVerifier.ReportPath);
        }
        else
        {
            Debug.LogError("Expedition equipment PlayMode verification failed. "
                + ExpeditionEquipmentPlayModeVerifier.ReportPath);
        }

        Destroy(gameObject);
        EditorApplication.ExitPlaymode();
    }

    private static string Compact(IEnumerable<string> lines)
    {
        string text = string.Join(" | ", lines ?? Array.Empty<string>());
        return Compact(text);
    }

    private static string Compact(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        text = text.Replace('\r', ' ').Replace('\n', ' ');
        return text.Length <= 240 ? text : text.Substring(0, 240) + "...";
    }
}
#endif
