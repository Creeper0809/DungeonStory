# Reachable R80 inference definitions. See recipe-provenance.json.
from copy import deepcopy
from audit_sft_token_lengths import require, canonical
import scoped_auxiliary_view as previous
import json, re
PROFILES = frozenset(('MacroGoal', 'MoodImpulse'))
LABELS = {'hunger': '포만감', 'fun': '즐거움 충족도', 'sleep': '수면 충족도', 'excretion': '배설 충족도', 'hygiene': '위생 충족도', 'mood': '기분'}
INSTRUCTION = 'currentNeedSatisfaction은 충족도다. 각 값은 0이면 고갈되고 100이면 충분히 채워진 상태다. 포만감이 높다는 것은 배가 부르다는 뜻이다. 현재 부족한 욕구에 맞는 목표나 충동을 모델이 고르고 reason에 현재 이유를 쓴다. 일반 목표나 충동은 targetFacilityId=-1과 targetFacilityTag=""를 쓴다. AvoidFacility나 Vandalize일 때만 제공된 구체 시설을 지정할 수 있다. validSeconds는 이 판단의 유효기간이며 능력 지속 시간이 아니다. 일반 목표는 30~120초 범위로 정한다. 모델이 새 효과나 결과를 만들지 않는다.'

def messages_for(profile, messages):
    if profile not in PROFILES:
        return previous.messages_for(profile, messages)
    result = deepcopy(messages)
    plaintext = False
    for message in result:
        if message.get('role') != 'user':
            continue
        try:
            packet = json.loads(message.get('content', ''))
        except (ValueError, TypeError):
            message['content'] = re.sub('(?m)^(hunger|fun|sleep|excretion|hygiene|mood):[ \\t]*(-?\\d+(?:\\.\\d+)?)[ \\t]*$', lambda match: LABELS[match[1]] + ' (충족도 0~100): ' + match[2], message.get('content', ''))
            plaintext = True
            continue
        if not isinstance(packet, dict) or packet.get('profile') != profile or 'publicFacts' not in packet:
            continue
        projected = previous.project_packet(profile, packet)
        facts = projected['publicFacts']
        state = facts.pop('currentState')
        require(isinstance(state, dict), 'typed current need values required')
        facts['currentNeedSatisfaction'] = {LABELS.get(k, k): v for k, v in state.items()}
        require(list(facts['currentNeedSatisfaction'].values()) == list(state.values()), 'need values unchanged')
        projected['instructions'] = packet.get('instructions', '') + '\n' + INSTRUCTION
        require(projected.get('publicBindings') == packet.get('publicBindings') and projected.get('responseSchema') == packet.get('responseSchema'), 'caller contracts exact')
        message['content'] = canonical(projected).decode()
    if plaintext:
        result.insert(0, dict(role='system', content=INSTRUCTION))
    return result
