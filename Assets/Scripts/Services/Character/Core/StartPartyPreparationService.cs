using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public enum StartPartyRerollGroup
{
    Identity,
    Aptitude,
    Skill
}

public enum StartPartyPreparationPhase
{
    OwnerSelect,
    PartyPrepare
}

public sealed class StartPartyMemberPreparation
{
    internal GameObject previewObject;
    internal GameObject prefetchedSkillObject;
    internal CharacterProgression prefetchedSkillProgression;
    internal int rollSerial;
    internal int proficiencySeed;

    public int Index { get; internal set; }
    public int RosterId { get; internal set; }
    public int PartySlot { get; internal set; }
    public bool IsOwner { get; internal set; }
    public bool IsReserve { get; internal set; }
    public bool IsOwnerLocked { get; internal set; }
    public CharacterSO CharacterData { get; internal set; }
    public CharacterProgression Progression { get; internal set; }
    public CharacterSkillSlotProfile SlotProfile { get; internal set; }
    public int IdentityRerollsRemaining { get; internal set; } = 3;
    public int AptitudeRerollsRemaining { get; internal set; } = 3;
    public int SkillRerollsRemaining { get; internal set; } = 3;

    public string RoleLabel => IsOwner ? "\uC0AC\uC7A5" : $"\uC9C1\uC6D0 {Index}";
    public string RosterLabel => IsOwner
        ? "\uC0AC\uC7A5"
        : IsReserve
            ? $"\uC608\uBE44 {Mathf.Max(1, Index - 2)}"
            : $"\uC120\uBC1C {Mathf.Max(1, PartySlot)}";
    public bool HasReadyFirstActive => Progression != null
        && Progression.Drafts.Any(draft => draft != null
            && draft.kind == CharacterSkillKind.Active
            && draft.unlockLevel == 1
            && draft.isReady);
    public bool HasOwnerFixedSkills => IsOwner
        && CharacterOwnerFixedSkillUtility.GetSkills(CharacterData).Count >= CharacterOwnerFixedSkillUtility.FixedSlotCount;
    public bool HasFirstPassive => Progression != null && Progression.PassiveSkills.Count > 0;
    public bool HasSelectedFirstActive => Progression != null && Progression.ActiveSkills.Count > 0;
    public bool IsReadyToStart => IsOwner
        ? HasOwnerFixedSkills
        : HasReadyFirstActive && HasFirstPassive && HasSelectedFirstActive;
}

public interface IStartPartyPreparationService
{
    bool IsPreparing { get; }
    StartPartyPreparationPhase Phase { get; }
    IReadOnlyList<StartPartyMemberPreparation> Members { get; }
    IReadOnlyList<StartPartyMemberPreparation> Roster { get; }
    IReadOnlyList<StartPartyMemberPreparation> Reserves { get; }
    event Action Changed;

    bool Begin(CharacterSO ownerData, out string message);
    bool TrySwapWithReserve(int selectedMemberIndex, int reserveMemberIndex, out string message);
    bool TryFullReroll(int memberIndex, out string message);
    bool TryPartialReroll(int memberIndex, StartPartyRerollGroup group, out string message);
    bool TryChooseFirstActive(int memberIndex, int candidateIndex, out string message);
    bool TryCreatePreparedSnapshot(
        DungeonDifficulty difficulty,
        int runSeed,
        out PreparedStartPartySnapshot snapshot,
        out string message);
    bool TryCreatePreparedSnapshot(
        DungeonDifficulty difficulty,
        DungeonSurvivalPressure survivalPressure,
        int runSeed,
        out PreparedStartPartySnapshot snapshot,
        out string message);
    void Cancel();
}

public sealed class StartPartyPreparationService : IStartPartyPreparationService, IDisposable
{
    private const int PartialRerollCharge = 3;

    private static readonly string[] GivenNames =
    {
        "Arin", "Bora", "Dion", "Leon", "Miru", "Serin", "Yuna", "Haram",
        "Ayun", "Sion", "Ruka", "Noa", "Cain", "Rena", "Ian", "Roma"
    };

    private readonly ICharacterSkillGenerationService skillGenerationService;
    private readonly ICharacterSkillSystemSettingsProvider settingsProvider;
    private readonly IRunCharacterCatalog characterCatalog;
    private readonly IGameContentCatalog content;
    private readonly ICharacterRuntimeProfileFactory runtimeProfileFactory;
    private readonly List<StartPartyMemberPreparation> members = new List<StartPartyMemberPreparation>(7);
    private readonly IReadOnlyList<StartPartyMemberPreparation> membersView;
    private readonly IRandomStream random;

    private CharacterTraitSO[] traitPool;
    private CharacterStartingOriginSO[] startingOrigins;
    private CharacterStartingHistorySO[] startingHistories;
    private AgeConditionDefinitionSO[] ageConditions;
    private CharacterSpeciesSO[] speciesDefinitions;
    private int seedSerial;

