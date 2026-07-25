using System;
using DungeonStory.Foundation;
using VContainer.Unity;

public sealed class CaptivityRetaliationRuntime : IStartable, IDisposable
{
    private readonly ICaptivityRuntime captivity;
    private readonly IInvasionThreatRuntimeProvider threatProvider;
    private readonly IGameEventBus events;
    private IDisposable ransomSubscription;
    private IDisposable escapeSubscription;

    public CaptivityRetaliationRuntime(
        ICaptivityRuntime captivity,
        IInvasionThreatRuntimeProvider threatProvider,
        IGameEventBus events)
    {
        this.captivity = captivity ?? throw new ArgumentNullException(nameof(captivity));
        this.threatProvider = threatProvider
            ?? throw new ArgumentNullException(nameof(threatProvider));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Start()
    {
        ransomSubscription = events.Subscribe<CaptiveRansomedEvent>(OnRansomed);
        escapeSubscription = events.Subscribe<CaptiveEscapedEvent>(OnEscaped);
    }

    public void Dispose()
    {
        ransomSubscription?.Dispose();
        escapeSubscription?.Dispose();
        ransomSubscription = null;
        escapeSubscription = null;
    }

    private void OnRansomed(CaptiveRansomedEvent gameEvent)
    {
        float pressure = Math.Max(0f, gameEvent.RetaliationPressure);
        ApplyPressure(
            pressure,
            pressure >= 70f
                ? "몸값 협상이 적대 세력의 구출·보복 준비를 앞당겼습니다."
                : "몸값을 받은 대가로 적대 세력의 시선이 던전에 쏠립니다.");
    }

    private void OnEscaped(CaptiveEscapedEvent gameEvent)
    {
        float pressure = gameEvent.Betrayal ? 45f : 28f;
        if (captivity.TryGetCaptive(gameEvent.CaptiveId, out CaptiveState captive))
        {
            pressure += captive.retaliationPressure * 0.55f;
            pressure += captive.grudge * 0.2f;
        }

        ApplyPressure(
            pressure,
            gameEvent.Betrayal
                ? "거짓 복종자가 탈출해 감방 구조와 경비 정보를 넘겼습니다."
                : "탈출한 포로가 던전의 처우와 경로를 외부에 증언했습니다.");
    }

    private void ApplyPressure(float pressure, string message)
    {
        float clamped = UnityEngine.Mathf.Clamp(pressure, 0f, 100f);
        if (threatProvider.TryGetRuntime(out InvasionThreatRuntime threat))
        {
            threat.AddThreat(clamped * 0.45f);
            if (clamped >= 85f)
            {
                threat.ForceCandidateNow();
            }
        }

        events.RaiseAlert(
            clamped >= 70f ? "포로 관련 보복 위험" : "포로 소문 확산",
            message,
            clamped >= 70f
                ? EventAlertImportance.High
                : EventAlertImportance.Medium,
            "포로");
    }
}
