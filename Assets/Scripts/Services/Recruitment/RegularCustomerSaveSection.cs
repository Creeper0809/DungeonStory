using System;
using System.Collections.Generic;
using System.Linq;

public sealed class RegularCustomerSaveSection :
    DungeonJsonSaveSection<DungeonRegularCustomerSaveData>
{
    public const string Id = "recruitment.regular-customers";

    private readonly IRegularCustomerRuntimeProvider runtimeProvider;
    private readonly IRunCharacterCatalog characterCatalog;

    public RegularCustomerSaveSection(
        IRegularCustomerRuntimeProvider runtimeProvider,
        IRunCharacterCatalog characterCatalog)
    {
        this.runtimeProvider = runtimeProvider
            ?? throw new ArgumentNullException(nameof(runtimeProvider));
        this.characterCatalog = characterCatalog
            ?? throw new ArgumentNullException(nameof(characterCatalog));
    }

    public override string SectionId => Id;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;

    protected override DungeonRegularCustomerSaveData CapturePayload()
    {
        DungeonRegularCustomerSaveData destination =
            new DungeonRegularCustomerSaveData();
        if (!runtimeProvider.TryGetRuntime(out RegularCustomerRuntime runtime))
        {
            return destination;
        }

        destination.records = runtime.State.Records
            .OrderBy(record => record.CustomerId)
            .Select(record => new DungeonRegularCustomerRecordSaveData
            {
                customerId = record.CustomerId,
                displayName = record.DisplayName,
                speciesTag = record.SpeciesTag,
                sourceDataId = record.SourceData != null
                    ? record.SourceData.id
                    : -1,
                visitCount = record.VisitCount,
                averageSatisfaction = record.AverageSatisfaction,
                isRegular = record.IsRegular,
                isRecruitCandidate = record.IsRecruitCandidate,
                isRecruited = record.IsRecruited,
                recruitCapabilities = record.RecruitCapabilities
            })
            .ToList();
        return destination;
    }

    protected override void RestorePayload(
        DungeonRegularCustomerSaveData source,
        DungeonGameRestoreReport report)
    {
        if (!runtimeProvider.TryGetRuntime(out RegularCustomerRuntime runtime))
        {
            report.AddWarning(
                "Regular customer runtime was not present; customer history was skipped.");
            return;
        }

        Dictionary<int, CharacterSO> characters = characterCatalog.Characters
            .Where(character => character != null)
            .GroupBy(character => character.id)
            .ToDictionary(group => group.Key, group => group.First());
        List<RegularCustomerRecord> records = new List<RegularCustomerRecord>();
        foreach (DungeonRegularCustomerRecordSaveData saved in source.records
                     ?? new List<DungeonRegularCustomerRecordSaveData>())
        {
            if (saved == null || string.IsNullOrWhiteSpace(saved.customerId))
            {
                continue;
            }

            characters.TryGetValue(saved.sourceDataId, out CharacterSO sourceData);
            records.Add(new RegularCustomerRecord(
                saved.customerId,
                saved.displayName,
                saved.speciesTag,
                sourceData,
                saved.visitCount,
                saved.averageSatisfaction,
                saved.isRegular,
                saved.isRecruitCandidate,
                saved.isRecruited,
                saved.recruitCapabilities));
        }

        runtime.State.Restore(records);
    }
}
