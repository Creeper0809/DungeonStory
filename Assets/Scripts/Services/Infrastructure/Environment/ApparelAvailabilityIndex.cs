using System;
using System.Collections.Generic;

public readonly struct ApparelSelectionQuery
{
    public ApparelSelectionQuery(
        AnatomyAttachmentPoint availablePoints,
        ApparelSizeClass size,
        ApparelLayer layer,
        ApparelUseTag useTags,
        TextileMaterialTag materialTags,
        CraftsmanshipQualityTier minimumQuality,
        bool allowWet = false)
    {
        AvailablePoints = availablePoints;
        Size = size;
        Layer = layer;
        UseTags = useTags;
        MaterialTags = materialTags;
        MinimumQuality = minimumQuality;
        AllowWet = allowWet;
    }

    public AnatomyAttachmentPoint AvailablePoints { get; }
    public ApparelSizeClass Size { get; }
    public ApparelLayer Layer { get; }
    public ApparelUseTag UseTags { get; }
    public TextileMaterialTag MaterialTags { get; }
    public CraftsmanshipQualityTier MinimumQuality { get; }
    public bool AllowWet { get; }
}

public readonly struct ApparelCandidate
{
    public ApparelCandidate(
        ItemInstanceId itemInstanceId,
        string stackId,
        int apparelCatalogIndex,
        int materialCatalogIndex,
        ApparelSizeClass size,
        CraftsmanshipQualityTier quality,
        TextileConditionBand condition,
        float durability)
    {
        ItemInstanceId = itemInstanceId;
        StackId = stackId ?? string.Empty;
        ApparelCatalogIndex = apparelCatalogIndex;
        MaterialCatalogIndex = materialCatalogIndex;
        Size = size;
        Quality = quality;
        Condition = condition;
        Durability = durability;
    }

    public ItemInstanceId ItemInstanceId { get; }
    public string StackId { get; }
    public int ApparelCatalogIndex { get; }
    public int MaterialCatalogIndex { get; }
    public ApparelSizeClass Size { get; }
    public CraftsmanshipQualityTier Quality { get; }
    public TextileConditionBand Condition { get; }
    public float Durability { get; }
}

public interface IApparelAvailabilityIndex
{
    int ApparelStockVersion { get; }
    int FindCandidates(ApparelSelectionQuery query, Span<ApparelCandidate> destination);
    void Invalidate();
}

public sealed class ApparelAvailabilityIndex : IApparelAvailabilityIndex
{
    private const int MaximumReturnedCandidates = 8;

    private readonly IWorldItemStackRuntime items;
    private readonly IApparelDefinitionCatalog apparel;
    private readonly ITextileMaterialCatalog materials;
    private readonly List<ApparelCandidate>[] byLayer;
    private bool dirty = true;
    private int apparelStockVersion;

    public ApparelAvailabilityIndex(
        IWorldItemStackRuntime items,
        IApparelDefinitionCatalog apparel,
        ITextileMaterialCatalog materials)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.apparel = apparel ?? throw new ArgumentNullException(nameof(apparel));
        this.materials = materials ?? throw new ArgumentNullException(nameof(materials));
        byLayer = new List<ApparelCandidate>[Enum.GetValues(typeof(ApparelLayer)).Length];
        for (int index = 0; index < byLayer.Length; index++)
        {
            byLayer[index] = new List<ApparelCandidate>(32);
        }
    }

    public int ApparelStockVersion => apparelStockVersion;

    public void Invalidate()
    {
        dirty = true;
    }

    public int FindCandidates(
        ApparelSelectionQuery query,
        Span<ApparelCandidate> destination)
    {
        RebuildIfRequired();
        int limit = Math.Min(Math.Min(destination.Length, MaximumReturnedCandidates), 8);
        if (limit <= 0 || (int)query.Layer < 0 || (int)query.Layer >= byLayer.Length)
        {
            return 0;
        }

        List<ApparelCandidate> source = byLayer[(int)query.Layer];
        int written = 0;
        for (int index = 0; index < source.Count && written < limit; index++)
        {
            ApparelCandidate candidate = source[index];
            ApparelDefinitionSO definition = apparel.Definitions[candidate.ApparelCatalogIndex];
            TextileMaterialDefinitionSO material = materials.Definitions[candidate.MaterialCatalogIndex];
            if ((query.AvailablePoints & definition.RequiredPoints) != definition.RequiredPoints
                || definition.FitMode == ApparelFitMode.Sized && candidate.Size != query.Size
                || definition.FitMode == ApparelFitMode.Adjustable
                    && Math.Abs((int)candidate.Size - (int)query.Size) > 1
                || query.UseTags != ApparelUseTag.None
                    && (definition.UseTags & query.UseTags) == 0
                || query.MaterialTags != TextileMaterialTag.None
                    && (material.Tags & query.MaterialTags) == 0
                || candidate.Quality < query.MinimumQuality
                || candidate.Condition == TextileConditionBand.Contaminated
                || !query.AllowWet && candidate.Condition == TextileConditionBand.Wet)
            {
                continue;
            }
            destination[written++] = candidate;
        }
        return written;
    }

    private void RebuildIfRequired()
    {
        if (!dirty)
        {
            return;
        }
        foreach (List<ApparelCandidate> list in byLayer)
        {
            list.Clear();
        }
        IReadOnlyList<WorldItemStackSnapshot> stacks = items.GetAllStacks();
        for (int index = 0; index < stacks.Count; index++)
        {
            WorldItemStackSnapshot stack = stacks[index];
            if (stack == null
                || stack.IsReserved
                || stack.Quantity != 1
                || !((ItemInstanceId)stack.ItemInstanceId).IsValid
                || !apparel.TryGetByItemId(stack.ItemId, out ApparelDefinitionSO definition)
                || !ApparelItemStateCodec.TryRead(stack.Components, out ApparelInstanceState state)
                || !materials.TryGet(state.primaryMaterialId, out TextileMaterialDefinitionSO material))
            {
                continue;
            }
            int apparelIndex = apparel.GetIndex(definition.ApparelId);
            int materialIndex = materials.GetIndex(material.MaterialId);
            if (apparelIndex < 0 || materialIndex < 0)
            {
                continue;
            }
            byLayer[(int)definition.Layer].Add(new ApparelCandidate(
                (ItemInstanceId)stack.ItemInstanceId,
                stack.StackId,
                apparelIndex,
                materialIndex,
                state.size,
                state.craftsmanshipQuality,
                TextileConditionRules.ResolveCondition(state.moisture, state.contamination),
                state.durability));
        }
        foreach (List<ApparelCandidate> list in byLayer)
        {
            list.Sort(CompareCandidates);
        }
        apparelStockVersion++;
        dirty = false;
    }

    private static int CompareCandidates(ApparelCandidate left, ApparelCandidate right)
    {
        int quality = right.Quality.CompareTo(left.Quality);
        if (quality != 0) return quality;
        int durability = right.Durability.CompareTo(left.Durability);
        if (durability != 0) return durability;
        return StringComparer.Ordinal.Compare(left.StackId, right.StackId);
    }
}
