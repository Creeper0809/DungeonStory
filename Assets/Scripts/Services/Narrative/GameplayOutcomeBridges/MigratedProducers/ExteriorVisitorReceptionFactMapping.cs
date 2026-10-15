using System;

public static class ExteriorVisitorReceptionFactMapping
{
    public static MigratedDomainFactCapture Capture(ExteriorVisitorReceptionFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        var subject = new MigratedProducerOutcomeSubject(MigratedProducerOutcomeIds.CharacterKind,
            fact.VisitorId, string.IsNullOrWhiteSpace(fact.VisitorName) ? fact.VisitorId : fact.VisitorName,
            MigratedProducerOutcomeIds.ActorRole);
        var details = new MigratedProducerOutcomeDetails(new[] { subject },
            Array.Empty<GameplayOutcomeMetric>(), Array.Empty<GameplayOutcomeFact>(),
            "외부 방문객 응대 적용: incident=" + fact.IncidentId + "; visitor=" + fact.VisitorId
            + "; factor=" + fact.MoodFactorId);
        return new MigratedDomainFactCapture(MigratedProducerOutcomeKind.ExteriorVisitorReceptionAppliedEvent,
            fact.IncidentId, fact.AbsoluteDay, GameplayOutcomeStatus.Succeeded, details);
    }
}
