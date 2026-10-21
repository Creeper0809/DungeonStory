using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Resolves authored formula versions from the injected content authority, including persisted V3 nodes.</summary>
public sealed class EquipmentEvolutionFormulaCatalogs
{
    private readonly IReadOnlyList<EquipmentEvolutionFormulaCatalogSO> catalogs;

    public EquipmentEvolutionFormulaCatalogs(IGameContentDefinitionSource content)
    {
        catalogs = (content ?? throw new ArgumentNullException(nameof(content)))
            .GetAll<EquipmentEvolutionFormulaCatalogSO>().ToArray();
    }

    public EquipmentEvolutionFormulaCatalogSO RequireCurrent() =>
        RequireVersion(EquipmentEvolutionRules.CurrentFormulaVersion);

    public EquipmentEvolutionFormulaCatalogSO RequireForPersistedNode(int formulaVersion)
    {
        if (formulaVersion == EquipmentEvolutionRules.DrawbackModuleSelectionFormulaVersion)
            return RequireVersion(EquipmentEvolutionRules.DrawbackModuleSelectionFormulaVersion);
        if (formulaVersion >= EquipmentEvolutionRules.CurrentFormulaVersion)
            return RequireVersion(formulaVersion);

        // V2 has no catalog hash; its descriptors remain a strict subset of the current catalog.
        return RequireCurrent();
    }

    private EquipmentEvolutionFormulaCatalogSO RequireVersion(int formulaVersion)
    {
        EquipmentEvolutionFormulaCatalogSO[] matches = catalogs
            .Where(value => value != null && value.formulaPolicy != null
                && value.formulaPolicy.formulaVersion == formulaVersion)
            .ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException(
                $"Expected one authored equipment formula catalog for version {formulaVersion}; found {matches.Length}.");
        matches[0].RequirePolicy();
        return matches[0];
    }
}
