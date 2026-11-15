# Pendientes de la app móvil para cerrar el laboratorio (para Joaquín)

> **Actualización 12/11:** las secciones 1.1 (entrega parcial por bultos) y 1.2 (retiro en domicilio) **ya están implementadas en la app** (commit 979456c: `RegistroEntregaViewModel`, `RegistroRetiroViewModel`, cola offline y sincronización en `SyncService`, funcionalidades habilitadas por operador) y el encabezado `X-Correlation-Id` ya lo envía `AuthDelegatingHandler`. Lo que sigue pendiente es **verificarlo en un dispositivo** (escenarios offline de la defensa), la medición de batería (`docs/Geocercas_Bateria.md`, sección 5) y las notificaciones con la app cerrada. Lo de abajo se conserva como contrato y lista de comprobación.

Estado al 10/11/2026. La rama `mobile` tiene su último commit del 5/11. El backend ya soporta todo lo de abajo y está desplegado; **la app todavía no usa los cambios del 9/11**. El contrato detallado del retiro está en [`UC-Retiro-Movil.md`](UC-Retiro-Movil.md).

## 0. Antes de empezar
- Traer `desarrollo-backend` (o `main`, que ya lo tiene) a `mobile`: trae los cambios de contrato de la API (paradas con `tipo`, endpoints nuevos) y el encabezado `X-Correlation-Id` del `AuthDelegatingHandler`.
- Confirmar que la APK apunta al servidor (`https://shippingbox.duckdns.org`) y que el login del repartidor funciona contra producción (la clave por defecto de los repartidores ya no existe: el alta exige contraseña).

## 1. Obligatorios del cambio de requerimientos (se evalúan el jueves 12/11 en el monitoreo)

### 1.1 Entrega parcial por bultos (RF 31 a 36) — **no está en la app**
- Escenario de defensa: *entregar parcialmente un envío sin conectividad, sincronizar y verificar en backoffice, seguimiento público y aviso al comercio*.
- Backend: `POST /api/mobile/v1/rutas/{rutaId}/paradas/{paradaId}/entrega-parcial` (idempotente; mismo patrón de cola offline que la entrega exitosa).
- La app necesita: marcar cada bulto como entregado / pendiente / devuelto en la parada, registrar la prueba de entrega, encolar offline y reenviar con la misma clave de idempotencia, y mostrar el resultado de la sincronización.

### 1.2 Retiro en domicilio (RF 37 a 41, equipos de 4) — **no está en la app**
- Escenario de defensa: *hoja de ruta mixta con entregas y retiros*.
- La descarga de la ruta (`GET /api/mobile/v1/rutas`) trae paradas `tipo = "Retiro"` con `retiroId`, `bultos`, `destinatario`, etc. (ver contrato).
- Registrar: `POST …/paradas/{paradaId}/retiro` (escanear **todos** los bultos, foto del estado y firma obligatorias; también offline) y `POST …/retiro/no-realizado`.
- Conflicto offline: si responde `conflictoResuelto: true`, mostrar el aviso y dar la operación por sincronizada.
- Una ruta puede tener solo retiros: se despacha sin escaneo de carga.
- Nota: lo que hoy hay en la app con "Retiro" (`RETIRO_DEVOLUCION`) es otra cosa (devolución al depósito), no este flujo.

### 1.3 Funcionalidades habilitadas
- `GET /api/funcionalidades` → `{ "retiro-en-domicilio": true, "entrega-parcial": true, … }` para ocultar lo que el operador no tiene habilitado.

## 2. No funcionales obligatorios de la app
- **6.12 Observabilidad:** enviar `X-Correlation-Id` en cada llamada (el handler ya lo tiene en `desarrollo-backend`; verificar que se genera por operación y se conserva al reenviar lo encolado).
- **6.11 Notificaciones con la app cerrada:** el ADR-002 descarta FCM y usa un servicio en primer plano; **es riesgo de nota**. Preparar el argumento de la defensa (qué cubre, qué no, y por qué) o evaluar FCM solo para la reasignación urgente (que además suma 3 puntos opcionales, ver abajo).

## 3. Opcionales a cargo de la app (suman a la meta de 16 puntos)
| Opcional | Pts | Estado | Qué falta |
|---|---|---|---|
| Geocercas | 3 | Implementado | **Análisis del impacto en batería** (medición con y sin geocercas, y conclusión). Sin eso no otorga puntos |
| Optimización del orden de paradas | 2 | 2-opt implementado | **Tabla medida** contra el criterio del RF 16 (franja, prioridad, antigüedad): distancia base vs. optimizada en varios casos |
| Impresión por Bluetooth | 2 | No iniciado | Imprimir etiqueta o comprobante en impresora térmica portátil |
| Notificaciones push remotas | 3 | No iniciado | FCM real con la app cerrada, reasignación urgente y ciclo de vida de tokens de dispositivo |
| gRPC para sincronización | 4 | No iniciado | Canal gRPC con client streaming y comparación medida contra REST |

Recordatorio de la letra: *un opcional implementado parcialmente no otorga puntos parciales*. Los dos primeros solo faltan documentación medida: son los de mejor relación esfuerzo/puntos.

## 3 bis. Actualización de la app por descarga directa (OTA): lo que falta en la app
El servidor ya está listo (ver `README.md` y ADR-007): `https://shippingbox.duckdns.org/descargas/version.json` y `.../descargas/UltimaMilla-Release-Signed.apk`. Formato de `version.json`: `versionCode`, `versionName`, `apkUrl`, `sha256`, `tamano`, `notas`. **Esto no es una notificación push**: no cubre "notificaciones con la app cerrada" (6.11) ni suma los 3 puntos opcionales de push; solo evita pasar por una tienda.
El servicio de la app (`Services/Updates/AppUpdateService.cs`) ya consulta, descarga y lanza el instalador, con las reglas de seguridad de `PoliticaDeActualizacion.cs` (huella SHA-256 obligatoria, tamaño exacto, solo https del propio servidor, sin redirecciones; cubierto por `ActualizacionDeLaAppTests`). Sigue siendo necesario, y solo se prueba en el teléfono:
- Al abrir la app, consultar `version.json` (sin caché) y comparar `versionCode` con el instalado; avisar si hay una versión nueva.
- Descargar el APK, **verificar `sha256` y `tamano`** antes de instalar y descartar el archivo si no coinciden.
- Permiso `REQUEST_INSTALL_PACKAGES` en el `AndroidManifest.xml`, un `FileProvider` para pasar el APK al instalador y guiar al usuario para que habilite "instalar apps desconocidas" para esta app.
- Subir `ApplicationVersion` en el `.csproj` en **cada** versión publicada: con el mismo número Android no actualiza y `scripts/publicar-apk.ps1` rechaza el APK.
- Sacar las contraseñas del keystore del `.csproj` (hoy están escritas ahí) y pasarlas por variable de entorno.

## 4. Orden sugerido
1. Merge de `main` a `mobile` y comprobar que compila y entra a producción.
2. Entrega parcial offline (1.1): es el escenario del monitoreo.
3. Retiro en la hoja de ruta mixta (1.2) si el equipo es de 4.
4. Medición de batería y tabla de optimización (3): sin código nuevo, solo medir y documentar.
5. El resto según el tiempo que quede.
