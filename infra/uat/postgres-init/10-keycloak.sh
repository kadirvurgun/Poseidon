#!/usr/bin/env bash
# Runs once, on an empty data directory: a separate login role and database for Keycloak.
# The credential is read from the compose file secret and passed as a psql variable (%L quoted).
set -euo pipefail
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  --set=kc_db_credential="$(cat "${KEYCLOAK_DB_PASSWORD_FILE}")" <<'SQL'
SELECT format('CREATE ROLE keycloak LOGIN PASSWORD %L', :'kc_db_credential') \gexec
CREATE DATABASE keycloak OWNER keycloak;
SQL
