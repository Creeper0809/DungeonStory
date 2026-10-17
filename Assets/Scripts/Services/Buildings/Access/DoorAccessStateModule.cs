using System;
using System.Linq;
using UnityEngine;

public sealed class DoorAccessStateModule : IBuildingStateModule
{
    public const string StateModuleId = "door.access";
    private readonly DoorAccessPolicyState state = new DoorAccessPolicyState();
    private readonly Action changed;

    public DoorAccessStateModule(Action changed = null)
    {
        this.changed = changed;
    }

    public string ModuleId => StateModuleId;
    public int CurrentVersion => 1;
    public DoorAccessPolicyState State => state;
    public long Revision => state.Revision;

    public bool SetGroupAllowed(DoorAccessGroup group, bool allowed)
    {
        DoorAccessPolicyState candidate = state.Clone();
        if (!candidate.SetGroupAllowed(group, allowed))
        {
            return false;
        }
        return TryCommitPolicy(candidate);
    }

    public bool SetIndividualRule(
        string persistentId,
        DoorAccessIndividualRule rule)
    {
        DoorAccessPolicyState candidate = state.Clone();
        if (!candidate.SetIndividualRule(persistentId, rule))
        {
            return false;
        }
        return TryCommitPolicy(candidate);
    }

    public bool ApplyPreset(DoorAccessPreset preset)
    {
        DoorAccessPolicyState candidate = state.Clone();
        candidate.ApplyPreset(preset);
        return TryCommitPolicy(candidate);
    }

    public bool CopyFrom(DoorAccessPolicyState source)
    {
        DoorAccessPolicyState candidate = source?.Clone()
            ?? new DoorAccessPolicyState();
        candidate.SetRevision(state.Revision);
        return TryCommitPolicy(candidate);
    }

    public string CaptureState()
    {
        state.Normalize();
        return JsonUtility.ToJson(state);
    }

    public bool TryRestoreState(int version, string payload, out string error)
    {
        if (version != CurrentVersion)
        {
            error = $"unsupported version {version}; current version is {CurrentVersion}";
            return false;
        }

        try
        {
            DoorAccessPolicyState restored =
                JsonUtility.FromJson<DoorAccessPolicyState>(payload);
            if (restored == null || restored.Revision < 0L)
            {
                error = "door access policy revision is invalid";
                return false;
            }
            restored.Normalize();
            state.CopyFrom(restored);
            changed?.Invoke();
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private bool TryCommitPolicy(DoorAccessPolicyState candidate)
    {
        if (candidate == null || EquivalentPolicy(state, candidate))
        {
            return false;
        }
        if (state.Revision == long.MaxValue)
        {
            return false;
        }

        candidate.SetRevision(checked(state.Revision + 1L));
        state.CopyFrom(candidate);
        changed?.Invoke();
        return true;
    }

    private static bool EquivalentPolicy(
        DoorAccessPolicyState left,
        DoorAccessPolicyState right)
    {
        return left != null
            && right != null
            && left.AllowedGroups == right.AllowedGroups
            && left.IndividuallyAllowedIds.SequenceEqual(
                right.IndividuallyAllowedIds,
                StringComparer.Ordinal)
            && left.IndividuallyDeniedIds.SequenceEqual(
                right.IndividuallyDeniedIds,
                StringComparer.Ordinal);
    }
}
