# ADR-005: Cambio de requerimientos — entrega parcial de bultos (2.1) y retiro en domicilio (2.2)

**Estado:** aprobado · **Responsables:** Francisco Lema (2.1) y Anthony Curbelo (2.2) · **Requerimientos:** RF 31 a RF 41 del documento de cambios

## 1. Contexto
El 30/10 se incorporaron dos cambios obligatorios para equipos de 4: cada bulto tiene su propio resultado y el estado del envío se deriva de ellos (2.1), y un flujo nuevo de **retiro en domicilio** que va del comprador al comercio y convive con el de envíos (2.2). El cambio debía respetar la máquina de estados declarativa, la configuración versionada, el outbox, la idempotencia, la operación sin conexión y el aislamiento entre inquilinos.

## 2. Decisión
**2.1 Entrega parcial.** Máquina de estados por bulto (`MaquinaEstadosBulto`) y estado `EntregadoParcialmente` en el envío, derivado de sus bultos. La política (`AdmiteEntregaParcial`, `PoliticaEntregaParcial`) vive en la **regla operativa versionada**. El seguimiento público muestra «2 de 3 bultos entregados» sin identificadores internos y los avisos usan el contrato `v2` (ver ADR-004).

**2.2 Retiro como agregado propio, no como un tipo de envío.** Tiene entidad (`Retiro`, `BultoRetiro`, `EventoRetiro`), máquina de estados declarativa (`MaquinaEstadosRetiro`: solicitado, asignado a ruta, retirado, no retirado, en depósito, entregado al comercio, cancelado) y tarifa propia (regla de modalidad `Retiro` en el cuadro vigente).
- **Hoja de ruta mixta:** la parada pasa a tener tipo (entrega o retiro). La validación de capacidad se hace **recorriendo la ruta en orden**, porque los bultos retirados ocupan lugar desde la parada en que se suben (`PerfilCargaRuta`).
- **Conflictos sin conexión:** la política (prevalece lo ocurrido físicamente en el domicilio, con su evidencia) está en una **tabla declarativa** de transiciones por conflicto y deja asentado el `ConflictoSincronizacion`.
- **Misma infraestructura:** el evento sale por el outbox de la unidad de trabajo; el seguimiento público usa el mismo enlace con el prefijo `RET-`; aislamiento por operador (filtro global) y por comercio (el repositorio y los casos de uso filtran por la relación).

## 3. Alternativas consideradas
- **Retiro como un envío con sentido invertido:** descartada; mezcla estados, reglas y tarifas distintos y obliga a condicionales por todo el código.
- **Guardar la información nueva fuera de la base (diccionarios en memoria) para evitar migraciones:** se hizo en la primera versión de 2.1 y se **corrigió**: se perdía al reiniciar y no se compartía entre las dos instancias ni con el worker. Ahora son columnas con migración y una prueba contra Postgres real que lo verifica.

## 4. Qué resultó sencillo, qué no y por qué
- **Sencillo:** el outbox en la unidad de trabajo (un evento nuevo se publica solo), el filtro de inquilino, el versionado de reglas, el patrón de casos de uso y puertos, la máquina de estados declarativa y el cache con clave por operador.
- **Costoso:** (1) la parada estaba acoplada al envío: pasar de `EnvioId` obligatorio a una parada con tipo tocó la descarga móvil, la rendición, el tablero y los reportes (unas 20 zonas que la compilación fue señalando); (2) la capacidad dejó de ser una suma y pasó a ser un perfil de carga a lo largo del recorrido; (3) la sincronización sin conexión necesita una política por tipo de entidad; (4) los reportes y el tablero asumían que toda parada es un envío.
- **Lección:** ocultar propiedades del mapeo para evitar una migración parece un atajo y es una deuda; y todo agregado nuevo necesita desde el inicio su filtro de inquilino, su evento, su política de conflicto y su prueba de aislamiento.

## 5. Consecuencias
- El retiro es un módulo separado y reutilizable; agregar un tercer tipo de parada sería directo.
- La hoja de ruta mixta obliga a que cualquier consulta sobre paradas distinga el tipo (existen ayudas `IdsDeEnvios()` e `IdsDeRetiros()`).
- Falta incorporar el retiro a la facturación (la liquidación por comercio todavía considera solo envíos entregados).
