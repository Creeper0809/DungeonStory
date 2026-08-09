#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

/// <summary>
/// Maintains V21 installation BOMs and removes the former fabricated guest
/// sinks. Operational tools and medical supplies are consumed only by their
/// owning domain command, never by an unrelated guest request.
/// </summary>
public static class V21OperationalContentLinkBuilder
{
    private static readonly IReadOnlyDictionary<string, string> InstallationComponents =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["research:climate:environment-control"] = "component:climate-control-manifold",
            ["research:defense:corridor-mechanisms"] = "component:corridor-detonator",
            ["research:medical:construct-core-engineering"] = "component:golem-core-case",
            ["research:industry:electric-lighting"] = "component:insulated-wiring",
            ["research:plumbing:reuse"] = "component:reclaimed-water-filter",
            ["research:housing:room-assignment"] = "component:room-partition-kit",
            ["research:plumbing:rune-purification"] = "component:rune-purification-crystal",
            ["research:survival:seasonal-storage"] = "component:sealed-seasonal-container",
            ["research:defense:siege-fortification"] = "component:siege-reinforcement-kit",
            ["research:industry:waterwheel"] = "component:waterwheel-drive-shaft"
        };
    private static readonly IReadOnlyDictionary<string, string[]> FormerFabricatedGuestSupplyIds =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["guest-request:sealed-archive"] = new[]
            {
                "record:arcane-index",
                "tool:administrative-seal",
                "record:career-ledger",
                "book:seasonal-almanac"
            },
            ["guest-request:persecuted-family"] = new[]
            {
                "record:breeding-ledger"
            },
            ["guest-request:memorial-performance"] = new[]
            {
                "supply:funeral-preparation-kit",
                "supply:performance-prop-box",
                "tool:banquet-cart"
            },
            ["guest-request:precision-barter"] = new[]
            {
                "component:material-test-coupon",
                "tool:inspection-gauge",
                "tool:rune-identification-lens"
            },
            ["guest-request:bodyguard-kit"] = new[]
            {
                "tool:hauling-harness",
                "tool:prisoner-work-kit",
                "tool:reinforced-restraint",
                "tool:watch-signal-horn"
            },
            ["guest-request:militia-arms"] = new[]
            {
                "supply:alliance-signal-kit",
                "supply:defense-mixed-ammo-box"
            },
            ["guest-request:disease-sample"] = new[]
            {
                "medical:trait-analysis-kit",
                "medical:cross-lineage-medium"
            },
            ["guest-request:emergency-surgery"] = new[]
            {
                "medical:fertility-treatment",
                "medical:isolation-care-kit",
                "medical:organ-preservation-canister",
                "medical:organ-regeneration-scaffold",
                "medical:rejuvenation-serum",
                "medical:rune-hibernation-catalyst",
                "medical:trauma-care-kit"
            },
            ["guest-request:allergen-banquet"] = new[]
            {
                "supply:certified-seed-kit",
                "supply:greenhouse-nutrient"
            },
            ["guest-request:winter-fuel-auction"] = new[]
            {
                "supply:inoculated-log"
            },
            ["guest-request:flood-refuge"] = new[]
            {
                "tool:weather-observation-kit"
            }
        };

    public static void EnsureAssets()
    {
        Dictionary<string, GuestRequestDefinitionSO> guests = AssetDatabase
            .FindAssets("t:GuestRequestDefinitionSO")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<GuestRequestDefinitionSO>)
            .Where(value => value != null)
            .ToDictionary(value => value.StableId, StringComparer.Ordinal);
        HashSet<string> managedItemIds = FormerFabricatedGuestSupplyIds.Values
            .SelectMany(value => value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (GuestRequestDefinitionSO guest in guests.Values)
        {
            guest.serviceRequirements ??= new V20ContentRequirementSet();
            guest.serviceRequirements.items ??= new List<V20ItemAmountRequirement>();
            guest.serviceRequirements.items.RemoveAll(value =>
                value != null && managedItemIds.Contains(value.itemDefinitionId?.Trim() ?? string.Empty));
        }

        foreach (GuestRequestDefinitionSO guest in guests.Values)
        {
            EditorUtility.SetDirty(guest);
        }
    }

    public static void WireInstallationComponents(
        IReadOnlyDictionary<string, ResearchProjectSO> projects)
    {
        Dictionary<int, BuildingSO> buildings = AssetDatabase
            .FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(value => value != null)
            .GroupBy(value => value.id)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (KeyValuePair<string, string> pair in InstallationComponents)
        {
            if (projects == null
                || !projects.TryGetValue(pair.Key, out ResearchProjectSO project))
            {
                throw new InvalidOperationException(
                    $"V21 installation component owner '{pair.Key}' is missing.");
            }

            int[] buildingIds = project.Unlocks
                .OfType<BlueprintBuildingUnlock>()
                .Select(value => value.buildingId)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            if (buildingIds.Length == 0)
            {
                throw new InvalidOperationException(
                    $"V21 installation component '{pair.Value}' has no unlocked facility.");
            }

            foreach (int buildingId in buildingIds)
            {
                if (!buildings.TryGetValue(buildingId, out BuildingSO building))
                {
                    throw new InvalidOperationException(
                        $"V21 installation target building '{buildingId}' is missing.");
                }
                BuildingWorkAmountAbility work =
                    building.GetAbility<BuildingWorkAmountAbility>();
                if (work == null)
                {
                    throw new InvalidOperationException(
                        $"V21 installation target building '{buildingId}' has no construction contract.");
                }
                List<ItemAmountDefinition> materials = work.ConstructionMaterials
                    .Select(value => new ItemAmountDefinition(value.ItemId, value.Amount))
                    .ToList();
                if (materials.All(value => !string.Equals(
                        value.ItemId,
                        pair.Value,
                        StringComparison.Ordinal)))
                {
                    materials.Add(new ItemAmountDefinition(pair.Value, 1));
                    work.SetConstructionMaterials(materials);
                    EditorUtility.SetDirty(building);
                }
            }
        }
    }
}
#endif
