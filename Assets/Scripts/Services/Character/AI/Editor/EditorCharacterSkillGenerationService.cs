using System.Collections.Generic;

internal sealed class EditorCharacterSkillGenerationService :
    ICharacterSkillGenerationService
{
    public CharacterSkillDraft CreateDraft(
        CharacterProgression progression,
        CharacterSkillKind kind,
        int unlockLevel,
        int revision = 0)
    {
        CharacterSkillDraft draft = new CharacterSkillDraft
        {
            kind = kind,
            unlockLevel = unlockLevel,
            requestKey = $"editor:{kind}:{unlockLevel}:r{revision}"
        };
        int count = kind == CharacterSkillKind.Active ? 3 : 1;
        for (int index = 0; index < count; index++)
        {
            draft.rules.Add(new CharacterSkillCandidateRule
            {
                rarity = kind == CharacterSkillKind.Ultimate
                    ? CharacterSkillRarity.Legendary
                    : CharacterSkillRarity.Advanced,
                budget = 10,
                trigger = kind == CharacterSkillKind.Active
                    ? CharacterSkillTrigger.ManualCombat
                    : CharacterSkillTrigger.WorkCompleted,
                target = kind == CharacterSkillKind.Active
                    ? CharacterSkillTarget.Enemy
                    : CharacterSkillTarget.Self
            });
        }

        return draft;
    }

    public void RequestDraft(
        CharacterProgression progression,
        CharacterSkillDraft draft)
    {
        if (draft == null || draft.isReady || draft.permanentlyChosen)
        {
            return;
        }

        draft.isReady = true;
        progression?.OnDraftReady(draft);
    }

    public void CancelRequests(CharacterProgression progression)
    {
    }

    public bool TryValidateResponse(
        CharacterSkillDraft draft,
        string response,
        out List<CharacterSkillInstance> skills,
        out string error)
    {
        skills = new List<CharacterSkillInstance>();
        error = "Editor fixture does not parse LLM responses.";
        return false;
    }
}
