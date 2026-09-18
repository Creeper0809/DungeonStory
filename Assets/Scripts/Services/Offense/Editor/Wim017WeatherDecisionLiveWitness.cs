#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

// Root-owned integration witness. Only the fixture weather, historical event
// sequence and starting supplies/stress are controlled; no card/result is injected.
public static partial class OffenseJourneyPlayModeFacade
{
    public const string Wim017WeatherReportPath =
        "Artifacts/QA/wim-implementation/wim-017-main-weather-decision-ui-save.txt";
    public static bool Wim017WeatherRunning { get; private set; }
    private static readonly List<string> Wim017Lines = new();

    public static string StartWim017WeatherDecisionWitness()
    {
        WeatherRequire(Application.isPlaying && !Wim017WeatherRunning,
            "Fresh disposable main Play is required.");
        var scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        var host = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        WeatherRequire(scope?.Container != null && host != null, "Main runtime missing.");
        var save = scope.Container.Resolve<IDungeonSaveCommandService>() as IDisposable;
        WeatherRequire(save != null, "User persistence protection unavailable.");
        save.Dispose();
        scope.Container.Resolve<MetaProfilePersistenceService>().Dispose();
        PauseWeather(scope, true);
        Wim017Lines.Clear();
        WeatherRecord("result=RUNNING; source=main registered services, disposable Play");
        Wim017WeatherRunning = true;
        host.StartCoroutine(ObserveWeatherWitness(scope));
        return "RUNNING " + Wim017WeatherReportPath;
    }

    private static IEnumerator ObserveWeatherWitness(DungeonRuntimeLifetimeScope scope)
    {
        var pending = new Stack<IEnumerator>();
        pending.Push(RunWeatherWitness(scope));
        Exception failure = null;
        while (pending.Count > 0)
        {
            bool moved;
            object next = null;
            try
            {
                moved = pending.Peek().MoveNext();
                if (moved) next = pending.Peek().Current;
            }
            catch (Exception error) { failure = error; break; }
            if (!moved) { (pending.Pop() as IDisposable)?.Dispose(); continue; }
            if (next is IEnumerator nested) pending.Push(nested);
            else yield return next;
        }
        try
        {
            while (pending.Count > 0) (pending.Pop() as IDisposable)?.Dispose();
            PauseWeather(scope, true);
            WeatherRecord(failure == null ? "result=PASS" : "result=FAIL\n" + failure);
            WeatherRecord("scope=actual prepared party and public departure; controlled climate/current-format event sequence, supply/stress preparation and elapsed Tick; actual segment sampler/event publisher, main decision UI, registered whole-save, real effect consumers. NOT natural weather frequency, physical supply packing, natural full campaign or balance certification. User disk persistence disabled; runtime retained until Play stop.");
        }
        finally { Wim017WeatherRunning = false; }
    }

