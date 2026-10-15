using System;
using System.Globalization;
using DungeonStory.Foundation.Facts;

/// <summary>Only the observation layer interprets durable publication identities.</summary>
public sealed class GameplayDomainFactDeliveryQuery : IDomainFactDeliveryQuery
{
    private readonly GameplayDomainFactCommitGroup commits;
    public GameplayDomainFactDeliveryQuery(IGameplayOutcomeRecorder recorder, IGameplayOutcomeDiagnosticsQuery diagnostics)
        => commits = new GameplayDomainFactCommitGroup(recorder, diagnostics);

    public DomainFactDelivery Reconcile(string publicationKey)
    {
        try
        {
            if (GameplayDomainFactPairProof.TryReadKeys(publicationKey, out string first, out string second))
                return GameplayDomainFactPairProof.Combine(publicationKey, Reconcile(first), Reconcile(second));
            if (!TryReadKey(publicationKey, out var key))
                return DomainFactDelivery.Reject("fact-publication-key-invalid");
            var result = commits.Replay(key);
            // A query has no authority to reject or roll back an applied domain mutation.
            return result.MayBeCommitted ? result : new DomainFactDelivery(DomainFactDeliveryPhase.InDoubt,
                publicationKey, null, null, "fact-publication-unresolved:" + result.Detail);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            and not StackOverflowException and not AccessViolationException)
        {
            return new DomainFactDelivery(DomainFactDeliveryPhase.InDoubt, publicationKey, null, null,
                "fact-publication-query-failed:" + exception.GetType().Name);
        }
    }

    internal static bool TryReadKey(string value, out GameplayResultKey key)
    {
        key = default;
        if (string.IsNullOrEmpty(value)) return false;
        string[] parts = value.Split('|');
        if (parts.Length != 4 || !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out long revision)
            || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int index)) return false;
        try
        {
            key = new GameplayResultKey(parts[0], new GameplayOperationId(parts[1]), revision, index);
            return key.IsValid && key.ToString() == value;
        }
        catch (ArgumentException) { return false; }
    }
}
