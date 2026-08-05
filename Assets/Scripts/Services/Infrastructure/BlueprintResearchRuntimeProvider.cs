using System;
using VContainer;

public interface IBlueprintResearchWorkService
{
    bool HasResearchWorkFor(BuildableObject facility);
    BlueprintResearchWorkResult ApplyResearchWork(
        CharacterActor researcher,
        BuildableObject researchFacility,
        float seconds);
}

public interface IBlueprintResearchStateService
{
    BlueprintResearchState GetState();
}

public sealed class BlueprintResearchWorkService : IBlueprintResearchWorkService
{
    private readonly BlueprintResearchRuntime runtime;
    private readonly IMetaProgressionRuntimeReader metaProgressionReader;
    private readonly IKnowledgeResidueProcessingRuntime knowledgeProcessing;

    [Inject]
    public BlueprintResearchWorkService(
        ProgressionSceneRuntimeReferences runtimeReferences,
        IMetaProgressionRuntimeReader metaProgressionReader,
        IKnowledgeResidueProcessingRuntime knowledgeProcessing)
    {
        runtime = (runtimeReferences
                ?? throw new ArgumentNullException(nameof(runtimeReferences)))
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(BlueprintResearchWorkService)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        this.metaProgressionReader = metaProgressionReader
            ?? throw new ArgumentNullException(nameof(metaProgressionReader));
        this.knowledgeProcessing = knowledgeProcessing
            ?? throw new ArgumentNullException(nameof(knowledgeProcessing));
    }

    public bool HasResearchWorkFor(BuildableObject facility)
    {
        if (facility == null
            || !facility.SupportsWork(BuiltInWorkTypeIds.Research))
        {
            return false;
        }

        return runtime.HasActiveResearch
            || knowledgeProcessing?.HasProcessingWorkFor(facility) == true;
    }

    public BlueprintResearchWorkResult ApplyResearchWork(
        CharacterActor researcher,
        BuildableObject researchFacility,
        float seconds)
    {
        float multiplier = metaProgressionReader.GetArcaneResearchWorkMultiplier();
        if (!runtime.HasActiveResearch
            && knowledgeProcessing.HasProcessingWorkFor(researchFacility))
        {
            return knowledgeProcessing.ApplyWork(
                researcher,
                researchFacility,
                seconds * Math.Max(0.05f, multiplier));
        }

        return runtime.ApplyResearchWork(researcher, researchFacility, seconds * Math.Max(0.05f, multiplier));
    }
}

public sealed class BuildingResearchWorkPortAdapter : IBuildingResearchWorkPort
{
    private readonly IBlueprintResearchWorkService researchWork;

    public BuildingResearchWorkPortAdapter(
        IBlueprintResearchWorkService researchWork)
    {
        this.researchWork = researchWork
            ?? throw new ArgumentNullException(nameof(researchWork));
    }

    public bool HasResearchWorkFor(IBuildingWorldEntryPort facility)
    {
        if (facility == null)
        {
            return false;
        }

        return researchWork.HasResearchWorkFor(
            facility as BuildableObject
            ?? throw new ArgumentException(
                $"{nameof(IBuildingResearchWorkPort)} only accepts {nameof(BuildableObject)} facilities.",
                nameof(facility)));
    }
}

public sealed class BlueprintResearchStateService : IBlueprintResearchStateService
{
    private readonly BlueprintResearchRuntime runtime;

    public BlueprintResearchStateService(
        ProgressionSceneRuntimeReferences runtimeReferences)
    {
        runtime = (runtimeReferences
                ?? throw new ArgumentNullException(nameof(runtimeReferences)))
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(BlueprintResearchStateService)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
    }

    public BlueprintResearchState GetState()
    {
        return runtime.State;
    }
}
