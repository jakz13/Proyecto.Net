# Autenticación con el token de sesión del navegador (oci session authenticate):
# no hay claves ni secretos en el código. El perfil y la región son variables.
provider "oci" {
  auth                = "SecurityToken"
  config_file_profile = var.perfil_oci
  region              = var.region
}
