# ADR-003: Estrategia de caché distribuido e invalidación

**Estado:** aprobado · **Responsable:** Francisco Lema · **Requerimiento:** 6.7 y aclaración 3.1 del cambio de requerimientos

## 1. Contexto y restricciones
La API corre en **dos instancias** detrás de un balanceador y no puede guardar estado en memoria. Un caché por proceso (`IMemoryCache`) haría que, al modificar una tarifa desde una instancia, la otra siguiera respondiendo con el valor anterior hasta que expirara la entrada. La letra exige caché distribuido sobre al menos dos casos de uso con perfiles de acceso distintos, con expiración e invalidación coherentes con el dominio, entradas que distingan al operador, métrica de aciertos y fallos por caso de uso, y continuidad del servicio si el caché cae.

## 2. Decisión
- **Redis** como almacén compartido, a través de `IDistributedCache` (puerto propio `IDistributedCacheService`, adaptador `DistributedCacheService`). Sin Redis configurado (pruebas y desarrollo mínimo) se usa un caché distribuido en memoria con la misma interfaz.
- **Dos casos de uso con perfiles opuestos:**
  | Caso | Lectura | Cambio | Expiración | Invalidación |
  |---|---|---|---|---|
  | Seguimiento público | muy alta, anónima | varias veces al día | 5 min | al guardar cualquier cambio del envío o del retiro (unidad de trabajo) y al procesar su evento en el worker |
  | Cuadro tarifario vigente | alta | casi nunca | 30 min (versión puntual: 2 h) | al publicar una nueva versión; las versiones publicadas son inmutables |
- **Las claves incluyen al operador** (`tarifas:operador:{id}:vigente`, `seguimiento:operador:{id}:{codigo}`), de modo que el caché no puede filtrar datos entre inquilinos.
- **Degradación segura:** cualquier error del almacén se registra, se cuenta como fallo y la consulta sigue contra la base de datos (conexión a Redis con tiempos de 1 s y sin abortar el arranque).
- **Métrica por caso de uso:** `MedidorCache` publica aciertos, fallos, invalidaciones y errores con la etiqueta del caso de uso en el medidor `UltimaMilla.Cache` (OpenTelemetry → Prometheus → panel de Grafana).
- Redis también sostiene el *backplane* de SignalR, para que el tablero en tiempo real funcione con las dos instancias.

## 3. Alternativas consideradas
- **`IMemoryCache` por instancia:** descartada, es exactamente el problema que la aclaración 3.1 describe y viola «API sin estado en memoria».
- **HybridCache / FusionCache (dos niveles):** descartadas por ahora; exigen resolver cómo se enteran las demás instancias de una invalidación y suman complejidad sin que la carga actual lo justifique. Queda como mejora si el volumen lo pidiera.
- **Sin caché en el seguimiento público:** descartada; es el componente de mayor tráfico y el único expuesto sin autenticación.

## 4. Consecuencias
- Una tarifa modificada por una instancia se ve de inmediato en la otra (el cuadro vigente se invalida en el almacén compartido).
- Con Redis caído el sistema sigue funcionando pero más lento; el panel muestra la relación de aciertos en cero y los errores.
- El caché de seguimiento puede estar desactualizado hasta 5 minutos si fallara la invalidación; se acepta porque el dato público no es crítico y expira solo.
- Todo dato nuevo cacheable debe construir su clave con el operador y declarar su caso de uso en `ClavesCache`.
