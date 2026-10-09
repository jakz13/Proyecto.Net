# Clases del ModeloDominio (versión actual del diagrama)

Extraído del archivo `NETProyecto2026_drawio.xml`, página **ModeloDominio**. Total: 50 clases (incluye 1 enumerado).

Convenciones: el primer atributo de cada clase es su clave primaria (`xxxId`), salvo las excepciones marcadas abajo; los atributos que terminan en `Id` y no son el primero son claves foráneas; `selloModificacion` es el token de concurrencia optimista (RNF 6.4).


## 1. Identidad y acceso

### Usuario
- usuarioId
- nombre
- email
- passwordHash
- operadorId
- comercioId
- perfilId
- activo
- fechaAlta

### Perfil
- perfild
- descripcion
- nombre

### Permiso
- permisoId
- descripcion
- codigo

### PerfilPermiso
- perfilId
- permisoId


## 2. Operador y configuración

### Operador
- operadorId
- razonSocial
- nombreComercial
- estado
- fechaAlta

### IdentidadVisual
- operadorId
- logoUrl
- colorPrimario
- colorSecundario
- emailContacto
- telefonoContacto

### ZonaCobertura
- zonaCoberturaId
- operadorId
- nombre
- poligonoGeografico
- activa

### FranjaHoraria
- franjaHorariaId
- zonaCoberturaId
- diaSemana
- horaInicio
- horaFin
- activa

### CuadroTarifario
- cuadroTarifarioId
- operadorId
- version
- vigenteDesde
- vigenteHasta

### ReglaTarifa
- reglaTarifaId
- cuadroTarifarioId
- zonaCoberturaId
- modalidadServicio
- pesoDesde
- pesoHasta
- volumenDesde
- volumenHasta
- precioBase
- recargo
- bonificacion

### ReglaOperativa
- reglaOperativaId
- operadorId
- version
- vigenteDesde
- vigenteHasta
- maxIntentosEntrega
- plazoEntreIntentos
- tipoPruebaEntregaExigida
- politicaDevolucion
- plazoComprometidoEstandar
- plazoComprometidoUrgente

### MotivoNoEntrega
- motivoNoEntregaId
- operadorId
- codigo
- descripcion
- activo

### Vehiculo
- vehiculoId
- operadorId
- patente
- tipo
- capacidadPeso
- capacidadVolumen
- activo

### Repartidor
- repartidorId
- usuarioId
- operadorId
- vehiculoAsignadoId
- documento
- telefono
- estado


## 3. Comercio

### Comercio
- comercioId
- razonSocial
- documento
- email
- telefono

### ComercioOperador
- comercioOperadorId
- comercioId
- operadorId
- fechaAlta
- estado
- claveApi

### CuentaCorriente
- cuentaCorrienteId
- comercioOperadorId
- saldo
- moneda
- selloModificacion

### MovimientoCuentaCorriente
- movimientoCuentaCorrienteId
- cuentaCorrienteId
- fecha
- concepto
- monto
- envioId
- liquidacionId

### Liquidacion
- liquidacionId
- comercioOperadorId
- periodoDesde
- periodoHasta
- montoTotal
- estado
- fechaGeneracion


## 4. Envío y bultos

### Direccion
- direccionId
- calle
- numero
- ciudad
- zonaCoberturaId
- latitud
- longitud
- referencia

### Destinatario
- destinatarioId
- nombre
- documento
- email
- telefono

### Envio
- envioId
- comercioOperadorId
- destinatarioId
- direccionEntregaId
- modalidadServicio
- franjaHorariaComprometidaId
- estadoActual
- tarifaCalculada
- reglaTarifaAplicadaId
- codigoSeguimientoPublico
- loteImportacionId
- fechaAlta
- selloModificacion
- hojaDeRutaId

### LoteImportacion
- loteImportacionId
- comercioOperadorId
- claveIdempotencia
- fechaProcesamiento
- cantidadRegistros
- cantidadExitosos
- cantidadConError

### Bulto
- bultoId
- envioId
- identificacion
- peso
- alto
- ancho
- profundo
- estadoBulto

### EstadoEnvio *(enumerado)*
- Admitido
- EnDeposito
- AsignadoARuta
- EnTransito
- Entregado
- NoEntregado
- Reprogramado
- EnDevolucion
- Devuelto
- Extraviado

### TransicionEstadoPermitida
- transicionEstadoPermitidaId
- estadoOrigen
- estadoDestino
- condicion

### EventoEnvio
- eventoEnvioId
- envioId
- tipoEvento
- estadoAnterior
- estadoNuevo
- timestamp
- origen
- responsableUsuarioId
- latitud
- longitud
- payload

