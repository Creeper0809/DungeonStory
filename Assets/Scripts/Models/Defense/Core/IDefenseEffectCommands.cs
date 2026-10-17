public interface IDefenseEffectCommands
{
    string CombatLogText { get; }
    void ApplyDamage(float amount, string reason);
    int ApplyStatus(DefenseStatusKind kind, float value, float duration, int stacks);
    void ClearStatus(DefenseStatusKind kind);
    void AddMovementDelay(float seconds);
    void AddEffectTag(string tag);
    void RecordEnvironmentalIgnition(float ignitionIntensity);
}
