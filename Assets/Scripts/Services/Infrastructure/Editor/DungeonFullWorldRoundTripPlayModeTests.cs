#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class DungeonFullWorldRoundTripPlayModeTests
{
    [UnityTest]
    public IEnumerator FullWorldRoundTrip()
    {
        File.Delete(DungeonFullWorldRoundTripPlayModeFacade.ReportPath);
        DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();

        EditorSceneManager.OpenScene(
            DungeonFullWorldRoundTripPlayModeFacade.GameplayScenePath,
            OpenSceneMode.Single);
        yield return new EnterPlayMode();

        GameObject runnerHost = new("Full World Round Trip Test Runner");
        runnerHost.AddComponent<DungeonFullWorldRoundTripPlayModeRunner>();

        float deadline = Time.realtimeSinceStartup + 180f;
        while (!File.Exists(DungeonFullWorldRoundTripPlayModeFacade.ReportPath)
            && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.That(
            File.Exists(DungeonFullWorldRoundTripPlayModeFacade.ReportPath),
            Is.True,
            "The full-world round-trip runner did not produce its report.");
        string report = File.ReadAllText(
            DungeonFullWorldRoundTripPlayModeFacade.ReportPath);
        Assert.That(report, Does.StartWith("RESULT=PASS"), report);
        yield return new ExitPlayMode();
    }
}
#endif
