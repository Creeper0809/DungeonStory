using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Infrastructure;
using DungeonStory.Operation;

/// <summary>Checks saved suspension authority against its frozen domain evidence.</summary>
public static class EmergencyWorkSuspensionObservationState
{
    public static void Validate(SettlementThreatAlertSaveData source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        var errors = EventAlertPayloadValidation.Validate(new DungeonEventAlertSaveData { threatAlert = source });
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" | ", errors));
        var active = source.suspendedWork.ToDictionary(row => row.characterId, StringComparer.Ordinal);
        foreach (var row in source.suspensionObservations)
        {
            var fact = row.fact;
            if (!WorkTypeCatalog.TryGet(new WorkTypeId(fact.workTypeId), out _))
                throw new InvalidOperationException("suspension-observation-work-type-unknown");
            if (!active.TryGetValue(fact.characterId, out var work))
            {
                // A resumed/abandoned worker may still have undelivered evidence.
                if (!row.pending) throw new InvalidOperationException("suspension-completed-observation-has-no-active-work");
                continue;
            }
            if (work.workTypeId != fact.workTypeId || work.targetBuildingId != fact.targetBuildingId
                || work.alertEpochId < fact.alertEpochId || work.suspendedAtAbsoluteHour != fact.suspendedAtAbsoluteHour
                || work.outcomeOwnerRevision != fact.ownerRevision || work.progressExternallyPersisted != fact.progressExternallyPersisted
                || work.inlineCompletedWork != fact.inlineCompletedWork || work.inlineRequiredWork != fact.inlineRequiredWork)
                throw new InvalidOperationException("suspension-active-state-frozen-fact-conflict");
        }
    }
}
