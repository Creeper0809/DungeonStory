#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class SurgeryDebugScenarios
{
    private const string ReportPath = "Temp/surgery-system-contracts.tsv";

    private static readonly string[] RequiredResearchIds =
    {
        "research:medical:anatomy",
        "research:medical:surgery",
        "research:medical:prosthetics",
        "research:medical:organ-preservation",
        "research:medical:xenotransplant",
        "research:medical:aberrant-augmentation"
    };

    [MenuItem("DungeonStory/Debug/Medical/Run Surgery System Contracts")]
    public static void RunFromMenu()
    {
        if (!RunAll(logSuccess: true))
        {
            Debug.LogError("Surgery system contracts failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        SurgeryContentAssetBuilder.ValidateBuiltContent();
        Directory.CreateDirectory("Temp");
        List<string> lines = new List<string> { "case\tresult\tdetails" };
        List<string> errors = new List<string>();

        Run("specialized_facilities", VerifySpecializedFacilities, lines, errors);
        Run("procedure_catalog", VerifyProcedureCatalog, lines, errors);
        Run("anatomy_profiles", VerifyAnatomyProfiles, lines, errors);
        Run("research_branch", VerifyResearchBranch, lines, errors);
        Run("prosthetic_recipes", VerifyProstheticRecipes, lines, errors);
        Run("risk_formula", VerifyRiskFormula, lines, errors);
        Run("organ_preservation_restore_join", OrganPreservationRestoreJoinFixture.Run, lines, errors);
        Run("corpse_extraction_ledger", VerifyExtractionLedger, lines, errors);
        Run("unique_part_save_data", VerifyUniquePartSaveData, lines, errors);
        Run(
            "surgical_part_installation_pending_outbox",
            VerifySurgicalPartInstallationPendingOutbox,
            lines,
            errors);
        Run(
            "surgery_material_sink_join",
            VerifyMaterialSinkJoin,
            lines,
            errors);
        Run("strict_v6_payload", VerifyStrictV6Payload, lines, errors);
        Run(
            "identifier_sequence_exhaustion",
            VerifyIdentifierSequenceExhaustion,
            lines,
            errors);
        Run(
            "restore_late_participant_rollback",
            VerifyRestoreLateParticipantRollback,
            lines,
            errors);
        Run("work_and_stat_contract", VerifyWorkAndStatContract, lines, errors);

        File.WriteAllLines(ReportPath, lines);
        foreach (string error in errors)
        {
            Debug.LogError(error);
        }

        if (errors.Count == 0 && logSuccess)
        {
            Debug.Log($"Surgery system contracts PASS. Report: {ReportPath}");
        }

        return errors.Count == 0;
    }

    public static bool RunAtomicSurgeryRestoreContracts()
    {
        try
        {
            DungeonRuntimeLifetimeScope scope =
                UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                    FindObjectsInactive.Include);
            if (scope == null)
            {
                throw new InvalidOperationException(
                    "Atomic surgery validation requires a live runtime scope.");
            }

            ISurgeryPersistence persistence =
                scope.Container.Resolve<ISurgeryPersistence>();
            SurgeryRestoreCoordinator coordinator =
                scope.Container.Resolve<SurgeryRestoreCoordinator>();
            DungeonRuntimeAggregateRootStore rootStore =
                scope.Container.Resolve<DungeonRuntimeAggregateRootStore>();
            IsolatedSurgerySaveSection surgerySection = new(
                persistence,
                coordinator);
            FailOnceAfterSurgerySaveSection failOnce = new();
            DungeonSaveSectionRegistry registry = new(
                new IDungeonSaveSection[] { surgerySection, failOnce },
                rootStore,
                new IDungeonRestoreTransactionParticipant[] { coordinator });

            List<DungeonSaveSectionEnvelope> baseline = registry.CaptureAll();
            DungeonSaveSectionEnvelope surgeryEnvelope = baseline.Single(
                envelope => envelope.sectionId == surgerySection.SectionId);
            DungeonSurgerySaveData payload =
                JsonUtility.FromJson<DungeonSurgerySaveData>(
                    surgeryEnvelope.payloadJson);
            if (surgeryEnvelope.sectionVersion
                    != DungeonSurgerySaveData.CurrentVersion
                || payload.version != DungeonSurgerySaveData.CurrentVersion)
            {
                Debug.LogError(
                    "SURGERY_ATOMIC_RESTORE section/payload version mismatch.");
                return false;
            }

            DungeonGameRestoreReport valid = new();
            if (!registry.RestoreAll(baseline, valid) || !valid.Success)
            {
                Debug.LogError(
                    "SURGERY_ATOMIC_RESTORE valid round trip failed: "
                    + string.Join(" | ", valid.Errors));
                return false;
            }

            string stateBeforeFailure = JsonUtility.ToJson(
                persistence.Capture());
            int revisionBeforeFailure = rootStore.PublishedRestoreRevision;
            List<DungeonSaveSectionEnvelope> failing = registry.CaptureAll();
            DungeonSaveSectionEnvelope changed = failing.Single(
                envelope => envelope.sectionId == surgerySection.SectionId);
            DungeonSurgerySaveData changedPayload =
                JsonUtility.FromJson<DungeonSurgerySaveData>(changed.payloadJson);
            changedPayload.orderSequence++;
            changed.payloadJson = JsonUtility.ToJson(changedPayload);
            failOnce.FailNextCommit = true;
            DungeonGameRestoreReport failed = new();
            bool failureAccepted = registry.RestoreAll(failing, failed);
            string stateAfterFailure = JsonUtility.ToJson(
                persistence.Capture());
            if (failureAccepted
                || failed.Success
                || !string.Equals(
                    stateBeforeFailure,
                    stateAfterFailure,
                    StringComparison.Ordinal)
                || rootStore.PublishedRestoreRevision != revisionBeforeFailure
                || rootStore.IsRestoreStaging)
            {
                Debug.LogError(
                    "SURGERY_ATOMIC_RESTORE failed commit changed live state.");
                return false;
            }

            List<DungeonSaveSectionEnvelope> legacy = registry.CaptureAll();
            legacy.Single(envelope =>
                    envelope.sectionId == surgerySection.SectionId)
                .sectionVersion = DungeonSurgerySaveData.CurrentVersion - 1;
            DungeonGameRestoreReport legacyReport = new();
            if (registry.RestoreAll(legacy, legacyReport)
                || legacyReport.Success
                || rootStore.IsRestoreStaging)
            {
                Debug.LogError(
                    "SURGERY_ATOMIC_RESTORE accepted a legacy section version.");
                return false;
            }

            Debug.Log(
                "SURGERY_ATOMIC_RESTORE=PASS "
                + $"rollbackErrors={failed.Errors.Count} "
                + $"legacyErrors={legacyReport.Errors.Count}");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }

    private static string VerifySpecializedFacilities()
    {
        BuildingSO[] buildings = LoadAssets<BuildingSO>(
            "Assets/Resources/SO/Building/Medical");
        Require(buildings.Length == 13, $"expected 13 medical facilities, got {buildings.Length}");
        Require(
            buildings.Select(building => building.id).Distinct().Count() == buildings.Length,
            "medical building ids were not unique");
        Require(
            buildings.All(building => building.sprite != null),
            "a medical facility had no authored sprite");
        Require(
            buildings.All(building =>
                building.Abilities.OfType<ISurgicalFacilityAbility>().Any()),
            "a medical facility had no surgical ability");
        Require(
            buildings.All(building =>
                building.Facility?.SupportsRole(FacilityRole.Medical) == true),
            "a medical facility was not assigned FacilityRole.Medical");

        SurgeryFacilityTag covered = buildings
            .SelectMany(building => building.Abilities.OfType<ISurgicalFacilityAbility>())
            .Aggregate(
                SurgeryFacilityTag.None,
                (current, ability) => current | ability.FacilityTags);
        SurgeryFacilityTag required = Enum.GetValues(typeof(SurgeryFacilityTag))
            .Cast<SurgeryFacilityTag>()
            .Aggregate(SurgeryFacilityTag.None, (current, value) => current | value);
        // Age treatments are intentionally executed by the five V21 domain
        // facilities (8868-8872), not duplicated among the 13 foundational
        // surgery buildings in this folder.
        required &= ~SurgeryFacilityTag.AgeTreatment;
        Require((covered & required) == required, $"facility tag coverage incomplete: {covered}");
        Require(
            buildings.Any(building =>
                building.Abilities.OfType<BuildingOrganStorageAbility>().Any()),
            "organ storage facility was missing");
        BuildingSO organStorage = buildings.SingleOrDefault(building =>
            string.Equals(
                building.GetFacilityCode(),
                "M08",
                StringComparison.Ordinal));
        BuildingStorageAbility organWarehouse = organStorage?
            .Abilities
            .OfType<BuildingStorageAbility>()
            .SingleOrDefault();
        Require(organStorage != null
                && organWarehouse != null
                && organWarehouse.category == StockCategory.Biological
                && organWarehouse.capacity == 8
                && organWarehouse.maxStoredMassGrams
                    == SurgeryContentAssetBuilder.OrganStorageMassCapacityGrams
                && !organWarehouse.allCategories,
            "M08 organ warehouse is not exact Biological/count 8/12,500g restricted storage");
        BuildingSO prostheticAssembly = buildings.SingleOrDefault(building =>
            building.Abilities.OfType<BuildingProstheticAssemblyAbility>().Any());
        BuildingProductionWorkstationAbility prostheticWorkstation =
            prostheticAssembly?.GetProductionWorkstationAbility();
        BuildingProductionBufferAbility prostheticBuffer =
            prostheticAssembly?.GetProductionBufferAbility();
        Require(
            prostheticAssembly != null
            && string.Equals(
                prostheticWorkstation?.WorkstationTag,
                "m06",
                StringComparison.Ordinal)
            && string.Equals(
                prostheticWorkstation.StockSensorInstallationItemId,
                ProductionBillRuntime.StockSensorItemId,
                StringComparison.Ordinal)
            && prostheticBuffer?.defaultBatchCapacity == 4
            && prostheticBuffer.physicalOutputBufferCycleCapacity == 4
            && !prostheticBuffer.allowOverflowDump,
            "prosthetic assembly facility is missing exact m06/4-cycle common production authority");
        return "13 medical facilities cover every foundational surgery/support tag; M06 owns exact m06/4-cycle production authority; V21 age-treatment facilities are validated separately";
    }

    private static string VerifyProcedureCatalog()
    {
        SurgicalProcedureSO[] procedures = LoadAssets<SurgicalProcedureSO>(
            "Assets/Resources/SO/Medical/Procedures");
        ResourceSurgicalProcedureCatalog catalog =
            new ResourceSurgicalProcedureCatalog(procedures);
        Require(procedures.Length == 47, $"expected 47 procedures, got {procedures.Length}");
        Require(catalog.Validate().Count == 0, string.Join(" | ", catalog.Validate()));
        Require(
            procedures.All(procedure =>
                procedure.RequiredWork > 0f
                && procedure.RequiredFacilityTags != SurgeryFacilityTag.None),
            "a procedure had no work or facility requirement");
        Require(
            procedures.All(procedure =>
                procedure.Materials.Count > 0
                && procedure.Materials.All(material =>
                    !string.IsNullOrWhiteSpace(material.itemId)
                    && material.quantity > 0)),
            "a procedure did not require physical material delivery");
        Require(
            procedures.Any(procedure =>
                procedure.Kind == SurgicalProcedureKind.ExtractOrgan
                && procedure.AllowsCorpseSubject),
            "corpse organ extraction procedure was missing");
        Require(
            procedures.Any(procedure =>
                procedure.Kind == SurgicalProcedureKind.Rehabilitation
                && procedure.Effects.OfType<ReduceSurgicalBurdenEffect>().Any()),
            "rehabilitation did not reduce post-operative burdens");
        return "47 procedures require work, facilities, hauled materials, and valid effects";
    }

    private static string VerifyAnatomyProfiles()
    {
        AnatomyProfileSO[] profiles = LoadAssets<AnatomyProfileSO>(
            "Assets/Resources/SO/Medical/Anatomy");
        ResourceAnatomyProfileCatalog catalog =
            new ResourceAnatomyProfileCatalog(profiles);
        Require(profiles.Length == 12, $"expected 12 anatomy profiles, got {profiles.Length}");
        Require(catalog.Validate().Count == 0, string.Join(" | ", catalog.Validate()));

        AnatomyProfileDefinition humanoid = catalog.GetDefaultHumanoid();
        Require(humanoid.Nodes.Count >= 16, "humanoid anatomy did not contain the full node set");
        RequireNode(humanoid, "brain", vital: true);
        RequireNode(humanoid, "heart", vital: true);
        RequireNode(humanoid, "torso", vital: true);
        RequirePaired(humanoid, "eyes", 2);
        RequirePaired(humanoid, "lungs", 2);
        RequirePaired(humanoid, "kidneys", 2);
        RequirePaired(humanoid, "arms", 2);
        RequirePaired(humanoid, "legs", 2);
        Require(
            catalog.GetForSpecies("shadow_wolf").AnatomyFamily == "quadruped",
            "quadruped wildlife did not resolve to its anatomy");
        Require(
            catalog.GetForSpecies("Slime").ProfileId == "anatomy:slime",
            "slime did not resolve to its dedicated anatomy");
        Dictionary<string, string> expectedSpeciesProfiles =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Human"] = "anatomy:human",
                ["Orc"] = "anatomy:orc",
                ["Vampire"] = "anatomy:vampire",
                ["Beastkin"] = "anatomy:beastkin",
                ["Demon"] = "anatomy:demon",
                ["Kobold"] = "anatomy:kobold",
                ["Myconid"] = "anatomy:fungal",
                ["Harpy"] = "anatomy:avian",
                ["Golem"] = "anatomy:construct"
            };
        foreach (KeyValuePair<string, string> pair in expectedSpeciesProfiles)
        {
            Require(
                catalog.GetForSpecies(pair.Key).ProfileId == pair.Value,
                $"{pair.Key} resolved to the wrong anatomy profile");
        }
        Require(
            expectedSpeciesProfiles.Keys
                .Select(species => catalog.GetForSpecies(species).ProfileId)
                .Distinct(StringComparer.Ordinal)
                .Count() == 9,
            "the nine playable/NPC species did not resolve to nine independent anatomy profiles");
        RequireNode(catalog.GetForSpecies("Orc"), "tusk:left", vital: false);
        RequireNode(catalog.GetForSpecies("Vampire"), "blood-sac", vital: false);
        RequireNode(catalog.GetForSpecies("Beastkin"), "balance-tail", vital: false);
        RequireNode(catalog.GetForSpecies("Demon"), "mana-core", vital: true);
        RequireNode(catalog.GetForSpecies("Kobold"), "hand:left", vital: false);
        return "12 anatomy assets provide nine independent species profiles plus legacy and wildlife contracts";
    }

    private static string VerifyResearchBranch()
    {
        ResearchProjectSO[] projects = LoadAssets<ResearchProjectSO>(
            "Assets/Resources/SO/Research/Projects");
        ResourceResearchProjectCatalog catalog =
            new ResourceResearchProjectCatalog(projects);
        Require(
            projects.Length >= 78,
            $"expected at least 78 research projects, got {projects.Length}");
        Require(catalog.Validate().Count == 0, string.Join(" | ", catalog.Validate()));
        foreach (string id in RequiredResearchIds)
        {
            Require(
                catalog.TryGet(id, out ResearchProjectSO project)
                && project.Field == ResearchField.SurgeryAndTransplant,
                $"missing surgery research node {id}");
        }

        RequirePrerequisite(
            catalog,
            "research:medical:surgery",
            "research:medical:anatomy");
        RequirePrerequisite(
            catalog,
            "research:medical:prosthetics",
            "research:medical:surgery");
        RequirePrerequisite(
            catalog,
            "research:medical:xenotransplant",
            "research:medical:organ-preservation");
        RequirePrerequisite(
            catalog,
            "research:medical:aberrant-augmentation",
            "research:medical:xenotransplant");
        return "the research graph includes the six surgery nodes and prerequisites";
    }

    private static string VerifyProstheticRecipes()
    {
        ProductionRecipeSO[] recipes = LoadAssets<ProductionRecipeSO>(
                "Assets/Resources/SO/Economy/Recipes")
            .Where(recipe => recipe.RecipeId.StartsWith(
                "recipe:surgery:",
                StringComparison.Ordinal))
            .ToArray();
        Require(recipes.Length == 3, $"expected 3 prosthetic recipes, got {recipes.Length}");
        Require(
            recipes.All(recipe =>
                recipe.WorkTypeId == BuiltInWorkTypeIds.Craft
                && string.Equals(
                    recipe.WorkstationTag,
                    "m06",
                    StringComparison.Ordinal)
                && recipe.RequiredWork > 0f
                && recipe.Inputs.Count > 0
                && recipe.Outputs.Count == 1),
            "prosthetic recipes did not use exact m06 work, materials, and a unique output");
        return "three prosthetic recipes use the exact m06 workstation, physical inputs, and cumulative craft work";
    }

    private static string VerifyRiskFormula()
    {
        SurgicalProcedureSO procedure = LoadAssets<SurgicalProcedureSO>(
                "Assets/Resources/SO/Medical/Procedures")
            .First();
        SurgeryRiskEvaluator evaluator = new SurgeryRiskEvaluator(
            CharacterAiEditorTestDependencies.NeutralPerformance);
        SurgicalFacilitySnapshot poor = new SurgicalFacilitySnapshot(
            null,
            procedure.RequiredFacilityTags,
            0f,
            1f,
            0f,
            0f,
            Array.Empty<BuildableObject>(),
            DomainFailure.None);
        SurgicalFacilitySnapshot good = new SurgicalFacilitySnapshot(
            null,
            procedure.RequiredFacilityTags,
            1f,
            1.5f,
            0.25f,
            1f,
            Array.Empty<BuildableObject>(),
            DomainFailure.None);
        SurgicalSubjectRef subject = new SurgicalSubjectRef
        {
            kind = SurgicalSubjectKind.Character,
            subjectId = "patient:test",
            speciesId = "Human"
        };
        SurgeryRiskBreakdown poorRisk =
            evaluator.Evaluate(null, subject, procedure, poor, 0.8f, 0.5f);
        SurgeryRiskBreakdown goodRisk =
            evaluator.Evaluate(null, subject, procedure, good, 0f, 0f);
        Require(goodRisk.successChance > poorRisk.successChance, "facility and stability did not affect success");
        Require(
            poorRisk.successChance >= 0.05f && goodRisk.successChance <= 0.98f,
            "success clamp was violated");
        Require(
            Mathf.Approximately(
                poorRisk.deathChance,
                (1f - poorRisk.successChance) * 0.1f),
            "fatal failure weighting changed");
        return "risk uses facility, cleanliness, instability, compatibility, and fixed clamps";
    }

    private static string VerifyExtractionLedger()
    {
        SurgeryAggregateStateStore stateStore = new(
            new DungeonRuntimeAggregateRootStore());
        SurgeryExtractionLedger ledger = new SurgeryExtractionLedger(stateStore);
        Require(
            ledger.TryMarkExtracted("corpse:test", "heart", out _),
            "first extraction was rejected");
        Require(
            !ledger.TryMarkExtracted(
                "corpse:test",
                "heart",
                out DomainFailure failure)
            && failure.Code == FailureCode.SurgeryExtractionAlreadyRecorded,
            "duplicate extraction was accepted");
        Require(
            ledger.TryMarkExtracted("corpse:test", "lung:left", out _),
            "another organ could not be extracted");

        IReadOnlyList<CorpseSurgicalRecord> captured = ledger.Capture();
        Require(
            captured.Count == 1
            && captured[0].stackId == "corpse:test"
            && captured[0].extractedNodeIds.SequenceEqual(
                new[] { "heart", "lung:left" }),
            "extraction ledger did not capture canonical state");
        return "corpse organs are extracted once and capture canonical aggregate state";
    }

    private static string VerifyUniquePartSaveData()
    {
        SurgicalPartInstance part = new SurgicalPartInstance
        {
            partInstanceId = "surgical-part:test",
            kind = SurgicalPartKind.NaturalOrgan,
            nodeId = "eye:left",
            displayName = "룬사슴의 눈",
            donorId = "wildlife:test",
            donorSpeciesId = "rune_deer",
            anatomyFamily = "quadruped",
            quality = 1.2f,
            freshnessSeconds = 360f,
            specialEffectId = "graft:rune-deer-night-sight",
            specialEffectStrength = 1f,
            worldStackId = "stack:test"
        };
        DungeonSurgerySaveData data = new DungeonSurgerySaveData
        {
            parts = new List<SurgicalPartInstance> { part },
            orders = new List<SurgeryOrder>
            {
                new SurgeryOrder
                {
                    orderId = "surgery:test",
                    procedureId = "procedure:emergency-suture",
                    state = SurgeryOrderState.Procedure,
                    statusData = new SurgeryStatusData
                    {
                        code = SurgeryStatusCode.ProcedureInProgress,
                        primaryId = "character:test",
                        secondaryId = "procedure:emergency-suture",
                        scalarValue = 12.5f,
                        secondaryScalarValue = 3.25f,
                        tertiaryScalarValue = 0.75f,
                        countValue = 2,
                        stage = SurgeryOrderState.Procedure
                    },
                    environmentWait = new SurgeryStatusData
                    {
                        code = SurgeryStatusCode.EnvironmentStabilizing,
                        scalarValue = 4.5f,
                        stage = SurgeryOrderState.Suturing
                    },
                    environmentRecovery = new SurgeryStatusData
                    {
                        code = SurgeryStatusCode.EnvironmentRecoveryRequested,
                        countValue = 3,
                        stage = SurgeryOrderState.Recovering
                    },
                    reachedClinicalStages = new List<SurgeryOrderState>
                    {
                        SurgeryOrderState.Anesthetizing,
                        SurgeryOrderState.Incision,
                        SurgeryOrderState.Procedure
                    }
                }
            },
            corpseFreshness = new List<SurgicalCorpseFreshnessState>
            {
                new SurgicalCorpseFreshnessState
                {
                    stackId = "corpse:test",
                    remainingFreshnessSeconds = 180f
                }
            }
        };
        DungeonSurgerySaveData restored = JsonUtility.FromJson<DungeonSurgerySaveData>(
            JsonUtility.ToJson(data));
        Require(restored.parts.Count == 1, "unique surgical part was lost");
        Require(
            restored.parts[0].partInstanceId == part.partInstanceId
            && restored.parts[0].specialEffectId == part.specialEffectId,
            "donor or graft metadata changed during save");
        Require(
            restored.corpseFreshness.Single().remainingFreshnessSeconds == 180f,
            "corpse freshness changed during save");
        Require(
            restored.orders.Single().reachedClinicalStages.SequenceEqual(
                data.orders.Single().reachedClinicalStages),
            "clinical stage history changed during save");
        SurgeryOrder restoredOrder = restored.orders.Single();
        Require(
            restoredOrder.statusData.code == SurgeryStatusCode.ProcedureInProgress
            && restoredOrder.statusData.primaryId == "character:test"
            && restoredOrder.statusData.secondaryId == "procedure:emergency-suture"
            && restoredOrder.statusData.scalarValue == 12.5f
            && restoredOrder.statusData.secondaryScalarValue == 3.25f
            && restoredOrder.statusData.tertiaryScalarValue == 0.75f
            && restoredOrder.statusData.countValue == 2
            && restoredOrder.statusData.stage == SurgeryOrderState.Procedure
            && restoredOrder.environmentWait.code
                == SurgeryStatusCode.EnvironmentStabilizing
            && restoredOrder.environmentRecovery.code
                == SurgeryStatusCode.EnvironmentRecoveryRequested,
            "typed surgery status payload changed during save");
        return "unique donor, graft, freshness, clinical stages, and typed statuses round-trip through V6 section data";
    }

    private static string VerifySurgicalPartInstallationPendingOutbox()
    {
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        var rootStore = new DungeonRuntimeAggregateRootStore();
        var repository = new WorldItemRepository(
            new GuidPersistentIdGenerator(),
            rootStore);
        var batch = new PhysicalItemBatchDispositionService(
            repository,
            new PhysicalItemMassQuery(catalog),
            EditorNullItemMarkerPresenter.Instance);
        string sourceStackId = repository.AddEditorTestStack(
            "material:lumber",
            1,
            WorldItemStackState.Loose);
        const string orderId = "surgery:1";
        const string partId = "surgical-part:1";
        const string subjectId = "character:contract-patient";
        string operationId =
            SurgicalPartInstallationIdentity.FormatOperationId(
                orderId,
                partId);
        Require(batch.TryCommitPending(
                new[] { new PhysicalItemTransformInput(sourceStackId, 1) },
                PhysicalItemDispositionKind.Transfer,
                operationId,
                SurgicalPartInstallationOutbox.TransferReason,
                out PhysicalItemBatchDispositionReceipt receipt,
                out string commitFailure),
            "surgical part fixture could not stage its pending transfer: "
                + commitFailure);
        var pending = new SurgicalPartInstance
        {
            partInstanceId = partId,
            kind = SurgicalPartKind.Prosthetic,
            nodeId = "heart",
            displayName = "outbox contract part",
            quality = 1f,
            freshnessSeconds = 360f,
            worldStackId = sourceStackId,
            reservedOrderId = orderId,
            installationOrderId = orderId,
            installationOperationId = operationId,
            installationCommitId = receipt.CommitId,
            installationSourceStackId = sourceStackId,
            installationSubjectId = subjectId
        };
        Require(repository.GetEditorTestQuantity(sourceStackId) == 0
            && repository.GetEditorPendingBatchDispositionCount() == 1,
            "pending surgical transfer did not retain exact physical custody");

        ResourceSurgicalProcedureCatalog procedures = new(
            LoadAssets<SurgicalProcedureSO>(
                "Assets/Resources/SO/Medical/Procedures"));
        ResourceAnatomyProfileCatalog anatomyProfiles = new(
            LoadAssets<AnatomyProfileSO>(
                "Assets/Resources/SO/Medical/Anatomy"));
        DungeonSurgerySaveData pendingSave = new()
        {
            orderSequence = 1,
            partSequence = 1,
            orders = new List<SurgeryOrder>
            {
                new()
                {
                    orderId = orderId,
                    procedureId = "procedure:emergency-suture",
                    subject = new SurgicalSubjectRef
                    {
                        kind = SurgicalSubjectKind.Character,
                        subjectId = subjectId
                    },
                    facilityId = "facility:surgery-outbox-contract",
                    materialDestinationId =
                        ReservedTargetDestinationIdentity.SurgeryMaterialsPrefix
                        + orderId,
                    materialBufferCapacityGrams = 1L,
                    materialMassAuthorityRevision = 1L,
                    materialCapacityFingerprint = string.Empty,
                    state = SurgeryOrderState.Procedure
                }
            },
            parts = new List<SurgicalPartInstance>
            {
                SurgeryStateCloner.ClonePart(pending)
            }
        };
        SurgeryOrder pendingOrder = pendingSave.orders.Single();
        pendingOrder.materialCapacityFingerprint =
            SurgeryMaterialCapacityFingerprint.Create(pendingOrder);
        DungeonGameRestoreReport pendingReport = new();
        SurgerySaveValidation.Validate(
            pendingSave,
            procedures,
            anatomyProfiles,
            pendingReport);
        Require(pendingReport.Success,
            "V11 rejected canonical pending surgical outbox: "
                + string.Join(" | ", pendingReport.Errors));

        SurgicalPartInstance mismatched = SurgeryStateCloner.ClonePart(pending);
        mismatched.installationCommitId += "1";
        Require(!SurgicalPartInstallationOutbox.TryFinalizePending(
                mismatched,
                batch,
                out _)
            && !mismatched.installed
            && repository.GetEditorPendingBatchDispositionCount() == 1,
            "mismatched surgical receipt mutated domain or physical custody");
        DungeonSurgerySaveData tamperedSave = CloneSaveData(pendingSave);
        tamperedSave.parts[0].installationCommitId += ":tampered";
        DungeonGameRestoreReport tamperedReport = new();
        SurgerySaveValidation.Validate(
            tamperedSave,
            procedures,
            anatomyProfiles,
            tamperedReport);
        Require(!tamperedReport.Success,
            "V9 accepted a tampered surgical installation receipt");

        SurgicalPartInstance restored = SurgeryStateCloner.ClonePart(pending);
        Require(SurgicalPartInstallationOutbox.TryFinalizePending(
                restored,
                batch,
                out string finalizeFailure)
            && restored.installed
            && restored.installedSubjectId == subjectId
            && string.IsNullOrEmpty(restored.worldStackId)
            && string.IsNullOrEmpty(restored.reservedOrderId)
            && repository.GetEditorPendingBatchDispositionCount() == 0,
            "pending surgical transfer did not finalize exactly once: "
                + finalizeFailure);
        Require(SurgicalPartInstallationOutbox.TryFinalizePending(
                restored,
                batch,
                out string replayFailure)
            && repository.GetEditorPendingBatchDispositionCount() == 0,
            "installed surgical transfer retry was not idempotent: "
                + replayFailure);
        pendingSave.parts[0] = SurgeryStateCloner.ClonePart(restored);
        DungeonGameRestoreReport installedReport = new();
        SurgerySaveValidation.Validate(
            pendingSave,
            procedures,
            anatomyProfiles,
            installedReport);
        Require(installedReport.Success,
            "V9 rejected terminal surgical outbox evidence: "
                + string.Join(" | ", installedReport.Errors));

        return "physical pending receipt survives the crash boundary, mismatched commit is atomic, restore finalizes and acknowledges exactly once";
    }

    private static string VerifyStrictV6Payload()
    {
        ResourceSurgicalProcedureCatalog procedures = new(
            LoadAssets<SurgicalProcedureSO>(
                "Assets/Resources/SO/Medical/Procedures"));
        ResourceAnatomyProfileCatalog anatomyProfiles = new(
            LoadAssets<AnatomyProfileSO>(
                "Assets/Resources/SO/Medical/Anatomy"));

        DungeonSurgerySaveData valid = new();
        DungeonGameRestoreReport validReport = new();
        SurgerySaveValidation.Validate(
            valid,
            procedures,
            anatomyProfiles,
            validReport);
        Require(
            validReport.Success,
            $"canonical empty V6 payload failed: {string.Join(" | ", validReport.Errors)}");

        DungeonSurgerySaveData legacy = CloneSaveData(valid);
        legacy.version = DungeonSurgerySaveData.CurrentVersion - 1;
        RequireRejected(legacy, procedures, anatomyProfiles, "legacy V5 payload");

        DungeonSurgerySaveData unknownStatus = CloneSaveData(valid);
        unknownStatus.orderSequence = 1;
        unknownStatus.orders.Add(new SurgeryOrder
        {
            orderId = "surgery:1",
            procedureId = "procedure:emergency-suture",
            subject = new SurgicalSubjectRef
            {
                kind = SurgicalSubjectKind.Character,
                subjectId = "character:contract-patient"
            },
            state = SurgeryOrderState.Completed,
            statusData = new SurgeryStatusData
            {
                code = (SurgeryStatusCode)int.MaxValue,
                stage = SurgeryOrderState.Completed
            }
        });
        RequireRejected(
            unknownStatus,
            procedures,
            anatomyProfiles,
            "unknown surgery status code");

        DungeonSurgerySaveData missingCollection = CloneSaveData(valid);
        missingCollection.parts = null;
        RequireRejected(
            missingCollection,
            procedures,
            anatomyProfiles,
            "missing required collection");

        DungeonSurgerySaveData reusedSequence = CloneSaveData(valid);
        reusedSequence.orderSequence = -1;
        RequireRejected(
            reusedSequence,
            procedures,
            anatomyProfiles,
            "negative order sequence");

        DungeonSurgerySaveData canonicalNumericIds = CloneSaveData(valid);
        canonicalNumericIds.orderSequence = 1;
        canonicalNumericIds.partSequence = 1;
        canonicalNumericIds.orders.Add(new SurgeryOrder
        {
            orderId = "surgery:1",
            procedureId = "procedure:emergency-suture",
            subject = new SurgicalSubjectRef
            {
                kind = SurgicalSubjectKind.Character,
                subjectId = "character:contract-patient"
            },
            state = SurgeryOrderState.Completed
        });
        canonicalNumericIds.parts.Add(new SurgicalPartInstance
        {
            partInstanceId = "surgical-part:1",
            kind = SurgicalPartKind.NaturalOrgan,
            nodeId = "heart",
            displayName = "contract organ"
        });
        DungeonGameRestoreReport canonicalNumericReport = new();
        SurgerySaveValidation.Validate(
            canonicalNumericIds,
            procedures,
            anatomyProfiles,
            canonicalNumericReport);
        Require(
            canonicalNumericReport.Success,
            "canonical positive surgery IDs were rejected: "
                + string.Join(" | ", canonicalNumericReport.Errors));

        foreach (string malformedOrderId in new[]
                 {
                     "surgery:+1",
                     "surgery:01",
                     "surgery:0",
                     "surgery:test"
                 })
        {
            DungeonSurgerySaveData malformed = CloneSaveData(
                canonicalNumericIds);
            malformed.orders[0].orderId = malformedOrderId;
            RequireRejected(
                malformed,
                procedures,
                anatomyProfiles,
                $"noncanonical order ID {malformedOrderId}");
        }

        foreach (string malformedPartId in new[]
                 {
                     "surgical-part:+1",
                     "surgical-part:01",
                     "surgical-part:0",
                     "surgical-part:test"
                 })
        {
            DungeonSurgerySaveData malformed = CloneSaveData(
                canonicalNumericIds);
            malformed.parts[0].partInstanceId = malformedPartId;
            RequireRejected(
                malformed,
                procedures,
                anatomyProfiles,
                $"noncanonical part ID {malformedPartId}");
        }

        DungeonSurgerySaveData duplicatePolicy = CloneSaveData(valid);
        duplicatePolicy.policies.Add(new SurgerySubjectPolicyState
        {
            subjectId = "character:contract-patient",
            automaticEmergencySurgery = true
        });
        duplicatePolicy.policies.Add(new SurgerySubjectPolicyState
        {
            subjectId = "character:contract-patient",
            automaticEmergencySurgery = false
        });
        RequireRejected(
            duplicatePolicy,
            procedures,
            anatomyProfiles,
            "duplicate subject policy");

        return "strict V6 accepts canonical state and rejects legacy, unknown status, missing, sequence, duplicate, and noncanonical numeric ID corruption";
    }

    private static string VerifyMaterialSinkJoin()
    {
        ResourceSurgicalProcedureCatalog procedures = new(
            LoadAssets<SurgicalProcedureSO>(
                "Assets/Resources/SO/Medical/Procedures"));
        ResourceAnatomyProfileCatalog anatomyProfiles = new(
            LoadAssets<AnatomyProfileSO>(
                "Assets/Resources/SO/Medical/Anatomy"));
        SurgeryOrder order = new()
        {
            orderId = "surgery:1",
            procedureId = "procedure:emergency-suture",
            subject = new SurgicalSubjectRef
            {
                kind = SurgicalSubjectKind.Character,
                subjectId = "character:material-sink-contract"
            },
            facilityId = "building:material-sink-contract",
            materialDestinationId = "surgery-materials:surgery:1",
            materialBufferCapacityGrams = 250L,
            materialMassAuthorityRevision = 1L,
            materialSinkOperationId =
                SurgeryMaterialSinkIdentity.FormatOperationId("surgery:1"),
            materialSinkCommitId =
                "physical-item-disposition:surgery-material-sink-contract",
            materialSinkInputMassGrams = 250L,
            materialSinkAcknowledged = false,
            materialsConsumed = true,
            state = SurgeryOrderState.Procedure,
            materials = new List<SurgicalMaterialRequirement>
            {
                new()
                {
                    itemId = SurgeryItemDefinitions.AnestheticId,
                    quantity = 1
                }
            }
        };
        order.materialCapacityFingerprint =
            SurgeryMaterialCapacityFingerprint.Create(order);
        DungeonSurgerySaveData save = new()
        {
            orderSequence = 1,
            orders = new List<SurgeryOrder> { order }
        };
        DungeonSurgerySaveData roundTrip =
            JsonUtility.FromJson<DungeonSurgerySaveData>(
                JsonUtility.ToJson(save));
        DungeonGameRestoreReport valid = new();
        SurgerySaveValidation.Validate(
            roundTrip,
            procedures,
            anatomyProfiles,
            valid);
        Require(valid.Success,
            "V11 rejected canonical pending material sink join: "
                + string.Join(" | ", valid.Errors));

        DungeonSurgerySaveData badOperation = CloneSaveData(save);
        badOperation.orders[0].materialSinkOperationId += ":tampered";
        RequireRejected(
            badOperation,
            procedures,
            anatomyProfiles,
            "tampered material sink operation");

        DungeonSurgerySaveData badMass = CloneSaveData(save);
        badMass.orders[0].materialSinkInputMassGrams = 0L;
        RequireRejected(
            badMass,
            procedures,
            anatomyProfiles,
            "zero material sink input grams");

        DungeonSurgerySaveData terminalPending = CloneSaveData(save);
        terminalPending.orders[0].state = SurgeryOrderState.Cancelled;
        RequireRejected(
            terminalPending,
            procedures,
            anatomyProfiles,
            "terminal order with unacknowledged material sink");

        DungeonSurgerySaveData unconsumedWithJoin = CloneSaveData(save);
        unconsumedWithJoin.orders[0].materialsConsumed = false;
        RequireRejected(
            unconsumedWithJoin,
            procedures,
            anatomyProfiles,
            "unconsumed order retaining material sink join");

        return "V11 persists and rejects tampering of exact material sink operation, commit, grams, and acknowledgement";
    }

    private static string VerifyIdentifierSequenceExhaustion()
    {
        ResourceSurgicalProcedureCatalog procedures = new(
            LoadAssets<SurgicalProcedureSO>(
                "Assets/Resources/SO/Medical/Procedures"));
        ResourceAnatomyProfileCatalog anatomyProfiles = new(
            LoadAssets<AnatomyProfileSO>(
                "Assets/Resources/SO/Medical/Anatomy"));
        string maximumSequence = int.MaxValue.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        string maximumOrderId = "surgery:" + maximumSequence;
        string maximumPartId = "surgical-part:" + maximumSequence;
        DungeonSurgerySaveData maximum = new()
        {
            orderSequence = int.MaxValue,
            partSequence = int.MaxValue,
            orders = new List<SurgeryOrder>
            {
                new()
                {
                    orderId = maximumOrderId,
                    procedureId = "procedure:emergency-suture",
                    subject = new SurgicalSubjectRef
                    {
                        kind = SurgicalSubjectKind.Character,
                        subjectId = "character:sequence-limit-patient"
                    },
                    state = SurgeryOrderState.Completed
                }
            },
            parts = new List<SurgicalPartInstance>
            {
                new()
                {
                    partInstanceId = maximumPartId,
                    kind = SurgicalPartKind.NaturalOrgan,
                    nodeId = "heart",
                    displayName = "sequence limit organ"
                }
            }
        };
        DungeonGameRestoreReport validation = new();
        SurgerySaveValidation.Validate(
            maximum,
            procedures,
            anatomyProfiles,
            validation);
        Require(
            validation.Success,
            "maximum canonical surgery identities/watermarks were rejected: "
                + string.Join(" | ", validation.Errors));

        SurgeryAggregateState state = SurgerySaveValidation.CreateState(maximum);
        SurgeryOrder preservedOrder = state.Orders.Single();
        SurgicalPartInstance preservedPart = state.Parts.Single();
        int orderCount = state.Orders.Count;
        int partCount = state.Parts.Count;

        bool orderPrepared = state.TryPrepareNextOrderIdentity(
            out int nextOrderSequence,
            out string nextOrderId,
            out DomainFailure orderFailure);
        bool partPrepared = state.TryPrepareNextPartIdentity(
            out int nextPartSequence,
            out string nextPartId,
            out DomainFailure partFailure);

        Require(
            !orderPrepared
            && orderFailure.Code == FailureCode.SurgeryEffectFailed
            && orderFailure.Parameters.Length == 1
            && orderFailure.Parameters[0]
                == SurgeryAggregateState.OrderSequenceExhaustedReason
            && nextOrderSequence == int.MaxValue
            && string.IsNullOrEmpty(nextOrderId),
            "maximum restored surgery order sequence did not fail explicitly");
        Require(
            !partPrepared
            && partFailure.Code == FailureCode.SurgeryEffectFailed
            && partFailure.Parameters.Length == 1
            && partFailure.Parameters[0]
                == SurgeryAggregateState.PartSequenceExhaustedReason
            && nextPartSequence == int.MaxValue
            && string.IsNullOrEmpty(nextPartId),
            "maximum restored surgical part sequence did not fail explicitly");
        Require(
            state.OrderSequence == int.MaxValue
            && state.PartSequence == int.MaxValue
            && state.Orders.Count == orderCount
            && state.Parts.Count == partCount
            && ReferenceEquals(state.Orders.Single(), preservedOrder)
            && ReferenceEquals(state.Parts.Single(), preservedPart)
            && preservedOrder.orderId == maximumOrderId
            && preservedPart.partInstanceId == maximumPartId,
            "failed next-identity preparation mutated restored surgery state");

        return "canonical maximum IDs restore with matching watermarks; next order and part creation fail without mutation";
    }

    private static string VerifyWorkAndStatContract()
    {
        Require(
            BuiltInWorkTypeIds.Surgery.Value == "work:surgery",
            "surgery work id was unstable");
        Require(
            WorkTypeCatalog.TryGet(
                BuiltInWorkTypeIds.Surgery,
                out WorkTypeDefinition definition)
            && definition.DefaultPriority == WorkPriorityLevel.Priority1,
            "surgery was not Priority1");
        Require(
            Enum.GetValues(typeof(CharacterFunctionalCapacityId)).Length == 14,
            "functional capacity count was not 14");
        CharacterSkillSystemSettingsSO settings =
            ScriptableObject.CreateInstance<CharacterSkillSystemSettingsSO>();
        try
        {
            Require(settings.initialStatTotal == 60, "initial stat total was not 60");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }

        return "surgery is a registered Priority1 work type and Medical is the twelfth stat";
    }

    private static string VerifyRestoreLateParticipantRollback()
    {
        Require(
            SurgeryRestoreFaultScenarios.Run(),
            "surgery restore publication did not roll back exactly after a late participant failure");
        return "three late-participant checkpoints preserve transports, orders, patient phase, and deferred wildlife returns until completion";
    }

    private static T[] LoadAssets<T>(string folder)
        where T : UnityEngine.Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset != null)
            .ToArray();
    }

    private static DungeonSurgerySaveData CloneSaveData(
        DungeonSurgerySaveData source)
    {
        return JsonUtility.FromJson<DungeonSurgerySaveData>(
            JsonUtility.ToJson(source));
    }

    private static void RequireRejected(
        DungeonSurgerySaveData payload,
        ISurgicalProcedureCatalog procedures,
        IAnatomyProfileCatalog anatomyProfiles,
        string caseName)
    {
        DungeonGameRestoreReport report = new();
        SurgerySaveValidation.Validate(
            payload,
            procedures,
            anatomyProfiles,
            report);
        Require(
            !report.Success && report.Errors.Count > 0,
            $"{caseName} was accepted");
    }

    private sealed class IsolatedSurgerySaveSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private readonly ISurgeryPersistence persistence;
        private readonly SurgeryRestoreCoordinator coordinator;

        internal IsolatedSurgerySaveSection(
            ISurgeryPersistence persistence,
            SurgeryRestoreCoordinator coordinator)
        {
            this.persistence = persistence
                ?? throw new ArgumentNullException(nameof(persistence));
            this.coordinator = coordinator
                ?? throw new ArgumentNullException(nameof(coordinator));
        }

        public string SectionId => "surgery.atomic-contract";
        public int SectionVersion =>
            DungeonSurgerySaveData.CurrentVersion;
        public DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.LateRuntimeState;
        public IReadOnlyList<string> DependsOn => Array.Empty<string>();

        public string Capture()
        {
            return JsonUtility.ToJson(persistence.Capture());
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            IDungeonSaveRestoreStage stage = StageRestore(
                payloadJson,
                sectionVersion,
                report);
            if (report.Success)
            {
                stage.Commit(report);
            }
        }

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            coordinator.PrepareRestore(ReadPayload(payloadJson, sectionVersion));
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            SurgeryRestoreCandidate candidate = coordinator.PrepareRestore(
                ReadPayload(payloadJson, sectionVersion));
            return new DungeonDelegateSaveRestoreStage(
                SectionId,
                _ => coordinator.PublishRestore(candidate));
        }

        private DungeonSurgerySaveData ReadPayload(
            string payloadJson,
            int sectionVersion)
        {
            if (sectionVersion != SectionVersion)
            {
                throw new InvalidOperationException(
                    $"Unsupported {SectionId} section version {sectionVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                throw new InvalidOperationException(
                    $"{SectionId} payload is empty.");
            }

            return JsonUtility.FromJson<DungeonSurgerySaveData>(payloadJson)
                ?? throw new InvalidOperationException(
                    $"{SectionId} payload deserialized to null.");
        }
    }

    private sealed class FailOnceAfterSurgerySaveSection :
        DungeonDebugStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        internal bool FailNextCommit;

        public override string SectionId => "zz.surgery-atomic-failure";
        public override DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.LateRuntimeState;

        protected override void CommitMarker(
            DungeonGameRestoreReport report)
        {
            if (!FailNextCommit)
            {
                return;
            }

            FailNextCommit = false;
            report.AddError("Injected post-surgery commit failure.");
        }
    }

    private static void RequireNode(
        AnatomyProfileDefinition profile,
        string nodeId,
        bool vital)
    {
        Require(profile.TryGetNode(nodeId, out AnatomyNodeDefinition node), $"missing anatomy node {nodeId}");
        Require(node.Vital == vital, $"anatomy node {nodeId} vital flag was incorrect");
    }

    private static void RequirePaired(
        AnatomyProfileDefinition profile,
        string pairedGroupId,
        int count)
    {
        Require(
            profile.Nodes.Count(node =>
                string.Equals(
                    node.PairedGroupId,
                    pairedGroupId,
                    StringComparison.Ordinal)) == count,
            $"paired anatomy group {pairedGroupId} did not contain {count} nodes");
    }

    private static void RequirePrerequisite(
        IResearchProjectCatalog catalog,
        string projectId,
        string prerequisiteId)
    {
        Require(catalog.TryGet(projectId, out ResearchProjectSO project), $"missing project {projectId}");
        Require(
            project.Prerequisites.Any(prerequisite =>
                string.Equals(
                    prerequisite.ProjectId.Value,
                    prerequisiteId,
                    StringComparison.Ordinal)),
            $"{projectId} did not depend on {prerequisiteId}");
    }

    private static void Run(
        string name,
        Func<string> test,
        ICollection<string> lines,
        ICollection<string> errors)
    {
        try
        {
            string details = test();
            lines.Add($"{name}\tPASS\t{details}");
        }
        catch (Exception exception)
        {
            string message = $"{name}: {exception.Message}";
            lines.Add($"{name}\tFAIL\t{exception.Message}");
            errors.Add(message);
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
