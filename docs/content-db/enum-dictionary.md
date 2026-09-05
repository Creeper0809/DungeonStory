# 직렬화 enum 사전

원시 숫자의 해석 권위는 현재 C# enum 선언이다.

## AnatomyAttachmentPoint

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None |
| 1 | `Head` | Head |
| 2 | `Face` | Face |
| 3 | `Neck` | Neck |
| 4 | `Torso` | Torso |
| 5 | `Pelvis` | Pelvis |
| 6 | `ArmLeft` | Arm Left |
| 7 | `Arms` | Arms |
| 8 | `HandLeft` | Hand Left |
| 9 | `Hands` | Hands |
| 10 | `LegLeft` | Leg Left |
| 11 | `Legs` | Legs |
| 12 | `FootLeft` | Foot Left |
| 13 | `Feet` | Feet |
| 14 | `Back` | Back |
| 15 | `Tail` | Tail |
| 16 | `WingLeft` | Wing Left |
| 17 | `Wings` | Wings |
| 18 | `HornSet` | Horn Set |
| 31 | `OptionalAppendages` | Optional Appendages |

## AnatomyConditionKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `FluidLoss` | Fluid Loss |
| 1 | `Contamination` | Contamination |
| 2 | `Overstrain` | Overstrain |
| 3 | `Fracture` | Fracture |
| 4 | `PartFailure` | Part Failure |
| 5 | `CompatibilityFailure` | Compatibility Failure |
| 6 | `TreatmentRequired` | Treatment Required |

## ApparelBodyForm

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Humanoid` | Humanoid |
| 1 | `Construct` | Construct |
| 2 | `Any` | Any |

## ApparelFitMode

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Sized` | Sized |
| 1 | `Adjustable` | Adjustable |
| 2 | `Accessory` | Accessory |

## ApparelLayer

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Underwear` | Underwear |
| 1 | `Inner` | Inner |
| 2 | `Outer` | Outer |
| 3 | `Armor` | Armor |
| 4 | `Accessory` | Accessory |

## ApparelModificationKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None |
| 1 | `TailOpening` | Tail Opening |
| 2 | `WingSlits` | Wing Slits |
| 4 | `HornClearance` | Horn Clearance |

## ApparelSizeClass

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Small` | Small |
| 1 | `Medium` | Medium |
| 2 | `Large` | Large |

## ApparelUseTag

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None |
| 1 | `Underwear` | Underwear |
| 2 | `Sleep` | Sleep |
| 4 | `Daily` | Daily |
| 8 | `Work` | Work |
| 16 | `Cold` | Cold |
| 32 | `Heat` | Heat |
| 64 | `Wet` | Wet |
| 128 | `Medical` | Medical |
| 256 | `Formal` | Formal |
| 512 | `Cultural` | Cultural |
| 1024 | `Accessory` | Accessory |
| 2048 | `Protective` | Protective |

## BattlefieldModifierKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Terrain` | Terrain |
| 1 | `Objective` | Objective |
| 2 | `Hazard` | Hazard |

## BuildingCategory

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None (없음) |
| 1 | `Wall` | Wall (벽) |
| 2 | `Shop` | Shop (상점) |
| 3 | `Special` | Special (특수) |
| 4 | `Movement` | Movement (이동) |
| 5 | `Production` | Production (생산) |
| 6 | `Crafting` | Crafting (제작) |
| 7 | `Resource` | Resource (자원) |

## BuildingRuntimeArchetypeKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Generic` | Generic |
| 1 | `Facility` | Facility |
| 2 | `Shop` | Shop |
| 3 | `Door` | Door |
| 4 | `InteriorDoor` | Interior Door |
| 5 | `Hallway` | Hallway |
| 6 | `Stair` | Stair |
| 7 | `DefenseFacility` | Defense Facility |

## CareerPositionKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None |
| 1 | `Steward` | Steward |
| 2 | `ChiefResearcher` | Chief Researcher |
| 3 | `ChiefPhysician` | Chief Physician |
| 4 | `GuardCaptain` | Guard Captain |
| 5 | `Foreman` | Foreman |
| 6 | `Mentor` | Mentor |

## CareerPositionScopeKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Global` | Global |
| 1 | `Facility` | Facility |

