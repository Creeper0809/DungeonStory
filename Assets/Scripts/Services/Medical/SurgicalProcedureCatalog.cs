using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ResourceSurgicalProcedureCatalog : ISurgicalProcedureCatalog
{
    private readonly IReadOnlyList<SurgicalProcedureSO> procedures;
    private readonly IReadOnlyDictionary<string, SurgicalProcedureSO> byId;

    public ResourceSurgicalProcedureCatalog(IGameContentCatalog content)
        : this((content ?? throw new ArgumentNullException(nameof(content)))
            .GetAll<SurgicalProcedureSO>())
    {
    }

    public ResourceSurgicalProcedureCatalog(IEnumerable<SurgicalProcedureSO> source)
    {
        SurgicalProcedureSO[] materialized = (source
                ?? Array.Empty<SurgicalProcedureSO>())
            .Where(procedure => procedure != null)
            .ToArray();
        SurgicalProcedureSO invalid = materialized.FirstOrDefault(
            procedure => string.IsNullOrWhiteSpace(procedure.ProcedureId));
        if (invalid != null)
        {
            throw new InvalidOperationException(
                $"Surgical procedure '{invalid.name}' has no stable procedure ID.");
        }

        IGrouping<string, SurgicalProcedureSO> duplicate = materialized
            .GroupBy(procedure => procedure.ProcedureId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            throw new InvalidOperationException(
                $"Duplicate surgical procedure ID '{duplicate.Key}'.");
        }

        SurgicalProcedureSO[] ordered = materialized
            .OrderBy(procedure => procedure.ProcedureId, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, SurgicalProcedureSO> index = ordered
            .ToDictionary(
                procedure => procedure.ProcedureId,
                procedure => procedure,
                StringComparer.Ordinal);
        procedures = ordered;
        byId = index;
    }

    public IReadOnlyList<SurgicalProcedureSO> Procedures => procedures;

    public bool TryGet(string procedureId, out SurgicalProcedureSO procedure)
    {
        return byId.TryGetValue(procedureId?.Trim() ?? string.Empty, out procedure);
    }

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = new List<string>();
        foreach (SurgicalProcedureSO procedure in procedures)
        {
            if (procedure.RequiredFacilityTags == SurgeryFacilityTag.None)
            {
                errors.Add($"{procedure.ProcedureId}: 집도 시설 태그가 없습니다.");
            }

            if (procedure.Effects == null || procedure.Effects.Count == 0)
            {
                errors.Add($"{procedure.ProcedureId}: 수술 효과가 없습니다.");
            }

            try
            {
                if (procedure.TryGetInstallationEffect(
                        out InstallSurgicalPartEffect installation))
                {
                    string requiredItemId =
                        installation.requiredItemDefinitionId?.Trim()
                        ?? string.Empty;
                    if (requiredItemId.Length > 0
                        && !string.Equals(
                            requiredItemId,
                            installation.requiredItemDefinitionId,
                            StringComparison.Ordinal))
                    {
                        errors.Add(
                            $"{procedure.ProcedureId}: 설치 부품 ID가 정규형이 아닙니다.");
                    }
                }
            }
            catch (InvalidOperationException exception)
            {
                errors.Add(exception.Message);
            }

            errors.AddRange(
                procedure.OperatorRequirement.Validate(procedure.ProcedureId));

            foreach (SurgicalMaterialRequirement material in
                     procedure.Materials ?? Array.Empty<SurgicalMaterialRequirement>())
            {
                if (material == null
                    || string.IsNullOrWhiteSpace(material.itemId)
                    || material.quantity <= 0)
                {
                    errors.Add($"{procedure.ProcedureId}: 잘못된 수술 재료가 있습니다.");
                }
            }
        }

        return errors;
    }
}
