#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class Wim039ExperienceRiskAuthoringDebugScenarios
{
    private const string CatalogPath =
        "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";

    private static readonly Expectation[] Expected =
    {
        new("guest-request:coronation-feast", Source.Guest, ExperienceEventRiskTier.Recoverable, "실패 결과가 세력 원한 5를 추가한다."),
        new("guest-request:allergen-banquet", Source.Guest, ExperienceEventRiskTier.Recoverable, "실패 결과가 세력 원한 5를 추가한다."),
        new("guest-request:emergency-surgery", Source.Guest, ExperienceEventRiskTier.Recoverable, "실패 결과가 세력 원한 5를 추가한다."),
        new("guest-request:plague-screening", Source.Guest, ExperienceEventRiskTier.Recoverable, "실패 결과가 세력 원한 5를 추가한다."),
        new("guest-request:precision-barter", Source.Guest, ExperienceEventRiskTier.Recoverable, "실패 결과가 세력 원한 5를 추가한다."),
        new("guest-request:winter-fuel-auction", Source.Guest, ExperienceEventRiskTier.Recoverable, "실패 결과가 세력 원한 5를 추가한다."),
        new("guest-request:memorial-performance", Source.Guest, ExperienceEventRiskTier.Recoverable, "실패 결과가 세력 원한 5를 추가한다."),
        new("guest-request:flood-refuge", Source.Guest, ExperienceEventRiskTier.Recoverable, "실패 결과가 세력 원한 5를 추가한다."),
        new("guest-request:persecuted-family", Source.Guest, ExperienceEventRiskTier.Recoverable, "실패 결과가 세력 원한 5를 추가한다."),
        new("guest-request:sealed-archive", Source.Guest, ExperienceEventRiskTier.Recoverable, "실패 결과가 세력 원한 5를 추가한다."),
        new("guest-request:disease-sample", Source.Guest, ExperienceEventRiskTier.Recoverable, "실패 결과가 세력 원한 5를 추가한다."),
        new("guest-request:militia-arms", Source.Guest, ExperienceEventRiskTier.Recoverable, "실패 결과가 세력 원한 5를 추가한다."),
        new("guest-request:bodyguard-kit", Source.Guest, ExperienceEventRiskTier.Recoverable, "실패 결과가 세력 원한 5를 추가한다."),
        new("service-incident:brawl", Source.Incident, ExperienceEventRiskTier.Serious, "작성된 대응에 비치명 체력 피해 -3이 있다."),
        new("service-incident:theft", Source.Incident, ExperienceEventRiskTier.Recoverable, "작성된 대응에 작업 지연 1일 또는 돈 -100이 있다."),
        new("service-incident:contamination", Source.Incident, ExperienceEventRiskTier.Serious, "작성된 대응에 활성 압력 식별자를 추가하는 Threat 6이 있다."),
        new("service-incident:culturalinsult", Source.Incident, ExperienceEventRiskTier.Recoverable, "작성된 대응에 세력 원한 8이 있다."),
        new("service-incident:forbiddenmeal", Source.Incident, ExperienceEventRiskTier.Recoverable, "작성된 대응에 돈 -60 또는 세력 원한 5가 있다."),
        new("service-incident:medicalcollapse", Source.Incident, ExperienceEventRiskTier.Recoverable, "작성된 대응에 작업 지연 1일 또는 세력 원한 10이 있다."),
        new("service-incident:envoyconflict", Source.Incident, ExperienceEventRiskTier.Recoverable, "작성된 대응에 작업 지연 1일 또는 세력 원한 8이 있다."),
        new("service-incident:sabotage", Source.Incident, ExperienceEventRiskTier.Serious, "작성된 대응에 활성 압력 식별자를 추가하는 Threat 3이 있다."),
        new("life-event:first-forbidden-door", Source.LifeEvent, ExperienceEventRiskTier.Recoverable, "작성된 선택에 기분 -2가 있다."),
        new("life-event:foundling-question", Source.LifeEvent, ExperienceEventRiskTier.Serious, "작성된 선택의 트라우마 +3이 지속된다."),
        new("life-event:dangerous-friendship", Source.LifeEvent, ExperienceEventRiskTier.Recoverable, "작성된 선택에 기분 -3이 있다."),
        new("life-event:childhood-bully", Source.LifeEvent, ExperienceEventRiskTier.Recoverable, "작성된 선택에 작업 지연 1일이 있다."),
        new("life-event:apprentice-mistake", Source.LifeEvent, ExperienceEventRiskTier.Recoverable, "작성된 선택에 기분 -4가 있다."),
        new("life-event:stolen-design", Source.LifeEvent, ExperienceEventRiskTier.None, "작성된 선택에 불리한 게임플레이 효과가 없고 관계 증가 또는 WorldFlag만 있다."),
        new("life-event:mentor-favor", Source.LifeEvent, ExperienceEventRiskTier.Recoverable, "작성된 선택에 기분 -3이 있다."),
        new("life-event:masterpiece-commission", Source.LifeEvent, ExperienceEventRiskTier.None, "작성된 선택에 불리한 게임플레이 효과가 없고 야망 진행 또는 경험치 증가만 있다."),
        new("life-event:family-room", Source.LifeEvent, ExperienceEventRiskTier.None, "작성된 선택에 불리한 게임플레이 효과가 없고 관계 또는 기분 증가만 있다."),
        new("life-event:guardian-oath", Source.LifeEvent, ExperienceEventRiskTier.None, "작성된 선택에 불리한 게임플레이 효과가 없고 관계 증가 또는 체력 회복만 있다."),
        new("life-event:inherited-debt", Source.LifeEvent, ExperienceEventRiskTier.Recoverable, "작성된 선택에 세력 원한 8이 있다."),
        new("life-event:cultural-petition", Source.LifeEvent, ExperienceEventRiskTier.None, "작성된 선택에 불리한 게임플레이 효과가 없고 기분 또는 관계 증가만 있다."),
        new("life-event:position-rivalry", Source.LifeEvent, ExperienceEventRiskTier.None, "작성된 선택에 불리한 게임플레이 효과가 없고 경험치 또는 관계 증가만 있다."),
        new("life-event:captains-test", Source.LifeEvent, ExperienceEventRiskTier.None, "작성된 선택에 불리한 게임플레이 효과가 없고 야망 진행 또는 관계 증가만 있다."),
        new("life-event:disputed-thesis", Source.LifeEvent, ExperienceEventRiskTier.Recoverable, "작성된 선택에 작업 지연 2일이 있다."),
        new("life-event:clinic-shortage", Source.LifeEvent, ExperienceEventRiskTier.Recoverable, "작성된 선택에 세력 신뢰 -6이 있다."),
        new("life-event:retirement-request", Source.LifeEvent, ExperienceEventRiskTier.Serious, "작성된 선택이 은퇴 일정을 설정해 경력 종료를 만든다."),
        new("life-event:last-lesson", Source.LifeEvent, ExperienceEventRiskTier.Serious, "작성된 선택에 비치명 체력 피해 -5가 있다."),
        new("life-event:lineage-relic", Source.LifeEvent, ExperienceEventRiskTier.None, "작성된 선택에 불리한 게임플레이 효과가 없고 관계 증가 또는 WorldFlag만 있다."),
        new("life-event:killer-sighted", Source.LifeEvent, ExperienceEventRiskTier.None, "작성된 선택에 불리한 게임플레이 효과가 없고 트라우마 -8만 있다."),
        new("life-event:first-lost-tooth", Source.LifeEvent, ExperienceEventRiskTier.None, "작성된 자동 결과에 불리한 게임플레이 효과가 없다."),
        new("life-event:shared-lullaby", Source.LifeEvent, ExperienceEventRiskTier.None, "작성된 자동 결과에 불리한 게임플레이 효과가 없다."),
        new("life-event:first-safe-task", Source.LifeEvent, ExperienceEventRiskTier.None, "작성된 자동 결과에 불리한 게임플레이 효과가 없다."),
        new("life-event:tool-inheritance", Source.LifeEvent, ExperienceEventRiskTier.None, "작성된 자동 결과에 불리한 게임플레이 효과가 없다."),
        new("life-event:household-meal", Source.LifeEvent, ExperienceEventRiskTier.None, "작성된 자동 결과에 불리한 게임플레이 효과가 없다."),
        new("life-event:newborn-welcome", Source.LifeEvent, ExperienceEventRiskTier.None, "작성된 자동 결과에 불리한 게임플레이 효과가 없다."),
        new("life-event:quiet-promotion", Source.LifeEvent, ExperienceEventRiskTier.None, "작성된 자동 결과에 불리한 게임플레이 효과가 없다."),
        new("life-event:shift-saved", Source.LifeEvent, ExperienceEventRiskTier.None, "작성된 자동 결과에 불리한 게임플레이 효과가 없다."),
        new("life-event:retiree-story", Source.LifeEvent, ExperienceEventRiskTier.None, "작성된 자동 결과에 불리한 게임플레이 효과가 없다."),
        new("life-event:elder-birthday", Source.LifeEvent, ExperienceEventRiskTier.None, "작성된 자동 결과에 불리한 게임플레이 효과가 없다."),
        new("life-event:grave-visit", Source.LifeEvent, ExperienceEventRiskTier.None, "작성된 자동 결과에 불리한 게임플레이 효과가 없다."),
        new("life-event:story-compressed", Source.LifeEvent, ExperienceEventRiskTier.None, "작성된 자동 결과에 불리한 게임플레이 효과가 없다.")
    };

    public static string RunAll()
    {
        List<string> rows = new();
        int passed = 0;
        Run(rows, ref passed, "RUNTIME_CATALOG_EXACT53_RISK_PROFILES", VerifyRuntimeCatalog);
        Run(rows, ref passed, "EVERY_RISK_REASON_IS_NONEMPTY_AND_VALID", VerifyReasons);
        Run(rows, ref passed, "LAST_LESSON_IS_NONLETHAL_SERIOUS", VerifyLastLesson);
        Run(rows, ref passed, "ALL_GUEST_REQUESTS_ARE_RECOVERABLE", VerifyGuests);
        Run(rows, ref passed, "BUILDER_TABLES_EQUAL_ACTUAL_RISK_PROFILES", VerifyBuilderTables);
        rows.Add($"wim-039-experience-risk-authoring; RESULT={(passed == rows.Count ? "PASS" : "FAIL")}; passed={passed}; failed={rows.Count - passed}; rows={rows.Count}");
        return string.Join(Environment.NewLine, rows);
    }

    [MenuItem("DungeonStory/WIM-039/Run Experience Risk Authoring Scenarios")]
    public static void RunFromMenu() => Debug.Log(RunAll());

    private static void VerifyRuntimeCatalog()
    {
        Dictionary<string,V20AuthoredContentSO> actual = RuntimeDefinitions()
            .ToDictionary(value => value.StableId, StringComparer.Ordinal);
        RequireExactIds(actual.Keys, Expected.Select(value => value.Id), "risk catalog");
        foreach (Expectation expected in Expected)
        {
            Require(actual.TryGetValue(expected.Id, out V20AuthoredContentSO definition), $"Missing '{expected.Id}'.");
            RequireSource(definition, expected.Source);
            RequireRisk(definition, expected);
        }
    }

    private static void VerifyReasons()
    {
        Dictionary<string,V20AuthoredContentSO> actual = RuntimeDefinitions()
            .ToDictionary(value => value.StableId, StringComparer.Ordinal);
        foreach (Expectation expected in Expected)
        {
            Require(actual.TryGetValue(expected.Id, out V20AuthoredContentSO definition), $"Missing '{expected.Id}'.");
            (ExperienceEventRiskTier tier, string reason) = Profile(definition);
            Require(!string.IsNullOrWhiteSpace(reason) && string.Equals(reason, reason.Trim(), StringComparison.Ordinal), $"'{expected.Id}' has an empty or noncanonical reason.");
            SocietyEventRiskContract.RequireValid(tier, reason, expected.Id);
        }
    }

    private static void VerifyLastLesson()
    {
        LifeEventDefinitionSO lastLesson = RuntimeDefinitions().OfType<LifeEventDefinitionSO>()
            .Single(value => value.StableId == "life-event:last-lesson");
        Require(lastLesson.riskTier == ExperienceEventRiskTier.Serious
            && string.Equals(lastLesson.riskReason, "작성된 선택에 비치명 체력 피해 -5가 있다.", StringComparison.Ordinal),
            "last-lesson must remain explicitly Serious and nonlethal.");
    }

    private static void VerifyGuests()
    {
        GuestRequestDefinitionSO[] guests = RuntimeDefinitions().OfType<GuestRequestDefinitionSO>().ToArray();
        Require(guests.Length == 13 && guests.All(value => value.riskTier == ExperienceEventRiskTier.Recoverable),
            "Every published guest request must remain Recoverable.");
    }

    private static void VerifyBuilderTables()
    {
        VerifyBuilderTable(typeof(V20FactionServiceContentAssetBuilder), Expected.Where(value => value.Source != Source.LifeEvent));
        VerifyBuilderTable(typeof(V20NarrativeContentAssetBuilder), Expected.Where(value => value.Source == Source.LifeEvent));
    }

    private static void VerifyBuilderTable(Type builderType, IEnumerable<Expectation> expected)
    {
        FieldInfo field = builderType.GetField("ExperienceRisks", BindingFlags.Static | BindingFlags.NonPublic);
        IEnumerable values = field?.GetValue(null) as IEnumerable;
        Require(values != null, $"{builderType.Name}.ExperienceRisks is missing.");
        Dictionary<string,(ExperienceEventRiskTier Tier,string Reason)> actual = new(StringComparer.Ordinal);
        foreach (object entry in values)
        {
            Type type = entry.GetType();
            string id = type.GetProperty("Key")?.GetValue(entry) as string;
            object profile = type.GetProperty("Value")?.GetValue(entry);
            Type profileType = profile?.GetType();
            ExperienceEventRiskTier tier = (ExperienceEventRiskTier)(profileType?.GetField("Tier")?.GetValue(profile)
                ?? throw new InvalidOperationException($"{builderType.Name} has no risk tier."));
            string reason = profileType?.GetField("Reason")?.GetValue(profile) as string;
            Require(!string.IsNullOrWhiteSpace(id) && reason != null, $"{builderType.Name} contains an invalid risk profile.");
            actual.Add(id, (tier, reason));
        }
        Expectation[] expectedArray = expected.ToArray();
        RequireExactIds(actual.Keys, expectedArray.Select(value => value.Id), builderType.Name + " risk table");
        foreach (Expectation profile in expectedArray)
            Require(actual[profile.Id].Tier == profile.Tier
                && string.Equals(actual[profile.Id].Reason, profile.Reason, StringComparison.Ordinal),
                $"{builderType.Name} profile differs for '{profile.Id}'.");
    }

    private static IEnumerable<V20AuthoredContentSO> RuntimeDefinitions()
    {
        GameDomainContentCatalogSO catalog = AssetDatabase.LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath)
            ?? throw new InvalidOperationException("Game-domain content catalog is missing.");
        return catalog.GetAll<GuestRequestDefinitionSO>().Cast<V20AuthoredContentSO>()
            .Concat(catalog.GetAll<LifeEventDefinitionSO>())
            .Concat(catalog.GetAll<ServiceIncidentDefinitionSO>());
    }

    private static void RequireSource(V20AuthoredContentSO definition, Source source)
    {
        bool matches = source switch
        {
            Source.Guest => definition is GuestRequestDefinitionSO,
            Source.Incident => definition is ServiceIncidentDefinitionSO,
            Source.LifeEvent => definition is LifeEventDefinitionSO,
            _ => false
        };
        Require(matches, $"'{definition.StableId}' has the wrong risk source type.");
    }

    private static void RequireRisk(V20AuthoredContentSO definition, Expectation expected)
    {
        (ExperienceEventRiskTier tier, string reason) = Profile(definition);
        Require(tier == expected.Tier && string.Equals(reason, expected.Reason, StringComparison.Ordinal),
            $"'{expected.Id}' differs from the approved literal risk profile.");
    }

    private static (ExperienceEventRiskTier Tier,string Reason) Profile(V20AuthoredContentSO definition) => definition switch
    {
        GuestRequestDefinitionSO guest => (guest.riskTier, guest.riskReason),
        ServiceIncidentDefinitionSO incident => (incident.riskTier, incident.riskReason),
        LifeEventDefinitionSO lifeEvent => (lifeEvent.riskTier, lifeEvent.riskReason),
        _ => throw new InvalidOperationException($"Unsupported risk definition '{definition?.GetType().Name}'.")
    };

    private static void RequireExactIds(IEnumerable<string> actual, IEnumerable<string> expected, string label)
    {
        string[] actualIds = actual.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        string[] expectedIds = expected.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        Require(actualIds.SequenceEqual(expectedIds, StringComparer.Ordinal), $"{label} must have exact approved coverage.");
    }

    private static void Run(ICollection<string> rows, ref int passed, string name, Action scenario)
    {
        try { scenario(); rows.Add(name + "=PASS"); passed++; }
        catch (Exception exception) { rows.Add(name + "=FAIL; " + exception.Message); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private enum Source { Guest, Incident, LifeEvent }

    private sealed class Expectation
    {
        public Expectation(string id, Source source, ExperienceEventRiskTier tier, string reason)
        {
            Id = id; Source = source; Tier = tier; Reason = reason;
        }

        public string Id { get; }
        public Source Source { get; }
        public ExperienceEventRiskTier Tier { get; }
        public string Reason { get; }
    }
}
#endif
