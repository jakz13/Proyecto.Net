# Plan de pruebas de carga (opcional 7.4)

Herramienta: **k6** (`scripts/carga/carga.js`). Los objetivos de nivel de servicio (SLO) están definidos como *umbrales* del propio guion: si no se cumplen, k6 termina con error.

## Escenarios
| Escenario | Tipo | Perfil | Qué verifica |
|---|---|---|---|
| `seguimiento` | Rampa | 0 → 10 → 50 → 100 usuarios virtuales en 2 min | Consulta pública (el componente de mayor tráfico y el único anónimo): latencia y que la limitación de tasa proteja sin errores de servidor |
| `alta` | Concurrencia sostenida | 1,5 altas/s por API durante 90 s, con la clave de producción | Alta de envíos con tarifa, outbox y publicación bajo carga constante |
| `duracion` | Duración | 2 usuarios durante 3 min | Degradación con el tiempo (memoria, conexiones) |
| `sync` | **Sincronización simultánea de repartidores** | N repartidores (20 en la medición) a la vez, una sola vuelta cada uno | Al regresar al depósito: inicio de sesión, descarga de la ruta, registro del retiro, **reenvío idempotente** (la app sin conexión repite) y rendición. Cada repartidor tiene su propio usuario, vehículo y ruta |

El `setup()` crea los datos de cada repartidor por la propia API (flota, retiro, hoja de ruta despachada): no depende de datos previos.

## Objetivos de nivel de servicio
| Escenario | Objetivo |
|---|---|
| Consulta pública | p95 < 300 ms y p99 < 800 ms; **cero errores 5xx** (los 429 son la protección funcionando) |
| Alta por API | p95 < 800 ms; menos del 1 % de fallos |
| Sincronización | 100 % de las comprobaciones correctas; p95 del ciclo completo de un repartidor < 2 s |

## Resultados (ambiente local, `docker compose`, 20 repartidores)
| Escenario | Medido | Objetivo | |
|---|---|---|---|
| Consulta pública | p95 **3,7 ms**, p99 5,7 ms, 0 errores de servidor | p95 < 300 ms | Cumple |
| Alta por API | p95 **17 ms**, 136 altas, 0 fallos | p95 < 800 ms | Cumple |
| Sincronización simultánea (20 repartidores) | ciclo completo p95 **139 ms**, 100 % de comprobaciones (descarga, retiro, reenvío idempotente, rendición) | p95 < 2 s | Cumple |
| Duración (3 min) | sin degradación; 27 835 iteraciones sin errores | sin errores | Cumple |

## Resultados sobre el ambiente remoto (12/11/2026, VM de Azure, 2 instancias de la API detrás de Caddy, desde una PC en Uruguay)
| Escenario | Medido | Objetivo | |
|---|---|---|---|
| Consulta pública, rampa 0 → 100 usuarios en 2 min | 21 628 pedidos (179/s), **p95 44,7 ms, p99 48,3 ms**, **0 errores de servidor** (los 404/429 son la protección y códigos inexistentes) | p95 < 300 ms, p99 < 800 ms, 0 errores 5xx | Cumple |

Incluye la latencia de red de ida y vuelta (~40 ms). Se ejecutó con `SIN_CUENTAS=true` (solo el escenario anónimo). **Sincronización simultánea sobre el ambiente remoto (13/11/2026, 10 repartidores a la vez):** 40/40 comprobaciones correctas (descarga de la ruta, retiro, reenvío idempotente y rendición), ciclo completo por repartidor **p95 1,37 s** (promedio 1,2 s, máximo 1,41 s; objetivo < 2 s). **Pendiente:** el escenario de alta por API, que necesita la clave de integración del operador, y la repetición de los demás con las cuentas de demostración (contraseña `SEED_PASSWORD_DEMO` del servidor, que no se guarda en el repositorio). Se ejecutan así, y dejan datos de prueba en la base:
```bash
MSYS_NO_PATHCONV=1 docker run --rm -i -v "$(pwd -W)/scripts/carga:/scripts" grafana/k6 run   -e BASE_URL=https://shippingbox.duckdns.org -e ESCENARIOS=seguimiento,sync -e REPARTIDORES=5   -e PASSWORD_DEMO='<SEED_PASSWORD_DEMO del servidor>' /scripts/carga.js
```
Hasta que se haga y se anote aquí, el opcional de carga cumple "ejecución sobre el ambiente remoto" solo en parte.

**Qué dicen los números (con honestidad):**
- Con un solo origen, la consulta pública queda dominada por el **límite de tasa por IP** (30/min): de 27 700 pedidos, **27 589 recibieron 429 en ~3 ms** y solo los permitidos se atendieron. Eso demuestra la protección, no la capacidad máxima. La capacidad real de la consulta se sostiene en el caché (ADR-003) y se mediría con orígenes distribuidos.
- Es una máquina de desarrollo con la base en el mismo equipo. **La sincronización no se ejecutó todavía sobre el ambiente remoto** (ver arriba): la VM es una `E2.1.Micro` (1 GB) en producción y una rampa de 100 usuarios la afectaría mientras se la usa para la defensa. Se ejecuta apuntando `BASE_URL` a producción en una ventana acordada.

## Cómo ejecutarlo
```bash
# Local (con docker compose up) — sin instalar nada
docker run --rm -i -v "$PWD/scripts/carga:/scripts" grafana/k6 run \
  -e BASE_URL=http://host.docker.internal:5000 -e REPARTIDORES=20 /scripts/carga.js

# Remoto (ventana acordada; la instancia es chica, bajar REPARTIDORES y la rampa)
k6 run -e BASE_URL=https://shippingbox.duckdns.org -e ESCENARIOS=alta,sync -e REPARTIDORES=10 scripts/carga/carga.js
```
**Ojo:** el escenario `sync` deja datos de prueba (repartidores, vehículos, retiros) en la base contra la que se ejecute.

`ESCENARIOS` acepta cualquier combinación de `seguimiento,alta,duracion,sync`. El sembrado de demostración debe estar presente (cuentas y clave de API del operador 1).
