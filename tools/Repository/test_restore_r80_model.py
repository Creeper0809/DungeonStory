"""Small byte fixtures for model integrity and create-only publication."""
from copy import deepcopy
import hashlib
import json
import os
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest
from unittest.mock import patch

import restore_r80_model as subject


class RestoreModelTests(unittest.TestCase):
    def setUp(self):
        self.directory = TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.bundle = Path(self.directory.name) / 'bundle'
        self.parts = Path(self.directory.name) / 'parts'
        self.bundle.mkdir()
        self.parts.mkdir()
        self.data = b'first raw block\x00second raw block\xffthird raw block'
        self.model = self.bundle / 'fixture.gguf'
        self.manifest = dict(modelFile=self.model.name, modelSha256=self.sha(self.data),
                             maximumModelBytes=len(self.data))
        pieces = [self.data[:15], self.data[15:31], self.data[31:]]
        self.document = dict(schema='r80-model-parts-v1', modelFile=self.model.name,
                             modelSha256=self.sha(self.data), modelBytes=len(self.data), parts=[])
        for index, piece in enumerate(pieces, 1):
            name = self.model.name + f'.part{index:03d}'
            (self.parts / name).write_bytes(piece)
            self.document['parts'].append(dict(file=name, bytes=len(piece), sha256=self.sha(piece)))
        self.write_metadata()

    @staticmethod
    def sha(data):
        return hashlib.sha256(data).hexdigest()

    def write_metadata(self):
        (self.bundle / 'manifest.json').write_text(json.dumps(self.manifest), encoding='utf8')
        (self.parts / 'model-parts.json').write_text(json.dumps(self.document), encoding='utf8')

    def assert_no_partial_model(self):
        self.assertFalse(os.path.lexists(self.model))
        self.assertEqual(list(self.bundle.glob('.r80-model-restore-*.tmp')), [])

    def test_exact_restore_and_second_call_is_noop(self):
        parts = {part['file']: (self.parts / part['file']).read_bytes() for part in self.document['parts']}
        self.assertEqual(subject.restore(self.bundle, parts_directory=self.parts), 'RESTORED')
        self.assertEqual(self.model.read_bytes(), self.data)
        modified = self.model.stat().st_mtime_ns
        with patch.object(subject, '_copy_parts', side_effect=AssertionError('No part copy on a no-op')):
            self.assertEqual(subject.restore(self.bundle, parts_directory=self.parts), 'MODEL_OK')
            self.assertEqual(subject.restore(self.bundle, parts_directory=self.parts, check_only=True), 'MODEL_OK')
        self.assertEqual(self.model.stat().st_mtime_ns, modified)
        self.assertEqual(parts, {name: (self.parts / name).read_bytes() for name in parts})
        self.assertEqual(list(self.bundle.glob('.r80-model-restore-*.tmp')), [])

    def test_check_only_verifies_all_parts_without_creating_model(self):
        self.assertEqual(subject.restore(self.bundle, parts_directory=self.parts, check_only=True), 'PARTS_OK')
        self.assert_no_partial_model()
        part = self.parts / self.document['parts'][-1]['file']
        part.write_bytes(b'x' * part.stat().st_size)
        with self.assertRaisesRegex(subject.RestoreError, 'SHA-256 mismatch'):
            subject.restore(self.bundle, parts_directory=self.parts, check_only=True)
        self.assert_no_partial_model()

    def test_restored_bundle_has_no_parts_or_descriptor(self):
        self.assertEqual(subject.restore(self.bundle, parts_directory=self.parts), 'RESTORED')
        self.assertEqual({path.name for path in self.bundle.iterdir()}, {'manifest.json', self.model.name})
        self.assertEqual(self.model.read_bytes(), self.data)
        self.assertTrue((self.parts / 'model-parts.json').is_file())
        for part in self.document['parts']:
            self.assertTrue((self.parts / part['file']).is_file())

    def test_missing_truncated_or_changed_part_preserves_inputs(self):
        part = self.parts / self.document['parts'][1]['file']
        original = part.read_bytes()
        for bad in (None, original[:-1], b'x' * len(original)):
            with self.subTest(bad=bad):
                if bad is None:
                    part.unlink()
                else:
                    part.write_bytes(bad)
                with self.assertRaises(subject.RestoreError):
                    subject.restore(self.bundle, parts_directory=self.parts)
                self.assert_no_partial_model()
                self.assertEqual(part.read_bytes() if part.exists() else None, bad)
                part.write_bytes(original)

    def test_existing_changed_model_is_never_overwritten(self):
        for bad in (b'x', b'x' * len(self.data)):
            self.model.write_bytes(bad)
            for check_only in (False, True):
                with self.subTest(size=len(bad), check_only=check_only), self.assertRaises(subject.RestoreError):
                    subject.restore(self.bundle, parts_directory=self.parts, check_only=check_only)
                self.assertEqual(self.model.read_bytes(), bad)
                self.assertEqual(list(self.bundle.glob('.r80-model-restore-*.tmp')), [])

    def test_metadata_rejects_wrong_fields_bounds_hashes_and_order(self):
        changes = [
            lambda d: d.update(schema='unknown'),
            lambda d: d.update(modelFile='../fixture.gguf'),
            lambda d: d.update(modelFile='C:\\fixture.gguf'),
            lambda d: d.update(modelSha256='bad'),
            lambda d: d.update(modelSha256='0' * 64),
            lambda d: d.update(modelBytes=True),
            lambda d: d.update(modelBytes=d['modelBytes'] + 1),
            lambda d: d.update(parts=[]),
            lambda d: d.update(extra='unexpected'),
            lambda d: d['parts'][0].update(file='../escape'),
            lambda d: d['parts'][0].update(file='..\\escape'),
            lambda d: d['parts'][0].update(file='/absolute'),
            lambda d: d['parts'][0].update(file='fixture.gguf:stream'),
            lambda d: d['parts'][1].update(file=d['parts'][0]['file']),
            lambda d: d['parts'].reverse(),
            lambda d: d['parts'][0].update(bytes=0),
            lambda d: d['parts'][0].update(bytes=True),
            lambda d: d['parts'][0].update(bytes=subject.PART_LIMIT),
            lambda d: d['parts'][0].update(bytes=d['parts'][0]['bytes'] + 1),
            lambda d: d['parts'][0].update(sha256='z' * 64),
        ]
        original = deepcopy(self.document)
        for index, change in enumerate(changes):
            with self.subTest(case=index):
                self.document = deepcopy(original)
                change(self.document)
                self.write_metadata()
                with self.assertRaises(subject.RestoreError):
                    subject.restore(self.bundle, parts_directory=self.parts)
                self.assert_no_partial_model()

    def test_manifest_disagreement_is_rejected(self):
        for field, value in [('modelFile', 'other.gguf'), ('modelSha256', '0' * 64),
                             ('maximumModelBytes', len(self.data) + 1), ('maximumModelBytes', True)]:
            with self.subTest(field=field):
                original = self.manifest[field]
                self.manifest[field] = value
                self.write_metadata()
                with self.assertRaises(subject.RestoreError):
                    subject.restore(self.bundle, parts_directory=self.parts)
                self.assert_no_partial_model()
                self.manifest[field] = original

    def test_duplicate_json_keys_and_missing_metadata_are_rejected(self):
        path = self.parts / 'model-parts.json'
        path.write_text('{"schema":"r80-model-parts-v1","schema":"r80-model-parts-v1"}', encoding='utf8')
        with self.assertRaisesRegex(subject.RestoreError, 'Duplicate JSON key'):
            subject.restore(self.bundle, parts_directory=self.parts)
        path.unlink()
        with self.assertRaises(subject.RestoreError):
            subject.restore(self.bundle, parts_directory=self.parts)
        self.assert_no_partial_model()

    def test_lfs_pointer_is_rejected_even_when_its_declared_hash_matches(self):
        pointer = subject.LFS_POINTER + b'\noid sha256:' + b'0' * 64 + b'\nsize 5027783552\n'
        self.manifest.update(modelSha256=self.sha(pointer), maximumModelBytes=len(pointer))
        self.document.update(modelSha256=self.sha(pointer), modelBytes=len(pointer), parts=[
            dict(file=self.model.name + '.part001', bytes=len(pointer), sha256=self.sha(pointer))])
        (self.parts / self.document['parts'][0]['file']).write_bytes(pointer)
        self.write_metadata()
        with self.assertRaisesRegex(subject.RestoreError, 'LFS pointer'):
            subject.restore(self.bundle, parts_directory=self.parts)
        self.assert_no_partial_model()
        self.model.write_bytes(pointer)
        with self.assertRaisesRegex(subject.RestoreError, 'LFS pointer'):
            subject.restore(self.bundle, parts_directory=self.parts, check_only=True)
        self.assertEqual(self.model.read_bytes(), pointer)

    def test_combined_hash_is_checked_separately_from_part_hashes(self):
        self.manifest['modelSha256'] = self.document['modelSha256'] = '0' * 64
        self.write_metadata()
        with self.assertRaisesRegex(subject.RestoreError, 'Combined model SHA-256 mismatch'):
            subject.restore(self.bundle, parts_directory=self.parts)
        self.assert_no_partial_model()

    def test_write_failure_removes_only_own_temporary_file(self):
        real_fdopen = os.fdopen
        unrelated = self.bundle / '.r80-model-restore-other.tmp'
        unrelated.write_bytes(b'keep')
        parts = {p['file']: (self.parts / p['file']).read_bytes() for p in self.document['parts']}

        class FailingWriter:
            def __init__(self, stream):
                self.stream = stream
                self.writes = 0

            def __enter__(self):
                return self

            def __exit__(self, *args):
                self.stream.close()

            def write(self, block):
                self.writes += 1
                if self.writes > 1:
                    raise OSError('simulated disk full')
                return self.stream.write(block)

        with patch.object(subject.os, 'fdopen', side_effect=lambda *a, **k: FailingWriter(real_fdopen(*a, **k))):
            with self.assertRaisesRegex(OSError, 'disk full'):
                subject.restore(self.bundle, parts_directory=self.parts)
        self.assertFalse(self.model.exists())
        self.assertEqual(list(self.bundle.glob('.r80-model-restore-*.tmp')), [unrelated])
        self.assertEqual(unrelated.read_bytes(), b'keep')
        self.assertEqual(parts, {name: (self.parts / name).read_bytes() for name in parts})

    def test_concurrent_destination_is_not_overwritten(self):
        real_link = os.link

        def competing_link(source, destination):
            Path(destination).write_bytes(b'another writer')
            return real_link(source, destination)

        with patch.object(subject.os, 'link', side_effect=competing_link):
            with self.assertRaisesRegex(subject.RestoreError, 'appeared during restore'):
                subject.restore(self.bundle, parts_directory=self.parts)
        self.assertEqual(self.model.read_bytes(), b'another writer')
        self.assertEqual(list(self.bundle.glob('.r80-model-restore-*.tmp')), [])

    def test_written_bytes_are_verified_before_publication(self):
        real_copy = subject._copy_parts

        def corrupted_copy(parts_directory, document, output):
            real_copy(parts_directory, document, output)
            output.seek(0)
            output.write(b'X')

        with patch.object(subject, '_copy_parts', side_effect=corrupted_copy):
            with self.assertRaisesRegex(subject.RestoreError, 'Restored model SHA-256 mismatch'):
                subject.restore(self.bundle, parts_directory=self.parts)
        self.assert_no_partial_model()

    def test_file_wrapper_failure_closes_and_removes_temporary_file(self):
        with patch.object(subject.os, 'fdopen', side_effect=OSError('simulated open failure')):
            with self.assertRaisesRegex(OSError, 'open failure'):
                subject.restore(self.bundle, parts_directory=self.parts)
        self.assert_no_partial_model()


if __name__ == '__main__':
    unittest.main()