## CharacterAmbitionCategory

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Mastery` | Mastery |
| 1 | `Family` | Family |
| 2 | `Status` | Status |
| 3 | `Community` | Community |
| 4 | `Faction` | Faction |
| 5 | `VengeanceOrDiscovery` | Vengeance Or Discovery |

## CharacterFunctionalCapacityId

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `MentalMaintenance` | Mental Maintenance |
| 1 | `VisualDiscernment` | Visual Discernment |
| 2 | `AuditorySensing` | Auditory Sensing |
| 3 | `RespiratoryExchange` | Respiratory Exchange |
| 4 | `PowerCirculation` | Power Circulation |
| 5 | `IntakeProcessing` | Intake Processing |
| 6 | `PurificationProcessing` | Purification Processing |
| 7 | `VitalityResponse` | Vitality Response |
| 8 | `PhysicalPower` | Physical Power |
| 9 | `PrecisionManipulation` | Precision Manipulation |
| 10 | `PhysicalMobility` | Physical Mobility |
| 11 | `Communication` | Communication |
| 12 | `ArcaneConduction` | Arcane Conduction |
| 13 | `ImmuneDefense` | Immune Defense |

## CharacterPerformanceFormulaDomain

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Composite` | Composite |
| 1 | `Work` | Work |
| 2 | `Combat` | Combat |
| 3 | `Medical` | Medical |
| 4 | `SurvivalSocial` | Survival Social |

## CharacterPerformanceInputRole

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None |
| 1 | `Contribution` | Contribution |
| 2 | `Bottleneck` | Bottleneck |
| 4 | `Required` | Required |

## CharacterPerformanceResultChannel

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Factor` | Factor |
| 1 | `Speed` | Speed |
| 2 | `AccidentRisk` | Accident Risk |
| 3 | `Quality` | Quality |
| 4 | `Yield` | Yield |
| 5 | `SuccessChance` | Success Chance |
| 6 | `Recovery` | Recovery |
| 7 | `Consumption` | Consumption |
| 8 | `Exposure` | Exposure |
| 9 | `Detection` | Detection |
| 10 | `MoodDuration` | Mood Duration |
| 11 | `RelationshipRecovery` | Relationship Recovery |

## CharacterRespawnSpeedType

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 3 | `VeryFast` | Very Fast |
| 8 | `Fast` | Fast |
| 13 | `Normal` | Normal |
| 18 | `Slow` | Slow |
| 23 | `VerySlow` | Very Slow |

## CharacterRole

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Regular` | Regular |
| 1 | `Owner` | Owner |

## CharacterSpeedType

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 2 | `VerySlow` | Very Slow |
| 3 | `Slow` | Slow |
| 4 | `Normal` | Normal |
| 5 | `Fast` | Fast |
| 6 | `VeryFast` | Very Fast |

## CharacterTraitPolarity

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Advantage` | Advantage (이점) |
| 1 | `Tradeoff` | Tradeoff (상충) |
| 2 | `Negative` | Negative (불리) |
| 3 | `Quirk` | Quirk (기벽) |
| 4 | `Extreme` | Extreme (극단) |

## CharacterTraitSelectionRarity

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Common` | Common (일반) |
| 1 | `Uncommon` | Uncommon (비일반) |
| 2 | `Rare` | Rare (희귀) |
| 3 | `Exceptional` | Exceptional (특별) |

## CharacterType

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `NPC` | N P C |
| 1 | `Customer` | Customer |
| 2 | `Intruder` | Intruder |

## CombatArmorLayer

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Skin` | Skin |
| 1 | `Clothing` | Clothing |
| 2 | `Mail` | Mail |
| 3 | `Plate` | Plate |
| 4 | `Outer` | Outer |

## CombatEquipmentKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `MeleeWeapon` | Melee Weapon |
| 1 | `RangedWeapon` | Ranged Weapon |
| 2 | `RecoverableThrowingWeapon` | Recoverable Throwing Weapon |
| 3 | `Armor` | Armor |
| 4 | `Shield` | Shield |

## CombatMaterialFamily

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Wood` | Wood |
| 1 | `Stone` | Stone |
| 2 | `Bone` | Bone |
| 3 | `Metal` | Metal |
| 4 | `Textile` | Textile |
| 5 | `Leather` | Leather |

## CraftsmanshipQualityTier

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Awful` | Awful |
| 1 | `Poor` | Poor |
| 2 | `Normal` | Normal |
| 3 | `Good` | Good |
| 4 | `Excellent` | Excellent |
| 5 | `Masterwork` | Masterwork |
| 6 | `Legendary` | Legendary |
| 7 | `Mythic` | Mythic |

## CropDiseaseKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None |
| 1 | `GrainFiberRust` | Grain Fiber Rust |
| 2 | `RootRot` | Root Rot |
| 3 | `LeafVinePowderyMildew` | Leaf Vine Powdery Mildew |
| 4 | `MushroomSporeMold` | Mushroom Spore Mold |

## CropFamilyGroup

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Grain` | Grain |
| 1 | `Fiber` | Fiber |
| 2 | `Root` | Root |
| 3 | `Leaf` | Leaf |
| 4 | `Vine` | Vine |
| 5 | `Fungus` | Fungus |

## CulturalPracticeKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `DailyRoutine` | Daily Routine |
| 1 | `Food` | Food |
| 2 | `Room` | Room |
| 3 | `Social` | Social |
| 4 | `ComingOfAge` | Coming Of Age |
| 5 | `Partnership` | Partnership |
| 6 | `Funeral` | Funeral |
| 7 | `WorkRest` | Work Rest |

## DiseaseTargetSystem

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Core` | Core |
| 1 | `Consciousness` | Consciousness |
| 2 | `Breathing` | Breathing |
| 3 | `Digestion` | Digestion |
| 4 | `Filtration` | Filtration |

## DiseaseTransmissionRoute

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None |
| 1 | `Air` | Air |
| 2 | `Droplet` | Droplet |
| 4 | `Blood` | Blood |
| 8 | `Food` | Food |
| 16 | `Water` | Water |
| 32 | `ManaExposure` | Mana Exposure |
| 64 | `Contact` | Contact |
| 128 | `Environment` | Environment |

## EnemyAbilityEffectKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Damage` | Damage |
| 1 | `DamageOverTime` | Damage Over Time |
| 2 | `Heal` | Heal |
| 3 | `Delay` | Delay |
| 4 | `Vulnerability` | Vulnerability |
| 5 | `Suppression` | Suppression |
| 6 | `Smoke` | Smoke |
| 7 | `Summon` | Summon |
| 8 | `Dispel` | Dispel |
| 9 | `Guard` | Guard |

## EnemyCombatRole

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Vanguard` | Vanguard |
| 1 | `Defender` | Defender |
| 2 | `Marksman` | Marksman |
| 3 | `Support` | Support |
| 4 | `Controller` | Controller |
| 5 | `Boss` | Boss |

## EquipmentEra

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Starting` | Starting |
| 1 | `Medieval` | Medieval |
| 2 | `EarlyIndustrial` | Early Industrial |
| 3 | `MatureIndustrial` | Mature Industrial |
| 4 | `RuneAbyssal` | Rune Abyssal |

## EquipmentLineageKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Weapon` | Weapon |
| 1 | `Armor` | Armor |
| 2 | `Shield` | Shield |

## FacilityEvolutionRecordTokenConsumePolicy

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `ConsumeRequiredAmount` | Consume Required Amount |
| 1 | `Preserve` | Preserve |
| 2 | `ConsumeAll` | Consume All |

## FacilityRole

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None |
| 1 | `Meal` | Meal |
| 2 | `Purchase` | Purchase |
| 4 | `Rest` | Rest |
| 8 | `Training` | Training |
| 16 | `Research` | Research |
| 32 | `Mana` | Mana |
| 64 | `Logistics` | Logistics |
| 128 | `Toilet` | Toilet |
| 256 | `Hygiene` | Hygiene |
| 512 | `Administration` | Administration |
| 1024 | `Security` | Security |
| 2048 | `Entertainment` | Entertainment |
| 4096 | `Medical` | Medical |

