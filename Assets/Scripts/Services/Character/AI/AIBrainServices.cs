using System;
using DungeonStory.Foundation;

public sealed class AIBrainDecisionServices
{
    public AIBrainDecisionServices(
        ICharacterAiActionAssetCatalog actionAssets,
        ICharacterAiSchedulingService scheduling,
        IFacilityCandidateCache facilityCandidates,
        ICharacterAiFacilityLookup facilities,
        ICharacterAiJobGiverCatalog jobGivers,
        ICharacterAiDecisionPipeline decisions,
        ICharacterAiPerformanceRecorder performance)
    {
        ActionAssets = actionAssets ?? throw new ArgumentNullException(nameof(actionAssets));
        Scheduling = scheduling ?? throw new ArgumentNullException(nameof(scheduling));
        FacilityCandidates = facilityCandidates
            ?? throw new ArgumentNullException(nameof(facilityCandidates));
        Facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        JobGivers = jobGivers ?? throw new ArgumentNullException(nameof(jobGivers));
        Decisions = decisions ?? throw new ArgumentNullException(nameof(decisions));
        Performance = performance ?? throw new ArgumentNullException(nameof(performance));
    }

    public ICharacterAiActionAssetCatalog ActionAssets { get; }
    public ICharacterAiSchedulingService Scheduling { get; }
    public IFacilityCandidateCache FacilityCandidates { get; }
    public ICharacterAiFacilityLookup Facilities { get; }
    public ICharacterAiJobGiverCatalog JobGivers { get; }
    public ICharacterAiDecisionPipeline Decisions { get; }
    public ICharacterAiPerformanceRecorder Performance { get; }
}

public sealed class AIBrainExecutionServices
{
    public AIBrainExecutionServices(
        IGridPathSearchBroker pathSearch,
        IGameClock clock,
        IRandomStreamProvider randomStreams,
        ISocialReputationBiasService reputationBias,
        IRoomFacilityPolicy roomFacilities)
    {
        PathSearch = pathSearch ?? throw new ArgumentNullException(nameof(pathSearch));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        ActionRandom = (randomStreams
            ?? throw new ArgumentNullException(nameof(randomStreams)))
            .Get("character-ai");
        FacilityScoring = new FacilityScoringContext(
            reputationBias,
            roomFacilities);
    }

    public IGridPathSearchBroker PathSearch { get; }
    public IGameClock Clock { get; }
    public IRandomStream ActionRandom { get; }
    public FacilityScoringContext FacilityScoring { get; }
}
