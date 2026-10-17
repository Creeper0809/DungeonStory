#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Exercises the actual action commit and broker, not a priority mock or movement shortcut.
public static class CharacterAiCommittedPathDebugScenarios
{
    public static void RunBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException("A new committed-path evidence directory is required.");
        Directory.CreateDirectory(output);
        JObject report = new()
        {
            ["testId"] = "selected-action-path-budget-progress",
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false,
            ["actualMovementTest"] = false
        };
        JArray cases = new();
        report["cases"] = cases;
        try
        {
            VerifyCommittedProgress(cases, 1, double.PositiveInfinity);
            VerifyCommittedProgress(cases, 8, double.Epsilon);
            VerifyCommittedProgress(cases, 1, double.PositiveInfinity, throughBrain: true);
            VerifyCommittedProgress(cases, 8, double.Epsilon, throughBrain: true);
            Require(GridFoundationDebugScenarios.RunAll(false), "Grid foundation regression failed.");
            report["gridFoundationPassed"] = true;
            report["passed"] = true;
        }
        catch (Exception error)
        {
            report["passed"] = false;
            report["failure"] = error.ToString();
            throw;
        }
        finally
        {
            File.WriteAllText(Path.Combine(output, "report.json"), report.ToString(Formatting.Indented));
        }
    }

    private static void VerifyCommittedProgress(JArray cases, int actorCount, double timeBudget, bool throughBrain = false)
    {
        using CustomerAiDebugScenarios.CustomerAiScenarioWorld world = new();
        TestClock clock = new();
        GridPathSearchBroker broker = new(clock, null, null, null);
        BuildableObject target = world.Place("D04_배식카운터", new Vector2Int(30, 0), gameClock: clock);
        CharacterActor[] actors = new CharacterActor[actorCount];
        AIAction[] actions = new AIAction[actorCount];
        bool[] completed = new bool[actorCount];
        JObject row = new()
        {
            ["actorCount"] = actorCount,
            ["throughBrainCommit"] = throughBrain,
            ["ordinarySearchBudget"] = 1,
            ["forceElapsedBudgetExhaustion"] = !double.IsPositiveInfinity(timeBudget)
        };
        JArray frames = new();
        row["frames"] = frames;
        cases.Add(row);
        for (int index = 0; index < actorCount; index++)
        {
            actors[index] = world.CreateCustomer("Slime", new Vector2Int(index + 2, 0),
                25f, 100f, 100f, 100f, clock);
            CharacterAiEditorTestDependencies.Inject(actors[index].gameObject, scheduling: null,
                gameClock: clock, pathSearchBroker: broker, frameWorkBudget: null);
            actions[index] = actors[index].Brain.availableActions.Single(action => action.actionset is AIEat);
        }
        try
        {
            broker.BeginFrame(0, true);
            Require(!actions[0].SetResolvedDestinationWithFailure(actors[0], target, out AIActionFailure probeFailure)
                && probeFailure.Kind == AIActionFailureKind.PathSearchDeferred
                && broker.SearchesThisFrame == 0 && !actions[0].HasReservation,
                "An ordinary feasibility probe bypassed its budget or acquired a reservation.");
            row["ordinaryProbeRespectsBudget"] = true;
            // Stable topology/actor positions: the only competing work is one ordinary
            // exact search before every commit attempt, as in a decision's guard/scoring.
            for (int frame = 0; frame < 32 && completed.Any(value => !value); frame++)
            {
                clock.FrameCountValue = frame + 1;
                broker.BeginFrame(1, true, timeBudget);
                Require(broker.RequestMovePathTo(world.Grid, new Vector2Int(2, 0),
                    new Vector2Int(3, 0), out _) == GridPathRequestStatus.Reachable,
                    "Competing ordinary path did not consume the frame's first search.");
                Require(broker.SearchesThisFrame == 1, "Competing search was not charged.");
                for (int index = 0; index < actorCount; index++)
                {
                    if (completed[index]) continue;
                    AIActionFailure failure;
                    bool prepared = throughBrain
                        ? actors[index].Brain.TryCommitActionCandidate(new CharacterAiActionCandidate(
                            actions[index], 1f, AIActionFailure.None, "controlled-winner", target), out failure)
                        : actions[index].SetResolvedDestinationWithFailure(
                            actors[index], target, out failure, GridPathSearchPriority.Urgent);
                    if (!prepared)
                    {
                        Require(failure.Kind == AIActionFailureKind.PathSearchDeferred,
                            "Unexpected commit rejection: " + failure);
                        Require(actions[index].planKind == AIActionPlanKind.None
                            && !actions[index].HasReservation, "Deferred commit acquired a plan/reservation.");
                        Require(!throughBrain || actors[index].Brain.bestAction == null,
                            "Deferred winner was selected before path preparation succeeded.");
                        continue;
                    }
                    Require(actions[index].planKind == AIActionPlanKind.MovePath
                        && actions[index].pathSteps.Count > 0
                        && actions[index].pathSteps[0].From == actors[index].GetNowXY()
                        && target.ContainsGridPosition(actions[index].pathSteps.Last().To),
                        "Commit did not produce the actual destination path.");
                    completed[index] = true;
                    Require(!throughBrain || ReferenceEquals(actors[index].Brain.bestAction, actions[index]),
                        "Successful winner was not selected by the actual brain committer.");
                    actions[index].ReleaseReservation(actors[index]);
                }
                Require(broker.SearchesThisFrame <= 1 + GridPathSearchBroker.MaximumUrgentContinuationOverdraft
                    && broker.UrgentOverdraftSearchesThisFrame <= GridPathSearchBroker.MaximumUrgentContinuationOverdraft,
                    "Committed paths exceeded the existing broker hard cap.");
                frames.Add(new JObject { ["frame"] = frame, ["searches"] = broker.SearchesThisFrame,
                    ["overdraft"] = broker.UrgentOverdraftSearchesThisFrame,
                    ["deferrals"] = broker.BudgetDeferralsThisFrame,
                    ["completed"] = completed.Count(value => value) });
            }
            Require(completed.All(value => value),
                "Selected action paths were starved behind ordinary scans for 32 consecutive frames.");
            row["passed"] = true;
        }
        finally
        {
            for (int index = 0; index < actorCount; index++)
                actions[index]?.ReleaseReservation(actors[index]);
            broker.Clear();
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class TestClock : IGameClock
    {
        public float DeltaTime => 1f / 60f;
        public float Time => FrameCount * DeltaTime;
        public int FrameCount => FrameCountValue;
        public bool IsPaused => false;
        public int FrameCountValue { get; set; }
    }
}
#endif
