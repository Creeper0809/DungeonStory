using System;
using System.Collections.Generic;
using DungeonStory.Characters;
using VContainer;

public sealed class CharacterPopulationRestoreCandidate
{
    private readonly CharacterPopulationApplicationAdapter owner;
    private CharacterPopulationDomain<WorldCharacterProfile> state;

    internal CharacterPopulationRestoreCandidate(
        CharacterPopulationApplicationAdapter owner,
        CharacterPopulationDomain<WorldCharacterProfile> state)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.state = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal CharacterPopulationDomain<WorldCharacterProfile> Peek(
        CharacterPopulationApplicationAdapter expectedOwner)
    {
        if (!ReferenceEquals(owner, expectedOwner) || state == null)
        {
            throw new InvalidOperationException(
                "Character population restore candidate has the wrong owner or was already applied.");
        }

        return state;
    }

    internal void Consume(
        CharacterPopulationApplicationAdapter expectedOwner,
        CharacterPopulationDomain<WorldCharacterProfile> expectedState)
    {
        if (!ReferenceEquals(owner, expectedOwner)
            || state == null
            || !ReferenceEquals(state, expectedState))
        {
            throw new InvalidOperationException(
                "Character population restore candidate has the wrong owner or was already applied.");
        }

        state = null;
    }
}

public sealed class CharacterPopulationRestoreTransaction
{
    private readonly CharacterPopulationApplicationAdapter owner;
    private Action rollback;
    private Action complete;

    internal CharacterPopulationRestoreTransaction(
        CharacterPopulationApplicationAdapter owner,
        Action rollback,
        Action complete)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.rollback = rollback ?? throw new ArgumentNullException(nameof(rollback));
        this.complete = complete ?? throw new ArgumentNullException(nameof(complete));
    }

    internal void Rollback(CharacterPopulationApplicationAdapter expectedOwner)
    {
        Action action = RequireActive(expectedOwner, rollback);
        action();
        rollback = null;
        complete = null;
    }

    internal void Complete(CharacterPopulationApplicationAdapter expectedOwner)
    {
        Action action = RequireActive(expectedOwner, complete);
        action();
        complete = null;
        rollback = null;
    }

    private Action RequireActive(
        CharacterPopulationApplicationAdapter expectedOwner,
        Action action)
    {
        if (!ReferenceEquals(owner, expectedOwner) || action == null)
        {
            throw new InvalidOperationException(
                "Character population restore transaction has the wrong owner or is already finished.");
        }

        return action;
    }
}

public sealed class CharacterSettlementStandingTransaction
{
    internal CharacterSettlementStandingTransaction(
        CharacterActor actor,
        WorldCharacterProfile profile,
        CharacterSettlementStanding previousStanding,
        bool profileWasCreated)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        PreviousStanding = previousStanding;
        ProfileWasCreated = profileWasCreated;
    }

    internal CharacterActor Actor { get; }
    internal WorldCharacterProfile Profile { get; }
    internal CharacterSettlementStanding PreviousStanding { get; }
    internal bool ProfileWasCreated { get; }
    internal bool IsActive { get; set; } = true;
}

public interface ICharacterPopulationService
{
    IReadOnlyList<WorldCharacterProfile> Profiles { get; }
    WorldCharacterProfile AcquireVisitor(
        CharacterSO characterData,
        IEnumerable<string> unavailableProfileIds = null);
    bool TryCreateRecruitCandidate(
        out WorldCharacterProfile profile,
        out CharacterSO sourceData);
    void BindActor(WorldCharacterProfile profile, CharacterActor actor);
    void RefreshProfile(CharacterActor actor);
    void ReleaseVisitor(CharacterActor actor);
    void PromoteToStaff(CharacterActor actor);
    void PromoteToMinion(CharacterActor actor);
    CharacterSettlementStanding SetSettlementStanding(
        CharacterActor actor,
        CharacterSettlementStanding standing);
    void RestoreSettlementStanding(
        CharacterActor actor,
        CharacterSettlementStanding standing);
    CharacterSettlementStandingTransaction BeginSettlementStandingTransition(
        CharacterActor actor,
        CharacterSettlementStanding standing);
    void RollbackSettlementStandingTransition(
        CharacterSettlementStandingTransaction transaction);
    void CompleteSettlementStandingTransition(
        CharacterSettlementStandingTransaction transaction);
    bool TryGetProfile(CharacterActor actor, out WorldCharacterProfile profile);
    List<WorldCharacterProfile> CaptureProfiles();
    CharacterPopulationRestoreCandidate BuildRestoreCandidate(
        IEnumerable<WorldCharacterProfile> profiles);
    CharacterPopulationRestoreTransaction ApplyRestoreCandidate(
        CharacterPopulationRestoreCandidate candidate);
    void RollbackRestore(CharacterPopulationRestoreTransaction transaction);
    void CompleteRestore(CharacterPopulationRestoreTransaction transaction);
    void RestoreProfiles(IEnumerable<WorldCharacterProfile> profiles);
}

