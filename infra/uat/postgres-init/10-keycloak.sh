#!/usr/bin/env bash
# Runs once, on an empty data directory: a separate login role and database for Keycloak.
set -euo pipefail
kc_password="$(cat "${KEYCLOAK_DB_PASSWORD_FILE}")"
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  --set=kc_password="$kc_password" <<'SQL'
CREATE ROLE keycloak LOGIN PASSWORD :'kc_password';
CREATE DATABASE keycloak OWNER keycloak;
SQL
