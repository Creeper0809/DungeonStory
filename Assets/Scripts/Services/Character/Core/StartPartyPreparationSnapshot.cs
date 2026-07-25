using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class PreparedStartPartyMemberSnapshot
{
    public int rosterId;
    public int partySlot;
    public bool isOwner;
    public int characterDataId;
    public string persistentId = string.Empty;
    public string displayName = string.Empty;
    public int level = 1;
    public int currentExperience;
    public CharacterGrowthState growth = new CharacterGrowthState();
    public CharacterNarrativeLedger narrative = new CharacterNarrativeLedger();

    public CharacterProgressionSnapshot ToProgressionSnapshot()
    {
        return new CharacterProgressionSnapshot(
            Mathf.Max(1, level),
            Mathf.Max(0, currentExperience),
            growth?.Clone() ?? new CharacterGrowthState(),
            narrative?.Clone() ?? new CharacterNarrativeLedger());
    }
}

[Serializable]
public sealed class PreparedStartPartySnapshot
{
    public DungeonDifficulty difficulty = DungeonDifficulty.Normal;
    public int runSeed;
    public PreparedStartPartyMemberSnapshot owner;
    public List<PreparedStartPartyMemberSnapshot> staff = new List<PreparedStartPartyMemberSnapshot>();

    public bool IsValid => owner != null
        && owner.isOwner
        && staff != null
        && staff.Count == 2
        && staff.All(member => member != null && !member.isOwner);

    public IReadOnlyList<PreparedStartPartyMemberSnapshot> OrderedMembers
    {
        get
        {
            List<PreparedStartPartyMemberSnapshot> members = new List<PreparedStartPartyMemberSnapshot>();
            if (owner != null)
            {
                members.Add(owner);
            }

            if (staff != null)
            {
                members.AddRange(staff.Where(member => member != null));
            }

            return members.OrderBy(member => member.partySlot).ToArray();
        }
    }
}

public readonly struct CharacterSkillSlotProfile
{
    public CharacterSkillSlotProfile(
        int speciesActiveSlots,
        int normalActiveSlots,
        int passiveSlots,
        int ultimateSlots,
        int ownerFixedSlots)
    {
        SpeciesActiveSlots = Mathf.Max(0, speciesActiveSlots);
        NormalActiveSlots = Mathf.Max(0, normalActiveSlots);
        PassiveSlots = Mathf.Max(0, passiveSlots);
        UltimateSlots = Mathf.Max(0, ultimateSlots);
        OwnerFixedSlots = Mathf.Max(0, ownerFixedSlots);
    }

    public int SpeciesActiveSlots { get; }
    public int NormalActiveSlots { get; }
    public int PassiveSlots { get; }
    public int UltimateSlots { get; }
    public int OwnerFixedSlots { get; }

    public static CharacterSkillSlotProfile Normal => new CharacterSkillSlotProfile(
        1,
        CharacterProgression.NormalActiveSlots,
        CharacterProgression.PassiveSlots,
        1,
        0);

    public static CharacterSkillSlotProfile Owner => new CharacterSkillSlotProfile(
        1,
        CharacterProgression.NormalActiveSlots,
        CharacterProgression.PassiveSlots,
        1,
        CharacterOwnerFixedSkillUtility.FixedSlotCount);

    public static CharacterSkillSlotProfile For(CharacterSO data, bool forceOwner = false)
    {
        return forceOwner || (data != null && data.IsOwnerCandidate)
            ? Owner
            : Normal;
    }
}

public static class CharacterOwnerFixedSkillUtility
{
    public const int FixedSlotCount = 4;

    private static readonly CharacterSkillInstance[] FallbackSkills =
    {
        CreateFallback(
            "owner_fixed_instinct",
            "창업 본능",
            "사장이 현장에 있으면 작업 시작 속도가 조금 오른다.",
            "work_speed",
            "small",
            CharacterSkillTrigger.WorkStarted),
        CreateFallback(
            "owner_fixed_reputation",
            "첫인상 장악",
            "긍정적인 관계 변화가 생길 때 사장 보너스를 조금 더한다.",
            "relationship",
            "small",
            CharacterSkillTrigger.RelationshipChanged),
        CreateFallback(
            "owner_fixed_accounting",
            "장부 감각",
            "상점 업무 완료 시 수익 보너스를 조금 얻는다.",
            "revenue",
            "small",
            CharacterSkillTrigger.WorkCompleted),
        CreateFallback(
            "owner_fixed_morale",
            "마지막 격려",
            "업무 완료 시 주변 아군의 기분을 조금 올린다.",
            "mood",
            "small",
            CharacterSkillTrigger.WorkCompleted)
    };

    public static IReadOnlyList<CharacterSkillInstance> GetSkills(CharacterSO ownerData)
    {
        List<CharacterSkillInstance> skills = ownerData != null
            ? ownerData.OwnerFixedSkills
                .Where(skill => skill != null && !string.IsNullOrWhiteSpace(skill.displayName))
                .Select(skill => Normalize(skill.Clone(), ownerData))
                .ToList()
            : new List<CharacterSkillInstance>();

        for (int i = skills.Count; i < FixedSlotCount; i++)
        {
            skills.Add(FallbackSkills[i].Clone());
        }

        return skills.Take(FixedSlotCount).ToArray();
    }

    private static CharacterSkillInstance Normalize(CharacterSkillInstance skill, CharacterSO ownerData)
    {
        skill.kind = CharacterSkillKind.OwnerFixed;
        if (string.IsNullOrWhiteSpace(skill.id))
        {
            string ownerId = ownerData != null ? ownerData.id.ToString() : "owner";
            skill.id = $"owner_fixed:{ownerId}:{skill.displayName}";
        }

        if (skill.rarity == default)
        {
            skill.rarity = CharacterSkillRarity.Heroic;
        }

        skill.target = skill.target == default
            ? CharacterSkillTarget.Self
            : skill.target;
        skill.usableFrom = skill.usableFrom == OffenseFormationMask.None
            ? OffenseFormationMask.Any
            : skill.usableFrom;
        skill.targetPositions = skill.targetPositions == OffenseFormationMask.None
            ? OffenseFormationMask.Any
            : skill.targetPositions;
        return skill;
    }

    private static CharacterSkillInstance CreateFallback(
        string id,
        string name,
        string description,
        string moduleId,
        string variantId,
        CharacterSkillTrigger trigger)
    {
        return new CharacterSkillInstance
        {
            id = id,
            displayName = name,
            description = description,
            narrativeReason = "처음부터 사용할 수 있는 사장 고정 권능입니다.",
            kind = CharacterSkillKind.OwnerFixed,
            rarity = CharacterSkillRarity.Heroic,
            trigger = trigger,
            target = CharacterSkillTarget.Self,
            modules = new List<CharacterSkillModuleSelection>
            {
                new CharacterSkillModuleSelection
                {
                    moduleId = moduleId,
                    variantId = variantId
                }
            }
        };
    }
}
