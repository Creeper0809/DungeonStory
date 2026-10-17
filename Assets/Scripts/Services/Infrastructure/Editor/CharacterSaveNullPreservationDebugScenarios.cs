#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

/// <summary>
/// Pure DTO coverage for the character-world public-field JSON codec. These
/// checks do not validate or publish a world candidate.
/// </summary>
public static class CharacterSaveNullPreservationDebugScenarios
{
    public static string[] RunChecks()
    {
        List<string> failures = new();
        Run("null-references-and-rumor-strings", VerifyNullReferencesAndRumorStrings, failures);
        Run("nonnull-values-and-exact-numbers", VerifyNonNullValuesAndExactNumbers, failures);
        Run("derived-getters-omitted", VerifyDerivedGettersAreOmitted, failures);
        return failures.ToArray();
    }

    private static void VerifyNullReferencesAndRumorStrings()
    {
        DungeonCharacterWorldSaveData source = CreatePayload(includeOptionalValues: false);
        string json = DungeonCharacterWorldSaveCodec.Serialize(source);
        JObject document = JObject.Parse(json);

        RequireExplicitNull(document, "actors[0].growth.ultimate");
        RequireExplicitNull(document, "actors[0].growth.activeSkills[0].drawbackEffect");
        RequireExplicitNull(document, "actors[0].growth.drafts[0].ownerBinding");
        RequireExplicitNull(document, "actors[0].growth.drafts[0].requestContext");
        RequireExplicitNull(document, "actors[0].acquiredTraits.pendingRequests[0].ownerBinding");
        RequireExplicitNull(document, "actors[0].acquiredTraits.pendingRequests[0].publicContext");
        RequireExplicitNull(document, "actors[0].acquiredTraits.lastRequestInvalidation");
        RequireExplicitNull(document, "actors[0].socialMemory.recentRumors[0].sourceActorName");
        RequireExplicitNull(document, "actors[0].socialMemory.recentRumors[0].targetFacilityTag");
        RequireExplicitNull(document, "actors[0].socialMemory.recentRumors[0].targetCharacterName");
        RequireExplicitNull(document, "actors[0].socialMemory.recentRumors[0].summary");
        RequireExplicitNull(document, "actors[0].socialMemory.recentRumors[0].source");

        DungeonCharacterSaveData restored = DeserializeActor(json);
        CharacterSkillInstance restoredSkill = restored.growth.activeSkills.Single();
        CharacterSkillDraft restoredDraft = restored.growth.drafts.Single();
        CharacterAcquiredTraitPendingRequestState restoredPending =
            restored.acquiredTraits.pendingRequests.Single();
        SocialRumorSnapshot restoredRumor =
            restored.socialMemory.recentRumors.Single();

        Require(restored.growth.ultimate == null,
            "A null ultimate was materialized during the codec round trip.");
        Require(restoredSkill.drawbackEffect == null,
            "A null drawback was materialized during the codec round trip.");
        Require(restoredDraft.ownerBinding == null
                && restoredDraft.requestContext == null,
            "A null pending skill request owner/context was materialized.");
        Require(restoredPending.ownerBinding == null
                && restoredPending.publicContext == null,
            "A null acquired-trait pending request owner/context was materialized.");
        Require(restored.acquiredTraits.lastRequestInvalidation == null,
            "A null request invalidation was materialized during the codec round trip.");
        Require(restoredRumor.sourceActorName == null
                && restoredRumor.targetFacilityTag == null
                && restoredRumor.targetCharacterName == null
                && restoredRumor.summary == null
                && restoredRumor.source == null,
            "Null rumor strings were rewritten during the codec round trip.");
    }

