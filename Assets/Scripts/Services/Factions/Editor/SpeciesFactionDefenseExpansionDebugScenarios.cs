#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;
using DungeonStory.Infrastructure;
using UnityEditor;
using UnityEngine;

public static class SpeciesFactionDefenseExpansionDebugScenarios
{
    private static readonly string[] NewSpecies =
    {
        "Beastkin",
        "Demon",
        "Kobold",
        "Myconid",
        "Harpy",
        "Golem"
    };

    private static readonly string[] RequiredAnatomies =
    {
        "anatomy:human",
        "anatomy:humanoid",
        "anatomy:orc",
        "anatomy:vampire",
        "anatomy:beastkin",
        "anatomy:demon",
        "anatomy:kobold",
        "anatomy:quadruped",
        "anatomy:slime",
        "anatomy:fungal",
        "anatomy:avian",
        "anatomy:construct"
    };

    private static readonly int[] NewDefenseBuildingIds =
    {
        1800, 1801, 1802, 1803, 1804, 1805
    };

    private static readonly string[] NewResearchIds =
    {
        "research:defense:supply",
        "research:defense:corridor-mechanisms",
        "research:defense:rune-identification",
        "research:defense:remote-control",
        "research:defense:siege-fortification",
        "research:defense:alliance-signals"
    };

    [MenuItem(
        "DungeonStory/Debug/Expansion/Build And Validate Species Factions Defense")]
    public static void BuildAndValidate()
    {
        CharacterSpeciesExpansionAssetBuilder.BuildAll();
        DungeonFactionAssetBuilder.BuildAll();
        SurgeryContentAssetBuilder.RebuildAll();
        P1DefenseFacilityAssetBuilder.EnsureP1DefenseAssets();
        ResearchProjectAssetBuilder.Rebuild();
        ValidateOnly();
    }

    [MenuItem(
        "DungeonStory/Debug/Expansion/Validate Species Factions Defense")]
    public static void ValidateOnly()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        List<string> errors = new List<string>();
        ValidateSpecies(errors);
        ValidateAnatomy(errors);
        ValidateFactions(errors);
        ValidateFactionSaveBoundary(errors);
        ValidateHumanBranches(errors);
        ValidateDefense(errors);
        ValidateResearch(errors);

        if (errors.Count > 0)
        {
            string report = string.Join(Environment.NewLine, errors);
            Debug.LogError(
                "Species/faction/defense expansion validation failed:"
                + Environment.NewLine
                + report);
            throw new InvalidOperationException(report);
        }

