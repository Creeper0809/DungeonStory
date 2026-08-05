using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class CodexEvolutionRecorder
{
    public static void Record(CodexState state, CodexEvolutionObservationSnapshot snapshot)
    {
        CodexObservationRecorder.Apply(state, snapshot?.Entry);
    }
}
