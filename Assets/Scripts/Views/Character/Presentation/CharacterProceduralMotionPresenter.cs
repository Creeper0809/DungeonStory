using DungeonStory.Foundation;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterProceduralMotionPresenter : MonoBehaviour
{
    private const float PixelSize =
        1f / WorldInteractionPresentationCatalogSO.PixelsPerUnit;
    private const float MovementHoldSeconds = 0.16f;
    private const float PositionEpsilonSquared = 0.000001f;

    private CharacterActor actor;
    private CharacterVisual visual;
    private IGameClock gameClock;
    private IMainCameraProvider mainCameraProvider;
    private WorldInteractionPresentationCatalogSO catalog;
    private Transform visualRoot;
    private Sprite baselineSprite;
    private Vector3 baselineLocalPosition;
    private Vector3 lastActorWorldPosition;
    private CharacterLifecycleState lastLifecycleState;
    private float movingUntil;
    private float impactUntil;
    private float impactDirection = 1f;
    private bool baselineCaptured;
    private bool visible;
    private bool impactRotationCompatible;
    private CharacterWorldActionKind actionKind;
    private int effectToken;
    private int appliedTransformCount;
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private float lastCameraOrthographicSize = float.NaN;

    public bool IsVisible => visible;
    public bool IsMoving => visible && gameClock != null && gameClock.Time <= movingUntil;
    public bool ImpactRotationCompatible => impactRotationCompatible;
    public int AppliedTransformCount => appliedTransformCount;
    public Vector3 BaselineLocalPosition => baselineLocalPosition;

    public static CharacterProceduralMotionPresenter Ensure(
        CharacterActor actor,
        IGameClock gameClock,
        WorldInteractionPresentationCatalogSO catalog,
        IMainCameraProvider mainCameraProvider)
    {
        if (actor == null)
        {
            return null;
        }

        CharacterProceduralMotionPresenter presenter =
            actor.GetComponent<CharacterProceduralMotionPresenter>();
        if (presenter == null && Application.isPlaying)
        {
            presenter = actor.gameObject.AddComponent<CharacterProceduralMotionPresenter>();
        }

        presenter?.Configure(actor, gameClock, catalog, mainCameraProvider);
        return presenter;
    }

    public void Configure(
        CharacterActor actor,
        IGameClock gameClock,
        WorldInteractionPresentationCatalogSO catalog,
        IMainCameraProvider mainCameraProvider)
    {
        this.actor = actor;
        visual = actor != null ? actor.GetComponent<CharacterVisual>() : null;
        this.gameClock = gameClock;
        this.catalog = catalog
            ?? throw new System.ArgumentNullException(nameof(catalog));
        this.mainCameraProvider = mainCameraProvider
            ?? throw new System.ArgumentNullException(nameof(mainCameraProvider));
        lastActorWorldPosition = actor != null ? actor.transform.position : Vector3.zero;
        lastLifecycleState = actor != null
            ? actor.CurrentLifecycleState
            : CharacterLifecycleState.None;
        RefreshRotationCompatibility(force: true);
        RecaptureBaselineAfterFootAlignment();
    }

    public void TickPresentation(bool isVisible)
    {
        if (actor == null || gameClock == null || catalog == null)
        {
            return;
        }

        if (visible != isVisible)
        {
            visible = isVisible;
            if (!visible)
            {
                ResetVisualRootToDefault();
                return;
            }

            lastActorWorldPosition = actor.transform.position;
        }

        CharacterLifecycleState lifecycleState = actor.CurrentLifecycleState;
        if (lifecycleState != lastLifecycleState)
        {
            lastLifecycleState = lifecycleState;
            ResetVisualRootToDefault();
        }

        EnsureCurrentBaseline();
        RefreshRotationCompatibility(force: false);
        if (!visible || visualRoot == null || gameClock.IsPaused)
        {
            return;
        }

        Vector3 actorPosition = actor.transform.position;
        if ((actorPosition - lastActorWorldPosition).sqrMagnitude
            > PositionEpsilonSquared)
        {
            movingUntil = gameClock.Time + MovementHoldSeconds;
        }

        lastActorWorldPosition = actorPosition;
        ApplyCurrentPose();
    }

    public void TriggerImpact(float direction)
    {
        if (gameClock == null || catalog == null)
        {
            return;
        }

        impactDirection = direction < 0f ? -1f : 1f;
        impactUntil = gameClock.Time + catalog.ImpactDuration;
        effectToken++;
    }

    public void SetActionKind(CharacterWorldActionKind nextActionKind)
    {
        actionKind = nextActionKind;
    }

    public void SetImpactRotationCompatibility(bool compatible)
    {
        impactRotationCompatible = compatible;
        if (!compatible && visualRoot != null)
        {
            visualRoot.localRotation = Quaternion.identity;
        }
    }

    public void RecaptureBaselineAfterFootAlignment()
    {
        if (visual == null)
        {
            return;
        }

        ResetVisualRootToDefault();
        visual.EnsureVisualReferences();
        visual.ApplyVisualFootAnchor();
        visualRoot = visual.VisualRoot;
        if (visualRoot == null || visualRoot == transform)
        {
            baselineCaptured = false;
            return;
        }

        baselineLocalPosition = visualRoot.localPosition;
        baselineSprite = visual.VisualRenderer != null
            ? visual.VisualRenderer.sprite
            : null;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;
        baselineCaptured = true;
    }

    public void ResetVisualRootToDefault()
    {
        effectToken++;
        impactUntil = float.NegativeInfinity;
        movingUntil = float.NegativeInfinity;
        if (visualRoot == null)
        {
            visualRoot = visual != null ? visual.VisualRoot : null;
        }

        if (visualRoot == null || visualRoot == transform)
        {
            return;
        }

        if (baselineCaptured)
        {
            visualRoot.localPosition = baselineLocalPosition;
        }

        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;
    }

    private void EnsureCurrentBaseline()
    {
        Transform currentRoot = visual != null ? visual.VisualRoot : null;
        Sprite currentSprite = visual != null && visual.VisualRenderer != null
            ? visual.VisualRenderer.sprite
            : null;
        if (!baselineCaptured
            || currentRoot != visualRoot
            || currentSprite != baselineSprite)
        {
            RecaptureBaselineAfterFootAlignment();
        }
    }

    private void ApplyCurrentPose()
    {
        bool moving = gameClock.Time <= movingUntil;
        bool impacting = gameClock.Time <= impactUntil;
        int xPixels = 0;
        int yPixels = 0;
        float scaleY = 1f;
        float scaleX = 1f;

        if (moving)
        {
            int frame = catalog.GetWalkFrameIndex(gameClock.Time);
            yPixels = catalog.GetWalkYOffsetPixels(frame);
            int direction = visual != null
                && visual.VisualRenderer != null
                && visual.VisualRenderer.flipX
                    ? 1
                    : -1;
            xPixels = catalog.GetWalkXOffsetPixels(frame) * direction;
            scaleY = catalog.GetWalkSquash(frame);
            scaleX = Mathf.Min(1.04f, 1f / Mathf.Max(0.01f, scaleY));
        }
        else if (actionKind == CharacterWorldActionKind.Rest)
        {
            int breathingFrame = Mathf.Abs(
                Mathf.FloorToInt(gameClock.Time / 0.55f)) % 4;
            if (breathingFrame == 1 || breathingFrame == 2)
            {
                scaleY = 0.98f;
                scaleX = 1.02f;
            }
        }

        if (impacting)
        {
            bool slime = actor != null
                && string.Equals(
                    actor.SpeciesTag,
                    "Slime",
                    System.StringComparison.OrdinalIgnoreCase);
            scaleY = slime ? 0.88f : 0.96f;
            scaleX = slime ? 1.12f : 1.04f;
            xPixels += Mathf.RoundToInt(impactDirection);
        }

        float footCompensation = 0f;
        SpriteRenderer renderer = visual != null ? visual.VisualRenderer : null;
        if (renderer != null && renderer.sprite != null)
        {
            footCompensation = renderer.sprite.bounds.min.y * (1f - scaleY);
        }

        Vector3 offset = new Vector3(
            Quantize(xPixels * PixelSize),
            Quantize((yPixels * PixelSize) + footCompensation),
            0f);
        visualRoot.localPosition = baselineLocalPosition + offset;
        visualRoot.localScale = new Vector3(scaleX, scaleY, 1f);
        visualRoot.localRotation = impacting && impactRotationCompatible
            ? Quaternion.Euler(
                0f,
                0f,
                catalog.MaximumImpactRotation * impactDirection)
            : Quaternion.identity;
        appliedTransformCount++;
    }

    private static float Quantize(float value)
    {
        return Mathf.Round(value / PixelSize) * PixelSize;
    }

    private void RefreshRotationCompatibility(bool force)
    {
        Camera camera = mainCameraProvider.Camera;
        float orthographicSize = camera != null && camera.orthographic
            ? camera.orthographicSize
            : float.NaN;
        if (!force
            && lastScreenWidth == Screen.width
            && lastScreenHeight == Screen.height
            && (float.IsNaN(orthographicSize)
                ? float.IsNaN(lastCameraOrthographicSize)
                : Mathf.Approximately(
                    orthographicSize,
                    lastCameraOrthographicSize)))
        {
            return;
        }

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastCameraOrthographicSize = orthographicSize;
        impactRotationCompatible = EvaluateRotationCompatibility(
            orthographicSize);
        if (!impactRotationCompatible && visualRoot != null)
        {
            visualRoot.localRotation = Quaternion.identity;
        }
    }

    private static bool EvaluateRotationCompatibility(float orthographicSize)
    {
        const int referenceWidth = 480;
        const int referenceHeight = 240;
        if (Screen.width <= 0 || Screen.height <= 0)
        {
            return false;
        }

        float horizontalScale = Screen.width / (float)referenceWidth;
        float verticalScale = Screen.height / (float)referenceHeight;
        bool integerScale =
            Mathf.Abs(horizontalScale - Mathf.Round(horizontalScale)) < 0.001f
            && Mathf.Abs(verticalScale - Mathf.Round(verticalScale)) < 0.001f
            && Mathf.Abs(horizontalScale - verticalScale) < 0.001f;
        bool zoomAligned = float.IsNaN(orthographicSize)
            || Mathf.Abs(
                (orthographicSize
                    * 2f
                    * WorldInteractionPresentationCatalogSO.PixelsPerUnit)
                - Mathf.Round(
                    orthographicSize
                    * 2f
                    * WorldInteractionPresentationCatalogSO.PixelsPerUnit))
                < 0.001f;
        return integerScale && zoomAligned;
    }

    private void OnDisable()
    {
        visible = false;
        ResetVisualRootToDefault();
    }

    private void OnDestroy()
    {
        ResetVisualRootToDefault();
    }
}
