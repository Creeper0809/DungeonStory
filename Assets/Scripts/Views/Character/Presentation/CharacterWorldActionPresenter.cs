using System;
using DungeonStory.Foundation;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterWorldActionPresenter : MonoBehaviour
{
    private const float PixelSize =
        1f / WorldInteractionPresentationCatalogSO.PixelsPerUnit;

    private CharacterActor actor;
    private CharacterVisual visual;
    private CharacterProceduralMotionPresenter motionPresenter;
    private IGameClock gameClock;
    private SpriteRenderer toolRenderer;
    private SpriteRenderer effectRenderer;
    private CharacterWorldActionKind actionKind;
    private CharacterLifecycleState lastLifecycleState;
    private int lastBrainVersion = int.MinValue;
    private int lastPulseIndex = int.MinValue;
    private float effectVisibleUntil = float.NegativeInfinity;
    private bool visible;
    private bool reducedMotion;
    private int visibleTickCount;
    private int hiddenTickCount;

    public CharacterWorldActionKind CurrentActionKind => actionKind;
    public int VisibleTickCount => visibleTickCount;
    public int HiddenTickCount => hiddenTickCount;
    public SpriteRenderer ToolRenderer => toolRenderer;
    public SpriteRenderer EffectRenderer => effectRenderer;

    public static CharacterWorldActionPresenter Ensure(
        CharacterActor actor,
        IGameClock gameClock,
        CharacterProceduralMotionPresenter motionPresenter,
        bool reducedMotion)
    {
        if (actor == null)
        {
            return null;
        }

        CharacterWorldActionPresenter presenter =
            actor.GetComponent<CharacterWorldActionPresenter>();
        if (presenter == null && Application.isPlaying)
        {
            presenter = actor.gameObject.AddComponent<CharacterWorldActionPresenter>();
        }

        presenter?.Configure(actor, gameClock, motionPresenter, reducedMotion);
        return presenter;
    }

    public void Configure(
        CharacterActor actor,
        IGameClock gameClock,
        CharacterProceduralMotionPresenter motionPresenter,
        bool reducedMotion)
    {
        this.actor = actor;
        visual = actor != null ? actor.GetComponent<CharacterVisual>() : null;
        this.gameClock = gameClock;
        this.motionPresenter = motionPresenter;
        this.reducedMotion = reducedMotion;
        lastLifecycleState = actor != null
            ? actor.CurrentLifecycleState
            : CharacterLifecycleState.None;
        EnsureRenderers();
        RefreshAction(force: true);
    }

    public void TickPresentation(bool isVisible)
    {
        if (actor == null || gameClock == null)
        {
            return;
        }

        if (visible != isVisible)
        {
            visible = isVisible;
            if (!visible)
            {
                hiddenTickCount++;
                ResetPresentation();
                return;
            }

            RefreshAction(force: true);
        }

        if (!visible)
        {
            return;
        }

        visibleTickCount++;
        CharacterLifecycleState lifecycleState = actor.CurrentLifecycleState;
        if (lifecycleState != lastLifecycleState)
        {
            lastLifecycleState = lifecycleState;
            motionPresenter?.ResetVisualRootToDefault();
            ResetPresentation();
            RefreshAction(force: true);
        }
        else
        {
            RefreshAction(force: false);
        }

        motionPresenter?.SetActionKind(actionKind);
        UpdateRendererFacingAndLayer();
        TickActionPulse();
        if (effectRenderer != null
            && effectRenderer.enabled
            && gameClock.Time > effectVisibleUntil)
        {
            effectRenderer.enabled = false;
        }
    }

    public void ResetPresentation()
    {
        lastPulseIndex = int.MinValue;
        effectVisibleUntil = float.NegativeInfinity;
        if (toolRenderer != null)
        {
            toolRenderer.enabled = false;
        }

        if (effectRenderer != null)
        {
            effectRenderer.enabled = false;
        }

        motionPresenter?.ResetVisualRootToDefault();
    }

    private void RefreshAction(bool force)
    {
        AIBrain brain = actor != null ? actor.Brain : null;
        int version = brain != null ? brain.DebugVersion : -1;
        if (!force && version == lastBrainVersion)
        {
            return;
        }

        lastBrainVersion = version;
        CharacterWorldActionKind next = ClassifyAction(
            brain != null ? brain.CurrentActionDebugLabel : string.Empty,
            brain != null ? brain.CurrentActionPhase : string.Empty,
            actor?.Blackboard?.CurrentTask,
            actor?.Blackboard?.CurrentStatus);
        if (actionKind != next)
        {
            actionKind = next;
            lastPulseIndex = int.MinValue;
            motionPresenter?.ResetVisualRootToDefault();
        }

        RefreshToolSprite();
    }

    private void TickActionPulse()
    {
        float interval = ResolvePulseInterval(actionKind);
        if (interval <= 0f || gameClock.IsPaused)
        {
            return;
        }

        int pulseIndex = Mathf.FloorToInt(gameClock.Time / interval);
        if (pulseIndex == lastPulseIndex)
        {
            return;
        }

        lastPulseIndex = pulseIndex;
        bool flipX = visual != null
            && visual.VisualRenderer != null
            && visual.VisualRenderer.flipX;
        float direction = flipX ? 1f : -1f;
        if (actionKind != CharacterWorldActionKind.Rest
            && actionKind != CharacterWorldActionKind.Idle
            && actionKind != CharacterWorldActionKind.Move
            && actionKind != CharacterWorldActionKind.Carry)
        {
            motionPresenter?.TriggerImpact(direction);
        }

        ShowEffectForAction(direction);
    }

    private void RefreshToolSprite()
    {
        EnsureRenderers();
        CharacterPresentationSpriteKind? spriteKind = actionKind switch
        {
            CharacterWorldActionKind.Construct
                or CharacterWorldActionKind.Repair => CharacterPresentationSpriteKind.Hammer,
            CharacterWorldActionKind.Clean => CharacterPresentationSpriteKind.Broom,
            CharacterWorldActionKind.Craft
                or CharacterWorldActionKind.Cook => CharacterPresentationSpriteKind.Ladle,
            CharacterWorldActionKind.Eat
                or CharacterWorldActionKind.Drink => CharacterPresentationSpriteKind.Cup,
            CharacterWorldActionKind.Reception
                or CharacterWorldActionKind.Payment => CharacterPresentationSpriteKind.Coin,
            CharacterWorldActionKind.Medical => CharacterPresentationSpriteKind.Medical,
            _ => null
        };
        toolRenderer.sprite = spriteKind.HasValue
            ? CharacterPresentationSpriteFactory.Get(spriteKind.Value)
            : null;
        toolRenderer.enabled = visible && toolRenderer.sprite != null;
    }

    private void ShowEffectForAction(float direction)
    {
        CharacterPresentationSpriteKind? effectKind = actionKind switch
        {
            CharacterWorldActionKind.Construct
                or CharacterWorldActionKind.Repair
                or CharacterWorldActionKind.Craft => CharacterPresentationSpriteKind.Spark,
            CharacterWorldActionKind.Clean => CharacterPresentationSpriteKind.Dust,
            CharacterWorldActionKind.Cook => CharacterPresentationSpriteKind.Steam,
            CharacterWorldActionKind.Hygiene => CharacterPresentationSpriteKind.Bubble,
            CharacterWorldActionKind.Payment => CharacterPresentationSpriteKind.Coin,
            CharacterWorldActionKind.Medical => CharacterPresentationSpriteKind.Medical,
            _ => null
        };
        if (!effectKind.HasValue)
        {
            return;
        }

        EnsureRenderers();
        effectRenderer.sprite = CharacterPresentationSpriteFactory.Get(effectKind.Value);
        effectRenderer.transform.localPosition = new Vector3(
            7f * PixelSize * direction,
            8f * PixelSize,
            0f);
        effectRenderer.enabled = true;
        effectVisibleUntil = gameClock.Time
            + (reducedMotion ? 0.08f : 0.14f);
    }

    private void UpdateRendererFacingAndLayer()
    {
        SpriteRenderer characterRenderer = visual != null ? visual.VisualRenderer : null;
        if (characterRenderer == null)
        {
            return;
        }

        float direction = characterRenderer.flipX ? 1f : -1f;
        toolRenderer.transform.localPosition = new Vector3(
            6f * PixelSize * direction,
            7f * PixelSize,
            0f);
        toolRenderer.flipX = !characterRenderer.flipX;
        toolRenderer.sortingLayerID = characterRenderer.sortingLayerID;
        toolRenderer.sortingOrder = characterRenderer.sortingOrder + 1;
        effectRenderer.sortingLayerID = characterRenderer.sortingLayerID;
        effectRenderer.sortingOrder = characterRenderer.sortingOrder + 2;
    }

    private void EnsureRenderers()
    {
        if (toolRenderer == null)
        {
            toolRenderer = CreateRenderer("ActionProp");
        }

        if (effectRenderer == null)
        {
            effectRenderer = CreateRenderer("ActionEffect");
        }
    }

    private SpriteRenderer CreateRenderer(string objectName)
    {
        Transform existing = transform.Find(objectName);
        GameObject target = existing != null
            ? existing.gameObject
            : new GameObject(objectName);
        target.transform.SetParent(transform, worldPositionStays: false);
        SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = target.AddComponent<SpriteRenderer>();
        }

        renderer.enabled = false;
        return renderer;
    }

    private static CharacterWorldActionKind ClassifyAction(
        string action,
        string phase,
        string task,
        string status)
    {
        if (ContainsAny(action, phase, task, status, "construct", "건설", "build"))
        {
            return CharacterWorldActionKind.Construct;
        }

        if (ContainsAny(action, phase, task, status, "repair", "수리", "maintenance"))
        {
            return CharacterWorldActionKind.Repair;
        }

        if (ContainsAny(action, phase, task, status, "clean", "청소", "오염"))
        {
            return CharacterWorldActionKind.Clean;
        }

        if (ContainsAny(action, phase, task, status, "cook", "조리", "요리"))
        {
            return CharacterWorldActionKind.Cook;
        }

        if (ContainsAny(action, phase, task, status, "craft", "제작", "생산", "research", "연구"))
        {
            return CharacterWorldActionKind.Craft;
        }

        if (ContainsAny(action, phase, task, status, "eat", "식사", "먹"))
        {
            return CharacterWorldActionKind.Eat;
        }

        if (ContainsAny(action, phase, task, status, "drink", "음료", "마시"))
        {
            return CharacterWorldActionKind.Drink;
        }

        if (ContainsAny(action, phase, task, status, "hygiene", "wash", "씻", "목욕", "화장실"))
        {
            return CharacterWorldActionKind.Hygiene;
        }

        if (ContainsAny(action, phase, task, status, "rest", "sleep", "휴식", "수면"))
        {
            return CharacterWorldActionKind.Rest;
        }

        if (ContainsAny(action, phase, task, status, "reception", "접객", "응대", "serve"))
        {
            return CharacterWorldActionKind.Reception;
        }

        if (ContainsAny(action, phase, task, status, "payment", "checkout", "결제", "계산"))
        {
            return CharacterWorldActionKind.Payment;
        }

        if (ContainsAny(action, phase, task, status, "treat", "surgery", "medical", "치료", "수술", "구조"))
        {
            return CharacterWorldActionKind.Medical;
        }

        if (ContainsAny(action, phase, task, status, "attack", "guard", "combat", "공격", "경비", "전투"))
        {
            return CharacterWorldActionKind.Combat;
        }

        if (ContainsAny(action, phase, task, status, "move", "이동", "haul", "운반"))
        {
            return CharacterWorldActionKind.Move;
        }

        return CharacterWorldActionKind.Idle;
    }

    private static bool ContainsAny(
        string first,
        string second,
        string third,
        string fourth,
        params string[] tokens)
    {
        for (int index = 0; index < tokens.Length; index++)
        {
            string token = tokens[index];
            if (Contains(first, token)
                || Contains(second, token)
                || Contains(third, token)
                || Contains(fourth, token))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Contains(string value, string token)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static float ResolvePulseInterval(CharacterWorldActionKind kind)
    {
        return kind switch
        {
            CharacterWorldActionKind.Construct
                or CharacterWorldActionKind.Repair => 0.32f,
            CharacterWorldActionKind.Clean => 0.4f,
            CharacterWorldActionKind.Craft
                or CharacterWorldActionKind.Cook => 0.46f,
            CharacterWorldActionKind.Eat
                or CharacterWorldActionKind.Drink
                or CharacterWorldActionKind.Hygiene => 0.58f,
            CharacterWorldActionKind.Reception
                or CharacterWorldActionKind.Payment => 0.65f,
            CharacterWorldActionKind.Combat => 0.3f,
            CharacterWorldActionKind.Medical => 0.5f,
            _ => 0f
        };
    }

    private void OnDisable()
    {
        visible = false;
        ResetPresentation();
    }

    private void OnDestroy()
    {
        ResetPresentation();
    }
}
