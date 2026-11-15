# Plan de pruebas de carga (opcional 7.4)

Las pruebas de carga usan **k6** (`scripts/carga/carga.js`). Los objetivos de nivel de servicio (SLO) están configurados como umbrales (*thresholds*) dentro del propio script: si alguno no se cumple, k6 termina con error.

## 1. Escenarios probados
| Escenario | Tipo | Carga | Qué verifica |
|---|---|---|---|
| `seguimiento` | Rampa | 0 → 10 → 50 → 100 usuarios virtuales en 2 min | Consulta pública (la más visitada y anónima): latencia y protección por límite de tasa sin errores de servidor |
| `alta` | Concurrencia sostenida | 1,5 altas/s por API durante 90 s (clave de producción) | Alta de envíos con tarifa, outbox y publicación bajo carga constante |
| `duracion` | Duración | 2 usuarios durante 3 min | Que no haya degradación de memoria o conexiones con el paso del tiempo |
| `sync` | **Sincronización simultánea de repartidores** | N repartidores a la vez (20 en local, 10 en Azure), una vuelta cada uno | Login, descarga de ruta, registro de retiro, **reenvío idempotente** y rendición |

El `setup()` del script crea sus propios datos de prueba por la API (flota, retiro y hoja de ruta), sin depender de datos previos.

## 2. Objetivos de nivel de servicio (SLO)
| Escenario | Objetivo definido |
|---|---|
| Consulta pública | p95 < 300 ms y p99 < 800 ms; **0 errores 5xx** (los `429` son el límite de tasa funcionando) |
| Alta por API | p95 < 800 ms; menos de 1 % de fallos |
| Sincronización | 100 % de comprobaciones correctas; p95 del ciclo completo de un repartidor < 2 s |

## 3. Resultados en ambiente local (`docker compose`, 20 repartidores)
| Escenario | Medido | Objetivo | Estado |
|---|---|---|---|
| Consulta pública | p95 **3,7 ms**, p99 5,7 ms, 0 errores 5xx | p95 < 300 ms | Cumple |
| Alta por API | p95 **17 ms**, 136 altas, 0 fallos | p95 < 800 ms | Cumple |
| Sincronización simultánea (20 repartidores) | ciclo completo p95 **139 ms**, 100 % de comprobaciones | p95 < 2 s | Cumple |
| Duración (3 min) | 27 835 iteraciones sin errores ni degradación | sin errores | Cumple |

## 4. Resultados en ambiente remoto (VM de Azure, 2 instancias detrás de Caddy)
| Escenario | Fecha | Medido | Objetivo | Estado |
|---|---|---|---|---|
| Consulta pública (rampa 0 → 100 usuarios en 2 min) | 12/11/2026 | 21 628 pedidos (179/s), **p95 44,7 ms, p99 48,3 ms**, **0 errores 5xx** | p95 < 300 ms, p99 < 800 ms, 0 errores 5xx | Cumple |
| Sincronización simultánea (**10 repartidores a la vez**) | 13/11/2026 | **40/40** comprobaciones correctas (descarga, retiro, reenvío idempotente, rendición), ciclo completo **p95 1,37 s** (media 1,2 s, máx. 1,41 s) | p95 < 2 s, 100 % checks | Cumple |

*Pendiente en remoto:* ejecutar únicamente el escenario `alta` (con la clave de integración del operador). Se corre así (deja datos de prueba en la base):
```bash
MSYS_NO_PATHCONV=1 docker run --rm -i -v "$(pwd -W)/scripts/carga:/scripts" grafana/k6 run \
  -e BASE_URL=https://shippingbox.duckdns.org -e ESCENARIOS=seguimiento,sync -e REPARTIDORES=5 \
  -e PASSWORD_DEMO='<SEED_PASSWORD_DEMO del servidor>' /scripts/carga.js
```

## 5. Interpretación honesta de los resultados
- Al correr `seguimiento` desde una única IP, el límite de tasa por IP (30/min) bloqueó 27 589 de 27 700 pedidos con `429` en ~3 ms. Esto demuestra que la protección contra abuso funciona; la capacidad bruta del caché se mediría distribuyendo IPs de origen.
- El escenario `sync` deja datos de prueba (repartidores, vehículos y retiros) en la base contra la que se ejecute.

## 6. Cómo ejecutarlo
```bash
# Local (con docker compose up)
docker run --rm -i -v "$PWD/scripts/carga:/scripts" grafana/k6 run \
  -e BASE_URL=http://host.docker.internal:5000 -e REPARTIDORES=20 /scripts/carga.js

# Remoto (en una ventana acordada)
k6 run -e BASE_URL=https://shippingbox.duckdns.org -e ESCENARIOS=alta,sync -e REPARTIDORES=10 scripts/carga/carga.js
```
