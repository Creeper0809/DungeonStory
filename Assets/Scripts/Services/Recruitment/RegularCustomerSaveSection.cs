using System;
using System.Collections.Generic;
using System.Linq;

public sealed class RegularCustomerPersistenceAdapter :
    IRegularCustomerPersistence,
    IRecruitmentCharacterDefinitionCatalog
{
    private readonly RegularCustomerRuntime runtime;
    private readonly IRunCharacterCatalog characterCatalog;

    public RegularCustomerPersistenceAdapter(
        RegularCustomerRuntime runtime,
        IRunCharacterCatalog characterCatalog)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
        this.characterCatalog = characterCatalog
            ?? throw new ArgumentNullException(nameof(characterCatalog));
    }

    public IReadOnlyCollection<int> CharacterDefinitionIds => characterCatalog
        .Characters
        .Where(character => character != null)
        .Select(character => character.id)
        .Distinct()
        .OrderBy(id => id)
        .ToArray();

    public DungeonRegularCustomerSaveData CaptureState() => new()
    {
        records = runtime.State.Records
            .OrderBy(record => record.CustomerId, StringComparer.Ordinal)
            .Select(record => new DungeonRegularCustomerRecordSaveData
            {
                customerId = record.CustomerId,
                displayName = record.DisplayName,
                speciesTag = record.SpeciesTag,
                sourceDataId = record.SourceData != null ? record.SourceData.id : -1,
                visitCount = record.VisitCount,
                averageSatisfaction = record.AverageSatisfaction,
                isRegular = record.IsRegular,
                isRecruitCandidate = record.IsRecruitCandidate,
                isRecruited = record.IsRecruited,
                recruitCapabilities = record.RecruitCapabilities
            })
            .ToList()
    };

    public RegularCustomerRestoreCandidate PrepareRestore(
        DungeonRegularCustomerSaveData snapshot)
    {
        if (snapshot?.records == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }
        Dictionary<int, CharacterSO> characters = characterCatalog.Characters
            .Where(character => character != null)
            .GroupBy(character => character.id)
            .ToDictionary(group => group.Key, group => group.First());
        return runtime.PrepareRestoreCandidate(snapshot.records.Select(saved =>
        {
            characters.TryGetValue(saved.sourceDataId, out CharacterSO sourceData);
            return new RegularCustomerRecord(
                saved.customerId,
                saved.displayName,
                saved.speciesTag,
                sourceData,
                saved.visitCount,
                saved.averageSatisfaction,
                saved.isRegular,
                saved.isRecruitCandidate,
                saved.isRecruited,
                saved.recruitCapabilities);
        }));
    }

    public void PublishRestore(RegularCustomerRestoreCandidate candidate)
    {
        runtime.PublishRestoreCandidate(candidate
            ?? throw new ArgumentNullException(nameof(candidate)));
    }
}
