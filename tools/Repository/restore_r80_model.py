"""Restore the exact R80 model from verified raw parts without replacing a file."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import tempfile

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_BUNDLE = ROOT / 'Assets/StreamingAssets/R80/DungeonStoryLlm'
DEFAULT_PARTS = ROOT / 'RuntimePackages/R80'
PART_LIMIT = 2_147_483_648
CHUNK_BYTES = 4 * 1024 * 1024
LFS_POINTER = b'version https://git-lfs.github.com/spec/v1'


class RestoreError(RuntimeError):
    """The bundle cannot safely produce its declared original model."""


def require(ok: bool, message: str) -> None:
    if not ok:
        raise RestoreError(message)


def _object(pairs: list[tuple[str, object]]) -> dict:
    result = {}
    for key, value in pairs:
        require(key not in result, 'Duplicate JSON key: ' + key)
        result[key] = value
    return result


def _read_json(path: Path) -> dict:
    require(not path.is_symlink() and path.is_file(), 'Metadata file missing or not regular: ' + path.name)
    try:
        value = json.loads(path.read_text(encoding='utf-8-sig'), object_pairs_hook=_object)
    except (OSError, ValueError, UnicodeError) as error:
        raise RestoreError('Cannot read metadata ' + path.name + ': ' + str(error)) from error
    require(isinstance(value, dict), 'Metadata must be an object: ' + path.name)
    return value


def _basename(value: object) -> bool:
    if not isinstance(value, str) or not value or value in ('.', '..'):
        return False
    if re.search(r'[<>:"/\\|?*\x00-\x1f]', value) or value.endswith(('.', ' ')):
        return False
    reserved = {'CON', 'PRN', 'AUX', 'NUL', *(f'COM{i}' for i in range(1, 10)),
                *(f'LPT{i}' for i in range(1, 10))}
    return value.split('.')[0].upper() not in reserved


def _sha(value: object) -> bool:
    return isinstance(value, str) and re.fullmatch(r'[0-9a-f]{64}', value) is not None


def _metadata(bundle: Path, parts_directory: Path) -> dict:
    manifest = _read_json(bundle / 'manifest.json')
    document = _read_json(parts_directory / 'model-parts.json')
    require(set(document) == {'schema', 'modelFile', 'modelSha256', 'modelBytes', 'parts'},
            'Unexpected model-parts fields')
    require(document['schema'] == 'r80-model-parts-v1', 'Unknown model-parts schema')
    require(_basename(document['modelFile']), 'Unsafe model filename')
    require(_sha(document['modelSha256']), 'Invalid model SHA-256')
    require(type(document['modelBytes']) is int and document['modelBytes'] > 0,
            'Model byte count must be a positive integer')
    require(document['modelFile'] == manifest.get('modelFile'), 'Model filename differs from manifest')
    require(document['modelSha256'] == manifest.get('modelSha256'), 'Model SHA-256 differs from manifest')
    require(type(manifest.get('maximumModelBytes')) is int
            and document['modelBytes'] == manifest['maximumModelBytes'],
            'Model byte count differs from manifest maximumModelBytes')
    parts = document['parts']
    require(isinstance(parts, list) and bool(parts), 'At least one raw model part is required')
    total = 0
    seen = set()
    for index, part in enumerate(parts, 1):
        require(isinstance(part, dict) and set(part) == {'file', 'bytes', 'sha256'},
                'Unexpected model part fields')
        name = part['file']
        require(_basename(name), 'Unsafe model part filename')
        require(name not in seen, 'Duplicate model part: ' + name)
        require(name == document['modelFile'] + f'.part{index:03d}',
                'Model parts must be ordered consecutively from .part001')
        require(type(part['bytes']) is int and 0 < part['bytes'] < PART_LIMIT,
                'Part byte count must be positive and below 2147483648: ' + name)
        require(_sha(part['sha256']), 'Invalid part SHA-256: ' + name)
        seen.add(name)
        total += part['bytes']
    require(total == document['modelBytes'], 'Part byte counts do not sum to modelBytes')
    return document


def _blocks(bundle: Path, name: str, expected_bytes: int):
    path = bundle / name
    require(not path.is_symlink() and path.is_file(), 'Required file missing or not regular: ' + name)
    require(path.stat().st_size == expected_bytes, 'File size mismatch: ' + name)
    count = 0
    with path.open('rb') as stream:
        first = True
        while block := stream.read(CHUNK_BYTES):
            if first:
                require(not block.startswith(LFS_POINTER), 'Run git lfs pull for LFS pointer: ' + name)
                first = False
            count += len(block)
            require(count <= expected_bytes, 'File grew while reading: ' + name)
            yield block
    require(count == expected_bytes, 'File changed while reading: ' + name)


def _digest_file(bundle: Path, name: str, expected_bytes: int) -> str:
    digest = hashlib.sha256()
    for block in _blocks(bundle, name, expected_bytes):
        digest.update(block)
    return digest.hexdigest()


def _check_model(bundle: Path, document: dict) -> None:
    require(_digest_file(bundle, document['modelFile'], document['modelBytes']) == document['modelSha256'],
            'Existing model SHA-256 mismatch; refusing to replace it')


def _copy_parts(parts_directory: Path, document: dict, output=None) -> None:
    combined = hashlib.sha256()
    for part in document['parts']:
        digest = hashlib.sha256()
        for block in _blocks(parts_directory, part['file'], part['bytes']):
            digest.update(block)
            combined.update(block)
            if output is not None:
                require(output.write(block) == len(block), 'Incomplete model write')
        require(digest.hexdigest() == part['sha256'], 'Part SHA-256 mismatch: ' + part['file'])
    require(combined.hexdigest() == document['modelSha256'], 'Combined model SHA-256 mismatch')


def restore(bundle: Path = DEFAULT_BUNDLE, *, parts_directory: Path = DEFAULT_PARTS,
            check_only: bool = False) -> str:
    bundle = Path(bundle).resolve()
    parts_directory = Path(parts_directory).resolve()
    require(bundle.is_dir(), 'Bundle directory missing: ' + str(bundle))
    require(parts_directory.is_dir(), 'Parts directory missing: ' + str(parts_directory))
    document = _metadata(bundle, parts_directory)
    model = bundle / document['modelFile']
    # lexists also protects a dangling symlink from accidental replacement.
    if os.path.lexists(model):
        _check_model(bundle, document)
        return 'MODEL_OK'
    if check_only:
        _copy_parts(parts_directory, document)
        return 'PARTS_OK'

    descriptor, temporary = tempfile.mkstemp(prefix='.r80-model-restore-', suffix='.tmp', dir=bundle)
    temporary = Path(temporary)
    try:
        try:
            output = os.fdopen(descriptor, 'wb')
        except BaseException:
            os.close(descriptor)
            raise
        with output:
            _copy_parts(parts_directory, document, output)
            output.flush()
            os.fsync(output.fileno())
        require(_digest_file(bundle, temporary.name, document['modelBytes']) == document['modelSha256'],
                'Restored model SHA-256 mismatch')
        # A same-directory hard link publishes complete bytes atomically and
        # fails if another process created the model. It never overwrites it.
        try:
            os.link(temporary, model)
        except FileExistsError as error:
            raise RestoreError('Model appeared during restore; refusing to replace it') from error
        return 'RESTORED'
    finally:
        temporary.unlink(missing_ok=True)


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--bundle', type=Path, default=DEFAULT_BUNDLE)
    parser.add_argument('--parts', type=Path, default=DEFAULT_PARTS)
    parser.add_argument('--check-only', action='store_true', help='Verify the original model or all parts without writing')
    arguments = parser.parse_args(argv)
    try:
        result = restore(arguments.bundle, parts_directory=arguments.parts, check_only=arguments.check_only)
    except (RestoreError, OSError) as error:
        parser.exit(1, 'R80 model restore failed: ' + str(error) + '\n')
    print('R80_MODEL=' + result)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
