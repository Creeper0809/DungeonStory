# V26 equipment production and combat-readiness throughput

This audit uses live physical BOM, direct craft work, embedded work, research prerequisites and deterministic quality probabilities. It does not create equipment or infer costs from campaign power.
The expedition kit is the authored contemporary party loadout. The readiness kit is the minimum weapon and protection already accepted by the day-1 readiness authority; new reserve slots are not silently upgraded to expedition-grade equipment.
Founder input is the deterministic 10,000-party natural distribution: industry speed-sum 2.758, best craft 0.924, best research 0.934. Later workers are conservatively neutral x1.00; founder proficiency growth is not credited.

## Checkpoint throughput

The period capacity is a conservative floor: the natural founders' measured industry speed sum, plus neutral additional workers, × 99 WU/day × the baseline 35% growth/production share. Quality-adjusted EWU is a gross upper envelope because rejected-output salvage is not credited here.

| Day | Playtime | Window | Crafter rank | Party quality and direct / gross EWU | Party qty / growth share | Ready quality and direct / gross EWU | New-ready qty / growth share | Full reserve qty / growth share | Research WU / isolated days | Status |
|---:|---:|---:|---|---:|---:|---:|---:|---:|---|
| 1 | 3m | start | Apprentice | Normal 427.5 / 704.3 | 1 / start | Normal 427.5 / 704.3 | 2 / start | 2 / start | 824 / 8.9 | STARTING STOCK |
| 30 | 1.5h | 29 | Skilled | Normal 443 / 979.2 | 1 / 35.3% | Normal 184.8 / 304.4 | 0 / 0.0% | 2 / 22.0% | 1168 / 12.6 | PASS |
| 120 | 6h | 90 | Technician | Normal 330.6 / 1134 | 2 / 26.4% | Normal 129.8 / 213.9 | 1 / 2.5% | 3 / 7.5% | 1484 / 16.1 | PASS |
| 240 | 12h | 120 | Expert | Good 792.1 / 7853.7 | 2 / 79.4% | Normal 124 / 204.2 | 2 / 2.1% | 5 / 5.2% | 8504 / 92.0 | PASS |
| 400 | 20h | 160 | Master | Good 1338.5 / 13436.8 | 3 / 93.7% | Normal 124 / 204.2 | 5 / 2.4% | 10 / 4.7% | 10912 / 118.0 | PASS |
| 960 | 48h | 560 | Master | Excellent 2614.4 / 26322.5 | 3 / 27.6% | Normal 124 / 204.2 | 15 / 1.1% | 25 / 1.8% | 20916 / 226.2 | PASS |

## Minimum readiness demand crossover

Supply is the minimum readiness sets producible per day from the same 35% growth-production allocation. Demand is the increase in the lower-bound combat-ready target divided by the checkpoint window. Crossover day is the first absolute day on which that window's new minimum kits can be completed if production starts at the previous checkpoint.

| Target day | Window | New ready people | Growth EWU/day | Readiness EWU/set | Supply sets/day | Demand people/day | Supply / demand | Completion days | First crossover | Playtime at crossover |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | start | 2 | 95.56 | 704.299 | 0.136 | 0 | starting stock | start | Day 1 | 3m |
| 30 | 29 | 0 | 95.56 | 304.359 | 0.314 | 0 | no new demand | 0 | Day 1 | 3m |
| 120 | 90 | 1 | 95.56 | 213.857 | 0.447 | 0.011 | 40.216x | 2.238 | Day 32.238 | 1.6h |
| 240 | 120 | 2 | 164.861 | 204.221 | 0.807 | 0.017 | 48.436x | 2.478 | Day 122.478 | 6.1h |
| 400 | 160 | 5 | 268.811 | 204.221 | 1.316 | 0.031 | 42.121x | 3.799 | Day 243.799 | 12.2h |
| 960 | 560 | 15 | 511.361 | 204.221 | 2.504 | 0.027 | 93.481x | 5.991 | Day 405.991 | 20.3h |

## Equipment detail

