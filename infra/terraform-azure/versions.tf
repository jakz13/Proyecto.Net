terraform {
  required_version = ">= 1.5"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }
}

# Autenticación: la sesión de Azure CLI (az login). No hay credenciales en el código.
provider "azurerm" {
  features {}
  subscription_id = var.subscription_id
}
