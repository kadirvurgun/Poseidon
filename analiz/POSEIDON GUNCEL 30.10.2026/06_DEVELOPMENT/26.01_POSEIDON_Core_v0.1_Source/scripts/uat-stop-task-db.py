#!/usr/bin/env python3
"""Explicit graceful stop of the verified synthetic task DB; never a general PID tool."""
import fcntl,os,pathlib,select,signal,stat,sys,types
ROOT=pathlib.Path(__file__).resolve().parents[1]
# Load reviewed source directly: neither create nor execute a generated .pyc cache.
source=ROOT/'scripts/uat-diagnose.py'
diag=types.ModuleType('uat_stop_diagnostic');diag.__file__=str(source)
exec(compile(source.read_bytes(),str(source),'exec'),diag.__dict__)
class Refused(Exception):pass

def exited(fd,timeout_ms=0):
    poll=select.poll();poll.register(fd,select.POLLIN)
    return bool(poll.poll(timeout_ms))

def stop():
    if not hasattr(os,'pidfd_open') or not hasattr(signal,'pidfd_send_signal'):raise Refused()
    state=pathlib.Path.home()/'.poseidon-synthetic-uat';data=state/'pgdata';sock=state/'socket'
    for p in (state,data,sock):diag.checked(p,True)
    lock=state/'supervisor.lock';info=diag.checked(lock)
    # No create/unlink: coordinate with launcher on its existing inode.
    fd=os.open(lock,os.O_RDWR|os.O_NOFOLLOW|os.O_CLOEXEC)
    try:
        opened=os.fstat(fd)
        if (opened.st_dev,opened.st_ino)!=(info.st_dev,info.st_ino):raise Refused()
        try:fcntl.flock(fd,fcntl.LOCK_EX|fcntl.LOCK_NB)
        except BlockingIOError:return 'STOP_REFUSED_SUPERVISOR_BUSY',1
        pidfile=data/'postmaster.pid'
        before=diag.small_text(pidfile);pid=int(before.splitlines()[0])
        if pid<=0:raise Refused()
        diag.self_namespace()
        identity=diag.process_identity(pid)
        pinned=os.pidfd_open(pid,0)
        try:
            if exited(pinned):raise Refused()
            if diag.diagnose()!='TASK_DB_IDENTITY_MATCH':raise Refused()
            if diag.small_text(pidfile)!=before or diag.process_identity(pid)!=identity:raise Refused()
            if diag.socket_peer(sock/'.s.PGSQL.55433')!=(pid,os.getuid()):raise Refused()
            current=diag.checked(lock)
            if (current.st_dev,current.st_ino)!=(opened.st_dev,opened.st_ino) or exited(pinned):raise Refused()
            # Only the pinned, freshly verified process. No numeric PID signal fallback.
            signal.pidfd_send_signal(pinned,signal.SIGTERM,None,0)
            if not exited(pinned,30000):return 'STOP_PENDING_NO_ESCALATION',1
            # PostgreSQL normally removes its own files. Never remove residuals here.
            if pidfile.exists() or pidfile.is_symlink() or (sock/'.s.PGSQL.55433').exists() or (sock/'.s.PGSQL.55433.lock').exists():
                return 'STOPPED_REQUIRES_INSPECTION',1
            return 'TASK_DB_STOPPED',0
        finally:os.close(pinned)
    finally:os.close(fd)

def main():
    if sys.argv[1:]!=['--graceful']:print('STOP_REFUSED_ARGUMENTS');return 1
    try:message,code=stop()
    except KeyboardInterrupt:message,code='STOP_INTERRUPTED_STATE_UNKNOWN',1
    except Exception:message,code='STOP_REFUSED_OR_UNCONFIRMED',1
    print(message);return code
if __name__=='__main__':sys.exit(main())
