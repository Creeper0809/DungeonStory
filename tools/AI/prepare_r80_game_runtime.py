"""Build the offline R80 game bundle without modifying earlier model bundles.

Only reachable inference definitions are copied from the evaluated recipe.
Every retained definition is checked against its source AST. Two explicit game
cardinality parser adaptations are then recorded separately. No data, evaluation
answers, training entry points or Hugging Face downloads enter this runtime.
"""
import argparse
import ast
import copy
import hashlib
import json
from pathlib import Path
import shutil
import sys
import zipfile


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


class InferenceSlice:
    def __init__(self, source):
        self.source = source
        self.trees, self.definitions, self.selected, self.imports = {}, {}, {}, {}

    def load(self, module):
        if module in self.trees:
            return
        path = self.source.joinpath(*module.split('.')).with_suffix('.py')
        if not path.is_file():
            return
        tree = ast.parse(path.read_text(encoding='utf-8-sig'))
        definitions = {}
        imports_in_try = []
        for statement in tree.body:
            if isinstance(statement, ast.Try):
                for handler in statement.handlers:
                    imports_in_try.extend(n for n in handler.body if isinstance(n, (ast.Import, ast.ImportFrom)))
        for node in tree.body + imports_in_try:
            if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef, ast.ClassDef)):
                definitions[node.name] = node
            elif isinstance(node, (ast.Assign, ast.AnnAssign)):
                for target in node.targets if isinstance(node, ast.Assign) else [node.target]:
                    for name in ast.walk(target):
                        if isinstance(name, ast.Name):
                            definitions[name.id] = node
            elif isinstance(node, (ast.Import, ast.ImportFrom)):
                for alias in node.names:
                    definitions[alias.asname or alias.name.split('.')[0]] = (node, alias)
        self.trees[module] = tree
        self.definitions[module] = definitions
        self.selected[module] = set()
        self.imports[module] = {}

    def require(self, module, name):
        self.load(module)
        if module not in self.trees:
            return
        if name not in self.definitions[module]:
            raise ValueError(f'Missing inference export {module}.{name}')
        item = self.definitions[module][name]
        if isinstance(item, tuple):
            node, alias = item
            self.imports[module][name] = (node, alias)
            if isinstance(node, ast.ImportFrom):
                if node.level:
                    raise ValueError('Unexpected relative import in inference slice')
                self.require(node.module, alias.name)
            return
        if item in self.selected[module]:
            return
        self.selected[module].add(item)
        for dep in ast.walk(item):
            if isinstance(dep, ast.Name) and isinstance(dep.ctx, ast.Load) and dep.id in self.definitions[module]:
                self.require(module, dep.id)
            if isinstance(dep, ast.Attribute) and isinstance(dep.value, ast.Name):
                imported = self.definitions[module].get(dep.value.id)
                if isinstance(imported, tuple) and isinstance(imported[0], ast.Import):
                    self.require(imported[1].name, dep.attr)
            # Lazy imports inside retained functions are also part of the closure.
            if isinstance(dep, ast.ImportFrom):
                for alias in dep.names:
                    self.require(dep.module, alias.name)

    def write(self, destination):
        records = []
        for module in sorted(self.trees):
            chosen = self.selected[module]
            imports = self.imports[module]
            if not chosen and not imports:
                continue
            nodes = []
            for node in self.trees[module].body:
                if isinstance(node, ast.ImportFrom) and node.module == '__future__':
                    nodes.append(copy.deepcopy(node))
            grouped = {}
            for node, alias in imports.values():
                grouped.setdefault(node, []).append(alias)
            for node, aliases in grouped.items():
                cloned = copy.deepcopy(node)
                cloned.names = copy.deepcopy(aliases)
                nodes.append(cloned)
            for node in self.trees[module].body:
                if node in chosen:
                    nodes.append(copy.deepcopy(node))
            tree = ast.Module(body=nodes, type_ignores=[])
            text = '# Reachable R80 inference definitions. See recipe-provenance.json.\n' + ast.unparse(tree) + '\n'
            path = destination.joinpath(*module.split('.')).with_suffix('.py')
            path.parent.mkdir(parents=True, exist_ok=True)
            for parent in path.parents:
                if parent == destination:
                    break
                (parent / '__init__.py').touch(exist_ok=True)
            path.write_text(text, encoding='utf8', newline='\n')
            actual = ast.parse(text)
            for node in chosen:
                assert any(ast.dump(node) == ast.dump(found) for found in actual.body)
            source = self.source.joinpath(*module.split('.')).with_suffix('.py')
            records.append(dict(module=module, sourceSha256=sha(source), runtimeSha256=sha(path),
                                retainedDefinitions=len(chosen), retainedAstExact=True))
        return records


