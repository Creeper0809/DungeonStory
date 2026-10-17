using System;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum LocalLlmQueueFullBehavior
{
    Fail,
    RejectQuietly,
    Drop
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class LocalLlmRequestProfile
{
    public LocalLlmRequestProfile(
        string id,
        int priority,
        float temperature = 0.4f,
        LocalLlmQueueFullBehavior queueFullBehavior = LocalLlmQueueFullBehavior.Fail,
        bool canBeEvictedForQueuePressure = false,
        float maxQueueAgeSeconds = 0f,
        bool logFailureWarnings = false,
        int maxOutputTokens = 256)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A local LLM request profile requires a stable id.", nameof(id));
        }

        Id = id.Trim();
        Priority = priority;
        Temperature = Mathf.Clamp(temperature, 0f, 2f);
        QueueFullBehavior = queueFullBehavior;
        CanBeEvictedForQueuePressure = canBeEvictedForQueuePressure;
        MaxQueueAgeSeconds = Mathf.Max(0f, maxQueueAgeSeconds);
        LogFailureWarnings = logFailureWarnings;
        MaxOutputTokens = Mathf.Max(64, maxOutputTokens);
    }

    public string Id { get; }
    public int Priority { get; }
    public float Temperature { get; }
    public LocalLlmQueueFullBehavior QueueFullBehavior { get; }
    public bool CanBeEvictedForQueuePressure { get; }
    public float MaxQueueAgeSeconds { get; }
    public bool LogFailureWarnings { get; }
    public int MaxOutputTokens { get; }

    public LocalLlmRequestProfile WithMaxQueueAge(float maxQueueAgeSeconds)
    {
        return new LocalLlmRequestProfile(
            Id,
            Priority,
            Temperature,
            QueueFullBehavior,
            CanBeEvictedForQueuePressure,
            maxQueueAgeSeconds,
            LogFailureWarnings,
            MaxOutputTokens);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class LocalLlmRequestProfiles
{
    public static readonly LocalLlmRequestProfile CharacterSkill = new LocalLlmRequestProfile(
        "CharacterSkill",
        40,
        temperature: 0.35f,
        queueFullBehavior: LocalLlmQueueFullBehavior.Fail,
        logFailureWarnings: false,
        maxOutputTokens: 768);
    public static readonly LocalLlmRequestProfile CharacterSkillModuleSelection = new LocalLlmRequestProfile(
        "CharacterSkillModuleSelection",
        40,
        temperature: 0.35f,
        queueFullBehavior: LocalLlmQueueFullBehavior.Fail,
        logFailureWarnings: false,
        maxOutputTokens: 768);
    public static readonly LocalLlmRequestProfile CharacterSkillLegacyV2 = new LocalLlmRequestProfile(
        "CharacterSkillLegacyV2",
        40,
        temperature: 0.35f,
        queueFullBehavior: LocalLlmQueueFullBehavior.Fail,
        logFailureWarnings: false,
        maxOutputTokens: 768);
    public static readonly LocalLlmRequestProfile Persona = new LocalLlmRequestProfile("Persona", 30);
    public static readonly LocalLlmRequestProfile MacroGoal = new LocalLlmRequestProfile(
        "MacroGoal",
        20,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly);
    public static readonly LocalLlmRequestProfile MoodImpulse = new LocalLlmRequestProfile(
        "MoodImpulse",
        18,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly);
    public static readonly LocalLlmRequestProfile FacilityEvolution = new LocalLlmRequestProfile(
        "FacilityEvolution",
        16,
        logFailureWarnings: false,
        maxOutputTokens: 768);
    public static readonly LocalLlmRequestProfile FacilityEvolutionModuleSelection = new LocalLlmRequestProfile(
        "FacilityEvolutionModuleSelection",
        16,
        temperature: 0.35f,
        logFailureWarnings: false,
        maxOutputTokens: 768);
    public static readonly LocalLlmRequestProfile FacilityEvolutionLegacyV2 = new LocalLlmRequestProfile(
        "FacilityEvolutionLegacyV2",
        16,
        logFailureWarnings: false,
        maxOutputTokens: 768);
    public static readonly LocalLlmRequestProfile EvolutionHistory = new LocalLlmRequestProfile(
        "EvolutionHistory",
        17,
        temperature: 0.65f,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly,
        logFailureWarnings: false,
        maxOutputTokens: 384);
    public static readonly LocalLlmRequestProfile EvolutionHistoryLegacyV2 = new LocalLlmRequestProfile(
        "EvolutionHistoryLegacyV2",
        17,
        temperature: 0.65f,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly,
        logFailureWarnings: false,
        maxOutputTokens: 384);
    public static readonly LocalLlmRequestProfile EquipmentChoiceLegacyV2 = new LocalLlmRequestProfile(
        "EquipmentChoiceLegacyV2",
        17,
        temperature: 0f,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly,
        logFailureWarnings: false,
        maxOutputTokens: 32);
    public static readonly LocalLlmRequestProfile EquipmentEvolutionModuleSelection = new LocalLlmRequestProfile(
        "EquipmentEvolutionModuleSelection",
        17,
        temperature: 0.35f,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly,
        logFailureWarnings: false,
        maxOutputTokens: 512);
    public static readonly LocalLlmRequestProfile AcquiredTrait = new LocalLlmRequestProfile(
        "AcquiredTrait",
        17,
        temperature: 0.35f,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly,
        logFailureWarnings: false,
        maxOutputTokens: 384);
    public static readonly LocalLlmRequestProfile AcquiredTraitModuleSelection = new LocalLlmRequestProfile(
        "AcquiredTraitModuleSelection",
        17,
        temperature: 0.35f,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly,
        logFailureWarnings: false,
        maxOutputTokens: 512);
    public static readonly LocalLlmRequestProfile AcquiredTraitLegacyV2 = new LocalLlmRequestProfile(
        "AcquiredTraitLegacyV2",
        17,
        temperature: 0.35f,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly,
        logFailureWarnings: false,
        maxOutputTokens: 384);
    public static readonly LocalLlmRequestProfile SocialRumor = new LocalLlmRequestProfile(
        "SocialRumor",
        15,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly,
        maxOutputTokens: 384);
    public static readonly LocalLlmRequestProfile CharacterRecord = new LocalLlmRequestProfile(
        "CharacterRecord",
        12,
        temperature: 0.7f,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly);
    public static readonly LocalLlmRequestProfile MultiPerspective = new LocalLlmRequestProfile(
        "MultiPerspective",
        18,
        temperature: 0.65f,
        queueFullBehavior: LocalLlmQueueFullBehavior.Fail,
        logFailureWarnings: false,
        maxOutputTokens: 768);
    public static readonly LocalLlmRequestProfile BubbleLine = new LocalLlmRequestProfile(
        "BubbleLine",
        10,
        queueFullBehavior: LocalLlmQueueFullBehavior.Drop,
        canBeEvictedForQueuePressure: true,
        maxQueueAgeSeconds: 3f);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum LocalLlmRequestStatus
{
    Succeeded,
    Failed,
    Dropped,
    TimedOut,
    Cancelled
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface ILocalLlmRuntime
{
    bool GenerateCharacterSkillAsync(string prompt, Action<LocalLlmResult> callback);
    bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> callback);
    bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> callback);
    bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> callback);
    bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> callback);
    bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> callback);
    bool GenerateCharacterRecordAsync(string prompt, string originalText, Action<LocalLlmResult> callback);
    bool GenerateBubbleLineAsync(string prompt, string originalText, Action<LocalLlmResult> callback);
}

