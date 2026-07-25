using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DungeonStory.Foundation;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

[Serializable]
public sealed class GlobalFacilityReputationSnapshot
{
    public List<SocialRumorSnapshot> rumors = new List<SocialRumorSnapshot>();
    public List<SocialMemoryFloat> reputation = new List<SocialMemoryFloat>();

    public GlobalFacilityReputationSnapshot Clone()
    {
        return new GlobalFacilityReputationSnapshot
        {
            rumors = rumors?.Where(item => item != null).Select(item => item.Clone()).ToList()
                ?? new List<SocialRumorSnapshot>(),
            reputation = reputation?.Where(item => item != null)
                .Select(item => new SocialMemoryFloat(item.key, item.value)).ToList()
                ?? new List<SocialMemoryFloat>()
        };
    }
}

[DisallowMultipleComponent]
[DrawWithUnity]
public sealed class SocialReputationRuntime : SerializedMonoBehaviour
{
    [SerializeField, Min(0.25f)] private float actorScanIntervalSeconds = 1f;
    [SerializeField, Min(0.1f)] private float minSecondsBetweenActorRequests = 4f;
    [SerializeField, Min(0f)] private float rumorSpreadDistance = 8f;
    [SerializeField, Range(0f, 1f)] private float globalReputationBlend = 0.3f;
    [SerializeField, Range(0f, 0.4f)] private float maxFacilityUtilityBias = 0.18f;
    [SerializeField, Min(300)] private int maxPromptCharacters = 2400;
    [SerializeField, ReadOnly] private int appliedRumorCount;
    [SerializeField, ReadOnly] private int heardRumorCount;
    [SerializeField, ReadOnly] private string lastRequestDebug;
    [SerializeField, ReadOnly] private string lastRumorDebug;
    [SerializeField, ReadOnly] private string lastError;
    [SerializeField, ReadOnly] private int actorLogEventCountForDebug;
    [SerializeField, ReadOnly] private string lastActorLogDebug;
    [SerializeField, ReadOnly] private string lastRequestSkipDebug;
    [SerializeField, ReadOnly] private bool suppressWarningLogsForDebug;
    [SerializeField, ReadOnly] private bool suppressActorLogRequestsForDebug;
    [SerializeField, ReadOnly] private CharacterActor actorLogRequestOnlyForDebug;
    [SerializeField, ReadOnly] private List<SocialRumor> globalFacilityRumors = new List<SocialRumor>();
    [SerializeField, ReadOnly] private List<SocialMemoryFloat> facilityReputationDebug = new List<SocialMemoryFloat>();

    private readonly Dictionary<string, float> facilityReputationByKey = new Dictionary<string, float>();
    private readonly Dictionary<CharacterActor, Action<CharacterLogEntry>> actorLogHandlers =
        new Dictionary<CharacterActor, Action<CharacterLogEntry>>();
    private readonly Dictionary<CharacterActor, float> nextRequestTimeByActor =
        new Dictionary<CharacterActor, float>();
    private IBuildingWorldQuery buildingWorldQuery;
    private ICharacterWorldQuery characterWorldQuery;
    private IGameClock gameClock;
    private ILocalLlmRuntimeProvider llmRuntimeProvider;
    private IRandomStream socialRandom;
    private ICharacterSocialMemoryFactory socialMemoryFactory;

    public int AppliedRumorCount => appliedRumorCount;
    public int HeardRumorCount => heardRumorCount;
    public string LastRequestDebug => lastRequestDebug;
    public string LastRumorDebug => lastRumorDebug;
    public string LastError => lastError;
    public int ActorLogEventCountForDebug => actorLogEventCountForDebug;
    public string LastActorLogDebug => lastActorLogDebug;
    public string LastRequestSkipDebug => lastRequestSkipDebug;

    private float nextActorScanTime;

    public GlobalFacilityReputationSnapshot CaptureSnapshot()
    {
        PruneExpiredGlobalRumors();
        float now = RequireGameClock().Time;
        return new GlobalFacilityReputationSnapshot
        {
            rumors = globalFacilityRumors
                .Where(rumor => rumor != null && !rumor.IsExpiredAt(now))
                .Select(rumor => SocialRumorSnapshot.Capture(rumor, now))
                .Where(snapshot => snapshot != null)
                .ToList(),
            reputation = facilityReputationByKey
                .Select(entry => new SocialMemoryFloat(entry.Key, entry.Value))
                .ToList()
        };
    }

