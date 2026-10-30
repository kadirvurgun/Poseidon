#!/usr/bin/env python3
"""Explicit, same-host synthetic rehearsal. No DROP, existing DB restore, or real file inputs.
Requires installed dotnet/PostgreSQL tools and POSEIDON_TEST_POSTGRES in the environment.
Private dumps/objects stay in a newly allocated temporary directory; only results/ is deliverable.
"""
import ipaddress, stat, urllib.parse
import argparse, base64, hashlib, json, os, pathlib, re, shutil, subprocess, tempfile, time, urllib.request, urllib.error, uuid

BASE = '253a5d3a96542cfde65118898abfe90b358c1943'
REPO = pathlib.Path(__file__).resolve().parents[2]
TABLES = ['companies','drafts','audit_events','idempotency','poseidon_migrations','evidence_intakes']
PDF = b'%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n'
PNG = base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aL1cAAAAASUVORK5CYII=')

def sha(data): return hashlib.sha256(data).hexdigest()
def newkey(): return str(uuid.uuid4())

def validated_connection(raw, inherited):
 # Pure preflight: no subprocess, database, DNS or new resource before validation.
 if not raw or any(c in raw for c in ['"', "'", '\n', '\r', '\x00']):
  raise RuntimeError('Explicit simple test connection required.')
 cfg={}
 for part in raw.split(';'):
  if not part.strip():continue
  if '=' not in part:raise RuntimeError('Malformed connection.')
  k,v=part.split('=',1);k=k.strip().lower()
  if k in cfg or k not in {'host','port','username','password','database'}:raise RuntimeError('Duplicate or unsupported connection key.')
  cfg[k]=v
 if not re.fullmatch(r'poseidon_test(?:_[a-z0-9_]+)?',cfg.get('database','')):raise RuntimeError('Dedicated test database required.')
 if not cfg.get('username') or not cfg.get('host') or not re.fullmatch(r'[0-9]{1,5}',cfg.get('port','')) or not 1<=int(cfg['port'])<=65535:raise RuntimeError('Explicit host, port and username required.')
 host=cfg['host'];port=cfg['port']
 if host.startswith('/'):
  path=pathlib.Path(host)
  if str(path)!=host or '..' in path.parts:raise RuntimeError('Noncanonical socket directory.')
  # Filesystem root is the namespace trust anchor (its UID may be unmapped).
  # Every descendant must be owned by this user/root and never group/world writable.
  for directory in [*reversed(path.parents),path]:
   info=directory.lstat()
   if not stat.S_ISDIR(info.st_mode) or (directory!=pathlib.Path('/') and info.st_uid not in {0,os.getuid()}) or info.st_mode & 0o022:raise RuntimeError('Untrusted socket directory.')
  info=(path/('.s.PGSQL.'+port)).lstat()
  if not stat.S_ISSOCK(info.st_mode) or info.st_uid not in {0,os.getuid()}:raise RuntimeError('Verified local socket required.')
  locality={'transport':'LOCAL_UNIX_SOCKET','directoryVerified':True,'socketOwnerVerified':True,'port':int(port)}
 else:
  try:address=ipaddress.ip_address(host)
  except ValueError:raise RuntimeError('Literal loopback required; DNS and multi-host endpoints unsupported.') from None
  if not address.is_loopback or '%' in host:raise RuntimeError('Only loopback endpoints accepted.')
  locality={'transport':'LITERAL_LOOPBACK','address':str(address),'port':int(port)}
 mapping={'host':'PGHOST','port':'PGPORT','username':'PGUSER','password':'PGPASSWORD','database':'PGDATABASE'}
 allowed={v:cfg.get(k,'') for k,v in mapping.items()}
 for key,value in inherited.items():
  if key.startswith('PG') and value and (key not in allowed or value!=allowed[key]):raise RuntimeError('Conflicting or unsupported inherited libpq override.')
 env={k:v for k,v in inherited.items() if not k.startswith('PG')}
 env.update(allowed)
 return cfg,env,locality

class NoRedirect(urllib.request.HTTPRedirectHandler):
 def redirect_request(self,req,fp,code,msg,headers,newurl):
  raise RuntimeError('Rehearsal HTTP redirects are forbidden.')

