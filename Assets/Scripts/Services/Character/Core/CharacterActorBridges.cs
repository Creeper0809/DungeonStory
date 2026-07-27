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

    public IWorldItemStackRuntime WorldItemStackRuntime { get; private set; }
    public IWildlifeRuntime WildlifeRuntime { get; private set; }
    public ICharacterMedicalRuntime MedicalRuntime { get; private set; }
    public ICharacterDeprivationRuntime DeprivationRuntime { get; private set; }
    public IGameClock GameClock { get; private set; }
    public IGridPathSearchBroker PathSearchBroker => pathSearchBroker;
    public ICharacterAiWorldRegistry WorldRegistry => worldRegistry;
    public ICharacterAiWorldSignalQuery WorldSignalQuery => worldSignalQuery;
    public bool ShouldCollectDetailedAiDiagnostics =>
        aiSchedulingService == null
        || aiSchedulingService.ShouldCollectDetailedDiagnostics(actor);

    public void Configure(
        CharacterActor actor,
        IGridSystemProvider gridSystemProvider,
        ICharacterAiSchedulingService aiSchedulingService,
        IGridPathSearchBroker pathSearchBroker,
        ICharacterAiWorldRegistry worldRegistry,
        ICharacterAiWorldSignalQuery worldSignalQuery,
        IWorldItemStackRuntime worldItemStackRuntime,
        IWildlifeRuntime wildlifeRuntime,
        ICharacterMedicalRuntime medicalRuntime,
        ICharacterDeprivationRuntime deprivationRuntime,
        IGameClock gameClock)
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
        MedicalRuntime = medicalRuntime;
        DeprivationRuntime = deprivationRuntime;
        GameClock = gameClock;
        worldRegistry.RegisterCharacterLifetime(actor);
        RegisterIfActive();
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

    public void OnActorDisabled()
    {
        UnregisterFromAiScheduler();
        UnregisterFromWorldRegistry();
    }

    public void OnActorDestroyed()
    {
        UnregisterFromAiScheduler();
        UnregisterFromWorldRegistry();
        worldRegistry?.UnregisterCharacterLifetime(actor);
    }

    private void RegisterIfActive()
    {
        if (actor == null || !actor.isActiveAndEnabled)
        {
            return;
        }

        if (!registeredWithWorldRegistry && worldRegistry != null)
        {
            worldRegistry.RegisterCharacter(actor);
            registeredWithWorldRegistry = true;
        }

        if (!registeredWithAiScheduler && aiSchedulingService != null)
        {
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

[DisallowMultipleComponent]
public sealed class CharacterActorPresentationBridge : MonoBehaviour
{
    private CharacterActor actor;
    private IWorldInfoClickSelector worldInfoClickSelector;
    private ICharacterFeedbackBubbleFactory feedbackBubbleFactory;
    private ICharacterPresentationScheduler presentationScheduler;

    public IMainCameraProvider MainCameraProvider { get; private set; }
    public ITmpKoreanFontService TmpKoreanFontService { get; private set; }
    public IDynamicFrameWorkBudget FrameWorkBudget { get; private set; }

    public void Configure(
        CharacterActor actor,
        IWorldInfoClickSelector worldInfoClickSelector,
        ICharacterFeedbackBubbleFactory feedbackBubbleFactory,
        IMainCameraProvider mainCameraProvider,
        IDynamicFrameWorkBudget frameWorkBudget,
        ITmpKoreanFontService tmpKoreanFontService = null,
        ICharacterPresentationScheduler presentationScheduler = null)
    {
        this.actor = actor ?? throw new ArgumentNullException(nameof(actor));
        this.worldInfoClickSelector = worldInfoClickSelector
            ?? throw new ArgumentNullException(nameof(worldInfoClickSelector));
        this.feedbackBubbleFactory = feedbackBubbleFactory
            ?? throw new ArgumentNullException(nameof(feedbackBubbleFactory));
        MainCameraProvider = mainCameraProvider
            ?? throw new ArgumentNullException(nameof(mainCameraProvider));
        FrameWorkBudget = frameWorkBudget
            ?? throw new ArgumentNullException(nameof(frameWorkBudget));
        TmpKoreanFontService = tmpKoreanFontService;
        this.presentationScheduler = presentationScheduler;
        EnsurePresentation();
    }

    public IWorldInfoClickSelector RequireWorldInfoClickSelector()
    {
        return worldInfoClickSelector
            ?? throw new InvalidOperationException(
                $"{nameof(CharacterActorPresentationBridge)} requires a click selector.");
    }

    public void EnsurePresentation()
    {
        if (actor == null || !Application.isPlaying)
        {
            return;
        }

        WorldCharacterNameplate nameplate =
            WorldCharacterNameplate.Ensure(actor, TmpKoreanFontService);
        CharacterFeedbackBubble feedbackBubble =
            feedbackBubbleFactory?.GetOrAdd(actor);
        presentationScheduler?.Register(actor, nameplate, feedbackBubble);
    }

    public void OnActorEnabled()
    {
        EnsurePresentation();
    }

    public void OnActorDisabled()
    {
        presentationScheduler?.Unregister(actor);
    }

    public void OnActorDestroyed()
    {
        presentationScheduler?.Unregister(actor);
    }
}