    private static IEnumerator RunWeatherWitness(DungeonRuntimeLifetimeScope scope)
    {
        var owner = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        WeatherRequire(owner != null, "Owner runtime missing.");
        if (owner.CurrentOwnerActor == null)
        {
            bool expanded = scope.Container.Resolve<IDungeonSpaceExpansionCommand>()
                .TryReconcileNewRunTierZero(out _, out string reason);
            WeatherRequire(expanded, "TierZero: " + reason);
            double ownerUiDeadline = Time.realtimeSinceStartupAsDouble + 15;
            bool ownerClicked = false;
            while (!ownerClicked
                && Time.realtimeSinceStartupAsDouble < ownerUiDeadline)
            {
                Button ownerButton = Resources.FindObjectsOfTypeAll<Button>()
                    .SingleOrDefault(value => value != null
                        && value.gameObject.scene.isLoaded
                        && value.gameObject.activeInHierarchy
                        && string.Equals(value.name, "OwnerOption_1001", StringComparison.Ordinal));
                ownerClicked = ownerButton != null
                    && ownerButton.IsInteractable()
                    && PlayModeVerificationFrameWait.DispatchPointerClick(
                        ownerButton.gameObject,
                        Vector2.zero);
                if (!ownerClicked) yield return null;
            }
            WeatherRequire(ownerClicked, "Owner selection not clickable after UI readiness wait.");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible(30f);
            PauseWeather(scope, true);
        }
        WeatherRequire(owner.CurrentOwnerActor != null, "Prepared party missing.");
        WeatherRequire(EnsureExpeditionResearchPrerequisite(scope,
            out string researchEvidence, out string researchFailure), researchFailure);
        WeatherRecord("preparation=existing owner/party UI + " + researchEvidence);

        var runtime = UnityEngine.Object.FindFirstObjectByType<OffenseExpeditionRuntime>();
        var world = scope.Container.Resolve<IOffenseWorldSimulation>();
        var targets = scope.Container.Resolve<IOffenseStrategicTargetService>();
        var travel = scope.Container.Resolve<IOffenseTravelRuntime>();
        var decisions = scope.Container.Resolve<IOffenseDecisionRuntime>();
        var gameSaves = scope.Container.Resolve<IDungeonGameSaveService>();
        var panels = scope.Container.Resolve<IOffensePanelService>();
        var content = scope.Container.Resolve<IOffenseContentCatalog>();
        var safety = scope.Container.Resolve<IOffenseReturnSafetyRuntime>();
        WeatherRequire(runtime != null && runtime.ActiveExpeditions.Count == 0,
            "Fresh main must have no existing expedition.");
        var available = runtime.GetAvailableMemberActors().Where(a => a != null
            && a.CurrentLifecycleState == CharacterLifecycleState.Active).Take(5).ToArray();
        OffenseTargetDefinition target = null;
        foreach (var site in world.Sites.Where(s => s != null && s.IsActive
                     && s.state == OffenseWorldSiteState.Revealed)
                 .OrderBy(s => s.strength).ThenBy(s => s.siteId, StringComparer.Ordinal))
        {
            if (targets.TryCreateTarget(site.siteId, out var candidate, out _)
                && candidate.requiredMembers <= available.Length)
            { target = candidate; break; }
        }
        WeatherRequire(target != null, "No actual revealed site with an eligible party.");
        PrepareWeatherArmor(scope, available[0]);
        bool started = runtime.TryStartExpedition(target.id,
            available.Take(target.requiredMembers), new OffenseSupplyLoadout(),
            new OffenseExpeditionPreparation(fieldFunds: 200), out var expedition,
            out string launchFailure);
        WeatherRequire(started, "Public departure: " + launchFailure);
        string id = expedition.ExpeditionId;
        double startWall = Time.realtimeSinceStartupAsDouble;
        PauseWeather(scope, false);
        while (!expedition.DepartureCompleted
            && Time.realtimeSinceStartupAsDouble - startWall < 60)
            yield return null;
        PauseWeather(scope, true);
        WeatherRequire(expedition.DepartureCompleted, "Actual departure did not complete within 60 wall seconds.");
        WeatherRequire(!decisions.TryGetActiveDecision(id, out _), "Departure already crossed a decision.");
        foreach (var actor in scope.Container.Resolve<ICharacterAiWorldRegistry>().Characters.Where(a => a != null))
            actor.SetAiPaused(true);
        // Current public run inputs, not effect outputs or physical-pack evidence.
        expedition.Supplies.Add(OffenseSupplyType.ManaLantern, 1);
        expedition.AdjustStress(30);
        WeatherRequire(travel.TryAdjustExposure(id, 25, out _), "Exposure preparation failed.");
        WeatherRecord($"departure=PASS; id={id}; members={expedition.MemberStates.Count}; wall={Time.realtimeSinceStartupAsDouble-startWall:R}; fieldFunds={expedition.FieldFunds}; fixtureManaLantern=1; fixtureStressDelta=30; fixtureExposureDelta=25");
        DungeonGameSaveData baseline = gameSaves.Capture();
        string[] cards = { "travel_rain_drift_cargo", "travel_fog_guide",
            "travel_heatwave_shade_shelter", "travel_cold_snap_frost_camp",
            "travel_storm_exposed_hideout", "travel_black_rain" };
        string[] weather = { "weather:rain", "weather:fog", "weather:heatwave",
            "weather:cold-snap", "weather:storm", "weather:storm" };

        for (int index = 0; index < cards.Length; index++)
        {
            RestoreWeatherWorld(gameSaves, baseline);
            SetFixtureWeather(scope, weather[index]);
            WeatherRequire(travel.TryGetState(id, out var state), "Travel missing after restore.");
            var destination = FindWeatherRoute(world, state.CurrentCoord);
            WeatherRequire(travel.TrySetDestination(id, destination, string.Empty,
                OffenseTravelProfile.Default, false, out string routeFailure), routeFailure);
            WeatherRequire(travel.TryGetState(id, out state) && state.remainingPath.Count >= 2,
                "Weather route must have a nonterminal first segment.");
            int sequence = FindWeatherSequence(content, safety, id, state.ActiveSegmentCoord,
                weather[index], cards[index]);
            DungeonGameSaveData prepared = gameSaves.Capture();
            var offense = GetWeatherOffense(prepared.sections);
            offense.world.travelStates.Single(t => t.expeditionId == id).eventSequence = sequence;
            SetWeatherOffense(prepared.sections, offense);
            RestoreWeatherWorld(gameSaves, prepared);
            WeatherRequire(travel.TryGetState(id, out state)
                && state.activeSegmentWeatherFrontId == weather[index], "Segment weather was not sampled.");
            float duration = state.activeSegmentDurationSeconds - state.progressToNextTile;
            // Changing current climate must not replace the completed segment's tag.
            SetFixtureWeather(scope, "weather:clear");
            string publishedWeather = null;
            int eventCount = 0;
            void OnDecision(string eventId, string front)
            { if (eventId == id) { eventCount++; publishedWeather = front; } }
            travel.DecisionRequired += OnDecision;
            try { travel.Tick(duration); }
            finally { travel.DecisionRequired -= OnDecision; }
            WeatherRequire(eventCount == 1 && publishedWeather == weather[index],
                "Actual completed-segment weather event missing or duplicated.");
            WeatherRequire(decisions.TryGetActiveDecision(id, out var view) && view.cardId == cards[index],
                "Actual main candidate selection did not publish " + cards[index]);
            DungeonGameSaveData posted = gameSaves.Capture();
            string decisionJson = JsonUtility.ToJson(GetWeatherOffense(posted.sections).world.decisions.Single(d => d.expeditionId == id));
            panels.ShowWorldMap();
            yield return null;
            RequireWeatherUi(view);
            RestoreWeatherWorld(gameSaves, posted);
            WeatherRequire(JsonUtility.ToJson(GetWeatherOffense(gameSaves.Capture().sections).world.decisions.Single(d => d.expeditionId == id)) == decisionJson,
                "Whole restore rerolled or changed the posted card.");
            panels.ShowWorldMap();
            yield return null;
            WeatherRequire(decisions.TryGetActiveDecision(id, out view) && view.cardId == cards[index],
                "UI reopen lost the posted card.");
            RequireWeatherUi(view);
            WeatherRecord($"card={cards[index]}; event=1; segmentWeather={publishedWeather}; currentWeather=weather:clear; preparedSequence={sequence}; wholeSavePosted=exact; UI=visible; decision={decisionJson}");

            if (index == 1 || index == 3)
            {
                expedition = runtime.ActiveExpeditions.Single(e => e.ExpeditionId == id);
                bool preparedShortage = index == 1
                    ? expedition.TrySpendFieldFunds(expedition.FieldFunds)
                    : expedition.TryConsumeSupply(OffenseSupplyType.ManaLantern, 1);
                WeatherRequire(preparedShortage, "Shortage input preparation failed.");
                var equipment = scope.Container.Resolve<ICombatEquipmentRuntime>();
                var beforeFailure = CaptureWeatherEffects(expedition, world, travel, equipment);
                var panel = panels.ShowWorldMap();
                yield return null;
                var costly = view.choices[0];
                WeatherRequire(ClickButtonByName("Button_" + costly.Label + "\n" + costly.DirectionLabel),
                    "Unaffordable actual choice not clickable.");
                WeatherRequire(decisions.TryGetActiveDecision(id, out var retained) && retained.cardId == cards[index],
                    "Unaffordable choice resolved or replaced the posted card.");
                WeatherRequire(beforeFailure.Signature == CaptureWeatherEffects(expedition, world, travel, equipment).Signature,
                    "Unaffordable choice partially applied effects.");
                WeatherRequire(panel.GetComponentsInChildren<TMP_Text>()
                    .Any(t => t.isActiveAndEnabled && t.text.Contains("부족")),
                    "Actual decision panel did not show the shortage reason.");
                WeatherRecord($"shortage={cards[index]}; actualPointer=PASS; activeCard=retained; effectAuthorities=unchanged; panelReason=visible");
            }

            for (int branch = 0; branch < 2; branch++)
            {
                RestoreWeatherWorld(gameSaves, posted);
                expedition = runtime.ActiveExpeditions.Single(e => e.ExpeditionId == id);
                WeatherRequire(decisions.TryGetActiveDecision(id, out view), "Posted choice unavailable.");
                var choice = view.choices[branch];
                panels.ShowWorldMap();
                yield return null;
                var equipment = scope.Container.Resolve<ICombatEquipmentRuntime>();
                var before = CaptureWeatherEffects(expedition, world, travel, equipment);
                WeatherRequire(ClickButtonByName("Button_" + choice.Label + "\n" + choice.DirectionLabel),
                    "Actual decision pointer unavailable: " + choice.ChoiceId);
                WeatherRequire(!decisions.TryGetActiveDecision(id, out _)
                    && travel.TryGetState(id, out var afterTravel) && !afterTravel.pausedForDecision,
                    "UI choice did not resolve and resume travel.");
                var after = CaptureWeatherEffects(expedition, world, travel, equipment);
                int roll = GetWeatherOffense(posted.sections).world.decisions.Single(d => d.expeditionId == id).deterministicRoll;
                AssertWeatherEffects(index, branch, roll, before, after);
                WeatherRequire(!runtime.TryResolveDecision(id, choice.ChoiceId, out _),
                    "Resolved choice replay was accepted.");
                WeatherRequire(after.Signature == CaptureWeatherEffects(expedition, world, travel, equipment).Signature,
                    "Rejected replay changed actual effect authorities.");
                WeatherRecord($"choice={cards[index]}/{choice.ChoiceId}; actualUI=PASS; effects=PASS; replay=unchanged; before={before.Signature}; after={after.Signature}");
                if (index == 5 && branch == 1)
                    WeatherRecord("blackRainArmorWear=actual physically backed equipped durability decreased; pickup AI not under test");
            }
        }
    }

