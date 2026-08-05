using System;

public sealed class ShopServiceCompletion
{
    private readonly Action<IBuildingVisitorPort> endShopUse;

    public ShopServiceCompletion(Action<IBuildingVisitorPort> endShopUse)
    {
        this.endShopUse = endShopUse
            ?? throw new ArgumentNullException(nameof(endShopUse));
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

        endShopUse(actor);
    }
}
