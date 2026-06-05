#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

cd "${REPO_ROOT}"

if [ ! -f ".env.production" ]; then
  echo "Missing .env.production. Copy .env.production.example first and update its values."
  exit 1
fi

set -a
# shellcheck disable=SC1091
. ./.env.production
set +a

LLM_MODEL="${OLLAMA_LLM_MODEL:-llama3.2:1b}"
EMBEDDING_MODEL="${OLLAMA_EMBEDDING_MODEL:-nomic-embed-text}"

docker compose -f docker-compose.prod.yml --env-file .env.production up -d ollama
docker compose -f docker-compose.prod.yml --env-file .env.production exec -T ollama ollama pull "${LLM_MODEL}"
docker compose -f docker-compose.prod.yml --env-file .env.production exec -T ollama ollama pull "${EMBEDDING_MODEL}"
docker compose -f docker-compose.prod.yml --env-file .env.production exec -T ollama ollama list
