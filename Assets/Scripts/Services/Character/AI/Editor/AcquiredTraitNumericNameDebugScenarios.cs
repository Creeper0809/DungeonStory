#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

// Controlled text and transport fixture. It does not invoke a model.
public static class AcquiredTraitNumericNameDebugScenarios
{
    public static void VerifyForBatch()
    {
        foreach (string name in new[] { "세나 1", "세나 １", "검증(별). ３" })
        {
            var accepted = Response(name + "은 경험을 기억한다.");
            Require(accepted.Validate(name, out _), "Exact authoritative name was rejected: " + name);
            Require(!accepted.Validate(out _), "Default validation accepted numeric prose without authority.");
            Require(!Response(name + "은 회복 20을 얻는다.").Validate(name, out _),
                "A number outside the authoritative name was accepted.");
            Require(!Response(name + "은 회복 20%를 얻는다.").Validate(name, out _),
                "A mechanical percentage was accepted.");
            Require(!Response("다른 인물 9는 경험을 기억한다.").Validate(name, out _),
                "A different numeric name was accepted.");
            Require(!Response(name + "0은 경험을 기억한다.").Validate(name, out _),
                "A numeric suffix attached to the name was accepted.");
            Require(!Response("가" + name + "은 경험을 기억한다.").Validate(name, out _),
                "A letter prefix attached to the name was accepted.");
            VerifyBackendPattern(name);
        }

        var invalid = Response("세나 1은 경험을 기억한다.");
        invalid.positiveModuleIds = null;
        Require(!invalid.Validate("세나 1", out _), "Missing module field was accepted.");
        invalid = Response("세나 1은 경험을 기억한다.");
        invalid.narrativeFlavor = " " + invalid.narrativeFlavor;
        Require(!invalid.Validate("세나 1", out _), "Padded narrative was accepted.");
        invalid = Response(new string('가', 181));
        Require(!invalid.Validate("세나 1", out _), "Overlong narrative was accepted.");
        invalid = Response("세나 1은 경험을 기억한다.");
        invalid.displayName = new string('가', 33);
        Require(!invalid.Validate("세나 1", out _), "Overlong display name was accepted.");
        string extra = JsonConvert.SerializeObject(Response("세나 1은 경험을 기억한다."));
        JObject extraObject = JObject.Parse(extra);
        extraObject["unexpected"] = "value";
        Require(!NarrativeExactKeyContract.TryValidateProfileResponse(
                LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id,
                extraObject.ToString(Formatting.None), out _, out _),
            "The existing exact-key gate accepted an extra field.");
        VerifyFinalPresentationAndPersistence();
    }

