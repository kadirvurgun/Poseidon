"""Explicit disposable TLS protocol peer; NO OCR accuracy or document approval.

Accepts only the exact public one-pixel synthetic software fixture. Never sends
bytes to an external service, emits raw diagnostics, or signs operational facts.
19.01 paragraphs68-123/189-245; DEV045 certificate extraction protocol contract.
"""
import argparse
import base64
from datetime import datetime, timezone
from email import policy
from email.parser import BytesParser
import hashlib
import hmac
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import os
from pathlib import Path
import secrets
import ssl
import uuid

PROTOCOL = 'poseidon.certificate-extraction.v1'
SOURCE = '18.01:09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674:46-47;14.02:1634-1657:v46.3'
MODEL = 'SYNTHETIC-protocol-peer-v1'
PROMPT = 'SYNTHETIC-no-ocr-v1'
FIELDS = ['certificate_type','certificate_no','issuer','issue_date','expiry_date','vessel_reference','person_reference']
FIXTURE = base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGMQUDD4DwACBAFgjby7cQAAAABJRU5ErkJggg==')
SECRET_REFERENCE = 'POSEIDON_BUNDLE_EXTRACTION_SECRET'
MAX_REQUEST_BYTES = 16384


def closed_object(pairs):
    result = {}
    for key,value in pairs:
        if key in result:
            raise ValueError('duplicate_key')
        result[key]=value
    return result


def protocol_response(content_type, body, vessel):
    if not isinstance(content_type,str) or not content_type.startswith('multipart/form-data;') or len(content_type)>256 or len(body)>MAX_REQUEST_BYTES:
        raise ValueError('invalid_transport')
    message=BytesParser(policy=policy.default).parsebytes(('Content-Type: '+content_type+'\r\nMIME-Version: 1.0\r\n\r\n').encode()+body)
    if not message.is_multipart() or message.defects:
        raise ValueError('invalid_multipart')
    parts={}
    for part in message.iter_parts():
        name=part.get_param('name',header='content-disposition')
        if part.defects or part.is_multipart() or part.get_content_disposition()!='form-data' or name not in ('contract','document') or name in parts:
            raise ValueError('invalid_part')
        parts[name]=part
    if set(parts)!= {'contract','document'} or parts['contract'].get_content_type()!='application/json' or parts['document'].get_content_type()!='image/png':
        raise ValueError('invalid_parts')
    document=parts['document'].get_payload(decode=True)
    if not hmac.compare_digest(document,FIXTURE):
        raise ValueError('synthetic_fixture_only')
    raw=parts['contract'].get_payload(decode=True)
    if len(raw)>4096:
        raise ValueError('invalid_contract')
    contract=json.loads(raw,object_pairs_hook=closed_object)
    checksum=hashlib.sha256(document).hexdigest().upper()
    expected={'protocol':PROTOCOL,'modelVersion':MODEL,'promptVersion':PROMPT,'inputSha256':checksum,'fields':FIELDS}
    if contract!=expected:
        raise ValueError('invalid_contract')
    return {'protocol':PROTOCOL,'modelVersion':MODEL,'promptVersion':PROMPT,'inputSha256':checksum,
        'fields': {'certificate_no':{'value':'SYNTHETIC-PROTOCOL-CANDIDATE','confidence':0.75,'page':1},
                   'vessel_reference':{'value':vessel,'confidence':0.75,'page':1}}}


def fixture_policy(plan,port):
    if (plan.get('schemaVersion')!='poseidon.bundle-fixture-plan.v1' or not isinstance(port,int) or not 1<=port<=65535 or
            any(str(uuid.UUID(plan[key]))!=plan[key] or uuid.UUID(plan[key]).int==0 for key in ('tenantId','writerActorId','vesselId'))):
        raise ValueError('fixture_plan_invalid')
    return {'Enabled':True,'Endpoint':f'https://localhost:{port}/v1/extract','AllowedHost':'localhost',
        'SecretReference':SECRET_REFERENCE,'PolicyVersion':'SYNTHETIC-protocol-only-v1',
        'ProcessingReference':'SYNTHETIC local protocol test; no OCR or external processing',
        'StorageReference':'SYNTHETIC disposable transport only','Region':'SYNTHETIC loopback',
        'PrivacyReference':'SYNTHETIC public pixel only','RetentionReference':'SYNTHETIC no document persistence',
        'ConfidenceReference':'SYNTHETIC arbitrary protocol number; not model accuracy','SourceVersion':SOURCE,
        'ModelVersion':MODEL,'PromptVersion':PROMPT,'LowConfidenceThreshold':0.8,'TimeoutSeconds':15,
        'ReviewSeconds':600,'MaxBytes':1048576,'MaxResponseBytes':16384,
        'Scopes':[{'TenantId':plan['tenantId'],'ActorId':plan['writerActorId'],'VesselId':plan['vesselId'],'Scope':'Vessel'}]}


