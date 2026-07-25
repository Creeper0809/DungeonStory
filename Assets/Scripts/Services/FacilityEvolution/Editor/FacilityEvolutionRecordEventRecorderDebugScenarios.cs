using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

public static class FacilityEvolutionRecordEventRecorderDebugScenarios
{
    public static bool RunPlayModeSmoke(out string report)
    {
        report = string.Empty;
        if (!EditorApplication.isPlaying)
        {
            report = "PlayMode is required.";
            return false;
        }

        LifetimeScope scope = Object.FindFirstObjectByType<LifetimeScope>(FindObjectsInactive.Include);
        if (scope == null || scope.Container == null)
        {
            report = "No active LifetimeScope/container.";
            return false;
        }

        IFacilityEvolutionRecordEventRecorder recorder =
            scope.Container.Resolve<IFacilityEvolutionRecordEventRecorder>();
        IFacilityEvolutionRecordComponentService records =
            scope.Container.Resolve<IFacilityEvolutionRecordComponentService>();
        if (recorder == null || records == null)
        {
            report = $"Resolve failed. recorder={recorder != null}, records={records != null}";
            return false;
        }

        BuildingSO mealBuilding = Resources.LoadAll<BuildingSO>("SO/Building/P1")
            .FirstOrDefault((building) =>
                building != null
                && building.Facility != null
                && building.Facility.SupportsRole(FacilityRole.Meal));
        if (mealBuilding == null)
        {
            report = "No P1 meal facility BuildingSO.";
            return false;
        }

        GameObject facilityObject = new GameObject("FacilityEvolutionRecordSmokeFacility");
        GameObject runtimeObject = new GameObject("FacilityEvolutionRecordSmokeRuntime");
        BuildableObject facility = facilityObject.AddComponent<BuildableObject>();
        FacilityEvolutionRecordRuntime runtime = runtimeObject.AddComponent<FacilityEvolutionRecordRuntime>();
        try
        {
            facility.Initialization(mealBuilding, Vector2Int.zero);
            scope.Container.Inject(runtime);

            runtime.OnTriggerEvent(new FacilityVisitEvent(null, facility));
            runtime.OnTriggerEvent(new FacilityVisitEvent(null, facility));
            runtime.OnTriggerEvent(new FacilityVisitEvent(null, facility));
            runtime.OnTriggerEvent(new OperatingDayEndedEvent(1));
            runtime.OnTriggerEvent(new FacilityRevenueEvent(null, facility, 45));
            runtime.OnTriggerEvent(new FacilityStockConsumedEvent(null, facility, StockCategory.Food, 2));

            FacilityEvolutionRecord record = records.GetRecord(facility);
            bool visitCount = Mathf.Approximately(record.GetMetric(FacilityEvolutionTerms.VisitCount), 3f);
            bool revenue = Mathf.Approximately(record.GetMetric(FacilityEvolutionTerms.TotalRevenue), 45f);
            bool highValue = Mathf.Approximately(record.GetMetric(FacilityEvolutionTerms.HighValueTransactionCount), 1f);
            bool clean = record.GetToken(FacilityEvolutionTerms.CleanServiceStreak) >= 1;
            bool meat = record.GetToken(FacilityEvolutionTerms.HighMeatConsumption) >= 2;

            report = $"scope={scope.name}, recorderResolved=True, recordsResolved=True, runtimeInjected=True, building={mealBuilding.name}, visits={record.GetMetric(FacilityEvolutionTerms.VisitCount)}, revenue={record.GetMetric(FacilityEvolutionTerms.TotalRevenue)}, cleanToken={record.GetToken(FacilityEvolutionTerms.CleanServiceStreak)}, meatToken={record.GetToken(FacilityEvolutionTerms.HighMeatConsumption)}";
            return visitCount && revenue && highValue && clean && meat;
        }
        finally
        {
            Object.Destroy(runtimeObject);
            Object.Destroy(facilityObject);
        }
    }
}
