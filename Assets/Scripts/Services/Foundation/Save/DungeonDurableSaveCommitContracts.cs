using System;

public enum DungeonDurableSaveCommitStatus
{
    Applied = 1,
    Deferred = 2,
    AlreadyApplied = 3,
    Corruption = 4
}

public readonly struct DungeonDurableSaveCommitContext
{
    public DungeonDurableSaveCommitContext(
        string slotId,
        string serializedByteDigest)
    {
        if (string.IsNullOrEmpty(slotId)
            || !string.Equals(slotId, slotId.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical durable-save slot ID is required.",
                nameof(slotId));
        }
        if (!IsDigest(serializedByteDigest))
        {
            throw new ArgumentException(
                "A lowercase SHA-256 durable-save digest is required.",
                nameof(serializedByteDigest));
        }
        SlotId = slotId;
        SerializedByteDigest = serializedByteDigest;
    }

    public string SlotId { get; }
    public string SerializedByteDigest { get; }

    private static bool IsDigest(string value)
    {
        if (value == null || value.Length != 64)
            return false;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (!((character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'f')))
            {
                return false;
            }
        }
        return true;
    }
}

public readonly struct DungeonDurableSaveCommitResult
{
    public DungeonDurableSaveCommitResult(
        DungeonDurableSaveCommitStatus status,
        string participantId,
        string message)
    {
        if (!Enum.IsDefined(typeof(DungeonDurableSaveCommitStatus), status)
            || string.IsNullOrEmpty(participantId)
            || !string.Equals(
                participantId,
                participantId.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Durable-save commit result is invalid.");
        }
        Status = status;
        ParticipantId = participantId;
        Message = message ?? string.Empty;
    }

    public DungeonDurableSaveCommitStatus Status { get; }
    public string ParticipantId { get; }
    public string Message { get; }
}

public interface IDungeonDurableSaveCommitParticipant
{
    string ParticipantId { get; }
    int Order { get; }
    DungeonDurableSaveCommitResult OnDurableSaveCommitted(
        DungeonDurableSaveCommitContext context);
}

public interface IDungeonDurableSaveCommitCoordinator
{
    DungeonDurableSaveCommitResult OnDurableSaveCommitted(
        string slotId,
        string serializedByteDigest);
}
