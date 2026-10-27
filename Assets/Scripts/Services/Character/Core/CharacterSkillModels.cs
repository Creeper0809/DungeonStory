using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Characters;
using DungeonStory.Foundation.Facts;
using UnityEngine;

[Serializable]
public sealed class CharacterSkillDraft
{
    public bool IsInvalidated() => presentationState is
        CharacterSkillPresentationState.InvalidatedOwner or CharacterSkillPresentationState.InvalidatedEvidence;

    public int unlockLevel;
    public CharacterSkillKind kind;
    public CharacterUltimateDomain requestedUltimateDomain;
    public string requestKey = string.Empty;
    public CharacterNarrativeRequestOwnerBinding ownerBinding;
    public string ownerInvalidationReason = string.Empty;
    public bool requestSubmitted;
    public bool isReady;
    public bool permanentlyChosen;
    public int chosenIndex = -1;
    public bool grantsUpperRarityPity;
    public List<CharacterSkillCandidateRule> rules = new List<CharacterSkillCandidateRule>();
    public List<CharacterSkillInstance> candidates = new List<CharacterSkillInstance>();
    public int formulaVersion;
    public string formulaCatalogSha256 = string.Empty;
    [Min(0)] public int formulaBudget;
    public CharacterSkillPresentationState presentationState;
    [Min(0)] public int presentationFailureCount;
    [Min(0)] public int nextPresentationIndex;
    public List<CharacterSkillInstance> frozenMechanics =
        new List<CharacterSkillInstance>();
    public List<CharacterSkillModuleOfferState> moduleSelectionOffers =
        new List<CharacterSkillModuleOfferState>();
    public List<GameplayOutcomeEvidenceBindingSnapshot> outcomeEvidenceBindings = new();
    public int requestContextVersion;
    public string requestContextHash = string.Empty;
    public CharacterSkillRequestContextSnapshot requestContext;
    public string presentationCorrection = string.Empty;

    public CharacterSkillInstance ChosenSkill => permanentlyChosen
        && chosenIndex >= 0
        && candidates != null
        && chosenIndex < candidates.Count
            ? candidates[chosenIndex]
            : null;

    public NarrativePublicContextMaterial RestoreRequestPublicContext()
    {
        if (requestContextVersion == 0 && string.IsNullOrEmpty(requestContextHash)
            && requestContext?.Restore(this) == null) return null;
        if (requestContextVersion != 1 || requestContext == null || requestContext.version != 1)
            throw new InvalidOperationException("CharacterSkill request context is missing or has an invalid version.");
        return requestContext.Restore(this);
    }

    public CharacterSkillDraft Clone()
    {
        return new CharacterSkillDraft
        {
            unlockLevel = unlockLevel,
            kind = kind,
            requestedUltimateDomain = requestedUltimateDomain,
            requestKey = requestKey,
            requestContextVersion = requestContextVersion,
            requestContextHash = requestContextHash,
            requestContext = requestContext?.Clone(),
            presentationCorrection = presentationCorrection,
            ownerBinding = ownerBinding?.Clone(),
            ownerInvalidationReason = ownerInvalidationReason,
            requestSubmitted = requestSubmitted,
            isReady = isReady,
            permanentlyChosen = permanentlyChosen,
            chosenIndex = chosenIndex,
            grantsUpperRarityPity = grantsUpperRarityPity,
            rules = rules?.Where(item => item != null).Select(item => item.Clone()).ToList()
                ?? new List<CharacterSkillCandidateRule>(),
            candidates = candidates?.Where(item => item != null).Select(item => item.Clone()).ToList()
                ?? new List<CharacterSkillInstance>(),
            formulaVersion = formulaVersion,
            formulaCatalogSha256 = formulaCatalogSha256,
            formulaBudget = formulaBudget,
            presentationState = presentationState,
            presentationFailureCount = presentationFailureCount,
            nextPresentationIndex = nextPresentationIndex,
            frozenMechanics = frozenMechanics?.Where(item => item != null)
                .Select(item => item.Clone()).ToList()
                ?? new List<CharacterSkillInstance>(),
            moduleSelectionOffers = moduleSelectionOffers?.Where(item => item != null)
                .Select(item => item.Clone()).ToList()
                ?? new List<CharacterSkillModuleOfferState>(),
            outcomeEvidenceBindings = outcomeEvidenceBindings?
                .Where(value => value != null)
                .Select(value => value.Clone()).ToList()
                ?? new List<GameplayOutcomeEvidenceBindingSnapshot>()
        };
    }
}

