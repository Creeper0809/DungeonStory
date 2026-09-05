# Runtime Script Function Inventory

> Generated from current workspace. Excludes any script under an `Editor` directory. Heuristic extraction can miss multiline declarations; inspect target files directly before refactoring.

## Summary

- Generated: 2026-07-12 facility-evolution-record-event-recorder refresh
- Runtime script files: 296
- Extracted declarations: 3716
- Exclusion: `Assets/Scripts/**/Editor/**/*.cs`

## Flag Meanings

- `GlobalObjectLookup`
- `SingletonAccess`
- `ResourcesAccess`
- `Reflection`
- `EventBus`
- `RuntimeObjectCreation`
- `SceneMutation`
- `DependencyInjection`

## Files

### Assets\Scripts\Buildings\BuildableObject.cs

- Lines: 560
- Flags: SceneMutation, DependencyInjection

- L6 [type] `public class BuildableObject : MonoBehaviour, IGridOccupant, IGridMovementOccupant`
- L61 [function] `public virtual void Start()`
- L82 [function] `public virtual void SetGrid(Grid grid)`
- L87 [function] `public virtual void Initialization(BuildingSO buildingSO, Vector2Int buildPos)`
- L104 [function] `public virtual Vector3 GetMovementWorldPosition(Vector2Int gridPosition)`
- L120 [function] `public bool TryGetNearestWalkableWorldPosition(Vector3 fromWorld, out Vector3 worldPosition)`
- L139 [function] `public void DestroySelf()`
- L154 [function] `public void SetDamaged(bool value)`
- L165 [function] `public void SetFacilityLevel(int value)`
- L177 [function] `public bool SupportsFacilityRole(FacilityRole role)`
- L182 [function] `public bool SupportsWork(FacilityWorkType workType)`
- L187 [function] `public bool CanVisit(CharacterActor visitor, out string failureReason)`
- L233 [function] `public bool TryBeginUse(CharacterActor visitor, out string failureReason)`
- L247 [function] `public void EndUse(CharacterActor visitor)`
- L256 [function] `public bool TryReserveVisit(CharacterActor visitor, out string failureReason, float seconds = DefaultAiReservationSeconds)`
- L275 [function] `public void RefreshVisitReservation(CharacterActor visitor, float seconds = DefaultAiReservationSeconds)`
- L285 [function] `public void ReleaseVisitReservation(CharacterActor visitor)`
- L298 [function] `public bool TryReserveWorker(CharacterActor worker, out string failureReason, float seconds = DefaultAiReservationSeconds)`
- L320 [function] `public void RefreshWorkerReservation(CharacterActor worker, float seconds = DefaultAiReservationSeconds)`
- L331 [function] `public bool HasWorkerReservationForOther(CharacterActor worker)`
- L337 [function] `public void ReleaseWorkerReservation(CharacterActor worker)`
- L349 [function] `public bool CanAssignWork(FacilityWorkType workType, out string failureReason)`
- L380 [function] `private int GetActiveVisitReservationCountExcept(CharacterActor visitor)`
- L395 [function] `private void PruneExpiredVisitReservations()`
- L430 [function] `private void PruneExpiredWorkerReservation()`
- L483 [function] `public virtual float GetWorkUrgency(FacilityWorkType workType)`
- L517 [function] `public virtual bool isVisitable()`
- L522 [function] `private void OnMouseDown()`
- L532 [function] `protected void MarkFacilityDynamicStateDirty()`
- L537 [function] `private IFacilityCandidateCache ResolveFacilityCandidateCache()`
- L543 [function] `private IRoomFacilityPolicy ResolveRoomFacilityPolicy()`
- L549 [function] `private IBlueprintResearchWorkService ResolveBlueprintResearchWorkService()`
- L555 [function] `private IWorldInfoClickSelector RequireWorldInfoClickSelector()`

### Assets\Scripts\Buildings\BuildingManagementSummaryQuery.cs

- Lines: 196
- Flags: None

- L6 [type] `public readonly struct BuildingManagementSummary`
- L26 [type] `public readonly struct ShopManagementSummary`
- L28 [function] `public ShopManagementSummary(int totalShops, int stockedShops, int emptyShops)`
- L40 [type] `public readonly struct WarehouseManagementSummary`
- L70 [type] `public interface IBuildingManagementSummaryService`
- L77 [type] `public interface IBuildingManagementSummaryRuntimeSource`
- L84 [type] `public sealed class BuildingManagementSummaryRuntimeSource : IBuildingManagementSummaryRuntimeSource`
- L88 [function] `public BuildingManagementSummaryRuntimeSource(IDungeonSceneComponentQuery sceneQuery)`
- L98 [type] `public sealed class BuildingManagementSummaryService : IBuildingManagementSummaryService`
- L102 [function] `public BuildingManagementSummaryService(IBuildingManagementSummaryRuntimeSource runtimeSource)`
- L107 [function] `public BuildingManagementSummary CaptureBuildings()`
- L112 [function] `public ShopManagementSummary CaptureShops()`
- L117 [function] `public WarehouseManagementSummary CaptureWarehouses()`
- L127 [function] `private static bool IsValidWarehouse(IWarehouseFacility warehouse)`
- L135 [type] `public static class BuildingManagementSummaryQuery`
- L137 [function] `public static BuildingManagementSummary FromBuildings(IEnumerable<BuildableObject> buildings)`
- L157 [function] `public static ShopManagementSummary FromShops(IEnumerable<Shop> shops)`
- L168 [function] `public static WarehouseManagementSummary FromWarehouses(IEnumerable<IWarehouseFacility> warehouses)`
- L190 [function] `private static bool IsValidWarehouse(IWarehouseFacility warehouse)`

### Assets\Scripts\Buildings\BuildingSummaryFormatter.cs

- Lines: 59
- Flags: None

- L3 [type] `public readonly struct BuildingSummaryPresentation`
- L5 [function] `public BuildingSummaryPresentation(string objectName, string stockText)`
- L15 [type] `public interface IBuildingSummaryFormatter`
- L20 [type] `public sealed class BuildingSummaryFormatter : IBuildingSummaryFormatter`
- L24 [function] `public BuildingSummaryFormatter(IBuildingDefinitionLookup buildingDefinitionLookup)`
- L30 [function] `public BuildingSummaryPresentation Format(BuildableObject building)`
- L41 [function] `private static string FormatStockText(BuildableObject building)`

### Assets\Scripts\Buildings\Door.cs

- Lines: 33
- Flags: None

- L3 [type] `public class Door : BuildableObject`
- L5 [function] `public override void Initialization(BuildingSO buildingSO, Vector2Int buildPos)`
- L11 [function] `private void OnTriggerEnter2D(Collider2D collision)`
- L22 [function] `private void OnTriggerExit2D(Collider2D collision)`

### Assets\Scripts\Buildings\Facility.cs

- Lines: 266
- Flags: SceneMutation

- L5 [type] `public class Facility : BuildableObject, IInteractable, IWorkableFacility, IWarehouseFacility`
- L13 [function] `public override void Initialization(BuildingSO buildingSO, Vector2Int buildPos)`
- L24 [function] `public IEnumerator Interact(CharacterActor actor)`
- L57 [function] `public bool CanAssignWorker(CharacterActor actor, out string failureReason)`
- L91 [function] `public IEnumerator AllocateWorker(CharacterActor actor)`
- L110 [function] `public void DeallocateWorker(CharacterActor actor)`
- L129 [function] `private void PruneInvalidWorker()`
- L151 [function] `private Vector2 GetRandomUsePosition()`
- L165 [function] `private Vector2 GetWorkerPosition()`
- L176 [function] `private void ApplyUseRecovery(CharacterActor actor)`
- L260 [function] `private string objectNameOrDefault()`

### Assets\Scripts\Buildings\FacilityCrimeRiskUtility.cs

- Lines: 201
- Flags: None

- L3 [type] `public readonly struct FacilityCrimeRiskContext`
- L38 [type] `public static class FacilityCrimeRiskUtility`
- L40 [function] `public static float CalculateShopliftingChance(FacilityCrimeRiskContext context)`
- L59 [function] `public static float CalculateOperationalRisk(FacilityCrimeRiskContext context)`
- L88 [function] `private static float GetSupervisionPressure(FacilityCrimeRiskContext context)`
- L99 [function] `private static float GetNeedPressure(FacilityCrimeRiskContext context)`
- L126 [function] `private static float GetCrowdPressure(FacilityCrimeRiskContext context)`
- L139 [function] `private static float GetCartValuePressure(FacilityCrimeRiskContext context)`
- L151 [function] `private static float GetFacilityStatePressure(FacilityCrimeRiskContext context)`
- L167 [function] `private static float GetActorIncidentMultiplier(CharacterActor actor)`
- L181 [function] `private static float GetStat(CharacterStats stats, CharacterCondition condition, float defaultValue)`
- L186 [function] `private static float LowStatPressure(float value, float comfortable, float critical)`
- L192 [function] `public static bool ShouldTriggerCrime(float chance, float roll)`

### Assets\Scripts\Buildings\Hallway.cs

- Lines: 3
- Flags: None

- L1 [type] `public class Hallway : BuildableObject`

### Assets\Scripts\Buildings\IInteractable.cs

- Lines: 30
- Flags: None

- L3 [type] `public interface IInteractable`
- L5 [function] `public IEnumerator Interact(CharacterActor actor);`
- L8 [type] `public interface IGridMovementHandler`
- L10 [function] `public IEnumerator Traverse(CharacterActor actor, GridMoveStep step);`
- L13 [type] `public interface IStockedFacility`
- L19 [type] `public interface IWarehouseFacility`
- L25 [type] `public interface IWorkableFacility`
- L27 [function] `public bool CanAssignWorker(CharacterActor actor, out string failureReason);`
- L28 [function] `public IEnumerator AllocateWorker(CharacterActor actor);`
- L29 [function] `public void DeallocateWorker(CharacterActor actor);`

### Assets\Scripts\Buildings\Shop.cs

- Lines: 959
- Flags: SceneMutation, DependencyInjection

- L8 [type] `public class Shop : BuildableObject, IInteractable, IStockedFacility, IWorkableFacility`
- L16 [type] `public enum Type`
- L52 [function] `public override void Initialization(BuildingSO buildingSO, Vector2Int buildPos)`
- L82 [function] `public IEnumerator Interact(CharacterActor actor)`
- L200 [function] `public static bool CreatesRevenueFor(CharacterActor actor)`
- L205 [function] `public static bool IsInternalStaffUse(CharacterActor actor)`
- L210 [function] `public bool CanServeCustomer(CharacterActor actor, out string failureReason)`
- L232 [function] `public float GetCheckoutCrimeChance(int cartItemCount)`
- L237 [function] `public float GetCheckoutCrimeChance(CharacterActor actor, int cartItemCount, int cartValue)`
- L251 [function] `private bool TryResolveCheckoutCrime(CharacterActor actor, IReadOnlyList<RemainStock> cart)`
- L285 [function] `private string BuildCrimeDetail(CharacterActor actor, RemainStock stolenStock, int lossValue, float chance)`
- L294 [function] `private static int GetCartValue(IReadOnlyList<RemainStock> cart)`
- L313 [function] `private IEnumerator RunCheckoutService(CharacterActor actor)`
- L344 [function] `private IEnumerator WaitForServingWorker(CharacterActor actor)`
- L376 [function] `private bool ShouldWaitForServingWorker(CharacterActor actor)`
- L385 [function] `public List<Stock> GetStock()`
- L390 [function] `private List<Stock> GetStock(IReadOnlyDictionary<int, int> selectedCounts)`
- L403 [function] `private Stock CreatePricedStock(RemainStock stock)`
- L413 [function] `private float GetPriceMultiplier()`
- L432 [function] `public int GetStockCount()`
- L446 [function] `public int RestockFrom(IEnumerable<IWarehouseFacility> warehouses, int maxAmount, out string resultMessage)`
- L570 [function] `public int ReceiveRestock(SaleItem saleItem, int amount, int requestedAmount, out string resultMessage)`
- L593 [function] `public bool HasRestockSupply(IEnumerable<IWarehouseFacility> warehouses, out string failureReason)`
- L632 [function] `public void DebugClearStock()`
- L639 [function] `public Vector2 GetRandomBuyPos()`
- L646 [function] `private Vector2 GetCheckoutWorldPosition()`
- L652 [function] `public override float GetWorkUrgency(FacilityWorkType workType)`
- L666 [function] `public override bool isVisitable()`
- L671 [function] `public bool CanAssignWorker(CharacterActor actor, out string failureReason)`
- L705 [function] `public IEnumerator AllocateWorker(CharacterActor actor)`
- L726 [function] `public void DeallocateWorker(CharacterActor actor)`
- L746 [function] `private void PruneInvalidWorker()`
- L770 [function] `private bool RequiresServingWorker()`
- L777 [function] `private void FillStock()`
- L799 [function] `private void AddRemainStock(SaleItem saleItem, int amount)`
- L815 [function] `private RemainStock CreateRemainStock(SaleItem saleItem, int count)`
- L825 [function] `private StockCategory GetStockCategory(int saleItemId)`
- L830 [function] `private int GetConfiguredStockCapacity()`
- L849 [function] `private string objectNameOrDefault()`
- L856 [function] `private bool TryInitializeStock(bool requireCatalog)`
- L890 [function] `private void EnsureStockInitialized()`
- L895 [function] `private bool TryResolveGameData(bool requireProvider)`
- L915 [function] `private IShopStockCatalog RequireStockCatalog()`
- L921 [function] `private IFloatingNumberFeedbackService RequireFloatingNumberFeedbackService()`
- L927 [function] `private IWorkforceReplanService RequireWorkforceReplanService()`
- L933 [type] `public class RemainStock`
- L940 [function] `public RemainStock(int id,string itemName, int cost, int stock, OnBuyItemSO[] onbuy)`
- L949 [type] `public struct Stock`
- L954 [function] `public Stock(int id, int cost) : this()`

### Assets\Scripts\Buildings\SO\BuildingSO.cs

- Lines: 194
- Flags: None

- L7 [type] `public enum BuildingCategory`
- L20 [type] `public enum FacilityRole`
- L35 [type] `public enum FacilityWorkType`
- L49 [type] `public class FacilityData`
- L64 [field] `public bool selfContainedRoom`
- L76 [function] `public bool SupportsRole(FacilityRole role)`
- L81 [function] `public bool SupportsWork(FacilityWorkType workType)`
- L87 [type] `public readonly struct GridBuildingPlacement`
- L118 [function] `public List<Vector2Int> GetGridPosList(Vector2Int center)`
- L136 [type] `public class BuildingSO : DataScriptableObject`
- L185 [function] `public List<Vector2Int> GetGridPosList(Vector2Int center)`
- L190 [function] `public bool GetDraggable()`

### Assets\Scripts\Buildings\SO\Conditions\ConditionNeedMoney.cs

- Lines: 39
- Flags: None

- L4 [type] `public class ConditionNeedMoney : IBuildingCondition`
- L8 [function] `public void OnBuild(BuildingConditionContext context)`

### Assets\Scripts\Buildings\SO\Conditions\ConditionNeedToConnect.cs

- Lines: 42
- Flags: None

- L6 [type] `public class ConditionNeedToConnect : IBuildingCondition`
- L13 [function] `public void OnBuild(BuildingConditionContext context) {}`

### Assets\Scripts\Buildings\SO\Conditions\IBuildingCondition.cs

- Lines: 25
- Flags: None

- L4 [type] `public interface IBuildingCondition`
- L12 [function] `public void OnBuild(BuildingConditionContext context);`
- L15 [type] `public readonly struct BuildingConditionContext`
- L19 [function] `public BuildingConditionContext(GameData gameData)`

### Assets\Scripts\Buildings\SO\OnBuyItemSO.cs

- Lines: 12
- Flags: None

- L5 [type] `public class OnBuyItemSO : ScriptableObject`
- L7 [function] `public virtual void Onbuy(CharacterActor actor)`

### Assets\Scripts\Buildings\SO\SaleItem.cs

- Lines: 22
- Flags: None

- L6 [type] `public enum StockCategory`
- L15 [type] `public class SaleItem : DataScriptableObject`

### Assets\Scripts\Buildings\SO\StatChange.cs

- Lines: 13
- Flags: None

- L6 [type] `public class StatChange : OnBuyItemSO`
- L9 [function] `public override void Onbuy(CharacterActor actor)`

### Assets\Scripts\Buildings\SO\StockInfo.cs

- Lines: 402
- Flags: EventBus

- L8 [type] `public class StockInfo : DataScriptableObject`
- L17 [type] `public class WarehouseInventory`
- L27 [function] `public WarehouseInventory()`
- L31 [function] `public WarehouseInventory(int maxCapacity)`
- L36 [function] `public static WarehouseInventory CreateSeeded(int totalStock)`
- L52 [function] `public int GetStock(StockCategory category)`
- L59 [function] `public bool HasStock(StockCategory category)`
- L64 [function] `public bool CanStore(int amount)`
- L69 [function] `public int AddStock(StockCategory category, int amount)`
- L78 [function] `public int Deposit(StockCategory category, int amount)`
- L88 [function] `public int Withdraw(StockCategory category, int amount)`
- L101 [type] `public struct StockDeliveryOffer`
- L108 [function] `public StockDeliveryOffer(StockCategory category, int amount, int cost, string sourceLabel)`
- L120 [type] `public struct StockProductionRule`
- L126 [function] `public StockProductionRule(StockCategory category, int amount, string sourceLabel)`
- L135 [type] `public struct StockSupplyResult`
- L163 [function] `public string ToSummaryText()`
- L176 [type] `public struct StockSupplyEvent`
- L180 [function] `public StockSupplyEvent(StockSupplyResult result)`
- L187 [function] `public static void Trigger(StockSupplyResult result)`
- L194 [type] `public static class StockSupplyService`
- L335 [function] `public static int GetRemainingCapacity(IEnumerable<IWarehouseFacility> warehouses)`
- L363 [function] `private static bool CanDepositAll(IEnumerable<IWarehouseFacility> warehouses, int amount)`
- L369 [function] `private static int DepositToWarehouses(IEnumerable<IWarehouseFacility> warehouses, StockCategory category, int amount)`
- L386 [function] `private static IEnumerable<IWarehouseFacility> GetValidWarehouses(IEnumerable<IWarehouseFacility> warehouses)`

### Assets\Scripts\Buildings\Stair.cs

- Lines: 117
- Flags: SceneMutation

- L4 [type] `public class Stair : BuildableObject, IInteractable, IGridMovementHandler`
- L12 [function] `public IEnumerator Traverse(CharacterActor actor, GridMoveStep step)`
- L45 [function] `public IEnumerator Interact(CharacterActor actor)`
- L72 [function] `private float GetHiddenTravelDelay()`
- L79 [function] `private Vector3 GetFloorCenterAnchor(Vector2Int fallbackPosition)`

### Assets\Scripts\Buildings\UI\UIBuildingInfo.cs

- Lines: 129
- Flags: SceneMutation, DependencyInjection

- L11 [type] `public class UIBuildingInfo : SerializedMonoBehaviour`
- L41 [function] `private void Awake()`
- L53 [function] `public void DisplayBuildingInfo(BuildableObject building)`
- L90 [function] `public void OpenDispaly()`
- L100 [function] `public void CloseDispaly()`
- L112 [function] `private IBuildingDefinitionLookup ResolveBuildingLookup()`
- L118 [function] `private IUiTouchGuardService ResolveTouchGuard()`
- L125 [type] `public class UIConfig<T>`

### Assets\Scripts\Buildings\UI\UIBuildingSelectButton.cs

- Lines: 108
- Flags: SceneMutation, DependencyInjection

- L6 [type] `public class UIBuildingSelectButton : MonoBehaviour`
- L19 [function] `public void Construct(IDungeonGridBuildingControllerProvider buildingControllerProvider)`
- L25 [function] `public void Initialization(BuildingSO so)`
- L34 [function] `public void Initialization(BuildingSO so, IDungeonGridBuildingControllerProvider buildingControllerProvider)`
- L40 [function] `public void OnClick()`
- L47 [function] `public void ActiveDestroyMode()`
- L52 [function] `private void ApplyButtonSize()`
- L60 [function] `private void SetIcon(Sprite sprite)`
- L79 [function] `private Image ResolveIconImage()`
- L88 [function] `private Vector2 GetFittedIconSize(Sprite sprite)`
- L102 [function] `private DungeonStoryGridBuildingController RequireBuildingController()`

### Assets\Scripts\Character\Ability\AbilityMove.cs

- Lines: 564
- Flags: SceneMutation, DependencyInjection

- L8 [type] `public class AbilityMove : CharacterAbility`
- L29 [function] `protected override void Awake()`
- L34 [function] `public override void Initializtion(CharacterSO data)`
- L45 [function] `public IEnumerator MoveByPath(Queue<GridMoveStep> path, AIAction expectedAction = null)`
- L64 [function] `public IEnumerator MoveByStep(GridMoveStep step, AIAction expectedAction = null)`
- L89 [function] `public IEnumerator Move2GridPosition(Vector2Int gridPosition, AIAction expectedAction = null)`
- L98 [function] `public void StartExitDungeon()`
- L117 [function] `public void StartEnterDungeon(Vector3 entryDoorWorldPosition, Vector2Int entryGridPosition)`
- L127 [function] `public void StartMoveByCurrentActionPath(float waitDuration = 0f)`
- L133 [function] `public void StartWait(float duration)`
- L139 [function] `public bool StartIdleWander(float waitDuration, int minDistance = 2, int maxDistance = 8)`
- L153 [function] `public void CancelActiveMovement()`
- L162 [function] `private void StartTrackedActionMovement(IEnumerator routine)`
- L168 [function] `private IEnumerator TrackActionMovement(IEnumerator routine)`
- L174 [function] `private AIAction GetCurrentAction()`
- L181 [function] `private IEnumerator MoveByCurrentActionPath(float waitDuration, AIAction expectedAction)`
- L201 [function] `private IEnumerator MoveByPathThenWait(Queue<GridMoveStep> path, float waitDuration, AIAction expectedAction)`
- L255 [function] `private bool IsPlainIdleWalkable(Vector2Int position)`
- L266 [function] `private static bool IsSupportedIdleWanderPath(Queue<GridMoveStep> path)`
- L294 [function] `private static bool IsSupportedVerticalMovementStep(GridMoveStep step)`
- L301 [function] `private GridPathSearchResult GetIdleSearchResult(Vector2Int originalPos)`
- L318 [function] `private void SnapToGridRowIfWalkable(Vector2Int gridPosition)`
- L332 [function] `public IEnumerator MoveByCurrentBestActionPath()`
- L337 [function] `private IEnumerator MoveByActionPath(AIAction action)`
- L350 [function] `private void RefreshCurrentActionReservation()`
- L360 [function] `private bool IsActionMovementCancelled(AIAction expectedAction)`
- L369 [function] `private IEnumerator WaitForAiAction(float duration, AIAction expectedAction)`
- L384 [function] `private IEnumerator WaitForAiActionDelay(float duration, AIAction expectedAction)`
- L399 [function] `private IEnumerator EnterDungeon(Vector3 entryDoorWorldPosition, Vector2Int entryGridPosition)`
- L424 [function] `private IEnumerator ExitDungeon()`
- L477 [function] `private bool TryResolveSpawner()`
- L487 [function] `private ICharacterAiSchedulingService RequireAiSchedulingService()`
- L493 [function] `public IEnumerator Move2PosByTime(Vector3 endPos, float duration)`
- L505 [function] `public IEnumerator Move2PosBySpeed(Vector3 endPos, float multifly = 1.0f, AIAction expectedAction = null)`
- L555 [function] `private void UpdateFacingForMovement(float deltaX)`

### Assets\Scripts\Character\Ability\AbilitySchedule.cs

- Lines: 35
- Flags: None

- L5 [type] `public class AbilitySchedule : CharacterAbility`
- L10 [function] `protected override void Awake()`
- L20 [function] `private void CheckTime(int hour)`
- L24 [function] `private void OnDisable()`
- L29 [type] `public enum Schedule`

### Assets\Scripts\Character\Ability\AbilityShopping.cs

- Lines: 380
- Flags: SceneMutation, DependencyInjection

- L7 [type] `public class AbilityShopping : CharacterAbility`
- L33 [function] `public override void Initializtion(CharacterSO data)`
- L43 [function] `public Stock DetermineBuyingItem(List<Stock> stocks)`
- L74 [function] `public bool CanPay(Stock stock)`
- L80 [function] `public bool CanBuyFrom(Shop shop, out string failureReason)`
- L121 [function] `public float GetAffordabilityScore(Shop shop)`
- L148 [function] `public void StartSopping()`
- L153 [function] `private IEnumerator Shopping()`
- L201 [function] `public void RegisterVisit(BuildableObject building)`
- L214 [function] `public void RegisterLookAround()`
- L219 [function] `public void BeginOffDutyVisitCycle()`
- L226 [function] `public bool IsOffDutyStaffVisitor()`
- L235 [function] `public FacilityRole GetInterestRoles()`
- L240 [function] `public bool CanLookAround()`
- L247 [function] `public bool ShouldExitDungeon()`
- L254 [function] `public bool ShouldEndVisitCycle()`
- L259 [function] `public bool IsThereVisitableBuilding()`
- L291 [function] `public BuildableObject FindShop()`
- L335 [function] `private bool CanVisitBuilding(BuildableObject building)`
- L349 [function] `public int GetShoppingCount()`
- L355 [function] `public IEnumerator BuyItem(RemainStock item, int purchaseCost)`
- L375 [function] `private IFloatingIconFeedbackService RequireFloatingIconFeedbackService()`

### Assets\Scripts\Character\Ability\AbilityWork.cs

- Lines: 666
- Flags: DependencyInjection

- L6 [type] `public class AbilityWork : CharacterAbility`
- L8 [type] `public enum DutyState`
- L128 [function] `protected override void Awake()`
- L155 [function] `public override void Initializtion(CharacterSO data)`
- L172 [function] `public void EnsureWorkReferences()`
- L177 [function] `public bool TryAssignShop(GridPathSearchResult searchResult = null)`
- L182 [function] `public bool TryAssignWork(FacilityWorkType requestedWorkType, GridPathSearchResult searchResult = null)`
- L201 [function] `public float GetWorkUtilityScore(FacilityWorkType requestedWorkType, GridPathSearchResult searchResult)`
- L206 [function] `public bool TryGetLastRejectedWorkCandidate(out WorkTargetCandidate candidate)`
- L263 [function] `public bool TrySetPriorityWorkTarget(BuildableObject building, out string errorMessage)`
- L285 [function] `public bool TryGetPrioritySuppressDestination(GridPathSearchResult searchResult, out BuildableObject destination)`
- L290 [function] `public void ClearPriorityWorkTarget()`
- L295 [function] `public void SetWorkPriority(FacilityWorkType workType, WorkPriorityLevel priority)`
- L320 [function] `public bool ShouldUseRestProtection()`
- L325 [function] `public bool CanStartWorkAction()`
- L330 [function] `public bool CanStartWorkAction(FacilityWorkType requestedWorkType, GridPathSearchResult searchResult)`
- L336 [function] `public bool CanContinueCurrentWork(out string stopReason)`
- L341 [function] `public bool ShouldInterruptCurrentWork(out string interruptReason)`
- L346 [function] `public bool ShouldThrottleRoutineWork(FacilityWorkType workType)`
- L355 [function] `public void BeginRoutineWorkCooldown(FacilityWorkType workType)`
- L369 [function] `public bool ShouldTakeOffDuty()`
- L374 [function] `public bool ShouldReturnToWork()`
- L379 [function] `public void BeginOffDuty(string reason)`
- L384 [function] `public void PrepareForExpedition()`
- L389 [function] `public void SetDutyState(DutyState nextState)`
- L405 [function] `public void ApplyWorkFatigueTick()`
- L410 [function] `public IEnumerator CheckActionWork()`
- L415 [function] `public void CheckSchedule(Schedule schedule)`
- L423 [function] `internal void AssignWork(BuildableObject building, FacilityWorkType workType)`
- L429 [function] `internal void ReleaseAssignedWorkTarget()`
- L434 [function] `internal void StopAssignedWork(string reason)`
- L439 [function] `internal void StopAssignedWorkFromAi(string reason)`
- L444 [function] `private void StopAssignedWork(string reason, bool requestImmediateReplan)`
- L478 [function] `internal bool IsActiveWorkRun(int runId)`
- L483 [function] `internal bool CanContinueWorkRun(int runId)`
- L488 [function] `internal Coroutine StartCheckActionWork(int runId)`
- L500 [function] `internal void ClearActiveWorkRoutine(int runId)`
- L508 [function] `internal void ClearActiveWorkCheckRoutine(int runId)`
- L516 [function] `internal bool HasUrgentPriorityTarget()`
- L521 [function] `internal void MarkFacilityDynamicStateDirty()`
- L526 [function] `private bool CanExecuteSuppressCommand(FacilityWorkType requestedWorkType)`
- L532 [function] `private void OnDisable()`
- L537 [function] `private void OnEnable()`
- L542 [function] `private void TryBindScheduleEvents()`
- L565 [function] `private void UnbindScheduleEvents()`
- L575 [function] `private void EnsureWorkModules()`
- L591 [function] `private int BeginWorkRun()`
- L598 [function] `private void InvalidateActiveWorkRun()`
- L604 [function] `private void StopActiveWorkRoutines()`
- L610 [function] `private void StopActiveWorkRoutine()`
- L621 [function] `private void StopActiveWorkCheckRoutine()`
- L632 [function] `private IEnumerator ExecuteRestockWork()`
- L637 [function] `private IEnumerator ExecuteRepairWork()`
- L642 [function] `private IEnumerator ExecuteResearchWork()`
- L647 [function] `private IEnumerator SuppressPriorityTarget()`

### Assets\Scripts\Character\Ability\CharacterAbility.cs

- Lines: 84
- Flags: DependencyInjection

- L8 [type] `public class CharacterAbility : SerializedMonoBehaviour`
- L21 [function] `public void ConstructCharacterAbility(IGridSystemProvider gridSystemProvider)`
- L28 [function] `protected virtual void Awake()`
- L34 [function] `protected virtual void Start()`
- L39 [function] `protected void CacheCommonReferences()`
- L60 [function] `protected bool TryGetGrid(out Grid resolvedGrid)`
- L71 [function] `private void CacheSplitComponents()`
- L80 [function] `public virtual void Initializtion(CharacterSO data)`

### Assets\Scripts\Character\Ability\CharacterVisitPolicy.cs

- Lines: 73
- Flags: None

- L1 [type] `public static class CharacterVisitPolicy`
- L20 [function] `public static FacilityRole GetInterestRoles(CharacterActor actor)`
- L27 [function] `public static bool IsStaffPurchaseShop(CharacterActor actor, BuildableObject building)`

### Assets\Scripts\Character\AI\Action\AIActionSet.cs

- Lines: 198
- Flags: None

- L8 [type] `public abstract class AIActionSet : SerializedScriptableObject`
- L20 [function] `public virtual bool CanStart(CharacterActor actor)`
- L25 [function] `public virtual float AdjustScore(CharacterActor actor, float baseScore)`
- L83 [function] `public virtual bool CanContinue(CharacterActor actor, AIAction runningAction, out string stopReason)`
- L89 [function] `public virtual bool CanInterrupt(CharacterActor actor, AIAction runningAction, out string interruptReason)`
- L95 [function] `public virtual void Execute(CharacterActor actor)`
- L99 [function] `public virtual void OnStop(CharacterActor actor, AIAction runningAction, string reason)`
- L193 [function] `public virtual BuildableObject GetDestination(CharacterActor actor)`

### Assets\Scripts\Character\AI\Action\AIEat.cs

- Lines: 115
- Flags: None

- L6 [type] `public class AIEat : AIActionSet`
- L8 [function] `public override bool CanStart(CharacterActor actor)`
- L13 [function] `public override void Execute(CharacterActor actor)`
- L89 [function] `public override void RefreshDestinationReservation(CharacterActor actor, BuildableObject destination)`
- L94 [function] `public override void ReleaseDestinationReservation(CharacterActor actor, BuildableObject destination)`
- L99 [function] `private static bool CanUseVisitorAction(CharacterActor actor)`

### Assets\Scripts\Character\AI\Action\AIExitDungeon.cs

- Lines: 32
- Flags: None

- L4 [type] `public class AIExitDungeon : AIActionSet`
- L8 [function] `public override bool CanStart(CharacterActor actor)`
- L17 [function] `public override void Execute(CharacterActor actor)`

### Assets\Scripts\Character\AI\Action\AIFacilityRoleAction.cs

- Lines: 126
- Flags: None

- L6 [type] `public class AIFacilityRoleAction : AIActionSet`
- L16 [function] `public override bool CanStart(CharacterActor actor)`
- L21 [function] `public override void Execute(CharacterActor actor)`
- L97 [function] `public override void RefreshDestinationReservation(CharacterActor actor, BuildableObject destination)`
- L102 [function] `public override void ReleaseDestinationReservation(CharacterActor actor, BuildableObject destination)`
- L107 [function] `private static bool CanUseVisitorAction(CharacterActor actor)`

### Assets\Scripts\Character\AI\Action\AILookAround.cs

- Lines: 95
- Flags: None

- L7 [type] `public class AILookAround : AIActionSet`
- L14 [function] `public override bool CanStart(CharacterActor actor)`
- L19 [function] `public override void Execute(CharacterActor actor)`
- L83 [function] `public static bool CanUseVisitLookAround(CharacterActor actor)`

### Assets\Scripts\Character\AI\Action\AIRest.cs

- Lines: 118
- Flags: None

- L6 [type] `public class AIRest : AIActionSet`
- L8 [function] `public override bool CanStart(CharacterActor actor)`
- L13 [function] `public override void Execute(CharacterActor actor)`
- L92 [function] `public override void RefreshDestinationReservation(CharacterActor actor, BuildableObject destination)`
- L97 [function] `public override void ReleaseDestinationReservation(CharacterActor actor, BuildableObject destination)`
- L102 [function] `private static bool CanUseVisitorAction(CharacterActor actor)`

### Assets\Scripts\Character\AI\Action\AIShopping.cs

- Lines: 136
- Flags: None

- L6 [type] `public class AIShopping : AIActionSet`
- L8 [function] `public override bool CanStart(CharacterActor actor)`
- L13 [function] `public override void Execute(CharacterActor actor)`
- L110 [function] `public override void RefreshDestinationReservation(CharacterActor actor, BuildableObject destination)`
- L115 [function] `public override void ReleaseDestinationReservation(CharacterActor actor, BuildableObject destination)`
- L120 [function] `private static bool CanUseVisitorAction(CharacterActor actor)`

### Assets\Scripts\Character\AI\Action\AIWait.cs

- Lines: 110
- Flags: None

- L5 [type] `public class AIWait : AIActionSet`
- L14 [function] `public override float AdjustScore(CharacterActor actor, float baseScore)`
- L48 [function] `public override bool CanStart(CharacterActor actor)`
- L53 [function] `public override void Execute(CharacterActor actor)`
- L96 [function] `private static bool HasOffDutyVisitCandidate(CharacterActor actor, GridPathSearchResult searchResult)`

### Assets\Scripts\Character\AI\Action\AIWork.cs

- Lines: 147
- Flags: None

- L5 [type] `public class AIWork : AIActionSet`
- L16 [function] `public override float AdjustScore(CharacterActor actor, float baseScore)`
- L36 [function] `public override bool CanStart(CharacterActor actor)`
- L49 [function] `public override bool CanContinue(CharacterActor actor, AIAction runningAction, out string stopReason)`
- L57 [function] `public override bool CanInterrupt(CharacterActor actor, AIAction runningAction, out string interruptReason)`
- L65 [function] `public override void Execute(CharacterActor actor)`
- L82 [function] `public override void OnStop(CharacterActor actor, AIAction runningAction, string reason)`
- L113 [function] `public override void RefreshDestinationReservation(CharacterActor actor, BuildableObject destination)`
- L118 [function] `public override void ReleaseDestinationReservation(CharacterActor actor, BuildableObject destination)`
- L143 [function] `private bool CanHandleSuppressCommand()`

### Assets\Scripts\Character\AI\AIActionFailure.cs

- Lines: 187
- Flags: None

- L4 [type] `public enum AIActionFailureKind`
- L25 [type] `public struct AIActionFailure`
- L27 [function] `public AIActionFailure(AIActionFailureKind kind, string reason, BuildableObject target = null)`
- L127 [function] `public override string ToString()`
- L132 [function] `private static bool ContainsAny(string value, params string[] patterns)`
- L145 [function] `private static string GetDefaultReason(AIActionFailureKind kind)`
- L169 [type] `public readonly struct AIActionDebugCandidate`

### Assets\Scripts\Character\AI\AIBrain.cs

- Lines: 1294
- Flags: Reflection, DependencyInjection

- L9 [type] `public class AIBrain : CharacterAbility`
- L78 [function] `public override void Initializtion(CharacterSO data)`
- L94 [function] `public void UseOwnerWorkActions()`
- L102 [function] `private void NormalizeConfiguredActions()`
- L125 [function] `private void EnsureVisitorActions()`
- L145 [function] `private void AddRequiredAction<T>(List<AIAction> actions, string resourcePath) where T : AIActionSet`
- L170 [function] `private ICharacterAiActionAssetCatalog RequireActionCatalog()`
- L176 [function] `private ICharacterAiSchedulingService RequireAiSchedulingService()`
- L182 [function] `public FacilityScoringContext RequireFacilityScoringContext()`
- L193 [function] `public IFacilityCandidateCache RequireFacilityCandidateCache()`
- L199 [function] `public ICharacterAiFacilityLookup RequireFacilityLookup()`
- L205 [function] `public ICharacterAiJobGiverCatalog RequireJobGiverCatalog()`
- L211 [function] `public ICharacterAiDecisionPipeline RequireDecisionPipeline()`
- L217 [function] `public bool DecideAction()`
- L392 [function] `private bool DecideActionByScoreThenDestination()`
- L436 [function] `private bool TryFindHighestScoredAction(out AIAction bestCandidate)`
- L460 [function] `private float GetSelectionScore(AIAction action)`
- L485 [function] `public GridPathSearchResult GetPathSearch(CharacterActor actor)`
- L507 [function] `public bool TryGetRuntimeGrid(out Grid resolvedGrid)`
- L512 [function] `public void ClearPathSearchCache()`
- L517 [function] `public void RequestImmediateReplan(bool clearFailures = false)`
- L546 [function] `public void ClearSelectedActionForIdle(string idleLabel)`
- L560 [function] `public void NotifyActionStarted()`
- L572 [function] `public bool ShouldStopCurrentAction(out string stopReason)`
- L593 [function] `public bool ShouldStopCurrentActionForReplan(out string stopReason)`
- L638 [function] `public bool CanContinueCurrentAction(out string status)`
- L688 [function] `public bool StopCurrentActionForReplan(string reason)`
- L720 [function] `private bool TryUseQueuedAction()`
- L806 [function] `private bool CanConsiderAction(AIAction action, out string failureReason)`
- L813 [function] `private bool CanConsiderAction(AIAction action, out AIActionFailure failure)`
- L858 [function] `private bool CanUseAction(AIAction action, out string failureReason)`
- L865 [function] `private bool CanUseAction(AIAction action, out AIActionFailure failure)`
- L883 [function] `private bool IsActionCoolingDown(AIActionSet actionSet)`
- L890 [function] `private void RecordActionFailure(AIActionSet actionSet, string reason)`
- L895 [function] `private void RecordActionFailure(AIActionSet actionSet, AIActionFailure failure)`
- L906 [function] `private void RecordNoActionFailure()`
- L918 [function] `private static string GetActionLabel(AIActionSet actionSet)`
- L926 [function] `private AIActionFailure RefineActionFailure(AIAction action, AIActionFailure failure)`
- L945 [function] `private void RecordCandidateDebug(AIAction action, AIActionFailure failure)`
- L960 [function] `private void RememberCandidateFailure(AIAction action, AIActionFailure failure)`
- L980 [function] `private void ReleaseFinishedActionReservation()`
- L990 [function] `private static bool ShouldCooldownCandidateFailure(AIActionFailureKind kind)`
- L998 [function] `private static int GetFailureDebugPriority(AIActionFailureKind kind)`
- L1016 [function] `public string GetDebugSummary(int candidateCount = 3)`
- L1051 [function] `private static string GetDestinationLabel(BuildableObject destination)`
- L1067 [function] `public int GetDebugHash()`
- L1085 [function] `private void MarkDebugDirty()`
- L1090 [type] `public enum AIActionPlanKind`
- L1099 [type] `public class AIAction`
- L1122 [function] `public void MarkStarted(float time)`
- L1127 [function] `public float CalculateScore(CharacterActor actor)`
- L1168 [function] `public bool SetDestinationWithFailure(CharacterActor actor, out AIActionFailure failure)`
- L1210 [function] `public void RefreshReservation(CharacterActor actor)`
- L1220 [function] `public void ReleaseReservation(CharacterActor actor)`
- L1254 [function] `private bool ResolvePathPlan(CharacterActor actor, BuildableObject destination, out string failureReason)`
- L1261 [function] `private bool ResolvePathPlan(CharacterActor actor, BuildableObject destination, out AIActionFailure failure)`
- L1281 [function] `private static bool IsCharacterAtDestination(CharacterActor actor, BuildableObject destination)`

### Assets\Scripts\Character\AI\AiDirectorContextAggregator.cs

- Lines: 172
- Flags: None

- L8 [type] `public sealed class AiDirectorContextSummary`
- L18 [function] `public string ToPromptText(int maxCharacters)`
- L39 [type] `public static class AiDirectorContextAggregator`
- L61 [function] `private static float AverageCondition(CharacterActor[] actors, CharacterCondition condition)`
- L87 [function] `private static int CountStockShortages(BuildableObject[] facilities)`
- L109 [function] `private static string[] GetTopQueuedFacilities(BuildableObject[] facilities, int limit)`
- L120 [function] `private static string[] GetRepeatedFailureReasons(CharacterActor[] actors, int limit)`
- L147 [function] `private static string[] GetRecentEvents(CharacterActor target, int limit)`
- L161 [function] `private static string GetBuildingLabel(BuildableObject building)`

### Assets\Scripts\Character\AI\AiDirectorContextSceneQuery.cs

- Lines: 37
- Flags: None

- L4 [type] `public readonly struct AiDirectorContextSceneSnapshot`
- L6 [function] `public AiDirectorContextSceneSnapshot(CharacterActor[] actors, BuildableObject[] facilities)`
- L16 [type] `public interface IAiDirectorContextSceneQuery`
- L21 [type] `public sealed class AiDirectorContextSceneQuery : IAiDirectorContextSceneQuery`
- L25 [function] `public AiDirectorContextSceneQuery(IDungeonSceneComponentQuery sceneQuery)`
- L31 [function] `public AiDirectorContextSceneSnapshot Capture()`

### Assets\Scripts\Character\AI\AiDirectorRuntime.cs

- Lines: 610
- Flags: DependencyInjection

- L10 [type] `public sealed class AiDirectorRuntime : SerializedMonoBehaviour`
- L69 [function] `public void SetWarningLogsSuppressedForDebug(bool value)`
- L74 [function] `private void Update()`
- L85 [function] `public void EvaluateOneActor()`
- L109 [function] `public bool ShouldRequestMoodImpulse(CharacterActor actor)`
- L132 [function] `public bool ShouldRequestMacroGoal(CharacterActor actor)`
- L162 [function] `private bool HasUrgentDirectorReason(CharacterActor actor)`
- L172 [function] `private bool HasRepeatedFailureReason(CharacterActor actor)`
- L181 [function] `private bool HasRoutineDirectorReason(CharacterActor actor)`
- L196 [function] `private float GetNextRoutineMacroGoalTime(CharacterActor actor)`
- L208 [function] `private void ScheduleNextRoutineMacroGoal(CharacterActor actor)`
- L218 [function] `private float GetNextMoodImpulseTime(CharacterActor actor)`
- L230 [function] `private void ScheduleNextMoodImpulse(CharacterActor actor)`
- L240 [function] `public bool RequestMoodImpulse(CharacterActor actor)`
- L266 [function] `public bool RequestMacroGoal(CharacterActor actor)`
- L292 [function] `private void OnMacroGoalResult(CharacterActor actor, LocalLlmResult result)`
- L323 [function] `private void OnMoodImpulseResult(CharacterActor actor, LocalLlmResult result)`
- L369 [function] `private bool TryGetLlmRuntime(out ILocalLlmRuntime queue)`
- L386 [function] `private IAiDirectorContextSceneQuery RequireContextSceneQuery()`
- L396 [function] `private ICharacterAiSchedulingService RequireAiSchedulingService()`
- L406 [function] `private ICharacterAiFacilityLookup RequireFacilityLookup()`
- L416 [function] `private string BuildMacroGoalPrompt(CharacterActor actor)`
- L451 [function] `private string BuildMoodImpulsePrompt(CharacterActor actor)`
- L494 [function] `private bool ValidateMoodImpulseTarget(CharacterMoodImpulse impulse, out string error)`
- L521 [function] `private void ApplyMoodImpulseSideEffects(CharacterActor actor, CharacterMoodImpulse impulse)`
- L586 [function] `private static float GetMood(CharacterActor actor)`
- L591 [function] `private static float GetCondition(CharacterActor actor, CharacterCondition condition)`
- L601 [function] `private void LogWarningIfAllowed(string message)`

### Assets\Scripts\Character\AI\CharacterAiBehaviorTasks.cs

- Lines: 708
- Flags: None

- L6 [type] `internal static class CharacterAiBehaviorTaskServices`
- L24 [type] `public sealed class HasCriticalState : Conditional`
- L28 [function] `public override void OnAwake()`
- L33 [function] `public override TaskStatus OnUpdate()`
- L44 [type] `public sealed class HasMacroGoal : Conditional`
- L48 [function] `public override void OnAwake()`
- L53 [function] `public override TaskStatus OnUpdate()`
- L63 [type] `public sealed class HasMacroGoalType : Conditional`
- L68 [function] `public override void OnAwake()`
- L73 [function] `public override TaskStatus OnUpdate()`
- L83 [type] `public sealed class ClearMacroGoal : Action`
- L89 [function] `public override void OnAwake()`
- L94 [function] `public override TaskStatus OnUpdate()`
- L108 [type] `public sealed class RunComplainMacroGoal : Action`
- L112 [function] `public override void OnAwake()`
- L117 [function] `public override TaskStatus OnUpdate()`
- L134 [type] `public sealed class RunAvoidFacilityMacroGoal : Action`
- L138 [function] `public override void OnAwake()`
- L143 [function] `public override TaskStatus OnUpdate()`
- L160 [type] `public sealed class RunExitDungeonMacroGoal : Action`
- L164 [function] `public override void OnAwake()`
- L169 [function] `public override TaskStatus OnUpdate()`
- L186 [type] `public sealed class RunVandalizeMacroGoal : Action`
- L190 [function] `public override void OnAwake()`
- L195 [function] `public override TaskStatus OnUpdate()`
- L212 [type] `public sealed class RunMacroGoalDecision : Action`
- L216 [function] `public override void OnAwake()`
- L221 [function] `public override TaskStatus OnUpdate()`
- L235 [type] `public sealed class RunCriticalState : Action`
- L239 [function] `public override void OnAwake()`
- L244 [function] `public override TaskStatus OnUpdate()`
- L258 [type] `public sealed class HasContinuableCurrentAction : Conditional`
- L262 [function] `public override void OnAwake()`
- L267 [function] `public override TaskStatus OnUpdate()`
- L277 [type] `public sealed class ContinueCurrentAction : Action`
- L281 [function] `public override void OnAwake()`
- L286 [function] `public override TaskStatus OnUpdate()`
- L300 [type] `public sealed class ShouldStopCurrentAction : Conditional`
- L304 [function] `public override void OnAwake()`
- L309 [function] `public override TaskStatus OnUpdate()`
- L319 [type] `public sealed class StopCurrentActionForReplan : Action`
- L323 [function] `public override void OnAwake()`
- L328 [function] `public override TaskStatus OnUpdate()`
- L341 [type] `public abstract class CharacterRoutineGroupBranchBase : UtilitySelector`
- L347 [function] `public override void OnAwake()`
- L352 [function] `public override float GetPriority()`
- L357 [function] `public override float GetUtility()`
- L362 [function] `private float EvaluatePriority()`
- L379 [type] `public sealed class SurvivalNeedsRoutineBranch : CharacterRoutineGroupBranchBase`
- L385 [type] `public sealed class DutyWorkRoutineBranch : CharacterRoutineGroupBranchBase`
- L391 [type] `public sealed class LeisureVisitRoutineBranch : CharacterRoutineGroupBranchBase`
- L397 [type] `public sealed class IdleRoutineBranch : CharacterRoutineGroupBranchBase`
- L402 [type] `public abstract class CharacterJobGiverBranchBase : Sequence`
- L408 [function] `public override void OnAwake()`
- L413 [function] `public override float GetUtility()`
- L443 [function] `private CharacterAiJobGiver ResolveJobGiver()`
- L455 [type] `public sealed class ExitDungeonJobGiverBranch : CharacterJobGiverBranchBase`
- L461 [type] `public sealed class GetFoodJobGiverBranch : CharacterJobGiverBranchBase`
- L467 [type] `public sealed class RestJobGiverBranch : CharacterJobGiverBranchBase`
- L473 [type] `public sealed class ToiletJobGiverBranch : CharacterJobGiverBranchBase`
- L479 [type] `public sealed class HygieneJobGiverBranch : CharacterJobGiverBranchBase`
- L485 [type] `public sealed class WorkJobGiverBranch : CharacterJobGiverBranchBase`
- L491 [type] `public sealed class ShoppingJobGiverBranch : CharacterJobGiverBranchBase`
- L497 [type] `public sealed class LookAroundJobGiverBranch : CharacterJobGiverBranchBase`
- L503 [type] `public sealed class WaitJobGiverBranch : CharacterJobGiverBranchBase`
- L509 [type] `public sealed class AmbientIdleJobGiverBranch : Sequence`
- L514 [function] `public override void OnAwake()`
- L519 [function] `public override float GetUtility()`
- L539 [type] `public abstract class SelectCharacterActionBase : Action`
- L545 [function] `public override void OnAwake()`
- L550 [function] `public override TaskStatus OnUpdate()`
- L567 [function] `protected abstract bool MatchesAction(AIActionSet actionSet);`
- L571 [type] `public sealed class SelectExitDungeonAction : SelectCharacterActionBase`
- L574 [function] `protected override bool MatchesAction(AIActionSet actionSet) => actionSet is AIExitDungeon;`
- L578 [type] `public sealed class SelectEatAction : SelectCharacterActionBase`
- L581 [function] `protected override bool MatchesAction(AIActionSet actionSet) => actionSet is AIEat;`
- L585 [type] `public sealed class SelectRestAction : SelectCharacterActionBase`
- L588 [function] `protected override bool MatchesAction(AIActionSet actionSet) => actionSet is AIRest;`
- L592 [type] `public sealed class SelectToiletAction : SelectCharacterActionBase`
- L595 [function] `protected override bool MatchesAction(AIActionSet actionSet)`
- L603 [type] `public sealed class SelectHygieneAction : SelectCharacterActionBase`
- L606 [function] `protected override bool MatchesAction(AIActionSet actionSet)`
- L614 [type] `public sealed class SelectWorkAction : SelectCharacterActionBase`
- L617 [function] `protected override bool MatchesAction(AIActionSet actionSet) => actionSet is AIWork;`
- L621 [type] `public sealed class SelectShoppingAction : SelectCharacterActionBase`
- L624 [function] `protected override bool MatchesAction(AIActionSet actionSet) => actionSet is AIShopping;`
- L628 [type] `public sealed class SelectLookAroundAction : SelectCharacterActionBase`
- L631 [function] `protected override bool MatchesAction(AIActionSet actionSet) => actionSet is AILookAround;`
- L635 [type] `public sealed class SelectWaitAction : SelectCharacterActionBase`
- L638 [function] `protected override bool MatchesAction(AIActionSet actionSet) => actionSet is AIWait;`
- L642 [type] `public sealed class RunSelectedCharacterAction : Action`
- L646 [function] `public override void OnAwake()`
- L651 [function] `public override TaskStatus OnUpdate()`
- L665 [type] `public sealed class RunIdleBehavior : Action`
- L669 [function] `public override void OnAwake()`
- L674 [function] `public override TaskStatus OnUpdate()`
- L687 [type] `public sealed class EmitContextBubble : Action`
- L692 [function] `public override void OnAwake()`
- L697 [function] `public override TaskStatus OnUpdate()`

### Assets\Scripts\Character\AI\CharacterAiDecisionPipeline.cs

- Lines: 748
- Flags: Reflection

- L5 [type] `public readonly struct CharacterAiDecisionTickResult`
- L25 [type] `public interface ICharacterAiDecisionPipeline`
- L49 [type] `public interface ICharacterAiFacilityLookup`
- L54 [type] `public sealed class CharacterAiFacilityLookup : ICharacterAiFacilityLookup`
- L58 [function] `public CharacterAiFacilityLookup(IDungeonSceneComponentQuery sceneQuery)`
- L64 [function] `public BuildableObject FindFacility(int id, string tag)`
- L79 [type] `public sealed class CharacterAiDecisionPipeline : ICharacterAiDecisionPipeline`
- L81 [function] `public bool HasCriticalState(CharacterActor actor)`
- L90 [function] `public CharacterAiDecisionTickResult RunCritical(CharacterActor actor, CharacterBlackboard blackboard)`
- L106 [function] `public bool HasMacroGoal(CharacterActor actor)`
- L111 [function] `public bool HasContinuableCurrentAction(CharacterActor actor)`
- L118 [function] `public bool ShouldStopCurrentActionForReplan(CharacterActor actor)`
- L125 [function] `public CharacterAiDecisionTickResult ContinueCurrentAction(CharacterActor actor)`
- L154 [function] `public CharacterAiDecisionTickResult StopCurrentActionForReplan(CharacterActor actor)`
- L260 [function] `public CharacterAiDecisionTickResult RunMacroGoalDecision(CharacterActor actor)`
- L321 [function] `public CharacterAiDecisionTickResult RunIdleBehavior(CharacterActor actor, CharacterBlackboard blackboard)`
- L357 [function] `public bool HasMacroGoalType(CharacterActor actor, CharacterMacroGoalType goalType)`
- L450 [function] `public CharacterAiDecisionTickResult ClearContinueMacro(CharacterActor actor)`
- L579 [function] `private static BuildableObject FindFacility(CharacterActor actor, int id, string tag)`
- L584 [function] `public static bool MatchesFacility(BuildableObject building, int id, string tag)`
- L609 [function] `private static ICharacterAiFacilityLookup RequireFacilityLookup(CharacterActor actor)`
- L620 [function] `private static ICharacterAiJobGiverCatalog RequireJobGiverCatalog(CharacterActor actor)`
- L631 [function] `private static bool CanVandalize(BuildableObject target, out string failureReason)`
- L667 [function] `private static string GetBuildingLabel(BuildableObject building)`
- L679 [function] `public static CharacterAiBranch GetBranchForActionSet(AIActionSet actionSet)`
- L696 [function] `public static string GetActionLabel(AIActionSet actionSet)`

### Assets\Scripts\Character\AI\CharacterAiJobGiver.cs

- Lines: 538
- Flags: None

- L4 [type] `public readonly struct CharacterAiActionCandidate`
- L26 [type] `public readonly struct CharacterAiJobCandidate`
- L55 [type] `public abstract class CharacterAiJobGiver`
- L60 [function] `public bool TryEvaluate(CharacterActor actor, out CharacterAiJobCandidate candidate)`
- L103 [function] `public abstract bool MatchesAction(AIActionSet actionSet);`
- L105 [function] `protected abstract float GetDomainScore(CharacterActor actor, out string reason);`
- L107 [function] `protected virtual float CombineUtility(float domainScore, float actionScore)`
- L112 [function] `public static float Need(CharacterActor actor, CharacterCondition condition)`
- L125 [function] `public static float StatRatio(CharacterActor actor, CharacterCondition condition)`
- L138 [function] `protected static float InterestMultiplier(CharacterActor actor, AIActionSet actionSet)`
- L143 [function] `private CharacterAiJobCandidate CreateRejected(float domainScore, string reason)`
- L155 [type] `public static class CharacterAiRoutinePriority`
- L189 [function] `private static float GetSurvivalPriority(CharacterActor actor, out string reason)`
- L227 [function] `private static float GetDutyPriority(CharacterActor actor, out string reason)`
- L248 [function] `private static float GetLeisurePriority(CharacterActor actor, out string reason)`
- L271 [function] `private static float GetIdlePriority(CharacterActor actor, out string reason)`
- L277 [function] `private static float ReturnNoPriority(out string reason)`
- L283 [function] `private static float GetSurvivalPressure(CharacterActor actor)`
- L294 [function] `private static bool CanUseLeisure(CharacterActor actor)`
- L309 [function] `private static bool ShouldExitDungeon(CharacterActor actor)`
- L317 [type] `public sealed class ExitDungeonJobGiver : CharacterAiJobGiver`
- L321 [function] `public override bool MatchesAction(AIActionSet actionSet) => actionSet is AIExitDungeon;`
- L323 [function] `protected override float GetDomainScore(CharacterActor actor, out string reason)`
- L330 [type] `public sealed class GetFoodJobGiver : CharacterAiJobGiver`
- L334 [function] `public override bool MatchesAction(AIActionSet actionSet) => actionSet is AIEat;`
- L336 [function] `protected override float GetDomainScore(CharacterActor actor, out string reason)`
- L344 [type] `public sealed class RestJobGiver : CharacterAiJobGiver`
- L348 [function] `public override bool MatchesAction(AIActionSet actionSet) => actionSet is AIRest;`
- L350 [function] `protected override float GetDomainScore(CharacterActor actor, out string reason)`
- L360 [type] `public sealed class ToiletJobGiver : CharacterAiJobGiver`
- L365 [function] `public override bool MatchesAction(AIActionSet actionSet)`
- L371 [function] `protected override float GetDomainScore(CharacterActor actor, out string reason)`
- L379 [type] `public sealed class HygieneJobGiver : CharacterAiJobGiver`
- L384 [function] `public override bool MatchesAction(AIActionSet actionSet)`
- L390 [function] `protected override float GetDomainScore(CharacterActor actor, out string reason)`
- L398 [type] `public sealed class WorkJobGiver : CharacterAiJobGiver`
- L402 [function] `public override bool MatchesAction(AIActionSet actionSet) => actionSet is AIWork;`
- L404 [function] `protected override float GetDomainScore(CharacterActor actor, out string reason)`
- L431 [type] `public sealed class ShoppingJobGiver : CharacterAiJobGiver`
- L435 [function] `public override bool MatchesAction(AIActionSet actionSet) => actionSet is AIShopping;`
- L437 [function] `protected override float GetDomainScore(CharacterActor actor, out string reason)`
- L445 [type] `public sealed class LookAroundJobGiver : CharacterAiJobGiver`
- L449 [function] `public override bool MatchesAction(AIActionSet actionSet) => actionSet is AILookAround;`
- L451 [function] `protected override float GetDomainScore(CharacterActor actor, out string reason)`
- L467 [type] `public sealed class WaitJobGiver : CharacterAiJobGiver`
- L471 [function] `public override bool MatchesAction(AIActionSet actionSet) => actionSet is AIWait;`
- L473 [function] `protected override float GetDomainScore(CharacterActor actor, out string reason)`
- L496 [type] `public interface ICharacterAiJobGiverCatalog`
- L510 [type] `public sealed class CharacterAiJobGiverCatalog : ICharacterAiJobGiverCatalog`
- L522 [function] `public CharacterAiJobGiver Get(CharacterAiBranch branch)`

### Assets\Scripts\Character\AI\CharacterAiPersonality.cs

- Lines: 65
- Flags: None

- L5 [type] `public class CharacterAiPersonality`
- L13 [function] `public float GetActionMultiplier(AIActionSet actionSet)`
- L41 [function] `private static float ClampMultiplier(float value)`
- L47 [type] `public static class CharacterAiPersonalityUtility`
- L49 [function] `public static float GetActionScoreMultiplier(CharacterActor actor, AIActionSet actionSet)`

### Assets\Scripts\Character\AI\CharacterAiScheduler.cs

- Lines: 561
- Flags: SceneMutation, DependencyInjection

- L9 [type] `public sealed class CharacterAiScheduler : MonoBehaviour`
- L61 [function] `private void Awake()`
- L66 [function] `private void OnEnable()`
- L91 [function] `private void Start()`
- L96 [function] `private void Update()`
- L101 [function] `public void RegisterActor(CharacterActor actor)`
- L111 [function] `public void UnregisterActor(CharacterActor actor)`
- L121 [function] `public void RequestImmediateDecisionFor(CharacterActor actor)`
- L132 [function] `public bool TryConsumePathSearchBudget()`
- L142 [function] `public bool ShouldShowCharacterFeedbackFor(CharacterActor actor)`
- L152 [function] `public int GetMovementFrameStrideFor(CharacterActor actor)`
- L165 [function] `public void RunManualTick(float deltaTime)`
- L171 [function] `public void ClearRegistrationsForDebug()`
- L182 [function] `public void ResetPathSearchBudgetForDebugInstance()`
- L190 [function] `private void ProcessAiBudget(float now)`
- L267 [function] `private void RefreshBehaviorDesignerVisualsForEditor()`
- L288 [function] `private void RegisterExistingCharacters()`
- L296 [function] `private void RegisterExistingCharactersIfInjected()`
- L304 [function] `private IDungeonSceneComponentQuery RequireSceneQuery()`
- L314 [function] `private void RegisterInternal(CharacterActor actor)`
- L327 [function] `private bool TryRunScheduledDecision(CharacterActor actor)`
- L373 [function] `private static void ConfigureBehaviorManagerForManualTick()`
- L387 [function] `private BehaviorTree ConfigureCharacterBehaviorTree(CharacterActor actor)`
- L392 [function] `private void UnregisterInternal(CharacterActor actor)`
- L404 [function] `private void RemoveAt(int index)`
- L420 [function] `private float GetDecisionInterval(CharacterActor actor)`
- L432 [function] `private bool IsHighDetailCharacter(CharacterActor actor)`
- L458 [function] `private bool TryConsumePathSearchBudgetInternal()`
- L470 [function] `private void BeginPathBudgetWindow()`
- L478 [function] `private void ResetPathBudgetIfNeeded()`
- L489 [function] `private int GetDecisionBudgetForFrame()`
- L495 [function] `private int GetPathSearchBudgetForFrame()`
- L501 [function] `private void EnsureAdaptiveBudgetsInitialized()`
- L509 [function] `private void ResetAdaptiveBudgets()`
- L515 [function] `private void UpdateAdaptiveBudgets()`
- L550 [function] `private IMainCameraProvider RequireMainCameraProvider()`
- L556 [function] `private ICharacterBehaviorTreeRuntimeConfigurator RequireBehaviorTreeConfigurator()`

### Assets\Scripts\Character\AI\CharacterBehaviorTreeRuntimeConfigurator.cs

- Lines: 46
- Flags: RuntimeObjectCreation, SceneMutation

- L5 [type] `public interface ICharacterBehaviorTreeRuntimeConfigurator`
- L10 [type] `public sealed class CharacterBehaviorTreeRuntimeConfigurator : ICharacterBehaviorTreeRuntimeConfigurator`
- L12 [function] `public BehaviorTree Configure(CharacterActor actor, ExternalBehaviorTree externalBehavior)`

### Assets\Scripts\Character\AI\CharacterBlackboard.cs

- Lines: 748
- Flags: Reflection

- L7 [type] `public enum CharacterAiBranch`
- L29 [type] `public enum CharacterAiInterruptReason`
- L43 [type] `public enum CharacterMoodImpulseType`
- L62 [type] `public enum CharacterMacroGoalType`
- L77 [type] `public sealed class CharacterMacroGoal`
- L86 [function] `public bool IsActive(float now)`
- L92 [function] `public bool IsEquivalentTo(CharacterMacroGoal other)`
- L106 [type] `public sealed class CharacterMoodImpulse`
- L116 [function] `public bool IsActive(float now)`
- L123 [function] `public bool IsEquivalentTo(CharacterMoodImpulse other)`
- L139 [type] `public sealed class CharacterBlackboard : SerializedMonoBehaviour`
- L200 [function] `private void Awake()`
- L205 [function] `public void Bind(CharacterActor owner)`
- L219 [function] `public void BeginDecisionTrace(int tick)`
- L226 [function] `public void RecordBtStatus(CharacterAiBranch branch, string taskName, string status)`
- L234 [function] `public void SetIntent(CharacterAiBranch branch, string intent, string taskName = "", string status = "")`
- L245 [function] `public void RecordJobGiverUtility(CharacterAiBranch branch, float utility, string detail)`
- L259 [function] `public void RecordSelectedJobGiverUtility(CharacterAiJobCandidate candidate)`
- L265 [function] `public void RecordSelectedUtilitySummary(string summary)`
- L271 [function] `public void RecordRoutineGroupPriority(CharacterAiBranch branch, float priority, string detail)`
- L285 [function] `public void ClearJobGiverCandidateCache()`
- L291 [function] `public void CacheJobGiverCandidate(CharacterAiJobCandidate candidate)`
- L302 [function] `public void RemoveJobGiverCandidateCache(CharacterAiBranch branch)`
- L322 [function] `public void Commit(AIAction action, string intent)`
- L335 [function] `public void RefreshCommitment(AIAction action)`
- L348 [function] `public bool TryGetCommitmentBonus(AIAction action, out float bonus)`
- L376 [function] `public bool CanBreakCommitment(CharacterAiInterruptReason reason)`
- L389 [function] `public void ClearCommitment(CharacterAiInterruptReason reason, string detail)`
- L405 [function] `public bool IsFacilityCoolingDown(BuildableObject building, out float remainingSeconds)`
- L423 [function] `public void PutFacilityOnCooldown(BuildableObject building, string reason)`
- L437 [function] `public void ReportActionFailure(AIActionSet actionSet, AIActionFailure failure)`
- L479 [function] `public bool HasActiveMacroGoal()`
- L495 [function] `public bool HasActiveMoodImpulse()`
- L517 [function] `public void SetMacroGoal(CharacterMacroGoal goal)`
- L540 [function] `public void ClearMacroGoal(string reason)`
- L552 [function] `public void SetMoodImpulse(CharacterMoodImpulse impulse)`
- L582 [function] `public void ClearMoodImpulse(string reason)`
- L595 [function] `public int GetRecentFailureCount(AIActionFailureKind kind)`
- L600 [function] `public string GetDebugSummary()`
- L622 [function] `private string BuildDecisionRouteSummary()`
- L653 [function] `private void AppendDecisionTrace(string entry)`
- L668 [function] `private static string TrimTrace(string value)`
- L681 [function] `private void PruneFacilityCooldowns()`
- L711 [function] `private static bool ShouldCooldownFacility(AIActionFailureKind kind)`
- L720 [function] `private static bool IsDestinationInvalid(BuildableObject destination)`
- L725 [function] `private static string GetActionLabel(AIActionSet actionSet)`
- L737 [function] `private static string GetBuildingLabel(BuildableObject building)`

### Assets\Scripts\Character\AI\CharacterDialogueBubbleFactory.cs

- Lines: 46
- Flags: RuntimeObjectCreation, SceneMutation, DependencyInjection

- L6 [type] `public interface ICharacterDialogueBubbleFactory`
- L11 [type] `public sealed class CharacterDialogueBubbleFactory : ICharacterDialogueBubbleFactory`
- L16 [function] `public CharacterDialogueBubbleFactory(ITmpKoreanFontService tmpKoreanFontService)`
- L22 [function] `public TextMeshPro Create(Transform parent)`

### Assets\Scripts\Character\AI\CharacterDialogueRuntime.cs

- Lines: 283
- Flags: SceneMutation, DependencyInjection

- L9 [type] `public sealed class CharacterDialogueRuntime : MonoBehaviour`
- L46 [function] `private void Awake()`
- L53 [function] `private void OnEnable()`
- L62 [function] `private void OnDisable()`
- L75 [function] `private void LateUpdate()`
- L91 [function] `public void ShowLine(string line)`
- L106 [function] `private void OnLogAdded(CharacterLogEntry entry)`
- L132 [function] `private void OnBubbleResult(LocalLlmResult result)`
- L159 [function] `private bool TryGetLlmRuntime(out ILocalLlmRuntime queue)`
- L176 [function] `private ICharacterAiSchedulingService RequireAiSchedulingService()`
- L182 [function] `private void HideLine()`
- L193 [function] `private string BuildPrompt(CharacterLogEntry entry)`
- L208 [function] `private static bool ShouldRequestBubble(CharacterLogEntry entry)`
- L237 [function] `private void EnsureText()`
- L247 [function] `private Vector3 GetLocalOffset()`
- L259 [function] `private ICharacterDialogueBubbleFactory RequireBubbleFactory()`
- L266 [function] `private static bool ContainsAny(string value, params string[] patterns)`

### Assets\Scripts\Character\AI\CharacterMoodImpulseUtility.cs

- Lines: 380
- Flags: None

- L4 [type] `public static class CharacterMoodImpulseUtility`
- L9 [function] `public static float GetMood01(CharacterActor actor)`
- L207 [function] `public static CharacterAiBranch GetBranchForActionSet(AIActionSet actionSet)`
- L224 [function] `public static bool MatchesBranch(CharacterMoodImpulseType impulse, CharacterAiBranch branch)`
- L265 [function] `public static string AppendReason(string reason, string suffix)`
- L277 [function] `private static bool TryGetActiveImpulse(CharacterActor actor, out CharacterMoodImpulse impulse)`
- L292 [function] `private static bool IsOnDuty(CharacterActor actor)`
- L298 [function] `private static bool IsSurvivalImpulse(CharacterMoodImpulseType impulse)`
- L307 [function] `private static bool IsLeisureImpulse(CharacterMoodImpulseType impulse)`
- L317 [function] `private static bool IsTemperamentalImpulse(CharacterMoodImpulseType impulse)`
- L329 [function] `private static bool MatchesFacilityTarget(BuildableObject facility, CharacterMoodImpulse impulse)`
- L354 [function] `private static string AppendImpulseReason(string reason, CharacterMoodImpulse impulse)`
- L359 [function] `private static string GetFacilityLabel(BuildableObject facility)`
- L371 [function] `private static float GetCondition(CharacterActor actor, CharacterCondition condition)`

### Assets\Scripts\Character\AI\CharacterSocialMemory.cs

- Lines: 452
- Flags: None

- L6 [type] `public enum SocialRumorType`
- L15 [type] `public enum SocialRumorTargetType`
- L23 [type] `public sealed class SocialRumor`
- L46 [function] `public SocialRumor Clone()`
- L53 [type] `public sealed class SocialMemoryFloat`
- L58 [function] `public SocialMemoryFloat(string key, float value)`
- L67 [type] `public sealed class CharacterSocialMemory : SerializedMonoBehaviour`
- L83 [function] `private void Awake()`
- L88 [function] `public void Bind(CharacterActor owner)`
- L93 [function] `public void HearRumor(SocialRumor rumor, CharacterActor speaker)`
- L131 [function] `public float GetFacilitySentiment(BuildableObject building)`
- L155 [function] `public float GetRelationshipSentiment(CharacterActor target)`
- L174 [function] `public float GetSourceTrust(CharacterActor source)`
- L185 [function] `private float GetSourceTrustScore(CharacterActor speaker, SocialRumor rumor)`
- L211 [function] `private void RememberRumor(SocialRumor rumor)`
- L221 [function] `private void PruneExpiredRumors()`
- L239 [function] `private void RebuildSentimentMaps()`
- L269 [function] `private void SyncDebugLists()`
- L276 [function] `private static void Blend(Dictionary<string, float> map, string key, float value, float blend)`
- L287 [function] `private static float GetDictionaryValue(Dictionary<string, float> map, string key, float fallback)`
- L294 [function] `private static void SyncDebugList(Dictionary<string, float> source, List<SocialMemoryFloat> target)`
- L304 [type] `public static class SocialRumorUtility`
- L306 [function] `public static IEnumerable<string> GetFacilityKeys(SocialRumor rumor)`
- L324 [function] `public static IEnumerable<string> GetCharacterKeys(SocialRumor rumor)`
- L342 [function] `public static string GetActorKey(CharacterActor actor)`
- L353 [function] `public static string GetActorNameKey(CharacterActor actor)`
- L363 [function] `public static bool MatchesFacilityKey(BuildableObject building, string key)`
- L384 [function] `public static bool MatchesFacilityTag(BuildableObject building, string tag)`
- L400 [function] `public static string GetActorLabel(CharacterActor actor)`
- L410 [function] `public static string GetFacilityLabel(BuildableObject building)`
- L425 [function] `public static string GetFacilityTag(BuildableObject building)`
- L440 [function] `private static bool ContainsNormalized(string source, string normalizedNeedle)`
- L446 [function] `private static string Normalize(string value)`

### Assets\Scripts\Character\AI\CharacterSocialMemoryFactory.cs

- Lines: 36
- Flags: RuntimeObjectCreation, SceneMutation, DependencyInjection

- L5 [type] `public interface ICharacterSocialMemoryFactory`
- L10 [type] `public sealed class CharacterSocialMemoryFactory : ICharacterSocialMemoryFactory`
- L14 [function] `public CharacterSocialMemoryFactory(IObjectResolver objectResolver)`
- L20 [function] `public CharacterSocialMemory GetOrAdd(CharacterActor actor)`

### Assets\Scripts\Character\AI\CharacterSocialMemoryService.cs

- Lines: 22
- Flags: None

- L3 [type] `public interface ICharacterSocialMemoryService`
- L8 [type] `public sealed class CharacterSocialMemoryService : ICharacterSocialMemoryService`
- L12 [function] `public CharacterSocialMemoryService(ICharacterSocialMemoryFactory memoryFactory)`
- L18 [function] `public CharacterSocialMemory GetOrAdd(CharacterActor actor)`

### Assets\Scripts\Character\AI\Consideration\Consideration.cs

- Lines: 11
- Flags: None

- L7 [type] `public abstract class Consideration : SerializedScriptableObject`
- L10 [function] `public abstract float ScoreConsideration(CharacterActor actor);`

### Assets\Scripts\Character\AI\Consideration\ConsiderationCanLookAround.cs

- Lines: 10
- Flags: None

- L4 [type] `public class ConsiderationCanLookAround : Consideration`
- L6 [function] `public override float ScoreConsideration(CharacterActor actor)`

### Assets\Scripts\Character\AI\Consideration\ConsiderationFacilityNeed.cs

- Lines: 38
- Flags: None

- L4 [type] `public class ConsiderationFacilityNeed : Consideration`
- L15 [function] `public override float ScoreConsideration(CharacterActor actor)`

### Assets\Scripts\Character\AI\Consideration\ConsiderationIsVisitable.cs

- Lines: 43
- Flags: None

- L3 [type] `public class ConsiderationIsVisitable : Consideration`
- L7 [function] `public override float ScoreConsideration(CharacterActor actor)`
- L34 [function] `private static FacilityRole ConvertLegacyType(Shop.Type type)`

### Assets\Scripts\Character\AI\Consideration\ConsiderationRandom.cs

- Lines: 14
- Flags: None

- L6 [type] `public class ConsiderationRandom : Consideration`
- L10 [function] `public override float ScoreConsideration(CharacterActor actor)`

### Assets\Scripts\Character\AI\Consideration\ConsiderationShoppingCount.cs

- Lines: 19
- Flags: None

- L6 [type] `public class ConsiderationShoppingCount : Consideration`
- L8 [function] `public override float ScoreConsideration(CharacterActor actor)`

### Assets\Scripts\Character\AI\Consideration\ConsiderationShouldExitDungeon.cs

- Lines: 12
- Flags: None

- L4 [type] `public class ConsiderationShouldExitDungeon : Consideration`
- L6 [function] `public override float ScoreConsideration(CharacterActor actor)`

### Assets\Scripts\Character\AI\Consideration\ConsiderationStat.cs

- Lines: 22
- Flags: None

- L6 [type] `public class ConsiderationStat : Consideration`
- L10 [function] `public override float ScoreConsideration(CharacterActor actor)`

### Assets\Scripts\Character\AI\Consideration\ConsiderationWorkNeed.cs

- Lines: 33
- Flags: None

- L4 [type] `public class ConsiderationWorkNeed : Consideration`
- L15 [function] `public override float ScoreConsideration(CharacterActor actor)`

### Assets\Scripts\Character\AI\CustomerPersonaRuntime.cs

- Lines: 323
- Flags: DependencyInjection

- L8 [type] `public sealed class CustomerPersonaData`
- L21 [function] `public void Clamp()`
- L33 [function] `private static float ClampMultiplier(float value)`
- L41 [type] `public sealed class CustomerPersonaRuntime : SerializedMonoBehaviour`
- L58 [function] `public void ConstructCustomerPersonaRuntime(ILocalLlmRuntimeProvider llmRuntimeProvider)`
- L64 [function] `private void Awake()`
- L69 [function] `public void Bind(CharacterActor owner)`
- L75 [function] `public bool RequestPersonaIfNeeded(bool logIfMissingQueue = true)`
- L116 [function] `public void ApplyGeneratedPersona(CustomerPersonaData generatedPersona)`
- L131 [function] `private bool TryGetLlmRuntime(bool logIfMissingQueue, out ILocalLlmRuntime queue)`
- L152 [function] `public float GetActionMultiplier(AIActionSet actionSet)`
- L180 [function] `public float GetConditionCurveMultiplier(CharacterCondition condition)`
- L192 [function] `public float GetFacilityTagPreference(BuildableObject building)`
- L224 [function] `private static bool ValidatePersona(CustomerPersonaData candidate, out string error)`
- L260 [function] `private static bool IsMultiplierValid(float value, string fieldName, out string error)`
- L272 [function] `private void OnPersonaResult(LocalLlmResult result)`
- L292 [function] `private static string BuildPersonaPrompt(CharacterActor actor)`
- L314 [function] `private static float GetCondition(CharacterActor actor, CharacterCondition condition)`

### Assets\Scripts\Character\AI\FacilityCandidateCache.cs

- Lines: 125
- Flags: None

- L4 [type] `public interface IFacilityCandidateCache`
- L11 [type] `public sealed class FacilityCandidateCacheStore : IFacilityCandidateCache`
- L13 [type] `private sealed class GridFacilityCache`
- L25 [function] `public IReadOnlyList<BuildableObject> GetCandidates(Grid grid, FacilityRole role)`
- L53 [function] `public void MarkDynamicStateDirty()`
- L61 [function] `public void Clear()`
- L67 [function] `private GridFacilityCache GetCache(Grid grid)`
- L109 [function] `private static bool IsSingleRole(FacilityRole role)`
- L115 [function] `private static IEnumerable<FacilityRole> GetSingleRoles(FacilityRole roles)`

### Assets\Scripts\Character\AI\FacilityCandidateScorer.cs

- Lines: 499
- Flags: None

- L4 [type] `public static class FacilityCandidateScorer`
- L238 [function] `public static float GetNeedScore(CharacterActor actor, FacilityRole role)`
- L295 [function] `private static IFacilityCandidateCache RequireFacilityCandidateCache(CharacterActor actor)`
- L351 [function] `private static bool HasMultipleRoles(FacilityRole role)`
- L357 [function] `private static float GetLowStatNeed(CharacterActor actor, CharacterCondition condition)`
- L383 [function] `private static float GetSpeciesTagPreferenceScore(CharacterActor actor, BuildableObject building)`
- L423 [function] `private static float GetStockScore(BuildableObject building)`
- L439 [function] `private static float GetAffordabilityScore(CharacterActor actor, BuildableObject building)`
- L454 [function] `private static float GetCrowdScore(CharacterActor actor, BuildableObject building)`
- L466 [function] `private static float GetDistanceScore(BuildableObject building, GridPathSearchResult searchResult)`
- L482 [function] `private static float GetNoveltyScore(CharacterActor actor, BuildableObject building)`

### Assets\Scripts\Character\AI\FacilityScoringContext.cs

- Lines: 86
- Flags: None

- L3 [type] `public readonly struct FacilityScoringContext`
- L39 [function] `public static FacilityScoringContext RequireFromActor(CharacterActor actor)`
- L50 [function] `public float GetReputationBias(CharacterActor actor, BuildableObject building)`
- L75 [function] `public float GetRoomUtilityScore(BuildableObject building, FacilityRole role)`
- L80 [function] `private IRoomFacilityPolicy RequireRoomFacilityPolicy()`

### Assets\Scripts\Character\AI\Idle\IdleBehavior.cs

- Lines: 135
- Flags: Reflection

- L1 [type] `public interface IIdleBehavior`
- L8 [type] `public sealed class StaffWanderIdleBehavior : IIdleBehavior`
- L12 [function] `public bool CanRun(CharacterActor actor)`
- L18 [function] `public bool TryRun(CharacterActor actor, float duration, out string failureReason)`
- L38 [type] `public sealed class StaticWaitIdleBehavior : IIdleBehavior`
- L42 [function] `public bool CanRun(CharacterActor actor)`
- L47 [function] `public bool TryRun(CharacterActor actor, float duration, out string failureReason)`
- L61 [type] `public static class IdleBehaviorRunner`
- L115 [function] `public static string GetSelectedBehaviorTypeNameForDebug(CharacterActor actor, bool allowMovement)`
- L121 [function] `private static IIdleBehavior SelectBehavior(CharacterActor actor, bool allowMovement)`

### Assets\Scripts\Character\AI\LlmJsonResponseParser.cs

- Lines: 654
- Flags: None

- L5 [type] `public interface ILlmJsonPayload`
- L10 [type] `public static class LlmJsonResponseParser`
- L16 [function] `public static bool TryParse<T>(string response, out T payload, out string error)`
- L75 [function] `public static bool TryExtractJsonObject(string response, out string json, out string error)`
- L100 [function] `private static string StripMarkdownFence(string value)`
- L124 [type] `public sealed class MoodImpulseJsonDto : ILlmJsonPayload`
- L136 [function] `public bool Validate(out string error)`
- L181 [function] `public static bool ValidateRawJson(string json, out string error)`
- L205 [function] `public CharacterMoodImpulse ToRuntimeImpulse(string source)`
- L220 [function] `private static bool HasRawNumber(string json, string fieldName)`
- L226 [function] `private static bool HasRawInteger(string json, string fieldName)`
- L234 [type] `public sealed class CustomerPersonaJsonDto : ILlmJsonPayload`
- L249 [function] `public bool Validate(out string error)`
- L279 [function] `public CustomerPersonaData ToRuntimeData()`
- L296 [function] `public static bool ValidateRawJson(string json, out string error)`
- L329 [function] `private static bool ValidateMultiplier(float value, string fieldName, out string error)`
- L341 [function] `private static bool HasRawNumber(string json, string fieldName)`
- L349 [type] `public sealed class MacroGoalJsonDto : ILlmJsonPayload`
- L360 [function] `public bool Validate(out string error)`
- L400 [function] `public static bool ValidateRawJson(string json, out string error)`
- L418 [function] `public CharacterMacroGoal ToRuntimeGoal(string source)`
- L432 [function] `private static bool HasRawNumber(string json, string fieldName)`
- L438 [function] `private static bool HasRawInteger(string json, string fieldName)`
- L446 [type] `public sealed class SocialRumorJsonDto : ILlmJsonPayload`
- L463 [function] `public bool Validate(out string error)`
- L574 [function] `public static bool ValidateRawJson(string json, out string error)`
- L596 [function] `public SocialRumor ToRuntimeRumor(string source, CharacterActor speaker)`
- L619 [function] `private static bool HasRawNumber(string json, string fieldName)`
- L625 [function] `private static bool HasRawInteger(string json, string fieldName)`
- L633 [type] `public sealed class BubbleLineJsonDto : ILlmJsonPayload`
- L637 [function] `public bool Validate(out string error)`

### Assets\Scripts\Character\AI\LocalLlmRequestQueue.cs

- Lines: 588
- Flags: SceneMutation

- L9 [type] `public enum LocalLlmRequestType`
- L19 [type] `public enum LocalLlmRequestStatus`
- L27 [type] `public interface ILocalLlmRuntime`
- L37 [type] `public readonly struct LocalLlmResult`
- L58 [type] `internal sealed class LocalLlmQueuedRequest`
- L70 [type] `public sealed class LocalLlmRequestQueue : SerializedMonoBehaviour, ILocalLlmRuntime`
- L103 [function] `public void ConfigureBubblePolicyForDebug(float timeoutSeconds, float maxQueueAgeSeconds)`
- L129 [function] `public void ClearForDebug()`
- L138 [function] `public void AbortAllForDebug()`
- L149 [function] `public void SetWarningLogsSuppressedForDebug(bool value)`
- L150 [function] `private void Awake()`
- L162 [function] `private void OnDestroy()`
- L170 [function] `private void Update()`
- L186 [function] `public bool EnqueuePersona(string prompt, Action<LocalLlmResult> callback)`
- L191 [function] `public bool EnqueueMacroGoal(string prompt, Action<LocalLlmResult> callback)`
- L196 [function] `public bool EnqueueMoodImpulse(string prompt, Action<LocalLlmResult> callback)`
- L201 [function] `public bool EnqueueSocialRumor(string prompt, Action<LocalLlmResult> callback)`
- L206 [function] `public bool EnqueueFacilityEvolution(string prompt, Action<LocalLlmResult> callback)`
- L211 [function] `public bool EnqueueBubbleLine(string prompt, string originalText, Action<LocalLlmResult> callback)`
- L216 [function] `public bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> callback)`
- L221 [function] `public bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> callback)`
- L226 [function] `public bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> callback)`
- L231 [function] `public bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> callback)`
- L236 [function] `public bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> callback)`
- L241 [function] `public bool GenerateBubbleLineAsync(string prompt, string originalText, Action<LocalLlmResult> callback)`
- L298 [function] `private IEnumerator ProcessRequest(LocalLlmQueuedRequest request)`
- L371 [function] `private UnityWebRequest BuildRequest(string prompt)`
- L401 [function] `private void Complete(LocalLlmQueuedRequest request, LocalLlmResult result)`
- L417 [function] `private void LogWarningIfAllowed(string message)`
- L427 [function] `private bool TryDropLowestPriorityBubble()`
- L459 [function] `private void DropExpiredBubbleRequests()`
- L485 [function] `private int FindNextRequestIndex()`
- L501 [function] `private static int GetPriority(LocalLlmRequestType type)`
- L515 [function] `private static bool TryExtractContent(string responseJson, out string content, out string error)`
- L559 [type] `private sealed class OpenAiChatRequest`
- L568 [type] `private sealed class OpenAiChatMessage`
- L575 [type] `private sealed class OpenAiResponseFormat`
- L581 [type] `private sealed class OpenAiChatResponse`
- L587 [type] `private sealed class OpenAiChoice`

### Assets\Scripts\Character\AI\SocialReputationRuntime.cs

- Lines: 1001
- Flags: SceneMutation, DependencyInjection

- L12 [type] `public sealed class SocialReputationRuntime : SerializedMonoBehaviour`
- L75 [function] `private void Awake()`
- L87 [function] `private void OnEnable()`
- L92 [function] `private void Start()`
- L97 [function] `private void OnDisable()`
- L106 [function] `private void Update()`
- L117 [function] `public bool RequestSocialInterpretation(CharacterActor speaker, CharacterLogEntry entry)`
- L222 [function] `public bool ApplyRumor(SocialRumor rumor, CharacterActor speaker)`
- L242 [function] `private bool TryGetLlmRuntime(out ILocalLlmRuntime queue)`
- L263 [function] `private IDungeonSceneComponentQuery RequireSceneQuery()`
- L273 [function] `public float GetFacilityUtilityBias(CharacterActor actor, BuildableObject building)`
- L284 [function] `public float GetCombinedFacilitySentiment(CharacterActor actor, BuildableObject building)`
- L297 [function] `public float GetGlobalFacilitySentiment(BuildableObject building)`
- L321 [function] `public void ClearForDebug()`
- L339 [function] `public void SetActorLogRequestsSuppressedForDebug(bool value)`
- L344 [function] `public void SetWarningLogsSuppressedForDebug(bool value)`
- L349 [function] `public void RestrictActorLogRequestsForDebug(CharacterActor actor)`
- L354 [function] `public void RegisterActorForDebug(CharacterActor actor)`
- L359 [function] `private void RegisterExistingActors()`
- L368 [function] `private void RegisterExistingActorsIfInjected()`
- L376 [function] `private void RegisterActor(CharacterActor actor)`
- L395 [function] `private void OnActorLogAdded(CharacterActor actor, CharacterLogEntry entry)`
- L402 [function] `private void OnSocialRumorResult(CharacterActor speaker, LocalLlmResult result)`
- L466 [function] `private void LogSocialWarningIfNeeded(bool logWarnings, string message)`
- L474 [function] `private void LogWarningIfAllowed(string message)`
- L616 [function] `private IEnumerable<BuildableObject> FindNearbyFacilities(CharacterActor speaker)`
- L630 [function] `private static void AppendFacilityLine(StringBuilder builder, BuildableObject building)`
- L637 [function] `private BuildableObject ResolveFacilityFromLog(CharacterLogEntry entry)`
- L649 [function] `private static bool TryExtractFacilityId(string value, out int facilityId)`
- L666 [function] `private static float InferSocialEventSentiment(CharacterLogEntry entry)`
- L698 [function] `private static bool ShouldInterpretSocialEvent(CharacterLogEntry entry)`
- L737 [function] `private int SpreadRumor(SocialRumor rumor, CharacterActor speaker)`
- L766 [function] `private bool CanHearRumor(CharacterActor speaker, CharacterActor listener)`
- L782 [function] `private void ApplyGlobalFacilityReputation(SocialRumor rumor)`
- L789 [function] `private void RebuildGlobalFacilityReputation()`
- L805 [function] `private void ApplyGlobalFacilityReputationEntry(SocialRumor rumor)`
- L822 [function] `private void PruneExpiredGlobalRumors()`
- L840 [function] `private CharacterSocialMemory EnsureMemory(CharacterActor actor)`
- L850 [function] `private ICharacterSocialMemoryService RequireSocialMemoryService()`
- L860 [function] `private static void FillSourceIfMissing(SocialRumor rumor, CharacterActor speaker)`
- L878 [function] `private static bool HasValidTarget(SocialRumor rumor)`
- L898 [function] `private static bool RumorTargetsExpectedFacility(SocialRumor rumor, BuildableObject expectedFacility)`
- L918 [function] `private bool RumorTargetsKnownFacility(SocialRumor rumor)`
- L947 [function] `private static bool SentimentMatchesExpected(float actualSentiment, float expectedSentiment)`
- L957 [function] `private float GetNextRequestTime(CharacterActor actor)`
- L964 [function] `private void UnsubscribeAllActors()`
- L984 [function] `private void SyncDebugList()`
- L993 [function] `private static bool ContainsAny(string value, params string[] patterns)`

### Assets\Scripts\Character\CharacterSpawner.cs

- Lines: 338
- Flags: SceneMutation, DependencyInjection

- L9 [type] `public class CharacterSpawner : BuildableObject,IInteractable`
- L45 [function] `private void Awake()`
- L50 [function] `public override void Start()`
- L63 [function] `private void EnsureRuntimeState()`
- L81 [function] `public IEnumerator StartSpawn()`
- L117 [function] `public bool TrySpawnCharacter(int id)`
- L178 [function] `public Vector3 GetOutsideSpawnWorldPosition()`
- L188 [function] `public Vector3 GetEntryDoorWorldPosition()`
- L203 [function] `public Vector2Int GetEntryGridPosition()`
- L210 [function] `public bool TryGetEntryGridPosition(out Vector2Int resolvedEntryGridPosition)`
- L229 [function] `private RegularCustomerState GetRegularCustomerState()`
- L236 [function] `private bool TryGetGrid(out Grid grid)`
- L241 [function] `private IRegularCustomerRuntimeProvider ResolveRegularCustomerRuntimeProvider()`
- L247 [function] `private IGridSystemProvider ResolveGridSystemProvider()`
- L253 [function] `private IRunVariableRuntimeReader ResolveRunVariableReader()`
- L259 [function] `private ICharacterSpawnObjectFactory RequireCharacterObjectFactory()`
- L265 [function] `public void Respawned(CharacterRespawnData data)`
- L269 [function] `private GameObject CreatePooledItem()`
- L273 [function] `private void OnTakeFromPool(GameObject poolGo)`
- L277 [function] `private void OnReturnedToPool(GameObject poolGo)`
- L287 [function] `private void OnDestroyPoolObject(GameObject poolGo)`
- L291 [function] `public IEnumerator Interact(CharacterActor actor)`
- L315 [type] `public class CharacterRespawnData`
- L321 [function] `public CharacterRespawnData(int id, float respawnTime)`
- L327 [function] `public void StartCheckRespawn(float lastDisabledTime)`
- L332 [function] `public bool CheckResapwn(float time)`

### Assets\Scripts\Character\CharacterSpawnObjectFactory.cs

- Lines: 64
- Flags: RuntimeObjectCreation, SceneMutation, DependencyInjection

- L6 [type] `public interface ICharacterSpawnObjectFactory`
- L13 [type] `public sealed class CharacterSpawnObjectFactory : ICharacterSpawnObjectFactory`
- L17 [function] `public CharacterSpawnObjectFactory(IObjectResolver objectResolver)`
- L23 [function] `public GameObject Create(GameObject characterPrefab)`
- L33 [function] `public void Inject(GameObject characterObject)`
- L49 [function] `public void Destroy(GameObject characterObject)`

### Assets\Scripts\Character\Core\CharacterAbilityCache.cs

- Lines: 68
- Flags: SceneMutation

- L7 [type] `public class CharacterAbilityCache : SerializedMonoBehaviour`
- L21 [function] `private void Awake()`
- L26 [function] `public void CacheAbility()`
- L32 [function] `public void RefreshAbilityCache()`
- L38 [function] `public T GetAbility<T>() where T : CharacterAbility`
- L53 [function] `public bool TryGetAbility<T>(out T result) where T : CharacterAbility`

### Assets\Scripts\Character\Core\CharacterActor.cs

- Lines: 730
- Flags: SceneMutation, DependencyInjection

- L23 [type] `public class CharacterActor : SerializedMonoBehaviour, IInfoable`
- L222 [function] `public static CharacterActor From(Component component)`
- L227 [function] `private void Awake()`
- L233 [function] `private void Start()`
- L252 [function] `private void Update()`
- L257 [function] `private void OnEnable()`
- L262 [function] `private void OnDisable()`
- L268 [function] `private void OnMouseDown()`
- L273 [function] `public void Initialize(CharacterSO data)`
- L297 [function] `public bool TryExecuteSelectedAiAction()`
- L313 [function] `public List<BuildableObject> GetReachableBuilding()`
- L333 [function] `private bool TryGetGrid(out Grid grid)`
- L344 [function] `public void EnsureRuntimeState()`
- L386 [function] `public InfoFeedEvent.Type GetInfoType()`
- L391 [function] `public T GetAbility<T>() where T : CharacterAbility`
- L397 [function] `public bool TryGetAbility<T>(out T result) where T : CharacterAbility`
- L409 [function] `public Vector2Int GetNowXY()`
- L415 [function] `public void AddLog(string message)`
- L421 [function] `public float GetMoveSpeed()`
- L427 [function] `public float GetConsumptionMultiplier()`
- L433 [function] `public float GetStayDurationMultiplier()`
- L439 [function] `public float GetCrowdSensitivityMultiplier()`
- L445 [function] `public float GetWorkSpeedMultiplier(FacilityWorkType workTypes)`
- L451 [function] `public float GetWorkPreferenceScore(FacilityWorkType workTypes)`
- L457 [function] `public float GetFacilityPreferenceScore(FacilityRole roles)`
- L463 [function] `public float GetAccidentChanceMultiplier()`
- L469 [function] `public CharacterSpeciesIncidentType GetIncidentType()`
- L475 [function] `public float GetCombatPowerMultiplier()`
- L481 [function] `public float GetFatigueEfficiencyMultiplier()`
- L487 [function] `public float GetInjuryEfficiencyMultiplier()`
- L511 [function] `public void Initialization(CharacterSO data)`
- L516 [function] `public void CacheAbility()`
- L522 [function] `public void RefreshAbilityCache()`
- L528 [function] `public IEnumerator ChangeStatByTick()`
- L534 [function] `public void ChangesStat(CharacterCondition condition, float value)`
- L540 [function] `public int GetCharacterStat(CharacterStatType statType)`
- L546 [function] `public void ApplyDamage(float amount, string reason = "")`
- L552 [function] `public void Heal(float amount)`
- L558 [function] `public void SetInjurySeverity(float value)`
- L564 [function] `public void Die(string reason = "")`
- L570 [function] `public void InitializeStats(bool resetCurrentHealth)`
- L576 [function] `public void SetLifecycleState(CharacterLifecycleState nextState)`
- L582 [function] `public bool BeginExpedition()`
- L588 [function] `public void EndExpedition(bool alive = true)`
- L594 [function] `public void ChangeLayer(string layer)`
- L600 [function] `public void ApplyVisualFootAnchor()`
- L606 [function] `public float GetVisualTopLocalY()`
- L612 [function] `public void DoFade(float alpha, float duration)`
- L618 [function] `public void Flip(CharacterFacing facing)`
- L624 [function] `public void HideForTraversal(float failSafeSeconds)`
- L630 [function] `public void RestoreTraversalVisibility()`
- L636 [function] `public void SetAiPaused(bool value)`
- L642 [function] `public bool IsAiPaused()`
- L647 [function] `public string GetSpeciesShortDescription()`
- L653 [function] `internal void RaiseDied(string reason)`
- L669 [function] `private void EnsureFeedbackBubbleIfInjected()`
- L679 [function] `private void EnsureSocialMemory()`
- L686 [function] `private ICharacterAiSchedulingService RequireAiSchedulingService()`
- L692 [function] `private IWorldInfoClickSelector RequireWorldInfoClickSelector()`
- L698 [function] `private void RegisterWithAiSchedulerRequired()`
- L704 [function] `private void RegisterWithAiSchedulerIfReady()`
- L715 [function] `private void UnregisterFromAiScheduler()`
- L726 [function] `private static IEnumerator EmptyRoutine()`

### Assets\Scripts\Character\Core\CharacterDeathEvent.cs

- Lines: 20
- Flags: EventBus

- L1 [type] `public struct CharacterDeathEvent`
- L6 [function] `public CharacterDeathEvent(CharacterActor actor, string reason)`
- L14 [function] `public static void Trigger(CharacterActor actor, string reason)`

### Assets\Scripts\Character\Core\CharacterEnums.cs

- Lines: 33
- Flags: None

- L1 [type] `public enum CharacterFacing`
- L7 [type] `public enum CharacterCondition`
- L17 [type] `public enum CharacterDecisionState`
- L24 [type] `public enum CharacterLifecycleState`

### Assets\Scripts\Character\Core\CharacterIdentity.cs

- Lines: 82
- Flags: None

- L5 [type] `public class CharacterIdentity : SerializedMonoBehaviour`
- L51 [function] `private void Awake()`
- L60 [function] `public void Bind(CharacterActor owner)`
- L65 [function] `public void SetData(CharacterSO nextData)`
- L73 [function] `public void SetCharacterType(CharacterType nextType)`
- L78 [function] `public string GetSpeciesShortDescription()`

### Assets\Scripts\Character\Core\CharacterLifecycle.cs

- Lines: 182
- Flags: SceneMutation, DependencyInjection

- L7 [type] `public class CharacterLifecycle : SerializedMonoBehaviour`
- L35 [function] `public void ConstructCharacterLifecycle(IGridSystemProvider gridSystemProvider)`
- L41 [function] `private void Awake()`
- L46 [function] `public void Bind(CharacterActor owner)`
- L56 [function] `public void SetAiPaused(bool value)`
- L61 [function] `public bool BeginExpedition()`
- L82 [function] `public void EndExpedition(bool alive = true)`
- L101 [function] `public void SetLifecycleState(CharacterLifecycleState nextState)`
- L127 [function] `public Vector2Int GetNowXY()`
- L139 [function] `public IEnumerator SnapToWalkableGridWhenReady()`
- L172 [function] `private bool TryGetGrid(out Grid grid)`

### Assets\Scripts\Character\Core\CharacterLog.cs

- Lines: 176
- Flags: None

- L8 [type] `public class CharacterLog : SerializedMonoBehaviour`
- L27 [function] `private void Awake()`
- L32 [function] `public void Bind()`
- L37 [function] `public void AddLog(string message)`
- L68 [function] `private void EnsureLog()`
- L75 [type] `public readonly struct CharacterLogEntry`
- L82 [function] `public CharacterLogEntry(string tag, string displayLine, int count, string originalMessage)`
- L91 [type] `public static class CharacterLogUtility`
- L93 [function] `public static string ToCauseTag(string message)`
- L155 [function] `private static bool ContainsAny(string value, params string[] patterns)`
- L160 [function] `private static string ExtractReasonAfterSeparator(string value)`

### Assets\Scripts\Character\Core\CharacterStats.cs

- Lines: 301
- Flags: DependencyInjection

- L9 [type] `public class CharacterStats : SerializedMonoBehaviour`
- L63 [function] `private void Awake()`
- L82 [function] `public void Bind(CharacterActor owner)`
- L92 [function] `public IEnumerator ChangeStatByTick()`
- L112 [function] `public void ChangesStat(CharacterCondition condition, float value)`
- L119 [function] `public int GetCharacterStat(CharacterStatType statType)`
- L124 [function] `public float GetMoveSpeed()`
- L133 [function] `public float GetConsumptionMultiplier()`
- L138 [function] `public float GetStayDurationMultiplier()`
- L143 [function] `public float GetCrowdSensitivityMultiplier()`
- L148 [function] `public float GetWorkSpeedMultiplier(FacilityWorkType workTypes)`
- L161 [function] `public float GetWorkPreferenceScore(FacilityWorkType workTypes)`
- L166 [function] `public float GetFacilityPreferenceScore(FacilityRole roles)`
- L171 [function] `public float GetAccidentChanceMultiplier()`
- L176 [function] `public CharacterSpeciesIncidentType GetIncidentType()`
- L181 [function] `public float GetCombatPowerMultiplier()`
- L187 [function] `public float GetFatigueEfficiencyMultiplier()`
- L198 [function] `public float GetInjuryEfficiencyMultiplier()`
- L203 [function] `public void ApplyDamage(float amount, string reason = "")`
- L219 [function] `public void Heal(float amount)`
- L228 [function] `public void SetInjurySeverity(float value)`
- L235 [function] `public void Die(string reason = "")`
- L255 [function] `public void RecalculateVitals(bool resetCurrentHealth)`
- L277 [function] `private void EnsureStats()`
- L288 [function] `private IMetaProgressionRuntimeReader ResolveMetaProgressionRuntimeReader()`
- L294 [function] `private void EnsureStat(CharacterCondition condition, float defaultValue)`

### Assets\Scripts\Character\Core\CharacterVisual.cs

- Lines: 441
- Flags: RuntimeObjectCreation, SceneMutation

- L8 [type] `public class CharacterVisual : SerializedMonoBehaviour`
- L30 [function] `private void Awake()`
- L35 [function] `public void Bind()`
- L42 [function] `public void ChangeLayer(string layer)`
- L51 [function] `public void EnsureVisualReferences()`
- L99 [function] `private Transform CreateVisualRoot()`
- L110 [function] `private static void CopySpriteRenderer(SpriteRenderer source, SpriteRenderer target)`
- L129 [function] `private static void RemoveRootSpriteRenderer(SpriteRenderer rootRenderer)`
- L146 [function] `public void SetCharacterSprite(Sprite sprite)`
- L158 [function] `public void ApplyVisualFootAnchor()`
- L174 [function] `public float GetVisualTopLocalY()`
- L186 [function] `public void DoFade(float alpha, float duration)`
- L196 [function] `public void Flip(CharacterFacing nextFacing)`
- L215 [function] `public void HideForTraversal(float failSafeSeconds)`
- L233 [function] `public void RestoreTraversalVisibility()`
- L239 [function] `private IEnumerator RestoreTraversalVisibilityAfter(float seconds)`
- L246 [function] `private void StopTraversalVisibilityTimer()`
- L257 [function] `private void RestoreTraversalVisibilityNow()`
- L294 [function] `public void RecoverExpiredTraversalVisibility()`
- L319 [function] `private List<RendererVisibilityState> CaptureRendererVisibility()`
- L333 [function] `private List<CanvasVisibilityState> CaptureCanvasVisibility()`
- L347 [function] `private void SetTraversalVisible(bool value)`
- L372 [function] `public void SetRenderersVisible(bool value)`
- L378 [function] `public void EnsureVisibleForActiveLifecycle()`
- L392 [function] `private bool CanRecoverActiveVisibility()`
- L410 [function] `private void SetSpriteRenderersVisible(bool value)`
- L418 [type] `private readonly struct RendererVisibilityState`
- L420 [function] `public RendererVisibilityState(Renderer renderer, bool enabled)`
- L430 [type] `private readonly struct CanvasVisibilityState`
- L432 [function] `public CanvasVisibilityState(Canvas canvas, bool enabled)`

### Assets\Scripts\Character\Core\CharacterVisualRootFactory.cs

- Lines: 35
- Flags: RuntimeObjectCreation, SceneMutation

- L4 [type] `public interface ICharacterVisualRootFactory`
- L9 [type] `public sealed class CharacterVisualRootFactory : ICharacterVisualRootFactory`
- L13 [function] `public SpriteRenderer EnsureVisualRoot(GameObject characterObject)`

### Assets\Scripts\Character\Core\Customer.cs

- Lines: 15
- Flags: None

- L12 [type] `public class Customer : CharacterActor`

### Assets\Scripts\Character\Core\OwnerCharacterFactory.cs

- Lines: 126
- Flags: RuntimeObjectCreation, SceneMutation, DependencyInjection

- L6 [type] `public interface IOwnerCharacterFactory`
- L15 [type] `public sealed class OwnerCharacterFactory : IOwnerCharacterFactory`
- L60 [function] `private CharacterActor EnsureOwnerComponents(GameObject ownerObject)`
- L97 [function] `private void InjectOwnerRuntime(GameObject ownerObject)`
- L105 [function] `private Vector3 ResolveOwnerSpawnPosition(Transform ownerSpawnPoint, Vector2Int ownerSpawnGridPosition)`

### Assets\Scripts\Character\Core\OwnerRunManager.cs

- Lines: 194
- Flags: EventBus, SceneMutation, DependencyInjection

- L7 [type] `public class OwnerRunManager : SerializedMonoBehaviour, UtilEventListener<CharacterDeathEvent>`
- L29 [function] `private void Awake()`
- L47 [function] `private void Start()`
- L59 [function] `public void SelectOwnerByIndex(int index)`
- L71 [function] `public void SelectOwner(CharacterSO ownerData)`
- L96 [function] `public void HandleOwnerDeath(CharacterActor owner, string reason)`
- L112 [function] `public CharacterSO GetDefaultOwner()`
- L118 [function] `private CharacterActor SpawnOwner(CharacterSO ownerData)`
- L127 [function] `private void EnsureOwnerCandidates()`
- L142 [function] `private void NormalizeOwnerCandidates()`
- L150 [function] `private IOwnerCharacterFactory ResolveOwnerCharacterFactory()`
- L156 [function] `public void OnTriggerEvent(CharacterDeathEvent eventType)`
- L164 [function] `private void OnEnable()`
- L169 [function] `private void OnDisable()`
- L175 [type] `public struct OwnerRunEndedEvent`
- L180 [function] `public OwnerRunEndedEvent(CharacterActor owner, string reason)`
- L188 [function] `public static void Trigger(CharacterActor owner, string reason)`

### Assets\Scripts\Character\Core\Shopkeeper.cs

- Lines: 15
- Flags: None

- L12 [type] `public class Shopkeeper : CharacterActor`

### Assets\Scripts\Character\Input\OwnerCommandController.cs

- Lines: 148
- Flags: EventBus, DependencyInjection

- L5 [type] `public class OwnerCommandController : MonoBehaviour, UtilEventListener<InfoFeedEvent>`
- L30 [function] `private void Update()`
- L43 [function] `private void TryIssuePriorityWorkCommand()`
- L92 [function] `private void TryIssueSuppressCommand(CharacterActor target)`
- L124 [function] `public void OnTriggerEvent(InfoFeedEvent eventType)`
- L133 [function] `private void OnEnable()`
- L138 [function] `private void OnDisable()`
- L143 [function] `private IMainCameraProvider RequireMainCameraProvider()`

### Assets\Scripts\Character\SO\CharacterModelData.cs

- Lines: 364
- Flags: None

- L6 [type] `public enum CharacterStatType`
- L20 [type] `public class CharacterStatBlock`
- L42 [function] `public static CharacterStatBlock CreateDefault(int value = 5)`
- L58 [function] `public int Get(CharacterStatType type)`
- L75 [function] `public void Add(CharacterStatBlock other)`
- L92 [type] `public class CharacterModelModifiers`
- L109 [function] `public void Multiply(CharacterModelModifiers other)`
- L130 [type] `public sealed class CharacterRuntimeProfile`
- L159 [function] `public static CharacterRuntimeProfile From(CharacterSO source)`
- L164 [function] `public int GetStat(CharacterStatType type)`
- L169 [function] `public float GetMoveSpeedMultiplier()`
- L175 [function] `public float GetSpendingMultiplier()`
- L181 [function] `public float GetConsumptionMultiplier()`
- L186 [function] `public float GetStayDurationMultiplier()`
- L192 [function] `public float GetCrowdSensitivityMultiplier()`
- L197 [function] `public float GetAccidentChanceMultiplier()`
- L204 [function] `public CharacterSpeciesIncidentType GetIncidentType()`
- L209 [function] `public string GetIncidentName()`
- L214 [function] `public string GetIncidentDescription()`
- L219 [function] `public string GetShortDescription()`
- L224 [function] `public float GetCombatPowerMultiplier()`
- L230 [function] `public float GetWorkSpeedMultiplier(FacilityWorkType workTypes)`
- L252 [function] `public float GetWorkPreferenceScore(FacilityWorkType workTypes)`
- L272 [function] `public float GetFacilityPreferenceScore(FacilityRole roles)`
- L292 [function] `public bool HasTrait(string traitName)`
- L308 [function] `private static CharacterStatType GetBestWorkStat(FacilityWorkType workTypes)`
- L358 [function] `private static CharacterStatBlock CopyStats(CharacterStatBlock source)`

### Assets\Scripts\Character\SO\CharacterSO.cs

- Lines: 155
- Flags: None

- L10 [type] `public class CharacterSO : ScriptableObject`
- L56 [function] `public CharacterRuntimeProfile CreateRuntimeProfile()`
- L105 [function] `public int GetFrequencyVisit()`
- L111 [function] `public int GetHoldingMoney()`
- L116 [function] `public int GetHoldingMoney(CharacterRuntimeProfile profile)`
- L127 [type] `private enum CharacterSpeedType`
- L135 [type] `private enum CharacterRespawnSpeedType`
- L144 [type] `public enum CharacterType`
- L151 [type] `public enum CharacterRole`

### Assets\Scripts\Character\SO\CharacterSpeciesSO.cs

- Lines: 28
- Flags: None

- L4 [type] `public enum CharacterSpeciesIncidentType`
- L13 [type] `public class CharacterSpeciesSO : DataScriptableObject`

### Assets\Scripts\Character\SO\CharacterTraitSO.cs

- Lines: 10
- Flags: None

- L4 [type] `public class CharacterTraitSO : DataScriptableObject`

### Assets\Scripts\Character\UI\CharacterFeedbackBubble.cs

- Lines: 341
- Flags: SceneMutation, DependencyInjection

- L7 [type] `public enum CharacterFeedbackState`
- L22 [type] `public class CharacterFeedbackBubble : MonoBehaviour`
- L51 [function] `private void Awake()`
- L60 [function] `private void OnEnable()`
- L77 [function] `private void OnDisable()`
- L95 [function] `private void LateUpdate()`
- L113 [function] `public void Show(CharacterFeedbackState state)`
- L124 [function] `public CharacterFeedbackState EvaluatePersistentState()`
- L164 [function] `public static CharacterFeedbackState ClassifyLogTag(string tag)`
- L199 [function] `public static string GetSymbol(CharacterFeedbackState state)`
- L212 [function] `private void OnLogAdded(CharacterLogEntry entry)`
- L228 [function] `private void OnStatChanged(System.Collections.Generic.Dictionary<CharacterCondition, float> stats)`
- L236 [function] `private void ApplyState(CharacterFeedbackState state)`
- L255 [function] `private void HideView()`
- L261 [function] `private void EnsureView()`
- L271 [function] `private void ReleaseView()`
- L282 [function] `private Vector3 GetLocalOffset()`
- L294 [function] `private float GetStat(CharacterCondition condition, float defaultValue)`
- L303 [function] `private ICharacterAiSchedulingService RequireAiSchedulingService()`
- L309 [function] `private ICharacterFeedbackBubbleViewFactory RequireBubbleViewFactory()`
- L316 [function] `private static Color GetColor(CharacterFeedbackState state)`
- L329 [function] `private static bool ContainsAny(string value, params string[] patterns)`

### Assets\Scripts\Character\UI\CharacterFeedbackBubbleFactory.cs

- Lines: 35
- Flags: RuntimeObjectCreation, SceneMutation, DependencyInjection

- L5 [type] `public interface ICharacterFeedbackBubbleFactory`
- L10 [type] `public sealed class CharacterFeedbackBubbleFactory : ICharacterFeedbackBubbleFactory`
- L14 [function] `public CharacterFeedbackBubbleFactory(IObjectResolver objectResolver)`
- L20 [function] `public CharacterFeedbackBubble GetOrAdd(CharacterActor actor)`

### Assets\Scripts\Character\UI\CharacterFeedbackBubbleService.cs

- Lines: 22
- Flags: None

- L3 [type] `public interface ICharacterFeedbackBubbleService`
- L8 [type] `public sealed class CharacterFeedbackBubbleService : ICharacterFeedbackBubbleService`
- L12 [function] `public CharacterFeedbackBubbleService(ICharacterFeedbackBubbleFactory bubbleFactory)`
- L18 [function] `public CharacterFeedbackBubble GetOrAdd(CharacterActor actor)`

### Assets\Scripts\Character\UI\CharacterFeedbackBubbleViewFactory.cs

- Lines: 70
- Flags: RuntimeObjectCreation, SceneMutation, DependencyInjection

- L7 [type] `public interface ICharacterFeedbackBubbleViewFactory`
- L13 [type] `public sealed class CharacterFeedbackBubbleViewFactory : ICharacterFeedbackBubbleViewFactory`
- L19 [function] `public CharacterFeedbackBubbleViewFactory(ITmpKoreanFontService tmpKoreanFontService)`
- L25 [function] `public TextMeshPro Acquire(Transform parent, Vector3 localPosition)`
- L47 [function] `public void Release(TextMeshPro text)`
- L60 [function] `private TextMeshPro CreateTextView()`

### Assets\Scripts\Character\UI\CharacterFloatingIcon.cs

- Lines: 92
- Flags: SceneMutation

- L5 [type] `public interface IFloatingIconFeedbackService`
- L10 [type] `public static class FloatingIconFeedbackDefaults`
- L15 [type] `public sealed class GameManagerFloatingIconFeedbackService : IFloatingIconFeedbackService`
- L20 [function] `public GameManagerFloatingIconFeedbackService(IDungeonSceneComponentQuery sceneQuery)`
- L25 [function] `public bool Show(Component target, Sprite sprite, float maxWorldSize)`
- L45 [function] `private DamageNumber ResolveNumber(NumberCondition condition)`
- L61 [function] `private GameManager ResolveGameManager()`
- L68 [function] `private static void FitIcon(SpriteRenderer iconRenderer, float maxWorldSize)`

### Assets\Scripts\Character\UI\OwnerSelectionPanel.cs

- Lines: 140
- Flags: SceneMutation, DependencyInjection

- L7 [type] `public class OwnerSelectionPanel : MonoBehaviour`
- L20 [function] `public void ConstructOwnerSelectionPanel(IOwnerRunManagerProvider ownerRunManagerProvider, ITmpKoreanFontService tmpKoreanFontService, IOwnerSelectionOptionButtonFactory optionButtonFactory)`
- L33 [function] `private void Start()`
- L51 [function] `public void BuildOptions()`
- L78 [function] `private OwnerRunManager ResolveOwnerRunManager()`
- L99 [function] `private void RefreshSelectedOwner(CharacterSO ownerData)`
- L109 [function] `private ITmpKoreanFontService RequireTmpKoreanFontService()`
- L116 [function] `private IOwnerSelectionOptionButtonFactory RequireOptionButtonFactory()`
- L123 [function] `private static string MakeButtonLabel(CharacterSO candidate)`
- L133 [function] `private void OnDestroy()`

### Assets\Scripts\Character\UI\OwnerSelectionOptionButtonFactory.cs

- Lines: 70
- Flags: RuntimeObjectCreation, SceneMutation, DependencyInjection

- L7 [type] `public interface IOwnerSelectionOptionButtonFactory`
- L9 [function] `Button Create(Button prefab, Transform parent, string objectName, string label, UnityAction onClick)`
- L10 [function] `void Release(GameObject optionObject)`
- L13 [type] `public sealed class OwnerSelectionOptionButtonFactory : IOwnerSelectionOptionButtonFactory`
- L17 [function] `public OwnerSelectionOptionButtonFactory(ITmpKoreanFontService tmpKoreanFontService)`
- L23 [function] `public Button Create(Button prefab, Transform parent, string objectName, string label, UnityAction onClick)`
- L53 [function] `public void Release(GameObject optionObject)`

### Assets\Scripts\Character\UI\StaffWorkPriorityPanel.cs

- Lines: 528
- Flags: EventBus, SceneMutation, DependencyInjection

- L8 [type] `public class StaffWorkPriorityPanel : MonoBehaviour, UtilEventListener<InfoFeedEvent>`
- L36 [function] `public void ConstructStaffWorkPriorityPanel(IStaffWorkPriorityPanelModelBuilder modelBuilder, IStaffWorkPriorityPanelUiFactory uiFactory)`
- L46 [function] `private void Awake()`
- L51 [function] `private void Start()`
- L57 [function] `private void Update()`
- L72 [function] `public void OnTriggerEvent(InfoFeedEvent eventType)`
- L89 [function] `public void Refresh()`
- L95 [function] `private void EnsureLayout()`
- L156 [function] `private RectTransform ResolveHost()`
- L185 [function] `private void ClearHost(RectTransform host)`
- L206 [function] `private void BuildTable()`
- L249 [function] `private void BuildHeader(IReadOnlyList<FacilityWorkType> workTypes, float tableWidth)`
- L273 [function] `private void BuildWorkerRow(StaffWorkPriorityRowModel worker, IReadOnlyList<FacilityWorkType> workTypes, float tableWidth)`
- L309 [function] `private GameObject CreateRow(string name, float width, float height)`
- L322 [function] `private TMP_Text CreateLabelCell(Transform parent, string text, float width, float height, TextAlignmentOptions alignment, bool header)`
- L347 [function] `private void CreatePriorityCell(Transform parent, StaffWorkPriorityRowModel worker, FacilityWorkType workType)`
- L375 [function] `private GameObject CreateCellObject(string name, Transform parent, float width, float height)`
- L385 [function] `private TMP_Text AddCellText(Transform parent, string text, TextAlignmentOptions alignment, bool allowAutoSize)`
- L406 [function] `private int CalculateWorkerHash()`
- L411 [function] `private static string GetWorkTypeLabel(FacilityWorkType workType)`
- L427 [function] `private static string GetPriorityLabel(WorkPriorityLevel priority)`
- L438 [function] `private static Color GetPriorityColor(WorkPriorityLevel priority, bool selected)`
- L453 [function] `private static string GetWorkerStatus(StaffWorkPriorityRowModel worker)`
- L481 [function] `private void ClearSpawnedObjects()`
- L505 [function] `private void OnEnable()`
- L511 [function] `private void OnDisable()`
- L516 [function] `private IStaffWorkPriorityPanelModelBuilder RequireModelBuilder()`
- L522 [function] `private IStaffWorkPriorityPanelUiFactory RequireUiFactory()`

### Assets\Scripts\Character\UI\StaffWorkPriorityPanelModel.cs

- Lines: 94
- Flags: DependencyInjection

- L4 [type] `public readonly struct StaffWorkPriorityRowModel`
- L6 [function] `public StaffWorkPriorityRowModel(CharacterActor character, AbilityWork work, string name)`
- L18 [type] `public interface IStaffWorkPriorityPanelModelBuilder`
- L20 [function] `IReadOnlyList<StaffWorkPriorityRowModel> BuildRows()`
- L21 [function] `int CalculateWorkerHash()`
- L22 [function] `int CalculateWorkerHash(IReadOnlyList<StaffWorkPriorityRowModel> workers)`
- L23 [function] `string GetDisplayName(CharacterActor character)`
- L26 [type] `public sealed class StaffWorkPriorityPanelModelBuilder : IStaffWorkPriorityPanelModelBuilder`
- L30 [function] `public StaffWorkPriorityPanelModelBuilder(IStaffWorkforceQueryService workforceQueryService)`
- L36 [function] `public IReadOnlyList<StaffWorkPriorityRowModel> BuildRows()`
- L54 [function] `public int CalculateWorkerHash()`
- L59 [function] `public int CalculateWorkerHash(IReadOnlyList<StaffWorkPriorityRowModel> workers)`
- L90 [function] `public string GetDisplayName(CharacterActor character)`

### Assets\Scripts\Character\UI\StaffWorkPriorityPanelUiFactory.cs

- Lines: 163
- Flags: RuntimeObjectCreation, SceneMutation, DependencyInjection

- L6 [type] `public interface IStaffWorkPriorityPanelUiFactory`
- L24 [type] `public sealed class StaffWorkPriorityPanelUiFactory : IStaffWorkPriorityPanelUiFactory`
- L28 [function] `public StaffWorkPriorityPanelUiFactory(ITmpKoreanFontService tmpKoreanFontService)`
- L34 [function] `public RectTransform EnsureRectTransform(GameObject target)`
- L47 [function] `public GameObject CreateUiObject(string name, Transform parent)`
- L54 [function] `public Image AddImage(GameObject target, Color color)`
- L61 [function] `public ScrollRect AddScrollRect(GameObject target)`
- L71 [function] `public Mask AddMask(GameObject target, bool showMaskGraphic)`
- L78 [function] `public VerticalLayoutGroup AddVerticalLayoutGroup(GameObject target)`
- L89 [function] `public HorizontalLayoutGroup AddHorizontalLayoutGroup(GameObject target)`
- L100 [function] `public ContentSizeFitter AddContentSizeFitter(GameObject target)`
- L108 [function] `public LayoutElement AddLayoutElement(GameObject target, float width, float height)`
- L118 [function] `public Button AddButton(GameObject target, Graphic targetGraphic)`
- L125 [function] `public TMP_Text AddText(GameObject target)`
- L132 [function] `public Shadow AddShadow(GameObject target, Color effectColor, Vector2 effectDistance)`
- L140 [function] `public void ApplyFonts(Transform root)`
- L145 [function] `public void Release(GameObject target)`

### Assets\Scripts\Character\Work\CharacterWorkRoleUtility.cs

- Lines: 22
- Flags: None

- L1 [type] `public static class CharacterWorkRoleUtility`
- L3 [function] `public static bool TryGetWork(CharacterActor actor, out AbilityWork work)`
- L11 [function] `public static bool IsWorker(CharacterActor actor)`
- L16 [function] `public static bool IsOnDutyWorker(CharacterActor actor)`

### Assets\Scripts\Character\Work\StaffDiscontentSystem.cs

- Lines: 758
- Flags: EventBus, DependencyInjection

- L7 [type] `public enum StaffDiscontentStage`
- L17 [type] `public enum StaffDiscontentOutcome`
- L28 [type] `public enum StaffRebellionResponseType`
- L37 [type] `public class StaffDiscontentRules`
- L53 [function] `public static StaffDiscontentRules CreateDefault()`
- L59 [type] `public sealed class StaffDiscontentSnapshot`
- L74 [function] `public string ToSummaryText()`
- L80 [type] `public sealed class StaffDiscontentRecord`
- L82 [function] `public StaffDiscontentRecord(int staffId, CharacterActor staff)`
- L101 [function] `public StaffDiscontentOutcome Update(CharacterActor staff, StaffDiscontentRules rules)`
- L166 [function] `public bool MarkIsolated()`
- L178 [function] `public bool MarkSuppressed()`
- L192 [function] `public bool TryCalm(CharacterActor staff, StaffDiscontentRules rules, out string failureReason)`
- L222 [function] `public StaffDiscontentSnapshot ToSnapshot(StaffDiscontentOutcome outcome = StaffDiscontentOutcome.None)`
- L242 [type] `public readonly struct StaffRebellionResponseResult`
- L265 [type] `public sealed class StaffDiscontentState`
- L271 [function] `public StaffDiscontentRecord ProcessStaff(CharacterActor staff, StaffDiscontentRules rules, out StaffDiscontentOutcome outcome)`
- L285 [function] `public bool TryGetRecord(CharacterActor staff, out StaffDiscontentRecord record)`
- L296 [function] `public bool IsPermanentLoss(CharacterActor staff)`
- L301 [function] `private StaffDiscontentRecord GetOrCreate(int staffId, CharacterActor staff)`
- L313 [type] `public struct StaffDiscontentChangedEvent`
- L317 [function] `public StaffDiscontentChangedEvent(StaffDiscontentSnapshot snapshot)`
- L324 [function] `public static void Trigger(StaffDiscontentSnapshot snapshot)`
- L331 [type] `public struct StaffPermanentLossEvent`
- L335 [function] `public StaffPermanentLossEvent(StaffDiscontentSnapshot snapshot)`
- L342 [function] `public static void Trigger(StaffDiscontentSnapshot snapshot)`
- L349 [type] `public struct StaffRebellionResponseEvent`
- L353 [function] `public StaffRebellionResponseEvent(StaffRebellionResponseResult result)`
- L360 [function] `public static void Trigger(StaffRebellionResponseResult result)`
- L367 [type] `public static class StaffDiscontentService`
- L369 [function] `public static bool IsTrackableStaff(CharacterActor staff)`
- L379 [function] `public static int GetStaffId(CharacterActor staff)`
- L390 [function] `public static string GetStaffDisplayName(CharacterActor staff, int staffId)`
- L406 [function] `public static float GetMood(CharacterActor staff)`
- L419 [function] `public static StaffDiscontentStage EvaluateStage(float mood, int lowMoodDays, StaffDiscontentRules rules)`
- L455 [function] `public static float GetWorkEfficiencyMultiplier(StaffDiscontentStage stage, StaffDiscontentRules rules)`
- L469 [function] `public static bool ShouldBlockWork(StaffDiscontentStage stage)`
- L476 [function] `public static string GetBlockReason(StaffDiscontentStage stage)`
- L488 [type] `public class StaffDiscontentRuntime : MonoBehaviour, UtilEventListener<OperatingDayEndedEvent>`
- L499 [function] `public void Construct(IDungeonSceneComponentQuery sceneQuery)`
- L505 [function] `public void OnTriggerEvent(OperatingDayEndedEvent eventType)`
- L510 [function] `public StaffDiscontentRecord ProcessStaff(CharacterActor staff, out StaffDiscontentOutcome outcome)`
- L526 [function] `public void ProcessAllStaff()`
- L535 [function] `public float GetWorkEfficiencyMultiplier(CharacterActor staff)`
- L548 [function] `public bool ShouldBlockWork(CharacterActor staff, out string reason)`
- L568 [function] `public bool IsRebellionTarget(CharacterActor target)`
- L576 [function] `public int DispatchAutoSuppress(CharacterActor rebel)`
- L630 [function] `public bool TryIsolateRebel(CharacterActor rebel, CharacterActor actor, out StaffRebellionResponseResult result)`
- L652 [function] `public bool TryCalmStaff(CharacterActor staff, CharacterActor actor, out StaffRebellionResponseResult result)`
- L673 [function] `public bool ResolveSuppressedRebel(CharacterActor rebel, CharacterActor defender)`
- L696 [function] `private void ApplyOutcome(CharacterActor staff, StaffDiscontentRecord record, StaffDiscontentOutcome outcome)`
- L739 [function] `private IDungeonSceneComponentQuery RequireSceneQuery()`
- L749 [function] `private void OnEnable()`
- L754 [function] `private void OnDisable()`

### Assets\Scripts\Character\Work\StaffWorkforceQueryService.cs

- Lines: 51
- Flags: None

- L4 [type] `public interface IStaffWorkforceQueryService`
- L11 [type] `public sealed class StaffWorkforceRuntimeQueryService : IStaffWorkforceQueryService`
- L15 [function] `public StaffWorkforceRuntimeQueryService(IDungeonSceneComponentQuery sceneQuery)`
- L20 [function] `public IReadOnlyList<CharacterActor> FindActiveWorkers()`
- L29 [function] `public bool IsActiveWorker(CharacterActor character)`
- L36 [function] `public string GetDisplayName(CharacterActor character)`

### Assets\Scripts\Character\Work\WorkCommandHandler.cs

- Lines: 260
- Flags: None

- L6 [type] `public sealed class WorkCommandHandler`
- L15 [function] `public WorkCommandHandler(AbilityWork work, WorkTargetSelector targetSelector)`
- L26 [function] `public bool TrySetPriorityWorkTarget(BuildableObject building, out string errorMessage)`
- L92 [function] `public bool TryGetPrioritySuppressDestination(GridPathSearchResult searchResult, out BuildableObject destination)`
- L119 [function] `public void ClearPriorityWorkTarget()`
- L127 [function] `public bool HasUrgentPriorityTarget()`
- L139 [function] `public IEnumerator SuppressPriorityTarget()`

### Assets\Scripts\Character\Work\WorkDebugLog.cs

- Lines: 36
- Flags: None

- L1 [type] `public static class WorkDebugLog`
- L3 [function] `public static void LogProgress(CharacterActor actor)`
- L8 [function] `public static void LogEnd(CharacterActor actor, string reason = null)`
- L19 [function] `private static string GetCharacterName(CharacterActor actor)`

### Assets\Scripts\Character\Work\WorkDutyController.cs

- Lines: 462
- Flags: None

- L4 [type] `public sealed class WorkDutyController`
- L12 [function] `public WorkDutyController(AbilityWork work)`
- L20 [function] `public void InitializeWorkerCondition(CharacterSO data)`
- L35 [function] `public bool ShouldUseRestProtection()`
- L84 [function] `public bool CanStartWorkAction()`
- L148 [function] `public bool ShouldTakeOffDuty()`
- L166 [function] `public bool ShouldReturnToWork()`
- L188 [function] `public void BeginOffDuty(string reason)`
- L212 [function] `public void PrepareForExpedition()`
- L220 [function] `public void SetDutyState(AbilityWork.DutyState nextState)`
- L261 [function] `public void ApplyWorkFatigueTick()`
- L272 [function] `public IEnumerator CheckActionWork(int runId)`
- L326 [function] `public bool ShouldInterruptCurrentWork(out string interruptReason)`
- L363 [function] `private bool ShouldEndRoutineWorkShift(float startedAt, out string reason)`
- L385 [function] `public bool CanContinueAssignedWork(out string stopReason)`
- L434 [function] `private void EnsureStatAtLeast(CharacterCondition condition, float value)`
- L445 [function] `private float GetStat(CharacterCondition condition, float defaultValue)`
- L458 [function] `private CharacterStats GetWorkerStats()`

### Assets\Scripts\Character\Work\WorkforceReplanService.cs

- Lines: 38
- Flags: None

- L5 [type] `public interface IWorkforceReplanService`
- L10 [type] `public sealed class DungeonWorkforceReplanService : IWorkforceReplanService`
- L14 [function] `public DungeonWorkforceReplanService(IDungeonSceneComponentQuery sceneQuery)`
- L20 [function] `public void RequestIdleWorkersToReplan(bool clearFailures = true)`

### Assets\Scripts\Character\Work\WorkGridUtility.cs

- Lines: 58
- Flags: SceneMutation

- L4 [type] `public interface IWorkGridResolver`
- L14 [type] `public sealed class WorkGridResolver : IWorkGridResolver`
- L18 [function] `public WorkGridResolver(IGridSystemProvider gridSystemProvider)`
- L48 [function] `public Vector2Int GetGridPosition(Grid activeGrid, CharacterActor actor)`

### Assets\Scripts\Character\Work\WorkPriorityProfile.cs

- Lines: 373
- Flags: None

- L5 [type] `public enum WorkPriorityLevel`
- L13 [type] `public static class WorkPriorityLevelExtensions`
- L15 [function] `public static WorkPriorityLevel Next(this WorkPriorityLevel priority)`
- L26 [function] `public static float GetBaseScore(this WorkPriorityLevel priority)`
- L37 [function] `public static string ToDisplayText(this WorkPriorityLevel priority)`
- L49 [type] `public static class WorkTaskCatalog`
- L63 [function] `public static string GetDisplayName(FacilityWorkType workType)`
- L79 [function] `public static IEnumerable<FacilityWorkType> GetSingleTypes(FacilityWorkType workTypes)`
- L91 [type] `public static class WorkCommandResolver`
- L168 [function] `public static bool IsSuppressTarget(CharacterActor target, Predicate<CharacterActor> isRebellionTarget)`
- L244 [function] `private static bool NeedsRestock(BuildableObject target)`
- L262 [type] `public class WorkPriorityProfile`
- L273 [function] `public static WorkPriorityProfile CreateDefault()`
- L278 [function] `public WorkPriorityProfile Clone()`
- L293 [function] `public WorkPriorityLevel GetPriority(FacilityWorkType workType)`
- L309 [function] `public void SetPriority(FacilityWorkType workType, WorkPriorityLevel priority)`
- L340 [function] `public bool IsEnabled(FacilityWorkType workType)`
- L345 [function] `public void ApplyPreferredTypes(FacilityWorkType preferredTypes)`
- L357 [function] `private WorkPriorityLevel GetBestPriority(FacilityWorkType workTypes)`

### Assets\Scripts\Character\Work\WorkTargetCandidate.cs

- Lines: 45
- Flags: None

- L1 [type] `public readonly struct WorkTargetCandidate`

### Assets\Scripts\Character\Work\WorkTargetSelector.cs

- Lines: 388
- Flags: None

- L5 [type] `public sealed class WorkTargetSelector`
- L10 [function] `public WorkTargetSelector(AbilityWork work)`
- L83 [function] `public bool CanUseAsWorkTarget(BuildableObject building)`
- L88 [function] `public bool CanUseAsWorkTarget(BuildableObject building, FacilityWorkType requestedWorkType)`
- L140 [function] `public float GetUtilityScore(FacilityWorkType requestedWorkType, GridPathSearchResult searchResult)`
- L150 [function] `public IEnumerable<BuildableObject> GetReachableBuildings(GridPathSearchResult searchResult)`
- L168 [function] `public IEnumerable<IWarehouseFacility> FindReachableWarehouses(GridPathSearchResult searchResult = null)`
- L352 [function] `private static int GetFailureRelevance(WorkTargetCandidate candidate)`
- L365 [function] `private static float GetDistanceScore(BuildableObject building, GridPathSearchResult searchResult)`
- L384 [function] `private static bool CanUseSuppressFor(FacilityWorkType requestedWorkType)`

### Assets\Scripts\Character\Work\WorkTaskExecutor.cs

- Lines: 490
- Flags: SceneMutation

- L6 [type] `public sealed class WorkTaskExecutor`
- L13 [function] `public WorkTaskExecutor(AbilityWork work, WorkTargetSelector targetSelector)`
- L19 [function] `public IEnumerator Work(int runId)`
- L138 [function] `private bool HasReachedAssignedWorkTarget(CharacterActor actor, Grid grid)`
- L386 [function] `public IEnumerator ExecuteRestockWork()`
- L404 [function] `public IEnumerator ExecuteRepairWork()`
- L421 [function] `public IEnumerator ExecuteResearchWork()`
- L446 [function] `private static void EndAiAction(CharacterActor actor, AIAction currentAction)`
- L455 [function] `private void FinishWorkRun(CharacterActor actor, AIAction currentAction)`
- L471 [function] `private bool ShouldAbortWorkRun(int runId, CharacterActor actor)`
- L479 [function] `private void AbortWorkRun(int runId, CharacterActor actor, AIAction currentAction)`

### Assets\Scripts\Codex\CodexEvolutionRecorder.cs

- Lines: 57
- Flags: None

- L1 [type] `public static class CodexEvolutionRecorder`
- L3 [function] `public static void Record(CodexState state, FacilityEvolutionResult result)`

### Assets\Scripts\Codex\CodexFacilityInfoWriter.cs

- Lines: 22
- Flags: None

- L1 [type] `public static class CodexFacilityInfoWriter`
- L3 [function] `public static void Add(CodexState state, BuildingSO building, string info, CodexInfoSource source)`
- L18 [function] `public static string GetFacilityEntryId(BuildingSO building)`

### Assets\Scripts\Codex\CodexInvasionObservationMapper.cs

- Lines: 79
- Flags: None

- L3 [type] `public static class CodexInvasionObservationMapper`
- L5 [function] `public static IEnumerable<string> FromEffectTag(string tag)`
- L39 [function] `public static string NormalizeObservation(string observation)`

### Assets\Scripts\Codex\CodexInvasionRecorder.cs

- Lines: 96
- Flags: None

- L3 [type] `public static class CodexInvasionRecorder`
- L7 [function] `public static void RecordDefenseObservation(CodexState state, DefenseActivationReport report)`
- L35 [function] `public static void RecordCombatReport(CodexState state, InvasionCombatReport report)`
- L58 [function] `public static void RecordFacilityDamage(CodexState state, BuildableObject facility)`
- L64 [function] `public static void SeedBreakthroughIntruder(CodexState state)`
- L81 [function] `private static void AddInvasionInfo(CodexState state, string info, CodexInfoSource source)`

### Assets\Scripts\Codex\CodexObservationRecorder.cs

- Lines: 140
- Flags: None

- L3 [type] `public static class CodexObservationRecorder`
- L5 [function] `public static void ObserveCharacter(CodexState state, CharacterActor actor)`
- L28 [function] `public static void ObserveSpecies(CodexState state, CharacterSpeciesSO species, CodexInfoSource source)`
- L60 [function] `public static void ObserveFacility(CodexState state, BuildableObject facility, CodexInfoSource source)`
- L70 [function] `public static void ObserveFacility(CodexState state, BuildingSO building, CodexInfoSource source)`
- L128 [function] `private static void AddIfNotBlank(CodexEntryRecord entry, string text, CodexInfoSource source)`
- L136 [function] `private static string GetMonsterEntryId(string speciesTag)`

### Assets\Scripts\Codex\CodexRecipeRecorder.cs

- Lines: 136
- Flags: None

- L4 [type] `public static class CodexRecipeRecorder`
- L114 [function] `private static void AddSpecialRecipeHint(CodexState state, FacilitySynthesisRecipeSO recipe)`
- L129 [function] `private static string BuildSpecialRecipeHint(FacilitySynthesisRecipeSO recipe)`

### Assets\Scripts\Codex\CodexRecordSummaryQuery.cs

- Lines: 79
- Flags: None

- L3 [type] `public readonly struct CodexRecordSummary`
- L29 [type] `public interface ICodexRecordSummaryService`
- L34 [type] `public interface ICodexRecordSummaryRuntimeSource`
- L41 [type] `public sealed class CodexRecordSummaryRuntimeSource : ICodexRecordSummaryRuntimeSource`
- L45 [function] `public CodexRecordSummaryRuntimeSource(IDungeonSceneComponentQuery sceneQuery)`
- L55 [type] `public sealed class CodexRecordSummaryService : ICodexRecordSummaryService`
- L59 [function] `public CodexRecordSummaryService(ICodexRecordSummaryRuntimeSource runtimeSource)`
- L64 [function] `public CodexRecordSummary Capture()`

### Assets\Scripts\Codex\CodexReferenceImporter.cs

- Lines: 43
- Flags: None

- L3 [type] `public interface ICodexReferenceImporter`
- L8 [type] `public sealed class CodexReferenceImporter : ICodexReferenceImporter`
- L23 [function] `public void Import(CodexState state, BlueprintResearchState researchState)`

### Assets\Scripts\Codex\CodexSystem.cs

- Lines: 462
- Flags: EventBus, DependencyInjection

- L7 [type] `public enum CodexEntryCategory`
- L14 [type] `public enum CodexInfoSource`
- L23 [type] `public readonly struct CodexInfoLine`
- L25 [function] `public CodexInfoLine(string text, CodexInfoSource source)`
- L35 [type] `public sealed class CodexEntrySnapshot`
- L43 [function] `public string ToDisplayText()`
- L63 [type] `public sealed class CodexEntryRecord`
- L68 [function] `public CodexEntryRecord(CodexEntryCategory category, string entryId, string title)`
- L82 [function] `public void Rename(string title)`
- L90 [function] `public bool AddInfo(string text, CodexInfoSource source)`
- L108 [function] `public CodexEntrySnapshot ToSnapshot()`
- L121 [type] `public sealed class CodexState`
- L127 [function] `public CodexEntryRecord GetOrCreate(CodexEntryCategory category, string entryId, string title)`
- L143 [function] `public bool AddInfo(CodexEntryCategory category, string entryId, string title, string info, CodexInfoSource source)`
- L148 [function] `public bool HasInfo(CodexEntryCategory category, string entryId, string info)`
- L155 [function] `public IReadOnlyList<CodexEntrySnapshot> GetSnapshots(CodexEntryCategory category)`
- L164 [function] `public CodexEntrySnapshot GetSnapshot(CodexEntryCategory category, string entryId)`
- L171 [function] `private static string GetKey(CodexEntryCategory category, string entryId)`
- L177 [type] `public struct CodexUpdatedEvent`
- L182 [function] `public CodexUpdatedEvent(CodexEntryCategory category, string entryId)`
- L190 [function] `public static void Trigger(CodexEntryCategory category, string entryId)`
- L198 [type] `public static class CodexService`
- L215 [function] `public static void ObserveCharacter(CodexState state, CharacterActor actor)`
- L220 [function] `public static void ObserveSpecies(CodexState state, CharacterSpeciesSO species, CodexInfoSource source)`
- L225 [function] `public static void ObserveFacility(CodexState state, BuildableObject facility, CodexInfoSource source)`
- L230 [function] `public static void ObserveFacility(CodexState state, BuildingSO building, CodexInfoSource source)`
- L235 [function] `public static void RecordDefenseObservation(CodexState state, DefenseActivationReport report)`
- L240 [function] `public static void RecordCombatReport(CodexState state, InvasionCombatReport report)`
- L245 [function] `public static void RecordFacilityDamage(CodexState state, BuildableObject facility)`
- L274 [function] `public static void RecordEvolution(CodexState state, FacilityEvolutionResult result)`
- L287 [function] `public static void SeedBreakthroughIntruder(CodexState state)`
- L294 [type] `public class CodexRuntime :`
- L337 [function] `private void Start()`
- L345 [function] `public void ImportReferenceData()`
- L353 [function] `private IBlueprintResearchStateService ResolveResearchStateService()`
- L359 [function] `private ICodexReferenceImporter ResolveReferenceImporter()`
- L365 [function] `private IFacilitySynthesisRecipeQuery ResolveSynthesisRecipeQuery()`
- L371 [function] `private IFacilityShopCatalog ResolveFacilityShopCatalog()`
- L377 [function] `public IReadOnlyList<CodexEntrySnapshot> GetEntries(CodexEntryCategory category)`
- L382 [function] `public void OnTriggerEvent(FacilityVisitEvent eventType)`
- L390 [function] `public void OnTriggerEvent(DefenseFacilityTriggeredEvent eventType)`
- L397 [function] `public void OnTriggerEvent(InvasionCombatReportReadyEvent eventType)`
- L403 [function] `public void OnTriggerEvent(InvasionFacilityDamagedEvent eventType)`
- L409 [function] `public void OnTriggerEvent(InvasionSpawnedEvent eventType)`
- L416 [function] `public void OnTriggerEvent(BlueprintResearchCompletedEvent eventType)`
- L427 [function] `public void OnTriggerEvent(FacilitySynthesisCompletedEvent eventType)`
- L433 [function] `public void OnTriggerEvent(FacilityEvolutionCompletedEvent eventType)`
- L439 [function] `private void OnEnable()`
- L451 [function] `private void OnDisable()`

### Assets\Scripts\Codex\CodexTextFormatter.cs

- Lines: 158
- Flags: None

- L5 [type] `public static class CodexTextFormatter`
- L7 [function] `public static string FormatEvolutionMutationTags(IReadOnlyList<string> mutationTags)`
- L19 [function] `public static string FormatFacilityRoles(FacilityRole roles)`
- L27 [function] `public static string FormatWorkTypes(FacilityWorkType workTypes)`
- L35 [function] `public static string FormatDefenseConcept(DefenseAttackConcept concept)`
- L49 [function] `public static string FormatTriggerTimings(DefenseTriggerTiming timings)`
- L69 [function] `public static string FormatTargetRule(DefenseTargetRule targetRule)`
- L81 [function] `public static IEnumerable<string> FormatDefenseEffects(DefenseFacilityData defense)`
- L94 [function] `private static string FormatFacilityRole(FacilityRole role)`
- L111 [function] `private static string FormatWorkType(FacilityWorkType workType)`
- L127 [function] `private static string FormatEffect(DefenseEffectKind kind, float amount, float duration, int stacks)`

### Assets\Scripts\Codex\UI\CodexPanel.cs

- Lines: 120
- Flags: EventBus, DependencyInjection

- L7 [type] `public class CodexPanel : MonoBehaviour, UtilEventListener<CodexUpdatedEvent>`
- L16 [function] `public void Construct(ICodexRuntimeProvider runtimeProvider)`
- L22 [function] `public void Bind(CodexRuntime nextRuntime)`
- L28 [function] `internal void BindGeneratedView(TMP_Text summaryText)`
- L35 [function] `public void Refresh()`
- L47 [function] `public void OnTriggerEvent(CodexUpdatedEvent eventType)`
- L87 [function] `private static IEnumerable<CodexInfoLine> GetSummaryLines(CodexInfoLine[] lines, int maxLines)`
- L94 [function] `private void ApplyText()`
- L102 [function] `private CodexRuntime ResolveRuntime()`
- L111 [function] `private void OnEnable()`
- L116 [function] `private void OnDisable()`

### Assets\Scripts\Data.cs

- Lines: 50
- Flags: None

- L8 [type] `public class Data<T>`
- L22 [function] `public void Initialize(T t)`
- L28 [type] `public class DataList<T>`
- L40 [function] `public void Add(T item)`
- L45 [function] `public void Remove(T item)`

### Assets\Scripts\Defense\DefenseEffectSO.cs

- Lines: 49
- Flags: None

- L4 [type] `public class DefenseEffectSO : ScriptableObject`

### Assets\Scripts\Defense\DefenseFacilitySystem.cs

- Lines: 481
- Flags: EventBus

- L7 [type] `public enum DefenseTriggerTiming`
- L16 [type] `public enum DefenseAttackConcept`
- L27 [type] `public enum DefenseTargetRule`
- L35 [type] `public enum DefenseEffectKind`
- L45 [type] `public enum DefenseStatusKind`
- L54 [type] `public class DefenseEffectData`
- L62 [function] `public DefenseEffectData Clone()`
- L76 [type] `public class DefenseFacilityData`
- L92 [function] `public bool SupportsTrigger(DefenseTriggerTiming timing)`
- L98 [type] `public class DefenseActivationReport`
- L102 [function] `public DefenseActivationReport(DefenseFacility facility, CharacterActor target, DefenseTriggerTiming timing)`
- L121 [function] `public void AddDamage(float amount)`
- L126 [function] `public void AddMovementDelay(float seconds)`
- L131 [function] `public void AddEffectTag(string tag)`
- L139 [function] `public string FormatSummary()`
- L150 [type] `public struct DefenseFacilityTriggeredEvent`
- L154 [function] `public DefenseFacilityTriggeredEvent(DefenseActivationReport report)`
- L161 [function] `public static void Trigger(DefenseActivationReport report)`
- L168 [type] `public class DefenseFacility : Facility`
- L174 [function] `public bool CanTrigger(DefenseTriggerTiming timing, out string failureReason)`
- L231 [type] `public static class DefenseFacilityResolver`
- L274 [type] `public static class DefenseEffectResolver`
- L407 [type] `public class DefenseStatusRuntime : MonoBehaviour`
- L411 [function] `public int ApplyStatus(DefenseStatusKind kind, float value, float duration, int stacks)`
- L428 [function] `public void ClearStatus(DefenseStatusKind kind)`
- L433 [function] `public float GetIncomingDamageMultiplier()`
- L439 [function] `public float Tick(CharacterActor target, float deltaSeconds)`
- L469 [type] `private sealed class DefenseRuntimeStatus`
- L471 [function] `public DefenseRuntimeStatus(DefenseStatusKind kind)`

### Assets\Scripts\Defense\DefenseStatusRuntimeFactory.cs

- Lines: 32
- Flags: RuntimeObjectCreation, SceneMutation

- L3 [type] `public interface IDefenseStatusRuntimeFactory`
- L9 [type] `public sealed class DefenseStatusRuntimeFactory : IDefenseStatusRuntimeFactory`
- L11 [function] `public DefenseStatusRuntime GetOrAdd(CharacterActor character)`
- L26 [function] `public DefenseStatusRuntime Get(CharacterActor character)`

### Assets\Scripts\Defense\DefenseStatusRuntimeService.cs

- Lines: 42
- Flags: None

- L3 [type] `public interface IDefenseStatusRuntimeService`
- L10 [type] `public sealed class DefenseStatusRuntimeService : IDefenseStatusRuntimeService`
- L14 [function] `public DefenseStatusRuntimeService(IDefenseStatusRuntimeFactory runtimeFactory)`
- L20 [function] `public DefenseStatusRuntime GetOrAdd(CharacterActor character)`
- L25 [function] `public DefenseStatusRuntime Get(CharacterActor character)`
- L30 [function] `public float TickStatuses(CharacterActor target, float deltaSeconds)`

### Assets\Scripts\FacilityEvolution\FacilityEvolutionIdentity.cs

- Lines: 401
- Flags: None

- L6 [type] `public interface IFacilityIdentityPressureProvider`
- L11 [type] `public readonly struct FacilityEvolutionIdentityScore`
- L31 [function] `public string ToMessage()`
- L45 [type] `public sealed class FacilityIdentitySnapshot`
- L47 [function] `public FacilityIdentitySnapshot(FacilityEvolutionContext context)`
- L93 [function] `private static string BuildRoomSummary(RoomProfile profile)`
- L103 [function] `private static IReadOnlyDictionary<string, float> TopPairs(IReadOnlyDictionary<string, float> values)`
- L117 [function] `private static IReadOnlyDictionary<string, int> TopTokenPairs(IReadOnlyDictionary<string, int> values)`
- L132 [type] `public sealed class DefaultFacilityIdentityPressureProvider : IFacilityIdentityPressureProvider`
- L134 [function] `public void Apply(RoomProfile profile)`
- L140 [type] `public static class FacilityIdentityPressureUtility`
- L142 [function] `public static void ApplyDefaultPressures(RoomProfile profile)`
- L213 [function] `private static void ApplyPressure(RoomProfile profile, string key, IEnumerable<IdentitySignal> signals)`
- L236 [function] `private static IEnumerable<IdentitySignal> CrowdSignals(RoomProfile profile)`
- L245 [function] `private static IEnumerable<IdentitySignal> LuxurySignals(RoomProfile profile)`
- L258 [function] `private static float CoreLuxuryStrength(RoomProfile profile)`
- L268 [function] `private static IEnumerable<IdentitySignal> CombatSignals(RoomProfile profile)`
- L280 [function] `private static IEnumerable<IdentitySignal> OutlawSignals(RoomProfile profile)`
- L290 [function] `private static IEnumerable<IdentitySignal> RestSignals(RoomProfile profile)`
- L300 [function] `private static IEnumerable<IdentitySignal> ServiceSignals(RoomProfile profile)`
- L310 [function] `private static IEnumerable<IdentitySignal> RitualSignals(RoomProfile profile)`
- L319 [function] `private static IEnumerable<IdentitySignal> SecuritySignals(RoomProfile profile)`
- L329 [function] `private static IEnumerable<IdentitySignal> LogisticsSignals(RoomProfile profile)`
- L338 [function] `private static void DetectConflicts(RoomProfile profile)`
- L345 [function] `private static void AddConflict(RoomProfile profile, string a, string b, float threshold)`
- L355 [function] `private static IdentitySignal Signal(string label, float value)`
- L360 [function] `private static float Token01(RoomProfile profile, string key, int atCount)`
- L365 [function] `private static float Normalize(float value, float min, float max)`
- L375 [function] `private static float InverseNormalize(float value, float best, float worst)`
- L390 [type] `private readonly struct IdentitySignal`
- L392 [function] `public IdentitySignal(string label, float value)`

### Assets\Scripts\FacilityEvolution\FacilityEvolutionLlmProposalProvider.cs

- Lines: 520
- Flags: None

- L8 [type] `public sealed class FacilityEvolutionProposalReasonDto`
- L15 [type] `public sealed class FacilityEvolutionProposalJsonDto : ILlmJsonPayload`
- L26 [function] `public bool Validate(out string error)`
- L183 [type] `public sealed class CachedLocalLlmFacilityEvolutionProposalProvider : IFacilityEvolutionProposalProvider`
- L207 [function] `public FacilityEvolutionProposal Propose(FacilityEvolutionContext context)`
- L237 [function] `private void TryRequestProposal(string signature, FacilityEvolutionContext context)`
- L304 [function] `private void SetStatus(string signature, string message)`
- L329 [type] `public static class FacilityEvolutionPromptFormatter`
- L331 [function] `public static string BuildSignature(FacilityEvolutionContext context)`
- L350 [function] `public static string BuildPrompt(FacilityEvolutionContext context)`
- L405 [function] `private static void AppendPairs(StringBuilder builder, IReadOnlyDictionary<string, float> values)`
- L418 [function] `private static void AppendTokenPairs(StringBuilder builder, IReadOnlyDictionary<string, int> values)`
- L431 [function] `private static void AppendList(StringBuilder builder, IEnumerable<string> values)`
- L442 [function] `private static string JoinValues(IEnumerable<string> values)`
- L452 [function] `private static string JoinPairs(IReadOnlyDictionary<string, float> values)`
- L465 [function] `private static string JoinTokenPairs(IReadOnlyDictionary<string, int> values)`
- L478 [function] `private static string JoinRequirements(FacilityEvolutionMetricRequirement[] requirements)`
- L496 [function] `private static string JoinTokenRequirements(FacilityEvolutionTokenRequirement[] requirements)`
- L508 [function] `private static string JoinIdentityWeights(FacilityEvolutionValue[] weights)`

### Assets\Scripts\FacilityEvolution\FacilityEvolutionMutations.cs

- Lines: 192
- Flags: None

- L6 [type] `public readonly struct FacilityEvolutionMutationResult`
- L20 [type] `public interface IFacilityEvolutionMutationResolver`
- L28 [type] `public sealed class DefaultFacilityEvolutionMutationResolver : IFacilityEvolutionMutationResolver`
- L106 [function] `private static bool HasEvidence(RoomProfile profile, string tag)`
- L111 [function] `private static float GetTagEvidenceScore(RoomProfile profile, string tag)`

### Assets\Scripts\FacilityEvolution\FacilityEvolutionBuildingReplacerFactory.cs

- Lines: 44
- Flags: RuntimeObjectCreation, DependencyInjection

- L4 [type] `public interface IFacilityEvolutionBuildingReplacerFactory`
- L9 [type] `public sealed class GridFacilityEvolutionBuildingReplacerFactory : IFacilityEvolutionBuildingReplacerFactory`
- L15 [function] `public GridFacilityEvolutionBuildingReplacerFactory(IGridTextureProvider gridTextureProvider, IGridBuildingObjectFactory gridBuildingObjectFactory, IObjectResolver objectResolver)`
- L28 [function] `public IFacilityEvolutionBuildingReplacer Create()`
- L37 [function] `private void InjectCreatedBuilding(BuildableObject building)`

### Assets\Scripts\FacilityEvolution\FacilityEvolutionRecipeSO.cs

- Lines: 72
- Flags: None

- L6 [type] `public class FacilityEvolutionRecipeSO : DataScriptableObject`
- L51 [function] `public bool MatchesSource(BuildableObject facility, FacilityEvolutionStateComponent state)`

### Assets\Scripts\FacilityEvolution\FacilityEvolutionRecordComponentFactory.cs

- Lines: 23
- Flags: RuntimeObjectCreation, SceneMutation

- L1 [type] `public interface IFacilityEvolutionRecordComponentFactory`
- L6 [type] `public sealed class FacilityEvolutionRecordComponentFactory : IFacilityEvolutionRecordComponentFactory`
- L8 [function] `public FacilityEvolutionRecordComponent GetOrAdd(BuildableObject facility)`

### Assets\Scripts\FacilityEvolution\FacilityEvolutionRecord.cs

- Lines: 315
- Flags: None

- L6 [type] `public interface IFacilityEvolutionRecordProvider`
- L11 [type] `public interface IFacilityEvolutionRecordComponentService : IFacilityEvolutionRecordProvider`
- L17 [type] `public sealed class FacilityEvolutionRecord`
- L27 [function] `public float GetMetric(string key)`
- L32 [function] `public int GetToken(string key)`
- L37 [function] `public void AddMetric(string key, float value)`
- L47 [function] `public void AddToken(string key, int count)`
- L58 [function] `public void SetToken(string key, int count)`
- L68 [function] `public bool TryConsumeToken(string key, int count, out string reason)`
- L88 [function] `public void AddEvent(string text)`
- L96 [function] `public bool TryConsumeTokens(IEnumerable<FacilityEvolutionTokenRequirement> requirements, out string reason)`
- L130 [function] `public FacilityEvolutionRecord Clone()`
- L152 [type] `public class FacilityEvolutionRecordComponent : MonoBehaviour, IFacilityEvolutionRecordProvider`
- L158 [function] `public FacilityEvolutionRecord GetRecord(BuildableObject facility)`
- L188 [function] `public void SetMetric(string key, float value)`
- L204 [function] `public void AddToken(string key, int count)`
- L220 [function] `public void AddRecentEvent(string text)`
- L233 [function] `public void ReplaceWith(FacilityEvolutionRecord record)`
- L257 [type] `public sealed class ComponentFacilityEvolutionRecordProvider : IFacilityEvolutionRecordProvider`
- L259 [function] `public FacilityEvolutionRecord GetRecord(BuildableObject facility)`
- L271 [type] `public sealed class FacilityEvolutionRecordComponentService : IFacilityEvolutionRecordComponentService`
- L275 [function] `public FacilityEvolutionRecordComponentService(IFacilityEvolutionRecordComponentFactory recordComponentFactory)`
- L282 [function] `public FacilityEvolutionRecord GetRecord(BuildableObject facility)`
- L293 [function] `public FacilityEvolutionRecordComponent GetOrAdd(BuildableObject facility)`
- L298 [function] `public void ReplaceWith(BuildableObject facility, FacilityEvolutionRecord record)`

### Assets\Scripts\FacilityEvolution\FacilityEvolutionRecordEventRecorder.cs

- Lines: 421
- Flags: DependencyInjection

- L5 [type] `public interface IFacilityEvolutionRecordEventRecorder`
- L17 [type] `public sealed class FacilityEvolutionRecordEventRecorder : IFacilityEvolutionRecordEventRecorder`
- L29 [function] `public FacilityEvolutionRecordEventRecorder(IFacilityCandidateCache facilityCandidateCache, IFacilityEvolutionRecordComponentService recordComponentService)`
- L39 [function] `public void RecordVisit(FacilityVisitEvent eventType, int highTurnoverVisitStep)`
- L104 [function] `public void RecordRevenue(FacilityRevenueEvent eventType, int highValueRevenueThreshold)`
- L129 [function] `public void RecordStockConsumed(FacilityStockConsumedEvent eventType)`
- L152 [function] `public void RecordCrime(FacilityCrimeEvent eventType)`
- L181 [function] `public void RecordRestock(FacilityRestockEvent eventType)`
- L201 [function] `public void RecordDefenseTriggered(DefenseFacilityTriggeredEvent eventType)`
- L227 [function] `public void RecordInvasionDamage(InvasionFacilityDamagedEvent eventType)`
- L243 [function] `public void CompleteOperatingDay(int cleanServiceMinVisits)`
- L265 [function] `private FacilityDayRecord GetDayRecord(BuildableObject facility)`
- L277 [function] `private void MarkDynamicStateDirty()`
- L282 [function] `private HashSet<int> GetUniqueVisitors(BuildableObject facility)`
- L294 [function] `private FacilityEvolutionRecordComponent GetOrAddRecord(BuildableObject facility)`
- L299 [function] `private static void IncrementMetric(FacilityEvolutionRecordComponent record, string key, float delta)`
- L309 [function] `private static void SetMetric(FacilityEvolutionRecordComponent record, string key, float value)`
- L314 [function] `private static float GetMetric(FacilityEvolutionRecordComponent record, string key)`
- L319 [function] `private static float UpdateAverage(float previousAverage, float previousCount, float nextCount, float value)`
- L329 [function] `private static float UpdateRatio(float previousRatio, float previousTotal, float nextTotal, bool increment)`
- L340 [function] `private static bool IsValidFacility(BuildableObject facility)`
- L345 [function] `private static int GetFacilityKey(BuildableObject facility)`
- L350 [function] `private static int GetVisitorId(CharacterActor actor)`
- L357 [function] `private static bool SupportsRole(BuildableObject facility, FacilityRole role)`
- L362 [function] `private static bool IsCombatVisitor(CharacterActor actor)`
- L376 [function] `private static bool IsNobleVisitor(CharacterActor actor)`
- L389 [function] `private static float GetCondition(CharacterActor actor, CharacterCondition condition, float defaultValue)`
- L399 [function] `private static string GetActorName(CharacterActor actor)`
- L404 [function] `private static string GetFacilityName(BuildableObject facility)`
- L409 [type] `private sealed class FacilityDayRecord`
- L411 [function] `public FacilityDayRecord(BuildableObject facility)`

### Assets\Scripts\FacilityEvolution\FacilityEvolutionRecordRuntime.cs

- Lines: 98
- Flags: EventBus, DependencyInjection

- L5 [type] `public class FacilityEvolutionRecordRuntime :`
- L23 [function] `public void Construct(IFacilityEvolutionRecordEventRecorder recordEventRecorder)`
- L29 [function] `public void OnTriggerEvent(FacilityVisitEvent eventType)`
- L34 [function] `public void OnTriggerEvent(FacilityRevenueEvent eventType)`
- L39 [function] `public void OnTriggerEvent(FacilityStockConsumedEvent eventType)`
- L44 [function] `public void OnTriggerEvent(FacilityCrimeEvent eventType)`
- L49 [function] `public void OnTriggerEvent(FacilityRestockEvent eventType)`
- L54 [function] `public void OnTriggerEvent(DefenseFacilityTriggeredEvent eventType)`
- L59 [function] `public void OnTriggerEvent(InvasionFacilityDamagedEvent eventType)`
- L64 [function] `public void OnTriggerEvent(OperatingDayEndedEvent eventType)`
- L69 [function] `private void OnEnable()`
- L81 [function] `private void OnDisable()`
- L93 [function] `private IFacilityEvolutionRecordEventRecorder ResolveRecordEventRecorder()`

### Assets\Scripts\FacilityEvolution\FacilityEvolutionRecordTokens.cs

- Lines: 144
- Flags: None

- L6 [type] `public enum FacilityEvolutionRecordTokenConsumePolicy`
- L14 [type] `public class FacilityEvolutionRecordTokenDefinitionSO : DataScriptableObject`
- L35 [type] `public interface IFacilityEvolutionRecordTokenDefinitionProvider`
- L41 [type] `public sealed class EmptyFacilityEvolutionRecordTokenDefinitionProvider :`
- L44 [function] `public IReadOnlyList<FacilityEvolutionRecordTokenDefinitionSO> GetDefinitions()`
- L49 [function] `public FacilityEvolutionRecordTokenDefinitionSO GetDefinition(string tokenId)`
- L55 [type] `public interface IFacilityEvolutionRecordTokenConsumer`
- L64 [type] `public sealed class DefaultFacilityEvolutionRecordTokenConsumer : IFacilityEvolutionRecordTokenConsumer`

### Assets\Scripts\FacilityEvolution\FacilityEvolutionRuntime.cs

- Lines: 764
- Flags: EventBus, SceneMutation, DependencyInjection

- L7 [type] `public interface IFacilityEvolutionRecipeProvider`
- L12 [type] `public interface IFacilityEvolutionBuildingReplacer`
- L18 [type] `public sealed class GridFacilityEvolutionBuildingReplacer : IFacilityEvolutionBuildingReplacer`
- L22 [function] `public GridFacilityEvolutionBuildingReplacer(GridBuildingFactory buildingFactory)`
- L28 [function] `public bool CanReplace(BuildableObject source, BuildingSO resultBuilding, out string reason)`
- L69 [function] `public bool TryReplace(BuildableObject source, BuildingSO resultBuilding, out BuildableObject result, out string reason)`
- L116 [type] `public readonly struct FacilityEvolutionResult`
- L148 [type] `public struct FacilityEvolutionCompletedEvent`
- L152 [function] `public FacilityEvolutionCompletedEvent(FacilityEvolutionResult result)`
- L159 [function] `public static void Trigger(FacilityEvolutionResult result)`
- L166 [type] `public interface IFacilityEvolutionValidator`
- L176 [type] `public sealed class DefaultFacilityEvolutionValidator : IFacilityEvolutionValidator`
- L225 [type] `public interface IFacilityEvolutionCandidateBuilder`
- L237 [type] `public sealed class DefaultFacilityEvolutionCandidateBuilder : IFacilityEvolutionCandidateBuilder`
- L241 [function] `public DefaultFacilityEvolutionCandidateBuilder(IFacilityEvolutionValidator validator)`
- L293 [type] `public sealed class FacilityEvolutionEngine`
- L311 [function] `public FacilityEvolutionEngine(IFacilityEvolutionRecipeQuery recipeQuery, IRoomProfileProvider roomProfileProvider, IFacilityEvolutionRecordProvider recordProvider, IFacilityEvolutionProposalProvider proposalProvider, IFacilityEvolutionResourceProvider resourceProvider, IFacilityEvolutionBuildingReplacer buildingReplacer, IRoomLayoutCache roomLayoutCache, IFacilityEvolutionStateService stateService, IFacilityCandidateCache facilityCandidateCache, Func<BlueprintResearchState> researchStateProvider, IFacilityEvolutionValidator validator = null, IFacilityEvolutionCandidateBuilder candidateBuilder = null, IFacilityEvolutionRecordTokenConsumer recordTokenConsumer = null, IFacilityEvolutionRecordComponentService recordComponentService = null, IFacilityEvolutionMutationResolver mutationResolver = null)`
- L359 [function] `public FacilityEvolutionContext BuildContext(BuildableObject facility)`
- L368 [function] `public IReadOnlyList<FacilityEvolutionCandidate> GetCandidates(BuildableObject facility, bool includeRejected = false)`
- L396 [function] `public bool TryEvolve(BuildableObject facility, FacilityEvolutionRecipeSO recipe, out FacilityEvolutionResult result)`
- L482 [function] `private static IReadOnlyDictionary<string, int> BuildProposalOrder(FacilityEvolutionProposal proposal)`
- L502 [function] `private bool ConsumeMaterials(FacilityEvolutionMaterialRequirement[] requirements, out string reason)`
- L523 [function] `private static FacilityEvolutionResult Fail(FacilityEvolutionRecipeSO recipe, BuildableObject facility, FacilityEvolutionProposal proposal, string message)`
- L540 [type] `public class FacilityEvolutionRuntime : MonoBehaviour`
- L565 [function] `public void ConstructFacilityEvolutionRuntime(IBlueprintResearchStateService blueprintResearchStateService, ILocalLlmRuntimeProvider llmRuntimeProvider, IRoomLayoutCache roomLayoutCache, IFacilityEvolutionStateService stateService, IFacilityCandidateCache facilityCandidateCache, IFacilityEvolutionRecipeQuery recipeQuery, IFacilityEvolutionRecordTokenConsumer recordTokenConsumer, IFacilityEvolutionRecordComponentService recordComponentService, IFacilityEvolutionBuildingReplacerFactory buildingReplacerFactory)`
- L606 [function] `public void Configure(IFacilityEvolutionRecipeQuery nextRecipeQuery = null, IRoomProfileProvider nextRoomProfileProvider = null, IFacilityEvolutionRecordProvider nextRecordProvider = null, IFacilityEvolutionProposalProvider nextProposalProvider = null, IFacilityEvolutionResourceProvider nextResourceProvider = null, IFacilityEvolutionBuildingReplacer nextBuildingReplacer = null, IRoomLayoutCache nextRoomLayoutCache = null, IFacilityEvolutionStateService nextStateService = null, IFacilityCandidateCache nextFacilityCandidateCache = null, IFacilityEvolutionValidator nextValidator = null, IFacilityEvolutionCandidateBuilder nextCandidateBuilder = null, IFacilityEvolutionRecordTokenConsumer nextRecordTokenConsumer = null, IFacilityEvolutionRecordComponentService nextRecordComponentService = null, IBlueprintResearchStateService nextResearchStateService = null, IFacilityEvolutionBuildingReplacerFactory nextBuildingReplacerFactory = null, IFacilityEvolutionMutationResolver nextMutationResolver = null)`
- L643 [function] `public FacilityEvolutionContext BuildContext(BuildableObject facility)`
- L648 [function] `public IReadOnlyList<FacilityEvolutionCandidate> GetCandidates(BuildableObject facility, bool includeRejected = false)`
- L655 [function] `public bool TryEvolve(BuildableObject facility, FacilityEvolutionRecipeSO recipe, out FacilityEvolutionResult result)`
- L679 [function] `private FacilityEvolutionEngine CreateEngine()`
- L708 [function] `private IFacilityEvolutionProposalProvider CreateDefaultProposalProvider()`
- L716 [function] `private ILocalLlmRuntime ResolveLocalLlmRuntime()`
- L728 [function] `private IBlueprintResearchStateService ResolveResearchStateService()`
- L734 [function] `private IRoomLayoutCache ResolveRoomLayoutCache()`
- L740 [function] `private IFacilityEvolutionStateService ResolveStateService()`
- L746 [function] `private IFacilityCandidateCache ResolveFacilityCandidateCache()`
- L752 [function] `private IFacilityEvolutionRecordComponentService ResolveRecordComponentService()`
- L759 [function] `private IFacilityEvolutionBuildingReplacerFactory ResolveBuildingReplacerFactory()`

### Assets\Scripts\FacilityEvolution\FacilityEvolutionService.cs

- Lines: 731
- Flags: None

- L6 [type] `public interface IFacilityEvolutionResourceProvider`
- L12 [type] `public interface IFacilityEvolutionProposalProvider`
- L17 [type] `public readonly struct FacilityEvolutionProposal`
- L53 [type] `public static class FacilityEvolutionProposalSources`
- L61 [type] `public sealed class RuleBasedFacilityEvolutionProposalProvider : IFacilityEvolutionProposalProvider`
- L63 [function] `public FacilityEvolutionProposal Propose(FacilityEvolutionContext context)`
- L104 [function] `private static string BuildReason(RoomProfile profile, FacilityEvolutionRecipeSO recipe)`
- L152 [function] `private static string BuildRejectedHint(RoomProfile profile, FacilityEvolutionRecipeSO recipe)`
- L183 [function] `private static string BuildSummary(FacilityEvolutionContext context)`
- L217 [function] `private static IReadOnlyList<string> GuessMutationTags(RoomProfile profile)`
- L240 [type] `public sealed class EmptyFacilityEvolutionResourceProvider : IFacilityEvolutionResourceProvider`
- L242 [function] `public bool HasMaterial(string materialId, int amount)`
- L247 [function] `public bool ConsumeMaterial(string materialId, int amount)`
- L253 [type] `public sealed class MemoryFacilityEvolutionResourceProvider : IFacilityEvolutionResourceProvider`
- L257 [function] `public void SetMaterial(string materialId, int amount)`
- L265 [function] `public bool HasMaterial(string materialId, int amount)`
- L275 [function] `public bool ConsumeMaterial(string materialId, int amount)`
- L291 [type] `public sealed class FacilityEvolutionContext`
- L311 [type] `public sealed class FacilityEvolutionValidationResult`
- L320 [function] `public void Reject(string reason)`
- L329 [function] `public void AddCheck(string category, string label, bool passed, string detail = "")`
- L339 [function] `public string ToMessage()`
- L345 [type] `public readonly struct FacilityEvolutionValidationCheck`
- L365 [type] `public sealed class FacilityEvolutionCandidate`
- L421 [type] `public static class FacilityEvolutionUtility`
- L423 [function] `public static string GetFacilityId(BuildingSO building)`
- L433 [function] `public static IEnumerable<string> GetDefaultLineageTags(BuildingSO building)`
- L461 [type] `public static class FacilityEvolutionService`

### Assets\Scripts\FacilityEvolution\FacilityEvolutionStateComponentFactory.cs

- Lines: 24
- Flags: RuntimeObjectCreation, SceneMutation

- L1 [type] `public interface IFacilityEvolutionStateComponentFactory`
- L6 [type] `public sealed class FacilityEvolutionStateComponentFactory : IFacilityEvolutionStateComponentFactory`
- L8 [function] `public FacilityEvolutionStateComponent GetOrAdd(BuildableObject facility)`

### Assets\Scripts\FacilityEvolution\FacilityEvolutionState.cs

- Lines: 243
- Flags: None

- L7 [type] `public class FacilityEvolutionHistoryEntry`
- L15 [function] `public FacilityEvolutionHistoryEntry Clone()`
- L28 [type] `public sealed class FacilityEvolutionStateSnapshot`
- L41 [type] `public class FacilityEvolutionStateComponent : MonoBehaviour`
- L64 [function] `public FacilityEvolutionStateSnapshot CreateSnapshot()`
- L83 [function] `public void ApplySnapshot(FacilityEvolutionStateSnapshot snapshot)`
- L120 [function] `public void InitializeIfNeeded(BuildableObject facility)`
- L201 [function] `private void CaptureIdentity(RoomProfile profile)`
- L224 [type] `public interface IFacilityEvolutionStateService`
- L229 [type] `public sealed class FacilityEvolutionStateService : IFacilityEvolutionStateService`
- L233 [function] `public FacilityEvolutionStateService(IFacilityEvolutionStateComponentFactory stateComponentFactory)`
- L239 [function] `public FacilityEvolutionStateComponent GetOrAdd(BuildableObject facility)`

### Assets\Scripts\FacilityEvolution\FacilityEvolutionTerms.cs

- Lines: 219
- Flags: None

- L5 [type] `public static class FacilityEvolutionTerms`
- L115 [type] `public struct FacilityEvolutionValue`
- L120 [function] `public FacilityEvolutionValue(string key, float value)`
- L128 [type] `public struct FacilityEvolutionTokenValue`
- L133 [function] `public FacilityEvolutionTokenValue(string key, int count)`
- L141 [type] `public class FacilityEvolutionContributionData`
- L155 [type] `public struct FacilityEvolutionMetricRequirement`
- L163 [function] `public bool IsSatisfied(IReadOnlyDictionary<string, float> values, out string reason)`
- L189 [type] `public struct FacilityEvolutionTokenRequirement`
- L194 [function] `public bool IsSatisfied(IReadOnlyDictionary<string, int> values, out string reason)`
- L215 [type] `public struct FacilityEvolutionMaterialRequirement`

### Assets\Scripts\FacilityEvolution\RoomProfile.cs

- Lines: 394
- Flags: None

- L6 [type] `public interface IRoomProfileProvider`
- L11 [type] `public sealed class RoomProfile`
- L23 [function] `public RoomProfile(BuildableObject facility, RoomInstance room)`
- L46 [function] `public float GetScore(string key)`
- L51 [function] `public float GetMetric(string key)`
- L56 [function] `public float GetIdentityPressure(string key)`
- L61 [function] `public int GetToken(string key)`
- L66 [function] `public bool HasTag(string tag)`
- L71 [function] `public void AddTag(string tag)`
- L79 [function] `public void AddScore(string key, float value)`
- L90 [function] `public void AddMetric(string key, float value)`
- L100 [function] `public void AddMetricDelta(string key, float value)`
- L111 [function] `public void SetIdentityPressure(string key, float value)`
- L121 [function] `public void AddRecordToken(string key, int count)`
- L132 [function] `public void AddFixture(BuildableObject fixture)`
- L140 [function] `public void AddRecentEvent(string text)`
- L148 [function] `public void AddDominantSignal(string text)`
- L156 [function] `public void AddConflictingSignal(string text)`
- L165 [type] `public sealed class RoomProfileBuilder : IRoomProfileProvider`
- L180 [function] `public RoomProfile Build(BuildableObject facility)`
- L198 [function] `private static IEnumerable<BuildableObject> CollectFixtures(BuildableObject facility, RoomInstance room)`
- L226 [function] `private static void AddFixtureContribution(RoomProfile profile, BuildableObject fixture)`
- L267 [function] `private static void AddRoleContribution(RoomProfile profile, FacilityData facility)`
- L325 [function] `private static void AddDefenseContribution(RoomProfile profile, DefenseFacilityData defense)`
- L338 [function] `private static void AddRecordContribution(RoomProfile profile, FacilityEvolutionRecord record)`
- L361 [function] `private static void CalculateDerivedMetrics(RoomProfile profile)`

### Assets\Scripts\FacilityEvolution\UI\FacilityEvolutionPanel.cs

- Lines: 281
- Flags: EventBus, DependencyInjection

- L7 [type] `public class FacilityEvolutionPanel :`
- L24 [function] `public void Construct(IFacilityEvolutionRuntimeProvider runtimeProvider)`
- L30 [function] `public void Bind(FacilityEvolutionRuntime nextRuntime)`
- L36 [function] `internal void BindGeneratedView(TMP_Text summaryText)`
- L43 [function] `public void SelectFacility(BuildableObject facility)`
- L49 [function] `public bool TryEvolve(string evolutionId, out FacilityEvolutionResult result)`
- L76 [function] `public bool TryEvolveFirstApproved(out FacilityEvolutionResult result)`
- L91 [function] `public void Refresh()`
- L181 [function] `public void OnTriggerEvent(InfoFeedEvent eventType)`
- L191 [function] `public void OnTriggerEvent(BlueprintResearchCompletedEvent eventType)`
- L196 [function] `public void OnTriggerEvent(FacilityEvolutionCompletedEvent eventType)`
- L206 [function] `private void ApplyText()`
- L214 [function] `private static string FormatList(IEnumerable<string> values)`
- L223 [function] `private static IEnumerable<string> FormatChecks(FacilityEvolutionValidationResult validation)`
- L243 [function] `private static string FormatPressures(IReadOnlyDictionary<string, float> values)`
- L259 [function] `private FacilityEvolutionRuntime ResolveRuntime()`
- L268 [function] `private void OnEnable()`
- L275 [function] `private void OnDisable()`

### Assets\Scripts\FacilityShop\FacilityBlueprintSO.cs

- Lines: 17
- Flags: None

- L5 [type] `public class FacilityBlueprintSO : DataScriptableObject`

### Assets\Scripts\FacilityShop\FacilityShopSystem.cs

- Lines: 727
- Flags: EventBus, DependencyInjection

- L7 [type] `public enum FacilityShopOfferType`
- L13 [type] `public enum FacilityShopRarity`
- L21 [type] `public class FacilityShopOfferSnapshot`
- L30 [function] `public string ToSummaryText()`
- L40 [type] `public class FacilityShopOffer`
- L83 [function] `public FacilityShopOfferSnapshot ToSnapshot()`
- L97 [type] `public readonly struct FacilityShopPurchaseResult`
- L108 [function] `public FacilityShopPurchaseResult(bool success, FacilityShopOffer offer, int cost, string message)`
- L125 [type] `public struct FacilityShopRefreshedEvent`
- L155 [type] `public struct FacilityShopPurchasedEvent`
- L159 [function] `public FacilityShopPurchasedEvent(FacilityShopPurchaseResult result)`
- L166 [function] `public static void Trigger(FacilityShopPurchaseResult result)`
- L173 [type] `public class FacilityShopUnlockState`
- L181 [function] `public bool UnlockBasicPurchase(BuildingSO building)`
- L191 [function] `public bool UnlockBasicPurchaseById(int buildingId)`
- L201 [function] `public bool IsBasicPurchaseUnlocked(BuildingSO building)`
- L206 [function] `public bool MarkBlueprintAcquired(FacilityBlueprintSO blueprint)`
- L216 [function] `public bool IsBlueprintAcquired(FacilityBlueprintSO blueprint)`
- L222 [type] `public static class FacilityShopService`
- L405 [function] `public static bool CanEnterBasicPurchase(BuildingSO building)`
- L410 [function] `public static int GetBuildingStar(BuildingSO building)`
- L431 [function] `public static string GetBuildingName(BuildingSO building)`
- L441 [function] `public static BuildingSO FindBuildingById(IFacilityShopCatalog catalog, int buildingId)`
- L489 [function] `private static bool IsDailyShopBuildingCandidate(BuildingSO building)`
- L497 [function] `private static FacilityShopRarity ResolveBuildingRarity(BuildingSO building)`
- L536 [function] `private static float GetBuildingCostMultiplier(BuildingSO building, Func<BuildingSO, float> buildingCostMultiplier)`
- L548 [function] `private static string ApplyPurchase(FacilityShopOffer offer, FacilityShopUnlockState unlockState)`
- L568 [type] `public class DailyFacilityShopRuntime : MonoBehaviour, UtilEventListener<OperatingDayEndedEvent>`
- L603 [function] `private void Start()`
- L611 [function] `public void OnTriggerEvent(OperatingDayEndedEvent eventType)`
- L616 [function] `public void Refresh(int day, bool raiseAlert)`
- L638 [function] `public bool TryPurchaseDailyOffer(int index, GameData gameData, out FacilityShopPurchaseResult result)`
- L650 [function] `public bool TryPurchaseBasicOffer(int index, GameData gameData, out FacilityShopPurchaseResult result)`
- L663 [function] `private void OnEnable()`
- L668 [function] `private void OnDisable()`
- L710 [function] `private IFacilityShopCatalog ResolveFacilityShopCatalog()`
- L716 [function] `private IRunVariableRuntimeReader ResolveRunVariableReader()`
- L722 [function] `private IMetaProgressionRuntimeReader ResolveMetaProgressionReader()`

### Assets\Scripts\GameData.cs

- Lines: 14
- Flags: None

- L5 [type] `public class GameData : ScriptableObject`

### Assets\Scripts\GameManager.cs

- Lines: 106
- Flags: None

- L7 [type] `public enum TimeOfDay`
- L15 [type] `public enum NumberCondition`
- L20 [type] `public class GameManager : SerializedMonoBehaviour`
- L26 [function] `private void Awake()`
- L38 [function] `public void ConvertSecondsToGameTime()`
- L45 [function] `public void ChangeGameSpeed()`
- L53 [function] `public void TogglePause()`
- L90 [function] `public IEnumerator Timer()`

### Assets\Scripts\Grid\Building\GridBuildingObjectFactory.cs

- Lines: 48
- Flags: RuntimeObjectCreation, SceneMutation

- L3 [type] `public interface IGridBuildingObjectFactory`
- L8 [type] `public sealed class GridBuildingObjectFactory : IGridBuildingObjectFactory`
- L10 [function] `public BuildableObject Create(Grid grid, BuildingSO buildingData, Vector2Int selectPos)`

### Assets\Scripts\Grid\Building\GridBuildingRuntime.cs

- Lines: 535
- Flags: SceneMutation

- L7 [type] `public class InitialBuildInfo`
- L13 [type] `public class GridBuildingPlacementService`
- L21 [function] `public GridBuildingPlacementService(Grid grid, BuildingSO hallwayBuilding)`
- L26 [function] `public GridBuildingPlacementService(Grid grid, BuildingSO hallwayBuilding, Func<int, BuildingSO> findBuildingData)`
- L36 [function] `public GridBuildingPlacementService(Grid grid, BuildingSO hallwayBuilding, Func<int, BuildingSO> findBuildingData, IGridBuildingFactory buildingFactory, BuildingPlacementValidator placementValidator)`
- L51 [function] `public void SetGrid(Grid grid)`
- L56 [function] `public bool TryPlaceBuilding(BuildingSO buildingData, Vector2Int position, out string errorMessage)`
- L73 [function] `public bool CanPlaceBuilding(BuildingSO buildingData, Vector2Int position)`
- L78 [function] `public bool CanPlaceBuilding(BuildingSO buildingData, Vector2Int position, out string errorMessage)`
- L83 [function] `public bool TryDestroyBuilding(BuildableObject building, out BuildingSO buildingData, out string errorMessage)`
- L117 [function] `public void PlaceInitialBuildings(IEnumerable<InitialBuildInfo> initialPlacement)`
- L137 [function] `private void EnsureHallwayForPassageOverlay(BuildingSO buildingData, Vector2Int position)`
- L153 [function] `private static HashSet<Vector2Int> CollectReservedRoomCells(IReadOnlyList<InitialBuildInfo> placements)`
- L177 [function] `private static bool IsRedundantInitialHallway(InitialBuildInfo item, HashSet<Vector2Int> reservedRoomCells)`
- L185 [function] `private static bool ReservesRoomFootprint(BuildingSO buildingData)`
- L192 [function] `private static bool CanShareHallwayFootprint(BuildingSO buildingData)`
- L205 [function] `private bool PlaceBuildingWithoutValidation(BuildingSO buildingData, Vector2Int position, out string errorMessage)`
- L236 [type] `public interface IGridBuildingVisual`
- L238 [function] `void DrawBuilding(BuildingSO buildingData, Vector2Int position)`
- L239 [function] `void DeleteBuilding(BuildingSO buildingData, Vector2Int position)`
- L242 [type] `public interface IGridBuildingFactory`
- L244 [function] `BuildableObject Create(Grid grid, BuildingSO buildingData, Vector2Int selectPos)`
- L245 [function] `void DeleteVisual(BuildingSO buildingData, Vector2Int selectPos)`
- L248 [type] `public class GridBuildingFactory : IGridBuildingFactory`
- L254 [function] `public GridBuildingFactory(IGridBuildingObjectFactory objectFactory)`
- L259 [function] `public GridBuildingFactory(Action<BuildableObject> onBuildingCreated = null)`
- L264 [function] `public GridBuildingFactory(IGridBuildingVisual buildingVisual, Action<BuildableObject> onBuildingCreated = null)`
- L269 [function] `public GridBuildingFactory(IGridBuildingVisual buildingVisual, Action<BuildableObject> onBuildingCreated, IGridBuildingObjectFactory objectFactory)`
- L279 [function] `public BuildableObject Create(Grid grid, BuildingSO buildingData, Vector2Int selectPos)`
- L290 [function] `public void DeleteVisual(BuildingSO buildingData, Vector2Int selectPos)`
- L297 [function] `private static void ValidateBuildingVisual(BuildingSO buildingData)`
- L307 [function] `private static bool HasTileVisual(BuildingSO buildingData)`
- L314 [type] `public class BuildingPlacementValidator`
- L319 [function] `public BuildingPlacementValidator()`
- L324 [function] `public BuildingPlacementValidator(GridPlacementValidator gridPlacementValidator)`
- L329 [function] `public BuildingPlacementValidator(GridPlacementValidator gridPlacementValidator, Func<BuildingConditionContext> conditionContextFactory)`
- L337 [function] `public bool CanBuild(Grid grid, BuildingSO buildingData, Vector2Int buildPos, out string errorMessage)`
- L383 [function] `public bool CanDestroy(Grid grid, BuildingSO buildingData, BuildableObject building, out string errorMessage)`
- L408 [function] `public void ApplyBuildSuccess(BuildingSO buildingData)`
- L419 [function] `private BuildingConditionContext CreateConditionContext()`
- L427 [type] `public static class GridBuildingExtensions`
- L429 [function] `public static bool CanBuild(this GridCell cell, GridLayer layer = GridLayer.Building)`
- L434 [function] `public static bool HasBuildingInLayer(this GridCell cell, GridLayer layer = GridLayer.Building)`
- L439 [function] `public static bool HasBuilding(this GridCell cell)`
- L444 [function] `public static BuildableObject GetBuildingInlayer(this GridCell cell, GridLayer layer = GridLayer.Building)`
- L449 [function] `public static BuildableObject GetBuilding(this GridCell cell)`
- L454 [function] `public static List<BuildableObject> GetAllBuilding(this GridCell cell)`
- L463 [function] `public static List<BuildableObject> GetAllVisitableBuilding(this GridPathSearchResult searchResult)`
- L472 [function] `public static List<BuildableObject> GetAllReachableBuilding(this GridPathSearchResult searchResult)`
- L481 [function] `public static List<BuildableObject> GetAllVisitableBuilding(this Grid grid, Vector2Int start)`
- L490 [function] `public static List<BuildableObject> GetAllReachableBuilding(this Grid grid, Vector2Int start)`
- L499 [function] `public static bool IsConneted(this Grid grid, Vector2Int start, int id)`
- L504 [function] `public static bool IsConnected(this Grid grid, Vector2Int start, int id)`
- L515 [function] `public static List<BuildableObject> FindAllBuilding(this Grid grid, int id)`
- L524 [function] `public static int CountBuilding(this Grid grid, BuildingSO buildingSO)`

### Assets\Scripts\Grid\Core\Grid.cs

- Lines: 955
- Flags: None

- L6 [type] `public enum GridLayer`
- L13 [type] `public enum GridMoveType`
- L22 [type] `public interface IGridOccupant`
- L30 [type] `public interface IGridMovementOccupant`
- L35 [type] `public class GridTraversalLink`
- L41 [function] `public GridTraversalLink(Vector2Int to, IGridOccupant through, GridMoveType moveType)`
- L49 [type] `public class GridMoveStep`
- L73 [function] `public GridMoveStep WithDestination(IGridOccupant destination)`
- L79 [type] `public class GridPathSearchResult`
- L111 [function] `public List<IGridOccupant> GetAllVisitableOccupants()`
- L116 [function] `public bool ContainsVisitableOccupant(IGridOccupant occupant)`
- L121 [function] `public int GetMoveDistanceTo(IGridOccupant destination)`
- L136 [function] `public List<IGridOccupant> GetAllReachableOccupants()`
- L159 [function] `public List<Vector2Int> GetReachablePositions()`
- L217 [function] `public Queue<IGridOccupant> GetOccupantPathTo(IGridOccupant destination)`
- L233 [function] `public Queue<IGridOccupant> GetOccupantPath(Func<Vector2Int, bool> terminateEndCondition)`
- L248 [function] `public Queue<GridMoveStep> GetMovePathTo(IGridOccupant destination)`
- L264 [function] `public Queue<GridMoveStep> GetMovePath(Func<Vector2Int, bool> terminateEndCondition)`
- L279 [function] `private Queue<IGridOccupant> BuildOccupantPath(Vector2Int end, IGridOccupant destination = null)`
- L296 [function] `private Queue<GridMoveStep> BuildMovePath(Vector2Int end, IGridOccupant destination = null)`
- L319 [function] `private bool TryGetVisitableOccupantPosition(IGridOccupant occupant, out Vector2Int position)`
- L325 [function] `private void EnsureVisitableOccupantPositionCache()`
- L355 [function] `private int GetMoveDistance(Vector2Int end)`
- L397 [type] `public class Grid`
- L411 [function] `public Grid(int gridWidth, int gridHeight)`
- L416 [function] `public Grid(int gridWidth, int gridHeight, Vector3 originPos, int cellWorldHeight = DefaultCellWorldHeight)`
- L436 [function] `public void SetUnityCoordinates(Vector3 originPos, int cellWorldHeight = DefaultCellWorldHeight)`
- L442 [function] `public Vector2Int GetXY(Vector3 worldPosition)`
- L449 [function] `public Vector3 GetWorldPos(Vector2Int gridPosition)`
- L454 [function] `public Vector3 GetWorldPos(Vector2 gridPosition)`
- L462 [function] `public bool IsValidGridPos(Vector2Int gridPos)`
- L470 [function] `public GridCell GetGridCell(Vector2Int pos)`
- L477 [function] `public IEnumerable<GridCell> GetCells()`
- L485 [function] `public Grid TryExpandGrid(int x, int y)`
- L505 [function] `public bool RegisterOccupant(IGridOccupant occupant, GridLayer layer, IReadOnlyList<Vector2Int> positions, bool connectPositions)`
- L533 [function] `public bool RemoveOccupant(GridLayer layer, IReadOnlyList<Vector2Int> positions, bool disconnectPositions)`
- L566 [function] `public GridPathSearchResult SearchPath(Vector2Int start)`
- L571 [function] `private GridPathSearchResult SearchPath(Vector2Int start, Func<Vector2Int, bool> stopCondition)`
- L656 [function] `public Queue<IGridOccupant> GetOccupantPath(Vector2Int start, Func<Vector2Int, bool> terminateEndCondition)`
- L661 [function] `public Queue<GridMoveStep> GetMovePath(Vector2Int start, Func<Vector2Int, bool> terminateEndCondition)`
- L682 [function] `public List<IGridOccupant> GetAllVisitableOccupants(Vector2Int start)`
- L687 [function] `public List<IGridOccupant> GetAllReachableOccupants(Vector2Int start)`
- L692 [function] `public Queue<IGridOccupant> SmoothOccupantPath(Queue<IGridOccupant> gridPath)`
- L710 [function] `public bool IsWalkable(Vector2Int pos)`
- L743 [function] `private static bool IsWalkableFacilityCell(BuildableObject building)`
- L749 [function] `public bool TryFindNearestWalkablePosition(Vector2Int start, out Vector2Int walkablePosition)`
- L776 [function] `public bool IsConnectedWithAny(IReadOnlyCollection<Vector2Int> end)`
- L783 [function] `public List<IGridOccupant> FindAllOccupants(Func<IGridOccupant, bool> predicate)`
- L806 [function] `private int NextSearchMark()`
- L818 [function] `private void RegisterTraversalLinks(IGridOccupant occupant, IReadOnlyList<Vector2Int> positions)`
- L838 [function] `private bool CanConnectMovementCells(Vector2Int from, Vector2Int to, GridMoveType moveType)`
- L855 [function] `private GridMoveType ResolveMoveType(IGridOccupant occupant)`
- L865 [function] `private void AddMoveStep(List<GridMoveStep> steps, Vector2Int from, Vector2Int to, IGridOccupant movementOccupant, GridMoveType moveType)`
- L878 [type] `internal static class GridSearchScratch`
- L891 [function] `public static Queue<Vector2Int> RentPositionQueue()`
- L896 [function] `public static List<GridMoveStep> RentMoveStepList()`
- L901 [function] `public static List<Vector2Int> RentPositionList()`
- L906 [function] `public static List<IGridOccupant> RentOccupantList()`
- L911 [function] `public static HashSet<IGridOccupant> RentOccupantSet()`
- L916 [function] `public static void Return(Queue<Vector2Int> queue)`
- L924 [function] `public static void Return(List<GridMoveStep> list)`
- L932 [function] `public static void ReturnPositionList(List<Vector2Int> list)`
- L940 [function] `public static void ReturnOccupantList(List<IGridOccupant> list)`
- L948 [function] `public static void Return(HashSet<IGridOccupant> set)`

### Assets\Scripts\Grid\Core\GridCell.cs

- Lines: 142
- Flags: None

- L4 [type] `public class GridCell`
- L13 [function] `public GridCell(Vector2Int pos)`
- L20 [function] `public IGridOccupant GetOccupant(GridLayer layer = GridLayer.Building)`
- L25 [function] `public IGridOccupant GetTopOccupant()`
- L44 [function] `public void ConnectFloor(IEnumerable<Vector2Int> poses)`
- L61 [function] `public void SetTraversalLinks(IEnumerable<GridTraversalLink> links)`
- L77 [function] `public void RemoveOccupantByLayer(GridLayer layer)`
- L82 [function] `public List<IGridOccupant> GetAllOccupants()`
- L88 [function] `public void FillAllOccupants(List<IGridOccupant> result)`
- L100 [function] `public bool ContainsOccupant(IGridOccupant occupant)`
- L117 [function] `public bool CanOccupy(GridLayer layer = GridLayer.Building)`
- L121 [function] `public bool HasOccupantInLayer(GridLayer layer = GridLayer.Building)`
- L125 [function] `public bool HasOccupant()`
- L129 [function] `public bool TrySetOccupant(GridLayer layer, IGridOccupant occupant)`
- L137 [function] `public void SetOccupant(GridLayer layer,IGridOccupant occupant)`

### Assets\Scripts\Grid\DungeonStory\Building\DungeonStoryGridBuildingController.cs

- Lines: 463
- Flags: SceneMutation, DependencyInjection

- L8 [type] `public class DungeonStoryGridBuildingController : MonoBehaviour`
- L57 [function] `private void Awake()`
- L67 [function] `private void Start()`
- L77 [function] `private void EnsureInitialized()`
- L108 [function] `private void Update()`
- L119 [function] `public void TriggerPlaceBuilding()`
- L146 [function] `public void TriggerDestroyBuildableObject()`
- L165 [function] `private void OnEnable()`
- L175 [function] `private void OnDisable()`
- L185 [function] `public BuildableObject GetBuildingByMousePos()`
- L196 [function] `public bool IsBuildable()`
- L204 [function] `public bool IsBuildableAt(Vector2Int pos)`
- L212 [function] `public Vector3 GetMouseWorldPosSnapped()`
- L220 [function] `public void SelectBuildingById(int id)`
- L236 [function] `public void ClearBuildingSO()`
- L241 [function] `public void SetGridModeBuild()`
- L249 [function] `public void SetGridModeNone()`
- L258 [function] `public void SetDestroyMode()`
- L267 [function] `private void PlaceBuilding(List<Vector2Int> poses)`
- L295 [function] `private void DrawGridTextureWalls()`
- L300 [function] `private static bool HasAnyGridOccupants(Grid grid)`
- L315 [function] `private void OnGridExpand()`
- L323 [function] `private void OnDestroy()`
- L333 [function] `private void EnsureInputActions()`
- L349 [function] `private void OnBuildingPlaceInput(InputAction.CallbackContext context)`
- L355 [function] `private void OnExpandGridInput(InputAction.CallbackContext context)`
- L363 [function] `private BuildingSO FindBuildingDataById(int id)`
- L370 [function] `private BuildingConditionContext CreateBuildingConditionContext()`
- L376 [function] `private void ConfigurePlacedBuilding(BuildableObject building)`
- L385 [function] `private void OnPlacedBuildingClicked(BuildableObject building)`
- L393 [function] `private GridSystemManager ResolveGridSystem()`
- L404 [function] `private IGridSystemProvider ResolveGridSystemProvider()`
- L410 [function] `private IDataCatalog ResolveDataCatalog()`
- L416 [function] `private Vector3 ResolveMouseWorldPosition()`
- L423 [function] `private GridTexture ResolveGridTexture()`
- L430 [function] `private IObjectResolver ResolveObjectResolver()`
- L436 [function] `private IGameDataProvider ResolveGameDataProvider()`
- L442 [function] `private IGridBuildingObjectFactory ResolveGridBuildingObjectFactory()`
- L448 [function] `private BuildingSO ResolveBuildingDataById(int id)`
- L458 [function] `private bool TryResolveBuildingDataById(int id, out BuildingSO buildingData)`

### Assets\Scripts\Grid\DungeonStory\UI\DungeonStoryGridGhostPresenter.cs

- Lines: 149
- Flags: DependencyInjection

- L6 [type] `public class DungeonStoryGridGhostPresenter : GridPlacementGhostPresenter`
- L41 [function] `protected override void Awake()`
- L46 [function] `protected override bool CanPlaceAt(Vector2Int gridPosition)`
- L51 [function] `private void OnGridModeChanged(GridMode gridMode)`
- L62 [function] `public void OnSelectedChanged(BuildingSO buildingSO)`
- L67 [function] `private void OnEnable()`
- L72 [function] `private void Start()`
- L77 [function] `private void OnDisable()`
- L82 [function] `private void SubscribeToRuntimeEventsIfInjected()`
- L90 [function] `private void SubscribeToRuntimeEvents()`
- L107 [function] `private void UnsubscribeFromRuntimeEvents()`
- L121 [function] `private IGridSystemProvider RequireGridSystemProvider()`
- L127 [function] `private GridSystemManager RequireGridSystem()`
- L132 [function] `private IDungeonGridBuildingControllerProvider RequireBuildingControllerProvider()`
- L138 [function] `private DungeonStoryGridBuildingController RequireBuildingController()`
- L143 [function] `private IWorldPointerPositionProvider RequireWorldPointerPositionProvider()`

### Assets\Scripts\Grid\DungeonStory\UI\GridConstructButtonFactory.cs

- Lines: 114
- Flags: RuntimeObjectCreation, SceneMutation, DependencyInjection

- L8 [type] `public interface IGridConstructButtonFactory`
- L10 [function] `UITab CreateCategoryPanel(GameObject panelPrefab, Transform parent, BuildingCategory category)`
- L11 [function] `Button CreateCategoryButton(GameObject buttonPrefab, Transform parent, string labelText, UnityAction onClick)`
- L12 [function] `UIBuildingSelectButton CreateBuildingSelectButton(GameObject buttonPrefab, Transform parent, BuildingSO buildingData)`
- L15 [type] `public sealed class GridConstructButtonFactory : IGridConstructButtonFactory`
- L20 [function] `public GridConstructButtonFactory(ITmpKoreanFontService tmpKoreanFontService)`
- L27 [function] `public GridConstructButtonFactory(ITmpKoreanFontService tmpKoreanFontService, IObjectResolver objectResolver)`
- L37 [function] `public UITab CreateCategoryPanel(GameObject panelPrefab, Transform parent, BuildingCategory category)`
- L56 [function] `public Button CreateCategoryButton(GameObject buttonPrefab, Transform parent, string labelText, UnityAction onClick)`
- L88 [function] `public UIBuildingSelectButton CreateBuildingSelectButton(GameObject buttonPrefab, Transform parent, BuildingSO buildingData)`

### Assets\Scripts\Grid\DungeonStory\UI\GridConstructTab.cs

- Lines: 230
- Flags: SceneMutation, DependencyInjection

- L8 [type] `public class GridConstructTab : UITab`
- L25 [function] `public void ConstructGridConstructTab(IDataCatalog dataCatalog, IUiPopupService popupService, IDungeonGridBuildingControllerProvider buildingControllerProvider, ITmpKoreanFontService tmpKoreanFontService, IGridConstructButtonFactory buttonFactory)`
- L41 [function] `private void Start()`
- L46 [function] `public override void OnClose()`
- L52 [function] `public override void OnOpen()`
- L59 [function] `private void MakeSelectButton()`
- L89 [function] `private void AddCategoryPanel(BuildingCategory category)`
- L104 [function] `private bool HasCategoryPanel(BuildingCategory category)`
- L110 [function] `private UITab GetCategoryPanel(BuildingCategory category)`
- L122 [function] `private Transform ResolveCategoryButtonRoot()`
- L127 [function] `public void ToggleSelectButton(int category)`
- L147 [function] `public void RefreshCategoryLabels()`
- L161 [function] `private static string GetCategoryDisplayName(BuildingCategory category)`
- L176 [function] `private static bool TryGetCategoryDisplayName(string rawText, out string displayName)`
- L194 [function] `private IDataCatalog RequireDataCatalog()`
- L200 [function] `private IUiPopupService RequirePopupService()`
- L206 [function] `private IDungeonGridBuildingControllerProvider RequireBuildingControllerProvider()`
- L212 [function] `private DungeonStoryGridBuildingController RequireBuildingController()`
- L217 [function] `private ITmpKoreanFontService RequireTmpKoreanFontService()`
- L224 [function] `private IGridConstructButtonFactory RequireButtonFactory()`

### Assets\Scripts\Grid\DungeonStory\UI\GridUIManager.cs

- Lines: 118
- Flags: SceneMutation, DependencyInjection

- L5 [type] `public class GridUIManager : MonoBehaviour`
- L28 [function] `private void Start()`
- L35 [function] `private void Update()`
- L48 [function] `public void ToggleConstructTab()`
- L60 [function] `public void ShowConstructTab()`
- L65 [function] `public void CloseConstructTab()`
- L71 [function] `public void HideGrid()`
- L77 [function] `public void ShowGrid()`
- L83 [function] `public void DrawGrid()`
- L88 [function] `private void OnEnable()`
- L92 [function] `private void OnDisable()`
- L96 [function] `private void ResolveRuntimeDependencies()`
- L106 [function] `private IGridSystemProvider RequireGridSystemProvider()`
- L112 [function] `private IDungeonGridBuildingControllerProvider RequireBuildingControllerProvider()`

### Assets\Scripts\Grid\Placement\GridPlacementValidator.cs

- Lines: 71
- Flags: None

- L5 [type] `public class GridPlacementValidator`
- L7 [function] `public bool AreInsideGrid(Grid grid, IReadOnlyList<Vector2Int> positions)`
- L15 [function] `public bool AreInsideHorizontalBounds(Grid grid, IReadOnlyList<Vector2Int> positions, int sidePadding)`
- L21 [function] `public bool CanOccupy(Grid grid, GridLayer layer, IReadOnlyList<Vector2Int> positions)`
- L27 [function] `public bool HasSupportBelow(Grid grid, IReadOnlyList<Vector2Int> positions)`

### Assets\Scripts\Grid\Rendering\GridTexture.cs

- Lines: 491
- Flags: RuntimeObjectCreation, SceneMutation

- L7 [type] `public class GridTexture : SerializedMonoBehaviour, IGridBuildingVisual`
- L9 [type] `public enum TilemapLayer`
- L27 [function] `private void Awake()`
- L32 [function] `private void OnValidate()`
- L37 [function] `public void DrawBuilding(Dictionary<TilemapLayer, Tile> tiles,Vector2Int selectPos,bool isEven)`
- L49 [function] `public void DrawBuilding(BuildingSO buildingData, Vector2Int selectPos)`
- L68 [function] `public void DeleteBuilding(Dictionary<TilemapLayer, Tile> tiles,Vector2Int selectPos,bool isEven)`
- L80 [function] `public void DeleteBuilding(BuildingSO buildingData, Vector2Int selectPos)`
- L99 [function] `private void DrawSpriteBuilding(BuildingSO buildingData, Vector2Int selectPos)`
- L107 [function] `private void DeleteSpriteBuilding(BuildingSO buildingData, Vector2Int selectPos)`
- L115 [function] `private Tile GetSpriteTile(BuildingSO buildingData, Tilemap targetTilemap)`
- L143 [function] `private bool TryGetTilemap(TilemapLayer layer, bool isEven, out Tilemap targetTilemap)`
- L152 [function] `private static bool HasTileVisual(BuildingSO buildingData)`
- L157 [function] `private static TilemapLayer GetSpriteTilemapLayer(BuildingSO buildingData)`
- L165 [function] `private static Vector3Int GetTilePosition(Vector2Int selectPos)`
- L170 [function] `private void SetWallCell(Vector2Int selectPos, Tile tile)`
- L183 [type] `private readonly struct SpriteTileKey : System.IEquatable<SpriteTileKey>`
- L192 [function] `public SpriteTileKey(Sprite sprite, int width, int height, int cellTileHeight, Vector2 tileAnchor, float tilemapLocalYOffset)`
- L202 [function] `public bool Equals(SpriteTileKey other)`
- L212 [function] `public override bool Equals(object obj)`
- L217 [function] `public override int GetHashCode()`
- L232 [function] `public void DrawWall(Grid grid)`
- L301 [function] `private void SynchronizeHallwayVisuals(Grid grid)`
- L326 [function] `private void ClearHallwayTile(Vector2Int gridPos)`
- L333 [function] `private static bool CanShareHallwayVisual(BuildableObject building)`
- L339 [function] `private void ClearTile(TilemapLayer layer, bool isEven, Vector3Int tilePos)`
- L347 [function] `private void ApplyHallwaySorting()`
- L354 [function] `private void ConfigureHallwayTilemap(Dictionary<TilemapLayer, Tilemap> tilemaps, HashSet<Tilemap> configured)`
- L380 [type] `public static class GridBuildingTileTransformCalculator`
- L386 [function] `public static Matrix4x4 Calculate(BuildingSO buildingData, int cellTileHeight = DefaultCellTileHeight)`
- L391 [function] `public static Matrix4x4 Calculate(BuildingSO buildingData, int cellTileHeight, Vector2 tileAnchor)`
- L396 [function] `public static Matrix4x4 Calculate(BuildingSO buildingData, int cellTileHeight, Vector2 tileAnchor, float tilemapLocalYOffset)`
- L437 [function] `public static Vector2 GetVisualFootprintSize(Vector2 footprintSize)`
- L445 [function] `private static float GetVerticalVisualInset(float footprintHeight)`
- L456 [type] `public class GridWallTileCalculator`
- L460 [function] `public GridWallTileCalculator(int cellTileHeight = 3)`
- L465 [function] `public HashSet<Vector2Int> GetWallTilePositions(Grid grid)`

### Assets\Scripts\Grid\System\GridSystemManager.cs

- Lines: 188
- Flags: None

- L6 [type] `public enum GridMode`
- L14 [type] `public class GridSystemManager : MonoBehaviour`
- L37 [function] `protected virtual void Awake()`
- L42 [function] `protected virtual void OnEnable()`
- L47 [function] `public void EnsureGridInitialized()`
- L54 [function] `protected virtual void Start()`
- L60 [function] `public void GridExpand(int x,int y)`
- L69 [function] `public void SetGridMode(GridMode gridMode)`
- L82 [function] `public void SetGridModeBuild()`
- L89 [function] `public void SetGridModeNone()`
- L94 [function] `public void ToggleBuildMode()`
- L99 [function] `public bool TryBeginDragSelection(Vector2Int start, bool horizontalDraggable, bool verticalDraggable)`
- L109 [function] `public void UpdateDragSelection(Vector2Int pos, bool horizontalDraggable, bool verticalDraggable)`
- L118 [function] `public List<Vector2Int> CompleteDragSelection()`
- L124 [function] `public void CancelDragSelection()`
- L132 [function] `public Vector3 GetWorldPosSnapped(Vector3 worldPosition)`
- L142 [function] `public void NotifyGridObjectChanged()`
- L147 [function] `private void RecalculateSelectedPositions(Vector2Int pos, bool horizontalDraggable, bool verticalDraggable)`

### Assets\Scripts\Grid\UI\GridGhostObject.cs

- Lines: 262
- Flags: RuntimeObjectCreation, SceneMutation

- L4 [type] `public class GridGhostObject : MonoBehaviour`
- L22 [function] `private void Awake()`
- L27 [function] `private void OnValidate()`
- L32 [function] `public void Initialize(GameObject target = null)`
- L58 [function] `public void Show(Sprite sprite)`
- L72 [function] `public void ShowRepeated(Sprite sprite, IReadOnlyList<Vector3> worldPositions, Vector2 tileFootprintSize, IReadOnlyList<bool> buildableStates = null)`
- L112 [function] `public void Hide()`
- L125 [function] `public void SetBuildable(bool buildable)`
- L134 [function] `public void SetWorldPosition(Vector3 worldPosition, float lerpSpeed = 0f)`
- L145 [function] `public void SetSize(Vector2 size)`
- L152 [function] `private void ApplyFootprintSize()`
- L157 [function] `private Vector3 ApplyFootprintSize(SpriteRenderer renderer, Vector2 size)`
- L189 [function] `private Color GetPreviewColor(bool buildable)`
- L194 [function] `private Color WithPreviewAlpha(Color color)`
- L200 [function] `private void EnsureRepeatedRendererCount(int count)`
- L221 [function] `private void ConfigurePreviewRenderers()`
- L229 [function] `private void ConfigurePreviewRenderer(SpriteRenderer renderer)`
- L244 [function] `private void HideRepeatedRenderers(int keepActiveCount)`
- L256 [function] `private void EnsureInitialized()`

### Assets\Scripts\Grid\UI\GridGhostObjectResolver.cs

- Lines: 32
- Flags: None

- L4 [type] `public interface IGridGhostObjectResolver`
- L9 [type] `public sealed class GridGhostObjectResolver : IGridGhostObjectResolver`
- L11 [function] `public GridGhostObject Resolve(Component owner, GridGhostObject configuredGhostObject)`

### Assets\Scripts\Grid\UI\GridPlacementGhostPresenter.cs

- Lines: 134
- Flags: RuntimeObjectCreation, SceneMutation

- L4 [type] `public abstract class GridPlacementGhostPresenter : MonoBehaviour`
- L23 [function] `protected abstract bool CanPlaceAt(Vector2Int gridPosition);`
- L25 [function] `protected virtual void Awake()`
- L31 [function] `protected virtual void Update()`
- L36 [function] `protected void RefreshGhostSelection()`
- L50 [function] `protected void HideGhost()`
- L56 [function] `private void UpdateGhost()`
- L77 [function] `private void UpdateSingleCellGhost(Grid grid)`
- L89 [function] `private void UpdateDraggedGhost(Grid grid)`
- L115 [function] `private Vector2 GetGhostFootprintSize()`
- L122 [function] `private void EnsureGhostObject()`

### Assets\Scripts\Grid\UI\UIGridTab.cs

- Lines: 39
- Flags: SceneMutation

- L4 [type] `public class UIGridTab : MonoBehaviour`
- L9 [function] `private void Start()`
- L13 [function] `public bool ToggleTab(int categoryId)`
- L31 [function] `public void OpenTap()`
- L35 [function] `public void CloseTab()`

### Assets\Scripts\Infrastructure\BlueprintResearchRuntimeProvider.cs

- Lines: 97
- Flags: None

- L3 [type] `public interface IBlueprintResearchRuntimeProvider`
- L8 [type] `public interface IBlueprintResearchWorkService`
- L17 [type] `public interface IBlueprintResearchStateService`
- L22 [type] `public sealed class BlueprintResearchRuntimeProvider : IBlueprintResearchRuntimeProvider`
- L26 [function] `public BlueprintResearchRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)`
- L32 [function] `public bool TryGetRuntime(out BlueprintResearchRuntime runtime)`
- L39 [type] `public sealed class BlueprintResearchWorkService : IBlueprintResearchWorkService`
- L43 [function] `public BlueprintResearchWorkService(IBlueprintResearchRuntimeProvider runtimeProvider)`
- L49 [function] `public bool HasResearchWorkFor(BuildableObject facility)`
- L78 [type] `public sealed class BlueprintResearchStateService : IBlueprintResearchStateService`
- L82 [function] `public BlueprintResearchStateService(IBlueprintResearchRuntimeProvider runtimeProvider)`
- L88 [function] `public BlueprintResearchState GetState()`

### Assets\Scripts\Infrastructure\CharacterAiActionAssetCatalog.cs

- Lines: 40
- Flags: None

- L3 [type] `public interface ICharacterAiActionAssetCatalog`
- L9 [type] `public sealed class ResourceCharacterAiActionAssetCatalog : ICharacterAiActionAssetCatalog`
- L13 [function] `public ResourceCharacterAiActionAssetCatalog(IResourcesAssetLoader resourcesAssetLoader)`
- L19 [function] `public T GetRequiredAction<T>(string resourcePath) where T : AIActionSet`
- L29 [function] `public AIFacilityRoleAction GetRequiredFacilityRoleAction(string resourcePath, FacilityRole role)`

### Assets\Scripts\Infrastructure\CharacterAiSchedulingService.cs

- Lines: 79
- Flags: None

- L3 [type] `public interface ICharacterAiSchedulingService`
- L15 [type] `public sealed class CharacterAiSchedulingService : ICharacterAiSchedulingService`
- L20 [function] `public CharacterAiSchedulingService(IDungeonSceneComponentQuery sceneQuery)`
- L28 [function] `public void Register(CharacterActor actor)`
- L33 [function] `public void Unregister(CharacterActor actor)`
- L41 [function] `public void RequestImmediateDecision(CharacterActor actor)`
- L46 [function] `public bool TryConsumePathSearchBudget()`
- L51 [function] `public bool ShouldShowCharacterFeedback(CharacterActor actor)`
- L56 [function] `public int GetMovementFrameStride(CharacterActor actor)`
- L61 [function] `public void ResetPathSearchBudgetForDebug()`
- L66 [function] `private CharacterAiScheduler ResolveScheduler()`
- L73 [function] `private bool TryResolveScheduler(out CharacterAiScheduler resolvedScheduler)`

### Assets\Scripts\Infrastructure\CharacterSpawnerProvider.cs

- Lines: 25
- Flags: None

- L3 [type] `public interface ICharacterSpawnerProvider`
- L8 [type] `public sealed class CharacterSpawnerProvider : ICharacterSpawnerProvider`
- L13 [function] `public CharacterSpawnerProvider(IDungeonSceneComponentQuery sceneQuery)`
- L19 [function] `public bool TryGetSpawner(out CharacterSpawner resolvedSpawner)`

### Assets\Scripts\Infrastructure\CodexReferenceCatalogServices.cs

- Lines: 34
- Flags: None

- L5 [type] `public interface ICodexReferenceCatalog`
- L11 [type] `public sealed class DataCatalogCodexReferenceCatalog : ICodexReferenceCatalog`
- L15 [function] `public DataCatalogCodexReferenceCatalog(IDataCatalog catalog)`

### Assets\Scripts\Infrastructure\DataCatalogService.cs

- Lines: 51
- Flags: None

- L4 [type] `public interface IDataCatalog`
- L9 [type] `public sealed class DataManagerCatalog : IDataCatalog`
- L13 [function] `public DataManagerCatalog(DataManager dataManager)`
- L19 [function] `public IReadOnlyDictionary<int, T> GetData<T>() where T : DataScriptableObject`
- L27 [type] `public interface IBuildingDefinitionLookup`
- L32 [type] `public sealed class BuildingDefinitionLookup : IBuildingDefinitionLookup`
- L36 [function] `public BuildingDefinitionLookup(IDataCatalog catalog)`
- L41 [function] `public BuildingSO GetBuilding(int id)`

### Assets\Scripts\Infrastructure\DataScriptableObjectSource.cs

- Lines: 25
- Flags: None

- L4 [type] `public interface IDataScriptableObjectSource`
- L9 [type] `public sealed class ResourceDataScriptableObjectSource : IDataScriptableObjectSource`
- L15 [function] `public ResourceDataScriptableObjectSource(IResourcesAssetLoader resourcesAssetLoader)`
- L21 [function] `public IReadOnlyCollection<DataScriptableObject> LoadAll()`

### Assets\Scripts\Infrastructure\DungeonBackdropReferenceProvider.cs

- Lines: 65
- Flags: SceneMutation

- L5 [type] `public interface IDungeonBackdropReferenceProvider`
- L11 [type] `public sealed class DungeonBackdropReferenceProvider : IDungeonBackdropReferenceProvider`
- L17 [function] `public DungeonBackdropReferenceProvider(IDungeonSceneComponentQuery sceneQuery)`
- L40 [function] `private Transform FindTransform(string objectName)`
- L53 [function] `private Tilemap FindTilemap(string tilemapName)`

### Assets\Scripts\Infrastructure\DungeonGridBuildingRuntimeProviders.cs

- Lines: 108
- Flags: SceneMutation

- L4 [type] `public interface IDungeonGridBuildingControllerProvider`
- L9 [type] `public interface IWorldPointerPositionProvider`
- L14 [type] `public interface IMainCameraProvider`
- L19 [type] `public interface IGridTextureProvider`
- L24 [type] `public sealed class DungeonGridBuildingControllerProvider : IDungeonGridBuildingControllerProvider`
- L29 [function] `public DungeonGridBuildingControllerProvider(IDungeonSceneComponentQuery sceneQuery)`
- L45 [type] `public sealed class GridTextureProvider : IGridTextureProvider`
- L50 [function] `public GridTextureProvider(IDungeonSceneComponentQuery sceneQuery)`
- L66 [type] `public sealed class SceneCameraWorldPointerPositionProvider : IWorldPointerPositionProvider`
- L70 [function] `public SceneCameraWorldPointerPositionProvider(IMainCameraProvider cameraProvider)`
- L89 [type] `public sealed class SceneMainCameraProvider : IMainCameraProvider`
- L94 [function] `public SceneMainCameraProvider(IDungeonSceneComponentQuery sceneQuery)`

### Assets\Scripts\Infrastructure\DungeonRuntimeLifetimeScope.cs

- Lines: 543
- Flags: SceneMutation, DependencyInjection

- L5 [type] `public sealed class DungeonRuntimeLifetimeScope : LifetimeScope`
- L7 [function] `protected override void OnDestroy()`
- L12 [function] `protected override void Configure(IContainerBuilder builder)`
- L293 [function] `private static void InjectSceneComponents(IObjectResolver resolver)`
- L533 [function] `private static DungeonSceneRuntimeReferences CaptureSceneRuntimeReferences()`

### Assets\Scripts\Infrastructure\DungeonSceneComponentQuery.cs

- Lines: 67
- Flags: None

- L5 [type] `public interface IDungeonSceneComponentQuery`
- L11 [type] `public sealed class DungeonSceneComponentQuery : IDungeonSceneComponentQuery`
- L13 [function] `public T First<T>(bool includeInactive = false) where T : Component`
- L23 [function] `public IReadOnlyList<T> All<T>(bool includeInactive = false) where T : Component`
- L40 [function] `private static IEnumerable<T> EnumerateLoadedSceneComponents<T>(bool includeInactive) where T : Component`

### Assets\Scripts\Infrastructure\DungeonSceneRuntimeReferences.cs

- Lines: 24
- Flags: None

- L3 [type] `public sealed class DungeonSceneRuntimeReferences`

### Assets\Scripts\Infrastructure\FacilityRecipeCatalogServices.cs

- Lines: 195
- Flags: None

- L5 [type] `public interface IFacilitySynthesisRecipeCatalog`
- L10 [type] `public interface IFacilitySynthesisRecipeQuery`
- L20 [type] `public interface IFacilityEvolutionRecipeQuery : IFacilityEvolutionRecipeProvider`
- L29 [type] `public sealed class DataCatalogFacilitySynthesisRecipeCatalog : IFacilitySynthesisRecipeCatalog`
- L33 [function] `public DataCatalogFacilitySynthesisRecipeCatalog(IDataCatalog catalog)`
- L39 [function] `public IReadOnlyList<FacilitySynthesisRecipeSO> GetRecipes()`
- L50 [type] `public sealed class FacilitySynthesisRecipeQuery : IFacilitySynthesisRecipeQuery`
- L65 [function] `public IReadOnlyList<FacilitySynthesisRecipeSO> GetAllRecipes()`
- L70 [function] `public bool IsVisible(FacilitySynthesisRecipeSO recipe, BlueprintResearchState researchState)`
- L75 [function] `public IReadOnlyList<FacilitySynthesisRecipeSO> GetVisibleRecipes(BlueprintResearchState researchState)`
- L90 [type] `public sealed class DataCatalogFacilityEvolutionRecipeProvider : IFacilityEvolutionRecipeProvider`
- L94 [function] `public DataCatalogFacilityEvolutionRecipeProvider(IDataCatalog catalog)`
- L100 [function] `public IReadOnlyList<FacilityEvolutionRecipeSO> GetRecipes()`
- L111 [type] `public sealed class FacilityEvolutionRecipeQuery : IFacilityEvolutionRecipeQuery`
- L130 [function] `public IReadOnlyList<FacilityEvolutionRecipeSO> GetRecipes()`
- L135 [function] `public bool IsVisible(FacilityEvolutionRecipeSO recipe, BlueprintResearchState researchState)`
- L140 [function] `public IReadOnlyList<FacilityEvolutionRecipeSO> GetVisibleRecipes(BlueprintResearchState researchState)`
- L160 [type] `public sealed class DataCatalogFacilityEvolutionRecordTokenDefinitionProvider :`
- L165 [function] `public DataCatalogFacilityEvolutionRecordTokenDefinitionProvider(IDataCatalog catalog)`
- L171 [function] `public IReadOnlyList<FacilityEvolutionRecordTokenDefinitionSO> GetDefinitions()`
- L182 [function] `public FacilityEvolutionRecordTokenDefinitionSO GetDefinition(string tokenId)`

### Assets\Scripts\Infrastructure\FacilityShopRuntimeProvider.cs

- Lines: 93
- Flags: None

- L5 [type] `public interface IDailyFacilityShopRuntimeProvider`
- L10 [type] `public interface IFacilityShopCatalog`
- L17 [type] `public interface IFacilityShopUnlockStateService`
- L22 [type] `public sealed class DailyFacilityShopRuntimeProvider : IDailyFacilityShopRuntimeProvider`
- L26 [function] `public DailyFacilityShopRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)`
- L32 [function] `public bool TryGetRuntime(out DailyFacilityShopRuntime runtime)`
- L39 [type] `public sealed class FacilityShopUnlockStateService : IFacilityShopUnlockStateService`
- L43 [function] `public FacilityShopUnlockStateService(IDailyFacilityShopRuntimeProvider runtimeProvider)`
- L49 [function] `public FacilityShopUnlockState GetUnlockState()`
- L60 [type] `public sealed class DataCatalogFacilityShopCatalog : IFacilityShopCatalog`
- L64 [function] `public DataCatalogFacilityShopCatalog(IDataCatalog catalog)`
- L82 [function] `public BuildingSO FindBuildingById(int buildingId)`

### Assets\Scripts\Infrastructure\GameRuntimeServices.cs

- Lines: 65
- Flags: None

- L5 [type] `public interface IGameDataProvider`
- L10 [type] `public interface IFloatingNumberFeedbackService`
- L15 [type] `public sealed class GameManagerGameDataProvider : IGameDataProvider`
- L20 [function] `public GameManagerGameDataProvider(IDungeonSceneComponentQuery sceneQuery)`
- L26 [function] `public bool TryGetGameData(out GameData gameData)`
- L34 [type] `public sealed class GameManagerFloatingNumberFeedbackService : IFloatingNumberFeedbackService`
- L39 [function] `public GameManagerFloatingNumberFeedbackService(IDungeonSceneComponentQuery sceneQuery)`
- L45 [function] `public bool TryShow(NumberCondition condition, Vector3 worldPosition, float value)`
- L59 [function] `private GameManager ResolveGameManager()`

### Assets\Scripts\Infrastructure\GridSystemProvider.cs

- Lines: 72
- Flags: None

- L3 [type] `public interface IGridSystemProvider`
- L11 [type] `public sealed class GridSystemProvider : IGridSystemProvider`
- L16 [function] `public GridSystemProvider(IDungeonSceneComponentQuery sceneQuery)`
- L47 [function] `public bool TryGetManager(out GridSystemManager resolvedManager)`
- L61 [function] `public bool TryGetGrid(out Grid grid)`

### Assets\Scripts\Infrastructure\InvasionIntruderDataProvider.cs

- Lines: 24
- Flags: None

- L3 [type] `public sealed class ResourceInvasionIntruderDataProvider : IInvasionIntruderDataProvider`
- L9 [function] `public ResourceInvasionIntruderDataProvider(IResourcesAssetLoader resourcesAssetLoader)`
- L15 [function] `public CharacterSO GetRequiredIntruderData(CharacterSO configuredData)`

### Assets\Scripts\Infrastructure\InvasionThreatRuntimeProvider.cs

- Lines: 23
- Flags: None

- L1 [type] `public interface IInvasionThreatRuntimeProvider`
- L6 [type] `public sealed class InvasionThreatRuntimeProvider : IInvasionThreatRuntimeProvider`
- L11 [function] `public InvasionThreatRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)`
- L17 [function] `public bool TryGetRuntime(out InvasionThreatRuntime resolvedRuntime)`

### Assets\Scripts\Infrastructure\LocalLlmRuntimeProvider.cs

- Lines: 35
- Flags: None

- L3 [type] `public interface ILocalLlmRuntimeProvider`
- L9 [type] `public sealed class LocalLlmRuntimeProvider : ILocalLlmRuntimeProvider`
- L14 [function] `public LocalLlmRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)`
- L19 [function] `public bool TryGetRuntime(out ILocalLlmRuntime resolvedRuntime)`
- L26 [function] `public ILocalLlmRuntime GetRequiredRuntime()`

### Assets\Scripts\Infrastructure\MetaProgressionRuntimeProvider.cs

- Lines: 86
- Flags: None

- L4 [type] `public interface IMetaProgressionRuntimeProvider`
- L9 [type] `public interface IMetaProgressionRuntimeReader`
- L19 [type] `public sealed class MetaProgressionRuntimeProvider : IMetaProgressionRuntimeProvider`
- L23 [function] `public MetaProgressionRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)`
- L29 [function] `public bool TryGetRuntime(out MetaProgressionRuntime runtime)`
- L36 [type] `public sealed class MetaProgressionRuntimeReader : IMetaProgressionRuntimeReader`
- L40 [function] `public MetaProgressionRuntimeReader(IMetaProgressionRuntimeProvider provider)`
- L46 [function] `public int GetStartingFacilityCandidateBonus()`
- L53 [function] `public int GetStartingOwnerTraitCandidateBonus()`
- L60 [function] `public float GetOwnerMaxHealthMultiplier()`
- L67 [function] `public float GetInvasionWarningThresholdMultiplier()`
- L74 [function] `public bool IsRecipePreserved(string recipeId)`
- L80 [function] `public IReadOnlyCollection<int> GetExpandedBasicPurchaseBuildingIds(IEnumerable<BuildingSO> buildings)`

### Assets\Scripts\Infrastructure\RegularCustomerRuntimeProvider.cs

- Lines: 25
- Flags: None

- L3 [type] `public interface IRegularCustomerRuntimeProvider`
- L8 [type] `public sealed class RegularCustomerRuntimeProvider : IRegularCustomerRuntimeProvider`
- L13 [function] `public RegularCustomerRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)`
- L19 [function] `public bool TryGetRuntime(out RegularCustomerRuntime resolvedRuntime)`

### Assets\Scripts\Infrastructure\ResourcesAssetLoader.cs

- Lines: 53
- Flags: ResourcesAccess

- L6 [type] `public interface IResourcesAssetLoader`
- L12 [type] `public sealed class UnityResourcesAssetLoader : IResourcesAssetLoader`
- L14 [function] `public T LoadRequired<T>(string resourcePath) where T : UnityEngine.Object`
- L28 [function] `public IReadOnlyCollection<T> LoadAllRequired<T>(string resourcePath) where T : UnityEngine.Object`
- L46 [function] `private static void ValidateResourcePath(string resourcePath)`

### Assets\Scripts\Infrastructure\RuntimePanelProviders.cs

- Lines: 79
- Flags: None

- L3 [type] `public interface IFacilityEvolutionRuntimeProvider`
- L8 [type] `public interface IFacilitySynthesisRuntimeProvider`
- L13 [type] `public interface ICodexRuntimeProvider`
- L18 [type] `public sealed class FacilityEvolutionRuntimeProvider : IFacilityEvolutionRuntimeProvider`
- L23 [function] `public FacilityEvolutionRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)`
- L39 [type] `public sealed class FacilitySynthesisRuntimeProvider : IFacilitySynthesisRuntimeProvider`
- L44 [function] `public FacilitySynthesisRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)`
- L60 [type] `public sealed class CodexRuntimeProvider : ICodexRuntimeProvider`
- L65 [function] `public CodexRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)`

### Assets\Scripts\Infrastructure\RunVariableCatalogServices.cs

- Lines: 91
- Flags: None

- L5 [type] `public interface IRunCharacterCatalog`
- L10 [type] `public interface IOwnerCandidateCatalog`
- L15 [type] `public interface IRunStartVariableCatalog`
- L22 [type] `public sealed class ResourceRunCharacterCatalog : IRunCharacterCatalog`
- L29 [function] `public ResourceRunCharacterCatalog(IResourcesAssetLoader resourcesAssetLoader)`
- L48 [type] `public sealed class ResourceOwnerCandidateCatalog : IOwnerCandidateCatalog`
- L52 [function] `public ResourceOwnerCandidateCatalog(IRunCharacterCatalog characterCatalog)`
- L65 [type] `public sealed class RunStartVariableCatalog : IRunStartVariableCatalog`
- L70 [function] `public RunStartVariableCatalog(IDataCatalog catalog, IRunCharacterCatalog characterCatalog)`

### Assets\Scripts\Infrastructure\RunVariableRuntimeProvider.cs

- Lines: 171
- Flags: None

- L4 [type] `public interface IRunVariableRuntimeProvider`
- L9 [type] `public interface IRunVariableRuntimeReader`
- L21 [type] `public interface IOwnerRunDataProvider`
- L26 [type] `public interface IOwnerRunManagerProvider`
- L31 [type] `public interface IOwnerRunLifecycleService`
- L36 [type] `public sealed class RunVariableRuntimeProvider : IRunVariableRuntimeProvider`
- L41 [function] `public RunVariableRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)`
- L47 [function] `public bool TryGetRuntime(out RunVariableRuntime resolvedRuntime)`
- L55 [type] `public sealed class RunVariableRuntimeReader : IRunVariableRuntimeReader`
- L59 [function] `public RunVariableRuntimeReader(IRunVariableRuntimeProvider provider)`
- L65 [function] `public int GetInitialShopSeed()`
- L73 [function] `public float GetGuestDemandMultiplier(string speciesTag)`
- L80 [function] `public float GetStockCostMultiplier(StockCategory category)`
- L87 [function] `public float GetFacilityShopCostMultiplier(BuildingSO building)`
- L94 [function] `public float GetBlueprintCostMultiplier(FacilityBlueprintSO blueprint)`
- L101 [function] `public float GetThreatRiseMultiplier()`
- L108 [function] `public float GetWarningThresholdMultiplier()`
- L115 [function] `public InvasionIntruderSettings ApplyInvasionSettings(InvasionIntruderSettings source)`
- L123 [type] `public sealed class OwnerRunDataProvider : IOwnerRunDataProvider, IOwnerRunManagerProvider`
- L128 [function] `public OwnerRunDataProvider(IDungeonSceneComponentQuery sceneQuery)`
- L144 [function] `public bool TryGetManager(out OwnerRunManager manager)`
- L152 [type] `public sealed class OwnerRunLifecycleService : IOwnerRunLifecycleService`
- L156 [function] `public OwnerRunLifecycleService(IOwnerRunManagerProvider provider)`
- L162 [function] `public void HandleOwnerDeath(CharacterActor owner, string reason)`

### Assets\Scripts\Infrastructure\SceneBuildableLeakValidator.cs

- Lines: 136
- Flags: SceneMutation, DependencyInjection

- L7 [type] `public sealed class SceneBuildableLeakValidator : IInitializable`
- L11 [function] `public SceneBuildableLeakValidator(IDungeonSceneComponentQuery sceneQuery)`
- L16 [function] `public void Initialize()`
- L35 [function] `private void CollectLeakedFacilities(List<string> invalidSceneObjects)`
- L51 [function] `private static void CollectMissingScriptObjects(List<string> invalidSceneObjects)`
- L66 [function] `private static void CollectMissingScriptObjects(GameObject gameObject, List<string> invalidSceneObjects)`
- L90 [function] `private static bool IsLeakedFacilityRoot(BuildableObject buildable)`
- L108 [function] `private static string DescribeLeakedBuildable(BuildableObject buildable)`
- L114 [function] `private static string DescribeMissingScript(GameObject gameObject)`
- L119 [function] `private static string GetHierarchyPath(GameObject gameObject)`

### Assets\Scripts\Infrastructure\ShopStockCatalogService.cs

- Lines: 41
- Flags: None

- L5 [type] `public interface IShopStockCatalog`
- L12 [type] `public sealed class ShopStockCatalog : IShopStockCatalog`
- L16 [function] `public ShopStockCatalog(IDataCatalog dataCatalog)`
- L22 [function] `public bool TryGetStockInfoForShop(int shopId, out StockInfo stockInfo)`
- L29 [function] `public bool TryGetSaleItem(int saleItemId, out SaleItem saleItem)`
- L35 [function] `public StockCategory GetStockCategory(int saleItemId)`

### Assets\Scripts\Infrastructure\SocialReputationRuntimeProvider.cs

- Lines: 54
- Flags: None

- L3 [type] `public interface ISocialReputationRuntimeProvider`
- L8 [type] `public interface ISocialReputationBiasService`
- L13 [type] `public sealed class SocialReputationRuntimeProvider : ISocialReputationRuntimeProvider`
- L18 [function] `public SocialReputationRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)`
- L23 [function] `public bool TryGetRuntime(out SocialReputationRuntime resolvedRuntime)`
- L31 [type] `public sealed class SocialReputationBiasService : ISocialReputationBiasService`
- L35 [function] `public SocialReputationBiasService(ISocialReputationRuntimeProvider provider)`
- L40 [function] `public float GetFacilityUtilityBias(CharacterActor actor, BuildableObject building)`

### Assets\Scripts\Infrastructure\StaffDiscontentRuntimeProvider.cs

- Lines: 74
- Flags: None

- L3 [type] `public interface IStaffDiscontentRuntimeProvider`
- L8 [type] `public interface IStaffDiscontentRuntimeService`
- L16 [type] `public sealed class StaffDiscontentRuntimeProvider : IStaffDiscontentRuntimeProvider`
- L21 [function] `public StaffDiscontentRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)`
- L27 [function] `public bool TryGetRuntime(out StaffDiscontentRuntime resolvedRuntime)`
- L35 [type] `public sealed class StaffDiscontentRuntimeService : IStaffDiscontentRuntimeService`
- L39 [function] `public StaffDiscontentRuntimeService(IStaffDiscontentRuntimeProvider provider)`
- L45 [function] `public float GetWorkEfficiencyMultiplier(CharacterActor staff)`
- L52 [function] `public bool ShouldBlockWork(CharacterActor staff, out string reason)`
- L60 [function] `public bool IsRebellionTarget(CharacterActor target)`
- L67 [function] `public bool ResolveSuppressedRebel(CharacterActor rebel, CharacterActor defender)`

### Assets\Scripts\Infrastructure\TmpKoreanFontProvider.cs

- Lines: 29
- Flags: None

- L4 [type] `public sealed class ResourceTmpKoreanFontProvider : ITmpKoreanFontProvider`
- L12 [function] `public ResourceTmpKoreanFontProvider(IResourcesAssetLoader resourcesAssetLoader)`
- L18 [function] `public TMP_FontAsset GetRequiredFont()`

### Assets\Scripts\Invasion\InvasionCombatReportRuntime.cs

- Lines: 119
- Flags: EventBus

- L3 [type] `public class InvasionCombatReportRuntime : MonoBehaviour,`
- L19 [function] `public void OnTriggerEvent(InvasionStartedEvent eventType)`
- L25 [function] `public void OnTriggerEvent(InvasionSpawnedEvent eventType)`
- L31 [function] `public void OnTriggerEvent(DefenseFacilityTriggeredEvent eventType)`
- L48 [function] `public void OnTriggerEvent(InvasionFacilityDamagedEvent eventType)`
- L58 [function] `public void OnTriggerEvent(InvasionFinalCombatStartedEvent eventType)`
- L68 [function] `public void OnTriggerEvent(InvasionResolvedEvent eventType)`
- L83 [function] `private void OnEnable()`
- L93 [function] `private void OnDisable()`
- L103 [function] `private InvasionCombatReport EnsureReport(InvasionThreatSnapshot snapshot)`
- L109 [function] `private string ClampLine(string message)`

### Assets\Scripts\Invasion\InvasionCombatReportSystem.cs

- Lines: 435
- Flags: EventBus

- L6 [type] `public struct InvasionCombatFeedbackEvent`
- L11 [function] `public InvasionCombatFeedbackEvent(string message, DefenseActivationReport defenseReport)`
- L19 [function] `public static void Trigger(string message, DefenseActivationReport defenseReport)`
- L27 [type] `public struct InvasionCombatReportReadyEvent`
- L31 [function] `public InvasionCombatReportReadyEvent(InvasionCombatReport report)`
- L38 [function] `public static void Trigger(InvasionCombatReport report)`
- L45 [type] `public class InvasionCombatReport`
- L53 [function] `public InvasionCombatReport(InvasionThreatSnapshot snapshot)`
- L88 [function] `public void SetIntruder(CharacterActor intruder)`
- L93 [function] `public void RecordDefenseActivation(DefenseActivationReport report)`
- L121 [function] `public void RecordFacilityDamage(BuildableObject facility)`
- L131 [function] `public void RecordFinalCombat(CharacterActor owner)`
- L137 [function] `public void Resolve(bool defended, float residualRisk)`
- L144 [function] `public string ToDetailText()`
- L183 [function] `private DefenseContribution FindOrCreateContribution(DefenseFacility facility)`
- L196 [function] `private void AddObservation(string observation)`
- L204 [function] `private string GetDecisiveDefenseText()`
- L227 [function] `private string FormatFacilities(IEnumerable<BuildableObject> facilities)`
- L239 [function] `private string FormatObservations()`
- L263 [type] `public class DefenseContribution`
- L267 [function] `public DefenseContribution(DefenseFacility facility)`
- L279 [function] `public void Add(DefenseActivationReport report)`
- L300 [type] `public static class InvasionCombatReportFormatter`
- L302 [function] `public static string FormatActivation(DefenseActivationReport report)`
- L332 [function] `public static string FormatObservation(string effectTag)`
- L378 [function] `public static string FormatSynergy(DefenseActivationReport report)`
- L420 [function] `public static string GetBuildingName(BuildableObject building)`

### Assets\Scripts\Invasion\InvasionDefenseSummaryQuery.cs

- Lines: 128
- Flags: None

- L4 [type] `public readonly struct InvasionDefenseSummary`
- L39 [type] `public interface IInvasionDefenseSummaryService`
- L44 [type] `public interface IInvasionDefenseSummaryRuntimeSource`
- L52 [type] `public sealed class InvasionDefenseSummaryRuntimeSource : IInvasionDefenseSummaryRuntimeSource`
- L56 [function] `public InvasionDefenseSummaryRuntimeSource(IDungeonSceneComponentQuery sceneQuery)`
- L67 [type] `public sealed class InvasionDefenseSummaryService : IInvasionDefenseSummaryService`
- L71 [function] `public InvasionDefenseSummaryService(IInvasionDefenseSummaryRuntimeSource runtimeSource)`
- L76 [function] `public InvasionDefenseSummary Capture()`

### Assets\Scripts\Invasion\InvasionFacilityDamageResolver.cs

- Lines: 49
- Flags: None

- L4 [type] `public static class InvasionFacilityDamageResolver`
- L6 [function] `public static bool TryFindDamageTarget(Grid grid, Vector2Int current, out BuildableObject target)`
- L41 [function] `private static bool IsDamageableFacility(BuildableObject building)`

### Assets\Scripts\Invasion\InvasionIntruderContext.cs

- Lines: 82
- Flags: None

- L4 [type] `public readonly struct InvasionIntruderEntry`
- L21 [type] `public interface IInvasionIntruderContext`
- L29 [type] `public sealed class InvasionIntruderContext : IInvasionIntruderContext`
- L50 [function] `public bool TryGetGrid(out Grid grid)`
- L64 [function] `public bool TryGetOwner(out CharacterActor owner)`
- L71 [function] `public bool TryResolveEntry(out InvasionIntruderEntry entry)`
- L78 [function] `public InvasionIntruderSettings ApplyRunVariables(InvasionIntruderSettings source)`

### Assets\Scripts\Invasion\InvasionIntruderDataResolver.cs

- Lines: 4
- Flags: None

- L1 [type] `public interface IInvasionIntruderDataProvider`

### Assets\Scripts\Invasion\InvasionIntruderEntryResolver.cs

- Lines: 32
- Flags: None

- L3 [type] `public static class InvasionIntruderEntryResolver`

### Assets\Scripts\Invasion\InvasionIntruderEvents.cs

- Lines: 62
- Flags: EventBus

- L1 [type] `public struct InvasionSpawnedEvent`
- L6 [function] `public InvasionSpawnedEvent(CharacterActor intruder, InvasionThreatSnapshot threatSnapshot)`
- L14 [function] `public static void Trigger(CharacterActor intruder, InvasionThreatSnapshot threatSnapshot)`
- L22 [type] `public struct InvasionFacilityDamagedEvent`
- L27 [function] `public InvasionFacilityDamagedEvent(CharacterActor intruder, BuildableObject facility)`
- L35 [function] `public static void Trigger(CharacterActor intruder, BuildableObject facility)`
- L43 [type] `public struct InvasionFinalCombatStartedEvent`
- L48 [function] `public InvasionFinalCombatStartedEvent(CharacterActor intruder, CharacterActor owner)`
- L56 [function] `public static void Trigger(CharacterActor intruder, CharacterActor owner)`

### Assets\Scripts\Invasion\InvasionIntruderFactory.cs

- Lines: 67
- Flags: RuntimeObjectCreation, SceneMutation

- L4 [type] `public interface IInvasionIntruderFactory`
- L10 [type] `public sealed class InvasionIntruderRuntimeFactory : IInvasionIntruderFactory`
- L15 [function] `public InvasionIntruderRuntimeFactory(ICharacterVisualRootFactory visualRootFactory)`
- L21 [function] `public InvasionIntruderRuntime Create(GameObject intruderPrefab, Vector3 position)`
- L31 [function] `public InvasionIntruderRuntime EnsureRuntime(GameObject intruderObject)`

### Assets\Scripts\Invasion\InvasionIntruderModel.cs

- Lines: 23
- Flags: None

- L5 [type] `public class InvasionIntruderSettings`
- L14 [type] `public enum InvasionIntruderState`

### Assets\Scripts\Invasion\InvasionIntruderPlanner.cs

- Lines: 105
- Flags: SceneMutation

- L5 [type] `public static class InvasionIntruderPlanner`
- L7 [function] `public static float CalculateFocus(float elapsedSeconds, float secondsToFullFocus)`
- L91 [function] `public static bool IsAtOwner(Grid grid, CharacterActor intruder, CharacterActor owner)`
- L101 [function] `private static int Manhattan(Vector2Int a, Vector2Int b)`

### Assets\Scripts\Invasion\InvasionIntruderSystem.cs

- Lines: 425
- Flags: EventBus, SceneMutation, DependencyInjection

- L8 [type] `public class InvasionDirectorRuntime : MonoBehaviour, UtilEventListener<InvasionCandidateEvent>`
- L39 [function] `public void OnTriggerEvent(InvasionCandidateEvent eventType)`
- L44 [function] `public bool TrySpawnIntruder(InvasionThreatSnapshot snapshot, out CharacterActor intruder)`
- L79 [function] `private void OnEnable()`
- L84 [function] `private void OnDisable()`
- L89 [function] `private CharacterSO ResolveIntruderData()`
- L95 [function] `private IInvasionIntruderDataProvider ResolveIntruderDataProvider()`
- L101 [function] `private IInvasionIntruderContext ResolveInvasionContext()`
- L107 [function] `private IInvasionIntruderFactory ResolveIntruderFactory()`
- L113 [function] `private IDefenseStatusRuntimeService ResolveDefenseStatusRuntimeService()`
- L119 [function] `private void OnIntruderFinished(InvasionIntruderRuntime runtime)`
- L131 [type] `public class InvasionIntruderRuntime : MonoBehaviour`
- L149 [function] `private void Awake()`
- L191 [function] `public Queue<GridMoveStep> CreateNextPath(Grid grid, Vector2Int ownerPosition, out bool direct)`
- L197 [function] `public bool TryDamageNearbyFacility(Grid grid)`
- L216 [function] `public void ApplyFinalCombat(CharacterActor owner)`
- L230 [function] `public void ResolveSuppressedBy(CharacterActor defender)`
- L247 [function] `private IEnumerator Run(Vector3 entryDoorPosition, Vector2Int entryGridPosition)`
- L310 [function] `private IEnumerator MovePathWithDefense(Grid grid, Queue<GridMoveStep> path)`
- L346 [function] `private void TickDefenseStatuses(float deltaSeconds)`
- L359 [function] `private IEnumerator FinalCombat(CharacterActor owner)`
- L371 [function] `private void ResolveIntruderDefeated()`
- L382 [function] `private void Finish()`
- L394 [function] `private void RequireRuntimeComponents()`
- L414 [function] `private IInvasionIntruderContext ResolveInvasionContext()`
- L420 [function] `private IDefenseStatusRuntimeService ResolveDefenseStatusRuntimeService()`

### Assets\Scripts\Invasion\InvasionThreatRuntime.cs

- Lines: 281
- Flags: EventBus, DependencyInjection

- L5 [type] `public class InvasionThreatRuntime : MonoBehaviour,`
- L46 [function] `private void Update()`
- L51 [function] `public void Tick(float deltaTime)`
- L92 [function] `public void AddThreat(float amount)`
- L100 [function] `public void OnTriggerEvent(InvasionStartedEvent eventType)`
- L105 [function] `public void OnTriggerEvent(InvasionResolvedEvent eventType)`
- L114 [function] `public void OnTriggerEvent(OperatingDayStartedEvent eventType)`
- L124 [function] `private void OnEnable()`
- L131 [function] `private void OnDisable()`
- L138 [function] `private void TryRaiseWarning()`
- L164 [function] `private void TickCandidateDelay(float deltaTime)`
- L200 [function] `private void ResetAfterInvasion()`
- L212 [function] `private InvasionThreatSnapshot BuildSnapshot()`
- L222 [function] `private InvasionThreatStage ResolveStage()`
- L246 [function] `private InvasionThreatFactors SampleWorldFactors()`
- L256 [function] `private float GetWarningThresholdMultiplier()`
- L262 [function] `private IRunVariableRuntimeReader RequireRunVariableReader()`
- L272 [function] `private IMetaProgressionRuntimeReader RequireMetaProgressionReader()`

### Assets\Scripts\Invasion\InvasionThreatSystem.cs

- Lines: 236
- Flags: EventBus

- L5 [type] `public enum InvasionThreatDifficulty`
- L12 [type] `public enum InvasionThreatStage`
- L21 [type] `public class InvasionThreatSettings`
- L44 [function] `public float GetDifficultyMultiplier()`
- L54 [function] `public float GetCandidateDelay()`
- L62 [type] `public readonly struct InvasionThreatFactors`
- L69 [function] `public InvasionThreatFactors(float dungeonValue, float reputation, float time, float risk)`
- L78 [type] `public readonly struct InvasionThreatSnapshot`
- L101 [type] `public struct InvasionThreatWarningEvent`
- L105 [function] `public InvasionThreatWarningEvent(InvasionThreatSnapshot snapshot)`
- L112 [function] `public static void Trigger(InvasionThreatSnapshot snapshot)`
- L119 [type] `public struct InvasionCandidateEvent`
- L123 [function] `public InvasionCandidateEvent(InvasionThreatSnapshot snapshot)`
- L130 [function] `public static void Trigger(InvasionThreatSnapshot snapshot)`
- L137 [type] `public struct InvasionStartedEvent`
- L141 [function] `public InvasionStartedEvent(InvasionThreatSnapshot snapshot)`
- L148 [function] `public static void Trigger(InvasionThreatSnapshot snapshot)`
- L155 [type] `public struct InvasionResolvedEvent`
- L160 [function] `public InvasionResolvedEvent(bool defended, float residualRisk)`
- L168 [function] `public static void Trigger(bool defended, float residualRisk = 0f)`
- L176 [type] `public static class InvasionThreatCalculator`
- L178 [function] `public static float CalculateRisePerSecond(InvasionThreatSettings settings, InvasionThreatFactors factors)`
- L183 [function] `public static float CalculateRisePerSecond(InvasionThreatSettings settings, InvasionThreatFactors factors, float runMultiplier)`
- L199 [function] `public static string BuildWarningDetail(InvasionThreatSnapshot snapshot)`
- L231 [function] `public static string BuildCandidateDetail(InvasionThreatSnapshot snapshot)`

### Assets\Scripts\Invasion\InvasionThreatWorldSampler.cs

- Lines: 135
- Flags: None

- L4 [type] `public interface IInvasionThreatWorldSampler`
- L9 [type] `public sealed class InvasionThreatWorldSampler : IInvasionThreatWorldSampler`
- L13 [function] `public InvasionThreatWorldSampler(IDungeonSceneComponentQuery sceneQuery)`
- L19 [function] `public InvasionThreatFactors Sample(float secondsSinceLastInvasion)`
- L31 [function] `private static float CalculateDungeonValue(IEnumerable<BuildableObject> buildings)`
- L60 [function] `private static float CalculateReputation(IEnumerable<CharacterActor> characters)`
- L93 [function] `private static float CalculateRisk(IEnumerable<BuildableObject> buildings)`

### Assets\Scripts\Meta\MetaProgressionCalculator.cs

- Lines: 35
- Flags: None

- L3 [type] `public static class MetaProgressionCalculator`
- L5 [function] `public static int CalculateLegacyCurrency(RunResultSnapshot result)`
- L25 [function] `private static int GetThreatStageScore(InvasionThreatStage stage)`

### Assets\Scripts\Meta\MetaProgressionCatalog.cs

- Lines: 77
- Flags: None

- L4 [type] `public static class MetaProgressionCatalog`
- L10 [function] `public static MetaUpgradeDefinition Get(MetaUpgradeId id)`
- L15 [function] `private static Dictionary<MetaUpgradeId, MetaUpgradeDefinition> BuildDefinitions()`

### Assets\Scripts\Meta\MetaProgressionEvents.cs

- Lines: 17
- Flags: EventBus

- L1 [type] `public struct RunResultReadyEvent`
- L5 [function] `public RunResultReadyEvent(RunResultSnapshot result)`
- L12 [function] `public static void Trigger(RunResultSnapshot result)`

### Assets\Scripts\Meta\MetaProgressionModel.cs

- Lines: 170
- Flags: None

- L5 [type] `public enum MetaProgressionBranch`
- L12 [type] `public enum MetaUpgradeId`
- L23 [type] `public sealed class MetaUpgradeDefinition`
- L33 [type] `public sealed class MetaProgressionState`
- L44 [function] `public void AddCurrency(int amount)`
- L49 [function] `public int GetUpgradeLevel(MetaUpgradeId id)`
- L54 [function] `public bool TryPurchaseUpgrade(MetaUpgradeId id, out string message)`
- L83 [function] `public void SetUpgradeLevelForDebug(MetaUpgradeId id, int level)`
- L91 [function] `public void PreserveRecipes(IEnumerable<string> recipeIds, int maxCount)`
- L105 [type] `public sealed class RunResultSnapshot`
- L121 [function] `public string ToDetailText()`
- L146 [function] `private static string FormatTime(float seconds)`
- L154 [function] `private static string TextOrDefault(string value, string defaultValue)`
- L159 [function] `private static string FormatThreatStage(InvasionThreatStage stage)`

### Assets\Scripts\Meta\MetaProgressionRunResultServices.cs

- Lines: 118
- Flags: None

- L3 [type] `public readonly struct MetaRunResultBuildContext`
- L5 [function] `public MetaRunResultBuildContext(CharacterActor owner, string reason, float runStartTime, int currentDay, int settlementCount, int defendedInvasionCount, InvasionThreatStage maxThreatStage, float finalInvasionThreat, int discoveredFacilityCount, int unlockedRecipeCount, int offenseSuccessCount)`
- L44 [type] `public interface IMetaRunResultBuilder`
- L49 [type] `public sealed class MetaRunResultBuilder : IMetaRunResultBuilder`
- L53 [function] `public MetaRunResultBuilder(IInvasionThreatRuntimeProvider threatRuntimeProvider)`
- L59 [function] `public RunResultSnapshot Build(MetaRunResultBuildContext context)`
- L91 [type] `public interface IRunResultPanelService`
- L96 [type] `public sealed class RunResultPanelService : IRunResultPanelService`
- L101 [function] `public RunResultPanelService(IDungeonSceneComponentQuery sceneQuery, IRunResultPanelFactory panelFactory)`
- L111 [function] `public RunResultPanel Show(RunResultSnapshot result)`

### Assets\Scripts\Meta\MetaProgressionSystem.cs

- Lines: 247
- Flags: EventBus, DependencyInjection

- L7 [type] `public class MetaProgressionRuntime :`
- L43 [function] `public void SetShowRunResultPanel(bool value)`
- L48 [function] `private void Awake()`
- L53 [function] `public void StartNewRun()`
- L60 [function] `public bool TryPurchaseUpgrade(MetaUpgradeId id, out string message)`
- L71 [function] `public int GetStartingFacilityCandidateBonus()`
- L76 [function] `public int GetStartingOwnerTraitCandidateBonus()`
- L81 [function] `public float GetOwnerMaxHealthMultiplier()`
- L86 [function] `public float GetInvasionWarningThresholdMultiplier()`
- L91 [function] `public bool IsRecipePreserved(string recipeId)`
- L97 [function] `public IReadOnlyCollection<int> GetExpandedBasicPurchaseBuildingIds(IEnumerable<BuildingSO> buildings)`
- L117 [function] `public void RecordOffenseSuccess()`
- L122 [function] `public void OnTriggerEvent(OperatingDayStartedEvent eventType)`
- L132 [function] `public void OnTriggerEvent(OperatingDayReportEvent eventType)`
- L137 [function] `public void OnTriggerEvent(InvasionThreatWarningEvent eventType)`
- L142 [function] `public void OnTriggerEvent(InvasionCandidateEvent eventType)`
- L147 [function] `public void OnTriggerEvent(InvasionStartedEvent eventType)`
- L152 [function] `public void OnTriggerEvent(InvasionResolvedEvent eventType)`
- L157 [function] `public void OnTriggerEvent(FacilityVisitEvent eventType)`
- L162 [function] `public void OnTriggerEvent(BlueprintResearchCompletedEvent eventType)`
- L167 [function] `public void OnTriggerEvent(FacilitySynthesisCompletedEvent eventType)`
- L172 [function] `public void OnTriggerEvent(OwnerRunEndedEvent eventType)`
- L177 [function] `public RunResultSnapshot EndRun(CharacterActor owner, string reason)`
- L201 [function] `private IMetaRunResultBuilder ResolveRunResultBuilder()`
- L207 [function] `private IRunResultPanelService ResolveRunResultPanelService()`
- L213 [function] `private void PreserveRunRecipes()`
- L219 [function] `private void OnEnable()`
- L233 [function] `private void OnDisable()`

### Assets\Scripts\Meta\MetaRunProgressTracker.cs

- Lines: 144
- Flags: None

- L5 [type] `public sealed class MetaRunProgressTracker`
- L20 [function] `public void StartNewRun(float startTime)`
- L33 [function] `public void RecordOperatingDayStarted(int day)`
- L38 [function] `public void RecordOperatingDayReport(OperatingDayReport report)`
- L49 [function] `public void RecordThreat(InvasionThreatSnapshot snapshot)`
- L59 [function] `public void RecordInvasionStarted(InvasionThreatSnapshot snapshot)`
- L65 [function] `public void RecordInvasionResolved(bool defended)`
- L73 [function] `public void RecordFacilityVisit(BuildableObject facility)`
- L82 [function] `public void RecordBlueprintResearchCompleted(BlueprintResearchUnlockResult unlockResult)`
- L90 [function] `public void RecordFacilitySynthesisCompleted(FacilitySynthesisResult result)`
- L105 [function] `public void RecordOffenseSuccess()`
- L110 [function] `public MetaRunResultBuildContext CreateResultContext(CharacterActor owner, string reason)`
- L126 [function] `private void RecordRecipe(string recipeId)`
- L134 [function] `private static int GetThreatStageScore(InvasionThreatStage stage)`

### Assets\Scripts\Meta\RunResultPanel.cs

- Lines: 29
- Flags: SceneMutation

- L4 [type] `public class RunResultPanel : MonoBehaviour`
- L8 [function] `public void Render(RunResultSnapshot result)`
- L18 [function] `public void Hide()`
- L23 [function] `private void EnsureView()`

### Assets\Scripts\Meta\RunResultPanelFactory.cs

- Lines: 67
- Flags: RuntimeObjectCreation, SceneMutation, DependencyInjection

- L6 [type] `public interface IRunResultPanelFactory`
- L11 [type] `public sealed class RunResultPanelFactory : IRunResultPanelFactory`
- L16 [function] `public RunResultPanelFactory(ITmpKoreanFontService tmpKoreanFontService, IObjectResolver objectResolver)`
- L26 [function] `public RunResultPanel CreateDefaultPanel()`

### Assets\Scripts\Offense\OffenseExpeditionEvents.cs

- Lines: 35
- Flags: EventBus

- L1 [type] `public struct OffenseExpeditionStartedEvent`
- L5 [function] `public OffenseExpeditionStartedEvent(OffenseExpeditionRun expedition)`
- L12 [function] `public static void Trigger(OffenseExpeditionRun expedition)`
- L19 [type] `public struct OffenseExpeditionCompletedEvent`
- L23 [function] `public OffenseExpeditionCompletedEvent(OffenseExpeditionResult result)`
- L30 [function] `public static void Trigger(OffenseExpeditionResult result)`

### Assets\Scripts\Offense\OffenseExpeditionModel.cs

- Lines: 122
- Flags: None

- L6 [type] `public sealed class OffenseExpeditionMemberSnapshot`
- L14 [function] `public string ToSummaryText()`
- L22 [type] `public sealed class OffenseExpeditionResult`
- L36 [function] `public string ToDetailText()`
- L89 [type] `public sealed class OffenseExpeditionRun`
- L118 [function] `public void Tick(float deltaTime)`

### Assets\Scripts\Offense\OffenseExpeditionService.cs

- Lines: 163
- Flags: None

- L6 [type] `public static class OffenseExpeditionService`
- L8 [function] `public static bool CanJoinExpedition(CharacterActor actor, out string reason)`
- L62 [function] `public static float CalculateMemberPower(CharacterActor actor)`
- L80 [function] `public static float CalculatePartyPower(IEnumerable<CharacterActor> members)`
- L85 [function] `public static bool ShouldSucceed(OffenseExpeditionRun expedition)`

### Assets\Scripts\Offense\OffenseExpeditionSystem.cs

- Lines: 380
- Flags: SceneMutation, DependencyInjection

- L8 [type] `public class OffenseExpeditionRuntime : MonoBehaviour`
- L20 [function] `public void Construct(IOffenseExpeditionMemberQuery memberQuery, IOffenseWorldMapRuntimeProvider worldMapProvider, IOffenseRewardRuntimeProvider rewardProvider, IMetaProgressionRuntimeProvider metaProgressionProvider, IOffensePanelService panelService)`
- L39 [function] `private void Update()`
- L44 [function] `public IReadOnlyList<CharacterActor> GetAvailableMemberActors()`
- L49 [function] `public bool TryStartExpedition(string targetId, IEnumerable<CharacterActor> members, out OffenseExpeditionRun expedition, out string message)`
- L106 [function] `public void Tick(float deltaTime)`
- L119 [function] `public bool CompleteExpeditionForDebug(string expeditionId, bool? forceSuccess, out OffenseExpeditionResult result)`
- L135 [function] `private OffenseExpeditionResult CompleteExpeditionAt(int index, bool? forceSuccess)`
- L176 [function] `public OffenseExpeditionPanel ShowExpeditionPanel()`
- L181 [function] `private IOffenseExpeditionMemberQuery ResolveMemberQuery()`
- L187 [function] `private IOffenseWorldMapRuntimeProvider ResolveWorldMapProvider()`
- L193 [function] `private IOffenseRewardRuntimeProvider ResolveRewardProvider()`
- L199 [function] `private IMetaProgressionRuntimeProvider ResolveMetaProgressionProvider()`
- L205 [function] `private IOffensePanelService ResolvePanelService()`
- L212 [type] `public class OffenseExpeditionPanel : MonoBehaviour`
- L224 [function] `public void Bind(OffenseExpeditionRuntime source, OffenseWorldMapRuntime worldMap, IOffensePanelButtonFactory buttonFactory)`
- L239 [function] `public void Render()`
- L315 [function] `public void Hide()`
- L320 [function] `private static string BuildMemberLabel(CharacterActor member)`
- L334 [function] `private void EnsureView()`
- L345 [function] `private void ClearButtons()`
- L358 [function] `internal void BindGeneratedView(TMP_Text headerText, TMP_Text detailText, RectTransform memberButtonRoot)`
- L374 [function] `private IOffensePanelButtonFactory RequireButtonFactory()`

### Assets\Scripts\Offense\OffensePanelFactory.cs

- Lines: 117
- Flags: RuntimeObjectCreation, SceneMutation, DependencyInjection

- L5 [type] `public interface IOffensePanelFactory`
- L11 [type] `public sealed class OffensePanelFactory : IOffensePanelFactory`
- L16 [function] `public OffensePanelFactory(ITmpKoreanFontService tmpKoreanFontService, IObjectResolver objectResolver)`
- L26 [function] `public OffenseWorldMapPanel CreateWorldMapPanel()`
- L72 [function] `public OffenseExpeditionPanel CreateExpeditionPanel()`

### Assets\Scripts\Offense\OffensePanelUiFactory.cs

- Lines: 161
- Flags: RuntimeObjectCreation, SceneMutation

- L6 [type] `public static class OffensePanelUiFactory`
- L8 [function] `public static GameObject CreateOverlayCanvas(string name, int sortingOrder, Vector2 referenceResolution)`
- L24 [function] `public static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)`
- L42 [function] `public static GameObject CreateVerticalRoot(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, float spacing)`
- L67 [function] `public static GameObject CreateText(Transform parent, string name, float fontSize, TextAlignmentOptions alignment, ITmpKoreanFontService tmpKoreanFontService)`
- L90 [function] `public static GameObject CreateButton(RectTransform parent, string label, float fontSize, Action callback, ITmpKoreanFontService tmpKoreanFontService)`
- L116 [type] `public interface IOffensePanelButtonFactory`
- L118 [function] `GameObject CreateButton(RectTransform parent, string label, float fontSize, Action callback)`
- L119 [function] `void Release(GameObject buttonObject)`
- L122 [type] `public sealed class OffensePanelButtonFactory : IOffensePanelButtonFactory`
- L126 [function] `public OffensePanelButtonFactory(ITmpKoreanFontService tmpKoreanFontService)`
- L132 [function] `public GameObject CreateButton(RectTransform parent, string label, float fontSize, Action callback)`
- L142 [function] `public void Release(GameObject buttonObject)`

### Assets\Scripts\Offense\OffenseRewardContextResolver.cs

- Lines: 78
- Flags: None

- L5 [type] `public sealed class OffenseRewardDebugContext`
- L12 [function] `public void Clear()`
- L21 [type] `public interface IOffenseRewardContextBuilder`
- L29 [type] `public sealed class OffenseRewardContextBuilder : IOffenseRewardContextBuilder`
- L33 [function] `public OffenseRewardContextBuilder(IDungeonSceneComponentQuery sceneQuery)`
- L72 [function] `private IEnumerable<IWarehouseFacility> ResolveWarehouses()`

### Assets\Scripts\Offense\OffenseRewardEvents.cs

- Lines: 27
- Flags: EventBus

- L4 [type] `public struct OffenseRewardGrantedEvent`

### Assets\Scripts\Offense\OffenseRewardModel.cs

- Lines: 121
- Flags: None

- L5 [type] `public sealed class OffenseRewardGrantResult`
- L14 [function] `public string ToSummaryText()`
- L29 [type] `public sealed class OffenseRewardState`
- L45 [function] `public void Reset()`
- L58 [function] `public void RecordMoney(int amount)`
- L63 [function] `public void RecordStock(StockCategory category, int amount)`
- L73 [function] `public bool RecordRareFacility(BuildingSO building)`
- L78 [function] `public bool RecordBlueprint(FacilityBlueprintSO blueprint)`
- L83 [function] `public void RecordFactionWeakening(bool humanFaction, int amount)`
- L96 [function] `public void RecordRecruitCandidates(int amount)`
- L101 [function] `public void RecordPrisoners(int amount)`
- L106 [function] `public void RecordSpecialMonsters(int amount)`
- L112 [type] `public sealed class OffenseRewardContext`

### Assets\Scripts\Offense\OffenseRewardSelector.cs

- Lines: 117
- Flags: None

- L5 [type] `public sealed class OffenseRewardSelector : IOffenseRewardSelector`
- L9 [function] `public OffenseRewardSelector(IOffenseRewardCatalog catalog)`
- L15 [function] `public StockCategory ResolveStockCategory(OffenseRewardPreview reward)`
- L92 [function] `public bool IsHumanFactionWeakening(OffenseRewardPreview reward, OffenseTargetDefinition target)`
- L107 [function] `public bool ContainsAny(string source, params string[] values)`

### Assets\Scripts\Offense\OffenseRewardSystem.cs

- Lines: 294
- Flags: DependencyInjection

- L7 [type] `public sealed class OffenseRewardGrantService : IOffenseRewardGrantService`
- L11 [function] `public OffenseRewardGrantService(IOffenseRewardSelector selector)`
- L205 [function] `private static OffenseRewardGrantResult Fail(OffenseRewardPreview reward, string detail)`
- L219 [type] `public class OffenseRewardRuntime : MonoBehaviour`
- L268 [function] `public void ClearDebugContext()`
- L273 [function] `public void ResetState()`
- L278 [function] `private OffenseRewardContext CreateContext(OffenseTargetDefinition target)`
- L283 [function] `private IOffenseRewardContextBuilder ResolveContextBuilder()`
- L289 [function] `private IOffenseRewardGrantService ResolveGrantService()`

### Assets\Scripts\Offense\OffenseRuntimeServices.cs

- Lines: 177
- Flags: None

- L5 [type] `public interface IOffenseWorldMapRuntimeProvider`
- L10 [type] `public interface IOffenseRewardRuntimeProvider`
- L15 [type] `public interface IOffenseExpeditionMemberQuery`
- L20 [type] `public interface IOffenseRewardCatalog`
- L26 [type] `public interface IOffenseRewardSelector`
- L39 [type] `public interface IOffenseRewardGrantService`
- L46 [type] `public interface IOffensePanelService`
- L52 [type] `public sealed class OffenseWorldMapRuntimeProvider : IOffenseWorldMapRuntimeProvider`
- L56 [function] `public OffenseWorldMapRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)`
- L62 [function] `public bool TryGetRuntime(out OffenseWorldMapRuntime runtime)`
- L69 [type] `public sealed class OffenseRewardRuntimeProvider : IOffenseRewardRuntimeProvider`
- L73 [function] `public OffenseRewardRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)`
- L79 [function] `public bool TryGetRuntime(out OffenseRewardRuntime runtime)`
- L86 [type] `public sealed class OffenseExpeditionMemberQuery : IOffenseExpeditionMemberQuery`
- L90 [function] `public OffenseExpeditionMemberQuery(IDungeonSceneComponentQuery sceneQuery)`
- L96 [function] `public IReadOnlyList<CharacterActor> GetAvailableMemberActors()`
- L105 [type] `public sealed class DataCatalogOffenseRewardCatalog : IOffenseRewardCatalog`
- L109 [function] `public DataCatalogOffenseRewardCatalog(IDataCatalog catalog)`
- L128 [type] `public sealed class OffensePanelService : IOffensePanelService`
- L135 [function] `public OffensePanelService(IDungeonSceneComponentQuery sceneQuery, IOffenseWorldMapRuntimeProvider worldMapProvider, IOffensePanelFactory panelFactory, IOffensePanelButtonFactory buttonFactory)`
- L151 [function] `public OffenseWorldMapPanel ShowWorldMap(OffenseWorldMapRuntime runtime)`
- L164 [function] `public OffenseExpeditionPanel ShowExpedition(OffenseExpeditionRuntime runtime)`

### Assets\Scripts\Offense\OffenseTabSummaryQuery.cs

- Lines: 91
- Flags: None

- L3 [type] `public readonly struct OffenseTabSummary`
- L39 [type] `public interface IOffenseTabSummaryService`
- L44 [type] `public interface IOffenseTabSummaryRuntimeSource`
- L51 [type] `public sealed class OffenseTabSummaryRuntimeSource : IOffenseTabSummaryRuntimeSource`
- L55 [function] `public OffenseTabSummaryRuntimeSource(IDungeonSceneComponentQuery sceneQuery)`
- L65 [type] `public sealed class OffenseTabSummaryService : IOffenseTabSummaryService`
- L69 [function] `public OffenseTabSummaryService(IOffenseTabSummaryRuntimeSource runtimeSource)`
- L74 [function] `public OffenseTabSummary Capture()`

### Assets\Scripts\Offense\OffenseWorldMapEvents.cs

- Lines: 69
- Flags: EventBus

- L4 [type] `public struct OffenseWorldMapChangedEvent`
- L29 [type] `public struct OffenseTargetSelectedEvent`
- L33 [function] `public OffenseTargetSelectedEvent(OffenseTargetSnapshot target)`
- L40 [function] `public static void Trigger(OffenseTargetSnapshot target)`
- L47 [type] `public struct OffenseReconUpgradedEvent`
- L53 [function] `public OffenseReconUpgradedEvent(int reconLevel, float scanRange, int newlyRevealedCount)`
- L62 [function] `public static void Trigger(int reconLevel, float scanRange, int newlyRevealedCount)`

### Assets\Scripts\Offense\OffenseWorldMapModel.cs

- Lines: 191
- Flags: None

- L6 [type] `public enum OffenseTargetKind`
- L14 [type] `public enum OffenseRewardCategory`
- L26 [type] `public class OffenseRewardPreview`
- L32 [function] `public string ToSummaryText()`
- L40 [type] `public class OffenseTargetDefinition`
- L58 [function] `public OffenseTargetSnapshot ToSnapshot(bool preciseIntel)`
- L81 [type] `public class OffenseTargetSnapshot`
- L94 [function] `public string ToDetailText()`
- L129 [function] `private static string FormatDuration(float seconds)`
- L137 [function] `private static string GetKindName(OffenseTargetKind kind)`
- L150 [type] `public sealed class OffenseWorldMapState`
- L158 [function] `public void Reset(int reconLevel = 0)`
- L165 [function] `public bool KnowTarget(string targetId)`
- L170 [function] `public bool AddKnownTarget(string targetId)`
- L175 [function] `public void SetSelectedTarget(string targetId)`
- L180 [function] `public bool TryUpgradeRecon(int maxLevel)`

### Assets\Scripts\Offense\OffenseWorldMapService.cs

- Lines: 189
- Flags: None

- L6 [type] `public static class OffenseWorldMapService`
- L10 [function] `public static float GetScanRange(int reconLevel)`
- L21 [function] `public static IReadOnlyList<OffenseTargetDefinition> CreateDefaultTargets()`
- L90 [function] `public static IReadOnlyList<OffenseTargetDefinition> NormalizeTargets(IEnumerable<OffenseTargetDefinition> targets)`
- L180 [function] `private static OffenseRewardPreview Reward(OffenseRewardCategory category, string label, int amount)`

### Assets\Scripts\Offense\OffenseWorldMapSystem.cs

- Lines: 279
- Flags: SceneMutation, DependencyInjection

- L8 [type] `public class OffenseWorldMapRuntime : MonoBehaviour`
- L39 [function] `public void Construct(IOffensePanelService panelService)`
- L45 [function] `private void Awake()`
- L50 [function] `public void StartWorldMap(int reconLevel = 0)`
- L58 [function] `public bool TryUpgradeRecon(out string message)`
- L75 [function] `public bool TrySelectTarget(string targetId, out OffenseTargetSnapshot snapshot, out string message)`
- L94 [function] `public bool TryGetKnownTargetSnapshot(string targetId, out OffenseTargetSnapshot snapshot)`
- L108 [function] `public OffenseWorldMapPanel ShowWorldMap()`
- L114 [function] `public void SetPreciseIntelForDebug(bool value)`
- L120 [function] `public void SetTargetsForDebug(IEnumerable<OffenseTargetDefinition> debugTargets)`
- L127 [function] `private void EnsureInitialized()`
- L138 [function] `private void RaiseChanged()`
- L148 [function] `private IOffensePanelService ResolvePanelService()`
- L155 [type] `public class OffenseWorldMapPanel : MonoBehaviour`
- L164 [function] `public void Bind(OffenseWorldMapRuntime source, IOffensePanelButtonFactory buttonFactory)`
- L175 [function] `public void Render()`
- L231 [function] `public void Hide()`
- L236 [function] `private void EnsureView()`
- L247 [function] `private void ClearButtons()`
- L257 [function] `internal void BindGeneratedView(TMP_Text headerText, TMP_Text detailText, RectTransform targetButtonRoot)`
- L273 [function] `private IOffensePanelButtonFactory RequireButtonFactory()`

### Assets\Scripts\Operation\EventAlertCanvasProvider.cs

- Lines: 39
- Flags: RuntimeObjectCreation

- L4 [type] `public interface IEventAlertCanvasProvider`
- L9 [type] `public sealed class EventAlertCanvasProvider : IEventAlertCanvasProvider`
- L14 [function] `public EventAlertCanvasProvider(DungeonSceneRuntimeReferences sceneReferences)`
- L20 [function] `public Canvas GetOrCreateCanvas()`

### Assets\Scripts\Operation\EventAlertChoicePresenter.cs

- Lines: 53
- Flags: SceneMutation, DependencyInjection

- L6 [type] `public sealed class EventAlertChoicePresenter`
- L11 [function] `public EventAlertChoicePresenter(IEventAlertButtonFactory buttonFactory)`
- L17 [function] `public void Rebuild(Transform parent, EventAlertRecord record, Func<int, bool> executeChoice)`
- L41 [function] `public void Clear()`

### Assets\Scripts\Operation\EventAlertEvents.cs

- Lines: 35
- Flags: EventBus

- L1 [type] `public struct EventAlertRequestedEvent`
- L5 [function] `public EventAlertRequestedEvent(EventAlertRequest request)`
- L12 [function] `public static void Trigger(EventAlertRequest request)`
- L19 [type] `public struct EventAlertLoggedEvent`
- L23 [function] `public EventAlertLoggedEvent(EventAlertRecord record)`
- L30 [function] `public static void Trigger(EventAlertRecord record)`

### Assets\Scripts\Operation\EventAlertLayout.cs

- Lines: 144
- Flags: None

- L5 [type] `public static class EventAlertLayout`
- L92 [function] `private static float GetAlertListHeight(Canvas canvas)`
- L105 [function] `private static float GetButtonViewportHeight(RectTransform buttonViewportRect)`
- L115 [function] `private static int GetVisibleRowsForHeight(float availableHeight)`
- L130 [function] `private static int GetMaxVisibleRows()`
- L137 [function] `private static float GetContentHeightForRows(int rowCount)`

### Assets\Scripts\Operation\EventAlertMergePolicy.cs

- Lines: 24
- Flags: None

- L4 [type] `public static class EventAlertMergePolicy`
- L6 [function] `public static EventAlertRecord FindMergeTarget(IEnumerable<EventAlertRecord> records, EventAlertRequest request)`
- L16 [function] `private static bool CanMerge(EventAlertRecord record, EventAlertRequest request)`

### Assets\Scripts\Operation\EventAlertModel.cs

- Lines: 135
- Flags: None

- L5 [type] `public enum EventAlertImportance`
- L12 [type] `public class EventAlertChoice`
- L18 [function] `public EventAlertChoice(string label, string description = "", Action callback = null)`
- L26 [type] `public class EventAlertRequest`
- L48 [function] `private static IReadOnlyList<EventAlertChoice> NormalizeChoices(IEnumerable<EventAlertChoice> choices)`
- L58 [type] `public class EventAlertRecord`
- L68 [function] `public EventAlertRecord(int id, EventAlertRequest request)`
- L79 [function] `public void Increment()`
- L86 [function] `public string ToDetailText()`
- L125 [function] `private static string GetImportanceName(EventAlertImportance importance)`

### Assets\Scripts\Operation\EventAlertSelectionState.cs

- Lines: 23
- Flags: None

- L1 [type] `public sealed class EventAlertSelectionState`
- L5 [function] `public void Select(EventAlertRecord record)`
- L13 [function] `public bool ExecuteChoice(int index)`

### Assets\Scripts\Operation\EventAlertService.cs

- Lines: 29
- Flags: None

- L3 [type] `public static class EventAlertService`
- L15 [function] `public static void RaiseInvasionResult(string detail, EventAlertImportance importance = EventAlertImportance.High)`
- L20 [function] `public static void RaiseStaffComplaint(string detail, EventAlertImportance importance = EventAlertImportance.Medium)`
- L25 [function] `public static void RaiseBlueprintAcquired(string detail, EventAlertImportance importance = EventAlertImportance.Medium)`

### Assets\Scripts\Operation\EventAlertSystem.cs

- Lines: 129
- Flags: EventBus, SceneMutation, DependencyInjection

- L6 [type] `public class EventAlertRuntime : MonoBehaviour, UtilEventListener<EventAlertRequestedEvent>`
- L25 [function] `public void Construct(IEventAlertViewPresenterFactory viewPresenterFactory)`
- L32 [function] `public void OnTriggerEvent(EventAlertRequestedEvent eventType)`
- L55 [function] `public void Open(EventAlertRecord record)`
- L66 [function] `public void CloseDetail()`
- L71 [function] `public bool ExecuteChoice(int index)`
- L82 [function] `private void OnEnable()`
- L87 [function] `private void OnDisable()`
- L92 [function] `private void OnDestroy()`
- L97 [function] `private void CreateButton(EventAlertRecord record)`
- L102 [function] `private void UpdateButton(EventAlertRecord record)`
- L107 [function] `private IEventAlertViewPresenter ResolveViewPresenter()`

### Assets\Scripts\Operation\EventAlertUiFactory.cs

- Lines: 361
- Flags: RuntimeObjectCreation, SceneMutation, DependencyInjection

- L7 [type] `public static class EventAlertUiFactory`
- L9 [function] `public static GameObject CreateRuntimeRoot(Canvas canvas)`
- L22 [function] `public static Button CreateAlertButton(Transform buttonRoot, EventAlertRecord record, UnityAction onClick, ITmpKoreanFontService tmpKoreanFontService)`
- L60 [function] `public static void UpdateAlertButton(Button button, EventAlertRecord record)`
- L74 [function] `public static Button CreateChoiceButton(Transform parent, EventAlertChoice choice, int choiceIndex, UnityAction onClick, ITmpKoreanFontService tmpKoreanFontService)`
- L115 [function] `public static void CreateButtonRoot(...)`
- L171 [function] `public static void BindExistingButtonRootReferences(...)`
- L192 [function] `public static bool IsButtonRootReady(...)`
- L205 [function] `public static GameObject CreateDetailPanel(...)`
- L263 [function] `private static TMP_Text CreateText(...)`
- L283 [function] `private static Color GetImportanceColor(EventAlertImportance importance)`
- L295 [type] `public interface IEventAlertButtonFactory`
- L297 [function] `Button CreateAlertButton(Transform buttonRoot, EventAlertRecord record, UnityAction onClick)`
- L298 [function] `void UpdateAlertButton(Button button, EventAlertRecord record)`
- L299 [function] `Button CreateChoiceButton(Transform parent, EventAlertChoice choice, int choiceIndex, UnityAction onClick)`
- L300 [function] `void Release(Button button)`
- L303 [type] `public sealed class EventAlertButtonFactory : IEventAlertButtonFactory`
- L307 [function] `public EventAlertButtonFactory(ITmpKoreanFontService tmpKoreanFontService)`
- L313 [function] `public Button CreateAlertButton(Transform buttonRoot, EventAlertRecord record, UnityAction onClick)`
- L322 [function] `public void UpdateAlertButton(Button button, EventAlertRecord record)`
- L327 [function] `public Button CreateChoiceButton(...)`
- L341 [function] `public void Release(Button button)`

### Assets\Scripts\Operation\EventAlertViewPresenter.cs

- Lines: 252
- Flags: SceneMutation, DependencyInjection

- L7 [type] `public readonly struct EventAlertViewPresenterContext`
- L33 [type] `public interface IEventAlertViewPresenter`
- L44 [type] `public interface IEventAlertViewPresenterFactory`
- L49 [type] `public sealed class EventAlertViewPresenterFactory : IEventAlertViewPresenterFactory`
- L55 [function] `public EventAlertViewPresenterFactory(IEventAlertCanvasProvider canvasProvider, ITmpKoreanFontService tmpKoreanFontService, IEventAlertButtonFactory buttonFactory)`
- L68 [function] `public IEventAlertViewPresenter Create(EventAlertViewPresenterContext context)`
- L74 [type] `public sealed class EventAlertViewPresenter : IEventAlertViewPresenter`
- L93 [function] `public EventAlertViewPresenter(...)`
- L117 [function] `public bool IsDetailVisible => detailPanel != null && detailPanel.activeSelf`
- L119 [function] `public void EnsureRuntimeUI()`
- L166 [function] `public void DestroyRuntimeUI()`
- L185 [function] `public void CreateButton(EventAlertRecord record)`
- L206 [function] `public void UpdateButton(EventAlertRecord record)`
- L216 [function] `public void OpenDetail(EventAlertRecord record)`
- L229 [function] `public void CloseDetail()`
- L237 [function] `private void LayoutButtons()`
- L242 [function] `private void ClearButtons()`

### Assets\Scripts\Operation\OperatingDayReportAlertBridge.cs

- Lines: 36
- Flags: EventBus

- L3 [type] `public class OperatingDayReportAlertBridge : MonoBehaviour, UtilEventListener<OperatingDayReportEvent>`
- L5 [function] `public void OnTriggerEvent(OperatingDayReportEvent eventType)`
- L27 [function] `private void OnEnable()`
- L32 [function] `private void OnDisable()`

### Assets\Scripts\Operation\OperatingDaySettlement.cs

- Lines: 662
- Flags: EventBus, DependencyInjection

- L8 [type] `public class FacilityRevenueSummary`
- L15 [type] `public class SpeciesVisitSummary`
- L22 [type] `public class StockConsumptionSummary`
- L29 [type] `public class WarehouseStockSummary`
- L41 [type] `public class StaffWorkSummary`
- L51 [type] `public class OperatingDayReport`
- L74 [function] `public string ToDetailText()`
- L105 [function] `private static string FormatList(string title, IEnumerable<string> rows)`
- L120 [function] `private static string TextOrDefault(string value, string defaultValue)`
- L125 [function] `private static string GetStockCategoryName(StockCategory category)`
- L138 [type] `public struct OperatingDayStartedEvent`
- L142 [function] `public OperatingDayStartedEvent(int day)`
- L149 [function] `public static void Trigger(int day)`
- L156 [type] `public struct OperatingDayEndedEvent`
- L160 [function] `public OperatingDayEndedEvent(int day)`
- L167 [function] `public static void Trigger(int day)`
- L174 [type] `public struct OperatingDayReportEvent`
- L178 [function] `public OperatingDayReportEvent(OperatingDayReport report)`
- L185 [function] `public static void Trigger(OperatingDayReport report)`
- L192 [type] `public struct FacilityVisitEvent`
- L197 [function] `public FacilityVisitEvent(CharacterActor visitor, BuildableObject facility)`
- L205 [function] `public static void Trigger(CharacterActor visitor, BuildableObject facility)`
- L213 [type] `public struct FacilityRevenueEvent`
- L219 [function] `public FacilityRevenueEvent(CharacterActor customer, BuildableObject facility, int revenue)`
- L228 [function] `public static void Trigger(CharacterActor customer, BuildableObject facility, int revenue)`
- L237 [type] `public struct FacilityStockConsumedEvent`
- L244 [function] `public FacilityStockConsumedEvent(CharacterActor consumer, BuildableObject facility, StockCategory category, int amount)`
- L254 [function] `public static void Trigger(CharacterActor consumer, BuildableObject facility, StockCategory category, int amount)`
- L264 [type] `public enum FacilityCrimeKind`
- L269 [type] `public struct FacilityCrimeEvent`
- L309 [type] `public struct FacilityRestockEvent`
- L316 [function] `public FacilityRestockEvent(BuildableObject facility, int requestedAmount, int restockedAmount, string message)`
- L326 [function] `public static void Trigger(BuildableObject facility, int requestedAmount, int restockedAmount, string message)`
- L336 [type] `public class OperatingDaySettlementRuntime : MonoBehaviour,`
- L379 [function] `public void OnTriggerEvent(OperatingDayStartedEvent eventType)`
- L385 [function] `public void OnTriggerEvent(OperatingDayEndedEvent eventType)`
- L392 [function] `public void OnTriggerEvent(FacilityVisitEvent eventType)`
- L412 [function] `public void OnTriggerEvent(FacilityRevenueEvent eventType)`
- L424 [function] `public void OnTriggerEvent(FacilityStockConsumedEvent eventType)`
- L434 [function] `public void OnTriggerEvent(FacilityCrimeEvent eventType)`
- L442 [function] `public void OnTriggerEvent(FacilityRestockEvent eventType)`
- L450 [function] `public void OnTriggerEvent(StockSupplyEvent eventType)`
- L455 [function] `public void OnTriggerEvent(EventAlertLoggedEvent eventType)`
- L465 [function] `private void OnEnable()`
- L478 [function] `private void OnDisable()`
- L491 [function] `private OperatingDayReport BuildReport(int day)`
- L532 [function] `private void FillBuildingSnapshot(OperatingDayReport report, IEnumerable<BuildableObject> buildings)`
- L565 [function] `private static void FillStaffSnapshot(OperatingDayReport report, IEnumerable<CharacterActor> characters)`
- L589 [function] `private void ResetLedger()`
- L603 [function] `private static string GetFacilityName(BuildableObject facility)`
- L614 [function] `private static float GetStat(CharacterActor actor, CharacterCondition condition)`
- L625 [function] `private static bool IsStaffCharacter(CharacterActor actor)`
- L633 [function] `private IDungeonSceneComponentQuery RequireSceneQuery()`
- L643 [function] `private IFacilityShopCatalog RequireFacilityShopCatalog()`
- L653 [function] `private IRunVariableRuntimeReader RequireRunVariableReader()`

### Assets\Scripts\Operation\OperationTabSummaryQuery.cs

- Lines: 103
- Flags: None

- L4 [type] `public readonly struct OperationTabSummary`
- L39 [type] `public interface IOperationTabSummaryService`
- L44 [type] `public interface IOperationTabSummaryRuntimeSource`
- L52 [type] `public sealed class OperationTabSummaryRuntimeSource : IOperationTabSummaryRuntimeSource`
- L56 [function] `public OperationTabSummaryRuntimeSource(DungeonSceneRuntimeReferences sceneReferences)`
- L67 [type] `public sealed class OperationTabSummaryService : IOperationTabSummaryService`
- L71 [function] `public OperationTabSummaryService(IOperationTabSummaryRuntimeSource runtimeSource)`
- L76 [function] `public OperationTabSummary Capture()`

### Assets\Scripts\Recruitment\RegularCustomerSystem.cs

- Lines: 521
- Flags: EventBus

- L6 [type] `public enum RegularCustomerStatus`
- L15 [type] `public enum RecruitCapability`
- L25 [type] `public class RegularCustomerRules`
- L33 [function] `public static RegularCustomerRules CreateDefault()`
- L39 [type] `public sealed class RegularCustomerSnapshot`
- L49 [function] `public string ToSummaryText()`
- L55 [type] `public sealed class RegularCustomerRecord`
- L59 [function] `public RegularCustomerRecord(int customerId, CharacterActor customer, RecruitCapability recruitCapabilities)`
- L91 [function] `public void RecordVisit(CharacterActor customer, float satisfaction, RegularCustomerRules rules)`
- L115 [function] `public bool MarkRecruited()`
- L127 [function] `public RegularCustomerSnapshot ToSnapshot()`
- L142 [type] `public readonly struct RegularCustomerVisitResult`
- L165 [type] `public readonly struct RegularCustomerRecruitResult`
- L167 [function] `public RegularCustomerRecruitResult(bool success, RegularCustomerRecord record, string message)`
- L182 [type] `public sealed class RegularCustomerState`
- L190 [function] `public RegularCustomerVisitResult RecordVisit(CharacterActor customer, RegularCustomerRules rules)`
- L214 [function] `public bool TryGetRecord(int customerId, out RegularCustomerRecord record)`
- L219 [function] `public bool IsRecruited(int customerId)`
- L224 [function] `public bool TryRecruit(int customerId, out RegularCustomerRecruitResult result)`
- L255 [function] `private RegularCustomerRecord GetOrCreate(int customerId, CharacterActor customer, RegularCustomerRules rules)`
- L267 [type] `public struct RegularCustomerUpdatedEvent`
- L271 [function] `public RegularCustomerUpdatedEvent(RegularCustomerVisitResult result)`
- L278 [function] `public static void Trigger(RegularCustomerVisitResult result)`
- L285 [type] `public struct RegularCustomerBecameRegularEvent`
- L289 [function] `public RegularCustomerBecameRegularEvent(RegularCustomerSnapshot snapshot)`
- L296 [function] `public static void Trigger(RegularCustomerSnapshot snapshot)`
- L303 [type] `public struct RecruitCandidateDiscoveredEvent`
- L307 [function] `public RecruitCandidateDiscoveredEvent(RegularCustomerSnapshot snapshot)`
- L314 [function] `public static void Trigger(RegularCustomerSnapshot snapshot)`
- L321 [type] `public struct CustomerRecruitedEvent`
- L325 [function] `public CustomerRecruitedEvent(RegularCustomerRecruitResult result)`
- L332 [function] `public static void Trigger(RegularCustomerRecruitResult result)`
- L339 [type] `public static class RegularCustomerService`
- L341 [function] `public static bool IsTrackableCustomer(CharacterActor customer)`
- L350 [function] `public static int GetCustomerId(CharacterActor customer)`
- L361 [function] `public static string GetCustomerDisplayName(CharacterActor customer, int customerId)`
- L377 [function] `public static string GetCustomerSpeciesTag(CharacterActor customer)`
- L388 [function] `public static float GetSatisfaction(CharacterActor customer)`
- L401 [function] `public static CharacterSO GetCustomerData(CharacterActor customer)`
- L406 [function] `private static CharacterIdentity GetIdentity(CharacterActor customer)`
- L411 [function] `public static bool MeetsRegularCondition(RegularCustomerRecord record, RegularCustomerRules rules)`
- L419 [function] `public static bool MeetsRecruitCandidateCondition(RegularCustomerRecord record, RegularCustomerRules rules)`
- L428 [function] `public static bool CanSpawnAsCustomer(CharacterSO data, RegularCustomerState state)`
- L443 [function] `public static string FormatCapabilities(RecruitCapability capabilities)`
- L453 [type] `public class RegularCustomerRuntime : MonoBehaviour, UtilEventListener<FacilityVisitEvent>`
- L462 [function] `public void OnTriggerEvent(FacilityVisitEvent eventType)`
- L495 [function] `public bool TryRecruit(int customerId, out RegularCustomerRecruitResult result)`
- L512 [function] `private void OnEnable()`
- L517 [function] `private void OnDisable()`

### Assets\Scripts\Research\BlueprintResearchSystem.cs

- Lines: 448
- Flags: EventBus, DependencyInjection

- L8 [type] `public class BlueprintResearchTask`
- L13 [function] `public BlueprintResearchTask(FacilityBlueprintSO blueprint)`
- L25 [function] `public float AddProgress(float amount)`
- L38 [type] `public class BlueprintResearchState`
- L50 [function] `public bool EnqueueBlueprint(FacilityBlueprintSO blueprint)`
- L66 [function] `public bool TryGetActiveTask(out BlueprintResearchTask task)`
- L72 [function] `public bool IsCompleted(FacilityBlueprintSO blueprint)`
- L77 [function] `public void MarkCompleted(FacilityBlueprintSO blueprint)`
- L87 [function] `public bool UnlockRecipe(string recipeId)`
- L93 [type] `public readonly struct BlueprintResearchUnlockResult`
- L113 [type] `public readonly struct BlueprintResearchWorkResult`
- L143 [type] `public struct BlueprintResearchQueuedEvent`
- L147 [function] `public BlueprintResearchQueuedEvent(FacilityBlueprintSO blueprint)`
- L154 [function] `public static void Trigger(FacilityBlueprintSO blueprint)`
- L161 [type] `public struct BlueprintResearchProgressEvent`
- L165 [function] `public BlueprintResearchProgressEvent(BlueprintResearchWorkResult result)`
- L172 [function] `public static void Trigger(BlueprintResearchWorkResult result)`
- L179 [type] `public struct BlueprintResearchCompletedEvent`
- L184 [function] `public BlueprintResearchCompletedEvent(FacilityBlueprintSO blueprint, BlueprintResearchUnlockResult unlockResult)`
- L192 [function] `public static void Trigger(FacilityBlueprintSO blueprint, BlueprintResearchUnlockResult unlockResult)`
- L200 [type] `public static class BlueprintResearchService`
- L204 [function] `public static float CalculateResearchWork(CharacterActor researcher, BuildableObject researchFacility, float seconds)`
- L213 [function] `public static float GetFacilityResearchMultiplier(BuildableObject researchFacility)`
- L302 [type] `public class BlueprintResearchRuntime : MonoBehaviour, UtilEventListener<FacilityShopPurchasedEvent>`
- L325 [function] `public bool EnqueueBlueprint(FacilityBlueprintSO blueprint)`
- L341 [function] `public BlueprintResearchWorkResult ApplyResearchWork(CharacterActor researcher, BuildableObject researchFacility, float seconds)`
- L374 [function] `public void OnTriggerEvent(FacilityShopPurchasedEvent eventType)`
- L384 [function] `private void CompleteTask(FacilityBlueprintSO blueprint)`
- L403 [function] `private static string FormatUnlockResult(BlueprintResearchUnlockResult result)`
- L417 [function] `private static void AddLines(List<string> lines, string title, IReadOnlyList<string> values)`
- L427 [function] `private IFacilityShopUnlockStateService ResolveShopUnlockStateService()`
- L433 [function] `private IFacilityShopCatalog ResolveFacilityShopCatalog()`
- L439 [function] `private void OnEnable()`
- L444 [function] `private void OnDisable()`

### Assets\Scripts\Research\ResearchCraftingSummaryQuery.cs

- Lines: 82
- Flags: None

- L3 [type] `public readonly struct ResearchCraftingSummary`
- L32 [type] `public interface IResearchCraftingSummaryService`
- L37 [type] `public interface IResearchCraftingSummaryRuntimeSource`
- L43 [type] `public sealed class ResearchCraftingSummaryRuntimeSource : IResearchCraftingSummaryRuntimeSource`
- L47 [function] `public ResearchCraftingSummaryRuntimeSource(IDungeonSceneComponentQuery sceneQuery)`
- L56 [type] `public sealed class ResearchCraftingSummaryService : IResearchCraftingSummaryService`
- L60 [function] `public ResearchCraftingSummaryService(IResearchCraftingSummaryRuntimeSource runtimeSource)`
- L65 [function] `public ResearchCraftingSummary Capture()`

### Assets\Scripts\Rooms\RoomDetector.cs

- Lines: 380
- Flags: None

- L5 [type] `public static class RoomDetector`
- L19 [function] `public static RoomLayout Build(Grid grid)`
- L63 [function] `internal static bool IsDoor(BuildableObject building)`
- L80 [function] `internal static bool IsWall(BuildableObject building)`
- L87 [function] `private static bool IsInteriorCell(Grid grid, RoomOccupancySnapshot snapshot, Vector2Int position)`
- L95 [function] `private static List<Vector2Int> CollectConnectedInteriorCells(Grid grid, RoomOccupancySnapshot snapshot, Vector2Int start, HashSet<Vector2Int> visited)`
- L127 [function] `private static RoomBoundaryInfo AnalyzeBoundary(Grid grid, RoomOccupancySnapshot snapshot, IReadOnlyList<Vector2Int> cells)`
- L185 [function] `private static List<BuildableObject> CollectFurniture(RoomOccupancySnapshot snapshot, IReadOnlyList<Vector2Int> cells)`
- L211 [function] `private static void AddSelfContainedFacilityRooms(Grid grid, List<RoomInstance> rooms, ref int nextId)`
- L243 [function] `private static bool IsSelfContainedFacilityRoom(BuildableObject building)`
- L254 [function] `private static List<Vector2Int> GetValidBuildCells(Grid grid, BuildableObject building)`
- L274 [type] `private sealed class RoomBoundaryInfo`
- L284 [function] `public void AddDoor(BuildableObject door)`
- L292 [function] `public void AddWall(BuildableObject wall)`
- L301 [type] `private sealed class RoomOccupancySnapshot`
- L310 [function] `public static RoomOccupancySnapshot Build(Grid grid)`
- L353 [function] `public bool HasDoor(Vector2Int cell)`
- L358 [function] `public bool HasWall(Vector2Int cell)`
- L363 [function] `public bool TryGetDoor(Vector2Int cell, out BuildableObject door)`
- L368 [function] `public bool TryGetWall(Vector2Int cell, out BuildableObject wall)`
- L373 [function] `public IReadOnlyList<BuildableObject> GetBuildings(Vector2Int cell)`

### Assets\Scripts\Rooms\RoomFacilityPolicy.cs

- Lines: 81
- Flags: None

- L4 [type] `public interface IRoomFacilityPolicy`
- L14 [type] `public sealed class RoomFacilityPolicyService : IRoomFacilityPolicy`
- L18 [function] `public RoomFacilityPolicyService(IRoomLayoutCache roomLayoutCache)`
- L67 [function] `public float GetRoomUtilityScore(BuildableObject building, FacilityRole role)`

### Assets\Scripts\Rooms\RoomInstance.cs

- Lines: 130
- Flags: None

- L4 [type] `public sealed class RoomInstance`
- L9 [function] `public RoomInstance(int id, IReadOnlyList<Vector2Int> cells, IReadOnlyList<BuildableObject> furniture, IReadOnlyList<BuildableObject> doors, IReadOnlyList<BuildableObject> walls, int solidBoundaryCount, int openBoundaryCount, bool selfContained = false)`
- L59 [function] `public bool ContainsCell(Vector2Int position)`
- L64 [function] `public bool ContainsPart(BuildableObject part)`
- L69 [function] `public bool SupportsFacilityRole(FacilityRole role)`
- L76 [function] `public float GetQualityScore()`
- L89 [function] `private static FacilityRole BuildFacilityRoles(IReadOnlyList<BuildableObject> furniture)`
- L108 [function] `private static RectInt CalculateBounds(IReadOnlyList<Vector2Int> cells)`

### Assets\Scripts\Rooms\RoomLayout.cs

- Lines: 49
- Flags: None

- L4 [type] `public sealed class RoomLayout`
- L9 [function] `public RoomLayout(IReadOnlyList<RoomInstance> rooms)`
- L39 [function] `public bool TryGetRoom(Vector2Int cell, out RoomInstance room)`
- L44 [function] `public bool TryGetRoom(BuildableObject part, out RoomInstance room)`

### Assets\Scripts\Rooms\RoomRegistry.cs

- Lines: 59
- Flags: None

- L4 [type] `public interface IRoomLayoutCache`
- L11 [type] `public sealed class RoomLayoutCache : IRoomLayoutCache`
- L13 [type] `private sealed class CachedLayout`
- L22 [function] `public RoomLayout GetLayout(Grid grid)`
- L44 [function] `public bool TryGetRoom(BuildableObject part, out RoomInstance room)`
- L55 [function] `public void Clear()`

### Assets\Scripts\Rooms\RoomRole.cs

- Lines: 49
- Flags: None

- L4 [type] `public enum RoomRole`
- L18 [type] `public static class RoomRoleUtility`
- L20 [function] `public static RoomRole FromFacilityRoles(FacilityRole roles)`
- L35 [function] `public static FacilityRole ToFacilityRoles(RoomRole roles)`

### Assets\Scripts\Run\RunStartVariableSelector.cs

- Lines: 102
- Flags: None

- L5 [type] `public interface IRunStartVariableSelector`
- L13 [type] `public sealed class RunStartVariableSelector : IRunStartVariableSelector`
- L76 [function] `private static string ResolveLayoutId(CharacterSO ownerData, System.Random startRandom)`

### Assets\Scripts\Run\RunVariableCatalog.cs

- Lines: 168
- Flags: None

- L4 [type] `public static class RunVariableCatalog`
- L10 [function] `public static RunVariableDefinition Get(RunVariableId id)`
- L15 [function] `public static IReadOnlyList<RunVariableDefinition> GetByCategory(RunVariableCategory category)`
- L22 [function] `private static Dictionary<RunVariableId, RunVariableDefinition> BuildDefinitions()`

### Assets\Scripts\Run\RunVariableEffects.cs

- Lines: 120
- Flags: None

- L5 [type] `public static class RunVariableEffects`
- L7 [function] `public static float GetGuestDemandMultiplier(RunVariableState state, string speciesTag)`
- L21 [function] `public static float GetStockCostMultiplier(RunVariableState state, StockCategory category)`
- L35 [function] `public static float GetFacilityShopCostMultiplier(RunVariableState state, BuildingSO building)`
- L56 [function] `public static float GetBlueprintCostMultiplier(RunVariableState state, FacilityBlueprintSO blueprint)`
- L68 [function] `public static float GetThreatRiseMultiplier(RunVariableState state)`
- L84 [function] `public static float GetWarningThresholdMultiplier(RunVariableState state)`
- L96 [function] `public static InvasionIntruderSettings ApplyInvasionSettings(RunVariableState state, InvasionIntruderSettings source)`

### Assets\Scripts\Run\RunVariableEvents.cs

- Lines: 71
- Flags: EventBus

- L1 [type] `public struct RunStartVariablesSelectedEvent`
- L5 [function] `public RunStartVariablesSelectedEvent(RunStartVariableSnapshot snapshot)`
- L12 [function] `public static void Trigger(RunStartVariableSnapshot snapshot)`
- L19 [type] `public struct RunVariableActivatedEvent`
- L23 [function] `public RunVariableActivatedEvent(ActiveRunVariable activeVariable)`
- L30 [function] `public static void Trigger(ActiveRunVariable activeVariable)`
- L37 [type] `public struct RunVariableExpiredEvent`
- L41 [function] `public RunVariableExpiredEvent(RunVariableDefinition definition)`
- L48 [function] `public static void Trigger(RunVariableDefinition definition)`
- L55 [type] `public struct InvasionVariableSelectedEvent`
- L59 [function] `public InvasionVariableSelectedEvent(RunVariableDefinition definition)`
- L66 [function] `public static void Trigger(RunVariableDefinition definition)`

### Assets\Scripts\Run\RunVariableModel.cs

- Lines: 181
- Flags: None

- L6 [type] `public enum RunVariableCategory`
- L13 [type] `public enum RunVariableId`
- L32 [type] `public sealed class RunStartVariableSnapshot`
- L44 [function] `public string ToSummaryText()`
- L58 [function] `private static string FormatIds(IEnumerable<int> values)`
- L64 [function] `private static string FormatStrings(IEnumerable<string> values)`
- L72 [function] `private static string TextOrDefault(string value, string defaultValue)`
- L78 [type] `public sealed class RunVariableDefinition`
- L101 [function] `public string ToDetailText()`
- L107 [type] `public sealed class ActiveRunVariable`
- L109 [function] `public ActiveRunVariable(RunVariableDefinition definition, int startDay)`
- L121 [function] `public void AdvanceDay()`
- L127 [type] `public sealed class RunVariableState`
- L136 [function] `public void SetStartVariables(RunStartVariableSnapshot snapshot)`
- L141 [function] `public ActiveRunVariable ActivateOperationVariable(RunVariableDefinition definition, int day)`
- L154 [function] `public IReadOnlyList<ActiveRunVariable> AdvanceOperationVariables()`
- L170 [function] `public void SetInvasionVariable(RunVariableDefinition definition)`
- L177 [function] `public void ClearInvasionVariable()`

### Assets\Scripts\Run\RunVariableSystem.cs

- Lines: 272
- Flags: EventBus, DependencyInjection

- L6 [type] `public class RunVariableRuntime :`
- L39 [function] `private void Awake()`
- L49 [function] `public void StartRun(int seed, CharacterSO ownerData = null, InvasionThreatDifficulty difficulty = InvasionThreatDifficulty.Normal)`
- L67 [function] `public void OnTriggerEvent(OperatingDayStartedEvent eventType)`
- L78 [function] `public void OnTriggerEvent(OperatingDayEndedEvent eventType)`
- L87 [function] `public void OnTriggerEvent(InvasionCandidateEvent eventType)`
- L93 [function] `public void OnTriggerEvent(InvasionResolvedEvent eventType)`
- L98 [function] `public ActiveRunVariable ActivateOperationVariable(RunVariableId id, int day = -1, bool alert = true)`
- L120 [function] `public RunVariableDefinition SelectInvasionVariable(RunVariableId id, bool alert = true)`
- L142 [function] `public float GetGuestDemandMultiplier(string speciesTag)`
- L147 [function] `public float GetStockCostMultiplier(StockCategory category)`
- L152 [function] `public float GetFacilityShopCostMultiplier(BuildingSO building)`
- L157 [function] `public float GetBlueprintCostMultiplier(FacilityBlueprintSO blueprint)`
- L162 [function] `public float GetThreatRiseMultiplier()`
- L167 [function] `public float GetWarningThresholdMultiplier()`
- L172 [function] `public InvasionIntruderSettings ApplyInvasionSettings(InvasionIntruderSettings source)`
- L177 [function] `private void RollOperationVariable(int day)`
- L189 [function] `private void SelectRandomInvasionVariable()`
- L202 [function] `private void EnsureRunStarted()`
- L214 [function] `private void EnsureRandom()`
- L222 [function] `private InvasionThreatDifficulty ResolveDifficulty()`
- L233 [function] `private CharacterSO ResolveSelectedOwnerData()`
- L238 [function] `private IOwnerRunDataProvider ResolveOwnerRunDataProvider()`
- L244 [function] `private IInvasionThreatRuntimeProvider ResolveInvasionThreatRuntimeProvider()`
- L250 [function] `private IRunStartVariableSelector ResolveRunStartVariableSelector()`
- L256 [function] `private void OnEnable()`
- L264 [function] `private void OnDisable()`

### Assets\Scripts\Synthesis\FacilitySynthesisRecipeSO.cs

- Lines: 30
- Flags: None

- L6 [type] `public class FacilitySynthesisRecipeSO : DataScriptableObject`

### Assets\Scripts\Synthesis\FacilitySynthesisSystem.cs

- Lines: 466
- Flags: EventBus, DependencyInjection

- L8 [type] `public class FacilitySynthesisRecipeSnapshot`
- L17 [function] `public string ToSummaryText()`
- L27 [type] `public readonly struct FacilitySynthesisResult`
- L50 [type] `public struct FacilitySynthesisCompletedEvent`
- L54 [function] `public FacilitySynthesisCompletedEvent(FacilitySynthesisResult result)`
- L61 [function] `public static void Trigger(FacilitySynthesisResult result)`
- L68 [type] `public struct FacilitySynthesisSelectionChangedEvent`
- L72 [function] `public FacilitySynthesisSelectionChangedEvent(IReadOnlyList<BuildableObject> selectedMaterials)`
- L79 [function] `public static void Trigger(IReadOnlyList<BuildableObject> selectedMaterials)`
- L86 [type] `public static class FacilitySynthesisService`
- L140 [function] `public static bool MatchesMaterials(FacilitySynthesisRecipeSO recipe, IReadOnlyList<BuildableObject> materials)`
- L172 [type] `public class FacilitySynthesisRuntime : MonoBehaviour`
- L217 [function] `public void ToggleMaterialSelection(BuildableObject building)`
- L236 [function] `public void ClearSelection()`
- L242 [function] `public bool TrySynthesizeSelected(FacilitySynthesisRecipeSO recipe, out FacilitySynthesisResult result)`
- L253 [function] `public bool TrySynthesizeSelected(string recipeId, out FacilitySynthesisResult result)`
- L259 [function] `public FacilitySynthesisRecipeSnapshot ToSnapshot(FacilitySynthesisRecipeSO recipe)`
- L412 [function] `private void RemoveMaterialFromGrid(BuildableObject material)`
- L427 [function] `private IBlueprintResearchStateService ResolveResearchStateService()`
- L433 [function] `private IGridTextureProvider ResolveGridTextureProvider()`
- L439 [function] `private void InjectCreatedBuilding(BuildableObject building)`
- L449 [function] `private IObjectResolver ResolveObjectResolver()`
- L455 [function] `private IGridBuildingObjectFactory ResolveGridBuildingObjectFactory()`
- L461 [function] `private IFacilitySynthesisRecipeQuery ResolveRecipeQuery()`

### Assets\Scripts\Synthesis\UI\FacilitySynthesisPanel.cs

- Lines: 126
- Flags: EventBus, DependencyInjection

- L7 [type] `public class FacilitySynthesisPanel :`
- L20 [function] `public void Construct(IFacilitySynthesisRuntimeProvider runtimeProvider)`
- L26 [function] `public void Bind(FacilitySynthesisRuntime nextRuntime)`
- L32 [function] `internal void BindGeneratedView(TMP_Text summaryText)`
- L39 [function] `public void Refresh()`
- L81 [function] `public void OnTriggerEvent(FacilitySynthesisSelectionChangedEvent eventType)`
- L86 [function] `public void OnTriggerEvent(BlueprintResearchCompletedEvent eventType)`
- L91 [function] `public void OnTriggerEvent(FacilitySynthesisCompletedEvent eventType)`
- L96 [function] `private void ApplyText()`
- L104 [function] `private FacilitySynthesisRuntime ResolveRuntime()`
- L113 [function] `private void OnEnable()`
- L120 [function] `private void OnDisable()`

### Assets\Scripts\UI\BackgroundManager.cs

- Lines: 45
- Flags: None

- L7 [type] `public class BackgroundManager : MonoBehaviour`
- L12 [function] `private void Awake()`

### Assets\Scripts\UI\BuildingSummaryInfo.cs

- Lines: 143
- Flags: EventBus, SceneMutation, DependencyInjection

- L6 [type] `public class BuildingSummaryInfo : UIPopUp, UtilEventListener<InfoFeedEvent>`
- L17 [function] `public void Construct(IUiPopupService popupService, IBuildingSummaryFormatter summaryFormatter, ITmpKoreanFontService tmpKoreanFontService)`
- L30 [function] `private void Start()`
- L37 [function] `public void OnTriggerEvent(InfoFeedEvent eventType)`
- L55 [function] `public override void OnClose()`
- L60 [function] `public void OnEnable()`
- L65 [function] `private void OnDisable()`
- L70 [function] `private IUiPopupService ResolvePopupService()`
- L77 [function] `private IBuildingSummaryFormatter ResolveSummaryFormatter()`
- L84 [function] `private ITmpKoreanFontService RequireTmpKoreanFontService()`
- L91 [function] `private GameObject RequireUiRoot()`
- L97 [function] `private TMP_Text RequireObjectNameText()`
- L103 [function] `private TMP_Text RequireStockText()`
- L110 [type] `public class BuildingInfoTarget : IInfoable`
- L114 [function] `public BuildingInfoTarget(BuildableObject building)`
- L119 [function] `public InfoFeedEvent.Type GetInfoType()`
- L125 [type] `public struct InfoFeedEvent`
- L128 [type] `public enum Type`
- L133 [function] `public InfoFeedEvent(IInfoable infoable)`
- L138 [function] `public static void Trigger(IInfoable infoable)`

### Assets\Scripts\UI\CameraManager.cs

- Lines: 184
- Flags: SceneMutation, DependencyInjection

- L5 [type] `public class CameraManager : MonoBehaviour`
- L21 [function] `public void Construct(IGridSystemProvider gridSystemProvider, IMainCameraProvider mainCameraProvider)`
- L28 [function] `private void Awake()`
- L33 [function] `private void OnEnable()`
- L38 [function] `private void Start()`
- L45 [function] `private void OnDisable()`
- L50 [function] `private void OnDestroy()`
- L55 [function] `private void Update()`
- L85 [function] `public void ClampToCurrentBounds()`
- L95 [function] `public void OnGridExpand()`
- L100 [function] `private void SubscribeToGridExpansionIfInjected()`
- L108 [function] `private void SubscribeToGridExpansion()`
- L121 [function] `private void UnsubscribeFromGridExpansion()`
- L132 [function] `private IGridSystemProvider RequireGridSystemProvider()`
- L138 [function] `private void UpdateViewportSize()`
- L152 [function] `private void GetGridBounds(out Vector2 lowerBound, out Vector2 upperBound)`
- L167 [function] `private static float ClampAxis(float value, float min, float max, float halfViewSize)`
- L179 [function] `private IMainCameraProvider RequireMainCameraProvider()`

### Assets\Scripts\UI\CharacterSummaryRuntimeLogFactory.cs

- Lines: 70
- Flags: RuntimeObjectCreation, SceneMutation, DependencyInjection

- L6 [type] `public interface ICharacterSummaryRuntimeLogFactory`
- L12 [type] `public sealed class CharacterSummaryRuntimeLogFactory : ICharacterSummaryRuntimeLogFactory`
- L19 [function] `public CharacterSummaryRuntimeLogFactory(ITmpKoreanFontService tmpKoreanFontService)`
- L25 [function] `public TMP_Text Ensure(GameObject uiRoot, TMP_Text current)`
- L51 [function] `public void ApplyFonts(Transform root)`
- L61 [function] `private void ApplyStyle(TMP_Text text)`

### Assets\Scripts\UI\CharacterSummeryInfo.cs

- Lines: 228
- Flags: EventBus, SceneMutation, DependencyInjection

- L8 [type] `public class CharacterSummeryInfo : UIPopUp, UtilEventListener<InfoFeedEvent>`
- L37 [function] `private void Start()`
- L44 [function] `public void OnTriggerEvent(InfoFeedEvent eventType)`
- L84 [function] `public override void OnClose()`
- L90 [function] `public void OnStatChange(Dictionary<CharacterCondition, float> stats)`
- L115 [function] `public void OnLogAdded(CharacterLogEntry entry)`
- L120 [function] `public void RefreshLogText()`
- L130 [function] `public static string FormatLogText(CharacterActor character, int maxLines = 8)`
- L135 [function] `public static string FormatLogText(CharacterLog characterLog, int maxLines = 8)`
- L153 [function] `public void OnEnable()`
- L158 [function] `private void OnDisable()`
- L164 [function] `private void UnbindCharacter()`
- L186 [function] `private static string GetDisplayName(GameObject targetObject)`
- L197 [function] `private IUiPopupService ResolvePopupService()`
- L208 [function] `private ICharacterSummaryRuntimeLogFactory ResolveRuntimeLogFactory()`
- L219 [function] `private GameObject RequireUiRoot()`

### Assets\Scripts\UI\DungeonBackdropSpriteTilingFactory.cs

- Lines: 38
- Flags: RuntimeObjectCreation, SceneMutation, DependencyInjection

- L4 [type] `public interface IDungeonBackdropSpriteTilingFactory`
- L6 [function] `SpriteRenderer Duplicate(SpriteRenderer template, float targetMinX)`
- L9 [type] `public sealed class DungeonBackdropSpriteTilingFactory : IDungeonBackdropSpriteTilingFactory`
- L11 [function] `public SpriteRenderer Duplicate(SpriteRenderer template, float targetMinX)`

### Assets\Scripts\UI\DungeonSceneBackdropFitter.cs

- Lines: 255
- Flags: SceneMutation, DependencyInjection

- L7 [type] `public class DungeonSceneBackdropFitter : MonoBehaviour`
- L19 [function] `public void Construct(IGridSystemProvider gridSystemProvider, IDungeonBackdropReferenceProvider backdropReferenceProvider, IDungeonBackdropSpriteTilingFactory spriteTilingFactory)`
- L30 [function] `private void OnEnable()`
- L35 [function] `private void Start()`
- L41 [function] `private void OnDisable()`
- L46 [function] `public void FitToGrid()`
- L62 [function] `private void SubscribeToGridExpansionIfInjected()`
- L70 [function] `private void SubscribeToGridExpansion()`
- L83 [function] `private void UnsubscribeFromGridExpansion()`
- L94 [function] `private IGridSystemProvider RequireGridSystemProvider()`
- L100 [function] `private IDungeonBackdropReferenceProvider RequireBackdropReferenceProvider()`
- L106 [function] `private IDungeonBackdropSpriteTilingFactory RequireSpriteTilingFactory()`
- L112 [function] `private Transform ResolveBackgroundRoot()`
- L119 [function] `private Tilemap ResolveGroundTilemap()`
- L126 [function] `private void ExtendGround(int leftTile, int rightTile)`
- L157 [function] `private void ExtendBackgroundSprites(int leftTile, int rightTile)`
- L182 [function] `private static bool IsTiledBackgroundRenderer(SpriteRenderer renderer)`
- L190 [function] `private void ExtendBackgroundSpriteGroup(List<SpriteRenderer> renderers, int leftTile, int rightTile)`
- L216 [function] `private void FitSolidBackground(int leftTile, int rightTile)`
- L234 [function] `private static float GetMinX(IEnumerable<SpriteRenderer> renderers)`
- L245 [function] `private static float GetMaxX(IEnumerable<SpriteRenderer> renderers)`

### Assets\Scripts\UI\IInfoable.cs

- Lines: 8
- Flags: None

- L5 [type] `public interface IInfoable`
- L7 [function] `public InfoFeedEvent.Type GetInfoType();`

### Assets\Scripts\UI\NoticeFeed.cs

- Lines: 59
- Flags: EventBus, DependencyInjection

- L4 [type] `public class NoticeFeed : MonoBehaviour, UtilEventListener<NoticeFeedEvent>`
- L10 [function] `public void ConstructNoticeFeed(INoticeFeedPresenter presenter)`
- L16 [function] `public virtual void OnTriggerEvent(NoticeFeedEvent e)`
- L21 [function] `private void OnEnable()`
- L25 [function] `private void OnDisable()`
- L30 [function] `private INoticeFeedPresenter RequirePresenter()`
- L37 [type] `public struct NoticeFeedEvent`
- L41 [type] `public enum Grade`
- L47 [function] `public NoticeFeedEvent(string notice, Grade grade)`
- L53 [function] `public static void Trigger(string notice, Grade grade)`

### Assets\Scripts\UI\NoticeFeedItemFactory.cs

- Lines: 94
- Flags: RuntimeObjectCreation, SceneMutation, DependencyInjection

- L6 [type] `public interface INoticeFeedItemFactory`
- L15 [type] `public sealed class NoticeFeedItemFactory : INoticeFeedItemFactory`
- L20 [function] `public NoticeFeedItemFactory(ITmpKoreanFontService tmpKoreanFontService)`
- L26 [function] `public GameObject Create(GameObject prefab)`
- L37 [function] `public bool TryPrepare(GameObject noticeObject, Transform parent, NoticeFeedEvent notice, out TMP_Text text)`
- L61 [function] `public void OnTake(GameObject noticeObject)`
- L69 [function] `public void OnReturn(GameObject noticeObject)`
- L77 [function] `public void DestroyItem(GameObject noticeObject)`
- L85 [function] `private static Color GetColor(NoticeFeedEvent.Grade grade)`

### Assets\Scripts\UI\NoticeFeedPresenter.cs

- Lines: 80
- Flags: RuntimeObjectCreation, SceneMutation, DependencyInjection

- L8 [type] `public interface INoticeFeedPresenter`
- L10 [function] `void Present(GameObject prefab, Transform parent, NoticeFeedEvent notice)`
- L13 [type] `public sealed class NoticeFeedPresenter : INoticeFeedPresenter`
- L24 [function] `public NoticeFeedPresenter(INoticeFeedItemFactory itemFactory)`
- L30 [function] `public void Present(GameObject prefab, Transform parent, NoticeFeedEvent notice)`
- L58 [function] `private IObjectPool<GameObject> GetPool(GameObject prefab)`
- L69 [function] `private IObjectPool<GameObject> CreatePool(GameObject prefab)`

### Assets\Scripts\UI\RuntimePanelFactories.cs

- Lines: 230
- Flags: RuntimeObjectCreation, SceneMutation, DependencyInjection

- L7 [type] `public interface ICodexPanelFactory`
- L12 [type] `public interface IFacilitySynthesisPanelFactory`
- L17 [type] `public interface IFacilityEvolutionPanelFactory`
- L22 [type] `public sealed class CodexPanelFactory : ICodexPanelFactory`
- L27 [function] `public CodexPanelFactory(ITmpKoreanFontService tmpKoreanFontService)`
- L44 [function] `public CodexPanel CreateDefaultPanel(CodexRuntime runtime)`
- L66 [function] `private TMP_Text CreateSummaryText(Transform parent, float fontSize, Vector2 offsetMin, Vector2 offsetMax)`
- L72 [type] `public sealed class FacilitySynthesisPanelFactory : IFacilitySynthesisPanelFactory`
- L77 [function] `public FacilitySynthesisPanelFactory(ITmpKoreanFontService tmpKoreanFontService)`
- L94 [function] `public FacilitySynthesisPanel CreateDefaultPanel(FacilitySynthesisRuntime runtime)`
- L122 [type] `public sealed class FacilityEvolutionPanelFactory : IFacilityEvolutionPanelFactory`
- L127 [function] `public FacilityEvolutionPanelFactory(ITmpKoreanFontService tmpKoreanFontService)`
- L144 [function] `public FacilityEvolutionPanel CreateDefaultPanel(FacilityEvolutionRuntime runtime)`
- L172 [type] `internal static class RuntimePanelFactoryUtility`
- L174 [function] `public static GameObject CreateOverlayCanvas(string name, Vector2 referenceResolution)`

### Assets\Scripts\UI\UITabGeneratedPanelFactory.cs

- Lines: 157
- Flags: RuntimeObjectCreation, SceneMutation, DependencyInjection

- L7 [type] `public readonly struct GeneratedUITabPanel`
- L9 [function] `public GeneratedUITabPanel(UITab tab, TMP_Text bodyText)`
- L19 [type] `public interface IUITabGeneratedPanelFactory`
- L24 [type] `public interface IStaffWorkPriorityPanelFactory`
- L29 [type] `public sealed class UITabGeneratedPanelFactory : IUITabGeneratedPanelFactory`
- L38 [function] `public UITabGeneratedPanelFactory(ITmpKoreanFontService tmpKoreanFontService, IObjectResolver objectResolver, IStaffWorkPriorityPanelFactory staffWorkPriorityPanelFactory)`
- L51 [function] `public GeneratedUITabPanel Create(Transform parent, int id, string panelTitle)`
- L107 [function] `private TMP_Text CreateText(Transform parent, string name)`
- L117 [type] `public sealed class StaffWorkPriorityPanelFactory : IStaffWorkPriorityPanelFactory`
- L121 [function] `public StaffWorkPriorityPanelFactory(IObjectResolver objectResolver)`
- L127 [function] `public StaffWorkPriorityPanel Ensure(GameObject panelObject)`
- L145 [function] `private static void DisablePlaceholderBody(GameObject panelObject)`

### Assets\Scripts\UI\SummeryInfo.cs

- Lines: 8
- Flags: None

- L5 [type] `public abstract class SummeryInfo : UIPopUp`
- L7 [function] `public abstract void ShowInfo(IInfoable infoable);`

### Assets\Scripts\UI\TMPKoreanFont.cs

- Lines: 68
- Flags: None

- L5 [type] `public interface ITmpKoreanFontProvider`
- L10 [type] `public interface ITmpKoreanFontService`
- L17 [type] `public sealed class TmpKoreanFontAssetProvider : ITmpKoreanFontProvider`
- L21 [function] `public TmpKoreanFontAssetProvider(TMP_FontAsset font)`
- L27 [function] `public TMP_FontAsset GetRequiredFont()`
- L33 [type] `public sealed class TmpKoreanFontService : ITmpKoreanFontService`
- L37 [function] `public TmpKoreanFontService(ITmpKoreanFontProvider fontProvider)`
- L43 [function] `public TMP_FontAsset Resolve()`
- L48 [function] `public void Apply(TMP_Text text)`
- L59 [function] `public void ApplyToChildren(Transform root, bool includeInactive = true)`

### Assets\Scripts\UI\TmpKoreanFontSettingsSO.cs

- Lines: 18
- Flags: None

- L6 [type] `public sealed class TmpKoreanFontSettingsSO : ScriptableObject`
- L12 [function] `public TMP_FontAsset GetRequiredFont()`

### Assets\Scripts\UI\UIBase.cs

- Lines: 8
- Flags: None

- L5 [type] `public class UIBase : MonoBehaviour`

### Assets\Scripts\UI\UIManager.cs

- Lines: 85
- Flags: None

- L7 [type] `public class UIManager : SerializedMonoBehaviour`
- L18 [function] `public void Update()`
- L25 [function] `public void CloseAllPopup()`
- L32 [function] `public void OpenPopup(UIPopUp popup)`
- L38 [function] `public void ClosePopupPeek(UIPopUp popup)`
- L43 [function] `public void ClosePopupPeek()`
- L49 [function] `public void UpdateTime()`
- L53 [function] `private void UpdateHoldingMoneyText(int holdingMoney)`
- L57 [function] `private void UpdateGameSpeedText(int gameSpeed)`
- L61 [function] `public void MakeTouchFalse()`
- L66 [function] `public void MakeTouchTrue()`
- L71 [function] `private void OnEnable()`
- L78 [function] `private void OnDisable()`

### Assets\Scripts\UI\UIPopUp.cs

- Lines: 19
- Flags: None

- L5 [type] `public class UIPopUp : UIBase`
- L7 [function] `public virtual void OnOpen()`
- L11 [function] `public virtual void OnClose()`
- L15 [function] `public virtual void ClosePopup()`

### Assets\Scripts\UI\UiPopupService.cs

- Lines: 71
- Flags: None

- L3 [type] `public interface IUiPopupService`
- L10 [type] `public interface IUiTouchGuardService`
- L16 [type] `public sealed class UiPopupService : IUiPopupService`
- L20 [function] `public UiPopupService(DungeonSceneRuntimeReferences sceneReferences)`
- L25 [function] `public void CloseAll()`
- L30 [function] `public void Open(UIPopUp popup)`
- L35 [function] `public void ClosePeek(UIPopUp popup)`
- L40 [function] `private UIManager ResolveManager()`
- L47 [type] `public sealed class UiTouchGuardService : IUiTouchGuardService`
- L51 [function] `public UiTouchGuardService(DungeonSceneRuntimeReferences sceneReferences)`
- L56 [function] `public void BlockTouch()`
- L61 [function] `public void ReleaseTouch()`
- L66 [function] `private UIManager ResolveManager()`

### Assets\Scripts\UI\UITab.cs

- Lines: 63
- Flags: SceneMutation, DependencyInjection

- L5 [type] `public class UITab : UIPopUp`
- L11 [function] `public void Construct(IUiPopupService popupService)`
- L17 [function] `public bool ToggleTab(int id)`
- L29 [function] `public bool Toggle()`
- L43 [function] `public override void OnClose()`
- L47 [function] `public void CloseTab()`
- L53 [function] `private IUiPopupService ResolvePopupService()`

### Assets\Scripts\UI\UITabContentTextProvider.cs

- Lines: 277
- Flags: None

- L5 [type] `public interface IUITabContentTextProvider`
- L10 [type] `public sealed class UITabContentTextProvider : IUITabContentTextProvider`
- L45 [function] `public string Build(int id)`
- L62 [function] `private string BuildBuildingManagementText()`
- L80 [function] `private string BuildStaffManagementText()`
- L115 [function] `private string BuildShopText()`
- L133 [function] `private string BuildWarehouseText()`
- L156 [function] `private string BuildOperationText()`
- L183 [function] `private string BuildInvasionDefenseText()`
- L212 [function] `private string BuildOffenseText()`
- L240 [function] `private string BuildResearchCraftingText()`
- L260 [function] `private string BuildCodexRecordText()`

### Assets\Scripts\UI\UITabManager.cs

- Lines: 557
- Flags: SceneMutation, DependencyInjection

- L9 [type] `public class UITabManager : MonoBehaviour`
- L67 [function] `private void Start()`
- L73 [function] `public void Construct(IUITabContentTextProvider contentTextProvider, IUiPopupService popupService, ITmpKoreanFontService tmpKoreanFontService, IUITabGeneratedPanelFactory generatedPanelFactory, IStaffWorkPriorityPanelFactory staffWorkPriorityPanelFactory, IUITabTopButtonFactory topButtonFactory)`
- L95 [function] `public void ToggleSelectButton(int category)`
- L118 [function] `public void ResgisterTab(UITab tab)`
- L123 [function] `public void UnRegisterTab(UITab tab)`
- L129 [function] `private void ConfigureTopTabs()`
- L164 [function] `private void EnsureHudCanvasRendersAboveWorld()`
- L177 [function] `private void RegisterExistingTabs()`
- L193 [function] `private void EnsureSpecializedTabContent()`
- L201 [function] `private void EnsureSpecializedTabContent(int id)`
- L214 [function] `private void EnsureSpecializedTabContent(UITab tab)`
- L228 [function] `private IEnumerable<UITab> GetAllTabs()`
- L237 [function] `private void CloseAllTabsImmediate()`
- L250 [function] `private void CloseAllTabsForInitialState()`
- L258 [function] `private static string GetPanelTitle(int id, string defaultTitle)`
- L271 [function] `private void OpenTabImmediate(UITab tab)`
- L279 [function] `private void BindTopButtons()`
- L305 [function] `private Transform ResolveButtonPanel()`
- L324 [function] `private static string GetButtonLabel(Button button)`
- L330 [function] `private static bool TryGetTopTabId(string label, out int tabId)`
- L336 [function] `private static string NormalizeTopTabLabel(string label)`
- L343 [function] `private void EnsureTopButtons()`
- L384 [function] `private void ArrangeTopButtonsInSingleRow(Transform root)`
- L394 [function] `private static void OrderTopButtons(Transform root)`
- L411 [function] `private static Button FindTopButtonForId(Transform root, int id)`
- L424 [function] `private void NormalizeTopButtons(Transform root)`
- L456 [function] `private void SetButtonLabel(Button button, string title)`
- L465 [function] `private void PolishTopButton(Button button)`
- L478 [function] `private void EnsureGeneratedTab(int id, string title)`
- L492 [function] `private void RefreshGeneratedTab(int id)`
- L508 [function] `private string BuildTabContent(int id)`
- L519 [function] `private IUiPopupService ResolvePopupService()`
- L530 [function] `private ITmpKoreanFontService RequireTmpKoreanFontService()`
- L537 [function] `private IUITabGeneratedPanelFactory RequireGeneratedPanelFactory()`
- L544 [function] `private IStaffWorkPriorityPanelFactory RequireStaffWorkPriorityPanelFactory()`
- L551 [function] `private IUITabTopButtonFactory RequireTopButtonFactory()`

### Assets\Scripts\UI\UITabTopButtonFactory.cs

- Lines: 124
- Flags: RuntimeObjectCreation, SceneMutation, DependencyInjection

- L6 [type] `public interface IUITabTopButtonFactory`
- L8 [function] `Button CreateButton(Button template, Transform parent, int id, string title)`
- L9 [function] `HorizontalLayoutGroup EnsureSingleRowLayout(Transform root, float barHeight, params string[] rowNames)`
- L12 [type] `public sealed class UITabTopButtonFactory : IUITabTopButtonFactory`
- L16 [function] `public UITabTopButtonFactory(ITmpKoreanFontService tmpKoreanFontService)`
- L22 [function] `public Button CreateButton(Button template, Transform parent, int id, string title)`
- L41 [function] `public HorizontalLayoutGroup EnsureSingleRowLayout(Transform root, float barHeight, params string[] rowNames)`
- L85 [function] `private void SetLabel(Button button, string title)`
- L97 [function] `private static void MoveRowChildrenBackToRoot(Transform root, string rowName)`
- L118 [function] `private static string SanitizeName(string title)`

### Assets\Scripts\UI\WorldInfoClickSelector.cs

- Lines: 145
- Flags: SceneMutation

- L4 [type] `public interface IWorldInfoClickSelector`
- L12 [type] `public sealed class WorldInfoClickSelectionService : IWorldInfoClickSelector`
- L27 [function] `public bool TryTriggerCharacterUnderPointer()`
- L43 [function] `public bool TryGetPreferredCharacterUnderPointer(out CharacterActor actor)`
- L48 [function] `public bool TryGetPreferredCharacterAtScreenPosition(Vector3 screenPosition, Camera camera, out CharacterActor actor)`
- L63 [function] `public bool TryGetPreferredCharacter(Collider2D[] hits, out CharacterActor actor)`
- L102 [function] `private bool CanSelectWorldInfo()`
- L107 [function] `private static int GetCharacterClickPriority(CharacterActor actor)`
- L134 [function] `private void TriggerCharacter(CharacterActor actor)`

### Assets\Scripts\Utils\DataScriptableObject.cs

- Lines: 7
- Flags: None

- L4 [type] `public class DataScriptableObject : SerializedScriptableObject`

### Assets\Scripts\Utils\EventObserver.cs

- Lines: 152
- Flags: EventBus

- L7 [type] `public static class EventObserver`
- L11 [function] `static EventObserver()`
- L15 [function] `public static void AddListener<Event>(UtilEventListener<Event> listener) where Event : struct`
- L29 [function] `public static void RemoveListener<Event>(UtilEventListener<Event> listener) where Event : struct`
- L52 [function] `public static void TriggerEvent<Event>(Event newEvent) where Event : struct`
- L62 [function] `private static bool SubscriptionExists(Type type, UtilEventListenerBase receiver)`
- L82 [type] `public interface UtilEventListenerBase { };`
- L83 [type] `public interface UtilEventListener<T> : UtilEventListenerBase`
- L87 [type] `public static class EventRegister`
- L89 [function] `public delegate void Delegate<T>(T eventType);`
- L91 [function] `public static void EventStartListening<EventType>(this UtilEventListener<EventType> caller) where EventType : struct`
- L96 [function] `public static void EventStopListening<EventType>(this UtilEventListener<EventType> caller) where EventType : struct`
- L101 [type] `public class EventListenerWrapper<TOwner, TTarget, TEvent> : UtilEventListener<TEvent>, IDisposable`
- L107 [function] `public EventListenerWrapper(TOwner owner, Action<TTarget> callback)`
- L114 [function] `public void Dispose()`
- L120 [function] `protected virtual TTarget OnEvent(TEvent eventType) => default;`
- L121 [function] `public void OnTriggerEvent(TEvent eventType)`
- L127 [function] `private void RegisterCallbacks(bool b)`
- L139 [type] `public struct GameEvent`
- L142 [function] `public GameEvent(string newName)`
- L147 [function] `public static void Trigger(string newName)`

