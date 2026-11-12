# Optimización del orden de paradas: comparación medida (opcional 7.3)

Qué se pide: ordenar la ruta con un algoritmo de optimización y **compararlo con una medición contra el criterio adoptado en el requerimiento funcional 16**.

## Los dos criterios
| | Criterio del RF 16 (base) | Optimización de la app |
|---|---|---|
| Dónde | Backend, al armar la hoja de ruta (`ArmarHojaDeRutaCommandHandler.Ordenar`) y espejo en la app (`ConstruirSecuenciaEstandarDespacho`) | App del repartidor (`RutaOptimizationService`) |
| Regla | Inicio de la franja comprometida → prioridad → zona → código de seguimiento. La app, que no conoce prioridad ni zona, ordena por **cierre de la ventana y código** | Vecino más cercano con penalización por ventana (cierres que se alejan más de 90 minutos cuestan 0,05 km por minuto) y refinamiento **2-opt** (hasta 100 iteraciones). Nunca devuelve algo peor que la base |
| Qué prioriza | Cumplir ventanas y prioridades; no mira la distancia | Distancia recorrida, cuidando las ventanas |

La app conserva las paradas ya completadas en su posición y solo reordena las pendientes. El ordenamiento del backend es el criterio "documentado y razonable" pedido por la letra; la optimización es un refinamiento **opcional y local** que decide el repartidor.

## Medición
Se compila el servicio **real** de la app (`RutaOptimizationService.cs`, sin copiarlo) en un arnés de consola y se ejecutan 300 rutas sintéticas por tamaño, con paradas al azar en Montevideo y su área metropolitana cercana y tres ventanas (09-12, 12-15, 15-18). Distancia por Haversine desde el punto de partida operativo. Reproducible: `dotnet run -c Release` en `scripts/medicion/optimizacion/`.

| Paradas | Casos | Distancia base media | Distancia optimizada media | Ahorro medio | Ahorro mediano | Ahorro mín. – máx. | Pares de paradas fuera del orden de ventana | Tiempo de cálculo |
|---|---|---|---|---|---|---|---|---|
| 8 | 300 | 69,9 km | 48,2 km | **31,1 %** | 30,1 % | 0,0 – 63,4 % | 7,5 % | 0,2 ms |
| 15 | 300 | 129,7 km | 75,0 km | **42,2 %** | 42,1 % | 18,1 – 56,5 % | 5,6 % | 0,3 ms |
| 25 | 300 | 219,2 km | 104,5 km | **52,3 %** | 52,5 % | 25,7 – 68,3 % | 4,8 % | 0,9 ms |
| 40 | 300 | 349,7 km | 141,1 km | **59,6 %** | 59,6 % | 38,3 – 70,3 % | 3,8 % | 1,9 ms |

(El ahorro crece con el tamaño porque el criterio base ignora la geografía: salta entre puntos lejanos dentro de una misma ventana.)

## Lectura honesta de los resultados
- La optimización reduce la distancia entre **31 % y 60 %** según el tamaño de la ruta, en una fracción de milisegundo, sin conexión y sin enviar datos a ningún servicio.
- **Costo**: entre 4 % y 8 % de los pares de paradas quedan en un orden distinto al de sus ventanas (una parada de una ventana posterior se visita antes que otra de una anterior). Es el precio de acortar el recorrido; la penalización por ventana lo mantiene acotado y el repartidor decide si aplica la nueva secuencia. Por eso la optimización **no sustituye** al criterio del RF 16 en el backend, que garantiza el orden comprometido.
- Las distancias son en línea recta (Haversine), no por calle: sirven para comparar los dos órdenes entre sí, no como kilómetros reales. El tiempo estimado en la app supone 25 km/h y 5 minutos por parada.
- Los datos son sintéticos, uniformes en el área; con clientes concentrados por zona la diferencia puede ser menor. No se midió sobre rutas reales de producción.
