using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
public sealed class WasteOriginRuleRecord
{
    public WasteOriginKind origin;
    public WasteDispositionKind defaultDisposition;
    [Range(0f, 100f)] public float defaultMaximumFeedContamination;
    public List<WasteDispositionKind> supportedDispositions = new();
}

[Serializable]
public sealed class WasteRecipeBindingRecord
{
    public WasteOriginKind origin;
    public WasteDispositionKind disposition;
    public string recipeId = string.Empty;
}

[Serializable]
public sealed class WasteFeedRuleRecord
{
    public WildlifeDietType diet;
    public WasteOriginKind origin;
    [Range(0f, 1f)] public float nutrition;
    [Range(0f, 1f)] public float diseaseChance;
}

[Serializable]
public sealed class LegacyWasteItemRuleRecord
{
    public string itemId = string.Empty;
    public WasteOriginKind origin;
    [Range(0f, 100f)] public float contamination;
}

[CreateAssetMenu(
    fileName = "WasteProcessingRules",
    menuName = "DungeonStory/Economy/Waste Processing Rules",
    order = 40)]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WasteProcessingRulesSO : ScriptableObject
{
    [SerializeField, Min(0.1f)] private float tickIntervalSeconds;
    [SerializeField, Range(0f, 100f)] private float toxicThreshold;
    [SerializeField] private List<WasteOriginRuleRecord> origins = new();
    [SerializeField] private List<WasteRecipeBindingRecord> recipes = new();
    [SerializeField] private List<WasteFeedRuleRecord> feedRules = new();
    [SerializeField] private List<LegacyWasteItemRuleRecord> legacyItems = new();

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (!IsFinite(tickIntervalSeconds) || tickIntervalSeconds <= 0f)
        {
            errors.Add("Waste tick interval must be finite and positive.");
        }
        if (!IsFinite(toxicThreshold)
            || toxicThreshold <= 0f
            || toxicThreshold > 100f)
        {
            errors.Add("Waste toxic threshold must be in (0, 100].");
        }

        WasteOriginKind[] requiredOrigins = Enum.GetValues(typeof(WasteOriginKind))
            .Cast<WasteOriginKind>()
            .Where(origin => origin != WasteOriginKind.Unknown)
            .ToArray();
        if (origins == null
            || origins.Count != requiredOrigins.Length
            || origins.Any(rule => rule == null
                || rule.origin == WasteOriginKind.Unknown
                || !requiredOrigins.Contains(rule.origin)
                || rule.supportedDispositions == null
                || rule.supportedDispositions.Count == 0
                || rule.supportedDispositions.Distinct().Count()
                    != rule.supportedDispositions.Count
                || !rule.supportedDispositions.Contains(rule.defaultDisposition)
                || !IsFinite(rule.defaultMaximumFeedContamination)
                || rule.defaultMaximumFeedContamination < 0f
                || rule.defaultMaximumFeedContamination >= toxicThreshold)
            || origins.Select(rule => rule.origin).Distinct().Count()
                != requiredOrigins.Length)
        {
            errors.Add(
                "Waste origin rules must cover every concrete origin exactly once with valid defaults.");
        }

        ValidateRecipeBindings(errors);
        ValidateFeedRules(errors);
        ValidateLegacyItems(errors);
        return errors;
    }

    public WasteProcessingRulesDefinition CreateRuntimeDefinition()
    {
        IReadOnlyList<string> errors = ValidateDefinition();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Cannot project invalid waste-processing rules:\n"
                + string.Join("\n", errors));
        }
        return new WasteProcessingRulesDefinition(
            tickIntervalSeconds,
            toxicThreshold,
            origins,
            recipes,
            feedRules,
            legacyItems);
    }

    private void ValidateRecipeBindings(ICollection<string> errors)
    {
        if (recipes == null
            || recipes.Any(rule => rule == null
                || rule.origin == WasteOriginKind.Unknown
                || rule.disposition is WasteDispositionKind.Store
                    or WasteDispositionKind.DirectFeed
                || !IsCanonical(rule.recipeId))
            || recipes.GroupBy(
                    rule => (rule.origin, rule.disposition))
                .Any(group => group.Count() != 1))
        {
            errors.Add("Waste recipe bindings must be concrete and unique.");
            return;
        }

        foreach (WasteOriginRuleRecord origin in origins ?? new())
        {
            foreach (WasteDispositionKind disposition in
                     origin?.supportedDispositions ?? new())
            {
                bool requiresRecipe = disposition is not (
                    WasteDispositionKind.Store
                    or WasteDispositionKind.DirectFeed);
                bool hasBinding = recipes.Any(rule => rule.origin == origin.origin
                    && rule.disposition == disposition);
                if (requiresRecipe != hasBinding)
                {
                    errors.Add(
                        $"Waste origin '{origin.origin}' disposition '{disposition}' has inconsistent recipe authority.");
                }
            }
        }
    }

    private void ValidateFeedRules(ICollection<string> errors)
    {
        if (feedRules == null
            || feedRules.Count == 0
            || feedRules.Any(rule => rule == null
                || rule.origin == WasteOriginKind.Unknown
                || !IsFinite(rule.nutrition)
                || !IsFinite(rule.diseaseChance)
                || rule.nutrition <= 0f
                || rule.nutrition > 1f
                || rule.diseaseChance < 0f
                || rule.diseaseChance > 1f)
            || feedRules.GroupBy(rule => (rule.diet, rule.origin))
                .Any(group => group.Count() != 1))
        {
            errors.Add("Waste feed rules must be finite, positive, and unique.");
        }
    }

    private void ValidateLegacyItems(ICollection<string> errors)
    {
        if (legacyItems == null
            || legacyItems.Any(rule => rule == null
                || !IsCanonical(rule.itemId)
                || rule.origin == WasteOriginKind.Unknown
                || !IsFinite(rule.contamination)
                || rule.contamination < 0f
                || rule.contamination > 100f)
            || legacyItems.GroupBy(rule => rule.itemId, StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
        {
            errors.Add("Legacy waste-item mappings must be canonical and unique.");
        }
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}

public interface IWasteProcessingRules
{
    float TickIntervalSeconds { get; }
    float ToxicThreshold { get; }
    IReadOnlyCollection<WasteOriginKind> Origins { get; }
    WastePolicyData CreateDefaultPolicy(WasteOriginKind origin);
    bool IsSupported(
        WasteOriginKind origin,
        WasteDispositionKind disposition);
    bool TryGetRecipeId(
        WasteOriginKind origin,
        WasteDispositionKind disposition,
        out string recipeId);
    bool IsWasteRecipe(string recipeId);
    bool TryGetFeedValues(
        WildlifeDietType diet,
        WasteOriginKind origin,
        out float nutrition,
        out float diseaseChance);
    bool TryGetLegacyWaste(
        string itemId,
        out WasteOriginKind origin,
        out float contamination);
}

public sealed class WasteProcessingRulesDefinition : IWasteProcessingRules
{
    private readonly IReadOnlyDictionary<WasteOriginKind, WasteOriginRuleRecord>
        origins;
    private readonly IReadOnlyDictionary<
        (WasteOriginKind Origin, WasteDispositionKind Disposition),
        string> recipes;
    private readonly IReadOnlyDictionary<
        (WildlifeDietType Diet, WasteOriginKind Origin),
        WasteFeedRuleRecord> feeds;
    private readonly IReadOnlyDictionary<string, LegacyWasteItemRuleRecord>
        legacyItems;
    private readonly HashSet<string> recipeIds;

    public WasteProcessingRulesDefinition(
        float tickIntervalSeconds,
        float toxicThreshold,
        IEnumerable<WasteOriginRuleRecord> origins,
        IEnumerable<WasteRecipeBindingRecord> recipes,
        IEnumerable<WasteFeedRuleRecord> feeds,
        IEnumerable<LegacyWasteItemRuleRecord> legacyItems)
    {
        TickIntervalSeconds = tickIntervalSeconds;
        ToxicThreshold = toxicThreshold;
        this.origins = origins.ToDictionary(
            rule => rule.origin,
            CloneOrigin);
        this.recipes = recipes.ToDictionary(
            rule => (rule.origin, rule.disposition),
            rule => rule.recipeId,
            EqualityComparer<(WasteOriginKind, WasteDispositionKind)>.Default);
        this.feeds = feeds.ToDictionary(
            rule => (rule.diet, rule.origin),
            CloneFeed);
        this.legacyItems = legacyItems.ToDictionary(
            rule => rule.itemId,
            CloneLegacy,
            StringComparer.Ordinal);
        recipeIds = new HashSet<string>(this.recipes.Values, StringComparer.Ordinal);
        Origins = Array.AsReadOnly(
            this.origins.Keys.OrderBy(origin => origin).ToArray());
    }

    public float TickIntervalSeconds { get; }
    public float ToxicThreshold { get; }
    public IReadOnlyCollection<WasteOriginKind> Origins { get; }

    public WastePolicyData CreateDefaultPolicy(WasteOriginKind origin)
    {
        if (!origins.TryGetValue(origin, out WasteOriginRuleRecord rule))
        {
            throw new KeyNotFoundException(
                $"No authored waste-origin rule exists for '{origin}'.");
        }
        return new WastePolicyData
        {
            origin = origin,
            disposition = rule.defaultDisposition,
            enabled = true,
            maximumFeedContamination = rule.defaultMaximumFeedContamination
        };
    }

    public bool IsSupported(
        WasteOriginKind origin,
        WasteDispositionKind disposition) =>
        origins.TryGetValue(origin, out WasteOriginRuleRecord rule)
        && rule.supportedDispositions.Contains(disposition);

    public bool TryGetRecipeId(
        WasteOriginKind origin,
        WasteDispositionKind disposition,
        out string recipeId) =>
        recipes.TryGetValue((origin, disposition), out recipeId);

    public bool IsWasteRecipe(string recipeId) =>
        recipeIds.Contains(recipeId?.Trim() ?? string.Empty);

    public bool TryGetFeedValues(
        WildlifeDietType diet,
        WasteOriginKind origin,
        out float nutrition,
        out float diseaseChance)
    {
        if (!feeds.TryGetValue((diet, origin), out WasteFeedRuleRecord rule))
        {
            nutrition = 0f;
            diseaseChance = 0f;
            return false;
        }
        nutrition = rule.nutrition;
        diseaseChance = rule.diseaseChance;
        return true;
    }

    public bool TryGetLegacyWaste(
        string itemId,
        out WasteOriginKind origin,
        out float contamination)
    {
        if (!legacyItems.TryGetValue(
                itemId?.Trim() ?? string.Empty,
                out LegacyWasteItemRuleRecord rule))
        {
            origin = WasteOriginKind.Unknown;
            contamination = 0f;
            return false;
        }
        origin = rule.origin;
        contamination = rule.contamination;
        return true;
    }

    private static WasteOriginRuleRecord CloneOrigin(WasteOriginRuleRecord source) =>
        new()
        {
            origin = source.origin,
            defaultDisposition = source.defaultDisposition,
            defaultMaximumFeedContamination =
                source.defaultMaximumFeedContamination,
            supportedDispositions = source.supportedDispositions.ToList()
        };

    private static WasteFeedRuleRecord CloneFeed(WasteFeedRuleRecord source) =>
        new()
        {
            diet = source.diet,
            origin = source.origin,
            nutrition = source.nutrition,
            diseaseChance = source.diseaseChance
        };

    private static LegacyWasteItemRuleRecord CloneLegacy(
        LegacyWasteItemRuleRecord source) => new()
        {
            itemId = source.itemId,
            origin = source.origin,
            contamination = source.contamination
        };
}

public sealed class GameContentWasteProcessingRules : IWasteProcessingRules
{
    private readonly WasteProcessingRulesDefinition definition;

    public GameContentWasteProcessingRules(IGameContentDefinitionSource content)
    {
        definition = (content
            ?? throw new ArgumentNullException(nameof(content)))
            .RequireSingle<WasteProcessingRulesSO>()
            .CreateRuntimeDefinition();
    }

    public float TickIntervalSeconds => definition.TickIntervalSeconds;
    public float ToxicThreshold => definition.ToxicThreshold;
    public IReadOnlyCollection<WasteOriginKind> Origins => definition.Origins;
    public WastePolicyData CreateDefaultPolicy(WasteOriginKind origin) =>
        definition.CreateDefaultPolicy(origin);
    public bool IsSupported(WasteOriginKind origin, WasteDispositionKind disposition) =>
        definition.IsSupported(origin, disposition);
    public bool TryGetRecipeId(
        WasteOriginKind origin,
        WasteDispositionKind disposition,
        out string recipeId) =>
        definition.TryGetRecipeId(origin, disposition, out recipeId);
    public bool IsWasteRecipe(string recipeId) =>
        definition.IsWasteRecipe(recipeId);
    public bool TryGetFeedValues(
        WildlifeDietType diet,
        WasteOriginKind origin,
        out float nutrition,
        out float diseaseChance) =>
        definition.TryGetFeedValues(
            diet,
            origin,
            out nutrition,
            out diseaseChance);
    public bool TryGetLegacyWaste(
        string itemId,
        out WasteOriginKind origin,
        out float contamination) =>
        definition.TryGetLegacyWaste(itemId, out origin, out contamination);
}
