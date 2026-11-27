#!/usr/bin/env bash
# PostgreSQL backup of the POSEIDON and Keycloak databases (custom format), 14-day retention.
#   bash /opt/poseidon-uat/backup.sh <label>
# Restore (runbook): docker compose ... exec -T postgres pg_restore -U poseidon -d <db> --clean --if-exists < file
set -euo pipefail
cd /opt/poseidon-uat
LABEL="${1:-manual}"
[[ "$LABEL" =~ ^[A-Za-z0-9._-]{1,80}$ ]] || { echo "invalid label" >&2; exit 2; }
TAG="$(cat deployed-tag 2>/dev/null || echo none)"
TS="$(date -u +%Y%m%dT%H%M%SZ)"
umask 077
mkdir -p backups
for db in poseidon keycloak; do
  POSEIDON_TAG="$TAG" docker compose -f compose.uat.yaml exec -T postgres \
    pg_dump -U poseidon -d "$db" -Fc > "backups/${db}-${TS}-${LABEL}.dump"
done
find backups -name '*.dump' -mtime +14 -delete
echo "backup ok ${TS}-${LABEL}"
