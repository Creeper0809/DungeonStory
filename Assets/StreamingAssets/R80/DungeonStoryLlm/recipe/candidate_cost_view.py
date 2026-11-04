# Reachable R80 inference definitions. See recipe-provenance.json.
from candidate_task_view import candidate_messages, candidate_schema, contract
from audit_sft_token_lengths import require
import json
from copy import deepcopy

def messages(row, index, previous):
    full, _ = candidate_messages(row, index, previous)
    doc = json.loads(full[1]['content'])
    offer = doc['currentOffer']
    allowed = offer['drawback']
    require(allowed, 'no model decision is needed for an offer with no legal costs')
    value = dict(task='READ_CURRENT_BURDEN_FOR_THIS_OFFER_ONLY', definitionOwner=doc['definitionOwner'], characterName=doc['characterName'], selectionId=offer['selectionId'], originalRequestRef=doc['originalRequestRef'], background=doc['background'], originalPlainHeaders=doc['remainingPlainHeaders'], offer={k: deepcopy(v) for k, v in offer.items() if k not in ('positive', 'maximumPositive', 'primaryRule')}, costModules={key: deepcopy(doc['modules'][key]) for key in allowed}, facts=deepcopy(doc['facts']))
    require(value['facts'] == contract(row)['facts'], 'cost subtask changed original facts')
    system = '게임이 허용한 단점 후보 중 지금 이 대상에게 남은 부담에 맞는 것만 선택한다. 과거에 나쁜 일이 있었다는 사실만으로 현재 단점을 고르지 않는다. 같은 당사자·같은 대상의 후속 수행 결과까지 읽는다. 준비·휴식만 했거나 남의 상태가 좋아진 것을 본인의 회복으로 바꾸지 않는다. 단점 효과는 원문 costModules를 따른다. 사실에 없는 상태를 만들지 않는다. 이 호출은 부담 선택만 답한다. 이름·효과·설명·코드·추론문을 출력하지 않는다. 형식은 drawbackModuleIds 배열 하나의 JSON 객체다. 안 맞으면 빈 배열을 선택한다.'
    field = candidate_schema(row, index)['properties']['drawbackModuleIds']
    schema = dict(type='object', properties=dict(drawbackModuleIds=deepcopy(field)), required=['drawbackModuleIds'], additionalProperties=False)
    return ([dict(role='system', content=system), dict(role='user', content=json.dumps(value, ensure_ascii=False, separators=(',', ':')))], schema)