public enum LocalLlmRuntimeReadinessState
{
    Unknown = 0,
    Starting = 1,
    Ready = 2,
    Failed = 3
}

public readonly struct LocalLlmRuntimeReadinessSnapshot
{
    public LocalLlmRuntimeReadinessSnapshot(
        LocalLlmRuntimeReadinessState state,
        string failureReason = "")
    {
        State = state;
        FailureReason = failureReason?.Trim() ?? string.Empty;
        if (state == LocalLlmRuntimeReadinessState.Unknown)
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                "Local LLM readiness must be explicit.");
        }
        if (state == LocalLlmRuntimeReadinessState.Failed
            && FailureReason.Length == 0)
        {
            throw new ArgumentException(
                "Failed local LLM readiness requires an explicit reason.",
                nameof(failureReason));
        }
    }

    public LocalLlmRuntimeReadinessState State { get; }
    public string FailureReason { get; }
}

public interface ILocalLlmRuntimeReadiness
{
    LocalLlmRuntimeReadinessSnapshot CaptureReadiness();
}

/// <summary>
/// An implementation returning true owns the complete lifetime of every
/// request it accepts: queue wait, transport timeout, terminal failure, and
/// cancellation. Each accepted request must invoke its callback exactly once.
/// </summary>
public interface ILocalLlmAcceptedRequestCompletionOwner
{
    bool OwnsAcceptedRequestCompletion { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface ICorrelatedCharacterSkillLlmRuntime
{
    bool GenerateCharacterSkillAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback);

    void CancelCharacterSkillRequest(string requestKey);
}

public interface ICharacterSkillModuleSelectionLlmRuntime
{
    bool GenerateCharacterSkillModuleSelectionAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback);
}

