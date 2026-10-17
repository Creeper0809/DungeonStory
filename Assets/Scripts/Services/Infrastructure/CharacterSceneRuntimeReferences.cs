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
