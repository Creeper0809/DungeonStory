using UnityEngine;

public interface IInvasionIntruderFactory
{
    InvasionIntruderRuntime Create(GameObject intruderPrefab, Vector3 position);
    InvasionIntruderRuntime CreateDetached(
        GameObject intruderPrefab,
        Vector3 position);
    void Publish(InvasionIntruderRuntime runtime);
    void PublishDetached(InvasionIntruderRuntime runtime);
    void DestroyDetached(InvasionIntruderRuntime runtime);
    InvasionIntruderRuntime EnsureRuntime(GameObject intruderObject);
}

public sealed class InvasionIntruderRuntimeFactory : IInvasionIntruderFactory
{
    private const string PrefablessIntruderName = "Breakthrough Intruder";
    private readonly ICharacterVisualRootFactory visualRootFactory;
    private readonly ICharacterSpawnObjectFactory characterObjectFactory;
    private readonly IDefenseEngagementRuntime defenseEngagementRuntime;
    private readonly IDefenseBreachPlanner breachPlanner;
    private readonly IBuildingStructuralIntegrityRuntime structuralIntegrity;
    private readonly IDefenseRaidAwarenessRuntime raidAwareness;
    private readonly IDefenseFacilityNetworkRuntime facilityNetwork;
    private readonly IInvasionIntruderPatternDefinitionCatalog patternCatalog;

    public InvasionIntruderRuntimeFactory(
        ICharacterVisualRootFactory visualRootFactory,
        ICharacterSpawnObjectFactory characterObjectFactory,
        IDefenseEngagementRuntime defenseEngagementRuntime,
        IDefenseBreachPlanner breachPlanner,
        IBuildingStructuralIntegrityRuntime structuralIntegrity,
        IDefenseRaidAwarenessRuntime raidAwareness,
        IDefenseFacilityNetworkRuntime facilityNetwork,
        IInvasionIntruderPatternDefinitionCatalog patternCatalog)
    {
        this.visualRootFactory = visualRootFactory
            ?? throw new System.ArgumentNullException(nameof(visualRootFactory));
        this.characterObjectFactory = characterObjectFactory
            ?? throw new System.ArgumentNullException(nameof(characterObjectFactory));
        this.defenseEngagementRuntime = defenseEngagementRuntime
            ?? throw new System.ArgumentNullException(nameof(defenseEngagementRuntime));
        this.breachPlanner = breachPlanner
            ?? throw new System.ArgumentNullException(nameof(breachPlanner));
        this.structuralIntegrity = structuralIntegrity
            ?? throw new System.ArgumentNullException(nameof(structuralIntegrity));
        this.raidAwareness = raidAwareness
            ?? throw new System.ArgumentNullException(nameof(raidAwareness));
        this.facilityNetwork = facilityNetwork
            ?? throw new System.ArgumentNullException(nameof(facilityNetwork));
        this.patternCatalog = patternCatalog
            ?? throw new System.ArgumentNullException(nameof(patternCatalog));
    }

    public InvasionIntruderRuntime Create(GameObject intruderPrefab, Vector3 position)
    {
        GameObject intruderObject = null;
        try
        {
            bool prefabless = intruderPrefab == null;
            intruderObject = prefabless
                ? CreateInactivePrefablessObject()
                : characterObjectFactory.CreateInactive(
                    intruderPrefab,
                    EnsureRuntimeComponents);

            intruderObject.transform.position = position;
            return prefabless
                ? EnsureRuntime(intruderObject)
                : ConfigureRuntime(intruderObject);
        }
        catch
        {
            DestroyFailedCandidate(intruderObject);
            throw;
        }
    }

    public InvasionIntruderRuntime CreateDetached(
        GameObject intruderPrefab,
        Vector3 position)
    {
        GameObject intruderObject = null;
        try
        {
            bool prefabless = intruderPrefab == null;
            intruderObject = prefabless
                ? CreateDetachedPrefablessObject()
                : characterObjectFactory.CreateDetached(
                    intruderPrefab,
                    EnsureRuntimeComponents);
            intruderObject.transform.position = position;
            if (prefabless)
            {
                EnsureRuntimeComponents(intruderObject);
                characterObjectFactory.ComposeDetached(intruderObject);
            }

            return ConfigureRuntime(intruderObject);
        }
        catch
        {
            DestroyFailedCandidate(intruderObject);
            throw;
        }
    }

