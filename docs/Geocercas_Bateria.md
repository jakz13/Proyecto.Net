# Geocercas: análisis de consumo de batería (opcional 7.3)

Este documento explica cómo funcionan las geocercas en la app móvil (llegada y salida automática de parada) y por qué **ayudan a ahorrar batería** en lugar de gastarla.

## 1. Cómo funcionan en la app
- **No abrimos un segundo servicio de GPS:** en vez de usar `GeofencingClient` de Android aparte, `GeofenceService.EvaluarPosicionAsync` aprovecha las mismas lecturas GPS que ya obtiene el seguimiento normal de la jornada (`GeolocalizacionTrackingService`).
- Con cada lectura calcula la distancia Haversine contra las paradas pendientes y aplica doble umbral para evitar rebotes del GPS:
  - **Entrada (`LLEGADA_GEOCERCA`):** distancia `< 100 m`.
  - **Salida (`SALIDA_GEOCERCA`):** distancia `> 150 m` (luego de haber entrado), guardando también los segundos de permanencia.
- Cuando se cruza un umbral, guarda el evento en SQLite y lo envía al backend. Al finalizar la ruta, la evaluación se detiene.

## 2. Costo del cálculo en CPU
Medido con un microbenchmark en .NET 10 (200.000 posiciones en PC de desarrollo):

| Paradas pendientes | Tiempo por posición GPS |
|---|---|
| 10 | 0,20 µs |
| 30 | 0,67 µs |
| 60 | 1,36 µs |

Incluso en un celular 50 veces más lento, evaluar 60 paradas lleva menos de `0,1 ms` cada 8 segundos (< 0,0001 % de CPU). **El cálculo matemático es despreciable.**

## 3. Cómo controlan el consumo real del GPS (muestreo adaptativo)
Lo que realmente gasta batería es pedir ubicaciones muy seguido y con máxima precisión. Como la geocerca calcula a qué distancia está la próxima parada, `ActualizarPerfilMuestreoAdaptativo` ajusta el GPS automáticamente:

| Perfil | Cuándo se activa | Cada cuánto mide | Precisión | Posiciones por hora |
|---|---|---|---|---|
| En puerta | Parada a `< 500 m` | 8 s | Alta | 450 |
| Aproximación | Entre `500 m` y `2000 m` | 20 s | Media | 180 |
| Tránsito lejano | A más de `2000 m` | 60 s | Baja | 60 |
| Ahorro crítico | Batería `< 20 %` o modo ahorro | 60 s | Baja | 60 |

Si no tuviéramos este ajuste y midiéramos siempre cada 8 s en precisión alta, serían **450 posiciones/hora**. Con un reparto típico (25 % en puerta, 35 % aproximación, 40 % tránsito), el promedio baja a **200 posiciones/hora (−56 %)** y con precisión alta solo una cuarta parte del tiempo.

## 4. Lo que falta medir en teléfono físico
Los valores en mAh/h definidos en el código (`42`, `19` y `8 mAh/h`) son estimaciones de diseño, no mediciones reales en un teléfono. Para completar la tabla con un dispositivo real:
1. Hacer dos recorridos iguales con batería al 100 %: **A** con intervalo fijo de 8 s en precisión alta, y **B** con geocercas y muestreo adaptativo.
2. Consultar `PosicionesVehiculo` (que ya guarda `BateriaPct`) con esta consulta:
```sql
SELECT "HojaDeRutaId",
       min("Timestamp") AS inicio, max("Timestamp") AS fin,
       max("BateriaPct") - min("BateriaPct") AS puntos_consumidos,
       round(((max("BateriaPct") - min("BateriaPct")) /
              greatest(extract(epoch FROM (max("Timestamp") - min("Timestamp"))) / 3600.0, 0.01))::numeric, 1) AS puntos_por_hora,
       count(*) AS posiciones
FROM "PosicionesVehiculo"
WHERE "HojaDeRutaId" = '<id de la hoja de ruta>' AND "BateriaPct" IS NOT NULL
GROUP BY "HojaDeRutaId";
```

## 5. Tabla para completar en dispositivo real
| Recorrido | Duración | Posiciones | Puntos de batería consumidos | Puntos por hora |
|---|---|---|---|---|
| A. Sin geocercas (8 s fijo) | | | | |
| B. Con geocercas y muestreo adaptativo | | | | |

## 6. Conclusión
Las geocercas tienen un costo de CPU prácticamente nulo y **reducen el consumo total de batería** al habilitar el muestreo adaptativo (−56 % de lecturas GPS en el ejemplo típico). Solo queda pendiente completar la tabla de la sección 5 con la medición en teléfono físico.
