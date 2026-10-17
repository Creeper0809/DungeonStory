#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

// Controlled decision/restore witness, not a natural wildlife population simulation.
public static class Wim019WildlifeMigrationDebugScenarios
{
    public const string ReportPath = "Artifacts/QA/wim-implementation/wim-019-migration-focused.txt";
    private static readonly Vector2Int Signal = new(16, 0);

    public static bool RunFocused()
    {
        var lines = new List<string> { "scope=authored profiles; controlled world port; actual ecology selector and current ecology restore; no natural movement claim" };
        int failures = 0;
        void Run(string name, Action test)
        {
            try { test(); lines.Add(name + "=PASS"); }
            catch (Exception error) { failures++; lines.Add(name + "=FAIL " + error); }
        }
        Run("warm-temperature", () => Temperature("ember_lizard", 28));
        Run("cold-temperature", () => Temperature("frost_ram", -5));
        Run("foul-water-not-clean-water", () =>
        {
            using var f = new Fixture("mire_leech");
            f.World.Water.Add(Water(14, false, 10));
            f.World.Water.Add(Water(16, true, 3));
            Require(f.Choose(out var target, out var intent, out _) && target == Signal && intent == WildlifeIntent.Wander, "Foul-water preference did not choose the physical qualifying source.");
        });
        Run("foul-water-below-threshold", () =>
        {
            using var f = new Fixture("mire_leech");
            f.World.Water.Add(Water(16, true, 2));
            f.Choose(out var target, out _, out _);
            Require(target != Signal, "Below-25% source was accepted as migration signal.");
        });
        Run("physical-carcass-no-consumption", () =>
        {
            using var f = new Fixture("carrion_drake");
            var carcass = Carcass(16);
            f.Items.Add(carcass);
            f.Initialize();
            string before = JsonUtility.ToJson(f.Core.Capture());
            Require(f.Choose(out var target, out var intent, out _) && target == Signal && intent == WildlifeIntent.Wander, "Sated scavenger must approach, not consume/hunt.");
            Require(f.Items[0].Quantity == 1 && f.Animal.Hunger == 0 && f.World.DrinkCalls == 0, "Preference query mutated physical needs/resource.");
            Require(before == JsonUtility.ToJson(f.Core.Capture()), "Preference query changed ecology authority.");
        });
        Run("forbidden-and-empty-carcass", () =>
        {
            using var f = new Fixture("carrion_drake");
            f.Items.Add(new WildlifeCarcassStackSnapshot("wild:carcass:shadow_hare", 1, true, Signal));
            f.Items.Add(new WildlifeCarcassStackSnapshot("wild:carcass:shadow_hare", 0, false, new Vector2Int(17, 0)));
            f.Choose(out var target, out _, out _);
            Require(target != Signal && target.x != 17, "Forbidden/empty physical source influenced preference.");
        });
        Run("survival-water-origin-outside-sensing", () =>
        {
            using var f = new Fixture("mire_leech");
            f.Animal.Thirst = .8f;
            f.World.Water.Add(Water(23, true, 10)); // 11 > authored10, even if a nearby stand is reachable.
            f.Choose(out var target, out var intent, out _);
            Require(intent != WildlifeIntent.Drink && target.x != 23, "Thirst or migration discovered a source beyond its sensory radius.");
        });
        Run("survival-carcass-outside-sensing", () =>
        {
            using var f = new Fixture("carrion_drake");
            f.Animal.Hunger = .8f;
            f.Items.Add(Carcass(25)); // 13 > authored12.
            f.Choose(out var target, out _, out _);
            Require(target.x != 25, "Hunger or migration discovered a distant carcass.");
        });
        Run("temperature-no-global-grid-scan", () =>
        {
            using var f = new Fixture("ember_lizard");
            f.World.Temperatures[new Vector2Int(25, 0)] = 28;
            f.Initialize();
            f.Grid.GetCellsCalls = 0;
            f.World.TemperatureQueries.Clear();
            f.Choose(out var target, out _, out _);
            Require(target.x != 25 && f.Grid.GetCellsCalls == 0, "Preference scanned the whole grid or chose an unseen temperature.");
            Require(f.World.TemperatureQueries.All(p => Distance(p, f.Animal.GridPosition) <= 8), "Temperature query escaped authored sensory radius.");
        });
        Run("territory-admission", () =>
        {
            using var f = new Fixture("ember_lizard");
            f.World.Temperatures[new Vector2Int(19, 0)] = 28; // sensed7, outside territory6.
            f.Choose(out var target, out _, out _);
            Require(target.x != 19, "Attraction selected outside territory and would oscillate with return.");
        });
        Run("no-signal-preserves-general-exploration", () =>
        {
            using var f = new Fixture("ember_lizard");
            f.World.RemotePatches = true;
            Require(!f.Choose(out _, out _, out string reason) && !string.IsNullOrWhiteSpace(reason),
                "No habitat/signal must leave the existing exploration consumer available with an explicit reason, not force perpetual current-cell dwell.");
        });
        Run("distance-and-ordinal-tie", () =>
        {
            using var f = new Fixture("carrion_drake");
            f.Items.Add(Carcass(17));
            f.Items.Add(Carcass(14));
            Require(f.Choose(out var nearer, out _, out _) && nearer.x == 14, "A more distant equal signal beat the nearer source.");
            f.Items.Add(Carcass(10));
            Require(f.Choose(out var tied, out _, out _) && tied.x == 10, "Equal-distance tie did not use ordinal x/y.");
            f.Items.Reverse();
            Require(f.Choose(out var shuffled, out _, out _) && shuffled == tied, "Input order changed equal-score selection.");
        });
        Run("urgent-thirst-priority", () =>
        {
            using var f = new Fixture("ember_lizard");
            f.World.Temperatures[Signal] = 28;
            f.World.Water.Add(Water(15, false, 10));
            f.Animal.Thirst = .8f;
            Require(f.Choose(out _, out var intent, out _) && intent == WildlifeIntent.Drink, "Temperature overrode thirst.");
        });
        Run("flee-and-return-priority", () =>
        {
            using var f = new Fixture("ember_lizard");
            f.World.Temperatures[Signal] = 28;
            f.Animal.Fear = 5;
            f.Choose(out _, out var fearIntent, out _);
            Require(fearIntent == WildlifeIntent.Flee, "Temperature overrode flee.");
            f.Animal.Fear = 0;
            f.Animal.TerritoryCenter = new Vector2Int(2, 0);
            Require(f.Choose(out _, out var returnIntent, out _) && returnIntent == WildlifeIntent.ReturnToTerritory, "Temperature overrode existing territory return.");
        });
        Run("lore-only-no-special-attraction", () =>
        {
            foreach (string id in new[] { "glow_moth", "mana_wisp", "tunnel_mole", "spore_elk", "crystal_beetle", "ash_crawler" })
            {
                using var f = new Fixture(id);
                f.World.Temperatures[Signal] = 28;
                f.Items.Add(Carcass(16));
                f.Choose(out var target, out _, out string reason);
                Require(target != Signal && !string.IsNullOrWhiteSpace(reason), id + " lacks explicit general/lore policy or gained an unsupported signal.");
            }
        });
        Run("current-ecology-restore-recomputes-signal", () =>
        {
            using var f = new Fixture("ember_lizard");
            f.World.Temperatures[Signal] = 28;
            Require(f.Choose(out var first, out _, out _) && first == Signal, "Initial signal missing.");
            var saved = JsonUtility.FromJson<DungeonWildlifeEcosystemSaveData>(JsonUtility.ToJson(f.Core.Capture()));
            f.Core.PublishRestoreCandidate(f.Core.PrepareRestoreCandidate(saved, f.Grid));
            f.World.Temperatures.Clear();
            f.World.Temperatures[new Vector2Int(17, 0)] = 28;
            Require(f.Choose(out var second, out _, out _) && second == new Vector2Int(17, 0), "Restore replayed a stale cached destination instead of current environment.");
        });
        lines.Add("result=" + (failures == 0 ? "PASS" : "FAIL") + "; failures=" + failures);
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllLines(ReportPath, lines);
        return failures == 0;
    }

