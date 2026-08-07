using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DungeonStory.Tests.Architecture
{
    internal static class ArchitectureTestBatchRunner
    {
        public const int ExpectedTestCount = 154;
        public const string SynchronousReportPath =
            "Artifacts/QA/architecture-editmode-report.txt";

        [MenuItem("DungeonStory/Debug/Architecture/Run EditMode Tests")]
        public static void Run()
        {
            RunSynchronous();
        }

        [MenuItem("DungeonStory/Debug/Architecture/Run Synchronous EditMode Tests")]
        public static void RunSynchronous()
        {
            _ = RunForFinalGate(out _);
        }

        public static bool RunForFinalGate(out string detail)
        {
            List<string> failures = new List<string>();
            MethodInfo[] tests;
            try
            {
                tests = DiscoverTests();
            }
            catch (Exception exception)
            {
                failures.Add(
                    "ArchitectureTestBatchRunner.DiscoverTests: "
                    + $"{exception.GetType().Name}: {exception.Message}");
                WriteReport(0, 0, failures);
                detail = failures[0];
                Debug.LogError(
                    "[ArchitectureTests] synchronous discovery failed; "
                    + $"report={SynchronousReportPath}");
                return false;
            }

            if (tests.Length != ExpectedTestCount)
            {
                failures.Add(
                    $"Architecture test count must remain exactly {ExpectedTestCount}; "
                    + $"discovered={tests.Length}.");
            }

            int passed = 0;
            foreach (MethodInfo test in tests)
            {
                string unsupportedReason = GetUnsupportedReason(test);
                if (!string.IsNullOrEmpty(unsupportedReason))
                {
                    failures.Add(
                        $"{GetTestName(test)}: UnsupportedTestShape: "
                        + unsupportedReason);
                    continue;
                }

                try
                {
                    object fixture = test.IsStatic
                        ? null
                        : Activator.CreateInstance(test.DeclaringType, true);
                    test.Invoke(fixture, null);
                    passed++;
                }
                catch (TargetInvocationException exception)
                {
                    Exception cause = exception.InnerException ?? exception;
                    failures.Add(
                        $"{test.DeclaringType?.FullName}.{test.Name}: "
                        + $"{cause.GetType().Name}: {cause.Message}");
                }
                catch (Exception exception)
                {
                    failures.Add(
                        $"{test.DeclaringType?.FullName}.{test.Name}: "
                        + $"{exception.GetType().Name}: {exception.Message}");
                }
            }

            WriteReport(tests.Length, passed, failures);

            if (failures.Count == 0)
            {
                Debug.Log(
                    $"[ArchitectureTests] scene-neutral finished pass={passed} fail=0");
            }
            else
            {
                Debug.LogError(
                    $"[ArchitectureTests] scene-neutral finished pass={passed} "
                    + $"fail={failures.Count}; report={SynchronousReportPath}");
            }

            detail = failures.Count == 0
                ? $"Architecture tests passed exactly {passed}/{ExpectedTestCount}."
                : string.Join(" | ", failures);
            return failures.Count == 0
                && tests.Length == ExpectedTestCount
                && passed == ExpectedTestCount;
        }

        private static MethodInfo[] DiscoverTests()
        {
            return typeof(ArchitectureTestBatchRunner).Assembly
                .GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract)
                .SelectMany(type => type.GetMethods(
                    BindingFlags.DeclaredOnly
                    | BindingFlags.Instance
                    | BindingFlags.Static
                    | BindingFlags.Public
                    | BindingFlags.NonPublic))
                .Where(method => method.GetCustomAttributes(
                    typeof(TestAttribute), false).Length > 0)
                .OrderBy(method => method.DeclaringType?.FullName, StringComparer.Ordinal)
                .ThenBy(method => method.Name, StringComparer.Ordinal)
                .ToArray();
        }

        private static string GetUnsupportedReason(MethodInfo test)
        {
            if (test.ContainsGenericParameters)
            {
                return "generic test methods are not supported";
            }

            if (test.GetParameters().Length != 0)
            {
                return "test methods must have no parameters";
            }

            if (test.ReturnType != typeof(void))
            {
                return "test methods must return void";
            }

            Type declaringType = test.DeclaringType;
            if (declaringType == null)
            {
                return "test method has no declaring fixture";
            }

            if (!test.IsStatic && declaringType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    types: Type.EmptyTypes,
                    modifiers: null) == null)
            {
                return "fixture must have a parameterless constructor";
            }

            return string.Empty;
        }

        private static string GetTestName(MethodInfo test)
        {
            return $"{test.DeclaringType?.FullName ?? "<unknown>"}.{test.Name}";
        }

        private static void WriteReport(
            int testCount,
            int passed,
            IReadOnlyCollection<string> failures)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SynchronousReportPath)
                ?? "Artifacts/QA");
            List<string> report = new List<string>
            {
                failures.Count == 0
                    ? "ARCHITECTURE_EDITMODE RESULT=PASS"
                    : "ARCHITECTURE_EDITMODE RESULT=FAIL",
                $"expectedTests={ExpectedTestCount}",
                $"tests={testCount}",
                $"pass={passed}",
                $"fail={failures.Count}",
                $"generatedUtc={DateTime.UtcNow:O}"
            };
            report.AddRange(failures.Select(failure => "[FAIL] " + failure));
            File.WriteAllLines(SynchronousReportPath, report);
        }
    }
}
