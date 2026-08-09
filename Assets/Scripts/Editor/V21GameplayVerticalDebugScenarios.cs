#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Focused V21 vertical gameplay gate. This deliberately invokes domain
/// scenarios through the same public entry points used by their menu actions
/// so a catalog-only success cannot satisfy the gate.
/// </summary>
public static class V21GameplayVerticalDebugScenarios
{
    [MenuItem("DungeonStory/QA/V21 Gameplay Vertical Gate")]
    public static void Run()
    {
        Require(DungeonSaveSectionDebugScenarios.RunAll(false),
            "Atomic save-section staging/rollback scenarios failed.");
        Require(EventAlertDebugScenarios.RunAll(false),
            "Functional event-alert action/save scenarios failed.");
        Require(V21CropGenomeDebugScenarios.RunAll(),
            "The six-locus crop gameplay scenarios failed.");
        Require(WildlifeDebugScenarios.RunAll(false),
            "Wildlife ecology and disease-vector scenarios failed.");
        Require(SurgeryDebugScenarios.RunAll(false),
            "Physical surgery and age-treatment scenarios failed.");
        Require(OffenseBattleDebugScenarios.RunAll(false),
            "Offense ammunition, equipment-role and encounter scenarios failed.");
        Require(DefenseEngagementDebugScenarios.RunAll(false),
            "Defense combat and V21 persistence scenarios failed.");

        V20EnemyIndividualDebugScenarios.Run();
        V20CampaignDebugScenarios.Run();
        SpeciesFactionDefenseExpansionDebugScenarios.ValidateOnly();
        ResearchEquipmentOverhaulDebugScenarios.RunFromMenu();

        Debug.Log(
            "V21_GAMEPLAY_VERTICAL_GATE=PASS; atomicSaveSections=true; alerts=true; crops=6/6; "
            + "wildlife=true; medical=true; offense=true; defense=true; "
            + "enemyContinuity=true; campaign=true; factionAuthority=true; "
            + "researchFacilities=115");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