[Serializable]
public sealed class CharacterSkillRequestContextSnapshot
{
    public int version;
    public NarrativePublicContextMaterialSnapshot publicContext;
    public string characterName = string.Empty;
    public string origin = string.Empty;
    public string species = string.Empty;
    public string potential = string.Empty;

    public CharacterSkillRequestContextSnapshot Clone()
    {
        var copy = (CharacterSkillRequestContextSnapshot)MemberwiseClone();
        copy.publicContext = publicContext?.Clone();
        return copy;
    }

    public static CharacterSkillRequestContextSnapshot Capture(
        CharacterProgression progression, CharacterSkillDraft draft, NarrativePublicContextMaterial material)
    {
        var snapshot = new CharacterSkillRequestContextSnapshot
        {
            version = 1,
            publicContext = material.CaptureSnapshot(),
            characterName = progression.Actor?.Identity?.DisplayName,
            origin = progression.GrowthState.origin ?? string.Empty,
            species = progression.Actor?.SpeciesTag ?? string.Empty,
            potential = CharacterSkillDisplay.Potential(progression.GrowthState.potentialGrade)
        };
        if (string.IsNullOrWhiteSpace(snapshot.characterName))
            snapshot.characterName = progression.GrowthState.displayName ?? "인물";
        if (string.IsNullOrWhiteSpace(snapshot.characterName))
            throw new InvalidOperationException("CharacterSkill request context requires a nonblank character name.");
        snapshot.ValidateOwner(material, draft);
        return snapshot;
    }

    public NarrativePublicContextMaterial Restore(CharacterSkillDraft draft)
    {
        // Inline Unity JSON can instantiate a null nested DTO as an empty v0 object.
        // Only a completely empty envelope is an old, uncaptured request.
        if (version == 0 && string.IsNullOrEmpty(draft?.requestContextHash)
            && string.IsNullOrEmpty(characterName) && string.IsNullOrEmpty(origin)
            && string.IsNullOrEmpty(species) && string.IsNullOrEmpty(potential)
            && (publicContext == null || string.IsNullOrEmpty(publicContext.semanticHash)
                && string.IsNullOrEmpty(publicContext.profileId) && string.IsNullOrEmpty(publicContext.subjectId)
                && string.IsNullOrEmpty(publicContext.cultureStyleId) && string.IsNullOrEmpty(publicContext.selectionPolicyId)
                && !publicContext.requireCharacterFact && !publicContext.requireMotif
                && publicContext.availableFactCount == 0 && publicContext.omittedFactCount == 0
                && (publicContext.entities?.Count ?? 0) == 0 && (publicContext.publicFacts?.Count ?? 0) == 0
                && (publicContext.events?.Count ?? 0) == 0 && (publicContext.motifs?.Count ?? 0) == 0
                && (publicContext.backgroundFactIds?.Count ?? 0) == 0 && (publicContext.relationshipFactIds?.Count ?? 0) == 0
                && (publicContext.memoryFactIds?.Count ?? 0) == 0 && (publicContext.priorHistoryFactIds?.Count ?? 0) == 0))
            return null;
        if (version != 1 || publicContext == null || draft?.ownerBinding == null
            || string.IsNullOrWhiteSpace(characterName) || origin == null || species == null || potential == null
            || !string.Equals(draft.requestContextHash, ComputeHash(draft.requestKey), StringComparison.Ordinal))
            throw new InvalidOperationException("CharacterSkill frozen request context version, header or request binding is invalid.");
        NarrativePublicContextMaterial material = NarrativePublicContextFactory.Restore(publicContext);
        ValidateOwner(material, draft);
        return material;
    }

    private void ValidateOwner(NarrativePublicContextMaterial material, CharacterSkillDraft draft)
    {
        if (material.ProfileId != LocalLlmRequestProfiles.CharacterSkill.Id
            || material.SubjectKind != NarrativePublicSubjectKind.Character
            || material.SubjectId != draft.ownerBinding.persistentId)
            throw new InvalidOperationException("CharacterSkill frozen public context does not match its profile and owner.");
    }

