output "ip_publica" {
  description = "IP pública estática: apuntar a ella los tres dominios de DuckDNS."
  value       = azurerm_public_ip.ip.ip_address
}

output "ssh" {
  description = "Comando para entrar a la VM."
  value       = "ssh ${var.usuario_admin}@${azurerm_public_ip.ip.ip_address}"
}

output "grupo_de_recursos" {
  description = "Todo el ambiente vive en este grupo: borrarlo (o terraform destroy) lo elimina por completo."
  value       = azurerm_resource_group.rg.name
}
