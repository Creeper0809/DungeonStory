public readonly struct BuildingConditionContext : IBuildingConditionContextPort
{
    public static readonly BuildingConditionContext Empty =
        new BuildingConditionContext(
            null,
            null,
            null,
            DisabledDungeonDebugRuleQuery.Instance,
            null);

    public BuildingConditionContext(GameSessionState gameData)
        : this(gameData, null, null, DisabledDungeonDebugRuleQuery.Instance, null)
    {
    }

    public BuildingConditionContext(GameSessionState gameData, IBuildingUnlockStateView buildingUnlockState)
        : this(
            gameData,
            buildingUnlockState,
            null,
            DisabledDungeonDebugRuleQuery.Instance,
            null)
    {
    }

    public BuildingConditionContext(
        GameSessionState gameData,
        IBuildingUnlockStateView buildingUnlockState,
        IGameMoneyAccount moneyAccount)
        : this(
            gameData,
            buildingUnlockState,
            moneyAccount,
            DisabledDungeonDebugRuleQuery.Instance,
            null)
    {
    }

    public BuildingConditionContext(
        GameSessionState gameData,
        IBuildingUnlockStateView buildingUnlockState,
        IGameMoneyAccount moneyAccount,
        IDungeonDebugRuleQuery debugRules,
        IRunMilestoneQuery milestoneQuery = null)
    {
        GameSessionState = gameData;
        BuildingUnlockState = buildingUnlockState;
        MoneyAccount = moneyAccount;
        DebugRules = debugRules ?? throw new System.ArgumentNullException(nameof(debugRules));
        MilestoneQuery = milestoneQuery;
    }

    public GameSessionState GameSessionState { get; }
    public IBuildingUnlockStateView BuildingUnlockState { get; }
    public IGameMoneyAccount MoneyAccount { get; }
    public IDungeonDebugRuleQuery DebugRules { get; }
    public IRunMilestoneQuery MilestoneQuery { get; }
    public bool ShouldSkipConstructionCosts =>
        DebugRules.ShouldSkipCosts()
        || DebugRules.IsEnabled(DungeonDebugCheat.FreeConstruction);

    public bool CanSpendConstruction(int amount) =>
        MoneyAccount != null && MoneyAccount.CanSpend(amount);

    public bool TrySpendConstruction(int amount)
    {
        return MoneyAccount != null
            && MoneyAccount.TrySpend(
                amount,
                new EconomyTransactionContext(
                    EconomyTransactionKind.LegacyExpense,
                    "building-construction",
                    description: "시설 건설"),
                out _);
    }
}
