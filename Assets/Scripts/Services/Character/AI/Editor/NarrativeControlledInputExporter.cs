#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Converts caller-authored controlled narrative cases through the live C# request
/// authorities and writes a create-only, response-free handoff. This is an Editor
/// fixture exporter; it never claims natural-play collection or training approval.
/// </summary>
public static class NarrativeControlledInputExporter
{
    private const int SchemaVersion = 1;
    private const string GeneratorId = "dungeonstory-controlled-input-bridge-v1";
    private const string Origin = "authored-controlled-scenario";
    private const string CatalogParentRelativePath =
        "Artifacts/Exports/NarrativeMechanicCatalog";

    private static readonly string[] BridgeSourcePaths =
    {
        "Assets/Scripts/Services/Character/AI/Editor/NarrativeControlledEquipmentCapture.cs",
        "Assets/Scripts/Services/Character/AI/Editor/NarrativeControlledFacilityCapture.cs",
        "Assets/Scripts/Services/Character/AI/Editor/NarrativeControlledInputExporter.cs",
        "Assets/Scripts/Services/Character/AI/Editor/NarrativeControlledSkillCapture.cs"
    };

    private static readonly string[] ScenarioFields =
    {
        "accepted",
        "authorityContext",
        "failureReason",
        "fullLegalCandidates",
        "profileId",
        "publicFacts",
        "publicNarrativeContext",
        "request",
        "scenarioId",
        "targetPersistentId"
    };

    private static readonly HashSet<string> ForbiddenOutputKeys = new(
        new[]
        {
            "expectedResponse",
            "response",
            "responseJson",
            "selectedCombinations",
            "validatedSkills"
        },
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Exports the exact input file against one explicit current catalog export.
    /// The output path must not exist. Returned JSON is a small completion summary.
    /// </summary>
    public static string Export(
        string inputPath,
        string outputDirectory,
        string catalogExportDirectory)
    {
        string projectRoot = GetProjectRoot();
        string resolvedInputPath = RequireExistingFile(inputPath, nameof(inputPath));
        string resolvedOutputDirectory = RequireNewDirectoryPath(
            outputDirectory,
            nameof(outputDirectory));
        string resolvedCatalogDirectory = RequireCatalogDirectory(
            projectRoot,
            catalogExportDirectory);

        byte[] inputBytes = File.ReadAllBytes(resolvedInputPath);
        JObject inputRoot = NarrativeControlledJson.ParseStrictObject(
            inputBytes,
            "controlled input");
        NarrativeControlledJson.RequireExactProperties(
            inputRoot,
            "controlled input root",
            "schemaVersion",
            "cases");
        int inputSchemaVersion = NarrativeControlledJson.RequireInt32(
            inputRoot,
            "schemaVersion",
            "controlled input root");
        if (inputSchemaVersion != SchemaVersion)
        {
            throw ExportFailure(
                "controlled-input-schema-version",
                $"Controlled input schemaVersion must be {SchemaVersion}; found {inputSchemaVersion}.");
        }

        JArray cases = NarrativeControlledJson.RequireArray(
            inputRoot,
            "cases",
            "controlled input root");
        if (cases.Count == 0)
        {
            throw ExportFailure(
                "controlled-input-cases-empty",
                "Controlled input requires at least one authored case.");
        }
        ValidateBatchIdentifiers(cases);

        CatalogPin catalog = CaptureCatalogPin(projectRoot, resolvedCatalogDirectory);
        string sourceInputSha256 = NarrativeControlledJson.Sha256Prefixed(inputBytes);
        List<JObject> acceptedRows = new();
        List<JObject> rejectedRows = new();
        HashSet<string> scenarioIds = new(StringComparer.Ordinal);

        foreach (JToken token in cases)
        {
            string authoredCaseSha256 = NarrativeControlledJson.Sha256Prefixed(
                NarrativeControlledJson.ToCanonicalUtf8(token));
            JObject inputCase = token as JObject;
            string caseId = TryReadString(inputCase, "caseId");
            string scenarioFamilyId = TryReadString(inputCase, "scenarioFamilyId");
            string profileId = TryReadString(inputCase, "profileId");
            try
            {
                if (inputCase == null)
                {
                    throw new ArgumentException("Each cases item must be a JSON object.");
                }

                JObject scenario = CaptureScenario(inputCase, profileId);
                ValidateScenario(inputCase, scenario, profileId);
                string scenarioId = NarrativeControlledJson.RequireString(
                    scenario,
                    "scenarioId",
                    "captured scenario");
                if (!scenarioIds.Add(scenarioId))
                {
                    throw ExportFailure(
                        "controlled-scenario-id-duplicate",
                        $"Captured scenarioId '{scenarioId}' is duplicated.");
                }

                JObject row = (JObject)scenario.DeepClone();
                AddWrapper(row, "caseId", caseId);
                AddWrapper(row, "scenarioFamilyId", scenarioFamilyId);
                AddWrapper(row, "authoredCaseSha256", authoredCaseSha256);
                AddWrapper(row, "origin", Origin);
                AddWrapper(row, "newUnityExecutionClaimed", true);
                AddWrapper(row, "naturalPlayClaimed", false);
                AddWrapper(row, "humanApprovalClaimed", false);
                AddWrapper(row, "trainingEligible", false);
                acceptedRows.Add(row);
            }
            catch (Exception exception) when (IsIndividualCaseFailure(exception))
            {
                rejectedRows.Add(new JObject
                {
                    ["caseId"] = caseId,
                    ["scenarioFamilyId"] = scenarioFamilyId,
                    ["authoredCaseSha256"] = authoredCaseSha256,
                    ["profileId"] = profileId,
                    ["accepted"] = false,
                    ["failureReason"] = CanonicalFailureReason(exception)
                });
            }
        }

        byte[] requestsBytes = BuildJsonLines(acceptedRows);
        byte[] rejectedBytes = BuildJsonLines(rejectedRows);
        JObject manifest = BuildManifest(
            sourceInputSha256,
            catalog,
            requestsBytes,
            rejectedBytes,
            acceptedRows.Count,
            rejectedRows.Count);
        byte[] manifestBytes = NarrativeControlledJson.ToCanonicalUtf8(manifest);
        WriteCreateOnlyDirectory(
            resolvedOutputDirectory,
            requestsBytes,
            rejectedBytes,
            manifestBytes);

        JObject summary = new JObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["outputDirectory"] = resolvedOutputDirectory.Replace('\\', '/'),
            ["manifestPath"] = Path.Combine(resolvedOutputDirectory, "manifest.json")
                .Replace('\\', '/'),
            ["acceptedCount"] = acceptedRows.Count,
            ["rejectedCount"] = rejectedRows.Count
        };
        return NarrativeControlledJson.ToCanonicalString(summary);
    }

