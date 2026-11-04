"""Independently interpret the generated acyclic name grammar as a regex.

Compare its accepted language with ordinary string predicates, including the
actual caller's pattern and the previously observed duplicate names.
"""
import argparse
from functools import lru_cache
import json
from pathlib import Path
import random
import re

import r80_game_bridge as bridge


def regex_for(rules, root):
    @lru_cache(maxsize=None)
    def expand(name):
        text = rules[name]
        position = 0
        def whitespace():
            nonlocal position
            while position < len(text) and text[position].isspace():
                position += 1
        def expression():
            nonlocal position
            choices = [sequence()]
            whitespace()
            while position < len(text) and text[position] == '|':
                position += 1
                choices.append(sequence())
                whitespace()
            return '(?:' + '|'.join(choices) + ')'
        def sequence():
            nonlocal position
            values = []
            whitespace()
            while position < len(text) and text[position] not in ')|':
                if text[position] == '"':
                    value, end = json.JSONDecoder().raw_decode(text[position:])
                    position += end
                    value = re.escape(value)
                elif text[position] == '[':
                    end = text.index(']', position) + 1
                    value = text[position:end]
                    position = end
                elif text[position] == '(':
                    position += 1
                    value = expression()
                    assert text[position] == ')'
                    position += 1
                else:
                    match = re.match(r'[A-Za-z0-9-]+', text[position:])
                    assert match, text[position:]
                    position += len(match[0])
                    value = expand(match[0])
                whitespace()
                if position < len(text) and text[position] == '{':
                    end = text.index('}', position) + 1
                    value = '(?:' + value + ')' + text[position:end]
                    position = end
                values.append(value)
                whitespace()
            return ''.join(values)
        result = expression()
        assert position == len(text)
        return result
    return re.compile(expand(root))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--wire', type=Path, required=True)
    args = parser.parse_args()
    wire = json.loads(args.wire.read_bytes())
    schema = bridge.authored_skill_schema(wire, 0, [])['properties']['displayName']
    original = re.compile(schema['pattern'])
    rng = random.Random(20261027)
    total = 0
    for forbidden in [('익숙한 손길', '채워진 여유'), ('가', '가나', '가나다'), ('가' * schema['maxLength'],)]:
        grammar = regex_for(bridge.unique_name_rules(schema, forbidden), 'displayName')
        samples = list(forbidden) + ['', ' ', ' 앞', '뒤 ', '새 기술', '새로운 손길', '가' * 14, '가' * 15,
                                      '숫자1', '²', '０', '기술%', '기술"', '\\', '\n', '😀']
        for name in forbidden:
            samples += [name + '결', name[:-1], name + ' ', ' ' + name]
        for _ in range(1000):
            samples.append(''.join(rng.choice('가나다라마바사아자채워진여유손길 ABC12%²') for _ in range(rng.randrange(17))))
        for value in samples:
            expected = (1 <= len(value) <= schema['maxLength'] and value == value.strip()
                and value not in forbidden and all(ord(c) < 65536 and ord(c) not in (34, 92)
                    and not 0xD800 <= ord(c) <= 0xDFFF and ord(c) >= 32 and original.fullmatch(c) for c in value))
            actual = bool(grammar.fullmatch(json.dumps(value, ensure_ascii=False)))
            assert actual == bool(expected), (forbidden, value, expected, actual)
            total += 1
    print('CANONICAL_NAME_GRAMMAR_LANGUAGE_PASS; cases=' + str(total) + '; modelGeneration=False')


if __name__ == '__main__':
    main()
