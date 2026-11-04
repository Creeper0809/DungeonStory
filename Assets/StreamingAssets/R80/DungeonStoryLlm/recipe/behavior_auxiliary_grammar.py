# Reachable R80 inference definitions. See recipe-provenance.json.
from copy import deepcopy
import general_prototype_auxiliary_grammar as previous
import hashlib, json
VERSION = 'dungeonstory-ordinary-goal-target-binding@1'

def bound_schema(schema):
    props = schema.get('properties', {})
    field = next((k for k in ('macroGoal', 'moodImpulse') if k in props), None)
    if field is None:
        return deepcopy(schema)
    assert 'enum' in props[field] and props['targetFacilityId']['type'] == 'integer'
    goals = props[field]['enum']
    branches = []
    for targeted in (False, True):
        values = [v for v in goals if (v in ('AvoidFacility', 'Vandalize')) == targeted]
        if not values:
            continue
        branch = deepcopy(schema)
        branch['properties'][field]['enum'] = values
        if not targeted:
            branch['properties']['targetFacilityId'] = {'type': 'integer', 'const': -1}
            branch['properties']['targetFacilityTag'] = {'type': 'string', 'const': ''}
        branches.append(branch)
    return {'anyOf': branches}

def native_constraint(native, *, prose=False):
    if not any((k in native.get('properties', {}) for k in ('macroGoal', 'moodImpulse'))):
        return previous.native_constraint(native, prose=prose)
    assert not prose
    derived = bound_schema(native)
    text = previous.grammar(derived)
    _, record = previous.native_constraint(native)
    record.update(decoder=VERSION, nativeGrammarSha256='sha256:' + hashlib.sha256(text.encode()).hexdigest(), conditionalTargetBindingEnforcedBeforeGeneration=True, conditionalRuleSource='AiDirectorRuntime.BuildMacroGoalPrompt/BuildMoodImpulsePrompt', grammarSchemaSha256='sha256:' + hashlib.sha256(json.dumps(derived, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode()).hexdigest())
    return (dict(grammar=text), record)
