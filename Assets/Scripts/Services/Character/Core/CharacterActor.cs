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
public class CharacterActor : SerializedMonoBehaviour,
    IInfoable,
    IBuildingCharacterPort,
    IInvasionThreatSubject,
    ICharacterMovementKinematicsActor
{
    public GameObject noExit;

    private readonly CharacterActorLifecycleCoordinator lifecycleCoordinator = new();
    private readonly CharacterActorAbilityBridge abilityBridge = new();
    private readonly CharacterActorActivityBridge activityBridge = new();

    private AIBrain brain;
    [SerializeField, ReadOnly] private CharacterIdentity identity;
    [SerializeField, ReadOnly] private CharacterProgression progression;
    [SerializeField, ReadOnly] private CharacterAbilityCache abilityCache;
    [SerializeField, ReadOnly] private CharacterStats characterStats;
    [SerializeField, ReadOnly] private CharacterVisual visual;
    [SerializeField, ReadOnly] private CharacterLifecycle lifecycle;
    [SerializeField, ReadOnly] private CharacterLog characterLog;
    [SerializeField, ReadOnly] private CharacterBlackboard blackboard;
    [SerializeField, ReadOnly] private CustomerPersonaRuntime personaRuntime;
    [SerializeField, ReadOnly] private CharacterDialogueRuntime dialogueRuntime;
    [SerializeField, ReadOnly] private CharacterSocialMemory socialMemory;
    [SerializeField, ReadOnly] private CharacterAiMemoryRuntime aiMemory;
    [SerializeField, ReadOnly] private BehaviorTree behaviorTree;
    private CharacterCarryInventory carryInventory;
    private ICharacterSocialMemoryFactory socialMemoryFactory;
    private ICharacterRuntimeProfileFactory runtimeProfileFactory;
    private CharacterMoodPolicyService moodPolicy;
    [SerializeField, ReadOnly] private CharacterActorRuntimeBridge runtimeBridge;
    [SerializeField, ReadOnly] private CharacterActorPresentationBridge presentationBridge;
    private bool runtimeStateInitialized;
    private bool runtimeStateInitializing;
    private bool explicitInitializationCompleted;
    private bool transientAiOwnershipReleased;
    private int transientAiOwnershipReleaseAttemptCount;
    private int repeatedWorkOwnershipCleanupCount;
    private string lastTransientAiOwnershipReleaseReason = string.Empty;

    public CharacterDecisionState State { get; private set; }
    public CharacterDecisionState state
    {
        get => State;
        set => State = value;
    }
    public AIBrain Brain => brain;
    public AIBrain ai => brain;
    public CharacterIdentity Identity => identity;
    public CharacterId BuildingCharacterId =>
        identity != null ? identity.TypedPersistentId : default;
    public string BuildingDisplayName => this != null ? name : string.Empty;
    public InvasionThreatSubjectSnapshot CaptureInvasionThreatSubject() =>
        new(BuildingCharacterId, BuildingDisplayName);
    public bool IsBuildingInteractionAvailable =>
        this != null
        && isActiveAndEnabled
        && CurrentLifecycleState == CharacterLifecycleState.Active
        && !IsDead;
    public IBuildingVisitorPort BuildingVisitor
    {
        get
        {
            EnsureRuntimeBridges();
            return runtimeBridge.GetBuildingVisitor(this);
        }
    }
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
        ? progression.GetEffectiveRuntimeProfile() : identity?.Profile;
    public CharacterRole Role => identity != null ? identity.Role : CharacterRole.Regular;
    public bool IsOwner => identity != null && identity.IsOwner;
    public bool CanLeaveByDissatisfaction => !IsOwner;
    public bool CanRebel => !IsOwner;
    public bool IsDead => CurrentLifecycleState == CharacterLifecycleState.Despawned
        || characterStats?.IsDead == true;
    public bool IsOnExpedition => lifecycle?.CurrentState
        == CharacterLifecycleState.OnExpedition;
    public CharacterLifecycleState CurrentLifecycleState => lifecycle != null
        ? lifecycle.CurrentState : CharacterLifecycleState.None;
    public bool TransientAiOwnershipReleasedForDiagnostics =>
        transientAiOwnershipReleased;
    public int TransientAiOwnershipReleaseAttemptCountForDiagnostics =>
        transientAiOwnershipReleaseAttemptCount;
    public int RepeatedWorkOwnershipCleanupCountForDiagnostics =>
        repeatedWorkOwnershipCleanupCount;
    public string LastTransientAiOwnershipReleaseReasonForDiagnostics =>
        lastTransientAiOwnershipReleaseReason;
    public bool CanRunAi => identity != null
        && identity.Data != null
        && lifecycle != null
        && lifecycle.CurrentState == CharacterLifecycleState.Active
        && !lifecycle.IsAiPaused
        && gameObject.activeInHierarchy
        && !IsDetachedRestoreCandidate
        && !IsUnpublishedComposition;
    public bool IsAiDecisionPending => CanRunAi && brain != null && brain.isBestActionEnd;
    public bool IsUnpublishedComposition =>
        lifecycleCoordinator.IsUnpublishedComposition;
    public bool IsDetachedRestoreCandidate =>
        lifecycleCoordinator.IsDetachedRestoreCandidate;
    public bool HasBeenPublished => lifecycleCoordinator.HasBeenPublished;
    internal bool IsRuntimeBridgeConfigured => runtimeBridge?.IsConfigured == true;
    public float InjurySeverity => characterStats != null ? characterStats.InjurySeverity : 0f;
    public CharacterMoodSnapshot Mood =>
        CharacterActorRuntimeFacade.GetMood(characterStats);
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
    internal IDynamicFrameWorkBudget FrameWorkBudget =>
        presentationBridge?.FrameWorkBudget;
    public IGridPathSearchBroker PathSearchBroker => runtimeBridge?.PathSearchBroker;
    internal ICharacterAiWorldRegistry WorldRegistry => runtimeBridge?.WorldRegistry;
    internal ICharacterAiWorldSignalQuery WorldSignalQuery => runtimeBridge?.WorldSignalQuery;
    internal bool ShouldCollectDetailedAiDiagnostics =>
        runtimeBridge == null || runtimeBridge.ShouldCollectDetailedAiDiagnostics;
    internal IWorldItemStackRuntime WorldItemStackRuntime => runtimeBridge?.WorldItemStackRuntime;
    internal ICharacterMedicalQuery MedicalQuery => runtimeBridge?.MedicalQuery;
    internal ICharacterMedicalCommand MedicalCommands => runtimeBridge?.MedicalCommands;
    internal ICharacterDeprivationRuntime DeprivationRuntime => runtimeBridge?.DeprivationRuntime;
    internal ICharacterDeprivationQuery DeprivationQuery => runtimeBridge?.DeprivationRuntime;
    internal ICharacterDeprivationCommand DeprivationCommands => runtimeBridge?.DeprivationRuntime;
    internal ICharacterSubstanceRuntime SubstanceRuntime =>
        runtimeBridge?.SubstanceRuntime;
    internal IGameClock GameClock => runtimeBridge?.GameClock;
    internal IWorkAmountCalculator WorkAmountCalculator =>
        runtimeBridge?.WorkAmountCalculator;
    internal CharacterAiNaturalnessSettingsSO NaturalnessSettings { get; private set; }
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
        IDynamicFrameWorkBudget frameWorkBudget,
        ICharacterRuntimeTransientStateRegistry transientStateRegistry,
        ICharacterIdRegistry characterIdRegistry,
        IGameContentCatalog gameContentCatalog,
        IDungeonUserSettingsService userSettings,
        IWorldItemStackRuntime worldItemStackRuntime,
        IWildlifeRuntime wildlifeRuntime,
        ICharacterMedicalQuery medicalQuery,
        ICharacterMedicalCommand medicalCommands,
        ICharacterDeprivationRuntime deprivationRuntime,
        ICharacterSubstanceRuntime substanceRuntime,
        ICharacterMealOperationCancellation mealOperationCancellation,
        IGameClock gameClock,
        IWorkAmountCalculator workAmountCalculator,
        ITmpKoreanFontService tmpKoreanFontService,
        ICharacterPresentationScheduler presentationScheduler,
        ICharacterRuntimeProfileFactory runtimeProfileFactory,
        CharacterMoodPolicyService moodPolicy)
    {
        this.runtimeProfileFactory = runtimeProfileFactory
            ?? throw new ArgumentNullException(nameof(runtimeProfileFactory));
        this.socialMemoryFactory = socialMemoryFactory
            ?? throw new ArgumentNullException(nameof(socialMemoryFactory));
        this.moodPolicy = moodPolicy
            ?? throw new ArgumentNullException(nameof(moodPolicy));
        NaturalnessSettings = (gameContentCatalog ?? throw new ArgumentNullException(nameof(gameContentCatalog))).RequireSingle<CharacterAiNaturalnessSettingsSO>();
        EnsureRuntimeState();
        if (identity.Data != null && identity.Profile == null)
        {
            identity.SetData(
                identity.Data,
                this.runtimeProfileFactory.Create(
                    CharacterSpawnRequest.FromAuthoring(identity.Data)));
        }
        (characterIdRegistry
            ?? throw new ArgumentNullException(nameof(characterIdRegistry)))
            .GetOrAssignPersistentId(this);
        CharacterSkillTransientState.Ensure(this).Configure(
            transientStateRegistry
                ?? throw new ArgumentNullException(nameof(transientStateRegistry)),
            Identity.TypedPersistentId);
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
            medicalQuery,
            medicalCommands,
            deprivationRuntime,
            substanceRuntime,
            mealOperationCancellation,
            gameClock,
            workAmountCalculator);
        presentationBridge.Configure(
            this,
            worldInfoClickSelector,
            feedbackBubbleFactory,
            mainCameraProvider,
            frameWorkBudget,
            gameContentCatalog.WorldPresentation,
            userSettings,
            tmpKoreanFontService,
            presentationScheduler,
            gameClock);
        if (worldItemStackRuntime != null)
        {
            CharacterCarryInventory.Ensure(this)?.Configure(
                worldItemStackRuntime.CatalogProvider,
                worldItemStackRuntime.HaulingSettingsProvider,
                transientStateRegistry);
        }
        EnsureSocialMemory();
        abilityBridge.EnsureInjectedAbilities(this, wildlifeRuntime);
        transientAiOwnershipReleased = false;
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
            RequireRuntimeProfileFactory();
            identity?.SetData(
                value,
                value != null
                    ? runtimeProfileFactory.Create(
                        CharacterSpawnRequest.FromAuthoring(value))
                    : null);
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
        lifecycleCoordinator.Start(
            this,
            identity,
            lifecycle,
            characterStats,
            runtimeBridge,
            explicitInitializationCompleted);
    }

    internal void TickPresentationMaintenance()
    {
        lifecycleCoordinator.TickPresentation(
            presentationBridge,
            visual,
            GameClock);
    }

    private void OnEnable()
    {
        if (CurrentLifecycleState == CharacterLifecycleState.Active)
        {
            transientAiOwnershipReleased = false;
        }
        lifecycleCoordinator.OnEnabled(
            runtimeBridge,
            presentationBridge,
            personaRuntime);
    }

    private void OnDisable()
    {
        ReleaseTransientAiOwnership("character-game-object-disabled");
        lifecycleCoordinator.OnDisabled(
            this,
            visual,
            runtimeBridge,
            presentationBridge);
    }

    private void OnDestroy()
    {
        ReleaseTransientAiOwnership("character-game-object-destroyed");
        lifecycleCoordinator.OnDestroyed(this, runtimeBridge, presentationBridge);
    }

    internal void PrepareForScopeTeardown()
    {
        // A managed-domain reload rebuilds the scene scope while preserving
        // active Unity objects. Do not SetActive(false): that serialized state
        // would strand the old actor and make the next scope create a duplicate
        // persistent character. Release only scope-owned runtime edges while
        // their services are still alive; the new scope will inject and
        // register this same active object again.
        ReleaseTransientAiOwnership("character-runtime-scope-disposed");
        lifecycleCoordinator.OnDisabled(
            this,
            visual,
            runtimeBridge,
            presentationBridge);
    }

    public void ReleaseTransientAiOwnership(string reason)
    {
        string normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? "character-lifecycle-ended"
            : reason.Trim();
        transientAiOwnershipReleaseAttemptCount = checked(
            transientAiOwnershipReleaseAttemptCount + 1);
        lastTransientAiOwnershipReleaseReason = normalizedReason;

        if (transientAiOwnershipReleased)
        {
            // The lifecycle boundary is idempotent, but an already scheduled
            // AIWork commit can race the first cleanup and reacquire a facility
            // reservation before the next frame. Re-run the work-owned cleanup
            // on repeated lifecycle notifications so that stale coroutine
            // commits cannot survive Downed/Despawned/disable transitions.
            AbilityWork repeatedWork = GetComponent<AbilityWork>();
            if (repeatedWork != null
                && (repeatedWork.isWorking
                    || repeatedWork.HasActiveWorkRoutineForDiagnostics))
            {
                repeatedWorkOwnershipCleanupCount = checked(
                    repeatedWorkOwnershipCleanupCount + 1);
                repeatedWork.StopAssignedWorkFromAi(normalizedReason);
            }
            return;
        }
        transientAiOwnershipReleased = true;

        Brain?.StopAllAiForLifecycleTransition(normalizedReason);
        GetComponent<AbilityCaptiveEscape>()?.StopForLifecycleTransition(
            normalizedReason);
        GetComponent<AbilityWildlifeCaptureTransport>()
            ?.StopForLifecycleTransition(normalizedReason);
        GetComponent<AbilityMove>()?.CancelActiveMovement();
        GetComponent<AbilityWork>()?.StopAssignedWorkFromAi(normalizedReason);
        GetComponent<AbilityShopping>()?.StopShopping(normalizedReason);
        GetComponent<AbilityHaul>()?.StopHauling(normalizedReason);
        GetComponent<AbilityRescue>()?.StopRescue(
            CharacterMedicalStatusCode.RescueInterrupted,
            $"lifecycle-release:{normalizedReason}");
        GetComponent<AbilityHunt>()?.StopHunting(normalizedReason);
        GetComponent<AbilityUseSubstance>()?.StopUse(normalizedReason);
        runtimeBridge?.MealOperationCancellation?.CancelActiveMealOperations(
            this,
            normalizedReason);

        IBuildingVisitorPort visitor = BuildingVisitor;
        if (visitor == null)
        {
            return;
        }
        WorldRegistry?.ReleaseTransientBuildingOwnership(
            visitor,
            normalizedReason);
    }

    internal void PrepareTransientAiOwnershipForActiveLifecycle() =>
        transientAiOwnershipReleased = false;

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
        Initialize(
            data,
            data != null
                ? CharacterSpawnRequest.FromAuthoring(data)
                : null);
    }

    public void Initialize(
        CharacterSO data,
        CharacterSpawnRequest spawnRequest)
    {
        EnsureRuntimeState();
        if (!HasRuntimeComponents)
        {
            return;
        }

        RequireRuntimeProfileFactory();
        identity.SetData(
            data,
            data != null
                ? runtimeProfileFactory.Create(
                    spawnRequest ?? throw new ArgumentNullException(nameof(spawnRequest)))
                : null);
        progression.Bind(this);
        if (identity.Data != null)
        {
            visual.SetCharacterSprite(identity.Data.characterSprite);
        }

        characterStats.RecalculateVitals(resetCurrentHealth: true);
        abilityBridge.Initialize(abilityCache, data);
        explicitInitializationCompleted = true;
        lifecycleCoordinator.MarkInitializedBeforeFirstStart();

        if (!IsDetachedRestoreCandidate && !IsUnpublishedComposition)
        {
            personaRuntime.RequestPersonaIfNeeded(logIfMissingQueue: false);
        }
    }

    private void RequireRuntimeProfileFactory()
    {
        if (runtimeProfileFactory == null)
        {
            throw new InvalidOperationException(
                "CharacterActor requires ICharacterRuntimeProfileFactory before assigning character data.");
        }
    }

    public void PrepareForComposition()
    {
        EnsureRuntimeBridges();
        if (lifecycle == null)
        {
            lifecycle = GetComponent<CharacterLifecycle>();
        }
        lifecycleCoordinator.PrepareForComposition(
            this,
            lifecycle,
            runtimeBridge,
            presentationBridge);
    }

    internal void RequireCompositionReadyForPublication()
    {
        lifecycleCoordinator.RequireCompositionReadyForPublication(
            identity,
            lifecycle,
            runtimeBridge,
            presentationBridge);
    }

    internal void RequireReadyForPublishedReactivation()
    {
        if (!HasBeenPublished
            || IsUnpublishedComposition
            || IsDetachedRestoreCandidate
            || !IsRuntimeBridgeConfigured
            || identity == null
            || identity.Data == null
            || !identity.TypedPersistentId.IsValid)
        {
            throw new InvalidOperationException(
                "Only a complete, previously published character can be reactivated.");
        }
    }

    public void PublishComposition()
    {
        lifecycleCoordinator.PublishComposition(
            identity,
            lifecycle,
            runtimeBridge,
            presentationBridge);
    }

    public void PrepareForDetachedRestore()
    {
        EnsureRuntimeBridges();
        if (lifecycle == null)
        {
            lifecycle = GetComponent<CharacterLifecycle>();
        }
        lifecycleCoordinator.PrepareForDetachedRestore(
            this,
            lifecycle,
            runtimeBridge,
            presentationBridge);
    }

    public void PublishDetachedRestore()
    {
        RequireDetachedReadyForPublication();
        lifecycleCoordinator.PublishDetachedRestore(
            lifecycle,
            runtimeBridge,
            presentationBridge);
    }

    internal void ReconcilePublishedRuntimeRegistration()
    {
        if (!HasBeenPublished || IsDetachedRestoreCandidate || IsUnpublishedComposition)
        {
            throw new InvalidOperationException(
                "Only a published character can reconcile runtime registration.");
        }

        runtimeBridge.ReconcilePublishedRegistration();
    }

    internal void RollbackDetachedRestorePublication()
    {
        lifecycleCoordinator.RollbackDetachedRestorePublication(
            lifecycle,
            runtimeBridge,
            presentationBridge);
    }

    internal void RequireDetachedReadyForPublication()
    {
        lifecycleCoordinator.RequireDetachedReadyForPublication(
            identity,
            lifecycle,
            runtimeBridge,
            presentationBridge);
    }

    public void PrepareForPersistentRestore()
    {
        lifecycleCoordinator.PrepareForPersistentRestore(presentationBridge);
    }

    public bool TryExecuteSelectedAiAction()
    {
        return TryExecuteSelectedAiAction(out _);
    }

    internal bool TryExecuteSelectedAiAction(out AIActionFailure failure)
    {
        EnsureRuntimeState();
        if (brain == null)
        {
            failure = AIActionFailure.Create(
                AIActionFailureKind.NoAction,
                "AIBrain is missing.");
            return false;
        }

        if (!CanRunAi)
        {
            failure = AIActionFailure.Create(
                AIActionFailureKind.CannotStart,
                $"AI cannot execute in lifecycle state {CurrentLifecycleState}.");
            return false;
        }

        if (brain.bestAction == null || brain.bestAction.actionset == null)
        {
            failure = AIActionFailure.Create(
                AIActionFailureKind.NoAction,
                "No selected AI action is available for execution.");
            return false;
        }

        AIAction selectedAction = brain.bestAction;
        if (selectedAction.HasStarted || brain.isExecuted)
        {
            // A multi-frame behavior-tree leaf is RUNNING after the first Execute.
            // Re-entering Execute would start a second coroutine for the same
            // intent. Treat the repeated request as an idempotent acknowledgement,
            // retain the valid running action, and expose exact diagnostics.
            brain.NotifyDuplicateExecutionSuppressed(selectedAction);
            failure = AIActionFailure.None;
            return true;
        }

        BuildableObject selectedDestination = selectedAction.destination;
        if (!ReferenceEquals(selectedDestination, null)
            && (selectedDestination == null || selectedDestination.isDestroy))
        {
            failure = AIActionFailure.Create(
                AIActionFailureKind.Destroyed,
                "Selected destination was destroyed before execution.",
                selectedDestination);
            return false;
        }

        string actionName = !string.IsNullOrWhiteSpace(selectedAction.actionset.actionName)
            ? selectedAction.actionset.actionName
            : selectedAction.actionset.GetType().Name;
        brain.NotifyActionStarted();
        blackboard?.Commit(selectedAction, actionName);
        try
        {
            selectedAction.actionset.Execute(this);
            failure = AIActionFailure.None;
            return true;
        }
        catch (System.Exception exception)
        {
            failure = AIActionFailure.Create(
                AIActionFailureKind.CannotStart,
                $"{actionName} Execute threw {exception.GetType().Name}: {exception.Message}",
                selectedDestination);
            brain.FailExpectedActionExecution(
                selectedAction,
                failure,
                exception);
            return false;
        }
    }

    public List<BuildableObject> GetReachableBuilding()
    {
        EnsureRuntimeState();
        if (!TryGetGrid(out Grid grid))
        {
            return new List<BuildableObject>();
        }

        if (brain != null)
        {
            IFacilityCandidateCache cache = brain.RequireFacilityCandidateCache();
            FacilityRole roles = cache.GetAvailableRoles(grid);
            return cache.GetCandidates(grid, roles).ToList();
        }

        return new List<BuildableObject>();
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
        if (runtimeStateInitialized || runtimeStateInitializing)
        {
            return;
        }

        runtimeStateInitializing = true;
        try
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

            carryInventory = abilityBridge.EnsureRuntimeAbilities(
                this,
                runtimeBridge?.WildlifeRuntime);
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
            runtimeStateInitialized = true;
        }
        finally
        {
            runtimeStateInitializing = false;
        }
    }

    public T GetAbility<T>() where T : CharacterAbility =>
        abilityBridge.Get<T>(RuntimeAbilityCache);
    public bool TryGetAbility<T>(out T result) where T : CharacterAbility =>
        abilityBridge.TryGet(RuntimeAbilityCache, out result);
    public Vector2Int GetNowXY() =>
        RuntimeLifecycle != null ? RuntimeLifecycle.GetNowXY() : Vector2Int.zero;
    public void AddLog(string message) => activityBridge.AddLog(RuntimeLog, message);
    public void AddActivity(CharacterActivityEvent activity) =>
        activityBridge.AddActivity(RuntimeLog, progression, activity);
    public float GetMoveSpeed()
    {
        EnsureRuntimeState();
        return CharacterActorRuntimeFacade.GetMoveSpeed(
            characterStats,
            identity,
            carryInventory);
    }
    public CharacterCarryInventory CarryInventory => RuntimeCarryInventory;
    public float GetConsumptionMultiplier() =>
        RuntimeStats != null ? RuntimeStats.GetConsumptionMultiplier() : 1f;
    public GameplayEffectProjectionResult ProjectDetailedStat(
        string targetId,
        float baseValue,
        IEnumerable<string> activeConditionIds = null) =>
        RuntimeStats != null
            ? RuntimeStats.ProjectDetailedStat(
                targetId,
                baseValue,
                activeConditionIds)
            : new GameplayEffectProjectionResult(
                baseValue,
                Array.Empty<GameplayEffectContribution>());
    public float GetDetailedStatMultiplier(
        string targetId,
        IEnumerable<string> activeConditionIds = null) =>
        RuntimeStats != null
            ? RuntimeStats.GetDetailedStatMultiplier(
                targetId,
                activeConditionIds)
            : 1f;
    public float GetStayDurationMultiplier() =>
        RuntimeStats != null ? RuntimeStats.GetStayDurationMultiplier() : 1f;
    public float GetCrowdSensitivityMultiplier() =>
        RuntimeStats != null ? RuntimeStats.GetCrowdSensitivityMultiplier() : 1f;
    public float GetWorkSpeedMultiplier(WorkTypeId workTypeId) =>
        RuntimeStats != null ? RuntimeStats.GetWorkSpeedMultiplier(workTypeId) : 1f;
    public float GetWorkSpeedMultiplier(
        WorkTypeId workTypeId,
        BuildableObject target) =>
        RuntimeStats != null
            ? RuntimeStats.GetWorkSpeedMultiplier(workTypeId, target)
            : 1f;
    public float GetWorkContextMultiplier(WorkTypeId workTypeId) =>
        RuntimeStats != null
            ? RuntimeStats.GetWorkContextMultiplier(workTypeId)
            : throw new InvalidOperationException(
                "Character runtime stats are unavailable for work performance.");
    public float GetWorkPreferenceScore(WorkTypeId workTypeId) =>
        RuntimeStats != null ? RuntimeStats.GetWorkPreferenceScore(workTypeId) : 0.5f;
    public float GetFacilityPreferenceScore(FacilityRole roles) =>
        RuntimeStats != null ? RuntimeStats.GetFacilityPreferenceScore(roles) : 0.5f;
    public float GetAccidentChanceMultiplier() =>
        RuntimeStats != null ? RuntimeStats.GetAccidentChanceMultiplier() : 1f;
    public CharacterSpeciesIncidentType GetIncidentType() => RuntimeStats != null
        ? RuntimeStats.GetIncidentType()
        : CharacterSpeciesIncidentType.None;
    public float GetCrimeRiskMultiplier() =>
        RuntimeStats != null ? RuntimeStats.GetCrimeRiskMultiplier() : 1f;
    public float GetCombatPowerMultiplier() =>
        RuntimeStats != null ? RuntimeStats.GetCombatPowerMultiplier() : 1f;
    public float GetFatigueEfficiencyMultiplier() => RuntimeStats != null
        ? RuntimeStats.GetFatigueEfficiencyMultiplier()
        : 1f;
    public float GetInjuryEfficiencyMultiplier() => RuntimeStats != null
        ? RuntimeStats.GetInjuryEfficiencyMultiplier()
        : 1f;
    public float MaxHealth => RuntimeStats != null ? RuntimeStats.MaxHealth : 100f;
    public float CurrentHealth => RuntimeStats != null ? RuntimeStats.CurrentHealth : 100f;
    public void Initialization(CharacterSO data) => Initialize(data);
    public void CacheAbility() => RuntimeAbilityCache?.CacheAbility();
    public void RefreshAbilityCache() => RuntimeAbilityCache?.RefreshAbilityCache();
    public IEnumerator ChangeStatByTick() => RuntimeStats != null
        ? RuntimeStats.ChangeStatByTick()
        : EmptyRoutine();
    public void ChangesStat(CharacterCondition condition, float value) =>
        RuntimeStats?.ChangesStat(condition, value);
    [GameplayInternalOnly(
        "Domain services submit mood impulses here so the injected mood policy can apply immunity and transforms.",
        "RoomEnvironmentExperienceService")]
    public void ApplyMoodFactor(
        string id,
        string label,
        float value,
        float durationSeconds = 180f,
        int maxStacks = 1)
    {
        if (moodPolicy == null)
            throw new InvalidOperationException(
                "CharacterMoodPolicyService must be injected before applying mood.");
        moodPolicy.ApplySeconds(
            this,
            id,
            value,
            durationSeconds,
            label,
            maxStacks);
    }

    internal void ApplyResolvedMoodFactor(
        string id,
        string label,
        float value,
        float durationSeconds,
        int maxStacks) => RuntimeStats?.ApplyResolvedMoodFactor(
            id,
            label,
            value,
            durationSeconds,
            maxStacks);
    public void ApplyDamage(float amount, string reason = "") =>
        RuntimeStats?.ApplyDamage(amount, reason);
    public void ApplyBodyDamage(float amount, string reason = "") =>
        RuntimeStats?.ApplyNonLethalDamage(amount, reason);
    public void Heal(float amount) => RuntimeStats?.Heal(amount);
    public void ScaleMaxHealth(float multiplier) => RuntimeStats?.ScaleMaxHealth(multiplier);
    public void SetInjurySeverity(float value) => RuntimeStats?.SetInjurySeverity(value);
    public void Die(string reason = "") => RuntimeStats?.Die(reason);
    public void Die(CharacterDeathCauseCode cause, string reasonCode) =>
        RuntimeStats?.Die(cause, reasonCode);
    public void InitializeStats(bool resetCurrentHealth) =>
        RuntimeStats?.RecalculateVitals(resetCurrentHealth);
    public void SetLifecycleState(CharacterLifecycleState nextState) =>
        RuntimeLifecycle?.SetLifecycleState(nextState);
    public bool BeginExpedition() =>
        RuntimeLifecycle != null && RuntimeLifecycle.BeginExpedition();
    public void EndExpedition(bool alive = true) => RuntimeLifecycle?.EndExpedition(alive);
    public void ChangeLayer(string layer) => RuntimeVisual?.ChangeLayer(layer);
    public void ApplyVisualFootAnchor() => RuntimeVisual?.ApplyVisualFootAnchor();
    public float GetVisualTopLocalY() =>
        RuntimeVisual != null ? RuntimeVisual.GetVisualTopLocalY() : 1f;
    public void DoFade(float alpha, float duration) => RuntimeVisual?.DoFade(alpha, duration);
    public void Flip(CharacterFacing facing) => RuntimeVisual?.Flip(facing);
    public void HideForTraversal(float failSafeSeconds) =>
        RuntimeVisual?.HideForTraversal(failSafeSeconds);
    public void RestoreTraversalVisibility() => RuntimeVisual?.RestoreTraversalVisibility();
    public void SetAiPaused(bool value) => RuntimeLifecycle?.SetAiPaused(value);
    public bool IsAiPaused() => RuntimeLifecycle != null && RuntimeLifecycle.IsAiPaused;
    public string GetSpeciesShortDescription() => RuntimeIdentity != null
        ? RuntimeIdentity.GetSpeciesShortDescription()
        : string.Empty;
    internal void RaiseDied(string reason) => OnDied?.Invoke(this, reason);

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

    private CharacterAbilityCache RuntimeAbilityCache
        { get { EnsureRuntimeState(); return abilityCache; } }
    private CharacterCarryInventory RuntimeCarryInventory
        { get { EnsureRuntimeState(); return carryInventory; } }
    private CharacterStats RuntimeStats
        { get { EnsureRuntimeState(); return characterStats; } }
    private CharacterLifecycle RuntimeLifecycle
        { get { EnsureRuntimeState(); return lifecycle; } }
    private CharacterVisual RuntimeVisual
        { get { EnsureRuntimeState(); return visual; } }
    private CharacterIdentity RuntimeIdentity
        { get { EnsureRuntimeState(); return identity; } }
    private CharacterLog RuntimeLog
        { get { EnsureRuntimeState(); return characterLog; } }

    private static IEnumerator EmptyRoutine()
    {
        yield break;
    }

}
