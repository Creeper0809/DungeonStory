using System;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

/// <summary>
/// Emits post-commit expedition presentation and identity events from a frozen fact;
/// it never mutates campaign, reward, or expedition state.
/// </summary>
public sealed class OffenseExpeditionResultPresentationObserver :
    IInitializable,
    IDisposable
{
    private readonly IGameEventBus events;
    private readonly CharacterIdentityEventPublisher identityEvents;
    private IDisposable subscription;

    public OffenseExpeditionResultPresentationObserver(
        IGameEventBus events,
        CharacterIdentityEventPublisher identityEvents)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.identityEvents = identityEvents
            ?? throw new ArgumentNullException(nameof(identityEvents));
    }

    public void Initialize()
    {
        if (subscription != null)
            return;
        subscription = events.Subscribe<OffenseExpeditionResolvedFact>(Observe);
    }

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    private void Observe(OffenseExpeditionResolvedFact fact)
    {
        if (fact == null)
        {
            Debug.LogError("Offense expedition presentation received a null fact.");
            return;
        }

        OffenseExpeditionResultFactData data;
        OffenseExpeditionResult result;
        try
        {
            data = fact.Snapshot();
            result = fact.Result;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            Debug.LogException(exception);
            return;
        }

        if (result.success)
        {
            TryNotify(() => events.Publish(new OffenseRewardGrantedEvent(
                result,
                result.grantedRewards)));
        }
        PublishRewardPresentation(result);
        TryNotify(() => events.RaiseAlert(
            "expedition-result",
            result.ToDetailText(),
            result.success
                ? EventAlertImportance.Medium
                : EventAlertImportance.High,
            "offense"));
        TryNotify(() => PublishIdentity(data, result));
    }

    private void PublishRewardPresentation(OffenseExpeditionResult result)
    {
        foreach (OffenseRewardGrantResult grant in result.grantedRewards)
        {
            if (grant.stockSupply != null)
            {
                OffenseStockRewardReceiptData stock =
                    grant.stockSupply.Snapshot();
                StockSupplyResult notification = new(
                    stock.success,
                    stock.category,
                    stock.requestedAmount,
                    stock.deliveredAmount,
                    0,
                    stock.sourceLabel,
                    stock.reason);
                TryNotify(() => events.Publish(
                    new StockSupplyEvent(notification)));
            }

            if (grant.success
                && grant.category == OffenseRewardCategory.Blueprint)
            {
                TryNotify(() => events.RaiseAlert(
                    "원정 설계도 도착",
                    grant.detail,
                    EventAlertImportance.Low,
                    "연구"));
            }
        }
    }

    private void PublishIdentity(
        OffenseExpeditionResultFactData data,
        OffenseExpeditionResult result)
    {
        CharacterId[] participants = data.participants
            .Where(value => value.identityParticipant)
            .Select(value => new CharacterId(value.characterId))
            .Where(value => value.IsValid)
            .Distinct()
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
        if (participants.Length == 0)
            return;
        identityEvents.Publish(new ExpeditionOutcomeEvent(
            result.expeditionId,
            participants,
            result.success ? "success" : "failure",
            data.absoluteDay));
    }

    private static void TryNotify(Action notify)
    {
        try
        {
            notify();
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            Debug.LogException(exception);
        }
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException
        && exception is not StackOverflowException
        && exception is not AccessViolationException;
}