    private static JObject CaptureScenario(JObject inputCase, string profileId)
    {
        return profileId switch
        {
            NarrativeMechanicScenarioProfiles.CharacterSkill =>
                NarrativeControlledSkillCapture.Capture(inputCase),
            NarrativeMechanicScenarioProfiles.FacilityEvolution =>
                NarrativeControlledFacilityCapture.Capture(inputCase),
            NarrativeMechanicScenarioProfiles.EquipmentChoice or
                NarrativeMechanicScenarioProfiles.EvolutionHistory =>
                NarrativeControlledEquipmentCapture.Capture(inputCase),
            _ => throw new ArgumentException(
                $"Unsupported controlled profileId '{profileId}'.")
        };
    }

    private static void ValidateScenario(
        JObject inputCase,
        JObject scenario,
        string expectedProfileId)
    {
        if (scenario == null)
        {
            throw new InvalidOperationException("Controlled capture returned null.");
        }
        NarrativeControlledJson.RequireExactProperties(
            scenario,
            "captured scenario",
            ScenarioFields);
        RejectForbiddenKeys(scenario);

        string profileId = NarrativeControlledJson.RequireString(
            scenario,
            "profileId",
            "captured scenario");
        if (!string.Equals(profileId, expectedProfileId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Captured profileId '{profileId}' does not match input '{expectedProfileId}'.");
        }
        if (!NarrativeControlledJson.RequireBoolean(
                scenario,
                "accepted",
                "captured scenario"))
        {
            throw new InvalidOperationException("A captured request scenario must be accepted=true.");
        }
        if (NarrativeControlledJson.RequireStringAllowEmpty(
                scenario,
                "failureReason",
                "captured scenario").Length != 0)
        {
            throw new InvalidOperationException(
                "A captured request scenario must have failureReason=\"\".");
        }

        JObject subject = NarrativeControlledJson.RequireObject(
            inputCase,
            "subject",
            "controlled case");
        string persistentId = NarrativeControlledJson.RequireString(
            subject,
            "persistentId",
            "controlled subject");
        string targetPersistentId = NarrativeControlledJson.RequireString(
            scenario,
            "targetPersistentId",
            "captured scenario");
        if (!string.Equals(targetPersistentId, persistentId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Captured target '{targetPersistentId}' does not match authored subject '{persistentId}'.");
        }

        JObject publicContext = NarrativeControlledJson.RequireObject(
            scenario,
            "publicNarrativeContext",
            "captured scenario");
        string publicSubjectId = NarrativeControlledJson.RequireString(
            publicContext,
            "subjectId",
            "captured publicNarrativeContext");
        if (!string.Equals(publicSubjectId, persistentId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Captured public narrative subject does not match the authored subject.");
        }

        JArray publicFacts = NarrativeControlledJson.RequireArray(
            scenario,
            "publicFacts",
            "captured scenario");
        if (publicFacts.Count == 0)
        {
            throw new InvalidOperationException("Captured publicFacts cannot be empty.");
        }
        string[] publicTexts = publicFacts
            .OfType<JObject>()
            .Select(value => value["text"]?.Type == JTokenType.String
                ? value.Value<string>("text")
                : string.Empty)
            .ToArray();
        string background = NarrativeControlledJson.RequireString(
            subject,
            "background",
            "controlled subject");
        if (!publicTexts.Any(text => text.IndexOf(background, StringComparison.Ordinal) >= 0))
        {
            throw new InvalidOperationException(
                "The authored subject background is missing from publicFacts.");
        }
        foreach (JObject inputEvent in NarrativeControlledJson.RequireArray(
                     inputCase,
                     "events",
                     "controlled case").OfType<JObject>())
        {
            string text = NarrativeControlledJson.RequireString(
                inputEvent,
                "text",
                "controlled event");
            if (!publicTexts.Any(publicText =>
                    publicText.IndexOf(text, StringComparison.Ordinal) >= 0))
            {
                throw new InvalidOperationException(
                    $"Authored event text is missing from publicFacts: '{text}'.");
            }
        }

        if (NarrativeControlledJson.RequireArray(
                scenario,
                "fullLegalCandidates",
                "captured scenario").Count == 0)
        {
            throw new InvalidOperationException("Captured fullLegalCandidates cannot be empty.");
        }
        NarrativeControlledJson.RequireObject(
            scenario,
            "request",
            "captured scenario");
        NarrativeControlledJson.RequireObject(
            scenario,
            "authorityContext",
            "captured scenario");
    }

    private static void RejectForbiddenKeys(JToken token)
    {
        IEnumerable<JToken> descendants = token is JContainer container
            ? container.DescendantsAndSelf()
            : new[] { token };
        foreach (JProperty property in descendants.OfType<JProperty>())
        {
            if (ForbiddenOutputKeys.Contains(property.Name))
            {
                throw new InvalidOperationException(
                    $"Captured request contains forbidden answer/witness key '{property.Name}'.");
            }
        }
    }

    private static void ValidateBatchIdentifiers(JArray cases)
    {
        Dictionary<string, int> caseIds = new(StringComparer.Ordinal);
        Dictionary<string, int> choiceInputHashes = new(StringComparer.Ordinal);
        Dictionary<string, string> familyByEntity = new(StringComparer.Ordinal);
        for (int index = 0; index < cases.Count; index++)
        {
            if (cases[index] is not JObject inputCase)
            {
                continue;
            }
            CountCanonicalIdentifier(inputCase, "caseId", index, caseIds);
            if (inputCase["state"] is JObject state)
            {
                CountCanonicalIdentifier(
                    state,
                    "choiceInputHash",
                    index,
                    choiceInputHashes);
            }
            string familyId = TryReadCanonicalIdentifier(inputCase, "scenarioFamilyId");
            if (familyId.Length == 0)
            {
                continue;
            }
            foreach (string entityId in EnumerateFamilyEntityIds(inputCase))
            {
                if (familyByEntity.TryGetValue(entityId, out string existingFamily)
                    && !string.Equals(existingFamily, familyId, StringComparison.Ordinal))
                {
                    throw ExportFailure(
                        "controlled-input-family-assignment-conflict",
                        $"Entity '{entityId}' is assigned to both scenario families "
                        + $"'{existingFamily}' and '{familyId}'.");
                }
                familyByEntity[entityId] = familyId;
            }
        }

        string duplicateCaseId = caseIds.FirstOrDefault(pair => pair.Value > 1).Key;
        if (!string.IsNullOrEmpty(duplicateCaseId))
        {
            throw ExportFailure(
                "controlled-input-case-id-duplicate",
                $"caseId '{duplicateCaseId}' occurs more than once.");
        }
        string duplicateChoiceInputHash = choiceInputHashes
            .FirstOrDefault(pair => pair.Value > 1).Key;
        if (!string.IsNullOrEmpty(duplicateChoiceInputHash))
        {
            throw ExportFailure(
                "controlled-input-reference-duplicate",
                $"choiceInputHash '{duplicateChoiceInputHash}' is referenced more than once.");
        }
    }

    private static IEnumerable<string> EnumerateFamilyEntityIds(JObject inputCase)
    {
        HashSet<string> result = new(StringComparer.Ordinal);
        if (inputCase["subject"] is JObject subject)
        {
            string subjectId = TryReadCanonicalIdentifier(subject, "persistentId");
            if (subjectId.Length > 0)
            {
                result.Add(subjectId);
            }
        }
        if (inputCase["state"] is JObject state)
        {
            string ownerId = TryReadCanonicalIdentifier(state, "ownerId");
            if (ownerId.Length > 0)
            {
                result.Add(ownerId);
            }
        }
        if (inputCase["events"] is JArray events)
        {
            foreach (JObject inputEvent in events.OfType<JObject>())
            {
                string actorId = TryReadCanonicalIdentifier(inputEvent, "actorId");
                if (actorId.Length > 0)
                {
                    result.Add(actorId);
                }
            }
        }
        return result;
    }

    private static string TryReadCanonicalIdentifier(JObject value, string propertyName)
    {
        string result = TryReadString(value, propertyName);
        return !string.IsNullOrWhiteSpace(result)
            && string.Equals(result, result.Trim(), StringComparison.Ordinal)
            && !result.Any(char.IsWhiteSpace)
            && !result.Any(char.IsControl)
                ? result
                : string.Empty;
    }

    private static void CountCanonicalIdentifier(
        JObject inputCase,
        string propertyName,
        int index,
        IDictionary<string, int> counts)
    {
        JToken token = inputCase[propertyName];
        if (token?.Type != JTokenType.String)
        {
            return;
        }
        string value = token.Value<string>();
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return;
        }
        counts.TryGetValue(value, out int count);
        counts[value] = count + 1;
    }

    private static CatalogPin CaptureCatalogPin(
        string projectRoot,
        string catalogDirectory)
    {
        string manifestPath = Path.Combine(catalogDirectory, "manifest.json");
        string catalogPath = Path.Combine(catalogDirectory, "catalog.json");
        if (!File.Exists(manifestPath) || !File.Exists(catalogPath))
        {
            throw ExportFailure(
                "controlled-catalog-files-missing",
                "The explicit catalog export requires manifest.json and catalog.json.");
        }

        byte[] manifestBytes = File.ReadAllBytes(manifestPath);
        byte[] catalogBytes = File.ReadAllBytes(catalogPath);
        JObject manifest = NarrativeControlledJson.ParseStrictObject(
            manifestBytes,
            "catalog manifest");
        JObject catalog = NarrativeControlledJson.ParseStrictObject(
            catalogBytes,
            "catalog document");

        int manifestSchema = NarrativeControlledJson.RequireInt32(
            manifest,
            "schemaVersion",
            "catalog manifest");
        if (manifestSchema != 2)
        {
            throw ExportFailure(
                "controlled-catalog-manifest-schema",
                $"Current controlled capture requires catalog manifest schemaVersion 2; found {manifestSchema}.");
        }
        JObject source = NarrativeControlledJson.RequireObject(
            manifest,
            "source",
            "catalog manifest");
        string authority = NarrativeControlledJson.RequireString(
            source,
            "authority",
            "catalog manifest source");
        if (!string.Equals(authority, "unity-editor-export", StringComparison.Ordinal))
        {
            throw ExportFailure(
                "controlled-catalog-authority",
                $"Catalog authority must be 'unity-editor-export'; found '{authority}'.");
        }

        string gameCommit = NarrativeControlledJson.RequireString(
            source,
            "gameCommit",
            "catalog manifest source");
        string currentCommit = RunGit(projectRoot, "rev-parse HEAD").Trim().ToLowerInvariant();
        if (!IsCommit(currentCommit)
            || !string.Equals(gameCommit, currentCommit, StringComparison.Ordinal))
        {
            throw ExportFailure(
                "controlled-catalog-commit-stale",
                $"Catalog gameCommit '{gameCommit}' does not match current HEAD '{currentCommit}'.");
        }

        string catalogHash = RequireSha256(
            NarrativeControlledJson.RequireString(
                manifest,
                "catalogHash",
                "catalog manifest"),
            "catalogHash");
        string catalogBytesSha256 = RequireSha256(
            NarrativeControlledJson.RequireString(
                manifest,
                "catalogBytesSha256",
                "catalog manifest"),
            "catalogBytesSha256");
        string actualCatalogBytesSha256 = NarrativeControlledJson.Sha256Prefixed(catalogBytes);
        if (!string.Equals(
                catalogBytesSha256,
                actualCatalogBytesSha256,
                StringComparison.Ordinal))
        {
            throw ExportFailure(
                "controlled-catalog-bytes-stale",
                "catalog.json bytes do not match manifest.catalogBytesSha256.");
        }

        string embeddedCatalogHash = NarrativeControlledJson.RequireString(
            catalog,
            "catalogHash",
            "catalog document");
        JObject catalogWithoutHash = (JObject)catalog.DeepClone();
        catalogWithoutHash.Property("catalogHash")?.Remove();
        string computedCatalogHash = NarrativeControlledJson.Sha256Prefixed(
            NarrativeControlledJson.ToCanonicalUtf8(catalogWithoutHash));
        if (!string.Equals(catalogHash, embeddedCatalogHash, StringComparison.Ordinal)
            || !string.Equals(catalogHash, computedCatalogHash, StringComparison.Ordinal))
        {
            throw ExportFailure(
                "controlled-catalog-hash-mismatch",
                "Catalog semantic hash does not match its manifest and canonical content.");
        }

        SortedDictionary<string, string> sourceFiles = new(StringComparer.Ordinal);
        foreach (JToken fileToken in NarrativeControlledJson.RequireArray(
                     manifest,
                     "files",
                     "catalog manifest"))
        {
            if (fileToken is not JObject sourceFile)
            {
                throw ExportFailure(
                    "controlled-catalog-source-entry",
                    "Catalog manifest files must contain only objects.");
            }
            string relativePath = NarrativeControlledJson.RequireString(
                sourceFile,
                "path",
                "catalog manifest source file").Replace('\\', '/');
            string expectedHash = RequireSha256(
                NarrativeControlledJson.RequireString(
                    sourceFile,
                    "sha256",
                    "catalog manifest source file"),
                "source sha256");
            if (sourceFiles.ContainsKey(relativePath))
            {
                throw ExportFailure(
                    "controlled-catalog-source-duplicate",
                    $"Catalog manifest repeats source path '{relativePath}'.");
            }
            string fullPath = ResolveProjectFile(projectRoot, relativePath);
            string actualHash = NarrativeControlledJson.Sha256Prefixed(File.ReadAllBytes(fullPath));
            if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
            {
                throw ExportFailure(
                    "controlled-catalog-source-stale",
                    $"Catalog source '{relativePath}' no longer matches its pinned hash.");
            }
            sourceFiles.Add(relativePath, actualHash);
        }
        if (sourceFiles.Count == 0)
        {
            throw ExportFailure(
                "controlled-catalog-sources-empty",
                "Catalog manifest contains no source provenance files.");
        }

        foreach (string bridgeSourcePath in BridgeSourcePaths)
        {
            string fullPath = ResolveProjectFile(projectRoot, bridgeSourcePath);
            sourceFiles[bridgeSourcePath] = NarrativeControlledJson.Sha256Prefixed(
                File.ReadAllBytes(fullPath));
        }
        return new CatalogPin(
            catalogHash,
            catalogBytesSha256,
            gameCommit,
            sourceFiles);
    }

    private static JObject BuildManifest(
        string sourceInputSha256,
        CatalogPin catalog,
        byte[] requestsBytes,
        byte[] rejectedBytes,
        int acceptedCount,
        int rejectedCount)
    {
        return new JObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["generatorId"] = GeneratorId,
            ["sourceInputSha256"] = sourceInputSha256,
            ["catalogHash"] = catalog.CatalogHash,
            ["catalogBytesSha256"] = catalog.CatalogBytesSha256,
            ["gameCommit"] = catalog.GameCommit,
            ["files"] = new JArray(
                BuildFileEntry("rejected.jsonl", rejectedBytes),
                BuildFileEntry("requests.jsonl", requestsBytes)),
            ["sourceFiles"] = new JArray(catalog.SourceFiles.Select(pair =>
                new JObject
                {
                    ["path"] = pair.Key,
                    ["sha256"] = pair.Value
                })),
            ["acceptedCount"] = acceptedCount,
            ["rejectedCount"] = rejectedCount,
            ["naturalPlayClaimed"] = false,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false,
            ["newUnityExecutionClaimed"] = true
        };
    }

    private static JObject BuildFileEntry(string path, byte[] bytes) => new()
    {
        ["path"] = path,
        ["sha256"] = NarrativeControlledJson.Sha256Prefixed(bytes),
        ["byteLength"] = bytes.LongLength
    };

    private static byte[] BuildJsonLines(IEnumerable<JObject> rows)
    {
        using MemoryStream stream = new();
        foreach (JObject row in rows)
        {
            byte[] bytes = NarrativeControlledJson.ToCanonicalUtf8(row);
            stream.Write(bytes, 0, bytes.Length);
            stream.WriteByte((byte)'\n');
        }
        return stream.ToArray();
    }

    private static void WriteCreateOnlyDirectory(
        string outputDirectory,
        byte[] requestsBytes,
        byte[] rejectedBytes,
        byte[] manifestBytes)
    {
        string parent = Path.GetDirectoryName(outputDirectory);
        if (string.IsNullOrWhiteSpace(parent))
        {
            throw ExportFailure(
                "controlled-output-parent-missing",
                $"Could not resolve output parent for '{outputDirectory}'.");
        }
        Directory.CreateDirectory(parent);
        if (Directory.Exists(outputDirectory) || File.Exists(outputDirectory))
        {
            throw ExportFailure(
                "controlled-output-collision",
                $"Output path already exists and will not be overwritten: '{outputDirectory}'.");
        }

        string stagingDirectory = Path.Combine(
            parent,
            $".{Path.GetFileName(outputDirectory)}.staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDirectory);
        try
        {
            WriteCreateNew(Path.Combine(stagingDirectory, "requests.jsonl"), requestsBytes);
            WriteCreateNew(Path.Combine(stagingDirectory, "rejected.jsonl"), rejectedBytes);
            WriteCreateNew(Path.Combine(stagingDirectory, "manifest.json"), manifestBytes);
            Directory.Move(stagingDirectory, outputDirectory);
        }
        catch
        {
            // Keep the unique failed staging directory as explicit audit evidence.
            throw;
        }
    }

    private static void WriteCreateNew(string path, byte[] bytes)
    {
        using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(true);
    }

    private static void AddWrapper(JObject row, string key, JToken value)
    {
        if (row.Property(key) != null)
        {
            throw new InvalidOperationException(
                $"Captured scenario already contains exporter-owned key '{key}'.");
        }
        row.Add(key, value);
    }

    private static string CanonicalFailureReason(Exception exception)
    {
        string code = exception is NarrativeMechanicCatalogExportException exportException
            ? exportException.Code
            : exception.GetType().Name;
        string message = (exception.Message ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        string result = string.IsNullOrEmpty(message) ? code : code + ": " + message;
        if (result.Length <= 2000)
        {
            return result;
        }
        int length = char.IsHighSurrogate(result[1999]) ? 1999 : 2000;
        return result.Substring(0, length);
    }

    private static bool IsIndividualCaseFailure(Exception exception)
    {
        if (exception is NarrativeMechanicCatalogExportException)
        {
            return exception is NarrativeMechanicCatalogUnsupportedSourceException;
        }
        return exception is ArgumentException
            || exception is FormatException
            || exception is OverflowException
            || exception is InvalidOperationException;
    }

    private static string TryReadString(JObject value, string propertyName) =>
        value?[propertyName]?.Type == JTokenType.String
            ? value.Value<string>(propertyName) ?? string.Empty
            : string.Empty;

    private static string RequireExistingFile(string path, string parameterName)
    {
        string fullPath = RequireFullPath(path, parameterName);
        if (!File.Exists(fullPath))
        {
            throw ExportFailure(
                "controlled-input-file-missing",
                $"Input file does not exist: '{fullPath}'.");
        }
        return fullPath;
    }

    private static string RequireNewDirectoryPath(string path, string parameterName)
    {
        string fullPath = RequireFullPath(path, parameterName)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (Directory.Exists(fullPath) || File.Exists(fullPath))
        {
            throw ExportFailure(
                "controlled-output-collision",
                $"Output path already exists and will not be overwritten: '{fullPath}'.");
        }
        return fullPath;
    }

    private static string RequireCatalogDirectory(string projectRoot, string path)
    {
        string fullPath = RequireFullPath(path, nameof(path))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Directory.Exists(fullPath))
        {
            throw ExportFailure(
                "controlled-catalog-directory-missing",
                $"Catalog export directory does not exist: '{fullPath}'.");
        }
        string expectedParent = Path.GetFullPath(Path.Combine(
            projectRoot,
            CatalogParentRelativePath.Replace('/', Path.DirectorySeparatorChar)))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string actualParent = Path.GetDirectoryName(fullPath)?.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) ?? string.Empty;
        if (!string.Equals(expectedParent, actualParent, StringComparison.OrdinalIgnoreCase))
        {
            throw ExportFailure(
                "controlled-catalog-directory-not-explicit-export",
                $"Catalog directory must be one explicit child of '{expectedParent}'.");
        }
        return fullPath;
    }

    private static string RequireFullPath(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A non-blank path is required.", parameterName);
        }
        return Path.GetFullPath(path.Trim());
    }

