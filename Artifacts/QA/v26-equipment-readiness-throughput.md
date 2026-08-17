# V26 equipment production and combat-readiness throughput

This audit uses live physical BOM, direct craft work, embedded work, research prerequisites and deterministic quality probabilities. It does not create equipment or infer costs from campaign power.
The expedition kit is the authored contemporary party loadout. The readiness kit is the minimum weapon and protection already accepted by the day-1 readiness authority; new reserve slots are not silently upgraded to expedition-grade equipment.
Founder input is the deterministic 10,000-party natural distribution: industry speed-sum 2.758, best craft 0.924, best research 0.934. Later workers are conservatively neutral x1.00; founder proficiency growth is not credited.

## Checkpoint throughput

The period capacity is a conservative floor: the natural founders' measured industry speed sum, plus neutral additional workers, × 45 effective WU/day × the V27 37% equipment growth/production share. Quality-adjusted EWU reports both gross fresh-input pressure and production-exact net pressure after rejected-output dismantle work and recovered physical inputs.

| Day | Playtime | Window | Crafter rank | Party quality and direct / gross / net EWU | Party qty / net growth share | Ready quality and direct / gross / net EWU | New-ready qty / net growth share | Full reserve qty / growth share | Research WU / isolated days | Status |
|---:|---:|---:|---|---:|---:|---:|---:|---:|---|
| 1 | 3m | start | Apprentice | Normal 420.991 / 596.165 / 627.034 | 1 / start | Normal 420.991 / 596.165 / 627.034 | 2 / start | 2 / start | 380 / 9.0 | STARTING STOCK |
| 30 | 1.5h | 29 | Skilled | Normal 436.319 / 888.841 / 860.342 | 1 / 64.6% | Normal 187.135 / 286.331 / 293.531 | 0 / 0.0% | 2 / 44.1% | 540 / 12.9 | PASS |
| 120 | 6h | 90 | Technician | Normal 324.125 / 1089.732 / 1062.343 | 2 / 47.2% | Normal 122.847 / 201.157 / 201.829 | 1 / 4.5% | 3 / 13.4% | 684 / 14.9 | PASS |
| 240 | 12h | 120 | Technician | Normal 611.303 / 5648.163 / 5480.962 | 2 / 92.3% | Normal 122.847 / 201.157 / 201.829 | 2 / 3.4% | 5 / 8.5% | 3882 / 73.9 | PASS |
| 400 | 20h | 160 | Expert | Normal 1088.999 / 10413.29 / 10234.99 | 3 / 99.7% | Normal 116 / 192.085 / 192.085 | 5 / 3.1% | 10 / 6.2% | 4980 / 79.5 | PASS |
| 960 | 48h | 560 | Master | Good 1105.851 / 10627.41 / 10566.5 | 3 / 13.6% | Normal 116 / 192.085 / 192.085 | 15 / 1.2% | 25 / 2.1% | 9531 / 133.4 | PASS |

## Minimum readiness demand crossover

Supply is the minimum readiness sets producible per day from the same 37% equipment growth-production allocation. Demand is the increase in the lower-bound combat-ready target divided by the checkpoint window. Crossover day is the first absolute day on which that window's new minimum kits can be completed if production starts at the previous checkpoint.

| Target day | Window | New ready people | Growth EWU/day | Readiness EWU/set | Supply sets/day | Demand people/day | Supply / demand | Completion days | First crossover | Playtime at crossover |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | start | 2 | 45.918 | 627.034 | 0.073 | 0 | starting stock | start | Day 1 | 3m |
| 30 | 29 | 0 | 45.918 | 293.531 | 0.156 | 0 | no new demand | 0 | Day 1 | 3m |
| 120 | 90 | 1 | 50.051 | 201.829 | 0.248 | 0.011 | 22.319x | 4.032 | Day 34.032 | 1.7h |
| 240 | 120 | 2 | 99.023 | 201.829 | 0.491 | 0.017 | 29.438x | 4.076 | Day 124.076 | 6.2h |
| 400 | 160 | 5 | 192.461 | 192.085 | 1.002 | 0.031 | 32.063x | 4.99 | Day 244.99 | 12.2h |
| 960 | 560 | 15 | 417.721 | 192.085 | 2.175 | 0.027 | 81.188x | 6.898 | Day 406.898 | 20.3h |

## Equipment detail

