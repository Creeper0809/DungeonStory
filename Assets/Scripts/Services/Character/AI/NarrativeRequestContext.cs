using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using VContainer.Unity;

/// <summary>
/// Immutable, request-scoped publication meaning for one exact narrative-ledger
/// tuple. It carries no gameplay authority and is never serialized or saved.
/// </summary>
public sealed class NarrativeLedgerPublicationDescriptor
{
    private readonly NarrativePublicEntityInput targetEntity;

    public NarrativeLedgerPublicationDescriptor(
        CharacterNarrativeDomain domain,
        string originalFactId,
        string sourceSubjectId,
        string readableText,
        string eventType,
        NarrativePublicEntityInput targetEntity = null)
    {
        if (!Enum.IsDefined(typeof(CharacterNarrativeDomain), domain))
            throw new ArgumentOutOfRangeException(nameof(domain));

        Domain = domain;
        OriginalFactId = RequireCanonical(
            originalFactId,
            nameof(originalFactId),
            allowEmpty: false,
            maximumLength: 512);
        SourceSubjectId = RequireCanonical(
            sourceSubjectId,
            nameof(sourceSubjectId),
            allowEmpty: true,
            maximumLength: 512);
        ReadableText = RequireCanonical(
            readableText,
            nameof(readableText),
            allowEmpty: false,
            maximumLength: 2000);
        if (ReadableText.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            throw new ArgumentException(
                "Ledger publication text must be one canonical line.",
                nameof(readableText));
        EventType = RequireCanonical(
            eventType,
            nameof(eventType),
            allowEmpty: false,
            maximumLength: 512);
        if (EventType.Any(char.IsWhiteSpace))
            throw new ArgumentException(
                "Ledger publication event types cannot contain whitespace.",
                nameof(eventType));
        if (ContainsFixtureToken(EventType))
            throw new ArgumentException(
                "Ledger publication event type must describe the semantic event, not a fixture.",
                nameof(eventType));
        if (ContainsFixtureToken(ReadableText)
            || ReadableText.IndexOf(
                "controlled scenario signal",
                StringComparison.OrdinalIgnoreCase) >= 0)
        {
            throw new ArgumentException(
                "Ledger publication text must be a readable event sentence, not fixture prose.",
                nameof(readableText));
        }

        if (targetEntity == null)
        {
            this.targetEntity = null;
            return;
        }

        string targetId = RequireCanonical(
            targetEntity.EntityId,
            nameof(targetEntity),
            allowEmpty: false,
            maximumLength: 512);
        if (targetId.Any(char.IsWhiteSpace))
            throw new ArgumentException(
                "Ledger publication target IDs cannot contain whitespace.",
                nameof(targetEntity));
        if (!string.Equals(targetId, SourceSubjectId, StringComparison.Ordinal))
            throw new ArgumentException(
                "Ledger publication target ID must exactly equal sourceSubjectId.",
                nameof(targetEntity));
        if (!Enum.IsDefined(typeof(NarrativePublicEntityKind), targetEntity.Kind))
            throw new ArgumentOutOfRangeException(nameof(targetEntity));
        string targetName = RequireCanonical(
            targetEntity.DisplayName,
            nameof(targetEntity),
            allowEmpty: false,
            maximumLength: 512);
        if (targetName.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            throw new ArgumentException(
                "Ledger publication target names must be one canonical line.",
                nameof(targetEntity));
        this.targetEntity = new NarrativePublicEntityInput(
            targetId,
            targetEntity.Kind,
            targetName);
    }

    public CharacterNarrativeDomain Domain { get; }
    public string OriginalFactId { get; }
    public string SourceSubjectId { get; }
    public string ReadableText { get; }
    public string EventType { get; }
    public NarrativePublicEntityInput TargetEntity => targetEntity == null
        ? null
        : new NarrativePublicEntityInput(
            targetEntity.EntityId,
            targetEntity.Kind,
            targetEntity.DisplayName);

    internal string TupleKey => BuildTupleKey(Domain, OriginalFactId, SourceSubjectId);

    internal static string BuildTupleKey(
        CharacterNarrativeDomain domain,
        string originalFactId,
        string sourceSubjectId) =>
        NarrativePublicContextFactory.BuildProjectedFactId(
            domain.ToString(),
            originalFactId,
            sourceSubjectId);

    private static string RequireCanonical(
        string value,
        string parameterName,
        bool allowEmpty,
        int maximumLength)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (!string.Equals(value ?? string.Empty, normalized, StringComparison.Ordinal))
            throw new ArgumentException(
                "Ledger publication strings must already be trimmed.",
                parameterName);
        if (!allowEmpty && normalized.Length == 0)
            throw new ArgumentException(
                "Ledger publication value cannot be empty.",
                parameterName);
        if (normalized.Length > maximumLength)
            throw new ArgumentException(
                $"Ledger publication value exceeds {maximumLength} characters.",
                parameterName);
        return normalized;
    }

    private static bool ContainsFixtureToken(string value) =>
        value?.IndexOf("fixture", StringComparison.OrdinalIgnoreCase) >= 0
        || value?.IndexOf("unseen-eval", StringComparison.OrdinalIgnoreCase) >= 0
        || value?.IndexOf("controlled-scenario", StringComparison.OrdinalIgnoreCase) >= 0;
}

public static class NarrativeRequestContextBuilder
{
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

