using System;
using System.Collections.Generic;

public enum EconomyTransactionKind
{
    LegacyIncome = 0,
    LegacyExpense = 1,
    SaleIncome = 10,
    GuestServiceIncome = 11,
    CircusIncome = 12,
    RansomIncome = 13,
    ContractIncome = 14,
    LootSaleIncome = 15,
    EmployeeWage = 20,
    MercenaryAdvance = 21,
    MercenaryRenewal = 22,
    PaidFacilityContract = 23,
    AutoProcurement = 24,
    PaidFacilityUse = 25,
    PaidFacilityOrder = 26,
    ShopPurchase = 27,
    ReforgePrecision = 30,
    EquipmentOverclock = 31,
    FacilityOverclock = 32,
    TreasuryDefenseShot = 33,
    Bribe = 34,
    ExpeditionFieldFundAllocation = 35,
    ExpeditionFieldFundReturn = 36,
    DebugAdjustment = 90
}

[Serializable]
public struct EconomyTransactionContext
{
    public EconomyTransactionKind kind;
    public string sourceId;
    public string targetId;
    public string description;

    public EconomyTransactionContext(
        EconomyTransactionKind kind,
        string sourceId,
        string targetId = "",
        string description = "")
    {
        this.kind = kind;
        this.sourceId = sourceId ?? string.Empty;
        this.targetId = targetId ?? string.Empty;
        this.description = description ?? string.Empty;
    }
}

[Serializable]
public sealed class EconomyTransactionRecord
{
    public string transactionId = string.Empty;
    public EconomyTransactionKind kind;
    public string sourceId = string.Empty;
    public string targetId = string.Empty;
    public string description = string.Empty;
    public int amount;
    public int balanceAfter;
    public float gameTime;
    public bool succeeded = true;
    public string failureReason = string.Empty;

    public EconomyTransactionRecord Clone()
    {
        return new EconomyTransactionRecord
        {
            transactionId = transactionId,
            kind = kind,
            sourceId = sourceId,
            targetId = targetId,
            description = description,
            amount = amount,
            balanceAfter = balanceAfter,
            gameTime = gameTime,
            succeeded = succeeded,
            failureReason = failureReason
        };
    }
}

[Serializable]
public sealed class EconomyTransactionLedgerSaveData
{
    public int nextSequence = 1;
    public List<EconomyTransactionRecord> records =
        new List<EconomyTransactionRecord>();
}

public enum EmploymentContractKind
{
    Founder = 0,
    Employee = 1,
    Mercenary = 2
}

[Serializable]
public sealed class EmployeeWageState
{
    public string characterId = string.Empty;
    public EmploymentContractKind contractKind = EmploymentContractKind.Employee;
    public int rolePremium;
    public int equipmentGradePremium;
    public int unpaidWages;
    public int lastSettledDay;
    public bool active = true;
    public bool departed;

    public EmployeeWageState Clone()
    {
        return new EmployeeWageState
        {
            characterId = characterId,
            contractKind = contractKind,
            rolePremium = rolePremium,
            equipmentGradePremium = equipmentGradePremium,
            unpaidWages = unpaidWages,
            lastSettledDay = lastSettledDay,
            active = active,
            departed = departed
        };
    }
}

[Serializable]
public sealed class MercenaryContract
{
    public string characterId = string.Empty;
    public int hiredDay;
    public int lastRenewedDay;
    public bool active = true;

    public MercenaryContract Clone()
    {
        return new MercenaryContract
        {
            characterId = characterId,
            hiredDay = hiredDay,
            lastRenewedDay = lastRenewedDay,
            active = active
        };
    }
}

[Serializable]
public sealed class EmploymentDailySettlement
{
    public int day;
    public int employeeWagesDue;
    public int employeeWagesPaid;
    public int mercenaryFeesDue;
    public int mercenaryFeesPaid;
    public int unpaidEmployeeWages;
    public List<string> departedMercenaryIds = new List<string>();
}

[Serializable]
public sealed class EmploymentContractSaveData
{
    public List<EmployeeWageState> wageStates = new List<EmployeeWageState>();
    public List<MercenaryContract> mercenaryContracts =
        new List<MercenaryContract>();
}

[Serializable]
public sealed class AutoProcurementRule
{
    public string ruleId = string.Empty;
    public StockCategory category = StockCategory.General;
    public int targetQuantity;
    public int maximumUnitPrice;
    public int dailyMaximumQuantity;
    public int priority;
    public bool enabled = true;

    public AutoProcurementRule Clone()
    {
        return new AutoProcurementRule
        {
            ruleId = ruleId,
            category = category,
            targetQuantity = targetQuantity,
            maximumUnitPrice = maximumUnitPrice,
            dailyMaximumQuantity = dailyMaximumQuantity,
            priority = priority,
            enabled = enabled
        };
    }
}

[Serializable]
public sealed class ProcurementWishlistRule
{
    public string ruleId = string.Empty;
    public string offerTypeId = string.Empty;
    public int dataId = -1;
    public string requiredTag = string.Empty;
    public int maximumPrice;
    public int maximumOwned = 1;
    public int priority;
    public bool enabled = true;

