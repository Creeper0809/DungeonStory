#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class V25NarrativeInferenceDebugScenarios
{
    [MenuItem("DungeonStory/Debug/V25/Run Narrative Inference Contracts")]
    public static void RunAll()
    {
        VerifyPublicNarrativeContinuityContract();
        VerifyChoiceCanonicalizationAndGrammar();
        VerifyAcquiredTraitExactResponseContract();
        VerifyContextAwareScheduling();
        VerifyTypedEquipmentEvidence();
        VerifyMechanicalNarrativeSeparation();
        VerifyMultiPerspectiveIdentity();
        VerifyMissingHostFailsClosed();
        VerifyCorruptModelFailsClosed();
        VerifyBundledLlamaCppBackendContract();
        VerifyCharacterSkillPublicSemanticsContract();
        VerifyCanonicalCatalogJsonParity();
        VerifyIndependentCatalogExportBytes();
        Debug.Log("V25 narrative inference contracts PASS (14/14).");
    }

    public static void RunPublicNarrativeContinuityScenario() =>
        VerifyPublicNarrativeContinuityContract();

    public static void RunAcquiredTraitExactResponseScenario() =>
        VerifyAcquiredTraitExactResponseContract();

    private static void VerifyPublicNarrativeContinuityContract()
    {
        const string EquipmentId = "equipment:continuity:검증";
        const string ActorId = "character:continuity:행위자";
        string sharedLongFactId = "event:" + new string('x', 180) + ":공유-꼬리";
        NarrativePublicEntityInput[] entities =
        {
            new NarrativePublicEntityInput(
                EquipmentId,
                NarrativePublicEntityKind.Equipment,
                "계승 장비"),
            new NarrativePublicEntityInput(
                ActorId,
                NarrativePublicEntityKind.Character,
                "검증 행위자")
        };
        NarrativePublicFactInput[] facts =
        {
            BuildContinuityFact(
                "combat",
                sharedLongFactId,
                EquipmentId,
                EquipmentId,
                ActorId,
                "같은 원본 ID의 전투 기록"),
            BuildContinuityFact(
                "work",
                sharedLongFactId,
                EquipmentId,
                EquipmentId,
                ActorId,
                "같은 원본 ID의 작업 기록"),
            BuildContinuityFact(
                "combat",
                sharedLongFactId,
                "equipment:other-life",
                EquipmentId,
                ActorId,
                "다른 생애의 동일 원본 ID")
        };

        NarrativePublicContextMaterial material = NarrativePublicContextFactory.Build(
            "EquipmentChoiceLegacyV2",
            EquipmentId,
            NarrativePublicSubjectKind.Equipment,
            string.Empty,
            requireCharacterFact: false,
            requireMotif: false,
            entities,
            facts,
            "qa-continuity-selection-v1");

        Require(material.PublicFacts.Count == 3,
            "Public continuity material lost a distinct source tuple.");
        Require(material.PublicFacts.Select(value => value.FactId)
                .Distinct(StringComparer.Ordinal).Count() == 3,
            "Shared raw fact IDs collided across domain or source subject.");
        Require(material.PublicFacts.All(value =>
                value.FactId.StartsWith("public-fact:sha256:", StringComparison.Ordinal)
                && value.FactId.Length == "public-fact:sha256:".Length + 64),
            "Projected fact IDs are not full collision-resistant SHA-256 identities.");
        Require(material.Events.Count == 3,
            "Readable event payloads were not preserved for equipment choice.");

        NarrativePublicPromptEnvelope promptEnvelope =
            NarrativePublicPromptEnvelope.Create("공개 서사 전달 검증", material);
        string finalModelPrompt = NarrativeRequestContext.ToModelPrompt(
            promptEnvelope.Prompt);
        Require(
            NarrativePublicModelInput.TryCaptureSnapshotFromPrompt(
                finalModelPrompt,
                out NarrativePublicModelInputProjection finalProjection),
            "Final model prompt lost its structured public narrative payload.");
        NarrativePublicModelContext finalContext =
            finalProjection.PublicNarrativeContext;
        NarrativeMechanicScenarioPublicContext exportedContext =
            NarrativeMechanicScenarioPublicContextSerializer.Serialize(material);
        string exportedModelInput = NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "publicFacts",
                    exportedContext.PublicFacts),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "publicNarrativeContext",
                    exportedContext.PublicNarrativeContext))
            .ToCanonicalString();
        Require(finalContext.Entities.Any(value =>
                value != null
                && string.Equals(value.EntityId, ActorId, StringComparison.Ordinal)
                && string.Equals(value.DisplayName, "검증 행위자", StringComparison.Ordinal))
            && finalContext.Events.Count == 3
            && finalContext.Events.All(value => value.Day == 12
                && value.Count == 1
                && value.Generation == 2
                && string.Equals(value.ActorId, ActorId, StringComparison.Ordinal))
            && finalContext.MemoryFactIds.Count == 3
            && finalContext.Selection.AvailableFactCount == 3
            && finalContext.Selection.OmittedFactCount == 0
            && finalProjection.PublicFacts.Count == 3
            && finalModelPrompt.Contains(
                promptEnvelope.ModelInput.CanonicalPayload,
                StringComparison.Ordinal)
            && string.Equals(
                exportedModelInput,
                promptEnvelope.ModelInput.CanonicalPayload,
                StringComparison.Ordinal),
            "Final model prompt or export changed dates, participants, counts, generations, references, selection metadata, or public serialization.");

        NarrativePublicContextMaterial changedDate = NarrativePublicContextFactory.Build(
            "EquipmentChoiceLegacyV2",
            EquipmentId,
            NarrativePublicSubjectKind.Equipment,
            string.Empty,
            requireCharacterFact: false,
            requireMotif: false,
            entities,
            new[]
            {
                BuildContinuityFact(
                    "combat",
                    sharedLongFactId,
                    EquipmentId,
                    EquipmentId,
                    ActorId,
                    "같은 원본 ID의 전투 기록",
                    day: 13),
                facts[1],
                facts[2]
            },
            "qa-continuity-selection-v1");
        NarrativePublicContextMaterial changedTarget = NarrativePublicContextFactory.Build(
            "EquipmentChoiceLegacyV2",
            EquipmentId,
            NarrativePublicSubjectKind.Equipment,
            string.Empty,
            requireCharacterFact: false,
            requireMotif: false,
            entities,
            new[]
            {
                BuildContinuityFact(
                    "combat",
                    sharedLongFactId,
                    EquipmentId,
                    ActorId,
                    ActorId,
                    "같은 원본 ID의 전투 기록"),
                facts[1],
                facts[2]
            },
            "qa-continuity-selection-v1");
        string changedDatePrompt = NarrativeRequestContext.ToModelPrompt(
            NarrativePublicPromptEnvelope.Create("공개 서사 전달 검증", changedDate).Prompt);
        string changedTargetPrompt = NarrativeRequestContext.ToModelPrompt(
            NarrativePublicPromptEnvelope.Create("공개 서사 전달 검증", changedTarget).Prompt);
        Require(!string.Equals(finalModelPrompt, changedDatePrompt, StringComparison.Ordinal)
                && !string.Equals(finalModelPrompt, changedTargetPrompt, StringComparison.Ordinal)
                && !string.Equals(changedDatePrompt, changedTargetPrompt, StringComparison.Ordinal),
            "Date or target changes did not reach the final model input.");

        NarrativePublicContextMaterial restored = NarrativePublicContextFactory.Restore(
            material.CaptureSnapshot());
        Require(string.Equals(restored.SemanticHash, material.SemanticHash, StringComparison.Ordinal),
            "Public narrative snapshot round-trip changed its semantic hash.");
        Require(restored.PublicFacts.Select(value => value.FactId)
                .SequenceEqual(material.PublicFacts.Select(value => value.FactId), StringComparer.Ordinal),
            "Public narrative snapshot round-trip changed projected fact identities.");

        string delimiterA = NarrativePublicContextFactory.BuildProjectedFactId(
            "도메인:1;subject#2",
            "원본#3:값;한글",
            "대상:4;끝");
        string delimiterB = NarrativePublicContextFactory.BuildProjectedFactId(
            "도메인",
            "1;subject#2:원본#3:값",
            "한글;대상:4;끝");
        Require(!string.Equals(delimiterA, delimiterB, StringComparison.Ordinal),
            "Length-prefixed tuple encoding collapsed delimiter-heavy Korean IDs.");

        string boundA = NarrativePublicContextIdentity.Bind("request:continuity", material.SemanticHash);
        string changedHash = NarrativePublicContextFactory.Build(
            "EquipmentChoiceLegacyV2",
            EquipmentId,
            NarrativePublicSubjectKind.Equipment,
            string.Empty,
            requireCharacterFact: false,
            requireMotif: false,
            entities,
            facts.Take(2),
            "qa-continuity-selection-v1").SemanticHash;
        string boundB = NarrativePublicContextIdentity.Bind("request:continuity", changedHash);
        Require(!string.Equals(boundA, boundB, StringComparison.Ordinal),
            "Request identity ignored a changed public narrative context.");

        NarrativePublicFactInput[] overflowFacts = Enumerable.Range(0, 25)
            .Select(index => new NarrativePublicFactInput(
                "persona",
                $"fact:{index:D2}",
                ActorId,
                $"공개 사실 {index:D2}",
                100 - index,
                NarrativePublicFactCategory.Background,
                ActorId))
            .ToArray();
        NarrativePublicContextMaterial selected = NarrativePublicContextFactory.Build(
            "Persona",
            ActorId,
            NarrativePublicSubjectKind.Character,
            string.Empty,
            requireCharacterFact: false,
            requireMotif: false,
            new[]
            {
                new NarrativePublicEntityInput(
                    ActorId,
                    NarrativePublicEntityKind.Character,
                    "검증 인물")
            },
            overflowFacts,
            "qa-continuity-selection-v1");
        Require(selected.PublicFacts.Count == NarrativeRequestContext.MaximumFacts
                && selected.Selection.AvailableFactCount == 25
                && selected.Selection.OmittedFactCount == 1,
            "Deterministic public-context selection did not account for omitted facts.");
    }

    private static NarrativePublicFactInput BuildContinuityFact(
        string domain,
        string originalFactId,
        string sourceSubjectId,
        string publicSubjectId,
        string actorId,
        string text,
        int day = 12)
    {
        return new NarrativePublicFactInput(
            domain,
            originalFactId,
            sourceSubjectId,
            text,
            100,
            NarrativePublicFactCategory.Memory,
            publicSubjectId,
            "completed",
            lastDay: day,
            count: 1,
            eventData: new NarrativePublicEventInput(
                "evidence:" + domain + ":" + sourceSubjectId,
                actorId,
                publicSubjectId,
                "continuity-event",
                "completed",
                day: day,
                count: 1,
                generation: 2));
    }

    private static void VerifyAcquiredTraitExactResponseContract()
    {
        const string valid =
            "{\"presentationId\":\"presentation:trait:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\","
            + "\"displayName\":\"전장의 담력\","
            + "\"narrativeFlavor\":\"위기를 거듭 넘기며 침착함을 익혔다.\"}";
        Require(
            NarrativeExactKeyContract.TryValidateProfileResponse(
                LocalLlmRequestProfiles.AcquiredTrait.Id,
                valid,
                out _,
                out string exactError),
            "valid AcquiredTrait exact-key response failed: " + exactError);
        Require(
            LlmJsonResponseParser.TryParse(
                LocalLlmRequestProfiles.AcquiredTrait.Id,
                valid,
                out CharacterAcquiredTraitFormulaPresentationDto parsed,
                out string parseError)
            && string.Equals(parsed.displayName, "전장의 담력", StringComparison.Ordinal),
            "valid AcquiredTrait payload failed to parse: " + parseError);

        Require(
            !NarrativeExactKeyContract.TryValidateProfileResponse(
                LocalLlmRequestProfiles.AcquiredTrait.Id,
                valid.Insert(valid.Length - 1, ",\"combatMultiplier\":1.04"),
                out _,
                out _),
            "AcquiredTrait accepted a model-owned mechanical field.");
        Require(
            !NarrativeExactKeyContract.TryValidateProfileResponse(
                LocalLlmRequestProfiles.AcquiredTrait.Id,
                "{\"presentationId\":\"presentation:trait:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\","
                + "\"displayName\":\"전장의 담력\","
                + "\"description\":\"설명\"}",
                out _,
                out _),
            "AcquiredTrait accepted legacy mechanic fields or a missing narrativeFlavor key.");
        Require(
            !NarrativeExactKeyContract.TryValidateProfileResponse(
                LocalLlmRequestProfiles.AcquiredTrait.Id,
                valid.Replace(
                    "\"displayName\":\"전장의 담력\"",
                    "\"displayName\":\"전장의 담력\",\"displayName\":\"중복\""),
                out _,
                out _),
            "AcquiredTrait accepted a duplicate response key.");

        const string moduleSelection =
            "{\"selectionId\":\"selection:trait:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"," 
            + "\"positiveModuleIds\":[\"acquired-trait:module:battle-temper\"],"
            + "\"drawbackModuleIds\":[],"
            + "\"evidenceFactIds\":[\"public-fact:sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"],"
            + "\"displayName\":\"전장의 담력\","
            + "\"narrativeFlavor\":\"위기를 넘기며 침착함을 익혔다.\"}";
        Require(
            NarrativeExactKeyContract.TryValidateProfileResponse(
                LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id,
                moduleSelection, out _, out string selectionError),
            "valid AcquiredTraitModuleSelection response failed: " + selectionError);
        Require(
            LlmJsonResponseParser.TryParse(
                LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id,
                moduleSelection,
                out CharacterAcquiredTraitModuleSelectionResponseDto selection,
                out string selectionParseError)
            && selection.positiveModuleIds.Count == 1,
            "valid AcquiredTraitModuleSelection payload failed to parse: " + selectionParseError);
        Require(
            !NarrativeExactKeyContract.TryValidateProfileResponse(
                LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id,
                moduleSelection.Insert(moduleSelection.Length - 1, ",\"magnitude\":99"),
                out _, out _),
            "AcquiredTraitModuleSelection accepted a model-owned numeric field.");
    }

    private static void VerifyChoiceCanonicalizationAndGrammar()
    {
        string[] suffixes = { string.Empty, " ", "\t", "\n", " \t\r\n" };
        foreach (string suffix in suffixes)
        {
            Require(ChoicePromptCanonicalizer.TryCanonicalize(
                    "후보 0과 1 중 선택" + suffix,
                    out ChoicePromptDiagnostic diagnostic,
                    out string error),
                "choice canonicalization failed: " + error);
            Require(diagnostic.Prompt.EndsWith(
                    ChoicePromptCanonicalizer.FinalMarker,
                    StringComparison.Ordinal),
                "choice prompt marker mismatch");
            Require(!char.IsWhiteSpace(diagnostic.Prompt[diagnostic.Prompt.Length - 1]),
                "choice prompt retained trailing whitespace");
        }

        Require(EquipmentChoiceGrammarCatalog.Require(2) == EquipmentChoiceGrammarCatalog.Choice2,
            "choice-2 grammar is not static");
        Require(EquipmentChoiceGrammarCatalog.Require(3) == EquipmentChoiceGrammarCatalog.Choice3,
            "choice-3 grammar is not static");
        Require(EquipmentChoiceResultParser.TryParse(
                " { \"selectedIndex\" : 0 } ",
                2,
                out int zero)
            && zero == 0,
            "whitespace-tolerant JSON choice failed");
        Require(EquipmentChoiceResultParser.TryParse(
                "\n{\"selectedIndex\":2}\r\n",
                3,
                out int two)
            && two == 2,
            "leading-newline JSON choice failed");
        Require(!EquipmentChoiceResultParser.TryParse("{\"selectedIndex\":3}", 3, out _),
            "out-of-range choice passed");
        Require(!EquipmentChoiceResultParser.TryParse("0", 3, out _),
            "legacy raw-digit choice passed");
        Require(!EquipmentChoiceResultParser.TryParse("{\"choice\":0}", 3, out _),
            "wrong-key JSON choice passed");
        Require(!EquipmentChoiceResultParser.TryParse(
                "{\"selectedIndex\":0,\"confidence\":1}",
                3,
                out _),
            "extra EquipmentChoice key passed");
        Require(!EquipmentChoiceResultParser.TryParse(
                "{\"selectedIndex\":0,\"selectedIndex\":1}",
                3,
                out _),
            "duplicate EquipmentChoice key passed");
        Require(!EquipmentChoiceResultParser.TryParse("{\"selectedIndex\":\"0\"}", 3, out _),
            "string EquipmentChoice index passed");
        Require(!EquipmentChoiceResultParser.TryParse(" ", 3, out _),
            "whitespace-only choice passed");

        Require(NarrativeExactKeyContract.TryValidateProfileResponse(
                "Persona",
                "{\"personaName\":\"신중한 손님\",\"flavorText\":\"조용한 방을 선호한다.\"}",
                out _,
                out string personaError),
            "valid Persona exact-key response failed: " + personaError);
        Require(!NarrativeExactKeyContract.TryValidateProfileResponse(
                "Persona",
                "{\"personaName\":\"신중한 손님\",\"flavorText\":\"조용하다.\",\"selfCareMultiplier\":1.2}",
                out _,
                out _),
            "Persona accepted a model-owned mechanical multiplier.");
        Require(!NarrativeExactKeyContract.TryValidateProfileResponse(
                "Persona",
                "{\"personaName\":\"신중한 손님\",\"flavorText\":\"조용하다.\",\"facilityTags\":[\"quiet\"]}",
                out _,
                out _),
            "Persona accepted a model-owned facility tag.");
    }

    private static void VerifyContextAwareScheduling()
    {
        PrefixAffinityKey affinityA = new PrefixAffinityKey("schema", "A", "facts-a", 1, 1);
        PrefixAffinityKey affinityB = new PrefixAffinityKey("schema", "B", "facts-b", 1, 1);
        SchedulerFixture coalesced = new SchedulerFixture(
            1, 0f, affinityA, persistent: true, urgent: false, expiresAt: float.PositiveInfinity);
        Require(!ContextAwareLlmScheduler.CanDispatch(coalesced, 0.05f, 1),
            "persistent request skipped the general coalescing window");
        Require(ContextAwareLlmScheduler.CanDispatch(coalesced, 0.08f, 1),
            "coalescing window did not release a persistent request");
        List<SchedulerFixture> queue = new List<SchedulerFixture>
        {
            new SchedulerFixture(10, 0f, affinityB, persistent: false, urgent: false, expiresAt: 20f),
            new SchedulerFixture(10, 0.01f, affinityA, persistent: false, urgent: false, expiresAt: 20f),
            new SchedulerFixture(1, 0.02f, affinityB, persistent: true, urgent: false, expiresAt: float.PositiveInfinity)
        };
        int persistent = ContextAwareLlmScheduler.FindNext(queue, 0.1f, affinityA, 0);
        Require(persistent == 2, "persistent narrative did not outrank affinity");

        queue.RemoveAt(2);
        int affinity = ContextAwareLlmScheduler.FindNext(queue, 0.1f, affinityA, 0);
        Require(affinity == 1, "matching prefix affinity was not grouped");

        int burstLimited = ContextAwareLlmScheduler.FindNext(
            queue,
            0.7f,
            affinityA,
            ContextAwareLlmScheduler.MaximumAffinityBurst);
        Require(burstLimited == 0, "affinity burst cap did not release an aged request");

        queue.Add(new SchedulerFixture(
            0,
            0.69f,
            affinityB,
            persistent: false,
            urgent: true,
            expiresAt: 0.8f));
        int deadline = ContextAwareLlmScheduler.FindNext(queue, 0.7f, affinityA, 0);
        Require(deadline == 2, "deadline-imminent request did not override affinity");
    }

    private static void VerifyTypedEquipmentEvidence()
    {
        UsageLedger ledger = new UsageLedger();
        UsageLedgerCompactor compactor = new UsageLedgerCompactor();
        compactor.Record(
            ledger,
            "combat:hit",
            12f,
            "character:archer",
            "equipment:test",
            new[] { "ranged" },
            historicalEvidenceKind: HistoricalEvidenceKind.RepeatedLongRangeHit,
            outcomeId: "hit",
            generation: 2,
            repeatCount: 4);
        compactor.Record(
            ledger,
            "combat:block",
            8f,
            "character:guardian",
            "equipment:test",
            new[] { "shield" },
            historicalEvidenceKind: HistoricalEvidenceKind.ProtectedOwner,
            outcomeId: "blocked",
            generation: 2);

        List<string> candidates = EquipmentEvolutionRules
            .BuildLegalHistoricalEffectCandidates(ledger);
        Require(candidates.Count is >= 2 and <= 3, "typed evidence did not create 2-3 legal candidates");
        Require(candidates[0] == "equipment:cadence", "strongest typed evidence did not rank first");

        CompactedHistorySegment segment = compactor.CloseGeneration(ledger, 2);
        Require(segment.historicalEvidence.Any(entry =>
                entry.kind == HistoricalEvidenceKind.RepeatedLongRangeHit
                && entry.occurrences == 4),
            "typed evidence was not compacted with repeat count");
        UsageLedgerEvent currentGenerationEvent = compactor.Record(
            ledger,
            "combat:after-reforge",
            3f,
            "character:archer",
            "equipment:test",
            new[] { "ranged" },
            historicalEvidenceKind: HistoricalEvidenceKind.RepeatedLongRangeHit,
            outcomeId: "hit",
            generation: 3);
        Require((segment.keyEvents ?? new List<UsageLedgerEvent>()).All(entry => !string.Equals(
                    entry.evidenceId,
                    currentGenerationEvent.evidenceId,
                    StringComparison.Ordinal))
                && (segment.keyEvents ?? new List<UsageLedgerEvent>())
                    .Concat(ledger.currentGenerationEvents ?? new List<UsageLedgerEvent>())
                    .Select(entry => entry.evidenceId)
                    .Distinct(StringComparer.Ordinal)
                    .Count()
                == (segment.keyEvents?.Count ?? 0)
                    + (ledger.currentGenerationEvents?.Count ?? 0),
            "Prior and current generations reused a ledger evidence ID.");
    }

    private static void VerifyEquipmentChoiceFailureAudits()
    {
        VerifyEquipmentChoiceFailure(
            EquipmentChoiceStubMode.RuntimeUnavailable,
            EquipmentChoiceFailureKind.RuntimeUnavailable);
        VerifyEquipmentChoiceFailure(
            EquipmentChoiceStubMode.RequestRejected,
            EquipmentChoiceFailureKind.RequestRejected);
        VerifyEquipmentChoiceFailure(
            EquipmentChoiceStubMode.AsyncInvalid,
            EquipmentChoiceFailureKind.AsyncResultInvalid);
        VerifyEquipmentChoiceFailure(
            EquipmentChoiceStubMode.OutOfRange,
            EquipmentChoiceFailureKind.SelectedIndexOutOfRange);
    }

    private static void VerifyEquipmentChoiceFailure(
        EquipmentChoiceStubMode mode,
        EquipmentChoiceFailureKind expectedFailure)
    {
        using EquipmentChoiceParticipantFixture participant =
            new EquipmentChoiceParticipantFixture();
        IGameContentCatalog gameContent = new ResourceGameContentCatalog(
            new UnityGameContentRootLoader());
        WorldItemRepository repository = new WorldItemRepository(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        CombatEquipmentRuntime equipment = CombatEquipmentEditorTestFactory.Create(
            new ResourceCombatEquipmentCatalog(gameContent),
            repository,
            new CharacterCarryInventoryRegistry(),
            researchProvider: EditorAllResearchRuntimeProvider.Instance,
            materialCatalog: EmptyResourceEconomyContentCatalog.Instance,
            evolutionModules: EmptyEvolutionModuleRegistry.Instance,
            moduleCatalog: EmptyEquipmentModuleCatalog.Instance,
            itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance);
        CombatEquipmentInstance instance = equipment.CreateInstance(
            "weapon:shortbow",
            CombatEquipmentQuality.Normal);
        Require(instance != null, "Could not create equipment choice test instance.");

        string nodeId = "equipment-choice-node:" + mode;
        List<string> candidates = new List<string>
        {
            "equipment:cadence",
            "equipment:durability"
        };
        EvolutionNode seededNode = new EvolutionNode
        {
            nodeId = nodeId,
            effectId = string.Empty,
            historical = true,
            mechanicallyUnlocked = false,
            narrativeReady = false,
            legalCandidateEffectIds = new List<string>(candidates),
            selectedCandidateIndex = -1
        };
        UsageLedger requestLedger = new UsageLedger();
        new UsageLedgerCompactor().Record(
            requestLedger,
            "combat:hit",
            1f,
            participant.ActorId,
            instance.instanceId,
            new[] { "fallback-audit" },
            historicalEvidenceKind: HistoricalEvidenceKind.RepeatedLongRangeHit,
            outcomeId: "hit");
        EvolutionNarrativeRequestSnapshot frozenRequest =
            EvolutionNarrativeRequestFactory.Create(
                EvolutionNarrativeTargetKind.Equipment,
                instance.instanceId,
                seededNode,
                "history:" + mode,
                requestLedger,
                effectBudget: 1);
        string requestKey = frozenRequest.requestKey;
        EquipmentEvolutionState state = new EquipmentEvolutionState
        {
            evolutionNodes = new List<EvolutionNode>
            {
                seededNode
            },
            narrativeRequests = new List<EvolutionNarrativeRequestSnapshot>
            {
                frozenRequest
            }
        };
        Require(equipment.TryUpdateEvolutionState(instance.instanceId, state),
            "Could not seed an equipment choice request.");

        ILocalLlmRuntimeProvider provider = mode == EquipmentChoiceStubMode.RuntimeUnavailable
            ? new MissingEquipmentChoiceRuntimeProvider()
            : new FixedEquipmentChoiceRuntimeProvider(new EquipmentChoiceRuntimeStub(mode));
        EvolutionHistoryNarrativeRuntime runtime = new EvolutionHistoryNarrativeRuntime(
            participant.World,
            equipment,
            new FacilityEvolutionStateComponentFactory(),
            provider,
            CharacterAiEditorTestDependencies.UiClock);
        runtime.Tick();

        Require(equipment.TryGetInstance(instance.instanceId, out CombatEquipmentInstance updated),
            "Equipment choice test instance disappeared.");
        EvolutionNode node = updated.evolution.evolutionNodes.Single(candidate =>
            string.Equals(candidate.nodeId, nodeId, StringComparison.Ordinal));
        Require(node.selectedCandidateIndex == -1
                && string.IsNullOrEmpty(node.effectId)
                && !node.mechanicallyUnlocked
                && !node.equipmentChoiceAuditRecorded
                && !node.equipmentChoiceFallbackUsed,
            $"{mode} mutated the equipment node after a failed legacy choice.");
        bool auditFound = runtime.TryGetEquipmentChoiceAudit(
            requestKey,
            out NarrativeInferenceAuditRecord audit);
        Require(auditFound
                && !audit.Succeeded
                && !audit.FallbackUsed
                && audit.ValidationError.StartsWith(expectedFailure.ToString(), StringComparison.Ordinal)
                && string.IsNullOrEmpty(audit.FallbackReason)
                && string.IsNullOrEmpty(audit.SelectedId)
                && audit.SelectedIndex == -1,
            $"{mode} did not publish a matching failed-choice audit record: "
            + $"found={auditFound}, succeeded={audit.Succeeded}, fallback={audit.FallbackUsed}, "
            + $"error='{audit.ValidationError}', fallbackReason='{audit.FallbackReason}', "
            + $"selectedId='{audit.SelectedId}', selectedIndex={audit.SelectedIndex}.");
    }

    private static void VerifyMechanicalNarrativeSeparation()
    {
        EvolutionNode node = new EvolutionNode
        {
            historical = true,
            mechanicallyUnlocked = true,
            narrativeReady = false,
            uiVisible = true,
            playerVisible = true,
            effectId = "equipment:durability",
            displayName = "버텨 낸 흔적 1단계"
        };
        Require(node.mechanicallyUnlocked && !node.narrativeReady && node.uiVisible,
            "historical mechanics are still gated by narrative readiness");
        EvolutionNode clone = node.Clone();
        Require(clone.mechanicallyUnlocked && !clone.narrativeReady && clone.uiVisible,
            "V25 authority split did not survive cloning");
    }

    private static void VerifyMultiPerspectiveIdentity()
    {
        NarrativeMultiPerspectiveRequest request = new NarrativeMultiPerspectiveRequest
        {
            sharedFactPacket = "event facts",
            viewpoints = new List<NarrativeViewpointRequest>
            {
                new NarrativeViewpointRequest
                {
                    eventId = "event:test",
                    viewpointCharacterId = "character:a",
                    knowledgeSnapshotHash = "knowledge:a",
                    knowledgeSnapshotVersion = 3,
                    cultureStyleId = "culture:orc",
                    modelVersion = "v25"
                },
                new NarrativeViewpointRequest
                {
                    eventId = "event:test",
                    viewpointCharacterId = "character:b",
                    knowledgeSnapshotHash = "knowledge:b",
                    knowledgeSnapshotVersion = 3,
                    cultureStyleId = "culture:orc",
                    modelVersion = "v25"
                }
            }
        };
        Require(request.TryValidate(out string error), error);
        Require(LlmStaticSchemaCatalog.Require("MultiPerspective").PersistentNarrative,
            "multi-perspective schema must be persistent");

        NarrativeMultiPerspectiveOutput output = new NarrativeMultiPerspectiveOutput
        {
            eventId = "event:test",
            perspectives = new List<NarrativePerspectiveOutput>
            {
                new NarrativePerspectiveOutput
                    { viewpointCharacterId = "character:a", line = "첫 시점" },
                new NarrativePerspectiveOutput
                    { viewpointCharacterId = "character:b", line = "둘째 시점" }
            }
        };
        Require(output.Matches(request), "viewpoint outputs were not bound to character identities");
        output.perspectives[1].viewpointCharacterId = "character:a";
        Require(!output.Matches(request), "duplicate viewpoint character was accepted");
    }

    private static void VerifyMissingHostFailsClosed()
    {
        string impossibleRoot = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "DungeonStoryV25Missing-" + Guid.NewGuid().ToString("N"));
        bool started = DungeonStoryLlmHostProcess.TryStart(
            impossibleRoot,
            out DungeonStoryLlmHostProcess host,
            out string error);
        host?.Dispose();
        Require(!started && host == null, "missing host artifacts did not fail closed");
        Require(error.IndexOf("manifest", StringComparison.OrdinalIgnoreCase) >= 0,
            "missing host diagnostic did not identify the manifest boundary");
    }

    private static void VerifyCorruptModelFailsClosed()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "DungeonStoryV25Corrupt-" + Guid.NewGuid().ToString("N"));
        string bundle = Path.Combine(root, "DungeonStoryLlm");
        Directory.CreateDirectory(bundle);
        string hostPath = Path.Combine(bundle, "DungeonStoryLlmHost.exe");
        string modelPath = Path.Combine(bundle, "DungeonStory-Qwen3-1.7B-Q4_K_M.gguf");
        File.WriteAllBytes(hostPath, new byte[] { 1, 2, 3, 4 });
        File.WriteAllBytes(modelPath, Encoding.ASCII.GetBytes("GGUF-corrupt"));
        DungeonStoryLlmHostManifest manifest = new DungeonStoryLlmHostManifest
        {
            hostKind = "LlamaCppServer",
            hostWindows = Path.GetFileName(hostPath),
            hostWindowsSha256 = Sha256(hostPath),
            modelFile = Path.GetFileName(modelPath),
            modelSha256 = new string('0', 64),
            supportFiles = Array.Empty<DungeonStoryLlmHostSupportFile>()
        };
        File.WriteAllText(
            Path.Combine(bundle, "manifest.json"),
            JsonUtility.ToJson(manifest),
            Encoding.UTF8);
        try
        {
            bool started = DungeonStoryLlmHostProcess.TryStart(
                root,
                out DungeonStoryLlmHostProcess host,
                out string error);
            host?.Dispose();
            Require(!started && host == null, "corrupt model unexpectedly started");
            Require(error.IndexOf("model", StringComparison.OrdinalIgnoreCase) >= 0,
                "corrupt model diagnostic did not identify the model boundary");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void VerifyBundledLlamaCppBackendContract()
    {
        DungeonStoryHostStructuredChatBackend backend =
            new DungeonStoryHostStructuredChatBackend(() => "test-token");
        Require(
            backend.ResolveEndpoint("http://127.0.0.1:8080")
                .EndsWith("/v1/chat/completions", StringComparison.Ordinal),
            "bundled backend did not resolve llama.cpp chat completions");
        LlmStaticSchemaDefinition schema = LlmStaticSchemaCatalog.Require("CharacterRecord");
        using UnityEngine.Networking.UnityWebRequest request = backend.BuildRequest(
            "http://127.0.0.1:8080",
            "DungeonStory-Qwen3-1.7B-Q4_K_M",
            LocalLlmRequestProfiles.CharacterRecord,
            schema,
            "test prompt");
        string payload = Encoding.UTF8.GetString(request.uploadHandler.data);
        Require(payload.IndexOf("\"response_format\":{\"type\":\"json_schema\"",
                    StringComparison.Ordinal) >= 0,
            "bundled backend did not pass the static JSON schema to llama.cpp");
        Require(payload.IndexOf("\"enable_thinking\":false", StringComparison.Ordinal) >= 0,
            "bundled backend did not disable thinking in the Qwen chat template");
        Require(string.Equals(
                request.GetRequestHeader("Authorization"),
                "Bearer test-token",
                StringComparison.Ordinal),
            "bundled backend omitted loopback authentication");
    }

    private static void VerifyCanonicalCatalogJsonParity()
    {
        NarrativeMechanicCatalogCanonicalJsonObject fixture =
            NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "z",
                    NarrativeMechanicCatalogCanonicalJson.Integer(1)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "\uE000",
                    NarrativeMechanicCatalogCanonicalJson.String("bmp")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "😀",
                    NarrativeMechanicCatalogCanonicalJson.String("supplementary")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "한글",
                    NarrativeMechanicCatalogCanonicalJson.String("값")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "nested",
                    NarrativeMechanicCatalogCanonicalJson.Object(
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "b",
                            NarrativeMechanicCatalogCanonicalJson.Boolean(true)),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "a",
                            NarrativeMechanicCatalogCanonicalJson.String("line\n\"")))));
        byte[] bytes = fixture.ToCanonicalUtf8();
        string canonical = Encoding.UTF8.GetString(bytes);
        const string PythonCanonical =
            "{\"nested\":{\"a\":\"line\\n\\\"\",\"b\":true},\"z\":1,\"한글\":\"값\",\"\":\"bmp\",\"😀\":\"supplementary\"}";
        Require(string.Equals(canonical, PythonCanonical, StringComparison.Ordinal),
            "C# canonical JSON bytes differ from Python json.dumps(sort_keys=True, ensure_ascii=False, separators=(',', ':')).");
        Require(string.Equals(
                NarrativeMechanicCatalogCanonicalJson.Sha256Prefixed(bytes),
                "sha256:bddd83f5e2ea2f19530c142ef3db9842832fdbd2a8f077b409ade64161febf27",
                StringComparison.Ordinal),
            "C# canonical JSON hash differs from the pinned Python fixture hash.");
        Require(bytes.Length >= 3
                && !(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF),
            "Canonical JSON unexpectedly contains a UTF-8 BOM.");

        bool rejectedMalformedUtf16 = false;
        try
        {
            NarrativeMechanicCatalogCanonicalJson.String("\uD800").ToCanonicalUtf8();
        }
        catch (ArgumentException)
        {
            rejectedMalformedUtf16 = true;
        }
        Require(rejectedMalformedUtf16,
            "Canonical JSON silently replaced an unpaired UTF-16 surrogate.");
    }

    private static void VerifyCharacterSkillPublicSemanticsContract()
    {
        NarrativeMechanicCatalogSnapshot catalogSnapshot =
            new NarrativeMechanicCatalogUnityAssetSource().CaptureSnapshot();
        NarrativeMechanicCatalogCanonicalJsonObject coverage = RequireCanonicalObject(
            catalogSnapshot.CharacterSkill,
            "semanticsCoverage");
        Require(coverage.Properties.Count == 6
                && RequireCanonicalProperty<NarrativeMechanicCatalogCanonicalJsonInt64>(
                    coverage,
                    "schemaVersion").Value == CharacterSkillCombinationSemanticsFactory.SchemaVersion
                && string.Equals(
                    RequireCanonicalString(coverage, "coveragePolicy"),
                    CharacterSkillCombinationSemanticsFactory.CatalogCoveragePolicy,
                    StringComparison.Ordinal),
            "The base CharacterSkill catalog semantics coverage contract is missing or changed.");
        NarrativeMechanicCatalogCanonicalJsonArray coverageEntries =
            RequireCanonicalArray(coverage, "entries");
        Require(RequireCanonicalProperty<NarrativeMechanicCatalogCanonicalJsonInt64>(
                    coverage,
                    "entryCount").Value == coverageEntries.Values.Count
                && RequireCanonicalProperty<NarrativeMechanicCatalogCanonicalJsonInt64>(
                    coverage,
                    "moduleCount").Value == 24
                && RequireCanonicalProperty<NarrativeMechanicCatalogCanonicalJsonInt64>(
                    coverage,
                    "variantCount").Value == 50,
            "The base CharacterSkill catalog does not cover all 24 modules and 50 variants.");

        HashSet<string> catalogPairs = new HashSet<string>(StringComparer.Ordinal);
        foreach (NarrativeMechanicCatalogCanonicalJsonObject module in
                 RequireCanonicalArray(catalogSnapshot.CharacterSkill, "modules").Values
                     .Cast<NarrativeMechanicCatalogCanonicalJsonObject>())
        {
            string moduleId = RequireCanonicalString(module, "moduleId");
            foreach (NarrativeMechanicCatalogCanonicalJsonObject variant in
                     RequireCanonicalArray(module, "variants").Values
                         .Cast<NarrativeMechanicCatalogCanonicalJsonObject>())
            {
                Require(catalogPairs.Add(moduleId + "|" + RequireCanonicalString(
                        variant,
                        "variantId")),
                    $"The base CharacterSkill catalog duplicated module '{moduleId}' variants.");
            }
        }

        HashSet<string> coveredPairs = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> coveredScopes = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> contextIds = new HashSet<string>(StringComparer.Ordinal);
        List<string> coverageOrder = new List<string>();
        foreach (NarrativeMechanicCatalogCanonicalJsonObject entry in coverageEntries.Values
                     .Cast<NarrativeMechanicCatalogCanonicalJsonObject>())
        {
            Require(entry.Properties.Count == 9
                    && string.Equals(
                        RequireCanonicalString(entry, "applicability"),
                        CharacterSkillCombinationSemanticsFactory.CatalogEntryApplicability,
                        StringComparison.Ordinal),
                "A CharacterSkill catalog semantics row lost its representative-context contract.");
            string contextId = RequireCanonicalString(entry, "contextId");
            string moduleId = RequireCanonicalString(entry, "moduleId");
            string variantId = RequireCanonicalString(entry, "variantId");
            Require(contextIds.Add(contextId),
                $"The CharacterSkill catalog duplicated semantics context '{contextId}'.");
            coveredPairs.Add(moduleId + "|" + variantId);
            coverageOrder.Add(moduleId + "|" + variantId + "|" + contextId);

            NarrativeMechanicCatalogCanonicalJsonObject semantics =
                RequireCanonicalObject(entry, "semantics");
            ValidatePublicSemanticsShape("catalog:" + contextId, semantics);
            foreach (NarrativeMechanicCatalogCanonicalJsonObject semanticModule in
                     RequireCanonicalArray(semantics, "modules").Values
                         .Cast<NarrativeMechanicCatalogCanonicalJsonObject>())
            {
                Require(string.Equals(
                            RequireCanonicalString(semanticModule, "moduleId"),
                            moduleId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            RequireCanonicalString(semanticModule, "variantId"),
                            variantId,
                            StringComparison.Ordinal),
                    $"Catalog semantics context '{contextId}' describes a different module/variant.");
                coveredScopes.Add(RequireCanonicalString(semanticModule, "executionScope"));
            }
        }
        Require(catalogPairs.SetEquals(coveredPairs),
            "CharacterSkill catalog semantics coverage differs from the authored module/variant set.");
        Require(coveredScopes.SetEquals(
                CharacterSkillCombinationSemanticsFactory.ExecutionScopeTokens),
            "CharacterSkill catalog semantics coverage omits a runtime execution scope.");
        Require(coverageOrder.SequenceEqual(
                coverageOrder.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal),
            "CharacterSkill catalog semantics contexts are not deterministically ordered.");
        foreach (string requiredLarge in new[]
                 {
                     "work_speed|large", "output|large", "cleaning|large", "repair|large",
                     "stock|large", "research|large", "needs|large", "mood|large",
                     "relationship|large", "revenue|large"
                 })
        {
            Require(coveredPairs.Contains(requiredLarge),
                $"The catalog-only CharacterSkill contract omitted '{requiredLarge}'.");
        }

        NarrativeMechanicScenarioSourceCapture capture =
            new NarrativeMechanicScenarioCharacterSkillSource().CaptureScenarios();
        Require(capture.PositiveScenarios.Count == 20,
            $"CharacterSkill semantics coverage expected 20 positive scenarios, found "
            + $"{capture.PositiveScenarios.Count}.");

        const string PreservedWorkCompletedRuleId =
            "skill-rule:sha256:62c34e081c49f89c696f15087671c2872ac4ceec2c1e624a4f52d492a49e5d9a";
        const string PreservedWorkCompletedCombinationId =
            "skill-combination:sha256:b34f39adcf06ae8247058a861b39edd4c4bd595d50ed2d4c910e76cae94519cd";
        NarrativeMechanicScenario preservedScenario = capture.PositiveScenarios.Single(
            scenario => string.Equals(
                scenario.ScenarioId,
                "character-skill-authority-10-passive-l01-facility-complete",
                StringComparison.Ordinal));
        NarrativeMechanicCatalogCanonicalJsonObject preservedRequest =
            RequireCanonicalObject(
                preservedScenario.ToCanonicalJson(
                    "sha256:character-skill-preserved-id-catalog",
                    "sha256:character-skill-preserved-id-input"),
                "request");
        NarrativeMechanicCatalogCanonicalJsonObject preservedRule =
            RequireCanonicalArray(preservedRequest, "rules").Values
                .Cast<NarrativeMechanicCatalogCanonicalJsonObject>()
                .Single();
        Require(string.Equals(
                    RequireCanonicalString(preservedRule, "ruleId"),
                    PreservedWorkCompletedRuleId,
                    StringComparison.Ordinal)
                && RequireCanonicalArray(preservedRule, "combinationOptions").Values
                    .Cast<NarrativeMechanicCatalogCanonicalJsonObject>()
                    .Any(option => string.Equals(
                        RequireCanonicalString(option, "combinationId"),
                        PreservedWorkCompletedCombinationId,
                        StringComparison.Ordinal)),
            "The production WorkCompleted draft changed a still-valid v17 rule or combination ID.");

        int inspectedCombinationCount = 0;
        foreach (NarrativeMechanicScenario scenario in capture.PositiveScenarios)
        {
            NarrativeMechanicCatalogCanonicalJsonObject root = scenario.ToCanonicalJson(
                "sha256:character-skill-semantics-test-catalog",
                "sha256:character-skill-semantics-test-input");
            NarrativeMechanicCatalogCanonicalJsonObject request =
                RequireCanonicalObject(root, "request");
            string candidatePacket = RequireCanonicalString(request, "candidatePacket");
            string prompt = RequireCanonicalString(request, "prompt");
            Require(!string.IsNullOrWhiteSpace(candidatePacket)
                    && prompt.Contains(candidatePacket, StringComparison.Ordinal),
                $"Scenario '{scenario.ScenarioId}' final live prompt does not contain its exact "
                + "public candidate packet.");

            NarrativeMechanicCatalogCanonicalJsonArray fullRules =
                RequireCanonicalArray(root, "fullLegalCandidates");
            Dictionary<string, Dictionary<string, string>> fullSemanticsByRule =
                new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (NarrativeMechanicCatalogCanonicalJsonObject fullRule in fullRules.Values
                         .Cast<NarrativeMechanicCatalogCanonicalJsonObject>())
            {
                string ruleId = RequireCanonicalString(fullRule, "ruleId");
                Dictionary<string, string> byCombination =
                    new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (NarrativeMechanicCatalogCanonicalJsonObject option in
                         RequireCanonicalArray(fullRule, "options").Values
                             .Cast<NarrativeMechanicCatalogCanonicalJsonObject>())
                {
                    string combinationId = RequireCanonicalString(option, "combinationId");
                    NarrativeMechanicCatalogCanonicalJsonObject semantics =
                        RequireCanonicalObject(option, "semantics");
                    ValidatePublicSemanticsShape(scenario.ScenarioId, semantics);
                    Require(byCombination.TryAdd(
                            combinationId,
                            semantics.ToCanonicalString()),
                        $"Scenario '{scenario.ScenarioId}' duplicated combination "
                        + $"'{combinationId}' in fullLegalCandidates.");
                }
                Require(fullSemanticsByRule.TryAdd(ruleId, byCombination),
                    $"Scenario '{scenario.ScenarioId}' duplicated full rule '{ruleId}'.");
            }

            foreach (NarrativeMechanicCatalogCanonicalJsonObject rule in
                     RequireCanonicalArray(request, "rules").Values
                         .Cast<NarrativeMechanicCatalogCanonicalJsonObject>())
            {
                string ruleId = RequireCanonicalString(rule, "ruleId");
                Require(fullSemanticsByRule.TryGetValue(ruleId, out Dictionary<string, string> full),
                    $"Scenario '{scenario.ScenarioId}' request rule '{ruleId}' has no full export row.");
                foreach (NarrativeMechanicCatalogCanonicalJsonObject option in
                         RequireCanonicalArray(rule, "combinationOptions").Values
                             .Cast<NarrativeMechanicCatalogCanonicalJsonObject>())
                {
                    string combinationId = RequireCanonicalString(option, "combinationId");
                    NarrativeMechanicCatalogCanonicalJsonObject semantics =
                        RequireCanonicalObject(option, "semantics");
                    ValidatePublicSemanticsShape(scenario.ScenarioId, semantics);
                    foreach (NarrativeMechanicCatalogCanonicalJsonValue module in
                             RequireCanonicalArray(semantics, "modules").Values)
                    {
                        Require(candidatePacket.Contains(
                                "json=" + module.ToCanonicalString(),
                                StringComparison.Ordinal),
                            $"Scenario '{scenario.ScenarioId}' final live candidate packet "
                            + $"omitted the exported semantics for combination '{combinationId}'.");
                    }
                    Require(full.TryGetValue(combinationId, out string fullSemantics)
                            && string.Equals(
                                semantics.ToCanonicalString(),
                                fullSemantics,
                                StringComparison.Ordinal),
                        $"Scenario '{scenario.ScenarioId}' live request and export disagree for "
                        + $"combination '{combinationId}'.");
                    inspectedCombinationCount++;
                }
            }
        }
        Require(inspectedCombinationCount > 0,
            "CharacterSkill semantics coverage inspected no legal combinations.");

        NarrativeMechanicScenario affected = capture.PositiveScenarios.Single(scenario =>
            string.Equals(
                scenario.ScenarioId,
                "character-skill-authority-04-active-l05-exceptional-enemy",
                StringComparison.Ordinal));
        NarrativeMechanicCatalogCanonicalJsonObject affectedRequest = RequireCanonicalObject(
            affected.ToCanonicalJson(
                "sha256:character-skill-semantics-test-catalog",
                "sha256:character-skill-semantics-test-input"),
            "request");
        NarrativeMechanicCatalogCanonicalJsonArray affectedRules =
            RequireCanonicalArray(affectedRequest, "rules");
        Require(affectedRules.Values.Count >= 2,
            "The affected CharacterSkill scenario no longer has a second rule.");
        NarrativeMechanicCatalogCanonicalJsonArray unavoidableOptions =
            RequireCanonicalArray(
                (NarrativeMechanicCatalogCanonicalJsonObject)affectedRules.Values[1],
                "combinationOptions");
        Require(unavoidableOptions.Values.Count == 12,
            $"The affected second rule expected 12 legal options, found "
            + $"{unavoidableOptions.Values.Count}.");
        foreach (NarrativeMechanicCatalogCanonicalJsonObject option in unavoidableOptions.Values
                     .Cast<NarrativeMechanicCatalogCanonicalJsonObject>())
        {
            NarrativeMechanicCatalogCanonicalJsonObject semantics =
                RequireCanonicalObject(option, "semantics");
            NarrativeMechanicCatalogCanonicalJsonObject conditional =
                RequireCanonicalArray(semantics, "modules").Values
                    .Cast<NarrativeMechanicCatalogCanonicalJsonObject>()
                    .Single(module => string.Equals(
                        RequireCanonicalString(module, "moduleId"),
                        "conditional_amplify",
                        StringComparison.Ordinal));
            string[] terms = RequireCanonicalArray(conditional, "terms").Values
                .Cast<NarrativeMechanicCatalogCanonicalJsonString>()
                .Select(value => value.Value)
                .ToArray();
            Require(terms.Contains("health_ratio_subject=selected_target", StringComparer.Ordinal)
                    && terms.Contains("comparison=less_than_or_equal", StringComparer.Ordinal)
                    && terms.Any(term => term.StartsWith(
                        "health_ratio_threshold=",
                        StringComparison.Ordinal))
                    && terms.Any(term => term.StartsWith(
                        "separate_basic_damage_multiplier=",
                        StringComparison.Ordinal)),
                "An unavoidable conditional_amplify option omitted its subject, comparison, "
                + "threshold, or amplified effect.");
        }
    }

    private static void ValidatePublicSemanticsShape(
        string scenarioId,
        NarrativeMechanicCatalogCanonicalJsonObject semantics)
    {
        NarrativeMechanicCatalogCanonicalJsonInt64 schemaVersion =
            RequireCanonicalProperty<NarrativeMechanicCatalogCanonicalJsonInt64>(
                semantics,
                "schemaVersion");
        Require(schemaVersion.Value == CharacterSkillCombinationSemanticsFactory.SchemaVersion,
            $"Scenario '{scenarioId}' exported an unsupported CharacterSkill semantics version.");
        Require(!string.IsNullOrWhiteSpace(RequireCanonicalString(semantics, "combinationId"))
                && !string.IsNullOrWhiteSpace(RequireCanonicalString(semantics, "descriptionKo")),
            $"Scenario '{scenarioId}' exported blank CharacterSkill semantics identity or prose.");
        NarrativeMechanicCatalogCanonicalJsonArray modules =
            RequireCanonicalArray(semantics, "modules");
        Require(modules.Values.Count > 0,
            $"Scenario '{scenarioId}' exported CharacterSkill semantics without modules.");
        foreach (NarrativeMechanicCatalogCanonicalJsonObject module in modules.Values
                     .Cast<NarrativeMechanicCatalogCanonicalJsonObject>())
        {
            string scope = RequireCanonicalString(module, "executionScope");
            string effectKind = RequireCanonicalString(module, "effectKind");
            Require(CharacterSkillCombinationSemanticsFactory.ExecutionScopeTokens.Contains(
                        scope,
                        StringComparer.Ordinal)
                    && CharacterSkillCombinationSemanticsFactory.EffectKindTokens.Contains(
                        effectKind,
                        StringComparer.Ordinal)
                    && !string.IsNullOrWhiteSpace(RequireCanonicalString(module, "moduleId"))
                    && !string.IsNullOrWhiteSpace(RequireCanonicalString(module, "variantId"))
                    && !string.IsNullOrWhiteSpace(RequireCanonicalString(module, "descriptionKo")),
                $"Scenario '{scenarioId}' exported an incomplete or unknown module semantics row.");
            string[] terms = RequireCanonicalArray(module, "terms").Values
                .Cast<NarrativeMechanicCatalogCanonicalJsonString>()
                .Select(value => value.Value)
                .ToArray();
            Require(terms.Length > 0
                    && terms.SequenceEqual(
                        terms.OrderBy(value => value, StringComparer.Ordinal),
                        StringComparer.Ordinal)
                    && terms.Distinct(StringComparer.Ordinal).Count() == terms.Length
                    && terms.All(term => !string.IsNullOrWhiteSpace(term)
                        && term.IndexOf('=') > 0),
                $"Scenario '{scenarioId}' exported non-canonical CharacterSkill semantic terms.");
            string moduleId = RequireCanonicalString(module, "moduleId");
            string variantId = RequireCanonicalString(module, "variantId");
            if (string.Equals(moduleId, "conditional_amplify", StringComparison.Ordinal)
                && string.Equals(scope, "offense_battle", StringComparison.Ordinal))
            {
                string expectedThreshold = string.Equals(
                    variantId,
                    "critical",
                    StringComparison.Ordinal)
                        ? "0.25"
                        : "0.5";
                string expectedMultiplier = string.Equals(
                    variantId,
                    "critical",
                    StringComparison.Ordinal)
                        ? "0.7"
                        : "0.35";
                Require(terms.Contains(
                            "health_ratio_subject=selected_target",
                            StringComparer.Ordinal)
                        && terms.Contains(
                            "comparison=less_than_or_equal",
                            StringComparer.Ordinal)
                        && terms.Contains(
                            "health_ratio_threshold=" + expectedThreshold,
                            StringComparer.Ordinal)
                        && terms.Contains(
                            "amplified_effect=separate_basic_damage",
                            StringComparer.Ordinal)
                        && terms.Contains(
                            "separate_basic_damage_multiplier=" + expectedMultiplier,
                            StringComparer.Ordinal),
                    $"Scenario '{scenarioId}' exported runtime-mismatched {variantId} "
                    + "conditional amplification semantics.");
            }
            if (string.Equals(moduleId, "conditional_amplify", StringComparison.Ordinal)
                && string.Equals(scope, "defense_ultimate", StringComparison.Ordinal))
            {
                Require(terms.Contains("health_check=none", StringComparer.Ordinal)
                        && terms.Contains(
                            "contribution_is_unconditional=true",
                            StringComparer.Ordinal),
                    $"Scenario '{scenarioId}' falsely described defense-ultimate conditional "
                    + "amplification as health-gated.");
            }
        }
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject RequireCanonicalObject(
        NarrativeMechanicCatalogCanonicalJsonObject owner,
        string key) =>
        RequireCanonicalProperty<NarrativeMechanicCatalogCanonicalJsonObject>(owner, key);

    private static NarrativeMechanicCatalogCanonicalJsonArray RequireCanonicalArray(
        NarrativeMechanicCatalogCanonicalJsonObject owner,
        string key) =>
        RequireCanonicalProperty<NarrativeMechanicCatalogCanonicalJsonArray>(owner, key);

    private static string RequireCanonicalString(
        NarrativeMechanicCatalogCanonicalJsonObject owner,
        string key) =>
        RequireCanonicalProperty<NarrativeMechanicCatalogCanonicalJsonString>(owner, key).Value;

    private static T RequireCanonicalProperty<T>(
        NarrativeMechanicCatalogCanonicalJsonObject owner,
        string key)
        where T : NarrativeMechanicCatalogCanonicalJsonValue
    {
        NarrativeMechanicCatalogCanonicalJsonValue[] values = owner.Properties
            .Where(pair => string.Equals(pair.Key, key, StringComparison.Ordinal))
            .Select(pair => pair.Value)
            .ToArray();
        Require(values.Length == 1 && values[0] is T,
            $"Canonical JSON property '{key}' was missing or had the wrong type.");
        return (T)values[0];
    }

    private static void VerifyIndependentCatalogExportBytes()
    {
        NarrativeMechanicCatalogByteComparison comparison =
            NarrativeMechanicCatalogExporter.CompareIndependentExports(
                () => new NarrativeMechanicCatalogUnityAssetSource());
        comparison.RequireByteIdentical();
        Require(comparison.FirstBytes.Length > 0,
            "Independent catalog comparison produced an empty catalog.");
        Require(string.Equals(
                comparison.FirstInputDigest,
                comparison.SecondInputDigest,
                StringComparison.Ordinal),
            "Independent catalog captures observed different provenance digests.");
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using SHA256 sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(stream))
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class SchedulerFixture : IContextAwareLlmRequest
    {
        public SchedulerFixture(
            int priority,
            float enqueuedAt,
            PrefixAffinityKey key,
            bool persistent,
            bool urgent,
            float expiresAt)
        {
            Priority = priority;
            EnqueuedAt = enqueuedAt;
            Scheduling = new NarrativeSchedulingMetadata
            {
                AffinityKey = key,
                Persistent = persistent,
                Urgent = urgent,
                ExpiresAt = expiresAt
            };
        }

        public int Priority { get; }
        public float EnqueuedAt { get; }
        public NarrativeSchedulingMetadata Scheduling { get; }
    }

    private sealed class StableCatalogSnapshotProvider :
        INarrativeMechanicCatalogSnapshotProvider
    {
        public NarrativeMechanicCatalogSnapshot CaptureSnapshot()
        {
            NarrativeMechanicCatalogCanonicalJsonArray empty =
                NarrativeMechanicCatalogCanonicalJson.Array();
            return new NarrativeMechanicCatalogSnapshot(
                "v25-test-catalog",
                NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property("modules", empty),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "rarityBudgets",
                        NarrativeMechanicCatalogCanonicalJson.Array())),
                NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "recipes",
                        NarrativeMechanicCatalogCanonicalJson.Array())),
                NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "effects",
                        NarrativeMechanicCatalogCanonicalJson.Array())),
                NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "eraseDisplayName",
                        NarrativeMechanicCatalogCanonicalJson.String("망각의 인장")),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "eraseItemId",
                        NarrativeMechanicCatalogCanonicalJson.String("item:memory-erasure-seal")),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "gates",
                        NarrativeMechanicCatalogCanonicalJson.Array(
                            NarrativeMechanicCatalogCanonicalJson.Integer(3),
                            NarrativeMechanicCatalogCanonicalJson.Integer(8),
                            NarrativeMechanicCatalogCanonicalJson.Integer(20))),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "implementationStatus",
                        NarrativeMechanicCatalogCanonicalJson.String("fixture")),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "maxActive",
                        NarrativeMechanicCatalogCanonicalJson.Integer(3)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "modules",
                        NarrativeMechanicCatalogCanonicalJson.Array())),
                new[]
                {
                    "Assets/Scripts/Services/Character/AI/Editor/"
                    + "V25NarrativeInferenceDebugScenarios.cs"
                },
                new NarrativeMechanicCatalogTrainingPolicy(Array.Empty<string>()),
                BuildFixturePacketParity(),
                BuildFixtureScenarioBundle());
        }

        private static NarrativeMechanicScenarioBundle BuildFixtureScenarioBundle()
        {
            return NarrativeMechanicScenarioCatalog.Capture(
                NarrativeMechanicScenarioProfiles.ExpectedPositiveCounts
                    .Select(pair => (INarrativeMechanicScenarioSource)
                        new StableScenarioSource(pair.Key, pair.Value)));
        }

        private static NarrativeMechanicCatalogPacketParity BuildFixturePacketParity()
        {
            string[] profileIds =
            {
                "CharacterSkill",
                "FacilityEvolution",
                "EquipmentChoiceLegacyV2",
                "EvolutionHistory",
                "AcquiredTrait",
                "Persona"
            };
            List<NarrativeMechanicCatalogCanonicalJsonObject> cases =
                new List<NarrativeMechanicCatalogCanonicalJsonObject>();
            foreach (string profileId in profileIds)
            {
                cases.Add(BuildFixtureParityCase(profileId, accepted: false));
                cases.Add(BuildFixtureParityCase(profileId, accepted: true));
            }
            return new NarrativeMechanicCatalogPacketParity(
                cases,
                Array.Empty<NarrativeMechanicCatalogParityBlocker>());
        }

        private static NarrativeMechanicCatalogCanonicalJsonObject BuildFixtureParityCase(
            string profileId,
            bool accepted)
        {
            return NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "caseId",
                    NarrativeMechanicCatalogCanonicalJson.String(
                        $"fixture:{profileId}:{(accepted ? "accepted" : "rejected")}")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "expected",
                    NarrativeMechanicCatalogCanonicalJson.Object(
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "accepted",
                            NarrativeMechanicCatalogCanonicalJson.Boolean(accepted)))),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "profileId",
                    NarrativeMechanicCatalogCanonicalJson.String(profileId)));
        }

        private sealed class StableScenarioSource : INarrativeMechanicScenarioSource
        {
            private readonly int positiveCount;

            public StableScenarioSource(string profileId, int positiveCount)
            {
                ProfileId = profileId;
                this.positiveCount = positiveCount;
            }

            public string ProfileId { get; }

            public NarrativeMechanicScenarioSourceCapture CaptureScenarios()
            {
                List<NarrativeMechanicScenario> positives =
                    new List<NarrativeMechanicScenario>(positiveCount);
                for (int ordinal = 0; ordinal < positiveCount; ordinal++)
                {
                    positives.Add(BuildScenario(ordinal, accepted: true));
                }

                return new NarrativeMechanicScenarioSourceCapture(
                    ProfileId,
                    positives,
                    new[] { BuildScenario(-1, accepted: false) },
                    new[]
                    {
                        "Assets/Scripts/Services/Character/AI/Editor/"
                        + "V25NarrativeInferenceDebugScenarios.cs"
                    });
            }

            private NarrativeMechanicScenario BuildScenario(int ordinal, bool accepted)
            {
                string suffix = ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
                KeyValuePair<string, string> axis = ProfileId switch
                {
                    NarrativeMechanicScenarioProfiles.CharacterSkill =>
                        new KeyValuePair<string, string>(
                            "kind",
                            new[] { "Active", "Passive", "Ultimate" }[
                                Math.Max(0, ordinal) % 3]),
                    NarrativeMechanicScenarioProfiles.AcquiredTrait =>
                        new KeyValuePair<string, string>(
                            "manifestationMilestone",
                            new[] { "3", "8", "20" }[Math.Max(0, ordinal) % 3]),
                    _ => new KeyValuePair<string, string>("ordinal", suffix)
                };
                NarrativeMechanicCatalogCanonicalJsonObject identity =
                    NarrativeMechanicCatalogCanonicalJson.Object(
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "ordinal", NarrativeMechanicCatalogCanonicalJson.Integer(ordinal)),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "profileId", NarrativeMechanicCatalogCanonicalJson.String(ProfileId)));
                return new NarrativeMechanicScenario(
                    $"fixture:{ProfileId}:{(accepted ? "positive" : "negative")}:{suffix}",
                    ProfileId,
                    identity,
                    NarrativeMechanicCatalogCanonicalJson.Array(
                        NarrativeMechanicCatalogCanonicalJson.Object(
                            NarrativeMechanicCatalogCanonicalJson.Property(
                                "candidateId",
                                NarrativeMechanicCatalogCanonicalJson.String(
                                    $"fixture-candidate:{ProfileId}:{suffix}")))),
                    NarrativeMechanicCatalogCanonicalJson.Array(
                        NarrativeMechanicCatalogCanonicalJson.Object(
                            NarrativeMechanicCatalogCanonicalJson.Property(
                                "factId",
                                NarrativeMechanicCatalogCanonicalJson.String(
                                    $"fixture-fact:{ProfileId}:{suffix}")))),
                    new[] { $"fixture-effect:{ProfileId}:{suffix}" },
                    identity,
                    identity,
                    "StableScenarioSource.BuildScenario",
                    "StableScenarioSource.BuildScenario",
                    accepted,
                    accepted ? string.Empty : "fixture validator rejection",
                    new[] { axis });
            }
        }
    }

    private enum EquipmentChoiceStubMode
    {
        RuntimeUnavailable,
        RequestRejected,
        AsyncInvalid,
        OutOfRange
    }

    private sealed class EquipmentChoiceParticipantFixture : IDisposable
    {
        private readonly CharacterSO data;

        public EquipmentChoiceParticipantFixture()
        {
            Root = CharacterAiPlanDebugFixtures.CreateActorObject(
                "EquipmentChoiceFailureParticipant");
            try
            {
                data = CharacterAiPlanDebugFixtures.CreateCharacterData(
                    CharacterType.NPC,
                    "장비 선택 감사자",
                    "human");
                Actor = Root.GetComponent<CharacterActor>();
                if (Actor == null)
                    throw new InvalidOperationException(
                        "Equipment choice failure fixture has no CharacterActor.");
                Actor.EnsureRuntimeState();
                Actor.Identity.SetPersistentId(
                    "character:equipment-choice-auditor");
                Actor.Initialization(data);
                World = new EquipmentChoiceBuildingWorldQuery(Actor);
            }
            catch
            {
                if (data != null) UnityEngine.Object.DestroyImmediate(data);
                UnityEngine.Object.DestroyImmediate(Root);
                throw;
            }
        }

        public GameObject Root { get; }
        public CharacterActor Actor { get; }
        public string ActorId => Actor.Identity.PersistentId;
        public IBuildingWorldQuery World { get; }

        public void Dispose()
        {
            if (Root != null) UnityEngine.Object.DestroyImmediate(Root);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private sealed class EquipmentChoiceBuildingWorldQuery :
        IBuildingWorldQuery,
        ICharacterWorldQuery,
        ICharacterLifetimeQuery
    {
        private static readonly IReadOnlyList<BuildableObject> EmptyBuildings =
            Array.Empty<BuildableObject>();
        private readonly CharacterActor[] characters;

        public EquipmentChoiceBuildingWorldQuery(CharacterActor actor)
        {
            characters = new[]
            {
                actor ?? throw new ArgumentNullException(nameof(actor))
            };
        }

        public int BuildingVersion => 0;
        public int CharacterVersion => 0;
        public int LifetimeCharacterVersion => 0;
        public IReadOnlyList<BuildableObject> Buildings => EmptyBuildings;
        public IReadOnlyList<CharacterActor> Characters => characters;
        public IReadOnlyList<CharacterActor> AllCharacters => characters;
    }

    private sealed class MissingEquipmentChoiceRuntimeProvider : ILocalLlmRuntimeProvider
    {
        public bool TryGetRuntime(out ILocalLlmRuntime runtime)
        {
            runtime = null;
            return false;
        }

        public ILocalLlmRuntime GetRequiredRuntime()
        {
            throw new InvalidOperationException("Equipment choice fixture has no runtime.");
        }
    }

    private sealed class FixedEquipmentChoiceRuntimeProvider : ILocalLlmRuntimeProvider
    {
        private readonly ILocalLlmRuntime runtime;

        public FixedEquipmentChoiceRuntimeProvider(ILocalLlmRuntime runtime)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public bool TryGetRuntime(out ILocalLlmRuntime resolved)
        {
            resolved = runtime;
            return true;
        }

        public ILocalLlmRuntime GetRequiredRuntime() => runtime;
    }

    private sealed class EquipmentChoiceRuntimeStub :
        ILocalLlmRuntime,
        IConstrainedEquipmentChoiceLlmRuntime
    {
        private readonly EquipmentChoiceStubMode mode;

        public EquipmentChoiceRuntimeStub(EquipmentChoiceStubMode mode)
        {
            this.mode = mode;
        }

        public bool GenerateEquipmentChoiceAsync(
            string requestKey,
            string prompt,
            int candidateCount,
            Action<LocalLlmChoiceResult> callback)
        {
            if (mode == EquipmentChoiceStubMode.RequestRejected)
            {
                return false;
            }
            if (mode == EquipmentChoiceStubMode.OutOfRange)
            {
                callback?.Invoke(new LocalLlmChoiceResult(true, 9, string.Empty));
                return true;
            }

            callback?.Invoke(new LocalLlmChoiceResult(
                false,
                -1,
                "EquipmentChoice.SchemaInvalid: fixture response rejected"));
            return true;
        }

        public bool GenerateCharacterSkillAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateCharacterRecordAsync(
            string prompt,
            string originalText,
            Action<LocalLlmResult> callback) => false;
        public bool GenerateBubbleLineAsync(
            string prompt,
            string originalText,
            Action<LocalLlmResult> callback) => false;
    }
}
#endif
