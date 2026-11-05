# Reachable R80 inference definitions. See recipe-provenance.json.
import needs_auxiliary_view as previous
import json
from audit_sft_token_lengths import canonical
PRIORITY = '충족도 값들을 비교해 가장 낮은, 즉 가장 부족한 욕구를 우선한다. 다른 욕구가 훨씬 부족한데 이미 충분히 채워진 욕구를 더 채우는 목표를 고르지 않는다. availableTargetsOnlyForAvoidFacilityOrVandalize는 특수 목표의 장소 후보이며 일반 목표를 정하는 이유가 아니다.'

def messages_for(profile, messages):
    result = previous.messages_for(profile, messages)
    if profile not in previous.PROFILES:
        return result
    for message in result:
        if message.get('role') != 'user':
            continue
        try:
            packet = json.loads(message.get('content', ''))
        except (ValueError, TypeError):
            result.insert(0, dict(role='system', content=PRIORITY))
            return result
        if not isinstance(packet, dict) or packet.get('profile') != profile or 'publicFacts' not in packet:
            continue
        facts = packet['publicFacts']
        if 'facility' in facts:
            packet['availableTargetsOnlyForAvoidFacilityOrVandalize'] = [facts.pop('facility')]
        packet['instructions'] = packet.get('instructions', '') + '\n' + PRIORITY
        message['content'] = canonical(packet).decode()
    return result
