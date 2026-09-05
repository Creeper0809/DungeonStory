using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

namespace DungeonStory.Factions
{
    public readonly struct FactionTrustTransition
    {
        public FactionTrustTransition(string factionId, int previous, int current)
        {
            FactionId = factionId ?? string.Empty;
            Previous = previous;
            Current = current;
        }

        public string FactionId { get; }
        public int Previous { get; }
        public int Current { get; }
    }

    /// <summary>
    /// Owns faction Aggregate access and deterministic diplomacy/route state
    /// transitions. Unity world, inventory, character, and event projections
    /// remain outside this domain owner.
    /// </summary>
    public sealed class FactionDomainRuntime
    {
        private const float TrustScarMultiplier = 0.85f;
        private const int BetrayalEmbargoDays = 10;
        private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;

        public FactionDomainRuntime(DungeonRuntimeAggregateRootStore aggregateRootStore)
        {
            this.aggregateRootStore = aggregateRootStore
                ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        }

        private FactionAggregateState State
        {
            get => aggregateRootStore.GetOrCreate(() => new FactionAggregateState());
            set => aggregateRootStore.Replace(value);
        }

        public IEnumerable<DungeonFactionState> FactionStates => State.Factions.Values;
        public IReadOnlyList<FactionRouteState> Routes => State.Routes;
        public int CurrentDay => State.CurrentDay;
        public int RouteSequence => State.RouteSequence;
        public int RouteSettlementOperationSequence =>
            State.RouteSettlementOperationSequence;
        public int GoodwillOperationSequence => State.GoodwillOperationSequence;
        public long AllianceBenefitBalanceMilliEwu =>
            State.AllianceBenefitBalanceMilliEwu;
        public long AllianceBenefitRefillRemainder =>
            State.AllianceBenefitRefillRemainder;
        public int AllianceBenefitLastRefillDay =>
            State.AllianceBenefitLastRefillDay;
        public string AllianceBenefitAuthorityDigest =>
            State.AllianceBenefitAuthorityDigest;
        public bool IsRestoreStaging => aggregateRootStore.IsRestoreStaging;
        public int PublishedRestoreRevision => aggregateRootStore.PublishedRestoreRevision;

        public void SetCurrentDay(int day)
        {
            State.CurrentDay = Math.Max(1, day);
        }

        public void ApplyAllianceBenefitRefill(
            int day,
            string authorityDigest,
            long capacityMilliEwu,
            long refillNumeratorMilliEwu,
            long refillDenominatorDays)
        {
            FactionAggregateState state = State;
            int canonicalDay = Math.Max(1, day);
            ValidateAllianceBenefitAuthority(
                state,
                authorityDigest,
                capacityMilliEwu,
                refillNumeratorMilliEwu,
                refillDenominatorDays);
            if (canonicalDay < state.AllianceBenefitLastRefillDay)
            {
                throw new InvalidOperationException(
                    "Faction alliance-benefit refill day cannot move backwards.");
            }
            if (canonicalDay == state.AllianceBenefitLastRefillDay)
            {
                return;
            }

            int elapsedDays = checked(
                canonicalDay - state.AllianceBenefitLastRefillDay);
            state.AllianceBenefitLastRefillDay = canonicalDay;
            if (state.AllianceBenefitBalanceMilliEwu >= capacityMilliEwu)
            {
                state.AllianceBenefitBalanceMilliEwu = capacityMilliEwu;
                state.AllianceBenefitRefillRemainder = 0L;
                return;
            }

            long wholePerDay = refillNumeratorMilliEwu
                / refillDenominatorDays;
            long remainderPerDay = refillNumeratorMilliEwu
                % refillDenominatorDays;
            long wholeCredit = checked(wholePerDay * elapsedDays);
            long remainderTotal = checked(
                state.AllianceBenefitRefillRemainder
                + checked(remainderPerDay * elapsedDays));
            long fractionalCredit = remainderTotal / refillDenominatorDays;
            long nextRemainder = remainderTotal % refillDenominatorDays;
            long credit = checked(wholeCredit + fractionalCredit);
            long room = capacityMilliEwu - state.AllianceBenefitBalanceMilliEwu;
            if (credit >= room)
            {
                state.AllianceBenefitBalanceMilliEwu = capacityMilliEwu;
                state.AllianceBenefitRefillRemainder = 0L;
                return;
            }

            state.AllianceBenefitBalanceMilliEwu = checked(
                state.AllianceBenefitBalanceMilliEwu + credit);
            state.AllianceBenefitRefillRemainder = nextRemainder;
        }