## FacilityShopRarity

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Common` | Common |
| 1 | `Rare` | Rare |
| 2 | `Special` | Special |

## FacilityUseClassification

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None |
| 1 | `Structure` | Structure |
| 2 | `Storage` | Storage |
| 3 | `Production` | Production |
| 4 | `Service` | Service |
| 5 | `Environment` | Environment |
| 6 | `Logistics` | Logistics |
| 7 | `Combat` | Combat |
| 8 | `DomainCommand` | Domain Command |
| 9 | `EventVenue` | Event Venue |
| 10 | `Decoration` | Decoration |

## FacilityWorkType

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None |
| 1 | `Operate` | Operate |
| 2 | `Restock` | Restock |
| 4 | `Repair` | Repair |
| 8 | `Clean` | Clean |
| 16 | `Research` | Research |
| 32 | `Guard` | Guard |
| 64 | `Rescue` | Rescue |
| 128 | `Rest` | Rest |
| 256 | `Craft` | Craft |
| 512 | `Haul` | Haul |
| 1024 | `Reception` | Reception |
| 2048 | `Hunt` | Hunt |
| 4096 | `Butcher` | Butcher |
| 8192 | `DrawWater` | Draw Water |
| 16384 | `Cook` | Cook |
| 32768 | `Treat` | Treat |
| 65536 | `Refuel` | Refuel |
| 131072 | `Construct` | Construct |
| 262144 | `Warden` | Warden |
| 524288 | `Perform` | Perform |
| 1048576 | `Gather` | Gather |
| 2097152 | `Sow` | Sow |
| 4194304 | `Harvest` | Harvest |
| 8388608 | `Logging` | Logging |
| 16777216 | `Quarry` | Quarry |
| 33554432 | `AnimalCare` | Animal Care |
| 67108864 | `GrandProject` | Grand Project |
| 134217728 | `Surgery` | Surgery |
| 268435456 | `ThreatMitigation` | Threat Mitigation |
| 536870912 | `Plumbing` | Plumbing |
| 1073741824 | `Dismantle` | Dismantle |

## FactionChapterKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `FirstContact` | First Contact |
| 1 | `InternalProblem` | Internal Problem |
| 2 | `RivalConflict` | Rival Conflict |
| 3 | `Intervention` | Intervention |
| 4 | `CrisisOrBetrayal` | Crisis Or Betrayal |
| 5 | `Resolution` | Resolution |

## GameplayEffectOperation

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `AddFlat` | Add Flat |
| 1 | `AddPercent` | Add Percent |
| 2 | `Multiply` | Multiply |
| 3 | `Override` | Override |
| 4 | `ClampMinimum` | Clamp Minimum |
| 5 | `ClampMaximum` | Clamp Maximum |

## GameplayEffectProjectionPhase

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `BaseAdd` | Base Add |
| 1 | `AdditivePercent` | Additive Percent |
| 2 | `Multiplicative` | Multiplicative |
| 3 | `Override` | Override |
| 4 | `Clamp` | Clamp |

## GameplayEffectSourceKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None |
| 1 | `Trait` | Trait |
| 2 | `Species` | Species |
| 4 | `Equipment` | Equipment |
| 8 | `EquipmentModule` | Equipment Module |
| 16 | `Status` | Status |
| 32 | `Research` | Research |
| 63 | `All` | All |

## GameplayEffectStackingPolicy

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `StackAll` | Stack All |
| 1 | `HighestMagnitude` | Highest Magnitude |
| 2 | `LowestMagnitude` | Lowest Magnitude |
| 3 | `UniquePerDefinition` | Unique Per Definition |
| 4 | `UniquePerSource` | Unique Per Source |

## GridLayer

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Hallway` | Hallway |
| 1 | `Building` | Building |
| 2 | `Character` | Character |
| 3 | `WallFixture` | Wall Fixture |
| 4 | `CeilingFixture` | Ceiling Fixture |
| 5 | `FloorOverlay` | Floor Overlay |
| 6 | `Item` | Item |
| 7 | `Wildlife` | Wildlife |
| 8 | `Construction` | Construction |
| 9 | `Filth` | Filth |
| 10 | `DownedCharacter` | Downed Character |
| 11 | `Utility` | Utility |
| 12 | `Conveyor` | Conveyor |

## GuestRequestKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `LuxuryMeal` | Luxury Meal |
| 1 | `Medical` | Medical |
| 2 | `Trade` | Trade |
| 3 | `Spectacle` | Spectacle |
| 4 | `Refuge` | Refuge |
| 5 | `Research` | Research |
| 6 | `Armament` | Armament |

## HeritableTraitCategory

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Anatomy` | Anatomy |
| 1 | `Metabolism` | Metabolism |
| 2 | `Arcane` | Arcane |
| 3 | `Reproduction` | Reproduction |
| 4 | `ImmunityLongevity` | Immunity Longevity |

## HeritableTraitConsequenceKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None |
| 1 | `Aptitude` | Aptitude |
| 2 | `EnvironmentalTolerance` | Environmental Tolerance |
| 3 | `DiseaseResistance` | Disease Resistance |
| 4 | `Fertility` | Fertility |
| 5 | `AgingRate` | Aging Rate |
| 6 | `AnatomyCapacity` | Anatomy Capacity |
| 7 | `ManaAffinity` | Mana Affinity |
| 8 | `NeedRate` | Need Rate |
| 9 | `Movement` | Movement |
| 10 | `ManaOverloadDamage` | Mana Overload Damage |

## LifeEventCategory

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Childhood` | Childhood |
| 1 | `Apprenticeship` | Apprenticeship |
| 2 | `PartnershipFamily` | Partnership Family |
| 3 | `Career` | Career |
| 4 | `ElderRetirement` | Elder Retirement |
| 5 | `DeathLegacy` | Death Legacy |

## LifeEventFrequencyRule

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Repeatable` | Repeatable |
| 1 | `OncePerCharacter` | Once Per Character |
| 2 | `OncePerGeneration` | Once Per Generation |
| 3 | `OncePerRun` | Once Per Run |

## MealDietClass

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Vegan` | Vegan |
| 1 | `Vegetarian` | Vegetarian |
| 2 | `Mixed` | Mixed |
| 3 | `Carnivore` | Carnivore |