def loopback_request(url, **kwargs):
 parsed=urllib.parse.urlsplit(url)
 try:local=ipaddress.ip_address(parsed.hostname or '').is_loopback
 except ValueError:local=False
 if parsed.scheme!='http' or not local or parsed.username or parsed.password or not parsed.port or parsed.fragment:
  raise RuntimeError('Explicit loopback HTTP endpoint required.')
 request=urllib.request.Request(url,**kwargs)
 # Empty ProxyHandler disables ambient proxy settings including system discovery.
 return urllib.request.build_opener(urllib.request.ProxyHandler({}),NoRedirect()).open(request,timeout=15)

class Rehearsal:
 def __init__(self):
  self.cfg,self.env,self.locality=validated_connection(os.environ.get('POSEIDON_TEST_POSTGRES',''),os.environ)
  self.private=pathlib.Path(tempfile.mkdtemp(prefix='poseidon_restore_rehearsal_'))
  self.results=self.private/'results';self.results.mkdir(mode=0o700)
  self.runid=uuid.uuid4().hex[:16];self.owned=[];self.commands=[];self.checks=[];self.apps=[];self.http=[]
  self.prefix='poseidon_test_restore_'+self.runid
  self.save_ownership()
 def save_ownership(self):
  (self.private/'task-owned.json').write_text(json.dumps({'runId':self.runid,'baseline':BASE,'databases':self.owned,'privateDirectory':str(self.private),'cleanup':'No automatic deletion. Only these newly created resources belong to this run.'},indent=2))
 def record(self,name,**details):self.checks.append({'check':name,'status':'PASS',**details});self.write_results()
 def write_results(self,status='RUNNING',error=None):
  result={'baseline':BASE,'runId':self.runid,'status':status,'checks':self.checks,'error':error,'commands':self.commands,'httpChecks':self.http,'gates':{'locality':self.locality,'offsiteDr':'NOT_RUN','hostPowerLoss':'NOT_RUN','nativeTls':'NOT_RUN','realIdp':'NOT_RUN','roleAclTablespaceRestore':'NOT_RUN_NO_GRANTS_CHANGED','productionRpoRtoSla':'NOT_ESTABLISHED','realCustomerData':'NOT_USED','scanner':'NOT_CONFIGURED','downloadPreviewUse':'BLOCKED','userAccess':'NOT_ESTABLISHED','userAcceptance':'PENDING','runtimeTamperAfterSuccessfulStartup':'NOT_RUN_STARTUP_CHECK_ONLY','fullUnitAndBrowserSuites':'NOT_RERUN_PRODUCT_SOURCE_UNCHANGED'},'retention':'Task-owned synthetic DBs, private dump and object copies retained locally; no automatic DROP/cleanup.'}
  (self.results/'result.json').write_text(json.dumps(result,indent=2)+'\n')
 def command(self,args,env=None,timeout=60):
  # Arguments contain no credentials; private file locations are replaced in exported command evidence.
  safe=[str(a).replace(str(self.private),'<TASK_PRIVATE>') for a in args]
  r=subprocess.run(args,env=env or self.env,capture_output=True,timeout=timeout)
  self.commands.append({'argv':safe,'exitCode':r.returncode,'stdoutSha256':sha(r.stdout),'stderrSha256':sha(r.stderr),'stderrBytes':len(r.stderr)})
  if r.returncode:
   reason='permission denied' if b'permission denied' in r.stderr.lower() else 'command rejected or failed (private diagnostic retained)'
   (self.private/'last-command.stderr').write_bytes(r.stderr)
   raise RuntimeError(f'{pathlib.Path(args[0]).name}: {reason}; exit={r.returncode}')
  return r.stdout.decode()
 def sql(self,db,sql):
  env=self.env|{'PGDATABASE':db}
  return self.command(['psql','-XAt','-v','ON_ERROR_STOP=1','-c',sql],env)
 def create_db(self,suffix):
  name=self.prefix+'_'+suffix
  assert re.fullmatch(r'poseidon_test_restore_[a-f0-9]{16}_[a-z]+',name)
  exists=self.sql(self.cfg['database'],f"SELECT count(*) FROM pg_database WHERE datname='{name}'").strip()
  if exists!='0':raise RuntimeError('Generated database already exists; refusing to reuse it.')
  self.sql(self.cfg['database'],f'CREATE DATABASE {name} TEMPLATE template0')
  self.owned.append(name);self.save_ownership();return name
 def root(self,label):
  p=self.private/label;p.mkdir(mode=0o700);return p
 def connection(self,db):
  assert db in self.owned
  keys={'host':'Host','port':'Port','username':'Username','password':'Password'}
  return ';'.join([f'{v}={self.cfg[k]}' for k,v in keys.items() if k in self.cfg]+[f'Database={db}'])
 def snapshot(self,db):
  result={}
  for table in TABLES:
   rows=self.sql(db,f'SELECT row_to_json(t)::text FROM {table} t ORDER BY row_to_json(t)::text')
   result[table]={'rows':len(rows.splitlines()),'sha256':sha(rows.encode())}
  result['quota']=self.sql(db,"SELECT tenant_id,sum(expected_bytes),count(*) FILTER(WHERE state IN ('RESERVED','RECEIVING')) FROM evidence_intakes GROUP BY tenant_id ORDER BY tenant_id").strip()
  return result
 def files(self,root):
  result={}
  for p in sorted(root.iterdir()):
   assert p.is_file() and not p.is_symlink() and re.fullmatch('[a-f0-9]{32}\\.quarantine',p.name)
   data=p.read_bytes();result[p.stem]={'bytes':len(data),'sha256':sha(data)}
  return result
 def copy_files(self,src,dst):
  for p in src.iterdir():
   assert p.is_file() and not p.is_symlink()
   fd=os.open(dst/p.name,os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600)
   with os.fdopen(fd,'wb') as target:target.write(p.read_bytes());target.flush();os.fsync(target.fileno())
  assert self.files(src)==self.files(dst)
 def restore(self,db,dump):
  assert db in self.owned
  assert self.sql(db,"SELECT count(*) FROM information_schema.tables WHERE table_schema='public'").strip()=='0'
  self.command(['pg_restore','--exit-on-error','--single-transaction','--no-owner','--no-acl','--dbname',db,str(dump)])
 def app(self,db,root,initialize=False,expect_failure=False):
  return App(self,db,root,initialize,expect_failure)

