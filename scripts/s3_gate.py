"""Owned real loopback S3 driver for the later gate. Never run during authoring.

Requires an operator-built, source-labelled MinIO image by immutable local image ID.
No production vendor, subscription, ambient AWS credential, shared service or fake S3 peer.
"""
import argparse
import ctypes
from ctypes import wintypes
import json
import os
from pathlib import Path
import re
import secrets
import signal
import subprocess
import time
import urllib.request
import uuid
from oidc_lab import ROOT, environment, private_directory, write
from codex_accounts import Workers

LABEL = 'poseidon.files-source-s3-gate'


def isolated_environment():
    # A disposable actual-host test must never inherit an operator's lab config
    # selector or database. Test hosts provision their own owned PostgreSQL scope.
    return {k: v for k, v in environment().items()
            if k not in ('POSEIDON_LAB_CONFIG', 'POSEIDON_DATABASE')
            and not k.startswith(('AWS_', 'AMAZON_', 'MINIO_', 'POSEIDON_GATE_S3_'))}


def docker(*args):
    result = subprocess.run(['docker', *args], capture_output=True, timeout=60)
    if result.returncode:
        raise RuntimeError('owned_s3_command_failed')
    return result.stdout.decode().strip()


def owned(identity, owner, name, require_binding=True):
    record = json.loads(docker('inspect', identity))[0]
    if record['Id'] != identity or record['Name'] != '/' + name or record['Config'].get('Labels', {}).get(LABEL) != owner:
        raise RuntimeError('ownership_mismatch')
    if not require_binding:
        return None
    ports = record['NetworkSettings']['Ports'].get('9000/tcp', [])
    if len(ports) != 1 or ports[0]['HostIp'] != '127.0.0.1':
        raise RuntimeError('non_loopback_binding')
    return int(ports[0]['HostPort'])


