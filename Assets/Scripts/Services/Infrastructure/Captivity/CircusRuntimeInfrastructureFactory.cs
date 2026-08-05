internal static class CircusRuntimeInfrastructureFactory
{
    internal static ICircusMovementCommands CreateMovement(
        CircusProgramContext program,
        CircusWorldContext worldContext,
        CircusSessionContext session) =>
        new CircusMovementCoordinator(program, worldContext, session);

    internal static ICircusRestoreLifecycle CreateRestore(
        CircusProgramContext program,
        CircusWorldContext worldContext,
        CircusRestoreStateContext state) =>
        new CircusRestoreCoordinator(program, worldContext, state);
}
