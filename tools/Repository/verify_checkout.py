"""Validate a development checkout without Unity, generated caches or local QA runs."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
LOCAL_ROOTS = {'library', 'temp', 'obj', 'logs', 'usersettings', 'build', 'builds', '.vs', '.planning', '.codex'}
LOCAL_NOTES = {'HANDOFF.md', 'task_plan.md', 'findings.md', 'progress.md'}


def require(ok: bool, message: str) -> None:
    if not ok:
        raise RuntimeError(message)


def digest(path: Path) -> str:
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def main() -> None:
    tracked = set(filter(None, subprocess.check_output(
        ['git', 'ls-files', '-z'], cwd=ROOT).decode('utf-8').split('\0')))
    policy = json.loads((ROOT / 'tools/Repository/required-fixtures.json').read_text(encoding='utf-8-sig'))
    require(policy.get('schema') == 'development-required-fixtures-v1', 'Unknown fixture policy')
    fixture_paths = [row['path'] for row in policy['files']]
    fixtures = set(fixture_paths)
    require(len(fixtures) == len(fixture_paths), 'Duplicate fixture entry')
    require(all(p.startswith('Artifacts/') and '..' not in Path(p).parts for p in fixtures), 'Unsafe fixture entry')
    require(all(fixtures_path in tracked for fixtures_path in fixtures), 'Required fixture is not tracked')
    for row in policy['files']:
        if row.get('sha256'):
            require(digest(ROOT / row['path']) == row['sha256'], 'Fixed input bytes changed: ' + row['path'])
    unexpected = sorted(p for p in tracked if p.startswith('Artifacts/') and p not in fixtures)
    require(not unexpected, 'Generated artifacts are tracked: ' + repr(unexpected[:8]))
    local = sorted(p for p in tracked if p.split('/')[0].lower() in LOCAL_ROOTS
                   or p in LOCAL_NOTES or p.endswith('.slnx')
                   or (p.endswith('.log') and p not in fixtures))
    require(not local, 'Local outputs/settings are tracked: ' + repr(local[:8]))
    missing = sorted(p for p in tracked if not (ROOT / p).is_file())
    require(not missing, 'Tracked files missing: ' + repr(missing[:8]))
    for name in tracked:
        if name.startswith('Assets/') and not name.endswith('.meta') and not Path(name).name.startswith('.'):
            require(name + '.meta' in tracked, 'Unity meta missing: ' + name)
    for folder in ['Assets', 'Packages', 'ProjectSettings']:
        require((ROOT / folder).is_dir(), 'Unity root missing: ' + folder)
    version = (ROOT / 'ProjectSettings/ProjectVersion.txt').read_text(encoding='utf-8-sig')
    require(re.search(r'^m_EditorVersion: \S+', version, re.M) is not None, 'Unity version missing')
    packages = json.loads((ROOT / 'Packages/manifest.json').read_text(encoding='utf-8-sig'))
    require((ROOT / 'Packages/packages-lock.json').is_file(), 'Package lock missing')
    for package, source in packages['dependencies'].items():
        if not source.startswith('file:'):
            continue
        path = (ROOT / 'Packages' / source[5:]).resolve()
        require(path.is_relative_to(ROOT.resolve()), 'External local package: ' + package)
        require((path / 'package.json').is_file(), 'Local package missing: ' + package)
    build = (ROOT / 'ProjectSettings/EditorBuildSettings.asset').read_text(encoding='utf-8-sig')
    scenes = re.findall(r'path: (.+)\r?\n\s+guid: ([a-f0-9]+)', build)
    require(bool(scenes), 'Build scenes missing')
    for scene, guid in scenes:
        scene = scene.strip()
        require(scene in tracked and scene + '.meta' in tracked, 'Build scene missing: ' + scene)
        meta = (ROOT / (scene + '.meta')).read_text(encoding='utf-8-sig')
        require(re.search(r'^guid: ' + re.escape(guid) + r'$', meta, re.M) is not None,
                'Build scene GUID mismatch: ' + scene)
    lfs = json.loads(subprocess.check_output(['git', 'lfs', 'ls-files', '--json'], cwd=ROOT))
    lfs_files = lfs['files'] if isinstance(lfs, dict) else lfs
    if lfs_files:
        names = b'\0'.join(entry['name'].encode('utf-8') for entry in lfs_files) + b'\0'
        attributes = subprocess.check_output(
            ['git', '-c', 'core.ignorecase=false', 'check-attr', '-z', '--stdin', 'filter'],
            cwd=ROOT, input=names).decode('utf-8').split('\0')
        for offset in range(0, len(attributes) - 2, 3):
            require(attributes[offset + 2] == 'lfs',
                    'Case-sensitive LFS filter missing: ' + attributes[offset])
    for entry in lfs_files:
        path = ROOT / entry['name']
        require(path.is_file(), 'LFS file missing: ' + entry['name'])
        with path.open('rb') as stream:
            pointer = stream.read(64).startswith(b'version https://git-lfs.github.com/spec/v1')
        require(not pointer, 'Run git lfs pull: ' + entry['name'])
        require(path.stat().st_size == entry['size'], 'LFS size mismatch: ' + entry['name'])
    streaming = ROOT / 'Assets/StreamingAssets'
    runtimes = [streaming / 'DungeonStoryLlm']
    activation = streaming / 'narrative-runtime.json'
    if activation.is_file():
        active = json.loads(activation.read_bytes())
        relative = active.get('relativeRoot', '')
        selected = (streaming / relative).resolve()
        require(bool(relative) and not Path(relative).is_absolute()
                and selected.is_relative_to(streaming.resolve()) and selected != streaming.resolve(),
                'Unsafe active runtime root')
        runtime = selected / 'DungeonStoryLlm'
        require(digest(runtime / 'manifest.json') == active.get('manifestSha256'),
                'Active runtime manifest hash mismatch')
        runtimes.append(runtime)
    checked_binaries = 0
    for runtime in runtimes:
        manifest = json.loads((runtime / 'manifest.json').read_bytes())
        binaries = [(manifest['hostWindows'], manifest['hostWindowsSha256']),
                    (manifest['modelFile'], manifest['modelSha256'])]
        binaries += [(row['file'], row['sha256']) for row in manifest['supportFiles']]
        for name, expected in binaries:
            path = (runtime / name).resolve()
            require(bool(name) and not Path(name).is_absolute()
                    and path.is_relative_to(runtime.resolve()) and path != runtime.resolve(),
                    'Unsafe/missing runtime filename')
            require(path.is_file(), 'Runtime file missing; run restore_r80_model.py if needed: ' + name)
            require(digest(path) == expected, 'Runtime hash mismatch: ' + name)
        checked_binaries += len(binaries)
    print(f'CHECKOUT=PASS; scenes={len(scenes)}; lfs={len(lfs_files)}; runtime={checked_binaries}; fixtures={len(fixtures)}')
    print('Scope: file completeness and integrity; Unity import/compilation/play not executed.')


if __name__ == '__main__':
    main()
