#!/bin/sh
# Se ejecuta al arrancar el contenedor: el cliente WASM lee su configuración de /appsettings.json.
set -e
: "${API_BASE_URL:?definir API_BASE_URL (por ejemplo https://seguimiento.ejemplo.org)}"
cat > /usr/share/nginx/html/appsettings.json <<EOF
{ "ApiSettings": { "BaseUrl": "${API_BASE_URL}" }, "UsarMocks": false }
EOF
# En producción no debe existir la configuración de desarrollo
rm -f /usr/share/nginx/html/appsettings.Development.json
