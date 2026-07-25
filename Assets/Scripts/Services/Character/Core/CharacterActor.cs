using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BehaviorDesigner.Runtime;
using DungeonStory.Foundation;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

[DrawWithUnity]
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterIdentity))]
[RequireComponent(typeof(CharacterProgression))]
[RequireComponent(typeof(CharacterAbilityCache))]
[RequireComponent(typeof(CharacterStats))]
[RequireComponent(typeof(CharacterVisual))]
[RequireComponent(typeof(CharacterLifecycle))]
[RequireComponent(typeof(CharacterLog))]
[RequireComponent(typeof(CharacterBlackboard))]
[RequireComponent(typeof(CustomerPersonaRuntime))]
[RequireComponent(typeof(CharacterDialogueRuntime))]
[RequireComponent(typeof(CharacterSocialMemory))]
[RequireComponent(typeof(CharacterAiMemoryRuntime))]
[RequireComponent(typeof(BehaviorTree))]
public class CharacterActor : SerializedMonoBehaviour, IInfoable
{
    public GameObject noExit;

    private AIBrain brain;
    [SerializeField]
    [ReadOnly]
    private CharacterIdentity identity;
    [SerializeField]
    [ReadOnly]
    private CharacterProgression progression;
    [SerializeField]
    [ReadOnly]
    private CharacterAbilityCache abilityCache;
    [SerializeField]
    [ReadOnly]
    private CharacterStats characterStats;
    [SerializeField]
    [ReadOnly]
    private CharacterVisual visual;
    [SerializeField]
    [ReadOnly]
    private CharacterLifecycle lifecycle;
    [SerializeField]
    [ReadOnly]
    private CharacterLog characterLog;
    [SerializeField]
    [ReadOnly]
    private CharacterBlackboard blackboard;
    [SerializeField]
    [ReadOnly]
    private CustomerPersonaRuntime personaRuntime;
    [SerializeField]
    [ReadOnly]
    private CharacterDialogueRuntime dialogueRuntime;
    [SerializeField]
    [ReadOnly]
    private CharacterSocialMemory socialMemory;
    [SerializeField]
    [ReadOnly]
    private CharacterAiMemoryRuntime aiMemory;
    [SerializeField]
    [ReadOnly]
    private BehaviorTree behaviorTree;
    private ICharacterSocialMemoryFactory socialMemoryFactory;
    [SerializeField, ReadOnly] private CharacterActorRuntimeBridge runtimeBridge;
    [SerializeField, ReadOnly] private CharacterActorPresentationBridge presentationBridge;
    private bool persistentRestorePrepared;