    private static OffenseHexCoord FindWeatherRoute(IOffenseWorldSimulation world, OffenseHexCoord start)
    {
        var occupied = world.Sites.Where(s => s != null && s.IsActive).Select(s => s.Coord)
            .Concat(world.UrgentSites.Where(s => s != null && s.IsActive).Select(s => s.Coord)).ToHashSet();
        foreach (var tile in world.Tiles.Where(t => t != null && !t.blocked && !occupied.Contains(t.Coord))
                     .OrderBy(t => start.DistanceTo(t.Coord)).ThenBy(t => t.q).ThenBy(t => t.r))
            if (world.TryFindPath(start, tile.Coord, OffenseTravelProfile.Default, out var path, out _)
                && path.Count >= 2 && !occupied.Contains(path[0])) return tile.Coord;
        throw new InvalidOperationException("No real nonterminal weather route.");
    }

    private static int FindWeatherSequence(IOffenseContentCatalog content,
        IOffenseReturnSafetyRuntime safety, string id, OffenseHexCoord next, string weather, string card)
    {
        // A detached proposal chooses fixture INPUTS only. Main event publication,
        // persisted roll and actual effects are asserted separately below.
        var proposal = new OffenseDecisionRuntime(content, safety);
        for (int sequence = 0; sequence < 20000; sequence++)
        {
            if (DeterministicTravelDecisionHash(id, sequence, next.Q, next.R) % 100u >= 32u) continue;
            if (proposal.TryCreateDecision(new OffenseDecisionContext { expeditionId = id,
                sequence = sequence + 1, stage = OffenseDecisionStage.Travel,
                tags = new HashSet<string>(StringComparer.Ordinal) { weather } }, out var result, out _)
                && result.cardId == card) return sequence;
        }
        throw new InvalidOperationException("No bounded deterministic input for " + card);
    }

