using System;

public static class SurgeryWorkCompletionFactMapping
{
    public const string ObserverId =
        "identity:medical.surgery-work-completed";

    public static WorkCompletionIdentityDeliveryRequest Capture(
        SurgeryWorkCompletedFact fact) =>
        WorkCompletionIdentityDeliveryLedger.CreateReferencedRequest(
            CreateCompletionEvent(fact));

    public static WorkCompletedIdentityEvent CreateCompletionEvent(
        SurgeryWorkCompletedFact fact)
    {
        Validate(fact);
        GameplayResultKey child = SurgeryOutcomeIds.ResultKey(fact.OrderId);
        return new WorkCompletedIdentityEvent(
            new CharacterId(fact.DoctorId),
            "surgery:" + fact.ProcedureId,
            fact.Succeeded
                ? fact.ResultId
                : "outcome:" + fact.ResultId,
            fact.Origin,
            fact.AbsoluteDay,
            child.OperationId.Value,
            child.ProducerId,
            child.ToString(),
            fact.FacilityId,
            fact.CompletedWork);
    }

    public static WorkCompletionDomainFactBinding<SurgeryWorkCompletedFact>
        CreateBinding(
            IWorkCompletionIdentityDeliveryCommand completion,
            WorkCompletionIdentityDeliveryLedger deliveries,
            CharacterIdentityEventPublisher events,
            WorkIdentityAppliedFactMapping childValidation) => new(
                ObserverId,
                Capture,
                completion,
                deliveries,
                events,
                WorkCompletionNotificationMode.ReferencedEvent,
                childValidation);

    private static void Validate(SurgeryWorkCompletedFact fact)
    {
        bool validResult = fact.Succeeded
            ? fact.ResultId is "success" or "miracle"
            : fact.ResultId is "failure" or "severe-complication";
        if (!GameplayOutcomeStableIdSyntax.IsValid(fact.OrderId)
            || !GameplayOutcomeStableIdSyntax.IsValid(fact.ProcedureId)
            || !GameplayOutcomeStableIdSyntax.IsValid(fact.DoctorId)
            || !GameplayOutcomeStableIdSyntax.IsValid(fact.FacilityId)
            || !validResult
            || !Enum.IsDefined(typeof(CharacterCommandOrigin), fact.Origin)
            || fact.AbsoluteDay <= 0
            || !float.IsFinite(fact.CompletedWork)
            || fact.CompletedWork <= 0f)
        {
            throw new ArgumentException(
                "surgery-work-completed-fact-invalid",
                nameof(fact));
        }
    }
}