    public bool IsPreparing { get; private set; }
    public StartPartyPreparationPhase Phase { get; private set; } = StartPartyPreparationPhase.OwnerSelect;
    public IReadOnlyList<StartPartyMemberPreparation> Members => GetSelectedMembers();
    public IReadOnlyList<StartPartyMemberPreparation> Roster => membersView;
    public IReadOnlyList<StartPartyMemberPreparation> Reserves => members
        .Where(member => member != null && member.IsReserve)
        .OrderBy(member => member.Index)
        .ToArray();
    public event Action Changed;

    public StartPartyPreparationService(
        ICharacterSkillGenerationService skillGenerationService,
        ICharacterSkillSystemSettingsProvider settingsProvider,
        IRunCharacterCatalog characterCatalog,
        IGameContentCatalog content,
        IRandomStreamProvider randomStreams,
        ICharacterRuntimeProfileFactory runtimeProfileFactory)
    {
        this.skillGenerationService = skillGenerationService
            ?? throw new ArgumentNullException(nameof(skillGenerationService));
        this.settingsProvider = settingsProvider
            ?? throw new ArgumentNullException(nameof(settingsProvider));
        this.characterCatalog = characterCatalog
            ?? throw new ArgumentNullException(nameof(characterCatalog));
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.runtimeProfileFactory = runtimeProfileFactory
            ?? throw new ArgumentNullException(nameof(runtimeProfileFactory));
        random = (randomStreams ?? throw new ArgumentNullException(nameof(randomStreams)))
            .Get("character:start-party-preparation");
        membersView = members.AsReadOnly();
    }