    private sealed class WeatherEffects
    {
        public float Hours, Exposure, Stress, Health, ArmorRatio;
        public int Funds, Food, Lantern, Loot, Revealed, Members, ArmorCount;
        public float[] MemberHealth, MemberMaxHealth;
        public string[] ArmorIds;
        public float[] ArmorRatios, ArmorMaxDurability;
        public string Signature => FormattableString.Invariant(
            $"hours:{Hours:R},exposure:{Exposure:R},stress:{Stress:R},hp:{Health:R},funds:{Funds},food:{Food},lantern:{Lantern},loot:{Loot},revealed:{Revealed},armorCount:{ArmorCount},armorRatio:{ArmorRatio:R}");
    }

    private static WeatherEffects CaptureWeatherEffects(OffenseExpeditionRun expedition,
        IOffenseWorldSimulation world, IOffenseTravelRuntime travel, ICombatEquipmentRuntime equipment)
    {
        WeatherRequire(travel.TryGetState(expedition.ExpeditionId, out var state), "Effect travel missing.");
        var members = expedition.MemberStates.OrderBy(m => m.Actor.Identity.PersistentId, StringComparer.Ordinal).ToArray();
        var armorIds = members.SelectMany(m => equipment.GetArmor(m.Actor.Identity.PersistentId).Select(a => a.InstanceId))
            .Concat(members.Select(m => equipment.GetShield(m.Actor.Identity.PersistentId)).Where(s => s.IsValid).Select(s => s.InstanceId))
            .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        float armorRatio = 0;
        var armorRatios = new float[armorIds.Length];
        var armorMax = new float[armorIds.Length];
        for (int a = 0; a < armorIds.Length; a++)
        {
            WeatherRequire(equipment.TryGetInstance(armorIds[a], out var instance), "Equipped armor owner missing.");
            WeatherRequire(equipment.TryGetDerivedStats(armorIds[a], out var stats), "Equipped armor stats missing.");
            armorRatios[a] = instance.durabilityRatio;
            armorMax[a] = stats.MaxDurability;
            armorRatio += instance.durabilityRatio;
        }
        return new WeatherEffects { Hours = world.WorldDay * 24f + world.WorldHour,
            Exposure = state.exposure, Stress = expedition.MemberStates.Sum(m => m.Stress),
            Health = expedition.MemberStates.Sum(m => m.Actor.CurrentHealth), Funds = expedition.FieldFunds,
            Food = expedition.Supplies.Get(OffenseSupplyType.Rations),
            Lantern = expedition.Supplies.Get(OffenseSupplyType.ManaLantern),
            Loot = expedition.CarriedStock.Values.Sum(),
            Revealed = world.Sites.Count(s => s.state != OffenseWorldSiteState.Hidden),
            Members = members.Length, ArmorCount = armorIds.Length, ArmorRatio = armorRatio,
            ArmorIds = armorIds, ArmorRatios = armorRatios, ArmorMaxDurability = armorMax,
            MemberHealth = members.Select(m => m.Actor.CurrentHealth).ToArray(),
            MemberMaxHealth = members.Select(m => m.Actor.MaxHealth).ToArray() };
    }

