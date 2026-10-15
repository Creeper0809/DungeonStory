using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public sealed class FacilityVandalismPendingSaveSection :
    DungeonStrictJsonSaveSection<FacilityVandalismPendingSaveData, FacilityVandalismPendingState>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "buildings.vandalism-pending";
    private readonly FacilityVandalismPublicationOutbox owner;
    public FacilityVandalismPendingSaveSection(FacilityVandalismPublicationOutbox owner) =>
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    public override string SectionId => Id;
    public override int SectionVersion => FacilityVandalismPendingSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => Array.Empty<string>();
    protected override FacilityVandalismPendingSaveData CapturePayload() => owner.Capture();
    protected override FacilityVandalismPendingState BuildRestoreCandidate(FacilityVandalismPendingSaveData payload) => owner.PrepareRestore(payload);
    protected override void PublishRestoreCandidate(FacilityVandalismPendingState candidate) => owner.PublishRestore(candidate);
    protected override void ValidateRawPayload(string json) => ValidatePendingJson(json);
    internal static void ValidatePendingJson(string json)
    {
        var root = JObject.Parse(json);
        if (root["version"]?.Type != JTokenType.Integer
            || root["version"].Value<int>() != FacilityVandalismPendingSaveData.CurrentVersion
            || root["pending"] is not JArray pending)
            throw new InvalidOperationException("Facility vandalism pending schema is missing or unsupported.");
        foreach (var entry in pending) ValidateRecord(entry as JObject);
    }
    internal static void ValidateFacilityJson(string json)
    {
        var root = JObject.Parse(json);
        ValidateRecord(root["lastVandalism"] as JObject);
    }
    private static void ValidateRecord(JObject record)
    {
        if (record == null) throw new InvalidOperationException("Facility vandalism record is missing or null.");
        Require(JTokenType.String, "facilityId", "buildingDefinitionId", "actorId", "actorName", "facilityName", "source", "reason", "publicationKey");
        Require(JTokenType.Integer, "operationSequence", "absoluteDay", "positionX", "positionY", "phase");
        Require(JTokenType.Boolean, "damagedBefore", "damagedAfter", "hasStructuralIntegrity");
        foreach (string field in new[] { "structuralHitPointsBefore", "structuralHitPointsAfter" })
            if (record[field]?.Type is not (JTokenType.Integer or JTokenType.Float))
                throw new InvalidOperationException("Facility vandalism scalar missing: " + field);
        if (record["witnessPublicationKeys"] is not JArray witnesses)
            throw new InvalidOperationException("Facility vandalism witness references missing.");
        foreach (var witness in witnesses)
            if (witness.Type != JTokenType.String) throw new InvalidOperationException("Facility vandalism witness reference invalid.");
        void Require(JTokenType type, params string[] fields)
        {
            foreach (string field in fields)
                if (record[field]?.Type != type) throw new InvalidOperationException("Facility vandalism field missing: " + field);
        }
    }
}