public sealed class CharacterPopulationService :
    ICharacterPopulationService,
    ICharacterSettlementStandingQuery,
    IDisposable
{
    private readonly CharacterPopulationApplicationAdapter adapter;

    [Inject]
    public CharacterPopulationService(CharacterPopulationApplicationAdapter adapter)
    {
        this.adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public IReadOnlyList<WorldCharacterProfile> Profiles => adapter.Profiles;

    public WorldCharacterProfile AcquireVisitor(
        CharacterSO characterData,
        IEnumerable<string> unavailableProfileIds = null) =>
        adapter.AcquireVisitor(characterData, unavailableProfileIds);

    public bool TryCreateRecruitCandidate(
        out WorldCharacterProfile profile,
        out CharacterSO sourceData) =>
        adapter.TryCreateRecruitCandidate(out profile, out sourceData);

    public void BindActor(WorldCharacterProfile profile, CharacterActor actor) =>
        adapter.BindActor(profile, actor);

    public void RefreshProfile(CharacterActor actor) => adapter.RefreshProfile(actor);

    public void ReleaseVisitor(CharacterActor actor) => adapter.ReleaseVisitor(actor);

    public void PromoteToStaff(CharacterActor actor) => adapter.PromoteToStaff(actor);

    public void PromoteToMinion(CharacterActor actor) => adapter.PromoteToMinion(actor);

    public CharacterSettlementStanding SetSettlementStanding(
        CharacterActor actor,
        CharacterSettlementStanding standing) =>
        adapter.SetSettlementStanding(actor, standing);

    public void RestoreSettlementStanding(
        CharacterActor actor,
        CharacterSettlementStanding standing) =>
        adapter.RestoreSettlementStanding(actor, standing);

    public CharacterSettlementStandingTransaction BeginSettlementStandingTransition(
        CharacterActor actor,
        CharacterSettlementStanding standing) =>
        adapter.BeginSettlementStandingTransition(actor, standing);

    public void RollbackSettlementStandingTransition(
        CharacterSettlementStandingTransaction transaction) =>
        adapter.RollbackSettlementStandingTransition(transaction);

    public void CompleteSettlementStandingTransition(
        CharacterSettlementStandingTransaction transaction) =>
        adapter.CompleteSettlementStandingTransition(transaction);

    public CharacterSettlementStanding GetStanding(CharacterActor actor) =>
        adapter.GetStanding(actor);

    public CharacterSettlementStanding GetStanding(
        string persistentCharacterId) => adapter.GetStanding(persistentCharacterId);

    public CharacterSettlementPopulationSnapshot GetSettlementPopulation() =>
        adapter.GetSettlementPopulation();

    public bool IsFormalResident(CharacterActor actor) =>
        GetStanding(actor) == CharacterSettlementStanding.Resident;

    public bool IsMinion(CharacterActor actor) =>
        GetStanding(actor) == CharacterSettlementStanding.Minion;

    public bool CanJoinExpedition(
        CharacterActor actor,
        out string failureReason)
    {
        if (IsMinion(actor))
        {
            failureReason = "하수인은 정착지 경비만 맡을 수 있어 원정에 참가할 수 없습니다.";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    public bool CanParticipateInMentoring(
        CharacterActor actor,
        out string failureReason)
    {
        if (IsMinion(actor))
        {
            failureReason = "하수인은 멘토나 학생으로 지정할 수 없습니다.";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    public bool IsWorkAllowed(
        CharacterActor actor,
        WorkTypeId workTypeId,
        out string failureReason)
    {
        if (!IsMinion(actor) || MinionIntegrationRules.IsWorkAllowed(workTypeId))
        {
            failureReason = string.Empty;
            return true;
        }
        failureReason = $"하수인은 {WorkTaskCatalog.GetDisplayName(workTypeId)} 업무를 맡을 수 없습니다.";
        return false;
    }

    public float GetApprovedWorkExperienceMultiplier(CharacterActor actor) =>
        IsMinion(actor)
            ? MinionIntegrationRules.MinionApprovedWorkExperienceMultiplier
            : 1f;

    public bool TryGetProfile(CharacterActor actor, out WorldCharacterProfile profile) =>
        adapter.TryGetProfile(actor, out profile);

    public List<WorldCharacterProfile> CaptureProfiles() => adapter.CaptureProfiles();

    public CharacterPopulationRestoreCandidate BuildRestoreCandidate(
        IEnumerable<WorldCharacterProfile> profiles) =>
        adapter.BuildRestoreCandidate(profiles);

    public CharacterPopulationRestoreTransaction ApplyRestoreCandidate(
        CharacterPopulationRestoreCandidate candidate) =>
        adapter.ApplyRestoreCandidate(candidate);

    public void RollbackRestore(CharacterPopulationRestoreTransaction transaction) =>
        adapter.RollbackRestore(transaction);

    public void CompleteRestore(CharacterPopulationRestoreTransaction transaction) =>
        adapter.CompleteRestore(transaction);

    public void RestoreProfiles(IEnumerable<WorldCharacterProfile> profiles) =>
        adapter.RestoreProfiles(profiles);

    public void Dispose() => adapter.Dispose();
}