    internal string ComputeHash(string requestKey) => NarrativeInferenceHash.ComputeSha256Utf8(
        "skill-request-context-v1:" + string.Concat(new[]
        {
            requestKey, publicContext?.semanticHash, characterName, origin, species, potential
        }.Select(value => (value ?? string.Empty).Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ":" + (value ?? string.Empty))));
}

[Serializable]
public sealed class CharacterSkillModuleOfferState
{
    public string selectionId = string.Empty;
    public string ruleId = string.Empty;
    public List<string> positiveModuleIds = new List<string>();
    public List<string> drawbackModuleIds = new List<string>();
    public List<string> evidenceFactIds = new List<string>();
    public List<string> qualifiedNegativeEvidenceFactIds = new List<string>();
    [Min(1)] public int maximumPositiveModules = 3;
    [Min(0)] public int maximumDrawbackModules;

    public CharacterSkillModuleOfferState Clone() => new CharacterSkillModuleOfferState
    {
        selectionId = selectionId,
        ruleId = ruleId,
        positiveModuleIds = positiveModuleIds?.ToList() ?? new List<string>(),
        drawbackModuleIds = drawbackModuleIds?.ToList() ?? new List<string>(),
        evidenceFactIds = evidenceFactIds?.ToList() ?? new List<string>(),
        qualifiedNegativeEvidenceFactIds = qualifiedNegativeEvidenceFactIds?.ToList()
            ?? new List<string>(),
        maximumPositiveModules = maximumPositiveModules,
        maximumDrawbackModules = maximumDrawbackModules
    };
}

[Serializable]
public sealed class CharacterManualSkillCooldownState
{
    public string skillId = string.Empty;
    public long readyAbsoluteHour;
    public int useCount;

    public CharacterManualSkillCooldownState Clone() => new CharacterManualSkillCooldownState
    {
        skillId = skillId,
        readyAbsoluteHour = readyAbsoluteHour,
        useCount = useCount
    };
}

[Serializable]
public sealed class CharacterManualSkillBuffState
{
    public string sourceCharacterId = string.Empty;
    public string skillId = string.Empty;
    public long expiresAbsoluteHour;
    public CharacterSkillInstance frozenSkill;

    public CharacterManualSkillBuffState Clone() => new CharacterManualSkillBuffState
    {
        sourceCharacterId = sourceCharacterId,
        skillId = skillId,
        expiresAbsoluteHour = expiresAbsoluteHour,
        frozenSkill = frozenSkill?.Clone()
    };
}

[Serializable]
public enum CharacterSkillActivationKind
{
    None = 0,
    ManualDungeon = 1,
    ManualExpedition = 2,
    AutomaticPassive = 3,
    AutomaticUltimate = 4,
    ManualCombatUltimate = 5
}

public static class CharacterSkillActivationIdentity
{
    public const string OperationPrefix = "skill-use:";

    public static string OperationId(string sourceCharacterId, long revision)
    {
        if (!DungeonStory.Foundation.Facts.DomainFactValueSyntax.IsIdentity(sourceCharacterId))
            throw new ArgumentException("skill-activation-source-identity-invalid", nameof(sourceCharacterId));
        string source = sourceCharacterId;
        if (revision <= 0L)
            throw new ArgumentOutOfRangeException(nameof(revision));
        return OperationPrefix + source + ":" + revision;
    }

}

[Serializable]
public sealed class CharacterSkillActivationCommitState
{
    public long ownerRevision;
    public string operationId = string.Empty;
    public string resultKey = string.Empty;
    public string canonicalPayloadHash = string.Empty;
    public bool observationPending;
    public CharacterSkillActivationKind activationKind;
    public string skillId = string.Empty;
    public string mechanicsSha256 = string.Empty;
    public long absoluteHour;
    public long expiresAbsoluteHour;
    public int useCountBefore;
    public int useCountAfter;
    public long readyAbsoluteHourBefore;
    public long readyAbsoluteHourAfter;
    public List<string> recipientCharacterIds = new List<string>();
    public string recipientStateSha256 = string.Empty;
    public string triggerEventId = string.Empty;
    public string triggerEventKeySha256 = string.Empty;
    public CharacterUltimateDomain ultimateDomain;
    public int triggerSerial = -1;

