#!/usr/bin/env bash
# POSEIDON UAT deploy, run on the VPS as 'deploy' by the UAT deploy workflow:
#   bash /opt/poseidon-uat/deploy.sh <image-tag> <git-sha>
# Steps: pull exact images -> render config -> pre-migration backup -> migrate (forward-only)
# -> first-run synthetic seed -> start -> health check -> on failure restart the previous images.
# A failed migration is NOT rolled back automatically; restore from backups/ by hand (runbook).
set -euo pipefail
cd /opt/poseidon-uat

TAG="${1:?image tag required}"
SHA="${2:?git sha required}"
[[ "$TAG" =~ ^[a-z0-9][a-z0-9._-]{0,127}$ ]] || { echo "invalid tag" >&2; exit 2; }
[[ -f state/initialized ]] || { echo "run: python3 manage.py init ... first" >&2; exit 2; }

PREV="$(cat deployed-tag 2>/dev/null || true)"
DOMAIN="$(grep '^UAT_DOMAIN=' .env | cut -d= -f2)"
dc() { POSEIDON_TAG="$1" docker compose -f compose.uat.yaml "${@:2}"; }

log() { printf '%s %s\n' "$(date -u +%FT%TZ)" "$*" | tee -a deploy-history.log; }

healthy() {
  for _ in $(seq 1 60); do
    if curl -fsS --max-time 5 --resolve "${DOMAIN}:443:127.0.0.1" "https://${DOMAIN}/health/live" 2>/dev/null | grep -q alive \
       && curl -fsS --max-time 5 --resolve "${DOMAIN}:443:127.0.0.1" "https://${DOMAIN}/auth/realms/poseidon/.well-known/openid-configuration" >/dev/null 2>&1; then
      return 0
    fi
    sleep 5
  done
  return 1
}

log "deploy start tag=$TAG sha=$SHA prev=${PREV:-none}"
# SKIP_PULL=1: images were built locally on this host (first bring-up before the workflow is on main).
if [[ "${SKIP_PULL:-0}" != 1 ]]; then
  dc "$TAG" --profile ops pull web api migrator
fi
python3 manage.py render

dc "$TAG" up -d postgres
if [[ -n "$PREV" ]]; then
  bash backup.sh "pre-${TAG}"
fi

log "migrate"
dc "$TAG" --profile ops run --rm migrator

if [[ ! -f state/seeded ]]; then
  # Synthetic seed + FICTIONAL demo drafts (no decisions, verifications, signatures or credentials).
  # The migrator only allows these in Development/Test; this is the documented UAT exception.
  log "first-run synthetic seed"
  dc "$TAG" --profile ops run --rm -e DOTNET_ENVIRONMENT=Development migrator --seed /app/backend/seed/synthetic-demo.json
  dc "$TAG" --profile ops run --rm -e DOTNET_ENVIRONMENT=Development migrator --demo-fixtures /app/backend/seed/synthetic-demo.json
  date -u +%FT%TZ > state/seeded
fi

dc "$TAG" up -d --remove-orphans web api keycloak postgres
# Keycloak serves theme resources from a gzip cache inside the container (data/tmp) that survives a
# restart: recreate the container only when the mounted theme files changed.
THEME_HASH="$(find keycloak/themes -type f -print0 2>/dev/null | sort -z | xargs -0 sha256sum 2>/dev/null | sha256sum | cut -c1-16)"
if [[ "$THEME_HASH" != "$(cat state/theme-hash 2>/dev/null || true)" ]]; then
  log "login theme changed: recreating keycloak"
  dc "$TAG" up -d --force-recreate --no-deps keycloak
fi
if healthy; then
  echo "$THEME_HASH" > state/theme-hash
  echo "$TAG" > deployed-tag
  echo "$SHA" > deployed-sha
  python3 manage.py branding >/dev/null 2>&1 || log "WARN realm branding not applied"
  log "deploy ok tag=$TAG"
  docker image prune -af --filter "until=168h" >/dev/null || true
  # Nightly backup at 02:30 UTC (idempotent).
  # (crontab -l fails when no crontab exists yet; grep -v fails on empty input.)
  { crontab -l 2>/dev/null | grep -v 'poseidon-uat/backup.sh' || true
    echo '30 2 * * * bash /opt/poseidon-uat/backup.sh nightly >/dev/null 2>&1'; } | crontab -
  exit 0
fi

log "deploy FAILED tag=$TAG (health check)"
dc "$TAG" ps || true
dc "$TAG" logs --tail 80 api keycloak web 2>&1 | tail -n 200 || true
if [[ -n "$PREV" ]]; then
  log "restarting previous images tag=$PREV (database stays at the new schema)"
  dc "$PREV" up -d --remove-orphans web api keycloak postgres || true
fi
exit 1
