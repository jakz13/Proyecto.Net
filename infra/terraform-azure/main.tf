locals {
  nombre = "ultimamilla-${var.ambiente}"
  etiquetas = {
    proyecto = "ultima-milla"
    ambiente = var.ambiente
    gestion  = "terraform"
  }
}

resource "azurerm_resource_group" "rg" {
  name     = "rg-${local.nombre}"
  location = var.region
  tags     = local.etiquetas
}

resource "azurerm_virtual_network" "red" {
  name                = "vnet-${local.nombre}"
  address_space       = [var.cidr_red]
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  tags                = local.etiquetas
}

resource "azurerm_subnet" "subred" {
  name                 = "snet-${local.nombre}"
  resource_group_name  = azurerm_resource_group.rg.name
  virtual_network_name = azurerm_virtual_network.red.name
  address_prefixes     = [var.cidr_subred]
}

# Solo SSH, HTTP y HTTPS (TCP y UDP para HTTP/3). La base, la cola, Redis y Prometheus no se publican.
resource "azurerm_network_security_group" "nsg" {
  name                = "nsg-${local.nombre}"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  tags                = local.etiquetas

  security_rule {
    name                       = "ssh"
    priority                   = 100
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "22"
    source_address_prefix      = var.cidr_ssh_permitido
    destination_address_prefix = "*"
  }

  security_rule {
    name                       = "http"
    priority                   = 110
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "80"
    source_address_prefix      = "*"
    destination_address_prefix = "*"
  }

  security_rule {
    name                       = "https-tcp"
    priority                   = 120
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "443"
    source_address_prefix      = "*"
    destination_address_prefix = "*"
  }

  security_rule {
    name                       = "https-udp-http3"
    priority                   = 130
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Udp"
    source_port_range          = "*"
    destination_port_range     = "443"
    source_address_prefix      = "*"
    destination_address_prefix = "*"
  }
}

# IP pública estática: los tres dominios de DuckDNS apuntan a ella y no cambia al reiniciar la VM.
resource "azurerm_public_ip" "ip" {
  name                = "pip-${local.nombre}"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  allocation_method   = "Static"
  sku                 = "Standard"
  tags                = local.etiquetas
}

resource "azurerm_network_interface" "nic" {
  name                = "nic-${local.nombre}"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  tags                = local.etiquetas

  ip_configuration {
    name                          = "principal"
    subnet_id                     = azurerm_subnet.subred.id
    private_ip_address_allocation = "Dynamic"
    public_ip_address_id          = azurerm_public_ip.ip.id
  }
}

resource "azurerm_network_interface_security_group_association" "nic_nsg" {
  network_interface_id      = azurerm_network_interface.nic.id
  network_security_group_id = azurerm_network_security_group.nsg.id
}

resource "azurerm_linux_virtual_machine" "vm" {
  name                  = "vm-${local.nombre}"
  location              = azurerm_resource_group.rg.location
  resource_group_name   = azurerm_resource_group.rg.name
  size                  = var.tamano_vm
  admin_username        = var.usuario_admin
  network_interface_ids = [azurerm_network_interface.nic.id]
  tags                  = local.etiquetas

  # Acceso solo por clave SSH: sin contraseña
  disable_password_authentication = true

  dynamic "admin_ssh_key" {
    for_each = toset(var.claves_publicas_ssh)
    content {
      username   = var.usuario_admin
      public_key = admin_ssh_key.value
    }
  }

  os_disk {
    caching              = "ReadWrite"
    storage_account_type = "StandardSSD_LRS"
    disk_size_gb         = var.disco_gb
  }

  source_image_reference {
    publisher = "Canonical"
    offer     = "ubuntu-24_04-lts"
    sku       = "server"
    version   = "latest"
  }

  # Docker, swap y la carpeta de la aplicación con los secretos generados AL AZAR en la propia VM (ver cloud-init)
  custom_data = base64encode(templatefile("${path.module}/cloud-init.yaml.tftpl", {
    usuario             = var.usuario_admin
    tamano_swap_gb      = var.tamano_swap_gb
    dominio             = var.dominio
    dominio_comercio    = var.dominio_comercio
    dominio_panel       = var.dominio_panel
    dominio_seguimiento = var.dominio_seguimiento
  }))
}
