using System;
using System.Collections.Generic;

public sealed class WorldSimulationSceneReferences
{
    public WorldSimulationSceneReferences(
        IReadOnlyList<WildlifeHabitatMarker> wildlifeHabitats = null,
        IReadOnlyList<ExteriorZoneMarker> exteriorZones = null)
    {
        WildlifeHabitats = wildlifeHabitats
            ?? Array.Empty<WildlifeHabitatMarker>();
        ExteriorZones = exteriorZones
            ?? Array.Empty<ExteriorZoneMarker>();
    }

    public IReadOnlyList<WildlifeHabitatMarker> WildlifeHabitats { get; }
    public IReadOnlyList<ExteriorZoneMarker> ExteriorZones { get; }
}

public sealed class OffenseSceneRuntimeReferences
{
    public OffenseSceneRuntimeReferences(
        OffenseWorldMapRuntime worldMap,
        OffenseRewardRuntime rewards,
        OffenseExpeditionRuntime expedition,
        OffenseWorldMapPanel worldMapPanel,
        OffenseExpeditionPanel expeditionPanel)
    {
        WorldMap = worldMap;
        Rewards = rewards;
        Expedition = expedition;
        WorldMapPanel = worldMapPanel;
        ExpeditionPanel = expeditionPanel;
    }

    public OffenseWorldMapRuntime WorldMap { get; }
    public OffenseRewardRuntime Rewards { get; }
    public OffenseExpeditionRuntime Expedition { get; }
    public OffenseWorldMapPanel WorldMapPanel { get; private set; }
    public OffenseExpeditionPanel ExpeditionPanel { get; private set; }

    public void RegisterWorldMapPanel(OffenseWorldMapPanel panel)
    {
        WorldMapPanel = panel
            ?? throw new ArgumentNullException(nameof(panel));
    }

    public void RegisterExpeditionPanel(OffenseExpeditionPanel panel)
    {
        ExpeditionPanel = panel
            ?? throw new ArgumentNullException(nameof(panel));
    }
}

public sealed class InvasionSceneRuntimeReferences
{
    public InvasionSceneRuntimeReferences(
        InvasionThreatRuntime threat,
        InvasionDirectorRuntime director,
        InvasionCombatReportRuntime combatReport)
    {
        Threat = threat;
        Director = director;
        CombatReport = combatReport;
    }

    public InvasionThreatRuntime Threat { get; }
    public InvasionDirectorRuntime Director { get; }
    public InvasionCombatReportRuntime CombatReport { get; }
}

public sealed class FacilityFeatureSceneRuntimeReferences
{
    public FacilityFeatureSceneRuntimeReferences(
        FacilityEvolutionRuntime evolution,
        FacilitySynthesisRuntime synthesis,
        CodexRuntime codex)
    {
        Evolution = evolution;
        Synthesis = synthesis;
        Codex = codex;
    }

    public FacilityEvolutionRuntime Evolution { get; }
    public FacilitySynthesisRuntime Synthesis { get; }
    public CodexRuntime Codex { get; }
}

public sealed class CharacterSceneRuntimeReferences
{
    public CharacterSceneRuntimeReferences(
        LocalLlmRequestQueue localLlm,
        SocialReputationRuntime socialReputation,
        StaffDiscontentRuntime staffDiscontent,
        RegularCustomerRuntime regularCustomers,
        CharacterSpawner spawner,
        CharacterAiScheduler aiScheduler,
        OwnerRunManager ownerRunManager,
        AiDirectorRuntime aiDirector)
    {
        LocalLlm = localLlm;
        SocialReputation = socialReputation;
        StaffDiscontent = staffDiscontent;
        RegularCustomers = regularCustomers;
        Spawner = spawner;
        AiScheduler = aiScheduler;
        OwnerRunManager = ownerRunManager;
        AiDirector = aiDirector;
    }

    public LocalLlmRequestQueue LocalLlm { get; }
    public SocialReputationRuntime SocialReputation { get; }
    public StaffDiscontentRuntime StaffDiscontent { get; }
    public RegularCustomerRuntime RegularCustomers { get; }
    public CharacterSpawner Spawner { get; }
    public CharacterAiScheduler AiScheduler { get; }
    public OwnerRunManager OwnerRunManager { get; }
    public AiDirectorRuntime AiDirector { get; }
}

public sealed class ProgressionSceneRuntimeReferences
{
    public ProgressionSceneRuntimeReferences(
        DailyFacilityShopRuntime facilityShop,
        BlueprintResearchRuntime blueprintResearch,
        MetaProgressionRuntime metaProgression)
    {
        FacilityShop = facilityShop;
        BlueprintResearch = blueprintResearch;
        MetaProgression = metaProgression;
    }

    public DailyFacilityShopRuntime FacilityShop { get; }
    public BlueprintResearchRuntime BlueprintResearch { get; }
    public MetaProgressionRuntime MetaProgression { get; }
}