    public CharacterDecisionState State { get; private set; }
    public CharacterDecisionState state
    {
        get => State;
        set => State = value;
    }
    public AIBrain Brain => brain;
    public AIBrain ai => brain;
    public CharacterIdentity Identity => identity;
    public CharacterProgression Progression => progression;
    public CharacterStats Stats => characterStats;
    public CharacterAbilityCache AbilityCache => abilityCache;
    public CharacterLifecycle Lifecycle => lifecycle;
    public CharacterLog LogComponent => characterLog;
    public CharacterBlackboard Blackboard => blackboard;
    public CustomerPersonaRuntime PersonaRuntime => personaRuntime;
    public CharacterDialogueRuntime DialogueRuntime => dialogueRuntime;
    public CharacterSocialMemory SocialMemory => socialMemory;
    public CharacterAiMemoryRuntime AiMemory => aiMemory;
    public BehaviorTree BehaviorTree => behaviorTree;
    public CharacterRuntimeProfile profile => progression != null
        ? progression.GetEffectiveRuntimeProfile()
        : identity != null ? identity.Profile : null;
    public CharacterRole Role => identity != null ? identity.Role : CharacterRole.Regular;
    public bool IsOwner => identity != null && identity.IsOwner;
    public bool CanLeaveByDissatisfaction => !IsOwner;
    public bool CanRebel => !IsOwner;
    public bool IsDead => CurrentLifecycleState == CharacterLifecycleState.Despawned
        || (characterStats != null && characterStats.IsDead);
    public bool IsOnExpedition => lifecycle != null
        && lifecycle.CurrentState == CharacterLifecycleState.OnExpedition;
    public CharacterLifecycleState CurrentLifecycleState => lifecycle != null
        ? lifecycle.CurrentState
        : CharacterLifecycleState.None;
    public bool CanRunAi => identity != null
        && identity.Data != null
        && lifecycle != null
        && lifecycle.CurrentState == CharacterLifecycleState.Active
        && !lifecycle.IsAiPaused;
    public bool IsAiDecisionPending => CanRunAi && brain != null && brain.isBestActionEnd;
    public float InjurySeverity => characterStats != null ? characterStats.InjurySeverity : 0f;
    public CharacterMoodSnapshot Mood => characterStats != null
        ? characterStats.GetMoodSnapshot()
        : new CharacterMoodSnapshot(
            CharacterMoodRules.DefaultBaseMood,
            CharacterMoodRules.DefaultBaseMood,
            Array.Empty<CharacterMoodFactorSnapshot>());
    public IReadOnlyList<string> Log
    {
        get
        {
            EnsureRuntimeState();
            return characterLog != null ? characterLog.Entries : Array.Empty<string>();
        }
    }
    public string SpeciesTag => identity != null ? identity.SpeciesTag : string.Empty;
    public Transform VisualRoot => visual != null ? visual.VisualRoot : null;
    public SpriteRenderer VisualRenderer => visual != null ? visual.VisualRenderer : null;
    internal IMainCameraProvider MainCameraProvider => presentationBridge?.MainCameraProvider;
    internal ITmpKoreanFontService TmpKoreanFontService => presentationBridge?.TmpKoreanFontService;
    public IGridPathSearchBroker PathSearchBroker => runtimeBridge?.PathSearchBroker;
    internal ICharacterAiWorldRegistry WorldRegistry => runtimeBridge?.WorldRegistry;
    internal ICharacterAiWorldSignalQuery WorldSignalQuery => runtimeBridge?.WorldSignalQuery;
    internal IWorldItemStackRuntime WorldItemStackRuntime => runtimeBridge?.WorldItemStackRuntime;
    internal ICharacterMedicalRuntime MedicalRuntime => runtimeBridge?.MedicalRuntime;
    internal ICharacterDeprivationRuntime DeprivationRuntime => runtimeBridge?.DeprivationRuntime;
    internal IGameClock GameClock => runtimeBridge?.GameClock;
    public event Action<CharacterActor, string> OnDied;

    [Inject]
    public void ConstructCharacterActor(
        IGridSystemProvider gridSystemProvider,
        ICharacterAiSchedulingService aiSchedulingService,
        IWorldInfoClickSelector worldInfoClickSelector,
        ICharacterSocialMemoryFactory socialMemoryFactory,
        ICharacterFeedbackBubbleFactory feedbackBubbleFactory,
        IMainCameraProvider mainCameraProvider,
        IGridPathSearchBroker pathSearchBroker,
        ICharacterAiWorldRegistry worldRegistry,
        ICharacterAiWorldSignalQuery worldSignalQuery,
        IWorldItemStackRuntime worldItemStackRuntime = null,
        IWildlifeRuntime wildlifeRuntime = null,
        ICharacterMedicalRuntime medicalRuntime = null,
        ICharacterDeprivationRuntime deprivationRuntime = null,
        IGameClock gameClock = null,
        ITmpKoreanFontService tmpKoreanFontService = null)
    {
        this.socialMemoryFactory = socialMemoryFactory
            ?? throw new ArgumentNullException(nameof(socialMemoryFactory));
        EnsureRuntimeBridges();
        runtimeBridge.Configure(
            this,
            gridSystemProvider,
            aiSchedulingService,
            pathSearchBroker,
            worldRegistry,
            worldSignalQuery,
            worldItemStackRuntime,
            wildlifeRuntime,
            medicalRuntime,
            deprivationRuntime,
            gameClock);
        presentationBridge.Configure(
            this,
            worldInfoClickSelector,
            feedbackBubbleFactory,
            mainCameraProvider,
            tmpKoreanFontService);
        if (worldItemStackRuntime != null)
        {
            CharacterCarryInventory.Ensure(this)?.Configure(
                worldItemStackRuntime.CatalogProvider,
                worldItemStackRuntime.HaulingSettingsProvider);
        }
        EnsureSocialMemory();
        AbilityHunt.Ensure(this, wildlifeRuntime);
        AbilityRescue.Ensure(this);
        runtimeBridge.OnActorEnabled();
    }