    private static string ResolveProjectFile(string projectRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathRooted(relativePath))
        {
            throw ExportFailure(
                "controlled-source-path-invalid",
                $"Source path must be project-relative: '{relativePath}'.");
        }
        string normalizedRoot = Path.GetFullPath(projectRoot).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(Path.Combine(
            projectRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(fullPath))
        {
            throw ExportFailure(
                "controlled-source-file-missing",
                $"Pinned source path is missing or outside the project: '{relativePath}'.");
        }
        return fullPath;
    }

    private static string RequireSha256(string value, string label)
    {
        if (value == null
            || value.Length != 71
            || !value.StartsWith("sha256:", StringComparison.Ordinal)
            || value.Substring(7).Any(character =>
                !(character >= '0' && character <= '9')
                && !(character >= 'a' && character <= 'f')))
        {
            throw ExportFailure(
                "controlled-sha256-invalid",
                $"{label} is not a lowercase sha256:<64 hex> value.");
        }
        return value;
    }

    private static bool IsCommit(string value) => value != null
        && value.Length == 40
        && value.All(character =>
            (character >= '0' && character <= '9')
            || (character >= 'a' && character <= 'f'));

    private static string GetProjectRoot()
    {
        DirectoryInfo parent = Directory.GetParent(Path.GetFullPath(Application.dataPath));
        return parent?.FullName ?? throw ExportFailure(
            "controlled-project-root-missing",
            "Could not resolve the Unity project root from Application.dataPath.");
    }

