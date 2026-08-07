using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

/// <summary>
/// Advances authored reproduction processes once per operating day and turns a
/// completed process into exactly one physical world character. The published
/// result character ID is persisted on the process, so reload cannot duplicate
/// a birth or golem activation.
/// </summary>
public sealed class ReproductionApplicationAdapter : IStartable, IDisposable
{
    private readonly IReproductionService reproduction;
    private readonly ICharacterWorldQuery world;
    private readonly ICharacterBodyHealthQuery bodyHealth;
    private readonly IClimateQuery climate;
    private readonly ICharacterLifeDefinitionCatalog lifeDefinitions;
    private readonly ICharacterLifeQuery life;
    private readonly ICharacterLifeCommand lifeCommands;
    private readonly IKinshipCommand kinship;
    private readonly IGameContentCatalog content;
    private readonly ICharacterSpawnObjectFactory characterObjects;
    private readonly ICharacterSpawnerProvider spawners;
    private readonly IGameEventBus events;
    private IDisposable dayEndedSubscription;

    public ReproductionApplicationAdapter(
        IReproductionService reproduction,
        ICharacterWorldQuery world,
        ICharacterBodyHealthQuery bodyHealth,
        IClimateQuery climate,
        ICharacterLifeDefinitionCatalog lifeDefinitions,
        ICharacterLifeQuery life,
        ICharacterLifeCommand lifeCommands,
        IKinshipCommand kinship,
        IGameContentCatalog content,
        ICharacterSpawnObjectFactory characterObjects,
        ICharacterSpawnerProvider spawners,
        IGameEventBus events)
    {
        this.reproduction = reproduction
            ?? throw new ArgumentNullException(nameof(reproduction));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.bodyHealth = bodyHealth ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.climate = climate ?? throw new ArgumentNullException(nameof(climate));
        this.lifeDefinitions = lifeDefinitions
            ?? throw new ArgumentNullException(nameof(lifeDefinitions));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.lifeCommands = lifeCommands
            ?? throw new ArgumentNullException(nameof(lifeCommands));
        this.kinship = kinship ?? throw new ArgumentNullException(nameof(kinship));
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.characterObjects = characterObjects
            ?? throw new ArgumentNullException(nameof(characterObjects));
        this.spawners = spawners ?? throw new ArgumentNullException(nameof(spawners));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Start() => dayEndedSubscription ??=
        events.Subscribe<OperatingDayEndedEvent>(OnDayEnded);

    public void Dispose()
    {
        dayEndedSubscription?.Dispose();
        dayEndedSubscription = null;
    }

    private void OnDayEnded(OperatingDayEndedEvent ended)
    {
        int absoluteDay = ended.day + 1;
        ReproductionProcess[] processes = reproduction.Processes
            .Where(process => process.Status != ReproductionProcessStatus.Failed
                && !process.ResultPublished)
            .OrderBy(process => process.ProcessId, StringComparer.Ordinal)
            .ToArray();

        foreach (ReproductionProcess process in processes)
        {
            if (process.Status != ReproductionProcessStatus.Completed)
            {
                CharacterActor carrier = FindActor(process.CarrierId);
                float health = ResolveHealth(carrier);
                float nutrition = ResolveNutrition(carrier);
                reproduction.AdvanceProcess(
                    process.ProcessId,
                    new ReproductionDailyContext(
                        absoluteDay,
                        health,
                        nutrition,
                        climate.OutdoorTemperatureC,
                        ResolveFertilityCoefficient(process)));
            }

            ReproductionProcess current = reproduction.Processes.First(value =>
                string.Equals(
                    value.ProcessId,
                    process.ProcessId,
                    StringComparison.Ordinal));
            if (current.Status == ReproductionProcessStatus.Completed
                && !current.ResultPublished)
            {
                PublishResult(current, absoluteDay);
            }
        }
    }

    private void PublishResult(ReproductionProcess process, int absoluteDay)
    {
        if (!spawners.TryGetSpawner(out CharacterSpawner spawner)
            || spawner.characterPrefab == null)
        {
            throw new InvalidOperationException(
                $"Reproduction process '{process.ProcessId}' completed without a character spawn prefab.");
        }

        CharacterSO archetype = ResolveArchetype(process.PhenotypeSpeciesId);
        CharacterSpawnRequest request = new(
            archetype.DefinitionId,
            process.PhenotypeSpeciesId,
            archetype.VisualVariantId,
            ResolveOffspringRole(process),
            process.ExpressedTraitIds.Select(value => new CharacterTraitId(value)),
            process.LatentTraitIds.Select(value => new CharacterTraitId(value)),
            process.InnateAptitudes.ToDictionary(
                value => value.skillId,
                value => Math.Clamp(value.value, 0, 100),
                StringComparer.Ordinal));

        GameObject characterObject = characterObjects.CreateInactive(
            spawner.characterPrefab);
        CharacterActor actor = CharacterActorCollection.GetCanonical(
            characterObject.GetComponent<CharacterActor>());
        if (actor == null)
        {
            characterObjects.Destroy(characterObject);
            throw new InvalidOperationException(
                "The authored character prefab has no canonical CharacterActor.");
        }

        actor.Initialize(archetype, request);
        actor.characterType = CharacterType.NPC;
        CharacterId childId = CharacterPersistentIdentity.Require(actor);
        actor.gameObject.name = $"Newborn {process.PhenotypeSpeciesId.Value} {childId.Value}";
        actor.transform.position = ResolveBirthPosition(process, spawner);

        SpeciesLifeHistoryDefinition history = lifeDefinitions.RequireLifeHistory(
            process.PhenotypeSpeciesId);
        bool activatedGolem = process.Mode == ReproductionMode.GolemAssembly;
        lifeCommands.Register(
            childId,
            process.PhenotypeSpeciesId,
            chronologicalAgeDays: 0,
            biologicalAgeDayUnits: activatedGolem ? history.AdultAgeDayUnits : 0d,
            birthdayDayOfYear: GameCalendarRules.Project(absoluteDay, 0).DayOfYear);

        if (activatedGolem)
        {
            kinship.SetGuardian(childId, process.FirstParentId);
        }
        else
        {
            if (process.FirstParentId.Equals(process.SecondParentId))
                throw new InvalidOperationException(
                    $"Reproduction process '{process.ProcessId}' has the same character as both genetic parents.");
            kinship.AddParent(childId, process.FirstParentId, adoptive: false);
            kinship.AddParent(childId, process.SecondParentId, adoptive: false);
        }

        characterObjects.Publish(characterObject);
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        actor.Brain?.UseStaffWorkActions();
        actor.Brain?.RequestImmediateReplan(clearFailures: true);
        reproduction.MarkResultPublished(process.ProcessId, childId);
    }

    private CharacterSO ResolveArchetype(CharacterSpeciesId phenotypeSpeciesId)
    {
        CharacterSO[] candidates = content.GetAll<CharacterSO>()
            .Where(value => value != null
                && value.DefinitionId.IsValid
                && value.species != null
                && value.species.DefinitionId.Equals(phenotypeSpeciesId)
                && value.role != CharacterRole.Owner)
            .OrderBy(value => value.characterType == CharacterType.NPC ? 0 : 1)
            .ThenBy(value => value.DefinitionId.Value, StringComparer.Ordinal)
            .ToArray();
        return candidates.FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"No authored non-owner character archetype exists for phenotype '{phenotypeSpeciesId.Value}'.");
    }

