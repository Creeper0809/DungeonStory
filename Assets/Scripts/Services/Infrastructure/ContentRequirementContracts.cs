using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;

public interface IContentRequirementEvaluator
{
    bool TryEvaluate(
        V20ContentRequirementSet requirements,
        RunMilestoneEvaluationSnapshot world,
        IReadOnlyList<string> participantCharacterIds,
        out DomainFailure failure);

    bool IsLivingCharacterAtStage(
        CharacterId characterId,
        CharacterLifeStage lifeStage);
}