    public void RestoreSnapshot(GlobalFacilityReputationSnapshot snapshot)
    {
        float now = RequireGameClock().Time;
        globalFacilityRumors.Clear();
        facilityReputationByKey.Clear();
        if (snapshot != null)
        {
            globalFacilityRumors.AddRange(snapshot.rumors?
                .Where(item => item != null && item.remainingSeconds > 0f)
                .Select(item => item.Restore(now))
                .Where(rumor => rumor != null) ?? Enumerable.Empty<SocialRumor>());
            foreach (SocialMemoryFloat entry in snapshot.reputation ?? new List<SocialMemoryFloat>())
            {
                if (entry != null && !string.IsNullOrWhiteSpace(entry.key))
                {
                    facilityReputationByKey[entry.key] = Mathf.Clamp(entry.value, -1f, 1f);
                }
            }
        }

        SyncDebugList();
    }

    [Inject]
    public void ConstructSocialReputationRuntime(
        ILocalLlmRuntimeProvider llmRuntimeProvider,
        ICharacterWorldQuery characterWorldQuery,
        IBuildingWorldQuery buildingWorldQuery,
        ICharacterSocialMemoryFactory socialMemoryFactory,
        IGameClock gameClock,
        IRandomStreamProvider randomStreamProvider)
    {
        this.llmRuntimeProvider = llmRuntimeProvider
            ?? throw new ArgumentNullException(nameof(llmRuntimeProvider));
        this.characterWorldQuery = characterWorldQuery
            ?? throw new ArgumentNullException(nameof(characterWorldQuery));
        this.buildingWorldQuery = buildingWorldQuery
            ?? throw new ArgumentNullException(nameof(buildingWorldQuery));
        this.socialMemoryFactory = socialMemoryFactory
            ?? throw new ArgumentNullException(nameof(socialMemoryFactory));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        socialRandom = (randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get("social-reputation");

        if (isActiveAndEnabled)
        {
            RegisterExistingActors();
        }
    }

    private void OnEnable()
    {
        RegisterExistingActorsIfInjected();
    }

    private void Start()
    {
        RegisterExistingActorsIfInjected();
    }

    private void OnDisable()
    {
        UnsubscribeAllActors();
    }

    private void Update()
    {
        if (characterWorldQuery == null || gameClock == null)
        {
            return;
        }

        if (gameClock.Time < nextActorScanTime)
        {
            return;
        }

        nextActorScanTime = gameClock.Time + Mathf.Max(0.25f, actorScanIntervalSeconds);
        RegisterExistingActors();
    }

    public bool RequestSocialInterpretation(CharacterActor speaker, CharacterLogEntry entry)
    {
        if (suppressActorLogRequestsForDebug)
        {
            lastRequestSkipDebug = "suppressed";
            return false;
        }

        if (actorLogRequestOnlyForDebug != null && speaker != actorLogRequestOnlyForDebug)
        {
            lastRequestSkipDebug = "filtered";
            return false;
        }

        if (speaker == null || !ShouldInterpretSocialEvent(entry))
        {
            lastRequestSkipDebug = speaker == null
                ? "speaker missing"
                : $"ignored event: {entry.Tag} {entry.OriginalMessage}";
            return false;
        }

        if (RequireGameClock().Time < GetNextRequestTime(speaker))
        {
            lastRequestSkipDebug = "cooldown";
            return false;
        }

        if (!TryGetLlmRuntime(out ILocalLlmRuntime queue))
        {
            return false;
        }

        BuildableObject eventFacility = ResolveFacilityFromLog(entry);
        float inferredSentiment = InferSocialEventSentiment(entry);
        bool hasInferredSentiment = Mathf.Abs(inferredSentiment) > 0.01f;
        string prompt = BuildSocialRumorPrompt(
            speaker,
            entry,
            eventFacility,
            hasInferredSentiment,
            inferredSentiment);
        lastRequestDebug = prompt;
        lastRequestSkipDebug = string.Empty;
        if (!queue.GenerateSocialRumorAsync(
                prompt,
                (result) => OnSocialRumorResult(
                    speaker,
                    result,
                    eventFacility,
                    hasInferredSentiment,
                    inferredSentiment,
                    false)))
        {
            lastError = "Social rumor request was not accepted by LocalLlmRequestQueue.";
            lastRequestSkipDebug = lastError;
            nextRequestTimeByActor[speaker] =
                RequireGameClock().Time + Mathf.Max(0.1f, minSecondsBetweenActorRequests);
            return false;
        }

        nextRequestTimeByActor[speaker] =
            RequireGameClock().Time + Mathf.Max(0.1f, minSecondsBetweenActorRequests);
        return true;
    }

    public bool ReportFacilityExperience(
        CharacterActor speaker,
        BuildableObject facility,
        string eventName,
        float sentiment,
        string summary)
    {
        if (speaker == null || facility == null)
        {
            return false;
        }

        if (RequireGameClock().Time < GetNextRequestTime(speaker))
        {
            lastRequestSkipDebug = "cooldown";
            return false;
        }

        if (!TryGetLlmRuntime(out ILocalLlmRuntime queue))
        {
            return false;
        }

        float expectedSentiment = Mathf.Clamp(sentiment, -1f, 1f);
        string prompt = BuildFacilityExperienceRumorPrompt(
            speaker,
            facility,
            eventName,
            expectedSentiment,
            summary);
        lastRequestDebug = prompt;
        if (!queue.GenerateSocialRumorAsync(
                prompt,
                (result) => OnSocialRumorResult(speaker, result, facility, true, expectedSentiment, true)))
        {
            lastError = "Social rumor request was not accepted by LocalLlmRequestQueue.";
            lastRequestSkipDebug = lastError;
            nextRequestTimeByActor[speaker] =
                RequireGameClock().Time + Mathf.Max(0.1f, minSecondsBetweenActorRequests);
            return false;
        }

        nextRequestTimeByActor[speaker] =
            RequireGameClock().Time + Mathf.Max(0.1f, minSecondsBetweenActorRequests);
        lastRequestSkipDebug = string.Empty;
        return true;
    }

    public bool ApplyRumor(SocialRumor rumor, CharacterActor speaker)
    {
        if (rumor == null
            || !rumor.IsActionableAt(RequireGameClock().Time)
            || !HasValidTarget(rumor))
        {
            return false;
        }

        FillSourceIfMissing(rumor, speaker);
        rumor.sentiment = CharacterSkillRuntimeEffects.ApplyPositiveRelationshipBonus(
            speaker,
            rumor.sentiment);
        if (rumor.targetType == SocialRumorTargetType.Facility)
        {
            ApplyGlobalFacilityReputation(rumor);
        }

        CharacterSocialMemory speakerMemory = EnsureMemory(speaker);
        speakerMemory?.HearRumor(rumor, speaker);

        int spreadCount = SpreadRumor(rumor, speaker);
        appliedRumorCount++;
        heardRumorCount += spreadCount + (speakerMemory != null ? 1 : 0);
        lastRumorDebug = $"{rumor.type} {rumor.targetType} sentiment={rumor.sentiment:0.00} spread={spreadCount} summary={rumor.summary}";
        SyncDebugList();
        return true;
    }

    private bool TryGetLlmRuntime(out ILocalLlmRuntime queue)
    {
        if (llmRuntimeProvider == null)
        {
            throw new InvalidOperationException($"{nameof(SocialReputationRuntime)} requires {nameof(ILocalLlmRuntimeProvider)} injection.");
        }

        if (llmRuntimeProvider.TryGetRuntime(out queue))
        {
            return true;
        }

        lastError = $"{nameof(LocalLlmRequestQueue)} is missing.";
        LogWarningIfAllowed(lastError);
        return false;
    }

    private ICharacterWorldQuery RequireCharacterWorldQuery()
    {
        if (characterWorldQuery == null)
        {
            throw new InvalidOperationException(
                $"{nameof(SocialReputationRuntime)} requires {nameof(ICharacterWorldQuery)} injection.");
        }

        return characterWorldQuery;
    }

    private IBuildingWorldQuery RequireBuildingWorldQuery()
    {
        if (buildingWorldQuery == null)
        {
            throw new InvalidOperationException(
                $"{nameof(SocialReputationRuntime)} requires {nameof(IBuildingWorldQuery)} injection.");
        }

        return buildingWorldQuery;
    }

    private IGameClock RequireGameClock()
    {
        return gameClock
            ?? throw new InvalidOperationException(
                $"{nameof(SocialReputationRuntime)} requires {nameof(IGameClock)} injection.");
    }

    private IRandomStream RequireSocialRandom()
    {
        return socialRandom
            ?? throw new InvalidOperationException(
                $"{nameof(SocialReputationRuntime)} requires {nameof(IRandomStreamProvider)} injection.");
    }

    public float GetFacilityUtilityBias(CharacterActor actor, BuildableObject building)
    {
        if (building == null)
        {
            return 0f;
        }

        float sentiment = GetCombinedFacilitySentiment(actor, building);
        return Mathf.Clamp(sentiment * maxFacilityUtilityBias, -maxFacilityUtilityBias, maxFacilityUtilityBias);
    }

    public float GetCombinedFacilitySentiment(CharacterActor actor, BuildableObject building)
    {
        if (building == null)
        {
            return 0f;
        }

        float global = GetGlobalFacilitySentiment(building);
        CharacterSocialMemory memory = actor != null ? actor.GetComponent<CharacterSocialMemory>() : null;
        float personal = memory != null ? memory.GetFacilitySentiment(building) : 0f;
        return Mathf.Clamp(global * 0.4f + personal * 0.6f, -1f, 1f);
    }

    public float GetGlobalFacilitySentiment(BuildableObject building)
    {
        if (building == null)
        {
            return 0f;
        }

        PruneExpiredGlobalRumors();
        float sum = 0f;
        int count = 0;
        foreach (KeyValuePair<string, float> entry in facilityReputationByKey)
        {
            if (!SocialRumorUtility.MatchesFacilityKey(building, entry.Key))
            {
                continue;
            }

            sum += entry.Value;
            count++;
        }

        return count > 0 ? Mathf.Clamp(sum / count, -1f, 1f) : 0f;
    }

    public void ClearForDebug()
    {
        globalFacilityRumors.Clear();
        facilityReputationByKey.Clear();
        nextRequestTimeByActor.Clear();
        appliedRumorCount = 0;
        heardRumorCount = 0;
        lastRequestDebug = string.Empty;
        lastRumorDebug = string.Empty;
        lastError = string.Empty;
        actorLogEventCountForDebug = 0;
        lastActorLogDebug = string.Empty;
        lastRequestSkipDebug = string.Empty;
        suppressWarningLogsForDebug = false;
        actorLogRequestOnlyForDebug = null;
        SyncDebugList();
    }

    public void SetActorLogRequestsSuppressedForDebug(bool value)
    {
        suppressActorLogRequestsForDebug = value;
    }

    public void SetWarningLogsSuppressedForDebug(bool value)
    {
        suppressWarningLogsForDebug = value;
    }

    public void RestrictActorLogRequestsForDebug(CharacterActor actor)
    {
        actorLogRequestOnlyForDebug = actor;
    }

    public void RegisterActorForDebug(CharacterActor actor)
    {
        RegisterActor(actor);
    }

    private void RegisterExistingActors()
    {
        IReadOnlyList<CharacterActor> actors = RequireCharacterWorldQuery().Characters;
        foreach (CharacterActor actor in actors)
        {
            RegisterActor(actor);
        }
    }

    private void RegisterExistingActorsIfInjected()
    {
        if (characterWorldQuery != null)
        {
            RegisterExistingActors();
        }
    }

    private void RegisterActor(CharacterActor actor)
    {
        if (actor == null || actorLogHandlers.ContainsKey(actor))
        {
            return;
        }

        CharacterLog log = actor.GetComponent<CharacterLog>();
        if (log == null)
        {
            return;
        }

        EnsureMemory(actor);
        Action<CharacterLogEntry> handler = (entry) => OnActorLogAdded(actor, entry);
        log.OnLogAdded += handler;
        actorLogHandlers[actor] = handler;
    }

    private void OnActorLogAdded(CharacterActor actor, CharacterLogEntry entry)
    {
        actorLogEventCountForDebug++;
        lastActorLogDebug = $"{SocialRumorUtility.GetActorLabel(actor)}: {entry.Tag} / {entry.OriginalMessage}";
        RequestSocialInterpretation(actor, entry);
    }

    private void OnSocialRumorResult(CharacterActor speaker, LocalLlmResult result)
    {
        OnSocialRumorResult(speaker, result, null, false, 0f, false);
    }

    private void OnSocialRumorResult(
        CharacterActor speaker,
        LocalLlmResult result,
        BuildableObject expectedFacility,
        bool validateExpectedSentiment,
        float expectedSentiment,
        bool logWarnings)
    {
        if (result.IsCancelled)
        {
            lastError = string.Empty;
            return;
        }

        if (!result.IsSuccess)
        {
            lastError = $"{result.Status}: {result.Error}";
            LogSocialWarningIfNeeded(logWarnings, $"Social rumor request failed: {lastError}");
            return;
        }

        if (!LlmJsonResponseParser.TryParse(result.Content, out SocialRumorJsonDto dto, out string parseError))
        {
            lastError = parseError;
            LogSocialWarningIfNeeded(logWarnings, $"Social rumor JSON rejected: {parseError}");
            return;
        }

        SocialRumor rumor = dto.ToRuntimeRumor("LocalLLM", speaker, RequireGameClock().Time);
        if (rumor.type == SocialRumorType.None)
        {
            lastError = string.Empty;
            lastRumorDebug = "LLM marked event as not socially actionable.";
            return;
        }

        if (expectedFacility != null && !RumorTargetsExpectedFacility(rumor, expectedFacility))
        {
            lastError = "Social rumor target did not match the requested facility.";
            LogSocialWarningIfNeeded(logWarnings, lastError);
            return;
        }

        if (validateExpectedSentiment && !SentimentMatchesExpected(rumor.sentiment, expectedSentiment))
        {
            lastError = "Social rumor sentiment did not match the reported experience.";
            LogSocialWarningIfNeeded(logWarnings, lastError);
            return;
        }

        if (rumor.targetType == SocialRumorTargetType.Facility
            && !RumorTargetsKnownFacility(rumor))
        {
            lastError = "Social rumor facility target did not match a known facility.";
            LogSocialWarningIfNeeded(logWarnings, lastError);
            return;
        }

        if (!ApplyRumor(rumor, speaker))
        {
            lastError = "Social rumor had no valid target.";
            LogSocialWarningIfNeeded(logWarnings, lastError);
        }
    }

    private void LogSocialWarningIfNeeded(bool logWarnings, string message)
    {
        if (logWarnings)
        {
            LogWarningIfAllowed(message);
        }
    }

    private void LogWarningIfAllowed(string message)
    {
        if (suppressWarningLogsForDebug)
        {
            return;
        }

        Debug.Log($"{name}: {message}", this);
    }

    private string BuildSocialRumorPrompt(
        CharacterActor speaker,
        CharacterLogEntry entry,
        BuildableObject explicitFacility,
        bool hasExplicitSentiment,
        float explicitSentiment)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Interpret a DungeonStory NPC event as social rumor/reputation data.");
        builder.AppendLine("Return exactly one JSON object with rumorType, targetType, targetFacilityId, targetFacilityTag, targetCharacterId, targetCharacterName, sentiment, summary, spreadChance, trustImpact, validSeconds.");
        builder.AppendLine("Allowed rumorType values: None, Complaint, Recommendation, Warning, Praise.");
        builder.AppendLine("Allowed targetType values: None, Facility, Character.");
        builder.AppendLine("All numeric fields must be raw JSON numbers, never strings, words, or null.");
        builder.AppendLine("targetFacilityId must be an integer. targetCharacterId must be a persistent-ID string. Use -1 and an empty string when not used. Never output null.");
        builder.AppendLine("sentiment and trustImpact must be numbers between -1 and 1. spreadChance must be a number between 0 and 1.");
        builder.AppendLine("validSeconds must be a number between 0 and 1800. Use 600 for normal rumors. Never output 3600 or higher.");
        builder.AppendLine("Actionable Complaint, Recommendation, Warning, and Praise rumors must use spreadChance between 0.35 and 1.0.");
        builder.AppendLine("For blocked path, no destination, or occupied destination warnings, use spreadChance 1.0.");
        builder.AppendLine("Use rumorType None only when no NPC should share this event.");
        builder.AppendLine("Use rumorType None and targetType None when this event should not become a rumor. Do not invent targets outside candidateFacilities.");
        if (explicitFacility != null)
        {
            builder.AppendLine("This request has exactly one allowed facility target.");
            builder.AppendLine($"The only valid facility target is id={explicitFacility.id}, tag={SocialRumorUtility.GetFacilityTag(explicitFacility)}.");
            builder.AppendLine("Because this event is a facility experience, output targetType Facility unless rumorType is None.");
            builder.AppendLine("Do not output targetType Character for facility experiences.");
            builder.AppendLine("If you output targetType Facility, targetFacilityId must equal that id. Any other facility target is invalid.");
            builder.AppendLine($"Required target fields for this event: \"targetType\":\"Facility\", \"targetFacilityId\":{explicitFacility.id}, \"targetCharacterId\":\"\", \"targetCharacterName\":\"\".");
            if (hasExplicitSentiment)
            {
                builder.AppendLine($"Reported experience sentiment is {explicitSentiment:0.00}; output sentiment must keep the same sign.");
                builder.AppendLine(explicitSentiment >= 0f
                    ? "For positive facility experiences, use Recommendation or Praise and a positive sentiment."
                    : "For negative facility experiences, use Complaint or Warning and a negative sentiment.");
            }
        }

        builder.AppendLine("Speaker:");
        builder.AppendLine($"name: {SocialRumorUtility.GetActorLabel(speaker)}");
        builder.AppendLine($"species: {(speaker != null ? speaker.SpeciesTag : string.Empty)}");
        builder.AppendLine($"role: {(speaker != null ? speaker.Role.ToString() : string.Empty)}");
        builder.AppendLine("Event:");
        builder.AppendLine($"tag: {entry.Tag}");
        builder.AppendLine($"count: {entry.Count}");
        builder.AppendLine($"message: {entry.OriginalMessage}");
        builder.AppendLine("candidateFacilities:");
        AppendCandidateFacilities(builder, speaker, explicitFacility);
        builder.AppendLine("Example: {\"rumorType\":\"Recommendation\",\"targetType\":\"Facility\",\"targetFacilityId\":12,\"targetFacilityTag\":\"Rest\",\"targetCharacterId\":\"\",\"targetCharacterName\":\"\",\"sentiment\":0.6,\"summary\":\"rest facility visit was good\",\"spreadChance\":0.55,\"trustImpact\":0.05,\"validSeconds\":600}");

        string prompt = builder.ToString();
        return prompt.Length > maxPromptCharacters
            ? prompt.Substring(0, maxPromptCharacters)
            : prompt;
    }

