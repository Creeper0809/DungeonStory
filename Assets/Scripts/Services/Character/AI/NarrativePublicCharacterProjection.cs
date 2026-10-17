using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>Request-local public projection shared by live and unpublished character contexts.
/// Authority readers are supplied by the owning scope; this projection owns no gameplay state.</summary>
internal sealed class NarrativePublicCharacterProjection
{
    private readonly Action<ICollection<NarrativePublicFactInput>, CharacterActor, string> addAuthorityFacts;
    private readonly Func<CharacterActor, string> resolveCulture;
    private readonly Func<string, NarrativePublicEntityInput> resolveCharacter;

    internal NarrativePublicCharacterProjection(
        Action<ICollection<NarrativePublicFactInput>, CharacterActor, string> addAuthorityFacts,
        Func<CharacterActor, string> resolveCulture,
        Func<string, NarrativePublicEntityInput> resolveCharacter)
    {
        this.addAuthorityFacts = addAuthorityFacts ?? throw new ArgumentNullException(nameof(addAuthorityFacts));
        this.resolveCulture = resolveCulture ?? throw new ArgumentNullException(nameof(resolveCulture));
        this.resolveCharacter = resolveCharacter ?? throw new ArgumentNullException(nameof(resolveCharacter));
    }

    private const string RequiredEvidenceExceedsSelectionCap =
        "RequiredEvidenceExceedsPublicContextSelectionCap";
    private const int CurrentNeedFactPriority = 99;

    private static readonly KeyValuePair<string, CharacterCondition>[] currentNeedFacts =
    {
        new("hunger", CharacterCondition.HUNGER),
        new("sleep", CharacterCondition.SLEEP),
        new("fun", CharacterCondition.FUN),
        new("mood", CharacterCondition.MOOD),
        new("excretion", CharacterCondition.EXCRETION),
        new("hygiene", CharacterCondition.HYGIENE)
    };

