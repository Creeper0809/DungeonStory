using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pure, mutation-free upper-bound authority for every registered natural
/// resource output recipe. Runtime publication consumes the resulting proof;
/// adding another binding contributor requires no change to this projector.
/// </summary>
public sealed class WorldResourceOutputMaximumEnvelopeAuthority :
    IWorldResourceOutputMaximumEnvelopeAuthority
{
    public const string Schema =
        "world-resource-output-maximum-envelope@1";

    private readonly IReadOnlyList<WorldResourceOutputMaximumEnvelopeSnapshot>
        envelopes;
    private readonly IReadOnlyDictionary<string,
        WorldResourceOutputMaximumEnvelopeSnapshot> byRecipeId;

    public WorldResourceOutputMaximumEnvelopeAuthority(
        IResourceEconomyContentCatalog catalog,
        IWorldResourceSourceBindingCatalog bindings,
        IProductionOutputMaximumMassRegistry maximumMass)
    {
        catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        maximumMass = maximumMass
            ?? throw new ArgumentNullException(nameof(maximumMass));

        WorldResourceSourceBinding[] registered = (bindings.Bindings
                ?? throw new InvalidOperationException(
                    "World-resource source binding catalog returned null."))
            .OrderBy(value => value?.BindingId, StringComparer.Ordinal)
            .ToArray();
        if (registered.Length == 0
            || registered.Any(value => value == null)
            || registered.Select(value => value.BindingId)
                .Distinct(StringComparer.Ordinal).Count() != registered.Length
            || string.IsNullOrEmpty(bindings.CatalogFingerprint)
            || bindings.CatalogFingerprint.Length != 64)
        {
            throw new InvalidOperationException(
                "World-resource maximum authority requires a canonical binding catalog.");
        }

        List<WorldResourceOutputMaximumEnvelopeSnapshot> captured = new();
        foreach (IGrouping<string, WorldResourceSourceBinding> recipeGroup
                 in registered.GroupBy(
                         value => value.RecipeId,
                         StringComparer.Ordinal)
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            if (!catalog.TryGetRecipe(recipeGroup.Key, out ProductionRecipeSO recipe)
                || recipe == null
                || !string.Equals(
                    recipe.RecipeId,
                    recipeGroup.Key,
                    StringComparison.Ordinal)
                || recipe.FlowRole != ProductionFlowRole.Source
                || recipe.ProcessKind != ProductionProcessKind.WorkOnly
                || recipeGroup.Any(value => value.WorkTypeId != recipe.WorkTypeId))
            {
                throw new InvalidOperationException(
                    "World-resource binding does not resolve to one canonical source recipe: "
                    + recipeGroup.Key);
            }

            recipe.ValidateCanonicalOutputLinesOrThrow();
            ProductionOutputFactor maximumFactor = ProductionOutputFactorAuthority
                .ResolveMaximumGrandProject(recipe.FacilityTag);
            List<WorldResourceOutputMaximumLineSnapshot> lineSnapshots = new();
            long totalMaximumMass = 0L;
            long massAuthorityRevision = 0L;
            foreach (ProductionOutputDefinition line in recipe
                         .CaptureCanonicalOutputs()
                         .OrderBy(value => value.OutputLineId,
                             StringComparer.Ordinal))
            {
                bool physical = ProductionOutputRoleRules.IsPhysical(line.Role)
                    && line.Probability > 0f;
                int maximumQuantity = 0;
                long unitMass = 0L;
                long maximumLineMass = 0L;
                string projectionDigest = string.Empty;
                if (physical)
                {
                    decimal scaled = maximumFactor.Scale(line.Amount);
                    if (scaled <= 0m || scaled > int.MaxValue)
                    {
                        throw new OverflowException(
                            "World-resource maximum output quantity is invalid: "
                            + recipe.RecipeId + "/" + line.OutputLineId);
                    }
                    maximumQuantity = checked((int)decimal.Ceiling(scaled));
                    ProductionOutputMaximumMassProjection projection = maximumMass
                        .CaptureAutomatic(
                            line.OutputLineId,
                            line.ItemId,
                            maximumQuantity);
                    if (!string.Equals(
                            projection.Descriptor.CapabilityId,
                            ProductionOutputCapabilityIds.StandardDefinition,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "World-resource exact-source publication cannot materialize "
                            + "stateful output capability: "
                            + recipe.RecipeId + "/" + line.OutputLineId + "/"
                            + projection.Descriptor.CapabilityId);
                    }
                    unitMass = projection.DefinitionUnitMassGrams;
                    maximumLineMass = projection.MaximumMassGrams;
                    projectionDigest = projection.SourceDigest;
                    if (massAuthorityRevision == 0L)
                        massAuthorityRevision = projection.MassAuthorityRevision;
                    else if (massAuthorityRevision
                             != projection.MassAuthorityRevision)
                    {
                        throw new InvalidOperationException(
                            "World-resource maximum lines used different mass revisions.");
                    }
                    totalMaximumMass = checked(
                        totalMaximumMass + maximumLineMass);
                }

                lineSnapshots.Add(new WorldResourceOutputMaximumLineSnapshot(
                    line.OutputLineId,
                    line.Role,
                    line.ItemId,
                    line.Probability,
                    maximumQuantity,
                    unitMass,
                    maximumLineMass,
                    projectionDigest));
            }

            string recipeDigest = ProductionRecipeSemanticDigest.Capture(recipe);
            string[] bindingIds = recipeGroup
                .Select(value => value.BindingId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            CanonicalSemanticDigestBuilder digest = new();
            digest.Append(Schema);
            digest.Append(bindings.CatalogFingerprint);
            digest.Append(maximumMass.RegistryFingerprint);
            digest.Append(recipe.RecipeId);
            digest.Append(recipeDigest);
            digest.Append(maximumFactor.Numerator);
            digest.Append(maximumFactor.Denominator);
            digest.Append(bindingIds.Length);
            foreach (string bindingId in bindingIds)
                digest.Append(bindingId);
            digest.Append(lineSnapshots.Count);
            foreach (WorldResourceOutputMaximumLineSnapshot line
                     in lineSnapshots)
            {
                digest.Append(line.OutputLineId);
                digest.AppendEnum(line.Role);
                digest.Append(line.ItemId);
                digest.AppendFloat(line.InclusionProbability);
                digest.Append(line.MaximumQuantity);
                digest.Append(line.UnitMassGrams);
                digest.Append(line.MaximumMassGrams);
                digest.Append(line.ProjectionSourceDigest);
            }
            digest.Append(totalMaximumMass);
            digest.Append(massAuthorityRevision);
            captured.Add(new WorldResourceOutputMaximumEnvelopeSnapshot(
                recipe.RecipeId,
                bindingIds,
                recipeDigest,
                maximumFactor,
                lineSnapshots,
                totalMaximumMass,
                massAuthorityRevision,
                maximumMass.RegistryFingerprint,
                digest.ComputeSha256()));
        }

        WorldResourceOutputMaximumEnvelopeSnapshot[] ordered = captured
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0
            || ordered.Select(value => value.RecipeId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "World-resource maximum authority captured no unique recipes.");
        }
        envelopes = Array.AsReadOnly(ordered);
        byRecipeId = ordered.ToDictionary(
            value => value.RecipeId,
            value => value,
            StringComparer.Ordinal);

        CanonicalSemanticDigestBuilder authority = new();
        authority.Append(Schema);
        authority.Append(bindings.CatalogFingerprint);
        authority.Append(maximumMass.RegistryFingerprint);
        authority.Append(ordered.Length);
        foreach (WorldResourceOutputMaximumEnvelopeSnapshot envelope in ordered)
            authority.Append(envelope.SourceDigest);
        AuthorityFingerprint = authority.ComputeSha256();
    }

    public string AuthorityFingerprint { get; }

    public IReadOnlyList<WorldResourceOutputMaximumEnvelopeSnapshot> Envelopes =>
        envelopes;

    public WorldResourceOutputMaximumEnvelopeSnapshot Require(string recipeId) =>
        !string.IsNullOrWhiteSpace(recipeId)
        && string.Equals(recipeId, recipeId.Trim(), StringComparison.Ordinal)
        && byRecipeId.TryGetValue(
            recipeId,
            out WorldResourceOutputMaximumEnvelopeSnapshot value)
            ? value
            : throw new InvalidOperationException(
                "World-resource recipe has no maximum-output proof: "
                + (recipeId ?? string.Empty));
}
