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
    bool TryGetProfile(CharacterActor actor, out WorldCharacterProfile profile);
    List<WorldCharacterProfile> CaptureProfiles();
    CharacterPopulationRestoreCandidate BuildRestoreCandidate(
        IEnumerable<WorldCharacterProfile> profiles);
    CharacterPopulationRestoreTransaction ApplyRestoreCandidate(
        CharacterPopulationRestoreCandidate candidate);
    void RollbackRestore(CharacterPopulationRestoreTransaction transaction);
    void CompleteRestore(CharacterPopulationRestoreTransaction transaction);
    void ReplenishPreparedPoolBestEffort();
    void RestoreProfiles(IEnumerable<WorldCharacterProfile> profiles);
}

public sealed class CharacterPopulationService : ICharacterPopulationService, IDisposable
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

    public void ReplenishPreparedPoolBestEffort() =>
        adapter.ReplenishPreparedPoolBestEffort();

    public void RestoreProfiles(IEnumerable<WorldCharacterProfile> profiles) =>
        adapter.RestoreProfiles(profiles);

    public void Dispose() => adapter.Dispose();
}
