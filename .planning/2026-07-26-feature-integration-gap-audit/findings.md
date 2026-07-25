# Findings

## P0 - Duplicate scene runtimes split command and customer state

- `GameplayScene` contains two enabled `OwnerCommandController` instances:
  `Priority Command Controller` and `OwnerCommandController`. The lifetime scope
  resolves only the first scene match while scene hierarchy injection can still
  activate both MonoBehaviours, so pointer/direct-command ownership is
  nondeterministic.
- `GameplayScene` contains two enabled `RegularCustomerRuntime` instances:
  `RegularCustomerRuntime_Test` and a second runtime on the gameplay root. Only
  one is registered into VContainer and receives the injected event bus; the
  other can keep separate regular/recruit state.

## P0 - Expedition equipment remains a second source of truth

- `ExpeditionEquipmentRuntime` still owns a separate inventory, character
  loadouts, craft queue, save DTO, and save section instead of adapting
  `ICombatEquipmentRuntime`.
- Equipment crafting advances the legacy `remainingSeconds` queue and only then
  creates a new combat equipment instance. The crafting source of truth and the
  produced equipment source of truth therefore belong to different runtimes.
- Offense battle creation applies legacy expedition stat bonuses and then also
  configures common combat equipment/body-health. An equipped item can therefore
  exist, reserve inventory, and affect combat through two independent models.

## P0 - Four offense rewards are counters, not rewards

- Prisoner, special monster, human-faction weakening, and rival-faction
  weakening handlers only increment `OffenseRewardState` fields.
- Their only consumers are save/restore and summary text.
- They do not create a captive, add a prepared world character, capture a
  wildlife specimen, alter an encounter/faction, change invasion pressure, or
  unlock a command. The player can earn these rewards but cannot use or observe
  their promised domain effect.
- Recruit-candidate rewards are not part of this finding: the reward event is
  consumed by `RegularCustomerRuntime` and promotes existing visitors. The
  duplicate scene runtime can, however, process that event twice into separate
  states.

## P1 - Exterior incidents are presentation-only timers

- Merchant cart, informant, thief, and injured returnee incidents only set text,
  zone ID, and a countdown. They do not spawn an actor/item, open trade, steal,
  create medical work, or publish a result event.
- Reception/patrol work can clear an incident, but expiration simply clears it
  with no success/failure consequence.
- `firstImpressionBonus` and `AveragePatrolReadiness` are stored and shown but
  are not consumed by visitor satisfaction, incident probability, theft, or
  invasion detection.

## P1 - AI weather pressure is disconnected from actual weather

- `DefaultCharacterAiWorldSignalQuery` receives `ISurvivalFoodRuntime`, but only
  reads food/water shortage from it.
- Weather pressure is hard-coded from exterior/day-night status, so rain, fog,
  storm, cold snap, and heat wave shown by the survival runtime do not change AI
  work/facility decisions.

## P1 - Circus progression stops at numbers

- Performances raise performer fame, skill, injury count, and privilege tier.
- Skill feeds later audience satisfaction and fame raises ransom value, but
  `privilegeTier` and `performerInjuries` have no gameplay consumer.
- Fame maps to tier 1/2 at 50/75, yet those tiers change no treatment or policy.
  The designed fame-75 contract path and fame-100 release/exclusive-fighter
  decision do not exist, so a long-running performer has no terminal progression
  payoff.

## P1 - Captivity extraction outputs lose their domain meaning

- Blood extraction and memory extraction produce physical items, but the item
  IDs have no specialist consumer outside their definitions.
- Both are categorized as generic Mana, so memory does not affect research,
  codex, rumors, factions, or offense intelligence, and blood does not feed a
  dedicated ritual/medical/corruption recipe.

## P1 - Food has two competing consumption loops

- `SurvivalFoodRuntime` removes one Food per owner/worker at day start.
- The same worker's personal hunger is restored by visiting a meal shop, whose
  physical facility stock is also decremented and must be hauled/restocked from
  warehouse stock.
- Water was already converted to forecast-only daily accounting, but Food was
  not. As a result, one visible Food category pays both an anonymous daily tax
  and an actual meal without explaining the distinction.

## P1 - Exterior night danger does not produce danger

- Weather, missing fuel, rot, and lighting calculate and save an
  `ExteriorNightDanger` score.
- The score is shown in Operations and raises Refuel urgency, but it is not
  consumed by theft, predator approach, delivery damage, patrol detection, or
  incident probability.

## P2 - AI performance analyzer is a declaration-only scaffold

- `CharacterAiPerfSettingsSO`, `CharacterAiPerformanceReport`, and its metrics
  are declared but have no runtime registration, asset, recorder, or report
  writer.
- Existing stress verifiers calculate their own frame/scheduler percentiles, so
  the product-facing analyzer model is currently unused duplication.

## P2 - Expedition support ability is legacy dead data

- `BuildingExpeditionSupportAbility` declares supply, light, camp heal, stress,
  medicine, and scouting bonuses.
- No production asset serializes it and no gameplay service reads it. It remains
  beside the newer physical inventory/equipment/recovery loop as an unused
  legacy feature shape.

## Verified connected

- Room environment is consumed by work duration, facility/commerce experience,
  mood, inspection UI, and save-backed room state.
- Captive labor is checked by the normal work target selector and AbilityWork.
- Door access is evaluated by pathfinding and again immediately before movement.
- Wildlife reaches hunting, combat, carcass stacks, hauling, butchering, and
  ecosystem save state.
- Work orders reach material reservation, physical hauling, incremental work,
  completion effects, UI diagnostics, and V15 save sections.

## Recommended integration order

1. Remove duplicate scene runtimes and add a scene singleton assertion.
2. Collapse expedition equipment onto common combat equipment.
3. Convert offense counters into domain commands/events.
4. Give exterior incidents real actors/results and feed actual weather/night
   danger into those incidents and AI.
5. Unify food consumption around physical individual meals.
6. Finish circus milestones and specialist captivity recipes.
7. Delete or implement declaration-only performance/support scaffolds.