/// <summary>
/// A runtime that can return every unresolved CharacterSkill module-selection
/// candidate in one exact-count response. Legacy/test runtimes may continue to
/// implement the single-selection interface and retain the service watchdog.
/// </summary>
public interface ICharacterSkillModuleSelectionBatchLlmRuntime
{
    bool GenerateCharacterSkillModuleSelectionBatchAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface ICorrelatedEvolutionHistoryLlmRuntime
{
    bool GenerateEvolutionHistoryAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback);

    bool GenerateEvolutionHistoryLegacyV2Async(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback);

    void CancelEvolutionHistoryRequest(string requestKey);
}

public interface ICorrelatedEquipmentEvolutionModuleSelectionLlmRuntime
{
    bool GenerateEquipmentEvolutionModuleSelectionAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback);
}

public interface ICorrelatedFacilityEvolutionModuleSelectionLlmRuntime
{
    bool GenerateFacilityEvolutionModuleSelectionAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback);
}

public interface ICorrelatedAcquiredTraitLlmRuntime
{
    bool GenerateAcquiredTraitAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback);

    void CancelAcquiredTraitRequest(string requestKey);
}

public interface ICorrelatedAcquiredTraitModuleSelectionLlmRuntime
{
    bool GenerateAcquiredTraitModuleSelectionAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback);
}

public interface IConstrainedEquipmentChoiceLlmRuntime
{
    bool GenerateEquipmentChoiceAsync(
        string requestKey,
        string prompt,
        int candidateCount,
        Action<LocalLlmChoiceResult> callback);
}

public interface IMultiPerspectiveNarrativeLlmRuntime
{
    bool GenerateMultiPerspectiveAsync(
        NarrativeMultiPerspectiveRequest request,
        string prompt,
        Action<LocalLlmResult> callback);
}

public readonly struct LocalLlmChoiceResult
{
    public LocalLlmChoiceResult(bool succeeded, int selectedIndex, string error)
    {
        Succeeded = succeeded;
        SelectedIndex = selectedIndex;
        Error = error ?? string.Empty;
    }

    public bool Succeeded { get; }
    public int SelectedIndex { get; }
    public string Error { get; }
}

#if UNITY_EDITOR
public sealed class LocalLlmCapturedRequest
{
    private readonly byte[] wireBodyBytes;

    internal LocalLlmCapturedRequest(
        string profileId,
        string correlationId,
        int attemptIndex,
        string prompt,
        string model,
        int schemaVersion,
        string schemaHash,
        byte[] wireBodyBytes)
    {
        ProfileId = profileId ?? string.Empty;
        CorrelationId = correlationId ?? string.Empty;
        AttemptIndex = attemptIndex;
        Prompt = prompt ?? string.Empty;
        Model = model ?? string.Empty;
        SchemaVersion = schemaVersion;
        SchemaHash = schemaHash ?? string.Empty;
        this.wireBodyBytes = wireBodyBytes != null
            ? (byte[])wireBodyBytes.Clone()
            : Array.Empty<byte>();
    }

    public string ProfileId { get; }
    public string CorrelationId { get; }
    public int AttemptIndex { get; }
    public string Prompt { get; }
    public string Model { get; }
    public int SchemaVersion { get; }
    public string SchemaHash { get; }
    public string WireBodyJson => Encoding.UTF8.GetString(wireBodyBytes);
    public bool CaptureOnly => true;
    public bool NetworkSent => false;

    public byte[] CopyWireBodyBytes()
    {
        return (byte[])wireBodyBytes.Clone();
    }
}
#endif

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct LocalLlmResult
{
    public LocalLlmResult(
        LocalLlmRequestStatus status,
        string content,
        string error,
        string originalText,
        NarrativeGenerationTrace narrativeTrace = null,
        string rawModelContent = null)
    {
        Status = status;
        Content = content ?? string.Empty;
        Error = error ?? string.Empty;
        OriginalText = originalText ?? string.Empty;
        NarrativeTrace = narrativeTrace;
        RawModelContent = rawModelContent ?? string.Empty;
    }

    public LocalLlmRequestStatus Status { get; }
    public string Content { get; }
    public string Error { get; }
    public string OriginalText { get; }
    public NarrativeGenerationTrace NarrativeTrace { get; }
    public string RawModelContent { get; }
    public bool IsSuccess => Status == LocalLlmRequestStatus.Succeeded;
    public bool IsCancelled => Status == LocalLlmRequestStatus.Cancelled;
}

