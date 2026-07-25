using System;
using System.Collections.Generic;
using System.Linq;

public sealed class BuildingFeatureRoomRow
{
    public Grid Grid { get; set; }
    public RoomInstance Room { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Feedback { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}

public sealed class BuildingFeatureSynthesisMaterialRow
{
    public BuildableObject Facility { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}

public sealed class BuildingFeatureSynthesisRecipeRow
{
    public FacilitySynthesisRecipeSO Recipe { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public sealed class BuildingFeatureEvolutionRow
{
    public BuildableObject Facility { get; set; }
    public FacilityEvolutionRecipeSO Recipe { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
}

public sealed class BuildingFeatureSurfaceModel
{
    public IReadOnlyList<BuildingFeatureRoomRow> Rooms { get; set; } =
        Array.Empty<BuildingFeatureRoomRow>();
    public IReadOnlyList<BuildingFeatureSynthesisMaterialRow> SynthesisMaterials { get; set; } =
        Array.Empty<BuildingFeatureSynthesisMaterialRow>();
    public IReadOnlyList<BuildingFeatureSynthesisRecipeRow> SynthesisRecipes { get; set; } =
        Array.Empty<BuildingFeatureSynthesisRecipeRow>();
    public IReadOnlyList<BuildingFeatureEvolutionRow> EvolutionCandidates { get; set; } =
        Array.Empty<BuildingFeatureEvolutionRow>();
    public bool HasSynthesisRuntime { get; set; }
    public bool HasEvolutionRuntime { get; set; }
}

public readonly struct BuildingFeatureCommandResult
{
    public BuildingFeatureCommandResult(bool success, string message)
    {
        Success = success;
        Message = message ?? string.Empty;
    }

    public bool Success { get; }
    public string Message { get; }
}

public interface IBuildingFeatureQueryService
{
    BuildingFeatureSurfaceModel Capture();
}

public interface IBuildingFeatureCommandService
{
    BuildingFeatureCommandResult InspectRoom(Grid grid, RoomInstance room, string feedback);
    BuildingFeatureCommandResult ToggleSynthesisMaterial(BuildableObject facility);
    BuildingFeatureCommandResult ExecuteSynthesis(FacilitySynthesisRecipeSO recipe);
    BuildingFeatureCommandResult ExecuteEvolution(
        BuildableObject facility,
        FacilityEvolutionRecipeSO recipe);
}

public sealed class BuildingFeatureQueryService : IBuildingFeatureQueryService
{
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IRoomLayoutCache roomLayoutCache;
    private readonly IRoomEnvironmentEvaluator roomEnvironmentEvaluator;
    private readonly IRoomInspectionService roomInspectionService;
    private readonly IFacilitySynthesisRuntimeProvider synthesisProvider;
    private readonly IFacilityEvolutionRuntimeProvider evolutionProvider;

    public BuildingFeatureQueryService(
        IGridSystemProvider gridSystemProvider,
        IBuildingWorldQuery buildingWorld,
        IRoomLayoutCache roomLayoutCache,
        IRoomEnvironmentEvaluator roomEnvironmentEvaluator,
        IRoomInspectionService roomInspectionService,
        IFacilitySynthesisRuntimeProvider synthesisProvider,
        IFacilityEvolutionRuntimeProvider evolutionProvider)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.roomLayoutCache = roomLayoutCache
            ?? throw new ArgumentNullException(nameof(roomLayoutCache));
        this.roomEnvironmentEvaluator = roomEnvironmentEvaluator
            ?? throw new ArgumentNullException(nameof(roomEnvironmentEvaluator));
        this.roomInspectionService = roomInspectionService
            ?? throw new ArgumentNullException(nameof(roomInspectionService));
        this.synthesisProvider = synthesisProvider
            ?? throw new ArgumentNullException(nameof(synthesisProvider));
        this.evolutionProvider = evolutionProvider
            ?? throw new ArgumentNullException(nameof(evolutionProvider));
    }

    public BuildingFeatureSurfaceModel Capture()
    {
        BuildableObject[] facilities = buildingWorld.Buildings
            .Where(IsPlacedFacility)
            .OrderBy((facility) => facility.BuildingData.id)
            .ThenBy((facility) => facility.GetInstanceID())
            .ToArray();

        bool hasSynthesis = synthesisProvider.TryGetRuntime(out FacilitySynthesisRuntime synthesis);
        bool hasEvolution = evolutionProvider.TryGetRuntime(out FacilityEvolutionRuntime evolution);

        return new BuildingFeatureSurfaceModel
        {
            Rooms = CaptureRooms(facilities),
            SynthesisMaterials = hasSynthesis
                ? CaptureSynthesisMaterials(facilities, synthesis)
                : Array.Empty<BuildingFeatureSynthesisMaterialRow>(),
            SynthesisRecipes = hasSynthesis
                ? CaptureSynthesisRecipes(synthesis)
                : Array.Empty<BuildingFeatureSynthesisRecipeRow>(),
            EvolutionCandidates = hasEvolution
                ? CaptureEvolutionCandidates(facilities, evolution)
                : Array.Empty<BuildingFeatureEvolutionRow>(),
            HasSynthesisRuntime = hasSynthesis,
            HasEvolutionRuntime = hasEvolution
        };
    }

    private IReadOnlyList<BuildingFeatureRoomRow> CaptureRooms(
        IReadOnlyList<BuildableObject> facilities)
    {
        List<Grid> grids = facilities
            .Select((facility) => facility.Grid)
            .Where((grid) => grid != null)
            .Distinct()
            .ToList();
        if (gridSystemProvider.TryGetGrid(out Grid primaryGrid)
            && primaryGrid != null
            && !grids.Contains(primaryGrid))
        {
            grids.Insert(0, primaryGrid);
        }

        List<BuildingFeatureRoomRow> rows = new List<BuildingFeatureRoomRow>();
        foreach (Grid grid in grids)
        {
            foreach (RoomInstance room in roomLayoutCache.GetLayout(grid).Rooms
                .Where((candidate) => candidate != null && !candidate.IsSelfContained))
            {
                RoomEnvironmentSnapshot environment =
                    roomEnvironmentEvaluator.Evaluate(grid, room);
                bool selected = roomInspectionService.CurrentSnapshot != null
                    && roomInspectionService.CurrentSnapshot.Grid == grid
                    && ReferenceEquals(roomInspectionService.CurrentSnapshot.Room, room);
                string roleText = FormatRoles(room.Roles);
                string boundary = room.IsUsable
                    ? "폐쇄 + 출입문"
                    : room.IsClosed
                        ? "폐쇄 / 출입문 없음"
                        : "열린 경계";
                rows.Add(new BuildingFeatureRoomRow
                {
                    Grid = grid,
                    Room = room,
                    Title = $"방 {room.Id} / {roleText}",
                    Summary =
                        $"{boundary} / 면적 {room.Cells.Count} / 문 {room.Doors.Count} / 벽 {room.Walls.Count}\n"
                        + $"시설 {environment.Fixtures.Count} / 성향 {roleText}\n"
                        + $"넓이 {environment.Spaciousness:0} · 미관 {environment.Beauty:0} · "
                        + $"청결 {environment.Cleanliness:0} · 인상도 {environment.Impressiveness:0}",
                    Feedback = $"방 {room.Id} 성향: {roleText} / 인상도 {environment.Impressiveness:0}",
                    IsSelected = selected
                });
            }
        }

        return rows
            .OrderByDescending((row) => row.Room.IsUsable)
            .ThenByDescending((row) => row.Room.IsClosed)
            .ThenBy((row) => row.Room.Id)
            .ToArray();
    }

    private static IReadOnlyList<BuildingFeatureSynthesisMaterialRow>
        CaptureSynthesisMaterials(
            IReadOnlyList<BuildableObject> facilities,
            FacilitySynthesisRuntime runtime)
    {
        return facilities
            .Select((facility) => new BuildingFeatureSynthesisMaterialRow
            {
                Facility = facility,
                Title = GetBuildingName(facility),
                Summary =
                    $"시설 ID {facility.BuildingData.id} / "
                    + (runtime.SelectedMaterials.Contains(facility)
                        ? "합성 재료로 선택됨"
                        : "선택 가능"),
                IsSelected = runtime.SelectedMaterials.Contains(facility)
            })
            .ToArray();
    }

    private static IReadOnlyList<BuildingFeatureSynthesisRecipeRow>
        CaptureSynthesisRecipes(FacilitySynthesisRuntime runtime)
    {
        return runtime.VisibleRecipes
            .Where((recipe) => recipe != null)
            .Select((recipe) => new BuildingFeatureSynthesisRecipeRow
            {
                Recipe = recipe,
                Title = recipe.DisplayName,
                Summary =
                    $"{FormatSynthesisMaterials(recipe)} -> "
                    + $"{FacilityShopService.GetBuildingName(recipe.resultBuilding)}\n"
                    + recipe.description
            })
            .ToArray();
    }

    private static IReadOnlyList<BuildingFeatureEvolutionRow>
        CaptureEvolutionCandidates(
            IReadOnlyList<BuildableObject> facilities,
            FacilityEvolutionRuntime runtime)
    {
        List<BuildingFeatureEvolutionRow> rows =
            new List<BuildingFeatureEvolutionRow>();
        foreach (BuildableObject facility in facilities)
        {
            foreach (FacilityEvolutionCandidate candidate in runtime.GetCandidates(
                facility,
                includeRejected: true,
                requestLlmProposal: false))
            {
                string reason = candidate.Approved
                    ? "조건 충족"
                    : FirstText(
                        candidate.RejectedHintText,
                        candidate.Reason,
                        "조건 미충족");
                rows.Add(new BuildingFeatureEvolutionRow
                {
                    Facility = facility,
                    Recipe = candidate.Recipe,
                    Title = $"{GetBuildingName(facility)} -> {candidate.Recipe.DisplayName}",
                    Summary = $"{reason}\n{candidate.Recipe.description}",
                    IsApproved = candidate.Approved
                });
            }
        }

        return rows.ToArray();
    }

    private static bool IsPlacedFacility(BuildableObject facility)
    {
        return facility != null
            && !facility.isDestroy
            && facility.BuildingData != null
            && facility.Grid != null;
    }

    private static string GetBuildingName(BuildableObject facility)
    {
        return facility != null
            ? FacilityShopService.GetBuildingName(facility.BuildingData)
            : "시설";
    }

    private static string FormatSynthesisMaterials(FacilitySynthesisRecipeSO recipe)
    {
        if (recipe?.materialBuildings == null)
        {
            return "재료 없음";
        }

        string[] names = recipe.materialBuildings
            .Where((building) => building != null)
            .Select(FacilityShopService.GetBuildingName)
            .ToArray();
        return names.Length > 0 ? string.Join(" + ", names) : "재료 없음";
    }

    private static string FormatRoles(FacilityRole roles)
    {
        if (roles == FacilityRole.None)
        {
            return "중립";
        }

        string[] labels = FacilityRoleCatalog.All
            .Where((definition) => (roles & definition.Role) != 0)
            .Select((definition) => definition.RoomLabel)
            .ToArray();
        return labels.Length > 0 ? string.Join(" + ", labels) : roles.ToString();
    }

    private static string FirstText(params string[] values)
    {
        return values.FirstOrDefault((value) => !string.IsNullOrWhiteSpace(value))
            ?? string.Empty;
    }
}

public sealed class BuildingFeatureCommandService : IBuildingFeatureCommandService
{
    private readonly IRoomInspectionService roomInspectionService;
    private readonly IFacilitySynthesisRuntimeProvider synthesisProvider;
    private readonly IFacilityEvolutionRuntimeProvider evolutionProvider;

    public BuildingFeatureCommandService(
        IRoomInspectionService roomInspectionService,
        IFacilitySynthesisRuntimeProvider synthesisProvider,
        IFacilityEvolutionRuntimeProvider evolutionProvider)
    {
        this.roomInspectionService = roomInspectionService
            ?? throw new ArgumentNullException(nameof(roomInspectionService));
        this.synthesisProvider = synthesisProvider
            ?? throw new ArgumentNullException(nameof(synthesisProvider));
        this.evolutionProvider = evolutionProvider
            ?? throw new ArgumentNullException(nameof(evolutionProvider));
    }

    public BuildingFeatureCommandResult InspectRoom(
        Grid grid,
        RoomInstance room,
        string feedback)
    {
        bool shown = grid != null
            && room != null
            && roomInspectionService.ShowRoom(grid, room);
        return new BuildingFeatureCommandResult(
            shown,
            shown ? feedback : "현재 화면에서 이 방을 표시할 수 없습니다.");
    }

    public BuildingFeatureCommandResult ToggleSynthesisMaterial(
        BuildableObject facility)
    {
        if (!synthesisProvider.TryGetRuntime(out FacilitySynthesisRuntime runtime)
            || facility == null
            || facility.isDestroy)
        {
            return new BuildingFeatureCommandResult(
                false,
                "합성 재료를 변경할 수 없습니다.");
        }

        runtime.ToggleMaterialSelection(facility);
        return new BuildingFeatureCommandResult(
            true,
            $"합성 재료 변경: {FacilityShopService.GetBuildingName(facility.BuildingData)} / "
            + $"선택 {runtime.SelectedMaterials.Count}개");
    }

    public BuildingFeatureCommandResult ExecuteSynthesis(
        FacilitySynthesisRecipeSO recipe)
    {
        if (!synthesisProvider.TryGetRuntime(out FacilitySynthesisRuntime runtime))
        {
            return new BuildingFeatureCommandResult(
                false,
                "합성 시스템을 사용할 수 없습니다.");
        }

        bool success = runtime.TrySynthesizeSelected(
            recipe,
            out FacilitySynthesisResult result);
        return new BuildingFeatureCommandResult(
            success,
            $"합성 {(success ? "성공" : "실패")}: {result.Message}");
    }

    public BuildingFeatureCommandResult ExecuteEvolution(
        BuildableObject facility,
        FacilityEvolutionRecipeSO recipe)
    {
        if (!evolutionProvider.TryGetRuntime(out FacilityEvolutionRuntime runtime))
        {
            return new BuildingFeatureCommandResult(
                false,
                "시설 진화 시스템을 사용할 수 없습니다.");
        }

        bool success = runtime.TryEvolve(
            facility,
            recipe,
            out FacilityEvolutionResult result);
        return new BuildingFeatureCommandResult(
            success,
            $"진화 {(success ? "성공" : "실패")}: {result.Message}");
    }
}

public sealed class BuildingFeatureSurfacePresenter : IFeatureSurfaceTabPresenter
{
    private const float CompactCardHeight = 66f;
    private const float CardHeight = 86f;

    private readonly IBuildingFeatureQueryService queryService;
    private readonly IBuildingFeatureCommandService commandService;

    public BuildingFeatureSurfacePresenter(
        IBuildingFeatureQueryService queryService,
        IBuildingFeatureCommandService commandService)
    {
        this.queryService = queryService
            ?? throw new ArgumentNullException(nameof(queryService));
        this.commandService = commandService
            ?? throw new ArgumentNullException(nameof(commandService));
    }

    public TabId Id => TabId.Buildings;

    public void Present(IFeatureSurfaceView view)
    {
        if (view == null)
        {
            throw new ArgumentNullException(nameof(view));
        }

        BuildingFeatureSurfaceModel model = queryService.Capture();
        PresentRooms(view, model.Rooms);
        PresentSynthesis(view, model);
        PresentEvolution(view, model);
    }

    private void PresentRooms(
        IFeatureSurfaceView view,
        IReadOnlyList<BuildingFeatureRoomRow> rooms)
    {
        view.AddSection(
            "방 경계와 배치 성향",
            $"정식 방 {rooms.Count}개 / 폐쇄 {rooms.Count((row) => row.Room.IsClosed)}개 / "
            + $"사용 가능 {rooms.Count((row) => row.Room.IsUsable)}개");
        if (rooms.Count == 0)
        {
            view.AddLabel("벽과 문으로 구획된 방이 없습니다.", 18f, 44f);
            return;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            BuildingFeatureRoomRow row = rooms[i];
            view.AddDataCard(
                $"P1Action_RoomInspect_{i}",
                row.Title,
                row.Summary,
                row.IsSelected ? "선택됨" : "성향 확인",
                () => view.ShowFeedback(
                    commandService.InspectRoom(
                        row.Grid,
                        row.Room,
                        row.Feedback).Message),
                row.IsSelected ? 154f : 132f);
        }
    }

    private void PresentSynthesis(
        IFeatureSurfaceView view,
        BuildingFeatureSurfaceModel model)
    {
        if (!model.HasSynthesisRuntime)
        {
            view.AddSection(
                "시설 합성",
                "합성 런타임이 현재 씬에 없습니다.");
            return;
        }

        view.AddSection(
            "시설 합성",
            $"선택 재료 {model.SynthesisMaterials.Count((row) => row.IsSelected)}개 / "
            + $"배치 시설 {model.SynthesisMaterials.Count}개 / "
            + $"공개 조합 {model.SynthesisRecipes.Count}개");
        foreach (BuildingFeatureSynthesisMaterialRow row in model.SynthesisMaterials)
        {
            view.AddDataCard(
                $"P1Action_SynthesisMaterial_{row.Facility.GetInstanceID()}",
                row.Title,
                row.Summary,
                row.IsSelected ? "선택 해제" : "재료 선택",
                () => view.ShowFeedback(
                    commandService.ToggleSynthesisMaterial(row.Facility).Message),
                CompactCardHeight);
        }

        for (int i = 0; i < model.SynthesisRecipes.Count; i++)
        {
            BuildingFeatureSynthesisRecipeRow row = model.SynthesisRecipes[i];
            view.AddDataCard(
                $"P1Action_SynthesisExecute_{i}",
                row.Title,
                row.Summary,
                "합성",
                () => view.ShowFeedback(
                    commandService.ExecuteSynthesis(row.Recipe).Message),
                CardHeight);
        }
    }

    private void PresentEvolution(
        IFeatureSurfaceView view,
        BuildingFeatureSurfaceModel model)
    {
        if (!model.HasEvolutionRuntime)
        {
            view.AddSection(
                "시설 진화",
                "진화 런타임이 현재 씬에 없습니다.");
            return;
        }

        view.AddSection(
            "시설 진화",
            $"후보 {model.EvolutionCandidates.Count}개 / "
            + $"승인 {model.EvolutionCandidates.Count((row) => row.IsApproved)}개");
        if (model.EvolutionCandidates.Count == 0)
        {
            view.AddLabel("현재 배치 시설에서 확인할 진화 후보가 없습니다.", 18f, 40f);
        }

        for (int i = 0; i < model.EvolutionCandidates.Count; i++)
        {
            BuildingFeatureEvolutionRow row = model.EvolutionCandidates[i];
            view.AddDataCard(
                $"P1Action_EvolutionExecute_{i}",
                row.Title,
                row.Summary,
                row.IsApproved ? "진화" : "조건 확인",
                () => view.ShowFeedback(
                    commandService.ExecuteEvolution(
                        row.Facility,
                        row.Recipe).Message),
                CardHeight);
        }
    }
}