    private static void AssertWeatherEffects(int card, int branch, int roll, WeatherEffects before, WeatherEffects after)
    {
        // Authored contract literals, not expected values recomputed by handlers.
        float[,] hours = { { 1.5f, 0 }, { 0, 1.5f }, { 2, 0 }, { 0, 0 }, { 1.5f, 0 }, { 1, 0 } };
        float[,] exposure = { { 8, 0 }, { -10, 8 }, { -8, 8 }, { -12, 15 }, { 0, 0 }, { 0, 0 } };
        Near(after.Hours - before.Hours, hours[card, branch], "world hours");
        Near(after.Exposure - before.Exposure, exposure[card, branch], "exposure");
        WeatherRequire(after.Funds - before.Funds == (card == 1 && branch == 0 ? -100 : 0), "Field funds delta.");
        WeatherRequire(after.Food - before.Food == (card == 0 && branch == 0 ? 2 : 0), "Food delta.");
        WeatherRequire(after.Lantern - before.Lantern == (card == 3 && branch == 0 ? -1 : 0), "Lantern delta.");
        WeatherRequire(after.Loot - before.Loot == (card == 4 && branch == 0 ? 6 : 0), "Loot delta.");
        if (card == 1 && branch == 0) WeatherRequire(after.Revealed == before.Revealed + 1, "Recon did not reveal one real hidden site.");
        else WeatherRequire(after.Revealed == before.Revealed, "Unexpected recon.");
        bool injury = card == 4 && branch == 0 || card == 5 && branch == 1;
        int victim = (int)((uint)roll % (uint)before.Members);
        float damage = injury ? Mathf.Min(before.MemberMaxHealth[victim] * (card == 4 ? 0.09f : 0.115f),
            Mathf.Max(0, before.MemberHealth[victim] - 1)) : 0;
        for (int m = 0; m < before.Members; m++)
            Near(before.MemberHealth[m] - after.MemberHealth[m], m == victim ? damage : 0, "member HP");
        bool wear = card == 5 && branch == 1;
        WeatherRequire(before.ArmorIds.SequenceEqual(after.ArmorIds), "Decision changed equipped armor ownership.");
        if (wear)
        {
            WeatherRequire(before.ArmorCount > 0, "Black rain requires actual equipped armor.");
            int selected = (int)((uint)roll % (uint)before.ArmorCount);
            WeatherRequire(before.ArmorRatios[selected] > 0, "Armor preparation is already broken.");
            for (int a = 0; a < before.ArmorCount; a++)
                Near(after.ArmorRatios[a], a == selected
                    ? Mathf.Max(0, before.ArmorRatios[a] - 13f / before.ArmorMaxDurability[a])
                    : before.ArmorRatios[a], "armor durability ratio");
        }
        else Near(after.ArmorRatio, before.ArmorRatio, "unexpected armor wear");
        float[,] stress = { { 0, 0 }, { 0, 0 }, { -9, 7 }, { 0, 7 }, { 0, 0 }, { 10, 0 } };
        Near(after.Stress - before.Stress, stress[card, branch] * before.Members, "member stress");
    }

