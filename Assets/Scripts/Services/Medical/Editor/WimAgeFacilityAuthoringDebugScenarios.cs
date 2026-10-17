#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Offline WIM-023 authoring audit for the five age-treatment procedures and
/// their required primary operating facilities. Runtime selection, UI, and
/// scheduling are intentionally exercised by separate focused witnesses.
/// </summary>
public static class WimAgeFacilityAuthoringDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/wim-implementation/wim-023-age-facility-authoring.txt";

    private const string ProcedureRoot = "Assets/Resources/SO/Medical/Procedures";
    private const string BuildingRoot = "Assets/Resources/SO/Building";

    private static readonly ExpectedMapping[] ExpectedMappings =
    {
        new("procedure:organ-regeneration", 8868),
        new("procedure:blood-rejuvenation", 8869),
        new("procedure:rune-hibernation", 8870),
        new("procedure:whole-body-regeneration", 8871),
        new("procedure:temporal-stasis", 8872)
    };

    /// <summary>
    /// Runs the read-only authored-asset audit, writes its concise witness,
    /// and throws when an approved mapping is absent or inconsistent.
    /// </summary>
    public static string RunFocused()
    {
        List<string> rows = new()
        {
            "scope=offline SurgicalProcedureSO and BuildingSO authoring; no runtime selection, UI, schedule, or restore claim"
        };

        try
        {
            (int procedureCount, int otherProcedureCount, int buildingCount) =
                VerifyAuthoring();
            rows.Add(
                "age-facility-authoring=PASS; procedures=" + procedureCount
                + "; age-procedures=" + ExpectedMappings.Length
                + "; other-procedures=" + otherProcedureCount
                + "; buildings=" + buildingCount);
            rows.Add("result=PASS");
        }
        catch (Exception error)
        {
            rows.Add("result=FAIL; " + error.Message);
            WriteReport(rows);
            throw;
        }

        WriteReport(rows);
        return string.Join(Environment.NewLine, rows);
    }

    private static (int ProcedureCount, int OtherProcedureCount, int BuildingCount)
        VerifyAuthoring()
    {
        SurgicalProcedureSO[] procedures = LoadAssets<SurgicalProcedureSO>(
            ProcedureRoot);
        BuildingSO[] buildings = LoadAssets<BuildingSO>(BuildingRoot);
        Require(
            procedures.Length == 47,
            "WIM-023 requires exactly 47 current SurgicalProcedureSO assets; found "
            + procedures.Length + ".");

        HashSet<string> expectedProcedureIds = new(
            ExpectedMappings.Select(mapping => mapping.ProcedureId),
            StringComparer.Ordinal);
        Require(
            expectedProcedureIds.Count == ExpectedMappings.Length,
            "WIM-023 expected procedure table contains a duplicate id.");

        foreach (ExpectedMapping expected in ExpectedMappings)
        {
            SurgicalProcedureSO[] matchingProcedures = procedures
                .Where(procedure => procedure != null
                    && string.Equals(
                        procedure.ProcedureId,
                        expected.ProcedureId,
                        StringComparison.Ordinal))
                .ToArray();
            Require(
                matchingProcedures.Length == 1,
                "WIM-023 requires exactly one procedure asset for '"
                + expected.ProcedureId + "'; found "
                + matchingProcedures.Length + ".");

            SurgicalProcedureSO procedure = matchingProcedures[0];
            Require(
                procedure.PrimaryFacilityDefinitionId
                    == expected.FacilityDefinitionId,
                "WIM-023 procedure '" + expected.ProcedureId
                + "' must reference building id "
                + expected.FacilityDefinitionId + "; found "
                + procedure.PrimaryFacilityDefinitionId + ".");

            BuildingSO[] matchingBuildings = buildings
                .Where(building => building != null
                    && building.id == expected.FacilityDefinitionId)
                .ToArray();
            Require(
                matchingBuildings.Length == 1,
                "WIM-023 procedure '" + expected.ProcedureId
                + "' requires exactly one actual BuildingSO with id "
                + expected.FacilityDefinitionId + "; found "
                + matchingBuildings.Length + ".");

            bool supportsAgeTreatmentAsPrimary = matchingBuildings[0].Abilities
                .OfType<ISurgicalFacilityAbility>()
                .Any(ability => ability.IsPrimaryOperatingFacility
                    && (ability.FacilityTags & SurgeryFacilityTag.AgeTreatment)
                        == SurgeryFacilityTag.AgeTreatment);
            Require(
                supportsAgeTreatmentAsPrimary,
                "WIM-023 building id " + expected.FacilityDefinitionId
                + " must expose a primary ISurgicalFacilityAbility with "
                + "SurgeryFacilityTag.AgeTreatment.");
        }

        SurgicalProcedureSO[] otherProcedures = procedures
            .Where(procedure => procedure != null
                && !expectedProcedureIds.Contains(procedure.ProcedureId))
            .ToArray();
        Require(
            otherProcedures.Length == 42,
            "WIM-023 requires exactly 42 non-age procedures; found "
            + otherProcedures.Length + ".");

        SurgicalProcedureSO[] incorrectlyBound = otherProcedures
            .Where(procedure => procedure.PrimaryFacilityDefinitionId != 0)
            .ToArray();
        Require(
            incorrectlyBound.Length == 0,
            "WIM-023 non-age procedures must retain primary facility id 0: "
            + string.Join(
                ", ",
                incorrectlyBound.Select(procedure => procedure.ProcedureId
                    + "=" + procedure.PrimaryFacilityDefinitionId))
            + ".");

        return (procedures.Length, otherProcedures.Length, buildings.Length);
    }

    private static T[] LoadAssets<T>(string root)
        where T : UnityEngine.Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset != null)
            .ToArray();
    }

    private static void WriteReport(IEnumerable<string> rows)
    {
        string directory = Path.GetDirectoryName(ReportPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllLines(ReportPath, rows);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private readonly struct ExpectedMapping
    {
        public ExpectedMapping(string procedureId, int facilityDefinitionId)
        {
            ProcedureId = procedureId;
            FacilityDefinitionId = facilityDefinitionId;
        }

        public string ProcedureId { get; }
        public int FacilityDefinitionId { get; }
    }
}
#endif