    private static void VerifyFinalPresentationAndPersistence()
    {
        const string path = "Artifacts/QA/R80PrototypePolish-20261028-r1/soak-polish-r5/unity-evidence/persistent-data/Saves/r80-extended-soak-checkpoint-1.json";
        byte[] bytes = File.ReadAllBytes(path);
        using (var hash = SHA256.Create())
            Require(BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant()
                == "18faaa124a28558b56740c742db3606f2d7a3960dd63bd4b0bb0c1a788493425", "Original failed checkpoint changed.");
        JObject save = JObject.Parse(Encoding.UTF8.GetString(bytes));
        var section = save["sections"].Single(value => (string)value["sectionId"] == "characters.world");
        JArray actors = (JArray)JObject.Parse((string)section["payloadJson"])["actors"];
        var actor = actors.Single(value => (string)value["displayName"] == "레오 2");
        var pending = actor["acquiredTraits"]["pendingRequests"].Single()
            .ToObject<CharacterAcquiredTraitPendingRequestState>();
        var original = actors.Single(value => (string)value["displayName"] == "Yuna")
            ["acquiredTraits"]["instances"].First(value => (int)value["formulaVersion"] > 0)
            .ToObject<CharacterAcquiredTraitInstanceState>();
        var instance = original.Clone();
        instance.acceptedRevision = pending.registeredRevision + 1;
        instance.manifestationMilestone = pending.manifestationMilestone;
        instance.originatingRequestId = pending.requestId;
        instance.originatingRequestKey = pending.requestKey;
        instance.evidenceFactIds = pending.evidenceFactIds.ToList();
        instance.presentationOrigin = CharacterAcquiredTraitPresentationOriginState.Capture(pending);
        var settings = AssetDatabase.FindAssets("t:CharacterAcquiredTraitSettingsSO")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CharacterAcquiredTraitSettingsSO>)
            .Single(value => value != null && MatchesRequest(instance, value));
        Require(instance.presentationOrigin.RestorePublicContext(instance).Entities
            .Single(value => value.EntityId == pending.targetPersistentId).DisplayName == "레오 2",
            "Final presentation lost the original public subject.");
        instance.narrativeFlavor = "레오 2는 경험을 기억한다.";
        Require(FinalPresentationValid(instance, "레오 2"), "Final formula validator rejected the exact numeric name.");
        Require(!FinalPresentationValid(instance, null), "Legacy final validation accepted unbound numeric prose.");
        foreach (string bad in new[] { "다른 인물 9는 경험을 기억한다.", "레오 20은 경험을 기억한다.", "레오 2는 회복 20을 얻는다.", "레오 2는 회복 20%를 얻는다." })
        {
            instance.narrativeFlavor = bad;
            Require(!FinalPresentationValid(instance, "레오 2"), "Final validation accepted unrelated numeric prose.");
        }
        instance.narrativeFlavor = "레오 2는 경험을 기억한다.";
        var restored = JsonConvert.DeserializeObject<CharacterAcquiredTraitInstanceState>(JsonConvert.SerializeObject(instance));
        restored.presentationOrigin.RequireRequestIdentity(restored, settings);
        Require(FinalPresentationValid(restored, "레오 2"), "Persisted presentation no longer validates.");
        var inlineRestored = UnityEngine.JsonUtility.FromJson<CharacterAcquiredTraitInstanceState>(UnityEngine.JsonUtility.ToJson(instance));
        inlineRestored.presentationOrigin.RequireRequestIdentity(inlineRestored, settings);
        Require(inlineRestored.presentationOrigin.RestorePublicContext(inlineRestored).Entities
            .Single(value => value.EntityId == pending.targetPersistentId).DisplayName == "레오 2",
            "An actor rename would rebind the saved original name.");
        var cloned = instance.Clone();
        cloned.presentationOrigin.publicContext.entities.Single(value => value.entityId == pending.targetPersistentId).displayName = "다른 이름 9";
        Require(!MatchesRequest(cloned, settings) && MatchesRequest(instance, settings), "Origin tampering or clone aliasing was accepted.");
        cloned = instance.Clone(); cloned.presentationOrigin.version = 2;
        Require(!MatchesRequest(cloned, settings), "Unknown origin version accepted.");
        cloned = instance.Clone(); cloned.presentationOrigin.ownerBinding.persistentId = "other-owner";
        Require(!MatchesRequest(cloned, settings), "Origin was rebound to another owner.");
        cloned = instance.Clone(); cloned.presentationOrigin.evidenceFactIds.Add("unoffered-fact");
        Require(!MatchesRequest(cloned, settings), "Unbound original evidence accepted.");
        Require(!new CharacterAcquiredTraitPresentationOriginState().HasData,
            "An empty inline legacy origin is no longer absent.");
        VerifyPublicationShapeCompatibility(save, instance.presentationOrigin);
    }

    private static void VerifyPublicationShapeCompatibility(JObject save, CharacterAcquiredTraitPresentationOriginState origin)
    {
        string legacy = (string)save["sections"].Single(value =>
            (string)value["sectionId"] == CharacterTraitInferencePublicationSaveSection.Id)["payloadJson"];
        CharacterTraitInferencePublicationSaveSection.ValidateJson(legacy);
        JObject payload = JObject.Parse(legacy);
        JObject recorded = payload.DescendantsAndSelf().OfType<JObject>()
            .First(value => value.Property("originatingRequestKey") != null);
        foreach (JToken valid in new JToken[] { JValue.CreateNull(),
                     JObject.FromObject(new CharacterAcquiredTraitPresentationOriginState()), JObject.FromObject(origin) })
        {
            recorded["presentationOrigin"] = valid;
            CharacterTraitInferencePublicationSaveSection.ValidateJson(payload.ToString(Formatting.None));
        }
        foreach (JToken invalid in new JToken[] { new JValue(1), JObject.Parse("{\"version\":1}"),
                     JObject.FromObject(new CharacterAcquiredTraitPresentationOriginState { version = 0, registeredRevision = 1 }) })
        {
            recorded["presentationOrigin"] = invalid;
            bool rejected = false;
            try { CharacterTraitInferencePublicationSaveSection.ValidateJson(payload.ToString(Formatting.None)); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Raw publication shape accepted malformed new origin data.");
        }
        recorded["presentationOrigin"] = JValue.CreateNull();
        recorded.Property("displayName").Remove();
        bool originalFieldRejected = false;
        try { CharacterTraitInferencePublicationSaveSection.ValidateJson(payload.ToString(Formatting.None)); }
        catch (InvalidOperationException) { originalFieldRejected = true; }
        Require(originalFieldRejected, "Optional origin compatibility weakened an existing required field.");
    }

    private static bool MatchesRequest(CharacterAcquiredTraitInstanceState instance, CharacterAcquiredTraitSettingsSO settings)
    {
        try { instance.presentationOrigin.RequireRequestIdentity(instance, settings); return true; }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return false; }
    }

    private static bool FinalPresentationValid(CharacterAcquiredTraitInstanceState instance, string name)
    {
        var issues = new List<CharacterAcquiredTraitValidationIssue>();
        var method = typeof(CharacterAcquiredTraitStateValidator).GetMethod("ValidateFormulaEnvelope", BindingFlags.NonPublic | BindingFlags.Static);
        Require(method != null, "Actual final formula validation is unavailable.");
        method.Invoke(null, new object[] { instance.formulaVersion, instance.formulaCatalogSha256,
            instance.calculatedCost, instance.positiveCost, instance.drawbackCredit, instance.drawbackId,
            instance.narrativeBudget, instance.formulaCapabilities, instance.effectOverrides,
            instance.presentationId, instance.mechanicalDescription, instance.displayName,
            instance.narrativeFlavor, CharacterAcquiredTraitPresentationState.Ready, name, issues, "Controlled original envelope" });
        return issues.Count == 0;
    }

    private static CharacterAcquiredTraitModuleSelectionResponseDto Response(string prose) => new()
    {
        selectionId = "selection:trait:qa-name",
        positiveModuleIds = new List<string> { "acquired-trait:module:qa" },
        drawbackModuleIds = new List<string>(),
        evidenceFactIds = new List<string> { "evidence:qa" },
        displayName = "기억의 결",
        narrativeFlavor = prose
    };

    private static void VerifyBackendPattern(string name)
    {
        string header = "character=" + name + "\n"
            + "selectionId=selection:trait:qa-name\n"
            + "- moduleId=acquired-trait:module:qa; polarity=Positive; meaning=경험을 기억한다\n"
            + "evidenceFactIds=evidence:qa\n";
        LlmStaticSchemaDefinition catalog = LlmStaticSchemaCatalog.Require(
            LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id);
        byte[] originalBytes = catalog.Utf8Bytes.ToArray();
        string originalHash = catalog.Hash;
        JObject wire = JObject.Parse(Encoding.UTF8.GetString(
            DungeonStoryHostStructuredChatBackend.BuildRequestPayload("test",
                LocalLlmRequestProfiles.AcquiredTraitModuleSelection, catalog, header)));
        string pattern = (string)wire["response_format"]["json_schema"]["schema"]
            ["properties"]["narrativeFlavor"]["pattern"];
        Regex allowed = new Regex(pattern, RegexOptions.CultureInvariant);
        string Interior(string value)
        {
            string encoded = JsonConvert.ToString(value);
            return encoded.Substring(1, encoded.Length - 2);
        }
        Require(allowed.IsMatch(Interior(name + "은 경험을 기억한다."))
                && !allowed.IsMatch(Interior(name + "은 회복 20을 얻는다."))
                && !allowed.IsMatch(Interior("다른 인물 9는 경험을 기억한다."))
                && originalBytes.SequenceEqual(catalog.Utf8Bytes)
                && originalHash == catalog.Hash,
            "Trait request pattern lost the exact name, admitted unrelated numbers, or mutated the catalog.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
