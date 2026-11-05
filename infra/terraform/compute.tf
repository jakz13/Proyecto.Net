data "oci_identity_availability_domains" "dominios" {
  compartment_id = var.compartimento_ocid
}

# Imagen Ubuntu 22.04 más reciente compatible con la forma elegida
data "oci_core_images" "ubuntu" {
  compartment_id           = var.compartimento_ocid
  operating_system         = "Canonical Ubuntu"
  operating_system_version = "22.04"
  shape                    = var.forma_vm
  sort_by                  = "TIMECREATED"
  sort_order               = "DESC"
}

resource "oci_core_instance" "servidor" {
  compartment_id      = var.compartimento_ocid
  availability_domain = data.oci_identity_availability_domains.dominios.availability_domains[0].name
  display_name        = var.nombre_vm
  shape               = var.forma_vm

  source_details {
    source_type = "image"
    source_id   = data.oci_core_images.ubuntu.images[0].id
  }

  create_vnic_details {
    subnet_id        = oci_core_subnet.publica.id
    assign_public_ip = !var.crear_ip_reservada # con IP reservada se asocia aparte (más abajo)
    hostname_label   = var.nombre_vm
  }

  metadata = {
    ssh_authorized_keys = join("\n", concat([trimspace(file(pathexpand(var.ruta_clave_publica_ssh)))], var.claves_publicas_adicionales))
    user_data = base64encode(templatefile("${path.module}/cloud-init.yaml.tftpl", {
      tamano_swap_gb   = var.tamano_swap_gb
      dominio          = var.dominio
      dominio_comercio = var.dominio_comercio
      dominio_panel    = var.dominio_panel
    }))
  }

  lifecycle {
    # Cambiar la imagen, el script de arranque o la configuración de la VNIC no debe recrear la VM (ni perder sus datos)
    ignore_changes = [source_details[0].source_id, metadata["user_data"], create_vnic_details]
  }
}

# IP pública reservada: gratuita mientras esté asociada. Evita actualizar DuckDNS
# cada vez que se detiene y se vuelve a iniciar la VM.
data "oci_core_vnic_attachments" "vm" {
  compartment_id = var.compartimento_ocid
  instance_id    = oci_core_instance.servidor.id
}

data "oci_core_private_ips" "vm" {
  vnic_id = data.oci_core_vnic_attachments.vm.vnic_attachments[0].vnic_id
}

data "oci_core_vnic" "vm" {
  vnic_id = data.oci_core_vnic_attachments.vm.vnic_attachments[0].vnic_id
}

resource "oci_core_public_ip" "reservada" {
  count          = var.crear_ip_reservada ? 1 : 0
  compartment_id = var.compartimento_ocid
  display_name   = "${local.prefijo}-ip"
  lifetime       = "RESERVED"
  private_ip_id  = data.oci_core_private_ips.vm.private_ips[0].id
}
