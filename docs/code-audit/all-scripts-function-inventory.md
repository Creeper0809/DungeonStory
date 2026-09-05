# Full Script Function Inventory

> Auto-generated audit document. Local functions and complex declarations can be missed; inspect target files directly before refactoring.

## Summary

- Script files: 326
- Extracted declarations: 6339
- Misplaced Runtime Editor API Candidates: None
- Runtime scope check: 277 non-Editor/non-debug-scenario scripts, 0 missing from this inventory (verified 2026-07-05).

> Runtime-only companion: [runtime-scripts-function-inventory.md](runtime-scripts-function-inventory.md) excludes scripts under `Editor` directories and is the goal-aligned inventory for runtime refactoring work.

> Targeted refresh note: `GameManager`, runtime DI composition, invasion runtime split, `OperatingDaySettlementRuntime`, `WorkforceReplanService`, `StaffDiscontentRuntime`, staff-discontent work command path, blueprint research work service path, AI scene-query sections, GameData/earning feedback access, facility shop/offense reward catalogs, run-start/owner candidate catalogs, facility synthesis/evolution recipe query services, codex reference/import recipe services, centralized Resources asset loader DI, AI action asset catalog, CharacterAi scheduling service, run-variable catalog services, run-start variable selector service, invasion intruder data provider, TMP Korean font provider/service, DataScriptableObject source provider, unused scene singleton inheritance, OwnerRunManager singleton inheritance, main camera provider, facility/room cache services, facility-evolution room-profile cache DI, facility-evolution state/cache invalidation DI, facility-evolution record cache dirty DI, facility-evolution service state DI, buildable object cache dirty DI, facility-candidate scorer cache DI, work ability cache dirty DI, shop inherited cache dirty DI, character AI facility lookup brain DI, character AI job giver catalog DI, room facility policy explicit DI, facility/room static cache facade lifecycle removal, facility-evolution state static lifecycle removal, TMP Korean font UI component DI, TMP Korean font runtime factory DI, TMP Korean font runtime static facade removal, character AI decision pipeline service DI, runtime static facade definition removal, invasion intruder creation factory ownership, defense status runtime service DI, owner character factory ownership, character behavior tree runtime configurator DI, character feedback bubble service DI, character feedback bubble view factory DI, character social memory service DI, run result panel factory DI, offense panel factory DI, runtime generated panel factory DI, character dialogue bubble factory DI, and notice feed item factory DI were manually refreshed on 2026-07-05 after the VContainer scene-composition refactor.

> Visual render refresh note: `BuildingSO.GridBuildingPlacement.IsStructuralWall` and `GridTexture.DrawBuilding/DeleteBuilding` were manually refreshed on 2026-07-05 so hallway-layer buildings that use `Wall` category for gameplay/collider semantics are not rendered as structural wall tiles.

> Spawn factory refresh note: `CharacterSpawner` and `CharacterSpawnObjectFactory` were manually refreshed on 2026-07-05 so spawned customer prefab creation, component injection, and pool destruction run through a VContainer-registered factory instead of the spawner policy component.

> Visual root factory refresh note: `OwnerCharacterFactory`, `InvasionIntruderRuntimeFactory`, and `CharacterVisualRootFactory` were manually refreshed on 2026-07-05 so owner/intruder runtime factories share one VContainer-registered Visual child/SpriteRenderer composition boundary.

> Grid building object factory refresh note: `GridBuildingObjectFactory`, `GridBuildingFactory`, `DungeonStoryGridBuildingController`, `FacilitySynthesisRuntime`, and `FacilityEvolutionRuntime` were manually refreshed on 2026-07-05 so building GameObject/collider/Rigidbody/component creation is owned by a VContainer-registered object factory.

> Hallway visibility and ghost preview refresh note: `GridTexture` keeps hallway tiles visible by default for PlayMode debugging while preserving hallway-layer gameplay occupancy, and `GridGhostObject` caps build-preview alpha so placement ghosts do not obscure the map. The older hide-under-buildings synchronization remains opt-in.

## Assets

### Assets/DataManager.cs

- L5 [type] `public class DataManager` [DependencyInjection]
- L9 [function] `public DataManager(IDataScriptableObjectSource source)` [DependencyInjection]
- L20 [function] `private void BuildAllData(IReadOnlyCollection<DataScriptableObject> allScriptableObjects)` [DependencyInjection]
- L48 [function] `public Dictionary<int, T> GetData<T>() where T : DataScriptableObject` [DependencyInjection]


## Assets\Scripts\Buildings

### Assets/Scripts/Buildings/BuildableObject.cs

- L6 [type] `public class BuildableObject : MonoBehaviour, IGridOccupant, IGridMovementOccupant` [DependencyInjection, Reflection, SceneMutation]
- L42 [function] `PruneExpiredVisitReservations()` [Reflection, SceneMutation]
- L51 [function] `PruneExpiredWorkerReservation()` [Reflection, SceneMutation]
- L60 [function] `public virtual void Start()` [DependencyInjection, Reflection, SceneMutation]
- L66 [function] `public void ConstructBuildableObject(IBlueprintResearchWorkService blueprintResearchWorkService, IWorldInfoClickSelector worldInfoClickSelector, IFacilityCandidateCache facilityCandidateCache, IRoomFacilityPolicy roomFacilityPolicy)` [DependencyInjection]
- L78 [function] `public virtual void SetGrid(Grid grid)` [DependencyInjection, Reflection, SceneMutation]
- L83 [function] `public virtual void Initialization(BuildingSO buildingSO, Vector2Int buildPos)` [DependencyInjection, Reflection, SceneMutation]
- L100 [function] `public virtual Vector3 GetMovementWorldPosition(Vector2Int gridPosition)` [Reflection, SceneMutation]
- L116 [function] `public bool TryGetNearestWalkableWorldPosition(Vector3 fromWorld, out Vector3 worldPosition)` [Reflection, SceneMutation]
- L135 [function] `public void DestroySelf()` [Reflection, SceneMutation]
- L142 [function] `Destroy(gameObject)` [Reflection, SceneMutation]
- L146 [function] `DestroyImmediate(gameObject)` [Reflection, SceneMutation]
- L150 [function] `public void SetDamaged(bool value)` [Reflection, SceneMutation]
- L161 [function] `public void SetFacilityLevel(int value)` [Reflection, SceneMutation]
- L173 [function] `public bool SupportsFacilityRole(FacilityRole role)` [Reflection, SceneMutation]
- L178 [function] `public bool SupportsWork(FacilityWorkType workType)` [Reflection, SceneMutation]
- L187 [function] `public bool CanVisit(CharacterActor visitor, out string failureReason)` [DependencyInjection, Reflection, SceneMutation]
- L185 [function] `PruneExpiredVisitReservations()` [Reflection, SceneMutation]
- L229 [function] `public bool TryBeginUse(CharacterActor visitor, out string failureReason)` [Reflection, SceneMutation]
- L236 [function] `ReleaseVisitReservation(visitor)` [Reflection, SceneMutation]
- L243 [function] `public void EndUse(CharacterActor visitor)` [Reflection, SceneMutation]
- L252 [function] `public bool TryReserveVisit(CharacterActor visitor, out string failureReason, float seconds = DefaultAiReservationSeconds)` [Reflection, SceneMutation]
- L271 [function] `public void RefreshVisitReservation(CharacterActor visitor, float seconds = DefaultAiReservationSeconds)` [Reflection, SceneMutation]
- L281 [function] `public void ReleaseVisitReservation(CharacterActor visitor)` [Reflection, SceneMutation]
- L294 [function] `public bool TryReserveWorker(CharacterActor worker, out string failureReason, float seconds = DefaultAiReservationSeconds)` [Reflection, SceneMutation]
- L303 [function] `PruneExpiredWorkerReservation()` [Reflection, SceneMutation]
- L316 [function] `public void RefreshWorkerReservation(CharacterActor worker, float seconds = DefaultAiReservationSeconds)` [Reflection, SceneMutation]
- L318 [function] `PruneExpiredWorkerReservation()` [Reflection, SceneMutation]
- L327 [function] `public bool HasWorkerReservationForOther(CharacterActor worker)` [Reflection, SceneMutation]
- L329 [function] `PruneExpiredWorkerReservation()` [Reflection, SceneMutation]
- L333 [function] `public void ReleaseWorkerReservation(CharacterActor worker)` [Reflection, SceneMutation]
- L345 [function] `public bool CanAssignWork(FacilityWorkType workType, out string failureReason)` [Reflection, SceneMutation]
- L347 [function] `PruneExpiredWorkerReservation()` [Reflection, SceneMutation]
- L376 [function] `private int GetActiveVisitReservationCountExcept(CharacterActor visitor)` [Reflection, SceneMutation]
- L378 [function] `PruneExpiredVisitReservations()` [Reflection, SceneMutation]
- L391 [function] `private void PruneExpiredVisitReservations()` [Reflection, SceneMutation]
- L426 [function] `private void PruneExpiredWorkerReservation()` [Reflection, SceneMutation]
- L479 [function] `public virtual float GetWorkUrgency(FacilityWorkType workType)` [DependencyInjection, Reflection, SceneMutation]
- L513 [function] `public virtual bool isVisitable()` [Reflection, SceneMutation]
- L518 [function] `private void OnMouseDown()` [DependencyInjection, Reflection, SceneMutation]
- L532 [function] `protected void MarkFacilityDynamicStateDirty()` [DependencyInjection]
- L537 [function] `private IFacilityCandidateCache ResolveFacilityCandidateCache()` [DependencyInjection]
- L543 [function] `private IRoomFacilityPolicy ResolveRoomFacilityPolicy()` [DependencyInjection]
- L549 [function] `private IBlueprintResearchWorkService ResolveBlueprintResearchWorkService()` [DependencyInjection]
- L555 [function] `private IWorldInfoClickSelector RequireWorldInfoClickSelector()` [DependencyInjection]

### Assets/Scripts/Buildings/BuildingManagementSummaryQuery.cs

- L6 [type] `public readonly struct BuildingManagementSummary`
- L26 [type] `public readonly struct ShopManagementSummary`
- L28 [function] `public ShopManagementSummary(int totalShops, int stockedShops, int emptyShops)`
- L40 [type] `public readonly struct WarehouseManagementSummary`
- L70 [type] `public interface IBuildingManagementSummaryService` [DependencyInjection]
- L77 [type] `public interface IBuildingManagementSummaryRuntimeSource` [DependencyInjection]
- L84 [type] `public sealed class BuildingManagementSummaryRuntimeSource : IBuildingManagementSummaryRuntimeSource` [DependencyInjection]
- L88 [function] `public BuildingManagementSummaryRuntimeSource(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L98 [type] `public sealed class BuildingManagementSummaryService : IBuildingManagementSummaryService` [DependencyInjection]
- L102 [function] `public BuildingManagementSummaryService(IBuildingManagementSummaryRuntimeSource runtimeSource)` [DependencyInjection]
- L107 [function] `public BuildingManagementSummary CaptureBuildings()` [DependencyInjection]
- L112 [function] `public ShopManagementSummary CaptureShops()` [DependencyInjection]
- L117 [function] `public WarehouseManagementSummary CaptureWarehouses()` [DependencyInjection]
- L127 [function] `private static bool IsValidWarehouse(IWarehouseFacility warehouse)`
- L135 [type] `public static class BuildingManagementSummaryQuery`
- L137 [function] `public static BuildingManagementSummary FromBuildings(IEnumerable<BuildableObject> buildings)`
- L157 [function] `public static ShopManagementSummary FromShops(IEnumerable<Shop> shops)`
- L168 [function] `public static WarehouseManagementSummary FromWarehouses(IEnumerable<IWarehouseFacility> warehouses)`
- L190 [function] `private static bool IsValidWarehouse(IWarehouseFacility warehouse)`

### Assets/Scripts/Buildings/Door.cs

- L3 [type] `public class Door : BuildableObject`
- L5 [function] `public override void Initialization(BuildingSO buildingSO, Vector2Int buildPos)`
- L11 [function] `private void OnTriggerEnter2D(Collider2D collision)`
- L22 [function] `private void OnTriggerExit2D(Collider2D collision)`


## Assets\Scripts\Buildings\Editor

### Assets/Scripts/Buildings/Editor/FacilityDebugScenarios.cs

- L7 [type] `public static class FacilityDebugScenarios` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L10 [function] `public static void RunFromMenu()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L19 [function] `public static bool RunAll(bool logSuccess)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L23 [function] `RunScenario("P1 ??嶺?筌????????, VerifyP1AssetCounts, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L24 [function] `RunScenario("?熬곣뫖?삥납??좂뙴?????썹땟?雅??????, VerifyVisitability, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L25 [function] `RunScenario("??????????썹땟?雅??????, VerifyWorkability, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L26 [function] `RunScenario("???????? ??嶺뚮????, VerifyUnavailableFacilitiesAreExcluded, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L27 [function] `RunScenario("??????影??낟?????繹먮냱??, VerifyStockCategories, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L28 [function] `RunScenario("?꿔꺂??????????????, VerifyWarehouseInventory, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L29 [function] `RunScenario("?꿔꺂?????????????쇨덫????⑤슢????, VerifyWarehouseRestocksShop, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L30 [function] `RunScenario("????ㅳ늾?온?????? ??嶺???, VerifyDailyDeliveryOffers, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L31 [function] `RunScenario("???繹먮겧嫄???꿔꺂?????????????彛??, VerifyPurchaseDeliveryUsesMoneyAndWarehouseCapacity, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L32 [function] `RunScenario("????????彛???????곌숯 ??됰슦????夷?, VerifyPurchaseDeliveryFailureConditions, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L33 [function] `RunScenario("??⑤㈇?뚧납????⑤슢???貫糾?????????⑹죰??, VerifyDefenseRewardStockGrant, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L34 [function] `RunScenario("???? ???꾩룆????癲ル슢캉???????, VerifyInternalProductionSlot, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L54 [function] `private static void RunScenario(string name, System.Func<bool> scenario, List<string> errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L61 [function] `private static bool VerifyP1AssetCounts()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L83 [function] `private static bool VerifyVisitability()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L96 [function] `private static bool VerifyWorkability()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L111 [function] `private static bool VerifyUnavailableFacilitiesAreExcluded()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L117 [function] `ClearShopStock(shop)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L129 [function] `private static bool VerifyStockCategories()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L143 [function] `private static bool VerifyWarehouseInventory()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L156 [function] `private static bool VerifyWarehouseRestocksShop()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L163 [function] `ClearShopStock(shopBuilding)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L174 [function] `private static bool VerifyDailyDeliveryOffers()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L185 [function] `private static bool VerifyPurchaseDeliveryUsesMoneyAndWarehouseCapacity()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L212 [function] `private static bool VerifyPurchaseDeliveryFailureConditions()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L240 [function] `private static bool VerifyDefenseRewardStockGrant()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L262 [function] `private static bool VerifyInternalProductionSlot()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L280 [function] `private static void ClearShopStock(BuildableObject building)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L289 [function] `private static GameData CreateGameData(int holdingMoney)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L297 [type] `private sealed class FacilityScenarioWorld : System.IDisposable` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L302 [function] `public FacilityScenarioWorld()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L308 [function] `new TestHallwayOccupant()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L317 [function] `public BuildableObject Place(string assetName, Vector2Int position)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L333 [function] `PlaceRoomDoorsFor(building)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L350 [function] `PlaceRuntimeDoor(new Vector2Int(maxX + 1, y))` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L353 [function] `private void PlaceRuntimeDoor(Vector2Int position)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L390 [function] `public void Dispose()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L404 [type] `private sealed class TestHallwayOccupant : IGridOccupant` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]


## Assets\Scripts\Buildings

### Assets/Scripts/Buildings/Facility.cs

- L5 [type] `public class Facility : BuildableObject, IInteractable, IWorkableFacility, IWarehouseFacility` [SceneMutation]
- L13 [function] `public override void Initialization(BuildingSO buildingSO, Vector2Int buildPos)` [SceneMutation]
- L24 [function] `public IEnumerator Interact(CharacterActor actor)` [SceneMutation]
- L35 [function] `EndUse(actor)` [SceneMutation]
- L52 [function] `ApplyUseRecovery(actor)` [SceneMutation]
- L54 [function] `EndUse(actor)` [SceneMutation]
- L57 [function] `public bool CanAssignWorker(CharacterActor actor, out string failureReason)` [SceneMutation]
- L59 [function] `PruneInvalidWorker()` [SceneMutation]
- L91 [function] `public IEnumerator AllocateWorker(CharacterActor actor)` [SceneMutation]
- L93 [function] `PruneInvalidWorker()` [SceneMutation]
- L100 [function] `ReleaseWorkerReservation(actor)` [SceneMutation]
- L110 [function] `public void DeallocateWorker(CharacterActor actor)` [SceneMutation]
- L117 [function] `PruneInvalidWorker()` [SceneMutation]
- L129 [function] `private void PruneInvalidWorker()` [SceneMutation]
- L151 [function] `private Vector2 GetRandomUsePosition()` [SceneMutation]
- L165 [function] `private Vector2 GetWorkerPosition()` [SceneMutation]
- L176 [function] `private void ApplyUseRecovery(CharacterActor actor)` [SceneMutation]
- L260 [function] `private string objectNameOrDefault()` [SceneMutation]

### Assets/Scripts/Buildings/FacilityCrimeRiskUtility.cs

- L3 [type] `public readonly struct FacilityCrimeRiskContext`
- L38 [type] `public static class FacilityCrimeRiskUtility`
- L40 [function] `public static float CalculateShopliftingChance(FacilityCrimeRiskContext context)`
- L59 [function] `public static float CalculateOperationalRisk(FacilityCrimeRiskContext context)`
- L88 [function] `private static float GetSupervisionPressure(FacilityCrimeRiskContext context)`
- L99 [function] `private static float GetNeedPressure(FacilityCrimeRiskContext context)`
- L118 [function] `LowStatPressure(hunger, comfortable: 45f, critical: 5f)`
- L119 [function] `LowStatPressure(fun, comfortable: 40f, critical: 0f)`
- L120 [function] `LowStatPressure(sleep, comfortable: 35f, critical: 0f)`
- L121 [function] `LowStatPressure(excretion, comfortable: 55f, critical: 10f)`
- L122 [function] `LowStatPressure(hygiene, comfortable: 45f, critical: 5f) * 0.55f)`
- L126 [function] `private static float GetCrowdPressure(FacilityCrimeRiskContext context)`
- L139 [function] `private static float GetCartValuePressure(FacilityCrimeRiskContext context)`
- L151 [function] `private static float GetFacilityStatePressure(FacilityCrimeRiskContext context)`
- L167 [function] `private static float GetActorIncidentMultiplier(CharacterActor actor)`
- L181 [function] `private static float GetStat(CharacterStats stats, CharacterCondition condition, float defaultValue)`
- L186 [function] `private static float LowStatPressure(float value, float comfortable, float critical)`
- L192 [function] `public static bool ShouldTriggerCrime(float chance, float roll)`

### Assets/Scripts/Buildings/Hallway.cs

- L1 [type] `public class Hallway : BuildableObject`

### Assets/Scripts/Buildings/IInteractable.cs

- L3 [type] `public interface IInteractable`
- L5 [function] `public IEnumerator Interact(CharacterActor actor)`
- L8 [type] `public interface IGridMovementHandler`
- L10 [function] `public IEnumerator Traverse(CharacterActor actor, GridMoveStep step)`
- L13 [type] `public interface IStockedFacility`
- L19 [type] `public interface IWarehouseFacility`
- L25 [type] `public interface IWorkableFacility`
- L27 [function] `public bool CanAssignWorker(CharacterActor actor, out string failureReason)`
- L28 [function] `public IEnumerator AllocateWorker(CharacterActor actor)`
- L29 [function] `public void DeallocateWorker(CharacterActor actor)`

### Assets/Scripts/Buildings/Shop.cs

- L8 [type] `public class Shop : BuildableObject, IInteractable, IStockedFacility, IWorkableFacility` [DependencyInjection, SceneMutation]
- L16 [type] `public enum Type` [DependencyInjection, SceneMutation]
- L52 [function] `public override void Initialization(BuildingSO buildingSO, Vector2Int buildPos)` [DependencyInjection, SceneMutation]
- L63 [function] `public void ConstructShop(IGameDataProvider gameDataProvider, IShopStockCatalog stockCatalog, IFloatingNumberFeedbackService floatingNumberFeedbackService, IWorkforceReplanService workforceReplanService)` [DependencyInjection, SceneMutation]
- L82 [function] `public IEnumerator Interact(CharacterActor actor)` [DependencyInjection, SceneMutation]
- L200 [function] `public static bool CreatesRevenueFor(CharacterActor actor)` [DependencyInjection, SceneMutation]
- L205 [function] `public static bool IsInternalStaffUse(CharacterActor actor)` [DependencyInjection, SceneMutation]
- L210 [function] `public bool CanServeCustomer(CharacterActor actor, out string failureReason)` [DependencyInjection, SceneMutation]
- L232 [function] `public float GetCheckoutCrimeChance(int cartItemCount)` [DependencyInjection, SceneMutation]
- L237 [function] `public float GetCheckoutCrimeChance(CharacterActor actor, int cartItemCount, int cartValue)` [DependencyInjection, SceneMutation]
- L251 [function] `private bool TryResolveCheckoutCrime(CharacterActor actor, IReadOnlyList<RemainStock> cart)` [DependencyInjection, SceneMutation]
- L285 [function] `private string BuildCrimeDetail(CharacterActor actor, RemainStock stolenStock, int lossValue, float chance)` [DependencyInjection, SceneMutation]
- L294 [function] `private static int GetCartValue(IReadOnlyList<RemainStock> cart)` [DependencyInjection, SceneMutation]
- L313 [function] `private IEnumerator RunCheckoutService(CharacterActor actor)` [DependencyInjection, SceneMutation]
- L344 [function] `private IEnumerator WaitForServingWorker(CharacterActor actor)` [DependencyInjection, SceneMutation]
- L376 [function] `private bool ShouldWaitForServingWorker(CharacterActor actor)` [DependencyInjection, SceneMutation]
- L385 [function] `public List<Stock> GetStock()` [DependencyInjection, SceneMutation]
- L390 [function] `private List<Stock> GetStock(IReadOnlyDictionary<int, int> selectedCounts)` [DependencyInjection, SceneMutation]
- L403 [function] `private Stock CreatePricedStock(RemainStock stock)` [DependencyInjection, SceneMutation]
- L413 [function] `private float GetPriceMultiplier()` [DependencyInjection, SceneMutation]
- L419 [function] `private static int GetRemainingStockAfterSelection(` [DependencyInjection, SceneMutation]
- L432 [function] `public int GetStockCount()` [DependencyInjection, SceneMutation]
- L446 [function] `public int RestockFrom(IEnumerable<IWarehouseFacility> warehouses, int maxAmount, out string resultMessage)` [DependencyInjection, SceneMutation]
- L508 [function] `public bool TryFindRestockSource(` [DependencyInjection, SceneMutation]
- L570 [function] `public int ReceiveRestock(SaleItem saleItem, int amount, int requestedAmount, out string resultMessage)` [DependencyInjection, SceneMutation]
- L593 [function] `public bool HasRestockSupply(IEnumerable<IWarehouseFacility> warehouses, out string failureReason)` [DependencyInjection, SceneMutation]
- L632 [function] `public void DebugClearStock()` [DependencyInjection, SceneMutation]
- L639 [function] `public Vector2 GetRandomBuyPos()` [DependencyInjection, SceneMutation]
- L646 [function] `private Vector2 GetCheckoutWorldPosition()` [DependencyInjection, SceneMutation]
- L652 [function] `public override float GetWorkUrgency(FacilityWorkType workType)` [DependencyInjection, SceneMutation]
- L666 [function] `public override bool isVisitable()` [DependencyInjection, SceneMutation]
- L671 [function] `public bool CanAssignWorker(CharacterActor actor, out string failureReason)` [DependencyInjection, SceneMutation]
- L705 [function] `public IEnumerator AllocateWorker(CharacterActor actor)` [DependencyInjection, SceneMutation]
- L726 [function] `public void DeallocateWorker(CharacterActor actor)` [DependencyInjection, SceneMutation]
- L746 [function] `private void PruneInvalidWorker()` [DependencyInjection, SceneMutation]
- L770 [function] `private bool RequiresServingWorker()` [DependencyInjection, SceneMutation]
- L777 [function] `private void FillStock()` [DependencyInjection, SceneMutation]
- L799 [function] `private void AddRemainStock(SaleItem saleItem, int amount)` [DependencyInjection, SceneMutation]
- L815 [function] `private RemainStock CreateRemainStock(SaleItem saleItem, int count)` [DependencyInjection, SceneMutation]
- L825 [function] `private StockCategory GetStockCategory(int saleItemId)` [DependencyInjection, SceneMutation]
- L830 [function] `private int GetConfiguredStockCapacity()` [DependencyInjection, SceneMutation]
- L849 [function] `private string objectNameOrDefault()` [DependencyInjection, SceneMutation]
- L856 [function] `private bool TryInitializeStock(bool requireCatalog)` [DependencyInjection, SceneMutation]
- L890 [function] `private void EnsureStockInitialized()` [DependencyInjection, SceneMutation]
- L895 [function] `private bool TryResolveGameData(bool requireProvider)` [DependencyInjection, SceneMutation]
- L915 [function] `private IShopStockCatalog RequireStockCatalog()` [DependencyInjection, SceneMutation]
- L921 [function] `private IFloatingNumberFeedbackService RequireFloatingNumberFeedbackService()` [DependencyInjection, SceneMutation]
- L927 [function] `private IWorkforceReplanService RequireWorkforceReplanService()` [DependencyInjection, SceneMutation]
- L933 [type] `public class RemainStock` [DependencyInjection, SceneMutation]
- L940 [function] `public RemainStock(int id,string itemName, int cost, int stock, OnBuyItemSO[] onbuy)` [DependencyInjection, SceneMutation]
- L949 [type] `public struct Stock` [DependencyInjection, SceneMutation]
- L954 [function] `public Stock(int id, int cost) : this()` [DependencyInjection, SceneMutation]

## Assets\Scripts\Buildings\SO

### Assets/Scripts/Buildings/SO/BuildingSO.cs

- L7 [type] `public enum BuildingCategory`
- L20 [type] `public enum FacilityRole`
- L35 [type] `public enum FacilityWorkType`
- L49 [type] `public class FacilityData`
- L64 [field] `public bool selfContainedRoom`
- L76 [function] `public bool SupportsRole(FacilityRole role)`
- L81 [function] `public bool SupportsWork(FacilityWorkType workType)`
- L87 [type] `public readonly struct GridBuildingPlacement`
- L98 [property] `public bool IsStructuralWall => Category == BuildingCategory.Wall && Layer != GridLayer.Hallway`
- L118 [function] `public List<Vector2Int> GetGridPosList(Vector2Int center)`
- L136 [type] `public class BuildingSO : DataScriptableObject`
- L175 [property] `public bool IsStructuralWall => Placement.IsStructuralWall`
- L185 [function] `public List<Vector2Int> GetGridPosList(Vector2Int center)`
- L190 [function] `public bool GetDraggable()`


## Assets\Scripts\Buildings\SO\Conditions

### Assets/Scripts/Buildings/SO/Conditions/ConditionNeedMoney.cs

- L4 [type] `public class ConditionNeedMoney : IBuildingCondition` [DependencyInjection]
- L8 [function] `public void OnBuild(BuildingConditionContext context)` [DependencyInjection]
- L16 [function] `public bool IsSatisfy(Grid grid, List<Vector2Int> buildPos, BuildingConditionContext context, out string errorMessage)` [DependencyInjection]

### Assets/Scripts/Buildings/SO/Conditions/ConditionNeedToConnect.cs

- L6 [type] `public class ConditionNeedToConnect : IBuildingCondition`
- L13 [function] `public void OnBuild(BuildingConditionContext context)`
- L15 [function] `public bool IsSatisfy(Grid grid, List<Vector2Int> buildPos, BuildingConditionContext context, out string errorMessage)`

### Assets/Scripts/Buildings/SO/Conditions/IBuildingCondition.cs

- L4 [type] `public interface IBuildingCondition`
- L6 [function] `public bool IsSatisfy(Grid grid, List<Vector2Int> buildPos, BuildingConditionContext context, out string errorMessage)`
- L12 [function] `public void OnBuild(BuildingConditionContext context)`
- L15 [type] `public readonly struct BuildingConditionContext`
- L19 [function] `public BuildingConditionContext(GameData gameData)`
- L24 [function] `public GameData GameData { get; }`

## Assets\Scripts\Buildings\SO

### Assets/Scripts/Buildings/SO/OnBuyItemSO.cs

- L5 [type] `public class OnBuyItemSO : ScriptableObject`
- L7 [function] `public virtual void Onbuy(CharacterActor actor)`

### Assets/Scripts/Buildings/SO/SaleItem.cs

- L6 [type] `public enum StockCategory`
- L15 [type] `public class SaleItem : DataScriptableObject`

### Assets/Scripts/Buildings/SO/StatChange.cs

- L6 [type] `public class StatChange : OnBuyItemSO`
- L9 [function] `public override void Onbuy(CharacterActor actor)`

### Assets/Scripts/Buildings/SO/StockInfo.cs

- L8 [type] `public class StockInfo : DataScriptableObject` [EventBus]
- L17 [type] `public class WarehouseInventory` [EventBus]
- L27 [function] `public WarehouseInventory()` [EventBus]
- L31 [function] `public WarehouseInventory(int maxCapacity)` [EventBus]
- L36 [function] `public static WarehouseInventory CreateSeeded(int totalStock)` [EventBus]
- L52 [function] `public int GetStock(StockCategory category)` [EventBus]
- L59 [function] `public bool HasStock(StockCategory category)` [EventBus]
- L64 [function] `public bool CanStore(int amount)` [EventBus]
- L69 [function] `public int AddStock(StockCategory category, int amount)` [EventBus]
- L78 [function] `public int Deposit(StockCategory category, int amount)` [EventBus]
- L88 [function] `public int Withdraw(StockCategory category, int amount)` [EventBus]
- L101 [type] `public struct StockDeliveryOffer` [EventBus]
- L108 [function] `public StockDeliveryOffer(StockCategory category, int amount, int cost, string sourceLabel)` [EventBus]
- L120 [type] `public struct StockProductionRule` [EventBus]
- L126 [function] `public StockProductionRule(StockCategory category, int amount, string sourceLabel)` [EventBus]
- L135 [type] `public struct StockSupplyResult` [EventBus]
- L145 [function] `public StockSupplyResult(...)` [EventBus]
- L163 [function] `public string ToSummaryText()` [EventBus]
- L176 [type] `public struct StockSupplyEvent` [EventBus]
- L180 [function] `public StockSupplyEvent(StockSupplyResult result)` [EventBus]
- L187 [function] `public static void Trigger(StockSupplyResult result)` [EventBus]
- L194 [type] `public static class StockSupplyService` [EventBus]
- L196 [function] `public static IReadOnlyList<StockDeliveryOffer> CreateDailyDeliveryOffers(int day, IRunVariableRuntimeReader runVariableReader)` [EventBus]
- L208 [function] `public static IReadOnlyList<StockDeliveryOffer> CreateDailyDeliveryOffers(int day, Func<StockCategory, float> stockCostMultiplier)` [EventBus]
- L220 [function] `StockDeliveryOffer CreateOffer(StockCategory category, int amount, int unitCost, string sourceLabel)` [EventBus]
- L234 [function] `public static bool TryPurchaseDelivery(GameData gameData, IEnumerable<IWarehouseFacility> warehouses, StockDeliveryOffer offer, out StockSupplyResult result)` [EventBus]
- L283 [function] `public static bool GrantReward(IEnumerable<IWarehouseFacility> warehouses, StockCategory category, int amount, string sourceLabel, out StockSupplyResult result)` [EventBus]
- L319 [function] `public static List<StockSupplyResult> RunInternalProduction(IEnumerable<IWarehouseFacility> warehouses, IEnumerable<StockProductionRule> productionRules)` [EventBus]
- L335 [function] `public static int GetRemainingCapacity(IEnumerable<IWarehouseFacility> warehouses)` [EventBus]
- L350 [function] `private static StockDeliveryOffer CreateOffer(StockCategory category, int amount, int unitCost, string sourceLabel, Func<StockCategory, float> stockCostMultiplier)` [EventBus]
- L363 [function] `private static bool CanDepositAll(IEnumerable<IWarehouseFacility> warehouses, int amount)` [EventBus]
- L369 [function] `private static int DepositToWarehouses(IEnumerable<IWarehouseFacility> warehouses, StockCategory category, int amount)` [EventBus]
- L386 [function] `private static IEnumerable<IWarehouseFacility> GetValidWarehouses(IEnumerable<IWarehouseFacility> warehouses)` [EventBus]
- L393 [function] `private static StockSupplyResult Fail(StockCategory category, int requestedAmount, int cost, string sourceLabel, string reason)` [EventBus]

## Assets\Scripts\Buildings

### Assets/Scripts/Buildings/Stair.cs

- L4 [type] `public class Stair : BuildableObject, IInteractable, IGridMovementHandler` [SceneMutation]
- L12 [function] `public IEnumerator Traverse(CharacterActor actor, GridMoveStep step)` [SceneMutation]
- L45 [function] `public IEnumerator Interact(CharacterActor actor)` [SceneMutation]
- L72 [function] `private float GetHiddenTravelDelay()` [SceneMutation]
- L79 [function] `private Vector3 GetFloorCenterAnchor(Vector2Int fallbackPosition)` [SceneMutation]


## Assets\Scripts\Buildings\UI

### Assets/Scripts/Buildings/UI/UIBuildingInfo.cs

- L11 [type] `public class UIBuildingInfo : SerializedMonoBehaviour` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L31 [function] `public void ConstructUIBuildingInfo(` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L41 [function] `private void Awake()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L47 [function] `void Start()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L53 [function] `public void DisplayBuildingInfo(BuildableObject building)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L90 [function] `public void OpenDispaly()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L100 [function] `public void CloseDispaly()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L112 [function] `private IBuildingDefinitionLookup ResolveBuildingLookup()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L118 [function] `private IUiTouchGuardService ResolveTouchGuard()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L125 [type] `public class UIConfig<T>`

### Assets/Scripts/Buildings/UI/UIBuildingSelectButton.cs

- L6 [type] `public class UIBuildingSelectButton : MonoBehaviour` [DependencyInjection, SceneMutation]
- L19 [function] `public void Construct(IDungeonGridBuildingControllerProvider buildingControllerProvider)` [DependencyInjection]
- L25 [function] `public void Initialization(BuildingSO so)` [SceneMutation]
- L34 [function] `public void Initialization(BuildingSO so, IDungeonGridBuildingControllerProvider buildingControllerProvider)` [DependencyInjection, SceneMutation]
- L40 [function] `public void OnClick()` [DependencyInjection, SceneMutation]
- L47 [function] `public void ActiveDestroyMode()` [DependencyInjection, SceneMutation]
- L52 [function] `private void ApplyButtonSize()` [SceneMutation]
- L60 [function] `private void SetIcon(Sprite sprite)` [SceneMutation]
- L79 [function] `private Image ResolveIconImage()`
- L88 [function] `private Vector2 GetFittedIconSize(Sprite sprite)`
- L102 [function] `private DungeonStoryGridBuildingController RequireBuildingController()` [DependencyInjection]


## Assets\Scripts\Character\Ability

### Assets/Scripts/Character/Ability/AbilityMove.cs

- L8 [type] `public class AbilityMove : CharacterAbility` [DependencyInjection, SceneMutation]
- L18 [function] `public void ConstructAbilityMove(ICharacterSpawnerProvider spawnerProvider, ICharacterAiSchedulingService aiSchedulingService)` [DependencyInjection, SceneMutation]
- L29 [function] `protected override void Awake()` [DependencyInjection, SceneMutation]
- L34 [function] `public override void Initializtion(CharacterSO data)` [DependencyInjection, SceneMutation]
- L45 [function] `public IEnumerator MoveByPath(Queue<GridMoveStep> path, AIAction expectedAction = null)` [DependencyInjection, SceneMutation]
- L64 [function] `public IEnumerator MoveByStep(GridMoveStep step, AIAction expectedAction = null)` [DependencyInjection, SceneMutation]
- L89 [function] `public IEnumerator Move2GridPosition(Vector2Int gridPosition, AIAction expectedAction = null)` [DependencyInjection, SceneMutation]
- L98 [function] `public void StartExitDungeon()` [DependencyInjection, SceneMutation]
- L117 [function] `public void StartEnterDungeon(Vector3 entryDoorWorldPosition, Vector2Int entryGridPosition)` [DependencyInjection, SceneMutation]
- L127 [function] `public void StartMoveByCurrentActionPath(float waitDuration = 0f)` [DependencyInjection, SceneMutation]
- L133 [function] `public void StartWait(float duration)` [DependencyInjection, SceneMutation]
- L139 [function] `public bool StartIdleWander(float waitDuration, int minDistance = 2, int maxDistance = 8)` [DependencyInjection, SceneMutation]
- L153 [function] `public void CancelActiveMovement()` [DependencyInjection, SceneMutation]
- L162 [function] `private void StartTrackedActionMovement(IEnumerator routine)` [DependencyInjection, SceneMutation]
- L168 [function] `private IEnumerator TrackActionMovement(IEnumerator routine)` [DependencyInjection, SceneMutation]
- L174 [function] `private AIAction GetCurrentAction()` [DependencyInjection, SceneMutation]
- L181 [function] `private IEnumerator MoveByCurrentActionPath(float waitDuration, AIAction expectedAction)` [DependencyInjection, SceneMutation]
- L201 [function] `private IEnumerator MoveByPathThenWait(Queue<GridMoveStep> path, float waitDuration, AIAction expectedAction)` [DependencyInjection, SceneMutation]
- L221 [function] `public bool TryFindIdleWanderPath(` [DependencyInjection, SceneMutation]
- L255 [function] `private bool IsPlainIdleWalkable(Vector2Int position)` [DependencyInjection, SceneMutation]
- L266 [function] `private static bool IsSupportedIdleWanderPath(Queue<GridMoveStep> path)` [DependencyInjection, SceneMutation]
- L294 [function] `private static bool IsSupportedVerticalMovementStep(GridMoveStep step)` [DependencyInjection, SceneMutation]
- L301 [function] `private GridPathSearchResult GetIdleSearchResult(Vector2Int originalPos)` [DependencyInjection, SceneMutation]
- L318 [function] `private void SnapToGridRowIfWalkable(Vector2Int gridPosition)` [DependencyInjection, SceneMutation]
- L332 [function] `public IEnumerator MoveByCurrentBestActionPath()` [DependencyInjection, SceneMutation]
- L337 [function] `private IEnumerator MoveByActionPath(AIAction action)` [DependencyInjection, SceneMutation]
- L350 [function] `private void RefreshCurrentActionReservation()` [DependencyInjection, SceneMutation]
- L360 [function] `private bool IsActionMovementCancelled(AIAction expectedAction)` [DependencyInjection, SceneMutation]
- L369 [function] `private IEnumerator WaitForAiAction(float duration, AIAction expectedAction)` [DependencyInjection, SceneMutation]
- L384 [function] `private IEnumerator WaitForAiActionDelay(float duration, AIAction expectedAction)` [DependencyInjection, SceneMutation]
- L399 [function] `private IEnumerator EnterDungeon(Vector3 entryDoorWorldPosition, Vector2Int entryGridPosition)` [DependencyInjection, SceneMutation]
- L424 [function] `private IEnumerator ExitDungeon()` [DependencyInjection, SceneMutation]
- L477 [function] `private bool TryResolveSpawner()` [DependencyInjection, SceneMutation]
- L487 [function] `private ICharacterAiSchedulingService RequireAiSchedulingService()` [DependencyInjection]
- L493 [function] `public IEnumerator Move2PosByTime(Vector3 endPos, float duration)` [DependencyInjection, SceneMutation]
- L505 [function] `public IEnumerator Move2PosBySpeed(Vector3 endPos, float multifly = 1.0f, AIAction expectedAction = null)` [DependencyInjection, SceneMutation]
- L555 [function] `private void UpdateFacingForMovement(float deltaX)` [DependencyInjection, SceneMutation]

### Assets/Scripts/Character/Ability/AbilitySchedule.cs

- L5 [type] `public class AbilitySchedule : CharacterAbility`
- L10 [function] `protected override void Awake()`
- L20 [function] `private void CheckTime(int hour)`
- L24 [function] `private void OnDisable()`
- L29 [type] `public enum Schedule`

### Assets/Scripts/Character/Ability/AbilityShopping.cs

- L7 [type] `public class AbilityShopping : CharacterAbility` [DependencyInjection, SceneMutation]
- L23 [function] `public void ConstructAbilityShopping(IShopStockCatalog shopStockCatalog, IFloatingIconFeedbackService floatingIconFeedbackService)` [DependencyInjection]
- L17 [function] `public override void Initializtion(CharacterSO data)` [DependencyInjection, SceneMutation]
- L27 [function] `public Stock DetermineBuyingItem(List<Stock> stocks)` [DependencyInjection, SceneMutation]
- L58 [function] `public bool CanPay(Stock stock)` [DependencyInjection, SceneMutation]
- L64 [function] `public bool CanBuyFrom(Shop shop, out string failureReason)` [DependencyInjection, SceneMutation]
- L105 [function] `public float GetAffordabilityScore(Shop shop)` [DependencyInjection, SceneMutation]
- L132 [function] `public void StartSopping()` [DependencyInjection, SceneMutation]
- L135 [function] `StartCoroutine(Shopping())` [DependencyInjection, SceneMutation]
- L137 [function] `private IEnumerator Shopping()` [DependencyInjection, SceneMutation]
- L154 [function] `CacheCommonReferences()` [DependencyInjection, SceneMutation]
- L172 [function] `RegisterVisit(action.destination)` [DependencyInjection, SceneMutation]
- L185 [function] `public void RegisterVisit(BuildableObject building)` [DependencyInjection, SceneMutation]
- L198 [function] `public void RegisterLookAround()` [DependencyInjection, SceneMutation]
- L203 [function] `public void BeginOffDutyVisitCycle()` [DependencyInjection, SceneMutation]
- L210 [function] `public bool IsOffDutyStaffVisitor()` [DependencyInjection, SceneMutation]
- L219 [function] `public FacilityRole GetInterestRoles()` [DependencyInjection, SceneMutation]
- L224 [function] `public bool CanLookAround()` [DependencyInjection, SceneMutation]
- L231 [function] `public bool ShouldExitDungeon()` [DependencyInjection, SceneMutation]
- L238 [function] `public bool ShouldEndVisitCycle()` [DependencyInjection, SceneMutation]
- L243 [function] `public bool IsThereVisitableBuilding()` [DependencyInjection, SceneMutation]
- L247 [function] `CacheCommonReferences()` [DependencyInjection, SceneMutation]
- L275 [function] `public BuildableObject FindShop()` [DependencyInjection, SceneMutation]
- L319 [function] `private bool CanVisitBuilding(BuildableObject building)` [DependencyInjection, SceneMutation]
- L333 [function] `public int GetShoppingCount()` [DependencyInjection, SceneMutation]
- L354 [function] `public IEnumerator BuyItem(RemainStock item, int purchaseCost)` [DependencyInjection, SceneMutation]
- L375 [function] `private IFloatingIconFeedbackService RequireFloatingIconFeedbackService()` [DependencyInjection]

### Assets/Scripts/Character/Ability/AbilityWork.cs

- L4 [type] `public class AbilityWork : CharacterAbility`
- L6 [type] `public enum DutyState`
- L79 [function] `EnsureWorkModules()`
- L88 [function] `EnsureWorkModules()`
- L97 [function] `EnsureWorkModules()`
- L106 [function] `EnsureWorkModules()`
- L111 [function] `protected override void Awake()`
- L114 [function] `EnsureWorkModules()`
- L115 [function] `TryBindScheduleEvents()`
- L136 [function] `public void ConstructAbilityWork(IBlueprintResearchWorkService blueprintResearchWorkService, IStaffDiscontentRuntimeService staffDiscontentRuntimeService, IFloatingIconFeedbackService floatingIconFeedbackService, IWorkGridResolver workGridResolver, IFacilityCandidateCache facilityCandidateCache)` [DependencyInjection]
- L131 [function] `public override void Initializtion(CharacterSO data)`
- L121 [function] `EnsureWorkModules()`
- L122 [function] `TryBindScheduleEvents()`
- L132 [function] `TryAssignShop()`
- L135 [function] `public void EnsureWorkReferences()`
- L137 [function] `CacheCommonReferences()`
- L140 [function] `public bool TryAssignShop(GridPathSearchResult searchResult = null)`
- L145 [function] `public bool TryAssignWork(FacilityWorkType requestedWorkType, GridPathSearchResult searchResult = null)`
- L158 [function] `AssignWork(null, FacilityWorkType.None)`
- L164 [function] `public float GetWorkUtilityScore(FacilityWorkType requestedWorkType, GridPathSearchResult searchResult)`
- L169 [function] `public bool TryGetLastRejectedWorkCandidate(out WorkTargetCandidate candidate)`
- L185 [function] `StartCoroutine(CommandHandler.SuppressPriorityTarget())`
- L191 [function] `StopAssignedWork(null)`
- L219 [function] `AssignWork(target, candidate.WorkType)`
- L226 [function] `public bool TrySetPriorityWorkTarget(BuildableObject building, out string errorMessage)`
- L248 [function] `public bool TryGetPrioritySuppressDestination(GridPathSearchResult searchResult, out BuildableObject destination)`
- L253 [function] `public void ClearPriorityWorkTarget()`
- L258 [function] `public void SetWorkPriority(FacilityWorkType workType, WorkPriorityLevel priority)`
- L267 [function] `StopAssignedWork($"{WorkTaskCatalog.GetDisplayName(assignedWorkType)} ???????얠???嶺뚮??▼퐲????Β?ル빢傭?)`
- L271 [function] `AssignWork(null, FacilityWorkType.None)`
- L283 [function] `public bool ShouldUseRestProtection()`
- L288 [function] `public bool CanStartWorkAction()`
- L293 [function] `public bool CanStartWorkAction(FacilityWorkType requestedWorkType, GridPathSearchResult searchResult)`
- L299 [function] `public bool CanContinueCurrentWork(out string stopReason)`
- L304 [function] `public bool ShouldInterruptCurrentWork(out string interruptReason)`
- L309 [function] `public bool ShouldThrottleRoutineWork(FacilityWorkType workType)`
- L318 [function] `public void BeginRoutineWorkCooldown(FacilityWorkType workType)`
- L332 [function] `public bool ShouldTakeOffDuty()`
- L337 [function] `public bool ShouldReturnToWork()`
- L342 [function] `public void BeginOffDuty(string reason)`
- L347 [function] `public void PrepareForExpedition()`
- L352 [function] `public void SetDutyState(DutyState nextState)`
- L368 [function] `public void ApplyWorkFatigueTick()`
- L373 [function] `public IEnumerator CheckActionWork()`
- L378 [function] `public void CheckSchedule(Schedule schedule)`
- L382 [function] `StopAssignedWork("???嚥???⑤슢堉???)`
- L386 [function] `internal void AssignWork(BuildableObject building, FacilityWorkType workType)`
- L392 [function] `internal void ReleaseAssignedWorkTarget()`
- L394 [function] `StopAssignedWork(null)`
- L397 [function] `internal void StopAssignedWork(string reason)`
- L399 [function] `StopAssignedWork(reason, true)`
- L402 [function] `internal void StopAssignedWorkFromAi(string reason)`
- L404 [function] `StopAssignedWork(reason, false)`
- L407 [function] `private void StopAssignedWork(string reason, bool requestImmediateReplan)`
- L409 [function] `InvalidateActiveWorkRun()`
- L422 [function] `AssignWork(null, FacilityWorkType.None)`
- L441 [function] `internal bool IsActiveWorkRun(int runId)`
- L446 [function] `internal bool CanContinueWorkRun(int runId)`
- L451 [function] `internal Coroutine StartCheckActionWork(int runId)`
- L458 [function] `StopActiveWorkCheckRoutine()`
- L463 [function] `internal void ClearActiveWorkRoutine(int runId)`
- L471 [function] `internal void ClearActiveWorkCheckRoutine(int runId)`
- L479 [function] `internal bool HasUrgentPriorityTarget()`
- L521 [function] `internal void MarkFacilityDynamicStateDirty()` [DependencyInjection]
- L484 [function] `private bool CanExecuteSuppressCommand(FacilityWorkType requestedWorkType)`
- L490 [function] `private void OnDisable()`
- L492 [function] `UnbindScheduleEvents()`
- L495 [function] `private void OnEnable()`
- L497 [function] `TryBindScheduleEvents()`
- L500 [function] `private void TryBindScheduleEvents()`
- L502 [function] `CacheCommonReferences()`
- L517 [function] `UnbindScheduleEvents()`
- L523 [function] `private void UnbindScheduleEvents()`
- L533 [function] `private void EnsureWorkModules()`
- L549 [function] `private int BeginWorkRun()`
- L551 [function] `StopActiveWorkRoutines()`
- L556 [function] `private void InvalidateActiveWorkRun()`
- L559 [function] `StopActiveWorkRoutines()`
- L562 [function] `private void StopActiveWorkRoutines()`
- L564 [function] `StopActiveWorkRoutine()`
- L565 [function] `StopActiveWorkCheckRoutine()`
- L568 [function] `private void StopActiveWorkRoutine()`
- L575 [function] `StopCoroutine(activeWorkRoutine)`
- L579 [function] `private void StopActiveWorkCheckRoutine()`
- L586 [function] `StopCoroutine(activeWorkCheckRoutine)`
- L590 [function] `private IEnumerator ExecuteRestockWork()`
- L595 [function] `private IEnumerator ExecuteRepairWork()`
- L600 [function] `private IEnumerator ExecuteResearchWork()`
- L605 [function] `private IEnumerator SuppressPriorityTarget()`

### Assets/Scripts/Character/Ability/CharacterAbility.cs

- L7 [type] `public class CharacterAbility : SerializedMonoBehaviour` [DependencyInjection]
- L18 [function] `protected virtual void Awake()` [DependencyInjection]
- L21 [function] `CacheSplitComponents()` [DependencyInjection]
- L24 [function] `protected virtual void Start()` [DependencyInjection]
- L26 [function] `CacheCommonReferences()` [DependencyInjection]
- L29 [function] `protected void CacheCommonReferences()` [DependencyInjection]
- L36 [function] `CacheSplitComponents()` [DependencyInjection]
- L50 [function] `private void CacheSplitComponents()` [DependencyInjection]
- L59 [function] `public virtual void Initializtion(CharacterSO data)` [DependencyInjection]
- L61 [function] `CacheCommonReferences()` [DependencyInjection]

### Assets/Scripts/Character/Ability/CharacterVisitPolicy.cs

- L1 [type] `public static class CharacterVisitPolicy`
- L20 [function] `public static FacilityRole GetInterestRoles(CharacterActor actor)`
- L27 [function] `public static bool IsStaffPurchaseShop(CharacterActor actor, BuildableObject building)`


## Assets\Scripts\Character\AI\Action

### Assets/Scripts/Character/AI/Action/AIActionSet.cs

- L8 [type] `public abstract class AIActionSet : SerializedScriptableObject`
- L20 [function] `public virtual bool CanStart(CharacterActor actor)`
- L25 [function] `public virtual float AdjustScore(CharacterActor actor, float baseScore)`
- L83 [function] `public virtual bool CanContinue(CharacterActor actor, AIAction runningAction, out string stopReason)`
- L89 [function] `public virtual bool CanInterrupt(CharacterActor actor, AIAction runningAction, out string interruptReason)`
- L95 [function] `public virtual void Execute(CharacterActor actor)`
- L99 [function] `public virtual void OnStop(CharacterActor actor, AIAction runningAction, string reason)`
- L193 [function] `public virtual BuildableObject GetDestination(CharacterActor actor)`

### Assets/Scripts/Character/AI/Action/AIEat.cs

- L6 [type] `public class AIEat : AIActionSet`
- L8 [function] `public override bool CanStart(CharacterActor actor)`
- L13 [function] `public override void Execute(CharacterActor actor)`
- L87 [function] `public override void RefreshDestinationReservation(CharacterActor actor, BuildableObject destination)`
- L92 [function] `public override void ReleaseDestinationReservation(CharacterActor actor, BuildableObject destination)`
- L97 [function] `private static bool CanUseVisitorAction(CharacterActor actor)`

### Assets/Scripts/Character/AI/Action/AIExitDungeon.cs

- L4 [type] `public class AIExitDungeon : AIActionSet`
- L8 [function] `public override bool CanStart(CharacterActor actor)`
- L17 [function] `public override void Execute(CharacterActor actor)`

### Assets/Scripts/Character/AI/Action/AIFacilityRoleAction.cs

- L6 [type] `public class AIFacilityRoleAction : AIActionSet`
- L16 [function] `public override bool CanStart(CharacterActor actor)`
- L21 [function] `public override void Execute(CharacterActor actor)`
- L87 [function] `public override void RefreshDestinationReservation(CharacterActor actor, BuildableObject destination)`
- L92 [function] `public override void ReleaseDestinationReservation(CharacterActor actor, BuildableObject destination)`
- L97 [function] `private static bool CanUseVisitorAction(CharacterActor actor)`

### Assets/Scripts/Character/AI/Action/AILookAround.cs

- L7 [type] `public class AILookAround : AIActionSet`
- L14 [function] `public override bool CanStart(CharacterActor actor)`
- L19 [function] `public override void Execute(CharacterActor actor)`
- L83 [function] `public static bool CanUseVisitLookAround(CharacterActor actor)`

### Assets/Scripts/Character/AI/Action/AIRest.cs

- L6 [type] `public class AIRest : AIActionSet`
- L8 [function] `public override bool CanStart(CharacterActor actor)`
- L13 [function] `public override void Execute(CharacterActor actor)`
- L90 [function] `public override void RefreshDestinationReservation(CharacterActor actor, BuildableObject destination)`
- L95 [function] `public override void ReleaseDestinationReservation(CharacterActor actor, BuildableObject destination)`
- L100 [function] `private static bool CanUseVisitorAction(CharacterActor actor)`

### Assets/Scripts/Character/AI/Action/AIShopping.cs

- L6 [type] `public class AIShopping : AIActionSet`
- L8 [function] `public override bool CanStart(CharacterActor actor)`
- L13 [function] `public override void Execute(CharacterActor actor)`
- L108 [function] `public override void RefreshDestinationReservation(CharacterActor actor, BuildableObject destination)`
- L113 [function] `public override void ReleaseDestinationReservation(CharacterActor actor, BuildableObject destination)`
- L118 [function] `private static bool CanUseVisitorAction(CharacterActor actor)`

### Assets/Scripts/Character/AI/Action/AIWait.cs

- L5 [type] `public class AIWait : AIActionSet`
- L14 [function] `public override float AdjustScore(CharacterActor actor, float baseScore)`
- L48 [function] `public override bool CanStart(CharacterActor actor)`
- L53 [function] `public override void Execute(CharacterActor actor)`
- L96 [function] `private static bool HasOffDutyVisitCandidate(CharacterActor actor, GridPathSearchResult searchResult)`

### Assets/Scripts/Character/AI/Action/AIWork.cs

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


## Assets\Scripts\Character\AI

### Assets/Scripts/Character/AI/AIActionFailure.cs

- L4 [type] `public enum AIActionFailureKind`
- L25 [type] `public struct AIActionFailure`
- L27 [function] `public AIActionFailure(AIActionFailureKind kind, string reason, BuildableObject target = null)`
- L127 [function] `public override string ToString()`
- L132 [function] `private static bool ContainsAny(string value, params string[] patterns)`
- L145 [function] `private static string GetDefaultReason(AIActionFailureKind kind)`
- L169 [type] `public readonly struct AIActionDebugCandidate`

### Assets/Scripts/Character/AI/AIBrain.cs

- L9 [type] `public class AIBrain : CharacterAbility` [DependencyInjection]
- L51 [function] `public void ConstructAIBrain(ICharacterAiActionAssetCatalog actionAssetCatalog, ICharacterAiSchedulingService aiSchedulingService, ISocialReputationBiasService socialReputationBiasService, IFacilityCandidateCache facilityCandidateCache, ICharacterAiFacilityLookup facilityLookup, ICharacterAiJobGiverCatalog jobGiverCatalog, ICharacterAiDecisionPipeline decisionPipeline, IRoomFacilityPolicy roomFacilityPolicy)` [DependencyInjection]
- L78 [function] `public override void Initializtion(CharacterSO data)` [DependencyInjection]
- L94 [function] `public void UseOwnerWorkActions()` [DependencyInjection]
- L102 [function] `private void NormalizeConfiguredActions()` [DependencyInjection]
- L125 [function] `private void EnsureVisitorActions()` [DependencyInjection]
- L145 [function] `private void AddRequiredAction<T>(List<AIAction> actions, string resourcePath) where T : AIActionSet` [DependencyInjection]
- L152 [function] `private void AddRequiredFacilityRoleAction(` [DependencyInjection]
- L170 [function] `private ICharacterAiActionAssetCatalog RequireActionCatalog()` [DependencyInjection]
- L176 [function] `private ICharacterAiSchedulingService RequireAiSchedulingService()` [DependencyInjection]
- L182 [function] `public FacilityScoringContext RequireFacilityScoringContext()` [DependencyInjection]
- L193 [function] `public IFacilityCandidateCache RequireFacilityCandidateCache()` [DependencyInjection]
- L199 [function] `public ICharacterAiFacilityLookup RequireFacilityLookup()` [DependencyInjection]
- L205 [function] `public ICharacterAiJobGiverCatalog RequireJobGiverCatalog()` [DependencyInjection]
- L211 [function] `public ICharacterAiDecisionPipeline RequireDecisionPipeline()` [DependencyInjection]
- L217 [function] `public bool DecideAction()` [DependencyInjection]
- L243 [function] `public bool TryCommitActionCandidate(` [DependencyInjection]
- L257 [function] `public bool TryFindBestScoredAction(` [DependencyInjection]
- L334 [function] `private bool DecideActionByScoreThenDestination()` [DependencyInjection]
- L378 [function] `private bool TryFindHighestScoredAction(out AIAction bestCandidate)` [DependencyInjection]
- L402 [function] `private float GetSelectionScore(AIAction action)` [DependencyInjection]
- L427 [function] `public GridPathSearchResult GetPathSearch(CharacterActor actor)` [DependencyInjection]
- L449 [function] `public bool TryGetRuntimeGrid(out Grid resolvedGrid)` [DependencyInjection]
- L454 [function] `public void ClearPathSearchCache()` [DependencyInjection]
- L459 [function] `public void RequestImmediateReplan(bool clearFailures = false)` [DependencyInjection]
- L488 [function] `public void ClearSelectedActionForIdle(string idleLabel)` [DependencyInjection]
- L502 [function] `public void NotifyActionStarted()` [DependencyInjection]
- L514 [function] `public bool ShouldStopCurrentAction(out string stopReason)` [DependencyInjection]
- L535 [function] `public bool ShouldStopCurrentActionForReplan(out string stopReason)` [DependencyInjection]
- L580 [function] `public bool CanContinueCurrentAction(out string status)` [DependencyInjection]
- L630 [function] `public bool StopCurrentActionForReplan(string reason)` [DependencyInjection]
- L662 [function] `private bool TryUseQueuedAction()` [DependencyInjection]
- L686 [function] `private bool TryFindInterruptAction(` [DependencyInjection]
- L748 [function] `private bool CanConsiderAction(AIAction action, out string failureReason)` [DependencyInjection]
- L755 [function] `private bool CanConsiderAction(AIAction action, out AIActionFailure failure)` [DependencyInjection]
- L800 [function] `private bool CanUseAction(AIAction action, out string failureReason)` [DependencyInjection]
- L807 [function] `private bool CanUseAction(AIAction action, out AIActionFailure failure)` [DependencyInjection]
- L825 [function] `private bool IsActionCoolingDown(AIActionSet actionSet)` [DependencyInjection]
- L832 [function] `private void RecordActionFailure(AIActionSet actionSet, string reason)` [DependencyInjection]
- L837 [function] `private void RecordActionFailure(AIActionSet actionSet, AIActionFailure failure)` [DependencyInjection]
- L848 [function] `private void RecordNoActionFailure()` [DependencyInjection]
- L860 [function] `private static string GetActionLabel(AIActionSet actionSet)` [DependencyInjection]
- L868 [function] `private AIActionFailure RefineActionFailure(AIAction action, AIActionFailure failure)` [DependencyInjection]
- L887 [function] `private void RecordCandidateDebug(AIAction action, AIActionFailure failure)` [DependencyInjection]
- L902 [function] `private void RememberCandidateFailure(AIAction action, AIActionFailure failure)` [DependencyInjection]
- L922 [function] `private void ReleaseFinishedActionReservation()` [DependencyInjection]
- L932 [function] `private static bool ShouldCooldownCandidateFailure(AIActionFailureKind kind)` [DependencyInjection]
- L940 [function] `private static int GetFailureDebugPriority(AIActionFailureKind kind)` [DependencyInjection]
- L958 [function] `public string GetDebugSummary(int candidateCount = 3)` [DependencyInjection]
- L993 [function] `private static string GetDestinationLabel(BuildableObject destination)` [DependencyInjection]
- L1009 [function] `public int GetDebugHash()` [DependencyInjection]
- L1027 [function] `private void MarkDebugDirty()` [DependencyInjection]
- L1032 [type] `public enum AIActionPlanKind` [DependencyInjection]
- L1041 [type] `public class AIAction` [DependencyInjection]
- L1064 [function] `public void MarkStarted(float time)` [DependencyInjection]
- L1069 [function] `public float CalculateScore(CharacterActor actor)` [DependencyInjection]
- L1110 [function] `public bool SetDestinationWithFailure(CharacterActor actor, out AIActionFailure failure)` [DependencyInjection]
- L1152 [function] `public void RefreshReservation(CharacterActor actor)` [DependencyInjection]
- L1162 [function] `public void ReleaseReservation(CharacterActor actor)` [DependencyInjection]
- L1174 [function] `private bool TryReserveResolvedDestination(` [DependencyInjection]
- L1196 [function] `private bool ResolvePathPlan(CharacterActor actor, BuildableObject destination, out string failureReason)` [DependencyInjection]
- L1203 [function] `private bool ResolvePathPlan(CharacterActor actor, BuildableObject destination, out AIActionFailure failure)` [DependencyInjection]
- L1223 [function] `private static bool IsCharacterAtDestination(CharacterActor actor, BuildableObject destination)` [DependencyInjection]

### Assets/Scripts/Character/AI/AiDirectorContextAggregator.cs

- L8 [type] `public sealed class AiDirectorContextSummary`
- L18 [function] `public string ToPromptText(int maxCharacters)`
- L39 [type] `public static class AiDirectorContextAggregator`
- L41 [function] `public static AiDirectorContextSummary Build(CharacterActor target, int maxTargetEvents = 5)`
- L66 [function] `private static float AverageCondition(CharacterActor[] actors, CharacterCondition condition)`
- L92 [function] `private static int CountStockShortages(BuildableObject[] facilities)`
- L114 [function] `private static string[] GetTopQueuedFacilities(BuildableObject[] facilities, int limit)`
- L125 [function] `private static string[] GetRepeatedFailureReasons(CharacterActor[] actors, int limit)`
- L152 [function] `private static string[] GetRecentEvents(CharacterActor target, int limit)`
- L166 [function] `private static string GetBuildingLabel(BuildableObject building)`

### Assets/Scripts/Character/AI/AiDirectorContextSceneQuery.cs

- L4 [type] `public readonly struct AiDirectorContextSceneSnapshot` [DependencyInjection]
- L6 [function] `public AiDirectorContextSceneSnapshot(CharacterActor[] actors, BuildableObject[] facilities)` [DependencyInjection]
- L16 [type] `public interface IAiDirectorContextSceneQuery` [DependencyInjection]
- L18 [function] `AiDirectorContextSceneSnapshot Capture()` [DependencyInjection]
- L21 [type] `public sealed class AiDirectorContextSceneQuery : IAiDirectorContextSceneQuery` [DependencyInjection]
- L25 [function] `public AiDirectorContextSceneQuery(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L31 [function] `public AiDirectorContextSceneSnapshot Capture()` [DependencyInjection]

### Assets/Scripts/Character/AI/AiDirectorRuntime.cs

- L10 [type] `public sealed class AiDirectorRuntime : SerializedMonoBehaviour` [DependencyInjection, Reflection]
- L53 [function] `public void ConstructAiDirectorRuntime(ILocalLlmRuntimeProvider llmRuntimeProvider, IAiDirectorContextSceneQuery contextSceneQuery, ICharacterAiSchedulingService aiSchedulingService, ICharacterAiFacilityLookup facilityLookup)` [DependencyInjection]
- L65 [function] `public void SetWarningLogsSuppressedForDebug(bool value)` [DependencyInjection, Reflection]
- L70 [function] `private void Update()` [DependencyInjection, Reflection]
- L81 [function] `public void EvaluateOneActor()` [DependencyInjection, Reflection]
- L105 [function] `public bool ShouldRequestMoodImpulse(CharacterActor actor)` [DependencyInjection, Reflection]
- L128 [function] `public bool ShouldRequestMacroGoal(CharacterActor actor)` [DependencyInjection, Reflection]
- L158 [function] `private bool HasUrgentDirectorReason(CharacterActor actor)` [DependencyInjection, Reflection]
- L168 [function] `private bool HasRepeatedFailureReason(CharacterActor actor)` [DependencyInjection, Reflection]
- L177 [function] `private bool HasRoutineDirectorReason(CharacterActor actor)` [DependencyInjection, Reflection]
- L192 [function] `private float GetNextRoutineMacroGoalTime(CharacterActor actor)` [DependencyInjection, Reflection]
- L204 [function] `private void ScheduleNextRoutineMacroGoal(CharacterActor actor)` [DependencyInjection, Reflection]
- L214 [function] `private float GetNextMoodImpulseTime(CharacterActor actor)` [DependencyInjection, Reflection]
- L226 [function] `private void ScheduleNextMoodImpulse(CharacterActor actor)` [DependencyInjection, Reflection]
- L236 [function] `public bool RequestMoodImpulse(CharacterActor actor)` [DependencyInjection, Reflection]
- L262 [function] `public bool RequestMacroGoal(CharacterActor actor)` [DependencyInjection, Reflection]
- L288 [function] `private void OnMacroGoalResult(CharacterActor actor, LocalLlmResult result)` [DependencyInjection, Reflection]
- L319 [function] `private void OnMoodImpulseResult(CharacterActor actor, LocalLlmResult result)` [DependencyInjection, Reflection]
- L365 [function] `private bool TryGetLlmRuntime(out ILocalLlmRuntime queue)` [DependencyInjection]
- L382 [function] `private IAiDirectorContextSceneQuery RequireContextSceneQuery()` [DependencyInjection]
- L396 [function] `private ICharacterAiSchedulingService RequireAiSchedulingService()` [DependencyInjection]
- L406 [function] `private ICharacterAiFacilityLookup RequireFacilityLookup()` [DependencyInjection]
- L402 [function] `private string BuildMacroGoalPrompt(CharacterActor actor)` [DependencyInjection, Reflection]
- L437 [function] `private string BuildMoodImpulsePrompt(CharacterActor actor)` [DependencyInjection, Reflection]
- L480 [function] `private bool ValidateMoodImpulseTarget(CharacterMoodImpulse impulse, out string error)` [DependencyInjection, Reflection]
- L507 [function] `private void ApplyMoodImpulseSideEffects(CharacterActor actor, CharacterMoodImpulse impulse)` [DependencyInjection, Reflection]
- L534 [function] `private bool TryCreateMacroGoalFromMoodImpulse(` [DependencyInjection, Reflection]
- L572 [function] `private static float GetMood(CharacterActor actor)` [DependencyInjection, Reflection]
- L577 [function] `private static float GetCondition(CharacterActor actor, CharacterCondition condition)` [DependencyInjection, Reflection]
- L587 [function] `private void LogWarningIfAllowed(string message)` [DependencyInjection, Reflection]

### Assets/Scripts/Character/AI/CharacterAiBehaviorTasks.cs

- L6 [type] `internal static class CharacterAiBehaviorTaskServices` [DependencyInjection]
- L8 [function] `public static bool TryGetDecisionPipeline(CharacterActor actor, out ICharacterAiDecisionPipeline decisionPipeline)` [DependencyInjection]
- L24 [type] `public sealed class HasCriticalState : Conditional` [DependencyInjection]
- L11 [function] `public override void OnAwake()`
- L33 [function] `public override TaskStatus OnUpdate()` [DependencyInjection]
- L25 [type] `public sealed class HasMacroGoal : Conditional`
- L29 [function] `public override void OnAwake()`
- L34 [function] `public override TaskStatus OnUpdate()`
- L43 [type] `public sealed class HasMacroGoalType : Conditional`
- L48 [function] `public override void OnAwake()`
- L53 [function] `public override TaskStatus OnUpdate()`
- L62 [type] `public sealed class ClearMacroGoal : Action`
- L68 [function] `public override void OnAwake()`
- L73 [function] `public override TaskStatus OnUpdate()`
- L108 [type] `public sealed class RunComplainMacroGoal : Action` [DependencyInjection]
- L91 [function] `public override void OnAwake()`
- L117 [function] `public override TaskStatus OnUpdate()` [DependencyInjection]
- L134 [type] `public sealed class RunAvoidFacilityMacroGoal : Action` [DependencyInjection]
- L112 [function] `public override void OnAwake()`
- L143 [function] `public override TaskStatus OnUpdate()` [DependencyInjection]
- L160 [type] `public sealed class RunExitDungeonMacroGoal : Action` [DependencyInjection]
- L133 [function] `public override void OnAwake()`
- L169 [function] `public override TaskStatus OnUpdate()` [DependencyInjection]
- L186 [type] `public sealed class RunVandalizeMacroGoal : Action` [DependencyInjection]
- L154 [function] `public override void OnAwake()`
- L195 [function] `public override TaskStatus OnUpdate()` [DependencyInjection]
- L212 [type] `public sealed class RunMacroGoalDecision : Action` [DependencyInjection]
- L175 [function] `public override void OnAwake()`
- L221 [function] `public override TaskStatus OnUpdate()` [DependencyInjection]
- L235 [type] `public sealed class RunCriticalState : Action` [DependencyInjection]
- L193 [function] `public override void OnAwake()`
- L244 [function] `public override TaskStatus OnUpdate()` [DependencyInjection]
- L207 [type] `public sealed class HasContinuableCurrentAction : Conditional`
- L211 [function] `public override void OnAwake()`
- L216 [function] `public override TaskStatus OnUpdate()`
- L225 [type] `public sealed class ContinueCurrentAction : Action`
- L229 [function] `public override void OnAwake()`
- L234 [function] `public override TaskStatus OnUpdate()`
- L243 [type] `public sealed class ShouldStopCurrentAction : Conditional`
- L247 [function] `public override void OnAwake()`
- L252 [function] `public override TaskStatus OnUpdate()`
- L261 [type] `public sealed class StopCurrentActionForReplan : Action`
- L265 [function] `public override void OnAwake()`
- L270 [function] `public override TaskStatus OnUpdate()`
- L278 [type] `public abstract class CharacterRoutineGroupBranchBase : UtilitySelector`
- L284 [function] `public override void OnAwake()`
- L289 [function] `public override float GetPriority()`
- L294 [function] `public override float GetUtility()`
- L299 [function] `private float EvaluatePriority()`
- L316 [type] `public sealed class SurvivalNeedsRoutineBranch : CharacterRoutineGroupBranchBase`
- L322 [type] `public sealed class DutyWorkRoutineBranch : CharacterRoutineGroupBranchBase`
- L328 [type] `public sealed class LeisureVisitRoutineBranch : CharacterRoutineGroupBranchBase`
- L334 [type] `public sealed class IdleRoutineBranch : CharacterRoutineGroupBranchBase`
- L339 [type] `public abstract class CharacterJobGiverBranchBase : Sequence`
- L345 [function] `public override void OnAwake()`
- L350 [function] `public override float GetUtility()`
- L380 [function] `private CharacterAiJobGiver ResolveJobGiver()`
- L382 [type] `public sealed class ExitDungeonJobGiverBranch : CharacterJobGiverBranchBase`
- L388 [type] `public sealed class GetFoodJobGiverBranch : CharacterJobGiverBranchBase`
- L394 [type] `public sealed class RestJobGiverBranch : CharacterJobGiverBranchBase`
- L400 [type] `public sealed class ToiletJobGiverBranch : CharacterJobGiverBranchBase`
- L406 [type] `public sealed class HygieneJobGiverBranch : CharacterJobGiverBranchBase`
- L412 [type] `public sealed class WorkJobGiverBranch : CharacterJobGiverBranchBase`
- L418 [type] `public sealed class ShoppingJobGiverBranch : CharacterJobGiverBranchBase`
- L424 [type] `public sealed class LookAroundJobGiverBranch : CharacterJobGiverBranchBase`
- L430 [type] `public sealed class WaitJobGiverBranch : CharacterJobGiverBranchBase`
- L436 [type] `public sealed class AmbientIdleJobGiverBranch : Sequence`
- L441 [function] `public override void OnAwake()`
- L446 [function] `public override float GetUtility()`
- L466 [type] `public abstract class SelectCharacterActionBase : Action`
- L472 [function] `public override void OnAwake()`
- L477 [function] `public override TaskStatus OnUpdate()`
- L486 [function] `protected abstract bool MatchesAction(AIActionSet actionSet)`
- L490 [type] `public sealed class SelectExitDungeonAction : SelectCharacterActionBase`
- L493 [function] `protected override bool MatchesAction(AIActionSet actionSet)`
- L497 [type] `public sealed class SelectEatAction : SelectCharacterActionBase`
- L500 [function] `protected override bool MatchesAction(AIActionSet actionSet)`
- L504 [type] `public sealed class SelectRestAction : SelectCharacterActionBase`
- L507 [function] `protected override bool MatchesAction(AIActionSet actionSet)`
- L511 [type] `public sealed class SelectToiletAction : SelectCharacterActionBase`
- L514 [function] `protected override bool MatchesAction(AIActionSet actionSet)`
- L522 [type] `public sealed class SelectHygieneAction : SelectCharacterActionBase`
- L525 [function] `protected override bool MatchesAction(AIActionSet actionSet)`
- L533 [type] `public sealed class SelectWorkAction : SelectCharacterActionBase`
- L536 [function] `protected override bool MatchesAction(AIActionSet actionSet)`
- L540 [type] `public sealed class SelectShoppingAction : SelectCharacterActionBase`
- L543 [function] `protected override bool MatchesAction(AIActionSet actionSet)`
- L547 [type] `public sealed class SelectLookAroundAction : SelectCharacterActionBase`
- L550 [function] `protected override bool MatchesAction(AIActionSet actionSet)`
- L554 [type] `public sealed class SelectWaitAction : SelectCharacterActionBase`
- L557 [function] `protected override bool MatchesAction(AIActionSet actionSet)`
- L642 [type] `public sealed class RunSelectedCharacterAction : Action` [DependencyInjection]
- L565 [function] `public override void OnAwake()`
- L651 [function] `public override TaskStatus OnUpdate()` [DependencyInjection]
- L665 [type] `public sealed class RunIdleBehavior : Action` [DependencyInjection]
- L583 [function] `public override void OnAwake()`
- L674 [function] `public override TaskStatus OnUpdate()` [DependencyInjection]
- L596 [type] `public sealed class EmitContextBubble : Action`
- L601 [function] `public override void OnAwake()`
- L606 [function] `public override TaskStatus OnUpdate()`

### Assets/Scripts/Character/AI/CharacterAiDecisionPipeline.cs

- L5 [type] `public readonly struct CharacterAiDecisionTickResult` [DependencyInjection]
- L25 [type] `public interface ICharacterAiDecisionPipeline` [DependencyInjection]
- L27 [function] `bool HasCriticalState(CharacterActor actor)` [DependencyInjection]
- L28 [function] `CharacterAiDecisionTickResult RunCritical(CharacterActor actor, CharacterBlackboard blackboard)` [DependencyInjection]
- L29 [function] `bool HasMacroGoal(CharacterActor actor)` [DependencyInjection]
- L30 [function] `bool HasContinuableCurrentAction(CharacterActor actor)` [DependencyInjection]
- L31 [function] `bool ShouldStopCurrentActionForReplan(CharacterActor actor)` [DependencyInjection]
- L32 [function] `CharacterAiDecisionTickResult ContinueCurrentAction(CharacterActor actor)` [DependencyInjection]
- L33 [function] `CharacterAiDecisionTickResult StopCurrentActionForReplan(CharacterActor actor)` [DependencyInjection]
- L34 [function] `CharacterAiDecisionTickResult SelectJobGiverAction(CharacterActor actor, CharacterAiJobGiver jobGiver, string taskName)` [DependencyInjection]
- L35 [function] `CharacterAiDecisionTickResult RunSelectedAction(CharacterActor actor, string taskName, CharacterAiBranch branchOverride = CharacterAiBranch.None)` [DependencyInjection]
- L39 [function] `CharacterAiDecisionTickResult RunMacroGoalDecision(CharacterActor actor)` [DependencyInjection]
- L40 [function] `CharacterAiDecisionTickResult RunIdleBehavior(CharacterActor actor, CharacterBlackboard blackboard)` [DependencyInjection]
- L41 [function] `bool HasMacroGoalType(CharacterActor actor, CharacterMacroGoalType goalType)` [DependencyInjection]
- L42 [function] `CharacterAiDecisionTickResult ClearContinueMacro(CharacterActor actor)` [DependencyInjection]
- L43 [function] `CharacterAiDecisionTickResult RunComplainMacro(CharacterActor actor, CharacterBlackboard blackboard, CharacterMacroGoal goal)` [DependencyInjection]
- L44 [function] `CharacterAiDecisionTickResult ApplyAvoidFacility(CharacterActor actor, CharacterBlackboard blackboard, CharacterMacroGoal goal)` [DependencyInjection]
- L45 [function] `CharacterAiDecisionTickResult RunExitDungeonMacro(CharacterActor actor, CharacterBlackboard blackboard, CharacterMacroGoal goal)` [DependencyInjection]
- L46 [function] `CharacterAiDecisionTickResult RunVandalizeMacro(CharacterActor actor, CharacterBlackboard blackboard, CharacterMacroGoal goal)` [DependencyInjection]
- L49 [type] `public interface ICharacterAiFacilityLookup` [DependencyInjection]
- L51 [function] `BuildableObject FindFacility(int id, string tag)` [DependencyInjection]
- L54 [type] `public sealed class CharacterAiFacilityLookup : ICharacterAiFacilityLookup` [DependencyInjection]
- L58 [function] `public CharacterAiFacilityLookup(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L64 [function] `public BuildableObject FindFacility(int id, string tag)` [DependencyInjection]
- L79 [type] `public sealed class CharacterAiDecisionPipeline : ICharacterAiDecisionPipeline` [DependencyInjection]
- L81 [function] `public bool HasCriticalState(CharacterActor actor)` [DependencyInjection]
- L90 [function] `public CharacterAiDecisionTickResult RunCritical(CharacterActor actor, CharacterBlackboard blackboard)` [DependencyInjection]
- L106 [function] `public bool HasMacroGoal(CharacterActor actor)` [DependencyInjection]
- L111 [function] `public bool HasContinuableCurrentAction(CharacterActor actor)` [DependencyInjection]
- L118 [function] `public bool ShouldStopCurrentActionForReplan(CharacterActor actor)` [DependencyInjection]
- L125 [function] `public CharacterAiDecisionTickResult ContinueCurrentAction(CharacterActor actor)` [DependencyInjection]
- L93 [function] `GetActionLabel(runningAction?.actionset)` [DependencyInjection]
- L154 [function] `public CharacterAiDecisionTickResult StopCurrentActionForReplan(CharacterActor actor)` [DependencyInjection]
- L183 [function] `public CharacterAiDecisionTickResult SelectJobGiverAction(CharacterActor actor, CharacterAiJobGiver jobGiver, string taskName)` [DependencyInjection]
- L227 [function] `public CharacterAiDecisionTickResult RunSelectedAction(CharacterActor actor, string taskName, CharacterAiBranch branchOverride = CharacterAiBranch.None)` [DependencyInjection]
- L260 [function] `public CharacterAiDecisionTickResult RunMacroGoalDecision(CharacterActor actor)` [DependencyInjection]
- L321 [function] `public CharacterAiDecisionTickResult RunIdleBehavior(CharacterActor actor, CharacterBlackboard blackboard)` [DependencyInjection]
- L357 [function] `public bool HasMacroGoalType(CharacterActor actor, CharacterMacroGoalType goalType)` [DependencyInjection]
- L366 [function] `private CharacterAiDecisionTickResult RunMacroJobGiverDecision(` [DependencyInjection]
- L450 [function] `public CharacterAiDecisionTickResult ClearContinueMacro(CharacterActor actor)` [DependencyInjection]
- L462 [function] `public CharacterAiDecisionTickResult RunComplainMacro(CharacterActor actor, CharacterBlackboard blackboard, CharacterMacroGoal goal)` [DependencyInjection]
- L483 [function] `public CharacterAiDecisionTickResult ApplyAvoidFacility(CharacterActor actor, CharacterBlackboard blackboard, CharacterMacroGoal goal)` [DependencyInjection]
- L510 [function] `public CharacterAiDecisionTickResult RunExitDungeonMacro(CharacterActor actor, CharacterBlackboard blackboard, CharacterMacroGoal goal)` [DependencyInjection]
- L542 [function] `public CharacterAiDecisionTickResult RunVandalizeMacro(CharacterActor actor, CharacterBlackboard blackboard, CharacterMacroGoal goal)` [DependencyInjection]
- L579 [function] `private static BuildableObject FindFacility(CharacterActor actor, int id, string tag)` [DependencyInjection]
- L584 [function] `public static bool MatchesFacility(BuildableObject building, int id, string tag)` [DependencyInjection]
- L609 [function] `private static ICharacterAiFacilityLookup RequireFacilityLookup(CharacterActor actor)` [DependencyInjection]
- L620 [function] `private static ICharacterAiJobGiverCatalog RequireJobGiverCatalog(CharacterActor actor)` [DependencyInjection]
- L631 [function] `private static bool CanVandalize(BuildableObject target, out string failureReason)` [DependencyInjection]
- L667 [function] `private static string GetBuildingLabel(BuildableObject building)` [DependencyInjection]
- L679 [function] `public static CharacterAiBranch GetBranchForActionSet(AIActionSet actionSet)` [DependencyInjection]
- L696 [function] `public static string GetActionLabel(AIActionSet actionSet)` [DependencyInjection]
- L708 [function] `private static bool TryPrepare(` [DependencyInjection]
- L738 [function] `private static CharacterAiDecisionTickResult Result(` [DependencyInjection]

### Assets/Scripts/Character/AI/CharacterAiJobGiver.cs

- L4 [type] `public readonly struct CharacterAiActionCandidate`
- L26 [type] `public readonly struct CharacterAiJobCandidate`
- L55 [type] `public abstract class CharacterAiJobGiver`
- L60 [function] `public bool TryEvaluate(CharacterActor actor, out CharacterAiJobCandidate candidate)`
- L103 [function] `public abstract bool MatchesAction(AIActionSet actionSet)`
- L105 [function] `protected abstract float GetDomainScore(CharacterActor actor, out string reason)`
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
- L321 [function] `public override bool MatchesAction(AIActionSet actionSet)`
- L323 [function] `protected override float GetDomainScore(CharacterActor actor, out string reason)`
- L330 [type] `public sealed class GetFoodJobGiver : CharacterAiJobGiver`
- L334 [function] `public override bool MatchesAction(AIActionSet actionSet)`
- L336 [function] `protected override float GetDomainScore(CharacterActor actor, out string reason)`
- L344 [type] `public sealed class RestJobGiver : CharacterAiJobGiver`
- L348 [function] `public override bool MatchesAction(AIActionSet actionSet)`
- L350 [function] `protected override float GetDomainScore(CharacterActor actor, out string reason)`
- L360 [type] `public sealed class ToiletJobGiver : CharacterAiJobGiver`
- L365 [function] `public override bool MatchesAction(AIActionSet actionSet)`
- L371 [function] `protected override float GetDomainScore(CharacterActor actor, out string reason)`
- L379 [type] `public sealed class HygieneJobGiver : CharacterAiJobGiver`
- L384 [function] `public override bool MatchesAction(AIActionSet actionSet)`
- L390 [function] `protected override float GetDomainScore(CharacterActor actor, out string reason)`
- L398 [type] `public sealed class WorkJobGiver : CharacterAiJobGiver`
- L402 [function] `public override bool MatchesAction(AIActionSet actionSet)`
- L404 [function] `protected override float GetDomainScore(CharacterActor actor, out string reason)`
- L419 [function] `Need(actor, CharacterCondition.HUNGER)`
- L420 [function] `Need(actor, CharacterCondition.SLEEP)`
- L421 [function] `Need(actor, CharacterCondition.EXCRETION)`
- L422 [function] `Need(actor, CharacterCondition.HYGIENE)`
- L423 [function] `Need(actor, CharacterCondition.MOOD) * 0.8f)`
- L431 [type] `public sealed class ShoppingJobGiver : CharacterAiJobGiver`
- L435 [function] `public override bool MatchesAction(AIActionSet actionSet)`
- L437 [function] `protected override float GetDomainScore(CharacterActor actor, out string reason)`
- L445 [type] `public sealed class LookAroundJobGiver : CharacterAiJobGiver`
- L449 [function] `public override bool MatchesAction(AIActionSet actionSet)`
- L451 [function] `protected override float GetDomainScore(CharacterActor actor, out string reason)`
- L467 [type] `public sealed class WaitJobGiver : CharacterAiJobGiver`
- L471 [function] `public override bool MatchesAction(AIActionSet actionSet)`
- L473 [function] `protected override float GetDomainScore(CharacterActor actor, out string reason)`
- L482 [function] `Need(actor, CharacterCondition.HUNGER)`
- L483 [function] `Need(actor, CharacterCondition.SLEEP)`
- L484 [function] `Need(actor, CharacterCondition.FUN)`
- L485 [function] `Need(actor, CharacterCondition.MOOD)`
- L486 [function] `Need(actor, CharacterCondition.EXCRETION)`
- L487 [function] `Need(actor, CharacterCondition.HYGIENE))`
- L496 [type] `public interface ICharacterAiJobGiverCatalog` [DependencyInjection]
- L498 [function] `CharacterAiJobGiver ExitDungeon { get; }` [DependencyInjection]
- L499 [function] `CharacterAiJobGiver GetFood { get; }` [DependencyInjection]
- L500 [function] `CharacterAiJobGiver Rest { get; }` [DependencyInjection]
- L501 [function] `CharacterAiJobGiver Toilet { get; }` [DependencyInjection]
- L502 [function] `CharacterAiJobGiver Hygiene { get; }` [DependencyInjection]
- L503 [function] `CharacterAiJobGiver Work { get; }` [DependencyInjection]
- L504 [function] `CharacterAiJobGiver Shopping { get; }` [DependencyInjection]
- L505 [function] `CharacterAiJobGiver LookAround { get; }` [DependencyInjection]
- L506 [function] `CharacterAiJobGiver Wait { get; }` [DependencyInjection]
- L507 [function] `CharacterAiJobGiver Get(CharacterAiBranch branch)` [DependencyInjection]
- L510 [type] `public sealed class CharacterAiJobGiverCatalog : ICharacterAiJobGiverCatalog` [DependencyInjection]
- L512 [function] `public CharacterAiJobGiver ExitDungeon { get; }` [DependencyInjection]
- L513 [function] `public CharacterAiJobGiver GetFood { get; }` [DependencyInjection]
- L514 [function] `public CharacterAiJobGiver Rest { get; }` [DependencyInjection]
- L515 [function] `public CharacterAiJobGiver Toilet { get; }` [DependencyInjection]
- L516 [function] `public CharacterAiJobGiver Hygiene { get; }` [DependencyInjection]
- L517 [function] `public CharacterAiJobGiver Work { get; }` [DependencyInjection]
- L518 [function] `public CharacterAiJobGiver Shopping { get; }` [DependencyInjection]
- L519 [function] `public CharacterAiJobGiver LookAround { get; }` [DependencyInjection]
- L520 [function] `public CharacterAiJobGiver Wait { get; }` [DependencyInjection]
- L522 [function] `public CharacterAiJobGiver Get(CharacterAiBranch branch)` [DependencyInjection]

### Assets/Scripts/Character/AI/CharacterAiPersonality.cs

- L5 [type] `public class CharacterAiPersonality`
- L13 [function] `public float GetActionMultiplier(AIActionSet actionSet)`
- L41 [function] `private static float ClampMultiplier(float value)`
- L47 [type] `public static class CharacterAiPersonalityUtility`
- L49 [function] `public static float GetActionScoreMultiplier(CharacterActor actor, AIActionSet actionSet)`

### Assets/Scripts/Character/AI/CharacterAiScheduler.cs

- L10 [type] `public sealed class CharacterAiScheduler : MonoBehaviour` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L13 [function] `new ProfilerMarker("CharacterAiScheduler.ProcessAiBudget")` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L49 [function] `public int RegisteredCharacterCount => actors.Count` [DependencyInjection]
- L50 [function] `public int LastProcessedDecisionCount { get; private set; }` [DependencyInjection]
- L51 [function] `public int LastBehaviorTreeTickCount { get; private set; }` [DependencyInjection]
- L52 [function] `public int LastPathSearchCount { get; private set; }` [DependencyInjection]
- L53 [function] `public double LastProcessingMilliseconds { get; private set; }` [DependencyInjection]
- L54 [function] `public ExternalBehaviorTree CharacterAiExternalBehavior => characterAiExternalBehavior` [DependencyInjection]
- L55 [function] `public bool IsDrivingAi => enabled && driveCharacterUpdates` [DependencyInjection]
- L56 [function] `public int CurrentDecisionBudget => Mathf.Max(1, currentDecisionBudget)` [DependencyInjection]
- L57 [function] `public int CurrentPathSearchBudget => Mathf.Max(1, currentPathSearchBudget)` [DependencyInjection]
- L58 [function] `public bool IsPathBudgetActiveForDebug => enabled` [DependencyInjection]
- L62 [function] `private void Awake()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L67 [function] `private void OnEnable()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L74 [function] `public void Construct(IDungeonSceneComponentQuery sceneQuery, IMainCameraProvider mainCameraProvider, ICharacterBehaviorTreeRuntimeConfigurator behaviorTreeConfigurator)` [DependencyInjection]
- L88 [function] `private void Start()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L93 [function] `private void Update()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L98 [function] `public void RegisterActor(CharacterActor actor)` [DependencyInjection]
- L108 [function] `public void UnregisterActor(CharacterActor actor)` [DependencyInjection]
- L118 [function] `public void RequestImmediateDecisionFor(CharacterActor actor)` [DependencyInjection]
- L129 [function] `public bool TryConsumePathSearchBudget()` [DependencyInjection]
- L139 [function] `public bool ShouldShowCharacterFeedbackFor(CharacterActor actor)` [DependencyInjection]
- L149 [function] `public int GetMovementFrameStrideFor(CharacterActor actor)` [DependencyInjection]
- L162 [function] `public void RunManualTick(float deltaTime)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L168 [function] `public void ClearRegistrationsForDebug()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L179 [function] `public void ResetPathSearchBudgetForDebugInstance()` [DependencyInjection]
- L187 [function] `private void ProcessAiBudget(float now)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L264 [function] `private void RefreshBehaviorDesignerVisualsForEditor()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L285 [function] `private void RegisterExistingCharacters()` [DependencyInjection]
- L293 [function] `private void RegisterExistingCharactersIfInjected()` [DependencyInjection]
- L301 [function] `private IDungeonSceneComponentQuery RequireSceneQuery()` [DependencyInjection]
- L311 [function] `private void RegisterInternal(CharacterActor actor)` [DependencyInjection]
- L324 [function] `private bool TryRunScheduledDecision(CharacterActor actor)` [DependencyInjection]
- L370 [function] `private static void ConfigureBehaviorManagerForManualTick()` [RuntimeObjectCreation]
- L388 [function] `private BehaviorTree ConfigureCharacterBehaviorTree(CharacterActor actor)` [DependencyInjection]
- L393 [function] `private void UnregisterInternal(CharacterActor actor)` [DependencyInjection]
- L405 [function] `private void RemoveAt(int index)` [DependencyInjection]
- L421 [function] `private float GetDecisionInterval(CharacterActor actor)` [DependencyInjection]
- L438 [function] `private bool IsHighDetailCharacter(CharacterActor actor)` [DependencyInjection]
- L459 [function] `private bool TryConsumePathSearchBudgetInternal()` [DependencyInjection]
- L471 [function] `private void BeginPathBudgetWindow()` [DependencyInjection]
- L479 [function] `private void ResetPathBudgetIfNeeded()` [DependencyInjection]
- L490 [function] `private int GetDecisionBudgetForFrame()` [DependencyInjection]
- L496 [function] `private int GetPathSearchBudgetForFrame()` [DependencyInjection]
- L502 [function] `private void EnsureAdaptiveBudgetsInitialized()` [DependencyInjection]
- L510 [function] `private void ResetAdaptiveBudgets()` [DependencyInjection]
- L516 [function] `private void UpdateAdaptiveBudgets()` [DependencyInjection]
- L551 [function] `private IMainCameraProvider RequireMainCameraProvider()` [DependencyInjection]
- L556 [function] `private ICharacterBehaviorTreeRuntimeConfigurator RequireBehaviorTreeConfigurator()` [DependencyInjection]

### Assets/Scripts/Character/AI/CharacterBehaviorTreeRuntimeConfigurator.cs

- L5 [type] `public interface ICharacterBehaviorTreeRuntimeConfigurator` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L7 [function] `BehaviorTree Configure(CharacterActor actor, ExternalBehaviorTree externalBehavior)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L10 [type] `public sealed class CharacterBehaviorTreeRuntimeConfigurator : ICharacterBehaviorTreeRuntimeConfigurator` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L12 [function] `public BehaviorTree Configure(CharacterActor actor, ExternalBehaviorTree externalBehavior)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Character/AI/CharacterBlackboard.cs

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
- L202 [function] `Bind(GetComponent<CharacterActor>())`
- L205 [function] `public void Bind(CharacterActor owner)`
- L216 [function] `PruneFacilityCooldowns()`
- L219 [function] `public void BeginDecisionTrace(int tick)`
- L223 [function] `AppendDecisionTrace($"Tick {tick}")`
- L226 [function] `public void RecordBtStatus(CharacterAiBranch branch, string taskName, string status)`
- L231 [function] `AppendDecisionTrace($"BT {branch}/{currentTask}: {TrimTrace(currentStatus)}")`
- L234 [function] `public void SetIntent(CharacterAiBranch branch, string intent, string taskName = "", string status = "")`
- L245 [function] `public void RecordJobGiverUtility(CharacterAiBranch branch, float utility, string detail)`
- L256 [function] `AppendDecisionTrace($"Utility {branch}={utility:0.###} {TrimTrace(detail)}")`
- L259 [function] `public void RecordSelectedJobGiverUtility(CharacterAiJobCandidate candidate)`
- L262 [function] `AppendDecisionTrace($"Selected {candidate.Branch}: {TrimTrace(candidate.DebugSummary)}")`
- L265 [function] `public void RecordSelectedUtilitySummary(string summary)`
- L268 [function] `AppendDecisionTrace($"Selected: {TrimTrace(selectedJobGiverUtilitySummary)}")`
- L271 [function] `public void RecordRoutineGroupPriority(CharacterAiBranch branch, float priority, string detail)`
- L282 [function] `AppendDecisionTrace($"Priority {branch}={priority:0.###} {TrimTrace(detail)}")`
- L285 [function] `public void ClearJobGiverCandidateCache()`
- L291 [function] `public void CacheJobGiverCandidate(CharacterAiJobCandidate candidate)`
- L302 [function] `public void RemoveJobGiverCandidateCache(CharacterAiBranch branch)`
- L322 [function] `public void Commit(AIAction action, string intent)`
- L335 [function] `public void RefreshCommitment(AIAction action)`
- L348 [function] `public bool TryGetCommitmentBonus(AIAction action, out float bonus)`
- L368 [function] `ClearCommitment(CharacterAiInterruptReason.DestinationInvalid, "Committed destination is invalid.")`
- L376 [function] `public bool CanBreakCommitment(CharacterAiInterruptReason reason)`
- L389 [function] `public void ClearCommitment(CharacterAiInterruptReason reason, string detail)`
- L402 [function] `AppendDecisionTrace($"CommitBreak {TrimTrace(lastCommitBreakReason)}")`
- L405 [function] `public bool IsFacilityCoolingDown(BuildableObject building, out float remainingSeconds)`
- L413 [function] `PruneFacilityCooldowns()`
- L423 [function] `public void PutFacilityOnCooldown(BuildableObject building, string reason)`
- L437 [function] `public void ReportActionFailure(AIActionSet actionSet, AIActionFailure failure)`
- L465 [function] `ClearCommitment(CharacterAiInterruptReason.NoPath, failure.ToString())`
- L469 [function] `ClearCommitment(CharacterAiInterruptReason.DestinationInvalid, failure.ToString())`
- L475 [function] `ClearCommitment(CharacterAiInterruptReason.FacilityUnavailable, failure.ToString())`
- L479 [function] `public bool HasActiveMacroGoal()`
- L491 [function] `ClearMacroGoal("Macro goal expired.")`
- L495 [function] `public bool HasActiveMoodImpulse()`
- L513 [function] `ClearMoodImpulse("Mood impulse expired.")`
- L517 [function] `public void SetMacroGoal(CharacterMacroGoal goal)`
- L536 [function] `ClearCommitment(CharacterAiInterruptReason.MacroGoalChanged, $"Macro goal changed to {goal.type}.")`
- L540 [function] `public void ClearMacroGoal(string reason)`
- L552 [function] `public void SetMoodImpulse(CharacterMoodImpulse impulse)`
- L582 [function] `public void ClearMoodImpulse(string reason)`
- L592 [function] `AppendDecisionTrace($"MoodImpulse cleared: {TrimTrace(reason)}")`
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

### Assets/Scripts/Character/AI/CharacterDialogueBubbleFactory.cs

- L6 [type] `public interface ICharacterDialogueBubbleFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L8 [function] `TextMeshPro Create(Transform parent)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L11 [type] `public sealed class CharacterDialogueBubbleFactory : ICharacterDialogueBubbleFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L16 [function] `public CharacterDialogueBubbleFactory(ITmpKoreanFontService tmpKoreanFontService)` [DependencyInjection]
- L22 [function] `public TextMeshPro Create(Transform parent)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Character/AI/CharacterDialogueRuntime.cs

- L9 [type] `public sealed class CharacterDialogueRuntime : MonoBehaviour` [DependencyInjection, Reflection, SceneMutation]
- L33 [function] `public void ConstructCharacterDialogueRuntime(ILocalLlmRuntimeProvider llmRuntimeProvider, ICharacterAiSchedulingService aiSchedulingService, ICharacterDialogueBubbleFactory bubbleFactory)` [DependencyInjection]
- L46 [function] `private void Awake()` [DependencyInjection, Reflection, SceneMutation]
- L53 [function] `private void OnEnable()` [DependencyInjection, Reflection, SceneMutation]
- L62 [function] `private void OnDisable()` [DependencyInjection, Reflection, SceneMutation]
- L75 [function] `private void LateUpdate()` [DependencyInjection, Reflection, SceneMutation]
- L91 [function] `public void ShowLine(string line)` [DependencyInjection, Reflection, SceneMutation]
- L106 [function] `private void OnLogAdded(CharacterLogEntry entry)` [DependencyInjection, Reflection, SceneMutation]
- L132 [function] `private void OnBubbleResult(LocalLlmResult result)` [DependencyInjection, Reflection, SceneMutation]
- L159 [function] `private bool TryGetLlmRuntime(out ILocalLlmRuntime queue)` [DependencyInjection]
- L176 [function] `private ICharacterAiSchedulingService RequireAiSchedulingService()` [DependencyInjection]
- L182 [function] `private void HideLine()` [DependencyInjection, Reflection, SceneMutation]
- L193 [function] `private string BuildPrompt(CharacterLogEntry entry)` [DependencyInjection, Reflection, SceneMutation]
- L208 [function] `private static bool ShouldRequestBubble(CharacterLogEntry entry)` [DependencyInjection, Reflection, SceneMutation]
- L237 [function] `private void EnsureText()` [DependencyInjection, Reflection, SceneMutation]
- L247 [function] `private Vector3 GetLocalOffset()` [DependencyInjection, Reflection, SceneMutation]
- L259 [function] `private ICharacterDialogueBubbleFactory RequireBubbleFactory()` [DependencyInjection]
- L266 [function] `private static bool ContainsAny(string value, params string[] patterns)` [DependencyInjection, Reflection, SceneMutation]

### Assets/Scripts/Character/AI/CharacterMoodImpulseUtility.cs

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

### Assets/Scripts/Character/AI/CharacterSocialMemory.cs

- L6 [type] `public enum SocialRumorType` [Reflection]
- L15 [type] `public enum SocialRumorTargetType` [Reflection]
- L23 [type] `public sealed class SocialRumor` [Reflection]
- L46 [function] `public SocialRumor Clone()` [Reflection]
- L53 [type] `public sealed class SocialMemoryFloat` [Reflection]
- L58 [function] `public SocialMemoryFloat(string key, float value)` [Reflection]
- L67 [type] `public sealed class CharacterSocialMemory : SerializedMonoBehaviour` [Reflection]
- L83 [function] `private void Awake()` [Reflection]
- L85 [function] `Bind(GetComponent<CharacterActor>())` [Reflection]
- L88 [function] `public void Bind(CharacterActor owner)` [Reflection]
- L93 [function] `public void HearRumor(SocialRumor rumor, CharacterActor speaker)` [Reflection]
- L104 [function] `RememberRumor(copy)` [Reflection]
- L110 [function] `Blend(facilitySentimentByKey, key, copy.sentiment, newRumorBlend)` [Reflection]
- L117 [function] `Blend(characterSentimentByKey, key, copy.sentiment, newRumorBlend)` [Reflection]
- L128 [function] `SyncDebugLists()` [Reflection]
- L131 [function] `public float GetFacilitySentiment(BuildableObject building)` [Reflection]
- L138 [function] `PruneExpiredRumors()` [Reflection]
- L155 [function] `public float GetRelationshipSentiment(CharacterActor target)` [Reflection]
- L174 [function] `public float GetSourceTrust(CharacterActor source)` [Reflection]
- L185 [function] `private float GetSourceTrustScore(CharacterActor speaker, SocialRumor rumor)` [Reflection]
- L211 [function] `private void RememberRumor(SocialRumor rumor)` [Reflection]
- L214 [function] `PruneExpiredRumors()` [Reflection]
- L221 [function] `private void PruneExpiredRumors()` [Reflection]
- L235 [function] `RebuildSentimentMaps()` [Reflection]
- L239 [function] `private void RebuildSentimentMaps()` [Reflection]
- L254 [function] `Blend(facilitySentimentByKey, key, rumor.sentiment, newRumorBlend)` [Reflection]
- L261 [function] `Blend(characterSentimentByKey, key, rumor.sentiment, newRumorBlend)` [Reflection]
- L266 [function] `SyncDebugLists()` [Reflection]
- L269 [function] `private void SyncDebugLists()` [Reflection]
- L271 [function] `SyncDebugList(facilitySentimentByKey, facilitySentimentDebug)` [Reflection]
- L272 [function] `SyncDebugList(characterSentimentByKey, characterSentimentDebug)` [Reflection]
- L273 [function] `SyncDebugList(sourceTrustByKey, sourceTrustDebug)` [Reflection]
- L276 [function] `private static void Blend(Dictionary<string, float> map, string key, float value, float blend)` [Reflection]
- L287 [function] `private static float GetDictionaryValue(Dictionary<string, float> map, string key, float fallback)` [Reflection]
- L294 [function] `private static void SyncDebugList(Dictionary<string, float> source, List<SocialMemoryFloat> target)` [Reflection]
- L304 [type] `public static class SocialRumorUtility` [Reflection]
- L306 [function] `public static IEnumerable<string> GetFacilityKeys(SocialRumor rumor)` [Reflection]
- L324 [function] `public static IEnumerable<string> GetCharacterKeys(SocialRumor rumor)` [Reflection]
- L342 [function] `public static string GetActorKey(CharacterActor actor)` [Reflection]
- L353 [function] `public static string GetActorNameKey(CharacterActor actor)` [Reflection]
- L363 [function] `public static bool MatchesFacilityKey(BuildableObject building, string key)` [Reflection]
- L384 [function] `public static bool MatchesFacilityTag(BuildableObject building, string tag)` [Reflection]
- L400 [function] `public static string GetActorLabel(CharacterActor actor)` [Reflection]
- L410 [function] `public static string GetFacilityLabel(BuildableObject building)` [Reflection]
- L425 [function] `public static string GetFacilityTag(BuildableObject building)` [Reflection]
- L440 [function] `private static bool ContainsNormalized(string source, string normalizedNeedle)` [Reflection]
- L446 [function] `private static string Normalize(string value)` [Reflection]

### Assets/Scripts/Character/AI/CharacterSocialMemoryService.cs

- L5 [type] `public interface ICharacterSocialMemoryService` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L7 [function] `CharacterSocialMemory GetOrAdd(CharacterActor actor)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L10 [type] `public sealed class CharacterSocialMemoryService : ICharacterSocialMemoryService` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L14 [function] `public CharacterSocialMemoryService(IObjectResolver objectResolver)` [DependencyInjection]
- L20 [function] `public CharacterSocialMemory GetOrAdd(CharacterActor actor)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]


## Assets\Scripts\Character\AI\Consideration

### Assets/Scripts/Character/AI/Consideration/Consideration.cs

- L7 [type] `public abstract class Consideration : SerializedScriptableObject`
- L10 [function] `public abstract float ScoreConsideration(CharacterActor actor)`

### Assets/Scripts/Character/AI/Consideration/ConsiderationCanLookAround.cs

- L4 [type] `public class ConsiderationCanLookAround : Consideration`
- L6 [function] `public override float ScoreConsideration(CharacterActor actor)`

### Assets/Scripts/Character/AI/Consideration/ConsiderationFacilityNeed.cs

- L4 [type] `public class ConsiderationFacilityNeed : Consideration`
- L15 [function] `public override float ScoreConsideration(CharacterActor actor)`

### Assets/Scripts/Character/AI/Consideration/ConsiderationIsVisitable.cs

- L3 [type] `public class ConsiderationIsVisitable : Consideration`
- L7 [function] `public override float ScoreConsideration(CharacterActor actor)`
- L34 [function] `private static FacilityRole ConvertLegacyType(Shop.Type type)`

### Assets/Scripts/Character/AI/Consideration/ConsiderationRandom.cs

- L6 [type] `public class ConsiderationRandom : Consideration`
- L10 [function] `public override float ScoreConsideration(CharacterActor actor)`

### Assets/Scripts/Character/AI/Consideration/ConsiderationShoppingCount.cs

- L6 [type] `public class ConsiderationShoppingCount : Consideration`
- L8 [function] `public override float ScoreConsideration(CharacterActor actor)`

### Assets/Scripts/Character/AI/Consideration/ConsiderationShouldExitDungeon.cs

- L4 [type] `public class ConsiderationShouldExitDungeon : Consideration`
- L6 [function] `public override float ScoreConsideration(CharacterActor actor)`

### Assets/Scripts/Character/AI/Consideration/ConsiderationStat.cs

- L6 [type] `public class ConsiderationStat : Consideration`
- L10 [function] `public override float ScoreConsideration(CharacterActor actor)`

### Assets/Scripts/Character/AI/Consideration/ConsiderationWorkNeed.cs

- L4 [type] `public class ConsiderationWorkNeed : Consideration`
- L15 [function] `public override float ScoreConsideration(CharacterActor actor)`


## Assets\Scripts\Character\AI

### Assets/Scripts/Character/AI/CustomerPersonaRuntime.cs

- L7 [type] `public sealed class CustomerPersonaData`
- L20 [function] `public void Clamp()`
- L32 [function] `private static float ClampMultiplier(float value)`
- L40 [type] `public sealed class CustomerPersonaRuntime : SerializedMonoBehaviour`
- L55 [function] `private void Awake()`
- L57 [function] `Bind(GetComponent<CharacterActor>())`
- L60 [function] `public void Bind(CharacterActor owner)`
- L66 [function] `public bool RequestPersonaIfNeeded(bool logIfMissingQueue = true)`
- L114 [function] `public void ApplyGeneratedPersona(CustomerPersonaData generatedPersona)`
- L129 [function] `public float GetActionMultiplier(AIActionSet actionSet)`
- L157 [function] `public float GetConditionCurveMultiplier(CharacterCondition condition)`
- L169 [function] `public float GetFacilityTagPreference(BuildableObject building)`
- L201 [function] `private static bool ValidatePersona(CustomerPersonaData candidate, out string error)`
- L237 [function] `private static bool IsMultiplierValid(float value, string fieldName, out string error)`
- L249 [function] `private void OnPersonaResult(LocalLlmResult result)`
- L266 [function] `ApplyGeneratedPersona(dto.ToRuntimeData())`
- L269 [function] `private static string BuildPersonaPrompt(CharacterActor actor)`
- L291 [function] `private static float GetCondition(CharacterActor actor, CharacterCondition condition)`


## Assets\Scripts\Character\AI\Editor

### Assets/Scripts/Character/AI/Editor/AiDebugScenarioActionFactory.cs

- L5 [type] `public static class AiDebugScenarioActionFactory` [ResourcesAccess]
- L7 [function] `public static AIAction[] CreateCustomerActions()` [ResourcesAccess]
- L17 [function] `public static AIAction[] CreateStaffActions()` [ResourcesAccess]
- L27 [function] `private static AIAction[] CreateActions(params string[] resourcePaths)` [ResourcesAccess]

### Assets/Scripts/Character/AI/Editor/CharacterAiBehaviorDesignerGraphBuilder.cs

- L12 [type] `public static class CharacterAiBehaviorDesignerGraphBuilder` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L19 [function] `public static void BuildVisualCharacterAiBehaviorTrees()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L27 [function] `RefreshOpenBehaviorDesignerWindow()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L38 [function] `RefreshOpenBehaviorDesignerWindow()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L44 [function] `public static bool EnsureVisualGraph(BehaviorTree tree)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L50 [function] `public static ExternalBehaviorTree EnsureCharacterAiExternalBehavior()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L52 [function] `EnsureExternalBehaviorFolder()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L62 [function] `EnsureVisualGraph(externalBehavior)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L66 [function] `public static bool EnsureVisualGraph(ExternalBehaviorTree externalBehavior)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L86 [function] `AddChildren(critical, hasCritical, runCritical)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L98 [function] `new Vector2(-380f, 120f))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L104 [function] `new Vector2(-380f, 220f))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L110 [function] `new Vector2(-380f, 320f))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L116 [function] `new Vector2(-380f, 420f))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L122 [function] `new Vector2(-380f, 520f))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L128 [function] `new Vector2(-380f, 620f))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L134 [function] `new Vector2(-380f, 720f))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L140 [function] `new Vector2(-380f, 820f))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L146 [function] `new Vector2(-380f, 920f)))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L154 [function] `CreateSurvivalNeedsRoutine(ref id, new Vector2(700f, 150f))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L155 [function] `CreateDutyWorkRoutine(ref id, new Vector2(1480f, 150f))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L156 [function] `CreateLeisureVisitRoutine(ref id, new Vector2(700f, 530f))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L157 [function] `CreateIdleRoutine(ref id, new Vector2(1220f, 530f)))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L159 [function] `AddChildren(root, critical, macro, continueCurrent, stopCurrent, normal)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L192 [function] `private static void AddChildren(ParentTask parent, params Task[] children)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L219 [function] `AddChildren(branch, selector, runner)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L223 [function] `private static Sequence CreateContinueCurrentBranch(ref int id, Vector2 branchOffset)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L233 [function] `private static Sequence CreateStopCurrentBranch(ref int id, Vector2 branchOffset)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L243 [function] `private static SurvivalNeedsRoutineBranch CreateSurvivalNeedsRoutine(ref int id, Vector2 branchOffset)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L253 [function] `new Vector2(160f, 270f))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L258 [function] `new Vector2(440f, 270f))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L263 [function] `new Vector2(720f, 270f))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L268 [function] `new Vector2(1000f, 270f))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L273 [function] `new Vector2(1280f, 270f)))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L277 [function] `private static DutyWorkRoutineBranch CreateDutyWorkRoutine(ref int id, Vector2 branchOffset)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L287 [function] `new Vector2(1480f, 430f)))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L291 [function] `private static LeisureVisitRoutineBranch CreateLeisureVisitRoutine(ref int id, Vector2 branchOffset)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L301 [function] `new Vector2(540f, 650f))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L306 [function] `new Vector2(860f, 650f)))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L310 [function] `private static IdleRoutineBranch CreateIdleRoutine(ref int id, Vector2 branchOffset)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L320 [function] `new Vector2(1140f, 650f))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L321 [function] `CreateAmbientIdleBranch(ref id, new Vector2(1460f, 650f)))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L325 [function] `private static Sequence CreateAmbientIdleBranch(ref int id, Vector2 branchOffset)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L346 [function] `CreateMacroGoalCondition(ref id, $"Has {macroGoalType}?", branchOffset + new Vector2(-100f, 80f), macroGoalType)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L363 [function] `CreateMacroGoalCondition(ref id, $"Has {macroGoalType}?", branchOffset + new Vector2(-100f, 80f), macroGoalType)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L364 [function] `CreateClearMacroTask(ref id, taskName, reason, failAfterClear, branchOffset + new Vector2(100f, 80f)))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L379 [function] `CreateMacroGoalCondition(ref id, $"Has {macroGoalType}?", branchOffset + new Vector2(-285f, 110f), macroGoalType)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L399 [function] `SetNodeOffset(macroTask, branchOffset + new Vector2(100f, 100f))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L402 [function] `CreateMacroGoalCondition(ref id, $"Has {macroGoalType}?", branchOffset + new Vector2(-100f, 100f), macroGoalType)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L407 [function] `private static void SetNodeOffset(Task task, Vector2 offset)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L440 [function] `new Vector2(150f, 180f))` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L456 [function] `private static void EnsureExternalBehaviorFolder()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L511 [function] `private static int BuildPrefabGraphs(ExternalBehaviorTree externalBehavior)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L547 [function] `private static int BuildOpenSceneGraphs(ExternalBehaviorTree externalBehavior)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L586 [function] `private static int BuildCurrentPrefabStageGraph(ExternalBehaviorTree externalBehavior)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L622 [function] `private static int BuildRuntimeGraphs(ExternalBehaviorTree externalBehavior)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L654 [function] `private static bool EnsureActorGraph(CharacterActor actor, ExternalBehaviorTree externalBehavior)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L686 [function] `private static void RefreshOpenBehaviorDesignerWindow()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]

### Assets/Scripts/Character/AI/Editor/CharacterAiBehaviorDesignerRuntimeDebugger.cs

- L8 [type] `public static class CharacterAiBehaviorDesignerRuntimeDebugger` [SingletonAccess, EditorAssetAccess]
- L10 [function] `static CharacterAiBehaviorDesignerRuntimeDebugger()` [SingletonAccess, EditorAssetAccess]
- L16 [function] `private static void RefreshActiveRuntimeTree()` [SingletonAccess, EditorAssetAccess]
- L43 [function] `LoadExternalSource(window, tree)` [SingletonAccess, EditorAssetAccess]
- L60 [function] `LoadExternalSource(window, tree)` [SingletonAccess, EditorAssetAccess]
- L64 [function] `private static BehaviorTree GetActiveRuntimeTree(BehaviorDesignerWindow window)` [SingletonAccess, EditorAssetAccess]
- L72 [function] `private static BehaviorTree GetSelectedRuntimeTree()` [SingletonAccess, EditorAssetAccess]
- L85 [function] `private static void LoadExternalSource(BehaviorDesignerWindow window, BehaviorTree tree)` [SingletonAccess, EditorAssetAccess]

### Assets/Scripts/Character/AI/Editor/CharacterAiJobGiverRegistry.cs

- L1 [type] `public static class CharacterAiJobGiverRegistry`
- L5 [function] `public static CharacterAiJobGiver ExitDungeon => Catalog.ExitDungeon`
- L6 [function] `public static CharacterAiJobGiver GetFood => Catalog.GetFood`
- L7 [function] `public static CharacterAiJobGiver Rest => Catalog.Rest`
- L8 [function] `public static CharacterAiJobGiver Toilet => Catalog.Toilet`
- L9 [function] `public static CharacterAiJobGiver Hygiene => Catalog.Hygiene`
- L10 [function] `public static CharacterAiJobGiver Work => Catalog.Work`
- L11 [function] `public static CharacterAiJobGiver Shopping => Catalog.Shopping`
- L12 [function] `public static CharacterAiJobGiver LookAround => Catalog.LookAround`
- L13 [function] `public static CharacterAiJobGiver Wait => Catalog.Wait`
- L15 [function] `public static CharacterAiJobGiver Get(CharacterAiBranch branch)`

### Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugFixtures.cs

- L6 [type] `internal static class CharacterAiPlanDebugFixtures` [GlobalObjectLookup, SingletonAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L8 [function] `public static AiDirectorRuntime GetOrCreatePlayModeAiDirector(out GameObject createdObject)` [GlobalObjectLookup, SingletonAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L21 [function] `public static AiDirectorRuntime FindPlayModeAiDirector()` [GlobalObjectLookup, SingletonAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L37 [function] `public static string SafeDebugValue(string value, int maxLength = 80)` [GlobalObjectLookup, SingletonAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L54 [function] `public static GameObject CreatePlayActorObject(string name)` [GlobalObjectLookup, SingletonAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L67 [function] `public static GameObject CreateActorObject(string name)` [GlobalObjectLookup, SingletonAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L84 [function] `public static void DestroyProbeObject(Object target)` [GlobalObjectLookup, SingletonAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L101 [function] `public static GameObject EnsureGridForScenario(out bool created)` [GlobalObjectLookup, SingletonAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L121 [function] `public static SocialReputationRuntime EnsureSocialRuntimeInstance(out GameObject createdObject)` [GlobalObjectLookup, SingletonAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L137 [function] `public static LocalLlmRequestQueue EnsureQueueInstance(out GameObject createdObject)` [GlobalObjectLookup, SingletonAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L184 [function] `public static CharacterSO CreateCharacterData(` [GlobalObjectLookup, SingletonAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L196 [function] `public static BuildingSO CreateBuildingData(int id, string objectName)` [GlobalObjectLookup, SingletonAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L215 [function] `private static void EnsureActorRuntimeComponents(GameObject actorObject)` [GlobalObjectLookup, SingletonAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L233 [function] `private static T EnsureLocalComponent<T>(GameObject target)` [GlobalObjectLookup, SingletonAccess, Reflection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugScenarios.cs

- L10 [type] `public static class CharacterAiPlanDebugScenarios` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L63 [function] `private static void ResetSchedulerPathBudgetForDebug()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation]
- L58 [function] `public static void PrepareTestScene()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L64 [function] `public static void RunAll()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L110 [function] `public static void RunPlayModeSocialAiProbe()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L122 [function] `public static void RunPlayModeBehaviorTreeActivationProbeMenu()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L128 [function] `public static void RunPlayModeRimWorldStyleAiProbeMenu()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L134 [function] `public static void RunPlayModeMoodImpulseAiProbeMenu()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L140 [function] `public static void StartPlayModeMoodImpulseLlmProbeMenu()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L146 [function] `public static void GetPlayModeMoodImpulseLlmProbeMenu()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L152 [function] `public static void RunPlayModeRelationshipAiProbe()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L164 [function] `public static void StartPlayModePersonaMacroLlmProbeMenu()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L170 [function] `public static void GetPlayModePersonaMacroLlmProbeMenu()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L176 [function] `public static void StartPlayModeBubbleLlmProbeMenu()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L182 [function] `public static void GetPlayModeBubbleLlmProbeMenu()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L188 [function] `public static void StartPlayModeSocialLogLlmProbeMenu()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L194 [function] `public static void TriggerPlayModeSocialLogLlmProbeMenu()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L200 [function] `public static void GetPlayModeSocialLogLlmProbeMenu()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L205 [function] `public static string RunPlayModeSocialRuntimeProbe()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L325 [function] `public static string RunPlayModeMoodImpulseRuntimeProbe()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L429 [function] `public static string StartPlayModeMoodImpulseLlmProbe()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L493 [function] `public static string GetPlayModeMoodImpulseLlmProbeReport(bool cleanupOnComplete = true)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L567 [function] `public static void CleanupPlayModeMoodImpulseLlmProbe()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L585 [function] `private static bool VerifyMoodImpulseRuntimeBias(` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L628 [function] `public static string RunPlayModeBehaviorTreeActivationProbe()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L717 [function] `public static string RunPlayModeRimWorldStyleAiProbe()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L852 [function] `private static bool HasNormalRoutineTree(BehaviorTree tree)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L871 [function] `private static bool HasRoutineGroupPriorityReport(CharacterActor actor)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L885 [function] `private static bool HasRunningTransitionBelowRoot(BehaviorTree tree)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L906 [function] `private static bool HasRunningLeafTransitionBelowRoot(BehaviorTree tree)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L927 [function] `private static bool HasRunningNode(BehaviorDesigner.Runtime.Tasks.Task task)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L955 [function] `private static bool HasRunningLeafNode(BehaviorDesigner.Runtime.Tasks.Task task)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L978 [function] `public static string RunPlayModeRelationshipRuntimeProbe()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1079 [function] `public static string StartPlayModeSocialLlmProbe()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1114 [function] `public static string GetPlayModeSocialLlmProbeReport()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1122 [function] `public static string GetPlayModeLlmQueueDebugReport()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1135 [function] `public static string StartPlayModeSocialEndToEndProbe()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1185 [function] `public static string GetPlayModeSocialEndToEndProbeReport(bool cleanupOnComplete = true)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1233 [function] `public static void CleanupPlayModeSocialEndToEndProbe()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1257 [function] `public static string StartPlayModeSocialLogLlmProbe()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1305 [function] `public static string TriggerPlayModeSocialLogLlmProbeEvent()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1341 [function] `public static string GetPlayModeSocialLogLlmProbeReport(bool cleanupOnComplete = true)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1400 [function] `public static void CleanupPlayModeSocialLogLlmProbe()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1426 [function] `public static string StartPlayModePersonaMacroLlmProbe()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1477 [function] `public static string GetPlayModePersonaMacroLlmProbeReport(bool cleanupOnComplete = true)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1548 [function] `public static void CleanupPlayModePersonaMacroLlmProbe()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1565 [function] `public static string StartPlayModeBubbleLlmProbe()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1607 [function] `public static string GetPlayModeBubbleLlmProbeReport(bool cleanupOnComplete = true)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1651 [function] `public static void CleanupPlayModeBubbleLlmProbe()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1662 [function] `private static void OnPlayModeSocialLlmProbeResult(LocalLlmResult result)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1682 [function] `private static void RunScenario(string name, System.Func<bool> scenario, List<string> errors)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1697 [function] `private static bool VerifyLlmJsonParser()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1731 [function] `private static bool VerifySocialRumorJsonParser()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1764 [function] `private static bool VerifyBlackboardCooldown()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1784 [function] `private static bool VerifySocialRumorSpreadAndReputation()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1846 [function] `private static bool VerifySocialRelationshipAndSourceTrust()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1924 [function] `private static bool VerifySocialReputationAffectsFacilityScore()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L1994 [function] `private static bool VerifyRimWorldStyleBtUtilityResponsibility()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2098 [function] `private static bool VerifyNoFlatUtilitySelectionApi()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2110 [function] `private static bool VerifyMoodImpulseJsonParser()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2149 [function] `private static bool VerifyMoodImpulseBiasesJobGiverUtility()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2233 [function] `private static bool VerifyMoodImpulseInterruptsCurrentAction()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2288 [function] `private static bool VerifyDirectorMoodImpulseTrigger()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2357 [function] `private static bool VerifyBtContinuesCurrentActionBeforeUtility()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2430 [function] `private static bool VerifyBtStopsCurrentActionBeforeUtility()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2507 [function] `private static bool VerifyBtDecisionTraceRecordsRouting()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2572 [function] `private static bool ContainsTrace(CharacterActor actor, string token)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2580 [function] `private static bool VerifyBehaviorTreeWrapperDebug()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2606 [function] `private static bool VerifyBehaviorTreeVisualGraph()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2649 [function] `private static bool VerifyBehaviorTreeRuntimeBranchSelection()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2689 [function] `private static bool VerifyMacroGoalsUseJobGiverCandidates()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2787 [function] `private static bool VerifyJobGiverInvalidReevaluationClearsCachedCandidate()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2852 [function] `private static bool HasVisualRootLayout(ParentTask root)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2882 [function] `private static bool HasSurvivalNeedsRoutine(ParentTask branch)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2893 [function] `private static bool HasDutyWorkRoutine(ParentTask branch)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2900 [function] `private static bool HasLeisureVisitRoutine(ParentTask branch)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2908 [function] `private static bool HasIdleRoutine(ParentTask branch)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2916 [function] `private static bool HasMacroBranchLayout(ParentTask branch)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2930 [function] `private static bool HasUtilityBranch(` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2941 [function] `private static bool HasJobGiverBranch(` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2952 [function] `private static bool HasAmbientIdleBranch(ParentTask branch, Vector2 branchOffset)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2958 [function] `private static bool HasMacroTaskBranch(` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2970 [function] `private static bool HasSelectorChildren(` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2981 [function] `private static bool HasActionBranch(` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L2996 [function] `private static bool HasCriticalBranchLayout(ParentTask branch)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3005 [function] `private static bool HasContinueCurrentBranchLayout(ParentTask branch)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3012 [function] `private static bool HasStopCurrentBranchLayout(ParentTask branch)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3019 [function] `private static bool HasMacroUtilityBranch(` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3034 [function] `private static bool HasMacroActionBranch(` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3053 [function] `private static bool HasNodeLayout(BehaviorDesigner.Runtime.Tasks.Task task, string name, Vector2 offset)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3062 [function] `private static bool HasNoLayoutOverlap(params BehaviorDesigner.Runtime.Tasks.Task[] roots)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3101 [function] `private static bool HasCompactLayoutBounds(` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3146 [function] `private static void CollectTasks(` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3167 [function] `private static string NodeName(BehaviorDesigner.Runtime.Tasks.Task task)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3179 [function] `private static string TaskDisplayName(BehaviorDesigner.Runtime.Tasks.Task task)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3191 [function] `private static bool VerifyGameManagerReusesSceneRuntime()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3225 [function] `private static bool VerifyRuntimeActorBehaviorTreeContract()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3262 [function] `private static bool VerifyDirectorContextCompression()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3288 [function] `private static bool VerifyDirectorPromptNumericContract()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3314 [function] `private static bool VerifyDirectorRoutineMacroTrigger()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3370 [function] `private static bool VerifyDirectorRejectedMacroRequestKeepsRetryWindowOpen()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3432 [function] `private static bool VerifySocialRejectedRequestDoesNotStartCooldown()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3503 [function] `private static bool VerifyLlmQueueDropPolicy()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3548 [function] `private static bool VerifyLlmQueueCapacityProtectsNonBubbleRequests()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3604 [function] `private static bool VerifyPersonaRequestLifecycle()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3636 [function] `private static bool VerifyBubbleNoFallback()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3664 [function] `private static bool VerifyPersonaRejectsInvalidJson()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L3691 [function] `private static bool VerifyVandalizeMacro()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Character/AI/Editor/CharacterAiPlanProbeActions.cs

- L1 [type] `internal sealed class ProbeExitDungeonActionSet : AIExitDungeon`
- L6 [function] `public override bool CanStart(CharacterActor actor)`
- L11 [function] `public override float AdjustScore(CharacterActor actor, float baseScore)`
- L16 [function] `public override void Execute(CharacterActor actor)`
- L26 [type] `internal sealed class ProbeEatActionSet : AIEat`
- L31 [function] `public override bool CanStart(CharacterActor actor)`
- L36 [function] `public override float AdjustScore(CharacterActor actor, float baseScore)`
- L41 [function] `public override bool TryResolveDestination(`
- L52 [function] `public override void Execute(CharacterActor actor)`
- L62 [type] `internal sealed class ProbeWorkActionSet : AIWork`
- L67 [function] `public override bool CanStart(CharacterActor actor)`
- L72 [function] `public override float AdjustScore(CharacterActor actor, float baseScore)`
- L77 [function] `public override void Execute(CharacterActor actor)`
- L87 [type] `internal sealed class ProbeShoppingActionSet : AIShopping`
- L92 [function] `public override bool CanStart(CharacterActor actor)`
- L97 [function] `public override float AdjustScore(CharacterActor actor, float baseScore)`
- L102 [function] `public override bool TryResolveDestination(`
- L113 [function] `public override void Execute(CharacterActor actor)`
- L123 [type] `internal sealed class ProbeLookAroundActionSet : AILookAround`
- L128 [function] `public override bool CanStart(CharacterActor actor)`
- L133 [function] `public override float AdjustScore(CharacterActor actor, float baseScore)`
- L138 [function] `public override void Execute(CharacterActor actor)`
- L148 [type] `internal sealed class ProbeWaitActionSet : AIWait`
- L153 [function] `public override bool CanStart(CharacterActor actor)`
- L158 [function] `public override float AdjustScore(CharacterActor actor, float baseScore)`
- L163 [function] `public override void Execute(CharacterActor actor)`
- L173 [type] `internal sealed class ProbeVolatileWorkActionSet : AIWork`
- L181 [function] `public override bool CanStart(CharacterActor actor)`
- L186 [function] `public override float AdjustScore(CharacterActor actor, float baseScore)`
- L194 [function] `public override void Execute(CharacterActor actor)`
- L204 [type] `internal sealed class ProbeContinuableWorkActionSet : AIWork`
- L215 [function] `public override bool CanStart(CharacterActor actor)`
- L220 [function] `public override float AdjustScore(CharacterActor actor, float baseScore)`
- L228 [function] `public override bool CanContinue(CharacterActor actor, AIAction runningAction, out string stopReason)`
- L234 [function] `public override bool CanInterrupt(CharacterActor actor, AIAction runningAction, out string interruptReason)`
- L240 [function] `public override void Execute(CharacterActor actor)`
- L254 [function] `public override void OnStop(CharacterActor actor, AIAction runningAction, string reason)`
- L261 [type] `internal sealed class ProbeOneShotWorkActionSet : AIWork`
- L266 [function] `public override bool CanStart(CharacterActor actor)`
- L271 [function] `public override float AdjustScore(CharacterActor actor, float baseScore)`
- L276 [function] `public override void Execute(CharacterActor actor)`

### Assets/Scripts/Character/AI/Editor/CharacterAiPlanTestScenePreparer.cs

- L8 [type] `internal static class CharacterAiPlanTestScenePreparer` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L10 [function] `public static string Prepare()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L24 [function] `private static int EnsureCharacterPrefabs()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L62 [function] `private static string CopySampleScene()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L81 [function] `private static int EnsureSceneCharacters(Scene scene)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L99 [function] `private static bool EnsureCharacterAiComponents(GameObject target)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L137 [function] `private static void EnsureSceneMarker(Scene scene)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L160 [function] `private static bool EnsureComponent<T>(GameObject target)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Character/AI/Editor/CharacterAiPriorityCornerCaseDebugScenarios.cs

- L10 [type] `public static class CharacterAiPriorityCornerCaseDebugScenarios` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L13 [function] `public static void RunFromMenu()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L22 [function] `public static bool RunAll(bool logSuccess)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L26 [function] `RunScenario("AI action score edge cases", VerifyActionScoreEdgeCases, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L27 [function] `RunScenario("AI selects next action after failed high-score destination", VerifyBrainSelectsNextActionAfterDestinationFailure, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L28 [function] `RunScenario("AI tie keeps action order", VerifyBrainTieKeepsActionOrder, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L29 [function] `RunScenario("Off priority excludes urgent automatic work", VerifyOffPriorityExcludesUrgentAutomaticWork, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L30 [function] `RunScenario("Direct command bypasses Off through assignment", VerifyDirectCommandBypassesOffThroughAssignment, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L31 [function] `RunScenario("Requested work type does not substitute", VerifyRequestedWorkTypeDoesNotSubstitute, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L32 [function] `RunScenario("Invalid priority target clears and resumes automatic work", VerifyInvalidPriorityTargetClearsAndResumesAutomaticWork, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L33 [function] `RunScenario("Nearest equivalent work target wins", VerifyNearestEquivalentWorkTargetWins, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L34 [function] `RunScenario("Priority level beats lower urgent work", VerifyPriorityLevelBeatsLowerUrgentWork, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L35 [function] `RunScenario("Combined priority profile edges", VerifyCombinedPriorityProfileEdges, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L36 [function] `RunScenario("AI personality modifies action score", VerifyPersonalityModifierAffectsActionScore, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L37 [function] `RunScenario("Occupied work target is classified", VerifyOccupiedWorkTargetFailureClassification, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L38 [function] `RunScenario("Work and wait scores prefer real work", VerifyWorkAndWaitScoresPreferRealWork, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L39 [function] `RunScenario("No work target uses explicit wait", VerifyNoWorkTargetUsesExplicitWait, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L59 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L73 [function] `private static bool VerifyActionScoreEdgeCases()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L86 [function] `SetConsiderations(actionSet)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L89 [function] `SetConsiderations(actionSet, one, null)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L94 [function] `SetConsiderations(actionSet, one, zero)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L99 [function] `SetConsiderations(actionSet, overMax, half)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L127 [function] `private static bool VerifyBrainSelectsNextActionAfterDestinationFailure()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L155 [function] `private static bool VerifyBrainTieKeepsActionOrder()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L182 [function] `private static bool VerifyOffPriorityExcludesUrgentAutomaticWork()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L189 [function] `SetOnly(work)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L198 [function] `private static bool VerifyDirectCommandBypassesOffThroughAssignment()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L205 [function] `SetOnly(work)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L216 [function] `private static bool VerifyRequestedWorkTypeDoesNotSubstitute()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L222 [function] `SetOnly(work, FacilityWorkType.Operate, FacilityWorkType.Repair)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L231 [function] `private static bool VerifyInvalidPriorityTargetClearsAndResumesAutomaticWork()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L241 [function] `SetOnly(work, FacilityWorkType.Repair)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L255 [function] `private static bool VerifyNearestEquivalentWorkTargetWins()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L265 [function] `SetOnly(work, FacilityWorkType.Repair)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L273 [function] `private static bool VerifyPriorityLevelBeatsLowerUrgentWork()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L280 [function] `ClearShopStock(restockShop)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L284 [function] `SetAllOff(work)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L295 [function] `private static bool VerifyCombinedPriorityProfileEdges()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L316 [function] `private static bool VerifyPersonalityModifierAffectsActionScore()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L338 [function] `private static bool VerifyOccupiedWorkTargetFailureClassification()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L363 [function] `private static bool VerifyWorkAndWaitScoresPreferRealWork()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L392 [function] `private static bool VerifyNoWorkTargetUsesExplicitWait()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L434 [function] `SetConsiderations(action, consideration)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L438 [function] `private static void SetOnly(AbilityWork work, params FacilityWorkType[] enabledTypes)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L440 [function] `SetAllOff(work)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L447 [function] `private static void SetAllOff(AbilityWork work)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L455 [function] `private static void ClearShopStock(BuildableObject building)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L462 [function] `private static void SetConsiderations(AIActionSet actionSet, params Consideration[] considerations)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L470 [function] `private static float ExpectedWeightedScore(params float[] scores)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L492 [function] `private static bool NearlyEqual(float a, float b)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L497 [function] `private static bool IsAdjacentWalkPath(Queue<GridMoveStep> path)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L531 [type] `private sealed class FixedScoreConsideration : Consideration` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L535 [function] `public override float ScoreConsideration(CharacterActor actor)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L541 [type] `private sealed class TestActionSet : AIActionSet` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L549 [function] `public override void Execute(CharacterActor actor)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L576 [function] `private void OnDestroy()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L585 [type] `private sealed class PriorityScenarioWorld : IDisposable` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L600 [function] `public PriorityScenarioWorld(int width = 16)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L609 [function] `new TestHallwayOccupant()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L624 [function] `public BuildableObject Place(string assetName, Vector2Int position)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L656 [function] `public CharacterActor CreateOwner(string ownerAssetName, Vector2Int position)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L683 [function] `public void Dispose()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L696 [type] `private sealed class TestHallwayOccupant : IGridOccupant` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Character/AI/Editor/CharacterAiStressDebugScenarios.cs

- L13 [type] `public static class CharacterAiStressDebugScenarios` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L35 [function] `private static void InitializePlayModeProfiler()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L41 [function] `public static void RunFromMenu()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L51 [function] `public static void RunScaleSuiteFromMenu()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L61 [function] `public static void RunPlayModeProfileFromMenu()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L63 [function] `StartPlayModeProfile(NpcCount, 0, 600, true)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L75 [function] `public static void PumpPlayModeProfileFrames(int maxFrames = 600)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L80 [function] `public static bool RunAll(bool logSuccess)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L85 [function] `public static bool RunScaleSuite(bool logSuccess)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L104 [function] `public static bool RunForCount(int npcCount, bool logSuccess)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L190 [type] `private sealed class PlayModeProfileSession` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L245 [function] `public static void Initialize()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L255 [function] `EnsureCurrent().BeginIfNeeded()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L294 [function] `private static PlayModeProfileSession EnsureCurrent()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L309 [function] `private static void OnPlayModeStateChanged(PlayModeStateChange state)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L323 [function] `EnsureCurrent().BeginIfNeeded()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L334 [function] `private static void OnEditorUpdate()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L342 [function] `EnsureCurrent().Tick()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L345 [function] `public static void PumpFrames(int maxFrames)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L376 [function] `private void BeginIfNeeded()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L383 [function] `CaptureProfilerState()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L394 [function] `StartRecorders()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L402 [function] `private void Tick()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L404 [function] `BeginIfNeeded()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L405 [function] `SampleCurrentFrame()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L408 [function] `private void SampleCurrentFrame()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L469 [function] `Complete()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L473 [function] `private void Complete()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L526 [function] `Percentile(frameTimesMs, 0.95)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L529 [function] `Percentile(schedulerTimesMs, 0.95)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L536 [function] `Cleanup()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L544 [function] `private void Abort(string reason)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L547 [function] `Cleanup()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L553 [function] `private void CaptureProfilerState()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L570 [function] `private void StartRecorders()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L576 [function] `private void Cleanup()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L604 [function] `private static double Percentile(List<double> values, double percentile)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L667 [function] `private static string EscapeJson(string value)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L675 [type] `private sealed class StressWorld : IDisposable` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L690 [function] `public StressWorld()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L700 [function] `new TestHallwayOccupant()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L707 [function] `RegisterStressStair(0)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L708 [function] `RegisterStressStair(Grid.width - 1)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L726 [function] `SetPrivateField(Scheduler, "characterAiExternalBehavior", externalBehavior)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L727 [function] `SetPrivateField(Scheduler, "maxDecisionsPerFrame", DecisionBudget)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L728 [function] `SetPrivateField(Scheduler, "maxPathSearchesPerFrame", PathBudget)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L729 [function] `SetPrivateField(Scheduler, "visibleDecisionInterval", 0.01f)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L730 [function] `SetPrivateField(Scheduler, "offscreenDecisionInterval", 0.01f)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L731 [function] `SetPrivateField(Scheduler, "ownerDecisionInterval", 0.01f)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L732 [function] `SetPrivateField(Scheduler, "retryDelay", 0.01f)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L739 [function] `public void PlaceFacilities()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L756 [function] `Place(assetNames[i % assetNames.Length], new Vector2Int(x, floor))` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L761 [function] `public void CreateCustomers(int count)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L768 [function] `GetCustomerPosition(i)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L778 [function] `private void RegisterStressStair(int x)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L789 [function] `private Vector2Int GetCustomerPosition(int index)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L805 [function] `public void Dispose()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L817 [function] `DestroyRuntimeAware(obj)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L822 [function] `DestroyRuntimeAware(obj)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L826 [function] `private static void DestroyRuntimeAware(Object obj)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L843 [function] `private BuildableObject Place(string assetName, Vector2Int position)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L898 [function] `SetPrivateField(data, "frequencyVisitMin", 3)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L899 [function] `SetPrivateField(data, "frequencyVisitMax", 3)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L900 [function] `SetPrivateField(data, "minHoldingMoney", 500)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L901 [function] `SetPrivateField(data, "maxHoldingMoney", 600)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L906 [function] `ApplyStressPersona(obj.GetComponent<CustomerPersonaRuntime>(), speciesTag)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L917 [function] `private static void ApplyStressPersona(CustomerPersonaRuntime personaRuntime, string speciesTag)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L939 [function] `private static void SetPrivateField(object target, string fieldName, object value)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L946 [type] `private sealed class TestHallwayOccupant : IGridOccupant` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L954 [type] `private sealed class TestStairOccupant : IGridOccupant, IGridMovementOccupant` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Character/AI/Editor/CharacterFeedbackDebugScenarios.cs

- L5 [type] `public static class CharacterFeedbackDebugScenarios` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L8 [function] `public static void RunFromMenu()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L17 [function] `public static bool RunAll(bool logSuccess)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L21 [function] `RunScenario("?汝??吏?????곌떽?깆쓦?? ?熬곣뫖利???????썹땟??, VerifyLogTagsAndRepeatCount, errors)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L22 [function] `RunScenario("?????곗? ????븐뻤?????곗뒩泳?봺異?, VerifyFeedbackBubbleStateClassification, errors)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L23 [function] `RunScenario("??????汝??吏??????, VerifySummaryLogFormatting, errors)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L43 [function] `private static void RunScenario(string name, System.Func<bool> scenario, List<string> errors)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L50 [function] `private static bool VerifyLogTagsAndRepeatCount()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L75 [function] `private static bool VerifyFeedbackBubbleStateClassification()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L101 [function] `private static bool VerifySummaryLogFormatting()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L118 [function] `private static CharacterActor CreateCharacter(string name)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Character/AI/Editor/CharacterModelDebugScenarios.cs

- L9 [type] `public static class CharacterModelDebugScenarios` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L12 [function] `public static void RunFromMenu()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L21 [function] `public static bool RunAll(bool logSuccess)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L25 [function] `RunScenario("????띻샵???????????????, VerifyAssetCounts, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L26 [function] `RunScenario("?逆???ｋ굜?????꾩룆???, VerifyStatComposition, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L27 [function] `RunScenario("??醫딆┻????????????????????⑤슢????, VerifyTraitConsumptionAndAccidentDifferences, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L28 [function] `RunScenario("??????????노듋????⑤슢????, VerifyWorkAffinityDifferences, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L29 [function] `RunScenario("????????꾣뤃?????", VerifyRoleSwitchKeepsProfile, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L30 [function] `RunScenario("Character ?????????썹땟怨⒲뀋??????쇰뮚??, VerifyCharacterRuntimeProfile, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L31 [function] `RunScenario("????띻샵??????ㅳ늾?온 ?????????, VerifySpeciesOperationalData, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L32 [function] `RunScenario("????띻샵???꿔꺂???癲?????꾣뤃???????꿔꺂?볟젆怨곷븶??, VerifySpeciesRuntimeTendencies, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L33 [function] `RunScenario("????띻샵????濚밸Ŧ?길쾮????붺몭??沅걔??, VerifySpeciesCrowdSensitivity, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L53 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L67 [function] `private static bool VerifyAssetCounts()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L74 [function] `private static bool VerifyStatComposition()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L87 [function] `private static bool VerifyTraitConsumptionAndAccidentDifferences()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L98 [function] `private static bool VerifyWorkAffinityDifferences()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L109 [function] `private static bool VerifyRoleSwitchKeepsProfile()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L125 [function] `private static bool VerifyCharacterRuntimeProfile()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L146 [function] `private static bool VerifySpeciesOperationalData()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L169 [function] `private static bool VerifySpeciesRuntimeTendencies()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L185 [function] `private static bool VerifySpeciesCrowdSensitivity()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L210 [function] `private static CharacterRuntimeProfile CreateProfile(string speciesAssetName, params string[] traitAssetNames)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L218 [function] `private static CharacterSO CreateCharacterData(string speciesAssetName, params string[] traitAssetNames)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L233 [function] `private static CharacterSpeciesSO LoadSpecies(string assetName)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L239 [function] `private static CharacterTraitSO LoadTrait(string assetName)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Character/AI/Editor/CustomerAiDebugScenarios.cs

- L9 [type] `public static class CustomerAiDebugScenarios` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L12 [function] `public static void RunFromMenu()` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L21 [function] `public static bool RunAll(bool logSuccess)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L24 [function] `RunScenario("Action score compensation", VerifyActionScoreCompensation, errors)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L25 [function] `RunScenario("Toilet and hygiene facility recovers needs", VerifyToiletAndHygieneFacilityRecovery, errors)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L26 [function] `RunScenario("?꿔꺂??袁ㅻ븶????? ????紐꾪닓 ????ㅻ깹壤?? no action ?꿔꺂??節뉖き??, VerifyUnavailableDestinationActionsReportNoAction, errors)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L27 [function] `RunScenario("????????紐꾪닓 ???쒙쭫??????ㅻ깹壤?????????????ㅻ깹壤?????ｋ??, VerifyUnavailableHighScoreNeedSelectsReachableAction, errors)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L29 [function] `RunScenario("????怨ㅽ렫??????썹땟?雅?????꾣뤃?饔낃퀣??, VerifyRoleCandidateFiltering, errors)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L30 [function] `RunScenario("?癲????????琉우º???쎛 ????ㅻ깹壤????????얠???嶺뚮??▼퐲???嚥▲굧????, VerifyNeedScoresDriveActionPriority, errors)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L31 [function] `RunScenario("????띻샵??????ъ군濚밸Ŧ?쏙쭔???쎛 ?꿸쑨??????????????????얠? ??醫딆쓧???, VerifySpeciesPreferenceCanBeatDistance, errors)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L32 [function] `RunScenario("?????????????꿔꺂???熬곻퐢夷?????쇰뮛??????ъ군濚?, VerifyVampireSelectsManaOrResearch, errors)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L33 [function] `RunScenario("????ㅼ굡獒????곗뵯?? ??嶺?筌???嶺뚮????, VerifyUnavailableFacilitiesAreExcluded, errors)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L34 [function] `RunScenario("Unstaffed shop allows self-service checkout", VerifyUnstaffedShopAllowsSelfServiceCheckout, errors)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L35 [function] `RunScenario("?????낇뀘???????쇨덫?嚥▲룂?????繹먮겧嫄х솾??熬곣뫖?삥납??좂뙴?????띻샴癲?, VerifyUnaffordableShopEndsVisitCycle, errors)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L36 [function] `RunScenario("?熬곣뫖?삥납??좂뙴??????썹땟??AI ????ㅻ깹壤????嶺???⑤슢?????, VerifyVisitorActionsAreCompletedFromPartialPrefab, errors)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L37 [function] `RunScenario("????ャ렑?????濚밸Ŧ?길쾮????????熬곣뫖利???, VerifyNoveltyAndCrowdScores, errors)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L57 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L71 [function] `private static bool VerifyRoleCandidateFiltering()` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L112 [function] `private static bool VerifyActionScoreCompensation()` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L127 [function] `SetConsiderations(actionSet, highA, highB)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L130 [function] `NearlyEqual(highScore, ExpectedWeightedScore(0.9f, 0.9f))` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L135 [function] `SetConsiderations(actionSet, overMax, half)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L138 [function] `NearlyEqual(clampedScore, ExpectedWeightedScore(1f, 0.5f))` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L143 [function] `SetConsiderations(actionSet, midA, midB, midC)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L146 [function] `NearlyEqual(threeScore, ExpectedWeightedScore(0.5f, 0.5f, 0.5f))` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L165 [function] `private static bool VerifyNeedScoresDriveActionPriority()` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L189 [function] `SetStats(customer, 90f, 90f, 10f, 20f)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L193 [function] `SetStats(customer, 90f, 10f, 90f, 20f)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L197 [function] `SetStats(customer, 90f, 90f, 90f, 90f, 5f, 90f)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L201 [function] `SetStats(customer, 90f, 90f, 90f, 90f, 90f, 5f)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L217 [function] `private static bool VerifySpeciesPreferenceCanBeatDistance()` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L235 [function] `private static bool VerifyToiletAndHygieneFacilityRecovery()` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L245 [function] `SetStats(customer, 90f, 90f, 90f, 90f, 5f, 10f)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L267 [function] `private static bool VerifyVampireSelectsManaOrResearch()` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L282 [function] `private static bool VerifyUnavailableFacilitiesAreExcluded()` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L292 [function] `ClearShopStock(lowFood)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L294 [function] `SetHoldingMoney(customer, 0)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L313 [function] `private static bool VerifySelfServiceCheckoutWithoutWorker()` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L348 [function] `private static bool VerifyUnstaffedShopAllowsSelfServiceCheckout()` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L397 [function] `private static bool VerifyUnaffordableShopEndsVisitCycle()` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L405 [function] `SetHoldingMoney(customer, 0)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L422 [function] `private static bool VerifyVisitorActionsAreCompletedFromPartialPrefab()` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L456 [function] `private static bool VerifyUnavailableDestinationActionsReportNoAction()` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L490 [function] `DestroyScriptableObjects(owned)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L494 [function] `private static bool VerifyUnavailableHighScoreNeedSelectsReachableAction()` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L522 [function] `DestroyScriptableObjects(owned)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L526 [function] `private static bool VerifyNoveltyAndCrowdScores()` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L564 [function] `private static ConsiderationFacilityNeed CreateNeed(FacilityRole role)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L571 [function] `private static void SetStats(CharacterActor character, float hunger, float sleep, float fun, float mood)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L573 [function] `SetStats(character, hunger, sleep, fun, mood, 100f, 100f)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L593 [function] `private static void SetHoldingMoney(CharacterActor character, int money)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L600 [function] `private static void ClearShopStock(BuildableObject building)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L606 [function] `private static void SetConsiderations(AIActionSet actionSet, params Consideration[] considerations)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L620 [function] `SetConsiderations(action, consideration)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L626 [function] `private static void DestroyScriptableObjects(List<ScriptableObject> objects)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L637 [function] `private static float ExpectedWeightedScore(params float[] scores)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L659 [function] `private static bool NearlyEqual(float a, float b)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L664 [type] `private sealed class FixedScoreConsideration : Consideration` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L668 [function] `public override float ScoreConsideration(CharacterActor actor)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L674 [type] `private sealed class CustomerAiScenarioWorld : IDisposable` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L693 [function] `public CustomerAiScenarioWorld()` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L702 [function] `new TestHallwayOccupant()` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L718 [function] `public BuildableObject Place(string assetName, Vector2Int position)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L749 [function] `PlaceRoomDoorsFor(building)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L755 [function] `public void PlaceRoomDoorsFor(BuildableObject building)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L765 [function] `PlaceRuntimeDoor(new Vector2Int(minX - 1, y))` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L766 [function] `PlaceRuntimeDoor(new Vector2Int(maxX + 1, y))` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L769 [function] `private BuildableObject PlaceRuntimeDoor(Vector2Int position)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L833 [function] `SetPrivateField(data, "frequencyVisitMin", 3)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L834 [function] `SetPrivateField(data, "frequencyVisitMax", 3)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L835 [function] `SetPrivateField(data, "minHoldingMoney", 500)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L836 [function] `SetPrivateField(data, "maxHoldingMoney", 600)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L844 [function] `SetStats(character, hunger, sleep, fun, mood)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L848 [function] `public void StaffShop(BuildableObject building)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L860 [function] `private CharacterActor CreateStaff(string name, Vector2Int position)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L883 [function] `public void Dispose()` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L898 [function] `private static void SetPrivateField(object target, string fieldName, object value)` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L905 [type] `private sealed class TestHallwayOccupant : IGridOccupant` [SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Character/AI/Editor/FacilityCandidateCacheEditorFacade.cs

- L3 [type] `public static class FacilityCandidateCache` [EditorAssetAccess, DependencyInjection]
- L7 [function] `public static IReadOnlyList<BuildableObject> GetCandidates(Grid grid, FacilityRole role)` [EditorAssetAccess, DependencyInjection]
- L12 [function] `public static void MarkDynamicStateDirty()` [EditorAssetAccess, DependencyInjection]
- L17 [function] `public static void Clear()` [EditorAssetAccess, DependencyInjection]

### Assets/Scripts/Character/AI/Editor/OwnerDebugScenarios.cs

- L9 [type] `public static class OwnerDebugScenarios` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L12 [function] `public static void RunFromMenu()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L21 [function] `public static bool RunAll(bool logSuccess)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L25 [function] `RunScenario("????????썹땟?雅??????, VerifyOwnerCandidateAssets, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L26 [function] `RunScenario("?????????????????쇰뮚??, VerifyOwnerRuntimeRole, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L27 [function] `RunScenario("???????嶺????????????Β?, VerifyOwnerAiActions, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L28 [function] `RunScenario("??????傭???????띻샴癲?, VerifyOwnerDeathEndsRun, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L29 [function] `RunScenario("???????????얠? ???????꿔꺂?????, VerifyOwnerPriorityWork, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L49 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L63 [function] `private static bool VerifyOwnerCandidateAssets()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L81 [function] `private static bool VerifyOwnerRuntimeRole()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L87 [function] `InitializeCharacter(character, ownerData)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L100 [function] `private static bool VerifyOwnerAiActions()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L106 [function] `InitializeCharacter(character, ownerData)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L116 [function] `private static bool VerifyOwnerDeathEndsRun()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L141 [function] `private static bool VerifyOwnerPriorityWork()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L146 [function] `InitializeCharacter(character, ownerData)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L170 [function] `private static GameObject CreateCharacterObject(string name)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L181 [function] `private static void InitializeCharacter(CharacterActor character, CharacterSO data)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L192 [function] `private static CharacterSO[] LoadOwners()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L202 [function] `private static CharacterSO LoadOwner(string assetName)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Character/AI/Editor/PriorityCommandDebugScenarios.cs

- L9 [type] `public static class PriorityCommandDebugScenarios` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L12 [function] `public static void RunFromMenu()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L21 [function] `public static bool RunAll(bool logSuccess)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L25 [function] `RunScenario("????ｋ????????????븐뻤??, VerifySelectionState, errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L26 [function] `RunScenario("???? ??嶺?筌????β뼯?蹂λ뤀 ?꿔꺂?????, VerifyDamagedFacilityResolvesRepair, errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L27 [function] `RunScenario("???????낇뀘?????嶺?筌???⑤슢?????꿔꺂?????, VerifyEmptyStockResolvesRestock, errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L28 [function] `RunScenario("????쇰뮛????嶺?筌?????쇰뮛???꿔꺂?????, VerifyResearchFacilityResolvesResearch, errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L29 [function] `RunScenario("???????얠? ?꿔꺂?????? ???Β?ル빢傭????????얠???嶺뚮??▼퐲????關履??, VerifyDirectCommandBypassesOffPriority, errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L30 [function] `RunScenario("????썹땟??????곗뵯?? ?????곌숯 ?汝??吏??, VerifyUnreachableCommandFails, errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L31 [function] `RunScenario("??⑤㈇?뚧납?????嶺?藥????????얠? ?꿔꺂?????, VerifySuppressPriorityCommand, errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L51 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L65 [function] `private static bool VerifySelectionState()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L79 [function] `private static bool VerifyDamagedFacilityResolvesRepair()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L93 [function] `private static bool VerifyEmptyStockResolvesRestock()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L98 [function] `ClearShopStock(shop)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L107 [function] `private static bool VerifyResearchFacilityResolvesResearch()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L120 [function] `private static bool VerifyDirectCommandBypassesOffPriority()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L127 [function] `ClearShopStock(shop)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L139 [function] `private static bool VerifyUnreachableCommandFails()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L155 [function] `private static bool VerifySuppressPriorityCommand()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L180 [function] `private static CharacterActor CreateCharacter(string ownerAssetName)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L194 [function] `InvokeAwake(character)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L201 [function] `private static CharacterActor CreateIntruder(string intruderAssetName, Vector3 position)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L212 [function] `InvokeAwake(character)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L218 [function] `private static void InvokeAwake(CharacterActor character)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L225 [function] `private static void ClearShopStock(BuildableObject building)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L234 [type] `private sealed class CommandScenarioWorld : IDisposable` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L238 [function] `public CommandScenarioWorld(int width = 12, int hallwayCount = 12)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L244 [function] `new TestHallwayOccupant()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L253 [function] `public BuildableObject Place(string assetName, Vector2Int position)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L270 [function] `public void Dispose()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L279 [type] `private sealed class TestHallwayOccupant : IGridOccupant` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Character/AI/Editor/StaffDiscontentDebugScenarios.cs

- L8 [type] `public static class StaffDiscontentDebugScenarios` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L11 [function] `public static void RunFromMenu()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L20 [function] `public static bool RunAll(bool logSuccess)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L24 [function] `RunScenario("?꿔꺂??????????????븐뻤??, VerifyLowSatisfactionStage, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L25 [function] `RunScenario("1??壤굿????????숈춻?????, VerifyEfficiencyDropMultiplier, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L26 [function] `RunScenario("2??壤굿??????嶺??????????꿔꺂?볟젆怨곷븶???, VerifyWorkDisruptionBlocksWork, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L27 [function] `RunScenario("3??壤굿???????ш낄猷?????????????, VerifyDeparturePermanentLoss, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L28 [function] `RunScenario("4??壤굿??????? ?熬곣뫖利??", VerifyLocalRebellionPermanentLoss, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L29 [function] `RunScenario("?熬곣뫖利?? ?轝꿸섣????熬곣뫖?삥납??????????꾣뤃?琉?, VerifyOwnerThreatEscalation, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L49 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L63 [function] `private static bool VerifyLowSatisfactionStage()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L74 [function] `DestroyStaff(staff)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L78 [function] `private static bool VerifyEfficiencyDropMultiplier()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L91 [function] `DestroyStaff(staff)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L95 [function] `private static bool VerifyWorkDisruptionBlocksWork()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L109 [function] `DestroyStaff(staff)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L113 [function] `private static bool VerifyDeparturePermanentLoss()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L126 [function] `DestroyStaff(staff)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L130 [function] `private static bool VerifyLocalRebellionPermanentLoss()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L143 [function] `DestroyStaff(staff)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L147 [function] `private static bool VerifyOwnerThreatEscalation()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L159 [function] `DestroyStaff(staff)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L163 [function] `private static CharacterActor CreateStaff(int id, string name, float mood)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L195 [function] `private static void DestroyStaff(CharacterActor staff)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L207 [type] `private sealed class ScenarioRuntime : IDisposable` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L211 [function] `public ScenarioRuntime()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L219 [function] `public void Dispose()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Character/AI/Editor/StaffDutyDebugScenarios.cs

- L9 [type] `public static class StaffDutyDebugScenarios` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L12 [function] `public static void RunFromMenu()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L21 [function] `public static bool RunAll(bool logSuccess)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L25 [function] `RunScenario("?轅붽틓??????逆???⑸걦????????????ㅼ뒧????, VerifyWorkerInitialCondition, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L26 [function] `RunScenario("????嶺뚮??껎슙 ???????????????????袁ｋ쨨??, VerifyWorkFatigueTriggersOffDuty, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L27 [function] `RunScenario("Critical fatigue enters off-duty instead of stuck rest protection", VerifyCriticalFatigueEntersOffDuty, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L28 [function] `RunScenario("????????ш끽維??λ궔??醫귣쇀????????癲ル슢??節녿쨨?, VerifyOffDutyVisitCycle, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L29 [function] `RunScenario("?轅붽틓?????AI ???????????산뭐鶯????ㅼ뒧?????, VerifyStaffBrainAddsOffDutyActions, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L30 [function] `RunScenario("??????轅붽틓??????????????癲ル슢????, VerifyOnDutyStaffDoesNotUseLookAround, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L31 [function] `RunScenario("????AI ??ш끽維??λ궔??醫귣쇀??????산뭐鶯???癲ル슢????, VerifyOwnerBrainDoesNotAddVisitorActions, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L32 [function] `RunScenario("???????轅붽틓??????轅붽틓???????癲ル슢????, VerifyOffDutyStaffDoesNotCreateRevenue, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L33 [function] `RunScenario("???沅걔?????????????????μ떝?띄몭??袁㏉떄??, VerifyEmergencyCanInterruptOffDuty, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L35 [function] `RunScenario("???嶺???쒖녇??щ뮛???????癲ル슢??????轅붽틓????????????????袁ｋ쨨???? ?????ㅿ폎??, VerifyHungerDoesNotForceOffDuty, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L36 [function] `RunScenario("???????????????????嶺??????????? ?????ㅿ폎??, VerifyWaitDoesNotRecoverHunger, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L37 [function] `RunScenario("??????????轅붽틓?????????蹂κ텥????ш끽維뽳쭩?????β뼯援????る쑏?, VerifyOnDutyWaitCanWander, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L38 [function] `RunScenario("On-duty wait selects dungeon wander", VerifyOnDutyWaitSelectsDungeonWander, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L39 [function] `RunScenario("Idle wander can use valid stairs", VerifyIdleWanderCanUseValidStairs, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L40 [function] `RunScenario("Off-duty wait can wander", VerifyOffDutyWaitCanWander, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L41 [function] `RunScenario("Occupied work wait uses dungeon wander", VerifyOccupiedWorkWaitUsesDungeonWander, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L42 [function] `RunScenario("Customer ?????轅붽틓???????轅붽틓?????AI ???????????⑤뜪??, VerifyCustomerTypedWorkerUsesStaffRules, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L43 [function] `RunScenario("Expedition return wakes staff work decision", VerifyExpeditionReturnWakesWorkDecision, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L63 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L77 [function] `private static bool VerifyWorkerInitialCondition()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L91 [function] `private static bool VerifyWorkFatigueTriggersOffDuty()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L109 [function] `private static bool VerifyCriticalFatigueEntersOffDuty()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L125 [function] `private static bool VerifyOffDutyVisitCycle()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L144 [function] `private static bool VerifyStaffBrainAddsOffDutyActions()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L163 [function] `private static bool VerifyOnDutyStaffDoesNotUseLookAround()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L182 [function] `private static bool VerifyOwnerBrainDoesNotAddVisitorActions()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L201 [function] `private static bool VerifyOffDutyStaffDoesNotCreateRevenue()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L219 [function] `private static bool VerifyEmergencyCanInterruptOffDuty()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L244 [function] `private static bool VerifyHungerDoesNotForceOffDuty()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L261 [function] `private static bool VerifyWaitDoesNotRecoverHunger()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L286 [function] `private static bool VerifyOnDutyWaitCanWander()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L307 [function] `new TestHallwayOccupant()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L360 [function] `private static bool VerifyOnDutyWaitSelectsDungeonWander()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L381 [function] `new TestHallwayOccupant()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L435 [function] `private static bool VerifyIdleWanderCanUseValidStairs()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L454 [function] `new TestHallwayOccupant()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L460 [function] `new TestStairOccupant()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L511 [function] `private static bool VerifyOffDutyWaitCanWander()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L533 [function] `new TestHallwayOccupant()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L596 [function] `private static bool VerifyOccupiedWorkWaitUsesDungeonWander()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L620 [function] `new TestHallwayOccupant()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L679 [function] `private static bool VerifyCustomerTypedWorkerUsesStaffRules()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L712 [function] `private static bool VerifyExpeditionReturnWakesWorkDecision()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L817 [function] `private static CharacterActor CreateOwner(string ownerAssetName)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L833 [function] `private static CharacterActor CreateCustomer()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L848 [function] `private static CharacterActor InitializeCharacterObject(GameObject obj, CharacterSO data)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L860 [function] `private static bool IsAdjacentWalkPath(Queue<GridMoveStep> path)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L894 [function] `private static BuildableObject CreateDummyBuilding()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L900 [type] `private sealed class WorkScenarioWorld : IDisposable` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L904 [function] `public WorkScenarioWorld()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L910 [function] `new TestHallwayOccupant()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L919 [function] `public BuildableObject Place(string assetName, Vector2Int position)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L936 [function] `public void Dispose()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L945 [type] `private sealed class TestHallwayOccupant : IGridOccupant` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L953 [type] `private sealed class TestStairOccupant : IGridOccupant, IGridMovementOccupant, IGridMovementHandler` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L961 [function] `public System.Collections.IEnumerator Traverse(CharacterActor actor, GridMoveStep step)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Character/AI/Editor/StaffRebellionResponseDebugScenarios.cs

- L9 [type] `public static class StaffRebellionResponseDebugScenarios` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L12 [function] `public static void RunFromMenu()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L21 [function] `public static bool RunAll(bool logSuccess)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L25 [function] `RunScenario("???嶺???嶺?藥??熬곣뫖利???, VerifyAutoSuppressAssignment, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L26 [function] `RunScenario("?熬곣뫖利?? ?꿔꺂???????嶺?藥??꿔꺂??琉몃쨨???????, VerifyRebelSuppressCommandTarget, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L27 [function] `RunScenario("?嚥▲굥?멩납????틕??????????꾣뤃?琉??癲ル슢캉????꿔꺂?볟젆怨곷븶???, VerifyIsolationBlocksOwnerThreat, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L28 [function] `RunScenario("?熬곣뫖利?? ?꿔꺂??????꿔꺂?????, VerifyCalmBeforeRebellion, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L29 [function] `RunScenario("??嶺?藥?????썹땟????????????ㅼ굣??, VerifySuppressedRebelClearsThreat, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L49 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L63 [function] `private static bool VerifyAutoSuppressAssignment()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L79 [function] `private static bool VerifyRebelSuppressCommandTarget()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L90 [function] `private static bool VerifyIsolationBlocksOwnerThreat()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L109 [function] `private static bool VerifyCalmBeforeRebellion()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L127 [function] `private static bool VerifySuppressedRebelClearsThreat()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L144 [type] `private sealed class RebellionScenarioWorld : IDisposable` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L157 [function] `public RebellionScenarioWorld()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L174 [function] `PlaceHallway(new Vector2Int(x, 0))` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L181 [function] `public CharacterActor CreateStaff(int id, string name, Vector2Int position, float mood)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L214 [function] `public void Dispose()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L228 [function] `private void PlaceHallway(Vector2Int position)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Character/AI/Editor/WorkPriorityDebugScenarios.cs

- L10 [type] `public static class WorkPriorityDebugScenarios` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L13 [function] `public static void RunFromMenu()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L22 [function] `public static bool RunAll(bool logSuccess)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L25 [function] `RunScenario("Urgent work overrides rest protection", VerifyUrgentWorkOverridesRestProtection, errors)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L26 [function] `RunScenario("Rest protection has hysteresis", VerifyRestProtectionHysteresis, errors)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L28 [function] `RunScenario("???????얠???嶺뚮??▼퐲????뚯????嶺?, VerifyPriorityDefaults, errors)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L29 [function] `RunScenario("???Β?ル빢傭?????????嶺뚮????, VerifyDisabledWorkIsExcluded, errors)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L30 [function] `RunScenario("????띻샵???逆???????쎈뼔?????????꿔꺂?볟젆怨곷븶??, VerifySpeciesWorkPreferenceChangesTarget, errors)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L31 [function] `RunScenario("???? ??嶺?筌????궰?????, VerifyDamagedFacilityUrgency, errors)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L32 [function] `RunScenario("Undamaged repair is excluded", VerifyUndamagedRepairIsExcluded, errors)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L33 [function] `RunScenario("?꿔꺂?????????????ㅼ굡?類㎮뵾???⑤슢????????????嶺뚮????, VerifyRestockWithoutWarehouseSupplyIsExcluded, errors)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L34 [function] `RunScenario("????Β????⑤슢????, VerifyFatigueProtection, errors)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L35 [function] `RunScenario("???????얠???嶺뚮??▼퐲?UI ????썼キ?κ괌??, VerifyPriorityPanelPrefab, errors)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L36 [function] `RunScenario("?濚밸Ŧ?????嶺?筌????????얠???嶺뚮??▼퐲?UI ?꿔꺂??????⑸걦????, VerifyPriorityPanelBuildsMatrix, errors)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L38 [function] `RunScenario("???????꿔꺂?????? AI bestAction ???됰Ŧ???濚밸Ŧ?????꿔꺂?ｉ뜮戮녹춹??????띻샴癲??? ????⑤９??, VerifyWorkContinuationIgnoresTransientBestActionLoss, errors)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L39 [function] `RunScenario("?꿔꺂????紐꾩뗄?嚥??????????????얠???嶺뚮??▼퐲?????썼린????꿔꺂?ｉ뜮戮녹춹??嚥싳쉶瑗??꾧틚??, VerifyDisablingCurrentWorkPriorityStopsWork, errors)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L40 [function] `RunScenario("?????봔??????????????얠???嶺뚮??▼퐲???⑤슢堉??嚥▲굧??? ?꿔꺂????紐꾩뗄?嚥??????????", VerifyUnrelatedPriorityChangeKeepsCurrentWork, errors)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L60 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L74 [function] `private static bool VerifyPriorityDefaults()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L86 [function] `private static bool VerifyDisabledWorkIsExcluded()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L105 [function] `private static bool VerifySpeciesWorkPreferenceChangesTarget()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L121 [function] `SetOnly(orcWork, FacilityWorkType.Guard, FacilityWorkType.Research)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L122 [function] `SetOnly(vampireWork, FacilityWorkType.Guard, FacilityWorkType.Research)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L137 [function] `private static bool VerifyDamagedFacilityUrgency()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L146 [function] `SetOnly(work, FacilityWorkType.Repair)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L156 [function] `private static bool VerifyUndamagedRepairIsExcluded()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L163 [function] `SetOnly(work, FacilityWorkType.Repair)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L178 [function] `private static bool VerifyFatigueProtection()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L193 [function] `private static bool VerifyRestProtectionHysteresis()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L212 [function] `private static bool VerifyRestockWithoutWarehouseSupplyIsExcluded()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L221 [function] `SetOnly(work, FacilityWorkType.Restock)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L225 [function] `ClearShopStock(shopBuilding)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L226 [function] `DrainWarehouse(warehouse)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L256 [function] `private static bool VerifyUrgentWorkOverridesRestProtection()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L265 [function] `SetOnly(work, FacilityWorkType.Repair, FacilityWorkType.Rest)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L281 [function] `private static bool VerifyPriorityPanelPrefab()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L289 [function] `private static bool VerifyPriorityPanelBuildsMatrix()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L333 [function] `private static bool VerifyWorkContinuationIgnoresTransientBestActionLoss()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L364 [function] `private static bool VerifyDisablingCurrentWorkPriorityStopsWork()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L400 [function] `private static bool VerifyUnrelatedPriorityChangeKeepsCurrentWork()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L430 [function] `private static void SetOnly(AbilityWork work, params FacilityWorkType[] enabledTypes)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L440 [function] `private static void ClearShopStock(BuildableObject building)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L446 [function] `private static void DrainWarehouse(IWarehouseFacility warehouse)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L459 [function] `private static CharacterActor CreateCharacter(string ownerAssetName)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L481 [type] `private sealed class WorkScenarioWorld : IDisposable` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L485 [function] `public WorkScenarioWorld()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L491 [function] `new TestHallwayOccupant()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L500 [function] `public BuildableObject Place(string assetName, Vector2Int position)` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L517 [function] `public void Dispose()` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L526 [type] `private sealed class TestHallwayOccupant : IGridOccupant` [GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]


## Assets\Scripts\Character\AI

### Assets/Scripts/Character/AI/FacilityCandidateCache.cs

- L4 [type] `public interface IFacilityCandidateCache` [DependencyInjection]
- L6 [function] `IReadOnlyList<BuildableObject> GetCandidates(Grid grid, FacilityRole role)` [DependencyInjection]
- L7 [function] `void MarkDynamicStateDirty()` [DependencyInjection]
- L8 [function] `void Clear()` [DependencyInjection]
- L11 [type] `public sealed class FacilityCandidateCacheStore : IFacilityCandidateCache` [DependencyInjection]
- L13 [type] `private sealed class GridFacilityCache` [DependencyInjection]
- L17 [function] `public readonly Dictionary<FacilityRole, List<BuildableObject>> CandidatesByRole` [DependencyInjection]
- L25 [function] `public IReadOnlyList<BuildableObject> GetCandidates(Grid grid, FacilityRole role)` [DependencyInjection]
- L53 [function] `public void MarkDynamicStateDirty()` [DependencyInjection]
- L61 [function] `public void Clear()` [DependencyInjection]
- L67 [function] `private GridFacilityCache GetCache(Grid grid)` [DependencyInjection]
- L85 [function] `private List<BuildableObject> GetSingleRoleCandidates(Grid grid, GridFacilityCache cache, FacilityRole role)` [DependencyInjection]
- L109 [function] `private static bool IsSingleRole(FacilityRole role)` [DependencyInjection]
- L115 [function] `private static IEnumerable<FacilityRole> GetSingleRoles(FacilityRole roles)` [DependencyInjection]

### Assets/Scripts/Character/AI/FacilityCandidateScorer.cs

- L4 [type] `public static class FacilityCandidateScorer` [DependencyInjection]
- L19 [function] `public static List<BuildableObject> GetCandidates(CharacterActor actor, GridPathSearchResult searchResult, FacilityRole role)` [DependencyInjection]
- L44 [function] `public static BuildableObject SelectBest(CharacterActor actor, IReadOnlyList<BuildableObject> candidates, FacilityRole role, GridPathSearchResult searchResult, FacilityScoringContext scoringContext)` [DependencyInjection]
- L79 [function] `public static bool HasCandidate(CharacterActor actor, GridPathSearchResult searchResult, FacilityRole role)` [DependencyInjection]
- L102 [function] `public static bool TrySelectBest(CharacterActor actor, GridPathSearchResult searchResult, FacilityRole role, FacilityScoringContext scoringContext, out BuildableObject bestBuilding)` [DependencyInjection]
- L138 [function] `public static bool IsCandidate(CharacterActor actor, BuildableObject building, FacilityRole role, out string rejectReason)` [DependencyInjection]
- L152 [function] `public static bool IsCandidate(CharacterActor actor, BuildableObject building, FacilityRole role, FacilityScoringContext scoringContext, out string rejectReason)` [DependencyInjection]
- L201 [function] `public static float ScoreCandidate(CharacterActor actor, BuildableObject building, FacilityRole role, GridPathSearchResult searchResult, FacilityScoringContext scoringContext)` [DependencyInjection]
- L221 [function] `public static float GetNeedScore(CharacterActor actor, FacilityRole role)` [DependencyInjection]
- L243 [function] `GetLowStatNeed(actor, CharacterCondition.FUN)` [DependencyInjection]
- L244 [function] `GetLowStatNeed(actor, CharacterCondition.MOOD) * 0.6f)` [DependencyInjection]
- L246 [function] `GetLowStatNeed(actor, CharacterCondition.SLEEP)` [DependencyInjection]
- L247 [function] `GetLowStatNeed(actor, CharacterCondition.MOOD) * 0.4f)` [DependencyInjection]
- L277 [function] `private static IEnumerable<BuildableObject> GetCandidateSource(CharacterActor actor, GridPathSearchResult searchResult, FacilityRole role)` [DependencyInjection]
- L295 [function] `private static IFacilityCandidateCache RequireFacilityCandidateCache(CharacterActor actor)` [DependencyInjection]
- L306 [function] `private static bool IsReachableCandidate(CharacterActor actor, GridPathSearchResult searchResult, BuildableObject building, FacilityRole role, FacilityScoringContext scoringContext)` [DependencyInjection]
- L321 [function] `private static FacilityRole GetBestMatchedRole(CharacterActor actor, BuildableObject building, FacilityRole requestedRoles)` [DependencyInjection]
- L333 [function] `private static bool HasMultipleRoles(FacilityRole role)` [DependencyInjection]
- L352 [function] `private static float GetPreferenceScore(CharacterActor actor, BuildableObject building, FacilityRole role)` [DependencyInjection]
- L365 [function] `private static float GetSpeciesTagPreferenceScore(CharacterActor actor, BuildableObject building)` [DependencyInjection]
- L389 [function] `private static float GetCharacterModelPreferenceScore(CharacterActor actor, BuildableObject building)` [DependencyInjection]
- L405 [function] `private static float GetStockScore(BuildableObject building)` [DependencyInjection]
- L421 [function] `private static float GetAffordabilityScore(CharacterActor actor, BuildableObject building)` [DependencyInjection]
- L436 [function] `private static float GetCrowdScore(CharacterActor actor, BuildableObject building)` [DependencyInjection]
- L448 [function] `private static float GetDistanceScore(BuildableObject building, GridPathSearchResult searchResult)` [DependencyInjection]
- L464 [function] `private static float GetNoveltyScore(CharacterActor actor, BuildableObject building)` [DependencyInjection]
- L492 [function] `private static float GetReputationBias(CharacterActor actor, BuildableObject building, FacilityScoringContext scoringContext)` [DependencyInjection]

### Assets/Scripts/Character/AI/FacilityScoringContext.cs

- L3 [type] `public readonly struct FacilityScoringContext` [DependencyInjection]
- L9 [function] `public FacilityScoringContext(ISocialReputationBiasService reputationBiasService, IRoomFacilityPolicy roomFacilityPolicy)` [DependencyInjection]
- L20 [function] `private FacilityScoringContext(IRoomFacilityPolicy roomFacilityPolicy, bool ignoreReputationBias)` [DependencyInjection]
- L30 [property] `public bool IsConfigured => roomFacilityPolicy != null && (ignoreReputationBias || reputationBiasService != null)` [DependencyInjection]
- L33 [function] `public static FacilityScoringContext WithoutReputationBiasForIsolatedTest(IRoomFacilityPolicy roomFacilityPolicy)` [DependencyInjection]
- L39 [function] `public static FacilityScoringContext RequireFromActor(CharacterActor actor)` [DependencyInjection]
- L50 [function] `public float GetReputationBias(CharacterActor actor, BuildableObject building)` [DependencyInjection]
- L66 [function] `public bool IsFacilityRoleAvailable(BuildableObject building, FacilityRole requestedRole, out string rejectReason)` [DependencyInjection]
- L75 [function] `public float GetRoomUtilityScore(BuildableObject building, FacilityRole role)` [DependencyInjection]
- L80 [function] `private IRoomFacilityPolicy RequireRoomFacilityPolicy()` [DependencyInjection]

## Assets\Scripts\Character\AI\Idle

### Assets/Scripts/Character/AI/Idle/IdleBehavior.cs

- L1 [type] `public interface IIdleBehavior`
- L4 [function] `bool CanRun(CharacterActor actor)`
- L5 [function] `bool TryRun(CharacterActor actor, float duration, out string failureReason)`
- L8 [type] `public sealed class StaffWanderIdleBehavior : IIdleBehavior`
- L12 [function] `public bool CanRun(CharacterActor actor)`
- L18 [function] `public bool TryRun(CharacterActor actor, float duration, out string failureReason)`
- L38 [type] `public sealed class StaticWaitIdleBehavior : IIdleBehavior`
- L42 [function] `public bool CanRun(CharacterActor actor)`
- L47 [function] `public bool TryRun(CharacterActor actor, float duration, out string failureReason)`
- L61 [type] `public static class IdleBehaviorRunner`
- L115 [function] `public static string GetSelectedBehaviorTypeNameForDebug(CharacterActor actor, bool allowMovement)`
- L121 [function] `private static IIdleBehavior SelectBehavior(CharacterActor actor, bool allowMovement)`


## Assets\Scripts\Character\AI

### Assets/Scripts/Character/AI/LlmJsonResponseParser.cs

- L5 [type] `public interface ILlmJsonPayload` [Reflection]
- L7 [function] `bool Validate(out string error)` [Reflection]
- L10 [type] `public static class LlmJsonResponseParser` [Reflection]
- L75 [function] `public static bool TryExtractJsonObject(string response, out string json, out string error)` [Reflection]
- L100 [function] `private static string StripMarkdownFence(string value)` [Reflection]
- L124 [type] `public sealed class MoodImpulseJsonDto : ILlmJsonPayload` [Reflection]
- L136 [function] `public bool Validate(out string error)` [Reflection]
- L181 [function] `public static bool ValidateRawJson(string json, out string error)` [Reflection]
- L205 [function] `public CharacterMoodImpulse ToRuntimeImpulse(string source)` [Reflection]
- L220 [function] `private static bool HasRawNumber(string json, string fieldName)` [Reflection]
- L226 [function] `private static bool HasRawInteger(string json, string fieldName)` [Reflection]
- L234 [type] `public sealed class CustomerPersonaJsonDto : ILlmJsonPayload` [Reflection]
- L249 [function] `public bool Validate(out string error)` [Reflection]
- L279 [function] `public CustomerPersonaData ToRuntimeData()` [Reflection]
- L296 [function] `public static bool ValidateRawJson(string json, out string error)` [Reflection]
- L329 [function] `private static bool ValidateMultiplier(float value, string fieldName, out string error)` [Reflection]
- L341 [function] `private static bool HasRawNumber(string json, string fieldName)` [Reflection]
- L349 [type] `public sealed class MacroGoalJsonDto : ILlmJsonPayload` [Reflection]
- L360 [function] `public bool Validate(out string error)` [Reflection]
- L400 [function] `public static bool ValidateRawJson(string json, out string error)` [Reflection]
- L418 [function] `public CharacterMacroGoal ToRuntimeGoal(string source)` [Reflection]
- L432 [function] `private static bool HasRawNumber(string json, string fieldName)` [Reflection]
- L438 [function] `private static bool HasRawInteger(string json, string fieldName)` [Reflection]
- L446 [type] `public sealed class SocialRumorJsonDto : ILlmJsonPayload` [Reflection]
- L463 [function] `public bool Validate(out string error)` [Reflection]
- L574 [function] `public static bool ValidateRawJson(string json, out string error)` [Reflection]
- L596 [function] `public SocialRumor ToRuntimeRumor(string source, CharacterActor speaker)` [Reflection]
- L619 [function] `private static bool HasRawNumber(string json, string fieldName)` [Reflection]
- L625 [function] `private static bool HasRawInteger(string json, string fieldName)` [Reflection]
- L633 [type] `public sealed class BubbleLineJsonDto : ILlmJsonPayload` [Reflection]
- L637 [function] `public bool Validate(out string error)` [Reflection]

### Assets/Scripts/Character/AI/LocalLlmRequestQueue.cs

- L9 [type] `public enum LocalLlmRequestType` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L19 [type] `public enum LocalLlmRequestStatus` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L27 [type] `public interface ILocalLlmRuntime` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L29 [function] `bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> callback)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L30 [function] `bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> callback)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L31 [function] `bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> callback)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L32 [function] `bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> callback)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L33 [function] `bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> callback)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L34 [function] `bool GenerateBubbleLineAsync(string prompt, string originalText, Action<LocalLlmResult> callback)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L37 [type] `public readonly struct LocalLlmResult` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L58 [type] `internal sealed class LocalLlmQueuedRequest` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L70 [type] `public sealed class LocalLlmRequestQueue : SerializedMonoBehaviour, ILocalLlmRuntime` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L76 [function] `public static LocalLlmRequestQueue GetOrCreateInstance()` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L99 [function] `DontDestroyOnLoad(runtimeObject)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L134 [function] `public void ConfigureBubblePolicyForDebug(float timeoutSeconds, float maxQueueAgeSeconds)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L156 [function] `public void ClearForDebug()` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L165 [function] `public void AbortAllForDebug()` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L167 [function] `StopAllCoroutines()` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L176 [function] `public void SetWarningLogsSuppressedForDebug(bool value)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L182 [function] `private static void EnsureRuntimeOnLoad()` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L184 [function] `GetOrCreateInstance()` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L187 [function] `private void Awake()` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L199 [function] `private void OnDestroy()` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L207 [function] `private void Update()` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L209 [function] `DropExpiredBubbleRequests()` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L215 [function] `StartCoroutine(ProcessRequest(request))` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L219 [function] `public bool EnqueuePersona(string prompt, Action<LocalLlmResult> callback)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L224 [function] `public bool EnqueueMacroGoal(string prompt, Action<LocalLlmResult> callback)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L229 [function] `public bool EnqueueMoodImpulse(string prompt, Action<LocalLlmResult> callback)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L234 [function] `public bool EnqueueSocialRumor(string prompt, Action<LocalLlmResult> callback)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L239 [function] `public bool EnqueueFacilityEvolution(string prompt, Action<LocalLlmResult> callback)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L244 [function] `public bool EnqueueBubbleLine(string prompt, string originalText, Action<LocalLlmResult> callback)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L249 [function] `public bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> callback)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L254 [function] `public bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> callback)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L259 [function] `public bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> callback)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L264 [function] `public bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> callback)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L269 [function] `public bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> callback)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L274 [function] `public bool GenerateBubbleLineAsync(string prompt, string originalText, Action<LocalLlmResult> callback)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L315 [function] `LogWarningIfAllowed(lastError)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L331 [function] `private IEnumerator ProcessRequest(LocalLlmQueuedRequest request)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L404 [function] `private UnityWebRequest BuildRequest(string prompt)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L434 [function] `private void Complete(LocalLlmQueuedRequest request, LocalLlmResult result)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L444 [function] `LogWarningIfAllowed(lastError)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L450 [function] `private void LogWarningIfAllowed(string message)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L460 [function] `private bool TryDropLowestPriorityBubble()` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L492 [function] `private void DropExpiredBubbleRequests()` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L518 [function] `private int FindNextRequestIndex()` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L538 [function] `private static int GetPriority(LocalLlmRequestType type)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L552 [function] `private static bool TryExtractContent(string responseJson, out string content, out string error)` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L592 [type] `private sealed class OpenAiChatRequest` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L601 [type] `private sealed class OpenAiChatMessage` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L608 [type] `private sealed class OpenAiResponseFormat` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L614 [type] `private sealed class OpenAiChatResponse` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]
- L620 [type] `private sealed class OpenAiChoice` [GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Character/AI/SocialReputationRuntime.cs

- L12 [type] `public sealed class SocialReputationRuntime : SerializedMonoBehaviour` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L61 [function] `public void ConstructSocialReputationRuntime(ILocalLlmRuntimeProvider llmRuntimeProvider, IDungeonSceneComponentQuery sceneQuery, ICharacterSocialMemoryService socialMemoryService)` [DependencyInjection]
- L75 [function] `private void Awake()` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L87 [function] `private void OnEnable()` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L89 [function] `RegisterExistingActorsIfInjected()` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L92 [function] `private void Start()` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L94 [function] `RegisterExistingActors()` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L97 [function] `private void OnDisable()` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L99 [function] `UnsubscribeAllActors()` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L106 [function] `private void Update()` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L117 [function] `public bool RequestSocialInterpretation(CharacterActor speaker, CharacterLogEntry entry)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L148 [function] `LogWarningIfAllowed(lastError)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L175 [function] `LogWarningIfAllowed(lastError)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L199 [function] `LogWarningIfAllowed(lastError)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L216 [function] `LogWarningIfAllowed(lastError)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L218 [function] `public bool ApplyRumor(SocialRumor rumor, CharacterActor speaker)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L242 [function] `private bool TryGetLlmRuntime(out ILocalLlmRuntime queue)` [DependencyInjection]
- L259 [function] `private IDungeonSceneComponentQuery RequireSceneQuery()` [DependencyInjection]
- L269 [function] `public float GetFacilityUtilityBias(CharacterActor actor, BuildableObject building)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L280 [function] `public float GetCombinedFacilitySentiment(CharacterActor actor, BuildableObject building)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L293 [function] `public float GetGlobalFacilitySentiment(BuildableObject building)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L317 [function] `public void ClearForDebug()` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L335 [function] `public void SetActorLogRequestsSuppressedForDebug(bool value)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L340 [function] `public void SetWarningLogsSuppressedForDebug(bool value)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L345 [function] `public void RestrictActorLogRequestsForDebug(CharacterActor actor)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L350 [function] `public void RegisterActorForDebug(CharacterActor actor)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L355 [function] `private void RegisterExistingActors()` [DependencyInjection]
- L364 [function] `private void RegisterExistingActorsIfInjected()` [DependencyInjection]
- L372 [function] `private void RegisterActor(CharacterActor actor)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L391 [function] `private void OnActorLogAdded(CharacterActor actor, CharacterLogEntry entry)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L398 [function] `private void OnSocialRumorResult(CharacterActor speaker, LocalLlmResult result)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L386 [function] `LogSocialWarningIfNeeded(logWarnings, $"Social rumor request failed: {lastError}")` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L393 [function] `LogSocialWarningIfNeeded(logWarnings, $"Social rumor JSON rejected: {parseError}")` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L408 [function] `LogSocialWarningIfNeeded(logWarnings, lastError)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L415 [function] `LogSocialWarningIfNeeded(logWarnings, lastError)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L423 [function] `LogSocialWarningIfNeeded(logWarnings, lastError)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L430 [function] `LogSocialWarningIfNeeded(logWarnings, lastError)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L434 [function] `private void LogSocialWarningIfNeeded(bool logWarnings, string message)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L438 [function] `LogWarningIfAllowed(message)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L442 [function] `private void LogWarningIfAllowed(string message)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L498 [function] `AppendCandidateFacilities(builder, speaker, explicitFacility)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L560 [function] `AppendFacilityLine(builder, explicitFacility)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L580 [function] `AppendFacilityLine(builder, building)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L584 [function] `private static IEnumerable<BuildableObject> FindNearbyFacilities(CharacterActor speaker)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L600 [function] `private static void AppendFacilityLine(StringBuilder builder, BuildableObject building)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L607 [function] `private static BuildableObject ResolveFacilityFromLog(CharacterLogEntry entry)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L621 [function] `private static bool TryExtractFacilityId(string value, out int facilityId)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L638 [function] `private static float InferSocialEventSentiment(CharacterLogEntry entry)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L670 [function] `private static bool ShouldInterpretSocialEvent(CharacterLogEntry entry)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L709 [function] `private int SpreadRumor(SocialRumor rumor, CharacterActor speaker)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L740 [function] `private bool CanHearRumor(CharacterActor speaker, CharacterActor listener)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L756 [function] `private void ApplyGlobalFacilityReputation(SocialRumor rumor)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L759 [function] `PruneExpiredGlobalRumors()` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L760 [function] `RebuildGlobalFacilityReputation()` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L763 [function] `private void RebuildGlobalFacilityReputation()` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L773 [function] `ApplyGlobalFacilityReputationEntry(rumor)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L776 [function] `SyncDebugList()` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L779 [function] `private void ApplyGlobalFacilityReputationEntry(SocialRumor rumor)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L796 [function] `private void PruneExpiredGlobalRumors()` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L810 [function] `RebuildGlobalFacilityReputation()` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L840 [function] `private CharacterSocialMemory EnsureMemory(CharacterActor actor)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L850 [function] `private ICharacterSocialMemoryService RequireSocialMemoryService()` [DependencyInjection]
- L860 [function] `private static void FillSourceIfMissing(SocialRumor rumor, CharacterActor speaker)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L878 [function] `private static bool HasValidTarget(SocialRumor rumor)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L898 [function] `private static bool RumorTargetsExpectedFacility(SocialRumor rumor, BuildableObject expectedFacility)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L918 [function] `private bool RumorTargetsKnownFacility(SocialRumor rumor)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L947 [function] `private static bool SentimentMatchesExpected(float actualSentiment, float expectedSentiment)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L957 [function] `private float GetNextRequestTime(CharacterActor actor)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L964 [function] `private void UnsubscribeAllActors()` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L984 [function] `private void SyncDebugList()` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]
- L993 [function] `private static bool ContainsAny(string value, params string[] patterns)` [DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation]


## Assets\Scripts\Character

### Assets/Scripts/Character/CharacterSpawner.cs

- L9 [type] `public class CharacterSpawner : BuildableObject,IInteractable` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L29 [function] `public void Construct(IRegularCustomerRuntimeProvider regularCustomerRuntimeProvider, IGridSystemProvider gridSystemProvider, IRunVariableRuntimeReader runVariableReader, ICharacterSpawnObjectFactory characterObjectFactory)` [DependencyInjection, SceneMutation]
- L45 [function] `private void Awake()` [DependencyInjection, SceneMutation]
- L50 [function] `public override void Start()` [DependencyInjection, SceneMutation]
- L63 [function] `private void EnsureRuntimeState()` [DependencyInjection, SceneMutation]
- L81 [function] `public IEnumerator StartSpawn()` [DependencyInjection, SceneMutation]
- L98 [function] `void Update()` [DependencyInjection, SceneMutation]
- L117 [function] `public bool TrySpawnCharacter(int id)` [DependencyInjection, SceneMutation]
- L146 [call] `RequireCharacterObjectFactory().Inject(spawnedCharacterGameobject)` [DependencyInjection]
- L178 [function] `public Vector3 GetOutsideSpawnWorldPosition()` [DependencyInjection, SceneMutation]
- L188 [function] `public Vector3 GetEntryDoorWorldPosition()` [DependencyInjection, SceneMutation]
- L203 [function] `public Vector2Int GetEntryGridPosition()` [DependencyInjection, SceneMutation]
- L210 [function] `public bool TryGetEntryGridPosition(out Vector2Int resolvedEntryGridPosition)` [DependencyInjection, SceneMutation]
- L229 [function] `private RegularCustomerState GetRegularCustomerState()` [DependencyInjection]
- L236 [function] `private bool TryGetGrid(out Grid grid)` [DependencyInjection]
- L241 [function] `private IRegularCustomerRuntimeProvider ResolveRegularCustomerRuntimeProvider()` [DependencyInjection]
- L247 [function] `private IGridSystemProvider ResolveGridSystemProvider()` [DependencyInjection]
- L253 [function] `private IRunVariableRuntimeReader ResolveRunVariableReader()` [DependencyInjection]
- L259 [function] `private ICharacterSpawnObjectFactory RequireCharacterObjectFactory()` [DependencyInjection]
- L265 [function] `public void Respawned(CharacterRespawnData data)` [SceneMutation]
- L269 [function] `private GameObject CreatePooledItem()` [DependencyInjection]
- L273 [function] `private void OnTakeFromPool(GameObject poolGo)` [SceneMutation]
- L277 [function] `private void OnReturnedToPool(GameObject poolGo)` [SceneMutation]
- L287 [function] `private void OnDestroyPoolObject(GameObject poolGo)` [DependencyInjection]
- L291 [function] `public IEnumerator Interact(CharacterActor actor)` [SceneMutation]
- L315 [type] `public class CharacterRespawnData`
- L321 [function] `public CharacterRespawnData(int id, float respawnTime)`
- L327 [function] `public void StartCheckRespawn(float lastDisabledTime)`
- L332 [function] `public bool CheckResapwn(float time)`

### Assets/Scripts/Character/CharacterSpawnObjectFactory.cs

- L6 [type] `public interface ICharacterSpawnObjectFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L8 [function] `GameObject Create(GameObject characterPrefab)` [RuntimeObjectCreation]
- L9 [function] `void Inject(GameObject characterObject)` [DependencyInjection]
- L10 [function] `void Destroy(GameObject characterObject)` [SceneMutation]
- L13 [type] `public sealed class CharacterSpawnObjectFactory : ICharacterSpawnObjectFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L17 [function] `public CharacterSpawnObjectFactory(IObjectResolver objectResolver)` [DependencyInjection]
- L23 [function] `public GameObject Create(GameObject characterPrefab)` [RuntimeObjectCreation]
- L33 [function] `public void Inject(GameObject characterObject)` [DependencyInjection]
- L49 [function] `public void Destroy(GameObject characterObject)` [SceneMutation]


## Assets\Scripts\Character\Core

### Assets/Scripts/Character/Core/CharacterAbilityCache.cs

- L7 [type] `public class CharacterAbilityCache : SerializedMonoBehaviour` [Reflection]
- L16 [function] `CacheAbility()` [Reflection]
- L21 [function] `private void Awake()` [Reflection]
- L23 [function] `CacheAbility()` [Reflection]
- L26 [function] `public void CacheAbility()` [Reflection]
- L29 [function] `RefreshAbilityCache()` [Reflection]
- L32 [function] `public void RefreshAbilityCache()` [Reflection]
- L40 [function] `CacheAbility()` [Reflection]
- L55 [function] `CacheAbility()` [Reflection]

### Assets/Scripts/Character/Core/CharacterActor.cs

- L23 [type] `public class CharacterActor : SerializedMonoBehaviour, IInfoable` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L113 [function] `public event Action<CharacterActor, string> OnDied` [DependencyInjection]
- L118 [function] `public void ConstructCharacterActor(IGridSystemProvider gridSystemProvider, ICharacterAiSchedulingService aiSchedulingService, IWorldInfoClickSelector worldInfoClickSelector, ICharacterSocialMemoryService socialMemoryService, ICharacterFeedbackBubbleService feedbackBubbleService)` [DependencyInjection]
- L130 [function] `public event Action<Dictionary<CharacterCondition, float>> OnStatChange` [DependencyInjection, Reflection]
- L144 [function] `public event Action<CharacterLogEntry> OnLogAdded` [DependencyInjection, Reflection]
- L208 [function] `public static CharacterActor From(Component component)` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L213 [function] `private void Awake()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L219 [function] `private void Start()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L238 [function] `private void Update()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L243 [function] `private void OnEnable()` [DependencyInjection]
- L248 [function] `private void OnDisable()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L254 [function] `private void OnMouseDown()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L259 [function] `public void Initialize(CharacterSO data)` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L283 [function] `public bool TryExecuteSelectedAiAction()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L299 [function] `public List<BuildableObject> GetReachableBuilding()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L319 [function] `private bool TryGetGrid(out Grid grid)` [DependencyInjection]
- L344 [function] `public void EnsureRuntimeState()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L399 [function] `public Vector2Int GetNowXY()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L405 [function] `public void AddLog(string message)` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L411 [function] `public float GetMoveSpeed()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L417 [function] `public float GetConsumptionMultiplier()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L423 [function] `public float GetStayDurationMultiplier()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L429 [function] `public float GetCrowdSensitivityMultiplier()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L435 [function] `public float GetWorkSpeedMultiplier(FacilityWorkType workTypes)` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L441 [function] `public float GetWorkPreferenceScore(FacilityWorkType workTypes)` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L447 [function] `public float GetFacilityPreferenceScore(FacilityRole roles)` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L453 [function] `public float GetAccidentChanceMultiplier()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L459 [function] `public CharacterSpeciesIncidentType GetIncidentType()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L465 [function] `public float GetCombatPowerMultiplier()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L471 [function] `public float GetFatigueEfficiencyMultiplier()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L477 [function] `public float GetInjuryEfficiencyMultiplier()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L501 [function] `public void Initialization(CharacterSO data)` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L506 [function] `public void CacheAbility()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L512 [function] `public void RefreshAbilityCache()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L518 [function] `public IEnumerator ChangeStatByTick()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L524 [function] `public void ChangesStat(CharacterCondition condition, float value)` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L530 [function] `public int GetCharacterStat(CharacterStatType statType)` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L536 [function] `public void ApplyDamage(float amount, string reason = "")` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L542 [function] `public void Heal(float amount)` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L548 [function] `public void SetInjurySeverity(float value)` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L554 [function] `public void Die(string reason = "")` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L560 [function] `public void InitializeStats(bool resetCurrentHealth)` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L566 [function] `public void SetLifecycleState(CharacterLifecycleState nextState)` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L572 [function] `public bool BeginExpedition()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L578 [function] `public void EndExpedition(bool alive = true)` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L584 [function] `public void ChangeLayer(string layer)` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L590 [function] `public void ApplyVisualFootAnchor()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L596 [function] `public float GetVisualTopLocalY()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L602 [function] `public void DoFade(float alpha, float duration)` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L608 [function] `public void Flip(CharacterFacing facing)` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L614 [function] `public void HideForTraversal(float failSafeSeconds)` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L620 [function] `public void RestoreTraversalVisibility()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L626 [function] `public void SetAiPaused(bool value)` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L632 [function] `public bool IsAiPaused()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L637 [function] `public string GetSpeciesShortDescription()` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L643 [function] `internal void RaiseDied(string reason)` [DependencyInjection, Reflection, RuntimeObjectCreation]
- L669 [function] `private void EnsureFeedbackBubbleIfInjected()` [DependencyInjection]
- L679 [function] `private void EnsureSocialMemory()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L686 [function] `private ICharacterAiSchedulingService RequireAiSchedulingService()` [DependencyInjection]
- L692 [function] `private IWorldInfoClickSelector RequireWorldInfoClickSelector()` [DependencyInjection]
- L698 [function] `private void RegisterWithAiSchedulerRequired()` [DependencyInjection]
- L704 [function] `private void RegisterWithAiSchedulerIfReady()` [DependencyInjection]
- L715 [function] `private void UnregisterFromAiScheduler()` [DependencyInjection]
- L726 [function] `private static IEnumerator EmptyRoutine()` [DependencyInjection, Reflection, RuntimeObjectCreation]

### Assets/Scripts/Character/Core/CharacterDeathEvent.cs

- L1 [type] `public struct CharacterDeathEvent` [EventBus]
- L6 [function] `public CharacterDeathEvent(CharacterActor actor, string reason)` [EventBus]
- L14 [function] `public static void Trigger(CharacterActor actor, string reason)` [EventBus]

### Assets/Scripts/Character/Core/CharacterEnums.cs

- L1 [type] `public enum CharacterFacing`
- L7 [type] `public enum CharacterCondition`
- L17 [type] `public enum CharacterDecisionState`
- L24 [type] `public enum CharacterLifecycleState`

### Assets/Scripts/Character/Core/CharacterIdentity.cs

- L5 [type] `public class CharacterIdentity : SerializedMonoBehaviour`
- L51 [function] `private void Awake()`
- L53 [function] `Bind(GetComponent<CharacterActor>())`
- L56 [function] `SetData(data)`
- L60 [function] `public void Bind(CharacterActor owner)`
- L65 [function] `public void SetData(CharacterSO nextData)`
- L73 [function] `public void SetCharacterType(CharacterType nextType)`
- L78 [function] `public string GetSpeciesShortDescription()`

### Assets/Scripts/Character/Core/CharacterLifecycle.cs

- L6 [type] `public class CharacterLifecycle : SerializedMonoBehaviour` [DependencyInjection, SceneMutation]
- L32 [function] `private void Awake()` [DependencyInjection, SceneMutation]
- L34 [function] `Bind(GetComponent<CharacterActor>())` [DependencyInjection, SceneMutation]
- L37 [function] `public void Bind(CharacterActor owner)` [DependencyInjection, SceneMutation]
- L47 [function] `public void SetAiPaused(bool value)` [DependencyInjection, SceneMutation]
- L49 [function] `SetLifecycleState(value ? CharacterLifecycleState.EnteringDungeon : CharacterLifecycleState.Active)` [DependencyInjection, SceneMutation]
- L52 [function] `public bool BeginExpedition()` [DependencyInjection, SceneMutation]
- L68 [function] `SetLifecycleState(CharacterLifecycleState.OnExpedition)` [DependencyInjection, SceneMutation]
- L73 [function] `public void EndExpedition(bool alive = true)` [DependencyInjection, SceneMutation]
- L83 [function] `SetLifecycleState(CharacterLifecycleState.Active)` [DependencyInjection, SceneMutation]
- L92 [function] `public void SetLifecycleState(CharacterLifecycleState nextState)` [DependencyInjection, SceneMutation]
- L118 [function] `public Vector2Int GetNowXY()` [DependencyInjection, SceneMutation]
- L131 [function] `public IEnumerator SnapToWalkableGridWhenReady()` [DependencyInjection, SceneMutation]

### Assets/Scripts/Character/Core/CharacterLog.cs

- L8 [type] `public class CharacterLog : SerializedMonoBehaviour` [Reflection]
- L21 [function] `EnsureLog()` [Reflection]
- L27 [function] `private void Awake()` [Reflection]
- L29 [function] `EnsureLog()` [Reflection]
- L32 [function] `public void Bind()` [Reflection]
- L34 [function] `EnsureLog()` [Reflection]
- L37 [function] `public void AddLog(string message)` [Reflection]
- L41 [function] `EnsureLog()` [Reflection]
- L68 [function] `private void EnsureLog()` [Reflection]
- L75 [type] `public readonly struct CharacterLogEntry` [Reflection]
- L82 [function] `public CharacterLogEntry(string tag, string displayLine, int count, string originalMessage)` [Reflection]
- L91 [type] `public static class CharacterLogUtility` [Reflection]
- L93 [function] `public static string ToCauseTag(string message)` [Reflection]
- L155 [function] `private static bool ContainsAny(string value, params string[] patterns)` [Reflection]
- L160 [function] `private static string ExtractReasonAfterSeparator(string value)` [Reflection]

### Assets/Scripts/Character/Core/CharacterStats.cs

- L9 [type] `public class CharacterStats : SerializedMonoBehaviour` [DependencyInjection, Reflection]
- L45 [function] `EnsureStats()` [DependencyInjection, Reflection]
- L51 [function] `EnsureStats()` [DependencyInjection, Reflection]
- L62 [function] `private void Awake()` [DependencyInjection, Reflection]
- L64 [function] `Bind(GetComponent<CharacterActor>())` [DependencyInjection, Reflection]
- L69 [function] `public void ConstructCharacterStats(IStaffDiscontentRuntimeService staffDiscontentRuntimeService, IOwnerRunLifecycleService ownerRunLifecycleService, IMetaProgressionRuntimeReader metaProgressionRuntimeReader)` [DependencyInjection, Reflection]
- L82 [function] `public void Bind(CharacterActor owner)` [DependencyInjection, Reflection]
- L89 [function] `EnsureStats()` [DependencyInjection, Reflection]
- L92 [function] `public IEnumerator ChangeStatByTick()` [DependencyInjection, Reflection]
- L105 [function] `ChangesStat(CharacterCondition.HUNGER, -5f * hungerMultiplier)` [DependencyInjection, Reflection]
- L106 [function] `ChangesStat(CharacterCondition.EXCRETION, -3f * excretionMultiplier)` [DependencyInjection, Reflection]
- L107 [function] `ChangesStat(CharacterCondition.HYGIENE, -1.5f * hygieneMultiplier)` [DependencyInjection, Reflection]
- L112 [function] `public void ChangesStat(CharacterCondition condition, float value)` [DependencyInjection, Reflection]
- L114 [function] `EnsureStats()` [DependencyInjection, Reflection]
- L119 [function] `public int GetCharacterStat(CharacterStatType statType)` [DependencyInjection, Reflection]
- L124 [function] `public float GetMoveSpeed()` [DependencyInjection, Reflection]
- L133 [function] `public float GetConsumptionMultiplier()` [DependencyInjection, Reflection]
- L138 [function] `public float GetStayDurationMultiplier()` [DependencyInjection, Reflection]
- L143 [function] `public float GetCrowdSensitivityMultiplier()` [DependencyInjection, Reflection]
- L148 [function] `public float GetWorkSpeedMultiplier(FacilityWorkType workTypes)` [DependencyInjection, Reflection]
- L161 [function] `public float GetWorkPreferenceScore(FacilityWorkType workTypes)` [DependencyInjection, Reflection]
- L166 [function] `public float GetFacilityPreferenceScore(FacilityRole roles)` [DependencyInjection, Reflection]
- L171 [function] `public float GetAccidentChanceMultiplier()` [DependencyInjection, Reflection]
- L176 [function] `public CharacterSpeciesIncidentType GetIncidentType()` [DependencyInjection, Reflection]
- L181 [function] `public float GetCombatPowerMultiplier()` [DependencyInjection, Reflection]
- L187 [function] `public float GetFatigueEfficiencyMultiplier()` [DependencyInjection, Reflection]
- L189 [function] `EnsureStats()` [DependencyInjection, Reflection]
- L198 [function] `public float GetInjuryEfficiencyMultiplier()` [DependencyInjection, Reflection]
- L203 [function] `public void ApplyDamage(float amount, string reason = "")` [DependencyInjection, Reflection]
- L215 [function] `Die(reason)` [DependencyInjection, Reflection]
- L219 [function] `public void Heal(float amount)` [DependencyInjection, Reflection]
- L228 [function] `public void SetInjurySeverity(float value)` [DependencyInjection, Reflection]
- L235 [function] `public void Die(string reason = "")` [DependencyInjection, Reflection]
- L255 [function] `public void RecalculateVitals(bool resetCurrentHealth)` [DependencyInjection, Reflection]
- L277 [function] `private void EnsureStats()` [DependencyInjection, Reflection]
- L280 [function] `EnsureStat(CharacterCondition.SLEEP, 5f)` [DependencyInjection, Reflection]
- L281 [function] `EnsureStat(CharacterCondition.HUNGER, 100f)` [DependencyInjection, Reflection]
- L282 [function] `EnsureStat(CharacterCondition.FUN, 5f)` [DependencyInjection, Reflection]
- L283 [function] `EnsureStat(CharacterCondition.MOOD, 5f)` [DependencyInjection, Reflection]
- L284 [function] `EnsureStat(CharacterCondition.EXCRETION, 100f)` [DependencyInjection, Reflection]
- L285 [function] `EnsureStat(CharacterCondition.HYGIENE, 100f)` [DependencyInjection, Reflection]
- L288 [function] `private IMetaProgressionRuntimeReader ResolveMetaProgressionRuntimeReader()` [DependencyInjection, Reflection]
- L294 [function] `private void EnsureStat(CharacterCondition condition, float defaultValue)` [DependencyInjection, Reflection]

### Assets/Scripts/Character/Core/CharacterVisual.cs

- L8 [type] `public class CharacterVisual : SerializedMonoBehaviour` [RuntimeObjectCreation, SceneMutation]
- L30 [function] `private void Awake()` [RuntimeObjectCreation, SceneMutation]
- L32 [function] `Bind()` [RuntimeObjectCreation, SceneMutation]
- L35 [function] `public void Bind()` [RuntimeObjectCreation, SceneMutation]
- L39 [function] `EnsureVisualReferences()` [RuntimeObjectCreation, SceneMutation]
- L42 [function] `public void ChangeLayer(string layer)` [RuntimeObjectCreation, SceneMutation]
- L44 [function] `EnsureVisualReferences()` [RuntimeObjectCreation, SceneMutation]
- L51 [function] `public void EnsureVisualReferences()` [RuntimeObjectCreation, SceneMutation]
- L70 [function] `CopySpriteRenderer(rootRenderer, visualRenderer)` [RuntimeObjectCreation, SceneMutation]
- L77 [function] `CopySpriteRenderer(rootRenderer, visualRenderer)` [RuntimeObjectCreation, SceneMutation]
- L80 [function] `RemoveRootSpriteRenderer(rootRenderer)` [RuntimeObjectCreation, SceneMutation]
- L96 [function] `ApplyVisualFootAnchor()` [RuntimeObjectCreation, SceneMutation]
- L99 [function] `private Transform CreateVisualRoot()` [RuntimeObjectCreation, SceneMutation]
- L110 [function] `private static void CopySpriteRenderer(SpriteRenderer source, SpriteRenderer target)` [RuntimeObjectCreation, SceneMutation]
- L129 [function] `private static void RemoveRootSpriteRenderer(SpriteRenderer rootRenderer)` [RuntimeObjectCreation, SceneMutation]
- L139 [function] `Destroy(rootRenderer)` [RuntimeObjectCreation, SceneMutation]
- L143 [function] `DestroyImmediate(rootRenderer)` [RuntimeObjectCreation, SceneMutation]
- L146 [function] `public void SetCharacterSprite(Sprite sprite)` [RuntimeObjectCreation, SceneMutation]
- L148 [function] `EnsureVisualReferences()` [RuntimeObjectCreation, SceneMutation]
- L155 [function] `ApplyVisualFootAnchor()` [RuntimeObjectCreation, SceneMutation]
- L158 [function] `public void ApplyVisualFootAnchor()` [RuntimeObjectCreation, SceneMutation]
- L174 [function] `public float GetVisualTopLocalY()` [RuntimeObjectCreation, SceneMutation]
- L176 [function] `EnsureVisualReferences()` [RuntimeObjectCreation, SceneMutation]
- L186 [function] `public void DoFade(float alpha, float duration)` [RuntimeObjectCreation, SceneMutation]
- L188 [function] `EnsureVisualReferences()` [RuntimeObjectCreation, SceneMutation]
- L196 [function] `public void Flip(CharacterFacing nextFacing)` [RuntimeObjectCreation, SceneMutation]
- L199 [function] `EnsureVisualReferences()` [RuntimeObjectCreation, SceneMutation]
- L215 [function] `public void HideForTraversal(float failSafeSeconds)` [RuntimeObjectCreation, SceneMutation]
- L217 [function] `EnsureVisualReferences()` [RuntimeObjectCreation, SceneMutation]
- L218 [function] `RestoreTraversalVisibility()` [RuntimeObjectCreation, SceneMutation]
- L219 [function] `EnsureVisibleForActiveLifecycle()` [RuntimeObjectCreation, SceneMutation]
- L225 [function] `SetTraversalVisible(false)` [RuntimeObjectCreation, SceneMutation]
- L233 [function] `public void RestoreTraversalVisibility()` [RuntimeObjectCreation, SceneMutation]
- L235 [function] `StopTraversalVisibilityTimer()` [RuntimeObjectCreation, SceneMutation]
- L236 [function] `RestoreTraversalVisibilityNow()` [RuntimeObjectCreation, SceneMutation]
- L239 [function] `private IEnumerator RestoreTraversalVisibilityAfter(float seconds)` [RuntimeObjectCreation, SceneMutation]
- L243 [function] `RestoreTraversalVisibilityNow()` [RuntimeObjectCreation, SceneMutation]
- L246 [function] `private void StopTraversalVisibilityTimer()` [RuntimeObjectCreation, SceneMutation]
- L253 [function] `StopCoroutine(traversalVisibilityRestoreRoutine)` [RuntimeObjectCreation, SceneMutation]
- L257 [function] `private void RestoreTraversalVisibilityNow()` [RuntimeObjectCreation, SceneMutation]
- L291 [function] `EnsureVisibleForActiveLifecycle()` [RuntimeObjectCreation, SceneMutation]
- L294 [function] `public void RecoverExpiredTraversalVisibility()` [RuntimeObjectCreation, SceneMutation]
- L301 [function] `RestoreTraversalVisibility()` [RuntimeObjectCreation, SceneMutation]
- L312 [function] `EnsureVisualReferences()` [RuntimeObjectCreation, SceneMutation]
- L319 [function] `private List<RendererVisibilityState> CaptureRendererVisibility()` [RuntimeObjectCreation, SceneMutation]
- L333 [function] `private List<CanvasVisibilityState> CaptureCanvasVisibility()` [RuntimeObjectCreation, SceneMutation]
- L347 [function] `private void SetTraversalVisible(bool value)` [RuntimeObjectCreation, SceneMutation]
- L372 [function] `public void SetRenderersVisible(bool value)` [RuntimeObjectCreation, SceneMutation]
- L374 [function] `RestoreTraversalVisibility()` [RuntimeObjectCreation, SceneMutation]
- L375 [function] `SetSpriteRenderersVisible(value)` [RuntimeObjectCreation, SceneMutation]
- L378 [function] `public void EnsureVisibleForActiveLifecycle()` [RuntimeObjectCreation, SceneMutation]
- L389 [function] `SetSpriteRenderersVisible(true)` [RuntimeObjectCreation, SceneMutation]
- L392 [function] `private bool CanRecoverActiveVisibility()` [RuntimeObjectCreation, SceneMutation]
- L410 [function] `private void SetSpriteRenderersVisible(bool value)` [RuntimeObjectCreation, SceneMutation]
- L418 [type] `private readonly struct RendererVisibilityState` [RuntimeObjectCreation, SceneMutation]
- L420 [function] `public RendererVisibilityState(Renderer renderer, bool enabled)` [RuntimeObjectCreation, SceneMutation]
- L430 [type] `private readonly struct CanvasVisibilityState` [RuntimeObjectCreation, SceneMutation]
- L432 [function] `public CanvasVisibilityState(Canvas canvas, bool enabled)` [RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Character/Core/CharacterVisualRootFactory.cs

- L4 [type] `public interface ICharacterVisualRootFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L6 [function] `SpriteRenderer EnsureVisualRoot(GameObject characterObject)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L9 [type] `public sealed class CharacterVisualRootFactory : ICharacterVisualRootFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L13 [function] `public SpriteRenderer EnsureVisualRoot(GameObject characterObject)` [RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Character/Core/Customer.cs

- L12 [type] `public class Customer : CharacterActor` [Reflection]

### Assets/Scripts/Character/Core/OwnerRunManager.cs

- L7 [type] `public class OwnerRunManager : SerializedMonoBehaviour, UtilEventListener<CharacterDeathEvent>` [DependencyInjection, Reflection, EventBus, SceneMutation]
- L29 [function] `private void Awake()` [DependencyInjection, Reflection, EventBus, SceneMutation]
- L36 [function] `public void ConstructOwnerRunManager(IOwnerCandidateCatalog ownerCandidateCatalog, IOwnerCharacterFactory ownerCharacterFactory)` [DependencyInjection, Reflection, EventBus, SceneMutation]
- L47 [function] `private void Start()` [DependencyInjection, Reflection, EventBus, SceneMutation]
- L59 [function] `public void SelectOwnerByIndex(int index)` [DependencyInjection, Reflection, EventBus, SceneMutation]
- L71 [function] `public void SelectOwner(CharacterSO ownerData)` [DependencyInjection, Reflection, EventBus, SceneMutation]
- L96 [function] `public void HandleOwnerDeath(CharacterActor owner, string reason)` [DependencyInjection, Reflection, EventBus, SceneMutation]
- L112 [function] `public CharacterSO GetDefaultOwner()` [DependencyInjection, Reflection, EventBus, SceneMutation]
- L118 [function] `private CharacterActor SpawnOwner(CharacterSO ownerData)` [DependencyInjection, Reflection, EventBus, SceneMutation]
- L127 [function] `private void EnsureOwnerCandidates()` [DependencyInjection, Reflection, EventBus, SceneMutation]
- L142 [function] `private void NormalizeOwnerCandidates()` [DependencyInjection, Reflection, EventBus, SceneMutation]
- L150 [function] `private IOwnerCharacterFactory ResolveOwnerCharacterFactory()` [DependencyInjection, Reflection, EventBus, SceneMutation]
- L156 [function] `public void OnTriggerEvent(CharacterDeathEvent eventType)` [DependencyInjection, Reflection, EventBus, SceneMutation]
- L164 [function] `private void OnEnable()` [DependencyInjection, Reflection, EventBus, SceneMutation]
- L169 [function] `private void OnDisable()` [DependencyInjection, Reflection, EventBus, SceneMutation]
- L175 [type] `public struct OwnerRunEndedEvent` [DependencyInjection, Reflection, EventBus, SceneMutation]
- L180 [function] `public OwnerRunEndedEvent(CharacterActor owner, string reason)` [DependencyInjection, Reflection, EventBus, SceneMutation]
- L188 [function] `public static void Trigger(CharacterActor owner, string reason)` [DependencyInjection, Reflection, EventBus, SceneMutation]

### Assets/Scripts/Character/Core/OwnerCharacterFactory.cs

- L6 [type] `public interface IOwnerCharacterFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L8 [function] `CharacterActor CreateOwner(CharacterSO ownerData, GameObject ownerPrefab, Transform ownerSpawnPoint, Vector2Int ownerSpawnGridPosition)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L15 [type] `public sealed class OwnerCharacterFactory : IOwnerCharacterFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L21 [function] `public OwnerCharacterFactory(IObjectResolver objectResolver, IGridSystemProvider gridSystemProvider, ICharacterVisualRootFactory visualRootFactory)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L34 [function] `public CharacterActor CreateOwner(CharacterSO ownerData, GameObject ownerPrefab, Transform ownerSpawnPoint, Vector2Int ownerSpawnGridPosition)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L60 [function] `private CharacterActor EnsureOwnerComponents(GameObject ownerObject)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L97 [function] `private void InjectOwnerRuntime(GameObject ownerObject)` [DependencyInjection]
- L105 [function] `private Vector3 ResolveOwnerSpawnPosition(Transform ownerSpawnPoint, Vector2Int ownerSpawnGridPosition)` [DependencyInjection]

### Assets/Scripts/Character/Core/Shopkeeper.cs

- L12 [type] `public class Shopkeeper : CharacterActor` [Reflection]


## Assets\Scripts\Character\Input

### Assets/Scripts/Character/Input/OwnerCommandController.cs

- L5 [type] `public class OwnerCommandController : MonoBehaviour, UtilEventListener<InfoFeedEvent>` [DependencyInjection, EventBus]
- L12 [function] `public CharacterActor SelectedActor => selectedActor` [DependencyInjection, EventBus]
- L15 [function] `private IStaffDiscontentRuntimeService StaffDiscontentRuntimeService => staffDiscontentRuntimeService` [DependencyInjection]
- L20 [function] `public void ConstructOwnerCommandController(IStaffDiscontentRuntimeService staffDiscontentRuntimeService, IMainCameraProvider mainCameraProvider)` [DependencyInjection]
- L30 [function] `private void Update()` [DependencyInjection, EventBus]
- L43 [function] `private void TryIssuePriorityWorkCommand()` [DependencyInjection, EventBus]
- L92 [function] `private void TryIssueSuppressCommand(CharacterActor target)` [DependencyInjection, EventBus]
- L124 [function] `public void OnTriggerEvent(InfoFeedEvent eventType)` [DependencyInjection, EventBus]
- L133 [function] `private void OnEnable()` [DependencyInjection, EventBus]
- L138 [function] `private void OnDisable()` [DependencyInjection, EventBus]
- L143 [function] `private IMainCameraProvider RequireMainCameraProvider()` [DependencyInjection]


## Assets\Scripts\Character\SO

### Assets/Scripts/Character/SO/CharacterModelData.cs

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

### Assets/Scripts/Character/SO/CharacterSO.cs

- L10 [type] `public class CharacterSO : ScriptableObject`
- L56 [function] `public CharacterRuntimeProfile CreateRuntimeProfile()`
- L105 [function] `public int GetFrequencyVisit()`
- L111 [function] `public int GetHoldingMoney()`
- L116 [function] `public int GetHoldingMoney(CharacterRuntimeProfile profile)`
- L127 [type] `private enum CharacterSpeedType`
- L135 [type] `private enum CharacterRespawnSpeedType`
- L144 [type] `public enum CharacterType`
- L151 [type] `public enum CharacterRole`

### Assets/Scripts/Character/SO/CharacterSpeciesSO.cs

- L4 [type] `public enum CharacterSpeciesIncidentType`
- L13 [type] `public class CharacterSpeciesSO : DataScriptableObject`

### Assets/Scripts/Character/SO/CharacterTraitSO.cs

- L4 [type] `public class CharacterTraitSO : DataScriptableObject`


## Assets\Scripts\Character\UI

### Assets/Scripts/Character/UI/CharacterFeedbackBubble.cs

- L8 [type] `public enum CharacterFeedbackState` [DependencyInjection, Reflection, SceneMutation]
- L22 [type] `public class CharacterFeedbackBubble : MonoBehaviour` [DependencyInjection, Reflection, SceneMutation]
- L41 [function] `public void ConstructCharacterFeedbackBubble(ICharacterAiSchedulingService aiSchedulingService, ICharacterFeedbackBubbleViewFactory bubbleViewFactory)` [DependencyInjection]
- L51 [function] `private void Awake()` [DependencyInjection, Reflection, SceneMutation]
- L60 [function] `private void OnEnable()` [DependencyInjection, Reflection, SceneMutation]
- L77 [function] `private void OnDisable()` [DependencyInjection, Reflection, SceneMutation]
- L95 [function] `private void LateUpdate()` [DependencyInjection, Reflection, SceneMutation]
- L113 [function] `public void Show(CharacterFeedbackState state)` [DependencyInjection, Reflection, SceneMutation]
- L124 [function] `public CharacterFeedbackState EvaluatePersistentState()` [DependencyInjection, Reflection, SceneMutation]
- L164 [function] `public static CharacterFeedbackState ClassifyLogTag(string tag)` [DependencyInjection, Reflection, SceneMutation]
- L199 [function] `public static string GetSymbol(CharacterFeedbackState state)` [DependencyInjection, Reflection, SceneMutation]
- L212 [function] `private void OnLogAdded(CharacterLogEntry entry)` [DependencyInjection, Reflection, SceneMutation]
- L228 [function] `private void OnStatChanged(System.Collections.Generic.Dictionary<CharacterCondition, float> stats)` [DependencyInjection, Reflection, SceneMutation]
- L236 [function] `private void ApplyState(CharacterFeedbackState state)` [DependencyInjection, Reflection, SceneMutation]
- L255 [function] `private void HideView()` [DependencyInjection, Reflection, SceneMutation]
- L261 [function] `private void EnsureView()` [DependencyInjection, Reflection, SceneMutation]
- L271 [function] `private void ReleaseView()` [DependencyInjection, Reflection, SceneMutation]
- L282 [function] `private Vector3 GetLocalOffset()` [DependencyInjection, Reflection, SceneMutation]
- L294 [function] `private float GetStat(CharacterCondition condition, float defaultValue)` [DependencyInjection, Reflection, SceneMutation]
- L303 [function] `private ICharacterAiSchedulingService RequireAiSchedulingService()` [DependencyInjection]
- L309 [function] `private ICharacterFeedbackBubbleViewFactory RequireBubbleViewFactory()` [DependencyInjection]
- L316 [function] `private static Color GetColor(CharacterFeedbackState state)` [DependencyInjection, Reflection, SceneMutation]
- L329 [function] `private static bool ContainsAny(string value, params string[] patterns)` [DependencyInjection, Reflection, SceneMutation]

### Assets/Scripts/Character/UI/CharacterFeedbackBubbleService.cs

- L5 [type] `public interface ICharacterFeedbackBubbleService` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L7 [function] `CharacterFeedbackBubble GetOrAdd(CharacterActor actor)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L10 [type] `public sealed class CharacterFeedbackBubbleService : ICharacterFeedbackBubbleService` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L14 [function] `public CharacterFeedbackBubbleService(IObjectResolver objectResolver)` [DependencyInjection]
- L19 [function] `public CharacterFeedbackBubble GetOrAdd(CharacterActor actor)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Character/UI/CharacterFeedbackBubbleViewFactory.cs

- L7 [type] `public interface ICharacterFeedbackBubbleViewFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L9 [function] `TextMeshPro Acquire(Transform parent, Vector3 localPosition)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L10 [function] `void Release(TextMeshPro text)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L13 [type] `public sealed class CharacterFeedbackBubbleViewFactory : ICharacterFeedbackBubbleViewFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L19 [function] `public CharacterFeedbackBubbleViewFactory(ITmpKoreanFontService tmpKoreanFontService)` [DependencyInjection]
- L25 [function] `public TextMeshPro Acquire(Transform parent, Vector3 localPosition)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L47 [function] `public void Release(TextMeshPro text)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L60 [function] `private TextMeshPro CreateTextView()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Character/UI/CharacterFloatingIcon.cs

- L5 [type] `public interface IFloatingIconFeedbackService` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L10 [type] `public static class FloatingIconFeedbackDefaults`
- L15 [type] `public sealed class GameManagerFloatingIconFeedbackService : IFloatingIconFeedbackService` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L20 [function] `public GameManagerFloatingIconFeedbackService(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L25 [function] `public bool Show(Component target, Sprite sprite, float maxWorldSize)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L45 [function] `private DamageNumber ResolveNumber(NumberCondition condition)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L61 [function] `private GameManager ResolveGameManager()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L68 [function] `private static void FitIcon(SpriteRenderer iconRenderer, float maxWorldSize)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Character/UI/OwnerSelectionPanel.cs

- L7 [type] `public class OwnerSelectionPanel : MonoBehaviour` [DependencyInjection, SceneMutation]
- L20 [function] `public void ConstructOwnerSelectionPanel(IOwnerRunManagerProvider ownerRunManagerProvider, ITmpKoreanFontService tmpKoreanFontService, IOwnerSelectionOptionButtonFactory optionButtonFactory)` [DependencyInjection, SceneMutation]
- L33 [function] `private void Start()` [DependencyInjection, SceneMutation]
- L51 [function] `public void BuildOptions()` [DependencyInjection, SceneMutation]
- L78 [function] `private OwnerRunManager ResolveOwnerRunManager()` [DependencyInjection, SceneMutation]
- L99 [function] `private void RefreshSelectedOwner(CharacterSO ownerData)` [DependencyInjection, SceneMutation]
- L109 [function] `private ITmpKoreanFontService RequireTmpKoreanFontService()` [DependencyInjection]
- L116 [function] `private IOwnerSelectionOptionButtonFactory RequireOptionButtonFactory()` [DependencyInjection]
- L123 [function] `private static string MakeButtonLabel(CharacterSO candidate)` [DependencyInjection, SceneMutation]
- L133 [function] `private void OnDestroy()` [DependencyInjection, SceneMutation]

### Assets/Scripts/Character/UI/OwnerSelectionOptionButtonFactory.cs

- L7 [type] `public interface IOwnerSelectionOptionButtonFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L9 [function] `Button Create(Button prefab, Transform parent, string objectName, string label, UnityAction onClick)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L10 [function] `void Release(GameObject optionObject)` [RuntimeObjectCreation, SceneMutation]
- L13 [type] `public sealed class OwnerSelectionOptionButtonFactory : IOwnerSelectionOptionButtonFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L17 [function] `public OwnerSelectionOptionButtonFactory(ITmpKoreanFontService tmpKoreanFontService)` [DependencyInjection]
- L23 [function] `public Button Create(Button prefab, Transform parent, string objectName, string label, UnityAction onClick)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L53 [function] `public void Release(GameObject optionObject)` [RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Character/UI/StaffWorkPriorityPanel.cs

- L8 [type] `public class StaffWorkPriorityPanel : MonoBehaviour, UtilEventListener<InfoFeedEvent>` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L36 [function] `public void ConstructStaffWorkPriorityPanel(IStaffWorkforceQueryService workforceQueryService, ITmpKoreanFontService tmpKoreanFontService)` [DependencyInjection]
- L31 [function] `private void Awake()` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L36 [function] `private void Start()` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L39 [function] `Refresh()` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L42 [function] `private void Update()` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L53 [function] `Refresh()` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L57 [function] `public void OnTriggerEvent(InfoFeedEvent eventType)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L63 [function] `Refresh()` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L70 [function] `Refresh()` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L74 [function] `public void Refresh()` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L76 [function] `EnsureLayout()` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L77 [function] `BuildTable()` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L80 [function] `private void EnsureLayout()` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L88 [function] `ClearHost(host)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L157 [function] `private RectTransform ResolveHost()` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L186 [function] `private void ClearHost(RectTransform host)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L203 [function] `DestroyUiObject(child.gameObject)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L207 [function] `private void BuildTable()` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L214 [function] `ClearSpawnedObjects()` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L239 [function] `CreateLabelCell(emptyRow.transform, "?꿔꺂?????????ㅼ굡??, tableWidth, HeaderHeight, TextAlignmentOptions.Center, true)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L243 [function] `BuildHeader(workTypes, tableWidth)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L246 [function] `BuildWorkerRow(worker, workTypes, tableWidth)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L250 [function] `private void BuildHeader(IReadOnlyList<FacilityWorkType> workTypes, float tableWidth)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L253 [function] `CreateLabelCell(row.transform, "??????, CharacterColumnWidth, HeaderHeight, TextAlignmentOptions.Center, true)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L259 [function] `GetWorkTypeLabel(workType)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L271 [function] `CreateLabelCell(row.transform, "????븐뻤??, StatusColumnWidth, HeaderHeight, TextAlignmentOptions.Center, true)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L274 [function] `private void BuildWorkerRow(WorkerRow worker, IReadOnlyList<FacilityWorkType> workTypes, float tableWidth)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L294 [function] `CreatePriorityCell(row.transform, worker, workType)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L299 [function] `GetWorkerStatus(worker)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L310 [function] `private GameObject CreateRow(string name, float width, float height)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L355 [function] `private void CreatePriorityCell(Transform parent, WorkerRow worker, FacilityWorkType workType)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L370 [function] `Refresh()` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L387 [function] `private static GameObject CreateCellObject(string name, Transform parent, float width, float height)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L416 [function] `private TMP_Text AddCellText(Transform parent, string text, TextAlignmentOptions alignment, bool allowAutoSize)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L423 [function] `private static GameObject CreateUIObject(string name, Transform parent)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L440 [function] `private List<WorkerRow> FindWorkers()` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L445 [function] `private int CalculateWorkerHash()` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L450 [function] `private static int CalculateWorkerHash(IReadOnlyList<WorkerRow> workers)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L476 [function] `private static string GetWorkTypeLabel(FacilityWorkType workType)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L492 [function] `private static string GetPriorityLabel(WorkPriorityLevel priority)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L503 [function] `private static Color GetPriorityColor(WorkPriorityLevel priority, bool selected)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L518 [function] `private static string GetWorkerStatus(WorkerRow worker)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L546 [function] `private void ClearSpawnedObjects()` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L552 [function] `DestroyUiObject(tableRoot.GetChild(i).gameObject)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L563 [function] `DestroyUiObject(obj)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L570 [function] `private static void DestroyUiObject(GameObject obj)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L581 [function] `Destroy(obj)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L585 [function] `DestroyImmediate(obj)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L589 [function] `private void OnEnable()` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L595 [function] `private void OnDisable()` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L616 [function] `private IStaffWorkforceQueryService RequireWorkforceQueryService()` [DependencyInjection]
- L622 [function] `private ITmpKoreanFontService RequireTmpKoreanFontService()` [DependencyInjection]
- L629 [type] `private readonly struct WorkerRow` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L631 [function] `public WorkerRow(CharacterActor character, AbilityWork work, string name)` [Reflection, EventBus, RuntimeObjectCreation, SceneMutation]

#### Manual refresh: StaffWorkPriorityPanel.cs current extraction

- Lines: 528
- Current flags: `DependencyInjection, EventBus, SceneMutation`
- Creation ownership remains in `StaffWorkPriorityPanelUiFactory`; worker discovery/display-name/hash calculation moved to `IStaffWorkPriorityPanelModelBuilder`.
- `StaffWorkPriorityPanel.cs` no longer contains `new GameObject`, `AddComponent`, `Instantiate`, `Destroy(`, `DestroyImmediate`, or direct `IStaffWorkforceQueryService` ownership.
- L36 [function] `public void ConstructStaffWorkPriorityPanel(IStaffWorkPriorityPanelModelBuilder modelBuilder, IStaffWorkPriorityPanelUiFactory uiFactory)` [DependencyInjection]
- L89 [function] `public void Refresh()` [DependencyInjection, SceneMutation]
- L95 [function] `private void EnsureLayout()` [DependencyInjection, SceneMutation]
- L309 [function] `private GameObject CreateRow(string name, float width, float height)` [DependencyInjection, SceneMutation]
- L322 [function] `private TMP_Text CreateLabelCell(Transform parent, string text, float width, float height, TextAlignmentOptions alignment, bool header)` [DependencyInjection, SceneMutation]
- L347 [function] `private void CreatePriorityCell(Transform parent, StaffWorkPriorityRowModel worker, FacilityWorkType workType)` [DependencyInjection, SceneMutation]
- L375 [function] `private GameObject CreateCellObject(string name, Transform parent, float width, float height)` [DependencyInjection, SceneMutation]
- L385 [function] `private TMP_Text AddCellText(Transform parent, string text, TextAlignmentOptions alignment, bool allowAutoSize)` [DependencyInjection, SceneMutation]
- L406 [function] `private int CalculateWorkerHash()` [DependencyInjection]
- L453 [function] `private static string GetWorkerStatus(StaffWorkPriorityRowModel worker)` [SceneMutation]
- L481 [function] `private void ClearSpawnedObjects()` [DependencyInjection, SceneMutation]
- L516 [function] `private IStaffWorkPriorityPanelModelBuilder RequireModelBuilder()` [DependencyInjection]
- L522 [function] `private IStaffWorkPriorityPanelUiFactory RequireUiFactory()` [DependencyInjection]

### Assets/Scripts/Character/UI/StaffWorkPriorityPanelModel.cs

- L4 [type] `public readonly struct StaffWorkPriorityRowModel` [DependencyInjection]
- L6 [function] `public StaffWorkPriorityRowModel(CharacterActor character, AbilityWork work, string name)` [DependencyInjection]
- L18 [type] `public interface IStaffWorkPriorityPanelModelBuilder` [DependencyInjection]
- L20 [function] `IReadOnlyList<StaffWorkPriorityRowModel> BuildRows()` [DependencyInjection]
- L21 [function] `int CalculateWorkerHash()` [DependencyInjection]
- L22 [function] `int CalculateWorkerHash(IReadOnlyList<StaffWorkPriorityRowModel> workers)` [DependencyInjection]
- L23 [function] `string GetDisplayName(CharacterActor character)` [DependencyInjection]
- L26 [type] `public sealed class StaffWorkPriorityPanelModelBuilder : IStaffWorkPriorityPanelModelBuilder` [DependencyInjection]
- L30 [function] `public StaffWorkPriorityPanelModelBuilder(IStaffWorkforceQueryService workforceQueryService)` [DependencyInjection]
- L36 [function] `public IReadOnlyList<StaffWorkPriorityRowModel> BuildRows()` [DependencyInjection]
- L54 [function] `public int CalculateWorkerHash()` [DependencyInjection]
- L59 [function] `public int CalculateWorkerHash(IReadOnlyList<StaffWorkPriorityRowModel> workers)` [DependencyInjection]
- L90 [function] `public string GetDisplayName(CharacterActor character)` [DependencyInjection]

### Assets/Scripts/Character/UI/StaffWorkPriorityPanelUiFactory.cs

- L6 [type] `public interface IStaffWorkPriorityPanelUiFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L24 [type] `public sealed class StaffWorkPriorityPanelUiFactory : IStaffWorkPriorityPanelUiFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L28 [function] `public StaffWorkPriorityPanelUiFactory(ITmpKoreanFontService tmpKoreanFontService)` [DependencyInjection]
- L34 [function] `public RectTransform EnsureRectTransform(GameObject target)` [RuntimeObjectCreation, SceneMutation]
- L47 [function] `public GameObject CreateUiObject(string name, Transform parent)` [RuntimeObjectCreation, SceneMutation]
- L54 [function] `public Image AddImage(GameObject target, Color color)` [RuntimeObjectCreation, SceneMutation]
- L61 [function] `public ScrollRect AddScrollRect(GameObject target)` [RuntimeObjectCreation, SceneMutation]
- L71 [function] `public Mask AddMask(GameObject target, bool showMaskGraphic)` [RuntimeObjectCreation, SceneMutation]
- L78 [function] `public VerticalLayoutGroup AddVerticalLayoutGroup(GameObject target)` [RuntimeObjectCreation, SceneMutation]
- L89 [function] `public HorizontalLayoutGroup AddHorizontalLayoutGroup(GameObject target)` [RuntimeObjectCreation, SceneMutation]
- L100 [function] `public ContentSizeFitter AddContentSizeFitter(GameObject target)` [RuntimeObjectCreation, SceneMutation]
- L108 [function] `public LayoutElement AddLayoutElement(GameObject target, float width, float height)` [RuntimeObjectCreation, SceneMutation]
- L118 [function] `public Button AddButton(GameObject target, Graphic targetGraphic)` [RuntimeObjectCreation, SceneMutation]
- L125 [function] `public TMP_Text AddText(GameObject target)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L132 [function] `public Shadow AddShadow(GameObject target, Color effectColor, Vector2 effectDistance)` [RuntimeObjectCreation, SceneMutation]
- L140 [function] `public void ApplyFonts(Transform root)` [DependencyInjection]
- L145 [function] `public void Release(GameObject target)` [RuntimeObjectCreation, SceneMutation]


## Assets\Scripts\Character\Work

### Assets/Scripts/Character/Work/CharacterWorkRoleUtility.cs

- L1 [type] `public static class CharacterWorkRoleUtility`
- L3 [function] `public static bool TryGetWork(CharacterActor actor, out AbilityWork work)`
- L11 [function] `public static bool IsWorker(CharacterActor actor)`
- L16 [function] `public static bool IsOnDutyWorker(CharacterActor actor)`

### Assets/Scripts/Character/Work/StaffDiscontentSystem.cs

- L6 [type] `public enum StaffDiscontentStage` [DependencyInjection, EventBus]
- L16 [type] `public enum StaffDiscontentOutcome` [DependencyInjection, EventBus]
- L27 [type] `public enum StaffRebellionResponseType` [DependencyInjection, EventBus]
- L36 [type] `public class StaffDiscontentRules` [DependencyInjection, EventBus]
- L52 [function] `public static StaffDiscontentRules CreateDefault()` [DependencyInjection, EventBus]
- L58 [type] `public sealed class StaffDiscontentSnapshot` [DependencyInjection, EventBus]
- L73 [function] `public string ToSummaryText()` [DependencyInjection, EventBus]
- L79 [type] `public sealed class StaffDiscontentRecord` [DependencyInjection, EventBus]
- L81 [function] `public StaffDiscontentRecord(int staffId, CharacterActor staff)` [DependencyInjection, EventBus]
- L100 [function] `public StaffDiscontentOutcome Update(CharacterActor staff, StaffDiscontentRules rules)` [DependencyInjection, EventBus]
- L165 [function] `public bool MarkIsolated()` [DependencyInjection, EventBus]
- L177 [function] `public bool MarkSuppressed()` [DependencyInjection, EventBus]
- L191 [function] `public bool TryCalm(CharacterActor staff, StaffDiscontentRules rules, out string failureReason)` [DependencyInjection, EventBus]
- L221 [function] `public StaffDiscontentSnapshot ToSnapshot(StaffDiscontentOutcome outcome = StaffDiscontentOutcome.None)` [DependencyInjection, EventBus]
- L241 [type] `public readonly struct StaffRebellionResponseResult` [DependencyInjection, EventBus]
- L264 [type] `public sealed class StaffDiscontentState` [DependencyInjection, EventBus]
- L270 [function] `public StaffDiscontentRecord ProcessStaff(CharacterActor staff, StaffDiscontentRules rules, out StaffDiscontentOutcome outcome)` [DependencyInjection, EventBus]
- L284 [function] `public bool TryGetRecord(CharacterActor staff, out StaffDiscontentRecord record)` [DependencyInjection, EventBus]
- L295 [function] `public bool IsPermanentLoss(CharacterActor staff)` [DependencyInjection, EventBus]
- L300 [function] `private StaffDiscontentRecord GetOrCreate(int staffId, CharacterActor staff)` [DependencyInjection, EventBus]
- L312 [type] `public struct StaffDiscontentChangedEvent` [DependencyInjection, EventBus]
- L316 [function] `public StaffDiscontentChangedEvent(StaffDiscontentSnapshot snapshot)` [DependencyInjection, EventBus]
- L323 [function] `public static void Trigger(StaffDiscontentSnapshot snapshot)` [DependencyInjection, EventBus]
- L330 [type] `public struct StaffPermanentLossEvent` [DependencyInjection, EventBus]
- L334 [function] `public StaffPermanentLossEvent(StaffDiscontentSnapshot snapshot)` [DependencyInjection, EventBus]
- L341 [function] `public static void Trigger(StaffDiscontentSnapshot snapshot)` [DependencyInjection, EventBus]
- L348 [type] `public struct StaffRebellionResponseEvent` [DependencyInjection, EventBus]
- L352 [function] `public StaffRebellionResponseEvent(StaffRebellionResponseResult result)` [DependencyInjection, EventBus]
- L359 [function] `public static void Trigger(StaffRebellionResponseResult result)` [DependencyInjection, EventBus]
- L366 [type] `public static class StaffDiscontentService` [DependencyInjection, EventBus]
- L368 [function] `public static bool IsTrackableStaff(CharacterActor staff)` [DependencyInjection, EventBus]
- L378 [function] `public static int GetStaffId(CharacterActor staff)` [DependencyInjection, EventBus]
- L389 [function] `public static string GetStaffDisplayName(CharacterActor staff, int staffId)` [DependencyInjection, EventBus]
- L405 [function] `public static float GetMood(CharacterActor staff)` [DependencyInjection, EventBus]
- L418 [function] `public static StaffDiscontentStage EvaluateStage(float mood, int lowMoodDays, StaffDiscontentRules rules)` [DependencyInjection, EventBus]
- L454 [function] `public static float GetWorkEfficiencyMultiplier(StaffDiscontentStage stage, StaffDiscontentRules rules)` [DependencyInjection, EventBus]
- L468 [function] `public static bool ShouldBlockWork(StaffDiscontentStage stage)` [DependencyInjection, EventBus]
- L475 [function] `public static string GetBlockReason(StaffDiscontentStage stage)` [DependencyInjection, EventBus]
- L488 [type] `public class StaffDiscontentRuntime : MonoBehaviour, UtilEventListener<OperatingDayEndedEvent>` [DependencyInjection, EventBus]
- L499 [function] `public void Construct(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L505 [function] `public void OnTriggerEvent(OperatingDayEndedEvent eventType)` [DependencyInjection, EventBus]
- L507 [function] `ProcessAllStaff()` [DependencyInjection, EventBus]
- L510 [function] `public StaffDiscontentRecord ProcessStaff(CharacterActor staff, out StaffDiscontentOutcome outcome)` [DependencyInjection, EventBus]
- L518 [function] `ApplyOutcome(staff, record, outcome)` [DependencyInjection, EventBus]
- L521 [function] `DispatchAutoSuppress(staff)` [DependencyInjection, EventBus]
- L526 [function] `public void ProcessAllStaff()` [DependencyInjection, EventBus]
- L531 [function] `ProcessStaff(staff, out _)` [DependencyInjection, EventBus]
- L535 [function] `public float GetWorkEfficiencyMultiplier(CharacterActor staff)` [DependencyInjection, EventBus]
- L548 [function] `public bool ShouldBlockWork(CharacterActor staff, out string reason)` [DependencyInjection, EventBus]
- L568 [function] `public bool IsRebellionTarget(CharacterActor target)` [DependencyInjection, EventBus]
- L576 [function] `public int DispatchAutoSuppress(CharacterActor rebel)` [DependencyInjection, EventBus]
- L630 [function] `public bool TryIsolateRebel(CharacterActor rebel, CharacterActor actor, out StaffRebellionResponseResult result)` [DependencyInjection, EventBus]
- L652 [function] `public bool TryCalmStaff(CharacterActor staff, CharacterActor actor, out StaffRebellionResponseResult result)` [DependencyInjection, EventBus]
- L673 [function] `public bool ResolveSuppressedRebel(CharacterActor rebel, CharacterActor defender)` [DependencyInjection, EventBus]
- L696 [function] `private void ApplyOutcome(CharacterActor staff, StaffDiscontentRecord record, StaffDiscontentOutcome outcome)` [DependencyInjection, EventBus]
- L730 [function] `DispatchAutoSuppress(staff)` [DependencyInjection, EventBus]
- L739 [function] `private IDungeonSceneComponentQuery RequireSceneQuery()` [DependencyInjection]
- L749 [function] `private void OnEnable()` [DependencyInjection, EventBus]
- L754 [function] `private void OnDisable()` [DependencyInjection, EventBus]

### Assets/Scripts/Character/Work/StaffWorkforceQueryService.cs

- L4 [type] `public interface IStaffWorkforceQueryService` [DependencyInjection]
- L6 [function] `IReadOnlyList<CharacterActor> FindActiveWorkers()` [DependencyInjection]
- L7 [function] `bool IsActiveWorker(CharacterActor character)` [DependencyInjection]
- L8 [function] `string GetDisplayName(CharacterActor character)` [DependencyInjection]
- L11 [type] `public sealed class StaffWorkforceRuntimeQueryService : IStaffWorkforceQueryService` [DependencyInjection]
- L15 [function] `public StaffWorkforceRuntimeQueryService(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L20 [function] `public IReadOnlyList<CharacterActor> FindActiveWorkers()` [DependencyInjection]
- L29 [function] `public bool IsActiveWorker(CharacterActor character)` [DependencyInjection]
- L36 [function] `public string GetDisplayName(CharacterActor character)` [DependencyInjection]

### Assets/Scripts/Character/Work/WorkCommandHandler.cs

- L6 [type] `public sealed class WorkCommandHandler` [DependencyInjection]
- L15 [function] `public WorkCommandHandler(AbilityWork work, WorkTargetSelector targetSelector)` [DependencyInjection]
- L26 [function] `public bool TrySetPriorityWorkTarget(BuildableObject building, out string errorMessage)` [DependencyInjection]
- L31 [function] `public bool TrySetPriorityWorkTarget(` [DependencyInjection]
- L61 [function] `public bool TrySetPrioritySuppressTarget(` [DependencyInjection]
- L92 [function] `public bool TryGetPrioritySuppressDestination(GridPathSearchResult searchResult, out BuildableObject destination)` [DependencyInjection]
- L119 [function] `public void ClearPriorityWorkTarget()` [DependencyInjection]
- L127 [function] `public bool HasUrgentPriorityTarget()` [DependencyInjection]
- L139 [function] `public IEnumerator SuppressPriorityTarget()` [DependencyInjection]
- L216 [function] `private bool CanReachSuppressTarget(` [DependencyInjection]

### Assets/Scripts/Character/Work/WorkDebugLog.cs

- L1 [type] `public static class WorkDebugLog`
- L3 [function] `public static void LogProgress(CharacterActor actor)`
- L8 [function] `public static void LogEnd(CharacterActor actor, string reason = null)`
- L19 [function] `private static string GetCharacterName(CharacterActor actor)`

### Assets/Scripts/Character/Work/WorkDutyController.cs

- L4 [type] `public sealed class WorkDutyController` [DependencyInjection]
- L12 [function] `public WorkDutyController(AbilityWork work)` [DependencyInjection]
- L20 [function] `public void InitializeWorkerCondition(CharacterSO data)` [DependencyInjection]
- L27 [function] `EnsureStatAtLeast(CharacterCondition.SLEEP, 85f)` [DependencyInjection]
- L28 [function] `EnsureStatAtLeast(CharacterCondition.MOOD, 75f)` [DependencyInjection]
- L29 [function] `EnsureStatAtLeast(CharacterCondition.FUN, 70f)` [DependencyInjection]
- L30 [function] `EnsureStatAtLeast(CharacterCondition.HUNGER, 80f)` [DependencyInjection]
- L31 [function] `EnsureStatAtLeast(CharacterCondition.EXCRETION, 85f)` [DependencyInjection]
- L32 [function] `EnsureStatAtLeast(CharacterCondition.HYGIENE, 80f)` [DependencyInjection]
- L35 [function] `public bool ShouldUseRestProtection()` [DependencyInjection]
- L84 [function] `public bool CanStartWorkAction()` [DependencyInjection]
- L96 [function] `SetDutyState(AbilityWork.DutyState.OnDuty)` [DependencyInjection]
- L107 [function] `SetDutyState(AbilityWork.DutyState.OnDuty)` [DependencyInjection]
- L117 [function] `BeginOffDuty(string.IsNullOrWhiteSpace(discontentReason)` [DependencyInjection]
- L127 [function] `SetDutyState(AbilityWork.DutyState.OnDuty)` [DependencyInjection]
- L137 [function] `BeginOffDuty("???????????)` [DependencyInjection]
- L149 [function] `public bool ShouldTakeOffDuty()` [DependencyInjection]
- L167 [function] `public bool ShouldReturnToWork()` [DependencyInjection]
- L189 [function] `public void BeginOffDuty(string reason)` [DependencyInjection]
- L199 [function] `SetDutyState(AbilityWork.DutyState.OffDuty)` [DependencyInjection]
- L213 [function] `public void PrepareForExpedition()` [DependencyInjection]
- L217 [function] `SetDutyState(AbilityWork.DutyState.OnDuty)` [DependencyInjection]
- L221 [function] `public void SetDutyState(AbilityWork.DutyState nextState)` [DependencyInjection]
- L262 [function] `public void ApplyWorkFatigueTick()` [DependencyInjection]
- L273 [function] `public IEnumerator CheckActionWork(int runId)` [DependencyInjection]
- L281 [function] `ApplyWorkFatigueTick()` [DependencyInjection]
- L327 [function] `public bool ShouldInterruptCurrentWork(out string interruptReason)` [DependencyInjection]
- L356 [function] `BeginOffDuty("????筌믩끃횧 ????Β??????썹땟??)` [DependencyInjection]
- L364 [function] `private bool ShouldEndRoutineWorkShift(float startedAt, out string reason)` [DependencyInjection]
- L386 [function] `public bool CanContinueAssignedWork(out string stopReason)` [DependencyInjection]
- L435 [function] `private void EnsureStatAtLeast(CharacterCondition condition, float value)` [DependencyInjection]
- L446 [function] `private float GetStat(CharacterCondition condition, float defaultValue)` [DependencyInjection]
- L459 [function] `private CharacterStats GetWorkerStats()` [DependencyInjection]

### Assets/Scripts/Character/Work/WorkforceReplanService.cs

- L5 [type] `public interface IWorkforceReplanService` [DependencyInjection]
- L7 [function] `void RequestIdleWorkersToReplan(bool clearFailures = true)` [DependencyInjection]
- L10 [type] `public sealed class DungeonWorkforceReplanService : IWorkforceReplanService` [DependencyInjection]
- L14 [function] `public DungeonWorkforceReplanService(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L20 [function] `public void RequestIdleWorkersToReplan(bool clearFailures = true)` [DependencyInjection]

### Assets/Scripts/Character/Work/WorkGridUtility.cs

- L4 [type] `public interface IWorkGridResolver` [DependencyInjection, SceneMutation]
- L14 [type] `public sealed class WorkGridResolver : IWorkGridResolver` [DependencyInjection, SceneMutation]
- L18 [function] `public WorkGridResolver(IGridSystemProvider gridSystemProvider)` [DependencyInjection, SceneMutation]
- L24 [function] `public Grid ResolveActiveGrid(` [DependencyInjection, SceneMutation]
- L50 [function] `public Vector2Int GetGridPosition(Grid activeGrid, CharacterActor actor)` [DependencyInjection, SceneMutation]

### Assets/Scripts/Character/Work/WorkPriorityProfile.cs

- L5 [type] `public enum WorkPriorityLevel` [DependencyInjection]
- L13 [type] `public static class WorkPriorityLevelExtensions` [DependencyInjection]
- L15 [function] `public static WorkPriorityLevel Next(this WorkPriorityLevel priority)` [DependencyInjection]
- L26 [function] `public static float GetBaseScore(this WorkPriorityLevel priority)` [DependencyInjection]
- L37 [function] `public static string ToDisplayText(this WorkPriorityLevel priority)` [DependencyInjection]
- L49 [type] `public static class WorkTaskCatalog` [DependencyInjection]
- L63 [function] `public static string GetDisplayName(FacilityWorkType workType)` [DependencyInjection]
- L79 [function] `public static IEnumerable<FacilityWorkType> GetSingleTypes(FacilityWorkType workTypes)` [DependencyInjection]
- L91 [type] `public static class WorkCommandResolver` [DependencyInjection]
- L168 [function] `public static bool IsSuppressTarget(CharacterActor target, Predicate<CharacterActor> isRebellionTarget)` [DependencyInjection]
- L176 [function] `public static bool TryResolveSuppressCommand(` [DependencyInjection]
- L244 [function] `private static bool NeedsRestock(BuildableObject target)` [DependencyInjection]
- L262 [type] `public class WorkPriorityProfile` [DependencyInjection]
- L273 [function] `public static WorkPriorityProfile CreateDefault()` [DependencyInjection]
- L278 [function] `public WorkPriorityProfile Clone()` [DependencyInjection]
- L293 [function] `public WorkPriorityLevel GetPriority(FacilityWorkType workType)` [DependencyInjection]
- L309 [function] `public void SetPriority(FacilityWorkType workType, WorkPriorityLevel priority)` [DependencyInjection]
- L340 [function] `public bool IsEnabled(FacilityWorkType workType)` [DependencyInjection]
- L345 [function] `public void ApplyPreferredTypes(FacilityWorkType preferredTypes)` [DependencyInjection]
- L352 [function] `SetPriority(type, WorkPriorityLevel.Priority1)` [DependencyInjection]
- L357 [function] `private WorkPriorityLevel GetBestPriority(FacilityWorkType workTypes)` [DependencyInjection]

### Assets/Scripts/Character/Work/WorkTargetCandidate.cs

- L1 [type] `public readonly struct WorkTargetCandidate`

### Assets/Scripts/Character/Work/WorkTargetSelector.cs

- L5 [type] `public sealed class WorkTargetSelector`
- L10 [function] `public WorkTargetSelector(AbilityWork work)`
- L78 [function] `TryGetBestCandidate(requestedWorkType, searchResult, out WorkTargetCandidate best)`
- L83 [function] `public bool CanUseAsWorkTarget(BuildableObject building)`
- L88 [function] `public bool CanUseAsWorkTarget(BuildableObject building, FacilityWorkType requestedWorkType)`
- L117 [function] `TryEvaluateWorkTarget(building, searchResult, requestedWorkType, false, out WorkTargetCandidate candidate)`
- L140 [function] `public float GetUtilityScore(FacilityWorkType requestedWorkType, GridPathSearchResult searchResult)`
- L150 [function] `public IEnumerable<BuildableObject> GetReachableBuildings(GridPathSearchResult searchResult)`
- L168 [function] `public IEnumerable<IWarehouseFacility> FindReachableWarehouses(GridPathSearchResult searchResult = null)`
- L352 [function] `private static int GetFailureRelevance(WorkTargetCandidate candidate)`
- L365 [function] `private static float GetDistanceScore(BuildableObject building, GridPathSearchResult searchResult)`
- L384 [function] `private static bool CanUseSuppressFor(FacilityWorkType requestedWorkType)`

### Assets/Scripts/Character/Work/WorkTaskExecutor.cs

- L6 [type] `public sealed class WorkTaskExecutor` [DependencyInjection, SceneMutation]
- L13 [function] `public WorkTaskExecutor(AbilityWork work, WorkTargetSelector targetSelector)` [DependencyInjection, SceneMutation]
- L19 [function] `public IEnumerator Work(int runId)` [DependencyInjection, SceneMutation]
- L34 [function] `EndAiAction(actor, currentAction)` [DependencyInjection, SceneMutation]
- L43 [function] `FinishWorkRun(actor, currentAction)` [DependencyInjection, SceneMutation]
- L51 [function] `AbortWorkRun(runId, actor, currentAction)` [DependencyInjection, SceneMutation]
- L65 [function] `AbortWorkRun(runId, actor, currentAction)` [DependencyInjection, SceneMutation]
- L79 [function] `AbortWorkRun(runId, actor, currentAction)` [DependencyInjection, SceneMutation]
- L91 [function] `AbortWorkRun(runId, actor, currentAction)` [DependencyInjection, SceneMutation]
- L105 [function] `AbortWorkRun(runId, actor, currentAction)` [DependencyInjection, SceneMutation]
- L134 [function] `EndAiAction(actor, currentAction)` [DependencyInjection, SceneMutation]
- L138 [function] `private bool HasReachedAssignedWorkTarget(CharacterActor actor, Grid grid)` [DependencyInjection, SceneMutation]
- L183 [function] `AbortWorkRun(runId, actor, currentAction)` [DependencyInjection, SceneMutation]
- L194 [function] `ReturnCarriedStock(warehouse, saleItem, carriedAmount)` [DependencyInjection, SceneMutation]
- L195 [function] `AbortWorkRun(runId, actor, currentAction)` [DependencyInjection, SceneMutation]
- L211 [function] `ReturnCarriedStock(warehouse, saleItem, carriedAmount)` [DependencyInjection, SceneMutation]
- L212 [function] `AbortWorkRun(runId, actor, currentAction)` [DependencyInjection, SceneMutation]
- L226 [function] `ReturnCarriedStock(warehouse, saleItem, carriedAmount)` [DependencyInjection, SceneMutation]
- L235 [function] `ReturnCarriedStock(warehouse, saleItem, carriedAmount)` [DependencyInjection, SceneMutation]
- L236 [function] `AbortWorkRun(runId, actor, currentAction)` [DependencyInjection, SceneMutation]
- L244 [function] `ReturnCarriedStock(warehouse, saleItem, leftover)` [DependencyInjection, SceneMutation]
- L386 [function] `public IEnumerator ExecuteRestockWork()` [DependencyInjection, SceneMutation]
- L404 [function] `public IEnumerator ExecuteRepairWork()` [DependencyInjection, SceneMutation]
- L421 [function] `public IEnumerator ExecuteResearchWork()` [DependencyInjection, SceneMutation]
- L446 [function] `private static void EndAiAction(CharacterActor actor, AIAction currentAction)` [DependencyInjection, SceneMutation]
- L455 [function] `private void FinishWorkRun(CharacterActor actor, AIAction currentAction)` [DependencyInjection, SceneMutation]
- L471 [function] `private bool ShouldAbortWorkRun(int runId, CharacterActor actor)` [DependencyInjection, SceneMutation]
- L479 [function] `private void AbortWorkRun(int runId, CharacterActor actor, AIAction currentAction)` [DependencyInjection, SceneMutation]


## Assets\Scripts\Codex

### Assets/Scripts/Codex/CodexEvolutionRecorder.cs

- L1 [type] `public static class CodexEvolutionRecorder`
- L3 [function] `public static void Record(CodexState state, FacilityEvolutionResult result)`
- L36 [function] `AddLineIfNotBlank(state, resultBuilding, "?癲ル슢캉????, proposal.FacilityIdentitySummary);`
- L37 [function] `AddLineIfNotBlank(state, resultBuilding, "?꿔꺂????????뚯????덈춣?, proposal.FlavorText);`
- L38 [function] `AddLineIfNotBlank(state, resultBuilding, "????ㅻ샑筌????Β?ы닎??, proposal.Source);`
- L41 [function] `AddLineIfNotBlank(state, resultBuilding, "??⑤슢堉???, mutationText);`

### Assets/Scripts/Codex/CodexFacilityInfoWriter.cs

- L1 [type] `public static class CodexFacilityInfoWriter`
- L3 [function] `public static void Add(CodexState state, BuildingSO building, string info, CodexInfoSource source)`
- L18 [function] `public static string GetFacilityEntryId(BuildingSO building)`

### Assets/Scripts/Codex/CodexInvasionObservationMapper.cs

- L3 [type] `public static class CodexInvasionObservationMapper`
- L5 [function] `public static IEnumerable<string> FromEffectTag(string tag)`
- L39 [function] `public static string NormalizeObservation(string observation)`

### Assets/Scripts/Codex/CodexInvasionRecorder.cs

- L3 [type] `public static class CodexInvasionRecorder`
- L7 [function] `public static void RecordDefenseObservation(CodexState state, DefenseActivationReport report)`
- L15 [function] `SeedBreakthroughIntruder(state);`
- L20 [function] `AddInvasionInfo(state, info, CodexInfoSource.Observation);`
- L26 [function] `AddInvasionInfo(state, "?????? ??醫딆┫???, CodexInfoSource.Observation);`
- L31 [function] `AddInvasionInfo(state, "?????? ???筌뤿굝琉?????썹땟??, CodexInfoSource.Observation);`
- L35 [function] `public static void RecordCombatReport(CodexState state, InvasionCombatReport report)`
- L42 [function] `SeedBreakthroughIntruder(state);`
- L54 [function] `AddInvasionInfo(state, "?嚥싲갭큔??? ??嶺?筌?????????????얠?", CodexInfoSource.Observation);`
- L58 [function] `public static void RecordFacilityDamage(CodexState state, BuildableObject facility)`
- L61 [function] `AddInvasionInfo(state, "?嚥싲갭큔??? ??嶺?筌?????????????얠?", CodexInfoSource.Observation);`
- L64 [function] `public static void SeedBreakthroughIntruder(CodexState state)`
- L81 [function] `private static void AddInvasionInfo(CodexState state, string info, CodexInfoSource source)`
- L88 [function] `SeedBreakthroughIntruder(state);`

### Assets/Scripts/Codex/CodexObservationRecorder.cs

- L3 [type] `public static class CodexObservationRecorder`
- L5 [function] `public static void ObserveCharacter(CodexState state, CharacterActor actor)`
- L28 [function] `public static void ObserveSpecies(CodexState state, CharacterSpeciesSO species, CodexInfoSource source)`
- L41 [function] `AddIfNotBlank(entry, species.shortDescription, source);`
- L44 [function] `AddIfNotBlank(entry, $"????ъ군濚? {preferred}", source);`
- L49 [function] `AddIfNotBlank(entry, $"???뚯????? {disliked}", source);`
- L52 [function] `AddIfNotBlank(entry, $"????????꾣뤃?? {species.incidentName}", source);`
- L53 [function] `AddIfNotBlank(entry, species.incidentDescription, source);`
- L60 [function] `public static void ObserveFacility(CodexState state, BuildableObject facility, CodexInfoSource source)`
- L67 [function] `ObserveFacility(state, facility.BuildingData, source);`
- L70 [function] `public static void ObserveFacility(CodexState state, BuildingSO building, CodexInfoSource source)`
- L128 [function] `private static void AddIfNotBlank(CodexEntryRecord entry, string text, CodexInfoSource source)`
- L136 [function] `private static string GetMonsterEntryId(string speciesTag)`

### Assets/Scripts/Codex/CodexRecipeRecorder.cs

- L4 [type] `public static class CodexRecipeRecorder` [DependencyInjection]
- L6 [function] `public static void RecordResearch(CodexState state, BlueprintResearchUnlockResult unlockResult, BlueprintResearchState researchState, IFacilitySynthesisRecipeQuery synthesisRecipeQuery, IFacilityShopCatalog facilityShopCatalog)` [DependencyInjection]
- L45 [function] `public static void RecordSynthesis(CodexState state, FacilitySynthesisResult result, BlueprintResearchState researchState, IFacilitySynthesisRecipeQuery synthesisRecipeQuery)` [DependencyInjection]
- L61 [function] `public static void ImportSynthesisRecipes(CodexState state, BlueprintResearchState researchState, IFacilitySynthesisRecipeQuery synthesisRecipeQuery)` [DependencyInjection]
- L90 [function] `private static void AddRecipeInfo(CodexState state, FacilitySynthesisRecipeSO recipe, bool unlocked, CodexInfoSource source)` [DependencyInjection]
- L114 [function] `private static void AddSpecialRecipeHint(CodexState state, FacilitySynthesisRecipeSO recipe)` [DependencyInjection]
- L129 [function] `private static string BuildSpecialRecipeHint(FacilitySynthesisRecipeSO recipe)` [DependencyInjection]

### Assets/Scripts/Codex/CodexRecordSummaryQuery.cs

- L3 [type] `public readonly struct CodexRecordSummary`
- L29 [type] `public interface ICodexRecordSummaryService` [DependencyInjection]
- L34 [type] `public interface ICodexRecordSummaryRuntimeSource` [DependencyInjection]
- L41 [type] `public sealed class CodexRecordSummaryRuntimeSource : ICodexRecordSummaryRuntimeSource` [DependencyInjection]
- L45 [function] `public CodexRecordSummaryRuntimeSource(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L55 [type] `public sealed class CodexRecordSummaryService : ICodexRecordSummaryService` [DependencyInjection]
- L59 [function] `public CodexRecordSummaryService(ICodexRecordSummaryRuntimeSource runtimeSource)` [DependencyInjection]
- L64 [function] `public CodexRecordSummary Capture()` [DependencyInjection]

### Assets/Scripts/Codex/CodexReferenceImporter.cs

- L3 [type] `public interface ICodexReferenceImporter` [DependencyInjection]
- L5 [function] `void Import(CodexState state, BlueprintResearchState researchState)` [DependencyInjection]
- L8 [type] `public sealed class CodexReferenceImporter : ICodexReferenceImporter` [DependencyInjection]
- L13 [function] `public CodexReferenceImporter(ICodexReferenceCatalog catalog, IFacilitySynthesisRecipeQuery synthesisRecipeQuery)` [DependencyInjection]
- L23 [function] `public void Import(CodexState state, BlueprintResearchState researchState)` [DependencyInjection]

### Assets/Scripts/Codex/CodexSystem.cs

- L7 [type] `public enum CodexEntryCategory` [DependencyInjection, EventBus]
- L14 [type] `public enum CodexInfoSource` [DependencyInjection, EventBus]
- L23 [type] `public readonly struct CodexInfoLine` [DependencyInjection, EventBus]
- L25 [function] `public CodexInfoLine(string text, CodexInfoSource source)` [DependencyInjection, EventBus]
- L35 [type] `public sealed class CodexEntrySnapshot` [DependencyInjection, EventBus]
- L43 [function] `public string ToDisplayText()` [DependencyInjection, EventBus]
- L63 [type] `public sealed class CodexEntryRecord` [DependencyInjection, EventBus]
- L68 [function] `public CodexEntryRecord(CodexEntryCategory category, string entryId, string title)` [DependencyInjection, EventBus]
- L82 [function] `public void Rename(string title)` [DependencyInjection, EventBus]
- L90 [function] `public bool AddInfo(string text, CodexInfoSource source)` [DependencyInjection, EventBus]
- L108 [function] `public CodexEntrySnapshot ToSnapshot()` [DependencyInjection, EventBus]
- L121 [type] `public sealed class CodexState` [DependencyInjection, EventBus]
- L127 [function] `public CodexEntryRecord GetOrCreate(CodexEntryCategory category, string entryId, string title)` [DependencyInjection, EventBus]
- L143 [function] `public bool AddInfo(CodexEntryCategory category, string entryId, string title, string info, CodexInfoSource source)` [DependencyInjection, EventBus]
- L148 [function] `public bool HasInfo(CodexEntryCategory category, string entryId, string info)` [DependencyInjection, EventBus]
- L155 [function] `public IReadOnlyList<CodexEntrySnapshot> GetSnapshots(CodexEntryCategory category)` [DependencyInjection, EventBus]
- L164 [function] `public CodexEntrySnapshot GetSnapshot(CodexEntryCategory category, string entryId)` [DependencyInjection, EventBus]
- L171 [function] `private static string GetKey(CodexEntryCategory category, string entryId)` [DependencyInjection, EventBus]
- L177 [type] `public struct CodexUpdatedEvent` [DependencyInjection, EventBus]
- L182 [function] `public CodexUpdatedEvent(CodexEntryCategory category, string entryId)` [DependencyInjection, EventBus]
- L190 [function] `public static void Trigger(CodexEntryCategory category, string entryId)` [DependencyInjection, EventBus]
- L198 [type] `public static class CodexService` [DependencyInjection, EventBus]
- L202 [function] `public static void ImportReferenceData(CodexState state, BlueprintResearchState researchState, ICodexReferenceImporter referenceImporter)` [DependencyInjection, EventBus]
- L215 [function] `public static void ObserveCharacter(CodexState state, CharacterActor actor)` [DependencyInjection, EventBus]
- L220 [function] `public static void ObserveSpecies(CodexState state, CharacterSpeciesSO species, CodexInfoSource source)` [DependencyInjection, EventBus]
- L225 [function] `public static void ObserveFacility(CodexState state, BuildableObject facility, CodexInfoSource source)` [DependencyInjection, EventBus]
- L230 [function] `public static void ObserveFacility(CodexState state, BuildingSO building, CodexInfoSource source)` [DependencyInjection, EventBus]
- L235 [function] `public static void RecordDefenseObservation(CodexState state, DefenseActivationReport report)` [DependencyInjection, EventBus]
- L240 [function] `public static void RecordCombatReport(CodexState state, InvasionCombatReport report)` [DependencyInjection, EventBus]
- L245 [function] `public static void RecordFacilityDamage(CodexState state, BuildableObject facility)` [DependencyInjection, EventBus]
- L250 [function] `public static void RecordResearch(CodexState state, BlueprintResearchUnlockResult unlockResult, BlueprintResearchState researchState, IFacilitySynthesisRecipeQuery synthesisRecipeQuery, IFacilityShopCatalog facilityShopCatalog)` [DependencyInjection, EventBus]
- L265 [function] `public static void RecordSynthesis(CodexState state, FacilitySynthesisResult result, BlueprintResearchState researchState, IFacilitySynthesisRecipeQuery synthesisRecipeQuery)` [DependencyInjection, EventBus]
- L274 [function] `public static void RecordEvolution(CodexState state, FacilityEvolutionResult result)` [DependencyInjection, EventBus]
- L279 [function] `public static void ImportSynthesisRecipes(CodexState state, BlueprintResearchState researchState, IFacilitySynthesisRecipeQuery synthesisRecipeQuery)` [DependencyInjection, EventBus]
- L287 [function] `public static void SeedBreakthroughIntruder(CodexState state)` [DependencyInjection, EventBus]
- L294 [type] `public class CodexRuntime :` [DependencyInjection, EventBus]
- L317 [function] `public void ConstructCodexRuntime(IBlueprintResearchStateService blueprintResearchStateService, ICodexReferenceImporter referenceImporter, IFacilitySynthesisRecipeQuery synthesisRecipeQuery, IFacilityShopCatalog facilityShopCatalog)` [DependencyInjection, EventBus]
- L338 [function] `private void Start()` [DependencyInjection, EventBus]
- L346 [function] `public void ImportReferenceData()` [DependencyInjection, EventBus]
- L354 [function] `private IBlueprintResearchStateService ResolveResearchStateService()` [DependencyInjection, EventBus]
- L360 [function] `private ICodexReferenceImporter ResolveReferenceImporter()` [DependencyInjection, EventBus]
- L366 [function] `private IFacilitySynthesisRecipeQuery ResolveSynthesisRecipeQuery()` [DependencyInjection, EventBus]
- L372 [function] `private IFacilityShopCatalog ResolveFacilityShopCatalog()` [DependencyInjection, EventBus]
- L378 [function] `public IReadOnlyList<CodexEntrySnapshot> GetEntries(CodexEntryCategory category)` [DependencyInjection, EventBus]
- L383 [function] `public void OnTriggerEvent(FacilityVisitEvent eventType)` [DependencyInjection, EventBus]
- L391 [function] `public void OnTriggerEvent(DefenseFacilityTriggeredEvent eventType)` [DependencyInjection, EventBus]
- L398 [function] `public void OnTriggerEvent(InvasionCombatReportReadyEvent eventType)` [DependencyInjection, EventBus]
- L404 [function] `public void OnTriggerEvent(InvasionFacilityDamagedEvent eventType)` [DependencyInjection, EventBus]
- L410 [function] `public void OnTriggerEvent(InvasionSpawnedEvent eventType)` [DependencyInjection, EventBus]
- L417 [function] `public void OnTriggerEvent(BlueprintResearchCompletedEvent eventType)` [DependencyInjection, EventBus]
- L428 [function] `public void OnTriggerEvent(FacilitySynthesisCompletedEvent eventType)` [DependencyInjection, EventBus]
- L434 [function] `public void OnTriggerEvent(FacilityEvolutionCompletedEvent eventType)` [DependencyInjection, EventBus]
- L440 [function] `private void OnEnable()` [DependencyInjection, EventBus]
- L452 [function] `private void OnDisable()` [DependencyInjection, EventBus]

### Assets/Scripts/Codex/CodexTextFormatter.cs

- L5 [type] `public static class CodexTextFormatter` [Reflection]
- L7 [function] `public static string FormatEvolutionMutationTags(IReadOnlyList<string> mutationTags)` [Reflection]
- L19 [function] `public static string FormatFacilityRoles(FacilityRole roles)` [Reflection]
- L27 [function] `public static string FormatWorkTypes(FacilityWorkType workTypes)` [Reflection]
- L35 [function] `public static string FormatDefenseConcept(DefenseAttackConcept concept)` [Reflection]
- L49 [function] `public static string FormatTriggerTimings(DefenseTriggerTiming timings)` [Reflection]
- L69 [function] `public static string FormatTargetRule(DefenseTargetRule targetRule)` [Reflection]
- L81 [function] `public static IEnumerable<string> FormatDefenseEffects(DefenseFacilityData defense)` [Reflection]
- L94 [function] `private static string FormatFacilityRole(FacilityRole role)` [Reflection]
- L111 [function] `private static string FormatWorkType(FacilityWorkType workType)` [Reflection]
- L127 [function] `private static string FormatEffect(DefenseEffectKind kind, float amount, float duration, int stacks)` [Reflection]


## Assets\Scripts\Codex\Editor

### Assets/Scripts/Codex/Editor/CodexDebugScenarios.cs

- L8 [type] `public static class CodexDebugScenarios` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L11 [function] `public static void RunFromMenu()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L20 [function] `public static bool RunAll(bool logSuccess)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L27 [function] `RunScenario("????꾤뙴洹〓쑏????뚯??? ?????????, VerifyReferenceCodexData, errors)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L28 [function] `RunScenario("????????됰슦???????????ㅻ쿋??? ????쇰뮛?????繹먮굝鍮?, VerifySpecialRecipeHintAndResearchReveal, errors)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L29 [function] `RunScenario("?熬곣뫖?삥납??關?????援온????⑤㈇?뚧납????????꾤뙴洹〓쑏?, VerifyDefenseObservationUpdatesInvasionCodex, errors)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L30 [function] `RunScenario("???嶺??熬곣뫖?삥납??좂뙴??꿔꺂?????녿쫯??????꾤뙴洹〓쑏?, VerifyFacilityVisitUpdatesMonsterCodex, errors)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L31 [function] `RunScenario("??嶺?筌??꿔꺂?????????꾤뙴洹〓쑏????뚯????덈춣?, VerifyFacilityEvolutionUpdatesFacilityCodex, errors)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L32 [function] `RunScenario("????꾤뙴洹〓쑏?UI ?????, VerifyCodexPanelRendering, errors)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L52 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L66 [function] `private static bool VerifyReferenceCodexData()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L88 [function] `private static bool VerifySpecialRecipeHintAndResearchReveal()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L110 [function] `private static bool VerifyDefenseObservationUpdatesInvasionCodex()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L130 [function] `private static bool VerifyFacilityVisitUpdatesMonsterCodex()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L147 [function] `private static bool VerifyFacilityEvolutionUpdatesFacilityCodex()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L196 [function] `private static bool VerifyCodexPanelRendering()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L215 [function] `private static bool ContainsLine(CodexEntrySnapshot entry, string line)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L222 [function] `private static bool ContainsLinePart(CodexEntrySnapshot entry, string text)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L229 [function] `private static BuildingSO LoadBuilding(string assetName)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L234 [function] `private static CharacterSO LoadCharacter(string assetName)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L245 [type] `private sealed class CodexScenarioWorld : IDisposable` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L249 [function] `public CodexRuntime CreateRuntime()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L258 [function] `public BuildableObject CreateFacility(string assetName)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L273 [function] `public DefenseFacility CreateDefenseFacility(string assetName)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L278 [function] `public CharacterActor CreateCharacter(string assetName)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L288 [function] `public void TrackObject(GameObject obj)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L296 [function] `public void Dispose()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]


## Assets\Scripts\Codex\UI

### Assets/Scripts/Codex/UI/CodexPanel.cs

- L7 [type] `public class CodexPanel : MonoBehaviour, UtilEventListener<CodexUpdatedEvent>` [DependencyInjection, Reflection, EventBus, SceneMutation]
- L16 [function] `public void Construct(ICodexRuntimeProvider runtimeProvider)` [DependencyInjection]
- L22 [function] `public void Bind(CodexRuntime nextRuntime)` [DependencyInjection]
- L28 [function] `internal void BindGeneratedView(TMP_Text summaryText)` [SceneMutation]
- L35 [function] `public void Refresh()` [DependencyInjection, SceneMutation]
- L47 [function] `public void OnTriggerEvent(CodexUpdatedEvent eventType)` [DependencyInjection, EventBus, SceneMutation]
- L52 [function] `private static void AppendCategory(List<CodexInfoLine> lines, string title, IReadOnlyList<CodexEntrySnapshot> entries, int maxEntries)` [SceneMutation]
- L87 [function] `private static IEnumerable<CodexInfoLine> GetSummaryLines(CodexInfoLine[] lines, int maxLines)`
- L94 [function] `private void ApplyText()` [SceneMutation]
- L102 [function] `private CodexRuntime ResolveRuntime()` [DependencyInjection]
- L111 [function] `private void OnEnable()` [EventBus]
- L116 [function] `private void OnDisable()` [EventBus]


## Assets\Scripts

### Assets/Scripts/Data.cs

- L8 [type] `public class Data<T>` [Reflection]
- L22 [function] `public void Initialize(T t)` [Reflection]
- L28 [type] `public class DataList<T>` [Reflection]
- L40 [function] `public void Add(T item)` [Reflection]
- L45 [function] `public void Remove(T item)` [Reflection]


## Assets\Scripts\Defense

### Assets/Scripts/Defense/DefenseEffectSO.cs

- L4 [type] `public class DefenseEffectSO : ScriptableObject`

### Assets/Scripts/Defense/DefenseFacilitySystem.cs

- L7 [type] `public enum DefenseTriggerTiming` [EventBus]
- L16 [type] `public enum DefenseAttackConcept` [EventBus]
- L27 [type] `public enum DefenseTargetRule` [EventBus]
- L35 [type] `public enum DefenseEffectKind` [EventBus]
- L45 [type] `public enum DefenseStatusKind` [EventBus]
- L54 [type] `public class DefenseEffectData` [EventBus]
- L62 [function] `public DefenseEffectData Clone()` [EventBus]
- L76 [type] `public class DefenseFacilityData` [EventBus]
- L92 [function] `public bool SupportsTrigger(DefenseTriggerTiming timing)` [EventBus]
- L98 [type] `public class DefenseActivationReport` [EventBus]
- L102 [function] `public DefenseActivationReport(DefenseFacility facility, CharacterActor target, DefenseTriggerTiming timing)` [EventBus]
- L121 [function] `public void AddDamage(float amount)` [EventBus]
- L126 [function] `public void AddMovementDelay(float seconds)` [EventBus]
- L131 [function] `public void AddEffectTag(string tag)` [EventBus]
- L139 [function] `public string FormatSummary()` [EventBus]
- L150 [type] `public struct DefenseFacilityTriggeredEvent` [EventBus]
- L154 [function] `public DefenseFacilityTriggeredEvent(DefenseActivationReport report)` [EventBus]
- L161 [function] `public static void Trigger(DefenseActivationReport report)` [EventBus]
- L168 [type] `public class DefenseFacility : Facility` [EventBus]
- L174 [function] `public bool CanTrigger(DefenseTriggerTiming timing, out string failureReason)` [EventBus]
- L211 [function] `public DefenseActivationReport Trigger(CharacterActor intruder, DefenseTriggerTiming timing, IDefenseStatusRuntimeService statusRuntimeService)` [EventBus]
- L231 [type] `public static class DefenseFacilityResolver` [EventBus]
- L233 [function] `public static List<DefenseActivationReport> TriggerAt(Grid grid, CharacterActor intruder, Vector2Int position, DefenseTriggerTiming timing, IDefenseStatusRuntimeService statusRuntimeService)` [EventBus]
- L274 [type] `public static class DefenseEffectResolver` [EventBus]
- L278 [function] `public static void ApplyEffects(DefenseFacility facility, CharacterActor target, DefenseActivationReport report, IDefenseStatusRuntimeService statusRuntimeService)` [EventBus]
- L324 [function] `public static void ApplyEffect(DefenseEffectData effect, CharacterActor target, DefenseStatusRuntime statusRuntime, DefenseActivationReport report, DefenseFacilityData defense)` [EventBus]
- L371 [function] `public static float TickStatuses(CharacterActor target, float deltaSeconds, IDefenseStatusRuntimeService statusRuntimeService)` [EventBus]
- L389 [function] `private static void ApplyDamage(CharacterActor target, DefenseStatusRuntime statusRuntime, DefenseActivationReport report, float amount, string reason)` [EventBus]
- L407 [type] `public class DefenseStatusRuntime : MonoBehaviour` [EventBus]
- L411 [function] `public int ApplyStatus(DefenseStatusKind kind, float value, float duration, int stacks)` [EventBus]
- L428 [function] `public void ClearStatus(DefenseStatusKind kind)` [EventBus]
- L433 [function] `public float GetIncomingDamageMultiplier()` [EventBus]
- L439 [function] `public float Tick(CharacterActor target, float deltaSeconds)` [EventBus]
- L469 [type] `private sealed class DefenseRuntimeStatus` [EventBus]
- L471 [function] `public DefenseRuntimeStatus(DefenseStatusKind kind)` [EventBus]

### Assets/Scripts/Defense/DefenseStatusRuntimeService.cs

- L3 [type] `public interface IDefenseStatusRuntimeService` [RuntimeObjectCreation]
- L5 [function] `DefenseStatusRuntime GetOrAdd(CharacterActor character)` [RuntimeObjectCreation]
- L6 [function] `DefenseStatusRuntime Get(CharacterActor character)` [RuntimeObjectCreation]
- L7 [function] `float TickStatuses(CharacterActor target, float deltaSeconds)` [RuntimeObjectCreation]
- L10 [type] `public sealed class DefenseStatusRuntimeService : IDefenseStatusRuntimeService` [RuntimeObjectCreation]
- L12 [function] `public DefenseStatusRuntime GetOrAdd(CharacterActor character)` [RuntimeObjectCreation]
- L27 [function] `public DefenseStatusRuntime Get(CharacterActor character)` [RuntimeObjectCreation]
- L34 [function] `public float TickStatuses(CharacterActor target, float deltaSeconds)` [RuntimeObjectCreation]


## Assets\Scripts\Defense\Editor

### Assets/Scripts/Defense/Editor/DefenseFacilityDebugScenarios.cs

- L10 [type] `public static class DefenseFacilityDebugScenarios` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L23 [function] `public static void RunFromMenu()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L32 [function] `public static bool RunAll(bool logSuccess)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L37 [function] `RunScenario("?熬곣뫖?삥납??關????嶺?筌??????, VerifyDefenseAssets, errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L38 [function] `RunScenario("SO Effect ????쇨덫??, VerifyEffectAssetsDriveDamage, errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L39 [function] `RunScenario("?꿔꺂??????熬곣뫖利든뜏類ｋ걝?????筌뤿굝琉?? ???嚥??, VerifyTriggerDamageAndEvent, errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L40 [function] `RunScenario("???? ?????嚥싲갭큔???? ???β뼯?蹂λ뤀 ??⑤슢?뽫뵓嫄붋??, VerifyDamagedDisableAndRepair, errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L41 [function] `RunScenario("?????낇뀘??????筌뤿굝琉???⑤슢????, VerifyPoisonCorrosion, errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L42 [function] `RunScenario("???됰Ŋ??????????띾뼎 ?꿔꺂????????筌뤿굝琉?, VerifyFireBurn, errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L43 [function] `RunScenario("?筌????용닽?????ㅻ쿋繹???熬곣뫖?삥납??, VerifyLightningCharge, errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L44 [function] `RunScenario("???????醫딆┫????꿔꺂?????, VerifyIceSlow, errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L45 [function] `RunScenario("?嚥▲굧??????嚥▲굧?????????????????, VerifyGuardRoom, errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L65 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L79 [function] `private static bool VerifyDefenseAssets()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L99 [function] `private static bool VerifyEffectAssetsDriveDamage()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L126 [function] `new Vector2Int(1, 0)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L134 [function] `private static bool VerifyTriggerDamageAndEvent()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L144 [function] `new Vector2Int(1, 0)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L157 [function] `private static bool VerifyDamagedDisableAndRepair()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L168 [function] `new Vector2Int(1, 0)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L179 [function] `private static bool VerifyPoisonCorrosion()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L196 [function] `private static bool VerifyFireBurn()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L209 [function] `private static bool VerifyLightningCharge()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L226 [function] `private static bool VerifyIceSlow()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L235 [function] `new Vector2Int(1, 0)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L243 [function] `private static bool VerifyGuardRoom()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L252 [function] `new Vector2Int(1, 0)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L262 [function] `private static BuildingSO LoadDefense(string assetName)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L268 [function] `private static bool ExecuteRepairForTest(AbilityWork work, BuildableObject target)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L291 [type] `private sealed class DefenseScenarioWorld : IDisposable` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L304 [function] `public DefenseScenarioWorld()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L311 [function] `new TestHallwayOccupant()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L326 [function] `public DefenseFacility PlaceDefense(string assetName, Vector2Int position)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L332 [function] `public DefenseFacility PlaceDefense(BuildingSO buildingData, Vector2Int position)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L357 [function] `public void TrackScriptableObject(ScriptableObject scriptableObject)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L365 [function] `public CharacterActor CreateIntruder(Vector2Int position)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L371 [function] `InitializeCharacter(character, data, position)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L375 [function] `public CharacterActor CreateWorker(Vector2Int position)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L385 [function] `InitializeCharacter(character, data, position)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L390 [function] `public void Dispose()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L404 [function] `private GameObject CreateCharacterObject(string name)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L414 [function] `private void InitializeCharacter(CharacterActor character, CharacterSO data, Vector2Int position)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L424 [type] `private sealed class TestHallwayOccupant : IGridOccupant` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L432 [type] `private sealed class CountingDefenseTriggerListener : UtilEventListener<DefenseFacilityTriggeredEvent>, IDisposable` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L436 [function] `public CountingDefenseTriggerListener()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L441 [function] `public void OnTriggerEvent(DefenseFacilityTriggeredEvent eventType)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L446 [function] `public void Dispose()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Defense/Editor/P1DefenseFacilityAssetBuilder.cs

- L6 [type] `public static class P1DefenseFacilityAssetBuilder` [EditorAssetAccess, Reflection]
- L12 [function] `public static void EnsureP1DefenseAssetsFromMenu()` [EditorAssetAccess, Reflection]
- L14 [function] `EnsureP1DefenseAssets()` [EditorAssetAccess, Reflection]
- L17 [function] `public static void EnsureP1DefenseAssets()` [EditorAssetAccess, Reflection]
- L20 [function] `EnsureSpriteImport("Assets/Images/Placeholders/Defense/defense_spike.png")` [EditorAssetAccess, Reflection]
- L21 [function] `EnsureSpriteImport("Assets/Images/Placeholders/Defense/defense_poison.png")` [EditorAssetAccess, Reflection]
- L22 [function] `EnsureSpriteImport("Assets/Images/Placeholders/Defense/defense_fire.png")` [EditorAssetAccess, Reflection]
- L23 [function] `EnsureSpriteImport("Assets/Images/Placeholders/Defense/defense_lightning.png")` [EditorAssetAccess, Reflection]
- L24 [function] `EnsureSpriteImport("Assets/Images/Placeholders/Defense/defense_ice.png")` [EditorAssetAccess, Reflection]
- L25 [function] `EnsureSpriteImport("Assets/Images/Placeholders/Defense/defense_guard_room.png")` [EditorAssetAccess, Reflection]
- L31 [function] `EnsureAsset(spec)` [EditorAssetAccess, Reflection]
- L38 [function] `private static void EnsureAsset(DefenseAssetSpec spec)` [EditorAssetAccess, Reflection]
- L96 [function] `private static DefenseEffectSO[] EnsureEffectAssets(DefenseAssetSpec spec)` [EditorAssetAccess, Reflection]
- L127 [function] `private static DefenseAssetSpec[] CreateSpecs()` [EditorAssetAccess, Reflection]
- L160 [function] `Effect(DefenseEffectKind.Damage, 6f, 0f, 1, "???筌뤿굝琉?)` [EditorAssetAccess, Reflection]
- L161 [function] `Effect(DefenseEffectKind.Corrosion, 0.25f, 8f, 1, "???낇뀘???)` [EditorAssetAccess, Reflection]
- L178 [function] `Effect(DefenseEffectKind.Damage, 18f, 0f, 1, "???筌뤿굝琉?)` [EditorAssetAccess, Reflection]
- L179 [function] `Effect(DefenseEffectKind.Burn, 2f, 5f, 1, "???????띾뼎")` [EditorAssetAccess, Reflection]
- L196 [function] `Effect(DefenseEffectKind.Damage, 8f, 0f, 1, "???筌뤿굝琉?)` [EditorAssetAccess, Reflection]
- L197 [function] `Effect(DefenseEffectKind.Charge, 10f, 10f, 1, "???ㅻ쿋繹??)` [EditorAssetAccess, Reflection]
- L214 [function] `Effect(DefenseEffectKind.Damage, 5f, 0f, 1, "???筌뤿굝琉?)` [EditorAssetAccess, Reflection]
- L215 [function] `Effect(DefenseEffectKind.Slow, 0.7f, 4f, 1, "??醫딆┫???)` [EditorAssetAccess, Reflection]
- L234 [function] `private static DefenseEffectData Effect(DefenseEffectKind kind, float amount, float duration, int stacks, string logTag)` [EditorAssetAccess, Reflection]
- L246 [function] `private static void EnsureSpriteImport(string path)` [EditorAssetAccess, Reflection]
- L262 [type] `private readonly struct DefenseAssetSpec` [EditorAssetAccess, Reflection]


## Assets\Scripts\Editor

### Assets/Scripts/Editor/ImplementedScenarioDebugRunner.cs

- L8 [type] `public static class ImplementedScenarioDebugRunner` [EditorAssetAccess]
- L14 [function] `public static void RunFromMenu()` [EditorAssetAccess]
- L24 [function] `public static void OpenLastReportFromMenu()` [EditorAssetAccess]
- L36 [function] `public static void RunForBatchMode()` [EditorAssetAccess]
- L53 [function] `GetReportPath()` [EditorAssetAccess]
- L62 [function] `GetJsonReportPath()` [EditorAssetAccess]
- L63 [function] `BuildCrashJson(generatedAt, ex))` [EditorAssetAccess]
- L75 [function] `public static bool RunAll(bool logSummary)` [EditorAssetAccess]
- L77 [function] `EnsureGeneratedAssets()` [EditorAssetAccess]
- L81 [function] `Run("P0 Grid foundation", GridFoundationDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L82 [function] `Run("P1 Facilities and logistics", FacilityDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L83 [function] `Run("P1 Plan character AI", RunPlanCharacterAiScenarios, results)` [EditorAssetAccess]
- L84 [function] `Run("P1 Customer AI", CustomerAiDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L85 [function] `Run("P1 Character model", CharacterModelDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L86 [function] `Run("P1 Owner character", OwnerDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L87 [function] `Run("P1 Work priority", WorkPriorityDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L88 [function] `Run("P1 Priority command", PriorityCommandDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L89 [function] `Run("P1 Staff duty", StaffDutyDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L90 [function] `Run("P1 Operating day", OperatingDaySettlementDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L91 [function] `Run("P1 Character feedback", CharacterFeedbackDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L92 [function] `Run("P1 Event alerts", EventAlertDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L93 [function] `Run("P1 Invasion threat", InvasionThreatDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L94 [function] `Run("P1 Intruder", InvasionIntruderDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L95 [function] `Run("P1 Defense facilities", DefenseFacilityDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L96 [function] `Run("P1 Combat report", InvasionCombatReportDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L97 [function] `Run("P1 Facility shop", FacilityShopDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L98 [function] `Run("P1 Blueprint research", BlueprintResearchDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L99 [function] `Run("P1 Facility synthesis", FacilitySynthesisDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L100 [function] `Run("P1 Facility evolution", FacilityEvolutionDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L101 [function] `Run("P1 Codex", CodexDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L102 [function] `Run("P2 Regular customer", RegularCustomerDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L103 [function] `Run("P2 Staff discontent", StaffDiscontentDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L104 [function] `Run("P2 Staff rebellion response", StaffRebellionResponseDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L105 [function] `Run("P2 Run variables", RunVariableDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L106 [function] `Run("P2 Meta progression", MetaProgressionDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L107 [function] `Run("P3 Offense world map", OffenseWorldMapDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L108 [function] `Run("P3 Offense expedition", OffenseExpeditionDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L109 [function] `Run("P3 Offense rewards", OffenseRewardDebugScenarios.RunAll, results)` [EditorAssetAccess]
- L114 [function] `LogSummary(results, success)` [EditorAssetAccess]
- L120 [function] `private static void EnsureGeneratedAssets()` [EditorAssetAccess]
- L150 [function] `private static bool RunPlanCharacterAiScenarios(bool log)` [EditorAssetAccess]
- L156 [function] `private static void LogSummary(IReadOnlyList<ScenarioSuiteResult> results, bool success)` [EditorAssetAccess]
- L179 [function] `WriteTextReport(reportPath, summary)` [EditorAssetAccess]
- L180 [function] `WriteJsonReport(jsonReportPath, BuildJsonSummary(results, success, generatedAt, reportPath, jsonReportPath))` [EditorAssetAccess]
- L192 [function] `private static string GetReportPath()` [EditorAssetAccess]
- L198 [function] `private static string GetJsonReportPath()` [EditorAssetAccess]
- L204 [function] `private static void WriteTextReport(string reportPath, string summary)` [EditorAssetAccess]
- L223 [function] `private static void WriteJsonReport(string reportPath, string json)` [EditorAssetAccess]
- L279 [function] `private static string BuildCrashJson(string generatedAt, Exception ex)` [EditorAssetAccess]
- L293 [function] `private static string ToJsonBool(bool value)` [EditorAssetAccess]
- L298 [function] `private static string EscapeJson(string value)` [EditorAssetAccess]
- L310 [type] `private readonly struct ScenarioSuiteResult` [EditorAssetAccess]
- L312 [function] `public ScenarioSuiteResult(string name, bool success, string detail, long durationMs)` [EditorAssetAccess]


## Assets\Scripts\FacilityEvolution\Editor

### Assets/Scripts/FacilityEvolution/Editor/FacilityEvolutionDebugScenarios.cs

- L7 [type] `public static class FacilityEvolutionDebugScenarios` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L10 [function] `public static void RunAllFromMenu()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L12 [function] `RunAll(true)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L15 [function] `public static bool RunAll(bool log = false)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L18 [function] `RunScenario("P1 evolution assets are generated and loadable", VerifyP1EvolutionAssets, errors)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L19 [function] `RunScenario("Room profile separates crowded and fine dining", VerifyDiningProfilesSeparateCrowdedAndFineDining, errors)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L20 [function] `RunScenario("Identity pressure scores room lineage direction", VerifyIdentityPressureScoresRoomLineageDirection, errors)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L21 [function] `RunScenario("Record token consume policy preserves configured history tokens", VerifyRecordTokenConsumePolicyPreservesHistoryTokens, errors)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L22 [function] `RunScenario("Mutation resolver gates suggestions by evidence", VerifyMutationResolverGatesSuggestionsByEvidence, errors)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L23 [function] `RunScenario("Context gates evolution candidates", VerifyContextGatesEvolutionCandidates, errors)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L24 [function] `RunScenario("Validation checks expose candidate condition state", VerifyValidationChecksExposeCandidateConditionState, errors)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L25 [function] `RunScenario("LLM proposal filters ids and orders valid candidates", VerifyLlmProposalFiltersIdsAndOrdersCandidates, errors)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L26 [function] `RunScenario("Runtime events build evolution records", VerifyRuntimeEventsBuildEvolutionRecords, errors)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L27 [function] `RunScenario("Evolution replaces facility and preserves lineage records", VerifyEvolutionReplacesFacilityAndPreservesLineageRecords, errors)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L28 [function] `RunScenario("Failed evolution keeps original facility", VerifyFailedEvolutionKeepsOriginalFacility, errors)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L29 [function] `RunScenario("Injected validator blocks candidate and evolution", VerifyInjectedValidatorBlocksCandidateAndEvolution, errors)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L30 [function] `RunScenario("Evolution UI renders context and executes approved candidate", VerifyEvolutionPanelRenderingAndAction, errors)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L46 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L61 [function] `private static bool VerifyP1EvolutionAssets()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L94 [function] `private static bool VerifyDiningProfilesSeparateCrowdedAndFineDining()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L111 [function] `private static bool VerifyIdentityPressureScoresRoomLineageDirection()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L145 [function] `private static bool VerifyRecordTokenConsumePolicyPreservesHistoryTokens()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L159 [function] `new StaticRecordTokenDefinitionProvider(definition))` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L176 [function] `private static bool VerifyMutationResolverGatesSuggestionsByEvidence()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L193 [function] `new DefaultFacilityEvolutionMutationResolver().Resolve(context, recipe, proposal)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L200 [function] `private static bool VerifyContextGatesEvolutionCandidates()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L234 [function] `private static bool VerifyValidationChecksExposeCandidateConditionState()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L255 [function] `private static bool VerifyLlmProposalFiltersIdsAndOrdersCandidates()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L282 [function] `new RuleBasedFacilityEvolutionProposalProvider()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L304 [function] `private static bool VerifyRuntimeEventsBuildEvolutionRecords()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L380 [function] `private static bool VerifyEvolutionReplacesFacilityAndPreservesLineageRecords()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L389 [function] `new EmptyFacilityEvolutionRecordTokenDefinitionProvider())` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L436 [function] `private static bool VerifyFailedEvolutionKeepsOriginalFacility()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L457 [function] `private static bool VerifyInjectedValidatorBlocksCandidateAndEvolution()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L485 [function] `private static bool VerifyEvolutionPanelRenderingAndAction()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L521 [function] `private static FacilityEvolutionRecipeSO CreateCrowdRecipe(BuildingSO source, BuildingSO result)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L527 [function] `Min(FacilityEvolutionTerms.SeatDensity, 0.9f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L528 [function] `Min(FacilityEvolutionTerms.TurnoverRate, 0.65f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L533 [function] `new FacilityEvolutionValue(FacilityEvolutionTerms.Crowd, 0.7f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L534 [function] `new FacilityEvolutionValue(FacilityEvolutionTerms.Luxury, -0.2f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L540 [function] `private static FacilityEvolutionRecipeSO CreateFineRecipe(BuildingSO source, BuildingSO result)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L546 [function] `Max(FacilityEvolutionTerms.SeatDensity, 0.5f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L547 [function] `Min(FacilityEvolutionTerms.LuxuryPerSeat, 2f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L548 [function] `Min(FacilityEvolutionTerms.AverageSpend, 40f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L553 [function] `new FacilityEvolutionValue(FacilityEvolutionTerms.Luxury, 0.7f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L554 [function] `new FacilityEvolutionValue(FacilityEvolutionTerms.Service, 0.2f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L555 [function] `new FacilityEvolutionValue(FacilityEvolutionTerms.Crowd, -0.2f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L561 [function] `private static FacilityEvolutionRecipeSO CreateCrowdIdentityRecipe(BuildingSO source, BuildingSO result)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L567 [function] `new FacilityEvolutionValue(FacilityEvolutionTerms.Crowd, 0.75f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L568 [function] `new FacilityEvolutionValue(FacilityEvolutionTerms.Luxury, -0.25f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L574 [function] `private static FacilityEvolutionRecipeSO CreateFineIdentityRecipe(BuildingSO source, BuildingSO result)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L580 [function] `new FacilityEvolutionValue(FacilityEvolutionTerms.Luxury, 0.7f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L581 [function] `new FacilityEvolutionValue(FacilityEvolutionTerms.Service, 0.2f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L582 [function] `new FacilityEvolutionValue(FacilityEvolutionTerms.Crowd, -0.25f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L596 [function] `Min(FacilityEvolutionTerms.Dining, 25f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L597 [function] `Min(FacilityEvolutionTerms.Combat, 12f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L607 [function] `new FacilityEvolutionValue(FacilityEvolutionTerms.Combat, 0.75f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L608 [function] `new FacilityEvolutionValue(FacilityEvolutionTerms.Crowd, 0.1f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L609 [function] `new FacilityEvolutionValue(FacilityEvolutionTerms.Luxury, -0.2f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L634 [function] `private static FacilityEvolutionMetricRequirement Min(string key, float value)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L644 [function] `private static FacilityEvolutionMetricRequirement Max(string key, float value)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L654 [function] `private static FacilityEvolutionTokenRequirement Token(string key, int count)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L701 [type] `private sealed class StaticFacilityEvolutionRecipeProvider : IFacilityEvolutionRecipeProvider` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L705 [function] `public StaticFacilityEvolutionRecipeProvider(params FacilityEvolutionRecipeSO[] recipes)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L710 [function] `public IReadOnlyList<FacilityEvolutionRecipeSO> GetRecipes()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L716 [type] `private sealed class StaticRecordTokenDefinitionProvider :` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L721 [function] `public StaticRecordTokenDefinitionProvider(params FacilityEvolutionRecordTokenDefinitionSO[] definitions)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L726 [function] `public IReadOnlyList<FacilityEvolutionRecordTokenDefinitionSO> GetDefinitions()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L731 [function] `public FacilityEvolutionRecordTokenDefinitionSO GetDefinition(string tokenId)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L739 [type] `private sealed class RejectingEvolutionValidator : IFacilityEvolutionValidator` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L743 [function] `public RejectingEvolutionValidator(string reason)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L764 [type] `private sealed class EvolutionScenarioWorld : IDisposable` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L769 [function] `private EvolutionScenarioWorld()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L775 [function] `AddHallway(new Vector2Int(x, 0))` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L778 [function] `PlaceDoor(new Vector2Int(1, 0))` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L779 [function] `PlaceWall(new Vector2Int(12, 0))` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L796 [function] `public static EvolutionScenarioWorld CreateCrowdedDining()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L807 [function] `public static EvolutionScenarioWorld CreateFineDining()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L820 [function] `public static EvolutionScenarioWorld CreateCombatDining()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L843 [function] `new RoomProfileBuilder(records)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L847 [function] `CreateReplacer()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L866 [function] `new RoomProfileBuilder(records)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L868 [function] `new RuleBasedFacilityEvolutionProposalProvider()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L870 [function] `CreateReplacer())` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L874 [function] `public void TrackObject(UnityEngine.Object obj)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L882 [function] `public void Dispose()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L892 [function] `private void AddHallway(Vector2Int position)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L895 [function] `new TestHallwayOccupant()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L901 [function] `private GridFacilityEvolutionBuildingReplacer CreateReplacer()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L912 [function] `private void PlaceDoor(Vector2Int position)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L916 [function] `Place(data, position)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L919 [function] `private void PlaceWall(Vector2Int position)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L923 [function] `Place(data, position)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L938 [function] `new FacilityEvolutionValue(FacilityEvolutionTerms.Dining, dining)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L939 [function] `new FacilityEvolutionValue(FacilityEvolutionTerms.Luxury, luxury)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L943 [function] `new FacilityEvolutionValue(FacilityEvolutionTerms.SeatCount, seats)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L944 [function] `new FacilityEvolutionValue(FacilityEvolutionTerms.TableCount, 1)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L945 [function] `new FacilityEvolutionValue(FacilityEvolutionTerms.PrivateSeatCount, privateSeats)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L947 [function] `Place(data, position)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L950 [function] `private void PlaceDecor(Vector2Int position, float luxury, float service)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L957 [function] `new FacilityEvolutionValue(FacilityEvolutionTerms.Luxury, luxury)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L958 [function] `new FacilityEvolutionValue(FacilityEvolutionTerms.Service, service)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L961 [function] `Place(data, position)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L964 [function] `private void PlaceTrainingFixture(Vector2Int position)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L971 [function] `new FacilityEvolutionValue(FacilityEvolutionTerms.Training, 18f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L972 [function] `new FacilityEvolutionValue(FacilityEvolutionTerms.Combat, 15f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L975 [function] `Place(data, position)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L995 [function] `private BuildingSO CreateBuildingData(string name, FacilityRole roles)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1019 [function] `private Sprite CreateDebugSprite()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1023 [function] `new Rect(0, 0, 1, 1)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1024 [function] `new Vector2(0.5f, 0.5f)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1030 [function] `private BuildableObject Place(BuildingSO data, Vector2Int position)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1052 [function] `private void AddRecord(BuildableObject facility, params (string key, float value)[] metrics)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1061 [function] `private void AddToken(BuildableObject facility, string key, int count)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1063 [function] `GetRecordComponent(facility).AddToken(key, count)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1066 [function] `private void AddEvent(BuildableObject facility, string text)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1068 [function] `GetRecordComponent(facility).AddRecentEvent(text)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1071 [function] `private static FacilityEvolutionRecordComponent GetRecordComponent(BuildableObject facility)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1078 [type] `private sealed class CountingEvolutionCompletedListener :` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1084 [function] `public CountingEvolutionCompletedListener()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1089 [function] `public void OnTriggerEvent(FacilityEvolutionCompletedEvent eventType)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1097 [function] `public void Dispose()` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1103 [type] `private sealed class TestHallwayOccupant : IGridOccupant` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1111 [type] `private sealed class FakeLlmRuntime : ILocalLlmRuntime` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1115 [function] `public FakeLlmRuntime(string facilityEvolutionResponse)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1123 [function] `public bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> callback)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1129 [function] `public bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> callback)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1135 [function] `public bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> callback)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1141 [function] `public bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> callback)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1147 [function] `public bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> callback)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1159 [function] `public bool GenerateBubbleLineAsync(string prompt, string originalText, Action<LocalLlmResult> callback)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L1165 [function] `private static LocalLlmResult Failed(string message)` [ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/FacilityEvolution/Editor/P1FacilityEvolutionAssetBuilder.cs

- L8 [type] `public static class P1FacilityEvolutionAssetBuilder` [EditorAssetAccess]
- L15 [function] `public static void EnsureP1EvolutionAssetsFromMenu()` [EditorAssetAccess]
- L17 [function] `EnsureP1EvolutionAssets()` [EditorAssetAccess]
- L20 [function] `public static void EnsureP1EvolutionAssets()` [EditorAssetAccess]
- L24 [function] `EnsureFolder(RecipeFolder)` [EditorAssetAccess]
- L25 [function] `EnsureFolder(TokenDefinitionFolder)` [EditorAssetAccess]
- L26 [function] `ApplyFacilityContributions()` [EditorAssetAccess]
- L27 [function] `EnsureRecordTokenDefinitionAssets()` [EditorAssetAccess]
- L31 [function] `EnsureRecipeAsset(spec)` [EditorAssetAccess]
- L38 [function] `private static void ApplyFacilityContributions()` [EditorAssetAccess]
- L45 [function] `Value(FacilityEvolutionTerms.Dining, 30f)` [EditorAssetAccess]
- L46 [function] `Value(FacilityEvolutionTerms.Cooking, 24f)` [EditorAssetAccess]
- L47 [function] `Value(FacilityEvolutionTerms.Meat, 24f)` [EditorAssetAccess]
- L48 [function] `Value(FacilityEvolutionTerms.Service, 6f)` [EditorAssetAccess]
- L52 [function] `Value(FacilityEvolutionTerms.SeatCount, 4f)` [EditorAssetAccess]
- L53 [function] `Value(FacilityEvolutionTerms.TableCount, 1f)` [EditorAssetAccess]
- L61 [function] `Value(FacilityEvolutionTerms.Dining, 34f)` [EditorAssetAccess]
- L62 [function] `Value(FacilityEvolutionTerms.Cooking, 20f)` [EditorAssetAccess]
- L63 [function] `Value(FacilityEvolutionTerms.Meat, 24f)` [EditorAssetAccess]
- L64 [function] `Value(FacilityEvolutionTerms.Combat, 18f)` [EditorAssetAccess]
- L65 [function] `Value(FacilityEvolutionTerms.Training, 10f)` [EditorAssetAccess]
- L69 [function] `Value(FacilityEvolutionTerms.SeatCount, 4f)` [EditorAssetAccess]
- L70 [function] `Value(FacilityEvolutionTerms.TableCount, 1f)` [EditorAssetAccess]
- L71 [function] `Value(FacilityEvolutionTerms.LargeTableCount, 1f)` [EditorAssetAccess]
- L79 [function] `Value(FacilityEvolutionTerms.Dining, 32f)` [EditorAssetAccess]
- L80 [function] `Value(FacilityEvolutionTerms.Cooking, 18f)` [EditorAssetAccess]
- L81 [function] `Value(FacilityEvolutionTerms.Meat, 18f)` [EditorAssetAccess]
- L82 [function] `Value(FacilityEvolutionTerms.Luxury, 24f)` [EditorAssetAccess]
- L83 [function] `Value(FacilityEvolutionTerms.Service, 14f)` [EditorAssetAccess]
- L84 [function] `Value(FacilityEvolutionTerms.Rest, 10f)` [EditorAssetAccess]
- L88 [function] `Value(FacilityEvolutionTerms.SeatCount, 3f)` [EditorAssetAccess]
- L89 [function] `Value(FacilityEvolutionTerms.TableCount, 1f)` [EditorAssetAccess]
- L90 [function] `Value(FacilityEvolutionTerms.PrivateSeatCount, 2f)` [EditorAssetAccess]
- L98 [function] `Value(FacilityEvolutionTerms.Dining, 38f)` [EditorAssetAccess]
- L99 [function] `Value(FacilityEvolutionTerms.Cooking, 22f)` [EditorAssetAccess]
- L100 [function] `Value(FacilityEvolutionTerms.Combat, 32f)` [EditorAssetAccess]
- L101 [function] `Value(FacilityEvolutionTerms.Defense, 18f)` [EditorAssetAccess]
- L102 [function] `Value(FacilityEvolutionTerms.Training, 18f)` [EditorAssetAccess]
- L106 [function] `Value(FacilityEvolutionTerms.SeatCount, 5f)` [EditorAssetAccess]
- L107 [function] `Value(FacilityEvolutionTerms.TableCount, 1f)` [EditorAssetAccess]
- L108 [function] `Value(FacilityEvolutionTerms.LargeTableCount, 1f)` [EditorAssetAccess]
- L116 [function] `Value(FacilityEvolutionTerms.Dining, 36f)` [EditorAssetAccess]
- L117 [function] `Value(FacilityEvolutionTerms.Luxury, 38f)` [EditorAssetAccess]
- L118 [function] `Value(FacilityEvolutionTerms.Service, 20f)` [EditorAssetAccess]
- L119 [function] `Value(FacilityEvolutionTerms.Rest, 16f)` [EditorAssetAccess]
- L120 [function] `Value(FacilityEvolutionTerms.Mana, 18f)` [EditorAssetAccess]
- L124 [function] `Value(FacilityEvolutionTerms.SeatCount, 3f)` [EditorAssetAccess]
- L125 [function] `Value(FacilityEvolutionTerms.TableCount, 1f)` [EditorAssetAccess]
- L126 [function] `Value(FacilityEvolutionTerms.PrivateSeatCount, 3f)` [EditorAssetAccess]
- L134 [function] `Value(FacilityEvolutionTerms.Training, 30f)` [EditorAssetAccess]
- L135 [function] `Value(FacilityEvolutionTerms.Combat, 16f)` [EditorAssetAccess]
- L144 [function] `Value(FacilityEvolutionTerms.Training, 24f)` [EditorAssetAccess]
- L145 [function] `Value(FacilityEvolutionTerms.Combat, 24f)` [EditorAssetAccess]
- L146 [function] `Value(FacilityEvolutionTerms.Defense, 18f)` [EditorAssetAccess]
- L155 [function] `Value(FacilityEvolutionTerms.Rest, 26f)` [EditorAssetAccess]
- L156 [function] `Value(FacilityEvolutionTerms.Service, 10f)` [EditorAssetAccess]
- L157 [function] `Value(FacilityEvolutionTerms.Luxury, 8f)` [EditorAssetAccess]
- L166 [function] `Value(FacilityEvolutionTerms.Mana, 24f)` [EditorAssetAccess]
- L167 [function] `Value(FacilityEvolutionTerms.Luxury, 10f)` [EditorAssetAccess]
- L194 [function] `private static void EnsureRecordTokenDefinitionAssets()` [EditorAssetAccess]
- L221 [function] `private static void EnsureRecipeAsset(EvolutionRecipeSpec spec)` [EditorAssetAccess]
- L262 [function] `private static EvolutionRecipeSpec[] CreateRecipeSpecs()` [EditorAssetAccess]
- L280 [function] `Min(FacilityEvolutionTerms.Dining, 40f)` [EditorAssetAccess]
- L281 [function] `Min(FacilityEvolutionTerms.Training, 15f)` [EditorAssetAccess]
- L285 [function] `Min(FacilityEvolutionTerms.SeatDensity, 0.25f)` [EditorAssetAccess]
- L286 [function] `Max(FacilityEvolutionTerms.SeatDensity, 1.4f)` [EditorAssetAccess]
- L290 [function] `Token(FacilityEvolutionTerms.MercenaryHangout, 1)` [EditorAssetAccess]
- L295 [function] `Value(FacilityEvolutionTerms.Combat, 0.6f)` [EditorAssetAccess]
- L296 [function] `Value(FacilityEvolutionTerms.Crowd, 0.15f)` [EditorAssetAccess]
- L297 [function] `Value(FacilityEvolutionTerms.Service, 0.1f)` [EditorAssetAccess]
- L298 [function] `Value(FacilityEvolutionTerms.Luxury, -0.2f)` [EditorAssetAccess]
- L316 [function] `Min(FacilityEvolutionTerms.Dining, 35f)` [EditorAssetAccess]
- L317 [function] `Min(FacilityEvolutionTerms.Luxury, 12f)` [EditorAssetAccess]
- L321 [function] `Max(FacilityEvolutionTerms.SeatDensity, 0.75f)` [EditorAssetAccess]
- L322 [function] `Min(FacilityEvolutionTerms.LuxuryPerSeat, 1.2f)` [EditorAssetAccess]
- L326 [function] `Token(FacilityEvolutionTerms.NoblePatronage, 1)` [EditorAssetAccess]
- L331 [function] `Value(FacilityEvolutionTerms.Luxury, 0.55f)` [EditorAssetAccess]
- L332 [function] `Value(FacilityEvolutionTerms.Service, 0.2f)` [EditorAssetAccess]
- L333 [function] `Value(FacilityEvolutionTerms.Rest, 0.15f)` [EditorAssetAccess]
- L334 [function] `Value(FacilityEvolutionTerms.Crowd, -0.2f)` [EditorAssetAccess]
- L335 [function] `Value(FacilityEvolutionTerms.Outlaw, -0.2f)` [EditorAssetAccess]
- L353 [function] `Min(FacilityEvolutionTerms.Combat, 34f)` [EditorAssetAccess]
- L354 [function] `Min(FacilityEvolutionTerms.Defense, 12f)` [EditorAssetAccess]
- L359 [function] `Token(FacilityEvolutionTerms.GuardRallyPoint, 1)` [EditorAssetAccess]
- L360 [function] `Token(FacilityEvolutionTerms.IntruderBloodied, 1)` [EditorAssetAccess]
- L365 [function] `Value(FacilityEvolutionTerms.Combat, 0.4f)` [EditorAssetAccess]
- L366 [function] `Value(FacilityEvolutionTerms.Security, 0.45f)` [EditorAssetAccess]
- L367 [function] `Value(FacilityEvolutionTerms.Service, 0.1f)` [EditorAssetAccess]
- L385 [function] `Min(FacilityEvolutionTerms.Luxury, 32f)` [EditorAssetAccess]
- L386 [function] `Min(FacilityEvolutionTerms.Mana, 12f)` [EditorAssetAccess]
- L390 [function] `Min(FacilityEvolutionTerms.LuxuryPerSeat, 2f)` [EditorAssetAccess]
- L394 [function] `Token(FacilityEvolutionTerms.NoblePatronage, 2)` [EditorAssetAccess]
- L395 [function] `Token(FacilityEvolutionTerms.CleanServiceStreak, 1)` [EditorAssetAccess]
- L400 [function] `Value(FacilityEvolutionTerms.Luxury, 0.5f)` [EditorAssetAccess]
- L401 [function] `Value(FacilityEvolutionTerms.Service, 0.2f)` [EditorAssetAccess]
- L402 [function] `Value(FacilityEvolutionTerms.Rest, 0.15f)` [EditorAssetAccess]
- L403 [function] `Value(FacilityEvolutionTerms.Ritual, 0.1f)` [EditorAssetAccess]
- L404 [function] `Value(FacilityEvolutionTerms.Outlaw, -0.25f)` [EditorAssetAccess]
- L410 [function] `private static RecordTokenDefinitionSpec[] CreateRecordTokenDefinitionSpecs()` [EditorAssetAccess]
- L533 [function] `private static BuildingSO LoadBuilding(string assetName)` [EditorAssetAccess]
- L543 [function] `private static FacilityEvolutionValue Value(string key, float value)` [EditorAssetAccess]
- L548 [function] `private static FacilityEvolutionMetricRequirement Min(string key, float value)` [EditorAssetAccess]
- L558 [function] `private static FacilityEvolutionMetricRequirement Max(string key, float value)` [EditorAssetAccess]
- L568 [function] `private static FacilityEvolutionTokenRequirement Token(string key, int count)` [EditorAssetAccess]
- L577 [function] `private static void EnsureFolder(string folder)` [EditorAssetAccess]
- L594 [type] `private sealed class EvolutionRecipeSpec` [EditorAssetAccess]
- L668 [type] `private sealed class RecordTokenDefinitionSpec` [EditorAssetAccess]


## Assets\Scripts\FacilityEvolution

### Assets/Scripts/FacilityEvolution/FacilityEvolutionIdentity.cs

- L6 [type] `public interface IFacilityIdentityPressureProvider`
- L8 [function] `void Apply(RoomProfile profile)`
- L11 [type] `public readonly struct FacilityEvolutionIdentityScore`
- L31 [function] `public string ToMessage()`
- L45 [type] `public sealed class FacilityIdentitySnapshot`
- L47 [function] `public FacilityIdentitySnapshot(FacilityEvolutionContext context)`
- L93 [function] `private static string BuildRoomSummary(RoomProfile profile)`
- L132 [type] `public sealed class DefaultFacilityIdentityPressureProvider : IFacilityIdentityPressureProvider`
- L134 [function] `public void Apply(RoomProfile profile)`
- L140 [type] `public static class FacilityIdentityPressureUtility`
- L142 [function] `public static void ApplyDefaultPressures(RoomProfile profile)`
- L149 [function] `ApplyPressure(profile, FacilityEvolutionTerms.Crowd, CrowdSignals(profile))`
- L150 [function] `ApplyPressure(profile, FacilityEvolutionTerms.Luxury, LuxurySignals(profile))`
- L151 [function] `ApplyPressure(profile, FacilityEvolutionTerms.Combat, CombatSignals(profile))`
- L152 [function] `ApplyPressure(profile, FacilityEvolutionTerms.Outlaw, OutlawSignals(profile))`
- L153 [function] `ApplyPressure(profile, FacilityEvolutionTerms.Rest, RestSignals(profile))`
- L154 [function] `ApplyPressure(profile, FacilityEvolutionTerms.Service, ServiceSignals(profile))`
- L155 [function] `ApplyPressure(profile, FacilityEvolutionTerms.Ritual, RitualSignals(profile))`
- L156 [function] `ApplyPressure(profile, FacilityEvolutionTerms.Security, SecuritySignals(profile))`
- L157 [function] `ApplyPressure(profile, FacilityEvolutionTerms.Logistics, LogisticsSignals(profile))`
- L159 [function] `DetectConflicts(profile)`
- L213 [function] `private static void ApplyPressure(RoomProfile profile, string key, IEnumerable<IdentitySignal> signals)`
- L236 [function] `private static IEnumerable<IdentitySignal> CrowdSignals(RoomProfile profile)`
- L245 [function] `private static IEnumerable<IdentitySignal> LuxurySignals(RoomProfile profile)`
- L258 [function] `private static float CoreLuxuryStrength(RoomProfile profile)`
- L261 [function] `Normalize(profile.GetScore(FacilityEvolutionTerms.Luxury), 8f, 40f)`
- L262 [function] `Normalize(profile.GetMetric(FacilityEvolutionTerms.LuxuryPerSeat), 0.8f, 4f)`
- L264 [function] `Normalize(profile.GetMetric(FacilityEvolutionTerms.AverageSpend), 20f, 80f)`
- L265 [function] `Token01(profile, FacilityEvolutionTerms.NoblePatronage, 3))`
- L268 [function] `private static IEnumerable<IdentitySignal> CombatSignals(RoomProfile profile)`
- L280 [function] `private static IEnumerable<IdentitySignal> OutlawSignals(RoomProfile profile)`
- L290 [function] `private static IEnumerable<IdentitySignal> RestSignals(RoomProfile profile)`
- L300 [function] `private static IEnumerable<IdentitySignal> ServiceSignals(RoomProfile profile)`
- L310 [function] `private static IEnumerable<IdentitySignal> RitualSignals(RoomProfile profile)`
- L319 [function] `private static IEnumerable<IdentitySignal> SecuritySignals(RoomProfile profile)`
- L329 [function] `private static IEnumerable<IdentitySignal> LogisticsSignals(RoomProfile profile)`
- L338 [function] `private static void DetectConflicts(RoomProfile profile)`
- L340 [function] `AddConflict(profile, FacilityEvolutionTerms.Crowd, FacilityEvolutionTerms.Luxury, 0.55f)`
- L341 [function] `AddConflict(profile, FacilityEvolutionTerms.Service, FacilityEvolutionTerms.Outlaw, 0.45f)`
- L342 [function] `AddConflict(profile, FacilityEvolutionTerms.Rest, FacilityEvolutionTerms.Combat, 0.6f)`
- L345 [function] `private static void AddConflict(RoomProfile profile, string a, string b, float threshold)`
- L355 [function] `private static IdentitySignal Signal(string label, float value)`
- L360 [function] `private static float Token01(RoomProfile profile, string key, int atCount)`
- L365 [function] `private static float Normalize(float value, float min, float max)`
- L375 [function] `private static float InverseNormalize(float value, float best, float worst)`
- L390 [type] `private readonly struct IdentitySignal`
- L392 [function] `public IdentitySignal(string label, float value)`

### Assets/Scripts/FacilityEvolution/FacilityEvolutionLlmProposalProvider.cs

- L8 [type] `public sealed class FacilityEvolutionProposalReasonDto` [Reflection]
- L15 [type] `public sealed class FacilityEvolutionProposalJsonDto : ILlmJsonPayload` [Reflection]
- L26 [function] `public bool Validate(out string error)` [Reflection]
- L183 [type] `public sealed class CachedLocalLlmFacilityEvolutionProposalProvider : IFacilityEvolutionProposalProvider` [Reflection]
- L207 [function] `public FacilityEvolutionProposal Propose(FacilityEvolutionContext context)` [Reflection]
- L223 [function] `TryRequestProposal(signature, context)` [Reflection]
- L237 [function] `private void TryRequestProposal(string signature, FacilityEvolutionContext context)` [Reflection]
- L241 [function] `SetStatus(signature, "LLM disabled outside play mode.")` [Reflection]
- L248 [function] `SetStatus(signature, "LLM unavailable: LocalLlmRequestQueue is missing.")` [Reflection]
- L266 [function] `SetStatus(signature, "LLM proposal requested.")` [Reflection]
- L268 [function] `OnLlmResult(signature, result, validCandidateIds, validMutationTags))` [Reflection]
- L272 [function] `SetStatus(signature, "LLM failed: request was not accepted.")` [Reflection]
- L286 [function] `SetStatus(signature, $"LLM failed: {result.Status} {result.Error}")` [Reflection]
- L292 [function] `SetStatus(signature, $"LLM failed: {parseError}")` [Reflection]
- L301 [function] `SetStatus(signature, statusMessage)` [Reflection]
- L304 [function] `private void SetStatus(string signature, string message)` [Reflection]
- L329 [type] `public static class FacilityEvolutionPromptFormatter` [Reflection]
- L331 [function] `public static string BuildSignature(FacilityEvolutionContext context)` [Reflection]
- L341 [function] `AppendList(builder, context.State != null ? context.State.LineageTags : Array.Empty<string>())` [Reflection]
- L342 [function] `AppendList(builder, context.CandidateRecipes?.Select((recipe) => recipe != null ? recipe.EffectiveId : string.Empty))` [Reflection]
- L343 [function] `AppendPairs(builder, context.Profile != null ? context.Profile.Scores : null)` [Reflection]
- L344 [function] `AppendPairs(builder, context.Profile != null ? context.Profile.Metrics : null)` [Reflection]
- L345 [function] `AppendPairs(builder, context.Profile != null ? context.Profile.IdentityPressures : null)` [Reflection]
- L346 [function] `AppendTokenPairs(builder, context.Profile != null ? context.Profile.RecordTokens : null)` [Reflection]
- L350 [function] `public static string BuildPrompt(FacilityEvolutionContext context)` [Reflection]
- L405 [function] `private static void AppendPairs(StringBuilder builder, IReadOnlyDictionary<string, float> values)` [Reflection]
- L418 [function] `private static void AppendTokenPairs(StringBuilder builder, IReadOnlyDictionary<string, int> values)` [Reflection]
- L431 [function] `private static void AppendList(StringBuilder builder, IEnumerable<string> values)` [Reflection]
- L442 [function] `private static string JoinValues(IEnumerable<string> values)` [Reflection]
- L452 [function] `private static string JoinPairs(IReadOnlyDictionary<string, float> values)` [Reflection]
- L465 [function] `private static string JoinTokenPairs(IReadOnlyDictionary<string, int> values)` [Reflection]
- L478 [function] `private static string JoinRequirements(FacilityEvolutionMetricRequirement[] requirements)` [Reflection]
- L496 [function] `private static string JoinTokenRequirements(FacilityEvolutionTokenRequirement[] requirements)` [Reflection]
- L508 [function] `private static string JoinIdentityWeights(FacilityEvolutionValue[] weights)` [Reflection]

### Assets/Scripts/FacilityEvolution/FacilityEvolutionMutations.cs

- L6 [type] `public readonly struct FacilityEvolutionMutationResult`
- L20 [type] `public interface IFacilityEvolutionMutationResolver`
- L28 [type] `public sealed class DefaultFacilityEvolutionMutationResolver : IFacilityEvolutionMutationResolver`
- L58 [function] `TryAddMutation(tag, allowed, profile, reasons, ordered, "??嶺????????썹땟?????뚯????덈춣????濚밸Ŧ遊얕맱?)`
- L78 [function] `TryAddMutation(tag, allowed, profile, reasons, ordered, "?癲ル슢캉??????縕????)`
- L106 [function] `private static bool HasEvidence(RoomProfile profile, string tag)`
- L111 [function] `private static float GetTagEvidenceScore(RoomProfile profile, string tag)`

### Assets/Scripts/FacilityEvolution/FacilityEvolutionRecipeSO.cs

- L6 [type] `public class FacilityEvolutionRecipeSO : DataScriptableObject`
- L51 [function] `public bool MatchesSource(BuildableObject facility, FacilityEvolutionStateComponent state)`

### Assets/Scripts/FacilityEvolution/FacilityEvolutionRecord.cs

- L6 [type] `public interface IFacilityEvolutionRecordProvider`
- L8 [function] `FacilityEvolutionRecord GetRecord(BuildableObject facility)`
- L11 [type] `public sealed class FacilityEvolutionRecord`
- L21 [function] `public float GetMetric(string key)`
- L26 [function] `public int GetToken(string key)`
- L31 [function] `public void AddMetric(string key, float value)`
- L41 [function] `public void AddToken(string key, int count)`
- L52 [function] `public void SetToken(string key, int count)`
- L62 [function] `public bool TryConsumeToken(string key, int count, out string reason)`
- L82 [function] `public void AddEvent(string text)`
- L124 [function] `public FacilityEvolutionRecord Clone()`
- L146 [type] `public class FacilityEvolutionRecordComponent : MonoBehaviour, IFacilityEvolutionRecordProvider`
- L152 [function] `public FacilityEvolutionRecord GetRecord(BuildableObject facility)`
- L182 [function] `public void SetMetric(string key, float value)`
- L198 [function] `public void AddToken(string key, int count)`
- L214 [function] `public void AddRecentEvent(string text)`
- L227 [function] `public void ReplaceWith(FacilityEvolutionRecord record)`
- L251 [type] `public sealed class ComponentFacilityEvolutionRecordProvider : IFacilityEvolutionRecordProvider`
- L253 [function] `public FacilityEvolutionRecord GetRecord(BuildableObject facility)`

### Assets/Scripts/FacilityEvolution/FacilityEvolutionRecordRuntime.cs

- L7 [type] `public class FacilityEvolutionRecordRuntime :` [DependencyInjection, EventBus, RuntimeObjectCreation]
- L32 [function] `public void Construct(IFacilityCandidateCache facilityCandidateCache)` [DependencyInjection]
- L38 [function] `public void OnTriggerEvent(FacilityVisitEvent eventType)` [DependencyInjection, EventBus, RuntimeObjectCreation]
- L39 [function] `SetMetric(record, FacilityEvolutionTerms.VisitCount, nextVisits)` [EventBus, RuntimeObjectCreation]
- L50 [function] `SetMetric(record, FacilityEvolutionTerms.UniqueVisitorCount, uniqueVisitors.Count)` [EventBus, RuntimeObjectCreation]
- L54 [function] `UpdateRatio(GetMetric(record, FacilityEvolutionTerms.RepeatVisitorRatio), previousVisits, nextVisits, repeatVisit))` [EventBus, RuntimeObjectCreation]
- L60 [function] `UpdateAverage(GetMetric(record, FacilityEvolutionTerms.AverageSatisfaction), previousVisits, nextVisits, mood))` [EventBus, RuntimeObjectCreation]
- L67 [function] `UpdateRatio(GetMetric(record, FacilityEvolutionTerms.CombatVisitorRatio), previousVisits, nextVisits, combatVisitor))` [EventBus, RuntimeObjectCreation]
- L71 [function] `UpdateRatio(GetMetric(record, FacilityEvolutionTerms.NobleVisitorRatio), previousVisits, nextVisits, nobleVisitor))` [EventBus, RuntimeObjectCreation]
- L93 [function] `public void OnTriggerEvent(FacilityRevenueEvent eventType)` [EventBus, RuntimeObjectCreation]
- L102 [function] `IncrementMetric(record, FacilityEvolutionTerms.TotalRevenue, eventType.revenue)` [EventBus, RuntimeObjectCreation]
- L105 [function] `SetMetric(record, FacilityEvolutionTerms.AverageSpend, totalRevenue / visitCount)` [EventBus, RuntimeObjectCreation]
- L106 [function] `SetMetric(record, FacilityEvolutionTerms.ProfitPerVisit, totalRevenue / visitCount)` [EventBus, RuntimeObjectCreation]
- L110 [function] `IncrementMetric(record, FacilityEvolutionTerms.HighValueTransactionCount, 1f)` [EventBus, RuntimeObjectCreation]
- L114 [function] `GetDayRecord(facility)` [EventBus, RuntimeObjectCreation]
- L118 [function] `public void OnTriggerEvent(FacilityStockConsumedEvent eventType)` [EventBus, RuntimeObjectCreation]
- L132 [function] `SetMetric(record, FacilityEvolutionTerms.StockCostPerVisit, consumedStock / visitCount)` [EventBus, RuntimeObjectCreation]
- L141 [function] `public void OnTriggerEvent(FacilityCrimeEvent eventType)` [EventBus, RuntimeObjectCreation]
- L150 [function] `IncrementMetric(record, FacilityEvolutionTerms.CrimeCount, 1f)` [EventBus, RuntimeObjectCreation]
- L151 [function] `IncrementMetric(record, FacilityEvolutionTerms.NegativeMentionCount, 1f)` [EventBus, RuntimeObjectCreation]
- L154 [function] `IncrementMetric(record, FacilityEvolutionTerms.TheftCount, 1f)` [EventBus, RuntimeObjectCreation]
- L166 [function] `GetDayRecord(facility)` [EventBus, RuntimeObjectCreation]
- L170 [function] `public void OnTriggerEvent(FacilityRestockEvent eventType)` [EventBus, RuntimeObjectCreation]
- L181 [function] `IncrementMetric(record, FacilityEvolutionTerms.StockoutCount, 1f)` [EventBus, RuntimeObjectCreation]
- L185 [function] `GetDayRecord(facility)` [EventBus, RuntimeObjectCreation]
- L190 [function] `public void OnTriggerEvent(DefenseFacilityTriggeredEvent eventType)` [EventBus, RuntimeObjectCreation]
- L200 [function] `IncrementMetric(record, FacilityEvolutionTerms.IntruderDamageDealt, report.TotalDamage)` [EventBus, RuntimeObjectCreation]
- L201 [function] `IncrementMetric(record, FacilityEvolutionTerms.IntruderDelayTime, report.MovementDelaySeconds)` [EventBus, RuntimeObjectCreation]
- L216 [function] `public void OnTriggerEvent(InvasionFacilityDamagedEvent eventType)` [EventBus, RuntimeObjectCreation]
- L225 [function] `IncrementMetric(record, FacilityEvolutionTerms.FacilityDamageTaken, 1f)` [EventBus, RuntimeObjectCreation]
- L226 [function] `IncrementMetric(record, FacilityEvolutionTerms.NegativeMentionCount, 1f)` [EventBus, RuntimeObjectCreation]
- L228 [function] `GetDayRecord(facility)` [EventBus, RuntimeObjectCreation]
- L232 [function] `public void OnTriggerEvent(OperatingDayEndedEvent eventType)` [EventBus, RuntimeObjectCreation]
- L246 [function] `IncrementMetric(record, FacilityEvolutionTerms.PositiveMentionCount, 1f)` [EventBus, RuntimeObjectCreation]
- L264 [function] `private void OnEnable()` [EventBus, RuntimeObjectCreation]
- L276 [function] `private void OnDisable()` [EventBus, RuntimeObjectCreation]
- L288 [function] `private FacilityDayRecord GetDayRecord(BuildableObject facility)` [EventBus, RuntimeObjectCreation]
- L300 [function] `private void MarkDynamicStateDirty()` [DependencyInjection]
- L305 [function] `private IFacilityCandidateCache ResolveFacilityCandidateCache()` [DependencyInjection]
- L313 [function] `private HashSet<int> GetUniqueVisitors(BuildableObject facility)` [DependencyInjection, EventBus, RuntimeObjectCreation]
- L323 [function] `private static FacilityEvolutionRecordComponent GetOrAddRecord(BuildableObject facility)` [EventBus, RuntimeObjectCreation]
- L308 [function] `private static void IncrementMetric(FacilityEvolutionRecordComponent record, string key, float delta)` [EventBus, RuntimeObjectCreation]
- L315 [function] `SetMetric(record, key, GetMetric(record, key) + delta)` [EventBus, RuntimeObjectCreation]
- L318 [function] `private static void SetMetric(FacilityEvolutionRecordComponent record, string key, float value)` [EventBus, RuntimeObjectCreation]
- L323 [function] `private static float GetMetric(FacilityEvolutionRecordComponent record, string key)` [EventBus, RuntimeObjectCreation]
- L328 [function] `private static float UpdateAverage(float previousAverage, float previousCount, float nextCount, float value)` [EventBus, RuntimeObjectCreation]
- L338 [function] `private static float UpdateRatio(float previousRatio, float previousTotal, float nextTotal, bool increment)` [EventBus, RuntimeObjectCreation]
- L354 [function] `private static int GetFacilityKey(BuildableObject facility)` [EventBus, RuntimeObjectCreation]
- L359 [function] `private static int GetVisitorId(CharacterActor actor)` [EventBus, RuntimeObjectCreation]
- L366 [function] `private static bool SupportsRole(BuildableObject facility, FacilityRole role)` [EventBus, RuntimeObjectCreation]
- L371 [function] `private static bool IsCombatVisitor(CharacterActor actor)` [EventBus, RuntimeObjectCreation]
- L385 [function] `private static bool IsNobleVisitor(CharacterActor actor)` [EventBus, RuntimeObjectCreation]
- L398 [function] `private static float GetCondition(CharacterActor actor, CharacterCondition condition, float defaultValue)` [EventBus, RuntimeObjectCreation]
- L408 [function] `private static string GetActorName(CharacterActor actor)` [EventBus, RuntimeObjectCreation]
- L413 [function] `private static string GetFacilityName(BuildableObject facility)` [EventBus, RuntimeObjectCreation]
- L418 [type] `private sealed class FacilityDayRecord` [EventBus, RuntimeObjectCreation]
- L420 [function] `public FacilityDayRecord(BuildableObject facility)` [EventBus, RuntimeObjectCreation]

### Assets/Scripts/FacilityEvolution/FacilityEvolutionRecordTokens.cs

- L6 [type] `public enum FacilityEvolutionRecordTokenConsumePolicy`
- L14 [type] `public class FacilityEvolutionRecordTokenDefinitionSO : DataScriptableObject`
- L35 [type] `public interface IFacilityEvolutionRecordTokenDefinitionProvider`
- L37 [function] `IReadOnlyList<FacilityEvolutionRecordTokenDefinitionSO> GetDefinitions()`
- L38 [function] `FacilityEvolutionRecordTokenDefinitionSO GetDefinition(string tokenId)`
- L41 [type] `public sealed class EmptyFacilityEvolutionRecordTokenDefinitionProvider :`
- L44 [function] `public IReadOnlyList<FacilityEvolutionRecordTokenDefinitionSO> GetDefinitions()`
- L49 [function] `public FacilityEvolutionRecordTokenDefinitionSO GetDefinition(string tokenId)`
- L55 [type] `public interface IFacilityEvolutionRecordTokenConsumer`
- L64 [type] `public sealed class DefaultFacilityEvolutionRecordTokenConsumer : IFacilityEvolutionRecordTokenConsumer` [DependencyInjection]
- L68 [function] `public DefaultFacilityEvolutionRecordTokenConsumer(` [DependencyInjection]
- L75 [function] `public bool TryConsume(` [DependencyInjection]

### Assets/Scripts/FacilityEvolution/FacilityEvolutionRuntime.cs

- L7 [type] `public interface IFacilityEvolutionRecipeProvider` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L12 [type] `public interface IFacilityEvolutionBuildingReplacer` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L18 [type] `public sealed class GridFacilityEvolutionBuildingReplacer : IFacilityEvolutionBuildingReplacer` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L22 [function] `public GridFacilityEvolutionBuildingReplacer(GridBuildingFactory buildingFactory)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L28 [function] `public bool CanReplace(BuildableObject source, BuildingSO resultBuilding, out string reason)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L69 [function] `public bool TryReplace(BuildableObject source, BuildingSO resultBuilding, out BuildableObject result, out string reason)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L116 [type] `public readonly struct FacilityEvolutionResult` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L118 [function] `public FacilityEvolutionResult(...)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L148 [type] `public struct FacilityEvolutionCompletedEvent` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L152 [function] `public FacilityEvolutionCompletedEvent(FacilityEvolutionResult result)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L159 [function] `public static void Trigger(FacilityEvolutionResult result)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L166 [type] `public interface IFacilityEvolutionValidator` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L176 [type] `public sealed class DefaultFacilityEvolutionValidator : IFacilityEvolutionValidator` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L181 [function] `public DefaultFacilityEvolutionValidator(IFacilityEvolutionRecipeQuery recipeQuery, IFacilityEvolutionStateService stateService)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L186 [function] `public FacilityEvolutionValidationResult Validate(...)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L219 [type] `public interface IFacilityEvolutionCandidateBuilder` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L231 [type] `public sealed class DefaultFacilityEvolutionCandidateBuilder : IFacilityEvolutionCandidateBuilder` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L235 [function] `public DefaultFacilityEvolutionCandidateBuilder(IFacilityEvolutionValidator validator)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L241 [function] `public FacilityEvolutionCandidate Build(...)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L293 [type] `public sealed class FacilityEvolutionEngine` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L310 [function] `public FacilityEvolutionEngine(IFacilityEvolutionRecipeQuery recipeQuery, ...)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L332 [function] `public IReadOnlyList<FacilityEvolutionRecipeSO> VisibleRecipes => recipeQuery.GetVisibleRecipes(ResearchState)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L335 [function] `public FacilityEvolutionContext BuildContext(BuildableObject facility)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L344 [function] `public IReadOnlyList<FacilityEvolutionCandidate> GetCandidates(...)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L372 [function] `public bool TryEvolve(...)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L458 [function] `private static IReadOnlyDictionary<string, int> BuildProposalOrder(FacilityEvolutionProposal proposal)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L478 [function] `private bool ConsumeMaterials(FacilityEvolutionMaterialRequirement[] requirements, out string reason)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L499 [function] `private static void CopyRecordToResult(BuildableObject resultBuilding, FacilityEvolutionRecord record)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L520 [function] `private static FacilityEvolutionResult Fail(...)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L537 [type] `public class FacilityEvolutionRuntime : MonoBehaviour` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L581 [function] `public void ConstructFacilityEvolutionRuntime(..., IFacilityEvolutionRecordTokenConsumer recordTokenConsumer, IObjectResolver objectResolver, IGridBuildingObjectFactory gridBuildingObjectFactory)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L589 [function] `public IReadOnlyList<FacilityEvolutionRecipeSO> VisibleRecipes => Engine.VisibleRecipes` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L622 [function] `public void Configure(IFacilityEvolutionRecipeQuery nextRecipeQuery = null, ...)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L618 [function] `public FacilityEvolutionContext BuildContext(BuildableObject facility)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L623 [function] `public IReadOnlyList<FacilityEvolutionCandidate> GetCandidates(...)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L630 [function] `public bool TryEvolve(...)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L689 [function] `private FacilityEvolutionEngine CreateEngine()` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L712 [function] `private IFacilityEvolutionProposalProvider CreateDefaultProposalProvider()` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L720 [function] `private ILocalLlmRuntime ResolveLocalLlmRuntime()` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L732 [function] `private IBlueprintResearchStateService ResolveResearchStateService()` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L738 [function] `private IGridTextureProvider ResolveGridTextureProvider()` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L744 [function] `private IRoomLayoutCache ResolveRoomLayoutCache()` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L750 [function] `private IFacilityEvolutionStateService ResolveStateService()` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L756 [function] `private IFacilityCandidateCache ResolveFacilityCandidateCache()` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L762 [function] `private void InjectCreatedBuilding(BuildableObject building)` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]
- L778 [function] `private IObjectResolver ResolveObjectResolver()` [DependencyInjection, Reflection, EventBus, RuntimeObjectCreation]

### Assets/Scripts/FacilityEvolution/FacilityEvolutionService.cs

- L6 [type] `public interface IFacilityEvolutionResourceProvider`
- L12 [type] `public interface IFacilityEvolutionProposalProvider`
- L17 [type] `public readonly struct FacilityEvolutionProposal`
- L19 [function] `public FacilityEvolutionProposal(...)`
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
- L293 [function] `public FacilityEvolutionContext(...)`
- L311 [type] `public sealed class FacilityEvolutionValidationResult`
- L320 [function] `public void Reject(string reason)`
- L329 [function] `public void AddCheck(string category, string label, bool passed, string detail = "")`
- L345 [type] `public readonly struct FacilityEvolutionValidationCheck`
- L347 [function] `public FacilityEvolutionValidationCheck(...)`
- L365 [type] `public sealed class FacilityEvolutionCandidate`
- L367 [function] `public FacilityEvolutionCandidate(...)`
- L421 [type] `public static class FacilityEvolutionUtility`
- L423 [function] `public static string GetFacilityId(BuildingSO building)`
- L433 [function] `public static IEnumerable<string> GetDefaultLineageTags(BuildingSO building)`
- L461 [type] `public static class FacilityEvolutionService`
- L463 [function] `public static bool IsRecipeVisible(FacilityEvolutionRecipeSO recipe, BlueprintResearchState researchState, IMetaProgressionRuntimeReader metaProgressionReader)`
- L490 [function] `public static IReadOnlyList<FacilityEvolutionRecipeSO> GetSourceCandidates(..., IFacilityEvolutionRecipeQuery recipeQuery, IFacilityEvolutionStateService stateService)` [DependencyInjection]
- L518 [function] `public static FacilityEvolutionValidationResult Validate(..., IFacilityEvolutionRecipeQuery recipeQuery, IFacilityEvolutionStateService stateService)` [DependencyInjection]
- L582 [function] `private static void ValidateTags(...)`
- L593 [function] `private static void ValidateMetricRequirements(...)`
- L617 [function] `private static void ValidateTokenRequirements(...)`
- L640 [function] `private static void ValidateIdentityPressure(...)`
- L662 [function] `private static void ValidateUniqueFixtures(...)`
- L691 [function] `private static void ValidateMaterials(...)`

### Assets/Scripts/FacilityEvolution/FacilityEvolutionState.cs

- L7 [type] `public class FacilityEvolutionHistoryEntry` [RuntimeObjectCreation]
- L15 [function] `public FacilityEvolutionHistoryEntry Clone()` [RuntimeObjectCreation]
- L28 [type] `public sealed class FacilityEvolutionStateSnapshot` [RuntimeObjectCreation]
- L41 [type] `public class FacilityEvolutionStateComponent : MonoBehaviour` [RuntimeObjectCreation]
- L64 [function] `public FacilityEvolutionStateSnapshot CreateSnapshot()` [RuntimeObjectCreation]
- L83 [function] `public void ApplySnapshot(FacilityEvolutionStateSnapshot snapshot)` [RuntimeObjectCreation]
- L120 [function] `public void InitializeIfNeeded(BuildableObject facility)` [RuntimeObjectCreation]
- L159 [function] `InitializeIfNeeded(toFacility)` [RuntimeObjectCreation]
- L187 [function] `CaptureIdentity(profile)` [RuntimeObjectCreation]
- L201 [function] `private void CaptureIdentity(RoomProfile profile)` [RuntimeObjectCreation]
- L224 [type] `public interface IFacilityEvolutionStateService` [DependencyInjection]
- L226 [function] `FacilityEvolutionStateComponent GetOrAdd(BuildableObject facility)` [DependencyInjection]
- L229 [type] `public sealed class FacilityEvolutionStateService : IFacilityEvolutionStateService` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L231 [function] `public FacilityEvolutionStateComponent GetOrAdd(BuildableObject facility)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/FacilityEvolution/FacilityEvolutionTerms.cs

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

### Assets/Scripts/FacilityEvolution/RoomProfile.cs

- L6 [type] `public interface IRoomProfileProvider`
- L8 [function] `RoomProfile Build(BuildableObject facility)`
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
- L165 [type] `public sealed class RoomProfileBuilder : IRoomProfileProvider` [DependencyInjection]
- L170 [function] `public RoomProfileBuilder(IFacilityEvolutionRecordProvider recordProvider, IRoomLayoutCache roomLayoutCache)` [DependencyInjection]
- L180 [function] `public RoomProfile Build(BuildableObject facility)` [DependencyInjection]
- L183 [function] `AddFixtureContribution(profile, fixture)`
- L186 [function] `AddRecordContribution(profile, recordProvider.GetRecord(facility))`
- L187 [function] `CalculateDerivedMetrics(profile)`
- L198 [function] `private static IEnumerable<BuildableObject> CollectFixtures(BuildableObject facility, RoomInstance room)`
- L220 [function] `private static void AddFixtureContribution(RoomProfile profile, BuildableObject fixture)`
- L257 [function] `AddRoleContribution(profile, data.Facility)`
- L258 [function] `AddDefenseContribution(profile, data.Defense)`
- L261 [function] `private static void AddRoleContribution(RoomProfile profile, FacilityData facility)`
- L319 [function] `private static void AddDefenseContribution(RoomProfile profile, DefenseFacilityData defense)`
- L332 [function] `private static void AddRecordContribution(RoomProfile profile, FacilityEvolutionRecord record)`
- L355 [function] `private static void CalculateDerivedMetrics(RoomProfile profile)`


## Assets\Scripts\FacilityEvolution\UI

### Assets/Scripts/FacilityEvolution/UI/FacilityEvolutionPanel.cs

- L7 [type] `public class FacilityEvolutionPanel :` [DependencyInjection, Reflection, EventBus, SceneMutation]
- L24 [function] `public void Construct(IFacilityEvolutionRuntimeProvider runtimeProvider)` [DependencyInjection]
- L30 [function] `public void Bind(FacilityEvolutionRuntime nextRuntime)` [DependencyInjection]
- L36 [function] `internal void BindGeneratedView(TMP_Text summaryText)` [SceneMutation]
- L43 [function] `public void SelectFacility(BuildableObject facility)` [SceneMutation]
- L49 [function] `public bool TryEvolve(string evolutionId, out FacilityEvolutionResult result)` [DependencyInjection, SceneMutation]
- L76 [function] `public bool TryEvolveFirstApproved(out FacilityEvolutionResult result)` [DependencyInjection]
- L91 [function] `public void Refresh()` [DependencyInjection, SceneMutation]
- L181 [function] `public void OnTriggerEvent(InfoFeedEvent eventType)` [EventBus, SceneMutation]
- L191 [function] `public void OnTriggerEvent(BlueprintResearchCompletedEvent eventType)` [EventBus, SceneMutation]
- L196 [function] `public void OnTriggerEvent(FacilityEvolutionCompletedEvent eventType)` [EventBus, SceneMutation]
- L206 [function] `private void ApplyText()` [SceneMutation]
- L214 [function] `private static string FormatList(IEnumerable<string> values)`
- L223 [function] `private static IEnumerable<string> FormatChecks(FacilityEvolutionValidationResult validation)`
- L243 [function] `private static string FormatPressures(IReadOnlyDictionary<string, float> values)`
- L259 [function] `private FacilityEvolutionRuntime ResolveRuntime()` [DependencyInjection]
- L268 [function] `private void OnEnable()` [EventBus]
- L275 [function] `private void OnDisable()` [EventBus]


## Assets\Scripts\FacilityShop\Editor

### Assets/Scripts/FacilityShop/Editor/FacilityShopDebugScenarios.cs

- L8 [type] `public static class FacilityShopDebugScenarios` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L11 [function] `public static void RunFromMenu()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L20 [function] `public static bool RunAll(bool logSuccess)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L25 [function] `RunScenario("??濚밸Ŧ???????브컯? ??嶺?筌?????고뀘???????, VerifyDailyOffersContainBuildingAndBlueprint, errors)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L26 [function] `RunScenario("??? ????브컯? ??嶺뚮㉡?ｏ쭗??嚥싲갭큔???, VerifyRareOffersAppearRandomly, errors)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L27 [function] `RunScenario("???뚯????????彛?????繹먮굝鍮?, VerifyBasicPurchaseUnlocksLowStarsOnly, errors)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L28 [function] `RunScenario("??嶺?筌?????彛??, VerifyBuildingPurchaseUsesMoneyAndUnlocksBuilding, errors)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L29 [function] `RunScenario("????고뀘???????彛??, VerifyBlueprintPurchaseUsesMoneyAndRecordsBlueprint, errors)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L30 [function] `RunScenario("????ㅳ늾?온????????쇨덫????醫딆┣???, VerifyRuntimeRefreshesAfterOperatingDay, errors)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L31 [function] `RunScenario("?癲ル슢캉?????⑤슢???????嶺?筌?????쇨덫??????, VerifySettlementReportIncludesFacilityShop, errors)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L51 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L65 [function] `private static bool VerifyDailyOffersContainBuildingAndBlueprint()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L76 [function] `private static bool VerifyRareOffersAppearRandomly()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L91 [function] `private static bool VerifyBasicPurchaseUnlocksLowStarsOnly()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L120 [function] `private static bool VerifyBuildingPurchaseUsesMoneyAndUnlocksBuilding()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L148 [function] `private static bool VerifyBlueprintPurchaseUsesMoneyAndRecordsBlueprint()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L172 [function] `private static bool VerifyRuntimeRefreshesAfterOperatingDay()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L189 [function] `private static bool VerifySettlementReportIncludesFacilityShop()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L206 [function] `private static BuildingSO LoadBuilding(string assetName)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L211 [function] `private static FacilityBlueprintSO LoadBlueprint(string assetName)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L216 [function] `private static BuildingSO CreateSyntheticDefenseBuilding(int id, string objectName, int star)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L237 [function] `private static GameData CreateGameData(int holdingMoney)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L245 [type] `private sealed class CountingFacilityShopRefreshedListener : UtilEventListener<FacilityShopRefreshedEvent>, IDisposable` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L250 [function] `public CountingFacilityShopRefreshedListener()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L255 [function] `public void OnTriggerEvent(FacilityShopRefreshedEvent eventType)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L261 [function] `public void Dispose()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L267 [type] `private sealed class CountingEventAlertRequestListener : UtilEventListener<EventAlertRequestedEvent>, IDisposable` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L274 [function] `public CountingEventAlertRequestListener()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L279 [function] `public void OnTriggerEvent(EventAlertRequestedEvent eventType)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L285 [function] `public void Dispose()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/FacilityShop/Editor/P1FacilityShopAssetBuilder.cs

- L7 [type] `public static class P1FacilityShopAssetBuilder` [EditorAssetAccess]
- L82 [function] `public static void EnsureP1FacilityShopAssets()` [EditorAssetAccess]
- L84 [function] `EnsureFolder(BlueprintFolder)` [EditorAssetAccess]
- L105 [function] `private static FacilityBlueprintSO LoadOrCreateBlueprint(BlueprintSpec spec)` [EditorAssetAccess]
- L119 [function] `private static int[] ResolveBuildingIds(IEnumerable<string> assetNames)` [EditorAssetAccess]
- L130 [function] `private static BuildingSO LoadBuilding(string assetName)` [EditorAssetAccess]
- L140 [function] `private static void EnsureFolder(string folder)` [EditorAssetAccess]
- L156 [type] `private readonly struct BlueprintSpec` [EditorAssetAccess]


## Assets\Scripts\FacilityShop

### Assets/Scripts/FacilityShop/FacilityBlueprintSO.cs

- L5 [type] `public class FacilityBlueprintSO : DataScriptableObject`

### Assets/Scripts/FacilityShop/FacilityShopSystem.cs

- L7 [type] `public enum FacilityShopOfferType` [DependencyInjection, EventBus]
- L13 [type] `public enum FacilityShopRarity` [DependencyInjection, EventBus]
- L21 [type] `public class FacilityShopOfferSnapshot` [DependencyInjection, EventBus]
- L30 [function] `public string ToSummaryText()` [DependencyInjection, EventBus]
- L40 [type] `public class FacilityShopOffer` [DependencyInjection, EventBus]
- L42 [function] `public FacilityShopOffer(...)` [DependencyInjection, EventBus]
- L57 [function] `public FacilityShopOffer(...)` [DependencyInjection, EventBus]
- L83 [function] `public FacilityShopOfferSnapshot ToSnapshot()` [DependencyInjection, EventBus]
- L97 [type] `public readonly struct FacilityShopPurchaseResult` [DependencyInjection, EventBus]
- L108 [function] `public FacilityShopPurchaseResult(bool success, FacilityShopOffer offer, int cost, string message)` [DependencyInjection, EventBus]
- L125 [type] `public struct FacilityShopRefreshedEvent` [DependencyInjection, EventBus]
- L143 [function] `public static void Trigger(...)` [DependencyInjection, EventBus]
- L155 [type] `public struct FacilityShopPurchasedEvent` [DependencyInjection, EventBus]
- L166 [function] `public static void Trigger(FacilityShopPurchaseResult result)` [DependencyInjection, EventBus]
- L173 [type] `public class FacilityShopUnlockState` [DependencyInjection, EventBus]
- L181 [function] `public bool UnlockBasicPurchase(BuildingSO building)` [DependencyInjection, EventBus]
- L191 [function] `public bool UnlockBasicPurchaseById(int buildingId)` [DependencyInjection, EventBus]
- L201 [function] `public bool IsBasicPurchaseUnlocked(BuildingSO building)` [DependencyInjection, EventBus]
- L206 [function] `public bool MarkBlueprintAcquired(FacilityBlueprintSO blueprint)` [DependencyInjection, EventBus]
- L216 [function] `public bool IsBlueprintAcquired(FacilityBlueprintSO blueprint)` [DependencyInjection, EventBus]
- L222 [type] `public static class FacilityShopService` [DependencyInjection, EventBus]
- L228 [function] `public static IReadOnlyList<FacilityShopOffer> CreateDailyOffers(int day, IFacilityShopCatalog catalog, IRunVariableRuntimeReader runVariableReader)` [DependencyInjection, EventBus]
- L252 [function] `public static IReadOnlyList<FacilityShopOffer> CreateDailyOffers(int day, IEnumerable<BuildingSO> buildings, IEnumerable<FacilityBlueprintSO> blueprints, int runShopSeed, Func<BuildingSO, float> buildingCostMultiplier, Func<FacilityBlueprintSO, float> blueprintCostMultiplier)` [EventBus]
- L310 [function] `public static IReadOnlyList<FacilityShopOffer> CreateBasicPurchaseOffers(IFacilityShopCatalog catalog, FacilityShopUnlockState unlockState, IMetaProgressionRuntimeReader metaProgressionReader, IRunVariableRuntimeReader runVariableReader)` [DependencyInjection, EventBus]
- L338 [function] `public static IReadOnlyList<FacilityShopOffer> CreateBasicPurchaseOffers(IEnumerable<BuildingSO> buildings, FacilityShopUnlockState unlockState, IEnumerable<int> expandedBasicPurchaseBuildingIds, Func<BuildingSO, float> buildingCostMultiplier)` [EventBus]
- L371 [function] `public static bool TryPurchaseOffer(GameData gameData, FacilityShopOffer offer, FacilityShopUnlockState unlockState, out FacilityShopPurchaseResult result)` [EventBus]
- L405 [function] `public static bool CanEnterBasicPurchase(BuildingSO building)` [EventBus]
- L410 [function] `public static int GetBuildingStar(BuildingSO building)` [EventBus]
- L431 [function] `public static string GetBuildingName(BuildingSO building)` [EventBus]
- L441 [function] `public static BuildingSO FindBuildingById(IFacilityShopCatalog catalog, int buildingId)` [DependencyInjection, EventBus]
- L456 [function] `private static FacilityShopOffer CreateBuildingOffer(...)` [EventBus]
- L472 [function] `private static FacilityShopOffer CreateBlueprintOffer(...)` [EventBus]
- L489 [function] `private static bool IsDailyShopBuildingCandidate(BuildingSO building)` [EventBus]
- L497 [function] `private static FacilityShopRarity ResolveBuildingRarity(BuildingSO building)` [EventBus]
- L508 [function] `private static int CalculateBuildingCost(...)` [EventBus]
- L536 [function] `private static float GetBuildingCostMultiplier(BuildingSO building, Func<BuildingSO, float> buildingCostMultiplier)` [EventBus]
- L541 [function] `private static float GetBlueprintCostMultiplier(FacilityBlueprintSO blueprint, Func<FacilityBlueprintSO, float> blueprintCostMultiplier)` [EventBus]
- L548 [function] `private static string ApplyPurchase(FacilityShopOffer offer, FacilityShopUnlockState unlockState)` [EventBus]
- L568 [type] `public class DailyFacilityShopRuntime : MonoBehaviour, UtilEventListener<OperatingDayEndedEvent>` [DependencyInjection, EventBus]
- L590 [function] `public void ConstructDailyFacilityShopRuntime(IFacilityShopCatalog facilityShopCatalog, IRunVariableRuntimeReader runVariableReader, IMetaProgressionRuntimeReader metaProgressionReader)` [DependencyInjection, EventBus]
- L603 [function] `private void Start()` [DependencyInjection, EventBus]
- L611 [function] `public void OnTriggerEvent(OperatingDayEndedEvent eventType)` [DependencyInjection, EventBus]
- L616 [function] `public void Refresh(int day, bool raiseAlert)` [DependencyInjection, EventBus]
- L638 [function] `public bool TryPurchaseDailyOffer(int index, GameData gameData, out FacilityShopPurchaseResult result)` [EventBus]
- L650 [function] `public bool TryPurchaseBasicOffer(int index, GameData gameData, out FacilityShopPurchaseResult result)` [EventBus]
- L663 [function] `private void OnEnable()` [EventBus]
- L668 [function] `private void OnDisable()` [EventBus]
- L673 [function] `private static string FormatOfferList(...)` [EventBus]
- L710 [function] `private IFacilityShopCatalog ResolveFacilityShopCatalog()` [DependencyInjection, EventBus]
- L716 [function] `private IRunVariableRuntimeReader ResolveRunVariableReader()` [DependencyInjection, EventBus]
- L722 [function] `private IMetaProgressionRuntimeReader ResolveMetaProgressionReader()` [DependencyInjection, EventBus]

## Assets\Scripts

### Assets/Scripts/GameData.cs

- L5 [type] `public class GameData : ScriptableObject`

### Assets/Scripts/GameManager.cs

- L7 [type] `public enum TimeOfDay`
- L15 [type] `public enum NumberCondition`
- L20 [type] `public class GameManager : SerializedMonoBehaviour` [EventBus]
- L26 [function] `private void Awake()`
- L30 [function] `void Start()` [EventBus]
- L38 [function] `public void ConvertSecondsToGameTime()`
- L45 [function] `public void ChangeGameSpeed()`
- L53 [function] `public void TogglePause()`
- L66 [function] `void Update()`
- L90 [function] `public IEnumerator Timer()` [EventBus]


## Assets\Scripts\Grid\Building

### Assets/Scripts/Grid/Building/GridBuildingObjectFactory.cs

- L3 [type] `public interface IGridBuildingObjectFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L5 [function] `BuildableObject Create(Grid grid, BuildingSO buildingData, Vector2Int selectPos)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L8 [type] `public sealed class GridBuildingObjectFactory : IGridBuildingObjectFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L12 [function] `public BuildableObject Create(Grid grid, BuildingSO buildingData, Vector2Int selectPos)` [RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Grid/Building/GridBuildingRuntime.cs

- L7 [type] `public class InitialBuildInfo` [Reflection, RuntimeObjectCreation, SceneMutation]
- L13 [type] `public class GridBuildingPlacementService` [Reflection, RuntimeObjectCreation, SceneMutation]
- L21 [function] `public GridBuildingPlacementService(Grid grid, BuildingSO hallwayBuilding)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L26 [function] `public GridBuildingPlacementService(Grid grid, BuildingSO hallwayBuilding, Func<int, BuildingSO> findBuildingData)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L36 [function] `public GridBuildingPlacementService(Grid grid, BuildingSO hallwayBuilding, Func<int, BuildingSO> findBuildingData, IGridBuildingFactory buildingFactory, BuildingPlacementValidator placementValidator)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L51 [function] `public void SetGrid(Grid grid)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L56 [function] `public bool TryPlaceBuilding(BuildingSO buildingData, Vector2Int position, out string errorMessage)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L73 [function] `public bool CanPlaceBuilding(BuildingSO buildingData, Vector2Int position)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L78 [function] `public bool CanPlaceBuilding(BuildingSO buildingData, Vector2Int position, out string errorMessage)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L83 [function] `public bool TryDestroyBuilding(BuildableObject building, out BuildingSO buildingData, out string errorMessage)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L117 [function] `public void PlaceInitialBuildings(IEnumerable<InitialBuildInfo> initialPlacement)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L137 [function] `private void EnsureHallwayForPassageOverlay(BuildingSO buildingData, Vector2Int position)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L153 [function] `private static HashSet<Vector2Int> CollectReservedRoomCells(IReadOnlyList<InitialBuildInfo> placements)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L177 [function] `private static bool IsRedundantInitialHallway(InitialBuildInfo item, HashSet<Vector2Int> reservedRoomCells)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L185 [function] `private static bool ReservesRoomFootprint(BuildingSO buildingData)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L192 [function] `private static bool CanShareHallwayFootprint(BuildingSO buildingData)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L205 [function] `private bool PlaceBuildingWithoutValidation(BuildingSO buildingData, Vector2Int position, out string errorMessage)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L236 [type] `public interface IGridBuildingVisual` [Reflection, RuntimeObjectCreation, SceneMutation]
- L238 [function] `void DrawBuilding(BuildingSO buildingData, Vector2Int position)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L239 [function] `void DeleteBuilding(BuildingSO buildingData, Vector2Int position)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L242 [type] `public interface IGridBuildingFactory` [Reflection, RuntimeObjectCreation, SceneMutation]
- L244 [function] `BuildableObject Create(Grid grid, BuildingSO buildingData, Vector2Int selectPos)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L245 [function] `void DeleteVisual(BuildingSO buildingData, Vector2Int selectPos)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L248 [type] `public class GridBuildingFactory : IGridBuildingFactory` [Reflection, RuntimeObjectCreation, SceneMutation]
- L254 [function] `public GridBuildingFactory(IGridBuildingObjectFactory objectFactory)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L259 [function] `public GridBuildingFactory(Action<BuildableObject> onBuildingCreated = null)` [RuntimeObjectCreation, SceneMutation]
- L264 [function] `public GridBuildingFactory(IGridBuildingVisual buildingVisual, Action<BuildableObject> onBuildingCreated = null)` [RuntimeObjectCreation, SceneMutation]
- L269 [function] `public GridBuildingFactory(IGridBuildingVisual buildingVisual, Action<BuildableObject> onBuildingCreated, IGridBuildingObjectFactory objectFactory)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L279 [function] `public BuildableObject Create(Grid grid, BuildingSO buildingData, Vector2Int selectPos)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L290 [function] `public void DeleteVisual(BuildingSO buildingData, Vector2Int selectPos)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L297 [function] `private static void ValidateBuildingVisual(BuildingSO buildingData)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L307 [function] `private static bool HasTileVisual(BuildingSO buildingData)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L314 [type] `public class BuildingPlacementValidator` [Reflection, RuntimeObjectCreation, SceneMutation]
- L319 [function] `public BuildingPlacementValidator()` [Reflection, RuntimeObjectCreation, SceneMutation]
- L324 [function] `public BuildingPlacementValidator(GridPlacementValidator gridPlacementValidator)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L329 [function] `public BuildingPlacementValidator(GridPlacementValidator gridPlacementValidator, Func<BuildingConditionContext> conditionContextFactory)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L337 [function] `public bool CanBuild(Grid grid, BuildingSO buildingData, Vector2Int buildPos, out string errorMessage)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L383 [function] `public bool CanDestroy(Grid grid, BuildingSO buildingData, BuildableObject building, out string errorMessage)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L408 [function] `public void ApplyBuildSuccess(BuildingSO buildingData)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L419 [function] `private BuildingConditionContext CreateConditionContext()` [Reflection, RuntimeObjectCreation, SceneMutation]
- L427 [type] `public static class GridBuildingExtensions` [Reflection, RuntimeObjectCreation, SceneMutation]
- L429 [function] `public static bool CanBuild(this GridCell cell, GridLayer layer = GridLayer.Building)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L434 [function] `public static bool HasBuildingInLayer(this GridCell cell, GridLayer layer = GridLayer.Building)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L439 [function] `public static bool HasBuilding(this GridCell cell)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L444 [function] `public static BuildableObject GetBuildingInlayer(this GridCell cell, GridLayer layer = GridLayer.Building)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L449 [function] `public static BuildableObject GetBuilding(this GridCell cell)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L454 [function] `public static List<BuildableObject> GetAllBuilding(this GridCell cell)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L463 [function] `public static List<BuildableObject> GetAllVisitableBuilding(this GridPathSearchResult searchResult)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L472 [function] `public static List<BuildableObject> GetAllReachableBuilding(this GridPathSearchResult searchResult)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L481 [function] `public static List<BuildableObject> GetAllVisitableBuilding(this Grid grid, Vector2Int start)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L490 [function] `public static List<BuildableObject> GetAllReachableBuilding(this Grid grid, Vector2Int start)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L499 [function] `public static bool IsConneted(this Grid grid, Vector2Int start, int id)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L504 [function] `public static bool IsConnected(this Grid grid, Vector2Int start, int id)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L515 [function] `public static List<BuildableObject> FindAllBuilding(this Grid grid, int id)` [Reflection, RuntimeObjectCreation, SceneMutation]
- L524 [function] `public static int CountBuilding(this Grid grid, BuildingSO buildingSO)` [Reflection, RuntimeObjectCreation, SceneMutation]

## Assets\Scripts\Grid\Core

### Assets/Scripts/Grid/Core/Grid.cs

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
- L321 [function] `EnsureVisitableOccupantPositionCache()`
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
- L526 [function] `RegisterTraversalLinks(occupant, targetPositions)`
- L533 [function] `public bool RemoveOccupant(GridLayer layer, IReadOnlyList<Vector2Int> positions, bool disconnectPositions)`
- L566 [function] `public GridPathSearchResult SearchPath(Vector2Int start)`
- L571 [function] `private GridPathSearchResult SearchPath(Vector2Int start, Func<Vector2Int, bool> stopCondition)`
- L621 [function] `AddMoveStep(nextSteps, pos, link.To, link.Through, link.MoveType)`
- L626 [function] `AddMoveStep(nextSteps, pos, nextPos + pos, null, GridMoveType.Walk)`
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

### Assets/Scripts/Grid/Core/GridCell.cs

- L4 [type] `public class GridCell`
- L13 [function] `public GridCell(Vector2Int pos)`
- L20 [function] `public IGridOccupant GetOccupant(GridLayer layer = GridLayer.Building)`
- L25 [function] `public IGridOccupant GetTopOccupant()`
- L44 [function] `public void ConnectFloor(IEnumerable<Vector2Int> poses)`
- L61 [function] `public void SetTraversalLinks(IEnumerable<GridTraversalLink> links)`
- L77 [function] `public void RemoveOccupantByLayer(GridLayer layer)`
- L82 [function] `public List<IGridOccupant> GetAllOccupants()`
- L85 [function] `FillAllOccupants(result)`
- L88 [function] `public void FillAllOccupants(List<IGridOccupant> result)`
- L100 [function] `public bool ContainsOccupant(IGridOccupant occupant)`
- L117 [function] `public bool CanOccupy(GridLayer layer = GridLayer.Building)`
- L121 [function] `public bool HasOccupantInLayer(GridLayer layer = GridLayer.Building)`
- L125 [function] `public bool HasOccupant()`
- L129 [function] `public bool TrySetOccupant(GridLayer layer, IGridOccupant occupant)`
- L137 [function] `public void SetOccupant(GridLayer layer,IGridOccupant occupant)`
- L139 [function] `TrySetOccupant(layer, occupant)`


## Assets\Scripts\Grid\DungeonStory\Building

### Assets/Scripts/Grid/DungeonStory/Building/DungeonStoryGridBuildingController.cs

- L8 [type] `public class DungeonStoryGridBuildingController : MonoBehaviour` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L31 [function] `public void Construct(IGridSystemProvider gridSystemProvider, IDataCatalog dataCatalog, IWorldPointerPositionProvider worldPointerPositionProvider, IGridTextureProvider gridTextureProvider, IObjectResolver objectResolver, IGameDataProvider gameDataProvider, IGridBuildingObjectFactory gridBuildingObjectFactory)` [DependencyInjection]
- L50 [function] `private void Awake()`
- L60 [function] `private void Start()` [DependencyInjection, SceneMutation]
- L70 [function] `private void EnsureInitialized()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L100 [function] `private void Update()` [DependencyInjection, SceneMutation]
- L111 [function] `public void TriggerPlaceBuilding()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L138 [function] `public void TriggerDestroyBuildableObject()` [DependencyInjection, SceneMutation]
- L157 [function] `private void OnEnable()`
- L167 [function] `private void OnDisable()`
- L177 [function] `public BuildableObject GetBuildingByMousePos()` [DependencyInjection]
- L188 [function] `public bool IsBuildable()` [DependencyInjection]
- L196 [function] `public bool IsBuildableAt(Vector2Int pos)` [DependencyInjection]
- L204 [function] `public Vector3 GetMouseWorldPosSnapped()` [DependencyInjection]
- L212 [function] `public void SelectBuildingById(int id)` [DependencyInjection, SceneMutation]
- L228 [function] `public void ClearBuildingSO()` [SceneMutation]
- L233 [function] `public void SetGridModeBuild()` [DependencyInjection, SceneMutation]
- L241 [function] `public void SetGridModeNone()` [DependencyInjection, SceneMutation]
- L250 [function] `public void SetDestroyMode()` [DependencyInjection, SceneMutation]
- L259 [function] `private void PlaceBuilding(List<Vector2Int> poses)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L287 [function] `private void DrawGridTextureWalls()` [DependencyInjection, SceneMutation]
- L292 [function] `private static bool HasAnyGridOccupants(Grid grid)`
- L307 [function] `private void OnGridExpand()` [SceneMutation]
- L315 [function] `private void OnDestroy()`
- L325 [function] `private void EnsureInputActions()`
- L341 [function] `private void OnBuildingPlaceInput(InputAction.CallbackContext context)`
- L347 [function] `private void OnExpandGridInput(InputAction.CallbackContext context)` [DependencyInjection, SceneMutation]
- L355 [function] `private BuildingSO FindBuildingDataById(int id)` [DependencyInjection]
- L365 [function] `private BuildingConditionContext CreateBuildingConditionContext()` [DependencyInjection]
- L371 [function] `private void ConfigurePlacedBuilding(BuildableObject building)`
- L380 [function] `private void OnPlacedBuildingClicked(BuildableObject building)` [SceneMutation]
- L388 [function] `private GridSystemManager ResolveGridSystem()` [DependencyInjection]
- L399 [function] `private IGridSystemProvider ResolveGridSystemProvider()` [DependencyInjection]
- L405 [function] `private IDataCatalog ResolveDataCatalog()` [DependencyInjection]
- L411 [function] `private Vector3 ResolveMouseWorldPosition()` [DependencyInjection]
- L418 [function] `private GridTexture ResolveGridTexture()` [DependencyInjection]
- L425 [function] `private IObjectResolver ResolveObjectResolver()` [DependencyInjection]
- L431 [function] `private IGameDataProvider ResolveGameDataProvider()` [DependencyInjection]
- L442 [function] `private IGridBuildingObjectFactory ResolveGridBuildingObjectFactory()` [DependencyInjection]
- L437 [function] `private BuildingSO ResolveBuildingDataById(int id)` [DependencyInjection]
- L447 [function] `private bool TryResolveBuildingDataById(int id, out BuildingSO buildingData)` [DependencyInjection]

## Assets\Scripts\Grid\DungeonStory\UI

### Assets/Scripts/Grid/DungeonStory/UI/DungeonStoryGridGhostPresenter.cs

- L6 [type] `public class DungeonStoryGridGhostPresenter : GridPlacementGhostPresenter` [DependencyInjection, SceneMutation]
- L15 [function] `protected override Grid ActiveGrid => RequireGridSystem().grid` [DependencyInjection]
- L16 [function] `protected override bool HasGhostSelection => RequireBuildingController().SelectedBuilding != null` [DependencyInjection]
- L23 [function] `protected override Vector3 MouseWorldPosition => RequireWorldPointerPositionProvider().MouseWorldPosition` [DependencyInjection]
- L24 [function] `protected override Vector3 MouseWorldPositionSnapped => RequireBuildingController().GetMouseWorldPosSnapped()` [DependencyInjection]
- L27 [function] `public void Construct(IGridSystemProvider, IDungeonGridBuildingControllerProvider, IWorldPointerPositionProvider)` [DependencyInjection]
- L41 [function] `protected override void Awake()` [SceneMutation]
- L46 [function] `protected override bool CanPlaceAt(Vector2Int gridPosition)` [DependencyInjection]
- L51 [function] `private void OnGridModeChanged(GridMode gridMode)` [DependencyInjection, SceneMutation]
- L62 [function] `public void OnSelectedChanged(BuildingSO buildingSO)` [SceneMutation]
- L67 [function] `private void OnEnable()` [DependencyInjection]
- L72 [function] `private void Start()` [DependencyInjection]
- L77 [function] `private void OnDisable()` [DependencyInjection]
- L82 [function] `private void SubscribeToRuntimeEventsIfInjected()` [DependencyInjection]
- L90 [function] `private void SubscribeToRuntimeEvents()` [DependencyInjection]
- L107 [function] `private void UnsubscribeFromRuntimeEvents()` [DependencyInjection]
- L121 [function] `private IGridSystemProvider RequireGridSystemProvider()` [DependencyInjection]
- L127 [function] `private GridSystemManager RequireGridSystem()` [DependencyInjection]
- L132 [function] `private IDungeonGridBuildingControllerProvider RequireBuildingControllerProvider()` [DependencyInjection]
- L138 [function] `private DungeonStoryGridBuildingController RequireBuildingController()` [DependencyInjection]
- L143 [function] `private IWorldPointerPositionProvider RequireWorldPointerPositionProvider()` [DependencyInjection]

### Assets/Scripts/Grid/DungeonStory/UI/GridConstructButtonFactory.cs

- L8 [type] `public interface IGridConstructButtonFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L10 [function] `UITab CreateCategoryPanel(GameObject panelPrefab, Transform parent, BuildingCategory category)` [RuntimeObjectCreation, SceneMutation]
- L11 [function] `Button CreateCategoryButton(GameObject buttonPrefab, Transform parent, string labelText, UnityAction onClick)` [RuntimeObjectCreation, SceneMutation]
- L12 [function] `UIBuildingSelectButton CreateBuildingSelectButton(GameObject buttonPrefab, Transform parent, BuildingSO buildingData)` [RuntimeObjectCreation, SceneMutation]
- L15 [type] `public sealed class GridConstructButtonFactory : IGridConstructButtonFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L20 [function] `public GridConstructButtonFactory(ITmpKoreanFontService tmpKoreanFontService)` [DependencyInjection]
- L27 [function] `public GridConstructButtonFactory(ITmpKoreanFontService tmpKoreanFontService, IObjectResolver objectResolver)` [DependencyInjection]
- L37 [function] `public UITab CreateCategoryPanel(GameObject panelPrefab, Transform parent, BuildingCategory category)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L56 [function] `public Button CreateCategoryButton(GameObject buttonPrefab, Transform parent, string labelText, UnityAction onClick)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L88 [function] `public UIBuildingSelectButton CreateBuildingSelectButton(GameObject buttonPrefab, Transform parent, BuildingSO buildingData)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Grid/DungeonStory/UI/GridConstructTab.cs

- L8 [type] `public class GridConstructTab : UITab` [DependencyInjection, SceneMutation]
- L25 [function] `public void ConstructGridConstructTab(IDataCatalog dataCatalog, IUiPopupService popupService, IDungeonGridBuildingControllerProvider buildingControllerProvider, ITmpKoreanFontService tmpKoreanFontService, IGridConstructButtonFactory buttonFactory)` [DependencyInjection]
- L41 [function] `private void Start()` [DependencyInjection, SceneMutation]
- L46 [function] `public override void OnClose()` [DependencyInjection, SceneMutation]
- L52 [function] `public override void OnOpen()` [DependencyInjection, SceneMutation]
- L59 [function] `private void MakeSelectButton()` [DependencyInjection, SceneMutation]
- L89 [function] `private void AddCategoryPanel(BuildingCategory category)` [DependencyInjection, SceneMutation]
- L104 [function] `private bool HasCategoryPanel(BuildingCategory category)`
- L110 [function] `private UITab GetCategoryPanel(BuildingCategory category)`
- L122 [function] `private Transform ResolveCategoryButtonRoot()` [SceneMutation]
- L127 [function] `public void ToggleSelectButton(int category)` [DependencyInjection, SceneMutation]
- L147 [function] `public void RefreshCategoryLabels()` [DependencyInjection, SceneMutation]
- L161 [function] `private static string GetCategoryDisplayName(BuildingCategory category)`
- L176 [function] `private static bool TryGetCategoryDisplayName(string rawText, out string displayName)`
- L194 [function] `private IDataCatalog RequireDataCatalog()` [DependencyInjection]
- L200 [function] `private IUiPopupService RequirePopupService()` [DependencyInjection]
- L206 [function] `private IDungeonGridBuildingControllerProvider RequireBuildingControllerProvider()` [DependencyInjection]
- L212 [function] `private DungeonStoryGridBuildingController RequireBuildingController()` [DependencyInjection]
- L217 [function] `private ITmpKoreanFontService RequireTmpKoreanFontService()` [DependencyInjection]
- L224 [function] `private IGridConstructButtonFactory RequireButtonFactory()` [DependencyInjection]

### Assets/Scripts/Grid/DungeonStory/UI/GridUIManager.cs

- L5 [type] `public class GridUIManager : MonoBehaviour` [DependencyInjection, SceneMutation]
- L18 [function] `public void Construct(IGridSystemProvider, IDungeonGridBuildingControllerProvider)` [DependencyInjection]
- L28 [function] `private void Start()` [DependencyInjection, SceneMutation]
- L35 [function] `private void Update()` [DependencyInjection, SceneMutation]
- L48 [function] `public void ToggleConstructTab()` [SceneMutation]
- L60 [function] `public void ShowConstructTab()` [SceneMutation]
- L65 [function] `public void CloseConstructTab()` [SceneMutation]
- L71 [function] `public void HideGrid()` [DependencyInjection, SceneMutation]
- L77 [function] `public void ShowGrid()` [DependencyInjection, SceneMutation]
- L83 [function] `public void DrawGrid()` [DependencyInjection]
- L88 [function] `private void OnEnable()`
- L92 [function] `private void OnDisable()`
- L96 [function] `private void ResolveRuntimeDependencies()` [DependencyInjection]
- L106 [function] `private IGridSystemProvider RequireGridSystemProvider()` [DependencyInjection]
- L112 [function] `private IDungeonGridBuildingControllerProvider RequireBuildingControllerProvider()` [DependencyInjection]


## Assets\Scripts\Grid\Placement

### Assets/Scripts/Grid/Placement/GridPlacementValidator.cs

- L5 [type] `public class GridPlacementValidator`
- L7 [function] `public bool AreInsideGrid(Grid grid, IReadOnlyList<Vector2Int> positions)`
- L15 [function] `public bool AreInsideHorizontalBounds(Grid grid, IReadOnlyList<Vector2Int> positions, int sidePadding)`
- L21 [function] `public bool CanOccupy(Grid grid, GridLayer layer, IReadOnlyList<Vector2Int> positions)`
- L27 [function] `public bool HasSupportBelow(Grid grid, IReadOnlyList<Vector2Int> positions)`


## Assets\Scripts\Grid\Rendering

### Assets/Scripts/Grid/Rendering/GridTexture.cs

- L7 [type] `public class GridTexture : SerializedMonoBehaviour, IGridBuildingVisual` [RuntimeObjectCreation, SceneMutation]
- L9 [type] `public enum TilemapLayer` [RuntimeObjectCreation, SceneMutation]
- L27 [function] `private void Awake()` [SceneMutation]
- L32 [function] `private void OnValidate()` [SceneMutation]
- L37 [function] `public void DrawBuilding(Dictionary<TilemapLayer, Tile> tiles,Vector2Int selectPos,bool isEven)` [SceneMutation]
- L49 [function] `public void DrawBuilding(BuildingSO buildingData, Vector2Int selectPos)` [RuntimeObjectCreation, SceneMutation]
- L53 [branch] `buildingData.IsStructuralWall` routes only structural walls to wallTilemap, preserving hallway-layer tile visuals.
- L68 [function] `public void DeleteBuilding(Dictionary<TilemapLayer, Tile> tiles,Vector2Int selectPos,bool isEven)` [SceneMutation]
- L80 [function] `public void DeleteBuilding(BuildingSO buildingData, Vector2Int selectPos)` [SceneMutation]
- L84 [branch] `buildingData.IsStructuralWall` clears only structural wall cells from wallTilemap.
- L99 [function] `private void DrawSpriteBuilding(BuildingSO buildingData, Vector2Int selectPos)` [RuntimeObjectCreation, SceneMutation]
- L107 [function] `private void DeleteSpriteBuilding(BuildingSO buildingData, Vector2Int selectPos)` [SceneMutation]
- L115 [function] `private Tile GetSpriteTile(BuildingSO buildingData, Tilemap targetTilemap)` [RuntimeObjectCreation]
- L143 [function] `private bool TryGetTilemap(TilemapLayer layer, bool isEven, out Tilemap targetTilemap)`
- L152 [function] `private static bool HasTileVisual(BuildingSO buildingData)`
- L157 [function] `private static TilemapLayer GetSpriteTilemapLayer(BuildingSO buildingData)`
- L165 [function] `private static Vector3Int GetTilePosition(Vector2Int selectPos)`
- L170 [function] `private void SetWallCell(Vector2Int selectPos, Tile tile)` [SceneMutation]
- L181 [type] `private readonly struct SpriteTileKey : System.IEquatable<SpriteTileKey>`
- L189 [function] `public SpriteTileKey(Sprite sprite, int width, int height, int cellTileHeight, Vector2 tileAnchor)`
- L198 [function] `public bool Equals(SpriteTileKey other)`
- L207 [function] `public override bool Equals(object obj)`
- L212 [function] `public override int GetHashCode()`
- L226 [function] `public void DrawWall(Grid grid)` [SceneMutation]
- L295 [function] `private void SynchronizeHallwayVisuals(Grid grid)` [SceneMutation]
- L320 [function] `private void ClearHallwayTile(Vector2Int gridPos)` [SceneMutation]
- L327 [function] `private static bool CanShareHallwayVisual(BuildableObject building)`
- L333 [function] `private void ClearTile(TilemapLayer layer, bool isEven, Vector3Int tilePos)` [SceneMutation]
- L341 [function] `private void ApplyHallwaySorting()` [SceneMutation]
- L348 [function] `private void ConfigureHallwayTilemap(Dictionary<TilemapLayer, Tilemap> tilemaps, HashSet<Tilemap> configured)` [SceneMutation]
- L374 [type] `public static class GridBuildingTileTransformCalculator`
- L379 [function] `public static Matrix4x4 Calculate(BuildingSO buildingData, int cellTileHeight = DefaultCellTileHeight)`
- L384 [function] `public static Matrix4x4 Calculate(BuildingSO buildingData, int cellTileHeight, Vector2 tileAnchor)`
- L421 [type] `public class GridWallTileCalculator`
- L425 [function] `public GridWallTileCalculator(int cellTileHeight = 3)`
- L430 [function] `public HashSet<Vector2Int> GetWallTilePositions(Grid grid)`


## Assets\Scripts\Grid\System\Editor

### Assets/Scripts/Grid/System/Editor/GridFoundationDebugScenarios.cs

- L6 [type] `public static class GridFoundationDebugScenarios` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L9 [function] `public static void RunFromMenu()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L18 [function] `public static bool RunAll(bool logSuccess)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L22 [function] `RunScenario("????????堉????ㅼ뒧?戮ル탶?⑤베彛???????, VerifyWalkPath, errors)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L23 [function] `RunScenario("??壤굿??????????, VerifyStairPath, errors)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L24 [function] `RunScenario("?????ㅳ늾???????쇨덫雅???β뼯援????る쑏?, VerifyEntryExitPath, errors)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L25 [function] `RunScenario("?????밸븶??????怨쀫뎐?? ??????????밸븶?????癲ル슢????, VerifyUnreachableMovementIsExcluded, errors)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L27 [function] `RunScenario("????????筌뤾쑵???轅몄뫅?????sprite generated visual ?????, VerifyTilelessBuildingDoesNotCreateGeneratedSprite, errors)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L28 [function] `RunScenario("??癲ル슢캉??る쨨??ghost sprite ??ш끽維뽳쭩????????, VerifyDraggedGhostUsesRepeatedSprites, errors)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L48 [function] `private static void RunScenario(string name, System.Func<bool> scenario, List<string> errors)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L59 [function] `private static bool VerifyWalkPath()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L62 [function] `AddHallway(grid, new Vector2Int(0, 0))` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L63 [function] `AddHallway(grid, new Vector2Int(1, 0))` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L64 [function] `AddHallway(grid, new Vector2Int(2, 0))` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L70 [function] `private static bool VerifyStairPath()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L73 [function] `AddHallway(grid, new Vector2Int(0, 0))` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L74 [function] `AddHallway(grid, new Vector2Int(1, 0))` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L75 [function] `AddHallway(grid, new Vector2Int(1, 1))` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L76 [function] `AddHallway(grid, new Vector2Int(2, 1))` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L77 [function] `AddMovement(grid, new Vector2Int(1, 0), new Vector2Int(1, 1), GridMoveType.Stair)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L84 [function] `private static bool VerifyEntryExitPath()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L87 [function] `AddHallway(grid, new Vector2Int(0, 0))` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L88 [function] `AddHallway(grid, new Vector2Int(1, 0))` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L89 [function] `AddHallway(grid, new Vector2Int(2, 0))` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L90 [function] `AddHallway(grid, new Vector2Int(3, 0))` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L100 [function] `private static bool VerifyUnreachableMovementIsExcluded()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L104 [function] `AddHallway(grid, new Vector2Int(1, 0))` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L111 [function] `private static bool VerifyTilelessBuildingDoesNotCreateGeneratedSprite()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L125 [function] `AddHallway(grid, new Vector2Int(x, 0))` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L132 [function] `new GridBuildingFactory()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L133 [function] `new BuildingPlacementValidator())` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L152 [function] `private static bool VerifyDraggedGhostUsesRepeatedSprites()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L174 [function] `new Vector3(0f, 0f, 0f)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L175 [function] `new Vector3(1f, 0f, 0f)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L176 [function] `new Vector3(2f, 0f, 0f)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L205 [function] `private static TestOccupant AddHallway(Grid grid, Vector2Int position)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L212 [function] `private static void AddMovement(Grid grid, Vector2Int from, Vector2Int to, GridMoveType moveType)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L218 [type] `private sealed class TestOccupant : IGridOccupant, IGridMovementOccupant` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L220 [function] `public TestOccupant(int id, bool isMovement, GridMoveType moveType)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Grid/System/Editor/GridVisualDebugScenarios.cs

- L5 [type] `public static class GridVisualDebugScenarios` [EditorAssetAccess]
- L8 [function] `public static void RunFromMenu()` [EditorAssetAccess]
- L17 [function] `public static bool RunAll(bool logSuccess)` [EditorAssetAccess]
- L20 [function] `RunScenario("sprite tile visual footprint alignment", VerifySpriteTileTransformMatchesGridFootprint, errors)` [EditorAssetAccess]
- L40 [function] `private static void RunScenario(string name, System.Func<bool> scenario, List<string> errors)` [EditorAssetAccess]
- L51 [function] `private static bool VerifySpriteTileTransformMatchesGridFootprint()` [EditorAssetAccess]
- L67 [function] `private static bool VerifySpriteTileTransform(string assetPath, float expectedCenterOffsetX, float expectedCenterOffsetY, float expectedWidth, float expectedHeight)` [EditorAssetAccess]

### Assets/Scripts/Grid/System/Editor/GridSystemManagerEditor.cs

- L5 [type] `public class GridSystemManagerEditor : Editor` [EditorAssetAccess, Reflection]
- L7 [function] `public override void OnInspectorGUI()` [EditorAssetAccess, Reflection]
- L9 [function] `DrawDefaultInspector()` [EditorAssetAccess, Reflection]


## Assets\Scripts\Grid\System

### Assets/Scripts/Grid/System/GridSystemManager.cs

- L6 [type] `public enum GridMode` [SceneMutation]
- L14 [type] `public class GridSystemManager : MonoBehaviour` [SceneMutation]
- L37 [function] `protected virtual void Awake()` [SceneMutation]
- L42 [function] `protected virtual void OnEnable()` [SceneMutation]
- L47 [function] `public void EnsureGridInitialized()` [SceneMutation]
- L54 [function] `protected virtual void Start()` [SceneMutation]
- L60 [function] `public void GridExpand(int x,int y)` [SceneMutation]
- L69 [function] `public void SetGridMode(GridMode gridMode)` [SceneMutation]
- L82 [function] `public void SetGridModeBuild()` [SceneMutation]
- L89 [function] `public void SetGridModeNone()` [SceneMutation]
- L94 [function] `public void ToggleBuildMode()` [SceneMutation]
- L99 [function] `public bool TryBeginDragSelection(Vector2Int start, bool horizontalDraggable, bool verticalDraggable)` [SceneMutation]
- L109 [function] `public void UpdateDragSelection(Vector2Int pos, bool horizontalDraggable, bool verticalDraggable)` [SceneMutation]
- L118 [function] `public List<Vector2Int> CompleteDragSelection()` [SceneMutation]
- L124 [function] `public void CancelDragSelection()` [SceneMutation]
- L132 [function] `public Vector3 GetWorldPosSnapped(Vector3 worldPosition)` [SceneMutation]
- L142 [function] `public void NotifyGridObjectChanged()` [SceneMutation]
- L147 [function] `private void RecalculateSelectedPositions(Vector2Int pos, bool horizontalDraggable, bool verticalDraggable)` [SceneMutation]


## Assets\Scripts\Grid\UI

### Assets/Scripts/Grid/UI/GridGhostObject.cs

- L4 [type] `public class GridGhostObject : MonoBehaviour` [RuntimeObjectCreation, SceneMutation]
- L20 [function] `private void Awake()` [RuntimeObjectCreation, SceneMutation]
- L25 [function] `public void Initialize(GameObject target = null)` [RuntimeObjectCreation, SceneMutation]
- L47 [function] `public void Show(Sprite sprite)` [RuntimeObjectCreation, SceneMutation]
- L61 [function] `public void ShowRepeated(Sprite sprite, IReadOnlyList<Vector3> worldPositions, Vector2 tileFootprintSize, IReadOnlyList<bool> buildableStates = null)` [RuntimeObjectCreation, SceneMutation]
- L101 [function] `public void Hide()` [RuntimeObjectCreation, SceneMutation]
- L114 [function] `public void SetBuildable(bool buildable)` [RuntimeObjectCreation, SceneMutation]
- L123 [function] `public void SetWorldPosition(Vector3 worldPosition, float lerpSpeed = 0f)` [RuntimeObjectCreation, SceneMutation]
- L134 [function] `public void SetSize(Vector2 size)` [RuntimeObjectCreation, SceneMutation]
- L141 [function] `private void ApplyFootprintSize()` [RuntimeObjectCreation, SceneMutation]
- L146 [function] `private Vector3 ApplyFootprintSize(SpriteRenderer renderer, Vector2 size)` [RuntimeObjectCreation, SceneMutation]
- L177 [function] `private Color GetPreviewColor(bool buildable)` [RuntimeObjectCreation, SceneMutation]
- L182 [function] `private Color WithPreviewAlpha(Color color)` [RuntimeObjectCreation, SceneMutation]
- L188 [function] `private void EnsureRepeatedRendererCount(int count)` [RuntimeObjectCreation, SceneMutation]
- L210 [function] `private void HideRepeatedRenderers(int keepActiveCount)` [RuntimeObjectCreation, SceneMutation]
- L222 [function] `private void EnsureInitialized()` [RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Grid/UI/GridPlacementGhostPresenter.cs

- L4 [type] `public abstract class GridPlacementGhostPresenter : MonoBehaviour` [RuntimeObjectCreation, SceneMutation]
- L24 [function] `protected abstract bool CanPlaceAt(Vector2Int gridPosition)` [RuntimeObjectCreation, SceneMutation]
- L26 [function] `protected virtual void Awake()` [RuntimeObjectCreation, SceneMutation]
- L28 [function] `EnsureGhostObject()` [RuntimeObjectCreation, SceneMutation]
- L29 [function] `HideGhost()` [RuntimeObjectCreation, SceneMutation]
- L32 [function] `protected virtual void Update()` [RuntimeObjectCreation, SceneMutation]
- L34 [function] `UpdateGhost()` [RuntimeObjectCreation, SceneMutation]
- L37 [function] `protected void RefreshGhostSelection()` [RuntimeObjectCreation, SceneMutation]
- L40 [function] `HideGhost()` [RuntimeObjectCreation, SceneMutation]
- L44 [function] `EnsureGhostObject()` [RuntimeObjectCreation, SceneMutation]
- L51 [function] `protected void HideGhost()` [RuntimeObjectCreation, SceneMutation]
- L53 [function] `EnsureGhostObject()` [RuntimeObjectCreation, SceneMutation]
- L57 [function] `private void UpdateGhost()` [RuntimeObjectCreation, SceneMutation]
- L59 [function] `EnsureGhostObject()` [RuntimeObjectCreation, SceneMutation]
- L65 [function] `HideGhost()` [RuntimeObjectCreation, SceneMutation]
- L71 [function] `UpdateSingleCellGhost(grid)` [RuntimeObjectCreation, SceneMutation]
- L75 [function] `UpdateDraggedGhost(grid)` [RuntimeObjectCreation, SceneMutation]
- L78 [function] `private void UpdateSingleCellGhost(Grid grid)` [RuntimeObjectCreation, SceneMutation]
- L90 [function] `private void UpdateDraggedGhost(Grid grid)` [RuntimeObjectCreation, SceneMutation]
- L115 [function] `private Vector2 GetGhostFootprintSize()` [RuntimeObjectCreation, SceneMutation]
- L122 [function] `private void EnsureGhostObject()` [RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Grid/UI/UIGridTab.cs

- L4 [type] `public class UIGridTab : MonoBehaviour` [SceneMutation]
- L9 [function] `private void Start()` [SceneMutation]
- L11 [function] `CloseTab()` [SceneMutation]
- L13 [function] `public bool ToggleTab(int categoryId)` [SceneMutation]
- L17 [function] `CloseTab()` [SceneMutation]
- L22 [function] `CloseTab()` [SceneMutation]
- L27 [function] `OpenTap()` [SceneMutation]
- L31 [function] `public void OpenTap()` [SceneMutation]
- L35 [function] `public void CloseTab()` [SceneMutation]


## Assets\Scripts\Infrastructure

### Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs

- L5 [type] `public sealed class DungeonRuntimeLifetimeScope : LifetimeScope` [DependencyInjection]
- L7 [function] `protected override void OnDestroy()` [DependencyInjection]
- L12 [function] `protected override void Configure(IContainerBuilder builder)` [DependencyInjection]
- L266 [function] `private static void InjectSceneComponents(IObjectResolver resolver)` [DependencyInjection]
- L506 [function] `private static DungeonSceneRuntimeReferences CaptureSceneRuntimeReferences()` [DependencyInjection]

### Assets/Scripts/Infrastructure/ResourcesAssetLoader.cs

- L6 [type] `public interface IResourcesAssetLoader` [DependencyInjection, ResourcesAccess]
- L8 [function] `T LoadRequired<T>(string resourcePath) where T : UnityEngine.Object` [DependencyInjection, ResourcesAccess]
- L9 [function] `IReadOnlyCollection<T> LoadAllRequired<T>(string resourcePath) where T : UnityEngine.Object` [DependencyInjection, ResourcesAccess]
- L12 [type] `public sealed class UnityResourcesAssetLoader : IResourcesAssetLoader` [DependencyInjection, ResourcesAccess]
- L14 [function] `public T LoadRequired<T>(string resourcePath) where T : UnityEngine.Object` [DependencyInjection, ResourcesAccess]
- L28 [function] `public IReadOnlyCollection<T> LoadAllRequired<T>(string resourcePath) where T : UnityEngine.Object` [DependencyInjection, ResourcesAccess]
- L46 [function] `private static void ValidateResourcePath(string resourcePath)` [ResourcesAccess]

### Assets/Scripts/Infrastructure/GameRuntimeServices.cs

- L5 [type] `public interface IGameDataProvider` [DependencyInjection]
- L7 [function] `bool TryGetGameData(out GameData gameData)` [DependencyInjection]
- L10 [type] `public interface IFloatingNumberFeedbackService` [DependencyInjection]
- L12 [function] `bool TryShow(NumberCondition condition, Vector3 worldPosition, float value)` [DependencyInjection]
- L15 [type] `public sealed class GameManagerGameDataProvider : IGameDataProvider` [DependencyInjection]
- L20 [function] `public GameManagerGameDataProvider(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L26 [function] `public bool TryGetGameData(out GameData gameData)` [DependencyInjection]
- L34 [type] `public sealed class GameManagerFloatingNumberFeedbackService : IFloatingNumberFeedbackService` [DependencyInjection]
- L39 [function] `public GameManagerFloatingNumberFeedbackService(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L45 [function] `public bool TryShow(NumberCondition condition, Vector3 worldPosition, float value)` [DependencyInjection]
- L59 [function] `private GameManager ResolveGameManager()` [DependencyInjection]

### Assets/Scripts/Infrastructure/CodexReferenceCatalogServices.cs

- L5 [type] `public interface ICodexReferenceCatalog` [DependencyInjection]
- L7 [function] `IReadOnlyCollection<CharacterSpeciesSO> Species` [DependencyInjection]
- L8 [function] `IReadOnlyCollection<BuildingSO> Facilities` [DependencyInjection]
- L11 [type] `public sealed class DataCatalogCodexReferenceCatalog : ICodexReferenceCatalog` [DependencyInjection]
- L15 [function] `public DataCatalogCodexReferenceCatalog(IDataCatalog catalog)` [DependencyInjection]
- L21 [function] `public IReadOnlyCollection<CharacterSpeciesSO> Species` [DependencyInjection]
- L28 [function] `public IReadOnlyCollection<BuildingSO> Facilities` [DependencyInjection]

### Assets/Scripts/Infrastructure/CharacterAiActionAssetCatalog.cs

- L3 [type] `public interface ICharacterAiActionAssetCatalog` [DependencyInjection]
- L5 [function] `T GetRequiredAction<T>(string resourcePath) where T : AIActionSet` [DependencyInjection]
- L6 [function] `AIFacilityRoleAction GetRequiredFacilityRoleAction(string resourcePath, FacilityRole role)` [DependencyInjection]
- L9 [type] `public sealed class ResourceCharacterAiActionAssetCatalog : ICharacterAiActionAssetCatalog` [DependencyInjection]
- L13 [function] `public ResourceCharacterAiActionAssetCatalog(IResourcesAssetLoader resourcesAssetLoader)` [DependencyInjection]
- L19 [function] `public T GetRequiredAction<T>(string resourcePath) where T : AIActionSet` [DependencyInjection]
- L29 [function] `public AIFacilityRoleAction GetRequiredFacilityRoleAction(string resourcePath, FacilityRole role)` [DependencyInjection]

### Assets/Scripts/Infrastructure/CharacterAiSchedulingService.cs

- L3 [type] `public interface ICharacterAiSchedulingService` [DependencyInjection]
- L5 [function] `bool IsDrivingAi { get; }` [DependencyInjection]
- L6 [function] `void Register(CharacterActor actor)` [DependencyInjection]
- L7 [function] `void Unregister(CharacterActor actor)` [DependencyInjection]
- L8 [function] `void RequestImmediateDecision(CharacterActor actor)` [DependencyInjection]
- L9 [function] `bool TryConsumePathSearchBudget()` [DependencyInjection]
- L10 [function] `bool ShouldShowCharacterFeedback(CharacterActor actor)` [DependencyInjection]
- L11 [function] `int GetMovementFrameStride(CharacterActor actor)` [DependencyInjection]
- L12 [function] `void ResetPathSearchBudgetForDebug()` [DependencyInjection]
- L15 [type] `public sealed class CharacterAiSchedulingService : ICharacterAiSchedulingService` [DependencyInjection]
- L20 [function] `public CharacterAiSchedulingService(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L26 [function] `public bool IsDrivingAi => ResolveScheduler().IsDrivingAi` [DependencyInjection]
- L28 [function] `public void Register(CharacterActor actor)` [DependencyInjection]
- L33 [function] `public void Unregister(CharacterActor actor)` [DependencyInjection]
- L38 [function] `public void RequestImmediateDecision(CharacterActor actor)` [DependencyInjection]
- L43 [function] `public bool TryConsumePathSearchBudget()` [DependencyInjection]
- L48 [function] `public bool ShouldShowCharacterFeedback(CharacterActor actor)` [DependencyInjection]
- L53 [function] `public int GetMovementFrameStride(CharacterActor actor)` [DependencyInjection]
- L58 [function] `public void ResetPathSearchBudgetForDebug()` [DependencyInjection]
- L66 [function] `private CharacterAiScheduler ResolveScheduler()` [DependencyInjection]
- L73 [function] `private bool TryResolveScheduler(out CharacterAiScheduler resolvedScheduler)` [DependencyInjection]

### Assets/Scripts/Infrastructure/TmpKoreanFontProvider.cs

- L4 [type] `public sealed class ResourceTmpKoreanFontProvider : ITmpKoreanFontProvider` [DependencyInjection]
- L12 [function] `public ResourceTmpKoreanFontProvider(IResourcesAssetLoader resourcesAssetLoader)` [DependencyInjection]
- L18 [function] `public TMP_FontAsset GetRequiredFont()` [DependencyInjection]

### Assets/Scripts/Infrastructure/DataScriptableObjectSource.cs

- L4 [type] `public interface IDataScriptableObjectSource` [DependencyInjection]
- L6 [function] `IReadOnlyCollection<DataScriptableObject> LoadAll()` [DependencyInjection]
- L9 [type] `public sealed class ResourceDataScriptableObjectSource : IDataScriptableObjectSource` [DependencyInjection]
- L15 [function] `public ResourceDataScriptableObjectSource(IResourcesAssetLoader resourcesAssetLoader)` [DependencyInjection]
- L21 [function] `public IReadOnlyCollection<DataScriptableObject> LoadAll()` [DependencyInjection]

### Assets/Scripts/Infrastructure/LocalLlmRuntimeProvider.cs

- L3 [type] `public interface ILocalLlmRuntimeProvider` [DependencyInjection]
- L9 [type] `public sealed class LocalLlmRuntimeProvider : ILocalLlmRuntimeProvider` [DependencyInjection]
- L14 [function] `public LocalLlmRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L19 [function] `public bool TryGetRuntime(out ILocalLlmRuntime resolvedRuntime)` [DependencyInjection]
- L26 [function] `public ILocalLlmRuntime GetRequiredRuntime()` [DependencyInjection]

### Assets/Scripts/Infrastructure/BlueprintResearchRuntimeProvider.cs

- L3 [type] `public interface IBlueprintResearchRuntimeProvider` [DependencyInjection]
- L8 [type] `public interface IBlueprintResearchWorkService` [DependencyInjection]
- L17 [type] `public interface IBlueprintResearchStateService` [DependencyInjection]
- L22 [type] `public sealed class BlueprintResearchRuntimeProvider : IBlueprintResearchRuntimeProvider` [DependencyInjection]
- L26 [function] `public BlueprintResearchRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L32 [function] `public bool TryGetRuntime(out BlueprintResearchRuntime runtime)` [DependencyInjection]
- L39 [type] `public sealed class BlueprintResearchWorkService : IBlueprintResearchWorkService` [DependencyInjection]
- L43 [function] `public BlueprintResearchWorkService(IBlueprintResearchRuntimeProvider runtimeProvider)` [DependencyInjection]
- L49 [function] `public bool HasResearchWorkFor(BuildableObject facility)` [DependencyInjection]
- L57 [function] `public BlueprintResearchWorkResult ApplyResearchWork(` [DependencyInjection]
- L78 [type] `public sealed class BlueprintResearchStateService : IBlueprintResearchStateService` [DependencyInjection]
- L82 [function] `public BlueprintResearchStateService(IBlueprintResearchRuntimeProvider runtimeProvider)` [DependencyInjection]
- L88 [function] `public BlueprintResearchState GetState()` [DependencyInjection]

### Assets/Scripts/Infrastructure/FacilityShopRuntimeProvider.cs

- L5 [type] `public interface IDailyFacilityShopRuntimeProvider` [DependencyInjection]
- L7 [function] `bool TryGetRuntime(out DailyFacilityShopRuntime runtime)` [DependencyInjection]
- L10 [type] `public interface IFacilityShopCatalog` [DependencyInjection]
- L12 [function] `IReadOnlyCollection<BuildingSO> Buildings { get; }` [DependencyInjection]
- L13 [function] `IReadOnlyCollection<FacilityBlueprintSO> Blueprints { get; }` [DependencyInjection]
- L14 [function] `BuildingSO FindBuildingById(int buildingId)` [DependencyInjection]
- L17 [type] `public interface IFacilityShopUnlockStateService` [DependencyInjection]
- L19 [function] `FacilityShopUnlockState GetUnlockState()` [DependencyInjection]
- L22 [type] `public sealed class DailyFacilityShopRuntimeProvider : IDailyFacilityShopRuntimeProvider` [DependencyInjection]
- L26 [function] `public DailyFacilityShopRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L32 [function] `public bool TryGetRuntime(out DailyFacilityShopRuntime runtime)` [DependencyInjection]
- L39 [type] `public sealed class FacilityShopUnlockStateService : IFacilityShopUnlockStateService` [DependencyInjection]
- L43 [function] `public FacilityShopUnlockStateService(IDailyFacilityShopRuntimeProvider runtimeProvider)` [DependencyInjection]
- L49 [function] `public FacilityShopUnlockState GetUnlockState()` [DependencyInjection]
- L60 [type] `public sealed class DataCatalogFacilityShopCatalog : IFacilityShopCatalog` [DependencyInjection]
- L64 [function] `public DataCatalogFacilityShopCatalog(IDataCatalog catalog)` [DependencyInjection]
- L70 [function] `public IReadOnlyCollection<BuildingSO> Buildings => catalog.GetData<BuildingSO>()...` [DependencyInjection]
- L76 [function] `public IReadOnlyCollection<FacilityBlueprintSO> Blueprints => catalog.GetData<FacilityBlueprintSO>()...` [DependencyInjection]
- L82 [function] `public BuildingSO FindBuildingById(int buildingId)` [DependencyInjection]

### Assets/Scripts/Infrastructure/FacilityRecipeCatalogServices.cs

- L5 [type] `public interface IFacilitySynthesisRecipeCatalog` [DependencyInjection]
- L7 [function] `IReadOnlyList<FacilitySynthesisRecipeSO> GetRecipes()` [DependencyInjection]
- L10 [type] `public interface IFacilitySynthesisRecipeQuery` [DependencyInjection]
- L12 [function] `IReadOnlyList<FacilitySynthesisRecipeSO> GetAllRecipes()` [DependencyInjection]
- L13 [function] `bool IsVisible(FacilitySynthesisRecipeSO recipe, BlueprintResearchState researchState)` [DependencyInjection]
- L14 [function] `IReadOnlyList<FacilitySynthesisRecipeSO> GetVisibleRecipes(BlueprintResearchState researchState)` [DependencyInjection]
- L15 [function] `FacilitySynthesisRecipeSnapshot ToSnapshot(FacilitySynthesisRecipeSO recipe, BlueprintResearchState researchState)` [DependencyInjection]
- L20 [type] `public interface IFacilityEvolutionRecipeQuery : IFacilityEvolutionRecipeProvider` [DependencyInjection]
- L22 [function] `bool IsVisible(FacilityEvolutionRecipeSO recipe, BlueprintResearchState researchState)` [DependencyInjection]
- L23 [function] `IReadOnlyList<FacilityEvolutionRecipeSO> GetVisibleRecipes(BlueprintResearchState researchState)` [DependencyInjection]
- L24 [function] `IReadOnlyList<FacilityEvolutionRecipeSO> GetSourceCandidates(BuildableObject facility, BlueprintResearchState researchState)` [DependencyInjection]
- L29 [type] `public sealed class DataCatalogFacilitySynthesisRecipeCatalog : IFacilitySynthesisRecipeCatalog` [DependencyInjection]
- L33 [function] `public DataCatalogFacilitySynthesisRecipeCatalog(IDataCatalog catalog)` [DependencyInjection]
- L39 [function] `public IReadOnlyList<FacilitySynthesisRecipeSO> GetRecipes()` [DependencyInjection]
- L50 [type] `public sealed class FacilitySynthesisRecipeQuery : IFacilitySynthesisRecipeQuery` [DependencyInjection]
- L55 [function] `public FacilitySynthesisRecipeQuery(IFacilitySynthesisRecipeCatalog catalog, IMetaProgressionRuntimeReader metaProgressionReader)` [DependencyInjection]
- L65 [function] `public IReadOnlyList<FacilitySynthesisRecipeSO> GetAllRecipes()` [DependencyInjection]
- L70 [function] `public bool IsVisible(FacilitySynthesisRecipeSO recipe, BlueprintResearchState researchState)` [DependencyInjection]
- L75 [function] `public IReadOnlyList<FacilitySynthesisRecipeSO> GetVisibleRecipes(BlueprintResearchState researchState)` [DependencyInjection]
- L82 [function] `public FacilitySynthesisRecipeSnapshot ToSnapshot(...)` [DependencyInjection]
- L90 [type] `public sealed class DataCatalogFacilityEvolutionRecipeProvider : IFacilityEvolutionRecipeProvider` [DependencyInjection]
- L94 [function] `public DataCatalogFacilityEvolutionRecipeProvider(IDataCatalog catalog)` [DependencyInjection]
- L100 [function] `public IReadOnlyList<FacilityEvolutionRecipeSO> GetRecipes()` [DependencyInjection]
- L111 [type] `public sealed class FacilityEvolutionRecipeQuery : IFacilityEvolutionRecipeQuery` [DependencyInjection]
- L117 [function] `public FacilityEvolutionRecipeQuery(IFacilityEvolutionRecipeProvider provider, IMetaProgressionRuntimeReader metaProgressionReader, IFacilityEvolutionStateService stateService)` [DependencyInjection]
- L130 [function] `public IReadOnlyList<FacilityEvolutionRecipeSO> GetRecipes()` [DependencyInjection]
- L135 [function] `public bool IsVisible(FacilityEvolutionRecipeSO recipe, BlueprintResearchState researchState)` [DependencyInjection]
- L140 [function] `public IReadOnlyList<FacilityEvolutionRecipeSO> GetVisibleRecipes(BlueprintResearchState researchState)` [DependencyInjection]
- L147 [function] `public IReadOnlyList<FacilityEvolutionRecipeSO> GetSourceCandidates(BuildableObject facility, BlueprintResearchState researchState)` [DependencyInjection]
- L160 [type] `public sealed class DataCatalogFacilityEvolutionRecordTokenDefinitionProvider : IFacilityEvolutionRecordTokenDefinitionProvider` [DependencyInjection]
- L160 [function] `public DataCatalogFacilityEvolutionRecordTokenDefinitionProvider(IDataCatalog catalog)` [DependencyInjection]
- L166 [function] `public IReadOnlyList<FacilityEvolutionRecordTokenDefinitionSO> GetDefinitions()` [DependencyInjection]
- L177 [function] `public FacilityEvolutionRecordTokenDefinitionSO GetDefinition(string tokenId)` [DependencyInjection]

### Assets/Scripts/Infrastructure/ShopStockCatalogService.cs

- L5 [type] `public interface IShopStockCatalog` [DependencyInjection]
- L7 [function] `bool TryGetStockInfoForShop(int shopId, out StockInfo stockInfo)` [DependencyInjection]
- L8 [function] `bool TryGetSaleItem(int saleItemId, out SaleItem saleItem)` [DependencyInjection]
- L9 [function] `StockCategory GetStockCategory(int saleItemId)` [DependencyInjection]
- L12 [type] `public sealed class ShopStockCatalog : IShopStockCatalog` [DependencyInjection]
- L16 [function] `public ShopStockCatalog(IDataCatalog dataCatalog)` [DependencyInjection]
- L22 [function] `public bool TryGetStockInfoForShop(int shopId, out StockInfo stockInfo)` [DependencyInjection]
- L29 [function] `public bool TryGetSaleItem(int saleItemId, out SaleItem saleItem)` [DependencyInjection]
- L35 [function] `public StockCategory GetStockCategory(int saleItemId)` [DependencyInjection]

### Assets/Scripts/Infrastructure/DataCatalogService.cs

- L4 [type] `public interface IDataCatalog` [DependencyInjection]
- L9 [type] `public sealed class DataManagerCatalog : IDataCatalog` [DependencyInjection]
- L13 [function] `public DataManagerCatalog(DataManager dataManager)` [DependencyInjection]
- L19 [function] `public IReadOnlyDictionary<int, T> GetData<T>() where T : DataScriptableObject` [DependencyInjection]
- L27 [type] `public interface IBuildingDefinitionLookup` [DependencyInjection]
- L32 [type] `public sealed class BuildingDefinitionLookup : IBuildingDefinitionLookup` [DependencyInjection]
- L36 [function] `public BuildingDefinitionLookup(IDataCatalog catalog)` [DependencyInjection]
- L41 [function] `public BuildingSO GetBuilding(int id)` [DependencyInjection]

### Assets/Scripts/Infrastructure/DungeonBackdropReferenceProvider.cs

- L5 [type] `public interface IDungeonBackdropReferenceProvider` [DependencyInjection]
- L11 [type] `public sealed class DungeonBackdropReferenceProvider : IDungeonBackdropReferenceProvider` [DependencyInjection]
- L17 [function] `public DungeonBackdropReferenceProvider(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L40 [function] `private Transform FindTransform(string objectName)` [DependencyInjection]
- L53 [function] `private Tilemap FindTilemap(string tilemapName)` [DependencyInjection]

### Assets/Scripts/Infrastructure/DungeonGridBuildingRuntimeProviders.cs

- L4 [type] `public interface IDungeonGridBuildingControllerProvider` [DependencyInjection]
- L9 [type] `public interface IWorldPointerPositionProvider` [DependencyInjection]
- L14 [type] `public interface IMainCameraProvider` [DependencyInjection]
- L19 [type] `public interface IGridTextureProvider` [DependencyInjection]
- L24 [type] `public sealed class DungeonGridBuildingControllerProvider : IDungeonGridBuildingControllerProvider` [DependencyInjection]
- L29 [function] `public DungeonGridBuildingControllerProvider(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L45 [type] `public sealed class GridTextureProvider : IGridTextureProvider` [DependencyInjection]
- L50 [function] `public GridTextureProvider(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L66 [type] `public sealed class SceneCameraWorldPointerPositionProvider : IWorldPointerPositionProvider` [DependencyInjection]
- L70 [function] `public SceneCameraWorldPointerPositionProvider(IMainCameraProvider cameraProvider)` [DependencyInjection]
- L75 [function] `public Vector3 MouseWorldPosition` [DependencyInjection]
- L89 [type] `public sealed class SceneMainCameraProvider : IMainCameraProvider` [DependencyInjection]
- L94 [function] `public SceneMainCameraProvider(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L99 [function] `public Camera Camera` [DependencyInjection]

### Assets/Scripts/Infrastructure/DungeonSceneComponentQuery.cs

- L5 [type] `public interface IDungeonSceneComponentQuery` [DependencyInjection]
- L11 [type] `public sealed class DungeonSceneComponentQuery : IDungeonSceneComponentQuery` [DependencyInjection, GlobalObjectLookup, ResourcesAccess]
- L13 [function] `public T First<T>(bool includeInactive = false) where T : Component` [DependencyInjection, GlobalObjectLookup, ResourcesAccess]
- L19 [function] `public IReadOnlyList<T> All<T>(bool includeInactive = false) where T : Component` [DependencyInjection, GlobalObjectLookup, ResourcesAccess]
- L43 [function] `private static bool IsLoadedSceneComponent(Component component, bool includeInactive)` [DependencyInjection]

### Assets/Scripts/Infrastructure/DungeonSceneRuntimeReferences.cs

- L3 [type] `public sealed class DungeonSceneRuntimeReferences` [DependencyInjection]
- L5 [function] `public DungeonSceneRuntimeReferences(UIManager, OperatingDaySettlementRuntime, EventAlertRuntime, RunVariableRuntime, Canvas)` [DependencyInjection]

### Assets/Scripts/Infrastructure/CharacterSpawnerProvider.cs

- L3 [type] `public interface ICharacterSpawnerProvider` [DependencyInjection]
- L8 [type] `public sealed class CharacterSpawnerProvider : ICharacterSpawnerProvider` [DependencyInjection]
- L13 [function] `public CharacterSpawnerProvider(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L19 [function] `public bool TryGetSpawner(out CharacterSpawner resolvedSpawner)` [DependencyInjection]

### Assets/Scripts/Infrastructure/GridSystemProvider.cs

- L3 [type] `public interface IGridSystemProvider` [DependencyInjection]
- L11 [type] `public sealed class GridSystemProvider : IGridSystemProvider` [DependencyInjection]
- L16 [function] `public GridSystemProvider(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L47 [function] `public bool TryGetManager(out GridSystemManager resolvedManager)` [DependencyInjection]
- L61 [function] `public bool TryGetGrid(out Grid grid)` [DependencyInjection]

### Assets/Scripts/Infrastructure/RegularCustomerRuntimeProvider.cs

- L3 [type] `public interface IRegularCustomerRuntimeProvider` [DependencyInjection]
- L8 [type] `public sealed class RegularCustomerRuntimeProvider : IRegularCustomerRuntimeProvider` [DependencyInjection]
- L13 [function] `public RegularCustomerRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L19 [function] `public bool TryGetRuntime(out RegularCustomerRuntime resolvedRuntime)` [DependencyInjection]

### Assets/Scripts/Infrastructure/InvasionThreatRuntimeProvider.cs

- L1 [type] `public interface IInvasionThreatRuntimeProvider` [DependencyInjection]
- L6 [type] `public sealed class InvasionThreatRuntimeProvider : IInvasionThreatRuntimeProvider` [DependencyInjection]
- L11 [function] `public InvasionThreatRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L17 [function] `public bool TryGetRuntime(out InvasionThreatRuntime resolvedRuntime)` [DependencyInjection]

### Assets/Scripts/Infrastructure/InvasionIntruderDataProvider.cs

- L3 [type] `public sealed class ResourceInvasionIntruderDataProvider : IInvasionIntruderDataProvider` [DependencyInjection]
- L9 [function] `public ResourceInvasionIntruderDataProvider(IResourcesAssetLoader resourcesAssetLoader)` [DependencyInjection]
- L15 [function] `public CharacterSO GetRequiredIntruderData(CharacterSO configuredData)` [DependencyInjection]

### Assets/Scripts/Infrastructure/MetaProgressionRuntimeProvider.cs

- L4 [type] `public interface IMetaProgressionRuntimeProvider` [DependencyInjection]
- L9 [type] `public interface IMetaProgressionRuntimeReader` [DependencyInjection]
- L19 [type] `public sealed class MetaProgressionRuntimeProvider : IMetaProgressionRuntimeProvider` [DependencyInjection]
- L23 [function] `public MetaProgressionRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L29 [function] `public bool TryGetRuntime(out MetaProgressionRuntime runtime)` [DependencyInjection]
- L36 [type] `public sealed class MetaProgressionRuntimeReader : IMetaProgressionRuntimeReader` [DependencyInjection]
- L40 [function] `public MetaProgressionRuntimeReader(IMetaProgressionRuntimeProvider provider)` [DependencyInjection]
- L46 [function] `public int GetStartingFacilityCandidateBonus()` [DependencyInjection]
- L53 [function] `public int GetStartingOwnerTraitCandidateBonus()` [DependencyInjection]
- L60 [function] `public float GetOwnerMaxHealthMultiplier()` [DependencyInjection]
- L67 [function] `public float GetInvasionWarningThresholdMultiplier()` [DependencyInjection]
- L74 [function] `public bool IsRecipePreserved(string recipeId)` [DependencyInjection]
- L80 [function] `public IReadOnlyCollection<int> GetExpandedBasicPurchaseBuildingIds(IEnumerable<BuildingSO> buildings)` [DependencyInjection]

### Assets/Scripts/Infrastructure/RunVariableRuntimeProvider.cs

- L4 [type] `public interface IRunVariableRuntimeProvider` [DependencyInjection]
- L6 [function] `bool TryGetRuntime(out RunVariableRuntime runtime)` [DependencyInjection]
- L9 [type] `public interface IRunVariableRuntimeReader` [DependencyInjection]
- L11 [function] `int GetInitialShopSeed()` [DependencyInjection]
- L12 [function] `float GetGuestDemandMultiplier(string speciesTag)` [DependencyInjection]
- L13 [function] `float GetStockCostMultiplier(StockCategory category)` [DependencyInjection]
- L14 [function] `float GetFacilityShopCostMultiplier(BuildingSO building)` [DependencyInjection]
- L15 [function] `float GetBlueprintCostMultiplier(FacilityBlueprintSO blueprint)` [DependencyInjection]
- L16 [function] `float GetThreatRiseMultiplier()` [DependencyInjection]
- L17 [function] `float GetWarningThresholdMultiplier()` [DependencyInjection]
- L18 [function] `InvasionIntruderSettings ApplyInvasionSettings(InvasionIntruderSettings source)` [DependencyInjection]
- L21 [type] `public interface IOwnerRunDataProvider` [DependencyInjection]
- L23 [function] `CharacterSO SelectedOwnerData { get; }` [DependencyInjection]
- L26 [type] `public interface IOwnerRunManagerProvider` [DependencyInjection]
- L28 [function] `bool TryGetManager(out OwnerRunManager manager)` [DependencyInjection]
- L31 [type] `public interface IOwnerRunLifecycleService` [DependencyInjection]
- L33 [function] `void HandleOwnerDeath(CharacterActor owner, string reason)` [DependencyInjection]
- L36 [type] `public sealed class RunVariableRuntimeProvider : IRunVariableRuntimeProvider` [DependencyInjection]
- L41 [function] `public RunVariableRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L47 [function] `public bool TryGetRuntime(out RunVariableRuntime resolvedRuntime)` [DependencyInjection]
- L55 [type] `public sealed class RunVariableRuntimeReader : IRunVariableRuntimeReader` [DependencyInjection]
- L59 [function] `public RunVariableRuntimeReader(IRunVariableRuntimeProvider provider)` [DependencyInjection]
- L65 [function] `public int GetInitialShopSeed()` [DependencyInjection]
- L73 [function] `public float GetGuestDemandMultiplier(string speciesTag)` [DependencyInjection]
- L80 [function] `public float GetStockCostMultiplier(StockCategory category)` [DependencyInjection]
- L87 [function] `public float GetFacilityShopCostMultiplier(BuildingSO building)` [DependencyInjection]
- L94 [function] `public float GetBlueprintCostMultiplier(FacilityBlueprintSO blueprint)` [DependencyInjection]
- L101 [function] `public float GetThreatRiseMultiplier()` [DependencyInjection]
- L108 [function] `public float GetWarningThresholdMultiplier()` [DependencyInjection]
- L115 [function] `public InvasionIntruderSettings ApplyInvasionSettings(InvasionIntruderSettings source)` [DependencyInjection]
- L123 [type] `public sealed class OwnerRunDataProvider : IOwnerRunDataProvider, IOwnerRunManagerProvider` [DependencyInjection]
- L128 [function] `public OwnerRunDataProvider(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L134 [function] `public CharacterSO SelectedOwnerData` [DependencyInjection]
- L144 [function] `public bool TryGetManager(out OwnerRunManager manager)` [DependencyInjection]
- L152 [type] `public sealed class OwnerRunLifecycleService : IOwnerRunLifecycleService` [DependencyInjection]
- L156 [function] `public OwnerRunLifecycleService(IOwnerRunManagerProvider provider)` [DependencyInjection]
- L162 [function] `public void HandleOwnerDeath(CharacterActor owner, string reason)` [DependencyInjection]

### Assets/Scripts/Infrastructure/SceneBuildableLeakValidator.cs

- L7 [type] `public sealed class SceneBuildableLeakValidator : IInitializable` [DependencyInjection, SceneMutation]
- L11 [function] `public SceneBuildableLeakValidator(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L16 [function] `public void Initialize()` [SceneMutation]
- L35 [function] `private void CollectLeakedFacilities(List<string> invalidSceneObjects)` [SceneMutation]
- L51 [function] `private static void CollectMissingScriptObjects(List<string> invalidSceneObjects)` [SceneMutation]
- L66 [function] `private static void CollectMissingScriptObjects(GameObject gameObject, List<string> invalidSceneObjects)` [SceneMutation]
- L90 [function] `private static bool IsLeakedFacilityRoot(BuildableObject buildable)` [SceneMutation]
- L108 [function] `private static string DescribeLeakedBuildable(BuildableObject buildable)` [SceneMutation]
- L114 [function] `private static string DescribeMissingScript(GameObject gameObject)` [SceneMutation]
- L119 [function] `private static string GetHierarchyPath(GameObject gameObject)` [SceneMutation]

### Assets/Scripts/Infrastructure/RunVariableCatalogServices.cs

- L5 [type] `public interface IRunCharacterCatalog` [DependencyInjection]
- L7 [function] `IReadOnlyCollection<CharacterSO> Characters { get; }` [DependencyInjection]
- L10 [type] `public interface IOwnerCandidateCatalog` [DependencyInjection]
- L12 [function] `IReadOnlyCollection<CharacterSO> OwnerCandidates { get; }` [DependencyInjection]
- L15 [type] `public interface IRunStartVariableCatalog` [DependencyInjection]
- L17 [function] `IReadOnlyCollection<BuildingSO> Buildings { get; }` [DependencyInjection]
- L18 [function] `IReadOnlyCollection<CharacterSO> Characters { get; }` [DependencyInjection]
- L19 [function] `IReadOnlyCollection<FacilityBlueprintSO> Blueprints { get; }` [DependencyInjection]
- L22 [type] `public sealed class ResourceRunCharacterCatalog : IRunCharacterCatalog` [DependencyInjection]
- L29 [function] `public ResourceRunCharacterCatalog(IResourcesAssetLoader resourcesAssetLoader)` [DependencyInjection]
- L35 [function] `public IReadOnlyCollection<CharacterSO> Characters` [DependencyInjection]
- L48 [type] `public sealed class ResourceOwnerCandidateCatalog : IOwnerCandidateCatalog` [DependencyInjection]
- L52 [function] `public ResourceOwnerCandidateCatalog(IRunCharacterCatalog characterCatalog)` [DependencyInjection]
- L58 [function] `public IReadOnlyCollection<CharacterSO> OwnerCandidates` [DependencyInjection]
- L65 [type] `public sealed class RunStartVariableCatalog : IRunStartVariableCatalog` [DependencyInjection]
- L70 [function] `public RunStartVariableCatalog(IDataCatalog catalog, IRunCharacterCatalog characterCatalog)` [DependencyInjection]
- L78 [function] `public IReadOnlyCollection<BuildingSO> Buildings` [DependencyInjection]
- L84 [function] `public IReadOnlyCollection<CharacterSO> Characters => characterCatalog.Characters` [DependencyInjection]
- L86 [function] `public IReadOnlyCollection<FacilityBlueprintSO> Blueprints` [DependencyInjection]

### Assets/Scripts/Infrastructure/StaffDiscontentRuntimeProvider.cs

- L3 [type] `public interface IStaffDiscontentRuntimeProvider` [DependencyInjection]
- L8 [type] `public interface IStaffDiscontentRuntimeService` [DependencyInjection]
- L16 [type] `public sealed class StaffDiscontentRuntimeProvider : IStaffDiscontentRuntimeProvider` [DependencyInjection]
- L21 [function] `public StaffDiscontentRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L27 [function] `public bool TryGetRuntime(out StaffDiscontentRuntime resolvedRuntime)` [DependencyInjection]
- L35 [type] `public sealed class StaffDiscontentRuntimeService : IStaffDiscontentRuntimeService` [DependencyInjection]
- L39 [function] `public StaffDiscontentRuntimeService(IStaffDiscontentRuntimeProvider provider)` [DependencyInjection]
- L45 [function] `public float GetWorkEfficiencyMultiplier(CharacterActor staff)` [DependencyInjection]
- L52 [function] `public bool ShouldBlockWork(CharacterActor staff, out string reason)` [DependencyInjection]
- L60 [function] `public bool IsRebellionTarget(CharacterActor target)` [DependencyInjection]
- L67 [function] `public bool ResolveSuppressedRebel(CharacterActor rebel, CharacterActor defender)` [DependencyInjection]

### Assets/Scripts/Infrastructure/SocialReputationRuntimeProvider.cs

- L3 [type] `public interface ISocialReputationRuntimeProvider` [DependencyInjection]
- L8 [type] `public interface ISocialReputationBiasService` [DependencyInjection]
- L13 [type] `public sealed class SocialReputationRuntimeProvider : ISocialReputationRuntimeProvider` [DependencyInjection]
- L18 [function] `public SocialReputationRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L23 [function] `public bool TryGetRuntime(out SocialReputationRuntime resolvedRuntime)` [DependencyInjection]
- L31 [type] `public sealed class SocialReputationBiasService : ISocialReputationBiasService` [DependencyInjection]
- L35 [function] `public SocialReputationBiasService(ISocialReputationRuntimeProvider provider)` [DependencyInjection]
- L40 [function] `public float GetFacilityUtilityBias(CharacterActor actor, BuildableObject building)` [DependencyInjection]

### Assets/Scripts/Infrastructure/RuntimePanelProviders.cs

- L3 [type] `public interface IFacilityEvolutionRuntimeProvider` [DependencyInjection]
- L8 [type] `public interface IFacilitySynthesisRuntimeProvider` [DependencyInjection]
- L13 [type] `public interface ICodexRuntimeProvider` [DependencyInjection]
- L18 [type] `public sealed class FacilityEvolutionRuntimeProvider : IFacilityEvolutionRuntimeProvider` [DependencyInjection]
- L23 [function] `public FacilityEvolutionRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L39 [type] `public sealed class FacilitySynthesisRuntimeProvider : IFacilitySynthesisRuntimeProvider` [DependencyInjection]
- L44 [function] `public FacilitySynthesisRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L60 [type] `public sealed class CodexRuntimeProvider : ICodexRuntimeProvider` [DependencyInjection]
- L65 [function] `public CodexRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]

## Assets\Scripts\Invasion\Editor

### Assets/Scripts/Invasion/Editor/InvasionCombatReportDebugScenarios.cs

- L7 [type] `public static class InvasionCombatReportDebugScenarios` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L10 [function] `public static void RunFromMenu()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L19 [function] `public static bool RunAll(bool logSuccess)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L22 [function] `RunScenario("????꾣뤃???熬곣뫖利든뜏類ｋ걝??????ㅻ쿅??? ?嚥▲굧???????됰Ŋ???, VerifyCombatFeedbackAndSummary, errors)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L23 [function] `RunScenario("???ㅻ쿋驪?????????붺몭?겹럷筌뤿슣異??, VerifySummaryHasNoRecommendation, errors)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L43 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L57 [function] `private static bool VerifyCombatFeedbackAndSummary()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L108 [function] `private static bool VerifySummaryHasNoRecommendation()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L135 [type] `private sealed class CombatReportScenarioWorld : IDisposable` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L139 [function] `public CombatReportScenarioWorld()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L153 [function] `public void StartInvasion()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L158 [function] `new InvasionThreatFactors(3f, 2f, 1f, 0f)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L165 [function] `public void TriggerDefense(DefenseActivationReport report)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L170 [function] `public void TriggerFacilityDamaged(BuildableObject facility)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L175 [function] `public void TriggerFinalCombat()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L180 [function] `public void Resolve(bool defended, float residualRisk)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L203 [function] `public BuildableObject CreateFacility(string buildingName, DefenseAttackConcept concept)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L234 [function] `public void Dispose()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L245 [function] `private CharacterActor CreateCharacter(string name)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L255 [type] `private sealed class CountingCombatReportListener : UtilEventListener<InvasionCombatReportReadyEvent>, IDisposable` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L260 [function] `public CountingCombatReportListener()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L265 [function] `public void OnTriggerEvent(InvasionCombatReportReadyEvent eventType)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L271 [function] `public void Dispose()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L277 [type] `private sealed class CountingCombatFeedbackListener : UtilEventListener<InvasionCombatFeedbackEvent>, IDisposable` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L281 [function] `public CountingCombatFeedbackListener()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L286 [function] `public void OnTriggerEvent(InvasionCombatFeedbackEvent eventType)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L294 [function] `public void Dispose()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L300 [type] `private sealed class CountingEventAlertRequestListener : UtilEventListener<EventAlertRequestedEvent>, IDisposable` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L304 [function] `public CountingEventAlertRequestListener()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L309 [function] `public void OnTriggerEvent(EventAlertRequestedEvent eventType)` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L314 [function] `public void Dispose()` [EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Invasion/Editor/InvasionIntruderDebugScenarios.cs

- L9 [type] `public static class InvasionIntruderDebugScenarios` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L12 [function] `public static void RunFromMenu()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L21 [function] `public static bool RunAll(bool logSuccess)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L25 [function] `RunScenario("??⑤㈇?뚧납????????, VerifyIntruderAsset, errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L26 [function] `RunScenario("?????嚥싲갭횧?蹂좎쒜??꿔꺂??袁ㅻ븶?猷뱀떻???⑤슢????, VerifyExplorationBias, errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L27 [function] `RunScenario("??嶺?筌????? ??⑤슢?????꿔꺂??袁ㅻ븶?猷뱀떻?, VerifyFacilityDamage, errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L28 [function] `RunScenario("?꿔꺂????쭍?瑜귣젺???????筌뚮?????????띻샴癲?, VerifyFinalCombatEndsRun, errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L48 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L62 [function] `private static bool VerifyIntruderAsset()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L73 [function] `private static bool VerifyExplorationBias()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L78 [function] `AddHallway(grid, new Vector2Int(x, 0))` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L106 [function] `private static bool VerifyFacilityDamage()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L112 [function] `SetPrivateField(runtime, "intruderActor", CharacterActor.From(intruder))` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L122 [function] `private static bool VerifyFinalCombatEndsRun()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L145 [function] `SetPrivateField(runtime, "intruderActor", CharacterActor.From(intruder))` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L157 [function] `private static CharacterSO LoadIntruder()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L163 [function] `private static void AddHallway(Grid grid, Vector2Int position)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L166 [function] `new TestHallwayOccupant()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L172 [function] `private static void SetPrivateField(object target, string fieldName, object value)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L178 [type] `private sealed class IntruderScenarioWorld : IDisposable` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L194 [function] `public IntruderScenarioWorld(int width)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L202 [function] `AddHallway(Grid, new Vector2Int(x, 0))` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L215 [function] `public void Track(GameObject obj)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L223 [function] `public BuildableObject Place(string assetName, Vector2Int position)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L255 [function] `public CharacterActor CreateIntruder(Vector2Int position)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L270 [function] `public void Dispose()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L281 [type] `private sealed class TestHallwayOccupant : IGridOccupant` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L289 [type] `private sealed class CountingFacilityDamageListener : UtilEventListener<InvasionFacilityDamagedEvent>, IDisposable` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L294 [function] `public CountingFacilityDamageListener()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L299 [function] `public void OnTriggerEvent(InvasionFacilityDamagedEvent eventType)` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]
- L305 [function] `public void Dispose()` [SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Invasion/Editor/InvasionThreatDebugScenarios.cs

- L6 [type] `public static class InvasionThreatDebugScenarios` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L9 [function] `public static void RunFromMenu()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L18 [function] `public static bool RunAll(bool logSuccess)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L22 [function] `RunScenario("????꾣뤃?琉??????쇨덧?????됰Ŋ?????影??낟??, VerifyThreatRiseFactors, errors)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L23 [function] `RunScenario("?嚥▲굧???????嚥?癲? ?????, VerifyWarningAlert, errors)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L24 [function] `RunScenario("??⑤㈇?뚧납??????썹땟?雅??꿔꺂????????嚥??, VerifyCandidateDelay, errors)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L25 [function] `RunScenario("??⑤㈇?뚧납????嶺뚮??ｆ뤃????潁??용끏???? ???繹먮냱踰???????, VerifyResetAndSafety, errors)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L45 [function] `private static void RunScenario(string name, System.Func<bool> scenario, List<string> errors)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L52 [function] `private static bool VerifyThreatRiseFactors()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L75 [function] `private static bool VerifyWarningAlert()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L78 [function] `ConfigureFastSettings(runtime)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L93 [function] `CleanupRuntimeUi()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L97 [function] `private static bool VerifyCandidateDelay()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L100 [function] `ConfigureFastSettings(runtime)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L114 [function] `CleanupRuntimeUi()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L118 [function] `private static bool VerifyResetAndSafety()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L121 [function] `ConfigureFastSettings(runtime)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L136 [function] `CleanupRuntimeUi()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L140 [function] `private static InvasionThreatRuntime CreateRuntime(out GameObject root)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L146 [function] `private static void ConfigureFastSettings(InvasionThreatRuntime runtime)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L160 [function] `private static void CleanupRuntimeUi()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L181 [type] `private sealed class CountingThreatWarningListener : UtilEventListener<InvasionThreatWarningEvent>, System.IDisposable` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L185 [function] `public CountingThreatWarningListener()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L190 [function] `public void OnTriggerEvent(InvasionThreatWarningEvent eventType)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L195 [function] `public void Dispose()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L201 [type] `private sealed class CountingInvasionCandidateListener : UtilEventListener<InvasionCandidateEvent>, System.IDisposable` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L205 [function] `public CountingInvasionCandidateListener()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L210 [function] `public void OnTriggerEvent(InvasionCandidateEvent eventType)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L215 [function] `public void Dispose()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L221 [type] `private sealed class CountingEventAlertRequestListener : UtilEventListener<EventAlertRequestedEvent>, System.IDisposable` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L230 [function] `public CountingEventAlertRequestListener()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L235 [function] `public void OnTriggerEvent(EventAlertRequestedEvent eventType)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L243 [function] `public void Dispose()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]


## Assets\Scripts\Invasion

### Assets/Scripts/Invasion/InvasionCombatReportSystem.cs

- L6 [type] `public struct InvasionCombatFeedbackEvent` [EventBus]
- L11 [function] `public InvasionCombatFeedbackEvent(string message, DefenseActivationReport defenseReport)` [EventBus]
- L19 [function] `public static void Trigger(string message, DefenseActivationReport defenseReport)` [EventBus]
- L27 [type] `public struct InvasionCombatReportReadyEvent` [EventBus]
- L31 [function] `public InvasionCombatReportReadyEvent(InvasionCombatReport report)` [EventBus]
- L38 [function] `public static void Trigger(InvasionCombatReport report)` [EventBus]
- L45 [type] `public class InvasionCombatReport` [EventBus]
- L53 [function] `public InvasionCombatReport(InvasionThreatSnapshot snapshot)` [EventBus]
- L88 [function] `public void SetIntruder(CharacterActor intruder)` [EventBus]
- L93 [function] `public void RecordDefenseActivation(DefenseActivationReport report)` [EventBus]
- L111 [function] `AddObservation(InvasionCombatReportFormatter.FormatObservation(tag))` [EventBus]
- L121 [function] `public void RecordFacilityDamage(BuildableObject facility)` [EventBus]
- L131 [function] `public void RecordFinalCombat(CharacterActor owner)` [EventBus]
- L137 [function] `public void Resolve(bool defended, float residualRisk)` [EventBus]
- L144 [function] `public string ToDetailText()` [EventBus]
- L152 [function] `AddContributionLine(lines, "??醫딆쓧????꿔꺂???? ???筌뤿굝琉??嚥싳쉶瑗ц짆堉샕 ??嶺?筌?, TopDamageContribution, true)` [EventBus]
- L153 [function] `AddContributionLine(lines, "??醫딆쓧???????怨대뜲 ?꿔꺂??????????ル물????嶺?筌?, TopDelayContribution, false)` [EventBus]
- L183 [function] `private DefenseContribution FindOrCreateContribution(DefenseFacility facility)` [EventBus]
- L196 [function] `private void AddObservation(string observation)` [EventBus]
- L204 [function] `private string GetDecisiveDefenseText()` [EventBus]
- L227 [function] `private string FormatFacilities(IEnumerable<BuildableObject> facilities)` [EventBus]
- L239 [function] `private string FormatObservations()` [EventBus]
- L263 [type] `public class DefenseContribution` [EventBus]
- L267 [function] `public DefenseContribution(DefenseFacility facility)` [EventBus]
- L279 [function] `public void Add(DefenseActivationReport report)` [EventBus]
- L300 [type] `public static class InvasionCombatReportFormatter` [EventBus]
- L302 [function] `public static string FormatActivation(DefenseActivationReport report)` [EventBus]
- L332 [function] `public static string FormatObservation(string effectTag)` [EventBus]
- L378 [function] `public static string FormatSynergy(DefenseActivationReport report)` [EventBus]
- L420 [function] `public static string GetBuildingName(BuildableObject building)` [EventBus]

### Assets/Scripts/Invasion/InvasionCombatReportRuntime.cs

- L3 [type] `public class InvasionCombatReportRuntime : MonoBehaviour,` [EventBus]
- L19 [function] `public void OnTriggerEvent(InvasionStartedEvent eventType)` [EventBus]
- L25 [function] `public void OnTriggerEvent(InvasionSpawnedEvent eventType)` [EventBus]
- L31 [function] `public void OnTriggerEvent(DefenseFacilityTriggeredEvent eventType)` [EventBus]
- L48 [function] `public void OnTriggerEvent(InvasionFacilityDamagedEvent eventType)` [EventBus]
- L58 [function] `public void OnTriggerEvent(InvasionFinalCombatStartedEvent eventType)` [EventBus]
- L68 [function] `public void OnTriggerEvent(InvasionResolvedEvent eventType)` [EventBus]
- L83 [function] `private void OnEnable()` [EventBus]
- L93 [function] `private void OnDisable()` [EventBus]
- L103 [function] `private InvasionCombatReport EnsureReport(InvasionThreatSnapshot snapshot)` [EventBus]
- L109 [function] `private string ClampLine(string message)` [EventBus]

### Assets/Scripts/Invasion/InvasionDefenseSummaryQuery.cs

- L4 [type] `public readonly struct InvasionDefenseSummary`
- L39 [type] `public interface IInvasionDefenseSummaryService` [DependencyInjection]
- L44 [type] `public interface IInvasionDefenseSummaryRuntimeSource` [DependencyInjection]
- L52 [type] `public sealed class InvasionDefenseSummaryRuntimeSource : IInvasionDefenseSummaryRuntimeSource` [DependencyInjection]
- L56 [function] `public InvasionDefenseSummaryRuntimeSource(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L67 [type] `public sealed class InvasionDefenseSummaryService : IInvasionDefenseSummaryService` [DependencyInjection]
- L71 [function] `public InvasionDefenseSummaryService(IInvasionDefenseSummaryRuntimeSource runtimeSource)` [DependencyInjection]
- L76 [function] `public InvasionDefenseSummary Capture()` [DependencyInjection]
- L96 [function] `private static void CountFacilities(IReadOnlyList<BuildableObject> buildings, out int defenseFacilities, out int damagedFacilities)`

### Assets/Scripts/Invasion/InvasionIntruderSystem.cs

- L8 [type] `public class InvasionDirectorRuntime : MonoBehaviour, UtilEventListener<InvasionCandidateEvent>` [DependencyInjection, EventBus, SceneMutation]
- L22 [function] `public void Construct(` [DependencyInjection]
- L35 [function] `public void OnTriggerEvent(InvasionCandidateEvent eventType)` [DependencyInjection, EventBus]
- L40 [function] `public bool TrySpawnIntruder(InvasionThreatSnapshot snapshot, out CharacterActor intruder)` [DependencyInjection, EventBus, SceneMutation]
- L75 [function] `private void OnEnable()` [EventBus]
- L80 [function] `private void OnDisable()` [EventBus]
- L85 [function] `private CharacterSO ResolveIntruderData()` [DependencyInjection]
- L91 [function] `private IInvasionIntruderDataProvider ResolveIntruderDataProvider()` [DependencyInjection]
- L97 [function] `private IInvasionIntruderContext ResolveInvasionContext()` [DependencyInjection]
- L103 [function] `private IInvasionIntruderFactory ResolveIntruderFactory()` [DependencyInjection]
- L109 [function] `private void OnIntruderFinished(InvasionIntruderRuntime runtime)`
- L121 [type] `public class InvasionIntruderRuntime : MonoBehaviour` [DependencyInjection, EventBus, SceneMutation]
- L138 [function] `private void Awake()`
- L144 [function] `public void Initialize(IInvasionIntruderContext invasionContext)` [DependencyInjection]
- L150 [function] `public void Begin(` [DependencyInjection, SceneMutation]
- L176 [function] `public Queue<GridMoveStep> CreateNextPath(Grid grid, Vector2Int ownerPosition, out bool direct)`
- L182 [function] `public bool TryDamageNearbyFacility(Grid grid)` [EventBus, SceneMutation]
- L201 [function] `public void ApplyFinalCombat(CharacterActor owner)` [EventBus]
- L215 [function] `public void ResolveSuppressedBy(CharacterActor defender)` [EventBus]
- L232 [function] `private IEnumerator Run(Vector3 entryDoorPosition, Vector2Int entryGridPosition)` [DependencyInjection, EventBus, SceneMutation]
- L295 [function] `private IEnumerator MovePathWithDefense(Grid grid, Queue<GridMoveStep> path)` [SceneMutation]
- L330 [function] `private void TickDefenseStatuses(float deltaSeconds)`
- L340 [function] `private IEnumerator FinalCombat(CharacterActor owner)` [EventBus]
- L352 [function] `private void ResolveIntruderDefeated()` [EventBus]
- L363 [function] `private void Finish()` [SceneMutation]
- L375 [function] `private void RequireRuntimeComponents()` [DependencyInjection]
- L395 [function] `private IInvasionIntruderContext ResolveInvasionContext()` [DependencyInjection]

### Assets/Scripts/Invasion/InvasionIntruderModel.cs

- L5 [type] `public class InvasionIntruderSettings`
- L14 [type] `public enum InvasionIntruderState`

### Assets/Scripts/Invasion/InvasionIntruderEvents.cs

- L1 [type] `public struct InvasionSpawnedEvent` [EventBus]
- L6 [function] `public InvasionSpawnedEvent(CharacterActor intruder, InvasionThreatSnapshot threatSnapshot)` [EventBus]
- L14 [function] `public static void Trigger(CharacterActor intruder, InvasionThreatSnapshot threatSnapshot)` [EventBus]
- L22 [type] `public struct InvasionFacilityDamagedEvent` [EventBus]
- L27 [function] `public InvasionFacilityDamagedEvent(CharacterActor intruder, BuildableObject facility)` [EventBus]
- L35 [function] `public static void Trigger(CharacterActor intruder, BuildableObject facility)` [EventBus]
- L43 [type] `public struct InvasionFinalCombatStartedEvent` [EventBus]
- L48 [function] `public InvasionFinalCombatStartedEvent(CharacterActor intruder, CharacterActor owner)` [EventBus]
- L56 [function] `public static void Trigger(CharacterActor intruder, CharacterActor owner)` [EventBus]

### Assets/Scripts/Invasion/InvasionIntruderPlanner.cs

- L5 [type] `public static class InvasionIntruderPlanner`
- L7 [function] `public static float CalculateFocus(float elapsedSeconds, float secondsToFullFocus)`
- L12 [function] `public static Queue<GridMoveStep> GetNextPath(Grid grid, Vector2Int start, Vector2Int ownerPosition, float focus, out bool directPath)`
- L45 [function] `public static Vector2Int SelectExploreTarget(Grid grid, GridPathSearchResult searchResult, Vector2Int ownerPosition, float focus)`
- L91 [function] `public static bool IsAtOwner(Grid grid, CharacterActor intruder, CharacterActor owner)`
- L101 [function] `private static int Manhattan(Vector2Int a, Vector2Int b)`

### Assets/Scripts/Invasion/InvasionIntruderDataResolver.cs

- L1 [type] `public interface IInvasionIntruderDataProvider` [DependencyInjection]

### Assets/Scripts/Invasion/InvasionIntruderContext.cs

- L4 [type] `public readonly struct InvasionIntruderEntry` [DependencyInjection]
- L6 [function] `public InvasionIntruderEntry(Vector2Int gridPosition, Vector3 outsidePosition, Vector3 doorPosition)` [DependencyInjection]
- L21 [type] `public interface IInvasionIntruderContext` [DependencyInjection]
- L29 [type] `public sealed class InvasionIntruderContext : IInvasionIntruderContext` [DependencyInjection]
- L37 [function] `public InvasionIntruderContext(IDungeonSceneComponentQuery sceneQuery, IGridSystemProvider gridSystemProvider, IRunVariableRuntimeReader runVariableReader)` [DependencyInjection]
- L50 [function] `public bool TryGetGrid(out Grid grid)` [DependencyInjection]
- L64 [function] `public bool TryGetOwner(out CharacterActor owner)` [DependencyInjection]
- L71 [function] `public bool TryResolveEntry(out InvasionIntruderEntry entry)` [DependencyInjection]
- L78 [function] `public InvasionIntruderSettings ApplyRunVariables(InvasionIntruderSettings source)` [DependencyInjection]

### Assets/Scripts/Invasion/InvasionIntruderEntryResolver.cs

- L3 [type] `public static class InvasionIntruderEntryResolver`
- L5 [function] `public static bool TryResolve(CharacterSpawner spawner, Grid grid, out InvasionIntruderEntry entry)`

### Assets/Scripts/Invasion/InvasionIntruderFactory.cs

- L4 [type] `public interface IInvasionIntruderFactory` [DependencyInjection]
- L6 [function] `InvasionIntruderRuntime Create(GameObject intruderPrefab, Vector3 position)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L7 [function] `InvasionIntruderRuntime EnsureRuntime(GameObject intruderObject)` [DependencyInjection]
- L10 [type] `public sealed class InvasionIntruderRuntimeFactory : IInvasionIntruderFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L15 [function] `public InvasionIntruderRuntimeFactory(ICharacterVisualRootFactory visualRootFactory)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L21 [function] `public InvasionIntruderRuntime Create(GameObject intruderPrefab, Vector3 position)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L31 [function] `public InvasionIntruderRuntime EnsureRuntime(GameObject intruderObject)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Invasion/InvasionFacilityDamageResolver.cs

- L4 [type] `public static class InvasionFacilityDamageResolver`
- L6 [function] `public static bool TryFindDamageTarget(Grid grid, Vector2Int current, out BuildableObject target)`
- L41 [function] `private static bool IsDamageableFacility(BuildableObject building)`

### Assets/Scripts/Invasion/InvasionThreatSystem.cs

- L5 [type] `public enum InvasionThreatDifficulty`
- L12 [type] `public enum InvasionThreatStage`
- L21 [type] `public class InvasionThreatSettings`
- L44 [function] `public float GetDifficultyMultiplier()`
- L54 [function] `public float GetCandidateDelay()`
- L69 [function] `public InvasionThreatFactors(float dungeonValue, float reputation, float time, float risk)`
- L101 [type] `public struct InvasionThreatWarningEvent` [EventBus]
- L105 [function] `public InvasionThreatWarningEvent(InvasionThreatSnapshot snapshot)` [EventBus]
- L112 [function] `public static void Trigger(InvasionThreatSnapshot snapshot)` [EventBus]
- L119 [type] `public struct InvasionCandidateEvent` [EventBus]
- L123 [function] `public InvasionCandidateEvent(InvasionThreatSnapshot snapshot)` [EventBus]
- L130 [function] `public static void Trigger(InvasionThreatSnapshot snapshot)` [EventBus]
- L137 [type] `public struct InvasionStartedEvent` [EventBus]
- L141 [function] `public InvasionStartedEvent(InvasionThreatSnapshot snapshot)` [EventBus]
- L148 [function] `public static void Trigger(InvasionThreatSnapshot snapshot)` [EventBus]
- L155 [type] `public struct InvasionResolvedEvent` [EventBus]
- L160 [function] `public InvasionResolvedEvent(bool defended, float residualRisk)` [EventBus]
- L168 [function] `public static void Trigger(bool defended, float residualRisk = 0f)` [EventBus]
- L178 [function] `public static float CalculateRisePerSecond(InvasionThreatSettings settings, InvasionThreatFactors factors)`
- L183 [function] `public static float CalculateRisePerSecond(InvasionThreatSettings settings, InvasionThreatFactors factors, float runMultiplier)`
- L199 [function] `public static string BuildWarningDetail(InvasionThreatSnapshot snapshot)`
- L231 [function] `public static string BuildCandidateDetail(InvasionThreatSnapshot snapshot)`

### Assets/Scripts/Invasion/InvasionThreatRuntime.cs

- L5 [type] `public class InvasionThreatRuntime : MonoBehaviour,` [DependencyInjection, EventBus]
- L32 [function] `public void Construct(IInvasionThreatWorldSampler worldSampler, IRunVariableRuntimeReader runVariableReader, IMetaProgressionRuntimeReader metaProgressionReader)` [DependencyInjection]
- L46 [function] `private void Update()` [DependencyInjection, EventBus]
- L51 [function] `public void Tick(float deltaTime)` [DependencyInjection, EventBus]
- L92 [function] `public void AddThreat(float amount)` [DependencyInjection, EventBus]
- L100 [function] `public void OnTriggerEvent(InvasionStartedEvent eventType)` [EventBus]
- L105 [function] `public void OnTriggerEvent(InvasionResolvedEvent eventType)` [EventBus]
- L114 [function] `public void OnTriggerEvent(OperatingDayStartedEvent eventType)` [EventBus]
- L124 [function] `private void OnEnable()` [EventBus]
- L131 [function] `private void OnDisable()` [EventBus]
- L138 [function] `private void TryRaiseWarning()` [DependencyInjection, EventBus]
- L164 [function] `private void TickCandidateDelay(float deltaTime)` [DependencyInjection, EventBus]
- L200 [function] `private void ResetAfterInvasion()`
- L212 [function] `private InvasionThreatSnapshot BuildSnapshot()`
- L222 [function] `private InvasionThreatStage ResolveStage()` [DependencyInjection]
- L246 [function] `private InvasionThreatFactors SampleWorldFactors()` [DependencyInjection]
- L256 [function] `private float GetWarningThresholdMultiplier()` [DependencyInjection]
- L262 [function] `private IRunVariableRuntimeReader RequireRunVariableReader()` [DependencyInjection]
- L272 [function] `private IMetaProgressionRuntimeReader RequireMetaProgressionReader()` [DependencyInjection]

### Assets/Scripts/Invasion/InvasionThreatWorldSampler.cs

- L4 [type] `public interface IInvasionThreatWorldSampler` [DependencyInjection]
- L9 [type] `public sealed class InvasionThreatWorldSampler : IInvasionThreatWorldSampler` [DependencyInjection]
- L13 [function] `public InvasionThreatWorldSampler(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L19 [function] `public InvasionThreatFactors Sample(float secondsSinceLastInvasion)` [DependencyInjection]
- L31 [function] `private static float CalculateDungeonValue(IEnumerable<BuildableObject> buildings)` [DependencyInjection]
- L60 [function] `private static float CalculateReputation(IEnumerable<CharacterActor> characters)` [DependencyInjection]
- L93 [function] `private static float CalculateRisk(IEnumerable<BuildableObject> buildings)` [DependencyInjection]


## Assets\Scripts\Meta\Editor

### Assets/Scripts/Meta/Editor/MetaProgressionDebugScenarios.cs

- L8 [type] `public static class MetaProgressionDebugScenarios` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L11 [function] `public static void RunFromMenu()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L20 [function] `public static bool RunAll(bool logSuccess)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L24 [function] `RunScenario("??????傭????嚥▲굧?????癲ル슢캉???, VerifyOwnerDeathCreatesRunResult, errors)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L25 [function] `RunScenario("???꾩렯?????⑤슢???貫糾????????얠? ??影??낟?????????, VerifySurvivalRewardDominatesDiscoveryOnly, errors)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L26 [function] `RunScenario("????ㅳ늾?온 ?꿔꺂???????醫딆┫?????壤굿??뗫툞", VerifyOperationKnowledgeUpgrades, errors)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L27 [function] `RunScenario("????고뀘????⑤슢??????醫딆┫?????壤굿??뗫툞", VerifyRecipePreservation, errors)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L28 [function] `RunScenario("???????꾩렯?????醫딆┫?????壤굿??뗫툞", VerifyOwnerSurvivalUpgrades, errors)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L48 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L62 [function] `private static bool VerifyOwnerDeathCreatesRunResult()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L74 [function] `new InvasionThreatFactors(2f, 2f, 2f, 1f)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L97 [function] `private static bool VerifySurvivalRewardDominatesDiscoveryOnly()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L122 [function] `private static bool VerifyOperationKnowledgeUpgrades()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L138 [function] `new FacilityShopUnlockState())` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L150 [function] `private static bool VerifyRecipePreservation()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L175 [function] `private static bool VerifyOwnerSurvivalUpgrades()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L196 [function] `private static CharacterActor CreateOwner()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L217 [function] `private static BuildableObject CreateFacility(int id, string name)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L226 [function] `private static BuildingSO CreateBuilding(int id, string name, bool defense)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L245 [type] `private sealed class ScenarioRuntime : IDisposable` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L251 [function] `public ScenarioRuntime()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L259 [function] `public void Dispose()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L276 [type] `private sealed class CountingRunResultReadyListener :` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L282 [function] `public CountingRunResultReadyListener()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L287 [function] `public void OnTriggerEvent(RunResultReadyEvent eventType)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L295 [function] `public void Dispose()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L301 [type] `private sealed class CountingEventAlertRequestListener :` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L309 [function] `public CountingEventAlertRequestListener()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L314 [function] `public void OnTriggerEvent(EventAlertRequestedEvent eventType)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L322 [function] `public void Dispose()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]


## Assets\Scripts\Meta

### Assets/Scripts/Meta/MetaProgressionCalculator.cs

- L3 [type] `public static class MetaProgressionCalculator`
- L5 [function] `public static int CalculateLegacyCurrency(RunResultSnapshot result)`
- L25 [function] `private static int GetThreatStageScore(InvasionThreatStage stage)`

### Assets/Scripts/Meta/MetaProgressionCatalog.cs

- L4 [type] `public static class MetaProgressionCatalog`
- L6 [function] `private static readonly Dictionary<MetaUpgradeId, MetaUpgradeDefinition> definitions = BuildDefinitions();`
- L10 [function] `public static MetaUpgradeDefinition Get(MetaUpgradeId id)`
- L15 [function] `private static Dictionary<MetaUpgradeId, MetaUpgradeDefinition> BuildDefinitions()`

### Assets/Scripts/Meta/MetaProgressionEvents.cs

- L1 [type] `public struct RunResultReadyEvent` [EventBus]
- L5 [function] `public RunResultReadyEvent(RunResultSnapshot result)` [EventBus]
- L12 [function] `public static void Trigger(RunResultSnapshot result)` [EventBus]

### Assets/Scripts/Meta/MetaProgressionModel.cs

- L5 [type] `public enum MetaProgressionBranch`
- L12 [type] `public enum MetaUpgradeId`
- L23 [type] `public sealed class MetaUpgradeDefinition`
- L33 [type] `public sealed class MetaProgressionState`
- L35 [function] `private readonly Dictionary<MetaUpgradeId, int> upgradeLevels = new Dictionary<MetaUpgradeId, int>();`
- L36 [function] `private readonly HashSet<string> preservedRecipeIds = new HashSet<string>();`
- L40 [function] `public int AvailableCurrency => Mathf.Max(0, LifetimeEarnedCurrency - SpentCurrency);`
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

### Assets/Scripts/Meta/MetaProgressionSystem.cs

- L7 [type] `public class MetaProgressionRuntime :` [DependencyInjection, EventBus]
- L22 [function] `private readonly MetaProgressionState state = new MetaProgressionState();` [DependencyInjection, EventBus]
- L23 [function] `private readonly MetaRunProgressTracker runProgress = new MetaRunProgressTracker();` [DependencyInjection, EventBus]
- L33 [function] `public void Construct(IMetaRunResultBuilder runResultBuilder, IRunResultPanelService runResultPanelService)` [DependencyInjection]
- L43 [function] `public void SetShowRunResultPanel(bool value)` [DependencyInjection, EventBus]
- L48 [function] `private void Awake()` [DependencyInjection, EventBus]
- L53 [function] `public void StartNewRun()` [DependencyInjection, EventBus]
- L60 [function] `public bool TryPurchaseUpgrade(MetaUpgradeId id, out string message)` [DependencyInjection, EventBus]
- L71 [function] `public int GetStartingFacilityCandidateBonus()` [DependencyInjection, EventBus]
- L76 [function] `public int GetStartingOwnerTraitCandidateBonus()` [DependencyInjection, EventBus]
- L81 [function] `public float GetOwnerMaxHealthMultiplier()` [DependencyInjection, EventBus]
- L86 [function] `public float GetInvasionWarningThresholdMultiplier()` [DependencyInjection, EventBus]
- L91 [function] `public bool IsRecipePreserved(string recipeId)` [DependencyInjection, EventBus]
- L97 [function] `public IReadOnlyCollection<int> GetExpandedBasicPurchaseBuildingIds(IEnumerable<BuildingSO> buildings)` [DependencyInjection, EventBus]
- L117 [function] `public void RecordOffenseSuccess()` [DependencyInjection, EventBus]
- L122 [function] `public void OnTriggerEvent(OperatingDayStartedEvent eventType)` [DependencyInjection, EventBus]
- L132 [function] `public void OnTriggerEvent(OperatingDayReportEvent eventType)` [DependencyInjection, EventBus]
- L137 [function] `public void OnTriggerEvent(InvasionThreatWarningEvent eventType)` [DependencyInjection, EventBus]
- L142 [function] `public void OnTriggerEvent(InvasionCandidateEvent eventType)` [DependencyInjection, EventBus]
- L147 [function] `public void OnTriggerEvent(InvasionStartedEvent eventType)` [DependencyInjection, EventBus]
- L152 [function] `public void OnTriggerEvent(InvasionResolvedEvent eventType)` [DependencyInjection, EventBus]
- L157 [function] `public void OnTriggerEvent(FacilityVisitEvent eventType)` [DependencyInjection, EventBus]
- L162 [function] `public void OnTriggerEvent(BlueprintResearchCompletedEvent eventType)` [DependencyInjection, EventBus]
- L167 [function] `public void OnTriggerEvent(FacilitySynthesisCompletedEvent eventType)` [DependencyInjection, EventBus]
- L172 [function] `public void OnTriggerEvent(OwnerRunEndedEvent eventType)` [DependencyInjection, EventBus]
- L177 [function] `public RunResultSnapshot EndRun(CharacterActor owner, string reason)` [DependencyInjection, EventBus]
- L201 [function] `private IMetaRunResultBuilder ResolveRunResultBuilder()` [DependencyInjection]
- L207 [function] `private IRunResultPanelService ResolveRunResultPanelService()` [DependencyInjection]
- L213 [function] `private void PreserveRunRecipes()` [DependencyInjection, EventBus]
- L219 [function] `private void OnEnable()` [EventBus]
- L233 [function] `private void OnDisable()` [EventBus]

### Assets/Scripts/Meta/MetaRunProgressTracker.cs

- L5 [type] `public sealed class MetaRunProgressTracker`
- L7 [function] `private readonly HashSet<int> discoveredFacilityIds = new HashSet<int>();`
- L8 [function] `private readonly HashSet<string> unlockedRecipeIds = new HashSet<string>();`
- L18 [function] `public IReadOnlyCollection<string> UnlockedRecipeIds => unlockedRecipeIds;`
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

### Assets/Scripts/Meta/MetaProgressionRunResultServices.cs

- L3 [type] `public readonly struct MetaRunResultBuildContext` [DependencyInjection]
- L5 [function] `public MetaRunResultBuildContext(CharacterActor owner, string reason, float runStartTime, int currentDay, int settlementCount, int defendedInvasionCount, InvasionThreatStage maxThreatStage, float finalInvasionThreat, int discoveredFacilityCount, int unlockedRecipeCount, int offenseSuccessCount)` [DependencyInjection]
- L44 [type] `public interface IMetaRunResultBuilder` [DependencyInjection]
- L49 [type] `public sealed class MetaRunResultBuilder : IMetaRunResultBuilder` [DependencyInjection]
- L53 [function] `public MetaRunResultBuilder(IInvasionThreatRuntimeProvider threatRuntimeProvider)` [DependencyInjection]
- L59 [function] `public RunResultSnapshot Build(MetaRunResultBuildContext context)` [DependencyInjection]
- L94 [type] `public interface IRunResultPanelService` [DependencyInjection]
- L99 [type] `public interface IRunResultPanelFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L101 [function] `RunResultPanel CreateDefaultPanel()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L104 [type] `public sealed class RunResultPanelFactory : IRunResultPanelFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L109 [function] `public RunResultPanelFactory(ITmpKoreanFontService tmpKoreanFontService, IObjectResolver objectResolver)` [DependencyInjection]
- L119 [function] `public RunResultPanel CreateDefaultPanel()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L162 [type] `public sealed class RunResultPanelService : IRunResultPanelService` [DependencyInjection]
- L167 [function] `public RunResultPanelService(IDungeonSceneComponentQuery sceneQuery, IRunResultPanelFactory panelFactory)` [DependencyInjection]
- L177 [function] `public RunResultPanel Show(RunResultSnapshot result)` [DependencyInjection]

### Assets/Scripts/Meta/RunResultPanel.cs

- L5 [type] `public class RunResultPanel : MonoBehaviour` [RuntimeObjectCreation, SceneMutation]
- L9 [function] `public void Render(RunResultSnapshot result)` [SceneMutation]
- L19 [function] `public void Hide()` [SceneMutation]
- L24 [function] `private void EnsureView()` [SceneMutation]

### Assets/Scripts/Offense/Editor/OffenseExpeditionDebugScenarios.cs

- L8 [type] `public static class OffenseExpeditionDebugScenarios` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L11 [function] `public static void RunFromMenu()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L20 [function] `public static bool RunAll(bool logSuccess)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L24 [function] `RunScenario("???????醫딆쓧????癲ル슢????????꾣뤃??, VerifyAvailableMemberFilter, errors)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L25 [function] `RunScenario("????????Β?ы닍????????볥궚??????ㅳ늾?온 ??嶺뚮????, VerifyStartExpeditionRemovesMembersFromDungeon, errors)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L26 [function] `RunScenario("????썹땟???癲ル슢???????낇뀘????????곌숯", VerifyRequiredMemberValidation, errors)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L27 [function] `RunScenario("???嶺???????嚥싲갭횧?蹂좎쒜??嚥▲굧????, VerifySuccessfulAutoResolve, errors)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L28 [function] `RunScenario("??????????곌숯 ???낇뀘?????傭??꿔꺂??節뉖き??, VerifyFailureCanKillMember, errors)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L29 [function] `RunScenario("??????癲ル슢????????됰Þ?????꾩룆???, VerifyPanelCreation, errors)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L49 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L63 [function] `private static bool VerifyAvailableMemberFilter()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L76 [function] `private static bool VerifyStartExpeditionRemovesMembersFromDungeon()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L96 [function] `private static bool VerifyRequiredMemberValidation()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L111 [function] `private static bool VerifySuccessfulAutoResolve()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L141 [function] `private static bool VerifyFailureCanKillMember()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L146 [function] `CreateTarget("deadly_test", "????꾣뤃??????????????, 1f, 240f, 999f, 1)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L168 [function] `private static bool VerifyPanelCreation()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L218 [type] `private sealed class ScenarioRuntime : IDisposable` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L225 [function] `public ScenarioRuntime()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L265 [function] `public void Dispose()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L276 [type] `private sealed class WorldMapFixture : IDisposable` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L282 [function] `public WorldMapFixture()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L289 [function] `public void Dispose()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L295 [type] `private sealed class ExpeditionFixture : IDisposable` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L301 [function] `public ExpeditionFixture()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L307 [function] `public void Dispose()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L313 [type] `private sealed class CountingCompletionListener :` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L319 [function] `public CountingCompletionListener()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L324 [function] `public void OnTriggerEvent(OffenseExpeditionCompletedEvent eventType)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L332 [function] `public void Dispose()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Offense/Editor/OffenseRewardDebugScenarios.cs

- L8 [type] `public static class OffenseRewardDebugScenarios` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L11 [function] `public static void RunFromMenu()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L20 [function] `public static bool RunAll(bool logSuccess)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L24 [function] `RunScenario("??⑤슢???貫糾???嶺뚮쮳?놂폇????????????븐뻤???꿔꺂?????, VerifyMoneyStockAndStateRewards, errors)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L25 [function] `RunScenario("??? ??嶺?筌??????고뀘????꿔꺂?????, VerifyRareFacilityAndBlueprintRewards, errors)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L26 [function] `RunScenario("?????????썹땟??????⑤슢???貫糾??꿔꺂?????????쇰뮚??, VerifyExpeditionCompletionGrantsRewards, errors)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L46 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L60 [function] `private static bool VerifyMoneyStockAndStateRewards()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L66 [function] `Reward(OffenseRewardCategory.Money, "???嚥싲갭큔筌띿떔??, 80)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L67 [function] `Reward(OffenseRewardCategory.Stock, "????????, 40)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L68 [function] `Reward(OffenseRewardCategory.FactionWeakening, "?癲ル슢?ο㎖???癲ル슢?????????, 2)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L69 [function] `Reward(OffenseRewardCategory.RecruitCandidate, "?꿔꺂?????????썹땟?雅?, 1)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L70 [function] `Reward(OffenseRewardCategory.Prisoner, "??濚?, 1)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L71 [function] `Reward(OffenseRewardCategory.Prisoner, "???????꿔꺂?????녿쫯??, 1)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L86 [function] `private static bool VerifyRareFacilityAndBlueprintRewards()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L92 [function] `Reward(OffenseRewardCategory.RareFacility, "??? ??嶺?筌?, 1)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L93 [function] `Reward(OffenseRewardCategory.Blueprint, "??????????고뀘???, 1)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L104 [function] `private static bool VerifyExpeditionCompletionGrantsRewards()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L130 [function] `private static OffenseRewardPreview Reward(OffenseRewardCategory category, string label, int amount)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L140 [function] `private static IOffenseRewardGrantService CreateGrantService()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L148 [function] `private static GameData CreateGameData(int holdingMoney)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L156 [type] `private sealed class ScenarioContext : IDisposable` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L158 [function] `public ScenarioContext(int holdingMoney)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L173 [function] `public OffenseRewardContext CreateRewardContext(OffenseTargetDefinition target = null)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L186 [function] `public void Dispose()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L195 [type] `private sealed class TestWarehouse : IWarehouseFacility` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L197 [function] `public TestWarehouse(int capacity)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L204 [type] `private sealed class EditorOffenseRewardCatalog : IOffenseRewardCatalog` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L209 [function] `public IReadOnlyCollection<BuildingSO> Buildings` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L210 [function] `public IReadOnlyCollection<FacilityBlueprintSO> Blueprints` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L212 [function] `private static IReadOnlyCollection<T> LoadAssets<T>()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L223 [type] `private sealed class ExpeditionRewardScenario : IDisposable` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L232 [function] `public ExpeditionRewardScenario()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L276 [function] `public void Dispose()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L289 [type] `private sealed class WorldMapFixture : IDisposable` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L295 [function] `public WorldMapFixture()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L302 [function] `public void Dispose()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L308 [type] `private sealed class ExpeditionFixture : IDisposable` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L314 [function] `public ExpeditionFixture()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L320 [function] `public void Dispose()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L326 [type] `private sealed class RewardFixture : IDisposable` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L332 [function] `public RewardFixture(ScenarioContext context)` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]
- L343 [function] `public void Dispose()` [EditorAssetAccess, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Offense/Editor/OffenseWorldMapDebugScenarios.cs

- L8 [type] `public static class OffenseWorldMapDebugScenarios` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L11 [function] `public static void RunFromMenu()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L20 [function] `public static bool RunAll(bool logSuccess)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L24 [function] `RunScenario("?潁??용끏????醫딆쓧??μ떜媛?걫??????????????????, VerifyInitialNearbyTargets, errors)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L25 [function] `RunScenario("?癲ル슢캉??뼘???醫딆┫??嶺뚮Ŋ?멱굢?????????????ㅻ쿋?? ??????, VerifyReconUpgradeRevealsMoreTargets, errors)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L26 [function] `RunScenario("??????????癲ル슢???ъ쒜?????썹땟???, VerifyTargetRequirementFields, errors)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L27 [function] `RunScenario("?????????????ｋ?????嚥??, VerifyTargetSelectionEvent, errors)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L28 [function] `RunScenario("???됰Ŧ???노???????됰Þ?????꾩룆???, VerifyPanelCreation, errors)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L48 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L62 [function] `private static bool VerifyInitialNearbyTargets()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L75 [function] `private static bool VerifyReconUpgradeRevealsMoreTargets()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L92 [function] `private static bool VerifyTargetRequirementFields()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L107 [function] `private static bool VerifyTargetSelectionEvent()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L121 [function] `private static bool VerifyPanelCreation()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L138 [type] `private sealed class ScenarioRuntime : IDisposable` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L144 [function] `public ScenarioRuntime()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L151 [function] `public void Dispose()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L157 [type] `private sealed class CountingSelectionListener :` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L164 [function] `public CountingSelectionListener()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L169 [function] `public void OnTriggerEvent(OffenseTargetSelectedEvent eventType)` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L177 [function] `public void Dispose()` [GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]


## Assets\Scripts\Offense

### Assets/Scripts/Offense/OffenseExpeditionEvents.cs

- L1 [type] `public struct OffenseExpeditionStartedEvent` [EventBus]
- L5 [function] `public OffenseExpeditionStartedEvent(OffenseExpeditionRun expedition)` [EventBus]
- L12 [function] `public static void Trigger(OffenseExpeditionRun expedition)` [EventBus]
- L19 [type] `public struct OffenseExpeditionCompletedEvent` [EventBus]
- L23 [function] `public OffenseExpeditionCompletedEvent(OffenseExpeditionResult result)` [EventBus]
- L30 [function] `public static void Trigger(OffenseExpeditionResult result)` [EventBus]

### Assets/Scripts/Offense/OffenseExpeditionModel.cs

- L6 [type] `public sealed class OffenseExpeditionMemberSnapshot`
- L14 [function] `public string ToSummaryText()`
- L22 [type] `public sealed class OffenseExpeditionResult`
- L36 [function] `public string ToDetailText()`
- L89 [type] `public sealed class OffenseExpeditionRun`
- L93 [function] `public OffenseExpeditionRun(string expeditionId, OffenseTargetDefinition target, IEnumerable<CharacterActor> members, float totalPower)`
- L118 [function] `public void Tick(float deltaTime)`

### Assets/Scripts/Offense/OffenseExpeditionService.cs

- L6 [type] `public static class OffenseExpeditionService`
- L8 [function] `public static bool CanJoinExpedition(CharacterActor actor, out string reason)`
- L62 [function] `public static float CalculateMemberPower(CharacterActor actor)`
- L80 [function] `public static float CalculatePartyPower(IEnumerable<CharacterActor> members)`
- L85 [function] `public static bool ShouldSucceed(OffenseExpeditionRun expedition)`
- L96 [function] `public static OffenseExpeditionResult Resolve(OffenseExpeditionRun expedition, bool? forceSuccess = null)`

### Assets/Scripts/Offense/OffenseExpeditionSystem.cs

- L8 [type] `public class OffenseExpeditionRuntime : MonoBehaviour` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L20 [function] `public void Construct(IOffenseExpeditionMemberQuery, IOffenseWorldMapRuntimeProvider, IOffenseRewardRuntimeProvider, IMetaProgressionRuntimeProvider, IOffensePanelService)` [DependencyInjection]
- L39 [function] `private void Update()`
- L44 [function] `public IReadOnlyList<CharacterActor> GetAvailableMemberActors()` [DependencyInjection]
- L49 [function] `public bool TryStartExpedition(string targetId, IEnumerable<CharacterActor> members, out OffenseExpeditionRun expedition, out string message)` [DependencyInjection, RuntimeObjectCreation]
- L106 [function] `public void Tick(float deltaTime)`
- L119 [function] `public bool CompleteExpeditionForDebug(string expeditionId, bool? forceSuccess, out OffenseExpeditionResult result)`
- L135 [function] `private OffenseExpeditionResult CompleteExpeditionAt(int index, bool? forceSuccess)` [DependencyInjection, RuntimeObjectCreation]
- L176 [function] `public OffenseExpeditionPanel ShowExpeditionPanel()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L181 [function] `private IOffenseExpeditionMemberQuery ResolveMemberQuery()` [DependencyInjection]
- L187 [function] `private IOffenseWorldMapRuntimeProvider ResolveWorldMapProvider()` [DependencyInjection]
- L193 [function] `private IOffenseRewardRuntimeProvider ResolveRewardProvider()` [DependencyInjection]
- L199 [function] `private IMetaProgressionRuntimeProvider ResolveMetaProgressionProvider()` [DependencyInjection]
- L205 [function] `private IOffensePanelService ResolvePanelService()` [DependencyInjection]
- L212 [type] `public class OffenseExpeditionPanel : MonoBehaviour` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L224 [function] `public void Bind(OffenseExpeditionRuntime source, OffenseWorldMapRuntime worldMap, IOffensePanelButtonFactory buttonFactory)` [DependencyInjection, SceneMutation]
- L239 [function] `public void Render()` [RuntimeObjectCreation, SceneMutation]
- L315 [function] `public void Hide()` [SceneMutation]
- L320 [function] `private static string BuildMemberLabel(CharacterActor member)`
- L334 [function] `private void EnsureView()`
- L345 [function] `private void ClearButtons()` [DependencyInjection, SceneMutation]
- L358 [function] `internal void BindGeneratedView(TMP_Text headerText, TMP_Text detailText, RectTransform memberButtonRoot)` [SceneMutation]
- L374 [function] `private IOffensePanelButtonFactory RequireButtonFactory()` [DependencyInjection]

### Assets/Scripts/Offense/OffensePanelUiFactory.cs

- L6 [type] `public static class OffensePanelUiFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L8 [function] `public static GameObject CreateOverlayCanvas(string name, int sortingOrder, Vector2 referenceResolution)` [RuntimeObjectCreation, SceneMutation]
- L24 [function] `public static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)` [RuntimeObjectCreation, SceneMutation]
- L42 [function] `public static GameObject CreateVerticalRoot(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, float spacing)` [RuntimeObjectCreation, SceneMutation]
- L67 [function] `public static GameObject CreateText(Transform parent, string name, float fontSize, TextAlignmentOptions alignment, ITmpKoreanFontService tmpKoreanFontService)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L90 [function] `public static GameObject CreateButton(RectTransform parent, string label, float fontSize, Action callback, ITmpKoreanFontService tmpKoreanFontService)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L116 [type] `public interface IOffensePanelButtonFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L118 [function] `GameObject CreateButton(RectTransform parent, string label, float fontSize, Action callback)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L119 [function] `void Release(GameObject buttonObject)` [SceneMutation]
- L122 [type] `public sealed class OffensePanelButtonFactory : IOffensePanelButtonFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L126 [function] `public OffensePanelButtonFactory(ITmpKoreanFontService tmpKoreanFontService)` [DependencyInjection]
- L132 [function] `public GameObject CreateButton(RectTransform parent, string label, float fontSize, Action callback)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L142 [function] `public void Release(GameObject buttonObject)` [SceneMutation]

### Assets/Scripts/Offense/OffenseRuntimeServices.cs

- L5 [type] `public interface IOffenseWorldMapRuntimeProvider` [DependencyInjection]
- L10 [type] `public interface IOffenseRewardRuntimeProvider` [DependencyInjection]
- L15 [type] `public interface IOffenseExpeditionMemberQuery` [DependencyInjection]
- L20 [type] `public interface IOffenseRewardCatalog` [DependencyInjection]
- L22 [function] `IReadOnlyCollection<BuildingSO> Buildings` [DependencyInjection]
- L23 [function] `IReadOnlyCollection<FacilityBlueprintSO> Blueprints` [DependencyInjection]
- L26 [type] `public interface IOffenseRewardSelector` [DependencyInjection]
- L28 [function] `StockCategory ResolveStockCategory(OffenseRewardPreview reward)` [DependencyInjection]
- L29 [function] `BuildingSO SelectRareFacility(OffenseRewardContext context, IReadOnlyCollection<int> additionallyExcludedBuildingIds)` [DependencyInjection]
- L32 [function] `FacilityBlueprintSO SelectBlueprint(OffenseRewardPreview reward, OffenseRewardContext context)` [DependencyInjection]
- L35 [function] `bool IsHumanFactionWeakening(OffenseRewardPreview reward, OffenseTargetDefinition target)` [DependencyInjection]
- L36 [function] `bool ContainsAny(string source, params string[] values)` [DependencyInjection]
- L39 [type] `public interface IOffenseRewardGrantService` [DependencyInjection]
- L41 [function] `IReadOnlyList<OffenseRewardGrantResult> GrantRewards(IEnumerable<OffenseRewardPreview> rewards, OffenseRewardContext context)` [DependencyInjection]
- L49 [type] `public interface IOffensePanelService` [DependencyInjection]
- L51 [function] `OffenseWorldMapPanel ShowWorldMap(OffenseWorldMapRuntime runtime)` [DependencyInjection]
- L52 [function] `OffenseExpeditionPanel ShowExpedition(OffenseExpeditionRuntime runtime)` [DependencyInjection]
- L55 [type] `public interface IOffensePanelFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L57 [function] `OffenseWorldMapPanel CreateWorldMapPanel()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L58 [function] `OffenseExpeditionPanel CreateExpeditionPanel()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L61 [type] `public sealed class OffenseWorldMapRuntimeProvider : IOffenseWorldMapRuntimeProvider` [DependencyInjection]
- L65 [function] `public OffenseWorldMapRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L71 [function] `public bool TryGetRuntime(out OffenseWorldMapRuntime runtime)` [DependencyInjection]
- L78 [type] `public sealed class OffenseRewardRuntimeProvider : IOffenseRewardRuntimeProvider` [DependencyInjection]
- L82 [function] `public OffenseRewardRuntimeProvider(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L88 [function] `public bool TryGetRuntime(out OffenseRewardRuntime runtime)` [DependencyInjection]
- L95 [type] `public sealed class OffenseExpeditionMemberQuery : IOffenseExpeditionMemberQuery` [DependencyInjection]
- L99 [function] `public OffenseExpeditionMemberQuery(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L105 [function] `public IReadOnlyList<CharacterActor> GetAvailableMemberActors()` [DependencyInjection]
- L114 [type] `public sealed class DataCatalogOffenseRewardCatalog : IOffenseRewardCatalog` [DependencyInjection]
- L118 [function] `public DataCatalogOffenseRewardCatalog(IDataCatalog catalog)` [DependencyInjection]
- L124 [function] `public IReadOnlyCollection<BuildingSO> Buildings` [DependencyInjection]
- L130 [function] `public IReadOnlyCollection<FacilityBlueprintSO> Blueprints` [DependencyInjection]
- L137 [type] `public sealed class OffensePanelService : IOffensePanelService` [DependencyInjection]
- L144 [function] `public OffensePanelService(IDungeonSceneComponentQuery sceneQuery, IOffenseWorldMapRuntimeProvider worldMapProvider, IOffensePanelFactory panelFactory, IOffensePanelButtonFactory buttonFactory)` [DependencyInjection]
- L160 [function] `public OffenseWorldMapPanel ShowWorldMap(OffenseWorldMapRuntime runtime)` [DependencyInjection]
- L173 [function] `public OffenseExpeditionPanel ShowExpedition(OffenseExpeditionRuntime runtime)` [DependencyInjection]
- L188 [type] `public sealed class OffensePanelFactory : IOffensePanelFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L193 [function] `public OffensePanelFactory(ITmpKoreanFontService tmpKoreanFontService, IObjectResolver objectResolver)` [DependencyInjection]
- L203 [function] `public OffenseWorldMapPanel CreateWorldMapPanel()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L249 [function] `public OffenseExpeditionPanel CreateExpeditionPanel()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Offense/OffenseRewardContextResolver.cs

- L5 [type] `public sealed class OffenseRewardDebugContext`
- L12 [function] `public void Clear()`
- L21 [type] `public interface IOffenseRewardContextBuilder` [DependencyInjection]
- L29 [type] `public sealed class OffenseRewardContextBuilder : IOffenseRewardContextBuilder` [DependencyInjection]
- L33 [function] `public OffenseRewardContextBuilder(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L39 [function] `public OffenseRewardContext Create(OffenseTargetDefinition target, OffenseRewardState state, OffenseRewardDebugContext debugContext)` [DependencyInjection]
- L72 [function] `private IEnumerable<IWarehouseFacility> ResolveWarehouses()` [DependencyInjection]

### Assets/Scripts/Offense/OffenseRewardEvents.cs

- L4 [type] `public struct OffenseRewardGrantedEvent` [EventBus]
- L9 [function] `public OffenseRewardGrantedEvent(OffenseExpeditionResult expeditionResult, IReadOnlyList<OffenseRewardGrantResult> grantResults)` [EventBus]
- L19 [function] `public static void Trigger(OffenseExpeditionResult expeditionResult, IReadOnlyList<OffenseRewardGrantResult> grantResults)` [EventBus]

### Assets/Scripts/Offense/OffenseRewardModel.cs

- L5 [type] `public sealed class OffenseRewardGrantResult`
- L14 [function] `public string ToSummaryText()`
- L29 [type] `public sealed class OffenseRewardState`
- L43 [function] `public void Reset()`
- L55 [function] `public void RecordMoney(int amount)`
- L60 [function] `public void RecordStock(StockCategory category, int amount)`
- L70 [function] `public bool RecordRareFacility(BuildingSO building)`
- L75 [function] `public bool RecordBlueprint(FacilityBlueprintSO blueprint)`
- L80 [function] `public void RecordFactionWeakening(bool humanFaction, int amount)`
- L93 [function] `public void RecordRecruitCandidates(int amount)`
- L98 [function] `public void RecordPrisoners(int amount)`
- L103 [function] `public void RecordSpecialMonsters(int amount)`
- L112 [type] `public sealed class OffenseRewardContext`

### Assets/Scripts/Offense/OffenseRewardSelector.cs

- L5 [type] `public sealed class OffenseRewardSelector : IOffenseRewardSelector` [DependencyInjection]
- L9 [function] `public OffenseRewardSelector(IOffenseRewardCatalog catalog)` [DependencyInjection]
- L15 [function] `public StockCategory ResolveStockCategory(OffenseRewardPreview reward)` [DependencyInjection]
- L36 [function] `public BuildingSO SelectRareFacility(OffenseRewardContext context, IReadOnlyCollection<int> additionallyExcludedBuildingIds)` [DependencyInjection]
- L62 [function] `public FacilityBlueprintSO SelectBlueprint(OffenseRewardPreview reward, OffenseRewardContext context)` [DependencyInjection]
- L92 [function] `public bool IsHumanFactionWeakening(OffenseRewardPreview reward, OffenseTargetDefinition target)` [DependencyInjection]
- L107 [function] `public bool ContainsAny(string source, params string[] values)` [DependencyInjection]

### Assets/Scripts/Offense/OffenseRewardSystem.cs

- L7 [type] `public sealed class OffenseRewardGrantService : IOffenseRewardGrantService` [DependencyInjection, EventBus]
- L11 [function] `public OffenseRewardGrantService(IOffenseRewardSelector selector)` [DependencyInjection]
- L17 [function] `public IReadOnlyList<OffenseRewardGrantResult> GrantRewards(IEnumerable<OffenseRewardPreview> rewards, OffenseRewardContext context)` [DependencyInjection, EventBus]
- L33 [function] `private OffenseRewardGrantResult GrantReward(OffenseRewardPreview reward, OffenseRewardContext context)` [DependencyInjection, EventBus]
- L50 [function] `private static OffenseRewardGrantResult GrantMoney(OffenseRewardPreview reward, OffenseRewardContext context)` [EventBus]
- L70 [function] `private OffenseRewardGrantResult GrantStock(OffenseRewardPreview reward, OffenseRewardContext context)` [DependencyInjection, EventBus]
- L97 [function] `private OffenseRewardGrantResult GrantRareFacility(OffenseRewardPreview reward, OffenseRewardContext context)` [DependencyInjection, EventBus]
- L128 [function] `private OffenseRewardGrantResult GrantBlueprint(OffenseRewardPreview reward, OffenseRewardContext context)` [DependencyInjection, EventBus]
- L155 [function] `private OffenseRewardGrantResult GrantFactionWeakening(OffenseRewardPreview reward, OffenseRewardContext context)` [DependencyInjection, EventBus]
- L165 [function] `private static OffenseRewardGrantResult GrantRecruitCandidate(OffenseRewardPreview reward, OffenseRewardContext context)` [EventBus]
- L174 [function] `private OffenseRewardGrantResult GrantPrisoner(OffenseRewardPreview reward, OffenseRewardContext context)` [DependencyInjection, EventBus]
- L189 [function] `private static OffenseRewardGrantResult Success(OffenseRewardPreview reward, int grantedAmount, string detail)` [EventBus]
- L205 [function] `private static OffenseRewardGrantResult Fail(OffenseRewardPreview reward, string detail)` [EventBus]
- L219 [type] `public class OffenseRewardRuntime : MonoBehaviour` [DependencyInjection, EventBus]
- L229 [function] `public void Construct(IOffenseRewardContextBuilder contextBuilder, IOffenseRewardGrantService grantService)` [DependencyInjection]
- L239 [function] `public IReadOnlyList<OffenseRewardGrantResult> ApplyExpeditionRewards(OffenseExpeditionRun expedition, OffenseExpeditionResult result)` [DependencyInjection, EventBus]
- L256 [function] `public void SetDebugContext(GameData gameData, IEnumerable<IWarehouseFacility> warehouses, FacilityShopUnlockState shopUnlockState, BlueprintResearchState researchState)`
- L268 [function] `public void ClearDebugContext()`
- L273 [function] `public void ResetState()`
- L278 [function] `private OffenseRewardContext CreateContext(OffenseTargetDefinition target)` [DependencyInjection]
- L283 [function] `private IOffenseRewardContextBuilder ResolveContextBuilder()` [DependencyInjection]
- L289 [function] `private IOffenseRewardGrantService ResolveGrantService()` [DependencyInjection]

### Assets/Scripts/Offense/OffenseTabSummaryQuery.cs

- L3 [type] `public readonly struct OffenseTabSummary`
- L39 [type] `public interface IOffenseTabSummaryService` [DependencyInjection]
- L44 [type] `public interface IOffenseTabSummaryRuntimeSource` [DependencyInjection]
- L51 [type] `public sealed class OffenseTabSummaryRuntimeSource : IOffenseTabSummaryRuntimeSource` [DependencyInjection]
- L55 [function] `public OffenseTabSummaryRuntimeSource(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L65 [type] `public sealed class OffenseTabSummaryService : IOffenseTabSummaryService` [DependencyInjection]
- L69 [function] `public OffenseTabSummaryService(IOffenseTabSummaryRuntimeSource runtimeSource)` [DependencyInjection]
- L74 [function] `public OffenseTabSummary Capture()` [DependencyInjection]

### Assets/Scripts/Offense/OffenseWorldMapEvents.cs

- L4 [type] `public struct OffenseWorldMapChangedEvent` [EventBus]
- L9 [function] `public OffenseWorldMapChangedEvent(OffenseWorldMapState state, IReadOnlyList<OffenseTargetSnapshot> visibleTargets)` [EventBus]
- L19 [function] `public static void Trigger(OffenseWorldMapState state, IReadOnlyList<OffenseTargetSnapshot> visibleTargets)` [EventBus]
- L29 [type] `public struct OffenseTargetSelectedEvent` [EventBus]
- L33 [function] `public OffenseTargetSelectedEvent(OffenseTargetSnapshot target)` [EventBus]
- L40 [function] `public static void Trigger(OffenseTargetSnapshot target)` [EventBus]
- L47 [type] `public struct OffenseReconUpgradedEvent` [EventBus]
- L53 [function] `public OffenseReconUpgradedEvent(int reconLevel, float scanRange, int newlyRevealedCount)` [EventBus]
- L62 [function] `public static void Trigger(int reconLevel, float scanRange, int newlyRevealedCount)` [EventBus]

### Assets/Scripts/Offense/OffenseWorldMapModel.cs

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

### Assets/Scripts/Offense/OffenseWorldMapService.cs

- L6 [type] `public static class OffenseWorldMapService`
- L10 [function] `public static float GetScanRange(int reconLevel)`
- L21 [function] `public static IReadOnlyList<OffenseTargetDefinition> CreateDefaultTargets()`
- L90 [function] `public static IReadOnlyList<OffenseTargetDefinition> NormalizeTargets(IEnumerable<OffenseTargetDefinition> targets)`
- L102 [function] `public static int RevealTargetsInRange(OffenseWorldMapState state, IEnumerable<OffenseTargetDefinition> targets)`
- L124 [function] `public static IReadOnlyList<OffenseTargetSnapshot> GetVisibleTargetSnapshots(OffenseWorldMapState state, IEnumerable<OffenseTargetDefinition> targets, bool preciseIntel)`
- L140 [function] `public static OffenseTargetDefinition FindKnownTarget(OffenseWorldMapState state, IEnumerable<OffenseTargetDefinition> targets, string targetId)`
- L153 [function] `private static OffenseTargetDefinition CreateTarget(string id, string title, string description, OffenseTargetKind kind, float distance, float danger, float durationSeconds, int requiredMembers, float requiredPower, params OffenseRewardPreview[] rewards)`
- L180 [function] `private static OffenseRewardPreview Reward(OffenseRewardCategory category, string label, int amount)`

### Assets/Scripts/Offense/OffenseWorldMapSystem.cs

- L8 [type] `public class OffenseWorldMapRuntime : MonoBehaviour` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L39 [function] `public void Construct(IOffensePanelService panelService)` [DependencyInjection]
- L45 [function] `private void Awake()`
- L50 [function] `public void StartWorldMap(int reconLevel = 0)`
- L58 [function] `public bool TryUpgradeRecon(out string message)`
- L75 [function] `public bool TrySelectTarget(string targetId, out OffenseTargetSnapshot snapshot, out string message)`
- L94 [function] `public bool TryGetKnownTargetSnapshot(string targetId, out OffenseTargetSnapshot snapshot)`
- L108 [function] `public OffenseWorldMapPanel ShowWorldMap()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L114 [function] `public void SetPreciseIntelForDebug(bool value)`
- L120 [function] `public void SetTargetsForDebug(IEnumerable<OffenseTargetDefinition> debugTargets)`
- L127 [function] `private void EnsureInitialized()`
- L138 [function] `private void RaiseChanged()`
- L148 [function] `private IOffensePanelService ResolvePanelService()` [DependencyInjection]
- L155 [type] `public class OffenseWorldMapPanel : MonoBehaviour` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L164 [function] `public void Bind(OffenseWorldMapRuntime source, IOffensePanelButtonFactory buttonFactory)` [DependencyInjection, SceneMutation]
- L175 [function] `public void Render()` [RuntimeObjectCreation, SceneMutation]
- L231 [function] `public void Hide()` [SceneMutation]
- L236 [function] `private void EnsureView()`
- L247 [function] `private void ClearButtons()` [DependencyInjection, SceneMutation]
- L257 [function] `internal void BindGeneratedView(TMP_Text headerText, TMP_Text detailText, RectTransform targetButtonRoot)` [SceneMutation]
- L273 [function] `private IOffensePanelButtonFactory RequireButtonFactory()` [DependencyInjection]

## Assets\Scripts\Operation\Editor

### Assets/Scripts/Operation/Editor/EventAlertDebugScenarios.cs

- L5 [type] `public static class EventAlertDebugScenarios` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L8 [function] `public static void RunFromMenu()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L17 [function] `public static bool RunAll(bool logSuccess)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L21 [function] `RunScenario("????????꾩룆????????노듋??????됰Þ??, VerifyAlertCreatesButtonAndDetail, errors)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L22 [function] `RunScenario("?熬곣뫖利??????嚥????⑤슢?뽫춯?살탨??", VerifyRepeatedAlertMerge, errors)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L23 [function] `RunScenario("????ｋ?????嚥??, VerifyChoiceEvent, errors)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L24 [function] `RunScenario("????ㅳ늾?온???癲ル슢캉??????嚥???汝??吏??, VerifySettlementKeepsEventLog, errors)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L44 [function] `private static void RunScenario(string name, System.Func<bool> scenario, List<string> errors)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L51 [function] `private static bool VerifyAlertCreatesButtonAndDetail()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L69 [function] `CleanupRuntimeUi()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L73 [function] `private static bool VerifyRepeatedAlertMerge()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L86 [function] `CleanupRuntimeUi()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L90 [function] `private static bool VerifyChoiceEvent()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L101 [function] `new EventAlertChoice("????彛??, "???繹먮굛???꿔꺂??????곗뵯?????????嶺?????⑤９??, () => selected = 1)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L102 [function] `new EventAlertChoice("???類ㅺ퉻??, "????썼キ?????ㅻ깹??????ㅼ굡??, () => selected = 2)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L103 [function] `new EventAlertChoice("????⑸뵃彛?, "????꾣뤃???????ｋ??, () => selected = 3)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L104 [function] `new EventAlertChoice("?潁???, "??嶺?筌??? ????源낅뼀????, () => selected = 4)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L116 [function] `CleanupRuntimeUi()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L120 [function] `private static bool VerifySettlementKeepsEventLog()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L126 [function] `new EventAlertRequest("????고뀘???????蹂λ쑋", "???????우쾵??, EventAlertImportance.Medium, "????고뀘???))` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L141 [function] `private static EventAlertRuntime CreateRuntime(out GameObject root)` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L147 [function] `private static void CleanupRuntimeUi()` [GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Operation/Editor/OperatingDaySettlementDebugScenarios.cs

- L5 [type] `public static class OperatingDaySettlementDebugScenarios` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L8 [function] `public static void RunFromMenu()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L17 [function] `public static bool RunAll(bool logSuccess)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L21 [function] `RunScenario("????ㅳ늾?온???癲ル슢캉???????볥궚??, VerifySettlementCollectsRuntimeEvents, errors)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L22 [function] `RunScenario("?癲ル슢캉???????노듋??????紐꾨쫯??, VerifyReportDetailText, errors)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L42 [function] `private static void RunScenario(string name, System.Func<bool> scenario, List<string> errors)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L49 [function] `private static bool VerifySettlementCollectsRuntimeEvents()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L100 [function] `private static bool VerifyReportDetailText()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L140 [function] `EnsureStat(character, CharacterCondition.HUNGER, 100f)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L141 [function] `EnsureStat(character, CharacterCondition.FUN, 50f)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L147 [function] `private static void EnsureStat(CharacterActor character, CharacterCondition condition, float value)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L155 [function] `private static BuildableObject CreateBuilding(string objectName, bool damaged, int maintenance)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L176 [function] `private static Facility CreateWarehouse(string objectName, int capacity)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]


## Assets\Scripts\Operation

### Assets/Scripts/Operation/EventAlertSystem.cs

- L6 [type] `public class EventAlertRuntime : MonoBehaviour, UtilEventListener<EventAlertRequestedEvent>` [DependencyInjection, EventBus]
- L12 [function] `private readonly List<EventAlertRecord> eventLog = new List<EventAlertRecord>();` [DependencyInjection, EventBus]
- L13 [function] `private readonly EventAlertSelectionState selectionState = new EventAlertSelectionState();` [DependencyInjection, EventBus]
- L25 [function] `public void Construct(IEventAlertViewPresenterFactory viewPresenterFactory)` [DependencyInjection]
- L32 [function] `public void OnTriggerEvent(EventAlertRequestedEvent eventType)` [DependencyInjection, EventBus]
- L55 [function] `public void Open(EventAlertRecord record)` [DependencyInjection, EventBus]
- L66 [function] `public void CloseDetail()` [DependencyInjection, EventBus]
- L71 [function] `public bool ExecuteChoice(int index)` [EventBus]
- L83 [function] `private void OnEnable()` [EventBus]
- L88 [function] `private void OnDisable()` [EventBus]
- L93 [function] `private void OnDestroy()` [DependencyInjection]
- L98 [function] `private void CreateButton(EventAlertRecord record)` [DependencyInjection, EventBus]
- L103 [function] `private void UpdateButton(EventAlertRecord record)` [DependencyInjection, EventBus]
- L108 [function] `private IEventAlertViewPresenter ResolveViewPresenter()` [DependencyInjection]

### Assets/Scripts/Operation/EventAlertEvents.cs

- L1 [type] `public struct EventAlertRequestedEvent` [EventBus]
- L5 [function] `public EventAlertRequestedEvent(EventAlertRequest request)` [EventBus]
- L12 [function] `public static void Trigger(EventAlertRequest request)` [EventBus]
- L19 [type] `public struct EventAlertLoggedEvent` [EventBus]
- L23 [function] `public EventAlertLoggedEvent(EventAlertRecord record)` [EventBus]
- L30 [function] `public static void Trigger(EventAlertRecord record)` [EventBus]

### Assets/Scripts/Operation/EventAlertMergePolicy.cs

- L4 [type] `public static class EventAlertMergePolicy`
- L6 [function] `public static EventAlertRecord FindMergeTarget(IEnumerable<EventAlertRecord> records, EventAlertRequest request)`
- L16 [function] `private static bool CanMerge(EventAlertRecord record, EventAlertRequest request)`

### Assets/Scripts/Operation/EventAlertModel.cs

- L5 [type] `public enum EventAlertImportance`
- L12 [type] `public class EventAlertChoice`
- L18 [function] `public EventAlertChoice(string label, string description = "", Action callback = null)`
- L26 [type] `public class EventAlertRequest`
- L34 [function] `public EventAlertRequest(`
- L48 [function] `private static IReadOnlyList<EventAlertChoice> NormalizeChoices(IEnumerable<EventAlertChoice> choices)`
- L58 [type] `public class EventAlertRecord`
- L68 [function] `public EventAlertRecord(int id, EventAlertRequest request)`
- L79 [function] `public void Increment()`
- L86 [function] `public string ToDetailText()`
- L125 [function] `private static string GetImportanceName(EventAlertImportance importance)`

### Assets/Scripts/Operation/EventAlertService.cs

- L3 [type] `public static class EventAlertService` [EventBus]
- L5 [function] `public static void Raise(` [EventBus]
- L15 [function] `public static void RaiseInvasionResult(string detail, EventAlertImportance importance = EventAlertImportance.High)` [EventBus]
- L20 [function] `public static void RaiseStaffComplaint(string detail, EventAlertImportance importance = EventAlertImportance.Medium)` [EventBus]
- L25 [function] `public static void RaiseBlueprintAcquired(string detail, EventAlertImportance importance = EventAlertImportance.Medium)` [EventBus]

### Assets/Scripts/Operation/EventAlertCanvasProvider.cs

- L4 [type] `public interface IEventAlertCanvasProvider` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L9 [type] `public sealed class EventAlertCanvasProvider : IEventAlertCanvasProvider` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L14 [function] `public EventAlertCanvasProvider(DungeonSceneRuntimeReferences sceneReferences)` [DependencyInjection]
- L20 [function] `public Canvas GetOrCreateCanvas()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Operation/EventAlertChoicePresenter.cs

- L6 [type] `public sealed class EventAlertChoicePresenter` [DependencyInjection, SceneMutation]
- L8 [function] `private readonly List<Button> choiceButtons = new List<Button>();` [RuntimeObjectCreation, SceneMutation]
- L11 [function] `public EventAlertChoicePresenter(IEventAlertButtonFactory buttonFactory)` [DependencyInjection]
- L17 [function] `public void Rebuild(Transform parent, EventAlertRecord record, Func<int, bool> executeChoice)` [DependencyInjection, SceneMutation]
- L41 [function] `public void Clear()` [SceneMutation]

### Assets/Scripts/Operation/EventAlertUiFactory.cs

- L7 [type] `public static class EventAlertUiFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L9 [function] `public static GameObject CreateRuntimeRoot(Canvas canvas)` [RuntimeObjectCreation, SceneMutation]
- L22 [function] `public static Button CreateAlertButton(Transform buttonRoot, EventAlertRecord record, UnityAction onClick, ITmpKoreanFontService tmpKoreanFontService)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L56 [function] `public static void UpdateAlertButton(Button button, EventAlertRecord record)` [SceneMutation]
- L74 [function] `public static Button CreateChoiceButton(` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L115 [function] `public static void CreateButtonRoot(` [RuntimeObjectCreation, SceneMutation]
- L171 [function] `public static void BindExistingButtonRootReferences(` [SceneMutation]
- L192 [function] `public static bool IsButtonRootReady(` [SceneMutation]
- L205 [function] `public static GameObject CreateDetailPanel(` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L259 [function] `private static TMP_Text CreateText(Transform parent, string name, string text, int fontSize, TextAlignmentOptions alignment, ITmpKoreanFontService tmpKoreanFontService)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L280 [function] `private static Color GetImportanceColor(EventAlertImportance importance)` [RuntimeObjectCreation, SceneMutation]
- L295 [type] `public interface IEventAlertButtonFactory` [DependencyInjection]
- L297 [function] `Button CreateAlertButton(Transform buttonRoot, EventAlertRecord record, UnityAction onClick)` [DependencyInjection]
- L298 [function] `void UpdateAlertButton(Button button, EventAlertRecord record)` [DependencyInjection]
- L299 [function] `Button CreateChoiceButton(Transform parent, EventAlertChoice choice, int choiceIndex, UnityAction onClick)` [DependencyInjection]
- L300 [function] `void Release(Button button)` [DependencyInjection]
- L303 [type] `public sealed class EventAlertButtonFactory : IEventAlertButtonFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L307 [function] `public EventAlertButtonFactory(ITmpKoreanFontService tmpKoreanFontService)` [DependencyInjection]
- L313 [function] `public Button CreateAlertButton(Transform buttonRoot, EventAlertRecord record, UnityAction onClick)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L322 [function] `public void UpdateAlertButton(Button button, EventAlertRecord record)` [DependencyInjection, SceneMutation]
- L327 [function] `public Button CreateChoiceButton(...)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L341 [function] `public void Release(Button button)` [RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Operation/EventAlertViewPresenter.cs

- L7 [type] `public readonly struct EventAlertViewPresenterContext` [DependencyInjection]
- L9 [function] `public EventAlertViewPresenterContext(Transform buttonRoot, GameObject detailPanel, TMP_Text detailText, Action<EventAlertRecord> openRecord, Func<int, bool> executeChoice, Action closeDetail)` [DependencyInjection]
- L33 [type] `public interface IEventAlertViewPresenter` [DependencyInjection]
- L44 [type] `public interface IEventAlertViewPresenterFactory` [DependencyInjection]
- L49 [type] `public sealed class EventAlertViewPresenterFactory : IEventAlertViewPresenterFactory` [DependencyInjection]
- L55 [function] `public EventAlertViewPresenterFactory(IEventAlertCanvasProvider canvasProvider, ITmpKoreanFontService tmpKoreanFontService, IEventAlertButtonFactory buttonFactory)` [DependencyInjection]
- L68 [function] `public IEventAlertViewPresenter Create(EventAlertViewPresenterContext context)` [DependencyInjection]
- L74 [type] `public sealed class EventAlertViewPresenter : IEventAlertViewPresenter` [DependencyInjection, SceneMutation]
- L76 [function] `private readonly Dictionary<int, Button> buttonsById = new Dictionary<int, Button>();` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L77 [function] `private readonly EventAlertChoicePresenter choicePresenter` [DependencyInjection, SceneMutation]
- L93 [function] `public EventAlertViewPresenter(...)` [DependencyInjection]
- L117 [function] `public bool IsDetailVisible => detailPanel != null && detailPanel.activeSelf` [SceneMutation]
- L119 [function] `public void EnsureRuntimeUI()` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L166 [function] `public void DestroyRuntimeUI()` [RuntimeObjectCreation, SceneMutation]
- L185 [function] `public void CreateButton(EventAlertRecord record)` [DependencyInjection, SceneMutation]
- L206 [function] `public void UpdateButton(EventAlertRecord record)` [DependencyInjection, SceneMutation]
- L216 [function] `public void OpenDetail(EventAlertRecord record)` [RuntimeObjectCreation, SceneMutation]
- L229 [function] `public void CloseDetail()` [SceneMutation]
- L237 [function] `private void LayoutButtons()` [SceneMutation]
- L242 [function] `private void ClearButtons()` [RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Operation/EventAlertLayout.cs

- L4 [type] `public static class EventAlertLayout` [SceneMutation]
- L19 [function] `public static void LayoutButtons(` [SceneMutation]
- L64 [function] `public static void ConfigureButtonViewport(` [SceneMutation]
- L92 [function] `private static float GetAlertListHeight(Canvas canvas)` [SceneMutation]
- L105 [function] `private static float GetButtonViewportHeight(RectTransform buttonViewportRect)` [SceneMutation]
- L115 [function] `private static int GetVisibleRowsForHeight(float availableHeight)` [SceneMutation]
- L130 [function] `private static int GetMaxVisibleRows()` [SceneMutation]
- L137 [function] `private static float GetContentHeightForRows(int rowCount)` [SceneMutation]

### Assets/Scripts/Operation/EventAlertSelectionState.cs

- L1 [type] `public sealed class EventAlertSelectionState`
- L3 [function] `public EventAlertRecord SelectedRecord { get; private set; }`
- L5 [function] `public void Select(EventAlertRecord record)`
- L13 [function] `public bool ExecuteChoice(int index)`

### Assets/Scripts/Operation/OperatingDayReportAlertBridge.cs

- L3 [type] `public class OperatingDayReportAlertBridge : MonoBehaviour, UtilEventListener<OperatingDayReportEvent>` [EventBus]
- L5 [function] `public void OnTriggerEvent(OperatingDayReportEvent eventType)` [EventBus]
- L27 [function] `private void OnEnable()` [EventBus]
- L32 [function] `private void OnDisable()` [EventBus]

### Assets/Scripts/Operation/OperatingDaySettlement.cs

- L8 [type] `public class FacilityRevenueSummary` [DependencyInjection, EventBus]
- L15 [type] `public class SpeciesVisitSummary` [DependencyInjection, EventBus]
- L22 [type] `public class StockConsumptionSummary` [DependencyInjection, EventBus]
- L29 [type] `public class WarehouseStockSummary` [DependencyInjection, EventBus]
- L41 [type] `public class StaffWorkSummary` [DependencyInjection, EventBus]
- L51 [type] `public class OperatingDayReport` [DependencyInjection, EventBus]
- L74 [function] `public string ToDetailText()` [DependencyInjection, EventBus]
- L138 [type] `public struct OperatingDayStartedEvent` [DependencyInjection, EventBus]
- L156 [type] `public struct OperatingDayEndedEvent` [DependencyInjection, EventBus]
- L174 [type] `public struct OperatingDayReportEvent` [DependencyInjection, EventBus]
- L192 [type] `public struct FacilityVisitEvent` [DependencyInjection, EventBus]
- L213 [type] `public struct FacilityRevenueEvent` [DependencyInjection, EventBus]
- L237 [type] `public struct FacilityStockConsumedEvent` [DependencyInjection, EventBus]
- L264 [type] `public enum FacilityCrimeKind` [DependencyInjection, EventBus]
- L269 [type] `public struct FacilityCrimeEvent` [DependencyInjection, EventBus]
- L309 [type] `public struct FacilityRestockEvent` [DependencyInjection, EventBus]
- L336 [type] `public class OperatingDaySettlementRuntime : MonoBehaviour, ...` [DependencyInjection, EventBus]
- L366 [function] `public void Construct(IDungeonSceneComponentQuery sceneQuery, IFacilityShopCatalog facilityShopCatalog, IRunVariableRuntimeReader runVariableReader)` [DependencyInjection, EventBus]
- L379 [function] `public void OnTriggerEvent(OperatingDayStartedEvent eventType)` [DependencyInjection, EventBus]
- L385 [function] `public void OnTriggerEvent(OperatingDayEndedEvent eventType)` [DependencyInjection, EventBus]
- L392 [function] `public void OnTriggerEvent(FacilityVisitEvent eventType)` [DependencyInjection, EventBus]
- L412 [function] `public void OnTriggerEvent(FacilityRevenueEvent eventType)` [DependencyInjection, EventBus]
- L424 [function] `public void OnTriggerEvent(FacilityStockConsumedEvent eventType)` [DependencyInjection, EventBus]
- L434 [function] `public void OnTriggerEvent(FacilityCrimeEvent eventType)` [DependencyInjection, EventBus]
- L442 [function] `public void OnTriggerEvent(FacilityRestockEvent eventType)` [DependencyInjection, EventBus]
- L450 [function] `public void OnTriggerEvent(StockSupplyEvent eventType)` [DependencyInjection, EventBus]
- L455 [function] `public void OnTriggerEvent(EventAlertLoggedEvent eventType)` [DependencyInjection, EventBus]
- L465 [function] `private void OnEnable()` [DependencyInjection, EventBus]
- L478 [function] `private void OnDisable()` [DependencyInjection, EventBus]
- L491 [function] `private OperatingDayReport BuildReport(int day)` [DependencyInjection, EventBus]
- L532 [function] `private void FillBuildingSnapshot(OperatingDayReport report, IEnumerable<BuildableObject> buildings)` [DependencyInjection, EventBus]
- L565 [function] `private static void FillStaffSnapshot(OperatingDayReport report, IEnumerable<CharacterActor> characters)` [DependencyInjection, EventBus]
- L589 [function] `private void ResetLedger()` [DependencyInjection, EventBus]
- L633 [function] `private IDungeonSceneComponentQuery RequireSceneQuery()` [DependencyInjection, EventBus]
- L643 [function] `private IFacilityShopCatalog RequireFacilityShopCatalog()` [DependencyInjection, EventBus]
- L653 [function] `private IRunVariableRuntimeReader RequireRunVariableReader()` [DependencyInjection, EventBus]

### Assets/Scripts/Operation/OperationTabSummaryQuery.cs

- L4 [type] `public readonly struct OperationTabSummary`
- L39 [type] `public interface IOperationTabSummaryService` [DependencyInjection]
- L44 [type] `public interface IOperationTabSummaryRuntimeSource` [DependencyInjection]
- L52 [type] `public sealed class OperationTabSummaryRuntimeSource : IOperationTabSummaryRuntimeSource` [DependencyInjection]
- L56 [function] `public OperationTabSummaryRuntimeSource(DungeonSceneRuntimeReferences sceneReferences)` [DependencyInjection]
- L61 [function] `public UIManager UIManager => sceneReferences.UIManager` [DependencyInjection]
- L62 [function] `public OperatingDaySettlementRuntime Settlement => sceneReferences.Settlement` [DependencyInjection]
- L63 [function] `public EventAlertRuntime Alerts => sceneReferences.Alerts` [DependencyInjection]
- L64 [function] `public RunVariableRuntime RunVariables => sceneReferences.RunVariables` [DependencyInjection]
- L67 [type] `public sealed class OperationTabSummaryService : IOperationTabSummaryService` [DependencyInjection]
- L71 [function] `public OperationTabSummaryService(IOperationTabSummaryRuntimeSource runtimeSource)` [DependencyInjection]
- L76 [function] `public OperationTabSummary Capture()` [DependencyInjection]


## Assets\Scripts\Recruitment\Editor

### Assets/Scripts/Recruitment/Editor/RegularCustomerDebugScenarios.cs

- L7 [type] `public static class RegularCustomerDebugScenarios` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L10 [function] `public static void RunFromMenu()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L19 [function] `public static bool RunAll(bool logSuccess)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L23 [function] `RunScenario("?熬곣뫖?삥납??좂뙴??????낅쵂?? ??????꿔꺂?????????뚯????덈춣?, VerifyVisitCountAndAverageSatisfaction, errors)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L24 [function] `RunScenario("??壤굿??? ???뚯??? ????, VerifyRegularThreshold, errors)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L25 [function] `RunScenario("????쇨덫??????썹땟?雅????뚯??? ????, VerifyRecruitCandidateThreshold, errors)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L26 [function] `RunScenario("????쇨덫???嚥▲굧????? ????????쒙쭫????嶺뚮????, VerifyRecruitmentConsumesCustomer, errors)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L27 [function] `RunScenario("??? ?꿔꺂????????썹땟?????壤굿??? ??嶺뚮????, VerifyLowSatisfactionDoesNotBecomeRegular, errors)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L28 [function] `RunScenario("?熬곣뫖?삥납??좂뙴????嚥???????????쇰뮚??, VerifyRuntimeEvents, errors)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L48 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L62 [function] `private static bool VerifyVisitCountAndAverageSatisfaction()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L77 [function] `DestroyCustomer(customer)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L81 [function] `private static bool VerifyRegularThreshold()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L96 [function] `DestroyCustomer(customer)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L100 [function] `private static bool VerifyRecruitCandidateThreshold()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L116 [function] `DestroyCustomer(customer)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L120 [function] `private static bool VerifyRecruitmentConsumesCustomer()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L142 [function] `DestroyCustomer(customer)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L146 [function] `private static bool VerifyLowSatisfactionDoesNotBecomeRegular()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L161 [function] `DestroyCustomer(customer)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L165 [function] `private static bool VerifyRuntimeEvents()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L189 [function] `DestroyCustomer(customer)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L194 [function] `private static RegularCustomerRules CreateTestRules()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L206 [function] `private static CharacterActor CreateCustomer(int id, string name, string speciesTag, float mood)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L229 [function] `private static BuildableObject CreateFacility()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L249 [function] `private static void DestroyCustomer(CharacterActor character)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L261 [type] `private sealed class CountingRegularListener : UtilEventListener<RegularCustomerBecameRegularEvent>, IDisposable` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L265 [function] `public CountingRegularListener()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L270 [function] `public void OnTriggerEvent(RegularCustomerBecameRegularEvent eventType)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L275 [function] `public void Dispose()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L281 [type] `private sealed class CountingCandidateListener : UtilEventListener<RecruitCandidateDiscoveredEvent>, IDisposable` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L285 [function] `public CountingCandidateListener()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L290 [function] `public void OnTriggerEvent(RecruitCandidateDiscoveredEvent eventType)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L295 [function] `public void Dispose()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]


## Assets\Scripts\Recruitment

### Assets/Scripts/Recruitment/RegularCustomerSystem.cs

- L6 [type] `public enum RegularCustomerStatus` [EventBus]
- L15 [type] `public enum RecruitCapability` [EventBus]
- L25 [type] `public class RegularCustomerRules` [EventBus]
- L33 [function] `public static RegularCustomerRules CreateDefault()` [EventBus]
- L39 [type] `public sealed class RegularCustomerSnapshot` [EventBus]
- L49 [function] `public string ToSummaryText()` [EventBus]
- L55 [type] `public sealed class RegularCustomerRecord` [EventBus]
- L59 [function] `public RegularCustomerRecord(int customerId, CharacterActor customer, RecruitCapability recruitCapabilities)` [EventBus]
- L91 [function] `public void RecordVisit(CharacterActor customer, float satisfaction, RegularCustomerRules rules)` [EventBus]
- L115 [function] `public bool MarkRecruited()` [EventBus]
- L127 [function] `public RegularCustomerSnapshot ToSnapshot()` [EventBus]
- L142 [type] `public readonly struct RegularCustomerVisitResult` [EventBus]
- L165 [type] `public readonly struct RegularCustomerRecruitResult` [EventBus]
- L167 [function] `public RegularCustomerRecruitResult(bool success, RegularCustomerRecord record, string message)` [EventBus]
- L182 [type] `public sealed class RegularCustomerState` [EventBus]
- L190 [function] `public RegularCustomerVisitResult RecordVisit(CharacterActor customer, RegularCustomerRules rules)` [EventBus]
- L214 [function] `public bool TryGetRecord(int customerId, out RegularCustomerRecord record)` [EventBus]
- L219 [function] `public bool IsRecruited(int customerId)` [EventBus]
- L224 [function] `public bool TryRecruit(int customerId, out RegularCustomerRecruitResult result)` [EventBus]
- L255 [function] `private RegularCustomerRecord GetOrCreate(int customerId, CharacterActor customer, RegularCustomerRules rules)` [EventBus]
- L267 [type] `public struct RegularCustomerUpdatedEvent` [EventBus]
- L271 [function] `public RegularCustomerUpdatedEvent(RegularCustomerVisitResult result)` [EventBus]
- L278 [function] `public static void Trigger(RegularCustomerVisitResult result)` [EventBus]
- L285 [type] `public struct RegularCustomerBecameRegularEvent` [EventBus]
- L289 [function] `public RegularCustomerBecameRegularEvent(RegularCustomerSnapshot snapshot)` [EventBus]
- L296 [function] `public static void Trigger(RegularCustomerSnapshot snapshot)` [EventBus]
- L303 [type] `public struct RecruitCandidateDiscoveredEvent` [EventBus]
- L307 [function] `public RecruitCandidateDiscoveredEvent(RegularCustomerSnapshot snapshot)` [EventBus]
- L314 [function] `public static void Trigger(RegularCustomerSnapshot snapshot)` [EventBus]
- L321 [type] `public struct CustomerRecruitedEvent` [EventBus]
- L325 [function] `public CustomerRecruitedEvent(RegularCustomerRecruitResult result)` [EventBus]
- L332 [function] `public static void Trigger(RegularCustomerRecruitResult result)` [EventBus]
- L339 [type] `public static class RegularCustomerService` [EventBus]
- L341 [function] `public static bool IsTrackableCustomer(CharacterActor customer)` [EventBus]
- L350 [function] `public static int GetCustomerId(CharacterActor customer)` [EventBus]
- L361 [function] `public static string GetCustomerDisplayName(CharacterActor customer, int customerId)` [EventBus]
- L377 [function] `public static string GetCustomerSpeciesTag(CharacterActor customer)` [EventBus]
- L388 [function] `public static float GetSatisfaction(CharacterActor customer)` [EventBus]
- L401 [function] `public static CharacterSO GetCustomerData(CharacterActor customer)` [EventBus]
- L406 [function] `private static CharacterIdentity GetIdentity(CharacterActor customer)` [EventBus]
- L411 [function] `public static bool MeetsRegularCondition(RegularCustomerRecord record, RegularCustomerRules rules)` [EventBus]
- L419 [function] `public static bool MeetsRecruitCandidateCondition(RegularCustomerRecord record, RegularCustomerRules rules)` [EventBus]
- L428 [function] `public static bool CanSpawnAsCustomer(CharacterSO data, RegularCustomerState state)` [EventBus]
- L443 [function] `public static string FormatCapabilities(RecruitCapability capabilities)` [EventBus]
- L453 [type] `public class RegularCustomerRuntime : MonoBehaviour, UtilEventListener<FacilityVisitEvent>` [EventBus]
- L462 [function] `public void OnTriggerEvent(FacilityVisitEvent eventType)` [EventBus]
- L495 [function] `public bool TryRecruit(int customerId, out RegularCustomerRecruitResult result)` [EventBus]
- L512 [function] `private void OnEnable()` [EventBus]
- L517 [function] `private void OnDisable()` [EventBus]


## Assets\Scripts\Research

### Assets/Scripts/Research/BlueprintResearchSystem.cs

- L8 [type] `public class BlueprintResearchTask` [DependencyInjection, EventBus]
- L13 [function] `public BlueprintResearchTask(FacilityBlueprintSO blueprint)` [DependencyInjection, EventBus]
- L25 [function] `public float AddProgress(float amount)` [DependencyInjection, EventBus]
- L38 [type] `public class BlueprintResearchState` [DependencyInjection, EventBus]
- L50 [function] `public bool EnqueueBlueprint(FacilityBlueprintSO blueprint)` [DependencyInjection, EventBus]
- L66 [function] `public bool TryGetActiveTask(out BlueprintResearchTask task)` [DependencyInjection, EventBus]
- L72 [function] `public bool IsCompleted(FacilityBlueprintSO blueprint)` [DependencyInjection, EventBus]
- L77 [function] `public void MarkCompleted(FacilityBlueprintSO blueprint)` [DependencyInjection, EventBus]
- L87 [function] `public bool UnlockRecipe(string recipeId)` [DependencyInjection, EventBus]
- L93 [type] `public readonly struct BlueprintResearchUnlockResult` [DependencyInjection, EventBus]
- L113 [type] `public readonly struct BlueprintResearchWorkResult` [DependencyInjection, EventBus]
- L143 [type] `public struct BlueprintResearchQueuedEvent` [DependencyInjection, EventBus]
- L161 [type] `public struct BlueprintResearchProgressEvent` [DependencyInjection, EventBus]
- L179 [type] `public struct BlueprintResearchCompletedEvent` [DependencyInjection, EventBus]
- L200 [type] `public static class BlueprintResearchService` [DependencyInjection, EventBus]
- L204 [function] `public static float CalculateResearchWork(CharacterActor researcher, BuildableObject researchFacility, float seconds)` [DependencyInjection, EventBus]
- L213 [function] `public static float GetFacilityResearchMultiplier(BuildableObject researchFacility)` [DependencyInjection, EventBus]
- L239 [function] `public static BlueprintResearchUnlockResult ApplyCompletion(FacilityBlueprintSO blueprint, BlueprintResearchState state, FacilityShopUnlockState shopUnlockState, IFacilityShopCatalog facilityShopCatalog)` [DependencyInjection, EventBus]
- L302 [type] `public class BlueprintResearchRuntime : MonoBehaviour, UtilEventListener<FacilityShopPurchasedEvent>` [DependencyInjection, EventBus]
- L315 [function] `public void Construct(IFacilityShopUnlockStateService shopUnlockStateService, IFacilityShopCatalog facilityShopCatalog)` [DependencyInjection, EventBus]
- L325 [function] `public bool EnqueueBlueprint(FacilityBlueprintSO blueprint)` [DependencyInjection, EventBus]
- L341 [function] `public BlueprintResearchWorkResult ApplyResearchWork(CharacterActor researcher, BuildableObject researchFacility, float seconds)` [DependencyInjection, EventBus]
- L374 [function] `public void OnTriggerEvent(FacilityShopPurchasedEvent eventType)` [DependencyInjection, EventBus]
- L384 [function] `private void CompleteTask(FacilityBlueprintSO blueprint)` [DependencyInjection, EventBus]
- L403 [function] `private static string FormatUnlockResult(BlueprintResearchUnlockResult result)` [DependencyInjection, EventBus]
- L417 [function] `private static void AddLines(List<string> lines, string title, IReadOnlyList<string> values)` [DependencyInjection, EventBus]
- L427 [function] `private IFacilityShopUnlockStateService ResolveShopUnlockStateService()` [DependencyInjection, EventBus]
- L433 [function] `private IFacilityShopCatalog ResolveFacilityShopCatalog()` [DependencyInjection, EventBus]
- L439 [function] `private void OnEnable()` [DependencyInjection, EventBus]
- L444 [function] `private void OnDisable()` [DependencyInjection, EventBus]

### Assets/Scripts/Research/ResearchCraftingSummaryQuery.cs

- L3 [type] `public readonly struct ResearchCraftingSummary`
- L32 [type] `public interface IResearchCraftingSummaryService` [DependencyInjection]
- L37 [type] `public interface IResearchCraftingSummaryRuntimeSource` [DependencyInjection]
- L43 [type] `public sealed class ResearchCraftingSummaryRuntimeSource : IResearchCraftingSummaryRuntimeSource` [DependencyInjection]
- L47 [function] `public ResearchCraftingSummaryRuntimeSource(IDungeonSceneComponentQuery sceneQuery)` [DependencyInjection]
- L56 [type] `public sealed class ResearchCraftingSummaryService : IResearchCraftingSummaryService` [DependencyInjection]
- L60 [function] `public ResearchCraftingSummaryService(IResearchCraftingSummaryRuntimeSource runtimeSource)` [DependencyInjection]
- L65 [function] `public ResearchCraftingSummary Capture()` [DependencyInjection]


## Assets\Scripts\Rooms\Editor

### Assets/Scripts/Rooms/Editor/RoomSystemDebugScenarios.cs

- L7 [type] `public static class RoomSystemDebugScenarios` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L10 [function] `public static void RunAllFromMenu()` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L12 [function] `RunAll(true)` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L15 [function] `public static bool RunAll(bool log = false)` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L18 [function] `RunScenario("Closed room gets role from furniture", VerifyClosedRoomGetsRoleFromFurniture, errors)` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L19 [function] `RunScenario("Open hallway is not a usable room", VerifyOpenHallwayIsNotUsableRoom, errors)` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L20 [function] `RunScenario("Room scan does not mutate pathing", VerifyRoomScanDoesNotMutatePathing, errors)` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L21 [function] `RunScenario("Room requirement gates facility candidate", VerifyRoomRequirementGatesFacilityCandidate, errors)` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L22 [function] `RunScenario("Room requirement gates CanVisit and path visitability", VerifyRoomRequirementGatesCanVisitAndPathVisitability, errors)` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L23 [function] `RunScenario("Formal wall blocks movement even over hallway", VerifyFormalWallBlocksMovement, errors)` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L39 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L54 [function] `private static bool VerifyClosedRoomGetsRoleFromFurniture()` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L69 [function] `private static bool VerifyOpenHallwayIsNotUsableRoom()` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L78 [function] `private static bool VerifyRoomScanDoesNotMutatePathing()` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L97 [function] `private static bool VerifyRoomRequirementGatesFacilityCandidate()` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L127 [function] `private static bool VerifyRoomRequirementGatesCanVisitAndPathVisitability()` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L150 [function] `private static bool VerifyFormalWallBlocksMovement()` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L159 [type] `private sealed class RoomScenarioWorld : IDisposable` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L163 [function] `private RoomScenarioWorld(Grid grid)` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L171 [function] `public static RoomScenarioWorld CreateClosedToiletRoom()` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L182 [function] `new Vector2Int(3, 0)` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L189 [function] `public static RoomScenarioWorld CreateOpenToiletHallway()` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L199 [function] `new Vector2Int(3, 0)` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L205 [function] `public static RoomScenarioWorld CreateWallBlockedHallway()` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L217 [function] `public void Dispose()` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L227 [function] `private void AddHallway(Vector2Int position)` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L230 [function] `new TestHallwayOccupant()` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L236 [function] `private BuildableObject PlaceDoor(Vector2Int position)` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L242 [function] `private BuildableObject PlaceWall(Vector2Int position)` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L259 [function] `private BuildableObject Place(BuildingSO data, Vector2Int position)` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L310 [type] `private sealed class TestHallwayOccupant : IGridOccupant` [EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Rooms/Editor/RoomEditorFacades.cs

- L1 [type] `public static class RoomRegistry` [EditorAssetAccess, DependencyInjection]
- L5 [function] `internal static IRoomLayoutCache EditorCache` [EditorAssetAccess, DependencyInjection]
- L7 [function] `public static RoomLayout GetLayout(Grid grid)` [EditorAssetAccess, DependencyInjection]
- L12 [function] `public static bool TryGetRoom(BuildableObject part, out RoomInstance room)` [EditorAssetAccess, DependencyInjection]
- L17 [function] `public static void Clear()` [EditorAssetAccess, DependencyInjection]
- L23 [type] `public static class RoomFacilityPolicy` [EditorAssetAccess, DependencyInjection]
- L28 [function] `public static bool IsFacilityRoleAvailable(...)` [EditorAssetAccess, DependencyInjection]
- L36 [function] `public static float GetRoomUtilityScore(BuildableObject building, FacilityRole role)` [EditorAssetAccess, DependencyInjection]


## Assets\Scripts\Rooms

### Assets/Scripts/Rooms/RoomDetector.cs

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

### Assets/Scripts/Rooms/RoomFacilityPolicy.cs

- L4 [type] `public interface IRoomFacilityPolicy` [DependencyInjection]
- L6 [function] `bool IsFacilityRoleAvailable(` [DependencyInjection]
- L11 [function] `float GetRoomUtilityScore(BuildableObject building, FacilityRole role)` [DependencyInjection]
- L14 [type] `public sealed class RoomFacilityPolicyService : IRoomFacilityPolicy` [DependencyInjection]
- L18 [function] `public RoomFacilityPolicyService(IRoomLayoutCache roomLayoutCache)` [DependencyInjection]
- L24 [function] `public bool IsFacilityRoleAvailable(` [DependencyInjection]
- L67 [function] `public float GetRoomUtilityScore(BuildableObject building, FacilityRole role)` [DependencyInjection]

### Assets/Scripts/Rooms/RoomInstance.cs

- L4 [type] `public sealed class RoomInstance`
- L9 [function] `public RoomInstance(int id, IReadOnlyList<Vector2Int> cells, IReadOnlyList<BuildableObject> furniture, IReadOnlyList<BuildableObject> doors, IReadOnlyList<BuildableObject> walls, int solidBoundaryCount, int openBoundaryCount, bool selfContained = false)`
- L59 [function] `public bool ContainsCell(Vector2Int position)`
- L64 [function] `public bool ContainsPart(BuildableObject part)`
- L69 [function] `public bool SupportsFacilityRole(FacilityRole role)`
- L76 [function] `public float GetQualityScore()`
- L89 [function] `private static FacilityRole BuildFacilityRoles(IReadOnlyList<BuildableObject> furniture)`
- L108 [function] `private static RectInt CalculateBounds(IReadOnlyList<Vector2Int> cells)`

### Assets/Scripts/Rooms/RoomLayout.cs

- L4 [type] `public sealed class RoomLayout`
- L9 [function] `public RoomLayout(IReadOnlyList<RoomInstance> rooms)`
- L39 [function] `public bool TryGetRoom(Vector2Int cell, out RoomInstance room)`
- L44 [function] `public bool TryGetRoom(BuildableObject part, out RoomInstance room)`

### Assets/Scripts/Rooms/RoomRegistry.cs

- L4 [type] `public interface IRoomLayoutCache` [DependencyInjection]
- L6 [function] `RoomLayout GetLayout(Grid grid)` [DependencyInjection]
- L7 [function] `bool TryGetRoom(BuildableObject part, out RoomInstance room)` [DependencyInjection]
- L8 [function] `void Clear()` [DependencyInjection]
- L11 [type] `public sealed class RoomLayoutCache : IRoomLayoutCache` [DependencyInjection]
- L13 [type] `private sealed class CachedLayout` [DependencyInjection]
- L22 [function] `public RoomLayout GetLayout(Grid grid)` [DependencyInjection]
- L44 [function] `public bool TryGetRoom(BuildableObject part, out RoomInstance room)` [DependencyInjection]
- L55 [function] `public void Clear()` [DependencyInjection]

### Assets/Scripts/Rooms/RoomRole.cs

- L4 [type] `public enum RoomRole`
- L18 [type] `public static class RoomRoleUtility`
- L20 [function] `public static RoomRole FromFacilityRoles(FacilityRole roles)`
- L35 [function] `public static FacilityRole ToFacilityRoles(RoomRole roles)`


## Assets\Scripts\Run\Editor

### Assets/Scripts/Run/Editor/RunVariableDebugScenarios.cs

- L8 [type] `public static class RunVariableDebugScenarios` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L11 [function] `public static void RunFromMenu()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L20 [function] `public static bool RunAll(bool logSuccess)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L24 [function] `RunScenario("??嶺뚮??ｆ뤃???⑤슢堉???????ｋ??, VerifyStartVariables, errors)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L25 [function] `RunScenario("????ㅳ늾?온 ???嚥??9???熬곣뫖利???, VerifyOperationVariables, errors)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L26 [function] `RunScenario("????ㅳ늾?온 ???嚥???꿔꺂????壤?, VerifyOperationVariableExpiration, errors)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L27 [function] `RunScenario("????쇨덫???????????????쇰뮚??, VerifyCostIntegrations, errors)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L28 [function] `RunScenario("??⑤㈇?뚧납????⑤슢堉???5?????繹먮냱??????쇰뮚??, VerifyInvasionVariables, errors)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L29 [function] `RunScenario("???嚥????????熬곣뫖利든뜏類ｋ㎜壤?, VerifyEventAlerts, errors)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L49 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L63 [function] `private static bool VerifyStartVariables()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L82 [function] `private static bool VerifyOperationVariables()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L110 [function] `private static bool VerifyOperationVariableExpiration()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L123 [function] `private static bool VerifyCostIntegrations()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L160 [function] `private static bool VerifyInvasionVariables()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L189 [function] `private static bool VerifyEventAlerts()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L201 [function] `private static BuildingSO CreateBuilding(int id, string name, bool defense)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L220 [function] `private static FacilityBlueprintSO CreateBlueprint(int id, string displayName, int defaultCost)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L230 [function] `private static CharacterSO CreateCharacter(int id, CharacterType type, string speciesTag)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L239 [type] `private sealed class ScenarioRuntime : IDisposable` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L245 [function] `public ScenarioRuntime()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L252 [function] `public void Dispose()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L258 [type] `private sealed class CountingEventAlertRequestListener :` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L266 [function] `public CountingEventAlertRequestListener()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L271 [function] `public void OnTriggerEvent(EventAlertRequestedEvent eventType)` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]
- L279 [function] `public void Dispose()` [EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation]


## Assets\Scripts\Run

### Assets/Scripts/Run/RunVariableSystem.cs

- L6 [type] `public class RunVariableRuntime :` [DependencyInjection, EventBus]
- L26 [function] `public void Construct(IOwnerRunDataProvider ownerRunDataProvider, IInvasionThreatRuntimeProvider invasionThreatRuntimeProvider, IRunStartVariableSelector runStartVariableSelector)` [DependencyInjection]
- L39 [function] `private void Awake()` [DependencyInjection, EventBus]
- L49 [function] `public void StartRun(int seed, CharacterSO ownerData = null, InvasionThreatDifficulty difficulty = InvasionThreatDifficulty.Normal)` [DependencyInjection, EventBus]
- L67 [function] `public void OnTriggerEvent(OperatingDayStartedEvent eventType)` [DependencyInjection, EventBus]
- L78 [function] `public void OnTriggerEvent(OperatingDayEndedEvent eventType)` [DependencyInjection, EventBus]
- L87 [function] `public void OnTriggerEvent(InvasionCandidateEvent eventType)` [DependencyInjection, EventBus]
- L93 [function] `public void OnTriggerEvent(InvasionResolvedEvent eventType)` [DependencyInjection, EventBus]
- L98 [function] `public ActiveRunVariable ActivateOperationVariable(RunVariableId id, int day = -1, bool alert = true)` [DependencyInjection, EventBus]
- L120 [function] `public RunVariableDefinition SelectInvasionVariable(RunVariableId id, bool alert = true)` [DependencyInjection, EventBus]
- L142 [function] `public float GetGuestDemandMultiplier(string speciesTag)` [DependencyInjection, EventBus]
- L147 [function] `public float GetStockCostMultiplier(StockCategory category)` [DependencyInjection, EventBus]
- L152 [function] `public float GetFacilityShopCostMultiplier(BuildingSO building)` [DependencyInjection, EventBus]
- L157 [function] `public float GetBlueprintCostMultiplier(FacilityBlueprintSO blueprint)` [DependencyInjection, EventBus]
- L162 [function] `public float GetThreatRiseMultiplier()` [DependencyInjection, EventBus]
- L167 [function] `public float GetWarningThresholdMultiplier()` [DependencyInjection, EventBus]
- L172 [function] `public InvasionIntruderSettings ApplyInvasionSettings(InvasionIntruderSettings source)` [DependencyInjection, EventBus]
- L177 [function] `private void RollOperationVariable(int day)` [DependencyInjection, EventBus]
- L189 [function] `private void SelectRandomInvasionVariable()` [DependencyInjection, EventBus]
- L202 [function] `private void EnsureRunStarted()` [DependencyInjection, EventBus]
- L214 [function] `private void EnsureRandom()` [DependencyInjection, EventBus]
- L222 [function] `private InvasionThreatDifficulty ResolveDifficulty()` [DependencyInjection]
- L233 [function] `private CharacterSO ResolveSelectedOwnerData()` [DependencyInjection]
- L238 [function] `private IOwnerRunDataProvider ResolveOwnerRunDataProvider()` [DependencyInjection]
- L244 [function] `private IInvasionThreatRuntimeProvider ResolveInvasionThreatRuntimeProvider()` [DependencyInjection]
- L250 [function] `private IRunStartVariableSelector ResolveRunStartVariableSelector()` [DependencyInjection]
- L256 [function] `private void OnEnable()` [EventBus]
- L264 [function] `private void OnDisable()` [EventBus]

### Assets/Scripts/Run/RunVariableModel.cs

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

### Assets/Scripts/Run/RunVariableEvents.cs

- L1 [type] `public struct RunStartVariablesSelectedEvent` [EventBus]
- L5 [function] `public RunStartVariablesSelectedEvent(RunStartVariableSnapshot snapshot)` [EventBus]
- L12 [function] `public static void Trigger(RunStartVariableSnapshot snapshot)` [EventBus]
- L19 [type] `public struct RunVariableActivatedEvent` [EventBus]
- L23 [function] `public RunVariableActivatedEvent(ActiveRunVariable activeVariable)` [EventBus]
- L30 [function] `public static void Trigger(ActiveRunVariable activeVariable)` [EventBus]
- L37 [type] `public struct RunVariableExpiredEvent` [EventBus]
- L41 [function] `public RunVariableExpiredEvent(RunVariableDefinition definition)` [EventBus]
- L48 [function] `public static void Trigger(RunVariableDefinition definition)` [EventBus]
- L55 [type] `public struct InvasionVariableSelectedEvent` [EventBus]
- L59 [function] `public InvasionVariableSelectedEvent(RunVariableDefinition definition)` [EventBus]
- L66 [function] `public static void Trigger(RunVariableDefinition definition)` [EventBus]

### Assets/Scripts/Run/RunVariableCatalog.cs

- L4 [type] `public static class RunVariableCatalog`
- L10 [function] `public static RunVariableDefinition Get(RunVariableId id)`
- L15 [function] `public static IReadOnlyList<RunVariableDefinition> GetByCategory(RunVariableCategory category)`
- L22 [function] `private static Dictionary<RunVariableId, RunVariableDefinition> BuildDefinitions()`

### Assets/Scripts/Run/RunStartVariableSelector.cs

- L5 [type] `public interface IRunStartVariableSelector` [DependencyInjection]
- L7 [function] `RunStartVariableSnapshot Create(int seed, CharacterSO ownerData, InvasionThreatDifficulty difficulty)` [DependencyInjection]
- L13 [type] `public sealed class RunStartVariableSelector : IRunStartVariableSelector` [DependencyInjection]
- L18 [function] `public RunStartVariableSelector(IRunStartVariableCatalog catalog, IMetaProgressionRuntimeReader metaProgressionReader)` [DependencyInjection]
- L28 [function] `public RunStartVariableSnapshot Create(int seed, CharacterSO ownerData, InvasionThreatDifficulty difficulty)` [DependencyInjection]
- L76 [function] `private static string ResolveLayoutId(CharacterSO ownerData, System.Random startRandom)` [DependencyInjection]

### Assets/Scripts/Run/RunVariableEffects.cs

- L5 [type] `public static class RunVariableEffects`
- L7 [function] `public static float GetGuestDemandMultiplier(RunVariableState state, string speciesTag)`
- L21 [function] `public static float GetStockCostMultiplier(RunVariableState state, StockCategory category)`
- L35 [function] `public static float GetFacilityShopCostMultiplier(RunVariableState state, BuildingSO building)`
- L56 [function] `public static float GetBlueprintCostMultiplier(RunVariableState state, FacilityBlueprintSO blueprint)`
- L68 [function] `public static float GetThreatRiseMultiplier(RunVariableState state)`
- L84 [function] `public static float GetWarningThresholdMultiplier(RunVariableState state)`
- L96 [function] `public static InvasionIntruderSettings ApplyInvasionSettings(RunVariableState state, InvasionIntruderSettings source)`


## Assets\Scripts\Synthesis\Editor

### Assets/Scripts/Synthesis/Editor/FacilitySynthesisDebugScenarios.cs

- L9 [type] `public static class FacilitySynthesisDebugScenarios` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L12 [function] `public static void RunFromMenu()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L21 [function] `public static bool RunAll(bool logSuccess)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L26 [function] `RunScenario("???꾩룆???????????꾩룆???, VerifySynthesisAssets, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L27 [function] `RunScenario("???????꾩룆????癲ル슢?롩걡?붽괌??????, VerifyRepresentativeTreeAssets, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L28 [function] `RunScenario("??????????????됰슦?????????醫딆쓧???嶺???, VerifyRecipeVisibility, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L29 [function] `RunScenario("?熬곣뫖利?????嶺?筌????꾩룆???, VerifyPlacedFacilitiesAreConsumedAndReplaced, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L30 [function] `RunScenario("3????嶺뚮ㅎ??????꾩룆????癲ル슢?롩걡?붽괌?, VerifyThreeStarRestaurantSynthesis, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L31 [function] `RunScenario("3????????嚥▲굧???????꾩룆????癲ル슢?롩걡?붽괌?, VerifyThreeStarDefenseAndGuardTrees, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L32 [function] `RunScenario("????戮?땁 ??影??낟??, VerifyLevelInheritance, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L33 [function] `RunScenario("???? ??癲??꿸쑨????", VerifyDamagedMaterialRejected, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L34 [function] `RunScenario("????????됰슦???????????쇰뮛?????繹먮굝鍮?, VerifySpecialRecipeRequiresResearch, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L35 [function] `RunScenario("3??????????됰슦???????????쇰뮛?????繹먮굝鍮?, VerifyThreeStarSpecialRecipeRequiresResearch, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L36 [function] `RunScenario("???꾩룆???UI ?????, VerifySynthesisPanelRendering, errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L56 [function] `private static void RunScenario(string name, Func<bool> scenario, List<string> errors)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L70 [function] `private static bool VerifySynthesisAssets()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L90 [function] `private static bool VerifyRepresentativeTreeAssets()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L122 [function] `private static bool VerifyRecipeVisibility()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L144 [function] `private static bool VerifyPlacedFacilitiesAreConsumedAndReplaced()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L167 [function] `private static bool VerifyThreeStarRestaurantSynthesis()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L177 [function] `LoadRecipe("RS_BattlefieldDining")` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L181 [function] `LoadRecipe("RS_NobleDining")` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L195 [function] `private static bool VerifyThreeStarDefenseAndGuardTrees()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L205 [function] `LoadRecipe("RS_CorrosionFreezer")` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L209 [function] `LoadRecipe("RS_WarBarracks")` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L226 [function] `private static bool VerifyLevelInheritance()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L236 [function] `LoadRecipe("RS_PremiumMeatRestaurant")` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L246 [function] `private static bool VerifyDamagedMaterialRejected()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L255 [function] `LoadRecipe("RS_VenomSpikeTrap")` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L265 [function] `private static bool VerifySpecialRecipeRequiresResearch()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L292 [function] `private static bool VerifyThreeStarSpecialRecipeRequiresResearch()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L321 [function] `private static bool VerifySynthesisPanelRendering()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L338 [function] `private static BuildingSO LoadBuilding(string assetName)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L343 [function] `private static FacilitySynthesisRecipeSO LoadRecipe(string assetName)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L348 [type] `private sealed class SynthesisScenarioWorld : IDisposable` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L358 [function] `public SynthesisScenarioWorld()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L365 [function] `new TestHallwayOccupant()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L381 [function] `public FacilitySynthesisRuntime CreateRuntime()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L388 [function] `public BlueprintResearchRuntime CreateResearchRuntime()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L395 [function] `public BuildableObject Place(string assetName, Vector2Int position)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L421 [function] `public void TrackObject(GameObject obj)` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L429 [function] `public void Dispose()` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]
- L440 [type] `private sealed class TestHallwayOccupant : IGridOccupant` [SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/Synthesis/Editor/P1FacilitySynthesisAssetBuilder.cs

- L7 [type] `public static class P1FacilitySynthesisAssetBuilder` [EditorAssetAccess, Reflection]
- L15 [function] `public static void EnsureP1SynthesisAssetsFromMenu()` [EditorAssetAccess, Reflection]
- L17 [function] `EnsureP1SynthesisAssets()` [EditorAssetAccess, Reflection]
- L20 [function] `public static void EnsureP1SynthesisAssets()` [EditorAssetAccess, Reflection]
- L25 [function] `EnsureFolder(RecipeFolder)` [EditorAssetAccess, Reflection]
- L26 [function] `EnsureFolder(EffectFolder)` [EditorAssetAccess, Reflection]
- L27 [function] `EnsureFolder(StockFolder)` [EditorAssetAccess, Reflection]
- L31 [function] `EnsureBuildingAsset(spec)` [EditorAssetAccess, Reflection]
- L36 [function] `EnsureRecipeAsset(spec)` [EditorAssetAccess, Reflection]
- L43 [function] `private static void EnsureBuildingAsset(SynthesisBuildingSpec spec)` [EditorAssetAccess, Reflection]
- L53 [function] `EnsureSpriteImport(spec.spritePath)` [EditorAssetAccess, Reflection]
- L77 [function] `EnsureStockInfo(spec)` [EditorAssetAccess, Reflection]
- L81 [function] `private static void EnsureRecipeAsset(RecipeSpec spec)` [EditorAssetAccess, Reflection]
- L106 [function] `private static SynthesisBuildingSpec[] CreateBuildingSpecs()` [EditorAssetAccess, Reflection]
- L134 [function] `EmptyDefense())` [EditorAssetAccess, Reflection]
- L159 [function] `EmptyDefense())` [EditorAssetAccess, Reflection]
- L169 [function] `DefenseFacilityData(FacilityWorkType.Repair, 0)` [EditorAssetAccess, Reflection]
- L181 [function] `Effect(DefenseEffectKind.Damage, 18f, 0f, 1, "???筌뤿굝琉?)` [EditorAssetAccess, Reflection]
- L182 [function] `Effect(DefenseEffectKind.Corrosion, 0.35f, 10f, 1, "???낇뀘???)` [EditorAssetAccess, Reflection]
- L193 [function] `DefenseFacilityData(FacilityWorkType.Repair | FacilityWorkType.Guard, 1)` [EditorAssetAccess, Reflection]
- L205 [function] `Effect(DefenseEffectKind.Damage, 12f, 0f, 1, "???筌뤿굝琉?)` [EditorAssetAccess, Reflection]
- L206 [function] `Effect(DefenseEffectKind.Charge, 14f, 10f, 1, "???ㅻ쿋繹??)` [EditorAssetAccess, Reflection]
- L207 [function] `Effect(DefenseEffectKind.GuardAttack, 8f, 0f, 1, "?嚥▲굧??????????)` [EditorAssetAccess, Reflection]
- L267 [function] `EmptyDefense())` [EditorAssetAccess, Reflection]
- L292 [function] `EmptyDefense())` [EditorAssetAccess, Reflection]
- L302 [function] `DefenseFacilityData(FacilityWorkType.Repair, 0)` [EditorAssetAccess, Reflection]
- L314 [function] `Effect(DefenseEffectKind.Damage, 12f, 0f, 1, "???筌뤿굝琉?)` [EditorAssetAccess, Reflection]
- L315 [function] `Effect(DefenseEffectKind.Corrosion, 0.45f, 12f, 1, "???낇뀘???)` [EditorAssetAccess, Reflection]
- L316 [function] `Effect(DefenseEffectKind.Slow, 0.75f, 5f, 1, "??醫딆┫???)` [EditorAssetAccess, Reflection]
- L327 [function] `DefenseFacilityData(FacilityWorkType.Repair | FacilityWorkType.Guard, 1)` [EditorAssetAccess, Reflection]
- L340 [function] `Effect(DefenseEffectKind.Burn, 3f, 6f, 1, "???????띾뼎")` [EditorAssetAccess, Reflection]
- L341 [function] `Effect(DefenseEffectKind.Charge, 18f, 10f, 1, "???ㅻ쿋繹??)` [EditorAssetAccess, Reflection]
- L342 [function] `Effect(DefenseEffectKind.GuardAttack, 10f, 0f, 1, "?嚥▲굧??????????)` [EditorAssetAccess, Reflection]
- L381 [function] `private static RecipeSpec[] CreateRecipeSpecs()` [EditorAssetAccess, Reflection]
- L498 [function] `private static FacilityData DefenseFacilityData(FacilityWorkType workTypes, int requiredWorkers)` [EditorAssetAccess, Reflection]
- L543 [function] `private static DefenseFacilityData EmptyDefense()` [EditorAssetAccess, Reflection]
- L561 [function] `private static DefenseEffectSO[] EnsureEffectAssets(string prefix, DefenseEffectData[] effects)` [EditorAssetAccess, Reflection]
- L592 [function] `private static DefenseEffectData Effect(DefenseEffectKind kind, float amount, float duration, int stacks, string logTag)` [EditorAssetAccess, Reflection]
- L604 [function] `private static BuildingSO LoadBuilding(string assetName)` [EditorAssetAccess, Reflection]
- L614 [function] `private static Sprite LoadSprite(string path)` [EditorAssetAccess, Reflection]
- L624 [function] `private static void EnsureSpriteImport(string path)` [EditorAssetAccess, Reflection]
- L640 [function] `private static void EnsureStockInfo(SynthesisBuildingSpec spec)` [EditorAssetAccess, Reflection]
- L661 [function] `private static void EnsureFolder(string folder)` [EditorAssetAccess, Reflection]
- L677 [type] `private readonly struct SynthesisBuildingSpec` [EditorAssetAccess, Reflection]
- L715 [type] `private readonly struct RecipeSpec` [EditorAssetAccess, Reflection]


## Assets\Scripts\Synthesis

### Assets/Scripts/Synthesis/FacilitySynthesisRecipeSO.cs

- L6 [type] `public class FacilitySynthesisRecipeSO : DataScriptableObject`

### Assets/Scripts/Synthesis/FacilitySynthesisSystem.cs

- L8 [type] `public class FacilitySynthesisRecipeSnapshot` [DependencyInjection, EventBus]
- L17 [function] `public string ToSummaryText()` [DependencyInjection, EventBus]
- L27 [type] `public readonly struct FacilitySynthesisResult` [DependencyInjection, EventBus]
- L29 [function] `public FacilitySynthesisResult(...)` [DependencyInjection, EventBus]
- L50 [type] `public struct FacilitySynthesisCompletedEvent` [DependencyInjection, EventBus]
- L54 [function] `public FacilitySynthesisCompletedEvent(FacilitySynthesisResult result)` [DependencyInjection, EventBus]
- L61 [function] `public static void Trigger(FacilitySynthesisResult result)` [DependencyInjection, EventBus]
- L68 [type] `public struct FacilitySynthesisSelectionChangedEvent` [DependencyInjection, EventBus]
- L72 [function] `public FacilitySynthesisSelectionChangedEvent(IReadOnlyList<BuildableObject> selectedMaterials)` [DependencyInjection, EventBus]
- L79 [function] `public static void Trigger(IReadOnlyList<BuildableObject> selectedMaterials)` [DependencyInjection, EventBus]
- L86 [type] `public static class FacilitySynthesisService` [DependencyInjection, EventBus]
- L88 [function] `public static bool IsRecipeVisible(FacilitySynthesisRecipeSO recipe, BlueprintResearchState researchState, IMetaProgressionRuntimeReader metaProgressionReader)` [DependencyInjection, EventBus]
- L115 [function] `public static FacilitySynthesisRecipeSnapshot ToSnapshot(FacilitySynthesisRecipeSO recipe, BlueprintResearchState researchState, IMetaProgressionRuntimeReader metaProgressionReader)` [DependencyInjection, EventBus]
- L140 [function] `public static bool MatchesMaterials(FacilitySynthesisRecipeSO recipe, IReadOnlyList<BuildableObject> materials)` [DependencyInjection, EventBus]
- L157 [function] `public static int CalculateInheritedLevel(...)` [DependencyInjection, EventBus]
- L172 [type] `public class FacilitySynthesisRuntime : MonoBehaviour` [DependencyInjection, EventBus]
- L185 [function] `public void ConstructFacilitySynthesisRuntime(IBlueprintResearchStateService blueprintResearchStateService, IGridTextureProvider gridTextureProvider, IObjectResolver objectResolver, IFacilitySynthesisRecipeQuery recipeQuery, IGridBuildingObjectFactory gridBuildingObjectFactory)` [DependencyInjection, EventBus]
- L207 [function] `public IReadOnlyList<FacilitySynthesisRecipeSO> VisibleRecipes => ResolveRecipeQuery().GetVisibleRecipes(ResearchState)` [DependencyInjection, EventBus]
- L213 [function] `public void ToggleMaterialSelection(BuildableObject building)` [DependencyInjection, EventBus]
- L232 [function] `public void ClearSelection()` [DependencyInjection, EventBus]
- L238 [function] `public bool TrySynthesizeSelected(FacilitySynthesisRecipeSO recipe, out FacilitySynthesisResult result)` [DependencyInjection, EventBus]
- L249 [function] `public bool TrySynthesizeSelected(string recipeId, out FacilitySynthesisResult result)` [DependencyInjection, EventBus]
- L255 [function] `public FacilitySynthesisRecipeSnapshot ToSnapshot(FacilitySynthesisRecipeSO recipe)` [DependencyInjection, EventBus]
- L260 [function] `public bool TrySynthesize(...)` [DependencyInjection, EventBus]
- L320 [function] `private bool Validate(...)` [DependencyInjection, EventBus]
- L384 [function] `private static bool CanPlaceResultOverMaterials(...)` [DependencyInjection, EventBus]
- L408 [function] `private void RemoveMaterialFromGrid(BuildableObject material)` [DependencyInjection, EventBus]
- L423 [function] `private IBlueprintResearchStateService ResolveResearchStateService()` [DependencyInjection, EventBus]
- L429 [function] `private IGridTextureProvider ResolveGridTextureProvider()` [DependencyInjection, EventBus]
- L435 [function] `private void InjectCreatedBuilding(BuildableObject building)` [DependencyInjection, EventBus]
- L445 [function] `private IObjectResolver ResolveObjectResolver()` [DependencyInjection, EventBus]
- L451 [function] `private IFacilitySynthesisRecipeQuery ResolveRecipeQuery()` [DependencyInjection, EventBus]

## Assets\Scripts\Synthesis\UI

### Assets/Scripts/Synthesis/UI/FacilitySynthesisPanel.cs

- L7 [type] `public class FacilitySynthesisPanel :` [DependencyInjection, Reflection, EventBus, SceneMutation]
- L20 [function] `public void Construct(IFacilitySynthesisRuntimeProvider runtimeProvider)` [DependencyInjection]
- L26 [function] `public void Bind(FacilitySynthesisRuntime nextRuntime)` [DependencyInjection]
- L32 [function] `internal void BindGeneratedView(TMP_Text summaryText)` [SceneMutation]
- L39 [function] `public void Refresh()` [DependencyInjection, SceneMutation]
- L81 [function] `public void OnTriggerEvent(FacilitySynthesisSelectionChangedEvent eventType)` [EventBus, SceneMutation]
- L86 [function] `public void OnTriggerEvent(BlueprintResearchCompletedEvent eventType)` [EventBus, SceneMutation]
- L91 [function] `public void OnTriggerEvent(FacilitySynthesisCompletedEvent eventType)` [EventBus, SceneMutation]
- L96 [function] `private void ApplyText()` [SceneMutation]
- L104 [function] `private FacilitySynthesisRuntime ResolveRuntime()` [DependencyInjection]
- L113 [function] `private void OnEnable()` [EventBus]
- L120 [function] `private void OnDisable()` [EventBus]


## Assets\Scripts\UI

### Assets/Scripts/UI/RuntimePanelFactories.cs

- L7 [type] `public interface ICodexPanelFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L9 [function] `CodexPanel CreateDefaultPanel(CodexRuntime runtime)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L12 [type] `public interface IFacilitySynthesisPanelFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L14 [function] `FacilitySynthesisPanel CreateDefaultPanel(FacilitySynthesisRuntime runtime)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L17 [type] `public interface IFacilityEvolutionPanelFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L19 [function] `FacilityEvolutionPanel CreateDefaultPanel(FacilityEvolutionRuntime runtime)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L22 [type] `public sealed class CodexPanelFactory : ICodexPanelFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L27 [function] `public CodexPanelFactory(ITmpKoreanFontService tmpKoreanFontService)` [DependencyInjection]
- L34 [function] `public CodexPanelFactory(ITmpKoreanFontService tmpKoreanFontService, IObjectResolver objectResolver)` [DependencyInjection]
- L44 [function] `public CodexPanel CreateDefaultPanel(CodexRuntime runtime)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L66 [function] `private TMP_Text CreateSummaryText(Transform parent, float fontSize, Vector2 offsetMin, Vector2 offsetMax)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L72 [type] `public sealed class FacilitySynthesisPanelFactory : IFacilitySynthesisPanelFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L77 [function] `public FacilitySynthesisPanelFactory(ITmpKoreanFontService tmpKoreanFontService)` [DependencyInjection]
- L84 [function] `public FacilitySynthesisPanelFactory(ITmpKoreanFontService tmpKoreanFontService, IObjectResolver objectResolver)` [DependencyInjection]
- L94 [function] `public FacilitySynthesisPanel CreateDefaultPanel(FacilitySynthesisRuntime runtime)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L122 [type] `public sealed class FacilityEvolutionPanelFactory : IFacilityEvolutionPanelFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L127 [function] `public FacilityEvolutionPanelFactory(ITmpKoreanFontService tmpKoreanFontService)` [DependencyInjection]
- L134 [function] `public FacilityEvolutionPanelFactory(ITmpKoreanFontService tmpKoreanFontService, IObjectResolver objectResolver)` [DependencyInjection]
- L144 [function] `public FacilityEvolutionPanel CreateDefaultPanel(FacilityEvolutionRuntime runtime)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L172 [type] `internal static class RuntimePanelFactoryUtility` [RuntimeObjectCreation, SceneMutation]
- L174 [function] `public static GameObject CreateOverlayCanvas(string name, Vector2 referenceResolution)` [RuntimeObjectCreation, SceneMutation]
- L185 [function] `public static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color backgroundColor)` [RuntimeObjectCreation, SceneMutation]
- L207 [function] `public static TMP_Text CreateSummaryText(Transform parent, ITmpKoreanFontService tmpKoreanFontService, float fontSize, Vector2 offsetMin, Vector2 offsetMax)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]

### Assets/Scripts/UI/BackgroundManager.cs

- L7 [type] `public class BackgroundManager : MonoBehaviour`
- L12 [function] `private void Awake()`
- L41 [function] `void Start()`

### Assets/Scripts/UI/BuildingSummaryInfo.cs

- L6 [type] `public class BuildingSummaryInfo : UIPopUp,UtilEventListener<InfoFeedEvent>` [DependencyInjection, EventBus, SceneMutation]
- L16 [function] `public void Construct(IUiPopupService popupService, IBuildingDefinitionLookup buildingDefinitionLookup, ITmpKoreanFontService tmpKoreanFontService)` [DependencyInjection]
- L29 [function] `private void Start()` [DependencyInjection, EventBus, SceneMutation]
- L34 [function] `public void OnTriggerEvent(InfoFeedEvent eventType)` [DependencyInjection, EventBus, SceneMutation]
- L63 [function] `public override void OnClose()` [SceneMutation]
- L67 [function] `public void OnEnable()` [EventBus]
- L71 [function] `private void OnDisable()` [EventBus]
- L76 [function] `private IUiPopupService ResolvePopupService()` [DependencyInjection]
- L87 [function] `private IBuildingDefinitionLookup ResolveBuildingDefinitionLookup()` [DependencyInjection]
- L98 [function] `private ITmpKoreanFontService RequireTmpKoreanFontService()` [DependencyInjection]
- L106 [type] `public class BuildingInfoTarget : IInfoable` [EventBus]
- L110 [function] `public BuildingInfoTarget(BuildableObject building)` [EventBus]
- L115 [function] `public InfoFeedEvent.Type GetInfoType()` [EventBus]
- L121 [type] `public struct InfoFeedEvent` [EventBus]
- L124 [type] `public enum Type` [EventBus]
- L129 [function] `public InfoFeedEvent(IInfoable infoable)` [EventBus]
- L134 [function] `public static void Trigger(IInfoable infoable)` [EventBus]

### Assets/Scripts/UI/CameraManager.cs

- L5 [type] `public class CameraManager : MonoBehaviour` [DependencyInjection, SceneMutation]
- L21 [function] `public void Construct(IGridSystemProvider gridSystemProvider, IMainCameraProvider mainCameraProvider)` [DependencyInjection]
- L28 [function] `private void Awake()` [SceneMutation]
- L33 [function] `private void OnEnable()` [DependencyInjection]
- L38 [function] `private void Start()` [DependencyInjection, SceneMutation]
- L45 [function] `private void OnDisable()` [DependencyInjection]
- L50 [function] `private void OnDestroy()` [DependencyInjection]
- L55 [function] `private void Update()` [SceneMutation]
- L85 [function] `public void ClampToCurrentBounds()` [DependencyInjection, SceneMutation]
- L95 [function] `public void OnGridExpand()` [SceneMutation]
- L100 [function] `private void SubscribeToGridExpansionIfInjected()` [DependencyInjection]
- L108 [function] `private void SubscribeToGridExpansion()` [DependencyInjection]
- L121 [function] `private void UnsubscribeFromGridExpansion()` [DependencyInjection]
- L132 [function] `private IGridSystemProvider RequireGridSystemProvider()` [DependencyInjection]
- L138 [function] `private void UpdateViewportSize()` [DependencyInjection, SceneMutation]
- L152 [function] `private void GetGridBounds(out Vector2 lowerBound, out Vector2 upperBound)` [DependencyInjection]
- L167 [function] `private static float ClampAxis(float value, float min, float max, float halfViewSize)` [SceneMutation]
- L179 [function] `private IMainCameraProvider RequireMainCameraProvider()` [DependencyInjection]

### Assets/Scripts/UI/CharacterSummeryInfo.cs

- L8 [type] `public class CharacterSummeryInfo : UIPopUp, UtilEventListener<InfoFeedEvent>` [DependencyInjection, Reflection, EventBus, SceneMutation]
- L27 [function] `public void Construct(IUiPopupService popupService, ICharacterSummaryRuntimeLogFactory runtimeLogFactory)` [DependencyInjection]
- L37 [function] `private void Start()` [DependencyInjection, SceneMutation]
- L44 [function] `public void OnTriggerEvent(InfoFeedEvent eventType)` [DependencyInjection, Reflection, EventBus, SceneMutation]
- L84 [function] `public override void OnClose()` [SceneMutation]
- L90 [function] `public void OnStatChange(Dictionary<CharacterCondition, float> stats)` [SceneMutation]
- L100 [function] `private static void SetSlider(Slider slider, Dictionary<CharacterCondition, float> stats, CharacterCondition condition)` [SceneMutation]
- L115 [function] `public void OnLogAdded(CharacterLogEntry entry)` [EventBus, SceneMutation]
- L120 [function] `public void RefreshLogText()` [SceneMutation]
- L130 [function] `public static string FormatLogText(CharacterActor character, int maxLines = 8)`
- L135 [function] `public static string FormatLogText(CharacterLog characterLog, int maxLines = 8)`
- L153 [function] `public void OnEnable()` [EventBus]
- L158 [function] `private void OnDisable()` [EventBus]
- L164 [function] `private void UnbindCharacter()` [EventBus]
- L186 [function] `private static string GetDisplayName(GameObject targetObject)` [Reflection]
- L197 [function] `private IUiPopupService ResolvePopupService()` [DependencyInjection]
- L208 [function] `private ICharacterSummaryRuntimeLogFactory ResolveRuntimeLogFactory()` [DependencyInjection]
- L219 [function] `private GameObject RequireUiRoot()`

### Assets/Scripts/UI/CharacterSummaryRuntimeLogFactory.cs

- L6 [type] `public interface ICharacterSummaryRuntimeLogFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L8 [function] `TMP_Text Ensure(GameObject uiRoot, TMP_Text current)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L9 [function] `void ApplyFonts(Transform root)` [DependencyInjection]
- L12 [type] `public sealed class CharacterSummaryRuntimeLogFactory : ICharacterSummaryRuntimeLogFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L19 [function] `public CharacterSummaryRuntimeLogFactory(ITmpKoreanFontService tmpKoreanFontService)` [DependencyInjection]
- L25 [function] `public TMP_Text Ensure(GameObject uiRoot, TMP_Text current)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L51 [function] `public void ApplyFonts(Transform root)` [DependencyInjection]
- L61 [function] `private void ApplyStyle(TMP_Text text)` [DependencyInjection, SceneMutation]

### Assets/Scripts/UI/DungeonSceneBackdropFitter.cs

- L7 [type] `public class DungeonSceneBackdropFitter : MonoBehaviour` [DependencyInjection, SceneMutation]
- L19 [function] `public void Construct(IGridSystemProvider gridSystemProvider, IDungeonBackdropReferenceProvider backdropReferenceProvider, IDungeonBackdropSpriteTilingFactory spriteTilingFactory)` [DependencyInjection]
- L30 [function] `private void OnEnable()` [DependencyInjection]
- L35 [function] `private void Start()` [DependencyInjection, SceneMutation]
- L41 [function] `private void OnDisable()` [DependencyInjection]
- L46 [function] `public void FitToGrid()` [DependencyInjection, SceneMutation]
- L62 [function] `private void SubscribeToGridExpansionIfInjected()` [DependencyInjection]
- L70 [function] `private void SubscribeToGridExpansion()` [DependencyInjection]
- L83 [function] `private void UnsubscribeFromGridExpansion()` [DependencyInjection]
- L94 [function] `private IGridSystemProvider RequireGridSystemProvider()` [DependencyInjection]
- L100 [function] `private IDungeonBackdropReferenceProvider RequireBackdropReferenceProvider()` [DependencyInjection]
- L106 [function] `private IDungeonBackdropSpriteTilingFactory RequireSpriteTilingFactory()` [DependencyInjection]
- L112 [function] `private Transform ResolveBackgroundRoot()` [DependencyInjection]
- L119 [function] `private Tilemap ResolveGroundTilemap()` [DependencyInjection]
- L126 [function] `private void ExtendGround(int leftTile, int rightTile)` [DependencyInjection, SceneMutation]
- L157 [function] `private void ExtendBackgroundSprites(int leftTile, int rightTile)` [DependencyInjection, SceneMutation]
- L182 [function] `private static bool IsTiledBackgroundRenderer(SpriteRenderer renderer)`
- L190 [function] `private void ExtendBackgroundSpriteGroup(List<SpriteRenderer> renderers, int leftTile, int rightTile)` [DependencyInjection, SceneMutation]
- L216 [function] `private void FitSolidBackground(int leftTile, int rightTile)` [DependencyInjection, SceneMutation]
- L234 [function] `private static float GetMinX(IEnumerable<SpriteRenderer> renderers)`
- L245 [function] `private static float GetMaxX(IEnumerable<SpriteRenderer> renderers)`

### Assets/Scripts/UI/DungeonBackdropSpriteTilingFactory.cs

- L4 [type] `public interface IDungeonBackdropSpriteTilingFactory` [DependencyInjection]
- L6 [function] `SpriteRenderer Duplicate(SpriteRenderer template, float targetMinX)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L9 [type] `public sealed class DungeonBackdropSpriteTilingFactory : IDungeonBackdropSpriteTilingFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L11 [function] `public SpriteRenderer Duplicate(SpriteRenderer template, float targetMinX)` [RuntimeObjectCreation, SceneMutation]


## Assets\Scripts\UI\Editor

### Assets/Scripts/UI/Editor/TMPKoreanFontEditorResolver.cs

- L4 [type] `public static class TMPKoreanFontEditorResolver` [EditorAssetAccess]
- L8 [function] `public static ITmpKoreanFontService CreateService()` [DependencyInjection, EditorAssetAccess]


## Assets\Scripts\UI

### Assets/Scripts/UI/IInfoable.cs

- L5 [type] `public interface IInfoable`
- L7 [function] `public InfoFeedEvent.Type GetInfoType()`

### Assets/Scripts/UI/NoticeFeed.cs

- L4 [type] `public class NoticeFeed : MonoBehaviour, UtilEventListener<NoticeFeedEvent>` [DependencyInjection, EventBus]
- L10 [function] `public void ConstructNoticeFeed(INoticeFeedPresenter presenter)` [DependencyInjection]
- L16 [function] `public virtual void OnTriggerEvent(NoticeFeedEvent e)` [DependencyInjection, EventBus]
- L21 [function] `private void OnEnable()` [EventBus]
- L25 [function] `private void OnDisable()` [EventBus]
- L30 [function] `private INoticeFeedPresenter RequirePresenter()` [DependencyInjection]
- L37 [type] `public struct NoticeFeedEvent` [EventBus]
- L41 [type] `public enum Grade` [EventBus]
- L47 [function] `public NoticeFeedEvent(string notice, Grade grade)` [EventBus]
- L53 [function] `public static void Trigger(string notice, Grade grade)` [EventBus]

### Assets/Scripts/UI/NoticeFeedItemFactory.cs

- L6 [type] `public interface INoticeFeedItemFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L8 [function] `GameObject Create(GameObject prefab)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L9 [function] `bool TryPrepare(GameObject noticeObject, Transform parent, NoticeFeedEvent notice, out TMP_Text text)` [DependencyInjection, SceneMutation]
- L10 [function] `void OnTake(GameObject noticeObject)` [SceneMutation]
- L11 [function] `void OnReturn(GameObject noticeObject)` [SceneMutation]
- L12 [function] `void DestroyItem(GameObject noticeObject)` [RuntimeObjectCreation, SceneMutation]
- L15 [type] `public sealed class NoticeFeedItemFactory : INoticeFeedItemFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L20 [function] `public NoticeFeedItemFactory(ITmpKoreanFontService tmpKoreanFontService)` [DependencyInjection]
- L26 [function] `public GameObject Create(GameObject prefab)` [DependencyInjection, RuntimeObjectCreation]
- L37 [function] `public bool TryPrepare(GameObject noticeObject, Transform parent, NoticeFeedEvent notice, out TMP_Text text)` [DependencyInjection, SceneMutation]
- L61 [function] `public void OnTake(GameObject noticeObject)` [SceneMutation]
- L69 [function] `public void OnReturn(GameObject noticeObject)` [SceneMutation]
- L77 [function] `public void DestroyItem(GameObject noticeObject)` [RuntimeObjectCreation, SceneMutation]
- L85 [function] `private static Color GetColor(NoticeFeedEvent.Grade grade)`

### Assets/Scripts/UI/NoticeFeedPresenter.cs

- L8 [type] `public interface INoticeFeedPresenter` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L10 [function] `void Present(GameObject prefab, Transform parent, NoticeFeedEvent notice)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L13 [type] `public sealed class NoticeFeedPresenter : INoticeFeedPresenter` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L24 [function] `public NoticeFeedPresenter(INoticeFeedItemFactory itemFactory)` [DependencyInjection]
- L30 [function] `public void Present(GameObject prefab, Transform parent, NoticeFeedEvent notice)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L58 [function] `private IObjectPool<GameObject> GetPool(GameObject prefab)` [RuntimeObjectCreation]
- L69 [function] `private IObjectPool<GameObject> CreatePool(GameObject prefab)` [DependencyInjection, RuntimeObjectCreation]

### Assets/Scripts/UI/SummeryInfo.cs

- L5 [type] `public abstract class SummeryInfo : UIPopUp`
- L7 [function] `public abstract void ShowInfo(IInfoable infoable)`

### Assets/Scripts/UI/TMPKoreanFont.cs

- L5 [type] `public interface ITmpKoreanFontProvider` [DependencyInjection]
- L7 [function] `TMP_FontAsset GetRequiredFont()` [DependencyInjection]
- L10 [type] `public interface ITmpKoreanFontService` [DependencyInjection]
- L12 [function] `TMP_FontAsset Resolve()` [DependencyInjection]
- L13 [function] `void Apply(TMP_Text text)` [DependencyInjection]
- L14 [function] `void ApplyToChildren(Transform root, bool includeInactive = true)` [DependencyInjection]
- L17 [type] `public sealed class TmpKoreanFontAssetProvider : ITmpKoreanFontProvider` [DependencyInjection]
- L21 [function] `public TmpKoreanFontAssetProvider(TMP_FontAsset font)` [DependencyInjection]
- L27 [function] `public TMP_FontAsset GetRequiredFont()` [DependencyInjection]
- L33 [type] `public sealed class TmpKoreanFontService : ITmpKoreanFontService` [DependencyInjection]
- L37 [function] `public TmpKoreanFontService(ITmpKoreanFontProvider fontProvider)` [DependencyInjection]
- L43 [function] `public TMP_FontAsset Resolve()` [DependencyInjection]
- L48 [function] `public void Apply(TMP_Text text)` [DependencyInjection]
- L59 [function] `public void ApplyToChildren(Transform root, bool includeInactive = true)` [DependencyInjection]

### Assets/Scripts/UI/TmpKoreanFontSettingsSO.cs

- L6 [type] `public sealed class TmpKoreanFontSettingsSO : ScriptableObject`
- L10 [function] `public TMP_FontAsset Font`
- L12 [function] `public TMP_FontAsset GetRequiredFont()`

### Assets/Scripts/UI/UIBase.cs

- L5 [type] `public class UIBase : MonoBehaviour`

### Assets/Scripts/UI/UIManager.cs

- L7 [type] `public class UIManager : SerializedMonoBehaviour`
- L18 [function] `public void Update()`
- L22 [function] `ClosePopupPeek()`
- L25 [function] `public void CloseAllPopup()`
- L29 [function] `ClosePopupPeek()`
- L32 [function] `public void OpenPopup(UIPopUp popup)`
- L38 [function] `public void ClosePopupPeek(UIPopUp popup)`
- L41 [function] `ClosePopupPeek()`
- L43 [function] `public void ClosePopupPeek()`
- L49 [function] `public void UpdateTime()`
- L53 [function] `private void UpdateHoldingMoneyText(int holdingMoney)`
- L57 [function] `private void UpdateGameSpeedText(int gameSpeed)`
- L61 [function] `public void MakeTouchFalse()`
- L66 [function] `public void MakeTouchTrue()`
- L71 [function] `private void OnEnable()`
- L78 [function] `private void OnDisable()`

### Assets/Scripts/UI/UIPopUp.cs

- L5 [type] `public class UIPopUp : UIBase`
- L7 [function] `public virtual void OnOpen()`
- L11 [function] `public virtual void OnClose()`
- L15 [function] `public virtual void ClosePopup()`

### Assets/Scripts/UI/UITab.cs

- L5 [type] `public class UITab : UIPopUp` [DependencyInjection, SceneMutation]
- L11 [function] `public void Construct(IUiPopupService popupService)` [DependencyInjection]
- L17 [function] `public bool ToggleTab(int id)` [DependencyInjection, SceneMutation]
- L29 [function] `public bool Toggle()` [DependencyInjection, SceneMutation]
- L43 [function] `public override void OnClose()` [SceneMutation]
- L47 [function] `public void CloseTab()` [DependencyInjection, SceneMutation]
- L53 [function] `private IUiPopupService ResolvePopupService()` [DependencyInjection]

### Assets/Scripts/UI/UITabContentTextProvider.cs

- L5 [type] `public interface IUITabContentTextProvider` [DependencyInjection]
- L10 [type] `public sealed class UITabContentTextProvider : IUITabContentTextProvider` [DependencyInjection]
- L20 [function] `public UITabContentTextProvider(IBuildingManagementSummaryService, IStaffWorkforceQueryService, IInvasionDefenseSummaryService, IOffenseTabSummaryService, IOperationTabSummaryService, IResearchCraftingSummaryService, ICodexRecordSummaryService)` [DependencyInjection]
- L45 [function] `public string Build(int id)` [DependencyInjection]
- L62 [function] `private string BuildBuildingManagementText()` [DependencyInjection]
- L80 [function] `private string BuildStaffManagementText()` [DependencyInjection]
- L115 [function] `private string BuildShopText()` [DependencyInjection]
- L133 [function] `private string BuildWarehouseText()` [DependencyInjection]
- L156 [function] `private string BuildOperationText()` [DependencyInjection]
- L183 [function] `private string BuildInvasionDefenseText()` [DependencyInjection]
- L212 [function] `private string BuildOffenseText()` [DependencyInjection]
- L240 [function] `private string BuildResearchCraftingText()` [DependencyInjection]
- L260 [function] `private string BuildCodexRecordText()` [DependencyInjection]

### Assets/Scripts/UI/UITabGeneratedPanelFactory.cs

- L7 [type] `public readonly struct GeneratedUITabPanel` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L9 [function] `public GeneratedUITabPanel(UITab tab, TMP_Text bodyText)` [DependencyInjection]
- L14 [function] `public UITab Tab { get; }` [DependencyInjection]
- L15 [function] `public TMP_Text BodyText { get; }` [DependencyInjection]
- L18 [type] `public interface IUITabGeneratedPanelFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L20 [function] `GeneratedUITabPanel Create(Transform parent, int id, string panelTitle)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L23 [type] `public interface IStaffWorkPriorityPanelFactory` [DependencyInjection, SceneMutation]
- L25 [function] `StaffWorkPriorityPanel Ensure(GameObject panelObject)` [DependencyInjection, SceneMutation]
- L28 [type] `public sealed class UITabGeneratedPanelFactory : IUITabGeneratedPanelFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L36 [function] `public UITabGeneratedPanelFactory(ITmpKoreanFontService tmpKoreanFontService, IObjectResolver objectResolver, IStaffWorkPriorityPanelFactory staffWorkPriorityPanelFactory)` [DependencyInjection]
- L49 [function] `public GeneratedUITabPanel Create(Transform parent, int id, string panelTitle)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L119 [function] `private TMP_Text CreateText(Transform parent, string name)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L129 [type] `public sealed class StaffWorkPriorityPanelFactory : IStaffWorkPriorityPanelFactory` [DependencyInjection, SceneMutation]
- L133 [function] `public StaffWorkPriorityPanelFactory(IObjectResolver objectResolver)` [DependencyInjection]
- L139 [function] `public StaffWorkPriorityPanel Ensure(GameObject panelObject)` [DependencyInjection, SceneMutation]
- L157 [function] `private static void DisablePlaceholderBody(GameObject panelObject)` [SceneMutation]

### Assets/Scripts/UI/UITabManager.cs

- L9 [type] `public class UITabManager : MonoBehaviour` [DependencyInjection, SceneMutation]
- L67 [function] `private void Start()` [DependencyInjection, SceneMutation]
- L73 [function] `public void Construct(IUITabContentTextProvider contentTextProvider, IUiPopupService popupService, ITmpKoreanFontService tmpKoreanFontService, IUITabGeneratedPanelFactory generatedPanelFactory, IStaffWorkPriorityPanelFactory staffWorkPriorityPanelFactory, IUITabTopButtonFactory topButtonFactory)` [DependencyInjection]
- L95 [function] `public void ToggleSelectButton(int category)` [DependencyInjection, SceneMutation]
- L118 [function] `public void ResgisterTab(UITab tab)` [SceneMutation]
- L123 [function] `public void UnRegisterTab(UITab tab)` [SceneMutation]
- L129 [function] `private void ConfigureTopTabs()` [DependencyInjection, SceneMutation]
- L164 [function] `private void EnsureHudCanvasRendersAboveWorld()` [SceneMutation]
- L177 [function] `private void RegisterExistingTabs()` [SceneMutation]
- L193 [function] `private void EnsureSpecializedTabContent()` [DependencyInjection, SceneMutation]
- L201 [function] `private void EnsureSpecializedTabContent(int id)` [DependencyInjection, SceneMutation]
- L214 [function] `private void EnsureSpecializedTabContent(UITab tab)` [DependencyInjection, SceneMutation]
- L228 [function] `private IEnumerable<UITab> GetAllTabs()` [SceneMutation]
- L237 [function] `private void CloseAllTabsImmediate()` [DependencyInjection, SceneMutation]
- L250 [function] `private void CloseAllTabsForInitialState()` [SceneMutation]
- L258 [function] `private static string GetPanelTitle(int id, string defaultTitle)`
- L271 [function] `private void OpenTabImmediate(UITab tab)` [DependencyInjection, SceneMutation]
- L279 [function] `private void BindTopButtons()` [SceneMutation]
- L305 [function] `private Transform ResolveButtonPanel()`
- L324 [function] `private static string GetButtonLabel(Button button)`
- L330 [function] `private static bool TryGetTopTabId(string label, out int tabId)`
- L336 [function] `private static string NormalizeTopTabLabel(string label)`
- L343 [function] `private void EnsureTopButtons()` [DependencyInjection, SceneMutation]
- L384 [function] `private void ArrangeTopButtonsInSingleRow(Transform root)` [DependencyInjection, SceneMutation]
- L394 [function] `private static void OrderTopButtons(Transform root)` [SceneMutation]
- L411 [function] `private static Button FindTopButtonForId(Transform root, int id)`
- L424 [function] `private void NormalizeTopButtons(Transform root)` [SceneMutation]
- L456 [function] `private void SetButtonLabel(Button button, string title)` [DependencyInjection]
- L465 [function] `private void PolishTopButton(Button button)` [DependencyInjection, SceneMutation]
- L478 [function] `private void EnsureGeneratedTab(int id, string title)` [DependencyInjection, SceneMutation]
- L492 [function] `private void RefreshGeneratedTab(int id)` [DependencyInjection, SceneMutation]
- L508 [function] `private string BuildTabContent(int id)` [DependencyInjection]
- L519 [function] `private IUiPopupService ResolvePopupService()` [DependencyInjection]
- L530 [function] `private ITmpKoreanFontService RequireTmpKoreanFontService()` [DependencyInjection]
- L537 [function] `private IUITabGeneratedPanelFactory RequireGeneratedPanelFactory()` [DependencyInjection]
- L544 [function] `private IStaffWorkPriorityPanelFactory RequireStaffWorkPriorityPanelFactory()` [DependencyInjection]
- L551 [function] `private IUITabTopButtonFactory RequireTopButtonFactory()` [DependencyInjection]

### Assets/Scripts/UI/UITabTopButtonFactory.cs

- L6 [type] `public interface IUITabTopButtonFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L8 [function] `Button CreateButton(Button template, Transform parent, int id, string title)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L9 [function] `HorizontalLayoutGroup EnsureSingleRowLayout(Transform root, float barHeight, params string[] rowNames)` [RuntimeObjectCreation, SceneMutation]
- L12 [type] `public sealed class UITabTopButtonFactory : IUITabTopButtonFactory` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L16 [function] `public UITabTopButtonFactory(ITmpKoreanFontService tmpKoreanFontService)` [DependencyInjection]
- L22 [function] `public Button CreateButton(Button template, Transform parent, int id, string title)` [DependencyInjection, RuntimeObjectCreation, SceneMutation]
- L41 [function] `public HorizontalLayoutGroup EnsureSingleRowLayout(Transform root, float barHeight, params string[] rowNames)` [RuntimeObjectCreation, SceneMutation]
- L85 [function] `private void SetLabel(Button button, string title)` [DependencyInjection]
- L97 [function] `private static void MoveRowChildrenBackToRoot(Transform root, string rowName)` [SceneMutation]
- L118 [function] `private static string SanitizeName(string title)`

### Assets/Scripts/UI/UiPopupService.cs

- L3 [type] `public interface IUiPopupService` [DependencyInjection]
- L10 [type] `public interface IUiTouchGuardService` [DependencyInjection]
- L16 [type] `public sealed class UiPopupService : IUiPopupService` [DependencyInjection]
- L20 [function] `public UiPopupService(DungeonSceneRuntimeReferences sceneReferences)` [DependencyInjection]
- L25 [function] `public void CloseAll()` [DependencyInjection]
- L30 [function] `public void Open(UIPopUp popup)` [DependencyInjection]
- L35 [function] `public void ClosePeek(UIPopUp popup)` [DependencyInjection]
- L40 [function] `private UIManager ResolveManager()` [DependencyInjection]
- L47 [type] `public sealed class UiTouchGuardService : IUiTouchGuardService` [DependencyInjection]
- L51 [function] `public UiTouchGuardService(DungeonSceneRuntimeReferences sceneReferences)` [DependencyInjection]
- L56 [function] `public void BlockTouch()` [DependencyInjection]
- L61 [function] `public void ReleaseTouch()` [DependencyInjection]
- L66 [function] `private UIManager ResolveManager()` [DependencyInjection]

### Assets/Scripts/UI/WorldInfoClickSelector.cs

- L4 [type] `public interface IWorldInfoClickSelector` [DependencyInjection]
- L12 [type] `public sealed class WorldInfoClickSelectionService : IWorldInfoClickSelector` [DependencyInjection, SceneMutation]
- L19 [function] `public WorldInfoClickSelectionService(IGridSystemProvider gridSystemProvider, IMainCameraProvider mainCameraProvider)` [DependencyInjection]
- L27 [function] `public bool TryTriggerCharacterUnderPointer()` [DependencyInjection, SceneMutation]
- L43 [function] `public bool TryGetPreferredCharacterUnderPointer(out CharacterActor actor)` [DependencyInjection, SceneMutation]
- L48 [function] `public bool TryGetPreferredCharacterAtScreenPosition(Vector3 screenPosition, Camera camera, out CharacterActor actor)` [DependencyInjection, SceneMutation]
- L63 [function] `public bool TryGetPreferredCharacter(Collider2D[] hits, out CharacterActor actor)` [SceneMutation]
- L102 [function] `private bool CanSelectWorldInfo()` [DependencyInjection]
- L107 [function] `private static int GetCharacterClickPriority(CharacterActor actor)` [SceneMutation]
- L134 [function] `private void TriggerCharacter(CharacterActor actor)` [SceneMutation]


## Assets\Scripts\Utils

### Assets/Scripts/Utils/DataScriptableObject.cs

- L4 [type] `public class DataScriptableObject : SerializedScriptableObject`

### Assets/Scripts/Utils/EventObserver.cs

- L7 [type] `public static class EventObserver` [Reflection, EventBus]
- L11 [function] `static EventObserver()` [Reflection, EventBus]
- L62 [function] `private static bool SubscriptionExists(Type type, UtilEventListenerBase receiver)` [Reflection, EventBus]
- L82 [type] `public interface UtilEventListenerBase { };` [Reflection, EventBus]
- L83 [type] `public interface UtilEventListener<T> : UtilEventListenerBase` [Reflection, EventBus]
- L85 [function] `void OnTriggerEvent(T eventType)` [Reflection, EventBus]
- L87 [type] `public static class EventRegister` [Reflection, EventBus]
- L101 [type] `public class EventListenerWrapper<TOwner, TTarget, TEvent> : UtilEventListener<TEvent>, IDisposable` [Reflection, EventBus]
- L107 [function] `public EventListenerWrapper(TOwner owner, Action<TTarget> callback)` [Reflection, EventBus]
- L111 [function] `RegisterCallbacks(true)` [Reflection, EventBus]
- L114 [function] `public void Dispose()` [Reflection, EventBus]
- L116 [function] `RegisterCallbacks(false)` [Reflection, EventBus]
- L120 [function] `protected virtual TTarget OnEvent(TEvent eventType)` [Reflection, EventBus]
- L121 [function] `public void OnTriggerEvent(TEvent eventType)` [Reflection, EventBus]
- L127 [function] `private void RegisterCallbacks(bool b)` [Reflection, EventBus]
- L139 [type] `public struct GameEvent` [Reflection, EventBus]
- L142 [function] `public GameEvent(string newName)` [Reflection, EventBus]
- L147 [function] `public static void Trigger(string newName)` [Reflection, EventBus]
