# Reachable R80 inference definitions. See recipe-provenance.json.
from candidate_task_view import candidate_messages, candidate_schema
from audit_sft_token_lengths import require
from copy import deepcopy
import json
from jsonschema import Draft7Validator

def final_messages(row, index, previous, reading):
    messages, schema = candidate_messages(row, index, previous)
    doc = json.loads(messages[1]['content'])
    require(isinstance(reading, dict), 'actual earlier model reading required')
    legal = candidate_schema(row, index)['properties']['drawbackModuleIds']
    Draft7Validator(legal).validate(reading['drawbackModuleIds'])
    doc['earlierActualModelDecision'] = deepcopy(reading)
    messages[0]['content'] += '\n앞 단계에서 네가 선택한 drawbackModuleIds는 이 후보의 확정된 선택이다. 선택을 바꾸지 말고 그 선택과 허용 효과에 맞는 짧은 서술을 완성한다. 고른 이로운 효과에 내재된 장단점과 별도로 고른 단점을 모두 정확히 설명한다. 기술 서술은 캐릭터 이름을 주어로 하는 한 문장으로 48자 안에 마침표로 끝낸다. 효과와 대상을 먼저 담고 서론이나 반복 문장을 쓰지 않는다.'
    messages[1]['content'] = json.dumps(doc, ensure_ascii=False, separators=(',', ':'))
    schema['properties']['drawbackModuleIds'] = dict(type='array', enum=[deepcopy(reading['drawbackModuleIds'])])
    return (messages, schema)
