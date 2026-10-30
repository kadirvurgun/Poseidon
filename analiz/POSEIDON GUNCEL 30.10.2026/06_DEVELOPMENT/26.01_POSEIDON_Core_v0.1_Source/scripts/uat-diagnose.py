#!/usr/bin/env python3
"""Read-only synthetic UAT identity check. Fixed reasons; no recovery operations."""
import contextlib,json,math,os,pathlib,pwd,shutil,socket as unixsocket,stat,struct,subprocess,sys
ROOT=pathlib.Path(__file__).resolve().parents[1]
REASONS=frozenset(('ARGUMENT_CHECK','PATH_CHECK','MARKER_CHECK','PID_CHECK','SOCKET_PID_CHECK','SOCKET_CHECK','EXECUTABLE_CHECK','PROCESS_CHECK','PROCESS_NAMESPACE_CHECK','PROCESS_UID_CHECK','PROCESS_STATE_CHECK','SQL_CONNECT','SQL_RESULT_CHECK','DATA_DIRECTORY_CHECK','SQL_SOCKET_CHECK','START_TIME_CHECK','SOCKET_PEER_CHECK','SNAPSHOT_CHANGED','INTERRUPTED','INTERNAL_CHECK'))
class Inconclusive(Exception):
    def __init__(self,reason=None):self.reason=reason

@contextlib.contextmanager
def stage(reason):
    try:yield
    except Inconclusive as error:
        raise Inconclusive(error.reason or reason) from None
    except Exception:
        raise Inconclusive(reason) from None

def checked(path,directory=False):
    # Reject symlinks in the entire filesystem path, including parent directories.
    for part in (path,*path.parents):
        if part.is_symlink():raise Inconclusive()
    info=path.stat()
    if info.st_uid!=os.getuid():raise Inconclusive()
    if directory:
        if not stat.S_ISDIR(info.st_mode) or info.st_mode & 0o077:raise Inconclusive()
    elif not stat.S_ISREG(info.st_mode) or info.st_mode & 0o022:raise Inconclusive()
    return info

def small_text(path):
    info=checked(path)
    if info.st_size>8192:raise Inconclusive()
    with path.open() as f:value=f.read(8193)
    if len(value)>8192:raise Inconclusive()
    return value

def self_namespace():
    fields={line.split(':',1)[0]:line.split(':',1)[1].split() for line in pathlib.Path('/proc/self/status').read_text().splitlines() if ':' in line}
    if fields.get('NSpid')!=[str(os.getpid())]:raise Inconclusive('PROCESS_NAMESPACE_CHECK')

def process_identity(pid):
    proc=pathlib.Path('/proc')/str(pid)
    fields={line.split(':',1)[0]:line.split(':',1)[1].split() for line in (proc/'status').read_text().splitlines() if ':' in line}
    # Namespace translation is deliberately unsupported, never inferred.
    if fields.get('NSpid')!=[str(pid)]:raise Inconclusive('PROCESS_NAMESPACE_CHECK')
    if fields.get('Uid')!=[str(os.getuid())]*4:raise Inconclusive('PROCESS_UID_CHECK')
    value=(proc/'stat').read_text();tail=value[value.rfind(')')+2:].split()
    if len(tail)<20 or tail[0] in ('Z','X'):raise Inconclusive('PROCESS_STATE_CHECK')
    return ((proc/'exe').resolve(strict=True),int(tail[19]))

def socket_peer(path):
    # Linux reports the listener credentials at connect time; no SQL/auth data sent.
    if not hasattr(unixsocket,'SO_PEERCRED'):raise Inconclusive('SOCKET_PEER_CHECK')
    with unixsocket.socket(unixsocket.AF_UNIX,unixsocket.SOCK_STREAM) as connection:
        connection.settimeout(3)
        connection.connect(str(path))
        pid,uid,_=struct.unpack('3i',connection.getsockopt(unixsocket.SOL_SOCKET,unixsocket.SO_PEERCRED,struct.calcsize('3i')))
        return pid,uid

