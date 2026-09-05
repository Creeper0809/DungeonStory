using System;
using System.Collections.Generic;
using System.Linq;

public static class WildlifeFeedSelectionRules
{
    public static bool IsAllowed(
        WildlifeDietType diet,
        ResourceItemDefinitionSO item)
    {
        if (item == null || item.StockCategory != StockCategory.Food)
        {
            return false;
        }

        ResourceIngredientTag tags = item.IngredientTags;
        bool plant = (tags & (ResourceIngredientTag.Plant
            | ResourceIngredientTag.Fungus)) != 0;
        bool animal = (tags & (ResourceIngredientTag.Meat
            | ResourceIngredientTag.Blood
            | ResourceIngredientTag.Fat
            | ResourceIngredientTag.Egg
            | ResourceIngredientTag.Milk)) != 0;
        bool spoiled = (tags & ResourceIngredientTag.Spoiled) != 0;

        return diet switch
        {
            WildlifeDietType.Herbivore => plant && !animal,
            WildlifeDietType.Carnivore => animal,
            WildlifeDietType.Scavenger => animal || spoiled,
            _ => plant || animal
        };
    }

    public static int GetPreference(
        WildlifeDietType diet,
        ResourceItemDefinitionSO item)
    {
        if (!IsAllowed(diet, item))
        {
            return int.MaxValue;
        }

        if (item.FacilityFeedEligible)
        {
            return 0;
        }

        if (item.Kind == ResourceItemKind.FinishedGood)
        {
            return 1;
        }

        return item.Kind == ResourceItemKind.Food ? 3 : 2;
    }

    public static ResourceItemDefinitionSO SelectPreferred(
        WildlifeDietType diet,
        IEnumerable<ResourceItemDefinitionSO> candidates,
        Func<string, int> projectedBalance)
    {
        if (candidates == null)
        {
            throw new ArgumentNullException(nameof(candidates));
        }

        projectedBalance ??= _ => 0;
        return candidates
            .Where(item => IsAllowed(diet, item))
            .OrderBy(item => GetPreference(diet, item))
            .ThenByDescending(item => projectedBalance(item.ItemId))
            .ThenBy(item => item.UnitPrice)
            .ThenBy(item => item.ItemId, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
