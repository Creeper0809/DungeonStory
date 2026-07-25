using System;
using System.Collections.Generic;
using System.Linq;

public sealed class StaffDiscontentSaveSection :
    DungeonJsonSaveSection<DungeonStaffDiscontentSaveData>
{
    public const string Id = "characters.staff-discontent";

    private readonly IStaffDiscontentRuntimeProvider runtimeProvider;

    public StaffDiscontentSaveSection(
        IStaffDiscontentRuntimeProvider runtimeProvider)
    {
        this.runtimeProvider = runtimeProvider
            ?? throw new ArgumentNullException(nameof(runtimeProvider));
    }

    public override string SectionId => Id;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;

    protected override DungeonStaffDiscontentSaveData CapturePayload()
    {
        DungeonStaffDiscontentSaveData destination =
            new DungeonStaffDiscontentSaveData();
        if (!runtimeProvider.TryGetRuntime(out StaffDiscontentRuntime runtime))
        {
            return destination;
        }

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

    protected override void RestorePayload(
        DungeonStaffDiscontentSaveData source,
        DungeonGameRestoreReport report)
    {
        if (!runtimeProvider.TryGetRuntime(out StaffDiscontentRuntime runtime))
        {
            report.AddWarning(
                "Staff discontent runtime was not present; staff discontent was skipped.");
            return;
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        List<StaffDiscontentSnapshot> records =
            new List<StaffDiscontentSnapshot>();
        foreach (DungeonStaffDiscontentRecordSaveData saved in source.records
                     ?? new List<DungeonStaffDiscontentRecordSaveData>())
        {
            if (saved == null || string.IsNullOrWhiteSpace(saved.staffId))
            {
                continue;
            }

            string staffId = saved.staffId.Trim();
            if (!ids.Add(staffId))
            {
                report.AddError($"Duplicate staff discontent ID '{staffId}'.");
                return;
            }

            records.Add(new StaffDiscontentSnapshot(
                staffId,
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
                saved.suppressed));
        }

        runtime.RestoreSnapshots(records);
    }
}