class WindowsOwnedTree:
    """Atomic Windows 10+ job attachment; no child runs before containment.

    JOB_LIST: https://learn.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute
    KILL_ON_JOB_CLOSE also contains a driver crash, not just its handled deadline.
    """
    def __init__(self):
        self.api = ctypes.WinDLL('kernel32', use_last_error=True)
        self.job = self.process = None
        self.returncode = None
        self.pid = None
        size_t = ctypes.c_size_t
        class BasicLimits(ctypes.Structure):
            _fields_ = [('user',ctypes.c_longlong),('job_user',ctypes.c_longlong),('flags',wintypes.DWORD),
                        ('minimum',size_t),('maximum',size_t),('active',wintypes.DWORD),('affinity',size_t),
                        ('priority',wintypes.DWORD),('scheduling',wintypes.DWORD)]
        class ExtendedLimits(ctypes.Structure):
            _fields_ = [('basic',BasicLimits),('io',ctypes.c_ulonglong*6),('memory',size_t*4)]
        class Accounting(ctypes.Structure):
            _fields_ = [('times',ctypes.c_longlong*4),('faults',wintypes.DWORD),('total',wintypes.DWORD),
                        ('active',wintypes.DWORD),('terminated',wintypes.DWORD)]
        self.Accounting = Accounting
        for name, result, args in (
            ('CreateJobObjectW',wintypes.HANDLE,[ctypes.c_void_p,wintypes.LPCWSTR]),
            ('SetInformationJobObject',wintypes.BOOL,[wintypes.HANDLE,ctypes.c_int,ctypes.c_void_p,wintypes.DWORD]),
            ('QueryInformationJobObject',wintypes.BOOL,[wintypes.HANDLE,ctypes.c_int,ctypes.c_void_p,wintypes.DWORD,ctypes.c_void_p]),
            ('TerminateJobObject',wintypes.BOOL,[wintypes.HANDLE,wintypes.UINT]),
            ('CloseHandle',wintypes.BOOL,[wintypes.HANDLE]),
            ('WaitForSingleObject',wintypes.DWORD,[wintypes.HANDLE,wintypes.DWORD]),
            ('GetExitCodeProcess',wintypes.BOOL,[wintypes.HANDLE,ctypes.POINTER(wintypes.DWORD)]),
            ('InitializeProcThreadAttributeList',wintypes.BOOL,[ctypes.c_void_p,wintypes.DWORD,wintypes.DWORD,ctypes.POINTER(size_t)]),
            ('UpdateProcThreadAttribute',wintypes.BOOL,[ctypes.c_void_p,wintypes.DWORD,size_t,ctypes.c_void_p,size_t,ctypes.c_void_p,ctypes.c_void_p]),
            ('DeleteProcThreadAttributeList',None,[ctypes.c_void_p]),
            ('CreateFileW',wintypes.HANDLE,[wintypes.LPCWSTR,wintypes.DWORD,wintypes.DWORD,ctypes.c_void_p,wintypes.DWORD,wintypes.DWORD,wintypes.HANDLE]),
            ('CreateProcessW',wintypes.BOOL,[wintypes.LPCWSTR,wintypes.LPWSTR,ctypes.c_void_p,ctypes.c_void_p,wintypes.BOOL,wintypes.DWORD,ctypes.c_void_p,wintypes.LPCWSTR,ctypes.c_void_p,ctypes.c_void_p]),
        ):
            fn=getattr(self.api,name);fn.restype=result;fn.argtypes=args
        self.job=self.api.CreateJobObjectW(None,None)
        if not self.job: raise RuntimeError('owned_job_create_failed')
        limits=ExtendedLimits();limits.basic.flags=0x2000  # JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE; no breakaway.
        if not self.api.SetInformationJobObject(self.job,9,ctypes.byref(limits),ctypes.sizeof(limits)):
            self.close();raise RuntimeError('owned_job_limits_failed')

    def start(self, command, env):
        class Security(ctypes.Structure):
            _fields_=[('length',wintypes.DWORD),('descriptor',ctypes.c_void_p),('inherit',wintypes.BOOL)]
        class Startup(ctypes.Structure):
            _fields_=[('cb',wintypes.DWORD),('reserved',wintypes.LPWSTR),('desktop',wintypes.LPWSTR),('title',wintypes.LPWSTR),
                ('x',wintypes.DWORD),('y',wintypes.DWORD),('width',wintypes.DWORD),('height',wintypes.DWORD),
                ('chars_x',wintypes.DWORD),('chars_y',wintypes.DWORD),('fill',wintypes.DWORD),('flags',wintypes.DWORD),
                ('show',wintypes.WORD),('reserved_size',wintypes.WORD),('reserved_bytes',ctypes.c_void_p),
                ('input',wintypes.HANDLE),('output',wintypes.HANDLE),('error',wintypes.HANDLE)]
        class StartupEx(ctypes.Structure):
            _fields_=[('startup',Startup),('attributes',ctypes.c_void_p)]
        class ProcessInfo(ctypes.Structure):
            _fields_=[('process',wintypes.HANDLE),('thread',wintypes.HANDLE),('pid',wintypes.DWORD),('tid',wintypes.DWORD)]
        null=None;attributes=None;initialized=False
        try:
            security=Security(ctypes.sizeof(Security),None,True)
            null=self.api.CreateFileW('NUL',0xc0000000,3,ctypes.byref(security),3,0,None)
            if null in (None,ctypes.c_void_p(-1).value): raise RuntimeError('owned_child_stdio_failed')
            size=ctypes.c_size_t()
            self.api.InitializeProcThreadAttributeList(None,2,0,ctypes.byref(size))
            attributes=ctypes.create_string_buffer(size.value)
            if not self.api.InitializeProcThreadAttributeList(attributes,2,0,ctypes.byref(size)):
                raise RuntimeError('owned_job_attributes_failed')
            initialized=True
            jobs=(wintypes.HANDLE*1)(self.job);handles=(wintypes.HANDLE*1)(null)
            for key,value in ((0x2000d,jobs),(0x20002,handles)):  # JOB_LIST, explicit HANDLE_LIST.
                if not self.api.UpdateProcThreadAttribute(attributes,0,key,value,ctypes.sizeof(value),None,None):
                    raise RuntimeError('owned_job_assignment_failed')
            startup=StartupEx();startup.startup.cb=ctypes.sizeof(startup);startup.attributes=ctypes.cast(attributes,ctypes.c_void_p)
            startup.startup.flags=0x100;startup.startup.input=startup.startup.output=startup.startup.error=null
            command_line=subprocess.list2cmdline(command)
            if len(command_line.encode('utf-16-le'))//2>=32766: raise RuntimeError('owned_child_command_too_long')
            line=ctypes.create_unicode_buffer(command_line)
            block=ctypes.create_unicode_buffer('\0'.join(f'{key}={value}' for key,value in sorted(env.items(),key=lambda item:item[0].upper()))+'\0\0')
            info=ProcessInfo()
            if not self.api.CreateProcessW(None,line,None,None,True,0x80000|0x400|0x200,block,str(ROOT),ctypes.byref(startup),ctypes.byref(info)):
                raise RuntimeError('owned_child_atomic_start_failed')
            self.process=info.process;self.pid=info.pid
            self.api.CloseHandle(info.thread)
            return self
        finally:
            if initialized: self.api.DeleteProcThreadAttributeList(attributes)
            if null not in (None,ctypes.c_void_p(-1).value): self.api.CloseHandle(null)

    def poll(self):
        if self.returncode is not None: return self.returncode
        if not self.process: return None
        status=self.api.WaitForSingleObject(self.process,0)
        if status==0x102: return None
        if status!=0: raise RuntimeError('owned_child_wait_failed')
        result=wintypes.DWORD()
        if not self.api.GetExitCodeProcess(self.process,ctypes.byref(result)): raise RuntimeError('owned_child_exit_unproven')
        self.returncode=result.value;return self.returncode

    def wait(self, timeout):
        status=self.api.WaitForSingleObject(self.process,max(1,min(0xfffffffe,int(timeout*1000))))
        if status==0x102: raise subprocess.TimeoutExpired('owned-child',timeout)
        if status!=0: raise RuntimeError('owned_child_wait_failed')
        return self.poll()

    def active(self):
        result=self.Accounting()
        if not self.api.QueryInformationJobObject(self.job,1,ctypes.byref(result),ctypes.sizeof(result),None):
            raise RuntimeError('owned_job_state_unproven')
        return result.active

    def stop(self):
        if not self.api.TerminateJobObject(self.job,1): raise RuntimeError('owned_job_stop_failed')
        deadline=time.monotonic()+10
        while self.active():
            if time.monotonic()>=deadline: raise RuntimeError('owned_job_stop_unproven')
            time.sleep(.02)
        self.wait(10)

    def close(self):
        # The job handle is never inheritable. Its last close kills every owned
        # descendant even if this driver is terminated before Python cleanup.
        for name in ('job','process'):
            handle=getattr(self,name,None)
            if handle:
                if not self.api.CloseHandle(handle): raise RuntimeError('owned_job_close_failed')
                setattr(self,name,None)


