using System;
using System.Collections.Generic;
using System.Linq;

public static class ProductionOutputCommitComponentCodec
{
    private const string CommitKey = "commit-id";

    public static ItemInstanceComponentSaveData Create(string commitId)
    {
        string canonical = commitId ?? string.Empty;
        if (canonical.Length == 0
            || !string.Equals(canonical, canonical.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Production output commit ID must be canonical.",
                nameof(commitId));
        }
        return new ItemInstanceComponentSaveData
        {
            componentTypeId = ItemInstanceComponentIds.ProductionOutputCommit,
            schemaVersion = 1,
            affectsStacking = true,
            values = new List<ItemStateValueSaveData>
            {
                new()
                {
                    key = CommitKey,
                    kind = ItemStateValueKind.String,
                    stringValue = canonical
                }
            }
        };
    }

    public static bool Matches(
        IEnumerable<ItemInstanceComponentSaveData> components,
        string commitId)
    {
        ItemInstanceComponentSaveData component = (components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.componentTypeId,
                    ItemInstanceComponentIds.ProductionOutputCommit,
                    StringComparison.Ordinal));
        ItemStateValueSaveData field = component?.values?.SingleOrDefault(value =>
            value != null
            && string.Equals(value.key, CommitKey, StringComparison.Ordinal)
            && value.kind == ItemStateValueKind.String);
        return field != null
            && string.Equals(
                field.stringValue,
                commitId,
                StringComparison.Ordinal);
    }
}