    public bool Begin(CharacterSO ownerData, out string message)
    {
        if (ownerData == null || !ownerData.IsOwnerCandidate)
        {
            message = "사장 후보가 올바르지 않습니다.";
            return false;
        }

        CharacterSO[] staffCandidates = characterCatalog.Characters
            .Where(candidate => candidate != null
                && candidate.characterType == CharacterType.Customer
                && string.Equals(candidate.SpeciesTag, ownerData.SpeciesTag, StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => candidate.id)
            .ToArray();
        if (staffCandidates.Length == 0)
        {
            message = $"{ownerData.SpeciesTag} 종족의 직원 후보를 찾지 못했습니다.";
            return false;
        }

        Cancel();
        traitPool ??= content.GetAll<CharacterTraitSO>()
            .Where(trait => trait != null)
            .OrderBy(trait => trait.id)
            .ToArray();
        startingOrigins ??= content.GetAll<CharacterStartingOriginSO>()
            .Where(value => value != null && value.ValidateDefinition().Count == 0)
            .OrderBy(value => value.originId, StringComparer.Ordinal)
            .ToArray();
        startingHistories ??= content.GetAll<CharacterStartingHistorySO>()
            .Where(value => value != null && value.ValidateDefinition().Count == 0)
            .OrderBy(value => value.historyId, StringComparer.Ordinal)
            .ToArray();
        ageConditions ??= content.GetAll<AgeConditionDefinitionSO>()
            .Where(value => value != null && value.ValidateDefinition().Count == 0)
            .OrderBy(value => value.conditionId, StringComparer.Ordinal)
            .ToArray();
        speciesDefinitions ??= content.GetAll<CharacterSpeciesSO>()
            .Where(value => value != null)
            .OrderBy(value => value.speciesTag, StringComparer.Ordinal)
            .ToArray();
        if (startingOrigins.Length == 0 || startingHistories.Length == 0)
        {
            message = "시작 출신 또는 과거 이력 데이터가 없습니다.";
            return false;
        }

        members.Add(CreateMember(0, true, ownerData, partySlot: 0, isReserve: false));
        for (int i = 0; i < 6; i++)
        {
            CharacterSO staffData = staffCandidates[i % staffCandidates.Length];
            int rosterId = i + 1;
            members.Add(CreateMember(
                rosterId,
                false,
                staffData,
                partySlot: i < 2 ? i + 1 : -1,
                isReserve: i >= 2));
        }

        IsPreparing = true;
        Phase = StartPartyPreparationPhase.PartyPrepare;
        message = "시작 파티 준비를 시작했습니다.";
        Changed?.Invoke();
        return true;
    }

    public bool TryFullReroll(int memberIndex, out string message)
    {
        if (!TryGetMember(memberIndex, out StartPartyMemberPreparation member, out message))
        {
            return false;
        }

        CharacterPreparedIdentity identity = RollIdentity(member.CharacterData, member.Index);
        member.proficiencySeed = NextSeed(member);
        CharacterPotentialGrade potential = CharacterGrowthRules.RollPotential(settingsProvider.Settings, random);
        ReplaceCurrentProgression(member, identity, potential);
        member.IdentityRerollsRemaining = PartialRerollCharge;
        member.AptitudeRerollsRemaining = PartialRerollCharge;
        member.SkillRerollsRemaining = PartialRerollCharge;
        message = $"{member.RoleLabel} 전체를 다시 굴렸습니다.";
        Changed?.Invoke();
        return true;
    }

    public bool TryPartialReroll(
        int memberIndex,
        StartPartyRerollGroup group,
        out string message)
    {
        if (!TryGetMember(memberIndex, out StartPartyMemberPreparation member, out message))
        {
            return false;
        }

        switch (group)
        {
            case StartPartyRerollGroup.Identity:
                if (member.IdentityRerollsRemaining <= 0)
                {
                    message = "정체성 리롤 횟수를 모두 썼습니다.";
                    return false;
                }

                member.IdentityRerollsRemaining--;
                ReplaceCurrentProgression(
                    member,
                    RollIdentity(member.CharacterData, member.Index),
                    member.Progression.PotentialGrade);
                message = $"{member.RoleLabel} 정체성을 다시 굴렸습니다.";
                break;

            case StartPartyRerollGroup.Aptitude:
                if (member.AptitudeRerollsRemaining <= 0)
                {
                    message = "재능 리롤 횟수를 모두 썼습니다.";
                    return false;
                }

                member.AptitudeRerollsRemaining--;
                member.proficiencySeed = NextSeed(member);
                CharacterPreparedIdentity aptitudeIdentity =
                    ReadIdentity(member.Progression);
                ApplyStartingProfile(
                    aptitudeIdentity,
                    member.CharacterData,
                    member.Index);
                ReplaceCurrentProgression(
                    member,
                    aptitudeIdentity,
                    CharacterGrowthRules.RollPotential(settingsProvider.Settings, random));
                message = $"{member.RoleLabel} 재능을 다시 굴렸습니다.";
                break;

            case StartPartyRerollGroup.Skill:
                if (member.SkillRerollsRemaining <= 0)
                {
                    message = "스킬 리롤 횟수를 모두 썼습니다.";
                    return false;
                }

                member.SkillRerollsRemaining--;
                UsePrefetchedSkills(member);
                message = $"{member.RoleLabel} 스킬 후보를 다시 굴렸습니다.";
                break;

            default:
                message = "지원하지 않는 리롤 묶음입니다.";
                return false;
        }

        Changed?.Invoke();
        return true;
    }

    public bool TryChooseFirstActive(int memberIndex, int candidateIndex, out string message)
    {
        if (!TryGetMember(memberIndex, out StartPartyMemberPreparation member, out message))
        {
            return false;
        }

        bool selected = member.Progression.TryChooseActiveSkill(
            unlockLevel: 1,
            candidateIndex,
            confirmed: true,
            out message);
        Changed?.Invoke();
        return selected;
    }

    public bool TrySwapWithReserve(int selectedMemberIndex, int reserveMemberIndex, out string message)
    {
        if (!TryGetMember(selectedMemberIndex, out StartPartyMemberPreparation selected, out message)
            || !TryGetMember(reserveMemberIndex, out StartPartyMemberPreparation reserve, out message))
        {
            return false;
        }

        if (selected.IsOwner || selected.IsReserve || !reserve.IsReserve)
        {
            message = "선발 직원과 예비 직원만 교체할 수 있습니다.";
            return false;
        }

        string incomingName = !string.IsNullOrWhiteSpace(reserve.Progression?.GrowthState?.displayName)
            ? reserve.Progression.GrowthState.displayName
            : reserve.RosterLabel;
        int selectedSlot = selected.PartySlot;
        selected.PartySlot = -1;
        selected.IsReserve = true;
        reserve.PartySlot = selectedSlot;
        reserve.IsReserve = false;
        message = $"선발 {selectedSlot}번에 {incomingName}을 배치했습니다.";
        Changed?.Invoke();
        return true;
    }

    public bool TryCreatePreparedSnapshot(
        DungeonDifficulty difficulty,
        int runSeed,
        out PreparedStartPartySnapshot snapshot,
        out string message)
    {
        return TryCreatePreparedSnapshot(
            difficulty,
            DungeonSurvivalPressure.Standard,
            runSeed,
            out snapshot,
            out message);
    }

    public bool TryCreatePreparedSnapshot(
        DungeonDifficulty difficulty,
        DungeonSurvivalPressure survivalPressure,
        int runSeed,
        out PreparedStartPartySnapshot snapshot,
        out string message)
    {
        snapshot = null;
        IReadOnlyList<StartPartyMemberPreparation> selectedMembers = GetSelectedMembers();
        if (!IsPreparing || selectedMembers.Count != 3)
        {
            message = "시작 파티가 아직 준비되지 않았습니다.";
            return false;
        }

        StartPartyMemberPreparation incomplete = selectedMembers.FirstOrDefault(member => !member.IsReadyToStart);
        if (incomplete != null)
        {
            message = $"{incomplete.RosterLabel}의 첫 액티브와 패시브 준비가 필요합니다.";
            return false;
        }

        PreparedStartPartyMemberSnapshot owner = CreateSnapshotMember(
            selectedMembers[0],
            persistentId: "owner");
        List<PreparedStartPartyMemberSnapshot> staff = selectedMembers
            .Skip(1)
            .Select((member, index) => CreateSnapshotMember(
                member,
                persistentId: CharacterId.FromStableSuffix(
                    $"staff:{runSeed}:{index + 1:D2}").Value))
            .ToList();
        snapshot = new PreparedStartPartySnapshot
        {
            difficulty = difficulty,
            survivalPressure = DungeonSurvivalPressureRules.Normalize(
                (int)survivalPressure),
            runSeed = runSeed,
            owner = owner,
            staff = staff
        };
        message = "준비한 파티 스냅샷이 완성됐습니다.";
        return true;
    }

    public void Cancel()
    {
        CleanupPreviews();
        members.Clear();
        IsPreparing = false;
        Phase = StartPartyPreparationPhase.OwnerSelect;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        Cancel();
    }

    private StartPartyMemberPreparation CreateMember(
        int index,
        bool isOwner,
        CharacterSO data,
        int partySlot,
        bool isReserve)
    {
        StartPartyMemberPreparation member = new StartPartyMemberPreparation
        {
            Index = index,
            RosterId = index,
            PartySlot = partySlot,
            IsOwner = isOwner,
            IsReserve = isReserve,
            IsOwnerLocked = isOwner,
            CharacterData = data,
            SlotProfile = CharacterSkillSlotProfile.For(data, isOwner)
        };
        CharacterPreparedIdentity identity = RollIdentity(data, index);
        member.proficiencySeed = NextSeed(member);
        ReplaceCurrentProgression(
            member,
            identity,
            CharacterGrowthRules.RollPotential(settingsProvider.Settings, random));
        return member;
    }

    private IReadOnlyList<StartPartyMemberPreparation> GetSelectedMembers()
    {
        return members
            .Where(member => member != null && !member.IsReserve)
            .OrderBy(member => member.PartySlot)
            .ThenBy(member => member.Index)
            .ToArray();
    }

    private static PreparedStartPartyMemberSnapshot CreateSnapshotMember(
        StartPartyMemberPreparation member,
        string persistentId)
    {
        CharacterProgressionSnapshot progressionSnapshot =
            member.Progression.CapturePersistentState();
        CharacterGrowthState growth = progressionSnapshot.GrowthState?.Clone()
            ?? new CharacterGrowthState();
        string displayName = !string.IsNullOrWhiteSpace(growth.displayName)
            ? growth.displayName
            : member.CharacterData != null
                ? member.CharacterData.characterName
                : member.RosterLabel;
        return new PreparedStartPartyMemberSnapshot
        {
            rosterId = member.RosterId,
            partySlot = member.PartySlot,
            isOwner = member.IsOwner,
            characterDataId = member.CharacterData != null ? member.CharacterData.id : -1,
            persistentId = persistentId ?? string.Empty,
            displayName = displayName,
            level = progressionSnapshot.Level,
            currentExperience = progressionSnapshot.CurrentExperience,
            growth = growth,
            narrative = progressionSnapshot.NarrativeLedger?.Clone()
                ?? new CharacterNarrativeLedger()
        };
    }

    private void ReplaceCurrentProgression(
        StartPartyMemberPreparation member,
        CharacterPreparedIdentity identity,
        CharacterPotentialGrade potential)
    {
        DestroyPreview(member.Progression, member.previewObject);
        DestroyPreview(member.prefetchedSkillProgression, member.prefetchedSkillObject);
        member.previewObject = CreatePreviewObject($"StartParty_{member.Index}_Current", out CharacterProgression progression);
        member.Progression = progression;
        progression.DraftReady += _ => Changed?.Invoke();
        progression.Changed += HandleProgressionChanged;
        ApplyRoll(progression, member, identity, potential);
        TryBeginSkillPrefetch(member);
    }

    private void UsePrefetchedSkills(StartPartyMemberPreparation member)
    {
        CharacterPreparedIdentity identity = ReadIdentity(member.Progression);
        CharacterPotentialGrade potential = member.Progression.PotentialGrade;
        DestroyPreview(member.Progression, member.previewObject);

        if (member.prefetchedSkillProgression != null)
        {
            member.Progression = member.prefetchedSkillProgression;
            member.previewObject = member.prefetchedSkillObject;
            member.prefetchedSkillProgression = null;
            member.prefetchedSkillObject = null;
            member.Progression.DraftReady += _ => Changed?.Invoke();
            member.Progression.Changed += HandleProgressionChanged;
        }
        else
        {
            member.previewObject = CreatePreviewObject($"StartParty_{member.Index}_Current", out CharacterProgression progression);
            member.Progression = progression;
            progression.DraftReady += _ => Changed?.Invoke();
            progression.Changed += HandleProgressionChanged;
            ApplyRoll(progression, member, identity, potential);
        }

        TryBeginSkillPrefetch(member);
    }

    private void TryBeginSkillPrefetch(StartPartyMemberPreparation member)
    {
        if (member == null
            || member.Progression == null
            || !member.HasReadyFirstActive
            || !member.HasFirstPassive
            || member.prefetchedSkillProgression != null)
        {
            return;
        }

        DestroyPreview(member.prefetchedSkillProgression, member.prefetchedSkillObject);
        member.prefetchedSkillObject = CreatePreviewObject(
            $"StartParty_{member.Index}_Prefetch",
            out CharacterProgression progression);
        member.prefetchedSkillProgression = progression;
        ApplyRoll(
            progression,
            member,
            ReadIdentity(member.Progression),
            member.Progression.PotentialGrade);
    }

    private void ApplyRoll(
        CharacterProgression progression,
        StartPartyMemberPreparation member,
        CharacterPreparedIdentity identity,
        CharacterPotentialGrade potential)
    {
        member.rollSerial++;
        progression.ApplyPreparedIdentity(
            identity.displayName,
            identity.origin,
            identity.traitIds,
            potential,
            NextSeed(member),
            autoChooseDrafts: false,
            startingProficiencySeed: member.proficiencySeed,
            startingProfile: identity.startingProfile,
            preparedStartingProficiencies: identity.startingProficiencies);
        EnsureGeneratedStartingSkills(member, progression);
    }

    private void EnsureGeneratedStartingSkills(
        StartPartyMemberPreparation member,
        CharacterProgression progression)
    {
        if (member == null || member.IsOwner || progression == null)
        {
            return;
        }

        skillGenerationService.CancelRequests(progression);
        CharacterSkillDraft activeDraft = EnsurePreparedDraft(
            progression,
            CharacterSkillKind.Active,
            1);
        EnsurePreparedDraftCandidates(member, progression, activeDraft, 1);
        if (!activeDraft.permanentlyChosen)
        {
            progression.OnDraftReady(activeDraft);
            int selectedIndex = Mathf.Clamp(
                ChoosePreparedActiveCandidate(progression, activeDraft),
                0,
                Mathf.Max(0, activeDraft.candidates.Count - 1));
            progression.TryChooseActiveSkill(
                activeDraft.unlockLevel,
                selectedIndex,
                confirmed: true,
                out _);
        }

        CharacterSkillDraft passiveDraft = EnsurePreparedDraft(
            progression,
            CharacterSkillKind.Passive,
            1);
        EnsurePreparedDraftCandidates(member, progression, passiveDraft, 1);
        if (!passiveDraft.permanentlyChosen)
        {
            progression.OnDraftReady(passiveDraft);
        }
    }

    private CharacterSkillDraft EnsurePreparedDraft(
        CharacterProgression progression,
        CharacterSkillKind kind,
        int unlockLevel)
    {
        CharacterSkillDraft draft = progression.GrowthState.drafts.FirstOrDefault(item => item != null
            && item.kind == kind
            && item.unlockLevel == unlockLevel);
        if (draft != null)
        {
            return draft;
        }

        draft = skillGenerationService.CreateDraft(
            progression,
            kind,
            unlockLevel,
            progression.GrowthState.skillGenerationRevision);
        progression.GrowthState.drafts.Add(draft);
        return draft;
    }

    private void EnsurePreparedDraftCandidates(
        StartPartyMemberPreparation member,
        CharacterProgression progression,
        CharacterSkillDraft draft,
        int candidateCount)
    {
        if (draft == null)
        {
            return;
        }

        int count = Mathf.Max(1, candidateCount);
        if (draft.isReady && draft.candidates != null && draft.candidates.Count >= count)
        {
            draft.requestSubmitted = false;
            progression.MarkGenerationRequestCompleted(draft.requestKey);
            return;
        }

        draft.candidates = new List<CharacterSkillInstance>(count);
        for (int i = 0; i < count; i++)
        {
            draft.candidates.Add(CreatePreparedSkill(member, progression, draft, i));
        }

        draft.isReady = true;
        draft.requestSubmitted = false;
        progression.MarkGenerationRequestCompleted(draft.requestKey);
    }

    private CharacterSkillInstance CreatePreparedSkill(
        StartPartyMemberPreparation member,
        CharacterProgression progression,
        CharacterSkillDraft draft,
        int candidateIndex)
    {
        CharacterSkillCandidateRule rule = draft.rules != null && draft.rules.Count > 0
            ? draft.rules[Mathf.Clamp(candidateIndex, 0, draft.rules.Count - 1)]
            : CreateFallbackRule(draft.kind);
        List<CharacterSkillModuleSelection> modules = ResolvePreparedModules(rule, draft.kind, candidateIndex);
        CharacterSkillFormationRules.Resolve(
            rule.target,
            modules,
            out OffenseFormationMask usableFrom,
            out OffenseFormationMask targetPositions);

        string characterName = !string.IsNullOrWhiteSpace(progression.GrowthState.displayName)
            ? progression.GrowthState.displayName.Trim()
            : ResolveMemberName(member);
        string moduleId = modules.FirstOrDefault()?.moduleId ?? string.Empty;
        string displayName = BuildPreparedSkillName(draft.kind, moduleId);
        string description = BuildPreparedSkillDescription(characterName, draft.kind, moduleId, rule);
        string reason = BuildPreparedSkillReason(characterName, draft.kind);
        return new CharacterSkillInstance
        {
            id = $"{draft.requestKey}:prepared:{candidateIndex}",
            displayName = displayName,
            description = description,
            narrativeReason = reason,
            kind = draft.kind,
            rarity = rule.rarity,
            trigger = rule.trigger,
            target = rule.target,
            ultimateDomain = CharacterUltimateDomain.None,
            cooldownTurns = draft.kind == CharacterSkillKind.Active ? 1 : 0,
            usableFrom = usableFrom,
            targetPositions = targetPositions,
            modules = modules,
            requestKey = draft.requestKey
        };
    }

    private CharacterSkillCandidateRule CreateFallbackRule(CharacterSkillKind kind)
    {
        CharacterSkillTarget target = kind == CharacterSkillKind.Active
            ? CharacterSkillTarget.Enemy
            : CharacterSkillTarget.Self;
        CharacterSkillTrigger trigger = kind == CharacterSkillKind.Active
            ? CharacterSkillTrigger.ManualCombat
            : CharacterSkillTrigger.WorkStarted;
        CharacterSkillCandidateRule rule = new CharacterSkillCandidateRule
        {
            rarity = kind == CharacterSkillKind.Active
                ? CharacterSkillRarity.Common
                : CharacterSkillRarity.Advanced,
            budget = settingsProvider.Settings.GetBudget(kind == CharacterSkillKind.Active
                ? CharacterSkillRarity.Common
                : CharacterSkillRarity.Advanced),
            trigger = trigger,
            target = target
        };
        foreach (CharacterSkillModuleRule module in settingsProvider.Settings.Modules
            .Where(module => module != null
                && module.Allows(kind, trigger, target)
                && CharacterSkillValidation.IsTargetCompatible(module.id, target)
                && !CharacterSkillValidation.WouldSelfTrigger(module.id, trigger)))
        {
            rule.allowedModuleIds.Add(module.id);
            foreach (CharacterSkillNumericVariant variant in module.variants ?? new List<CharacterSkillNumericVariant>())
            {
                if (variant != null && variant.cost <= rule.budget)
                {
                    rule.allowedVariantIds.Add(variant.id);
                }
            }
        }

        CharacterSkillFormationRules.Resolve(
            target,
            Array.Empty<CharacterSkillModuleSelection>(),
            out rule.usableFrom,
            out rule.targetPositions);
        return rule;
    }

    private List<CharacterSkillModuleSelection> ResolvePreparedModules(
        CharacterSkillCandidateRule rule,
        CharacterSkillKind kind,
        int candidateIndex)
    {
        List<CharacterSkillAllowedCombination> combinations =
            CharacterSkillCombinationCatalog.Build(rule, settingsProvider.Settings, kind);
        if (combinations.Count > 0)
        {
            return combinations[Mathf.Abs(candidateIndex) % combinations.Count]
                .Modules
                .Where(module => module != null)
                .Select(module => module.Clone())
                .ToList();
        }

        CharacterSkillModuleRule selectedModule = settingsProvider.Settings.Modules
            .Where(module => module != null
                && rule.allowedModuleIds.Contains(module.id, StringComparer.Ordinal)
                && module.Allows(kind, rule.trigger, rule.target))
            .OrderBy(module => PreferredPreparedModuleOrder(module.id, kind))
            .ThenBy(module => module.id, StringComparer.Ordinal)
            .FirstOrDefault();
        CharacterSkillNumericVariant selectedVariant = selectedModule?.variants
            .Where(variant => variant != null
                && rule.allowedVariantIds.Contains(variant.id, StringComparer.Ordinal)
                && variant.cost <= rule.budget)
            .OrderBy(variant => variant.cost)
            .FirstOrDefault();
        if (selectedModule != null && selectedVariant != null)
        {
            return new List<CharacterSkillModuleSelection>
            {
                new CharacterSkillModuleSelection
                {
                    moduleId = selectedModule.id,
                    variantId = selectedVariant.id
                }
            };
        }

        return new List<CharacterSkillModuleSelection>
        {
            new CharacterSkillModuleSelection
            {
                moduleId = kind == CharacterSkillKind.Active ? "damage" : "work_speed",
                variantId = kind == CharacterSkillKind.Active ? "light" : "small"
            }
        };
    }

    private static int PreferredPreparedModuleOrder(string moduleId, CharacterSkillKind kind)
    {
        if (kind == CharacterSkillKind.Active)
        {
            return moduleId switch
            {
                "damage" => 0,
                "delay" => 1,
                "guard" => 2,
                "heal" => 3,
                _ => 10
            };
        }

        return moduleId switch
        {
            "work_speed" => 0,
            "research" => 1,
            "mood" => 2,
            "cleaning" => 3,
            "repair" => 4,
            _ => 10
        };
    }

    private static int ChoosePreparedActiveCandidate(
        CharacterProgression progression,
        CharacterSkillDraft draft)
    {
        if (draft?.candidates == null || draft.candidates.Count == 0)
        {
            return 0;
        }

        int bestIndex = 0;
        float bestScore = float.MinValue;
        for (int i = 0; i < draft.candidates.Count; i++)
        {
            CharacterSkillInstance candidate = draft.candidates[i];
            float score = candidate != null ? (int)candidate.rarity * 100f : 0f;
            foreach (CharacterSkillModuleSelection module in candidate?.modules ?? new List<CharacterSkillModuleSelection>())
            {
                score += 1f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static string BuildPreparedSkillName(CharacterSkillKind kind, string moduleId)
    {
        if (kind == CharacterSkillKind.Passive)
        {
            return moduleId switch
            {
                "research" => "연구 습관",
                "mood" => "가벼운 숨",
                "cleaning" => "정돈 감각",
                "repair" => "손끝 수리",
                _ => "부지런함"
            };
        }

        return moduleId switch
        {
            "delay" => "흐름 끊기",
            "guard" => "앞막기",
            "heal" => "응급 손길",
            "dot" => "깊은 상처",
            _ => "첫 일격"
        };
    }

    private static string BuildPreparedSkillDescription(
        string characterName,
        CharacterSkillKind kind,
        string moduleId,
        CharacterSkillCandidateRule rule)
    {
        string name = string.IsNullOrWhiteSpace(characterName) ? "이 인물" : characterName;
        if (kind == CharacterSkillKind.Passive)
        {
            return moduleId switch
            {
                "research" => $"{name}은 작업을 마칠 때 연구 정리를 조금 더 잘한다.",
                "mood" => $"{name}은 일이 풀릴 때 짧은 기분 회복을 얻는다.",
                "cleaning" => $"{name}은 작업 사이에 주변을 조금 더 말끔히 둔다.",
                "repair" => $"{name}은 손상된 시설을 다룰 때 수리 감각이 붙는다.",
                _ => $"{name}은 일을 시작할 때 작업 흐름을 조금 더 빨리 잡는다."
            };
        }

        return rule.target switch
        {
            CharacterSkillTarget.Self => $"{name}은 전투 중 자신을 가다듬는 초기 전술을 쓴다.",
            CharacterSkillTarget.Ally => $"{name}은 전투 중 아군 하나를 짧게 지원한다.",
            _ => $"{name}은 전투 중 적 하나를 겨냥하는 기본 전술을 쓴다."
        };
    }

    private static string BuildPreparedSkillReason(string characterName, CharacterSkillKind kind)
    {
        string name = string.IsNullOrWhiteSpace(characterName) ? "이 인물" : characterName;
        return kind == CharacterSkillKind.Passive
            ? $"{name}의 출신과 성향이 초반 습관으로 굳어졌다."
            : $"{name}의 첫 전투 감각이 초기 액티브로 자리 잡았다.";
    }

    private static string ResolveMemberName(StartPartyMemberPreparation member)
    {
        string preparedName = member?.Progression?.GrowthState?.displayName;
        if (!string.IsNullOrWhiteSpace(preparedName))
        {
            return preparedName;
        }

        return member?.CharacterData != null
            ? member.CharacterData.characterName
            : "인물";
    }

    private GameObject CreatePreviewObject(string objectName, out CharacterProgression progression)
    {
        GameObject preview = new GameObject(objectName);
        preview.hideFlags = HideFlags.HideAndDontSave;
        progression = preview.AddComponent<CharacterProgression>();
        progression.ConfigurePreview(
            skillGenerationService,
            settingsProvider,
            new CharacterProgressionProfileProjector(
                content,
                runtimeProfileFactory));
        progression.SetPublicSkillNotificationsSuppressed(true);
        return preview;
    }

    private CharacterPreparedIdentity RollIdentity(CharacterSO data, int memberIndex)
    {
        List<int> traitIds = CharacterTraitSelectionRules.Select(
                traitPool,
                settingsProvider.Settings.traitConflicts,
                random,
                data?.SpeciesTag)
            .ToList();

        string baseName = GivenNames[random.NextInt(0, GivenNames.Length)];
        string displayName = members.Any(member => member.Progression != null
                && string.Equals(member.Progression.GrowthState.displayName, baseName, StringComparison.Ordinal))
            ? $"{baseName}{memberIndex + 1}"
            : baseName;
        CharacterPreparedIdentity identity = new CharacterPreparedIdentity
        {
            displayName = displayName,
            traitIds = traitIds
        };
        ApplyStartingProfile(identity, data, memberIndex);
        return identity;
    }

    private void ApplyStartingProfile(
        CharacterPreparedIdentity identity,
        CharacterSO data,
        int memberIndex)
    {
        if (identity == null || data == null)
            throw new ArgumentNullException(identity == null
                ? nameof(identity)
                : nameof(data));
        CharacterSpeciesSO species = speciesDefinitions.FirstOrDefault(value =>
            string.Equals(
                value.speciesTag,
                data.SpeciesTag,
                StringComparison.OrdinalIgnoreCase));
        if (species?.lifeHistory == null)
        {
            throw new InvalidOperationException(
                $"Starting profile requires a life history for '{data.SpeciesTag}'.");
        }

        CharacterStartingOriginSO origin = startingOrigins[
            random.NextInt(0, startingOrigins.Length)];
        CharacterStartingHistorySO history = startingHistories[
            random.NextInt(0, startingHistories.Length)];
        int profileSeed = CharacterGrowthRules.StableHash(
            $"founder-profile:{memberIndex}:{seedSerial}:{random.NextInt(0, int.MaxValue)}");
        CharacterStartingProfileRoll roll = CharacterStartingProfileRules.Create(
            profileSeed,
            new CharacterStartingLifeHistory(
                species.lifeHistory.adultAgeYears,
                species.lifeHistory.elderAgeYears,
                species.lifeHistory.untreatedExpectedLifeYears,
                species.lifeHistory.construct),
            origin,
            history,
            ageConditions.Select(value => new CharacterStartingAgeCondition(
                    value.conditionId,
                    value.constructCondition))
                .ToArray());
        identity.origin = $"{data.SpeciesTag} · {origin.displayName} · {history.displayName}";
        identity.startingProfile = roll.Profile.Clone();
        identity.startingProficiencies = roll.Proficiencies
            .Select(value => value.Clone())
            .ToList();
        CharacterTraitSO[] selectedTraits = (identity.traitIds ?? new List<int>())
            .Select(traitId => traitPool.FirstOrDefault(value => value.id == traitId)
                ?? throw new InvalidOperationException(
                    $"MissingFounderTraitId: founder slot {memberIndex} references trait {traitId}."))
            .ToArray();
        CharacterTraitStartingProficiencyRules.Apply(
            identity.startingProficiencies,
            selectedTraits,
            roll.Profile.proficiencyCap);
    }

    private bool TryGetMember(
        int memberIndex,
        out StartPartyMemberPreparation member,
        out string message)
    {
        member = members.FirstOrDefault(candidate => candidate.Index == memberIndex);
        if (!IsPreparing || member == null || member.Progression == null)
        {
            message = "준비 중인 캐릭터를 찾지 못했습니다.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private int NextSeed(StartPartyMemberPreparation member)
    {
        seedSerial++;
        return CharacterGrowthRules.StableHash(
            $"start:{member.Index}:{member.rollSerial}:{seedSerial}:{random.NextInt(0, int.MaxValue)}");
    }

    private static CharacterPreparedIdentity ReadIdentity(CharacterProgression progression)
    {
        return new CharacterPreparedIdentity
        {
            displayName = progression.GrowthState.displayName,
            origin = progression.GrowthState.origin,
            traitIds = progression.GrowthState.traitIds.ToList(),
            startingProfile = progression.GrowthState.startingProfile?.Clone()
                ?? new CharacterStartingProfileState(),
            startingProficiencies = progression.GrowthState.startingProficiencies
                .Where(value => value != null)
                .Select(value => value.Clone())
                .ToList()
        };
    }

    private void HandleProgressionChanged()
    {
        foreach (StartPartyMemberPreparation member in members)
        {
            TryBeginSkillPrefetch(member);
        }

        Changed?.Invoke();
    }

    private void CleanupPreviews()
    {
        foreach (StartPartyMemberPreparation member in members)
        {
            DestroyPreview(member.Progression, member.previewObject);
            DestroyPreview(member.prefetchedSkillProgression, member.prefetchedSkillObject);
            member.Progression = null;
            member.previewObject = null;
            member.prefetchedSkillProgression = null;
            member.prefetchedSkillObject = null;
        }
    }

    private void DestroyPreview(CharacterProgression progression, GameObject preview)
    {
        if (progression != null)
        {
            progression.Changed -= HandleProgressionChanged;
            skillGenerationService.CancelRequests(progression);
        }

        if (preview != null)
        {
            UnityEngine.Object.Destroy(preview);
        }
    }

    private sealed class CharacterPreparedIdentity
    {
        public string displayName;
        public string origin;
        public List<int> traitIds = new List<int>();
        public CharacterStartingProfileState startingProfile =
            new CharacterStartingProfileState();
        public List<CharacterStartingProficiencyExperience>
            startingProficiencies = new();
    }
}