    public event Action<IReadOnlyDictionary<CharacterCondition, float>> OnStatChange
    {
        add
        {
            EnsureRuntimeState();
            if (characterStats != null)
            {
                characterStats.OnStatChange += value;
            }
        }
        remove
        {
            if (characterStats != null)
            {
                characterStats.OnStatChange -= value;
            }
        }
    }
    public event Action<CharacterLogEntry> OnLogAdded
    {
        add
        {
            EnsureRuntimeState();
            if (characterLog != null)
            {
                characterLog.OnLogAdded += value;
            }
        }
        remove
        {
            if (characterLog != null)
            {
                characterLog.OnLogAdded -= value;
            }
        }
    }

    public IDictionary<CharacterCondition, float> stats
    {
        get
        {
            EnsureRuntimeState();
            return characterStats != null ? characterStats.Stats : null;
        }
        set
        {
            EnsureRuntimeState();
            if (characterStats != null)
            {
                characterStats.Stats = value;
            }
        }
    }

    public CharacterSO data
    {
        get
        {
            EnsureRuntimeState();
            return identity != null ? identity.Data : null;
        }
        set
        {
            EnsureRuntimeState();
            identity?.SetData(value);
        }
    }

    public CharacterType characterType
    {
        get
        {
            EnsureRuntimeState();
            return identity != null ? identity.CharacterType : CharacterType.Customer;
        }
        set
        {
            EnsureRuntimeState();
            identity?.SetCharacterType(value);
        }
    }

    public static CharacterActor From(Component component)
    {
        return component != null ? component.GetComponent<CharacterActor>() : null;
    }

    private void Awake()
    {
        EnsureRuntimeState();
        OrganizeRuntimeHierarchy();
        abilityCache?.CacheAbility();
    }

    private void Start()
    {
        EnsureRuntimeState();
        runtimeBridge?.RequireConfigured();
        runtimeBridge?.OnActorEnabled();
        State = CharacterDecisionState.DECIDE;
        bool isPersistentRestore = persistentRestorePrepared;
        if (identity != null && identity.Data != null)
        {
            if (!isPersistentRestore)
            {
                Initialize(identity.Data);
            }

            if (lifecycle != null && lifecycle.CurrentState == CharacterLifecycleState.None)
            {
                lifecycle.SetLifecycleState(CharacterLifecycleState.Active);
            }

            if (!isPersistentRestore)
            {
                StartCoroutine(lifecycle != null ? lifecycle.SnapToWalkableGridWhenReady() : EmptyRoutine());
            }
        }

        persistentRestorePrepared = false;
        StartCoroutine(characterStats != null ? characterStats.ChangeStatByTick() : EmptyRoutine());
    }

    private void Update()
    {
        visual?.RecoverExpiredTraversalVisibility();
    }

    private void OnEnable()
    {
        runtimeBridge?.OnActorEnabled();
    }

    private void OnDisable()
    {
        visual?.RestoreTraversalVisibility();
        runtimeBridge?.OnActorDisabled();
    }

    private void OnDestroy()
    {
        runtimeBridge?.OnActorDestroyed();
    }

    private void OrganizeRuntimeHierarchy()
    {
        if (!Application.isPlaying || transform.parent != null)
        {
            return;
        }

        DungeonRuntimeHierarchy.Parent(gameObject, DungeonRuntimeHierarchy.Characters);
    }

    public void Initialize(CharacterSO data)
    {
        EnsureRuntimeState();
        if (!HasRuntimeComponents)
        {
            return;
        }

        identity.SetData(data);
        progression.Bind(this);
        if (identity.Data != null)
        {
            visual.SetCharacterSprite(identity.Data.characterSprite);
        }

        characterStats.RecalculateVitals(resetCurrentHealth: true);
        abilityCache.CacheAbility();
        foreach (CharacterAbility ability in abilityCache.Abilities)
        {
            ability.Initializtion(data);
        }

        personaRuntime.RequestPersonaIfNeeded(logIfMissingQueue: false);
    }

    public void PrepareForPersistentRestore()
    {
        persistentRestorePrepared = true;
    }

    public bool TryExecuteSelectedAiAction()
    {
        EnsureRuntimeState();
        if (brain == null || brain.bestAction == null || brain.bestAction.actionset == null)
        {
            return false;
        }

        AIAction selectedAction = brain.bestAction;
        string actionName = !string.IsNullOrWhiteSpace(selectedAction.actionset.actionName)
            ? selectedAction.actionset.actionName
            : selectedAction.actionset.GetType().Name;
        brain.NotifyActionStarted();
        blackboard?.Commit(selectedAction, actionName);
        selectedAction.actionset.Execute(this);
        return true;
    }

