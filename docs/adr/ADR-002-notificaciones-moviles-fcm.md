# ADR-002: Cómo enviamos notificaciones urgentes al celular sin usar Firebase

- **Estado:** Aceptado
- **Fecha:** 2026-11-05
- **Responsable:** Joaquín Bassini

## Contexto y restricciones

El alcance del módulo móvil (requerimiento 6.11) contempla recibir notificaciones urgentes en el teléfono cuando el despachador reasigna o modifica una ruta en curso, incluso con la pantalla apagada o la aplicación en segundo plano.

La alternativa estándar en Android es Firebase Cloud Messaging (FCM), pero su adopción impone restricciones operativas y de portabilidad:
1. Obliga a crear una cuenta externa en Google Firebase y depender de archivos propietarios (`google-services.json`) y credenciales de servidor fuera del control del entorno autohospedado de evaluación.
2. Por política de seguridad no se pueden subir claves reales al repositorio (`.agents/rules/02-secretos-y-configuracion.md`), y sin `google-services.json` la compilación nativa Android falla al integrar el plugin de Google Services.
3. La sección 7.3 permite elegir módulos opcionales alternativos para completar la funcionalidad avanzada evaluada.

## Decisión adoptada

- **Descartar Firebase Cloud Messaging (FCM):** la app no incorpora el SDK de Firebase ni depende de archivos o cuentas externas de Google para compilar ni ejecutarse.
- **Usar canal dúplex SignalR + Servicio en Primer Plano de Android (`LocationForegroundService`):**
  - Durante toda la jornada operativa, la app mantiene activo `LocationForegroundService` (con notificación persistente de rastreo y `WakeLock` parcial para que Android no suspenda el proceso al apagar la pantalla).
  - Mientras el servicio está activo, la aplicación conserva conectada la sesión en tiempo real contra `OperacionHub` (`RealtimeTrackingService`), con reconexión automática incremental frente a cambios de red celular.
  - Cuando el despachador reasigna o altera la ruta, el servidor notifica por SignalR (`ReasignacionRecibida`, `CambioEstadoRuta`) y `NotificacionLocalService` (`INotificacionLocalService`) dispara de inmediato una notificación local de alta prioridad en el canal Android (`ultimamilla_alertas_operativas`), con sonido y vibración incluso con pantalla bloqueada.
- **Módulos opcionales elegidos en reemplazo (sección 7.3):** en lugar de push con el proceso totalmente terminado, se priorizaron e implementaron **Geocercas con análisis de impacto en batería** (`GeofenceService`) y **Optimización heurística del orden de paradas 2-Opt** (`RutaOptimizationService`).

## Alternativas consideradas y motivo de su descarte

- **Firebase Cloud Messaging (FCM):** Descartado porque corresponde a un requerimiento opcional no elegido (reemplazado por Geocercas y análisis de batería) y obliga a depender de una cuenta externa de Google y de credenciales privadas (`google-services.json` y clave de servicio) que no pueden subirse al repositorio sin comprometer la reproducibilidad del entorno.
- **Azure Notification Hubs / OneSignal:** Descartado porque para Android siguen requiriendo configurar FCM como transporte subyacente, manteniendo la dependencia de credenciales externas.
- **Sondeo periódico con Android WorkManager sin ForegroundService:** Descartado porque el intervalo mínimo de ejecución de WorkManager es de 15 minutos y el modo Doze de Android difiere los trabajos con pantalla apagada, impidiendo recibir avisos urgentes en tiempo real.

## Consecuencias

### Ventajas
- Compilación y despliegue totalmente reproducibles en cualquier equipo o pipeline de CI sin registrar proyectos externos en Google ni exponer secretos.
- Avisos operativos instantáneos durante toda la jornada en ruta aprovechando la conexión SignalR ya abierta.

### Desventajas y mitigaciones
- Si el repartidor fuerza el cierre completo de la aplicación y detiene manualmente el servicio en primer plano, no recibirá avisos mientras el proceso siga cerrado; como mitigación, al reabrir la aplicación sincroniza de inmediato la ruta vigente mediante `GET /api/mobile/v1/rutas/activa` y `GET /api/mobile/v1/rutas/activas`.

### Qué dificulta a futuro
- **Mensajería push silenciosa con la app totalmente cerrada o fuera de turno:** al no integrar un canal push nativo gestionado por el sistema operativo (FCM / APNs), si en el futuro se exigiera despertar el teléfono cuando el repartidor aún no inició jornada o tras un cierre forzado, habrá que incorporar un proveedor push nativo y gestionar el registro de tokens por dispositivo.
- **Portabilidad hacia iOS bajo políticas estrictas de segundo plano:** a diferencia del `ForegroundService` de Android durante la jornada, iOS restringe la ejecución prolongada en segundo plano, por lo que una eventual versión productiva iOS requerirá adoptar APNs.
- **Adaptación continua a restricciones energéticas de futuras versiones de Android:** endurecimientos adicionales de Doze y permisos de servicios en primer plano (Android 14/15+) exigirán mantener explícitos los permisos de ubicación y notificación persistente para evitar que el sistema suspenda la conexión.
