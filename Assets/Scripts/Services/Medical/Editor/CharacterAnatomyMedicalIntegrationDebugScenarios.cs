#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class CharacterAnatomyMedicalIntegrationDebugScenarios
{
    public static void RunD04FromBatch()
    {
        if (!Application.isBatchMode)
        {
            throw new InvalidOperationException(
                "D04 requires the owned isolated batch verification context.");
        }
        string configured = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(configured)
            || !Path.IsPathRooted(configured))
        {
            throw new InvalidOperationException(
                "D04 requires an absolute isolated QA output directory.");
        }

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unity project root is unavailable.");
        string qaRoot = Path.GetFullPath(Path.Combine(projectRoot, "Artifacts/QA"));
        string output = Path.GetFullPath(configured);
        string reportPath = Path.Combine(output, "medical-d04-components-report.json");
        if (!output.StartsWith(
                qaRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || File.Exists(reportPath))
        {
            throw new InvalidOperationException(
                "D04 report must be a create-only path under Artifacts/QA.");
        }

        string runDirectory = Directory.GetParent(output)?.FullName
            ?? throw new InvalidOperationException("D04 run directory is unavailable.");
        if (!string.Equals(Path.GetFileName(output), "unity-evidence",
                StringComparison.OrdinalIgnoreCase)
            || !runDirectory.StartsWith(qaRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("D04 requires the launcher evidence directory.");
        string probePath = Path.Combine(runDirectory, "isolation-probe.txt");
        string probe = File.ReadAllText(probePath);
        if (!probe.Contains("PASS_NONINTERACTIVE_PROBE", StringComparison.Ordinal)
            || !probe.Contains("visible=false", StringComparison.Ordinal)
            || !probe.Contains("windowCreatedAndDestroyed=true", StringComparison.Ordinal)
            || !probe.Contains("inJob=true", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "D04 requires the successful per-run noninteractive OS probe.");
        }

        string previousRoot = Environment.GetEnvironmentVariable(
            DungeonPersistencePaths.VerificationRootVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                DungeonPersistencePaths.VerificationRootVariable,
                Path.Combine(output, "persistent-data"));
            if (!DungeonPersistencePaths.IsIsolatedVerification)
                throw new InvalidOperationException("D04 isolated persistence is unavailable.");
            // These retained component contracts are not a new natural-play or
            // coupled clinical witness. The real scene witness is bound separately.
            CharacterMedicalOutcomeProductionEntryDebugScenarios.RunOrThrow();
            VerifySurfaceHealingAuthority();
            CharacterMedicalSupplyOutboxDebugScenarios.RunAll();

            JObject report = new()
            {
                ["schema"] = "dungeonstory-d04-component-report@1",
                ["rowId"] = "D04",
                ["passed"] = true,
                ["rowCurrentEvidenceComplete"] = false,
                ["clinicalOutcome"] = "stabilization,reserve-rejected,write-rejected,small-treatment,pending-ledger-restore-retry,partial-labor-interruption",
                ["surfaceHealing"] = "human,slime;canonical-budget,read-stability,zero-work-no-op",
                ["physicalSupply"] = "published-ack-fault,ack-only-recovery,uncommitted-intent,exact-extracted-blood-definition",
                ["scope"] = "Original clinical runtime/real ledger with body-mutation and supply test ports; separate real body-health surface contracts; real physical repository/Sink/coordinator with controlled stock queries and tare. Not a coupled natural clinical, Brain/UI, whole-world or cold-restore witness.",
                ["exportLimitations"] = "Assertion summary only; no observed full DTO or numeric identity export is fabricated from test expectations.",
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            };
            Directory.CreateDirectory(output);
            using FileStream stream = new(
                reportPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using StreamWriter writer = new(stream);
            writer.Write(report.ToString(Newtonsoft.Json.Formatting.Indented));
            Debug.Log("U0_D04_MEDICAL_COMPONENTS=PASS; report=" + reportPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                DungeonPersistencePaths.VerificationRootVariable, previousRoot);
        }
    }

    public static void VerifySurfaceHealingAuthority()
    {
        VerifySurfaceHealing("Human", "torso");
        VerifySurfaceHealing("Slime", "membrane");
        Debug.Log("SURFACE_HEALING_AUTHORITY=PASS; human/slime; treatment/heal; alias-budget; read-stability");
    }

    private static void VerifySurfaceHealing(string species, string surfaceNode)
    {
        CharacterSO data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
            CharacterType.Customer, "Surface healing " + species, species);
        GameObject instance = new GameObject(data.characterName);
        CharacterActor actor = null;
        try
        {
            actor = instance.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(instance);
            actor.EnsureRuntimeState();
            actor.data = data;
            actor.characterType = CharacterType.Customer;
            actor.Identity.SetPersistentId("character:surface-healing:" + species.ToLowerInvariant());
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(actor);
            CharacterBodyHealthRuntime health = CharacterAiEditorTestDependencies.BodyHealthRuntime;
            health.ConfigureVitals(actor, 100f, resetCurrentHealth: true);
            if (!health.TryDamageNode(actor, surfaceNode, 10f, 0f, "surface-healing-fixture"))
                throw new InvalidOperationException(species + " surface damage setup failed.");
            float missing = health.GetMissingPartHealth(actor);
            if (Mathf.Abs(missing - 10f) > 0.001f)
                throw new InvalidOperationException(species + " expected exactly 10 canonical damage, got " + missing);

            if (!health.ApplyTreatment(actor, 5f, 0f))
                throw new InvalidOperationException(species + " treatment was unexpectedly rejected.");
            float afterTreatment = health.GetMissingPartHealth(actor);
            if (Mathf.Abs(afterTreatment - 5f) > 0.001f)
                throw new InvalidOperationException(species + " treatment must restore 5 canonical HP once; missing=" + afterTreatment);

            // Repeated queries must not overwrite the typed result from stale aliases.
            health.GetSnapshot(actor);
            health.GetAnatomySnapshot(actor);
            if (Mathf.Abs(health.GetMissingPartHealth(actor) - afterTreatment) > 0.001f)
                throw new InvalidOperationException(species + " healing changed during read projection.");
            health.Heal(actor, 3f, stopBleeding: false);
            if (Mathf.Abs(health.GetMissingPartHealth(actor) - 2f) > 0.001f)
                throw new InvalidOperationException(species + " general healing lost or multiplied its canonical budget.");
            health.Heal(actor, 1000f, stopBleeding: true);
            if (health.GetMissingPartHealth(actor) > 0.001f
                || health.ApplyTreatment(actor, 0f, 0f))
                throw new InvalidOperationException(species + " full-health/no-op boundary failed.");
        }
        finally
        {
            if (actor != null)
                CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacter(actor);
            UnityEngine.Object.DestroyImmediate(instance);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [MenuItem("DungeonStory/Debug/QA/Run Character Anatomy Medical Integration")]
    public static void RunFromMenu()
    {
        IReadOnlyList<string> errors = RunAll();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join("\n", errors));
        }
        Debug.Log("CHARACTER_ANATOMY_MEDICAL_INTEGRATION PASS");
    }

    public static IReadOnlyList<string> RunAll()
    {
        List<string> errors = new();
        CheckAdvancedPartMath(errors);
        CheckAuthoredIdentityDoesNotFollowAssetName(errors);
        CheckPredictivePrefetch(errors);
        CheckPhysicalFieldMedicalSupplies(errors);
        CheckFieldMedicalAndRescue(errors);
        return errors;
    }

    public static void CheckAuthoredIdentityDoesNotFollowAssetName(List<string> errors)
    {
        AnatomyProfileSO profile = ScriptableObject.CreateInstance<AnatomyProfileSO>();
        AnatomyConditionLexiconSO lexicon =
            ScriptableObject.CreateInstance<AnatomyConditionLexiconSO>();
        try
        {
            profile.name = "ProfileAssetName";
            profile.Configure("   ", "Fixture", "fixture", Array.Empty<string>(),
                Array.Empty<AnatomyNodeDefinition>());
            lexicon.name = "LexiconAssetName";
            lexicon.Configure("   ", "fixture", Array.Empty<string>(),
                Array.Empty<AnatomyConditionLexiconEntry>());
            Require(profile.ProfileId.Length == 0
                    && profile.ValidateDefinition().Count > 0,
                "A blank anatomy profile ID must not fall back to the asset name.", errors);
            Require(lexicon.LexiconId.Length == 0
                    && lexicon.ValidateDefinition().Count > 0,
                "A blank anatomy lexicon ID must not fall back to the asset name.", errors);
            profile.name = "RenamedBlankProfileAsset";
            lexicon.name = "RenamedBlankLexiconAsset";
            Require(profile.ProfileId.Length == 0 && lexicon.LexiconId.Length == 0,
                "Renaming anatomy assets must not manufacture IDs for blank definitions.", errors);

            profile.Configure(" anatomy:fixture ", "Fixture", "fixture",
                Array.Empty<string>(), Array.Empty<AnatomyNodeDefinition>());
            lexicon.Configure(" condition:fixture ", "fixture",
                Array.Empty<string>(), Array.Empty<AnatomyConditionLexiconEntry>());
            profile.name = "RenamedProfileAsset";
            lexicon.name = "RenamedLexiconAsset";
            Require(profile.ProfileId == "anatomy:fixture"
                    && profile.ValidateDefinition().Count == 0,
                "An explicit anatomy profile ID must remain stable across asset renames.", errors);
            Require(lexicon.LexiconId == "condition:fixture"
                    && lexicon.ValidateDefinition().Count == 0,
                "An explicit anatomy lexicon ID must remain stable across asset renames.", errors);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(lexicon);
        }
    }

    private static void CheckAdvancedPartMath(List<string> errors)
    {
        AnatomyNodeHealthState node = new AnatomyNodeHealthState
        {
            maxHealth = 100f,
            currentHealth = 80f,
            installedPartEfficiency = 1.35f,
            recoveryPolicy = PartRecoveryPolicy.MaintenanceOnly
        };
        Require(Mathf.Abs(node.FunctionalEfficiency - 1.08f) < 0.0001f,
            "1.35 efficiency at 80% health must produce 1.08 functional efficiency.", errors);
        Require(node.FunctionalEfficiency > 1f,
            "Functional efficiency must not clamp advanced parts to 1.0.", errors);
    }

    private static void CheckPredictivePrefetch(List<string> errors)
    {
        Require(ProductionMaterialPrefetchPolicy.CalculateBatchCount(12f, 3f, 20f) == 1,
            "Slow production should request one batch.", errors);
        Require(ProductionMaterialPrefetchPolicy.CalculateBatchCount(12f, 3f, 7f) == 3,
            "Fast production should request three batches.", errors);
        Require(ProductionMaterialPrefetchPolicy.CalculateBatchCount(12f, 3f, 0.5f) == 3,
            "Prefetch must remain capped at three batches.", errors);
    }

    private static void CheckFieldMedicalAndRescue(List<string> errors)
    {
        OffenseFieldMedicalRuntime runtime = new OffenseFieldMedicalRuntime();
        Require(runtime.TryApplyStabilization(
                "expedition:a", "character:a", "node:wing", "kit:instance:1", 3,
                out _),
            "First field stabilization should succeed.", errors);
        Require(!runtime.TryApplyStabilization(
                "expedition:a", "character:a", "node:wing", "kit:instance:2", 4,
                out _),
            "The same node must not be stabilized twice in one expedition.", errors);
        Require(runtime.TryAssignCarrier(
                "expedition:a", "character:a", "character:b",
                35f, 5f, 60f, 10f, out _),
            "A carrier with enough remaining capacity should accept one casualty.", errors);
        Require(!runtime.TryAssignCarrier(
                "expedition:a", "character:c", "character:b",
                10f, 0f, 60f, 10f, out _),
            "One carrier must not carry two casualties.", errors);
        Require(runtime.GetMovementTimeMultiplier("expedition:a") > 1f,
            "Casualty carrying must slow hex travel.", errors);
        Require(runtime.TrySetStranded(
                "expedition:a", new OffenseHexCoord(2, -1), 4f, 12f, "이동 불능"),
            "Expedition should enter stranded state.", errors);
        Require(runtime.TryDispatchRescue(
                "expedition:a", "expedition:rescue",
                new[] { "character:r1", "character:r2" }, out _),
            "A 1-5 member rescue expedition should dispatch.", errors);
        Require(runtime.TryMergeRescue(
                "expedition:rescue", new[] { "character:a" }, out _),
            "Rescue convoy should merge on arrival.", errors);
        Require(!runtime.IsStranded("expedition:a"),
            "Merged rescue must clear the active stranded state.", errors);

        OffenseWorldSaveData save = new OffenseWorldSaveData();
        runtime.Capture(save);
        OffenseFieldMedicalRuntime restored = new OffenseFieldMedicalRuntime();
        restored.PublishRestoreCandidate(
            restored.BuildRestoreCandidate(save));
        Require(restored.GetStabilizations("expedition:rescue").Count == 1,
            "Merged field stabilization must follow the rescue convoy through save/restore.", errors);
        restored.ClearOnDungeonArrival("expedition:rescue");
        Require(restored.GetStabilizations("expedition:rescue").Count == 0,
            "Field stabilization must clear when the rescue convoy reaches the dungeon.", errors);
    }

    private static void CheckPhysicalFieldMedicalSupplies(List<string> errors)
    {
        OffenseSupplyType[] kits =
        {
            OffenseSupplyType.FieldEmergencyKit,
            OffenseSupplyType.RuneSlimePatch,
            OffenseSupplyType.MycelialCulturePack,
            OffenseSupplyType.WingSplintKit,
            OffenseSupplyType.TemporaryPowerBypass,
            OffenseSupplyType.BloodSealKit,
            OffenseSupplyType.ManaCoreRestraint
        };
        string[] itemIds = kits
            .Select(OffenseSupplyCatalog.GetPhysicalItemId)
            .ToArray();
        Require(itemIds.All(id => !string.IsNullOrWhiteSpace(id)),
            "Every field medical supply must map to a physical item id.", errors);
        Require(itemIds.Distinct(StringComparer.Ordinal).Count() == kits.Length,
            "Field medical supplies must not share physical item ids.", errors);
        Require(OffenseSupplyCatalog.GetFieldMedicalKit("Slime")
                == OffenseSupplyType.RuneSlimePatch,
            "Slime expeditions must use rune slime patches.", errors);
        Require(OffenseSupplyCatalog.GetFieldMedicalKit("Golem")
                == OffenseSupplyType.TemporaryPowerBypass,
            "Golem expeditions must use temporary power bypasses.", errors);
    }

    private static void Require(bool condition, string message, List<string> errors)
    {
        if (!condition) errors.Add(message);
    }
}
#endif