class App:
 def __init__(self,r,db,root,initialize,expect_failure):
  self.r=r;self.token=None;self.url=None;self.db=db;self.root=root
  env=os.environ.copy();env.update({'ASPNETCORE_ENVIRONMENT':'Development','DOTNET_ENVIRONMENT':'Development','ConnectionStrings__Poseidon':r.connection(db),'SyntheticIdentity__Enabled':'true','Database__Initialize':str(initialize).lower(),'Bff__Enabled':'false','EvidenceIntake__Enabled':'true','EvidenceIntake__Root':str(root)})
  self.log=r.private/('app-'+str(len(r.apps))+'.log');self.stream=self.log.open('wb')
  dll=REPO/'src/Backend/bin/Debug/net10.0/Poseidon.Backend.dll'
  args=['dotnet',str(dll),'--urls','http://127.0.0.1:0']
  self.process=subprocess.Popen(args,cwd=REPO/'src/Backend',env=env,stdout=self.stream,stderr=subprocess.STDOUT)
  r.apps.append(self) # Track before readiness assertions, so every failure stops only this task-owned child.
  r.commands.append({'argv':['dotnet','<REPO>/src/Backend/bin/Debug/net10.0/Poseidon.Backend.dll','--urls','http://127.0.0.1:0'],'database':db,'initialize':initialize,'pid':self.process.pid,'configuration':'existing credentials via child environment; synthetic/BFF-off/evidence opt-in; no credential values recorded'})
  deadline=time.monotonic()+30
  while time.monotonic()<deadline:
   text=self.log.read_text(errors='replace');match=re.search(r'Now listening on: (http://127\.0\.0\.1:\d+)',text)
   if match:self.url=match[1];break
   if self.process.poll() is not None:break
   time.sleep(.1)
  if expect_failure:
   assert self.url is None and self.process.poll() is not None,'Corrupt-copy startup unexpectedly listened or stalled'
   messages=['Stored quarantine integrity reconciliation failed.','Stored quarantine object unavailable.','Evidence root contains unaccounted retained objects.']
   found=[m for m in messages if m in self.log.read_text(errors='replace')]
   assert found,'Unexpected startup failure; private diagnostics retained'
   r.record('isolated_copy_startup_failed_closed',database=db,reason=found[0],readAvailable=False)
   self.stop();return
  if not self.url:self.stop();raise RuntimeError('Application did not reach loopback readiness; private diagnostic retained.')
  assert self.call('/health/ready',auth=False)[0]==200
 def stop(self):
  if self.process.poll() is None:
   self.process.terminate()
   try:self.process.wait(timeout=10)
   except subprocess.TimeoutExpired:self.process.kill();self.process.wait(timeout=5)
  self.stream.close()
 def call(self,path,method='GET',body=None,headers=None,auth=True):
  h=dict(headers or {})
  if auth and self.token:h['Authorization']='Bearer '+self.token
  if body is not None and not isinstance(body,bytes):body=json.dumps(body).encode();h['Content-Type']='application/json'
  try:
   with loopback_request(self.url+path,data=body,headers=h,method=method) as response:status=response.status;data=response.read()
  except urllib.error.HTTPError as response:status=response.code;data=response.read()
  self.r.http.append({'database':self.db,'method':method,'path':path,'status':status})
  return status,json.loads(data) if data else None
 def login(self,fixture='evidence-manager'):
  status,data=self.call('/api/v1/dev/session','POST',{'fixture':fixture},auth=False);assert status==200;self.token=data['accessToken']
 def get(self,path):
  status,data=self.call(path);assert status==200,(path,status);return data
 def post(self,path,body,version=None,key=None,expected=200,raw=False):
  h={'Idempotency-Key':key or newkey()}
  if version is not None:h['If-Match']='"'+str(version)+'"'
  if raw:h['Content-Type']=raw
  status,data=self.call(path,'POST',body,h);assert status==expected,(path,status,data.get('code') if data else None);return data