    private static void VerifyNonNullValuesAndExactNumbers()
    {
        DungeonCharacterWorldSaveData source = CreatePayload(includeOptionalValues: true);
        string json = DungeonCharacterWorldSaveCodec.Serialize(source);
        DungeonCharacterSaveData restored = DeserializeActor(json);
        CharacterSkillInstance restoredSkill = restored.growth.activeSkills.Single();
        CharacterSkillDraft restoredDraft = restored.growth.drafts.Single();
        CharacterAcquiredTraitPendingRequestState restoredPending =
            restored.acquiredTraits.pendingRequests.Single();
        SocialRumorSnapshot restoredRumor =
            restored.socialMemory.recentRumors.Single();

        Require(restored.growth.ultimate != null
                && restored.growth.ultimate.id == "skill:codec:ultimate"
                && restored.growth.ultimate.cooldownTurns == 37,
            "A non-null ultimate did not retain its field values.");
        Require(restoredSkill.drawbackEffect != null
                && restoredSkill.drawbackEffect.drawbackModuleId
                    == "drawback:codec"
                && restoredSkill.drawbackEffect.effectId == "effect:codec"
                && restoredSkill.drawbackEffect.value == -2.5f
                && restoredSkill.drawbackEffect.severityUnits == 7,
            "A non-null drawback did not retain its field values.");
        Require(restoredDraft.ownerBinding != null
                && restoredDraft.ownerBinding.persistentId
                    == "character:codec-owner"
                && restoredDraft.requestContext != null
                && restoredDraft.requestContext.version == 1
                && restoredDraft.requestContext.characterName == "Codec Owner"
                && restoredDraft.requestContext.publicContext != null
                && restoredDraft.requestContext.publicContext.semanticHash
                    == "skill-context-hash",
            "A non-null pending skill request did not retain its field values.");
        Require(restoredPending.ownerBinding != null
                && restoredPending.ownerBinding.generationRevision == 23
                && restoredPending.publicContext != null
                && restoredPending.publicContext.semanticHash
                    == "trait-context-hash"
                && restoredPending.registeredRevision == 314159,
            "A non-null acquired-trait pending request did not retain its field values.");
        Require(restored.acquiredTraits.lastRequestInvalidation != null
                && restored.acquiredTraits.lastRequestInvalidation.requestId
                    == "request:codec:invalidated"
                && restored.acquiredTraits.lastRequestInvalidation
                    .invalidatedRevision == 13,
            "A non-null request invalidation did not retain its field values.");
        Require(restoredRumor.sourceActorName == "Rumor Source"
                && restoredRumor.targetFacilityTag == "facility:codec"
                && restoredRumor.targetCharacterName == "Rumor Target"
                && restoredRumor.summary == "Exact rumor summary"
                && restoredRumor.source == "codec-regression",
            "Non-null rumor strings did not retain their exact values.");
        Require(restored.lifecycleRevision == 9007199254740993L
                && restored.currentHealth == 123.5f
                && restored.currentExperience == 987654321
                && restored.growth.generationSeed == 2147483000
                && restored.growth.skillAcquisitionRevision
                    == 9007199254740995L
                && restoredRumor.targetFacilityId == 1700000000
                && restoredRumor.baseSentiment == 0.5f
                && restoredRumor.sentiment == -0.25f
                && restoredRumor.remainingSeconds == 37.25f,
            "Public numeric DTO fields did not round-trip exactly.");
    }

    private static void VerifyDerivedGettersAreOmitted()
    {
        string json = DungeonCharacterWorldSaveCodec.Serialize(
            CreatePayload(includeOptionalValues: true));
        HashSet<string> propertyNames = JObject.Parse(json)
            .Descendants()
            .OfType<JProperty>()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Require(!propertyNames.Contains("IsReady"),
            "Derived IsReady was emitted by the public-field codec.");
        Require(!propertyNames.Contains("ChosenSkill"),
            "Derived ChosenSkill was emitted by the public-field codec.");
        Require(!propertyNames.Contains("HasData"),
            "Derived HasData was emitted by the public-field codec.");
        Require(!propertyNames.Contains("IsWellFormed")
                && !propertyNames.Contains("IdentitySuffix")
                && !propertyNames.Contains("ActiveCount")
                && !propertyNames.Contains("HasPersistentData"),
            "A derived request/aggregate getter was emitted by the public-field codec.");

        DungeonCharacterSaveData restored = DeserializeActor(json);
        Require(restored.growth.activeSkills.Single().IsReady
                && restored.growth.drafts.Single().ChosenSkill != null
                && restored.acquiredTraits.lastRequestInvalidation.HasData,
            "The fixture did not make the omitted derived getters observable.");
    }

