#!/usr/bin/env python3
"""One foreground supervisor. Ctrl+C stops only its own application and PostgreSQL."""
import argparse,json,os,pathlib,re,signal,subprocess,sys,time
ROOT=pathlib.Path(__file__).resolve().parents[1]

def clean_env(environ):
    # No inherited tokens, connection strings, VITE flags, proxy, PG*, npm config, loader or hooks.
    keep=('PATH','HOME','LANG','LC_ALL','DOTNET_ROOT')
    return {k:environ[k] for k in keep if k in environ}|{'DOTNET_CLI_TELEMETRY_OPTOUT':'1','DOTNET_NOLOGO':'1','DOTNET_GENERATE_ASPNET_CERTIFICATE':'false','DOTNET_SKIP_FIRST_TIME_EXPERIENCE':'1','NPM_CONFIG_USERCONFIG':str(ROOT/'.devcontainer/npm-user.npmrc'),'NPM_CONFIG_GLOBALCONFIG':str(ROOT/'.devcontainer/npm-global.npmrc')}

def verify_visibility(metadata,ports,name,origin):
    if metadata.get('name')!=name or metadata.get('owner',{}).get('login')!='emgt41' or metadata.get('repository',{}).get('full_name')!='emgt41/Poseidon' or metadata.get('repository',{}).get('private') is not True:
        raise RuntimeError('Codespace creator/private repository mismatch.')
    if len(ports)!=1 or ports[0].get('sourcePort')!=8080 or ports[0].get('visibility')!='private' or ports[0].get('browseUrl','').rstrip('/')!=origin:
        raise RuntimeError('Only the exact creator-private 8080 port is permitted; inspect Ports and retry.')

def github_check(env,name,origin):
    gh_env=clean_env(env)
    token=env.get('GITHUB_TOKEN')
    if not token:raise RuntimeError('Automatic Codespaces credential unavailable; do not create/share a replacement token.')
    gh_env['GH_TOKEN']=token;gh_env['GH_PROMPT_DISABLED']='1'
    def get(args):
        r=subprocess.run(['gh',*args],env=gh_env,capture_output=True,timeout=15)
        if r.returncode:raise RuntimeError('Read-only GitHub visibility check failed; no application started/continued.')
        return json.loads(r.stdout)
    verify_visibility(get(['api','/user/codespaces/'+name]),get(['codespace','ports','-c',name,'--json','sourcePort,visibility,browseUrl']),name,origin)

