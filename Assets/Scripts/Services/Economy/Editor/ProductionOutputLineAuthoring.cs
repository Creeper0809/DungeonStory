#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;

/// <summary>
/// Single editor-only authority for stable physical recipe-output identities.
/// Existing identities are preserved when quantity or probability changes;
/// topology changes require an explicit reviewed migration.
/// </summary>
internal static class ProductionOutputLineAuthoring
{
    public static string BuildStableId(
        string recipeId,
        int authoredOrdinal,
        string itemId,
        ProductionOutputRole role)
    {
        RequireCanonicalAsciiToken(recipeId, nameof(recipeId));
        RequireCanonicalAsciiToken(itemId, nameof(itemId));
        if (authoredOrdinal < 0)
            throw new ArgumentOutOfRangeException(nameof(authoredOrdinal));
        if (!ProductionOutputRoleRules.IsPhysical(role))
        {
            throw new ArgumentOutOfRangeException(
                nameof(role), role, "A physical output role is required.");
        }

        string roleToken = role switch
        {
            ProductionOutputRole.Main => "main",
            ProductionOutputRole.Byproduct => "byproduct",
            ProductionOutputRole.ReturnedPackaging => "returned-packaging",
            ProductionOutputRole.RecoverableWaste => "recoverable-waste",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
        };
        string result = "output:" + recipeId
            + "/" + authoredOrdinal.ToString("D3", CultureInfo.InvariantCulture)
            + "/" + roleToken
            + "/" + itemId;
        if (!ProductionOutputDefinition.IsCanonicalOutputLineId(result))
        {
            throw new InvalidOperationException(
                "Stable output-line components generated a non-canonical ID: "
                + result + ".");
        }
        return result;
    }

    public static ProductionOutputDefinition[] ResolveStableOutputs(
        string recipeId,
        IReadOnlyList<ProductionOutputDefinition> existingOutputs,
        IEnumerable<ProductionOutputDefinition> desiredOutputs)
    {
        ProductionOutputDefinition[] desired = (desiredOutputs
                ?? throw new ArgumentNullException(nameof(desiredOutputs)))
            .ToArray();
        if (desired.Any(value => value == null))
        {
            throw new InvalidOperationException(
                $"Recipe '{recipeId}' contains a null desired output.");
        }

        if (existingOutputs == null)
        {
            return desired.Select((output, ordinal) => Rebuild(
                    output,
                    BuildStableId(
                        recipeId,
                        ordinal,
                        output.ItemId,
                        output.Role)))
                .ToArray();
        }

        if (existingOutputs.Count != desired.Length)
        {
            throw new InvalidOperationException(
                $"Recipe '{recipeId}' output count changed from "
                + $"{existingOutputs.Count} to {desired.Length}. Stable output "
                + "identity requires an explicit reviewed migration.");
        }

        HashSet<string> identities = new(StringComparer.Ordinal);
        ProductionOutputDefinition[] resolved =
            new ProductionOutputDefinition[desired.Length];
        for (int ordinal = 0; ordinal < desired.Length; ordinal++)
        {
            ProductionOutputDefinition existing = existingOutputs[ordinal]
                ?? throw new InvalidOperationException(
                    $"Recipe '{recipeId}' contains a null existing output at "
                    + $"ordinal {ordinal}.");
            ProductionOutputDefinition expected = desired[ordinal];
            if (!string.Equals(
                    existing.ItemId,
                    expected.ItemId,
                    StringComparison.Ordinal)
                || existing.Role != expected.Role)
            {
                throw new InvalidOperationException(
                    $"Recipe '{recipeId}' output topology changed at ordinal "
                    + $"{ordinal}. Stable output identity requires an explicit "
                    + "reviewed migration.");
            }
            string canonicalOutputLineId = BuildStableId(
                recipeId,
                ordinal,
                existing.ItemId,
                existing.Role);
            bool exactCanonicalIdentity = string.Equals(
                existing.OutputLineId,
                canonicalOutputLineId,
                StringComparison.Ordinal);
            bool repairableLegacyMainIdentity = desired.Length == 1
                && ordinal == 0
                && existing.Role == ProductionOutputRole.Main
                && string.Equals(
                    existing.OutputLineId,
                    "output:main",
                    StringComparison.Ordinal);
            if ((!exactCanonicalIdentity && !repairableLegacyMainIdentity)
                || !identities.Add(canonicalOutputLineId))
            {
                throw new InvalidOperationException(
                    $"Recipe '{recipeId}' has a missing, non-canonical, or "
                    + $"duplicate output identity at ordinal {ordinal}.");
            }
            resolved[ordinal] = Rebuild(expected, canonicalOutputLineId);
        }
        return resolved;
    }

    private static ProductionOutputDefinition Rebuild(
        ProductionOutputDefinition source,
        string outputLineId) => new(
        outputLineId,
        source.Role,
        source.ItemId,
        source.Amount,
        source.Probability);

    private static void RequireCanonicalAsciiToken(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Any(character => character > 0x7f))
        {
            throw new ArgumentException(
                "A canonical non-empty ASCII token is required.", name);
        }
    }
}

/// <summary>
/// Compares the complete serialized authoring surface of recipe assets while
/// treating an absent optional V27 contract and its all-default inline Unity
/// representation as the same authored value.
/// </summary>
internal static class ProductionRecipeAuthoringComparison
{
    public static bool AreEquivalent(
        ProductionRecipeSO left,
        ProductionRecipeSO right)
    {
        if (left == null)
            throw new ArgumentNullException(nameof(left));
        if (right == null)
            throw new ArgumentNullException(nameof(right));
        if (!Same(left.MassExplanation, right.MassExplanation)
            || !Same(left.OutputCostAllocation, right.OutputCostAllocation))
        {
            return false;
        }

        SerializedObject leftSerialized = new(left);
        SerializedObject rightSerialized = new(right);
        leftSerialized.UpdateIfRequiredOrScript();
        rightSerialized.UpdateIfRequiredOrScript();
        SerializedProperty property = leftSerialized.GetIterator();
        bool enterChildren = true;
        while (property.Next(enterChildren))
        {
            enterChildren = false;
            if (string.Equals(
                    property.propertyPath,
                    "massExplanation",
                    StringComparison.Ordinal)
                || string.Equals(
                    property.propertyPath,
                    "outputCostAllocation",
                    StringComparison.Ordinal))
            {
                continue;
            }

            SerializedProperty counterpart = rightSerialized.FindProperty(
                property.propertyPath);
            if (counterpart == null
                || !SerializedProperty.DataEquals(property, counterpart))
            {
                return false;
            }
        }
        return true;
    }

    private static bool Same(
        ProductionMassExplanationAuthoringSnapshot left,
        ProductionMassExplanationAuthoringSnapshot right) =>
        string.Equals(left.CapabilityId, right.CapabilityId,
            StringComparison.Ordinal)
        && left.ContractVersion == right.ContractVersion
        && string.Equals(left.CanonicalPayload, right.CanonicalPayload,
            StringComparison.Ordinal);

    private static bool Same(
        ProductionOutputCostAllocationAuthoringSnapshot left,
        ProductionOutputCostAllocationAuthoringSnapshot right) =>
        string.Equals(left.CapabilityId, right.CapabilityId,
            StringComparison.Ordinal)
        && left.ContractVersion == right.ContractVersion
        && string.Equals(left.CanonicalPayload, right.CanonicalPayload,
            StringComparison.Ordinal);
}
#endif
