using System;
using System.Linq;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

public sealed class DetachedCharacterPublication
{
    internal DetachedCharacterPublication(
        CharacterSpawnObjectFactory owner,
        GameObject characterObject,
        Transform previousParent,
        int previousSiblingIndex)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        CharacterObject = characterObject
            ?? throw new ArgumentNullException(nameof(characterObject));
        PreviousParent = previousParent;
        PreviousSiblingIndex = previousSiblingIndex;
    }

    internal CharacterSpawnObjectFactory Owner { get; }
    internal Transform PreviousParent { get; }
    internal int PreviousSiblingIndex { get; }
    public GameObject CharacterObject { get; }
    public bool IsPending { get; private set; } = true;

    internal void Finish() => IsPending = false;
}

public interface ICharacterSpawnObjectFactory
{
    GameObject CreateInactive(GameObject characterPrefab);
    GameObject CreateInactive(
        GameObject characterPrefab,
        Action<GameObject> compose);
    GameObject CreateDetached(GameObject characterPrefab);
    GameObject CreateDetached(
        GameObject characterPrefab,
        Action<GameObject> compose);
    void ComposeDetached(GameObject characterObject);
    void Inject(GameObject characterObject);
    void InjectAddedAbility(CharacterAbility ability);
    void Publish(GameObject characterObject);
    void PublishDetached(GameObject characterObject);
    DetachedCharacterPublication PublishDetachedInactive(
        GameObject characterObject);
    void ValidateDetachedPublication(DetachedCharacterPublication publication);
    void CompleteDetachedPublication(DetachedCharacterPublication publication);
    void RollbackDetachedPublication(DetachedCharacterPublication publication);
    void Destroy(GameObject characterObject);
}

public sealed class CharacterSpawnObjectFactory : ICharacterSpawnObjectFactory
{
    private readonly IObjectResolver objectResolver;

    public CharacterSpawnObjectFactory(IObjectResolver objectResolver)
    {
        this.objectResolver = objectResolver
            ?? throw new ArgumentNullException(nameof(objectResolver));
    }

    public GameObject CreateInactive(GameObject characterPrefab)
    {
        return CreateInactive(characterPrefab, compose: null);
    }

    public GameObject CreateInactive(
        GameObject characterPrefab,
        Action<GameObject> compose)
    {
        if (characterPrefab == null)
        {
            throw new ArgumentNullException(nameof(characterPrefab));
        }

        Transform stagingRoot = DungeonRuntimeHierarchy.GetCategory(
            DungeonRuntimeHierarchy.CharacterComposition,
            characterPrefab);
        stagingRoot.gameObject.SetActive(false);
        GameObject characterObject = Object.Instantiate(
            characterPrefab,
            stagingRoot,
            worldPositionStays: true);
        characterObject.SetActive(false);
        try
        {
            compose?.Invoke(characterObject);
            ComposeInactive(characterObject);
            return characterObject;
        }
        catch
        {
            Destroy(characterObject);
            throw;
        }
    }

    public GameObject CreateDetached(GameObject characterPrefab)
    {
        return CreateDetached(characterPrefab, compose: null);
    }

    public GameObject CreateDetached(
        GameObject characterPrefab,
        Action<GameObject> compose)
    {
        if (characterPrefab == null)
        {
            throw new ArgumentNullException(nameof(characterPrefab));
        }

        Transform candidateRoot = DungeonRuntimeHierarchy.GetCategory(
            DungeonRuntimeHierarchy.RestoreCandidates,
            characterPrefab);
        candidateRoot.gameObject.SetActive(false);
        GameObject characterObject = Object.Instantiate(
            characterPrefab,
            candidateRoot,
            worldPositionStays: true);
        characterObject.SetActive(false);
        try
        {
            compose?.Invoke(characterObject);
            ComposeDetached(characterObject);
            return characterObject;
        }
        catch
        {
            Destroy(characterObject);
            throw;
        }
    }