    private string BuildFacilityExperienceRumorPrompt(
        CharacterActor speaker,
        BuildableObject facility,
        string eventName,
        float sentiment,
        string summary)
    {
        string rumorTypeHint = sentiment >= 0f ? "Recommendation" : "Complaint";
        string facilityTag = SocialRumorUtility.GetFacilityTag(facility);
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Create one shareable NPC facility rumor from this facility experience.");
        builder.AppendLine("Return exactly one compact JSON object and no markdown.");
        builder.AppendLine("Do not invent another target. Do not output targetType Character.");
        builder.AppendLine("All numeric fields must be raw JSON numbers, never strings, words, or null.");
        builder.AppendLine($"The rumorType should be {rumorTypeHint} unless the event clearly fits Praise or Warning.");
        builder.AppendLine("targetType must be Facility.");
        builder.AppendLine($"targetFacilityId must be {facility.id}.");
        builder.AppendLine($"targetFacilityTag must be \"{facilityTag}\".");
        builder.AppendLine("targetCharacterId and targetCharacterName must both be empty strings.");
        builder.AppendLine(sentiment >= 0f
            ? "sentiment must be a positive number between 0.35 and 1."
            : "sentiment must be a negative number between -1 and -0.35.");
        builder.AppendLine("spreadChance must be 1.0 so nearby listeners can deterministically hear this facility experience.");
        builder.AppendLine("trustImpact must be a number between -1 and 1.");
        builder.AppendLine("validSeconds must be 600.");
        builder.AppendLine("Required JSON shape:");
        builder.AppendLine(
            $"{{\"rumorType\":\"{rumorTypeHint}\",\"targetType\":\"Facility\",\"targetFacilityId\":{facility.id},\"targetFacilityTag\":\"{facilityTag}\",\"targetCharacterId\":\"\",\"targetCharacterName\":\"\",\"sentiment\":{(sentiment >= 0f ? "0.75" : "-0.75")},\"summary\":\"short text\",\"spreadChance\":1.0,\"trustImpact\":0.1,\"validSeconds\":600}}");
        builder.AppendLine("Speaker:");
        builder.AppendLine($"name: {SocialRumorUtility.GetActorLabel(speaker)}");
        builder.AppendLine($"species: {(speaker != null ? speaker.SpeciesTag : string.Empty)}");
        builder.AppendLine("Facility:");
        builder.AppendLine($"id: {facility.id}");
        builder.AppendLine($"name: {SocialRumorUtility.GetFacilityLabel(facility)}");
        builder.AppendLine($"tag: {facilityTag}");
        builder.AppendLine("Experience:");
        builder.AppendLine($"eventName: {eventName}");
        builder.AppendLine($"reportedSentiment: {sentiment:0.00}");
        builder.AppendLine($"summary: {summary}");

        string prompt = builder.ToString();
        return prompt.Length > maxPromptCharacters
            ? prompt.Substring(0, maxPromptCharacters)
            : prompt;
    }

