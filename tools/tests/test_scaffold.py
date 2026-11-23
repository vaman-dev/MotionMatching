import sys
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
from validate_scaffold import validate, parser_skips

class TestScaffold(unittest.TestCase):
    def test_validation(self):
        self.assertEqual([], validate())

    def test_missing_agent_references_are_rejected_without_toml_parser(self):
        import validate_scaffold
        with TemporaryDirectory() as temporary:
            fixture = Path(temporary)
            agent = fixture/'.codex/agents/test_agent.toml'
            agent.parent.mkdir(parents=True)
            agent.write_text('name = "test_agent"', encoding='utf-8')
            with patch.object(validate_scaffold, 'ROOT', fixture), patch.object(validate_scaffold, 'tomllib', None):
                failures = validate()
            self.assertIn('missing agent reference test_agent: agents/test-agent.md', failures)
            self.assertIn('missing agent reference test_agent: .agents/skills/unity-test/SKILL.md', failures)

    def test_parser_limitations_are_reported(self):
        import validate_scaffold
        with patch.object(validate_scaffold, 'tomllib', None):
            self.assertTrue(any('TOML' in message for message in parser_skips()))

if __name__ == '__main__':
    unittest.main()
