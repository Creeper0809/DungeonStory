using System;

/// <summary>
/// Narrative projection for a physical disease-exposure fact. Gameplay result
/// identity, subjects, metrics, provenance, status, and summary stay here.
/// </summary>
public static class PopulationDiseaseExposureFactMapping
{
    public static PopulationDiseaseExposureOutcomeReceipt Capture(
        PopulationDiseaseExposureFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));
        return EnvironmentOutcomeReceiptFactory.CreateDiseaseExposure(
            fact.CharacterId,
            fact.CharacterDisplayName,
            fact.Disease,
            fact.Route,
            fact.ExposureHours,
            fact.EnvironmentCoefficient,
            fact.AbsoluteDay,
            fact.OwnerRevision,
            fact.SourceKind,
            fact.SourceId);
    }
}
