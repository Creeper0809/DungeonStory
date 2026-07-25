using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using DungeonStory.Foundation;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

[DisallowMultipleComponent]
public class CharacterVisual : SerializedMonoBehaviour
{
    [SerializeField]
    [ReadOnly]
    private CharacterIdentity identity;
    [SerializeField]
    [ReadOnly]
    private CharacterLifecycle lifecycle;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private bool autoAlignVisualToFeet = true;

    private CharacterFacing facing;
    private Coroutine traversalVisibilityRestoreRoutine;
    private List<RendererVisibilityState> traversalRendererStates;
    private List<CanvasVisibilityState> traversalCanvasStates;
    private Tween fadeTween;
    private bool isTraversalHidden;
    private float traversalVisibilityRestoreDeadline = -1f;
    private IGameClock gameClock;

    public Transform VisualRoot => visualRoot;
    public SpriteRenderer VisualRenderer => spriteRenderer;

    [Inject]
    public void ConstructCharacterVisual(IGameClock gameClock)
    {
        this.gameClock = gameClock ?? throw new System.ArgumentNullException(nameof(gameClock));
    }

    private void Awake()
    {
        Bind();
    }

    private void OnDisable()
    {
        CleanupVisualTweens();
    }

    private void OnDestroy()
    {
        CleanupVisualTweens();
    }

    public void Bind()
    {
        identity = GetComponent<CharacterIdentity>();
        lifecycle = GetComponent<CharacterLifecycle>();
        EnsureVisualReferences();
    }

    private IGameClock RequireGameClock()
    {
        return gameClock
            ?? throw new System.InvalidOperationException(
                $"{nameof(CharacterVisual)} requires {nameof(IGameClock)} injection.");
    }

