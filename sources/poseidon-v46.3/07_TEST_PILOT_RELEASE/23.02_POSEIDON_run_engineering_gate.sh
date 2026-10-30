#!/usr/bin/env bash
set -euo pipefail

ROOT="${1:-./POSEIDON_v46.2_Applicability_Backend}"
cd "$ROOT"

echo "== POSEIDON v46.2 Engineering Gate =="
dotnet --info
docker --version
docker compose version

echo "== Start PostgreSQL =="
docker compose up -d postgres

echo "== Wait for PostgreSQL =="
for i in {1..30}; do
  if docker compose exec -T postgres pg_isready -U poseidon -d poseidon >/dev/null 2>&1; then break; fi
  sleep 2
done

echo "== Apply DDL =="
docker compose exec -T postgres psql -U poseidon -d poseidon < database/001_applicability_engine.sql

echo "== Apply controlled candidate seed =="
docker compose exec -T postgres psql -U poseidon -d poseidon < database/002_seed_sample_rules.sql

echo "== Restore =="
dotnet restore Poseidon.Applicability.sln

echo "== Build =="
dotnet build Poseidon.Applicability.sln -c Release --no-restore

echo "== Tests =="
dotnet test Poseidon.Applicability.sln -c Release --no-build --logger "trx;LogFileName=poseidon-gate.trx"

echo "== SQL sanity =="
docker compose exec -T postgres psql -U poseidon -d poseidon -c \
"SELECT count(*) AS rule_count FROM applicability.rule_versions;"

echo "== Gate PASS =="
