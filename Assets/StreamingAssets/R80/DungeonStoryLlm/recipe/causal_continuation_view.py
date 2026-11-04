# Reachable R80 inference definitions. See recipe-provenance.json.
from temporal_staged_view import reading_messages, prose_native
from temporal_staged_view import selection_messages as old_selection
from temporal_staged_view import prose_messages as old_prose
import json
HISTORY_RULE = ' 이전 후보의 선택은 모델의 이전 답이지 새로운 사건 기록이 아니다. 이전 후보의 효과나 이름을 현재 후보의 경험 근거로 재사용하지 않는다. 현재 facts에서 각 이로운 효과를 뒷받침하는 직접 관련 근거만 인용하며, 여러 이로운 효과를 고르면 그 효과들 각각의 근거를 빠뜨리지 않는다. 중복 금지 규칙은 유지하되 중복을 피하려고 타인의 효과나 무관한 근거를 고르지 않는다.'
POLARITY_RULE = ' selectedPositiveEffects는 이로운 모듈 자체의 효과와 내재된 부담이다. selectedSeparateDrawbacks는 그와 별도로 실제 선택한 단점이다. 한 이로운 모듈에 추가 부담이 없다는 문구는 별도 단점이 없다는 뜻이 아니다. 두 목록의 실제 선택을 모두 설명하되 선택되지 않은 부담을 새로 덧붙이지 않는다.'

def selection_projection(messages):
    from copy import deepcopy
    result = deepcopy(messages)
    result[0]['content'] += HISTORY_RULE
    return result

def prose_projection(messages):
    from copy import deepcopy
    result = deepcopy(messages)
    doc = json.loads(result[1]['content'])
    selection = doc['selection']
    modules = doc['selectedModules']
    doc['selectedPositiveEffects'] = {k: deepcopy(modules[k]) for k in selection['positiveModuleIds']}
    doc['selectedSeparateDrawbacks'] = {k: deepcopy(modules[k]) for k in selection['drawbackModuleIds']}
    assert all((v['polarity'] == 'positive' for v in doc['selectedPositiveEffects'].values()))
    assert all((v['polarity'] == 'drawback' for v in doc['selectedSeparateDrawbacks'].values()))
    result[0]['content'] += POLARITY_RULE
    result[1]['content'] = json.dumps(doc, ensure_ascii=False, separators=(',', ':'))
    return result

def selection_messages(row, index, previous, reading):
    messages, schema = old_selection(row, index, previous, reading)
    return (selection_projection(messages), schema)

def prose_messages(row, index, selection):
    messages, schema = old_prose(row, index, selection)
    return (prose_projection(messages), schema)
