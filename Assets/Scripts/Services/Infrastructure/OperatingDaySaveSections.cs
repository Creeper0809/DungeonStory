using System;
using System.Collections.Generic;
using DungeonStory.Operation;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DungeonStory.Infrastructure
{
public sealed class OperatingDaySettlementSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonOperatingDaySettlementSaveData,
        OperatingDaySettlementRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "operation.settlement";
    public const int CurrentVersion = 3;

    private readonly IOperatingDaySettlementSaveService saveService;

    public OperatingDaySettlementSaveSection(
        IOperatingDaySettlementSaveService saveService)
    {
        this.saveService = saveService
            ?? throw new ArgumentNullException(nameof(saveService));
    }

    public override string SectionId => Id;
    public override int SectionVersion => CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;

    protected override DungeonOperatingDaySettlementSaveData CapturePayload()
    {
        return saveService.Capture();
    }

    protected override void ValidateRawPayload(string payloadJson)
    {
        RequireTopLevelArrayFields(
            payloadJson,
            "facilityDetails",
            "reportHistory");
    }

    protected override OperatingDaySettlementRestoreCandidate
        BuildRestoreCandidate(
            DungeonOperatingDaySettlementSaveData payload)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        OperatingDaySettlementSaveValidation.Validate(payload, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Operating-day settlement restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }

        return saveService.PrepareRestore(payload);
    }

    protected override void PublishRestoreCandidate(
        OperatingDaySettlementRestoreCandidate candidate)
    {
        saveService.PublishRestore(candidate);
    }
}

public sealed class EventAlertSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonEventAlertSaveData,
        EventAlertRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "operation.event-alerts";
    public const int CurrentVersion = 6;

    private readonly IEventAlertSaveService saveService;

    public EventAlertSaveSection(IEventAlertSaveService saveService)
    {
        this.saveService = saveService
            ?? throw new ArgumentNullException(nameof(saveService));
    }

    public override string SectionId => Id;
    public override int SectionVersion => CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.Presentation;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        OperatingDaySettlementSaveSection.Id,
        InvasionSaveSection.Id,
        OffenseAggregateSaveSection.Id
    };

    protected override DungeonEventAlertSaveData CapturePayload()
    {
        return saveService.Capture();
    }

    protected override void ValidateRawPayload(string payloadJson)
    {
        var root = JObject.Parse(payloadJson);
        if (root["threatAlert"] is not JObject threat
            || threat["suspensionObservations"] is not JArray observations)
            throw new InvalidOperationException("Suspension observation list is missing or null.");
        foreach (var token in observations)
        {
            if (token is not JObject row || row["fact"] is not JObject fact
                || row["publicationKey"]?.Type != JTokenType.String || row["pending"]?.Type != JTokenType.Boolean)
                throw new InvalidOperationException("Suspension observation entry is incomplete.");
            foreach (string field in new[] { "characterId", "workerDisplayName", "workTypeId", "workDisplayName", "targetBuildingId", "targetDisplayName" })
                Require(fact, field, JTokenType.String);
            foreach (string field in new[] { "alertEpochId", "suspendedAtAbsoluteHour", "ownerRevision", "absoluteDay" })
                Require(fact, field, JTokenType.Integer);
            Require(fact, "progressExternallyPersisted", JTokenType.Boolean);
            foreach (string field in new[] { "inlineCompletedWork", "inlineRequiredWork" })
                if (fact[field]?.Type is not (JTokenType.Float or JTokenType.Integer))
                    throw new InvalidOperationException("Suspension progress field is missing: " + field);
        }
        static void Require(JObject value, string field, JTokenType type)
        {
            if (value[field]?.Type != type)
                throw new InvalidOperationException("Suspension fact field is missing: " + field);
        }
    }

    protected override EventAlertRestoreCandidate BuildRestoreCandidate(
        DungeonEventAlertSaveData payload)
    {
        EmergencyWorkSuspensionObservationState.Validate(payload?.threatAlert);
        IReadOnlyList<string> errors = EventAlertPayloadValidation.Validate(payload);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Event-alert restore candidate is invalid: "
                + string.Join(" | ", errors));
        }
        return saveService.PrepareRestore(payload);
    }

    protected override void PublishRestoreCandidate(
        EventAlertRestoreCandidate candidate) =>
        saveService.PublishRestore(candidate);
}

}