    private static string RunGit(string projectRoot, string arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "git",
            Arguments = $"-C \"{projectRoot.Replace("\"", "\\\"")}\" {arguments}",
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardErrorEncoding = new UTF8Encoding(false, true),
            StandardOutputEncoding = new UTF8Encoding(false, true),
            UseShellExecute = false
        };
        using Process process = Process.Start(startInfo);
        if (process == null)
        {
            throw ExportFailure(
                "controlled-git-start-failed",
                "Could not start git for controlled export provenance.");
        }
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw ExportFailure(
                "controlled-git-command-failed",
                $"git {arguments} failed with exit code {process.ExitCode}: {error.Trim()}");
        }
        return output;
    }

    private static NarrativeMechanicCatalogExportException ExportFailure(
        string code,
        string message) => new(code, message);

    private sealed class CatalogPin
    {
        public CatalogPin(
            string catalogHash,
            string catalogBytesSha256,
            string gameCommit,
            IReadOnlyDictionary<string, string> sourceFiles)
        {
            CatalogHash = catalogHash;
            CatalogBytesSha256 = catalogBytesSha256;
            GameCommit = gameCommit;
            SourceFiles = sourceFiles;
        }

        public string CatalogHash { get; }
        public string CatalogBytesSha256 { get; }
        public string GameCommit { get; }
        public IReadOnlyDictionary<string, string> SourceFiles { get; }
    }
}