    public void ComposeDetached(GameObject characterObject)
    {
        if (characterObject == null)
        {
            throw new ArgumentNullException(nameof(characterObject));
        }

        CharacterActor actor = CharacterActorCollection.GetCanonical(
            characterObject.GetComponent<CharacterActor>());
        if (actor == null)
        {
            throw new InvalidOperationException(
                "A detached character candidate requires CharacterActor.");
        }

        actor.PrepareForDetachedRestore();
        InjectComponents(
            characterObject,
            component => component is not CharacterActor);
        objectResolver.Inject(actor);
    }

    private void ComposeInactive(GameObject characterObject)
    {
        CharacterActor actor = RequireCanonicalActor(
            characterObject,
            "An unpublished character composition requires CharacterActor.");
        actor.PrepareForComposition();
        InjectComponents(
            characterObject,
            component => component is not CharacterActor);
        objectResolver.Inject(actor);
    }

    public void Inject(GameObject characterObject)
    {
        if (characterObject == null)
        {
            return;
        }

        CharacterActor actor = CharacterActorCollection.GetCanonical(
            characterObject.GetComponent<CharacterActor>());
        if (actor != null
            && !actor.IsUnpublishedComposition
            && !actor.IsDetachedRestoreCandidate
            && !actor.IsRuntimeBridgeConfigured)
        {
            actor.PrepareForComposition();
        }

        InjectComponents(
            characterObject,
            component => component is not CharacterActor);
        if (actor != null)
        {
            objectResolver.Inject(actor);
        }
    }

    public void InjectAddedAbility(CharacterAbility ability)
    {
        if (ability == null)
        {
            throw new ArgumentNullException(nameof(ability));
        }

        objectResolver.Inject(ability);
    }

    private void InjectComponents(
        GameObject characterObject,
        Func<MonoBehaviour, bool> predicate)
    {
        foreach (MonoBehaviour component in characterObject
                     .GetComponentsInChildren<MonoBehaviour>(includeInactive: true)
                     .Where(component => component != null && predicate(component)))
        {
            objectResolver.Inject(component);
        }
    }

    public void Publish(GameObject characterObject)
    {
        if (characterObject == null)
        {
            throw new ArgumentNullException(nameof(characterObject));
        }

        CharacterActor actor = RequireCanonicalActor(
            characterObject,
            "A published character requires CharacterActor.");
        if (actor.IsDetachedRestoreCandidate)
        {
            throw new InvalidOperationException(
                "A detached character candidate must use PublishDetached.");
        }
        if (characterObject.activeSelf || characterObject.activeInHierarchy)
        {
            throw new InvalidOperationException(
                "A character composition must remain inactive until publication.");
        }

        if (!actor.IsUnpublishedComposition)
        {
            actor.RequireReadyForPublishedReactivation();
            DungeonRuntimeHierarchy.Parent(
                characterObject,
                DungeonRuntimeHierarchy.Characters);
            characterObject.SetActive(true);
            return;
        }

        actor.RequireCompositionReadyForPublication();

        DungeonRuntimeHierarchy.Parent(
            characterObject,
            DungeonRuntimeHierarchy.Characters);
        actor.PublishComposition();
        characterObject.SetActive(true);
    }

    public void PublishDetached(GameObject characterObject)
    {
        DetachedCharacterPublication publication =
            PublishDetachedInactive(characterObject);
        CompleteDetachedPublication(publication);
    }

