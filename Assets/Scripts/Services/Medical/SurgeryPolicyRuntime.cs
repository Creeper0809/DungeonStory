using System;
using System.Collections.Generic;
using System.Linq;

public sealed class SurgeryPolicyRuntime : ISurgeryPolicyRuntime
{
    private readonly Dictionary<string, bool> policies =
        new Dictionary<string, bool>(StringComparer.Ordinal);

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

    public void Restore(
        IEnumerable<SurgerySubjectPolicyState> restored,
        IList<string> warnings)
    {
        policies.Clear();
        foreach (SurgerySubjectPolicyState state in
                 restored ?? Array.Empty<SurgerySubjectPolicyState>())
        {
            if (state == null || string.IsNullOrWhiteSpace(state.subjectId))
            {
                warnings?.Add("대상이 없는 수술 정책을 복원에서 제외했습니다.");
                continue;
            }

            policies[state.subjectId.Trim()] =
                state.automaticEmergencySurgery;
        }
    }
}
