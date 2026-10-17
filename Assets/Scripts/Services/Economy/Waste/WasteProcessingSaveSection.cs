using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed class WasteProcessingSaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "economy.waste";

    private static readonly IReadOnlyList<string> Dependencies =
        new[]
        {
            PhysicalItemsSaveSection.Id,
            ProductionBillsSaveSection.Id
        };

    private readonly IWasteProcessingPersistence persistence;

    public WasteProcessingSaveSection(IWasteProcessingPersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonWasteProcessingSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;

    public string Capture() => JsonUtility.ToJson(persistence.Capture());

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        RequireVersion(sectionVersion);
        persistence.BuildRestore(Parse(payloadJson));
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        IDungeonSaveRestoreStage stage = StageRestore(
            payloadJson,
            sectionVersion,
            report);
        if (report.Success)
        {
            stage.Commit(report);
        }
    }

    public IDungeonSaveRestoreStage StageRestore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        RequireVersion(sectionVersion);
        WasteProcessingRestoreCandidate candidate =
            persistence.BuildRestore(Parse(payloadJson));
        return new DungeonDelegateSaveRestoreStage(
            SectionId,
            _ => persistence.Restore(candidate));
    }

    private void RequireVersion(int sectionVersion)
    {
        if (sectionVersion != SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {SectionId} section version {sectionVersion}; expected {SectionVersion}.");
        }
    }

    private DungeonWasteProcessingSaveData Parse(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new InvalidOperationException(
                $"{SectionId} payload is empty.");
        }
        try
        {
            ValidateObservationJson(payloadJson);
            return JsonUtility.FromJson<DungeonWasteProcessingSaveData>(payloadJson)
                ?? throw new InvalidOperationException(
                    $"{SectionId} payload deserialized to null.");
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"{SectionId} payload JSON is invalid: {exception.Message}",
                exception);
        }
    }

    public static void ValidateObservationJson(string json)
    {
        var root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        Require(root, "version", JTokenType.Integer);
        Require(root, "nextPolicySequence", JTokenType.Integer);
        Require(root, "policies", JTokenType.Array);
        Require(root, "pendingPolicyChanges", JTokenType.Array);
        foreach (var token in (JArray)root["pendingPolicyChanges"])
        {
            if (token is not JObject row) throw new InvalidOperationException("waste-policy-observation-row-invalid");
            foreach (string name in new[] { "publicationKey", "publicationId", "publicationFingerprint" }) Require(row, name, JTokenType.String);
            Require(row, "fact", JTokenType.Object);
            var fact = (JObject)row["fact"];
            Require(fact, "operationId", JTokenType.String);
            foreach (string name in new[] { "ownerRevision", "absoluteDay", "origin", "beforeDisposition", "afterDisposition" }) Require(fact, name, JTokenType.Integer);
            foreach (string name in new[] { "beforeEnabled", "afterEnabled" }) Require(fact, name, JTokenType.Boolean);
            foreach (string name in new[] { "beforeMaximumFeedContamination", "afterMaximumFeedContamination" })
                if (fact[name]?.Type is not (JTokenType.Float or JTokenType.Integer))
                    throw new InvalidOperationException("waste-policy-fact-field-invalid:" + name);
        }
    }

    private static void Require(JObject row, string name, JTokenType type)
    {
        if (row[name]?.Type != type) throw new InvalidOperationException("waste-policy-save-field-invalid:" + name);
    }
}