def launch_owned_child(command, env):
    if os.name=='nt':
        tree=WindowsOwnedTree()
        try: return tree.start(command,env),tree
        except BaseException:
            tree.close();raise
    return subprocess.Popen(command,cwd=ROOT,env=env,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,start_new_session=True),None


def finish_owned_tree(process, tree, force=False):
    if tree is not None:
        try:
            active=tree.active()
            if active: tree.stop()
            return active>0
        finally:
            tree.close()
    # The session belongs to this invocation, even after its leader exits.
    try: os.killpg(process.pid,0)
    except ProcessLookupError: return False
    if process.poll() is None: Workers.stop(process)
    else:
        try: os.killpg(process.pid,signal.SIGKILL)
        except ProcessLookupError: pass
    return True


def owned_child(command, env, timeout, evidence):
    evidence['child_status']='STARTING'
    try: process,tree=launch_owned_child(command,env)
    except BaseException:
        evidence['child_status']='START_FAILED'
        raise RuntimeError('owned_child_start_failed') from None
    evidence.update(child_status='RUNNING',child_tree_status='OWNED_CONTAINED_TREE')
    try:
        code=process.wait(timeout=timeout)
    except BaseException as failure:
        evidence['child_status']=('DEADLINE' if isinstance(failure,subprocess.TimeoutExpired)
            else 'INTERRUPTED' if isinstance(failure,KeyboardInterrupt) else 'WAIT_FAILED')
        evidence['child_tree_status']='STOP_UNPROVEN'
        try: finish_owned_tree(process,tree,True)
        except BaseException: raise RuntimeError('owned_child_tree_stop_failed') from None
        evidence['child_tree_status']='STOPPED_BEFORE_PROVIDER_CLEANUP'
        cause={'DEADLINE':'owned_child_deadline','INTERRUPTED':'owned_child_interrupted','WAIT_FAILED':'owned_child_wait_failed'}[evidence['child_status']]
        raise RuntimeError(cause) from None
    evidence.update(child_status='EXITED',child_exit_code=code,child_tree_status='STOP_UNPROVEN')
    try: descendants=finish_owned_tree(process,tree)
    except BaseException: raise RuntimeError('owned_child_tree_stop_failed') from None
    evidence['child_tree_status']='STOPPED_BEFORE_PROVIDER_CLEANUP' if descendants else 'COMPLETED'
    if descendants:
        evidence['child_status']='EXITED_WITH_LIVE_DESCENDANTS'
        raise RuntimeError('owned_child_unfinished_descendants') from None
    return code


