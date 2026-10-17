#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Buildings;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

// Root-owned query integration, run inside the existing isolated actual guest
// UI/haul witness. It does not turn query acceptance into festival attendance.
internal static class WimSocietyVenueLiveScenario
{
    internal static string RunSpatialFocused()
    {
        const string path = "Artifacts/QA/wim-implementation/wim-venue-spatial-focused.txt";
        var report = new List<string>();
        var cleanup = new List<UnityEngine.Object>();
        try
        {
            var scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
            Require(Application.isPlaying && scope?.Container != null, "Registered main Play scope required.");
            var query = scope.Container.Resolve<ISocietyVenueQuery>();
            var grid = new Grid(9, 1);
            var cells = Enumerable.Range(0, 9).Select(x => new Vector2Int(x, 0)).ToArray();
            var parts = new List<BuildableObject>();
            var doors = new List<BuildableObject>();
            foreach (var cell in cells)
            {
                grid.SetAreaType(cell, GridCellAreaType.DungeonInterior);
                AddPart(cell.x, GridLayer.Hallway, FacilityUseClassification.Structure, typeof(BuildableObject));
            }
            var capture = typeof(SocietyVenueQuery).GetMethod("CaptureEventCells",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Require(capture != null, "Spatial selector contract missing.");
            Require(Capture().SequenceEqual(cells.Skip(1)),
                "Structural Hallway parts invented work-access exclusions or hard-blocked the one-dimensional room.");
            report.Add("PASS nine real Hallway occupants: eight remaining event cells after explicit origin exclusion; no blanket articulation exclusion");

            AddPart(4, GridLayer.Building, FacilityUseClassification.Production, typeof(BuildableObject));
            var multi = Capture();
            Require(!multi.Contains(new Vector2Int(4, 0))
                && new[] { new Vector2Int(3, 0), new Vector2Int(5, 0) }.Count(multi.Contains) == 1,
                "Multi-access operation lost all access or unnecessarily excluded both alternatives.");
            AddPart(8, GridLayer.Building, FacilityUseClassification.Service, typeof(BuildableObject));
            Require(!Capture().Contains(new Vector2Int(7, 0)), "Sole operational access was assigned to an event.");
            report.Add("PASS body exclusion, one retained access for multi-access operation, sole-access protection");

            doors.Add(AddPart(0, GridLayer.Building, FacilityUseClassification.Structure, typeof(Door)));
            AddPart(6, GridLayer.Building, FacilityUseClassification.Structure, typeof(Stair));
            int version = grid.version;
            var guarded = Capture();
            Require(guarded.SequenceEqual(new[] { new Vector2Int(2, 0) }),
                "Door/stair footprint and landing or previously retained operational access was not protected.");
            Require(Capture(1).Count == 0 && grid.version == version,
                "Claimed space was reused or read-only spatial query mutated grid occupancy.");
            report.Add("PASS door/stair/landing/origin protection, claimed-cell subtraction, grid query purity");
            report.Insert(0, "result=PASS");

            IReadOnlyList<Vector2Int> Capture(int claimed = 0)
            {
                var room = new RoomInstance(1, cells, parts, doors, Array.Empty<BuildableObject>(), 2, 0);
                var profile = new FacilityRoomOperationalProfile(room, parts, 0, 0, 0, default, null);
                return (IReadOnlyList<Vector2Int>)capture.Invoke(query,
                    new object[] { grid, profile, claimed, new[] { new Vector2Int(0, 0) } });
            }

            BuildableObject AddPart(int x, GridLayer layer, FacilityUseClassification use, Type type)
            {
                var data = ScriptableObject.CreateInstance<BuildingSO>();
                cleanup.Add(data);
                data.id = 99830 + parts.Count;
                data.width = data.height = 1;
                data.layer = layer;
                data.objectName = "WIM spatial boundary fixture";
                data.runtimeArchetype = type == typeof(Door) ? BuildingRuntimeArchetypeKind.Door
                    : type == typeof(Stair) ? BuildingRuntimeArchetypeKind.Stair : BuildingRuntimeArchetypeKind.Generic;
                data.ConfigureGameplayExecution(use, default);
                var host = new GameObject(data.objectName);
                cleanup.Add(host);
                var part = (BuildableObject)host.AddComponent(type);
                part.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
                part.SetGrid(grid);
                typeof(BuildableObject).GetProperty(nameof(BuildableObject.BuildingData)).SetValue(part, data);
                typeof(BuildableObject).GetProperty(nameof(BuildableObject.centerPos)).SetValue(part, new Vector2Int(x, 0));
                var positions = (List<Vector2Int>)typeof(BuildableObject).GetField("mutableBuildPoses",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(part);
                positions.Add(new Vector2Int(x, 0));
                Require(grid.RegisterOccupant(part, layer, positions, false), "Controlled spatial part registration failed.");
                parts.Add(part);
                return part;
            }
        }
        catch (Exception error)
        {
            report.Insert(0, "result=FAIL");
            report.Add(error.ToString());
        }
        finally
        {
            for (int i = cleanup.Count - 1; i >= 0; i--)
                if (cleanup[i] != null) UnityEngine.Object.DestroyImmediate(cleanup[i]);
        }
        report.Add("scope=production cell selector over real Grid occupancy and controlled horizontal room/parts; no natural festival attendance, main venue eligibility or six-adult claim");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        System.IO.File.WriteAllLines(path, report);
        return string.Join("\n", report);
    }

    internal static void Verify(DungeonRuntimeLifetimeScope scope, Vector2Int origin,
        string authoredAnchorId, List<string> report)
    {
        var query = scope.Container.Resolve<ISocietyVenueQuery>();
        var items = scope.Container.Resolve<IWorldItemStackRuntime>();
        var society = scope.Container.Resolve<V20CampaignRuntime>();
        var buildings = scope.Container.Resolve<IBuildingWorldQuery>();
        var actors = scope.Container.Resolve<ICharacterWorldQuery>();
        string physicalBefore = JsonUtility.ToJson(items.Capture());
        string societyBefore = JsonUtility.ToJson(society.CaptureSociety());
        var required = new FacilityVenueRequirements();
        required.anchor.exactBuildingDefinitionIds.Add(authoredAnchorId);
        Require(query.TryFindDeliveryVenues(required, origin, string.Empty, out var candidates, out string why)
            && candidates.Count >= 1, "Actual prepared venue unavailable: " + why);
        var selected = candidates[0];
        Require(selected.Facility != null && selected.Room != null && selected.Room.IsUsable
            && selected.AnchorCenter == selected.Facility.centerPos && selected.EventCells.Count >= 1,
            "Candidate lacks an actual usable room/anchor/event cell.");
        Require(candidates.Select(c => c.FacilityInstanceId).Distinct(StringComparer.Ordinal).Count() == candidates.Count,
            "Duplicate live venue candidates.");
        Require(candidates.SequenceEqual(candidates.OrderBy(c => c.PathCost)
            .ThenBy(c => c.FacilityInstanceId, StringComparer.Ordinal)
            .ThenBy(c => c.AccessCell.x).ThenBy(c => c.AccessCell.y)), "Venue ranking is not cost/stable-identity ordered.");
        Require(query.TryValidateDeliveryVenue(required, origin, string.Empty, selected.FacilityInstanceId,
            selected.AnchorCenter, out var frozen, out why)
            && frozen.FacilityInstanceId == selected.FacilityInstanceId, "Valid frozen venue did not retain its identity: " + why);
        Require(!query.TryValidateDeliveryVenue(required, origin, string.Empty, selected.FacilityInstanceId,
            selected.AnchorCenter + Vector2Int.right, out _, out _), "Changed anchor coordinates bypassed frozen venue validation.");
        Require(!query.TryValidateDeliveryVenue(required, origin, string.Empty, "building:test-venue-absent",
            selected.AnchorCenter, out _, out _), "Absent frozen facility silently rebound to another venue.");
        report.Add("PASS actual room/anchor, deterministic candidate order, frozen exact identity and stale-coordinate rejection");

        var roomParts = buildings.Buildings.Where(b => b != null && !b.isDestroy && b.BuildingData != null
            && selected.Room.ContainsCell(b.centerPos)).ToArray();
        // Independent upper bounds from authored physical furniture, not the
        // venue projector or its effective/base-capacity calculation.
        int rawSeats = roomParts.Sum(b => Math.Max(0, b.BuildingData.GetAbility<BuildingSeatingAbility>()?.capacity ?? 0));
        int rawTables = roomParts.Sum(b => Math.Max(0, b.BuildingData.GetAbility<BuildingTableAbility>()?.capacity ?? 0));
        int rawService = roomParts.Sum(b => Math.Max(0, b.BuildingData.GetAbility<BuildingServiceAbility>()?.capacity ?? 0));
        Require(selected.SpareSeats <= rawSeats && selected.SpareTables <= rawTables
            && selected.SpareServiceCapacity <= rawService, "Base/effective capacity invented physical furniture capacity.");
        required.minimumSeats = checked(rawSeats + 1);
        Require(!FrozenValid(), "Venue exceeded actual authored seat upper bound.");
        required.minimumSeats = 0;
        required.minimumTables = checked(rawTables + 1);
        Require(!FrozenValid(), "Venue exceeded actual authored table upper bound.");
        required.minimumTables = 0;
        required.minimumServiceCapacity = checked(rawService + 1);
        Require(!FrozenValid(), "Venue exceeded actual authored service upper bound.");
        required.minimumServiceCapacity = 0;
        required.minimumEventCells = checked(selected.Room.Cells.Count + 1);
        Require(!FrozenValid(), "Venue exceeded its whole physical room cell count.");
        required.minimumEventCells = 1;
        Require(FrozenValid(), "Clearing only test demands did not recover the same actual venue.");
        report.Add("PASS actual furniture upper bounds and physical room-cell bound; raw seats=" + rawSeats
            + "; tables=" + rawTables + "; service=" + rawService);

        const string Missing = "building:2147483000";
        Require(buildings.Buildings.All(b => b == null || b.BuildingData == null
            || BuildingDefinitionIdentity.Resolve(b.BuildingData) != Missing), "Synthetic absent selector unexpectedly exists.");
        var alternative = new FacilityVenueAnchorSelector();
        alternative.exactBuildingDefinitionIds.Add(authoredAnchorId);
        alternative.exactBuildingDefinitionIds.Add(Missing);
        required.requiredRoomFacilities.Add(alternative);
        Require(FrozenValid(), "Declared same-room OR alternatives were treated as AND.");
        var requiredMissing = new FacilityVenueAnchorSelector();
        requiredMissing.exactBuildingDefinitionIds.Add(Missing);
        required.requiredRoomFacilities.Add(requiredMissing);
        Require(!FrozenValid(), "Separate required same-room facility selectors were treated as OR.");
        required.requiredRoomFacilities.Clear();
        Require(!selected.EventCells.Contains(origin), "Delivery dropoff counted as spare event space.");
        var occupied = actors.Characters.Where(c => c != null && !c.IsDead).Select(c => c.GetNowXY()).ToHashSet();
        Require(!selected.EventCells.Any(occupied.Contains), "Actual character occupancy counted as spare event space.");
        Require(!query.TryFindFestivalVenues(required, Array.Empty<CharacterId>(), 1, out _, out _),
            "Festival venue accepted no actual participants.");
        Require(!query.TryFindFestivalVenues(required, new[] { new CharacterId("character:test-venue-absent") }, 1, out _, out _),
            "Festival venue accepted an absent participant.");
        Require(JsonUtility.ToJson(items.Capture()) == physicalBefore
            && JsonUtility.ToJson(society.CaptureSociety()) == societyBefore,
            "Eligibility queries consumed/reserved/delivered stock or changed Society ownership.");
        report.Add("PASS same-room selector OR/AND, dropoff/actor cell exclusion, absent participants, physical/Society query purity");
        report.Add("venue-query-scope=registered main services and authored RF25 in actual main room; guest EventSystem/haul/restore follows in this run; festival attendance, cross-room claims and six-adult capacity remain separate");

        bool FrozenValid() => query.TryValidateDeliveryVenue(required, origin, string.Empty,
            selected.FacilityInstanceId, selected.AnchorCenter, out _, out _);
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
#endif
