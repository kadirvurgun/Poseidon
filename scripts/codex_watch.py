"""OPS-HOURLY-01: metadata-only quota checks; explicit trusted task opt-in."""

import argparse
from datetime import datetime, timezone
import json
import math
from pathlib import Path
import time

from codex_accounts import BusyError, Workers, WorkerStillRunningError, exclusive, write_json

PROFILES = ('esra-pro', 'kadir-plus')
INTERVAL = 3600
RESERVE = 85


def eligible(status):
    windows = status.get('windows')
    return (status.get('ready') is True and status.get('authenticated') is True
            and status.get('plan_matches') is True and status.get('limits_known') is True
            and isinstance(windows, list) and bool(windows)
            and all(isinstance(w, dict) and type(w.get('usedPercent')) in (int, float)
                    and math.isfinite(w['usedPercent']) and 0 <= w['usedPercent'] < RESERVE
                    for w in windows))


def timestamp(value):
    return datetime.fromtimestamp(value, timezone.utc).isoformat()


def safe_status(profile, raw):
    # Explicit allowlist: never persist arbitrary metadata, identities or diagnostics.
    result = {'profile': profile, 'state': 'known', 'ready': eligible(raw)}
    for key in ('authenticated', 'plan_matches', 'exhausted', 'limits_known'):
        result[key] = raw.get(key) is True
    result['plan'] = raw.get('plan') if raw.get('plan') in {'pro', 'prolite', 'promax', 'plus'} else 'unknown'
    result['windows'] = []
    for window in raw.get('windows', []):
        result['windows'].append({key: value for key in ('usedPercent', 'windowDurationMins', 'resetsAt')
                                  if type(value := window.get(key)) in (int, float) and math.isfinite(value)})
    return result


class Watch:
    def __init__(self, workers=None, wall=time.time):
        self.workers = workers or Workers()
        self.wall = wall

    def snapshot(self):
        profiles = {}
        for profile in PROFILES:
            try:
                with exclusive(self.workers.home(profile) / 'worker.lock'):
                    profiles[profile] = safe_status(profile, self.workers.account(profile))
            except BusyError:
                profiles[profile] = {'profile': profile, 'state': 'busy', 'ready': False}
            except WorkerStillRunningError:
                # Keep this profile locked. Metadata-only uncertainty must not
                # freeze future hourly checks of the independent healthy profile.
                profiles[profile] = {'profile': profile, 'state': 'reconcile_required', 'ready': False}
            except Exception:
                profiles[profile] = {'profile': profile, 'state': 'unavailable', 'ready': False}
        now = self.wall()
        snapshot = {'timestamp': timestamp(now), 'next_check': timestamp(now + INTERVAL), 'profiles': profiles,
                    'selected': next((p for p in PROFILES if profiles[p]['ready']), None)}
        write_json(self.workers.runtime / 'quota.json', snapshot)
        return snapshot

    def once(self, task=None):
        with exclusive(self.workers.runtime / 'watch.lock'):
            snapshot = self.snapshot()
            if task is None:
                return snapshot
            receipt = self.workers.runtime / 'dispatch.json'
            # One durable opt-in dispatch only, including across restarts. Never
            # requeue blocked/review/uncertain work or silently resume old jobs.
            jobs = self.workers.runtime / 'jobs'
            if receipt.exists() or (jobs.exists() and any(jobs.iterdir())):
                snapshot['dispatch'] = 'existing_job_requires_review'
            elif any(p['state'] != 'known' for p in snapshot['profiles'].values()):
                snapshot['dispatch'] = 'account_unknown_or_busy'
            elif snapshot['selected'] is None:
                snapshot['dispatch'] = 'no_eligible_account'
            else:
                write_json(receipt, {'status': 'dispatching', 'profile': snapshot['selected']})
                try:
                    state = self.workers.run(task, snapshot['selected'], single_account=True, eligibility=eligible)
                    write_json(receipt, {'status': state['status'], 'job_id': state['job_id']})
                    snapshot['dispatch'] = state['status']
                except BaseException:
                    # Receipt stays dispatching; human reconciliation is required.
                    raise
            write_json(self.workers.runtime / 'quota.json', snapshot)
            return snapshot


def run_loop(watch, task=None, monotonic=time.monotonic, sleep=time.sleep):
    while True:
        deadline = monotonic() + INTERVAL
        watch.once(task)
        # Monotonic waits resist wall-clock changes; no catch-up burst after a
        # slow job. Short sleeps permit prompt Ctrl+C cancellation.
        now = monotonic()
        if now >= deadline:
            deadline = now + INTERVAL
        while (remaining := deadline - monotonic()) > 0:
            sleep(min(remaining, 30))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', nargs='?', choices=['once'], default='once')
    parser.add_argument('--loop', action='store_true')
    parser.add_argument('--task', type=Path, help='Trusted local task; authorizes one bounded model job only')
    args = parser.parse_args()
    try:
        watch = Watch()
        if args.loop:
            run_loop(watch, args.task)
        else:
            watch.once(args.task)
    except KeyboardInterrupt:
        return 130
    except Exception:
        print(json.dumps({'status': 'unavailable', 'ready': False}))
        return 1
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
