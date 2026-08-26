using System;
using System.Collections.Generic;
using System.Linq;

public interface IProductionMaximumOutputFactorCatalog
{
    int SupportDefinitionCount { get; }
    string SourceDigest { get; }
    ProductionOutputFactor ResolveMaximum(ProductionRecipeSO recipe);
    string CaptureRecipeSourceDigest(ProductionRecipeSO recipe);
}

/// <summary>
/// Immutable authored maximum for production output modifiers. Current content has
/// only unit-valued support output factors. A non-unit support is rejected until a
/// stable required-tag/provider combination solver is introduced.
/// </summary>
public sealed class ProductionMaximumOutputFactorCatalog :
    IProductionMaximumOutputFactorCatalog
{
    public const string SourceDigestSchemaToken =
        "production-maximum-output-factor-catalog@1";
    public const string NonUnitSupportFailureCode =
        "NON_UNIT_SUPPORT_MAXIMUM_REQUIRES_DP";

    private readonly SupportProfile[] supports;

    public ProductionMaximumOutputFactorCatalog(IGameContentCatalog content)
        : this((content ?? throw new ArgumentNullException(nameof(content)))
            .GetAll<BuildingSO>())
    {
    }

    public ProductionMaximumOutputFactorCatalog(
        IEnumerable<BuildingSO> buildings)
    {
        if (buildings == null)
            throw new ArgumentNullException(nameof(buildings));

        supports = buildings
            .Where(value => value != null)
            .Select(value => value.GetProductionSupportAbility())
            .Where(value => value != null)
            .Select(CreateProfile)
            .OrderBy(value => value.SupportId, StringComparer.Ordinal)
            .ToArray();
        if (supports.Select(value => value.SupportId)
            .Distinct(StringComparer.Ordinal).Count() != supports.Length)
        {
            throw new InvalidOperationException(
                "Production support catalog contains duplicate support IDs.");
        }
        SupportProfile nonUnit = supports.FirstOrDefault(value =>
            !value.OutputFactor.Equals(ProductionOutputFactor.One));
        if (nonUnit != null)
        {
            throw new InvalidOperationException(
                $"{NonUnitSupportFailureCode}: support '{nonUnit.SupportId}' "
                + $"has output factor {nonUnit.OutputFactor}.");
        }
        CanonicalSemanticDigestBuilder canonical = new();
        canonical.Append(SourceDigestSchemaToken);
        canonical.Append(supports.Length);
        foreach (SupportProfile support in supports)
            support.AppendTo(canonical);
        SourceDigest = canonical.ComputeSha256();
    }

    public int SupportDefinitionCount => supports.Length;
    public string SourceDigest { get; }

    public ProductionOutputFactor ResolveMaximum(ProductionRecipeSO recipe)
    {
        if (recipe == null)
            throw new ArgumentNullException(nameof(recipe));

        string[] requiredTags = CaptureRequiredTags(recipe);
        for (int index = 0; index < requiredTags.Length; index++)
        {
            string requiredTag = requiredTags[index];
            bool providerExists = supports.Any(value =>
                value.Supports(recipe.WorkstationTag, requiredTag));
            if (!providerExists)
            {
                throw new InvalidOperationException(
                    $"Production recipe '{recipe.RecipeId}' has no authored support "
                    + $"provider for '{requiredTag}' at '{recipe.WorkstationTag}'.");
            }
        }

        return ProductionOutputFactorAuthority.ResolveMaximumGrandProject(
            recipe.FacilityTag);
    }

    public string CaptureRecipeSourceDigest(ProductionRecipeSO recipe)
    {
        ProductionOutputFactor maximum = ResolveMaximum(recipe);
        string[] requiredTags = CaptureRequiredTags(recipe);
        SupportProfile[] providers = supports
            .Where(value => requiredTags.Any(tag =>
                value.Supports(recipe.WorkstationTag, tag)))
            .OrderBy(value => value.SupportId, StringComparer.Ordinal)
            .ToArray();
        CanonicalSemanticDigestBuilder canonical = new();
        canonical.Append("production-maximum-output-factor-recipe-source@1");
        canonical.Append(recipe.WorkstationTag);
        canonical.Append(recipe.FacilityTag);
        canonical.Append(requiredTags.Length);
        foreach (string tag in requiredTags)
            canonical.Append(tag);
        canonical.Append(providers.Length);
        foreach (SupportProfile provider in providers)
            provider.AppendTo(canonical);
        canonical.Append(maximum.Numerator);
        canonical.Append(maximum.Denominator);
        return canonical.ComputeSha256();
    }

    private static string[] CaptureRequiredTags(ProductionRecipeSO recipe)
    {
        string[] source = (recipe.RequiredSupportTags
                ?? Array.Empty<string>())
            .Concat(string.IsNullOrEmpty(recipe.BatchSupportTag)
                ? Array.Empty<string>()
                : new[] { recipe.BatchSupportTag })
            .ToArray();
        if (source.Any(value => string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Production recipe '{recipe.RecipeId}' has noncanonical support tags.");
        }
        string[] canonical = source
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return canonical;
    }

    private static SupportProfile CreateProfile(
        BuildingProductionSupportAbility ability)
    {
        if (ability == null
            || !ability.IsValid
            || string.IsNullOrEmpty(ability.SupportId)
            || !string.Equals(
                ability.supportId,
                ability.SupportId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Production support has a noncanonical or invalid identity.");
        }

        string[] featureTags = CanonicalizeTags(
            ability.featureTags,
            ability.SupportId,
            "feature");
        string[] workstationTags = CanonicalizeTags(
            ability.compatibleWorkstationTags,
            ability.SupportId,
            "workstation");
        return new SupportProfile(
            ability.SupportId,
            featureTags,
            workstationTags,
            ProductionOutputFactor.FromAuthoredMultiplier(
                ability.outputMultiplier));
    }

    private static string[] CanonicalizeTags(
        IEnumerable<string> values,
        string supportId,
        string role)
    {
        string[] source = (values ?? Array.Empty<string>()).ToArray();
        if (source.Length == 0
            || source.Any(value => string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Production support '{supportId}' has invalid {role} tags.");
        }
        string[] canonical = source
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (canonical.Length != source.Length)
        {
            throw new InvalidOperationException(
                $"Production support '{supportId}' has duplicate {role} tags.");
        }
        return canonical;
    }

    private sealed class SupportProfile
    {
        public SupportProfile(
            string supportId,
            IReadOnlyCollection<string> featureTags,
            IReadOnlyCollection<string> workstationTags,
            ProductionOutputFactor outputFactor)
        {
            SupportId = supportId;
            FeatureTags = featureTags;
            WorkstationTags = workstationTags;
            OutputFactor = outputFactor;
        }

        public string SupportId { get; }
        private IReadOnlyCollection<string> FeatureTags { get; }
        private IReadOnlyCollection<string> WorkstationTags { get; }
        public ProductionOutputFactor OutputFactor { get; }

        public void AppendTo(CanonicalSemanticDigestBuilder canonical)
        {
            if (canonical == null)
                throw new ArgumentNullException(nameof(canonical));
            canonical.Append(SupportId);
            canonical.Append(FeatureTags.Count);
            foreach (string tag in FeatureTags)
                canonical.Append(tag);
            canonical.Append(WorkstationTags.Count);
            foreach (string tag in WorkstationTags)
                canonical.Append(tag);
            canonical.Append(OutputFactor.Numerator);
            canonical.Append(OutputFactor.Denominator);
        }

        public bool Supports(string workstationTag, string featureTag) =>
            WorkstationTags.Contains(workstationTag, StringComparer.Ordinal)
            && FeatureTags.Contains(featureTag, StringComparer.Ordinal);
    }
}
