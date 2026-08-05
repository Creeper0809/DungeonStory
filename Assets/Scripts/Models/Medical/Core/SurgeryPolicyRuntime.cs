using System;
using System.Collections.Generic;
using System.Linq;

public sealed class SurgeryPolicyRuntime : ISurgeryPolicyRuntime
{
    private readonly SurgeryAggregateStateStore stateStore;

    public SurgeryPolicyRuntime(SurgeryAggregateStateStore stateStore)
    {
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
    }

    private Dictionary<string, bool> policies => stateStore.State.Policies;

    public bool IsAutomaticEmergencySurgeryEnabled(SurgicalSubjectRef subject)
    {
        if (subject == null || !subject.IsValid)
        {
            return false;
        }

        if (policies.TryGetValue(subject.subjectId, out bool enabled))
        {
            return enabled;
        }

        return subject.kind == SurgicalSubjectKind.Character
            && subject.automaticEmergencyDefault;
    }

    public void SetAutomaticEmergencySurgery(
        SurgicalSubjectRef subject,
        bool enabled)
    {
        if (subject == null || !subject.IsValid)
        {
            throw new ArgumentException(
                "수술 정책에는 유효한 대상이 필요합니다.",
                nameof(subject));
        }

        policies[subject.subjectId] = enabled;
    }

    public IReadOnlyList<SurgerySubjectPolicyState> Capture()
    {
        return policies
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new SurgerySubjectPolicyState
            {
                subjectId = pair.Key,
                automaticEmergencySurgery = pair.Value
            })
            .ToArray();
    }

}
