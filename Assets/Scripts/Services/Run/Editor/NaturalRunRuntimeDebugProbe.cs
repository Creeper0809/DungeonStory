using System;
using System.Linq;
using System.Text;
using UnityEngine;
using VContainer;

public static class NaturalRunRuntimeDebugProbe
{
    public static string Summarize()
    {
        StringBuilder builder = new StringBuilder();
        DungeonRuntimeLifetimeScope scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        GameData gameData = null;
        IDungeonRunFlowRuntime flow = null;
        if (scope != null)
        {
            try
            {
                scope.Container.Resolve<IGameDataProvider>().TryGetGameData(out gameData);
                flow = scope.Container.Resolve<IDungeonRunFlowRuntime>();
            }
            catch (Exception exception)
            {
                builder.AppendLine($"resolve failed: {exception.Message}");
            }
        }

        OwnerRunManager ownerManager = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        CharacterActor owner = ownerManager != null ? ownerManager.CurrentOwnerActor : null;
        NaturalRunVerificationRunner[] runners =
            UnityEngine.Object.FindObjectsByType<NaturalRunVerificationRunner>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        CharacterActor[] actors = UnityEngine.Object.FindObjectsByType<CharacterActor>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        builder.AppendLine(
            $"scope={scope != null}; runners={runners.Length}; actors={actors.Length}; "
            + $"day={gameData?.day.Value ?? -1}; speed={gameData?.gameSpeed.Value ?? -1}; "
            + $"timeScale={Time.timeScale:0.##}; phase={flow?.Phase.ToString() ?? "missing"}; "
            + $"outcome={flow?.Outcome.ToString() ?? "missing"}; "
            + $"owner={owner?.name ?? "missing"}; ownerHp={owner?.CurrentHealth ?? -1f:0.#}; "
            + $"realtime={Time.realtimeSinceStartup:0.0}");

        builder.AppendLine("actors="
            + string.Join(" | ", actors
                .OrderBy(actor => actor.Identity?.PersistentId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(actor => actor.GetInstanceID())
                .Take(16)
                .Select(DescribeActor)));
        return builder.ToString();
    }

    private static string DescribeActor(CharacterActor actor)
    {
        if (actor == null)
        {
            return "null";
        }

        actor.EnsureRuntimeState();
        CharacterIdentity identity = actor.Identity;
        AIBrain brain = actor.Brain;
        CharacterLifecycle lifecycle = actor.Lifecycle;
        return $"{actor.name}:id={identity?.PersistentId ?? "none"}"
            + $":type={identity?.CharacterType.ToString() ?? "none"}"
            + $":state={actor.CurrentLifecycleState}"
            + $":active={actor.gameObject.activeInHierarchy}"
            + $":hp={actor.CurrentHealth:0}/{actor.MaxHealth:0}"
            + $":stress={lifecycle?.ExpeditionRecovery?.stress ?? 0f:0}"
            + $":ai={brain?.CurrentActionDebugLabel ?? "none"}"
            + $":phase={brain?.CurrentActionPhase ?? string.Empty}";
    }
}