    private static void Temperature(string species, float temperature)
    {
        using var f = new Fixture(species);
        f.World.Temperatures[Signal] = temperature;
        Require(f.Choose(out var target, out var intent, out string reason) && target == Signal && intent == WildlifeIntent.Wander && !string.IsNullOrWhiteSpace(reason), species + " did not choose the authored temperature range.");
    }
    private static int Distance(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    private static WildlifeWaterSourceSnapshot Water(int x, bool foul, float remaining) => new("qa-water-" + x, new Vector2Int(x, 0), false, foul, 10, remaining);
    private static WildlifeCarcassStackSnapshot Carcass(int x) => new("wild:carcass:shadow_hare", 1, false, new Vector2Int(x, 0));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private sealed class Fixture : IDisposable
    {
        public readonly TestGrid Grid = new();
        public readonly TestWorld World = new();
        public readonly Animal Animal = new();
        public readonly List<WildlifeCarcassStackSnapshot> Items = new();
        public readonly WildlifeEcosystemRuntime Core;
        public Fixture(string species)
        {
            var asset = AssetDatabase.LoadAssetAtPath<WildlifeSpeciesSO>("Assets/Resources/SO/V20/Ecology/Wildlife/wildlife_" + species + ".asset");
            Require(asset != null, "Missing actual authored species: " + species);
            Animal.Species = asset.ToDefinition();
            World.Grid = Grid;
            Core = new WildlifeEcosystemRuntime(World, new Presentation(), new UnityGameClock(), new RandomStreamProvider(19019), new GuidPersistentIdGenerator());
        }
        public void Initialize() => Core.EnsureInitialized(Grid);
        public bool Choose(out Vector2Int target, out WildlifeIntent intent, out string reason)
        {
            Initialize();
            return Core.TryChooseEcologyTarget(Animal, Grid, new[] { Animal }, Items, out target, out intent, out reason);
        }
        public void Dispose() => Core.Dispose();
    }
    private sealed class Animal : IWildlifeAnimalPort
    {
        public string WildlifeId => "qa-migration-animal";
        public string SpeciesId => Species.SpeciesId;
        public WildlifeSpeciesDefinition Species { get; set; }
        public int MaxHealth => Species.MaxHealth;
        public int CurrentHealth => MaxHealth;
        public WildlifeState State => WildlifeState.Idle;
        public Vector2Int GridPosition => new(12, 0);
        public float Fear { get; set; }
        public float Hunger { get; set; }
        public float Thirst { get; set; }
        public WildlifeIntent Intent { get; private set; }
        public Vector2Int TerritoryCenter { get; set; } = new(12, 0);
        public bool HasLastThreatPosition => false;
        public Vector2Int LastThreatPosition => new(10, 0);
        public float LastThreatAge => 100;
        public bool CanEnterDungeon => Species.CanEnterDungeon;
        public bool IsAlive => true;
        public bool IsDangerous => Species.IsDangerous;
        public void SetIntent(WildlifeIntent intent, string reason) => Intent = intent;
        public void ChangeHunger(float delta) => Hunger += delta;
        public void ChangeThirst(float delta) => Thirst += delta;
    }
    private sealed class TestGrid : IWildlifeGridPort
    {
        private readonly IWildlifeGridCellPort[] cells = Enumerable.Range(0, 40).Select(x => (IWildlifeGridCellPort)new Cell(x)).ToArray();
        public int GetCellsCalls;
        public int Width => 40;
        public Vector2Int GetCellPosition(Vector3 p) => new(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
        public Vector3 GetWorldPosition(Vector2Int p) => new(p.x, p.y);
        public bool IsValidGridPos(Vector2Int p) => p.y == 0 && p.x >= 0 && p.x < 40;
        public bool IsWalkable(Vector2Int p) => IsValidGridPos(p);
        public IWildlifeGridCellPort GetGridCell(Vector2Int p) => IsValidGridPos(p) ? cells[p.x] : null;
        public IReadOnlyList<IWildlifeGridCellPort> GetCells() { GetCellsCalls++; return cells; }
    }
    private sealed class Cell : IWildlifeGridCellPort
    {
        public Cell(int x) => Position = new Vector2Int(x, 0);
        public Vector2Int Position { get; }
        public WildlifeGridAreaType AreaType => WildlifeGridAreaType.ExteriorPath;
        public bool IsWalkable => true;
        public bool HasWildlifeOccupant => false;
        public bool IsOutdoorSurface => true;
    }
    private sealed class TestWorld : IWildlifeEcosystemWorldPort
    {
        public TestGrid Grid;
        public readonly List<WildlifeWaterSourceSnapshot> Water = new();
        public readonly Dictionary<Vector2Int, float> Temperatures = new();
        public readonly List<Vector2Int> TemperatureQueries = new();
        public int DrinkCalls;
        public bool RemotePatches;
        public bool TryGetGrid(out IWildlifeGridPort grid) { grid = Grid; return true; }
        public IReadOnlyList<WildlifeHabitatPatch> GetMarkerPatches(IWildlifeGridPort grid, IPersistentIdGenerator ids) => new[]
        {
            new WildlifeHabitatPatch(ids.NewWildlifeHabitatPatchId().Value, WildlifeHabitatType.Grass, new Vector2Int(RemotePatches ? 0 : 13, 0), 0, 10, 10, 0, 0),
            new WildlifeHabitatPatch(ids.NewWildlifeHabitatPatchId().Value, WildlifeHabitatType.Brush, new Vector2Int(RemotePatches ? 39 : 14, 0), 0, 10, 10, 0, 0)
        };
        public IReadOnlyList<WildlifeWaterSourceSnapshot> GetWaterSources() => Water;
        public bool TryGetWaterSource(string id, out WildlifeWaterSourceSnapshot source) { source = Water.FirstOrDefault(x => x.SourceId == id); return source.SourceId != null; }
        public bool TryDrinkWater(string id, float amount, out float consumed) { DrinkCalls++; consumed = 0; return false; }
        public bool TryGetTemperatureC(Vector2Int position, out float temperatureC)
        {
            TemperatureQueries.Add(position);
            if (!Temperatures.TryGetValue(position, out temperatureC)) temperatureC = 50;
            return true;
        }
    }
    private sealed class Presentation : IWildlifeEcosystemPresentationPort
    {
        public bool OverlayEnabled => false;
        public void SetOverlayEnabled(bool value) { }
        public void Clear() { }
        public void Rebuild(IWildlifeGridPort grid, IReadOnlyList<WildlifeHabitatPatch> patches) { }
        public void RefreshOverlay(IWildlifeGridPort grid, IReadOnlyList<WildlifeHabitatPatch> patches) { }
        public void RefreshPatches(IReadOnlyList<WildlifeHabitatPatch> patches) { }
        public void RefreshPatch(WildlifeHabitatPatch patch) { }
        public void Dispose() { }
    }
}
#endif
