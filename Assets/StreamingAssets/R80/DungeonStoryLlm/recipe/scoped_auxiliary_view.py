# Reachable R80 inference definitions. See recipe-provenance.json.
from focused_auxiliary_view import prose_messages, prose_native
from copy import deepcopy
import focused_auxiliary_view as previous
from build_aligned_language_training import SYSTEM
import json
from aligned_language_view import BUBBLE_HEADER
from audit_sft_token_lengths import canonical, require
INSTRUCTIONS = {'AcquiredTraitLegacyV2': 'description은 selectedTrait의 이점과 부담을 모두 설명한다. narrativeReason은 sourceEvent에서 그 변화가 생긴 이유만 쓴다. 성향이나 문화 문장으로 효과 설명을 대신하지 않는다.', 'CharacterSkillLegacyV2': 'description은 selectedEffect의 대상, 효과와 부담을 그대로 설명한다. narrativeReason은 sourceEvent에 근거한다. 새로운 능력이나 수치는 만들지 않는다.', 'EvolutionHistoryLegacyV2': 'description은 selectedEffect를 설명하고 historyReason은 sourceEvent를 기록한다. 새 효과나 사건은 만들지 않는다.', 'FacilityEvolutionLegacyV2': 'flavorText는 selectedEffect의 이점과 부담을 모두 설명한다. reasons는 sourceEvent에만 근거한다.', 'MacroGoal': 'currentState의 욕구 값은 낮을수록 부족하고 높을수록 충족된 상태다. 현재 부족한 욕구에 맞는 목표를 고르고 그 이유를 쓴다. 시설을 지정할 때는 시설의 실제 서비스와 선택한 목표가 맞아야 한다. 구체 시설을 지정하지 않으면 targetFacilityId=-1, targetFacilityTag는 빈 문자열이다. validSeconds는 판단의 유효기간이며 능력의 지속 시간이 아니다.', 'MoodImpulse': 'currentState의 욕구 값은 낮을수록 부족하고 높을수록 충족된 상태다. 현재 욕구에 맞는 가벼운 충동과 이유를 쓴다. 시설을 지정할 때는 시설의 실제 서비스와 선택한 충동이 맞아야 한다. 구체 시설을 지정하지 않으면 targetFacilityId=-1, targetFacilityTag는 빈 문자열이다. validSeconds는 판단의 유효기간이며 능력의 지속 시간이 아니다.', 'SocialRumor': '제공된 sourceEvent와 시설의 observed 경험만 소문으로 표현한다. 행동, 효과나 미래 결과를 덧붙이지 않는다. targetType에 해당하는 대상만 채우고 다른 종류의 대상 필드는 비워 둔다.', 'CharacterRecord': 'sourceEvent의 실제 행동과 결과를 주어와 조사가 있는 한 문장으로 기록한다. 다른 행동, 보상이나 대화를 추가하지 않는다.', 'MultiPerspective': '같은 sourceEvent를 지정된 각 인물의 관점으로 표현한다. 관점 ID를 지키며 다른 인물이 하지 않은 행동이나 대화를 추가하지 않는다.', 'Persona': 'personaName은 실제 인물 이름이 아니라 공개된 행동 성향을 나타내는 별칭이다. 제공된 성향과 문화만 간결하게 표현한다.', 'BubbleLine': 'sourceEvent 직후 인물이 직접 말할 짧고 자연스러운 한국어 대사 한 줄을 쓴다. 내레이션이나 성향 소개는 쓰지 않는다.'}

def project_packet(profile, packet):
    if profile in previous.DISPLAY:
        return previous.project_packet(profile, packet)
    require(packet.get('profile') == profile and isinstance(packet.get('publicFacts'), dict), 'typed caller packet')
    result = deepcopy(packet)
    facts = result['publicFacts']
    identity = {'characterName', 'characterId', 'fictional'}
    if profile in previous.TRAIT | previous.EFFECT:
        keep = identity | {'sourceEvent', 'selectedTrait' if profile in previous.TRAIT else 'selectedEffect', 'evidenceIds', 'allowedCandidates'}
    elif profile in ('MacroGoal', 'MoodImpulse'):
        keep = identity | {'currentState', 'facility'}
        if isinstance(facts.get('facility'), dict):
            facts['facility'] = {k: v for k, v in facts['facility'].items() if k != 'observed'}
    elif profile == 'SocialRumor':
        keep = identity | {'sourceEvent', 'facility'}
    elif profile == 'BubbleLine':
        keep = identity | {'sourceEvent', 'persona', 'eventKind', 'outcome', 'reasonCode'}
    else:
        keep = identity | {'sourceEvent', 'publicCharacterFacts', 'publicCultureMotifs'}
    result['publicFacts'] = {k: v for k, v in facts.items() if k in keep}
    if profile in INSTRUCTIONS:
        result['instructions'] = result.get('instructions', '') + '\n' + INSTRUCTIONS[profile]
    require(result.get('publicBindings') == packet.get('publicBindings') and result.get('responseSchema') == packet.get('responseSchema'), 'original caller bindings/schema exact')
    return result

def messages_for(profile, messages):
    if profile == 'BubbleLine':
        if any((m.get('role') == 'user' and m.get('content', '').startswith(BUBBLE_HEADER) for m in messages)):
            return previous.messages_for(profile, messages)
        if any((m.get('content') == SYSTEM for m in messages)):
            return deepcopy(messages)
        result = deepcopy(messages)
        if not any((m.get('content') == SYSTEM for m in result)):
            result.insert(0, dict(role='system', content=SYSTEM))
    else:
        result = deepcopy(messages)
    for message in result:
        if message.get('role') != 'user':
            continue
        try:
            packet = json.loads(message.get('content', ''))
        except (ValueError, TypeError):
            continue
        if isinstance(packet, dict) and packet.get('profile') == profile and ('publicFacts' in packet):
            message['content'] = canonical(project_packet(profile, packet)).decode()
    return result
