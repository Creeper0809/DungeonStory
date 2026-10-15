using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivitySaveSection :
    DungeonStrictJsonSaveSection<
        CaptivitySaveData,
        CaptivityRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "captivity";

    private static readonly string[] Dependencies =
    {
        "world.facilities",
        "characters.world",
        "items.physical",
        "combat.equipment",
        "combat.body-health"
    };

    private readonly ICaptivityPersistence persistence;

    public CaptivitySaveSection(ICaptivityPersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public override string SectionId => Id;
    public override int SectionVersion => CaptivitySaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override CaptivitySaveData CapturePayload() =>
        persistence.Capture();

    protected override void ValidateRawPayload(string json)
    {
        var root = JObject.Parse(json);
        if (root["version"]?.Type != JTokenType.Integer
            || root["version"].Value<int>() != CaptivitySaveData.CurrentVersion
            || root["captives"] is not JArray captives)
            throw new InvalidOperationException("captivity-v11-json-shape-invalid");
        foreach (var captive in captives)
        {
            if (captive is not JObject value || value["performerMilestones"] is not JArray observations)
                throw new InvalidOperationException("captivity-milestone-observations-required");
            if (value["interrogationTerminal"] is not JObject interrogation
                || interrogation["codexPublicationCompleted"]?.Type != JTokenType.Boolean
                || interrogation["factPublicationCompleted"]?.Type != JTokenType.Boolean)
                throw new InvalidOperationException("captivity-interrogation-fact-json-invalid");
            if (value["interactionTerminal"] is not JObject interaction
                || interaction["captiveDisplayName"]?.Type != JTokenType.String
                || interaction["absoluteDay"]?.Type != JTokenType.Integer
                || interaction["publicationKey"]?.Type != JTokenType.String
                || interaction["deliveryPending"]?.Type != JTokenType.Boolean)
                throw new InvalidOperationException("captivity-interaction-fact-json-invalid");
            if (value["rehabilitationTerminal"] is not JObject rehabilitation
                || rehabilitation["publicationKey"]?.Type != JTokenType.String
                || value["releaseTerminal"] is not JObject release
                || release["publicationKey"]?.Type != JTokenType.String
                || release["effectsApplied"]?.Type != JTokenType.Boolean
                || release["decisionPublished"]?.Type != JTokenType.Boolean)
                throw new InvalidOperationException("captivity-transition-fact-json-invalid");
            if (value["escapeObservation"] is not JObject escape
                || escape["publicationKey"]?.Type != JTokenType.String
                || escape["pending"]?.Type != JTokenType.Boolean || escape["observersPending"]?.Type != JTokenType.Boolean
                || escape["fact"] is not JObject escapeFact)
                throw new InvalidOperationException("captivity-escape-observation-json-invalid");
            foreach (var field in typeof(CaptiveEscapedFactData).GetFields())
            {
                JToken token = escapeFact[field.Name];
                bool valid = field.FieldType == typeof(string) ? token?.Type == JTokenType.String
                    : field.FieldType == typeof(float) ? token?.Type is JTokenType.Float or JTokenType.Integer
                    : token?.Type == JTokenType.Integer;
                if (!valid) throw new InvalidOperationException("captivity-escape-fact-field-invalid:" + field.Name);
            }
            if (value["ransomObservation"] is not JObject ransom
                || ransom["publicationKey"]?.Type != JTokenType.String || ransom["pending"]?.Type != JTokenType.Boolean
                || ransom["fact"] is not JObject ransomFact)
                throw new InvalidOperationException("captivity-ransom-observation-json-invalid");
            foreach (var field in typeof(CaptiveRansomAcceptedFactData).GetFields())
            {
                JToken token = ransomFact[field.Name];
                bool valid = field.FieldType == typeof(string) ? token?.Type == JTokenType.String
                    : field.FieldType == typeof(float) ? token?.Type is JTokenType.Float or JTokenType.Integer
                    : token?.Type == JTokenType.Integer;
                if (!valid) throw new InvalidOperationException("captivity-ransom-fact-field-invalid:" + field.Name);
            }
            foreach (var entry in observations)
            {
                if (entry is not JObject observation || observation["publicationKey"]?.Type != JTokenType.String
                    || observation["pending"]?.Type != JTokenType.Boolean
                    || observation["observersPending"]?.Type != JTokenType.Boolean
                    || observation["fact"] is not JObject fact)
                    throw new InvalidOperationException("captivity-milestone-observation-json-invalid");
                foreach (var field in typeof(CaptivePerformerMilestoneFactData).GetFields())
                {
                    JToken token = fact[field.Name];
                    bool valid = field.FieldType == typeof(string) ? token?.Type == JTokenType.String
                        : field.FieldType == typeof(float) ? token?.Type is JTokenType.Float or JTokenType.Integer
                        : token?.Type == JTokenType.Integer;
                    if (!valid) throw new InvalidOperationException("captivity-milestone-fact-field-invalid:" + field.Name);
                }
            }
        }
    }

    protected override void ValidateParsedPayload(CaptivitySaveData payload)
    {
        // Preflight has no incoming Unity world yet. Keep every DTO check here;
        // BuildRestoreCandidate validates all incoming references in StageRestore
        // after its declared facility/character dependencies have staged.
        DungeonGameRestoreReport report = new();
        CaptivitySaveValidation.Validate(payload, report);
        if (!report.Success)
            throw new InvalidOperationException("Captivity payload is invalid: "
                + string.Join(" | ", report.Errors));
    }

    protected override void NormalizeRestorePayload(
        CaptivitySaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload?.captives == null)
        {
            return;
        }

        bool captiveIdsChanged = false;
        for (int index = 0; index < payload.captives.Count; index++)
        {
            CaptiveState captive = payload.captives[index];
            if (captive == null)
            {
                continue;
            }

            string path = $"captives[{index}]";
            string previousCaptiveId = captive.captiveId;
            captive.captiveId = NormalizeV18CharacterReference(
                previousCaptiveId, report, path + ".captiveId");
            captiveIdsChanged |= !string.Equals(
                previousCaptiveId,
                captive.captiveId,
                StringComparison.Ordinal);
            captive.reservedCarrierId = NormalizeV18CharacterReference(
                captive.reservedCarrierId, report, path + ".reservedCarrierId");
            captive.reservedWardenId = NormalizeV18CharacterReference(
                captive.reservedWardenId, report, path + ".reservedWardenId");
            if (captive.status == CaptivityStatus.Minion)
            {
                CaptivityStateTransitionRules.ClearCaptiveOnlyState(captive);
            }
        }
        if (captiveIdsChanged)
        {
            payload.captives.Sort((left, right) => string.CompareOrdinal(
                left?.captiveId,
                right?.captiveId));
        }
    }

    protected override CaptivityRestoreCandidate BuildRestoreCandidate(
        CaptivitySaveData payload) =>
        persistence.BuildRestore(payload);

    protected override void PublishRestoreCandidate(
        CaptivityRestoreCandidate candidate) =>
        persistence.PublishRestoreCandidate(candidate);
}
