#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Buildings;
using UnityEditor;
using UnityEngine;

public static class Wim037041VenueAuthoringDebugScenarios
{
    private const string CatalogPath =
        "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private const string GuestRoot =
        "Assets/Resources/SO/V20/Society/Services/GuestRequests";
    private const string FestivalRoot =
        "Assets/Resources/SO/V20/Society/Festivals";

    private static readonly VenueExpectation[] Guests =
    {
        new("guest-request:coronation-feast", FacilityRole.Meal, FacilityRole.Meal, seats: 4, tables: 4, service: 1),
        new("guest-request:allergen-banquet", FacilityRole.Meal, FacilityRole.Meal, seats: 8, tables: 8, service: 1),
        new("guest-request:emergency-surgery", FacilityRole.Medical, FacilityRole.Medical, emergency: true),
        new("guest-request:plague-screening", FacilityRole.None, FacilityRole.None, anchorIds: new[] { "building:8874" }),
        new("guest-request:precision-barter", FacilityRole.Purchase, FacilityRole.Purchase),
        new("guest-request:winter-fuel-auction", FacilityRole.Entertainment, FacilityRole.Entertainment, eventCells: 3),
        new("guest-request:memorial-performance", FacilityRole.None, FacilityRole.None, anchorIds: new[] { "building:8887" }),
        new("guest-request:flood-refuge", FacilityRole.Rest, FacilityRole.Rest, beds: 4, resident: true),
        new("guest-request:persecuted-family", FacilityRole.None, FacilityRole.None, anchorIds: new[] { "building:8883" }),
        new("guest-request:sealed-archive", FacilityRole.None, FacilityRole.None, anchorIds: new[] { "building:8825" }),
        new("guest-request:disease-sample", FacilityRole.None, FacilityRole.None, anchorIds: new[] { "building:8876" }),
        new("guest-request:militia-arms", FacilityRole.Logistics, FacilityRole.Logistics),
        new("guest-request:bodyguard-kit", FacilityRole.Logistics, FacilityRole.Logistics)
    };

    private static readonly VenueExpectation[] Festivals =
    {
        new("festival:sprout", FacilityRole.Meal, FacilityRole.Meal, seats: 8, tables: 8, service: 1, eventCells: 8),
        new("festival:high-sun", FacilityRole.Meal, FacilityRole.Meal, seats: 8, tables: 8, service: 1, eventCells: 8),
        new("festival:storage", FacilityRole.Meal, FacilityRole.Meal, seats: 10, tables: 10, service: 1, eventCells: 10),
        new("festival:long-night-memorial", FacilityRole.None, FacilityRole.None, eventCells: 10, anchorIds: new[] { "building:8887" }),
        new("festival:frontier-map-night", FacilityRole.None, FacilityRole.None, eventCells: 6, anchorIds: new[] { "building:1047" }),
        new("festival:pack-first-hunt", FacilityRole.Meal, FacilityRole.Meal, seats: 8, tables: 8, service: 1, eventCells: 8),
        new("festival:ash-oath", FacilityRole.Entertainment, FacilityRole.Entertainment, eventCells: 6),
        new("festival:core-resonance", FacilityRole.None, FacilityRole.None, eventCells: 5, anchorIds: new[] { "building:9826" }),
        new("festival:open-sky-chorus", FacilityRole.None, FacilityRole.None, eventCells: 6, anchorIds: new[] { "building:8851" }),
        new("festival:tool-clan-fair", FacilityRole.None, FacilityRole.None, eventCells: 6, anchorIds: new[] { "building:8861" }),
        new("festival:spore-bloom", FacilityRole.None, FacilityRole.None, eventCells: 6, anchorIds: new[] { "building:8803" }),
        new("festival:weapon-vigil", FacilityRole.None, FacilityRole.None, eventCells: 8, anchorIds: new[] { "building:1055" }, roomAnchorIds: new[] { "building:8830" }),
        new("festival:clear-confluence", FacilityRole.None, FacilityRole.Hygiene, eventCells: 8, wet: true, anchorIds: new[] { "building:1060" }),
        new("festival:blood-lantern", FacilityRole.None, FacilityRole.None, eventCells: 6, anchorIds: new[] { "building:8887" }),
        new("festival:many-tables", FacilityRole.Meal, FacilityRole.Meal, seats: 20, tables: 20, service: 1, eventCells: 20),
        new("festival:dungeon-accord-day", FacilityRole.Entertainment, FacilityRole.Entertainment, eventCells: 12)
    };

