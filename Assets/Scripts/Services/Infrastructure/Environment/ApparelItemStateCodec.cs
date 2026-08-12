using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class TextileBatchItemState
{
    public const int SchemaVersion = 2;

    public static ItemInstanceComponentSaveData Create(
        TextileConditionBand condition)
    {
        return new ItemInstanceComponentSaveData
        {
            componentTypeId = ItemInstanceComponentIds.FiberBatch,
            schemaVersion = SchemaVersion,
            affectsStacking = true,
            values = new List<ItemStateValueSaveData>
            {
                Integer("condition-band", (int)condition)
            }
        };
    }

    public static bool TryRead(
        IEnumerable<ItemInstanceComponentSaveData> components,
        out TextileConditionBand condition)
    {
        condition = TextileConditionBand.Ready;
        ItemInstanceComponentSaveData component = Find(
            components,
            ItemInstanceComponentIds.FiberBatch);
        if (component == null || component.schemaVersion != SchemaVersion)
        {
            return false;
        }

        int conditionValue = ReadInteger(component, "condition-band", -1);
        if (!Enum.IsDefined(typeof(TextileConditionBand), conditionValue))
        {
            return false;
        }

        condition = (TextileConditionBand)conditionValue;
        return true;
    }

    private static ItemStateValueSaveData Integer(string key, int value) => new()
    {
        key = key,
        kind = ItemStateValueKind.Integer,
        integerValue = value
    };

    internal static ItemInstanceComponentSaveData Find(
        IEnumerable<ItemInstanceComponentSaveData> components,
        string componentTypeId) => (components
            ?? Array.Empty<ItemInstanceComponentSaveData>())
        .FirstOrDefault(value => value != null
            && string.Equals(
                value.componentTypeId,
                componentTypeId,
                StringComparison.Ordinal));

    internal static int ReadInteger(
        ItemInstanceComponentSaveData component,
        string key,
        int fallback)
    {
        ItemStateValueSaveData field = component?.values?.FirstOrDefault(value =>
            value != null
            && value.kind == ItemStateValueKind.Integer
            && string.Equals(value.key, key, StringComparison.Ordinal));
        if (field == null
            || field.integerValue < int.MinValue
            || field.integerValue > int.MaxValue)
        {
            return fallback;
        }

        return (int)field.integerValue;
    }

    internal static string ReadString(
        ItemInstanceComponentSaveData component,
        string key,
        string fallback = "")
    {
        ItemStateValueSaveData field = component?.values?.FirstOrDefault(value =>
            value != null
            && value.kind == ItemStateValueKind.String
            && string.Equals(value.key, key, StringComparison.Ordinal));
        return field?.stringValue?.Trim() ?? fallback;
    }

    internal static double ReadDecimal(
        ItemInstanceComponentSaveData component,
        string key,
        double fallback)
    {
        ItemStateValueSaveData field = component?.values?.FirstOrDefault(value =>
            value != null
            && value.kind == ItemStateValueKind.Decimal
            && string.Equals(value.key, key, StringComparison.Ordinal));
        return field?.decimalValue ?? fallback;
    }
}

public static class ApparelItemStateCodec
{
    public const int SchemaVersion = 3;

    public static ItemInstanceComponentSaveData Create(ApparelInstanceState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        return new ItemInstanceComponentSaveData
        {
            componentTypeId = ItemInstanceComponentIds.Apparel,
            schemaVersion = SchemaVersion,
            affectsStacking = true,
            values = new List<ItemStateValueSaveData>
            {
                String("apparel-id", state.apparelDefinitionId),
                String("material-id", state.primaryMaterialId),
                Integer("craftsmanship-quality", (int)state.craftsmanshipQuality),
                Integer("source-kind", (int)state.sourceKind),
                String("source-id", state.sourceDefinitionId),
                Integer("size", (int)state.size),
                Integer("modifications", (int)state.modifications),
                Integer("closed-openings", (int)state.closedOpenings),
                Decimal("durability", Mathf.Clamp(state.durability, 0f, 100f)),
                Decimal("moisture", Mathf.Clamp(state.moisture, 0f, 100f)),
                Decimal("contamination", Mathf.Clamp(state.contamination, 0f, 100f)),
                String("designated-wearer", state.designatedWearerCharacterId),
                Integer("crafted-day", Math.Max(0, state.craftedAbsoluteDay)),
                String("batch-hash", state.deterministicBatchHash.ToString("X16")),
                String("mythic-maker", state.mythicProvenance?.makerCharacterId),
                Integer("mythic-trait", state.mythicProvenance?.sourceTraitId ?? 0),
                Integer("mythic-original-quality", (int)(state.mythicProvenance?.originalQuality
                    ?? CraftsmanshipQualityTier.Normal)),
                String("mythic-roll-hash", (state.mythicProvenance?.fixedRollHash ?? 0UL).ToString("X16")),
                Integer("mythic-created-day", Math.Max(0, state.mythicProvenance?.createdDay ?? 0)),
                String("mythic-facility", state.mythicProvenance?.createdFacilityId)
            }
        };
    }

