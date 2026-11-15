# API pública para comercios (opcional 7.4)

Documentación navegable en Swagger: `https://shippingbox.duckdns.org/swagger` → selector **"API pública para comercios (v1)"** (muestra únicamente las rutas de integración; el otro selector corresponde a la API interna).

## 1. Cómo cumple cada requisito
| Requisito | Cómo lo resolvimos |
|---|---|
| Documentada | Documento OpenAPI propio (`/swagger/integracion/swagger.json`) que detalla autenticación, límites de tasa, idempotencia y versionado; validado con `ApiPublicaTests` |
| Versionada | La versión va en la ruta (`/api/integracion/v1/...`). Si un cambio rompe compatibilidad se publica como `v2` sin retirar `v1` |
| Claves por comercio | Cada relación comercio-operador tiene su clave (`POST /api/comercio/clave-api`). Se muestra una sola vez y en la base se guarda solo su huella SHA-256; regenerarla invalida la anterior |
| Ambiente de pruebas | Clave de pruebas por comercio (`GET /api/comercio/clave-api-pruebas`): valida el pedido y calcula la tarifa real **sin guardar ni publicar nada** (`sandbox: true`, código `SBX-…`). Se genera con firma HMAC sin guardar datos extra |
| Límite de tasa diferenciado | Cupo por clave: **120 pedidos/min** en producción, **20/min** en pruebas y **30/min por IP** sin clave (frena intentos de adivinar claves). Al superarlo responde `429` |

## 2. Ejemplo de uso
```bash
# Alta de un envío (idempotente mediante referenciaExterna)
curl -X POST https://shippingbox.duckdns.org/api/integracion/v1/envios \
  -H "X-Api-Key: <clave>" -H "Content-Type: application/json" \
  -d '{"modalidadServicio":"Estandar","nombreDestinatario":"Ana","calle":"Av. Italia","numero":"100",
       "ciudad":"Montevideo","bultos":[{"peso":2,"alto":10,"ancho":10,"profundo":10}],"referenciaExterna":"PEDIDO-123"}'
```

**Respuestas posibles:** `201` creado · `200` reintento con la misma referencia (`duplicado: true`) o llamada de prueba (`sandbox: true`) · `400` error de validación · `401` clave inválida · `409` solicitud simultánea con la misma referencia (reintentar sin riesgo de duplicar) · `429` límite de tasa superado.

Los avisos de cambio de estado llegan al comercio por webhook firmado (`HMAC-SHA256` del cuerpo en la cabecera de firma), con reintentos automáticos y cola de fallidos (ver `ADR-004`).

## 3. Limitaciones conocidas
- El límite de tasa se cuenta por instancia de la API: con dos instancias corriendo (`api1` y `api2`), el cupo efectivo es el doble.
- La clave de pruebas se deriva del secreto de la plataforma: si ese secreto se rota, el comercio solo tiene que volver a consultarla.
