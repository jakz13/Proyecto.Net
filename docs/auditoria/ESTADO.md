# Estado de la auditoría nocturna (leer esto primero)

> Se actualiza al cerrar cada ronda. Detalle completo en [INFORME.md](INFORME.md); línea de tiempo en `diario.log`.

- **Última actualización:** 2026-11-14 02:20
- **Estado:** EN CURSO (ronda 7 próxima: F1, F4, F6 y luego F2/F3)
- **Pruebas ejecutadas:** más de 3.500 casos en 6 rondas (autorización 1.192; datos malformados 1.229 + 553 + 77; escenario F5 completo desde la pantalla real; secretos, dependencias, límite de tasa)
- **Hallazgos:** 9 (D-01 a D-09) — **todos corregidos, con prueba que fallaba antes, publicados y verificados en producción**
- **Suite del backend:** 1.209 pruebas, 0 fallas (integración repetida 12 veces sin fallas)
- **Último commit publicado:** `d8baa63` (línea base previa a la auditoría: `d65d984`)

## Hallazgos de mayor impacto para la defensa
| # | Gravedad | Qué pasaba | Estado |
|---|---|---|---|
| D-06 | crítica (F5) | el formulario de tarifas renombraba todas las reglas y las altas fallaban | corregido y verificado |
| D-07 | alta (F5) | no se podía publicar la tarifa vigente (volumen sin límite) y no mostraba el error | corregido y verificado |
| D-08 | crítica (F5) | publicar una regla operativa cambiaba en silencio la prueba de entrega y la devolución | corregido y verificado |
| D-01 | alta | el límite de tasa era un cupo único compartido por todos los visitantes | corregido y verificado |
| D-02 a D-05 | media | elementos nulos en listas → HTTP 500; `{}` guardaba datos por defecto | corregidos y verificados |

## Estado de producción ahora
- Tarifa vigente: Estandar 150, Urgente 300, Retiro 120 (versión 7). Regla operativa vigente: 3 intentos, prueba "Firma" (versión 6).
- Las versiones intermedias del cuadro tarifario (2 a 6) y de las reglas operativas (2 a 5) son del propio ensayo y quedan en el historial.
- Hay envíos de prueba con nombre `AUDIT-…` y una posición GPS de prueba para limpiar al final.
- **Antes del jueves:** encender `RECEPTOR_PRUEBA_AVISOS=true` en la VM (escenario 6) y regenerar la clave de API del comercio si la demo de la API pública usa una guardada.

## Cómo saber si sigo trabajando
1. La hora de la última línea de `docs/auditoria/diario.log`.
2. La fecha de modificación de `C:\Users\Curbe\.claude\projects\c--Users-Curbe-OneDrive-Escritorio-Proyecto-Proyecto-Net\70ebc7ed-eac9-44d4-99b2-f66f2414612d.jsonl`.
3. `C:\Users\Curbe\auditoria_local\latido.txt` (anti-suspensión, cada 5 min) y `reanudar.log` (script de reanudación).

## Pendiente (próximas rondas)
F1 caída de una API con tráfico; F4 dos operadores; F6 receptor de avisos caído y recuperado; F2/F3 móvil sin conexión y conflicto de sincronización;
concurrencia e idempotencia; costos de Azure; limpieza de datos `AUDIT-`; F7 destruir y reconstruir (al final).
