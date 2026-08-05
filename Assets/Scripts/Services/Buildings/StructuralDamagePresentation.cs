using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public static class StructuralDamagePresentation
{
    public static void Present(
        BuildableObject building,
        BuildingStructuralIntegritySnapshot snapshot,
        float hitPointDelta,
        bool destroyed,
        bool reducedMotion)
    {
        if (!Application.isPlaying || building == null)
        {
            return;
        }

        StructuralCrackOverlay.Ensure(building).SetStage(
            snapshot.CrackStage);
        if (hitPointDelta > 0f)
        {
            StructuralImpactFx.Play(
                building.transform.position,
                building.GetInstanceID(),
                destroyed,
                reducedMotion);
        }
    }
}

public sealed class StructuralCrackOverlay : MonoBehaviour
{
    [RuntimeRebuildableCache] private static Sprite crackSprite;
    private SpriteRenderer crackRenderer;

    public static StructuralCrackOverlay Ensure(BuildableObject building)
    {
        StructuralCrackOverlay overlay =
            building.GetComponent<StructuralCrackOverlay>();
        if (overlay == null)
        {
            overlay = building.gameObject.AddComponent<
                StructuralCrackOverlay>();
        }

        overlay.EnsureRenderer(building);
        return overlay;
    }

    public void SetStage(BuildingCrackStage stage)
    {
        if (crackRenderer == null)
        {
            return;
        }

        crackRenderer.enabled = stage != BuildingCrackStage.None;
        crackRenderer.color = stage switch
        {
            BuildingCrackStage.Critical =>
                new Color(1f, 0.29f, 0.12f, 0.95f),
            BuildingCrackStage.Cracked =>
                new Color(0.88f, 0.58f, 0.26f, 0.85f),
            BuildingCrackStage.Hairline =>
                new Color(0.44f, 0.36f, 0.3f, 0.7f),
            _ => Color.clear
        };
        transform.localScale = Vector3.one * (stage switch
        {
            BuildingCrackStage.Critical => 1.15f,
            BuildingCrackStage.Cracked => 1f,
            _ => 0.82f
        });
    }

    private void EnsureRenderer(BuildableObject building)
    {
        if (crackRenderer != null)
        {
            return;
        }

        GameObject child = new GameObject("Structural Cracks");
        child.transform.SetParent(building.transform, false);
        child.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        crackRenderer = child.AddComponent<SpriteRenderer>();
        crackRenderer.sprite = GetCrackSprite();
        SpriteRenderer source = building.GetComponentInChildren<SpriteRenderer>();
        if (source != null)
        {
            crackRenderer.sortingLayerID = source.sortingLayerID;
            crackRenderer.sortingOrder = source.sortingOrder + 2;
        }
        else
        {
            crackRenderer.sortingLayerName = "UI";
            crackRenderer.sortingOrder = 2;
        }

        crackRenderer.enabled = false;
    }

    private static Sprite GetCrackSprite()
    {
        if (crackSprite != null)
        {
            return crackSprite;
        }

        Texture2D texture = new Texture2D(
            11,
            11,
            TextureFormat.RGBA32,
            false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "Runtime Structural Cracks"
        };
        Color[] pixels = new Color[121];
        int[,] strokes =
        {
            { 5, 10 }, { 5, 9 }, { 4, 8 }, { 5, 7 }, { 5, 6 },
            { 6, 5 }, { 5, 4 }, { 4, 3 }, { 3, 2 }, { 3, 1 },
            { 6, 5 }, { 7, 4 }, { 8, 4 }, { 9, 3 },
            { 5, 7 }, { 3, 6 }, { 2, 6 }, { 1, 5 }
        };
        for (int index = 0; index < strokes.GetLength(0); index++)
        {
            int x = strokes[index, 0];
            int y = strokes[index, 1];
            pixels[y * 11 + x] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply();
        crackSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 11f, 11f),
            new Vector2(0.5f, 0.5f),
            12f);
        return crackSprite;
    }
}

public sealed class StructuralImpactFx : MonoBehaviour
{
    private static readonly Queue<StructuralImpactFx> Pool =
        new Queue<StructuralImpactFx>();
    [RuntimeRebuildableCache] private static Sprite impactSprite;
    private SpriteRenderer impactRenderer;
    private Coroutine routine;

    public static void Play(
        Vector3 position,
        int sourceId,
        bool collapse,
        bool reducedMotion)
    {
        StructuralImpactFx fx = Pool.Count > 0
            ? Pool.Dequeue()
            : Create();
        fx.gameObject.SetActive(true);
        fx.Begin(position, sourceId, collapse, reducedMotion);
    }

    private static StructuralImpactFx Create()
    {
        GameObject owner = new GameObject("Structural Impact FX");
        DungeonRuntimeHierarchy.Parent(
            owner,
            DungeonRuntimeHierarchy.WorldUi);
        StructuralImpactFx fx =
            owner.AddComponent<StructuralImpactFx>();
        fx.impactRenderer = owner.AddComponent<SpriteRenderer>();
        fx.impactRenderer.sprite = GetImpactSprite();
        fx.impactRenderer.sortingLayerName = "UI";
        fx.impactRenderer.sortingOrder = 18;
        return fx;
    }

    private void Begin(
        Vector3 position,
        int sourceId,
        bool collapse,
        bool reducedMotion)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
        }

        int variant = Mathf.Abs(sourceId + Time.frameCount) % 5;
        float x = (variant - 2) * 0.13f;
        transform.position = position
            + new Vector3(x, 0.24f + variant * 0.05f, 0f);
        transform.rotation = Quaternion.Euler(
            0f,
            0f,
            -28f + variant * 14f);
        impactRenderer.color = collapse
            ? new Color(1f, 0.5f, 0.18f, 1f)
            : new Color(0.72f, 0.61f, 0.45f, 0.95f);
        routine = StartCoroutine(Animate(collapse, reducedMotion));
    }

    private IEnumerator Animate(bool collapse, bool reducedMotion)
    {
        float duration = collapse ? 0.48f : 0.25f;
        if (reducedMotion)
        {
            duration *= 0.6f;
        }

        Color start = impactRenderer.color;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            float scale = Mathf.Lerp(
                collapse ? 0.55f : 0.32f,
                collapse ? 1.35f : 0.78f,
                progress);
            transform.localScale = Vector3.one * scale;
            impactRenderer.color = new Color(
                start.r,
                start.g,
                start.b,
                1f - progress);
            if (!reducedMotion)
            {
                transform.position += new Vector3(
                    Mathf.Sin(progress * 22f) * 0.006f,
                    Time.deltaTime * 0.2f,
                    0f);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        routine = null;
        gameObject.SetActive(false);
        Pool.Enqueue(this);
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private static Sprite GetImpactSprite()
    {
        if (impactSprite != null)
        {
            return impactSprite;
        }

        Texture2D texture = new Texture2D(
            9,
            9,
            TextureFormat.RGBA32,
            false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "Runtime Structural Dust"
        };
        Color[] pixels = new Color[81];
        for (int y = 0; y < 9; y++)
        {
            for (int x = 0; x < 9; x++)
            {
                int distance = Mathf.Abs(x - 4) + Mathf.Abs(y - 4);
                bool shard = distance <= 1
                    || (distance <= 4 && (x + y) % 3 == 0);
                pixels[y * 9 + x] = shard
                    ? Color.white
                    : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        impactSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 9f, 9f),
            new Vector2(0.5f, 0.5f),
            16f);
        return impactSprite;
    }
}
