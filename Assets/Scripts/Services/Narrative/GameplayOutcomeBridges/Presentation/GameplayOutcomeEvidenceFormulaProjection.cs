using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class GameplayOutcomeEvidenceFormulaProjection
{
    public static List<GameplayOutcomeEvidenceBindingSnapshot> CaptureExact(
        IEnumerable<GameplayOutcomeNarrativeEvidenceSource> sources)
    {
        List<GameplayOutcomeEvidenceBindingSnapshot> result = new();
        foreach (GameplayOutcomeNarrativeEvidenceSource source in
                 sources ?? Array.Empty<GameplayOutcomeNarrativeEvidenceSource>())
        {
            if (source.IsCompacted || !source.HasExactOutcome
                || !GameplayOutcomeStableIdSyntax.IsValid(source.SourceId))
                continue;
            result.Add(new GameplayOutcomeEvidenceBindingSnapshot
            {
                publicFactId = source.SourceId,
                outcomeRunId = source.ExactOutcomeId.RunId.Value,
                outcomeSequence = source.ExactOutcomeId.Sequence,
                outcomeTypeId = source.OutcomeTypeId,
                subjectKindId = source.SubjectId.Kind.Value,
                subjectId = source.SubjectId.Value,
                anchorRevision = source.AnchorRevision,
                status = (int)source.Status,
                subjectSalience = source.SubjectSalience,
                influenceUseCount = source.InfluenceUseCount,
                influenceRevision = source.InfluenceRevision,
                canonicalFactText = source.CanonicalFactText,
                roleIds = CanonicalDistinct(source.Participants.Select(value => value.RoleId)),
                metricIds = CanonicalDistinct(source.Metrics.Select(value => value.MetricId)),
                metricReferenceIds = CanonicalDistinct(source.Metrics.Select(value =>
                    string.IsNullOrWhiteSpace(value.ReferenceKindId)
                        ? string.Empty
                        : value.ReferenceKindId + ":" + value.ReferenceId)),
                factIds = CanonicalDistinct(source.Facts.Select(value => value.FactId)),
                semanticTags = CanonicalDistinct(source.Tags)
            });
        }
        return result.OrderBy(value => value.publicFactId, StringComparer.Ordinal).ToList();
    }

    public static NarrativeFormulaEvidence ToFormulaEvidence(
        GameplayOutcomeEvidenceBindingSnapshot binding,
        NarrativeFormulaStrengthPolicy policy)
    {
        if (binding == null)
            throw new ArgumentNullException(nameof(binding));
        GameplayOutcomeEvidenceBindingAuthority.RequireStatus(binding);
        double authoredImportance = Math.Max(1d, binding.subjectSalience * 10d);
        double importance = Math.Min(
            policy?.MaximumImportance ?? authoredImportance,
            Math.Max(policy?.MinimumImportance ?? 0d, authoredImportance));
        return new NarrativeFormulaEvidence(
            binding.publicFactId,
            binding.outcomeTypeId,
            string.Join("+", binding.roleIds ?? new List<string>()),
            string.Empty,
            DomainKey(binding.outcomeTypeId),
            1,
            importance,
            Math.Max(0, binding.influenceUseCount));
    }

    public static bool IsNegative(GameplayOutcomeEvidenceBindingSnapshot binding)
    {
        if (binding == null)
            return false;
        GameplayOutcomeStatus status =
            GameplayOutcomeEvidenceBindingAuthority.RequireStatus(binding);
        return status is GameplayOutcomeStatus.Failed
                or GameplayOutcomeStatus.Blocked
                or GameplayOutcomeStatus.Cancelled
            || NarrativeFormulaNegativeEvidence.Matches(
                binding.outcomeTypeId,
                binding.canonicalFactText,
                string.Join("|", binding.factIds ?? new List<string>()));
    }

    public static List<GameplayOutcomeEvidenceBindingSnapshot> Select(
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> bindings,
        IEnumerable<string> publicFactIds)
    {
        Dictionary<string, GameplayOutcomeEvidenceBindingSnapshot> index =
            (bindings ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null)
            .ToDictionary(value => value.publicFactId, StringComparer.Ordinal);
        List<GameplayOutcomeEvidenceBindingSnapshot> selected = new();
        foreach (string id in publicFactIds ?? Array.Empty<string>())
        {
            if (!index.TryGetValue(id ?? string.Empty, out var binding))
                continue;
            selected.Add(binding.Clone());
        }
        return selected;
    }

    public static void AppendPromptFacts(
        StringBuilder builder,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> bindings)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        GameplayOutcomeEvidenceBindingSnapshot[] values = (bindings
                ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null)
            .OrderBy(value => value.publicFactId, StringComparer.Ordinal)
            .ToArray();
        builder.AppendLine("authoritativeOutcomeEvidence:");
        if (values.Length == 0)
        {
            builder.AppendLine("- none");
            return;
        }
        foreach (GameplayOutcomeEvidenceBindingSnapshot value in values)
        {
            builder.Append("- id=").Append(value.publicFactId)
                .Append("; outcomeType=").Append(value.outcomeTypeId)
                .Append("; roles=").Append(string.Join(",", value.roleIds))
                .Append("; metrics=").Append(string.Join(",", value.metricIds))
                .Append("; fact=").AppendLine(value.canonicalFactText);
        }
    }

    private static string DomainKey(string outcomeTypeId)
    {
        string value = outcomeTypeId?.Trim() ?? string.Empty;
        int separator = value.IndexOfAny(new[] { '.', ':', '/' });
        return separator > 0 ? value.Substring(0, separator) : value;
    }

    private static List<string> CanonicalDistinct(IEnumerable<string> source) =>
        (source ?? Array.Empty<string>())
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim())
        .Distinct(StringComparer.Ordinal)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToList();
}
