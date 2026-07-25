using System;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

public interface ICharacterSpawnObjectFactory
{
    GameObject Create(GameObject characterPrefab);
    void Inject(GameObject characterObject);
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

    public GameObject Create(GameObject characterPrefab)
    {
        if (characterPrefab == null)
        {
            throw new ArgumentNullException(nameof(characterPrefab));
        }

        GameObject characterObject = Object.Instantiate(characterPrefab);
        DungeonRuntimeHierarchy.Parent(characterObject, DungeonRuntimeHierarchy.Characters);
        return characterObject;
    }

    public void Inject(GameObject characterObject)
    {
        if (characterObject == null)
        {
            return;
        }

        foreach (MonoBehaviour component in characterObject.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
        {
            if (component != null)
            {
                objectResolver.Inject(component);
            }
        }
    }

    public void Destroy(GameObject characterObject)
    {
        if (characterObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(characterObject);
            return;
        }

        Object.DestroyImmediate(characterObject);
    }
}
