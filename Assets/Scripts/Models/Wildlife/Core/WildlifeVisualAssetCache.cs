using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class WildlifeVisualAssetCache
{
    [RuntimeRebuildableCache] private static Sprite fallbackSprite;
    [RuntimeRebuildableCache] private static Sprite markerSprite;
    [RuntimeRebuildableCache] private static Material sharedLineMaterial;

    public static Material ResolveLineMaterial()
    {
        if (sharedLineMaterial != null)
        {
            return sharedLineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default")
            ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            return null;
        }

        sharedLineMaterial = new Material(shader)
        {
            name = "WildlifeHealthLineMaterial"
        };
        return sharedLineMaterial;
    }

    public static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
        {
            return fallbackSprite;
        }

        Texture2D texture = new Texture2D(
            16,
            16,
            TextureFormat.RGBA32,
            false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                bool body = x >= 3 && x <= 12 && y >= 4 && y <= 10;
                bool head = x >= 10 && x <= 14 && y >= 7 && y <= 12;
                bool leg = (x == 5 || x == 10) && y >= 1 && y <= 4;
                texture.SetPixel(
                    x,
                    y,
                    body || head || leg ? Color.white : clear);
            }
        }

        texture.Apply();
        fallbackSprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0f),
            16f);
        fallbackSprite.name = "WildlifeFallbackSprite";
        return fallbackSprite;
    }

    public static Sprite GetMarkerSprite()
    {
        if (markerSprite != null)
        {
            return markerSprite;
        }

        Texture2D texture = new Texture2D(
            8,
            8,
            TextureFormat.RGBA32,
            false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                bool diamond = Mathf.Abs(x - 3.5f)
                    + Mathf.Abs(y - 3.5f) <= 3.5f;
                bool outline = Mathf.Abs(x - 3.5f)
                    + Mathf.Abs(y - 3.5f) >= 2.5f;
                texture.SetPixel(
                    x,
                    y,
                    diamond
                        ? outline ? Color.black : Color.white
                        : clear);
            }
        }

        texture.Apply();
        markerSprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            8f);
        markerSprite.name = "WildlifeStateMarkerSprite";
        return markerSprite;
    }
}