    private static ICharacterNarrativeQuery authoritativeNarratives;
    private static ICharacterLifeQuery authoritativeLife;
    private static ICharacterNarrativeCatalog authoritativeCatalog;
    private static ICharacterWorldQuery authoritativeWorld;
    private static ICharacterLifetimeQuery authoritativeLifetime;

    public static void ConfigureAuthorities(
        ICharacterNarrativeQuery narratives,
        ICharacterLifeQuery life,
        ICharacterNarrativeCatalog catalog,
        ICharacterWorldQuery world = null,
        ICharacterLifetimeQuery lifetime = null)
    {
        authoritativeNarratives = narratives;
        authoritativeLife = life;
        authoritativeCatalog = catalog;
        authoritativeWorld = world;
        authoritativeLifetime = lifetime;
    }

    public static NarrativeRequestContext ForActor(
        string profileId,
        CharacterActor actor,
        bool requireCharacterFact,
        bool requireMotif)
    {
        if (actor != null && NarrativePublicContextFactory.SupportsProfile(profileId))
        {
            return BuildPublicMaterialForActor(
                profileId,
                actor,
                requireCharacterFact,
                requireMotif).RequestContext;
        }

        string species = actor?.SpeciesTag ?? string.Empty;
        CharacterId characterId = actor != null ? actor.BuildingCharacterId : default;
        string cultureOrSpecies = species;
        if (characterId.IsValid
            && authoritativeNarratives != null
            && authoritativeNarratives.TryGet(characterId, out CharacterNarrativeSnapshot snapshot)
            && snapshot.CultureId.IsValid)
        {
            cultureOrSpecies = snapshot.CultureId.Value;
        }

        NarrativeRequestContext context = NarrativeCultureStyleCatalog.Create(
            profileId, cultureOrSpecies, requireCharacterFact, requireMotif);
        if (actor == null) return context;

        CharacterIdentity identity = actor.Identity;
        CharacterRuntimeProfile profile = actor.profile;
        context.AddFact("fact:identity:name", $"Name: {identity?.DisplayName ?? actor.name}", 100);
        context.AddFact("fact:identity:species", $"Species: {species}", 95);
        context.AddFact("fact:identity:role", $"Role: {actor.Role}", 40);
        if (profile != null)
        {
            for (int index = 0; index < profile.ExpressedTraitIds.Count; index++)
            {
                string id = profile.ExpressedTraitIds[index];
                string label = index < profile.TraitDisplayNames.Count
                    ? profile.TraitDisplayNames[index]
                    : id;
                context.AddFact("fact:trait:" + Token(id), "Expressed trait: " + label, 90);
            }
        }

        if (actor.Progression != null) AddProgressionFacts(context, actor.Progression);
        if (actor.InjurySeverity > 0.01f)
            context.AddFact("fact:health:injury", $"Current injury severity: {actor.InjurySeverity:0.##}", 80);
        AddAuthoritativeFacts(context, characterId, authoritativeNarratives, authoritativeLife, authoritativeCatalog);
        return context;
    }

