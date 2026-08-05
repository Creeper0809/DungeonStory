#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class OffenseStrategicContentBuilder
{
    private const string Root = "Assets/Resources/SO/Offense";

    [MenuItem("DungeonStory/Build/Offense Strategic Content")]
    public static void BuildAll()
    {
        EnsureFolder(Root);
        BuildSiteArchetypes();
        BuildUrgentSites();
        BuildDecisionCards();
        BuildEncounters();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Offense Strategic content built: 12 sites, 6 urgent sites, 49 cards, 6 encounters.");
    }

    private static void BuildSiteArchetypes()
    {
        string folder = Root + "/Sites";
        EnsureFolder(folder);
        SiteSeed[] seeds =
        {
            new SiteSeed("farm", "변경 농장", "보급과 사료가 모이는 소규모 농장입니다.",
                StrategicPressureAxis.Logistics, 12f, 1, 3, true,
                StockReward("식량 전리품", StockCategory.Food, 10, 4)),
            new SiteSeed("caravan", "이동 상단", "도로를 따라 물자와 정보를 옮기는 상단입니다.",
                StrategicPressureAxis.Logistics, 10f, 1, 3, true,
                MoneyReward("압수한 자금", 140, 60),
                StockReward("상단 물자", StockCategory.General, 8, 3)),
            new SiteSeed("armory", "전진 무기고", "순찰대의 무기와 탄약을 보급하는 무기고입니다.",
                StrategicPressureAxis.Armament, 16f, 2, 5, false,
                StockReward("노획 무기", StockCategory.Weapon, 2, 1),
                StockReward("노획 탄약", StockCategory.Ammunition, 12, 5)),
            new SiteSeed("quarry", "징발 채석장", "성벽과 공성 장비 재료를 캐내는 채석장입니다.",
                StrategicPressureAxis.Armament, 12f, 1, 4, false,
                StockReward("채석 자재", StockCategory.General, 14, 6)),
            new SiteSeed("watchtower", "감시탑", "던전 주변의 움직임을 기록하는 감시탑입니다.",
                StrategicPressureAxis.Intelligence, 15f, 2, 4, false,
                StockReward("정찰 기록", StockCategory.Knowledge, 4, 2)),
            new SiteSeed("patrol", "순찰대 야영지", "병력의 이동 거점으로 쓰이는 임시 야영지입니다.",
                StrategicPressureAxis.Manpower, 13f, 2, 5, true,
                StockReward("야영 보급", StockCategory.Medicine, 3, 1),
                StockReward("순찰대 탄약", StockCategory.Ammunition, 8, 4)),
            new SiteSeed("ruin", "버려진 폐허", "물자와 오래된 기록이 남아 있을 수 있는 폐허입니다.",
                StrategicPressureAxis.None, 0f, 1, 4, false,
                StockReward("폐허의 잔재", StockCategory.General, 8, 5),
                BlueprintReward("회수한 설계도", 0, 1)),
            new SiteSeed("archive", "봉인 기록소", "지역의 길과 약점을 기록한 문서가 보관되어 있습니다.",
                StrategicPressureAxis.Intelligence, 18f, 3, 6, false,
                StockReward("봉인 지식", StockCategory.Knowledge, 8, 3),
                BlueprintReward("봉인 설계도", 1, 0)),
            new SiteSeed("prisoner_convoy", "포로 수송대", "붙잡힌 이들과 경비가 이동하는 수송대입니다.",
                StrategicPressureAxis.Manpower, 14f, 2, 5, true,
                PrisonerReward("구출한 포로", 1, 0)),
            new SiteSeed("ritual_site", "의식 장소", "지역 전체에 영향을 주는 의식이 준비되고 있습니다.",
                StrategicPressureAxis.Intelligence, 20f, 3, 7, false,
                StockReward("의식 마나", StockCategory.Mana, 8, 4),
                StockReward("의식 기록", StockCategory.Knowledge, 5, 2)),
            new SiteSeed("fixed:rival-dungeon", "경쟁 던전 전초권",
                "오랫동안 던전 주변을 압박해 온 경쟁 세력의 중심 권역입니다.",
                StrategicPressureAxis.Manpower, 20f, 5, 5, false, false,
                StockReward("경쟁 던전 무기", StockCategory.Weapon, 8, 2),
                StockReward("경쟁 던전 탄약", StockCategory.Ammunition, 24, 6)),
            new SiteSeed("fixed:truth-core", "봉인 기록 심장부",
                "모든 원정 기록이 가리키는 봉인 지대의 심장부입니다.",
                StrategicPressureAxis.None, 0f, 8, 8, false, false,
                StockReward("진실의 기록", StockCategory.Knowledge, 24, 4))
        };

        for (int index = 0; index < seeds.Length; index++)
        {
            SiteSeed seed = seeds[index];
            OffenseSiteArchetypeSO asset = LoadOrCreate<OffenseSiteArchetypeSO>(
                $"{folder}/{SafeAssetName(seed.Id)}.asset");
            asset.id = 170100 + index;
            asset.siteTypeId = seed.Id;
            asset.displayName = seed.Name;
            asset.description = seed.Description;
            asset.factionId = seed.Id is "ruin" or "ritual_site" ? "sealed" : "human";
            asset.pressureAxis = seed.Axis;
            asset.pressureAmount = seed.Pressure;
            asset.minimumStrength = seed.MinimumStrength;
            asset.maximumStrength = seed.MaximumStrength;
            asset.minimumLifetimeDays = 2;
            asset.maximumLifetimeDays = seed.Moves ? 5 : 7;
            asset.hiddenUntilDiscovered = true;
            asset.canMove = seed.Moves;
            asset.dynamicSpawnEligible = seed.DynamicSpawnEligible;
            asset.rewards = seed.Rewards.ToList();
            if (seed.Axis != StrategicPressureAxis.None && seed.Pressure > 0f)
            {
                asset.rewards.Add(new OffenseSiteRewardDefinition(
                    "지역 전략 압력",
                    1,
                    0,
                    new OffenseRegionalPressureRewardSpec()));
            }
            EditorUtility.SetDirty(asset);
        }
    }

    private static OffenseSiteRewardDefinition StockReward(
        string label,
        StockCategory category,
        int baseAmount,
        int amountPerStrength)
    {
        return new OffenseSiteRewardDefinition(
            label,
            baseAmount,
            amountPerStrength,
            new OffenseStockRewardSpec(category));
    }

    private static OffenseSiteRewardDefinition MoneyReward(
        string label,
        int baseAmount,
        int amountPerStrength)
    {
        return new OffenseSiteRewardDefinition(
            label,
            baseAmount,
            amountPerStrength,
            new OffenseMoneyRewardSpec());
    }

    private static OffenseSiteRewardDefinition BlueprintReward(
        string label,
        int baseAmount,
        int amountPerStrength)
    {
        return new OffenseSiteRewardDefinition(
            label,
            baseAmount,
            amountPerStrength,
            new OffenseAnyBlueprintRewardSpec());
    }

    private static OffenseSiteRewardDefinition PrisonerReward(
        string label,
        int baseAmount,
        int amountPerStrength)
    {
        return new OffenseSiteRewardDefinition(
            label,
            baseAmount,
            amountPerStrength,
            new OffensePrisonerRewardSpec());
    }

    private static void BuildUrgentSites()
    {
        string folder = Root + "/UrgentSites";
        EnsureFolder(folder);
        UrgentSeed[] seeds =
        {
            new UrgentSeed("heat_emitter", "열기 방출탑",
                "던전 주변의 공기를 달궈 냉각과 연료 부담을 높입니다.",
                OffenseThreatModifierKind.Temperature, "work:operate", "material:low-fuel"),
            new UrgentSeed("defense_disruptor", "방어 교란기",
                "자동 방어 장치의 감응과 발동을 흐트러뜨립니다.",
                OffenseThreatModifierKind.AutomatedDefense, "work:research", "resource:mana-crystal"),
            new UrgentSeed("dissonant_choir", "불협 성가대",
                "잠과 집중을 망가뜨리는 낮은 노래가 계속 울립니다.",
                OffenseThreatModifierKind.Mood, "work:perform", "resource:mana-crystal"),
            new UrgentSeed("rot_fog_nest", "부패 안개 둥지",
                "오염된 안개가 위생을 낮추고 질병을 퍼뜨립니다.",
                OffenseThreatModifierKind.Sanitation, "work:clean", "medicine:standard"),
            new UrgentSeed("shadow_screen", "그림자 차광막",
                "던전의 조명과 원거리 명중을 약화시키는 그림자를 드리웁니다.",
                OffenseThreatModifierKind.Lighting, "work:refuel", "material:low-fuel"),
            new UrgentSeed("siege_observer", "공성 관측소",
                "침공 경로와 방어 사각을 관측해 다음 공격을 돕습니다.",
                OffenseThreatModifierKind.InvasionWarning, "work:guard", "material:lumber")
        };

        for (int index = 0; index < seeds.Length; index++)
        {
            UrgentSeed seed = seeds[index];
            OffenseUrgentSiteDefinitionSO asset =
                LoadOrCreate<OffenseUrgentSiteDefinitionSO>(
                    $"{folder}/{seed.Id}.asset");
            asset.id = 170200 + index;
            asset.urgentSiteId = seed.Id;
            asset.displayName = seed.Name;
            asset.description = seed.Description;
            asset.modifierKind = seed.Kind;
            asset.maximumStrength = 1f;
            asset.maximumMitigation = 0.6f;
            asset.mitigationWorkTypeId = seed.WorkTypeId;
            asset.mitigationItemId = seed.ItemId;
            asset.mitigationItemAmount = 3;
            asset.mitigationWork = 24f;
            EditorUtility.SetDirty(asset);
        }
    }

    private static void BuildDecisionCards()
    {
        string folder = Root + "/Cards";
        EnsureFolder(folder);
        List<CardSeed> seeds = CreateCardSeeds();
        if (seeds.Count < 48)
        {
            throw new InvalidOperationException(
                $"Offense Strategic requires at least 48 initial cards, got {seeds.Count}.");
        }

        for (int index = 0; index < seeds.Count; index++)
        {
            CardSeed seed = seeds[index];
            string path = $"{folder}/{index + 1:00}_{seed.Id}.asset";
            OffenseDecisionCardSO asset = LoadOrCreate<OffenseDecisionCardSO>(path);
            asset.id = 170300 + index;
            asset.cardId = seed.Id;
            asset.stage = seed.Stage;
            asset.title = seed.Title;
            asset.situation = seed.Situation;
            asset.requiredWorldTags = new List<string>();
            asset.choices = new List<OffenseDecisionChoiceDefinition>
            {
                new OffenseDecisionChoiceDefinition
                {
                    choiceId = "left",
                    label = seed.Left,
                    description = seed.LeftDescription,
                    requiredTag = seed.LeftRequiredTag,
                    transformedLabel = seed.LeftTransformedLabel,
                    transformedDescription = seed.LeftTransformedDescription,
                    directionLabel = seed.LeftDirection,
                    severity = seed.LeftSeverity,
                    mayStartCombat = seed.LeftCombat,
                    mayCauseInjury = seed.LeftInjury,
                    mayMoveExpedition = seed.LeftMove,
                    effects = BuildDecisionEffects(
                        seed.LeftDirection,
                        seed.LeftSeverity,
                        seed.LeftCombat,
                        seed.LeftInjury,
                        seed.LeftMove)
                },
                new OffenseDecisionChoiceDefinition
                {
                    choiceId = "right",
                    label = seed.Right,
                    description = seed.RightDescription,
                    requiredTag = seed.RightRequiredTag,
                    transformedLabel = seed.RightTransformedLabel,
                    transformedDescription = seed.RightTransformedDescription,
                    directionLabel = seed.RightDirection,
                    severity = seed.RightSeverity,
                    mayStartCombat = seed.RightCombat,
                    mayCauseInjury = seed.RightInjury,
                    mayMoveExpedition = seed.RightMove,
                    effects = BuildDecisionEffects(
                        seed.RightDirection,
                        seed.RightSeverity,
                        seed.RightCombat,
                        seed.RightInjury,
                        seed.RightMove)
                }
            };
            ConfigureBribeOffer(asset);
            EditorUtility.SetDirty(asset);
        }
    }

    private static void ConfigureBribeOffer(OffenseDecisionCardSO card)
    {
        if (card == null
            || !string.Equals(
                card.cardId,
                "negotiation_gate_captain",
                StringComparison.Ordinal)
            || card.choices == null
            || card.choices.Count == 0)
        {
            return;
        }

        OffenseGoldDecisionEffect gold = card.choices[0].effects?
            .OfType<OffenseGoldDecisionEffect>()
            .FirstOrDefault();
        if (gold == null)
        {
            gold = new OffenseGoldDecisionEffect { amount = -125 };
            card.choices[0].effects.Add(gold);
        }

        int price = Mathf.Max(1, -gold.amount);
        gold.amount = -price;
        gold.bribe = new BribeOffer
        {
            offerId = "bribe:gate-captain:passage",
            factionId = "human",
            price = price,
            outcome = BribeOutcomeKind.Passage,
            acceptancePercent = 85,
            acceptedResult = "문지기 대장이 순찰 교대 틈을 열어 주었습니다.",
            rejectedResult = "문지기 대장이 돈을 챙기고도 통행을 거부했습니다."
        };
    }

    private static List<OffenseDecisionEffectDefinition> BuildDecisionEffects(
        string direction,
        int severity,
        bool startsCombat,
        bool causesInjury,
        bool forcesMovement)
    {
        string text = direction ?? string.Empty;
        int scale = Mathf.Clamp(severity, 0, 3) + 1;
        List<OffenseDecisionEffectDefinition> effects =
            new List<OffenseDecisionEffectDefinition>();

        AddSupplyEffect(
            effects,
            text,
            "식량",
            OffenseSupplyType.Rations,
            scale);
        AddSupplyEffect(
            effects,
            text,
            "약품",
            OffenseSupplyType.Medicine,
            scale);
        AddSupplyEffect(
            effects,
            text,
            "도구",
            OffenseSupplyType.Tools,
            scale);
        AddSupplyEffect(
            effects,
            text,
            "마나",
            OffenseSupplyType.ManaLantern,
            scale);
        AddSupplyEffect(
            effects,
            text,
            "연료",
            OffenseSupplyType.ManaLantern,
            scale);
        AddSupplyEffect(
            effects,
            text,
            "물",
            OffenseSupplyType.Rations,
            scale);

        bool hasSpecificSupply = effects.Any(effect =>
            effect is OffenseSupplyDecisionEffect);
        if (!hasSpecificSupply && text.Contains("보급 감소"))
        {
            effects.Add(new OffenseSupplyDecisionEffect
            {
                supplyType = OffenseSupplyType.Rations,
                amount = -1
            });
        }
        else if (!hasSpecificSupply && text.Contains("보급 증가"))
        {
            effects.Add(new OffenseSupplyDecisionEffect
            {
                supplyType = OffenseSupplyType.Rations,
                amount = Mathf.Max(1, scale)
            });
        }
        else if (!hasSpecificSupply && text.Contains("보급 손실"))
        {
            effects.Add(new OffenseSupplyDecisionEffect
            {
                supplyType = OffenseSupplyType.Rations,
                amount = -1
            });
        }
        else if (!hasSpecificSupply && text.Contains("보급 변화"))
        {
            effects.Add(new OffenseSupplyDecisionEffect
            {
                supplyType = OffenseSupplyType.Tools,
                amount = -1
            });
            effects.Add(new OffenseSupplyDecisionEffect
            {
                supplyType = OffenseSupplyType.Rations,
                amount = 2
            });
        }

        if (text.Contains("골드 감소"))
        {
            effects.Add(new OffenseGoldDecisionEffect
            {
                amount = -(75 + scale * 25)
            });
        }
        else if (text.Contains("골드 증가"))
        {
            effects.Add(new OffenseGoldDecisionEffect
            {
                amount = 50 + scale * 25
            });
        }

        float stress = 0f;
        if (text.Contains("스트레스 증가") || text.Contains("피로 증가"))
        {
            stress += 4f + scale * 3f;
        }
        if (text.Contains("스트레스 감소")
            || text.Contains("피로 회복")
            || text.Contains("기분 보상"))
        {
            stress -= 6f + scale * 3f;
        }
        if (stress != 0f)
        {
            effects.Add(new OffenseStressDecisionEffect { amount = stress });
        }

        float exposure = 0f;
        if (text.Contains("노출 증가")
            || text.Contains("노출 위험")
            || text.Contains("발각 위험")
            || text.Contains("경계 상승")
            || text.Contains("경계 크게 상승")
            || text.Contains("적대 증가"))
        {
            exposure += text.Contains("크게") ? 12f + scale * 5f : 6f + scale * 4f;
        }
        if (text.Contains("노출 감소")
            || text.Contains("위험 감소")
            || text.Contains("위험 크게 감소")
            || text.Contains("기습 유리")
            || text.Contains("전투 난도 감소"))
        {
            exposure -= text.Contains("크게") ? 12f + scale * 5f : 6f + scale * 4f;
        }
        if (exposure != 0f)
        {
            effects.Add(new OffenseExposureDecisionEffect { amount = exposure });
        }
        else if (text.Contains("노출 증가")
            && text.Contains("기습 유리"))
        {
            effects.Add(new OffenseExposureDecisionEffect { amount = 5f });
        }
        else if (text.Contains("침투"))
        {
            effects.Add(new OffenseExposureDecisionEffect
            {
                amount = -(5f + scale * 2f)
            });
        }

        if (text.Contains("정보")
            || text.Contains("비밀 발견")
            || text.Contains("거점 발견")
            || text.Contains("숨은 경로 발견"))
        {
            effects.Add(new OffenseReconDecisionEffect
            {
                revealCount = text.Contains("크게") ? 2 : 1
            });
        }

        if (text.Contains("전리품 증가")
            || text.Contains("전리품 소폭 증가")
            || text.Contains("전리품 크게 증가")
            || text.Contains("전리품 가능")
            || text.Contains("희귀 전리품")
            || text.Contains("촉매 획득"))
        {
            effects.Add(new OffenseLootDecisionEffect
            {
                stockCategory = text.Contains("희귀") || text.Contains("촉매")
                    ? StockCategory.Mana
                    : StockCategory.General,
                amount = text.Contains("크게") ? 4 + scale * 2 : 2 + scale
            });
        }
        if (text.Contains("전리품 감소"))
        {
            effects.Add(new OffenseLootDecisionEffect
            {
                stockCategory = StockCategory.General,
                amount = -Mathf.Max(1, scale)
            });
        }

        if (text.Contains("시간 감소"))
        {
            effects.Add(new OffenseTimeDecisionEffect
            {
                elapsedHours = 0.5f + scale * 0.5f
            });
        }

        if (text.Contains("장비 위험")
            || text.Contains("장비 손상 위험"))
        {
            effects.Add(new OffenseEquipmentWearDecisionEffect
            {
                durabilityDamage = 4f + scale * 3f
            });
        }

        if (causesInjury || text.Contains("부상 위험"))
        {
            effects.Add(new OffenseInjuryDecisionEffect
            {
                maxHealthRatio = 0.04f + scale * 0.025f,
                nonLethal = true
            });
        }
        if (text.Contains("부상 회복"))
        {
            effects.Add(new OffenseInjuryDecisionEffect
            {
                maxHealthRatio = -(0.05f + scale * 0.025f),
                nonLethal = true
            });
        }

        if (startsCombat)
        {
            effects.Add(new OffenseCombatDecisionEffect());
        }
        if (forcesMovement)
        {
            effects.Add(new OffenseForcedMoveDecisionEffect());
        }

        return effects;
    }

    private static void AddSupplyEffect(
        ICollection<OffenseDecisionEffectDefinition> effects,
        string text,
        string keyword,
        OffenseSupplyType type,
        int scale)
    {
        if (text.Contains($"{keyword} 감소"))
        {
            effects.Add(new OffenseSupplyDecisionEffect
            {
                supplyType = type,
                amount = -1
            });
        }
        else if (text.Contains($"{keyword} 증가"))
        {
            effects.Add(new OffenseSupplyDecisionEffect
            {
                supplyType = type,
                amount = Mathf.Max(1, scale)
            });
        }
    }

    private static void BuildEncounters()
    {
        string folder = Root + "/Encounters";
        EnsureFolder(folder);
        string[] names =
        {
            "변경 순찰대", "무기고 수비대", "봉인 수색조",
            "정예 추적대", "경쟁 던전 지휘부", "기록 심장부 수호자"
        };
        for (int index = 0; index < names.Length; index++)
        {
            OffenseEncounterSO asset = LoadOrCreate<OffenseEncounterSO>(
                $"{folder}/encounter_{index + 1:00}.asset");
            asset.id = 170400 + index;
            asset.encounterId = $"encounter:{index + 1:00}";
            asset.displayName = names[index];
            asset.minimumSiteStrength = Mathf.Max(1, index);
            asset.maximumSiteStrength = index + 3;
            asset.elite = index is 3 or 4;
            asset.boss = index >= 4;
            asset.enemies = new List<OffenseEnemyArchetypeEntry>
            {
                new OffenseEnemyArchetypeEntry
                {
                    enemyArchetypeId = index >= 4 ? "boss_guard" : "human_guard",
                    minimumCount = Mathf.Clamp(index + 1, 1, 4),
                    maximumCount = Mathf.Clamp(index + 2, 2, 6)
                }
            };
            EditorUtility.SetDirty(asset);
        }
    }

    private static List<CardSeed> CreateCardSeeds()
    {
        return new List<CardSeed>
        {
            Card("travel_broken_bridge", OffenseDecisionStage.Travel, "끊어진 교량",
                "강물이 불어난 협곡 앞에서 오래된 교량이 반쯤 무너져 있습니다.",
                "다리를 보강한다", "도구와 시간을 써서 안전하게 건넙니다.", "보급 감소", 1,
                "얕은 여울을 찾는다", "우회하며 발각 가능성을 감수합니다.", "노출 증가", 1,
                leftTag: "tools", leftAlt: "도구로 빠르게 고친다", leftAltDescription: "준비한 공구 덕분에 지체가 크게 줄어듭니다."),
            Card("travel_toll_gate", OffenseDecisionStage.Travel, "임시 통행세",
                "무장한 징수대가 도로를 막고 통행료를 요구합니다.",
                "금을 지불한다", "분쟁 없이 길을 통과합니다.", "골드 감소", 1,
                "숲길로 비킨다", "시간과 보급을 더 써서 감시를 피합니다.", "보급 감소", 1),
            Card("travel_wounded_scout", OffenseDecisionStage.Travel, "쓰러진 정찰병",
                "길가에서 적의 표식을 단 정찰병이 피를 흘리고 있습니다.",
                "치료하고 묻는다", "약품을 쓰고 주변 정보의 대가를 요구합니다.", "약품 감소·정보 증가", 1,
                "흔적만 조사한다", "거리를 유지한 채 지나온 경로를 살핍니다.", "정보 소폭 증가", 0),
            Card("travel_black_rain", OffenseDecisionStage.Travel, "검은 비",
                "먹구름에서 기름 냄새가 나는 빗방울이 떨어지기 시작합니다.",
                "천막을 친다", "보급을 보호하며 비가 잦아들기를 기다립니다.", "피로 증가", 1,
                "행군을 계속한다", "시간을 아끼지만 장비와 몸이 젖습니다.", "장비 손상 위험", 2, rightInjury: true),
            Card("travel_false_milestone", OffenseDecisionStage.Travel, "뒤집힌 이정표",
                "누군가 이정표의 방향판을 바꿔 놓았습니다.",
                "지형을 대조한다", "높은 곳에서 길과 별자리를 다시 맞춥니다.", "시간 감소", 1,
                "발자국을 따른다", "최근 지나간 무리의 흔적을 따라갑니다.", "매복 위험", 2, rightCombat: true),
            Card("travel_abandoned_cart", OffenseDecisionStage.Travel, "버려진 수레",
                "바퀴가 부서진 수레에 밀봉된 자루가 남아 있습니다.",
                "쓸 만한 것만 챙긴다", "무게를 살피며 필요한 물자만 고릅니다.", "보급 증가·적재 증가", 1,
                "손대지 않는다", "미끼일 가능성을 피해 그대로 지나갑니다.", "위험 감소", 0),
            Card("travel_narrow_pass", OffenseDecisionStage.Travel, "좁은 고갯길",
                "한 줄로만 지나갈 수 있는 고갯길 위에서 돌이 굴러옵니다.",
                "방패를 세운다", "전열이 충격을 받아내며 길을 엽니다.", "부상 위험", 2,
                "뒤로 물러난다", "한 칸 우회해 다른 비탈을 찾습니다.", "이동 지연", 1,
                leftInjury: true, rightMove: true),

            Card("recon_smoke_column", OffenseDecisionStage.Reconnaissance, "연기 기둥",
                "수평선 너머에서 일정한 간격으로 연기가 오릅니다.",
                "가까이 관찰한다", "경계 안쪽까지 접근해 병력 교대를 셉니다.", "정보 크게 증가·발각 위험", 2,
                "형태만 기록한다", "안전 거리에서 신호의 규칙만 남깁니다.", "정보 소폭 증가", 0),
            Card("recon_watch_rotation", OffenseDecisionStage.Reconnaissance, "감시 교대",
                "망루의 불빛이 꺼지는 짧은 틈이 반복됩니다.",
                "교대 틈을 잰다", "침투에 쓸 정확한 시간을 기록합니다.", "기습 유리", 1,
                "초병을 유인한다", "작은 소리로 시선을 다른 곳에 묶습니다.", "노출 위험", 2),
            Card("recon_buried_map", OffenseDecisionStage.Reconnaissance, "묻힌 지도통",
                "젖은 흙 아래에서 군용 지도통의 모서리가 보입니다.",
                "봉인을 해제한다", "안쪽 문서의 함정 여부를 확인하며 엽니다.", "정보 크게 증가", 1,
                "그대로 가져간다", "던전에서 분석하기 위해 밀봉한 채 챙깁니다.", "전리품 증가·적재 증가", 1),
            Card("recon_prisoner_signal", OffenseDecisionStage.Reconnaissance, "포로의 손짓",
                "울타리 안쪽의 포로가 감시를 피해 같은 동작을 반복합니다.",
                "신호에 응답한다", "위험을 감수하고 짧은 암호를 주고받습니다.", "숨은 경로 발견·발각 위험", 2,
                "기억만 해둔다", "위치와 인상착의를 기록하고 물러납니다.", "정보 증가", 0),
            Card("recon_old_runes", OffenseDecisionStage.Reconnaissance, "낡은 경계문",
                "지워진 룬이 현재 순찰로와 어긋난 방향을 가리킵니다.",
                "룬을 복원한다", "마나를 써서 과거의 경계선을 읽습니다.", "마나 감소·비밀 발견", 1,
                "현재 흔적을 따른다", "발자국과 불씨만으로 실제 경로를 추정합니다.", "시간 감소", 1),
            Card("recon_counter_scout", OffenseDecisionStage.Reconnaissance, "맞은편 망원경",
                "관찰하던 능선 반대편에서 렌즈가 번쩍입니다.",
                "정찰병을 쫓는다", "정보가 새기 전에 추격해 입을 막습니다.", "전투 위험", 2,
                "거짓 흔적을 남긴다", "다른 방향으로 이동한 것처럼 흔적을 꾸밉니다.", "노출 감소", 1,
                leftCombat: true),
            Card("recon_supply_marks", OffenseDecisionStage.Reconnaissance, "상자 표식",
                "운반 상자의 기호가 거점마다 다른 보급 시간을 드러냅니다.",
                "보급선을 표시한다", "다음 작전에서 노릴 연결점을 기록합니다.", "물류 정보 증가", 0,
                "상자 하나를 훔친다", "당장 쓸 물자를 얻지만 경계를 높입니다.", "보급 증가·경계 상승", 1),

            Card("negotiation_border_merchant", OffenseDecisionStage.Negotiation, "변경 상인",
                "중립 표식을 단 상인이 양쪽과 거래했다며 웃습니다.",
                "정보를 산다", "골드로 다음 거점의 경비 정보를 구입합니다.", "골드 감소·정보 증가", 1,
                "물자를 교환한다", "남는 보급을 현장 수리품과 맞바꿉니다.", "보급 변화", 1),
            Card("negotiation_deserter", OffenseDecisionStage.Negotiation, "탈영병의 조건",
                "탈영병이 안전한 길과 옛 동료의 배치를 대가로 요구합니다.",
                "귀환을 보장한다", "약속을 담보로 자세한 정보를 받습니다.", "정보 크게 증가·노출 증가", 1,
                "즉석에서 값을 치른다", "골드를 내고 서로의 이름을 묻지 않습니다.", "골드 감소·정보 증가", 1),
            Card("negotiation_hostage_exchange", OffenseDecisionStage.Negotiation, "인질 교환 제안",
                "적의 전령이 붙잡힌 이를 두고 거래를 제안합니다.",
                "협상 시간을 번다", "대화를 길게 끌며 주변 경계를 살핍니다.", "정보 증가·시간 감소", 1,
                "제안을 거절한다", "약점을 보이지 않지만 다음 교전이 거칠어집니다.", "적대 증가", 1),
            Card("negotiation_false_relic", OffenseDecisionStage.Negotiation, "성물 감정",
                "행상이 봉인 지대에서 나온 성물이라며 물건을 내밉니다.",
                "작동을 시험한다", "위험을 통제한 채 진위를 확인합니다.", "마나 위험·전리품 가능", 2,
                "출처를 캐묻는다", "물건보다 운반 경로와 판매자를 추적합니다.", "정보 증가", 0),
            Card("negotiation_gate_captain", OffenseDecisionStage.Negotiation, "문지기 대장",
                "문지기 대장이 통과를 허락하는 대신 작은 부탁을 요구합니다.",
                "뇌물을 건넨다", "골드로 빠르고 조용한 통행을 삽니다.", "골드 감소·노출 감소", 1,
                "부탁을 들어준다", "옆길의 위험 요소를 처리하고 통과합니다.", "시간 감소·부상 위험", 2, rightInjury: true),
            Card("negotiation_competing_raiders", OffenseDecisionStage.Negotiation, "다른 약탈대",
                "같은 거점을 노리는 작은 약탈대가 공동 공격을 제안합니다.",
                "전열을 나눈다", "병력을 분산시켜 정면 경계를 흔듭니다.", "전투 난도 감소·전리품 감소", 1,
                "먼저 치게 둔다", "상대가 소모될 때까지 숨어 기다립니다.", "시간 감소·노출 감소", 1),
            Card("negotiation_choir_envoy", OffenseDecisionStage.Negotiation, "성가대의 전령",
                "얼굴을 가린 전령이 노래를 멈출 대가를 제시합니다.",
                "대가를 듣는다", "조건을 들으며 의식의 구조를 파악합니다.", "정보 증가·스트레스 증가", 1,
                "전령을 붙잡는다", "도주로를 끊고 거점 위치를 캐냅니다.", "전투 위험·정보 크게 증가", 2, rightCombat: true),

            Card("infiltration_drain", OffenseDecisionStage.Infiltration, "배수로",
                "좁은 배수로가 외벽 아래로 이어져 있습니다.",
                "배수로로 들어간다", "오염을 감수하고 경계 안쪽으로 침투합니다.", "위생 악화·기습 유리", 2,
                "정문 교대를 기다린다", "보급을 쓰며 더 안전한 틈을 기다립니다.", "보급 감소·위험 감소", 1),
            Card("infiltration_rope_wall", OffenseDecisionStage.Infiltration, "낮은 성벽",
                "벽돌이 빠진 구간은 오를 수 있지만 위에서 잘 보입니다.",
                "밤에 밧줄을 건다", "도구를 써서 조용히 벽을 넘습니다.", "도구 감소·노출 감소", 1,
                "경비를 유인한다", "반대편에 소란을 만들어 빈 틈을 냅니다.", "노출 증가·기습 유리", 2),
            Card("infiltration_service_cart", OffenseDecisionStage.Infiltration, "보급 수레",
                "빈 보급 수레가 검문을 기다리고 있습니다.",
                "수레 밑에 숨는다", "적재를 줄이고 검문을 통과합니다.", "보급 손실 위험·침투", 1,
                "수레를 빼돌린다", "보급품을 얻지만 즉시 수색이 시작됩니다.", "보급 증가·경계 크게 상승", 2),
            Card("infiltration_sleeping_hounds", OffenseDecisionStage.Infiltration, "잠든 사냥개",
                "문 앞의 사냥개들이 서로 기대 잠들어 있습니다.",
                "먹이로 유인한다", "식량을 멀리 던져 길을 비웁니다.", "식량 감소", 1,
                "바람을 거슬러 돈다", "냄새가 닿지 않도록 먼 길을 택합니다.", "시간 감소", 1),
            Card("infiltration_signal_bell", OffenseDecisionStage.Infiltration, "신호 종",
                "통로 한가운데 가는 실이 경보 종과 연결되어 있습니다.",
                "실을 끊는다", "민첩한 대원이 장력을 유지하며 장치를 해제합니다.", "실패 시 경계 상승", 1,
                "다른 통로를 찾는다", "안전하지만 더 깊은 곳에서 합류합니다.", "이동 지연", 1, rightMove: true),
            Card("infiltration_powder_store", OffenseDecisionStage.Infiltration, "화약 냄새",
                "벽 너머 창고에서 기름과 화약 냄새가 납니다.",
                "불씨를 심는다", "전투가 시작되면 보급을 끊을 준비를 합니다.", "적 보급 감소·발각 위험", 2,
                "표식만 남긴다", "귀환하거나 다음 작전에서 노릴 위치를 기록합니다.", "정보 증가", 0),
            Card("infiltration_locked_archive", OffenseDecisionStage.Infiltration, "잠긴 기록실",
                "목표로 가는 길 옆에 두꺼운 기록실 문이 있습니다.",
                "자물쇠를 연다", "시간을 써서 지도와 명부를 찾습니다.", "시간 감소·정보 크게 증가", 1,
                "목표에 집중한다", "곁길을 버리고 현재 임무를 우선합니다.", "위험 감소", 0),

            Card("camp_dry_cave", OffenseDecisionStage.Camp, "마른 동굴",
                "바람이 들지 않는 동굴에 오래된 재가 남아 있습니다.",
                "불을 피운다", "몸을 덥히고 식사를 준비합니다.", "연료 감소·피로 회복", 1,
                "불 없이 쉰다", "발각을 피하지만 회복량이 줄어듭니다.", "노출 감소·회복 감소", 0),
            Card("camp_night_watch", OffenseDecisionStage.Camp, "야간 경계",
                "멀리서 한 번씩 돌 부딪히는 소리가 들립니다.",
                "교대로 보초를 선다", "모두 조금 덜 자고 매복을 예방합니다.", "피로 소폭 증가·위험 감소", 1,
                "함정을 설치한다", "도구로 주변 접근로를 표시합니다.", "도구 감소·위험 크게 감소", 1),
            Card("camp_shared_ration", OffenseDecisionStage.Camp, "모자란 식사",
                "남은 식량으로는 모두가 배불리 먹기 어렵습니다.",
                "균등하게 나눈다", "전원이 허기를 조금만 달랩니다.", "식량 감소·회복 보통", 1,
                "부상자에게 몰아준다", "부상자의 회복을 우선하고 나머지는 참습니다.", "부상 회복·스트레스 증가", 1),
            Card("camp_weapon_care", OffenseDecisionStage.Camp, "닳은 장비",
                "습기와 먼지가 활시위와 갑옷 이음새에 쌓였습니다.",
                "현장 정비한다", "도구와 시간을 써서 고장을 예방합니다.", "도구 감소·장비 회복", 1,
                "귀환 뒤 수리한다", "지금은 쉬지만 다음 전투의 위험이 남습니다.", "장비 위험", 1),
            Card("camp_bad_dream", OffenseDecisionStage.Camp, "같은 악몽",
                "잠든 대원 둘이 같은 말을 중얼거리며 몸을 떱니다.",
                "깨워서 이야기한다", "잠을 포기하고 꿈의 공통점을 기록합니다.", "피로 증가·정보 증가", 1,
                "끝까지 재운다", "몸은 쉬지만 꿈의 잔향이 남습니다.", "스트레스 증가", 1),
            Card("camp_local_herbs", OffenseDecisionStage.Camp, "푸른 약초",
                "습지 가장자리에서 상처 열을 낮추는 약초가 자랍니다.",
                "약초를 달인다", "물과 시간을 써서 가벼운 상처를 돌봅니다.", "물 감소·부상 회복", 1,
                "표본을 챙긴다", "던전에서 분석할 수 있도록 뿌리째 가져갑니다.", "전리품 증가", 0),
            Card("camp_distant_horns", OffenseDecisionStage.Camp, "먼 뿔나팔",
                "서로 다른 방향에서 뿔나팔이 두 차례 울립니다.",
                "즉시 이동한다", "휴식을 포기하고 포위되기 전에 자리를 뜹니다.", "피로 증가·위험 감소", 1,
                "위치를 숨긴다", "흔적을 지우고 조용히 경과를 지켜봅니다.", "시간 감소·노출 감소", 1,
                leftMove: true),

            Card("loot_locked_chest", OffenseDecisionStage.Loot, "쇠로 잠근 상자",
                "목표 방 안쪽에 주인 없는 무거운 상자가 놓여 있습니다.",
                "자물쇠만 연다", "내용을 확인하고 가치 높은 물건만 챙깁니다.", "전리품 증가", 1,
                "상자째 운반한다", "더 많이 얻지만 귀환 속도가 느려집니다.", "전리품 크게 증가·적재 증가", 2),
            Card("loot_wounded_enemy", OffenseDecisionStage.Loot, "살아 있는 수비병",
                "무너진 벽 아래에서 수비병 하나가 의식을 되찾고 있습니다.",
                "포로로 데려간다", "치료품과 운반 여력을 써서 생포합니다.", "약품 감소·포로 획득", 1,
                "정보만 묻는다", "짧게 심문하고 추격 전에 자리를 뜹니다.", "정보 증가", 0),
            Card("loot_burning_store", OffenseDecisionStage.Loot, "불타는 창고",
                "창고 불길이 아직 닿지 않은 물자 더미로 번지고 있습니다.",
                "불을 끈다", "물과 시간을 써서 더 많은 물자를 구합니다.", "물 감소·전리품 크게 증가", 2,
                "가까운 것만 챙긴다", "안전한 물자만 들고 즉시 철수합니다.", "전리품 소폭 증가", 0),
            Card("loot_cursed_weapon", OffenseDecisionStage.Loot, "검은 무기",
                "피가 묻지 않았는데도 칼날에서 쇠 냄새가 납니다.",
                "봉인해 가져간다", "마나와 천으로 감싸 분석용 전리품으로 만듭니다.", "마나 감소·희귀 전리품", 2,
                "촉매만 떼어낸다", "위험한 본체를 버리고 쓸 수 있는 조각만 챙깁니다.", "촉매 획득·위험 감소", 1),
            Card("loot_prison_records", OffenseDecisionStage.Loot, "수송 명부",
                "포로와 호송 경로가 적힌 명부가 반쯤 찢겨 있습니다.",
                "모든 장을 맞춘다", "시간을 써서 다음 수송대 위치를 복원합니다.", "시간 감소·거점 발견", 1,
                "이름만 기록한다", "훗날 확인할 이름과 표식만 남깁니다.", "정보 증가", 0),
            Card("loot_enemy_banner", OffenseDecisionStage.Loot, "지휘 깃발",
                "쓰러진 지휘관 곁에 지역 문장이 새겨진 깃발이 남았습니다.",
                "전리품으로 건다", "던전의 사기를 높일 증표로 가져갑니다.", "기분 보상·적재 증가", 1,
                "천을 잘라 쓴다", "부상자와 장비를 묶는 재료로 사용합니다.", "부상 회복·전리품 감소", 1),
            Card("loot_hidden_cellar", OffenseDecisionStage.Loot, "바닥 아래 저장고",
                "빈 선반 아래에서 찬 공기가 올라오는 틈이 보입니다.",
                "지하를 확인한다", "추가 위험을 감수하고 숨은 저장고로 내려갑니다.", "전리품 크게 증가·전투 위험", 2,
                "입구만 표시한다", "다음 작전을 위해 위치만 남기고 귀환합니다.", "정보 증가", 0,
                leftCombat: true),

            Card("return_following_tracks", OffenseDecisionStage.Return, "뒤따르는 발자국",
                "뒤에서 같은 간격으로 멈췄다 이어지는 발자국이 들립니다.",
                "매복을 준비한다", "유리한 지형에서 추격자의 정체를 확인합니다.", "전투 위험", 2,
                "짐을 나눠 속도를 낸다", "전리품 일부를 포기하고 거리를 벌립니다.", "전리품 감소·위험 감소", 1,
                leftCombat: true),
            Card("return_flooded_road", OffenseDecisionStage.Return, "불어난 귀로",
                "올 때 건넜던 길이 빗물에 잠겨 흐름이 거세졌습니다.",
                "밧줄로 건넌다", "도구를 써서 전리품과 부상자를 먼저 보냅니다.", "도구 감소·안전 이동", 1,
                "능선을 우회한다", "한 칸 더 움직여 높은 길로 돌아갑니다.", "우회 이동", 1, rightMove: true),
            Card("return_broken_pack", OffenseDecisionStage.Return, "찢어진 짐끈",
                "전리품 가방의 끈이 끊어져 물건이 길에 흩어집니다.",
                "다시 포장한다", "시간을 써서 가능한 물건을 모두 회수합니다.", "시간 감소", 1,
                "무거운 것을 버린다", "가치가 낮고 무거운 물건부터 포기합니다.", "전리품 감소·적재 감소", 1),
            Card("return_victory_song", OffenseDecisionStage.Return, "승리의 노래",
                "누군가 긴장을 풀기 위해 낮게 노래를 시작합니다.",
                "함께 부른다", "경계는 조금 느슨해지지만 스트레스가 내려갑니다.", "스트레스 감소·노출 소폭 증가", 1,
                "조용히 행군한다", "위치를 숨기고 던전의 불빛만 바라봅니다.", "노출 감소", 0),
            Card("return_field_surgery", OffenseDecisionStage.Return, "벌어진 상처",
                "한 대원의 봉합이 풀려 피가 다시 배어 나옵니다.",
                "현장에서 처치한다", "약품과 시간을 써서 상처를 안정시킵니다.", "약품 감소·부상 회복", 1,
                "속도를 높인다", "악화를 감수하고 던전 의료실을 향합니다.", "비치명 부상 위험", 2, rightInjury: true),
            Card("return_last_toll", OffenseDecisionStage.Return, "귀로의 검문",
                "던전으로 이어지는 길목에 낯선 검문대가 세워졌습니다.",
                "전리품 일부를 내민다", "검문을 짧게 끝내고 무사히 통과합니다.", "전리품 감소", 1,
                "정체를 속인다", "획득한 문서와 표식으로 다른 부대인 척합니다.", "실패 시 노출 증가", 1),
            Card("return_home_lights", OffenseDecisionStage.Return, "멀리 보이는 불빛",
                "던전 외곽의 불빛이 나무 사이로 희미하게 보입니다.",
                "정찰을 먼저 보낸다", "입구 주변의 위험을 확인한 뒤 접근합니다.", "시간 감소·위험 감소", 0,
                "곧장 돌아간다", "지친 대원들과 전리품을 빠르게 귀환시킵니다.", "귀환 가속", 0, rightMove: true)
        };
    }

    private static CardSeed Card(
        string id,
        OffenseDecisionStage stage,
        string title,
        string situation,
        string left,
        string leftDescription,
        string leftDirection,
        int leftSeverity,
        string right,
        string rightDescription,
        string rightDirection,
        int rightSeverity,
        bool leftCombat = false,
        bool rightCombat = false,
        bool leftInjury = false,
        bool rightInjury = false,
        bool leftMove = false,
        bool rightMove = false,
        string leftTag = "",
        string leftAlt = "",
        string leftAltDescription = "")
    {
        return new CardSeed
        {
            Id = id,
            Stage = stage,
            Title = title,
            Situation = situation,
            Left = left,
            LeftDescription = leftDescription,
            LeftDirection = leftDirection,
            LeftSeverity = leftSeverity,
            LeftCombat = leftCombat,
            LeftInjury = leftInjury,
            LeftMove = leftMove,
            LeftRequiredTag = leftTag,
            LeftTransformedLabel = leftAlt,
            LeftTransformedDescription = leftAltDescription,
            Right = right,
            RightDescription = rightDescription,
            RightDirection = rightDirection,
            RightSeverity = rightSeverity,
            RightCombat = rightCombat,
            RightInjury = rightInjury,
            RightMove = rightMove
        };
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
        {
            MonoScript script = MonoScript.FromScriptableObject(asset);
            if (script != null && script.GetClass() == typeof(T))
            {
                return asset;
            }

            AssetDatabase.DeleteAsset(path);
        }
        else if (AssetDatabase.LoadMainAssetAtPath(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
        }

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static string SafeAssetName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string((value ?? string.Empty)
            .Select(character =>
                invalid.Contains(character) || character == ':'
                    ? '_'
                    : character)
            .ToArray());
    }

    private static void EnsureFolder(string path)
    {
        string normalized = path.Replace('\\', '/');
        string[] parts = normalized.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[index]);
            }

            current = next;
        }
    }

    private readonly struct SiteSeed
    {
        public SiteSeed(
            string id,
            string name,
            string description,
            StrategicPressureAxis axis,
            float pressure,
            int minimumStrength,
            int maximumStrength,
            bool moves,
            params OffenseSiteRewardDefinition[] rewards)
            : this(
                id,
                name,
                description,
                axis,
                pressure,
                minimumStrength,
                maximumStrength,
                moves,
                true,
                rewards)
        {
        }

        public SiteSeed(
            string id,
            string name,
            string description,
            StrategicPressureAxis axis,
            float pressure,
            int minimumStrength,
            int maximumStrength,
            bool moves,
            bool dynamicSpawnEligible,
            params OffenseSiteRewardDefinition[] rewards)
        {
            Id = id;
            Name = name;
            Description = description;
            Axis = axis;
            Pressure = pressure;
            MinimumStrength = minimumStrength;
            MaximumStrength = maximumStrength;
            Moves = moves;
            DynamicSpawnEligible = dynamicSpawnEligible;
            Rewards = rewards ?? Array.Empty<OffenseSiteRewardDefinition>();
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public StrategicPressureAxis Axis { get; }
        public float Pressure { get; }
        public int MinimumStrength { get; }
        public int MaximumStrength { get; }
        public bool Moves { get; }
        public bool DynamicSpawnEligible { get; }
        public IReadOnlyList<OffenseSiteRewardDefinition> Rewards { get; }
    }

    private readonly struct UrgentSeed
    {
        public UrgentSeed(
            string id,
            string name,
            string description,
            OffenseThreatModifierKind kind,
            string workTypeId,
            string itemId)
        {
            Id = id;
            Name = name;
            Description = description;
            Kind = kind;
            WorkTypeId = workTypeId;
            ItemId = itemId;
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public OffenseThreatModifierKind Kind { get; }
        public string WorkTypeId { get; }
        public string ItemId { get; }
    }

    private sealed class CardSeed
    {
        public string Id;
        public OffenseDecisionStage Stage;
        public string Title;
        public string Situation;
        public string Left;
        public string LeftDescription;
        public string LeftDirection;
        public int LeftSeverity;
        public bool LeftCombat;
        public bool LeftInjury;
        public bool LeftMove;
        public string LeftRequiredTag;
        public string LeftTransformedLabel;
        public string LeftTransformedDescription;
        public string Right;
        public string RightDescription;
        public string RightDirection;
        public int RightSeverity;
        public bool RightCombat;
        public bool RightInjury;
        public bool RightMove;
        public string RightRequiredTag;
        public string RightTransformedLabel;
        public string RightTransformedDescription;
    }
}
#endif
