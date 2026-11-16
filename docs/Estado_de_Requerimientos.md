# Estado de requerimientos

Leyenda: **Cumplido** (con evidencia en código y pruebas) · **Parcial** (implementado, con algún punto pendiente) · **Pendiente**.

## 1. Cambio de requerimientos (obligatorio)
| Requerimiento | Estado | Evidencia / qué falta |
|---|---|---|
| 2.1 Entrega parcial (RF 31 a 36) | Cumplido en backend, web y app | `EntregaParcialBultosTests`, `PersistenciaEntregaParcialTests`. La app móvil tiene el flujo por bultos y la cola offline (`RegistroEntregaViewModel`, `SyncService`) |
| 2.2 Retiro en domicilio (RF 37 a 41) | Cumplido en backend, web y app (liquidación pendiente) | `RetiroEnDomicilioApiTests`, `RetiroDominioTests`. La app registra retiros y no realizados (`RegistroRetiroViewModel`). La liquidación por comercio todavía contempla solo envíos |
| Registro de decisión del cambio | Cumplido | `docs/adr/ADR-005-cambio-de-requerimientos-entrega-parcial-y-retiro.md` |

## 2. Requerimientos no funcionales obligatorios
| Requerimiento | Estado | Nota |
|---|---|---|
| 6.1 Arquitectura, prueba de arquitectura y ADR | Cumplido | `ReglasDeArquitecturaTests` (NetArchTest) y `docs/adr/` |
| 6.2 Capa web (Razor Pages y Blazor WASM) con ADR | Cumplido | `ADR 02` del Documento de Análisis y Diseño |
| 6.3 Seguridad | Cumplido con salvedades | Autenticación propia con JWT + `PasswordHasher` (`ADR-006`). Sesión única activa para repartidores (`PoliticaSesionUnica`) y aviso en tiempo real al resto de sesiones. El límite de tasa cuenta por instancia |
| 6.4 Persistencia y concurrencia optimista | Cumplido | Migraciones EF Core y `ConcurrenciaYAislamientoApiTests` |
| 6.5 Multitenancy (operador y comercio) | Cumplido | Filtros globales y pruebas de aislamiento entre operadores y entre comercios |
| 6.6 Configurabilidad versionada | Cumplido con salvedad | `CuadroTarifario` y `ReglaOperativa` versionados (`CuadroTarifario` queda fijado en `Envio`; `ReglaOperativa` se evalúa según la versión vigente para envíos aún abiertos) |
| 6.7 Caché distribuido | Cumplido | `ADR-003`, Redis con métricas por caso de uso en Grafana |
| 6.8 Mensajería, outbox, idempotencia y fallidos | Cumplido | `ADR-004`. Como mejora futura queda purgar filas ya procesadas del outbox |
| 6.9 Avisos a comercios | Cumplido | Firma HMAC, reintentos, cola de fallidos, panel en portal y receptor de prueba |
| 6.10 Tiempo real con SignalR en varias instancias | Cumplido | Respaldo (*backplane*) en Redis |
| 6.11 Aplicación móvil | **Parcial** | Notificaciones con la app **cerrada**: `ADR-002` descarta Firebase (FCM) y usa SignalR + servicio en primer plano, por lo que si el usuario mata la app por completo no recibe avisos hasta reabrirla. Distribución OTA en `/descargas` implementada (`ADR-007`) |
| 6.12 Observabilidad | Cumplido | Serilog, OpenTelemetry, Prometheus, Grafana (9 paneles) y `X-Correlation-Id` enviado desde la app móvil (`AuthDelegatingHandler`) hasta el Worker |
| 6.13 Escalabilidad y balanceo | Cumplido | Dos instancias de la API detrás de Caddy con chequeos de salud en `/ready` (corte en vivo medido en `Pruebas_de_Resiliencia.md`) |
| 6.14 Despliegue e IaC | Cumplido | Terraform de Azure (`infra/terraform-azure`, 8 recursos) aplicado y ensayado: destrucción en ≈ 2 min 20 s y reconstrucción completa en ≈ 8 min; CI/CD funcionando (`ghcr.io` + despliegue por SSH en `main`). Salvedad: `terraform.tfstate` local en la PC |
| 6.15 Calidad | Cumplido | **1.225 pruebas en backend** (0 fallas) y **111 en frontend** (`docs/auditoria/ESTADO.md`) |

## 3. Opcionales (mínimo 12 puntos)
| Opcional | Puntos | Estado |
|---|---|---|
| Cobertura superior al 70 % | 2 | **Cumplido**: **89,4 % de líneas y 73,2 % de ramas** sobre Dominio y Aplicación, publicado por el pipeline |
| Geocercas con análisis de batería | 3 | **Cumplido**: implementado y medido en teléfono físico Android 14 (`docs/Geocercas_Bateria.md` y `docs/Analisis_Geocercas_y_Consumo_Bateria.md`, ahorro de 45 % frente a GPS continuo) |
| Optimización del orden de paradas (2-opt) | 2 | **Cumplido**: 2-opt en la app y comparación medida contra RF 16 (`docs/Optimizacion_Paradas.md`, ahorro de 31 % a 60 %) |
| Banderas de funcionalidad | 2 | **Cumplido**: API, historial, pruebas (`FuncionalidadesPorOperadorApiTests`) y pantalla en Backoffice → Plataforma → Funcionalidades |
| API pública para comercios | 3 | **Cumplido**: claves por comercio, rutas `v1`, sandbox, límite de tasa diferenciado y Swagger (`docs/API_Publica.md`, `ApiPublicaTests`) |
| Pruebas de resiliencia | 2 | **Cumplido**: Redis, RabbitMQ, PostgreSQL, caída de `api1` en producción y caída/recuperación del receptor externo de webhooks medidos (esperado vs observado en `docs/Pruebas_de_Resiliencia.md`) |
| Pruebas de carga | 2 | **Casi completo**: 4 escenarios medidos en local; en Azure medidos `seguimiento` (`p95 44,7 ms`) y **`sync` con 10 repartidores** (`p95 1,37 s`, `40/40` checks OK). Falta correr `alta` en remoto (`docs/Plan_de_Carga.md`) |
