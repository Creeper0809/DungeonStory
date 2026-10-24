#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

/// <summary>Headless lifecycle for the existing V27 runtime evidence producers.</summary>
[InitializeOnLoad]
public static class V27CiEvidenceBatch
{
    private const string Prefix = "V27.CiEvidence.";
    private const string WorldReport = "docs/implementation-reports/world-resource-runtime-latest.txt";
    private const string CropReport = "docs/implementation-reports/crop-plot-runtime-latest.txt";
    private const double RuntimeReadySeconds = 30;

    static V27CiEvidenceBatch()
    {
        SuppressUnusedHeadlessEditorRelay();
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Application.logMessageReceived += ObserveLog;
    }

    private static void SuppressUnusedHeadlessEditorRelay()
    {
        if (Environment.GetEnvironmentVariable("DUNGEONSTORY_V27_HEADLESS_EVIDENCE") != "1")
            return;
        try
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("V27 headless evidence requires batch mode.");
            // The pinned Assistant package exposes this bootstrap hook for strict
            // test consoles. Its containing type is internal, so bind the exact
            // public API and fail if a package update changes that contract.
            Type relay = Type.GetType(
                "Unity.Relay.Editor.RelayService, Unity.AI.Assistant.Editor", true);
            MethodInfo suppress = relay.GetMethod("SuppressAutoStart",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly,
                null, Type.EmptyTypes, null);
            if (suppress == null || suppress.ReturnType != typeof(void))
                throw new MissingMethodException(relay.FullName, "SuppressAutoStart()");
            suppress.Invoke(null, null);
            Debug.Log("V27_HEADLESS_EDITOR_RELAY_AUTOSTART=SUPPRESSED; package bootstrap API; actual Assistant use retains lazy initialization");
        }
        catch (Exception error)
        {
            EditorApplication.delayCall += () => EditorApplication.Exit(1);
            throw new InvalidOperationException(
                "V27 headless editor relay bootstrap could not honor its package contract.", error);
        }
    }

    public static void RunWorldResources() => Begin("world");
    public static void RunCrops() => Begin("crop");
    public static void RunPaired() => Begin("paired");

    // PR contracts deliberately use existing bounded scenarios. Long balance
    // portfolios remain separate release evidence and are never invoked here.
    public static void RunImpactCoreContracts() => RunImpact("core", () =>
    {
        RunSourceIdentityContracts();
        RunNaturalTimeScopeContracts();
        RunProductionCommitIdentityContracts();
        RunNaturalRestoreContracts();
        RunSkillRestoreRetryContracts();
        DurableFacilityEquipmentSlotRuntimeDebugScenarios.RunAll();
        DurableFacilityEquipmentUseRuntimeDebugScenarios.RunAll();
        DurableFacilityEquipmentSaveDebugScenarios.RunAll();
        Debug.Log(V27RandomStreamManifestDebugScenarios.RunAll());
    });

    public static void RunImpactCharacterContracts() => RunImpact("character", RunPreparationSkills);
    public static void RunImpactEffectContracts() => RunImpact("effects", RunEffectInputs);
    public static void RunSoAuthoringContracts() => RunImpact("so-authoring", () => ContentReferenceSelectionDebugScenarios.RunWithUndoIsolation(() =>
    {
        ContentReferenceIntegrityDebugScenarios.RunAll();
        ContentReferenceSelectionDebugScenarios.RunAll();
        SoReferenceRemediationLeafADebugScenarios.ValidateOrThrow();
        V20CampaignDebugScenarios.ValidateFactionArcMembershipForR06();
        Debug.Log(ResearchPrerequisiteLinkDrawerDebugScenarios.RunOrThrow());
        var errors = new List<string>();
        CharacterAnatomyMedicalIntegrationDebugScenarios.CheckAuthoredIdentityDoesNotFollowAssetName(errors);
        SpeciesFactionDefenseExpansionDebugScenarios.ValidateDefense(errors);
        if (errors.Count != 0) throw new InvalidOperationException(string.Join(" | ", errors));
        if (!FacilityEvolutionDebugScenarios.VerifyAuthoredIdentityDoesNotFollowAssetName())
            throw new InvalidOperationException("Facility identities changed with asset names.");
        V23CraftingDebugScenarios.ValidateWorkerPolicyTraitReferencesForR13();
        CharacterSOEditor.RunAuthoringAudit();
        ContentReferenceSelectionDebugScenarios.VerifyHeritableTraitConsequenceAuthoring();
        if (!CharacterSkillCapabilityDebugScenarios.RunFormulaCatalogRelationScenario())
            throw new InvalidOperationException("Skill relation contracts failed.");
        V27CharacterPerformanceDebugScenarios.ValidateFormulaReferenceGuards();
            WorkTypeReferenceIntegrityDebugScenarios.RunFromMenu();
        OwnerScopedContentReferenceDebugScenarios.RunAll();
        EvolutionFormulaForbiddenSynergyReferenceDebugScenarios.Validate();
        ServiceRoomDebugScenarios.Run();
        CharacterProgressionDebugScenarios.VerifyOwnerFixedSkillReferenceRestore();
        CharacterTraitIdentityRuleContractDebugScenarios.Run();
        R18ContentReferenceDebugScenarios.RunAll();
        R18MedicalPolicyReferenceDebugScenarios.RunAll();
    }));
    public static void RunSoRuntimeContracts() => RunImpact("so-runtime", () =>
    {
        Debug.Log(OffenseStrategicDebugScenarios.RunWeatherTravelFocused());
        Debug.Log(SurgeryDebugScenarios.VerifyAnatomyProfiles());
        Debug.Log(SurgeryDebugScenarios.VerifyOperatorScoreThreshold());
        WildlifeDebugScenarios.VerifyAuthoredAnatomyAndLegacyRestore();
        AgeConditionAnatomyProjectionDebugScenarios.VerifyAuthoredProfileTargetProjectionFocused();
        Debug.Log(WildlifeDebugScenarios.VerifyPredationAuthority());
        DiseaseFieldResponseOutboxDebugScenarios.RunFromMenu();
        RitualFastingA08DebugScenarios.RunHeritableProjectionContracts();
        RitualFastingA08DebugScenarios.RunR18TraitEffectContracts();
        CombatSystemDebugScenarios.VerifyHeritableBluntDamage();
        OffenseRewardDebugScenarios.RunPersistentBattleSnapshotRestoreRegression();
    });
    public static void RunSoInspectorContracts() => RunImpact("so-inspector", () =>
        ContentReferenceSelectionDebugScenarios.RunWithUndoIsolation(() =>
        {
            ContentReferenceSelectionDebugScenarios.VerifyInspectorGUI();
            ContentReferenceSelectionDebugScenarios.VerifyOwnerFixedInspectorGUI();
            OwnerScopedContentReferenceDebugScenarios.VerifyInspectorGUI();
            R18ContentReferenceDebugScenarios.VerifyInspectorGUI();
            R18MedicalPolicyReferenceDebugScenarios.VerifyInspectorGUI();
        }));
    public static void CaptureContentPublication() => RunImpact("content-publication", () =>
    {
        Directory.CreateDirectory("Artifacts/QA/ContentReferenceIntegrity");
        File.WriteAllText("Artifacts/QA/ContentReferenceIntegrity/identities-candidate.json",
            ContentIdentityProtection.CapturePublicationCandidateJson());
        _ = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
        Debug.Log("SO_PUBLICATION_CANDIDATE_CAPTURED; no authored input files written");
    });
    public static void RunImpactCrops() => Begin("crop", "crop");

    public static void RunImpactWorldRestore()
    {
        RequireIsolatedEditMode();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        SessionState.SetString(Prefix + "impactWorldSource", V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest());
        SessionState.SetString(Prefix + "impactWorldConfig", ComputeImpactConfigurationDigest());
        SessionState.SetBool(Prefix + "impactWorldPending", true);
        SessionState.SetBool(Prefix + "errors", false);
        DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch();
    }

    // Called by the existing world runner after Play has stopped and its own
    // baseline/persistence/console checks have finished. Ordinary runs are unchanged.
    public static bool CompleteImpactWorldRestoreForBatch(bool passed)
    {
        if (!SessionState.GetBool(Prefix + "impactWorldPending", false)) return passed;
        try
        {
            if (!passed || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Actual world restore did not complete cleanly.");
            CompleteImpact("world", SessionState.GetString(Prefix + "impactWorldSource", string.Empty),
                SessionState.GetString(Prefix + "impactWorldConfig", string.Empty),
                SessionState.GetBool(Prefix + "errors", true));
            return true;
        }
        catch (Exception error) { Debug.LogException(error); return false; }
        finally { SessionState.SetBool(Prefix + "impactWorldPending", false); }
    }

    private static void RunImpact(string group, Action action)
    {
        RequireIsolatedEditMode();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        string source = V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest();
        string config = ComputeImpactConfigurationDigest();
        bool errors = false;
        void Observe(string message, string stack, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert) errors = true;
        }
        Application.logMessageReceived += Observe;
        try
        {
            action();
            CompleteImpact(group, source, config, errors);
        }
        finally { Application.logMessageReceived -= Observe; }
    }

    private static void CompleteImpact(string group, string source, string config, bool errors)
    {
        if (errors)
            throw new InvalidOperationException("Impact CI execution logged console errors: " + group);
        if (source != V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest())
            throw new InvalidOperationException("Impact CI execution changed its source inputs: " + group);
        if (config != ComputeImpactConfigurationDigest())
            throw new InvalidOperationException("Impact CI execution changed its configuration inputs: " + group);
        Debug.Log($"IMPACT_CI_PASS group={group};source={source};config={config};consoleErrors=0");
    }

    private static string ComputeImpactConfigurationDigest()
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        var inputs = new System.Collections.Generic.SortedDictionary<string, string>(
            Comparer<string>.Create((a, b) =>
            {
                byte[] left = Encoding.UTF8.GetBytes(a), right = Encoding.UTF8.GetBytes(b);
                int length = Math.Min(left.Length, right.Length);
                for (int i = 0; i < length; i++)
                    if (left[i] != right[i]) return left[i].CompareTo(right[i]);
                return left.Length.CompareTo(right.Length);
            }));
        foreach (string folder in new[] { "Assets", "Packages", "ProjectSettings" })
        foreach (string path in Directory.EnumerateFiles(Path.Combine(root, folder), "*", SearchOption.AllDirectories))
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Impact configuration forbids reparse files: " + path);
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/').Normalize(NormalizationForm.FormC);
            inputs.Add(relative, path);
        }
        using SHA256 sha = SHA256.Create();
        using SHA256 fileSha = SHA256.Create();
        var utf8 = new UTF8Encoding(false, true);
        var scanBuffer = new byte[64 * 1024];
        foreach (var input in inputs)
        {
            // Binary model/asset inputs can be gigabytes. Preserve their exact
            // digest without allocating the entire file in the Editor heap.
            bool opaque = Path.GetExtension(input.Value) == ".bytes";
            using var stream = File.OpenRead(input.Value);
            if (!opaque)
            {
                int read;
                while ((read = stream.Read(scanBuffer, 0, scanBuffer.Length)) != 0)
                {
                    if (Array.IndexOf(scanBuffer, (byte)0, 0, read) < 0) continue;
                    opaque = true;
                    break;
                }
            }
            byte[] hash;
            if (opaque)
            {
                stream.Position = 0;
                hash = fileSha.ComputeHash(stream);
            }
            else
            {
                byte[] content = File.ReadAllBytes(input.Value);
                // Same portable UTF-8/BOM/newline contract as the CI verifier.
                try
                {
                    string text = utf8.GetString(content);
                    if (text.Length != 0 && text[0] == '\ufeff') text = text.Substring(1);
                    content = utf8.GetBytes(text.Replace("\r\n", "\n").Replace('\r', '\n'));
                }
                catch (DecoderFallbackException) { /* Keep exact non-UTF8 bytes. */ }
                hash = fileSha.ComputeHash(content);
            }
            string digest = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            byte[] bytes = utf8.GetBytes(input.Key + "\n" + digest + "\n");
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
    }

    public static void RunProductionCommitIdentityContracts()
    {
        RequireIsolatedEditMode();
        ProductionPreparedOutputFullPersistenceDebugScenarios.RunCompletionCommitIdentityContractsForBatch();
        Debug.Log("V27_PRODUCTION_COMMIT_IDENTITY_CONTRACTS=PASS");
    }

    public static void RunSkillRestoreRetryContracts()
    {
        RequireIsolatedEditMode();
        if (!CharacterProgressionDebugScenarios.VerifyWorldRestoreGenerationContract(out string detail)
            || !CharacterProgressionDebugScenarios.VerifyInFlightGenerationTimeout())
            throw new InvalidOperationException("Skill restore/retry contracts failed: " + detail);
        Debug.Log("V27_SKILL_RESTORE_RETRY_CONTRACTS=PASS; saved pending resume; empty world no-op; five real timeouts; JSON/detached restore preserves deferred retry; explicit retry once");
    }

    public static void RunNaturalTimeScopeContracts()
    {
        RequireIsolatedEditMode();
        float originalScale = Time.timeScale;
        float originalFixed = Time.fixedDeltaTime;
        float originalCapture = Time.captureDeltaTime;
        UnityGameTimeScaleController controller = new();
        void Require(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException(message);
        }
        try
        {
            controller.Scale = 1f;
            float baseFixed = Time.fixedDeltaTime;
            Time.fixedDeltaTime = 0.03125f;
            Time.captureDeltaTime = 0.008f;
            void RequireRestored() => Require(!NaturalPortfolioTimeScope.IsActive
                && Time.timeScale == 1f && Time.fixedDeltaTime == 0.03125f
                && Time.captureDeltaTime == 0.008f, "Natural time scope did not restore its exact owner state.");
            NaturalPortfolioTimeScope scope = NaturalPortfolioTimeScope.Begin(controller, 16f, 1f / 60f);
            try
            {
                Require(Time.timeScale == 16f && Mathf.Approximately(Time.fixedDeltaTime, baseFixed * 16f)
                    && Time.captureDeltaTime == 1f / 60f, "Natural timing differs from the production controller.");
                bool nestedRejected = false;
                try { NaturalPortfolioTimeScope.Begin(controller, 2f, 0.1f); }
                catch (InvalidOperationException) { nestedRejected = true; }
                Require(nestedRejected && Time.timeScale == 16f, "Nested time ownership changed active state.");
                bool invalidRejected = false;
                try { NaturalPortfolioTimeScope.ApplyTimeScale(float.NaN); }
                catch (ArgumentOutOfRangeException) { invalidRejected = true; }
                Require(invalidRejected && Time.timeScale == 16f, "Invalid natural scale changed active state.");
                controller.Scale = 0f;
                NaturalPortfolioTimeScope.ApplyTimeScale(16f);
                Require(Time.timeScale == 16f && Mathf.Approximately(Time.fixedDeltaTime, baseFixed * 16f),
                    "Natural resume restored speed without the production fixed timestep.");
            }
            finally { scope.Dispose(); }
            RequireRestored();
            scope.Dispose();
            RequireRestored();
            bool faultObserved = false;
            try
            {
                using NaturalPortfolioTimeScope failed = NaturalPortfolioTimeScope.Begin(controller, 16f, 1f / 60f);
                throw new InvalidOperationException("natural-clock-fixture-fault");
            }
            catch (InvalidOperationException error) when (error.Message == "natural-clock-fixture-fault")
            { faultObserved = true; }
            Require(faultObserved, "Natural time scope swallowed an execution failure.");
            RequireRestored();
            Debug.Log("V27_NATURAL_TIME_SCOPE_CONTRACTS=PASS; production timestep; pause/resume; nested and invalid rejection; exact normal/error cleanup; duplicate dispose");
        }
        finally
        {
            controller.Scale = originalScale;
            Time.fixedDeltaTime = originalFixed;
            Time.captureDeltaTime = originalCapture;
        }
    }

    public static void RunSourceIdentityContracts()
    {
        RequireIsolatedEditMode();
        string path = "Temp/v27-source-identity-" + Guid.NewGuid().ToString("N") + ".cs";
        Directory.CreateDirectory("Temp");
        UTF8Encoding utf8 = new(false, true);
        const string source = "// 한글 source\nclass Sample { const int Value = 1; }\n";
        void Require(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException(message);
        }
        try
        {
            File.WriteAllBytes(path, utf8.GetBytes(source));
            string canonical = V27CurrentSourceEvidenceDigest.ComputeSourceFileDigest(path);
            string rawLf = V27BalanceArtifactWriter.ComputeSha256(path);
            Require(canonical == rawLf, "Canonical LF source bytes changed.");
            foreach (string newline in new[] { "\r\n", "\r" })
            {
                File.WriteAllBytes(path, utf8.GetBytes(source.Replace("\n", newline)));
                Require(canonical == V27CurrentSourceEvidenceDigest.ComputeSourceFileDigest(path),
                    "Source identity depends on checkout newlines.");
                Require(rawLf != V27BalanceArtifactWriter.ComputeSha256(path),
                    "Artifact byte hashing incorrectly normalized newlines.");
            }
            File.WriteAllBytes(path, new byte[] { 0xef, 0xbb, 0xbf }.Concat(utf8.GetBytes(source)).ToArray());
            Require(canonical == V27CurrentSourceEvidenceDigest.ComputeSourceFileDigest(path),
                "UTF-8 BOM changed source identity.");
            File.WriteAllBytes(path, utf8.GetBytes(source.Replace("Value = 1", "Value = 2")));
            Require(canonical != V27CurrentSourceEvidenceDigest.ComputeSourceFileDigest(path),
                "A source content mutation retained its digest.");
            File.WriteAllBytes(path, new byte[] { 0xc3, 0x28 });
            bool invalidRejected = false;
            try { V27CurrentSourceEvidenceDigest.ComputeSourceFileDigest(path); }
            catch (DecoderFallbackException) { invalidRejected = true; }
            Require(invalidRejected, "Invalid source UTF-8 was accepted.");
            File.Delete(path);
            bool missingRejected = false;
            try { V27CurrentSourceEvidenceDigest.ComputeSourceFileDigest(path); }
            catch (FileNotFoundException) { missingRejected = true; }
            Require(missingRejected, "A missing source authority was accepted.");
            Debug.Log("V27_SOURCE_IDENTITY_CONTRACTS=PASS; LF/CRLF/CR/BOM equivalence; content mutation; invalid UTF-8; missing authority; raw artifact hashing preserved");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    public static void RunNaturalRestoreContracts()
    {
        RequireIsolatedEditMode();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        bool errors = false;
        void Observe(string message, string stack, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert) errors = true;
        }
        Application.logMessageReceived += Observe;
        try
        {
            EnvironmentalFieldDebugScenarios.RunAmbientIndependentSaveRoundTrip();
            PhysicalItemLogisticsPlayModeVerificationRunner.VerifyNaturalRestoreExpectation();
            Debug.Log(GameplayOutcomeLedgerDebugScenarios.RunRestoreCandidateLifetimeContracts());
            if (errors) throw new InvalidOperationException("Natural checkpoint restore contracts failed.");
            Debug.Log("V27_NATURAL_RESTORE_CONTRACTS=PASS; ambient-independent field; sparse compatibility; exact epoch advance; mutation rejection; ledger candidate lifetime");
        }
        finally { Application.logMessageReceived -= Observe; }
    }

    public static void RunPreparationSkills()
    {
        RequireIsolatedEditMode();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        bool errors = false;
        void Observe(string message, string stack, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert) errors = true;
        }
        Application.logMessageReceived += Observe;
        try
        {
            if (!CharacterProgressionDebugScenarios.RunPreparationSkillAcquisitionScenario()
                || !CharacterProgressionDebugScenarios.RunSkillAcquiredOutcomeAtomicityScenario() || errors)
                throw new InvalidOperationException("V27 preparation/world skill acquisition regression failed.");
            Debug.Log("V27_PREPARATION_SKILLS=PASS; prepared selection; local evidence; duplicate no-op; world publication rejection/rollback/save/retry");
        }
        finally { Application.logMessageReceived -= Observe; }
    }

    public static void RunEffectInputs()
    {
        RequireIsolatedEditMode();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        bool errors = false;
        void Observe(string message, string stack, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert) errors = true;
        }
        Application.logMessageReceived += Observe;
        try
        {
            ContentReferenceIntegrityDebugScenarios.RunAll();
            Debug.Log(ScriptableObjectModularityDebugScenarios.RunCatalogExtensionsForBatch());
            V20ContentResolutionRecoveryDebugScenarios.RunAuthoredReferenceBoundaries();
            V20ContentResolutionRecoveryDebugScenarios.RunObserverBatch();
            V20ContentResolutionRecoveryDebugScenarios.RunRegisteredHandlerContracts();
            if (!CharacterProgressionDebugScenarios.RunAcquiredTraitEffectProjectionScenario()
                || !CharacterProgressionDebugScenarios.RunAcquiredTraitMilestoneLifecycleScenario()
                || !CharacterProgressionDebugScenarios.RunAcquiredTraitFormulaV2CompositionScenario()
                || errors)
                throw new InvalidOperationException("V27 effect-input regression failed.");
            Debug.Log("V27_EFFECT_INPUTS=PASS; catalog invalidation; authored effects; malformed state/ledger/authority rejection; milestone lifecycle; formula-v2 composition");
        }
        finally { Application.logMessageReceived -= Observe; }
    }

    public static void RunSpatial()
    {
        RequireIsolatedEditMode();
        // Import work must finish before the solver's unchanged five-minute budget starts.
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        V27AssetBackedSpatialCapacityDebugScenarios.RunBatchModeAndExit();
    }

    private static void RequireIsolatedEditMode()
    {
        if (!Application.isBatchMode || !DungeonPersistencePaths.IsIsolatedVerification
            || EditorApplication.isPlayingOrWillChangePlaymode
            || SessionState.GetString(Prefix + "mode", string.Empty).Length != 0)
            throw new InvalidOperationException("V27 evidence requires a fresh isolated EditMode batch.");
    }

    private static void Begin(string mode, string impactGroup = "")
    {
        RequireIsolatedEditMode();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("V27 batch refuses to replace an unsaved scene.");
        EditorSceneManager.OpenScene(V27CurrentSourceEvidenceDigest.GameplayScenePath, OpenSceneMode.Single);
        string scene = V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest();
        if (scene != V27CurrentSourceEvidenceDigest.OfficialGameplaySceneSha256)
            throw new InvalidOperationException("V27 batch requires the reviewed GameplayScene.");
        File.Delete(ReportPath(mode));
        SessionState.SetString(Prefix + "source", V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest());
        SessionState.SetString(Prefix + "impactGroup", impactGroup);
        if (impactGroup.Length != 0)
            SessionState.SetString(Prefix + "impactConfig", ComputeImpactConfigurationDigest());
        SessionState.SetString(Prefix + "scene", scene);
        SessionState.SetBool(Prefix + "errors", false);
        SessionState.SetBool(Prefix + "entered", false);
        SessionState.SetBool(Prefix + "invoked", false);
        SessionState.SetBool(Prefix + "partyPrepared", false);
        SessionState.SetString(Prefix + "mode", mode);
        if (mode == "paired") V27PairedClutterPlayModeVerifier.RequestRun();
        else EditorApplication.EnterPlaymode();
    }

    private static string ReportPath(string mode) => mode switch
    {
        "world" => WorldReport,
        "crop" => CropReport,
        "paired" => V27PairedClutterPlayModeVerifier.ReportPath,
        _ => throw new InvalidOperationException("Unknown V27 batch producer: " + mode)
    };

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (SessionState.GetString(Prefix + "mode", string.Empty).Length == 0) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            SessionState.SetBool(Prefix + "entered", true);
            SessionState.SetFloat(Prefix + "deadline", (float)(EditorApplication.timeSinceStartup + RuntimeReadySeconds));

        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            // Run after all producer cleanup callbacks, including the paired scene lease.
            EditorApplication.delayCall += Complete;
        }
    }

    private static void Update()
    {
        string mode = SessionState.GetString(Prefix + "mode", string.Empty);
        if (mode.Length == 0 || mode == "paired" || !EditorApplication.isPlaying
            || !SessionState.GetBool(Prefix + "entered", false)
            || SessionState.GetBool(Prefix + "invoked", false)) return;
        DungeonRuntimeLifetimeScope scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        try
        {
            bool beforeDeadline = EditorApplication.timeSinceStartup < SessionState.GetFloat(Prefix + "deadline", 0);
            if (scope?.Container == null)
            {
                if (beforeDeadline) return;
                throw new TimeoutException("V27 runtime composition did not become ready.");
            }
            if (mode == "crop" && !scope.Container.Resolve<ICharacterWorldQuery>().Characters.Any(
                    actor => actor != null && !actor.IsDead
                        && actor.CurrentLifecycleState == CharacterLifecycleState.Active
                        && !string.IsNullOrWhiteSpace(actor.Identity?.PersistentId)))
            {
                if (!SessionState.GetBool(Prefix + "partyPrepared", false))
                {
                    SessionState.SetBool(Prefix + "partyPrepared", true);
                    string preparation = StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
                    if (!preparation.Contains("committed=True", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("V27 crop party preparation failed: " + preparation);
                    // Commit may replace the scene and LifetimeScope. Resolve both
                    // again on the next update instead of retaining disposed services.
                    return;
                }
                if (beforeDeadline) return;
                throw new TimeoutException("V27 crop starting party did not activate.");
            }
            SessionState.SetBool(Prefix + "invoked", true);
            if (mode == "world") WorldResourceDebugScenarios.VerifyRuntimeFromMenu();
            else CropPlotDebugScenarios.VerifyRuntimeFromMenu();
        }
        catch (Exception exception) { Debug.LogException(exception); }
        SessionState.SetBool(Prefix + "invoked", true);
        EditorApplication.ExitPlaymode();
    }

    private static void ObserveLog(string message, string stack, LogType type)
    {
        if ((SessionState.GetString(Prefix + "mode", string.Empty).Length > 0
                || SessionState.GetBool(Prefix + "impactWorldPending", false))
            && type is LogType.Error or LogType.Exception or LogType.Assert)
            SessionState.SetBool(Prefix + "errors", true);
    }

    private static void Complete()
    {
        string mode = SessionState.GetString(Prefix + "mode", string.Empty);
        if (mode.Length == 0) return;
        int exitCode = 1;
        try
        {
            string[] lines = File.ReadAllLines(ReportPath(mode));
            bool passed = mode == "paired"
                ? (lines.FirstOrDefault()?.StartsWith("RESULT=PASS; seeds=32; windows=512;", StringComparison.Ordinal) == true
                    || lines.FirstOrDefault()?.StartsWith("RESULT=PASS; seeds=64; windows=1024;", StringComparison.Ordinal) == true)
                    && !V27PairedClutterPlayModeVerifier.HasPendingDurableRun
                    && !V27PairedClutterPlayModeVerifier.HasDurableInterruption
                : lines.Count(value => value == "valid=true") == 1 && !lines.Contains("valid=false");
            if (!SessionState.GetBool(Prefix + "entered", false)
                || SessionState.GetBool(Prefix + "errors", true) || !passed
                || EditorApplication.isPlayingOrWillChangePlaymode
                || V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest() != SessionState.GetString(Prefix + "source", string.Empty)
                || V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest() != SessionState.GetString(Prefix + "scene", string.Empty))
                throw new InvalidOperationException("V27 batch failed its execution, cleanup, or source/scene identity contract: " + mode);
            string impactGroup = SessionState.GetString(Prefix + "impactGroup", string.Empty);
            if (impactGroup.Length != 0)
                CompleteImpact(impactGroup, SessionState.GetString(Prefix + "source", string.Empty),
                    SessionState.GetString(Prefix + "impactConfig", string.Empty), false);
            exitCode = 0;
            Debug.Log("V27_CI_EVIDENCE_BATCH=PASS; producer=" + mode + "; cleanup=complete; sourceDrift=0; sceneDrift=0");
        }
        catch (Exception exception) { Debug.LogException(exception); }
        finally
        {
            SessionState.EraseString(Prefix + "mode");
            EditorApplication.Exit(exitCode);
        }
    }
}
#endif
