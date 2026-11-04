# Reachable R80 inference definitions. See recipe-provenance.json.
from aligned_language_view import prose_messages, prose_native, messages_for as prior_messages_for
from copy import deepcopy
import json
from audit_sft_token_lengths import canonical, require
DISPLAY = frozenset(('AcquiredTrait', 'CharacterSkill', 'EvolutionHistory', 'FacilityEvolution'))
NARRATIVE = frozenset(('CharacterRecord', 'Persona', 'MultiPerspective'))
BEHAVIOR = frozenset(('MacroGoal', 'MoodImpulse', 'SocialRumor'))
TRAIT = frozenset(('AcquiredTraitLegacyV2',))
EFFECT = frozenset(('CharacterSkillLegacyV2', 'EquipmentChoiceLegacyV2', 'EvolutionHistoryLegacyV2', 'FacilityEvolutionLegacyV2'))
PROFILES = DISPLAY | NARRATIVE | BEHAVIOR | TRAIT | EFFECT

def project_packet(profile, packet):
    require(packet.get('profile') == profile and isinstance(packet.get('publicFacts'), dict), 'explicit typed task packet required')
    result = deepcopy(packet)
    facts = result['publicFacts']
    if profile in DISPLAY:
        require(isinstance(facts.get('alreadySelectedEffect'), str) and facts['alreadySelectedEffect'], 'display task requires caller-committed effect')
        keep = {'alreadySelectedEffect', 'characterId', 'characterName', 'fictional'}
        result['publicFacts'] = {k: v for k, v in facts.items() if k in keep}
    elif profile in NARRATIVE:
        for key in ('alreadySelectedEffect', 'selectedEffect', 'selectedTrait', 'currentState', 'facility'):
            facts.pop(key, None)
    elif profile in BEHAVIOR:
        for key in ('alreadySelectedEffect', 'selectedEffect', 'selectedTrait'):
            facts.pop(key, None)
    elif profile in TRAIT | EFFECT:
        irrelevant = 'selectedEffect' if profile in TRAIT else 'selectedTrait'
        for key in (irrelevant, 'currentState', 'facility'):
            facts.pop(key, None)
    else:
        raise ValueError('unsupported focused profile: ' + profile)
    require(result.get('publicBindings') == packet.get('publicBindings') and result.get('responseSchema') == packet.get('responseSchema'), 'caller contract must remain exact')
    return result

def messages_for(profile, messages):
    if profile == 'BubbleLine':
        return prior_messages_for(profile, messages)
    result = deepcopy(messages)
    if profile not in PROFILES:
        return result
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
