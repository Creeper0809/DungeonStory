# Reachable R80 inference definitions. See recipe-provenance.json.
import single_decision_view as original
from practical_prototype_prose_view import SKILL, rewrite_messages as summary_messages
from copy import deepcopy
LANGUAGE = '주어진 효과는 원문에 적힌 쉬운 한국어 용어로 설명한다. 이름과 설명은 문법에 맞게 끝내고 명사와 조사를 빠뜨리지 않는다.'

def rewrite_messages(messages, profile, *, already_summary=False):
    result = deepcopy(messages) if already_summary else summary_messages(messages, profile)
    if profile == SKILL:
        system = [m for m in result if m['role'] == 'system']
        if len(system) != 1 or LANGUAGE in system[0]['content']:
            raise ValueError('exact one unmodified prose task instruction required')
        system[0]['content'] += ' ' + LANGUAGE
    return result

def prose_messages(row, index, decision):
    messages, schema = original.prose_messages(row, index, decision)
    return (rewrite_messages(messages, row['profile']), schema)

def prose_native(row, native):
    return original.prose_native(row, native)
