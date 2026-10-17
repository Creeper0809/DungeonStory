using static NarrativePublicCharacterProjection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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

public sealed class NarrativeRequestContextBuilder : IDisposable
{
    private ICharacterNarrativeQuery authoritativeNarratives;
    private ICharacterLifeQuery authoritativeLife;
    private ICharacterNarrativeCatalog authoritativeCatalog;
    private ICharacterWorldQuery authoritativeWorld;
    private ICharacterLifetimeQuery authoritativeLifetime;

    private bool disposed;

    public NarrativeRequestContextBuilder(
        ICharacterNarrativeQuery narratives,
        ICharacterLifeQuery life,
        ICharacterNarrativeCatalog catalog,
        ICharacterWorldQuery world,
        ICharacterLifetimeQuery lifetime)
    {
        authoritativeNarratives = narratives ?? throw new ArgumentNullException(nameof(narratives));
        authoritativeLife = life ?? throw new ArgumentNullException(nameof(life));
        authoritativeCatalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        authoritativeWorld = world ?? throw new ArgumentNullException(nameof(world));
        authoritativeLifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        authoritativeNarratives = null;
        authoritativeLife = null;
        authoritativeCatalog = null;
        authoritativeWorld = null;
        authoritativeLifetime = null;
    }

    private void RequireActive()
    {
        if (disposed) throw new ObjectDisposedException(nameof(NarrativeRequestContextBuilder));
    }

    public NarrativeRequestContext ForActor(
        string profileId,
        CharacterActor actor,
        bool requireCharacterFact,
        bool requireMotif)
    {
        RequireActive();
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

    public NarrativePublicContextMaterial BuildPublicMaterialForActor(
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

    public NarrativePublicContextMaterial BuildPublicMaterialForActor(
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

    internal NarrativePublicContextMaterial
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

    private NarrativePublicContextMaterial BuildPublicMaterialForActorCore(
        string profileId,
        CharacterActor actor,
        bool requireCharacterFact,
        bool requireMotif,
        IEnumerable<string> requiredOriginalFactIds,
        IEnumerable<NarrativeLedgerPublicationDescriptor> ledgerPublicationDescriptors,
        bool includeCurrentNeeds,
        IEnumerable<string> requiredProjectedFactIds = null)
    {
        RequireActive();
        var projection = new NarrativePublicCharacterProjection(
            AddAuthoritativePublicFacts,
            ResolveCultureOrSpecies,
            id => TryResolveCharacterEntity(id, out var entity) ? entity : null);
        return projection.Build(profileId, actor, requireCharacterFact, requireMotif,
            requiredOriginalFactIds, ledgerPublicationDescriptors, includeCurrentNeeds, requiredProjectedFactIds);
    }

    public NarrativePublicContextMaterial BuildPublicMaterialForProgression(
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

    internal NarrativePublicContextMaterial BuildSkillMaterialWithRequiredFacts(
        CharacterProgression progression, IEnumerable<string> requiredProjectedFactIds)
    {
        RequireActive();
        if (progression == null) throw new ArgumentNullException(nameof(progression));
        return BuildPublicMaterialForActorCore(
            LocalLlmRequestProfiles.CharacterSkill.Id,
            progression.GetComponent<CharacterActor>(), true, true, null, null,
            includeCurrentNeeds: false, requiredProjectedFactIds: requiredProjectedFactIds);
    }

    public NarrativePublicContextMaterial BuildPublicMaterialForProgression(
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

    private NarrativePublicContextMaterial BuildPublicMaterialForProgressionCore(
        string profileId,
        CharacterProgression progression,
        bool requireCharacterFact,
        bool requireMotif,
        IEnumerable<string> requiredOriginalFactIds,
        IEnumerable<NarrativeLedgerPublicationDescriptor> ledgerPublicationDescriptors)
    {
        RequireActive();
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

    private string ResolveCultureOrSpecies(CharacterActor actor)
    {
        string result = actor?.SpeciesTag ?? string.Empty;
        CharacterId characterId = actor != null ? actor.BuildingCharacterId : default;
        if (characterId.IsValid
            && authoritativeNarratives.TryGet(characterId, out CharacterNarrativeSnapshot snapshot)
            && snapshot.CultureId.IsValid)
        {
            result = snapshot.CultureId.Value;
        }
        return result;
    }

    private void AddAuthoritativePublicFacts(
        ICollection<NarrativePublicFactInput> facts,
        CharacterActor actor,
        string subjectId)
    {
        CharacterId characterId = actor.BuildingCharacterId;
        if (!characterId.IsValid) return;
        if (authoritativeNarratives.TryGet(characterId, out CharacterNarrativeSnapshot narrative))
        {
            if (narrative.BackgroundId.IsValid)
            {
                AddPublicFact(facts, "Background", "background:" + narrative.BackgroundId.Value,
                    subjectId,
                    "Background: " + authoritativeCatalog.Require(narrative.BackgroundId).DisplayName,
                    92,
                    NarrativePublicFactCategory.Background);
            }
            if (narrative.CultureId.IsValid)
            {
                AddPublicFact(facts, "Background", "culture:" + narrative.CultureId.Value,
                    subjectId,
                    "Culture: " + authoritativeCatalog.Require(narrative.CultureId).DisplayName,
                    94,
                    NarrativePublicFactCategory.Background);
            }
            if (narrative.ActiveAmbitionId.IsValid)
            {
                AddPublicFact(facts, "Background", "ambition:" + narrative.ActiveAmbitionId.Value,
                    subjectId,
                    "Active ambition: " + authoritativeCatalog.Require(narrative.ActiveAmbitionId).DisplayName,
                    88,
                    NarrativePublicFactCategory.Background);
            }
            foreach (string traitId in narrative.VisibleLatentHeritableTraitIds
                     ?? Array.Empty<string>())
            {
                AddPublicFact(facts, "Trait", "revealed-latent:" + traitId, subjectId,
                    "Analyzed latent trait: " + authoritativeCatalog.RequireHeritable(traitId).displayName,
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

        if (authoritativeLife.TryGet(characterId, out CharacterLifeRecord record))
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

    private bool TryResolveCharacterEntity(
        string persistentId,
        out NarrativePublicEntityInput entity)
    {
        entity = null;
        string target = persistentId?.Trim() ?? string.Empty;
        if (target.Length == 0) return false;
        CharacterActor[] candidates = authoritativeWorld.Characters
            .Concat(authoritativeLifetime.AllCharacters)
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

    private static string Token(string value)
    {
        string normalized = new string((value ?? string.Empty).Trim().ToLowerInvariant()
            .Where(character => char.IsLetterOrDigit(character) || character is ':' or '-' or '_')
            .Take(48).ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
    }
}
