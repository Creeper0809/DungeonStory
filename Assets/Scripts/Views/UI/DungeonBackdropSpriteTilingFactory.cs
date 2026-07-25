using System;
using UnityEngine;

public interface IDungeonBackdropSpriteTilingFactory
{
    SpriteRenderer Duplicate(SpriteRenderer template, float targetMinX);
}

public sealed class DungeonBackdropSpriteTilingFactory : IDungeonBackdropSpriteTilingFactory
{
    public SpriteRenderer Duplicate(SpriteRenderer template, float targetMinX)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        if (template.sprite == null)
        {
            throw new InvalidOperationException($"{nameof(DungeonBackdropSpriteTilingFactory)} requires a template with a sprite.");
        }

        GameObject copyObject = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
        copyObject.name = template.sprite.name + " AutoTile";

        SpriteRenderer copy = copyObject.GetComponent<SpriteRenderer>();
        if (copy == null)
        {
            throw new InvalidOperationException("Duplicated backdrop object is missing a SpriteRenderer.");
        }

        Vector3 position = template.transform.position;
        float centerOffset = template.transform.position.x - template.bounds.min.x;
        position.x = targetMinX + centerOffset;
        copyObject.transform.position = position;
        return copy;
    }
}
