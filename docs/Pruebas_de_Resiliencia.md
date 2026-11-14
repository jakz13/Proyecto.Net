# Pruebas de resiliencia (opcional 7.4)

Se interrumpió cada dependencia con `docker stop` sobre el ambiente local (`docker compose`: API, worker, Postgres, Redis, RabbitMQ) mientras se hacían solicitudes reales, y se anotó qué se esperaba y qué se observó. Reproducible con el guion de abajo.

## Resultados
| Dependencia caída | Comportamiento esperado | Comportamiento observado | Resultado |
|---|---|---|---|
| **Redis** (caché y respaldo de SignalR) | La API sigue funcionando contra la base; solo se pierde el caché y la difusión a otras instancias | `/ready` 200; login 200; alta de envío **201**; consulta pública 200 (primer pedido 1,9 s, el resto sin demora). Al volver Redis todo se normaliza solo (alta en 25 ms) | Cumple, con una demora corregida (ver hallazgo 1) |
| **RabbitMQ** (cola) | El alta no se pierde: el evento queda en el outbox y se publica al volver la cola | Con la cola detenida: 2 altas **201** en 20-30 ms; **6 eventos pendientes** en el outbox. Al reiniciar RabbitMQ, a los 15 s quedaron en **0** (el worker los procesó) | Cumple: sin pérdida de eventos |
| **PostgreSQL** (base) | Sin base no se puede operar: error claro y recuperable, sin colgarse ni filtrar detalles; el proceso sigue vivo | `/live` 200 (el proceso vive), `/ready` **503** en 4,4 s (el balanceador saca la instancia), alta **503** `{"error":"El servicio no está disponible temporalmente…"}` con `Retry-After: 5` (antes: 500 con la excepción completa). La consulta pública **siguió respondiendo 200 desde el caché**. Al volver la base, `/ready` 200 en menos de 5 s y el alta 201 | Cumple (tras el hallazgo 2) |
| **Una instancia de la API** (ambiente remoto, 2 instancias detrás de Caddy) | Caddy retira la instancia caída por el chequeo `/live` y atiende con la otra | Medido el 13/11/2026 sobre producción: `docker stop shippingbox-api1-1` y 12 consultas seguidas a `https://shippingbox.duckdns.org/ready` durante ~25 s: **12 de 12 respondieron 200**; la primera tardó 3,5 s (Caddy aún probaba la instancia caída) y desde la tercera 0,15 s. El inicio de sesión respondió 200 mientras `api1` estaba detenida. Al volver a levantarla (`docker start`) se reincorporó sola | Cumple: sin errores para el usuario |
| **Servicio externo** (webhook del comercio caído) | Reintentos con espera creciente y, agotados, cola de fallidos con reenvío manual | Cubierto por el diseño (ADR-004) y el receptor de prueba con modo 500 (`/api/testing/webhook-receiver/configuracion`); las llamadas salientes nunca siguen redirecciones ni llegan a redes internas | Probar en vivo en la defensa |

## Hallazgos que se corrigieron
1. **Con Redis caído, una solicitud tardó 19,5 s.** Cada operación de caché y cada emisión del respaldo de SignalR esperaba su tiempo de espera completo. Corrección: cortacircuitos del caché (`CortacircuitosCache`: tras un fallo no se vuelve a intentar durante 10 s y se va directo a la base) y tiempos de espera de 1 s en el respaldo de SignalR. **Medido después: 4,0 s** el primer pedido tras la caída y normal en adelante. Pruebas: `ResilienciaCacheTests`.
2. **Con la base caída, la API respondía 500 con la excepción completa** (tipo, mensaje de Npgsql). Se filtraba información interna y el cliente no sabía que podía reintentar. Corrección: manejador global de errores: **503 + `Retry-After`** si la base no responde, y 500 genérico con un `traceId` (el detalle queda en los logs) para cualquier otro fallo.

## Degradación aceptada (decirla antes de que pregunten)
- Con Redis caído, las invalidaciones de caché se omiten durante la pausa; las entradas viejas se corrigen por su vencimiento. Es el costo de no bloquear.
- Con Redis caído y dos instancias de la API, el tablero en tiempo real solo recibe lo que ocurre en su propia instancia.
- Sin base de datos no hay operación posible; solo responde lo que está en caché (consulta pública).

## Guion para reproducirlo
```bash
docker compose up -d
curl -X POST http://localhost:5000/api/integracion/v1/envios -H "X-Api-Key: dev-api-key-megastore-2026" -H "Content-Type: application/json" \
  -d '{"modalidadServicio":"Estandar","nombreDestinatario":"Prueba","calle":"Av. Italia","numero":"100","ciudad":"Montevideo","bultos":[{"peso":2,"alto":10,"ancho":10,"profundo":10}]}'
docker stop ultimamilla-redis      # repetir el alta y medir el tiempo; luego: docker start ultimamilla-redis
docker stop ultimamilla-rabbitmq   # repetir el alta; ver OutboxMessages con Procesado=false; luego: docker start ultimamilla-rabbitmq
docker stop ultimamilla-postgres   # /ready 503, alta 503 con Retry-After; luego: docker start ultimamilla-postgres
```
