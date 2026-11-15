# ADR-002: Cómo enviamos notificaciones urgentes al celular sin usar Firebase

**Estado:** Aprobado · **Fecha:** 2026-11-05 · **Responsable:** Joaquín Bassini · **Requerimientos:** 6.11 y opcionales 7.3

## 1. Contexto y problema
La letra planteaba recibir notificaciones urgentes en el celular cuando el despachador reasigna o cambia una ruta, incluso con la pantalla apagada o la app en segundo plano.

La opción clásica en Android es Firebase Cloud Messaging (FCM), pero integrarlo nos traía complicaciones concretas para un proyecto académico:
1. Exige crear una cuenta externa en Google Firebase y depender de archivos y claves privadas (`google-services.json` y credenciales de servidor).
2. No podíamos subir esas claves al repositorio por seguridad, y sin ese archivo el proyecto Android ni siquiera compila en la máquina de otro compañero o del docente que evalúa.

## 2. Decisión
- **Descartar Firebase (FCM):** la app no incluye el SDK de Firebase ni pide archivos externos de Google para compilar o correr.
- **Usar SignalR + servicio en primer plano de Android (`LocationForegroundService`):**
  - Mientras el repartidor está en jornada, la app mantiene activo un servicio en primer plano (con una notificación fija de que está rastreando la ruta y un `WakeLock` parcial para que Android no la duerma).
  - Gracias a ese servicio, la app mantiene abierta la conexión en tiempo real por SignalR (`OperacionHub`), con reconexión automática si cambia la red.
  - Cuando el despachador reasigna o modifica la ruta, el servidor avisa por SignalR y la app dispara una notificación local de alta prioridad en el teléfono (con sonido y vibración), incluso con la pantalla bloqueada.
- **Opcionales elegidos para el puntaje:** en lugar de las notificaciones push remotas con la app totalmente cerrada, priorizamos e implementamos **Geocercas con análisis de batería** y **Optimización del orden de paradas (2-opt)**.

## 3. Alternativas consideradas
- **Integrar Firebase Cloud Messaging (FCM):** descartada por la dependencia de cuentas y secretos externos de Google que complican la portabilidad y compilación limpia del proyecto.
- **Hacer consultas periódicas (polling) cada pocos segundos desde la app:** descartada porque consume más batería, genera tráfico innecesario y avisa más lento que SignalR.

## 4. Consecuencias
- **Ventajas:** cualquier persona puede clonar y compilar la app móvil sin configurar cuentas de Google ni exponer claves privadas; además, durante la ruta activa los avisos llegan al instante.
- **Desventajas y cómo se mitigan:** si el repartidor fuerza el cierre total de la app y detiene el servicio en primer plano, no recibirá avisos hasta volver a abrirla. Cuando la vuelve a abrir, la app sincroniza su hoja de ruta vigente desde `GET /api/mobile/v1/rutas/activa` y se pone al día.
