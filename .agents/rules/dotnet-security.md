---
trigger: always_on
---

# Reglas de Seguridad y Arquitectura (.NET / Última Milla)

## 1. Arquitectura de Capas
- `Domain`: Entidades ricas, enums y excepciones de dominio (cero dependencias externas, ni siquiera de EF).
- `Application`: Commands/Queries con sus handlers (MediatR), DTOs y puertos (interfaces) que implementa Infrastructure.
- `Infrastructure`: DbContext (EF Core), migraciones, repositorios, Unit of Work, caché, outbox y adaptadores externos.
- `Api`: Minimal API (endpoints, autenticación/autorización, middlewares) y composición de DI (`AddApplicationServices`, `AddInfrastructure`).

## 2. Seguridad Obligatoria
- **No Hardcode Secrets**: Usar `dotnet user-secrets` en desarrollo y Key Vault / Env Vars en producción.
- **Prevención de Vulnerabilidades**:
  - Validar siempre que el usuario tenga acceso al **recurso específico** (Anti-IDOR): operador y comercio del token contra el recurso pedido.
  - Usar LINQ o `FromSqlInterpolated` para evitar SQL Injection.
  - Habilitar antiforgery tokens / CSRF middleware en mutaciones con sesión (Backoffice y Portal).
  - Configurar Headers de Seguridad HTTP (CSP, HSTS, X-Frame-Options).
- **Logging Seguro**: Nunca registrar objetos completos de request/response ni campos PII sin enmascarar.

## 3. Git & Commits
- Mensajes de commit siempre en **español** siguiendo Conventional Commits (`feat`, `fix`, `security`, etc.).
- Nunca commitear secretos ni datos reales de producción.
- Nunca commitear sin autorizacion.
