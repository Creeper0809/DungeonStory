#if UNITY_EDITOR
using System;
using System.Collections.Generic;

/// <summary>
/// Authoring-only map for the V21 research tree. This rewrites content assets during
/// the V21 rebuild; it is deliberately not a runtime alias or save migration table.
/// </summary>
public static class V21ResearchConsolidation
{
    private static readonly IReadOnlyDictionary<string, string> SurvivorByAbsorbed =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["research:agriculture:seed-selection"] = "research:agriculture:phenology",
            ["research:agriculture:pest-control"] = "research:agriculture:soil-cycles",
            ["research:agriculture:crop-pathology"] = "research:agriculture:soil-cycles",
            ["research:life:infant-care"] = "research:society:household-records",
            ["research:housing:family-quarters"] = "research:housing:room-assignment",
            ["research:society:apprenticeship"] = "research:society:child-education",
            ["research:housing:guardian-succession"] = "research:society:generation-management",
            ["research:society:funeral-rites"] = "research:society:corpse-care",
            ["research:society:mentor-academy"] = "research:society:retirement",
            ["research:medical:biological-age-measurement"] = "research:medical:gerontology",
            ["research:medical:chronic-care"] = "research:medical:geriatric-medicine",
            ["research:health:immunoserology"] = "research:health:pathogen-observation",
            ["research:health:epidemic-control"] = "research:health:vaccination",
            ["research:genetics:trait-analysis"] = "research:genetics:hereditary-records",
            ["research:climate:chronometric-navigation"] = "research:climate:regional-climatology",
            ["research:industry:distribution"] = "research:industry:steam-power",
            ["research:industry:factory-layout"] = "research:industry:powered-tools",
            ["research:equipment:engineering-drawing"] = "research:industry:powered-tools",
            ["research:industry:maintenance"] = "research:industry:breakers",
            ["research:industry:ports"] = "research:industry:conveyor",
            ["research:industry:stock-sensors"] = "research:industry:automatic-bills",
            ["research:industry:industrial-cooling"] = "research:industry:electric-smelting",
            ["research:equipment:relic-restoration"] = "research:equipment:relic-appraisal",
            ["research:equipment:blast-protection"] = "research:equipment:pressure-barrels",
            ["research:plumbing:storage-valves"] = "research:plumbing:basics",
            ["research:plumbing:pumped-water"] = "research:plumbing:basics",
            ["research:plumbing:settling"] = "research:plumbing:sewer",
            ["research:plumbing:flush-sanitation"] = "research:plumbing:sewer",
            ["research:industry:filters"] = "research:industry:junctions",
            ["research:industry:priority-gates"] = "research:industry:junctions",
            ["research:industry:overflow"] = "research:industry:lifts",
            ["research:industry:high-speed-belts"] = "research:industry:lifts",
            ["research:industry:transformers"] = "research:industry:storage",
            ["research:industry:rune-grid"] = "research:industry:mana-power",
            ["research:industry:safety"] = "research:industry:automatic-sanitation",
            ["research:industry:defense-supply"] = "research:industry:line-balancing"
        };

    public static IReadOnlyDictionary<string, string> AbsorbedToSurvivor =>
        SurvivorByAbsorbed;

    public static string Normalize(string researchId)
    {
        string normalized = researchId?.Trim() ?? string.Empty;
        return SurvivorByAbsorbed.TryGetValue(normalized, out string survivor)
            ? survivor
            : normalized;
    }
}
#endif
