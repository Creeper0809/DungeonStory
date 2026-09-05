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
    private readonly IProductionFacilityMutationFence productionMutationFence;
    private readonly IProductionFacilityRetargetTransaction productionRetarget;
    private readonly IProductionFacilityHandleQuery productionFacilityHandles =
        new ProductionFacilityHandleQueryAdapter();

    public GridFacilityEvolutionBuildingReplacer(GridBuildingFactory buildingFactory)
        : this(buildingFactory, null, null)
    {
    }

    public GridFacilityEvolutionBuildingReplacer(
        GridBuildingFactory buildingFactory,
        IProductionFacilityMutationFence productionMutationFence)
        : this(buildingFactory, productionMutationFence, null)
    {
    }

    public GridFacilityEvolutionBuildingReplacer(
        GridBuildingFactory buildingFactory,
        IProductionFacilityMutationFence productionMutationFence,
        IProductionFacilityRetargetTransaction productionRetarget)
    {
        this.buildingFactory = buildingFactory
            ?? throw new ArgumentNullException(nameof(buildingFactory));
        this.productionMutationFence = productionMutationFence;
        this.productionRetarget = productionRetarget;
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

        bool canOwnProduction = source.BuildingData?.GetProductionWorkstationAbility() != null
            || source.BuildingData?.GetProductionBufferAbility() != null;
        // The staged retarget transaction is the authority for active owners.
        // Keep the legacy empty-only fence only for narrow fixtures that have
        // not supplied that transaction yet.
        if (canOwnProduction && productionRetarget == null
            && productionMutationFence == null)
        {
            reason = "생산 시설 진화 수명주기 권위를 찾을 수 없습니다.";
            return false;
        }
        else if (canOwnProduction && productionRetarget == null
                 && !productionMutationFence.TryRequireNoAuthority(
                     source,
                     ProductionFacilityMutationKind.Evolution,
                     out string productionFailure))
        {
            reason = "생산 주문·재공품·출력 권위가 남은 시설은 진화할 수 없습니다. "
                + productionFailure;
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
        BuildingInstanceId survivorId = source.RequirePersistentInstanceId();
        ProductionFacilityRetargetTransactionState retargetState = null;
        if (productionRetarget != null)
        {
            ProductionFacilityRetargetRequest request = new(
                productionFacilityHandles.CaptureFacility(source),
                ProductionFacilityMutationKind.Evolution);
            if (!productionRetarget.TryBegin(
                    new[] { request },
                    "evolution:" + survivorId.Value,
                    out retargetState,
                    out string beginFailure))
            {
                reason = "생산 권위 진화 사전 검증 실패: " + beginFailure;
                return false;
            }
        }

        BuildableObject candidate = null;
        try
        {
            candidate = buildingFactory.CreateDetached(
                grid,
                resultBuilding,
                position);
            if (candidate == null)
            {
                RollbackRetargetOrThrow(retargetState, "candidate-creation");
                reason = "결과 시설 후보 생성 실패";
                return false;
            }
            candidate.RestorePersistentIdentity(survivorId);
            candidate.SetGrid(grid);
            candidate.Initialization(resultBuilding, position);
        }
        catch (Exception exception)
        {
            DiscardCandidate(candidate, resultBuilding, position);
            RollbackRetargetOrThrow(retargetState, "candidate-initialization");
            reason = "결과 시설 후보 초기화 실패: " + exception.Message;
            return false;
        }

        if (!grid.RemoveOccupant(
                source,
                sourceBuilding.Placement.Layer,
                source.buildPoses,
                sourceBuilding.Placement.IsMovement))
        {
            DiscardCandidate(candidate, resultBuilding, position);
            RollbackRetargetOrThrow(retargetState, "occupancy-removal");
            reason = "원본 시설 점유를 해제할 수 없습니다";
            return false;
        }

        bool registered = grid.RegisterOccupant(
            candidate,
            resultBuilding.Placement.Layer,
            resultBuilding.GetGridPosList(position),
            resultBuilding.Placement.IsMovement);
        if (!registered)
        {
            DiscardCandidate(candidate, resultBuilding, position);
            RestoreSourceOccupancy(grid, source, sourceBuilding);
            RollbackRetargetOrThrow(retargetState, "candidate-registration");
            reason = "결과 시설 배치 실패";
            return false;
        }

        IBuildingWorldRegistryPort worldRegistry = source.WorldRegistry;
        string registryFailure = worldRegistry == null
            ? "building-world-registry-unavailable"
            : string.Empty;
        if (worldRegistry == null
            || !worldRegistry.TryReplaceBuilding(
                source,
                candidate,
                out registryFailure))
        {
            grid.RemoveOccupant(
                candidate,
                resultBuilding.Placement.Layer,
                candidate.buildPoses,
                resultBuilding.Placement.IsMovement);
            DiscardCandidate(candidate, resultBuilding, position);
            RestoreSourceOccupancy(grid, source, sourceBuilding);
            RollbackRetargetOrThrow(retargetState, "world-registry-handoff");
            reason = "결과 시설 월드 권위 교체 실패: " + registryFailure;
            return false;
        }

        if (retargetState != null)
        {
            bool authorityCommitted;
            string commitFailure;
            try
            {
                ProductionFacilityHandle target =
                    productionFacilityHandles.CaptureFacility(candidate);
                authorityCommitted = productionRetarget.TryCommit(
                    retargetState,
                    new[]
                    {
                        new ProductionFacilityRetargetBinding(survivorId, target)
                    },
                    out commitFailure);
            }
            catch (Exception exception)
            {
                authorityCommitted = false;
                commitFailure = "target-capture-or-commit-exception:"
                    + exception.GetType().Name + ":" + exception.Message;
            }
            if (!authorityCommitted)
            {
                RollbackRetargetOrThrow(retargetState, "authority-commit");
                if (!worldRegistry.TryRollbackBuildingReplacement(
                        candidate,
                        source,
                        out string rollbackFailure))
                {
                    throw new InvalidOperationException(
                        "Evolution authority commit failed and world rollback failed: "
                        + rollbackFailure);
                }
                grid.RemoveOccupant(
                    candidate,
                    resultBuilding.Placement.Layer,
                    candidate.buildPoses,
                    resultBuilding.Placement.IsMovement);
                DiscardCandidate(candidate, resultBuilding, position);
                RestoreSourceOccupancy(grid, source, sourceBuilding);
                reason = "생산 권위 진화 반영 실패로 원본을 복구했습니다: "
                    + commitFailure;
                return false;
            }
        }

        try
        {
            buildingFactory.PublishDetached(candidate, resultBuilding, position);
        }
        catch (Exception exception)
        {
            RollbackRetargetOrThrow(retargetState, "publication");
            if (!worldRegistry.TryRollbackBuildingReplacement(
                    candidate,
                    source,
                    out string rollbackFailure))
            {
                throw new InvalidOperationException(
                    "Evolution publication failed and world authority rollback also failed: "
                    + rollbackFailure,
                    exception);
            }
            grid.RemoveOccupant(
                candidate,
                resultBuilding.Placement.Layer,
                candidate.buildPoses,
                resultBuilding.Placement.IsMovement);
            DiscardCandidate(candidate, resultBuilding, position);
            RestoreSourceOccupancy(grid, source, sourceBuilding);
            reason = "결과 시설 게시 실패로 원본을 복구했습니다: "
                + exception.Message;
            return false;
        }

        if (retargetState != null
            && !productionRetarget.TryComplete(
                retargetState,
                out string completionFailure))
        {
            RollbackRetargetOrThrow(retargetState, "completion");
            if (!worldRegistry.TryRollbackBuildingReplacement(
                    candidate,
                    source,
                    out string rollbackFailure))
            {
                throw new InvalidOperationException(
                    "Evolution authority completion failed and world rollback failed: "
                    + rollbackFailure);
            }
            grid.RemoveOccupant(
                candidate,
                resultBuilding.Placement.Layer,
                candidate.buildPoses,
                resultBuilding.Placement.IsMovement);
            DiscardCandidate(candidate, resultBuilding, position);
            RestoreSourceOccupancy(grid, source, sourceBuilding);
            reason = "생산 권위 진화 완료 검증 실패로 원본을 복구했습니다: "
                + completionFailure;
            return false;
        }

        buildingFactory.DeleteVisual(sourceBuilding, position);
        source.SetGrid(null);
        source.RetireForWorldReplacement();
        result = candidate;
        reason = string.Empty;
        return true;
    }

    private void RollbackRetargetOrThrow(
        ProductionFacilityRetargetTransactionState state,
        string phase)
    {
        if (state == null
            || state.Phase is ProductionFacilityRetargetTransactionPhase
                .RolledBack or ProductionFacilityRetargetTransactionPhase.Completed)
        {
            return;
        }
        if (!productionRetarget.TryRollback(state, out string failureReason))
        {
            throw new InvalidOperationException(
                "Facility evolution retarget rollback failed during " + phase
                + ":" + failureReason);
        }
    }

    private void DiscardCandidate(
        BuildableObject candidate,
        BuildingSO resultBuilding,
        Vector2Int position)
    {
        if (candidate == null)
        {
            return;
        }

        buildingFactory.DeleteVisual(resultBuilding, position);
        candidate.SetGrid(null);
        if (candidate.IsDetachedRestoreCandidate)
            buildingFactory.DiscardDetached(candidate);
        else
            candidate.RetireForWorldReplacement();
    }

    private static void RestoreSourceOccupancy(
        Grid grid,
        BuildableObject source,
        BuildingSO sourceBuilding)
    {
        if (grid == null
            || source == null
            || sourceBuilding == null
            || !grid.RegisterOccupant(
                source,
                sourceBuilding.Placement.Layer,
                source.buildPoses,
                sourceBuilding.Placement.IsMovement))
        {
            throw new InvalidOperationException(
                "Facility evolution replacement failed and the original facility occupancy could not be restored.");
        }
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

public sealed class FacilityEvolutionDefinitionContext
{
    public FacilityEvolutionDefinitionContext(
        IFacilityEvolutionRecipeQuery recipeQuery,
        IRoomProfileProvider roomProfileProvider,
        IFacilityEvolutionRecordProvider recordProvider,
        IFacilityEvolutionProposalProvider proposalProvider,
        IRoomLayoutCache roomLayoutCache,
        IFacilityEvolutionStateComponentFactory stateComponentFactory,
        IFacilityEvolutionRecordComponentService recordComponentService)
    {
        RecipeQuery = recipeQuery
            ?? throw new ArgumentNullException(nameof(recipeQuery));
        RecordComponentService = recordComponentService
            ?? recordProvider as IFacilityEvolutionRecordComponentService
            ?? throw new ArgumentNullException(nameof(recordComponentService));
        RecordProvider = recordProvider ?? RecordComponentService;
        ProposalProvider = proposalProvider
            ?? throw new ArgumentNullException(nameof(proposalProvider));
        RoomLayoutCache = roomLayoutCache
            ?? throw new ArgumentNullException(nameof(roomLayoutCache));
        StateComponentFactory = stateComponentFactory
            ?? throw new ArgumentNullException(nameof(stateComponentFactory));
        RoomProfileProvider = roomProfileProvider
            ?? throw new ArgumentNullException(nameof(roomProfileProvider));
    }

    public IFacilityEvolutionRecipeQuery RecipeQuery { get; }
    public IRoomProfileProvider RoomProfileProvider { get; }
    public IFacilityEvolutionRecordProvider RecordProvider { get; }
    public IFacilityEvolutionProposalProvider ProposalProvider { get; }
    public IRoomLayoutCache RoomLayoutCache { get; }
    public IFacilityEvolutionStateComponentFactory StateComponentFactory { get; }
    public IFacilityEvolutionRecordComponentService RecordComponentService { get; }
}

public sealed class FacilityEvolutionExecutionContext
{
    public FacilityEvolutionExecutionContext(
        IFacilityEvolutionResourceProvider resourceProvider,
        IFacilityEvolutionBuildingReplacer buildingReplacer,
        IFacilityCandidateCache facilityCandidateCache,
        Func<BlueprintResearchState> researchStateProvider,
        IFacilityEvolutionValidator validator,
        IFacilityEvolutionCandidateBuilder candidateBuilder,
        IFacilityEvolutionRecordTokenConsumer recordTokenConsumer,
        IFacilityEvolutionMutationResolver mutationResolver)
    {
        ResourceProvider = resourceProvider
            ?? throw new ArgumentNullException(nameof(resourceProvider));
        BuildingReplacer = buildingReplacer
            ?? throw new ArgumentNullException(nameof(buildingReplacer));
        FacilityCandidateCache = facilityCandidateCache
            ?? throw new ArgumentNullException(nameof(facilityCandidateCache));
        ResearchStateProvider = researchStateProvider
            ?? throw new ArgumentNullException(nameof(researchStateProvider));
        Validator = validator
            ?? throw new ArgumentNullException(nameof(validator));
        CandidateBuilder = candidateBuilder
            ?? throw new ArgumentNullException(nameof(candidateBuilder));
        RecordTokenConsumer = recordTokenConsumer
            ?? throw new ArgumentNullException(nameof(recordTokenConsumer));
        MutationResolver = mutationResolver
            ?? throw new ArgumentNullException(nameof(mutationResolver));
    }

    public IFacilityEvolutionResourceProvider ResourceProvider { get; }
    public IFacilityEvolutionBuildingReplacer BuildingReplacer { get; }
    public IFacilityCandidateCache FacilityCandidateCache { get; }
    public Func<BlueprintResearchState> ResearchStateProvider { get; }
    public IFacilityEvolutionValidator Validator { get; }
    public IFacilityEvolutionCandidateBuilder CandidateBuilder { get; }
    public IFacilityEvolutionRecordTokenConsumer RecordTokenConsumer { get; }
    public IFacilityEvolutionMutationResolver MutationResolver { get; }
}

public interface IFacilityEvolutionExecutionContextFactory
{
    FacilityEvolutionExecutionContext Create(
        Func<BlueprintResearchState> researchStateProvider);
}

public sealed class FacilityEvolutionExecutionContextFactory :
    IFacilityEvolutionExecutionContextFactory
{
    private readonly IFacilityEvolutionResourceProvider resourceProvider;
    private readonly IFacilityEvolutionBuildingReplacerFactory buildingReplacerFactory;
    private readonly IFacilityCandidateCache facilityCandidateCache;
    private readonly IFacilityEvolutionValidator validator;
    private readonly IFacilityEvolutionCandidateBuilder candidateBuilder;
    private readonly IFacilityEvolutionRecordTokenConsumer recordTokenConsumer;
    private readonly IFacilityEvolutionMutationResolver mutationResolver;

    public FacilityEvolutionExecutionContextFactory(
        IFacilityEvolutionResourceProvider resourceProvider,
        IFacilityEvolutionBuildingReplacerFactory buildingReplacerFactory,
        IFacilityCandidateCache facilityCandidateCache,
        IFacilityEvolutionValidator validator,
        IFacilityEvolutionCandidateBuilder candidateBuilder,
        IFacilityEvolutionRecordTokenConsumer recordTokenConsumer,
        IFacilityEvolutionMutationResolver mutationResolver)
    {
        this.resourceProvider = resourceProvider
            ?? throw new ArgumentNullException(nameof(resourceProvider));
        this.buildingReplacerFactory = buildingReplacerFactory
            ?? throw new ArgumentNullException(nameof(buildingReplacerFactory));
        this.facilityCandidateCache = facilityCandidateCache
            ?? throw new ArgumentNullException(nameof(facilityCandidateCache));
        this.validator = validator
            ?? throw new ArgumentNullException(nameof(validator));
        this.candidateBuilder = candidateBuilder
            ?? throw new ArgumentNullException(nameof(candidateBuilder));
        this.recordTokenConsumer = recordTokenConsumer
            ?? throw new ArgumentNullException(nameof(recordTokenConsumer));
        this.mutationResolver = mutationResolver
            ?? throw new ArgumentNullException(nameof(mutationResolver));
    }

    public FacilityEvolutionExecutionContext Create(
        Func<BlueprintResearchState> researchStateProvider) =>
        new FacilityEvolutionExecutionContext(
            resourceProvider,
            buildingReplacerFactory.Create(),
            facilityCandidateCache,
            researchStateProvider
                ?? throw new ArgumentNullException(nameof(researchStateProvider)),
            validator,
            candidateBuilder,
            recordTokenConsumer,
            mutationResolver);
}

public interface IFacilityEvolutionEngineFactory
{
    FacilityEvolutionEngine Create(
        FacilityEvolutionDefinitionContext definitions,
        FacilityEvolutionExecutionContext execution);
}

public sealed class FacilityEvolutionEngineFactory : IFacilityEvolutionEngineFactory
{
    public FacilityEvolutionEngine Create(
        FacilityEvolutionDefinitionContext definitions,
        FacilityEvolutionExecutionContext execution)
    {
        return new FacilityEvolutionEngine(definitions, execution);
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
        FacilityEvolutionDefinitionContext definitions,
        FacilityEvolutionExecutionContext execution)
    {
        definitions = definitions
            ?? throw new ArgumentNullException(nameof(definitions));
        execution = execution
            ?? throw new ArgumentNullException(nameof(execution));
        recipeQuery = definitions.RecipeQuery;
        roomProfileProvider = definitions.RoomProfileProvider;
        recordProvider = definitions.RecordProvider;
        proposalProvider = definitions.ProposalProvider;
        roomLayoutCache = definitions.RoomLayoutCache;
        stateComponentFactory = definitions.StateComponentFactory;
        recordComponentService = definitions.RecordComponentService;
        resourceProvider = execution.ResourceProvider;
        buildingReplacer = execution.BuildingReplacer;
        facilityCandidateCache = execution.FacilityCandidateCache;
        validator = execution.Validator;
        candidateBuilder = execution.CandidateBuilder;
        recordTokenConsumer = execution.RecordTokenConsumer;
        mutationResolver = execution.MutationResolver;
        researchStateProvider = execution.ResearchStateProvider;
    }

    public BlueprintResearchState ResearchState => researchStateProvider();

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
        if (recipe == null)
        {
            result = Fail(null, facility, default, "진화 레시피가 없습니다");
            return false;
        }

        FacilityEvolutionContext context = BuildContext(facility);
        if (context.State.HasPendingMaterialCommit)
        {
            return TryResumePending(
                facility,
                recipe,
                out result,
                out _);
        }

        FacilityEvolutionProposal proposal = proposalProvider.Propose(context);
        string materialOperationId = BuildMaterialOperationId(context);
        string materialReasonCode = "facility-evolution-material-incorporated:"
            + recipe.EffectiveId;
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

        int historySequence = checked(context.State.EvolutionHistory.Count + 1);
        FacilityEvolutionStateSnapshot resolvedResultState =
            FacilityEvolutionStateComponent.BuildResolvedEvolutionSnapshot(
                stateSnapshot,
                facility.BuildingData,
                recipe.resultBuilding,
                recipe,
                proposal,
                sourceFacilityName,
                context.Profile,
                mutationResult.Tags,
                recordSnapshot);

        if (!resourceProvider.TryCommitMaterialsPending(
                recipe.requiredMaterials,
                materialOperationId,
                materialReasonCode,
                out FacilityEvolutionMaterialCommitReceipt materialReceipt,
                out string materialReason))
        {
            result = Fail(
                recipe,
                facility,
                proposal,
                string.IsNullOrWhiteSpace(materialReason)
                    ? "재료 소모 실패"
                    : materialReason);
            return false;
        }

        if (!materialReceipt.IsCommitted)
        {
            return TryPublishResolvedEvolutionWithoutMaterials(
                facility,
                recipe,
                proposal,
                sourceFacilityName,
                mutationResult.Tags,
                resolvedResultState,
                out result);
        }

        context.State.RecordPendingMaterialCommit(
            materialReceipt,
            recipe,
            FacilityEvolutionUtility.GetFacilityId(facility.BuildingData),
            historySequence,
            resolvedResultState,
            mutationResult.Tags);
        facilityCandidateCache.MarkDynamicStateDirty();
        return TryResumePending(
            facility,
            recipe,
            out result,
            out _);
    }

    public bool TryResumePending(
        BuildableObject facility,
        out FacilityEvolutionResult result,
        out string failureReason)
    {
        result = default;
        failureReason = string.Empty;
        if (facility == null || facility.isDestroy)
        {
            failureReason = "Facility evolution pending source is unavailable.";
            return false;
        }

        FacilityEvolutionStateComponent state = stateComponentFactory.GetOrAdd(facility);
        FacilityEvolutionPendingMaterialCommitSnapshot pending =
            state.PendingMaterialCommit;
        if (pending == null)
        {
            failureReason = "Facility evolution has no pending material commit.";
            return false;
        }

        FacilityEvolutionRecipeSO recipe = recipeQuery.GetRecipes()
            .SingleOrDefault(candidate => candidate != null
                && string.Equals(
                    candidate.EffectiveId,
                    pending.recipeId,
                    StringComparison.Ordinal));
        if (recipe == null)
        {
            failureReason = "Facility evolution pending recipe is not authored: "
                + pending.recipeId;
            return false;
        }

        return TryResumePending(
            facility,
            recipe,
            out result,
            out failureReason);
    }

    private bool TryResumePending(
        BuildableObject facility,
        FacilityEvolutionRecipeSO recipe,
        out FacilityEvolutionResult result,
        out string failureReason)
    {
        result = Fail(recipe, facility, default, "진화 재개 실패");
        failureReason = string.Empty;
        FacilityEvolutionStateComponent state = stateComponentFactory.GetOrAdd(facility);
        FacilityEvolutionStateSnapshot stateSnapshot = state.CreateSnapshot();
        FacilityEvolutionAggregateAdapter.ValidatePendingMaterialCommit(stateSnapshot);
        FacilityEvolutionPendingMaterialCommitSnapshot pending =
            stateSnapshot.pendingMaterialCommit;
        string sourceFacilityName =
            FacilityShopService.GetBuildingName(facility.BuildingData);
        if (recipe == null
            || !string.Equals(
                recipe.EffectiveId,
                pending.recipeId,
                StringComparison.Ordinal)
            || recipe.resultBuilding == null
            || !string.Equals(
                FacilityEvolutionUtility.GetFacilityId(recipe.resultBuilding),
                pending.resultFacilityDefinitionId,
                StringComparison.Ordinal))
        {
            failureReason =
                "Facility evolution pending recipe/result authority does not match the request.";
            result = Fail(recipe, facility, default, failureReason);
            return false;
        }

        if (!resourceProvider.TryGetPendingMaterialCommit(
                pending.operationId,
                pending.reasonCode,
                out FacilityEvolutionMaterialCommitReceipt receipt,
                out string receiptFailure)
            || !FacilityEvolutionMaterialCommitAuthority.Matches(
                pending,
                receipt))
        {
            failureReason = !string.IsNullOrWhiteSpace(receiptFailure)
                ? receiptFailure
                : "Facility evolution pending physical receipt does not match its aggregate.";
            result = Fail(recipe, facility, default, failureReason);
            return false;
        }

        BuildableObject resultBuilding = facility;
        FacilityEvolutionStateComponent resultState = state;
        FacilityEvolutionStateSnapshot resolvedResultState =
            pending.ReadResolvedResultState();
        if (pending.phase == FacilityEvolutionMaterialCommitPhase.MaterialCommitted)
        {
            if (!string.Equals(
                    FacilityEvolutionUtility.GetFacilityId(facility.BuildingData),
                    pending.sourceFacilityDefinitionId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    state.FacilityPersistentId,
                    pending.sourceFacilityPersistentId,
                    StringComparison.Ordinal))
            {
                failureReason =
                    "Facility evolution pending source building authority changed before publication.";
                result = Fail(recipe, facility, default, failureReason);
                return false;
            }

            if (!buildingReplacer.TryReplace(
                    facility,
                    recipe.resultBuilding,
                    out resultBuilding,
                    out string replaceReason))
            {
                failureReason = replaceReason;
                result = Fail(recipe, facility, default, replaceReason);
                return false;
            }

            resultBuilding.SetFacilityLevel(recipe.resultStarGrade);
            resultState = stateComponentFactory.GetOrAdd(resultBuilding);
            resultState.ApplySnapshot(resolvedResultState);
            resultState.RecordPendingMaterialCommit(
                receipt,
                recipe,
                pending.sourceFacilityDefinitionId,
                pending.historySequence,
                resolvedResultState,
                pending.resolvedMutationTags,
                FacilityEvolutionMaterialCommitPhase.DomainApplied);
            recordComponentService.ReplaceWith(
                resultBuilding,
                resultState.GetRecord());
            facilityCandidateCache.MarkDynamicStateDirty();
            roomLayoutCache.Clear();
        }

        if (!resourceProvider.AcknowledgeMaterialCommit(
                receipt.CommitId,
                out string acknowledgementReason))
        {
            failureReason =
                "Facility evolution material acknowledgement failed: "
                + acknowledgementReason;
            result = Fail(recipe, resultBuilding, default, failureReason);
            return false;
        }

        resultState.ClearPendingMaterialCommit(receipt.CommitId);
        facilityCandidateCache.MarkDynamicStateDirty();
        FacilityEvolutionProposal persistedProposal = BuildPersistedProposal(pending);
        result = new FacilityEvolutionResult(
            true,
            recipe,
            resultBuilding,
            recipe.resultStarGrade,
            sourceFacilityName,
            persistedProposal,
            $"{recipe.DisplayName} 진화 완료",
            pending.resolvedMutationTags);
        return true;
    }

    private bool TryPublishResolvedEvolutionWithoutMaterials(
        BuildableObject facility,
        FacilityEvolutionRecipeSO recipe,
        FacilityEvolutionProposal proposal,
        string sourceFacilityName,
        IReadOnlyList<string> mutationTags,
        FacilityEvolutionStateSnapshot resolvedResultState,
        out FacilityEvolutionResult result)
    {
        if (!buildingReplacer.TryReplace(
                facility,
                recipe.resultBuilding,
                out BuildableObject resultBuilding,
                out string replaceReason))
        {
            result = Fail(recipe, facility, proposal, replaceReason);
            return false;
        }

        resultBuilding.SetFacilityLevel(recipe.resultStarGrade);
        FacilityEvolutionStateComponent nextState =
            stateComponentFactory.GetOrAdd(resultBuilding);
        nextState.ApplySnapshot(resolvedResultState);
        recordComponentService.ReplaceWith(resultBuilding, nextState.GetRecord());
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
            mutationTags);
        return true;
    }

    private static FacilityEvolutionProposal BuildPersistedProposal(
        FacilityEvolutionPendingMaterialCommitSnapshot pending)
    {
        FacilityEvolutionStateSnapshot resolved = pending.ReadResolvedResultState();
        FacilityEvolutionHistoryEntry history = resolved.evolutionHistory
            .LastOrDefault();
        return new FacilityEvolutionProposal(
            resolved.lastIdentitySummary,
            new[] { pending.recipeId },
            null,
            pending.resolvedMutationTags,
            history?.summary ?? string.Empty,
            1f,
            FacilityEvolutionProposalSources.RuleBased,
            "Restored exact pending facility evolution result");
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

    private static string BuildMaterialOperationId(FacilityEvolutionContext context)
    {
        if (context?.State == null
            || string.IsNullOrWhiteSpace(context.State.FacilityPersistentId))
        {
            throw new InvalidOperationException(
                "Facility evolution material operation requires a persistent facility identity.");
        }
        int nextHistorySequence = checked(context.State.EvolutionHistory.Count + 1);
        return "facility-evolution-material:"
            + context.State.FacilityPersistentId
            + ":sequence:"
            + nextHistorySequence.ToString("D8");
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
    [SerializeField, HideInInspector] private bool enableLlmProposals;

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
    private IFacilityEvolutionBuildingReplacerFactory buildingReplacerFactory;
    private IFacilityEvolutionEngineFactory engineFactory;
    private FacilityEvolutionDefinitionContext definitionContext;
    private IFacilityEvolutionExecutionContextFactory executionContextFactory;
    private IGameEventBus gameEventBus;
    private FacilityEvolutionEngine engine;

    public event Action<FacilityEvolutionResult> Completed;

    [Inject]
    public void ConstructFacilityEvolutionRuntime(
        IBlueprintResearchStateService blueprintResearchStateService,
        FacilityEvolutionDefinitionContext definitionContext,
        IFacilityEvolutionExecutionContextFactory executionContextFactory,
        IFacilityEvolutionEngineFactory engineFactory,
        IGameEventBus gameEventBus)
    {
        this.blueprintResearchStateService = blueprintResearchStateService
            ?? throw new ArgumentNullException(nameof(blueprintResearchStateService));
        this.definitionContext = definitionContext
            ?? throw new ArgumentNullException(nameof(definitionContext));
        this.executionContextFactory = executionContextFactory
            ?? throw new ArgumentNullException(nameof(executionContextFactory));
        this.engineFactory = engineFactory
            ?? throw new ArgumentNullException(nameof(engineFactory));
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
        IFacilityEvolutionRecipeQuery nextRecipeQuery,
        IRoomProfileProvider nextRoomProfileProvider,
        IFacilityEvolutionRecordProvider nextRecordProvider,
        IFacilityEvolutionProposalProvider nextProposalProvider,
        IFacilityEvolutionResourceProvider nextResourceProvider,
        IFacilityEvolutionBuildingReplacer nextBuildingReplacer,
        IRoomLayoutCache nextRoomLayoutCache,
        IFacilityEvolutionStateComponentFactory nextStateComponentFactory,
        IFacilityCandidateCache nextFacilityCandidateCache,
        IFacilityEvolutionValidator nextValidator,
        IFacilityEvolutionCandidateBuilder nextCandidateBuilder,
        IFacilityEvolutionRecordTokenConsumer nextRecordTokenConsumer,
        IFacilityEvolutionRecordComponentService nextRecordComponentService,
        IBlueprintResearchStateService nextResearchStateService,
        IFacilityEvolutionBuildingReplacerFactory nextBuildingReplacerFactory,
        IFacilityEvolutionMutationResolver nextMutationResolver,
        IGameEventBus nextGameEventBus,
        IFacilityEvolutionEngineFactory nextEngineFactory)
    {
        recipeQuery = nextRecipeQuery ?? throw new ArgumentNullException(nameof(nextRecipeQuery));
        recordProvider = nextRecordProvider ?? throw new ArgumentNullException(nameof(nextRecordProvider));
        roomProfileProvider = nextRoomProfileProvider ?? throw new ArgumentNullException(nameof(nextRoomProfileProvider));
        proposalProvider = nextProposalProvider ?? throw new ArgumentNullException(nameof(nextProposalProvider));
        resourceProvider = nextResourceProvider ?? throw new ArgumentNullException(nameof(nextResourceProvider));
        buildingReplacer = nextBuildingReplacer ?? throw new ArgumentNullException(nameof(nextBuildingReplacer));
        roomLayoutCache = nextRoomLayoutCache ?? throw new ArgumentNullException(nameof(nextRoomLayoutCache));
        stateComponentFactory = nextStateComponentFactory ?? throw new ArgumentNullException(nameof(nextStateComponentFactory));
        facilityCandidateCache = nextFacilityCandidateCache ?? throw new ArgumentNullException(nameof(nextFacilityCandidateCache));
        validator = nextValidator ?? throw new ArgumentNullException(nameof(nextValidator));
        candidateBuilder = nextCandidateBuilder ?? throw new ArgumentNullException(nameof(nextCandidateBuilder));
        recordTokenConsumer = nextRecordTokenConsumer ?? throw new ArgumentNullException(nameof(nextRecordTokenConsumer));
        recordComponentService = nextRecordComponentService ?? throw new ArgumentNullException(nameof(nextRecordComponentService));
        blueprintResearchStateService = nextResearchStateService ?? throw new ArgumentNullException(nameof(nextResearchStateService));
        buildingReplacerFactory = nextBuildingReplacerFactory;
        mutationResolver = nextMutationResolver ?? throw new ArgumentNullException(nameof(nextMutationResolver));
        gameEventBus = nextGameEventBus ?? throw new ArgumentNullException(nameof(nextGameEventBus));
        engineFactory = nextEngineFactory ?? throw new ArgumentNullException(nameof(nextEngineFactory));
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

        PublishCompletion(result);
        return true;
    }

    public bool TryReconcilePendingMaterialEvolution(
        BuildableObject facility,
        out FacilityEvolutionResult result,
        out string failureReason)
    {
        bool success = Engine.TryResumePending(
            facility,
            out result,
            out failureReason);
        if (!success)
        {
            return false;
        }

        PublishCompletion(result);
        return true;
    }

    private void PublishCompletion(FacilityEvolutionResult result)
    {
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
    }

    private FacilityEvolutionEngine CreateEngine()
    {
        if (definitionContext != null && executionContextFactory != null)
        {
            return ResolveEngineFactory().Create(
                definitionContext,
                executionContextFactory.Create(() => ResearchState));
        }

        IFacilityEvolutionRecordProvider records =
            recordProvider ?? ResolveRecordComponentService();
        IFacilityEvolutionRecipeQuery recipes = recipeQuery
            ?? throw new InvalidOperationException($"{nameof(FacilityEvolutionRuntime)} requires {nameof(IFacilityEvolutionRecipeQuery)} injection or explicit configuration.");
        IFacilityEvolutionRecordTokenConsumer tokens = recordTokenConsumer
            ?? throw new InvalidOperationException($"{nameof(FacilityEvolutionRuntime)} requires {nameof(IFacilityEvolutionRecordTokenConsumer)} injection or explicit configuration.");
        IRoomLayoutCache rooms = ResolveRoomLayoutCache();
        IFacilityEvolutionStateComponentFactory states = ResolveStateComponentFactory();
        IFacilityCandidateCache candidateCache = ResolveFacilityCandidateCache();
        FacilityEvolutionDefinitionContext definitions =
            new FacilityEvolutionDefinitionContext(
                recipes,
                roomProfileProvider
                    ?? throw new InvalidOperationException($"{nameof(FacilityEvolutionRuntime)} requires {nameof(IRoomProfileProvider)} injection or explicit configuration."),
                records,
                proposalProvider
                    ?? throw new InvalidOperationException($"{nameof(FacilityEvolutionRuntime)} requires {nameof(IFacilityEvolutionProposalProvider)} injection or explicit configuration."),
                rooms,
                states,
                ResolveRecordComponentService());
        FacilityEvolutionExecutionContext execution =
            new FacilityEvolutionExecutionContext(
                resourceProvider
                    ?? throw new InvalidOperationException($"{nameof(FacilityEvolutionRuntime)} requires {nameof(IFacilityEvolutionResourceProvider)} injection or explicit configuration."),
                buildingReplacer ?? ResolveBuildingReplacerFactory().Create(),
                candidateCache,
                () => ResearchState,
                validator
                    ?? throw new InvalidOperationException($"{nameof(FacilityEvolutionRuntime)} requires {nameof(IFacilityEvolutionValidator)} injection or explicit configuration."),
                candidateBuilder
                    ?? throw new InvalidOperationException($"{nameof(FacilityEvolutionRuntime)} requires {nameof(IFacilityEvolutionCandidateBuilder)} injection or explicit configuration."),
                tokens,
                mutationResolver
                    ?? throw new InvalidOperationException($"{nameof(FacilityEvolutionRuntime)} requires {nameof(IFacilityEvolutionMutationResolver)} injection or explicit configuration."));
        return ResolveEngineFactory().Create(definitions, execution);
    }

    private IFacilityEvolutionEngineFactory ResolveEngineFactory()
    {
        return engineFactory
            ?? throw new InvalidOperationException(
                $"{nameof(FacilityEvolutionRuntime)} requires {nameof(IFacilityEvolutionEngineFactory)} injection or explicit configuration.");
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
