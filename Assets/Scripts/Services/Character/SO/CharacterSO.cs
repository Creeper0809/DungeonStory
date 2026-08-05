using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
[CreateAssetMenu(menuName = "DungeonStory/Character/SO", order = 0)]
[DrawWithUnity]
public class CharacterSO : ScriptableObject
{
    public CharacterType characterType;
    public CharacterRole role;
    public int id;
    public string characterName;
    public string speciesTag;
    public CharacterSpeciesSO species;
    public CharacterStatBlock baseStats = CharacterStatBlock.CreateDefault();
    public CharacterTraitSO[] traits = Array.Empty<CharacterTraitSO>();
    public WorkPriorityProfile defaultWorkPriorities = WorkPriorityProfile.CreateDefault();
    public CharacterAiPersonality aiPersonality = new CharacterAiPersonality();
    [TextArea] public string ownerSummary;
    [SerializeField] internal FacilityWorkType ownerPreferredWorkTypes;
    [SerializeField] private CharacterSkillInstance[] ownerFixedSkills = Array.Empty<CharacterSkillInstance>();

    public Sprite characterSprite;

    public BuildingSO[] favoriteStore;

    [SerializeField] private int frequencyVisitMax;
    [SerializeField] private int frequencyVisitMin;

    [SerializeField] private int maxHoldingMoney;
    [SerializeField] private int minHoldingMoney;

    [SerializeField] private CharacterSpeedType speedType;
    [SerializeField] private CharacterRespawnSpeedType respawnSpeedType;

    public string SpeciesTag => species != null && !string.IsNullOrWhiteSpace(species.speciesTag)
        ? species.speciesTag
        : speciesTag;
    public bool IsOwnerCandidate => role == CharacterRole.Owner
        && (species == null || species.ownerSelectable);
    public IEnumerable<WorkTypeId> OwnerPreferredWorkTypeIds
    {
        get
        {
            foreach (WorkTypeDefinition definition in FacilityWorkTypeMap.Enumerate(ownerPreferredWorkTypes))
            {
                yield return definition.WorkTypeId;
            }
        }
    }

    public bool HasOwnerPreferredWorkTypes => (int)ownerPreferredWorkTypes != 0;
    public IReadOnlyList<CharacterSkillInstance> OwnerFixedSkills =>
        ownerFixedSkills ?? Array.Empty<CharacterSkillInstance>();
    public float moveSpeed
    {
        get
        {
            int rawSpeed = (int)speedType;
            if (rawSpeed <= 0)
            {
                rawSpeed = (int)CharacterSpeedType.Normal;
            }

            return rawSpeed / 3.5f;
        }
    }

    public CharacterRuntimeProfile CreateRuntimeProfile()
    {
        return CharacterRuntimeProfile.From(this);
    }

    public TimeOfDay leavingTime
    {
        get
        {
            if(characterType == CharacterType.NPC)
            {
                return TimeOfDay.Morning;
            }
            else
            {
                return TimeOfDay.Evening;
            }
        }
    }
    public TimeOfDay respawnTime
    {
        get
        {
            if(characterType == CharacterType.NPC)
            {
                return TimeOfDay.Night;
            }
            else
            {
                return TimeOfDay.Noon;
            }
        }
    }

    public float GetRespawnSpeed(IRandomStream randomStream)
    {
        if (randomStream == null)
        {
            throw new ArgumentNullException(nameof(randomStream));
        }

        int baseSpeed = (int)respawnSpeedType;
        return Mathf.Lerp(baseSpeed, baseSpeed * 1.5f, randomStream.NextFloat());
    }

    public void ConfigureGeneratedVisitProfile(
        int minimumFrequency,
        int maximumFrequency,
        int minimumMoney,
        int maximumMoney,
        int movementSpeed = 4,
        int respawnSpeed = 13)
    {
        frequencyVisitMin = Mathf.Max(1, minimumFrequency);
        frequencyVisitMax = Mathf.Max(
            frequencyVisitMin,
            maximumFrequency);
        minHoldingMoney = Mathf.Max(0, minimumMoney);
        maxHoldingMoney = Mathf.Max(
            minHoldingMoney + 1,
            maximumMoney);
        speedType = (CharacterSpeedType)Mathf.Clamp(
            movementSpeed,
            (int)CharacterSpeedType.VerySlow,
            (int)CharacterSpeedType.VeryFast);
        respawnSpeedType = (CharacterRespawnSpeedType)Mathf.Clamp(
            respawnSpeed,
            (int)CharacterRespawnSpeedType.VeryFast,
            (int)CharacterRespawnSpeedType.VerySlow);
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //                                                                                                                              //
    //                                                                    GetSet                                                    //
    //                                                                                                                              //
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int GetFrequencyVisit(IRandomStream randomStream)
    {
        if (randomStream == null)
        {
            throw new ArgumentNullException(nameof(randomStream));
        }

        int min = Mathf.Max(1, frequencyVisitMin);
        int max = Mathf.Max(min, frequencyVisitMax);
        return randomStream.NextInt(min, max + 1);
    }

    public int GetHoldingMoney(IRandomStream randomStream)
    {
        if (randomStream == null)
        {
            throw new ArgumentNullException(nameof(randomStream));
        }

        int min = Mathf.Min(minHoldingMoney, maxHoldingMoney);
        int maxExclusive = Mathf.Max(min + 1, maxHoldingMoney);
        return randomStream.NextInt(min, maxExclusive);
    }

    public int GetHoldingMoney(
        IRandomStream randomStream,
        CharacterRuntimeProfile profile)
    {
        int baseMoney = GetHoldingMoney(randomStream);
        float multiplier = profile != null ? profile.GetSpendingMultiplier() : 1f;
        return Mathf.Max(0, Mathf.RoundToInt(baseMoney * multiplier));
    }
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //                                                                                                                              //
    //                                                                    ForEditor                                                 //
    //                                                                                                                              //
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private enum CharacterSpeedType
    {
        VerySlow = 2,
        Slow = 3,
        Normal = 4,
        Fast = 5,
        VeryFast = 6
    }
    private enum CharacterRespawnSpeedType
    {
        VerySlow = 23,
        Slow = 18,
        Normal = 13,
        Fast = 8,
        VeryFast = 3
    }
}