    public void ChangeLayer(string layer)
    {
        EnsureVisualReferences();
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.sortingLayerName = layer;
        }
    }

    public void EnsureVisualReferences()
    {
        if (visualRoot == null)
        {
            visualRoot = transform.Find("Visual");
        }

        SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
        if (visualRoot == transform && rootRenderer == null)
        {
            // Some legacy prefabs serialized the actor root as the visual root even though
            // their renderer lives on a child. Visual-only motion must never move the grid actor.
            visualRoot = null;
        }

        if ((visualRoot == null || visualRoot == transform) && rootRenderer != null)
        {
            visualRoot = CreateVisualRoot();
        }

        if (visualRoot != null)
        {
            SpriteRenderer visualRenderer = visualRoot.GetComponent<SpriteRenderer>();
            if (visualRenderer == null && rootRenderer != null)
            {
                visualRenderer = visualRoot.gameObject.AddComponent<SpriteRenderer>();
                CopySpriteRenderer(rootRenderer, visualRenderer);
            }

            if (rootRenderer != null && rootRenderer != visualRenderer)
            {
                if (visualRenderer != null && visualRenderer.sprite == null)
                {
                    CopySpriteRenderer(rootRenderer, visualRenderer);
                }

                RemoveRootSpriteRenderer(rootRenderer);
            }

            spriteRenderer = visualRenderer;
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (visualRoot == null && spriteRenderer != null)
        {
            visualRoot = spriteRenderer.transform;
        }

        ApplyVisualFootAnchor();
    }

    private Transform CreateVisualRoot()
    {
        GameObject visualObject = new GameObject("Visual");
        Transform visual = visualObject.transform;
        visual.SetParent(transform, false);
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        visual.localScale = Vector3.one;
        return visual;
    }

    private static void CopySpriteRenderer(SpriteRenderer source, SpriteRenderer target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.sprite = source.sprite;
        target.color = source.color;
        target.sharedMaterials = source.sharedMaterials;
        target.sortingLayerID = source.sortingLayerID;
        target.sortingOrder = source.sortingOrder;
        target.maskInteraction = source.maskInteraction;
        target.flipX = source.flipX;
        target.flipY = source.flipY;
        target.drawMode = SpriteDrawMode.Simple;
        target.size = source.sprite != null ? (Vector2)source.sprite.bounds.size : Vector2.one;
    }

    private static void RemoveRootSpriteRenderer(SpriteRenderer rootRenderer)
    {
        if (rootRenderer == null)
        {
            return;
        }

        KillRendererTweens(rootRenderer);
        if (Application.isPlaying)
        {
            Destroy(rootRenderer);
            return;
        }

        DestroyImmediate(rootRenderer);
    }

    public void SetCharacterSprite(Sprite sprite)
    {
        EnsureVisualReferences();
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.sprite = sprite;
        ApplyVisualFootAnchor();
    }

    public void ApplyVisualFootAnchor()
    {
        if (!autoAlignVisualToFeet
            || visualRoot == null
            || visualRoot == transform
            || spriteRenderer == null
            || spriteRenderer.sprite == null)
        {
            return;
        }

        Vector3 localPosition = visualRoot.localPosition;
        localPosition.y = -spriteRenderer.sprite.bounds.min.y;
        visualRoot.localPosition = localPosition;
    }

    public float GetVisualTopLocalY()
    {
        EnsureVisualReferences();
        if (visualRoot == null || spriteRenderer == null || spriteRenderer.sprite == null)
        {
            return 1f;
        }

        float visualY = visualRoot == transform ? 0f : visualRoot.localPosition.y;
        return visualY + spriteRenderer.sprite.bounds.max.y;
    }

    public void DoFade(float alpha, float duration)
    {
        EnsureVisualReferences();
        if (spriteRenderer != null)
        {
            KillFadeTween();
            KillRendererTweens(spriteRenderer);
            if (duration <= 0f)
            {
                Color color = spriteRenderer.color;
                color.a = alpha;
                spriteRenderer.color = color;
                return;
            }

            fadeTween = spriteRenderer
                .DOFade(alpha, duration)
                .SetTarget(spriteRenderer)
                .SetId(spriteRenderer)
                .OnKill(() => fadeTween = null);
        }
    }

    public void Flip(CharacterFacing nextFacing)
    {
        facing = nextFacing;
        EnsureVisualReferences();
        Vector3 rootScale = transform.localScale;
        transform.localScale = new Vector3(Mathf.Abs(rootScale.x), rootScale.y, rootScale.z);

        if (visualRoot != null && visualRoot != transform)
        {
            Vector3 visualScale = visualRoot.localScale;
            visualRoot.localScale = new Vector3(Mathf.Abs(visualScale.x), visualScale.y, visualScale.z);
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = facing == CharacterFacing.RIGHT;
        }
    }

    public void HideForTraversal(float failSafeSeconds)
    {
        EnsureVisualReferences();
        RestoreTraversalVisibility();
        EnsureVisibleForActiveLifecycle();

        traversalRendererStates = CaptureRendererVisibility();
        traversalCanvasStates = CaptureCanvasVisibility();
        isTraversalHidden = true;
        traversalVisibilityRestoreDeadline = RequireGameClock().Time + Mathf.Max(0f, failSafeSeconds);
        SetTraversalVisible(false);

        if (isActiveAndEnabled && failSafeSeconds > 0f)
        {
            traversalVisibilityRestoreRoutine = StartCoroutine(RestoreTraversalVisibilityAfter(failSafeSeconds));
        }
    }

    public void RestoreTraversalVisibility()
    {
        StopTraversalVisibilityTimer();
        RestoreTraversalVisibilityNow();
    }

    private IEnumerator RestoreTraversalVisibilityAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        traversalVisibilityRestoreRoutine = null;
        RestoreTraversalVisibilityNow();
    }

    private void StopTraversalVisibilityTimer()
    {
        if (traversalVisibilityRestoreRoutine == null)
        {
            return;
        }

        StopCoroutine(traversalVisibilityRestoreRoutine);
        traversalVisibilityRestoreRoutine = null;
    }

    private void RestoreTraversalVisibilityNow()
    {
        if (!isTraversalHidden)
        {
            return;
        }

        if (traversalRendererStates != null)
        {
            foreach (RendererVisibilityState state in traversalRendererStates)
            {
                if (state.Renderer != null)
                {
                    state.Renderer.enabled = state.Enabled;
                }
            }
        }

        if (traversalCanvasStates != null)
        {
            foreach (CanvasVisibilityState state in traversalCanvasStates)
            {
                if (state.Canvas != null)
                {
                    state.Canvas.enabled = state.Enabled;
                }
            }
        }

        traversalRendererStates = null;
        traversalCanvasStates = null;
        isTraversalHidden = false;
        traversalVisibilityRestoreDeadline = -1f;

        EnsureVisibleForActiveLifecycle();
    }

    public void RecoverExpiredTraversalVisibility()
    {
        if (isTraversalHidden)
        {
            if (traversalVisibilityRestoreDeadline >= 0f
                && RequireGameClock().Time >= traversalVisibilityRestoreDeadline)
            {
                RestoreTraversalVisibility();
            }

            return;
        }

        if (!CanRecoverActiveVisibility())
        {
            return;
        }

        EnsureVisualReferences();
        if (spriteRenderer != null && !spriteRenderer.enabled)
        {
            spriteRenderer.enabled = true;
        }
    }

    private List<RendererVisibilityState> CaptureRendererVisibility()
    {
        List<RendererVisibilityState> states = new List<RendererVisibilityState>();
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null)
            {
                states.Add(new RendererVisibilityState(renderer, renderer.enabled));
            }
        }

        return states;
    }

    private List<CanvasVisibilityState> CaptureCanvasVisibility()
    {
        List<CanvasVisibilityState> states = new List<CanvasVisibilityState>();
        foreach (Canvas canvas in GetComponentsInChildren<Canvas>(true))
        {
            if (canvas != null)
            {
                states.Add(new CanvasVisibilityState(canvas, canvas.enabled));
            }
        }

        return states;
    }

    private void SetTraversalVisible(bool value)
    {
        if (traversalRendererStates != null)
        {
            foreach (RendererVisibilityState state in traversalRendererStates)
            {
                if (state.Renderer != null)
                {
                    state.Renderer.enabled = value;
                }
            }
        }

        if (traversalCanvasStates != null)
        {
            foreach (CanvasVisibilityState state in traversalCanvasStates)
            {
                if (state.Canvas != null)
                {
                    state.Canvas.enabled = value;
                }
            }
        }
    }

    public void SetRenderersVisible(bool value)
    {
        RestoreTraversalVisibility();
        SetSpriteRenderersVisible(value);
    }

    public void EnsureVisibleForActiveLifecycle()
    {
        CharacterLifecycleState currentState = lifecycle != null
            ? lifecycle.CurrentState
            : CharacterLifecycleState.None;
        if (currentState == CharacterLifecycleState.OnExpedition
            || currentState == CharacterLifecycleState.Despawned)
        {
            return;
        }

        SetSpriteRenderersVisible(true);
    }

    private bool CanRecoverActiveVisibility()
    {
        if (identity == null)
        {
            identity = GetComponent<CharacterIdentity>();
        }

        if (lifecycle == null)
        {
            lifecycle = GetComponent<CharacterLifecycle>();
        }

        return identity != null
            && identity.Data != null
            && lifecycle != null
            && lifecycle.CurrentState == CharacterLifecycleState.Active;
    }

    private void SetSpriteRenderersVisible(bool value)
    {
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.enabled = value;
        }
    }

    private void CleanupVisualTweens()
    {
        StopTraversalVisibilityTimer();
        KillFadeTween();
        DOTween.Kill(this, complete: false);
        DOTween.Kill(gameObject, complete: false);
        DOTween.Kill(transform, complete: false);
        if (visualRoot != null)
        {
            DOTween.Kill(visualRoot, complete: false);
            DOTween.Kill(visualRoot.gameObject, complete: false);
        }

        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer != null)
            {
                KillRendererTweens(renderer);
            }
        }
    }

    private static void KillRendererTweens(SpriteRenderer renderer)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.DOKill();
        DOTween.Kill(renderer, complete: false);
        DOTween.Kill(renderer.gameObject, complete: false);
        DOTween.Kill(renderer.transform, complete: false);
    }

    private void KillFadeTween()
    {
        if (fadeTween != null && fadeTween.IsActive())
        {
            fadeTween.Kill(complete: false);
        }

        fadeTween = null;
    }

    private readonly struct RendererVisibilityState
    {
        public RendererVisibilityState(Renderer renderer, bool enabled)
        {
            Renderer = renderer;
            Enabled = enabled;
        }

        public Renderer Renderer { get; }
        public bool Enabled { get; }
    }

    private readonly struct CanvasVisibilityState
    {
        public CanvasVisibilityState(Canvas canvas, bool enabled)
        {
            Canvas = canvas;
            Enabled = enabled;
        }

        public Canvas Canvas { get; }
        public bool Enabled { get; }
    }
}
