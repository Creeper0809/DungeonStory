using UnityEngine;

public sealed class FactionReinforcementMarker : MonoBehaviour
{
    [SerializeField] private string routeId = string.Empty;
    [SerializeField] private string factionId = string.Empty;
    [SerializeField, Range(0, 100)] private int routeStrength = 100;

    public string RouteId => routeId;
    public string FactionId => factionId;
    public int RouteStrength => routeStrength;

    public void Configure(
        string reinforcementRouteId,
        string reinforcementFactionId,
        int strength)
    {
        routeId = reinforcementRouteId?.Trim() ?? string.Empty;
        factionId = reinforcementFactionId?.Trim() ?? string.Empty;
        routeStrength = Mathf.Clamp(strength, 0, 100);
    }
}
