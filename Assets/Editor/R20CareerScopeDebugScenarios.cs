using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class R20CareerScopeDebugScenarios
{
    private const string DomainCatalogPath =
        "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private const string MissingFacilityId =
        "building:qa:r20-career:missing";
    private const string LegacyFacilityId =
        "building:legacy:r20-career:removed";

    private static readonly CharacterId ForemanCharacter =
        new("character:qa:r20-career:foreman");
    private static readonly CharacterId MentorCharacter =
        new("character:qa:r20-career:mentor");
    private static readonly CharacterId StewardCharacter =
        new("character:qa:r20-career:steward");
    private static readonly CharacterId DuplicateCharacter =
        new("character:qa:r20-career:duplicate");
    private static readonly CharacterId LegacyCharacter =
        new("character:qa:r20-career:legacy");
    private static readonly CharacterId LegacyScopedFirst =
        new("character:qa:r20-career:legacy-scoped-first");
    private static readonly CharacterId LegacyScopedSecond =
        new("character:qa:r20-career:legacy-scoped-second");

    [MenuItem("DungeonStory/QA/R20 Validate Career Position Scopes")]
    public static void RunAll()
    {
        Require(!Application.isPlaying,
            "R20 career-scope verification must run before Play mode.");

        GameDomainContentCatalogSO domain =
            AssetDatabase.LoadAssetAtPath<GameDomainContentCatalogSO>(
                DomainCatalogPath);
        Require(domain != null,
            "Missing actual game-domain content catalog.");
        CareerPositionDefinitionSO[] definitions = domain
            .GetAll<CareerPositionDefinitionSO>()
            .OrderBy(value => value.position)
            .ToArray();
        Require(definitions.Length == 6
            && definitions.Select(value => value.position).Distinct().Count() == 6,
            "The actual career-position catalog must contain six unique definitions.");

        CareerPositionDefinitionCatalog catalog = new(
            new DomainDefinitionSource(domain));
        CareerPositionDefinitionSO foremanDefinition =
            catalog.Require(CareerPositionKind.Foreman);
        CareerPositionDefinitionSO mentorDefinition =
            catalog.Require(CareerPositionKind.Mentor);
        Require(foremanDefinition.scope == CareerPositionScopeKind.Facility
            && mentorDefinition.scope == CareerPositionScopeKind.Facility
            && !string.IsNullOrWhiteSpace(foremanDefinition.requiredFacilityTag)
            && !string.IsNullOrWhiteSpace(mentorDefinition.requiredFacilityTag),
            "Foreman and mentor definitions must own authored facility tags.");

        BuildingSO[] buildings = domain.GetAll<BuildingSO>().ToArray();
        BuildingSO foremanBuilding = buildings.FirstOrDefault(value =>
            value != null
            && value.HasSemanticTag(foremanDefinition.requiredFacilityTag)
            && !value.HasSemanticTag(mentorDefinition.requiredFacilityTag));
        BuildingSO mentorBuilding = buildings.FirstOrDefault(value =>
            value != null
            && value.HasSemanticTag(mentorDefinition.requiredFacilityTag));
        BuildingSO wrongBuilding = buildings.FirstOrDefault(value =>
            value != null
            && !value.HasSemanticTag(foremanDefinition.requiredFacilityTag)
            && !value.HasSemanticTag(mentorDefinition.requiredFacilityTag));
        Require(foremanBuilding != null && mentorBuilding != null
            && wrongBuilding != null,
            "Missing representative actual buildings for career-scope verification.");

        SourceSnapshot[] sources = definitions
            .Cast<ScriptableObject>()
            .Concat(new ScriptableObject[]
            {
                domain,
                foremanBuilding,
                mentorBuilding,
                wrongBuilding
            })
            .Distinct()
            .Select(SourceSnapshot.Capture)
            .ToArray();

        try
        {
            using var fixture = new RuntimeFixture(
                foremanBuilding,
                mentorBuilding,
                wrongBuilding);
            VerifyStrictAssignmentAndRepair(
                catalog,
                fixture,
                foremanDefinition,
                mentorDefinition);
            VerifyLegacyUnknownFacilityRestore(catalog, fixture.BuildingWorld);
            VerifyScopeDrivenRestoreUniqueness(catalog, fixture.BuildingWorld);
            Debug.Log(
                "R20_CAREER_SCOPE=PASS; six actual definitions; actual tagged facilities; "
                + "wrong, missing and destroyed rejected unchanged; repair retry; "
                + "global and duplicate occupancy guarded; JSON fresh-root restore; "
                + "legacy unknown facility scope preserved; source JSON and dirty state unchanged");
        }
        finally
        {
            foreach (SourceSnapshot source in sources)
                source.RequireUnchanged();
        }
    }

    private static void VerifyStrictAssignmentAndRepair(
        ICareerPositionDefinitionCatalog catalog,
        RuntimeFixture fixture,
        CareerPositionDefinitionSO foremanDefinition,
        CareerPositionDefinitionSO mentorDefinition)
    {
        Require(fixture.Foreman.BuildingData.HasSemanticTag(
                foremanDefinition.requiredFacilityTag),
            "Selected foreman building lost its authored required tag.");
        Require(fixture.Mentor.BuildingData.HasSemanticTag(
                mentorDefinition.requiredFacilityTag),
            "Selected mentor building lost its authored required tag.");
        Require(!fixture.Wrong.BuildingData.HasSemanticTag(
                foremanDefinition.requiredFacilityTag)
            && !fixture.Wrong.BuildingData.HasSemanticTag(
                mentorDefinition.requiredFacilityTag),
            "Wrong-facility fixture unexpectedly has an accepted career tag.");

        CareerRuntime runtime = NewRuntime(catalog, fixture.BuildingWorld);

        RequireRejectedUnchanged(
            runtime,
            () => runtime.AssignPosition(
                ForemanCharacter,
                CareerPositionKind.Foreman,
                fixture.Wrong.PersistentInstanceId.Value,
                10),
            "Foreman assignment accepted a building with the wrong tag.");
        RequireRejectedUnchanged(
            runtime,
            () => runtime.AssignPosition(
                ForemanCharacter,
                CareerPositionKind.Foreman,
                MissingFacilityId,
                10),
            "Foreman assignment accepted a missing building.");

        fixture.Mentor.isDestroy = true;
        try
        {
            RequireRejectedUnchanged(
                runtime,
                () => runtime.AssignPosition(
                    MentorCharacter,
                    CareerPositionKind.Mentor,
                    fixture.Mentor.PersistentInstanceId.Value,
                    10),
                "Mentor assignment accepted a destroyed building.");
        }
        finally
        {
            fixture.Mentor.isDestroy = false;
        }

        RequireRejectedUnchanged(
            runtime,
            () => runtime.AssignPosition(
                MentorCharacter,
                CareerPositionKind.Mentor,
                fixture.Foreman.PersistentInstanceId.Value,
                10),
            "Mentor assignment accepted a building with the wrong tag.");
        RequireRejectedUnchanged(
            runtime,
            () => runtime.AssignPosition(
                StewardCharacter,
                CareerPositionKind.Steward,
                fixture.Foreman.PersistentInstanceId.Value,
                10),
            "Global career position accepted a facility scope.");
        RequireRejectedUnchanged(
            runtime,
            () => runtime.AssignPosition(
                DuplicateCharacter,
                (CareerPositionKind)int.MaxValue,
                string.Empty,
                10),
            "Undefined career position was accepted.");

        runtime.AssignPosition(
            ForemanCharacter,
            CareerPositionKind.Foreman,
            fixture.Foreman.PersistentInstanceId.Value,
            11);
        runtime.AssignPosition(
            MentorCharacter,
            CareerPositionKind.Mentor,
            fixture.Mentor.PersistentInstanceId.Value,
            11);
        runtime.AssignPosition(
            StewardCharacter,
            CareerPositionKind.Steward,
            "   ",
            11);
        RequirePosition(
            runtime,
            ForemanCharacter,
            CareerPositionKind.Foreman,
            fixture.Foreman.PersistentInstanceId.Value);
        RequirePosition(
            runtime,
            MentorCharacter,
            CareerPositionKind.Mentor,
            fixture.Mentor.PersistentInstanceId.Value);
        RequirePosition(
            runtime,
            StewardCharacter,
            CareerPositionKind.Steward,
            string.Empty);

        RequireRejectedUnchanged(
            runtime,
            () => runtime.AssignPosition(
                DuplicateCharacter,
                CareerPositionKind.Foreman,
                fixture.Foreman.PersistentInstanceId.Value,
                12),
            "Facility-scoped position accepted a duplicate occupant.");
        RequireRejectedUnchanged(
            runtime,
            () => runtime.AssignPosition(
                DuplicateCharacter,
                CareerPositionKind.Steward,
                string.Empty,
                12),
            "Global position accepted a duplicate occupant.");

        CharacterCareerWorldSaveData saved = runtime.Capture();
        string savedJson = JsonUtility.ToJson(saved);
        CareerRuntime restored = NewRuntime(catalog, fixture.BuildingWorld);
        restored.PublishRestore(restored.PrepareRestore(
            JsonUtility.FromJson<CharacterCareerWorldSaveData>(savedJson)));
        Require(string.Equals(CaptureJson(restored), savedJson, StringComparison.Ordinal),
            "Fresh-root career restore changed the saved payload.");
        RequirePosition(
            restored,
            ForemanCharacter,
            CareerPositionKind.Foreman,
            fixture.Foreman.PersistentInstanceId.Value);
        RequirePosition(
            restored,
            MentorCharacter,
            CareerPositionKind.Mentor,
            fixture.Mentor.PersistentInstanceId.Value);
        RequirePosition(
            restored,
            StewardCharacter,
            CareerPositionKind.Steward,
            string.Empty);

        restored.PublishRestore(restored.PrepareRestore(
            JsonUtility.FromJson<CharacterCareerWorldSaveData>(savedJson)));
        Require(string.Equals(CaptureJson(restored), savedJson, StringComparison.Ordinal),
            "Career restore retry duplicated or transformed saved state.");
        RequireRejectedUnchanged(
            restored,
            () => restored.AssignPosition(
                DuplicateCharacter,
                CareerPositionKind.Foreman,
                fixture.Foreman.PersistentInstanceId.Value,
                13),
            "Restored facility position accepted a duplicate occupant.");

        restored.AssignPosition(
            ForemanCharacter,
            CareerPositionKind.None,
            MissingFacilityId,
            14);
        RequirePosition(
            restored,
            ForemanCharacter,
            CareerPositionKind.None,
            string.Empty);
        restored.AssignPosition(
            MentorCharacter,
            CareerPositionKind.None,
            fixture.Mentor.PersistentInstanceId.Value,
            14);
        RequirePosition(
            restored,
            MentorCharacter,
            CareerPositionKind.None,
            string.Empty);
        restored.AssignPosition(
            DuplicateCharacter,
            CareerPositionKind.Foreman,
            fixture.Foreman.PersistentInstanceId.Value,
            15);
        RequirePosition(
            restored,
            DuplicateCharacter,
            CareerPositionKind.Foreman,
            fixture.Foreman.PersistentInstanceId.Value);
    }

    private static void VerifyLegacyUnknownFacilityRestore(
        ICareerPositionDefinitionCatalog catalog,
        IBuildingWorldQuery buildings)
    {
        CharacterCareerWorldSaveData legacy = new()
        {
            characters = new List<CharacterCareerSaveData>
            {
                new()
                {
                    characterId = LegacyCharacter.Value,
                    position = CareerPositionKind.Foreman,
                    positionScopeId = LegacyFacilityId,
                    recentHistory = new List<CareerHistoryEventSaveData>()
                }
            },
            mentorships = new List<CareerMentorshipSaveData>()
        };
        string legacyJson = JsonUtility.ToJson(legacy);
        CareerRuntime runtime = NewRuntime(catalog, buildings);
        runtime.PublishRestore(runtime.PrepareRestore(
            JsonUtility.FromJson<CharacterCareerWorldSaveData>(legacyJson)));
        RequirePosition(
            runtime,
            LegacyCharacter,
            CareerPositionKind.Foreman,
            LegacyFacilityId);
        string captured = CaptureJson(runtime);
        Require(string.Equals(captured, legacyJson, StringComparison.Ordinal),
            "Legacy unknown facility scope was deleted or transformed on restore.");

        RequireRejectedUnchanged(
            runtime,
            () => runtime.AssignPosition(
                LegacyCharacter,
                CareerPositionKind.Foreman,
                LegacyFacilityId,
                20),
            "A new assignment accepted the missing legacy facility.");

        CharacterCareerWorldSaveData duplicate =
            JsonUtility.FromJson<CharacterCareerWorldSaveData>(captured);
        duplicate.characters.Add(new CharacterCareerSaveData
        {
            characterId = DuplicateCharacter.Value,
            position = CareerPositionKind.Foreman,
            positionScopeId = LegacyFacilityId,
            recentHistory = new List<CareerHistoryEventSaveData>()
        });
        RequirePrepareRejectedUnchanged(
            runtime,
            duplicate,
            "Restore accepted duplicate occupants for one facility scope.");

        CharacterCareerWorldSaveData undefined =
            JsonUtility.FromJson<CharacterCareerWorldSaveData>(captured);
        undefined.characters[0].position = (CareerPositionKind)int.MaxValue;
        RequirePrepareRejectedUnchanged(
            runtime,
            undefined,
            "Restore accepted an undefined career position.");

        runtime.PublishRestore(runtime.PrepareRestore(
            JsonUtility.FromJson<CharacterCareerWorldSaveData>(captured)));
        Require(string.Equals(CaptureJson(runtime), captured, StringComparison.Ordinal),
            "Legacy restore retry duplicated or transformed saved state.");
    }

    private static void VerifyScopeDrivenRestoreUniqueness(
        ICareerPositionDefinitionCatalog catalog,
        IBuildingWorldQuery buildings)
    {
        const string firstScope = "building:legacy:r20-career:scope-a";
        const string secondScope = "building:legacy:r20-career:scope-b";
        CharacterCareerWorldSaveData legacy = new()
        {
            characters = new List<CharacterCareerSaveData>
            {
                NewCareerRecord(
                    LegacyScopedFirst,
                    CareerPositionKind.Steward,
                    firstScope),
                NewCareerRecord(
                    LegacyScopedSecond,
                    CareerPositionKind.Steward,
                    secondScope)
            },
            mentorships = new List<CareerMentorshipSaveData>()
        };
        string json = JsonUtility.ToJson(legacy);
        CareerRuntime runtime = NewRuntime(catalog, buildings);
        runtime.PublishRestore(runtime.PrepareRestore(
            JsonUtility.FromJson<CharacterCareerWorldSaveData>(json)));
        RequirePosition(
            runtime,
            LegacyScopedFirst,
            CareerPositionKind.Steward,
            firstScope);
        RequirePosition(
            runtime,
            LegacyScopedSecond,
            CareerPositionKind.Steward,
            secondScope);
        Require(string.Equals(CaptureJson(runtime), json, StringComparison.Ordinal),
            "Restore collapsed distinct legacy scopes through a position-specific rule.");

        CharacterCareerWorldSaveData duplicate =
            JsonUtility.FromJson<CharacterCareerWorldSaveData>(json);
        duplicate.characters[1].positionScopeId = firstScope;
        RequirePrepareRejectedUnchanged(
            runtime,
            duplicate,
            "Restore accepted duplicate occupants for one nonempty legacy scope.");
    }

    private static CharacterCareerSaveData NewCareerRecord(
        CharacterId character,
        CareerPositionKind position,
        string scope) => new()
    {
        characterId = character.Value,
        position = position,
        positionScopeId = scope,
        recentHistory = new List<CareerHistoryEventSaveData>()
    };

    private static CareerRuntime NewRuntime(
        ICareerPositionDefinitionCatalog definitions,
        IBuildingWorldQuery buildings) => new(
            new DungeonRuntimeAggregateRootStore(),
            milestoneModifiers: null,
            positionDefinitions: definitions,
            buildings: buildings);

    private static void RequireRejectedUnchanged(
        CareerRuntime runtime,
        Action action,
        string message)
    {
        string before = CaptureJson(runtime);
        bool rejected = false;
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected, message);
        Require(string.Equals(CaptureJson(runtime), before, StringComparison.Ordinal),
            message + " Rejection changed writable state.");
    }

    private static void RequirePrepareRejectedUnchanged(
        CareerRuntime runtime,
        CharacterCareerWorldSaveData data,
        string message)
    {
        string before = CaptureJson(runtime);
        bool rejected = false;
        try
        {
            runtime.PrepareRestore(data);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected, message);
        Require(string.Equals(CaptureJson(runtime), before, StringComparison.Ordinal),
            message + " Rejected restore changed published state.");
    }

    private static void RequirePosition(
        CareerRuntime runtime,
        CharacterId characterId,
        CareerPositionKind position,
        string scope)
    {
        Require(runtime.TryGet(characterId, out CharacterCareerSnapshot snapshot),
            "Missing career snapshot for " + characterId.Value + ".");
        Require(snapshot.Position == position
            && string.Equals(
                snapshot.PositionScopeId,
                scope,
                StringComparison.Ordinal),
            $"Career position mismatch for {characterId.Value}: "
            + $"{snapshot.Position}/{snapshot.PositionScopeId}.");
    }

    private static string CaptureJson(CareerRuntime runtime) =>
        JsonUtility.ToJson(runtime.Capture());

    private sealed class RuntimeFixture : IDisposable
    {
        private readonly List<GameObject> hosts = new();

        internal RuntimeFixture(
            BuildingSO foreman,
            BuildingSO mentor,
            BuildingSO wrong)
        {
            Foreman = CreateBuilding(foreman, "foreman");
            Mentor = CreateBuilding(mentor, "mentor");
            Wrong = CreateBuilding(wrong, "wrong");
            BuildableObject[] values = { Foreman, Mentor, Wrong };
            BuildingWorld = Port<IBuildingWorldQuery>((method, _) =>
                method.Name switch
                {
                    "get_BuildingVersion" => 0,
                    "get_Buildings" => values,
                    _ => Unexpected(method)
                });
        }

        internal BuildableObject Foreman { get; }
        internal BuildableObject Mentor { get; }
        internal BuildableObject Wrong { get; }
        internal IBuildingWorldQuery BuildingWorld { get; }

        private BuildableObject CreateBuilding(
            BuildingSO definition,
            string suffix)
        {
            var host = new GameObject("R20CareerScopeFixture-" + suffix)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            hosts.Add(host);
            BuildableObject building = host.AddComponent<BuildableObject>();
            SetBuildingData(building, definition);
            building.RestorePersistentIdentity(new BuildingInstanceId(
                "building:qa:r20-career:" + suffix));
            return building;
        }

        public void Dispose()
        {
            for (int index = hosts.Count - 1; index >= 0; index--)
                Object.DestroyImmediate(hosts[index]);
        }
    }

    private sealed class DomainDefinitionSource : IGameContentDefinitionSource
    {
        private readonly GameDomainContentCatalogSO domain;

        internal DomainDefinitionSource(GameDomainContentCatalogSO domain)
        {
            this.domain = domain
                ?? throw new ArgumentNullException(nameof(domain));
        }

        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject =>
            domain.GetAll<T>();

        public T RequireSingle<T>() where T : ScriptableObject
        {
            IReadOnlyList<T> values = GetAll<T>();
            return values.Count == 1
                ? values[0]
                : throw new InvalidOperationException(
                    $"Expected one {typeof(T).Name}, found {values.Count}.");
        }
    }

    private sealed class SourceSnapshot
    {
        private readonly ScriptableObject source;
        private readonly string json;
        private readonly bool dirty;

        private SourceSnapshot(ScriptableObject source)
        {
            this.source = source;
            json = EditorJsonUtility.ToJson(source);
            dirty = EditorUtility.IsDirty(source);
        }

        internal static SourceSnapshot Capture(ScriptableObject source) =>
            new(source ?? throw new ArgumentNullException(nameof(source)));

        internal void RequireUnchanged()
        {
            Require(string.Equals(
                    EditorJsonUtility.ToJson(source),
                    json,
                    StringComparison.Ordinal),
                "Career fixture changed source JSON for '" + source.name + "'.");
            Require(EditorUtility.IsDirty(source) == dirty,
                "Career fixture changed dirty state for '" + source.name + "'.");
        }
    }

    private static T Port<T>(
        Func<MethodInfo, object[], object> invoke) where T : class
    {
        T value = DispatchProxy.Create<T, StrictPort>();
        ((StrictPort)(object)value).InvokeMethod = invoke
            ?? throw new ArgumentNullException(nameof(invoke));
        return value;
    }

    private static object Unexpected(MethodInfo method) =>
        throw new InvalidOperationException(
            "Unexpected R20 career fixture call: " + method.Name);

    public class StrictPort : DispatchProxy
    {
        public Func<MethodInfo, object[], object> InvokeMethod;

        protected override object Invoke(MethodInfo method, object[] args) =>
            InvokeMethod(method, args);
    }

    private static void SetBuildingData(
        BuildableObject building,
        BuildingSO definition)
    {
        PropertyInfo property = typeof(BuildableObject).GetProperty(
            nameof(BuildableObject.BuildingData),
            BindingFlags.Instance | BindingFlags.Public);
        MethodInfo setter = property?.GetSetMethod(true);
        if (setter == null)
        {
            throw new InvalidOperationException(
                "Missing BuildableObject.BuildingData fixture setter.");
        }
        setter.Invoke(building, new object[] { definition });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
