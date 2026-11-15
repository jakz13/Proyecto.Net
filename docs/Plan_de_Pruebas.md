# Plan de pruebas

## 1. Estrategia general
Combinamos pruebas automatizadas en cada cambio (unitarias, de arquitectura, de integración y de frontend) con pruebas de carga y de resiliencia sobre el ambiente desplegado.

| Nivel | Proyecto | Qué verifica |
|---|---|---|
| Unitarias de dominio | `tests/UltimaMilla.Domain.Tests` | Máquinas de estado (envío, bulto, retiro), reglas de negocio, conflictos y capacidad de ruta |
| Unitarias de aplicación | `tests/UltimaMilla.Application.Tests` | Casos de uso con repositorios simulados: caminos felices, errores e idempotencia |
| Arquitectura | `tests/UltimaMilla.Architecture.Tests` | Regla de dependencias entre capas (NetArchTest), compose y scripts; falla el pipeline si se viola |
| Integración | `tests/UltimaMilla.Integration.Tests` | API completa contra PostgreSQL real, con sesiones reales (JWT) por perfil |
| Backoffice | `tests/UltimaMilla.Backoffice.Tests` | Páginas, formularios, código de barras, sesiones y tiempo real del Backoffice |
| Frontend | `tests/UltimaMilla.Frontend.Tests` | Componentes y clientes del frontend |

En total la suite reúne **1.225 pruebas en backend** (0 fallas, ver `docs/auditoria/ESTADO.md`) y **111 en frontend**.

## 2. Pruebas clave por requerimiento obligatorio
| Requerimiento | Prueba que lo verifica |
|---|---|
| Aislamiento entre operadores (6.5) | `AislamientoMultitenantApiTests`, `ConcurrenciaYAislamientoApiTests` |
| Aislamiento entre comercios del mismo operador | `AutorizacionPorPerfilApiTests.DosComerciosDelMismoOperador_*` |
| Autorización por perfil (6.3) | `AutorizacionPorPerfilApiTests`, `PermisosPorPerfilApiTests` |
| Sesión única de repartidores y tiempo real | `PoliticaSesionUnicaTests`, `SesionUnicaActivaApiTests`, `CambiosEnTiempoRealApiTests` |
| Concurrencia optimista (6.4) | `ConcurrenciaYAislamientoApiTests` |
| Entrega parcial entre instancias (2.1) | `PersistenciaEntregaParcialTests` |
| Retiro en domicilio de punta a punta (2.2) | `RetiroEnDomicilioApiTests` |
| Banderas de funcionalidad por operador | `FuncionalidadesPorOperadorApiTests` |
| Máquina de estados declarativa (RF 11) | `MaquinaEstadosEnvioTests`, `RetiroDominioTests` |
| Regla de dependencias (6.1) | `ReglasDeArquitecturaTests` |

## 3. Cómo correrlas localmente
```bash
docker compose up -d postgres
dotnet test UltimaMilla.Backend.slnf -c Release
dotnet test tests/UltimaMilla.Frontend.Tests
```
*Nota:* si PostgreSQL no está levantado, las pruebas de integración se omiten. Como comparten una base, cada prueba crea sus propios datos para no depender de otras.

## 4. Cobertura y seguridad en el pipeline
- **Cobertura:** medida con Coverlet y publicada por ReportGenerator en `$GITHUB_STEP_SUMMARY` y en el artefacto `cobertura`. Medición actual sobre Dominio + Aplicación: **89,4 % de líneas y 73,2 % de ramas** (el pipeline falla si baja del 70 %).
```bash
dotnet test UltimaMilla.Backend.slnf -c Release --collect:"XPlat Code Coverage" --results-directory ./resultados
reportgenerator -reports:"resultados/**/coverage.cobertura.xml" -targetdir:cobertura -assemblyfilters:"+UltimaMilla.Domain;+UltimaMilla.Application"
```
- **Vulnerabilidades:** `dotnet list package --vulnerable --include-transitive` corre en el pipeline y falla ante vulnerabilidades altas o críticas.

## 5. Escenarios de demostración para la defensa
| Escenario | Cómo se prueba |
|---|---|
| Caída de una instancia de la API | `docker stop shippingbox-api1-1` y consultar `/ready`: responde con `api2` |
| App móvil sin conexión y conflictos | Ver `ADR-001` y `RetiroEnDomicilioApiTests` |
| Dos operadores con distinta configuración | Cuentas `admin@gmail.com` y `admin2@gmail.com` |
| Cambiar tarifa o regla en caliente | Backoffice → Tarifas / Reglas operativas |
| Detener receptor de avisos y recuperar | Receptor de prueba `/api/testing/webhook-receiver` y panel de avisos |
| Destruir y reconstruir el ambiente | `scripts\reconstruir.ps1` (`terraform destroy` + `apply` en `infra/terraform-azure/`, ≈ 8 min) |

## 6. Otros documentos de pruebas y pendientes
- **Carga:** `docs/Plan_de_Carga.md` (`seguimiento` y `sync` medidos en Azure; falta `alta` en remoto).
- **Resiliencia:** `docs/Pruebas_de_Resiliencia.md` (Redis, RabbitMQ, PostgreSQL y caída de `api1` en producción medidos; falta probar en vivo el receptor externo).
- **Seguridad:** `docs/Seguridad.md`.
- **API pública:** `docs/API_Publica.md`.
- **Extremo a extremo con navegador (Playwright):** no implementadas (opcional).
