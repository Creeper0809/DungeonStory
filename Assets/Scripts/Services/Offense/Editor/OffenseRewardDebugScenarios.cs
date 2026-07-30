using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

public static class OffenseRewardDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Offense/Run P3 Reward Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P3 offense reward scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();

        RunScenario("보상 서비스 돈/재고/실물 귀환 지급", VerifyMoneyStockAndStateRewards, errors);
        RunScenario("희귀 시설과 설계도 지급", VerifyRareFacilityAndBlueprintRewards, errors);
        RunScenario("지정 전략 설계도만 지급", VerifySpecificStrategyBlueprintReward, errors);
        RunScenario("원정 완료 시 보상 지급 연결", VerifyExpeditionCompletionGrantsRewards, errors);
        RunScenario("보상 핸들러 개방형 확장", VerifyOpenRewardHandlerExtension, errors);
        RunScenario("방어 설계도 판정 capability 확장", VerifyDefenseBlueprintUnlockCapability, errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("P3 offense reward scenarios passed.");
        }

        return true;
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (scenario()) return;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        errors.Add(name);
    }

    private static bool VerifyMoneyStockAndStateRewards()
    {
        using ScenarioContext context = new ScenarioContext(100);
        int warehouseFoodBefore = context.Warehouse.Inventory.GetStock(StockCategory.Food);
        int physicalFoodBefore = GetPhysicalStock(StockCategory.Food);
        int physicalLootBefore = GetPhysicalItem(OffenseLootItemIds.UnappraisedLoot);
        IReadOnlyList<OffenseRewardGrantResult> results = CreateGrantService().GrantRewards(
            new[]
            {
                Reward(new OffenseMoneyRewardSpec(), "약탈금", 80),
                Reward(new OffenseStockRewardSpec(StockCategory.Food), "무기처럼 보이는 이름", 40),
                Reward(new OffenseRecruitCandidateRewardSpec(), "직원 후보", 1),
                Reward(new OffensePrisonerRewardSpec(), "포로", 1),
                Reward(new OffenseSpecialMonsterRewardSpec(), "특수 동물", 1)
            },
            context.CreateRewardContext());
        int warehouseFoodDelta = context.Warehouse.Inventory.GetStock(StockCategory.Food) - warehouseFoodBefore;
        int physicalFoodDelta = GetPhysicalStock(StockCategory.Food) - physicalFoodBefore;
        int physicalLootDelta =
            GetPhysicalItem(OffenseLootItemIds.UnappraisedLoot) - physicalLootBefore;

        bool valid = results.Count == 5
            && results.All((result) => result.success)
            && context.GameData.holdingMoney.Value == 100
            && warehouseFoodDelta + physicalFoodDelta == 40
            && physicalLootDelta == 80
            && context.RewardState.MoneyEarned == 80
            && context.ReturnArrivals.PrisonerCount == 1
            && context.ReturnArrivals.WildlifeCount == 1;
        if (!valid)
        {
            throw new InvalidOperationException(
                $"Reward diagnostic: results={results.Count}, " +
                $"success={results.Count(result => result.success)}, " +
                $"money={context.GameData.holdingMoney.Value}, " +
                $"warehouseFoodDelta={warehouseFoodDelta}, physicalFoodDelta={physicalFoodDelta}, " +
                $"physicalLootDelta={physicalLootDelta}, " +
                $"moneyState={context.RewardState.MoneyEarned}, " +
                $"prisoners={context.ReturnArrivals.PrisonerCount}, " +
                $"wildlife={context.ReturnArrivals.WildlifeCount}");
        }

        return valid;
    }

    private static bool VerifyRareFacilityAndBlueprintRewards()
    {
        using ScenarioContext context = new ScenarioContext(0);
        IReadOnlyList<OffenseRewardGrantResult> results = CreateGrantService().GrantRewards(
            new[]
            {
                Reward(new OffenseRareFacilityRewardSpec(), "희귀 시설", 1),
                Reward(new OffenseSpecialBlueprintRewardSpec(), "특수 설계도", 1)
            },
            context.CreateRewardContext());

        return results.Count == 2
            && results.All((result) => result.success)
            && context.RewardState.RareFacilityBuildingIds.Count == 1
            && context.RewardState.RareFacilityBuildingIds
                .All(context.ResearchState.IsBuildingUnlocked)
            && context.RewardState.AcquiredBlueprintIds.Count == 1
            && context.ResearchState.Tasks.Count == 0;
    }

    private static bool VerifyOpenRewardHandlerExtension()
    {
        IOffenseRewardGrantService service = new OffenseRewardGrantService(
            new OffenseRewardSelector(new EditorOffenseRewardCatalog()),
            OffenseRewardGrantHandlers.CreateDefaults()
                .Concat(new IOffenseRewardGrantHandler[] { new TestRewardHandler() }));
        IReadOnlyList<OffenseRewardGrantResult> results = service.GrantRewards(
            new[] { new OffenseRewardPreview("custom", 3, new TestRewardSpec()) },
            new OffenseRewardContext());

        return results.Count == 1
            && results[0].success
            && results[0].grantedAmount == 3
            && results[0].detail == "custom handler";
    }

    private static bool VerifySpecificStrategyBlueprintReward()
    {
        using ScenarioContext context = new ScenarioContext(0);
        IReadOnlyList<OffenseTargetDefinition> targets = OffenseWorldMapService.CreateDefaultTargets();
        OffenseRewardPreview[] strategyRewards = new[]
        {
            "merchant_road",
            "old_armory",
            "mana_ruins"
        }
            .Select(targetId => targets.First(target => target.id == targetId))
            .SelectMany(target => target.rewards)
            .Where(reward => reward?.GrantSpec is OffenseSpecificBlueprintRewardSpec)
            .ToArray();
        IReadOnlyList<OffenseRewardGrantResult> results = CreateGrantService().GrantRewards(
            strategyRewards,
            context.CreateRewardContext());

        return results.Count == 3
            && results.All(result => result.success)
            && context.RewardState.AcquiredBlueprintIds.Count == 3
            && context.RewardState.AcquiredBlueprintIds.Contains(OffenseStrategyBlueprintIds.CommerceLogistics)
            && context.RewardState.AcquiredBlueprintIds.Contains(OffenseStrategyBlueprintIds.FortressDefense)
            && context.RewardState.AcquiredBlueprintIds.Contains(OffenseStrategyBlueprintIds.ArcaneResearch)
            && context.ResearchState.Tasks.Count == 0;
    }

    private static bool VerifyDefenseBlueprintUnlockCapability()
    {
        BuildingSO defenseBuilding = ScriptableObject.CreateInstance<BuildingSO>();
        FacilityBlueprintSO blueprint = ScriptableObject.CreateInstance<FacilityBlueprintSO>();
        try
        {
            defenseBuilding.id = 91234;
            defenseBuilding.Defense = new DefenseFacilityData
            {
                enabled = true,
                concept = DefenseAttackConcept.Physical
            };
            blueprint.unlocks.Add(new TestBuildingUnlock(defenseBuilding.id));

            return new OffenseDefenseBlueprintRewardSpec().IsEligible(
                blueprint,
                new[] { defenseBuilding });
        }
        finally
        {
            Object.DestroyImmediate(defenseBuilding);
            Object.DestroyImmediate(blueprint);
        }
    }

    private static bool VerifyExpeditionCompletionGrantsRewards()
    {
        using ExpeditionRewardScenario scenario = new ExpeditionRewardScenario();
        using CountingRewardGrantedListener rewardEvents =
            new CountingRewardGrantedListener(scenario.GameEvents);
        CharacterActor worker = scenario.CreateCharacter("RewardWorker", CharacterType.NPC, CharacterRole.Regular, 100);
        worker.ApplyDamage(20f, "원정 전 부상");

        int warehouseFoodBefore = scenario.Context.Warehouse.Inventory.GetStock(StockCategory.Food);
        int physicalFoodBefore = GetPhysicalStock(StockCategory.Food);
        int physicalLootBefore = GetPhysicalItem(OffenseLootItemIds.UnappraisedLoot);

        bool started = scenario.Expedition.Runtime.TryStartExpedition(
            "food_farm",
            new[] { CharacterActor.From(worker) },
            out OffenseExpeditionRun expedition,
            out _);
        bool journeyCompleted = started && CompleteJourney(scenario, expedition);

        OffenseExpeditionResult result = scenario.Expedition.Runtime.ResultHistory.FirstOrDefault();
        bool completed = result != null && !scenario.Battle.HasActiveBattle;
        int warehouseFoodDelta = scenario.Context.Warehouse.Inventory.GetStock(StockCategory.Food) - warehouseFoodBefore;
        int physicalFoodDelta = GetPhysicalStock(StockCategory.Food) - physicalFoodBefore;
        int physicalLootDelta =
            GetPhysicalItem(OffenseLootItemIds.UnappraisedLoot) - physicalLootBefore;
        OffenseStrategicPressureSnapshot pressure =
            scenario.Reward.Regions.GetFactionPressure(OffenseRegionRuntime.HumanFactionId);

        bool valid = started
            && journeyCompleted
            && completed
            && result != null
            && result.success
            && result.grantedRewards.Count == 3
            && result.grantedRewards.All((reward) => reward.success)
            && rewardEvents.Count == 1
            && object.ReferenceEquals(rewardEvents.LastEvent.expeditionResult, result)
            && rewardEvents.LastEvent.grantResults.Count == result.grantedRewards.Count
            && scenario.Context.GameData.holdingMoney.Value == 0
            && warehouseFoodDelta + physicalFoodDelta == 40
            && physicalLootDelta == 80
            && scenario.Reward.Runtime.State.MoneyEarned == 80
            && Mathf.Approximately(pressure.Logistics, 15f)
            && worker.CurrentHealth < worker.MaxHealth
            && !worker.LogComponent.ActivityEntries.Any(activity =>
                activity.ActionId == "offense:victory-recovery");
        if (!valid)
        {
            throw new InvalidOperationException(
                $"Expedition reward diagnostic: started={started}, journeyCompleted={journeyCompleted}, " +
                $"active={scenario.Expedition.Runtime.ActiveExpeditions.Count}, battle={scenario.Battle.HasActiveBattle}, " +
                $"result={(result == null ? "null" : result.success.ToString())}, grants={result?.grantedRewards.Count ?? -1}, " +
                $"eventCount={rewardEvents.Count}, money={scenario.Context.GameData.holdingMoney.Value}, " +
                $"foodWarehouseDelta={warehouseFoodDelta}, foodPhysicalDelta={physicalFoodDelta}, " +
                $"physicalLootDelta={physicalLootDelta}, " +
                $"logisticsPressure={pressure.Logistics:0.##}, " +
                $"health={worker.CurrentHealth:0.##}/{worker.MaxHealth:0.##}");
        }

        return valid;
    }

    private static bool CompleteJourney(ExpeditionRewardScenario scenario, OffenseExpeditionRun expedition)
    {
        int safety = 0;
        while (scenario.Expedition.Runtime.ActiveExpeditions.Count > 0 && safety++ < 100)
        {
            switch (expedition.Phase)
            {
                case OffenseExpeditionPhase.ChoosingRoute:
                    OffenseRouteNode next = expedition.GetAvailableRouteNodes().FirstOrDefault();
                    if (next == null
                        || !scenario.Expedition.Runtime.TryChooseRouteNode(expedition.ExpeditionId, next.Id, out _))
                    {
                        return false;
                    }
                    break;

                case OffenseExpeditionPhase.ResolvingNode:
                    if (!scenario.Expedition.Runtime.TryResolveCurrentNode(
                        expedition.ExpeditionId,
                        useSupply: false,
                        out _,
                        out _))
                    {
                        return false;
                    }
                    break;

                case OffenseExpeditionPhase.InBattle:
                    if (!CompleteCurrentBattle(scenario.Battle)) return false;
                    break;

                case OffenseExpeditionPhase.Completed:
                case OffenseExpeditionPhase.Defeated:
                case OffenseExpeditionPhase.Retreated:
                    return scenario.Expedition.Runtime.ActiveExpeditions.Count == 0;
            }
        }

        return scenario.Expedition.Runtime.ActiveExpeditions.Count == 0;
    }

    private static bool CompleteCurrentBattle(OffenseBattleRuntime battle)
    {
        int safety = 0;
        while (battle.HasActiveBattle && safety++ < 40)
        {
            OffenseBattleCombatant enemy = battle.Session.Combatants
                .FirstOrDefault(combatant => combatant.Team == OffenseBattleTeam.Enemies && !combatant.IsDead);
            if (enemy == null || battle.Session.CurrentActor?.Team != OffenseBattleTeam.Allies)
            {
                return false;
            }

            if (!battle.TryIssuePlayerCommand(
                OffenseBattleActionType.BasicAttack,
                enemy.PersistentId,
                string.Empty,
                out _))
            {
                return false;
            }
        }

        return !battle.HasActiveBattle;
    }

    private static OffenseRewardPreview Reward(
        OffenseRewardGrantSpec grantSpec,
        string label,
        int amount)
    {
        return new OffenseRewardPreview(label, amount, grantSpec);
    }

    private static IOffenseRewardGrantService CreateGrantService()
    {
        TryResolvePhysicalItemServices(
            out IWorldItemStackRuntime itemRuntime,
            out IWorldDropZoneQuery dropZoneQuery);
        return new OffenseRewardGrantService(
            new OffenseRewardSelector(new EditorOffenseRewardCatalog()),
            OffenseRewardGrantHandlers.CreateDefaults(itemRuntime, dropZoneQuery));
    }

    private static int GetPhysicalStock(StockCategory category)
    {
        DungeonRuntimeLifetimeScope scope = Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
            FindObjectsInactive.Include);
        if (scope == null || scope.Container == null)
        {
            return 0;
        }

        IWorldItemStackRuntime itemRuntime = scope.Container.Resolve<IWorldItemStackRuntime>();
        string itemId = DungeonItemCatalogSO.StockItemId(category);
        return itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
    }

    private static int GetPhysicalItem(string itemId)
    {
        TryResolvePhysicalItemServices(
            out IWorldItemStackRuntime itemRuntime,
            out _);
        return itemRuntime?.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal))
            .Sum(stack => stack.Quantity) ?? 0;
    }

    private static bool TryResolvePhysicalItemServices(
        out IWorldItemStackRuntime itemRuntime,
        out IWorldDropZoneQuery dropZoneQuery)
    {
        DungeonRuntimeLifetimeScope scope = Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
            FindObjectsInactive.Include);
        if (scope == null || scope.Container == null)
        {
            itemRuntime = null;
            dropZoneQuery = null;
            return false;
        }

        itemRuntime = scope.Container.Resolve<IWorldItemStackRuntime>();
        dropZoneQuery = scope.Container.Resolve<IWorldDropZoneQuery>();
        return itemRuntime != null && dropZoneQuery != null;
    }

    private static GameData CreateGameData(int holdingMoney)
    {
        GameData gameData = ScriptableObject.CreateInstance<GameData>();
        gameData.holdingMoney = new Data<int>();
        gameData.holdingMoney.Initialize(holdingMoney);
        return gameData;
    }

    private sealed class TestRewardSpec : OffenseRewardGrantSpec
    {
        public override string RewardTypeId => "offense.reward.test";
        public override OffenseRewardCategory Category => OffenseRewardCategory.Money;
    }

    private sealed class TestRewardHandler : IOffenseRewardGrantHandler
    {
        public string RewardTypeId => "offense.reward.test";

        public OffenseRewardGrantResult Grant(
            OffenseRewardPreview reward,
            OffenseRewardContext context,
            IOffenseRewardSelector selector)
        {
            return OffenseRewardGrantResultFactory.Success(reward, reward.amount, "custom handler");
        }
    }

    [Serializable]
    private sealed class TestBuildingUnlock : BlueprintUnlock, IBlueprintBuildingUnlock
    {
        public TestBuildingUnlock(int buildingId)
        {
            BuildingId = buildingId;
        }

        public int BuildingId { get; }
        public override string UnlockTypeId => "blueprint.test-building";
        public override bool IsConfigured => BuildingId >= 0;

        public override BlueprintUnlockRecord Apply(BlueprintUnlockContext context)
        {
            return default;
        }
    }

    private sealed class CountingRewardGrantedListener : IDisposable
    {
        private readonly IDisposable subscription;

        public CountingRewardGrantedListener(DungeonStory.Foundation.IGameEventBus gameEventBus)
        {
            subscription = gameEventBus.Subscribe<OffenseRewardGrantedEvent>(OnTriggerEvent);
        }

        public int Count { get; private set; }
        public OffenseRewardGrantedEvent LastEvent { get; private set; }

        public void OnTriggerEvent(OffenseRewardGrantedEvent eventType)
        {
            Count++;
            LastEvent = eventType;
        }

        public void Dispose()
        {
            subscription.Dispose();
        }
    }

    private sealed class ScenarioContext : IDisposable
    {
        public ScenarioContext(int holdingMoney)
        {
            GameData = CreateGameData(holdingMoney);
            Warehouse = new TestWarehouse(500);
            ShopUnlockState = new FacilityShopUnlockState();
            ResearchState = new BlueprintResearchState();
            RewardState = new OffenseRewardState();
            ReturnArrivals = new TestReturnArrivalRuntime();
        }

        public GameData GameData { get; }
        public TestWarehouse Warehouse { get; }
        public FacilityShopUnlockState ShopUnlockState { get; }
        public BlueprintResearchState ResearchState { get; }
        public OffenseRewardState RewardState { get; }
        public TestReturnArrivalRuntime ReturnArrivals { get; }

        public OffenseRewardContext CreateRewardContext(OffenseTargetDefinition target = null)
        {
            return new OffenseRewardContext
            {
                gameData = GameData,
                warehouses = new[] { Warehouse },
                shopUnlockState = ShopUnlockState,
                researchState = ResearchState,
                rewardState = RewardState,
                returnArrivalRuntime = ReturnArrivals,
                expeditionId = "reward-debug-expedition",
                target = target
            };
        }

        public void Dispose()
        {
            if (GameData != null)
            {
                Object.DestroyImmediate(GameData);
            }
        }
    }

    private sealed class TestWarehouse : IWarehouseFacility
    {
        public TestWarehouse(int capacity)
        {
            Inventory = new WarehouseInventory(capacity);
        }

        public WarehouseInventory Inventory { get; }
        public bool HasWarehouseInventory => true;
    }

    private sealed class TestReturnArrivalRuntime : IOffenseReturnArrivalRuntime
    {
        public int PrisonerCount { get; private set; }
        public int WildlifeCount { get; private set; }
        public IReadOnlyList<OffenseReturnArrivalState> Arrivals =>
            Array.Empty<OffenseReturnArrivalState>();

        public void BeginExpeditionReturn(string expeditionId) { }
        public void RegisterReturningMember(string expeditionId) { }
        public void CompleteReturningMember(string expeditionId) { }
        public void SealExpeditionReturn(string expeditionId) { }

        public int QueueArrival(
            string expeditionId,
            string targetId,
            OffenseReturnArrivalKind kind,
            int amount)
        {
            int safeAmount = Mathf.Max(0, amount);
            if (kind == OffenseReturnArrivalKind.Prisoner)
            {
                PrisonerCount += safeAmount;
            }
            else
            {
                WildlifeCount += safeAmount;
            }
            return safeAmount;
        }

        public DungeonOffenseReturnArrivalSaveData Capture()
        {
            return new DungeonOffenseReturnArrivalSaveData();
        }

        public void Restore(
            DungeonOffenseReturnArrivalSaveData saveData,
            DungeonGameRestoreReport report = null)
        {
        }
    }

    private sealed class EditorOffenseRewardCatalog : IOffenseRewardCatalog
    {
        private IReadOnlyCollection<BuildingSO> buildings;
        private IReadOnlyCollection<FacilityBlueprintSO> blueprints;

        public IReadOnlyCollection<BuildingSO> Buildings => buildings ??= LoadAssets<BuildingSO>();
        public IReadOnlyCollection<FacilityBlueprintSO> Blueprints => blueprints ??= LoadAssets<FacilityBlueprintSO>();

        private static IReadOnlyCollection<T> LoadAssets<T>()
            where T : Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where((asset) => asset != null)
                .ToArray();
        }
    }

    private sealed class ExpeditionRewardScenario : IDisposable
    {
        private readonly List<Object> objects = new List<Object>();

        public ScenarioContext Context { get; }
        public WorldMapFixture WorldMap { get; }
        public ExpeditionFixture Expedition { get; }
        public RewardFixture Reward { get; }
        public OffenseBattleRuntime Battle { get; }
        public DungeonStory.Foundation.IGameEventBus GameEvents { get; }

        public ExpeditionRewardScenario()
        {
            GameEvents = new DungeonStory.Foundation.GameEventBus();
            Context = new ScenarioContext(0);
            WorldMap = new WorldMapFixture(GameEvents);
            Reward = new RewardFixture(Context);
            Battle = new OffenseBattleRuntime(
                new TestCharacterSaveService(),
                new MissingRunVariableProvider(),
                GameEvents);
            Expedition = new ExpeditionFixture(
                WorldMap.Runtime,
                Reward.Runtime,
                GameEvents,
                Battle);
        }

        public CharacterActor CreateCharacter(
            string name,
            CharacterType type,
            CharacterRole role,
            int statValue)
        {
            CharacterSO data = ScriptableObject.CreateInstance<CharacterSO>();
            data.id = 9800 + objects.Count;
            data.characterName = name;
            data.characterType = type;
            data.role = role;
            data.speciesTag = "Orc";
            data.baseStats = CharacterStatBlock.CreateDefault(statValue);
            data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();

            GameObject obj = new GameObject(name);
            objects.Add(obj);
            objects.Add(data);
            obj.AddComponent<SpriteRenderer>();
            CharacterActor character = obj.AddComponent<CharacterActor>();
            obj.AddComponent<AbilityMove>();
            obj.AddComponent<AbilityWork>();
            AIBrain brain = obj.AddComponent<AIBrain>();
            brain.availableActions = type == CharacterType.Customer
                ? AiDebugScenarioActionFactory.CreateCustomerActions()
                : AiDebugScenarioActionFactory.CreateStaffActions();
            CharacterAiEditorTestDependencies.Inject(obj);
            character.RefreshAbilityCache();
            character.Initialization(data);
            character.SetLifecycleState(CharacterLifecycleState.Active);
            character.stats[CharacterCondition.SLEEP] = 100f;
            character.stats[CharacterCondition.MOOD] = 100f;
            return character;
        }

        public void Dispose()
        {
            Expedition.Dispose();
            Reward.Dispose();
            WorldMap.Dispose();
            Context.Dispose();
            foreach (Object obj in objects.Where((item) => item != null))
            {
                Object.DestroyImmediate(obj);
            }
        }
    }

    private sealed class WorldMapFixture : IDisposable
    {
        private readonly GameObject obj;

        public OffenseWorldMapRuntime Runtime { get; }

        public WorldMapFixture(DungeonStory.Foundation.IGameEventBus gameEventBus)
        {
            obj = new GameObject("Offense Reward World Map Fixture");
            Runtime = obj.AddComponent<OffenseWorldMapRuntime>();
            Runtime.Construct(new EmptyPanelService(), gameEventBus);
            Runtime.StartWorldMap();
        }

        public void Dispose()
        {
            Object.DestroyImmediate(obj);
        }
    }

    private sealed class ExpeditionFixture : IDisposable
    {
        private readonly GameObject obj;

        public OffenseExpeditionRuntime Runtime { get; }

        public ExpeditionFixture(
            OffenseWorldMapRuntime worldMap,
            OffenseRewardRuntime rewards,
            DungeonStory.Foundation.IGameEventBus gameEventBus)
            : this(worldMap, rewards, gameEventBus, null)
        {
        }

        public ExpeditionFixture(
            OffenseWorldMapRuntime worldMap,
            OffenseRewardRuntime rewards,
            DungeonStory.Foundation.IGameEventBus gameEventBus,
            IOffenseBattleRuntime battleRuntime)
        {
            obj = new GameObject("Offense Reward Expedition Fixture");
            Runtime = obj.AddComponent<OffenseExpeditionRuntime>();
            Runtime.Construct(
                new EmptyExpeditionMemberQuery(),
                new WorldMapRuntimeProvider(worldMap),
                new RewardRuntimeProvider(rewards),
                new EmptyMetaProgressionProvider(),
                new EmptyPanelService(),
                gameEventBus,
                battleRuntime ?? throw new ArgumentNullException(nameof(battleRuntime)));
        }

        public void Dispose()
        {
            Object.DestroyImmediate(obj);
        }
    }

    private sealed class MissingRunVariableProvider : IRunVariableRuntimeProvider
    {
        public bool TryGetRuntime(out RunVariableRuntime runtime)
        {
            runtime = null;
            return false;
        }
    }

    private sealed class TestCharacterSaveService : ICharacterWorldSaveService
    {
        private readonly Dictionary<CharacterActor, string> ids = new Dictionary<CharacterActor, string>();
        private int nextId;

        public DungeonCharacterWorldSaveData Capture(Grid grid) => new DungeonCharacterWorldSaveData();
        public void PrepareForWorldRestore() { }
        public int Restore(Grid grid, DungeonCharacterWorldSaveData source, DungeonGameRestoreReport report) => 0;

        public bool TryGetPersistentId(CharacterActor actor, out string persistentId)
        {
            return ids.TryGetValue(actor, out persistentId);
        }

        public string GetOrAssignPersistentId(CharacterActor actor)
        {
            if (!ids.TryGetValue(actor, out string persistentId))
            {
                persistentId = $"staff:test:{nextId++:D3}";
                ids[actor] = persistentId;
            }

            return persistentId;
        }

        public bool TryGetRestoredActor(string persistentId, out CharacterActor actor)
        {
            actor = ids.FirstOrDefault(pair => pair.Value == persistentId).Key;
            return actor != null;
        }
    }

    private sealed class EmptyExpeditionMemberQuery : IOffenseExpeditionMemberQuery
    {
        public IReadOnlyList<CharacterActor> GetAvailableMemberActors() => Array.Empty<CharacterActor>();
    }

    private sealed class WorldMapRuntimeProvider : IOffenseWorldMapRuntimeProvider
    {
        private readonly OffenseWorldMapRuntime runtime;

        public WorldMapRuntimeProvider(OffenseWorldMapRuntime runtime)
        {
            this.runtime = runtime;
        }

        public bool TryGetRuntime(out OffenseWorldMapRuntime resolved)
        {
            resolved = runtime;
            return resolved != null;
        }
    }

    private sealed class RewardRuntimeProvider : IOffenseRewardRuntimeProvider
    {
        private readonly OffenseRewardRuntime runtime;

        public RewardRuntimeProvider(OffenseRewardRuntime runtime)
        {
            this.runtime = runtime;
        }

        public bool TryGetRuntime(out OffenseRewardRuntime resolved)
        {
            resolved = runtime;
            return resolved != null;
        }
    }

    private sealed class EmptyMetaProgressionProvider : IMetaProgressionRuntimeProvider
    {
        public bool TryGetRuntime(out MetaProgressionRuntime runtime)
        {
            runtime = null;
            return false;
        }
    }

    private sealed class EmptyPanelService : IOffensePanelService
    {
        public OffenseWorldMapPanel ShowWorldMap(OffenseWorldMapRuntime runtime) => null;
        public OffenseExpeditionPanel ShowExpedition(OffenseExpeditionRuntime runtime) => null;
    }

    private sealed class RewardFixture : IDisposable
    {
        private readonly GameObject obj;

        public OffenseRewardRuntime Runtime { get; }
        public OffenseRegionRuntime Regions { get; }

        public RewardFixture(ScenarioContext context)
        {
            obj = new GameObject("Offense Reward Runtime Fixture");
            Runtime = obj.AddComponent<OffenseRewardRuntime>();
            Regions = new OffenseRegionRuntime();
            EmptyRewardContextDependencies dependencies = new EmptyRewardContextDependencies();
            Runtime.Construct(
                new OffenseRewardContextBuilder(
                    dependencies,
                    dependencies,
                    dependencies,
                    dependencies,
                    Regions),
                CreateGrantService());
            Runtime.SetDebugContext(
                context.GameData,
                new[] { context.Warehouse },
                context.ShopUnlockState,
                context.ResearchState);
        }

        public void Dispose()
        {
            Runtime.ClearDebugContext();
            Object.DestroyImmediate(obj);
        }
    }

    private sealed class EmptyRewardContextDependencies :
        IBlueprintResearchRuntimeProvider,
        IDailyFacilityShopRuntimeProvider,
        IGameDataProvider,
        IWarehouseWorldQuery
    {
        public int WarehouseVersion => 0;
        public IReadOnlyList<IWarehouseFacility> Warehouses => Array.Empty<IWarehouseFacility>();

        public bool TryGetRuntime(out BlueprintResearchRuntime runtime)
        {
            runtime = null;
            return false;
        }

        public bool TryGetRuntime(out DailyFacilityShopRuntime runtime)
        {
            runtime = null;
            return false;
        }

        public bool TryGetGameData(out GameData gameData)
        {
            gameData = null;
            return false;
        }
    }
}
