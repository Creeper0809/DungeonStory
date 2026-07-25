using UnityEngine;

public sealed class DoorAccessLockIndicator : MonoBehaviour
{
    private const string RootName = "DoorAccessLock";
    private static Sprite pixelSprite;
    private GameObject visualRoot;

    public void Refresh(bool restricted)
    {
        EnsureVisual();
        if (visualRoot != null)
        {
            visualRoot.SetActive(restricted);
        }
    }

    private void EnsureVisual()
    {
        if (visualRoot != null)
        {
            return;
        }

        Transform existing = transform.Find(RootName);
        if (existing != null)
        {
            visualRoot = existing.gameObject;
            return;
        }

        visualRoot = new GameObject(RootName);
        visualRoot.transform.SetParent(transform, false);
        visualRoot.transform.localPosition = new Vector3(0.35f, 0.85f, 0f);
        CreatePart("Body", new Vector2(0.3f, 0.24f), new Vector2(0f, -0.08f));
        CreatePart("ShackleLeft", new Vector2(0.055f, 0.16f), new Vector2(-0.09f, 0.1f));
        CreatePart("ShackleRight", new Vector2(0.055f, 0.16f), new Vector2(0.09f, 0.1f));
        CreatePart("ShackleTop", new Vector2(0.23f, 0.05f), new Vector2(0f, 0.18f));
        CreatePart("Keyhole", new Vector2(0.045f, 0.08f), new Vector2(0f, -0.08f), new Color(0.12f, 0.1f, 0.08f, 1f));
    }

    private void CreatePart(
        string name,
        Vector2 size,
        Vector2 localPosition,
        Color? color = null)
    {
        GameObject part = new GameObject(name, typeof(SpriteRenderer));
        part.transform.SetParent(visualRoot.transform, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer renderer = part.GetComponent<SpriteRenderer>();
        renderer.sprite = GetPixelSprite();
        renderer.color = color ?? new Color(0.95f, 0.72f, 0.22f, 1f);
        renderer.sortingLayerName = DungeonDoorVisualLayout.SortingLayerName;
        renderer.sortingOrder = DungeonDoorVisualLayout.CeilingSortingOrder + 12;
    }

    private static Sprite GetPixelSprite()
    {
        if (pixelSprite != null)
        {
            return pixelSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "DoorAccessLockPixel",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(false, true);
        pixelSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        pixelSprite.name = "DoorAccessLockPixel";
        pixelSprite.hideFlags = HideFlags.HideAndDontSave;
        return pixelSprite;
    }
}