    public static string RunAll()
    {
        List<string> rows = new();
        int passed = 0;
        Run(rows, ref passed, "GUEST_CATALOG_13_HAS_EXACT_AUTHORED_VENUES", VerifyGuests);
        Run(rows, ref passed, "FESTIVAL_CATALOG_16_HAS_EXACT_AUTHORED_VENUES", VerifyFestivals);
        Run(rows, ref passed, "LEGACY_FESTIVAL_DISK4_IS_NOT_PUBLISHED", VerifyLegacyFestivals);
        Run(rows, ref passed, "BUILDER_MANIFESTS_MATCH_PUBLISHED_VENUES", VerifyBuilderManifests);
        rows.Add($"wim-037-041-venue-authoring; RESULT={(passed == rows.Count ? "PASS" : "FAIL")}; passed={passed}; failed={rows.Count - passed}; rows={rows.Count}");
        return string.Join(Environment.NewLine, rows);
    }

    [MenuItem("DungeonStory/WIM-037-041/Run Venue Authoring Scenarios")]
    public static void RunFromMenu() => Debug.Log(RunAll());

    private static void VerifyGuests()
    {
        GameDomainContentCatalogSO catalog = RequireCatalog();
        GuestRequestDefinitionSO[] published = catalog.GetAll<GuestRequestDefinitionSO>().ToArray();
        RequireExactIds(published.Select(value => value.StableId), Guests.Select(value => value.Id), "guest-request catalog");
        foreach (VenueExpectation expected in Guests)
        {
            GuestRequestDefinitionSO asset = AssetDatabase.LoadAssetAtPath<GuestRequestDefinitionSO>(
                $"{GuestRoot}/{expected.Id.Replace(':', '_')}.asset");
            Require(asset != null && published.Contains(asset), $"'{expected.Id}' must be a published V20 guest asset.");
            Require((asset.serviceRequirements?.facilities?.Count ?? 0) == 0, $"'{expected.Id}' retains a duplicate legacy facility requirement.");
            RequireVenue(asset.venueRequirements, expected);
        }
    }

    private static void VerifyFestivals()
    {
        GameDomainContentCatalogSO catalog = RequireCatalog();
        FestivalDefinitionSO[] published = catalog.GetAll<FestivalDefinitionSO>().ToArray();
        RequireExactIds(published.Select(value => value.StableId), Festivals.Select(value => value.Id), "festival catalog");
        foreach (VenueExpectation expected in Festivals)
        {
            FestivalDefinitionSO asset = AssetDatabase.LoadAssetAtPath<FestivalDefinitionSO>(
                $"{FestivalRoot}/{expected.Id.Replace(':', '_')}.asset");
            Require(asset != null && published.Contains(asset), $"'{expected.Id}' must be a published V20 festival asset.");
            Require(string.IsNullOrEmpty(asset.requiredBuildingDefinitionId), $"'{expected.Id}' retains a duplicate legacy facility string.");
            RequireVenue(asset.venueRequirements, expected);
        }
    }

    private static void VerifyLegacyFestivals()
    {
        GameDomainContentCatalogSO catalog = RequireCatalog();
        FestivalDefinitionSO[] published = catalog.GetAll<FestivalDefinitionSO>().ToArray();
        string[] legacyIds = { "festival:sprout", "festival:high-sun", "festival:storage", "festival:long-night-memorial" };
        string[] legacyPaths = legacyIds.Select(id =>
            $"Assets/Resources/SO/Population/Festivals/{id.Replace(':', '_')}.asset").ToArray();
        FestivalDefinitionSO[] disk = Resources.LoadAll<FestivalDefinitionSO>("SO");
        Require(disk.Length == 20, "Festival disk denominator must remain 20.");
        foreach (string path in legacyPaths)
        {
            FestivalDefinitionSO legacy = AssetDatabase.LoadAssetAtPath<FestivalDefinitionSO>(path);
            Require(legacy != null && !published.Contains(legacy), $"Legacy festival '{path}' must remain on disk but unpublished.");
        }
    }

    private static void VerifyBuilderManifests()
    {
        VerifyManifest(typeof(V20FactionServiceContentAssetBuilder), "Requests", Guests);
        VerifyManifest(typeof(V20SocietyWorldContentAssetBuilder), "Festivals", Festivals);
    }

