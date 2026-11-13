#!/bin/bash
# Actualiza ClockMetrics en el servidor con lo último de GitHub (backend y frontend).
# Uso, en el servidor:  bash /opt/clockmetrics/backend/deploy.sh
# No aplica cambios de base de datos: esos se corren antes, a mano (ver DEPLOY.md).
set -euo pipefail

ROOT=/opt/clockmetrics

echo "== Bajando el código"
git -C "$ROOT/backend" pull --ff-only
git -C "$ROOT/frontend" pull --ff-only
echo "   backend:  $(git -C "$ROOT/backend" log --oneline -1)"
echo "   frontend: $(git -C "$ROOT/frontend" log --oneline -1)"

echo "== Construyendo las imágenes (la versión actual sigue funcionando mientras tanto)"
docker build -q -t clockmetrics-backend:latest "$ROOT/backend" > /dev/null
docker build -q -t clockmetrics-frontend:latest "$ROOT/frontend" > /dev/null

echo "== Cambiando los contenedores"
docker network inspect clockmetrics-net > /dev/null 2>&1 || docker network create clockmetrics-net > /dev/null
docker rm -f clockmetrics-backend clockmetrics-frontend > /dev/null 2>&1 || true

docker run -d --name clockmetrics-backend --network clockmetrics-net --restart unless-stopped \
  --env-file /root/clockmetrics-backend.env --add-host host.docker.internal:host-gateway \
  -p 127.0.0.1:5100:8080 clockmetrics-backend:latest > /dev/null

docker run -d --name clockmetrics-frontend --network clockmetrics-net --restart unless-stopped \
  -p 127.0.0.1:5101:80 clockmetrics-frontend:latest > /dev/null

docker image prune -f > /dev/null

echo "== Revisando"
sleep 8
docker ps --format "   {{.Names}}: {{.Status}}" | grep clockmetrics
echo "   sitio: $(curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:8100/)"
echo "Listo: http://148.230.73.226:8100"
