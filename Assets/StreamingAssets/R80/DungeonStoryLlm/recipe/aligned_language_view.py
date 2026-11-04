# Reachable R80 inference definitions. See recipe-provenance.json.
from natural_prototype_prose_view import prose_messages as previous_prose_messages, prose_native
from build_aligned_language_training import project_skill, SYSTEM, FRAME, TASKS
import json
from copy import deepcopy
from audit_sft_token_lengths import canonical
BUBBLE_HEADER = 'Write one short in-character speech bubble as JSON {"line":"..."}.\nThe line must be 80 characters or fewer. Use 4 to 10 words. No narration, no markdown, no extra keys.\nDo not copy the original event text verbatim; rewrite it as a natural speech bubble.\n'

def messages_for(profile, messages):
    result = deepcopy(messages)
    if profile != 'BubbleLine':
        return result
    if any((m.get('content') == SYSTEM for m in result)):
        raise ValueError('task framing already applied')
    for message in result:
        text = message.get('content')
        if message.get('role') != 'user' or not isinstance(text, str) or (not text.startswith(BUBBLE_HEADER)):
            continue
        remainder = text[len(BUBBLE_HEADER):]
        if remainder.startswith('Write in Korean.\n'):
            remainder = remainder[len('Write in Korean.\n'):]
        match = FRAME.fullmatch(remainder)
        if match is None:
            raise ValueError('malformed producer bubble frame')
        persona, kind, outcome, reason, event = match.groups()
        packet = dict(instructions=TASKS[0], profile='BubbleLine', publicFacts=dict(persona=persona, eventKind=kind, outcome=outcome, reasonCode=reason, sourceEvent=event))
        message['content'] = canonical(packet).decode()
    result.insert(0, dict(role='system', content=SYSTEM))
    return result

def prose_messages(row, index, decision):
    messages, schema = previous_prose_messages(row, index, decision)
    if row['profile'] == 'CharacterSkillModuleSelection':
        messages = project_skill(messages)
        assert json.loads(messages[1]['content'])['selection'] == decision
    return (messages, schema)
