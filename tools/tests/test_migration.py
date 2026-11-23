import json
from pathlib import Path
import sys
from tempfile import TemporaryDirectory
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from validate_migration import CLONE, ORIGINAL, M1_MUTABLE, sha256, validate_m0, validate_m1


class TestMigrationPreservation(unittest.TestCase):
    def setUp(self):
        self.temporary = TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        (self.root/ORIGINAL).parent.mkdir(parents=True)
        for path in (ORIGINAL, CLONE):
            (self.root/path).write_bytes(b'original user scene\r\n')
        for path, guid in ((ORIGINAL, 'a' * 32), (CLONE, 'b' * 32)):
            (self.root/(str(path) + '.meta')).write_text(f'fileFormatVersion: 2\nguid: {guid}\n', encoding='utf-8')
        self.manifest = self.root/'manifest.json'
        self.manifest.write_text(json.dumps({
            'schemaVersion': 1, 'verified': True, 'files': [
                {'path': str(ORIGINAL), 'exists': True, 'sha256': sha256(self.root/ORIGINAL)},
                {'path': 'Assets/user-deleted.anim', 'exists': False, 'sha256': None},
            ]}), encoding='utf-8')

    def test_preserved_scene_and_unique_clone(self):
        self.assertEqual([], validate_m0(self.root, self.manifest))

    def test_changed_original_is_detected_even_when_clone_matches(self):
        for path in (ORIGINAL, CLONE):
            (self.root/path).write_bytes(b'changed')
        self.assertIn(f'baseline bytes changed: {ORIGINAL.as_posix()}', validate_m0(self.root, self.manifest))

    def test_user_deletion_must_remain_deleted(self):
        (self.root/'Assets/user-deleted.anim').write_bytes(b'restored without permission')
        self.assertIn('baseline existence changed: Assets/user-deleted.anim', validate_m0(self.root, self.manifest))

    def test_scene_clone_mutation_is_detected(self):
        (self.root/CLONE).write_bytes(b'changed clone')
        self.assertIn('M0 scene clone must be byte-identical before M1 detachment', validate_m0(self.root))

    def test_duplicate_guid_is_detected(self):
        (self.root/(str(CLONE) + '.meta')).write_text('guid: ' + 'a' * 32, encoding='utf-8')
        self.assertIn(f'clone GUID duplicates: {ORIGINAL.as_posix()}.meta', validate_m0(self.root))

    def add_baseline_files(self, paths):
        manifest = json.loads(self.manifest.read_text(encoding='utf-8'))
        for relative in paths:
            path = self.root/relative
            path.parent.mkdir(parents=True, exist_ok=True)
            if not path.exists():
                path.write_bytes(b'original')
            manifest['files'].append({'path': str(relative), 'exists': True, 'sha256': sha256(path)})
        self.manifest.write_text(json.dumps(manifest), encoding='utf-8')

    def test_m1_allows_only_authorized_source_package_and_clone_byte_changes(self):
        self.add_baseline_files(M1_MUTABLE)
        for relative in M1_MUTABLE:
            (self.root/relative).write_bytes(b'authorized M1 change')
        self.assertEqual([], validate_m1(self.root, self.manifest))
        self.assertTrue(any('baseline bytes changed:' in error for error in validate_m0(self.root, self.manifest)))

    def test_m1_still_protects_original_scene_controller_settings_lfs_and_metadata(self):
        protected = [ORIGINAL, Path('Assets/Scripts/Player/Animation/PlayerController.controller'),
                     Path('Assets/Scripts/Player/PlayerController.cs.meta'), Path(str(ORIGINAL) + '.meta'),
                     Path('ProjectSettings/EditorBuildSettings.asset'), Path('.gitattributes'), Path('.gitignore'),
                     Path('Assets/user-animation.anim'), Path('Report.md')]
        self.add_baseline_files(protected)
        for relative in protected:
            with self.subTest(path=relative):
                path = self.root/relative
                original = path.read_bytes()
                path.write_bytes(b'unauthorized change')
                self.assertIn(f'baseline bytes changed: {relative.as_posix()}', validate_m1(self.root, self.manifest))
                path.write_bytes(original)

    def test_m1_does_not_allow_deleting_mutable_files_or_restoring_user_deletions(self):
        mutable = Path('Assets/Scripts/Player/RootMotionRelay.cs')
        self.add_baseline_files([mutable])
        (self.root/mutable).unlink()
        (self.root/'Assets/user-deleted.anim').write_bytes(b'restored')
        failures = validate_m1(self.root, self.manifest)
        self.assertIn(f'baseline existence changed: {mutable.as_posix()}', failures)
        self.assertIn('baseline existence changed: Assets/user-deleted.anim', failures)

    def test_m1_requires_baseline_and_unique_clone_guid(self):
        self.assertIn('M1 requires a verified preservation baseline', validate_m1(self.root))
        (self.root/(str(CLONE) + '.meta')).write_text('guid: ' + 'a' * 32, encoding='utf-8')
        self.assertIn(f'clone GUID duplicates: {ORIGINAL.as_posix()}.meta', validate_m1(self.root, self.manifest))


if __name__ == '__main__':
    unittest.main()
