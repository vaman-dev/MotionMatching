"""Milestone file-preservation checks only; never imports, compiles or plays Unity."""
import argparse
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
ORIGINAL = Path('Assets/Scenes/Test.unity')
CLONE = Path('Assets/Scenes/MotionMatchingTest.unity')
M1_MUTABLE = frozenset({
    'Assets/Scripts/Player/PlayerController.cs',
    'Assets/Scripts/Player/RootMotionRelay.cs',
    'Packages/manifest.json',
    'Packages/packages-lock.json',
    CLONE.as_posix(),
})


def sha256(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def validate_m0(root=ROOT, baseline=None):
    return _validate(root, baseline, 'm0')


def validate_m1(root=ROOT, baseline=None):
    if baseline is None:
        return ['M1 requires a verified preservation baseline']
    return _validate(root, baseline, 'm1')


def _validate(root, baseline, mode):
    root = Path(root)
    errors = []
    if baseline is not None:
        manifest = json.loads(Path(baseline).read_text(encoding='utf-8-sig'))
        if manifest.get('schemaVersion') != 1 or manifest.get('verified') is not True:
            errors.append('baseline is not a verified schemaVersion 1 preservation manifest')
        protected = {'Assets', 'Packages', 'ProjectSettings', '.gitattributes', '.gitignore'}
        if mode == 'm1':
            protected.add('Report.md')
        for row in manifest['files']:
            relative = Path(row['path'].replace('\\', '/'))
            if not relative.parts or relative.is_absolute() or '..' in relative.parts:
                errors.append(f'invalid baseline path: {relative}')
                continue
            if relative.parts[0] not in protected:
                continue
            path = root/relative
            if path.is_file() != row['exists']:
                errors.append(f'baseline existence changed: {relative.as_posix()}')
            elif row['exists'] and not (mode == 'm1' and relative.as_posix() in M1_MUTABLE) and sha256(path) != row['sha256']:
                errors.append(f'baseline bytes changed: {relative.as_posix()}')
    original, clone = root/ORIGINAL, root/CLONE
    if not original.is_file() or not clone.is_file():
        errors.append(f'{mode.upper()} requires original Test scene and MotionMatchingTest clone')
    elif mode == 'm0' and sha256(original) != sha256(clone):
        errors.append('M0 scene clone must be byte-identical before M1 detachment')
    clone_meta = root/(str(CLONE) + '.meta')
    if not clone_meta.is_file():
        errors.append('clone scene meta is missing')
    else:
        match = re.search(r'^guid: ([0-9a-f]{32})$', clone_meta.read_text(encoding='utf-8'), re.MULTILINE)
        if match is None:
            errors.append('clone scene meta needs a valid GUID')
        else:
            guid = match[1]
            for meta in (root/'Assets').rglob('*.meta'):
                if meta != clone_meta and re.search(rf'^guid: {guid}$', meta.read_text(encoding='utf-8'), re.MULTILINE):
                    errors.append(f'clone GUID duplicates: {meta.relative_to(root).as_posix()}')
    return errors


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mode', choices=('m0', 'm1'), default='m0', help='M0 requires an identical clone; M1 permits only the explicit motor/package/clone byte changes')
    parser.add_argument('--baseline', type=Path, help='Optional local preservation manifest for original asset/settings hashes')
    args = parser.parse_args()
    failures = (validate_m1 if args.mode == 'm1' else validate_m0)(baseline=args.baseline)
    for failure in failures:
        print('ERROR: ' + failure)
    if failures:
        raise SystemExit(1)
    print(f'{args.mode.upper()} static preservation passed. This does NOT test or compile Unity.')
