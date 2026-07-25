using System;
using BehaviorDesigner.Runtime;
using UnityEngine;
using VContainer;

public interface IOwnerCharacterFactory
{
    CharacterActor CreateOwner(
        CharacterSO ownerData,
        GameObject ownerPrefab,
        Transform ownerSpawnPoint,
        Vector2Int ownerSpawnGridPosition);
}

public sealed class OwnerCharacterFactory : IOwnerCharacterFactory
{
    private static readonly Vector2 OwnerClickColliderOffset = new Vector2(0f, 0.5f);
    private static readonly Vector2 OwnerClickColliderSize = Vector2.one;

    private readonly IObjectResolver objectResolver;
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly ICharacterVisualRootFactory visualRootFactory;

    public OwnerCharacterFactory(
        IObjectResolver objectResolver,
        IGridSystemProvider gridSystemProvider,
        ICharacterVisualRootFactory visualRootFactory)
    {
        this.objectResolver = objectResolver
            ?? throw new ArgumentNullException(nameof(objectResolver));
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.visualRootFactory = visualRootFactory
            ?? throw new ArgumentNullException(nameof(visualRootFactory));
    }

    public CharacterActor CreateOwner(
        CharacterSO ownerData,
        GameObject ownerPrefab,
        Transform ownerSpawnPoint,
        Vector2Int ownerSpawnGridPosition)
    {
        if (ownerData == null)
        {
            throw new ArgumentNullException(nameof(ownerData));
        }

        GameObject ownerObject = ownerPrefab != null
            ? UnityEngine.Object.Instantiate(ownerPrefab)
            : new GameObject("OwnerCharacter");

        ownerObject.name = ownerData.characterName;
        ownerObject.transform.position = ResolveOwnerSpawnPosition(ownerSpawnPoint, ownerSpawnGridPosition);
        DungeonRuntimeHierarchy.Parent(ownerObject, DungeonRuntimeHierarchy.Characters);

        CharacterActor owner = EnsureOwnerComponents(ownerObject);
        InjectOwnerRuntime(ownerObject);
        owner.SetLifecycleState(CharacterLifecycleState.Active);
        owner.Initialize(ownerData);
        owner.Brain?.UseOwnerWorkActions();
        return owner;
    }

    private CharacterActor EnsureOwnerComponents(GameObject ownerObject)
    {
        visualRootFactory.EnsureVisualRoot(ownerObject);
        EnsureOwnerClickCollider(ownerObject);

        BehaviorTree behaviorTree = ownerObject.GetComponent<BehaviorTree>();
        if (behaviorTree == null)
        {
            behaviorTree = ownerObject.AddComponent<BehaviorTree>();
        }

        behaviorTree.StartWhenEnabled = false;

        if (!ownerObject.TryGetComponent(out CharacterActor actor))
        {
            actor = ownerObject.AddComponent<CharacterActor>();
        }

        if (!ownerObject.TryGetComponent(out AIBrain _))
        {
            ownerObject.AddComponent<AIBrain>();
        }

        if (!ownerObject.TryGetComponent(out AbilityMove _))
        {
            ownerObject.AddComponent<AbilityMove>();
        }

        if (!ownerObject.TryGetComponent(out AbilityWork _))
        {
            ownerObject.AddComponent<AbilityWork>();
        }

        actor.EnsureRuntimeState();
        actor.AbilityCache?.RefreshAbilityCache();
        return actor;
    }

    private static void EnsureOwnerClickCollider(GameObject ownerObject)
    {
        if (ownerObject == null)
        {
            return;
        }

        BoxCollider2D collider = ownerObject.GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = ownerObject.AddComponent<BoxCollider2D>();
        }

        collider.isTrigger = true;
        collider.offset = OwnerClickColliderOffset;
        collider.size = OwnerClickColliderSize;
    }

    private void InjectOwnerRuntime(GameObject ownerObject)
    {
        foreach (MonoBehaviour component in ownerObject.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
        {
            objectResolver.Inject(component);
        }
    }

    private Vector3 ResolveOwnerSpawnPosition(Transform ownerSpawnPoint, Vector2Int ownerSpawnGridPosition)
    {
        if (ownerSpawnPoint != null)
        {
            return ownerSpawnPoint.position;
        }

        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return Vector3.zero;
        }

        if (grid.IsValidGridPos(ownerSpawnGridPosition) && grid.IsWalkable(ownerSpawnGridPosition))
        {
            return grid.GetWorldPos(ownerSpawnGridPosition);
        }

        return grid.TryFindNearestWalkablePosition(ownerSpawnGridPosition, out Vector2Int walkablePosition)
            ? grid.GetWorldPos(walkablePosition)
            : Vector3.zero;
    }
}
