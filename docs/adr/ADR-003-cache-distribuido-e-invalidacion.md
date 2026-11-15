# ADR-003: Cómo usamos caché compartido para no repetir datos viejos entre instancias

**Estado:** Aprobado · **Fecha:** 2026-11-05 · **Responsable:** Francisco Lema · **Requerimientos:** 6.7 y aclaración 3.1

## 1. Contexto y problema
En producción nuestra API corre con **dos instancias al mismo tiempo** (`api1` y `api2`) detrás de un balanceador. Si cada instancia guardara el caché en su propia memoria (`IMemoryCache`), cuando un usuario cambia una tarifa en `api1`, `api2` seguiría devolviendo el valor viejo hasta que venza.

Además, la letra pide usar caché distribuido en al menos dos casos de uso distintos, separar los datos por operador, medir aciertos y fallos en el tablero, y seguir funcionando aunque el caché se caiga.

## 2. Decisión
- **Usar Redis como caché compartido:** todas las instancias consultan el mismo Redis mediante una interfaz propia (`IDistributedCacheService`). En pruebas unitarias o desarrollo mínimo sin Redis, se usa una implementación en memoria con la misma interfaz.
- **Dos casos de uso con comportamientos opuestos:**
  - **Seguimiento público:** se consulta muchísimo y cambia varias veces al día. Expira cada **5 minutos** y se borra (invalida) automáticamente cuando se guarda un cambio en el envío/retiro o cuando el Worker procesa el evento.
  - **Cuadro tarifario vigente:** se consulta mucho pero cambia casi nunca. Expira cada **30 minutos** (y una versión puntual guardada por ID dura **2 horas**), y se invalida al publicar una nueva versión.
- **Separación por operador en las claves:** todas las claves incluyen el `OperadorId` (por ejemplo, `tarifas:operador:{id}:vigente` y `seguimiento:operador:{id}:{codigo}`) para que nunca se mezclen datos entre operadores.
- **Si Redis se cae, el sistema no se cae:** los timeouts son cortos (1 s), los errores se registran como fallos y la API consulta directamente a PostgreSQL.
- **Métricas y SignalR:** `MedidorCache` publica aciertos, fallos, invalidaciones y errores hacia Prometheus/Grafana, y Redis también sirve de respaldo (*backplane*) para que SignalR funcione entre `api1` y `api2`.

## 3. Alternativas consideradas
- **Caché en memoria local (`IMemoryCache`):** descartada, porque desincroniza las dos instancias de la API y rompe el requisito de API sin estado en memoria.
- **Caché híbrido en dos niveles (memoria + Redis):** descartada por ahora porque agrega complejidad para avisar a la otra instancia cuándo invalidar, sin que el tráfico actual lo justifique.
- **No cachear el seguimiento público:** descartada, porque es la pantalla pública con más visitas de todo el sistema.

## 4. Consecuencias
- **Ventajas:** lo que se actualiza en una instancia se refleja en la otra, y ante una caída de Redis el sistema sigue atendiendo (un poco más lento) sin devolver error 500.
- **Desventajas y cómo se mitigan:** si fallara una invalidación en el seguimiento público, el dato podría verse desactualizado como máximo 5 minutos; es un margen aceptable porque vence solo y no compromete datos críticos.
