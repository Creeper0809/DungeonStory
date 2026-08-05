using System;
using BehaviorDesigner.Runtime;
using UnityEngine;

public interface IOwnerCharacterFactory
{
    CharacterActor CreateOwner(
        CharacterSO ownerData,
        GameObject ownerPrefab,
        Transform ownerSpawnPoint,
        Vector2Int ownerSpawnGridPosition);
    CharacterActor CreateOwnerDetached(
        CharacterSO ownerData,
        GameObject ownerPrefab);
}

public sealed class OwnerCharacterFactory : IOwnerCharacterFactory
{
    private static readonly Vector2 OwnerClickColliderOffset = new Vector2(0f, 0.5f);
    private static readonly Vector2 OwnerClickColliderSize = Vector2.one;

    private readonly ICharacterSpawnObjectFactory characterObjectFactory;
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly ICharacterVisualRootFactory visualRootFactory;

    public OwnerCharacterFactory(
        ICharacterSpawnObjectFactory characterObjectFactory,
        IGridSystemProvider gridSystemProvider,
        ICharacterVisualRootFactory visualRootFactory)
    {
        this.characterObjectFactory = characterObjectFactory
            ?? throw new ArgumentNullException(nameof(characterObjectFactory));
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
        return CreateOwnerInternal(
            ownerData,
            ownerPrefab,
            ResolveOwnerSpawnPosition(
                ownerSpawnPoint,
                ownerSpawnGridPosition),
            detached: false);
    }

    public CharacterActor CreateOwnerDetached(
        CharacterSO ownerData,
        GameObject ownerPrefab)
    {
        return CreateOwnerInternal(
            ownerData,
            ownerPrefab,
            Vector3.zero,
            detached: true);
    }

    private CharacterActor CreateOwnerInternal(
        CharacterSO ownerData,
        GameObject ownerPrefab,
        Vector3 spawnPosition,
        bool detached)
    {
        if (ownerData == null)
        {
            throw new ArgumentNullException(nameof(ownerData));
        }

        GameObject ownerObject = CreateOwnerObject(ownerPrefab, detached);
        ownerObject.name = ownerData.characterName;
        ownerObject.transform.position = spawnPosition;

        try
        {
            CharacterActor owner = EnsureOwnerComponents(ownerObject);
            if (ownerPrefab == null)
            {
                if (detached)
                {
                    characterObjectFactory.ComposeDetached(ownerObject);
                }
                else
                {
                    InjectOwnerRuntime(ownerObject);
                }
            }
            owner.EnsureRuntimeState();
            owner.AbilityCache?.RefreshAbilityCache();
            if (owner.Brain == null || !owner.Brain.HasResumableDecisionPipeline)
            {
                throw new InvalidOperationException(
                    "Owner AI must be fully injected before the character becomes active.");
            }

            owner.Initialize(ownerData);
            owner.Brain.UseOwnerWorkActions();
            owner.SetLifecycleState(CharacterLifecycleState.Active);
            if (!detached)
            {
                characterObjectFactory.Publish(ownerObject);
            }
            return owner;
        }
        catch
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(ownerObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(ownerObject);
            }

            throw;
        }
    }

    private GameObject CreateOwnerObject(GameObject ownerPrefab, bool detached)
    {
        if (ownerPrefab != null)
        {
            return detached
                ? characterObjectFactory.CreateDetached(
                    ownerPrefab,
                    candidate => EnsureOwnerComponents(candidate))
                : characterObjectFactory.CreateInactive(
                    ownerPrefab,
                    candidate => EnsureOwnerComponents(candidate));
        }

        GameObject ownerObject = new GameObject("OwnerCharacter");
        ownerObject.SetActive(false);
        if (detached)
        {
            Transform candidateRoot = DungeonRuntimeHierarchy.GetCategory(
                DungeonRuntimeHierarchy.RestoreCandidates,
                ownerObject);
            candidateRoot.gameObject.SetActive(false);
            ownerObject.transform.SetParent(candidateRoot, true);
        }
        else
        {
            Transform stagingRoot = DungeonRuntimeHierarchy.GetCategory(
                DungeonRuntimeHierarchy.CharacterComposition,
                ownerObject);
            stagingRoot.gameObject.SetActive(false);
            ownerObject.transform.SetParent(stagingRoot, true);
        }

        return ownerObject;
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
        characterObjectFactory.Inject(ownerObject);
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
