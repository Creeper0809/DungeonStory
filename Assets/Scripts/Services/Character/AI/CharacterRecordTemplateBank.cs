using System;
using System.Collections.Generic;
using UnityEngine;

public static class CharacterRecordTemplateBank
{
    private const int MaxLineLength = CharacterLogNarrativeService.MaxLineCharacters;

    private static readonly Dictionary<string, string> WorkLabels = new Dictionary<string, string>(
        StringComparer.Ordinal)
    {
        { "work:operate", "운영" },
        { "work:restock", "보충" },
        { "work:repair", "수리" },
        { "work:clean", "청소" },
        { "work:research", "연구" },
        { "work:guard", "경비" },
        { "work:reception", "응대" },
        { "work:rescue", "구조" },
        { "work:rest", "휴식" },
        { "work:craft", "제작" },
        { "work:haul", "운반" },
        { "work:hunt", "사냥" },
        { "work:butcher", "도축" },
        { "work:draw-water", "급수" },
        { "work:cook", "조리" },
        { "work:treat", "치료" },
        { "work:refuel", "연료 보충" },
        { "work:alchemy-research", "연금 연구" },
        { "work:weapon-sales", "무기 판매" },
        { "work:cleaning", "청소" }
    };

    private static readonly Dictionary<string, string[]> WorkStartedTemplates =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "work:research", new[]
                {
                    "{subject} 메모를 한 번 접고 {place}에서 {workObject} 시작했다.",
                    "{subject} 생각을 가다듬고 {place}에서 {workObject} 붙잡았다.",
                    "{subject} 책상 앞에서 숨을 고른 뒤 {workObject} 시작했다.",
                    "{subject} 잉크 자국을 훔치고 {workObject} 파고들었다."
                }
            },
            { "work:clean", new[]
                {
                    "{subject} 먼지를 보고 한숨 쉰 뒤 {place}에서 {workObject} 시작했다.",
                    "{subject} 소매를 걷고 {place} 바닥부터 닦기 시작했다.",
                    "{subject} 지저분한 구석을 콕 집어 {workObject} 시작했다.",
                    "{subject} 빗자루 각도를 고쳐 잡고 {workObject} 들어갔다."
                }
            },
            { "work:repair", new[]
                {
                    "{subject} 삐걱대는 곳을 짚고 {place}에서 {workObject} 시작했다.",
                    "{subject} 공구를 한번 돌려 쥐고 {workObject} 들어갔다.",
                    "{subject} 금 간 부분을 살피며 {workObject} 시작했다.",
                    "{subject} 대충 넘기려다 말고 {workObject} 붙잡았다."
                }
            },
            { "work:restock", new[]
                {
                    "{subject} 빈 칸을 세어 보고 {place}에서 {workObject} 시작했다.",
                    "{subject} 물건 줄을 맞추며 {workObject} 들어갔다.",
                    "{subject} 선반을 훑고 부족한 물품을 채우기 시작했다.",
                    "{subject} 품목을 다시 확인한 뒤 {workObject} 시작했다."
                }
            },
            { "work:guard", new[]
                {
                    "{subject} 주변을 훑어보고 {place}에서 {workObject} 섰다.",
                    "{subject} 발끝을 고정하고 {workObject} 자리를 잡았다.",
                    "{subject} 문 쪽을 살피며 {workObject} 시작했다.",
                    "{subject} 눈을 가늘게 뜨고 {place} 경계를 맡았다."
                }
            },
            { "work:reception", new[]
                {
                    "{subject} 표정을 정돈하고 {place}에서 손님을 맞았다.",
                    "{subject} 입구 쪽을 보고 {workObject} 준비했다.",
                    "{subject} 먼저 고개를 들고 {place} 응대에 나섰다.",
                    "{subject} 말투를 가다듬고 방문객을 맞기 시작했다."
                }
            },
            { "work:craft", new[]
                {
                    "{subject} 재료를 한 줄로 놓고 {place}에서 {workObject} 시작했다.",
                    "{subject} 손끝을 풀고 {workObject} 들어갔다.",
                    "{subject} 도면을 훑은 뒤 {place} 제작에 붙었다.",
                    "{subject} 재료 수를 다시 세고 {workObject} 시작했다."
                }
            },
            { "work:haul", new[]
                {
                    "{subject} 짐 무게를 가늠하고 {workObject} 시작했다.",
                    "{subject} 들 수 있는 만큼 골라 {place} 쪽으로 옮기기 시작했다.",
                    "{subject} 허리를 낮추고 물건을 챙겨 들었다.",
                    "{subject} 경로를 한번 보고 {workObject} 들어갔다."
                }
            },
            { "work:hunt", new[]
                {
                    "{subject} 발소리를 낮추고 {target} 사냥에 나섰다.",
                    "{subject} 숨을 죽인 채 {target} 뒤를 밟기 시작했다.",
                    "{subject} 거리를 재며 {target}에게 다가갔다.",
                    "{subject} 무기를 고쳐 쥐고 사냥감을 쫓았다."
                }
            },
            { "work:butcher", new[]
                {
                    "{subject} 칼날을 확인하고 {place}에서 {workObject} 시작했다.",
                    "{subject} 사체 상태를 살핀 뒤 {workObject} 들어갔다.",
                    "{subject} 작업대를 정리하고 도축에 붙었다.",
                    "{subject} 부산물 바구니를 당겨 놓고 {workObject} 시작했다."
                }
            },
            { "work:draw-water", new[]
                {
                    "{subject} 빈 물통을 들고 {place}에서 물을 긷기 시작했다.",
                    "{subject} 물통을 흔들어 보고 {workObject} 나섰다.",
                    "{subject} 줄을 당겨 보며 급수 작업에 들어갔다.",
                    "{subject} 물길을 확인하고 {workObject} 시작했다."
                }
            },
            { "work:cook", new[]
                {
                    "{subject} 재료 냄새를 맡고 {place}에서 조리를 시작했다.",
                    "{subject} 불 세기를 보고 {workObject} 들어갔다.",
                    "{subject} 손을 닦고 음식 준비에 붙었다.",
                    "{subject} 냄비 가장자리를 살피며 조리를 시작했다."
                }
            },
            { "work:treat", new[]
                {
                    "{subject} 약품을 확인하고 {place}에서 치료를 시작했다.",
                    "{subject} 상처 상태를 보고 치료 준비에 들어갔다.",
                    "{subject} 붕대를 펼치고 {workObject} 시작했다.",
                    "{subject} 손을 진정시키고 환자를 살폈다."
                }
            },
            { "work:refuel", new[]
                {
                    "{subject} 남은 불씨를 보고 {workObject} 시작했다.",
                    "{subject} 연료 더미를 살피고 {place}에 보충하러 갔다.",
                    "{subject} 꺼질 듯한 불빛을 보고 연료를 챙겼다.",
                    "{subject} 화덕 옆을 정리하고 {workObject} 들어갔다."
                }
            }
        };

    private static readonly Dictionary<string, string[]> WorkCompletedTemplates =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "work:research", new[]
                {
                    "{subject} 헷갈린 줄을 다시 짚고 {place}에서 {workObject} 마쳤다.",
                    "{subject} 마지막 메모를 눌러 쓰고 {workObject} 끝냈다.",
                    "{subject} 잠깐 딴생각하다가 {workObject} 매듭지었다.",
                    "{subject} 계산을 한 번 고쳐 {place} 연구를 마무리했다.",
                    "{subject} 묘하게 뿌듯한 얼굴로 {workObject} 끝냈다."
                }
            },
            { "work:clean", new[]
                {
                    "{subject} 놓친 얼룩을 찾아내 {place} 청소를 끝냈다.",
                    "{subject} 마지막 먼지를 털고 {workObject} 마쳤다.",
                    "{subject} 바닥을 한 번 더 훑고 {workObject} 마무리했다.",
                    "{subject} 지저분한 구석까지 밀어내고 청소를 끝냈다.",
                    "{subject} 깨끗해진 바닥을 보고 고개를 끄덕였다."
                }
            },
            { "work:repair", new[]
                {
                    "{subject} 삐걱임을 잡아내고 {place} 수리를 마쳤다.",
                    "{subject} 마지막 나사를 죄고 {workObject} 끝냈다.",
                    "{subject} 고친 곳을 두드려 보고 {workObject} 마무리했다.",
                    "{subject} 문제 난 틈을 메우고 수리를 끝냈다.",
                    "{subject} 공구를 내려놓으며 {workObject} 매듭지었다."
                }
            },
            { "work:restock", new[]
                {
                    "{subject} 빈 선반을 채우고 {place} 보충을 끝냈다.",
                    "{subject} 수량을 다시 세고 {workObject} 마쳤다.",
                    "{subject} 물건 줄을 반듯하게 맞춰 보충을 끝냈다.",
                    "{subject} 빠진 품목을 찾아 넣고 {workObject} 마무리했다.",
                    "{subject} 마지막 상자를 밀어 넣고 보충을 끝냈다."
                }
            },
            { "work:guard", new[]
                {
                    "{subject} 수상한 낌새를 넘기고 {place} 경비를 마쳤다.",
                    "{subject} 끝까지 주변을 살피고 {workObject} 끝냈다.",
                    "{subject} 한 바퀴 더 확인한 뒤 경비를 마무리했다.",
                    "{subject} 문 쪽을 마지막으로 보고 {workObject} 마쳤다.",
                    "{subject} 긴장을 풀지 않은 채 경비를 넘겼다."
                }
            },
            { "work:reception", new[]
                {
                    "{subject} 어색한 침묵을 넘기고 응대를 마쳤다.",
                    "{subject} 손님 반응을 살피며 {place} 응대를 끝냈다.",
                    "{subject} 짧게 고개를 숙이고 응대를 마무리했다.",
                    "{subject} 첫인상을 챙기고 방문객을 들여보냈다.",
                    "{subject} 말꼬리를 정리하며 응대를 끝냈다."
                }
            },
            { "work:craft", new[]
                {
                    "{subject} 삐뚤어진 부분을 고쳐 {workObject} 마쳤다.",
                    "{subject} 마지막 손질을 얹고 {place} 제작을 끝냈다.",
                    "{subject} 완성품을 확인하고 {workObject} 마무리했다.",
                    "{subject} 재료 찌꺼기를 치우며 제작을 끝냈다.",
                    "{subject} 결과물을 들어 보고 {workObject} 매듭지었다."
                }
            },
            { "work:haul", new[]
                {
                    "{subject} 짐을 내려놓고 {place} 운반을 마쳤다.",
                    "{subject} 마지막 꾸러미를 옮기고 {workObject} 끝냈다.",
                    "{subject} 무게를 견디며 물건을 목적지에 놓았다.",
                    "{subject} 흔들린 짐을 바로잡고 운반을 마무리했다.",
                    "{subject} 빈손을 털며 {workObject} 끝냈다."
                }
            },
            { "work:hunt", new[]
                {
                    "{subject} 숨을 몰아쉬며 {target} 사냥을 끝냈다.",
                    "{subject} 끝까지 거리를 좁혀 사냥감을 쓰러뜨렸다.",
                    "{subject} 마지막 빈틈을 놓치지 않고 {target}를 잡았다.",
                    "{subject} 흔들린 자세를 바로잡고 사냥을 마쳤다.",
                    "{subject} 주변을 확인한 뒤 사냥감을 확보했다."
                }
            },
            { "work:butcher", new[]
                {
                    "{subject} 부산물을 나눠 담고 {workObject} 마쳤다.",
                    "{subject} 사체 손질을 끝내고 작업대를 닦았다.",
                    "{subject} 쓸 만한 부위를 골라내 도축을 마무리했다.",
                    "{subject} 칼을 내려놓고 {place} 도축을 끝냈다.",
                    "{subject} 남은 조각을 정리하며 {workObject} 마쳤다."
                }
            },
            { "work:draw-water", new[]
                {
                    "{subject} 물통을 가득 채워 {workObject} 마쳤다.",
                    "{subject} 튄 물을 털어내고 급수를 끝냈다.",
                    "{subject} 마지막 물통을 들어 올려 급수를 마무리했다.",
                    "{subject} 물 양을 확인하고 {place}에서 돌아섰다.",
                    "{subject} 흔들리는 물통을 잡고 급수를 끝냈다."
                }
            },
            { "work:cook", new[]
                {
                    "{subject} 간을 한 번 보고 {place} 조리를 마쳤다.",
                    "{subject} 불을 낮추고 조리된 음식을 내려놓았다.",
                    "{subject} 냄비를 정리하며 {workObject} 끝냈다.",
                    "{subject} 타기 직전의 냄새를 잡고 조리를 마쳤다.",
                    "{subject} 완성된 음식을 챙겨 {workObject} 매듭지었다."
                }
            },
            { "work:treat", new[]
                {
                    "{subject} 붕대를 단단히 묶고 {workObject} 마쳤다.",
                    "{subject} 환자 상태를 다시 보고 치료를 끝냈다.",
                    "{subject} 약품을 정리하며 {place} 치료를 마무리했다.",
                    "{subject} 숨을 고른 뒤 상처 처치를 끝냈다.",
                    "{subject} 떨리는 손을 감추고 치료를 매듭지었다."
                }
            },
            { "work:refuel", new[]
                {
                    "{subject} 불빛이 살아나는 걸 보고 연료 보충을 끝냈다.",
                    "{subject} 마지막 장작을 밀어 넣고 {workObject} 마쳤다.",
                    "{subject} 연기가 잦아드는 걸 확인하고 보충을 끝냈다.",
                    "{subject} 꺼질 뻔한 불을 살려 {place}을 정리했다.",
                    "{subject} 남은 연료를 세고 보충을 마무리했다."
                }
            }
        };

    private static readonly string[] GenericStarted =
    {
        "{subject} 잠깐 주변을 보고 {place}에서 {workObject} 시작했다.",
        "{subject} 손을 한번 털고 {workObject} 들어갔다.",
        "{subject} 할 일을 확인한 뒤 {place}로 향했다.",
        "{subject} 머릿속 순서를 맞추고 {workObject} 시작했다.",
        "{subject} 조금 꾸물대다 곧바로 {workObject} 붙잡았다.",
        "{subject} 발걸음을 멈추고 {place} 일을 시작했다.",
        "{subject} 상황을 훑어본 뒤 {workObject} 들어갔다.",
        "{subject} 짧게 숨을 고르고 {workObject} 시작했다."
    };

    private static readonly string[] GenericCompleted =
    {
        "{subject} 마지막 확인을 마치고 {workObject} 끝냈다.",
        "{subject} 작은 실수를 바로잡고 {place} 일을 마쳤다.",
        "{subject} 손끝을 털며 {workObject} 마무리했다.",
        "{subject} 한 번 더 살핀 뒤 {workObject} 끝냈다.",
        "{subject} 괜히 뿌듯한 표정으로 {place} 일을 마쳤다.",
        "{subject} 흐트러진 걸 정리하고 {workObject} 매듭지었다.",
        "{subject} 잠깐 멈칫했지만 {workObject} 마쳤다.",
        "{subject} 결과를 확인하고 {place}에서 돌아섰다."
    };

    private static readonly string[] GenericProgress =
    {
        "{subject} 집중을 되찾고 {workObject} 이어갔다.",
        "{subject} 잠깐 삐끗했지만 {place} 일을 계속했다.",
        "{subject} 흐름을 놓치지 않으려 {workObject} 붙들었다.",
        "{subject} 작은 문제를 넘기며 {workObject} 이어갔다.",
        "{subject} 주변을 살핀 뒤 {place} 일을 계속했다.",
        "{subject} 손을 바삐 움직이며 {workObject} 밀어붙였다."
    };

    private static readonly string[] GenericFailed =
    {
        "{subject} 끝까지 붙잡았지만 {workObject} 실패했다.",
        "{subject} 상황을 다시 봤지만 {place} 일을 마치지 못했다.",
        "{subject} 짧게 한숨 쉬며 {workObject} 물러났다.",
        "{subject} 방법을 바꿔 봤지만 {workObject} 막혔다.",
        "{subject} 어긋난 부분을 확인하고 일을 접었다.",
        "{subject} 무리하지 않고 {place}에서 손을 뗐다."
    };

    private static readonly string[] GenericBlocked =
    {
        "{subject} 길을 살폈지만 {workObject} 막혔다.",
        "{subject} 필요한 조건을 못 찾아 {place}에서 멈췄다.",
        "{subject} 주변을 둘러보다 {workObject} 보류했다.",
        "{subject} 당장 손댈 수 없어 {place} 일을 미뤘다.",
        "{subject} 막힌 이유를 확인하고 발을 뺐다.",
        "{subject} 억지로 밀지 않고 {workObject} 멈췄다."
    };

    private static readonly string[] FacilityTemplates =
    {
        "{subject} 잠깐 망설이다 {place}을 이용했다.",
        "{subject} 몸을 기울여 {place} 상태를 살폈다.",
        "{subject} 필요한 걸 챙기고 {place}을 썼다.",
        "{subject} 익숙한 손놀림으로 {place}을 다뤘다.",
        "{subject} 주변을 확인한 뒤 {place}을 이용했다.",
        "{subject} 짧게 고개를 끄덕이고 {place}에 붙었다.",
        "{subject} 손끝으로 확인하며 {place}을 작동했다.",
        "{subject} 삐걱임을 지나쳐 {place}을 사용했다."
    };

    private static readonly string[] ShoppingTemplates =
    {
        "{subject} 물건을 한참 보다가 {target}을 골랐다.",
        "{subject} 값어치를 따져 보고 {target}을 챙겼다.",
        "{subject} 선반 앞에서 망설이다 {target}을 샀다.",
        "{subject} 눈길이 간 {target}을 결국 집어 들었다.",
        "{subject} 손에 든 물건을 보고 만족스레 끄덕였다.",
        "{subject} 계산을 마치고 {target}을 품에 안았다."
    };

    private static readonly string[] StockTemplates =
    {
        "{subject} 수량을 맞춰 {target}을 정리했다.",
        "{subject} 빠진 물품을 찾아 {target}을 채웠다.",
        "{subject} 목록을 훑으며 재고를 맞췄다.",
        "{subject} 헷갈린 품목을 다시 세어 정리했다.",
        "{subject} 남은 칸을 보고 재고를 손봤다.",
        "{subject} 물건 더미를 나눠 재고를 정돈했다."
    };

    private static readonly string[] HealthTemplates =
    {
        "{subject} 몸 상태를 살피고 잠깐 숨을 골랐다.",
        "{subject} 불편한 곳을 짚으며 상태를 확인했다.",
        "{subject} 무리하지 않으려 걸음을 늦췄다.",
        "{subject} 아픈 기색을 감추고 몸을 추슬렀다.",
        "{subject} 상태를 확인한 뒤 조심스레 움직였다.",
        "{subject} 치료 흔적을 만져 보고 다시 일어섰다."
    };

    private static readonly string[] DutyTemplates =
    {
        "{subject} 할 일을 다시 정하고 움직였다.",
        "{subject} 우선순위를 곱씹고 자리를 옮겼다.",
        "{subject} 명령을 확인한 뒤 바로 움직였다.",
        "{subject} 잠깐 멈춰 계획을 고쳐 잡았다.",
        "{subject} 방향을 바꿔 다음 일로 넘어갔다.",
        "{subject} 망설임을 접고 맡은 일을 받아들였다."
    };

    private static readonly string[] WaitTemplates =
    {
        "{subject} 잠깐 멍하니 서서 차례를 기다렸다.",
        "{subject} 주변 소리에 귀를 기울이며 기다렸다.",
        "{subject} 할 일을 찾지 못해 발끝만 움직였다.",
        "{subject} 짧게 숨을 고르며 대기했다.",
        "{subject} 눈치를 보다가 잠시 멈춰 섰다.",
        "{subject} 다음 일을 기다리며 주변을 훑었다."
    };

    private static readonly string[] SocialTemplates =
    {
        "{subject} 말을 고르다 조심스레 반응했다.",
        "{subject} 상대 표정을 보고 짧게 답했다.",
        "{subject} 어색한 틈을 넘기며 대화를 이었다.",
        "{subject} 한 박자 늦게 고개를 끄덕였다.",
        "{subject} 기분을 숨기지 못하고 반응했다.",
        "{subject} 분위기를 살피며 말을 건넸다."
    };

    private static readonly string[] LifecycleTemplates =
    {
        "{subject} 잠깐 뒤돌아본 뒤 다시 움직였다.",
        "{subject} 먼지를 털고 다음 자리로 향했다.",
        "{subject} 돌아온 숨을 고르며 안쪽으로 들어왔다.",
        "{subject} 떠날 준비를 마치고 발걸음을 옮겼다.",
        "{subject} 주변을 확인하고 조용히 이동했다.",
        "{subject} 익숙한 길을 따라 천천히 움직였다."
    };

    public static bool TryBuildLine(CharacterLog characterLog, CharacterLogEntry entry, out string line)
    {
        line = string.Empty;
        CharacterActivityEvent activity = entry.Activity;
        if (!ShouldUseTemplate(entry))
        {
            return false;
        }

        string subject = BuildSubject(characterLog, activity);
        string actionId = activity.ActionId ?? string.Empty;
        string work = ResolveWorkLabel(actionId);
        string place = ResolvePlace(activity);
        string target = ResolveTarget(activity);
        string[] templates = ResolveTemplates(activity, actionId);
        if (templates == null || templates.Length == 0)
        {
            return false;
        }

        int index = Math.Abs(StableHash(
            entry.EntryId,
            activity.KindId,
            activity.ActionId,
            activity.OutcomeId,
            activity.TargetName,
            activity.FactText)) % templates.Length;
        line = RenderTemplate(templates[index], subject, work, place, target);
        if (line.Length > MaxLineLength)
        {
            line = RenderTemplate(
                PickShortTemplate(activity, actionId, entry.EntryId),
                subject,
                work,
                place,
                target);
        }

        if (string.IsNullOrWhiteSpace(line)
            || line.Length > MaxLineLength
            || string.Equals(line.Trim(), entry.DisplayLine?.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool ShouldUseTemplate(CharacterLogEntry entry)
    {
        CharacterActivityEvent activity = entry.Activity;
        if (entry.EntryId <= 0
            || entry.Count != 1
            || activity == null
            || !activity.NarrativeEligible
            || !activity.VisibleToPlayer)
        {
            return false;
        }

        if (IsMajorNarrative(activity))
        {
            return false;
        }

        return activity.KindId == CharacterActivityKinds.Work
            || activity.KindId == CharacterActivityKinds.FacilityUse
            || activity.KindId == CharacterActivityKinds.Stock
            || activity.KindId == CharacterActivityKinds.Shopping
            || activity.KindId == CharacterActivityKinds.Health
            || activity.KindId == CharacterActivityKinds.Duty
            || activity.KindId == CharacterActivityKinds.Wait
            || activity.KindId == CharacterActivityKinds.Social
            || activity.KindId == CharacterActivityKinds.Lifecycle;
    }

    private static bool IsMajorNarrative(CharacterActivityEvent activity)
    {
        if (activity == null)
        {
            return false;
        }

        if (activity.KindId == CharacterActivityKinds.Combat)
        {
            return true;
        }

        if (activity.KindId == CharacterActivityKinds.Health
            && string.Equals(activity.OutcomeId, CharacterActivityOutcomes.Damaged, StringComparison.Ordinal))
        {
            return true;
        }

        string reason = activity.ReasonCode ?? string.Empty;
        string action = activity.ActionId ?? string.Empty;
        string fact = activity.FactText ?? string.Empty;
        return ContainsMajorKeyword(reason)
            || ContainsMajorKeyword(action)
            || ContainsMajorKeyword(fact);
    }

    private static bool ContainsMajorKeyword(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("truth", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("ultimate", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("death", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("사망", StringComparison.Ordinal) >= 0
            || value.IndexOf("보스", StringComparison.Ordinal) >= 0
            || value.IndexOf("진실", StringComparison.Ordinal) >= 0
            || value.IndexOf("궁극", StringComparison.Ordinal) >= 0;
    }

    private static string[] ResolveTemplates(CharacterActivityEvent activity, string actionId)
    {
        if (activity.KindId == CharacterActivityKinds.Work)
        {
            if (string.Equals(activity.OutcomeId, CharacterActivityOutcomes.Started, StringComparison.Ordinal))
            {
                return WorkStartedTemplates.TryGetValue(actionId, out string[] templates)
                    ? templates
                    : GenericStarted;
            }

            if (string.Equals(activity.OutcomeId, CharacterActivityOutcomes.Completed, StringComparison.Ordinal)
                || string.Equals(activity.OutcomeId, CharacterActivityOutcomes.Returned, StringComparison.Ordinal))
            {
                return WorkCompletedTemplates.TryGetValue(actionId, out string[] templates)
                    ? templates
                    : GenericCompleted;
            }

            if (string.Equals(activity.OutcomeId, CharacterActivityOutcomes.Progress, StringComparison.Ordinal)
                || string.Equals(activity.OutcomeId, CharacterActivityOutcomes.Changed, StringComparison.Ordinal))
            {
                return GenericProgress;
            }

            if (string.Equals(activity.OutcomeId, CharacterActivityOutcomes.Failed, StringComparison.Ordinal)
                || string.Equals(activity.OutcomeId, CharacterActivityOutcomes.Cancelled, StringComparison.Ordinal))
            {
                return GenericFailed;
            }

            if (string.Equals(activity.OutcomeId, CharacterActivityOutcomes.Blocked, StringComparison.Ordinal))
            {
                return GenericBlocked;
            }

            return GenericCompleted;
        }

        return activity.KindId switch
        {
            CharacterActivityKinds.FacilityUse => FacilityTemplates,
            CharacterActivityKinds.Stock => StockTemplates,
            CharacterActivityKinds.Shopping => ShoppingTemplates,
            CharacterActivityKinds.Health => HealthTemplates,
            CharacterActivityKinds.Duty => DutyTemplates,
            CharacterActivityKinds.Wait => WaitTemplates,
            CharacterActivityKinds.Social => SocialTemplates,
            CharacterActivityKinds.Lifecycle => LifecycleTemplates,
            _ => null
        };
    }

    private static string PickShortTemplate(CharacterActivityEvent activity, string actionId, int entryId)
    {
        if (activity.KindId == CharacterActivityKinds.Work
            && string.Equals(activity.OutcomeId, CharacterActivityOutcomes.Started, StringComparison.Ordinal))
        {
            return GenericStarted[Math.Abs(entryId) % GenericStarted.Length];
        }

        if (activity.KindId == CharacterActivityKinds.Work
            && (string.Equals(activity.OutcomeId, CharacterActivityOutcomes.Failed, StringComparison.Ordinal)
                || string.Equals(activity.OutcomeId, CharacterActivityOutcomes.Blocked, StringComparison.Ordinal)))
        {
            return GenericFailed[Math.Abs(entryId) % GenericFailed.Length];
        }

        return GenericCompleted[Math.Abs(entryId) % GenericCompleted.Length];
    }

    private static string RenderTemplate(
        string template,
        string subject,
        string work,
        string place,
        string target)
    {
        string safeWork = string.IsNullOrWhiteSpace(work) ? "일" : work;
        string safePlace = string.IsNullOrWhiteSpace(place) ? "그 자리" : place;
        string safeTarget = string.IsNullOrWhiteSpace(target) ? safeWork : target;
        string result = template
            .Replace("{subject}", subject)
            .Replace("{work}", safeWork)
            .Replace("{workObject}", WithObjectParticle(safeWork))
            .Replace("{place}", safePlace)
            .Replace("{target}", safeTarget)
            .Replace("{targetObject}", WithObjectParticle(safeTarget));
        result = result.Replace("  ", " ").Trim();
        if (!result.EndsWith(".", StringComparison.Ordinal)
            && !result.EndsWith("!", StringComparison.Ordinal)
            && !result.EndsWith("?", StringComparison.Ordinal))
        {
            result += ".";
        }

        return result;
    }

    private static string BuildSubject(CharacterLog characterLog, CharacterActivityEvent activity)
    {
        string name = !string.IsNullOrWhiteSpace(activity?.ActorName)
            ? activity.ActorName.Trim()
            : characterLog != null ? characterLog.name : "누군가";
        return name + SelectParticle(name, "이", "가");
    }

    private static string ResolveWorkLabel(string actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId))
        {
            return "일";
        }

        if (WorkLabels.TryGetValue(actionId, out string label))
        {
            return label;
        }

        if (WorkTypeCatalog.TryGet(actionId, out WorkTypeDefinition definition)
            && !string.IsNullOrWhiteSpace(definition.DisplayName))
        {
            return definition.DisplayName;
        }

        int index = actionId.LastIndexOf(':');
        return index >= 0 && index < actionId.Length - 1
            ? actionId.Substring(index + 1).Replace('-', ' ')
            : actionId;
    }

    private static string ResolvePlace(CharacterActivityEvent activity)
    {
        if (!string.IsNullOrWhiteSpace(activity?.PlaceName))
        {
            return activity.PlaceName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(activity?.TargetName))
        {
            return activity.TargetName.Trim();
        }

        return "그 자리";
    }

    private static string ResolveTarget(CharacterActivityEvent activity)
    {
        if (!string.IsNullOrWhiteSpace(activity?.TargetName))
        {
            return activity.TargetName.Trim();
        }

        return ResolveWorkLabel(activity?.ActionId);
    }

    private static string WithObjectParticle(string noun)
    {
        noun = string.IsNullOrWhiteSpace(noun) ? "일" : noun.Trim();
        return noun + SelectParticle(noun, "을", "를");
    }

    private static string SelectParticle(string text, string withFinalConsonant, string withoutFinalConsonant)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return withoutFinalConsonant;
        }

        char last = text.Trim()[text.Trim().Length - 1];
        if (last < 0xAC00 || last > 0xD7A3)
        {
            return withoutFinalConsonant;
        }

        int code = last - 0xAC00;
        return code % 28 == 0 ? withoutFinalConsonant : withFinalConsonant;
    }

    private static int StableHash(int entryId, params string[] values)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + entryId;
            if (values != null)
            {
                foreach (string value in values)
                {
                    if (value == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < value.Length; i++)
                    {
                        hash = hash * 31 + value[i];
                    }
                }
            }

            return hash == int.MinValue ? 0 : hash;
        }
    }
}
