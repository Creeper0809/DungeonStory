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
        AppendExactEvidence(entities, facts, exact);
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

    public static NarrativePublicContextMaterial BuildExactEvidenceOnly(
        string profileId,
        string subjectId,
        NarrativePublicSubjectKind subjectKind,
        string subjectDisplayName,
        string cultureOrSpecies,
        bool requireCharacterFact,
        bool requireMotif,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> bindings,
        string selectionPolicyId)
    {
        GameplayOutcomeEvidenceBindingSnapshot[] exact = (bindings
                ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null)
            .OrderBy(value => value.publicFactId, StringComparer.Ordinal)
            .ToArray();
        if (exact.Length == 0)
            throw new InvalidOperationException(
                "Exact-only public material requires exact gameplay-outcome evidence.");
        NarrativePublicEntityKind subjectEntityKind = subjectKind switch
        {
            NarrativePublicSubjectKind.Character => NarrativePublicEntityKind.Character,
            NarrativePublicSubjectKind.Equipment => NarrativePublicEntityKind.Equipment,
            NarrativePublicSubjectKind.Facility => NarrativePublicEntityKind.Facility,
            _ => throw new ArgumentOutOfRangeException(nameof(subjectKind))
        };
        List<NarrativePublicEntityInput> entities = new()
        {
            new NarrativePublicEntityInput(
                subjectId,
                subjectEntityKind,
                string.IsNullOrWhiteSpace(subjectDisplayName)
                    ? subjectId
                    : subjectDisplayName.Trim())
        };
        List<NarrativePublicFactInput> facts = new();
        AppendExactEvidence(entities, facts, exact);
        return NarrativePublicContextFactory.Build(
            profileId,
            subjectId,
            subjectKind,
            cultureOrSpecies,
            requireCharacterFact,
            requireMotif,
            entities,
            facts,
            selectionPolicyId,
            NarrativeRequestContext.MaximumFacts);
    }

    private static void AppendExactEvidence(
        ICollection<NarrativePublicEntityInput> entities,
        ICollection<NarrativePublicFactInput> facts,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> bindings)
    {
        foreach (GameplayOutcomeEvidenceBindingSnapshot binding in bindings)
        {
            string statusText = GameplayOutcomeEvidenceBindingAuthority
                .RequireStatus(binding).ToString();
            string sourceKind = GameplayOutcomeEvidenceBindingAuthority
                .EffectiveSourceKind(binding);
            string domain = "GameplayOutcome:" + sourceKind + ":"
                + binding.outcomeTypeId;
            string original = string.Equals(
                    sourceKind,
                    "compacted",
                    StringComparison.Ordinal)
                ? binding.compactedMemoryId
                : new GameplayOutcomeId(
                    new GameplayOutcomeRunId(binding.outcomeRunId),
                    binding.outcomeSequence).ToString();
            string expected = NarrativePublicContextFactory.BuildProjectedFactId(
                domain,
                original,
                binding.subjectId);
            if (!string.Equals(expected, binding.publicFactId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Exact gameplay-outcome evidence binding has a forged public fact identity.");
            AddParticipantEntities(entities, binding.participants);
            string actorId = ResolveEventParticipant(
                binding.participants,
                ActorRoleTokens,
                excludedId: string.Empty);
            // A public fact is projected for exactly one evidence subject.
            // Event target must therefore remain that subject even when the
            // canonical outcome also contains a different target-role
            // participant (for example an attacker remembering its victim).
            string targetId = binding.subjectId;
            facts.Add(new NarrativePublicFactInput(
                domain,
                original,
                binding.subjectId,
                binding.canonicalFactText + (binding.schemaVersion >= 3
                    ? " [경험 평가: " + GameplayExperienceImportancePolicy.Describe(binding.experience) + "]"
                    : string.Empty),
                2000 + (binding.experience == null ? 0 : (int)(binding.experience.importance * 1000f)),
                NarrativePublicFactCategory.Memory,
                binding.subjectId,
                statusText,
                binding.lastDay,
                Math.Max(1, binding.occurrenceCount),
                Math.Max(1, binding.occurrenceCount),
                null,
                new NarrativePublicEventInput(
                    binding.publicFactId,
                    actorId,
                    targetId,
                    binding.outcomeTypeId,
                    statusText,
                    binding.lastDay,
                    Math.Max(1, binding.occurrenceCount),
                    null)));
        }
    }

    private static decimal? ParseDecimal(string value) =>
        decimal.TryParse(
            value,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out decimal parsed)
            ? parsed
            : null;

    private static readonly string[] ActorRoleTokens =
    {
        "actor", "attacker", "instigator", "worker", "researcher",
        "operator", "user", "buyer", "seller", "builder", "butcher",
        "healer", "rescuer", "owner"
    };

    private static void AddParticipantEntities(
        ICollection<NarrativePublicEntityInput> entities,
        IEnumerable<GameplayOutcomeEvidenceParticipantBindingSnapshot> participants)
    {
        HashSet<string> known = entities
            .Where(value => value != null)
            .Select(value => value.EntityId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (GameplayOutcomeEvidenceParticipantBindingSnapshot participant in
                 participants ?? Array.Empty<GameplayOutcomeEvidenceParticipantBindingSnapshot>())
        {
            if (participant == null
                || !GameplayOutcomeStableIdSyntax.IsValid(participant.entityId)
                || !known.Add(participant.entityId))
                continue;
            entities.Add(new NarrativePublicEntityInput(
                participant.entityId,
                PublicKind(participant.entityKindId),
                participant.displayText));
        }
    }

    private static string ResolveEventParticipant(
        IEnumerable<GameplayOutcomeEvidenceParticipantBindingSnapshot> participants,
        IEnumerable<string> roleTokens,
        string excludedId)
    {
        string[] tokens = roleTokens.ToArray();
        return (participants
                ?? Array.Empty<GameplayOutcomeEvidenceParticipantBindingSnapshot>())
            .Where(value => value != null
                && value.participationKind == (int)GameplayParticipationKind.Direct
                && !string.Equals(value.entityId, excludedId, StringComparison.Ordinal)
                && tokens.Any(token => value.roleId.IndexOf(
                    token, StringComparison.OrdinalIgnoreCase) >= 0))
            .OrderBy(value => value.roleId, StringComparer.Ordinal)
            .ThenBy(value => value.entityId, StringComparer.Ordinal)
            .Select(value => value.entityId)
            .FirstOrDefault() ?? string.Empty;
    }

    private static NarrativePublicEntityKind PublicKind(string kindId) => kindId switch
    {
        "character" => NarrativePublicEntityKind.Character,
        "item-instance" or "equipment" or "item-definition" =>
            NarrativePublicEntityKind.Equipment,
        "facility" or "building" => NarrativePublicEntityKind.Facility,
        "faction" or "group" => NarrativePublicEntityKind.Group,
        _ => NarrativePublicEntityKind.Place
    };
}
