using System;
using DungeonStory.Foundation.Facts;

// Opaque to gameplay owners. Only observation code interprets a two-row atomic publication.
internal static class GameplayDomainFactPairProof
{
    private const string Prefix = "cohort@1:";
    internal static string Key(string first, string second)
    {
        if (!GameplayDomainFactDeliveryQuery.TryReadKey(first, out _)
            || !GameplayDomainFactDeliveryQuery.TryReadKey(second, out _) || first == second)
            throw new ArgumentException("fact-pair-identity-invalid");
        return Prefix + first + "~" + second;
    }
    internal static bool TryReadKeys(string value, out string first, out string second)
    {
        first = second = string.Empty;
        if (value?.StartsWith(Prefix, StringComparison.Ordinal) != true) return false;
        var parts = value.Substring(Prefix.Length).Split('~');
        if (parts.Length != 2 || !GameplayDomainFactDeliveryQuery.TryReadKey(parts[0], out _)
            || !GameplayDomainFactDeliveryQuery.TryReadKey(parts[1], out _) || parts[0] == parts[1]) return false;
        first = parts[0]; second = parts[1]; return true;
    }
    internal static DomainFactDelivery Combine(string key, in DomainFactDelivery first, in DomainFactDelivery second)
    {
        if (!TryReadKeys(key, out string firstKey, out string secondKey)
            || !Matches(first, firstKey) || !Matches(second, secondKey))
            return DomainFactDelivery.Reject("fact-pair-delivery-identity-mismatch");
        string detail = string.IsNullOrEmpty(first.Detail) ? second.Detail
            : string.IsNullOrEmpty(second.Detail) ? first.Detail : first.Detail + ";" + second.Detail;
        if (!first.MayBeCommitted || !second.MayBeCommitted)
            return DomainFactDelivery.Reject("fact-pair-rejected:" + detail);
        if (first.Phase == DomainFactDeliveryPhase.InDoubt || second.Phase == DomainFactDeliveryPhase.InDoubt
            || string.IsNullOrEmpty(first.PublicationId) || string.IsNullOrEmpty(second.PublicationId)
            || string.IsNullOrEmpty(first.Fingerprint) || string.IsNullOrEmpty(second.Fingerprint))
            return new DomainFactDelivery(DomainFactDeliveryPhase.InDoubt, key, null, null, detail);
        return new DomainFactDelivery(first.Completed && second.Completed
                ? DomainFactDeliveryPhase.Delivered : DomainFactDeliveryPhase.AcceptedPendingDelivery,
            key, "cohort:" + Digest(first.PublicationId + "\n" + second.PublicationId),
            Digest(key + "\n" + first.Fingerprint + "\n" + second.Fingerprint), detail);
    }
    // Item publication proofs use the same bare 64-digit form as ledger hashes.
    private static string Digest(string value) =>
        NarrativeInferenceHash.ComputeSha256Utf8(value).Substring("sha256:".Length);
    private static bool Matches(in DomainFactDelivery value, string key) => value.OperationKey == key
        || value.Phase is DomainFactDeliveryPhase.InDoubt or DomainFactDeliveryPhase.Rejected
            && string.IsNullOrEmpty(value.OperationKey);
}
