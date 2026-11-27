"""Checks only the repository scaffold. It never claims Unity compile/test coverage."""
import json
from pathlib import Path
import re
import sys
try:
    import tomllib
except ImportError:  # Python 3.10 can still run JSON, file and migration checks.
    tomllib = None

ROOT = Path(__file__).resolve().parents[1]
REQUIRED = [
    'README.md', 'docs/STATUS.md', 'docs/PROJECT.md', 'docs/MotionMatchingArchitecture.md', 'docs/PLAN.md',
    'docs/Unity.md', 'docs/ReusablePackages.md', 'docs/PROGRESS.md', 'docs/Report.md', 'AGENTS.md',
    '.codex/config.toml',
    '.github/workflows/github.yml',
    '.github/codex-review-prompt.md',
    'docs/data/FEATURE_SCHEMA.md', 'config/motion-matching.sample.json',
]
ROLES = ['github_agent', 'bug_agent', 'test_agent', 'documentation_agent']
SKILLS = ['git-workflow', 'bug-triage', 'unity-test', 'docs-sync', 'motion-data-audit']
ROLE_SKILLS = dict(zip(ROLES, SKILLS))


def parser_skips():
    """Report unavailable checks; CI installs YAML and uses Python 3.12."""
    skips = []
    if tomllib is None:
        skips.append('TOML syntax parsing requires Python 3.11+ (file/reference checks still run).')
    try:
        import yaml
    except ImportError:
        skips.append('Workflow YAML syntax parsing requires PyYAML (installed in CI).')
    return skips


def validate():
    errors = []
    for name in REQUIRED:
        if not (ROOT/name).is_file():
            errors.append(f'missing: {name}')
    for role in ROLES:
        file = ROOT / '.codex' / 'agents' / f'{role}.toml'
        if not file.is_file():
            errors.append(f'missing agent TOML: {file.relative_to(ROOT)}')
            continue
        content = file.read_text(encoding='utf-8')
        playbook = f'agents/{role.replace("_", "-")}.md'
        skill = f'.agents/skills/{ROLE_SKILLS[role]}/SKILL.md'
        for reference in (playbook, skill):
            if reference not in content or not (ROOT/reference).is_file():
                errors.append(f'missing agent reference {role}: {reference}')
        try:
            if tomllib is None:
                continue
            data = tomllib.loads(content)
            if data.get('name') != role or not data.get('description') or not data.get('developer_instructions'):
                errors.append(f'incomplete agent TOML: {role}')
        except Exception as exc:
            errors.append(f'invalid TOML {role}: {exc}')
    for name in SKILLS:
        file = ROOT / '.agents' / 'skills' / name / 'SKILL.md'
        if not file.is_file():
            errors.append(f'missing skill: {name}')
            continue
        content = file.read_text(encoding='utf-8')
        if not re.match(r'\A---\nname: [^\n]+\ndescription: [^\n]+\n---\n', content):
            errors.append(f'invalid skill frontmatter: {name}')
    config = ROOT / '.codex' / 'config.toml'
    if config.exists() and tomllib is not None:
        try: tomllib.loads(config.read_text(encoding='utf-8'))
        except Exception as exc: errors.append(f'invalid config: {exc}')
    dataset = ROOT / 'config' / 'motion-matching.sample.json'
    if dataset.exists():
        try:
            d = json.loads(dataset.read_text(encoding='utf-8'))
            dims = sum(group['dims'] for group in d['featureGroups'])
            if dims != d['featureDimension'] or dims != 27:
                errors.append(f'feature dimensions mismatch: {dims}')
        except Exception as exc: errors.append(f'invalid sample config: {exc}')
    for workflow in (ROOT / '.github/workflows').glob('*.yml'):
        try:
            import yaml  # optional locally; installed in CI
        except ImportError:
            break
        try:
            data = yaml.safe_load(workflow.read_text(encoding='utf-8'))
            if not isinstance(data, dict) or 'jobs' not in data:
                errors.append(f'invalid workflow: {workflow.name}')
        except Exception as exc:
            errors.append(f'workflow YAML parse failed {workflow.name}: {exc}')
    return errors


if __name__ == '__main__':
    for skip in parser_skips():
        print('SKIP: ' + skip)
    failures = validate()
    if failures:
        print('\n'.join('ERROR: '+x for x in failures), file=sys.stderr)
        raise SystemExit(1)
    print('Scaffold validation passed. This does NOT test or compile Unity.')