    private void AppendCandidateFacilities(
        StringBuilder builder,
        CharacterActor speaker,
        BuildableObject explicitFacility)
    {
        if (explicitFacility != null)
        {
            AppendFacilityLine(builder, explicitFacility);
            return;
        }

        IEnumerable<BuildableObject> facilities = speaker != null
            ? speaker.GetReachableBuilding()
            : Enumerable.Empty<BuildableObject>();
        List<BuildableObject> candidates = facilities
            .Where((building) => building != null)
            .Take(8)
            .ToList();
        if (candidates.Count == 0)
        {
            candidates = FindNearbyFacilities(speaker)
                .Take(8)
                .ToList();
        }

        foreach (BuildableObject building in candidates)
        {
            AppendFacilityLine(builder, building);
        }
    }

    private IEnumerable<BuildableObject> FindNearbyFacilities(CharacterActor speaker)
    {
        IReadOnlyList<BuildableObject> buildings = RequireBuildingWorldQuery().Buildings;
        if (speaker == null)
        {
            return buildings.Where((building) => building != null);
        }

        Vector3 speakerPosition = speaker.transform.position;
        return buildings
            .Where((building) => building != null)
            .OrderBy((building) => (building.transform.position - speakerPosition).sqrMagnitude);
    }

    private static void AppendFacilityLine(StringBuilder builder, BuildableObject building)
    {
        string label = SocialRumorUtility.GetFacilityLabel(building);
        string tag = SocialRumorUtility.GetFacilityTag(building);
        builder.AppendLine($"- id={building.id}; name={label}; tag={tag}");
    }