        public bool TryReserveAllianceBenefit(
            string authorityDigest,
            long capacityMilliEwu,
            long debitMilliEwu,
            out long balanceBeforeMilliEwu,
            out long balanceAfterMilliEwu,
            out string failureReason)
        {
            FactionAggregateState state = State;
            balanceBeforeMilliEwu = state.AllianceBenefitBalanceMilliEwu;
            balanceAfterMilliEwu = balanceBeforeMilliEwu;
            if (!string.Equals(
                    state.AllianceBenefitAuthorityDigest,
                    authorityDigest,
                    StringComparison.Ordinal))
            {
                failureReason = "세력 보급 예산 권위가 현재 승인 원장과 일치하지 않습니다.";
                return false;
            }
            if (debitMilliEwu <= 0 || debitMilliEwu > capacityMilliEwu)
            {
                failureReason = "세력 보급 경로의 EWU 비용이 유효하지 않습니다.";
                return false;
            }
            if (balanceBeforeMilliEwu < debitMilliEwu)
            {
                failureReason = "전역 동맹 보급 예산이 부족합니다.";
                return false;
            }

            balanceAfterMilliEwu = checked(
                balanceBeforeMilliEwu - debitMilliEwu);
            state.AllianceBenefitBalanceMilliEwu = balanceAfterMilliEwu;
            failureReason = string.Empty;
            return true;
        }

        public void RefundAllianceBenefit(
            string authorityDigest,
            long capacityMilliEwu,
            long debitMilliEwu,
            long expectedBalanceAfterMilliEwu)
        {
            FactionAggregateState state = State;
            if (!string.Equals(
                    state.AllianceBenefitAuthorityDigest,
                    authorityDigest,
                    StringComparison.Ordinal)
                || state.AllianceBenefitBalanceMilliEwu
                    != expectedBalanceAfterMilliEwu)
            {
                throw new InvalidOperationException(
                    "Alliance-benefit refund no longer matches its aggregate reservation boundary.");
            }
            long refunded = checked(
                state.AllianceBenefitBalanceMilliEwu + debitMilliEwu);
            if (refunded > capacityMilliEwu)
            {
                throw new InvalidOperationException(
                    "Alliance-benefit refund exceeds its configured capacity.");
            }
            state.AllianceBenefitBalanceMilliEwu = refunded;
        }

        private static void ValidateAllianceBenefitAuthority(
            FactionAggregateState state,
            string authorityDigest,
            long capacityMilliEwu,
            long refillNumeratorMilliEwu,
            long refillDenominatorDays)
        {
            if (state == null
                || string.IsNullOrEmpty(authorityDigest)
                || capacityMilliEwu <= 0
                || refillNumeratorMilliEwu <= 0
                || refillDenominatorDays <= 0
                || !string.Equals(
                    state.AllianceBenefitAuthorityDigest,
                    authorityDigest,
                    StringComparison.Ordinal)
                || state.AllianceBenefitBalanceMilliEwu < 0
                || state.AllianceBenefitBalanceMilliEwu > capacityMilliEwu
                || state.AllianceBenefitRefillRemainder < 0
                || state.AllianceBenefitRefillRemainder
                    >= refillDenominatorDays
                || state.AllianceBenefitLastRefillDay <= 0)
            {
                throw new InvalidOperationException(
                    "Faction alliance-benefit budget authority is invalid or stale.");
            }
        }

        public int AllocateGoodwillOperationSequence()
        {
            if (State.GoodwillOperationSequence == int.MaxValue)
            {
                throw new InvalidOperationException(
                    "Faction goodwill operation sequence is exhausted.");
            }
            return ++State.GoodwillOperationSequence;
        }

        public int AllocateRouteSettlementOperationSequence()
        {
            if (State.RouteSettlementOperationSequence == int.MaxValue)
            {
                throw new InvalidOperationException(
                    "Faction route settlement operation sequence is exhausted.");
            }
            return ++State.RouteSettlementOperationSequence;
        }

