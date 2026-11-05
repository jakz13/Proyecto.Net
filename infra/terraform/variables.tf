variable "ambiente" {
  description = "Nombre del ambiente (prod, demo...). Se usa como prefijo de los recursos."
  type        = string
  default     = "prod"
}

variable "region" {
  description = "Región de Oracle Cloud"
  type        = string
  default     = "sa-saopaulo-1"
}

variable "perfil_oci" {
  description = "Perfil de ~/.oci/config con la sesión iniciada"
  type        = string
  default     = "DEFAULT"
}

variable "compartimento_ocid" {
  description = "OCID del compartimento (en una cuenta sin compartimentos, es el OCID de la tenancy)"
  type        = string
}

variable "nombre_vm" {
  description = "Nombre de la máquina virtual"
  type        = string
  default     = "ultimamilla"
}

variable "forma_vm" {
  description = "Forma (shape) de la VM. VM.Standard.E2.1.Micro es Always Free (x86, 1 OCPU, 1 GB)"
  type        = string
  default     = "VM.Standard.E2.1.Micro"
}

variable "ruta_clave_publica_ssh" {
  description = "Ruta al archivo de la clave pública SSH autorizada en la VM"
  type        = string
  default     = "~/.ssh/oracle_ultimamilla.pub"
}

variable "claves_publicas_adicionales" {
  description = "Claves públicas SSH de los compañeros y del pipeline de CI que pueden desplegar (solo claves públicas, nunca privadas)"
  type        = list(string)
  default     = []
}

variable "cidr_ssh_permitido" {
  description = "Rango de IP autorizado a conectarse por SSH. Restringirlo a la IP propia en cuanto sea posible."
  type        = string
  default     = "0.0.0.0/0"
}

variable "tamano_swap_gb" {
  description = "Tamaño del archivo de swap (la VM de 1 GB necesita swap para correr todos los servicios)"
  type        = number
  default     = 4
}

variable "dominio" {
  description = "Dominio del backoffice y la API"
  type        = string
  default     = "shippingbox.duckdns.org"
}

variable "dominio_comercio" {
  description = "Dominio del portal del comercio"
  type        = string
  default     = "shippingbox-comercio.duckdns.org"
}

variable "dominio_panel" {
  description = "Dominio del panel de observabilidad (Grafana)"
  type        = string
  default     = "panel.shippingbox.duckdns.org"
}

variable "cidr_vcn" {
  description = "Rango de la red virtual"
  type        = string
  default     = "10.0.0.0/16"
}

variable "cidr_subred" {
  description = "Rango de la subred pública"
  type        = string
  default     = "10.0.0.0/24"
}

variable "crear_ip_reservada" {
  description = "Crear una IP pública reservada (estable ante reinicios). En un ambiente ya creado con IP efímera, dejar en false."
  type        = bool
  default     = true
}