    public static NarrativeRequestContext ForProgression(
        string profileId,
        CharacterProgression progression,
        bool requireCharacterFact,
        bool requireMotif)
    {
        CharacterActor actor = progression != null
            ? progression.GetComponent<CharacterActor>()
            : null;
        NarrativeRequestContext context = ForActor(
            profileId, actor, requireCharacterFact, requireMotif);
        if (actor == null && progression != null) AddProgressionFacts(context, progression);
        return context;
    }

    public static NarrativePublicContextMaterial BuildPublicMaterialForActor(
        string profileId,
        CharacterActor actor,
        bool requireCharacterFact,
        bool requireMotif,
        IEnumerable<string> requiredOriginalFactIds = null)
    {
        return BuildPublicMaterialForActorCore(
            profileId,
            actor,
            requireCharacterFact,
            requireMotif,
            requiredOriginalFactIds,
            ledgerPublicationDescriptors: null,
            includeCurrentNeeds: false);
    }

    public static NarrativePublicContextMaterial BuildPublicMaterialForActor(
        string profileId,
        CharacterActor actor,
        bool requireCharacterFact,
        bool requireMotif,
        IEnumerable<string> requiredOriginalFactIds,
        IEnumerable<NarrativeLedgerPublicationDescriptor> ledgerPublicationDescriptors)
    {
        if (ledgerPublicationDescriptors == null)
            throw new ArgumentNullException(nameof(ledgerPublicationDescriptors));
        return BuildPublicMaterialForActorCore(
            profileId,
            actor,
            requireCharacterFact,
            requireMotif,
            requiredOriginalFactIds,
            ledgerPublicationDescriptors,
            includeCurrentNeeds: false);
    }

    internal static NarrativePublicContextMaterial
        BuildPublicMaterialForActorWithCurrentNeeds(
            string profileId,
            CharacterActor actor,
            bool requireCharacterFact,
            bool requireMotif)
    {
        return BuildPublicMaterialForActorCore(
            profileId,
            actor,
            requireCharacterFact,
            requireMotif,
            requiredOriginalFactIds: null,
            ledgerPublicationDescriptors: null,
            includeCurrentNeeds: true);
    }

    private static NarrativePublicContextMaterial BuildPublicMaterialForActorCore(
        string profileId,
        CharacterActor actor,
        bool requireCharacterFact,
        bool requireMotif,
        IEnumerable<string> requiredOriginalFactIds,
        IEnumerable<NarrativeLedgerPublicationDescriptor> ledgerPublicationDescriptors,
        bool includeCurrentNeeds)
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
        AddAuthoritativePublicFacts(facts, actor, subjectId);
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

        string cultureOrSpecies = ResolveCultureOrSpecies(actor);
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

    public static NarrativePublicContextMaterial BuildPublicMaterialForProgression(
        string profileId,
        CharacterProgression progression,
        bool requireCharacterFact,
        bool requireMotif,
        IEnumerable<string> requiredOriginalFactIds = null)
    {
        return BuildPublicMaterialForProgressionCore(
            profileId,
            progression,
            requireCharacterFact,
            requireMotif,
            requiredOriginalFactIds,
            ledgerPublicationDescriptors: null);
    }

    public static NarrativePublicContextMaterial BuildPublicMaterialForProgression(
        string profileId,
        CharacterProgression progression,
        bool requireCharacterFact,
        bool requireMotif,
        IEnumerable<string> requiredOriginalFactIds,
        IEnumerable<NarrativeLedgerPublicationDescriptor> ledgerPublicationDescriptors)
    {
        if (ledgerPublicationDescriptors == null)
            throw new ArgumentNullException(nameof(ledgerPublicationDescriptors));
        return BuildPublicMaterialForProgressionCore(
            profileId,
            progression,
            requireCharacterFact,
            requireMotif,
            requiredOriginalFactIds,
            ledgerPublicationDescriptors);
    }