def handler(secret,vessel):
    class Handler(BaseHTTPRequestHandler):
        protocol_version='HTTP/1.1'
        def log_message(self,*args):
            pass
        def setup(self):
            super().setup();self.connection.settimeout(10)
        def reply(self,status,value=None):
            payload=json.dumps(value if value is not None else {'code':'synthetic_protocol_rejected'},separators=(',',':')).encode()
            self.send_response(status);self.send_header('Content-Type','application/json');self.send_header('Content-Length',str(len(payload)));self.send_header('Connection','close');self.end_headers();self.wfile.write(payload);self.close_connection=True
        def do_POST(self):
            try:
                if self.path!='/v1/extract' or self.headers.get('Transfer-Encoding') or len(self.headers.get_all('Authorization',[]))!=1 or not hmac.compare_digest(self.headers.get('Authorization',''),'Bearer '+secret):
                    return self.reply(403)
                lengths=self.headers.get_all('Content-Length',[])
                if len(lengths)!=1 or not lengths[0].isdigit() or not 1<=int(lengths[0])<=MAX_REQUEST_BYTES:
                    return self.reply(400)
                body=self.rfile.read(int(lengths[0]))
                if len(body)!=int(lengths[0]):
                    return self.reply(400)
                value=protocol_response(self.headers.get('Content-Type',''),body,vessel)
                return self.reply(200,value)
            except Exception:
                try:self.reply(400)
                except Exception:pass
        def do_GET(self):self.reply(405)
        def do_PUT(self):self.reply(405)
    return Handler


def main(argv=None):
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--private',type=Path,required=True)
    parser.add_argument('--owner',required=True)
    args=parser.parse_args(argv)
    server=None
    try:
        from oidc_lab import PRIVATE, atomic_private_json, write
        private=args.private.resolve()
        if args.private.is_symlink() or private!=PRIVATE.resolve() or str(uuid.UUID(args.owner))!=args.owner:
            raise ValueError('private_boundary')
        plan=json.loads((private/'source-bundle-plan.json').read_text())
        config=json.loads((private/'appsettings.json').read_text())
        if config.get('CertificateExtraction') or (private/'extraction-peer-ready.json').exists():
            raise ValueError('existing_operator_policy_preserved')
        secret=secrets.token_urlsafe(48)
        context=ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER);context.minimum_version=ssl.TLSVersion.TLSv1_2
        context.load_cert_chain(private/'tls.pem',private/'tls.key')
        server=ThreadingHTTPServer(('127.0.0.1',0),handler(secret,plan['vesselId']));server.daemon_threads=True
        server.socket=context.wrap_socket(server.socket,server_side=True)
        extraction=fixture_policy(plan,server.server_port)
        config['CertificateExtraction']=extraction
        config.setdefault('DevelopmentLab',{})['CertificateExtractionCaPath']=str(private/'ca.pem')
        write(private/'extraction-peer-secret',secret)
        atomic_private_json(private/'appsettings.json',config)
        atomic_private_json(private/'extraction-peer-ready.json',{'schemaVersion':'poseidon.synthetic-extraction-peer.v1',
            'owner':args.owner,'pid':os.getpid(),'port':server.server_port,'modelVersion':MODEL,'promptVersion':PROMPT,
            'protocolOnly':True,'ocrAccuracyAsserted':False,'productionApproval':False,
            'startedAt':datetime.now(timezone.utc).isoformat()})
        server.serve_forever(poll_interval=0.5)
        return 0
    except Exception:
        # Child runs hidden; never emit TLS paths, provider configuration or data.
        return 1
    finally:
        if server:server.server_close()


if __name__=='__main__':raise SystemExit(main())