    public CharacterSkillActivationCommitState Clone() => new()
    {
        ownerRevision = ownerRevision,
        operationId = operationId,
        resultKey = resultKey,
        canonicalPayloadHash = canonicalPayloadHash,
        observationPending = observationPending,
        activationKind = activationKind,
        skillId = skillId,
        mechanicsSha256 = mechanicsSha256,
        absoluteHour = absoluteHour,
        expiresAbsoluteHour = expiresAbsoluteHour,
        useCountBefore = useCountBefore,
        useCountAfter = useCountAfter,
        readyAbsoluteHourBefore = readyAbsoluteHourBefore,
        readyAbsoluteHourAfter = readyAbsoluteHourAfter,
        recipientCharacterIds = recipientCharacterIds?.ToList()
            ?? new List<string>(),
        recipientStateSha256 = recipientStateSha256,
        triggerEventId = triggerEventId,
        triggerEventKeySha256 = triggerEventKeySha256,
        ultimateDomain = ultimateDomain,
        triggerSerial = triggerSerial
    };
}

[Serializable]
public sealed class CharacterAutomaticSkillActivationState
{
    public string eventKeySha256 = string.Empty;
    public string eventId = string.Empty;
    public string skillId = string.Empty;
    public CharacterSkillActivationKind activationKind;
    public long ownerRevision;
    public int useCount;

    public CharacterAutomaticSkillActivationState Clone() => new()
    {
        eventKeySha256 = eventKeySha256,
        eventId = eventId,
        skillId = skillId,
        activationKind = activationKind,
        ownerRevision = ownerRevision,
        useCount = useCount
    };
}

[Serializable]
public sealed class CharacterSkillUseLimitState
{
    public int offenseBattleSerial = -1;
    public int defenseInvasionSerial = -1;
    public int managementOperatingDay = -1;
    public List<CharacterManualSkillCooldownState> manualSkillCooldowns = new List<CharacterManualSkillCooldownState>();
    public List<CharacterManualSkillBuffState> manualSkillBuffs = new List<CharacterManualSkillBuffState>();
    public List<CharacterAutomaticSkillActivationState> automaticSkillActivations =
        new List<CharacterAutomaticSkillActivationState>();
    public long activationSequence;
    public CharacterSkillActivationCommitState lastSkillActivation;

    public bool CanUse(CharacterUltimateDomain domain, int serial)
    {
        return domain switch
        {
            CharacterUltimateDomain.Offense => offenseBattleSerial != serial,
            CharacterUltimateDomain.Defense => defenseInvasionSerial != serial,
            CharacterUltimateDomain.Management => managementOperatingDay != serial,
            _ => false
        };
    }

    public void MarkUsed(CharacterUltimateDomain domain, int serial)
    {
        switch (domain)
        {
            case CharacterUltimateDomain.Offense:
                offenseBattleSerial = serial;
                break;
            case CharacterUltimateDomain.Defense:
                defenseInvasionSerial = serial;
                break;
            case CharacterUltimateDomain.Management:
                managementOperatingDay = serial;
                break;
        }
    }

    public CharacterSkillUseLimitState Clone()
    {
        return new CharacterSkillUseLimitState
        {
            offenseBattleSerial = offenseBattleSerial,
            defenseInvasionSerial = defenseInvasionSerial,
            managementOperatingDay = managementOperatingDay,
            manualSkillCooldowns = (manualSkillCooldowns
                ?? new List<CharacterManualSkillCooldownState>())
                .Where(value => value != null).Select(value => value.Clone()).ToList(),
            manualSkillBuffs = (manualSkillBuffs
                ?? new List<CharacterManualSkillBuffState>())
                .Where(value => value != null).Select(value => value.Clone()).ToList(),
            automaticSkillActivations = (automaticSkillActivations
                ?? new List<CharacterAutomaticSkillActivationState>())
                .Where(value => value != null)
                .Select(value => value.Clone())
                .ToList(),
            activationSequence = activationSequence,
            lastSkillActivation = lastSkillActivation?.Clone()
        };
    }

