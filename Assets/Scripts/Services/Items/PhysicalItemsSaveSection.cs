using System;
using System.Collections.Generic;
using UnityEngine;

public interface IPhysicalItemRestoreStaging
{
    IDungeonSaveRestoreStage StageRestore(DungeonPhysicalItemSaveData snapshot);
    IDungeonSaveRestoreStage StageTransactionalRestore(
        DungeonPhysicalItemSaveData snapshot,
        IRestoreWorldCandidateQuery restoreWorldCandidates);
}

public sealed class PhysicalItemsSaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "items.physical";

    private readonly IWorldItemStackRuntime runtime;
    private readonly IPhysicalItemRestoreStaging restoreStaging;
    private readonly IRestoreWorldCandidateQuery restoreWorldCandidates;
    private readonly IProductionOutputLifecycleRestoreCandidatePublisher
        lifecycleRestoreCandidates;

    public PhysicalItemsSaveSection(
        IWorldItemStackRuntime runtime,
        IPhysicalItemRestoreStaging restoreStaging,
        IRestoreWorldCandidateQuery restoreWorldCandidates,
        IProductionOutputLifecycleRestoreCandidatePublisher
            lifecycleRestoreCandidates)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.restoreStaging = restoreStaging
            ?? throw new ArgumentNullException(nameof(restoreStaging));
        this.restoreWorldCandidates = restoreWorldCandidates
            ?? throw new ArgumentNullException(nameof(restoreWorldCandidates));
        this.lifecycleRestoreCandidates = lifecycleRestoreCandidates
            ?? throw new ArgumentNullException(nameof(lifecycleRestoreCandidates));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonPhysicalItemSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.Items;
    public IReadOnlyList<string> DependsOn => new[]
    {
        ModularFacilityWorldSaveSection.Id,
        CharacterWorldSaveSection.Id
    };

    public string Capture()
    {
        return JsonUtility.ToJson(runtime.Capture());
    }

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (sectionVersion != SectionVersion)
        {
            report.AddError(
                $"Unsupported physical item section version {sectionVersion}; expected {SectionVersion}.");
            return;
        }

        DungeonPhysicalItemSaveData payload = Deserialize(payloadJson, report);
        if (payload != null)
        {
            PhysicalItemSaveValidation.Validate(
                payload,
                report,
                runtime.CatalogProvider);
        }
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
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (sectionVersion != SectionVersion)
        {
            report.AddError(
                $"Unsupported physical item section version {sectionVersion}; expected {SectionVersion}.");
            return new DungeonDelegateSaveRestoreStage(Id, _ => { });
        }

        DungeonPhysicalItemSaveData payload = Deserialize(payloadJson, report);
        if (payload != null)
        {
            PhysicalItemSaveValidation.Validate(
                payload,
                report,
                runtime.CatalogProvider);
        }
        if (!report.Success)
        {
            return new DungeonDelegateSaveRestoreStage(Id, _ => { });
        }

        IDungeonSaveRestoreStage inner =
            restoreStaging.StageTransactionalRestore(
            payload,
            restoreWorldCandidates);
        return new DungeonBeforeCommitSaveRestoreStage(
            inner,
            () => lifecycleRestoreCandidates.SetPhysicalItems(payload));
    }

    private DungeonPhysicalItemSaveData Deserialize(
        string payloadJson,
        DungeonGameRestoreReport report)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            report.AddError("Physical item payload JSON is empty.");
            return null;
        }

        try
        {
            ValidateRequiredCurrentSchemaShape(payloadJson);
        }
        catch (Exception exception)
        {
            report.AddError(exception.Message);
            return null;
        }

        try
        {
            DungeonPhysicalItemSaveData payload =
                DungeonSaveJson.Deserialize<DungeonPhysicalItemSaveData>(payloadJson);
            if (payload == null)
            {
                report.AddError("Physical item payload deserialized to null.");
            }
            else
            {
                var raw = Newtonsoft.Json.Linq.JObject.Parse(payloadJson);
                if (raw["physicalEffectReceiptSchemaVersion"] == null)
                {
                    // Explicit additive compatibility: old saves contain no historical
                    // receipts. Cross-section validation must not invent missing ones.
                    payload.physicalEffectReceiptSchemaVersion = 0;
                    payload.physicalEffectReceipts = new();
                }
                V18WorldEconomyCharacterReferenceRestoreNormalizer.Normalize(
                    payload,
                    (value, path) =>
                        V18TypedCharacterReferenceRestoreNormalizer
                            .RewriteLegacyReference(
                                value,
                                report,
                                SectionId,
                                path));
            }
            return payload;
        }
        catch (Exception ex)
        {
            report.AddError(
                $"Physical item payload JSON is invalid: {ex.Message}");
            return null;
        }
    }

    internal static void ValidateRequiredCheckpointScalarFields(
        string payloadJson)
    {
        if (!HasTopLevelProperty(payloadJson, "version")
            || !HasTopLevelProperty(
                payloadJson,
                "lastConfirmedExactRouteCheckpointSequence")
            || !HasTopLevelProperty(
                payloadJson,
                "lastConfirmedExactRouteCheckpointDigest"))
        {
            throw new InvalidOperationException(
                "Physical item current schema is missing a required exact-route checkpoint scalar field.");
        }
    }

    public static void ValidateRequiredCurrentSchemaShape(string payloadJson)
    {
        DungeonStrictJsonShape.RequireTopLevelArrays(
            Id,
            payloadJson,
            new[]
            {
                "pendingExactOutputRoutes",
                "pendingProductionCustodyDrains",
                "pendingProductionInputDestinationDrains",
                "pendingCapacityRoutingDrains"
            });
        ValidateRequiredCheckpointScalarFields(payloadJson);
        ValidateRelocationShape(payloadJson);
        ValidateBatchDispositionShape(payloadJson);
        ValidateTransformationShape(payloadJson);
        ValidatePhysicalEffectReceiptShape(payloadJson);
    }

    private static void ValidatePhysicalEffectReceiptShape(string json)
    {
        var root = Newtonsoft.Json.Linq.JObject.Parse(json);
        var version = root["physicalEffectReceiptSchemaVersion"];
        var receipts = root["physicalEffectReceipts"];
        if (version == null && receipts == null) return; // legacy physical22
        if (version?.Type != Newtonsoft.Json.Linq.JTokenType.Integer
            || (int)version != PhysicalEffectReceiptRules.SchemaVersion
            || receipts is not Newtonsoft.Json.Linq.JArray rows)
            throw new InvalidOperationException("Physical effect receipt schema fields are missing or unsupported.");
        foreach (var row in rows)
        {
            RequireFields(row, typeof(PhysicalEffectReceiptSaveData));
            if (row["lines"] is not Newtonsoft.Json.Linq.JArray lines)
                throw new InvalidOperationException("Physical effect receipt lines are required.");
            foreach (var line in lines) RequireFields(line, typeof(PhysicalEffectReceiptLineSaveData));
        }
    }

    private static void ValidateTransformationShape(string json)
    {
        var root = Newtonsoft.Json.Linq.JObject.Parse(json);
        if (root["physicalItemTransformations"] is not Newtonsoft.Json.Linq.JArray rows)
            throw new InvalidOperationException("Physical item current schema requires physicalItemTransformations.");
        foreach (var row in rows)
        {
            RequireFields(row, typeof(PhysicalItemTransformationSaveData));
            var fact = row["fact"];
            RequireFields(fact, typeof(PhysicalItemTransformationData));
            foreach (var input in (Newtonsoft.Json.Linq.JArray)fact["inputs"])
                RequireFields(input, typeof(PhysicalItemTransformationInputData));
            foreach (var output in (Newtonsoft.Json.Linq.JArray)fact["outputs"])
                RequireFields(output, typeof(PhysicalItemTransformationOutputData));
        }
    }

    private static void RequireFields(Newtonsoft.Json.Linq.JToken token, Type type)
    {
        if (token is not Newtonsoft.Json.Linq.JObject data)
            throw new InvalidOperationException("Physical receipt/transform requires an object.");
        foreach (var field in type.GetFields())
        {
            var expected = field.FieldType == typeof(string) ? Newtonsoft.Json.Linq.JTokenType.String
                : field.FieldType == typeof(bool) ? Newtonsoft.Json.Linq.JTokenType.Boolean
                : field.FieldType == typeof(int) || field.FieldType == typeof(long)
                    ? Newtonsoft.Json.Linq.JTokenType.Integer
                : field.FieldType == typeof(PhysicalItemTransformationData) ? Newtonsoft.Json.Linq.JTokenType.Object
                : Newtonsoft.Json.Linq.JTokenType.Array;
            if (data[field.Name]?.Type != expected)
                throw new InvalidOperationException("Physical receipt/transform field missing or invalid: " + field.Name);
        }
    }

    private static void ValidateBatchDispositionShape(string json)
    {
        var root = Newtonsoft.Json.Linq.JObject.Parse(json);
        if (root["pendingBatchDispositions"] is not Newtonsoft.Json.Linq.JArray rows)
            throw new InvalidOperationException("Physical item current schema requires pendingBatchDispositions.");
        foreach (var row in rows)
        {
            if (row is not Newtonsoft.Json.Linq.JObject data)
                throw new InvalidOperationException("Physical disposition must be an object.");
            foreach (var field in typeof(PhysicalItemBatchDispositionSaveData).GetFields())
            {
                var expected = field.FieldType == typeof(string) ? Newtonsoft.Json.Linq.JTokenType.String
                    : field.FieldType == typeof(bool) ? Newtonsoft.Json.Linq.JTokenType.Boolean
                    : field.FieldType == typeof(int) || field.FieldType == typeof(long)
                        ? Newtonsoft.Json.Linq.JTokenType.Integer : Newtonsoft.Json.Linq.JTokenType.Array;
                if (data[field.Name]?.Type != expected)
                    throw new InvalidOperationException("Physical disposition field missing or invalid: " + field.Name);
            }
            foreach (var fact in (Newtonsoft.Json.Linq.JArray)data["sourceFacts"])
            {
                if (fact is not Newtonsoft.Json.Linq.JObject source)
                    throw new InvalidOperationException("Physical disposition source must be an object.");
                foreach (var field in typeof(PhysicalItemDispositionSourceFactSaveData).GetFields())
                {
                    var expected = field.FieldType == typeof(string)
                        ? Newtonsoft.Json.Linq.JTokenType.String : Newtonsoft.Json.Linq.JTokenType.Integer;
                    if (source[field.Name]?.Type != expected)
                        throw new InvalidOperationException("Physical disposition source field missing or invalid: " + field.Name);
                }
            }
        }
    }

    private static void ValidateRelocationShape(string json)
    {
        var root = Newtonsoft.Json.Linq.JObject.Parse(json);
        if (root["physicalItemRelocations"] is not Newtonsoft.Json.Linq.JArray rows)
            throw new InvalidOperationException("Physical item current schema requires physicalItemRelocations.");
        foreach (var row in rows)
        {
            if (row is not Newtonsoft.Json.Linq.JObject data)
                throw new InvalidOperationException("Physical relocation must be an object.");
            foreach (var field in typeof(PhysicalItemRelocationSaveData).GetFields())
            {
                var token = data[field.Name];
                var expected = field.FieldType == typeof(string)
                    ? Newtonsoft.Json.Linq.JTokenType.String : Newtonsoft.Json.Linq.JTokenType.Integer;
                if (token == null || token.Type != expected)
                    throw new InvalidOperationException("Physical relocation field missing or invalid: " + field.Name);
            }
        }
    }

    private static bool HasTopLevelProperty(string json, string propertyName)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(propertyName))
            return false;
        int depth = 0;
        for (int index = 0; index < json.Length; index++)
        {
            char character = json[index];
            if (character == '{' || character == '[')
            {
                depth++;
                continue;
            }
            if (character == '}' || character == ']')
            {
                depth--;
                continue;
            }
            if (character != '"')
                continue;

            int start = ++index;
            bool escaped = false;
            while (index < json.Length)
            {
                char current = json[index];
                if (!escaped && current == '"')
                    break;
                escaped = !escaped && current == '\\';
                if (current != '\\')
                    escaped = false;
                index++;
            }
            if (depth != 1
                || index >= json.Length
                || index - start != propertyName.Length
                || string.CompareOrdinal(
                    json,
                    start,
                    propertyName,
                    0,
                    propertyName.Length) != 0)
            {
                continue;
            }
            int separator = index + 1;
            while (separator < json.Length
                   && char.IsWhiteSpace(json[separator]))
            {
                separator++;
            }
            if (separator < json.Length && json[separator] == ':')
                return true;
        }
        return false;
    }
}
