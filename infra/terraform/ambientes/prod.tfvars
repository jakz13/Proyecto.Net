# Variables del ambiente de producción (no contienen secretos).
# El OCID del compartimento se pasa por variable de entorno, sin escribirlo en el repositorio:
#   $env:TF_VAR_compartimento_ocid = "ocid1.tenancy.oc1..xxxx"
ambiente         = "prod"
region           = "sa-saopaulo-1"
nombre_vm        = "ultimamilla"
forma_vm         = "VM.Standard.E2.1.Micro"
dominio          = "shippingbox.duckdns.org"
dominio_comercio = "shippingbox-comercio.duckdns.org"
dominio_panel    = "panel.shippingbox.duckdns.org"
# La VM actual usa una IP pública efímera (152.70.219.103): no se crea una reservada
crear_ip_reservada = false

# Claves PUBLICAS autorizadas a desplegar (pipeline de CI y companeros). Nunca poner claves privadas aqui.
claves_publicas_adicionales = [
  "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIPTnETXSVATtMEeWZr7ZrFmRro8AhprR+Wd8FkXSFTvw github-actions-despliegue",
]
