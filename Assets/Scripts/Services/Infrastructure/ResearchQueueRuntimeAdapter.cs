using System;

public sealed class ResearchQueueRuntimeAdapter : IResearchQueueRuntimePort
{
    private readonly BlueprintResearchRuntime runtime;

    public ResearchQueueRuntimeAdapter(
        ProgressionSceneRuntimeReferences runtimeReferences)
    {
        runtime = (runtimeReferences
                ?? throw new ArgumentNullException(nameof(runtimeReferences)))
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(ResearchQueueRuntimeAdapter)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
    }

    public ResearchQueueCommandResult EnqueueProject(ResearchProjectId projectId) =>
        runtime.EnqueueProject(projectId);

    public ResearchQueueCommandResult RemoveProject(ResearchProjectId projectId) =>
        runtime.RemoveProject(projectId);

    public ResearchQueueCommandResult MoveProject(int fromIndex, int toIndex) =>
        runtime.MoveProject(fromIndex, toIndex);
}
