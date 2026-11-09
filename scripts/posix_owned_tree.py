"""Linux invocation-owned session, reserved until every live member has stopped.

The unreaped session leader reserves its PID/SID; pidfds target exact members,
including native runner groups whose API parent has already exited. Runners in
this trusted session must inherit it rather than call setsid themselves.
"""
import os
from pathlib import Path
import signal
import subprocess
import sys
import time

SESSION_ENV = 'POSEIDON_GATE_OWNED_POSIX_SESSION'


def _stat(pid):
    # comm may contain spaces and parentheses. Fields after the final ')' start
    # at field 3: state, ppid, pgrp, session, ..., starttime (field 22).
    fields = Path(f'/proc/{pid}/stat').read_text().rsplit(')', 1)[1].split()
    return fields[0], int(fields[3]), int(fields[19])


def native_group_options(env):
    marker = (os.environ if env is None else env).get(SESSION_ENV)
    if marker is None:
        return {'start_new_session': True}
    try:
        sid, born = map(int, marker.split(':'))
        if sid != os.getsid(0) or _stat(sid)[1:] != (sid, born):
            raise ValueError()
    except (OSError, ValueError, IndexError):
        raise RuntimeError('owned_posix_session_unproven') from None
    # Each capture can still stop its own native group on its own deadline.
    # The outer gate retains authority over every group in this session.
    return {'process_group': 0}


class PosixOwnedTree:
    def __init__(self, command, env, cwd):
        if (not sys.platform.startswith('linux') or not hasattr(os, 'pidfd_open')
                or not hasattr(signal, 'pidfd_send_signal') or not hasattr(os, 'WNOWAIT')
                or not Path('/proc/self/stat').is_file()):
            raise RuntimeError('owned_posix_containment_unavailable')
        clean = {k: v for k, v in env.items() if k != SESSION_ENV}
        self.process = subprocess.Popen([sys.executable, str(Path(__file__).resolve()), *command],
            cwd=cwd, env=clean, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
            start_new_session=True)
        self.pid = self.process.pid
        self.returncode = None

    def wait(self, timeout):
        deadline = time.monotonic() + timeout
        while True:
            # Popen.wait/poll reap the leader and would release its SID for reuse.
            status = os.waitid(os.P_PID, self.pid, os.WEXITED | os.WNOHANG | os.WNOWAIT)
            if status is not None:
                self.returncode = status.si_status if status.si_code == os.CLD_EXITED else -status.si_status
                return self.returncode
            if time.monotonic() >= deadline:
                raise subprocess.TimeoutExpired('owned-posix-child', timeout)
            time.sleep(.01)

    def _members(self):
        # Only members of the still-reserved, invocation-created SID are owned.
        for entry in Path('/proc').iterdir():
            if not entry.name.isdecimal():
                continue
            try:
                state, sid, born = _stat(int(entry.name))
            except (FileNotFoundError, ProcessLookupError):
                continue
            if sid == self.pid and state not in ('Z', 'X'):
                yield int(entry.name), born

    def active(self):
        return sum(1 for _ in self._members())

    def stop(self):
        deadline = time.monotonic() + 10
        while True:
            members = list(self._members())
            if not members:
                return
            for pid, born in members:
                descriptor = None
                try:
                    descriptor = os.pidfd_open(pid)
                    state, sid, current_born = _stat(pid)
                    if sid == self.pid and current_born == born and state not in ('Z', 'X'):
                        signal.pidfd_send_signal(descriptor, signal.SIGKILL)
                except (FileNotFoundError, ProcessLookupError):
                    pass
                finally:
                    if descriptor is not None:
                        os.close(descriptor)
            # A member may have forked before its pidfd was signalled. Repeat
            # until no live member remains: only then is further forking impossible.
            if time.monotonic() >= deadline:
                raise RuntimeError('owned_posix_stop_unproven')
            time.sleep(.01)

    def close(self):
        if self.active():
            raise RuntimeError('owned_posix_stop_unproven')
        self.process.wait(timeout=10)  # Release reserved leader only after proof.


if __name__ == '__main__':
    sid = os.getsid(0)
    if sid != os.getpid() or len(sys.argv) < 2:
        raise SystemExit(2)
    os.environ[SESSION_ENV] = f'{sid}:{_stat(sid)[2]}'
    os.execvpe(sys.argv[1], sys.argv[1:], os.environ)
