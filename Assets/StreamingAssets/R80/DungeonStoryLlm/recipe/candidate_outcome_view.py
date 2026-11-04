# Reachable R80 inference definitions. See recipe-provenance.json.
from candidate_cost_view import messages as cost_messages
import json
from copy import deepcopy
VERSION = 'MODEL_EXTRACTS_OWN_LATEST_OUTCOME_THEN_COST_V1'

def messages(row, index, previous):
    m, s = cost_messages(row, index, previous)
    doc = json.loads(m[1]['content'])
    doc['task'] = VERSION
    s['properties'] = {'latestOwnOutcome': dict(type='string', minLength=1, maxLength=600), 'drawbackModuleIds': deepcopy(s['properties']['drawbackModuleIds'])}
    s['required'] = ['latestOwnOutcome', 'drawbackModuleIds']
    m[0]['content'] = '현재 후보의 단점 판단 전에 해당 당사자·해당 대상의 마지막 실제 수행 결과를 읽는다. latestOwnOutcome에는 그 결과를 보여주는 원문 문장을 그대로 인용한다. 준비·계획·남의 성공이 아니라 본인의 마지막 실제 결과다. 예전의 실패와 마지막 결과를 혼동하지 않는다. 마지막 결과에도 부담이 남으면 해당 단점을 고르고, 실제 수행에서 그 부담이 사라졌음이 확인되면 빈 배열을 고른다. 현재 실패가 있으면 준비를 했다는 이유만으로 회복이라고 하지 않는다. 원문 인용과 drawbackModuleIds만 순서대로 JSON으로 작성한다. 상태 판단은 모델 자신이 한다.'
    m[1]['content'] = json.dumps(doc, ensure_ascii=False, separators=(',', ':'))
    return (m, s)