/// <summary>
/// Strict JSON and canonical hashing helpers shared only by the controlled bridge.
/// Canonical objects use Unicode-scalar key order, compact UTF-8, no BOM, and
/// signed Int64 JSON numbers, matching the Python sorted-key compact contract.
/// </summary>
internal static class NarrativeControlledJson
{
    private static readonly UTF8Encoding Utf8NoBomStrict = new(false, true);

    internal static JObject ParseStrictObject(byte[] bytes, string label)
    {
        if (bytes == null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }
        string json = Utf8NoBomStrict.GetString(bytes);
        ValidateStrictLexemes(json, label);
        using StringReader stringReader = new(json);
        using JsonTextReader reader = new(stringReader)
        {
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Decimal,
            MaxDepth = 128,
            SupportMultipleContent = false
        };
        JsonLoadSettings settings = new()
        {
            CommentHandling = CommentHandling.Load,
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
            LineInfoHandling = LineInfoHandling.Load
        };
        JObject result = JObject.Load(reader, settings);
        if (reader.Read())
        {
            throw new JsonReaderException($"{label} contains trailing JSON content.");
        }
        RejectComments(result, label);
        return result;
    }

    internal static void RequireExactProperties(
        JObject value,
        string label,
        params string[] expected)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        HashSet<string> expectedSet = new(expected, StringComparer.Ordinal);
        string unknown = value.Properties()
            .Select(property => property.Name)
            .FirstOrDefault(name => !expectedSet.Contains(name));
        if (!string.IsNullOrEmpty(unknown))
        {
            throw new ArgumentException($"{label} has unsupported field '{unknown}'.");
        }
        string missing = expected.FirstOrDefault(name => value.Property(name) == null);
        if (!string.IsNullOrEmpty(missing))
        {
            throw new ArgumentException($"{label} is missing required field '{missing}'.");
        }
    }

    internal static string RequireString(
        JObject value,
        string propertyName,
        string label)
    {
        string result = RequireStringAllowEmpty(value, propertyName, label);
        if (string.IsNullOrWhiteSpace(result)
            || !string.Equals(result, result.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"{label}.{propertyName} must be a non-blank already-trimmed string.");
        }
        return result;
    }

    internal static string RequireIdentifier(
        JObject value,
        string propertyName,
        string label,
        int maximumLength = 512)
    {
        string result = RequireString(value, propertyName, label);
        if (result.Length > maximumLength
            || result.Any(char.IsWhiteSpace)
            || result.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"{label}.{propertyName} must be a canonical whitespace-free identifier of at most {maximumLength} characters.");
        }
        return result;
    }

    internal static string RequireStringAllowEmpty(
        JObject value,
        string propertyName,
        string label)
    {
        JToken token = RequireProperty(value, propertyName, label);
        if (token.Type != JTokenType.String)
        {
            throw new ArgumentException($"{label}.{propertyName} must be a JSON string.");
        }
        string result = token.Value<string>() ?? string.Empty;
        if (result.Any(character => char.IsSurrogate(character)))
        {
            NarrativeMechanicCatalogCanonicalJson.RequireWellFormedUtf16(
                result,
                $"{label}.{propertyName}");
        }
        return result;
    }

    internal static int RequireInt32(
        JObject value,
        string propertyName,
        string label)
    {
        JToken token = RequireProperty(value, propertyName, label);
        if (token.Type != JTokenType.Integer)
        {
            throw new ArgumentException($"{label}.{propertyName} must be an integer JSON number.");
        }
        try
        {
            return token.Value<int>();
        }
        catch (Exception exception) when (exception is OverflowException or FormatException)
        {
            throw new ArgumentException(
                $"{label}.{propertyName} is outside Int32 range.",
                exception);
        }
    }

    internal static bool RequireBoolean(
        JObject value,
        string propertyName,
        string label)
    {
        JToken token = RequireProperty(value, propertyName, label);
        if (token.Type != JTokenType.Boolean)
        {
            throw new ArgumentException($"{label}.{propertyName} must be a JSON boolean.");
        }
        return token.Value<bool>();
    }

    internal static JObject RequireObject(
        JObject value,
        string propertyName,
        string label)
    {
        JToken token = RequireProperty(value, propertyName, label);
        return token as JObject ?? throw new ArgumentException(
            $"{label}.{propertyName} must be a JSON object.");
    }

    internal static JArray RequireArray(
        JObject value,
        string propertyName,
        string label)
    {
        JToken token = RequireProperty(value, propertyName, label);
        return token as JArray ?? throw new ArgumentException(
            $"{label}.{propertyName} must be a JSON array.");
    }

    internal static byte[] ToCanonicalUtf8(JToken value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        StringBuilder builder = new();
        WriteCanonical(builder, value);
        return Utf8NoBomStrict.GetBytes(builder.ToString());
    }

    internal static string ToCanonicalString(JToken value) =>
        Utf8NoBomStrict.GetString(ToCanonicalUtf8(value));

    internal static string Sha256Prefixed(byte[] bytes)
    {
        using SHA256 sha256 = SHA256.Create();
        return "sha256:" + string.Concat(sha256.ComputeHash(bytes)
            .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static JToken RequireProperty(
        JObject value,
        string propertyName,
        string label)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        return value[propertyName] ?? throw new ArgumentException(
            $"{label} is missing required field '{propertyName}'.");
    }

    private static void WriteCanonical(StringBuilder builder, JToken value)
    {
        switch (value.Type)
        {
            case JTokenType.Object:
                builder.Append('{');
                bool firstProperty = true;
                foreach (JProperty property in ((JObject)value).Properties()
                             .OrderBy(
                                 property => property.Name,
                                 NarrativeMechanicCatalogCanonicalJson.UnicodeScalarOrdinalComparer))
                {
                    if (!firstProperty)
                    {
                        builder.Append(',');
                    }
                    firstProperty = false;
                    NarrativeMechanicCatalogCanonicalJson.WriteString(builder, property.Name);
                    builder.Append(':');
                    WriteCanonical(builder, property.Value);
                }
                builder.Append('}');
                return;
            case JTokenType.Array:
                builder.Append('[');
                bool firstValue = true;
                foreach (JToken item in (JArray)value)
                {
                    if (!firstValue)
                    {
                        builder.Append(',');
                    }
                    firstValue = false;
                    WriteCanonical(builder, item);
                }
                builder.Append(']');
                return;
            case JTokenType.Integer:
                long integer;
                try
                {
                    integer = value.Value<long>();
                }
                catch (Exception exception) when (exception is OverflowException or FormatException)
                {
                    throw new ArgumentException(
                        "Canonical JSON integers must fit signed Int64.",
                        exception);
                }
                builder.Append(integer.ToString(CultureInfo.InvariantCulture));
                return;
            case JTokenType.String:
                NarrativeMechanicCatalogCanonicalJson.WriteString(
                    builder,
                    value.Value<string>() ?? string.Empty);
                return;
            case JTokenType.Boolean:
                builder.Append(value.Value<bool>() ? "true" : "false");
                return;
            case JTokenType.Null:
                builder.Append("null");
                return;
            default:
                throw new ArgumentException(
                    $"Canonical controlled JSON does not allow token type {value.Type}; decimal quantities must be strings.");
        }
    }

    private static void RejectComments(JToken value, string label)
    {
        IEnumerable<JToken> descendants = value is JContainer container
            ? container.DescendantsAndSelf()
            : new[] { value };
        if (descendants.Any(token => token.Type == JTokenType.Comment))
        {
            throw new JsonReaderException($"{label} cannot contain comments.");
        }
    }

    private static void ValidateStrictLexemes(string json, string label)
    {
        if (json.Length > 0 && json[0] == '\ufeff')
        {
            throw new JsonReaderException($"{label} must be UTF-8 without a BOM.");
        }
        bool inString = false;
        bool escaped = false;
        for (int index = 0; index < json.Length; index++)
        {
            char character = json[index];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }
                else if (character < 0x20)
                {
                    throw new JsonReaderException(
                        $"{label} contains an unescaped control character.");
                }
                continue;
            }

            if (character == '"')
            {
                inString = true;
                continue;
            }
            if (char.IsWhiteSpace(character)
                && character != ' '
                && character != '\t'
                && character != '\r'
                && character != '\n')
            {
                throw new JsonReaderException(
                    $"{label} contains whitespace that is not valid JSON whitespace.");
            }
            if (character == '\'' || character == '/')
            {
                throw new JsonReaderException(
                    $"{label} contains non-standard JSON quoting or comments.");
            }
            if (character == '-' || char.IsDigit(character))
            {
                index = ValidateIntegerLexeme(json, index, label);
                continue;
            }
            if (character == ',')
            {
                int next = index + 1;
                while (next < json.Length && char.IsWhiteSpace(json[next]))
                {
                    next++;
                }
                if (next < json.Length && (json[next] == '}' || json[next] == ']'))
                {
                    throw new JsonReaderException($"{label} contains a trailing comma.");
                }
            }
        }
        if (inString || escaped)
        {
            throw new JsonReaderException($"{label} contains an unterminated string.");
        }
    }

    private static int ValidateIntegerLexeme(string json, int start, string label)
    {
        int index = start;
        if (json[index] == '-')
        {
            index++;
            if (index >= json.Length || !char.IsDigit(json[index]))
            {
                throw new JsonReaderException($"{label} contains an invalid JSON number.");
            }
        }
        int integerStart = index;
        if (json[index] == '0')
        {
            index++;
            if (index < json.Length && char.IsDigit(json[index]))
            {
                throw new JsonReaderException(
                    $"{label} contains a JSON number with a leading zero.");
            }
        }
        else
        {
            while (index < json.Length && char.IsDigit(json[index]))
            {
                index++;
            }
        }
        if (index == integerStart
            || (index < json.Length
                && (json[index] == '.' || json[index] == 'e' || json[index] == 'E')))
        {
            throw new JsonReaderException(
                $"{label} permits integer JSON numbers only; decimal quantities must be strings.");
        }
        if (index < json.Length
            && !char.IsWhiteSpace(json[index])
            && json[index] != ','
            && json[index] != ']'
            && json[index] != '}')
        {
            throw new JsonReaderException($"{label} contains an invalid JSON number token.");
        }
        return index - 1;
    }
}
#endif
