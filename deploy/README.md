# Talent Pilot VPS Deployment

This deploys Talent Pilot on one Ubuntu 24.04 Contabo VPS with Docker Compose.

Only port `80` is public. SQL Server, Ollama, the API, the worker, and the frontend container stay on the private Docker network. SQL Server uses the 2025 image because Talent Pilot stores embeddings in SQL Server `VECTOR(768)` columns.

## First Deploy

```bash
ssh root@13.140.139.57
apt update && apt install -y git
mkdir -p /opt/talent-pilot
cd /opt/talent-pilot

git clone -b deploy-talent-pilot https://github.com/mudasarahmad42/Talent-Pilot-BE.git backend
git clone -b deploy-talent-pilot https://github.com/mudasarahmad42/Talent-Pilot-FE.git frontend

cd backend
cp .env.production.example .env.production
nano .env.production

chmod +x deploy/*.sh
./deploy/install-server-prereqs.sh
./deploy/deploy-prod.sh
./deploy/init-ollama-models.sh
```

Use a strong `SA_PASSWORD` and `JWT_SIGNING_KEY` in `.env.production`.

## Diagnostics

```bash
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs -f api
docker compose -f docker-compose.prod.yml logs -f worker
docker compose -f docker-compose.prod.yml logs -f ollama
```

## Update Existing Deployment

```bash
cd /opt/talent-pilot/backend
git pull
cd ../frontend
git pull
cd ../backend
./deploy/deploy-prod.sh
```

## Test URLs

```text
http://13.140.139.57
http://13.140.139.57/api/health
```