    public List<BuildableObject> GetReachableBuilding()
    {
        EnsureRuntimeState();
        if (!TryGetGrid(out Grid grid))
        {
            return new List<BuildableObject>();
        }

        GridPathSearchResult searchResult = brain != null
            ? brain.GetPathSearch(this)
            : null;
        if (searchResult != null)
        {
            return searchResult.GetAllVisitableBuilding();
        }

        Vector2Int pos = lifecycle != null ? lifecycle.GetNowXY() : Vector2Int.zero;
        return grid.GetAllVisitableBuilding(pos).ToList();
    }

    private bool TryGetGrid(out Grid grid)
    {
        if (runtimeBridge == null)
        {
            grid = null;
            return false;
        }

        return runtimeBridge.TryGetGrid(out grid);
    }

    public void EnsureRuntimeState()
    {
        EnsureRuntimeBridges();
        if (brain == null)
        {
            brain = GetComponent<AIBrain>();
        }

        identity = GetComponent<CharacterIdentity>();
        progression = GetComponent<CharacterProgression>();
        if (progression == null && Application.isPlaying)
        {
            progression = gameObject.AddComponent<CharacterProgression>();
        }
        CharacterCarryInventory.Ensure(this);
        AbilityHaul.Ensure(this);
        AbilityHunt.Ensure(this, runtimeBridge?.WildlifeRuntime);
        abilityCache = GetComponent<CharacterAbilityCache>();
        characterStats = GetComponent<CharacterStats>();
        visual = GetComponent<CharacterVisual>();
        lifecycle = GetComponent<CharacterLifecycle>();
        characterLog = GetComponent<CharacterLog>();
        blackboard = GetComponent<CharacterBlackboard>();
        personaRuntime = GetComponent<CustomerPersonaRuntime>();
        dialogueRuntime = GetComponent<CharacterDialogueRuntime>();
        aiMemory = GetComponent<CharacterAiMemoryRuntime>();
        if (aiMemory == null && Application.isPlaying)
        {
            aiMemory = gameObject.AddComponent<CharacterAiMemoryRuntime>();
        }
        EnsureSocialMemory();

        behaviorTree = GetComponent<BehaviorTree>();
        if (behaviorTree != null)
        {
            behaviorTree.StartWhenEnabled = false;
        }

        if (!HasRuntimeComponents)
        {
            Debug.LogError($"{name}: CharacterActor component split is incomplete. Fix the prefab components.", this);
            return;
        }

        identity.Bind(this);
        progression.Bind(this);
        characterStats.Bind(this);
        lifecycle.Bind(this);
        blackboard.Bind(this);
        personaRuntime.Bind(this);
        socialMemory.Bind(this);
        aiMemory.Bind(this);

        visual.Bind();
        characterLog.Bind();
        presentationBridge?.EnsurePresentation();
    }

    public T GetAbility<T>() where T : CharacterAbility
    {
        EnsureRuntimeState();
        return abilityCache != null ? abilityCache.GetAbility<T>() : null;
    }

    public bool TryGetAbility<T>(out T result) where T : CharacterAbility
    {
        EnsureRuntimeState();
        if (abilityCache != null)
        {
            return abilityCache.TryGetAbility(out result);
        }

        result = null;
        return false;
    }

    public Vector2Int GetNowXY()
    {
        EnsureRuntimeState();
        return lifecycle != null ? lifecycle.GetNowXY() : Vector2Int.zero;
    }

    public void AddLog(string message)
    {
        EnsureRuntimeState();
        characterLog?.AddLog(message);
    }

    public void AddActivity(CharacterActivityEvent activity)
    {
        EnsureRuntimeState();
        characterLog?.AddActivity(activity);
        if (activity != null
            && activity.NarrativeEligible
            && !string.Equals(activity.OutcomeId, CharacterActivityOutcomes.Started, StringComparison.Ordinal)
            && !string.Equals(activity.OutcomeId, CharacterActivityOutcomes.Progress, StringComparison.Ordinal))
        {
            progression?.RecordNarrative(
                CharacterNarrativeDomainUtility.FromActivity(activity),
                !string.IsNullOrWhiteSpace(activity.ActionId) ? activity.ActionId : activity.KindId,
                !string.IsNullOrWhiteSpace(activity.TargetId) ? activity.TargetId : activity.PlaceId,
                activity.OutcomeId,
                !Mathf.Approximately(activity.Value, 0f) ? activity.Value : activity.Quantity);
        }
    }

