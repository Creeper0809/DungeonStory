#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EditorVerificationSceneFixtureScopeDebugScenarios
{
    [MenuItem("DungeonStory/Debug/QA/Run Editor Verification Fixture Scope Contracts")]
    public static void RunAll()
    {
        Scene original = SceneManager.GetActiveScene();
        bool dirtyBefore = original.isDirty;
        int[] rootsBefore = CaptureRootIds(original);

        for (int attempt = 0; attempt < 2; attempt++)
        {
            bool injectedFailureObserved = false;
            try
            {
                EditorVerificationSceneFixtureScope.Run(
                    "qa:fixture-scope-failure",
                    () =>
                    {
                        new GameObject("HaulPlanActor");
                        new GameObject("HaulPlanWarehouse");
                        new GameObject("LifecycleDemolitionFixture");
                        new GameObject(
                            "QA Surgery Material Destination Facility");
                        new GameObject(
                            "QA Character Medical Supply Destination Facility");
                        throw new InvalidOperationException(
                            "qa-injected-fixture-failure");
                    });
            }
            catch (InvalidOperationException exception)
            {
                injectedFailureObserved = string.Equals(
                    exception.Message,
                    "qa-injected-fixture-failure",
                    StringComparison.Ordinal);
            }

            Require(injectedFailureObserved,
                "The injected fixture failure was not preserved.");
            Require(SceneManager.GetActiveScene().handle == original.handle
                    && original.isDirty == dirtyBefore
                    && CaptureRootIds(original).SequenceEqual(rootsBefore),
                "A failed scratch fixture changed the original scene.");
        }

        EditorVerificationSceneFixtureScope.Run(
            "qa:fixture-scope-nested-outer",
            () =>
            {
                Scene outer = SceneManager.GetActiveScene();
                bool outerDirty = outer.isDirty;
                int[] outerRoots = CaptureRootIds(outer);
                EditorVerificationSceneFixtureScope.Run(
                    "qa:fixture-scope-nested-inner",
                    () => new GameObject("NestedFixtureOwnedRoot"));
                Require(SceneManager.GetActiveScene().handle == outer.handle
                        && outer.isDirty == outerDirty
                        && CaptureRootIds(outer).SequenceEqual(outerRoots),
                    "A nested scratch fixture changed its outer fixture scene.");
            });

        Debug.Log(
            "EDITOR_VERIFICATION_FIXTURE_SCOPE_PASS failure cleanup, repeated and nested "
            + "execution, active scene, dirty state, and root topology are exact.");
    }

    private static int[] CaptureRootIds(Scene scene) => scene
        .GetRootGameObjects()
        .Where(root => root != null)
        .Select(root => root.GetInstanceID())
        .OrderBy(value => value)
        .ToArray();

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