def adapt_game_cardinality(recipe, records):
    """Extend two parser guards to read the game's explicit smaller offers.

    The original R80 source remains untouched. No selection, evidence, loss,
    grammar or assembly algorithm changes. Exact three-candidate 1..3 requests
    retain the same contract. Smaller game offers keep their declared bounds.
    """
    path = recipe / 'live_module_selection_contract.py'
    before = path.read_text(encoding='utf8')
    replacements = {
        "require(candidate['positiveModuleLimit'] == '1..3', 'unsupported skill cardinality')":
            "require(re.fullmatch(r'1\\.\\.[1-3]', candidate['positiveModuleLimit']) is not None, 'unsupported skill cardinality')",
        'maximumPositive=3, maximumDrawback=1, maximumEvidence=2)':
            "maximumPositive=int(candidate['positiveModuleLimit'][-1]), maximumDrawback=1, maximumEvidence=2)",
        "require(len(offers) == 3, 'current skill requires three candidates')":
            "require(1 <= len(offers) <= 3, 'current game skill requires one to three candidates')",
    }
    after = before
    for old, new in replacements.items():
        if after.count(old) != 1:
            raise ValueError('Original game cardinality guard drift: ' + old)
        after = after.replace(old, new, 1)
    original = {n.name: ast.dump(n) for n in ast.parse(before).body if isinstance(n, ast.FunctionDef)}
    adapted = {n.name: ast.dump(n) for n in ast.parse(after).body if isinstance(n, ast.FunctionDef)}
    if {key for key in original if original[key] != adapted[key]} != {'_compact', 'inspect_wire'}:
        raise ValueError('Game adaptation changed an unexpected inference definition')
    path.write_text(after, encoding='utf8', newline='\n')
    record = next(value for value in records if value['module'] == 'live_module_selection_contract')
    record.update(originalRuntimeSha256=hashlib.sha256(before.encode('utf8')).hexdigest(),
        runtimeSha256=sha(path), retainedAstExact=False,
        gameAdapter='explicit-one-to-three-candidate-cardinality@1',
        adaptedDefinitions=['_compact', 'inspect_wire'],
        semanticSelectionAlgorithmChanged=False)
    return records


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--engine-bundle', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    source = args.source.resolve()
    root = args.output.resolve()
    # A failed preparation can leave the verified large model copy in place.
    # A finished bundle is immutable; only that unsealed preparation may resume.
    if (root / 'DungeonStoryLlm/manifest.json').exists():
        raise FileExistsError('Completed runtime already exists')
    root.mkdir(parents=True, exist_ok=True)
    bundle = root / 'DungeonStoryLlm'
    bundle.mkdir(exist_ok=True)
    recipe = bundle / 'recipe'
    recipe.mkdir(exist_ok=True)
    slicer = InferenceSlice(source / 'tools/v25_narrative_training')
    exports = {
        'candidate_task_view': ['contract', 'assemble'],
        'single_decision_view': ['reading_messages', 'selection_messages', 'selection_native', 'binding_errors'],
        'scoped_auxiliary_view': ['prose_messages', 'prose_native'],
        'numeric_free_prose_grammar': ['native_constraint'],
        'behavior_auxiliary_grammar': ['native_constraint'],
        'prioritized_needs_view': ['messages_for'],
        'student_llama_inference': ['llama_schema', 'validated_response'],
        'native_alignment_trial': ['reading_native'],
    }
    for module, names in exports.items():
        for name in names:
            slicer.require(module, name)
    provenance = adapt_game_cardinality(recipe, slicer.write(recipe))
    converter = source / 'Artifacts/Working/DistilledNarrative-20261018-b205-r1/native-prose-bounds-r56/cpu-native-r1/source/examples/json_schema_to_grammar.py'
    assert sha(converter) == '6eedcbad62ba45438e7e1efad4749f297f218f0b8989e7128edd071e4784b0b0'
    shutil.copyfile(converter, recipe / 'r72_aux_json_schema_to_grammar.py')
    # The evaluated public dispatcher is retained verbatim as a module. Its CLI
    # and legacy standalone boot path are never invoked by the game bridge.
    prototype = source / 'Prototype/Model-R80'
    shutil.copyfile(prototype / 'run_request.py', recipe / 'r80_request.py')
    shutil.copyfile(prototype / 'static-schemas.json', recipe / 'static-schemas.json')
    model = prototype / 'DungeonStory-R80-8B-Q4_K_M.gguf'
    assert model.stat().st_size == 5027783552
    assert sha(model) == '2894ec5261b3a8352a375eb6319bbc8dd8c68c9b7b64fba255d51ce1bc2eb9b2'
    if not (bundle / model.name).exists():
        shutil.copyfile(model, bundle / model.name)
    assert sha(bundle / model.name) == sha(model)
    old_manifest = json.loads((args.engine_bundle / 'manifest.json').read_text(encoding='utf-8-sig'))
    for entry in [{'file': old_manifest['hostWindows']}] + old_manifest['supportFiles']:
        shutil.copyfile(args.engine_bundle / entry['file'], bundle / entry['file'])
    python_root = Path(sys.base_prefix)
    python_dir = bundle / 'python'
    python_dir.mkdir()
    for name in ['python.exe', 'python3.dll', 'python313.dll', 'vcruntime140.dll', 'vcruntime140_1.dll', 'LICENSE.txt']:
        if (python_root / name).is_file():
            shutil.copyfile(python_root / name, python_dir / name)
    shutil.copytree(python_root / 'DLLs', python_dir / 'DLLs',
                    ignore=shutil.ignore_patterns('*.pdb', '*.lib', '*.exp', 'tk*', 'tcl*', '_tkinter*'))
    with zipfile.ZipFile(python_dir / 'python313.zip', 'x', zipfile.ZIP_DEFLATED) as archive:
        for path in (python_root / 'Lib').rglob('*.py'):
            relative = path.relative_to(python_root / 'Lib')
            if not set(relative.parts) & {'site-packages', '__pycache__', 'test', 'tests', 'idlelib', 'tkinter', 'ensurepip', 'turtledemo'}:
                archive.write(path, relative.as_posix())
    packages = source / '.venv-scenarios/Lib/site-packages'
    # Unity adds .meta siblings to loose resources. Keep pure packages and their
    # JSON schema resources in the zip so resource enumeration stays unchanged.
    with zipfile.ZipFile(python_dir / 'python313.zip', 'a', zipfile.ZIP_DEFLATED) as archive:
        names = ['jsonschema', 'jsonschema_specifications', 'referencing', 'attrs', 'attr']
        for pattern in ['jsonschema-*.dist-info', 'jsonschema_specifications-*.dist-info', 'referencing-*.dist-info', 'rpds_py-*.dist-info', 'attrs-*.dist-info']:
            matches = list(packages.glob(pattern))
            assert len(matches) == 1, pattern
            names.append(matches[0].name)
        for name in names:
            for path in (packages / name).rglob('*'):
                if path.is_file() and not set(path.parts) & {'__pycache__', 'tests'}:
                    archive.write(path, path.relative_to(packages).as_posix())
    shutil.copytree(packages / 'rpds', python_dir / 'packages/rpds',
                    ignore=shutil.ignore_patterns('__pycache__', 'tests'))
    (python_dir / 'python313._pth').write_text('python313.zip\nDLLs\npackages\n../recipe\n..\n', encoding='utf8')
    bridge = Path(__file__).with_name('r80_game_bridge.py')
    shutil.copyfile(bridge, bundle / bridge.name)
    (bundle / 'recipe-provenance.json').write_text(json.dumps(dict(
        sourceModelManifestSha256=sha(prototype / 'model-manifest.json'),
        originalDispatcherSha256=sha(prototype / 'run_request.py'),
        slices=provenance, dataIncluded=False, trainingEntryPointsInvoked=False), indent=2), encoding='utf8')
    manifest = dict(protocolVersion=25, hostKind='R80StagedServer', hostWindows='python/python.exe',
        hostWindowsSha256=sha(python_dir / 'python.exe'), modelFile=model.name, modelSha256=sha(model),
        modelAlias='DungeonStory-R80-8B-Q4_K_M', adapterFile='', adapterSha256='', gpuLayers=99,
        contextSize=12288, parallelSlots=1, maximumModelBytes=5027783552,
        modelVersion='DungeonStory-R80-8B-Q4_K_M@2894ec5261b3', releaseCertified=False,
        trainingState='r80-single-checkpoint-prototype',
        supportFiles=[dict(file=p.relative_to(bundle).as_posix(), sha256=sha(p))
                      for p in sorted(bundle.rglob('*')) if p.is_file() and p.name != model.name])
    (bundle / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf8')
    print(json.dumps(dict(bundle=str(bundle), slices=len(provenance), supportFiles=len(manifest['supportFiles']),
                          modelSha256=manifest['modelSha256'])))


if __name__ == '__main__':
    main()
