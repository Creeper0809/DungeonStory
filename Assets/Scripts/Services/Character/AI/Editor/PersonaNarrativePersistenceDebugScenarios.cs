#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

// Controlled DTO and validation fixture. No model response or gameplay save is generated.
public static class PersonaNarrativePersistenceDebugScenarios
{
    public static void VerifyForBatch()
    {
        var original = new DungeonCharacterPersonaNarrativeSaveData
        {
            personaName = "신중한 방문자",
            flavorText = "낯선 곳을 천천히 살피는 방문자다.",
            narrativeTrace = new NarrativeGenerationTrace
            {
                schemaId = "Persona",
                schemaVersion = 2,
                schemaHash = new string('a', 64),
                cultureStyleId = "culture:qa",
                usedMotifIds = new[] { "M01", "M02" },
                usedCharacterFactIds = new[] { "F01", "F02" },
                verdict = NarrativeQualityVerdict.SoftPass,
                retryCount = 1,
                usedFallback = false
            }
        };
        CustomerPersonaRuntime.ValidateSavedNarrative(original, CharacterType.Customer);
        CustomerPersonaRuntime.ValidateSavedNarrative(null, CharacterType.Customer);
        var noTrace = original.Clone();
        noTrace.narrativeTrace = null;
        CustomerPersonaRuntime.ValidateSavedNarrative(noTrace, CharacterType.Customer);
        var atLimit = noTrace.Clone();
        atLimit.personaName = new string('a', 40);
        atLimit.flavorText = new string('b', 180);
        CustomerPersonaRuntime.ValidateSavedNarrative(atLimit, CharacterType.Customer);

        DungeonCharacterPersonaNarrativeSaveData clone = original.Clone();
        Require(!ReferenceEquals(clone.narrativeTrace, original.narrativeTrace)
                && !ReferenceEquals(clone.narrativeTrace.usedMotifIds, original.narrativeTrace.usedMotifIds)
                && !ReferenceEquals(clone.narrativeTrace.usedCharacterFactIds, original.narrativeTrace.usedCharacterFactIds),
            "Persona trace clone shared mutable arrays.");
        clone.narrativeTrace.usedMotifIds[0] = "M99";
        clone.narrativeTrace.usedCharacterFactIds[0] = "F99";
        Require(original.narrativeTrace.usedMotifIds[0] == "M01"
                && original.narrativeTrace.usedCharacterFactIds[0] == "F01",
            "Changing the clone altered the original trace.");

        var source = new DungeonCharacterWorldSaveData
        {
            actors = new List<DungeonCharacterSaveData>
            {
                new DungeonCharacterSaveData { characterType = CharacterType.Customer, personaNarrative = original },
                new DungeonCharacterSaveData { characterType = CharacterType.Customer, personaNarrative = null }
            }
        };
        Type codec = typeof(CharacterWorldSaveService).Assembly.GetType("DungeonCharacterWorldSaveCodec", true);
        MethodInfo serialize = codec.GetMethod("Serialize", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(codec.FullName, "Serialize");
        string json = (string)serialize.Invoke(null, new object[] { source });
        DungeonCharacterWorldSaveData restored = DungeonSaveJson.Deserialize<DungeonCharacterWorldSaveData>(json);
        var loaded = restored.actors[0].personaNarrative;
        Require(loaded != null && loaded.personaName == original.personaName
                && loaded.flavorText == original.flavorText && loaded.narrativeTrace != null
                && loaded.narrativeTrace.schemaId == original.narrativeTrace.schemaId
                && loaded.narrativeTrace.schemaVersion == original.narrativeTrace.schemaVersion
                && loaded.narrativeTrace.schemaHash == original.narrativeTrace.schemaHash
                && loaded.narrativeTrace.cultureStyleId == original.narrativeTrace.cultureStyleId
                && loaded.narrativeTrace.usedMotifIds.SequenceEqual(original.narrativeTrace.usedMotifIds)
                && loaded.narrativeTrace.usedCharacterFactIds.SequenceEqual(original.narrativeTrace.usedCharacterFactIds)
                && loaded.narrativeTrace.verdict == original.narrativeTrace.verdict
                && loaded.narrativeTrace.retryCount == original.narrativeTrace.retryCount
                && loaded.narrativeTrace.usedFallback == original.narrativeTrace.usedFallback
                && restored.actors[1].personaNarrative == null,
            "Character world codec did not preserve Persona text, trace, or null.");
        CustomerPersonaRuntime.ValidateSavedNarrative(loaded, CharacterType.Customer);
        JObject legacy = JObject.Parse(json);
        ((JObject)legacy["actors"][0]).Remove("personaNarrative");
        DungeonCharacterWorldSaveData missing = DungeonSaveJson.Deserialize<DungeonCharacterWorldSaveData>(legacy.ToString());
        Require(missing.actors[0].personaNarrative == null,
            "A legacy actor without the optional field did not deserialize to null.");

        var invalid = original.Clone();
        invalid.personaName = string.Empty;
        RequireRejected(invalid, CharacterType.Customer, "empty name");
        invalid = original.Clone();
        invalid.personaName = new string('a', 41);
        RequireRejected(invalid, CharacterType.Customer, "overlong name");
        invalid = original.Clone();
        invalid.flavorText = string.Empty;
        RequireRejected(invalid, CharacterType.Customer, "empty description");
        invalid = original.Clone();
        invalid.flavorText = new string('a', 181);
        RequireRejected(invalid, CharacterType.Customer, "overlong description");
        RequireRejected(original, CharacterType.NPC, "non-customer");
        invalid = original.Clone();
        invalid.narrativeTrace.usedMotifIds = null;
        RequireRejected(invalid, CharacterType.Customer, "missing motif array");
        invalid = original.Clone();
        invalid.narrativeTrace.usedCharacterFactIds = null;
        RequireRejected(invalid, CharacterType.Customer, "missing fact array");
        invalid = original.Clone();
        invalid.narrativeTrace.verdict = NarrativeQualityVerdict.HardReject;
        RequireRejected(invalid, CharacterType.Customer, "rejected trace");

        GameObject holder = new GameObject("PersonaIncompleteCaptureFixture");
        try
        {
            CustomerPersonaRuntime runtime = holder.AddComponent<CustomerPersonaRuntime>();
            Require(runtime.CaptureGeneratedNarrative() == null,
                "An incomplete Persona was captured as generated narrative.");
        }
        finally { Object.DestroyImmediate(holder); }
    }

    private static void RequireRejected(DungeonCharacterPersonaNarrativeSaveData source,
        CharacterType characterType, string label)
    {
        bool rejected = false;
        try { CustomerPersonaRuntime.ValidateSavedNarrative(source, characterType); }
        catch (ArgumentException) { rejected = true; }
        Require(rejected, "Persona validation accepted " + label + ".");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