### DiscrepanciaRecepcion
- discrepanciaRecepcionId
- envioId
- bultoId
- tipoDiscrepancia
- detalle
- fechaDeteccion
- usuarioDeposito


## 5. Planificación y despacho

### HojaDeRuta
- hojaDeRutaId
- operadorId
- repartidorId
- vehiculoId
- fecha
- estado
- fechaDespacho
- selloModificacion

### Parada
- paradaId
- hojaDeRutaId
- envioId
- orden
- direccionId
- franjaHorariaId
- estado

### AsignacionEnvioRuta
- asignacionEnvioRutaId
- envioId
- hojaDeRutaId
- fechaAsignacion
- despachadorUsuarioId
- vigente
- selloModificacion


## 6. Ejecución en la calle

### EscaneoCargaVehiculo
- escaneoCargaVehiculoId
- hojaDeRutaId
- bultoId
- resultado
- timestamp

### IntentoEntrega
- intentoEntregaId
- paradaId
- envioId
- resultado
- motivoNoEntregaId
- timestamp
- latitud
- longitud
- origenDatos

### PruebaDeEntrega
- intentoEntregaId
- tipo
- firmaImagenUrl
- fotoUrl
- nombreReceptor
- documentoReceptor
- latitud
- longitud
- timestampDispositivo

### Devolucion
- devolucionId
- envioId
- motivoDevolucionId
- estado
- fechaInicio
- fechaRendicion

### RendicionDeposito
- rendicionDepositoId
- hojaDeRutaId
- repartidorId
- fechaHora
- bultosEntregados
- bultosDevueltos
- bultosPendientes
- observaciones

### PosicionVehiculo
- posicionVehiculoId
- vehiculoId
- hojaDeRutaId
- latitud
- longitud
- timestamp
- origen

### OperacionOfflinePendiente
- tipoOperacion
- payload
- timestampCaptura
- sincronizada
- intentosSincronizacion

### ConflictoSincronizacion
- conflictoSincronizacionId
- entidadAfectada
- entidadId
- estadoServidor
- estadoDispositivo
- politicaAplicada
- resolucion
- timestamp

### DispositivoPush
- dispositivoPushId
- usuarioId
- token
- plataforma
- activo
- fechaRegistro


## 7. Seguimiento y avisos

### SolicitudReprogramacion
- solicitudReprogramacionId
- envioId
- franjaHorariaSolicitada
- estado
- fechaSolicitud
- origenIp

### Notificacion
- notificacionId
- envioId
- canal
- tipoEvento
- estadoEnvio
- timestamp

### SuscripcionAviso
- suscripcionAvisoId
- comercioOperadorId
- tipoEvento
- urlWebhook
- claveFirma
- activa

### AvisoComercio
- avisoComercioId
- suscripcionAvisoId
- envioId
- payload
- firmaEnviada
- estado
- intentos
- proximoReintento
- ultimaRespuestaHttp
- timestamp


## 8. Mensajería e integración

### OutboxMessage
- agregadoOrigen
- agregadoId
- tipoEvento
- payload
- creadoEn
- procesado
- procesadoEn

### MensajeFallidoCola
- tipoMensaje
- payloadOriginal
- excepcion
- intentos
- primerFallo
- ultimoFallo
- estado

### ClaveIdempotenciaConsumidor
- mensajeId
- procesadoEn
- resultado


## 9. Incidencias e indicadores

### Incidencia
- incidenciaId
- operadorId
- envioId
- tipo
- descripcion
- estado
- responsableUsuarioId
- fechaApertura
- fechaCierre

### IndicadorCumplimiento
- indicadorCumplimientoId
- operadorId
- zonaCoberturaId
- repartidorId
- comercioOperadorId
- periodo
- totalEnvios
- entregadosEnPlazo
- entregadosFueraDePlazo
- noEntregados
- porcentajeCumplimiento

### CierreDiarioOperacion
- cierreDiarioOperacionId
- operadorId
- fecha
- resumenJson
- ejecutadoEn


---

## Detalles a corregir en el diagrama (detectados al extraer)

- **Perfil**: el atributo `perfild` tiene un typo, debería ser `perfilId`.
- **Sin id propio** (se les escapó al agregar claves primarias): `OperacionOfflinePendiente`, `OutboxMessage` y `MensajeFallidoCola`.
- **EstadoEnvio** es un enumerado, por eso sus "atributos" son los valores posibles y no lleva id.
- **PerfilPermiso** usa clave compuesta (`perfilId` + `permisoId`), por eso no tiene un id propio.