    private BuildableObject ResolveFacilityFromLog(CharacterLogEntry entry)
    {
        int facilityId = entry.Activity != null ? entry.Activity.FacilityId : -1;
        if (facilityId < 0)
        {
            return null;
        }

        IReadOnlyList<BuildableObject> buildings = RequireBuildingWorldQuery().Buildings;
        return buildings.FirstOrDefault((building) => building != null && building.id == facilityId);
    }

    private static bool TryExtractFacilityId(string value, out int facilityId)
    {
        facilityId = -1;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        Match match = Regex.Match(
            value,
            @"\bfacility\s*(?:id|#|=|:)?\s*(-?\d+)\b",
            RegexOptions.IgnoreCase);
        return match.Success
            && int.TryParse(match.Groups[1].Value, out facilityId)
            && facilityId >= 0;
    }

    private static float InferSocialEventSentiment(CharacterLogEntry entry)
    {
        return entry.Activity != null ? entry.Activity.Sentiment : 0f;
    }

    private static bool ShouldInterpretSocialEvent(CharacterLogEntry entry)
    {
        CharacterActivityEvent activity = entry.Activity;
        return activity != null
            && activity.VisibleToPlayer
            && (Mathf.Abs(activity.Sentiment) > 0.01f
                || string.Equals(activity.KindId, CharacterActivityKinds.Social, StringComparison.Ordinal)
                || string.Equals(activity.KindId, CharacterActivityKinds.Combat, StringComparison.Ordinal));
    }