    public float GetMoveSpeed()
    {
        EnsureRuntimeState();
        float baseSpeed = characterStats != null
            ? characterStats.GetMoveSpeed()
            : identity != null && identity.Data != null
                ? identity.Data.moveSpeed
                : 1f;
        CharacterCarryInventory carry = GetComponent<CharacterCarryInventory>();
        return baseSpeed * (carry != null ? carry.GetMoveSpeedMultiplier() : 1f);
    }

    public float GetConsumptionMultiplier()
    {
        EnsureRuntimeState();
        return characterStats != null ? characterStats.GetConsumptionMultiplier() : 1f;
    }

    public float GetStayDurationMultiplier()
    {
        EnsureRuntimeState();
        return characterStats != null ? characterStats.GetStayDurationMultiplier() : 1f;
    }

    public float GetCrowdSensitivityMultiplier()
    {
        EnsureRuntimeState();
        return characterStats != null ? characterStats.GetCrowdSensitivityMultiplier() : 1f;
    }

    public float GetWorkSpeedMultiplier(WorkTypeId workTypeId)
    {
        EnsureRuntimeState();
        return characterStats != null ? characterStats.GetWorkSpeedMultiplier(workTypeId) : 1f;
    }

    public float GetWorkPreferenceScore(WorkTypeId workTypeId)
    {
        EnsureRuntimeState();
        return characterStats != null ? characterStats.GetWorkPreferenceScore(workTypeId) : 0.5f;
    }

    public float GetFacilityPreferenceScore(FacilityRole roles)
    {
        EnsureRuntimeState();
        return characterStats != null ? characterStats.GetFacilityPreferenceScore(roles) : 0.5f;
    }

    public float GetAccidentChanceMultiplier()
    {
        EnsureRuntimeState();
        return characterStats != null ? characterStats.GetAccidentChanceMultiplier() : 1f;
    }

    public CharacterSpeciesIncidentType GetIncidentType()
    {
        EnsureRuntimeState();
        return characterStats != null ? characterStats.GetIncidentType() : CharacterSpeciesIncidentType.None;
    }

    public float GetCrimeRiskMultiplier()
    {
        EnsureRuntimeState();
        return characterStats != null ? characterStats.GetCrimeRiskMultiplier() : 1f;
    }

    public float GetCombatPowerMultiplier()
    {
        EnsureRuntimeState();
        return characterStats != null ? characterStats.GetCombatPowerMultiplier() : 1f;
    }

    public float GetFatigueEfficiencyMultiplier()
    {
        EnsureRuntimeState();
        return characterStats != null ? characterStats.GetFatigueEfficiencyMultiplier() : 1f;
    }

    public float GetInjuryEfficiencyMultiplier()
    {
        EnsureRuntimeState();
        return characterStats != null ? characterStats.GetInjuryEfficiencyMultiplier() : 1f;
    }

    public float MaxHealth
    {
        get
        {
            EnsureRuntimeState();
            return characterStats != null ? characterStats.MaxHealth : 100f;
        }
    }

    public float CurrentHealth
    {
        get
        {
            EnsureRuntimeState();
            return characterStats != null ? characterStats.CurrentHealth : 100f;
        }
    }

    public void Initialization(CharacterSO data)
    {
        Initialize(data);
    }

    public void CacheAbility()
    {
        EnsureRuntimeState();
        abilityCache?.CacheAbility();
    }

    public void RefreshAbilityCache()
    {
        EnsureRuntimeState();
        abilityCache?.RefreshAbilityCache();
    }

    public IEnumerator ChangeStatByTick()
    {
        EnsureRuntimeState();
        return characterStats != null ? characterStats.ChangeStatByTick() : EmptyRoutine();
    }

    public void ChangesStat(CharacterCondition condition, float value)
    {
        EnsureRuntimeState();
        characterStats?.ChangesStat(condition, value);
    }

    public void ApplyMoodFactor(
        string id,
        string label,
        float value,
        float durationSeconds = 180f,
        int maxStacks = 1)
    {
        EnsureRuntimeState();
        characterStats?.ApplyMoodFactor(id, label, value, durationSeconds, maxStacks);
    }

