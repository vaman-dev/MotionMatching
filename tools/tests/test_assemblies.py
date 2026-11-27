import json
from pathlib import Path
import sys
from tempfile import TemporaryDirectory
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from validate_assemblies import BASE, FOLDERS, PROJECT_REFERENCES, validate_assemblies


class TestAssemblyBoundaries(unittest.TestCase):
    def setUp(self):
        self.temporary = TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        for index, (name, folder) in enumerate(FOLDERS.items(), start=1):
            data = {'name': name, 'references': sorted(PROJECT_REFERENCES[name])}
            if name == 'MotionMatching.Core':
                data['noEngineReferences'] = True
            if name.endswith('.Editor'):
                data['includePlatforms'] = ['Editor']
            if name.startswith('MotionMatching.Tests.'):
                data.update(autoReferenced=False, defineConstraints=['UNITY_INCLUDE_TESTS'],
                            overrideReferences=True, precompiledReferences=['nunit.framework.dll'])
                data['references'].append('UnityEngine.TestRunner')
                if name.endswith('.Editor'):
                    data['references'].append('UnityEditor.TestRunner')
            path = self.path(name)
            path.parent.mkdir(parents=True)
            path.write_text(json.dumps(data), encoding='utf-8')
            Path(str(path) + '.meta').write_text(f'guid: {index:032x}\n', encoding='utf-8')

    def path(self, name):
        return self.root/BASE/FOLDERS[name]/(name + '.asmdef')

    def change(self, name, **fields):
        path = self.path(name)
        data = json.loads(path.read_text(encoding='utf-8'))
        data.update(fields)
        path.write_text(json.dumps(data), encoding='utf-8')

    def test_valid_five_assembly_graph(self):
        self.assertEqual([], validate_assemblies(self.root))

    def test_project_cycle_is_detected(self):
        self.change('MotionMatching.Core', references=['MotionMatching.Runtime'])
        self.assertTrue(any('assembly reference cycle:' in failure for failure in validate_assemblies(self.root)))

    def test_runtime_editor_reference_and_api_are_rejected(self):
        self.change('MotionMatching.Runtime', references=['MotionMatching.Core', 'UnityEditor.TestRunner'])
        source = self.path('MotionMatching.Runtime').parent/'Leak.cs'
        source.write_text('using UnityEditor; class Leak {}', encoding='utf-8')
        failures = validate_assemblies(self.root)
        self.assertIn('forbidden assembly reference: MotionMatching.Runtime: UnityEditor.TestRunner', failures)
        self.assertTrue(any('UnityEditor API in first-party runtime source:' in failure for failure in failures))

    def test_editor_sources_and_comments_do_not_trigger_runtime_leak(self):
        (self.path('MotionMatching.Editor').parent/'Tool.cs').write_text('using UnityEditor;', encoding='utf-8')
        (self.path('MotionMatching.Runtime').parent/'Clean.cs').write_text('// UnityEditor\nclass Clean { string label = "UnityEditor"; }', encoding='utf-8')
        self.assertEqual([], validate_assemblies(self.root))

    def test_runtime_test_gate_and_runner_are_required(self):
        name = 'MotionMatching.Tests.Runtime'
        self.change(name, defineConstraints=[], autoReferenced=True, references=sorted(PROJECT_REFERENCES[name]))
        failures = validate_assemblies(self.root)
        self.assertIn(f'test assembly requires UNITY_INCLUDE_TESTS: {name}', failures)
        self.assertIn(f'test assembly requires autoReferenced: false: {name}', failures)
        self.assertIn(f'test assembly missing explicit runner references: {name}', failures)

    def test_vendor_capture_and_neural_definition_are_rejected(self):
        (self.root/'Assets/VendorCapture.asmdef').write_text('{}', encoding='utf-8')
        (self.root/BASE/'Neural').mkdir()
        (self.root/BASE/'Neural/MotionMatching.Neural.asmdef').write_text('{}', encoding='utf-8')
        failures = validate_assemblies(self.root)
        self.assertTrue(any('assembly captures unrelated assets:' in failure for failure in failures))
        self.assertTrue(any('MotionMatching.Neural.asmdef' in failure for failure in failures))

    def test_guid_project_references_resolve_and_predefined_do_not(self):
        self.change('MotionMatching.Runtime', references=['GUID:' + f'{1:032x}'])
        self.assertEqual([], validate_assemblies(self.root))
        self.change('MotionMatching.Runtime', references=['MotionMatching.Core', 'Assembly-CSharp'])
        self.assertIn('forbidden assembly reference: MotionMatching.Runtime: Assembly-CSharp', validate_assemblies(self.root))

    def test_package_reference_requires_runtime_namespace_usage(self):
        self.change('MotionMatching.Runtime', references=['MotionMatching.Core', 'Unity.InputSystem'])
        self.assertIn('unused Runtime package assembly reference: Unity.InputSystem', validate_assemblies(self.root))
        (self.path('MotionMatching.Runtime').parent/'Input.cs').write_text('using UnityEngine.InputSystem;', encoding='utf-8')
        self.assertEqual([], validate_assemblies(self.root))

    def test_editor_package_usage_requires_reference_and_rejects_unused_reference(self):
        path = self.path('MotionMatching.Editor').parent/'Capture.cs'
        path.write_text('using UnityEngine.InputSystem;', encoding='utf-8')
        self.assertIn('missing package assembly reference: MotionMatching.Editor: Unity.InputSystem', validate_assemblies(self.root))
        self.change('MotionMatching.Editor', references=['MotionMatching.Core', 'MotionMatching.Runtime', 'Unity.InputSystem'])
        self.assertEqual([], validate_assemblies(self.root))
        path.write_text('class Capture {}', encoding='utf-8')
        self.assertIn('unused Editor package assembly reference: Unity.InputSystem', validate_assemblies(self.root))

    def test_orphan_first_party_source_and_unowned_base_source_are_rejected(self):
        orphan = self.root/'Assets/Editor/OldCapture.cs'
        orphan.parent.mkdir(parents=True)
        orphan.write_text('class OldCapture {}', encoding='utf-8')
        (self.root/BASE/'Unowned.cs').write_text('class Unowned {}', encoding='utf-8')
        errors = validate_assemblies(self.root)
        self.assertEqual(2, sum('outside an owned assembly' in error for error in errors))

    def test_reviewed_vendor_sources_are_exempt_but_adjacent_project_code_is_not(self):
        vendor = self.root/'Assets/Kevin Iglesias/Human Animations/Scripts/SpineProxy.cs'
        vendor.parent.mkdir(parents=True)
        vendor.write_text('class SpineProxy {}', encoding='utf-8')
        self.assertEqual([], validate_assemblies(self.root))
        (vendor.parent/'Unreviewed.cs').write_text('class Unreviewed {}', encoding='utf-8')
        self.assertTrue(any('outside an owned assembly' in error for error in validate_assemblies(self.root)))

    def test_core_api_and_nested_editor_leak_are_rejected(self):
        (self.path('MotionMatching.Core').parent/'EngineLeak.cs').write_text('using UnityEngine;', encoding='utf-8')
        editor = self.path('MotionMatching.Runtime').parent/'Editor/Leak.cs'
        editor.parent.mkdir()
        editor.write_text('class Leak {}', encoding='utf-8')
        errors = validate_assemblies(self.root)
        self.assertTrue(any('engine-free Core' in error for error in errors))
        self.assertTrue(any('Editor source owned by non-Editor assembly' in error for error in errors))


if __name__ == '__main__':
    unittest.main()
