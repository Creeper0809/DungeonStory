using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

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

    private readonly Dictionary<CharacterActor, Action<CharacterLogEntry>> actorLogHandlers =
        new Dictionary<CharacterActor, Action<CharacterLogEntry>>();
    private readonly Dictionary<CharacterActor, float> nextRequestTimeByActor =
        new Dictionary<CharacterActor, float>();
    private IBuildingWorldQuery buildingWorldQuery;
    private ICharacterWorldQuery characterWorldQuery;
    private IGameClock gameClock;
    private IUiClock uiClock;
    private ILocalLlmRuntimeProvider llmRuntimeProvider;
    private IRandomStream socialRandom;
    private ICharacterSocialMemoryFactory socialMemoryFactory;
    private GlobalFacilityReputationLedger globalReputationLedger;
    private SocialRumorPromptComposer promptComposer;

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
        return RequireGlobalReputationLedger().CaptureSnapshot(globalReputationBlend);
    }

    public void RestoreSnapshot(GlobalFacilityReputationSnapshot snapshot)
    {
        GlobalFacilityReputationRestoreTransaction transaction =
            ApplyRestoreCandidate(BuildRestoreCandidate(snapshot));
        CompleteRestore(transaction);
    }

    public GlobalFacilityReputationRestoreCandidate BuildRestoreCandidate(
        GlobalFacilityReputationSnapshot snapshot)
    {
        return RequireGlobalReputationLedger().BuildRestoreCandidate(snapshot);
    }

    public GlobalFacilityReputationRestoreTransaction ApplyRestoreCandidate(
        GlobalFacilityReputationRestoreCandidate candidate)
    {
        GlobalFacilityReputationRestoreTransaction transaction =
            RequireGlobalReputationLedger().ApplyRestoreCandidate(candidate);
        SynchronizeLedgerProjectionReferences();
        return transaction;
    }

    public void RollbackRestore(
        GlobalFacilityReputationRestoreTransaction transaction)
    {
        RequireGlobalReputationLedger().RollbackRestore(transaction);
        SynchronizeLedgerProjectionReferences();
    }

    public void CompleteRestore(
        GlobalFacilityReputationRestoreTransaction transaction)
    {
        RequireGlobalReputationLedger().CompleteRestore(transaction);
        SynchronizeLedgerProjectionReferences();
    }

    [Inject]
    public void ConstructSocialReputationRuntime(
        ILocalLlmRuntimeProvider llmRuntimeProvider,
        ICharacterWorldQuery characterWorldQuery,
        IBuildingWorldQuery buildingWorldQuery,
        ICharacterSocialMemoryFactory socialMemoryFactory,
        IGameClock gameClock,
        IRandomStreamProvider randomStreamProvider,
        IUiClock uiClock)
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
        this.uiClock = uiClock;
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

        float cadenceTime = uiClock?.Time ?? gameClock.Time;
        if (gameClock.IsPaused || cadenceTime < nextActorScanTime)
        {
            return;
        }

        nextActorScanTime = cadenceTime
            + Mathf.Max(0.25f, actorScanIntervalSeconds);
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

        BuildableObject eventFacility = RequirePromptComposer().ResolveFacility(entry);
        float inferredSentiment = InferSocialEventSentiment(entry);
        bool hasInferredSentiment = Mathf.Abs(inferredSentiment) > 0.01f;
        bool ruleApplied = eventFacility != null && hasInferredSentiment
            && ApplyRuleFacilityRumor(
                speaker,
                eventFacility,
                inferredSentiment,
                entry.OriginalMessage,
                "RuleEvent");
        if (!TryGetLlmRuntime(out ILocalLlmRuntime queue))
        {
            nextRequestTimeByActor[speaker] =
                RequireGameClock().Time + Mathf.Max(0.1f, minSecondsBetweenActorRequests);
            return ruleApplied;
        }

        string prompt = RequirePromptComposer().BuildEventPrompt(
            speaker,
            entry,
            eventFacility,
            hasInferredSentiment,
            inferredSentiment,
            maxPromptCharacters);
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
            return ruleApplied;
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

        float expectedSentiment = Mathf.Clamp(sentiment, -1f, 1f);
        bool ruleApplied = ApplyRuleFacilityRumor(
            speaker,
            facility,
            expectedSentiment,
            summary,
            string.IsNullOrWhiteSpace(eventName) ? "RuleExperience" : eventName.Trim());
        if (!TryGetLlmRuntime(out ILocalLlmRuntime queue))
        {
            nextRequestTimeByActor[speaker] =
                RequireGameClock().Time + Mathf.Max(0.1f, minSecondsBetweenActorRequests);
            return ruleApplied;
        }

        string prompt = RequirePromptComposer().BuildFacilityExperiencePrompt(
            speaker,
            facility,
            eventName,
            expectedSentiment,
            summary,
            maxPromptCharacters);
        lastRequestDebug = prompt;
        if (!queue.GenerateSocialRumorAsync(
                prompt,
                (result) => OnSocialRumorResult(speaker, result, facility, true, expectedSentiment, true)))
        {
            lastError = "Social rumor request was not accepted by LocalLlmRequestQueue.";
            lastRequestSkipDebug = lastError;
            nextRequestTimeByActor[speaker] =
                RequireGameClock().Time + Mathf.Max(0.1f, minSecondsBetweenActorRequests);
            return ruleApplied;
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
            RequireGlobalReputationLedger().Apply(
                rumor,
                globalReputationBlend);
        }

        CharacterSocialMemory speakerMemory = EnsureMemory(speaker);
        speakerMemory?.HearRumor(rumor, speaker);

        int spreadCount = SpreadRumor(rumor, speaker);
        appliedRumorCount++;
        heardRumorCount += spreadCount + (speakerMemory != null ? 1 : 0);
        lastRumorDebug = $"{rumor.type} {rumor.targetType} sentiment={rumor.sentiment:0.00} spread={spreadCount} summary={rumor.summary}";
        RequireGlobalReputationLedger().SyncDebugProjection();
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

    private SocialRumorPromptComposer RequirePromptComposer()
    {
        return promptComposer ??= new SocialRumorPromptComposer(RequireBuildingWorldQuery());
    }

    private GlobalFacilityReputationLedger RequireGlobalReputationLedger()
    {
        globalFacilityRumors ??= new List<SocialRumor>();
        facilityReputationDebug ??= new List<SocialMemoryFloat>();
        return globalReputationLedger ??= new GlobalFacilityReputationLedger(
            globalFacilityRumors,
            facilityReputationDebug,
            RequireGameClock());
    }

    private void SynchronizeLedgerProjectionReferences()
    {
        GlobalFacilityReputationLedger ledger =
            RequireGlobalReputationLedger();
        globalFacilityRumors = ledger.Rumors;
        facilityReputationDebug = ledger.DebugProjection;
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

        return RequireGlobalReputationLedger().GetSentiment(
            building,
            globalReputationBlend);
    }

    public void ClearForDebug()
    {
        RequireGlobalReputationLedger().Clear();
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

        // V25: validation protects the prose channel, but mechanical reputation
        // was already committed from the rule-owned event facts. Never apply
        // model-authored type, spread, trust, target, or sentiment values here.
        lastError = string.Empty;
        lastRumorDebug = $"narrative-only: {rumor.summary}";
    }

    private bool ApplyRuleFacilityRumor(
        CharacterActor speaker,
        BuildableObject facility,
        float sentiment,
        string summary,
        string source)
    {
        if (speaker == null || facility == null || Mathf.Abs(sentiment) <= 0.01f)
        {
            return false;
        }

        float normalized = Mathf.Clamp(sentiment, -1f, 1f);
        SocialRumor rumor = new SocialRumor
        {
            type = normalized >= 0.5f
                ? SocialRumorType.Praise
                : normalized > 0f
                    ? SocialRumorType.Recommendation
                    : normalized <= -0.5f
                        ? SocialRumorType.Warning
                        : SocialRumorType.Complaint,
            targetType = SocialRumorTargetType.Facility,
            targetFacilityId = facility.id,
            sentiment = normalized,
            spreadChance = Mathf.Clamp01(0.25f + 0.35f * Mathf.Abs(normalized)),
            trustImpact = 0.25f * normalized,
            validUntil = RequireGameClock().Time + 120f,
            summary = string.IsNullOrWhiteSpace(summary)
                ? "시설 이용 경험"
                : summary.Trim(),
            source = source ?? "RuleSystem"
        };
        return ApplyRumor(rumor, speaker);
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

}
