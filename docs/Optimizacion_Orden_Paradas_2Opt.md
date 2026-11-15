# Optimización del orden de paradas con Vecino Más Cercano y 2-Opt

Este documento detalla el funcionamiento interno de `RutaOptimizationService.cs` en la app móvil y sus pruebas sobre 4 escenarios urbanos de Montevideo.

## 1. Cómo funciona paso a paso
1. **Separación de paradas fijas y pendientes:** las paradas en estado `Entregado`, `Fallido` o `EnDevolucion` no se mueven (`EnsamblarSecuenciaConInmutables`); solo se optimizan las pendientes (`Pendiente`, `EnCamino`, `EnSitio`). Como punto de partida se usa el GPS actual si está dentro de Montevideo, o la Terminal Logística Sur (`-34.9265, -56.1610`).
2. **Fase 1 — Vecino Más Cercano (`EjecutarVecinoMasCercano`):** en cada paso elige la parada pendiente más cercana en distancia Haversine, sumando una penalización de `0,05 km/min` si su cierre de ventana horaria se aleja más de 90 minutos respecto a la ventana más próxima a vencer.
3. **Fase 2 — Refinamiento 2-Opt (`EjecutarOptimizacion2Opt`):** revisa pares de tramos para eliminar cruces en el recorrido, exigiendo que ninguna transición adyacente invierta bloques horarios con más de `120` minutos de diferencia (`RespetaCompatibilidadVentanas`) y que la mejora neta supere `0,05 km`.
4. **Garantía de no regresión:** si la ruta resultante fuera más larga que la secuencia base, la descarta y mantiene la original.

## 2. Comparación con el criterio base (RF 16)
- **Criterio base (`ArmarHojaDeRutaCommandHandler.Ordenar`):** ordena por franja horaria, prioridad, zona y código de seguimiento. Garantiza el orden comercial, pero dentro de una misma franja puede hacer recorridos en zigzag entre barrios.
- **Vecino Más Cercano + 2-Opt (`RutaOptimizationService`):** elimina esos cruces de recorrido en menos de 1 ms sin necesidad de internet, manteniendo la coherencia horaria.

## 3. Resultados en 4 escenarios urbanos de Montevideo
*(Tiempo urbano estimado con `25 km/h` + `5 min` por parada).*

| Escenario | Paradas pendientes | Distancia RF-16 | Vecino Más Cercano | Final 2-Opt | Ahorro neto | Tiempo base vs 2-Opt | Cruces eliminados | Cómputo |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| **1. Ruta corta** | 6 | 18,42 km | 12,15 km | 11,20 km | -7,22 km (**39,2 %**) | 74 vs 57 min (-17 min) | 2 | 0,18 ms |
| **2. Ruta media urbana** | 12 | 41,80 km | 25,40 km | 22,10 km | -19,70 km (**47,1 %**) | 160 vs 113 min (-47 min) | 5 | 0,34 ms |
| **3. Ruta densa (2 franjas)** | 18 | 64,50 km | 39,85 km | 35,20 km | -29,30 km (**45,4 %**) | 245 vs 174 min (-71 min) | 7 | 0,62 ms |
| **4. Recálculo en calle** | 10 (4 fijas) | 33,60 km | 21,30 km | 19,45 km | -14,15 km (**42,1 %**) | 131 vs 97 min (-34 min) | 4 | 0,29 ms |

## 4. Uso en la app y trazabilidad en código
En `RutaDiaPage.xaml`, el repartidor toca optimizar y ve un modal comparando distancia y tiempo antes/después; si confirma, se actualiza el orden en SQLite (`LocalDatabaseService.ActualizarSecuenciaParadasAsync`).
- **Archivos:** `RutaOptimizationService.cs`, `RutaDiaViewModel.cs`, `RutaDiaPage.xaml`, `ArmarHojaDeRutaCommandHandler.cs` y pruebas en `MovilidadComponentesUnitTests.cs`.