    private static NarrativePublicContextMaterial BuildPublicMaterialForProgressionCore(
        string profileId,
        CharacterProgression progression,
        bool requireCharacterFact,
        bool requireMotif,
        IEnumerable<string> requiredOriginalFactIds,
        IEnumerable<NarrativeLedgerPublicationDescriptor> ledgerPublicationDescriptors)
    {
        if (progression == null) throw new ArgumentNullException(nameof(progression));
        CharacterActor actor = progression.GetComponent<CharacterActor>();
        if (actor == null)
            throw new InvalidOperationException(
                "Public progression context requires its bound CharacterActor.");
        return BuildPublicMaterialForActorCore(
            profileId,
            actor,
            requireCharacterFact,
            requireMotif,
            requiredOriginalFactIds,
            ledgerPublicationDescriptors,
            includeCurrentNeeds: false);
    }

    public static void AddAuthoritativeFacts(
        NarrativeRequestContext context,
        CharacterId characterId,
        ICharacterNarrativeQuery narratives,
        ICharacterLifeQuery life,
        ICharacterNarrativeCatalog catalog)
    {
        if (context == null || !characterId.IsValid) return;
        if (narratives != null && narratives.TryGet(characterId, out CharacterNarrativeSnapshot narrative))
        {
            if (narrative.BackgroundId.IsValid)
                context.AddFact("fact:background:" + Token(narrative.BackgroundId.Value),
                    "Background: " + (catalog != null ? catalog.Require(narrative.BackgroundId).DisplayName : narrative.BackgroundId.Value), 92);
            if (narrative.CultureId.IsValid)
                context.AddFact("fact:culture:" + Token(narrative.CultureId.Value),
                    "Culture: " + (catalog != null ? catalog.Require(narrative.CultureId).DisplayName : narrative.CultureId.Value), 94);
            if (narrative.ActiveAmbitionId.IsValid)
                context.AddFact("fact:ambition:" + Token(narrative.ActiveAmbitionId.Value),
                    "Active ambition: " + (catalog != null ? catalog.Require(narrative.ActiveAmbitionId).DisplayName : narrative.ActiveAmbitionId.Value), 88);

            foreach (string traitId in narrative.VisibleLatentHeritableTraitIds ?? Array.Empty<string>())
                context.AddFact("fact:revealed-latent:" + Token(traitId),
                    "Analyzed latent trait: " + (catalog != null ? catalog.RequireHeritable(traitId).displayName : traitId), 82);
            foreach (CharacterNarrativeEventSaveData recent in
                (narrative.RecentEvents ?? Array.Empty<CharacterNarrativeEventSaveData>())
                    .OrderByDescending(value => value.absoluteDay).Take(8))
                context.AddFact(NarrativePublicContextFactory.BuildProjectedFactId(
                        "LifeEvent",
                        recent.eventId,
                        characterId.Value),
                    $"Recent event: {recent.eventId}; choice: {recent.choiceId}", 78);
        }

        if (life != null && life.TryGet(characterId, out CharacterLifeRecord record))
        {
            double chronologicalYears = record.ChronologicalAgeDays / (double)GameCalendarRules.DaysPerYear;
            double biologicalYears = record.BiologicalAgeDayUnits / GameCalendarRules.DaysPerYear;
            context.AddFact("fact:age:chronological", $"Chronological age: {chronologicalYears:0.0} years", 84);
            context.AddFact("fact:age:biological", $"Biological age: {biologicalYears:0.0} years; life stage: {record.LifeStage}", 86);
            foreach (CharacterAgeConditionState condition in record.AgeConditions
                .OrderByDescending(value => value.Severity).Take(4))
                context.AddFact("fact:age-condition:" + Token(condition.ConditionId),
                    $"Age condition: {condition.ConditionId}; severity: {condition.Severity}", 83);
        }
    }

    public static NarrativeRequestContext DefaultForProfile(string profileId)
    {
        LlmStaticSchemaDefinition schema = LlmStaticSchemaCatalog.Require(profileId);
        return NarrativeCultureStyleCatalog.Create(
            profileId, string.Empty, false, schema.PersistentNarrative);
    }

