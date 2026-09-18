using System;

internal static class GameplayOutcomeStableIdSyntax
{
    public static bool IsValid(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 192)
            return false;

        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            bool valid = current >= 'a' && current <= 'z'
                || current >= 'A' && current <= 'Z'
                || current >= '0' && current <= '9'
                || current == '.'
                || current == ':'
                || current == '_'
                || current == '-'
                || current == '/';
            if (!valid)
                return false;
        }

        return true;
    }

    public static string Require(string value, string parameterName)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException(
                "A non-empty stable ID containing only ASCII letters, digits, '.', ':', '_', '-', or '/' is required.",
                parameterName);
        }

        return value;
    }
}

public readonly struct GameplayOutcomeRunId : IEquatable<GameplayOutcomeRunId>
{
    public GameplayOutcomeRunId(string value) =>
        Value = GameplayOutcomeStableIdSyntax.Require(value, nameof(value));

    public string Value { get; }
    public bool IsValid => GameplayOutcomeStableIdSyntax.IsValid(Value);
    public bool Equals(GameplayOutcomeRunId other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) =>
        obj is GameplayOutcomeRunId other && Equals(other);
    public override int GetHashCode() =>
        Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
    public override string ToString() => Value ?? string.Empty;
    public static bool operator ==(GameplayOutcomeRunId left, GameplayOutcomeRunId right) => left.Equals(right);
    public static bool operator !=(GameplayOutcomeRunId left, GameplayOutcomeRunId right) => !left.Equals(right);
}

public readonly struct GameplayOutcomeId : IEquatable<GameplayOutcomeId>, IComparable<GameplayOutcomeId>
{
    public GameplayOutcomeId(GameplayOutcomeRunId runId, long sequence)
    {
        if (!runId.IsValid)
            throw new ArgumentException("A valid run ID is required.", nameof(runId));
        if (sequence <= 0L)
            throw new ArgumentOutOfRangeException(nameof(sequence));
        RunId = runId;
        Sequence = sequence;
    }

    public GameplayOutcomeRunId RunId { get; }
    public long Sequence { get; }
    public bool IsValid => RunId.IsValid && Sequence > 0L;
    public int CompareTo(GameplayOutcomeId other)
    {
        int run = string.CompareOrdinal(RunId.Value, other.RunId.Value);
        return run != 0 ? run : Sequence.CompareTo(other.Sequence);
    }
    public bool Equals(GameplayOutcomeId other) =>
        RunId.Equals(other.RunId) && Sequence == other.Sequence;
    public override bool Equals(object obj) => obj is GameplayOutcomeId other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(RunId, Sequence);
    public override string ToString() => IsValid ? $"{RunId.Value}/{Sequence}" : string.Empty;
    public static bool operator ==(GameplayOutcomeId left, GameplayOutcomeId right) => left.Equals(right);
    public static bool operator !=(GameplayOutcomeId left, GameplayOutcomeId right) => !left.Equals(right);
}