    private int SpreadRumor(SocialRumor rumor, CharacterActor speaker)
    {
        int spreadCount = 0;
        IReadOnlyList<CharacterActor> actors = RequireCharacterWorldQuery().Characters;
        foreach (CharacterActor listener in actors)
        {
            if (listener == null || listener == speaker || !CanHearRumor(speaker, listener))
            {
                continue;
            }

            if (!RequireSocialRandom().Chance(rumor.spreadChance))
            {
                continue;
            }

            CharacterSocialMemory memory = EnsureMemory(listener);
            if (memory == null)
            {
                continue;
            }

            memory.HearRumor(rumor, speaker);
            spreadCount++;
        }

        return spreadCount;
    }

    private bool CanHearRumor(CharacterActor speaker, CharacterActor listener)
    {
        if (listener == null || listener.IsDead)
        {
            return false;
        }

        if (speaker == null || rumorSpreadDistance <= 0f)
        {
            return true;
        }

        float maxDistanceSquared = rumorSpreadDistance * rumorSpreadDistance;
        return (speaker.transform.position - listener.transform.position).sqrMagnitude <= maxDistanceSquared;
    }

    private void ApplyGlobalFacilityReputation(SocialRumor rumor)
    {
        globalFacilityRumors.Add(rumor.Clone());
        PruneExpiredGlobalRumors();
        RebuildGlobalFacilityReputation();
    }

