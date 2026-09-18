using System;
using System.Linq;
using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

[Serializable]
public sealed class CustomerPersonaData
{
    public string traitName;
    public string flavorText;
    [Range(0.25f, 2f)] public float selfCareMultiplier = 1f;
    [Range(0.25f, 2f)] public float curiosityMultiplier = 1f;
    [Range(0.25f, 2f)] public float shoppingMultiplier = 1f;
    [Range(0.25f, 2f)] public float patienceMultiplier = 1f;
    [Range(0.25f, 2f)] public float hungerCurveMultiplier = 1f;
    [Range(0.25f, 2f)] public float funCurveMultiplier = 1f;
    [Range(0.25f, 2f)] public float moodCurveMultiplier = 1f;
    public string[] preferredFacilityTags = Array.Empty<string>();
    public NarrativeGenerationTrace narrativeTrace;

    public void Clamp()
    {
        selfCareMultiplier = ClampMultiplier(selfCareMultiplier);
        curiosityMultiplier = ClampMultiplier(curiosityMultiplier);
        shoppingMultiplier = ClampMultiplier(shoppingMultiplier);
        patienceMultiplier = ClampMultiplier(patienceMultiplier);
        hungerCurveMultiplier = ClampMultiplier(hungerCurveMultiplier);
        funCurveMultiplier = ClampMultiplier(funCurveMultiplier);
        moodCurveMultiplier = ClampMultiplier(moodCurveMultiplier);
        preferredFacilityTags ??= Array.Empty<string>();
    }

    private static float ClampMultiplier(float value)
    {
        return Mathf.Clamp(value, 0.25f, 2f);
    }
}

public static class CustomerPersonaPromptBuilder
{
    public static NarrativePublicContextMaterial BuildPublicMaterial(
        CharacterActor actor)
    {
        return NarrativeRequestContextBuilder.BuildPublicMaterialForActorWithCurrentNeeds(
            LocalLlmRequestProfiles.Persona.Id,
            actor,
            requireCharacterFact: false,
            requireMotif: false);
    }

    public static string Build(CharacterActor actor) =>
        BuildEnvelope(actor, BuildPublicMaterial(actor)).Prompt;

    public static NarrativePublicPromptEnvelope BuildEnvelope(CharacterActor actor) =>
        BuildEnvelope(actor, BuildPublicMaterial(actor));

    public static NarrativePublicPromptEnvelope BuildEnvelope(
        CharacterActor actor,
        NarrativePublicContextMaterial publicMaterial)
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        if (actor.Identity?.Data == null)
            throw new InvalidOperationException(
                "Persona prompt requires authoritative character data.");
        if (publicMaterial == null)
            throw new ArgumentNullException(nameof(publicMaterial));
        string subjectId = actor.Identity.PersistentId?.Trim() ?? string.Empty;
        if (!string.Equals(
                publicMaterial.ProfileId,
                LocalLlmRequestProfiles.Persona.Id,
                StringComparison.Ordinal)
            || publicMaterial.SubjectKind != NarrativePublicSubjectKind.Character
            || !string.Equals(publicMaterial.SubjectId, subjectId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Persona prompt material does not match its target character.");
        }

        CharacterSO data = actor.Identity.Data;
        StringBuilder builder = new();
        builder.AppendLine("Generate a compact customer persona for DungeonStory.");
        builder.AppendLine("Return exactly one JSON object and no other text.");
        builder.AppendLine("Use exactly these keys: personaName, flavorText.");
        builder.AppendLine("Do not output multipliers, numeric mechanics, facility tags, or any extra key. Existing C# authored mechanics remain authoritative.");
        builder.AppendLine("Required JSON shape:");
        builder.AppendLine("{\"personaName\":\"string\",\"flavorText\":\"string\"}");
        builder.AppendLine($"name: {data.characterName}");
        builder.AppendLine($"species: {data.SpeciesTag}");
        builder.AppendLine($"role: {actor.Role}");
        return NarrativePublicPromptEnvelope.Create(builder.ToString(), publicMaterial);
    }
}

[DisallowMultipleComponent]
[DrawWithUnity]
public sealed class CustomerPersonaRuntime : SerializedMonoBehaviour
{
    private static readonly ILocalLlmRuntimeProvider FallbackLlmRuntimeProvider =
        new CustomerPersonaMissingLlmRuntimeProvider();

    [SerializeField, ReadOnly] private CharacterActor actor;
    [SerializeField] private CustomerPersonaData persona = new CustomerPersonaData();
    [SerializeField, ReadOnly] private bool hasGeneratedPersona;
    [SerializeField, ReadOnly] private bool personaRequestInProgress;
    [SerializeField, ReadOnly] private string lastPrompt;
    [SerializeField, ReadOnly] private string lastError;
    private ILocalLlmRuntimeProvider llmRuntimeProvider;
    private string activeRequestKey = string.Empty;
    private string activeCandidatePacketHash = string.Empty;

