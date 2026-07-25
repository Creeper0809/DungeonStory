using System;
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

    public CustomerPersonaData Persona => persona ??= new CustomerPersonaData();
    public bool HasGeneratedPersona => hasGeneratedPersona;
    public bool PersonaRequestInProgress => personaRequestInProgress;
    public string LastPrompt => lastPrompt;
    public string LastError => lastError;

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

        if (!TryGetLlmRuntime(logIfMissingQueue, out ILocalLlmRuntime queue))
        {
            return false;
        }

        lastPrompt = BuildPersonaPrompt(actor);
        personaRequestInProgress = true;
        bool accepted = queue.GeneratePersonaAsync(lastPrompt, OnPersonaResult);
        if (!accepted)
        {
            personaRequestInProgress = false;
            lastError = "Persona request was not accepted by LocalLlmRequestQueue.";
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
        personaRequestInProgress = false;
        if (result.IsCancelled)
        {
            lastError = string.Empty;
            return;
        }

        if (!result.IsSuccess)
        {
            lastError = $"{result.Status}: {result.Error}";
            Debug.Log($"{name}: Persona request failed: {lastError}", this);
            return;
        }

        if (!LlmJsonResponseParser.TryParse(result.Content, out CustomerPersonaJsonDto dto, out string parseError))
        {
            lastError = parseError;
            Debug.Log($"{name}: Persona JSON rejected: {parseError}", this);
            return;
        }

        ApplyGeneratedPersona(dto.ToRuntimeData());
    }

    private static string BuildPersonaPrompt(CharacterActor actor)
    {
        CharacterSO data = actor.Identity.Data;
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Generate a compact customer persona for DungeonStory.");
        builder.AppendLine("Return exactly one JSON object and no other text.");
        builder.AppendLine("Use exactly these keys: traitName, flavorText, selfCareMultiplier, curiosityMultiplier, shoppingMultiplier, patienceMultiplier, hungerCurveMultiplier, funCurveMultiplier, moodCurveMultiplier, preferredFacilityTags.");
        builder.AppendLine("All multipliers must be numbers between 0.25 and 2.0. preferredFacilityTags must be an array of short facility tags.");
        builder.AppendLine("Required JSON shape:");
        builder.AppendLine("{\"traitName\":\"string\",\"flavorText\":\"string\",\"selfCareMultiplier\":1.0,\"curiosityMultiplier\":1.0,\"shoppingMultiplier\":1.0,\"patienceMultiplier\":1.0,\"hungerCurveMultiplier\":1.0,\"funCurveMultiplier\":1.0,\"moodCurveMultiplier\":1.0,\"preferredFacilityTags\":[\"Meal\",\"Rest\"]}");
        builder.AppendLine($"name: {data.characterName}");
        builder.AppendLine($"species: {data.SpeciesTag}");
        builder.AppendLine($"role: {actor.Role}");
        builder.AppendLine($"currentHunger: {GetCondition(actor, CharacterCondition.HUNGER):0.0}");
        builder.AppendLine($"currentSleep: {GetCondition(actor, CharacterCondition.SLEEP):0.0}");
        builder.AppendLine($"currentFun: {GetCondition(actor, CharacterCondition.FUN):0.0}");
        builder.AppendLine($"currentMood: {GetCondition(actor, CharacterCondition.MOOD):0.0}");
        builder.AppendLine($"currentExcretion: {GetCondition(actor, CharacterCondition.EXCRETION):0.0}");
        builder.AppendLine($"currentHygiene: {GetCondition(actor, CharacterCondition.HYGIENE):0.0}");
        return builder.ToString();
    }

    private static float GetCondition(CharacterActor actor, CharacterCondition condition)
    {
        return actor != null
            && actor.Stats != null
            && actor.Stats.Stats != null
            && actor.Stats.Stats.TryGetValue(condition, out float value)
                ? value
                : 0f;
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