    public static bool TryRead(
        IEnumerable<ItemInstanceComponentSaveData> components,
        out ApparelInstanceState state)
    {
        state = null;
        ItemInstanceComponentSaveData component = TextileBatchItemState.Find(
            components,
            ItemInstanceComponentIds.Apparel);
        if (component == null || component.schemaVersion < 2
            || component.schemaVersion > SchemaVersion)
        {
            return false;
        }

        int size = TextileBatchItemState.ReadInteger(component, "size", -1);
        int craftsmanshipQuality = TextileBatchItemState.ReadInteger(
            component,
            "craftsmanship-quality",
            (int)CraftsmanshipQualityTier.Normal);
        int sourceKind = TextileBatchItemState.ReadInteger(
            component,
            "source-kind",
            (int)TextileSourceKind.Unknown);
        int modifications = TextileBatchItemState.ReadInteger(
            component,
            "modifications",
            0);
        int closed = TextileBatchItemState.ReadInteger(
            component,
            "closed-openings",
            0);
        string apparelId = TextileBatchItemState.ReadString(component, "apparel-id");
        string materialId = TextileBatchItemState.ReadString(component, "material-id");
        if (apparelId.Length == 0
            || materialId.Length == 0
            || !Enum.IsDefined(typeof(ApparelSizeClass), size)
            || !Enum.IsDefined(typeof(CraftsmanshipQualityTier), craftsmanshipQuality)
            || !Enum.IsDefined(typeof(TextileSourceKind), sourceKind))
        {
            return false;
        }

        ulong.TryParse(
            TextileBatchItemState.ReadString(component, "batch-hash"),
            System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture,
            out ulong hash);
        string mythicMaker = TextileBatchItemState.ReadString(component, "mythic-maker");
        ulong.TryParse(
            TextileBatchItemState.ReadString(component, "mythic-roll-hash"),
            System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture,
            out ulong mythicRollHash);
        MythicProvenanceSaveData mythic = mythicMaker.Length == 0
            ? null
            : new MythicProvenanceSaveData
            {
                makerCharacterId = mythicMaker,
                sourceTraitId = TextileBatchItemState.ReadInteger(component, "mythic-trait", 0),
                originalQuality = (CraftsmanshipQualityTier)TextileBatchItemState.ReadInteger(
                    component,
                    "mythic-original-quality",
                    (int)CraftsmanshipQualityTier.Normal),
                fixedRollHash = mythicRollHash,
                createdDay = Math.Max(0, TextileBatchItemState.ReadInteger(
                    component,
                    "mythic-created-day",
                    0)),
                createdFacilityId = TextileBatchItemState.ReadString(component, "mythic-facility")
            };
        bool isMythic = craftsmanshipQuality == (int)CraftsmanshipQualityTier.Mythic;
        if (isMythic != (mythic != null)
            || (mythic != null
                && (mythic.sourceTraitId != MythicCraftInspirationRules.SourceTraitId
                    || !Enum.IsDefined(typeof(CraftsmanshipQualityTier), mythic.originalQuality)
                    || mythic.originalQuality == CraftsmanshipQualityTier.Mythic)))
        {
            return false;
        }
        state = new ApparelInstanceState
        {
            apparelDefinitionId = apparelId,
            primaryMaterialId = materialId,
            craftsmanshipQuality = (CraftsmanshipQualityTier)craftsmanshipQuality,
            sourceKind = (TextileSourceKind)sourceKind,
            sourceDefinitionId = TextileBatchItemState.ReadString(component, "source-id"),
            size = (ApparelSizeClass)size,
            modifications = (ApparelModificationKind)modifications,
            closedOpenings = (ApparelModificationKind)closed,
            durability = Mathf.Clamp(
                (float)TextileBatchItemState.ReadDecimal(component, "durability", 100d),
                0f,
                100f),
            moisture = Mathf.Clamp(
                (float)TextileBatchItemState.ReadDecimal(component, "moisture", 0d),
                0f,
                100f),
            contamination = Mathf.Clamp(
                (float)TextileBatchItemState.ReadDecimal(component, "contamination", 0d),
                0f,
                100f),
            designatedWearerCharacterId = TextileBatchItemState.ReadString(
                component,
                "designated-wearer"),
            craftedAbsoluteDay = Math.Max(
                0,
                TextileBatchItemState.ReadInteger(component, "crafted-day", 0)),
            deterministicBatchHash = hash,
            mythicProvenance = mythic
        };
        return true;
    }

    private static ItemStateValueSaveData String(string key, string value) => new()
    {
        key = key,
        kind = ItemStateValueKind.String,
        stringValue = value?.Trim() ?? string.Empty
    };

    private static ItemStateValueSaveData Integer(string key, int value) => new()
    {
        key = key,
        kind = ItemStateValueKind.Integer,
        integerValue = value
    };

