"""DEV-BUNDLE-FIXTURE-01 storage configuration units; no server or credentials."""
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import bundle_oidc_lab as bundle


class BundleObjectRootTests(unittest.TestCase):
    def test_dedicated_project_path_outside_repo_is_restricted_before_return(self):
        with tempfile.TemporaryDirectory() as directory:
            temporary=Path(directory);repo=temporary/'repository';repo.mkdir()
            with patch.object(bundle.tempfile,'gettempdir',return_value=str(temporary)), patch.object(bundle.lab,'ROOT',repo), patch.object(bundle.lab,'PROJECT','synthetic-project'), patch.object(bundle.lab,'private_directory') as restrict:
                result=bundle.source_object_root()
                self.assertEqual(temporary/'synthetic-project-source-bundle-objects',result)
                self.assertFalse(result.resolve().is_relative_to(repo.resolve()))
                restrict.assert_called_once_with(result)

    def test_repo_temp_override_is_rejected_without_creating_or_restricting_storage(self):
        with tempfile.TemporaryDirectory() as directory:
            repo=Path(directory)
            with patch.object(bundle.tempfile,'gettempdir',return_value=str(repo)), patch.object(bundle.lab,'ROOT',repo), patch.object(bundle.lab,'private_directory') as restrict:
                with self.assertRaisesRegex(RuntimeError,'object_boundary'):bundle.source_object_root()
                restrict.assert_not_called()

    def test_another_repository_ancestor_is_also_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            temporary=Path(directory);(temporary/'.git').write_text('synthetic git marker')
            with patch.object(bundle.tempfile,'gettempdir',return_value=str(temporary)), patch.object(bundle.lab,'ROOT',temporary/'other'), patch.object(bundle.lab,'private_directory') as restrict:
                with self.assertRaisesRegex(RuntimeError,'object_boundary'):bundle.source_object_root()
                restrict.assert_not_called()

    def test_existing_objects_are_retained_and_acl_is_reapplied(self):
        with tempfile.TemporaryDirectory() as directory:
            temporary=Path(directory);objects=temporary/'synthetic-project-source-bundle-objects';objects.mkdir()
            original=objects/'retained.object';original.write_bytes(b'synthetic retained transport')
            with patch.object(bundle.tempfile,'gettempdir',return_value=str(temporary)), patch.object(bundle.lab,'ROOT',temporary/'repo'), patch.object(bundle.lab,'PROJECT','synthetic-project'), patch.object(bundle.lab,'private_directory') as restrict:
                self.assertEqual(objects,bundle.source_object_root());restrict.assert_called_once_with(objects)
                self.assertEqual(b'synthetic retained transport',original.read_bytes())


if __name__=='__main__':unittest.main()
