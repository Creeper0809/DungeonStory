using System;
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

    public bool SetGroupAllowed(DoorAccessGroup group, bool allowed)
    {
        if (!state.SetGroupAllowed(group, allowed))
        {
            return false;
        }

        changed?.Invoke();
        return true;
    }

    public bool SetIndividualRule(
        string persistentId,
        DoorAccessIndividualRule rule)
    {
        if (!state.SetIndividualRule(persistentId, rule))
        {
            return false;
        }

        changed?.Invoke();
        return true;
    }

    public void ApplyPreset(DoorAccessPreset preset)
    {
        DoorAccessPolicyState previous = state.Clone();
        state.ApplyPreset(preset);
        if (!Equivalent(previous, state))
        {
            changed?.Invoke();
        }
    }

    public void CopyFrom(DoorAccessPolicyState source)
    {
        DoorAccessPolicyState previous = state.Clone();
        state.CopyFrom(source);
        if (!Equivalent(previous, state))
        {
            changed?.Invoke();
        }
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

    private static bool Equivalent(
        DoorAccessPolicyState left,
        DoorAccessPolicyState right)
    {
        return left != null
            && right != null
            && string.Equals(
                JsonUtility.ToJson(left),
                JsonUtility.ToJson(right),
                StringComparison.Ordinal);
    }
}