    public void ValidatePersistentState(
        string sourceCharacterId,
        IEnumerable<CharacterSkillInstance> availableSkills)
    {
        if (activationSequence < 0L
            || manualSkillCooldowns == null
            || manualSkillBuffs == null
            || automaticSkillActivations == null
            || manualSkillCooldowns.Any(value => value == null
                || string.IsNullOrWhiteSpace(value.skillId)
                || value.readyAbsoluteHour < 0L
                || value.useCount < 0)
            || manualSkillCooldowns
                .Where(value => value != null)
                .Select(value => value.skillId)
                .Distinct(StringComparer.Ordinal).Count()
                != manualSkillCooldowns.Count
            || manualSkillBuffs.Any(value => value == null
                || string.IsNullOrWhiteSpace(value.sourceCharacterId)
                || string.IsNullOrWhiteSpace(value.skillId)
                || value.expiresAbsoluteHour < 0L
                || value.frozenSkill == null)
            || automaticSkillActivations.Any(value => value == null
                || !IsSha256(value.eventKeySha256)
                || !DomainFactValueSyntax.IsText(
                    value.eventId,
                    CharacterSkillActivationFact.MaximumStateCharacters,
                    allowEmpty: false)
                || !DomainFactValueSyntax.IsIdentity(value.skillId)
                || value.activationKind is not
                    (CharacterSkillActivationKind.AutomaticPassive
                     or CharacterSkillActivationKind.AutomaticUltimate
                     or CharacterSkillActivationKind.ManualCombatUltimate)
                || value.ownerRevision <= 0L
                || value.ownerRevision > activationSequence
                || value.useCount <= 0)
            || automaticSkillActivations.Select(value => value.eventKeySha256)
                .Distinct(StringComparer.Ordinal).Count()
                != automaticSkillActivations.Count
            || automaticSkillActivations.Select(value => value.ownerRevision)
                .Distinct().Count() != automaticSkillActivations.Count)
        {
            throw new InvalidOperationException(
                "Character skill use-limit state is invalid.");
        }

        if (activationSequence == 0L)
        {
            if (lastSkillActivation != null
                && (!string.IsNullOrEmpty(lastSkillActivation.operationId)
                    || lastSkillActivation.ownerRevision != 0L))
            {
                throw new InvalidOperationException(
                    "Character skill activation owner exists without a sequence.");
            }
            return;
        }

        CharacterSkillActivationCommitState committed = lastSkillActivation
            ?? throw new InvalidOperationException(
                "Character skill activation owner is missing.");
        CharacterSkillInstance[] matchingSkills = (availableSkills
                ?? throw new ArgumentNullException(nameof(availableSkills)))
            .Where(value => value != null
                && string.Equals(
                    value.id,
                    committed.skillId,
                    StringComparison.Ordinal))
            .ToArray();
        bool mechanicsCanonical = matchingSkills.Length == 1
            && string.Equals(
                CharacterSkillActivationTransaction
                    .ComputeMechanicsSha256(matchingSkills[0]),
                committed.mechanicsSha256,
                StringComparison.Ordinal);
        string expectedOperation = CharacterSkillActivationIdentity
            .OperationId(sourceCharacterId, activationSequence);
        string[] recipients = (committed.recipientCharacterIds
                ?? new List<string>())
            .ToArray();
        bool recipientsCanonical = recipients.Length > 0
            && recipients.All(DomainFactValueSyntax.IsIdentity)
            && recipients.SequenceEqual(
                recipients.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal)
            && recipients.Distinct(StringComparer.Ordinal).Count()
                == recipients.Length;
        bool eventBound = committed.activationKind is
            CharacterSkillActivationKind.AutomaticPassive
            or CharacterSkillActivationKind.AutomaticUltimate
            or CharacterSkillActivationKind.ManualCombatUltimate;
        CharacterAutomaticSkillActivationState automaticState = eventBound
            ? automaticSkillActivations.SingleOrDefault(value => value != null
                && string.Equals(
                    value.eventKeySha256,
                    committed.triggerEventKeySha256,
                    StringComparison.Ordinal))
            : null;
        bool eventBoundContract = !eventBound
            ? string.IsNullOrEmpty(committed.triggerEventId)
                && string.IsNullOrEmpty(committed.triggerEventKeySha256)
            : automaticState != null
                && automaticState.ownerRevision == activationSequence
                && automaticState.useCount == committed.useCountAfter
                && string.Equals(
                    automaticState.eventId,
                    committed.triggerEventId,
                    StringComparison.Ordinal)
                && string.Equals(
                    automaticState.skillId,
                    committed.skillId,
                    StringComparison.Ordinal)
                && automaticState.activationKind == committed.activationKind
                && IsSha256(committed.triggerEventKeySha256)
                && DomainFactValueSyntax.IsText(
                    committed.triggerEventId,
                    CharacterSkillActivationFact.MaximumStateCharacters,
                    allowEmpty: false)
                && (committed.activationKind
                        == CharacterSkillActivationKind.AutomaticPassive
                    ? committed.ultimateDomain
                        == CharacterUltimateDomain.None
                        && committed.triggerSerial == -1
                    : committed.ultimateDomain
                        != CharacterUltimateDomain.None
                        && Enum.IsDefined(
                            typeof(CharacterUltimateDomain),
                            committed.ultimateDomain)
                        && committed.triggerSerial >= 0);
        if (committed.ownerRevision != activationSequence
            || committed.observationPending && committed.canonicalPayloadHash != string.Empty
            || !string.Equals(
                committed.operationId,
                expectedOperation,
                StringComparison.Ordinal)
            || !DungeonStory.Foundation.Facts.DomainFactValueSyntax.IsText(committed.resultKey, 1024, false)
            || !Enum.IsDefined(
                typeof(CharacterSkillActivationKind),
                committed.activationKind)
            || committed.activationKind == CharacterSkillActivationKind.None
            || !DomainFactValueSyntax.IsIdentity(committed.skillId)
            || !IsSha256(committed.mechanicsSha256)
            || !mechanicsCanonical
            || committed.absoluteHour < 0L
            || committed.expiresAbsoluteHour < committed.absoluteHour
            || committed.useCountBefore < 0
            || committed.useCountAfter != committed.useCountBefore + 1
            || (!eventBound
                && (committed.readyAbsoluteHourBefore < 0L
                    || committed.readyAbsoluteHourAfter
                        < committed.absoluteHour))
            || (eventBound
                && (committed.readyAbsoluteHourBefore != 0L
                    || committed.readyAbsoluteHourAfter != 0L))
            || !recipientsCanonical
            || !IsSha256(committed.recipientStateSha256)
            || committed.canonicalPayloadHash.Length > 0
                && !IsSha256(committed.canonicalPayloadHash)
            || !eventBoundContract)
        {
            throw new InvalidOperationException(
                "Character skill activation owner is incoherent.");
        }
    }

