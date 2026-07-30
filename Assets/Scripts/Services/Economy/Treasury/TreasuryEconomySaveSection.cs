using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TreasuryEconomySaveSection : IDungeonSaveSection
{
    public const string Id = "economy.treasury";

    private readonly IEconomyTransactionLedger transactionLedger;
    private readonly IEmploymentContractRuntime employment;
    private readonly IAutoProcurementRuntime autoProcurement;
    private readonly IPaidFacilityContractRuntime facilityContracts;
    private readonly IEquipmentOverclockRuntime overclock;
    private readonly ITreasuryDefenseRuntime treasuryDefense;

    public TreasuryEconomySaveSection(
        IEconomyTransactionLedger transactionLedger,
        IEmploymentContractRuntime employment,
        IAutoProcurementRuntime autoProcurement,
        IPaidFacilityContractRuntime facilityContracts,
        IEquipmentOverclockRuntime overclock,
        ITreasuryDefenseRuntime treasuryDefense)
    {
        this.transactionLedger = transactionLedger
            ?? throw new ArgumentNullException(nameof(transactionLedger));
        this.employment = employment
            ?? throw new ArgumentNullException(nameof(employment));
        this.autoProcurement = autoProcurement
            ?? throw new ArgumentNullException(nameof(autoProcurement));
        this.facilityContracts = facilityContracts
            ?? throw new ArgumentNullException(nameof(facilityContracts));
        this.overclock = overclock
            ?? throw new ArgumentNullException(nameof(overclock));
        this.treasuryDefense = treasuryDefense
            ?? throw new ArgumentNullException(nameof(treasuryDefense));
    }

    public string SectionId => Id;
    public int SectionVersion => 2;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => new[]
    {
        PhysicalItemsSaveSection.Id,
        CharacterWorldSaveSection.Id
    };

    public string Capture()
    {
        return JsonUtility.ToJson(new TreasuryEconomySaveData
        {
            transactionLedger = transactionLedger.Capture(),
            employment = employment.Capture(),
            autoProcurement = autoProcurement.Capture(),
            facilityContracts = facilityContracts.Capture(),
            overclock = overclock.Capture(),
            treasuryDefense = treasuryDefense.Capture()
        });
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (sectionVersion != SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {Id} section version {sectionVersion}.");
        }

        TreasuryEconomySaveData saveData =
            JsonUtility.FromJson<TreasuryEconomySaveData>(
                payloadJson ?? string.Empty)
            ?? new TreasuryEconomySaveData();
        transactionLedger.Restore(saveData.transactionLedger);
        employment.Restore(saveData.employment);
        facilityContracts.Restore(saveData.facilityContracts);
        autoProcurement.Restore(saveData.autoProcurement);
        overclock.Restore(saveData.overclock);
        treasuryDefense.Restore(saveData.treasuryDefense);
    }
}
