using UnityEngine;

public enum CustomerCheckoutWaitStage
{
    Waiting = 0,
    Restless = 1,
    RequestingService = 2,
    Abandoned = 3
}

public readonly struct CustomerCheckoutPatienceProfile
{
    public CustomerCheckoutPatienceProfile(
        float patienceMultiplier,
        int queuePosition,
        float restlessSeconds,
        float requestServiceSeconds,
        float abandonSeconds,
        float abandonMoodPenalty,
        float abandonSentiment)
    {
        PatienceMultiplier = patienceMultiplier;
        QueuePosition = queuePosition;
        RestlessSeconds = restlessSeconds;
        RequestServiceSeconds = requestServiceSeconds;
        AbandonSeconds = abandonSeconds;
        AbandonMoodPenalty = abandonMoodPenalty;
        AbandonSentiment = abandonSentiment;
    }

    public float PatienceMultiplier { get; }
    public int QueuePosition { get; }
    public float RestlessSeconds { get; }
    public float RequestServiceSeconds { get; }
    public float AbandonSeconds { get; }
    public float AbandonMoodPenalty { get; }
    public float AbandonSentiment { get; }
}

public static class CustomerCheckoutPatienceRules
{
    private const float BaseAbandonSeconds = 12f;
    private const float MinimumAbandonSeconds = 3f;
    private const float MaximumAbandonSeconds = 30f;

    public static CustomerCheckoutPatienceProfile Create(CharacterActor actor, int queuePosition)
    {
        CharacterAiPersonality personality = actor != null
            && actor.Identity != null
            && actor.Identity.Data != null
                ? actor.Identity.Data.aiPersonality
                : null;
        float personalityPatience = personality != null ? personality.patience : 1f;
        float modelPatience = actor != null && actor.Identity != null && actor.Identity.Profile != null
            ? actor.Identity.Profile.GetWaitPatienceMultiplier()
            : 1f;
        return Create(personalityPatience, modelPatience, queuePosition);
    }

    public static CustomerCheckoutPatienceProfile Create(
        float personalityPatience,
        float modelPatience,
        int queuePosition)
    {
        float patience = Mathf.Clamp(
            Mathf.Max(0.05f, personalityPatience) * Mathf.Max(0.05f, modelPatience),
            0.25f,
            3f);
        int normalizedPosition = Mathf.Max(1, queuePosition);
        float visibleQueueTolerance = 1f / (1f + ((normalizedPosition - 1) * 0.08f));
        float abandonSeconds = Mathf.Clamp(
            BaseAbandonSeconds * patience * visibleQueueTolerance,
            MinimumAbandonSeconds,
            MaximumAbandonSeconds);
        float patience01 = Mathf.InverseLerp(0.25f, 3f, patience);

        return new CustomerCheckoutPatienceProfile(
            patience,
            normalizedPosition,
            abandonSeconds * 0.35f,
            abandonSeconds * 0.62f,
            abandonSeconds,
            -Mathf.Lerp(7f, 3f, patience01),
            -Mathf.Lerp(0.8f, 0.35f, patience01));
    }

    public static CustomerCheckoutWaitStage GetStage(
        CustomerCheckoutPatienceProfile profile,
        float elapsedSeconds)
    {
        float elapsed = Mathf.Max(0f, elapsedSeconds);
        if (elapsed >= profile.AbandonSeconds)
        {
            return CustomerCheckoutWaitStage.Abandoned;
        }

        if (elapsed >= profile.RequestServiceSeconds)
        {
            return CustomerCheckoutWaitStage.RequestingService;
        }

        if (elapsed >= profile.RestlessSeconds)
        {
            return CustomerCheckoutWaitStage.Restless;
        }

        return CustomerCheckoutWaitStage.Waiting;
    }

    public static string GetQueueDetail(
        CustomerCheckoutPatienceProfile profile,
        float elapsedSeconds,
        CustomerCheckoutWaitStage stage)
    {
        string reaction = stage switch
        {
            CustomerCheckoutWaitStage.Restless => "초조해함",
            CustomerCheckoutWaitStage.RequestingService => "직원을 찾는 중",
            CustomerCheckoutWaitStage.Abandoned => "구매를 포기함",
            _ => "차례를 기다리는 중"
        };
        return $"{profile.QueuePosition}번째 · {Mathf.CeilToInt(elapsedSeconds)}초 · {reaction}";
    }
}
