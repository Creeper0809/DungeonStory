# Reachable R80 inference definitions. See recipe-provenance.json.
from causal_continuation_view import reading_messages, prose_messages, prose_native
from causal_continuation_view import selection_messages as old_selection
from copy import deepcopy
import json
BINDING_RULE = ' 이번 응답은 이로운 효과마다 근거를 묶는 형식이다. benefitEvidence의 첫 moduleId가 주효과이고 나머지는 부효과다. 각 moduleId에 그 효과를 직접 뒷받침하는 본인의 행동·실제 결과 근거만 연결한다. 한 효과의 근거를 다른 효과의 근거로 대신하거나 모든 근거를 채우지 않는다. 다른 사람의 행동·다른 대상·배경은 본인이 한 일로 바꾸지 않는다. outcomeEvidenceFactIds는 공개 요청이 요구한 부정 경험·후속 결과의 별도 근거다. 회복되어 단점을 고르지 않았어도 공개 요청이 요구하면 그 후속 근거를 인용한다. drawbackModuleIds는 앞선 실제 모델 판단을 그대로 유지한다. 마지막 decision에는 네가 확정한 원래 공개 선택을 직접 쓴다. decision의 이로운 효과와 근거는 앞서 묶은 값과 일치해야 한다. 호출자는 decision을 그대로 사용하고, 불일치는 고치지 않고 실패로 기록한다.'

def project_selection(messages, flat_schema):
    result = deepcopy(messages)
    doc = json.loads(result[1]['content'])
    offer = doc['currentOffer']
    props = flat_schema['properties']
    positive = props['positiveModuleIds']
    evidence = deepcopy(props['evidenceFactIds'])
    evidence['minItems'] = 1
    bound = dict(type='object', properties=dict(moduleId=deepcopy(positive['items']), evidenceFactIds=evidence), required=['moduleId', 'evidenceFactIds'], additionalProperties=False)
    follow = list(offer.get('qualifiedNegativeEvidence', []))
    outcome = dict(type='array', items=dict(type='string', enum=follow), minItems=0, maxItems=len(follow), uniqueItems=True) if follow else dict(type='array', items=dict(type='string'), minItems=0, maxItems=0)
    schema = dict(type='object', properties=dict(selectionId=deepcopy(props['selectionId']), benefitEvidence=dict(type='array', items=bound, minItems=positive.get('minItems', 1), maxItems=positive['maxItems']), drawbackModuleIds=deepcopy(props['drawbackModuleIds']), outcomeEvidenceFactIds=outcome, decision=deepcopy(flat_schema)), required=['selectionId', 'benefitEvidence', 'drawbackModuleIds', 'outcomeEvidenceFactIds', 'decision'], additionalProperties=False)
    result[0]['content'] += BINDING_RULE
    doc['responseBindingFormat'] = 'MODEL_SELECTS_EACH_EFFECT_AND_ITS_OWN_PUBLIC_EVIDENCE'
    result[1]['content'] = json.dumps(doc, ensure_ascii=False, separators=(',', ':'))
    return (result, schema)

def selection_messages(row, index, previous, reading):
    messages, schema = old_selection(row, index, previous, reading)
    return project_selection(messages, schema)
