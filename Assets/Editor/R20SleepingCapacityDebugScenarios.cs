using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class R20SleepingCapacityDebugScenarios
{
    private static readonly string[] BedAssetPaths =
    {
        "Assets/Resources/SO/Building/Modular/R01_간이침대.asset",
        "Assets/Resources/SO/Building/Modular/R02_정식침대.asset",
        "Assets/Resources/SO/Building/Modular/R03_이층침대.asset"
    };

    private const string RestSofaAssetPath =
        "Assets/Resources/SO/Building/Modular/R04_휴식용소파.asset";

    [MenuItem("DungeonStory/QA/R20 Validate Sleeping Capacity")]
    public static void RunAll()
    {
        Require(!Application.isPlaying,
            "R20 sleeping-capacity verification must run before Play mode.");

        BuildingSO[] beds = BedAssetPaths
            .Select(LoadRequired)
            .ToArray();
        BuildingSO restSofa = LoadRequired(RestSofaAssetPath);
        SourceSnapshot[] snapshots = beds
            .Append(restSofa)
            .Select(SourceSnapshot.Capture)
            .ToArray();

        try
        {
            VerifyCurrentBeds(beds);
            VerifyRestSofaExcluded(restSofa);
            VerifyTypedLodgingAuthority(beds[0]);
            Debug.Log(
                "R20_SLEEPING_CAPACITY=PASS; current beds 1+1+2; "
                + "Rest sofa excluded; cloned ID and code independent; "
                + "non-lodging, invalid and destroyed excluded; repair retry; "
                + "source JSON and dirty state unchanged");
        }
        finally
        {
            foreach (SourceSnapshot snapshot in snapshots)
                snapshot.RequireUnchanged();
        }
    }

    private static void VerifyCurrentBeds(IReadOnlyList<BuildingSO> beds)
    {
        int[] expectedCapacities = { 1, 1, 2 };
        using RuntimeFixture fixture = new RuntimeFixture();
        var liveBeds = new List<BuildableObject>();
        for (int index = 0; index < beds.Count; index++)
        {
            BuildingSO bed = beds[index];
            BuildingServiceHubAbility hub = bed.GetServiceHubAbility();
            Require(hub != null
                    && Enum.IsDefined(typeof(ServiceCategory), hub.serviceCategory)
                    && hub.serviceCategory == ServiceCategory.Lodging,
                $"Current bed '{bed.name}' lacks a valid lodging hub.");
            Require(bed.Facility?.capacity == expectedCapacities[index],
                $"Current bed '{bed.name}' capacity changed from "
                + $"{expectedCapacities[index]}.");
            liveBeds.Add(fixture.CreateBuilding(bed));
        }

        Require(CaptureSleepingSlots(liveBeds) == 4,
            "The three current bed definitions did not preserve four sleeping slots.");
    }

    private static void VerifyRestSofaExcluded(BuildingSO restSofa)
    {
        BuildingEvolutionAbility evolution =
            restSofa.GetAbility<BuildingEvolutionAbility>();
        Require(evolution?.settings?.tags?.Contains(
                "Rest",
                StringComparer.Ordinal) == true,
            "The ordinary Rest fixture is no longer a Rest facility.");

        using RuntimeFixture fixture = new RuntimeFixture();
        BuildableObject sofa = fixture.CreateBuilding(restSofa);
        Require(CaptureSleepingSlots(new[] { sofa }) == 0,
            "An ordinary Rest facility without a lodging hub added sleeping slots.");
    }

    private static void VerifyTypedLodgingAuthority(BuildingSO sourceBed)
    {
        BuildingSO clone = Object.Instantiate(sourceBed);
        clone.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            clone.id = 902011;
            BuildingFacilityPartAbility facilityPart =
                clone.GetAbility<BuildingFacilityPartAbility>();
            Require(facilityPart != null,
                "The cloned bed lacks its facility-part ability.");
            facilityPart.code = "R20-CLONED-LODGING";

            BuildingServiceHubAbility hub =
                clone.GetAbility<BuildingServiceHubAbility>();
            Require(hub != null && hub.IsValid,
                "The cloned bed lacks a valid service hub.");
            string originalHubTag = hub.serviceHubTag;

            using RuntimeFixture fixture = new RuntimeFixture();
            BuildableObject building = fixture.CreateBuilding(clone);
            int expectedCapacity = Math.Max(1, clone.Facility?.capacity ?? 1);
            Require(CaptureSleepingSlots(new[] { building }) == expectedCapacity,
                "A lodging clone stopped counting after its numeric ID and code changed.");

            hub.serviceCategory = ServiceCategory.Dining;
            Require(CaptureSleepingSlots(new[] { building }) == 0,
                "A non-lodging service hub added sleeping slots.");

            hub.serviceCategory = (ServiceCategory)int.MaxValue;
            Require(CaptureSleepingSlots(new[] { building }) == 0,
                "A service hub with an invalid category added sleeping slots.");

            hub.serviceCategory = ServiceCategory.Lodging;
            hub.serviceHubTag = string.Empty;
            Require(CaptureSleepingSlots(new[] { building }) == 0,
                "An invalid lodging hub added sleeping slots.");

            hub.serviceHubTag = originalHubTag;
            building.isDestroy = true;
            Require(CaptureSleepingSlots(new[] { building }) == 0,
                "A destroyed lodging building added sleeping slots.");

            building.isDestroy = false;
            Require(CaptureSleepingSlots(new[] { building }) == expectedCapacity,
                "A repaired lodging building did not pass the capture retry.");
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }
    }

    private static int CaptureSleepingSlots(
        IReadOnlyList<BuildableObject> buildings)
    {
        ICharacterWorldQuery characterQuery = Port<ICharacterWorldQuery>(
            (method, _) => method.Name switch
            {
                "get_CharacterVersion" => 0,
                "get_Characters" => Array.Empty<CharacterActor>(),
                _ => Unexpected(method)
            });
        IBuildingWorldQuery buildingQuery = Port<IBuildingWorldQuery>(
            (method, _) => method.Name switch
            {
                "get_BuildingVersion" => 0,
                "get_Buildings" => buildings,
                _ => Unexpected(method)
            });
        ISurvivalFoodQuery survival = Port<ISurvivalFoodQuery>(
            (method, _) => method.Name == "GetOverview"
                ? default(SurvivalFoodOverview)
                : Unexpected(method));
        ISettlementAlertService alerts = Port<ISettlementAlertService>(
            (method, _) => method.Name == "Capture"
                ? default(SettlementAlertSnapshot)
                : Unexpected(method));
        ISettlementLaborAccountingService labor =
            Port<ISettlementLaborAccountingService>(
                (method, _) => method.Name == "Capture"
                    ? default(SettlementLaborAccountingSnapshot)
                    : Unexpected(method));

        var runtime = new SettlementPopulationCapacityRuntime(
            characterQuery,
            buildingQuery,
            survival,
            alerts,
            labor);
        return runtime.CapturePopulationCapacity().SleepingSlotCount;
    }

    private static BuildingSO LoadRequired(string path)
    {
        BuildingSO asset = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
        return asset != null
            ? asset
            : throw new InvalidOperationException(
                $"Missing R20 sleeping-capacity fixture asset at '{path}'.");
    }

    private static T Port<T>(
        Func<MethodInfo, object[], object> invoke) where T : class
    {
        T value = DispatchProxy.Create<T, StrictPort>();
        ((StrictPort)(object)value).InvokeMethod = invoke
            ?? throw new ArgumentNullException(nameof(invoke));
        return value;
    }

    private static object Unexpected(MethodInfo method) =>
        throw new InvalidOperationException(
            "Unexpected R20 fixture call: " + method.Name);

    public class StrictPort : DispatchProxy
    {
        public Func<MethodInfo, object[], object> InvokeMethod;

        protected override object Invoke(MethodInfo method, object[] args) =>
            InvokeMethod(method, args);
    }

    private sealed class RuntimeFixture : IDisposable
    {
        private readonly List<GameObject> hosts = new List<GameObject>();

        internal BuildableObject CreateBuilding(BuildingSO definition)
        {
            var host = new GameObject("R20SleepingCapacityFixture")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            hosts.Add(host);
            BuildableObject building = host.AddComponent<BuildableObject>();
            SetBuildingData(building, definition);
            return building;
        }

        public void Dispose()
        {
            for (int index = hosts.Count - 1; index >= 0; index--)
                Object.DestroyImmediate(hosts[index]);
        }
    }

    private sealed class SourceSnapshot
    {
        private readonly BuildingSO source;
        private readonly string json;
        private readonly bool dirty;

        private SourceSnapshot(BuildingSO source)
        {
            this.source = source;
            json = EditorJsonUtility.ToJson(source);
            dirty = EditorUtility.IsDirty(source);
        }

        internal static SourceSnapshot Capture(BuildingSO source) =>
            new SourceSnapshot(
                source ?? throw new ArgumentNullException(nameof(source)));

        internal void RequireUnchanged()
        {
            Require(EditorJsonUtility.ToJson(source) == json,
                $"R20 sleeping fixture changed source asset '{source.name}'.");
            Require(EditorUtility.IsDirty(source) == dirty,
                $"R20 sleeping fixture changed dirty state for '{source.name}'.");
        }
    }

    private static void SetBuildingData(
        BuildableObject building,
        BuildingSO definition)
    {
        PropertyInfo property = typeof(BuildableObject).GetProperty(
            nameof(BuildableObject.BuildingData),
            BindingFlags.Instance | BindingFlags.Public);
        MethodInfo setter = property?.GetSetMethod(true);
        if (setter == null)
        {
            throw new InvalidOperationException(
                "Missing BuildableObject.BuildingData fixture setter.");
        }

        setter.Invoke(building, new object[] { definition });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