    public CustomerPersonaData Persona => persona ??= new CustomerPersonaData();
    public bool HasGeneratedPersona => hasGeneratedPersona;
    public bool PersonaRequestInProgress => personaRequestInProgress;
    public string LastPrompt => lastPrompt;
    public string LastError => lastError;
    public NarrativeInferenceAuditRecord? LastInferenceAudit { get; private set; }

    [Inject]
    public void ConstructCustomerPersonaRuntime(ILocalLlmRuntimeProvider llmRuntimeProvider)
    {
        this.llmRuntimeProvider = llmRuntimeProvider
            ?? throw new ArgumentNullException(nameof(llmRuntimeProvider));
    }

    private void Awake()
    {
        Bind(GetComponent<CharacterActor>());
    }

    public void Bind(CharacterActor owner)
    {
        actor = owner;
        Persona.Clamp();
    }

    public bool RequestPersonaIfNeeded(bool logIfMissingQueue = true)
    {
        if (hasGeneratedPersona || personaRequestInProgress)
        {
            return false;
        }

        if (actor == null)
        {
            actor = GetComponent<CharacterActor>();
        }

        if (actor == null || actor.Identity == null || actor.Identity.Data == null)
        {
            lastError = "Character data is not ready for persona generation.";
            return false;
        }

        if (actor.characterType != CharacterType.Customer)
        {
            return false;
        }

        NarrativePublicContextMaterial publicMaterial =
            CustomerPersonaPromptBuilder.BuildPublicMaterial(actor);
        NarrativePublicPromptEnvelope promptEnvelope =
            CustomerPersonaPromptBuilder.BuildEnvelope(actor, publicMaterial);
        lastPrompt = promptEnvelope.Prompt;
        activeRequestKey = NarrativePublicContextIdentity.Bind(
            "persona:" + actor.Identity.PersistentId,
            publicMaterial.SemanticHash);
        activeCandidatePacketHash = NarrativeInferenceHash.ComputeSha256Utf8(lastPrompt);
        if (!TryGetLlmRuntime(logIfMissingQueue, out ILocalLlmRuntime queue))
        {
            RecordAudit(false, lastError, string.Empty);
            return false;
        }

        personaRequestInProgress = true;
        bool accepted = queue.GeneratePersonaAsync(lastPrompt, OnPersonaResult);
        if (!accepted)
        {
            personaRequestInProgress = false;
            lastError = "Persona request was not accepted by LocalLlmRequestQueue.";
            RecordAudit(false, lastError, string.Empty);
            Debug.Log($"{name}: {lastError}", this);
        }

        return accepted;
    }

    public void ApplyGeneratedPersona(CustomerPersonaData generatedPersona)
    {
        if (!ValidatePersona(generatedPersona, out string error))
        {
            lastError = error;
            Debug.Log($"{name}: Rejected generated persona. {error}", this);
            return;
        }

        persona = generatedPersona;
        persona.Clamp();
        hasGeneratedPersona = true;
        lastError = string.Empty;
    }

    private bool TryGetLlmRuntime(bool logIfMissingQueue, out ILocalLlmRuntime queue)
    {
        ILocalLlmRuntimeProvider provider = llmRuntimeProvider ?? FallbackLlmRuntimeProvider;

        if (provider.TryGetRuntime(out queue))
        {
            return true;
        }

        lastError = $"{nameof(LocalLlmRequestQueue)} is missing.";
        if (logIfMissingQueue)
        {
            Debug.Log($"{name}: {lastError}", this);
        }

        return false;
    }

    public float GetActionMultiplier(AIActionSet actionSet)
    {
        CustomerPersonaData data = Persona;
        if (actionSet != null && actionSet.HasSemanticTag(CharacterAiActionTags.SelfCare))
        {
            return data.selfCareMultiplier;
        }

        if (actionSet != null && actionSet.HasSemanticTag(CharacterAiActionTags.Curiosity))
        {
            return data.curiosityMultiplier;
        }

        if (actionSet != null && actionSet.HasSemanticTag(CharacterAiActionTags.Shopping))
        {
            return data.shoppingMultiplier;
        }

        if (actionSet != null && actionSet.HasSemanticTag(CharacterAiActionTags.Patience))
        {
            return data.patienceMultiplier;
        }

        return 1f;
    }

    public float GetConditionCurveMultiplier(CharacterCondition condition)
    {
        CustomerPersonaData data = Persona;
        return condition switch
        {
            CharacterCondition.HUNGER => data.hungerCurveMultiplier,
            CharacterCondition.FUN => data.funCurveMultiplier,
            CharacterCondition.MOOD => data.moodCurveMultiplier,
            _ => 1f
        };
    }

    public float GetFacilityTagPreference(BuildableObject building)
    {
        if (building == null || building.BuildingData == null)
        {
            return 0.5f;
        }

        string[] preferredTags = Persona.preferredFacilityTags;
        if (preferredTags == null || preferredTags.Length == 0)
        {
            return 0.5f;
        }

        foreach (string tag in preferredTags)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            if (building.HasSemanticTag(tag))
            {
                return 1f;
            }
        }

