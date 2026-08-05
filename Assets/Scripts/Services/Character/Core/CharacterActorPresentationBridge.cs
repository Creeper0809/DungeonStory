using System;
using DungeonStory.Foundation;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterActorPresentationBridge : MonoBehaviour
{
    private CharacterActor actor;
    private IWorldInfoClickSelector worldInfoClickSelector;
    private ICharacterFeedbackBubbleFactory feedbackBubbleFactory;
    private ICharacterPresentationScheduler presentationScheduler;
    private IGameClock gameClock;
    private WorldInteractionPresentationCatalogSO worldPresentationCatalog;
    private IDungeonUserSettingsService userSettings;
    private CharacterProceduralMotionPresenter proceduralMotionPresenter;
    private CharacterCarryPresentation carryPresentation;
    private CharacterWorldActionPresenter worldActionPresenter;
    private bool unpublishedComposition;
    private bool detachedRestoreCandidate;

    public bool IsUnpublishedComposition => unpublishedComposition;

    public void PrepareForComposition()
    {
        if (actor != null || unpublishedComposition || detachedRestoreCandidate)
        {
            throw new InvalidOperationException(
                "Character presentation composition mode must be selected exactly once before configuration.");
        }

        unpublishedComposition = true;
    }

    public IMainCameraProvider MainCameraProvider { get; private set; }
    public ITmpKoreanFontService TmpKoreanFontService { get; private set; }
    public IDynamicFrameWorkBudget FrameWorkBudget { get; private set; }

    public void PrepareForDetachedRestore()
    {
        if (actor != null || detachedRestoreCandidate || unpublishedComposition)
        {
            throw new InvalidOperationException(
                "Detached presentation mode must be selected exactly once before configuration.");
        }

        detachedRestoreCandidate = true;
    }

    public void Configure(
        CharacterActor actor,
        IWorldInfoClickSelector worldInfoClickSelector,
        ICharacterFeedbackBubbleFactory feedbackBubbleFactory,
        IMainCameraProvider mainCameraProvider,
        IDynamicFrameWorkBudget frameWorkBudget,
        WorldInteractionPresentationCatalogSO worldPresentationCatalog,
        IDungeonUserSettingsService userSettings,
        ITmpKoreanFontService tmpKoreanFontService,
        ICharacterPresentationScheduler presentationScheduler,
        IGameClock gameClock)
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
        this.worldPresentationCatalog = worldPresentationCatalog
            ?? throw new ArgumentNullException(nameof(worldPresentationCatalog));
        this.userSettings = userSettings
            ?? throw new ArgumentNullException(nameof(userSettings));
        TmpKoreanFontService = tmpKoreanFontService;
        this.presentationScheduler = presentationScheduler;
        this.gameClock = gameClock;
        if (!detachedRestoreCandidate && !unpublishedComposition)
        {
            EnsurePresentation();
        }
    }

    public void RequireCompositionReadyForPublication()
    {
        if (!unpublishedComposition || detachedRestoreCandidate || actor == null)
        {
            throw new InvalidOperationException(
                "Only a configured unpublished character presentation bridge can be published.");
        }
    }

    public void PublishComposition()
    {
        RequireCompositionReadyForPublication();
        unpublishedComposition = false;
    }

    public void PublishDetachedRestore()
    {
        if (!detachedRestoreCandidate)
        {
            throw new InvalidOperationException(
                "Only a detached presentation bridge can be published.");
        }

        detachedRestoreCandidate = false;
    }

    public void RollbackDetachedRestorePublication()
    {
        if (detachedRestoreCandidate)
        {
            return;
        }

        OnActorDisabled();
        detachedRestoreCandidate = true;
    }

    public IWorldInfoClickSelector RequireWorldInfoClickSelector()
    {
        return worldInfoClickSelector
            ?? throw new InvalidOperationException(
                $"{nameof(CharacterActorPresentationBridge)} requires a click selector.");
    }

    public void EnsurePresentation()
    {
        if (detachedRestoreCandidate
            || unpublishedComposition
            || actor == null
            || !Application.isPlaying)
        {
            return;
        }

        WorldCharacterNameplate nameplate =
            WorldCharacterNameplate.Ensure(actor);
        CharacterFeedbackBubble feedbackBubble =
            feedbackBubbleFactory?.GetOrAdd(actor);
        proceduralMotionPresenter =
            CharacterProceduralMotionPresenter.Ensure(
                actor,
                gameClock,
                worldPresentationCatalog,
                MainCameraProvider);
        carryPresentation =
            CharacterCarryPresentation.Ensure(actor, worldPresentationCatalog);
        worldActionPresenter =
            CharacterWorldActionPresenter.Ensure(
                actor,
                gameClock,
                proceduralMotionPresenter,
                userSettings.Current.reducedMotion);
        presentationScheduler?.Register(actor, nameplate, feedbackBubble);
    }

    public void TickPresentationMaintenance()
    {
        if (actor == null)
        {
            return;
        }

        bool visible = presentationScheduler != null
            ? presentationScheduler.IsVisible(actor)
            : actor.isActiveAndEnabled;
        worldActionPresenter?.TickPresentation(visible);
        proceduralMotionPresenter?.TickPresentation(visible);
        carryPresentation?.TickPresentation(visible);
    }

    public void ResetProceduralPresentation(bool recaptureBaseline)
    {
        worldActionPresenter?.ResetPresentation();
        carryPresentation?.ResetPresentation();
        proceduralMotionPresenter?.ResetVisualRootToDefault();
        if (recaptureBaseline)
        {
            proceduralMotionPresenter?.RecaptureBaselineAfterFootAlignment();
        }
    }

    public void OnActorEnabled()
    {
        EnsurePresentation();
    }

    public void OnActorDisabled()
    {
        ResetProceduralPresentation(recaptureBaseline: false);
        presentationScheduler?.Unregister(actor);
    }

    public void OnActorDestroyed()
    {
        ResetProceduralPresentation(recaptureBaseline: false);
        presentationScheduler?.Unregister(actor);
    }
}
