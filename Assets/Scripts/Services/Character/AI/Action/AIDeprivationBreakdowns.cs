using UnityEngine;

public abstract class AIDeprivationBreakdownAction : AIActionSet
{
    public override bool RequiresDestination => false;
    public override bool IsContinuous => true;
    public override float MinimumDuration => 1f;
    public override bool AllowsSurvivalEmergencyInterrupt => false;
    protected abstract CharacterBreakdownKind BreakdownKind { get; }

    public override bool CanStart(CharacterActor actor)
    {
        return actor != null
            && actor.DeprivationRuntime?.HasBreakdownKind(actor, BreakdownKind) == true;
    }

    public override void Execute(CharacterActor actor)
    {
        actor?.DeprivationRuntime?.BeginBreakdownAction(actor, BreakdownKind);
    }
}

[CreateAssetMenu(menuName = "DungeonStory/AI/Breakdown/Desperate Relief", order = 0)]
public sealed class AIDesperateRelief : AIDeprivationBreakdownAction
{
    private static readonly CharacterAiActionDescriptor ActionDescriptor = new CharacterAiActionDescriptor(
        CharacterAiBranch.DeprivationBreakdown,
        "참지 못하고 배설",
        CharacterAiActionTags.SelfCare);
    public override CharacterAiActionDescriptor Descriptor => ActionDescriptor;
    protected override CharacterBreakdownKind BreakdownKind => CharacterBreakdownKind.DesperateRelief;
}

[CreateAssetMenu(menuName = "DungeonStory/AI/Breakdown/Desperate Drink", order = 1)]
public sealed class AIDesperateDrink : AIDeprivationBreakdownAction
{
    private static readonly CharacterAiActionDescriptor ActionDescriptor = new CharacterAiActionDescriptor(
        CharacterAiBranch.DeprivationBreakdown,
        "마실 것을 닥치는 대로 찾음",
        CharacterAiActionTags.SelfCare);
    public override CharacterAiActionDescriptor Descriptor => ActionDescriptor;
    protected override CharacterBreakdownKind BreakdownKind => CharacterBreakdownKind.DesperateDrink;
}

[CreateAssetMenu(menuName = "DungeonStory/AI/Breakdown/Desperate Eat", order = 2)]
public sealed class AIDesperateEat : AIDeprivationBreakdownAction
{
    private static readonly CharacterAiActionDescriptor ActionDescriptor = new CharacterAiActionDescriptor(
        CharacterAiBranch.DeprivationBreakdown,
        "먹을 수 있는 것을 사냥함",
        CharacterAiActionTags.SelfCare);
    public override CharacterAiActionDescriptor Descriptor => ActionDescriptor;
    protected override CharacterBreakdownKind BreakdownKind => CharacterBreakdownKind.DesperateEat;
}

[CreateAssetMenu(menuName = "DungeonStory/AI/Breakdown/Collapse", order = 3)]
public sealed class AICollapse : AIDeprivationBreakdownAction
{
    private static readonly CharacterAiActionDescriptor ActionDescriptor = new CharacterAiActionDescriptor(
        CharacterAiBranch.DeprivationBreakdown,
        "바닥에 쓰러짐",
        CharacterAiActionTags.SelfCare);
    public override CharacterAiActionDescriptor Descriptor => ActionDescriptor;
    protected override CharacterBreakdownKind BreakdownKind => CharacterBreakdownKind.Collapse;
}

[CreateAssetMenu(menuName = "DungeonStory/AI/Breakdown/Violent Impulse", order = 4)]
public sealed class AIViolentBreakdown : AIDeprivationBreakdownAction
{
    private static readonly CharacterAiActionDescriptor ActionDescriptor = new CharacterAiActionDescriptor(
        CharacterAiBranch.DeprivationBreakdown,
        "불안정한 폭력 충동",
        CharacterAiActionTags.SelfCare);
    public override CharacterAiActionDescriptor Descriptor => ActionDescriptor;
    protected override CharacterBreakdownKind BreakdownKind => CharacterBreakdownKind.ViolentImpulse;
}
