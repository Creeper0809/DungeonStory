using System;
using System.Collections.Generic;

[Serializable]
public sealed class DungeonCharacterWorldSaveData
{
    public List<DungeonCharacterSaveData> actors =
        new List<DungeonCharacterSaveData>();
    public List<WorldCharacterProfile> populationProfiles =
        new List<WorldCharacterProfile>();
    public GlobalFacilityReputationSnapshot globalFacilityReputation =
        new GlobalFacilityReputationSnapshot();
}

[Serializable]
public sealed class DungeonCharacterSaveData
{
    public string persistentId = string.Empty;
    public int dataId = -1;
    public bool isOwner;
    public string displayName = string.Empty;
    public CharacterType characterType;
    public CharacterRole role;
    public int gridX;
    public int gridY;
    public string societyResponseMovementOperationId = string.Empty;
    public string societyResponseMovementExternalOperationId = string.Empty;
    public string societyResponseMovementFacilityId = string.Empty;
    public int societyResponseMovementDestinationX;
    public int societyResponseMovementDestinationY;
    public string societyResponseMovementReceiptId = string.Empty;
    public CharacterLifecycleState lifecycleState =
        CharacterLifecycleState.Active;
    // Missing legacy values are not a valid current owner checkpoint.
    public long lifecycleRevision = -1L;
    public float currentHealth;
    public float injurySeverity;
    public float baseMood = CharacterMoodRules.DefaultBaseMood;
    public CharacterRestObservationSaveData restObservation;
    public List<DungeonCharacterConditionSaveData> conditions =
        new List<DungeonCharacterConditionSaveData>();
    public List<DungeonCharacterMoodFactorSaveData> moodFactors =
        new List<DungeonCharacterMoodFactorSaveData>();
    public List<DungeonCharacterWorkPrioritySaveData> workPriorities =
        new List<DungeonCharacterWorkPrioritySaveData>();
    public AbilityWork.DutyState dutyState = AbilityWork.DutyState.OnDuty;
    public int visitCount;
    public int lookAroundCount;
    public int holdingMoney;
    public List<RetailPurchaseRecoverySaveData> pendingRetailPurchases =
        new List<RetailPurchaseRecoverySaveData>();
    public List<string> recentLogEntries = new List<string>();
    public int level = 1;
    public int currentExperience;
    public List<string> learnedSkillIds = new List<string>();
    public List<string> equippedSkillIds = new List<string>();
    public CharacterGrowthState growth = new CharacterGrowthState();
    public CharacterNarrativeLedger narrative = new CharacterNarrativeLedger();
    public CharacterAcquiredTraitAggregateState acquiredTraits;
    public CharacterSocialMemorySnapshot socialMemory =
        new CharacterSocialMemorySnapshot();
    public CharacterExpeditionRecoveryState expeditionRecovery =
        new CharacterExpeditionRecoveryState();
    public CharacterCarryInventorySaveData carryInventory =
        new CharacterCarryInventorySaveData();
    public HaulDeliveryIntentSaveData haulDeliveryIntent;
}

public enum RetailPurchaseRecoveryPhase
{
    LotTaken = 0,
    PhysicalCommitted = 1,
    OutcomeCommitted = 2,
    RevenueCommitted = 3
}

[Serializable]
public sealed class RetailPurchaseRecoverySaveData
{
    public static bool IsPublicationKey(string value) =>
        DungeonStory.Foundation.Facts.DomainFactValueSyntax.IsText(value, 2048, false) && value == value.Trim();
    public static bool IsFingerprint(string value)
    {
        if (value?.Length != 64) return false;
        foreach (char digit in value)
            if (!(digit is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F')) return false;
        return true;
    }

    public void Validate()
    {
        _ = new RetailPurchaseFact(this);
        if (shopUseOperationId == null || (shopUseOperationId.Length == 0 ? shopUseCartIndex != -1
                : !IsPublicationKey(shopUseOperationId) || shopUseCartIndex < 0)
            || !Enum.IsDefined(typeof(RetailPurchaseRecoveryPhase), phase)
            || publicationKey == null || publicationId == null || publicationFingerprint == null)
            throw new ArgumentException("retail-purchase-recovery-invalid");
        bool noProof = publicationId.Length == 0 && publicationFingerprint.Length == 0;
        if (phase == RetailPurchaseRecoveryPhase.LotTaken
                ? publicationKey.Length != 0 || !noProof
                : phase == RetailPurchaseRecoveryPhase.PhysicalCommitted
                    ? !noProof || publicationKey.Length > 0 && !IsPublicationKey(publicationKey)
                    : !IsPublicationKey(publicationKey) || !IsPublicationKey(publicationId) || !IsFingerprint(publicationFingerprint))
            throw new ArgumentException("retail-purchase-publication-phase-invalid");
    }

    public string shopUseOperationId = string.Empty;
    public int shopUseCartIndex = -1;
    public string operationId = string.Empty;
    public string buyerId = string.Empty;
    public string facilityId = string.Empty;
    public int absoluteDay;
    public int purchaseCost;
    public int buyerMoneyBefore;
    public int buyerMoneyAfter;
    public string revenueWorkerId = string.Empty;
    public float revenuePriceMultiplier = 1f;
    public string revenueSkillModifierProvenance =
        WorkCompletionIdentityReferenceDefaults.None;
    public RetailPurchaseRecoveryPhase phase;
    public string publicationKey = string.Empty;
    public string publicationId = string.Empty;
    public string publicationFingerprint = string.Empty;
    public RetailStockLotSnapshot lot = new RetailStockLotSnapshot();

    public RetailPurchaseRecoverySaveData Clone() =>
        new RetailPurchaseRecoverySaveData
        {
            shopUseOperationId = shopUseOperationId ?? string.Empty,
            shopUseCartIndex = shopUseCartIndex,
            operationId = operationId ?? string.Empty,
            buyerId = buyerId ?? string.Empty,
            facilityId = facilityId ?? string.Empty,
            absoluteDay = absoluteDay,
            purchaseCost = purchaseCost,
            buyerMoneyBefore = buyerMoneyBefore,
            buyerMoneyAfter = buyerMoneyAfter,
            revenueWorkerId = revenueWorkerId ?? string.Empty,
            revenuePriceMultiplier = revenuePriceMultiplier,
            revenueSkillModifierProvenance =
                revenueSkillModifierProvenance
                ?? WorkCompletionIdentityReferenceDefaults.None,
            phase = phase,
            publicationKey = publicationKey,
            publicationId = publicationId,
            publicationFingerprint = publicationFingerprint,
            lot = lot?.Clone()
        };
}

[Serializable]
public sealed class DungeonCharacterConditionSaveData
{
    public CharacterCondition condition;
    public float value;
}

[Serializable]
public sealed class DungeonCharacterMoodFactorSaveData
{
    public string id = string.Empty;
    public string label = string.Empty;
    public float value;
    public float remainingSeconds;
}

[Serializable]
public sealed class DungeonCharacterWorkPrioritySaveData
{
    public string workTypeId = string.Empty;
    public WorkPriorityLevel priority;
}
