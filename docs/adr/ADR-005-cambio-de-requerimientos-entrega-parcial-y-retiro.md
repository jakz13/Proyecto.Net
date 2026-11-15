# ADR-005: Cómo incorporamos la entrega parcial por bulto y el retiro en domicilio

**Estado:** Aprobado · **Fecha:** 2026-11-09 · **Responsables:** Francisco Lema (2.1) y Anthony Curbelo (2.2) · **Requerimientos:** RF 31 a RF 41

## 1. Contexto y problema
El 30/10 se sumaron dos cambios de alcance obligatorios para equipos de 4 personas:
- **Entrega parcial (2.1):** cada bulto de un envío puede tener un resultado distinto (ej. se entregan 2 de 3) y el estado del envío se calcula según sus bultos.
- **Retiro en domicilio (2.2):** un flujo nuevo donde el repartidor retira paquetes en la casa de un comprador para llevarlos al comercio.

El desafío era sumar ambos cambios sin romper la máquina de estados, el versionado de reglas, el Outbox, el modo sin conexión del celular ni el aislamiento entre operadores y comercios.

## 2. Decisión
- **Para entrega parcial (2.1):**
  - Se creó una máquina de estados propia por bulto (`MaquinaEstadosBulto`) y el estado `EntregadoParcialmente` en `Envio`, derivado automáticamente de sus bultos.
  - La configuración de si se admite entrega parcial y qué hacer con los bultos restantes (`AdmiteEntregaParcial`, `PoliticaEntregaParcial`) se guardó en `ReglaOperativa` versionada.
  - En el seguimiento público se muestra el progreso (ej. *"2 de 3 bultos entregados"*) sin exponer IDs internos, y los avisos a comercios salen con el contrato `v2`.
- **Para retiro en domicilio (2.2):**
  - Se modeló como un agregado propio (`Retiro`, `BultoRetiro`, `EventoRetiro`) con su propia máquina de estados (`MaquinaEstadosRetiro`) y tarifa propia de modalidad `Retiro`, en lugar de forzarlo dentro de `Envio`.
  - **Hojas de ruta mixtas:** una parada puede ser de entrega o de retiro, y el control de capacidad del vehículo (`PerfilCargaRuta`) recorre la ruta en orden porque los bultos retirados van ocupando lugar a medida que se suben.
  - **Offline, Outbox y multitenancy:** reutiliza el mismo Outbox, el seguimiento público (con prefijo `RET-`), los filtros por operador/comercio y tiene su propia tabla de resolución de conflictos donde prevalece lo ocurrido en el domicilio.

## 3. Alternativas consideradas
- **Modelar el retiro como un `Envio` "al revés":** descartada, porque tienen estados, reglas y tarifas distintas y habría llenado el código de `if/else` difíciles de mantener.
- **Guardar datos nuevos en memoria para evitar migraciones de base de datos:** se probó al principio de 2.1 pero se descartó y corrigió, porque los datos se perdían al reiniciar y no se compartían entre `api1`, `api2` y el Worker. Todo pasó a columnas reales con migración de EF Core.

## 4. Consecuencias
- **Ventajas:** el código de retiros quedó limpio y separado; si mañana apareciera un tercer tipo de parada, la estructura ya lo soporta.
- **Desventajas y cómo se mitigan:** todas las consultas de rutas y reportes tuvieron que adaptarse para distinguir paradas de entrega vs. paradas de retiro. Además, queda como pendiente sumar los retiros dentro del cálculo de liquidación/facturación por comercio (que hoy solo cuenta envíos entregados).
