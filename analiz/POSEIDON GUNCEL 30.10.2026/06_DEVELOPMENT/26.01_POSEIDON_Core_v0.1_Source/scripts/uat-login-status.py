#!/usr/bin/env python3
"""Read only fixed Development UAT login boundary classifications, never raw logs."""
import os,pathlib,re,stat,sys
PATTERN=re.compile(r'^\s*POSEIDON_UAT_REJECT LOGIN (PEER_NOT_LOOPBACK|HOST_NOT_ALLOWED|ORIGIN_MISSING|ORIGIN_NULL|ORIGIN_LOCAL_HTTP_8080|ORIGIN_LOCAL_HTTPS_8080|ORIGIN_MULTIPLE|ORIGIN_OTHER)\s*$')
def classify(text):
    codes=[m.group(1) for line in text.splitlines() if (m:=PATTERN.fullmatch(line))]
    return 'LOGIN_REJECT:'+codes[-1] if codes else 'NO_LOGIN_REJECTION_OBSERVED'
def main():
    try:
        if len(sys.argv)!=1:raise ValueError()
        state=pathlib.Path.home()/'.poseidon-synthetic-uat';log=state/'app.log'
        if any(p.is_symlink() for p in (log,*log.parents)):raise ValueError()
        info=state.stat()
        if info.st_uid!=os.getuid() or info.st_mode & 0o077:raise ValueError()
        info=log.stat()
        if not stat.S_ISREG(info.st_mode) or info.st_uid!=os.getuid():raise ValueError()
        with log.open('rb') as f:
            f.seek(0,2);offset=max(0,f.tell()-131072);f.seek(offset)
            tail=f.read(131072)
            # A seek may land inside an unrelated line. Never promote its suffix.
            # Even at an exact boundary, conservatively discard the first line.
            if offset:tail=tail.partition(b'\n')[2]
            text=tail.decode('utf-8','replace')
        print(classify(text));return 0
    except (Exception,KeyboardInterrupt):print('LOGIN_CHECK_UNAVAILABLE');return 1
if __name__=='__main__':sys.exit(main())
