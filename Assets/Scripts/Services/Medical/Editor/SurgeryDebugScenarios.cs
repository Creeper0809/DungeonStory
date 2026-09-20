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
        Run(
            "replacement_physical_save_join",
            VerifyReplacementPhysicalSaveJoin,
            lines,
            errors);
        Run(
            "replacement_runtime_transaction",
            VerifyReplacementRuntimeTransaction,
            lines,
            errors);
        Run(
            "surgery_facility_single_owner_save_join",
            VerifySurgeryFacilitySingleOwnerSaveJoin,
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
        Require(recipes.Length == 15, $"expected 15 prosthetic recipes, got {recipes.Length}");
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
        return "fifteen prosthetic recipes use the exact m06 workstation, physical inputs, and cumulative craft work";
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
            itemDefinitionId = "medical:organ-eye-left",
            physicalItemInstanceId = "item-instance:surgery-test",
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
            itemDefinitionId = "material:lumber",
            physicalItemInstanceId = "item-instance:surgery-outbox",
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

    private static string VerifyReplacementPhysicalSaveJoin()
    {
        const string orderId = "surgery:1";
        const string oldPartId = "surgical-part:1";
        const string incomingPartId = "surgical-part:2";
        const string subjectId = "character:replacement-fixture";
        const string nodeId = "heart";
        const string itemId = "medical:prosthetic-heart";
        const string physicalId = "item-instance:replacement-old";
        const string stackId = "stack:replacement-old";
        const long mass = 1000L;
        string operationId = SurgicalPartReplacementIdentity
            .FormatOperationId(orderId);
        string commitId = SurgicalPartReplacementIdentity
            .FormatBatchCommitId(orderId);
        string plannedHash = new string('b', 64);
        SurgicalPartInstance previous = new()
        {
            partInstanceId = oldPartId,
            itemDefinitionId = itemId,
            physicalItemInstanceId = physicalId,
            kind = SurgicalPartKind.Prosthetic,
            nodeId = nodeId,
            displayName = "recovered heart",
            quality = 0.8f,
            worldStackId = stackId,
            reservedOrderId = orderId,
            detachedDurabilityCurrent = 32f,
            detachedDurabilityMaximum = 80f,
            recoveryOperationId = operationId,
            recoveryOrderId = orderId,
            recoveryCommitId = commitId
        };
        SurgicalPartInstance incoming = new()
        {
            partInstanceId = incomingPartId,
            itemDefinitionId = "medical:prosthetic-heart",
            physicalItemInstanceId = "item-instance:replacement-incoming",
            kind = SurgicalPartKind.Prosthetic,
            nodeId = nodeId,
            displayName = "incoming heart",
            quality = 1f,
            installed = true,
            installedSubjectId = subjectId,
            installationOrderId = orderId,
            installationOperationId =
                SurgicalPartInstallationIdentity.FormatOperationId(
                    orderId,
                    incomingPartId),
            installationCommitId =
                "physical-batch-disposition:1:"
                + SurgicalPartInstallationIdentity.FormatOperationId(
                    orderId,
                    incomingPartId)
                + ":1:1000",
            installationSourceStackId = "stack:replacement-incoming",
            installationSubjectId = subjectId
        };
        SurgeryOrder order = new()
        {
            orderId = orderId,
            procedureId = "procedure:prosthetic-installation",
            subject = new SurgicalSubjectRef
            {
                kind = SurgicalSubjectKind.Character,
                subjectId = subjectId
            },
            targetNodeId = nodeId,
            selectedPartInstanceId = incomingPartId,
            facilityId = "facility:replacement",
            materialDestinationId = "surgery-materials:" + orderId,
            materialBufferCapacityGrams = 3000L,
            materialMassAuthorityRevision = 1L,
            state = SurgeryOrderState.Recovering,
            resultRolled = true,
            resultSucceeded = true,
            resultOutcomeId = "success",
            resolvedEffectCount = 1,
            replacementPhase = SurgicalPartReplacementPhase.Completed,
            replacementOperationId = operationId,
            replacementExpectedOldPartId = oldPartId,
            replacementIncomingPartId = incomingPartId,
            replacementAdmissionTokenId = "planned-output:replacement",
            replacementPublicationOperationId =
                SurgicalPartReplacementIdentity.FormatPublicationOperationId(
                    orderId),
            replacementReservationAttempt = 1,
            replacementBatchCommitId = commitId,
            replacementOutcomeFingerprint = string.Empty,
            replacementPlannedOutputFingerprint = plannedHash,
            replacementOutputStackId = stackId,
            replacementOutputItemInstanceId = physicalId,
            replacementOutputMassGrams = mass,
            replacementDetachedCurrentHealth = 32f,
            replacementDetachedMaxHealth = 80f
        };
        order.replacementOutcomeFingerprint =
            SurgicalPartProductionOutputCrossAggregateSaveValidation
                .CreateReplacementOutcomeFingerprintForEditor(previous, order);
        order.materialCapacityFingerprint =
            SurgeryMaterialCapacityFingerprint.Create(order);
        IReadOnlyList<ItemInstanceComponentSaveData> components =
            SurgicalPartProductionOutputCrossAggregateSaveValidation
                .CreateReplacementPhysicalComponentsForEditor(
                    previous,
                    order,
                    mass);
        DungeonPhysicalItemSaveData physical = new()
        {
            stacks = new List<WorldItemStackSaveData>
            {
                new()
                {
                    stackId = stackId,
                    itemInstanceId = physicalId,
                    itemId = itemId,
                    quantity = 1,
                    state = WorldItemStackState.FacilityOutputBuffer,
                    destinationId = order.materialDestinationId,
                    components = components.Select(value => value.Clone())
                        .ToList()
                }
            }
        };
        DungeonSurgerySaveData surgery = new()
        {
            orders = new List<SurgeryOrder> { order },
            parts = new List<SurgicalPartInstance> { previous, incoming }
        };
        DungeonCharacterBodyHealthSaveData body = new()
        {
            characters = new List<CharacterBodyHealthState>
            {
                new()
                {
                    characterId = subjectId,
                    anatomyNodes = new List<AnatomyNodeHealthState>
                    {
                        new()
                        {
                            nodeId = nodeId,
                            maxHealth = 80f,
                            currentHealth = 28f,
                            installedPartId = incomingPartId,
                            installedPartKind = SurgicalPartKind.Prosthetic
                        }
                    }
                }
            }
        };
        surgery.orderSequence = 1;
        surgery.partSequence = 2;
        ResourceSurgicalProcedureCatalog procedures = new(
            LoadAssets<SurgicalProcedureSO>(
                "Assets/Resources/SO/Medical/Procedures"));
        ResourceAnatomyProfileCatalog anatomyProfiles = new(
            LoadAssets<AnatomyProfileSO>(
                "Assets/Resources/SO/Medical/Anatomy"));
        DungeonGameRestoreReport saveReport = new();
        SurgerySaveValidation.Validate(
            surgery,
            procedures,
            anatomyProfiles,
            saveReport);
        Require(saveReport.Success,
            "prosthetic replacement receipt failed surgery save validation: "
                + string.Join(" | ", saveReport.Errors));
        SurgicalPartProductionOutputCrossAggregateSaveValidation
            .ValidatePartOwnership(physical, surgery, body);
        body.characters[0].anatomyNodes[0].installedPartId = oldPartId;
        bool rejected = false;
        try
        {
            SurgicalPartProductionOutputCrossAggregateSaveValidation
                .ValidatePartOwnership(physical, surgery, body);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected,
            "replacement save join accepted a mismatched body owner");
        return "replacement receipt joins old physical identity, recovered durability, and incoming body owner";
    }

    private static string VerifyReplacementRuntimeTransaction()
    {
        VerifyReplacementCasRejectionPreservesIncomingPhysical();
        VerifyReplacementHealthCasRefreshReservesAfterRestore();
        VerifyReplacementPublicationFailureRestoresSameOperation();
        VerifyReplacementNaturalOrganBodyCommittedFreshnessReplay();
        return "real replacement runtime rejects stale CAS input loss, persists pending refreshed output admission, and replays post-CAS publication or natural-organ transfer without rerolling work";
    }

    // Root-owned focused integration witness. Real part/output transactions and
    // save joins; controlled anatomy adapter and temporary effect-bearing clones.
    // This is not a live UI/AI installation or authored enhancement balance test.
    public static string RunReplacementEffectProjectionFocused()
    {
        var profile = AssetDatabase.LoadAssetAtPath<AnatomyProfileSO>(
            "Assets/Resources/SO/Medical/Anatomy/anatomy_humanoid.asset");
        Require(profile != null, "Replacement projection requires the authored humanoid profile.");
        var profiles = new ResourceAnatomyProfileCatalog(new[] { profile });
        var originals = Resources.LoadAll<ItemDefinitionSO>(ItemDefinitionSO.UnifiedResourcePath);
        var clones = new List<ItemDefinitionSO>();
        var effect = ScriptableObject.CreateInstance<GameplayEffectDefinitionSO>();
        try
        {
            effect.Configure(900001, "effect:qa:replacement-work", GameplayEffectTargetIds.WorkSpeed,
                GameplayEffectOperation.Multiply, GameplayEffectProjectionPhase.Multiplicative,
                GameplayEffectSourceKind.SurgicalPart, GameplayEffectStackingPolicy.StackAll, 0f, 10f);
            foreach (string itemId in new[] { "surgery:prosthetic:heart", "surgery:organ:heart" })
            {
                var original = originals.Single(value => value.ItemId == itemId);
                var clone = UnityEngine.Object.Instantiate(original);
                clones.Add(clone);
                clone.SetFeature(new InstalledSurgicalPartEffectItemFeature
                {
                    effects = new List<GameplayEffectBinding>
                    {
                        new() { bindingId = "qa:replacement-work", definition = effect, value = 1.2f }
                    }
                });
            }
            var effectCatalog = new ResourceItemDefinitionCatalog(clones);
            Require(effectCatalog.Validate().Count == 0, "Temporary effect definitions are invalid.");

            foreach (bool incomingTransferFault in new[] { true, false })
            {
                var kind = incomingTransferFault ? SurgicalPartKind.NaturalOrgan : SurgicalPartKind.Prosthetic;
                using var fixture = new ReplacementRuntimeFixture(kind, 60f, incomingTransferFault);
                fixture.Anatomy.ProjectionProfileId = "anatomy:humanoid";
                var source = new CharacterSurgicalPartGameplayEffectSourceQuery(
                    () => fixture.Runtime, effectCatalog, fixture.Anatomy, profiles, fixture.OrderDemand);
                AssertProjected(fixture.PreviousPart.partInstanceId, 1.08f);
                Require(TryReserveReplacementRuntime(fixture.Runtime, fixture.Order, fixture.Anatomy.Node,
                    fixture.OutputPosition, fixture.Admission, out DomainFailure reserveFailure),
                    "Projection fixture reserve failed: " + reserveFailure);
                var publication = new FailOnceReplacementPublication(fixture.Publication)
                {
                    FailPublishOnce = !incomingTransferFault
                };
                Require(!TryCommitReplacementRuntime(fixture.Runtime, fixture.Order, fixture.Actor,
                    kind, 1f, fixture.Anatomy, fixture.Admission, publication, out DomainFailure pendingFailure)
                    && pendingFailure.IsFailure
                    && fixture.Order.replacementPhase == SurgicalPartReplacementPhase.BodyCommitted
                    && fixture.Anatomy.ReplaceCallCount == 1
                    && fixture.IncomingPart.installed == !incomingTransferFault,
                    "Fault did not reach the expected actual BodyCommitted boundary: " + pendingFailure);
                string expectedSource = incomingTransferFault ? null : fixture.IncomingPart.partInstanceId;
                AssertProjected(expectedSource, 1.07f);
                fixture.CaptureValidateCrossJoinAndRestore(profiles);
                AssertProjected(expectedSource, 1.07f);

                // Invalid ownership must not be hidden by the legitimate pending state.
                string subject = fixture.Order.subject.subjectId, node = fixture.Order.targetNodeId;
                string oldPart = fixture.Order.replacementExpectedOldPartId;
                Reject(() => fixture.Order.subject.subjectId = "character:qa:wrong",
                    () => fixture.Order.subject.subjectId = subject);
                Reject(() => fixture.Order.targetNodeId = "arm:left", () => fixture.Order.targetNodeId = node);
                Reject(() => fixture.Order.replacementExpectedOldPartId = "surgical-part:qa:wrong",
                    () => fixture.Order.replacementExpectedOldPartId = oldPart);
                Reject(() => fixture.Order.replacementPhase = SurgicalPartReplacementPhase.OutputReserved,
                    () => fixture.Order.replacementPhase = SurgicalPartReplacementPhase.BodyCommitted);
                Reject(() => fixture.Order.state = SurgeryOrderState.Completed,
                    () => fixture.Order.state = SurgeryOrderState.Procedure);
                var duplicate = JsonUtility.FromJson<SurgeryOrder>(JsonUtility.ToJson(fixture.Order));
                duplicate.orderId = "surgery:qa:duplicate";
                Reject(() => fixture.MutableOrders.Add(duplicate), () => fixture.MutableOrders.Remove(duplicate));
                AssertProjected(expectedSource, 1.07f);

                Require(TryCommitReplacementRuntime(fixture.Runtime, fixture.Order, fixture.Actor,
                    kind, 1f, fixture.Anatomy, fixture.Admission, publication, out DomainFailure retryFailure)
                    && !retryFailure.IsFailure
                    && fixture.Order.replacementPhase == SurgicalPartReplacementPhase.Completed
                    && fixture.Anatomy.ReplaceCallCount == 1
                    && fixture.Repository.GetEditorTestQuantity(fixture.IncomingStackId) == 0
                    && fixture.Publication.CaptureEditorTestSnapshot().Stacks.Count == 1,
                    "Projection retry changed the transaction result: " + retryFailure);
                AssertProjected(fixture.IncomingPart.partInstanceId, 1.07f);
                // Controlled coordinator boundary: SurgeryRuntime advances this
                // only after the real replacement command returned successfully.
                // The detached part-runtime fixture does not run that coordinator.
                fixture.Order.resolvedEffectCount = 1;
                fixture.CaptureValidateCrossJoinAndRestore(profiles);
                AssertProjected(fixture.IncomingPart.partInstanceId, 1.07f);

                void AssertProjected(string expectedId, float expectedValue)
                {
                    string before = fixture.CaptureProjectionStateJson();
                    var projected = source.GetInstalledPartSources(fixture.Actor);
                    Require(projected.Count == (expectedId == null ? 0 : 1), "Pending projection duplicated or lost an effect.");
                    if (expectedId != null)
                    {
                        Require(projected[0].SourceRef.SourceId == expectedId && projected[0].Effects.Count == 1,
                            "Projection used the detached old part or wrong incoming effect.");
                        Require(Mathf.Abs(projected[0].Effects[0].value - expectedValue) < 0.00001f,
                            "Health/efficiency or item quality was applied incorrectly.");
                    }
                    Require(fixture.CaptureProjectionStateJson() == before, "Effect query mutated surgery/body/physical ownership.");
                }

                void Reject(Action corrupt, Action restore)
                {
                    corrupt();
                    try
                    {
                        string before = fixture.CaptureProjectionStateJson();
                        bool rejected = false;
                        try { source.GetInstalledPartSources(fixture.Actor); }
                        catch (InvalidOperationException) { rejected = true; }
                        Require(rejected, "Projection accepted an unrelated, inactive, duplicate or mismatched pending owner.");
                        Require(fixture.CaptureProjectionStateJson() == before,
                            "Rejected effect query mutated surgery/body/physical ownership.");
                    }
                    finally { restore(); }
                }
            }
            return "PASS actual transfer/publication faults -> BodyCommitted projection -> current JSON ownership restore -> retry once; 12 invalid joins rejected; detached anatomy/effect clones, not live UI/AI/maxHP";
        }
        finally
        {
            foreach (var clone in clones) UnityEngine.Object.DestroyImmediate(clone);
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    // Root-owned authored projection witness. Scratch installed records and a
    // controlled anatomy adapter are not a live species surgery or save preflight.
    public static string RunAuthoredInstalledEffectCompositionFocused()
    {
        using var fixture = new ReplacementRuntimeFixture();
        var items = new ResourceItemDefinitionCatalog(Resources.LoadAll<ItemDefinitionSO>(ItemDefinitionSO.UnifiedResourcePath));
        var profiles = new ResourceAnatomyProfileCatalog(LoadAssets<AnatomyProfileSO>("Assets/Resources/SO/Medical/Anatomy"));
        var procedures = Resources.LoadAll<SurgicalProcedureSO>(SurgicalProcedureSO.ResourcePath);
        var cases = new[]
        {
            ("beastkin-sprint-joint", "anatomy:beastkin", "leg:left", GameplayEffectTargetIds.MoveSpeed, 1f, 1.0392304845f, 1.0198039027f),
            ("demon-heat-sac", "anatomy:demon", "heat-sac", GameplayEffectTargetIds.HeatExposure, 1f, .80f, .90f),
            ("orc-combat-heart", "anatomy:orc", "heart", GameplayEffectTargetIds.MaximumHealth, 100f, 110f, 105f),
            ("kobold-tail-balance", "anatomy:kobold", "balance-tail", GameplayEffectTargetIds.EvasionChance, .10f, .13f, .115f),
            ("human-neural-assist", "anatomy:humanoid", "brain", GameplayEffectTargetIds.WorkSpeed, 1f, 1.05f, 1.025f)
        };
        var lines = new List<string>();
        foreach (var test in cases)
        {
            var procedure = procedures.Single(x => x.ProcedureId == "procedure:" + test.Item1);
            Require(procedure.TryGetInstallationEffect(out var installation), "Authored installation missing: " + test.Item1);
            Require(profiles.TryGet(test.Item2, out var profile) && profile.TryGetNode(test.Item3, out _),
                "Authored anatomy node missing: " + test.Item2 + "/" + test.Item3);
            var parts = new List<SurgicalPartInstance>();
            var nodes = new List<AnatomyNodeHealthState>();
            Add(test.Item3);
            var runtime = CreateReplacementProxy<ISurgicalPartRuntime>((method, _) => method.Name == "get_Parts"
                ? parts : throw new InvalidOperationException("Unexpected part query: " + method.Name));
            var anatomy = CreateReplacementProxy<IAnatomyHealthRuntime>((method, _) => method.Name == "GetAnatomySnapshot"
                ? new AnatomyHealthSnapshot(test.Item2, nodes, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f)
                : throw new InvalidOperationException("Unexpected anatomy mutation/query: " + method.Name));
            var demand = CreateReplacementProxy<ISurgeryOrderDemandQuery>((method, _) => method.Name == "get_ActiveOrders"
                ? Array.Empty<SurgeryOrder>() : throw new InvalidOperationException("Unexpected order query: " + method.Name));
            var query = new CharacterSurgicalPartGameplayEffectSourceQuery(() => runtime, items, anatomy, profiles, demand);

            Check(test.Item6, "full authored single slot");
            parts[0].quality = nodes[0].installedPartEfficiency = .5f;
            Check(test.Item7, "quality once");
            parts[0].quality = nodes[0].installedPartEfficiency = 1f;
            nodes[0].currentHealth = 5f;
            Check(test.Item7, "condition once");
            nodes[0].currentHealth = 0f;
            Check(test.Item5, "nonfunctional neutral");
            nodes[0].currentHealth = 10f;

            if (test.Item1 == "beastkin-sprint-joint")
            {
                Add("leg:right"); // Same authored left intrinsic part is pair-compatible.
                Check(1.08f, "complete paired set, not1.1664");
                foreach (var node in nodes) node.installedPartEfficiency = .5f;
                foreach (var part in parts) part.quality = .5f;
                Check(1.04f, "equal half-strength pair");
                nodes[0].installedPartEfficiency = parts[0].quality = 1f;
                Check(1.0598113033f, "mixed strengths sqrt1.08*sqrt1.04");
                nodes[1].installedPartEfficiency = parts[1].quality = 1f;
                var before = CaptureScratch();
                parts = parts.Select(x => JsonUtility.FromJson<SurgicalPartInstance>(JsonUtility.ToJson(x))).ToList();
                nodes = nodes.Select(x => JsonUtility.FromJson<AnatomyNodeHealthState>(JsonUtility.ToJson(x))).ToList();
                Require(CaptureScratch() == before, "Scratch component JSON roundtrip changed state.");
                Check(1.08f, "scratch JSON projection replay");
                nodes[1].installedPartId = string.Empty;
                parts[1].installed = false;
                parts[1].installedSubjectId = string.Empty;
                Check(1.0392304845f, "removed right does not strengthen left");
                parts[0].itemDefinitionId = "surgery:prosthetic:leg:left";
                Check(1f, "ordinary prosthetic gets no enhancement");
            }
            else
            {
                parts[0].installed = false;
                parts[0].installedSubjectId = string.Empty;
                nodes[0].installedPartId = string.Empty;
                Check(test.Item5, "detached physical part contributes zero");
            }
            lines.Add(test.Item1 + "=PASS");

            void Add(string targetNode)
            {
                string partId = "surgical-part:qa:composition:" + parts.Count;
                parts.Add(new SurgicalPartInstance { partInstanceId = partId, itemDefinitionId = installation.requiredItemDefinitionId,
                    nodeId = test.Item3, kind = installation.partKind, installed = true,
                    installedSubjectId = fixture.Actor.Identity.PersistentId, quality = 1f });
                nodes.Add(new AnatomyNodeHealthState { nodeId = targetNode, maxHealth = 10f, currentHealth = 10f,
                    installedPartId = partId, installedPartKind = installation.partKind, installedPartEfficiency = 1f });
            }
            string CaptureScratch() => string.Join("|", parts.Select(x => JsonUtility.ToJson(x)))
                + "/" + string.Join("|", nodes.Select(x => JsonUtility.ToJson(x)));
            void Check(float expected, string label)
            {
                string before = CaptureScratch();
                var sources = query.GetInstalledPartSources(fixture.Actor);
                float actual = CharacterGameplayEffectProjector.Resolve(test.Item4, test.Item5, sources).Value;
                Require(Mathf.Abs(actual - expected) < .0001f,
                    test.Item1 + " / " + label + ": expected=" + expected + "; actual=" + actual);
                Require(before == CaptureScratch(), "Projection mutated source records: " + label);
            }
        }
        return "PASS actual authored5 effect definitions; " + string.Join("; ", lines)
            + "; source/target projection only, scratch ownership/anatomy/JSON; NOT live species eligibility, whole-save or uncontrolled balance";
    }

    // Pass the actual main-scene DI service. This is display projection, not an
    // installation command, species-admission or natural modal-click witness.
    public static string RunAuthoredPartPreviewFocused(CharacterSurgeryWindowService service)
    {
        Require(service != null, "Actual main surgery window service is required.");
        var labelMethod = typeof(CharacterSurgeryWindowService).GetMethod("GetPartLabel",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Require(labelMethod != null, "Production part-label projection is missing.");
        var bodyMethod = typeof(CharacterSurgeryWindowService).GetMethod("AppendSelectedPartPreview",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Require(bodyMethod != null, "Selected-part body disclosure is missing.");
        var profiles = new ResourceAnatomyProfileCatalog(LoadAssets<AnatomyProfileSO>("Assets/Resources/SO/Medical/Anatomy"));
        var pairedProfile = profiles.GetForSpecies("Beastkin");
        Require(pairedProfile.TryGetNode("leg:left", out var pairedNode), "Authored Beastkin leg missing.");
        var scratchSubject = new SurgeryPlanningSubject { Subject = new SurgicalSubjectRef {
            subjectId = "character:qa:preview", speciesId = "Beastkin", anatomyProfileId = "anatomy:beastkin" }, Nodes = pairedProfile.Nodes };
        var cases = new[]
        {
            ("beastkin-sprint-joint", "이동", "×1.08"),
            ("demon-heat-sac", "열기", "×0.8"),
            ("orc-combat-heart", "체력", "×1.1"),
            ("kobold-tail-balance", "회피", "+0.03"),
            ("human-neural-assist", "작업", "×1.05")
        };
        var procedures = Resources.LoadAll<SurgicalProcedureSO>(SurgicalProcedureSO.ResourcePath);
        var labels = new List<string>();
        var priorCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var test in cases)
            {
                var procedure = procedures.Single(x => x.ProcedureId == "procedure:" + test.Item1);
                Require(procedure.TryGetInstallationEffect(out var installation), "Installation definition missing.");
                var part = new SurgicalPartInstance { partInstanceId = "surgical-part:qa:preview",
                    itemDefinitionId = installation.requiredItemDefinitionId, displayName = procedure.DisplayName,
                    kind = installation.partKind, quality = .5f };
                string before = JsonUtility.ToJson(part);
                string label = (string)labelMethod.Invoke(service, new object[] { part });
                Require(label.Contains(test.Item2) && label.Contains(test.Item3)
                    && label.Contains("기준 품질") && label.Contains("완성 구성") && label.Contains("0.50"),
                    "Authored baseline/quality/target meaning missing for " + test.Item1 + ": " + label);
                Require(before == JsonUtility.ToJson(part), "Preview mutated the scratch part.");
                Require(label == (string)labelMethod.Invoke(service, new object[] { part }),
                    "Repeated read changed preview meaning.");
                if (test.Item1 == "beastkin-sprint-joint")
                {
                    part.nodeId = "leg:left";
                    string body = PreviewBody(part);
                    Require(body.Contains("×1.08") && body.Contains("슬롯 2개")
                        && body.Contains("전체 보너스가 적용되지 않습니다") && body.Contains("0.50"),
                        "Paired baseline/single-slot/quality distinction missing from body: " + body);
                }
                labels.Add(label);
            }
            var ordinary = new SurgicalPartInstance { partInstanceId = "surgical-part:qa:ordinary-preview",
                itemDefinitionId = "surgery:prosthetic:leg:left", nodeId = "leg:left", displayName = "ordinary", kind = SurgicalPartKind.Prosthetic, quality = .5f };
            string ordinaryLabel = (string)labelMethod.Invoke(service, new object[] { ordinary });
            Require(!ordinaryLabel.Contains("완성 구성"), "Ordinary prosthetic leaked enhancement preview.");
            Require(!PreviewBody(ordinary).Contains("완성 구성"), "Ordinary details leaked enhancement baseline wording.");
            ordinary.itemDefinitionId = "qa:missing:surgical-preview";
            bool missingRejected = false;
            try { labelMethod.Invoke(service, new object[] { ordinary }); }
            catch (System.Reflection.TargetInvocationException error)
            { missingRejected = error.InnerException is InvalidOperationException || error.InnerException is KeyNotFoundException; }
            Require(missingRejected, "Missing physical definition was silently previewed.");
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = priorCulture; }
        return "PASS main DI service and authored5 preview labels, baseline/quality distinction, stable nonmutating reads, ordinary neutral, missing definition rejected; "
            + string.Join(" | ", labels) + "; scope=display projection with scratch parts, NOT actual modal input or installation";

        string PreviewBody(SurgicalPartInstance part)
        {
            string before = JsonUtility.ToJson(part);
            var text = new System.Text.StringBuilder();
            bodyMethod.Invoke(service, new object[] { text, scratchSubject, pairedNode, part });
            Require(JsonUtility.ToJson(part) == before, "Details projection mutated the scratch part.");
            return text.ToString();
        }
    }

    private static void VerifyReplacementCasRejectionPreservesIncomingPhysical()
    {
        using ReplacementRuntimeFixture fixture = new();
        SurgeryOrder order = fixture.Order;
        Require(
            TryReserveReplacementRuntime(
                fixture.Runtime,
                order,
                fixture.Anatomy.Node,
                fixture.OutputPosition,
                fixture.Admission,
                out DomainFailure reserveFailure)
            && !reserveFailure.IsFailure
            && order.replacementPhase ==
                SurgicalPartReplacementPhase.OutputReserved,
            "replacement runtime could not reserve its output before the CAS rejection fixture: "
            + reserveFailure);

        string admissionTokenId = order.replacementAdmissionTokenId;
        fixture.Anatomy.ForceInstalledPartForCasRace("surgical-part:race");
        Require(
            !TryCommitReplacementRuntime(
                fixture.Runtime,
                order,
                fixture.Actor,
                SurgicalPartKind.Prosthetic,
                1f,
                fixture.Anatomy,
                fixture.Admission,
                fixture.Publication,
                out DomainFailure failure)
            && failure.IsFailure
            && order.replacementPhase ==
                SurgicalPartReplacementPhase.OutputReserved
            && fixture.IncomingPart.installed == false
            && fixture.IncomingPart.reservedOrderId == order.orderId
            && fixture.IncomingPart.worldStackId == fixture.IncomingStackId
            && fixture.Repository.GetEditorTestQuantity(
                fixture.IncomingStackId) == 1
            && fixture.Repository.GetEditorPendingBatchDispositionCount() == 0
            && string.Equals(
                order.replacementAdmissionTokenId,
                admissionTokenId,
                StringComparison.Ordinal)
            && fixture.Admission.TryValidatePlannedOutputReservation(
                fixture.GetReplacementToken(),
                out _,
                out _),
            "replacement body CAS rejection consumed its incoming physical item or reservation");
    }

    private static void VerifyReplacementPublicationFailureRestoresSameOperation()
    {
        using ReplacementRuntimeFixture fixture = new();
        SurgeryOrder order = fixture.Order;
        Require(
            TryReserveReplacementRuntime(
                fixture.Runtime,
                order,
                fixture.Anatomy.Node,
                fixture.OutputPosition,
                fixture.Admission,
                out DomainFailure reserveFailure)
            && !reserveFailure.IsFailure
            && order.replacementPhase ==
                SurgicalPartReplacementPhase.OutputReserved,
            "replacement runtime did not enter OutputReserved: " + reserveFailure);

        string frozenOperationId = order.replacementOperationId;
        string frozenPublicationId = order.replacementPublicationOperationId;
        string frozenBatchCommitId = order.replacementBatchCommitId;
        string frozenAdmissionTokenId = order.replacementAdmissionTokenId;
        string frozenOutcomeFingerprint = order.replacementOutcomeFingerprint;
        string frozenPlannedFingerprint = order.replacementPlannedOutputFingerprint;
        FailOnceReplacementPublication publishFailure = new(
            fixture.Publication)
        {
            FailPublishOnce = true
        };
        Require(
            !TryCommitReplacementRuntime(
                fixture.Runtime,
                order,
                fixture.Actor,
                SurgicalPartKind.Prosthetic,
                1f,
                fixture.Anatomy,
                fixture.Admission,
                publishFailure,
                out DomainFailure publicationFailure)
            && publicationFailure.IsFailure
            && order.replacementPhase ==
                SurgicalPartReplacementPhase.BodyCommitted
            && fixture.Anatomy.ReplaceCallCount == 1
            && fixture.Anatomy.Node.installedPartId
                == fixture.IncomingPart.partInstanceId
            && fixture.IncomingPart.installed
            && fixture.Repository.GetEditorTestQuantity(
                fixture.IncomingStackId) == 0
            && fixture.Publication.CaptureEditorTestSnapshot().Stacks.Count == 0,
            "post-CAS replacement publication failure did not retain the body-committed retry state");

        DungeonSurgerySaveData persisted = fixture.CaptureValidateCrossJoinAndRestore();
        order = fixture.Order;
        Require(
            order.replacementPhase == SurgicalPartReplacementPhase.BodyCommitted
            && string.Equals(
                order.replacementOperationId,
                frozenOperationId,
                StringComparison.Ordinal)
            && string.Equals(
                order.replacementPublicationOperationId,
                frozenPublicationId,
                StringComparison.Ordinal)
            && string.Equals(
                order.replacementBatchCommitId,
                frozenBatchCommitId,
                StringComparison.Ordinal)
            && string.Equals(
                order.replacementOutcomeFingerprint,
                frozenOutcomeFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                order.replacementPlannedOutputFingerprint,
                frozenPlannedFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                persisted.orders.Single(candidate => string.Equals(
                    candidate.orderId,
                    order.orderId,
                    StringComparison.Ordinal)).replacementOutcomeFingerprint,
                frozenOutcomeFingerprint,
                StringComparison.Ordinal),
            "replacement save/restore changed the frozen operation or outcome fingerprint");

        fixture.ReconstructOutputServices(
            out FacilityBufferMassAdmissionService restoredAdmission,
            out FacilityBufferPlannedOutputPublicationService restoredPublication);
        Require(
            !restoredAdmission.TryGetPlannedOutputToken(
                frozenAdmissionTokenId,
                out _,
                out _),
            "replacement fixture carried its pre-restore admission token into a fresh service");

        FailOnceReplacementPublication acknowledgementFailure = new(
            restoredPublication)
        {
            FailAcknowledgementOnce = true
        };
        InstallSurgicalPartEffectHandler replacementHandler =
            fixture.CreateReplacementEffectHandler(
                restoredAdmission,
                acknowledgementFailure);
        InstallSurgicalPartEffect installEffect = new()
        {
            partKind = SurgicalPartKind.Prosthetic,
            efficiency = 1f
        };
        Require(
            !replacementHandler.Apply(
                order,
                installEffect,
                null,
                out DomainFailure acknowledgementResult)
            && acknowledgementResult.IsFailure
            && order.replacementPhase ==
                SurgicalPartReplacementPhase.OutputPublished
            && fixture.Anatomy.ReplaceCallCount == 1
            && restoredPublication.CaptureEditorTestSnapshot().Stacks.Count == 1
            && restoredAdmission.TryGetPlannedOutputToken(
                order.replacementAdmissionTokenId,
                out _,
                out FacilityBufferMassAdmissionTokenStatus restoredTokenStatus)
            && restoredTokenStatus == FacilityBufferMassAdmissionTokenStatus.Routed,
            "replacement runtime did not retain OutputPublished after the exact publication acknowledgement fault");

        string reconstructedAdmissionTokenId = order.replacementAdmissionTokenId;

        Require(
            replacementHandler.Apply(
                order,
                installEffect,
                null,
                out DomainFailure retryFailure)
            && !retryFailure.IsFailure
            && order.replacementPhase ==
                SurgicalPartReplacementPhase.Completed
            && fixture.Anatomy.ReplaceCallCount == 1
            && restoredPublication.CaptureEditorTestSnapshot().Stacks.Count == 1
            && string.Equals(
                order.replacementOperationId,
                frozenOperationId,
                StringComparison.Ordinal)
            && string.Equals(
                order.replacementPublicationOperationId,
                frozenPublicationId,
                StringComparison.Ordinal)
            && string.Equals(
                order.replacementBatchCommitId,
                frozenBatchCommitId,
                StringComparison.Ordinal)
            && string.Equals(
                order.replacementAdmissionTokenId,
                reconstructedAdmissionTokenId,
                StringComparison.Ordinal)
            && string.Equals(
                order.replacementOutcomeFingerprint,
                frozenOutcomeFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                order.replacementPlannedOutputFingerprint,
                frozenPlannedFingerprint,
                StringComparison.Ordinal),
            "replacement retry did not complete the same frozen operation: "
            + retryFailure);

        // The runtime owns the phase transition. The effect handler advances
        // this count only after the command returns successfully.
        order.resolvedEffectCount = 1;
        fixture.CaptureValidateCrossJoinAndRestore();
    }

    private static void VerifyReplacementHealthCasRefreshReservesAfterRestore()
    {
        using ReplacementRuntimeFixture fixture = new();
        SurgeryOrder order = fixture.Order;
        Require(
            TryReserveReplacementRuntime(
                fixture.Runtime,
                order,
                fixture.Anatomy.Node,
                fixture.OutputPosition,
                fixture.Admission,
                out DomainFailure reserveFailure)
            && !reserveFailure.IsFailure,
            "replacement refresh fixture could not reserve its initial output: "
            + reserveFailure);

        string frozenOperationId = order.replacementOperationId;
        string frozenBatchCommitId = order.replacementBatchCommitId;
        string expectedOldPartId = order.replacementExpectedOldPartId;
        string incomingPartId = order.replacementIncomingPartId;
        int initialAttempt = order.replacementReservationAttempt;
        string refreshedPublicationId = SurgicalPartReplacementIdentity
            .FormatPublicationOperationId(order.orderId, initialAttempt + 1);
        fixture.Anatomy.SetCurrentHealthForReplacementRefresh(24f);
        bool failNextReservation = true;
        IFacilityBufferMassAdmissionService reserveFailureAdmission =
            CreateReplacementProxy<IFacilityBufferMassAdmissionService>(
                (method, arguments) =>
                {
                    if (failNextReservation
                        && method.Name == "TryReservePlannedOutput")
                    {
                        failNextReservation = false;
                        arguments[1] = default(FacilityBufferPlannedOutputToken);
                        arguments[2] = FacilityBufferMassAdmissionFailureCode
                            .CapacityUnavailable;
                        arguments[3] = "qa-replacement-refresh-reservation-failure";
                        return false;
                    }

                    return method.Invoke(fixture.Admission, arguments);
                });
        Require(
            !TryCommitReplacementRuntime(
                fixture.Runtime,
                order,
                fixture.Actor,
                SurgicalPartKind.Prosthetic,
                1f,
                fixture.Anatomy,
                reserveFailureAdmission,
                fixture.Publication,
                out DomainFailure refreshFailure)
            && refreshFailure.IsFailure
            && !failNextReservation
            && order.replacementPhase ==
                SurgicalPartReplacementPhase.OutputReservationPending
            && order.replacementReservationAttempt == initialAttempt + 1
            && string.Equals(
                order.replacementOperationId,
                frozenOperationId,
                StringComparison.Ordinal)
            && string.Equals(
                order.replacementBatchCommitId,
                frozenBatchCommitId,
                StringComparison.Ordinal)
            && string.Equals(
                order.replacementPublicationOperationId,
                refreshedPublicationId,
                StringComparison.Ordinal)
            && string.Equals(order.replacementExpectedOldPartId, expectedOldPartId,
                StringComparison.Ordinal)
            && string.Equals(order.replacementIncomingPartId, incomingPartId,
                StringComparison.Ordinal)
            && order.replacementOutputX == fixture.OutputPosition.x
            && order.replacementOutputY == fixture.OutputPosition.y
            && order.replacementDetachedCurrentHealth == 24f
            && order.replacementDetachedMaxHealth == 80f
            && string.IsNullOrEmpty(order.replacementAdmissionTokenId)
            && string.IsNullOrEmpty(order.replacementOutcomeFingerprint)
            && string.IsNullOrEmpty(order.replacementPlannedOutputFingerprint)
            && string.IsNullOrEmpty(order.replacementOutputStackId)
            && string.IsNullOrEmpty(order.replacementOutputItemInstanceId)
            && order.replacementOutputMassGrams == 0L
            && order.resultRolled
            && order.resultSucceeded
            && order.resultOutcomeId == "success"
            && order.resolvedEffectCount == 0
            && fixture.Anatomy.ReplaceCallCount == 1
            && fixture.Repository.GetEditorTestQuantity(
                fixture.IncomingStackId) == 1,
            "health-CAS refresh did not preserve a reservation-pending replacement intent");

        DungeonSurgerySaveData persisted =
            fixture.CaptureValidateCrossJoinAndRestore();
        order = fixture.Order;
        Require(
            persisted.orders.Single(candidate => string.Equals(
                candidate.orderId,
                order.orderId,
                StringComparison.Ordinal)).replacementPhase ==
                SurgicalPartReplacementPhase.OutputReservationPending
            && order.replacementPhase ==
                SurgicalPartReplacementPhase.OutputReservationPending
            && string.Equals(
                order.replacementOperationId,
                frozenOperationId,
                StringComparison.Ordinal)
            && string.Equals(
                order.replacementBatchCommitId,
                frozenBatchCommitId,
                StringComparison.Ordinal)
            && order.replacementReservationAttempt == initialAttempt + 1
            && string.Equals(
                order.replacementPublicationOperationId,
                refreshedPublicationId,
                StringComparison.Ordinal)
            && string.IsNullOrEmpty(order.replacementAdmissionTokenId)
            && string.IsNullOrEmpty(order.replacementOutcomeFingerprint)
            && string.IsNullOrEmpty(order.replacementPlannedOutputFingerprint),
            "replacement save/restore changed its reservation-pending intent");

        fixture.ReconstructOutputServices(
            out FacilityBufferMassAdmissionService restoredAdmission,
            out FacilityBufferPlannedOutputPublicationService restoredPublication);
        InstallSurgicalPartEffectHandler replacementHandler =
            fixture.CreateReplacementEffectHandler(
                restoredAdmission,
                restoredPublication);
        InstallSurgicalPartEffect installEffect = new()
        {
            partKind = SurgicalPartKind.Prosthetic,
            efficiency = 1f
        };
        Require(
            replacementHandler.Apply(
                order,
                installEffect,
                null,
                out DomainFailure retryFailure)
            && !retryFailure.IsFailure
            && order.replacementPhase ==
                SurgicalPartReplacementPhase.Completed
            && order.replacementReservationAttempt == initialAttempt + 1
            && string.Equals(
                order.replacementOperationId,
                frozenOperationId,
                StringComparison.Ordinal)
            && string.Equals(
                order.replacementBatchCommitId,
                frozenBatchCommitId,
                StringComparison.Ordinal)
            && string.Equals(
                order.replacementPublicationOperationId,
                refreshedPublicationId,
                StringComparison.Ordinal)
            && order.resultRolled
            && order.resultSucceeded
            && order.resultOutcomeId == "success"
            && order.resolvedEffectCount == 0
            && fixture.Anatomy.ReplaceCallCount == 2
            && fixture.Repository.GetEditorTestQuantity(
                fixture.IncomingStackId) == 0,
            "reservation-pending replacement did not re-reserve and complete through the effect command: "
            + retryFailure);

        order.resolvedEffectCount = 1;
        fixture.CaptureValidateCrossJoinAndRestore();
    }

    private static void VerifyReplacementNaturalOrganBodyCommittedFreshnessReplay()
    {
        InstallSurgicalPartEffect installEffect = new()
        {
            partKind = SurgicalPartKind.NaturalOrgan,
            efficiency = 1f
        };
        using (ReplacementRuntimeFixture preCas = new(
                   SurgicalPartKind.NaturalOrgan,
                   incomingFreshnessSeconds: 60f))
        {
            Require(
                TryReserveReplacementRuntime(
                    preCas.Runtime,
                    preCas.Order,
                    preCas.Anatomy.Node,
                    preCas.OutputPosition,
                    preCas.Admission,
                    out DomainFailure preCasReserveFailure)
                && !preCasReserveFailure.IsFailure,
                "natural-organ pre-CAS fixture could not reserve output: "
                + preCasReserveFailure);
            preCas.IncomingPart.freshnessSeconds = 0f;
            InstallSurgicalPartEffectHandler preCasHandler =
                preCas.CreateReplacementEffectHandler(
                    preCas.Admission,
                    preCas.Publication);
            Require(
                !preCasHandler.Apply(
                    preCas.Order,
                    installEffect,
                    null,
                    out DomainFailure staleFailure)
                && staleFailure.IsFailure
                && preCas.Order.replacementPhase ==
                    SurgicalPartReplacementPhase.OutputReserved
                && preCas.Anatomy.ReplaceCallCount == 0
                && preCas.Repository.GetEditorTestQuantity(
                    preCas.IncomingStackId) == 1,
                "pre-CAS stale natural organ was accepted or consumed");
        }

        using ReplacementRuntimeFixture fixture = new(
            SurgicalPartKind.NaturalOrgan,
            incomingFreshnessSeconds: 60f,
            failFirstIncomingTransfer: true);
        SurgeryOrder order = fixture.Order;
        Require(
            TryReserveReplacementRuntime(
                fixture.Runtime,
                order,
                fixture.Anatomy.Node,
                fixture.OutputPosition,
                fixture.Admission,
                out DomainFailure reserveFailure)
            && !reserveFailure.IsFailure,
            "natural-organ transfer replay fixture could not reserve output: "
            + reserveFailure);
        string frozenOperationId = order.replacementOperationId;
        string frozenPublicationId = order.replacementPublicationOperationId;
        string frozenBatchCommitId = order.replacementBatchCommitId;
        InstallSurgicalPartEffectHandler replacementHandler =
            fixture.CreateReplacementEffectHandler(
                fixture.Admission,
                fixture.Publication);
        Require(
            !replacementHandler.Apply(
                order,
                installEffect,
                null,
                out DomainFailure transferFailure)
            && transferFailure.IsFailure
            && order.replacementPhase ==
                SurgicalPartReplacementPhase.BodyCommitted
            && fixture.Anatomy.ReplaceCallCount == 1
            && !fixture.IncomingPart.installed
            && string.IsNullOrEmpty(fixture.IncomingPart.installationOrderId)
            && string.IsNullOrEmpty(fixture.IncomingPart.installationOperationId)
            && string.IsNullOrEmpty(fixture.IncomingPart.installationCommitId)
            && string.IsNullOrEmpty(fixture.IncomingPart.installationSourceStackId)
            && string.IsNullOrEmpty(fixture.IncomingPart.installationSubjectId)
            && fixture.Repository.GetEditorTestQuantity(
                fixture.IncomingStackId) == 1,
            "post-CAS natural-organ transfer fault did not leave a clean replayable body commit");

        fixture.Runtime.TickFreshness(120f);
        Require(
            order.replacementPhase ==
                SurgicalPartReplacementPhase.BodyCommitted
            && string.Equals(
                order.replacementIncomingPartId,
                fixture.IncomingPart.partInstanceId,
                StringComparison.Ordinal)
            && fixture.IncomingPart.worldStackId == fixture.IncomingStackId
            && fixture.Repository.GetEditorTestQuantity(
                fixture.IncomingStackId) == 1
            && fixture.IncomingPart.freshnessSeconds == 60f,
            "freshness ticking expired the exact BodyCommitted natural-organ transfer input");

        fixture.CaptureValidateCrossJoinAndRestore();
        order = fixture.Order;
        fixture.IncomingPart.freshnessSeconds = 0f;
        Require(
            replacementHandler.Apply(
                order,
                installEffect,
                null,
                out DomainFailure retryFailure)
            && !retryFailure.IsFailure
            && order.replacementPhase ==
                SurgicalPartReplacementPhase.Completed
            && fixture.Anatomy.ReplaceCallCount == 1
            && fixture.IncomingPart.installed
            && fixture.Repository.GetEditorTestQuantity(
                fixture.IncomingStackId) == 0
            && string.Equals(
                order.replacementOperationId,
                frozenOperationId,
                StringComparison.Ordinal)
            && string.Equals(
                order.replacementPublicationOperationId,
                frozenPublicationId,
                StringComparison.Ordinal)
            && string.Equals(
                order.replacementBatchCommitId,
                frozenBatchCommitId,
                StringComparison.Ordinal),
            "BodyCommitted stale natural organ did not replay its same transfer: "
            + retryFailure);

        order.resolvedEffectCount = 1;
        fixture.CaptureValidateCrossJoinAndRestore();
    }

    private static string VerifySurgeryFacilitySingleOwnerSaveJoin()
    {
        SurgeryOrder owner = new()
        {
            orderId = "surgery:1",
            facilityId = "facility:single-owner",
            materialDestinationId = "surgery-materials:surgery:1",
            materialBufferCapacityGrams = 1000L,
            materialMassAuthorityRevision = 1L,
            materialCapacityFingerprint = new string('b', 64),
            state = SurgeryOrderState.PatientWaiting
        };
        SurgeryOrder queued = new()
        {
            orderId = "surgery:2",
            facilityId = owner.facilityId,
            state = SurgeryOrderState.PatientWaiting
        };
        DungeonPhysicalItemSaveData physical = new();
        DungeonCharacterBodyHealthSaveData body = new();
        DungeonSurgerySaveData surgery = new()
        {
            orders = new List<SurgeryOrder> { owner, queued }
        };
        SurgicalPartProductionOutputCrossAggregateSaveValidation
            .ValidatePartOwnership(physical, surgery, body);
        queued.materialDestinationId = "surgery-materials:surgery:2";
        queued.materialBufferCapacityGrams = 1000L;
        queued.materialMassAuthorityRevision = 1L;
        queued.materialCapacityFingerprint = new string('c', 64);
        bool rejected = false;
        try
        {
            SurgicalPartProductionOutputCrossAggregateSaveValidation
                .ValidatePartOwnership(physical, surgery, body);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected,
            "save join accepted two active material owners for one facility");
        return "one facility keeps one material authority while later surgery remains an empty queued owner";
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
            state = SurgeryOrderState.Cancelled
        });
        canonicalNumericIds.parts.Add(new SurgicalPartInstance
        {
            partInstanceId = "surgical-part:1",
            itemDefinitionId = "medical:organ-heart",
            physicalItemInstanceId = "item-instance:surgery-contract",
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

        DungeonSurgerySaveData unrolledCancelled = CloneSaveData(
            canonicalNumericIds);
        unrolledCancelled.orders[0].state = SurgeryOrderState.Cancelled;
        DungeonGameRestoreReport unrolledCancelledReport = new();
        SurgerySaveValidation.Validate(
            unrolledCancelled,
            procedures,
            anatomyProfiles,
            unrolledCancelledReport);
        Require(
            unrolledCancelledReport.Success,
            "unrolled cancelled surgery was rejected: "
                + string.Join(" | ", unrolledCancelledReport.Errors));

        DungeonSurgerySaveData unrolledFailed = CloneSaveData(
            canonicalNumericIds);
        unrolledFailed.orders[0].state = SurgeryOrderState.Failed;
        DungeonGameRestoreReport unrolledFailedReport = new();
        SurgerySaveValidation.Validate(
            unrolledFailed,
            procedures,
            anatomyProfiles,
            unrolledFailedReport);
        Require(
            !unrolledFailedReport.Success
            && unrolledFailedReport.Errors.Any(error => error.Contains(
                "Unrolled surgery order 'surgery:1' entered a clinical failure state.",
                StringComparison.Ordinal)),
            "unrolled failed surgery was not rejected by outcome validation");

        DungeonSurgerySaveData unrolledTerminalFailed = CloneSaveData(
            canonicalNumericIds);
        unrolledTerminalFailed.orders[0].state =
            SurgeryOrderState.TerminalDraining;
        unrolledTerminalFailed.orders[0].materialTerminalTargetState =
            SurgeryOrderState.Failed;
        DungeonGameRestoreReport unrolledTerminalFailedReport = new();
        SurgerySaveValidation.Validate(
            unrolledTerminalFailed,
            procedures,
            anatomyProfiles,
            unrolledTerminalFailedReport);
        Require(
            !unrolledTerminalFailedReport.Success
            && unrolledTerminalFailedReport.Errors.Any(error => error.Contains(
                "Unrolled surgery order 'surgery:1' entered a clinical failure state.",
                StringComparison.Ordinal)),
            "unrolled terminal-to-failed surgery was not rejected by outcome validation");

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

        return "strict V6 accepts canonical cancellation and rejects legacy, unrolled clinical failure, unknown status, missing, sequence, duplicate, and noncanonical numeric ID corruption";
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
                    state = SurgeryOrderState.Cancelled
                }
            },
            parts = new List<SurgicalPartInstance>
            {
                new()
                {
                    partInstanceId = maximumPartId,
                    itemDefinitionId = "medical:organ-heart",
                    physicalItemInstanceId =
                        "item-instance:surgery-sequence-limit",
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

    private static bool TryReserveReplacementRuntime(
        SurgicalPartRuntime runtime,
        SurgeryOrder order,
        AnatomyNodeHealthState node,
        Vector2Int outputPosition,
        IFacilityBufferMassAdmissionService admission,
        out DomainFailure failure)
    {
        object[] arguments =
        {
            order,
            node,
            outputPosition,
            admission,
            DomainFailure.None
        };
        bool succeeded = (bool)InvokeReplacementRuntime(
            runtime,
            "TryReserveReplacementOutput",
            arguments);
        failure = arguments[4] is DomainFailure result
            ? result
            : DomainFailure.None;
        return succeeded;
    }

    private static bool TryCommitReplacementRuntime(
        SurgicalPartRuntime runtime,
        SurgeryOrder order,
        CharacterActor actor,
        SurgicalPartKind kind,
        float efficiency,
        IAnatomyHealthRuntime anatomy,
        IFacilityBufferMassAdmissionService admission,
        IFacilityBufferPlannedOutputPublicationService publication,
        out DomainFailure failure)
    {
        object[] arguments =
        {
            order,
            actor,
            kind,
            efficiency,
            anatomy,
            admission,
            publication,
            DomainFailure.None
        };
        bool succeeded = (bool)InvokeReplacementRuntime(
            runtime,
            "TryCommitReplacement",
            arguments);
        failure = arguments[7] is DomainFailure result
            ? result
            : DomainFailure.None;
        return succeeded;
    }

    private static object InvokeReplacementRuntime(
        SurgicalPartRuntime runtime,
        string methodName,
        object[] arguments)
    {
        MethodInfo method = typeof(SurgicalPartRuntime)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name.EndsWith(
                "." + methodName,
                StringComparison.Ordinal));
        try
        {
            return method.Invoke(runtime, arguments);
        }
        catch (TargetInvocationException exception)
        {
            throw new InvalidOperationException(
                "Replacement runtime invocation failed: " + methodName,
                exception.InnerException ?? exception);
        }
    }

    private sealed class ReplacementRuntimeFixture : IDisposable
    {
        private const string PreviousInstallationOrderId = "surgery:1";
        internal const string OrderId = "surgery:2";
        internal const string SubjectId = "character:replacement-runtime";
        private const string FacilityId = "facility:replacement-runtime";
        private const string DestinationId = "surgery-materials:surgery:2";
        private const string NodeId = "heart";
        private const string ProstheticItemId = "surgery:prosthetic:heart";
        private const string NaturalOrganItemId = "surgery:organ:heart";
        private const string PreviousPartId = "surgical-part:1";
        private const string IncomingPartId = "surgical-part:2";
        private const string PreviousPhysicalId =
            "item-instance:replacement-runtime-old";
        private const string IncomingPhysicalId =
            "item-instance:replacement-runtime-incoming";

        private readonly GameObject actorObject;
        private readonly SurgeryAggregateStateStore stateStore;
        private readonly IDungeonItemCatalogProvider catalog;
        private readonly IPhysicalItemMassQuery mass;
        private readonly WorldItemQueryService query;

        internal ReplacementRuntimeFixture(
            SurgicalPartKind incomingKind = SurgicalPartKind.Prosthetic,
            float incomingFreshnessSeconds = 0f,
            bool failFirstIncomingTransfer = false)
        {
            IItemDefinitionCatalog itemDefinitions =
                new ResourceItemDefinitionCatalog(
                    Resources.LoadAll<ItemDefinitionSO>(
                        ItemDefinitionSO.UnifiedResourcePath));
            catalog =
                new ResourceDungeonItemCatalogProvider(itemDefinitions);
            mass = new PhysicalItemMassQuery(catalog);
            Repository = new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            query = new WorldItemQueryService(
                catalog,
                mass,
                Repository,
                EditorNullItemMarkerPresenter.Instance);
            BatchDispositions = new PhysicalItemBatchDispositionService(
                Repository,
                mass,
                EditorNullItemMarkerPresenter.Instance);
            FailOnceReplacementBatchDisposition incomingTransferFault =
                failFirstIncomingTransfer
                    ? new FailOnceReplacementBatchDisposition(
                        BatchDispositions)
                    : null;
            IPhysicalItemBatchDispositionService runtimeBatchDispositions =
                BatchDispositions;
            if (incomingTransferFault != null)
            {
                runtimeBatchDispositions = incomingTransferFault;
            }
            string incomingItemId = incomingKind == SurgicalPartKind.NaturalOrgan
                ? NaturalOrganItemId
                : ProstheticItemId;
            OutputPosition = new Vector2Int(17, 8);
            FacilityBufferDestinationClaimRegistry claims = new();
            Admission = CreateOutputAdmission(claims);
            Publication = new FacilityBufferPlannedOutputPublicationService(
                Repository,
                catalog,
                mass,
                Admission);
            stateStore = new SurgeryAggregateStateStore(
                new DungeonRuntimeAggregateRootStore());
            IWorldItemStackRuntime items = CreateReplacementProxy<
                IWorldItemStackRuntime>((method, _) => method.Name
                    == "GetAllStacks"
                    ? query.GetAllStacks()
                    : DefaultReplacementValue(method.ReturnType));
            IBuildingWorldQuery buildings = CreateReplacementProxy<
                IBuildingWorldQuery>((method, _) => method.Name
                    == "get_Buildings"
                    ? Array.Empty<BuildableObject>()
                    : DefaultReplacementValue(method.ReturnType));
            ISurgicalFacilityQuery facilities = CreateReplacementProxy<
                ISurgicalFacilityQuery>((method, _) =>
                DefaultReplacementValue(method.ReturnType));
            IEnvironmentalFieldQuery environment = CreateReplacementProxy<
                IEnvironmentalFieldQuery>((method, _) =>
                DefaultReplacementValue(method.ReturnType));
            IAnatomyProfileCatalog profiles = CreateReplacementProxy<
                IAnatomyProfileCatalog>((method, _) =>
                DefaultReplacementValue(method.ReturnType));
            IGameClock clock = CreateReplacementProxy<IGameClock>((method, _) =>
                DefaultReplacementValue(method.ReturnType));
            Runtime = new SurgicalPartRuntime(
                items,
                CreateReplacementProxy<IItemTransferService>((method, _) =>
                    DefaultReplacementValue(method.ReturnType)),
                buildings,
                facilities,
                environment,
                profiles,
                clock,
                stateStore,
                runtimeBatchDispositions,
                new PhysicalItemTransformService(
                    Repository,
                    new WorldItemSpawner(
                        catalog,
                        Repository,
                        EditorNullItemMarkerPresenter.Instance),
                    mass,
                    catalog,
                    EditorNullItemMarkerPresenter.Instance,
                    new FixedGameSessionStateProvider(),
                    new MigratedProducerOutcomeEditorFixture(
                        "run:surgery-physical-transform").Transaction),
                itemDefinitions,
                mass,
                claims,
                Admission,
                new FacilityBufferDestinationLifecycleService(
                    claims,
                    claims,
                    Admission,
                    Admission),
                ReplacementNoopRelease.Instance);

            actorObject = CharacterAiPlanDebugFixtures.CreateActorObject(
                "Surgery Replacement Runtime Fixture");
            Actor = actorObject.GetComponent<CharacterActor>();
            Require(
                Actor != null && Actor.Identity != null,
                "replacement fixture could not construct a character actor identity");
            Actor.Identity.SetPersistentId(SubjectId);
            Anatomy = new ReplacementAnatomyRuntime(
                NodeId,
                PreviousPartId);
            IncomingStackId = WorldItemRepositoryEditorAccess.AddStack(
                Repository,
                incomingItemId,
                1,
                WorldItemStackState.Loose,
                position: new Vector2Int(16, 8),
                itemInstanceId: IncomingPhysicalId);
            string previousStackId = WorldItemRepositoryEditorAccess.AddStack(
                Repository,
                ProstheticItemId,
                1,
                WorldItemStackState.Loose,
                position: new Vector2Int(15, 8),
                itemInstanceId: PreviousPhysicalId);
            stateStore.State.PartSequence = 2;
            stateStore.State.OrderSequence = 2;
            stateStore.State.Parts.Add(new SurgicalPartInstance
            {
                partInstanceId = PreviousPartId,
                itemDefinitionId = ProstheticItemId,
                physicalItemInstanceId = PreviousPhysicalId,
                kind = SurgicalPartKind.Prosthetic,
                nodeId = NodeId,
                displayName = "replacement fixture old heart",
                quality = 0.8f,
                worldStackId = previousStackId,
                reservedOrderId = PreviousInstallationOrderId
            });
            stateStore.State.Parts.Add(new SurgicalPartInstance
            {
                partInstanceId = IncomingPartId,
                itemDefinitionId = incomingItemId,
                physicalItemInstanceId = IncomingPhysicalId,
                kind = incomingKind,
                nodeId = NodeId,
                displayName = "replacement fixture incoming heart",
                quality = 1f,
                freshnessSeconds = incomingFreshnessSeconds,
                worldStackId = IncomingStackId,
                reservedOrderId = OrderId
            });
            stateStore.State.Orders.Add(new SurgeryOrder
            {
                orderId = PreviousInstallationOrderId,
                procedureId = "procedure:prosthetic-installation",
                subject = new SurgicalSubjectRef
                {
                    kind = SurgicalSubjectKind.Character,
                    subjectId = SubjectId
                },
                targetNodeId = NodeId,
                selectedPartInstanceId = PreviousPartId,
                state = SurgeryOrderState.Completed,
                resultRolled = true,
                resultSucceeded = true,
                resultOutcomeId = "success",
                resolvedEffectCount = 1
            });
            Require(
                Runtime.TryConsumeForInstallation(
                    PreviousPartId,
                    PreviousInstallationOrderId,
                    SubjectId,
                    out SurgicalPartInstance installedPrevious,
                    out DomainFailure previousInstallationFailure)
                && !previousInstallationFailure.IsFailure
                && installedPrevious.installed
                && string.Equals(
                    installedPrevious.installedSubjectId,
                    SubjectId,
                    StringComparison.Ordinal)
                && Repository.GetEditorTestQuantity(previousStackId) == 0,
                "replacement fixture could not create its historical installation receipt: "
                + previousInstallationFailure);
            if (incomingTransferFault != null)
            {
                incomingTransferFault.FailTransferPendingOnce = true;
            }
            SurgeryOrder order = new()
            {
                orderId = OrderId,
                procedureId = "procedure:prosthetic-installation",
                subject = new SurgicalSubjectRef
                {
                    kind = SurgicalSubjectKind.Character,
                    subjectId = SubjectId
                },
                targetNodeId = NodeId,
                selectedPartInstanceId = IncomingPartId,
                replacementExpectedOldPartId = PreviousPartId,
                facilityId = FacilityId,
                materialDestinationId = DestinationId,
                materialBufferCapacityGrams = 300000L,
                materialMassAuthorityRevision = mass.AuthorityRevision,
                state = SurgeryOrderState.Procedure,
                resultRolled = true,
                resultSucceeded = true,
                resultOutcomeId = "success",
                resolvedEffectCount = 0
            };
            order.materialCapacityFingerprint =
                SurgeryMaterialCapacityFingerprint.Create(order);
            stateStore.State.Orders.Add(order);
        }

        internal SurgicalPartRuntime Runtime { get; }
        internal ISurgeryOrderDemandQuery OrderDemand => stateStore;
        internal List<SurgeryOrder> MutableOrders => stateStore.State.Orders;
        internal string CaptureProjectionStateJson() =>
            JsonUtility.ToJson(new SurgeryPersistence(stateStore).Capture()) + "|"
            + JsonUtility.ToJson(CaptureBodySave()) + "|" + JsonUtility.ToJson(CapturePhysicalSave());
        internal CharacterActor Actor { get; }
        internal ReplacementAnatomyRuntime Anatomy { get; }
        internal WorldItemRepository Repository { get; }
        internal PhysicalItemBatchDispositionService BatchDispositions { get; }
        internal FacilityBufferMassAdmissionService Admission { get; }
        internal FacilityBufferPlannedOutputPublicationService Publication { get; }
        internal Vector2Int OutputPosition { get; }
        internal string IncomingStackId { get; }
        internal SurgeryOrder Order => stateStore.State.Orders.Single(
            order => string.Equals(
                order.orderId,
                OrderId,
                StringComparison.Ordinal));
        internal SurgicalPartInstance PreviousPart => stateStore.State.Parts.Single(
            part => part.partInstanceId == PreviousPartId);
        internal SurgicalPartInstance IncomingPart => stateStore.State.Parts.Single(
            part => part.partInstanceId == IncomingPartId);

        internal FacilityBufferPlannedOutputToken GetReplacementToken()
        {
            Require(
                Admission.TryGetPlannedOutputToken(
                    Order.replacementAdmissionTokenId,
                    out FacilityBufferPlannedOutputToken token,
                    out FacilityBufferMassAdmissionTokenStatus status)
                && status != FacilityBufferMassAdmissionTokenStatus.Released,
                "replacement fixture lost its planned output reservation");
            return token;
        }

        internal void ReconstructOutputServices(
            out FacilityBufferMassAdmissionService admission,
            out FacilityBufferPlannedOutputPublicationService publication)
        {
            FacilityBufferDestinationClaimRegistry claims = new();
            admission = CreateOutputAdmission(claims);
            publication = new FacilityBufferPlannedOutputPublicationService(
                Repository,
                catalog,
                mass,
                admission);
        }

        internal InstallSurgicalPartEffectHandler
            CreateReplacementEffectHandler(
                IFacilityBufferMassAdmissionService admission,
                IFacilityBufferPlannedOutputPublicationService publication)
        {
            ICharacterWorldQuery characters = CreateReplacementProxy<
                ICharacterWorldQuery>((method, _) => method.Name
                    == "get_Characters"
                    ? new[] { Actor }
                    : DefaultReplacementValue(method.ReturnType));
            IWildlifeWorldQuery wildlife = CreateReplacementProxy<
                IWildlifeWorldQuery>((method, _) => method.Name
                    == "get_Wildlife"
                    ? Array.Empty<WildlifeActor>()
                    : DefaultReplacementValue(method.ReturnType));
            IWildlifeAnatomyHealthRuntime wildlifeAnatomy =
                CreateReplacementProxy<IWildlifeAnatomyHealthRuntime>(
                    (method, _) => DefaultReplacementValue(
                        method.ReturnType));
            return new InstallSurgicalPartEffectHandler(
                characters,
                wildlife,
                Anatomy,
                wildlifeAnatomy,
                Runtime,
                admission,
                publication);
        }

        internal DungeonSurgerySaveData CaptureValidateCrossJoinAndRestore(
            IAnatomyProfileCatalog authoredProfiles = null)
        {
            DungeonSurgerySaveData surgery = JsonUtility.FromJson<
                DungeonSurgerySaveData>(JsonUtility.ToJson(
                new SurgeryPersistence(stateStore).Capture()));
            DungeonGameRestoreReport report = new();
            SurgerySaveValidation.Validate(
                surgery,
                new ResourceSurgicalProcedureCatalog(
                    LoadAssets<SurgicalProcedureSO>(
                        "Assets/Resources/SO/Medical/Procedures")),
                new ResourceAnatomyProfileCatalog(
                    LoadAssets<AnatomyProfileSO>(
                        "Assets/Resources/SO/Medical/Anatomy")),
                report);
            Require(
                report.Success,
                "replacement runtime save rejected its current phase: "
                + string.Join(" | ", report.Errors));
            DungeonPhysicalItemSaveData physical = JsonUtility.FromJson<
                DungeonPhysicalItemSaveData>(JsonUtility.ToJson(
                CapturePhysicalSave()));
            DungeonCharacterBodyHealthSaveData body = JsonUtility.FromJson<
                DungeonCharacterBodyHealthSaveData>(JsonUtility.ToJson(
                CaptureBodySave()));
            if (authoredProfiles != null)
                SurgicalPartProductionOutputCrossAggregateSaveValidation
                    .ValidatePartOwnership(physical, surgery, body, authoredProfiles);
            else
                SurgicalPartProductionOutputCrossAggregateSaveValidation
                    .ValidatePartOwnership(physical, surgery, body);
            stateStore.Replace(SurgerySaveValidation.CreateState(surgery));
            return surgery;
        }

        public void Dispose()
        {
            if (actorObject != null)
            {
                UnityEngine.Object.DestroyImmediate(actorObject);
            }
        }

        private FacilityBufferMassAdmissionService CreateOutputAdmission(
            FacilityBufferDestinationClaimRegistry claims)
        {
            FacilityBufferMassAdmissionService admission = new(
                claims,
                new ReplacementEmptyOccupancy(),
                mass);
            Require(
                claims.TryClaim(
                    new FacilityBufferDestinationClaim(
                        DestinationId,
                        OutputPosition,
                        SurgeryMaterialDestinationAuthority.OwnerDomain,
                        OrderId,
                        FacilityId,
                        FacilityBufferDestinationAnchorKind.LiveFacility),
                    out _,
                    out string claimFailure),
                "replacement fixture could not claim its output destination: "
                + claimFailure);
            Require(
                admission.TryReplaceOwnedProfiles(
                    SurgeryMaterialDestinationAuthority.OwnerDomain,
                    new[]
                    {
                        new FacilityBufferCapacityProfile(
                            DestinationId,
                            OutputPosition,
                            SurgeryMaterialDestinationAuthority.OwnerDomain,
                            OrderId,
                            FacilityId,
                            new PhysicalMassGrams(300000L),
                            SurgeryMaterialDestinationAuthority
                                .InputBufferCapacitySchemaRevision)
                    },
                    out _,
                    out string capacityFailure),
                "replacement fixture could not publish its output capacity: "
                + capacityFailure);
            return admission;
        }

        private DungeonPhysicalItemSaveData CapturePhysicalSave()
        {
            DungeonPhysicalItemSaveData physical = new();
            SurgeryOrder order = Order;
            SurgicalPartInstance incoming = IncomingPart;
            if (order.replacementPhase is
                    SurgicalPartReplacementPhase.OutputReservationPending
                    or SurgicalPartReplacementPhase.OutputReserved
                    or SurgicalPartReplacementPhase.BodyCommitted
                && !incoming.installed)
            {
                WorldItemStackSnapshot stack = query.GetAllStacks()
                    .SingleOrDefault(value => value != null
                        && string.Equals(
                            value.StackId,
                            incoming.worldStackId,
                            StringComparison.Ordinal));
                if (stack != null)
                {
                    physical.stacks.Add(new WorldItemStackSaveData
                    {
                        stackId = stack.StackId,
                        itemInstanceId = stack.ItemInstanceId,
                        itemId = stack.ItemId,
                        quantity = stack.Quantity,
                        state = stack.State,
                        gridX = stack.Position.x,
                        gridY = stack.Position.y,
                        destinationId = stack.DestinationId,
                        sourceStorageDestinationId =
                            stack.SourceStorageDestinationId,
                        components = stack.Components
                            .Select(component => component.Clone())
                            .ToList()
                    });
                }
                return physical;
            }
            if (order.replacementPhase is not
                    (SurgicalPartReplacementPhase.OutputPublished
                    or SurgicalPartReplacementPhase.Completed))
            {
                return physical;
            }

            SurgicalPartInstance previous = PreviousPart;
            physical.stacks.Add(new WorldItemStackSaveData
            {
                stackId = order.replacementOutputStackId,
                itemInstanceId = order.replacementOutputItemInstanceId,
                itemId = previous.itemDefinitionId,
                quantity = 1,
                state = WorldItemStackState.FacilityOutputBuffer,
                destinationId = order.materialDestinationId,
                gridX = order.replacementOutputX,
                gridY = order.replacementOutputY,
                components =
                    SurgicalPartProductionOutputCrossAggregateSaveValidation
                        .CreateReplacementPhysicalComponentsForEditor(
                            previous,
                            order,
                            order.replacementOutputMassGrams)
                        .Select(component => component.Clone())
                        .ToList()
            });
            return physical;
        }

        private DungeonCharacterBodyHealthSaveData CaptureBodySave() => new()
        {
            characters = new List<CharacterBodyHealthState>
            {
                new()
                {
                    characterId = SubjectId,
                    anatomyProfileId = Anatomy.ProjectionProfileId,
                    anatomyNodes = new List<AnatomyNodeHealthState>
                    {
                        ReplacementAnatomyRuntime.CloneNode(Anatomy.Node)
                    }
                }
            }
        };
    }

    private sealed class ReplacementAnatomyRuntime : IAnatomyHealthRuntime
    {
        internal ReplacementAnatomyRuntime(string nodeId, string installedPartId)
        {
            Node = new AnatomyNodeHealthState
            {
                nodeId = nodeId,
                maxHealth = 80f,
                currentHealth = 32f,
                installedPartId = installedPartId,
                installedPartKind = SurgicalPartKind.Prosthetic,
                installedPartEfficiency = 1f
            };
        }

        internal AnatomyNodeHealthState Node { get; }
        internal string ProjectionProfileId { get; set; } = "replacement-runtime";
        internal int ReplaceCallCount { get; private set; }

        internal void ForceInstalledPartForCasRace(string partId) =>
            Node.installedPartId = partId;

        internal void SetCurrentHealthForReplacementRefresh(float value) =>
            Node.currentHealth = value;

        public AnatomyHealthSnapshot GetAnatomySnapshot(CharacterActor actor) =>
            CreateSnapshot();

        public AnatomyHealthSnapshot GetAnatomySnapshot(string characterId) =>
            CreateSnapshot();

        public bool TryDamageNode(
            CharacterActor actor,
            string nodeId,
            float damage,
            float bleeding,
            string reason) => false;

        public bool TryDamageNodeWithCause(
            CharacterActor actor,
            string nodeId,
            float damage,
            float bleeding,
            CharacterDeathCauseCode deathCause,
            string reasonCode) => false;

        public bool TryHealNode(
            CharacterActor actor,
            string nodeId,
            float health,
            float infectionReduction) => false;

        public bool TryStopBleeding(
            CharacterActor actor,
            string nodeId,
            out DomainFailure failure)
        {
            failure = new DomainFailure(FailureCode.SurgeryTargetNodeMissing, nodeId);
            return false;
        }

        public PartRecoveryPolicy GetRecoveryPolicy(
            CharacterActor actor,
            string nodeId) => PartRecoveryPolicy.ReplacementOnly;

        public bool CanRecoverNaturally(CharacterActor actor, string nodeId) =>
            false;

        public bool TryMaintainNode(
            CharacterActor actor,
            string nodeId,
            float durability,
            float contaminationReduction,
            out DomainFailure failure)
        {
            failure = new DomainFailure(FailureCode.SurgeryTargetNodeMissing, nodeId);
            return false;
        }

        public bool TryRemoveNode(
            CharacterActor actor,
            string nodeId,
            out AnatomyNodeHealthState removedNode,
            out DomainFailure failure)
        {
            removedNode = null;
            failure = new DomainFailure(FailureCode.SurgeryTargetNodeMissing, nodeId);
            return false;
        }

        public bool TryInstallPart(
            CharacterActor actor,
            string nodeId,
            string partInstanceId,
            SurgicalPartKind partKind,
            float efficiency,
            float restoredCurrentHealth,
            bool preserveRestoredHealth,
            out DomainFailure failure)
        {
            failure = new DomainFailure(FailureCode.SurgeryTargetNodeMissing, nodeId);
            return false;
        }

        public bool TryReplaceNodePart(
            CharacterActor actor,
            string nodeId,
            string expectedPartInstanceId,
            float expectedCurrentHealth,
            float expectedMaxHealth,
            string partInstanceId,
            SurgicalPartKind partKind,
            float efficiency,
            float restoredCurrentHealth,
            bool preserveRestoredHealth,
            out AnatomyNodeHealthState replacedNode,
            out DomainFailure failure)
        {
            ReplaceCallCount++;
            replacedNode = null;
            if (!string.Equals(Node.nodeId, nodeId, StringComparison.Ordinal)
                || !string.Equals(
                    Node.installedPartId,
                    expectedPartInstanceId,
                    StringComparison.Ordinal)
                || Node.currentHealth != expectedCurrentHealth
                || Node.maxHealth != expectedMaxHealth)
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryPartUnavailable,
                    expectedPartInstanceId,
                    Node.installedPartId);
                return false;
            }

            replacedNode = CloneNode(Node);
            Node.missing = false;
            Node.installedPartId = partInstanceId;
            Node.installedPartKind = partKind;
            Node.installedPartEfficiency = Mathf.Clamp(efficiency, 0.1f, 1.75f);
            Node.currentHealth = preserveRestoredHealth
                ? Mathf.Clamp(restoredCurrentHealth, 0f, Node.maxHealth)
                : Mathf.Max(1f, Node.maxHealth * 0.35f);
            failure = DomainFailure.None;
            return true;
        }

        public bool TryAddNodeBurden(
            CharacterActor actor,
            string nodeId,
            float rejection,
            float mutation,
            float infection,
            out DomainFailure failure)
        {
            failure = new DomainFailure(FailureCode.SurgeryTargetNodeMissing, nodeId);
            return false;
        }

        public bool TryReduceNodeBurden(
            CharacterActor actor,
            string nodeId,
            float rejection,
            float mutation,
            float infection,
            out DomainFailure failure)
        {
            failure = new DomainFailure(FailureCode.SurgeryTargetNodeMissing, nodeId);
            return false;
        }

        internal static AnatomyNodeHealthState CloneNode(
            AnatomyNodeHealthState source) => new()
        {
            nodeId = source.nodeId,
            maxHealth = source.maxHealth,
            currentHealth = source.currentHealth,
            bleedingPerSecond = source.bleedingPerSecond,
            infection = source.infection,
            missing = source.missing,
            installedPartId = source.installedPartId,
            installedPartKind = source.installedPartKind,
            installedPartEfficiency = source.installedPartEfficiency,
            rejectionBurden = source.rejectionBurden,
            mutationBurden = source.mutationBurden,
            moduleBonus = source.moduleBonus,
            recoveryPolicy = source.recoveryPolicy
        };

        private AnatomyHealthSnapshot CreateSnapshot() => new(
            ProjectionProfileId,
            new[] { Node },
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            1f);
    }

    private sealed class FailOnceReplacementBatchDisposition :
        IPhysicalItemBatchDispositionService
    {
        private readonly IPhysicalItemBatchDispositionService inner;

        internal FailOnceReplacementBatchDisposition(
            IPhysicalItemBatchDispositionService inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        internal bool FailTransferPendingOnce { get; set; }

        public bool TryCommit(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => inner.TryCommit(
            inputs,
            kind,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);

        public bool TryCommitPending(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason)
        {
            if (FailTransferPendingOnce
                && kind == PhysicalItemDispositionKind.Transfer)
            {
                FailTransferPendingOnce = false;
                receipt = default;
                failureReason = "qa-replacement-natural-transfer-failure";
                return false;
            }

            return inner.TryCommitPending(
                inputs,
                kind,
                operationId,
                reasonCode,
                out receipt,
                out failureReason);
        }

        public bool Acknowledge(string commitId, out string failureReason) =>
            inner.Acknowledge(commitId, out failureReason);

        public bool TryGetPending(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt) =>
            inner.TryGetPending(operationId, out receipt);
    }

    private sealed class FailOnceReplacementPublication :
        IFacilityBufferPlannedOutputPublicationService
    {
        private readonly IFacilityBufferPlannedOutputPublicationService inner;

        internal FailOnceReplacementPublication(
            IFacilityBufferPlannedOutputPublicationService inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        internal bool FailPublishOnce { get; set; }
        internal bool FailAcknowledgementOnce { get; set; }

        public bool TryPublishFullBatch(
            FacilityBufferPlannedOutputToken token,
            out FacilityBufferPlannedOutputPublicationReceipt receipt,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason)
        {
            if (FailPublishOnce)
            {
                FailPublishOnce = false;
                receipt = default;
                failureCode = FacilityBufferPlannedOutputPublicationFailureCode
                    .RepositoryTransactionFailed;
                failureReason = "qa-replacement-publication-failure";
                return false;
            }

            return inner.TryPublishFullBatch(
                token,
                out receipt,
                out failureCode,
                out failureReason);
        }

        public bool TryRollbackPublishedBatch(
            FacilityBufferPlannedOutputPublicationReceipt receipt,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason) => inner.TryRollbackPublishedBatch(
            receipt,
            out failureCode,
            out failureReason);

        public bool TryAcknowledgePublishedBatch(
            FacilityBufferPlannedOutputPublicationReceipt receipt,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason)
        {
            if (FailAcknowledgementOnce)
            {
                FailAcknowledgementOnce = false;
                failureCode = FacilityBufferPlannedOutputPublicationFailureCode
                    .RepositoryTransactionFailed;
                failureReason = "qa-replacement-acknowledgement-failure";
                return false;
            }

            return inner.TryAcknowledgePublishedBatch(
                receipt,
                out failureCode,
                out failureReason);
        }

        public bool TryAcknowledgeAndReleasePublishedBatch(
            FacilityBufferPlannedOutputPublicationReceipt receipt,
            FacilityBufferAcknowledgedOutputReleaseTarget target,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason) => inner.TryAcknowledgeAndReleasePublishedBatch(
            receipt,
            target,
            out failureCode,
            out failureReason);

        public bool TryRollbackRestoreCandidate(
            FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason) => inner.TryRollbackRestoreCandidate(
            candidate,
            out failureCode,
            out failureReason);

        public bool TryAcknowledgeRestoreCandidate(
            FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason) => inner.TryAcknowledgeRestoreCandidate(
            candidate,
            out failureCode,
            out failureReason);

        public bool TryAcknowledgeAndReleaseRestoreCandidate(
            FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
            FacilityBufferAcknowledgedOutputReleaseTarget target,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason) =>
            inner.TryAcknowledgeAndReleaseRestoreCandidate(
                candidate,
                target,
                out failureCode,
                out failureReason);

        public bool TryCapturePendingBatch(
            string batchCommitId,
            out FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason) => inner.TryCapturePendingBatch(
            batchCommitId,
            out candidate,
            out failureCode,
            out failureReason);

        public bool TryCaptureBatch(
            string batchCommitId,
            bool allowAcknowledged,
            out FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
            out bool acknowledged,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason) => inner.TryCaptureBatch(
            batchCommitId,
            allowAcknowledged,
            out candidate,
            out acknowledged,
            out failureCode,
            out failureReason);
    }

    private sealed class ReplacementEmptyOccupancy :
        IFacilityBufferPhysicalOccupancyQuery
    {
        public FacilityBufferPhysicalOccupancySnapshot Capture(
            string destinationId) => new(0L, 0L);

        public bool TryCaptureExactLot(
            IReadOnlyList<FacilityBufferMassLotSlice> slices,
            out FacilityBufferExactLotSnapshot lot,
            out string failureReason)
        {
            lot = default;
            failureReason = "replacement-fixture-no-exact-lot";
            return false;
        }
    }

    private sealed class ReplacementNoopRelease :
        IFacilityBufferDestinationReleaseService
    {
        internal static readonly ReplacementNoopRelease Instance = new();

        public bool TryReleaseAtOwnerPosition(
            string destinationId,
            Vector2Int ownerPosition,
            string reasonCode,
            out int releasedQuantity,
            out string failureReason)
        {
            releasedQuantity = 0;
            failureReason = "replacement-fixture-no-release";
            return false;
        }
    }

    private static T CreateReplacementProxy<T>(
        Func<MethodInfo, object[], object> handler)
        where T : class
    {
        T proxy = DispatchProxy.Create<
            T,
            OrganPreservationRestoreJoinFixture.ConfigurableDispatchProxy>();
        ((OrganPreservationRestoreJoinFixture.ConfigurableDispatchProxy)(object)
                proxy)
            .Handler = handler;
        return proxy;
    }

    private static object DefaultReplacementValue(Type type) => type == typeof(void)
        ? null
        : type != null && type.IsValueType
            ? Activator.CreateInstance(type)
            : null;

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
