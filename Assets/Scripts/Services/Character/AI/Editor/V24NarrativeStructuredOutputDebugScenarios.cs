#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public static class V24NarrativeStructuredOutputDebugScenarios
{
    [MenuItem("DungeonStory/Debug/V24/Run Narrative Structured Output Scenarios")]
    public static void RunFromMenu()
    {
        if (!RunAll(logSuccess: true))
        {
            Debug.LogError("V24 narrative structured output scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        Run("nine static schemas", VerifyStaticSchemas, errors);
        Run("schema lookup allocation", VerifySchemaLookupAllocation, errors);
        Run("native Ollama request format", VerifyNativeOllamaRequest, errors);
        Run("deterministic request references", VerifyDeterministicReferences, errors);
        Run("quality gate hard and soft pass", VerifyQualityGate, errors);
        Run("hidden latent trait isolation", VerifyHiddenLatentTraitIsolation, errors);

        foreach (string error in errors)
        {
            Debug.LogError("V24 narrative scenario: " + error);
        }
        if (errors.Count == 0 && logSuccess)
        {
            Debug.Log("V24 narrative structured output scenarios passed (6/6).");
        }
        return errors.Count == 0;
    }

    private static void VerifyStaticSchemas()
    {
        Require(LlmStaticSchemaCatalog.All.Count == 10, "V25 requires ten static schemas including multi-perspective output.");
        Require(LlmStaticSchemaCatalog.All.Select(value => value.ProfileId)
            .Distinct(StringComparer.Ordinal).Count() == 10, "Schema profile ids must be unique.");
        Require(LlmStaticSchemaCatalog.All.Select(value => value.Hash)
            .Distinct(StringComparer.Ordinal).Count() == 10, "Schema hashes must be profile-specific.");
        foreach (LlmStaticSchemaDefinition schema in LlmStaticSchemaCatalog.All)
        {
            Require(schema.Version == 1, schema.ProfileId + " schema version drifted.");
            Require(schema.Utf8Bytes.SequenceEqual(Encoding.UTF8.GetBytes(schema.Json)),
                schema.ProfileId + " cached UTF-8 differs from static JSON.");
            Require(schema.Json.IndexOf("\"F01\"", StringComparison.Ordinal) < 0,
                schema.ProfileId + " contains a request-specific fact enum.");
            Require(schema.Json.IndexOf("\"M01\"", StringComparison.Ordinal) < 0,
                schema.ProfileId + " contains a request-specific motif enum.");
            for (int iteration = 0; iteration < 10000; iteration++)
            {
                Require(ReferenceEquals(schema, LlmStaticSchemaCatalog.Require(schema.ProfileId)),
                    schema.ProfileId + " was rebuilt at runtime.");
                Require(schema.Hash == LlmStaticSchemaCatalog.Require(schema.ProfileId).Hash,
                    schema.ProfileId + " hash changed across character contexts.");
            }
        }
    }

    private static void VerifySchemaLookupAllocation()
    {
        LlmStaticSchemaCatalog.Require(LocalLlmRequestProfiles.Persona.Id);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 10000; index++)
        {
            LlmStaticSchemaCatalog.Require(LocalLlmRequestProfiles.Persona.Id);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Require(allocated == 0, $"Warm schema lookup allocated {allocated} bytes.");
    }

    private static void VerifyNativeOllamaRequest()
    {
        OllamaStructuredChatBackend backend = new OllamaStructuredChatBackend();
        LlmStaticSchemaDefinition schema = LlmStaticSchemaCatalog.Require(
            LocalLlmRequestProfiles.Persona.Id);
        NarrativeRequestContext context = NarrativeCultureStyleCatalog.Create(
            LocalLlmRequestProfiles.Persona.Id,
            "orc",
            true,
            true);
        context.AddFact("fact:secret:stable-id", "Expressed trait: patient smith", 100);
        using UnityWebRequest request = backend.BuildRequest(
            "http://localhost:11434/v1/chat/completions",
            "llama3.1",
            LocalLlmRequestProfiles.Persona,
            schema,
            context.AppendToPrompt("prompt"));
        Require(request.url == "http://localhost:11434/api/chat",
            "Legacy endpoint was not normalized to native /api/chat.");
        string body = Encoding.UTF8.GetString(request.uploadHandler.data);
        Require(body.Contains("\"format\":" + schema.Json),
            "Request body does not embed the static schema in format.");
        Require(body.Contains("\"stream\":false"), "Ollama request must be non-streaming.");
        Require(!body.Contains("fact:secret:stable-id"),
            "A stable character fact id leaked into the model request body.");
        Require(body.Contains("F01 = Expressed trait: patient smith"),
            "The model request did not retain the request-local fact alias and label.");
    }

    private static void VerifyDeterministicReferences()
    {
        NarrativeRequestContext first = NarrativeCultureStyleCatalog.Create(
            LocalLlmRequestProfiles.CharacterRecord.Id,
            "orc",
            true,
            true);
        first.AddFact("fact:z", "later", 10);
        first.AddFact("fact:a", "first", 10);
        string firstPrompt = first.AppendToPrompt("base");

        NarrativeRequestContext second = NarrativeCultureStyleCatalog.Create(
            LocalLlmRequestProfiles.CharacterRecord.Id,
            "orc",
            true,
            true);
        second.AddFact("fact:a", "first", 10);
        second.AddFact("fact:z", "later", 10);
        string secondPrompt = second.AppendToPrompt("base");
        Require(firstPrompt == secondPrompt,
            "Reference assignment depends on insertion order instead of stable ids.");
        Require(firstPrompt.Contains("F01|fact:a|first"), "Sorted F01 reference is missing.");
    }

    private static void VerifyQualityGate()
    {
        NarrativeRequestContext context = NarrativeCultureStyleCatalog.Create(
            LocalLlmRequestProfiles.CharacterRecord.Id,
            "orc",
            true,
            true);
        context.AddFact("fact:test:scar", "Expressed trait: scarred veteran", 100);
        string prompt = context.AppendToPrompt("write");
        NarrativeTextQualityGate gate = new NarrativeTextQualityGate();

        NarrativeQualityResult invalid = gate.Evaluate(
            LocalLlmRequestProfiles.CharacterRecord,
            prompt,
            "{\"line\":\"rough\",\"usedMotifIds\":[\"M99\"],\"usedCharacterFactIds\":[\"F99\"]}");
        Require(invalid.Verdict == NarrativeQualityVerdict.HardReject,
            "Unknown F99/M99 references were not hard rejected.");

        NarrativeQualityResult soft = gate.Evaluate(
            LocalLlmRequestProfiles.CharacterRecord,
            prompt,
            "{\"line\":\"rough but grounded\",\"usedMotifIds\":[\"M01\"],\"usedCharacterFactIds\":[\"F01\"]}");
        Require(soft.Verdict == NarrativeQualityVerdict.SoftPass,
            "A valid but rough response did not soft-pass immediately.");

        NarrativeQualityResult strong = gate.Evaluate(
            LocalLlmRequestProfiles.CharacterRecord,
            prompt,
            "{\"line\":\"grounded culture line\",\"usedMotifIds\":[\"M01\",\"M02\"],\"usedCharacterFactIds\":[\"F01\"]}");
        Require(strong.Verdict == NarrativeQualityVerdict.StrongPass,
            "Two grounded motifs and one fact did not strong-pass.");
    }

    private static void VerifyHiddenLatentTraitIsolation()
    {
        CharacterId id = new CharacterId("character:v24:test");
        CharacterNarrativeSnapshot snapshot = new CharacterNarrativeSnapshot();
        SetProperty(snapshot, nameof(CharacterNarrativeSnapshot.CharacterId), id);
        SetProperty(snapshot, nameof(CharacterNarrativeSnapshot.BackgroundId),
            new CharacterBackgroundId(string.Empty));
        SetProperty(snapshot, nameof(CharacterNarrativeSnapshot.CultureId),
            new SpeciesCultureId(string.Empty));
        SetProperty(snapshot, nameof(CharacterNarrativeSnapshot.ActiveAmbitionId),
            new CharacterAmbitionId(string.Empty));
        SetProperty(snapshot, nameof(CharacterNarrativeSnapshot.LatentHeritableTraitIds),
            new[] { "heritable:hidden" });
        SetProperty(snapshot, nameof(CharacterNarrativeSnapshot.VisibleLatentHeritableTraitIds),
            new[] { "heritable:visible" });
        SetProperty(snapshot, nameof(CharacterNarrativeSnapshot.RecentEvents),
            Array.Empty<CharacterNarrativeEventSaveData>());

        NarrativeRequestContext context = NarrativeCultureStyleCatalog.Create(
            LocalLlmRequestProfiles.Persona.Id,
            "orc",
            true,
            true);
        NarrativeRequestContextBuilder.AddAuthoritativeFacts(
            context,
            id,
            new NarrativeQueryStub(snapshot),
            null,
            null);
        string prompt = context.AppendToPrompt("write");
        Require(prompt.Contains("heritable:visible"), "Analyzed latent trait was not projected.");
        Require(!prompt.Contains("heritable:hidden"), "Unrevealed latent trait leaked into the prompt.");
    }

    private static void SetProperty(object target, string name, object value)
    {
        PropertyInfo property = target.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        property?.SetValue(target, value);
    }

    private static void Run(string label, Action scenario, ICollection<string> errors)
    {
        try
        {
            scenario();
        }
        catch (Exception exception)
        {
            errors.Add(label + ": " + exception);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class NarrativeQueryStub : ICharacterNarrativeQuery
    {
        private readonly CharacterNarrativeSnapshot snapshot;

        public NarrativeQueryStub(CharacterNarrativeSnapshot snapshot) =>
            this.snapshot = snapshot;

        public int Version => 1;
        public IReadOnlyCollection<CharacterNarrativeSnapshot> All =>
            new[] { snapshot };

        public bool TryGet(CharacterId characterId, out CharacterNarrativeSnapshot value)
        {
            value = snapshot;
            return snapshot != null && snapshot.CharacterId.Equals(characterId);
        }

        public bool CanPerformPractice(
            CharacterId characterId,
            string practiceId,
            int absoluteDay,
            out int nextAllowedAbsoluteDay)
        {
            nextAllowedAbsoluteDay = absoluteDay;
            return true;
        }

        public bool TryPreviewAmbitionProgress(
            CharacterId characterId,
            int amount,
            out AmbitionProgressPreview preview)
        {
            preview = default;
            return false;
        }
    }
}
#endif