    public int GetCharacterStat(CharacterStatType statType)
    {
        EnsureRuntimeState();
        return characterStats != null ? characterStats.GetCharacterStat(statType) : 5;
    }

    public int GetCharacterStat(string statId)
    {
        EnsureRuntimeState();
        return characterStats != null ? characterStats.GetCharacterStat(statId) : 0;
    }

    public void ApplyDamage(float amount, string reason = "")
    {
        EnsureRuntimeState();
        characterStats?.ApplyDamage(amount, reason);
    }

    public void Heal(float amount)
    {
        EnsureRuntimeState();
        characterStats?.Heal(amount);
    }

    public void ScaleMaxHealth(float multiplier)
    {
        EnsureRuntimeState();
        characterStats?.ScaleMaxHealth(multiplier);
    }

    public void SetInjurySeverity(float value)
    {
        EnsureRuntimeState();
        characterStats?.SetInjurySeverity(value);
    }

    public void Die(string reason = "")
    {
        EnsureRuntimeState();
        characterStats?.Die(reason);
    }

    public void InitializeStats(bool resetCurrentHealth)
    {
        EnsureRuntimeState();
        characterStats?.RecalculateVitals(resetCurrentHealth);
    }

    public void SetLifecycleState(CharacterLifecycleState nextState)
    {
        EnsureRuntimeState();
        lifecycle?.SetLifecycleState(nextState);
    }

    public bool BeginExpedition()
    {
        EnsureRuntimeState();
        return lifecycle != null && lifecycle.BeginExpedition();
    }

    public void EndExpedition(bool alive = true)
    {
        EnsureRuntimeState();
        lifecycle?.EndExpedition(alive);
    }

    public void ChangeLayer(string layer)
    {
        EnsureRuntimeState();
        visual?.ChangeLayer(layer);
    }

    public void ApplyVisualFootAnchor()
    {
        EnsureRuntimeState();
        visual?.ApplyVisualFootAnchor();
    }

    public float GetVisualTopLocalY()
    {
        EnsureRuntimeState();
        return visual != null ? visual.GetVisualTopLocalY() : 1f;
    }

    public void DoFade(float alpha, float duration)
    {
        EnsureRuntimeState();
        visual?.DoFade(alpha, duration);
    }

    public void Flip(CharacterFacing facing)
    {
        EnsureRuntimeState();
        visual?.Flip(facing);
    }

    public void HideForTraversal(float failSafeSeconds)
    {
        EnsureRuntimeState();
        visual?.HideForTraversal(failSafeSeconds);
    }

    public void RestoreTraversalVisibility()
    {
        EnsureRuntimeState();
        visual?.RestoreTraversalVisibility();
    }

    public void SetAiPaused(bool value)
    {
        EnsureRuntimeState();
        lifecycle?.SetAiPaused(value);
    }

    public bool IsAiPaused()
    {
        EnsureRuntimeState();
        return lifecycle != null && lifecycle.IsAiPaused;
    }

    public string GetSpeciesShortDescription()
    {
        EnsureRuntimeState();
        return identity != null ? identity.GetSpeciesShortDescription() : string.Empty;
    }

    internal void RaiseDied(string reason)
    {
        OnDied?.Invoke(this, reason);
    }

    private bool HasRuntimeComponents => identity != null
        && progression != null
        && abilityCache != null
        && characterStats != null
        && visual != null
        && lifecycle != null
        && characterLog != null
        && blackboard != null
        && personaRuntime != null
        && dialogueRuntime != null
        && socialMemory != null
        && aiMemory != null;

    private void EnsureSocialMemory()
    {
        socialMemory = socialMemoryFactory != null
            ? socialMemoryFactory.GetOrAdd(this)
            : GetComponent<CharacterSocialMemory>();
    }

    private void EnsureRuntimeBridges()
    {
        if (runtimeBridge == null)
        {
            runtimeBridge = GetComponent<CharacterActorRuntimeBridge>();
            if (runtimeBridge == null)
            {
                runtimeBridge = gameObject.AddComponent<CharacterActorRuntimeBridge>();
            }
        }

        if (presentationBridge == null)
        {
            presentationBridge = GetComponent<CharacterActorPresentationBridge>();
            if (presentationBridge == null)
            {
                presentationBridge = gameObject.AddComponent<CharacterActorPresentationBridge>();
            }
        }
    }

    private static IEnumerator EmptyRoutine()
    {
        yield break;
    }
}
