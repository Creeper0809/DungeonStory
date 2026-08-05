using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class OffenseExpeditionArchitectureDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Offense/Run Expedition Architecture Scenarios")]
    public static void RunFromMenu()
    {
        if (!RunAll(logSuccess: true))
        {
            Debug.LogError("Offense expedition architecture scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> failures = new List<string>();
        VerifySingleStateAuthority(failures);
        VerifyConstructContract(failures);
        VerifyExperienceDeterminism(failures);
        foreach (string failure in failures)
        {
            Debug.LogError(failure);
        }

        if (failures.Count == 0 && logSuccess)
        {
            Debug.Log("Offense expedition architecture scenarios passed.");
        }

        return failures.Count == 0;
    }

    private static void VerifySingleStateAuthority(List<string> failures)
    {
        BindingFlags fields = BindingFlags.Instance
            | BindingFlags.Public
            | BindingFlags.NonPublic;
        bool launchOwnsState = typeof(OffenseExpeditionLaunchService)
            .GetFields(fields)
            .Any(field => IsExpeditionStateCollection(field.FieldType));
        bool decisionOwnsState = typeof(OffenseExpeditionDecisionService)
            .GetFields(fields)
            .Any(field => IsExpeditionStateCollection(field.FieldType));
        FieldInfo[] aggregateLists = typeof(OffenseExpeditionRuntime)
            .GetFields(fields)
            .Where(field => IsExpeditionStateCollection(field.FieldType))
            .ToArray();
        if (launchOwnsState || decisionOwnsState || aggregateLists.Length != 2)
        {
            failures.Add(
                "Expedition state authority must remain in OffenseExpeditionRuntime only.");
        }
    }

    private static void VerifyConstructContract(List<string> failures)
    {
        int[] parameterCounts = typeof(OffenseExpeditionRuntime)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name == "Construct")
            .Select(method => method.GetParameters().Length)
            .OrderBy(count => count)
            .ToArray();
        if (!parameterCounts.SequenceEqual(new[] { 5, 7, 23 }))
        {
            failures.Add(
                "OffenseExpeditionRuntime DI overload contract changed unexpectedly: "
                + string.Join(",", parameterCounts));
        }
    }

    private static void VerifyExperienceDeterminism(List<string> failures)
    {
        OffenseRouteNode node = new OffenseRouteNode(
            "architecture:elite",
            3,
            0,
            OffenseRouteNodeKind.Battle,
            "Elite",
            "Architecture determinism probe",
            1f,
            Array.Empty<string>());
        int first = OffenseExpeditionRuntime.CalculateNodeExperience(node, 4);
        int second = OffenseExpeditionRuntime.CalculateNodeExperience(node, 4);
        int returnFirst =
            OffenseExpeditionRuntime.CalculateSuccessfulReturnExperience(4);
        int returnSecond =
            OffenseExpeditionRuntime.CalculateSuccessfulReturnExperience(4);
        if (first != second || returnFirst != returnSecond)
        {
            failures.Add("Expedition experience rewards are not deterministic.");
        }
    }

    private static bool IsExpeditionStateCollection(Type type)
    {
        return type == typeof(List<OffenseExpeditionRun>)
            || type == typeof(List<OffenseExpeditionResult>);
    }
}
