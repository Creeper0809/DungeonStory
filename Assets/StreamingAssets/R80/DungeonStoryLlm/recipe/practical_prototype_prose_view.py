# Reachable R80 inference definitions. See recipe-provenance.json.
from copy import deepcopy
from readable_prose_view import OLD as ORIGINAL_STYLE, NEW as R74_STYLE
SKILL = 'CharacterSkillModuleSelection'
STYLE = '기술은 인물 이름을 주어로 하는 완결된 한 문장으로 쓰고 48자 이내다. 주효과의 실제 수신 대상과 발동 조건, 별도로 선택한 단점은 정확히 쓴다. 부효과는 짧은 설명에서 생략해도 선택 목록에서 없어지지 않는다.'
FULL = 'selectedModules의 모든 이로운 효과' + chr(183) + '내재된 부담' + chr(183) + '별도 단점을 빠뜨리거나 반대로 설명하지 않는다.'
SUMMARY = 'selectedModules의 첫 이로운 효과와 그 내재된 부담, 별도 단점은 반대로 설명하지 않는다. 부효과를 모두 열거할 필요는 없다.'
POLARITY = '두 목록의 실제 선택을 모두 설명하되 선택되지 않은 부담을 새로 덧붙이지 않는다.'
PRIMARY = '첫 이로운 효과와 실제 선택한 별도 단점을 짧게 설명하고 선택하지 않은 효과나 부담은 덧붙이지 않는다.'

def rewrite_messages(messages, profile):
    result = deepcopy(messages)
    if profile == SKILL:
        system = [m for m in result if m['role'] == 'system']
        if len(system) != 1:
            raise ValueError('one original prose system message required')
        m = system[0]
        styles = [s for s in (ORIGINAL_STYLE, R74_STYLE) if s in m['content']]
        if len(styles) != 1 or m['content'].count(FULL) != 1 or m['content'].count(POLARITY) != 1:
            raise ValueError('exact original prose clauses required')
        m['content'] = m['content'].replace(styles[0], STYLE).replace(FULL, SUMMARY).replace(POLARITY, PRIMARY)
    return result
