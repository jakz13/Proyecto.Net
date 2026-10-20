---
trigger: always_on
---

# Estructura del Proyecto (Última Milla)

##
El proyecto sigue **Clean Architecture** con **CQRS** (ADR-001), dentro de un monolito modular en .NET 10:

```
UltimaMilla.sln
├── src/Domain          → Entidades ricas, enums, excepciones de dominio. Sin dependencias de otras capas ni de frameworks.
├── src/Application     → Casos de uso (Commands/Queries + Handlers de MediatR), DTOs y puertos (interfaces).
├── src/Infrastructure  → EF Core (AppDbContext, migraciones, repositorios), servicios externos. Implementa los puertos.
├── src/Api             → Minimal API (Program.cs, Endpoints/). Sin lógica de negocio.
├── src/Web             → Backoffice (Razor Pages), PortalComercio y SeguimientoPublico (Blazor), contratos/infra del frontend.
├── src/Mobile          → App del repartidor (.NET MAUI, MVVM, offline-first).
└── tests               → Pruebas automáticas (Domain.Tests, Application.Tests).
```

**Regla central:** una sola clase por concepto. Las entidades de `Domain` son ricas (encapsulan sus invariantes y transiciones) y EF Core las
mapea directamente. No existe una "entidad de datos" separada ni conversores `ToNegocio()`/`FromNegocio()`. Lo que cruza hacia `Api` y los
clientes son DTOs de `Application`, nunca entidades de dominio.

**Reglas de dependencias entre capas (a verificar con una prueba de arquitectura NetArchTest, ver ADR-001):**
- `Domain` no depende de ninguna otra capa.
- `Application` depende solo de `Domain`. Define los puertos (`IEnvioRepository`, `IUnitOfWork`, etc.).
- `Infrastructure` depende de `Application` y `Domain`, e implementa los puertos.
- `Api` depende de `Application` e `Infrastructure` (solo para la composición de dependencias con `AddApplicationServices()` y `AddInfrastructure()`).
  Los endpoints hablan con `IMediator`, nunca con repositorios ni con `AppDbContext`.

**Flujo de un caso de uso:** endpoint → `IMediator.Send(Command/Query)` → handler → entidad de dominio (reglas y transiciones) → puerto de repositorio →
`IUnitOfWork.SaveChangesAsync`. Toda regla de negocio vive en `Domain` o en el handler, nunca en el endpoint.

**Persistencia:** los repositorios de `Infrastructure` exponen operaciones explícitas (`AddAsync`, `UpdateAsync`) y el handler confirma con
`IUnitOfWork.SaveChangesAsync`. La concurrencia optimista se resuelve con `SelloModificacion` (`IHasConcurrencyToken`).

**Multitenancy:** aislamiento por fila con filtros globales de EF a partir de `ICurrentTenant` (ADR-002). Las entidades de alcance de operador
implementan `IMustHaveTenant` y las de alcance de comercio `IMustHaveComercioTenant`.
