using System;

public sealed class ShopServiceCompletion
{
    private readonly Action<IBuildingVisitorPort, bool> finishShopUse;

    public ShopServiceCompletion(Action<IBuildingVisitorPort, bool> finishShopUse)
    {
        this.finishShopUse = finishShopUse
            ?? throw new ArgumentNullException(nameof(finishShopUse));
    }

    public void Finish(
        IBuildingVisitorPort actor,
        string sessionId,
        bool completed,
        string failureReason,
        IShopServiceSessionCompletionPort serviceSessionCompletion)
    {
        if (sessionId != null && serviceSessionCompletion != null)
        {
            if (completed)
            {
                if (!serviceSessionCompletion.TryCompleteSession(
                        sessionId,
                        out string completionFailureCode))
                {
                    serviceSessionCompletion.CancelSession(
                        sessionId,
                        completionFailureCode);
                }
            }
            else
            {
                serviceSessionCompletion.CancelSession(
                    sessionId,
                    failureReason);
            }
        }

        finishShopUse(actor, completed);
    }
}
