locals {
  prefijo = "${var.ambiente}-${var.nombre_vm}"
}

resource "oci_core_vcn" "red" {
  compartment_id = var.compartimento_ocid
  display_name   = "${local.prefijo}-vcn"
  cidr_blocks    = [var.cidr_vcn]
  dns_label      = substr(replace("${var.ambiente}${var.nombre_vm}", "-", ""), 0, 15)
}

resource "oci_core_internet_gateway" "salida" {
  compartment_id = var.compartimento_ocid
  vcn_id         = oci_core_vcn.red.id
  display_name   = "${local.prefijo}-igw"
  enabled        = true
}

resource "oci_core_route_table" "publica" {
  compartment_id = var.compartimento_ocid
  vcn_id         = oci_core_vcn.red.id
  display_name   = "${local.prefijo}-rutas"

  route_rules {
    destination       = "0.0.0.0/0"
    destination_type  = "CIDR_BLOCK"
    network_entity_id = oci_core_internet_gateway.salida.id
  }
}

# Solo se abren SSH, HTTP y HTTPS. Postgres, Redis, RabbitMQ y Prometheus no se publican:
# viven en la red interna de Docker y solo Caddy expone sitios (Grafana incluido, por HTTPS).
resource "oci_core_security_list" "web" {
  compartment_id = var.compartimento_ocid
  vcn_id         = oci_core_vcn.red.id
  display_name   = "${local.prefijo}-seguridad"

  egress_security_rules {
    destination = "0.0.0.0/0"
    protocol    = "all"
  }

  ingress_security_rules {
    protocol    = "6" # TCP
    source      = var.cidr_ssh_permitido
    description = "SSH"
    tcp_options {
      min = 22
      max = 22
    }
  }

  ingress_security_rules {
    protocol    = "6"
    source      = "0.0.0.0/0"
    description = "HTTP (desafio de Lets Encrypt y redireccion a HTTPS)"
    tcp_options {
      min = 80
      max = 80
    }
  }

  ingress_security_rules {
    protocol    = "6"
    source      = "0.0.0.0/0"
    description = "HTTPS"
    tcp_options {
      min = 443
      max = 443
    }
  }

  ingress_security_rules {
    protocol    = "17" # UDP
    source      = "0.0.0.0/0"
    description = "HTTP/3"
    udp_options {
      min = 443
      max = 443
    }
  }

  ingress_security_rules {
    protocol    = "1" # ICMP
    source      = "0.0.0.0/0"
    description = "Descubrimiento de MTU"
    icmp_options {
      type = 3
      code = 4
    }
  }
}

resource "oci_core_subnet" "publica" {
  compartment_id    = var.compartimento_ocid
  vcn_id            = oci_core_vcn.red.id
  display_name      = "${local.prefijo}-subred"
  cidr_block        = var.cidr_subred
  route_table_id    = oci_core_route_table.publica.id
  security_list_ids = [oci_core_security_list.web.id]
  dns_label         = "publica"
}
