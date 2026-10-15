using System;
using System.Globalization;
using DungeonStory.Infrastructure;

/// <summary>
/// Immutable evidence of an already applied work suspension without live
/// actor, facility, outcome, or save authority.
/// </summary>
public sealed class EmergencyWorkSuspensionFact
{
    private readonly EmergencyWorkSuspensionFactSaveData value;

    public EmergencyWorkSuspensionFact(
        EmergencyWorkSuspensionFactSaveData source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.Validate();
        value = source.Clone();
    }

    public EmergencyWorkSuspensionFact(
        SettlementSuspendedWorkSnapshot snapshot,
        int absoluteDay,
        string workDisplayName)
        : this(new EmergencyWorkSuspensionFactSaveData
        {
            characterId = snapshot.CharacterId,
            workerDisplayName = snapshot.WorkerDisplayName,
            workTypeId = snapshot.WorkTypeId.Value,
            workDisplayName = workDisplayName,
            targetBuildingId = snapshot.TargetBuildingId,
            targetDisplayName = snapshot.TargetDisplayName,
            alertEpochId = snapshot.AlertEpochId,
            suspendedAtAbsoluteHour = snapshot.SuspendedAtAbsoluteHour,
            ownerRevision = snapshot.OutcomeOwnerRevision,
            absoluteDay = absoluteDay,
            progressExternallyPersisted =
                snapshot.ProgressExternallyPersisted,
            inlineCompletedWork = snapshot.InlineCompletedWork,
            inlineRequiredWork = snapshot.InlineRequiredWork
        })
    {
    }

    public string DomainId =>
        "emergency-work-suspension:"
        + value.characterId + ":"
        + value.alertEpochId.ToString(CultureInfo.InvariantCulture) + ":"
        + value.ownerRevision.ToString(CultureInfo.InvariantCulture);

    public EmergencyWorkSuspensionFactSaveData Snapshot() => value.Clone();
}