class OwnedPostgres:
    """Direct child ownership: readiness failure never loses the postmaster handle.

    Caller holds supervisor.lock. Existing servers are refused, never adopted/stopped.
    """
    def __init__(self,data,sock,state,env):
        self.data,self.sock,self.state,self.env=data,sock,state,env
        self.expected={'purpose':'poseidon-synthetic-uat-v1','repository':str(ROOT)}
        self.identity=None;self.attempted=False;self.process=None;self.log=None
    def verify(self):
        if self.state.is_symlink() or self.data.is_symlink() or (self.state/'owner.json').is_symlink():raise RuntimeError('Unsafe cluster ownership path.')
        if json.loads((self.state/'owner.json').read_text())!=self.expected:raise RuntimeError('Cluster ownership marker changed; refusing control.')
        for directory in [self.state,self.data,self.sock]:
            info=directory.stat()
            if directory.is_symlink() or info.st_uid!=os.getuid() or info.st_mode & 0o077:raise RuntimeError('Unsafe cluster ownership.')
        info=self.data.stat();identity=(info.st_dev,info.st_ino)
        if self.identity is not None and identity!=self.identity:raise RuntimeError('Cluster directory replaced; refusing control.')
        return identity
    def status(self):
        result=subprocess.run(['pg_ctl','-D',str(self.data),'status'],env=self.env,capture_output=True,timeout=10)
        if result.returncode not in (0,3):raise RuntimeError('Cannot establish cluster process state; refusing control.')
        return result.returncode
    def start(self):
        self.identity=self.verify()
        if self.status()!=3:raise RuntimeError('Task cluster was already running; refusing to adopt or stop it.')
        # Recorded BEFORE process creation. Direct child handle replaces pg_ctl's detached start.
        self.attempted=True
        self.log=(self.state/'postgres.log').open('a')
        previous=signal.pthread_sigmask(signal.SIG_BLOCK,{signal.SIGINT,signal.SIGTERM})
        try:
            self.process=subprocess.Popen(['postgres','-D',str(self.data),'-k',str(self.sock),'-h','','-p','55433'],env=self.env,stdout=self.log,stderr=subprocess.STDOUT,
                preexec_fn=lambda:signal.pthread_sigmask(signal.SIG_SETMASK,previous))
        finally:signal.pthread_sigmask(signal.SIG_SETMASK,previous)
        self.wait_ready()
    def wait_ready(self):
        # libpq's default is 3 seconds, longer than the former 2-second outer
        # timeout. A transient probe timeout is not proof that the child failed.
        deadline=time.monotonic()+30
        while time.monotonic()<deadline:
            if self.process.poll() is not None:raise RuntimeError('Task PostgreSQL exited before readiness.')
            remaining=deadline-time.monotonic()
            if remaining<=0:break
            try:
                ready=subprocess.run(['pg_isready','-q','-t','1','-h',str(self.sock),'-p','55433'],env=self.env,timeout=min(2,remaining))
            except subprocess.TimeoutExpired:
                ready=None
            if ready is not None:
                if ready.returncode==0:
                    self.verify()
                    pidfile=self.data/'postmaster.pid'
                    if pidfile.is_symlink() or not pidfile.exists() or pidfile.read_text().splitlines()[0]!=str(self.process.pid):
                        raise RuntimeError('Task PostgreSQL readiness identity mismatch; no existing process adopted.')
                    if self.process.poll() is not None:raise RuntimeError('Task PostgreSQL exited before readiness.')
                    return
                if ready.returncode not in (1,2):raise RuntimeError('Task PostgreSQL readiness check failed.')
            remaining=deadline-time.monotonic()
            if remaining>0:time.sleep(min(.5,remaining))
        raise TimeoutError('Task PostgreSQL did not become ready within the bounded startup deadline.')
    def close(self):
        try:
            if not self.attempted or self.process is None:return
            # A dead owned child must not lead us to stop a replacement process at the same path.
            if self.process.poll() is not None:return
            self.verify()
            pidfile=self.data/'postmaster.pid'
            if pidfile.exists():
                if pidfile.is_symlink() or pidfile.read_text().splitlines()[0]!=str(self.process.pid):raise RuntimeError('Cluster PID differs from owned child; cleanup refused.')
            # Signal the retained child, never resolve a target again through pg_ctl -D.
            # Popen.send_signal checks/reaps its own child before signaling, avoiding stale PID reuse.
            self.process.send_signal(signal.SIGINT)
            try:self.process.wait(timeout=20)
            except subprocess.TimeoutExpired:raise RuntimeError('Owned PostgreSQL did not stop; retained cluster needs inspection. No unrelated process was signaled.') from None
        finally:
            if self.log:self.log.close()

class ShutdownOutcome:
    def __init__(self):self.cleanup_error=None;self.primary_error=None;self.stage="STARTUP"

def failure_class(error):
    """Fixed diagnostics only: never print subprocess arguments, paths or environment."""
    if isinstance(error,KeyboardInterrupt):return 'INTERRUPTED'
    if isinstance(error,subprocess.TimeoutExpired):return 'SUBPROCESS_TIMEOUT'
    if isinstance(error,subprocess.CalledProcessError):return 'SUBPROCESS_FAILED'
    if isinstance(error,TimeoutError):return 'TIMEOUT'
    if isinstance(error,OSError):return 'OS_ERROR_ERRNO_'+str(error.errno) if isinstance(error.errno,int) else 'OS_ERROR'
    return {
        'Task PostgreSQL exited before readiness.':'POSTGRES_EXITED_BEFORE_READINESS',
        'Task cluster was already running; refusing to adopt or stop it.':'PREEXISTING_CLUSTER_REFUSED',
        'Cannot establish cluster process state; refusing control.':'CLUSTER_STATUS_UNKNOWN',
        'Task PostgreSQL readiness check failed.':'POSTGRES_READINESS_CHECK_FAILED',
        'Application did not become ready; no successful startup is claimed.':'APP_NOT_READY',
    }.get(str(error),'RUNTIME_ERROR')

def cleanup(app,cluster,outcome):
    previous=signal.pthread_sigmask(signal.SIG_BLOCK,{signal.SIGINT,signal.SIGTERM})
    pending=None
    try:
        try:
            if app and app.poll() is None:
                app.terminate()
                try:app.wait(timeout=15)
                except subprocess.TimeoutExpired:app.kill();app.wait()
        finally:
            if cluster:cluster.close()
    except BaseException as error:
        # Sticky outcome also reaches the CLI if a further signal interrupts error propagation.
        outcome.cleanup_error=error
    finally:
        try:signal.pthread_sigmask(signal.SIG_SETMASK,previous)
        except BaseException as error:pending=error
    if outcome.cleanup_error is not None:raise outcome.cleanup_error
    if pending is not None:raise pending

