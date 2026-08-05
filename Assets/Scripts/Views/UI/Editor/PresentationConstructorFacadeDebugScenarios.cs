using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PresentationConstructorFacadeDebugScenarios
{
    private const int DependencyLimit = 8;

    [MenuItem("Dungeon Story/QA/Architecture/Verify Presentation Constructor Facades")]
    public static void Verify()
    {
        Type[] ownedTypes =
        {
            typeof(CharacterSurgeryWindowService),
            typeof(SurgeryClinicalContext),
            typeof(SurgeryExecutionContext),
            typeof(SurgerySubjectWorldContext),
            typeof(OperationsFeatureQueryService),
            typeof(OperationsSceneContext),
            typeof(OperationsWorldContext),
            typeof(OperationsStaffContext),
            typeof(WarehouseFeatureQueryService),
            typeof(WarehouseFeatureSessionContext),
            typeof(WarehouseFeatureWorldContext),
            typeof(WarehouseFeatureEconomyContext),
            typeof(DefenseFeatureQueryService),
            typeof(DefenseThreatContext),
            typeof(DefenseOperationsContext),
            typeof(DefenseFacilityContext),
            typeof(DungeonSaveUiController),
            typeof(DungeonSaveUiPresentationContext),
            typeof(DungeonSaveUiSessionContext),
            typeof(DungeonSaveUiActionContext),
            typeof(TreasuryResourceHudController),
            typeof(TreasuryHudPresentationContext),
            typeof(TreasuryHudEconomyContext),
            typeof(TreasuryHudContractContext),
            typeof(IndustrialFeatureSurfacePresenter),
            typeof(IndustrialPowerFluidContext),
            typeof(IndustrialTransportAutomationContext),
            typeof(IndustrialPresentationContext),
            typeof(ProductionBuildingPanelPresenter),
            typeof(ProductionPanelOrderContext),
            typeof(ProductionPanelFacilityContext),
            typeof(ProductionPanelEnvironmentContext),
            typeof(WarehouseFeatureCommandService),
            typeof(WarehouseCommandSessionContext),
            typeof(WarehouseCommandWorldContext),
            typeof(WarehouseCommandPlanningContext)
        };
        string[] violations = ownedTypes
            .SelectMany(type => type.GetConstructors()
                .Select(constructor => new
                {
                    Type = type,
                    Count = constructor.GetParameters().Length
                }))
            .Where(entry => entry.Count > DependencyLimit)
            .Select(entry => $"{entry.Type.Name}:{entry.Count}")
            .ToArray();
        if (violations.Length > 0)
        {
            throw new InvalidOperationException(
                "Presentation constructor dependency limit exceeded: "
                + string.Join(", ", violations));
        }

        RequireNullGuard(
            () => new CharacterSurgeryWindowService(null, null, null, null),
            nameof(CharacterSurgeryWindowService));
        RequireNullGuard(
            () => new OperationsFeatureQueryService(null, null, null, null),
            nameof(OperationsFeatureQueryService));
        RequireNullGuard(
            () => new WarehouseFeatureQueryService(null, null, null),
            nameof(WarehouseFeatureQueryService));
        RequireNullGuard(
            () => new DefenseFeatureQueryService(null, null, null, null),
            nameof(DefenseFeatureQueryService));
        RequireNullGuard(
            () => new DungeonSaveUiController(null, null, null),
            nameof(DungeonSaveUiController));
        RequireNullGuard(
            () => new TreasuryResourceHudController(null, null, null),
            nameof(TreasuryResourceHudController));
        RequireNullGuard(
            () => new IndustrialFeatureSurfacePresenter(null, null, null),
            nameof(IndustrialFeatureSurfacePresenter));
        RequireNullGuard(
            () => new ProductionBuildingPanelPresenter(null, null, null),
            nameof(ProductionBuildingPanelPresenter));
        RequireNullGuard(
            () => new WarehouseFeatureCommandService(null, null, null),
            nameof(WarehouseFeatureCommandService));

        Debug.Log(
            "[PresentationConstructorFacadeDebugScenarios] PASS "
            + "CharacterSurgeryWindowService=4, "
            + "OperationsFeatureQueryService=4, "
            + "WarehouseFeatureQueryService=3, "
            + "DefenseFeatureQueryService=4, "
            + "DungeonSaveUiController=3, "
            + "TreasuryResourceHudController=3, "
            + "IndustrialFeatureSurfacePresenter=3, "
            + "ProductionBuildingPanelPresenter=3, "
            + "WarehouseFeatureCommandService=3");
    }

    private static void RequireNullGuard(Action create, string typeName)
    {
        try
        {
            create();
        }
        catch (ArgumentNullException)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{typeName} accepted missing required dependencies.");
    }
}
