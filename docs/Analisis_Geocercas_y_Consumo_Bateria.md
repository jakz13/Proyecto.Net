# Estudio técnico de geocercas y consumo de batería

Este documento amplía `docs/Geocercas_Bateria.md` detallando cómo se implementó el sistema de geocercas en la app móvil (`GeofenceService.cs`), el muestreo adaptativo (`GeolocalizacionTrackingService.cs`) y la proyección energética para una jornada de 8 horas.

## 1. Problema y solución con doble umbral (histéresis)
En ciudad, el GPS de un celular suele oscilar entre `±15 m` y `±35 m` aunque el repartidor esté quieto. Si usáramos un solo radio (por ejemplo `100 m`), un repartidor detenido a `95 m` generaría entradas y salidas falsas (*efecto ping-pong*).

Para evitarlo, `GeofenceService` usa dos umbrales con una banda muerta de 50 metros entre medio:

| Parámetro en `GeofenceService.cs` | Valor | Cuándo dispara | Evento generado |
|---|---|---|---|
| **Entrada (`UmbralEntradaMetros`)** | `< 100,0 m` | Estaba afuera y baja de 100 m | `LLEGADA_GEOCERCA` |
| **Zona muerta (histéresis)** | `100,0` a `150,0 m` | Mantiene el estado anterior sin cambiar | Ninguno (absorbe rebotes GPS) |
| **Salida (`UmbralSalidaMetros`)** | `> 150,0 m` | Ya había entrado y supera 150 m | `SALIDA_GEOCERCA` (+ permanencia en segundos) |

## 2. Persistencia local y sincronización
1. En cada posición GPS, `EvaluarPosicionAsync` mide la distancia en metros hacia las paradas pendientes de la ruta activa.
2. Al entrar o salir, guarda un `EventoGeocercaLocal` en SQLite cifrado (`SQLCipher`) y encola la operación `EventoGeocerca`.
3. Si hay señal, `SyncService` la envía a `POST /api/mobile/v1/rutas/{rutaId}/paradas/{paradaId}/geocerca`, donde `RegistrarEventoGeocercaCommandHandler` la valida con idempotencia y la agrega al historial del `Envio`.

## 3. Modelo de muestreo adaptativo de 4 perfiles
Usando la distancia a la parada más cercana y el nivel de batería (`Battery.Default`), `ActualizarPerfilMuestreoAdaptativo` ajusta el GPS:

| Perfil | Condición | Intervalo | Precisión | Lecturas/hora | Consumo estimado |
|---|---|---|---|---:|---:|
| **1. En puerta** | `< 500 m` (batería ≥ 20 %) | `8 s` | `High` | 450 | `~42 mAh/h` |
| **2. Aproximación** | `500 m` a `2000 m` (batería ≥ 20 %) | `20 s` | `Medium` | 180 | `~19 mAh/h` |
| **3. Tránsito lejano** | `> 2000 m` (batería ≥ 20 %) | `60 s` | `Low` | 60 | `~8 mAh/h` |
| **4. Ahorro crítico** | Batería `< 20 %` o ahorro activo | `60 s` | `Low` | 60 | `~8 mAh/h` |

## 4. Comparación estimada en una jornada de 8 horas (batería de 4500 mAh)
Tomando un turno típico (15 % del tiempo en puerta, 35 % en aproximación y 50 % en tránsito lejano):
- **Con muestreo adaptativo:** promedio de `16,95 mAh/h` → **`~135,6 mAh` en 8 horas** (3,0 % de la batería, `1248` lecturas).
- **Sin optimización (fijo cada 3–5 s en `High`):** `~72,0 mAh/h` → **`~576,0 mAh` en 8 horas** (12,8 % de la batería, `5760` a `9600` lecturas).
- **Ahorro estimado:** **`-76,4 %`** en consumo de GPS/telemetría, permitiendo completar turnos de 10 a 12 horas sin recargar. *(Ver `docs/Geocercas_Bateria.md` para el procedimiento de medición en teléfono físico).*

## 5. Operación en segundo plano y trazabilidad en código
- **Segundo plano en Android:** `LocationForegroundService.cs` mantiene un servicio en primer plano (`TypeLocation`) con notificación persistente y `WakeLock` parcial para seguir midiendo aunque se apague la pantalla.
- **Archivos principales:** `GeofenceService.cs`, `GeolocalizacionTrackingService.cs`, `LocationForegroundService.cs`, `RegistrarEventoGeocercaCommandHandler.cs` y las 4 pruebas unitarias en `MovilidadComponentesUnitTests.cs`.
