# Reachable R80 inference definitions. See recipe-provenance.json.
import importlib.util
from pathlib import Path
import hashlib
from copy import deepcopy
from prose_bounds_grammar import literal, fragment
import json
PIN = 'sha256:6eedcbad62ba45438e7e1efad4749f297f218f0b8989e7128edd071e4784b0b0'
VERSION = 'dungeonstory-general-public-string-intersection@1'

def converter():
    here = Path(__file__).resolve().parent
    path = here / 'r72_aux_json_schema_to_grammar.py'
    if not path.is_file():
        path = here.parents[1] / 'Artifacts/Working/DistilledNarrative-20261018-b205-r1/native-prose-bounds-r56/cpu-native-r1/source/examples/json_schema_to_grammar.py'
    assert 'sha256:' + hashlib.sha256(path.read_bytes()).hexdigest() == PIN, 'official converter raw pin'
    spec = importlib.util.spec_from_file_location('r72_official_json_schema_to_grammar', path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

def grammar(schema):
    official = converter()

    class Intersected(official.SchemaConverter):

        def visit(self, node, name):
            if node.get('type') == 'string' and node.get('pattern') == '^[^0-9０-９%％]*$' and ('maxLength' in node) and ('enum' not in node) and ('const' not in node):
                lower, upper = (node.get('minLength', 0), node['maxLength'])
                assert type(lower) is int and type(upper) is int and (0 <= lower <= upper <= 320)
                raw = '[^"\\\\\\x00-\\x1F\\uD800-\\uDFFF0-9０-９%％]'
                escaped = [literal(fragment(c)) for c in ('"', '\\')]
                escaped += [literal(fragment(chr(c))) for c in range(32)]
                char = self._add_rule('r72-public-json-char', ' | '.join([raw] + escaped))
                quote = literal('"')
                return self._add_rule(name or 'root', f'{quote} {char}{{{lower},{upper}}} {quote} space')
            return super().visit(node, name)
    value = deepcopy(schema)
    builder = Intersected(prop_order={}, allow_fetch=False, dotall=False, raw_pattern=False)
    builder.visit(value, '')
    return builder.format_grammar() + '\n'

def native_constraint(native, *, prose=False):
    assert prose is False, 'original two-field prose uses the existing producer'
    text = grammar(native)
    record = dict(schema='dungeonstory-native-format-constraint@1', decoder=VERSION, nativeSchemaSha256='sha256:' + hashlib.sha256(json.dumps(native, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode()).hexdigest(), nativeGrammarSha256='sha256:' + hashlib.sha256(text.encode()).hexdigest(), officialConverterSha256=PIN, originalSchemaUnchanged=True, outputRepair=False, semanticChoicesComputedByCode=False)
    return (dict(grammar=text), record)
