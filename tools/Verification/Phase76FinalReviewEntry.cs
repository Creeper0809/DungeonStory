// Verification-only CLI entry points outside Assets: no game or asset mutation.
// Long synchronous editor checks use run_script's explicit dispatcher timeout.
public static class Phase76FinalReviewEntry
{
    public static bool VerifyExporter()
    {
        if (UnityEditor.EditorUtility.scriptCompilationFailed)
            throw new System.InvalidOperationException("Current Unity compilation failed.");
        FormulaPresentationPilotExporter.RunAll();
        return true;
    }

    public static string ExportPilot(string version)
    {
        if (UnityEditor.EditorUtility.scriptCompilationFailed)
            throw new System.InvalidOperationException("Current Unity compilation failed.");
        return FormulaPresentationPilotExporter.ExportVerified100(version);
    }
}
