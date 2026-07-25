using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CharacterFeedbackDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Character/Run P1 Character Feedback Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P1 character feedback scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();

        RunScenario("LLM record rewrite keeps event identity", VerifyNarrativeDisplayRewrite, errors);
        RunScenario("LLM record response validation", VerifyNarrativeResponseValidation, errors);

        RunScenario("구조화 활동 이벤트 계약", VerifyStructuredActivityContract, errors);
        RunScenario("로그 태그와 반복 누적", VerifyLogTagsAndRepeatCount, errors);
        RunScenario("상세 작업 로그 보존", VerifyDetailedWorkLog, errors);
        RunScenario("풍선 상태 분류", VerifyFeedbackBubbleStateClassification, errors);
        RunScenario("클릭 로그 포맷", VerifySummaryLogFormatting, errors);
        RunScenario("기분 기준값과 욕구 보정 호환", VerifyMoodDerivationCompatibility, errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("P1 character feedback scenarios passed.");
        }

        return true;
    }

    private static void RunScenario(string name, System.Func<bool> scenario, List<string> errors)
    {
        if (scenario()) return;

        errors.Add(name);
    }

    private static bool VerifyStructuredActivityContract()
    {
        CharacterActor character = CreateCharacter("Structured_Activity_Contract_Test");
        List<CharacterLogEntry> emitted = new List<CharacterLogEntry>();
        character.OnLogAdded += emitted.Add;

        CharacterActivityEvent first = CharacterActivityEvent.Create(
            CharacterActivityKinds.Shopping,
            CharacterActivityOutcomes.Failed,
            "통로가 막혀 상점에 가지 못했다",
            actionId: "shopping:visit",
            targetId: "facility:42",
            targetName: "무기상점",
            reasonCode: "destination-unreachable",
            facilityId: 42,
            sentiment: -0.6f,
            bubbleEligible: true);
        CharacterActivityEvent reworded = CharacterActivityEvent.Create(
            CharacterActivityKinds.Shopping,
            CharacterActivityOutcomes.Failed,
            "상점까지 가는 길을 찾지 못했다",
            actionId: "shopping:visit",
            targetId: "facility:42",
            targetName: "무기상점",
            reasonCode: "destination-unreachable",
            facilityId: 42,
            sentiment: -0.6f,
            bubbleEligible: true);
        character.AddActivity(first);
        character.AddActivity(reworded);
        character.AddActivity(CharacterActivityEvent.InternalAi(
            CharacterActivityOutcomes.Changed,
            "replan",
            "AI replan: destination changed"));
        character.AddLog("호환 문자열 기록");

        bool activityListMutationBlocked = MutationThrows(() =>
            ((IList<CharacterActivityEvent>)character.LogComponent.ActivityEntries)[0] = reworded);
        bool displayListMutationBlocked = MutationThrows(() =>
            ((IList<string>)character.Log)[0] = "변조");

        bool deferredDisplayMode = Application.isPlaying;
        bool firstDisplayHidden = emitted.Count > 0
            && !character.LogComponent.TryGetVisibleDisplayLine(emitted[0].EntryId, out _);
        bool secondDisplayHidden = emitted.Count > 1
            && !character.LogComponent.TryGetVisibleDisplayLine(emitted[1].EntryId, out _);
        bool displayContractValid = deferredDisplayMode
            ? firstDisplayHidden
                && secondDisplayHidden
                && character.Log.Count == 1
                && character.Log[0] == "호환 문자열 기록"
            : character.Log.Count == 2
                && character.Log[0] == "통로가 막혀 상점에 가지 못했다 x2"
                && character.Log[1] == "호환 문자열 기록";

        bool valid = emitted.Count == 4
            && emitted[0].EntryId == emitted[1].EntryId
            && emitted[1].Count == 2
            && emitted[1].Activity.FacilityId == 42
            && Mathf.Approximately(emitted[1].Activity.Sentiment, -0.6f)
            && emitted[1].Activity.AggregationKey == first.AggregationKey
            && string.IsNullOrEmpty(first.ActorId)
            && !string.IsNullOrEmpty(emitted[0].Activity.ActorId)
            && activityListMutationBlocked
            && displayListMutationBlocked
            && character.LogComponent.ActivityEntries.Count == 3
            && displayContractValid
            && !character.Log.Any(line => line.Contains("AI replan", System.StringComparison.Ordinal))
            && CharacterLogNarrativeService.ShouldNarrate(emitted[0])
            && !CharacterLogNarrativeService.ShouldNarrate(emitted[1])
            && !CharacterLogNarrativeService.ShouldNarrate(emitted[2])
            && !CharacterLogNarrativeService.ShouldNarrate(emitted[3])
            && CharacterFeedbackBubble.ClassifyActivity(emitted[0].Activity)
                == CharacterFeedbackState.Confused;

        if (!valid)
        {
            Debug.LogError(
                "Structured activity contract detail: "
                + $"emitted={emitted.Count}; entries={character.LogComponent.ActivityEntries.Count}; "
                + $"visible={character.Log.Count}; deferred={deferredDisplayMode}; "
                + $"hidden0={firstDisplayHidden}; hidden1={secondDisplayHidden}; "
                + $"displayValid={displayContractValid}; "
                + $"line0={(character.Log.Count > 0 ? character.Log[0] : "<none>")}; "
                + $"line1={(character.Log.Count > 1 ? character.Log[1] : "<none>")}; "
                + $"mutationActivity={activityListMutationBlocked}; mutationDisplay={displayListMutationBlocked}; "
                + $"narrate={FormatNarrateStates(emitted)}");
        }

        Object.DestroyImmediate(character.gameObject);
        return valid;
    }

    private static string FormatNarrateStates(IReadOnlyList<CharacterLogEntry> entries)
    {
        if (entries == null || entries.Count == 0)
        {
            return "<none>";
        }

        return string.Join(
            ",",
            entries.Select(entry =>
                $"{entry.EntryId}:{entry.Count}:{CharacterLogNarrativeService.ShouldNarrate(entry)}"));
    }

    private static bool MutationThrows(System.Action mutation)
    {
        try
        {
            mutation();
            return false;
        }
        catch (System.NotSupportedException)
        {
            return true;
        }
    }

    private static bool VerifyLogTagsAndRepeatCount()
    {
        CharacterActor character = CreateCharacter("Feedback_Log_Test");
        int eventCount = 0;
        CharacterLogEntry lastEntry = default;
        character.OnLogAdded += (entry) =>
        {
            eventCount++;
            lastEntry = entry;
        };

        character.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Shopping,
            CharacterActivityOutcomes.Failed,
            "길 막힘",
            actionId: "shopping:visit",
            reasonCode: "missing-destination",
            sentiment: -0.7f,
            bubbleEligible: true));
        character.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Shopping,
            CharacterActivityOutcomes.Failed,
            "길 막힘",
            actionId: "shopping:visit",
            reasonCode: "missing-destination",
            sentiment: -0.7f,
            bubbleEligible: true));
        character.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Stock,
            CharacterActivityOutcomes.Failed,
            "재고 부족",
            actionId: "stock:restock",
            reasonCode: "warehouse-stock-shortage",
            sentiment: -0.5f,
            bubbleEligible: true));

        bool valid = character.Log.Count == 2
            && character.Log[0] == "길 막힘 x2"
            && character.Log[1] == "재고 부족"
            && eventCount == 3
            && lastEntry.Tag == CharacterActivityKinds.Stock
            && lastEntry.Activity.ReasonCode == "warehouse-stock-shortage";

        if (!valid)
        {
            Debug.LogError(
                $"Structured repeat detail: lines=[{string.Join(" | ", character.Log)}], "
                + $"events={eventCount}, tag={lastEntry.Tag}, "
                + $"reason={lastEntry.Activity?.ReasonCode ?? "null"}");
        }

        Object.DestroyImmediate(character.gameObject);
        return valid;
    }

    private static bool VerifyFeedbackBubbleStateClassification()
    {
        CharacterActor character = CreateCharacter("Feedback_Bubble_Test");
        CharacterFeedbackBubble bubble = character.GetComponent<CharacterFeedbackBubble>()
            ?? character.gameObject.AddComponent<CharacterFeedbackBubble>();

        bool tagStates = CharacterFeedbackBubble.ClassifyActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Work,
                CharacterActivityOutcomes.Completed,
                "완료",
                bubbleEligible: true)) == CharacterFeedbackState.Joy
            && CharacterFeedbackBubble.ClassifyActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Stock,
                CharacterActivityOutcomes.Blocked,
                "재고 부족",
                bubbleEligible: true)) == CharacterFeedbackState.Confused
            && CharacterFeedbackBubble.ClassifyActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Combat,
                CharacterActivityOutcomes.Damaged,
                "위험",
                bubbleEligible: true)) == CharacterFeedbackState.Anger
            && CharacterFeedbackBubble.ClassifyActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Duty,
                CharacterActivityOutcomes.Blocked,
                "피로",
                bubbleEligible: true)) == CharacterFeedbackState.Fatigue;

        character.stats[CharacterCondition.SLEEP] = 10f;
        character.stats[CharacterCondition.MOOD] = 100f;
        CharacterFeedbackState fatigueState = bubble.EvaluatePersistentState();
        bool fatigue = fatigueState == CharacterFeedbackState.Fatigue;

        character.stats[CharacterCondition.HUNGER] = 50f;
        character.stats[CharacterCondition.SLEEP] = 50f;
        character.stats[CharacterCondition.FUN] = 50f;
        character.stats[CharacterCondition.EXCRETION] = 50f;
        character.stats[CharacterCondition.HYGIENE] = 50f;
        character.stats[CharacterCondition.MOOD] = 10f;
        CharacterFeedbackState angerState = bubble.EvaluatePersistentState();
        float angerMood = character.stats[CharacterCondition.MOOD];
        bool anger = angerState == CharacterFeedbackState.Anger;

        character.stats[CharacterCondition.MOOD] = 25f;
        CharacterFeedbackState discontentState = bubble.EvaluatePersistentState();
        bool discontent = discontentState == CharacterFeedbackState.Discontent;

        if (!(tagStates && fatigue && anger && discontent))
        {
            Debug.LogError(
                $"Feedback state detail: tags={tagStates}, fatigue={fatigueState}, "
                + $"anger={angerState}, discontent={discontentState}, "
                + $"angerMood={angerMood:0.#}, "
                + $"sleep={character.stats[CharacterCondition.SLEEP]:0.#}, "
                + $"mood={character.stats[CharacterCondition.MOOD]:0.#}, "
                + $"excretion={character.stats[CharacterCondition.EXCRETION]:0.#}, "
                + $"hygiene={character.stats[CharacterCondition.HYGIENE]:0.#}");
        }

        Object.DestroyImmediate(character.gameObject);
        return tagStates && fatigue && anger && discontent;
    }

    private static bool VerifyDetailedWorkLog()
    {
        CharacterActor character = CreateCharacter("Feedback_Work_Log_Test");
        character.AddActivity(CharacterActivityEvent.Work(
            BuiltInWorkTypeIds.Research,
            CharacterActivityOutcomes.Started,
            "작업 시작 · 연구 · 연구실"));
        character.AddActivity(CharacterActivityEvent.Work(
            BuiltInWorkTypeIds.Research,
            CharacterActivityOutcomes.Started,
            "작업 시작 · 연구 · 연구실"));
        character.AddActivity(CharacterActivityEvent.Work(
            BuiltInWorkTypeIds.Research,
            CharacterActivityOutcomes.Completed,
            "작업 종료 · 연구 · 연구실 · 완료"));

        bool valid = character.Log.Count == 2
            && character.Log[0] == "작업 시작 · 연구 · 연구실 x2"
            && character.Log[1] == "작업 종료 · 연구 · 연구실 · 완료"
            && !character.Log.Any(line => line == "행동 시작");

        CharacterActor unassigned = CreateCharacter("Feedback_Unassigned_Work_Log_Test");
        WorkDebugLog.LogStarted(unassigned);
        WorkDebugLog.LogEnd(unassigned, "근무 피로 누적");
        valid &= unassigned.Log.Count == 0;

        if (!valid)
        {
            Debug.LogError(
                $"Structured work detail: lines=[{string.Join(" | ", character.Log)}], "
                + $"unassigned=[{string.Join(" | ", unassigned.Log)}]");
        }

        Object.DestroyImmediate(character.gameObject);
        Object.DestroyImmediate(unassigned.gameObject);
        return valid;
    }

    private static bool VerifySummaryLogFormatting()
    {
        CharacterActor character = CreateCharacter("Feedback_Summary_Test");
        character.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Shopping,
            CharacterActivityOutcomes.Failed,
            "길 막힘",
            actionId: "shopping:visit",
            reasonCode: "missing-destination",
            bubbleEligible: true));
        character.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Stock,
            CharacterActivityOutcomes.Failed,
            "재고 부족",
            actionId: "stock:restock",
            reasonCode: "warehouse-stock-shortage",
            bubbleEligible: true));
        character.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Duty,
            CharacterActivityOutcomes.Blocked,
            "피로",
            actionId: "duty:rest",
            reasonCode: "fatigue-protection",
            bubbleEligible: true));

        string text = CharacterSummeryInfo.FormatLogText(CharacterActor.From(character), 2);
        bool valid = !text.Contains("최근 기록")
            && text.StartsWith("- 피로", System.StringComparison.Ordinal)
            && text.Contains("재고 부족")
            && text.Contains("피로");

        if (!valid)
        {
            Debug.LogError($"Structured summary detail: {text}");
        }

        Object.DestroyImmediate(character.gameObject);
        return valid;
    }

    private static bool VerifyNarrativeDisplayRewrite()
    {
        CharacterActor character = CreateCharacter("Feedback_Narrative_Rewrite_Test");
        CharacterLogEntry firstEntry = default;
        CharacterLogEntry repeatedEntry = default;
        int addedEventCount = 0;
        int displayChangeCount = 0;
        character.OnLogAdded += entry =>
        {
            addedEventCount++;
            if (addedEventCount == 1)
            {
                firstEntry = entry;
            }
            else
            {
                repeatedEntry = entry;
            }
        };
        character.LogComponent.OnLogDisplayChanged += () => displayChangeCount++;

        character.AddActivity(CharacterActivityEvent.Work(
            BuiltInWorkTypeIds.Research,
            CharacterActivityOutcomes.Started,
            "작업 시작 · 연구 · 연구실"));
        bool deferredDisplayMode = Application.isPlaying;
        bool hiddenBeforeRewrite = deferredDisplayMode
            ? !character.LogComponent.TryGetVisibleDisplayLine(firstEntry.EntryId, out _)
                && character.Log.Count == 0
            : character.LogComponent.TryGetVisibleDisplayLine(firstEntry.EntryId, out _)
                && character.Log.Count == 1;
        bool rewritten = character.LogComponent.TryUpdateDisplayLine(
            firstEntry.EntryId,
            firstEntry.DisplayLine,
            "연구실에 자리를 잡고 연구를 시작했다.");
        bool visibleAfterRewrite = character.LogComponent.TryGetVisibleDisplayLine(
                firstEntry.EntryId,
                out string rewrittenLine)
            && rewrittenLine == "연구실에 자리를 잡고 연구를 시작했다.";
        character.AddActivity(CharacterActivityEvent.Work(
            BuiltInWorkTypeIds.Research,
            CharacterActivityOutcomes.Started,
            "작업 시작 · 연구 · 연구실"));
        bool staleRewriteRejected = !character.LogComponent.TryUpdateDisplayLine(
            repeatedEntry.EntryId,
            firstEntry.DisplayLine,
            "뒤늦게 도착한 응답은 적용되지 않는다.");

        bool valid = firstEntry.EntryId > 0
            && repeatedEntry.EntryId == firstEntry.EntryId
            && hiddenBeforeRewrite
            && rewritten
            && visibleAfterRewrite
            && staleRewriteRejected
            && addedEventCount == 2
            && displayChangeCount >= 1
            && (deferredDisplayMode
                ? character.Log.Count == 0
                : character.Log.Count == 1
                    && character.Log[0] == "연구실에 자리를 잡고 연구를 시작했다. x2");

        Object.DestroyImmediate(character.gameObject);
        return valid;
    }

    private static bool VerifyMoodDerivationCompatibility()
    {
        CharacterActor character = CreateCharacter("Feedback_Mood_Derivation_Test");
        character.stats = new Dictionary<CharacterCondition, float>
        {
            { CharacterCondition.HUNGER, 50f },
            { CharacterCondition.SLEEP, 50f },
            { CharacterCondition.FUN, 50f },
            { CharacterCondition.MOOD, 10f },
            { CharacterCondition.EXCRETION, 50f },
            { CharacterCondition.HYGIENE, 50f }
        };

        float assigned = character.stats[CharacterCondition.MOOD];
        character.ChangesStat(CharacterCondition.HUNGER, -40f);
        float deprived = character.stats[CharacterCondition.MOOD];
        CharacterMoodSnapshot deprivedSnapshot = character.Mood;

        character.stats[CharacterCondition.MOOD] = 80f;
        float overridden = character.Mood.Value;
        StaffDiscontentRules rules = StaffDiscontentRules.CreateDefault();
        StaffDiscontentStage lowStage = StaffDiscontentService.EvaluateStage(10f, 0, rules);
        StaffDiscontentStage highStage = StaffDiscontentService.EvaluateStage(80f, 0, rules);

        bool valid = Mathf.Approximately(assigned, 10f)
            && deprived < assigned
            && deprivedSnapshot.Factors.Any(item => item.Id == "need:hunger" && item.Value < 0f)
            && Mathf.Approximately(overridden, 80f)
            && lowStage != StaffDiscontentStage.Stable
            && highStage == StaffDiscontentStage.Stable;

        Object.DestroyImmediate(character.gameObject);
        return valid;
    }

    private static bool VerifyNarrativeResponseValidation()
    {
        bool validResponse = CharacterLogNarrativeService.TryParseNarrativeLine(
            "{\"line\":\"테스트 캐릭터가 엉뚱한 방향으로 움직였다가 슬쩍 되돌아와 연구실에서 새 연구를 차분히 시작했다\"}",
            "작업 시작 · 연구 · 연구실",
            "테스트 캐릭터가",
            out string line,
            out _);
        bool blandRecordRejected = !CharacterLogNarrativeService.TryParseNarrativeLine(
            "{\"line\":\"테스트 캐릭터가 연구실에서 새 연구를 차분히 시작했다\"}",
            "작업 시작 · 연구 · 연구실",
            "테스트 캐릭터가",
            out _,
            out _);
        bool missingSubjectRejected = !CharacterLogNarrativeService.TryParseNarrativeLine(
            "{\"line\":\"연구실에서 새 연구를 차분히 시작했다\"}",
            "작업 시작 · 연구 · 연구실",
            "테스트 캐릭터가",
            out _,
            out _);
        bool wrongParticleRejected = !CharacterLogNarrativeService.TryParseNarrativeLine(
            "{\"line\":\"테스트 캐릭터이 연구실에서 새 연구를 차분히 시작했다\"}",
            "작업 시작 · 연구 · 연구실",
            "테스트 캐릭터가",
            out _,
            out _);
        bool unchangedRejected = !CharacterLogNarrativeService.TryParseNarrativeLine(
            "{\"line\":\"작업 시작 · 연구 · 연구실\"}",
            "작업 시작 · 연구 · 연구실",
            out _,
            out _);
        bool englishRejected = !CharacterLogNarrativeService.TryParseNarrativeLine(
            "{\"line\":\"Started research in the laboratory.\"}",
            "작업 시작 · 연구 · 연구실",
            out _,
            out _);
        bool hallucinationRejected = !CharacterLogNarrativeService.TryParseNarrativeLine(
            "{\"line\":\"재료를 정리하고 마무리했다, 지하 연구실\"}",
            "작업 종료 · 연금 연구 · 지하 연구실 · 새 제조법 정리 완료",
            out _,
            out _);
        bool inventedRewardRejected = !CharacterLogNarrativeService.TryParseNarrativeLine(
            "{\"line\":\"테스트 캐릭터가 지하 연구실에서 연금 연구를 마치고 금화 100개를 얻어 새 제조법 정리를 마무리했다.\"}",
            "작업 종료 · 연금 연구 · 지하 연구실 · 새 제조법 정리 완료",
            "테스트 캐릭터가",
            out _,
            out _);
        bool formalStyleRejected = !CharacterLogNarrativeService.TryParseNarrativeLine(
            "{\"line\":\"연구실에서 연구를 시작했습니다.\"}",
            "작업 시작 · 연구 · 연구실",
            out _,
            out _);
        bool genericFormalEndingRejected = !CharacterLogNarrativeService.TryParseNarrativeLine(
            "{\"line\":\"테스트 캐릭터가 엉뚱한 방향으로 움직였다가 슬쩍 되돌아와 연구실에서 연구를 마쳤습니다.\"}",
            "작업 종료 · 연구 · 연구실",
            "테스트 캐릭터가",
            out _,
            out _);
        bool internetSlangRejected = !CharacterLogNarrativeService.TryParseNarrativeLine(
            "{\"line\":\"테스트 캐릭터가 딴생각에 빠졌다가 뇌피셜을 접고 연구실에서 연구를 마쳤다.\"}",
            "작업 종료 · 연구 · 연구실",
            "테스트 캐릭터가",
            out _,
            out _);
        bool overlongRecordRejected = !CharacterLogNarrativeService.TryParseNarrativeLine(
            "{\"line\":\"테스트 캐릭터가 잠시 딴생각에 빠졌다가 뒤늦게 정신을 차리고 새 제조법 정리까지 마무리하고 지하 연구실에서 연금 연구를 마쳤다.\"}",
            "작업 종료 · 연금 연구 · 지하 연구실 · 새 제조법 정리 완료",
            "테스트 캐릭터가",
            out _,
            out _);
        bool unrelatedSceneDetailRejected = !CharacterLogNarrativeService.TryParseNarrativeLine(
            "{\"line\":\"테스트 캐릭터가 낯선 통로에서 허둥댄 뒤 지하 연구실에서 연금 연구에 착수했다.\"}",
            "작업 시작 · 연금 연구 · 지하 연구실",
            "테스트 캐릭터가",
            out _,
            out _);
        bool variedStartAccepted = CharacterLogNarrativeService.TryParseNarrativeLine(
            "{\"line\":\"테스트 캐릭터가 의욕이 앞서 잠깐 허둥댄 뒤 태연한 척하고 연구에 착수하며 연구실에 들어갔다.\"}",
            "작업 시작 · 연구 · 연구실",
            "테스트 캐릭터가",
            out _,
            out _);
        bool movementStartAccepted = CharacterLogNarrativeService.TryParseNarrativeLine(
            "{\"line\":\"테스트 캐릭터가 괜히 한 박자 멈칫했다가 머쓱한 기색으로 연구실로 향해 연구에 착수했다.\"}",
            "작업 시작 · 연구 · 연구실",
            "테스트 캐릭터가",
            out _,
            out _);
        bool alteredSourcePhraseRejected = !CharacterLogNarrativeService.TryParseNarrativeLine(
            "{\"line\":\"테스트 캐릭터가 수줍게 연금을 찾기 위해 지하 연구실에 들어갔다.\"}",
            "작업 시작 · 연금 연구 · 지하 연구실",
            "테스트 캐릭터가",
            out _,
            out _);
        bool intentionOnlyRejected = !CharacterLogNarrativeService.TryParseNarrativeLine(
            "{\"line\":\"테스트 캐릭터가 진열대를 한참 바라보다가 무기상점에서 무기 판매를 시작하기로 마음 먹었다.\"}",
            "작업 시작 · 무기 판매 · 무기상점",
            "테스트 캐릭터가",
            out _,
            out _);
        IReadOnlyList<string> anchors = CharacterLogNarrativeService.ExtractRequiredAnchors(
            "작업 종료 · 연금 연구 · 지하 연구실 · 새 제조법 정리 완료");
        IReadOnlyList<string> phrases = CharacterLogNarrativeService.ExtractRequiredPhrases(
            "작업 종료 · 연금 연구 · 지하 연구실 · 새 제조법 정리 완료");
        string correctionPrompt = CharacterLogNarrativeService.BuildCorrectionPrompt(
            new CharacterLogEntry(
                8,
                "작업 종료 · 연금 연구 · 지하 연구실 · 새 제조법 정리 완료",
                "작업 종료 · 연금 연구 · 지하 연구실 · 새 제조법 정리 완료",
                1,
                "작업 종료 · 연금 연구 · 지하 연구실 · 새 제조법 정리 완료"),
            "테스트 캐릭터가",
            "{\"line\":\"재료를 정리했다.\"}",
            "missing anchors");
        CharacterLogEntry eligible = new CharacterLogEntry(
            7,
            "작업 시작 · 연구 · 연구실",
            "작업 시작 · 연구 · 연구실",
            1,
            "작업 시작 · 연구 · 연구실");
        CharacterLogEntry repeated = new CharacterLogEntry(
            7,
            eligible.Tag,
            eligible.DisplayLine + " x2",
            2,
            eligible.OriginalMessage);
        CharacterLogEntry successfulWait = new CharacterLogEntry(
            9,
            CharacterActivityKinds.Wait,
            "대기 던전 배회",
            1,
            "대기 던전 배회",
            CharacterActivityEvent.Create(
                CharacterActivityKinds.Wait,
                CharacterActivityOutcomes.Completed,
                "대기 던전 배회",
                actionId: "wait:wander",
                narrativeEligible: false));
        string offDutyCorrectionPrompt = CharacterLogNarrativeService.BuildCorrectionPrompt(
            new CharacterLogEntry(
                10,
                "비번 시작 · 근무 피로 누적",
                "비번 시작 · 근무 피로 누적",
                1,
                "비번 시작: 근무 피로 누적"),
            "김철이",
            "{\"line\":\"비번에서 피로를 시작했다.\"}",
            "awkward transition");
        string variedCompletionPrompt = CharacterLogNarrativeService.BuildCorrectionPrompt(
            new CharacterLogEntry(
                11,
                "작업 종료 · 연금 연구 · 지하 연구실 · 새 제조법 정리 완료",
                "작업 종료 · 연금 연구 · 지하 연구실 · 새 제조법 정리 완료",
                1,
                "작업 종료 · 연금 연구 · 지하 연구실 · 새 제조법 정리 완료"),
            "테스트 캐릭터가",
            "{\"line\":\"새 제조법 정리까지 정리했다.\"}",
            "repeated result wording");
        string[] variedStartPrompts = Enumerable.Range(20, 4)
            .Select(entryId => CharacterLogNarrativeService.BuildPrompt(
                null,
                null,
                new CharacterLogEntry(
                    entryId,
                    "작업 시작 · 연구 · 연구실",
                    "작업 시작 · 연구 · 연구실",
                    1,
                    "작업 시작 · 연구 · 연구실")))
            .ToArray();
        CharacterLogEntry fallbackEntry = new CharacterLogEntry(
            11,
            "작업 종료 · 연금 연구 · 지하 연구실 · 새 제조법 정리 완료",
            "작업 종료 · 연금 연구 · 지하 연구실 · 새 제조법 정리 완료",
            1,
            "작업 종료 · 연금 연구 · 지하 연구실 · 새 제조법 정리 완료");
        string controlledFallback = CharacterLogNarrativeService.BuildControlledFallbackLine(
            fallbackEntry,
            "테스트 캐릭터가");
        bool controlledFallbackValid = CharacterLogNarrativeService.TryParseNarrativeLine(
            JsonUtility.ToJson(new CharacterRecordJsonDto { line = controlledFallback }),
            fallbackEntry.OriginalMessage,
            "테스트 캐릭터가",
            out _,
            out _);

        bool valid = validResponse
            && line == "테스트 캐릭터가 엉뚱한 방향으로 움직였다가 슬쩍 되돌아와 연구실에서 새 연구를 차분히 시작했다."
            && blandRecordRejected
            && missingSubjectRejected
            && wrongParticleRejected
            && unchangedRejected
            && englishRejected
            && hallucinationRejected
            && inventedRewardRejected
            && formalStyleRejected
            && genericFormalEndingRejected
            && internetSlangRejected
            && overlongRecordRejected
            && unrelatedSceneDetailRejected
            && variedStartAccepted
            && movementStartAccepted
            && alteredSourcePhraseRejected
            && intentionOnlyRejected
            && CharacterLogNarrativeService.HasCreativeDetail(
                line,
                "작업 시작 · 연구 · 연구실",
                "테스트 캐릭터가")
            && anchors.SequenceEqual(new[] { "연금", "연구", "지하", "연구실", "제조법", "정리" })
            && phrases.SequenceEqual(new[] { "연금 연구", "지하 연구실", "새 제조법 정리" })
            && correctionPrompt.Contains("<subject> <place>에서 <work>를 마치고 <result>도 끝냈다.")
            && correctionPrompt.Contains("테스트 캐릭터가 엉뚱한 길로 샜다가 돌아와")
            && correctionPrompt.Contains("지하 연구실에서 연금 연구를 마치고 새 제조법 정리도 끝냈다.")
            && correctionPrompt.Contains("harmless micro-situation")
            && offDutyCorrectionPrompt.Contains("<subject> <reason>으로 비번을 시작했다.")
            && offDutyCorrectionPrompt.Contains("김철이 한 박자 멈칫했다가 머쓱하게")
            && offDutyCorrectionPrompt.Contains("근무 피로 누적으로 비번을 시작했다.")
            && variedCompletionPrompt.Contains("지하 연구실에서 연금 연구와 새 제조법 정리를 마쳤다.")
            && !variedCompletionPrompt.Contains("새 제조법 정리까지 정리하고")
            && variedStartPrompts[0].Contains("<subject> <place>에서 <action>을 시작했다.")
            && variedStartPrompts[1].Contains("<subject> <place>에서 <action>에 착수했다.")
            && variedStartPrompts[2].Contains("<subject> <place>로 향해 <action>에 착수했다.")
            && variedStartPrompts[3].Contains("<subject> <place>에서 <action>을 시작했다.")
            && variedStartPrompts.Distinct().Count() == 4
            && controlledFallbackValid
            && controlledFallback.Length <= CharacterLogNarrativeService.MaxLineCharacters
            && controlledFallback.Contains("딴생각에서 퍼뜩 깨어나")
            && controlledFallback.Contains("새 제조법 정리")
            && controlledFallback.Contains("지하 연구실")
            && controlledFallback.Contains("연금 연구")
            && CharacterLogNarrativeService.BuildRequiredSubject("테스트 캐릭터") == "테스트 캐릭터가"
            && CharacterLogNarrativeService.BuildRequiredSubject("김철") == "김철이"
            && CharacterLogNarrativeService.BuildRequiredSubject("asd") == "asd가"
            && CharacterLogNarrativeService.ShouldNarrate(eligible)
            && !CharacterLogNarrativeService.ShouldNarrate(repeated)
            && !CharacterLogNarrativeService.ShouldNarrate(successfulWait);

        if (!valid)
        {
            Debug.LogError(
                "Narrative validation detail: "
                + $"eligible={CharacterLogNarrativeService.ShouldNarrate(eligible)}, "
                + $"repeated={CharacterLogNarrativeService.ShouldNarrate(repeated)}, "
                + $"wait={CharacterLogNarrativeService.ShouldNarrate(successfulWait)}, "
                + $"controlledFallbackValid={controlledFallbackValid}");
        }

        return valid;
    }

    private static CharacterActor CreateCharacter(string name)
    {
        GameObject obj = new GameObject(name);
        CharacterActor character = obj.AddComponent<CharacterActor>();
        CharacterAiEditorTestDependencies.Inject(obj);
        character.EnsureRuntimeState();
        character.stats = new Dictionary<CharacterCondition, float>
        {
            { CharacterCondition.HUNGER, 100f },
            { CharacterCondition.SLEEP, 100f },
            { CharacterCondition.FUN, 100f },
            { CharacterCondition.EXCRETION, 100f },
            { CharacterCondition.HYGIENE, 100f },
            { CharacterCondition.MOOD, 100f }
        };
        return character;
    }
}
