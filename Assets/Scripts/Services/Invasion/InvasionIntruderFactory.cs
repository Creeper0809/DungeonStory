using UnityEngine;

public interface IInvasionIntruderFactory
{
    InvasionIntruderRuntime Create(GameObject intruderPrefab, Vector3 position);
    InvasionIntruderRuntime EnsureRuntime(GameObject intruderObject);
}

public sealed class InvasionIntruderRuntimeFactory : IInvasionIntruderFactory
{
    private const string PrefablessIntruderName = "Breakthrough Intruder";
    private readonly ICharacterVisualRootFactory visualRootFactory;
    private readonly ICharacterSpawnObjectFactory characterObjectFactory;
    private readonly IDefenseEngagementRuntime defenseEngagementRuntime;

    public InvasionIntruderRuntimeFactory(
        ICharacterVisualRootFactory visualRootFactory,
        ICharacterSpawnObjectFactory characterObjectFactory,
        IDefenseEngagementRuntime defenseEngagementRuntime)
    {
        this.visualRootFactory = visualRootFactory
            ?? throw new System.ArgumentNullException(nameof(visualRootFactory));
        this.characterObjectFactory = characterObjectFactory
            ?? throw new System.ArgumentNullException(nameof(characterObjectFactory));
        this.defenseEngagementRuntime = defenseEngagementRuntime
            ?? throw new System.ArgumentNullException(nameof(defenseEngagementRuntime));
    }

    public InvasionIntruderRuntime Create(GameObject intruderPrefab, Vector3 position)
    {
        GameObject intruderObject = intruderPrefab != null
            ? characterObjectFactory.Create(intruderPrefab)
            : new GameObject(PrefablessIntruderName);

        intruderObject.transform.position = position;
        return EnsureRuntime(intruderObject);
    }

    public InvasionIntruderRuntime EnsureRuntime(GameObject intruderObject)
    {
        if (intruderObject == null)
        {
            throw new System.ArgumentNullException(nameof(intruderObject));
        }

        visualRootFactory.EnsureVisualRoot(intruderObject);

        if (!intruderObject.TryGetComponent(out CharacterActor actor))
        {
            actor = intruderObject.AddComponent<CharacterActor>();
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

        if (!intruderObject.TryGetComponent(out InvasionIntruderRuntime runtime))
        {
            runtime = intruderObject.AddComponent<InvasionIntruderRuntime>();
        }

        characterObjectFactory.Inject(intruderObject);
        runtime.ConfigureDefenseEngagement(defenseEngagementRuntime);
        actor.EnsureRuntimeState();
        actor.AbilityCache?.RefreshAbilityCache();
        return runtime;
    }

}
