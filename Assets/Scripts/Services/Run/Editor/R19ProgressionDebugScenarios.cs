#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class R19ProgressionDebugScenarios
{
    public static void RunR30LivingMembershipFocused()
    {
        VerifyLivingCharacterMembership(new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        Debug.Log("R30_LIVING_MEMBERSHIP=PASS");
    }

    public static void RunAll()
    {
        var content = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
        var catalog = new V20StoryContentCatalog(content);
        VerifyTemporal(catalog);
        VerifyFacilities(content, catalog);
        VerifyLivingCharacterMembership(content);
        Debug.Log("R19_PROGRESSION=PASS; temporal-119-save-120-restore-retry; real-facility-projector; live-preflight; chapter-command; rejection-atomicity; restore");
    }

    private static void VerifyTemporal(V20StoryContentCatalog catalog)
    {
        var campaign = new V20CampaignRuntime(new DungeonRuntimeAggregateRootStore(), catalog);
        RunMilestoneEvaluationSnapshot Day(int day)
        {
            var snapshot = new RunMilestoneEvaluationSnapshot { AbsoluteDay = day };
            snapshot.CompletedResearchIds.Add(7271);
            snapshot.WorldMetrics[V20WorldMetricKind.Population] = 60;
            snapshot.WorldFlags.Add("temporal:productivity-qualified-today");
            return snapshot;
        }
        for (int day = 1; day <= 119; day++) campaign.Evaluate(Day(day));
        Require(!campaign.CompletedMilestoneIds.Contains("ending:timeless-sanctuary"), "Temporal milestone completed before 120 qualified days.");
        var save = JsonUtility.FromJson<RunMilestoneWorldSaveData>(JsonUtility.ToJson(campaign.CaptureMilestones()));
        Require(save.productivityCoverageStreakDays == 119, "Qualified streak was not persisted.");
        var restored = new V20CampaignRuntime(new DungeonRuntimeAggregateRootStore(), catalog);
        restored.PublishMilestones(restored.PrepareMilestones(save));
        var last = Day(120);
        Require(restored.Evaluate(last).Contains("ending:timeless-sanctuary")
            && last.WorldFlags.Contains(V20WorldFlagIds.TemporalProductivitySustained)
            && last.WorldFlags.Contains(V20WorldFlagIds.TemporalPopulationSustained), "Actual qualified-day producer did not unlock the existing ending key.");
        string completed = JsonUtility.ToJson(restored.CaptureMilestones());
        Require(restored.Evaluate(Day(120)).Count == 0 && JsonUtility.ToJson(restored.CaptureMilestones()) == completed,
            "Same-day retry repeated rewards or advanced the streak.");
        var again = new V20CampaignRuntime(new DungeonRuntimeAggregateRootStore(), catalog);
        again.PublishMilestones(again.PrepareMilestones(JsonUtility.FromJson<RunMilestoneWorldSaveData>(completed)));
        Require(again.Evaluate(Day(120)).Count == 0 && JsonUtility.ToJson(again.CaptureMilestones()) == completed,
            "Restored milestone replay was not idempotent.");
    }

    private static void VerifyFacilities(ResourceGameContentCatalog content, V20StoryContentCatalog catalog)
    {
        BuildingSO source = content.GetAll<BuildingSO>().First(value => value.Facility != null
            && value.RequiresRoomRole() && (value.Facility.roles & FacilityRole.Administration) != 0);
        string sourceJson = EditorJsonUtility.ToJson(source);
        bool sourceDirty = EditorUtility.IsDirty(source);
        var host = new GameObject("R19 operational administration fixture");
        var building = host.AddComponent<BuildableObject>();
        bool usable = true;
        var policy = Port<IBuildingRoomPolicyPort>((method, args) => method.Name switch
        {
            "IsFacilityRoleAvailable" => SetOut(args, usable),
            "GetOperationalProfile" => new BuildingRoomOperationalSnapshot(new IBuildingWorldEntryPort[] { building }, true, usable, 1f, 1, 1, 1, StockCategory.General, null),
            "GetEffectiveCapacity" => 1,
            "GetRoomUtilityScore" => 1f,
            _ => throw new InvalidOperationException("Unexpected room fixture call: " + method.Name)
        });
        try
        {
            CharacterAiEditorTestDependencies.InjectWithRoomPolicy(building, policy);
            building.Initialization(source, Vector2Int.zero);
            var world = Port<IBuildingWorldQuery>((method, _) => method.Name == "get_Buildings" ? new[] { building } : 1);
            var characters = Port<ICharacterWorldQuery>((_, _) => Array.Empty<CharacterActor>());
            var projector = new V20MilestoneWorldSnapshotProjector(characters, world,
                Port<IStockQuery>((_, _) => Array.Empty<WorldItemStackSnapshot>()), new ProgressionSceneRuntimeReferences(null, null, null),
                Port<IFactionCampaignQuery>((_, _) => Array.Empty<FactionCampaignStateSaveData>()),
                Port<IGameMoneyAccount>((_, _) => 0), Port<IOffenseQuery>((_, _) => null), Port<IKinshipQuery>(),
                Port<ISettlementLaborAccountingService>((_, _) => default(SettlementLaborAccountingSnapshot)),
                Port<ISettlementAlertService>((_, _) => default(SettlementAlertSnapshot)));
            var live = new FacilityCapabilityQuery(world);
            var evaluator = new ContentRequirementEvaluator(characters, Port<ICharacterLifeQuery>(), Port<IKinshipQuery>(), Port<ICharacterNarrativeQuery>(), live);
            FactionChapterDefinitionSO chapter = catalog.Chapters.Single(value => value.StableId == "faction-chapter:demon:1");
            var choice = chapter.choices.Single(value => value.choiceId == "support");
            var campaign = new V20CampaignRuntime(new DungeonRuntimeAggregateRootStore(), catalog);
            RunMilestoneEvaluationSnapshot Snapshot()
            {
                var snapshot = projector.Build(1);
                foreach (var item in choice.requirements.items) snapshot.ItemQuantities[item.itemDefinitionId] = item.amount;
                foreach (var faction in campaign.CaptureFactions().factions) snapshot.Factions[faction.factionId] = faction;
                return snapshot;
            }
            var actual = Snapshot();
            var required = choice.requirements.facilities.Single();
            Require(actual.CountFacilities(required) == live.FindOperational(FacilityCapabilityKind.Administration).Count
                && actual.CountFacilities(required) == 1, "Actual projector omitted operational administration capability.");
            Require(evaluator.TryEvaluate(choice.requirements, actual, Array.Empty<string>(), out _), "Live facility preflight rejected actual operational facility.");
            var combined = new V20FacilityRequirement { buildingDefinitionId = source.ContentDefinitionId, capabilityId = "Administration", mustBeOperational = true, minimumCount = 1 };
            Require(actual.CountFacilities(combined) == 1, "Combined building/capability count was not projected.");
            usable = false;
            var unavailable = Snapshot();
            Require(!evaluator.TryEvaluate(choice.requirements, actual, Array.Empty<string>(), out _), "Stale snapshot bypassed live operational check.");
            Require(unavailable.CountFacilities(required) == 0, "Unusable room remained operational in campaign snapshot.");
            string before = JsonUtility.ToJson(campaign.CaptureFactions());
            Require(!campaign.TryResolveChapter(chapter.factionId, choice.choiceId, unavailable, out _, out _)
                && JsonUtility.ToJson(campaign.CaptureFactions()) == before, "Rejected facility condition partially changed faction state.");
            usable = true;
            Require(campaign.TryResolveChapter(chapter.factionId, choice.choiceId, Snapshot(), out var result, out _)
                && result.Effects.Any(value => value.kind == V20ContentEffectKind.ItemConsume), "Actual operational facility did not open chapter command on retry.");
            var saved = JsonUtility.FromJson<FactionCampaignWorldSaveData>(JsonUtility.ToJson(campaign.CaptureFactions()));
            var restored = new V20CampaignRuntime(new DungeonRuntimeAggregateRootStore(), catalog);
            restored.PublishFactions(restored.PrepareFactions(saved));
            Require(restored.TryGetFaction(chapter.factionId, out var after) && after.currentChapter == 2
                && after.majorChoiceFlags.Count(value => value == chapter.StableId + ":" + choice.choiceId) == 1,
                "Chapter choice did not survive JSON restore exactly once.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            Require(EditorJsonUtility.ToJson(source) == sourceJson && EditorUtility.IsDirty(source) == sourceDirty, "R19 facility fixture mutated its source asset.");
        }
    }

    private static void VerifyLivingCharacterMembership(
        ResourceGameContentCatalog content)
    {
        CharacterSO data = content.GetAll<CharacterSO>().First();
        GameObject firstObject = CharacterAiPlanDebugFixtures.CreateActorObject(
            "R19 living membership first");
        GameObject secondObject = CharacterAiPlanDebugFixtures.CreateActorObject(
            "R19 living membership second");
        CharacterActor first = firstObject.GetComponent<CharacterActor>();
        CharacterActor second = secondObject.GetComponent<CharacterActor>();
        try
        {
            int[] traitIds = (data.traits ?? Array.Empty<CharacterTraitSO>())
                .Where(trait => trait != null)
                .Select(trait => trait.id)
                .ToArray();
            first.Initialization(data);
            second.Initialization(data);
            first.Progression.ApplyPreparedIdentity(
                data.characterName,
                "r19:living:first",
                traitIds,
                CharacterPotentialGrade.Ordinary,
                generationSeed: 19001,
                autoChooseDrafts: false);
            second.Progression.ApplyPreparedIdentity(
                data.characterName,
                "r19:living:second",
                traitIds,
                CharacterPotentialGrade.Ordinary,
                generationSeed: 19002,
                autoChooseDrafts: false);
            CharacterActor[] current = { first };
            ICharacterWorldQuery characters = Port<ICharacterWorldQuery>(
                (method, _) => method.Name == "get_Characters"
                    ? current
                    : throw new InvalidOperationException(
                        "Unexpected character-world fixture call: "
                        + method.Name));
            var projector = new V20MilestoneWorldSnapshotProjector(
                characters,
                Port<IBuildingWorldQuery>((method, _) =>
                    method.Name == "get_Buildings"
                        ? Array.Empty<BuildableObject>()
                        : 0),
                Port<IStockQuery>((_, _) =>
                    Array.Empty<WorldItemStackSnapshot>()),
                new ProgressionSceneRuntimeReferences(null, null, null),
                Port<IFactionCampaignQuery>((_, _) =>
                    Array.Empty<FactionCampaignStateSaveData>()),
                Port<IGameMoneyAccount>((_, _) => 0),
                Port<IOffenseQuery>((_, _) => null),
                Port<IKinshipQuery>((method, _) => method.Name == "GetGeneration"
                    ? 1
                    : throw new InvalidOperationException(
                        "Unexpected kinship fixture call: " + method.Name)),
                Port<ISettlementLaborAccountingService>((_, _) =>
                    default(SettlementLaborAccountingSnapshot)),
                Port<ISettlementAlertService>((_, _) =>
                    default(SettlementAlertSnapshot)));

            projector.Build(1);
            IReadOnlyList<CharacterActor> exposed =
                projector.LivingCharacters;
            Require(exposed.Count == 1 && ReferenceEquals(exposed[0], first),
                "Living-character fixture did not retain the actor command reference.");
            RequireThrows<NotSupportedException>(
                () => ((IList<CharacterActor>)exposed)[0] = second,
                "Living-character membership accepted caller replacement.");
            Require(projector.LivingCharacters.Count == 1
                    && ReferenceEquals(projector.LivingCharacters[0], first),
                "Caller replacement changed cached living membership.");

            current = new[] { second };
            projector.Build(2);
            Require(projector.LivingCharacters.Count == 1
                    && ReferenceEquals(projector.LivingCharacters[0], second),
                "Fresh world capture did not replace living membership.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(firstObject);
            UnityEngine.Object.DestroyImmediate(secondObject);
        }
    }

    private static bool SetOut(object[] args, bool result) { args[2] = string.Empty; return result; }
    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static T Port<T>(Func<MethodInfo, object[], object> invoke = null) where T : class
    {
        T value = DispatchProxy.Create<T, StrictPort>();
        ((StrictPort)(object)value).Call = invoke ?? ((method, _) => throw new InvalidOperationException("Unexpected R19 fixture call: " + method.Name));
        return value;
    }
    public class StrictPort : DispatchProxy
    {
        public Func<MethodInfo, object[], object> Call;
        protected override object Invoke(MethodInfo method, object[] args) => Call(method, args);
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
