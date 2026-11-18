import unittest

from source_manifest_delta import compare


class SourceManifestDeltaTests(unittest.TestCase):
    def test_added_changed_removed_and_unchanged_are_distinct(self):
        previous = {'baseline_id': 'old', 'files': [
            {'source_path': 'a', 'sha256': '1'}, {'source_path': 'b', 'sha256': '2'}, {'source_path': 'c', 'sha256': '3'}]}
        current = {'baseline_id': 'new', 'files': [
            {'source_path': 'a', 'sha256': '1'}, {'source_path': 'b', 'sha256': 'changed'}, {'source_path': 'd', 'sha256': '4'}]}
        result = compare(previous, current)
        self.assertEqual(result['added'], ['d']); self.assertEqual(result['removed'], ['c'])
        self.assertEqual(result['changed'], ['b']); self.assertEqual(result['unchanged'], 1)

    def test_duplicate_paths_fail_closed(self):
        duplicate = {'baseline_id': 'x', 'files': [{'source_path': 'a', 'sha256': '1'}, {'source_path': 'a', 'sha256': '1'}]}
        with self.assertRaisesRegex(ValueError, 'Duplicate'):
            compare(duplicate, {'baseline_id': 'y', 'files': []})


if __name__ == '__main__':
    unittest.main()
