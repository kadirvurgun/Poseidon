"""Synthetic protocol/transport tests. No OCR, real approval or signed host acceptance."""
import hashlib
import http.client
from http.server import ThreadingHTTPServer
import json
import threading
import unittest
import uuid

import bundle_extraction_peer as peer


def multipart(contract=None, document=peer.FIXTURE, duplicate=False):
    contract = contract or {'protocol':peer.PROTOCOL,'modelVersion':peer.MODEL,'promptVersion':peer.PROMPT,
        'inputSha256':hashlib.sha256(document).hexdigest().upper(),'fields':peer.FIELDS}
    boundary='synthetic-boundary'
    part=lambda name,mime,body: ('--'+boundary+'\r\nContent-Disposition: form-data; name="'+name+'"\r\nContent-Type: '+mime+'\r\n\r\n').encode()+body+b'\r\n'
    body=part('contract','application/json',json.dumps(contract).encode())+part('document','image/png',document)
    if duplicate:body+=part('document','image/png',document)
    return 'multipart/form-data; boundary='+boundary,body+('--'+boundary+'--\r\n').encode()


class ExtractionProtocolPeerTests(unittest.TestCase):
    def test_actual_received_synthetic_bytes_and_contract_bind_response_hash(self):
        kind,body=multipart();vessel=str(uuid.uuid4())
        response=peer.protocol_response(kind,body,vessel)
        self.assertEqual(hashlib.sha256(peer.FIXTURE).hexdigest().upper(),response['inputSha256'])
        self.assertEqual(peer.PROTOCOL,response['protocol'])
        self.assertEqual(vessel,response['fields']['vessel_reference']['value'])
        self.assertEqual('SYNTHETIC-PROTOCOL-CANDIDATE',response['fields']['certificate_no']['value'])
        self.assertNotIn('verified',response);self.assertNotIn('readiness',response)

    def test_real_or_altered_documents_are_rejected_even_when_hash_matches(self):
        kind,body=multipart(document=b'not-the-public-synthetic-fixture')
        with self.assertRaisesRegex(ValueError,'synthetic_fixture_only'):
            peer.protocol_response(kind,body,str(uuid.uuid4()))

    def test_model_prompt_checksum_fields_and_extra_metadata_cannot_escape_contract(self):
        for key,value in [('modelVersion','other'),('promptVersion','other'),('inputSha256','A'*64),('fields',['issuer']),('unexpected',True)]:
            contract={'protocol':peer.PROTOCOL,'modelVersion':peer.MODEL,'promptVersion':peer.PROMPT,
                'inputSha256':hashlib.sha256(peer.FIXTURE).hexdigest().upper(),'fields':peer.FIELDS}
            contract[key]=value;kind,body=multipart(contract)
            with self.assertRaisesRegex(ValueError,'invalid_contract'):
                peer.protocol_response(kind,body,str(uuid.uuid4()))

    def test_duplicate_json_and_multipart_parts_fail_closed(self):
        with self.assertRaisesRegex(ValueError,'duplicate_key'):
            peer.closed_object([('key',1),('key',2)])
        kind,body=multipart(duplicate=True)
        with self.assertRaisesRegex(ValueError,'invalid_part'):
            peer.protocol_response(kind,body,str(uuid.uuid4()))

    def test_nonmultipart_and_over_budget_requests_rejected(self):
        for kind,body in [('application/json',b'{}'),('multipart/form-data; boundary=x',b'x'*(peer.MAX_REQUEST_BYTES+1))]:
            with self.assertRaisesRegex(ValueError,'invalid_transport'):
                peer.protocol_response(kind,body,str(uuid.uuid4()))

    def test_policy_only_binds_actual_planned_actor_ship_and_synthetic_loopback_endpoint(self):
        plan={'schemaVersion':'poseidon.bundle-fixture-plan.v1',**{key:str(uuid.uuid4()) for key in ('tenantId','writerActorId','vesselId')}}
        policy=peer.fixture_policy(plan,55123)
        self.assertEqual('https://localhost:55123/v1/extract',policy['Endpoint'])
        self.assertEqual('localhost',policy['AllowedHost'])
        self.assertEqual([{'TenantId':plan['tenantId'],'ActorId':plan['writerActorId'],'VesselId':plan['vesselId'],'Scope':'Vessel'}],policy['Scopes'])
        self.assertTrue(policy['ProcessingReference'].startswith('SYNTHETIC'))
        self.assertNotIn('PersonalProcessingReference',policy)
        for port in (0,65536,'55123'):
            with self.assertRaises(ValueError):peer.fixture_policy(plan,port)

    def test_actual_http_handler_requires_secret_and_correct_path_without_header_diagnostics(self):
        secret='synthetic-unit-secret';vessel=str(uuid.uuid4());server=ThreadingHTTPServer(('127.0.0.1',0),peer.handler(secret,vessel))
        thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start()
        try:
            kind,body=multipart()
            for path,auth,expected in [('/v1/extract',None,403),('/other','Bearer '+secret,403),('/v1/extract','Bearer wrong',403),('/v1/extract','Bearer '+secret,200)]:
                client=http.client.HTTPConnection('127.0.0.1',server.server_port,timeout=5)
                headers={'Content-Type':kind}
                if auth:headers['Authorization']=auth
                client.request('POST',path,body,headers);response=client.getresponse();actual=response.read();client.close()
                self.assertEqual(expected,response.status)
                self.assertNotIn(secret.encode(),actual)
                if expected==200:self.assertEqual(vessel,json.loads(actual)['fields']['vessel_reference']['value'])
        finally:
            server.shutdown();server.server_close();thread.join(timeout=5)


if __name__=='__main__':unittest.main()