    private CharacterActor FindActor(CharacterId id) =>
        world.Characters.FirstOrDefault(candidate =>
            CharacterPersistentIdentity.TryGet(candidate, out CharacterId candidateId)
            && candidateId.Equals(id));

    private float ResolveHealth(CharacterActor actor)
    {
        if (actor == null || actor.IsDead) return 0f;
        CharacterVitalsSnapshot vitals = bodyHealth.GetVitals(actor);
        return Mathf.Clamp(
            vitals.CurrentHealth / Mathf.Max(1f, vitals.MaximumHealth) * 100f,
            0f,
            100f);
    }

    private static float ResolveNutrition(CharacterActor actor)
    {
        return actor?.Stats != null
            && actor.Stats.TryGetConditionValue(
                CharacterCondition.HUNGER,
                out float nutrition)
                    ? Mathf.Clamp(nutrition, 0f, 100f)
                    : 100f;
    }

    private Vector3 ResolveBirthPosition(
        ReproductionProcess process,
        CharacterSpawner spawner)
    {
        CharacterActor carrier = FindActor(process.CarrierId);
        if (carrier != null) return carrier.transform.position;
        CharacterActor firstParent = FindActor(process.FirstParentId);
        return firstParent != null
            ? firstParent.transform.position
            : spawner.GetEntryDoorWorldPosition();
    }

    private static ReproductiveRole ResolveOffspringRole(
        ReproductionProcess process)
    {
        bool firstRole = (PersistentEntityId.GetStableHash32(process.ProcessId) & 1u) == 0u;
        return process.Mode switch
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
            _ => ReproductiveRole.None
        };
    }

    private float ResolveFertilityCoefficient(ReproductionProcess process)
    {
        if (process.Mode == ReproductionMode.GolemAssembly)
            return 1f;
        float first = ResolveFertilityCoefficient(process.FirstParentId);
        float second = ResolveFertilityCoefficient(process.SecondParentId);
        return Mathf.Min(first, second);
    }

    private float ResolveFertilityCoefficient(CharacterId characterId)
    {
        if (!life.TryGet(characterId, out CharacterLifeRecord record))
            return 0f;
        SpeciesLifeHistoryDefinition history = lifeDefinitions.RequireLifeHistory(
            record.PhenotypeSpeciesId);
        double ageYears = record.BiologicalAgeDayUnits
            / GameCalendarRules.DaysPerYear;
        double span = Math.Max(1d, history.ElderAgeYears - history.AdultAgeYears);
        return (float)Math.Clamp(
            (history.ElderAgeYears - ageYears) / span,
            0d,
            1d);
    }
}
