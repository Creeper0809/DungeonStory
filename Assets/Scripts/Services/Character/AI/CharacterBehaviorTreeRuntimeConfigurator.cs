using System;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

public interface ICharacterBehaviorTreeRuntimeConfigurator
{
    BehaviorTree Configure(CharacterActor actor, ExternalBehaviorTree externalBehavior);
}

public sealed class CharacterBehaviorTreeRuntimeConfigurator : ICharacterBehaviorTreeRuntimeConfigurator
{
    public BehaviorTree Configure(CharacterActor actor, ExternalBehaviorTree externalBehavior)
    {
        if (actor == null)
        {
            throw new ArgumentNullException(nameof(actor));
        }

        BehaviorTree behaviorTree = actor.GetComponent<BehaviorTree>();
        if (behaviorTree == null)
        {
            behaviorTree = actor.gameObject.AddComponent<BehaviorTree>();
        }

        if (externalBehavior != null && behaviorTree.ExternalBehavior != externalBehavior)
        {
            behaviorTree.ExternalBehavior = externalBehavior;
            behaviorTree.DungeonStoryReloadExternalBehaviorForRuntime();
        }

        behaviorTree.StartWhenEnabled = false;
        if (Application.isPlaying)
        {
            behaviorTree.enabled = true;
            if (behaviorTree.ExternalBehavior != null)
            {
                if (behaviorTree.ExecutionStatus != TaskStatus.Running)
                {
                    behaviorTree.EnableBehavior();
                }
            }
        }

        return behaviorTree;
    }
}