## MealQualityBand

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Poor` | Poor |
| 1 | `Simple` | Simple |
| 2 | `Decent` | Decent |
| 3 | `Fine` | Fine |
| 4 | `Lavish` | Lavish |

## MealQualityTier

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Simple` | Simple |
| 1 | `Fine` | Fine |
| 2 | `Lavish` | Lavish |
| 3 | `Preserved` | Preserved |

## MealServingRole

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `FullMeal` | Full Meal |
| 1 | `LightMeal` | Light Meal |
| 2 | `Snack` | Snack |
| 3 | `FieldRation` | Field Ration |
| 4 | `EmergencyOnly` | Emergency Only |

## MedicalProcedureFamily

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Biological` | Biological |
| 1 | `Slime` | Slime |
| 2 | `Myconid` | Myconid |
| 3 | `Avian` | Avian |
| 4 | `Construct` | Construct |
| 5 | `Vampiric` | Vampiric |
| 6 | `Demonic` | Demonic |
| 7 | `Arcane` | Arcane |

## MedicalProcedureUrgency

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Maintenance` | Maintenance |
| 1 | `Elective` | Elective |
| 2 | `Required` | Required |
| 3 | `Emergency` | Emergency |

## OffenseBattleTargetRule

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Self` | Self |
| 1 | `Ally` | Ally |
| 2 | `Enemy` | Enemy |

## OffenseDecisionStage

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Travel` | Travel |
| 1 | `Reconnaissance` | Reconnaissance |
| 2 | `Negotiation` | Negotiation |
| 3 | `Infiltration` | Infiltration |
| 4 | `Camp` | Camp |
| 5 | `Loot` | Loot |
| 6 | `Return` | Return |

## OffenseEncounterObjective

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `DefeatAll` | Defeat All |
| 1 | `SurviveRounds` | Survive Rounds |
| 2 | `ProtectTarget` | Protect Target |
| 3 | `SabotageTarget` | Sabotage Target |
| 4 | `Escape` | Escape |
| 5 | `CaptureLeader` | Capture Leader |

## OffenseThreatModifierKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Temperature` | Temperature |
| 1 | `FuelConsumption` | Fuel Consumption |
| 2 | `AutomatedDefense` | Automated Defense |
| 3 | `Mood` | Mood |
| 4 | `Rest` | Rest |
| 5 | `Sanitation` | Sanitation |
| 6 | `Disease` | Disease |
| 7 | `Lighting` | Lighting |
| 8 | `Accuracy` | Accuracy |
| 9 | `InvasionWarning` | Invasion Warning |
| 10 | `DefenseEvasion` | Defense Evasion |

## ProcessWastewaterComposition

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None |
| 1 | `SanitaryWashwater` | Sanitary Washwater |
| 2 | `FoodProcessWashwater` | Food Process Washwater |
| 3 | `Whey` | Whey |
| 4 | `Brine` | Brine |
| 5 | `FermentationEffluent` | Fermentation Effluent |
| 6 | `MedicalEffluent` | Medical Effluent |
| 7 | `IndustrialEffluent` | Industrial Effluent |
| 8 | `AgriculturalRunoff` | Agricultural Runoff |

## ProductionFlowRole

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Transform` | Transform |
| 1 | `Source` | Source |
| 2 | `Sink` | Sink |

## ProductionProcessClass

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Gathering` | Gathering |
| 1 | `CuttingGrindingWashing` | Cutting Grinding Washing |
| 2 | `CookingSimpleMixing` | Cooking Simple Mixing |
| 3 | `SpinningWeavingWoodworking` | Spinning Weaving Woodworking |
| 4 | `ForgingHeavyAssembly` | Forging Heavy Assembly |
| 5 | `Chemical` | Chemical |
| 6 | `Precision` | Precision |
| 7 | `Medical` | Medical |
| 8 | `Rune` | Rune |
| 9 | `HeavyIndustrial` | Heavy Industrial |

## ProductionProcessKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `WorkOnly` | Work Only |
| 1 | `PassiveBatch` | Passive Batch |

