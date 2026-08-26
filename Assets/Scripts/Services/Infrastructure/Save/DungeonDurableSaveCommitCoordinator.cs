using System;
using System.Collections.Generic;
using System.Linq;

public sealed class DungeonDurableSaveCommitCoordinator :
    IDungeonDurableSaveCommitCoordinator
{
    public const string PipelineParticipantId =
        "000.durable-save-commit-pipeline";

    private readonly IReadOnlyList<IDungeonDurableSaveCommitParticipant>
        participants;

    public DungeonDurableSaveCommitCoordinator(
        IEnumerable<IDungeonDurableSaveCommitParticipant> participants)
    {
        IDungeonDurableSaveCommitParticipant[] source = (participants
                ?? throw new ArgumentNullException(nameof(participants)))
            .ToArray();
        if (source.Any(value => value == null
                || string.IsNullOrEmpty(value.ParticipantId)
                || !string.Equals(
                    value.ParticipantId,
                    value.ParticipantId.Trim(),
                    StringComparison.Ordinal)
                || value.Order <= 0))
        {
            throw new InvalidOperationException(
                "Durable-save commit pipeline contains an invalid participant.");
        }
        if (source.Select(value => value.ParticipantId)
                .Distinct(StringComparer.Ordinal).Count() != source.Length
            || source.Select(value => value.Order).Distinct().Count()
                != source.Length)
        {
            throw new InvalidOperationException(
                "Durable-save commit participant ID or order is duplicated.");
        }
        this.participants = Array.AsReadOnly(source
            .OrderBy(value => value.Order)
            .ThenBy(value => value.ParticipantId, StringComparer.Ordinal)
            .ToArray());
    }

    public DungeonDurableSaveCommitResult OnDurableSaveCommitted(
        string slotId,
        string serializedByteDigest)
    {
        DungeonDurableSaveCommitContext context;
        try
        {
            context = new DungeonDurableSaveCommitContext(
                slotId,
                serializedByteDigest);
        }
        catch (Exception exception)
        {
            return Corruption(PipelineParticipantId, exception.Message);
        }

        foreach (IDungeonDurableSaveCommitParticipant participant in
                 participants)
        {
            DungeonDurableSaveCommitResult result;
            try
            {
                result = participant.OnDurableSaveCommitted(context);
            }
            catch (Exception exception)
            {
                return Corruption(participant.ParticipantId, exception.Message);
            }
            if (!string.Equals(
                    result.ParticipantId,
                    participant.ParticipantId,
                    StringComparison.Ordinal)
                || !Enum.IsDefined(
                    typeof(DungeonDurableSaveCommitStatus),
                    result.Status))
            {
                return Corruption(
                    participant.ParticipantId,
                    "Durable-save participant returned a conflicting result identity or status.");
            }

            switch (result.Status)
            {
                case DungeonDurableSaveCommitStatus.Applied:
                case DungeonDurableSaveCommitStatus.AlreadyApplied:
                    continue;
                case DungeonDurableSaveCommitStatus.Deferred:
                case DungeonDurableSaveCommitStatus.Corruption:
                    return result;
                default:
                    return Corruption(
                        participant.ParticipantId,
                        "Durable-save participant returned an unknown status.");
            }
        }

        return new DungeonDurableSaveCommitResult(
            DungeonDurableSaveCommitStatus.Applied,
            PipelineParticipantId,
            "All durable-save commit participants completed.");
    }

    private static DungeonDurableSaveCommitResult Corruption(
        string participantId,
        string message) => new(
        DungeonDurableSaveCommitStatus.Corruption,
        participantId,
        message);
}

public sealed class PreparedOutputCheckpointGcDurableSaveParticipant :
    IDungeonDurableSaveCommitParticipant
{
    public const string Id = "100.prepared-output-checkpoint-gc";

    private readonly IPreparedOutputCheckpointGcCoordinator coordinator;

    public PreparedOutputCheckpointGcDurableSaveParticipant(
        IPreparedOutputCheckpointGcCoordinator coordinator)
    {
        this.coordinator = coordinator
            ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public string ParticipantId => Id;
    public int Order => 100;

    public DungeonDurableSaveCommitResult OnDurableSaveCommitted(
        DungeonDurableSaveCommitContext context)
    {
        PreparedOutputCheckpointGcResult result = coordinator
            .OnDurableSaveCommitted(
                context.SlotId,
                context.SerializedByteDigest);
        DungeonDurableSaveCommitStatus status = result.Status switch
        {
            PreparedOutputCheckpointGcStatus.Applied =>
                DungeonDurableSaveCommitStatus.Applied,
            PreparedOutputCheckpointGcStatus.Deferred =>
                DungeonDurableSaveCommitStatus.Deferred,
            PreparedOutputCheckpointGcStatus.AlreadyApplied =>
                DungeonDurableSaveCommitStatus.AlreadyApplied,
            PreparedOutputCheckpointGcStatus.Corruption =>
                DungeonDurableSaveCommitStatus.Corruption,
            _ => DungeonDurableSaveCommitStatus.Corruption
        };
        return new DungeonDurableSaveCommitResult(
            status,
            Id,
            result.Reason + ": " + result.Message);
    }
}
