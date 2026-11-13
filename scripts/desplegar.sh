#!/bin/bash
# Despliega en la VM de Azure. Lo usa el pipeline (GitHub Actions) y sirve en Linux/Mac.
# En Windows usar scripts/desplegar.bat.
#
# Uso:      scripts/desplegar.sh [api] [worker] [backoffice] [portal] [panel]   (sin argumentos: todos)
# Variables: VM_HOST (IP de la VM, obligatoria), VM_USUARIO (por defecto azureuser),
#            VM_CLAVE_SSH (ruta a la clave privada, por defecto ~/.ssh/azure_ultimamilla),
#            DOMINIO, DOMINIO_COMERCIO, DOMINIO_PANEL (para verificar al final)
#            REGISTRO (prefijo de las imágenes, por defecto ghcr.io/jakz13/ultimamilla)
#            DESDE_REGISTRO=1: no construye ni envía imágenes; la VM las descarga del registro (lo usa el pipeline, que ya las publicó)
#              con GHCR_USUARIO y GHCR_TOKEN si el paquete no es público
set -euo pipefail
cd "$(dirname "$0")/.."

VM_HOST="${VM_HOST:?definir VM_HOST (IP de la VM)}"
VM_USUARIO="${VM_USUARIO:-azureuser}"
VM_CLAVE_SSH="${VM_CLAVE_SSH:-$HOME/.ssh/azure_ultimamilla}"
DOMINIO="${DOMINIO:-shippingbox.duckdns.org}"
DOMINIO_COMERCIO="${DOMINIO_COMERCIO:-shippingbox-comercio.duckdns.org}"
DOMINIO_PANEL="${DOMINIO_PANEL:-panel.shippingbox.duckdns.org}"
REGISTRO="${REGISTRO:-ghcr.io/jakz13/ultimamilla}"
DESDE_REGISTRO="${DESDE_REGISTRO:-}"
export REGISTRO
DESTINO="$VM_USUARIO@$VM_HOST"
SSH_OPC=(-i "$VM_CLAVE_SSH" -o ConnectTimeout=20 -o StrictHostKeyChecking=accept-new)

SERVICIOS=("$@")
[ ${#SERVICIOS[@]} -eq 0 ] && SERVICIOS=(api worker backoffice portal seguimiento panel)

compose=(); imagenes=(); construir=()
for s in "${SERVICIOS[@]}"; do
  case "$s" in
    api)        compose+=(api1);            construir+=(api1);            imagenes+=("$REGISTRO/api:latest") ;;
    worker)     compose+=(worker);          construir+=(worker);          imagenes+=("$REGISTRO/worker:latest") ;;
    backoffice) compose+=(backoffice);      construir+=(backoffice);      imagenes+=("$REGISTRO/backoffice:latest") ;;
    portal)     compose+=(portal-comercio); construir+=(portal-comercio); imagenes+=("$REGISTRO/portal-comercio:latest") ;;
    seguimiento) compose+=(seguimiento-publico); construir+=(seguimiento-publico); imagenes+=("$REGISTRO/seguimiento-publico:latest") ;;
    panel)      compose+=(prometheus grafana) ;;
    *) echo "Servicio desconocido: $s (api|worker|backoffice|portal|seguimiento|panel)"; exit 1 ;;
  esac
done

paso() { printf '\n==> %s\n' "$1"; }

if [ -z "$DESDE_REGISTRO" ] && [ ${#construir[@]} -gt 0 ]; then
  paso "Construyendo imágenes: ${construir[*]}"
  # El compose exige estas variables al interpolarse; para construir sus valores no importan
  POSTGRES_PASSWORD=x RABBITMQ_PASSWORD=x JWT_SECRET=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx \
  DOMINIO=x DOMINIO_COMERCIO=x DOMINIO_PANEL=x DOMINIO_SEGUIMIENTO=x GRAFANA_PASSWORD=x \
    docker compose -f docker-compose.prod.yml build "${construir[@]}"
fi

paso "Enviando archivos de configuración a la VM"
ssh "${SSH_OPC[@]}" "$DESTINO" 'mkdir -p ~/shippingbox/docker'
scp "${SSH_OPC[@]}" docker-compose.prod.yml "$DESTINO:shippingbox/docker-compose.prod.yml"
scp "${SSH_OPC[@]}" docker/Caddyfile.prod "$DESTINO:shippingbox/docker/Caddyfile.prod"
for carpeta in prometheus grafana rabbitmq; do
  scp -r "${SSH_OPC[@]}" "docker/$carpeta" "$DESTINO:shippingbox/docker/"
done

# La VM resuelve los nombres de las imágenes con REGISTRO (.env.prod): se deja registrado el mismo valor con el que se publicaron
ssh "${SSH_OPC[@]}" "$DESTINO" "touch ~/shippingbox/.env.prod; grep -v '^REGISTRO=' ~/shippingbox/.env.prod > ~/shippingbox/.env.tmp; echo 'REGISTRO=$REGISTRO' >> ~/shippingbox/.env.tmp; mv ~/shippingbox/.env.tmp ~/shippingbox/.env.prod; chmod 600 ~/shippingbox/.env.prod"

if [ ${#imagenes[@]} -gt 0 ]; then
  if [ -n "$DESDE_REGISTRO" ]; then
    paso "La VM descarga las imágenes del registro ($REGISTRO)"
    if [ -n "${GHCR_TOKEN:-}" ]; then
      echo "$GHCR_TOKEN" | ssh "${SSH_OPC[@]}" "$DESTINO" "docker login ghcr.io -u '${GHCR_USUARIO:-token}' --password-stdin"
    fi
    ssh "${SSH_OPC[@]}" "$DESTINO" "cd ~/shippingbox && docker compose -f docker-compose.prod.yml --env-file .env.prod pull ${compose[*]}"
  else
    paso "Enviando imágenes a la VM (comprimidas)"
    docker save "${imagenes[@]}" | gzip | ssh -C "${SSH_OPC[@]}" "$DESTINO" 'gunzip | docker load'
  fi
fi

paso "Reiniciando servicios en la VM"
ssh "${SSH_OPC[@]}" "$DESTINO" "bash -s -- '${compose[*]}'" < scripts/reiniciar-remoto.sh

verificar() {
  local url="$1" codigo=0
  paso "Verificando $url"
  for _ in $(seq 1 12); do
    codigo=$(curl -s -o /dev/null -w '%{http_code}' --max-time 15 "$url" || true)
    [ "$codigo" = "200" ] && { echo "  responde 200"; return 0; }
    sleep 5
  done
  echo "ERROR: $url no respondió 200 (último código: $codigo)"; return 1
}
verificar "https://$DOMINIO/ready"
for s in "${SERVICIOS[@]}"; do
  [ "$s" = portal ] && verificar "https://$DOMINIO_COMERCIO/"
  [ "$s" = panel ]  && verificar "https://$DOMINIO_PANEL/api/health"
done
echo; echo "Despliegue terminado."