| Day | Purpose | Target quality | Equipment | Material and components | Direct WU | Item EWU | Single attempt | Expected attempts / rejects | Rejected recovery EWU / dismantle WU | Net expected EWU | Within 10 | Research |
|---:|---|---|---|---|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | party | Normal | weapon:spear (item equipment-item:weapon:spear) | material:lumber x 2 | 68 | 112.8 | 30.9% | 3.232 / 2.232 | 13.496 / 17 | 372.322 | 97.5% | none |
| 1 | party | Normal | armor:cloth-hood (item equipment-item:armor:cloth-hood) | material:cloth x 1 | 48 | 79.3 | 34.2% | 2.921 / 1.921 | 0 / 12 | 254.712 | 98.5% | none |
| 30 | party | Normal | weapon:falchion (item equipment-item:weapon:falchion) | material:iron-ingot x 2 | 60 | 190.3 | 69.1% | 1.448 / 0.448 | 56.2 / 15 | 257.033 | 100.0% | research:equipment:weapon-patterns |
| 30 | party | Normal | armor:leather (item equipment-item:armor:leather) | material:leather x 4 | 116 | 239.2 | 58.9% | 1.698 / 0.698 | 47.488 / 29 | 393.34 | 100.0% | research:textile:tanning |
| 30 | party | Normal | shield:wood (item equipment-item:shield:wood) | material:lumber x 3 | 76 | 136.2 | 65.8% | 1.521 / 0.521 | 13.496 / 19 | 209.969 | 100.0% | none |
| 30 | readiness | Normal | weapon:spear (item equipment-item:weapon:spear) | material:lumber x 2 | 68 | 112.8 | 65.8% | 1.521 / 0.521 | 13.496 / 17 | 173.301 | 100.0% | none |
| 30 | readiness | Normal | armor:cloth-hood (item equipment-item:armor:cloth-hood) | material:cloth x 1 | 48 | 79.3 | 69.1% | 1.448 / 0.448 | 0 / 12 | 120.23 | 100.0% | none |
| 120 | party | Normal | weapon:mace (item equipment-item:weapon:mace) | material:iron-ingot x 3 | 68 | 257.3 | 95.1% | 1.052 / 0.052 | 112.399 / 17 | 265.641 | 100.0% | research:metallurgy:iron |
| 120 | party | Normal | armor:mail-shirt (item equipment-item:armor:mail-shirt) | material:iron-ingot x 6; material:chain-mesh x 1 | 148 | 603.7 | 89.5% | 1.117 / 0.117 | 224.798 / 37 | 652.26 | 100.0% | research:equipment:mail-weaving |
| 120 | party | Normal | shield:wood (item equipment-item:shield:wood) | material:lumber x 3 | 76 | 136.2 | 94.0% | 1.064 / 0.064 | 26.992 / 19 | 144.441 | 100.0% | none |
| 120 | readiness | Normal | weapon:spear (item equipment-item:weapon:spear) | material:lumber x 2 | 68 | 112.8 | 95.1% | 1.052 / 0.052 | 13.496 / 17 | 118.77 | 100.0% | none |
| 120 | readiness | Normal | armor:cloth-hood (item equipment-item:armor:cloth-hood) | material:cloth x 1 | 48 | 79.3 | 96.1% | 1.041 / 0.041 | 0 / 12 | 83.059 | 100.0% | none |
| 240 | party | Normal | weapon:estoc (item equipment-item:weapon:estoc) | material:iron-ingot x 2 | 60 | 190.4 | 95.1% | 1.052 / 0.052 | 56.2 / 15 | 198.079 | 100.0% | research:metallurgy:steel |
| 240 | party | Normal | armor:articulated-plate (item equipment-item:armor:articulated-plate) | material:iron-ingot x 10; component:growth-frame x 1, component:textile-hardener x 1, material:plate-blank x 2 | 248 | 2652.9 | 78.2% | 1.279 / 0.279 | 578.499 / 62 | 3249.326 | 100.0% | research:equipment:articulated-plate |
| 240 | party | Normal | shield:iron (item equipment-item:shield:iron) | material:iron-ingot x 5; component:growth-frame x 1 | 176 | 1759.4 | 85.6% | 1.168 / 0.168 | 168.599 / 44 | 2033.557 | 100.0% | research:metallurgy:iron |
| 240 | readiness | Normal | weapon:spear (item equipment-item:weapon:spear) | material:lumber x 2 | 68 | 112.8 | 95.1% | 1.052 / 0.052 | 13.496 / 17 | 118.77 | 100.0% | none |
| 240 | readiness | Normal | armor:cloth-hood (item equipment-item:armor:cloth-hood) | material:cloth x 1 | 48 | 79.3 | 96.1% | 1.041 / 0.041 | 0 / 12 | 83.059 | 100.0% | none |
| 400 | party | Normal | weapon:powered-striking-gauntlet (item equipment-item:weapon:powered-striking-gauntlet) | material:iron-ingot x 6; material:plate-blank x 1, component:machine-parts x 2 | 292 | 1258 | 97.6% | 1.024 / 0.024 | 366.287 / 73 | 1281.477 | 100.0% | research:equipment:powered-armor |
| 400 | party | Normal | armor:powered-harness (item equipment-item:armor:powered-harness) | material:iron-ingot x 10; component:growth-frame x 1, component:machine-parts x 2, component:precision-parts x 2, component:powered-armor-joint x 2, component:prototype-package x 1 | 380 | 5468.4 | 92.7% | 1.079 / 0.079 | 1805.575 / 95 | 5766.173 | 100.0% | research:equipment:powered-armor |
| 400 | party | Normal | shield:powered (item equipment-item:shield:powered) | material:iron-ingot x 10; component:growth-frame x 1, material:plate-blank x 2, component:machine-parts x 2 | 348 | 3064.6 | 95.1% | 1.052 / 0.052 | 776.186 / 87 | 3187.344 | 100.0% | research:equipment:powered-armor |
| 400 | readiness | Normal | weapon:spear (item equipment-item:weapon:spear) | material:lumber x 2 | 68 | 112.8 | 100.0% | 1 / 0 | 13.496 / 17 | 112.762 | 100.0% | none |
| 400 | readiness | Normal | armor:cloth-hood (item equipment-item:armor:cloth-hood) | material:cloth x 1 | 48 | 79.3 | 100.0% | 1 / 0 | 0 / 12 | 79.323 | 100.0% | none |
| 960 | party | Good | weapon:rune-blade (item equipment-item:weapon:rune-blade) | material:iron-ingot x 2; component:growth-frame x 1, component:rune-conductor x 1, component:rune-control-panel x 1 | 260 | 2637.4 | 89.5% | 1.117 / 0.117 | 56.2 / 65 | 2946.622 | 100.0% | research:equipment:rune-module-tuning |
| 960 | party | Good | armor:rune-ward-mail (item equipment-item:armor:rune-ward-mail) | material:iron-ingot x 7; component:growth-frame x 1, material:chain-mesh x 1, material:mana-alloy x 1, component:rune-conductor x 1, component:dreamweave-rune-lining x 1, component:rune-leather-lining x 1 | 336 | 2892.7 | 80.9% | 1.236 / 0.236 | 280.998 / 84 | 3530.063 | 100.0% | research:equipment:rune-module-tuning |
| 960 | party | Good | shield:rune (item equipment-item:shield:rune) | material:iron-ingot x 5; component:growth-frame x 1, component:rune-conductor x 1, component:rune-tuning-shield x 1, component:rune-leather-strap x 1, component:rune-control-panel x 1 | 308 | 3515.6 | 85.6% | 1.168 / 0.168 | 168.599 / 77 | 4089.817 | 100.0% | research:equipment:rune-module-tuning |
| 960 | readiness | Normal | weapon:spear (item equipment-item:weapon:spear) | material:lumber x 2 | 68 | 112.8 | 100.0% | 1 / 0 | 13.496 / 17 | 112.762 | 100.0% | none |
| 960 | readiness | Normal | armor:cloth-hood (item equipment-item:armor:cloth-hood) | material:cloth x 1 | 48 | 79.3 | 100.0% | 1 / 0 | 0 / 12 | 79.323 | 100.0% | none |

## Interpretation guardrails

- Day-1 equipment is a starting-stock condition; this audit does not pretend it was crafted before play begins.
- The party envelope answers whether the authored minimum expedition party can receive the contemporary set. The new-ready envelope uses the minimum day-1 readiness kit instead of silently giving every reserve the latest party set. The full-reserve envelope is a deliberately conservative minimum-kit pressure indicator and is not a pass gate.
- Old equipment remains usable physical reserve stock. No upgrade, deletion, salvage or sale value is credited automatically.
- Gross quality EWU assumes a fresh full input on each rejected attempt. Net expected EWU uses the same production V23MaterialSalvageCalculator, rank-derived relevant skill, Floor recovery quantities and 25% rejected dismantle WU. Recovered inputs reduce only material acquisition pressure; craft and dismantle labor remain real.
- Research days are an isolated one-researcher lower bound over the de-duplicated prerequisite closure of equipment, primary materials and every cheapest-EWU upstream production recipe. They do not include competing survival, industry or medical research.

## Failures

- none
