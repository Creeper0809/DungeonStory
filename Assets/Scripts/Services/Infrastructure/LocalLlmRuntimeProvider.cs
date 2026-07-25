using System;

public interface ILocalLlmRuntimeProvider
{
    bool TryGetRuntime(out ILocalLlmRuntime runtime);
    ILocalLlmRuntime GetRequiredRuntime();
}

public sealed class LocalLlmRuntimeProvider :
    ILocalLlmRuntimeProvider
{
    private readonly CharacterSceneRuntimeReferences runtimeReferences;

    public LocalLlmRuntimeProvider(
        CharacterSceneRuntimeReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
    }

    public bool TryGetRuntime(out ILocalLlmRuntime resolvedRuntime)
    {
        LocalLlmRequestQueue queue = runtimeReferences.LocalLlm;
        resolvedRuntime = queue;
        return queue != null;
    }

    public ILocalLlmRuntime GetRequiredRuntime()
    {
        if (TryGetRuntime(out ILocalLlmRuntime resolvedRuntime))
        {
            return resolvedRuntime;
        }

        throw new InvalidOperationException($"{nameof(LocalLlmRuntimeProvider)} requires a loaded {nameof(LocalLlmRequestQueue)}.");
    }
}

public sealed class PreparationLocalLlmRuntimeProvider :
    ILocalLlmRuntimeProvider
{
    private readonly LocalLlmRequestQueue requestQueue;

    public PreparationLocalLlmRuntimeProvider(
        LocalLlmRequestQueue requestQueue)
    {
        this.requestQueue = requestQueue
            ?? throw new ArgumentNullException(nameof(requestQueue));
    }

    public bool TryGetRuntime(out ILocalLlmRuntime runtime)
    {
        runtime = requestQueue;
        return true;
    }

    public ILocalLlmRuntime GetRequiredRuntime()
    {
        return requestQueue;
    }
}