    public DetachedCharacterPublication PublishDetachedInactive(
        GameObject characterObject)
    {
        if (characterObject == null)
        {
            throw new ArgumentNullException(nameof(characterObject));
        }

        CharacterActor actor = CharacterActorCollection.GetCanonical(
            characterObject.GetComponent<CharacterActor>());
        if (actor == null || !actor.IsDetachedRestoreCandidate)
        {
            throw new InvalidOperationException(
                "Only a detached character candidate can be published.");
        }
        if (characterObject.activeSelf || characterObject.activeInHierarchy)
        {
            throw new InvalidOperationException(
                "A detached character candidate must remain inactive until publication.");
        }

        actor.RequireDetachedReadyForPublication();

        DetachedCharacterPublication publication =
            new DetachedCharacterPublication(
                this,
                characterObject,
                characterObject.transform.parent,
                characterObject.transform.GetSiblingIndex());
        try
        {
            DungeonRuntimeHierarchy.Parent(
                characterObject,
                DungeonRuntimeHierarchy.Characters);
            actor.PublishDetachedRestore();
            return publication;
        }
        catch
        {
            if (actor.HasBeenPublished && !actor.IsDetachedRestoreCandidate)
            {
                actor.RollbackDetachedRestorePublication();
            }
            RestorePreviousPlacement(publication);
            publication.Finish();
            throw;
        }
    }

    public void CompleteDetachedPublication(
        DetachedCharacterPublication publication)
    {
        GameObject characterObject = RequirePendingPublication(publication);
        ValidateDetachedPublication(publication);
        characterObject.SetActive(true);
        CharacterActor actor = RequireCanonicalActor(
            characterObject,
            "A completed detached character publication requires CharacterActor.");
        actor.ReconcilePublishedRuntimeRegistration();
        publication.Finish();
    }

    public void ValidateDetachedPublication(
        DetachedCharacterPublication publication)
    {
        GameObject characterObject = RequirePendingPublication(publication);
        CharacterActor actor = RequireCanonicalActor(
            characterObject,
            "A detached character publication requires CharacterActor.");
        if (!actor.HasBeenPublished
            || actor.IsDetachedRestoreCandidate
            || actor.IsUnpublishedComposition
            || characterObject.activeSelf
            || characterObject.activeInHierarchy)
        {
            throw new InvalidOperationException(
                "Only an inactive published detached character can be completed.");
        }
    }

    public void RollbackDetachedPublication(
        DetachedCharacterPublication publication)
    {
        GameObject characterObject = RequirePendingPublication(publication);

        CharacterActor actor = RequireCanonicalActor(
            characterObject,
            "A detached character publication rollback requires CharacterActor.");
        if (characterObject.activeSelf)
        {
            characterObject.SetActive(false);
        }

        try
        {
            actor.RollbackDetachedRestorePublication();
        }
        finally
        {
            RestorePreviousPlacement(publication);
            publication.Finish();
        }
    }

    private GameObject RequirePendingPublication(
        DetachedCharacterPublication publication)
    {
        if (publication == null)
        {
            throw new ArgumentNullException(nameof(publication));
        }
        if (!ReferenceEquals(publication.Owner, this)
            || !publication.IsPending
            || publication.CharacterObject == null)
        {
            throw new InvalidOperationException(
                "The detached character publication is not pending on this factory.");
        }

        return publication.CharacterObject;
    }

    private static void RestorePreviousPlacement(
        DetachedCharacterPublication publication)
    {
        Transform transform = publication.CharacterObject.transform;
        transform.SetParent(publication.PreviousParent, true);
        if (publication.PreviousParent != null)
        {
            transform.SetSiblingIndex(Mathf.Min(
                publication.PreviousSiblingIndex,
                publication.PreviousParent.childCount - 1));
        }
    }

    public void Destroy(GameObject characterObject)
    {
        if (characterObject == null)
        {
            return;
        }

        CharacterActor actor = CharacterActorCollection.GetCanonical(
            characterObject.GetComponent<CharacterActor>());
        if (actor != null && actor.IsDetachedRestoreCandidate)
        {
            Object.DestroyImmediate(characterObject);
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(characterObject);
            return;
        }

        Object.DestroyImmediate(characterObject);
    }

    private static CharacterActor RequireCanonicalActor(
        GameObject characterObject,
        string message)
    {
        CharacterActor actor = CharacterActorCollection.GetCanonical(
            characterObject != null
                ? characterObject.GetComponent<CharacterActor>()
                : null);
        return actor ?? throw new InvalidOperationException(message);
    }
}
