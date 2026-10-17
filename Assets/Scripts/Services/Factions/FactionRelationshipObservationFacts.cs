using System;
using System.Linq;
using DungeonStory.Factions;
using DungeonStory.Foundation.Facts;

public static class FactionRelationshipObservationFacts
{
    public static FactionRelationshipObservationData Freeze(int sequence, string key, FactionTrustChangedDomainFact fact) => Seal(new()
    {
        kind = FactionRelationshipObservationKind.Trust, sequence = sequence, publicationKey = key,
        absoluteDay = fact.AbsoluteDay, ownerIdentity = fact.OwnerIdentity, factionId = fact.FactionId,
        factionDisplayName = fact.FactionDisplayName, previousTrust = fact.Previous, currentTrust = fact.Current,
        reason = fact.Reason
    });
    public static FactionRelationshipObservationData Freeze(int sequence, string key, FactionBetrayalDomainFact fact) => Seal(new()
    {
        kind = FactionRelationshipObservationKind.Betrayal, sequence = sequence, publicationKey = key,
        absoluteDay = fact.AbsoluteDay, ownerIdentity = fact.OperationId, factionId = fact.BetrayedFactionId,
        actualLootValue = fact.ActualLootValue,
        transitions = fact.Transitions.Select(value => new FactionRelationshipTrustData
        {
            factionId = value.FactionId, factionDisplayName = value.FactionDisplayName,
            previousTrust = value.Previous, currentTrust = value.Current
        }).ToList()
    });
    public static FactionTrustChangedDomainFact ToTrust(FactionRelationshipObservationData data)
    {
        Require(data, FactionRelationshipObservationKind.Trust);
        return new(data.ownerIdentity, data.absoluteDay, data.factionId, data.factionDisplayName,
            data.previousTrust, data.currentTrust, data.reason);
    }
    public static FactionBetrayalDomainFact ToBetrayal(FactionRelationshipObservationData data)
    {
        Require(data, FactionRelationshipObservationKind.Betrayal);
        return new(data.ownerIdentity, data.absoluteDay, data.factionId, data.actualLootValue,
            data.transitions.Select(value => new FactionTrustTransitionFactSnapshot(
                value.factionId, value.factionDisplayName, value.previousTrust, value.currentTrust)));
    }
    public static DomainFactReplay CreateReplay(FactionRelationshipObservationData data) => data?.kind switch
    {
        FactionRelationshipObservationKind.Trust => DomainFactReplay.Create(data.publicationKey, ToTrust(data)),
        FactionRelationshipObservationKind.Betrayal => DomainFactReplay.Create(data.publicationKey, ToBetrayal(data)),
        _ => throw new InvalidOperationException("faction-relationship-replay-kind-invalid")
    };
    private static FactionRelationshipObservationData Seal(FactionRelationshipObservationData value)
    {
        value.frozenFingerprint = value.ComputeFingerprint();
        return value;
    }
    private static void Require(FactionRelationshipObservationData data, FactionRelationshipObservationKind kind)
    {
        if (data == null || data.kind != kind)
            throw new InvalidOperationException("faction-relationship-fact-kind-invalid");
    }
}
