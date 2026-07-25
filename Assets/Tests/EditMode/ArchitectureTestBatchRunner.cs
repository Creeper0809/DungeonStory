using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace DungeonStory.Tests.Architecture
{
    internal static class ArchitectureTestBatchRunner
    {
        private static TestRunnerApi runner;
        private static ArchitectureTestCallbacks callbacks;

        [MenuItem("DungeonStory/Debug/Architecture/Run EditMode Tests")]
        public static void Run()
        {
            runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            runner.hideFlags = HideFlags.HideAndDontSave;
            callbacks = new ArchitectureTestCallbacks();
            runner.RegisterCallbacks(callbacks);
            runner.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { "DungeonStory.Architecture.Tests" }
            }));
        }

        private sealed class ArchitectureTestCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                Debug.Log("[ArchitectureTests] started");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Debug.Log(
                    $"[ArchitectureTests] finished pass={result.PassCount} "
                    + $"fail={result.FailCount} skip={result.SkipCount}");
                int failCount = result.FailCount;
                Cleanup();
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(failCount > 0 ? 1 : 0);
                }
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.FailCount > 0)
                {
                    Debug.LogError(
                        $"[ArchitectureTests] FAIL {result.FullName}: {result.Message}");
                }
            }
        }

        private static void Cleanup()
        {
            if (runner != null)
            {
                Object.DestroyImmediate(runner);
            }

            runner = null;
            callbacks = null;
        }
    }
}
