# Reachable R80 inference definitions. See recipe-provenance.json.
from copy import deepcopy
from audit_sft_token_lengths import require, canonical
import json
import re
TASKS = ('sourceEvent 직후 인물이 할 짧고 자연스러운 한국어 대사 한 문장을 쓴다. 내레이션과 사건 원문 복사는 하지 않는다.', '주어진 sourceEvent를 겪은 인물의 짧은 한국어 반응을 쓴다. 서술자의 설명 대신 인물이 직접 말하게 한다.', 'sourceEvent 다음에 인물이 직접 할 자연스러운 한국어 한마디를 쓴다. 사건을 새로 만들거나 반대로 설명하지 않는다.', '인물이 sourceEvent 직후 할 짧은 한국어 대사를 쓴다. 입력에 없는 사물이나 행동을 추가하지 않는다.')
SYSTEM = '게임 속 인물의 짧은 한국어 대사를 JSON으로 쓴다. 입력의 사건과 결과를 바꾸지 않고 문장을 자연스럽게 끝낸다. 80자 이내의 4~10어절로 쓰며 원문을 그대로 복사하지 않는다. 설명문이나 내레이션은 쓰지 않는다. 요청한 JSON만 반환한다.'
SCOPE = ' 이번 짧은 문장에는 selectedModules에 있는 주효과와 실제 별도 단점만 쓴다. 별도 단점이 선택되어 있으면 그 불리한 효과도 반드시 같은 문장에 쓴다. 다른 부효과를 덧붙이다 문장을 중간에 끝내지 않는다.'
FRAME = re.compile('persona: ([^\\r\\n]*)\\neventKind: ([^\\r\\n]*)\\noutcome: ([^\\r\\n]*)\\nreasonCode: ([^\\r\\n]*)\\nevent: ([\\s\\S]*)\\Z')

def project_skill(messages):
    result = deepcopy(messages)
    doc = json.loads(result[1]['content'])
    selection = deepcopy(doc['selection'])
    primary = selection['positiveModuleIds'][0]
    shown = [primary] + selection['drawbackModuleIds']
    modules = {k: deepcopy(doc['selectedModules'][k]) for k in shown}
    require(doc['futureTarget'] in ('Self', 'Ally', 'Enemy'), 'typed original recipient')
    doc = dict(task='NARRATE_ACTUAL_SELECTED_EFFECTS_ONLY', profile=doc['profile'], characterName=doc['characterName'], explicitPublicSubject=dict(character=doc['characterName']), selection=selection, futureTarget=doc['futureTarget'], futureTargetLabel={'Self': '자신', 'Ally': '아군', 'Enemy': '적'}[doc['futureTarget']], mechanics=doc['mechanics'], selectedModules=modules, selectedPositiveEffects={primary: deepcopy(modules[primary])}, selectedSeparateDrawbacks={k: deepcopy(modules[k]) for k in selection['drawbackModuleIds']}, statTermGlossary=deepcopy(doc['statTermGlossary']), pastEventsAreNotThisSubtask=True)
    doc['descriptionScope'] = dict(primaryModuleId=primary, actualSeparateDrawbackIds=deepcopy(selection['drawbackModuleIds']), secondaryEffectsRemainInActualDecision=True)
    result[1]['content'] = canonical(doc).decode()
    result[0]['content'] += SCOPE
    require(doc['selection'] == selection, 'original model-owned decision unchanged')
    return result
