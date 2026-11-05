# Reachable R80 inference definitions. See recipe-provenance.json.
from outcome_staged_view import prose_messages as original_prose_messages
import json
from outcome_staged_view import prose_native

def prose_messages(row, index, selection):
    messages, schema = original_prose_messages(row, index, selection)
    doc = json.loads(messages[1]['content'])
    doc.pop('citedOriginalFacts')
    doc['futureTargetLabel'] = {'Self': '자신', 'Ally': '아군', 'Enemy': '적'}.get(doc['futureTarget'])
    doc['pastEventsAreNotThisSubtask'] = True
    messages[0]['content'] += ' 이 호출은 경험을 재판단하거나 과거를 요약하는 단계가 아니다. 제공된 실제 선택과 selectedModules의 미래 효과만 설명한다. 이름은 첫 이로운 효과를 중심으로 짓는다. 단점이 없다는 안내나 설명 생략 안내는 쓰지 않는다. 수리/청소/생산 등은 그 효과와 방향을 명시하고 시범체 같은 사건 속 사물을 효과로 바꾸지 않는다.'
    messages[1]['content'] = json.dumps(doc, ensure_ascii=False, separators=(',', ':'))
    return (messages, schema)
