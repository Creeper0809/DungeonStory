using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct V27EmbeddedWorkValueProjection
{
    public V27EmbeddedWorkValueProjection(
        string itemId,
        long acquisitionMilliEwu,
        long recoverableMilliEwu,
        string basisId,
        string selectedSourceId)
    {
        ItemId = itemId;
        AcquisitionMilliEwu = acquisitionMilliEwu;
        RecoverableMilliEwu = recoverableMilliEwu;
        BasisId = basisId;
        SelectedSourceId = selectedSourceId;
    }

    public string ItemId { get; }
    public long AcquisitionMilliEwu { get; }
    public long RecoverableMilliEwu { get; }
    public string BasisId { get; }
    public string SelectedSourceId { get; }
}

public interface IV27EmbeddedWorkValueProjectionQuery
{
    bool AuthorityAvailable { get; }
    string AuthorityFailure { get; }
    bool TryGet(string itemId, out V27EmbeddedWorkValueProjection value);
}

[Serializable]
public sealed class V27EmbeddedWorkValueProjectionPayload
{
    public string schema = string.Empty;
    public string generatorVersion = string.Empty;
    public string ledgerCsvSha256 = string.Empty;
    public string sourceDigest = string.Empty;
    public string basisId = string.Empty;
    public int itemCount;
    public List<V27EmbeddedWorkValueProjectionRow> items = new();
}

[Serializable]
public sealed class V27EmbeddedWorkValueProjectionRow
{
    public string itemId = string.Empty;
    public long acquisitionMilliEwu;
    public long recoverableMilliEwu;
    public string selectedSourceId = string.Empty;
}

public static class V27EmbeddedWorkValueProjectionCodec
{
    public const string Schema = "dungeonstory.v27-ewu-projection";
    public const string ApprovedGeneratorVersion = "v27.13.3";

    public static IReadOnlyDictionary<string, V27EmbeddedWorkValueProjection> Parse(
        string json,
        out string basisId)
    {
        V27EmbeddedWorkValueProjectionPayload payload;
        try
        {
            payload = JsonUtility.FromJson<V27EmbeddedWorkValueProjectionPayload>(
                json ?? string.Empty);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "The V27 EWU projection is not valid JSON.", exception);
        }

        if (payload == null
            || !string.Equals(payload.schema, Schema, StringComparison.Ordinal)
            || !string.Equals(
                payload.generatorVersion,
                ApprovedGeneratorVersion,
                StringComparison.Ordinal)
            || !IsSha256(payload.ledgerCsvSha256)
            || !IsSha256(payload.sourceDigest)
            || !IsCanonicalId(payload.basisId)
            || payload.items == null
            || payload.itemCount <= 0
            || payload.itemCount != payload.items.Count)
        {
            throw new InvalidOperationException(
                "The V27 EWU projection header or approval freshness proof is invalid.");
        }

        Dictionary<string, V27EmbeddedWorkValueProjection> values =
            new(StringComparer.Ordinal);
        string previousItemId = string.Empty;
        foreach (V27EmbeddedWorkValueProjectionRow row in payload.items)
        {
            if (row == null
                || !IsCanonicalId(row.itemId)
                || previousItemId.Length > 0
                    && string.CompareOrdinal(previousItemId, row.itemId) >= 0
                || !IsCanonicalId(row.selectedSourceId)
                || row.acquisitionMilliEwu < 0L
                || row.recoverableMilliEwu < 0L
                || row.recoverableMilliEwu > row.acquisitionMilliEwu
                || !values.TryAdd(
                    row.itemId,
                    new V27EmbeddedWorkValueProjection(
                        row.itemId,
                        row.acquisitionMilliEwu,
                        row.recoverableMilliEwu,
                        payload.basisId,
                        row.selectedSourceId)))
            {
                throw new InvalidOperationException(
                    $"The V27 EWU projection contains an invalid or duplicate item row '{row?.itemId ?? "null"}'.");
            }
            previousItemId = row.itemId;
        }

        basisId = payload.basisId;
        return values;
    }

    private static bool IsCanonicalId(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && value.Contains(':');

    private static bool IsSha256(string value) =>
        value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');
}

public sealed class V27EmbeddedWorkValueProjectionResourceSource :
    IV27EmbeddedWorkValueProjectionQuery
{
    public const string ResourcePath = "Balance/v27-embedded-work-values";

    private readonly IReadOnlyDictionary<string, V27EmbeddedWorkValueProjection> values;

    public V27EmbeddedWorkValueProjectionResourceSource()
    {
        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
        {
            AuthorityFailure =
                $"Missing approved V27 EWU projection at Resources/{ResourcePath}.json.";
            values = new Dictionary<string, V27EmbeddedWorkValueProjection>(
                StringComparer.Ordinal);
            return;
        }

        try
        {
            values = V27EmbeddedWorkValueProjectionCodec.Parse(
                asset.text,
                out _);
            AuthorityAvailable = true;
            AuthorityFailure = string.Empty;
        }
        catch (Exception exception)
        {
            AuthorityFailure = exception.Message;
            values = new Dictionary<string, V27EmbeddedWorkValueProjection>(
                StringComparer.Ordinal);
        }
    }

    public bool AuthorityAvailable { get; }
    public string AuthorityFailure { get; }

    public bool TryGet(
        string itemId,
        out V27EmbeddedWorkValueProjection value)
    {
        value = default;
        return AuthorityAvailable
            && values.TryGetValue(itemId?.Trim() ?? string.Empty, out value);
    }
}
