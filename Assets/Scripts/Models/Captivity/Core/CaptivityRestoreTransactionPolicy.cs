using System;
using DungeonStory.Foundation;

public static class CaptivityRestoreTransactionPolicy
{
    public static void RequireStageBoundary(
        bool restoreTransactionActive,
        bool restoreCandidatePrepared,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        if (!restoreTransactionActive
            || aggregateRootStore?.IsRestoreStaging != true)
        {
            throw new InvalidOperationException(
                "Captivity restore requires the V18 save registry transaction boundary.");
        }
        if (restoreCandidatePrepared)
        {
            throw new InvalidOperationException(
                "A captivity restore candidate was staged more than once.");
        }
    }

    public static void RequireBeginBoundary(bool restoreTransactionActive)
    {
        if (restoreTransactionActive)
        {
            throw new InvalidOperationException(
                "A captivity restore candidate is already active.");
        }
    }

    public static void RequirePublishBoundary(
        bool restoreTransactionActive,
        bool restoreCandidatePrepared)
    {
        if (!restoreTransactionActive || !restoreCandidatePrepared)
        {
            throw new InvalidOperationException(
                "No captivity restore candidate is ready to publish.");
        }
    }
}
