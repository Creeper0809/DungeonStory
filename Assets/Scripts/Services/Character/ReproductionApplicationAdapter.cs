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
    private readonly IReproductionProcessTransitionCommand transitions;
    private readonly ICharacterWorldQuery world;
    private readonly ICharacterBodyHealthQuery bodyHealth;
    private readonly IClimateQuery climate;
    private readonly ICharacterLifeDefinitionCatalog lifeDefinitions;
    private readonly ICharacterLifeQuery life;
    private readonly IReproductionBirthCommand births;
    private readonly IGameEventBus events;
    private readonly IHeritableTraitEffectQuery heritableTraits;
    private IDisposable dayEndedSubscription;

    public ReproductionApplicationAdapter(
        IReproductionService reproduction,
        IReproductionProcessTransitionCommand transitions,
        ICharacterWorldQuery world,
        ICharacterBodyHealthQuery bodyHealth,
        IClimateQuery climate,
        ICharacterLifeDefinitionCatalog lifeDefinitions,
        ICharacterLifeQuery life,
        IReproductionBirthCommand births,
        IGameEventBus events,
        IHeritableTraitEffectQuery heritableTraits)
    {
        this.reproduction = reproduction
            ?? throw new ArgumentNullException(nameof(reproduction));
        this.transitions = transitions
            ?? throw new ArgumentNullException(nameof(transitions));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.bodyHealth = bodyHealth ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.climate = climate ?? throw new ArgumentNullException(nameof(climate));
        this.lifeDefinitions = lifeDefinitions
            ?? throw new ArgumentNullException(nameof(lifeDefinitions));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.births = births ?? throw new ArgumentNullException(nameof(births));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.heritableTraits = heritableTraits
            ?? throw new ArgumentNullException(nameof(heritableTraits));
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
            .Where(process => process.Status != ReproductionProcessStatus.Planned
                && process.Status != ReproductionProcessStatus.Failed
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
                if (!transitions.TryAdvance(
                        process.ProcessId,
                        new ReproductionDailyContext(
                        absoluteDay,
                        health,
                        nutrition,
                        climate.OutdoorTemperatureC,
                        ResolveFertilityCoefficient(process),
                        ResolveGestationStability(process)),
                        out DomainFailure transitionFailure))
                {
                    throw new InvalidOperationException(
                        "Reproduction daily transition failed: "
                        + transitionFailure.Code + " "
                        + string.Join(",", transitionFailure.Parameters.ToArray()));
                }
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
        if (!births.TryPublish(
                process.ProcessId,
                absoluteDay,
                out DomainFailure failure))
            throw new InvalidOperationException(
                "Reproduction birth transaction remains pending: "
                + failure.Code + " "
                + string.Join(",", failure.Parameters.ToArray()));
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

    private float ResolveFertilityCoefficient(ReproductionProcess process)
    {
        if (process.Mode == ReproductionMode.GolemAssembly)
            return 1f;
        float first = ResolveFertilityCoefficient(process.FirstParentId);
        float second = ResolveFertilityCoefficient(process.SecondParentId);
        float inherited = Mathf.Min(
            heritableTraits.GetMultiplier(
                process.FirstParentId,
                HeritableTraitConsequenceKind.Fertility,
                "success-rate"),
            heritableTraits.GetMultiplier(
                process.SecondParentId,
                HeritableTraitConsequenceKind.Fertility,
                "success-rate"));
        return ReproductionRules.ApplyFertilityTreatmentToCoefficient(
            Mathf.Min(first, second) * inherited * (process.CrossLineageIncubatorUsed
                ? Mathf.Min(
                    heritableTraits.GetMultiplier(process.FirstParentId,
                        HeritableTraitConsequenceKind.Fertility, "cross-lineage"),
                    heritableTraits.GetMultiplier(process.SecondParentId,
                        HeritableTraitConsequenceKind.Fertility, "cross-lineage")) : 1f),
            process.FertilityTreatmentUsed);
    }

    private float ResolveGestationStability(ReproductionProcess process)
    {
        if (!process.CarrierId.IsValid) return 1f;
        float stability = heritableTraits.GetMultiplier(
            process.CarrierId,
            HeritableTraitConsequenceKind.Fertility,
            "gestation-stability");
        float offspring = heritableTraits.GetMultiplier(
            process.CarrierId,
            HeritableTraitConsequenceKind.Fertility,
            "offspring-stability");
        return ReproductionRules.ApplyFertilityTreatmentToGestationStability(
            stability * offspring,
            process.FertilityTreatmentUsed);
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
