using System;
using UnityEngine;

public interface ICharacterVisualRootFactory
{
    SpriteRenderer EnsureVisualRoot(GameObject characterObject);
}

public sealed class CharacterVisualRootFactory : ICharacterVisualRootFactory
{
    private const string VisualRootName = "Visual";

    public SpriteRenderer EnsureVisualRoot(GameObject characterObject)
    {
        if (characterObject == null)
        {
            throw new ArgumentNullException(nameof(characterObject));
        }

        Transform visual = characterObject.transform.Find(VisualRootName);
        if (visual == null)
        {
            GameObject visualObject = new GameObject(VisualRootName);
            visual = visualObject.transform;
            visual.SetParent(characterObject.transform, false);
        }

        if (!visual.TryGetComponent(out SpriteRenderer renderer))
        {
            renderer = visual.gameObject.AddComponent<SpriteRenderer>();
        }

        return renderer;
    }
}