        public bool TryGetFaction(string factionId, out DungeonFactionState faction)
        {
            return State.Factions.TryGetValue(
                factionId?.Trim() ?? string.Empty,
                out faction);
        }

        public bool IsContractUnlocked(
            DungeonFactionState faction,
            FactionContractKind contract)
        {
            if (faction == null || faction.NegotiationBlocked(CurrentDay))
            {
                return false;
            }

            return contract switch
            {
                FactionContractKind.Trade => faction.trust >= 20,
                FactionContractKind.Recruitment => faction.trust >= 35,
                FactionContractKind.Supply => faction.trust >= 50,
                FactionContractKind.Reinforcement =>
                    faction.trust >= 70 && faction.allianceProjectCompleted,
                _ => false
            };
        }

        public FactionTrustTransition AdjustTrust(
            DungeonFactionState faction,
            int amount)
        {
            if (faction == null)
            {
                throw new ArgumentNullException(nameof(faction));
            }

            int adjusted = amount > 0
                ? Math.Max(1, (int)MathF.Round(
                    amount * MathF.Pow(TrustScarMultiplier, faction.betrayalScars)))
                : amount;
            int previous = faction.trust;
            faction.trust = Math.Clamp(faction.trust + adjusted, -100, 100);
            return new FactionTrustTransition(
                faction.factionId,
                previous,
                faction.trust);
        }

        public void AcceptGoodwill(DungeonFactionState faction)
        {
            (faction ?? throw new ArgumentNullException(nameof(faction))).discovered = true;
        }

        public void CompleteAllianceProject(DungeonFactionState faction)
        {
            (faction ?? throw new ArgumentNullException(nameof(faction)))
                .allianceProjectCompleted = true;
        }

        public FactionRouteState FindTravelingRoute(string routeId)
        {
            return State.Routes.FirstOrDefault(value =>
                value != null
                && string.Equals(value.routeId, routeId, StringComparison.Ordinal)
                && value.status is FactionRouteStatus.Traveling
                    or FactionRouteStatus.Delayed);
        }

        public bool ApplyRouteAmbush(
            FactionRouteState route,
            int strengthLoss,
            float delaySeconds)
        {
            if (route == null)
            {
                return false;
            }

            route.ambushed = true;
            route.strength = Math.Max(0, route.strength - Math.Max(0, strengthLoss));
            route.delaySeconds += Math.Max(0f, delaySeconds);
            route.status = route.strength <= 0
                ? FactionRouteStatus.Lost
                : FactionRouteStatus.Delayed;
            return true;
        }

        public IReadOnlyList<FactionTrustTransition> ApplyBetrayal(
            DungeonFactionState target,
            int actualLootValue)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            List<FactionTrustTransition> transitions = new();
            int previousTargetTrust = target.trust;
            target.trust = -100;
            target.betrayalScars++;
            target.negotiationBlockedUntilDay = CurrentDay + BetrayalEmbargoDays;
            target.restitutionPaid = false;
            target.recoveryEventCompleted = false;
            target.lastBetrayalLootValue = actualLootValue;
            target.restitutionRequiredValue =
                (int)Math.Ceiling(actualLootValue * 1.5f);
            target.restitutionTransferOperationId = string.Empty;
            target.restitutionTransferCommitId = string.Empty;
            target.restitutionTransferSourceStackIds = new List<string>();
            target.restitutionTransferQuantity = 0;
            target.restitutionTransferMassGrams = 0L;
            target.restitutionTransferredPhysicalValue = 0;
            target.restitutionCampaignGrievanceTarget = 0;
            target.restitutionTransferCompleted = false;
            target.discovered = true;
            transitions.Add(new FactionTrustTransition(
                target.factionId,
                previousTargetTrust,
                target.trust));

            foreach (DungeonFactionState peer in State.Factions.Values
                         .Where(value => !ReferenceEquals(value, target)))
            {
                int previousPeerTrust = peer.trust;
                peer.trust = Math.Max(-100, peer.trust - 15);
                transitions.Add(new FactionTrustTransition(
                    peer.factionId,
                    previousPeerTrust,
                    peer.trust));
            }

            return transitions;
        }