    private static ItemStateValueSaveData Decimal(string key, double value) => new()
    {
        key = key,
        kind = ItemStateValueKind.Decimal,
        decimalValue = value
    };
}

public readonly struct ApparelProjectionKey : IEquatable<ApparelProjectionKey>
{
    public ApparelProjectionKey(
        int apparelCatalogIndex,
        int materialCatalogIndex,
        CraftsmanshipQualityTier craftsmanshipQuality,
        int durabilityBand,
        TextileConditionBand condition,
        ApparelModificationKind unusedOpenings,
        bool adjacentSize)
    {
        ApparelCatalogIndex = apparelCatalogIndex;
        MaterialCatalogIndex = materialCatalogIndex;
        CraftsmanshipQuality = craftsmanshipQuality;
        DurabilityBand = Mathf.Clamp(durabilityBand, 0, 4);
        Condition = condition;
        UnusedOpenings = unusedOpenings;
        AdjacentSize = adjacentSize;
    }

    public int ApparelCatalogIndex { get; }
    public int MaterialCatalogIndex { get; }
    public CraftsmanshipQualityTier CraftsmanshipQuality { get; }
    public int DurabilityBand { get; }
    public TextileConditionBand Condition { get; }
    public ApparelModificationKind UnusedOpenings { get; }
    public bool AdjacentSize { get; }

    public bool Equals(ApparelProjectionKey other) =>
        ApparelCatalogIndex == other.ApparelCatalogIndex
        && MaterialCatalogIndex == other.MaterialCatalogIndex
        && CraftsmanshipQuality == other.CraftsmanshipQuality
        && DurabilityBand == other.DurabilityBand
        && Condition == other.Condition
        && UnusedOpenings == other.UnusedOpenings
        && AdjacentSize == other.AdjacentSize;
    public override bool Equals(object obj) =>
        obj is ApparelProjectionKey other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(
        ApparelCatalogIndex,
        MaterialCatalogIndex,
        (int)CraftsmanshipQuality,
        DurabilityBand,
        (int)Condition,
        (int)UnusedOpenings,
        AdjacentSize);
}

public interface IApparelMaterialProjector
{
    ApparelDerivedStats GetOrCreate(ApparelProjectionKey key);
}

public sealed class ApparelMaterialProjector : IApparelMaterialProjector
{
    private readonly IApparelDefinitionCatalog apparel;
    private readonly ITextileMaterialCatalog materials;
    private readonly Dictionary<ApparelProjectionKey, ApparelDerivedStats> cache = new();

    public ApparelMaterialProjector(
        IApparelDefinitionCatalog apparel,
        ITextileMaterialCatalog materials)
    {
        this.apparel = apparel ?? throw new ArgumentNullException(nameof(apparel));
        this.materials = materials ?? throw new ArgumentNullException(nameof(materials));
    }

    public ApparelDerivedStats GetOrCreate(ApparelProjectionKey key)
    {
        if (cache.TryGetValue(key, out ApparelDerivedStats cached))
        {
            return cached;
        }

        if (key.ApparelCatalogIndex < 0
            || key.ApparelCatalogIndex >= apparel.Definitions.Count
            || key.MaterialCatalogIndex < 0
            || key.MaterialCatalogIndex >= materials.Definitions.Count)
        {
            return default;
        }

        ApparelDefinitionSO definition = apparel.Definitions[key.ApparelCatalogIndex];
        TextileMaterialDefinitionSO material = materials.Definitions[key.MaterialCatalogIndex];
        float quality = CraftsmanshipQualityRules.ProjectionMultiplier(
            key.CraftsmanshipQuality);
        float durability = key.DurabilityBand switch
        {
            4 => 1f,
            3 => 0.9f,
            2 => 0.75f,
            1 => 0.55f,
            _ => 0.25f
        };
        float condition = key.Condition switch
        {
            TextileConditionBand.Wet => 0.72f,
            TextileConditionBand.Contaminated => 0.55f,
            _ => 1f
        };
        float coefficient = definition.TailoringCoefficient;
        float warmth = material.Warmth * quality * durability * condition * coefficient;
        float water = material.WaterResistance * quality * durability * coefficient;
        if ((key.UnusedOpenings & ApparelModificationKind.WingSlits) != 0)
        {
            warmth -= 0.05f;
            water -= 0.08f;
        }
        if ((key.UnusedOpenings
             & (ApparelModificationKind.TailOpening
                | ApparelModificationKind.HornClearance)) != 0)
        {
            warmth -= 0.02f;
            water -= 0.03f;
        }

        ApparelDerivedStats created = new(
            warmth,
            material.HeatResistance * quality * durability * coefficient,
            water,
            material.AirborneResistance * quality * durability * condition * coefficient,
            material.Sterility * quality * condition * coefficient,
            material.Durability * quality * durability * coefficient,
            definition.BaseWeight * material.WeightMultiplier,
            key.AdjacentSize ? -5f : 0f,
            key.AdjacentSize ? 0.97f : 1f);
        cache.Add(key, created);
        return created;
    }
}
