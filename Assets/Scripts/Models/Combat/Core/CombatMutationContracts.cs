using System;

public readonly struct CombatOutcomeApplyResult
{
    private CombatOutcomeApplyResult(bool succeeded, string failureReason)
    {
        Succeeded = succeeded;
        FailureReason = failureReason ?? string.Empty;
    }

    public bool Succeeded { get; }
    public string FailureReason { get; }
    public static CombatOutcomeApplyResult Success() =>
        new CombatOutcomeApplyResult(true, string.Empty);
    public static CombatOutcomeApplyResult Failed(string failureReason) =>
        new CombatOutcomeApplyResult(false, failureReason);
}

public readonly struct CombatOutcomeMechanicalMutation
{
    public CombatOutcomeMechanicalMutation(
        Func<string> apply,
        Action rollback,
        Action complete)
    {
        Apply = apply ?? throw new ArgumentNullException(nameof(apply));
        Rollback = rollback ?? throw new ArgumentNullException(nameof(rollback));
        Complete = complete ?? throw new ArgumentNullException(nameof(complete));
    }

    public Func<string> Apply { get; }
    public Action Rollback { get; }
    public Action Complete { get; }
    public bool IsValid => Apply != null && Rollback != null && Complete != null;
}
