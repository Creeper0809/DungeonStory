# Reachable R80 inference definitions. See recipe-provenance.json.
import prose_bounds_grammar as original
import hashlib, json
VERSION = 'dungeonstory-readable-prototype-format@1'

def prose_grammar(native):
    if 'pattern' in native['properties']['narrativeFlavor']:
        return original.prose_grammar(native)
    if set(native) != {'type', 'properties', 'required', 'additionalProperties'} or native['type'] != 'object' or native['additionalProperties'] is not False or (set(native['properties']) != set(native['required'])) or (set(native['properties']) != {'displayName', 'narrativeFlavor'}):
        raise ValueError('exact two-field public prose schema required')
    lo, hi = original.bounds(native['properties']['displayName'], patterned=False)
    nlo, nhi = original.bounds(native['properties']['narrativeFlavor'], patterned=False)
    quote = original.literal('"')
    root = original.literal('{"displayName":') + ' display-name ' + original.literal(',"narrativeFlavor":') + ' narrative ' + original.literal('}')
    return '\n'.join(('root ::= ' + root, 'display-name ::= ' + quote + f' json-char{{{lo},{hi}}} ' + quote, 'narrative ::= ' + quote + f' json-char{{{nlo},{nhi}}} ' + quote, 'json-char ::= ' + original.atom_rule(frozenset()))) + '\n'

def native_constraint(native, *, prose):
    if not prose:
        return original.native_constraint(native, prose=False)
    grammar = prose_grammar(native)
    record = dict(schema='dungeonstory-native-format-constraint@1', nativeSchemaSha256='sha256:' + hashlib.sha256(json.dumps(native, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode()).hexdigest(), decoder=VERSION, nativeGrammarSha256='sha256:' + hashlib.sha256(grammar.encode()).hexdigest(), outputRepair=False, semanticChoicesComputedByCode=False)
    return (dict(grammar=grammar), record)
