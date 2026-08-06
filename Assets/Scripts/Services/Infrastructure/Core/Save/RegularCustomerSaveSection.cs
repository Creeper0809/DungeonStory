using System;
using System.Collections.Generic;

public sealed class RegularCustomerSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonRegularCustomerSaveData,
        RegularCustomerRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "recruitment.regular-customers";
    private readonly IRegularCustomerPersistence persistence;
    private readonly IRecruitmentCharacterDefinitionCatalog catalog;

    public RegularCustomerSaveSection(
        IRegularCustomerPersistence persistence,
        IRecruitmentCharacterDefinitionCatalog catalog)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
    }

    public override string SectionId => Id;
    public override int SectionVersion => DungeonRegularCustomerSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    protected override DungeonRegularCustomerSaveData CapturePayload() =>
        persistence.CaptureState();

    protected override void NormalizeRestorePayload(
        DungeonRegularCustomerSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload?.records == null)
        {
            return;
        }

        bool changed = false;
        for (int index = 0; index < payload.records.Count; index++)
        {
            DungeonRegularCustomerRecordSaveData record = payload.records[index];
            if (record != null)
            {
                string previous = record.customerId;
                record.customerId = NormalizeV18CharacterReference(
                    previous,
                    report,
                    $"records[{index}].customerId");
                changed |= !string.Equals(
                    previous,
                    record.customerId,
                    StringComparison.Ordinal);
            }
        }
        if (changed)
        {
            payload.records.Sort((left, right) => string.CompareOrdinal(
                left?.customerId,
                right?.customerId));
        }
    }

    protected override void ValidateRawPayload(string payloadJson) =>
        RequireTopLevelArrayFields(payloadJson, "records");

    private void ValidatePayload(
        DungeonRegularCustomerSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload?.records == null)
        {
            report.AddError("Regular-customer payload or record list is null.");
            return;
        }
        if (payload.version != DungeonRegularCustomerSaveData.CurrentVersion)
        {
            report.AddError(
                $"Regular-customer payload version {payload.version} is unsupported.");
        }
        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<int> characterIds = new(catalog.CharacterDefinitionIds);
        string previous = string.Empty;
        foreach (DungeonRegularCustomerRecordSaveData saved in payload.records)
        {
            string id = saved?.customerId ?? string.Empty;
            if (saved == null
                || !IsCanonicalRequired(id)
                || previous.Length > 0 && string.CompareOrdinal(previous, id) >= 0)
            {
                report.AddError(
                    "Regular-customer payload contains a null, non-canonical, or unordered record ID.");
                continue;
            }
            previous = id;
            if (!ids.Add(id))
            {
                report.AddError($"Regular-customer payload contains duplicate ID '{id}'.");
            }
            if (saved.sourceDataId < -1
                || saved.sourceDataId >= 0 && !characterIds.Contains(saved.sourceDataId))
            {
                report.AddError(
                    $"Regular customer '{id}' references missing character definition {saved.sourceDataId}.");
            }
            if (saved.visitCount < 0
                || float.IsNaN(saved.averageSatisfaction)
                || float.IsInfinity(saved.averageSatisfaction)
                || saved.averageSatisfaction < 0f
                || saved.averageSatisfaction > 100f
                || saved.visitCount == 0 && saved.averageSatisfaction != 0f)
            {
                report.AddError($"Regular customer '{id}' has invalid visit statistics.");
            }
            if (!IsCanonicalRequired(saved.displayName)
                || !IsCanonicalRequired(saved.speciesTag))
            {
                report.AddError($"Regular customer '{id}' has non-canonical display data.");
            }
            if (saved.isRecruited && !saved.isRecruitCandidate
                || saved.isRecruitCandidate && !saved.isRegular)
            {
                report.AddError(
                    $"Regular customer '{id}' has an invalid recruitment status hierarchy.");
            }
            if (saved.recruitCapabilities == RecruitCapability.None
                || (saved.recruitCapabilities & ~RecruitCapability.All) != 0)
            {
                report.AddError(
                    $"Regular customer '{id}' has invalid recruit capabilities {(int)saved.recruitCapabilities}.");
            }
        }
    }

    protected override RegularCustomerRestoreCandidate BuildRestoreCandidate(
        DungeonRegularCustomerSaveData source)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        ValidatePayload(source, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Regular-customer restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }

        return persistence.PrepareRestore(source);
    }

    protected override void PublishRestoreCandidate(
        RegularCustomerRestoreCandidate candidate)
    {
        persistence.PublishRestore(candidate);
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
