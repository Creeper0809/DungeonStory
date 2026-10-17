using System;
using System.Globalization;
using System.Linq;

/// <summary>
/// Native outcome-ledger adapter for selecting an apology cause. Gameplay
/// commands consume only the neutral <see cref="ISocialOffenseHistory"/> view.
/// </summary>
public sealed class SocialOffenseHistory : ISocialOffenseHistory
{
    private readonly IGameplayOutcomeQuery outcomes;

    public SocialOffenseHistory(IGameplayOutcomeQuery outcomes) =>
        this.outcomes = outcomes
            ?? throw new ArgumentNullException(nameof(outcomes));

    public bool TryFind(
        string offenderId,
        string recipientId,
        out SocialOffenseReference cause)
    {
        cause = default;
        if (!DistinctCharacters(offenderId, recipientId))
            return false;

        GameplayOutcomeQueryPage page = outcomes.GetForEntity(
            new GameplayEntityId(
                SocialLifeOutcomeIds.CharacterKind,
                offenderId),
            OutcomeCursor.FirstPage(100),
            new OutcomeFilter(
                SocialLifeOutcomeIds.ConflictResolved,
                default,
                GameplayOutcomeStatus.Succeeded,
                int.MinValue,
                int.MaxValue,
                includeCompacted: false));
        foreach (GameplayOutcomeQueryItem item in page.Items)
        {
            GameplayOutcomeSnapshot exact = item.Exact;
            if (!MatchesCause(
                    exact,
                    offenderId,
                    recipientId,
                    expectedKind: null,
                    out string kind))
            {
                continue;
            }

            var publication = new GameplayOutcomeId(
                new GameplayOutcomeRunId(exact.runId),
                exact.sequence);
            cause = new SocialOffenseReference(
                publication.ToString(),
                RootOperation(exact),
                kind);
            return true;
        }
        return false;
    }

    public bool TryGet(
        string publicationId,
        string offenderId,
        string recipientId,
        string relationKind,
        out SocialOffenseReference cause)
    {
        cause = default;
        if (!DistinctCharacters(offenderId, recipientId)
            || !GameplayOutcomeStableIdSyntax.IsValid(relationKind))
        {
            return false;
        }

        GameplayOutcomeId id;
        try
        {
            id = ParsePublicationId(publicationId);
        }
        catch (Exception error) when (error is ArgumentException
            or InvalidOperationException
            or OverflowException)
        {
            return false;
        }
        if (!outcomes.TryGetExact(id, out GameplayOutcomeSnapshot exact)
            || !MatchesCause(
                exact,
                offenderId,
                recipientId,
                relationKind,
                out string kind))
        {
            return false;
        }

        cause = new SocialOffenseReference(
            id.ToString(),
            RootOperation(exact),
            kind);
        return true;
    }

    public static GameplayOutcomeId ParsePublicationId(string value)
    {
        int separator = value?.LastIndexOf('/') ?? -1;
        if (separator <= 0
            || separator >= value.Length - 1
            || !long.TryParse(
                value.Substring(separator + 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long sequence))
        {
            throw new ArgumentException(
                "A canonical social offense publication ID is required.",
                nameof(value));
        }

        var result = new GameplayOutcomeId(
            new GameplayOutcomeRunId(value.Substring(0, separator)),
            sequence);
        if (!result.IsValid
            || !string.Equals(result.ToString(), value, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical social offense publication ID is required.",
                nameof(value));
        }
        return result;
    }

    private static bool MatchesCause(
        GameplayOutcomeSnapshot exact,
        string offenderId,
        string recipientId,
        string expectedKind,
        out string kind)
    {
        kind = string.Empty;
        return exact != null
            && string.Equals(
                exact.outcomeTypeId,
                SocialLifeOutcomeIds.ConflictResolved.Value,
                StringComparison.Ordinal)
            && HasParticipant(
                exact,
                offenderId,
                SocialLifeOutcomeIds.InstigatorRole.Value)
            && HasParticipant(
                exact,
                recipientId,
                SocialLifeOutcomeIds.TargetRole.Value)
            && TryGetConflictKind(exact, out kind)
            && (expectedKind == null
                || string.Equals(kind, expectedKind, StringComparison.Ordinal));
    }

    private static bool HasParticipant(
        GameplayOutcomeSnapshot outcome,
        string characterId,
        string roleId) => outcome.participants.Any(value => value != null
            && string.Equals(
                value.entityKindId,
                SocialLifeOutcomeIds.CharacterKind.Value,
                StringComparison.Ordinal)
            && string.Equals(
                value.entityId,
                characterId,
                StringComparison.Ordinal)
            && string.Equals(value.roleId, roleId, StringComparison.Ordinal));

    private static bool TryGetConflictKind(
        GameplayOutcomeSnapshot outcome,
        out string relationKind)
    {
        GameplayOutcomeMetricSnapshot metric = outcome.metrics.FirstOrDefault(
            value => value != null
                && string.Equals(
                    value.metricId,
                    SocialLifeOutcomeIds.ConflictKindMetric.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.referenceKindId,
                    SocialLifeOutcomeIds.ConflictKind.Value,
                    StringComparison.Ordinal));
        relationKind = metric?.referenceId ?? string.Empty;
        return GameplayOutcomeStableIdSyntax.IsValid(relationKind);
    }

    private static string RootOperation(GameplayOutcomeSnapshot exact) =>
        new GameplayOperationId(
            string.IsNullOrWhiteSpace(exact.rootOperationId)
                ? exact.operationId
                : exact.rootOperationId).Value;

    private static bool DistinctCharacters(string left, string right) =>
        GameplayOutcomeStableIdSyntax.IsValid(left)
        && GameplayOutcomeStableIdSyntax.IsValid(right)
        && !string.Equals(left, right, StringComparison.Ordinal);
}
