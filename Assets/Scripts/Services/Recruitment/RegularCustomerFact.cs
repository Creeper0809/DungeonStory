using System;

public readonly struct RegularCustomerFact
{
    private readonly RegularCustomerFactData data;
    public RegularCustomerFact(RegularCustomerFactData source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        source.Validate();
        data = source.Clone();
    }
    public RegularCustomerFactData Snapshot() => data?.Clone()
        ?? throw new InvalidOperationException("regular-customer-fact-empty");

    internal static RegularCustomerFactData Capture(RegularCustomerFactStage stage,
        RegularCustomerRecord record, int absoluteDay, bool becameRegular = false, bool becameCandidate = false) => new()
    {
        stage = stage, customerId = record.CustomerId, displayName = record.DisplayName,
        speciesTag = record.SpeciesTag, absoluteDay = absoluteDay, visitCount = record.VisitCount,
        averageSatisfaction = record.AverageSatisfaction, status = record.Status,
        capabilities = record.RecruitCapabilities, recruitedAbsoluteDay = record.RecruitedAbsoluteDay,
        deliveryKind = record.RecruitDeliveryKind, mercenaryRolePremium = record.PendingMercenaryRolePremium,
        actorId = stage == RegularCustomerFactStage.RecruitmentDelivered ? record.CustomerId : "",
        becameRegular = becameRegular, becameRecruitCandidate = becameCandidate
    };
}
