# Estado de la auditoría nocturna (leer esto primero)

> Se actualiza al cerrar cada ronda. Detalle completo en [INFORME.md](INFORME.md); línea de tiempo en `diario.log`.

- **Última actualización:** 2026-11-14 02:45
- **Estado:** EN CURSO (hechos F1, F4, F5 y F6; próximos F2/F3 móvil, concurrencia, costos y F7)
- **Escenarios de la defensa verificados en producción:** F1 (caída de api1 y api2 con tráfico, 811 pedidos sin errores), F4 (dos operadores, aislamiento y cambio en caliente aislado), F5 (tarifa y regla operativa desde la pantalla real), F6 (receptor caído, eventos, reactivación y recuperación sin duplicados)
- **Pruebas ejecutadas:** más de 4.500 casos en 9 rondas
- **Hallazgos:** 11 (D-01 a D-11) — **todos corregidos, con prueba que fallaba antes, publicados y verificados en producción**
- **Suite del backend:** 1.213 pruebas, 0 fallas
- **Último commit publicado:** `a671eb8` (línea base previa a la auditoría: `d65d984`)

## Hallazgos de mayor impacto para la defensa
| # | Gravedad | Qué pasaba | Estado |
|---|---|---|---|
| D-06 | crítica (F5) | el formulario de tarifas renombraba todas las reglas y las altas fallaban | corregido y verificado |
| D-07 | alta (F5) | no se podía publicar la tarifa vigente (volumen sin límite) y no mostraba el error | corregido y verificado |
| D-08 | crítica (F5) | publicar una regla operativa cambiaba en silencio la prueba de entrega y la devolución | corregido y verificado |
| D-10 | alta (F6) | suscribirse a "Admisión de Envío" (el evento que provoca el propio comercio) nunca generaba avisos | corregido y verificado |
| D-11 | alta (F6) | el worker no tenía la excepción anti-SSRF: el receptor de prueba era inalcanzable | corregido y verificado |
| D-01 | alta | el límite de tasa era un cupo único compartido por todos los visitantes | corregido y verificado |
| D-02 a D-05 | media | elementos nulos en listas → HTTP 500; `{}` guardaba datos por defecto | corregidos y verificados |

## Estado de producción ahora
- Tarifa vigente: Estandar 150, Urgente 300, Retiro 120 (versión 7). Regla operativa vigente: 3 intentos, prueba "Firma" (versión 6).
- Las versiones intermedias del cuadro tarifario (2 a 6) y de las reglas operativas (2 a 5) son del propio ensayo y quedan en el historial.
- Hay envíos de prueba con nombre `AUDIT-…` y una posición GPS de prueba para limpiar al final.
- **Receptor de prueba de avisos: ENCENDIDO** (`RECEPTOR_PRUEBA_AVISOS=true` en el `.env.prod` de la VM) y en modo 200. Es un endpoint anónimo: apagarlo después de la defensa. La suscripción del comercio demo apunta a `http://api1:8080/api/testing/webhook-receiver`.
- **Antes del jueves:** regenerar la clave de API del comercio si la demo de la API pública usa una guardada (mi barrido la rotó). El operador 2 no trae zonas ni comercios: ya se le creó una zona y un comercio `AUDIT…` (ver F4).

## Cómo saber si sigo trabajando
1. La hora de la última línea de `docs/auditoria/diario.log`.
2. La fecha de modificación de `C:\Users\Curbe\.claude\projects\c--Users-Curbe-OneDrive-Escritorio-Proyecto-Proyecto-Net\70ebc7ed-eac9-44d4-99b2-f66f2414612d.jsonl`.
3. `C:\Users\Curbe\auditoria_local\latido.txt` (anti-suspensión, cada 5 min) y `reanudar.log` (script de reanudación).

## Cola de ideas para las próximas rondas (ronda 12 en adelante)
Seguridad de red y borde: puertos abiertos desde Internet (22, 5432, 5672, 15672, 6379, 9090, 3000), exposición y credenciales por defecto de Grafana y Prometheus, TLS (versiones y certificado), redirección HTTP→HTTPS, cabeceras en los cuatro dominios, CORS con orígenes ajenos, banderas de las cookies.
Sesión y cuentas: bloqueo al 5.º intento (cuenta `audit.*`), enumeración de usuarios por mensaje o tiempo, política de contraseñas débiles, vigencia del JWT, token de un usuario desactivado, auto-edición de perfil (asignación masiva).
Entradas y archivos: importación CSV (reimportación idempotente, fórmulas `=`, archivo enorme, codificación), carga de logos (SVG con script, tipo falsificado, ruta en el nombre), cuerpo de 30 MB.
Seguimiento público: campos devueltos (sin identificadores internos ni datos personales), enumeración de códigos, reprogramación abusiva.
Tiempo real y operación: autenticación y aislamiento de SignalR entre operadores, apagado controlado con una petición en vuelo, tareas programadas sin duplicado con dos instancias, secretos o contraseñas en los registros, crecimiento del outbox.
API pública: clave de pruebas y límite de 20/min, rotación de claves.
Infraestructura: `terraform.tfstate` dentro de OneDrive (copia en la nube), SSH abierto a `*` en el grupo de red, F7 (destruir y reconstruir) al final, limpieza de datos `AUDIT`.
