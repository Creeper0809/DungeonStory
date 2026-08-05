using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CombatImpactPresentation : MonoBehaviour
{
    private static readonly Queue<CombatImpactPresentation> Pool =
        new Queue<CombatImpactPresentation>();
    [RuntimeRebuildableCache] private static Sprite impactSprite;

    private SpriteRenderer spriteRenderer;
    private Coroutine routine;
    private IGameClock gameClock;

    public static void Play(
        Vector3 worldPosition,
        CombatDamageType damageType,
        IGameClock gameClock,
        IWorldUiHierarchy worldUiHierarchy,
        bool coverHit = false)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        IWorldUiHierarchy requiredHierarchy = worldUiHierarchy
            ?? throw new System.ArgumentNullException(nameof(worldUiHierarchy));
        CombatImpactPresentation impact = Pool.Count > 0
            ? Pool.Dequeue()
            : Create(requiredHierarchy);
        impact.gameObject.SetActive(true);
        impact.gameClock = gameClock
            ?? throw new System.ArgumentNullException(nameof(gameClock));
        impact.Begin(worldPosition, damageType, coverHit);
    }

    private static CombatImpactPresentation Create(IWorldUiHierarchy worldUiHierarchy)
    {
        GameObject gameObject = new GameObject("Combat Impact");
        worldUiHierarchy.ParentToWorldUi(gameObject);
        CombatImpactPresentation impact = gameObject.AddComponent<CombatImpactPresentation>();
        impact.spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        impact.spriteRenderer.sprite = GetImpactSprite();
        impact.spriteRenderer.sortingLayerName = "UI";
        impact.spriteRenderer.sortingOrder = 19;
        return impact;
    }

    private void Begin(Vector3 worldPosition, CombatDamageType damageType, bool coverHit)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
        }

        transform.position = worldPosition + Vector3.up * (coverHit ? 0.42f : 0.55f);
        transform.localScale = Vector3.one * (coverHit ? 0.55f : 0.42f);
        transform.rotation = Quaternion.Euler(
            0f,
            0f,
            damageType switch
            {
                CombatDamageType.Pierce => 45f,
                CombatDamageType.Blunt => 0f,
                _ => -25f
            });
        spriteRenderer.color = coverHit
            ? new Color(0.78f, 0.68f, 0.49f, 1f)
            : damageType switch
            {
                CombatDamageType.Pierce => new Color(0.88f, 0.94f, 1f, 1f),
                CombatDamageType.Blunt => new Color(1f, 0.74f, 0.28f, 1f),
                _ => new Color(0.94f, 0.2f, 0.16f, 1f)
            };
        routine = StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        Color startColor = spriteRenderer.color;
        float elapsed = 0f;
        const float duration = 0.24f;
        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            transform.localScale = Vector3.one * Mathf.Lerp(0.28f, 0.72f, progress);
            spriteRenderer.color = new Color(
                startColor.r,
                startColor.g,
                startColor.b,
                1f - progress);
            elapsed += gameClock.DeltaTime;
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

        Texture2D texture = new Texture2D(7, 7, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "Runtime Combat Impact"
        };
        Color[] pixels = new Color[49];
        for (int y = 0; y < 7; y++)
        {
            for (int x = 0; x < 7; x++)
            {
                bool stroke = x == 3 || y == 3 || x == y || x + y == 6;
                pixels[y * 7 + x] = stroke && (x != 0 || y != 0)
                    ? Color.white
                    : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        impactSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            16f);
        impactSprite.name = "Runtime Combat Impact";
        return impactSprite;
    }
}
