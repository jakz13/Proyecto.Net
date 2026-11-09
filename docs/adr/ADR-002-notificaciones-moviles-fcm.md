# ADR-002: Estrategia de Notificaciones Moviles y Descarte de Firebase Cloud Messaging (FCM)

## Estado
Aprobado

## Fecha
2026-11-05

## Contexto y Problematica
El punto 5 de la auditoria tecnica (relacionado con el alcance del modulo movil y RNF 6.11) planteo la evaluacion de notificaciones push con la aplicacion cerrada para eventos de reasignacion urgente de paquetes o modificaciones de ruta dispuestas por el despachador.

Para implementar notificaciones push tradicionales en dispositivos Android cerrados se requiere integrar Google Firebase Cloud Messaging (FCM), lo cual introduce:
1. Dependencia estricta de una cuenta corporativa externa de Google Firebase, exigiendo archivos de credenciales propietarias (`google-services.json`), claves de servidor / cuentas de servicio de Google Cloud, y configuraciones ajenas al control de despliegue local o autohospedado del evaluador.
2. Compromiso de seguridad y portabilidad: subir credenciales reales al repositorio infringe las reglas de seguridad corporativas (OWASP y `rules/02-secretos-y-configuracion.md`), mientras que depender de un archivo inexistente provocaria fallos de compilacion nativa en el SDK de Android (`com.google.gms.google-services`).
3. La letra de la especificacion (Seccion 7.3 - Funcionalidades Opcionales / Puntos Extra) establece una eleccion entre modulos opcionales para completar la calificacion maxima requerida.

## Decision Arquitectonica

Se resuelve:
1. **Descartar el acoplamiento a Firebase Cloud Messaging (FCM):**
   No se incorpora el SDK externo de Firebase ni se exige `google-services.json`. Se preserva la portabilidad, compilacion limpia sin dependencias en la nube de terceros y cumplimiento estricto de gestion de secretos.

2. **Cobertura de avisos urgentes mediante Canal Persistente SignalR + Foreground Service en Android:**
   - Durante la jornada operativa de reparto en calle, la aplicacion mantiene en ejecucion continua el servicio nativo en primer plano (`LocationForegroundService`), calificado como `foregroundServiceType="location"` con notificacion persistente del sistema operativo y `WakeLock` parcial.
   - Mientras este servicio esta activo, el proceso de la aplicacion MAUI se mantiene vivo y retiene la conexion duplex de SignalR (`OperacionHub`) con reconexion exponencial automatica ante variaciones de red celular.
   - Cuando el despachador realiza una reasignacion, cancelacion o reprogramacion en el Backoffice, el evento se transmite por SignalR (`ReasignacionRecibida`, `RutaActualizada`) y el servicio de notificaciones locales de la app (`INotificacionLocalService`) dispara de forma inmediata una notificacion heads-up en el canal de alta prioridad del sistema operativo Android (`ultimamilla_alertas_urgentes`), emitiendo vibracion y alerta sonora para el repartidor aun con la pantalla bloqueada.

3. **Alcance de Modulos Opcionales (Letra Seccion 7.3):**
   Conforme a las opciones brindadas en la Seccion 7.3 de la letra del proyecto, se priorizo e implemento en su totalidad:
   - **Modulo Opcional A: Deteccion de Geocercas con Analisis de Impacto en Bateria (3 Puntos):** Servicio `GeofenceService` que computa la proximidad Haversine al destino, transiciona automaticamente paradas a `EnSitio`, modula los intervalos de muestreo GPS para preservar autonomia y registra eventos analiticos de telemetria.
   - **Modulo Opcional B: Optimizacion Heuristica de Secuencia de Paradas 2-Opt (2 Puntos):** Algoritmo local `RutaOptimizationService` que permite al repartidor reordenar dinamicamente la secuencia de entregas pendientes minimizando la distancia euclediana/geografica recorrida antes de iniciar o durante el trayecto.

## Consecuencias

### Positivas
- Compilacion nativa reproducible y limpia para cualquier desarrollador o evaluador sin necesidad de registrar proyectos en consolas externas de Google.
- Alertas en tiempo real efectivas y de latencia minima durante la ejecucion de la ruta en calle.
- Cero exposicion de claves de API de servicios externos.
- Cumplimiento integral del puntaje de funcionalidades avanzadas mediante Geocercas y Optimizacion 2-Opt.

### Negativas / Mitigaciones
- Si el repartidor mata intencionalmente el proceso de la aplicacion y detiene de forma manual el Servicio en Primer Plano, las notificaciones remotas no se reciben hasta que vuelva a abrir la app.
- Mitigacion: Las reasignaciones quedan registradas en el backend e impactan de forma consistente al reabrir la aplicacion o al sincronizar la hoja de ruta mediante `GET /api/mobile/v1/rutas/activa`.

