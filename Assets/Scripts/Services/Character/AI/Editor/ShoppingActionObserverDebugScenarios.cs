using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ShoppingActionObserverDebugScenarios
{
    public static void RunOrThrow()
    {
        var events = new GameEventBus();
        var host = new GameObject("B107 Shopping Actor");
        try
        {
            CharacterActor actor = host.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.InjectWithDomainEvents(host, events);
            actor.EnsureRuntimeState();
            Require(actor.LogComponent != null,
                "Shopping observer fixture did not initialize the character log.");

            ShoppingActionObservedEvent[] facts =
            {
                new(actor, ShoppingActionTransition.MissingDestination),
                new(actor, ShoppingActionTransition.MissingMovementContext),
                new(actor, ShoppingActionTransition.DestinationUnreachable),
                new(actor, ShoppingActionTransition.LooseItemStolen,
                    itemStackId: "stack:b107:floor-theft",
                    itemDisplayName: "",
                    itemValue: 37.5f)
            };
            AssertProjection(
                CharacterActionActivityObserver.Project(facts[0]),
                CharacterActivityOutcomes.Failed,
                "쇼핑 실패: 목적지 없음",
                "shopping:visit",
                "missing-destination",
                string.Empty,
                string.Empty,
                0f,
                0,
                -0.7f);
            AssertProjection(
                CharacterActionActivityObserver.Project(facts[1]),
                CharacterActivityOutcomes.Failed,
                "쇼핑 실패: 이동 정보 없음",
                "shopping:visit",
                "missing-movement-context",
                string.Empty,
                string.Empty,
                0f,
                0,
                -0.7f);
            AssertProjection(
                CharacterActionActivityObserver.Project(facts[2]),
                CharacterActivityOutcomes.Failed,
                "쇼핑 실패: 목적지 도달 실패",
                "shopping:visit",
                "destination-unreachable",
                string.Empty,
                string.Empty,
                0f,
                0,
                -0.7f);
            AssertProjection(
                CharacterActionActivityObserver.Project(facts[3]),
                CharacterActivityOutcomes.Damaged,
                "B107 Shopping Actor pocketed item from the floor.",
                "crime:floor-theft",
                "floor-theft",
                "stack:b107:floor-theft",
                "item",
                37.5f,
                1,
                -0.5f);

            var observed = new List<CharacterActivityEvent>();
            var alerts = new List<EventAlertRequest>();
            void CaptureActivity(CharacterLogEntry entry) =>
                observed.Add(entry.Activity);
            actor.LogComponent.OnLogAdded += CaptureActivity;
            using IDisposable alertSubscription =
                events.Subscribe<EventAlertRequestedEvent>(value =>
                {
                    if (value.request != null)
                    {
                        alerts.Add(value.request);
                    }
                });
            try
            {
                using var observer = new CharacterActionActivityObserver(events);
                observer.Initialize();
                observer.Initialize();
                for (int index = 0; index < facts.Length; index++)
                {
                    events.Publish(facts[index]);
                }
                Require(observed.Count == 4,
                    "Shopping observer duplicated or lost an activity.");
                RequireAlert(alerts, 1);

                observer.Dispose();
                events.Publish(facts[3]);
                Require(observed.Count == 4 && alerts.Count == 1,
                    "Disposed shopping observer still projected output.");

                observer.Initialize();
                events.Publish(facts[3]);
                Require(observed.Count == 5,
                    "Reinitialized shopping observer did not project once.");
                RequireAlert(alerts, 2);
            }
            finally
            {
                actor.LogComponent.OnLogAdded -= CaptureActivity;
            }

            Debug.Log("B107_SHOPPING_ACTION_OBSERVER=PASS");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    private static void AssertProjection(
        CharacterActivityEvent value,
        string outcome,
        string text,
        string action,
        string reason,
        string targetId,
        string targetName,
        float amount,
        int quantity,
        float sentiment)
    {
        Require(value != null
            && value.KindId == CharacterActivityKinds.Shopping
            && value.OutcomeId == outcome
            && value.FactText == text
            && value.ActionId == action
            && value.ReasonCode == reason
            && value.TargetId == targetId
            && value.TargetName == targetName
            && Approximately(value.Value, amount)
            && value.Quantity == quantity
            && Approximately(value.Sentiment, sentiment)
            && value.BubbleEligible,
            "Shopping activity projection changed: " + reason);
    }

    private static void RequireAlert(
        IReadOnlyList<EventAlertRequest> alerts,
        int expectedCount)
    {
        Require(alerts.Count == expectedCount,
            "Floor-theft alert count changed.");
        EventAlertRequest value = alerts[alerts.Count - 1];
        Require(value.Title == "바닥 물품 도난"
            && value.Detail ==
                "B107 Shopping Actor pocketed item from the floor."
            && value.Importance == EventAlertImportance.Medium
            && value.Category == "범죄",
            "Floor-theft alert payload changed.");
    }

    private static bool Approximately(float left, float right) =>
        Math.Abs(left - right) <= 0.0001f;

    private static void Require(bool condition, string failure)
    {
        if (!condition)
        {
            throw new InvalidOperationException(failure);
        }
    }
}
