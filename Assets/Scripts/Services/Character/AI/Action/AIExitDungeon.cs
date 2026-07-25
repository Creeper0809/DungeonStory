using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/AI/Action/ExitDungeon", order = 0)]
public class AIExitDungeon : AIActionSet
{
    private static readonly CharacterAiActionDescriptor ActionDescriptor = new CharacterAiActionDescriptor(
        CharacterAiBranch.ExitDungeon,
        "던전 나가기",
        CharacterAiActionTags.Exit);

    public override CharacterAiActionDescriptor Descriptor => ActionDescriptor;
    public override bool RequiresDestination => false;

    public override bool CanStart(CharacterActor actor)
    {
        if (actor == null || CharacterWorkRoleUtility.TryGetWork(actor, out _))
        {
            return false;
        }

        AbilityShopping shopping = null;
        actor.TryGetAbility(out shopping);
        return shopping != null
            && shopping.ShouldExitDungeon();
    }

    public override void Execute(CharacterActor actor)
    {
        AbilityShopping shopping = null;
        actor?.TryGetAbility(out shopping);
        shopping?.TryStealLooseItemBeforeExit();

        AbilityMove move = null;
        actor?.TryGetAbility(out move);
        if (move != null)
        {
            move.StartExitDungeon();
            return;
        }

        if (actor != null && actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = true;
        }
    }
}
