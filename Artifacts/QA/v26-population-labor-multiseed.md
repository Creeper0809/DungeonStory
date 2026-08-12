# V26 population and labor multi-seed audit

- generated: 2026-08-10 UTC
- seeds: 256 per policy and starter species
- starter species: Slime, Orc, Vampire; three initial adults
- this is a policy envelope, not a prediction of player choices
- same-lineage adult recruits isolate maturity and mortality; mixed-culture candidate scarcity is a later PlayMode pressure probe
- safe temperature, health 100, nutrition 100; no fertility treatment or emergency extraction
- elder labor availability is 25%; continuous primary work uses 99 WU/day and the live 0.08 XP/WU rule

## Authored species authority

| species | adult | elder | reproduction | base success | duration |
|---|---:|---:|---|---:|---:|
| Slime | 8y | 42y | CoreDivision | 35 % | 31d |
| Orc | 14y | 45y | Pregnancy | 35 % | 62d |
| Vampire | 18y | 130y | Pregnancy | 35 % | 92d |

## Conservative policy

Recruit one eligible adult every 30 days; begin reproduction day 60, evaluate one pair every 120 days.

| day | total p10/median/p90 | workers p10/median/p90 | dependents p10/median/p90 | EWU/day p10/median/p90 | recruits med | births med | deaths med |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 3/3/3 | 3/3/3 | 0/0/0 | 189.3/252.5/252.5 | 0 | 0 | 0 |
| 30 | 4/4/4 | 4/4/4 | 0/0/0 | 293.3/366.3/366.3 | 1 | 0 | 0 |
| 120 | 7/7/7 | 7/7/7 | 0/0/0 | 551.9/637.3/707.8 | 4 | 0 | 0 |
| 240 | 11/11/12 | 11/11/11 | 0/0/1 | 926.9/1095.2/1183.0 | 8 | 0 | 0 |
| 400 | 16/16/17 | 15/16/16 | 0/0/1 | 1264.7/1528.3/1716.4 | 13 | 0 | 0 |
| 960 | 30/33/36 | 30/32/35 | 0/0/1 | 1873.6/2232.4/3815.6 | 32 | 1 | 3 |

Day 960 by starter lineage:

| lineage | total p10/median/p90 | workers p10/median/p90 | dependents p10/median/p90 | EWU/day p10/median/p90 |
|---|---:|---:|---:|---:|
| Slime | 29/32/34 | 29/32/34 | 0/0/0 | 1897.1/2153.9/2430.5 |
| Orc | 29/32/34 | 29/31/34 | 0/0/1 | 1778.9/2027.0/2277.0 |
| Vampire | 33/35/37 | 32/34/36 | 0/0/2 | 3390.8/3684.7/3992.2 |

## Balanced policy

Recruit one eligible adult every 15 days; begin reproduction day 40, evaluate one pair every 40 days.

| day | total p10/median/p90 | workers p10/median/p90 | dependents p10/median/p90 | EWU/day p10/median/p90 | recruits med | births med | deaths med |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 3/3/3 | 3/3/3 | 0/0/0 | 189.3/252.5/252.5 | 0 | 0 | 0 |
| 30 | 5/5/5 | 5/5/5 | 0/0/0 | 387.3/460.3/460.3 | 2 | 0 | 0 |
| 120 | 11/11/12 | 11/11/11 | 0/0/1 | 943.0/1033.3/1113.8 | 8 | 0 | 0 |
| 240 | 19/20/21 | 19/19/19 | 0/1/2 | 1624.1/1858.7/2034.4 | 16 | 1 | 0 |
| 400 | 29/30/32 | 28/29/29 | 0/1/2 | 2417.6/2744.8/3081.4 | 26 | 1 | 0 |
| 960 | 59/64/69 | 58/63/67 | 0/1/3 | 3797.6/4364.7/7259.9 | 64 | 2 | 6 |

Day 960 by starter lineage:

| lineage | total p10/median/p90 | workers p10/median/p90 | dependents p10/median/p90 | EWU/day p10/median/p90 |
|---|---:|---:|---:|---:|
| Slime | 58/62/66 | 58/62/65 | 0/0/1 | 3891.3/4292.3/4683.9 |
| Orc | 58/61/65 | 57/61/64 | 0/1/2 | 3637.0/4019.4/4365.3 |
| Vampire | 65/68/71 | 63/66/68 | 0/2/4 | 6724.0/7092.7/7445.4 |

## Expansion policy

Recruit one eligible adult every 10 days; begin reproduction day 20, evaluate one pair every 10 days.

| day | total p10/median/p90 | workers p10/median/p90 | dependents p10/median/p90 | EWU/day p10/median/p90 | recruits med | births med | deaths med |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 3/3/3 | 3/3/3 | 0/0/0 | 189.3/252.5/252.5 | 0 | 0 | 0 |
| 30 | 6/6/6 | 6/6/6 | 0/0/0 | 481.4/554.4/554.4 | 3 | 0 | 0 |
| 120 | 15/16/17 | 15/15/15 | 0/1/2 | 1275.9/1429.3/1509.8 | 12 | 1 | 0 |
| 240 | 28/30/32 | 27/27/27 | 1/3/5 | 2378.5/2627.2/2805.4 | 24 | 3 | 0 |
| 400 | 44/48/51 | 42/43/45 | 1/4/8 | 3699.0/4189.6/4573.3 | 40 | 5 | 0 |
| 960 | 93/100/111 | 90/97/103 | 1/4/10 | 6103.0/7033.9/11126.1 | 96 | 10 | 9 |

Day 960 by starter lineage:

| lineage | total p10/median/p90 | workers p10/median/p90 | dependents p10/median/p90 | EWU/day p10/median/p90 |
|---|---:|---:|---:|---:|
| Slime | 93/98/104 | 91/96/102 | 0/2/4 | 6369.4/6912.7/7525.2 |
| Orc | 91/97/104 | 89/93/98 | 1/4/7 | 5816.9/6357.0/6943.6 |
| Vampire | 101/109/116 | 96/101/106 | 3/8/12 | 10242.2/10857.2/11562.0 |

## Balanced-policy target comparison

The baseline band is compared only with the balanced policy. Conservative and expansion are deliberate lower/upper policy envelopes.

| day | target total | simulated median | status |
|---:|---:|---:|---|
| 1 | 3~3 | 3 | inside |
| 30 | 3~6 | 5 | inside |
| 120 | 6~14 | 11 | inside |
| 240 | 12~28 | 20 | inside |
| 400 | 25~60 | 30 | inside |
| 960 | 80~220 | 64 | below by 16 |

Balanced reaches day 400 without hidden growth. Its day-960 median is 16 below the target floor when captive recruitment, faction joiners and golem assembly are all excluded. Closing that exact gap needs roughly one additional adult from those physical routes every 60 days; it must not be patched by increasing biological birth success implicitly.

## Interpretation guardrails

- Reproduction success is now exercised by the authored Attempt phase; omitting that phase is a catalog error.
- Housing, food, medicine, wages, reproductive facilities and assembly inputs can only reduce these unconstrained envelopes.
- Headcount does not imply combat readiness. Equipment production and defense/expedition demand are audited in the next gate.
- A target-band miss is evidence for rule or cost tuning, not permission to fit a hidden growth multiplier.