def diagnose():
    with stage('PATH_CHECK'):
        state=pathlib.Path.home()/'.poseidon-synthetic-uat';data=state/'pgdata';sock=state/'socket'
        for path in (state,data,sock):checked(path,True)
    with stage('MARKER_CHECK'):
        if json.loads(small_text(state/'owner.json'))!={'purpose':'poseidon-synthetic-uat-v1','repository':str(ROOT)}:raise Inconclusive()
    with stage('PID_CHECK'):
        pidfile=data/'postmaster.pid';socketlock=sock/'.s.PGSQL.55433.lock'
        before=small_text(pidfile);lines=before.splitlines()
        if len(lines)<5:raise Inconclusive()
        pid=int(lines[0]);started=int(lines[2])
        if pid<=0 or started<=0:raise Inconclusive()
        if lines[1]!=str(data) or lines[3]!='55433' or lines[4]!=str(sock):raise Inconclusive()
    with stage('SOCKET_PID_CHECK'):
        lock_before=small_text(socketlock);socket_lines=lock_before.splitlines()
        if len(socket_lines)<5 or socket_lines[:5]!=lines[:5]:raise Inconclusive()
    with stage('SOCKET_CHECK'):
        socket=sock/'.s.PGSQL.55433'
        if socket.is_symlink():raise Inconclusive()
        socket_info=socket.stat()
        if not stat.S_ISSOCK(socket_info.st_mode) or socket_info.st_uid!=os.getuid():raise Inconclusive()
    with stage('EXECUTABLE_CHECK'):
        binary=shutil.which('postgres')
        if not binary:raise Inconclusive()
        postgres=pathlib.Path(binary).resolve(strict=True);psql=postgres.parent/'psql'
        if not psql.is_file() or not os.access(psql,os.X_OK):raise Inconclusive()
    with stage('PROCESS_CHECK'):
        self_namespace()
        identity=process_identity(pid)
    if identity[0]!=postgres:raise Inconclusive('EXECUTABLE_CHECK')
    with stage('SOCKET_PEER_CHECK'):
        if socket_peer(socket)!=(pid,os.getuid()):raise Inconclusive()
    # Exact local socket/port/user/database; no ambient libpq config or password file.
    env={'PATH':str(postgres.parent)+':/usr/bin:/bin','LANG':'C','PGPASSFILE':'/dev/null','PGSERVICEFILE':'/dev/null','PGCONNECT_TIMEOUT':'3','PGOPTIONS':'-c default_transaction_read_only=on'}
    sql="SELECT json_build_array(current_setting('data_directory'),current_setting('unix_socket_directories'),current_setting('port'),extract(epoch from pg_postmaster_start_time()))"
    with stage('SQL_CONNECT'):
        result=subprocess.run([str(psql),'-XAtw','-v','ON_ERROR_STOP=1','-h',str(sock),'-p','55433','-U',pwd.getpwuid(os.getuid()).pw_name,'-d','postgres','-c',sql],env=env,capture_output=True,text=True,timeout=5,check=True)
    with stage('SQL_RESULT_CHECK'):
        directory,sockets,port,server_start=json.loads(result.stdout)
        stamp=float(server_start)
        if not math.isfinite(stamp):raise Inconclusive()
    if directory!=str(data):raise Inconclusive('DATA_DIRECTORY_CHECK')
    if sockets!=str(sock) or port!='55433':raise Inconclusive('SQL_SOCKET_CHECK')
    # These are DIFFERENT PostgreSQL startup phases, not equivalent timestamps.
    # MyStartTime is written to pidfiles early; PgStartTime is assigned later.
    # Kernel peer PID/UID binds the listener instead of an arbitrary time tolerance.
    if stamp<=0:raise Inconclusive('START_TIME_CHECK')
    # Recheck snapshots against PID reuse/disappearance while SQL was running.
    with stage('SNAPSHOT_CHANGED'):
        if socket_peer(socket)!=(pid,os.getuid()):raise Inconclusive()
        if process_identity(pid)!=identity or small_text(pidfile)!=before or small_text(socketlock)!=lock_before:raise Inconclusive()
        now=socket.stat()
        if (now.st_dev,now.st_ino)!=(socket_info.st_dev,socket_info.st_ino):raise Inconclusive()
    return 'TASK_DB_IDENTITY_MATCH'

def main():
    reason=None
    if len(sys.argv)!=1:reason='ARGUMENT_CHECK'
    else:
        try:diagnose()
        except KeyboardInterrupt:reason='INTERRUPTED'
        except Inconclusive as error:reason=error.reason if error.reason in REASONS else 'INTERNAL_CHECK'
        except Exception:reason='INTERNAL_CHECK'
    if reason:print('CHECK_INCONCLUSIVE:'+reason);return 1
    print('TASK_DB_IDENTITY_MATCH');return 0
if __name__=='__main__':sys.exit(main())
