using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DungeonStory.Tests.Architecture
{
    internal static class TransactionalRestoreTestRunner
    {
        public const int ExpectedTestCount = 33;
        public const string ReportRelativePath =
            "Artifacts/QA/transactional-restore-editmode-report.txt";

        private static readonly string[] FixtureNames =
        {
            "DungeonStory.Tests.Architecture.CharacterSpawnCompositionArchitectureTests",
            "DungeonStory.Tests.Architecture.CharacterWorldTransactionalRestoreArchitectureTests",
            "DungeonStory.Tests.Architecture.WildlifeTransactionalRestoreArchitectureTests",
            "DungeonStory.Tests.Architecture.CircusTransactionalRestoreArchitectureTests",
            "DungeonStory.Tests.Architecture.ExteriorAndCombatTransactionalRestoreArchitectureTests",
            "DungeonStory.Tests.Architecture.InvasionIntruderArchitectureTests"
        };

        [MenuItem("DungeonStory/Debug/Architecture/Run Transactional Restore Tests")]
        public static void Run()
        {
            _ = RunForFinalGate(out _);
        }

        public static bool RunForFinalGate(out string detail)
        {
            string reportPath = ResolveReportPath();
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(
                reportPath,
                BuildReport(
                    started: false,
                    startedAtUtc: null,
                    startedTestCases: 0,
                    completed: false,
                    completedAtUtc: null,
                    pass: 0,
                    fail: 0,
                    skip: 0,
                    failures: Array.Empty<TestFailure>()),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            DateTime startedAtUtc = DateTime.UtcNow;
            int startedTestCases = 0;
            int passed = 0;
            List<TestFailure> failures = new List<TestFailure>();
            try
            {
                MethodInfo[] tests = DiscoverTests();
                startedTestCases = tests.Length;
                if (startedTestCases != ExpectedTestCount)
                {
                    failures.Add(new TestFailure(
                        "TransactionalRestoreTestRunner.TestCount",
                        $"Transactional restore test count must remain exactly "
                        + $"{ExpectedTestCount}; discovered={startedTestCases}."));
                }
                WriteReport(
                    reportPath,
                    started: true,
                    startedAtUtc,
                    startedTestCases,
                    completed: false,
                    completedAtUtc: null,
                    pass: 0,
                    fail: 0,
                    skip: 0,
                    failures);
                Debug.Log(
                    $"[TransactionalRestoreTests] started cases={startedTestCases}");

                foreach (MethodInfo test in tests)
                {
                    string unsupportedReason = GetUnsupportedReason(test);
                    if (!string.IsNullOrEmpty(unsupportedReason))
                    {
                        failures.Add(new TestFailure(
                            GetTestName(test),
                            "UnsupportedTestShape: " + unsupportedReason));
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
                        failures.Add(new TestFailure(
                            GetTestName(test),
                            $"{cause.GetType().Name}: {cause.Message}"));
                    }
                    catch (Exception exception)
                    {
                        failures.Add(new TestFailure(
                            GetTestName(test),
                            $"{exception.GetType().Name}: {exception.Message}"));
                    }
                }
            }
            catch (Exception exception)
            {
                failures.Add(new TestFailure(
                    "TransactionalRestoreTestRunner.Run",
                    exception.ToString()));
            }

            WriteReport(
                reportPath,
                started: true,
                startedAtUtc,
                startedTestCases,
                completed: true,
                completedAtUtc: DateTime.UtcNow,
                pass: passed,
                fail: failures.Count,
                skip: 0,
                failures);
            if (failures.Count == 0)
            {
                Debug.Log(
                    $"[TransactionalRestoreTests] completed pass={passed} "
                    + "fail=0 skip=0");
            }
            else
            {
                Debug.LogError(
                    $"[TransactionalRestoreTests] completed pass={passed} "
                    + $"fail={failures.Count} skip=0; report={reportPath}");
            }

            bool passedExactly = failures.Count == 0
                && startedTestCases == ExpectedTestCount
                && passed == ExpectedTestCount;
            detail = passedExactly
                ? $"Transactional restore tests passed exactly "
                    + $"{passed}/{ExpectedTestCount}."
                : failures.Count > 0
                    ? string.Join(" | ", failures.Select(failure =>
                        failure.FullName + ": " + failure.Message))
                    : $"Expected {ExpectedTestCount} passes; actual={passed}.";
            return passedExactly;
        }

        private static MethodInfo[] DiscoverTests()
        {
            Type[] fixtureTypes = typeof(TransactionalRestoreTestRunner).Assembly
                .GetTypes()
                .Where(type => FixtureNames.Contains(
                    type.FullName,
                    StringComparer.Ordinal))
                .ToArray();
            string[] missingFixtures = FixtureNames
                .Except(
                    fixtureTypes.Select(type => type.FullName),
                    StringComparer.Ordinal)
                .ToArray();
            if (missingFixtures.Length > 0)
            {
                throw new InvalidOperationException(
                    "Missing transactional restore fixtures: "
                    + string.Join(", ", missingFixtures));
            }

            return fixtureTypes
                .SelectMany(type => type.GetMethods(
                    BindingFlags.DeclaredOnly
                    | BindingFlags.Instance
                    | BindingFlags.Static
                    | BindingFlags.Public
                    | BindingFlags.NonPublic))
                .Where(method => method.GetCustomAttributes(
                    typeof(TestAttribute), false).Length > 0)
                .OrderBy(method => Array.IndexOf(
                    FixtureNames,
                    method.DeclaringType?.FullName))
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
            string reportPath,
            bool started,
            DateTime? startedAtUtc,
            int startedTestCases,
            bool completed,
            DateTime? completedAtUtc,
            int pass,
            int fail,
            int skip,
            IReadOnlyList<TestFailure> failures)
        {
            File.WriteAllText(
                reportPath,
                BuildReport(
                    started,
                    startedAtUtc,
                    startedTestCases,
                    completed,
                    completedAtUtc,
                    pass,
                    fail,
                    skip,
                    failures),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static string ResolveReportPath()
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                ReportRelativePath));
        }

        private static string BuildReport(
            bool started,
            DateTime? startedAtUtc,
            int startedTestCases,
            bool completed,
            DateTime? completedAtUtc,
            int pass,
            int fail,
            int skip,
            IReadOnlyList<TestFailure> failures)
        {
            StringBuilder report = new StringBuilder(1024);
            report.AppendLine("transactional-restore-editmode");
            bool passedExactly = completed
                && startedTestCases == ExpectedTestCount
                && pass == ExpectedTestCount
                && fail == 0
                && skip == 0;
            report.Append("TRANSACTIONAL_RESTORE RESULT=")
                .AppendLine(passedExactly ? "PASS" : "FAIL");
            report.Append("expectedTestCases=")
                .AppendLine(ExpectedTestCount.ToString());
            report.Append("started=").AppendLine(started ? "true" : "false");
            report.Append("startedAtUtc=")
                .AppendLine(startedAtUtc?.ToString("O") ?? string.Empty);
            report.Append("startedTestCases=").AppendLine(startedTestCases.ToString());
            report.Append("completed=").AppendLine(completed ? "true" : "false");
            report.Append("completedAtUtc=")
                .AppendLine(completedAtUtc?.ToString("O") ?? string.Empty);
            report.Append("pass=").AppendLine(pass.ToString());
            report.Append("fail=").AppendLine(fail.ToString());
            report.Append("skip=").AppendLine(skip.ToString());
            report.Append("fixtures=").AppendLine(string.Join(",", FixtureNames));
            report.AppendLine("failures:");
            foreach (TestFailure failure in failures ?? Array.Empty<TestFailure>())
            {
                report.Append("- fullName=").AppendLine(failure.FullName);
                report.Append("  message=")
                    .AppendLine(IndentContinuationLines(failure.Message));
            }

            return report.ToString();
        }

        private static string IndentContinuationLines(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\n", "\n    ");
        }

        private readonly struct TestFailure
        {
            public TestFailure(string fullName, string message)
            {
                FullName = fullName ?? string.Empty;
                Message = message ?? string.Empty;
            }

            public string FullName { get; }
            public string Message { get; }
        }
    }
}
