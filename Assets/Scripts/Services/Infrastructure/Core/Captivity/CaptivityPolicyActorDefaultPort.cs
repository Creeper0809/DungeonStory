using System;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivityPolicyActorDefaultPort : ICaptivityPolicyActorPort
{
    private readonly ICaptivityActorEffectsPort actors;

    public CaptivityPolicyActorDefaultPort(ICaptivityActorEffectsPort actors)
    {
        this.actors = actors ?? throw new ArgumentNullException(nameof(actors));
    }

    public void ConfineLaborer(string captiveId)
    {
        actors.ConfineLaborer(captiveId);
    }
}
