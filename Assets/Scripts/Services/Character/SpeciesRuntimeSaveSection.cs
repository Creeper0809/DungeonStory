using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed class SpeciesRuntimeSaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "character.species-runtime";

    private readonly ICharacterSpeciesPersistence persistence;

    public SpeciesRuntimeSaveSection(ICharacterSpeciesPersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public string SectionId => Id;
    public int SectionVersion => CharacterSpeciesRuntimeSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public IReadOnlyList<string> DependsOn =>
        new[] { CharacterWorldSaveSection.Id };

    public string Capture() => JsonUtility.ToJson(persistence.Capture());

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        RequireVersion(sectionVersion);
        persistence.BuildRestore(Parse(payloadJson, report));
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
        CharacterSpeciesRestoreCandidate candidate =
            persistence.BuildRestore(Parse(payloadJson, report));
        return Stage(candidate);
    }

    private IDungeonSaveRestoreStage Stage(
        CharacterSpeciesRestoreCandidate candidate) =>
        new DungeonDelegateSaveRestoreStage(
            SectionId,
            _ => persistence.Restore(candidate));

    private void RequireVersion(int sectionVersion)
    {
        if (sectionVersion != SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {SectionId} section version {sectionVersion}; expected {SectionVersion}.");
        }
    }

    private CharacterSpeciesRuntimeSaveData Parse(
        string payloadJson,
        DungeonGameRestoreReport report)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new InvalidOperationException(
                $"{SectionId} payload is empty.");
        }
        try
        {
            ValidateRawPayload(payloadJson);
            CharacterSpeciesRuntimeSaveData payload =
                JsonUtility.FromJson<CharacterSpeciesRuntimeSaveData>(payloadJson)
                ?? throw new InvalidOperationException(
                    $"{SectionId} payload deserialized to null.");
            V18SurvivalEnvironmentCharacterReferenceRestoreNormalizer.Normalize(
                payload,
                (value, path) =>
                    V18TypedCharacterReferenceRestoreNormalizer
                        .RewriteLegacyReference(
                            value,
                            report,
                            SectionId,
                            path));
            return payload;
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"{SectionId} payload JSON is invalid: {exception.Message}",
                exception);
        }
    }

    internal static void ValidateRawPayload(string json)
    {
        var root = JObject.Parse(json);
        if (root["version"]?.Type != JTokenType.Integer
            || root.Value<int>("version") != CharacterSpeciesRuntimeSaveData.CurrentVersion
            || root["characters"] is not JArray characters || root["pendingObservations"] is not JArray observations)
            throw new InvalidOperationException("species-current-save-fields-missing");
        foreach (var token in characters)
            if (token is not JObject row || row["rechargeObservationCompleted"]?.Type != JTokenType.Boolean)
                throw new InvalidOperationException("species-recharge-completion-field-missing");
        foreach (var token in observations)
        {
            if (token is not JObject row || row["actionType"]?.Type != JTokenType.Integer
                || row["publicationKey"]?.Type != JTokenType.String
                || row["rollbackBlocked"]?.Type != JTokenType.Boolean || row["rollbackFailure"]?.Type != JTokenType.String)
                throw new InvalidOperationException("species-observation-save-fields-missing");
            int kind = row.Value<int>("actionType");
            string member = kind == 0 ? "golem" : kind == 1 ? "incident" : kind == 2 ? "harpy" : null;
            if (member == null || row[member] is not JObject fact)
                throw new InvalidOperationException("species-observation-active-fact-missing");
            Require(fact, JTokenType.String, "operationId characterId characterDisplayName speciesId speciesDisplayName");
            Require(fact, JTokenType.Integer, "revision absoluteDay x y");
            if (kind == 0)
            {
                Require(fact, JTokenType.String, "kind physicalEvidence workSourceId workTypeId");
                Require(fact, JTokenType.Float, "chargeBefore chargeAfter integrityBefore integrityAfter healthBefore healthAfter completedWork requestedDamage appliedDamage");
                if (fact["wornNodes"] is not JArray nodes)
                    throw new InvalidOperationException("species-wear-nodes-missing");
                foreach (var node in nodes)
                    if (node.Type != JTokenType.String) throw new InvalidOperationException("species-wear-node-invalid");
            }
            else if (kind == 1) Require(fact, JTokenType.String, "incidentId summary");
            else
            {
                Require(fact, JTokenType.String, "sourceStackId destinationStackId itemId itemInstanceId itemDisplayName reasonCode");
                Require(fact, JTokenType.Integer, "quantity sourceX sourceY destinationX destinationY massGrams physicalRevision");
            }
        }
        static void Require(JObject value, JTokenType type, string names)
        {
            foreach (string name in names.Split(' '))
                if (value[name]?.Type != type && !(type == JTokenType.Float && value[name]?.Type == JTokenType.Integer))
                    throw new InvalidOperationException("species-fact-field-missing:" + name);
        }
    }
}