def run(image, command, child_timeout=900, evidence=None):
    evidence = evidence if evidence is not None else {}
    evidence.update(stage="owned_s3_setup", child_status="NOT_STARTED", child_exit_code=None,
                    child_tree_status="NOT_STARTED", provider_cleanup="NOT_STARTED", config_cleanup="NOT_STARTED")
    if type(child_timeout) is not int or not 1 <= child_timeout <= 3600:
        raise RuntimeError('invalid_child_deadline')
    if (not isinstance(image,str) or not re.fullmatch(r'sha256:[0-9a-f]{64}', image)
            or not isinstance(command,(list,tuple)) or not command
            or any(not isinstance(arg,str) or '\0' in arg for arg in command)):
        raise RuntimeError('explicit_owned_image_and_command_required')
    evidence['stage']='owned_s3_image_verification'
    record = json.loads(docker('image', 'inspect', image))[0]
    labels = record['Config'].get('Labels') or {}
    if record['Id'] != image or labels.get('org.opencontainers.image.source') != 'https://github.com/minio/minio' or not re.fullmatch('[0-9a-f]{40}', labels.get('org.opencontainers.image.revision', '')):
        raise RuntimeError('source_label_required')
    owner = uuid.uuid4().hex; name = 'poseidon-files-s3-' + owner
    private = ROOT / '.runtime/files-source-s3' / owner
    evidence['stage']='owned_s3_private_configuration'
    private_directory(private)
    access = secrets.token_hex(16); credential = secrets.token_urlsafe(48)
    config = private / 'container.env'
    configuration = {'MINIO_ROOT_USER': access, 'MINIO_ROOT_PASSWORD': credential, 'MINIO_BROWSER': 'off'}
    write(config, ''.join(f'{key}={value}\n' for key, value in configuration.items()))
    identity = None
    try:
        evidence['stage']='owned_s3_provider_start'
        identity = docker('run', '--detach', '--pull', 'never', '--name', name, '--label', LABEL + '=' + owner,
                          '--publish', '127.0.0.1::9000', '--env-file', str(config), image, 'server', '/data')
        evidence['stage']='owned_s3_binding_verification'
        port = owned(identity, owner, name); endpoint = f'http://127.0.0.1:{port}'
        opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
        evidence['stage']='owned_s3_readiness'
        deadline = time.monotonic() + 60
        while True:
            try:
                with opener.open(endpoint + '/minio/health/ready', timeout=2) as response:
                    if response.status == 200:
                        break
            except OSError:
                pass
            if time.monotonic() >= deadline:
                raise RuntimeError('owned_s3_readiness_deadline')
            time.sleep(.5)
        owned(identity, owner, name)  # Reconfirm exact binding immediately before handing it to tests.
        env = isolated_environment()
        env.update({'POSEIDON_GATE_S3_ENDPOINT': endpoint, 'POSEIDON_GATE_S3_ACCESS': access,
                    'POSEIDON_GATE_S3_SECRET': credential, 'POSEIDON_GATE_S3_OWNER': owner})
        # Child logs may contain failed SDK request diagnostics; never echo them.
        evidence['stage'] = 'owned_s3_child'
        return owned_child(command, env, child_timeout, evidence)
    finally:
        try:
            if identity:
                if evidence['child_tree_status'] == 'STOP_UNPROVEN':
                    evidence['provider_cleanup'] = 'DEFERRED_CHILD_TREE_STOP_UNPROVEN'
                else:
                    try:
                        owned(identity, owner, name, require_binding=False)
                        docker('rm', '--force', identity)  # Exact owned resource only, no volumes/prune.
                        evidence['provider_cleanup'] = 'REMOVED_EXACT_OWNED_CONTAINER'
                    except BaseException:
                        evidence['provider_cleanup'] = 'FAILED_OWNERSHIP_OR_REMOVAL'
                        raise RuntimeError('owned_s3_provider_cleanup_failed') from None
        finally:
            try:
                config.unlink(missing_ok=True)
                evidence['config_cleanup'] = 'REMOVED_PRIVATE_ENV_FILE'
            except BaseException:
                evidence['config_cleanup'] = 'FAILED_PRIVATE_ENV_REMOVAL'
                raise RuntimeError('owned_s3_config_cleanup_failed') from None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--image', required=True)
    parser.add_argument('--child-timeout', type=int, default=900, help='Explicit bounded child deadline, 1..3600 seconds; the full API bundle needs more than 15 minutes.')
    parser.add_argument('command', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    evidence = {}
    try:
        command = args.command[1:] if args.command[:1] == ['--'] else args.command
        code = run(args.image, command, args.child_timeout, evidence=evidence)
        print(json.dumps({**evidence, 'exit_code': code}))
        return code
    except BaseException:
        # Only closed lifecycle states are emitted; no command, credential, SDK output or exception text.
        interrupted = evidence.get('child_status') == 'INTERRUPTED'
        code = 130 if interrupted else 2
        print(json.dumps({**evidence, 'exit_code': code, 'status': 'FAILED_OR_INTERRUPTED'}))
        return code


if __name__ == '__main__':
    raise SystemExit(main())
