using System;
using UnityEngine.Scripting.APIUpdating;

public static class CharacterRecordNarrativeRules
{
    public const int MaxLineCharacters = 60;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterRecordJsonDto : ILlmJsonPayload
{
    public string line;
    public string[] usedMotifIds = Array.Empty<string>();
    public string[] usedCharacterFactIds = Array.Empty<string>();

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(line))
        {
            error = "line is required.";
            return false;
        }

        if (line.Length > CharacterRecordNarrativeRules.MaxLineCharacters)
        {
            error =
                $"line must be {CharacterRecordNarrativeRules.MaxLineCharacters} characters or shorter.";
            return false;
        }

        return true;
    }
}