    public void PublishDetached(InvasionIntruderRuntime runtime)
    {
        if (runtime == null)
        {
            throw new System.ArgumentNullException(nameof(runtime));
        }
        characterObjectFactory.PublishDetached(runtime.gameObject);
    }

    public void Publish(InvasionIntruderRuntime runtime)
    {
        if (runtime == null)
        {
            throw new System.ArgumentNullException(nameof(runtime));
        }

        characterObjectFactory.Publish(runtime.gameObject);
    }

    public void DestroyDetached(InvasionIntruderRuntime runtime)
    {
        if (runtime != null)
        {
            characterObjectFactory.Destroy(runtime.gameObject);
        }
    }

    private void DestroyFailedCandidate(GameObject intruderObject)
    {
        if (intruderObject != null)
        {
            characterObjectFactory.Destroy(intruderObject);
        }
    }

    private static GameObject CreateDetachedPrefablessObject()
    {
        GameObject intruderObject = new GameObject(PrefablessIntruderName);
        Transform candidateRoot = DungeonRuntimeHierarchy.GetCategory(
            DungeonRuntimeHierarchy.RestoreCandidates,
            intruderObject);
        candidateRoot.gameObject.SetActive(false);
        intruderObject.transform.SetParent(candidateRoot, worldPositionStays: true);
        intruderObject.SetActive(false);
        return intruderObject;
    }

    private static GameObject CreateInactivePrefablessObject()
    {
        GameObject intruderObject = new GameObject(PrefablessIntruderName);
        intruderObject.SetActive(false);
        Transform stagingRoot = DungeonRuntimeHierarchy.GetCategory(
            DungeonRuntimeHierarchy.CharacterComposition,
            intruderObject);
        stagingRoot.gameObject.SetActive(false);
        intruderObject.transform.SetParent(stagingRoot, true);
        return intruderObject;
    }

    public InvasionIntruderRuntime EnsureRuntime(GameObject intruderObject)
    {
        EnsureRuntimeComponents(intruderObject);
        characterObjectFactory.Inject(intruderObject);
        return ConfigureRuntime(intruderObject);
    }

    private void EnsureRuntimeComponents(GameObject intruderObject)
    {
        if (intruderObject == null)
        {
            throw new System.ArgumentNullException(nameof(intruderObject));
        }

        visualRootFactory.EnsureVisualRoot(intruderObject);

        if (!intruderObject.TryGetComponent(out CharacterActor _))
        {
            intruderObject.AddComponent<CharacterActor>();
        }

        if (!intruderObject.TryGetComponent(out AbilityMove _))
        {
            intruderObject.AddComponent<AbilityMove>();
        }

        if (!intruderObject.TryGetComponent(out Collider2D _))
        {
            BoxCollider2D collider = intruderObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.8f, 1.6f);
            collider.offset = new Vector2(0f, 0.8f);
        }

        if (!intruderObject.TryGetComponent(out InvasionIntruderRuntime _))
        {
            intruderObject.AddComponent<InvasionIntruderRuntime>();
        }
    }

    private InvasionIntruderRuntime ConfigureRuntime(GameObject intruderObject)
    {
        if (intruderObject == null)
        {
            throw new System.ArgumentNullException(nameof(intruderObject));
        }

        CharacterActor actor = intruderObject.GetComponent<CharacterActor>()
            ?? throw new System.InvalidOperationException(
                "An invasion intruder requires CharacterActor composition.");
        InvasionIntruderRuntime runtime =
            intruderObject.GetComponent<InvasionIntruderRuntime>()
            ?? throw new System.InvalidOperationException(
                "An invasion intruder requires its runtime component.");
        runtime.ConfigureContent(patternCatalog);
        runtime.ConfigureDefenseEngagement(defenseEngagementRuntime);
        runtime.ConfigureTacticalServices(
            breachPlanner,
            structuralIntegrity,
            raidAwareness,
            facilityNetwork);
        actor.EnsureRuntimeState();
        actor.AbilityCache?.RefreshAbilityCache();
        return runtime;
    }

}
