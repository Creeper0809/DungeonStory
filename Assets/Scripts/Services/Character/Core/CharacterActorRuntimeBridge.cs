using System;
using DungeonStory.Foundation;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterActorRuntimeBridge : MonoBehaviour
{
    private CharacterActor actor;
    private IGridSystemProvider gridSystemProvider;
    private ICharacterAiSchedulingService aiSchedulingService;
    private IGridPathSearchBroker pathSearchBroker;
    private ICharacterAiWorldRegistry worldRegistry;
    private ICharacterAiWorldSignalQuery worldSignalQuery;
    private bool registeredWithAiScheduler;
    private bool registeredWithWorldRegistry;
    private bool registeredWithLifetimeRegistry;
    private bool unpublishedComposition;
    private bool detachedRestoreCandidate;
    private IBuildingVisitorPort buildingVisitor;

    public IWorldItemStackRuntime WorldItemStackRuntime { get; private set; }
    public IWildlifeRuntime WildlifeRuntime { get; private set; }
    public ICharacterMedicalQuery MedicalQuery { get; private set; }
    public ICharacterMedicalCommand MedicalCommands { get; private set; }
    public ICharacterDeprivationRuntime DeprivationRuntime { get; private set; }
    public ICharacterSubstanceRuntime SubstanceRuntime { get; private set; }
    public ICharacterMealOperationCancellation MealOperationCancellation { get; private set; }
    public IGameClock GameClock { get; private set; }
    public IWorkAmountCalculator WorkAmountCalculator { get; private set; }
    public IGridPathSearchBroker PathSearchBroker => pathSearchBroker;
    public ICharacterAiWorldRegistry WorldRegistry => worldRegistry;
    public ICharacterAiWorldSignalQuery WorldSignalQuery => worldSignalQuery;
    public bool ShouldCollectDetailedAiDiagnostics =>
        aiSchedulingService == null
        || aiSchedulingService.ShouldCollectDetailedDiagnostics(actor);
    public bool IsConfigured => actor != null
        && gridSystemProvider != null
        && aiSchedulingService != null
        && worldRegistry != null;
    public bool IsUnpublishedComposition => unpublishedComposition;

    internal IBuildingVisitorPort GetBuildingVisitor(CharacterActor owner)
    {
        if (owner == null)
        {
            throw new ArgumentNullException(nameof(owner));
        }
        if (actor != null && !ReferenceEquals(actor, owner))
        {
            throw new InvalidOperationException(
                "Character runtime bridge cannot serve another actor's building visitor.");
        }

        return buildingVisitor ??= new CharacterBuildingVisitorAdapter(owner);
    }

    public void PrepareForComposition()
    {
        if (IsConfigured || unpublishedComposition || detachedRestoreCandidate)
        {
            throw new InvalidOperationException(
                "Character composition mode must be selected exactly once before configuration.");
        }

        unpublishedComposition = true;
    }

    public void PrepareForDetachedRestore()
    {
        if (IsConfigured || detachedRestoreCandidate || unpublishedComposition)
        {
            throw new InvalidOperationException(
                "Detached runtime-bridge mode must be selected exactly once before configuration.");
        }

        detachedRestoreCandidate = true;
    }

    public void Configure(
        CharacterActor actor,
        IGridSystemProvider gridSystemProvider,
        ICharacterAiSchedulingService aiSchedulingService,
        IGridPathSearchBroker pathSearchBroker,
        ICharacterAiWorldRegistry worldRegistry,
        ICharacterAiWorldSignalQuery worldSignalQuery,
        IWorldItemStackRuntime worldItemStackRuntime,
        IWildlifeRuntime wildlifeRuntime,
        ICharacterMedicalQuery medicalQuery,
        ICharacterMedicalCommand medicalCommands,
        ICharacterDeprivationRuntime deprivationRuntime,
        ICharacterSubstanceRuntime substanceRuntime,
        ICharacterMealOperationCancellation mealOperationCancellation,
        IGameClock gameClock,
        IWorkAmountCalculator workAmountCalculator)
    {
        this.actor = actor ?? throw new ArgumentNullException(nameof(actor));
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.aiSchedulingService = aiSchedulingService
            ?? throw new ArgumentNullException(nameof(aiSchedulingService));
        this.pathSearchBroker = pathSearchBroker
            ?? throw new ArgumentNullException(nameof(pathSearchBroker));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.worldSignalQuery = worldSignalQuery
            ?? throw new ArgumentNullException(nameof(worldSignalQuery));
        WorldItemStackRuntime = worldItemStackRuntime;
        WildlifeRuntime = wildlifeRuntime;
        MedicalQuery = medicalQuery
            ?? throw new ArgumentNullException(nameof(medicalQuery));
        MedicalCommands = medicalCommands
            ?? throw new ArgumentNullException(nameof(medicalCommands));
        DeprivationRuntime = deprivationRuntime;
        SubstanceRuntime = substanceRuntime;
        MealOperationCancellation = mealOperationCancellation;
        GameClock = gameClock;
        WorkAmountCalculator = workAmountCalculator
            ?? throw new ArgumentNullException(nameof(workAmountCalculator));
        if (!detachedRestoreCandidate && !unpublishedComposition)
        {
            worldRegistry.RegisterCharacterLifetime(actor);
            registeredWithLifetimeRegistry = true;
        }
        RegisterIfActive();
    }

    public void RequireCompositionReadyForPublication()
    {
        if (!unpublishedComposition || detachedRestoreCandidate || !IsConfigured)
        {
            throw new InvalidOperationException(
                "Only a fully configured unpublished character runtime bridge can be published.");
        }
    }

    public void PublishComposition()
    {
        RequireCompositionReadyForPublication();
        if (!registeredWithLifetimeRegistry)
        {
            worldRegistry.RegisterCharacterLifetime(actor);
            registeredWithLifetimeRegistry = true;
        }

        unpublishedComposition = false;
        RegisterIfActive();
    }

    public void PublishDetachedRestore()
    {
        if (!detachedRestoreCandidate)
        {
            throw new InvalidOperationException(
                "Only a detached runtime bridge can be published.");
        }

        detachedRestoreCandidate = false;
        if (!registeredWithLifetimeRegistry)
        {
            registeredWithLifetimeRegistry = true;
            try
            {
                worldRegistry.RegisterCharacterLifetime(actor);
            }
            catch
            {
                worldRegistry.UnregisterCharacterLifetime(actor);
                registeredWithLifetimeRegistry = false;
                detachedRestoreCandidate = true;
                throw;
            }
        }

        RegisterIfActive();
    }

    public void RollbackDetachedRestorePublication()
    {
        UnregisterFromAiScheduler();
        UnregisterFromWorldRegistry();
        if (registeredWithLifetimeRegistry)
        {
            worldRegistry?.UnregisterCharacterLifetime(actor);
            registeredWithLifetimeRegistry = false;
        }

        detachedRestoreCandidate = true;
    }

    public bool TryGetGrid(out Grid grid)
    {
        if (gridSystemProvider == null)
        {
            grid = null;
            return false;
        }

        return gridSystemProvider.TryGetGrid(out grid);
    }

    public void RequireConfigured()
    {
        if (aiSchedulingService == null || worldRegistry == null)
        {
            throw new InvalidOperationException(
                $"{nameof(CharacterActorRuntimeBridge)} requires scene-scoped runtime services.");
        }
    }

    public void OnActorEnabled()
    {
        RegisterIfActive();
    }

    internal void ReconcilePublishedRegistration()
    {
        if (detachedRestoreCandidate
            || unpublishedComposition
            || actor == null
            || !actor.isActiveAndEnabled
            || worldRegistry == null
            || aiSchedulingService == null)
        {
            throw new InvalidOperationException(
                "Only an active, configured, published character can reconcile runtime registration.");
        }

        worldRegistry.RegisterCharacterLifetime(actor);
        registeredWithLifetimeRegistry = true;
        worldRegistry.RegisterCharacter(actor);
        registeredWithWorldRegistry = true;
        aiSchedulingService.Register(actor);
        registeredWithAiScheduler = true;
    }

    public void OnActorDisabled()
    {
        UnregisterFromAiScheduler();
        UnregisterFromWorldRegistry();
    }

    public void OnActorDestroyed()
    {
        UnregisterFromAiScheduler();
        UnregisterFromWorldRegistry();
        if (registeredWithLifetimeRegistry)
        {
            worldRegistry?.UnregisterCharacterLifetime(actor);
            registeredWithLifetimeRegistry = false;
        }
    }

    private void RegisterIfActive()
    {
        if (detachedRestoreCandidate
            || unpublishedComposition
            || actor == null
            || !actor.isActiveAndEnabled)
        {
            return;
        }

        // An active published actor must always be visible to lifetime-backed
        // domain ports (consumables, population, medical, save). Reconcile the
        // lifetime registry here as well as during composition publication so
        // prefab reactivation and owner replacement cannot leave an actor in
        // the scheduler/world registry but absent from those domains.
        if (!registeredWithLifetimeRegistry && worldRegistry != null)
        {
            worldRegistry.RegisterCharacterLifetime(actor);
            registeredWithLifetimeRegistry = true;
        }

        if (!registeredWithWorldRegistry && worldRegistry != null)
        {
            worldRegistry.RegisterCharacter(actor);
            registeredWithWorldRegistry = true;
        }

        if (!registeredWithAiScheduler && aiSchedulingService != null)
        {
            // During scene replacement/application shutdown Unity may enable a
            // restored actor after the scene-scoped scheduler has already been
            // detached. Registration is deferred until the new scope exposes
            // a live scheduler instead of throwing from teardown callbacks.
            if (!aiSchedulingService.IsSchedulerAvailable)
            {
                return;
            }
            aiSchedulingService.Register(actor);
            registeredWithAiScheduler = true;
        }
    }

    private void UnregisterFromAiScheduler()
    {
        if (!registeredWithAiScheduler)
        {
            return;
        }

        aiSchedulingService?.Unregister(actor);
        registeredWithAiScheduler = false;
    }

    private void UnregisterFromWorldRegistry()
    {
        if (!registeredWithWorldRegistry)
        {
            return;
        }

        worldRegistry?.UnregisterCharacter(actor);
        registeredWithWorldRegistry = false;
    }
}
