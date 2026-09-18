using System;
using System.Collections.Generic;
using System.Linq;

public static class GameplayOutcomeEvidencePublicMaterialComposer
{
    public static NarrativePublicContextMaterial AddExactEvidence(
        NarrativePublicContextMaterial material,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> bindings)
    {
        if (material == null)
            throw new ArgumentNullException(nameof(material));
        GameplayOutcomeEvidenceBindingSnapshot[] exact = (bindings
                ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null)
            .OrderBy(value => value.publicFactId, StringComparer.Ordinal)
            .ToArray();
        if (exact.Length == 0)
            return material;

        List<NarrativePublicEntityInput> entities = material.Entities
            .Select(value => new NarrativePublicEntityInput(
                value.EntityId, value.Kind, value.DisplayName))
            .ToList();
        List<NarrativePublicFactInput> facts = material.PublicFacts
            .Select(value => new NarrativePublicFactInput(
                value.Domain,
                value.OriginalFactId,
                value.SourceSubjectId,
                value.Text,
                value.Priority,
                value.Category,
                value.SubjectId,
                value.Outcome,
                value.LastDay,
                value.Count,
                value.MilestoneCount,
                ParseDecimal(value.TotalValueDecimal),
                value.EventData))
            .ToList();
        foreach (GameplayOutcomeEvidenceBindingSnapshot binding in exact)
        {
            string statusText = GameplayOutcomeEvidenceBindingAuthority
                .RequireStatus(binding).ToString();
            string domain = "GameplayOutcome:exact:" + binding.outcomeTypeId;
            string original = new GameplayOutcomeId(
                new GameplayOutcomeRunId(binding.outcomeRunId),
                binding.outcomeSequence).ToString();
            string expected = NarrativePublicContextFactory.BuildProjectedFactId(
                domain, original, binding.subjectId);
            if (!string.Equals(expected, binding.publicFactId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Exact gameplay-outcome evidence binding has a forged public fact identity.");
            facts.Add(new NarrativePublicFactInput(
                domain,
                original,
                binding.subjectId,
                binding.canonicalFactText,
                2000,
                NarrativePublicFactCategory.Memory,
                string.Empty,
                statusText,
                null,
                1,
                1,
                null,
                new NarrativePublicEventInput(
                    binding.publicFactId,
                    material.SubjectId,
                    string.Empty,
                    binding.outcomeTypeId,
                    statusText,
                    null,
                    1,
                    null)));
        }
        return NarrativePublicContextFactory.Build(
            material.ProfileId,
            material.SubjectId,
            material.SubjectKind,
            material.RequestContext.CultureStyleId,
            material.RequestContext.RequireCharacterFact,
            material.RequestContext.RequireMotif,
            entities,
            facts,
            material.Selection.PolicyId,
            NarrativeRequestContext.MaximumFacts);
    }

    private static decimal? ParseDecimal(string value) =>
        decimal.TryParse(
            value,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out decimal parsed)
            ? parsed
            : null;
}
