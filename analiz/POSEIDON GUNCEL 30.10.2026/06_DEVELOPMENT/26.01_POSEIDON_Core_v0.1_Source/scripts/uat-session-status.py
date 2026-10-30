#!/usr/bin/env python3
"""Read-only bounded fixed session events; never print raw log lines."""
import datetime,os,pathlib,re,stat,sys
CODES=('PROCESS_STARTED','TICKET_CREATED','TICKET_EXPIRED','TICKET_MISSING','TICKET_REMOVED','LOGIN_CREATED','LOGIN_EXISTING_SESSION','LOGIN_CSRF_REJECTED','AUTH_401_COOKIE_PRESENT','AUTH_401_COOKIE_ABSENT','PRINCIPAL_REJECTED','LOGOUT_COMPLETED')
PATTERN=re.compile(r'^\s*POSEIDON_UAT_SESSION ([0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}[.][0-9]{3}Z) ('+'|'.join(CODES)+r') AGE_SECONDS=(-?[0-9]{1,5}) REMAINING_SECONDS=(-?[0-9]{1,5})\s*$')
def classify(text):
    rows=[]
    for line in text.splitlines():
        m=PATTERN.fullmatch(line)
        if not m:continue
        try:datetime.datetime.strptime(m[1],'%Y-%m-%dT%H:%M:%S.%fZ')
        except ValueError:continue
        age,remaining=int(m[3]),int(m[4])
        if not -1<=age<=86400 or not -86401<=remaining<=86400:continue
        rows.append(m[1]+' '+m[2]+' AGE_SECONDS='+str(age)+' REMAINING_SECONDS='+str(remaining))
    return '\n'.join(rows[-40:]) if rows else 'NO_SESSION_DIAGNOSTICS_IN_RETAINED_TAIL'
def main():
    try:
        if len(sys.argv)!=1:raise ValueError()
        state=pathlib.Path.home()/'.poseidon-synthetic-uat';log=state/'app.log'
        if any(p.is_symlink() for p in (log,*log.parents)):raise ValueError()
        info=state.stat()
        if info.st_uid!=os.getuid() or info.st_mode & 0o077:raise ValueError()
        with os.fdopen(os.open(log,os.O_RDONLY|os.O_NOFOLLOW),'rb') as f:
            info=os.fstat(f.fileno())
            if not stat.S_ISREG(info.st_mode) or info.st_uid!=os.getuid():raise ValueError()
            f.seek(0,2);offset=max(0,f.tell()-131072);f.seek(offset);tail=f.read(131072)
            if offset:tail=tail.partition(b'\n')[2]
            if tail and not tail.endswith(b'\n'):tail=tail.rpartition(b'\n')[0]
        print(classify(tail.decode('utf-8','replace')));return 0
    except (Exception,KeyboardInterrupt):print('SESSION_CHECK_UNAVAILABLE');return 1
if __name__=='__main__':sys.exit(main())
