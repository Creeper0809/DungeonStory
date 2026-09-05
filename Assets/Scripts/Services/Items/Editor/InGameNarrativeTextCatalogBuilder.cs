#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class InGameNarrativeTextCatalogBuilder
{
    private const string AssetPath =
        "Assets/Resources/SO/InGameNarrativeTextCatalog.asset";

    private static readonly string[] ForbiddenPlayerFacingPhrases =
    {
        "을(를)",
        "이(가)",
        "은(는)",
        "과(와)",
        "것이 아니다",
        "하려는 게 아니다",
        "주장하려는",
        "단순히",
        "그저",
        "니다"
    };

    private static readonly IReadOnlyDictionary<string, string> CardRecords =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["travel_toll_gate"] =
                "임시 검문소의 게시판에는 통행세와 함께 최근 실종자 명단이 붙어 있다. 징수대는 명단 옆에 '던전에게 먹힘'이라는 붉은 도장을 찍었다.",
            ["travel_false_milestone"] =
                "새 이정표 아래에서 지워진 옛 표석이 드러난다. 같은 길과 마을에 서로 다른 이름이 겹쳐 새겨져 있다.",
            ["recon_buried_map"] =
                "묻힌 지도통 안에는 현재 지도에서 사라진 공동체와 통행로가 남아 있다. 같은 장소가 인간식 이름과 여러 종족의 이름으로 함께 기록되어 있다.",
            ["recon_old_runes"] =
                "낡은 경계문에는 한 사람의 인간 이름과 종족 이름이 나란히 새겨져 있다. 두 이름 사이의 문장은 정으로 파내 읽을 수 없다.",
            ["negotiation_deserter"] =
                "인간군 탈영병이 보호를 대가로 수송 명부의 규칙을 털어놓는다. '던전에게 먹힘'으로 처리된 포로들은 죽음이 확인되기 전에 귀환소로 보내졌다.",
            ["negotiation_choir_envoy"] =
                "폭풍둥지 사절은 인간군 포로들의 이름을 넣은 오래된 노래를 들려준다. 식인 피해자로 기록된 병사의 이름을 한 하피가 자신의 옛 이름이라고 답한다.",
            ["infiltration_locked_archive"] =
                "잠긴 기록실에는 첫 협약 원본이 보관되어 있다. 여러 종족이 전쟁을 끝내려고 인간의 몸을 선택했으며, 원래 종족의 몸으로 돌아갈 권리도 함께 기록했다.",
            ["loot_prison_records"] =
                "포로 이송 기록은 다른 종족의 몸으로 돌아간 사람을 사망자로 고쳐 적는다. 같은 줄에 수호원과 등기원의 도장이 나란히 찍혀 있다.",
            ["loot_transport_manifest"] =
                "수송 명부에는 귀환자를 사망자로 고치고 그들이 간 도시를 식인 거점으로 지정하라는 지시가 붙어 있다. 수호, 등기, 교육, 통상, 개척 기관의 인장이 차례로 찍혀 있다."
        };

    private static readonly IReadOnlyDictionary<string, string> ItemRecords =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["record:arcane-index"] =
                "오래된 비전 색인은 같은 주문을 종족별 이름과 인간 학교의 표준명으로 나란히 적었다. 뒤쪽 판본에서는 종족별 이름과 처음 기록한 술사의 이름이 함께 지워져 있다.",
            ["record:breeding-ledger"] =
                "번식 장부에는 부모의 씨족과 신체 특징, 아이의 공식 이름을 따로 기록한 칸이 남아 있다. 후기 장부에서는 앞의 두 칸이 뜯기고 칼리오르의 등기 도장만 찍혀 있다.",
            ["record:career-ledger"] =
                "경력 장부는 인간식 직책 옆에 씨족 도장, 조상균 기억, 노래 계보를 함께 기록한다. 후기 장부는 인간 기관의 경력만 인정하며 다른 표식을 무효로 처리한다."
        };

    [InitializeOnLoadMethod]
    private static void EnsureCatalogueExists()
    {
        if (AssetDatabase.LoadAssetAtPath<InGameNarrativeTextCatalogSO>(
                AssetPath) == null)
        {
            EditorApplication.delayCall += Rebuild;
        }
    }

    [MenuItem("Tools/DungeonStory/Content/Rebuild In-Game Narrative Text Catalog")]
    public static void Rebuild()
    {
        ItemDefinitionSO[] items = FindAssets<ItemDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, ItemDefinitionSO> itemById = items.ToDictionary(
            value => value.ItemId,
            StringComparer.Ordinal);
        ProductionRecipeSO[] recipes = FindAssets<ProductionRecipeSO>()
            .Where(value => value != null)
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        BuildingSO[] facilities = FindAssets<BuildingSO>()
            .Where(value => value != null && value.id > 0)
            .OrderBy(value => value.ContentDefinitionId, StringComparer.Ordinal)
            .ToArray();
        OffenseDecisionCardSO[] cards = FindAssets<OffenseDecisionCardSO>()
            .Where(value => value != null)
            .OrderBy(value => value.cardId, StringComparer.Ordinal)
            .ToArray();
        OffenseSiteArchetypeSO[] sites = FindAssets<OffenseSiteArchetypeSO>()
            .Where(value => value != null)
            .OrderBy(value => value.siteTypeId, StringComparer.Ordinal)
            .ToArray();
        OffenseUrgentSiteDefinitionSO[] urgentSites =
            FindAssets<OffenseUrgentSiteDefinitionSO>()
                .Where(value => value != null)
                .OrderBy(value => value.urgentSiteId, StringComparer.Ordinal)
                .ToArray();

        List<InGameNarrativeTextEntry> entries = new();
        entries.AddRange(items.Select(item => Create(
            InGameNarrativeTextKind.Item,
            item.ItemId,
            BuildItemDescription(item),
            ResolveWorldBranchTag(item.ItemId))));
        entries.AddRange(recipes.Select(recipe => Create(
            InGameNarrativeTextKind.ProductionRecipe,
            recipe.RecipeId,
            BuildRecipeDescription(recipe, itemById),
            ResolveWorldBranchTag(recipe.RecipeId))));
        entries.AddRange(facilities.Select(facility => Create(
            InGameNarrativeTextKind.Facility,
            InGameNarrativeTextCatalogSO.ComposeFacilityStableId(
                facility.ContentDefinitionId,
                facility.id),
            BuildFacilityDescription(facility),
            ResolveWorldBranchTag(facility.ContentDefinitionId))));

        foreach (OffenseDecisionCardSO card in cards)
        {
            entries.Add(Create(
                InGameNarrativeTextKind.ExpeditionCard,
                card.cardId,
                BuildCardDescription(card),
                ResolveWorldBranchTag(card.cardId)));
            foreach (OffenseDecisionChoiceDefinition choice in card.choices
                         ?? new List<OffenseDecisionChoiceDefinition>())
            {
                entries.Add(Create(
                    InGameNarrativeTextKind.ExpeditionChoice,
                    InGameNarrativeTextCatalogSO.ComposeExpeditionChoiceStableId(
                        card.cardId,
                        choice.choiceId),
                    BuildChoiceDescription(choice),
                    ResolveWorldBranchTag(card.cardId)));
            }
        }

        entries.AddRange(sites.Select(site => Create(
            InGameNarrativeTextKind.ExpeditionSite,
            site.siteTypeId,
            BuildSiteDescription(site.description),
            ResolveWorldBranchTag(site.siteTypeId))));
        entries.AddRange(urgentSites.Select(site => Create(
            InGameNarrativeTextKind.ExpeditionSite,
            site.urgentSiteId,
            BuildSiteDescription(site.description),
            ResolveWorldBranchTag(site.urgentSiteId))));

        ValidateNaturalKorean(entries);

        InGameNarrativeTextCatalogSO catalog =
            AssetDatabase.LoadAssetAtPath<InGameNarrativeTextCatalogSO>(AssetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<InGameNarrativeTextCatalogSO>();
            AssetDatabase.CreateAsset(catalog, AssetPath);
        }

        catalog.SetEntries(entries);
        IReadOnlyList<string> errors = catalog.ValidateCatalog();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "In-game narrative catalogue rebuild failed:\n"
                + string.Join("\n", errors));
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);

        int choiceCount = cards.Sum(card => card.choices?.Count ?? 0);
        int targetCount = items.Length + recipes.Length + facilities.Length
            + cards.Length + choiceCount + sites.Length + urgentSites.Length;
        int consumerKinds = CountConnectedConsumerKinds();
        int orphanCount = Math.Max(0, entries.Count - targetCount);
        Debug.Log(
            "IN_GAME_NARRATIVE_TEXT_AUDIT "
            + $"target={targetCount}; descriptions={entries.Count}; "
            + $"uiConsumerKinds={consumerKinds}/6; orphans={orphanCount}; "
            + $"items={items.Length}; recipes={recipes.Length}; "
            + $"facilities={facilities.Length}; cards={cards.Length}; "
            + $"choices={choiceCount}; sites={sites.Length + urgentSites.Length}");

        if (entries.Count != targetCount || orphanCount != 0)
        {
            throw new InvalidOperationException(
                "In-game narrative target coverage is incomplete.");
        }
    }

    private static InGameNarrativeTextEntry Create(
        InGameNarrativeTextKind kind,
        string stableId,
        string description,
        string branchTag)
    {
        InGameNarrativeTextEntry entry = new();
        entry.Configure(kind, stableId, description, branchTag);
        return entry;
    }

    private static string BuildItemDescription(ItemDefinitionSO item)
    {
        if (ItemRecords.TryGetValue(item.ItemId, out string itemRecord))
        {
            return itemRecord;
        }

        string subject = AttachTopicParticle(item.DisplayName);
        string trace = BuildItemTrace(item);

        FoodItemFeature food = item.Features?.OfType<FoodItemFeature>()
            .FirstOrDefault();
        if (food != null)
        {
            string mood = Math.Abs(food.mood) < 0.001f
                ? "기분 변화는 없다"
                : food.mood > 0f
                    ? $"기분이 {food.mood:0.##} 오른다"
                    : $"기분이 {Math.Abs(food.mood):0.##} 내려간다";
            return Combine(
                $"{subject} 식사로 제공하면 영양을 {food.nutrition:0.##}만큼 채우고 {mood}.",
                trace);
        }

        MedicineItemFeature medicine = item.Features?
            .OfType<MedicineItemFeature>()
            .FirstOrDefault();
        if (medicine != null)
        {
            return Combine(
                $"{subject} 치료 효율 {medicine.treatmentPotency:0.##}, 감염 감소 {medicine.infectionReduction:0.##}, 해독 {medicine.detoxReduction:0.##}, 통증 완화 {medicine.painReduction:0.##}인 의료 물자다.",
                trace);
        }

        string purpose = ResolveItemPurpose(item);
        return Combine($"{subject} {purpose}", trace);
    }

    private static string ResolveItemPurpose(ItemDefinitionSO item)
    {
        string id = item.ItemId;
        if (item.Features?.OfType<AmmunitionItemFeature>().Any() == true
            || id.StartsWith("ammo:", StringComparison.Ordinal))
            return "원거리 무기와 함정에 장전하는 탄약이다.";
        if (item.Features?.OfType<InstallationItemFeature>().Any() == true
            || id.StartsWith("facility-kit:", StringComparison.Ordinal))
            return "완성된 시설을 원하는 자리에 설치하는 건설 키트다.";
        if (item.Features?.OfType<BlueprintItemFeature>().Any() == true
            || id.StartsWith("research-blueprint:", StringComparison.Ordinal))
            return "연구와 시설 해금에 사용하는 청사진이다.";
        if (item.Features?.OfType<EvolutionCatalystItemFeature>().Any() == true
            || id.StartsWith("evolution:", StringComparison.Ordinal))
            return "시설 개조와 장비 조율에 사용하는 진화 촉매다.";
        if (id.StartsWith("apparel:", StringComparison.Ordinal))
            return "주민이 일상, 작업 또는 의례에 맞춰 착용하는 의복이다.";
        if (item.Features?.OfType<EquipmentItemFeature>().Any() == true
            || id.StartsWith("equipment-item:", StringComparison.Ordinal)
            || id.StartsWith("equipment:", StringComparison.Ordinal))
        {
            if (id.Contains(":weapon:", StringComparison.Ordinal))
                return "전투에서 적을 공격하는 무기다.";
            if (id.Contains(":shield:", StringComparison.Ordinal))
                return "공격을 막고 전열을 지키는 방패다.";
            if (id.Contains(":armor:", StringComparison.Ordinal))
                return "전투에서 신체를 보호하는 방어구다.";
            return "주민이 착용해 작업이나 전투를 준비하는 장비다.";
        }
        if (id.StartsWith("relic:", StringComparison.Ordinal))
            return "원정과 사건에서 얻어 연구, 거래 또는 협약의 증거로 쓰는 유물이다.";
        if (id.StartsWith("record:", StringComparison.Ordinal))
            return "연구와 사건의 사실을 확인하고 후속 결정을 여는 기록물이다.";
        if (id.StartsWith("tool:", StringComparison.Ordinal))
            return "주민이 작업할 때 휴대해 속도와 안전을 높이는 도구다.";
        if (id.StartsWith("surgery:", StringComparison.Ordinal)
            || id.StartsWith("medical:", StringComparison.Ordinal)
            || id.StartsWith("sample:", StringComparison.Ordinal))
            return "진단, 치료, 수술 또는 질병 연구에 준비하는 의료 물자다.";

        return item.StockCategory switch
        {
            StockCategory.Food => "식사와 식량 생산에 쓰는 물자다.",
            StockCategory.Weapon => "주민이 전투에 휴대하거나 장비 제작에 쓰는 물품이다.",
            StockCategory.Mana => "마력 설비와 연구에 공급하는 물자다.",
            StockCategory.Water => "음용, 조리, 위생과 생산에 공급하는 물이다.",
            StockCategory.Medicine => "진단, 치료와 수술에 준비하는 의료 물자다.",
            StockCategory.Fuel => "난방과 가동 설비에 공급하는 연료다.",
            StockCategory.Ammunition => "원거리 무기와 함정에 장전하는 탄약이다.",
            StockCategory.Biological => "농업, 축산과 생물 처리에 쓰는 물자다.",
            StockCategory.Knowledge => "연구와 기록 검증에 쓰는 지식 자료다.",
            StockCategory.Blueprint => "연구를 열거나 시설 설치에 쓰는 설계 자료다.",
            _ => "건설, 제작, 정비와 거래에 쓰는 일반 물자다."
        };
    }

    private static string BuildRecipeDescription(
        ProductionRecipeSO recipe,
        IReadOnlyDictionary<string, ItemDefinitionSO> itemById)
    {
        string inputs = FormatAmounts(
            recipe.Inputs,
            input => input.ItemId,
            input => input.Amount,
            itemById);
        string outputs = FormatAmounts(
            recipe.Outputs,
            output => output.ItemId,
            output => output.Amount,
            itemById);
        string process = recipe.ProcessKind == ProductionProcessKind.PassiveBatch
            ? $"준비 작업량은 {recipe.PreparationWork:0.##}, 가공 시간은 {recipe.ProcessingGameHours:0.##}시간, 마감 작업량은 {recipe.FinishingWork:0.##}이다."
            : $"필요 작업량은 {recipe.RequiredWork:0.##}이다.";
        return $"필요한 재료는 {inputs}이며, 한 차례 생산하면 {outputs}가 완성된다. {process}";
    }

    private static string BuildFacilityDescription(BuildingSO facility)
    {
        string name = string.IsNullOrWhiteSpace(facility.objectName)
            ? $"시설 {facility.id}"
            : facility.objectName.Trim();
        string category = facility.IsDoor
            ? "출입구를 열고 닫아 방의 통행을 조절한다"
            : facility.category switch
        {
            BuildingCategory.Wall => "공간을 나누고 이동을 통제한다",
            BuildingCategory.Movement => "주민과 물자의 이동 경로를 잇는다",
            BuildingCategory.Production => "원료를 가공하고 생산을 이어 간다",
            BuildingCategory.Crafting => "도구와 장비를 제작하고 정비한다",
            BuildingCategory.Resource => "정착지에 필요한 자원을 모으고 보관한다",
            BuildingCategory.Shop => "손님과 주민의 거래와 서비스를 맡는다",
            BuildingCategory.Special => "특수한 작업과 정착지 운영을 맡는다",
            _ => "정착지의 공간과 운영을 지원한다"
        };
        return $"{AttachTopicParticle(name)} {category}. 배치 위치와 연결 통로가 작업자의 이동과 주변 시설 이용에 영향을 준다.";
    }

    private static string BuildCardDescription(OffenseDecisionCardSO card)
    {
        if (TryResolveCardRecord(card.cardId, out string record))
        {
            return record;
        }

        string stage = card.stage switch
        {
            OffenseDecisionStage.Travel => "이 선택은 원정대의 이동과 남은 보급에 영향을 준다.",
            OffenseDecisionStage.Reconnaissance => "관찰한 흔적은 거점의 경계와 다음 진입로를 판단하는 단서가 된다.",
            OffenseDecisionStage.Negotiation => "상대의 요구와 약속은 관계와 통행 조건으로 남는다.",
            OffenseDecisionStage.Infiltration => "진입 방법은 발각 위험과 전투 시작 조건을 바꾼다.",
            OffenseDecisionStage.Camp => "야영 결정은 대원의 회복과 다음 이동 준비에 영향을 준다.",
            OffenseDecisionStage.Loot => "회수 대상을 고르면 운반 부담과 귀환 위험이 함께 바뀐다.",
            OffenseDecisionStage.Return => "귀환로의 선택은 부상자와 전리품을 정착지까지 가져갈 수 있는지 결정한다.",
            _ => "선택의 결과는 이번 원정에 이어진다."
        };
        return Combine(NaturalizeLegacyDescription(card.situation), stage);
    }

    private static string BuildChoiceDescription(
        OffenseDecisionChoiceDefinition choice)
    {
        string description = NaturalizeLegacyDescription(choice.description);
        string transformed = NaturalizeLegacyDescription(
            choice.transformedDescription);
        string result = string.IsNullOrWhiteSpace(transformed)
            || string.Equals(description, transformed, StringComparison.Ordinal)
                ? description
                : Combine(description, $"조건을 갖추면 {transformed}");
        return result;
    }

    private static string BuildSiteDescription(string source)
    {
        return Combine(
            "원정대가 접근해 정찰, 교섭, 침투 또는 전투로 대응할 수 있는 거점이다.",
            NaturalizeLegacyDescription(source));
    }

    private static string FormatAmounts<T>(
        IEnumerable<T> values,
        Func<T, string> getId,
        Func<T, int> getAmount,
        IReadOnlyDictionary<string, ItemDefinitionSO> itemById)
    {
        string[] labels = (values ?? Array.Empty<T>())
            .Where(value => value != null)
            .Select(value =>
            {
                string id = getId(value);
                string name = itemById.TryGetValue(id, out ItemDefinitionSO item)
                    ? item.DisplayName
                    : id;
                return $"{name} {getAmount(value)}개";
            })
            .ToArray();
        return labels.Length > 0 ? string.Join(", ", labels) : "별도 재료 없음";
    }

    private static string BuildItemTrace(ItemDefinitionSO item)
    {
        string trace = FirstSentence(
            NaturalizeLegacyDescription(item.Description));
        string sentence = trace.Trim().TrimEnd('.', '!', '?');
        if (string.IsNullOrWhiteSpace(sentence)
            || string.Equals(sentence, item.DisplayName, StringComparison.Ordinal)
            || sentence.Contains("물리 인스턴스", StringComparison.Ordinal)
            || string.Equals(
                sentence,
                item.DisplayName + " 전투 장비",
                StringComparison.Ordinal))
        {
            return string.Empty;
        }

        if (string.Equals(
                sentence,
                "분기형 생산망의 " + item.DisplayName,
                StringComparison.Ordinal))
        {
            return "여러 단계의 생산을 거쳐 만든다.";
        }

        sentence = sentence
            .Replace("작업실의 다음 공정으로 실제 운반되는 물리 중간재", "다음 공정까지 직접 운반하는 중간 재료")
            .Replace("등급·상태 밴드로만 병합되는 V22 원섬유", "같은 등급과 상태끼리 묶어 보관하는 원섬유")
            .Replace("물리 원단", "원단")
            .Replace("물리 중간재", "중간 재료");
        if (sentence.EndsWith("함", StringComparison.Ordinal))
        {
            return sentence.Substring(0, sentence.Length - 1) + "한다.";
        }
        if (sentence.EndsWith("다", StringComparison.Ordinal))
        {
            return sentence + ".";
        }
        return sentence + "이다.";
    }

    private static string Combine(string first, string second)
    {
        string left = NormalizeSentence(first);
        string right = NormalizeSentence(second);
        if (string.IsNullOrWhiteSpace(left)) return right;
        if (string.IsNullOrWhiteSpace(right)
            || string.Equals(left, right, StringComparison.Ordinal)) return left;
        return $"{left} {right}";
    }

    private static string NormalizeSentence(string value)
    {
        string text = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        char last = text[text.Length - 1];
        return last is '.' or '!' or '?' ? text : text + ".";
    }

    private static string NaturalizeLegacyDescription(string value)
    {
        string text = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        text = ResolveParentheticalParticle(text, "을(를)", "을", "를");
        text = ResolveParentheticalParticle(text, "이(가)", "이", "가");
        text = ResolveParentheticalParticle(text, "은(는)", "은", "는");
        text = ResolveParentheticalParticle(text, "과(와)", "과", "와");
        text = text
            .Replace("구체 재료를 사용해", "정해진 재료로")
            .Replace("생산 주문", "제작 작업")
            .Replace("어렵습니다", "어렵다")
            .Replace("있습니다", "있다")
            .Replace("없습니다", "없다")
            .Replace("입니다", "이다")
            .Replace("합니다", "한다")
            .Replace("됩니다", "된다")
            .Replace("깁니다", "긴다")
            .Replace("봅니다", "본다")
            .Replace("옵니다", "온다")
            .Replace("줍니다", "준다");
        text = Regex.Replace(
            text,
            "([가-힣])습니다",
            match => HasDoubleFinalConsonant(match.Groups[1].Value[0])
                ? match.Groups[1].Value + "다"
                : match.Groups[1].Value + "는다");
        text = Regex.Replace(
            text,
            "([가-힣])니다",
            match => ReplaceFormalBieupEnding(match.Groups[1].Value[0]));
        return NormalizeSentence(text);
    }

    private static string ResolveParentheticalParticle(
        string source,
        string marker,
        string withFinalConsonant,
        string withoutFinalConsonant)
    {
        string result = source;
        int searchFrom = 0;
        while (searchFrom < result.Length)
        {
            int index = result.IndexOf(marker, searchFrom, StringComparison.Ordinal);
            if (index < 0) break;
            int previous = index - 1;
            while (previous >= 0 && char.IsWhiteSpace(result[previous])) previous--;
            string particle = previous >= 0 && HasFinalConsonant(result[previous])
                ? withFinalConsonant
                : withoutFinalConsonant;
            result = result.Remove(index, marker.Length).Insert(index, particle);
            searchFrom = index + particle.Length;
        }
        return result;
    }

    private static string AttachTopicParticle(string value)
    {
        string text = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return "이 물품은";
        return text + (HasFinalConsonant(text[text.Length - 1]) ? "은" : "는");
    }

    private static bool HasFinalConsonant(char value)
    {
        return value is >= '\uAC00' and <= '\uD7A3'
            && (value - '\uAC00') % 28 != 0;
    }

    private static bool HasDoubleFinalConsonant(char value)
    {
        return value is >= '\uAC00' and <= '\uD7A3'
            && (value - '\uAC00') % 28 == 20;
    }

    private static string ReplaceFormalBieupEnding(char value)
    {
        if (value is < '\uAC00' or > '\uD7A3') return value + "다";
        int finalConsonant = (value - '\uAC00') % 28;
        if (finalConsonant != 17) return value + "다";
        char plainForm = (char)(value - 13);
        return plainForm + "다";
    }

    private static string FirstSentence(string value)
    {
        string text = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        int end = text.IndexOfAny(new[] { '.', '!', '?' });
        return end >= 0 ? text.Substring(0, end + 1).Trim() : text;
    }

    private static void ValidateNaturalKorean(
        IEnumerable<InGameNarrativeTextEntry> entries)
    {
        InGameNarrativeTextEntry[] source = entries.ToArray();
        List<string> violations = source
            .SelectMany(entry => ForbiddenPlayerFacingPhrases
                .Where(phrase => entry.InGameDescription.Contains(
                    phrase,
                    StringComparison.Ordinal))
                .Select(phrase =>
                    $"{entry.Kind}:{entry.StableId} contains '{phrase}'"))
            .ToList();
        violations.AddRange(source
            .Where(entry => Regex.IsMatch(
                entry.InGameDescription,
                @"\d+(가|를)(?=\s|[,.])"))
            .Select(entry =>
                $"{entry.Kind}:{entry.StableId} contains a numeric particle error"));
        violations.AddRange(source
            .Where(entry => HasSentenceFragment(entry.InGameDescription))
            .Select(entry =>
                $"{entry.Kind}:{entry.StableId} contains a sentence fragment"));
        violations = violations.Take(20).ToList();
        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                "Player-facing narrative contains defensive or translation-style prose:\n"
                + string.Join("\n", violations));
        }
    }

    private static bool HasSentenceFragment(string value)
    {
        string[] sentences = Regex.Split(
                value?.Trim() ?? string.Empty,
                @"(?<!\d)[.!?]+|[.!?]+(?!\d)")
            .Select(sentence => sentence.Trim())
            .Where(sentence => sentence.Length > 0)
            .ToArray();
        return sentences.Any(sentence => sentence[sentence.Length - 1] != '다');
    }

    private static bool TryResolveCardRecord(string cardId, out string record)
    {
        string normalized = cardId?.Trim() ?? string.Empty;
        foreach (KeyValuePair<string, string> pair in CardRecords)
        {
            if (normalized.EndsWith(pair.Key, StringComparison.Ordinal))
            {
                record = pair.Value;
                return true;
            }
        }
        record = string.Empty;
        return false;
    }

    private static string ResolveWorldBranchTag(string stableId)
    {
        string id = stableId?.ToLowerInvariant() ?? string.Empty;
        if (id.Contains("archive") || id.Contains("record")
            || id.Contains("manifest") || id.Contains("old_rune")) return "first-accord-record";
        if (id.Contains("fung") || id.Contains("mycel")) return "witness:mycelial-garden";
        if (id.Contains("kobold") || id.Contains("cog")) return "witness:deep-cog-den";
        if (id.Contains("harpy") || id.Contains("choir")) return "witness:storm-nest";
        if (id.Contains("golem") || id.Contains("stonevein")) return "witness:stonevein-foundry";
        if (id.Contains("demon") || id.Contains("contract")) return "witness:ashen-contract-court";
        if (id.Contains("beast") || id.Contains("refugee")) return "witness:redclaw-trading-post";
        return string.Empty;
    }

    private static int CountConnectedConsumerKinds()
    {
        (string path, string token)[] checks =
        {
            ("Assets/Scripts/Services/Items/ItemPileInfoPanel.cs", "InGameNarrativeTextKind.Item"),
            ("Assets/Scripts/Views/Buildings/UI/ProductionBuildingPanelPresenter.cs", "InGameNarrativeTextKind.ProductionRecipe"),
            ("Assets/Scripts/Services/Buildings/BuildingSummaryFormatter.cs", "InGameNarrativeTextKind.Facility"),
            ("Assets/Scripts/Services/Offense/Strategic/OffenseWorldMapPanelStrategicEncounter.cs", "InGameNarrativeTextKind.ExpeditionCard"),
            ("Assets/Scripts/Services/Offense/Strategic/OffenseWorldMapPanelStrategicEncounter.cs", "InGameNarrativeTextKind.ExpeditionChoice"),
            ("Assets/Scripts/Services/Offense/Strategic/OffenseWorldMapPanelStrategicDetails.cs", "InGameNarrativeTextKind.ExpeditionSite")
        };
        return checks.Count(check => File.Exists(check.path)
            && File.ReadAllText(check.path).Contains(
                check.token,
                StringComparison.Ordinal));
    }

    private static T[] FindAssets<T>() where T : UnityEngine.Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[]
            {
                "Assets/Resources/SO"
            })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(value => value != null)
            .Distinct()
            .ToArray();
    }
}
#endif