    private static bool IsSha256(string value) =>
        value != null
        && value.Length == 64
        && value.All(character =>
            character is >= '0' and <= '9'
                or >= 'a' and <= 'f'
                or >= 'A' and <= 'F');
}

public enum CharacterTraitSelectionAuthorityOrigin
{
    None = 0,
    PreparedSelection = 1,
    PopulationGeneration = 2,
    LegacyCharacterDefinitionBootstrap = 3
}

[Serializable]
public sealed class CharacterGrowthState
{
    public const int CurrentTraitSelectionAuthorityVersion = 1;

    public bool initialized;
    public bool autoChooseDrafts;
    public CharacterPotentialGrade potentialGrade;
    public int generationSeed;
    public string origin = string.Empty;
    public string displayName = string.Empty;
    public CharacterStartingProfileState startingProfile =
        new CharacterStartingProfileState();
    public List<CharacterStartingProficiencyExperience> startingProficiencies =
        new List<CharacterStartingProficiencyExperience>();
    public int traitSelectionAuthorityVersion;
    public CharacterTraitSelectionAuthorityOrigin traitSelectionAuthorityOrigin;
    public List<int> traitIds = new List<int>();
    public List<CharacterSkillInstance> activeSkills = new List<CharacterSkillInstance>();
    public List<CharacterSkillInstance> passiveSkills = new List<CharacterSkillInstance>();
    public CharacterSkillInstance ultimate;
    public List<CharacterSkillDraft> drafts = new List<CharacterSkillDraft>();
    public List<string> pendingRequestKeys = new List<string>();
    public CharacterSkillUseLimitState useLimits = new CharacterSkillUseLimitState();
    public bool nextActiveDraftHasPity;
    public int skillGenerationRevision;
    public long skillAcquisitionRevision;

