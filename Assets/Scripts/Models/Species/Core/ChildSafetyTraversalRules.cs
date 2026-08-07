public static class ChildSafetyTraversalRules
{
    public static bool CanTraverse(
        CharacterLifeStage lifeStage,
        GridMovementIntent movementIntent,
        bool apprenticeshipAuthorizationValid,
        WorldHazardLevel from,
        WorldHazardLevel to,
        out FailureCode failureCode)
    {
        failureCode = FailureCode.None;
        if (lifeStage >= CharacterLifeStage.Adult)
        {
            return true;
        }
        if (movementIntent is GridMovementIntent.Combat
            or GridMovementIntent.CombatSupply)
        {
            failureCode = FailureCode.ChildSafetyCombatForbidden;
            return false;
        }
        if (from != WorldHazardLevel.Safe
            && movementIntent == GridMovementIntent.EscapeHazard)
        {
            if (to < from)
            {
                return true;
            }

            failureCode = FailureCode.ChildSafetyHazardEscapeDirectionInvalid;
            return false;
        }
        if (to == WorldHazardLevel.Safe)
        {
            return true;
        }
        if (to == WorldHazardLevel.Forbidden
            || lifeStage != CharacterLifeStage.Adolescent)
        {
            failureCode = FailureCode.ChildSafetyWorkForbidden;
            return false;
        }
        if (movementIntent != GridMovementIntent.Apprenticeship
            || !apprenticeshipAuthorizationValid)
        {
            failureCode = FailureCode.ChildSafetyAuthorizationInvalid;
            return false;
        }

        return true;
    }
}