## ReproductionMode

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Pregnancy` | Pregnancy |
| 1 | `Egg` | Egg |
| 2 | `Spore` | Spore |
| 3 | `CoreDivision` | Core Division |
| 4 | `GolemAssembly` | Golem Assembly |

## ResearchBlueprintRule

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None |
| 1 | `Required` | Required |
| 2 | `Shortcut` | Shortcut |

## ResearchFacilityCommandKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None |
| 1 | `GatheringPreparation` | Gathering Preparation |
| 2 | `BloodStageDrainage` | Blood Stage Drainage |
| 3 | `LoggingPreparation` | Logging Preparation |
| 4 | `DirectionalFelling` | Directional Felling |
| 5 | `SelectiveBreeding` | Selective Breeding |
| 6 | `StableHarnessing` | Stable Harnessing |
| 7 | `WildlifeTaming` | Wildlife Taming |
| 8 | `FlowMetering` | Flow Metering |
| 9 | `WeaponPatternAccess` | Weapon Pattern Access |
| 10 | `CropCalendar` | Crop Calendar |
| 11 | `SoilDiagnostics` | Soil Diagnostics |
| 12 | `BreedingSchedule` | Breeding Schedule |
| 13 | `ClimateControl` | Climate Control |
| 14 | `HouseholdRegistry` | Household Registry |
| 15 | `NurseryCare` | Nursery Care |
| 16 | `ClassroomEducation` | Classroom Education |
| 17 | `SupervisedApprenticeship` | Supervised Apprenticeship |
| 18 | `GenerationArchive` | Generation Archive |
| 19 | `AgingAssessment` | Aging Assessment |
| 20 | `BiologicalAgeMeasurement` | Biological Age Measurement |
| 21 | `GeriatricCare` | Geriatric Care |
| 22 | `ChronicCare` | Chronic Care |
| 23 | `PathogenDiagnosis` | Pathogen Diagnosis |
| 24 | `Serology` | Serology |
| 25 | `EpidemicBoard` | Epidemic Board |
| 26 | `GeneticArchive` | Genetic Archive |
| 27 | `GeneticCounseling` | Genetic Counseling |
| 28 | `FamilyPartition` | Family Partition |
| 29 | `GuardianRegistry` | Guardian Registry |
| 30 | `CorpseCare` | Corpse Care |
| 31 | `ClimateMapping` | Climate Mapping |
| 32 | `ChronometricNavigation` | Chronometric Navigation |
| 33 | `SeedSelection` | Seed Selection |
| 34 | `RetireeCare` | Retiree Care |
| 35 | `MentorAcademy` | Mentor Academy |
| 36 | `ResonanceTuning` | Resonance Tuning |
| 37 | `SecureTradeVault` | Secure Trade Vault |
| 38 | `DefenseControl` | Defense Control |
| 39 | `ApparelTailoring` | Apparel Tailoring |
| 40 | `ApparelDecoration` | Apparel Decoration |
| 41 | `HandLaundry` | Hand Laundry |
| 42 | `IndoorDrying` | Indoor Drying |
| 43 | `PoweredLaundry` | Powered Laundry |
| 44 | `ApparelDisplay` | Apparel Display |
| 45 | `DressingChange` | Dressing Change |
| 46 | `ApparelRepair` | Apparel Repair |
| 47 | `FiberSorting` | Fiber Sorting |
| 48 | `FiberScouring` | Fiber Scouring |
| 49 | `ManualSpinning` | Manual Spinning |
| 50 | `TextileFinishing` | Textile Finishing |
| 51 | `PoweredSpinning` | Powered Spinning |
| 52 | `PoweredWeaving` | Powered Weaving |

## ResearchField

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `LifeAndSurvival` | Life And Survival |
| 1 | `CommerceAndCraft` | Commerce And Craft |
| 2 | `DefenseAndTactics` | Defense And Tactics |
| 3 | `RecordsAndArcane` | Records And Arcane |
| 4 | `CaptivityAndEntertainment` | Captivity And Entertainment |
| 5 | `AuthorityAndHousing` | Authority And Housing |
| 6 | `Agriculture` | Agriculture |
| 7 | `Forestry` | Forestry |
| 8 | `Mining` | Mining |
| 9 | `Husbandry` | Husbandry |
| 10 | `Metallurgy` | Metallurgy |
| 11 | `Textiles` | Textiles |
| 12 | `Cuisine` | Cuisine |
| 13 | `Pharmacology` | Pharmacology |
| 14 | `SurgeryAndTransplant` | Surgery And Transplant |
| 15 | `IndustryAndAutomation` | Industry And Automation |
| 16 | `WaterAndSanitation` | Water And Sanitation |

## ResearchPrerequisiteKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Theory` | Theory |
| 1 | `Technique` | Technique |
| 2 | `Engineering` | Engineering |
| 3 | `Safety` | Safety |
| 4 | `Operations` | Operations |

## ResearchRewardKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Facility` | Facility |
| 1 | `ProductionItem` | Production Item |
| 2 | `ProductionRecipe` | Production Recipe |
| 3 | `CombatEquipment` | Combat Equipment |
| 4 | `MedicalProcedure` | Medical Procedure |
| 5 | `CraftMaterial` | Craft Material |
| 6 | `Crop` | Crop |
| 7 | `EnvironmentalWorkwear` | Environmental Workwear |
| 8 | `Ammunition` | Ammunition |
| 9 | `InstallationComponent` | Installation Component |

