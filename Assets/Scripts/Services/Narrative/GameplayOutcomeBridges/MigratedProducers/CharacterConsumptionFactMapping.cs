using System;
using System.Globalization;

public static class CharacterConsumptionFactMapping
{
    public static MigratedDomainFactCapture Capture(
        CharacterMealConsumedFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        CharacterConsumablesOutcomeEvidence evidence = fact.Evidence;
        bool effectsSkipped = !string.IsNullOrEmpty(
            evidence.EffectsSkippedReason);
        string summary = effectsSkipped
            ? "meal-physical-consumed; effects-not-applied; reason="
                + evidence.EffectsSkippedReason
            : "meal=" + evidence.ItemDefinitionId.Value
                + ";policyViolation=" + evidence.PolicyViolation
                + ";contaminated=" + evidence.Contaminated;
        if (!effectsSkipped && !fact.RecipientActiveAtCompletion)
            summary += "; liveObservers=recipient-retired";

        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.CharacterConsumablesMealResult,
            Identity("meal", fact.OperationId),
            fact.AbsoluteDay,
            effectsSkipped
                ? GameplayOutcomeStatus.PartiallySucceeded
                : GameplayOutcomeStatus.Succeeded,
            CharacterSurvivalOutcomeDetails.Meal(
                fact.CharacterId,
                fact.CharacterId.Value,
                evidence,
                summary));
    }

    public static MigratedDomainFactCapture Capture(
        CharacterSubstanceConsumedFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        string summary = "substance=" + fact.ItemId.Value
            + ";tolerance=" + fact.Tolerance.ToString(
                "0.###",
                CultureInfo.InvariantCulture)
            + ";addiction=" + fact.Addiction.ToString(
                "0.###",
                CultureInfo.InvariantCulture)
            + ";overdose=" + fact.Overdosed;
        return SingleSubject(
            MigratedProducerOutcomeKind.CharacterConsumablesSubstanceResult,
            Identity("substance", fact.OperationId),
            fact.CharacterId,
            fact.AbsoluteDay,
            summary);
    }

    public static MigratedDomainFactCapture Capture(
        CharacterDetoxAppliedFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        string summary = "medicine=" + fact.ItemId.Value
            + ";reduction=" + fact.AppliedReduction.ToString(
                "0.###",
                CultureInfo.InvariantCulture);
        return SingleSubject(
            MigratedProducerOutcomeKind.CharacterDetoxTreatmentResult,
            Identity("detox", fact.OperationId),
            fact.CharacterId,
            fact.AbsoluteDay,
            summary);
    }

    public static MigratedDomainFactCapture Capture(
        CharacterPrimitiveMealCompletedFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        return SingleSubject(
            MigratedProducerOutcomeKind.CharacterPrimitiveSurvivalCompletedEvent,
            "primitive-survival:" + fact.CharacterId.Value,
            fact.CharacterId,
            fact.AbsoluteDay,
            $"action=survival:field-meal; recovery={fact.Nutrition:0.###}; physicalItemCount=1");
    }

    private static MigratedDomainFactCapture SingleSubject(
        MigratedProducerOutcomeKind kind,
        string ownerIdentity,
        CharacterId characterId,
        int absoluteDay,
        string summary)
    {
        var subject = new MigratedProducerOutcomeSubject(
            MigratedProducerOutcomeIds.CharacterKind,
            characterId.Value,
            characterId.Value,
            MigratedProducerOutcomeIds.ActorRole);
        var details = new MigratedProducerOutcomeDetails(
            new[] { subject },
            Array.Empty<GameplayOutcomeMetric>(),
            Array.Empty<GameplayOutcomeFact>(),
            summary);
        return new MigratedDomainFactCapture(
            kind,
            ownerIdentity,
            absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    private static string Identity(
        string operationKind,
        ConsumableOperationId operationId) =>
        "character-consumables-" + operationKind + "@1|" + operationId.Value;
}
