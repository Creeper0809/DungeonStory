# Reachable R80 inference definitions. See recipe-provenance.json.
from copy import deepcopy
from audit_sft_token_lengths import require
from subject_staged_view import atomic_quotes
import json
from legal_binding_view import reading_messages as original_reading
from legal_binding_view import selection_messages, prose_messages, prose_native
RULE = ' 현재 호출은 공개 offer.qualifiedNegativeEvidence에 지정된 후속 경험의 상태를 읽는 단계다. currentFollowupFactIds는 그 공개 지정 범위를 그대로 옮긴 것이며 정답 문장이나 회복 판정이 아니다. 다른 facts도 맥락으로 읽되 인용은 지정된 후속 기록에서 고른다. 그 안에서도 과거 실패·준비·예약·다른 사람의 결과와 당사자의 마지막 실제 수행 결과를 구분한다. 마지막 줄이라는 이유만으로 고르지 말고 기록된 시간과 당사자·동일 대상을 직접 확인한다.'

def scope_reading(messages, schema):
    result = deepcopy(messages)
    sc = deepcopy(schema)
    doc = json.loads(result[1]['content'])
    ids = doc['offer'].get('qualifiedNegativeEvidence')
    require(isinstance(ids, list) and ids and (len(ids) == len(set(ids))) and all((isinstance(k, str) and k in doc['facts'] and (doc['facts'][k].get('selectable') is True) for k in ids)), 'explicit public follow-up scope required; never guess a fact ID')
    quotes = atomic_quotes({k: doc['facts'][k] for k in ids})
    require(set(quotes) <= set(sc['properties']['latestOwnOutcome']['enum']), 'scope must only restrict existing extractive options')
    doc['currentFollowupFactIds'] = deepcopy(ids)
    result[0]['content'] += RULE
    result[1]['content'] = json.dumps(doc, ensure_ascii=False, separators=(',', ':'))
    sc['properties']['latestOwnOutcome']['enum'] = quotes
    return (result, sc)

def reading_messages(row, index, previous):
    return scope_reading(*original_reading(row, index, previous))
