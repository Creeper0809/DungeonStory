using TMPro;
using System;
using DungeonStory.Foundation;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

public enum CharacterFeedbackState
{
    None,
    Joy,
    Discontent,
    Confused,
    Anger,
    Fatigue
}

[RequireComponent(typeof(CharacterStats))]
[RequireComponent(typeof(CharacterLog))]
[RequireComponent(typeof(CharacterVisual))]
[RequireComponent(typeof(CharacterActor))]
[DrawWithUnity]
public class CharacterFeedbackBubble : MonoBehaviour
{
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.35f, 0f);
    [SerializeField] private float temporaryDuration = 2.5f;
    [SerializeField] private float logFeedbackCooldown = 0.35f;
    [SerializeField] private TextMeshPro text;

    private CharacterActor actor;
    private CharacterStats characterStats;
    private CharacterLog characterLog;
    private CharacterVisual characterVisual;
    private float visibleUntil;
    private float nextLogFeedbackTime;
    private ICharacterAiSchedulingService aiSchedulingService;
    private ICharacterFeedbackBubbleViewFactory bubbleViewFactory;
    private IGameClock gameClock;

    public CharacterFeedbackState CurrentState { get; private set; } = CharacterFeedbackState.None;

    [Inject]
    public void ConstructCharacterFeedbackBubble(
        ICharacterAiSchedulingService aiSchedulingService,
        ICharacterFeedbackBubbleViewFactory bubbleViewFactory,
        IGameClock gameClock)
    {
        this.aiSchedulingService = aiSchedulingService
            ?? throw new ArgumentNullException(nameof(aiSchedulingService));
        this.bubbleViewFactory = bubbleViewFactory
            ?? throw new ArgumentNullException(nameof(bubbleViewFactory));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
    }

    private void Awake()
    {
        actor = GetComponent<CharacterActor>();
        characterStats = GetComponent<CharacterStats>();
        characterLog = GetComponent<CharacterLog>();
        characterVisual = GetComponent<CharacterVisual>();
        ApplyState(CharacterFeedbackState.None);
    }

    private void OnEnable()
    {
        if (actor == null)
        {
            actor = GetComponent<CharacterActor>();
        }

        if (characterStats == null)
        {
            characterStats = GetComponent<CharacterStats>();
        }

        if (characterLog == null)
        {
            characterLog = GetComponent<CharacterLog>();
        }

        if (characterLog != null)
        {
            characterLog.OnLogAdded += OnLogAdded;
        }

        if (characterStats != null)
        {
            characterStats.OnStatChange += OnStatChanged;
        }
    }

    private void OnDisable()
    {
        if (characterLog != null)
        {
            characterLog.OnLogAdded -= OnLogAdded;
        }

        if (characterStats != null)
        {
            characterStats.OnStatChange -= OnStatChanged;
        }

        if (text != null)
        {
            text.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (aiSchedulingService == null || bubbleViewFactory == null)
        {
            HideView();
            return;
        }

        if (!aiSchedulingService.ShouldShowCharacterFeedback(actor))
        {
            HideView();
            return;
        }

        if (CurrentState != CharacterFeedbackState.None && gameClock.Time <= visibleUntil)
        {
            EnsureView();
            text.transform.localPosition = GetLocalOffset();
            return;
        }

        ApplyState(EvaluatePersistentState());
    }

    public void Show(CharacterFeedbackState state)
    {
        if (aiSchedulingService == null || !aiSchedulingService.ShouldShowCharacterFeedback(actor))
        {
            return;
        }

        ApplyState(state);
        visibleUntil = gameClock.Time + temporaryDuration;
    }

    public CharacterFeedbackState EvaluatePersistentState()
    {
        if (characterStats == null)
        {
            characterStats = GetComponent<CharacterStats>();
        }

        if (characterStats == null || characterStats.Stats == null)
        {
            return CharacterFeedbackState.None;
        }

        float sleep = GetStat(CharacterCondition.SLEEP, 100f);
        float mood = GetStat(CharacterCondition.MOOD, 100f);
        float excretion = GetStat(CharacterCondition.EXCRETION, 100f);
        float hygiene = GetStat(CharacterCondition.HYGIENE, 100f);
        if (sleep <= 25f)
        {
            return CharacterFeedbackState.Fatigue;
        }

        if (excretion <= 15f)
        {
            return CharacterFeedbackState.Confused;
        }

        if (hygiene <= 20f)
        {
            return CharacterFeedbackState.Discontent;
        }

        if (mood <= 15f)
        {
            return CharacterFeedbackState.Anger;
        }

        if (mood <= 35f)
        {
            return CharacterFeedbackState.Discontent;
        }

        return CharacterFeedbackState.None;
    }

    public static CharacterFeedbackState ClassifyActivity(CharacterActivityEvent activity)
    {
        if (activity == null || !activity.BubbleEligible)
        {
            return CharacterFeedbackState.None;
        }

        if (activity.Sentiment >= 0.35f
            || activity.OutcomeId == CharacterActivityOutcomes.Completed
            || activity.OutcomeId == CharacterActivityOutcomes.Returned)
        {
            return CharacterFeedbackState.Joy;
        }

        if (activity.Sentiment <= -0.75f
            || activity.OutcomeId == CharacterActivityOutcomes.Defeated
            || (activity.KindId == CharacterActivityKinds.Combat
                && activity.OutcomeId == CharacterActivityOutcomes.Damaged))
        {
            return CharacterFeedbackState.Anger;
        }

        if (activity.KindId == CharacterActivityKinds.Duty
            && (activity.OutcomeId == CharacterActivityOutcomes.Blocked
                || activity.OutcomeId == CharacterActivityOutcomes.Cancelled))
        {
            return CharacterFeedbackState.Fatigue;
        }

        if ((activity.KindId == CharacterActivityKinds.Shopping
                || activity.KindId == CharacterActivityKinds.Stock
                || activity.KindId == CharacterActivityKinds.Wait)
            && (activity.OutcomeId == CharacterActivityOutcomes.Failed
                || activity.OutcomeId == CharacterActivityOutcomes.Blocked))
        {
            return CharacterFeedbackState.Confused;
        }

        if (activity.Sentiment < 0f
            || activity.OutcomeId == CharacterActivityOutcomes.Failed
            || activity.OutcomeId == CharacterActivityOutcomes.Blocked
            || activity.OutcomeId == CharacterActivityOutcomes.Cancelled)
        {
            return CharacterFeedbackState.Discontent;
        }

        return CharacterFeedbackState.None;
    }

    public static string GetSymbol(CharacterFeedbackState state)
    {
        return state switch
        {
            CharacterFeedbackState.Joy => ":)",
            CharacterFeedbackState.Discontent => ":/",
            CharacterFeedbackState.Confused => "?",
            CharacterFeedbackState.Anger => "!",
            CharacterFeedbackState.Fatigue => "Zz",
            _ => string.Empty
        };
    }

    private void OnLogAdded(CharacterLogEntry entry)
    {
        if (gameClock.Time < nextLogFeedbackTime
            || !RequireAiSchedulingService().ShouldShowCharacterFeedback(actor))
        {
            return;
        }

        CharacterFeedbackState state = ClassifyActivity(entry.Activity);
        if (state != CharacterFeedbackState.None)
        {
            nextLogFeedbackTime = gameClock.Time + logFeedbackCooldown;
            Show(state);
        }
    }

    private void OnStatChanged(System.Collections.Generic.IReadOnlyDictionary<CharacterCondition, float> stats)
    {
        if (CurrentState == CharacterFeedbackState.None || gameClock.Time > visibleUntil)
        {
            ApplyState(EvaluatePersistentState());
        }
    }

    private void ApplyState(CharacterFeedbackState state)
    {
        CurrentState = state;
        if (state != CharacterFeedbackState.None)
        {
            EnsureView();
        }

        if (text == null)
        {
            return;
        }

        string symbol = GetSymbol(state);
        text.text = symbol;
        text.gameObject.SetActive(!string.IsNullOrWhiteSpace(symbol));
        text.color = GetColor(state);
    }

    private void HideView()
    {
        CurrentState = CharacterFeedbackState.None;
        ReleaseView();
    }

    private void EnsureView()
    {
        if (text != null)
        {
            return;
        }

        if (bubbleViewFactory == null)
        {
            return;
        }

        text = bubbleViewFactory.Acquire(transform, GetLocalOffset());
    }

    private void ReleaseView()
    {
        if (text == null)
        {
            return;
        }

        if (bubbleViewFactory == null)
        {
            text.gameObject.SetActive(false);
            text = null;
            return;
        }

        bubbleViewFactory.Release(text);
        text = null;
    }

    private Vector3 GetLocalOffset()
    {
        if (characterVisual == null)
        {
            characterVisual = GetComponent<CharacterVisual>();
        }

        if (characterVisual == null)
        {
            return localOffset;
        }

        float y = Mathf.Max(localOffset.y, characterVisual.GetVisualTopLocalY() + 0.35f);
        return new Vector3(localOffset.x, y, localOffset.z);
    }

    private float GetStat(CharacterCondition condition, float defaultValue)
    {
        return characterStats != null
            && characterStats.Stats != null
            && characterStats.Stats.TryGetValue(condition, out float value)
                ? value
                : defaultValue;
    }

    private ICharacterAiSchedulingService RequireAiSchedulingService()
    {
        return aiSchedulingService
            ?? throw new InvalidOperationException($"{nameof(CharacterFeedbackBubble)} requires {nameof(ICharacterAiSchedulingService)} injection.");
    }

    private ICharacterFeedbackBubbleViewFactory RequireBubbleViewFactory()
    {
        return bubbleViewFactory
            ?? throw new InvalidOperationException(
                $"{nameof(CharacterFeedbackBubble)} requires {nameof(ICharacterFeedbackBubbleViewFactory)} injection.");
    }

    private static Color GetColor(CharacterFeedbackState state)
    {
        return state switch
        {
            CharacterFeedbackState.Joy => new Color(0.45f, 1f, 0.55f),
            CharacterFeedbackState.Discontent => new Color(1f, 0.9f, 0.35f),
            CharacterFeedbackState.Confused => new Color(0.65f, 0.85f, 1f),
            CharacterFeedbackState.Anger => new Color(1f, 0.25f, 0.2f),
            CharacterFeedbackState.Fatigue => new Color(0.75f, 0.7f, 1f),
            _ => Color.white
        };
    }

}
