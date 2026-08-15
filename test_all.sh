#!/usr/bin/env bash
set -euo pipefail

START_SERVICES=false
BASE_URL="${BASE_URL:-http://localhost:5000}"

if [[ "${1:-}" == "--start" ]]; then
  START_SERVICES=true
fi

cleanup() {
  if [[ "$START_SERVICES" == "true" ]]; then
    docker compose down -v --remove-orphans >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

if [[ "$START_SERVICES" == "true" ]]; then
  docker compose down -v --remove-orphans >/dev/null 2>&1 || true
  docker compose up -d --build
fi

for _ in {1..60}; do
  if curl -fsS "$BASE_URL/health" >/dev/null; then
    break
  fi
  sleep 2
done

curl -fsS "$BASE_URL/health" >/dev/null

echo "Health check passed."
echo "For the complete HTTP scenario matrix, run test_all.ps1."
