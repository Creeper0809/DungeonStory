using System;
using System.Collections.Generic;

internal sealed class DungeonSaveReferenceRules
{
    private readonly IItemDefinitionCatalog itemDefinitions;
    internal DungeonSaveReferenceRules(IItemDefinitionCatalog itemDefinitions)
    { this.itemDefinitions = itemDefinitions ?? throw new ArgumentNullException(nameof(itemDefinitions)); }

    internal static void RequireReference(
        string id,
        ISet<string> existing,
        string label,
        DungeonGameRestoreReport report)
    {
        if (string.IsNullOrEmpty(id) || !existing.Contains(id))
        {
            report.AddError($"{label} references missing ID '{id ?? string.Empty}'.");
        }
    }

    internal void RequireItemDefinition(
        string rawId,
        DungeonGameRestoreReport report)
    {
        ItemDefinitionId id = (ItemDefinitionId)(rawId?.Trim() ?? string.Empty);
        if (!id.IsValid || !itemDefinitions.TryGet(id, out _))
        {
            report.AddError($"Save references unknown item definition '{rawId ?? string.Empty}'.");
        }
    }

    internal static void RequireUniqueId(
        string rawId,
        string kind,
        ISet<string> ids,
        DungeonGameRestoreReport report)
    {
        string id = rawId?.Trim() ?? string.Empty;
        if (id.Length == 0)
        {
            report.AddError($"Save contains a {kind} without a persistent ID.");
        }
        else if (!ids.Add(id))
        {
            report.AddError($"Save contains duplicate {kind} ID '{id}'.");
        }
    }

    internal static bool RequireUniqueCharacterId(
        string rawId,
        string kind,
        ISet<string> ids,
        DungeonGameRestoreReport report,
        bool rejectDuplicate,
        out string canonicalId)
    {
        canonicalId = string.Empty;
        if (!CharacterV18RestoreIdentityResolver.TryResolve(
                rawId,
                allowLegacyCharacterIds: false,
                out CharacterId characterId,
                out _)
            || !string.Equals(
                rawId,
                characterId.Value,
                StringComparison.Ordinal))
        {
            report.AddError(
                $"Save contains a {kind} without an exact canonical persistent ID: "
                + $"'{rawId ?? "<null>"}'.");
            return false;
        }

        canonicalId = characterId.Value;
        if (!ids.Add(canonicalId) && rejectDuplicate)
        {
            report.AddError(
                $"Save contains duplicate {kind} ID '{canonicalId}'.");
            return false;
        }

        return true;
    }
}
