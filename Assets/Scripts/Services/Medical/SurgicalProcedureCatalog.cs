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
        procedures = (source ?? Array.Empty<SurgicalProcedureSO>())
            .Where(procedure => procedure != null)
            .OrderBy(procedure => procedure.ProcedureId, StringComparer.Ordinal)
            .ToArray();
        byId = procedures
            .Where(procedure => !string.IsNullOrWhiteSpace(procedure.ProcedureId))
            .GroupBy(procedure => procedure.ProcedureId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);
    }

    public IReadOnlyList<SurgicalProcedureSO> Procedures => procedures;

    public bool TryGet(string procedureId, out SurgicalProcedureSO procedure)
    {
        return byId.TryGetValue(procedureId?.Trim() ?? string.Empty, out procedure);
    }

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = new List<string>();
        foreach (IGrouping<string, SurgicalProcedureSO> duplicate in procedures
                     .GroupBy(item => item.ProcedureId, StringComparer.Ordinal)
                     .Where(group => string.IsNullOrWhiteSpace(group.Key)
                         || group.Count() > 1))
        {
            errors.Add($"중복 수술 절차 ID: {duplicate.Key}");
        }

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
