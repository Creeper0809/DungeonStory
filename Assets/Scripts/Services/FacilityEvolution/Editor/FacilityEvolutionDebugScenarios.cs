using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FacilityEvolutionDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Facility Evolution/Run Scenarios")]
    public static void RunAllFromMenu()
    {
        RunAll(true);
    }

    public static bool RunAll(bool log = false)
    {
        List<string> errors = new List<string>();
        RunScenario("Activation projection snapshots mutable building authority", () =>
            FacilityEvolutionActivationProjectionDebugScenarios.RunAll(), errors);
        RunScenario("Engine constructor facade stays bounded and guarded", FacilityEvolutionConstructorFacadeDebugScenarios.Verify, errors);
        RunScenario("Modular strategy evolution assets are generated and loadable", VerifyP1EvolutionAssets, errors);
        RunScenario("Room profile separates crowded and fine dining", VerifyDiningProfilesSeparateCrowdedAndFineDining, errors);
        RunScenario("Record metrics cannot override room-owned metrics", VerifyRecordMetricsCannotOverrideRoomOwnedMetrics, errors);
        RunScenario("Profile and record views reject external mutation", VerifyProfileAndRecordViewsRejectExternalMutation, errors);
        RunScenario("Identity pressure scores room lineage direction", VerifyIdentityPressureScoresRoomLineageDirection, errors);
        RunScenario("Record token consume policy preserves configured history tokens", VerifyRecordTokenConsumePolicyPreservesHistoryTokens, errors);
        RunScenario("Warehouse resources aggregate and consume atomically", VerifyWarehouseResourcesAggregateAndConsumeAtomically, errors);
        RunScenario("Mutation resolver gates suggestions by evidence", VerifyMutationResolverGatesSuggestionsByEvidence, errors);
        RunScenario("Context gates evolution candidates", VerifyContextGatesEvolutionCandidates, errors);
        RunScenario("Validation checks expose candidate condition state", VerifyValidationChecksExposeCandidateConditionState, errors);
        RunScenario("LLM narrative filters IDs without overriding rule authority", VerifyLlmProposalFiltersIdsAndOrdersCandidates, errors);
        RunScenario("Evolution overview stays rule based and does not enqueue LLM work", VerifyOverviewDoesNotRequestLlm, errors);
        RunScenario("Runtime events build evolution records", VerifyRuntimeEventsBuildEvolutionRecords, errors);
        RunScenario("Evolution replaces facility and preserves lineage records", VerifyEvolutionReplacesFacilityAndPreservesLineageRecords, errors);
        RunScenario("Failed evolution keeps original facility", VerifyFailedEvolutionKeepsOriginalFacility, errors);
        RunScenario("Failed replacement retries one pending material batch without a second debit", VerifyFailedReplacementReplaysPendingMaterialBatch, errors);
        RunScenario("Pending material V4 tamper restores atomically", VerifyPendingMaterialV4TamperRestoresAtomically, errors);
        RunScenario("Domain-applied material acknowledgement resumes without replacement replay", VerifyDomainAppliedAcknowledgementResume, errors);
        RunScenario("Relocation package Transfer restore and acknowledgement are exact", FacilityRelocationPackageOutboxFixture.Run, errors);
        RunScenario("Relocation completion rechecks production and stock-sensor authority before world mutation", FacilityRelocationCompletionFenceFixture.Run, errors);
        RunScenario(
            "Relocation preserves persistent identity and closes the retarget epoch",
            () =>
            {
                FacilityRelocationIdentityHandoffFixture.Verify();
                return true;
            },
            errors);
        RunScenario("Recalibration material Transfer restore and acknowledgement are exact", FacilityRecalibrationMaterialOutboxFixture.Run, errors);
        RunScenario("Modification material batch Transfer restore and acknowledgement are exact", FacilityModificationMaterialOutboxFixture.Run, errors);
        RunScenario("Pending material projection resumes the exact persisted result", VerifyPendingMaterialProjectionResumesExactResult, errors);
        RunScenario("Injected validator blocks candidate and evolution", VerifyInjectedValidatorBlocksCandidateAndEvolution, errors);
        RunScenario("Evolution UI renders context and executes approved candidate", VerifyEvolutionPanelRenderingAndAction, errors);

        if (errors.Count > 0)
        {
            Debug.LogError($"FacilityEvolutionDebugScenarios failed:\n{string.Join("\n", errors)}");
            return false;
        }

        if (log)
        {
            Debug.Log("FacilityEvolutionDebugScenarios passed.");
        }

        return true;
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (!scenario())
            {
                errors.Add($"- {name}");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"- {name}: {ex}");
        }
    }

    private static bool VerifyP1EvolutionAssets()
    {
        FacilityEvolutionRecipeSO[] recipes = AssetDatabase
            .FindAssets(
                "t:FacilityEvolutionRecipeSO",
                new[] { "Assets/Resources/SO/FacilityEvolution/P1" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<FacilityEvolutionRecipeSO>)
            .Where(recipe => recipe != null)
            .ToArray();
        FacilityEvolutionRecipeSO alchemy = recipes.FirstOrDefault(recipe =>
            recipe.EffectiveId == "evolve_research_desk_to_alchemy_bench");
        FacilityEvolutionRecordTokenDefinitionSO mercenaryToken =
            Resources.LoadAll<FacilityEvolutionRecordTokenDefinitionSO>("SO/FacilityEvolution")
                .FirstOrDefault((definition) =>
                    definition != null
                    && definition.EffectiveId == FacilityEvolutionTerms.MercenaryHangout);

        return recipes.Length == 6
            && recipes.All(recipe => recipe.HasValidData
                && recipe.requireUsableRoom
                && recipe.requiredUniqueFixtures.Length > 0
                && IsModularBuilding(recipe.resultBuilding)
                && recipe.fromFacilities.All(IsModularBuilding))
            && recipes.Count(recipe => recipe.EffectiveId.Contains("commercial", StringComparison.Ordinal)
                || recipe.EffectiveId.Contains("shop", StringComparison.Ordinal)) == 2
            && recipes.Count(recipe => recipe.EffectiveId.Contains("guard", StringComparison.Ordinal)
                || recipe.EffectiveId.Contains("training", StringComparison.Ordinal)) == 2
            && recipes.Count(recipe => recipe.EffectiveId.Contains("research", StringComparison.Ordinal)
                || recipe.EffectiveId.Contains("mana", StringComparison.Ordinal)) == 2
            && alchemy != null
            && alchemy.requiredRoomScores.Any(requirement => requirement.key == FacilityEvolutionTerms.Research)
            && alchemy.identityPressureWeights.Any(weight => weight.key == FacilityEvolutionTerms.Ritual)
            && alchemy.minimumIdentityScore > 0f
            && mercenaryToken != null
            && mercenaryToken.consumePolicy == FacilityEvolutionRecordTokenConsumePolicy.Preserve
            && mercenaryToken.recipeTags.Contains(FacilityEvolutionTerms.Combat)
            && LoadModularBuilding("G04_전술지도탁자").Facility.SupportsRole(FacilityRole.Security);
    }

    private static bool IsModularBuilding(BuildingSO building)
    {
        return building != null
            && AssetDatabase.GetAssetPath(building).StartsWith(
                "Assets/Resources/SO/Building/Modular/",
                StringComparison.Ordinal);
    }

    private static BuildingSO LoadModularBuilding(string assetName)
    {
        return AssetDatabase.LoadAssetAtPath<BuildingSO>(
            $"Assets/Resources/SO/Building/Modular/{assetName}.asset");
    }

    private static bool VerifyDiningProfilesSeparateCrowdedAndFineDining()
    {
        using EvolutionScenarioWorld crowded = EvolutionScenarioWorld.CreateCrowdedDining();
        using EvolutionScenarioWorld fine = EvolutionScenarioWorld.CreateFineDining();

        IFacilityEvolutionRecordProvider profileRecords =
            new FacilityEvolutionRecordComponentService(new FacilityEvolutionRecordComponentFactory());
        IRoomLayoutCache profileRooms = new RoomLayoutCache();
        RoomProfile crowdedProfile = new RoomProfileBuilder(profileRecords, profileRooms).Build(crowded.SourceFacility);
        RoomProfile fineProfile = new RoomProfileBuilder(profileRecords, profileRooms).Build(fine.SourceFacility);

        return crowdedProfile.IsUsable
            && fineProfile.IsUsable
            && crowdedProfile.GetMetric(FacilityEvolutionTerms.SeatDensity) > fineProfile.GetMetric(FacilityEvolutionTerms.SeatDensity)
            && fineProfile.GetMetric(FacilityEvolutionTerms.LuxuryPerSeat) > crowdedProfile.GetMetric(FacilityEvolutionTerms.LuxuryPerSeat)
            && fineProfile.GetMetric(FacilityEvolutionTerms.AverageSeatSpacing) > crowdedProfile.GetMetric(FacilityEvolutionTerms.AverageSeatSpacing)
            && crowdedProfile.GetIdentityPressure(FacilityEvolutionTerms.Crowd) > fineProfile.GetIdentityPressure(FacilityEvolutionTerms.Crowd)
            && fineProfile.GetIdentityPressure(FacilityEvolutionTerms.Luxury) > crowdedProfile.GetIdentityPressure(FacilityEvolutionTerms.Luxury);
    }

    private static bool VerifyRecordMetricsCannotOverrideRoomOwnedMetrics()
    {
        using EvolutionScenarioWorld fine = EvolutionScenarioWorld.CreateFineDining();
        FacilityEvolutionRecordComponent record = fine.SourceFacility.GetComponent<FacilityEvolutionRecordComponent>()
            ?? fine.SourceFacility.gameObject.AddComponent<FacilityEvolutionRecordComponent>();
        record.SetMetric(FacilityEvolutionTerms.SeatCount, 0f);
        record.SetMetric(FacilityEvolutionTerms.LuxuryPerSeat, 999f);
        record.SetMetric(FacilityEvolutionTerms.AverageSatisfaction, 87f);

        IFacilityEvolutionRecordProvider records =
            new FacilityEvolutionRecordComponentService(new FacilityEvolutionRecordComponentFactory());
        RoomProfile profile = new RoomProfileBuilder(records, new RoomLayoutCache()).Build(fine.SourceFacility);

        return profile.GetMetric(FacilityEvolutionTerms.SeatCount) > 0f
            && profile.GetMetric(FacilityEvolutionTerms.LuxuryPerSeat) < 999f
            && Mathf.Approximately(profile.GetMetric(FacilityEvolutionTerms.AverageSatisfaction), 87f);
    }

    private static bool VerifyProfileAndRecordViewsRejectExternalMutation()
    {
        RoomProfile profile = new RoomProfile(null, null);
        profile.AddMetric("metric:test", 3f);
        profile.AddTag("tag:test");
        profile.AddRecentEvent("before");

        FacilityEvolutionRecord record = new FacilityEvolutionRecord();
        record.AddMetric("metric:test", 4f);
        record.AddToken("token:test", 2);
        record.AddEvent("before");

        bool profileMetricBlocked = MutationThrows(
            () => ((IDictionary<string, float>)profile.Metrics)["metric:test"] = 99f);
        bool profileTagBlocked = MutationThrows(
            () => ((IList<string>)profile.Tags)[0] = "after");
        bool profileEventBlocked = MutationThrows(
            () => ((IList<string>)profile.RecentEvents)[0] = "after");
        bool recordMetricBlocked = MutationThrows(
            () => ((IDictionary<string, float>)record.Metrics)["metric:test"] = 99f);
        bool recordTokenBlocked = MutationThrows(
            () => ((IDictionary<string, int>)record.Tokens)["token:test"] = 99);
        bool recordEventBlocked = MutationThrows(
            () => ((IList<string>)record.RecentEvents)[0] = "after");

        return profileMetricBlocked
            && profileTagBlocked
            && profileEventBlocked
            && recordMetricBlocked
            && recordTokenBlocked
            && recordEventBlocked
            && Mathf.Approximately(profile.GetMetric("metric:test"), 3f)
            && profile.HasTag("tag:test")
            && profile.RecentEvents[0] == "before"
            && Mathf.Approximately(record.GetMetric("metric:test"), 4f)
            && record.GetToken("token:test") == 2
            && record.RecentEvents[0] == "before";
    }

    private static bool MutationThrows(Action mutation)
    {
        try
        {
            mutation();
            return false;
        }
        catch (NotSupportedException)
        {
            return true;
        }
    }

    private static bool VerifyIdentityPressureScoresRoomLineageDirection()
    {
        using EvolutionScenarioWorld crowded = EvolutionScenarioWorld.CreateCrowdedDining();
        using EvolutionScenarioWorld fine = EvolutionScenarioWorld.CreateFineDining();

        FacilityEvolutionRecipeSO crowdRecipe = CreateCrowdIdentityRecipe(crowded.SourceData, crowded.CrowdResultData);
        FacilityEvolutionRecipeSO fineRecipe = CreateFineIdentityRecipe(crowded.SourceData, crowded.FineResultData);
        StaticFacilityEvolutionRecipeProvider recipes = new StaticFacilityEvolutionRecipeProvider(crowdRecipe, fineRecipe);

        FacilityEvolutionEngine crowdedEngine = crowded.CreateEngine(recipes);
        FacilityEvolutionEngine fineEngine = fine.CreateEngine(recipes);

        IReadOnlyList<FacilityEvolutionCandidate> crowdedCandidates =
            crowdedEngine.GetCandidates(crowded.SourceFacility, includeRejected: true);
        IReadOnlyList<FacilityEvolutionCandidate> fineCandidates =
            fineEngine.GetCandidates(fine.SourceFacility, includeRejected: true);

        FacilityEvolutionCandidate crowdedCrowd = crowdedCandidates.FirstOrDefault((candidate) => candidate.Recipe == crowdRecipe);
        FacilityEvolutionCandidate crowdedFine = crowdedCandidates.FirstOrDefault((candidate) => candidate.Recipe == fineRecipe);
        FacilityEvolutionCandidate fineCrowd = fineCandidates.FirstOrDefault((candidate) => candidate.Recipe == crowdRecipe);
        FacilityEvolutionCandidate fineFine = fineCandidates.FirstOrDefault((candidate) => candidate.Recipe == fineRecipe);

        return crowdedCrowd != null
            && crowdedCrowd.Approved
            && crowdedCrowd.IdentityScore.Score >= crowdedCrowd.IdentityScore.MinimumScore
            && crowdedFine != null
            && !crowdedFine.Approved
            && fineFine != null
            && fineFine.Approved
            && fineFine.IdentityScore.Score >= fineFine.IdentityScore.MinimumScore
            && fineCrowd != null
            && !fineCrowd.Approved;
    }

    private static bool VerifyRecordTokenConsumePolicyPreservesHistoryTokens()
    {
        using EvolutionScenarioWorld world = EvolutionScenarioWorld.CreateCombatDining();
        FacilityEvolutionRecipeSO recipe = CreateCombatRecipe(world.SourceData, world.CombatResultData, consumeRecordToken: true);
        StaticFacilityEvolutionRecipeProvider recipes = new StaticFacilityEvolutionRecipeProvider(recipe);
        MemoryFacilityEvolutionResourceProvider resources = new MemoryFacilityEvolutionResourceProvider();
        resources.SetMaterial("high_grade_meat", 3);
        FacilityEvolutionRecordTokenDefinitionSO definition =
            ScriptableObject.CreateInstance<FacilityEvolutionRecordTokenDefinitionSO>();
        world.TrackObject(definition);
        definition.tokenId = FacilityEvolutionTerms.MercenaryHangout;
        definition.consumePolicy = FacilityEvolutionRecordTokenConsumePolicy.Preserve;
        DefaultFacilityEvolutionRecordTokenConsumer tokenConsumer =
            new DefaultFacilityEvolutionRecordTokenConsumer(
                new StaticRecordTokenDefinitionProvider(definition));

        FacilityEvolutionEngine engine = world.CreateEngine(
            recipes,
            resources,
            recordTokenConsumer: tokenConsumer);
        bool success = engine.TryEvolve(world.SourceFacility, recipe, out FacilityEvolutionResult result);
        FacilityEvolutionRecordComponent record = result.ResultBuilding != null
            ? result.ResultBuilding.GetComponent<FacilityEvolutionRecordComponent>()
            : null;
        FacilityEvolutionRecord copiedRecord = record != null ? record.GetRecord(result.ResultBuilding) : null;

        return success
            && copiedRecord != null
            && copiedRecord.GetToken(FacilityEvolutionTerms.MercenaryHangout) == 2;
    }

    private static bool VerifyWarehouseResourcesAggregateAndConsumeAtomically()
    {
        WarehouseInventory first = new WarehouseInventory(
            10L, StockCategory.General, restrictCategory: false);
        WarehouseInventory second = new WarehouseInventory(
            10L, StockCategory.General, restrictCategory: false);
        first.SeedPhysicalStockForTest(StockCategory.General, 2);
        second.SeedPhysicalStockForTest(StockCategory.General, 3);
        WarehouseFacilityEvolutionResourceProvider provider =
            new WarehouseFacilityEvolutionResourceProvider(
                new StaticWarehouseInventoryQuery(first, second));
        string materialId = StockCategoryPersistenceId.ToId(StockCategory.General);
        FacilityEvolutionMaterialRequirement[] firstDebit =
        {
            new FacilityEvolutionMaterialRequirement { materialId = materialId, amount = 4 }
        };
        FacilityEvolutionMaterialRequirement[] rejectedDebit =
        {
            new FacilityEvolutionMaterialRequirement { materialId = materialId, amount = 2 }
        };

        bool hadCombinedStock = provider.HasMaterial(materialId, 5);
        bool consumed = provider.TryCommitMaterialsPending(
                firstDebit,
                "facility-evolution-test:aggregate:1",
                "facility-evolution-test-material",
                out FacilityEvolutionMaterialCommitReceipt firstCommit,
                out _)
            && provider.AcknowledgeMaterialCommit(firstCommit.CommitId, out _);
        int afterConsume = first.GetStock(StockCategory.General)
            + second.GetStock(StockCategory.General);
        bool rejectedWithoutPartialWithdrawal = !provider.TryCommitMaterialsPending(
            rejectedDebit,
            "facility-evolution-test:aggregate:2",
            "facility-evolution-test-material",
            out _,
            out _);
        int afterRejectedConsume = first.GetStock(StockCategory.General)
            + second.GetStock(StockCategory.General);

        return hadCombinedStock
            && consumed
            && afterConsume == 1
            && rejectedWithoutPartialWithdrawal
            && afterRejectedConsume == 1
            && !provider.HasMaterial("unknown-material", 1);
    }

    private static bool VerifyMutationResolverGatesSuggestionsByEvidence()
    {
        using EvolutionScenarioWorld world = EvolutionScenarioWorld.CreateCombatDining();
        FacilityEvolutionRecipeSO recipe = CreateCombatRecipe(world.SourceData, world.CombatResultData, consumeRecordToken: false);
        recipe.allowedMutationTags = new[] { FacilityEvolutionTerms.Brutal, FacilityEvolutionTerms.Combat };
        StaticFacilityEvolutionRecipeProvider recipes = new StaticFacilityEvolutionRecipeProvider(recipe);
        FacilityEvolutionEngine engine = world.CreateEngine(recipes);
        FacilityEvolutionContext context = engine.BuildContext(world.SourceFacility);
        FacilityEvolutionProposal proposal = new FacilityEvolutionProposal(
            "거친 전투 식당",
            new[] { recipe.EffectiveId },
            null,
            new[] { FacilityEvolutionTerms.Brutal, FacilityEvolutionTerms.Combat, "UnknownMutation" },
            "용병들이 모여드는 식당",
            0.9f);

        FacilityEvolutionMutationResult result =
            new DefaultFacilityEvolutionMutationResolver().Resolve(context, recipe, proposal);

        return result.Tags.Contains(FacilityEvolutionTerms.Combat)
            && !result.Tags.Contains(FacilityEvolutionTerms.Brutal)
            && !result.Tags.Contains("UnknownMutation");
    }

    private static bool VerifyContextGatesEvolutionCandidates()
    {
        using EvolutionScenarioWorld crowded = EvolutionScenarioWorld.CreateCrowdedDining();
        using EvolutionScenarioWorld fine = EvolutionScenarioWorld.CreateFineDining();

        FacilityEvolutionRecipeSO crowdRecipe = CreateCrowdRecipe(crowded.SourceData, crowded.CrowdResultData);
        FacilityEvolutionRecipeSO fineRecipe = CreateFineRecipe(crowded.SourceData, crowded.FineResultData);
        StaticFacilityEvolutionRecipeProvider recipes = new StaticFacilityEvolutionRecipeProvider(crowdRecipe, fineRecipe);

        FacilityEvolutionEngine crowdedEngine = crowded.CreateEngine(recipes);
        FacilityEvolutionEngine fineEngine = fine.CreateEngine(recipes);

        IReadOnlyList<FacilityEvolutionCandidate> crowdedCandidates =
            crowdedEngine.GetCandidates(crowded.SourceFacility, includeRejected: true);
        IReadOnlyList<FacilityEvolutionCandidate> fineCandidates =
            fineEngine.GetCandidates(fine.SourceFacility, includeRejected: true);

        FacilityEvolutionCandidate crowdedCrowd = crowdedCandidates.FirstOrDefault((candidate) => candidate.Recipe == crowdRecipe);
        FacilityEvolutionCandidate crowdedFine = crowdedCandidates.FirstOrDefault((candidate) => candidate.Recipe == fineRecipe);
        FacilityEvolutionCandidate fineCrowd = fineCandidates.FirstOrDefault((candidate) => candidate.Recipe == crowdRecipe);
        FacilityEvolutionCandidate fineFine = fineCandidates.FirstOrDefault((candidate) => candidate.Recipe == fineRecipe);

        return crowdedCrowd != null
            && crowdedCrowd.Approved
            && crowdedFine != null
            && !crowdedFine.Approved
            && !string.IsNullOrWhiteSpace(crowdedFine.RejectedHintText)
            && fineFine != null
            && fineFine.Approved
            && fineCrowd != null
            && !fineCrowd.Approved
            && !string.IsNullOrWhiteSpace(fineCrowd.RejectedHintText);
    }

    private static bool VerifyValidationChecksExposeCandidateConditionState()
    {
        using EvolutionScenarioWorld crowded = EvolutionScenarioWorld.CreateCrowdedDining();
        FacilityEvolutionRecipeSO crowdRecipe = CreateCrowdRecipe(crowded.SourceData, crowded.CrowdResultData);
        FacilityEvolutionRecipeSO fineRecipe = CreateFineRecipe(crowded.SourceData, crowded.FineResultData);
        StaticFacilityEvolutionRecipeProvider recipes = new StaticFacilityEvolutionRecipeProvider(crowdRecipe, fineRecipe);
        FacilityEvolutionEngine engine = crowded.CreateEngine(recipes);

        IReadOnlyList<FacilityEvolutionCandidate> candidates =
            engine.GetCandidates(crowded.SourceFacility, includeRejected: true);
        FacilityEvolutionCandidate approved = candidates.FirstOrDefault((candidate) => candidate.Recipe == crowdRecipe);
        FacilityEvolutionCandidate rejected = candidates.FirstOrDefault((candidate) => candidate.Recipe == fineRecipe);

        return approved != null
            && approved.Validation.Checks.Any((check) => check.Passed && check.Category == "하드 조건")
            && approved.Validation.Checks.Any((check) => check.Passed && check.Category == "기록")
            && rejected != null
            && rejected.Validation.Checks.Any((check) => !check.Passed && check.Category == "방 지표")
            && rejected.Validation.Checks.Any((check) => !check.Passed && check.Category == "기록");
    }

    private static bool VerifyLlmProposalFiltersIdsAndOrdersCandidates()
    {
        using EvolutionScenarioWorld world = EvolutionScenarioWorld.CreateCombatDining();
        FacilityEvolutionRecipeSO primary = CreateCombatRecipe(
            world.SourceData,
            world.CombatResultData,
            consumeRecordToken: false);
        FacilityEvolutionRecipeSO secondary = CreateCombatRecipe(
            world.SourceData,
            world.FineResultData,
            consumeRecordToken: false);
        secondary.evolutionId = "evolve_test_llm_preferred_combat";
        secondary.displayName = "LLM 선호 전투 계보";

        StaticFacilityEvolutionRecipeProvider recipes = new StaticFacilityEvolutionRecipeProvider(primary, secondary);
        MemoryFacilityEvolutionResourceProvider resources = new MemoryFacilityEvolutionResourceProvider();
        resources.SetMaterial("high_grade_meat", 6);
        FakeLlmRuntime fakeLlm = new FakeLlmRuntime(
            "{\"facilityIdentitySummary\":\"용병들이 자주 찾는 거친 식당\","
            + "\"proposalIds\":[\"evolve_test_llm_preferred_combat\",\"unknown_candidate\",\"evolve_test_combat_dining\"],"
            + "\"reasons\":[{\"id\":\"evolve_test_llm_preferred_combat\",\"reason\":\"용병 기록과 전투 분위기가 이 계보와 가장 강하게 맞습니다.\"}],"
            + "\"rejectedHints\":[{\"id\":\"evolve_test_combat_dining\",\"reason\":\"다른 전투 계보도 가능하지만 결정적인 사건 기록이 조금 부족합니다.\"},{\"id\":\"unknown_candidate\",\"reason\":\"무시되어야 합니다.\"}],"
            + "\"mutationTagSuggestions\":[\"Combat\",\"UnknownMutation\"],"
            + "\"flavorText\":\"식탁 주변의 무용담이 다음 계보를 부르고 있습니다.\","
            + "\"usedMotifIds\":[],\"usedCharacterFactIds\":[],"
            + "\"confidence\":0.82}");
        CachedLocalLlmFacilityEvolutionProposalProvider proposalProvider =
            new CachedLocalLlmFacilityEvolutionProposalProvider(
                new RuleBasedFacilityEvolutionProposalProvider(),
                () => fakeLlm,
                allowRequestsOutsidePlayMode: true);

        FacilityEvolutionEngine engine = world.CreateEngine(recipes, resources, proposalProvider);
        engine.GetCandidates(world.SourceFacility, includeRejected: true);
        IReadOnlyList<FacilityEvolutionCandidate> candidates =
            engine.GetCandidates(world.SourceFacility, includeRejected: false);

        FacilityEvolutionCandidate first = candidates.FirstOrDefault();
        bool valid = fakeLlm.FacilityEvolutionRequestCount == 1
            && !string.IsNullOrWhiteSpace(fakeLlm.LastPrompt)
            && fakeLlm.LastPrompt.Contains("rejectedHints")
            && first != null
            && first.Recipe == primary
            && first.ProposalSource == FacilityEvolutionProposalSources.RuleBased
            && first.ProposalStatusMessage.Contains("Rule-authoritative")
            && first.ProposalStatusMessage.Contains("local narrative")
            && first.Reason.Contains("용병")
            && first.FlavorText.Contains("무용담");
        if (!valid)
        {
            Debug.LogError(
                $"Facility evolution LLM diagnostic: requests={fakeLlm.FacilityEvolutionRequestCount}, "
                + $"prompt={(!string.IsNullOrWhiteSpace(fakeLlm.LastPrompt))}, "
                + $"promptHints={fakeLlm.LastPrompt?.Contains("rejectedHints") == true}, "
                + $"candidateCount={candidates.Count}, "
                + $"first={first?.Recipe?.evolutionId ?? "null"}, "
                + $"source={first?.ProposalSource ?? "null"}, "
                + $"status={first?.ProposalStatusMessage ?? "null"}, "
                + $"reason={first?.Reason ?? "null"}, flavor={first?.FlavorText ?? "null"}");
        }
        return valid;
    }

    private static bool VerifyOverviewDoesNotRequestLlm()
    {
        using EvolutionScenarioWorld world = EvolutionScenarioWorld.CreateCombatDining();
        FacilityEvolutionRecipeSO recipe = CreateCombatRecipe(
            world.SourceData,
            world.CombatResultData,
            consumeRecordToken: false);
        FakeLlmRuntime fakeLlm = new FakeLlmRuntime("{}");
        CachedLocalLlmFacilityEvolutionProposalProvider proposalProvider =
            new CachedLocalLlmFacilityEvolutionProposalProvider(
                new RuleBasedFacilityEvolutionProposalProvider(),
                () => fakeLlm,
                allowRequestsOutsidePlayMode: true);
        FacilityEvolutionEngine engine = world.CreateEngine(
            new StaticFacilityEvolutionRecipeProvider(recipe),
            new MemoryFacilityEvolutionResourceProvider(),
            proposalProvider);

        IReadOnlyList<FacilityEvolutionCandidate> candidates = engine.GetCandidates(
            world.SourceFacility,
            includeRejected: true,
            requestLlmProposal: false);

        return fakeLlm.FacilityEvolutionRequestCount == 0
            && candidates.Count == 1
            && candidates[0].ProposalSource == FacilityEvolutionProposalSources.RuleBased;
    }

    private static bool VerifyRuntimeEventsBuildEvolutionRecords()
    {
        using EvolutionScenarioWorld world = EvolutionScenarioWorld.CreateCombatDining();
        GameObject runtimeObject = new GameObject("FacilityEvolutionRecordRuntimeScenario");
        world.TrackObject(runtimeObject);
        FacilityEvolutionRecordRuntime runtime = runtimeObject.AddComponent<FacilityEvolutionRecordRuntime>();
        FacilityCandidateCacheStore candidateCache = new FacilityCandidateCacheStore(CharacterAiEditorTestDependencies.WorldRegistry, frameWorkBudget: null);
        FacilityEvolutionRecordComponentService records =
            new FacilityEvolutionRecordComponentService(new FacilityEvolutionRecordComponentFactory());
        runtime.Construct(
            new FacilityEvolutionRecordEventRecorder(candidateCache, records, instanceEvolutionRuntime: null),
            CharacterAiEditorTestDependencies.GameEvents);
        CharacterActor mercenary = CreateDebugActor(
            world,
            "Debug Orc Mercenary",
            "Orc",
            CharacterType.Customer,
            attack: 12,
            sales: 5,
            mood: 76f);
        CharacterActor noble = CreateDebugActor(
            world,
            "Debug Vampire Patron",
            "Vampire",
            CharacterType.Customer,
            attack: 5,
            sales: 12,
            mood: 88f);

        for (int i = 0; i < 3; i++)
        {
            runtime.OnTriggerEvent(new FacilityVisitEvent(mercenary, world.SourceFacility));
        }

        runtime.OnTriggerEvent(new OperatingDayEndedEvent(1));

        for (int i = 0; i < 2; i++)
        {
            runtime.OnTriggerEvent(new FacilityVisitEvent(mercenary, world.SourceFacility));
        }

        runtime.OnTriggerEvent(new FacilityVisitEvent(noble, world.SourceFacility));
        runtime.OnTriggerEvent(new FacilityRevenueEvent(noble, world.SourceFacility, 45));
        runtime.OnTriggerEvent(new FacilityStockConsumedEvent(mercenary, world.SourceFacility, StockCategory.Food, 2));
        runtime.OnTriggerEvent(new FacilityRestockEvent(world.SourceFacility, 2, 0, "재고 보급 실패"));
        runtime.OnTriggerEvent(new FacilityCrimeEvent(
            mercenary,
            world.SourceFacility,
            FacilityCrimeKind.Shoplifting,
            "테스트 절도 발생",
            20));
        runtime.OnTriggerEvent(new InvasionFacilityDamagedEvent(mercenary, world.SourceFacility));

        FacilityEvolutionRecordComponent component =
            world.SourceFacility.GetComponent<FacilityEvolutionRecordComponent>();
        FacilityEvolutionRecord record = component != null ? component.GetRecord(world.SourceFacility) : null;

        return record != null
            && Mathf.Approximately(record.GetMetric(FacilityEvolutionTerms.VisitCount), 6f)
            && Mathf.Approximately(record.GetMetric(FacilityEvolutionTerms.UniqueVisitorCount), 2f)
            && record.GetMetric(FacilityEvolutionTerms.RepeatVisitorRatio) > 0.6f
            && record.GetMetric(FacilityEvolutionTerms.AverageSatisfaction) > 75f
            && record.GetMetric(FacilityEvolutionTerms.CombatVisitorRatio) > 0.8f
            && record.GetMetric(FacilityEvolutionTerms.NobleVisitorRatio) > 0.15f
            && Mathf.Approximately(record.GetMetric(FacilityEvolutionTerms.TotalRevenue), 45f)
            && Mathf.Approximately(record.GetMetric(FacilityEvolutionTerms.HighValueTransactionCount), 1f)
            && record.GetMetric(FacilityEvolutionTerms.StockCostPerVisit) > 0.3f
            && Mathf.Approximately(record.GetMetric(FacilityEvolutionTerms.StockoutCount), 1f)
            && Mathf.Approximately(record.GetMetric(FacilityEvolutionTerms.CrimeCount), 1f)
            && Mathf.Approximately(record.GetMetric(FacilityEvolutionTerms.TheftCount), 1f)
            && Mathf.Approximately(record.GetMetric(FacilityEvolutionTerms.FacilityDamageTaken), 1f)
            && record.GetToken(FacilityEvolutionTerms.CleanServiceStreak) >= 1
            && record.GetToken(FacilityEvolutionTerms.HighTurnoverService) >= 1
            && record.GetToken(FacilityEvolutionTerms.MercenaryHangout) >= 7
            && record.GetToken(FacilityEvolutionTerms.HighMeatConsumption) >= 3
            && record.GetToken(FacilityEvolutionTerms.NoblePatronage) >= 2
            && record.GetToken(FacilityEvolutionTerms.OutlawRumor) >= 1
            && record.RecentEvents.Any((entry) => entry.Contains("combat-oriented"))
            && record.RecentEvents.Any((entry) => entry.Contains("재고 보급 실패"))
            && record.RecentEvents.Any((entry) => entry.Contains("테스트 절도"));
    }

    private static bool VerifyEvolutionReplacesFacilityAndPreservesLineageRecords()
    {
        using EvolutionScenarioWorld world = EvolutionScenarioWorld.CreateCombatDining();
        FacilityEvolutionRecipeSO recipe = CreateCombatRecipe(world.SourceData, world.CombatResultData, consumeRecordToken: true);
        StaticFacilityEvolutionRecipeProvider recipes = new StaticFacilityEvolutionRecipeProvider(recipe);
        MemoryFacilityEvolutionResourceProvider resources = new MemoryFacilityEvolutionResourceProvider();
        resources.SetMaterial("high_grade_meat", 3);
        DefaultFacilityEvolutionRecordTokenConsumer tokenConsumer =
            new DefaultFacilityEvolutionRecordTokenConsumer(
                new EmptyFacilityEvolutionRecordTokenDefinitionProvider());

        FacilityEvolutionEngine engine = world.CreateEngine(
            recipes,
            resources,
            recordTokenConsumer: tokenConsumer);
        DungeonStory.Foundation.IGameEventBus gameEvents =
            new DungeonStory.Foundation.GameEventBus();
        CountingEvolutionCompletedListener listener =
            new CountingEvolutionCompletedListener(gameEvents);
        try
        {
            BuildingInstanceId survivorId =
                world.SourceFacility.RequirePersistentInstanceId();
            bool success = engine.TryEvolve(world.SourceFacility, recipe, out FacilityEvolutionResult result);
            gameEvents.Publish(new FacilityEvolutionCompletedEvent(result));

            BuildableObject occupant = world.Grid
                .GetGridCell(world.SourcePosition)
                .GetOccupant(GridLayer.Building) as BuildableObject;
            FacilityEvolutionStateComponent state = result.ResultBuilding != null
                ? result.ResultBuilding.GetComponent<FacilityEvolutionStateComponent>()
                : null;
            FacilityEvolutionRecordComponent record = result.ResultBuilding != null
                ? result.ResultBuilding.GetComponent<FacilityEvolutionRecordComponent>()
                : null;
            FacilityEvolutionRecord copiedRecord = record != null ? record.GetRecord(result.ResultBuilding) : null;

            return success
                && result.Success
                && result.ResultBuilding != null
                && result.ResultBuilding.id == world.CombatResultData.id
                && result.ResultBuilding.PersistentInstanceId.Equals(survivorId)
                && occupant == result.ResultBuilding
                && state != null
                && state.BaseFacilityId == world.SourceData.id.ToString()
                && state.CurrentFacilityId == world.CombatResultData.id.ToString()
                && state.StarGrade == 2
                && state.EvolutionHistory.Count == 1
                && state.LastIdentityPressures.Count > 0
                && state.LastIdentityPressures.Any((entry) => entry.key == FacilityEvolutionTerms.Combat && entry.value > 0f)
                && state.MutationTags.Contains(FacilityEvolutionTerms.Combat)
                && copiedRecord != null
                && copiedRecord.GetToken(FacilityEvolutionTerms.MercenaryHangout) == 1
                && copiedRecord.RecentEvents.Any((entry) => entry.Contains("용병"))
                && listener.Count == 1;
        }
        finally
        {
            listener.Dispose();
        }
    }

    private static bool VerifyFailedEvolutionKeepsOriginalFacility()
    {
        using EvolutionScenarioWorld world = EvolutionScenarioWorld.CreateCombatDining();
        FacilityEvolutionRecipeSO recipe = CreateCombatRecipe(world.SourceData, world.CombatResultData, consumeRecordToken: false);
        StaticFacilityEvolutionRecipeProvider recipes = new StaticFacilityEvolutionRecipeProvider(recipe);
        MemoryFacilityEvolutionResourceProvider resources = new MemoryFacilityEvolutionResourceProvider();
        resources.SetMaterial("high_grade_meat", 0);

        FacilityEvolutionEngine engine = world.CreateEngine(recipes, resources);
        bool success = engine.TryEvolve(world.SourceFacility, recipe, out FacilityEvolutionResult result);
        BuildableObject occupant = world.Grid
            .GetGridCell(world.SourcePosition)
            .GetOccupant(GridLayer.Building) as BuildableObject;

        return !success
            && !result.Success
            && !world.SourceFacility.isDestroy
            && occupant == world.SourceFacility
            && occupant.id == world.SourceData.id;
    }

    private static bool VerifyFailedReplacementReplaysPendingMaterialBatch()
    {
        using EvolutionScenarioWorld world = EvolutionScenarioWorld.CreateCombatDining();
        FacilityEvolutionRecipeSO recipe = CreateCombatRecipe(
            world.SourceData,
            world.CombatResultData,
            consumeRecordToken: false);
        StaticFacilityEvolutionRecipeProvider recipes =
            new StaticFacilityEvolutionRecipeProvider(recipe);
        MemoryFacilityEvolutionResourceProvider resources =
            new MemoryFacilityEvolutionResourceProvider();
        resources.SetMaterial("high_grade_meat", 3);
        FailOnceBuildingReplacer replacer = new FailOnceBuildingReplacer(
            world.CreateReplacer());
        FacilityEvolutionEngine engine = world.CreateEngine(
            recipes,
            resources,
            buildingReplacer: replacer);

        bool first = engine.TryEvolve(
            world.SourceFacility,
            recipe,
            out FacilityEvolutionResult firstResult);
        BuildableObject firstOccupant = world.Grid
            .GetGridCell(world.SourcePosition)
            .GetOccupant(GridLayer.Building) as BuildableObject;
        bool materialWasDebited = resources.HasMaterial("high_grade_meat", 1)
            && !resources.HasMaterial("high_grade_meat", 2);
        FacilityEvolutionStateComponent sourceState =
            world.SourceFacility.GetComponent<FacilityEvolutionStateComponent>();
        bool sourceAliveAfterFirst = !world.SourceFacility.isDestroy
            && ReferenceEquals(firstOccupant, world.SourceFacility);
        bool pendingAfterFirst = sourceState != null
            && sourceState.HasPendingMaterialCommit;
        string pendingPayload = sourceState?.CaptureState();
        bool currentFormatRoundTrip = sourceState != null
            && sourceState.TryRestoreState(
                sourceState.CurrentVersion,
                pendingPayload,
                out _)
            && sourceState.HasPendingMaterialCommit;

        bool second = engine.TryEvolve(
            world.SourceFacility,
            recipe,
            out FacilityEvolutionResult secondResult);
        BuildableObject secondOccupant = world.Grid
            .GetGridCell(world.SourcePosition)
            .GetOccupant(GridLayer.Building) as BuildableObject;

        bool passed = !first
            && !firstResult.Success
            && sourceAliveAfterFirst
            && materialWasDebited
            && pendingAfterFirst
            && currentFormatRoundTrip
            && second
            && secondResult.Success
            && secondResult.ResultBuilding != null
            && secondOccupant == secondResult.ResultBuilding
            && resources.HasMaterial("high_grade_meat", 1)
            && !resources.HasMaterial("high_grade_meat", 2)
            && !secondResult.ResultBuilding
                .GetComponent<FacilityEvolutionStateComponent>()
                .HasPendingMaterialCommit
            && replacer.TryReplaceCalls == 2;
        if (!passed)
        {
            Debug.LogError(
                $"Pending material retry detail: first={first}/{firstResult.Message}, "
                + $"second={second}/{secondResult.Message}, "
                + $"pendingAfterFirst={pendingAfterFirst}, roundTrip={currentFormatRoundTrip}, "
                + $"sourceAliveAfterFirst={sourceAliveAfterFirst}, "
                + $"debited={materialWasDebited}, replaceCalls={replacer.TryReplaceCalls}, "
                + $"secondOccupant={secondOccupant}.");
        }
        return passed;
    }

    private static bool VerifyPendingMaterialV4TamperRestoresAtomically()
    {
        using EvolutionScenarioWorld world = EvolutionScenarioWorld.CreateCombatDining();
        FacilityEvolutionRecipeSO recipe = CreateCombatRecipe(
            world.SourceData,
            world.CombatResultData,
            consumeRecordToken: false);
        MemoryFacilityEvolutionResourceProvider resources =
            new MemoryFacilityEvolutionResourceProvider();
        resources.SetMaterial("high_grade_meat", 3);
        FacilityEvolutionEngine engine = world.CreateEngine(
            new StaticFacilityEvolutionRecipeProvider(recipe),
            resources,
            buildingReplacer: new FailOnceBuildingReplacer(world.CreateReplacer()));
        if (engine.TryEvolve(world.SourceFacility, recipe, out _))
        {
            return false;
        }

        FacilityEvolutionStateComponent state =
            world.SourceFacility.GetComponent<FacilityEvolutionStateComponent>();
        string canonical = state.CaptureState();
        if (string.IsNullOrWhiteSpace(canonical)
            || !state.HasPendingMaterialCommit)
        {
            return false;
        }

        void ResetCanonical()
        {
            state.ApplySnapshot(
                JsonUtility.FromJson<FacilityEvolutionStateSnapshot>(canonical));
        }

        bool RejectStructuralTamper(Action<FacilityEvolutionStateSnapshot> mutate)
        {
            ResetCanonical();
            FacilityEvolutionStateSnapshot tampered =
                JsonUtility.FromJson<FacilityEvolutionStateSnapshot>(canonical);
            mutate(tampered);
            bool rejected = !state.TryRestoreState(
                state.CurrentVersion,
                JsonUtility.ToJson(tampered),
                out _);
            return rejected
                && string.Equals(state.CaptureState(), canonical, StringComparison.Ordinal);
        }

        bool RejectPhysicalJoinTamper(Action<FacilityEvolutionStateSnapshot> mutate)
        {
            ResetCanonical();
            FacilityEvolutionStateSnapshot tampered =
                JsonUtility.FromJson<FacilityEvolutionStateSnapshot>(canonical);
            mutate(tampered);
            state.ApplySnapshot(tampered);
            FacilityEvolutionPendingMaterialRestoreGuard guard =
                new FacilityEvolutionPendingMaterialRestoreGuard(
                    new GridOccupantBuildingQuery(
                        world.Grid,
                        world.SourcePosition),
                    resources,
                    new EvolutionScenarioWorld.EditorFacilityEvolutionRecipeQuery(
                        new StaticFacilityEvolutionRecipeProvider(recipe),
                        new FacilityEvolutionStateComponentFactory()));
            guard.BeginRestoreCandidate();
            bool rejected = false;
            try
            {
                guard.PublishRestoreCandidate();
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
            finally
            {
                guard.DiscardRestoreCandidate();
            }

            FacilityEvolutionPendingMaterialCommitSnapshot canonicalPending =
                JsonUtility.FromJson<FacilityEvolutionStateSnapshot>(canonical)
                    .pendingMaterialCommit;
            bool physicalReceiptPreserved = resources.TryGetPendingMaterialCommit(
                canonicalPending.operationId,
                canonicalPending.reasonCode,
                out FacilityEvolutionMaterialCommitReceipt receipt,
                out _)
                && FacilityEvolutionMaterialCommitAuthority.Matches(
                    canonicalPending,
                    receipt);
            ResetCanonical();
            return rejected
                && physicalReceiptPreserved
                && string.Equals(state.CaptureState(), canonical, StringComparison.Ordinal);
        }

        bool commitRejected = RejectPhysicalJoinTamper(snapshot =>
            snapshot.pendingMaterialCommit.commitId += ":tampered");
        bool quantityRejected = RejectPhysicalJoinTamper(snapshot =>
            snapshot.pendingMaterialCommit.quantity++);
        bool gramsRejected = RejectPhysicalJoinTamper(snapshot =>
            snapshot.pendingMaterialCommit.inputMassGrams++);
        bool recipeRejected = RejectStructuralTamper(snapshot =>
            snapshot.pendingMaterialCommit.recipeId += ":tampered");
        bool sourceRejected = RejectStructuralTamper(snapshot =>
            snapshot.pendingMaterialCommit.sourceStackIds = new[]
            {
                snapshot.pendingMaterialCommit.sourceStackIds[0],
                snapshot.pendingMaterialCommit.sourceStackIds[0]
            });
        bool resultRejected = RejectStructuralTamper(snapshot =>
        {
            FacilityEvolutionStateSnapshot resolved =
                snapshot.pendingMaterialCommit.ReadResolvedResultState();
            resolved.currentFacilityId += ":tampered";
            snapshot.pendingMaterialCommit.resolvedResultPayload =
                JsonUtility.ToJson(resolved);
        });

        bool passed = commitRejected
            && quantityRejected
            && gramsRejected
            && recipeRejected
            && sourceRejected
            && resultRejected;
        if (!passed)
        {
            Debug.LogError(
                $"Facility V4 tamper detail: commit={commitRejected}, "
                + $"quantity={quantityRejected}, grams={gramsRejected}, "
                + $"recipe={recipeRejected}, source={sourceRejected}, "
                + $"result={resultRejected}.");
        }
        return passed;
    }

    private static bool VerifyDomainAppliedAcknowledgementResume()
    {
        using EvolutionScenarioWorld world = EvolutionScenarioWorld.CreateCombatDining();
        FacilityEvolutionRecipeSO recipe = CreateCombatRecipe(
            world.SourceData,
            world.CombatResultData,
            consumeRecordToken: false);
        MemoryFacilityEvolutionResourceProvider inner =
            new MemoryFacilityEvolutionResourceProvider();
        inner.SetMaterial("high_grade_meat", 3);
        FailOnceAcknowledgementResourceProvider resources =
            new FailOnceAcknowledgementResourceProvider(inner);
        RecordingBuildingReplacer replacer = new RecordingBuildingReplacer(
            world.CreateReplacer());
        FacilityEvolutionEngine engine = world.CreateEngine(
            new StaticFacilityEvolutionRecipeProvider(recipe),
            resources,
            buildingReplacer: replacer);

        bool first = engine.TryEvolve(world.SourceFacility, recipe, out _);
        BuildableObject published = world.Grid
            .GetGridCell(world.SourcePosition)
            .GetOccupant(GridLayer.Building) as BuildableObject;
        FacilityEvolutionStateComponent publishedState =
            published?.GetComponent<FacilityEvolutionStateComponent>();
        bool domainApplied = !first
            && published != null
            && publishedState != null
            && publishedState.PendingMaterialCommit?.phase
                == FacilityEvolutionMaterialCommitPhase.DomainApplied;

        bool second = engine.TryResumePending(
            published,
            out FacilityEvolutionResult resumed,
            out _);
        BuildableObject afterResume = world.Grid
            .GetGridCell(world.SourcePosition)
            .GetOccupant(GridLayer.Building) as BuildableObject;

        return domainApplied
            && second
            && resumed.Success
            && ReferenceEquals(afterResume, published)
            && replacer.TryReplaceCalls == 1
            && resources.AcknowledgeCalls == 2
            && !publishedState.HasPendingMaterialCommit
            && inner.HasMaterial("high_grade_meat", 1)
            && !inner.HasMaterial("high_grade_meat", 2);
    }

    private static bool VerifyPendingMaterialProjectionResumesExactResult()
    {
        using EvolutionScenarioWorld world = EvolutionScenarioWorld.CreateCombatDining();
        FacilityEvolutionRecipeSO recipe = CreateCombatRecipe(
            world.SourceData,
            world.CombatResultData,
            consumeRecordToken: false);
        StaticFacilityEvolutionRecipeProvider recipes =
            new StaticFacilityEvolutionRecipeProvider(recipe);
        MemoryFacilityEvolutionResourceProvider resources =
            new MemoryFacilityEvolutionResourceProvider();
        resources.SetMaterial("high_grade_meat", 3);
        FailOnceBuildingReplacer replacer = new FailOnceBuildingReplacer(
            world.CreateReplacer());
        FacilityEvolutionRuntime runtime = world.CreateRuntime(
            recipes,
            resources,
            replacer);

        bool first = runtime.TryEvolve(world.SourceFacility, recipe, out _);
        FacilityEvolutionStateComponent sourceState =
            world.SourceFacility.GetComponent<FacilityEvolutionStateComponent>();
        string pendingPayload = sourceState.CaptureState();
        bool restored = !first
            && sourceState.TryRestoreState(
                sourceState.CurrentVersion,
                pendingPayload,
                out _)
            && sourceState.HasPendingMaterialCommit;

        FacilityEvolutionPendingMaterialProjection projection =
            new FacilityEvolutionPendingMaterialProjection(
                new GridOccupantBuildingQuery(world.Grid, world.SourcePosition),
                new FacilityCandidateCacheStore(
                    CharacterAiEditorTestDependencies.WorldRegistry,
                    frameWorkBudget: null),
                new FacilityFeatureSceneRuntimeReferences(runtime, null, null));
        projection.Initialize();

        BuildableObject resultBuilding = world.Grid
            .GetGridCell(world.SourcePosition)
            .GetOccupant(GridLayer.Building) as BuildableObject;
        FacilityEvolutionStateComponent resultState =
            resultBuilding?.GetComponent<FacilityEvolutionStateComponent>();
        return restored
            && resultBuilding != null
            && resultBuilding.BuildingData == world.CombatResultData
            && resultState != null
            && !resultState.HasPendingMaterialCommit
            && resultState.EvolutionHistory.Count == 1
            && string.Equals(
                resultState.EvolutionHistory[0].evolutionId,
                recipe.EffectiveId,
                StringComparison.Ordinal)
            && replacer.TryReplaceCalls == 2
            && resources.HasMaterial("high_grade_meat", 1)
            && !resources.HasMaterial("high_grade_meat", 2);
    }

    private static bool VerifyInjectedValidatorBlocksCandidateAndEvolution()
    {
        using EvolutionScenarioWorld world = EvolutionScenarioWorld.CreateCombatDining();
        FacilityEvolutionRecipeSO recipe = CreateCombatRecipe(world.SourceData, world.CombatResultData, consumeRecordToken: false);
        StaticFacilityEvolutionRecipeProvider recipes = new StaticFacilityEvolutionRecipeProvider(recipe);
        MemoryFacilityEvolutionResourceProvider resources = new MemoryFacilityEvolutionResourceProvider();
        resources.SetMaterial("high_grade_meat", 10);
        RejectingEvolutionValidator validator = new RejectingEvolutionValidator("Injected validator blocked");

        FacilityEvolutionEngine engine = world.CreateEngine(recipes, resources, validator: validator);
        IReadOnlyList<FacilityEvolutionCandidate> candidates =
            engine.GetCandidates(world.SourceFacility, includeRejected: true);
        FacilityEvolutionCandidate candidate = candidates.FirstOrDefault((entry) => entry.Recipe == recipe);
        bool success = engine.TryEvolve(world.SourceFacility, recipe, out FacilityEvolutionResult result);
        BuildableObject occupant = world.Grid
            .GetGridCell(world.SourcePosition)
            .GetOccupant(GridLayer.Building) as BuildableObject;

        return candidate != null
            && !candidate.Approved
            && candidate.Validation.RejectionReasons.Contains("Injected validator blocked")
            && !success
            && !result.Success
            && !world.SourceFacility.isDestroy
            && occupant == world.SourceFacility
            && validator.Calls >= 2;
    }

    private static bool VerifyEvolutionPanelRenderingAndAction()
    {
        using EvolutionScenarioWorld world = EvolutionScenarioWorld.CreateCombatDining();
        FacilityEvolutionRecipeSO recipe = CreateCombatRecipe(world.SourceData, world.CombatResultData, consumeRecordToken: true);
        StaticFacilityEvolutionRecipeProvider recipes = new StaticFacilityEvolutionRecipeProvider(recipe);
        MemoryFacilityEvolutionResourceProvider resources = new MemoryFacilityEvolutionResourceProvider();
        resources.SetMaterial("high_grade_meat", 4);
        FacilityEvolutionRuntime runtime = world.CreateRuntime(recipes, resources);

        FacilityEvolutionPanel panel = new FacilityEvolutionPanelFactory(TMPKoreanFontEditorResolver.CreateService())
            .CreateDefaultPanel(runtime);
        world.TrackObject(panel.transform.root.gameObject);
        panel.SelectFacility(world.SourceFacility);

        bool renderedContext = panel.LastRenderedText.Contains("시설 진화")
            && panel.LastRenderedText.Contains("일반 식당")
            && panel.LastRenderedText.Contains("전투 식당")
            && panel.LastRenderedText.Contains("[가능]")
            && panel.LastRenderedText.Contains("[충족]")
            && panel.LastRenderedText.Contains("용병");

        bool evolved = panel.TryEvolveFirstApproved(out FacilityEvolutionResult result);
        FacilityEvolutionStateComponent state = result.ResultBuilding != null
            ? result.ResultBuilding.GetComponent<FacilityEvolutionStateComponent>()
            : null;

        return renderedContext
            && evolved
            && result.Success
            && result.ResultBuilding != null
            && panel.SelectedFacility == result.ResultBuilding
            && panel.LastRenderedText.Contains("전투 식당")
            && state != null
            && state.StarGrade == 2
            && state.EvolutionHistory.Count == 1;
    }

    private static FacilityEvolutionRecipeSO CreateCrowdRecipe(BuildingSO source, BuildingSO result)
    {
        FacilityEvolutionRecipeSO recipe = CreateRecipe("evolve_test_crowd_dining", "대중 식당 진화", source, result);
        recipe.requiredRoomScores = new[] { Min(FacilityEvolutionTerms.Dining, 25f) };
        recipe.requiredRoomMetrics = new[]
        {
            Min(FacilityEvolutionTerms.SeatDensity, 0.9f),
            Min(FacilityEvolutionTerms.TurnoverRate, 0.65f)
        };
        recipe.requiredRecordTokens = new[] { Token(FacilityEvolutionTerms.HighTurnoverService, 1) };
        recipe.identityPressureWeights = new[]
        {
            new FacilityEvolutionValue(FacilityEvolutionTerms.Crowd, 0.7f),
            new FacilityEvolutionValue(FacilityEvolutionTerms.Luxury, -0.2f)
        };
        recipe.minimumIdentityScore = 0.35f;
        return recipe;
    }

    private static FacilityEvolutionRecipeSO CreateFineRecipe(BuildingSO source, BuildingSO result)
    {
        FacilityEvolutionRecipeSO recipe = CreateRecipe("evolve_test_fine_dining", "고급 식당 진화", source, result);
        recipe.requiredRoomScores = new[] { Min(FacilityEvolutionTerms.Dining, 20f) };
        recipe.requiredRoomMetrics = new[]
        {
            Max(FacilityEvolutionTerms.SeatDensity, 0.5f),
            Min(FacilityEvolutionTerms.LuxuryPerSeat, 2f),
            Min(FacilityEvolutionTerms.AverageSpend, 40f)
        };
        recipe.requiredRecordTokens = new[] { Token(FacilityEvolutionTerms.NoblePatronage, 1) };
        recipe.identityPressureWeights = new[]
        {
            new FacilityEvolutionValue(FacilityEvolutionTerms.Luxury, 0.7f),
            new FacilityEvolutionValue(FacilityEvolutionTerms.Service, 0.2f),
            new FacilityEvolutionValue(FacilityEvolutionTerms.Crowd, -0.2f)
        };
        recipe.minimumIdentityScore = 0.35f;
        return recipe;
    }

    private static FacilityEvolutionRecipeSO CreateCrowdIdentityRecipe(BuildingSO source, BuildingSO result)
    {
        FacilityEvolutionRecipeSO recipe = CreateRecipe("evolve_test_identity_crowd", "정체성 대중 식당", source, result);
        recipe.requiredRoomScores = new[] { Min(FacilityEvolutionTerms.Dining, 20f) };
        recipe.identityPressureWeights = new[]
        {
            new FacilityEvolutionValue(FacilityEvolutionTerms.Crowd, 0.75f),
            new FacilityEvolutionValue(FacilityEvolutionTerms.Luxury, -0.25f)
        };
        recipe.minimumIdentityScore = 0.35f;
        return recipe;
    }

    private static FacilityEvolutionRecipeSO CreateFineIdentityRecipe(BuildingSO source, BuildingSO result)
    {
        FacilityEvolutionRecipeSO recipe = CreateRecipe("evolve_test_identity_fine", "정체성 고급 식당", source, result);
        recipe.requiredRoomScores = new[] { Min(FacilityEvolutionTerms.Dining, 20f) };
        recipe.identityPressureWeights = new[]
        {
            new FacilityEvolutionValue(FacilityEvolutionTerms.Luxury, 0.7f),
            new FacilityEvolutionValue(FacilityEvolutionTerms.Service, 0.2f),
            new FacilityEvolutionValue(FacilityEvolutionTerms.Crowd, -0.25f)
        };
        recipe.minimumIdentityScore = 0.35f;
        return recipe;
    }

    private static FacilityEvolutionRecipeSO CreateCombatRecipe(
        BuildingSO source,
        BuildingSO result,
        bool consumeRecordToken)
    {
        FacilityEvolutionRecipeSO recipe = CreateRecipe("evolve_test_combat_dining", "전투 식당 진화", source, result);
        recipe.requiredRoomScores = new[]
        {
            Min(FacilityEvolutionTerms.Dining, 25f),
            Min(FacilityEvolutionTerms.Combat, 12f)
        };
        recipe.requiredRecordTokens = new[] { Token(FacilityEvolutionTerms.MercenaryHangout, 1) };
        recipe.requiredMaterials = new[]
        {
            new FacilityEvolutionMaterialRequirement { materialId = "high_grade_meat", amount = 2 }
        };
        recipe.allowedMutationTags = new[] { FacilityEvolutionTerms.Brutal, FacilityEvolutionTerms.Combat };
        recipe.identityPressureWeights = new[]
        {
            new FacilityEvolutionValue(FacilityEvolutionTerms.Combat, 0.75f),
            new FacilityEvolutionValue(FacilityEvolutionTerms.Crowd, 0.1f),
            new FacilityEvolutionValue(FacilityEvolutionTerms.Luxury, -0.2f)
        };
        recipe.minimumIdentityScore = 0.25f;
        recipe.consumeRecordTokens = consumeRecordToken;
        return recipe;
    }

    private static FacilityEvolutionRecipeSO CreateRecipe(
        string id,
        string name,
        BuildingSO source,
        BuildingSO result)
    {
        FacilityEvolutionRecipeSO recipe = ScriptableObject.CreateInstance<FacilityEvolutionRecipeSO>();
        recipe.id = Math.Abs(id.GetHashCode());
        recipe.evolutionId = id;
        recipe.displayName = name;
        recipe.resultBuilding = result;
        recipe.fromFacilities = new[] { source };
        recipe.requiredStarGrade = 1;
        recipe.resultStarGrade = 2;
        recipe.publicByDefault = true;
        return recipe;
    }

    private static FacilityEvolutionMetricRequirement Min(string key, float value)
    {
        return new FacilityEvolutionMetricRequirement
        {
            key = key,
            requireMin = true,
            minValue = value
        };
    }

    private static FacilityEvolutionMetricRequirement Max(string key, float value)
    {
        return new FacilityEvolutionMetricRequirement
        {
            key = key,
            requireMax = true,
            maxValue = value
        };
    }

    private static FacilityEvolutionTokenRequirement Token(string key, int count)
    {
        return new FacilityEvolutionTokenRequirement
        {
            key = key,
            minCount = count
        };
    }

    private static CharacterActor CreateDebugActor(
        EvolutionScenarioWorld world,
        string name,
        string speciesTag,
        CharacterType type,
        int attack,
        int sales,
        float mood)
    {
        CharacterSO data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
            type,
            name,
            speciesTag);
        world.TrackObject(data);
        data.id = 700000 + Math.Abs(name.GetHashCode() % 100000);
        data.characterName = name;
        data.speciesTag = speciesTag;
        data.characterType = type;
        data.role = CharacterRole.Regular;
        GameObject obj = new GameObject(name);
        world.TrackObject(obj);
        CharacterActor actor = obj.AddComponent<CharacterActor>();
        CharacterAiEditorTestDependencies.Inject(obj);
        actor.EnsureRuntimeState();
        actor.data = data;
        actor.characterType = type;
        actor.Identity.SetPersistentId($"character:facility-evolution-test:{data.id}");
        actor.stats = new Dictionary<CharacterCondition, float>
        {
            { CharacterCondition.SLEEP, 90f },
            { CharacterCondition.HUNGER, 85f },
            { CharacterCondition.FUN, 80f },
            { CharacterCondition.MOOD, mood },
            { CharacterCondition.EXCRETION, 90f },
            { CharacterCondition.HYGIENE, 85f }
        };
        return actor;
    }

    private sealed class StaticFacilityEvolutionRecipeProvider : IFacilityEvolutionRecipeProvider
    {
        private readonly IReadOnlyList<FacilityEvolutionRecipeSO> recipes;

        public StaticFacilityEvolutionRecipeProvider(params FacilityEvolutionRecipeSO[] recipes)
        {
            this.recipes = recipes ?? Array.Empty<FacilityEvolutionRecipeSO>();
        }

        public IReadOnlyList<FacilityEvolutionRecipeSO> GetRecipes()
        {
            return recipes;
        }
    }

    private sealed class StaticRecordTokenDefinitionProvider :
        IFacilityEvolutionRecordTokenDefinitionProvider
    {
        private readonly IReadOnlyList<FacilityEvolutionRecordTokenDefinitionSO> definitions;

        public StaticRecordTokenDefinitionProvider(params FacilityEvolutionRecordTokenDefinitionSO[] definitions)
        {
            this.definitions = definitions ?? Array.Empty<FacilityEvolutionRecordTokenDefinitionSO>();
        }

        public IReadOnlyList<FacilityEvolutionRecordTokenDefinitionSO> GetDefinitions()
        {
            return definitions;
        }

        public FacilityEvolutionRecordTokenDefinitionSO GetDefinition(string tokenId)
        {
            return definitions.FirstOrDefault((definition) =>
                definition != null
                && string.Equals(definition.EffectiveId, tokenId, StringComparison.Ordinal));
        }
    }

    private sealed class StaticWarehouseInventoryQuery :
        IFacilityEvolutionWarehouseInventoryQuery
    {
        private readonly IReadOnlyList<IWarehouseFacility> warehouses;

        public StaticWarehouseInventoryQuery(params WarehouseInventory[] inventories)
        {
            warehouses = (inventories ?? Array.Empty<WarehouseInventory>())
                .Select((inventory, index) => (IWarehouseFacility)new TestWarehouse(
                    inventory,
                    $"building:test-facility-evolution-{index}"))
                .ToArray();
        }

        public IReadOnlyList<IWarehouseFacility> GetWarehouses()
        {
            return warehouses;
        }

        public bool TryGetPending(
            string operationId,
            string reasonCode,
            out FacilityEvolutionMaterialCommitReceipt receipt,
            out string failureReason)
        {
            receipt = default;
            failureReason = string.Empty;
            return false;
        }

        public bool TryCommitPending(
            IReadOnlyList<FacilityEvolutionMaterialDebit> debits,
            string operationId,
            string reasonCode,
            out FacilityEvolutionMaterialCommitReceipt receipt,
            out string failureReason)
        {
            receipt = default;
            failureReason = string.Empty;
            Dictionary<StockCategory, int> totals = (debits
                    ?? Array.Empty<FacilityEvolutionMaterialDebit>())
                .Where(debit => debit.Amount > 0)
                .GroupBy(debit => debit.Category)
                .ToDictionary(group => group.Key, group => group.Sum(debit => debit.Amount));
            if (totals.Any(entry => warehouses.Sum(warehouse =>
                    warehouse.Inventory.GetStock(entry.Key)) < entry.Value))
            {
                failureReason = "facility-evolution-test-material-unavailable";
                return false;
            }
            foreach (KeyValuePair<StockCategory, int> entry in totals
                         .OrderBy(entry => (int)entry.Key))
            {
                int remaining = entry.Value;
                foreach (IWarehouseFacility warehouse in warehouses)
                {
                    remaining -= warehouse.Inventory.ConsumePhysicalStockForTest(
                        entry.Key,
                        remaining);
                    if (remaining == 0)
                    {
                        break;
                    }
                }
            }
            int total = totals.Values.Sum();
            receipt = new FacilityEvolutionMaterialCommitReceipt(
                operationId,
                reasonCode,
                $"physical-batch-disposition:1:{operationId}:{total}:{total}",
                new[] { "facility-evolution-test-stack" },
                total,
                total);
            return true;
        }

        public bool Acknowledge(string commitId, out string failureReason)
        {
            failureReason = string.Empty;
            return !string.IsNullOrWhiteSpace(commitId);
        }
    }

    private sealed class TestWarehouse : IWarehouseFacility
    {
        public TestWarehouse(WarehouseInventory inventory, string id)
        {
            Inventory = inventory;
            PersistentInstanceId = (BuildingInstanceId)id;
        }

        public BuildingInstanceId PersistentInstanceId { get; }
        public WarehouseInventory Inventory { get; }
        public bool HasWarehouseInventory => Inventory != null;
    }

    private sealed class RejectingEvolutionValidator : IFacilityEvolutionValidator
    {
        private readonly string reason;

        public RejectingEvolutionValidator(string reason)
        {
            this.reason = reason;
        }

        public int Calls { get; private set; }

        public FacilityEvolutionValidationResult Validate(
            FacilityEvolutionContext context,
            FacilityEvolutionRecipeSO recipe,
            BlueprintResearchState researchState,
            IFacilityEvolutionResourceProvider resources,
            IFacilityEvolutionBuildingReplacer buildingReplacer)
        {
            Calls++;
            FacilityEvolutionValidationResult result = new FacilityEvolutionValidationResult();
            result.Reject(reason);
            return result;
        }
    }

    private sealed class FailOnceBuildingReplacer : IFacilityEvolutionBuildingReplacer
    {
        private readonly IFacilityEvolutionBuildingReplacer inner;

        internal FailOnceBuildingReplacer(IFacilityEvolutionBuildingReplacer inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public int TryReplaceCalls { get; private set; }

        public bool CanReplace(
            BuildableObject source,
            BuildingSO resultBuilding,
            out string reason) => inner.CanReplace(source, resultBuilding, out reason);

        public bool TryReplace(
            BuildableObject source,
            BuildingSO resultBuilding,
            out BuildableObject result,
            out string reason)
        {
            TryReplaceCalls++;
            if (TryReplaceCalls == 1)
            {
                result = null;
                reason = "injected replacement failure";
                return false;
            }
            return inner.TryReplace(source, resultBuilding, out result, out reason);
        }
    }

    private sealed class RecordingBuildingReplacer : IFacilityEvolutionBuildingReplacer
    {
        private readonly IFacilityEvolutionBuildingReplacer inner;

        internal RecordingBuildingReplacer(IFacilityEvolutionBuildingReplacer inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public int TryReplaceCalls { get; private set; }

        public bool CanReplace(
            BuildableObject source,
            BuildingSO resultBuilding,
            out string reason) => inner.CanReplace(source, resultBuilding, out reason);

        public bool TryReplace(
            BuildableObject source,
            BuildingSO resultBuilding,
            out BuildableObject result,
            out string reason)
        {
            TryReplaceCalls++;
            return inner.TryReplace(source, resultBuilding, out result, out reason);
        }
    }

    private sealed class FailOnceAcknowledgementResourceProvider :
        IFacilityEvolutionResourceProvider
    {
        private readonly IFacilityEvolutionResourceProvider inner;

        internal FailOnceAcknowledgementResourceProvider(
            IFacilityEvolutionResourceProvider inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public int AcknowledgeCalls { get; private set; }

        public bool HasMaterial(string materialId, int amount) =>
            inner.HasMaterial(materialId, amount);

        public bool TryGetPendingMaterialCommit(
            string operationId,
            string reasonCode,
            out FacilityEvolutionMaterialCommitReceipt receipt,
            out string failureReason) => inner.TryGetPendingMaterialCommit(
            operationId,
            reasonCode,
            out receipt,
            out failureReason);

        public bool TryCommitMaterialsPending(
            IReadOnlyList<FacilityEvolutionMaterialRequirement> requirements,
            string operationId,
            string reasonCode,
            out FacilityEvolutionMaterialCommitReceipt receipt,
            out string failureReason) => inner.TryCommitMaterialsPending(
            requirements,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);

        public bool AcknowledgeMaterialCommit(
            string commitId,
            out string failureReason)
        {
            AcknowledgeCalls++;
            if (AcknowledgeCalls == 1)
            {
                failureReason = "injected acknowledgement failure";
                return false;
            }
            return inner.AcknowledgeMaterialCommit(commitId, out failureReason);
        }
    }

    private sealed class GridOccupantBuildingQuery : IBuildingWorldQuery
    {
        private readonly Grid grid;
        private readonly Vector2Int position;

        internal GridOccupantBuildingQuery(Grid grid, Vector2Int position)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            this.position = position;
        }

        public int BuildingVersion => 0;

        public IReadOnlyList<BuildableObject> Buildings
        {
            get
            {
                BuildableObject building = grid
                    .GetGridCell(position)
                    ?.GetOccupant(GridLayer.Building) as BuildableObject;
                return building != null
                    ? new[] { building }
                    : Array.Empty<BuildableObject>();
            }
        }
    }

    private sealed class EvolutionScenarioWorld : IDisposable
    {
        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
        private readonly IBlueprintResearchWorkService blueprintResearchWorkService =
            new NoopBlueprintResearchWorkService();
        private readonly IWorldInfoClickSelector worldInfoClickSelector =
            new NoopWorldInfoClickSelector();
        private readonly IFacilityCandidateCache facilityCandidateCache =
            new FacilityCandidateCacheStore(CharacterAiEditorTestDependencies.WorldRegistry, frameWorkBudget: null);
        private readonly IRoomFacilityPolicy roomFacilityPolicy =
            new RoomFacilityPolicyService(new RoomLayoutCache());
        private int nextBuildingId = 5100;

        private EvolutionScenarioWorld()
        {
            Grid = new Grid(14, 1);
            SourcePosition = new Vector2Int(3, 0);
            for (int x = 0; x <= 12; x++)
            {
                AddHallway(new Vector2Int(x, 0));
            }

            PlaceDoor(new Vector2Int(1, 0));
            PlaceWall(new Vector2Int(12, 0));

            SourceData = CreateBuildingData("일반 식당", FacilityRole.Meal);
            SourceFacility = Place(SourceData, SourcePosition);
            CrowdResultData = CreateBuildingData("대중 식당", FacilityRole.Meal);
            FineResultData = CreateBuildingData("고급 식당", FacilityRole.Meal);
            CombatResultData = CreateBuildingData("전투 식당", FacilityRole.Meal | FacilityRole.Training);
        }

        public Grid Grid { get; }
        public Vector2Int SourcePosition { get; }
        public BuildingSO SourceData { get; private set; }
        public BuildingSO CrowdResultData { get; private set; }
        public BuildingSO FineResultData { get; private set; }
        public BuildingSO CombatResultData { get; private set; }
        public BuildableObject SourceFacility { get; private set; }

        public static EvolutionScenarioWorld CreateCrowdedDining()
        {
            EvolutionScenarioWorld world = new EvolutionScenarioWorld();
            world.PlaceTableSet(new Vector2Int(5, 0), seats: 4, dining: 4f, luxury: 0f);
            world.PlaceTableSet(new Vector2Int(6, 0), seats: 4, dining: 4f, luxury: 0f);
            world.PlaceTableSet(new Vector2Int(7, 0), seats: 4, dining: 4f, luxury: 0f);
            world.AddRecord(world.SourceFacility, (FacilityEvolutionTerms.TurnoverRate, 0.8f));
            world.AddToken(world.SourceFacility, FacilityEvolutionTerms.HighTurnoverService, 1);
            return world;
        }

        public static EvolutionScenarioWorld CreateFineDining()
        {
            EvolutionScenarioWorld world = new EvolutionScenarioWorld();
            world.PlaceTableSet(new Vector2Int(5, 0), seats: 2, dining: 4f, luxury: 16f, privateSeats: 2);
            world.PlaceDecor(new Vector2Int(7, 0), luxury: 12f, service: 6f);
            world.AddRecord(
                world.SourceFacility,
                (FacilityEvolutionTerms.AverageSpend, 55f),
                (FacilityEvolutionTerms.NobleVisitorRatio, 0.7f));
            world.AddToken(world.SourceFacility, FacilityEvolutionTerms.NoblePatronage, 1);
            return world;
        }

        public static EvolutionScenarioWorld CreateCombatDining()
        {
            EvolutionScenarioWorld world = new EvolutionScenarioWorld();
            world.PlaceTableSet(new Vector2Int(5, 0), seats: 4, dining: 5f, luxury: 0f);
            world.PlaceTrainingFixture(new Vector2Int(6, 0));
            world.AddToken(world.SourceFacility, FacilityEvolutionTerms.MercenaryHangout, 2);
            world.AddToken(world.SourceFacility, FacilityEvolutionTerms.HighMeatConsumption, 1);
            world.AddEvent(world.SourceFacility, "용병들이 식당과 훈련 더미를 번갈아 이용했다.");
            return world;
        }

        public FacilityEvolutionEngine CreateEngine(
            IFacilityEvolutionRecipeProvider recipes,
            IFacilityEvolutionResourceProvider resources = null,
            IFacilityEvolutionProposalProvider proposalProvider = null,
            IFacilityEvolutionValidator validator = null,
            IFacilityEvolutionCandidateBuilder candidateBuilder = null,
            IFacilityEvolutionRecordTokenConsumer recordTokenConsumer = null,
            IFacilityEvolutionMutationResolver mutationResolver = null,
            IFacilityEvolutionBuildingReplacer buildingReplacer = null)
        {
            FacilityEvolutionRecordComponentService records =
                new FacilityEvolutionRecordComponentService(new FacilityEvolutionRecordComponentFactory());
            IRoomLayoutCache rooms = new RoomLayoutCache();
            IFacilityEvolutionStateComponentFactory states =
                new FacilityEvolutionStateComponentFactory();
            IFacilityCandidateCache candidateCache = new FacilityCandidateCacheStore(CharacterAiEditorTestDependencies.WorldRegistry, frameWorkBudget: null);
            IFacilityEvolutionRecipeQuery recipeQuery =
                new EditorFacilityEvolutionRecipeQuery(recipes, states);
            IFacilityEvolutionValidator resolvedValidator = validator
                ?? new DefaultFacilityEvolutionValidator(recipeQuery, states);
            IFacilityEvolutionCandidateBuilder resolvedCandidateBuilder =
                candidateBuilder
                ?? new DefaultFacilityEvolutionCandidateBuilder(resolvedValidator);
            FacilityEvolutionDefinitionContext definitions =
                new FacilityEvolutionDefinitionContext(
                    recipeQuery,
                    new RoomProfileBuilder(records, rooms),
                    records,
                    proposalProvider ?? new RuleBasedFacilityEvolutionProposalProvider(),
                    rooms,
                    states,
                    records);
            FacilityEvolutionExecutionContext execution =
                new FacilityEvolutionExecutionContext(
                    resources ?? new EmptyFacilityEvolutionResourceProvider(),
                    buildingReplacer ?? CreateReplacer(),
                    candidateCache,
                    () => null,
                    resolvedValidator,
                    resolvedCandidateBuilder,
                    recordTokenConsumer ?? new DefaultFacilityEvolutionRecordTokenConsumer(
                        new EmptyFacilityEvolutionRecordTokenDefinitionProvider()),
                    mutationResolver ?? new DefaultFacilityEvolutionMutationResolver());
            return new FacilityEvolutionEngine(definitions, execution);
        }

        public FacilityEvolutionRuntime CreateRuntime(
            IFacilityEvolutionRecipeProvider recipes,
            IFacilityEvolutionResourceProvider resources = null,
            IFacilityEvolutionBuildingReplacer buildingReplacer = null)
        {
            GameObject obj = new GameObject("FacilityEvolutionRuntime");
            cleanup.Add(obj);

            FacilityEvolutionRecordComponentService records =
                new FacilityEvolutionRecordComponentService(new FacilityEvolutionRecordComponentFactory());
            IRoomLayoutCache rooms = new RoomLayoutCache();
            IFacilityEvolutionStateComponentFactory states =
                new FacilityEvolutionStateComponentFactory();
            IFacilityCandidateCache candidateCache = new FacilityCandidateCacheStore(CharacterAiEditorTestDependencies.WorldRegistry, frameWorkBudget: null);
            IFacilityEvolutionRecipeQuery recipeQuery =
                new EditorFacilityEvolutionRecipeQuery(recipes, states);
            IFacilityEvolutionValidator validator =
                new DefaultFacilityEvolutionValidator(recipeQuery, states);
            FacilityEvolutionRuntime runtime = obj.AddComponent<FacilityEvolutionRuntime>();
            runtime.Configure(
                recipeQuery,
                new RoomProfileBuilder(records, rooms),
                records,
                new RuleBasedFacilityEvolutionProposalProvider(),
                resources ?? new EmptyFacilityEvolutionResourceProvider(),
                buildingReplacer ?? CreateReplacer(),
                rooms,
                states,
                candidateCache,
                nextRecordTokenConsumer: new DefaultFacilityEvolutionRecordTokenConsumer(
                    new EmptyFacilityEvolutionRecordTokenDefinitionProvider()),
                nextRecordComponentService: records,
                nextResearchStateService: new EmptyBlueprintResearchStateService(),
                nextGameEventBus: new DungeonStory.Foundation.GameEventBus(),
                nextValidator: validator,
                nextCandidateBuilder: new DefaultFacilityEvolutionCandidateBuilder(validator),
                nextBuildingReplacerFactory: null,
                nextMutationResolver: new DefaultFacilityEvolutionMutationResolver(),
                nextEngineFactory: new FacilityEvolutionEngineFactory());
            return runtime;
        }

        public sealed class EditorFacilityEvolutionRecipeQuery : IFacilityEvolutionRecipeQuery
        {
            private readonly IFacilityEvolutionRecipeProvider provider;
            private readonly IFacilityEvolutionStateComponentFactory stateComponentFactory;

            public EditorFacilityEvolutionRecipeQuery(
                IFacilityEvolutionRecipeProvider provider,
                IFacilityEvolutionStateComponentFactory stateComponentFactory)
            {
                this.provider = provider
                    ?? throw new ArgumentNullException(nameof(provider));
                this.stateComponentFactory = stateComponentFactory
                    ?? throw new ArgumentNullException(nameof(stateComponentFactory));
            }

            public IReadOnlyList<FacilityEvolutionRecipeSO> GetRecipes()
            {
                return provider.GetRecipes();
            }

            public bool IsVisible(FacilityEvolutionRecipeSO recipe, BlueprintResearchState researchState)
            {
                return FacilityEvolutionService.IsRecipeVisible(recipe, researchState, null);
            }

            public IReadOnlyList<FacilityEvolutionRecipeSO> GetVisibleRecipes(BlueprintResearchState researchState)
            {
                return GetRecipes()
                    .Where((recipe) => IsVisible(recipe, researchState))
                    .ToArray();
            }

            public IReadOnlyList<FacilityEvolutionRecipeSO> GetSourceCandidates(
                BuildableObject facility,
                BlueprintResearchState researchState)
            {
                return FacilityEvolutionService.GetSourceCandidates(
                    facility,
                    GetRecipes(),
                    researchState,
                    this,
                    stateComponentFactory);
            }
        }

        public void TrackObject(UnityEngine.Object obj)
        {
            if (obj != null)
            {
                cleanup.Add(obj);
            }
        }

        public void Dispose()
        {
            RoomRegistry.Clear();
            FacilityCandidateCache.Clear();
            foreach (UnityEngine.Object obj in cleanup.Where((obj) => obj != null))
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        private void AddHallway(Vector2Int position)
        {
            Grid.RegisterOccupant(
                new TestHallwayOccupant(),
                GridLayer.Hallway,
                new List<Vector2Int> { position },
                false);
        }

        internal GridFacilityEvolutionBuildingReplacer CreateReplacer()
        {
            return new GridFacilityEvolutionBuildingReplacer(new GridBuildingFactory((created) =>
            {
                if (created != null)
                {
                    InjectBuildable(created);
                    cleanup.Add(created.gameObject);
                }
            }));
        }

        private void PlaceDoor(Vector2Int position)
        {
            BuildingSO data = CreateBuildingData("문", FacilityRole.None);
            data.category = BuildingCategory.None;
            data.runtimeArchetype = BuildingRuntimeArchetypeKind.Door;
            Place(data, position);
        }

        private void PlaceWall(Vector2Int position)
        {
            BuildingSO data = CreateBuildingData("벽", FacilityRole.None);
            data.category = BuildingCategory.Wall;
            Place(data, position);
        }

        private void PlaceTableSet(
            Vector2Int position,
            int seats,
            float dining,
            float luxury,
            int privateSeats = 0)
        {
            BuildingSO data = CreateFixtureData(
                "식탁 세트",
                new[] { FacilityEvolutionTerms.Dining },
                new[]
                {
                    new FacilityEvolutionValue(FacilityEvolutionTerms.Dining, dining),
                    new FacilityEvolutionValue(FacilityEvolutionTerms.Luxury, luxury)
                },
                new[]
                {
                    new FacilityEvolutionValue(FacilityEvolutionTerms.SeatCount, seats),
                    new FacilityEvolutionValue(FacilityEvolutionTerms.TableCount, 1),
                    new FacilityEvolutionValue(FacilityEvolutionTerms.PrivateSeatCount, privateSeats)
                });
            Place(data, position);
        }

        private void PlaceDecor(Vector2Int position, float luxury, float service)
        {
            BuildingSO data = CreateFixtureData(
                "고급 장식",
                new[] { FacilityEvolutionTerms.Luxury, FacilityEvolutionTerms.Quiet },
                new[]
                {
                    new FacilityEvolutionValue(FacilityEvolutionTerms.Luxury, luxury),
                    new FacilityEvolutionValue(FacilityEvolutionTerms.Service, service)
                },
                Array.Empty<FacilityEvolutionValue>());
            Place(data, position);
        }

        private void PlaceTrainingFixture(Vector2Int position)
        {
            BuildingSO data = CreateFixtureData(
                "훈련 더미",
                new[] { FacilityEvolutionTerms.Training, FacilityEvolutionTerms.Combat },
                new[]
                {
                    new FacilityEvolutionValue(FacilityEvolutionTerms.Training, 18f),
                    new FacilityEvolutionValue(FacilityEvolutionTerms.Combat, 15f)
                },
                Array.Empty<FacilityEvolutionValue>());
            Place(data, position);
        }

        private BuildingSO CreateFixtureData(
            string name,
            string[] tags,
            FacilityEvolutionValue[] scores,
            FacilityEvolutionValue[] metrics)
        {
            BuildingSO data = CreateBuildingData(name, FacilityRole.None);
            data.Evolution = new FacilityEvolutionContributionData
            {
                contributesToRoomProfile = true,
                tags = tags,
                scores = scores,
                metrics = metrics
            };
            return data;
        }

        private BuildingSO CreateBuildingData(string name, FacilityRole roles)
        {
            BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
            cleanup.Add(data);
            data.id = nextBuildingId++;
            data.objectName = name;
            data.sprite = CreateDebugSprite();
            data.width = 1;
            data.height = 1;
            data.layer = GridLayer.Building;
            data.category = roles == FacilityRole.None ? BuildingCategory.None : BuildingCategory.Special;
            data.runtimeArchetype = roles == FacilityRole.None
                ? BuildingRuntimeArchetypeKind.Generic
                : BuildingRuntimeArchetypeKind.Facility;
            data.unlocked = true;
            data.Facility = new FacilityData
            {
                roles = roles,
                capacity = roles == FacilityRole.None ? 0 : 4,
                useDuration = roles == FacilityRole.None ? 0f : 1f,
                disabledWhenDamaged = true
            };
            if (roles != FacilityRole.None)
            {
                data.AbilityModules.Add(new BuildingRoomRequirementAbility());
            }

            return data;
        }

        private Sprite CreateDebugSprite()
        {
            Sprite sprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f),
                16f);
            cleanup.Add(sprite);
            return sprite;
        }

        private BuildableObject Place(BuildingSO data, Vector2Int position)
        {
            GameObject obj = new GameObject(data.objectName);
            cleanup.Add(obj);
            BuildableObject building = data.runtimeArchetype == BuildingRuntimeArchetypeKind.Facility
                ? obj.AddComponent<Facility>()
                : obj.AddComponent<BuildableObject>();
            InjectBuildable(building);
            building.SetGrid(Grid);
            building.Initialization(data, position);
            bool registered = Grid.RegisterOccupant(
                building,
                data.layer,
                data.GetGridPosList(position),
                data.Placement.IsMovement);
            if (!registered)
            {
                throw new InvalidOperationException($"{data.objectName} registration failed.");
            }

            return building;
        }

        private void InjectBuildable(BuildableObject building)
        {
            building?.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
            building?.ConstructBuildableObject(
                new BuildingResearchWorkPortAdapter(blueprintResearchWorkService),
                facilityCandidateCache,
                roomFacilityPolicy, combatEquipmentRuntime: null,
                worldRegistry: (IBuildingWorldRegistryPort)
                    CharacterAiEditorTestDependencies.WorldRegistry,
                worldItemStackRuntime: null, abilityRuntimeDispatcher: null,
                gameClock: null, paidFacilityContracts: null,
                evolutionState: new FacilityEvolutionStateComponentFactory());
        }

        private void AddRecord(BuildableObject facility, params (string key, float value)[] metrics)
        {
            FacilityEvolutionRecordComponent record = GetRecordComponent(facility);
            foreach ((string key, float value) in metrics)
            {
                record.SetMetric(key, value);
            }
        }

        private void AddToken(BuildableObject facility, string key, int count)
        {
            GetRecordComponent(facility).AddToken(key, count);
        }

        private void AddEvent(BuildableObject facility, string text)
        {
            GetRecordComponent(facility).AddRecentEvent(text);
        }

        private static FacilityEvolutionRecordComponent GetRecordComponent(BuildableObject facility)
        {
            return facility.GetComponent<FacilityEvolutionRecordComponent>()
                ?? facility.gameObject.AddComponent<FacilityEvolutionRecordComponent>();
        }
    }

    private sealed class NoopBlueprintResearchWorkService : IBlueprintResearchWorkService
    {
        public bool HasResearchWorkFor(BuildableObject facility)
        {
            return false;
        }

        public BlueprintResearchWorkResult ApplyResearchWork(
            CharacterActor researcher,
            BuildableObject researchFacility,
            float seconds)
        {
            return new BlueprintResearchWorkResult(
                false,
                null,
                0f,
                0f,
                1f,
                false,
                "No blueprint research runtime in facility evolution scenario.");
        }

        public BlueprintResearchWorkResult ApplyApprovedResearchWork(
            CharacterActor researcher,
            BuildableObject researchFacility,
            float approvedWorkUnits) =>
            ApplyResearchWork(researcher, researchFacility, approvedWorkUnits);
    }

    private sealed class NoopWorldInfoClickSelector : IWorldInfoClickSelector
    {
        public bool TryHandleWorldInfoClick()
        {
            return false;
        }

        public bool TryTriggerCharacterUnderPointer()
        {
            return false;
        }

        public bool TryGetPreferredCharacterUnderPointer(out CharacterActor actor)
        {
            actor = null;
            return false;
        }

        public bool TryGetPreferredCharacterAtScreenPosition(
            Vector3 screenPosition,
            Camera camera,
            out CharacterActor actor)
        {
            actor = null;
            return false;
        }

        public bool TryGetPreferredCharacter(Collider2D[] hits, out CharacterActor actor)
        {
            actor = null;
            return false;
        }
    }

    private sealed class EmptyBlueprintResearchStateService : IBlueprintResearchStateService
    {
        public BlueprintResearchState GetState()
        {
            return null;
        }
    }

    private sealed class CountingEvolutionCompletedListener : IDisposable
    {
        private readonly IDisposable subscription;
        public int Count { get; private set; }

        public CountingEvolutionCompletedListener(DungeonStory.Foundation.IGameEventBus gameEventBus)
        {
            subscription = gameEventBus.Subscribe<FacilityEvolutionCompletedEvent>(OnTriggerEvent);
        }

        public void OnTriggerEvent(FacilityEvolutionCompletedEvent eventType)
        {
            if (eventType.result.Success)
            {
                Count++;
            }
        }

        public void Dispose()
        {
            subscription.Dispose();
        }
    }

    private sealed class TestHallwayOccupant : IGridOccupant
    {
        public int GridId => 0;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
    }

    private sealed class FakeLlmRuntime : ILocalLlmRuntime
    {
        private readonly string facilityEvolutionResponse;

        public FakeLlmRuntime(string facilityEvolutionResponse)
        {
            this.facilityEvolutionResponse = facilityEvolutionResponse;
        }

        public int FacilityEvolutionRequestCount { get; private set; }
        public string LastPrompt { get; private set; }

        public bool GenerateCharacterSkillAsync(string prompt, Action<LocalLlmResult> callback)
        {
            callback?.Invoke(Failed("CharacterSkill not supported by fake LLM."));
            return false;
        }

        public bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> callback)
        {
            callback?.Invoke(Failed("Persona not supported by fake LLM."));
            return false;
        }

        public bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> callback)
        {
            callback?.Invoke(Failed("MacroGoal not supported by fake LLM."));
            return false;
        }

        public bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> callback)
        {
            callback?.Invoke(Failed("MoodImpulse not supported by fake LLM."));
            return false;
        }

        public bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> callback)
        {
            callback?.Invoke(Failed("SocialRumor not supported by fake LLM."));
            return false;
        }

        public bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> callback)
        {
            FacilityEvolutionRequestCount++;
            LastPrompt = prompt;
            callback?.Invoke(new LocalLlmResult(
                LocalLlmRequestStatus.Succeeded,
                facilityEvolutionResponse,
                string.Empty,
                string.Empty));
            return true;
        }

        public bool GenerateBubbleLineAsync(string prompt, string originalText, Action<LocalLlmResult> callback)
        {
            callback?.Invoke(Failed("BubbleLine not supported by fake LLM."));
            return false;
        }

        public bool GenerateCharacterRecordAsync(string prompt, string originalText, Action<LocalLlmResult> callback)
        {
            callback?.Invoke(Failed("CharacterRecord not supported by fake LLM."));
            return false;
        }

        private static LocalLlmResult Failed(string message)
        {
            return new LocalLlmResult(
                LocalLlmRequestStatus.Failed,
                string.Empty,
                message,
                string.Empty);
        }
    }
}
