using System.Collections.Generic;

public static class FacilityCandidateCache
{
    private static readonly IFacilityCandidateCache Cache = new FacilityCandidateCacheStore(CharacterAiEditorTestDependencies.WorldRegistry, frameWorkBudget: null);

    public static IReadOnlyList<BuildableObject> GetCandidates(Grid grid, FacilityRole role)
    {
        return Cache.GetCandidates(grid, role);
    }

    public static void MarkDynamicStateDirty()
    {
        Cache.MarkDynamicStateDirty();
    }

    public static void Clear()
    {
        Cache.Clear();
    }
}
