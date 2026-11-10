# Traspaso: estado al 9/11 y pendientes (para retomar)

Rama de trabajo: `desarrollo-backend` (commits locales, **sin subir**). `main` sigue en `71a3821`. Suite: 1017 pruebas del backend + 111 del frontend, todas en verde.

## Hecho en esta sesión (todo commiteado)
| Tema | Resultado |
|---|---|
| Merge de la rama `mobile` | Conflicto semántico resuelto (conflictos offline + entrega por bultos) |
| Entrega parcial (2.1) | Estaba guardada en diccionarios estáticos: ahora son columnas con migración y prueba en Postgres real. La migración crea además la tabla `ConflictosSincronizacion` (faltaba) |
| Retiro en domicilio (2.2) | Backend completo, pantallas de portal y backoffice, rutas mixtas con capacidad a lo largo del recorrido, conflictos offline, seguimiento público (`RET-`), avisos v2, tablero y reporte. 15 pruebas de integración + 48 de dominio |
| Seguridad | Autorización por perfil en **toda** la API (antes un comercio podía listar usuarios/comercios/reglas), fuga entre comercios en el detalle de envío, endpoint anónimo de reinicio de demo, paquete con vulnerabilidad alta |
| Arquitectura/calidad | Pruebas NetArchTest (10), cobertura 76 % medida y publicada por el CI con umbral 70 %, auditoría de paquetes, `CLAUDE.md` |
| Documentación | `README.md`, ADR-003 a ADR-008 e índice en `docs/adr/` |
| Banderas de funcionalidad | Backend completo + prueba. Usuario `plataforma@gmail.com` / `Plataforma.2026!` |

## Pendiente, en orden de prioridad
1. **Decidir si se sube** (`desarrollo-backend` y `main`) y desplegar. Al desplegar corren 3 migraciones (`PersistirEntregaParcial`, `RetiroEnDomicilio`, `BanderasDeFuncionalidad`). Verificar luego en producción: login de las 6 cuentas, alta de un retiro, `/api/funcionalidades`.
2. **Aplicación móvil (Joaquín)**: paradas de retiro, registro con escaneo/foto/firma sin conexión, enviar `X-Correlation-Id`, ocultar según `GET /api/funcionalidades`. Contrato: `docs/UC-Retiro-Movil.md`.
3. **Pantalla de administración de banderas** en el backoffice (solo `AdministradorPlataforma`); hoy se usa por API (`/api/plataforma/operadores`).
4. **API pública (3 pts)**: falta límite de tasa por clave (hoy por IP), clave de pruebas/sandbox y documentación navegable.
5. **Pruebas de resiliencia (2 pts)**: tests con Redis y RabbitMQ caídos + documento esperado vs observado.
6. **Pruebas de carga (2 pts)**: scripts k6 y ejecución sobre el ambiente remoto (incluir sincronización simultánea de repartidores).
7. **Optimización de paradas (2 pts)**: falta la comparación medida contra el criterio del RF 16.
8. **Geocercas (3 pts)**: falta el análisis del impacto en batería.
9. **Liquidación**: el retiro entregado todavía no entra en la liquidación por comercio (sí en el reporte de retiros).
10. `docs/Plan_de_Pruebas.md` y `docs/Estado_de_Requerimientos.md` (referenciados en el README, **aún no escritos**).
11. Terraform: `import` + `plan` sin cambios (requiere sesión de Oracle) y captura de costos.
12. Notificaciones con la app cerrada (letra 6.11, obligatorio): ADR-002 de Joaquín descarta FCM; riesgo de nota.
13. Límite de tasa por instancia (con 2 instancias el límite real es el doble): decidir si se implementa distribuido o se documenta.

## Prompt para retomar
> Estoy en `desarrollo-backend` con el trabajo descrito en `docs/Traspaso_Pendientes.md`. Retomá desde el punto 1: si te autorizo, subí a ambas ramas y desplegá, verificá producción; después escribí `docs/Plan_de_Pruebas.md` y `docs/Estado_de_Requerimientos.md` con el estado real de cada requerimiento, y seguí con los puntos 3 a 6. Respondé en español, commits sin marcas, no toques la rama `mobile`.
