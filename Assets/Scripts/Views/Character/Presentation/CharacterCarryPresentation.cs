using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterCarryPresentation : MonoBehaviour
{
    private const float PixelSize =
        1f / WorldInteractionPresentationCatalogSO.PixelsPerUnit;

    private CharacterActor actor;
    private CharacterCarryInventory inventory;
    private CharacterVisual visual;
    private WorldInteractionPresentationCatalogSO catalog;
    private IDungeonItemCatalogProvider itemCatalog;
    private SpriteRenderer propRenderer;
    private SpriteRenderer itemBadgeRenderer;
    private bool dirty = true;
    private bool visible;
    private bool lastFlipX;
    private int lastSortingLayerId;
    private int lastSortingOrder;
    private int refreshCount;

    public int RefreshCount => refreshCount;
    public SpriteRenderer PropRenderer => propRenderer;
    public SpriteRenderer ItemBadgeRenderer => itemBadgeRenderer;

    public static CharacterCarryPresentation Ensure(
        CharacterActor actor,
        WorldInteractionPresentationCatalogSO catalog)
    {
        if (actor == null)
        {
            return null;
        }

        CharacterCarryPresentation presenter =
            actor.GetComponent<CharacterCarryPresentation>();
        if (presenter == null && Application.isPlaying)
        {
            presenter = actor.gameObject.AddComponent<CharacterCarryPresentation>();
        }

        presenter?.Configure(actor, catalog);
        return presenter;
    }

    public void Configure(
        CharacterActor actor,
        WorldInteractionPresentationCatalogSO catalog)
    {
        Unsubscribe();
        this.actor = actor;
        visual = actor != null ? actor.GetComponent<CharacterVisual>() : null;
        inventory = CharacterCarryInventory.Ensure(actor);
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
        itemCatalog = actor?.WorldItemStackRuntime?.CatalogProvider;
        EnsureRenderers();
        Subscribe();
        dirty = true;
    }

    public void TickPresentation(bool isVisible)
    {
        if (visible != isVisible)
        {
            visible = isVisible;
            if (!visible)
            {
                SetRenderersEnabled(false);
                return;
            }

            dirty = true;
        }

        if (!visible || visual == null || visual.VisualRenderer == null)
        {
            return;
        }

        SpriteRenderer characterRenderer = visual.VisualRenderer;
        bool sortingChanged = lastSortingLayerId != characterRenderer.sortingLayerID
            || lastSortingOrder != characterRenderer.sortingOrder;
        bool facingChanged = lastFlipX != characterRenderer.flipX;
        if (dirty || sortingChanged || facingChanged)
        {
            RefreshPresentation();
        }
    }

    public void ResetPresentation()
    {
        SetRenderersEnabled(false);
        dirty = true;
    }

    private void RefreshPresentation()
    {
        dirty = false;
        refreshCount++;
        if (inventory == null || !inventory.HasItems)
        {
            SetRenderersEnabled(false);
            return;
        }

        CharacterCarriedItemSaveData primary = SelectPrimaryItem();
        if (primary == null)
        {
            SetRenderersEnabled(false);
            return;
        }

        DungeonItemDefinition definition = null;
        if (itemCatalog != null)
        {
            itemCatalog.TryGetDefinition(primary.itemId, out definition);
        }
        CharacterCarryVisualKind kind = ResolveCarryKind(
            definition != null ? definition.StockCategory : StockCategory.General,
            inventory.GetLoadRatio());
        CharacterPropAttachmentProfile profile =
            catalog.ResolvePropProfile(actor != null ? actor.SpeciesTag : "default", kind);
        EnsureRenderers();

        SpriteRenderer characterRenderer = visual.VisualRenderer;
        bool flipX = characterRenderer.flipX;
        Vector2 offset = profile.rightFacingOffsetPixels;
        if (!flipX && profile.mirrorOffsetX)
        {
            offset.x = -offset.x;
        }

        propRenderer.transform.localPosition =
            new Vector3(offset.x * PixelSize, offset.y * PixelSize, 0f);
        propRenderer.transform.localScale = ResolveLoadScale(
            profile,
            inventory.GetLoadRatio());
        propRenderer.flipX = profile.synchronizeFlipX && !flipX;
        propRenderer.sprite = ResolvePropSprite(kind);
        propRenderer.sortingLayerID = characterRenderer.sortingLayerID;
        propRenderer.sortingOrder = characterRenderer.sortingOrder
            + ResolveSortingOffset(profile, flipX);
        propRenderer.enabled = propRenderer.sprite != null;

        itemBadgeRenderer.sprite = definition?.Sprite;
        itemBadgeRenderer.transform.localPosition =
            propRenderer.transform.localPosition
            + new Vector3(0f, 4f * PixelSize, 0f);
        itemBadgeRenderer.transform.localScale = Vector3.one * 0.5f;
        itemBadgeRenderer.flipX = false;
        itemBadgeRenderer.sortingLayerID = characterRenderer.sortingLayerID;
        itemBadgeRenderer.sortingOrder = propRenderer.sortingOrder + 1;
        itemBadgeRenderer.enabled = itemBadgeRenderer.sprite != null;

        lastFlipX = flipX;
        lastSortingLayerId = characterRenderer.sortingLayerID;
        lastSortingOrder = characterRenderer.sortingOrder;
    }

    private CharacterCarriedItemSaveData SelectPrimaryItem()
    {
        CharacterCarriedItemSaveData selected = null;
        float selectedWeight = float.MinValue;
        for (int i = 0; i < inventory.Items.Count; i++)
        {
            CharacterCarriedItemSaveData item = inventory.Items[i];
            if (item == null || item.quantity <= 0)
            {
                continue;
            }

            float unitWeight = 1f;
            if (itemCatalog != null
                && itemCatalog.TryGetDefinition(item.itemId, out DungeonItemDefinition definition))
            {
                unitWeight = definition.UnitWeight;
            }

            float weight = unitWeight * item.quantity;
            if (selected == null
                || weight > selectedWeight
                || (Mathf.Approximately(weight, selectedWeight)
                    && string.CompareOrdinal(item.itemId, selected.itemId) < 0))
            {
                selected = item;
                selectedWeight = weight;
            }
        }

        return selected;
    }

    private void EnsureRenderers()
    {
        if (propRenderer == null)
        {
            propRenderer = CreateRenderer("CarryProp");
        }

        if (itemBadgeRenderer == null)
        {
            itemBadgeRenderer = CreateRenderer("CarryItemBadge");
        }
    }

    private SpriteRenderer CreateRenderer(string objectName)
    {
        Transform existing = transform.Find(objectName);
        GameObject target = existing != null
            ? existing.gameObject
            : new GameObject(objectName);
        target.transform.SetParent(
            visual != null && visual.VisualRoot != null
                ? visual.VisualRoot
                : transform,
            worldPositionStays: false);
        SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = target.AddComponent<SpriteRenderer>();
        }

        renderer.enabled = false;
        return renderer;
    }

    private static CharacterCarryVisualKind ResolveCarryKind(
        StockCategory category,
        float loadRatio)
    {
        if (loadRatio >= 0.82f)
        {
            return CharacterCarryVisualKind.Backpack;
        }

        return category switch
        {
            StockCategory.Food or StockCategory.Medicine
                or StockCategory.Water => CharacterCarryVisualKind.Tray,
            StockCategory.Fuel or StockCategory.Biological
                or StockCategory.General => CharacterCarryVisualKind.Sack,
            _ => CharacterCarryVisualKind.Crate
        };
    }

    private static Sprite ResolvePropSprite(CharacterCarryVisualKind kind)
    {
        CharacterPresentationSpriteKind spriteKind = kind switch
        {
            CharacterCarryVisualKind.Tray => CharacterPresentationSpriteKind.Tray,
            CharacterCarryVisualKind.Sack => CharacterPresentationSpriteKind.Sack,
            CharacterCarryVisualKind.Backpack => CharacterPresentationSpriteKind.Backpack,
            _ => CharacterPresentationSpriteKind.Crate
        };
        return CharacterPresentationSpriteFactory.Get(spriteKind);
    }

    private static int ResolveSortingOffset(
        CharacterPropAttachmentProfile profile,
        bool facingRight)
    {
        if (profile.sortingMode == CharacterPropSortingMode.FacingSide)
        {
            return facingRight ? 1 : -1;
        }

        return profile.sortingOrderOffset;
    }

    private static Vector3 ResolveLoadScale(
        CharacterPropAttachmentProfile profile,
        float loadRatio)
    {
        if (loadRatio < 0.34f)
        {
            return profile.lightLoadScale;
        }

        return loadRatio >= 0.82f
            ? profile.overloadedScale
            : profile.normalLoadScale;
    }

    private void SetRenderersEnabled(bool enabled)
    {
        if (propRenderer != null)
        {
            propRenderer.enabled = enabled && propRenderer.sprite != null;
        }

        if (itemBadgeRenderer != null)
        {
            itemBadgeRenderer.enabled = enabled && itemBadgeRenderer.sprite != null;
        }
    }

    private void Subscribe()
    {
        if (inventory != null)
        {
            inventory.Changed += HandleInventoryChanged;
        }
    }

    private void Unsubscribe()
    {
        if (inventory != null)
        {
            inventory.Changed -= HandleInventoryChanged;
        }
    }

    private void HandleInventoryChanged()
    {
        dirty = true;
    }

    private void OnDisable()
    {
        visible = false;
        ResetPresentation();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }
}
