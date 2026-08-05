using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WildlifeHabitatOverlay : IDisposable
{
    private readonly List<SpriteRenderer> renderers =
        new List<SpriteRenderer>();

    private GameObject root;
    private Sprite sprite;
    private readonly IWildlifeOverlayRootPort rootPort;

    public WildlifeHabitatOverlay(IWildlifeOverlayRootPort rootPort)
    {
        this.rootPort = rootPort
            ?? throw new ArgumentNullException(nameof(rootPort));
    }

    public bool Enabled { get; private set; }

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        if (!enabled)
        {
            Clear();
        }
    }

    public void Refresh(
        IWildlifeGridPort grid,
        IReadOnlyList<WildlifeHabitatPatch> patches)
    {
        if (!Enabled || grid == null)
        {
            Clear();
            return;
        }

        EnsureRoot();
        while (renderers.Count < patches.Count)
        {
            GameObject entry = new GameObject("HabitatPatchOverlay");
            entry.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = entry.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSprite();
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 78;
            renderers.Add(renderer);
        }

        for (int index = 0; index < renderers.Count; index++)
        {
            SpriteRenderer renderer = renderers[index];
            if (renderer == null)
            {
                continue;
            }

            bool active = index < patches.Count;
            renderer.gameObject.SetActive(active);
            if (!active)
            {
                continue;
            }

            WildlifeHabitatPatch patch = patches[index];
            Vector3 world = grid.GetWorldPosition(patch.Center);
            renderer.transform.position =
                new Vector3(world.x, world.y + 0.06f, -0.04f);
            renderer.transform.localScale = new Vector3(
                Mathf.Max(1f, patch.Radius * 2f + 1f),
                0.85f,
                1f);
            renderer.color = ResolveColor(patch);
            renderer.gameObject.name =
                "HabitatPatch_" + patch.HabitatType + "_" + patch.PatchId;
        }
    }

    public void Clear()
    {
        for (int index = renderers.Count - 1; index >= 0; index--)
        {
            SpriteRenderer renderer = renderers[index];
            if (renderer != null)
            {
                UnityEngine.Object.Destroy(renderer.gameObject);
            }
        }

        renderers.Clear();
        if (root != null)
        {
            UnityEngine.Object.Destroy(root);
            root = null;
        }
    }

    public void Dispose()
    {
        Clear();
        if (sprite != null)
        {
            UnityEngine.Object.Destroy(sprite.texture);
            UnityEngine.Object.Destroy(sprite);
            sprite = null;
        }
    }

    private void EnsureRoot()
    {
        if (root != null)
        {
            return;
        }

        root = new GameObject("WildlifeHabitatOverlay");
        rootPort.ParentOverlayRoot(root);
    }

    private static Color ResolveColor(WildlifeHabitatPatch patch)
    {
        float alpha = Mathf.Lerp(0.12f, 0.28f, patch.Resource01);
        return patch.HabitatType switch
        {
            WildlifeHabitatType.Water => new Color(0.2f, 0.55f, 1f, alpha),
            WildlifeHabitatType.Burrow => new Color(0.85f, 0.58f, 0.18f, alpha),
            WildlifeHabitatType.Brush => new Color(0.12f, 0.72f, 0.48f, alpha),
            WildlifeHabitatType.Lair => new Color(0.9f, 0.16f, 0.12f, alpha),
            _ => new Color(0.18f, 0.85f, 0.28f, alpha)
        };
    }

    private Sprite GetSprite()
    {
        if (sprite != null)
        {
            return sprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        sprite = Sprite.Create(
            texture,
            new Rect(0, 0, 1, 1),
            new Vector2(0.5f, 0.5f),
            1f);
        sprite.name = "WildlifeHabitatOverlaySprite";
        return sprite;
    }
}