    public void EnsureCollections()
    {
        startingProficiencies ??=
            new List<CharacterStartingProficiencyExperience>();
        startingProfile ??= new CharacterStartingProfileState();
        startingProfile.EnsureCollections();
        foreach (CharacterStartingProficiencyExperience value in
                 startingProficiencies.Where(value => value != null))
        {
            CharacterProficiencyId proficiencyId = new(value.proficiencyId);
            value.learningMultiplier = startingProfile.prepared
                ? CharacterProficiencySpecializationRules.Resolve(
                    startingProfile,
                    proficiencyId)
                : CharacterProficiencySpecializationRules
                    .NormalizeSerializedMultiplier(value.learningMultiplier);
        }
        traitIds ??= new List<int>();
        activeSkills ??= new List<CharacterSkillInstance>();
        passiveSkills ??= new List<CharacterSkillInstance>();
        drafts ??= new List<CharacterSkillDraft>();
        pendingRequestKeys ??= new List<string>();
        useLimits ??= new CharacterSkillUseLimitState();
    }

    public CharacterGrowthState Clone()
    {
        var copy = new CharacterGrowthState
        {
            initialized = initialized,
            autoChooseDrafts = autoChooseDrafts,
            potentialGrade = potentialGrade,
            generationSeed = generationSeed,
            origin = origin,
            displayName = displayName,
            startingProfile = startingProfile?.Clone() ?? new CharacterStartingProfileState(),
            startingProficiencies = (startingProficiencies ?? new List<CharacterStartingProficiencyExperience>())
                .Where(item => item != null)
                .Select(item => item.Clone())
                .ToList(),
            traitSelectionAuthorityVersion = traitSelectionAuthorityVersion,
            traitSelectionAuthorityOrigin = traitSelectionAuthorityOrigin,
            traitIds = (traitIds ?? new List<int>()).ToList(),
            activeSkills = (activeSkills ?? new List<CharacterSkillInstance>()).Where(item => item != null).Select(item => item.Clone()).ToList(),
            passiveSkills = (passiveSkills ?? new List<CharacterSkillInstance>()).Where(item => item != null).Select(item => item.Clone()).ToList(),
            ultimate = ultimate?.Clone(),
            drafts = (drafts ?? new List<CharacterSkillDraft>()).Where(item => item != null).Select(item => item.Clone()).ToList(),
            pendingRequestKeys = (pendingRequestKeys ?? new List<string>()).ToList(),
            useLimits = useLimits?.Clone() ?? new CharacterSkillUseLimitState(),
            nextActiveDraftHasPity = nextActiveDraftHasPity,
            skillGenerationRevision = skillGenerationRevision,
            skillAcquisitionRevision = skillAcquisitionRevision
        };
        copy.EnsureCollections();
        return copy;
    }
}

[Serializable]
public enum CharacterVisitorExitPhase
{
    None = 0,
    ExitPending = 1,
    TheftCommitted = 2,
    Departed = 3,
    ExitFailed = 4
}

[Serializable]
public sealed class CharacterVisitorExitOperationSaveData
{
    public string operationId = string.Empty;
    public long ownerRevision;
    public CharacterVisitorExitPhase phase;
    public string sourceMacro = string.Empty;
    public int absoluteDay;
    public CharacterLifecycleState lifecycleBefore;
    public bool visitingBefore;
    public int visitCountBefore;
    public int equipmentCountBefore;
    public string theftPublicationKey = string.Empty;
    public string departurePublicationKey = string.Empty;
    public string terminalKind = string.Empty;
    public string failureReason = string.Empty;

    public bool IsActive => !string.IsNullOrWhiteSpace(operationId)
        && ownerRevision > 0L
        && phase is CharacterVisitorExitPhase.ExitPending
            or CharacterVisitorExitPhase.TheftCommitted;

    public CharacterVisitorExitOperationSaveData Clone() => new()
    {
        operationId = operationId,
        ownerRevision = ownerRevision,
        phase = phase,
        sourceMacro = sourceMacro,
        absoluteDay = absoluteDay,
        lifecycleBefore = lifecycleBefore,
        visitingBefore = visitingBefore,
        visitCountBefore = visitCountBefore,
        equipmentCountBefore = equipmentCountBefore,
        theftPublicationKey = theftPublicationKey,
        departurePublicationKey = departurePublicationKey,
        terminalKind = terminalKind,
        failureReason = failureReason
    };
}

