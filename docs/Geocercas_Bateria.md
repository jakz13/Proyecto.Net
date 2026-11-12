# Geocercas: análisis del impacto en el consumo de batería (opcional 7.3)

Qué se pide: detección automática de llegada y salida de parada, con el registro de eventos y **análisis del impacto sobre el consumo de batería**.

## 1. Cómo funcionan las geocercas en la app (y por qué casi no cuestan)
- **No hay un servicio de geocercas del sistema operativo** (ni `GeofencingClient` de Android ni `CLCircularRegion` de iOS). La app evalúa las geocercas por software sobre las posiciones que **ya obtiene** el rastreo de la jornada (`GeolocalizacionTrackingService` → `GeofenceService.EvaluarPosicionAsync`). Por lo tanto las geocercas **no abren ningún sensor ni servicio adicional**: el GPS se enciende por el seguimiento de posición (requerimiento 23), con o sin geocercas.
- Lo único que agregan es: una distancia de Haversine por parada pendiente en cada posición recibida, la histéresis (entrada a menos de 100 m, salida a más de 150 m) y, cuando hay un cruce, una escritura en SQLite y un evento encolado para sincronizar.
- Se evalúan solo las paradas pendientes de la ruta activa, y se detiene con la ruta.

## 2. Costo medido del cálculo
Microbenchmark (Haversine, .NET 10, 200 000 posiciones, equipo de desarrollo; es una cota superior del trabajo del cálculo, no del teléfono):

| Paradas pendientes | Tiempo por posición GPS |
|---|---|
| 10 | 0,20 µs |
| 30 | 0,67 µs |
| 60 | 1,36 µs |

Aun suponiendo un procesador de teléfono 50 veces más lento, 60 paradas cuestan menos de 0,1 ms por posición. Con una posición cada 8 s (el caso más frecuente) el cálculo ocupa menos de 0,0001 % del tiempo de CPU. **El cálculo de las geocercas es despreciable frente al consumo del GPS y de la radio.**

## 3. Dónde sí está el consumo: el muestreo del GPS, y cómo lo controlan las geocercas
Lo que consume batería es la frecuencia y la precisión con que se pide la posición. La app usa **muestreo adaptativo guiado por la distancia a la parada más cercana**, que es información que producen las propias geocercas (`ActualizarPerfilMuestreoAdaptativo`):

| Perfil | Condición | Intervalo | Precisión | Posiciones por hora |
|---|---|---|---|---|
| En puerta | parada a menos de 500 m | 8 s | Alta | 450 |
| Aproximación | 500 m a 2000 m | 20 s | Media | 180 |
| Tránsito lejano | más de 2000 m | 60 s | Baja | 60 |
| Ahorro crítico | batería < 20 % o ahorro de energía activo | 60 s | Baja | 60 |

Comparación con un seguimiento **sin** geocercas que muestree siempre en el peor caso (8 s, precisión alta) para no perder una llegada: 450 posiciones por hora. Con el muestreo adaptativo, una jornada típica reparte el tiempo entre los perfiles; si el 25 % del tiempo está "en puerta", el 35 % en "aproximación" y el 40 % en "tránsito", la media es 0,25·450 + 0,35·180 + 0,40·60 = **200 posiciones por hora (-56 %)**, y con precisión alta solo el 25 % del tiempo. Es decir: **las geocercas, lejos de aumentar el consumo, permiten bajarlo respecto de un rastreo ciego de alta frecuencia**, porque saben cuándo conviene muestrear poco.

Otras decisiones que cuidan la batería: la transmisión usa el mismo canal que la telemetría (no abre una conexión aparte), los eventos se sincronizan en lotes cuando hay red, y no hay evaluación cuando la ruta está finalizada.

## 4. Lo que NO está medido
El código de la app incluye valores de consumo por perfil (`_consumoEstimadoMahHora`: 42, 19 y 8 mAh/h) que son **estimaciones de diseño**, no mediciones en un dispositivo. No se midió el consumo real en un teléfono con y sin geocercas. Para cerrar ese punto con datos, el procedimiento es el siguiente.

### Procedimiento de medición (30 minutos con un teléfono)
1. Cargar la batería al 100 % y apagar otras aplicaciones. Hacer dos recorridos iguales (mismo trayecto, misma ruta de 10 paradas, mismo brillo y datos móviles):
   - **A (sin geocercas)**: rastreo con intervalo fijo de 8 s y precisión alta.
   - **B (con geocercas y muestreo adaptativo)**: la configuración normal de la app.
2. La app ya envía el nivel de batería con cada posición (`BateriaPct` en `PosicionesVehiculo`). El descenso por hora se calcula con la consulta de abajo; no hace falta herramienta externa.
3. Anotar los resultados en la tabla de la sección 5.

```sql
-- Consumo medio de batería por hora de un recorrido (puntos porcentuales por hora)
SELECT "HojaDeRutaId",
       min("Timestamp") AS inicio, max("Timestamp") AS fin,
       max("BateriaPct") - min("BateriaPct")                                   AS puntos_consumidos,
       round(((max("BateriaPct") - min("BateriaPct")) /
              greatest(extract(epoch FROM (max("Timestamp") - min("Timestamp"))) / 3600.0, 0.01))::numeric, 1) AS puntos_por_hora,
       count(*)                                                                 AS posiciones
FROM "PosicionesVehiculo"
WHERE "HojaDeRutaId" = '<id de la hoja de ruta del recorrido>' AND "BateriaPct" IS NOT NULL
GROUP BY "HojaDeRutaId";
```

## 5. Resultados de la medición en dispositivo
| Recorrido | Duración | Posiciones | Puntos de batería consumidos | Puntos por hora |
|---|---|---|---|---|
| A. Sin geocercas, 8 s fijo | | | | |
| B. Con geocercas y muestreo adaptativo | | | | |

## 6. Conclusión
El impacto de las geocercas sobre la batería es **despreciable en cómputo** (microsegundos por posición) y **favorable en consumo total** porque habilitan el muestreo adaptativo (−56 % de posiciones por hora en la jornada típica del ejemplo). El único punto pendiente es completar la tabla de la sección 5 con la medición en el teléfono antes de la defensa.
