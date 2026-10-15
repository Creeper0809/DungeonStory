using System;

public static class CertifiedSeedFactObserverMapping
{
    public static CertifiedSeedCompletionOutcomeReceipt Capture(
        CertifiedSeedCompletedFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));
        return EnvironmentOutcomeReceiptFactory.CreateCertifiedSeedCompletion(
            fact.SourceReceipt,
            fact.FacilityDisplayName,
            fact.CropDisplayName,
            fact.OutputItemDefinitionId,
            fact.OutputItemDisplayName,
            fact.OutputQuantity,
            fact.OutputMassGrams,
            fact.OutputOutcomeFingerprint,
            fact.AbsoluteDay,
            fact.OwnerRevision);
    }
}
