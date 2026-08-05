using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CombatProjectilePresentation : MonoBehaviour
{
    private static readonly Queue<CombatProjectilePresentation> Pool =
        new Queue<CombatProjectilePresentation>();
    [RuntimeRebuildableCache] private static Sprite projectileSprite;

    private SpriteRenderer spriteRenderer;
    private Coroutine routine;
    private IGameClock gameClock;

    public static void Launch(
        Vector3 start,
        Vector3 end,
        float speed,
        CombatDamageType damageType,
        bool arcing,
        IGameClock gameClock,
        IWorldUiHierarchy worldUiHierarchy)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        IWorldUiHierarchy requiredHierarchy = worldUiHierarchy
            ?? throw new System.ArgumentNullException(nameof(worldUiHierarchy));
        CombatProjectilePresentation projectile = Pool.Count > 0
            ? Pool.Dequeue()
            : Create(requiredHierarchy);
        projectile.gameObject.SetActive(true);
        projectile.gameClock = gameClock
            ?? throw new System.ArgumentNullException(nameof(gameClock));
        projectile.Begin(start, end, speed, damageType, arcing);
    }

    private static CombatProjectilePresentation Create(IWorldUiHierarchy worldUiHierarchy)
    {
        GameObject gameObject = new GameObject("Combat Projectile");
        worldUiHierarchy.ParentToWorldUi(gameObject);
        CombatProjectilePresentation projectile = gameObject.AddComponent<CombatProjectilePresentation>();
        projectile.spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        projectile.spriteRenderer.sprite = GetProjectileSprite();
        projectile.spriteRenderer.sortingLayerName = "UI";
        projectile.spriteRenderer.sortingOrder = 18;
        return projectile;
    }

    private static Sprite GetProjectileSprite()
    {
        if (projectileSprite != null)
        {
            return projectileSprite;
        }

        Texture2D texture = new Texture2D(6, 2, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "Runtime Combat Projectile"
        };
        texture.SetPixels(new[]
        {
            Color.clear, new Color(0.95f, 0.79f, 0.38f), new Color(0.95f, 0.79f, 0.38f),
            new Color(0.95f, 0.79f, 0.38f), Color.white, Color.clear,
            Color.clear, new Color(0.35f, 0.22f, 0.12f), new Color(0.55f, 0.34f, 0.18f),
            new Color(0.55f, 0.34f, 0.18f), new Color(0.82f, 0.86f, 0.88f), Color.clear
        });
        texture.Apply();
        projectileSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            16f);
        projectileSprite.name = "Runtime Combat Projectile";
        return projectileSprite;
    }

    private void Begin(
        Vector3 start,
        Vector3 end,
        float speed,
        CombatDamageType damageType,
        bool arcing)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
        }

        transform.position = start + Vector3.up * 0.55f;
        Vector3 target = end + Vector3.up * 0.55f;
        Vector3 direction = target - transform.position;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        spriteRenderer.color = damageType switch
        {
            CombatDamageType.Blunt => new Color(0.85f, 0.72f, 0.42f),
            CombatDamageType.Pierce => new Color(0.88f, 0.92f, 0.98f),
            _ => new Color(0.98f, 0.65f, 0.34f)
        };
        routine = StartCoroutine(Fly(target, Mathf.Max(2f, speed), arcing));
    }

    private IEnumerator Fly(Vector3 target, float speed, bool arcing)
    {
        Vector3 start = transform.position;
        float duration = Mathf.Max(0.05f, Vector3.Distance(start, target) / speed);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            Vector3 position = Vector3.Lerp(start, target, progress);
            if (arcing)
            {
                position += Vector3.up * (Mathf.Sin(progress * Mathf.PI) * 0.65f);
            }

            transform.position = position;
            elapsed += gameClock.DeltaTime;
            yield return null;
        }

        transform.position = target;
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
}
