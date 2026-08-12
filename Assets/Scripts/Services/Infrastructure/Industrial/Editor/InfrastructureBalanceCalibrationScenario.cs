#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class InfrastructureBalanceCalibrationScenario
{
    private const string ReportPath =
        "Artifacts/QA/infrastructure-balance.txt";

    public static string RunAll()
    {
        BuildingSO[] buildings = AssetDatabase.FindAssets("t:BuildingSO")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(value => value != null)
            .GroupBy(value => value.id)
            .Select(group => group.First())
            .OrderBy(value => value.id)
            .ToArray();
        BuildingAutomationAbility[] automation = buildings
            .Select(value => value.GetAbility<BuildingAutomationAbility>())
            .Where(value => value != null)
            .ToArray();
        BuildingPowerProducerAbility[] producers = buildings
            .Select(value => value.GetAbility<BuildingPowerProducerAbility>())
            .Where(value => value != null)
            .ToArray();
        BuildingPowerConsumerAbility[] consumers = buildings
            .Select(value => value.GetAbility<BuildingPowerConsumerAbility>())
            .Where(value => value != null)
            .ToArray();
        BuildingPowerStorageAbility[] storage = buildings
            .Select(value => value.GetAbility<BuildingPowerStorageAbility>())
            .Where(value => value != null)
            .ToArray();

        Require(automation.Length > 0, "No automation-capable facility was authored.");
        Require(producers.Length > 0, "No power producer was authored.");
        Require(consumers.Length > 0, "No power consumer was authored.");
        Require(storage.Length > 0, "No power storage was authored.");

        foreach (BuildingAutomationAbility ability in automation)
        {
            Require(ability.maximumMode != AutomationMode.Manual,
                "An automation ability exposes only manual mode.");
            Require(ability.assistedPowerDemand > 0f,
                "Powered assist has no power cost.");
            Require(ability.assistedWorkMultiplier > 1f,
                "Powered assist does not improve work speed.");
            Require(ability.maintenancePerGameHour > 0f,
                "Automation has no maintenance burden.");
            if (ability.maximumMode == AutomationMode.Automatic)
            {
                Require(ability.automaticPowerDemand >= ability.assistedPowerDemand,
                    "Automatic mode costs less power than powered assist.");
                Require(ability.automaticWorkPerSecond > 0f,
                    "Automatic mode produces no work.");
                Require(ability.automaticQualityCap >= 0.5f
                        && ability.automaticQualityCap <= 0.9f,
                    "Automatic quality cap is outside the 0.50-0.90 trade-off band.");
            }
        }

        foreach (BuildingPowerProducerAbility producer in producers)
        {
            Require(producer.productionPerSecond > 0f,
                "A power producer has no output.");
            if (producer.requiresFuel)
            {
                Require(!string.IsNullOrWhiteSpace(producer.fuelItemId)
                        && producer.secondsPerFuel > 0f,
                    "A fueled generator has no concrete fuel contract.");
            }
        }
        foreach (BuildingPowerConsumerAbility consumer in consumers)
        {
            Require(consumer.demandPerSecond > 0f,
                "A power consumer has no demand.");
            Require(consumer.minimumSupplyFraction > 0f
                    && consumer.minimumSupplyFraction <= 1f,
                "A power consumer has an invalid minimum supply fraction.");
        }
        foreach (BuildingPowerStorageAbility battery in storage)
        {
            Require(battery.capacity > 0f && battery.transferPerSecond > 0f,
                "Power storage has no usable capacity or transfer rate.");
            Require(battery.efficiency >= 0.75f && battery.efficiency < 1f,
                "Power storage efficiency must preserve a visible 1-25% loss.");
        }

        IndustrialInfrastructureStressReport stress =
            IndustrialInfrastructureStressProbe.Run();
        string report =
            "INFRASTRUCTURE_BALANCE=PASS\n"
            + $"buildings={buildings.Length}\n"
            + $"automationFacilities={automation.Length}\n"
            + $"powerProducers={producers.Length}\n"
            + $"powerConsumers={consumers.Length}\n"
            + $"powerStorage={storage.Length}\n"
            + $"assistMultiplierRange={automation.Min(value => value.assistedWorkMultiplier):F2}-"
            + $"{automation.Max(value => value.assistedWorkMultiplier):F2}\n"
            + $"maintenancePerHourRange={automation.Min(value => value.maintenancePerGameHour):F2}-"
            + $"{automation.Max(value => value.maintenancePerGameHour):F2}\n"
            + $"utilityCells={stress.UtilityCellCount}\n"
            + $"payloadRoutes={stress.PayloadRouteCount}\n"
            + $"topologyMs={stress.TopologyMilliseconds:F1}\n"
            + $"routesMs={stress.RouteMilliseconds:F1}\n"
            + $"topologyAllocatedBytes={stress.TopologyAllocatedBytes}\n"
            + $"routeAllocatedBytes={stress.RouteAllocatedBytes}";
        string absolutePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            ReportPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? string.Empty);
        File.WriteAllText(absolutePath, report + Environment.NewLine);
        Debug.Log(report.Replace('\n', ';'));
        return report;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
