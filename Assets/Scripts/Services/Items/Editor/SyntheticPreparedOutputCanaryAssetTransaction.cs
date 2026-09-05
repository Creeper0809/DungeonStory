#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SyntheticPreparedOutputCanaryAssetTransaction
{
    private const int SchemaVersion = 4;
    private const string TransactionId = "qa:v27-definition-only-canary";
    private const string ItemId = "material:qa-v27-definition-only-output";
    private const string RecipeId = "recipe:qa-v27-definition-only-output";
    private const string ItemPath =
        "Assets/Resources/SO/Economy/Items/qa_v27_definition_only_canary.asset";
    private const string RecipePath =
        "Assets/Resources/SO/Economy/Recipes/qa_v27_definition_only_canary.asset";
    private const string ItemCatalogPath =
        "Assets/Resources/SO/Content/ItemDefinitionCatalog.asset";
    private const string DomainCatalogPath =
        "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private const string MarkerPath =
        "Temp/v27-definition-only-canary.transaction.json";
    private const string DispatchRequestPath =
        "Temp/v27-synthetic-prepared-output-canary.dispatch.request";
    private const string CleanupReportPath =
        "Artifacts/QA/prepared-output-synthetic-canary-cleanup.txt";
    private const string CatalogReportPath =
        "Artifacts/QA/prepared-output-synthetic-canary-catalog.txt";
    private const string RecoveryDirectoryName = "V27SyntheticPreparedOutputCanary";
    private const string TransactionSourcePath =
        "Assets/Scripts/Services/Items/Editor/SyntheticPreparedOutputCanaryAssetTransaction.cs";
    private const string VerifierSourcePath =
        "Assets/Scripts/Services/Items/Editor/PhysicalItemLogisticsPlayModeVerifier.cs";

    static SyntheticPreparedOutputCanaryAssetTransaction()
    {
        EditorApplication.update -= DispatchPendingRun;
        EditorApplication.update += DispatchPendingRun;
        EditorApplication.update -= RecoverIfSafe;
        EditorApplication.update += RecoverIfSafe;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall -= RecoverIfSafe;
        EditorApplication.delayCall += RecoverIfSafe;
    }

    private static void DispatchPendingRun()
    {
        if (!File.Exists(DispatchRequestPath)
            || EditorApplication.isCompiling
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid() || active.isDirty)
            return;

        string request;
        try
        {
            request = File.ReadAllText(DispatchRequestPath).Trim();
        }
        catch (IOException)
        {
            return;
        }
        if (!string.Equals(request, "run", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Synthetic prepared-output dispatch request must contain the exact token 'run'.");
        }

        File.Delete(DispatchRequestPath);
        RequestRunFromMenu();
    }

    [MenuItem("DungeonStory/Debug/QA/Request Synthetic Prepared Output Full-Path Canary")]
    public static void RequestRunFromMenu()
    {
        bool transactionAlreadyActive = File.Exists(MarkerPath)
            || File.Exists(
                PhysicalItemLogisticsPlayModeVerifier
                    .PreparedOutputWarehouseRequestPath);
        try
        {
            PrepareAndRequest();
            Debug.Log(
                "Synthetic prepared-output canary assets were published transactionally; "
                + "the existing live PlayMode verifier will run next.");
        }
        catch (Exception exception)
        {
            if (!transactionAlreadyActive)
                TryCleanup(writeFailureReport: true, out _);
            Debug.LogError(
                "Synthetic prepared-output canary setup failed before PlayMode: "
                + exception);
        }
    }

    public static void QueueRunFromEditorCommand()
    {
        Directory.CreateDirectory("Temp");
        if (File.Exists(DispatchRequestPath)
            || File.Exists(MarkerPath)
            || File.Exists(
                PhysicalItemLogisticsPlayModeVerifier
                    .PreparedOutputWarehouseRequestPath))
        {
            throw new InvalidOperationException(
                "A synthetic prepared-output canary run is already pending.");
        }
        Scene active = SceneManager.GetActiveScene();
        string dirtyFailure = "scene-invalid";
        if (!active.IsValid()
            || (active.isDirty
                && !ByteIdenticalSceneDirtinessGuard.TryClearFalseDirty(
                    active,
                    out dirtyFailure)))
        {
            throw new InvalidOperationException(
                "Synthetic prepared-output canary refused an unsaved scene: "
                + (active.IsValid() ? dirtyFailure : "scene-invalid"));
        }
        File.WriteAllText(
            DispatchRequestPath,
            "run",
            new UTF8Encoding(false));
    }

    [MenuItem("DungeonStory/Debug/QA/Run Synthetic Prepared Output Canary Contract Focused")]
    public static void RunContractFocused()
    {
        if (File.Exists(MarkerPath)
            || File.Exists(
                PhysicalItemLogisticsPlayModeVerifier.PreparedOutputWarehouseRequestPath))
        {
            throw new InvalidOperationException(
                "Synthetic canary contract fixture requires no active transaction/request.");
        }

        Scene activeBefore = SceneManager.GetActiveScene();
        bool dirtyBefore = activeBefore.IsValid() && activeBefore.isDirty;
        ItemDefinitionCatalogSO items = RequireAsset<ItemDefinitionCatalogSO>(
            ItemCatalogPath);
        GameDomainContentCatalogSO domain =
            RequireAsset<GameDomainContentCatalogSO>(DomainCatalogPath);
        ValidateBaselineAuthoritative(items, domain);
        Directory.CreateDirectory("Temp");
        try
        {
            File.WriteAllText(
                PhysicalItemLogisticsPlayModeVerifier
                    .PreparedOutputWarehouseRequestPath,
                JsonUtility.ToJson(new PreparedOutputWarehouseVerificationRequest
                {
                    mode = PreparedOutputWarehouseVerificationRequest.SyntheticMode,
                    transactionId = TransactionId,
                    recipeId = RecipeId,
                    itemId = ItemId,
                    transactionNonce = new string('a', 32),
                    itemAssetGuid = new string('b', 32),
                    recipeAssetGuid = new string('c', 32),
                    augmentedItemCatalogSha256 = new string('D', 64),
                    augmentedDomainCatalogSha256 = new string('E', 64),
                    transactionSourceSha256 = new string('F', 64),
                    verifierSourceSha256 = new string('1', 64)
                }));
            bool parsed = PhysicalItemLogisticsPlayModeVerifier
                .TryReadPreparedOutputWarehouseCase(
                    out PreparedOutputLiveRouteCase verificationCase,
                    out string failure);
            if (!parsed
                || !verificationCase.IsSynthetic
                || !string.Equals(
                    verificationCase.TransactionId,
                    TransactionId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    verificationCase.RecipeId,
                    RecipeId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    verificationCase.ItemId,
                    ItemId,
                    StringComparison.Ordinal)
                || verificationCase.TransactionNonce != new string('a', 32)
                || verificationCase.ItemAssetGuid != new string('b', 32)
                || verificationCase.RecipeAssetGuid != new string('c', 32)
                || verificationCase.AugmentedItemCatalogSha256 != new string('D', 64)
                || verificationCase.AugmentedDomainCatalogSha256 != new string('E', 64)
                || verificationCase.TransactionSourceSha256 != new string('F', 64)
                || verificationCase.VerifierSourceSha256 != new string('1', 64))
            {
                throw new InvalidOperationException(
                    "Synthetic canary request contract failed: " + failure);
            }

            string ownedRequest = File.ReadAllText(
                PhysicalItemLogisticsPlayModeVerifier
                    .PreparedOutputWarehouseRequestPath);
            bool reentryRejected = false;
            try
            {
                RunCatalogFocused();
            }
            catch (InvalidOperationException exception)
            {
                reentryRejected = exception.Message.Contains(
                    "will not take ownership",
                    StringComparison.Ordinal);
            }
            if (!reentryRejected
                || !File.Exists(
                    PhysicalItemLogisticsPlayModeVerifier
                        .PreparedOutputWarehouseRequestPath)
                || !string.Equals(
                    ownedRequest,
                    File.ReadAllText(
                        PhysicalItemLogisticsPlayModeVerifier
                            .PreparedOutputWarehouseRequestPath),
                    StringComparison.Ordinal)
                || File.Exists(MarkerPath)
                || AssetDatabase.LoadMainAssetAtPath(ItemPath) != null
                || AssetDatabase.LoadMainAssetAtPath(RecipePath) != null)
            {
                throw new InvalidOperationException(
                    "A reentrant canary invocation changed a request/transaction it did not own.");
            }
        }
        finally
        {
            File.Delete(
                PhysicalItemLogisticsPlayModeVerifier.PreparedOutputWarehouseRequestPath);
        }

        Scene activeAfter = SceneManager.GetActiveScene();
        if (!string.Equals(activeBefore.path, activeAfter.path, StringComparison.Ordinal)
            || dirtyBefore != (activeAfter.IsValid() && activeAfter.isDirty)
            || File.Exists(MarkerPath)
            || AssetDatabase.LoadMainAssetAtPath(ItemPath) != null
            || AssetDatabase.LoadMainAssetAtPath(RecipePath) != null)
        {
            throw new InvalidOperationException(
                "Synthetic canary contract fixture changed scene or asset state.");
        }

        Directory.CreateDirectory("Artifacts/QA");
        File.WriteAllText(
            "Artifacts/QA/prepared-output-synthetic-canary-contract.txt",
            "Synthetic Prepared Output Canary Contract\n"
            + $"baselineItems={items.Definitions.Count}\n"
            + $"baselineRecipes={domain.Definitions.OfType<ProductionRecipeSO>().Count()}\n"
            + "requestRoundTrip=PASS\n"
            + "reentryOwnership=PASS\n"
            + "sceneMutation=0\n"
            + "assetMutation=0\n"
            + "RESULT=PASS; failures=0\n");
        Debug.Log(
            "Synthetic prepared-output canary contract focused PASS: request round-trip, "
            + "authoritative catalog parity, scene mutation=0, asset mutation=0.");
    }

    [MenuItem("DungeonStory/Debug/QA/Run Synthetic Prepared Output Catalog Focused")]
    public static void RunCatalogFocused()
    {
        if (File.Exists(MarkerPath)
            || File.Exists(
                PhysicalItemLogisticsPlayModeVerifier
                    .PreparedOutputWarehouseRequestPath))
        {
            throw new InvalidOperationException(
                "Synthetic canary catalog fixture will not take ownership of an active transaction/request.");
        }
        Scene activeBefore = SceneManager.GetActiveScene();
        bool dirtyBefore = activeBefore.IsValid() && activeBefore.isDirty;
        ItemDefinitionCatalogSO items = RequireAsset<ItemDefinitionCatalogSO>(
            ItemCatalogPath);
        GameDomainContentCatalogSO domain =
            RequireAsset<GameDomainContentCatalogSO>(DomainCatalogPath);
        int baselineItemCount = items.Definitions.Count;
        int baselineRecipeCount = domain.Definitions
            .OfType<ProductionRecipeSO>()
            .Count();
        V27PhysicalMassAuthorityInventoryDebugScenarios.EconomyDenominatorSnapshot
            baselineEconomy = V27PhysicalMassAuthorityInventoryDebugScenarios
                .CaptureCurrentEconomyDenominator();
        string baselineItemHash = ComputeFileSha256(ItemCatalogPath);
        string baselineDomainHash = ComputeFileSha256(DomainCatalogPath);
        string operationFailure = string.Empty;
        try
        {
            CanaryCatalogTransactionManifest manifest =
                PrepareCanaryAssets(baselineEconomy);
            PreparedOutputLiveRouteCase exact = new(
                isSynthetic: true,
                manifest.transactionId,
                manifest.recipeId,
                manifest.itemId,
                manifest.transactionNonce,
                manifest.itemAssetGuid,
                manifest.recipeAssetGuid,
                manifest.augmentedItemCatalogSha256,
                manifest.augmentedDomainCatalogSha256,
                manifest.transactionSourceSha256,
                manifest.verifierSourceSha256);
            if (!TryValidateActiveRunIdentity(exact, out string exactFailure))
            {
                throw new InvalidOperationException(
                    "Synthetic canary active identity rejected its exact transaction: "
                    + exactFailure);
            }
            PreparedOutputLiveRouteCase staleNonce = new(
                isSynthetic: true,
                exact.TransactionId,
                exact.RecipeId,
                exact.ItemId,
                new string('0', 32),
                exact.ItemAssetGuid,
                exact.RecipeAssetGuid,
                exact.AugmentedItemCatalogSha256,
                exact.AugmentedDomainCatalogSha256,
                exact.TransactionSourceSha256,
                exact.VerifierSourceSha256);
            if (TryValidateActiveRunIdentity(staleNonce, out _))
            {
                throw new InvalidOperationException(
                    "Synthetic canary accepted a stale transaction nonce.");
            }
        }
        catch (Exception exception)
        {
            operationFailure = exception.ToString();
        }

        bool cleaned = TryCleanup(
            writeFailureReport: true,
            out string cleanupFailure);
        if (operationFailure.Length > 0 || !cleaned)
        {
            throw new InvalidOperationException(
                "Synthetic canary catalog fixture failed. setup="
                + operationFailure
                + "; cleanup="
                + cleanupFailure);
        }

        items = RequireAsset<ItemDefinitionCatalogSO>(ItemCatalogPath);
        domain = RequireAsset<GameDomainContentCatalogSO>(DomainCatalogPath);
        ValidateBaselineAuthoritative(items, domain);
        V27PhysicalMassAuthorityInventoryDebugScenarios.EconomyDenominatorSnapshot
            cleanedEconomy = V27PhysicalMassAuthorityInventoryDebugScenarios
                .CaptureCurrentEconomyDenominator();
        cleanedEconomy.RequireExactIdentity(baselineEconomy);
        Scene activeAfter = SceneManager.GetActiveScene();
        if (items.Definitions.Count != baselineItemCount
            || domain.Definitions.OfType<ProductionRecipeSO>().Count()
                != baselineRecipeCount
            || !string.Equals(
                ComputeFileSha256(ItemCatalogPath),
                baselineItemHash,
                StringComparison.Ordinal)
            || !string.Equals(
                ComputeFileSha256(DomainCatalogPath),
                baselineDomainHash,
                StringComparison.Ordinal)
            || !string.Equals(
                activeBefore.path,
                activeAfter.path,
                StringComparison.Ordinal)
            || dirtyBefore != (activeAfter.IsValid() && activeAfter.isDirty))
        {
            throw new InvalidOperationException(
                "Synthetic canary catalog fixture did not restore its exact catalog/scene baseline.");
        }

        Directory.CreateDirectory("Artifacts/QA");
        File.WriteAllText(
            CatalogReportPath,
            "Synthetic Prepared Output Canary Catalog\n"
            + $"baselineItems={baselineItemCount}\n"
            + $"augmentedItems={baselineItemCount + 1}\n"
            + $"baselineRecipes={baselineRecipeCount}\n"
            + $"augmentedRecipes={baselineRecipeCount + 1}\n"
            + "catalogDelta=+1/+1\n"
            + $"baselineEconomyItems={baselineEconomy.EconomyItemIds.Count}\n"
            + $"augmentedEconomyItems={baselineEconomy.EconomyItemIds.Count + 1}\n"
            + $"baselineEconomyRecipes={baselineEconomy.EconomyRecipeIds.Count}\n"
            + $"augmentedEconomyRecipes={baselineEconomy.EconomyRecipeIds.Count + 1}\n"
            + $"baselineEwuItems={baselineEconomy.V27ItemIds.Count}\n"
            + $"augmentedEwuItems={baselineEconomy.V27ItemIds.Count + 1}\n"
            + $"baselineLedgerItems={baselineEconomy.LedgerItemIds.Count}\n"
            + $"augmentedLedgerItems={baselineEconomy.LedgerItemIds.Count + 1}\n"
            + $"baselineEwuRecipes={baselineEconomy.V27RecipeIds.Count}\n"
            + $"augmentedEwuRecipes={baselineEconomy.V27RecipeIds.Count + 1}\n"
            + $"baselineExternalEwuSeeds={baselineEconomy.V27ExternalSeedItemIds.Count}\n"
            + $"augmentedExternalEwuSeeds={baselineEconomy.V27ExternalSeedItemIds.Count}\n"
            + "economyItemExclusionReason=NON_RESOURCE_ITEM_DEFINITION\n"
            + "ewuRecipeExclusionReason=SINK_OR_NO_OUTPUT\n"
            + "externalEwuSeedReason=CATALOG_RECIPE_INPUT_WITHOUT_PRODUCER\n"
            + "economyLedgerEwuDelta=+1/+1\n"
            + "externalEwuSeedDelta=0\n"
            + "economyLedgerEwuCleanup=EXACT_BASELINE\n"
            + "dynamicPreparedProfileAudit=PASS\n"
            + "transactionIdentity=PASS\n"
            + "staleNonceRejected=PASS\n"
            + "cleanupExact=PASS\n"
            + "sceneMutation=0\n"
            + "RESULT=PASS; failures=0\n");
        Debug.Log(
            "Synthetic prepared-output canary catalog focused PASS: exact +1/+1, "
            + "dynamic profile audit, exact cleanup.");
    }

    [MenuItem("DungeonStory/Debug/QA/Recover Pending Synthetic Prepared Output Canary")]
    public static void RecoverPendingTransactionFocused()
    {
        if (!TryCleanup(writeFailureReport: true, out string failure))
        {
            throw new InvalidOperationException(
                "Synthetic canary recovery failed: " + failure);
        }
        Debug.Log("Pending synthetic prepared-output canary transaction recovered.");
    }

    internal static bool HasActiveTransaction => File.Exists(MarkerPath);

    internal static bool HasPendingDurableRun =>
        File.Exists(DispatchRequestPath)
        || File.Exists(MarkerPath)
        || File.Exists(
            PhysicalItemLogisticsPlayModeVerifier
                .PreparedOutputWarehouseRequestPath);

    private static void PrepareAndRequest()
    {
        CanaryCatalogTransactionManifest manifest =
            PrepareCanaryAssets();
        PhysicalItemLogisticsPlayModeVerifier
            .RequestSyntheticPreparedOutputWarehouseRun(
                TransactionId,
                RecipeId,
                ItemId,
                manifest.transactionNonce,
                manifest.itemAssetGuid,
                manifest.recipeAssetGuid,
                manifest.augmentedItemCatalogSha256,
                manifest.augmentedDomainCatalogSha256,
                manifest.transactionSourceSha256,
                manifest.verifierSourceSha256);
    }

    private static CanaryCatalogTransactionManifest PrepareCanaryAssets(
        V27PhysicalMassAuthorityInventoryDebugScenarios.EconomyDenominatorSnapshot
            baselineEconomy = null)
    {
        RequireSafePreflight();
        ItemDefinitionCatalogSO itemCatalog = RequireAsset<ItemDefinitionCatalogSO>(
            ItemCatalogPath);
        GameDomainContentCatalogSO domainCatalog =
            RequireAsset<GameDomainContentCatalogSO>(DomainCatalogPath);
        ValidateBaselineAuthoritative(itemCatalog, domainCatalog);
        baselineEconomy ??= V27PhysicalMassAuthorityInventoryDebugScenarios
            .CaptureCurrentEconomyDenominator();

        byte[] itemCatalogBytes = File.ReadAllBytes(
            Path.GetFullPath(ItemCatalogPath));
        byte[] domainCatalogBytes = File.ReadAllBytes(
            Path.GetFullPath(DomainCatalogPath));
        string transactionNonce = Guid.NewGuid().ToString("N");
        string projectRootDigest = ComputeTextSha256(
            Path.GetFullPath(".").TrimEnd(Path.DirectorySeparatorChar)
                .ToUpperInvariant());
        string recoveryDirectory = GetRecoveryDirectory(
            projectRootDigest,
            transactionNonce);
        CanaryCatalogTransactionManifest manifest = new()
        {
            schemaVersion = SchemaVersion,
            transactionId = TransactionId,
            transactionNonce = transactionNonce,
            projectRootDigest = projectRootDigest,
            itemPath = ItemPath,
            recipePath = RecipePath,
            itemId = ItemId,
            recipeId = RecipeId,
            itemCatalogPath = ItemCatalogPath,
            domainCatalogPath = DomainCatalogPath,
            itemCatalogSha256 = ComputeSha256(itemCatalogBytes),
            domainCatalogSha256 = ComputeSha256(domainCatalogBytes),
            itemCatalogByteLength = itemCatalogBytes.LongLength,
            domainCatalogByteLength = domainCatalogBytes.LongLength,
            itemCatalogBackupPath = Path.Combine(
                recoveryDirectory,
                "item-catalog.bin.gz"),
            domainCatalogBackupPath = Path.Combine(
                recoveryDirectory,
                "domain-catalog.bin.gz"),
            baselineItemCount = itemCatalog.Definitions.Count,
            baselineDomainCount = domainCatalog.Definitions.Count,
            baselineRecipeCount = domainCatalog.Definitions
                .OfType<ProductionRecipeSO>()
                .Count(),
            baselineItemIdDigest = CaptureCanonicalDigest(
                itemCatalog.Definitions.Select(value => value.ItemId)),
            baselineRecipeIdDigest = CaptureCanonicalDigest(
                domainCatalog.Definitions
                    .OfType<ProductionRecipeSO>()
                    .Select(value => value.RecipeId)),
            baselineItemReferenceDigest = CaptureReferenceDigest(
                itemCatalog.Definitions),
            baselineDomainReferenceDigest = CaptureReferenceDigest(
                domainCatalog.Definitions)
        };
        PrepareRecoveryAuthority(
            manifest,
            itemCatalogBytes,
            domainCatalogBytes);

        bool editing = false;
        try
        {
            AssetDatabase.StartAssetEditing();
            editing = true;
            ResourceItemDefinitionSO item =
                ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
            item.name = "qa_v27_definition_only_canary";
            item.Configure(
                ItemId,
                "QA Definition-Only Output",
                "Temporary expansion-closure canary; deleted after verification.",
                StockCategory.General,
                ResourceItemKind.FinishedGood,
                ResourceIngredientTag.None,
                price: 1,
                weight: 1f,
                stackLimit: 20,
                researchId: string.Empty);
            item.ConfigureMarketSaleRate(0.5f);
            AssetDatabase.CreateAsset(item, ItemPath);

            ProductionRecipeSO recipe =
                ScriptableObject.CreateInstance<ProductionRecipeSO>();
            recipe.name = "qa_v27_definition_only_canary";
            recipe.Configure(
                RecipeId,
                "QA Definition-Only Output Recipe",
                "Temporary full-path prepared-output canary.",
                requiredFacilityTag: "feedbench",
                requiredWorkTypeId: "work:craft",
                researchId: string.Empty,
                work: 1f,
                recipeInputs: new[]
                {
                    new ItemAmountDefinition("resource:grass-straw", 1)
                },
                recipeOutputs: new[]
                {
                    new ProductionOutputDefinition(
                        "output:main",
                        ProductionOutputRole.Main,
                        ItemId,
                        amount: 20,
                        probability: 1f)
                });
            recipe.ConfigureWorkshop(
                "workstation:feedbench",
                Array.Empty<string>(),
                ProductionProcessKind.WorkOnly);
            recipe.ConfigureProficiency(BuiltInCharacterProficiencyIds.FoodProduction);
            recipe.ConfigureProcessClass(ProductionProcessClass.CookingSimpleMixing);
            AssetDatabase.CreateAsset(recipe, RecipePath);
        }
        finally
        {
            if (editing)
                AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        GameContentCatalogAssetBuilder.ReindexItemDefinitions();
        GameContentCatalogAssetBuilder.ReindexProductionRecipes();
        AssetDatabase.SaveAssetIfDirty(itemCatalog);
        AssetDatabase.SaveAssetIfDirty(domainCatalog);
        AssetDatabase.ImportAsset(ItemCatalogPath, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(DomainCatalogPath, ImportAssetOptions.ForceSynchronousImport);
        ValidateAugmentedCatalogs(manifest, baselineEconomy);
        manifest.itemAssetGuid = AssetDatabase.AssetPathToGUID(ItemPath);
        manifest.recipeAssetGuid = AssetDatabase.AssetPathToGUID(RecipePath);
        manifest.augmentedItemCatalogSha256 = ComputeFileSha256(ItemCatalogPath);
        manifest.augmentedDomainCatalogSha256 = ComputeFileSha256(DomainCatalogPath);
        manifest.transactionSourceSha256 = ComputeFileSha256(TransactionSourcePath);
        manifest.verifierSourceSha256 = ComputeFileSha256(VerifierSourcePath);
        ValidateManifest(manifest, requireAugmentedIdentity: true);
        WriteMarkerAtomically(manifest);
        return manifest;
    }

    internal static bool TryValidateActiveRunIdentity(
        PreparedOutputLiveRouteCase verificationCase,
        out string failure)
    {
        failure = string.Empty;
        if (!verificationCase.IsSynthetic)
            return true;
        try
        {
            if (!File.Exists(MarkerPath))
                throw new InvalidOperationException("transaction marker is missing");
            CanaryCatalogTransactionManifest manifest = JsonUtility.FromJson<
                CanaryCatalogTransactionManifest>(File.ReadAllText(MarkerPath));
            ValidateManifest(manifest, requireAugmentedIdentity: true);
            bool exact = string.Equals(
                    verificationCase.TransactionId,
                    manifest.transactionId,
                    StringComparison.Ordinal)
                && string.Equals(
                    verificationCase.TransactionNonce,
                    manifest.transactionNonce,
                    StringComparison.Ordinal)
                && string.Equals(
                    verificationCase.RecipeId,
                    manifest.recipeId,
                    StringComparison.Ordinal)
                && string.Equals(
                    verificationCase.ItemId,
                    manifest.itemId,
                    StringComparison.Ordinal)
                && string.Equals(
                    verificationCase.ItemAssetGuid,
                    manifest.itemAssetGuid,
                    StringComparison.Ordinal)
                && string.Equals(
                    verificationCase.RecipeAssetGuid,
                    manifest.recipeAssetGuid,
                    StringComparison.Ordinal)
                && string.Equals(
                    verificationCase.AugmentedItemCatalogSha256,
                    manifest.augmentedItemCatalogSha256,
                    StringComparison.Ordinal)
                && string.Equals(
                    verificationCase.AugmentedDomainCatalogSha256,
                    manifest.augmentedDomainCatalogSha256,
                    StringComparison.Ordinal)
                && string.Equals(
                    verificationCase.TransactionSourceSha256,
                    manifest.transactionSourceSha256,
                    StringComparison.Ordinal)
                && string.Equals(
                    verificationCase.VerifierSourceSha256,
                    manifest.verifierSourceSha256,
                    StringComparison.Ordinal)
                && string.Equals(
                    AssetDatabase.AssetPathToGUID(manifest.itemPath),
                    manifest.itemAssetGuid,
                    StringComparison.Ordinal)
                && string.Equals(
                    AssetDatabase.AssetPathToGUID(manifest.recipePath),
                    manifest.recipeAssetGuid,
                    StringComparison.Ordinal)
                && string.Equals(
                    ComputeFileSha256(manifest.itemCatalogPath),
                    manifest.augmentedItemCatalogSha256,
                    StringComparison.Ordinal)
                && string.Equals(
                    ComputeFileSha256(manifest.domainCatalogPath),
                    manifest.augmentedDomainCatalogSha256,
                    StringComparison.Ordinal)
                && string.Equals(
                    ComputeFileSha256(TransactionSourcePath),
                    manifest.transactionSourceSha256,
                    StringComparison.Ordinal)
                && string.Equals(
                    ComputeFileSha256(VerifierSourcePath),
                    manifest.verifierSourceSha256,
                    StringComparison.Ordinal);
            if (!exact)
                throw new InvalidOperationException("request/marker/asset/catalog/source identity drift");
            return true;
        }
        catch (Exception exception)
        {
            failure = exception.Message;
            return false;
        }
    }

    private static void RequireSafePreflight()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException(
                "Synthetic canary setup requires stable EditMode.");
        }
        if (File.Exists(MarkerPath))
        {
            throw new InvalidOperationException(
                "A synthetic canary transaction marker already exists; cleanup must finish first.");
        }
        if (File.Exists(
            PhysicalItemLogisticsPlayModeVerifier.PreparedOutputWarehouseRequestPath))
        {
            throw new InvalidOperationException(
                "A prepared-output verifier request already exists; this invocation does not own it.");
        }
        if (AssetDatabase.LoadMainAssetAtPath(ItemPath) != null
            || AssetDatabase.LoadMainAssetAtPath(RecipePath) != null
            || File.Exists(ItemPath)
            || File.Exists(RecipePath))
        {
            throw new InvalidOperationException(
                "Synthetic canary asset paths are occupied; no existing asset will be overwritten.");
        }
        ItemDefinitionCatalogSO itemCatalog = RequireAsset<ItemDefinitionCatalogSO>(
            ItemCatalogPath);
        GameDomainContentCatalogSO domainCatalog =
            RequireAsset<GameDomainContentCatalogSO>(DomainCatalogPath);
        if (EditorUtility.IsDirty(itemCatalog) || EditorUtility.IsDirty(domainCatalog))
        {
            throw new InvalidOperationException(
                "Synthetic canary refused because an authoritative catalog has unsaved edits.");
        }
    }

    private static void ValidateBaselineAuthoritative(
        ItemDefinitionCatalogSO itemCatalog,
        GameDomainContentCatalogSO domainCatalog)
    {
        ItemDefinitionSO[] discoveredItems = AssetDatabase
            .FindAssets("t:ItemDefinitionSO", new[] { "Assets/Resources/SO" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>)
            .Where(value => value != null)
            .Distinct()
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        ProductionRecipeSO[] discoveredRecipes = AssetDatabase
            .FindAssets("t:ProductionRecipeSO", new[]
            {
                "Assets/Resources/SO/Economy/Recipes"
            })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>)
            .Where(value => value != null)
            .Distinct()
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        bool itemsExact = CaptureReferenceTokens(discoveredItems)
            .SequenceEqual(
                CaptureReferenceTokens(itemCatalog.Definitions),
                StringComparer.Ordinal);
        bool recipesExact = CaptureReferenceTokens(discoveredRecipes)
            .SequenceEqual(
                CaptureReferenceTokens(domainCatalog.Definitions.OfType<ProductionRecipeSO>()),
                StringComparer.Ordinal);
        if (!itemsExact || !recipesExact)
        {
            throw new InvalidOperationException(
                "Synthetic canary requires authoritative item/recipe catalogs to match disk exactly.");
        }
        if (itemCatalog.Definitions.Any(value => value.ItemId == ItemId)
            || domainCatalog.Definitions.OfType<ProductionRecipeSO>()
                .Any(value => value.RecipeId == RecipeId))
        {
            throw new InvalidOperationException(
                "Synthetic canary stable IDs already exist in the authoritative catalogs.");
        }
    }

    private static void ValidateAugmentedCatalogs(
        CanaryCatalogTransactionManifest manifest,
        V27PhysicalMassAuthorityInventoryDebugScenarios.EconomyDenominatorSnapshot
            baselineEconomy)
    {
        ItemDefinitionCatalogSO itemCatalog = RequireAsset<ItemDefinitionCatalogSO>(
            manifest.itemCatalogPath);
        GameDomainContentCatalogSO domainCatalog =
            RequireAsset<GameDomainContentCatalogSO>(manifest.domainCatalogPath);
        ItemDefinitionSO[] baselineItems = itemCatalog.Definitions
            .Where(value => value != null
                && !string.Equals(value.ItemId, ItemId, StringComparison.Ordinal))
            .ToArray();
        ScriptableObject[] baselineDomain = domainCatalog.Definitions
            .Where(value => value != null
                && (value is not ProductionRecipeSO recipe
                    || !string.Equals(
                        recipe.RecipeId,
                        RecipeId,
                        StringComparison.Ordinal)))
            .ToArray();
        ProductionRecipeSO[] baselineRecipes = baselineDomain
            .OfType<ProductionRecipeSO>()
            .ToArray();
        bool exactOwnedDelta = itemCatalog.Definitions.Count
                == manifest.baselineItemCount + 1
            && domainCatalog.Definitions.Count
                == manifest.baselineDomainCount + 1
            && domainCatalog.Definitions.OfType<ProductionRecipeSO>().Count()
                == manifest.baselineRecipeCount + 1
            && itemCatalog.Definitions.Count(value => value != null
                && string.Equals(value.ItemId, ItemId, StringComparison.Ordinal)) == 1
            && domainCatalog.Definitions.OfType<ProductionRecipeSO>().Count(value =>
                string.Equals(value.RecipeId, RecipeId, StringComparison.Ordinal)) == 1
            && string.Equals(
                CaptureCanonicalDigest(baselineItems.Select(value => value.ItemId)),
                manifest.baselineItemIdDigest,
                StringComparison.Ordinal)
            && string.Equals(
                CaptureCanonicalDigest(baselineRecipes.Select(value => value.RecipeId)),
                manifest.baselineRecipeIdDigest,
                StringComparison.Ordinal)
            && string.Equals(
                CaptureReferenceDigest(baselineItems),
                manifest.baselineItemReferenceDigest,
                StringComparison.Ordinal)
            && string.Equals(
                CaptureReferenceDigest(baselineDomain),
                manifest.baselineDomainReferenceDigest,
                StringComparison.Ordinal);
        if (!exactOwnedDelta)
        {
            throw new InvalidOperationException(
                "Synthetic canary catalog delta is not exactly +1 item/+1 recipe.");
        }

        IGameContentCatalog root = new ResourceGameContentCatalog(
            new UnityGameContentRootLoader());
        ResourceEconomyContentCatalog economy = new(root);
        if (!economy.TryGetItem(ItemId, out ResourceItemDefinitionSO item)
            || !economy.TryGetRecipe(RecipeId, out ProductionRecipeSO recipe)
            || item == null
            || recipe == null
            || economy.Items.Count(value => value.ItemId == ItemId) != 1
            || economy.Recipes.Count(value => value.RecipeId == RecipeId) != 1)
        {
            throw new InvalidOperationException(
                "Runtime content projection did not expose exactly one synthetic item and recipe.");
        }

        V27PhysicalMassAuthorityInventoryDebugScenarios.EconomyDenominatorSnapshot
            augmentedEconomy = V27PhysicalMassAuthorityInventoryDebugScenarios
                .CaptureCurrentEconomyDenominator();
        augmentedEconomy.RequireExactAugmentationOf(
            baselineEconomy,
            ItemId,
            RecipeId);

        // This suite discovers every current physical-output recipe dynamically. Running
        // it while the temporary assets are installed proves that a definition-only
        // addition joins the prepared-output capability profile without a recipe-ID
        // allowlist or a fixed-count test edit.
        ProductionPreparedOutputMigrationProfileDebugScenarios.RunAll();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += RecoverAfterPlayMode;
    }

    private static void RecoverAfterPlayMode() =>
        TryCleanup(writeFailureReport: true, out _);

    private static void RecoverIfSafe()
    {
        if (!File.Exists(MarkerPath)
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }
        if (File.Exists(
                PhysicalItemLogisticsPlayModeVerifier.PreparedOutputWarehouseRequestPath)
            && !File.Exists(
                PhysicalItemLogisticsPlayModeVerifier.PreparedOutputWarehouseReportPath))
        {
            return;
        }
        TryCleanup(writeFailureReport: true, out _);
    }

    private static bool TryCleanup(bool writeFailureReport, out string failure)
    {
        failure = string.Empty;
        if (!File.Exists(MarkerPath))
            return true;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            failure = "cleanup deferred until stable EditMode";
            return false;
        }

        try
        {
            CanaryCatalogTransactionManifest manifest = JsonUtility.FromJson<
                CanaryCatalogTransactionManifest>(File.ReadAllText(MarkerPath));
            ValidateManifest(manifest);
            ValidateOwnedAsset<ProductionRecipeSO>(RecipePath, RecipeId,
                value => value.RecipeId);
            ValidateOwnedAsset<ResourceItemDefinitionSO>(ItemPath, ItemId,
                value => value.ItemId);

            ItemDefinitionCatalogSO itemCatalog = RequireAsset<ItemDefinitionCatalogSO>(
                manifest.itemCatalogPath);
            GameDomainContentCatalogSO domainCatalog =
                RequireAsset<GameDomainContentCatalogSO>(manifest.domainCatalogPath);
            ValidateOwnedCatalogDelta(manifest, itemCatalog, domainCatalog);
            byte[] itemCatalogBytes = ReadAndValidateBackup(
                manifest.itemCatalogBackupPath,
                manifest.itemCatalogBackupCompressedLength,
                manifest.itemCatalogBackupCompressedSha256,
                manifest.itemCatalogByteLength,
                manifest.itemCatalogSha256);
            byte[] domainCatalogBytes = ReadAndValidateBackup(
                manifest.domainCatalogBackupPath,
                manifest.domainCatalogBackupCompressedLength,
                manifest.domainCatalogBackupCompressedSha256,
                manifest.domainCatalogByteLength,
                manifest.domainCatalogSha256);

            // Restore the exact pre-transaction YAML bytes before deleting the owned
            // assets. A crash between the two catalog replaces is recoverable because
            // the marker and both independently verified backups remain authoritative.
            RestoreFileAtomically(manifest.itemCatalogPath, itemCatalogBytes);
            RestoreFileAtomically(manifest.domainCatalogPath, domainCatalogBytes);
            AssetDatabase.ImportAsset(
                manifest.itemCatalogPath,
                ImportAssetOptions.ForceSynchronousImport
                    | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(
                manifest.domainCatalogPath,
                ImportAssetOptions.ForceSynchronousImport
                    | ImportAssetOptions.ForceUpdate);

            if (AssetDatabase.LoadMainAssetAtPath(RecipePath) != null
                && !AssetDatabase.DeleteAsset(RecipePath))
            {
                throw new InvalidOperationException("Failed to delete the owned canary recipe asset.");
            }
            if (AssetDatabase.LoadMainAssetAtPath(ItemPath) != null
                && !AssetDatabase.DeleteAsset(ItemPath))
            {
                throw new InvalidOperationException("Failed to delete the owned canary item asset.");
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            itemCatalog = RequireAsset<ItemDefinitionCatalogSO>(
                manifest.itemCatalogPath);
            domainCatalog = RequireAsset<GameDomainContentCatalogSO>(
                manifest.domainCatalogPath);
            ValidateCleanupResult(manifest, itemCatalog, domainCatalog);
            string itemHash = ComputeFileSha256(manifest.itemCatalogPath);
            string domainHash = ComputeFileSha256(manifest.domainCatalogPath);

            Directory.CreateDirectory("Artifacts/QA");
            File.WriteAllText(
                CleanupReportPath,
                "Synthetic Prepared Output Canary Cleanup\n"
                + $"transaction={TransactionId}\n"
                + $"itemCatalogSha256={itemHash}\n"
                + $"domainCatalogSha256={domainHash}\n"
                + "assetPathsRemaining=0\n"
                + "exactByteRestore=PASS\n"
                + "RESULT=PASS; failures=0\n");
            File.Delete(
                PhysicalItemLogisticsPlayModeVerifier.PreparedOutputWarehouseRequestPath);
            File.Delete(MarkerPath);
            DeleteRecoveryAuthority(manifest);
            return true;
        }
        catch (Exception exception)
        {
            failure = exception.ToString();
            if (writeFailureReport)
            {
                Directory.CreateDirectory("Artifacts/QA");
                File.WriteAllText(
                    CleanupReportPath,
                    "Synthetic Prepared Output Canary Cleanup\n"
                    + "[FAIL] " + failure + "\n"
                    + "RESULT=FAIL; failures=1\n");
            }
            Debug.LogError(
                "Synthetic prepared-output canary cleanup remains pending: "
                + failure);
            return false;
        }
    }

    private static void ValidateCleanupResult(
        CanaryCatalogTransactionManifest manifest,
        ItemDefinitionCatalogSO itemCatalog,
        GameDomainContentCatalogSO domainCatalog)
    {
        if (AssetDatabase.LoadMainAssetAtPath(ItemPath) != null
            || AssetDatabase.LoadMainAssetAtPath(RecipePath) != null
            || itemCatalog.Definitions.Any(value => value != null
                && value.ItemId == ItemId)
            || domainCatalog.Definitions.OfType<ProductionRecipeSO>()
                .Any(value => value.RecipeId == RecipeId))
        {
            throw new InvalidOperationException(
                "Synthetic canary asset or catalog reference remained after cleanup.");
        }
        if (itemCatalog.Definitions.Count != manifest.baselineItemCount
            || domainCatalog.Definitions.Count != manifest.baselineDomainCount
            || domainCatalog.Definitions.OfType<ProductionRecipeSO>().Count()
                != manifest.baselineRecipeCount
            || !string.Equals(
                CaptureCanonicalDigest(
                    itemCatalog.Definitions.Select(value => value.ItemId)),
                manifest.baselineItemIdDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                CaptureCanonicalDigest(
                    domainCatalog.Definitions
                        .OfType<ProductionRecipeSO>()
                        .Select(value => value.RecipeId)),
                manifest.baselineRecipeIdDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                CaptureReferenceDigest(itemCatalog.Definitions),
                manifest.baselineItemReferenceDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                CaptureReferenceDigest(domainCatalog.Definitions),
                manifest.baselineDomainReferenceDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                ComputeFileSha256(manifest.itemCatalogPath),
                manifest.itemCatalogSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                ComputeFileSha256(manifest.domainCatalogPath),
                manifest.domainCatalogSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Synthetic canary cleanup did not restore the exact baseline catalogs.");
        }
        V27PhysicalMassAuthorityInventoryDebugScenarios.EconomyDenominatorSnapshot
            cleanedEconomy = V27PhysicalMassAuthorityInventoryDebugScenarios
                .CaptureCurrentEconomyDenominator();
        cleanedEconomy.RequireAbsent(ItemId, RecipeId);
    }

    private static void ValidateOwnedCatalogDelta(
        CanaryCatalogTransactionManifest manifest,
        ItemDefinitionCatalogSO itemCatalog,
        GameDomainContentCatalogSO domainCatalog)
    {
        ItemDefinitionSO[] currentItems = itemCatalog.Definitions.ToArray();
        ScriptableObject[] currentDomain = domainCatalog.Definitions.ToArray();
        int missingItemReferences = currentItems.Count(value => value == null);
        int missingDomainReferences = currentDomain.Count(value => value == null);
        bool itemAssetAlreadyDeleted =
            AssetDatabase.LoadMainAssetAtPath(ItemPath) == null;
        bool recipeAssetAlreadyDeleted =
            AssetDatabase.LoadMainAssetAtPath(RecipePath) == null;
        bool recoverableDeletedReferences =
            (missingItemReferences == 0
                || (missingItemReferences == 1 && itemAssetAlreadyDeleted))
            && (missingDomainReferences == 0
                || (missingDomainReferences == 1 && recipeAssetAlreadyDeleted));
        ItemDefinitionSO[] baselineItems = currentItems
            .Where(value => value != null && !string.Equals(
                value.ItemId,
                ItemId,
                StringComparison.Ordinal))
            .ToArray();
        ScriptableObject[] baselineDomain = currentDomain
            .Where(value => value != null
                && (value is not ProductionRecipeSO recipe
                || !string.Equals(recipe.RecipeId, RecipeId, StringComparison.Ordinal)))
            .ToArray();
        ProductionRecipeSO[] baselineRecipes = baselineDomain
            .OfType<ProductionRecipeSO>()
            .ToArray();
        bool catalogShapeOwned =
            (currentItems.Length == manifest.baselineItemCount
                || currentItems.Length == manifest.baselineItemCount + 1)
            && (currentDomain.Length == manifest.baselineDomainCount
                || currentDomain.Length == manifest.baselineDomainCount + 1)
            && baselineItems.Length == manifest.baselineItemCount
            && baselineDomain.Length == manifest.baselineDomainCount
            && baselineRecipes.Length == manifest.baselineRecipeCount;
        if (!recoverableDeletedReferences
            || !catalogShapeOwned
            || string.Equals(
                CaptureCanonicalDigest(baselineItems.Select(value => value.ItemId)),
                manifest.baselineItemIdDigest,
                StringComparison.Ordinal) == false
            || string.Equals(
                CaptureCanonicalDigest(baselineRecipes.Select(value => value.RecipeId)),
                manifest.baselineRecipeIdDigest,
                StringComparison.Ordinal) == false
            || string.Equals(
                CaptureReferenceDigest(baselineItems),
                manifest.baselineItemReferenceDigest,
                StringComparison.Ordinal) == false
            || string.Equals(
                CaptureReferenceDigest(baselineDomain),
                manifest.baselineDomainReferenceDigest,
                StringComparison.Ordinal) == false)
        {
            throw new InvalidOperationException(
                "Canary cleanup refused because catalog changes are not limited to the owned item/recipe delta.");
        }
    }

    private static void ValidateManifest(
        CanaryCatalogTransactionManifest manifest,
        bool requireAugmentedIdentity = false)
    {
        if (manifest == null
            || manifest.schemaVersion != SchemaVersion
            || !string.Equals(manifest.transactionId, TransactionId, StringComparison.Ordinal)
            || !string.Equals(manifest.itemPath, ItemPath, StringComparison.Ordinal)
            || !string.Equals(manifest.recipePath, RecipePath, StringComparison.Ordinal)
            || !string.Equals(manifest.itemId, ItemId, StringComparison.Ordinal)
            || !string.Equals(manifest.recipeId, RecipeId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(manifest.transactionNonce)
            || manifest.transactionNonce.Length != 32
            || !manifest.transactionNonce.All(Uri.IsHexDigit)
            || !IsSha256(manifest.projectRootDigest)
            || !string.Equals(
                manifest.projectRootDigest,
                ComputeTextSha256(
                    Path.GetFullPath(".").TrimEnd(Path.DirectorySeparatorChar)
                        .ToUpperInvariant()),
                StringComparison.Ordinal)
            || manifest.baselineItemCount <= 0
            || manifest.baselineDomainCount <= 0
            || manifest.baselineRecipeCount <= 0
            || manifest.itemCatalogByteLength <= 0
            || manifest.domainCatalogByteLength <= 0
            || manifest.itemCatalogBackupCompressedLength <= 0
            || manifest.domainCatalogBackupCompressedLength <= 0
            || !IsSha256(manifest.itemCatalogSha256)
            || !IsSha256(manifest.domainCatalogSha256)
            || !IsSha256(manifest.itemCatalogBackupCompressedSha256)
            || !IsSha256(manifest.domainCatalogBackupCompressedSha256)
            || !IsSha256(manifest.baselineItemIdDigest)
            || !IsSha256(manifest.baselineRecipeIdDigest)
            || !IsSha256(manifest.baselineItemReferenceDigest)
            || !IsSha256(manifest.baselineDomainReferenceDigest)
            || !IsExpectedBackupPath(
                manifest.itemCatalogBackupPath,
                manifest.projectRootDigest,
                manifest.transactionNonce,
                "item-catalog.bin.gz")
            || !IsExpectedBackupPath(
                manifest.domainCatalogBackupPath,
                manifest.projectRootDigest,
                manifest.transactionNonce,
                "domain-catalog.bin.gz")
            || requireAugmentedIdentity
                && (!IsGuidToken(manifest.itemAssetGuid)
                    || !IsGuidToken(manifest.recipeAssetGuid)
                    || !IsSha256(manifest.augmentedItemCatalogSha256)
                    || !IsSha256(manifest.augmentedDomainCatalogSha256)
                    || !IsSha256(manifest.transactionSourceSha256)
                    || !IsSha256(manifest.verifierSourceSha256)))
        {
            throw new InvalidOperationException(
                "Synthetic canary transaction marker is missing or does not own this transaction.");
        }
    }

    private static void ValidateOwnedAsset<T>(
        string path,
        string expectedId,
        Func<T, string> idSelector)
        where T : UnityEngine.Object
    {
        UnityEngine.Object current = AssetDatabase.LoadMainAssetAtPath(path);
        if (current == null)
            return;
        if (current is not T typed
            || !string.Equals(idSelector(typed), expectedId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Canary path '{path}' is occupied by an asset not owned by this transaction.");
        }
    }

    private static string[] CaptureReferenceTokens<T>(IEnumerable<T> values)
        where T : UnityEngine.Object
        => (values ?? Array.Empty<T>())
            .Select(value =>
            {
                if (value == null
                    || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        value,
                        out string guid,
                        out long localId))
                {
                    throw new InvalidOperationException(
                        $"Catalog object '{value?.name ?? "<null>"}' has no stable asset identity.");
                }
                return guid + ":" + localId;
            })
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static string CaptureReferenceDigest<T>(IEnumerable<T> values)
        where T : UnityEngine.Object => CaptureCanonicalDigest(
            CaptureReferenceTokens(values));

    private static string CaptureCanonicalDigest(IEnumerable<string> values)
    {
        string canonical = string.Join(
            "\n",
            (values ?? Array.Empty<string>())
                .OrderBy(value => value, StringComparer.Ordinal));
        using SHA256 sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(
                System.Text.Encoding.UTF8.GetBytes(canonical)))
            .Replace("-", string.Empty);
    }

    private static bool IsSha256(string value) =>
        !string.IsNullOrEmpty(value)
        && value.Length == 64
        && value.All(character =>
            character is >= '0' and <= '9'
            || character is >= 'A' and <= 'F'
            || character is >= 'a' and <= 'f');

    private static bool IsGuidToken(string value) =>
        !string.IsNullOrEmpty(value)
        && value.Length == 32
        && value.All(Uri.IsHexDigit);

    private static T RequireAsset<T>(string path) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(path)
        ?? throw new InvalidOperationException(
            $"Required canary transaction asset '{path}' is missing.");

    private static string ComputeFileSha256(string projectRelativePath)
    {
        string fullPath = Path.GetFullPath(projectRelativePath);
        using FileStream stream = File.OpenRead(fullPath);
        using SHA256 sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(stream))
            .Replace("-", string.Empty);
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using SHA256 sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes))
            .Replace("-", string.Empty);
    }

    private static string ComputeTextSha256(string value) =>
        ComputeSha256(Encoding.UTF8.GetBytes(value ?? string.Empty));

    private static string GetRecoveryRoot() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DungeonStory",
        RecoveryDirectoryName);

    private static string GetRecoveryDirectory(
        string projectRootDigest,
        string transactionNonce) => Path.Combine(
            GetRecoveryRoot(),
            projectRootDigest,
            transactionNonce);

    private static bool IsExpectedBackupPath(
        string path,
        string projectRootDigest,
        string transactionNonce,
        string expectedFileName)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        string expected = Path.GetFullPath(Path.Combine(
            GetRecoveryDirectory(projectRootDigest, transactionNonce),
            expectedFileName));
        return string.Equals(
            Path.GetFullPath(path),
            expected,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void PrepareRecoveryAuthority(
        CanaryCatalogTransactionManifest manifest,
        byte[] itemCatalogBytes,
        byte[] domainCatalogBytes)
    {
        string recoveryDirectory = Path.GetDirectoryName(
            manifest.itemCatalogBackupPath)
            ?? throw new InvalidOperationException(
                "Synthetic canary recovery directory is missing.");
        Directory.CreateDirectory(recoveryDirectory);
        bool markerPublished = false;
        try
        {
            WriteCompressedBackupAtomically(
                manifest.itemCatalogBackupPath,
                itemCatalogBytes,
                out manifest.itemCatalogBackupCompressedLength,
                out manifest.itemCatalogBackupCompressedSha256);
            WriteCompressedBackupAtomically(
                manifest.domainCatalogBackupPath,
                domainCatalogBytes,
                out manifest.domainCatalogBackupCompressedLength,
                out manifest.domainCatalogBackupCompressedSha256);
            ReadAndValidateBackup(
                manifest.itemCatalogBackupPath,
                manifest.itemCatalogBackupCompressedLength,
                manifest.itemCatalogBackupCompressedSha256,
                manifest.itemCatalogByteLength,
                manifest.itemCatalogSha256);
            ReadAndValidateBackup(
                manifest.domainCatalogBackupPath,
                manifest.domainCatalogBackupCompressedLength,
                manifest.domainCatalogBackupCompressedSha256,
                manifest.domainCatalogByteLength,
                manifest.domainCatalogSha256);
            WriteMarkerAtomically(manifest);
            CanaryCatalogTransactionManifest readBack = JsonUtility.FromJson<
                CanaryCatalogTransactionManifest>(File.ReadAllText(MarkerPath));
            ValidateManifest(readBack);
            markerPublished = true;
        }
        finally
        {
            if (!markerPublished && !File.Exists(MarkerPath))
                DeleteRecoveryAuthority(manifest);
        }
    }

    private static void WriteCompressedBackupAtomically(
        string path,
        byte[] rawBytes,
        out long compressedLength,
        out string compressedSha256)
    {
        string pending = path + ".pending";
        using (FileStream file = new(
                   pending,
                   FileMode.CreateNew,
                   FileAccess.Write,
                   FileShare.None))
        {
            using (GZipStream gzip = new(
                       file,
                       System.IO.Compression.CompressionLevel.Optimal,
                       leaveOpen: true))
            {
                gzip.Write(rawBytes, 0, rawBytes.Length);
            }
            file.Flush(flushToDisk: true);
        }
        File.Move(pending, path);
        byte[] compressed = File.ReadAllBytes(path);
        compressedLength = compressed.LongLength;
        compressedSha256 = ComputeSha256(compressed);
    }

    private static byte[] ReadAndValidateBackup(
        string path,
        long expectedCompressedLength,
        string expectedCompressedSha256,
        long expectedRawLength,
        string expectedRawSha256)
    {
        byte[] compressed = File.ReadAllBytes(path);
        if (compressed.LongLength != expectedCompressedLength
            || !string.Equals(
                ComputeSha256(compressed),
                expectedCompressedSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Synthetic canary compressed recovery backup failed integrity validation.");
        }
        using MemoryStream input = new(compressed, writable: false);
        using GZipStream gzip = new(input, CompressionMode.Decompress);
        using MemoryStream output = new();
        gzip.CopyTo(output);
        byte[] raw = output.ToArray();
        if (raw.LongLength != expectedRawLength
            || !string.Equals(
                ComputeSha256(raw),
                expectedRawSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Synthetic canary raw recovery backup failed integrity validation.");
        }
        return raw;
    }

    private static void RestoreFileAtomically(string projectRelativePath, byte[] bytes)
    {
        string target = Path.GetFullPath(projectRelativePath);
        string pending = target + ".canary-restore-pending";
        using (FileStream stream = new(
                   pending,
                   FileMode.Create,
                   FileAccess.Write,
                   FileShare.None))
        {
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: true);
        }
        if (!string.Equals(
            ComputeFileSha256(pending),
            ComputeSha256(bytes),
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Synthetic canary catalog restore pending file failed integrity validation.");
        }
        File.Replace(pending, target, destinationBackupFileName: null);
    }

    private static void DeleteRecoveryAuthority(
        CanaryCatalogTransactionManifest manifest)
    {
        DeleteIfPresent(manifest.itemCatalogBackupPath);
        DeleteIfPresent(manifest.itemCatalogBackupPath + ".pending");
        DeleteIfPresent(manifest.domainCatalogBackupPath);
        DeleteIfPresent(manifest.domainCatalogBackupPath + ".pending");
        string directory = Path.GetDirectoryName(manifest.itemCatalogBackupPath);
        if (!string.IsNullOrEmpty(directory)
            && Directory.Exists(directory)
            && !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }
    }

    private static void DeleteIfPresent(string path)
    {
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            File.Delete(path);
    }

    private static void WriteMarkerAtomically(
        CanaryCatalogTransactionManifest manifest)
    {
        Directory.CreateDirectory("Temp");
        string pending = MarkerPath + ".pending";
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(
            JsonUtility.ToJson(manifest, prettyPrint: true));
        using (FileStream stream = new(
                   pending,
                   FileMode.Create,
                   FileAccess.Write,
                   FileShare.None))
        {
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: true);
        }
        if (File.Exists(MarkerPath))
            File.Replace(pending, MarkerPath, destinationBackupFileName: null);
        else
            File.Move(pending, MarkerPath);
    }

    [Serializable]
    private sealed class CanaryCatalogTransactionManifest
    {
        public int schemaVersion;
        public string transactionId = string.Empty;
        public string transactionNonce = string.Empty;
        public string projectRootDigest = string.Empty;
        public string itemPath = string.Empty;
        public string recipePath = string.Empty;
        public string itemId = string.Empty;
        public string recipeId = string.Empty;
        public string itemCatalogPath = string.Empty;
        public string domainCatalogPath = string.Empty;
        public string itemCatalogSha256 = string.Empty;
        public string domainCatalogSha256 = string.Empty;
        public long itemCatalogByteLength;
        public long domainCatalogByteLength;
        public string itemCatalogBackupPath = string.Empty;
        public string domainCatalogBackupPath = string.Empty;
        public long itemCatalogBackupCompressedLength;
        public long domainCatalogBackupCompressedLength;
        public string itemCatalogBackupCompressedSha256 = string.Empty;
        public string domainCatalogBackupCompressedSha256 = string.Empty;
        public int baselineItemCount;
        public int baselineDomainCount;
        public int baselineRecipeCount;
        public string baselineItemIdDigest = string.Empty;
        public string baselineRecipeIdDigest = string.Empty;
        public string baselineItemReferenceDigest = string.Empty;
        public string baselineDomainReferenceDigest = string.Empty;
        public string itemAssetGuid = string.Empty;
        public string recipeAssetGuid = string.Empty;
        public string augmentedItemCatalogSha256 = string.Empty;
        public string augmentedDomainCatalogSha256 = string.Empty;
        public string transactionSourceSha256 = string.Empty;
        public string verifierSourceSha256 = string.Empty;
    }
}
#endif
