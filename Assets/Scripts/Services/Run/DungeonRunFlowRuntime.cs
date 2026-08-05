using DungeonStory.Content.CoreSession;
using DungeonStory.Foundation;

public sealed class DungeonRunFlowRuntime : DungeonRunFlowApplicationAdapter
{
    public DungeonRunFlowRuntime(
        IOwnerRunManagerProvider ownerProvider,
        InvasionSceneRuntimeReferences invasionRuntimes,
        IGameEventBus gameEventBus,
        IExperiencePacingRuntime experiencePacing,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        ICoreSessionRulesProvider rulesProvider)
        : base(
            ownerProvider,
            invasionRuntimes,
            gameEventBus,
            experiencePacing,
            aggregateRootStore,
            rulesProvider)
    {
    }

    public static float ResolveBossHealthMultiplier(int cycle) =>
        DungeonRunFlowReducer.ResolveBossHealthMultiplier(cycle);

    public static float ResolveBossDamageMultiplier(int cycle) =>
        DungeonRunFlowReducer.ResolveBossDamageMultiplier(cycle);

    public static float ResolveThreatRiseMultiplier(int cycle) =>
        DungeonRunFlowReducer.ResolveThreatRiseMultiplier(cycle);

    internal static DungeonRunPhase ResolvePhaseForDay(
        int day,
        CoreSessionRulesDefinition rules) =>
        DungeonRunFlowRules.ResolvePhaseForDay(day, rules);

    public static int ResolveBossCycleForDay(
        int day,
        CoreSessionRulesDefinition rules) =>
        DungeonRunFlowRules.ResolveBossCycleForDay(day, rules);
}
