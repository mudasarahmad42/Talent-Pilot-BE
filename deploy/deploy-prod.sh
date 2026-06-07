#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

cd "${REPO_ROOT}"

if [ ! -f ".env.production" ]; then
  echo "Missing .env.production. Copy .env.production.example first and update its values."
  exit 1
fi

if [ ! -d "../frontend" ]; then
  echo "Missing sibling frontend checkout at ../frontend."
  echo "Clone Talent-Pilot-FE into /opt/talent-pilot/frontend before deploying."
  exit 1
fi

docker compose -f docker-compose.prod.yml --env-file .env.production up -d --build
docker compose -f docker-compose.prod.yml --env-file .env.production restart nginx
docker compose -f docker-compose.prod.yml --env-file .env.production ps
