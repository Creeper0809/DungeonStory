using System;
using DungeonStory.Foundation.Facts;

// Immutable campaign receipt, not a log payload. The Society owner validates the
// authored incident/source union; observers never decide whether an incident occurs.
public sealed class ObservedServiceIncidentFact
{
    private readonly string payload;
    public ObservedServiceIncidentFact(ObservedMealIncidentEvidenceSaveData source)
    {
        if (source?.version != ObservedMealIncidentEvidenceSaveData.CurrentVersion
            || !Enum.IsDefined(typeof(ObservedIncidentSourceKind), source.sourceKind)
            || !Enum.IsDefined(typeof(ServiceIncidentKind), source.incidentKind)
            || source.absoluteDay < 1 || source.observedDead || !float.IsFinite(source.actualDamage)
            || source.actualDamage < 0 || source.quantity < 0 || source.unitMassGrams < 0 || source.lossValue < 0)
            throw new InvalidOperationException("observed-service-incident-fact-invalid");
        foreach (string value in new[] { source.operationId, source.targetCharacterId, source.definitionId, source.occurrenceInstanceId })
            if (!DomainFactValueSyntax.IsText(value, 4096, false) || value != value.Trim())
                throw new InvalidOperationException("observed-service-incident-fact-identity-invalid");
        if (!new CharacterId(source.targetCharacterId).IsValid
            || new CharacterId(source.targetCharacterId).Value != source.targetCharacterId)
            throw new InvalidOperationException("observed-service-incident-fact-character-invalid");
        payload = DungeonCharacterWorldSaveCodec.SerializeFields(source);
    }
    public ObservedMealIncidentEvidenceSaveData Snapshot() =>
        DungeonSaveJson.Deserialize<ObservedMealIncidentEvidenceSaveData>(payload);
    internal string CanonicalPayload => payload;
}
