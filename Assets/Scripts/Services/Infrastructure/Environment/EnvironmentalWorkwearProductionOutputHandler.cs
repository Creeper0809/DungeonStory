using System;
using System.Collections.Generic;
using System.Globalization;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class EnvironmentalWorkwearProductionOutputHandler :
    IProductionOutputHandler,
    IDomainFailureProductionOutputHandler
{
    private readonly IEnvironmentalWorkwearCatalog legacyCatalog;
    private readonly IApparelDefinitionCatalog apparelCatalog;
    private readonly ITextileMaterialCatalog materialCatalog;
    private readonly IWorldItemStackRuntime items;
    private readonly IGameClock clock;

    public EnvironmentalWorkwearProductionOutputHandler(
        IEnvironmentalWorkwearCatalog legacyCatalog,
        IApparelDefinitionCatalog apparelCatalog,
        ITextileMaterialCatalog materialCatalog,
        IWorldItemStackRuntime items,
        IGameClock clock)
    {
        this.legacyCatalog = legacyCatalog
            ?? throw new ArgumentNullException(nameof(legacyCatalog));
        this.apparelCatalog = apparelCatalog
            ?? throw new ArgumentNullException(nameof(apparelCatalog));
        this.materialCatalog = materialCatalog
            ?? throw new ArgumentNullException(nameof(materialCatalog));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public bool CanHandle(string itemId)
    {
        return apparelCatalog.TryGetByItemId(itemId, out _)
            || legacyCatalog.TryGetByItemDefinitionId(itemId, out _);
    }

    public bool TryProduce(
        ProductionOutputContext context,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (context.Facility == null
            || context.Amount <= 0
            || !apparelCatalog.TryGetByItemId(
                context.ItemId,
                out ApparelDefinitionSO apparel))
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentWorkwearProductionContextInvalid,
                context.ItemId ?? string.Empty,
                context.Amount.ToString(CultureInfo.InvariantCulture));
            return false;
        }

        string destinationId = ProductionBillRuntime.OutputDestinationPrefix
            + context.Facility.RequirePersistentInstanceId().Value;
        List<string> createdStackIds = new();
        for (int index = 0; index < context.Amount; index++)
        {
            if (items.SpawnUniqueItemAt(
                    context.ItemId,
                    context.Facility.centerPos,
                    WorldItemStackState.FacilityOutputBuffer,
                    destinationId,
                    out string stackId)
                && TryValidateUniquePhysicalOutput(stackId, context.ItemId)
                && TryAttachV22State(
                    stackId,
                    apparel,
                    context,
                    index))
            {
                createdStackIds.Add(stackId);
                continue;
            }

            foreach (string createdStackId in createdStackIds)
            {
                items.DeleteStack(createdStackId);
            }

            failure = new DomainFailure(
                FailureCode.EnvironmentWorkwearOutputSpawnFailed,
                context.ItemId,
                context.Amount.ToString(CultureInfo.InvariantCulture));
            return false;
        }

        return true;
    }

    private bool TryAttachV22State(
        string stackId,
        ApparelDefinitionSO apparel,
        ProductionOutputContext context,
        int outputIndex)
    {
        TextileMaterialDefinitionSO material = ResolvePrimaryMaterial(context.Recipe);
        if (material == null
            || (material.Tags & apparel.AllowedMaterialTags) == 0)
        {
            return false;
        }

        CraftsmanshipQualityTier craftsmanship = context.QualityModifier switch
        {
            >= 0.90f => CraftsmanshipQualityTier.Legendary,
            >= 0.65f => CraftsmanshipQualityTier.Masterwork,
            >= 0.35f => CraftsmanshipQualityTier.Excellent,
            >= 0.15f => CraftsmanshipQualityTier.Good,
            < -0.65f => CraftsmanshipQualityTier.Awful,
            < -0.30f => CraftsmanshipQualityTier.Poor,
            _ => CraftsmanshipQualityTier.Normal
        };
        int craftedDay = Mathf.Max(
            0,
            Mathf.FloorToInt(clock.Time / GameCalendarRules.SecondsPerDay));
        ApparelInstanceState state = new()
        {
            apparelDefinitionId = apparel.ApparelId,
            primaryMaterialId = material.MaterialId,
            craftsmanshipQuality = craftsmanship,
            sourceKind = ResolveSourceKind(material.Tags),
            sourceDefinitionId = material.MaterialId,
            size = ApparelSizeClass.Medium,
            modifications = ApparelModificationKind.None,
            closedOpenings = ApparelModificationKind.None,
            durability = 100f,
            moisture = 0f,
            contamination = 0f,
            craftedAbsoluteDay = craftedDay,
            deterministicBatchHash = DeterministicHash(
                context.Recipe?.RecipeId,
                context.Facility.RequirePersistentInstanceId().Value,
                craftedDay,
                outputIndex)
        };
        return items.TrySetInstanceComponent(
            stackId,
            ApparelItemStateCodec.Create(state));
    }

    private TextileMaterialDefinitionSO ResolvePrimaryMaterial(
        ProductionRecipeSO recipe)
    {
        foreach (ItemAmountDefinition input in recipe?.Inputs
                     ?? Array.Empty<ItemAmountDefinition>())
        {
            if (input != null
                && materialCatalog.TryGetByItemId(
                    input.ItemId,
                    out TextileMaterialDefinitionSO material))
            {
                return material;
            }
        }
        materialCatalog.TryGet("textile:shade-cloth", out TextileMaterialDefinitionSO fallback);
        return fallback;
    }

    private static TextileSourceKind ResolveSourceKind(TextileMaterialTag tags)
    {
        if ((tags & TextileMaterialTag.Animal) != 0) return TextileSourceKind.Animal;
        if ((tags & TextileMaterialTag.Arcane) != 0) return TextileSourceKind.Arcane;
        if ((tags & TextileMaterialTag.Plant) != 0) return TextileSourceKind.Crop;
        return TextileSourceKind.Unknown;
    }

    private static ulong DeterministicHash(
        string recipeId,
        string facilityId,
        int craftedDay,
        int outputIndex)
    {
        const ulong offset = 1469598103934665603UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        string source = $"{recipeId}|{facilityId}|{craftedDay}|{outputIndex}";
        for (int index = 0; index < source.Length; index++)
        {
            hash ^= source[index];
            hash *= prime;
        }
        return hash;
    }

    bool IProductionOutputHandler.TryProduce(
        ProductionOutputContext context,
        out string diagnosticCode)
    {
        bool succeeded = TryProduce(context, out DomainFailure failure);
        diagnosticCode = succeeded ? string.Empty : failure.Code.ToString();
        return succeeded;
    }

    private bool TryValidateUniquePhysicalOutput(string stackId, string itemId)
    {
        foreach (WorldItemStackSnapshot stack in items.GetAllStacks())
        {
            if (stack != null
                && string.Equals(stack.StackId, stackId, StringComparison.Ordinal)
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
                && stack.Quantity == 1
                && ((ItemInstanceId)stack.ItemInstanceId).IsValid)
            {
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(stackId))
        {
            items.DeleteStack(stackId);
        }
        return false;
    }
}
