#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class WildlifeDebugScenarios
{
    private const string ReportPath = "Temp/wildlife-contracts.tsv";

    [MenuItem("DungeonStory/Debug/Wildlife/Run Wildlife Contracts")]
    public static void RunAllFromMenu()
    {
        bool success = RunAll(logSuccess: true);
        if (!success)
        {
            Debug.LogError("Wildlife contracts failed.");
        }
    }

    [MenuItem("DungeonStory/Debug/Wildlife/Run Wildlife PlayMode Snapshot")]
    public static void RunPlayModeSnapshotFromMenu()
    {
        bool success = RunPlayModeSnapshot(logSuccess: true);
        if (!success)
        {
            Debug.LogError("Wildlife PlayMode snapshot failed.");
        }
    }

    [MenuItem("DungeonStory/Debug/Wildlife/Run Wildlife PlayMode Hunt Loop")]
    public static void RunPlayModeHuntLoopFromMenu()
    {
        bool success = RunPlayModeHuntLoop(logSuccess: true);
        if (!success)
        {
            Debug.LogError("Wildlife PlayMode hunt loop failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        Directory.CreateDirectory("Temp");
        List<string> lines = new List<string> { "case\tresult\tdetails" };
        List<string> errors = new List<string>();

        Run("grid_layer_and_priority", VerifyGridLayerAndPriority, lines, errors);
        Run("species_catalog", VerifySpeciesCatalog, lines, errors);
        Run("wildlife_item_definitions", VerifyWildlifeItemDefinitions, lines, errors);
        Run("hunt_and_butcher_work_types", VerifyWorkTypes, lines, errors);
        Run("save_v10_wildlife_contract", VerifySaveV10WildlifeContract, lines, errors);
        Run("runtime_state_not_so", VerifyRuntimeStateIsNotScriptableObject, lines, errors);
        Run("actor_collaborator_ownership", VerifyActorCollaboratorOwnership, lines, errors);
        Run("initial_spawn_exterior_path_only", VerifyInitialSpawnExteriorPathOnly, lines, errors);
        Run("default_physical_world_blocks_air_spawn", VerifyDefaultPhysicalWorldBlocksAirSpawn, lines, errors);
        Run("visual_grounding_and_wounded_healthbar", VerifyVisualGroundingAndWoundedHealthbar, lines, errors);
        Run("shared_combat_body_profile", VerifySharedCombatBodyProfile, lines, errors);
        Run("natural_motion_dwell_and_facing", VerifyNaturalMotionDwellAndFacing, lines, errors);
        Run("ecosystem_patch_resource_loop", VerifyEcosystemPatchResourceLoop, lines, errors);
        Run("authored_habitat_decoration_palette", VerifyAuthoredHabitatDecorationPalette, lines, errors);
        Run("habitat_decoration_consumption_visual", VerifyHabitatDecorationConsumptionVisual, lines, errors);
        Run("ecosystem_target_selection", VerifyEcosystemTargetSelection, lines, errors);
        Run("ecosystem_respawn_pressure", VerifyEcosystemRespawnPressure, lines, errors);

        File.WriteAllLines(ReportPath, lines);
        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log($"Wildlife contracts PASS. Report: {ReportPath}");
        }

        return true;
    }

    public static bool RunPlayModeSnapshot(bool logSuccess)
    {
        List<string> errors = new List<string>();
        RunScenario("playmode runtime services", VerifyPlayModeRuntimeServices, errors);
        RunScenario("playmode habitat decorations", VerifyPlayModeHabitatDecorations, errors);
        RunScenario("playmode wildlife spawn area", VerifyPlayModeWildlifeSpawnArea, errors);
        RunScenario("playmode info panel", VerifyPlayModeInfoPanel, errors);
        RunScenario("playmode save capture", VerifyPlayModeSaveCapture, errors);
        RunScenario(
            "playmode invalid wildlife preflight preserves live actors",
            VerifyInvalidWildlifePreflightPreservesLiveActors,
            errors);
        RunScenario(
            "playmode wildlife roundtrip publishes detached actors",
            VerifyWildlifeRoundtripPublishesDetachedActors,
            errors);
        RunScenario(
            "playmode later failure discards wildlife candidate",
            VerifyLaterFailureDiscardsWildlifeCandidate,
            errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("Wildlife PlayMode snapshot passed.");
        }

        return true;
    }

    public static bool RunPlayModeHuntLoop(bool logSuccess)
    {
        List<string> errors = new List<string>();
        string report = string.Empty;
        RunScenario(
            "playmode hunt carcass butcher loop",
            () => VerifyPlayModeHuntCarcassButcherLoop(out report),
            errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(string.IsNullOrWhiteSpace(report) ? error : $"{error}: {report}");
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log($"Wildlife PlayMode hunt loop passed. {report}");
        }

        return true;
    }

    private static string VerifyGridLayerAndPriority()
    {
        GridCell exterior = new GridCell(new Vector2Int(1, 0));
        exterior.SetAreaType(GridCellAreaType.ExteriorPath);
        Require(exterior.CanOccupy(GridLayer.Wildlife), "wildlife could not occupy exterior path");
        Require(exterior.CanOccupy(GridLayer.Item), "item could not occupy exterior path");
        exterior.SetAreaType(GridCellAreaType.BlockedExterior);
        Require(!exterior.CanOccupy(GridLayer.Wildlife), "wildlife could occupy blocked exterior");

        GridCell priorityCell = new GridCell(Vector2Int.zero);
        TestOccupant building = new TestOccupant();
        TestOccupant item = new TestOccupant();
        TestOccupant wildlife = new TestOccupant();
        TestOccupant character = new TestOccupant();
        Require(priorityCell.TrySetOccupant(GridLayer.Building, building), "building occupant failed");
        Require(priorityCell.TrySetOccupant(GridLayer.Item, item), "item occupant failed");
        Require(priorityCell.TrySetOccupant(GridLayer.Wildlife, wildlife), "wildlife occupant failed");
        Require(priorityCell.TrySetOccupant(GridLayer.Character, character), "character occupant failed");
        Require(ReferenceEquals(priorityCell.GetTopOccupant(), character), "character was not top priority");
        priorityCell.RemoveOccupantByLayer(GridLayer.Character);
        Require(ReferenceEquals(priorityCell.GetTopOccupant(), wildlife), "wildlife was not above item/building");

        return "wildlife layer allowed outside and prioritized below character";
    }

    private static string VerifySpeciesCatalog()
    {
        ResourceGameContentCatalog content =
            new ResourceGameContentCatalog(new UnityGameContentRootLoader());
        ResourceWildlifeSpeciesCatalogProvider catalog =
            new ResourceWildlifeSpeciesCatalogProvider(
                content,
                new ResourceItemDefinitionCatalog(content));
        IReadOnlyList<WildlifeSpeciesDefinition> species = catalog.All;
        Require(species.Count >= 5, $"expected at least 5 species, got {species.Count}");
        Require(catalog.TryGetSpecies("shadow_wolf", out WildlifeSpeciesDefinition wolf), "shadow wolf missing");
        Require(wolf.IsPredator && wolf.IsDangerous, "shadow wolf should be dangerous predator");
        Require(catalog.TryGetSpecies("rune_deer", out WildlifeSpeciesDefinition deer), "rune deer missing");
        Require(deer.ButcherYields.Any(yield => yield.itemId == WildlifeItemDefinitions.RuneDustItemId),
            "rune deer should produce rune dust");
        return $"species={species.Count}; wolf={wolf.DisplayName}; deer={deer.DisplayName}";
    }

    private static string VerifyWildlifeItemDefinitions()
    {
        ResourceDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        DungeonItemDefinition carcass = catalog.GetDefinition("wild:carcass:cave_rat");
        DungeonItemDefinition hide = catalog.GetDefinition(WildlifeItemDefinitions.HideItemId);
        DungeonItemDefinition rot = catalog.GetDefinition(WildlifeItemDefinitions.RotItemId);

        Require(WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(carcass.ItemId, out string speciesId),
            "carcass item id did not parse species");
        Require(speciesId == "cave_rat", $"unexpected carcass species {speciesId}");
        Require(carcass.MaxStack == 1 && carcass.UnitWeight > 0f, "carcass physical data invalid");
        Require(hide.MaxStack > 1 && hide.UnitPrice > 0, "hide definition invalid");
        Require(rot.UnitPrice == 0, "rot should have no value");
        return $"carcass={carcass.DisplayName}; hide={hide.DisplayName}; rot={rot.DisplayName}";
    }

    private static string VerifyWorkTypes()
    {
        Require(WorkTypeCatalog.TryGet(BuiltInWorkTypeIds.Hunt, out WorkTypeDefinition hunt),
            "hunt work type missing");
        Require(WorkTypeCatalog.TryGet(BuiltInWorkTypeIds.Butcher, out WorkTypeDefinition butcher),
            "butcher work type missing");
        WorkPriorityProfile priorities = WorkPriorityProfile.CreateDefault();
        Require(priorities.GetPriority(BuiltInWorkTypeIds.Hunt) == WorkPriorityLevel.Priority2,
            "hunt default priority should be 2");
        Require(priorities.GetPriority(BuiltInWorkTypeIds.Butcher) == WorkPriorityLevel.Priority2,
            "butcher default priority should be 2");
        return $"hunt={hunt.DisplayName}; butcher={butcher.DisplayName}";
    }

    private static string VerifySaveV10WildlifeContract()
    {
        DungeonGameSaveData save = new DungeonGameSaveData();
        Require(
            DungeonGameSaveData.CurrentVersion >= 16,
            $"save version is {DungeonGameSaveData.CurrentVersion}");
        Require(save.version == DungeonGameSaveData.CurrentVersion, $"new save version is {save.version}");
        DungeonWildlifeSaveData wildlife = new DungeonWildlifeSaveData();
        DungeonSurvivalSaveData survival = new DungeonSurvivalSaveData();
        DungeonSaveSectionPayload.Write(
            save,
            WildlifeSaveSection.Id,
            DungeonWildlifeSaveData.CurrentVersion,
            DungeonSaveRestorePhase.RuntimeState,
            wildlife);
        DungeonSaveSectionPayload.Write(
            save,
            SurvivalResourcesSaveSection.Id,
            DungeonSurvivalSaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState,
            survival);
        Require(
            DungeonSaveSectionPayload.TryRead(
                save,
                WildlifeSaveSection.Id,
                out wildlife)
            && wildlife.version == DungeonWildlifeSaveData.CurrentVersion,
            "wildlife snapshot missing");
        Require(wildlife.ecosystem != null
            && wildlife.ecosystem.version == DungeonWildlifeEcosystemSaveData.CurrentVersion,
            "wildlife ecosystem snapshot missing");
        Require(
            DungeonSaveSectionPayload.TryRead(
                save,
                SurvivalResourcesSaveSection.Id,
                out survival)
            && survival.version == DungeonSurvivalSaveData.CurrentVersion,
            "survival snapshot missing");
        return $"version={save.version}; wildlife={wildlife.version}; ecosystem={wildlife.ecosystem.version}; survival={survival.version}";
    }

    private static string VerifyRuntimeStateIsNotScriptableObject()
    {
        Require(!typeof(WildlifeRuntime).IsSubclassOf(typeof(ScriptableObject)),
            "WildlifeRuntime must not be a ScriptableObject");
        Require(!typeof(WildlifeEcosystemRuntime).IsSubclassOf(typeof(ScriptableObject)),
            "WildlifeEcosystemRuntime must not be a ScriptableObject");
        Require(typeof(MonoBehaviour).IsAssignableFrom(typeof(WildlifeActor)),
            "WildlifeActor should be a MonoBehaviour");
        Require(typeof(WildlifeSpeciesSO).IsSubclassOf(typeof(ScriptableObject)),
            "WildlifeSpeciesSO should be static species data");
        return "runtime/state separated from species SO";
    }

    private static string VerifyActorCollaboratorOwnership()
    {
        Type actorType = typeof(WildlifeActor);
        Type visualType = typeof(WildlifeVisualPresentation);
        Type conditionType = typeof(WildlifeNaturalCondition);
        BindingFlags instanceFields = BindingFlags.Instance
            | BindingFlags.Public
            | BindingFlags.NonPublic;

        Require(!typeof(MonoBehaviour).IsAssignableFrom(visualType),
            "wildlife visual presentation must remain a plain C# object");
        Require(!typeof(MonoBehaviour).IsAssignableFrom(conditionType),
            "wildlife natural condition must remain a plain C# object");
        Require(actorType.GetField("visualPresentation", instanceFields)?.FieldType == visualType,
            "actor must own one visual presentation collaborator");
        Require(actorType.GetField("naturalCondition", instanceFields)?.FieldType == conditionType,
            "actor must own one natural condition collaborator");

        string[] removedActorFields =
        {
            "visualRoot",
            "visualRenderer",
            "markerRenderer",
            "healthRoot",
            "hunger",
            "thirst",
            "intent",
            "territoryCenter",
            "lastThreatPosition"
        };
        foreach (string fieldName in removedActorFields)
        {
            Require(actorType.GetField(fieldName, instanceFields) == null,
                $"actor still owns extracted state '{fieldName}'");
        }

        Require(visualType.GetField("visualRoot", instanceFields) != null,
            "visual collaborator does not own its visual root");
        Require(conditionType.GetField("hunger", instanceFields) != null,
            "natural condition does not own hunger");
        Require(conditionType.GetField("lastThreatPosition", instanceFields) != null,
            "natural condition does not own threat memory");
        Require(actorType.GetMethod("Initialize", BindingFlags.Instance | BindingFlags.Public) != null,
            "actor initialization API changed");
        Require(actorType.GetMethod("Tick", BindingFlags.Instance | BindingFlags.Public) != null,
            "actor tick API changed");
        Require(actorType.GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic) != null,
            "actor Unity destruction hook changed");

        WildlifeSaveData source = new WildlifeSaveData
        {
            fear = 2.5f,
            hunger = 0.42f,
            thirst = 0.63f,
            intent = WildlifeIntent.Drink,
            intentReason = "condition ownership regression",
            hasTerritory = true,
            territoryX = 11,
            territoryY = 4,
            hasHerdAnchor = true,
            herdAnchorX = 9,
            herdAnchorY = 4,
            hasLastThreat = true,
            lastThreatX = 14,
            lastThreatY = 5
        };
        WildlifeNaturalCondition condition = new WildlifeNaturalCondition();
        condition.Initialize(source, Vector2Int.zero, 20f, 0f, 0f);
        WildlifeSaveData captured = new WildlifeSaveData();
        condition.CaptureInto(captured);
        Require(Mathf.Approximately(captured.fear, source.fear)
            && Mathf.Approximately(captured.hunger, source.hunger)
            && Mathf.Approximately(captured.thirst, source.thirst),
            "natural condition numeric state failed save roundtrip");
        Require(captured.intent == source.intent
            && captured.intentReason == source.intentReason
            && captured.territoryX == source.territoryX
            && captured.herdAnchorX == source.herdAnchorX
            && captured.lastThreatX == source.lastThreatX,
            "natural condition identity state failed save roundtrip");

        return "visual and natural state owned by plain C# collaborators";
    }

    private static string VerifyInitialSpawnExteriorPathOnly()
    {
        Grid grid = new Grid(36, 3);
        for (int y = 0; y < grid.height; y++)
        {
            for (int x = 0; x < grid.width; x++)
            {
                GridCell cell = grid.GetGridCell(new Vector2Int(x, y));
                cell.SetAreaType(GridCellAreaType.ExteriorPath);
            }
        }

        grid.GetGridCell(new Vector2Int(1, 0)).SetAreaType(GridCellAreaType.DropZone);
        grid.GetGridCell(new Vector2Int(4, 0)).SetAreaType(GridCellAreaType.Entrance);
        grid.GetGridCell(new Vector2Int(5, 0)).SetAreaType(GridCellAreaType.DungeonInterior);
        grid.GetGridCell(new Vector2Int(6, 0)).SetAreaType(GridCellAreaType.BlockedExterior);

        Require(WildlifeRuntime.IsInitialWildlifeSpawnCell(grid, grid.GetGridCell(new Vector2Int(0, 0))),
            "exterior path was rejected as initial wildlife spawn");
        Require(!WildlifeRuntime.IsInitialWildlifeSpawnCell(grid, grid.GetGridCell(new Vector2Int(1, 0))),
            "drop zone should not spawn wildlife");
        Require(!WildlifeRuntime.IsInitialWildlifeSpawnCell(grid, grid.GetGridCell(new Vector2Int(4, 0))),
            "entrance should not spawn wildlife");
        Require(!WildlifeRuntime.IsInitialWildlifeSpawnCell(grid, grid.GetGridCell(new Vector2Int(5, 0))),
            "dungeon interior should not spawn wildlife");
        Require(!WildlifeRuntime.IsInitialWildlifeSpawnCell(grid, grid.GetGridCell(new Vector2Int(6, 0))),
            "blocked exterior should not spawn wildlife");
        Require(!WildlifeRuntime.IsInitialWildlifeSpawnCell(grid, grid.GetGridCell(new Vector2Int(0, 1))),
            "upper exterior path without ground support should not spawn wildlife");
        return "initial spawn restricted to exterior path cells";
    }

    private static string VerifyDefaultPhysicalWorldBlocksAirSpawn()
    {
        Grid grid = new Grid(36, 3);
        GridSystemManager.ApplyPhysicalWorldAreas(
            grid,
            dungeonInteriorStartX: 4,
            dungeonInteriorColumnCount: 27,
            dropZoneWidth: 3,
            entranceGridPosition: new Vector2Int(4, 0));

        Require(WildlifeRuntime.IsInitialWildlifeSpawnCell(grid, grid.GetGridCell(new Vector2Int(0, 0))),
            "ground exterior path should spawn wildlife");
        Require(!WildlifeRuntime.IsInitialWildlifeSpawnCell(grid, grid.GetGridCell(new Vector2Int(0, 1))),
            "air above exterior ground should not spawn wildlife");
        Require(grid.GetGridCell(new Vector2Int(0, 1)).AreaType == GridCellAreaType.BlockedExterior,
            "default physical world should mark upper exterior as blocked");
        return "default physical world keeps wildlife on exterior ground";
    }

    private static string VerifyVisualGroundingAndWoundedHealthbar()
    {
        Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                texture.SetPixel(x, y, Color.white);
            }
        }

        texture.Apply();
        Sprite centeredPivotSprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            16f);
        WildlifeSpeciesDefinition species = new WildlifeSpeciesDefinition(
            "pivot_test",
            "피벗 시험",
            string.Empty,
            centeredPivotSprite,
            maxHealth: 10,
            moveSpeed: 1f,
            fearSensitivity: 1f,
            aggression: 0f,
            retaliationDamage: 0,
            spawnWeight: 1f,
            herdSize: 1,
            canEnterDungeon: true,
            carcassWeight: 1f,
            butcherYields: Array.Empty<WildlifeButcherYield>(),
            husbandry: new WildlifeHusbandryProfile(
                true,
                0.5f,
                2f,
                20f,
                3f,
                false,
                1f,
                2f));
        Grid grid = new Grid(4, 1);
        for (int x = 0; x < grid.width; x++)
        {
            Vector2Int pos = new Vector2Int(x, 0);
            grid.GetGridCell(pos).SetAreaType(GridCellAreaType.ExteriorPath);
        }

        GameObject actorObject = new GameObject("WildlifeVisualContract");
        try
        {
            WildlifeActor actor = actorObject.AddComponent<WildlifeActor>();
            ConfigureActor(actor, 36117);
            actor.Initialize(grid, species, "visual-test", Vector2Int.zero);
            SpriteRenderer renderer = actor.VisualRenderer;
            Require(renderer != null, "wildlife visual renderer missing");
            Require(Mathf.Abs(renderer.bounds.min.y - actorObject.transform.position.y) <= 0.02f,
                $"wildlife visual was not grounded. bottom={renderer.bounds.min.y}, root={actorObject.transform.position.y}");
            Require(!actor.IsHealthBarVisibleForDebug, "full-health wildlife health bar should be hidden");

            actor.ApplyDamage(3, null);
            actor.Tick(0f);
            Require(actor.IsHealthBarVisibleForDebug, "damaged wildlife health bar should be visible");
            Require(renderer.sortingLayerName == "Default",
                $"wildlife default sorting layer should match characters. got={renderer.sortingLayerName}");
            return "visual foot anchor and wounded-only health bar verified";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actorObject);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static string VerifyNaturalMotionDwellAndFacing()
    {
        Grid grid = CreateExteriorGrid(12);
        GameObject actorObject = new GameObject("WildlifeNaturalMotionContract");
        try
        {
            WildlifeActor actor = actorObject.AddComponent<WildlifeActor>();
            actor.ConfigureRuntimeServices(
                CreatePathSearchBroker(),
                CharacterAiEditorTestDependencies.WorldRegistry,
                new UnityGameClock(),
                new RandomStreamProvider(39017),
                OpenDoorAccessQuery.Instance);
            actor.Initialize(grid, WildlifeTestFixtures.CaveRat, "natural-motion", new Vector2Int(3, 0));
            float now = Time.time;
            Require(actor.TrySetPath(new Vector2Int(8, 0), now), "wildlife did not begin a valid route");
            float outboundWorldDelta = grid.GetWorldPos(new Vector2Int(8, 0)).x
                - grid.GetWorldPos(new Vector2Int(3, 0)).x;
            Require(outboundWorldDelta < 0f && actor.LastHorizontalDirection < 0,
                "world-left route did not set movement facing");
            Require(actor.VisualRenderer != null && actor.VisualRenderer.flipX,
                "world-left route should flip the right-facing source sprite");

            for (int index = 0; index < 160 && actor.IsMoving; index++)
            {
                actor.Tick(0.05f);
            }

            Require(!actor.IsMoving && actor.GridPosition == new Vector2Int(8, 0),
                $"wildlife did not finish its route. position={actor.GridPosition}; moving={actor.IsMoving}");
            Require(!actor.CanRepath(Time.time), "wildlife should dwell after reaching a destination");

            actor.RegisterThreat(new Vector2Int(9, 0), 1f);
            Require(actor.CanRepath(Time.time), "threat should interrupt non-critical arrival dwell");
            Require(actor.TrySetPath(new Vector2Int(4, 0), Time.time), "wildlife did not begin flee route");
            float returnWorldDelta = grid.GetWorldPos(new Vector2Int(4, 0)).x
                - grid.GetWorldPos(new Vector2Int(8, 0)).x;
            Require(returnWorldDelta > 0f && actor.LastHorizontalDirection > 0,
                "world-right route did not update movement facing");
            Require(!actor.VisualRenderer.flipX,
                "world-right route should preserve the right-facing source sprite");
            return "arrival dwell, threat interruption, eased travel, and world-space sprite facing verified";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static string VerifySharedCombatBodyProfile()
    {
        Grid grid = CreateExteriorGrid(4);
        GameObject actorObject = new GameObject("WildlifeCombatBodyContract");
        GameObject restoredObject = null;
        try
        {
            WildlifeActor actor = actorObject.AddComponent<WildlifeActor>();
            ConfigureActor(actor, 43917);
            actor.Initialize(grid, WildlifeTestFixtures.MossBoar, "combat-body", new Vector2Int(1, 0));
            float mobilityBefore = actor.CombatMobility;
            int healthBefore = actor.CurrentHealth;
            CombatAttackResult legHit = new CombatAttackResult(
                executed: true,
                hit: true,
                coverBlocked: false,
                evaded: false,
                bodyPart: CombatBodyPart.LeftLeg,
                rawDamage: 7f,
                appliedDamage: 7f,
                bleeding: 0.5f,
                suppression: 4f,
                armorDurabilityDamage: 0f,
                armorInstanceId: string.Empty,
                failureReason: string.Empty);
            int applied = actor.ApplyCombatDamage(legHit, null);
            Require(applied == 7, $"expected 7 applied damage, got {applied}");
            Require(actor.CurrentHealth == healthBefore - 7, "shared combat result did not reduce wildlife health");
            Require(actor.CombatMobility < mobilityBefore, "leg hit did not reduce wildlife mobility");
            float mobilityAfter = actor.CombatMobility;

            WildlifeSaveData save = actor.Capture();
            Require(save.hasCombatBodyProfile, "wildlife combat body was not captured");
            UnityEngine.Object.DestroyImmediate(actorObject);
            actorObject = null;

            Grid restoredGrid = CreateExteriorGrid(4);
            restoredObject = new GameObject("WildlifeCombatBodyRestored");
            WildlifeActor restored = restoredObject.AddComponent<WildlifeActor>();
            ConfigureActor(restored, 46917);
            restored.Initialize(
                restoredGrid,
                WildlifeTestFixtures.MossBoar,
                save.wildlifeId,
                new Vector2Int(save.gridX, save.gridY),
                save);
            Require(restored.CurrentHealth == healthBefore - 7, "wildlife health did not restore");
            Require(
                Mathf.Abs(restored.CombatMobility - mobilityAfter) <= 0.001f,
                "wildlife limb injury mobility did not restore");
            Require(restored.CombatMobility < 1f, "wildlife limb injury did not persist");
            return $"health={healthBefore}->{restored.CurrentHealth}; mobility={mobilityBefore:0.00}->{restored.CombatMobility:0.00}";
        }
        finally
        {
            if (actorObject != null)
            {
                UnityEngine.Object.DestroyImmediate(actorObject);
            }
            if (restoredObject != null)
            {
                UnityEngine.Object.DestroyImmediate(restoredObject);
            }
        }
    }

    private static string VerifyEcosystemPatchResourceLoop()
    {
        WildlifeHabitatPatch patch = new WildlifeHabitatPatch(
            "wildlife-habitat:test:grass",
            WildlifeHabitatType.Grass,
            Vector2Int.zero,
            radius: 2,
            resourceCapacity: 4f,
            currentResource: 2f,
            regenPerSecond: 1f,
            danger: 0f);
        float consumed = patch.Consume(1.25f);
        Require(Mathf.Abs(consumed - 1.25f) <= 0.001f, "patch did not consume requested resource");
        Require(patch.CurrentResource < 2f, "patch resource did not decrease");
        patch.Tick(1f);
        Require(patch.CurrentResource > 1.7f, "patch resource did not regenerate");
        Require(patch.Contains(new Vector2Int(1, 1)), "patch radius should include nearby cell");
        return $"resource={patch.CurrentResource:0.##}/{patch.ResourceCapacity:0.##}";
    }

    private static string VerifyAuthoredHabitatDecorationPalette()
    {
        WildlifeHabitatDecorationPaletteSO palette =
            Resources.Load<WildlifeHabitatDecorationPaletteSO>(
                WildlifeHabitatDecorationPaletteSO.ResourcePath);
        Require(palette != null, "authored wildlife decoration palette is missing");
        Require(palette.IsComplete, "authored wildlife decoration palette is incomplete");
        Require(palette.FlowerSprites.Count >= 6,
            $"expected flower variants, got {palette.FlowerSprites.Count}");
        Require(palette.TreeSprites.Count >= 3,
            $"expected tree variants, got {palette.TreeSprites.Count}");
        Require(palette.RockSprites.Count >= 3,
            $"expected rock variants, got {palette.RockSprites.Count}");
        return $"flowers={palette.FlowerSprites.Count}; trees={palette.TreeSprites.Count}; rocks={palette.RockSprites.Count}";
    }

    private static string VerifyHabitatDecorationConsumptionVisual()
    {
        Grid grid = CreateExteriorGrid(32);
        WildlifeHabitatPatch grass = new WildlifeHabitatPatch(
            "wildlife-habitat:test:decor:grass",
            WildlifeHabitatType.Grass,
            new Vector2Int(5, 0),
            radius: 3,
            resourceCapacity: 4f,
            currentResource: 4f,
            regenPerSecond: 0.5f,
            danger: 0f);
        WildlifeHabitatPatch brush = new WildlifeHabitatPatch(
            "wildlife-habitat:test:decor:brush",
            WildlifeHabitatType.Brush,
            new Vector2Int(14, 0),
            radius: 3,
            resourceCapacity: 3f,
            currentResource: 3f,
            regenPerSecond: 0.25f,
            danger: 0f);
        WildlifeHabitatPatch burrow = new WildlifeHabitatPatch(
            "wildlife-habitat:test:decor:burrow",
            WildlifeHabitatType.Burrow,
            new Vector2Int(24, 0),
            radius: 2,
            resourceCapacity: 2f,
            currentResource: 2f,
            regenPerSecond: 0.1f,
            danger: 0f);
        WildlifeHabitatPatch[] patches = { grass, brush, burrow };
        WildlifeHabitatDecorationPaletteSO palette =
            Resources.Load<WildlifeHabitatDecorationPaletteSO>(
                WildlifeHabitatDecorationPaletteSO.ResourcePath);
        Require(palette != null && palette.IsComplete, "decoration palette unavailable for runtime contract");

        WildlifeHabitatDecorationRuntime decorations = new WildlifeHabitatDecorationRuntime(
            new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        try
        {
            decorations.Rebuild(grid, patches, palette);
            int fullCount = decorations.GetVisibleFlowerCount(grass.PatchId);
            Require(decorations.IsReady, "decoration runtime did not initialize");
            Require(decorations.FlowerPatchCount == 2,
                $"expected two consumable flower patches, got {decorations.FlowerPatchCount}");
            Require(fullCount == palette.FlowersPerGrassPatch,
                $"full grass patch should show all flowers. visible={fullCount}");
            Require(decorations.TreeCount >= 1, "brush habitat did not place a tree");
            Require(decorations.RockCount >= 1, "burrow habitat did not place a rock");

            grass.Consume(grass.ResourceCapacity);
            decorations.RefreshPatch(grass);
            Require(decorations.GetVisibleFlowerCount(grass.PatchId) == 0,
                "depleted grass patch still displayed flowers");

            grass.Tick(4f);
            decorations.RefreshPatch(grass);
            int regrownCount = decorations.GetVisibleFlowerCount(grass.PatchId);
            Require(regrownCount > 0 && regrownCount < fullCount,
                $"regrowing patch did not restore flowers progressively. visible={regrownCount}");
            return $"full={fullCount}; depleted=0; regrown={regrownCount}; trees={decorations.TreeCount}; rocks={decorations.RockCount}";
        }
        finally
        {
            decorations.Dispose();
        }
    }

    private static string VerifyEcosystemTargetSelection()
    {
        Grid grid = CreateExteriorGrid(24);
        WildlifeEcosystemApplicationAdapter ecosystem =
            CreateEcosystemForTest(17);
        ecosystem.EnsureInitialized(grid);
        GameObject actorObject = new GameObject("WildlifeEcosystemTargetContract");
        try
        {
            WildlifeActor actor = actorObject.AddComponent<WildlifeActor>();
            ConfigureActor(actor, 61517);
            actor.Initialize(grid, WildlifeTestFixtures.RuneDeer, "eco-target", new Vector2Int(2, 0));
            actor.ChangeThirst(1f);
            bool selected = ecosystem.TryChooseEcologyTarget(
                actor,
                grid,
                new[] { actor },
                Array.Empty<WorldItemStackSnapshot>(),
                out Vector2Int target,
                out WildlifeIntent intent,
                out string reason);
            Require(selected, "thirsty wildlife did not choose ecology target");
            Require(intent == WildlifeIntent.Drink, $"expected drink intent, got {intent}");
            Require(target != actor.GridPosition, "drink target should move toward water patch");
            Require(!string.IsNullOrWhiteSpace(reason), "intent reason should be player-readable");
            return $"intent={intent}; target={target}; reason={reason}";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static string VerifyEcosystemRespawnPressure()
    {
        Grid grid = CreateExteriorGrid(28);
        WildlifeEcosystemApplicationAdapter ecosystem =
            CreateEcosystemForTest(29);
        ecosystem.EnsureInitialized(grid);
        float now = Time.time + 10f;
        bool first = ecosystem.TryConsumeRespawnOpportunity(
            now,
            aliveCount: 0,
            WildlifeTestFixtures.All,
            out WildlifeSpeciesDefinition selected);
        Require(first && selected != null, "ecosystem should allow slow refill when habitat is healthy");

        GameObject actorObject = new GameObject("WildlifeRespawnPressureContract");
        try
        {
            WildlifeActor actor = actorObject.AddComponent<WildlifeActor>();
            ConfigureActor(actor, 66117);
            actor.Initialize(grid, selected, "eco-pressure", new Vector2Int(1, 0));
            ecosystem.NotifyWildlifeKilled(actor, byHunt: true);
            bool immediate = ecosystem.TryConsumeRespawnOpportunity(
                Time.time + 11f,
                aliveCount: 0,
                WildlifeTestFixtures.All,
                out _);
            Require(!immediate, "hunt pressure should prevent immediate refill");
            return $"selected={selected.SpeciesId}; respawnBlocked={!immediate}";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static bool VerifyPlayModeRuntimeServices()
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        DungeonRuntimeLifetimeScope scope = FindScope();
        if (scope == null || scope.Container == null)
        {
            return false;
        }

        return scope.Container.Resolve<IWildlifeSpeciesCatalogProvider>() != null
            && scope.Container.Resolve<IWildlifeEcosystemRuntime>() != null
            && scope.Container.Resolve<IWildlifeRuntime>() != null
            && scope.Container.Resolve<ISurvivalFoodQuery>() != null;
    }

    private static WildlifeEcosystemApplicationAdapter CreateEcosystemForTest(
        int seed)
    {
        WildlifeEcosystemApplicationPorts ports =
            new WildlifeEcosystemApplicationPorts(
                gridSystemProvider: null,
                worldWaterQuery: null,
                habitatMarkerQuery: new WildlifeHabitatMarkerRegistry(
                    new WorldSimulationSceneReferences()),
                content: new ResourceGameContentCatalog(
                    new UnityGameContentRootLoader()),
                overlayRoot: new WildlifeOverlayRootPort());
        WildlifeEcosystemRuntime core = new WildlifeEcosystemRuntime(
            ports,
            ports,
            new DungeonStory.Foundation.UnityGameClock(),
            new DungeonStory.Foundation.RandomStreamProvider(seed),
            new GuidPersistentIdGenerator());
        return new WildlifeEcosystemApplicationAdapter(core, ports);
    }

    private static bool VerifyPlayModeHabitatDecorations()
    {
        if (!Application.isPlaying
            || !TryResolvePlayModeService(
                out WildlifeEcosystemApplicationAdapter ecosystem)
            || !ecosystem.DecorationRuntime.IsReady)
        {
            return false;
        }

        GameObject[] roots = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(gameObject => gameObject != null
                && gameObject.name == "WildlifeHabitatDecorations"
                && gameObject.scene.IsValid())
            .ToArray();
        if (roots.Length != 1
            || roots[0].transform.parent == null
            || roots[0].transform.parent.name != DungeonRuntimeHierarchy.Exterior)
        {
            return false;
        }

        SpriteRenderer[] renderers = roots[0].GetComponentsInChildren<SpriteRenderer>(true);
        return ecosystem.DecorationRuntime.FlowerPatchCount >= 2
            && ecosystem.DecorationRuntime.TreeCount > 0
            && ecosystem.DecorationRuntime.RockCount > 0
            && renderers.Length > 0
            && renderers.All(renderer => renderer.sortingLayerName == "OutsideObject");
    }

    private static bool VerifyPlayModeWildlifeSpawnArea()
    {
        if (!Application.isPlaying
            || !TryResolvePlayModeService(out WildlifeRuntime wildlifeRuntime))
        {
            return false;
        }

        wildlifeRuntime.Tick();
        GridSystemManager manager = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>();
        Grid grid = manager != null ? manager.grid : null;
        if (grid == null)
        {
            return false;
        }

        IReadOnlyList<WildlifeActor> animals = wildlifeRuntime.Wildlife;
        if (animals.Count == 0)
        {
            return false;
        }

        int exteriorSurfaceCells = grid.GetCells().Count(cell =>
            cell != null
            && cell.AreaType == GridCellAreaType.ExteriorPath
            && WildlifeRuntime.IsOutdoorSurfaceCell(grid, cell));
        if (exteriorSurfaceCells <= 12)
        {
            Debug.LogError($"Wildlife exterior grid is too narrow. cells={exteriorSurfaceCells}");
            return false;
        }

        return animals.All(actor =>
        {
            if (actor == null || !actor.IsAlive)
            {
                return true;
            }

            GridCell cell = grid.GetGridCell(actor.GridPosition);
            return cell != null
                && cell.AreaType != GridCellAreaType.BlockedExterior
                && (cell.AreaType != GridCellAreaType.ExteriorPath
                    || WildlifeRuntime.IsOutdoorSurfaceCell(grid, cell))
                && (actor.CanEnterDungeon || cell.AreaType != GridCellAreaType.DungeonInterior);
        });
    }

    private static bool VerifyPlayModeInfoPanel()
    {
        if (!Application.isPlaying
            || !TryResolvePlayModeService(out WildlifeRuntime wildlifeRuntime))
        {
            return false;
        }

        wildlifeRuntime.Tick();
        WildlifeActor actor = wildlifeRuntime.Wildlife.FirstOrDefault(item => item != null && item.IsAlive);
        if (actor == null)
        {
            return false;
        }

        WildlifeInfoPanel infoPanel = UnityEngine.Object.FindFirstObjectByType<WildlifeInfoPanel>();
        if (infoPanel == null)
        {
            return false;
        }

        FindScope().Container.Resolve<IGameEventBus>().ShowInfo(actor);
        bool firstClickOpened = infoPanel.IsShowingWildlife
            && infoPanel.CurrentWildlife == actor;
        FindScope().Container.Resolve<IGameEventBus>().ShowInfo(actor);
        return firstClickOpened
            && infoPanel.IsShowingWildlife
            && infoPanel.CurrentWildlife == actor;
    }

    private static bool VerifyPlayModeSaveCapture()
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        DungeonRuntimeLifetimeScope scope = FindScope();
        if (scope == null || scope.Container == null)
        {
            return false;
        }

        if (!EnsureLiveOwner(scope))
        {
            return false;
        }

        IDungeonGameSaveService saveService = scope.Container.Resolve<IDungeonGameSaveService>();
        DungeonGameSaveData save = saveService.Capture();
        return save.version == DungeonGameSaveData.CurrentVersion
            && DungeonSaveSectionPayload.TryRead(
                save,
                WildlifeSaveSection.Id,
                out DungeonWildlifeSaveData wildlife)
            && wildlife.ecosystem != null
            && DungeonSaveSectionPayload.TryRead(
                save,
                SurvivalResourcesSaveSection.Id,
                out DungeonSurvivalSaveData survival)
            && survival != null;
    }

    private static bool VerifyInvalidWildlifePreflightPreservesLiveActors()
    {
        if (!TryResolveSaveScenario(
                out WildlifeRuntime wildlifeRuntime,
                out IDungeonGameSaveService saveService,
                out DungeonRuntimeLifetimeScope scope))
        {
            return false;
        }

        WildlifeActor[] before = wildlifeRuntime.Wildlife
            .Where(actor => actor != null)
            .ToArray();
        if (before.Length == 0)
        {
            return false;
        }

        DungeonGameSaveData invalid = saveService.Capture();
        if (!DungeonSaveSectionPayload.TryRead(
                invalid,
                WildlifeSaveSection.Id,
                out DungeonWildlifeSaveData payload)
            || payload.wildlife.Count == 0)
        {
            return false;
        }

        payload.wildlife[0].speciesId = "wildlife:missing-preflight-species";
        DungeonSaveSectionPayload.Write(
            invalid,
            WildlifeSaveSection.Id,
            DungeonWildlifeSaveData.CurrentVersion,
            DungeonSaveRestorePhase.RuntimeState,
            payload);
        invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);

        bool restored = saveService.TryRestore(invalid, out DungeonGameRestoreReport report);
        WildlifeActor[] after = wildlifeRuntime.Wildlife
            .Where(actor => actor != null)
            .ToArray();
        return !restored
            && !report.Success
            && before.SequenceEqual(after)
            && after.All(actor => actor.gameObject.activeSelf
                && !actor.IsDetachedRestoreCandidate);
    }

    private static bool VerifyWildlifeRoundtripPublishesDetachedActors()
    {
        if (!TryResolveSaveScenario(
                out WildlifeRuntime wildlifeRuntime,
                out IDungeonGameSaveService saveService,
                out DungeonRuntimeLifetimeScope scope))
        {
            return false;
        }

        WildlifeActor[] before = wildlifeRuntime.Wildlife
            .Where(actor => actor != null && actor.IsAlive)
            .ToArray();
        if (before.Length == 0)
        {
            return false;
        }

        string[] expectedIds = before
            .Select(actor => actor.WildlifeId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        DungeonGameSaveData snapshot = saveService.Capture();
        if (!DungeonSaveSectionPayload.TryRead(
                snapshot,
                WildlifeSaveSection.Id,
                out DungeonWildlifeSaveData capturedWildlife))
        {
            return false;
        }

        string expectedPatches = CapturePatchJson(
            capturedWildlife.ecosystem.patches);
        if (!saveService.TryRestore(snapshot, out DungeonGameRestoreReport report)
            || !report.Success)
        {
            return false;
        }

        WildlifeActor[] after = wildlifeRuntime.Wildlife
            .Where(actor => actor != null && actor.IsAlive)
            .ToArray();
        string[] restoredIds = after
            .Select(actor => actor.WildlifeId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        string restoredPatches = CapturePatchJson(
            wildlifeRuntime.Capture().ecosystem.patches);
        IRestoreWorldCandidateQuery candidates =
            scope.Container.Resolve<IRestoreWorldCandidateQuery>();
        return expectedIds.SequenceEqual(restoredIds)
            && string.Equals(
                expectedPatches,
                restoredPatches,
                StringComparison.Ordinal)
            && before.All(previous => after.All(current =>
                !ReferenceEquals(previous, current)))
            && after.All(actor => actor.gameObject.activeSelf
                && !actor.IsDetachedRestoreCandidate)
            && !candidates.TryGetWildlife(out _)
            && report.Warnings.Count == 0;
    }

    private static bool VerifyLaterFailureDiscardsWildlifeCandidate()
    {
        if (!TryResolveSaveScenario(
                out WildlifeRuntime wildlifeRuntime,
                out _,
                out DungeonRuntimeLifetimeScope scope))
        {
            return false;
        }

        WildlifeActor[] before = wildlifeRuntime.Wildlife
            .Where(actor => actor != null && actor.IsAlive)
            .ToArray();
        string[] expectedIds = before
            .Select(actor => actor.WildlifeId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (expectedIds.Length == 0)
        {
            return false;
        }

        IDungeonSaveSectionRegistry liveRegistry =
            scope.Container.Resolve<IDungeonSaveSectionRegistry>();
        IDungeonSaveSection facilitySection = liveRegistry.OrderedSections
            .Single(section => section.SectionId == ModularFacilityWorldSaveSection.Id);
        IDungeonSaveSection wildlifeSection = liveRegistry.OrderedSections
            .Single(section => section.SectionId == WildlifeSaveSection.Id);
        MarkerDependencySection runDependency =
            new MarkerDependencySection(RunVariableSaveSection.Id);
        MarkerDependencySection sessionDependency =
            new MarkerDependencySection(FoundationSessionSaveSection.Id);
        MarkerDependencySection metaDependency =
            new MarkerDependencySection(MetaProgressionSaveSection.Id);
        MarkerDependencySection itemDependency =
            new MarkerDependencySection(PhysicalItemsSaveSection.Id);
        FailOnceAfterWildlifeSaveSection failSection =
            new FailOnceAfterWildlifeSaveSection();
        DungeonRuntimeAggregateRootStore isolatedRoot =
            new DungeonRuntimeAggregateRootStore();
        IDungeonRestoreTransactionParticipant facilityParticipant =
            scope.Container.Resolve<IModularFacilityWorldSaveService>()
                as IDungeonRestoreTransactionParticipant;
        if (facilityParticipant == null)
        {
            return false;
        }

        DungeonSaveSectionRegistry testRegistry = new DungeonSaveSectionRegistry(
            new IDungeonSaveSection[]
            {
                sessionDependency,
                runDependency,
                metaDependency,
                itemDependency,
                facilitySection,
                wildlifeSection,
                failSection
            },
            isolatedRoot,
            new IDungeonRestoreTransactionParticipant[]
            {
                facilityParticipant,
                wildlifeRuntime
            });

        List<DungeonSaveSectionEnvelope> snapshot = testRegistry.CaptureAll();
        IRestoreWorldCandidateQuery candidateIndex =
            scope.Container.Resolve<IRestoreWorldCandidateQuery>();
        IGridSystemProvider gridProvider =
            scope.Container.Resolve<IGridSystemProvider>();
        if (!gridProvider.TryGetGrid(out Grid liveGrid))
        {
            return false;
        }

        int detachedBefore = CountDetachedWildlifeCandidates();
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        bool restored = testRegistry.RestoreAll(snapshot, report);
        WildlifeActor[] after = wildlifeRuntime.Wildlife
            .Where(actor => actor != null && actor.IsAlive)
            .ToArray();
        string[] restoredIds = after
            .Select(actor => actor.WildlifeId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        bool sameGrid = gridProvider.TryGetGrid(out Grid afterGrid)
            && ReferenceEquals(liveGrid, afterGrid);
        bool candidateIndexClear = !candidateIndex.TryGetGrid(out _)
            && !candidateIndex.TryGetBuildings(out _)
            && !candidateIndex.TryGetWildlife(out _);
        int detachedAfter = CountDetachedWildlifeCandidates();

        return !restored
            && !report.Success
            && report.Errors.Any(error => error.Contains(
                "Intentional failure after wildlife candidate staging.",
                StringComparison.Ordinal))
            && failSection.CommitCount == 1
            && expectedIds.SequenceEqual(restoredIds)
            && sameGrid
            && candidateIndexClear
            && !isolatedRoot.IsRestoreStaging
            && isolatedRoot.PublishedRestoreRevision == 0
            && detachedAfter == detachedBefore
            && after.All(actor => actor.gameObject.activeSelf
                && !actor.IsDetachedRestoreCandidate);
    }

    private static string CapturePatchJson(
        IEnumerable<WildlifeHabitatPatchSaveData> patches)
    {
        return string.Join(
            "\n",
            (patches ?? Enumerable.Empty<WildlifeHabitatPatchSaveData>())
                .Select(JsonUtility.ToJson));
    }

    private static int CountDetachedWildlifeCandidates()
    {
        return Resources.FindObjectsOfTypeAll<WildlifeActor>()
            .Count(actor => actor != null && actor.IsDetachedRestoreCandidate);
    }

    private static bool TryResolveSaveScenario(
        out WildlifeRuntime wildlifeRuntime,
        out IDungeonGameSaveService saveService,
        out DungeonRuntimeLifetimeScope scope)
    {
        wildlifeRuntime = null;
        saveService = null;
        scope = null;
        if (!Application.isPlaying)
        {
            return false;
        }

        scope = FindScope();
        if (scope == null || scope.Container == null)
        {
            return false;
        }

        if (!EnsureLiveOwner(scope))
        {
            return false;
        }

        wildlifeRuntime = scope.Container.Resolve<WildlifeRuntime>();
        saveService = scope.Container.Resolve<IDungeonGameSaveService>();
        wildlifeRuntime.Tick();
        return wildlifeRuntime.Wildlife.Any(actor =>
            actor != null && actor.IsAlive);
    }

    private static Grid CreateExteriorGrid(int width)
    {
        Grid grid = new Grid(width, 1);
        for (int x = 0; x < grid.width; x++)
        {
            grid.GetGridCell(new Vector2Int(x, 0)).SetAreaType(GridCellAreaType.ExteriorPath);
        }

        return grid;
    }

    private static IGridPathSearchBroker CreatePathSearchBroker()
    {
        GridPathSearchBroker broker = new GridPathSearchBroker(new UnityGameClock(), doorAccessQuery: null, performanceRecorder: null, costPolicy: null);
        broker.BeginFrame(64, enforceBudget: false);
        return broker;
    }

    private static bool VerifyPlayModeHuntCarcassButcherLoop(out string report)
    {
        report = string.Empty;
        if (!Application.isPlaying
            || !TryResolvePlayModeService(out WildlifeRuntime wildlifeRuntime)
            || !TryResolvePlayModeService(out IWorldItemStackRuntime itemRuntime))
        {
            report = "required runtimes missing";
            return false;
        }

        wildlifeRuntime.Tick();
        WildlifeActor target = wildlifeRuntime.Wildlife
            .FirstOrDefault(actor => actor != null && actor.IsAlive);
        CharacterActor hunter = UnityEngine.Object
            .FindObjectsByType<CharacterActor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(actor => actor != null
                && !actor.IsDead
                && CharacterWorkRoleUtility.TryGetWork(actor, out _));
        GameObject temporaryHunterObject = null;
        if (hunter == null
            && target != null
            && TryCreateTemporaryHunter(target.GridPosition, out CharacterActor temporaryHunter, out temporaryHunterObject))
        {
            hunter = temporaryHunter;
        }

        if (hunter == null || target == null)
        {
            report = $"hunter={hunter != null}; target={target != null}";
            if (temporaryHunterObject != null)
            {
                UnityEngine.Object.DestroyImmediate(temporaryHunterObject);
            }

            return false;
        }

        try
        {
            int stacksBefore = itemRuntime.GetAllStacks().Count;
            int foodBefore = CountFoodStacks(itemRuntime);
            wildlifeRuntime.DesignateHunt(target.WildlifeId, true, priority: true);
            int hits = 0;
            while (target != null && target.IsAlive && hits < 30)
            {
                wildlifeRuntime.ApplyHuntHit(hunter, target.WildlifeId, out _);
                hits++;
            }

            int carcassesAfterHunt = CountCarcassStacks(itemRuntime);
            bool killed = target == null || !target.IsAlive;
            bool butchered = wildlifeRuntime.TryButcherNextCarcass(
                hunter,
                building: null,
                out int produced,
                out string butcherMessage);
            int foodAfter = CountFoodStacks(itemRuntime);
            int stacksAfter = itemRuntime.GetAllStacks().Count;

            report =
                $"hunter={hunter.name}; target={target?.SpeciesId ?? "null"}; hits={hits}; "
                + $"killed={killed}; carcasses={carcassesAfterHunt}; butchered={butchered}; "
                + $"produced={produced}; food={foodBefore}->{foodAfter}; stacks={stacksBefore}->{stacksAfter}; "
                + $"message={butcherMessage}";
            return killed
                && carcassesAfterHunt > 0
                && butchered
                && produced > 0
                && foodAfter > foodBefore;
        }
        finally
        {
            if (temporaryHunterObject != null)
            {
                UnityEngine.Object.DestroyImmediate(temporaryHunterObject);
            }
        }
    }

    private static bool TryCreateTemporaryHunter(
        Vector2Int nearPosition,
        out CharacterActor hunter,
        out GameObject hunterObject)
    {
        hunter = null;
        hunterObject = null;
        GridSystemManager manager = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>();
        Grid grid = manager != null ? manager.grid : null;
        if (grid == null)
        {
            return false;
        }

        Vector2Int spawnPosition = nearPosition + Vector2Int.left;
        if (!grid.IsValidGridPos(spawnPosition) || !grid.IsWalkable(spawnPosition))
        {
            spawnPosition = nearPosition + Vector2Int.right;
        }

        if (!grid.IsValidGridPos(spawnPosition) || !grid.IsWalkable(spawnPosition))
        {
            return false;
        }

        CharacterSO data = AssetDatabase.LoadAssetAtPath<CharacterSO>(
            "Assets/Resources/SO/Character/Owners/Owner_Slime.asset");
        if (data == null)
        {
            return false;
        }

        hunterObject = new GameObject("WildlifeTemporaryHunter");
        hunterObject.AddComponent<SpriteRenderer>();
        hunterObject.AddComponent<CharacterActor>();
        hunterObject.AddComponent<AbilityMove>();
        hunterObject.AddComponent<AbilityWork>();
        hunterObject.AddComponent<AIBrain>();
        hunterObject.transform.position = grid.GetWorldPos(spawnPosition);

        CharacterAiEditorTestDependencies.Inject(hunterObject);
        hunter = hunterObject.GetComponent<CharacterActor>();
        typeof(CharacterActor)
            .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(hunter, null);
        hunter.RefreshAbilityCache();
        hunter.Progression.ApplyPreparedIdentity(
            data.characterName,
            "debug:wildlife-hunt",
            data.traits.Select(trait => trait.id),
            CharacterPotentialGrade.Ordinary,
            generationSeed: data.id,
            autoChooseDrafts: false);
        hunter.Initialization(data);
        hunter.SetLifecycleState(CharacterLifecycleState.Active);
        return hunter != null;
    }

    private static bool EnsureLiveOwner(DungeonRuntimeLifetimeScope scope)
    {
        CharacterActor[] owners = UnityEngine.Object
            .FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .Where(actor => actor != null
                && actor.IsOwner
                && !actor.IsDead
                && actor.CurrentLifecycleState == CharacterLifecycleState.Active)
            .Distinct()
            .ToArray();
        if (owners.Length == 1)
        {
            return true;
        }
        if (owners.Length > 1)
        {
            return false;
        }

        OwnerRunManager manager = UnityEngine.Object
            .FindFirstObjectByType<OwnerRunManager>(FindObjectsInactive.Include);
        CharacterSO definition = manager?.GetDefaultOwner();
        if (manager == null || definition == null)
        {
            return false;
        }

        manager.SelectOwner(definition);
        CharacterActor owner = manager.CurrentOwnerActor;
        return owner != null
            && owner.IsOwner
            && !owner.IsDead
            && owner.CurrentLifecycleState == CharacterLifecycleState.Active;
    }

    private static int CountCarcassStacks(IWorldItemStackRuntime itemRuntime)
    {
        return itemRuntime == null
            ? 0
            : itemRuntime.GetAllStacks().Count(stack => stack != null
                && WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(stack.ItemId, out _));
    }

    private static int CountFoodStacks(IWorldItemStackRuntime itemRuntime)
    {
        IDungeonItemCatalogProvider catalog = itemRuntime?.CatalogProvider;
        return itemRuntime == null || catalog == null
            ? 0
            : itemRuntime.GetAllStacks()
                .Where(stack => stack != null
                    && catalog.TryGetDefinition(
                        stack.ItemId,
                        out DungeonItemDefinition definition)
                    && definition.StockCategory == StockCategory.Food)
                .Sum(stack => stack.Quantity);
    }

    private static DungeonRuntimeLifetimeScope FindScope()
    {
        return UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
            FindObjectsInactive.Include);
    }

    private static bool TryResolvePlayModeService<T>(out T service)
    {
        service = default;
        DungeonRuntimeLifetimeScope scope = FindScope();
        if (scope == null || scope.Container == null)
        {
            return false;
        }

        service = scope.Container.Resolve<T>();
        return service != null;
    }

    private static void Run(
        string name,
        Func<string> scenario,
        List<string> lines,
        List<string> errors)
    {
        try
        {
            string details = scenario();
            lines.Add($"{name}\tPASS\t{details}");
        }
        catch (Exception exception)
        {
            string message = $"{name}: {exception.Message}";
            errors.Add(message);
            lines.Add($"{name}\tFAIL\t{exception}");
        }
    }

    private static void ConfigureActor(WildlifeActor actor, int seed)
    {
        actor.ConfigureRuntimeServices(
            CreatePathSearchBroker(),
            CharacterAiEditorTestDependencies.WorldRegistry,
            new UnityGameClock(),
            new RandomStreamProvider(seed),
            OpenDoorAccessQuery.Instance);
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        bool passed;
        try
        {
            passed = scenario();
        }
        catch (Exception exception)
        {
            errors.Add($"{name}: {exception}");
            return;
        }

        if (!passed)
        {
            errors.Add(name);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class MarkerDependencySection :
        DungeonDebugStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private readonly string id;

        public MarkerDependencySection(string id)
        {
            this.id = id ?? throw new ArgumentNullException(nameof(id));
        }

        public override string SectionId => id;
        public override DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.Foundation;

        protected override void CommitMarker(
            DungeonGameRestoreReport report)
        {
        }
    }

    private sealed class FailOnceAfterWildlifeSaveSection :
        IDungeonSaveSection,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        public const string Id = "wildlife.debug.fail-after-candidate";

        public int CommitCount { get; private set; }
        public string SectionId => Id;
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.LateRuntimeState;
        public IReadOnlyList<string> DependsOn =>
            new[] { WildlifeSaveSection.Id };

        public string Capture() => "{}";

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion)
            {
                throw new InvalidOperationException(
                    $"Unexpected debug section version {sectionVersion}.");
            }

            return new DungeonDelegateSaveRestoreStage(
                SectionId,
                _ =>
                {
                    CommitCount++;
                    if (CommitCount == 1)
                    {
                        throw new InvalidOperationException(
                            "Intentional failure after wildlife candidate staging.");
                    }
                });
        }
    }

    private sealed class TestOccupant : IGridOccupant
    {
        private static int nextId;

        public int GridId { get; } = ++nextId;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => true;
        public bool IsGridMovement => true;
    }

    private sealed class OpenDoorAccessQuery : IDoorAccessQuery
    {
        public static readonly OpenDoorAccessQuery Instance = new OpenDoorAccessQuery();

        public int DoorAccessVersion => 0;

        public DoorAccessSubjectRef ResolveSubject(GridTraversalContext context)
        {
            if (context.SubjectKind == GridTraversalSubjectKind.Wildlife
                && !string.IsNullOrWhiteSpace(context.WildlifeId))
            {
                return new DoorAccessSubjectRef(
                    context.WildlifeId,
                    DoorAccessGroup.Wildlife);
            }

            return default;
        }

        public bool CanUse(
            Door door,
            GridTraversalContext context,
            out string denialReason)
        {
            denialReason = string.Empty;
            return true;
        }

        public bool CanTraverse(
            Grid grid,
            Vector2Int position,
            GridTraversalContext context,
            out string denialReason)
        {
            denialReason = string.Empty;
            return true;
        }
    }
}
#endif
