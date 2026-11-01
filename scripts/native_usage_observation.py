"""Bounded, read-only native quota observations for a busy profile's lastKnown UI.

No auth files, provider calls, session text export or dispatch/eligibility decisions.
Only explicit token_count rate-limit events under the selected profile home count.
"""
from datetime import datetime, timedelta, timezone
import json
import math
from pathlib import Path

TAIL_BYTES = 4 * 1024 * 1024
HEADER_BYTES = 128 * 1024
MAX_FILES = 128
MAX_AGE = 4500


def finite(value):
    return type(value) in (int, float) and math.isfinite(value)


def moment(value):
    try:
        parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
        return parsed.timestamp() if parsed.utcoffset() is not None else None
    except (ValueError, TypeError, AttributeError, OverflowError):
        return None


def event_observation(event, now):
    if not isinstance(event, dict) or event.get('type') != 'event_msg':
        return None
    payload = event.get('payload')
    stamp = moment(event.get('timestamp'))
    if (not isinstance(payload, dict) or payload.get('type') != 'token_count' or
            stamp is None or not 0 <= now - stamp <= MAX_AGE):
        return None
    limits = payload.get('rate_limits')
    if not isinstance(limits, dict) or limits.get('limit_id') != 'codex':
        return None
    windows = []
    for key in ('primary', 'secondary'):
        value = limits.get(key)
        if value is None:
            continue  # Never invent a five-hour bucket from a weekly one.
        if not isinstance(value, dict):
            return None
        used, duration, reset = (value.get(k) for k in ('used_percent', 'window_minutes', 'resets_at'))
        if (not finite(used) or not 0 <= used <= 100 or type(duration) is not int or
                not 1 <= duration <= 525600 or not finite(reset) or reset <= stamp):
            return None
        windows.append({'usedPercent': used, 'windowDurationMins': duration, 'resetsAt': reset})
    if not windows:
        return None
    result = {'windows': windows, 'observedAt': datetime.fromtimestamp(stamp, timezone.utc).isoformat()}
    if limits.get('plan_type') in ('pro', 'prolite', 'promax', 'plus'):
        result['plan'] = limits['plan_type']
    return result


def recent_usage(home, now):
    """Historical numeric observation only, even if the native process has ended."""
    sessions = Path(home) / 'sessions'
    if sessions.is_symlink():
        return None
    today = datetime.fromtimestamp(now).astimezone().date()
    newest = None
    seen = 0
    try:
        for day in (today, today - timedelta(days=1)):
            directory = sessions / f'{day.year:04}' / f'{day.month:02}' / f'{day.day:02}'
            if any(p.is_symlink() for p in (directory, directory.parent, directory.parent.parent)):
                continue
            if not directory.is_dir():
                continue
            for path in directory.iterdir():
                seen += 1
                if seen > MAX_FILES:
                    return None  # A truncated directory view cannot be called the latest.
                if path.is_symlink() or not path.is_file() or not path.name.startswith('rollout-') or path.suffix != '.jsonl':
                    continue
                with path.open('rb') as stream:
                    header = stream.readline(HEADER_BYTES + 1)
                    if len(header) > HEADER_BYTES:
                        continue
                    meta = json.loads(header)
                    if not isinstance(meta, dict) or meta.get('type') != 'session_meta':
                        continue
                    stream.seek(0, 2)
                    start = max(0, stream.tell() - TAIL_BYTES)
                    stream.seek(start)
                    lines = stream.read(TAIL_BYTES).splitlines()
                if start:
                    lines = lines[1:]  # Never parse the possible partial leading event.
                for line in lines:
                    if len(line) > 1024 * 1024:
                        continue
                    try:
                        observation = event_observation(json.loads(line), now)
                    except (ValueError, UnicodeError, RecursionError):
                        continue
                    if observation and (newest is None or moment(observation['observedAt']) > moment(newest['observedAt'])):
                        newest = observation
        return newest
    except (OSError, ValueError, UnicodeError, RecursionError):
        return None
