#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static partial class FormulaPresentationPilotExporter
{
    private const string ProductionSourceOutputEnvironment =
        "DUNGEONSTORY_GAMEPLAY_MODULE_SOURCE_V8_OUTPUT";
    private const string ProductionSourceVersionEnvironment =
        "DUNGEONSTORY_GAMEPLAY_MODULE_SOURCE_V8_VERSION";
    private const string ProductionCaptureModel =
        "dungeonstory-production-capture";

    private static readonly string[] ProductionSourceAuthorityFiles =
    {
        "Assets/Scripts/Models/NarrativeMechanics/NarrativeFormulaModuleSelection.cs",
        "Assets/Scripts/Services/Character/AI/Editor/GameplayModuleSelectionCanonicalJson.cs",
        "Assets/Scripts/Services/Character/AI/Editor/GameplayModuleSelectionLiveWireCapture.cs",
        "Assets/Scripts/Services/Character/AI/Editor/GameplayModuleSelectionSourceV8PackageExporter.cs",
        "Assets/Scripts/Services/Character/AI/Editor/GameplayModuleSelectionSourceV8RowBuilder.cs",
        "Assets/Scripts/Services/Character/AI/Editor/FormulaPresentationProductionSourceV8Exporter.cs",
        "Assets/Scripts/Services/Character/AI/Editor/CharacterCombatModuleSelectionSourceCapture.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Evolution/EquipmentModuleSelectionOutcomeDefinition.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ResearchWorkOutcomeDefinition.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/GameplayOutcomeEvidenceFormulaProjection.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/MigratedProducerOutcomeDefinition.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/CharacterFacilityUseOutcomeDetails.cs",
        "Assets/Scripts/Services/Infrastructure/BlueprintResearchOutcomeTransaction.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/ProductionCausalActionFixture.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/MigratedProducerOutcomeEditorFixture.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/MigratedProducerCombatDefenseEntryE2EDebugScenarios.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/PrimitiveSurvivalModuleSelectionProfileCapture.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/MigratedProducerCombatLifeCycleCausalDebugScenarios.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomes/Editor/EvolutionGameplayOutcomeEditorFixture.cs",
        "Assets/Scripts/Services/FacilityEvolution/Editor/FacilityEvolutionCausalProductionEntryDebugScenarios.cs",
        "Assets/Scripts/Services/FacilityEvolution/Editor/FacilityEvolutionModuleSelectionSourceCapture.cs",
        "Assets/Scripts/Services/Combat/Editor/EquipmentRelocationNarrativeEvidenceDebugScenarios.cs"
    };

    [MenuItem("Tools/DungeonStory/Narrative/Export Production Module Source V8 (100)")]
    public static void ExportProductionModuleSelection100FromEnvironment()
    {
        string output = RequireEnvironment(ProductionSourceOutputEnvironment);
        string version = RequireVersion(
            RequireEnvironment(ProductionSourceVersionEnvironment));
        string projectRoot = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            ".."));
        string gameCommit = RequireGitCommit(projectRoot);
        CatalogContext catalogs = LoadAndValidateCatalogs();
        string catalogSha256 = BuildCombinedCatalogSha256(catalogs);
        SourceDigest baseDigest = CaptureSourceDigest(projectRoot, catalogs);
        string[] sourceFiles = baseDigest.Files.Select(value => value.Path)
            .Concat(ProductionSourceAuthorityFiles)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        JArray sourceIdentity = new(sourceFiles.Select(path => new JObject
        {
            ["path"] = path,
            ["sha256"] = Sha256File(Path.Combine(
                projectRoot,
                path.Replace('/', Path.DirectorySeparatorChar)))
        }));
        string dirtySourceSha256 = GameplayModuleSelectionCanonicalJson.Hash(
            sourceIdentity);
        string contentSha256 = GameplayModuleSelectionCanonicalJson.Hash(
            new JObject
            {
                ["version"] = version,
                ["gameCommit"] = gameCommit,
                ["catalogSha256"] = catalogSha256,
                ["sourceFiles"] = sourceIdentity
            });
        string sourcePackageId = "gameplay-module-source-v8:" + version;
        string sourcePackageSha256 = GameplayModuleSelectionCanonicalJson.Hash(
            new JObject
            {
                ["sourcePackageId"] = sourcePackageId,
                ["contentSha256"] = contentSha256,
                ["catalogSha256"] = catalogSha256
            });

        var rows = new List<JObject>(RowsPerProfile * 4);
        var checkpoints = new List<GameplayModuleSelectionSourceCheckpoint>(
            RowsPerProfile * 4);
        BuildProductionSkillSourceRows(
            rows,
            checkpoints,
            catalogs,
            catalogSha256,
            gameCommit,
            dirtySourceSha256,
            contentSha256,
            sourcePackageId,
            sourcePackageSha256);
        BuildProductionTraitSourceRows(
            rows,
            checkpoints,
            catalogs,
            catalogSha256,
            gameCommit,
            dirtySourceSha256,
            contentSha256,
            sourcePackageId,
            sourcePackageSha256);
        BuildProductionEquipmentSourceRows(
            rows,
            checkpoints,
            catalogs,
            catalogSha256,
            gameCommit,
            dirtySourceSha256,
            contentSha256,
            sourcePackageId,
            sourcePackageSha256);
        BuildProductionFacilitySourceRows(
            rows,
            checkpoints,
            catalogs,
            catalogSha256,
            gameCommit,
            dirtySourceSha256,
            contentSha256,
            sourcePackageId,
            sourcePackageSha256);
        RequireProductionProfileCounts(rows);

        JObject result = GameplayModuleSelectionSourceV8PackageExporter.Export(
            output,
            sourcePackageId,
            rows,
            checkpoints,
            sourceFiles);
        Debug.Log(
            "Production gameplay module-selection source-v8 PASS "
            + result.ToString(Newtonsoft.Json.Formatting.None));
    }

    private static void BuildProductionSkillSourceRows(
        ICollection<JObject> rows,
        ICollection<GameplayModuleSelectionSourceCheckpoint> checkpoints,
        CatalogContext catalogs,
        string catalogSha256,
        string gameCommit,
        string dirtySourceSha256,
        string contentSha256,
        string sourcePackageId,
        string sourcePackageSha256)
    {
        CharacterSkillKind[] kinds =
        {
            CharacterSkillKind.Active,
            CharacterSkillKind.Passive,
            CharacterSkillKind.Ultimate
        };
        for (int index = 0; index < RowsPerProfile; index++)
        {
            CharacterSkillKind kind = kinds[index % kinds.Length];
            string ownerId = StableRowId("owner:source-v8:skill", index);
            PrimitiveSkillModuleSelectionCapture capture =
                CharacterCombatModuleSelectionSourceCapture.CaptureSkill(
                    ownerId,
                    catalogs.Skills,
                    kind,
                    revision: index,
                    scenarioIndex: index);
            string checkpointId = StableRowId(
                "checkpoint:source-v8:skill",
                index);
            int revision = RequireCharacterCheckpointRevision(
                capture.Checkpoint,
                expectedPrimitiveStage: 1);
            NarrativeFormulaModuleSelectionRequest[] requests =
                capture.Requests.ToArray();
            JObject[] authority = requests.Select((request, requestIndex) =>
            {
                int ruleIndex = capture.Draft.nextPresentationIndex + requestIndex;
                CharacterSkillCandidateRule rule = capture.Draft.rules[ruleIndex];
                return BuildAuthorityRequest(
                    request,
                    capture.DisplayName,
                    capture.Evidence,
                    SkillContextJson(rule, kind));
            }).ToArray();
            AddProductionRow(
                rows,
                checkpoints,
                capture.Checkpoint,
                checkpointId,
                capture.OwnerId,
                revision,
                capture.Draft.formulaVersion,
                catalogSha256,
                gameCommit,
                dirtySourceSha256,
                contentSha256,
                LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                capture.CharacterId,
                capture.Evidence,
                capture.Prompt,
                authority,
                sourcePackageId,
                sourcePackageSha256,
                index,
                "character-skill",
                "contract:character-skill-module-selection:1",
                SourceFilesHash(
                    "Assets/Scripts/Services/Character/Core/CharacterSkillFormulaGeneration.cs",
                    "Assets/Scripts/Services/Character/Core/CharacterSkillFormulaVersions.cs"));
        }
    }

    private static void BuildProductionTraitSourceRows(
        ICollection<JObject> rows,
        ICollection<GameplayModuleSelectionSourceCheckpoint> checkpoints,
        CatalogContext catalogs,
        string catalogSha256,
        string gameCommit,
        string dirtySourceSha256,
        string contentSha256,
        string sourcePackageId,
        string sourcePackageSha256)
    {
        for (int index = 0; index < RowsPerProfile; index++)
        {
            string ownerId = StableRowId("owner:source-v8:trait", index);
            PrimitiveTraitModuleSelectionCapture capture =
                CharacterCombatModuleSelectionSourceCapture.CaptureTrait(
                    ownerId,
                    catalogs.TraitSettings,
                    catalogs.Traits,
                    scenarioIndex: index);
            string checkpointId = StableRowId(
                "checkpoint:source-v8:trait",
                index);
            int revision = RequireCharacterCheckpointRevision(
                capture.Checkpoint,
                expectedPrimitiveStage: 3);
            JObject authority = BuildAuthorityRequest(
                capture.Request,
                capture.DisplayName,
                capture.Evidence);
            AddProductionRow(
                rows,
                checkpoints,
                capture.Checkpoint,
                checkpointId,
                capture.OwnerId,
                revision,
                capture.Pending.formulaVersion,
                catalogSha256,
                gameCommit,
                dirtySourceSha256,
                contentSha256,
                LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id,
                capture.CharacterId,
                capture.Evidence,
                capture.Prompt,
                new[] { authority },
                sourcePackageId,
                sourcePackageSha256,
                index,
                "acquired-trait",
                "contract:acquired-trait-module-selection:1",
                SourceFileHash(
                    "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitInferenceService.cs"));
        }
    }

    private static void BuildProductionEquipmentSourceRows(
        ICollection<JObject> rows,
        ICollection<GameplayModuleSelectionSourceCheckpoint> checkpoints,
        CatalogContext catalogs,
        string catalogSha256,
        string gameCommit,
        string dirtySourceSha256,
        string contentSha256,
        string sourcePackageId,
        string sourcePackageSha256)
    {
        CombatEquipmentDefinitionSO[] eligible = catalogs.EquipmentDefinitions
            .Where(value => EquipmentGameplayFormulaReplay.IsDefinitionReplayEligible(
                catalogs.Equipment,
                value,
                out _))
            .ToArray();
        if (eligible.Length == 0)
            throw new InvalidOperationException(
                "Production source has no replay-eligible equipment definition.");
        for (int index = 0; index < RowsPerProfile; index++)
        {
            CombatEquipmentDefinitionSO definition = eligible[index % eligible.Length];
            using EquipmentRelocationNarrativeEvidenceCapture relocated =
                EquipmentRelocationNarrativeEvidenceFixture.Capture(
                    "source-v8-" + index.ToString("D5", CultureInfo.InvariantCulture),
                    definition,
                    index);
            EquipmentProductionCapture capture = BuildEquipmentProductionCapture(
                relocated,
                catalogs.Equipment,
                index);
            string checkpointId = StableRowId(
                "checkpoint:source-v8:equipment",
                index);
            JObject authority = BuildAuthorityRequest(
                capture.Request,
                definition.DisplayName,
                capture.Evidence);
            AddProductionRow(
                rows,
                checkpoints,
                capture.Checkpoint,
                checkpointId,
                relocated.OwnerId,
                capture.OwnerRevision,
                capture.Node.formulaVersion,
                catalogSha256,
                gameCommit,
                dirtySourceSha256,
                contentSha256,
                LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection.Id,
                relocated.Instance.instanceId,
                capture.Evidence,
                capture.Prompt,
                new[] { authority },
                sourcePackageId,
                sourcePackageSha256,
                index,
                "equipment-evolution",
                "contract:equipment-evolution-module-selection:1",
                SourceFileHash(
                    "Assets/Scripts/Services/Combat/EquipmentEvolutionRules.cs"));
        }
    }

    private static void BuildProductionFacilitySourceRows(
        ICollection<JObject> rows,
        ICollection<GameplayModuleSelectionSourceCheckpoint> checkpoints,
        CatalogContext catalogs,
        string catalogSha256,
        string gameCommit,
        string dirtySourceSha256,
        string contentSha256,
        string sourcePackageId,
        string sourcePackageSha256)
    {
        for (int index = 0; index < RowsPerProfile; index++)
        {
            string ownerId = StableRowId("owner:source-v8:facility", index);
            FacilityEvolutionModuleSelectionSourceCapture capture =
                FacilityEvolutionDebugScenarios.CaptureModuleSelectionSource(
                    ownerId,
                    index);
            string checkpointId = StableRowId(
                "checkpoint:source-v8:facility",
                index);
            int revision = RequireFacilityCheckpointRevision(capture.Checkpoint);
            JObject authority = BuildAuthorityRequest(
                capture.Request,
                capture.DisplayName,
                capture.Evidence);
            AddProductionRow(
                rows,
                checkpoints,
                capture.Checkpoint,
                checkpointId,
                capture.OwnerId,
                revision,
                capture.Pending.node.formulaVersion,
                catalogSha256,
                gameCommit,
                dirtySourceSha256,
                contentSha256,
                LocalLlmRequestProfiles.FacilityEvolutionModuleSelection.Id,
                capture.FacilityId,
                capture.Evidence,
                capture.Prompt,
                new[] { authority },
                sourcePackageId,
                sourcePackageSha256,
                index,
                "facility-evolution",
                "contract:facility-evolution-module-selection:1",
                SourceFileHash(
                    "Assets/Scripts/Services/FacilityEvolution/FacilityFormulaEvolutionAuthority.cs"));
        }
    }

    private static EquipmentProductionCapture BuildEquipmentProductionCapture(
        EquipmentRelocationNarrativeEvidenceCapture relocated,
        EquipmentEvolutionFormulaCatalogSO catalog,
        int index)
    {
        CombatEquipmentInstance instance = relocated.Instance.Clone();
        instance.evolution ??= new EquipmentEvolutionState();
        string suffix = index.ToString("D5", CultureInfo.InvariantCulture);
        string historyHash = "history:source-v8:equipment:" + suffix;
        EquipmentEvolutionDirection direction = instance.definitionId == null
            ? EquipmentEvolutionDirection.Balanced
            : EquipmentDirection(relocated.Definition.Kind);
        float masteryCost = EquipmentEvolutionProgression.GetRequiredMastery(
            instance.evolution.generation);
        instance.evolution.mastery = Mathf.Max(
            instance.evolution.mastery,
            masteryCost);
        EvolutionNode priorNode = EquipmentEvolutionRules.BuildFormulaNode(
            instance.evolution,
            catalog,
            new EquipmentFormulaTargetContext(relocated.Definition),
            instance.instanceId,
            "equipment-node:source-v8:prior:" + suffix,
            string.Empty,
            historyHash + ":prior",
            direction,
            string.Empty,
            generation: 1,
            historical: false,
            manifestationPosition: "reforge:source-v8:prior:" + suffix,
            outcomeBindings: relocated.Evidence);
        EquipmentEvolutionPresentationRequest priorPresentation =
            EquipmentEvolutionRules.BuildPresentationRequest(
                priorNode,
                instance.instanceId,
                historyHash + ":prior",
                string.Empty,
                "reforge-order:source-v8:prior:" + suffix,
                1,
                masteryCost);
        instance.evolution.evolutionNodes.Add(priorNode);
        instance.evolution.presentationRequests.Add(priorPresentation);
        NarrativeFormulaModuleSelectionRequest priorRequest =
            EquipmentEvolutionRules.BuildModuleSelectionRequest(
                priorNode,
                catalog);
        EquipmentEvolutionModuleSelectionResponseDto priorResponse =
            BuildDeterministicEquipmentSelection(
                priorRequest,
                relocated.Evidence.First(value => string.Equals(
                    value.sourceKind,
                    "exact",
                    StringComparison.Ordinal)).publicFactId,
                relocated.Definition.DisplayName + "의 첫 담금질",
                "거친 이동과 보관의 흔적을 첫 진화의 결로 새겼다.");
        ICombatEquipmentRuntime port = System.Reflection.DispatchProxy.Create<
            ICombatEquipmentRuntime,
            ProductionEquipmentReplayProbe>();
        ProductionEquipmentReplayProbe probe =
            (ProductionEquipmentReplayProbe)(object)port;
        probe.Instance = instance;
        EquipmentEvolutionRuntime runtime =
            (EquipmentEvolutionRuntime)System.Runtime.Serialization
                .FormatterServices.GetUninitializedObject(
                    typeof(EquipmentEvolutionRuntime));
        SetRuntimeField(runtime, "equipment", port);
        SetRuntimeField(runtime, "ledgerCompactor", new UsageLedgerCompactor());
        SetRuntimeField(
            runtime,
            "aggregateRootStore",
            new DungeonRuntimeAggregateRootStore());
        SetRuntimeField(
            runtime,
            "evidenceUseTransaction",
            new GameplayOutcomeEvidenceUseTransaction(
                relocated.Ledger,
                new GameplayOutcomeNarrativeEvidenceQuery(relocated.Ledger)));
        SetRuntimeField(
            runtime,
            "moduleSelections",
            relocated.ModuleSelections);
        SetRuntimeField(
            runtime,
            "sessionStateProvider",
            new FixedGameSessionStateProvider(41));
        if (!runtime.TryCommitModuleSelection(
                instance.instanceId,
                priorResponse,
                out string priorFailure))
        {
            throw new InvalidOperationException(
                "Actual prior equipment module selection failed: "
                + priorFailure);
        }
        instance = probe.Instance.Clone();
        IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> evidence =
            DistinctPublicFacts(ProductionCausalNarrativeEvidence.Capture(
                relocated.Ledger,
                new GameplayEntityId(
                    TradeInventoryOutcomeIds.ItemInstanceKind,
                    instance.instanceId),
                8));
        if (evidence.Count < 2
            || evidence.All(value => !string.Equals(
                value.outcomeTypeId,
                EquipmentModuleSelectionOutcomeIds.Selected.Value,
                StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Prior equipment evolution did not become exact item-instance evidence.");
        }

        masteryCost = EquipmentEvolutionProgression.GetRequiredMastery(
            instance.evolution.generation);
        instance.evolution.mastery = Mathf.Max(
            instance.evolution.mastery,
            masteryCost);
        EvolutionNode node = EquipmentEvolutionRules.BuildFormulaNode(
            instance.evolution,
            catalog,
            new EquipmentFormulaTargetContext(relocated.Definition),
            instance.instanceId,
            "equipment-node:source-v8:" + suffix,
            priorNode.nodeId,
            historyHash,
            direction,
            string.Empty,
            generation: instance.evolution.generation + 1,
            historical: false,
            manifestationPosition: "reforge:source-v8:" + suffix,
            outcomeBindings: evidence);
        EquipmentEvolutionPresentationRequest presentation =
            EquipmentEvolutionRules.BuildPresentationRequest(
                node,
                instance.instanceId,
                historyHash,
                string.Empty,
                "reforge-order:source-v8:" + suffix,
                instance.evolution.generation + 1,
                masteryCost);
        instance.evolution.evolutionNodes.Add(node);
        instance.evolution.presentationRequests.Add(presentation);
        ItemInstanceComponentSaveData encoded = EquipmentItemStateCodec.Encode(
            instance);
        JObject encodedJson = JObject.Parse(JsonUtility.ToJson(encoded));
        ItemInstanceComponentSaveData restoredPayload = JsonUtility.FromJson<
            ItemInstanceComponentSaveData>(encodedJson.ToString(
                Newtonsoft.Json.Formatting.None));
        if (!EquipmentItemStateCodec.TryDecode(
                restoredPayload,
                out CombatEquipmentInstance restored,
                out string restoreFailure))
        {
            throw new InvalidOperationException(
                "Pending equipment module selection failed save restore: "
                + restoreFailure);
        }
        EvolutionNode restoredNode = restored.evolution.evolutionNodes.Single(value =>
            value != null
            && string.Equals(value.nodeId, node.nodeId, StringComparison.Ordinal));
        EquipmentEvolutionPresentationRequest restoredPresentation =
            restored.evolution.presentationRequests.Single(value => value != null
                && string.Equals(
                    value.presentationId,
                    node.presentationId,
                    StringComparison.Ordinal));
        NarrativeFormulaModuleSelectionRequest request =
            EquipmentEvolutionRules.BuildModuleSelectionRequest(
                restoredNode,
                catalog);
        EvolutionNarrativeRequestSnapshot publicRequest = new()
        {
            requestKey = restoredPresentation.presentationId,
            targetKind = EvolutionNarrativeTargetKind.Equipment,
            targetPersistentId = restored.instanceId,
            nodeId = restoredNode.nodeId,
            parentNodeId = restoredNode.parentNodeId,
            historyHash = restoredPresentation.historyHash,
            generation = restoredNode.generation,
            evidenceIds = new List<string>(),
            participantIds = new List<string>(),
            frozenCurrentEvents = new List<UsageLedgerEvent>(),
            frozenPriorGenerationSegments = new List<CompactedHistorySegment>(),
            frozenHistoryCaptured = true
        };
        NarrativePublicContextMaterial material =
            EvolutionNarrativePromptFormatter.BuildPublicMaterial(
                LocalLlmRequestProfiles.EvolutionHistory.Id,
                publicRequest,
            relocated.Definition.DisplayName,
            new Dictionary<string, string>(StringComparer.Ordinal),
            restoredPresentation.gameplayOutcomeEvidence);
        string prompt = EvolutionHistoryNarrativeRuntime
            .BuildEquipmentModuleSelectionPrompt(
                restoredNode,
                restoredPresentation,
                material);
        JObject sourceCheckpoint = (JObject)relocated.Checkpoint.DeepClone();
        sourceCheckpoint["equipmentState"] = JObject.Parse(JsonUtility.ToJson(
            instance));
        sourceCheckpoint["outcomeLedger"] = JObject.Parse(JsonUtility.ToJson(
            relocated.Ledger.CaptureGameplayOutcomes()));
        sourceCheckpoint["moduleSelectionPublications"] =
            EquipmentRelocationNarrativeEvidenceFixture
                .CaptureModuleSelectionPublications(
                    relocated.ModuleSelections);
        JObject checkpoint = new()
        {
            ["schemaVersion"] = 1,
            ["schema"] = "dungeonstory-module-source-equipment-owner@3",
            ["ownerId"] = relocated.OwnerId,
            ["ownerKind"] = "owner-kind:combat-equipment-instance",
            ["revision"] = 3,
            ["sourceCheckpoint"] = sourceCheckpoint,
            ["equipmentComponent"] = encodedJson,
            ["pendingSelectionId"] = restoredNode.moduleSelectionId
        };
        return new EquipmentProductionCapture(
            checkpoint,
            restoredNode,
            request,
            evidence,
            prompt,
            3);
    }

    private static EquipmentEvolutionModuleSelectionResponseDto
        BuildDeterministicEquipmentSelection(
            NarrativeFormulaModuleSelectionRequest request,
            string exactEvidenceFactId,
            string displayName,
            string narrativeFlavor)
    {
        string positive = request.Offers
            .Where(value => value.Polarity
                == NarrativeFormulaModulePolarity.Positive)
            .Select(value => value.ModuleId)
            .First();
        string exactEvidence = request.EvidenceFactIds.Single(value =>
            string.Equals(value, exactEvidenceFactId, StringComparison.Ordinal));
        return new EquipmentEvolutionModuleSelectionResponseDto
        {
            selectionId = request.SelectionId,
            positiveModuleIds = new List<string> { positive },
            drawbackModuleIds = new List<string>(),
            evidenceFactIds = new List<string> { exactEvidence },
            displayName = displayName,
            narrativeFlavor = narrativeFlavor
        };
    }

    private static IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot>
        DistinctPublicFacts(
            IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> evidence) =>
        (evidence ?? throw new ArgumentNullException(nameof(evidence)))
            .Where(value => value != null)
            .GroupBy(
                value => value.canonicalFactText,
                StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(value => value.outcomeSequence)
                .First()
                .Clone())
            .OrderBy(value => value.outcomeSequence)
            .ToArray();

    private static EquipmentEvolutionDirection EquipmentDirection(
        CombatEquipmentKind kind) => kind switch
    {
        CombatEquipmentKind.MeleeWeapon => EquipmentEvolutionDirection.Melee,
        CombatEquipmentKind.RangedWeapon => EquipmentEvolutionDirection.Ranged,
        CombatEquipmentKind.Armor => EquipmentEvolutionDirection.Protection,
        CombatEquipmentKind.Shield => EquipmentEvolutionDirection.Protection,
        _ => throw new InvalidOperationException(
            "Unsupported equipment source-v8 kind: " + kind)
    };

    private static void AddProductionRow(
        ICollection<JObject> rows,
        ICollection<GameplayModuleSelectionSourceCheckpoint> checkpoints,
        JObject checkpoint,
        string checkpointId,
        string ownerId,
        int ownerRevision,
        int formulaVersion,
        string catalogSha256,
        string gameCommit,
        string dirtySourceSha256,
        string contentSha256,
        string profileId,
        string targetPersistentId,
        IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> evidence,
        string prompt,
        IReadOnlyList<JObject> authority,
        string sourcePackageId,
        string sourcePackageSha256,
        int rowIndex,
        string domain,
        string applicationContractId,
        string applicationContractSha256)
    {
        JObject owner = checkpoint == null
            ? throw new ArgumentNullException(nameof(checkpoint))
            : (JObject)checkpoint.DeepClone();
        string ownerCheckpointSha256 = GameplayModuleSelectionCanonicalJson
            .Sha256Prefixed(GameplayModuleSelectionSourceV8PackageExporter
                .CheckpointFileBytes(owner));
        string logicalRequestId = "logical:source-v8:" + domain + ":"
            + rowIndex.ToString("D5", CultureInfo.InvariantCulture);
        JObject live = GameplayModuleSelectionLiveWireCapture.Capture(
            profileId,
            logicalRequestId,
            "attempt:source-v8:" + domain + ":"
                + rowIndex.ToString("D5", CultureInfo.InvariantCulture),
            ProductionCaptureModel,
            prompt,
            authority);
        GameplayOutcomeEvidenceBindingSnapshot[] bindings = evidence
            .Where(value => value != null)
            .OrderBy(value => value.publicFactId, StringComparer.Ordinal)
            .ToArray();
        if (bindings.Length == 0)
            throw new InvalidOperationException(
                "Production source-v8 row has no exact outcome evidence.");
        string[] actionIds = bindings.Select(value => value.operationId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (actionIds.Length == 0)
            throw new InvalidOperationException(
                "Production source-v8 evidence has no operation identity.");
        JObject execution = new()
        {
            ["sourcePackageId"] = sourcePackageId,
            ["sourcePackageSha256"] = sourcePackageSha256,
            ["campaignId"] = "campaign:module-source-v8:production",
            ["ownerId"] = ownerId,
            ["ownerCheckpointId"] = checkpointId,
            ["ownerCheckpointSha256"] = ownerCheckpointSha256,
            ["ownerRevision"] = ownerRevision,
            ["episodeId"] = "episode:source-v8:" + domain + ":"
                + rowIndex.ToString("D5", CultureInfo.InvariantCulture),
            ["actionIds"] = new JArray(actionIds),
            ["outcomeIds"] = new JArray(bindings
                .Where(value => string.Equals(
                    GameplayOutcomeEvidenceBindingAuthority
                        .EffectiveSourceKind(value),
                    "exact",
                    StringComparison.Ordinal))
                .Select(value => value.outcomeRunId + "/"
                    + value.outcomeSequence.ToString(
                        CultureInfo.InvariantCulture))),
            ["compactedMemoryIds"] = new JArray(bindings
                .Select(value => value.compactedMemoryId)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)),
            ["captureDay"] = bindings.Max(value => value.lastDay),
            ["triggerId"] = "trigger:" + domain + ":module-selection",
            ["controlledSimulation"] = true,
            ["restoredFromCheckpoint"] = true
        };
        rows.Add(GameplayModuleSelectionSourceV8RowBuilder.BuildRow(
            formulaVersion,
            catalogSha256,
            gameCommit,
            dirtySourceSha256,
            contentSha256,
            profileId,
            targetPersistentId,
            execution,
            bindings,
            live,
            authority,
            owner,
            applicationContractId,
            applicationContractSha256));
        checkpoints.Add(new GameplayModuleSelectionSourceCheckpoint(
            checkpointId,
            ownerId,
            ownerRevision,
            owner));
    }

    private static JObject BuildAuthorityRequest(
        NarrativeFormulaModuleSelectionRequest request,
        string targetDisplayName,
        IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> evidence,
        JObject skillContext = null)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        Dictionary<string, GameplayOutcomeEvidenceBindingSnapshot> byId =
            (evidence ?? throw new ArgumentNullException(nameof(evidence)))
            .Where(value => value != null)
            .ToDictionary(value => value.publicFactId, StringComparer.Ordinal);
        JObject[] publicFacts = request.EvidenceFactIds.Select(factId =>
        {
            if (!byId.TryGetValue(factId, out var binding))
            {
                throw new InvalidOperationException(
                    "A production authority request referenced non-exact evidence: "
                    + factId);
            }
            return new JObject
            {
                ["factId"] = factId,
                ["text"] = binding.canonicalFactText
            };
        }).ToArray();
        bool skill = skillContext != null;
        JObject result = new()
        {
            ["selectionId"] = request.SelectionId,
            ["targetDisplayName"] = RequireCanonicalText(
                targetDisplayName,
                nameof(targetDisplayName)),
            ["moduleOffers"] = new JArray(request.Offers.Select(value =>
                new JObject
                {
                    ["moduleId"] = value.ModuleId,
                    ["polarity"] = value.Polarity switch
                    {
                        NarrativeFormulaModulePolarity.Positive => "positive",
                        NarrativeFormulaModulePolarity.Drawback => "drawback",
                        _ => throw new ArgumentOutOfRangeException()
                    },
                    ["semanticDescription"] = value.SemanticDescription
                })),
            ["maximumPositiveModules"] = request.MaximumPositiveModules,
            ["maximumDrawbackModules"] = request.MaximumDrawbackModules,
            ["evidenceFactIds"] = new JArray(request.EvidenceFactIds),
            ["publicFacts"] = new JArray(publicFacts),
            ["style"] = new JObject
            {
                ["genre"] = "fantasy-wuxia",
                ["nameMaxLength"] = skill ? 14 : 32,
                ["flavorMaxLength"] = skill ? 48 : 180
            }
        };
        if (skill)
            result["skillContext"] = skillContext.DeepClone();
        return result;
    }

    private static int RequireCheckpointStage(
        JObject checkpoint,
        int expectedStage)
    {
        int stage = (int?)checkpoint?["domainState"]?["stage"] ?? -1;
        if (stage != expectedStage)
        {
            throw new InvalidOperationException(
                $"Primitive checkpoint stage {stage} != {expectedStage}.");
        }
        return stage;
    }

    private static int RequireCharacterCheckpointRevision(
        JObject checkpoint,
        int expectedPrimitiveStage) =>
        CharacterCombatModuleSelectionSourceCapture.IsCombatCheckpoint(checkpoint)
            ? CharacterCombatModuleSelectionSourceCapture.RequireRevision(
                checkpoint)
            : RequireCheckpointStage(checkpoint, expectedPrimitiveStage);

    private static int RequireFacilityCheckpointRevision(JObject checkpoint)
    {
        int revision = (int?)checkpoint?["domainState"]?["firstOwnerRevision"]
            ?? -1;
        if (revision < 1)
            throw new InvalidOperationException(
                "Facility checkpoint lost its committed owner revision.");
        return revision;
    }

    private static string SourceFileHash(string relativePath)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            ".."));
        return Sha256File(Path.Combine(
            projectRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string SourceFilesHash(params string[] relativePaths) =>
        Sha256Text(string.Join("\n", relativePaths
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(value => value + "=" + SourceFileHash(value))));

    private static string StableRowId(string prefix, int index) =>
        prefix + ":" + index.ToString("D5", CultureInfo.InvariantCulture);

    private static string RequireEnvironment(string name)
    {
        string value = Environment.GetEnvironmentVariable(name)?.Trim()
            ?? string.Empty;
        if (value.Length == 0)
            throw new InvalidOperationException(
                "Required export environment variable is missing: " + name);
        return value;
    }

    private static string RequireCanonicalText(string value, string name)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (canonical.Length == 0
            || !string.Equals(canonical, value, StringComparison.Ordinal))
            throw new ArgumentException(
                "A canonical non-empty value is required.",
                name);
        return canonical;
    }

    private static string RequirePrompt(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(
                "A non-empty prompt is required.",
                name);
        return value;
    }

    private static void RequireProductionProfileCounts(
        IReadOnlyCollection<JObject> rows)
    {
        string[] profiles =
        {
            LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
            LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id,
            LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection.Id,
            LocalLlmRequestProfiles.FacilityEvolutionModuleSelection.Id
        };
        if (rows.Count != RowsPerProfile * profiles.Length
            || profiles.Any(profile => rows.Count(value => string.Equals(
                    (string)value["profileId"],
                    profile,
                    StringComparison.Ordinal)) != RowsPerProfile))
        {
            throw new InvalidOperationException(
                "Production source-v8 export must contain exactly 25 rows per profile.");
        }
    }

    private sealed class EquipmentProductionCapture
    {
        internal EquipmentProductionCapture(
            JObject checkpoint,
            EvolutionNode node,
            NarrativeFormulaModuleSelectionRequest request,
            IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> evidence,
            string prompt,
            int ownerRevision)
        {
            Checkpoint = (JObject)(checkpoint
                ?? throw new ArgumentNullException(nameof(checkpoint))).DeepClone();
            Node = node?.Clone() ?? throw new ArgumentNullException(nameof(node));
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Evidence = (evidence ?? throw new ArgumentNullException(nameof(evidence)))
                .Select(value => value.Clone()).ToArray();
            Prompt = RequirePrompt(prompt, nameof(prompt));
            OwnerRevision = ownerRevision;
        }

        internal JObject Checkpoint { get; }
        internal EvolutionNode Node { get; }
        internal NarrativeFormulaModuleSelectionRequest Request { get; }
        internal IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> Evidence { get; }
        internal string Prompt { get; }
        internal int OwnerRevision { get; }
    }
}
#endif
