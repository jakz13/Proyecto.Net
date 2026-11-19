# Cómo probar la API pública

Referencia de la API: [API_Publica.md](API_Publica.md).

## 1. Pruebas automáticas
Necesitan Postgres local.
```powershell
docker compose up -d postgres
dotnet test UltimaMilla.Backend.slnf -c Release --filter "FullyQualifiedName~ApiPublicaTests"
```

## 2. Swagger
- Servidor: `https://shippingbox.duckdns.org/swagger`
- Local: `http://localhost:<puerto>/swagger` (con la API levantada)

Elegir el selector **"API pública para comercios (v1)"**. El otro selector es la API interna.

## 3. A mano con curl

### Obtener la clave
1. Entrar al Portal del comercio con una cuenta demo.
2. **Clave de pruebas:** `GET /api/comercio/clave-api-pruebas`. Valida el pedido y calcula la tarifa sin guardar nada.
3. **Clave real:** `POST /api/comercio/clave-api`. Se muestra una sola vez; regenerarla invalida la anterior.

### Enviar un pedido
```bash
curl -X POST https://shippingbox.duckdns.org/api/integracion/v1/envios \
  -H "X-Api-Key: <clave>" -H "Content-Type: application/json" \
  -d '{"modalidadServicio":"Estandar","nombreDestinatario":"Ana","calle":"Av. Italia","numero":"100","ciudad":"Montevideo","bultos":[{"peso":2,"alto":10,"ancho":10,"profundo":10}],"referenciaExterna":"PEDIDO-123"}'
```

## 3b. Con script
```powershell
$env:ULTIMAMILLA_PASSWORD = '<contraseña de demo>'
.\scripts\probar-api-publica.ps1                    # sandbox, 401, 400 y 429
.\scripts\probar-api-publica.ps1 -ProbarReal        # además 201 y duplicado (REGENERA la clave real)
.\scripts\probar-api-publica.ps1 -BaseUrl http://localhost:5000
```
Imprime `[OK]`/`[FALLO]` por caso y devuelve código de salida 1 si algo falla.

## 4. Resultados esperados
| Caso | Respuesta |
|---|---|
| Clave de pruebas | `200`, `sandbox: true`, código `SBX-…` |
| Clave real, primera vez | `201` |
| Misma `referenciaExterna` repetida | `200`, `duplicado: true` |
| Pedido inválido | `400` |
| Clave inválida | `401` |
| Misma referencia en simultáneo | `409` (se puede reintentar sin duplicar) |
| Cupo superado | `429` |

## 5. Límites de tasa
- Producción: 120 pedidos/min por clave.
- Pruebas: 20 pedidos/min por clave.
- Sin clave: 30/min por IP.

Para provocar el `429`, enviar más de 20 pedidos en un minuto con la clave de pruebas.

El cupo se cuenta por instancia: con `api1` y `api2` corriendo, el efectivo es el doble.
