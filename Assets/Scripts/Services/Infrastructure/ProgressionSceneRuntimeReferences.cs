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
