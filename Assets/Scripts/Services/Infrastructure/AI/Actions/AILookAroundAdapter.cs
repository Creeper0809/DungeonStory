using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/AI/Action/LookAround", order = 0)]
public class AILookAround : AIActionSet
{
    private static readonly CharacterAiActionDescriptor ActionDescriptor = new CharacterAiActionDescriptor(
        CharacterAiBranch.LookAround,
        "둘러보기",
        CharacterAiActionTags.Curiosity);

    public override CharacterAiActionDescriptor Descriptor => ActionDescriptor;
    [SerializeField] private float minWaitDuration = 0.5f;
    [SerializeField] private float maxWaitDuration = 1.2f;

    public override bool RequiresDestination => false;
    public override bool IsContinuous => true;

    public override bool CanContinue(
        CharacterActor actor,
        AIAction runningAction,
        out string stopReason)
    {
        stopReason = string.Empty;
        if (actor != null
            && actor.TryGetAbility(out AbilityMove move)
            && move.HasActiveMovementRoutineForDiagnostics)
        {
            return true;
        }

        stopReason = "The look-around movement or timer is no longer active.";
        return false;
    }

    public override bool CanStart(CharacterActor actor)
    {
        return CanUseVisitLookAround(actor);
    }

    public override bool CanStart(
        CharacterActor actor,
        in CharacterAiDecisionContext context)
    {
        return CanStart(actor);
    }

    public override float AdjustScore(CharacterActor actor, float baseScore)
    {
        return DungeonStory.AI.AILookAround.AdjustScore(baseScore);
    }

    public override void Execute(CharacterActor actor)
    {
        if (actor == null) return;

        if (CanUseVisitLookAround(actor)
            && actor.TryGetAbility(out AbilityShopping shopping))
        {
            shopping.RegisterLookAround();
        }

        float waitDuration = actor.Brain != null
            ? actor.Brain.NextRandom(minWaitDuration, maxWaitDuration)
            : minWaitDuration;
        DungeonStory.AI.AiActionRequest request =
            DungeonStory.AI.AILookAround.CreateRequest(
                CaptureLookAround(actor),
                waitDuration);
        waitDuration = request.Duration;
        actor.TryGetAbility(out AbilityMove move);
        if (move != null)
        {
            if (move.StartIdleWanderWithDeferredRecovery(waitDuration, 1, 6))
            {
                actor.Brain?.SetActionPhase("주변 둘러보기", detail: "가까운 곳을 돌아보는 중");
                return;
            }

            actor.Brain?.SetActionPhase("갈 곳 찾는 중", detail: "둘러볼 수 있는 칸을 다시 확인하는 중");
            move.StartWait(waitDuration);
            return;
        }

        if (actor.Brain != null)
        {
            AIBrain brain = actor.Brain;
            AIAction failedAction = brain.bestAction;
            brain.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.Unsupported,
                    "look-around-movement-ability-missing",
                    failedAction?.destination),
                requestImmediateReplan: false);
            brain.EndExpectedAction(
                failedAction,
                CharacterAiActionTerminalKind.Failed,
                clearFailures: false);
        }
    }

    public override IReadOnlyList<BuildableObject> GetDestinationCandidates(
        CharacterActor actor,
        GridPathSearchResult searchResult)
    {
        if (actor == null || !CanUseVisitLookAround(actor))
        {
            return new List<BuildableObject>();
        }

        Vector2Int currentPos = actor.GetNowXY();
        IReadOnlyList<BuildableObject> source = searchResult != null
            ? searchResult.GetAllReachableBuilding()
            : actor.WorldRegistry?.Buildings;
        if (source == null || source.Count == 0)
        {
            return new List<BuildableObject>();
        }

        Grid actorGrid = actor.Brain != null
            && actor.Brain.TryGetRuntimeGrid(out Grid runtimeGrid)
                ? runtimeGrid
                : null;
        List<BuildableObject> candidates = new List<BuildableObject>();
        foreach (BuildableObject building in source)
        {
            if (building == null
                || building.isDestroy
                || !building.IsGridMovement
                || (actorGrid != null && building.Grid != actorGrid)
                || ContainsPosition(building.buildPoses, currentPos))
            {
                continue;
            }

            candidates.Add(building);
        }

        Shuffle(actor, candidates);
        return candidates;
    }

    public override BuildableObject SelectDestination(
        CharacterActor actor,
        IReadOnlyList<BuildableObject> candidates)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return null;
        }

        return candidates[0];
    }

    private static bool ContainsPosition(
        IReadOnlyList<Vector2Int> positions,
        Vector2Int position)
    {
        if (positions == null)
        {
            return false;
        }

        for (int index = 0; index < positions.Count; index++)
        {
            if (positions[index] == position)
            {
                return true;
            }
        }

        return false;
    }

    public static bool CanUseVisitLookAround(CharacterActor actor)
    {
        return DungeonStory.AI.AILookAround.CanStart(
            CaptureLookAround(actor));
    }

    private static DungeonStory.AI.AiCharacterDecisionSnapshot CaptureLookAround(
        CharacterActor actor)
    {
        AbilityShopping shopping = null;
        bool hasShopping = actor != null
            && actor.TryGetAbility(out shopping);
        bool hasWork = CharacterWorkRoleUtility.TryGetWork(
            actor,
            out AbilityWork work);
        return new DungeonStory.AI.AiCharacterDecisionSnapshot(
            AiDecisionSceneSnapshotFactory.CaptureId(actor),
            actor != null,
            hasShopping: hasShopping,
            hasWorkRole: hasWork,
            isOffDuty: hasWork && work.IsOffDuty,
            canLookAround: hasShopping && shopping.CanLookAround());
    }

    private static void Shuffle(
        CharacterActor actor,
        IList<BuildableObject> candidates)
    {
        AIBrain brain = actor != null ? actor.Brain : null;
        if (brain == null || candidates == null)
        {
            return;
        }

        for (int index = candidates.Count - 1; index > 0; index--)
        {
            int swapIndex = brain.NextRandomIndex(index + 1);
            (candidates[index], candidates[swapIndex]) =
                (candidates[swapIndex], candidates[index]);
        }
    }
}
