# Reachable R80 inference definitions. See recipe-provenance.json.
import readable_prose_grammar as previous
import prose_bounds_grammar as bounds
import re, hashlib, json
VERSION = 'dungeonstory-readable-numeric-free-prototype@2'
FORBIDDEN = re.compile('[0-9０-９%％]')

def prose_grammar(native):
    grammar = previous.prose_grammar(native)
    narrative = native['properties']['narrativeFlavor']
    if 'pattern' in narrative:
        subject, _, _ = bounds.fixed_pattern(narrative['pattern'])
        if FORBIDDEN.search(subject):
            raise ValueError('fixed subject violates original numeric prose rule')
    result = []
    for line in grammar.splitlines():
        if line.startswith(('json-char ::=', 'prose-char ::=')):
            head, rule = line.split(' ::= ', 1)
            marker = '\\uDFFF'
            if rule.count(marker) != 1:
                raise ValueError('original canonical character rule drift')
            line = head + ' ::= ' + rule.replace(marker, marker + '0-9０-９%％', 1)
        result.append(line)
    return '\n'.join(result) + '\n'

def native_constraint(native, *, prose):
    if not prose:
        return previous.native_constraint(native, prose=False)
    grammar = prose_grammar(native)
    return (dict(grammar=grammar), dict(schema='dungeonstory-native-format-constraint@1', nativeSchemaSha256='sha256:' + hashlib.sha256(json.dumps(native, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode()).hexdigest(), decoder=VERSION, nativeGrammarSha256='sha256:' + hashlib.sha256(grammar.encode()).hexdigest(), outputRepair=False, semanticChoicesComputedByCode=False))
