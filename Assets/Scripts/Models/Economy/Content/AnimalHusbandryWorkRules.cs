using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class AnimalHusbandryWorkRules
{
    public static AnimalGrowthStage GetGrowthStage(
        HusbandryAnimalState state,
        WildlifeHusbandryProfile profile)
    {
        if (state.AgeDays < profile.AdultAgeDays)
        {
            return AnimalGrowthStage.Juvenile;
        }

        return state.AgeDays >= profile.MaximumAgeDays * 0.8f
            ? AnimalGrowthStage.Elder
            : AnimalGrowthStage.Adult;
    }

    public static int GetWorkPriority(HusbandryAnimalState state)
    {
        return ResolveWorkKind(state) switch
        {
            AnimalHusbandryWorkKind.Slaughter => 100,
            AnimalHusbandryWorkKind.CollectProduct => 80,
            AnimalHusbandryWorkKind.CollectManure => 70,
            AnimalHusbandryWorkKind.Tame => 60,
            _ => 0
        };
    }

    public static AnimalHusbandryWorkKind ResolveWorkKind(
        HusbandryAnimalState state)
    {
        if (state.SlaughterDesignated)
        {
            return AnimalHusbandryWorkKind.Slaughter;
        }

        if (state.Products?.Any(product =>
                product != null && product.ReadyCycles > 0) == true)
        {
            return AnimalHusbandryWorkKind.CollectProduct;
        }

        if (state.ReadyManureCycles > 0)
        {
            return AnimalHusbandryWorkKind.CollectManure;
        }

        return !state.Tamed
            ? AnimalHusbandryWorkKind.Tame
            : AnimalHusbandryWorkKind.None;
    }

    public static float GetCompletedWork(
        HusbandryAnimalState state,
        AnimalHusbandryWorkKind kind,
        float required)
    {
        return kind switch
        {
            AnimalHusbandryWorkKind.Tame =>
                state.TamingProgress * required,
            _ => Mathf.Clamp(state.PendingWorkCompleted, 0f, required)
        };
    }

    public static void PreparePendingWork(
        HusbandryAnimalState state,
        AnimalHusbandryWorkKind kind)
    {
        ItemDefinitionId productId = kind == AnimalHusbandryWorkKind.CollectProduct
            ? state.Products?.FirstOrDefault(product =>
                product != null && product.ReadyCycles > 0)?.ItemId ?? default
            : default;
        if (state.PendingWorkKind == kind
            && state.PendingProductItemId.Equals(productId))
        {
            return;
        }

        state.PendingWorkKind = kind;
        state.PendingProductItemId = productId;
        state.PendingWorkCompleted = 0f;
    }

    public static void ResetPendingWork(HusbandryAnimalState state)
    {
        state.PendingWorkKind = AnimalHusbandryWorkKind.None;
        state.PendingProductItemId = default;
        state.PendingWorkCompleted = 0f;
    }

    public static AnimalHusbandryWorkSnapshot Unavailable(
        AnimalHusbandryFailure failure)
    {
        return new AnimalHusbandryWorkSnapshot(
            false,
            default,
            AnimalHusbandryWorkKind.None,
            1f,
            0f,
            failure);
    }

    public static void SetStatus(
        HusbandryAnimalState state,
        AnimalHusbandryStatusCode statusCode,
        params string[] parameters)
    {
        state.StatusCode = statusCode;
        state.StatusParameters = parameters?.ToList() ?? new System.Collections.Generic.List<string>();
    }

    public static uint StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            string source = value ?? string.Empty;
            for (int index = 0; index < source.Length; index++)
            {
                hash ^= source[index];
                hash *= 16777619;
            }

            return hash;
        }
    }
}
