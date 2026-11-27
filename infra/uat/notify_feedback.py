"""After a successful UAT deploy, comment on and close the feedback issues it delivered.

Convention (no GitHub closing keyword, so issues stay open until the fix is really deployed):
    UAT-Fixes: #12            (in a commit message or a merged PR body)
    UAT-Fixes: #12, #15
Usage (repository root, full history):
    python infra/uat/notify_feedback.py --from <prev-sha|''> --to <sha> --tag <tag> --repo owner/Poseidon-UAT [--dry-run]
Needs GH_TOKEN with issues:write on the feedback repo. Prints only issue numbers.
"""
import argparse
import json
import re
import subprocess
import sys

TRAILER = re.compile(r'^\s*UAT-Fixes:\s*(.+)$', re.IGNORECASE | re.MULTILINE)
NUMBER = re.compile(r'#(\d{1,7})\b')
MERGE = re.compile(r'^Merge pull request #(\d+)', re.MULTILINE)


def git(*args: str) -> str:
    return subprocess.run(['git', *args], check=True, capture_output=True, text=True).stdout


def referenced(text: str) -> set[int]:
    found = set()
    for line in TRAILER.findall(text):
        found.update(int(n) for n in NUMBER.findall(line))
    return found


def collect(start: str, end: str, code_repo: str) -> set[int]:
    # First deploy (no previous sha), a rollback to an older commit or unrelated history: announce nothing.
    if not start or subprocess.run(['git', 'merge-base', '--is-ancestor', start, end]).returncode != 0:
        return set()
    messages = git('log', '--format=%B%x00', f'{start}..{end}')
    issues = referenced(messages)
    for pr in sorted(set(MERGE.findall(messages))):
        body = subprocess.run(['gh', 'pr', 'view', pr, '--repo', code_repo, '--json', 'body'],
                              capture_output=True, text=True)
        if body.returncode == 0:
            issues |= referenced(json.loads(body.stdout).get('body') or '')
    return issues


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument('--from', dest='start', default='')
    p.add_argument('--to', dest='end', required=True)
    p.add_argument('--tag', required=True)
    p.add_argument('--repo', required=True)
    p.add_argument('--code-repo', default='kadirvurgun/Poseidon')
    p.add_argument('--dry-run', action='store_true')
    a = p.parse_args()
    if not re.fullmatch(r'[0-9a-f]{40}', a.end) or (a.start and not re.fullmatch(r'[0-9a-f]{40}', a.start)):
        print('invalid_sha', file=sys.stderr)
        return 2
    issues = collect(a.start, a.end, a.code_repo)
    if not issues:
        print('no UAT-Fixes references in this deploy')
        return 0
    comment = (f'UAT ortamında yayında: sürüm `{a.tag}` (commit {a.end[:7]}).\n\n'
               'Lütfen bu sürümde tekrar deneyin. Sorun sürüyorsa bildirimi yeniden açın (Reopen) ve ne gördüğünüzü yazın.')
    failed = 0
    for n in sorted(issues):
        print(f'issue #{n}')
        if a.dry_run:
            continue
        ok = subprocess.run(['gh', 'issue', 'comment', str(n), '--repo', a.repo, '--body', comment]).returncode == 0
        ok = ok and subprocess.run(['gh', 'issue', 'close', str(n), '--repo', a.repo, '--reason', 'completed']).returncode == 0
        failed += 0 if ok else 1
    return 1 if failed else 0


if __name__ == '__main__':
    raise SystemExit(main())