[Serializable]
public sealed class WorldCharacterProfile : ICharacterPopulationProfileState
{
    public string persistentId = string.Empty;
    public int characterDataId = -1;
    public string displayName = string.Empty;
    public string origin = string.Empty;
    public bool isOwner;
    public bool isStaff;
    public bool isAlive = true;
    public bool isVisiting;
    public CharacterSettlementStanding settlementStanding;
    public int visitCount;
    public CharacterSocialMemorySnapshot socialMemory = new CharacterSocialMemorySnapshot();
    public int level = 1;
    public int currentExperience;
    public CharacterGrowthState growth = new CharacterGrowthState();
    public CharacterNarrativeLedger narrative = new CharacterNarrativeLedger();
    public CharacterAcquiredTraitAggregateState acquiredTraits =
        new CharacterAcquiredTraitAggregateState();
    public long nextVisitorExitSequence = 1L;
    public CharacterVisitorExitOperationSaveData visitorExitOperation =
        new CharacterVisitorExitOperationSaveData();

    public bool IsReady => growth != null
        && growth.activeSkills != null
        && growth.activeSkills.Count > 0
        && growth.passiveSkills != null
        && growth.passiveSkills.Count > 0;

    string ICharacterPopulationProfileState.PersistentId => persistentId;
    int ICharacterPopulationProfileState.CharacterDataId => characterDataId;
    bool ICharacterPopulationProfileState.IsAlive
    {
        get => isAlive;
        set => isAlive = value;
    }
    bool ICharacterPopulationProfileState.IsStaff
    {
        get => isStaff;
        set
        {
            isStaff = value;
            if (value && settlementStanding is
                CharacterSettlementStanding.Unknown or
                CharacterSettlementStanding.PreparedCandidate or
                CharacterSettlementStanding.Visitor)
            {
                settlementStanding = CharacterSettlementStanding.Resident;
            }
            else if (!value && CharacterSettlementStandingRules
                         .IsSettlementResident(settlementStanding))
            {
                settlementStanding = isVisiting
                    ? CharacterSettlementStanding.Visitor
                    : CharacterSettlementStanding.PreparedCandidate;
            }
        }
    }
    bool ICharacterPopulationProfileState.IsVisiting
    {
        get => isVisiting;
        set
        {
            isVisiting = value;
            if (value)
            {
                settlementStanding = CharacterSettlementStanding.Visitor;
                isStaff = false;
            }
            else if (settlementStanding == CharacterSettlementStanding.Visitor)
            {
                settlementStanding = CharacterSettlementStanding.PreparedCandidate;
            }
        }
    }
    CharacterSettlementStanding ICharacterPopulationProfileState.SettlementStanding
    {
        get => settlementStanding;
        set => settlementStanding = value;
    }
    int ICharacterPopulationProfileState.VisitCount
    {
        get => visitCount;
        set => visitCount = value;
    }

    public WorldCharacterProfile Clone()
    {
        return new WorldCharacterProfile
        {
            persistentId = persistentId,
            characterDataId = characterDataId,
            displayName = displayName,
            origin = origin,
            isOwner = isOwner,
            isStaff = isStaff,
            isAlive = isAlive,
            isVisiting = isVisiting,
            settlementStanding = settlementStanding,
            visitCount = visitCount,
            socialMemory = socialMemory?.Clone() ?? new CharacterSocialMemorySnapshot(),
            level = level,
            currentExperience = currentExperience,
            growth = growth?.Clone() ?? new CharacterGrowthState(),
            narrative = narrative?.Clone() ?? new CharacterNarrativeLedger(),
            nextVisitorExitSequence = Math.Max(1L, nextVisitorExitSequence),
            visitorExitOperation = visitorExitOperation?.Clone()
                ?? new CharacterVisitorExitOperationSaveData(),
            acquiredTraits = (acquiredTraits
                ?? throw new InvalidOperationException(
                    $"World profile '{persistentId}' has no current acquired-trait state."))
                .Clone()
        };
    }
}

public static class CharacterSkillDisplay
{
    public static string Potential(CharacterPotentialGrade grade)
    {
        return grade switch
        {
            CharacterPotentialGrade.Promising => "유망",
            CharacterPotentialGrade.Excellent => "우수",
            CharacterPotentialGrade.Exceptional => "탁월",
            CharacterPotentialGrade.Genius => "천재",
            _ => "평범"
        };
    }

    public static string Rarity(CharacterSkillRarity rarity)
    {
        return rarity switch
        {
            CharacterSkillRarity.Advanced => "고급",
            CharacterSkillRarity.Rare => "희귀",
            CharacterSkillRarity.Heroic => "영웅",
            CharacterSkillRarity.Legendary => "전설",
            _ => "일반"
        };
    }
}
