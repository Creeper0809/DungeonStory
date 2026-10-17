using System;

public static class RegularCustomerFactMapping
{
    public const string ObserverId = "log:recruitment.regular-customer";

    public static MigratedDomainFactCapture Capture(RegularCustomerFact fact)
    {
        RegularCustomerFactData data = fact.Snapshot();
        data.Validate();

        MigratedProducerOutcomeKind kind;
        string ownerIdentity;
        string summary;
        GameplayRoleId role;
        string subjectId;
        switch (data.stage)
        {
            case RegularCustomerFactStage.VisitRecorded:
                kind = MigratedProducerOutcomeKind.RegularCustomerVisitResult;
                ownerIdentity = "regular-customer-visit:customer="
                    + data.customerId
                    + ":visit=" + data.visitCount;
                summary = data.displayName
                    + "의 방문 " + data.visitCount
                    + "회차가 기록되었습니다. 상태=" + data.status + ".";
                role = MigratedProducerOutcomeIds.CustomerRole;
                subjectId = data.customerId;
                break;
            case RegularCustomerFactStage.RecruitmentCommitted:
                kind = MigratedProducerOutcomeKind.RegularCustomerRecruitResult;
                ownerIdentity = "regular-customer-recruit:customer="
                    + data.customerId
                    + ":day=" + Math.Max(1, data.recruitedAbsoluteDay)
                    + ":kind=" + data.deliveryKind;
                summary = data.displayName
                    + " 영입이 확정되었습니다. 배치="
                    + data.deliveryKind + ".";
                role = MigratedProducerOutcomeIds.CustomerRole;
                subjectId = data.customerId;
                break;
            case RegularCustomerFactStage.RecruitmentDelivered:
                kind = MigratedProducerOutcomeKind
                    .RegularCustomerRecruitDeliveryResult;
                ownerIdentity = "regular-customer-recruit-delivery:customer="
                    + data.customerId
                    + ":day=" + data.recruitedAbsoluteDay
                    + ":kind=" + data.deliveryKind;
                summary = "customer=" + data.customerId
                    + ";actor=" + data.actorId
                    + ";delivery=" + data.deliveryKind
                    + ";recruitedDay=" + data.recruitedAbsoluteDay;
                role = MigratedProducerOutcomeIds.ActorRole;
                subjectId = data.actorId;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(data.stage),
                    data.stage,
                    "Unknown regular customer fact stage.");
        }

        MigratedProducerOutcomeDetails details = new(
            new[]
            {
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.CharacterKind,
                    subjectId,
                    data.displayName,
                    role)
            },
            Array.Empty<GameplayOutcomeMetric>(),
            Array.Empty<GameplayOutcomeFact>(),
            summary);

        return new MigratedDomainFactCapture(
            kind,
            ownerIdentity,
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }
}