    private static void RequireWeatherUi(OffenseDecisionView view)
    {
        var titles = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
            .Where(t => t.isActiveAndEnabled && t.name == "DecisionTitle").ToArray();
        WeatherRequire(titles.Any(t => t.text == view.title), "Real decision title not visible.");
        WeatherRequire(UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
            .Any(t => t.isActiveAndEnabled && t.name == "DecisionSituation" && !string.IsNullOrWhiteSpace(t.text)),
            "Authored weather narrative not visible.");
    }

    private static void PrepareWeatherArmor(DungeonRuntimeLifetimeScope scope, CharacterActor actor)
    {
        // Same exact physical-backed assignment contract as the existing WIM053
        // fixture; no source-free assignment and no claim of natural pickup AI.
        var equipment = scope.Container.Resolve<ICombatEquipmentRuntime>();
        var items = scope.Container.Resolve<IWorldItemStackRuntime>();
        var grid = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>()?.grid;
        WeatherRequire(actor != null && grid != null, "Armor fixture actor/grid unavailable.");
        string characterId = actor.Identity.PersistentId;
        var armor = equipment.CreateInstance("armor:cloth-hood", CombatEquipmentQuality.Normal,
            CombatEquipmentWorldState.Carried);
        bool materialized = items.SpawnExistingUniqueItemAt(PhysicalItemIds.ForEquipment(armor.definitionId),
            (ItemInstanceId)armor.instanceId, grid.GetXY(actor.transform.position),
            WorldItemStackState.Carried, characterId, out string stackId);
        WeatherRequire(materialized && equipment.TryLinkToWorldStack(armor.instanceId,
            stackId, CombatEquipmentWorldState.Carried), "Exact physical armor materialization failed.");
        bool assigned = equipment.TryAssignToCharacter(characterId, armor.instanceId, out string failure);
        WeatherRequire(assigned && equipment.TryGetInstance(armor.instanceId, out var equipped)
            && equipped.worldState == CombatEquipmentWorldState.Equipped
            && equipped.ownerCharacterId == characterId && string.IsNullOrEmpty(equipped.sourceStackId)
            && !items.GetAllStacks().Any(s => s.StackId == stackId),
            "Physical-backed armor absorption/owner: " + failure);
        WeatherRecord($"fixtureArmor={armor.instanceId}; definition=armor:cloth-hood; owner={characterId}; physicalStackAbsorbed=exact; naturalPickup=NOT_RUN");
    }

