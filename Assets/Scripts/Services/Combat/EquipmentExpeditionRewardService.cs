using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct EquipmentExpeditionRewardRequest
{
    public EquipmentExpeditionRewardRequest(
        int runSeed,
        string eventKey,
        EquipmentExpeditionRewardKind kind,
        EquipmentEra era,
        string regionId,
        Vector2Int deliveryPosition)
    {
        RunSeed = runSeed;
        EventKey = eventKey?.Trim() ?? string.Empty;
        Kind = kind;
        Era = era;
        RegionId = regionId?.Trim() ?? string.Empty;
        DeliveryPosition = deliveryPosition;
    }
    public int RunSeed { get; }
    public string EventKey { get; }
    public EquipmentExpeditionRewardKind Kind { get; }
    public EquipmentEra Era { get; }
    public string RegionId { get; }
    public Vector2Int DeliveryPosition { get; }
}

public sealed class EquipmentExpeditionRewardResult
{
    public IReadOnlyList<EquipmentModuleInstance> Modules { get; set; } =
        Array.Empty<EquipmentModuleInstance>();
    public string LineageSealStackId { get; set; } = string.Empty;
}

public interface IEquipmentExpeditionRewardService
{
    EquipmentExpeditionRewardResult Resolve(EquipmentExpeditionRewardRequest request);
}

public sealed class EquipmentExpeditionRewardService : IEquipmentExpeditionRewardService
{
    private readonly ICombatEquipmentRuntime equipment;
    private readonly IEquipmentModuleCatalog modules;
    private readonly IWorldItemStackRuntime worldItems;

    public EquipmentExpeditionRewardService(
        ICombatEquipmentRuntime equipment,
        IEquipmentModuleCatalog modules,
        IWorldItemStackRuntime worldItems)
    {
        this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        this.modules = modules ?? throw new ArgumentNullException(nameof(modules));
        this.worldItems = worldItems ?? throw new ArgumentNullException(nameof(worldItems));
    }

    public EquipmentExpeditionRewardResult Resolve(
        EquipmentExpeditionRewardRequest request)
    {
        int count = PreviewModuleDropCount(request);
        EquipmentModuleDefinitionSO[] eligible = modules.All
            .Where(module => module != null && module.MinimumEra <= request.Era)
            .OrderBy(module => module.ModuleId, StringComparer.Ordinal)
            .ToArray();
        List<EquipmentModuleInstance> awarded = new List<EquipmentModuleInstance>();
        for (int index = 0; index < count && eligible.Length > 0; index++)
        {
            int selected = Mathf.FloorToInt(Roll01(request, 10 + index) * eligible.Length)
                % eligible.Length;
            int grade = Mathf.Clamp(
                1 + (int)request.Era + Mathf.FloorToInt(Roll01(request, 20 + index) * 2f),
                1,
                4);
            awarded.Add(equipment.CreateExpeditionModule(
                eligible[selected].ModuleId,
                grade,
                request.DeliveryPosition));
        }

        string sealStackId = string.Empty;
        if (request.Kind == EquipmentExpeditionRewardKind.RegionBoss
            && equipment.TryClaimRegionLineageSeal(request.RegionId))
        {
            if (!worldItems.SpawnUniqueItemAt(
                    EquipmentProgressionItemIds.LineageSeal,
                    request.DeliveryPosition,
                    WorldItemStackState.Loose,
                    $"lineage-seal:{request.RegionId}",
                    out sealStackId))
            {
                throw new InvalidOperationException(
                    $"Failed to materialize lineage seal for region '{request.RegionId}'.");
            }
        }
        return new EquipmentExpeditionRewardResult
        {
            Modules = awarded,
            LineageSealStackId = sealStackId
        };
    }

    public static int PreviewModuleDropCount(
        EquipmentExpeditionRewardRequest request)
    {
        return request.Kind switch
        {
            EquipmentExpeditionRewardKind.RegionBoss =>
                1 + (Roll01(request, 1) < 0.25f ? 1 : 0),
            EquipmentExpeditionRewardKind.EliteCombat =>
                Roll01(request, 1) < 0.08f ? 1 : 0,
            EquipmentExpeditionRewardKind.FacilityRaid =>
                Roll01(request, 1) < 0.15f ? 1 : 0,
            _ => 0
        };
    }

    private static float Roll01(
        EquipmentExpeditionRewardRequest request,
        int salt)
    {
        unchecked
        {
            uint hash = 2166136261u;
            string value = $"{request.RunSeed}|{request.EventKey}|{request.RegionId}|{(int)request.Kind}|{salt}";
            foreach (char character in value)
            {
                hash ^= character;
                hash *= 16777619u;
            }
            return (hash & 0x00ffffffu) / 16777216f;
        }
    }
}