    private static void VerifyManifest(Type builderType, string fieldName, IEnumerable<VenueExpectation> expected)
    {
        FieldInfo manifestField = builderType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        IEnumerable manifest = manifestField?.GetValue(null) as IEnumerable;
        Require(manifest != null, $"{builderType.Name}.{fieldName} is missing.");
        Dictionary<string, VenueExpectation> expectedById = expected.ToDictionary(value => value.Id, StringComparer.Ordinal);
        int count = 0;
        foreach (object spec in manifest)
        {
            Type type = spec?.GetType();
            string id = type?.GetField("Id", BindingFlags.Instance | BindingFlags.Public)?.GetValue(spec) as string;
            FacilityVenueRequirements venue = type?.GetField("Venue", BindingFlags.Instance | BindingFlags.Public)?.GetValue(spec) as FacilityVenueRequirements;
            if (id == null || !expectedById.TryGetValue(id, out VenueExpectation expectation))
                throw new InvalidOperationException($"{builderType.Name} contains an unexpected venue manifest ID.");
            RequireVenue(venue, expectation);
            count++;
        }
        Require(count == expectedById.Count, $"{builderType.Name} venue manifest count changed.");
    }

    private static void RequireVenue(FacilityVenueRequirements actual, VenueExpectation expected)
    {
        Require(actual != null && actual.Validate(expected.Id).Count == 0, $"'{expected.Id}' venue is invalid.");
        Require(actual.anchor != null
            && actual.anchor.facilityRoles == expected.AnchorRoles
            && SameIds(actual.anchor.exactBuildingDefinitionIds, expected.AnchorIds)
            && actual.requiredRoomFacilityRoles == expected.RoomRoles
            && actual.minimumSeats == expected.Seats
            && actual.minimumTables == expected.Tables
            && actual.minimumServiceCapacity == expected.ServiceCapacity
            && actual.minimumEventCells == expected.EventCells
            && actual.minimumVacantBeds == expected.VacantBeds
            && actual.requireResidentHeadroom == expected.ResidentHeadroom
            && actual.requireWetBath == expected.WetBath
            && actual.requireEmergencySurgeryFacility == expected.EmergencySurgery
            && SameRoomAnchors(actual.requiredRoomFacilities, expected.RoomAnchorIds),
            $"'{expected.Id}' venue differs from the approved authored mapping.");
    }

    private static bool SameRoomAnchors(IReadOnlyList<FacilityVenueAnchorSelector> actual, IReadOnlyList<string> expected)
    {
        if ((actual?.Count ?? 0) != expected.Count) return false;
        for (int index = 0; index < expected.Count; index++)
        {
            FacilityVenueAnchorSelector selector = actual[index];
            if (selector == null || selector.facilityRoles != FacilityRole.None || !SameIds(selector.exactBuildingDefinitionIds, new[] { expected[index] })) return false;
        }
        return true;
    }

    private static bool SameIds(IEnumerable<string> actual, IEnumerable<string> expected) =>
        (actual ?? Array.Empty<string>()).SequenceEqual(expected ?? Array.Empty<string>(), StringComparer.Ordinal);

    private static GameDomainContentCatalogSO RequireCatalog() =>
        AssetDatabase.LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath)
        ?? throw new InvalidOperationException("Game-domain content catalog is missing.");

    private static void RequireExactIds(IEnumerable<string> actual, IEnumerable<string> expected, string label)
    {
        string[] actualIds = actual.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        string[] expectedIds = expected.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        Require(actualIds.SequenceEqual(expectedIds, StringComparer.Ordinal), $"{label} must have exact approved coverage.");
    }

    private static void Run(ICollection<string> rows, ref int passed, string name, Action scenario)
    {
        try { scenario(); rows.Add(name + "=PASS"); passed++; }
        catch (Exception exception) { rows.Add(name + "=FAIL; " + exception.Message); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class VenueExpectation
    {
        public VenueExpectation(string id, FacilityRole anchorRoles, FacilityRole roomRoles, int seats = 0, int tables = 0, int service = 0, int eventCells = 1, int beds = 0, bool resident = false, bool wet = false, bool emergency = false, string[] anchorIds = null, string[] roomAnchorIds = null)
        {
            Id = id; AnchorRoles = anchorRoles; RoomRoles = roomRoles; Seats = seats; Tables = tables; ServiceCapacity = service; EventCells = eventCells; VacantBeds = beds; ResidentHeadroom = resident; WetBath = wet; EmergencySurgery = emergency; AnchorIds = anchorIds ?? Array.Empty<string>(); RoomAnchorIds = roomAnchorIds ?? Array.Empty<string>();
        }

        public string Id { get; }
        public FacilityRole AnchorRoles { get; }
        public FacilityRole RoomRoles { get; }
        public int Seats { get; }
        public int Tables { get; }
        public int ServiceCapacity { get; }
        public int EventCells { get; }
        public int VacantBeds { get; }
        public bool ResidentHeadroom { get; }
        public bool WetBath { get; }
        public bool EmergencySurgery { get; }
        public IReadOnlyList<string> AnchorIds { get; }
        public IReadOnlyList<string> RoomAnchorIds { get; }
    }
}
#endif
