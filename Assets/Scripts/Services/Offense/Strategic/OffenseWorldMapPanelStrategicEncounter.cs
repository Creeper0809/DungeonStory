using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class OffenseWorldMapPanel
{
    private void RenderStrategicDecision(
        OffenseExpeditionRun expedition,
        OffenseDecisionView decision)
    {
        TMP_Text title = CreateMapText(
            "DecisionTitle",
            decision.title,
            31f,
            TextAlignmentOptions.Center,
            new Vector2(0.08f, 0.68f),
            new Vector2(0.92f, 0.92f));
        title.color = new Color(0.92f, 0.84f, 0.68f, 1f);
        CreateMapText(
            "DecisionSituation",
            strategicNarrativeText.GetRequired(
                InGameNarrativeTextKind.ExpeditionCard,
                decision.cardId),
            22f,
            TextAlignmentOptions.Center,
            new Vector2(0.14f, 0.4f),
            new Vector2(0.86f, 0.68f));

        OffenseDecisionChoiceView[] choices =
            decision.choices?.Take(2).ToArray()
            ?? Array.Empty<OffenseDecisionChoiceView>();
        for (int index = 0; index < choices.Length; index++)
        {
            OffenseDecisionChoiceView choice = choices[index];
            float left = index == 0 ? 0.08f : 0.52f;
            CreateMapButton(
                $"{choice.Label}\n{choice.DirectionLabel}",
                new Vector2(left, 0.12f),
                new Vector2(left + 0.4f, 0.38f),
                () =>
                {
                    RequireStrategicExpedition().TryResolveDecision(
                        expedition.ExpeditionId,
                        choice.ChoiceId,
                        out strategicStatus);
                    RenderStrategic();
                },
                index == 0
                    ? new Color(0.18f, 0.3f, 0.34f, 1f)
                    : new Color(0.38f, 0.2f, 0.18f, 1f));
        }

        AddRightButton("사건 선택 대기", () => { });
        AddRightButton("닫기", Hide);
        OffenseReturnSafetySnapshot safety = strategicSafety.Get(expedition.ExpeditionId);
        detailText.text =
            $"단계: {GetDecisionStageLabel(decision.stage)}\n"
            + $"안전 이동: {safety.SafeStepBudget}칸\n\n"
            + string.Join(
                "\n\n",
                choices.Select(choice =>
                    $"{choice.Label}\n"
                    + strategicNarrativeText.GetRequired(
                        InGameNarrativeTextKind.ExpeditionChoice,
                        InGameNarrativeTextCatalogSO.ComposeExpeditionChoiceStableId(
                            decision.cardId,
                            choice.ChoiceId))
                    + "\n"
                    + $"{choice.DirectionLabel}"))
            + BuildStatusText();
    }

    private void RenderStrategicBattle(OffenseExpeditionRun expedition)
    {
        OffenseBattleDirectorStateData state = strategicBattleDirector.State;
        CreateMapText(
            "BattleTitle",
            $"명령열 전투 · 턴 {state.turn}",
            29f,
            TextAlignmentOptions.Center,
            new Vector2(0.08f, 0.9f),
            new Vector2(0.92f, 0.99f));

        for (int intentIndex = 0;
             intentIndex < state.enemyIntents.Count;
             intentIndex++)
        {
            OffenseEnemyIntentStateData intent = state.enemyIntents[intentIndex];
            float width = 0.8f / Mathf.Max(1, state.enemyIntents.Count);
            float left = 0.1f + width * intentIndex;
            CreateMapButton(
                $"{GetCombatantName(intent.enemyId)}\n"
                + $"{GetTagLabel(intent.tacticalTag)} {intent.executionStages}단계",
                new Vector2(left, 0.7f),
                new Vector2(left + width - 0.015f, 0.86f),
                () => CommitPendingCard(intent),
                string.IsNullOrWhiteSpace(pendingCardInstanceId)
                    ? new Color(0.28f, 0.14f, 0.15f, 1f)
                    : new Color(0.52f, 0.2f, 0.16f, 1f));
        }

        for (int deckIndex = 0; deckIndex < state.decks.Count; deckIndex++)
        {
            OffenseCommandDeckStateData deck = state.decks[deckIndex];
            float rowTop = 0.62f - deckIndex * 0.115f;
            CreateMapText(
                $"DeckName_{deckIndex}",
                GetCombatantName(deck.characterId),
                16f,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.04f, rowTop - 0.085f),
                new Vector2(0.19f, rowTop));

            bool committed = state.commandQueue.Any(entry =>
                entry.characterId == deck.characterId);
            for (int cardIndex = 0;
                 cardIndex < deck.candidates.Count;
                 cardIndex++)
            {
                OffenseCommandCardStateData card = deck.candidates[cardIndex];
                float left = 0.2f + cardIndex * 0.31f;
                bool selected = pendingCardInstanceId == card.instanceId;
                CreateMapButton(
                    $"{card.displayName}\n"
                    + $"{GetTagLabel(card.tacticalTag)} · {card.executionStages}단계"
                    + $" · 속도 {card.speed}",
                    new Vector2(left, rowTop - 0.09f),
                    new Vector2(left + 0.29f, rowTop),
                    () =>
                    {
                        if (!committed)
                        {
                            pendingCardCharacterId = deck.characterId;
                            pendingCardInstanceId = card.instanceId;
                            strategicStatus = "맞대응할 적 의도를 선택하세요.";
                        }

                        RenderStrategic();
                    },
                    committed
                        ? new Color(0.12f, 0.15f, 0.16f, 0.45f)
                        : selected
                            ? GetTagColor(card.tacticalTag)
                            : string.IsNullOrWhiteSpace(
                                pendingCardInstanceId)
                                ? new Color(0.2f, 0.23f, 0.26f, 1f)
                                : new Color(0.11f, 0.13f, 0.15f, 0.32f));
            }

            if (committed)
            {
                CreateMapButton(
                    "명령 취소",
                    new Vector2(0.83f, rowTop - 0.09f),
                    new Vector2(0.96f, rowTop),
                    () =>
                    {
                        strategicBattleDirector.TryRemoveCommittedCommand(
                            deck.characterId);
                        RenderStrategic();
                    },
                    new Color(0.32f, 0.2f, 0.18f, 1f));
            }
        }

        AddRightButton(
            $"명령 실행 {state.commandQueue.Count}/{state.decks.Count}",
            () =>
            {
                if (state.commandQueue.Count == 0)
                {
                    strategicStatus = "실행할 명령이 없습니다.";
                }
                else
                {
                    List<CombatCardPresentationRecipe> presentations =
                        BuildCardPresentationRecipes(state);
                    IReadOnlyList<OffenseResolvedCommand> resolved =
                        strategicBattleDirector.ResolveTurn();
                    ApplyCardPresentationResults(presentations, resolved);
                    strategicCardPresentation.Present(presentations);
                    strategicStatus = BuildResolutionSummary(resolved);
                    pendingCardCharacterId = string.Empty;
                    pendingCardInstanceId = string.Empty;
                    if (strategicBattleDirector.State != null
                        && strategicBattleRuntime.HasActiveBattle)
                    {
                        strategicBattleDirector.TryReplaceEnemyIntents(
                            OffenseStrategicBattleSetupFactory.CreateEnemyIntents(
                                strategicBattleRuntime.Session,
                                state.turn + 1),
                            out _);
                        strategicBattleDirector.TryDrawTurn(out _);
                    }
                }

                RenderStrategic();
            },
            new Color(0.48f, 0.2f, 0.14f, 1f));
        AddRightButton("카드 선택 해제", ClearPendingCard);
        AddRightButton("닫기", Hide);

        detailText.text = BuildBattleSidebar(state) + BuildStatusText();
    }

    private List<CombatCardPresentationRecipe> BuildCardPresentationRecipes(
        OffenseBattleDirectorStateData state)
    {
        List<CombatCardPresentationRecipe> recipes =
            new List<CombatCardPresentationRecipe>();
        if (state?.commandQueue == null)
        {
            return recipes;
        }

        HashSet<string> interceptedIntentIds =
            new HashSet<string>(StringComparer.Ordinal);
        foreach (OffenseCommandQueueEntryData entry in state.commandQueue
                     .Where(item => item != null)
                     .OrderBy(item => item.order))
        {
            OffenseCommandDeckStateData deck = state.decks?.FirstOrDefault(
                item => item != null
                    && item.characterId == entry.characterId);
            OffenseCommandCardStateData card = deck?.candidates?.FirstOrDefault(
                item => item != null
                    && item.instanceId == entry.cardInstanceId);
            OffenseEnemyIntentStateData intent =
                state.enemyIntents?.FirstOrDefault(
                    item => item != null
                        && item.intentId == entry.targetIntentId);
            if (card == null)
            {
                continue;
            }

            bool firstInterception = intent != null
                && interceptedIntentIds.Add(intent.intentId);
            recipes.Add(new CombatCardPresentationRecipe
            {
                allyName = GetCombatantName(entry.characterId),
                enemyName = GetCombatantName(
                    intent?.enemyId ?? entry.targetCombatantId),
                commandName = card.displayName,
                tacticalTag = card.tacticalTag,
                damageType = card.damageType,
                allyStages = card.executionStages,
                enemyStages = firstInterception
                    ? intent.executionStages
                    : 0,
                allyStagesRemaining = card.executionStages,
                enemyStagesRemaining = firstInterception
                    ? intent.executionStages
                    : 0,
                ultimate = false
            });
        }

        foreach (OffenseEnemyIntentStateData intent in state.enemyIntents
                     .Where(item => item != null
                         && !interceptedIntentIds.Contains(item.intentId)))
        {
            recipes.Add(new CombatCardPresentationRecipe
            {
                allyName = GetCombatantName(intent.targetCharacterId),
                enemyName = GetCombatantName(intent.enemyId),
                commandName = "대응 없음",
                tacticalTag = intent.tacticalTag,
                damageType = CombatDamageType.Blunt,
                allyStages = 0,
                enemyStages = intent.executionStages,
                allyStagesRemaining = 0,
                enemyStagesRemaining = intent.executionStages,
                ultimate = false
            });
        }

        return recipes;
    }

    private static void ApplyCardPresentationResults(
        IReadOnlyList<CombatCardPresentationRecipe> recipes,
        IReadOnlyList<OffenseResolvedCommand> resolved)
    {
        if (recipes == null || resolved == null)
        {
            return;
        }

        int count = Mathf.Min(recipes.Count, resolved.Count);
        for (int index = 0; index < count; index++)
        {
            CombatCardPresentationRecipe recipe = recipes[index];
            OffenseResolvedCommand command = resolved[index];
            recipe.allyStagesRemaining = command.clash.AllyStagesRemaining;
            recipe.enemyStagesRemaining = command.clash.EnemyStagesRemaining;
        }
    }

    private void CommitPendingCard(OffenseEnemyIntentStateData intent)
    {
        if (string.IsNullOrWhiteSpace(pendingCardCharacterId)
            || string.IsNullOrWhiteSpace(pendingCardInstanceId))
        {
            strategicStatus = "먼저 아군 명령 카드를 선택하세요.";
            RenderStrategic();
            return;
        }

        if (strategicBattleDirector.TryCommitCommand(
                pendingCardCharacterId,
                pendingCardInstanceId,
                intent.intentId,
                intent.enemyId,
                out string reason))
        {
            strategicStatus = "명령열에 추가했습니다.";
            pendingCardCharacterId = string.Empty;
            pendingCardInstanceId = string.Empty;
        }
        else
        {
            strategicStatus = reason;
        }

        RenderStrategic();
    }

    private void RenderStrategicSidebar(OffenseExpeditionRun expedition)
    {
        if (expedition == null)
        {
            RenderStrategicPreparationSidebar();
            return;
        }

        if (strategicFieldMedical != null
            && strategicFieldMedical.TryGetStrandedState(
                expedition.ExpeditionId,
                out OffenseStrandedState stranded))
        {
            RenderStrategicRescuePreparationSidebar(expedition, stranded);
            return;
        }

        strategicTravel.TryGetState(
            expedition.ExpeditionId,
            out OffenseTravelStateData travel);
        OffenseReturnSafetySnapshot safety = strategicSafety.Get(expedition.ExpeditionId);
        OffenseSupplyPackingSnapshot packing =
            strategicPreparation.GetPackingSnapshot(expedition.ExpeditionId);
        AddPreparationAction(
            "선택한 칸으로 이동",
            () =>
            {
                if (travel == null)
                {
                    strategicStatus = "이동 상태를 찾을 수 없습니다.";
                }
                else if (selectedStrategicCoord == travel.CurrentCoord)
                {
                    strategicStatus = "원정대가 이미 이 칸에 있습니다.";
                }
                else
                {
                    bool startsAttack = IsSelectedActiveSite(
                        out string destinationSiteId);
                    if (!RequireStrategicExpedition().TryRedirectExpedition(
                            expedition.ExpeditionId,
                            selectedStrategicCoord,
                            startsAttack ? destinationSiteId : string.Empty,
                            startsAttack,
                            out strategicStatus))
                    {
                        RenderStrategic();
                        return;
                    }

                }

                RenderStrategic();
            });
        AddUrgentMitigationButtonIfSelected();
        AddFieldMedicalButtons(expedition);
        AddPreparationAction("세력", OpenStrategicFactionSurface);
        AddPreparationAction("지도 맞춤", ResetStrategicMapView);
        AddPreparationAction("닫기", Hide);

        string destination = travel != null
            ? $"({travel.destinationQ}, {travel.destinationR})"
            : "-";
        int remaining = travel?.remainingPath?.Count ?? 0;
        SetPreparationDetail(
            $"원정대 이동\n"
            + $"현재: ({travel?.currentQ ?? 0}, {travel?.currentR ?? 0})\n"
            + $"목적지: {destination}\n"
            + $"남은 이동: {remaining}칸\n"
            + $"안전 이동: {safety.SafeStepBudget}칸\n"
            + $"노출도: {travel?.exposure ?? 0f:0.#}\n"
            + BuildPackingStatus(packing)
            + $"단계: {GetExpeditionPhaseLabel(expedition.Phase)}\n\n"
            + BuildFieldMedicalDetail(expedition)
            + BuildSelectedLocationDetail()
            + BuildStatusText());
    }

    private void RenderStrategicRescuePreparationSidebar(
        OffenseExpeditionRun strandedExpedition,
        OffenseStrandedState stranded)
    {
        IReadOnlyList<CharacterActor> available = RequireStrategicExpedition()
            .GetAvailableMemberActors();
        selectedStrategicMembers.RemoveAll(member =>
            member == null || !available.Contains(member));

        AddPreparationAction("구조대 편성", () => { });
        foreach (CharacterActor actor in available.Take(8))
        {
            CharacterActor captured = actor;
            bool selected = selectedStrategicMembers.Contains(captured);
            AddPreparationAction(
                $"{(selected ? "●" : "○")} {GetActorLabel(captured)}",
                () =>
                {
                    if (selectedStrategicMembers.Contains(captured))
                    {
                        selectedStrategicMembers.Remove(captured);
                    }
                    else if (selectedStrategicMembers.Count < 5)
                    {
                        selectedStrategicMembers.Add(captured);
                    }
                    else
                    {
                        strategicStatus = "구조대는 최대 5명입니다.";
                    }

                    RenderStrategic();
                },
                selected
                    ? new Color(0.2f, 0.42f, 0.32f, 1f)
                    : new Color(0.18f, 0.2f, 0.23f, 1f));
        }

        AddStrategicSupplyButtons();
        AddPreparationAction(
            "구조대 파견",
            () =>
            {
                if (selectedStrategicMembers.Count == 0)
                {
                    strategicStatus = "구조대원을 한 명 이상 선택하세요.";
                }
                else if (TryStartStrategicRescueExpedition(
                    strandedExpedition.ExpeditionId,
                    out string message))
                {
                    selectedStrategicMembers.Clear();
                    ResetStrategicSupplies();
                    selectedStrategicFieldFunds = 0;
                    strategicStatus = message;
                }
                else
                {
                    strategicStatus = message;
                }

                RenderStrategic();
            },
            new Color(0.48f, 0.2f, 0.16f, 1f));
        AddPreparationAction("지도 맞춤", ResetStrategicMapView);
        AddPreparationAction("닫기", Hide);

        SetPreparationDetail(
            "조난 원정 구조\n"
            + $"위치: ({stranded.q}, {stranded.r})\n"
            + $"남은 보급: {stranded.remainingSupply:0.#}\n"
            + $"예상 생존: {stranded.estimatedSurvivalHours:0.#}시간\n"
            + $"원인: {stranded.reason}\n\n"
            + $"구조 대상: {strandedExpedition.MemberActors.Count}명\n"
            + $"구조대: {selectedStrategicMembers.Count}/5\n"
            + BuildSelectedSupplyDetail()
            + "구조대는 같은 육각 이동·사건·전투 규칙을 사용합니다. "
            + "합류해도 새 안전 이동 예산은 생기지 않습니다."
            + BuildStatusText());
    }

    private bool TryStartStrategicRescueExpedition(
        string strandedExpeditionId,
        out string message)
    {
        OffenseExpeditionPreparation source =
            strategicPreparation.Evaluate().Preparation;
        OffenseExpeditionPreparation preparation =
            new OffenseExpeditionPreparation(
                source.SupplyCapacity,
                source.StartingLight,
                source.CampHealRatio,
                source.CampStressRecovery,
                source.MedicineHealRatio,
                source.Scouting,
                source.SourceSummaries,
                fieldFunds: 0);
        return RequireStrategicExpedition().TryStartExpedition(
            $"rescue:{strandedExpeditionId}",
            selectedStrategicMembers,
            new OffenseSupplyLoadout(selectedStrategicSupplies),
            preparation,
            out _,
            out message);
    }

    private void RenderStrategicPreparationSidebar()
    {
        selectedStrategicMembers.RemoveAll(member =>
            member == null
            || !RequireStrategicExpedition().GetAvailableMemberActors().Contains(member));
        AddPreparationAction("원정대 편성", () => { });
        foreach (CharacterActor actor in RequireStrategicExpedition()
                     .GetAvailableMemberActors()
                     .Take(8))
        {
            CharacterActor captured = actor;
            bool selected = selectedStrategicMembers.Contains(captured);
            AddPreparationAction(
                $"{(selected ? "●" : "○")} {GetActorLabel(captured)}",
                () =>
                {
                    if (selectedStrategicMembers.Contains(captured))
                    {
                        selectedStrategicMembers.Remove(captured);
                    }
                    else if (selectedStrategicMembers.Count < 5)
                    {
                        selectedStrategicMembers.Add(captured);
                    }
                    else
                    {
                        strategicStatus = "원정대는 최대 5명입니다.";
                    }

                    RenderStrategic();
                },
                selected
                    ? new Color(0.2f, 0.42f, 0.32f, 1f)
                    : new Color(0.18f, 0.2f, 0.23f, 1f));
        }

        AddStrategicSupplyButtons();

        AddPreparationAction(
            $"현장 자금 -100  ({selectedStrategicFieldFunds})",
            () =>
            {
                selectedStrategicFieldFunds = Mathf.Max(
                    0,
                    selectedStrategicFieldFunds - 100);
                RenderStrategic();
            });
        AddPreparationAction(
            $"현장 자금 +100  ({selectedStrategicFieldFunds})",
            () =>
            {
                selectedStrategicFieldFunds += 100;
                RenderStrategic();
            });
        AddIntelPurchaseButtonsIfSelected();

        AddPreparationAction(
            "선택 거점으로 출정",
            () =>
            {
                if (string.IsNullOrWhiteSpace(selectedWorldSiteId))
                {
                    strategicStatus = "지도에서 공격할 거점을 선택하세요.";
                }
                else if (TryStartPreparedStrategicExpedition(
                    out string message))
                {
                    selectedStrategicMembers.Clear();
                    ResetStrategicSupplies();
                    selectedStrategicFieldFunds = 0;
                    strategicStatus = message;
                }
                else
                {
                    strategicStatus = message;
                }

                RenderStrategic();
            },
            new Color(0.48f, 0.2f, 0.16f, 1f));
        AddUrgentMitigationButtonIfSelected();
        AddPreparationAction("세력", OpenStrategicFactionSurface);
        AddPreparationAction("지도 맞춤", ResetStrategicMapView);
        AddPreparationAction("닫기", Hide);

        SetPreparationDetail(
            $"{BuildSelectedLocationDetail()}\n\n"
            + $"선발 인원: {selectedStrategicMembers.Count}/5\n"
            + BuildSelectedSupplyDetail()
            + $"현장 자금: {selectedStrategicFieldFunds} 골드\n"
            + "전열 2 · 중열 2 · 후열 1 순으로 배치됩니다."
            + BuildThreatDetail()
            + BuildStatusText());
    }

    private bool TryStartPreparedStrategicExpedition(out string message)
    {
        OffenseExpeditionPreparation source =
            strategicPreparation.Evaluate().Preparation;
        OffenseExpeditionPreparation preparation =
            new OffenseExpeditionPreparation(
                source.SupplyCapacity,
                source.StartingLight,
                source.CampHealRatio,
                source.CampStressRecovery,
                source.MedicineHealRatio,
                source.Scouting,
                source.SourceSummaries,
                selectedStrategicFieldFunds);
        return RequireStrategicExpedition().TryStartExpedition(
            selectedWorldSiteId,
            selectedStrategicMembers,
            new OffenseSupplyLoadout(selectedStrategicSupplies),
            preparation,
            out _,
            out message);
    }

    private void AddStrategicSupplyButtons()
    {
        OffensePreparationSnapshot snapshot = strategicPreparation.Evaluate();
        int capacity = snapshot.Preparation.SupplyCapacity;
        foreach (OffenseSupplyType type in GetVisibleStrategicSupplyTypes())
        {
            OffenseSupplyType captured = type;
            int selected = selectedStrategicSupplies[captured];
            int available = snapshot.GetAvailable(captured);
            AddPreparationAction(
                $"{OffenseSupplyCatalog.GetDisplayName(captured)} {selected}/{available}",
                () =>
                {
                    int total = selectedStrategicSupplies.Values.Sum();
                    if (selectedStrategicSupplies[captured] > 0
                        && (selectedStrategicSupplies[captured] >= available
                            || total >= capacity))
                    {
                        selectedStrategicSupplies[captured] = 0;
                    }
                    else if (available > selectedStrategicSupplies[captured]
                        && total < capacity)
                    {
                        selectedStrategicSupplies[captured]++;
                    }
                    else
                    {
                        strategicStatus = "원정 보급 용량 또는 재고가 부족합니다.";
                    }

                    RenderStrategic();
                },
                selected > 0
                    ? new Color(0.24f, 0.38f, 0.3f, 1f)
                    : new Color(0.18f, 0.2f, 0.23f, 1f));
        }

        AddPreparationAction("보급 초기화", () =>
        {
            ResetStrategicSupplies();
            RenderStrategic();
        });
    }

    private IEnumerable<OffenseSupplyType> GetVisibleStrategicSupplyTypes()
    {
        yield return OffenseSupplyType.Rations;
        yield return OffenseSupplyType.Medicine;
        yield return OffenseSupplyType.Tools;
        yield return OffenseSupplyType.ManaLantern;
        foreach (OffenseSupplyType type in selectedStrategicMembers
                     .Where(member => member != null)
                     .Select(member => OffenseSupplyCatalog.GetFieldMedicalKit(
                         member.SpeciesTag))
                     .Distinct()
                     .OrderBy(type => (int)type))
        {
            yield return type;
        }
    }

    private void ResetStrategicSupplies()
    {
        foreach (OffenseSupplyType type in selectedStrategicSupplies.Keys.ToArray())
        {
            selectedStrategicSupplies[type] = 0;
        }
    }

    private string BuildSelectedSupplyDetail()
    {
        string[] lines = selectedStrategicSupplies
            .Where(pair => pair.Value > 0)
            .Select(pair => $"{OffenseSupplyCatalog.GetDisplayName(pair.Key)} {pair.Value}")
            .ToArray();
        return lines.Length == 0
            ? "보급: 없음\n"
            : $"보급: {string.Join(" · ", lines)}\n";
    }

    private void AddFieldMedicalButtons(OffenseExpeditionRun expedition)
    {
        if (expedition == null || strategicFieldMedical == null || strategicAnatomy == null)
        {
            return;
        }

        HashSet<string> stabilized = strategicFieldMedical
            .GetStabilizations(expedition.ExpeditionId)
            .Where(value => value.active || value.usedForNode)
            .Select(value => value.characterId + "\n" + value.anatomyNodeId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (CharacterActor member in expedition.MemberActors.Where(value => value != null))
        {
            string characterId = member.Identity?.PersistentId ?? string.Empty;
            AnatomyHealthSnapshot health = strategicAnatomy.GetAnatomySnapshot(member);
            strategicAnatomyProfiles.TryGet(health.ProfileId, out AnatomyProfileDefinition profile);
            foreach (AnatomyNodeHealthState node in health.Nodes
                         .Where(value => value != null
                             && (value.missing || value.currentHealth <= 0.01f))
                         .Where(value => !stabilized.Contains(
                             characterId + "\n" + value.nodeId))
                         .Take(2))
            {
                AnatomyNodeHealthState capturedNode = node;
                CharacterActor capturedMember = member;
                AnatomyNodeDefinition definition = null;
                if (profile != null)
                {
                    profile.TryGetNode(node.nodeId, out definition);
                }
                string partName = definition?.DisplayName ?? node.nodeId;
                OffenseSupplyType kit = OffenseSupplyCatalog.GetFieldMedicalKit(
                    member.SpeciesTag);
                AddPreparationAction(
                    $"가고정 · {GetActorLabel(member)} · {partName} "
                        + $"[{OffenseSupplyCatalog.GetDisplayName(kit)}]",
                    () =>
                    {
                        strategicFieldMedical.TryApplyPackedStabilization(
                            expedition,
                            capturedMember,
                            capturedNode.nodeId,
                            expedition.CompletedNodeIds.Count,
                            out strategicStatus);
                        RenderStrategic();
                    },
                    new Color(0.38f, 0.28f, 0.16f, 1f));
            }
        }
    }

    private string BuildFieldMedicalDetail(OffenseExpeditionRun expedition)
    {
        if (expedition == null || strategicFieldMedical == null)
        {
            return string.Empty;
        }

        int stabilized = strategicFieldMedical.GetStabilizations(expedition.ExpeditionId)
            .Count(value => value.active);
        int carried = strategicFieldMedical.GetCarries(expedition.ExpeditionId).Count;
        string stranded = strategicFieldMedical.IsStranded(expedition.ExpeditionId)
            ? " · 조난"
            : string.Empty;
        return stabilized > 0 || carried > 0 || stranded.Length > 0
            ? $"야전 의료: 가고정 {stabilized} · 운반 {carried}{stranded}\n\n"
            : string.Empty;
    }

}
