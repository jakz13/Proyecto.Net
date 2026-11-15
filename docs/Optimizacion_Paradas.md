# Optimización del orden de paradas: comparación medida (opcional 7.3)

Este documento compara el ordenamiento administrativo base del backend (**RF 16**) contra la optimización local de la app móvil (`RutaOptimizationService.cs`).

## 1. Los dos criterios comparados
| Aspecto | Criterio base del RF 16 | Optimización en la app móvil |
|---|---|---|
| Dónde corre | Backend (`ArmarHojaDeRutaCommandHandler.Ordenar`) y espejo en la app (`ConstruirSecuenciaEstandarDespacho`) | App del repartidor (`RutaOptimizationService`) |
| Cómo ordena | Inicio de franja horaria → prioridad → zona → código de seguimiento (la app usa cierre de ventana y código) | Vecino más cercano con penalización por ventana (> 90 min cuesta 0,05 km/min) + refinamiento **2-opt** (hasta 100 iteraciones). Nunca devuelve una ruta más larga que la base |
| Qué prioriza | Cumplir ventanas y prioridades sin mirar distancia | Reducir kilómetros recorridos respetando ventanas |

Las paradas ya completadas quedan fijas y solo se reordenan las pendientes. La optimización es opcional y la decide el repartidor.

## 2. Medición reproducible (300 rutas por tamaño)
Se compiló el `RutaOptimizationService.cs` real dentro de un arnés de consola (`dotnet run -c Release` en `scripts/medicion/optimizacion/`) ejecutando 300 rutas sintéticas por tamaño en Montevideo y área metropolitana con tres ventanas (`09-12`, `12-15`, `15-18`) y distancia Haversine:

| Paradas | Casos | Distancia base media | Distancia optimizada media | Ahorro medio | Ahorro mediano | Rango mín.–máx. | Pares fuera de orden de ventana | Tiempo de cálculo |
|---|---|---|---|---|---|---|---|---|
| 8 | 300 | 69,9 km | 48,2 km | **31,1 %** | 30,1 % | 0,0 – 63,4 % | 7,5 % | 0,2 ms |
| 15 | 300 | 129,7 km | 75,0 km | **42,2 %** | 42,1 % | 18,1 – 56,5 % | 5,6 % | 0,3 ms |
| 25 | 300 | 219,2 km | 104,5 km | **52,3 %** | 52,5 % | 25,7 – 68,3 % | 4,8 % | 0,9 ms |
| 40 | 300 | 349,7 km | 141,1 km | **59,6 %** | 59,6 % | 38,3 – 70,3 % | 3,8 % | 1,9 ms |

## 3. Lectura honesta de los resultados
- El algoritmo reduce la distancia entre **31 % y 60 %** en menos de 2 milisegundos y sin conexión.
- **Costo:** entre 4 % y 8 % de los pares de paradas cambian de orden respecto a sus ventanas (una parada de una ventana posterior se visita antes para evitar retrocesos). Por eso no reemplaza al criterio RF 16 del backend y queda como sugerencia opcional para el repartidor.
- **Limitaciones del ensayo:** las distancias son en línea recta (Haversine) y sobre puntos aleatorios en el área; en rutas reales concentradas por barrio el ahorro puede ser menor.
