# V26 Founder Starting Profile Audit

Audit ID: `v26-founder-profile`

## Implemented authority

- Major proficiency ranks remain Apprentice, Skilled, Technician, Expert and Master for authored requirements and compatibility.
- Each major rank is split into `IV → III → II → I`; the next step after rank I is the next major rank IV.
- A founder profile stores one primary and one secondary proficiency, an independently authored origin, an independently authored past history, species-relative biological age, the age cap and any rolled initial age-condition IDs.
- Primary proficiency earns x1.50 XP, secondary earns x1.20 and unrelated proficiencies earn x1.00. The factor is stored with each published proficiency and applies to approved work, direct/mentoring and combat/training awards before the existing hard daily caps.
- Current proficiency remains owned by the character narrative aggregate after publication. Prepared biological age and age conditions are published into the existing CharacterLife aggregate and are not parallel runtime stats.

## Exact subgrade boundaries

| Major rank | IV | III | II | I |
|---|---:|---:|---:|---:|
| Apprentice | 0–24 | 25–49 | 50–74 | 75–99 |
| Skilled | 100–174 | 175–249 | 250–324 | 325–399 |
| Technician | 400–599 | 600–799 | 800–999 | 1000–1199 |
| Expert | 1200–1649 | 1650–2099 | 2100–2549 | 2550–2999 |
| Master | 3000–3014 | 3015–3029 | 3030–3044 | 3045–3060 |

Work speed and accident multipliers now interpolate continuously between the existing major-rank anchors. This removes the old full-rank performance cliff while major-rank content gates remain unchanged.

## Starting-profile rules

| Species-relative age band | Weight | Per-proficiency cap | Mean primary XP | Mean secondary XP |
|---|---:|---:|---:|---:|
| Young adult | 40% | 99 | 89.43 | 64.62 |
| Established adult | 35% | 174 | 128.11 | 82.45 |
| Veteran adult | 20% | 249 | 172.42 | 104.71 |
| Elder | 5% | 399 | 212.55 | 124.99 |

The packet starts with the existing deterministic 15–45 XP floor for all nine proficiencies. Past history adds the primary/secondary career XP; origin adds a smaller contextual bonus. The age cap is applied last, so no combination can start at Technician or above.

At 99 approved WU/day, successful non-repetitive work awards 11.88 primary XP/day, 9.50 secondary XP/day and 7.92 unrelated XP/day before aptitude and difficulty modifiers. From the measured mean starting XP, reaching Technician takes about 23.1 / 33.5 / 46.5 full approved-work days respectively.

Authored content contains six origins and nine histories. The nine histories cover each built-in proficiency exactly once as their primary specialization.

## Deterministic sample result

- Samples: 18,000 profiles, 2,000 seeds for each of nine histories.
- Age-band counts: 7,206 / 6,323 / 3,596 / 875, matching the intended 40% / 35% / 20% / 5% envelope.
- Mean primary / secondary / unrelated XP: 125.58 / 81.83 / 31.88.
- Mean work-speed multipliers: 0.956 / 0.930 / 0.882.
- At the baseline 99 WU/adult/day, those means correspond to about 94.6 / 92.1 / 87.3 effective WU/day before other modifiers.
- Profiles with initial age conditions: 655; profiles with multiple initial conditions: 313. Among 875 Elder profiles this is 74.9% with any condition and 35.8% with multiple conditions, inside the 65-80% / 25-45% target bands. No condition occurred outside the Elder band, and every condition entered the existing life save record as newly diagnosed Mild age-condition state.

## Passed checks

- Exact 20 subgrade boundaries, including the Master 3015 XP edge.
- Monotonic continuous work-speed and accident multipliers from 0 through 3060 XP.
- Monotonic age caps 99 / 174 / 249 / 399.
- Primary mean > secondary mean > unrelated mean.
- Every generated proficiency respects its age cap and remains below Technician.
- Deterministic repeat generation for identical seeds.
- Six origins and nine histories resolve through the real runtime content catalog.
- Prepared profile and nine-XP packet survive strict character JSON/save validation.
- Initial age conditions capture through the existing CharacterLife save authority.
- x1.50 / x1.20 / x1.00 learning factors pass approved-work, direct/mentoring and combat/training award probes; campaign catch-up floors bypass the factor and combat/training daily caps remain unchanged.
- Unity compile passed; final Console Error 0 / Warning 0.

## Three-founder roster coverage

The selection audit uses one fixed owner plus six generated candidates and selects two candidates without rerolls. All seven profiles in a sample share the same species life-history thresholds. The balanced selector maximizes the best available Fieldwork, Food Production, Construction Engineering and Crafting speeds, protects the weakest of those four fields, rewards primary/secondary coverage and applies a small penalty for initial age conditions.

- Samples: 20,000 rosters.
- Mean best speed by Fieldwork / Food Production / Construction / Crafting: random first-three `0.929 / 0.920 / 0.928 / 0.920`; balanced selection `0.944 / 0.938 / 0.945 / 0.937`.
- Parties with all four fields covered by a primary or secondary specialization: `10.8% -> 48.7%`.
- Best distinct-worker Fieldwork + Food + Construction assignment total: `2.757 -> 2.798`.
- Best distinct-worker Fieldwork + Food + Crafting assignment total: `2.755 -> 2.801`.
- Mean primary XP among the chosen three: `125.54 -> 131.23`.
- Elder share among the chosen three: `5.0% -> 4.7%`.
- Total initial age-condition count across the selected parties: `3,461 -> 2,560`.

Candidate selection therefore improves the four essential starting work rates by roughly 1.5-2.1% and the three-worker assignment totals by roughly 1.5-1.7%. This is meaningful without being a large throughput multiplier. The selector lowers Elder share slightly and rejects enough unhealthy candidates that selected condition count falls by 26.0%. With 5% Elder weight, 25.1% healthy Elders and nine primary histories, one specified healthy Elder primary appears in roughly 1% of natural seven-candidate rosters before trait filtering.

## Remaining integration gates

- Visually inspect the preparation screen in PlayMode without discarding the currently open user-owned unsaved scene state.
- Re-run the full-world save round trip already pending in Phase 147.
- Recalculate initial food, construction and item-production milestone times using the selected-party speed envelope and real recipe/facility contention.
