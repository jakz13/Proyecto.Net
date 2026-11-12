variable "subscription_id" {
  description = "Id de la suscripción de Azure (se pasa por TF_VAR_subscription_id; no se guarda en el repositorio)."
  type        = string
}

variable "ambiente" {
  description = "Nombre del ambiente (prod, demo...). Forma parte del nombre de todos los recursos."
  type        = string
  default     = "prod"
}

variable "region" {
  description = "Región de Azure. La suscripción de estudiante solo permite algunas regiones."
  type        = string
  default     = "chilecentral"
}

variable "tamano_vm" {
  description = "Tamaño de la máquina virtual (2 vCPU / 4 GB alcanzan para los ~12 contenedores)."
  type        = string
  default     = "Standard_B2als_v2"
}

variable "disco_gb" {
  description = "Tamaño del disco del sistema operativo."
  type        = number
  default     = 64
}

variable "usuario_admin" {
  description = "Usuario administrador de la VM (acceso solo por clave SSH)."
  type        = string
  default     = "azureuser"
}

variable "claves_publicas_ssh" {
  description = "Claves PÚBLICAS autorizadas (la de quien administra y la del pipeline de CI). Nunca claves privadas."
  type        = list(string)
}

variable "cidr_ssh_permitido" {
  description = "Origen permitido para SSH. Para producción conviene limitarlo a la IP de quien despliega."
  type        = string
  default     = "*"
}

variable "tamano_swap_gb" {
  description = "Swap de la VM como colchón ante picos de memoria."
  type        = number
  default     = 2
}

variable "dominio" {
  description = "Dominio del Backoffice y la API."
  type        = string
  default     = "shippingbox.duckdns.org"
}

variable "dominio_comercio" {
  description = "Dominio del portal del comercio."
  type        = string
  default     = "shippingbox-comercio.duckdns.org"
}

variable "dominio_panel" {
  description = "Dominio del panel (Grafana y seguimiento público)."
  type        = string
  default     = "panel.shippingbox.duckdns.org"
}

variable "cidr_red" {
  description = "Rango de la red virtual."
  type        = string
  default     = "10.10.0.0/16"
}

variable "cidr_subred" {
  description = "Rango de la subred de la VM."
  type        = string
  default     = "10.10.1.0/24"
}
