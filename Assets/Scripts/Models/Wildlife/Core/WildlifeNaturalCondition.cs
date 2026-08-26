using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WildlifeNaturalCondition
{
    private float fear;
    private float hunger;
    private float thirst;
    private WildlifeIntent intent = WildlifeIntent.Wander;
    private string intentReason = string.Empty;
    private Vector2Int territoryCenter;
    private Vector2Int herdAnchorPosition;
    private Vector2Int lastThreatPosition;
    private bool hasLastThreatPosition;
    private float lastThreatTime;

    public float Fear => fear;
    public float Hunger => hunger;
    public float Thirst => thirst;
    public WildlifeIntent Intent => intent;
    public string IntentReason => intentReason;
    public Vector2Int TerritoryCenter => territoryCenter;
    public Vector2Int HerdAnchorPosition => herdAnchorPosition;
    public bool HasLastThreatPosition => hasLastThreatPosition;
    public Vector2Int LastThreatPosition => lastThreatPosition;

    public void Initialize(
        WildlifeSaveData saveData,
        Vector2Int spawnPosition,
        float now,
        float initialHunger,
        float initialThirst)
    {
        fear = saveData != null ? Mathf.Max(0f, saveData.fear) : 0f;
        hunger = saveData != null
            ? Mathf.Clamp01(saveData.hunger)
            : Mathf.Clamp01(initialHunger);
        thirst = saveData != null
            ? Mathf.Clamp01(saveData.thirst)
            : Mathf.Clamp01(initialThirst);
        intent = saveData != null ? saveData.intent : WildlifeIntent.Wander;
        intentReason = saveData?.intentReason ?? string.Empty;
        territoryCenter = saveData != null && saveData.hasTerritory
            ? new Vector2Int(saveData.territoryX, saveData.territoryY)
            : spawnPosition;
        herdAnchorPosition = saveData != null && saveData.hasHerdAnchor
            ? new Vector2Int(saveData.herdAnchorX, saveData.herdAnchorY)
            : territoryCenter;
        hasLastThreatPosition = saveData != null && saveData.hasLastThreat;
        lastThreatPosition = hasLastThreatPosition
            ? new Vector2Int(saveData.lastThreatX, saveData.lastThreatY)
            : spawnPosition;
        lastThreatTime = hasLastThreatPosition ? now : 0f;
    }

    public float GetLastThreatAge(float now)
    {
        return hasLastThreatPosition
            ? Mathf.Max(0f, now - lastThreatTime)
            : float.MaxValue;
    }

    public void AddFear(float amount)
    {
        fear = Mathf.Max(0f, fear + Mathf.Max(0f, amount));
    }

    public void SetFear(float value)
    {
        fear = Mathf.Max(0f, value);
    }

    public void RegisterThreat(
        Vector2Int position,
        float intensity,
        float fearSensitivity,
        float now)
    {
        hasLastThreatPosition = true;
        lastThreatPosition = position;
        lastThreatTime = now;
        fear = Mathf.Clamp(
            fear + Mathf.Max(0.1f, intensity) * Mathf.Max(0f, fearSensitivity),
            0f,
            12f);
    }

    public void SetHerdAnchor(Vector2Int position)
    {
        herdAnchorPosition = position;
    }

    public void SetTerritoryCenter(Vector2Int position)
    {
        territoryCenter = position;
    }

    public void AdvanceNeeds(
        float deltaTime,
        float hungerPerSecond,
        float thirstPerSecond)
    {
        hunger = Mathf.Clamp01(
            hunger + Mathf.Max(0f, hungerPerSecond) * Mathf.Max(0f, deltaTime));
        thirst = Mathf.Clamp01(
            thirst + Mathf.Max(0f, thirstPerSecond) * Mathf.Max(0f, deltaTime));
    }

    public void SatisfyNeeds(float food, float water)
    {
        hunger = Mathf.Clamp01(hunger - Mathf.Max(0f, food));
        thirst = Mathf.Clamp01(thirst - Mathf.Max(0f, water));
    }

    public void SetIntent(WildlifeIntent newIntent, string reason)
    {
        intent = newIntent;
        intentReason = reason ?? string.Empty;
    }

    public void ChangeHunger(float delta)
    {
        hunger = Mathf.Clamp01(hunger + delta);
    }

    public void SetHunger(float value)
    {
        hunger = Mathf.Clamp01(value);
    }

    public void ChangeThirst(float delta)
    {
        thirst = Mathf.Clamp01(thirst + delta);
    }

    public void Tick(
        float deltaTime,
        float dailyFoodNeed,
        float dailyWaterNeed,
        float now)
    {
        float elapsed = Mathf.Max(0f, deltaTime);
        hunger = Mathf.Clamp01(
            hunger + elapsed * Mathf.Max(0.1f, dailyFoodNeed) / 300f);
        thirst = Mathf.Clamp01(
            thirst + elapsed * Mathf.Max(0.1f, dailyWaterNeed) / 240f);
        fear = Mathf.Max(0f, fear - elapsed * 0.08f);
        if (hasLastThreatPosition && now - lastThreatTime > 90f)
        {
            hasLastThreatPosition = false;
        }
    }

    public void CaptureInto(WildlifeSaveData saveData)
    {
        saveData.fear = fear;
        saveData.hunger = hunger;
        saveData.thirst = thirst;
        saveData.intent = intent;
        saveData.intentReason = intentReason;
        saveData.hasTerritory = true;
        saveData.territoryX = territoryCenter.x;
        saveData.territoryY = territoryCenter.y;
        saveData.hasHerdAnchor = true;
        saveData.herdAnchorX = herdAnchorPosition.x;
        saveData.herdAnchorY = herdAnchorPosition.y;
        saveData.hasLastThreat = hasLastThreatPosition;
        saveData.lastThreatX = lastThreatPosition.x;
        saveData.lastThreatY = lastThreatPosition.y;
    }
}