        public int GetRestitutionRequired(DungeonFactionState faction)
        {
            if (faction == null)
            {
                throw new ArgumentNullException(nameof(faction));
            }

            return Math.Max(
                1,
                faction.restitutionRequiredValue > 0
                    ? faction.restitutionRequiredValue
                    : 150 * Math.Max(1, faction.betrayalScars));
        }

        public void AcceptRestitution(DungeonFactionState faction)
        {
            (faction ?? throw new ArgumentNullException(nameof(faction)))
                .restitutionPaid = true;
            TryFinishRecovery(faction);
        }

        public void CompleteRecoveryEvent(DungeonFactionState faction)
        {
            (faction ?? throw new ArgumentNullException(nameof(faction)))
                .recoveryEventCompleted = true;
            TryFinishRecovery(faction);
        }

        public void RecordReinforcementLoss(
            DungeonFactionState faction,
            int deaths,
            int equipmentLosses)
        {
            if (faction == null)
            {
                return;
            }

            int dead = Math.Max(0, deaths);
            int lost = Math.Max(0, equipmentLosses);
            faction.reinforcementDeaths += dead;
            faction.equipmentLosses += lost;
            faction.trust = Math.Clamp(faction.trust - dead * 4 - lost, -100, 100);
        }

        public IReadOnlyList<FactionRouteState> AdvanceRoutes(
            float deltaSeconds,
            float secondsPerHex)
        {
            List<FactionRouteState> arrivals = new();
            foreach (FactionRouteState route in State.Routes
                         .Where(value => value != null
                             && value.status is FactionRouteStatus.Traveling
                                 or FactionRouteStatus.Delayed)
                         .ToArray())
            {
                if (route.delaySeconds > 0f)
                {
                    route.status = FactionRouteStatus.Delayed;
                    route.delaySeconds = Math.Max(0f, route.delaySeconds - deltaSeconds);
                    if (route.delaySeconds > 0f)
                    {
                        continue;
                    }

                    route.status = FactionRouteStatus.Traveling;
                }

                route.segmentProgress += deltaSeconds / secondsPerHex;
                while (route.segmentProgress >= 1f
                    && route.pathIndex < route.path.Count - 1)
                {
                    route.segmentProgress -= 1f;
                    route.pathIndex++;
                }

                if (route.pathIndex >= route.path.Count - 1)
                {
                    route.status = FactionRouteStatus.Arrived;
                    route.segmentProgress = 0f;
                    arrivals.Add(route);
                }
            }

            return arrivals;
        }

        public string AddRoute(FactionRouteState route)
        {
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            if (State.RouteSequence == int.MaxValue)
            {
                throw new InvalidOperationException(
                    "Faction route sequence is exhausted.");
            }
            int nextSequence = checked(State.RouteSequence + 1);
            route.routeId = $"faction-route:{nextSequence}";
            State.Routes.Add(route);
            State.RouteSequence = nextSequence;
            return route.routeId;
        }

        public void MarkCargoDelivered(
            FactionRouteState route,
            FactionRouteCargoDeliveryReceipt receipt)
        {
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }
            if (receipt == null
                || receipt.state != FactionRouteCargoDeliveryState.Delivered)
            {
                throw new ArgumentException(
                    "A terminal faction cargo receipt is required.",
                    nameof(receipt));
            }
            route.cargoDelivery = receipt;
        }

        public void AddReinforcementActor(FactionRouteState route, string actorId)
        {
            if (route == null || string.IsNullOrWhiteSpace(actorId))
            {
                return;
            }

            route.reinforcementActorIds ??= new List<string>();
            route.reinforcementActorIds.Add(actorId);
        }

        public void FinishReinforcementMaterialization(FactionRouteState route)
        {
            if (route != null)
            {
                route.actorsSpawned = route.reinforcementActorIds?.Count > 0;
            }
        }

        public void ReplaceState(FactionAggregateState nextState)
        {
            State = nextState ?? throw new ArgumentNullException(nameof(nextState));
        }

        private void TryFinishRecovery(DungeonFactionState faction)
        {
            if (CurrentDay >= faction.negotiationBlockedUntilDay
                && faction.restitutionPaid
                && faction.recoveryEventCompleted)
            {
                faction.trust = 0;
            }
        }
    }
}