public readonly struct GameplayOutcomeTypeId : IEquatable<GameplayOutcomeTypeId>
{
    public GameplayOutcomeTypeId(string value) =>
        Value = GameplayOutcomeStableIdSyntax.Require(value, nameof(value));
    public string Value { get; }
    public bool IsValid => GameplayOutcomeStableIdSyntax.IsValid(Value);
    public bool Equals(GameplayOutcomeTypeId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is GameplayOutcomeTypeId other && Equals(other);
    public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
    public override string ToString() => Value ?? string.Empty;
    public static bool operator ==(GameplayOutcomeTypeId left, GameplayOutcomeTypeId right) => left.Equals(right);
    public static bool operator !=(GameplayOutcomeTypeId left, GameplayOutcomeTypeId right) => !left.Equals(right);
}

public readonly struct GameplayOperationId : IEquatable<GameplayOperationId>
{
    public GameplayOperationId(string value) =>
        Value = GameplayOutcomeStableIdSyntax.Require(value, nameof(value));
    public string Value { get; }
    public bool IsValid => GameplayOutcomeStableIdSyntax.IsValid(Value);
    public bool Equals(GameplayOperationId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is GameplayOperationId other && Equals(other);
    public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
    public override string ToString() => Value ?? string.Empty;
    public static bool operator ==(GameplayOperationId left, GameplayOperationId right) => left.Equals(right);
    public static bool operator !=(GameplayOperationId left, GameplayOperationId right) => !left.Equals(right);
}

public readonly struct GameplayResultKey : IEquatable<GameplayResultKey>
{
    public GameplayResultKey(
        string producerId,
        GameplayOperationId operationId,
        long commitRevision,
        int localResultIndex)
    {
        ProducerId = GameplayOutcomeStableIdSyntax.Require(producerId, nameof(producerId));
        if (!operationId.IsValid)
            throw new ArgumentException("A valid operation ID is required.", nameof(operationId));
        if (commitRevision < 0L)
            throw new ArgumentOutOfRangeException(nameof(commitRevision));
        if (localResultIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(localResultIndex));
        OperationId = operationId;
        CommitRevision = commitRevision;
        LocalResultIndex = localResultIndex;
    }

    public string ProducerId { get; }
    public GameplayOperationId OperationId { get; }
    public long CommitRevision { get; }
    public int LocalResultIndex { get; }
    public bool IsValid => GameplayOutcomeStableIdSyntax.IsValid(ProducerId)
        && OperationId.IsValid
        && CommitRevision >= 0L
        && LocalResultIndex >= 0;
    public bool Equals(GameplayResultKey other) =>
        string.Equals(ProducerId, other.ProducerId, StringComparison.Ordinal)
        && OperationId.Equals(other.OperationId)
        && CommitRevision == other.CommitRevision
        && LocalResultIndex == other.LocalResultIndex;
    public override bool Equals(object obj) => obj is GameplayResultKey other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(
        ProducerId == null ? 0 : StringComparer.Ordinal.GetHashCode(ProducerId),
        OperationId,
        CommitRevision,
        LocalResultIndex);
    public override string ToString() => IsValid
        ? $"{ProducerId}|{OperationId.Value}|{CommitRevision}|{LocalResultIndex}"
        : string.Empty;
    public static bool operator ==(GameplayResultKey left, GameplayResultKey right) => left.Equals(right);
    public static bool operator !=(GameplayResultKey left, GameplayResultKey right) => !left.Equals(right);
}

public readonly struct GameplayEntityKindId : IEquatable<GameplayEntityKindId>
{
    public GameplayEntityKindId(string value) => Value = GameplayOutcomeStableIdSyntax.Require(value, nameof(value));
    public string Value { get; }
    public bool IsValid => GameplayOutcomeStableIdSyntax.IsValid(Value);
    public bool Equals(GameplayEntityKindId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is GameplayEntityKindId other && Equals(other);
    public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
    public override string ToString() => Value ?? string.Empty;
}

public readonly struct GameplayEntityId : IEquatable<GameplayEntityId>, IComparable<GameplayEntityId>
{
    public GameplayEntityId(GameplayEntityKindId kind, string value)
    {
        if (!kind.IsValid)
            throw new ArgumentException("A valid entity kind is required.", nameof(kind));
        Kind = kind;
        Value = GameplayOutcomeStableIdSyntax.Require(value, nameof(value));
    }
    public GameplayEntityKindId Kind { get; }
    public string Value { get; }
    public bool IsValid => Kind.IsValid && GameplayOutcomeStableIdSyntax.IsValid(Value);
    public int CompareTo(GameplayEntityId other)
    {
        int kind = string.CompareOrdinal(Kind.Value, other.Kind.Value);
        return kind != 0 ? kind : string.CompareOrdinal(Value, other.Value);
    }
    public bool Equals(GameplayEntityId other) => Kind.Equals(other.Kind) && string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is GameplayEntityId other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Kind, Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value));
    public override string ToString() => IsValid ? $"{Kind.Value}:{Value}" : string.Empty;
    public static bool operator ==(GameplayEntityId left, GameplayEntityId right) => left.Equals(right);
    public static bool operator !=(GameplayEntityId left, GameplayEntityId right) => !left.Equals(right);
}

public readonly struct GameplayRoleId : IEquatable<GameplayRoleId>
{
    public GameplayRoleId(string value) => Value = GameplayOutcomeStableIdSyntax.Require(value, nameof(value));
    public string Value { get; }
    public bool IsValid => GameplayOutcomeStableIdSyntax.IsValid(Value);
    public bool Equals(GameplayRoleId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is GameplayRoleId other && Equals(other);
    public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
    public override string ToString() => Value ?? string.Empty;
}

public readonly struct GameplayMetricId : IEquatable<GameplayMetricId>
{
    public GameplayMetricId(string value) => Value = GameplayOutcomeStableIdSyntax.Require(value, nameof(value));
    public string Value { get; }
    public bool IsValid => GameplayOutcomeStableIdSyntax.IsValid(Value);
    public bool Equals(GameplayMetricId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is GameplayMetricId other && Equals(other);
    public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
    public override string ToString() => Value ?? string.Empty;
}

public readonly struct GameplayMetricUnitId : IEquatable<GameplayMetricUnitId>
{
    public GameplayMetricUnitId(string value) => Value = GameplayOutcomeStableIdSyntax.Require(value, nameof(value));
    public string Value { get; }
    public bool IsValid => GameplayOutcomeStableIdSyntax.IsValid(Value);
    public bool Equals(GameplayMetricUnitId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is GameplayMetricUnitId other && Equals(other);
    public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
    public override string ToString() => Value ?? string.Empty;
}

public readonly struct GameplayOutcomeTagId : IEquatable<GameplayOutcomeTagId>
{
    public GameplayOutcomeTagId(string value) => Value = GameplayOutcomeStableIdSyntax.Require(value, nameof(value));
    public string Value { get; }
    public bool IsValid => GameplayOutcomeStableIdSyntax.IsValid(Value);
    public bool Equals(GameplayOutcomeTagId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is GameplayOutcomeTagId other && Equals(other);
    public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
    public override string ToString() => Value ?? string.Empty;
}

public readonly struct GameplayOutcomeFactId : IEquatable<GameplayOutcomeFactId>
{
    public GameplayOutcomeFactId(string value) => Value = GameplayOutcomeStableIdSyntax.Require(value, nameof(value));
    public string Value { get; }
    public bool IsValid => GameplayOutcomeStableIdSyntax.IsValid(Value);
    public bool Equals(GameplayOutcomeFactId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is GameplayOutcomeFactId other && Equals(other);
    public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
    public override string ToString() => Value ?? string.Empty;
}

public readonly struct GameplayMemorySignature : IEquatable<GameplayMemorySignature>
{
    public GameplayMemorySignature(string value) => Value = GameplayOutcomeStableIdSyntax.Require(value, nameof(value));
    public string Value { get; }
    public bool IsValid => GameplayOutcomeStableIdSyntax.IsValid(Value);
    public bool Equals(GameplayMemorySignature other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is GameplayMemorySignature other && Equals(other);
    public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
    public override string ToString() => Value ?? string.Empty;
}

public readonly struct NarrativeMemoryId : IEquatable<NarrativeMemoryId>
{
    public NarrativeMemoryId(string value) => Value = GameplayOutcomeStableIdSyntax.Require(value, nameof(value));
    public string Value { get; }
    public bool IsValid => GameplayOutcomeStableIdSyntax.IsValid(Value);
    public bool Equals(NarrativeMemoryId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is NarrativeMemoryId other && Equals(other);
    public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
    public override string ToString() => Value ?? string.Empty;
}
