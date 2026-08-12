using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

public interface ICharacterLifePublicationService
{
    void EnsureRegistered(CharacterActor actor);
    void EnsureRegistered(
        CharacterId characterId,
        CharacterSpeciesId phenotypeSpeciesId);
}

public sealed class CharacterLifePublicationService :
    ICharacterLifePublicationService
{
    private const string InitialAgeRandomStreamId = "population:initial-age";
    private readonly ICharacterLifeQuery query;
    private readonly ICharacterLifeCommand commands;
    private readonly ICharacterLifeDefinitionCatalog definitions;
    private readonly IReproductionDefinitionCatalog reproductionDefinitions;
    private readonly ICharacterRuntimeProfileFactory profileFactory;
    private readonly ICharacterNarrativeQuery narrativeQuery;
    private readonly ICharacterNarrativeCommand narrativeCommands;
    private readonly HashSet<string> heritableTraitIds;
    private readonly IRandomStream random;
    private readonly IGameEventBus events;

    public CharacterLifePublicationService(
        ICharacterLifeQuery query,
        ICharacterLifeCommand commands,
        ICharacterLifeDefinitionCatalog definitions,
        IReproductionDefinitionCatalog reproductionDefinitions,
        ICharacterRuntimeProfileFactory profileFactory,
        ICharacterNarrativeQuery narrativeQuery,
        ICharacterNarrativeCommand narrativeCommands,
        ICharacterNarrativeCatalog narrativeCatalog,
        IRandomStreamProvider randomStreams,
        IGameEventBus events)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.definitions = definitions
            ?? throw new ArgumentNullException(nameof(definitions));
        this.reproductionDefinitions = reproductionDefinitions
            ?? throw new ArgumentNullException(nameof(reproductionDefinitions));
        this.profileFactory = profileFactory
            ?? throw new ArgumentNullException(nameof(profileFactory));
        this.narrativeQuery = narrativeQuery
            ?? throw new ArgumentNullException(nameof(narrativeQuery));
        this.narrativeCommands = narrativeCommands
            ?? throw new ArgumentNullException(nameof(narrativeCommands));
        heritableTraitIds = new HashSet<string>(
            (narrativeCatalog
                ?? throw new ArgumentNullException(nameof(narrativeCatalog)))
            .HeritableTraits
            .Where(value => value != null)
            .Select(value => value.traitId?.Trim() ?? string.Empty)
            .Where(id => id.Length > 0),
            StringComparer.Ordinal);
        random = (randomStreams ?? throw new ArgumentNullException(nameof(randomStreams)))
            .Get(InitialAgeRandomStreamId);
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void EnsureRegistered(CharacterActor actor)
    {
        if (!IsPersistentLifeActor(actor))
        {
            return;
        }

        CharacterId characterId = CharacterPersistentIdentity.Require(actor);
        EnsureReproductiveRole(actor, characterId);
        CharacterRuntimeProfile profile = actor.Identity?.Profile;
        EnsureRegisteredCore(
            characterId,
            new CharacterSpeciesId(actor.SpeciesTag),
            profile?.ExpressedTraitIds ?? Array.Empty<string>(),
            profile?.LatentTraitIds ?? Array.Empty<string>(),
            actor.Progression?.GrowthState?.startingProficiencies,
            actor.Progression?.GrowthState?.startingProfile);
    }

    private static bool IsPersistentLifeActor(CharacterActor actor)
    {
        return actor != null
            && (actor.IsOwner
                || (actor.Identity?.CharacterType == CharacterType.NPC
                    && actor.TryGetAbility(out AbilityWork _)));
    }

    public void EnsureRegistered(
        CharacterId characterId,
        CharacterSpeciesId phenotypeSpeciesId)
    {
        EnsureRegisteredCore(
            characterId,
            phenotypeSpeciesId,
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            null);
    }

    private void EnsureRegisteredCore(
        CharacterId characterId,
        CharacterSpeciesId phenotypeSpeciesId,
        IReadOnlyList<string> expressedTraitIds,
        IReadOnlyList<string> latentTraitIds,
        IReadOnlyList<CharacterStartingProficiencyExperience>
            startingProficiencies,
        CharacterStartingProfileState startingProfile)
    {
        bool registeredLife = false;
        if (!query.TryGet(characterId, out _))
        {
            SpeciesLifeHistoryDefinition history = definitions.RequireLifeHistory(
                phenotypeSpeciesId);
            double biologicalAgeYears = startingProfile?.prepared == true
                ? Math.Clamp(
                    startingProfile.biologicalAgeYears,
                    history.AdultAgeYears,
                    Math.Max(
                        history.AdultAgeYears,
                        history.UntreatedExpectedLifeYears))
                : SampleInitialBiologicalAgeYears(history);
            double biologicalAgeUnits = biologicalAgeYears * GameCalendarRules.DaysPerYear;
            int chronologicalAgeDays = CalculateChronologicalAgeDays(
                history,
                biologicalAgeUnits);
            int birthday = 1 + Math.Min(
                GameCalendarRules.DaysPerYear - 1,
                (int)Math.Floor(random.NextFloat() * GameCalendarRules.DaysPerYear));
            commands.Register(
                characterId,
                phenotypeSpeciesId,
                chronologicalAgeDays,
                biologicalAgeUnits,
                birthday);
            registeredLife = true;
        }

        if (registeredLife && startingProfile?.prepared == true)
        {
            startingProfile.EnsureCollections();
            IReadOnlyList<AgeConditionChange> changes =
                commands.AddInitialAgeConditions(
                    characterId,
                    startingProfile.initialAgeConditionIds);
            foreach (AgeConditionChange change in changes)
                events.Publish(new CharacterAgeConditionChangedEvent(change));
        }

        if (!narrativeQuery.TryGet(characterId, out _))
        {
            narrativeCommands.Register(
                characterId,
                phenotypeSpeciesId,
                FilterHeritableTraits(expressedTraitIds),
                FilterHeritableTraits(latentTraitIds),
                startingProficiencies);
        }
    }

    private IReadOnlyList<string> FilterHeritableTraits(
        IEnumerable<string> traitIds) =>
        (traitIds ?? Array.Empty<string>())
        .Where(id => !string.IsNullOrWhiteSpace(id)
            && heritableTraitIds.Contains(id.Trim()))
        .Select(id => id.Trim())
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private double SampleInitialBiologicalAgeYears(
        SpeciesLifeHistoryDefinition history)
    {
        double selector = ClampUnit(random.NextFloat());
        double withinBand = ClampUnit(random.NextFloat());
        double adult = history.AdultAgeYears;
        double elder = history.ElderAgeYears;
        double adultSpan = Math.Max(0d, elder - adult);
        if (selector < 0.40d)
        {
            return adult + adultSpan * 0.25d * withinBand;
        }
        if (selector < 0.75d)
        {
            return adult + adultSpan * (0.25d + 0.35d * withinBand);
        }
        if (selector < 0.95d)
        {
            return adult + adultSpan * (0.60d + 0.40d * withinBand);
        }

        return elder + 10d * withinBand;
    }

    private static int CalculateChronologicalAgeDays(
        SpeciesLifeHistoryDefinition history,
        double biologicalAgeUnits)
    {
        double adultUnits = history.AdultAgeDayUnits;
        double result = Math.Min(adultUnits, biologicalAgeUnits) / 4d;
        if (biologicalAgeUnits > adultUnits)
        {
            result += (biologicalAgeUnits - adultUnits) / 6d;
        }
        return Math.Max(0, (int)Math.Floor(result));
    }

    private static double ClampUnit(double value) =>
        Math.Max(0d, Math.Min(0.999999999999d, value));

    private void EnsureReproductiveRole(
        CharacterActor actor,
        CharacterId characterId)
    {
        CharacterRuntimeProfile profile = actor?.Identity?.Profile;
        CharacterSO archetype = actor?.Identity?.Data;
        if (profile == null || archetype == null
            || profile.ReproductiveRole != ReproductiveRole.None)
        {
            return;
        }

        ReproductionDefinition definition = reproductionDefinitions
            .RequireReproduction(profile.PhenotypeSpeciesId);
        bool firstRole = (PersistentEntityId.GetStableHash32(characterId.Value) & 1u) == 0u;
        ReproductiveRole role = definition.Mode switch
        {
            ReproductionMode.Pregnancy => firstRole
                ? ReproductiveRole.Carrier
                : ReproductiveRole.Contributor,
            ReproductionMode.Egg => firstRole
                ? ReproductiveRole.Layer
                : ReproductiveRole.Fertilizer,
            ReproductionMode.Spore => ReproductiveRole.SporeContributor,
            ReproductionMode.CoreDivision => ReproductiveRole.DivisionCore,
            ReproductionMode.GolemAssembly => ReproductiveRole.Assembler,
            _ => throw new ArgumentOutOfRangeException()
        };
        CharacterSpawnRequest request = new(
            profile.CharacterArchetypeId,
            profile.PhenotypeSpeciesId,
            profile.VisualVariantId,
            role,
            profile.ExpressedTraitIds.Select(value => new CharacterTraitId(value)),
            profile.LatentTraitIds.Select(value => new CharacterTraitId(value)),
            profile.InnateAptitudes);
        actor.Identity.SetData(archetype, profileFactory.Create(request));
    }
}
