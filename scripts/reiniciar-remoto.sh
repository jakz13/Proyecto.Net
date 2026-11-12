#!/bin/bash
# Se ejecuta EN la VM (lo envían scripts/desplegar.ps1 y scripts/desplegar.sh por SSH).
# Reinicia los servicios por turnos para que el balanceador mantenga el sitio disponible:
# api1 -> espera a que esté sana -> api2 -> espera. Uso: reiniciar-remoto.sh "api1 worker portal-comercio"
set -e
SERVICIOS="$1"
cd ~/shippingbox
C="docker compose -f docker-compose.prod.yml --env-file .env.prod"

esperar() {
  for i in $(seq 1 36); do
    h=$(docker inspect -f '{{.State.Health.Status}}' "shippingbox-$1-1" 2>/dev/null || echo none)
    [ "$h" = healthy ] && { echo "  $1: sano"; return 0; }
    sleep 5
  done
  echo "ERROR: $1 no quedó sano"
  docker logs --tail 30 "shippingbox-$1-1"
  return 1
}

# Los archivos montados (Grafana, Prometheus, RabbitMQ) los leen contenedores con otro usuario: deben ser legibles
chmod -R a+rX docker

$C up -d --no-build --no-deps postgres redis rabbitmq caddy
for s in $SERVICIOS; do
  case "$s" in
    api1) for n in api1 api2; do $C up -d --no-build --no-deps $n; esperar $n; done ;;
    *)    $C up -d --no-build --no-deps $s ;;
  esac
done
$C up -d --no-build
# Prometheus y Grafana leen su configuración al arrancar: si cambió un archivo montado hay que reiniciarlos
case " $SERVICIOS " in
  *" prometheus "*|*" grafana "*) $C restart prometheus grafana ;;
esac
docker exec shippingbox-caddy-1 caddy reload --config /etc/caddy/Caddyfile >/dev/null 2>&1 || echo "  (aviso) Caddy no pudo recargar su configuración"
$C ps --format 'table {{.Service}}\t{{.Status}}'
