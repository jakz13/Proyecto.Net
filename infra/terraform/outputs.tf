output "ip_publica" {
  description = "IP pública de la VM: es la que deben apuntar los tres dominios de DuckDNS"
  value       = oci_core_public_ip.reservada.ip_address
}

output "ssh" {
  description = "Comando para conectarse"
  value       = "ssh -i ~/.ssh/oracle_ultimamilla ubuntu@${oci_core_public_ip.reservada.ip_address}"
}

output "dominios" {
  value = [var.dominio, var.dominio_comercio, var.dominio_panel]
}
