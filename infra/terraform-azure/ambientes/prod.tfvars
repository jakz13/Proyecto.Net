# Variables del ambiente de producción (no contienen secretos).
# El id de la suscripción y las claves públicas SSH se pasan por variables de entorno, no se guardan aquí:
#   $env:TF_VAR_subscription_id     = "<id de la suscripción>"
#   $env:TF_VAR_claves_publicas_ssh = (ConvertTo-Json @((Get-Content ~/.ssh/azure_ultimamilla.pub), (Get-Content ~/.ssh/ci_deploy.pub)))
ambiente  = "prod"
region    = "chilecentral"
tamano_vm = "Standard_B2als_v2"
disco_gb  = 64

dominio          = "shippingbox.duckdns.org"
dominio_comercio = "shippingbox-comercio.duckdns.org"
dominio_panel    = "panel.shippingbox.duckdns.org"