        Debug.Log(
            "Species/faction/defense expansion validation passed: "
            + "9 population species plus Adventurer, 12 anatomy profiles, 6 dungeon factions, "
            + "5 human branches, "
            + "19 defense facilities, 180 research projects.");
    }

    private static void ValidateSpecies(ICollection<string> errors)
    {
        CharacterSpeciesSO[] species = FindAssets<CharacterSpeciesSO>(
            "Assets/Resources/SO/Character/Species");
        if (species.Length != 10)
        {
            errors.Add($"Expected 10 species assets including Adventurer, found {species.Length}.");
        }

        Dictionary<string, CharacterSpeciesSO> byTag = species
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.speciesTag))
            .GroupBy(value => value.speciesTag, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);
        if (byTag.Count != 10)
        {
            errors.Add($"Expected 10 unique species including Adventurer, found {byTag.Count}.");
        }

        int selectable = byTag.Values.Count(value => value.ownerSelectable);
        if (selectable != 3)
        {
            errors.Add($"Expected 3 owner-selectable species, found {selectable}.");
        }

        foreach (string tag in NewSpecies)
        {
            if (!byTag.TryGetValue(tag, out CharacterSpeciesSO value))
            {
                errors.Add($"Missing species '{tag}'.");
                continue;
            }

            if (value.ownerSelectable)
                errors.Add($"{tag} must remain NPC-only in phase one.");
            if (string.IsNullOrWhiteSpace(value.homeFactionId))
                errors.Add($"{tag} has no home faction.");
            if (string.IsNullOrWhiteSpace(value.anatomyProfileId))
                errors.Add($"{tag} has no anatomy profile.");
            if (string.IsNullOrWhiteSpace(value.IncidentId))
                errors.Add($"{tag} has no stable incident ID.");
            if (value.preferredFacilityLabels == null
                || value.preferredFacilityLabels.Length < 3)
                errors.Add($"{tag} needs three preferred facilities.");
            if (value.dislikedEnvironmentLabels == null
                || value.dislikedEnvironmentLabels.Length < 3)
                errors.Add($"{tag} needs three disliked environments.");
            if (value.strongWorkTypeIds == null
                || value.strongWorkTypeIds.Length < 2)
                errors.Add($"{tag} needs at least two work strengths.");
            if (value.defenseAffinityTags == null
                || value.defenseAffinityTags.Length == 0)
                errors.Add($"{tag} has no defense affinity.");
        }
    }

    private static void ValidateAnatomy(ICollection<string> errors)
    {
        AnatomyProfileSO[] profiles = FindAssets<AnatomyProfileSO>(
            "Assets/Resources/SO/Medical/Anatomy");
        if (profiles.Length != 12)
        {
            errors.Add(
                $"Expected 12 anatomy profile assets, found {profiles.Length}.");
        }

        HashSet<string> ids = profiles
            .Where(value => value != null)
            .Select(value => value.ProfileId)
            .ToHashSet(StringComparer.Ordinal);
        if (ids.Count != RequiredAnatomies.Length)
        {
            errors.Add(
                $"Expected {RequiredAnatomies.Length} unique anatomy IDs, "
                + $"found {ids.Count}.");
        }

        foreach (string required in RequiredAnatomies)
        {
            if (!ids.Contains(required))
            {
                errors.Add($"Missing anatomy profile '{required}'.");
            }
        }

        CharacterSpeciesSO[] species = FindAssets<CharacterSpeciesSO>(
            "Assets/Resources/SO/Character/Species");
        foreach (CharacterSpeciesSO value in species.Where(value => value != null))
        {
            if (!ids.Contains(value.anatomyProfileId))
            {
                errors.Add(
                    $"Species '{value.speciesTag}' references missing anatomy "
                    + $"profile '{value.anatomyProfileId}'.");
            }
        }
    }

    private static void ValidateFactions(ICollection<string> errors)
    {
        DungeonFactionDefinitionSO[] factions =
            FindAssets<DungeonFactionDefinitionSO>(
                "Assets/Resources/SO/Factions/Dungeons");
        if (factions.Length != 6)
        {
            errors.Add($"Expected 6 faction assets, found {factions.Length}.");
        }

        Dictionary<string, DungeonFactionDefinitionSO> byId = factions
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.StableId))
            .GroupBy(value => value.StableId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First());
        if (byId.Count != 6)
        {
            errors.Add($"Expected 6 dungeon factions, found {byId.Count}.");
        }

        foreach (string factionId in DungeonFactionIds.All)
        {
            if (!byId.TryGetValue(
                    factionId,
                    out DungeonFactionDefinitionSO faction))
            {
                errors.Add($"Missing dungeon faction '{factionId}'.");
                continue;
            }

            if (faction.crest == null)
                errors.Add($"{factionId} has no crest.");
            if (faction.tradeCargo == null || faction.tradeCargo.Count == 0)
                errors.Add($"{factionId} has no physical trade cargo.");
            if (faction.supplyCargo == null || faction.supplyCargo.Count == 0)
                errors.Add($"{factionId} has no physical supply cargo.");
        }
    }

    private static void ValidateFactionSaveBoundary(ICollection<string> errors)
    {
        DungeonFactionDefinitionSO[] definitions =
            FindAssets<DungeonFactionDefinitionSO>(
                    "Assets/Resources/SO/Factions/Dungeons")
                .OrderBy(value => value.StableId, StringComparer.Ordinal)
                .ToArray();
        DungeonFactionSaveData valid = new DungeonFactionSaveData
        {
            currentDay = 3,
            routeSequence = 1,
            factions = definitions
                .Select(value => new DungeonFactionState
                {
                    factionId = value.StableId,
                    homeQ = 4,
                    homeR = -2
                })
                .ToList(),
            routes = new List<FactionRouteState>
            {
                new FactionRouteState
                {
                    routeId = "faction-route:1",
                    factionId = definitions.FirstOrDefault()?.StableId
                        ?? string.Empty,
                    kind = FactionRouteKind.TradeCaravan,
                    status = FactionRouteStatus.Traveling,
                    path = new List<FactionHexCoordSaveData>
                    {
                        new FactionHexCoordSaveData { q = 4, r = -2 },
                        new FactionHexCoordSaveData { q = 3, r = -1 }
                    },
                    pathIndex = 0,
                    segmentProgress = 0.25f,
                    delaySeconds = 0f,
                    strength = 100,
                    createdDay = 1,
                    estimatedArrivalDay = 2,
                    actorsSpawned = true,
                    reinforcementActorIds = new List<string>
                    {
                        CharacterId.FromStableSuffix(
                            "faction-route:1:ally:1").Value
                    },
                    cargo = new List<FactionCargoLine>
                    {
                        new FactionCargoLine
                        {
                            itemId = "material:iron-ingot",
                            amount = 2
                        }
                    }
                }
            }
        };
        StrictFactionSaveRuntime runtime =
            new StrictFactionSaveRuntime(definitions, valid);
        FactionSaveSection section = new FactionSaveSection(
            runtime,
            EditorItemCatalogFactory.Create());
        string canonicalJson = JsonUtility.ToJson(valid);
        if (canonicalJson.Contains("\"trust\"", StringComparison.Ordinal))
        {
            errors.Add(
                "Faction save still serializes legacy trust instead of using campaign rapport/grievance/obligation authority.");
            return;
        }
        DungeonGameRestoreReport validReport = new DungeonGameRestoreReport();
        section.Restore(
            canonicalJson,
            DungeonFactionSaveData.CurrentVersion,
            validReport);
        object sectionContract = section;
        if (!validReport.Success
            || runtime.RestoreCount != 1
            || !string.Equals(section.Capture(), canonicalJson, StringComparison.Ordinal)
            || sectionContract is not IDungeonSaveSectionPreflight
            || sectionContract is not IDungeonStagedSaveSection
            || sectionContract is not IDungeonRollbackFreeSaveSection
            || sectionContract is IOptionalDungeonSaveSection
            || sectionContract is IDungeonStagedOptionalSaveSection)
        {
            errors.Add("Faction strict save boundary rejected a canonical payload.");
            return;
        }

        ValidateFactionReinforcementIdCompatibility(
            errors,
            runtime.Definitions,
            valid);

        DungeonFactionSaveData invalid = JsonUtility.FromJson<DungeonFactionSaveData>(
            canonicalJson);
        invalid.factions.Reverse();
        invalid.routes[0].cargo[0].itemId = "item:missing-faction-cargo";
        string beforeInvalid = section.Capture();
        if (!RejectsStrictWithoutMutation(
                section,
                JsonUtility.ToJson(invalid),
                section.SectionVersion,
                beforeInvalid)
            || !RejectsStrictWithoutMutation(
                section,
                canonicalJson,
                section.SectionVersion - 1,
                beforeInvalid)
            || !RejectsStrictWithoutMutation(
                section,
                string.Empty,
                section.SectionVersion,
                beforeInvalid))
        {
            errors.Add(
                "Faction strict save boundary accepted invalid, legacy, or empty state.");
        }
        DungeonFactionSaveData legacy =
            JsonUtility.FromJson<DungeonFactionSaveData>(canonicalJson);
        legacy.version--;
        if (!RejectsStrictWithoutMutation(
                section,
                JsonUtility.ToJson(legacy),
                section.SectionVersion,
                beforeInvalid)
            || runtime.RestoreCount != 1)
        {
            errors.Add(
                "Faction strict save boundary accepted a legacy payload version.");
        }

        ValidateFactionLateFailureDiscard(
            errors,
            definitions,
            valid,
            canonicalJson);
    }

    private static void ValidateFactionReinforcementIdCompatibility(
        ICollection<string> errors,
        IReadOnlyList<FactionDefinitionSnapshot> definitions,
        DungeonFactionSaveData canonical)
    {
        DungeonFactionSaveData legacy =
            JsonUtility.FromJson<DungeonFactionSaveData>(
                JsonUtility.ToJson(canonical));
        legacy.routes[0].reinforcementActorIds[0] =
            "faction-route:1:ally:1";
        string legacyBefore = JsonUtility.ToJson(legacy);
        IReadOnlyList<string> validationErrors =
            FactionPayloadValidation.Validate(
                legacy,
                definitions,
                itemId => string.Equals(
                    itemId,
                    "material:iron-ingot",
                    StringComparison.Ordinal));
        IReadOnlyList<string> restoredIds =
            FactionPayloadValidation
                .CanonicalizeReinforcementActorIdsForRestore(
                    legacy.routes[0]);
        string expected = CharacterId.FromStableSuffix(
            "faction-route:1:ally:1").Value;
        if (validationErrors.Count != 0
            || restoredIds.Count != 1
            || !string.Equals(restoredIds[0], expected,
                StringComparison.Ordinal)
            || !string.Equals(
                JsonUtility.ToJson(legacy),
                legacyBefore,
                StringComparison.Ordinal))
        {
            errors.Add(
                "Faction early-V18 reinforcement ID was rejected, not canonicalized in staging, or mutated at source.");
        }

        DungeonFactionSaveData malformed =
            JsonUtility.FromJson<DungeonFactionSaveData>(legacyBefore);
        malformed.routes[0].reinforcementActorIds[0] =
            "faction-route:1:ally:01";
        if (FactionPayloadValidation.Validate(
                malformed,
                definitions,
                itemId => string.Equals(
                    itemId,
                    "material:iron-ingot",
                    StringComparison.Ordinal)).Count == 0)
        {
            errors.Add(
                "Faction reinforcement compatibility accepted a non-exact legacy actor ID.");
        }
    }

    private static void ValidateFactionLateFailureDiscard(
        ICollection<string> errors,
        IReadOnlyList<DungeonFactionDefinitionSO> definitions,
        DungeonFactionSaveData valid,
        string canonicalJson)
    {
        DungeonRuntimeAggregateRootStore store = new();
        StrictFactionSaveRuntime runtime = new(definitions, valid, store);
        FactionSaveSection section = new(
            runtime,
            EditorItemCatalogFactory.Create());
        RequiredDependencyStubSection offense = new(
            OffenseAggregateSaveSection.Id,
            DungeonSaveRestorePhase.LateRuntimeState);
        RequiredDependencyStubSection items = new(
            PhysicalItemsSaveSection.Id,
            DungeonSaveRestorePhase.Items);
        FinalFailingSection finalFailure = new(section.SectionId);
        IDungeonSaveSection[] sections =
        {
            items,
            offense,
            section,
            finalFailure
        };
        DungeonSaveSectionRegistry registry = new(sections, store);
        string before = section.Capture();
        int revisionBefore = store.PublishedRestoreRevision;
        DungeonFactionSaveData incoming =
            JsonUtility.FromJson<DungeonFactionSaveData>(canonicalJson);
        incoming.factions[0].betrayalScars = 1;
        List<DungeonSaveSectionEnvelope> envelopes = sections
            .Select(candidate => new DungeonSaveSectionEnvelope
            {
                sectionId = candidate.SectionId,
                sectionVersion = candidate.SectionVersion,
                restorePhase = candidate.RestorePhase,
                optional = false,
                payloadJson = string.Equals(
                    candidate.SectionId,
                    section.SectionId,
                    StringComparison.Ordinal)
                    ? JsonUtility.ToJson(incoming)
                    : candidate.Capture()
            })
            .ToList();
        DungeonGameRestoreReport report = new();
        bool restored = registry.RestoreAll(envelopes, report);
        if (restored
            || report.Success
            || !finalFailure.WasCommitted
            || store.PublishedRestoreRevision != revisionBefore
            || !string.Equals(section.Capture(), before, StringComparison.Ordinal))
        {
            errors.Add(
                "Faction staged Aggregate leaked after a registry late failure.");
        }
    }

    private static bool RejectsStrictWithoutMutation(
        IDungeonSaveSection section,
        string payloadJson,
        int sectionVersion,
        string before)
    {
        try
        {
            ((IDungeonStagedSaveSection)section).StageRestore(
                payloadJson,
                sectionVersion,
                new DungeonGameRestoreReport());
            return false;
        }
        catch (InvalidOperationException)
        {
            return string.Equals(
                section.Capture(),
                before,
                StringComparison.Ordinal);
        }
    }

    private static void ValidateHumanBranches(ICollection<string> errors)
    {
        string[] branchIds =
        {
            HumanInvasionBranchIds.RoyalArmy,
            HumanInvasionBranchIds.PioneerSupply,
            HumanInvasionBranchIds.RoyalOrdnance,
            HumanInvasionBranchIds.IntelligenceHunters,
            HumanInvasionBranchIds.RadiantOrder
        };
        if (branchIds.Distinct(StringComparer.Ordinal).Count() != 5)
        {
            errors.Add("Human invasion branch IDs are not five unique values.");
        }
    }

    private static void ValidateDefense(ICollection<string> errors)
    {
        BuildingSO[] defenses = FindAssets<BuildingSO>(
                "Assets/Resources/SO/Building")
            .Where(value => value != null
                && value.Defense != null
                && value.Defense.IsDefenseFacility)
            .ToArray();
        if (defenses.Length != 19)
        {
            errors.Add(
                $"Expected 19 active defense facilities, found {defenses.Length}.");
        }
        int uniqueDefenseIds = defenses
            .Select(value => value.id)
            .Distinct()
            .Count();
        if (uniqueDefenseIds != defenses.Length)
        {
            errors.Add(
                $"Expected unique defense building IDs, found "
                + $"{uniqueDefenseIds} IDs across {defenses.Length} assets.");
        }

        foreach (int id in NewDefenseBuildingIds)
        {
            BuildingSO building = defenses.FirstOrDefault(value => value.id == id);
            if (building == null)
            {
                errors.Add($"Missing new defense building ID {id}.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(building.Defense.facilityFamilyId))
                errors.Add($"Defense building {id} has no facility family.");
            if (building.Defense.affinityTags == null
                || building.Defense.affinityTags.Length == 0)
                errors.Add($"Defense building {id} has no species affinity.");
        }

        foreach (BuildingSO building in defenses)
        {
            DefenseFacilityData defense = building.Defense;
            if (building.sprite == null)
                errors.Add($"Defense building {building.id} has no sprite.");
            if (defense.growth == null)
                errors.Add($"Defense building {building.id} has no growth state.");
            if (defense.conditionLossPerActivation < 0f)
                errors.Add($"Defense building {building.id} has invalid wear.");
        }

        int distinctSpritePaths = defenses
            .Select(value => AssetDatabase.GetAssetPath(value.sprite))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (distinctSpritePaths != defenses.Length)
        {
            errors.Add(
                $"Expected 19 distinct defense silhouettes, found "
                + $"{distinctSpritePaths} sprite assets.");
        }
    }

    private static void ValidateResearch(ICollection<string> errors)
    {
        ResearchProjectSO[] projects = FindAssets<ResearchProjectSO>(
            "Assets/Resources/SO/Research/Projects");
        if (projects.Length != 180)
        {
            errors.Add(
                $"Expected 180 research assets, found {projects.Length}.");
        }

        Dictionary<string, ResearchProjectSO> byId = projects
            .Where(value => value != null && value.ProjectId.IsValid)
            .GroupBy(value => value.ProjectId.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First());
        if (byId.Count != 180)
        {
            errors.Add($"Expected 180 research projects, found {byId.Count}.");
        }

        foreach (string id in NewResearchIds)
        {
            if (!byId.ContainsKey(id))
                errors.Add($"Missing defense research '{id}'.");
        }
    }

    private sealed class StrictFactionSaveRuntime : IFactionRuntime
    {
        private readonly DungeonRuntimeAggregateRootStore store;
        private DungeonFactionSaveData localData;

        public StrictFactionSaveRuntime(
            IReadOnlyList<DungeonFactionDefinitionSO> definitions,
            DungeonFactionSaveData data,
            DungeonRuntimeAggregateRootStore store = null)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));
            Definitions = definitions
                .Where(value => value != null)
                .Select(value => value.ToSnapshot())
                .ToArray();
            this.store = store;
            Data = Clone(data ?? throw new ArgumentNullException(nameof(data)));
        }

        private DungeonFactionSaveData Data
        {
            get => store != null
                ? store.GetOrCreate(() => new DungeonFactionSaveData())
                : localData;
            set
            {
                if (store != null)
                {
                    store.Replace(value);
                }
                else
                {
                    localData = value;
                }
            }
        }

        public IReadOnlyList<FactionDefinitionSnapshot> Definitions { get; }
        public IReadOnlyList<DungeonFactionState> Factions => Data.factions;
        public IReadOnlyList<FactionRouteState> Routes => Data.routes;
        public int RestoreCount { get; private set; }

        public bool TryGetFaction(
            string factionId,
            out DungeonFactionState faction)
        {
            faction = Data.factions.FirstOrDefault(value =>
                string.Equals(value.factionId, factionId, StringComparison.Ordinal));
            return faction != null;
        }

        public bool IsContractUnlocked(
            string factionId,
            FactionContractKind contract) => false;

        public bool TryAdjustTrust(
            string factionId,
            int amount,
            string reason,
            out string message)
        {
            message = string.Empty;
            return false;
        }

        public bool TryOfferGoodwill(
            string factionId,
            int physicalValue,
            out string message)
        {
            message = string.Empty;
            return false;
        }

        public bool TryCompleteAllianceProject(
            string factionId,
            out string message)
        {
            message = string.Empty;
            return false;
        }

        public bool TryRequestTrade(
            string factionId,
            out string routeId,
            out string message) => FailRoute(out routeId, out message);

        public bool TryRequestSupply(
            string factionId,
            out string routeId,
            out string message) => FailRoute(out routeId, out message);

        public bool TryRequestReinforcement(
            string factionId,
            out string routeId,
            out string message) => FailRoute(out routeId, out message);

        public bool TryApplyRouteAmbush(
            string routeId,
            int strengthLoss,
            float delaySeconds,
            out string message)
        {
            message = string.Empty;
            return false;
        }

        public bool TryBetray(
            string factionId,
            int stolenValue,
            out string message)
        {
            message = string.Empty;
            return false;
        }

        public bool TryPayRestitution(
            string factionId,
            int physicalValue,
            out string message)
        {
            message = string.Empty;
            return false;
        }

        public bool TryCompleteRecoveryEvent(
            string factionId,
            out string message)
        {
            message = string.Empty;
            return false;
        }

        public void RecordReinforcementLoss(
            string factionId,
            int deaths,
            int equipmentLosses)
        {
        }

        public DungeonFactionSaveData Capture() => Clone(Data);

        public FactionRestoreCandidate PrepareRestoreCandidate(
            DungeonFactionSaveData restored)
        {
            DungeonFactionSaveData payload = Clone(restored);
            FactionAggregateState candidateState = new()
            {
                CurrentDay = payload.currentDay,
                RouteSequence = payload.routeSequence
            };
            foreach (DungeonFactionState faction in payload.factions)
            {
                candidateState.Factions.Add(faction.factionId, faction);
            }
            candidateState.Routes.AddRange(payload.routes);
            return new FactionRestoreCandidate(candidateState, payload);
        }

        public void PublishRestoreCandidate(FactionRestoreCandidate candidate)
        {
            Data = candidate.Payload;
            if (store == null)
            {
                RestoreCount++;
            }
        }

        public void Reset()
        {
        }

        private static bool FailRoute(
            out string routeId,
            out string message)
        {
            routeId = string.Empty;
            message = string.Empty;
            return false;
        }

        private static DungeonFactionSaveData Clone(
            DungeonFactionSaveData source) =>
            JsonUtility.FromJson<DungeonFactionSaveData>(
                JsonUtility.ToJson(source));
    }

    private sealed class RequiredDependencyStubSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        public RequiredDependencyStubSection(
            string sectionId,
            DungeonSaveRestorePhase restorePhase)
        {
            SectionId = sectionId;
            RestorePhase = restorePhase;
        }

        public string SectionId { get; }
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase { get; }
        public IReadOnlyList<string> DependsOn => Array.Empty<string>();
        public string Capture() => "{}";
        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != 1 || string.IsNullOrWhiteSpace(payloadJson))
            {
                report.AddError("Invalid faction dependency payload.");
            }
        }
        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            ValidatePayload(payloadJson, sectionVersion, report);
            return new DungeonDelegateSaveRestoreStage(SectionId, _ => { });
        }
        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report) =>
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
    }

    private sealed class FinalFailingSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private readonly string dependency;

        public FinalFailingSection(string dependency)
        {
            this.dependency = dependency;
        }

        public bool WasCommitted { get; private set; }
        public string SectionId => "faction.debug.late-failure";
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.Presentation;
        public IReadOnlyList<string> DependsOn => new[] { dependency };
        public string Capture() => "{}";
        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != 1 || string.IsNullOrWhiteSpace(payloadJson))
            {
                report.AddError("Invalid faction final payload.");
            }
        }
        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            ValidatePayload(payloadJson, sectionVersion, report);
            return new DungeonDelegateSaveRestoreStage(
                SectionId,
                commitReport =>
                {
                    WasCommitted = true;
                    commitReport.AddError("Injected faction final failure.");
                });
        }
        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report) =>
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
    }

    private static T[] FindAssets<T>(string root)
        where T : UnityEngine.Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(value => value != null)
            .ToArray();
    }
}
#endif