    private static void AddProgressionFacts(NarrativeRequestContext context, CharacterProgression progression)
    {
        string origin = progression.GrowthState.origin;
        if (!string.IsNullOrWhiteSpace(origin))
            context.AddFact("fact:origin:" + Token(origin), "Origin: " + origin, 85);
        context.AddFact("fact:potential:" + progression.GrowthState.potentialGrade,
            "Potential: " + CharacterSkillDisplay.Potential(progression.GrowthState.potentialGrade), 35);
        foreach (CharacterNarrativeFact fact in progression.NarrativeLedger.Facts
            .Where(value => value != null)
            .OrderByDescending(value => value.milestoneCount)
            .ThenByDescending(value => value.lastDay).Take(12))
            context.AddFact(NarrativePublicContextFactory.BuildProjectedFactId(
                    fact.domain.ToString(),
                    fact.factId,
                    fact.subjectId),
                $"Experience: {fact.domain} / {fact.factId} / {fact.outcome}",
                70 + Math.Min(10, fact.milestoneCount));
    }

    private static string ResolveCultureOrSpecies(CharacterActor actor)
    {
        string result = actor?.SpeciesTag ?? string.Empty;
        CharacterId characterId = actor != null ? actor.BuildingCharacterId : default;
        if (characterId.IsValid
            && authoritativeNarratives != null
            && authoritativeNarratives.TryGet(characterId, out CharacterNarrativeSnapshot snapshot)
            && snapshot.CultureId.IsValid)
        {
            result = snapshot.CultureId.Value;
        }
        return result;
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

    private static void AddAuthoritativePublicFacts(
        ICollection<NarrativePublicFactInput> facts,
        CharacterActor actor,
        string subjectId)
    {
        CharacterId characterId = actor.BuildingCharacterId;
        if (!characterId.IsValid) return;
        if (authoritativeNarratives != null
            && authoritativeNarratives.TryGet(characterId, out CharacterNarrativeSnapshot narrative))
        {
            if (narrative.BackgroundId.IsValid)
            {
                AddPublicFact(facts, "Background", "background:" + narrative.BackgroundId.Value,
                    subjectId,
                    "Background: " + (authoritativeCatalog != null
                        ? authoritativeCatalog.Require(narrative.BackgroundId).DisplayName
                        : narrative.BackgroundId.Value),
                    92,
                    NarrativePublicFactCategory.Background);
            }
            if (narrative.CultureId.IsValid)
            {
                AddPublicFact(facts, "Background", "culture:" + narrative.CultureId.Value,
                    subjectId,
                    "Culture: " + (authoritativeCatalog != null
                        ? authoritativeCatalog.Require(narrative.CultureId).DisplayName
                        : narrative.CultureId.Value),
                    94,
                    NarrativePublicFactCategory.Background);
            }
            if (narrative.ActiveAmbitionId.IsValid)
            {
                AddPublicFact(facts, "Background", "ambition:" + narrative.ActiveAmbitionId.Value,
                    subjectId,
                    "Active ambition: " + (authoritativeCatalog != null
                        ? authoritativeCatalog.Require(narrative.ActiveAmbitionId).DisplayName
                        : narrative.ActiveAmbitionId.Value),
                    88,
                    NarrativePublicFactCategory.Background);
            }
            foreach (string traitId in narrative.VisibleLatentHeritableTraitIds
                     ?? Array.Empty<string>())
            {
                AddPublicFact(facts, "Trait", "revealed-latent:" + traitId, subjectId,
                    "Analyzed latent trait: " + (authoritativeCatalog != null
                        ? authoritativeCatalog.RequireHeritable(traitId).displayName
                        : traitId),
                    82,
                    NarrativePublicFactCategory.Background);
            }
            foreach (CharacterNarrativeEventSaveData recent in
                     (narrative.RecentEvents ?? Array.Empty<CharacterNarrativeEventSaveData>())
                     .OrderByDescending(value => value.absoluteDay)
                     .ThenBy(value => value.eventId, StringComparer.Ordinal))
            {
                int? day = recent.absoluteDay > 0 ? recent.absoluteDay : null;
                facts.Add(new NarrativePublicFactInput(
                    "LifeEvent",
                    recent.eventId,
                    subjectId,
                    $"Recent event: {recent.eventId}; choice: {recent.choiceId}",
                    78,
                    NarrativePublicFactCategory.Memory,
                    publicSubjectId: string.Empty,
                    outcome: recent.choiceId,
                    lastDay: day,
                    count: 1,
                    milestoneCount: 1,
                    totalValue: null,
                    eventData: new NarrativePublicEventInput(
                        string.Empty,
                        subjectId,
                        string.Empty,
                        recent.eventId,
                        recent.choiceId,
                        day,
                        1,
                        null)));
            }
        }

        if (authoritativeLife != null
            && authoritativeLife.TryGet(characterId, out CharacterLifeRecord record))
        {
            double chronologicalYears = record.ChronologicalAgeDays
                / (double)GameCalendarRules.DaysPerYear;
            double biologicalYears = record.BiologicalAgeDayUnits
                / GameCalendarRules.DaysPerYear;
            AddPublicFact(facts, "Life", "age:chronological", subjectId,
                "Chronological age: " + chronologicalYears.ToString("0.0", CultureInfo.InvariantCulture)
                    + " years",
                84,
                NarrativePublicFactCategory.Memory);
            AddPublicFact(facts, "Life", "age:biological", subjectId,
                "Biological age: " + biologicalYears.ToString("0.0", CultureInfo.InvariantCulture)
                    + " years; life stage: " + record.LifeStage,
                86,
                NarrativePublicFactCategory.Memory);
            foreach (CharacterAgeConditionState condition in record.AgeConditions
                     .OrderByDescending(value => value.Severity)
                     .ThenBy(value => value.ConditionId, StringComparer.Ordinal))
            {
                AddPublicFact(facts, "Life", "age-condition:" + condition.ConditionId,
                    subjectId,
                    "Age condition: " + condition.ConditionId
                        + "; severity: " + condition.Severity,
                    83,
                    NarrativePublicFactCategory.Memory);
            }
        }
    }

    private static void AddProgressionPublicFacts(
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
                && TryResolveCharacterEntity(sourceSubjectId, out NarrativePublicEntityInput entity))
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

    private static bool TryResolveCharacterEntity(
        string persistentId,
        out NarrativePublicEntityInput entity)
    {
        entity = null;
        string target = persistentId?.Trim() ?? string.Empty;
        if (target.Length == 0) return false;
        CharacterActor[] candidates = (authoritativeWorld?.Characters
                ?? Array.Empty<CharacterActor>())
            .Concat(authoritativeLifetime?.AllCharacters ?? Array.Empty<CharacterActor>())
            .Where(value => value?.Identity != null
                && string.Equals(value.Identity.PersistentId, target, StringComparison.Ordinal))
            .Distinct()
            .ToArray();
        if (candidates.Length == 0) return false;
        string[] names = candidates
            .Select(value => value.Identity.DisplayName?.Trim() ?? string.Empty)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (names.Length != 1)
        {
            throw new InvalidOperationException(
                $"Character participant '{target}' has conflicting or missing public names.");
        }
        entity = new NarrativePublicEntityInput(
            target,
            NarrativePublicEntityKind.Character,
            names[0]);
        return true;
    }

    private static void AddPublicFact(
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

    private static string Token(string value)
    {
        string normalized = new string((value ?? string.Empty).Trim().ToLowerInvariant()
            .Where(character => char.IsLetterOrDigit(character) || character is ':' or '-' or '_')
            .Take(48).ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
    }
}

public sealed class NarrativeAuthorityContextBootstrap : IStartable
{
    private readonly ICharacterNarrativeQuery narratives;
    private readonly ICharacterLifeQuery life;
    private readonly ICharacterNarrativeCatalog catalog;
    private readonly ICharacterAiWorldRegistry world;

    public NarrativeAuthorityContextBootstrap(
        ICharacterNarrativeQuery narratives,
        ICharacterLifeQuery life,
        ICharacterNarrativeCatalog catalog,
        ICharacterAiWorldRegistry world)
    {
        this.narratives = narratives;
        this.life = life;
        this.catalog = catalog;
        this.world = world;
    }

    public void Start() => NarrativeRequestContextBuilder.ConfigureAuthorities(
        narratives, life, catalog, world, world);
}
