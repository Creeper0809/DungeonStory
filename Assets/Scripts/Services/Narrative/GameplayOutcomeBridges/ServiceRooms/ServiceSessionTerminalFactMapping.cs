using System;

public static class ServiceSessionTerminalFactMapping
{
    public static OutcomeWriteRequirements Reserve(ServiceSessionTerminalFact fact, long worldEpoch)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        return new OutcomeWriteRequirements(ServiceSessionOutcomeKey.Create(fact.Snapshot().SessionId),
            ServiceSessionOutcomeIds.Terminal, fact.AbsoluteDay,
            fact.TerminalStage == ServiceSessionStage.Completed ? GameplayOutcomeStatus.Succeeded : GameplayOutcomeStatus.Cancelled,
            worldEpoch, ServiceSessionTerminalIdentity.OwnerRevision,
            participantCount: ServiceSessionOutcomeReceiptFactory.ParticipantCount,
            metricCount: ServiceSessionOutcomeReceiptFactory.MetricCount,
            subjectCount: ServiceSessionOutcomeReceiptFactory.SubjectCount,
            tagCount: ServiceSessionOutcomeReceiptFactory.TagCount, anchorCount: 0,
            provenanceCount: ServiceSessionOutcomeReceiptFactory.ProvenanceCount,
            factCount: ServiceSessionOutcomeReceiptFactory.FactCount);
    }

    public static ServiceSessionOutcomeReceipt Capture(ServiceSessionTerminalFact fact)
    {
        if (fact == null || fact.IsProposal)
            throw new InvalidOperationException("A service proposal has no completed result.");
        return ServiceSessionOutcomeReceiptFactory.Create(fact.Snapshot(), fact.HubName);
    }
}
