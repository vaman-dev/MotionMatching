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


def validate_consolidation(root=ROOT, baseline=None, move_map=None):
    """Verify relocations against the local pre-refactor snapshot, not Git HEAD.

    This checks bytes/GUID ownership, not arbitrary Unity serialized object semantics.
    Unity scene tests and compilation remain required independently.
    """
    root = Path(root)
    baseline = Path(baseline) if baseline else root/'.utmp/consolidation-start/manifest.json'
    move_map = Path(move_map) if move_map else root/'docs/migration/consolidation-moves.json'
    errors = []
    try:
        manifest = json.loads(baseline.read_text(encoding='utf-8-sig'))
        mapping = json.loads(move_map.read_text(encoding='utf-8-sig'))
        if manifest.get('schemaVersion') != 1 or manifest.get('verified') is not True:
            raise ValueError('baseline must be a verified schemaVersion 1 manifest')
        if mapping.get('schemaVersion') != 1:
            raise ValueError('move map must use schemaVersion 1')
        def safe(value):
            path = Path(value.replace('\\', '/'))
            if not path.parts or path.is_absolute() or '..' in path.parts or ':' in value:
                raise ValueError(f'unsafe relative path: {value}')
            return path.as_posix()
        moves = [(safe(row['source']), safe(row['destination'])) for row in mapping['moves']]
        removals = {safe(value) for value in mapping.get('emptyFolderMetaRemovals', [])}
        if len({source for source, _ in moves}) != len(moves) or len({dest for _, dest in moves}) != len(moves):
            raise ValueError('duplicate move source or destination')
        if any(not item.endswith('.meta') for item in removals):
            raise ValueError('only empty folder metadata may be removed')
    except (ValueError, KeyError, TypeError, OSError) as exc:
        return [f'invalid consolidation evidence: {exc}']

    def resolve(source):
        for old, new in sorted(moves, key=lambda pair: len(pair[0]), reverse=True):
            if source == old or source == old + '.meta' or source.startswith(old + '/'):
                return new + source[len(old):]
        return source

    destinations = {}
    for row in manifest['files']:
        try:
            source = safe(row['path'])
        except (ValueError, TypeError) as exc:
            errors.append(str(exc))
            continue
        destination = resolve(source)
        if source in removals:
            folder = root/source[:-5]
            if (root/source).exists() or folder.exists():
                errors.append(f'empty folder removal remains present: {source}')
            continue
        protected = source.startswith(('Assets/', 'Packages/', 'ProjectSettings/')) or source in {'.gitattributes', '.gitignore', 'Report.md', 'docs/Report.md'}
        if not protected and destination == source:
            continue
        if destination in destinations and destinations[destination] != source:
            errors.append(f'multiple baseline files map to destination: {destination}')
        destinations[destination] = source
        path = root/destination
        if path.is_file() != row['exists']:
            errors.append(f'baseline existence changed: {source} -> {destination}')
            continue
        if source != destination and (root/source).is_file():
            errors.append(f'moved source still exists: {source}')
        mutable = (destination.endswith('.asmdef') or
                   destination.startswith('Assets/Scripts/MotionMatching/Tests/') and destination.endswith('.cs') or
                   destination == 'Assets/Scripts/MotionMatching/Editor/AssemblyBoundaryAudit.cs' or
                   destination.startswith('docs/') and destination != 'docs/Report.md')
        if row['exists'] and not mutable and sha256(path) != row['sha256']:
            errors.append(f'baseline bytes changed: {source} -> {destination}')

    guids = {}
    for meta in (root/'Assets').rglob('*.meta'):
        match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(encoding='utf-8-sig'), re.MULTILINE)
        if match:
            guid = match[1]
            if guid in guids:
                errors.append(f'duplicate asset GUID: {meta.relative_to(root).as_posix()} and {guids[guid]}')
            guids[guid] = meta.relative_to(root).as_posix()
    for old_root in ('Assets/MotionMatching', 'Assets/Editor'):
        if (root/old_root).exists():
            errors.append(f'obsolete source root remains: {old_root}')
    return errors


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
    parser.add_argument('--mode', choices=('m0', 'm1', 'consolidation'), default='m0', help='Historical M0/M1 or GUID-preserving directory consolidation')
    parser.add_argument('--baseline', type=Path, help='Optional local preservation manifest for original asset/settings hashes')
    parser.add_argument('--move-map', type=Path, help='Consolidation source/destination mapping')
    args = parser.parse_args()
    failures = (validate_consolidation(baseline=args.baseline, move_map=args.move_map) if args.mode == 'consolidation'
                else (validate_m1 if args.mode == 'm1' else validate_m0)(baseline=args.baseline))
    for failure in failures:
        print('ERROR: ' + failure)
    if failures:
        raise SystemExit(1)
    print(f'{args.mode.upper()} static preservation passed. This does NOT test or compile Unity.')
