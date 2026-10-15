using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class AnimalHusbandryWorkFactMapping
{
    private static readonly GameplayEntityKindId WildlifeKind =
        new("wildlife");
    private static readonly GameplayEntityKindId ItemDefinitionKind =
        new("item-definition");
    private static readonly GameplayOutcomeFactId KindFact =
        new("husbandry-kind");
    private static readonly GameplayOutcomeFactId PenFact = new("pen-id");
    private static readonly GameplayOutcomeFactId AnimalFact =
        new("animal-id");
    private static readonly GameplayOutcomeFactId WorkerFact =
        new("worker-id");
    private static readonly GameplayOutcomeFactId ContributorsFact =
        new("contributors-and-work");
    private static readonly GameplayOutcomeFactId OutputFact =
        new("physical-output");

    public static MigratedDomainFactCapture Capture(
        AnimalHusbandryWorkFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));
        AnimalHusbandryPendingOutcomeState value =
            fact.CaptureDomainState();
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.AnimalHusbandryWorkResult,
            value.OperationId,
            value.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            CreateWorkDetails(value, fact.IsProposal),
            reservationOnly: fact.IsProposal);
    }

    private static MigratedProducerOutcomeDetails CreateWorkDetails(
        AnimalHusbandryPendingOutcomeState pending,
        bool proposal)
    {
        HusbandryAnimalState before = pending.AnimalStatesBefore[0];
        var subjects = new List<MigratedProducerOutcomeSubject>
        {
            new(
                WildlifeKind,
                pending.AnimalId.Value,
                pending.SpeciesId.Value,
                MigratedProducerOutcomeIds.TargetRole),
            new(
                MigratedProducerOutcomeIds.FacilityKind,
                pending.PenId.Value,
                pending.PenId.Value,
                MigratedProducerOutcomeIds.FacilityRole),
            new(
                MigratedProducerOutcomeIds.CharacterKind,
                pending.WorkerId,
                pending.WorkerDisplayName,
                MigratedProducerOutcomeIds.ActorRole)
        };
        if (pending.OutputItemId.IsValid)
        {
            subjects.Add(new MigratedProducerOutcomeSubject(
                ItemDefinitionKind,
                pending.OutputItemId.Value,
                pending.OutputItemId.Value,
                MigratedProducerOutcomeIds.ItemRole));
        }

        string stacks = proposal && pending.OutputItemId.IsValid
            ? "pending"
            : string.Join(",", pending.OutputStacks
                .OrderBy(output => output.StackId, StringComparer.Ordinal)
                .Select(output => output.StackId + ":" + output.Quantity));
        string output = pending.OutputItemId.IsValid
            ? "item=" + pending.OutputItemId.Value
                + ";quantity=" + pending.OutputQuantity
                + ";massGrams=" + pending.OutputMassGrams
                + ";stacks=" + stacks
                + ";carcass=" + (pending.CarcassStackId.Length == 0
                    ? "none"
                    : pending.CarcassStackId)
            : "none";
        string stateBefore = AnimalState(before);
        string stateAfter = proposal
            ? PreviewTerminalState(before, pending.Kind)
            : pending.Kind == AnimalHusbandryOutcomeOperationKind.Slaughter
                ? "alive=false;registered=false;carcass="
                    + pending.CarcassStackId
                : AnimalState(pending.AnimalStatesAfter[0]);
        string contributors = string.Join(",", pending.Contributions
            .OrderBy(value => value.characterId, StringComparer.Ordinal)
            .Select(value => value.characterId + ":"
                + ToMilliWork(value.contributedWork)));

        GameplayEntityId workerSubject = new(
            MigratedProducerOutcomeIds.CharacterKind,
            pending.WorkerId);
        GameplayEntityId outputSubject = pending.OutputItemId.IsValid
            ? new GameplayEntityId(
                ItemDefinitionKind,
                pending.OutputItemId.Value)
            : new GameplayEntityId(WildlifeKind, pending.AnimalId.Value);
        string kind = AnimalHusbandryDomainOperationIdentity.StableKind(
            pending.Kind);
        return new MigratedProducerOutcomeDetails(
            subjects.ToArray(),
            new[]
            {
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.CompletedWorkMetric,
                    pending.CompletedWorkMilliUnits,
                    MigratedProducerOutcomeIds.MilliWorkUnit,
                    workerSubject),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.OutputQuantityMetric,
                    pending.OutputQuantity,
                    MigratedProducerOutcomeIds.CountUnit,
                    outputSubject),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.OutputMassMetric,
                    pending.OutputMassGrams,
                    MigratedProducerOutcomeIds.GramUnit,
                    outputSubject)
            },
            new[]
            {
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.OperationFact,
                    pending.OperationId),
                new GameplayOutcomeFact(KindFact, kind),
                new GameplayOutcomeFact(PenFact, pending.PenId.Value),
                new GameplayOutcomeFact(AnimalFact, pending.AnimalId.Value),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.SpeciesFact,
                    pending.SpeciesId.Value),
                new GameplayOutcomeFact(WorkerFact, pending.WorkerId),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.BeforeFact,
                    stateBefore),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.AfterFact,
                    stateAfter),
                new GameplayOutcomeFact(ContributorsFact, contributors),
                new GameplayOutcomeFact(OutputFact, output)
            },
            kind + " 축산 작업이 실제 상태·산출 변경과 함께 완료되었다.");
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

    private static string PreviewTerminalState(
        HusbandryAnimalState state,
        AnimalHusbandryOutcomeOperationKind kind) => kind switch
    {
        AnimalHusbandryOutcomeOperationKind.Tame =>
            "tamed=1;taming=1000;status="
            + (int)AnimalHusbandryStatusCode.TamingCompleted,
        AnimalHusbandryOutcomeOperationKind.CollectProduct =>
            PreviewProductTerminalState(state),
        AnimalHusbandryOutcomeOperationKind.CollectManure =>
            "manure=" + Math.Max(0, state.ReadyManureCycles - 1)
            + ";status=" + (int)AnimalHusbandryStatusCode.ManureCollected,
        AnimalHusbandryOutcomeOperationKind.Slaughter =>
            "alive=false;registered=false;carcass=pending",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static string PreviewProductTerminalState(
        HusbandryAnimalState state)
    {
        AnimalProductProgressState product = (state.Products
                ?? new List<AnimalProductProgressState>())
            .FirstOrDefault(value => value != null && value.ReadyCycles > 0);
        return "product=" + (product?.ItemId.Value ?? "none")
            + ";ready=" + Math.Max(0, (product?.ReadyCycles ?? 0) - 1)
            + ";status=" + (int)AnimalHusbandryStatusCode.ProductCollected;
    }

    private static int ToMilliWork(float work)
    {
        double value = Math.Round(Math.Max(0f, work) * 1000d);
        if (value <= 0d || value > int.MaxValue)
        {
            throw new InvalidOperationException(
                "Husbandry completed work is out of range.");
        }
        return (int)value;
    }

    private static string Bool(bool value) => value ? "1" : "0";
}
