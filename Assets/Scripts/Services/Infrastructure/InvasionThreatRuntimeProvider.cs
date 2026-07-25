public interface IInvasionThreatRuntimeProvider
{
    bool TryGetRuntime(out InvasionThreatRuntime runtime);
}

public interface IInvasionDirectorRuntimeProvider
{
    bool TryGetRuntime(out InvasionDirectorRuntime runtime);
}

public interface IInvasionCombatReportRuntimeProvider
{
    bool TryGetRuntime(out InvasionCombatReportRuntime runtime);
}

public sealed class InvasionThreatRuntimeProvider :
    IInvasionThreatRuntimeProvider
{
    private readonly InvasionSceneRuntimeReferences runtimeReferences;

    public InvasionThreatRuntimeProvider(
        InvasionSceneRuntimeReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences;
    }

    public bool TryGetRuntime(out InvasionThreatRuntime resolvedRuntime)
    {
        resolvedRuntime = runtimeReferences.Threat;
        return resolvedRuntime != null;
    }
}


public sealed class InvasionDirectorRuntimeProvider :
    IInvasionDirectorRuntimeProvider
{
    private readonly InvasionSceneRuntimeReferences runtimeReferences;

    public InvasionDirectorRuntimeProvider(
        InvasionSceneRuntimeReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences;
    }

    public bool TryGetRuntime(out InvasionDirectorRuntime resolvedRuntime)
    {
        resolvedRuntime = runtimeReferences.Director;
        return resolvedRuntime != null;
    }
}

public sealed class InvasionCombatReportRuntimeProvider :
    IInvasionCombatReportRuntimeProvider
{
    private readonly InvasionSceneRuntimeReferences runtimeReferences;

    public InvasionCombatReportRuntimeProvider(
        InvasionSceneRuntimeReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences;
    }

    public bool TryGetRuntime(out InvasionCombatReportRuntime resolvedRuntime)
    {
        resolvedRuntime = runtimeReferences.CombatReport;
        return resolvedRuntime != null;
    }
}