    private static DungeonCharacterWorldSaveData CreatePayload(
        bool includeOptionalValues)
    {
        const string OwnerId = "character:codec-owner";
        CharacterNarrativeRequestOwnerBinding ownerBinding =
            includeOptionalValues
                ? new CharacterNarrativeRequestOwnerBinding
                {
                    version = CharacterNarrativeRequestOwnerBinding.CurrentVersion,
                    persistentId = OwnerId,
                    lifecycleRevision = 17,
                    generationRevision = 23
                }
                : null;
        CharacterSkillInstance skill = new()
        {
            id = "skill:codec:active",
            displayName = "Codec Active",
            formulaVersion = 0,
            modules = new List<CharacterSkillModuleSelection>
            {
                new()
                {
                    moduleId = "module:codec",
                    variantId = "variant:codec"
                }
            },
            drawbackEffect = includeOptionalValues
                ? new CharacterSkillDrawbackEffectEnvelope
                {
                    drawbackModuleId = "drawback:codec",
                    effectId = "effect:codec",
                    targetId = OwnerId,
                    value = -2.5f,
                    severityUnits = 7,
                    displayName = "Codec Drawback"
                }
                : null
        };
        CharacterSkillDraft draft = new()
        {
            unlockLevel = 19,
            requestKey = "request:codec:skill",
            requestSubmitted = true,
            permanentlyChosen = true,
            chosenIndex = 0,
            candidates = new List<CharacterSkillInstance> { skill },
            ownerBinding = ownerBinding,
            requestContextVersion = includeOptionalValues ? 1 : 0,
            requestContextHash = includeOptionalValues
                ? "request-context-hash"
                : null,
            requestContext = includeOptionalValues
                ? new CharacterSkillRequestContextSnapshot
                {
                    version = 1,
                    characterName = "Codec Owner",
                    origin = "Codec Origin",
                    species = "Codec Species",
                    potential = "Codec Potential",
                    publicContext = CreatePublicContext(
                        OwnerId,
                        "skill-context-hash")
                }
                : null,
            formulaBudget = 97
        };
        CharacterAcquiredTraitPendingRequestState pending = new()
        {
            targetPersistentId = OwnerId,
            requestId = "request:codec:trait",
            requestKey = "request-key:codec:trait",
            ownerBinding = includeOptionalValues
                ? new CharacterNarrativeRequestOwnerBinding
                {
                    version = CharacterNarrativeRequestOwnerBinding.CurrentVersion,
                    persistentId = OwnerId,
                    lifecycleRevision = 17,
                    generationRevision = 23
                }
                : null,
            publicContextVersion = includeOptionalValues ? 1 : 0,
            publicContext = includeOptionalValues
                ? CreatePublicContext(OwnerId, "trait-context-hash")
                : null,
            manifestationMilestone = 11,
            registeredRevision = 314159,
            formulaVersion = 2,
            calculatedCost = 43
        };
        SocialRumorSnapshot rumor = new()
        {
            type = SocialRumorType.Recommendation,
            targetType = SocialRumorTargetType.Facility,
            sourceActorId = OwnerId,
            sourceActorName = includeOptionalValues ? "Rumor Source" : null,
            targetFacilityId = 1700000000,
            targetFacilityTag = includeOptionalValues
                ? "facility:codec"
                : null,
            targetCharacterId = "character:codec-target",
            targetCharacterName = includeOptionalValues
                ? "Rumor Target"
                : null,
            baseSentiment = 0.5f,
            sentiment = -0.25f,
            spreadChance = 0.75f,
            trustImpact = -0.5f,
            remainingSeconds = 37.25f,
            summary = includeOptionalValues ? "Exact rumor summary" : null,
            source = includeOptionalValues ? "codec-regression" : null
        };

        return new DungeonCharacterWorldSaveData
        {
            actors = new List<DungeonCharacterSaveData>
            {
                new()
                {
                    persistentId = OwnerId,
                    dataId = 7301,
                    displayName = "Codec Owner",
                    lifecycleRevision = 9007199254740993L,
                    currentHealth = 123.5f,
                    currentExperience = 987654321,
                    growth = new CharacterGrowthState
                    {
                        initialized = true,
                        generationSeed = 2147483000,
                        activeSkills = new List<CharacterSkillInstance> { skill },
                        passiveSkills = new List<CharacterSkillInstance>(),
                        ultimate = includeOptionalValues
                            ? new CharacterSkillInstance
                            {
                                id = "skill:codec:ultimate",
                                displayName = "Codec Ultimate",
                                cooldownTurns = 37
                            }
                            : null,
                        drafts = new List<CharacterSkillDraft> { draft },
                        skillAcquisitionRevision = 9007199254740995L
                    },
                    acquiredTraits = new CharacterAcquiredTraitAggregateState
                    {
                        revision = 2718,
                        pendingRequests =
                            new List<CharacterAcquiredTraitPendingRequestState>
                            {
                                pending
                            },
                        lastRequestInvalidation = includeOptionalValues
                            ? new CharacterAcquiredTraitRequestInvalidationState
                            {
                                requestId = "request:codec:invalidated",
                                requestKey = "request-key:codec:invalidated",
                                reason = "codec-regression",
                                registeredRevision = 8,
                                invalidatedRevision = 13
                            }
                            : null
                    },
                    socialMemory = new CharacterSocialMemorySnapshot
                    {
                        recentRumors = new List<SocialRumorSnapshot> { rumor }
                    }
                }
            }
        };
    }

    private static NarrativePublicContextMaterialSnapshot CreatePublicContext(
        string subjectId,
        string semanticHash) => new()
    {
        profileId = "profile:codec",
        subjectId = subjectId,
        subjectKind = NarrativePublicSubjectKind.Character,
        selectionPolicyId = "selection:codec",
        availableFactCount = 3,
        omittedFactCount = 1,
        semanticHash = semanticHash
    };

    private static DungeonCharacterSaveData DeserializeActor(string json)
    {
        DungeonCharacterWorldSaveData restored =
            DungeonSaveJson.Deserialize<DungeonCharacterWorldSaveData>(json);
        Require(restored?.actors?.Count == 1,
            "The character-world codec did not restore its actor list.");
        return restored.actors[0]
            ?? throw new InvalidOperationException(
                "The character-world codec restored a null actor.");
    }

    private static void RequireExplicitNull(JObject document, string path)
    {
        JToken token = document.SelectToken(path);
        Require(token != null && token.Type == JTokenType.Null,
            "The codec did not emit an explicit null at '" + path + "'.");
    }

    private static void Run(
        string name,
        Action scenario,
        ICollection<string> failures)
    {
        try
        {
            scenario();
        }
        catch (Exception exception)
        {
            failures.Add(name + ": " + exception.GetType().Name
                + ": " + exception.Message);
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
