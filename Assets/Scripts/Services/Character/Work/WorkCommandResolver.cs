using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

public static class WorkCommandResolver
{
    public static bool TryResolveFacilityCommand(
        CharacterActor actor,
        BuildableObject target,
        out WorkTypeId workTypeId,
        out string failureReason)
    {
        workTypeId = default;
        if (!TryResolveLegacyFacilityCommand(
                actor,
                target,
                out FacilityWorkType workType,
                out failureReason))
        {
            return false;
        }

        if (!FacilityWorkTypeMap.TryGet(workType, out WorkTypeDefinition definition))
        {
            failureReason = "명령할 작업 종류를 해석할 수 없습니다.";
            return false;
        }

        workTypeId = definition.WorkTypeId;
        return true;
    }

    internal static bool TryResolveLegacyFacilityCommand(
        CharacterActor actor,
        BuildableObject target,
        out FacilityWorkType workType,
        out string failureReason)
    {
        workType = FacilityWorkType.None;
        failureReason = string.Empty;

        if (actor == null)
        {
            failureReason = "선택된 캐릭터가 없습니다";
            return false;
        }

        if (target == null)
        {
            failureReason = "우선 지정할 대상이 없습니다";
            return false;
        }

        if (target is ConstructionSite constructionSite)
        {
            if (!constructionSite.CanAssignWorker(
                    actor?.BuildingVisitor,
                    out failureReason))
            {
                return false;
            }

            workType = FacilityWorkType.Construct;
            return true;
        }

        if (target.isDestroy || target.Facility == null)
        {
            failureReason = "작업 가능한 시설이 아닙니다";
            return false;
        }

        FacilityWorkType supportedTypes = WildlifeButcherFacilityUtility.AddFallbackWorkTypes(
            target,
            target.Facility.supportedWorkTypes);
        supportedTypes = SurvivalFacilityUtility.AddFallbackWorkTypes(target, supportedTypes);
        if (supportedTypes == FacilityWorkType.None)
        {
            failureReason = "지원하는 작업이 없습니다";
            return false;
        }

        if (target.IsDamaged && TryUse(target, supportedTypes, FacilityWorkType.Repair, out workType))
        {
            return true;
        }

        if (NeedsRestock(target) && TryUse(target, supportedTypes, FacilityWorkType.Restock, out workType))
        {
            return true;
        }

        if (TryUse(target, supportedTypes, FacilityWorkType.Research, out workType))
        {
            return true;
        }

        int assignableTypeCount = 0;
        FacilityWorkType assignableType = FacilityWorkType.None;
        foreach (WorkTypeDefinition definition in
                 FacilityWorkTypeMap.Enumerate(supportedTypes))
        {
            FacilityWorkType legacyType = FacilityWorkTypeMap.GetRequired(definition);

            if (TryUse(target, supportedTypes, legacyType, out workType))
            {
                assignableType = workType;
                assignableTypeCount++;
            }
        }

        if (assignableTypeCount == 1)
        {
            workType = assignableType;
            return true;
        }

        if (assignableTypeCount > 1)
        {
            workType = FacilityWorkType.None;
            failureReason = "작업 종류를 명시해야 합니다";
            return false;
        }

        failureReason = "수행 가능한 작업이 없습니다";
        return false;
    }

    public static bool IsSuppressTarget(CharacterActor target, Predicate<CharacterActor> isRebellionTarget)
    {
        CharacterIdentity identity = target != null ? target.Identity : null;
        return target != null
            && ((identity != null && identity.CharacterType == CharacterType.Intruder)
                || (isRebellionTarget != null && isRebellionTarget(target))
                || (target.DeprivationQuery?.IsSuppressible(target) ?? false));
    }

    public static bool TryResolveSuppressCommand(
        CharacterActor actor,
        CharacterActor target,
        Predicate<CharacterActor> isRebellionTarget,
        out string failureReason)
    {
        failureReason = string.Empty;

        if (actor == null)
        {
            failureReason = "선택된 캐릭터가 없습니다";
            return false;
        }

        if (target == null)
        {
            failureReason = "제압할 대상이 없습니다";
            return false;
        }

        if (actor == target)
        {
            failureReason = "자기 자신은 제압할 수 없습니다";
            return false;
        }

        if (actor.IsDead)
        {
            failureReason = "선택된 캐릭터가 행동할 수 없습니다";
            return false;
        }

        if (target.IsDead)
        {
            failureReason = "이미 제압된 대상입니다";
            return false;
        }

        if (!IsSuppressTarget(target, isRebellionTarget))
        {
            failureReason = "제압 대상이 아닙니다";
            return false;
        }

        return true;
    }

    private static bool TryUse(
        BuildableObject target,
        FacilityWorkType supportedTypes,
        FacilityWorkType candidate,
        out FacilityWorkType result)
    {
        result = FacilityWorkType.None;
        if ((supportedTypes & candidate) == 0)
        {
            return false;
        }

        if (!FacilityWorkTypeMap.TryGet(candidate, out WorkTypeDefinition definition)
            || !target.CanAssignWork(definition.WorkTypeId, out _))
        {
            return false;
        }

        result = candidate;
        return true;
    }

    private static bool NeedsRestock(BuildableObject target)
    {
        if (target == null || target.Facility == null || target is not IStockedFacility stocked)
        {
            return false;
        }

        if (target.BuildingData.RequiresStockForUse() && !stocked.HasAvailableStock)
        {
            return true;
        }

        return target.GetInternalStockCapacity() > 0
            && stocked.CurrentStock <= target.GetRestockRequestThreshold();
    }
}
