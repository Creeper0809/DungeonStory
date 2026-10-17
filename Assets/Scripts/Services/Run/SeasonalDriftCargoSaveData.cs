using System;
using System.Collections.Generic;


[Serializable]
public sealed class SeasonalDriftCargoSaveData
{
    public bool configured;
    public string itemId = string.Empty;
    public int exactQuantity;
    public bool positionFrozen;
    public int positionX;
    public int positionY;
    public bool spawned;
    public List<string> sourceStackIds = new();
    public int securedQuantity;
    public bool expirationCompleted;
    public string dispositionOperationId = string.Empty;
    public string dispositionCommitId = string.Empty;
    public List<string> dispositionSourceStackIds = new();
    public int expiredQuantity;
    public long expiredMassGrams;
    public string lastFailureReason = string.Empty;
    public string outcomeResultKey = string.Empty;
    public SeasonalDriftCargoFactData observation = new();
    public SeasonalTerminalPublicationSaveData publication = new();

    public static SeasonalDriftCargoSaveData FromProfile(
        SeasonalDriftCargoProfile profile) => profile?.IsConfigured == true
        ? new SeasonalDriftCargoSaveData
        {
            configured = true,
            itemId = profile.itemId,
            exactQuantity = profile.exactQuantity
        }
        : new SeasonalDriftCargoSaveData();
}
