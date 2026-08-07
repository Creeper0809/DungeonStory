using System;
using UnityEngine;

public static class V19ChildSafetyDebugScenarios
{
    public static void RunAll()
    {
        Require(
            !ChildSafetyTraversalRules.CanTraverse(
                CharacterLifeStage.Child,
                GridMovementIntent.CombatSupply,
                false,
                WorldHazardLevel.Safe,
                WorldHazardLevel.Safe,
                out FailureCode combatFailure)
            && combatFailure == FailureCode.ChildSafetyCombatForbidden,
            "Child combat-supply traversal was not rejected.");
        Require(
            !ChildSafetyTraversalRules.CanTraverse(
                CharacterLifeStage.Child,
                GridMovementIntent.Apprenticeship,
                true,
                WorldHazardLevel.Safe,
                WorldHazardLevel.Restricted,
                out _),
            "A child entered a restricted apprenticeship zone.");
        Require(
            ChildSafetyTraversalRules.CanTraverse(
                CharacterLifeStage.Adolescent,
                GridMovementIntent.Apprenticeship,
                true,
                WorldHazardLevel.Safe,
                WorldHazardLevel.Restricted,
                out _),
            "An authorized adolescent apprenticeship was rejected.");
        Require(
            ChildSafetyTraversalRules.CanTraverse(
                CharacterLifeStage.Child,
                GridMovementIntent.EscapeHazard,
                false,
                WorldHazardLevel.Forbidden,
                WorldHazardLevel.Restricted,
                out _),
            "A child could not escape toward a strictly safer cell.");
        Require(
            !ChildSafetyTraversalRules.CanTraverse(
                CharacterLifeStage.Child,
                GridMovementIntent.EscapeHazard,
                false,
                WorldHazardLevel.Restricted,
                WorldHazardLevel.Restricted,
                out FailureCode escapeFailure)
            && escapeFailure
                == FailureCode.ChildSafetyHazardEscapeDirectionInvalid,
            "A child escaped laterally without reducing hazard severity.");
        Debug.Log("V19_CHILD_SAFETY_RULES=PASS; cases=5");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
