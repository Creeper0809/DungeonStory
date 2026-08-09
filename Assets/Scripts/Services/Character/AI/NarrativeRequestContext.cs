using System;
using System.Linq;
using VContainer.Unity;

public static class NarrativeRequestContextBuilder
{
    private static ICharacterNarrativeQuery authoritativeNarratives;
    private static ICharacterLifeQuery authoritativeLife;
    private static ICharacterNarrativeCatalog authoritativeCatalog;

    public static void ConfigureAuthorities(
        ICharacterNarrativeQuery narratives,
        ICharacterLifeQuery life,
        ICharacterNarrativeCatalog catalog)
    {
        authoritativeNarratives = narratives;
        authoritativeLife = life;
        authoritativeCatalog = catalog;
    }

    public static NarrativeRequestContext ForActor(
        string profileId,
        CharacterActor actor,
        bool requireCharacterFact,
        bool requireMotif)
    {
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
                context.AddFact("fact:event:" + Token(recent.eventId),
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
            context.AddFact("fact:history:" + Token(fact.factId),
                $"Experience: {fact.domain} / {fact.factId} / {fact.outcome}",
                70 + Math.Min(10, fact.milestoneCount));
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

    public NarrativeAuthorityContextBootstrap(
        ICharacterNarrativeQuery narratives,
        ICharacterLifeQuery life,
        ICharacterNarrativeCatalog catalog)
    {
        this.narratives = narratives;
        this.life = life;
        this.catalog = catalog;
    }

    public void Start() => NarrativeRequestContextBuilder.ConfigureAuthorities(
        narratives, life, catalog);
}
