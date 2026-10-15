using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class AnimalHusbandryCommandFactMapping
{
    private static readonly GameplayEntityKindId WildlifeKind =
        new("wildlife");
    private static readonly GameplayOutcomeFactId KindFact =
        new("husbandry-kind");
    private static readonly GameplayOutcomeFactId PenFact = new("pen-id");
    private static readonly GameplayOutcomeFactId AnimalFact =
        new("animal-id");

    public static MigratedDomainFactCapture Capture(
        AnimalHusbandryCommandFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.AnimalHusbandryCommandResult,
            fact.OperationId,
            fact.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            BuildCommandDetails(fact));
    }

    private static MigratedProducerOutcomeDetails BuildCommandDetails(
        AnimalHusbandryCommandFact fact)
    {
        AnimalPenPolicyData beforePolicy = fact.CaptureBeforePolicy();
        AnimalPenPolicyData afterPolicy = fact.CaptureAfterPolicy();
        HusbandryAnimalState beforeAnimal = fact.CaptureBeforeAnimal();
        HusbandryAnimalState afterAnimal = fact.CaptureAfterAnimal();
        string stateBefore;
        string stateAfter;
        long previousValue;
        long currentValue;
        if (fact.Kind == AnimalHusbandryOutcomeOperationKind.SetPenPolicy)
        {
            stateBefore = PolicyState(beforePolicy);
            stateAfter = PolicyState(afterPolicy);
            previousValue = beforePolicy.maximumAnimals;
            currentValue = afterPolicy.maximumAnimals;
        }
        else if (fact.Kind
                 == AnimalHusbandryOutcomeOperationKind.DesignateSlaughter)
        {
            stateBefore = AnimalState(beforeAnimal);
            stateAfter = AnimalState(afterAnimal);
            previousValue = beforeAnimal.SlaughterDesignated ? 1L : 0L;
            currentValue = afterAnimal.SlaughterDesignated ? 1L : 0L;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(fact));
        }

        var subjects = new List<MigratedProducerOutcomeSubject>
        {
            new(
                MigratedProducerOutcomeIds.FacilityKind,
                fact.PenId.Value,
                fact.PenId.Value,
                MigratedProducerOutcomeIds.FacilityRole)
        };
        if (fact.AnimalId.IsValid)
        {
            subjects.Add(new MigratedProducerOutcomeSubject(
                WildlifeKind,
                fact.AnimalId.Value,
                fact.SpeciesId.IsValid
                    ? fact.SpeciesId.Value
                    : fact.AnimalId.Value,
                MigratedProducerOutcomeIds.TargetRole));
        }
        GameplayEntityId metricSubject = new(
            MigratedProducerOutcomeIds.FacilityKind,
            fact.PenId.Value);
        string kind = AnimalHusbandryDomainOperationIdentity.StableKind(
            fact.Kind);
        return new MigratedProducerOutcomeDetails(
            subjects.ToArray(),
            new[]
            {
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.PreviousMetric,
                    previousValue,
                    MigratedProducerOutcomeIds.CountUnit,
                    metricSubject),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.CurrentMetric,
                    currentValue,
                    MigratedProducerOutcomeIds.CountUnit,
                    metricSubject)
            },
            new[]
            {
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.OperationFact,
                    fact.OperationId),
                new GameplayOutcomeFact(KindFact, kind),
                new GameplayOutcomeFact(PenFact, fact.PenId.Value),
                new GameplayOutcomeFact(
                    AnimalFact,
                    fact.AnimalId.IsValid ? fact.AnimalId.Value : "none"),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.SpeciesFact,
                    fact.SpeciesId.IsValid ? fact.SpeciesId.Value : "none"),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.BeforeFact,
                    stateBefore),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.AfterFact,
                    stateAfter)
            },
            kind
                + " 축산 명령이 검증된 상태 변경과 함께 확정되었다.");
    }

    private static string PolicyState(AnimalPenPolicyData policy)
    {
        if (policy == null)
            return "none";
        return "species=" + string.Join(",", (policy.AllowedSpeciesIds
                ?? new List<WildlifeSpeciesId>())
            .Select(value => value.Value)
            .OrderBy(value => value, StringComparer.Ordinal))
            + ";diet=" + Bool(policy.allowHerbivores)
            + Bool(policy.allowOmnivores)
            + Bool(policy.allowCarnivores)
            + Bool(policy.allowScavengers)
            + ";sex=" + Bool(policy.allowFemales)
            + Bool(policy.allowMales)
            + Bool(policy.allowJuveniles)
            + ";max=" + policy.maximumAnimals
            + ";breed=" + Bool(policy.breedingAllowed)
            + ";pregnant=" + Bool(policy.protectPregnant)
            + ";risk=" + Bool(policy.allowRiskyMixing)
            + ";limits=" + policy.adultFemaleLimit
            + "," + policy.adultMaleLimit
            + "," + policy.juvenileLimit
            + "," + policy.minimumBreedingFemales
            + "," + policy.minimumBreedingMales;
    }

    private static string AnimalState(HusbandryAnimalState state)
    {
        if (state == null)
            return "missing";
        string products = string.Join(",", (state.Products
                ?? new List<AnimalProductProgressState>())
            .Where(value => value != null)
            .OrderBy(value => value.ItemId.Value, StringComparer.Ordinal)
            .Select(value => value.ItemId.Value + ":" + value.ReadyCycles));
        return "tamed=" + Bool(state.Tamed)
            + ";taming=" + Mathf.RoundToInt(state.TamingProgress * 1000f)
            + ";slaughter=" + Bool(state.SlaughterDesignated)
            + ";auto=" + Bool(state.AutoSlaughterDesignated)
            + ";manure=" + state.ReadyManureCycles
            + ";products=" + (products.Length == 0 ? "none" : products)
            + ";status=" + (int)state.StatusCode;
    }

    private static string Bool(bool value) => value ? "1" : "0";
}