        return 0.5f;
    }

    private static bool ValidatePersona(CustomerPersonaData candidate, out string error)
    {
        error = string.Empty;
        if (candidate == null)
        {
            error = "Persona payload is null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(candidate.traitName))
        {
            error = "traitName is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(candidate.flavorText))
        {
            error = "flavorText is required.";
            return false;
        }

        if (candidate.preferredFacilityTags == null)
        {
            error = "preferredFacilityTags is required.";
            return false;
        }

        return IsMultiplierValid(candidate.selfCareMultiplier, nameof(candidate.selfCareMultiplier), out error)
            && IsMultiplierValid(candidate.curiosityMultiplier, nameof(candidate.curiosityMultiplier), out error)
            && IsMultiplierValid(candidate.shoppingMultiplier, nameof(candidate.shoppingMultiplier), out error)
            && IsMultiplierValid(candidate.patienceMultiplier, nameof(candidate.patienceMultiplier), out error)
            && IsMultiplierValid(candidate.hungerCurveMultiplier, nameof(candidate.hungerCurveMultiplier), out error)
            && IsMultiplierValid(candidate.funCurveMultiplier, nameof(candidate.funCurveMultiplier), out error)
            && IsMultiplierValid(candidate.moodCurveMultiplier, nameof(candidate.moodCurveMultiplier), out error);
    }

    private static bool IsMultiplierValid(float value, string fieldName, out string error)
    {
        error = string.Empty;
        if (value < 0.25f || value > 2f)
        {
            error = $"{fieldName} must be between 0.25 and 2.0.";
            return false;
        }

        return true;
    }

    private void OnPersonaResult(LocalLlmResult result)
    {
        // Save/restore can destroy a customer while an asynchronous persona
        // request is still completing. The callback is allowed to arrive, but
        // it must not dereference or mutate the retired Unity object.
        if (this == null)
        {
            return;
        }

        personaRequestInProgress = false;
        if (result.IsCancelled)
        {
            lastError = string.Empty;
            RecordAudit(false, "Persona request was cancelled.", string.Empty);
            return;
        }

        if (!result.IsSuccess)
        {
            lastError = $"{result.Status}: {result.Error}";
            RecordAudit(false, lastError, string.Empty);
            Debug.Log($"{name}: Persona request failed: {lastError}", this);
            return;
        }

        if (!LlmJsonResponseParser.TryParse(result.Content, out CustomerPersonaJsonDto dto, out string parseError))
        {
            lastError = parseError;
            RecordAudit(false, parseError, string.Empty);
            Debug.Log($"{name}: Persona JSON rejected: {parseError}", this);
            return;
        }

        CustomerPersonaData generated = dto.ToRuntimeData();
        CustomerPersonaData rules = Persona;
        generated.selfCareMultiplier = rules.selfCareMultiplier;
        generated.curiosityMultiplier = rules.curiosityMultiplier;
        generated.shoppingMultiplier = rules.shoppingMultiplier;
        generated.patienceMultiplier = rules.patienceMultiplier;
        generated.hungerCurveMultiplier = rules.hungerCurveMultiplier;
        generated.funCurveMultiplier = rules.funCurveMultiplier;
        generated.moodCurveMultiplier = rules.moodCurveMultiplier;
        generated.preferredFacilityTags = rules.preferredFacilityTags?.ToArray()
            ?? Array.Empty<string>();
        generated.narrativeTrace = result.NarrativeTrace;
        ApplyGeneratedPersona(generated);
        RecordAudit(hasGeneratedPersona, lastError, hasGeneratedPersona ? dto.personaName : string.Empty);
    }

    private void RecordAudit(bool succeeded, string validationError, string selectedId)
    {
        if (string.IsNullOrWhiteSpace(activeRequestKey)
            || string.IsNullOrWhiteSpace(activeCandidatePacketHash))
        {
            return;
        }
        LastInferenceAudit = new NarrativeInferenceAuditRecord(
            LocalLlmRequestProfiles.Persona.Id,
            activeRequestKey,
            activeCandidatePacketHash,
            succeeded,
            validationError,
            false,
            string.Empty,
            selectedId,
            -1,
            actor?.Identity?.PersistentId ?? string.Empty,
            NarrativeInferenceTimestamp.FromUtc(DateTime.UtcNow));
    }

    private sealed class CustomerPersonaMissingLlmRuntimeProvider : ILocalLlmRuntimeProvider
    {
        public bool TryGetRuntime(out ILocalLlmRuntime runtime)
        {
            runtime = null;
            return false;
        }

        public ILocalLlmRuntime GetRequiredRuntime()
        {
            throw new InvalidOperationException($"{nameof(CustomerPersonaRuntime)} has no Local LLM runtime.");
        }
    }
}
