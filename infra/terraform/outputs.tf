locals {
  ip_publica = var.crear_ip_reservada ? oci_core_public_ip.reservada[0].ip_address : data.oci_core_vnic.vm.public_ip_address
}

output "ip_publica" {
  description = "IP pública de la VM: es la que deben apuntar los tres dominios de DuckDNS"
  value       = local.ip_publica
}

output "ssh" {
  description = "Comando para conectarse"
  value       = "ssh -i ~/.ssh/oracle_ultimamilla ubuntu@${local.ip_publica}"
}

output "dominios" {
  value = [var.dominio, var.dominio_comercio, var.dominio_panel]
}
