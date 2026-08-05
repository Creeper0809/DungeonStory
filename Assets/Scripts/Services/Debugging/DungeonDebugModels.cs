using System;
using System.Collections.Generic;
using UnityEngine;

public enum DungeonDebugCategory
{
    Cheats,
    Spawn,
    Character,
    BuildingWork,
    SurvivalWildlife,
    EventsDefense,
    Overlay,
    History
}

public enum DungeonDebugTargetKind
{
    None,
    GridCell,
    Character,
    Building,
    ItemPile,
    Wildlife
}

public sealed class DungeonDebugTargetSelection
{
    public DungeonDebugTargetKind Kind { get; set; }
    public bool HasGridPosition { get; set; }
    public Vector2Int GridPosition { get; set; }
    public CharacterActor Character { get; set; }
    public BuildableObject Building { get; set; }
    public WorldItemStackSnapshot ItemStack { get; set; }
    public WildlifeActor Wildlife { get; set; }
    public UnityEngine.Object SourceObject { get; set; }

    public string Describe()
    {
        return Kind switch
        {
            DungeonDebugTargetKind.Character =>
                Character?.Identity?.DisplayName ?? "캐릭터 없음",
            DungeonDebugTargetKind.Building =>
                Building?.BuildingData?.objectName ?? "건물 없음",
            DungeonDebugTargetKind.ItemPile =>
                ItemStack != null
                    ? $"{ItemStack.DisplayName} x{ItemStack.Quantity} ({GridPosition.x}, {GridPosition.y})"
                    : $"아이템 더미 ({GridPosition.x}, {GridPosition.y})",
            DungeonDebugTargetKind.Wildlife =>
                Wildlife != null ? Wildlife.DisplayName : "야생동물 없음",
            DungeonDebugTargetKind.GridCell =>
                HasGridPosition ? $"칸 ({GridPosition.x}, {GridPosition.y})" : "칸 없음",
            _ => "전체"
        };
    }

    public bool Matches(DungeonDebugTargetKind required)
    {
        return required switch
        {
            DungeonDebugTargetKind.None => true,
            DungeonDebugTargetKind.GridCell => HasGridPosition,
            DungeonDebugTargetKind.Character => Character != null,
            DungeonDebugTargetKind.Building => Building != null,
            DungeonDebugTargetKind.ItemPile => HasGridPosition && ItemStack != null,
            DungeonDebugTargetKind.Wildlife => Wildlife != null,
            _ => false
        };
    }
}

public sealed class DungeonDebugExecutionContext
{
    public DungeonDebugTargetSelection Target { get; set; } = new DungeonDebugTargetSelection();
    public float NumericValue { get; set; } = 10f;
    public string TextValue { get; set; } = string.Empty;
    public bool RepeatRequested { get; set; }
}

public interface IDungeonDebugCommand
{
    string Id { get; }
    string DisplayName { get; }
    string Description { get; }
    DungeonDebugCategory Category { get; }
    DungeonDebugTargetKind TargetKind { get; }
    bool IsDangerous { get; }
    bool MutatesWorld { get; }
    float DefaultNumericValue { get; }
    DungeonDebugCommandResult Execute(DungeonDebugExecutionContext context);
}

public interface IDungeonDebugCommandProvider
{
    IEnumerable<IDungeonDebugCommand> GetCommands();
}

public interface IDungeonDebugCommandRegistry
{
    IReadOnlyList<IDungeonDebugCommand> Commands { get; }
    bool TryGet(string commandId, out IDungeonDebugCommand command);
    DungeonDebugCommandResult Execute(IDungeonDebugCommand command, DungeonDebugExecutionContext context);
}

public interface IDungeonDebugRuleQuery : IBuildingDamageRulePort
{
    bool IsExecutingCommand { get; }
    bool IsEnabled(DungeonDebugCheat cheat);
    bool ShouldFreezeNeed(CharacterCondition condition, float delta);
    bool ShouldBlockFriendlyDamage(CharacterActor actor);
    bool ShouldSkipCosts();
}

public interface IDungeonDebugRuleRuntime : IDungeonDebugRuleQuery
{
    void BeginCommandExecution();
    void EndCommandExecution();
}

public sealed class DisabledDungeonDebugRuleQuery : IDungeonDebugRuleQuery
{
    public static readonly DisabledDungeonDebugRuleQuery Instance = new();

    private DisabledDungeonDebugRuleQuery()
    {
    }

    public bool IsExecutingCommand => false;
    public bool IsEnabled(DungeonDebugCheat cheat) => false;
    public bool ShouldFreezeNeed(CharacterCondition condition, float delta) => false;
    public bool ShouldBlockFriendlyDamage(CharacterActor actor) => false;
    public bool ShouldBlockFacilityDamage(bool damaged) => false;
    public bool ShouldSkipCosts() => false;
}

public interface IDungeonDebugOverlayProvider
{
    DungeonDebugOverlayKind Kind { get; }
    void Render(DungeonDebugOverlayRenderContext context);
}

public sealed class DungeonDebugOverlayRenderContext
{
    public Grid Grid { get; set; }
    public Camera Camera { get; set; }
    public DungeonDebugOverlayScope Scope { get; set; }
    public DungeonDebugTargetSelection Selection { get; set; }
    public DungeonDebugOverlayRenderer Renderer { get; set; }
}