    public ProcurementWishlistRule Clone()
    {
        return new ProcurementWishlistRule
        {
            ruleId = ruleId,
            offerTypeId = offerTypeId,
            dataId = dataId,
            requiredTag = requiredTag,
            maximumPrice = maximumPrice,
            maximumOwned = maximumOwned,
            priority = priority,
            enabled = enabled
        };
    }
}

[Serializable]
public sealed class AutoProcurementResult
{
    public int day;
    public string ruleId = string.Empty;
    public string itemLabel = string.Empty;
    public int quantity;
    public int cost;
    public bool purchased;
    public string reason = string.Empty;
}

[Serializable]
public sealed class AutoProcurementSaveData
{
    public int dailyBudget = 500;
    public int minimumReserve;
    public int lastProcessedDay;
    public List<AutoProcurementRule> stockRules =
        new List<AutoProcurementRule>();
    public List<ProcurementWishlistRule> wishlistRules =
        new List<ProcurementWishlistRule>();
    public List<string> processedOfferKeys = new List<string>();
    public List<AutoProcurementResult> lastResults =
        new List<AutoProcurementResult>();
}

[Serializable]
public sealed class PaidFacilityContractState
{
    public string contractId = string.Empty;
    public string facilityPersistentId = string.Empty;
    public int dailyCost;
    public bool active = true;
    public int lastSettledDay;
}

[Serializable]
public sealed class PaidFacilityContractSaveData
{
    public List<PaidFacilityContractState> contracts =
        new List<PaidFacilityContractState>();
    public List<string> chargedOrderKeys = new List<string>();
}

[Serializable]
public sealed class TreasuryEconomySaveData
{
    public EconomyTransactionLedgerSaveData transactionLedger =
        new EconomyTransactionLedgerSaveData();
    public EmploymentContractSaveData employment =
        new EmploymentContractSaveData();
    public AutoProcurementSaveData autoProcurement =
        new AutoProcurementSaveData();
    public PaidFacilityContractSaveData facilityContracts =
        new PaidFacilityContractSaveData();
    public EquipmentOverclockSaveData overclock =
        new EquipmentOverclockSaveData();
    public TreasuryDefenseSaveData treasuryDefense =
        new TreasuryDefenseSaveData();
}

public enum ReforgePrecisionOption
{
    PreciseCalibration = 0,
    BurdenSuppression = 1,
    ExternalTechnicalSupport = 2
}

[Serializable]
public sealed class ReforgePrecisionSelection
{
    public bool preciseCalibration;
    public bool burdenSuppression;
    public bool externalTechnicalSupport;
    public string suppressedBurdenEffectId = string.Empty;

    public int SelectedCount =>
        (preciseCalibration ? 1 : 0)
        + (burdenSuppression ? 1 : 0)
        + (externalTechnicalSupport ? 1 : 0);
}

public enum OverclockTier
{
    None = 0,
    Controlled = 1,
    Aggressive = 2,
    Critical = 3
}

public enum OverclockTargetKind
{
    Equipment = 0,
    Facility = 1
}

[Serializable]
public sealed class OverclockState
{
    public OverclockTargetKind targetKind;
    public string targetId = string.Empty;
    public OverclockTier tier;
    public float remainingGameSeconds;
    public float overload;
    public int activationCost;

    public bool Active => tier != OverclockTier.None
        && remainingGameSeconds > 0f;

    public OverclockState Clone()
    {
        return new OverclockState
        {
            targetKind = targetKind,
            targetId = targetId,
            tier = tier,
            remainingGameSeconds = remainingGameSeconds,
            overload = overload,
            activationCost = activationCost
        };
    }
}

[Serializable]
public sealed class EquipmentOverclockSaveData
{
    public List<OverclockState> states = new List<OverclockState>();
}

[Serializable]
public sealed class TreasuryDefensePolicy
{
    public string facilityPersistentId = string.Empty;
    public bool automaticFire = true;
    public bool bossOnly;
    public int minimumThreat;
    public int invasionBudget = 300;
    public int protectedFunds = -1;

    public TreasuryDefensePolicy Clone()
    {
        return new TreasuryDefensePolicy
        {
            facilityPersistentId = facilityPersistentId,
            automaticFire = automaticFire,
            bossOnly = bossOnly,
            minimumThreat = minimumThreat,
            invasionBudget = invasionBudget,
            protectedFunds = protectedFunds
        };
    }
}

[Serializable]
public sealed class TreasuryDefenseInvasionSpendState
{
    public string invasionId = string.Empty;
    public string facilityPersistentId = string.Empty;
    public int spent;

    public TreasuryDefenseInvasionSpendState Clone()
    {
        return new TreasuryDefenseInvasionSpendState
        {
            invasionId = invasionId,
            facilityPersistentId = facilityPersistentId,
            spent = spent
        };
    }
}

[Serializable]
public sealed class TreasuryDefenseSaveData
{
    public List<TreasuryDefensePolicy> policies =
        new List<TreasuryDefensePolicy>();
    public List<TreasuryDefenseInvasionSpendState> invasionSpending =
        new List<TreasuryDefenseInvasionSpendState>();
}
