# Retiro en domicilio en la app del repartidor (para Joaquín)

El backend ya soporta el retiro (cambio de requerimientos 2.2, RF 39 y RF 40). Falta la app. Todo en `desarrollo-backend`; hacer merge a `mobile`.

## Qué cambia en la descarga de la hoja de ruta (`GET /api/mobile/v1/rutas`)
Cada parada trae ahora `tipo` (`"Entrega"` o `"Retiro"`). En las de retiro: `retiroId`, `motivoRetiro`, `modalidadServicio = "Retiro"`, `codigoSeguimiento` (`RET-…`), `destinatario` (quien entrega los bultos, con teléfono) y `bultos` (identificación y peso). `envioId` viene vacío (`Guid.Empty`). Una ruta puede tener solo retiros: se despacha sin escaneo de carga.
Si el operador no tiene habilitado el retiro, esas paradas no llegan.

## Registrar el retiro (también sin conexión: encolar y reenviar con la misma clave)
`POST /api/mobile/v1/rutas/{rutaId}/paradas/{paradaId}/retiro`
```json
{ "retiroId": "…", "idempotencyKey": "…", "codigosBultosEscaneados": ["RET-AB12CD34EF-B01"],
  "fotoEstadoBase64": "…", "firmaBase64": "…", "nombreEntrega": "Ana Pérez", "documentoEntrega": "1.234.567-8",
  "latitud": -34.9, "longitud": -56.1, "fechaHoraDispositivoUtc": "2026-11-09T14:30:00Z" }
```
Reglas: se deben escanear **todos** los bultos; foto y firma son obligatorias. Respuesta: `estadoRetiro`, `estadoParada`, `idempotente`, `conflictoResuelto`.
Si no se pudo retirar: `POST …/retiro/no-realizado` con `{ retiroId, idempotencyKey, motivoCodigo, observaciones, latitud, longitud }` (el motivo sale de `configuracion.motivosNoEntrega`).

## Conflictos sin conexión
Si el despachador sacó el retiro de la ruta o el comercio lo canceló mientras no había conexión, **prevalece lo hecho en el domicilio**: el servidor lo acepta y responde `conflictoResuelto: true`. La app debe mostrar ese aviso y dar por sincronizada la operación.

## Otros pendientes de la app
- Enviar `X-Correlation-Id` en cada llamada (letra 6.12: el seguimiento debe empezar en el teléfono).
- `GET /api/funcionalidades` → `{ "retiro-en-domicilio": true, "entrega-parcial": true, … }` para ocultar lo deshabilitado.
- Entrega por bultos (cambio 2.1): `POST …/paradas/{paradaId}/entrega-parcial` ya existe en el backend.
