# Traspaso: estado al 10/11 y pendientes (para retomar)

Rama de trabajo: `desarrollo-backend`. **Hay commits locales sin subir** (el usuario retiró la autorización de subir en esta tanda). Lo último subido y desplegado en producción es `8193a0d` (retiros, entrega parcial, seguridad por perfil, banderas). Suite: 139 pruebas de integración + 908 de dominio, aplicación y arquitectura en el backend, y 111 del frontend, todas en verde.

## Hecho (todo commiteado)
| Tema | Resultado |
|---|---|
| Entrega parcial (2.1) y retiro en domicilio (2.2) | Backend y web completos, con migraciones y pruebas. Producción verificada con un retiro real |
| Auditoría de seguridad | SSRF en avisos, XSS en recepción, subida de logos, JWT sin vencimiento, clave por defecto de repartidores, cabeceras HTTP. `docs/Seguridad.md`, `SeguridadEndurecimientoTests` |
| API pública (3 pts) | Clave de pruebas (sandbox), límite de tasa por clave, documentación navegable v1. `docs/API_Publica.md`, `ApiPublicaTests` |
| Resiliencia (2 pts) | Redis, cola y base interrumpidos con tráfico real; dos defectos corregidos (19 s con Redis caído, 500 con excepción filtrada). `docs/Pruebas_de_Resiliencia.md` |
| Carga (2 pts) | Guion k6 con SLO como umbrales y sincronización simultánea; medido en local, cumple. `docs/Plan_de_Carga.md` |
| Merge de `frontend` (Felipe) | Rediseño del seguimiento público integrado con el soporte de retiro |
| Documentación | `Plan_de_Pruebas.md`, `Estado_de_Requerimientos.md`, ADR-003 a 008, `README.md`, `CLAUDE.md` |

## Decisiones tomadas por el usuario
- **Banderas de funcionalidad: NO se reclaman** (Joaquín compensó con otro opcional). No construir la pantalla. El backend ya desplegado queda como está.
- **No subir a `main` ni desplegar sin autorización explícita** (la de esta tanda fue retirada).
- El CI de GitHub está bloqueado por un problema de la cuenta (`billing issue`, plan Free, sin deudas); se espera respuesta de soporte (~1 semana). Mientras tanto se despliega con `scripts\desplegar.bat`.

## Pendiente, en orden de prioridad
1. **Aplicación móvil (Joaquín), riesgo para el monitoreo del 12/11**: la hoja de ruta mixta (escenario obligatorio para equipos de 4) necesita paradas de retiro en la app; hoy no están. Contrato: `docs/UC-Retiro-Movil.md`. También: entrega por bultos y enviar `X-Correlation-Id`. Decidir si lo implementa Joaquín o se hace en `desarrollo-backend` y se le avisa.
2. **Subir y desplegar** lo de esta tanda (con autorización). El despliegue lleva el `Caddyfile.prod` nuevo (cabeceras de seguridad) y `Avisos__HostsPermitidos` en `docker-compose.prod.yml`. Verificar luego: el receptor de prueba de avisos sigue entregando (SSRF), el login del backoffice funciona (cookie `Secure`) y el alta de repartidores exige contraseña.
3. **Carga sobre el ambiente remoto** (la letra lo pide) en una ventana acordada; repetir el corte de una instancia de producción.
4. **Optimización de paradas (2 pts, Joaquín)**: la app compara distancia base vs optimizada en ejecución; falta una tabla medida contra el criterio del RF 16 (franja, prioridad, antigüedad).
5. **Geocercas (3 pts, Joaquín)**: falta el análisis del impacto en batería.
6. **Terraform**: `import` + `plan` sin cambios (requiere `oci session authenticate`) y captura de costos.
7. Notificaciones con la app cerrada (letra 6.11, obligatorio): ADR-002 descarta FCM; riesgo de nota.
8. Opcionales menores: el retiro entregado no entra en la liquidación por comercio (la letra no lo exige: el RF 41 pide tarifa, seguimiento, avisos, tablero y reportes, que sí están); purga del outbox; límite de tasa distribuido.

## Prompt para retomar
> Estoy en `desarrollo-backend` con el trabajo de `docs/Traspaso_Pendientes.md`. No subas ni despliegues sin que lo autorice. Retomá desde el punto 1 (aplicación móvil: retiro) o el que indique; respondé en español, commits sin marcas, no toques la rama `mobile`, no hagas la pantalla de banderas.
