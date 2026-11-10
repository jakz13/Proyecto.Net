# Plan de pruebas

## 1. Estrategia
Pirámide con pruebas automáticas que corren en cada push del pipeline (GitHub Actions), más pruebas de carga y de resiliencia sobre el ambiente desplegado.

| Nivel | Proyecto | Qué verifica | Cantidad |
|---|---|---|---|
| Unitarias de dominio | `tests/UltimaMilla.Domain.Tests` | Máquinas de estado (envío, bulto, retiro), reglas de negocio, política de conflicto, capacidad a lo largo del recorrido | 174 |
| Unitarias de aplicación | `tests/UltimaMilla.Application.Tests` | Casos de uso con repositorios simulados: caminos felices, errores, idempotencia | 724 |
| Arquitectura | `tests/UltimaMilla.Architecture.Tests` | Regla de dependencias entre capas (NetArchTest); falla el pipeline si se viola | 10 |
| Integración | `tests/UltimaMilla.Integration.Tests` | API completa contra Postgres real, con sesiones reales (JWT) por perfil | 109 |
| Frontend | `tests/UltimaMilla.Frontend.Tests` | Componentes y clientes del frontend | 111 |

## 2. Pruebas que respaldan los requerimientos obligatorios
| Requerimiento | Prueba |
|---|---|
| Aislamiento entre operadores (6.5) | `AislamientoMultitenantApiTests`, `ConcurrenciaYAislamientoApiTests` |
| Aislamiento entre comercios del mismo operador | `AutorizacionPorPerfilApiTests.DosComerciosDelMismoOperador_*` |
| Autorización por perfil (6.3) | `AutorizacionPorPerfilApiTests` (matriz de endpoints permitidos y denegados) |
| Concurrencia optimista (6.4) | `ConcurrenciaYAislamientoApiTests` |
| Persistencia de la entrega parcial entre instancias | `PersistenciaEntregaParcialTests` |
| Retiro en domicilio de punta a punta (2.2) | `RetiroEnDomicilioApiTests` |
| Banderas de funcionalidad por operador | `FuncionalidadesPorOperadorApiTests` |
| Máquina de estados declarativa (RF 11) | `MaquinaEstadosEnvioTests`, `RetiroDominioTests` |
| Regla de dependencias (6.1) | `ReglasDeArquitecturaTests` |

## 3. Ejecución
```bash
docker compose up -d postgres
dotnet test UltimaMilla.Backend.slnf -c Release
dotnet test tests/UltimaMilla.Frontend.Tests
```
Las pruebas de integración se omiten (no fallan) si no hay Postgres. Comparten una base: cada una debe crear sus propios datos y no depender del estado de otras.

## 4. Cobertura
Medida con coverlet y publicada por el pipeline con ReportGenerator (artefacto `cobertura` y resumen en cada ejecución). Última medición local de dominio + aplicación: **76 % de líneas**, 63 % de ramas. El pipeline falla si baja del 70 %.
```bash
dotnet test UltimaMilla.Backend.slnf -c Release --collect:"XPlat Code Coverage" --results-directory ./resultados
reportgenerator -reports:"resultados/**/coverage.cobertura.xml" -targetdir:cobertura -assemblyfilters:"+UltimaMilla.Domain;+UltimaMilla.Application"
```

## 5. Seguridad
`dotnet list package --vulnerable --include-transitive` corre en el pipeline y lo hace fallar ante vulnerabilidades altas o críticas.

## 6. Escenarios de la defensa (letra 9.3)
| Escenario | Cómo se prueba |
|---|---|
| Interrumpir una instancia de la API con tráfico | `docker stop shippingbox-api1-1` y consultar `/ready`: responde con la otra |
| App móvil sin conexión y sincronización | Ver ADR-001 y la aplicación móvil |
| Conflicto de sincronización | `RetiroEnDomicilioApiTests` (conflicto de retiro) y ADR-001 |
| Dos operadores con configuración distinta | Cuentas `admin@gmail.com` y `admin2@gmail.com` (tarifas distintas) |
| Modificar tarifa y regla en caliente | Backoffice → Tarifas / Reglas operativas |
| Detener el receptor de avisos y recuperar | Receptor de prueba `/api/testing/webhook-receiver` y panel de avisos |
| Destruir y reconstruir el ambiente | `terraform destroy` / `apply` (**pendiente de ejecutar contra la cuenta real**) |

## 7. Pendientes de este plan
- **Pruebas de carga** (opcional): escenarios k6 de concurrencia, rampa y duración sobre el ambiente remoto, con objetivos de nivel de servicio y el caso de sincronización simultánea de repartidores. **No ejecutadas.**
- **Pruebas de resiliencia** (opcional): interrumpir Redis, la cola y una instancia y documentar el comportamiento esperado y el observado. **No ejecutadas.**
- **Extremo a extremo con navegador** (opcional): no implementadas.
