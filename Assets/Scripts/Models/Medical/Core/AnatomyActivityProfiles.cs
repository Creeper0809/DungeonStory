using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IAnatomyActivityProfileCatalog
{
    IReadOnlyList<AnatomyActivityProfile> Profiles { get; }
    AnatomyActivityProfile Get(AnatomyActivityId activity);
    IReadOnlyList<string> Validate();
}

public sealed class DefaultAnatomyActivityProfileCatalog :
    IAnatomyActivityProfileCatalog
{
    private readonly IReadOnlyList<AnatomyActivityProfile> profiles;
    private readonly IReadOnlyDictionary<AnatomyActivityId, AnatomyActivityProfile> byId;

    public DefaultAnatomyActivityProfileCatalog()
    {
        profiles = new[]
        {
            Profile(AnatomyActivityId.Movement, 1.50f,
                Weight(AnatomyActionAxisId.Locomotion, 0.75f),
                Weight(AnatomyActionAxisId.Sustain, 0.25f)),
            Profile(AnatomyActivityId.Accuracy, 1.35f,
                Weight(AnatomyActionAxisId.Awareness, 0.55f),
                Weight(AnatomyActionAxisId.Handling, 0.45f)),
            Profile(AnatomyActivityId.Evasion, 1.35f,
                Weight(AnatomyActionAxisId.Awareness, 0.25f),
                Weight(AnatomyActionAxisId.Locomotion, 0.75f)),
            Profile(AnatomyActivityId.Work, 1.65f,
                Weight(AnatomyActionAxisId.Handling, 0.65f),
                Weight(AnatomyActionAxisId.Sustain, 0.35f)),
            Profile(AnatomyActivityId.Carry, 1.60f,
                Weight(AnatomyActionAxisId.Handling, 0.35f),
                Weight(AnatomyActionAxisId.Locomotion, 0.25f),
                Weight(AnatomyActionAxisId.Sustain, 0.40f)),
            Profile(AnatomyActivityId.MeleePower, 1.60f,
                Weight(AnatomyActionAxisId.Handling, 0.55f),
                Weight(AnatomyActionAxisId.Sustain, 0.45f)),
            Profile(AnatomyActivityId.Treatment, 1.50f,
                Weight(AnatomyActionAxisId.Awareness, 0.45f),
                Weight(AnatomyActionAxisId.Handling, 0.35f),
                Weight(AnatomyActionAxisId.Recovery, 0.20f)),
            Profile(AnatomyActivityId.Recovery, 1.50f,
                Weight(AnatomyActionAxisId.Recovery, 0.70f),
                Weight(AnatomyActionAxisId.Sustain, 0.30f)),
            Profile(AnatomyActivityId.Overclock, 1.75f,
                Weight(AnatomyActionAxisId.Awareness, 0.20f),
                Weight(AnatomyActionAxisId.Handling, 0.30f),
                Weight(AnatomyActionAxisId.Locomotion, 0.15f),
                Weight(AnatomyActionAxisId.Sustain, 0.35f))
        };
        byId = profiles.ToDictionary(profile => profile.Activity);
    }

    public IReadOnlyList<AnatomyActivityProfile> Profiles => profiles;

    public AnatomyActivityProfile Get(AnatomyActivityId activity)
    {
        return byId.TryGetValue(activity, out AnatomyActivityProfile profile)
            ? profile
            : byId[AnatomyActivityId.Work];
    }

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = new();
        foreach (AnatomyActivityId activity in Enum.GetValues(typeof(AnatomyActivityId)))
        {
            if (!byId.TryGetValue(activity, out AnatomyActivityProfile profile))
            {
                errors.Add($"Missing anatomy activity profile: {activity}.");
                continue;
            }

            float weight = profile.AxisWeights.Sum(item => item?.Weight ?? 0f);
            if (Mathf.Abs(weight - 1f) > 0.001f)
            {
                errors.Add($"{activity}: anatomy axis weights must total 1.0 (actual {weight:0.###}).");
            }
        }

        return errors;
    }

    private static AnatomyActivityProfile Profile(
        AnatomyActivityId activity,
        float cap,
        params AnatomyNodeAxisContribution[] weights)
    {
        return new AnatomyActivityProfile(activity, cap, weights);
    }

    private static AnatomyNodeAxisContribution Weight(
        AnatomyActionAxisId axis,
        float weight)
    {
        return new AnatomyNodeAxisContribution(axis, weight);
    }
}
