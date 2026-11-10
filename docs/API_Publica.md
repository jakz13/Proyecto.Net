# API pública para comercios (opcional 7.4)

Documentación navegable: `https://shippingbox.duckdns.org/swagger` → selector **"API pública para comercios (v1)"** (muestra solo las rutas de integración; la otra entrada es la API interna completa).

## Cómo cumple cada punto del requerimiento
| Requisito | Implementación |
|---|---|
| Documentada | Documento OpenAPI propio (`/swagger/integracion/swagger.json`) con autenticación, límites, idempotencia y versionado; prueba `ApiPublicaTests` |
| Versionada | La versión va en la ruta: `/api/integracion/v1/...`. Un cambio incompatible sale como `v2` y `v1` sigue vigente |
| Claves por comercio | Cada relación comercio-operador tiene su clave (`POST /api/comercio/clave-api`, se muestra una sola vez; en base queda solo su huella SHA-256). Regenerar invalida la anterior |
| Ambiente de pruebas | Clave de pruebas por comercio (`GET /api/comercio/clave-api-pruebas`): valida el pedido y calcula la tarifa real **sin registrar ni publicar nada** (`sandbox: true`, código `SBX-…`). Es una firma HMAC de la relación, sin datos extra que guardar |
| Limitación de tasa diferenciada | Cupo por clave: producción 120/min, pruebas 20/min, sin clave 30/min por IP (frena el intento de adivinar claves). Al superarlo, 429 |

## Uso
```bash
# Alta de un envío (idempotente con referenciaExterna)
curl -X POST https://shippingbox.duckdns.org/api/integracion/v1/envios \
  -H "X-Api-Key: <clave>" -H "Content-Type: application/json" \
  -d '{"modalidadServicio":"Estandar","nombreDestinatario":"Ana","calle":"Av. Italia","numero":"100",
       "ciudad":"Montevideo","bultos":[{"peso":2,"alto":10,"ancho":10,"profundo":10}],"referenciaExterna":"PEDIDO-123"}'
```
Respuestas: `201` creado, `200` reintento con la misma referencia (`duplicado: true`) o llamada de pruebas (`sandbox: true`), `400` validación, `401` clave inválida, `409` solicitud simultánea con la misma referencia (reintentar), `429` límite de tasa.

Los avisos de cambio de estado llegan al comercio por webhook firmado (HMAC-SHA256 del cuerpo en la cabecera de firma) con reintentos y cola de fallidos; ver ADR-004.

## Límites conocidos
- El cupo se cuenta por instancia de la API: con dos instancias el cupo efectivo es el doble (explícito en el documento de cambios, 3.1).
- La clave de pruebas se deriva del secreto de la plataforma: si se rota ese secreto, cambia (se vuelve a consultar).