def run(args,outcome=None):
    if outcome is None:outcome=ShutdownOutcome()
    env=clean_env(os.environ)
    if args.skip_build and not args.local_emulation:raise RuntimeError('Codespaces startup always builds the reviewed source.')
    name=os.environ.get('CODESPACE_NAME','')
    if not args.local_emulation:
        if os.environ.get('CODESPACES')!='true' or not re.fullmatch('[a-z0-9]+(?:-[a-z0-9]+)*',name) or os.environ.get('GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN')!='app.github.dev':raise RuntimeError('Reviewed Codespaces environment required.')
        origin=f'https://{name}-8080.app.github.dev'
        if not args.free_only_confirmed:raise RuntimeError('Creator must confirm free entitlement; no billing changes are automated.')
        github_check(os.environ,name,origin)
        status=subprocess.check_output(['git','status','--porcelain'],env=env,cwd=ROOT,text=True)
        if status.strip():raise RuntimeError('Codespaces source must be the clean reviewed commit; preserve edits and request review.')
    else:origin='https://localhost'
    if os.getuid()==0:raise RuntimeError('Run as the unprivileged container user, never root.')
    # State is deliberately outside source and belongs to this one synthetic runtime.
    state=pathlib.Path.home()/'.poseidon-synthetic-uat'
    if any(c in str(state) for c in [';', '\n', '\r', '\x00', ' ', '"', "'"]):raise RuntimeError('Unsupported state path.')
    if state.is_symlink():raise RuntimeError('Symlink state rejected.')
    if not state.exists():state.mkdir(mode=0o700)
    if state.stat().st_uid!=os.getuid() or state.stat().st_mode & 0o077:raise RuntimeError('Private task-owned state required; do not change existing permissions automatically.')
    for item in state.iterdir():
        if item.is_symlink():raise RuntimeError('Symlink inside task state rejected.')
    marker=state/'owner.json'
    expected={'purpose':'poseidon-synthetic-uat-v1','repository':str(ROOT)}
    if marker.exists():
        if json.loads(marker.read_text())!=expected:raise RuntimeError('State belongs to another checkout; no reuse.')
    elif list(state.iterdir()):raise RuntimeError('Unidentified existing state; refusing reuse.')
    else:marker.write_text(json.dumps(expected))
    import fcntl
    # Explicit lifetime: traceback references must not retain the lock after run ends.
    # Keep it held through build, runtime and cleanup; never remove/replace the inode.
    with (state/'supervisor.lock').open('a') as lock:
        try:fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
        except BlockingIOError:
            raise RuntimeError('Synthetic UAT supervisor lock is already held; do not delete the lock file or start another launcher.') from None
        data=state/'pgdata';sock=state/'socket'
        if not sock.exists():sock.mkdir(mode=0o700)
        for directory in [sock, data] if data.exists() else [sock]:
            if directory.is_symlink() or directory.stat().st_uid!=os.getuid() or directory.stat().st_mode & 0o077:raise RuntimeError('Unsafe task-owned database/socket directory.')
        def command(cmd,**kwargs):subprocess.run(cmd,env=env,cwd=ROOT,check=True,**kwargs)
        if not args.skip_build:
            for folder in [ROOT, ROOT/'src/Frontend']:
                if any(p.name!='.env.example' for p in folder.glob('.env*')):raise RuntimeError('Unreviewed dotenv file; build refused without reading its values.')
            command(['dotnet','restore','src/Backend/Poseidon.Backend.csproj','--locked-mode','--configfile',str(ROOT/'.devcontainer/NuGet.Config')])
            command(['dotnet','build','src/Backend/Poseidon.Backend.csproj','--no-restore'])
            command(['npm','ci','--ignore-scripts','--prefix','src/Frontend'])
            build_env=env|{'VITE_ENABLE_BFF':'true','VITE_ENABLE_UAT':'true','VITE_ENABLE_DEV_SESSION':'false','VITE_ENABLE_EVIDENCE_INTAKE':'false'}
            subprocess.run(['npm','run','build','--prefix','src/Frontend'],env=build_env,cwd=ROOT,check=True)
        cluster=None;app=None
        try:
            if not data.exists():command(['initdb','-D',str(data),'--auth-local=peer','--auth-host=reject','--no-instructions'])
            cluster=OwnedPostgres(data,sock,state,env)
            outcome.stage="POSTGRES_START"
            cluster.start()
            env.update({'PGHOST':str(sock),'PGPORT':'55433','PGUSER':__import__('pwd').getpwuid(os.getuid()).pw_name})
            outcome.stage="DATABASE_CHECK"
            exists=subprocess.check_output(['psql','-XAt','-d','postgres','-c',"SELECT count(*) FROM pg_database WHERE datname='poseidon_test_uat'"],env=env,text=True).strip()
            if exists=='0':
                outcome.stage="DATABASE_CREATE"
                command(['createdb','poseidon_test_uat'])
            app_env=env|{'ASPNETCORE_ENVIRONMENT':'Development','DOTNET_ENVIRONMENT':'Development','DevelopmentUat__Enabled':'true','DevelopmentUat__Origin':origin,'DevelopmentUat__StaticRoot':str(ROOT/'src/Frontend/dist'),'Database__Initialize':'true','EvidenceIntake__Enabled':'false','SyntheticIdentity__Enabled':'false','Bff__Enabled':'false','ConnectionStrings__Poseidon':f'Host={sock};Port=55433;Username={env["PGUSER"]};Database=poseidon_test_uat'}
            outcome.stage="VISIBILITY_CHECK"
            if not args.local_emulation:github_check(os.environ,name,origin)
            outcome.stage="APP_START"
            app=subprocess.Popen(['dotnet',str(ROOT/'src/Backend/bin/Debug/net10.0/Poseidon.Backend.dll'),'--urls','http://127.0.0.1:8080'],env=app_env,cwd=ROOT/'src/Backend',stdout=(state/'app.log').open('w'),stderr=subprocess.STDOUT)
            import urllib.request, urllib.error
            opener=urllib.request.build_opener(urllib.request.ProxyHandler({}))
            outcome.stage="APP_READINESS"
            ready=False
            for _ in range(60):
                if app.poll() is not None:break
                try:
                    request=urllib.request.Request('http://127.0.0.1:8080/health/ready',headers={'Host':origin.removeprefix('https://')})
                    with opener.open(request,timeout=1) as response:ready=response.status==200
                    if ready:break
                except (OSError,urllib.error.URLError):pass
                time.sleep(.5)
            if not ready:raise RuntimeError('Application did not become ready; no successful startup is claimed.')
            print('Synthetic UAT started. Ctrl+C stops application and its database. '+('LOCAL EMULATION ONLY' if args.local_emulation else origin),flush=True)
            outcome.stage="APP_MONITOR"
            while app.poll() is None:
                time.sleep(5)
                outcome.stage="VISIBILITY_CHECK"
                if not args.local_emulation:github_check(os.environ,name,origin)
                outcome.stage="APP_MONITOR"
            if app.returncode:raise RuntimeError('Application exited; inspect task-owned local app.log.')
        except BaseException as error:
            outcome.primary_error=error
            raise
        finally:
            cleanup(app,cluster,outcome)

def main(argv=None):
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--free-only-confirmed',action='store_true');p.add_argument('--local-emulation',action='store_true',help='Local test only: HTTPS localhost browser transport must be emulated. Never claims Codespaces privacy.');p.add_argument('--skip-build',action='store_true',help='Tests only: use explicitly prepared local build.')
    args=p.parse_args(argv);outcome=ShutdownOutcome()
    def terminate(signum, frame):raise KeyboardInterrupt()
    previous=signal.getsignal(signal.SIGTERM);signal.signal(signal.SIGTERM,terminate)
    try:
        try:run(args,outcome)
        except BaseException as error:
            if outcome.cleanup_error is not None:
                if outcome.primary_error is not None:
                    print('Original failure: '+outcome.stage+' / '+failure_class(outcome.primary_error),file=sys.stderr)
                print('Cleanup failed: '+str(outcome.cleanup_error),file=sys.stderr);return 1
            if isinstance(error,KeyboardInterrupt):
                print('Stopped. Synthetic data retained locally; no remote environment deleted.');return 0
            if isinstance(error,Exception):print('Startup stopped: '+str(error),file=sys.stderr);return 1
            raise
        return 0
    finally:signal.signal(signal.SIGTERM,previous)

if __name__=='__main__':sys.exit(main())
