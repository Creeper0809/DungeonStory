using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using VContainer.Unity;

public sealed class GuestRequestDeliveryPresentationObserver :
    IInitializable, IDisposable, IDungeonSaveRestoreCompletedHook
{
    private readonly IGameEventBus events;
    private readonly Dictionary<string, string> fingerprints = new(StringComparer.Ordinal);
    private readonly HashSet<(string source, string fingerprint)> publishing = new();
    private IDisposable subscription;
    private IDisposable finalizedSubscription;
    private int generation;
    public GuestRequestDeliveryPresentationObserver(IGameEventBus events) =>
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    public void Initialize()
    {
        if (subscription != null) return;
        ++generation;
        try
        {
            subscription = events.Subscribe<GuestRequestDeliveryProgressedFact>(Observe);
            finalizedSubscription = events.Subscribe<GuestRequestDeliveryFinalizedFact>(value =>
                fingerprints.Remove(value.InstanceId));
        }
        catch { Dispose(); throw; }
    }
    public void Dispose()
    {
        subscription?.Dispose(); subscription = null;
        finalizedSubscription?.Dispose(); finalizedSubscription = null;
        OnRestoreCompleted();
    }
    public void OnRestoreCompleted()
    {
        ++generation;
        fingerprints.Clear();
    }
    private void Observe(GuestRequestDeliveryProgressedFact fact)
    {
        try
        {
            EventAlertRequest request = GuestRequestDeliveryAlertProjection.Build(fact);
            string fingerprint = GuestRequestDeliveryAlertProjection.Fingerprint(fact, request);
            if (fingerprints.TryGetValue(request.SourceId, out string previous)
                && string.Equals(previous, fingerprint, StringComparison.Ordinal)) return;
            var pending = (request.SourceId, fingerprint);
            if (!publishing.Add(pending)) return;
            int observedGeneration = generation;
            try
            {
                events.Publish(new EventAlertRequestedEvent(request));
                if (observedGeneration == generation)
                    fingerprints[request.SourceId] = fingerprint;
            }
            finally { publishing.Remove(pending); }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        { Debug.LogError("guest-delivery-presentation-observer:" + exception.GetType().Name + ":" + exception.Message); }
    }
}
