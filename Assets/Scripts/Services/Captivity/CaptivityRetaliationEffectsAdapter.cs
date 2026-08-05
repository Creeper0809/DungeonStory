using System;
using DungeonStory.Foundation;

internal sealed class CaptivityRetaliationEffectsAdapter : ICaptivityRetaliationEffectsPort
{
    private readonly ICaptivityRuntime captivity;
    private readonly InvasionThreatRuntime threat;
    private readonly IGameEventBus events;

    internal CaptivityRetaliationEffectsAdapter(
        ICaptivityRuntime captivity,
        InvasionSceneRuntimeReferences invasionRuntimes,
        IGameEventBus events)
    {
        this.captivity = captivity ?? throw new ArgumentNullException(nameof(captivity));
        threat = invasionRuntimes?.Threat;
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public bool TryGetCaptive(string captiveId, out CaptiveState captive) =>
        captivity.TryGetCaptive(captiveId, out captive);

    public void ApplyThreat(float amount, bool forceCandidate)
    {
        if (threat == null)
        {
            return;
        }

        threat.AddThreat(amount);
        if (forceCandidate)
        {
            threat.ForceCandidateNow();
        }
    }

    public void RaiseAlert(
        string title,
        string message,
        CaptivityMilestoneImportance importance,
        string category) =>
        events.RaiseAlert(
            title,
            message,
            importance == CaptivityMilestoneImportance.High
                ? EventAlertImportance.High
                : EventAlertImportance.Medium,
            category);
}
