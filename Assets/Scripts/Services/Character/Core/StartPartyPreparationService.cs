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
    bool TryGetCandidateLivingSummary(
        int memberIndex,
        out StartPartyCandidateLivingSummary summary,
        out string message);
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
    private const string CurrentPreviewIdentityKind = "current";
    private const string PrefetchPreviewIdentityKind = "prefetch";

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
    private readonly ICharacterNeedDefinitionCatalog needDefinitionCatalog;
    private readonly IStartingOwnerTraitCountBonusQuery ownerTraitCountBonusQuery;
    private readonly List<StartPartyMemberPreparation> members = new List<StartPartyMemberPreparation>(7);
    private readonly IReadOnlyList<StartPartyMemberPreparation> membersView;
    private readonly IRandomStream random;

    private CharacterTraitSO[] traitPool;
    private CharacterStartingOriginSO[] startingOrigins;
    private CharacterStartingHistorySO[] startingHistories;
    private AgeConditionDefinitionSO[] ageConditions;
    private CharacterSpeciesSO[] speciesDefinitions;
    private int seedSerial;
    private int capturedOwnerTraitCountBonus;

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
        ICharacterRuntimeProfileFactory runtimeProfileFactory,
        ICharacterNeedDefinitionCatalog needDefinitionCatalog,
        IStartingOwnerTraitCountBonusQuery ownerTraitCountBonusQuery)
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
        this.needDefinitionCatalog = needDefinitionCatalog
            ?? throw new ArgumentNullException(nameof(needDefinitionCatalog));
        this.ownerTraitCountBonusQuery = ownerTraitCountBonusQuery
            ?? throw new ArgumentNullException(nameof(ownerTraitCountBonusQuery));
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

        if (!ownerTraitCountBonusQuery.TryGetStartingOwnerTraitCountBonus(
                out int ownerTraitCountBonus,
                out string bonusFailure))
        {
            message = "사장 특성 계승 강화 정보를 읽을 수 없습니다: "
                + bonusFailure;
            return false;
        }

        if (ownerTraitCountBonus is < 0 or > 1)
        {
            message = "사장 특성 계승 강화 값이 허용 범위를 벗어났습니다: "
                + ownerTraitCountBonus;
            return false;
        }

        Cancel();
        capturedOwnerTraitCountBonus = ownerTraitCountBonus;
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

        CharacterPreparedIdentity identity = RollIdentity(
            member.CharacterData,
            member.Index,
            GetTraitCountBonus(member));
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
                    RollIdentity(
                        member.CharacterData,
                        member.Index,
                        GetTraitCountBonus(member)),
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

    public bool TryGetCandidateLivingSummary(
        int memberIndex,
        out StartPartyCandidateLivingSummary summary,
        out string message)
    {
        summary = default;
        if (!TryGetMember(memberIndex, out StartPartyMemberPreparation member, out message))
        {
            return false;
        }

        try
        {
            summary = StartPartyCandidateLivingSummaryProjector.Create(
                member,
                ageConditions,
                runtimeProfileFactory,
                needDefinitionCatalog);
            message = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            message = $"{member.RosterLabel}의 생활 정보를 구성하지 못했습니다: {exception.Message}";
            return false;
        }
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
        CancelStartingSkillRequests(selected);
        ResumeStartingSkillRequests(reserve);
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
        capturedOwnerTraitCountBonus = 0;
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
        CharacterPreparedIdentity identity = RollIdentity(
            data,
            index,
            isOwner ? capturedOwnerTraitCountBonus : 0);
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
        if (!member.IsOwner)
        {
            // Starting staff commit their generated first active immediately, but
            // later gameplay unlocks remain player choices.
            growth.autoChooseDrafts = false;
        }
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
        member.previewObject = CreatePreviewObject(
            $"StartParty_{member.Index}_Current",
            member,
            CurrentPreviewIdentityKind,
            out CharacterProgression progression);
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
            member.previewObject = CreatePreviewObject(
                $"StartParty_{member.Index}_Current",
                member,
                CurrentPreviewIdentityKind,
                out CharacterProgression progression);
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
            || member.IsOwner
            || member.IsReserve
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
            member,
            PrefetchPreviewIdentityKind,
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
        member.rollSerial = checked(member.rollSerial + 1);
        progression.ApplyPreparedIdentity(
            identity.displayName,
            identity.origin,
            identity.traitIds,
            potential,
            NextSeed(member),
            autoChooseDrafts: !member.IsOwner,
            startingProficiencySeed: member.proficiencySeed,
            startingProfile: identity.startingProfile,
            preparedStartingProficiencies: identity.startingProficiencies,
            maximumTraitCount: 4 + GetTraitCountBonus(member),
            ensureInitialDrafts: false);
        EnsureGeneratedStartingSkills(member, progression);
    }

    private void EnsureGeneratedStartingSkills(
        StartPartyMemberPreparation member,
        CharacterProgression progression)
    {
        if (member == null || member.IsOwner || member.IsReserve || progression == null)
        {
            return;
        }

        skillGenerationService.CancelRequests(progression);
        CharacterSkillDraft activeDraft = EnsurePreparedDraft(
            progression,
            CharacterSkillKind.Active,
            1);
        if (!activeDraft.permanentlyChosen && !activeDraft.isReady)
            skillGenerationService.RequestDraft(progression, activeDraft);

        CharacterSkillDraft passiveDraft = EnsurePreparedDraft(
            progression,
            CharacterSkillKind.Passive,
            1);
        if (!passiveDraft.permanentlyChosen && !passiveDraft.isReady)
            skillGenerationService.RequestDraft(progression, passiveDraft);
    }

    private void CancelStartingSkillRequests(StartPartyMemberPreparation member)
    {
        if (member?.Progression != null)
        {
            skillGenerationService.CancelRequests(member.Progression);
        }

        if (member?.prefetchedSkillProgression != null)
        {
            skillGenerationService.CancelRequests(member.prefetchedSkillProgression);
        }
    }

    private void ResumeStartingSkillRequests(StartPartyMemberPreparation member)
    {
        if (member == null || member.IsOwner || member.IsReserve)
        {
            return;
        }

        EnsureGeneratedStartingSkills(member, member.Progression);
        EnsureGeneratedStartingSkills(member, member.prefetchedSkillProgression);
        TryBeginSkillPrefetch(member);
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

    private GameObject CreatePreviewObject(
        string objectName,
        StartPartyMemberPreparation member,
        string previewIdentityKind,
        out CharacterProgression progression)
    {
        if (member?.CharacterData == null)
        {
            throw new InvalidOperationException(
                "A start-party preview requires authored character data.");
        }
        if (string.IsNullOrWhiteSpace(previewIdentityKind))
        {
            throw new ArgumentException(
                "A start-party preview identity kind is required.",
                nameof(previewIdentityKind));
        }

        GameObject preview = new GameObject(objectName);
        preview.hideFlags = HideFlags.HideAndDontSave;
        preview.SetActive(false);

        CharacterActor actor = preview.AddComponent<CharacterActor>();
        actor.PrepareForComposition();
        actor.EnsureRuntimeState();
        progression = actor.Progression
            ?? throw new InvalidOperationException(
                "Start-party preview actor composition did not provide progression.");
        progression.ConfigurePreview(
            skillGenerationService,
            settingsProvider,
            new CharacterProgressionProfileProjector(
                content,
                runtimeProfileFactory));
        actor.Identity.SetData(
            member.CharacterData,
            runtimeProfileFactory.Create(
                CharacterSpawnRequest.FromAuthoring(member.CharacterData)));
        if (!member.IsOwner)
        {
            int nextRollSerial = checked(member.rollSerial + 1);
            actor.Identity.SetPersistentId(CharacterId.FromStableSuffix(
                FormattableString.Invariant(
                    $"start-party-preview:{member.RosterId:D2}:{nextRollSerial:D4}:{previewIdentityKind}")));
        }
        progression.SetPublicSkillNotificationsSuppressed(true);
        return preview;
    }

    private CharacterPreparedIdentity RollIdentity(
        CharacterSO data,
        int memberIndex,
        int traitCountBonus)
    {
        List<int> traitIds = CharacterTraitSelectionRules.Select(
                traitPool,
                settingsProvider.Settings.traitConflicts,
                random,
                data?.SpeciesTag,
                maximumCount: 4 + traitCountBonus,
                traitCountBonus: traitCountBonus)
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

    private int GetTraitCountBonus(StartPartyMemberPreparation member)
    {
        return member != null && member.IsOwner
            ? capturedOwnerTraitCountBonus
            : 0;
    }
}
