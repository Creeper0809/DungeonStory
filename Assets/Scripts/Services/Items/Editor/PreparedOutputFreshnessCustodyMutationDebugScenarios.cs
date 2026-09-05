#if UNITY_EDITOR
using UnityEngine;

public static class PreparedOutputFreshnessCustodyMutationDebugScenarios
{
    public static void RunAll()
    {
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        WorldItemStackRuntime runtime = PhysicalItemDebugScenarios
            .CreateRuntimeForCrossDomainFixture(
                catalog,
                out WorldItemRepository repository,
                out _,
                out _,
                out _,
                out _,
                out _);
        PreparedOutputCustodyMutationGuardDebugScenarios
            .RunFreshnessMutationGuard(runtime, repository);
        Debug.Log("V27_PREPARED_OUTPUT_FRESHNESS_CUSTODY_MUTATION=PASS");
    }
}
#endif
