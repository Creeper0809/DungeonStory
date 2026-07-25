using System;

public interface IRegularCustomerRuntimeProvider
{
    bool TryGetRuntime(out RegularCustomerRuntime runtime);
}

public sealed class RegularCustomerRuntimeProvider :
    IRegularCustomerRuntimeProvider
{
    private readonly RegularCustomerRuntime runtime;

    public RegularCustomerRuntimeProvider(RegularCustomerRuntime runtime)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
    }

    public bool TryGetRuntime(out RegularCustomerRuntime resolvedRuntime)
    {
        resolvedRuntime = runtime;
        return true;
    }
}
