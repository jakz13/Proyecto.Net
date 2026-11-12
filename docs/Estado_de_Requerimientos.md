# Estado de requerimientos al 12/11 (actualizado tras la auditoría del 12/11)

Leyenda: **Cumplido** (con evidencia en código y pruebas) · **Parcial** (existe pero falta algo concreto) · **Pendiente**.

## Cambio de requerimientos (obligatorio)
| Requerimiento | Estado | Evidencia / qué falta |
|---|---|---|
| 2.1 Entrega parcial (RF 31 a 36) | Cumplido en backend, web y app | `EntregaParcialBultosTests`, `PersistenciaEntregaParcialTests`. La app tiene el flujo por bultos y la cola offline (`RegistroEntregaViewModel`, `SyncService`, commit 979456c); verificar en dispositivo antes de la defensa |
| 2.2 Retiro en domicilio (RF 37 a 41) | Cumplido en backend, web y app; **liquidación del retiro pendiente** | `RetiroEnDomicilioApiTests`, `RetiroDominioTests`. La app registra retiros y no realizados (`RegistroRetiroViewModel`). El retiro no entra en la liquidación por comercio (la letra no lo exige) |
| Registro de decisión del cambio | Cumplido | ADR-005 |

## Requerimientos no funcionales obligatorios
| Requerimiento | Estado | Nota |
|---|---|---|
| 6.1 Arquitectura, prueba de arquitectura y ADR | Cumplido | `ReglasDeArquitecturaTests`, `docs/adr` |
| 6.2 Capa web (Razor y Blazor) con ADR de renderizado | Cumplido | ADR 02 del documento de análisis |
| 6.3 Seguridad | Cumplido con salvedades | Autenticación propia (JWT + `PasswordHasher`), declarada en ADR-006 (no es OIDC ni el almacén de Identity). Auditoría del 12/11: XSS almacenado reparado en 46 manejadores y 2 pantallas | Autorización por perfil corregida y probada. El límite de tasa cuenta por instancia (con 2 instancias el límite real es el doble) |
| 6.4 Persistencia y concurrencia optimista | Cumplido | Migraciones y `ConcurrenciaYAislamientoApiTests` |
| 6.5 Multitenancy | Cumplido | Pruebas entre operadores y entre comercios |
| 6.6 Configurabilidad versionada | Cumplido | Cuadro tarifario y reglas operativas versionados |
| 6.7 Caché distribuido | Cumplido | ADR-003, métrica por caso de uso en Grafana |
| 6.8 Mensajería, outbox, idempotencia, fallidos | Cumplido | ADR-004. No se purga el outbox procesado |
| 6.9 Avisos a comercios | Cumplido | Firma, reintentos, fallidos, panel y receptor de prueba |
| 6.10 Tiempo real con SignalR en varias instancias | Cumplido | Respaldo en Redis |
| 6.11 Aplicación móvil | **Parcial** | Notificaciones con la app **cerrada**: ADR-002 descarta FCM y usa un servicio en primer plano, que no cubre la app cerrada del todo. Riesgo de calificación |
| 6.12 Observabilidad | Cumplido con una salvedad | La app móvil aún no envía `X-Correlation-Id` |
| 6.13 Escalabilidad y balanceo | Cumplido | Dos instancias con Caddy y chequeos de salud |
| 6.14 Despliegue e IaC | **Parcial** | Terraform para Azure (`infra/terraform-azure`) validado, con `plan` real de 8 recursos y costo estimado, **sin aplicar** (no se duplicó el ambiente); falta ensayar destruir/reconstruir. CI/CD hoy bloqueado por la cuenta de GitHub |
| 6.15 Calidad | Cumplido | 1017 pruebas del backend y 111 del frontend |

## Opcionales (mínimo 12 puntos)
| Opcional | Puntos | Estado |
|---|---|---|
| Cobertura superior al 70 % | 2 | Cumplido: 76 % medido y publicado por el pipeline |
| Geocercas (Joaquín) | 3 | **Casi completo**: implementado y con análisis (`docs/Geocercas_Bateria.md`: costo medido del cálculo y muestreo adaptativo); **falta completar la tabla de consumo medida en un teléfono** |
| Optimización del orden de paradas (Joaquín) | 2 | **Cumplido**: 2-opt en la app y comparación medida contra el criterio del RF 16 (`docs/Optimizacion_Paradas.md`, ahorro de 31 % a 60 %) |
| Banderas de funcionalidad | 2 | **Ahora con pantalla** (Backoffice → Plataforma → Funcionalidades, perfil `AdministradorPlataforma`) además de la API. Antes: **no se reclamaba**: se acordó no entregarlo (pocos puntos para lo que exige) y Joaquín lo compensó con otro opcional. El backend existe y funciona (`/api/plataforma/operadores`), pero sin pantalla de administración no cumple el requisito |
| API pública para comercios | 3 | **Cumplido**: claves por comercio, ruta versionada, ambiente de pruebas (clave sandbox), límite de tasa por clave y documentación navegable. Ver `docs/API_Publica.md` y `ApiPublicaTests` |
| Pruebas de resiliencia | 2 | **Cumplido** (con una repetición pendiente): Redis, RabbitMQ y Postgres interrumpidos con tráfico real, esperado vs observado y dos defectos corregidos. Falta repetir sobre una instancia de producción en la defensa. Ver `docs/Pruebas_de_Resiliencia.md` |
| Pruebas de carga | 2 | **Parcial**: plan, SLO y 4 escenarios medidos en local; sobre el ambiente remoto ya se midió la consulta pública (p95 44,7 ms, 0 errores 5xx). **Falta la sincronización simultánea remota** (necesita la contraseña de demo del servidor). Ver `docs/Plan_de_Carga.md` |

Puntos que hoy se pueden defender sin trabajo adicional: **2** (cobertura). Los opcionales reclamados son: cobertura, geocercas, optimización de paradas, API pública, resiliencia y carga; hay que cerrar los parciales.
