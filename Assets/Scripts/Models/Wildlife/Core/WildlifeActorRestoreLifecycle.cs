using System;

public interface IWildlifeActorRestoreHost
{
    bool IsInitialized { get; }
    void Register();
    void Unregister();
    void Discard();
}

public sealed class WildlifeActorRestoreLifecycle
{
    private readonly IWildlifeActorRestoreHost host;
    private bool detached;
    private bool publicationPending;

    public WildlifeActorRestoreLifecycle(IWildlifeActorRestoreHost host)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public bool IsDetached => detached;
    public bool IsPublicationPending => publicationPending;

    public void Prepare()
    {
        if (host.IsInitialized)
        {
            throw new InvalidOperationException(
                "Detached wildlife restore mode must be selected before initialization.");
        }

        detached = true;
    }

    public void Publish()
    {
        RequireDetached("published");
        try
        {
            host.Register();
            detached = false;
            publicationPending = true;
        }
        catch
        {
            try
            {
                host.Unregister();
            }
            catch
            {
                // Preserve the original publication failure.
            }
            throw;
        }
    }

    public void ValidatePublication()
    {
        RequirePendingPublication();
    }

    public void RollbackPublication()
    {
        RequirePendingPublication();
        host.Unregister();
        publicationPending = false;
        detached = true;
    }

    public void CompletePublication()
    {
        RequirePendingPublication();
        publicationPending = false;
    }

    public void Discard()
    {
        RequireDetached("discarded");
        detached = false;
        host.Discard();
    }

    private void RequireDetached(string action)
    {
        if (!detached)
        {
            throw new InvalidOperationException(
                $"Only a detached wildlife restore candidate can be {action}.");
        }
    }

    private void RequirePendingPublication()
    {
        if (detached || !publicationPending)
        {
            throw new InvalidOperationException(
                "Only a reversibly published wildlife restore candidate can use this operation.");
        }
    }
}
