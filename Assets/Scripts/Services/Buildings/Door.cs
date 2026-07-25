using System.Collections.Generic;
using UnityEngine;

public static class DoorVisualMaterial
{
    public const string ResourcePath = "Materials/DoorSpriteUnlit";
    public const string ShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";

    private static Material material;

    public static void Apply(SpriteRenderer renderer, IResourcesAssetLoader assetLoader = null)
    {
        if (renderer == null)
        {
            return;
        }

        if (material == null)
        {
            material = assetLoader?.LoadOptional<Material>(ResourcePath);
        }

        if (material == null)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader != null)
            {
                material = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
        }

        if (material != null)
        {
            renderer.sharedMaterial = material;
        }
    }
}

public static class DungeonDoorVisualLayout
{
    public const string VisualObjectName = "DungeonDoorVisual";
    public const string SortingLayerName = "Wall";
    public const int SortingOrder = 99;
    public const string CeilingSortingLayerName = "Wall";
    public const int CeilingSortingOrder = 100;
    public const string TraversalSortingLayerName = "DungeonMiddleObject";
    public const string DefaultCharacterSortingLayerName = "Default";
    public const float VisualWidth = 3f;
    public const float VisualHeight = 3f;
    public static readonly Vector2 TraversalColliderSize = new Vector2(3f, 1f);
    public static readonly Vector2 TraversalColliderOffset = new Vector2(2f, 0.5f);

    public static Vector3 CalculateScale(Sprite sprite)
    {
        if (sprite == null || sprite.bounds.size.x <= 0f || sprite.bounds.size.y <= 0f)
        {
            return Vector3.one;
        }

        return new Vector3(
            VisualWidth / sprite.bounds.size.x,
            VisualHeight / sprite.bounds.size.y,
            1f);
    }
}

public class Door : BuildableObject
{
    public SpriteRenderer VisualRenderer { get; protected set; }
    public virtual bool IsDungeonEntrance => true;
    protected virtual bool ChangesCharacterLayerDuringTraversal => true;

    private readonly HashSet<CharacterActor> traversalActors = new HashSet<CharacterActor>();
    private readonly HashSet<WildlifeActor> traversalWildlife = new HashSet<WildlifeActor>();
    private IResourcesAssetLoader resourcesAssetLoader;

    protected IResourcesAssetLoader ResourcesAssetLoader => resourcesAssetLoader;

    [VContainer.Inject]
    public void ConstructDoorVisualResources(IResourcesAssetLoader resourcesAssetLoader)
    {
        this.resourcesAssetLoader = resourcesAssetLoader;
    }

    private void OnEnable()
    {
        if (!IsDungeonEntrance)
        {
            return;
        }

        RemoveLegacyInteriorVisual("DoorVisual");
        RemoveLegacyInteriorVisual(InteriorDoorVisualLayout.VisualObjectName);
        if (BuildingData != null)
        {
            ConfigureDungeonVisual(BuildingData);
            ConfigureTraversalCollider();
        }
    }

    private void OnDisable()
    {
        RestoreTrackedCharacterLayers();
    }

    public override void Initialization(BuildingSO buildingSO, Vector2Int buildPos)
    {
        base.Initialization(buildingSO, buildPos);
        if (IsDungeonEntrance)
        {
            ConfigureDungeonVisual(buildingSO);
            ConfigureTraversalCollider();
        }

        BoxCollider2D doorCollider = GetComponent<BoxCollider2D>();
        if (doorCollider != null)
        {
            doorCollider.isTrigger = true;
        }
    }

