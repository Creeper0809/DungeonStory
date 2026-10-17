// Verification-only CLI entry points outside Assets: no runtime content authority.
public static class Phase77CompletionEntry
{
    private static void RequireCurrentCompilation()
    {
        if (UnityEditor.EditorUtility.scriptCompilationFailed)
            throw new System.InvalidOperationException("Current Unity compilation failed.");
    }

    public static bool VerifyEquipmentV18()
    {
        RequireCurrentCompilation();
        EquipmentItemStateV18DebugScenarios.RunAll();
        return true;
    }

    public static bool VerifyFacilitySameAxis()
    {
        RequireCurrentCompilation();
        FacilityEvolutionSameAxisEligibilityDebugScenarios.Validate();
        return true;
    }

    public static bool VerifyFacilityLegacyContracts()
    {
        RequireCurrentCompilation();
        if (!FacilityEvolutionDebugScenarios.VerifyFormulaV2ModuleSelectionContract())
            throw new System.InvalidOperationException("Facility formula v2 contract failed.");
        if (!FacilityEvolutionDebugScenarios.VerifyFormulaV3BurdenSeparation())
            throw new System.InvalidOperationException("Facility formula v3 burden separation failed.");
        return true;
    }

    public static bool VerifyExporter()
    {
        RequireCurrentCompilation();
        FormulaPresentationPilotExporter.RunAll();
        return true;
    }

    public static string ExportPilot(string version)
    {
        RequireCurrentCompilation();
        return FormulaPresentationPilotExporter.ExportVerified100(version);
    }

    public static string ExportCatalog(string version)
    {
        RequireCurrentCompilation();
        NarrativeMechanicCatalogExportResult result =
            NarrativeMechanicCatalogExporter.Export(
                new NarrativeMechanicCatalogUnityAssetSource(),
                new NarrativeMechanicCatalogExportRequest(version));
        return result.DirectoryPath;
    }

    public static string ExportResolution(
        string version,
        string sourceJsonlPath,
        string responseJsonlPath)
    {
        RequireCurrentCompilation();
        return FormulaPresentationPilotExporter.ExportResolvedModuleSelection100(
            version,
            sourceJsonlPath,
            responseJsonlPath);
    }
}