## ResearchUnlockBundleRole

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Foundation` | Foundation |
| 1 | `ProductionChain` | Production Chain |
| 2 | `EquipmentFamily` | Equipment Family |
| 3 | `ServicePackage` | Service Package |
| 4 | `SystemFacility` | System Facility |
| 5 | `Capstone` | Capstone |

## ResourceIngredientTag

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None |
| 1 | `Plant` | Plant |
| 2 | `Fungus` | Fungus |
| 4 | `Milk` | Milk |
| 8 | `Egg` | Egg |
| 16 | `Meat` | Meat |
| 32 | `Blood` | Blood |
| 64 | `Fat` | Fat |
| 128 | `Fiber` | Fiber |
| 256 | `Wood` | Wood |
| 512 | `Mineral` | Mineral |
| 1024 | `Arcane` | Arcane |
| 2048 | `Spoiled` | Spoiled |
| 4096 | `Forbidden` | Forbidden |
| 8192 | `Fuel` | Fuel |
| 16384 | `Feed` | Feed |
| 32768 | `Sweet` | Sweet |
| 65536 | `Salted` | Salted |

## ResourceItemKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Raw` | Raw |
| 1 | `Intermediate` | Intermediate |
| 2 | `Food` | Food |
| 3 | `Medicine` | Medicine |
| 4 | `Substance` | Substance |
| 5 | `AnimalProduct` | Animal Product |
| 6 | `Waste` | Waste |
| 7 | `Ammunition` | Ammunition |
| 8 | `FinishedGood` | Finished Good |

## RunMilestoneTier

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Legacy` | Legacy |
| 1 | `Grand` | Grand |

## Season

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Spring` | Spring |
| 1 | `Summer` | Summer |
| 2 | `Autumn` | Autumn |
| 3 | `Winter` | Winter |

## ServiceCategory

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Dining` | Dining |
| 1 | `Retail` | Retail |
| 2 | `Lodging` | Lodging |
| 3 | `Bathing` | Bathing |
| 4 | `Medical` | Medical |

## ServiceIncidentKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Brawl` | Brawl |
| 1 | `Theft` | Theft |
| 2 | `Contamination` | Contamination |
| 3 | `CulturalInsult` | Cultural Insult |
| 4 | `ForbiddenMeal` | Forbidden Meal |
| 5 | `MedicalCollapse` | Medical Collapse |
| 6 | `EnvoyConflict` | Envoy Conflict |
| 7 | `Sabotage` | Sabotage |

## ServiceOperationMode

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Direct` | Direct |
| 1 | `Managed` | Managed |
| 2 | `Automated` | Automated |

## ServicePaymentPolicy

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Free` | Free |
| 1 | `PayAfterCompletion` | Pay After Completion |
| 2 | `InternalStaffFree` | Internal Staff Free |

## ServiceProcessStageMask

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None |
| 1 | `Reception` | Reception |
| 2 | `Waiting` | Waiting |
| 4 | `Service` | Service |
| 8 | `Payment` | Payment |
| 16 | `Cleanup` | Cleanup |

## StockCategory

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Food` | Food (식량) |
| 1 | `General` | General (일반) |
| 2 | `Weapon` | Weapon (무기) |
| 3 | `Mana` | Mana (마력) |
| 4 | `Water` | Water (물) |
| 5 | `Medicine` | Medicine (의약품) |
| 6 | `Fuel` | Fuel (연료) |
| 7 | `Ammunition` | Ammunition (탄약) |
| 8 | `Biological` | Biological (생물) |
| 9 | `Knowledge` | Knowledge (지식) |
| 10 | `Blueprint` | Blueprint (설계도) |

## StrategicPressureAxis

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None |
| 1 | `Logistics` | Logistics |
| 2 | `Armament` | Armament |
| 3 | `Manpower` | Manpower |
| 4 | `Intelligence` | Intelligence |

## SurgeryFacilityTag

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None |
| 1 | `Emergency` | Emergency |
| 2 | `Anatomy` | Anatomy |
| 4 | `GeneralSurgery` | General Surgery |
| 8 | `Sterilization` | Sterilization |
| 16 | `Anesthesia` | Anesthesia |
| 32 | `ProstheticAssembly` | Prosthetic Assembly |
| 64 | `Rehabilitation` | Rehabilitation |
| 128 | `OrganStorage` | Organ Storage |
| 256 | `Transplant` | Transplant |
| 512 | `ImmuneControl` | Immune Control |
| 1024 | `IsolationRecovery` | Isolation Recovery |
| 2048 | `ArcaneSurgery` | Arcane Surgery |
| 4096 | `RuneSuture` | Rune Suture |
| 8192 | `AgeTreatment` | Age Treatment |

## SurgicalProcedureKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Suture` | Suture |
| 1 | `Transfusion` | Transfusion |
| 2 | `RemoveForeignBody` | Remove Foreign Body |
| 3 | `HealOrgan` | Heal Organ |
| 4 | `Amputate` | Amputate |
| 5 | `ExtractOrgan` | Extract Organ |
| 6 | `TransplantOrgan` | Transplant Organ |
| 7 | `InstallProsthetic` | Install Prosthetic |
| 8 | `InstallImplant` | Install Implant |
| 9 | `ArcaneModification` | Arcane Modification |
| 10 | `Rehabilitation` | Rehabilitation |
| 11 | `Maintenance` | Maintenance |
| 12 | `SpeciesStabilization` | Species Stabilization |
| 13 | `SpeciesAugmentation` | Species Augmentation |