    internal NarrativePublicContextMaterial Build(
        string profileId,
        CharacterActor actor,
        bool requireCharacterFact,
        bool requireMotif,
        IEnumerable<string> requiredOriginalFactIds,
        IEnumerable<NarrativeLedgerPublicationDescriptor> ledgerPublicationDescriptors,
        bool includeCurrentNeeds,
        IEnumerable<string> requiredProjectedFactIds = null)
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        CharacterIdentity identity = actor.Identity
            ?? throw new InvalidOperationException(
                "Public character narrative context requires CharacterIdentity.");
        string subjectId = identity.PersistentId?.Trim() ?? string.Empty;
        if (subjectId.Length == 0)
            throw new InvalidOperationException(
                "Public character narrative context requires a persistent subject ID.");
        string displayName = identity.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length == 0)
            throw new InvalidOperationException(
                $"Public character narrative subject '{subjectId}' has no display name.");

        Dictionary<string, NarrativePublicEntityInput> entities =
            new(StringComparer.Ordinal)
            {
                [subjectId] = new NarrativePublicEntityInput(
                    subjectId,
                    NarrativePublicEntityKind.Character,
                    displayName)
            };
        HashSet<string> requiredFacts = new(
            (requiredOriginalFactIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim()),
            StringComparer.Ordinal);
        if (requiredFacts.Count > NarrativeRequestContext.MaximumFacts)
        {
            throw new InvalidOperationException(
                RequiredEvidenceExceedsSelectionCap
                + $": required {requiredFacts.Count} original fact IDs, but the public context cap is "
                + NarrativeRequestContext.MaximumFacts + ".");
        }
        List<NarrativePublicFactInput> facts = new();
        AddActorFacts(facts, actor, subjectId);
        if (includeCurrentNeeds)
            AddCurrentNeedPublicFacts(facts, actor, subjectId);
        addAuthorityFacts(facts, actor, subjectId);
        if (ledgerPublicationDescriptors != null && actor.Progression == null)
            throw new InvalidOperationException(
                "Ledger publication descriptors require actor progression.");
        if (actor.Progression != null)
        {
            AddProgressionPublicFacts(
                facts,
                entities,
                actor.Progression,
                subjectId,
                requiredFacts,
                ledgerPublicationDescriptors);
        }

        string cultureOrSpecies = resolveCulture(actor);
        var requiredProjected = new HashSet<string>(
            requiredProjectedFactIds ?? Array.Empty<string>(), StringComparer.Ordinal);
        if (requiredProjected.Count > NarrativeRequestContext.MaximumFacts)
            throw new InvalidOperationException(RequiredEvidenceExceedsSelectionCap
                + ": selected evidence exceeds the public context cap.");
        // Reserve exact tuples, not a representative with the same event name
        // but a different domain/target. These priorities are never model scores.
        facts = facts.Select(fact => requiredProjected.Contains(
                NarrativePublicContextFactory.BuildProjectedFactId(
                    fact.Domain, fact.OriginalFactId, fact.SourceSubjectId))
            ? new NarrativePublicFactInput(fact.Domain, fact.OriginalFactId,
                fact.SourceSubjectId, fact.Text, Math.Max(10000, fact.Priority),
                fact.Category, fact.PublicSubjectId, fact.Outcome, fact.LastDay,
                fact.Count, fact.MilestoneCount, fact.TotalValue, fact.EventData)
            : fact).ToList();
        NarrativePublicContextMaterial material = NarrativePublicContextFactory.Build(
            profileId,
            subjectId,
            NarrativePublicSubjectKind.Character,
            cultureOrSpecies,
            requireCharacterFact,
            requireMotif,
            entities.Values,
            facts,
            "public-context:character:v2:required-representative-priority-ordinal-cap24");
        if (includeCurrentNeeds)
            RequireCurrentNeedFactsSelected(material, subjectId);
        if (requiredProjected.Any(id => !material.Events.Any(value => value.FactId == id)))
            throw new InvalidOperationException(
                "Required exact narrative tuple has no selected readable public event.");
        foreach (string required in requiredFacts)
        {
            NarrativePublicFact[] selected = material.PublicFacts
                .Where(value => string.Equals(
                    value.OriginalFactId,
                    required,
                    StringComparison.Ordinal))
                .ToArray();
            if (selected.Length == 0
                || !selected.Any(selectedFact => material.Events.Any(value =>
                    string.Equals(
                        value.FactId,
                        selectedFact.FactId,
                        StringComparison.Ordinal))))
            {
                throw new InvalidOperationException(
                    $"Required narrative evidence '{required}' has no selected readable event.");
            }
        }
        return material;
    }

    private static void AddActorFacts(
        ICollection<NarrativePublicFactInput> facts,
        CharacterActor actor,
        string subjectId)
    {
        CharacterIdentity identity = actor.Identity;
        AddPublicFact(facts, "Identity", "identity:name", subjectId,
            "Name: " + identity.DisplayName, 100);
        AddPublicFact(facts, "Identity", "identity:species", subjectId,
            "Species: " + (actor.SpeciesTag ?? string.Empty), 95);
        AddPublicFact(facts, "Identity", "identity:role", subjectId,
            "Role: " + actor.Role, 40);

        CharacterRuntimeProfile profile = actor.profile;
        if (profile != null)
        {
            for (int index = 0; index < profile.ExpressedTraitIds.Count; index++)
            {
                string id = profile.ExpressedTraitIds[index];
                string label = index < profile.TraitDisplayNames.Count
                    ? profile.TraitDisplayNames[index]
                    : id;
                AddPublicFact(facts, "Trait", "expressed:" + id, subjectId,
                    "Expressed trait: " + label, 90,
                    NarrativePublicFactCategory.Background);
            }
        }

        if (actor.Progression != null)
        {
            string origin = actor.Progression.GrowthState.origin;
            if (!string.IsNullOrWhiteSpace(origin))
            {
                AddPublicFact(facts, "Background", "origin", subjectId,
                    "Origin: " + origin, 85,
                    NarrativePublicFactCategory.Background);
            }
            AddPublicFact(
                facts,
                "Progression",
                "potential:" + actor.Progression.GrowthState.potentialGrade,
                subjectId,
                "Potential: " + CharacterSkillDisplay.Potential(
                    actor.Progression.GrowthState.potentialGrade),
                35);
        }

        if (actor.InjurySeverity > 0.01f)
        {
            AddPublicFact(
                facts,
                "Health",
                "current-injury",
                subjectId,
                "Current injury severity: "
                    + actor.InjurySeverity.ToString("0.##", CultureInfo.InvariantCulture),
                80,
                NarrativePublicFactCategory.Memory);
        }
    }

    private static void AddCurrentNeedPublicFacts(
        ICollection<NarrativePublicFactInput> facts,
        CharacterActor actor,
        string subjectId)
    {
        foreach (KeyValuePair<string, CharacterCondition> need in currentNeedFacts)
        {
            float value = 0f;
            bool available = actor.Stats != null
                && actor.Stats.Stats != null
                && actor.Stats.Stats.TryGetValue(need.Value, out value);
            if (available && (float.IsNaN(value) || float.IsInfinity(value)))
                throw new InvalidOperationException(
                    $"Current need '{need.Key}' is not a finite authoritative value.");
            string text = "Current need " + need.Key + ": "
                + (available
                    ? value.ToString("0.0", CultureInfo.InvariantCulture)
                    : "unavailable");
            facts.Add(new NarrativePublicFactInput(
                "Need",
                "current-need:" + need.Key,
                subjectId,
                text,
                CurrentNeedFactPriority,
                totalValue: available ? (decimal)value : null));
        }
    }

    private static void RequireCurrentNeedFactsSelected(
        NarrativePublicContextMaterial material,
        string subjectId)
    {
        foreach (KeyValuePair<string, CharacterCondition> need in currentNeedFacts)
        {
            string originalFactId = "current-need:" + need.Key;
            int matches = material.PublicFacts.Count(value =>
                string.Equals(value.Domain, "Need", StringComparison.Ordinal)
                && string.Equals(
                    value.OriginalFactId,
                    originalFactId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.SourceSubjectId,
                    subjectId,
                    StringComparison.Ordinal));
            if (matches != 1)
                throw new InvalidOperationException(
                    $"Persona public material requires current need '{need.Key}' exactly once.");
        }
    }

    private void AddProgressionPublicFacts(
        ICollection<NarrativePublicFactInput> facts,
        IDictionary<string, NarrativePublicEntityInput> entities,
        CharacterProgression progression,
        string subjectId,
        ISet<string> requiredOriginalFactIds,
        IEnumerable<NarrativeLedgerPublicationDescriptor> ledgerPublicationDescriptors)
    {
        CharacterNarrativeFact[] ledgerFacts = progression.NarrativeLedger.Facts
            .Where(value => value != null)
            .ToArray();
        Dictionary<string, NarrativeLedgerPublicationDescriptor> publicationByTuple =
            BuildPublicationDescriptorIndex(ledgerPublicationDescriptors);
        HashSet<string> usedPublicationTuples = publicationByTuple == null
            ? null
            : new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> requiredRepresentativeFactIds = ledgerFacts
            .Where(value => requiredOriginalFactIds.Contains(
                value.factId?.Trim() ?? string.Empty))
            .GroupBy(
                value => value.factId?.Trim() ?? string.Empty,
                StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(ProgressionPublicFactPriority)
                .ThenByDescending(value => value.lastDay)
                .ThenByDescending(value => value.count)
                .ThenBy(value => value.domain.ToString(), StringComparer.Ordinal)
                .ThenBy(value => value.factId?.Trim() ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(value => value.subjectId?.Trim() ?? string.Empty, StringComparer.Ordinal)
                .First())
            .Select(BuildProgressionProjectedFactId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (CharacterNarrativeFact fact in ledgerFacts)
        {
            string sourceSubjectId = fact.subjectId?.Trim() ?? string.Empty;
            string publicTargetId = string.Empty;
            NarrativeLedgerPublicationDescriptor publication = null;
            if (publicationByTuple != null)
            {
                string tuple = NarrativeLedgerPublicationDescriptor.BuildTupleKey(
                    fact.domain,
                    fact.factId,
                    sourceSubjectId);
                if (!publicationByTuple.TryGetValue(tuple, out publication))
                    ThrowPublicationTupleMismatch(fact, publicationByTuple.Values);
                usedPublicationTuples.Add(tuple);
                NarrativePublicEntityInput exactTarget = publication.TargetEntity;
                if (exactTarget != null)
                {
                    publicTargetId = exactTarget.EntityId;
                    AddExactPublicationEntity(entities, exactTarget);
                }
            }
            else if (sourceSubjectId.Length > 0
                && resolveCharacter(sourceSubjectId) is NarrativePublicEntityInput entity)
            {
                publicTargetId = entity.EntityId;
                entities[entity.EntityId] = entity;
            }
            int? day = fact.lastDay > 0 ? fact.lastDay : null;
            int priority = ProgressionPublicFactPriority(fact);
            if (requiredRepresentativeFactIds.Contains(
                    BuildProgressionProjectedFactId(fact)))
            {
                priority += 1000;
            }
            NarrativePublicFactCategory category = NarrativePublicFactCategory.Memory;
            if (fact.domain == CharacterNarrativeDomain.Relationship)
                category |= NarrativePublicFactCategory.Relationship;
            string targetText = publicTargetId.Length > 0
                ? "; target: " + entities[publicTargetId].DisplayName
                : string.Empty;
            string readableText = publication?.ReadableText
                ?? $"Experience: {fact.domain}; event: {fact.factId}; outcome: {fact.outcome}; count: {fact.count}{targetText}";
            string eventType = publication?.EventType ?? fact.factId;
            facts.Add(new NarrativePublicFactInput(
                fact.domain.ToString(),
                fact.factId,
                sourceSubjectId,
                readableText,
                priority,
                category,
                publicTargetId,
                fact.outcome,
                day,
                fact.count,
                fact.milestoneCount,
                (decimal)fact.totalValue,
                new NarrativePublicEventInput(
                    string.Empty,
                    subjectId,
                    publicTargetId,
                    eventType,
                    fact.outcome,
                    day,
                    fact.count,
                    null)));
        }
        if (publicationByTuple != null
            && usedPublicationTuples.Count != publicationByTuple.Count)
        {
            NarrativeLedgerPublicationDescriptor extra = publicationByTuple.Values
                .First(value => !usedPublicationTuples.Contains(value.TupleKey));
            throw new InvalidOperationException(
                "Ledger publication descriptor has no exact ledger tuple: "
                + DescribePublicationTuple(
                    extra.Domain,
                    extra.OriginalFactId,
                    extra.SourceSubjectId)
                + ".");
        }
    }

    private static Dictionary<string, NarrativeLedgerPublicationDescriptor>
        BuildPublicationDescriptorIndex(
            IEnumerable<NarrativeLedgerPublicationDescriptor> descriptors)
    {
        if (descriptors == null) return null;
        Dictionary<string, NarrativeLedgerPublicationDescriptor> result =
            new(StringComparer.Ordinal);
        foreach (NarrativeLedgerPublicationDescriptor descriptor in descriptors)
        {
            if (descriptor == null)
                throw new InvalidOperationException(
                    "Ledger publication descriptors cannot contain null.");
            if (!result.TryAdd(descriptor.TupleKey, descriptor))
                throw new InvalidOperationException(
                    "Duplicate ledger publication descriptor for "
                    + DescribePublicationTuple(
                        descriptor.Domain,
                        descriptor.OriginalFactId,
                        descriptor.SourceSubjectId)
                    + ".");
        }
        return result;
    }

    private static void ThrowPublicationTupleMismatch(
        CharacterNarrativeFact fact,
        IEnumerable<NarrativeLedgerPublicationDescriptor> descriptors)
    {
        string originalFactId = fact.factId?.Trim() ?? string.Empty;
        string sourceSubjectId = fact.subjectId?.Trim() ?? string.Empty;
        NarrativeLedgerPublicationDescriptor domainMismatch = descriptors.FirstOrDefault(value =>
            string.Equals(value.OriginalFactId, originalFactId, StringComparison.Ordinal)
            && string.Equals(value.SourceSubjectId, sourceSubjectId, StringComparison.Ordinal)
            && value.Domain != fact.domain);
        if (domainMismatch != null)
            throw new InvalidOperationException(
                $"Ledger publication domain mismatch for '{originalFactId}': "
                + $"ledger={fact.domain}, descriptor={domainMismatch.Domain}.");

        NarrativeLedgerPublicationDescriptor subjectMismatch = descriptors.FirstOrDefault(value =>
            value.Domain == fact.domain
            && string.Equals(value.OriginalFactId, originalFactId, StringComparison.Ordinal)
            && !string.Equals(value.SourceSubjectId, sourceSubjectId, StringComparison.Ordinal));
        if (subjectMismatch != null)
            throw new InvalidOperationException(
                $"Ledger publication subject mismatch for '{originalFactId}': "
                + $"ledger='{sourceSubjectId}', descriptor='{subjectMismatch.SourceSubjectId}'.");

        throw new InvalidOperationException(
            "Missing ledger publication descriptor for "
            + DescribePublicationTuple(fact.domain, originalFactId, sourceSubjectId)
            + ".");
    }

    private static void AddExactPublicationEntity(
        IDictionary<string, NarrativePublicEntityInput> entities,
        NarrativePublicEntityInput target)
    {
        if (entities.TryGetValue(target.EntityId, out NarrativePublicEntityInput existing))
        {
            if (existing.Kind != target.Kind
                || !string.Equals(
                    existing.DisplayName,
                    target.DisplayName,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Ledger publication target '{target.EntityId}' conflicts with its public entity.");
            }
            return;
        }
        entities.Add(target.EntityId, new NarrativePublicEntityInput(
            target.EntityId,
            target.Kind,
            target.DisplayName));
    }

    private static string DescribePublicationTuple(
        CharacterNarrativeDomain domain,
        string originalFactId,
        string sourceSubjectId) =>
        $"({domain},'{originalFactId}','{sourceSubjectId}')";

    private static int ProgressionPublicFactPriority(CharacterNarrativeFact fact) =>
        70 + Math.Min(10, fact?.milestoneCount ?? 0);

    private static string BuildProgressionProjectedFactId(CharacterNarrativeFact fact)
    {
        return NarrativePublicContextFactory.BuildProjectedFactId(
            fact.domain.ToString(),
            fact.factId,
            fact.subjectId);
    }

    internal static void AddPublicFact(
        ICollection<NarrativePublicFactInput> facts,
        string domain,
        string originalFactId,
        string sourceSubjectId,
        string text,
        int priority,
        NarrativePublicFactCategory category = NarrativePublicFactCategory.None)
    {
        facts.Add(new NarrativePublicFactInput(
            domain,
            originalFactId,
            sourceSubjectId,
            text,
            priority,
            category));
    }

}
