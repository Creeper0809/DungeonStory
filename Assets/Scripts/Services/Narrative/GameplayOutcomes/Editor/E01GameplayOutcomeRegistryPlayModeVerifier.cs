#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class E01GameplayOutcomeRegistryPlayModeVerifier
{
    private const float RuntimeReadyTimeoutSeconds = 45f;
    private const string HistoricalClosureRelativePath =
        "Artifacts/QA/GameplayOutcomeLedgerPhase80FinalClosure-20260920-r6/final-closure.json";
    private const string HistoricalClosureSha256 =
        "5274ac45c9e4edf1496a95a3f0e3d614a4ea1b611661b77a9823741e286b98c1";

    public static IEnumerator Verify(
        ICollection<string> warnings,
        ICollection<string> errors)
    {
        var report = new E01RegistryReport
        {
            scope = "U0 E01 production registry/action-inventory bijection and ProductQuality historical/current lineage",
            historicalClosurePath = HistoricalClosureRelativePath,
            historicalClosureExpectedSha256 = HistoricalClosureSha256,
            fullAuditClosure = false,
            humanApprovalClaimed = false,
            trainingEligible = false
        };
        bool isolatedModeReady = true;
        try
        {
            Require(Application.isBatchMode
                    && DungeonPersistencePaths.IsIsolatedVerification
                    && string.Equals(
                        Environment.GetEnvironmentVariable(
                            "DUNGEONSTORY_U0_E01_REGISTRY_INVENTORY_VERIFY"),
                        "1",
                        StringComparison.Ordinal),
                "E01 registry verification requires explicit isolated batch mode.");
        }
        catch (Exception exception)
        {
            report.failure = exception.ToString();
            isolatedModeReady = false;
        }
        if (!isolatedModeReady)
        {
            Finish(report, warnings, errors);
            yield break;
        }

        float deadline = Time.realtimeSinceStartup
            + RuntimeReadyTimeoutSeconds;
        DungeonRuntimeLifetimeScope scope = null;
        while (scope == null && Time.realtimeSinceStartup < deadline)
        {
            scope = UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(value =>
                    value != null && value.Container != null);
            if (scope == null)
                yield return null;
        }

        try
        {
            Require(scope != null, "Official runtime scope did not initialize.");

            IGameplayOutcomeRegistry registry =
                scope.Container.Resolve<IGameplayOutcomeRegistry>();
            Require(registry != null, "Production gameplay outcome registry is missing.");
            Assembly runtimeAssembly = typeof(GameplayOutcomeRegistry).Assembly;
            Type[] runtimeTypes = LoadTypes(runtimeAssembly);

            Type[] expectedAdapterTypes = runtimeTypes
                .Where(value => value != null
                    && !value.IsAbstract
                    && !value.IsInterface
                    && typeof(IGameplayOutcomeAdapterRegistration)
                        .IsAssignableFrom(value))
                .OrderBy(TypeName, StringComparer.Ordinal)
                .ToArray();
            Type[] expectedDescriptorTypes = runtimeTypes
                .Where(value => value != null
                    && !value.IsAbstract
                    && !value.IsInterface
                    && typeof(IGameplayOutcomeDescriptor)
                        .IsAssignableFrom(value))
                .OrderBy(TypeName, StringComparer.Ordinal)
                .ToArray();
            Require(expectedAdapterTypes.Length > 0
                    && expectedDescriptorTypes.Length > 0,
                "Production action inventory did not expose outcome adapters/descriptors.");

            Type[] expectedReceiptTypes = expectedAdapterTypes
                .Select(RequireReceiptType)
                .OrderBy(TypeName, StringComparer.Ordinal)
                .ToArray();
            Require(expectedReceiptTypes.Distinct().Count()
                    == expectedReceiptTypes.Length,
                "Production action inventory contains duplicate receipt adapter ownership.");

            Type[] actualReceiptTypes = registry.RegisteredReceiptTypes
                .OrderBy(TypeName, StringComparer.Ordinal)
                .ToArray();
            Type[] actualAdapterTypes = actualReceiptTypes
                .Select(receiptType => ResolveAdapterType(registry, receiptType))
                .Distinct()
                .OrderBy(TypeName, StringComparer.Ordinal)
                .ToArray();
            Type[] actualDescriptorTypes = registry.RegisteredOutcomeTypes
                .Select(outcomeTypeId =>
                {
                    Require(registry.TryGetDescriptor(
                            outcomeTypeId,
                            out IGameplayOutcomeDescriptor descriptor)
                        && descriptor != null,
                        "Registered outcome type has no descriptor: "
                        + outcomeTypeId.Value);
                    return descriptor.GetType();
                })
                .Distinct()
                .OrderBy(TypeName, StringComparer.Ordinal)
                .ToArray();

            report.expectedReceiptTypes = Names(expectedReceiptTypes);
            report.registeredReceiptTypes = Names(actualReceiptTypes);
            report.expectedAdapterTypes = Names(expectedAdapterTypes);
            report.registeredAdapterTypes = Names(actualAdapterTypes);
            report.expectedDescriptorTypes = Names(expectedDescriptorTypes);
            report.registeredDescriptorTypes = Names(actualDescriptorTypes);
            report.registeredOutcomeTypes = registry.RegisteredOutcomeTypes
                .Select(value => value.Value)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            report.receiptTypesExact = expectedReceiptTypes.SequenceEqual(
                actualReceiptTypes);
            report.adapterTypesExact = expectedAdapterTypes.SequenceEqual(
                actualAdapterTypes);
            report.descriptorTypesExact = expectedDescriptorTypes.SequenceEqual(
                actualDescriptorTypes);
            report.missingReceiptTypes = Missing(
                expectedReceiptTypes,
                actualReceiptTypes);
            report.unexpectedReceiptTypes = Missing(
                actualReceiptTypes,
                expectedReceiptTypes);
            report.missingAdapterTypes = Missing(
                expectedAdapterTypes,
                actualAdapterTypes);
            report.unexpectedAdapterTypes = Missing(
                actualAdapterTypes,
                expectedAdapterTypes);
            report.missingDescriptorTypes = Missing(
                expectedDescriptorTypes,
                actualDescriptorTypes);
            report.unexpectedDescriptorTypes = Missing(
                actualDescriptorTypes,
                expectedDescriptorTypes);
            Require(report.receiptTypesExact
                    && report.adapterTypesExact
                    && report.descriptorTypesExact,
                "Production registry and action inventory differ."
                + " missingReceipts=" + string.Join(",", report.missingReceiptTypes)
                + "; unexpectedReceipts=" + string.Join(",", report.unexpectedReceiptTypes)
                + "; missingAdapters=" + string.Join(",", report.missingAdapterTypes)
                + "; unexpectedAdapters=" + string.Join(",", report.unexpectedAdapterTypes)
                + "; missingDescriptors=" + string.Join(",", report.missingDescriptorTypes)
                + "; unexpectedDescriptors=" + string.Join(",", report.unexpectedDescriptorTypes));

            report.productQualityReceiptRegistered = actualReceiptTypes.Contains(
                typeof(ProductQualityOutcomeReceipt));
            report.productQualityAdapterRegistered = actualAdapterTypes.Contains(
                typeof(ProductQualityOutcomeAdapter));
            report.productQualityDescriptorRegistered = registry.TryGetDescriptor(
                    EvolutionOutcomeIds.ProductQualityResolved,
                    out IGameplayOutcomeDescriptor productQualityDescriptor)
                && productQualityDescriptor is ProductQualityOutcomeDescriptor;
            report.productQualityOutcomeTypeRegistered =
                registry.RegisteredOutcomeTypes.Contains(
                    EvolutionOutcomeIds.ProductQualityResolved);
            Require(report.productQualityReceiptRegistered
                    && report.productQualityAdapterRegistered
                    && report.productQualityDescriptorRegistered
                    && report.productQualityOutcomeTypeRegistered,
                "Current production registry omitted ProductQuality receipt, adapter, descriptor, or outcome type.");

            VerifyHistoricalClosure(report);
            Require(warnings.Count == 0 && errors.Count == 0,
                "Unexpected runtime Console warning/error was observed.");
            report.passed = true;
        }
        catch (Exception exception)
        {
            report.failure = exception.ToString();
        }

        Finish(report, warnings, errors);
    }

    private static void Finish(
        E01RegistryReport report,
        ICollection<string> warnings,
        ICollection<string> errors)
    {
        report.warningCount = warnings.Count;
        report.errorCount = errors.Count;
        report.warnings = warnings.ToArray();
        report.errors = errors.ToArray();
        WriteNew("report.json", JsonUtility.ToJson(report, true));
        DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () =>
            EditorApplication.Exit(report.passed ? 0 : 1);
    }

    private static void VerifyHistoricalClosure(E01RegistryReport report)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string path = Path.Combine(
            projectRoot,
            HistoricalClosureRelativePath.Replace(
                '/', Path.DirectorySeparatorChar));
        Require(File.Exists(path), "Historical closure artifact is missing.");
        byte[] bytes = File.ReadAllBytes(path);
        string sha256 = Hash(bytes);
        report.historicalClosureBytes = bytes.Length;
        report.historicalClosureSha256 = sha256;
        Require(string.Equals(
                sha256,
                HistoricalClosureSha256,
                StringComparison.Ordinal),
            "Historical closure artifact identity changed.");

        JObject document = JObject.Parse(Encoding.UTF8.GetString(bytes));
        Require(string.Equals(
                (string)document["schema"],
                "gameplay-outcome-ledger-final-closure@1",
                StringComparison.Ordinal),
            "Historical closure schema changed.");
        JArray rows = document["rows"] as JArray
            ?? throw new InvalidOperationException(
                "Historical closure rows are missing.");
        report.historicalCandidateCount =
            (int?)document["candidateCount"] ?? -1;
        report.historicalRowCount = rows.Count;
        report.historicalIndependentProductQualityRows = rows
            .OfType<JObject>()
            .Count(row => string.Equals(
                (string)row["candidate"],
                nameof(ProductQualityOutcomeReceipt),
                StringComparison.Ordinal));
        report.historicalProductQualityAuthorityReferences = rows
            .OfType<JObject>()
            .Count(row => ((string)row["authoritySymbol"] ?? string.Empty)
                .Split(new[] { " or " }, StringSplitOptions.None)
                .Contains(
                    nameof(ProductQualityOutcomeReceipt),
                    StringComparer.Ordinal));
        Require(report.historicalCandidateCount == 323
                && report.historicalRowCount == 323
                && report.historicalIndependentProductQualityRows == 0
                && report.historicalProductQualityAuthorityReferences > 0,
            "Historical ProductQuality omission/reference lineage changed.");
        report.historicalCurrentLineageExact = true;
    }

    private static Type[] LoadTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            string detail = string.Join(
                " | ",
                exception.LoaderExceptions
                    .Where(value => value != null)
                    .Select(value => value.Message));
            throw new InvalidOperationException(
                "Could not enumerate production outcome types: " + detail,
                exception);
        }
    }

    private static Type RequireReceiptType(Type adapterType)
    {
        Type[] receiptTypes = adapterType.GetInterfaces()
            .Where(value => value.IsGenericType
                && value.GetGenericTypeDefinition()
                    == typeof(IGameplayOutcomeAdapter<>))
            .Select(value => value.GetGenericArguments()[0])
            .Distinct()
            .ToArray();
        Require(receiptTypes.Length == 1,
            "Outcome adapter inventory entry must own exactly one receipt: "
            + TypeName(adapterType));
        return receiptTypes[0];
    }

    private static Type ResolveAdapterType(
        IGameplayOutcomeRegistry registry,
        Type receiptType)
    {
        MethodInfo method = typeof(IGameplayOutcomeRegistry).GetMethods()
            .Single(value => value.Name == nameof(
                    IGameplayOutcomeRegistry.TryGetAdapter)
                && value.IsGenericMethodDefinition);
        object[] arguments = { null };
        bool found = (bool)method.MakeGenericMethod(receiptType)
            .Invoke(registry, arguments);
        Require(found
                && arguments[0] is IGameplayOutcomeAdapterRegistration,
            "Registered receipt has no resolvable adapter: "
            + TypeName(receiptType));
        return arguments[0].GetType();
    }

    private static string[] Missing(
        IEnumerable<Type> expected,
        IEnumerable<Type> actual)
    {
        var actualSet = new HashSet<Type>(actual);
        return expected.Where(value => !actualSet.Contains(value))
            .Select(TypeName)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] Names(IEnumerable<Type> values) => values
        .Select(TypeName)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    private static string TypeName(Type value) =>
        value?.FullName ?? value?.Name ?? string.Empty;

    private static string Hash(byte[] bytes)
    {
        using SHA256 sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(bytes)
            .Select(value => value.ToString("x2")));
    }

    private static void WriteNew(string name, string content)
    {
        string root = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException(
                "E01 output directory is missing.");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, name);
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using var writer = new StreamWriter(
            stream,
            new UTF8Encoding(false));
        writer.Write(content);
    }

    private static void Require(bool condition, string detail)
    {
        if (!condition)
            throw new InvalidOperationException(detail);
    }

    [Serializable]
    private sealed class E01RegistryReport
    {
        public string scope;
        public bool passed;
        public bool receiptTypesExact;
        public bool adapterTypesExact;
        public bool descriptorTypesExact;
        public bool productQualityReceiptRegistered;
        public bool productQualityAdapterRegistered;
        public bool productQualityDescriptorRegistered;
        public bool productQualityOutcomeTypeRegistered;
        public bool historicalCurrentLineageExact;
        public string historicalClosurePath;
        public string historicalClosureExpectedSha256;
        public string historicalClosureSha256;
        public int historicalClosureBytes;
        public int historicalCandidateCount;
        public int historicalRowCount;
        public int historicalIndependentProductQualityRows;
        public int historicalProductQualityAuthorityReferences;
        public string[] expectedReceiptTypes = Array.Empty<string>();
        public string[] registeredReceiptTypes = Array.Empty<string>();
        public string[] expectedAdapterTypes = Array.Empty<string>();
        public string[] registeredAdapterTypes = Array.Empty<string>();
        public string[] expectedDescriptorTypes = Array.Empty<string>();
        public string[] registeredDescriptorTypes = Array.Empty<string>();
        public string[] registeredOutcomeTypes = Array.Empty<string>();
        public string[] missingReceiptTypes = Array.Empty<string>();
        public string[] unexpectedReceiptTypes = Array.Empty<string>();
        public string[] missingAdapterTypes = Array.Empty<string>();
        public string[] unexpectedAdapterTypes = Array.Empty<string>();
        public string[] missingDescriptorTypes = Array.Empty<string>();
        public string[] unexpectedDescriptorTypes = Array.Empty<string>();
        public int warningCount;
        public int errorCount;
        public string[] warnings = Array.Empty<string>();
        public string[] errors = Array.Empty<string>();
        public string failure;
        public bool fullAuditClosure;
        public bool humanApprovalClaimed;
        public bool trainingEligible;
    }
}
#endif