    private static DungeonOffenseAggregateSaveData GetWeatherOffense(List<DungeonSaveSectionEnvelope> sections) =>
        JsonUtility.FromJson<DungeonOffenseAggregateSaveData>(sections.Single(s => s.sectionId == OffenseAggregateSaveSection.Id).payloadJson);
    private static void SetWeatherOffense(List<DungeonSaveSectionEnvelope> sections, DungeonOffenseAggregateSaveData data) =>
        sections.Single(s => s.sectionId == OffenseAggregateSaveSection.Id).payloadJson = JsonUtility.ToJson(data);
    private static void RestoreWeatherWorld(IDungeonGameSaveService gameSaves, DungeonGameSaveData snapshot)
    {
        WeatherRequire(gameSaves.TryRestore(snapshot, out DungeonGameRestoreReport report)
            && report.Success,
            "Current whole restore: " + string.Join(" | ", report.Errors));
    }
    private static void SetFixtureWeather(DungeonRuntimeLifetimeScope scope, string front)
    {
        var climate = scope.Container.Resolve<IClimatePersistence>();
        var data = climate.Capture();
        data.weatherFrontId = front;
        data.frontRemainingDays = 1;
        climate.PublishRestore(climate.PrepareRestore(data));
    }
    private static void PauseWeather(DungeonRuntimeLifetimeScope scope, bool paused)
    {
        scope.Container.Resolve<IGameTimeScaleController>().Scale = paused ? 0 : 1;
        UnityEngine.Object.FindFirstObjectByType<GameManager>().isPause = paused;
    }
    private static void Near(float actual, float expected, string label) =>
        WeatherRequire(Mathf.Abs(actual - expected) < 0.001f, $"{label}: expected={expected:R}; actual={actual:R}");
    private static void WeatherRequire(bool condition, string reason)
    { if (!condition) throw new InvalidOperationException(reason); }
    private static void WeatherRecord(string line)
    {
        Wim017Lines.Add(line);
        Directory.CreateDirectory(Path.GetDirectoryName(Wim017WeatherReportPath));
        File.WriteAllLines(Wim017WeatherReportPath, Wim017Lines);
    }
}
#endif