def run(r):
 assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=REPO,text=True).strip()==BASE
 for tool in ['dotnet','psql','pg_dump','pg_restore']:
  if not shutil.which(tool):raise RuntimeError('Installed tool missing: '+tool)
 versions={tool:r.command([tool,'--version']).strip() for tool in ['psql','pg_dump','pg_restore']}
 versions['server']=r.sql(r.cfg['database'],'SHOW server_version').strip();r.record('installed_official_tools',versions=versions)
 observed=json.loads(r.sql(r.cfg['database'],"SELECT json_build_object('serverAddress',inet_server_addr(),'clientAddress',inet_client_addr(),'port',current_setting('port')::int)").strip())
 assert observed['port']==int(r.cfg['port'])
 if r.locality['transport']=='LOCAL_UNIX_SOCKET':assert observed['serverAddress'] is None and observed['clientAddress'] is None
 else:assert all(ipaddress.ip_address(observed[k]).is_loopback for k in ['serverAddress','clientAddress'])
 r.locality['serverObservation']=observed;r.locality['verifiedBeforeDdl']=True
 r.record('explicit_local_endpoint_before_ddl',locality=r.locality)
 src=r.create_db('source');srcroot=r.root('source-objects');app=r.app(src,srcroot,True);app.login()
 inv='/api/v1/inventory/vessels';cert='/api/v1/certificate-register';ev='/api/v1/evidence-intake'
 createkey=newkey();vdata={'name':'Synthetic restore tug','flag':'Declared TR','vessel_type':'Tug','asset_mode':'TUG'}
 vessel=app.post(inv,{'data':vdata},key=createkey);created_id=vessel['id'];vessel=app.post(inv+'/'+created_id+'/commands/record',{},vessel['version'])
 certificate=app.post(cert,{'data':{'vessel_id':created_id,'type':'Synthetic declared document','issuer':'Synthetic declared issuer','expiry':'2026-12-01'}});cid=certificate['id'];certificate=app.post(cert+'/'+cid+'/commands/record',{},certificate['version'])
 # Reopen/re-record parent: stable identity and certificate linkage are not revoked.
 vessel=app.post(inv+'/'+created_id+'/commands/reopen',{'reason':'Synthetic correction before backup'},vessel['version']);vessel=app.post(inv+'/'+created_id+'/commands/record',{},vessel['version']);assert app.get(cert+'/'+cid)['parentVessel']['id']==created_id
 def metadata(kind,target,filename,mime,content):return {'targetType':kind,'targetId':target,'filename':filename,'declaredMime':mime,'expectedBytes':len(content),'expectedSha256':sha(content)}
 def upload(meta,content):
  row=app.post(ev,meta);return app.post(ev+'/'+row['id']+'/content',content,row['version'],raw=meta['declaredMime'])
 firstmeta=metadata('Vessel',created_id,'synthetic.pdf','application/pdf',PDF);reservekey=newkey();reserved=app.post(ev,firstmeta,key=reservekey);first=app.post(ev+'/'+reserved['id']+'/content',PDF,reserved['version'],raw='application/pdf')
 second=upload(metadata('Certificate',cid,'synthetic.png','image/png',PNG),PNG)
 changed=PDF.replace(b'%PDF-1.4',b'%PDF-1.4\n% synthetic correction')
 child=app.post(ev+'/'+first['id']+'/supersede',{'metadata':metadata('Vessel',created_id,'correction.pdf','application/pdf',changed),'reason':'PRIVATE synthetic correction'},first['version']);child=app.post(ev+'/'+child['id']+'/content',changed,child['version'],raw='application/pdf')
 pending=app.post(ev,metadata('Vessel',created_id,'interrupted.pdf','application/pdf',PDF));app.post(ev+'/'+pending['id']+'/content',PDF+b' ',pending['version'],raw='application/pdf',expected=422);assert app.get(ev+'/'+pending['id'])['state']=='RECEIVING'
 ids={'vessel':created_id,'certificate':cid,'evidence':[first['id'],second['id'],child['id'],pending['id']]}
 def verify(app,label):
  app.login();gotv=app.get(inv+'/'+created_id);gotc=app.get(cert+'/'+cid)
  assert gotv['id']==created_id and gotv['version']==vessel['version'] and gotv['adminState']=='RECORDED'
  assert gotc['id']==cid and gotc['version']==certificate['version'] and gotc['data']['vessel_id']==created_id and gotc['parentVessel']['adminState']=='RECORDED'
  for item in [first,second,child]:
   got=app.get(ev+'/'+item['id']);assert got['sha256']==item['sha256'] and got['version']==item['version'] and got['state']=='STORED_QUARANTINED' and got['canDownload'] is False and got['scanState']=='UNAVAILABLE'
   assert got['verificationAssessment']==got['operationalAssessment']=='NOT_PERFORMED'
   assert app.call(ev+'/'+item['id']+'/content')[0]>=400
  assert app.get(ev+'/'+first['id'])['supersededBy']==child['id'] and app.get(ev+'/'+child['id'])['previousId']==first['id']
  histories={path:app.get(path+'/history') for path in [inv+'/'+created_id,cert+'/'+cid,ev+'/'+first['id'],ev+'/'+second['id'],ev+'/'+child['id'],ev+'/'+pending['id']]}
  for h in histories.values():
   for item in h['items']:assert set(item)=={'eventId','occurredAt','version','action','changedFields','actorRelation'}
   assert 'PRIVATE' not in json.dumps(h) and 'synthetic-evidence-manager' not in json.dumps(h)
  assert app.get(ev+'/'+pending['id'])['state']=='RECEIVING'
  for fixture in ['tenant-a','tenant-a-other-company','tenant-b']:
   app.login(fixture);missing=app.call(ev+'/'+newkey());assert missing[0]==404
   for eid in ids['evidence']:
    assert app.call(ev+'/'+eid)==missing and app.call(ev+'/'+eid+'/history')==missing
   assert app.call(inv+'/'+created_id)[0]==404 and app.call(cert+'/'+cid)[0]==404
  app.login();r.record(label,idsVersionsRelationships=True,tenantCompanyAndObjectRestrictions=True,quarantineBlocked=True,safeHistorySha256=sha(json.dumps(histories,sort_keys=True).encode()))
  return histories
 histories=verify(app,'source_product_behavior');old_token=app.token;sourcepid=app.process.pid;app.stop()
 before=r.snapshot(src);files=r.files(srcroot);dump=r.private/'synthetic.custom';r.command(['pg_dump','--format=custom','--no-owner','--no-acl','--file',str(dump),src]);backup=r.root('backup-objects');r.copy_files(srcroot,backup)
 r.record('quiesced_database_and_object_backup',applicationStopped=True,dumpSha256=sha(dump.read_bytes()),dumpBytes=dump.stat().st_size,objects=files,tableDigests=before)
 dst=r.create_db('restored');dstroot=r.root('restored-objects');r.restore(dst,dump);r.copy_files(backup,dstroot);assert r.snapshot(dst)==before and r.files(dstroot)==files
 restored=r.app(dst,dstroot);assert restored.process.pid!=sourcepid;restored.token=old_token;assert restored.call('/api/v1/session')[0]==401
 assert verify(restored,'restored_product_behavior')==histories
 # Restored idempotency is exact-once and does not change quota/history/current aggregate version.
 replay=restored.post(inv,{'data':vdata},key=createkey);assert replay['id']==created_id
 reread=restored.post(ev,firstmeta,key=reservekey);assert reread['id']==first['id']
 restored.post(ev+'/'+first['id']+'/content',PDF,first['version'],raw='application/pdf',expected=409)
 assert r.snapshot(dst)==before and r.files(dstroot)==files;r.record('restored_idempotency_quota_and_bytes_unchanged',quota=before['quota'])
 first_restore_pid=restored.process.pid;restored.stop();restarted=r.app(dst,dstroot);assert restarted.process.pid!=first_restore_pid
 assert verify(restarted,'actual_restored_application_restart')==histories;restarted.stop();assert r.snapshot(dst)==before
 for suffix in ['missing','corrupt']:
  db=r.create_db(suffix);root=r.root(suffix+'-objects');r.restore(db,dump);r.copy_files(backup,root)
  target=root/(first['id'].replace('-','')+'.quarantine')
  if suffix=='missing':target.rename(root.parent/(suffix+'-isolated-held-object')) # No deletion, only the new isolated copy.
  else:
   content=bytearray(target.read_bytes());content[0]^=1
   with target.open('r+b') as f:f.write(content);f.flush();os.fsync(f.fileno())
  bad=r.app(db,root,expect_failure=True)
 assert r.files(srcroot)==files and r.files(backup)==files and r.files(dstroot)==files
 assert r.snapshot(src)==before and r.snapshot(dst)==before
 # Test restored actor reservation limit on another isolated restore, without changing the comparison copy.
 quota_db=r.create_db('quota');quota_root=r.root('quota-objects');r.restore(quota_db,dump);r.copy_files(backup,quota_root);quota_app=r.app(quota_db,quota_root);quota_app.login()
 quota_before=r.snapshot(quota_db)['quota'];lastkey=None;lastmeta=None;lastrow=None
 for i in range(9):
  lastkey=newkey();lastmeta={'targetType':'Vessel','targetId':created_id,'filename':'slot'+str(i)+'.pdf','declaredMime':'application/pdf','expectedBytes':1};lastrow=quota_app.post(ev,lastmeta,key=lastkey)
 quota_app.post(ev,lastmeta,expected=409);assert quota_app.post(ev,lastmeta,key=lastkey)['id']==lastrow['id']
 r.record('restored_quota_enforcement',before=quota_before,after=r.snapshot(quota_db)['quota'],outstandingLimit=10,extraReservationHttpStatus=409,authorizedReplayAddsNoSlot=True,originalPendingReservationStillCharged=True)
 quota_app.stop()
 r.record('original_and_successful_restore_copies_untouched_by_negative_rehearsals',noExistingDatabaseModified=True,noSourceObjectDeleted=True)
 r.write_results('PASS')

if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--run-synthetic-rehearsal',action='store_true',help='Explicitly create new task-owned test DBs/roots and retain them; never drops or overwrites an existing DB.');args=parser.parse_args()
 if not args.run_synthetic_rehearsal:parser.print_help();raise SystemExit(0)
 rehearsal=None
 try:
  rehearsal=Rehearsal();run(rehearsal);print(json.dumps({'status':'PASS','deliverableEvidence':str(rehearsal.results),'privateResources':str(rehearsal.private),'baseline':BASE}))
 except Exception as error:
  # Never print credentials, request headers, raw subprocess stderr or connection strings.
  message=str(error)
  if rehearsal:
   for secret in [rehearsal.cfg.get('password'),os.environ.get('POSEIDON_TEST_POSTGRES')]:
    if secret:message=message.replace(secret,'[REDACTED]')
   rehearsal.write_results('FAILED_OR_BLOCKED',message)
  print(json.dumps({'status':'FAILED_OR_BLOCKED','reason':message,'evidence':str(rehearsal.results) if rehearsal else None}));raise SystemExit(1)
 finally:
  if rehearsal:
   for app in rehearsal.apps:app.stop()
