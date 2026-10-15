using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

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

    protected override void ValidateRawPayload(string payloadJson)
    {
        var root = JObject.Parse(payloadJson);
        if (root["version"]?.Type != JTokenType.Integer || root["version"].Value<int>() != DungeonStaffDiscontentSaveData.CurrentVersion
            || root["records"] is not JArray records || root["outcomeRevision"]?.Type != JTokenType.Integer
            || root["transitionRevision"]?.Type != JTokenType.Integer)
            throw new InvalidOperationException("staff-discontent-v4-json-invalid");
        foreach (var record in records) RequireFields(record as JObject, typeof(DungeonStaffDiscontentRecordSaveData));
        foreach (string name in new[] { "responseObservation", "transitionObservation" })
        {
            var observation = root[name] as JObject;
            RequireFields(observation, typeof(StaffDiscontentObservationData));
            var fact = observation["fact"] as JObject;
            RequireFields(fact, typeof(StaffDiscontentFactData));
            RequireFields(fact["before"] as JObject, typeof(DungeonStaffDiscontentRecordSaveData));
            RequireFields(fact["after"] as JObject, typeof(DungeonStaffDiscontentRecordSaveData));
        }
        static void RequireFields(JObject value, Type type)
        {
            if (value == null) throw new InvalidOperationException("staff-discontent-json-object-missing");
            foreach (var field in type.GetFields())
            {
                var token = value[field.Name];
                bool valid = field.FieldType == typeof(string) ? token?.Type == JTokenType.String
                    : field.FieldType == typeof(bool) ? token?.Type == JTokenType.Boolean
                    : field.FieldType == typeof(float) ? token?.Type is JTokenType.Float or JTokenType.Integer
                    : field.FieldType.IsEnum || field.FieldType == typeof(int) || field.FieldType == typeof(long)
                        ? token?.Type == JTokenType.Integer : token != null;
                if (!valid) throw new InvalidOperationException("staff-discontent-json-field-invalid:" + field.Name);
            }
        }
    }

    protected override DungeonStaffDiscontentSaveData CapturePayload()
    {
        DungeonStaffDiscontentSaveData destination =
            new DungeonStaffDiscontentSaveData();
        destination.outcomeRevision = runtime.OutcomeRevision;
        destination.transitionRevision = runtime.TransitionRevision;
        destination.responseObservation = runtime.ResponseObservation;
        destination.transitionObservation = runtime.TransitionObservation;
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
                localRebellionDays = snapshot.localRebellionDays,
                lastProcessedAbsoluteDay = snapshot.lastProcessedAbsoluteDay,
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

    internal static void ValidatePayload(
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
        if (payload.outcomeRevision < 0L)
        {
            report.AddError(
                "Staff-discontent outcome revision cannot be negative.");
        }
        if (payload.transitionRevision < 0L)
        {
            report.AddError(
                "Staff-discontent transition revision cannot be negative.");
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
                || saved.lowMoodDays < 0
                || saved.localRebellionDays < 0
                || saved.lastProcessedAbsoluteDay < -1)
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
        ValidateObservation(payload.responseObservation, StaffDiscontentFactKind.Response, payload.outcomeRevision);
        ValidateObservation(payload.transitionObservation, StaffDiscontentFactKind.DailyTransition, payload.transitionRevision);
        void ValidateObservation(StaffDiscontentObservationData observation, StaffDiscontentFactKind kind, long revision)
        {
            try
            {
                StaffDiscontentFactValues.ValidateObservation(observation, kind, revision);
                if (StaffDiscontentFactValues.IsEmpty(observation.fact)) return;
                var owner = payload.records.FirstOrDefault(value => value?.staffId == observation.fact.after.staffId);
                if (owner == null || observation.pending && !StaffDiscontentFactValues.SameState(owner, observation.fact.after))
                    throw new ArgumentException("staff-discontent-observation-owner-conflict");
            }
            catch (ArgumentException exception) { report.AddError(exception.Message); }
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
                saved.suppressed,
                saved.lastProcessedAbsoluteDay,
                saved.localRebellionDays))
            .ToList();

        return runtime.PrepareRestoreCandidate(
            records,
            source.outcomeRevision,
            source.transitionRevision,
            source.responseObservation,
            source.transitionObservation);
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