    private void RebuildGlobalFacilityReputation()
    {
        float now = RequireGameClock().Time;
        facilityReputationByKey.Clear();
        foreach (SocialRumor rumor in globalFacilityRumors)
        {
            if (rumor == null
                || rumor.IsExpiredAt(now)
                || rumor.targetType != SocialRumorTargetType.Facility)
            {
                continue;
            }

            ApplyGlobalFacilityReputationEntry(rumor);
        }

        SyncDebugList();
    }

    private void ApplyGlobalFacilityReputationEntry(SocialRumor rumor)
    {
        foreach (string key in SocialRumorUtility.GetFacilityKeys(rumor))
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            float current = facilityReputationByKey.TryGetValue(key, out float value) ? value : 0f;
            facilityReputationByKey[key] = Mathf.Clamp(
                Mathf.Lerp(current, rumor.sentiment, globalReputationBlend),
                -1f,
                1f);
        }
    }

    private void PruneExpiredGlobalRumors()
    {
        float now = RequireGameClock().Time;
        bool removed = false;
        for (int i = globalFacilityRumors.Count - 1; i >= 0; i--)
        {
            if (globalFacilityRumors[i] == null
                || globalFacilityRumors[i].IsExpiredAt(now))
            {
                globalFacilityRumors.RemoveAt(i);
                removed = true;
            }
        }

        if (removed)
        {
            RebuildGlobalFacilityReputation();
        }
    }

    private CharacterSocialMemory EnsureMemory(CharacterActor actor)
    {
        if (actor == null)
        {
            return null;
        }

        return RequireSocialMemoryFactory().GetOrAdd(actor);
    }

    private ICharacterSocialMemoryFactory RequireSocialMemoryFactory()
    {
        if (socialMemoryFactory == null)
        {
            throw new InvalidOperationException($"{nameof(SocialReputationRuntime)} requires {nameof(ICharacterSocialMemoryFactory)} injection.");
        }

        return socialMemoryFactory;
    }

    private static void FillSourceIfMissing(SocialRumor rumor, CharacterActor speaker)
    {
        if (rumor == null || speaker == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(rumor.sourceActorId) && speaker.Identity != null)
        {
            rumor.sourceActorId = speaker.Identity.PersistentId;
        }

        if (string.IsNullOrWhiteSpace(rumor.sourceActorName))
        {
            rumor.sourceActorName = SocialRumorUtility.GetActorLabel(speaker);
        }
    }

    private static bool HasValidTarget(SocialRumor rumor)
    {
        if (rumor == null)
        {
            return false;
        }

        if (rumor.targetType == SocialRumorTargetType.Facility)
        {
            return SocialRumorUtility.GetFacilityKeys(rumor).Any();
        }

        if (rumor.targetType == SocialRumorTargetType.Character)
        {
            return SocialRumorUtility.GetCharacterKeys(rumor).Any();
        }

        return false;
    }

    private static bool RumorTargetsExpectedFacility(SocialRumor rumor, BuildableObject expectedFacility)
    {
        if (rumor == null || expectedFacility == null)
        {
            return false;
        }

        if (rumor.targetType != SocialRumorTargetType.Facility)
        {
            return false;
        }

        if (rumor.targetFacilityId >= 0)
        {
            return rumor.targetFacilityId == expectedFacility.id;
        }

        return SocialRumorUtility.MatchesFacilityTag(expectedFacility, rumor.targetFacilityTag);
    }

    private bool RumorTargetsKnownFacility(SocialRumor rumor)
    {
        if (rumor == null || rumor.targetType != SocialRumorTargetType.Facility)
        {
            return false;
        }

        IReadOnlyList<BuildableObject> buildings = RequireBuildingWorldQuery().Buildings;
        foreach (BuildableObject building in buildings)
        {
            if (building == null)
            {
                continue;
            }

            if (rumor.targetFacilityId >= 0 && building.id == rumor.targetFacilityId)
            {
                return true;
            }

            if (SocialRumorUtility.MatchesFacilityTag(building, rumor.targetFacilityTag))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SentimentMatchesExpected(float actualSentiment, float expectedSentiment)
    {
        if (Mathf.Abs(expectedSentiment) < 0.05f)
        {
            return Mathf.Abs(actualSentiment) <= 0.15f;
        }

        return Mathf.Sign(actualSentiment) == Mathf.Sign(expectedSentiment);
    }

    private float GetNextRequestTime(CharacterActor actor)
    {
        return actor != null && nextRequestTimeByActor.TryGetValue(actor, out float time)
            ? time
            : 0f;
    }

    private void UnsubscribeAllActors()
    {
        foreach (KeyValuePair<CharacterActor, Action<CharacterLogEntry>> entry in actorLogHandlers)
        {
            CharacterActor actor = entry.Key;
            if (actor == null)
            {
                continue;
            }

            CharacterLog log = actor.GetComponent<CharacterLog>();
            if (log != null)
            {
                log.OnLogAdded -= entry.Value;
            }
        }

        actorLogHandlers.Clear();
    }

    private void SyncDebugList()
    {
        facilityReputationDebug.Clear();
        foreach (KeyValuePair<string, float> entry in facilityReputationByKey)
        {
            facilityReputationDebug.Add(new SocialMemoryFloat(entry.Key, entry.Value));
        }
    }

}
