using System;
using System.Collections.Generic;
using System.Linq;

public sealed class StaffDiscontentSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonStaffDiscontentSaveData,
        StaffDiscontentRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "characters.staff-discontent";

    private readonly StaffDiscontentRuntime runtime;

    public StaffDiscontentSaveSection(
        CharacterSceneRuntimeReferences runtimeReferences)
    {
        runtime = (runtimeReferences
                ?? throw new ArgumentNullException(nameof(runtimeReferences)))
            .StaffDiscontent
            ?? throw new InvalidOperationException(
                $"{nameof(StaffDiscontentSaveSection)} requires a loaded {nameof(StaffDiscontentRuntime)}.");
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        DungeonStaffDiscontentSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;

    protected override void ValidateRawPayload(string payloadJson) =>
        RequireTopLevelArrayFields(payloadJson, "records");

    protected override DungeonStaffDiscontentSaveData CapturePayload()
    {
        DungeonStaffDiscontentSaveData destination =
            new DungeonStaffDiscontentSaveData();
        destination.records = runtime.CaptureSnapshots()
            .OrderBy(snapshot => snapshot.staffId, StringComparer.Ordinal)
            .Select(snapshot => new DungeonStaffDiscontentRecordSaveData
            {
                staffId = snapshot.staffId,
                displayName = snapshot.displayName,
                stage = snapshot.stage,
                outcome = snapshot.outcome,
                mood = snapshot.mood,
                lowMoodDays = snapshot.lowMoodDays,
                permanentLoss = snapshot.permanentLoss,
                departed = snapshot.departed,
                localRebellion = snapshot.localRebellion,
                ownerThreat = snapshot.ownerThreat,
                isolated = snapshot.isolated,
                suppressed = snapshot.suppressed
            })
            .ToList();
        return destination;
    }

    protected override void NormalizeRestorePayload(
        DungeonStaffDiscontentSaveData payload,
        DungeonGameRestoreReport report) =>
        V18SurvivalEnvironmentCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    private static void ValidatePayload(
        DungeonStaffDiscontentSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload == null || payload.records == null)
        {
            report.AddError(
                "Staff-discontent payload or record list is null.");
            return;
        }
        if (payload.version != DungeonStaffDiscontentSaveData.CurrentVersion)
        {
            report.AddError(
                $"Staff-discontent payload version {payload.version} is unsupported.");
        }

        HashSet<string> staffIds = new HashSet<string>(StringComparer.Ordinal);
        string previousStaffId = null;
        foreach (DungeonStaffDiscontentRecordSaveData saved in payload.records)
        {
            string staffId = saved?.staffId ?? string.Empty;
            CharacterId typedStaffId = new CharacterId(staffId);
            if (saved == null
                || !typedStaffId.IsValid
                || !string.Equals(
                    typedStaffId.Value,
                    staffId,
                    StringComparison.Ordinal)
                || (previousStaffId != null
                    && string.CompareOrdinal(previousStaffId, staffId) >= 0))
            {
                report.AddError(
                    "Staff-discontent payload contains a null, non-canonical, duplicate, or unordered staff ID.");
                continue;
            }
            previousStaffId = staffId;

            if (!staffIds.Add(staffId))
            {
                report.AddError(
                    $"Staff-discontent payload contains duplicate ID '{staffId}'.");
            }

            if (!IsCanonicalRequired(saved.displayName))
            {
                report.AddError(
                    $"Staff-discontent record '{staffId}' has non-canonical display data.");
            }
            if (!Enum.IsDefined(typeof(StaffDiscontentStage), saved.stage)
                || saved.outcome != StaffDiscontentOutcome.None)
            {
                report.AddError(
                    $"Staff-discontent record '{staffId}' has an invalid stage or persisted event outcome.");
            }
            if (float.IsNaN(saved.mood)
                || float.IsInfinity(saved.mood)
                || saved.mood < 0f
                || saved.mood > 100f
                || saved.lowMoodDays < 0)
            {
                report.AddError(
                    $"Staff-discontent record '{staffId}' has invalid mood history.");
            }
            if (!HasCanonicalTerminalStatus(saved))
            {
                report.AddError(
                    $"Staff-discontent record '{staffId}' has an invalid terminal-status hierarchy.");
            }
        }
    }

    protected override StaffDiscontentRestoreCandidate BuildRestoreCandidate(
        DungeonStaffDiscontentSaveData source)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        ValidatePayload(source, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Staff-discontent restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }

        List<StaffDiscontentSnapshot> records = source.records
            .Select(saved => new StaffDiscontentSnapshot(
                saved.staffId,
                saved.displayName,
                saved.stage,
                saved.outcome,
                saved.mood,
                saved.lowMoodDays,
                saved.permanentLoss,
                saved.departed,
                saved.localRebellion,
                saved.ownerThreat,
                saved.isolated,
                saved.suppressed))
            .ToList();

        return runtime.PrepareRestoreCandidate(records);
    }

    protected override void PublishRestoreCandidate(
        StaffDiscontentRestoreCandidate candidate)
    {
        runtime.PublishRestoreCandidate(candidate);
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool HasCanonicalTerminalStatus(
        DungeonStaffDiscontentRecordSaveData saved)
    {
        switch (saved.stage)
        {
            case StaffDiscontentStage.Departure:
                return saved.permanentLoss
                    && saved.departed
                    && !saved.localRebellion
                    && !saved.ownerThreat
                    && !saved.isolated
                    && !saved.suppressed;
            case StaffDiscontentStage.LocalRebellion:
                if (!saved.permanentLoss || saved.departed)
                {
                    return false;
                }
                if (saved.suppressed)
                {
                    return !saved.localRebellion && !saved.ownerThreat;
                }
                return saved.localRebellion
                    && (!saved.isolated || !saved.ownerThreat);
            default:
                return !saved.permanentLoss
                    && !saved.departed
                    && !saved.localRebellion
                    && !saved.ownerThreat
                    && !saved.isolated
                    && !saved.suppressed;
        }
    }
}
