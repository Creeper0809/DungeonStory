using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class FluidNodeState
{
    public float CleanWater;
    public float UnsafeWater;
    public float FoulWater;
    public float Wastewater;
    public float Blockage;
    public float Leak;
    public float ProcessorWork;
    public float ManualWaterReserve;
    public WaterContainerTransferMode TransferMode;
    public float TransferWork;
    public InfrastructureStatus TransferStatus;
}

public static class FluidNodeWaterRules
{
    public static WorldWaterQuality[] GetConsumptionOrder(
        WorldWaterQuality minimumQuality)
    {
        return minimumQuality switch
        {
            WorldWaterQuality.Clean => new[] { WorldWaterQuality.Clean },
            WorldWaterQuality.Unsafe => new[]
            {
                WorldWaterQuality.Unsafe,
                WorldWaterQuality.Clean
            },
            _ => new[]
            {
                WorldWaterQuality.Foul,
                WorldWaterQuality.Unsafe,
                WorldWaterQuality.Clean
            }
        };
    }

    public static float GetWater(
        FluidNodeState state,
        WorldWaterQuality quality)
    {
        return quality switch
        {
            WorldWaterQuality.Clean => state.CleanWater,
            WorldWaterQuality.Unsafe => state.UnsafeWater,
            _ => state.FoulWater
        };
    }

    public static void SetWater(
        FluidNodeState state,
        WorldWaterQuality quality,
        float value)
    {
        switch (quality)
        {
            case WorldWaterQuality.Clean:
                state.CleanWater = Mathf.Max(0f, value);
                break;
            case WorldWaterQuality.Unsafe:
                state.UnsafeWater = Mathf.Max(0f, value);
                break;
            default:
                state.FoulWater = Mathf.Max(0f, value);
                break;
        }
    }

    public static void Add(
        FluidNodeState state,
        WorldWaterQuality quality,
        float amount)
    {
        SetWater(
            state,
            quality,
            GetWater(state, quality) + Mathf.Max(0f, amount));
    }

    public static void Remove(
        FluidNodeState state,
        WorldWaterQuality preferredQuality,
        float amount)
    {
        float remaining = Mathf.Max(0f, amount);
        foreach (WorldWaterQuality quality in GetConsumptionOrder(
                     preferredQuality))
        {
            float removed = Mathf.Min(GetWater(state, quality), remaining);
            SetWater(state, quality, GetWater(state, quality) - removed);
            remaining -= removed;
            if (remaining <= 0.0001f)
            {
                break;
            }
        }
    }

    public static void ClampToCapacity(FluidNodeState state, float capacity)
    {
        float total = state.CleanWater + state.UnsafeWater + state.FoulWater;
        if (total <= capacity || total <= 0f)
        {
            return;
        }

        float multiplier = capacity / total;
        state.CleanWater *= multiplier;
        state.UnsafeWater *= multiplier;
        state.FoulWater *= multiplier;
    }
}

public static class FluidNodeStateExtensions
{
    public static float FaultEquivalent(this FluidNodeState state)
    {
        return state == null ? 0f : Mathf.Clamp01(state.Leak / 200f);
    }
}
