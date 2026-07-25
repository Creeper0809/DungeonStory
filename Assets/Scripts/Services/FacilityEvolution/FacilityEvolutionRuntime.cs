using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public interface IFacilityEvolutionRecipeProvider
{
    IReadOnlyList<FacilityEvolutionRecipeSO> GetRecipes();
}

public interface IFacilityEvolutionBuildingReplacer
{
    bool CanReplace(BuildableObject source, BuildingSO resultBuilding, out string reason);
    bool TryReplace(BuildableObject source, BuildingSO resultBuilding, out BuildableObject result, out string reason);
}

public sealed class GridFacilityEvolutionBuildingReplacer : IFacilityEvolutionBuildingReplacer
{
    private readonly GridBuildingFactory buildingFactory;

    public GridFacilityEvolutionBuildingReplacer(GridBuildingFactory buildingFactory)
    {
        this.buildingFactory = buildingFactory
            ?? throw new ArgumentNullException(nameof(buildingFactory));
    }

    public bool CanReplace(BuildableObject source, BuildingSO resultBuilding, out string reason)
    {
        if (source == null || source.isDestroy)
        {
            reason = "대상 시설 없음";
            return false;
        }

        if (source.Grid == null)
        {
            reason = "그리드 없음";
            return false;
        }

        if (resultBuilding == null)
        {
            reason = "결과 시설 없음";
            return false;
        }

        foreach (Vector2Int pos in resultBuilding.GetGridPosList(source.centerPos))
        {
            if (!source.Grid.IsValidGridPos(pos))
            {
                reason = "결과 시설 위치가 그리드 밖입니다";
                return false;
            }

            GridCell cell = source.Grid.GetGridCell(pos);
            IGridOccupant occupant = cell?.GetOccupant(resultBuilding.Placement.Layer);
            if (occupant != null && !ReferenceEquals(occupant, source))
            {
                reason = "결과 시설을 배치할 공간이 부족합니다";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    public bool TryReplace(BuildableObject source, BuildingSO resultBuilding, out BuildableObject result, out string reason)
    {
        result = null;
        if (!CanReplace(source, resultBuilding, out reason))
        {
            return false;
        }

        Grid grid = source.Grid;
        Vector2Int position = source.centerPos;
        BuildingSO sourceBuilding = source.BuildingData;

        grid.RemoveOccupant(
            source,
            sourceBuilding.Placement.Layer,
            source.buildPoses,
            sourceBuilding.Placement.IsMovement);
        buildingFactory.DeleteVisual(sourceBuilding, position);

        result = buildingFactory.Create(grid, resultBuilding, position);
        if (result == null)
        {
            source.DestroySelf();
            reason = "결과 시설 생성 실패";
            return false;
        }

        result.SetGrid(grid);
        result.Initialization(resultBuilding, position);
        bool registered = grid.RegisterOccupant(
            result,
            resultBuilding.Placement.Layer,
            resultBuilding.GetGridPosList(position),
            resultBuilding.Placement.IsMovement);
        if (!registered)
        {
            result.DestroySelf();
            source.DestroySelf();
            reason = "결과 시설 배치 실패";
            return false;
        }

        source.DestroySelf();
        reason = string.Empty;
        return true;
    }
}

public readonly struct FacilityEvolutionResult
{
    public FacilityEvolutionResult(
        bool success,
        FacilityEvolutionRecipeSO recipe,
        BuildableObject resultBuilding,
        int resultStarGrade,
        string sourceFacilityName,
        FacilityEvolutionProposal proposal,
        string message,
        IReadOnlyList<string> mutationTags = null)
    {
        Success = success;
        Recipe = recipe;
        ResultBuilding = resultBuilding;
        ResultStarGrade = Mathf.Max(1, resultStarGrade);
        SourceFacilityName = sourceFacilityName ?? string.Empty;
        Proposal = proposal;
        Message = message ?? string.Empty;
        MutationTags = EventPayloadSnapshot.Copy(mutationTags);
    }

    public bool Success { get; }
    public FacilityEvolutionRecipeSO Recipe { get; }
    public BuildableObject ResultBuilding { get; }
    public int ResultStarGrade { get; }
    public string SourceFacilityName { get; }
    public FacilityEvolutionProposal Proposal { get; }
    public string Message { get; }
    public IReadOnlyList<string> MutationTags { get; }
}

public struct FacilityEvolutionCompletedEvent
{
    public FacilityEvolutionResult result;

    public FacilityEvolutionCompletedEvent(FacilityEvolutionResult result)
    {
        this.result = result;
    }
}

public interface IFacilityEvolutionValidator
{
    FacilityEvolutionValidationResult Validate(
        FacilityEvolutionContext context,
        FacilityEvolutionRecipeSO recipe,
        BlueprintResearchState researchState,
        IFacilityEvolutionResourceProvider resources,
        IFacilityEvolutionBuildingReplacer buildingReplacer);
}

public sealed class DefaultFacilityEvolutionValidator : IFacilityEvolutionValidator
{
    private readonly IFacilityEvolutionRecipeQuery recipeQuery;
    private readonly IFacilityEvolutionStateComponentFactory stateComponentFactory;

    public DefaultFacilityEvolutionValidator(
        IFacilityEvolutionRecipeQuery recipeQuery,
        IFacilityEvolutionStateComponentFactory stateComponentFactory)
    {
        this.recipeQuery = recipeQuery
            ?? throw new ArgumentNullException(nameof(recipeQuery));
        this.stateComponentFactory = stateComponentFactory
            ?? throw new ArgumentNullException(nameof(stateComponentFactory));
    }

    public FacilityEvolutionValidationResult Validate(
        FacilityEvolutionContext context,
        FacilityEvolutionRecipeSO recipe,
        BlueprintResearchState researchState,
        IFacilityEvolutionResourceProvider resources,
        IFacilityEvolutionBuildingReplacer buildingReplacer)
    {
        BuildableObject facility = context != null ? context.Facility : null;
        RoomProfile profile = context != null ? context.Profile : null;
        FacilityEvolutionValidationResult validation = FacilityEvolutionService.Validate(
            facility,
            recipe,
            profile,
            researchState,
            resources ?? throw new ArgumentNullException(nameof(resources)),
            recipeQuery,
            stateComponentFactory);

        if (recipe != null
            && recipe.resultBuilding != null
            && buildingReplacer != null
            && !buildingReplacer.CanReplace(facility, recipe.resultBuilding, out string placementReason))
        {
            validation.AddCheck("배치", "결과 시설 배치", false, placementReason);
        }
        else if (recipe != null && recipe.resultBuilding != null)
        {
            validation.AddCheck("배치", "결과 시설 배치", true);
        }

        return validation;
    }
}

public interface IFacilityEvolutionCandidateBuilder
{
    FacilityEvolutionCandidate Build(
        FacilityEvolutionContext context,
        FacilityEvolutionRecipeSO recipe,
        FacilityEvolutionProposal proposal,
        IReadOnlyDictionary<string, int> proposalOrder,
        BlueprintResearchState researchState,
        IFacilityEvolutionResourceProvider resources,
        IFacilityEvolutionBuildingReplacer buildingReplacer);
}

public sealed class DefaultFacilityEvolutionCandidateBuilder : IFacilityEvolutionCandidateBuilder
{
    private readonly IFacilityEvolutionValidator validator;

    public DefaultFacilityEvolutionCandidateBuilder(IFacilityEvolutionValidator validator)
    {
        this.validator = validator
            ?? throw new ArgumentNullException(nameof(validator));
    }

    public FacilityEvolutionCandidate Build(
        FacilityEvolutionContext context,
        FacilityEvolutionRecipeSO recipe,
        FacilityEvolutionProposal proposal,
        IReadOnlyDictionary<string, int> proposalOrder,
        BlueprintResearchState researchState,
        IFacilityEvolutionResourceProvider resources,
        IFacilityEvolutionBuildingReplacer buildingReplacer)
    {
        FacilityEvolutionValidationResult validation =
            validator.Validate(context, recipe, researchState, resources, buildingReplacer);
        string id = recipe != null ? recipe.EffectiveId : string.Empty;
        bool proposed = !string.IsNullOrWhiteSpace(id)
            && proposalOrder != null
            && proposalOrder.ContainsKey(id);

        string reason = validation.ToMessage();
        if (!string.IsNullOrWhiteSpace(id)
            && proposal.ProposalReasons != null
            && proposal.ProposalReasons.TryGetValue(id, out string proposalReason)
            && !string.IsNullOrWhiteSpace(proposalReason))
        {
            reason = proposalReason;
        }

        string rejectedHint = string.Empty;
        if (!string.IsNullOrWhiteSpace(id)
            && proposal.RejectedHintTexts != null
            && proposal.RejectedHintTexts.TryGetValue(id, out string hint))
        {
            rejectedHint = hint;
        }

        return new FacilityEvolutionCandidate(
            recipe,
            validation,
            reason,
            proposed,
            proposal.FlavorText,
            proposal.Source,
            proposal.StatusMessage,
            FacilityIdentityPressureUtility.ScoreRecipe(context != null ? context.Profile : null, recipe),
            rejectedHint);
    }
}

public sealed class FacilityEvolutionEngine
{
    private readonly IFacilityEvolutionRecipeQuery recipeQuery;
    private readonly IRoomProfileProvider roomProfileProvider;
    private readonly IFacilityEvolutionRecordProvider recordProvider;
    private readonly IFacilityEvolutionProposalProvider proposalProvider;
    private readonly IFacilityEvolutionResourceProvider resourceProvider;
    private readonly IFacilityEvolutionBuildingReplacer buildingReplacer;
    private readonly IRoomLayoutCache roomLayoutCache;
    private readonly IFacilityEvolutionStateComponentFactory stateComponentFactory;
    private readonly IFacilityCandidateCache facilityCandidateCache;
    private readonly IFacilityEvolutionValidator validator;
    private readonly IFacilityEvolutionCandidateBuilder candidateBuilder;
    private readonly IFacilityEvolutionRecordTokenConsumer recordTokenConsumer;
    private readonly IFacilityEvolutionRecordComponentService recordComponentService;
    private readonly IFacilityEvolutionMutationResolver mutationResolver;
    private readonly Func<BlueprintResearchState> researchStateProvider;

    public FacilityEvolutionEngine(
        IFacilityEvolutionRecipeQuery recipeQuery,
        IRoomProfileProvider roomProfileProvider,
        IFacilityEvolutionRecordProvider recordProvider,
        IFacilityEvolutionProposalProvider proposalProvider,
        IFacilityEvolutionResourceProvider resourceProvider,
        IFacilityEvolutionBuildingReplacer buildingReplacer,
        IRoomLayoutCache roomLayoutCache,
        IFacilityEvolutionStateComponentFactory stateComponentFactory,
        IFacilityCandidateCache facilityCandidateCache,
        Func<BlueprintResearchState> researchStateProvider,
        IFacilityEvolutionValidator validator = null,
        IFacilityEvolutionCandidateBuilder candidateBuilder = null,
        IFacilityEvolutionRecordTokenConsumer recordTokenConsumer = null,
        IFacilityEvolutionRecordComponentService recordComponentService = null,
        IFacilityEvolutionMutationResolver mutationResolver = null)
    {
        this.recipeQuery = recipeQuery
            ?? throw new ArgumentNullException(nameof(recipeQuery));
        this.recordComponentService = recordComponentService
            ?? recordProvider as IFacilityEvolutionRecordComponentService
            ?? throw new ArgumentNullException(nameof(recordComponentService));
        this.recordProvider = recordProvider ?? this.recordComponentService;
        this.proposalProvider = proposalProvider ?? new RuleBasedFacilityEvolutionProposalProvider();
        this.resourceProvider = resourceProvider
            ?? throw new ArgumentNullException(nameof(resourceProvider));
        this.buildingReplacer = buildingReplacer
            ?? throw new ArgumentNullException(nameof(buildingReplacer));
        this.roomLayoutCache = roomLayoutCache
            ?? throw new ArgumentNullException(nameof(roomLayoutCache));
        this.stateComponentFactory = stateComponentFactory
            ?? throw new ArgumentNullException(nameof(stateComponentFactory));
        this.facilityCandidateCache = facilityCandidateCache
            ?? throw new ArgumentNullException(nameof(facilityCandidateCache));
        this.roomProfileProvider = roomProfileProvider
            ?? new RoomProfileBuilder(this.recordProvider, this.roomLayoutCache);
        this.validator = validator ?? new DefaultFacilityEvolutionValidator(this.recipeQuery, this.stateComponentFactory);
        this.candidateBuilder = candidateBuilder ?? new DefaultFacilityEvolutionCandidateBuilder(this.validator);
        this.recordTokenConsumer = recordTokenConsumer
            ?? throw new ArgumentNullException(nameof(recordTokenConsumer));
        this.mutationResolver = mutationResolver ?? new DefaultFacilityEvolutionMutationResolver();
        this.researchStateProvider = researchStateProvider;
    }

    public BlueprintResearchState ResearchState => researchStateProvider?.Invoke();

    public IReadOnlyList<FacilityEvolutionRecipeSO> VisibleRecipes =>
        recipeQuery.GetVisibleRecipes(ResearchState);

    public FacilityEvolutionContext BuildContext(BuildableObject facility)
    {
        FacilityEvolutionStateComponent state = stateComponentFactory.GetOrAdd(facility);
        RoomProfile profile = roomProfileProvider.Build(facility);
        IReadOnlyList<FacilityEvolutionRecipeSO> sourceCandidates =
            recipeQuery.GetSourceCandidates(facility, ResearchState);
        return new FacilityEvolutionContext(facility, state, profile, sourceCandidates);
    }

    public IReadOnlyList<FacilityEvolutionCandidate> GetCandidates(
        BuildableObject facility,
        bool includeRejected = false,
        bool requestLlmProposal = true)
    {
        if (facility == null || facility.isDestroy)
        {
            return Array.Empty<FacilityEvolutionCandidate>();
        }

        FacilityEvolutionContext context = BuildContext(facility);
        FacilityEvolutionProposal proposal = requestLlmProposal
            ? proposalProvider.Propose(context)
            : new RuleBasedFacilityEvolutionProposalProvider().Propose(context);
        IReadOnlyDictionary<string, int> proposalOrder = BuildProposalOrder(proposal);

        return context.CandidateRecipes
            .OrderBy((recipe) => proposalOrder.TryGetValue(recipe.EffectiveId, out int index) ? index : int.MaxValue)
            .ThenBy((recipe) => recipe.id)
            .Select((recipe) => candidateBuilder.Build(
                context,
                recipe,
                proposal,
                proposalOrder,
                ResearchState,
                resourceProvider,
                buildingReplacer))
            .Where((candidate) => includeRejected || candidate.Approved)
            .ToList();
    }

    public bool TryEvolve(
        BuildableObject facility,
        FacilityEvolutionRecipeSO recipe,
        out FacilityEvolutionResult result)
    {
        result = new FacilityEvolutionResult(
            false,
            recipe,
            null,
            recipe != null ? recipe.resultStarGrade : 1,
            FacilityShopService.GetBuildingName(facility != null ? facility.BuildingData : null),
            default,
            "진화할 수 없습니다");

        if (facility == null || facility.isDestroy)
        {
            result = Fail(recipe, null, default, "대상 시설이 없습니다");
            return false;
        }

        FacilityEvolutionContext context = BuildContext(facility);
        FacilityEvolutionProposal proposal = proposalProvider.Propose(context);
        FacilityEvolutionValidationResult validation =
            validator.Validate(context, recipe, ResearchState, resourceProvider, buildingReplacer);

        if (!validation.Approved)
        {
            result = Fail(recipe, facility, proposal, validation.ToMessage());
            return false;
        }

        string sourceFacilityName = FacilityShopService.GetBuildingName(facility.BuildingData);
        FacilityEvolutionStateSnapshot stateSnapshot =
            stateComponentFactory.GetOrAdd(facility)?.CreateSnapshot();
        FacilityEvolutionRecord recordSnapshot = recordProvider.GetRecord(facility).Clone();
        FacilityEvolutionMutationResult mutationResult =
            mutationResolver.Resolve(context, recipe, proposal);
        if (!recordTokenConsumer.TryConsume(
                recordSnapshot,
                recipe.requiredRecordTokens,
                recipe.consumeRecordTokens,
                out string consumeReason))
        {
            result = Fail(recipe, facility, proposal, $"기록 소모 실패 {consumeReason}");
            return false;
        }

        if (!ConsumeMaterials(recipe.requiredMaterials, out string materialReason))
        {
            result = Fail(recipe, facility, proposal, materialReason);
            return false;
        }

        if (!buildingReplacer.TryReplace(facility, recipe.resultBuilding, out BuildableObject resultBuilding, out string replaceReason))
        {
            result = Fail(recipe, null, proposal, replaceReason);
            return false;
        }

        resultBuilding.SetFacilityLevel(recipe.resultStarGrade);
        FacilityEvolutionStateComponent nextState = stateComponentFactory.GetOrAdd(resultBuilding);
        nextState.ApplySnapshot(stateSnapshot);
        nextState.ApplyEvolution(
            null,
            resultBuilding,
            recipe,
            proposal,
            sourceFacilityName,
            context.Profile,
            mutationResult.Tags);
        recordComponentService.ReplaceWith(resultBuilding, recordSnapshot);
        facilityCandidateCache.MarkDynamicStateDirty();
        roomLayoutCache.Clear();

        result = new FacilityEvolutionResult(
            true,
            recipe,
            resultBuilding,
            recipe.resultStarGrade,
            sourceFacilityName,
            proposal,
            $"{recipe.DisplayName} 진화 완료",
            mutationResult.Tags);
        return true;
    }

    private static IReadOnlyDictionary<string, int> BuildProposalOrder(FacilityEvolutionProposal proposal)
    {
        Dictionary<string, int> result = new Dictionary<string, int>();
        if (proposal.ProposalIds == null)
        {
            return result;
        }

        for (int i = 0; i < proposal.ProposalIds.Count; i++)
        {
            string id = proposal.ProposalIds[i];
            if (!string.IsNullOrWhiteSpace(id) && !result.ContainsKey(id))
            {
                result.Add(id, i);
            }
        }

        return result;
    }

    private bool ConsumeMaterials(FacilityEvolutionMaterialRequirement[] requirements, out string reason)
    {
        reason = string.Empty;
        if (requirements == null)
        {
            return true;
        }

        foreach (FacilityEvolutionMaterialRequirement requirement in requirements)
        {
            int amount = Mathf.Max(1, requirement.amount);
            if (!resourceProvider.ConsumeMaterial(requirement.materialId, amount))
            {
                reason = $"재료 소모 실패 {requirement.materialId} x{amount}";
                return false;
            }
        }

        return true;
    }

    private static FacilityEvolutionResult Fail(
        FacilityEvolutionRecipeSO recipe,
        BuildableObject facility,
        FacilityEvolutionProposal proposal,
        string message)
    {
        return new FacilityEvolutionResult(
            false,
            recipe,
            null,
            recipe != null ? recipe.resultStarGrade : 1,
            FacilityShopService.GetBuildingName(facility != null ? facility.BuildingData : null),
            proposal,
            message);
    }
}

public class FacilityEvolutionRuntime : MonoBehaviour
{
    [SerializeField] private bool raiseAlertOnEvolution = true;
    [SerializeField] private bool enableLlmProposals = true;

    private IFacilityEvolutionRecipeQuery recipeQuery;
    private IRoomProfileProvider roomProfileProvider;
    private IFacilityEvolutionRecordProvider recordProvider;
    private IFacilityEvolutionProposalProvider proposalProvider;
    private IFacilityEvolutionResourceProvider resourceProvider;
    private IFacilityEvolutionBuildingReplacer buildingReplacer;
    private IFacilityEvolutionValidator validator;
    private IFacilityEvolutionCandidateBuilder candidateBuilder;
    private IFacilityEvolutionRecordTokenConsumer recordTokenConsumer;
    private IFacilityEvolutionRecordComponentService recordComponentService;
    private IFacilityEvolutionMutationResolver mutationResolver;
    private IBlueprintResearchStateService blueprintResearchStateService;
    private IRoomLayoutCache roomLayoutCache;
    private IFacilityEvolutionStateComponentFactory stateComponentFactory;
    private IFacilityCandidateCache facilityCandidateCache;
    private ILocalLlmRuntimeProvider llmRuntimeProvider;
    private IFacilityEvolutionBuildingReplacerFactory buildingReplacerFactory;
    private IGameEventBus gameEventBus;
    private FacilityEvolutionEngine engine;

    public event Action<FacilityEvolutionResult> Completed;

    [Inject]
    public void ConstructFacilityEvolutionRuntime(
        IBlueprintResearchStateService blueprintResearchStateService,
        ILocalLlmRuntimeProvider llmRuntimeProvider,
        IRoomLayoutCache roomLayoutCache,
        IFacilityEvolutionStateComponentFactory stateComponentFactory,
        IFacilityCandidateCache facilityCandidateCache,
        IFacilityEvolutionRecipeQuery recipeQuery,
        IFacilityEvolutionResourceProvider resourceProvider,
        IFacilityEvolutionRecordTokenConsumer recordTokenConsumer,
        IFacilityEvolutionRecordComponentService recordComponentService,
        IFacilityEvolutionBuildingReplacerFactory buildingReplacerFactory,
        IGameEventBus gameEventBus)
    {
        this.blueprintResearchStateService = blueprintResearchStateService
            ?? throw new ArgumentNullException(nameof(blueprintResearchStateService));
        this.llmRuntimeProvider = llmRuntimeProvider
            ?? throw new ArgumentNullException(nameof(llmRuntimeProvider));
        this.roomLayoutCache = roomLayoutCache
            ?? throw new ArgumentNullException(nameof(roomLayoutCache));
        this.stateComponentFactory = stateComponentFactory
            ?? throw new ArgumentNullException(nameof(stateComponentFactory));
        this.facilityCandidateCache = facilityCandidateCache
            ?? throw new ArgumentNullException(nameof(facilityCandidateCache));
        this.recipeQuery = recipeQuery
            ?? throw new ArgumentNullException(nameof(recipeQuery));
        this.resourceProvider = resourceProvider
            ?? throw new ArgumentNullException(nameof(resourceProvider));
        this.recordTokenConsumer = recordTokenConsumer
            ?? throw new ArgumentNullException(nameof(recordTokenConsumer));
        this.recordComponentService = recordComponentService
            ?? throw new ArgumentNullException(nameof(recordComponentService));
        this.buildingReplacerFactory = buildingReplacerFactory
            ?? throw new ArgumentNullException(nameof(buildingReplacerFactory));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        engine = null;
    }

    public BlueprintResearchState ResearchState
    {
        get { return ResolveResearchStateService().GetState(); }
    }

    public IReadOnlyList<FacilityEvolutionRecipeSO> VisibleRecipes => Engine.VisibleRecipes;

    private FacilityEvolutionEngine Engine => engine ??= CreateEngine();

    public void Configure(
        IFacilityEvolutionRecipeQuery nextRecipeQuery = null,
        IRoomProfileProvider nextRoomProfileProvider = null,
        IFacilityEvolutionRecordProvider nextRecordProvider = null,
        IFacilityEvolutionProposalProvider nextProposalProvider = null,
        IFacilityEvolutionResourceProvider nextResourceProvider = null,
        IFacilityEvolutionBuildingReplacer nextBuildingReplacer = null,
        IRoomLayoutCache nextRoomLayoutCache = null,
        IFacilityEvolutionStateComponentFactory nextStateComponentFactory = null,
        IFacilityCandidateCache nextFacilityCandidateCache = null,
        IFacilityEvolutionValidator nextValidator = null,
        IFacilityEvolutionCandidateBuilder nextCandidateBuilder = null,
        IFacilityEvolutionRecordTokenConsumer nextRecordTokenConsumer = null,
        IFacilityEvolutionRecordComponentService nextRecordComponentService = null,
        IBlueprintResearchStateService nextResearchStateService = null,
        IFacilityEvolutionBuildingReplacerFactory nextBuildingReplacerFactory = null,
        IFacilityEvolutionMutationResolver nextMutationResolver = null,
        IGameEventBus nextGameEventBus = null)
    {
        recipeQuery = nextRecipeQuery ?? recipeQuery;
        recordProvider = nextRecordProvider ?? recordProvider;
        roomProfileProvider = nextRoomProfileProvider ?? roomProfileProvider;
        proposalProvider = nextProposalProvider ?? proposalProvider;
        resourceProvider = nextResourceProvider ?? resourceProvider;
        buildingReplacer = nextBuildingReplacer ?? buildingReplacer;
        roomLayoutCache = nextRoomLayoutCache ?? roomLayoutCache;
        stateComponentFactory = nextStateComponentFactory ?? stateComponentFactory;
        facilityCandidateCache = nextFacilityCandidateCache ?? facilityCandidateCache;
        validator = nextValidator ?? validator;
        candidateBuilder = nextCandidateBuilder ?? candidateBuilder;
        recordTokenConsumer = nextRecordTokenConsumer ?? recordTokenConsumer;
        recordComponentService = nextRecordComponentService ?? recordComponentService;
        blueprintResearchStateService = nextResearchStateService ?? blueprintResearchStateService;
        buildingReplacerFactory = nextBuildingReplacerFactory ?? buildingReplacerFactory;
        mutationResolver = nextMutationResolver ?? mutationResolver;
        gameEventBus = nextGameEventBus ?? gameEventBus;
        engine = null;
    }

    public FacilityEvolutionContext BuildContext(BuildableObject facility)
    {
        return Engine.BuildContext(facility);
    }

    public IReadOnlyList<FacilityEvolutionCandidate> GetCandidates(
        BuildableObject facility,
        bool includeRejected = false,
        bool requestLlmProposal = true)
    {
        return Engine.GetCandidates(facility, includeRejected, requestLlmProposal);
    }

    public bool TryEvolve(
        BuildableObject facility,
        FacilityEvolutionRecipeSO recipe,
        out FacilityEvolutionResult result)
    {
        bool success = Engine.TryEvolve(facility, recipe, out result);
        if (!success)
        {
            return false;
        }

        Completed?.Invoke(result);
        (gameEventBus
            ?? throw new InvalidOperationException($"{nameof(FacilityEvolutionRuntime)} requires {nameof(IGameEventBus)} injection."))
            .Publish(new FacilityEvolutionCompletedEvent(result));
        if (raiseAlertOnEvolution)
        {
            gameEventBus.RaiseAlert(
                "시설 진화 완료",
                $"{result.SourceFacilityName} -> {FacilityShopService.GetBuildingName(result.ResultBuilding.BuildingData)} {result.ResultStarGrade}성",
                EventAlertImportance.Medium,
                "시설 진화");
        }

        return true;
    }

    private FacilityEvolutionEngine CreateEngine()
    {
        IFacilityEvolutionRecordProvider records =
            recordProvider ?? ResolveRecordComponentService();
        IFacilityEvolutionRecipeQuery recipes = recipeQuery
            ?? throw new InvalidOperationException($"{nameof(FacilityEvolutionRuntime)} requires {nameof(IFacilityEvolutionRecipeQuery)} injection or explicit configuration.");
        IFacilityEvolutionRecordTokenConsumer tokens = recordTokenConsumer
            ?? throw new InvalidOperationException($"{nameof(FacilityEvolutionRuntime)} requires {nameof(IFacilityEvolutionRecordTokenConsumer)} injection or explicit configuration.");
        IRoomLayoutCache rooms = ResolveRoomLayoutCache();
        IFacilityEvolutionStateComponentFactory states = ResolveStateComponentFactory();
        IFacilityCandidateCache candidateCache = ResolveFacilityCandidateCache();
        return new FacilityEvolutionEngine(
            recipes,
            roomProfileProvider ?? new RoomProfileBuilder(records, rooms),
            records,
            proposalProvider ?? CreateDefaultProposalProvider(),
            resourceProvider
                ?? throw new InvalidOperationException($"{nameof(FacilityEvolutionRuntime)} requires {nameof(IFacilityEvolutionResourceProvider)} injection or explicit configuration."),
            buildingReplacer ?? ResolveBuildingReplacerFactory().Create(),
            rooms,
            states,
            candidateCache,
            () => ResearchState,
            validator,
            candidateBuilder,
            tokens,
            ResolveRecordComponentService(),
            mutationResolver);
    }

    private IFacilityEvolutionProposalProvider CreateDefaultProposalProvider()
    {
        IFacilityEvolutionProposalProvider ruleBased = new RuleBasedFacilityEvolutionProposalProvider();
        return enableLlmProposals
            ? new CachedLocalLlmFacilityEvolutionProposalProvider(ruleBased, ResolveLocalLlmRuntime)
            : ruleBased;
    }

    private ILocalLlmRuntime ResolveLocalLlmRuntime()
    {
        if (llmRuntimeProvider == null)
        {
            throw new InvalidOperationException($"{nameof(FacilityEvolutionRuntime)} requires {nameof(ILocalLlmRuntimeProvider)} injection.");
        }

        return llmRuntimeProvider.TryGetRuntime(out ILocalLlmRuntime runtime)
            ? runtime
            : null;
    }

    private IBlueprintResearchStateService ResolveResearchStateService()
    {
        return blueprintResearchStateService
            ?? throw new InvalidOperationException($"{nameof(FacilityEvolutionRuntime)} requires {nameof(IBlueprintResearchStateService)} injection.");
    }

    private IRoomLayoutCache ResolveRoomLayoutCache()
    {
        return roomLayoutCache
            ?? throw new InvalidOperationException($"{nameof(FacilityEvolutionRuntime)} requires {nameof(IRoomLayoutCache)} injection.");
    }

    private IFacilityEvolutionStateComponentFactory ResolveStateComponentFactory()
    {
        return stateComponentFactory
            ?? throw new InvalidOperationException($"{nameof(FacilityEvolutionRuntime)} requires {nameof(IFacilityEvolutionStateComponentFactory)} injection.");
    }

    private IFacilityCandidateCache ResolveFacilityCandidateCache()
    {
        return facilityCandidateCache
            ?? throw new InvalidOperationException($"{nameof(FacilityEvolutionRuntime)} requires {nameof(IFacilityCandidateCache)} injection.");
    }

    private IFacilityEvolutionRecordComponentService ResolveRecordComponentService()
    {
        return recordComponentService
            ?? recordProvider as IFacilityEvolutionRecordComponentService
            ?? throw new InvalidOperationException($"{nameof(FacilityEvolutionRuntime)} requires {nameof(IFacilityEvolutionRecordComponentService)} injection or explicit configuration.");
    }

    private IFacilityEvolutionBuildingReplacerFactory ResolveBuildingReplacerFactory()
    {
        return buildingReplacerFactory
            ?? throw new InvalidOperationException($"{nameof(FacilityEvolutionRuntime)} requires {nameof(IFacilityEvolutionBuildingReplacerFactory)} injection or explicit configuration.");
    }
}