| Day | Purpose | Target quality | Equipment | Material and components | Direct WU | EWU | Single attempt | Within 10 | Research |
|---:|---|---|---|---|---:|---:|---:|---:|---|
| 1 | party | Normal | weapon:spear (item equipment-item:weapon:spear) | material:lumber x 2 | 72 | 119 | 27.8% | 96.1% | none |
| 1 | party | Normal | armor:cloth-hood (item equipment-item:armor:cloth-hood) | material:cloth x 1 | 52 | 85.2 | 30.9% | 97.5% | none |
| 30 | party | Normal | weapon:falchion (item equipment-item:weapon:falchion) | material:iron-ingot x 2 | 64 | 199.8 | 65.8% | 100.0% | research:equipment:weapon-patterns |
| 30 | party | Normal | armor:leather (item equipment-item:armor:leather) | material:leather x 4 | 124 | 253.2 | 55.4% | 100.0% | research:textile:tanning |
| 30 | party | Normal | shield:wood (item equipment-item:shield:wood) | material:lumber x 3 | 80 | 143.2 | 65.8% | 100.0% | none |
| 30 | readiness | Normal | weapon:spear (item equipment-item:weapon:spear) | material:lumber x 2 | 72 | 119 | 65.8% | 100.0% | none |
| 30 | readiness | Normal | armor:cloth-hood (item equipment-item:armor:cloth-hood) | material:cloth x 1 | 52 | 85.2 | 69.1% | 100.0% | none |
| 120 | party | Normal | weapon:mace (item equipment-item:weapon:mace) | material:iron-ingot x 3 | 72 | 269.3 | 95.1% | 100.0% | research:metallurgy:iron |
| 120 | party | Normal | armor:mail-shirt (item equipment-item:armor:mail-shirt) | material:iron-ingot x 6; material:chain-mesh x 1 | 152 | 625.3 | 89.5% | 100.0% | research:equipment:mail-weaving |
| 120 | party | Normal | shield:wood (item equipment-item:shield:wood) | material:lumber x 3 | 80 | 143.2 | 94.0% | 100.0% | none |
| 120 | readiness | Normal | weapon:spear (item equipment-item:weapon:spear) | material:lumber x 2 | 72 | 119 | 95.1% | 100.0% | none |
| 120 | readiness | Normal | armor:cloth-hood (item equipment-item:armor:cloth-hood) | material:cloth x 1 | 52 | 85.2 | 96.1% | 100.0% | none |
| 240 | party | Good | weapon:estoc (item equipment-item:weapon:estoc) | material:iron-ingot x 2 | 64 | 199.9 | 85.6% | 100.0% | research:metallurgy:steel |
| 240 | party | Good | armor:articulated-plate (item equipment-item:armor:articulated-plate) | material:iron-ingot x 10; component:growth-frame x 1, component:textile-hardener x 1, material:plate-blank x 2 | 256 | 2758.1 | 55.4% | 100.0% | research:equipment:articulated-plate |
| 240 | party | Good | shield:iron (item equipment-item:shield:iron) | material:iron-ingot x 5; component:growth-frame x 1 | 176 | 1821.3 | 69.1% | 100.0% | research:metallurgy:iron |
| 240 | readiness | Normal | weapon:spear (item equipment-item:weapon:spear) | material:lumber x 2 | 72 | 119 | 100.0% | 100.0% | none |
| 240 | readiness | Normal | armor:cloth-hood (item equipment-item:armor:cloth-hood) | material:cloth x 1 | 52 | 85.2 | 100.0% | 100.0% | none |
| 400 | party | Good | weapon:powered-striking-gauntlet (item equipment-item:weapon:powered-striking-gauntlet) | material:iron-ingot x 6; material:plate-blank x 1, component:machine-parts x 2 | 296 | 1297.4 | 85.6% | 100.0% | research:equipment:powered-armor |
| 400 | party | Good | armor:powered-harness (item equipment-item:armor:powered-harness) | material:iron-ingot x 10; component:growth-frame x 1, component:machine-parts x 2, component:precision-parts x 2, component:powered-armor-joint x 2, component:prototype-package x 1 | 392 | 5680.5 | 72.2% | 100.0% | research:equipment:powered-armor |
| 400 | party | Good | shield:powered (item equipment-item:shield:powered) | material:iron-ingot x 10; component:growth-frame x 1, material:plate-blank x 2, component:machine-parts x 2 | 352 | 3173.5 | 78.2% | 100.0% | research:equipment:powered-armor |
| 400 | readiness | Normal | weapon:spear (item equipment-item:weapon:spear) | material:lumber x 2 | 72 | 119 | 100.0% | 100.0% | none |
| 400 | readiness | Normal | armor:cloth-hood (item equipment-item:armor:cloth-hood) | material:cloth x 1 | 52 | 85.2 | 100.0% | 100.0% | none |
| 960 | party | Excellent | weapon:rune-blade (item equipment-item:weapon:rune-blade) | material:iron-ingot x 2; component:growth-frame x 1, component:rune-conductor x 1, component:rune-control-panel x 1 | 264 | 2722.5 | 44.6% | 99.7% | research:equipment:rune-module-tuning |
| 960 | party | Excellent | armor:rune-ward-mail (item equipment-item:armor:rune-ward-mail) | material:iron-ingot x 7; component:growth-frame x 1, material:chain-mesh x 1, material:mana-alloy x 1, component:rune-conductor x 1, component:dreamweave-rune-lining x 1, component:rune-leather-lining x 1 | 344 | 2991.3 | 30.9% | 97.5% | research:equipment:rune-module-tuning |
| 960 | party | Excellent | shield:rune (item equipment-item:shield:rune) | material:iron-ingot x 5; component:growth-frame x 1, component:rune-conductor x 1, component:rune-tuning-shield x 1, component:rune-leather-strap x 1, component:rune-control-panel x 1 | 312 | 3614.4 | 34.2% | 98.5% | research:equipment:rune-module-tuning |
| 960 | readiness | Normal | weapon:spear (item equipment-item:weapon:spear) | material:lumber x 2 | 72 | 119 | 100.0% | 100.0% | none |
| 960 | readiness | Normal | armor:cloth-hood (item equipment-item:armor:cloth-hood) | material:cloth x 1 | 52 | 85.2 | 100.0% | 100.0% | none |

## Interpretation guardrails

- Day-1 equipment is a starting-stock condition; this audit does not pretend it was crafted before play begins.
- The party envelope answers whether the authored minimum expedition party can receive the contemporary set. The new-ready envelope uses the minimum day-1 readiness kit instead of silently giving every reserve the latest party set. The full-reserve envelope is a deliberately conservative minimum-kit pressure indicator and is not a pass gate.
- Old equipment remains usable physical reserve stock. No upgrade, deletion, salvage or sale value is credited automatically.
- Gross quality EWU assumes a fresh full input on each rejected attempt. Runtime auto-dismantle can reduce net material cost, but direct craft time and player attention remain real; a later live production simulation must measure the net value.
- Research days are an isolated one-researcher lower bound over the de-duplicated prerequisite closure of equipment, primary materials and every cheapest-EWU upstream production recipe. They do not include competing survival, industry or medical research.

## Failures

- none
