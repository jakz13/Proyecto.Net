# Pruebas de resiliencia (opcional 7.4)

Para verificar cómo reacciona la plataforma ante caídas reales, detuvimos deliberadamente cada dependencia con `docker stop` mientras enviábamos tráfico real, comparando el comportamiento esperado con el observado.

## 1. Resultados
| Dependencia caída | Comportamiento esperado | Comportamiento observado | Resultado |
|---|---|---|---|
| **Redis** (caché y respaldo de SignalR, local) | La API sigue atendiendo contra PostgreSQL; solo se pierde el caché y la difusión de SignalR entre instancias | `/ready` 200; login 200; alta **201**; consulta pública 200 (primer pedido 1,9 s, luego normal). Al volver Redis se normalizó solo (alta en 25 ms) | Cumple (ver hallazgo 1) |
| **RabbitMQ** (cola, local) | Las altas no se pierden: quedan en `OutboxMessages` y se publican cuando vuelve la cola | Con la cola detenida: 2 altas **201** en 20–30 ms y **6 eventos pendientes** en el outbox. Al reiniciar RabbitMQ, a los 15 s quedaron en **0** procesados por el Worker | Cumple: sin pérdida de eventos |
| **PostgreSQL** (base de datos, local) | Sin base no se puede escribir: error claro y recuperable sin filtrar detalles internos, manteniendo el proceso vivo | `/live` 200, `/ready` **503** en 4,4 s, alta **503** con `Retry-After: 5`. La consulta pública **siguió respondiendo 200 desde el caché**. Al volver la base, `/ready` volvió a 200 en < 5 s | Cumple (ver hallazgo 2) |
| **Una instancia de la API** (producción en Azure, detrás de Caddy) | Caddy retira la instancia caída por el chequeo de salud y atiende con la otra | Medido el 13/11/2026 con `docker stop shippingbox-api1-1` y 12 consultas seguidas a `/ready` durante ~25 s: **12 de 12 respondieron 200** (primera 3,5 s; desde la tercera 0,15 s) y el login siguió dando 200. Al hacer `docker start`, se reincorporó sola | Cumple: sin corte para el usuario |
| **Servicio externo** (webhook de comercio) | Reintentos con espera creciente y, al agotarlos, pasa a cola de fallidos para reenvío manual | Probado en vivo en producción (`INFORME.md`, Ronda 9): con el receptor en modo `503` (`/api/testing/webhook-receiver/configuracion`), 3 altas quedaron en `Reintentando` con espera creciente (1 min, 5 min); al restaurar a `200 OK`, 2 se reintentaron manualmente desde el portal y el 3.º se recuperó solo en el siguiente ciclo (`11/11` firmas HMAC-SHA256 válidas, 0 duplicados) | Cumple: reintento automático y manual verificados |

## 2. Fallas encontradas y corregidas durante las pruebas
1. **Demora de 19,5 s al caer Redis:** al inicio cada operación esperaba el timeout completo. Se agregó `CortacircuitosCache` (si falla, durante 10 s va directo a la base sin reintentar Redis) y timeouts de 1 s en SignalR. Con eso bajó a **4,0 s** en el primer pedido y normal en los siguientes (`ResilienciaCacheTests`).
2. **Error 500 con detalles internos al caer PostgreSQL:** se agregó un manejador global de errores que devuelve **503 + `Retry-After`** si la base no responde, y un `500` genérico con `traceId` (sin stack trace) para otros errores.

## 3. Degradación aceptada
- Con Redis caído, no se invalidan entradas de caché; las entradas viejas se corrigen solas al vencer su TTL.
- Con Redis caído y dos instancias de la API, el tablero en tiempo real solo muestra eventos de la instancia a la que está conectado el usuario.
- Sin PostgreSQL no se pueden registrar operaciones; solo sigue respondiendo lo que está en caché (seguimiento público).

## 4. Cómo reproducirlo en local
```bash
docker compose up -d
curl -X POST http://localhost:5000/api/integracion/v1/envios -H "X-Api-Key: dev-api-key-megastore-2026" -H "Content-Type: application/json" \
  -d '{"modalidadServicio":"Estandar","nombreDestinatario":"Prueba","calle":"Av. Italia","numero":"100","ciudad":"Montevideo","bultos":[{"peso":2,"alto":10,"ancho":10,"profundo":10}]}'
docker stop ultimamilla-redis      # probar consulta/alta; luego: docker start ultimamilla-redis
docker stop ultimamilla-rabbitmq   # probar alta y ver OutboxMessages; luego: docker start ultimamilla-rabbitmq
docker stop ultimamilla-postgres   # ver /ready 503 y alta 503; luego: docker start ultimamilla-postgres
```
