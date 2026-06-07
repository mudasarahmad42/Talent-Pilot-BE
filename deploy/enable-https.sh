#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
ENV_FILE="${REPO_ROOT}/.env.production"
COMPOSE_FILE="${REPO_ROOT}/docker-compose.prod.yml"

cd "${REPO_ROOT}"

if [ ! -f "${ENV_FILE}" ]; then
  echo "Missing .env.production. Copy .env.production.example first and update its values."
  exit 1
fi

get_env() {
  local key="$1"
  grep -E "^${key}=" "${ENV_FILE}" | tail -n 1 | cut -d= -f2- || true
}

set_env() {
  local key="$1"
  local value="$2"

  if grep -qE "^${key}=" "${ENV_FILE}"; then
    sed -i "s|^${key}=.*|${key}=${value}|" "${ENV_FILE}"
  else
    printf '\n%s=%s\n' "${key}" "${value}" >> "${ENV_FILE}"
  fi
}

APP_HOSTNAME="$(get_env APP_HOSTNAME)"
LETSENCRYPT_EMAIL="$(get_env LETSENCRYPT_EMAIL)"

if [ -z "${APP_HOSTNAME}" ]; then
  echo "APP_HOSTNAME is required in .env.production."
  exit 1
fi

if [ -z "${LETSENCRYPT_EMAIL}" ]; then
  echo "LETSENCRYPT_EMAIL is required in .env.production."
  exit 1
fi

echo "Bootstrapping HTTP challenge endpoint for ${APP_HOSTNAME}..."
NGINX_CONF=nginx.conf docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" up -d --build nginx

echo "Requesting Let's Encrypt certificate for ${APP_HOSTNAME}..."
docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" run --rm --entrypoint certbot \
  certbot certonly \
  --webroot \
  --webroot-path /var/www/certbot \
  --email "${LETSENCRYPT_EMAIL}" \
  --agree-tos \
  --no-eff-email \
  --non-interactive \
  -d "${APP_HOSTNAME}"

set_env NGINX_CONF nginx.https.conf
set_env GOOGLE_CALENDAR_REDIRECT_URI "https://${APP_HOSTNAME}/api/google-calendar/oauth/callback"

echo "Switching production stack to HTTPS..."
docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" up -d --build --force-recreate api nginx certbot

echo "HTTPS deployment complete."
docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" ps
