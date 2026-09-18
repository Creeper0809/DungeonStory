#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class Wim037GuestRequestCatalogExclusionDebugScenarios
{
    private const string ReportId = "wim-037-guest-request-catalog-exclusion";
    private const string CatalogPath =
        "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private const string CircusAssetPath =
        "Assets/Resources/SO/V20/Society/Services/GuestRequests/"
        + "guest-request_monster-circus.asset";
    private const string CircusGuid = "0a8cf11180ed6ca4d98949682d49b378";
    private const string ResourcesRequestPath =
        "SO/V20/Society/Services/GuestRequests";

    public static string RunAll()
    {
        List<string> rows = new();
        int passed = 0;

        Run(rows, ref passed, "CATALOG_EQUALS_ACTIVE_MANIFEST_AND_EXCLUDES_CIRCUS",
            VerifyCatalogEqualsActiveManifestAndExcludesCircus);
        Run(rows, ref passed, "RETIRED_CIRCUS_ASSET_AND_GUID_RETAINED_ON_DISK",
            VerifyRetiredCircusAssetAndGuidRetainedOnDisk);

        rows.Add($"{ReportId}; RESULT={(passed == rows.Count ? "PASS" : "FAIL")}; "
            + $"passed={passed}; failed={rows.Count - passed}; rows={rows.Count}");
        return string.Join(Environment.NewLine, rows);
    }

    [MenuItem("DungeonStory/WIM-037/Run Guest Request Catalog Exclusion Scenarios")]
    public static void RunFromMenu()
    {
        Debug.Log(RunAll());
    }

    private static void VerifyCatalogEqualsActiveManifestAndExcludesCircus()
    {
        GameDomainContentCatalogSO catalog =
            AssetDatabase.LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath);
        Require(catalog != null, "Game-domain content catalog is missing.");

        string[] publishedIds = catalog.GetAll<GuestRequestDefinitionSO>()
            .Select(request => request.StableId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        string[] manifestIds = ReadActiveManifestIds();

        Require(publishedIds.Length == 13
            && manifestIds.Length == 13
            && !publishedIds.Contains("guest-request:monster-circus", StringComparer.Ordinal)
            && publishedIds.SequenceEqual(manifestIds, StringComparer.Ordinal),
            "Published guest-request catalog must be exactly the active 13-entry manifest."
        );
    }

    private static void VerifyRetiredCircusAssetAndGuidRetainedOnDisk()
    {
        GuestRequestDefinitionSO circus =
            AssetDatabase.LoadAssetAtPath<GuestRequestDefinitionSO>(CircusAssetPath);
        GuestRequestDefinitionSO[] diskRequests =
            Resources.LoadAll<GuestRequestDefinitionSO>(ResourcesRequestPath);

        Require(circus != null
            && circus.StableId == "guest-request:monster-circus"
            && AssetDatabase.AssetPathToGUID(CircusAssetPath) == CircusGuid
            && diskRequests.Length == 14
            && diskRequests.Any(request => request != null
                && request.StableId == "guest-request:monster-circus"),
            "Retired circus request must remain intact on disk while excluded from the catalog."
        );
    }

    private static string[] ReadActiveManifestIds()
    {
        FieldInfo requestsField = typeof(V20FactionServiceContentAssetBuilder).GetField(
            "Requests",
            BindingFlags.Static | BindingFlags.NonPublic);
        IEnumerable requests = requestsField?.GetValue(null) as IEnumerable;
        Require(requests != null, "Guest-request manifest is missing.");

        List<string> ids = new();
        foreach (object spec in requests)
        {
            FieldInfo idField = spec?.GetType().GetField(
                "Id",
                BindingFlags.Instance | BindingFlags.Public);
            string id = idField?.GetValue(spec) as string;
            Require(!string.IsNullOrWhiteSpace(id),
                "Guest-request manifest contains an invalid stable ID.");
            ids.Add(id);
        }

        return ids.OrderBy(id => id, StringComparer.Ordinal).ToArray();
    }

    private static void Run(
        ICollection<string> rows,
        ref int passed,
        string name,
        Action scenario)
    {
        try
        {
            scenario();
            rows.Add(name + "=PASS");
            passed++;
        }
        catch (Exception exception)
        {
            rows.Add(name + "=FAIL; " + exception.Message);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
