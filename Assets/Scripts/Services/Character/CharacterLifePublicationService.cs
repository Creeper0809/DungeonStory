using System;
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
    private readonly IRandomStream random;

    public CharacterLifePublicationService(
        ICharacterLifeQuery query,
        ICharacterLifeCommand commands,
        ICharacterLifeDefinitionCatalog definitions,
        IReproductionDefinitionCatalog reproductionDefinitions,
        ICharacterRuntimeProfileFactory profileFactory,
        IRandomStreamProvider randomStreams)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.definitions = definitions
            ?? throw new ArgumentNullException(nameof(definitions));
        this.reproductionDefinitions = reproductionDefinitions
            ?? throw new ArgumentNullException(nameof(reproductionDefinitions));
        this.profileFactory = profileFactory
            ?? throw new ArgumentNullException(nameof(profileFactory));
        random = (randomStreams ?? throw new ArgumentNullException(nameof(randomStreams)))
            .Get(InitialAgeRandomStreamId);
    }

    public void EnsureRegistered(CharacterActor actor)
    {
        if (!IsPersistentLifeActor(actor))
        {
            return;
        }

        CharacterId characterId = CharacterPersistentIdentity.Require(actor);
        EnsureReproductiveRole(actor, characterId);
        EnsureRegistered(characterId, new CharacterSpeciesId(actor.SpeciesTag));
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
        if (query.TryGet(characterId, out _))
        {
            return;
        }

        SpeciesLifeHistoryDefinition history = definitions.RequireLifeHistory(
            phenotypeSpeciesId);
        double biologicalAgeYears = SampleInitialBiologicalAgeYears(history);
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
    }

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
