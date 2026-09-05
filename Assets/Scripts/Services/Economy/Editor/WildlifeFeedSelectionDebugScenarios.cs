using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WildlifeFeedSelectionDebugScenarios
{
    [MenuItem("Tools/Dungeon Story/QA/Economy/Run Wildlife Feed Selection Scenarios")]
    public static void RunAll()
    {
        ResourceItemDefinitionSO[] items = AssetDatabase
            .FindAssets("t:ResourceItemDefinitionSO", new[]
            {
                "Assets/Resources/SO/Economy/Items"
            })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>)
            .Where(item => item != null)
            .ToArray();

        ResourceItemDefinitionSO regular = Require(items, "feed:dog-food");
        ResourceItemDefinitionSO fresh = Require(
            items,
            "feed:dog-food-fresh");
        ResourceItemDefinitionSO hay = Require(items, "feed:hay");

        RequirePrimaryFeed(regular);
        RequirePrimaryFeed(fresh);
        RequirePrimaryFeed(hay);

        foreach (WildlifeDietType diet in new[]
                 {
                     WildlifeDietType.Carnivore,
                     WildlifeDietType.Omnivore,
                     WildlifeDietType.Scavenger
                 })
        {
            RequireAllowedPrimary(diet, regular);
            RequireAllowedPrimary(diet, fresh);
        }

        RequireAllowedPrimary(WildlifeDietType.Herbivore, hay);
        if (WildlifeFeedSelectionRules.IsAllowed(
                WildlifeDietType.Herbivore,
                fresh))
        {
            throw new InvalidOperationException(
                "Mixed plant/meat fresh dog food was accepted for herbivores.");
        }

        ResourceItemDefinitionSO[] dogFeeds = { regular, fresh };
        Dictionary<string, int> balances = new(StringComparer.Ordinal)
        {
            [regular.ItemId] = 0,
            [fresh.ItemId] = 12
        };
        ResourceItemDefinitionSO selected = WildlifeFeedSelectionRules
            .SelectPreferred(
                WildlifeDietType.Omnivore,
                dogFeeds,
                itemId => balances[itemId]);
        RequireSelected(selected, fresh.ItemId, "stock-backed selection");

        balances[fresh.ItemId] = 0;
        selected = WildlifeFeedSelectionRules.SelectPreferred(
            WildlifeDietType.Omnivore,
            dogFeeds,
            itemId => balances[itemId]);
        RequireSelected(selected, regular.ItemId, "stable lower-price tie break");

        Debug.Log(
            "[WildlifeFeedSelectionDebugScenarios] PASS "
            + "data-driven primary feeds, diet eligibility, fresh-stock forecast "
            + "selection, and deterministic tie break.");
    }

    private static ResourceItemDefinitionSO Require(
        IEnumerable<ResourceItemDefinitionSO> items,
        string itemId)
    {
        return items.SingleOrDefault(item => string.Equals(
                   item.ItemId,
                   itemId,
                   StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Missing feed definition '{itemId}'.");
    }

    private static void RequirePrimaryFeed(ResourceItemDefinitionSO item)
    {
        if (!item.FacilityFeedEligible)
        {
            throw new InvalidOperationException(
                $"Feed '{item.ItemId}' is not authored as feed eligible.");
        }
    }

    private static void RequireAllowedPrimary(
        WildlifeDietType diet,
        ResourceItemDefinitionSO item)
    {
        if (!WildlifeFeedSelectionRules.IsAllowed(diet, item)
            || WildlifeFeedSelectionRules.GetPreference(diet, item) != 0)
        {
            throw new InvalidOperationException(
                $"Feed '{item.ItemId}' is not primary for diet '{diet}'.");
        }
    }

    private static void RequireSelected(
        ResourceItemDefinitionSO selected,
        string expectedItemId,
        string scenario)
    {
        if (selected == null
            || !string.Equals(
                selected.ItemId,
                expectedItemId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unexpected {scenario}: expected='{expectedItemId}', "
                + $"actual='{selected?.ItemId ?? "<null>"}'.");
        }
    }
}