    private void ConfigureTraversalCollider()
    {
        BoxCollider2D doorCollider = GetComponent<BoxCollider2D>();
        if (doorCollider == null)
        {
            return;
        }

        doorCollider.isTrigger = true;
        doorCollider.size = DungeonDoorVisualLayout.TraversalColliderSize;
        doorCollider.offset = DungeonDoorVisualLayout.TraversalColliderOffset;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        CharacterActor actor = ResolveTraversalActor(collision);
        if (actor != null)
        {
            KeepCharacterBehindWall(actor);
        }

        WildlifeActor wildlife = ResolveTraversalWildlife(collision);
        if (wildlife != null)
        {
            KeepWildlifeBehindWall(wildlife);
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        CharacterActor actor = ResolveTraversalActor(collision);
        if (actor != null)
        {
            KeepCharacterBehindWall(actor);
        }

        WildlifeActor wildlife = ResolveTraversalWildlife(collision);
        if (wildlife != null)
        {
            KeepWildlifeBehindWall(wildlife);
        }
    }

    private void RemoveLegacyInteriorVisual(string childName)
    {
        Transform legacyVisual = transform.Find(childName);
        if (legacyVisual == null)
        {
            return;
        }

        legacyVisual.gameObject.SetActive(false);
        if (Application.isPlaying)
        {
            Destroy(legacyVisual.gameObject);
        }
        else
        {
            DestroyImmediate(legacyVisual.gameObject);
        }
    }

    private void ConfigureDungeonVisual(BuildingSO buildingSO)
    {
        Sprite sprite = buildingSO != null
            ? buildingSO.sprite != null ? buildingSO.sprite : buildingSO.icon
            : null;
        Transform visualTransform = transform.Find(DungeonDoorVisualLayout.VisualObjectName);
        if (visualTransform == null)
        {
            GameObject visualObject = new GameObject(DungeonDoorVisualLayout.VisualObjectName);
            visualTransform = visualObject.transform;
            visualTransform.SetParent(transform, false);
        }

        VisualRenderer = visualTransform.GetComponent<SpriteRenderer>();
        if (VisualRenderer == null)
        {
            VisualRenderer = visualTransform.gameObject.AddComponent<SpriteRenderer>();
        }

        visualTransform.localPosition = Vector3.zero;
        visualTransform.localRotation = Quaternion.identity;
        visualTransform.localScale = DungeonDoorVisualLayout.CalculateScale(sprite);
        VisualRenderer.sprite = sprite;
        VisualRenderer.color = Color.white;
        DoorVisualMaterial.Apply(VisualRenderer, resourcesAssetLoader);
        VisualRenderer.sortingLayerName = DungeonDoorVisualLayout.SortingLayerName;
        VisualRenderer.sortingOrder = DungeonDoorVisualLayout.SortingOrder;
        VisualRenderer.enabled = sprite != null;
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        CharacterActor actor = ResolveTraversalActor(collision);
        if (actor != null)
        {
            traversalActors.Remove(actor);
            actor.ChangeLayer(DungeonDoorVisualLayout.DefaultCharacterSortingLayerName);
        }

        WildlifeActor wildlife = ResolveTraversalWildlife(collision);
        if (wildlife != null)
        {
            traversalWildlife.Remove(wildlife);
            wildlife.ChangeLayer(DungeonDoorVisualLayout.DefaultCharacterSortingLayerName);
        }
    }

    private CharacterActor ResolveTraversalActor(Collider2D collision)
    {
        if (BuildingData == null
            || !ChangesCharacterLayerDuringTraversal
            || collision == null)
        {
            return null;
        }

        CharacterActor actor = collision.GetComponentInParent<CharacterActor>();
        return actor != null && actor.CompareTag("Character") ? actor : null;
    }

    private WildlifeActor ResolveTraversalWildlife(Collider2D collision)
    {
        if (BuildingData == null
            || !ChangesCharacterLayerDuringTraversal
            || collision == null)
        {
            return null;
        }

        WildlifeActor wildlife = collision.GetComponentInParent<WildlifeActor>();
        return wildlife != null && wildlife.CanEnterDungeon ? wildlife : null;
    }

    private void KeepCharacterBehindWall(CharacterActor actor)
    {
        if (actor == null)
        {
            return;
        }

        traversalActors.Add(actor);
        actor.ChangeLayer(DungeonDoorVisualLayout.TraversalSortingLayerName);
    }

    private void KeepWildlifeBehindWall(WildlifeActor wildlife)
    {
        if (wildlife == null)
        {
            return;
        }

        traversalWildlife.Add(wildlife);
        wildlife.ChangeLayer(DungeonDoorVisualLayout.TraversalSortingLayerName);
    }

    private void RestoreTrackedCharacterLayers()
    {
        foreach (CharacterActor actor in traversalActors)
        {
            if (actor != null)
            {
                actor.ChangeLayer(DungeonDoorVisualLayout.DefaultCharacterSortingLayerName);
            }
        }

        traversalActors.Clear();

        foreach (WildlifeActor wildlife in traversalWildlife)
        {
            if (wildlife != null)
            {
                wildlife.ChangeLayer(DungeonDoorVisualLayout.DefaultCharacterSortingLayerName);
            }
        }

        traversalWildlife.Clear();
    }
}
