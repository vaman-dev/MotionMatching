"""Static assembly-boundary checks. Does not compile Unity or discover Unity tests."""
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
BASE = Path('Assets/MotionMatching')
FOLDERS = {
    'MotionMatching.Core': 'Core',
    'MotionMatching.Runtime': 'Runtime',
    'MotionMatching.Editor': 'Editor',
    'MotionMatching.Tests.Editor': 'Tests/Editor',
    'MotionMatching.Tests.Runtime': 'Tests/Runtime',
}
PROJECT_REFERENCES = {
    'MotionMatching.Core': set(),
    'MotionMatching.Runtime': {'MotionMatching.Core'},
    'MotionMatching.Editor': {'MotionMatching.Core', 'MotionMatching.Runtime'},
    'MotionMatching.Tests.Editor': {'MotionMatching.Core', 'MotionMatching.Runtime', 'MotionMatching.Editor'},
    'MotionMatching.Tests.Runtime': {'MotionMatching.Core', 'MotionMatching.Runtime'},
}
PACKAGE_NAMESPACES = {
    'Unity.InputSystem': r'\bUnityEngine\s*\.\s*InputSystem\b',
    'Unity.Cinemachine': r'\bUnity\s*\.\s*Cinemachine\b',
}


def code_only(source):
    """Ignore comments and literals for the narrow namespace checks below."""
    return re.sub(r'//[^\n]*|/\*[\s\S]*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'',
                  ' ', source)


def validate_assemblies(root=ROOT):
    root = Path(root)
    errors, definitions, guids = [], {}, {}
    expected_paths = {root/BASE/folder/(name + '.asmdef'): name for name, folder in FOLDERS.items()}
    for folder in (root/'Assets', root/'Assets/Scripts'):
        for path in folder.glob('*.asmdef'):
            errors.append(f'assembly captures unrelated assets: {path.relative_to(root).as_posix()}')
    for path in (root/BASE).rglob('*.asmdef'):
        if path not in expected_paths:
            errors.append(f'unexpected assembly definition: {path.relative_to(root).as_posix()}')
    for path in (root/BASE).rglob('*.asmref'):
        errors.append(f'unexpected assembly ownership override: {path.relative_to(root).as_posix()}')
    for path, name in expected_paths.items():
        if not path.is_file():
            errors.append(f'missing assembly: {name}')
            continue
        try:
            data = json.loads(path.read_text(encoding='utf-8-sig'))
            if not isinstance(data, dict) or data.get('name') != name:
                raise ValueError('name must match expected assembly')
            for field in ('references', 'includePlatforms', 'excludePlatforms', 'defineConstraints', 'precompiledReferences'):
                value = data.get(field, [])
                if not isinstance(value, list) or not all(isinstance(item, str) for item in value):
                    raise ValueError(f'{field} must be an array of strings')
            definitions[name] = data
        except (ValueError, OSError) as exc:
            errors.append(f'invalid assembly {name}: {exc}')
        meta = Path(str(path) + '.meta')
        match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(encoding='utf-8'), re.MULTILINE) if meta.is_file() else None
        if not match:
            errors.append(f'missing or invalid assembly metadata: {name}')
        elif match[1] in guids:
            errors.append(f'duplicate assembly metadata GUID: {name}')
        else:
            guids[match[1]] = name

    graph = {}
    for name, data in definitions.items():
        refs = set()
        for reference in data.get('references', []):
            resolved = guids.get(reference[5:]) if reference.startswith('GUID:') else reference
            if resolved is None:
                errors.append(f'unresolved assembly GUID reference: {name}: {reference}')
            else:
                refs.add(resolved)
        graph[name] = refs & FOLDERS.keys()
        expected = PROJECT_REFERENCES[name]
        if graph[name] != expected:
            errors.append(f'project assembly references differ: {name}: expected {sorted(expected)}, got {sorted(graph[name])}')
        allowed = set(expected)
        is_test = name.startswith('MotionMatching.Tests.')
        is_editor = name.endswith('.Editor')
        if name == 'MotionMatching.Runtime':
            allowed.update(PACKAGE_NAMESPACES)
        if is_test:
            allowed.add('UnityEngine.TestRunner')
            if is_editor:
                allowed.add('UnityEditor.TestRunner')
        for reference in refs - allowed:
            errors.append(f'forbidden assembly reference: {name}: {reference}')
        if name == 'MotionMatching.Core' and data.get('noEngineReferences') is not True:
            errors.append('Core must set noEngineReferences: true')
        platforms = data.get('includePlatforms', [])
        if is_editor and (platforms != ['Editor'] or data.get('excludePlatforms', [])):
            errors.append(f'Editor assembly must include only Editor: {name}')
        if not is_editor and (platforms or data.get('excludePlatforms', [])):
            errors.append(f'runtime/Core assembly must be platform-independent: {name}')
        if is_test:
            if 'UNITY_INCLUDE_TESTS' not in data.get('defineConstraints', []):
                errors.append(f'test assembly requires UNITY_INCLUDE_TESTS: {name}')
            if data.get('autoReferenced') is not False:
                errors.append(f'test assembly requires autoReferenced: false: {name}')
            if data.get('overrideReferences') is not True or 'nunit.framework.dll' not in data.get('precompiledReferences', []):
                errors.append(f'test assembly requires explicit NUnit reference: {name}')
            runners = {'UnityEngine.TestRunner'} | ({'UnityEditor.TestRunner'} if is_editor else set())
            if not runners <= refs:
                errors.append(f'test assembly missing explicit runner references: {name}')

    active, visited = set(), set()
    def visit(name):
        if name in active:
            errors.append(f'assembly reference cycle: {name}')
            return
        if name in visited:
            return
        active.add(name)
        for dependency in graph.get(name, set()):
            visit(dependency)
        active.remove(name)
        visited.add(name)
    for name in graph:
        visit(name)

    runtime_sources = []
    for first_party in (root/'Assets/Scripts', root/BASE):
        for path in first_party.rglob('*.cs'):
            relative = path.relative_to(first_party)
            if 'Editor' in relative.parts:
                continue
            source = code_only(path.read_text(encoding='utf-8-sig'))
            if re.search(r'\bUnityEditor\b', source):
                errors.append(f'UnityEditor API in first-party runtime source: {path.relative_to(root).as_posix()}')
            if path.is_relative_to(root/BASE/'Runtime'):
                runtime_sources.append(source)
    for package, pattern in PACKAGE_NAMESPACES.items():
        refs = definitions.get('MotionMatching.Runtime', {}).get('references', [])
        if package in refs and not any(re.search(pattern, source) for source in runtime_sources):
            errors.append(f'unused Runtime package assembly reference: {package}')
    return errors


if __name__ == '__main__':
    failures = validate_assemblies()
    for failure in failures:
        print('ERROR: ' + failure)
    if failures:
        raise SystemExit(1)
    print('Assembly static validation passed. This does NOT compile Unity or discover Unity tests.')
