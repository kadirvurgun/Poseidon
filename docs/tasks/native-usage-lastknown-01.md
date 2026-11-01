# NATIVE-USAGE-LASTKNOWN-01

AUTHORED_UNVERIFIED. Supporting operations software for the user's explicit
hourly account usage/dashboard request, not a new canonical backlog story.
No tests, provider calls, account changes, browser journeys or builds executed
during authoring. Current native S0 product development is preserved.

## Concrete issue and contract

The busy Esra profile cannot acquire `worker.lock` for a direct metadata read.
The existing watcher therefore retained its old34% weekly observation while
the active native session had already reported53%. Busy is a dispatch state;
it does not mean no ongoing model use. A newer genuine native usage observation
can update the display without proving the account is eligible for more work.

`scripts/native_usage_observation.py` reads only the selected profile home's
`sessions/YYYY/MM/DD/rollout-*.jsonl` for today and yesterday. Limits:128 directory
entries,128KiB header,4MiB per-file tail,1MiB maximum event line and4500-second
observation freshness. Only `event_msg/token_count/rate_limits` with `limit_id`
`codex`, valid finite numeric windows and an actual event timestamp contributes.
Partial current events and unrelated tool/source text are ignored. Newest
actual provider timestamp wins, not filename order. Malformed/missing/unbounded
input yields unavailable observation. No auth files or remote calls are used.

Output contains only reported windows, timestamp and an allowlisted plan.
No transcript, instructions, identifiers, paths, cookies, prompt text, credential
balance or raw errors reach the quota snapshot. Credit balance0 is not a zero
subscription quota. A missing five-hour bucket stays missing; weekly windows are
never relabeled. This is a historical observation, not proof a process is live.

`Watch.snapshot` consults this display-only observation only for a busy profile,
and only replaces an older `lastKnown`. It preserves busy/ready=false and the
existing account lock, Esra-first routing and direct-provider eligibility rules.
It cannot select Kadir from a partial native observation or dispatch a task.
The existing dashboard contract already displays `lastKnown`, so no invented
live/eligible state or new frontend field is introduced.

## Local trace and meaningful tests

| Trace | Required evidence | Authored coverage |
|---|---|---|
| NUL-01 | Correct account-local reported windows, no invented bucket or credit inference | Actual synthetic JSONL reader tests; sibling profile isolation and newest timestamp selection |
| NUL-02 | No raw private content is projected | Header/tool/payload private markers remain absent from actual returned numeric projection |
| NUL-03 | Fail closed for stale/future/nonfinite/malformed/partial/budget inputs | Adversarial event/file tests including oversized private tool output and interrupted final event |
| NUL-04 | Busy remains ineligible and cannot trigger fallback/dispatch | Watch integration test holds the real exclusive fixture lock; only the other profile metadata is read, no task call occurs |
| NUL-05 | A newer direct-provider observation is retained | Watch test supplies an older native event and verifies it cannot replace direct metadata |

Five reader tests plus two watcher tests were authored. All execution remains
**DEFERRED / NOT RUN** until the larger foundation gate. Reading actual native
quota metadata later is operational observation, not a passing regression test.
No new dependency, entity, migration, seed, approval or canonical SP.