## TextileMaterialTag

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None |
| 1 | `Woven` | Woven |
| 2 | `NonWoven` | Non Woven |
| 4 | `Plant` | Plant |
| 8 | `Animal` | Animal |
| 16 | `Arcane` | Arcane |
| 32 | `Cold` | Cold |
| 64 | `Heat` | Heat |
| 128 | `Wet` | Wet |
| 256 | `Sterile` | Sterile |
| 512 | `Durable` | Durable |
| 1024 | `Light` | Light |
| 2048 | `Airborne` | Airborne |

## TextileSourceKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Unknown` | Unknown |
| 1 | `Crop` | Crop |
| 2 | `Animal` | Animal |
| 3 | `Synthetic` | Synthetic |
| 4 | `Arcane` | Arcane |
| 5 | `Salvaged` | Salvaged |

## V20ContentEffectKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None (없음) |
| 1 | `Mood` | Mood (기분) |
| 2 | `Trauma` | Trauma (트라우마) |
| 3 | `SkillExperience` | SkillExperience (숙련 경험치) |
| 4 | `Health` | Health (건강) |
| 5 | `Relationship` | Relationship (관계) |
| 6 | `FactionRapport` | FactionRapport (세력 우호도) |
| 7 | `FactionGrievance` | FactionGrievance (세력 원한) |
| 8 | `FactionObligation` | FactionObligation (세력 의무) |
| 9 | `Money` | Money (자금) |
| 10 | `ItemGrant` | ItemGrant (아이템 지급) |
| 11 | `ItemConsume` | ItemConsume (아이템 소비) |
| 12 | `WorldFlag` | WorldFlag (세계 플래그) |
| 13 | `WorkDelayDays` | WorkDelayDays (작업 지연) |
| 14 | `Threat` | Threat (위협) |
| 15 | `DiseaseExposure` | DiseaseExposure (질병 노출) |
| 16 | `AmbitionProgress` | AmbitionProgress (야망 진행) |
| 17 | `MilestonePressure` | MilestonePressure (이정표 압력) |

## V20FactionContractKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Supply` | Supply |
| 1 | `CrisisResponse` | Crisis Response |
| 2 | `Strategic` | Strategic |

## V20WorldMetricKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `None` | None |
| 1 | `Population` | Population |
| 2 | `Money` | Money |
| 3 | `FoodDays` | Food Days |
| 4 | `DefenseReadiness` | Defense Readiness |
| 5 | `ProductionAutomation` | Production Automation |
| 6 | `RunePower` | Rune Power |
| 7 | `SelfSufficiencyDays` | Self Sufficiency Days |
| 8 | `CompletedGenerations` | Completed Generations |
| 9 | `DefeatedHumanBranches` | Defeated Human Branches |
| 10 | `PerCapitaNetWuIndex` | Per Capita Net Wu Index |
| 11 | `EmergencyReserveCoverage` | Emergency Reserve Coverage |
| 12 | `ProductivityCoverageDays` | Productivity Coverage Days |
| 13 | `CultureAcceptance` | Culture Acceptance |
| 14 | `PerCapitaServiceIndex` | Per Capita Service Index |

## WeatherFrontKind

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Clear` | Clear |
| 1 | `Rain` | Rain |
| 2 | `Fog` | Fog |
| 3 | `Heatwave` | Heatwave |
| 4 | `ColdSnap` | Cold Snap |
| 5 | `Storm` | Storm |

## WildlifeDietType

| 값 | 이름 | 문서 표기 |
|---:|---|---|
| 0 | `Herbivore` | Herbivore |
| 1 | `Omnivore` | Omnivore |
| 2 | `Carnivore` | Carnivore |
| 3 | `Scavenger` | Scavenger |

