#if UNITY_EDITOR
using System;
using DungeonStory.Foundation.Facts;

public static class GameplayDomainFactPairProofDebugScenarios
{
    public static void Run()
    {
        string firstKey = new GameplayResultKey(
            "qa.pair.first",
            new GameplayOperationId("qa:pair:first"),
            41L,
            0).ToString();
        string secondKey = new GameplayResultKey(
            "qa.pair.second",
            new GameplayOperationId("qa:pair:second"),
            42L,
            0).ToString();
        string key = GameplayDomainFactPairProof.Key(firstKey, secondKey);
        Require(GameplayDomainFactPairProof.TryReadKeys(key, out string readFirst, out string readSecond)
            && readFirst == firstKey && readSecond == secondKey,
            "Pair proof did not preserve its exact member keys.");

        var first = new DomainFactDelivery(
            DomainFactDeliveryPhase.Delivered,
            firstKey,
            "publication:first",
            new string('a', 64),
            string.Empty);
        var second = new DomainFactDelivery(
            DomainFactDeliveryPhase.Delivered,
            secondKey,
            "publication:second",
            new string('b', 64),
            string.Empty);
        DomainFactDelivery delivered = GameplayDomainFactPairProof.Combine(key, first, second);
        Require(delivered.Phase == DomainFactDeliveryPhase.Delivered
            && delivered.OperationKey == key
            && delivered.Detail.Length == 0
            && delivered.PublicationId.Length is > 0 and <= 256
            && delivered.Fingerprint.Length == 64,
            "Two delivered members did not produce one bounded delivered proof.");

        var pendingSecond = new DomainFactDelivery(
            DomainFactDeliveryPhase.AcceptedPendingDelivery,
            secondKey,
            second.PublicationId,
            second.Fingerprint,
            string.Empty);
        DomainFactDelivery pending = GameplayDomainFactPairProof.Combine(key, first, pendingSecond);
        Require(pending.Phase == DomainFactDeliveryPhase.AcceptedPendingDelivery
            && pending.OperationKey == key,
            "A pending member did not keep the pair pending.");

        DomainFactDelivery uncertain = GameplayDomainFactPairProof.Combine(
            key,
            first,
            DomainFactDelivery.Uncertain("qa-pair-unknown"));
        Require(uncertain.Phase == DomainFactDeliveryPhase.InDoubt
            && uncertain.OperationKey == key
            && uncertain.Detail == "qa-pair-unknown",
            "An unknown member did not keep the pair in doubt.");

        DomainFactDelivery rejected = GameplayDomainFactPairProof.Combine(
            key,
            first,
            DomainFactDelivery.Reject("qa-pair-rejected"));
        Require(rejected.Phase == DomainFactDeliveryPhase.Rejected,
            "A rejected member did not reject the pair.");

        var wrongMember = new DomainFactDelivery(
            DomainFactDeliveryPhase.Delivered,
            firstKey,
            second.PublicationId,
            second.Fingerprint,
            string.Empty);
        Require(GameplayDomainFactPairProof.Combine(key, first, wrongMember).Phase
                == DomainFactDeliveryPhase.Rejected,
            "A delivery for the wrong member key was accepted.");

        string malformed = "cohort@1:" + firstKey + "~" + firstKey;
        Require(!GameplayDomainFactPairProof.TryReadKeys(malformed, out _, out _)
            && GameplayDomainFactPairProof.Combine(malformed, first, second).Phase
                == DomainFactDeliveryPhase.Rejected,
            "A malformed pair cohort was accepted.");

        DomainFactDelivery repeated = GameplayDomainFactPairProof.Combine(key, first, second);
        Require(GameplayDomainFactPairProof.Key(firstKey, secondKey) == key
            && repeated.Phase == delivered.Phase
            && repeated.OperationKey == delivered.OperationKey
            && repeated.PublicationId == delivered.PublicationId
            && repeated.Fingerprint == delivered.Fingerprint
            && repeated.Detail == delivered.Detail,
            "Pair proof combination was not deterministic.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
