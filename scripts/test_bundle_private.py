"""Immutable private fixture config tests; no provider, credentials or host activation."""
from copy import deepcopy
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import bundle_private as store
import oidc_lab as lab


class BundlePrivateTests(unittest.TestCase):
    def setUp(self):
        self.directory=tempfile.TemporaryDirectory();self.root=Path(self.directory.name)
        self.scope=patch.object(lab,'PRIVATE',self.root);self.scope.start()
        _,_,_,self.config=lab.make_configuration()
    def tearDown(self):
        self.scope.stop();self.directory.cleanup()

    def test_publish_once_is_complete_and_never_clobbers_existing_target(self):
        path=self.root/'synthetic.json';store.publish_json(path,{'first':True})
        self.assertEqual({'first':True},store.read_json(path))
        with self.assertRaisesRegex(RuntimeError,'publish_conflict'):store.publish_json(path,{'second':True})
        self.assertEqual({'first':True},store.read_json(path))

    def test_publication_failure_leaves_no_final_or_partial_readable_target(self):
        path=self.root/'failed.json'
        with patch.object(store,'publish_temp',side_effect=OSError('synthetic publication interruption')):
            with self.assertRaises(OSError):store.publish_json(path,{'synthetic':True})
        self.assertFalse(path.exists());self.assertFalse(list(self.root.glob('.bundle-publish-*')))

    def test_source_and_target_must_be_private_regular_files_in_same_parent(self):
        source=self.root/'source.json';source.write_text('{}');(self.root/'child').mkdir()
        for target in (self.root.parent/'outside.json',self.root/'child/other.json',source):
            with self.assertRaises(RuntimeError):store.publish_temp(source,target)
        self.assertTrue(source.exists())

    def test_monotonic_chain_keeps_old_snapshot_and_selects_last_complete_configuration(self):
        first=store.publish_config(self.config);initial=first.read_bytes()
        changed=deepcopy(self.config);changed['Authentication']['Oidc']['Memberships'][0]['Permissions'].append('certificate.read')
        second=store.publish_config(changed)
        selected,path=store.selected();self.assertEqual(second,path);self.assertEqual(changed,selected)
        self.assertEqual(initial,first.read_bytes());self.assertNotEqual(first,second)
        self.assertEqual(2,len(list(self.root.glob('source-bundle-host-*.json'))))

    def test_database_oidc_tls_and_identity_changes_cannot_rotate_in_a_generation(self):
        store.publish_config(self.config)
        for change in ('database','secret','subject','actor','tenant','tls','authority'):
            candidate=deepcopy(self.config);oidc=candidate['Authentication']['Oidc']
            if change=='database':candidate['ConnectionStrings']['Poseidon']='synthetic replacement endpoint'
            if change=='secret':oidc['ClientSecret']='synthetic replacement value'
            if change=='subject':oidc['Memberships'][0]['Subject']='different-fixture-subject'
            if change=='actor':oidc['Memberships'][0]['ActorId']=oidc['Memberships'][1]['ActorId']
            if change=='tenant':oidc['Memberships'][0]['TenantId']=oidc['Memberships'][1]['TenantId']+'-different'
            if change=='tls':candidate['Kestrel']['Endpoints']['LocalLab']['Url']='https://localhost:5999'
            if change=='authority':oidc['Authority']='https://external.invalid'
            with self.assertRaisesRegex(RuntimeError,'identity_changed'):store.publish_config(candidate)
        self.assertEqual(self.config,store.selected()[0])

    def test_payload_or_manifest_tampering_fails_closed(self):
        path=store.publish_config(self.config);original=path.read_bytes();path.write_bytes(original+b' ')
        with self.assertRaisesRegex(RuntimeError,'tampered'):store.selected()
        path.write_bytes(original)
        manifest=self.root/'source-bundle-host-000001.json';data=json.loads(manifest.read_text());data['unexpected']=True;manifest.write_text(json.dumps(data))
        with self.assertRaisesRegex(RuntimeError,'chain_invalid'):store.selected()

    def test_missing_previous_manifest_or_orphan_candidate_is_not_accepted_as_latest(self):
        store.publish_config(self.config)
        candidate=deepcopy(self.config);candidate['SecureFiles']={'synthetic':True}
        with patch.object(store,'publish_json',side_effect=OSError('synthetic crash before manifest')):
            with self.assertRaises(OSError):store.publish_config(candidate)
        with self.assertRaisesRegex(RuntimeError,'missing_or_orphaned'):store.selected()

    def test_duplicate_json_key_and_ambiguous_generation_name_are_rejected(self):
        file=self.root/'duplicate.json';file.write_text('{"key":1,"key":2}')
        with self.assertRaisesRegex(RuntimeError,'duplicate_json'):store.read_json(file)
        (self.root/'source-bundle-host-uncontrolled.json').write_text('{}')
        with self.assertRaisesRegex(RuntimeError,'generation_name_invalid'):store.selected()

    def test_manifest_exposes_only_hashes_and_config_filename_not_secret_material(self):
        store.publish_config(self.config)
        raw=(self.root/'source-bundle-host-000001.json').read_text()
        self.assertNotIn(self.config['Authentication']['Oidc']['ClientSecret'],raw)
        self.assertNotIn(self.config['ConnectionStrings']['Poseidon'],raw)
        self.assertEqual({'schemaVersion','generation','configFile','configSha256','previousManifestSha256','immutableIdentitySha256'},set(json.loads(raw)))

    def test_empty_missing_and_outside_configurations_fail_before_host_selection(self):
        with self.assertRaisesRegex(RuntimeError,'generation_required'):store.selected()
        with self.assertRaisesRegex(RuntimeError,'boundary'):store.read_json(self.root.parent/'external.json')


if __name__=='__main__':unittest.main()
